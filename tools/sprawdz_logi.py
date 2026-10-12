"""Progi etapu 2 z logu Armoury (docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md rozdz. 1 i "169c"; uwaga M12: kazdy prog jedna regula odczytu).

Uzycie (Python 3, tylko odczyt):
    python -I tools/sprawdz_logi.py --grupa etap2 <Armoury-*.log> [--baza baza.json] [--zapisz-baze baza.json] [--csv budzet-rodow.csv] [--start-kampanii N]
--grupa etap2     : progi etapu 2.
--grupa etap3     : (3.1, E3/0) progi fali 1 etapu 3 (reszta ksiegi, RB3, linia "Szczelnosc (164a)") i kontrolki "prog 0" fali 2 tego drzewa.
--grupa etap4     : (E4/0, paczka 188) progi T1-T10 pomiaru ludzi, jencow i zimy (projekt etapu 4, rozdz. 3 "E4/0" Test) w oknie dob kampanii 1-120
                    (--do-doby N zmienia okno); T9 (bieg zimowy) i baza konca etapu (d300-364) - INFO. Gdy w logu jest linia startowa
                    "PeopleUnit (E4/3)" - takze progi E4/3 (jednostka 1/k, punkty powrotu, tabela krain, pula wyrzutkow i bandy wobec biegu E4/2);
                    stan jednostki - z dziennej linii "Ludzie (ksiega)" (MCM), nie z linii startowej (ta powstaje przed MCM).
                    Gdy jest linia startowa "Devastation (E4/5)" - takze progi E4/5 S1-S21 (plan kodu E4/5 rozdz. 11; S10/S11 z pliku
                    <katalog logu>/<znacznik>/ludzie-regiony.csv; S6, S14, S19-S21 - INFO, porownanie z biegiem E4/4 i autotesty osobno).
                    Gdy jest linia startowa "Famine (E4/6)" - takze progi E4/6 F1-F13 i F9b (spichlerze okregow, glod, uchodzcy z glodu, H w przyroscie;
                    progi konca etapu dla zimy F6/F7: zadna warownia AI nie glodzi przed 30. doba zimy, mediana zapasu U27 >= 364 dni - wiaza
                    tylko w biegu z zima, w biegu zwyklym INFO; F8 "glodne okregi tylko spustoszone/oblezone" - doby bez zimy). S10/S11 (E4/5) pomijaja
                    wiersze region-doba z konto_e46 = 1 w spichlerze-regiony.csv (E4/6 zmienil konto uchodzcow regionu tej doby).
                    Gdy jest linia startowa "Weterani (E4/10): wpiete" - takze progi E4/10 V1-V18 (karczma tylko z puli weteranow; plan kodu E4/10
                    rozdz. 9; V8, V12-V14, V17, V18 - INFO; V10 i V11 - wiersze E4/7 T3/T2/T6 z ta sama linia, tu tylko karczma E4/7 = 0).
                    Gdy jest linia startowa "VillageBook (E4/11)" - takze progi E4/11 G1-G14 i G-ETAP (plan kodu E4/11 rozdz. 9; linia dzienna
                    "Ksiega wiosek (E4/11)", ognie z <katalog logu>/<znacznik>/wioski.log, G-ETAP z <katalog logu>/<znacznik>/wioski-okregi.csv - wiaze
                    w biegu 728 dob, krotszy bieg - INFO).
                    Gdy jest linia startowa "Krainy kanonu (E4/9): wpiete" - takze progi E4/9 K1-K25 (plan kodu E4/9 rozdz. 11: Straz z Westeros i dozywotnio,
                    H1, konto NIEWOLNI i bilans otwarcia Zelaznych, jency z rabunku z ludzi wsi, DLUG 4, Wolni Ludzie; K7 wobec biegu A zbioru 31, K11/K15 - tylko
                    nowa kampania, K23/K24 - autotest zapisu i grupy etap2/etap3 osobno).
--grupa etap5     : (E5/0, paczka 195) szkielet etapu 5 (projekt etapu 5 rozdz. 4): wiersze etapu jako INFO "stan wyjsciowy" (panowie samych zamkow
                    z budzet-rodow.csv, sakiewki w dniach zoldu, zloto swiata, magazyny wsi, wojsko) + blok E5/0 (progi E50-T1..T10 - kiesa ludu na sucho,
                    kontrola zamknieta, koszt, bitwy wobec ksiegi i decyzja A6, wydatek z petli BK wobec ConsumedValue, notable, gotowka wsi wedlug
                    polityki, odczyty do kalibracji B) w oknie dob kampanii 1-120 (--do-doby N). Kolejne paczki dopisuja wlasne bloki etap5_<paczka>.
                    Blok E5/A1 (111', kasy miast) A1-T1..T22 - dopisek " | E5/A1:" w linii "Kasy miast (169c)",
                    "Przeplywy osad (kasy miast/zamkow)", "Zawor zamkow (110)", przyczyny bilansu, linia startowa i przyciecia "TownPurse (E5/A1)";
                    okno doby kampanii 2-120 (--do-doby N). Wolany z etap5_main E5/0 (scalenie porcji A).
                    Bloki E5/A2 (162, dwor pelny) A2-1..A2-11 - linia sprzetu niewydanego w "Budzet rodow (166)",
                    przelew do dworu w dobach 31-120 (--do-doby N), zgodnosc 166 = Obieg, reszta ksiegi, glowy < 5 000, hak B1 = 0, gracz bez dworu,
                    bledy; A2-10/A2-11 zapas "wlasnego" (sprzet do wlasnej siedziby wraca jako wlasne, nie ziemia - linia D staly);
                    panowie zamkow (budzet-rodow.csv) - INFO. E5/A3 (163 + 164c) A3-1..A3-14 - linie "Markietani (E5/A3)" i "Notable (E5/A3)":
                    sakiewki <= 15 dni zoldu (Obieg), markietani > 0 i podzial 85/15, koszt, pasmo notabli i nowi notable bez zlota z niczego / w nicosc
                    (Obieg), kapital karawan notabli = korekta zrodel bilansu, kontrolki prog 0, karawany notabli d40/d120 (INFO), bledy, reszta ksiegi.
                    E5/A4 (196, R22) A4-1..A4-8 - linia "Oblezone (E5/A4)": zaplacone mimo oblezenia = 0 (renta miast, Crown Duty, War Tax, Road Tolls),
                    wstrzymane i oblezenia (INFO), zawor zamkow i zawor A1 bez oblezonych (INFO), "10 miast bez placenia" (gdy A1 w logu), prog podatkow
                    zamkow ponad zapasem kupcow (INFO), bledy.
                    E5/A5 (197, R23) A5-1..A5-7 - BetterEconomy: prefiksy Try* wplaty wpiete (linia startowa BEE 170), dopisek E5/A5 w linii
                    kampanii BEE (wylacznik, okno kwot 2/2, opcje daru 2/2), kasy prawdziwym pieniadzem (INFO), linie "BEE (E5/A5) wplata|dar" -
                    gracz traci tyle, ile dostaje kasa osady / notable (ROZNICA 0; autotest nie klika menu - linie z recznego testu Jeffa), A5-4b
                    wplata do zapasu "wlasnego" (cala; w liniach "D staly" tyle, ile wplat przed nimi), odmowy (INFO), bledy BeeGifts i potkniecia 0.
                    Wolane z etap5_main E5/0 (scalenie porcji A).
                    Blok E5/A6 (198, lup z kiesy pokonanego) prog "bitwy z niczego netto
                    = 0 +- 1 tys./d" (srednia 28 dob): z linii "Pieniadz swiata (bilans - przyczyny)" ZRODLA "bitwy" - UJSCIA "bitwy" + N4 (zloto partii
                    bez wodza w oknie bitew) - statki zatopione wyplacone z niczego (linia "Lup w bitwie (A6)" tej doby; statki to paczka B5). Linia A6 - INFO.
                    Wolany z etap5_main E5/0 (scalenie porcji A).
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


def etap3_main(logp, start_arg):
    """3.1 (31, E3/0): progi fali 1 etapu 3 (projekt PROJEKT-ETAP3-SZCZELNOSC-STATKI, rozdz. 5) i kontrolki "prog 0" kawalkow fali 2 tego drzewa.
    Fala 1: |R| srednia 28 dob < 10 000 (w dobie 40 i na koncu), RB3 |srednia| < 500, probki swiata wokol okien "rozjazd 0", linia "Szczelnosc (164a)"
    codziennie, potkniecia 0, bledy 0; pomiar statkow, U8r i Innych - liczby (INFO). Fala 2 (gdy kawalki w logu): "bez platnika", "miasto ponizej zapasu
    kupcow", "bez odbiorcy" = 0.
    Poprawka po recenzji 3.1+3.3: "ARYTMETYKA OK" to tozsamosc algebraiczna (R jest zdefiniowane z tych samych sum, ar1 = ar2 = 0 zawsze) - nie wykryje
    bledu znaku ani pominiecia pozycji (S31/U31/N31); zostaje jako INFO. Dowod szczelnosci: R, RB i probki swiata (zmiana swiata wobec pozycji nazwanych)."""
    lines = read(logp)
    rep = Report()
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    sea = by_day(lines, "Szczelnosc (164a): dzien")
    days = sorted(cau)
    start = start_arg
    if start is None:
        for s in lines:
            if s.startswith("Pomiary 169c: okna"):
                start = num(r"start kampanii dzien (\d+)", s)
                if start is not None:
                    break
    if start is None:
        start = days[0] if days else 0
    print(f"plik: {logp} | doby z linia przyczyn: {len(days)} | start kampanii {start}")
    # reszta ksiegi - srednia 28 dob z linii (pierscien w kodzie) i RB3 z kontroli
    avg = {d: num(r"srednia 28 dob ([+-]?\d+)", s) for d, s in cau.items()}
    d40 = [d for d in days if run_day(d, start) == 40]
    if d40 and avg.get(d40[0]) is not None:
        v = avg[d40[0]]
        rep.add("reszta R - srednia 28 dob w dobie 40", v, "|R| < 10 000 (prog etapu 3; gdy nie - plan B rozdz. 7)", abs(v) < 10000)
    if days and avg.get(days[-1]) is not None:
        v = avg[days[-1]]
        rep.add("reszta R - srednia 28 dob (ostatnia doba)", f"{v} (doba {run_day(days[-1], start)})", "|R| < 10 000", abs(v) < 10000)
    else:
        rep.add("reszta R - srednia 28 dob", "brak linii", "|R| < 10 000", None)
    rb3 = [num(r"\bRB3=(-?\d+)", cau[d]) for d in last_window(days)]
    rb3 = [x for x in rb3 if x is not None]
    if rb3:
        m = st.mean(rb3)
        rep.add("RB3 (od budow do korony) - srednia 28 dob", f"{m:.0f}", "|sr.| < 500 (portfele wezwan nazwane)", abs(m) < 500)
    bad = [d for d in days if "ARYTMETYKA OK" not in cau[d]]
    rep.add("arytmetyka linii przyczyn (tozsamosc - bez mocy dowodowej)", f"bledy w {len(bad)} z {len(days)} dob", "INFO (dowod: R, RB, probki)", None)
    # prawdziwa kontrola pozycji nazwanych (takze S31/U31/N31 - Seal31.AddWorld idzie do NamedRun): probki swiata wokol okien, rozjazd 0
    win = by_day(lines, "Obieg: okna (kontrolka): dzien")
    prb = [num(r"probki swiata wokol okien \[P\]: \d+ \(zgodne \d+; rozjazd (\d+)", win[d]) for d in sorted(win)]
    prb = [x for x in prb if x is not None]
    rep.add("probki swiata wokol okien - rozjazd (suma dob)", sum(prb) if prb else "brak probek", "0", (sum(prb) == 0) if prb else None)
    # linia 164a codziennie, potkniecia 0
    miss = [d for d in days if d not in sea]
    rep.add("linia Szczelnosc (164a) codziennie", f"brak w {len(miss)} z {len(days)} dob", "0", len(miss) == 0 if days else None)
    stum = sum(num(r"potkniecia (\d+)", sea[d], int, 0) or 0 for d in sea)
    rep.add("Szczelnosc (164a) - potkniecia", stum, "0", stum == 0 if sea else None)
    # pomiar (INFO): U8r, portfele wezwan, statki, lupienie cial, Inni - srednie 28 dob
    def mean_of(pat, src):
        vals = [num(pat, src[d]) for d in last_window(sorted(src))]
        vals = [x for x in vals if x is not None]
        return st.mean(vals) if vals else None
    for name, pat, src in (("U8r - kiesy partii usunietych bez zniszczenia", r"kiesy partii usunietych bez zniszczenia \(U8r\) (\d+)", cau),
                           ("portfele wezwan - wplaty do ujemnego portfela", r"wplaty do ujemnego portfela wezwanego (\d+)", cau),
                           ("portfele wezwan - dlug sprzed paczki", r"dlug portfela sprzed paczki ze skarbcow (\d+)", cau),
                           ("statki: drugi zwrot tej samej partii w dobie", r"DRUGI zwrot tej samej partii w dobie (\d+)", sea),
                           ("statki zatopione w bitwach morskich (PlunderedGold)", r"bitwach morskich \(PlunderedGold\) (-?\d+)", sea),
                           ("lupienie cial: bitwy z wywolaniami > 3 x ciala", r"bitew z wywolaniami > 3 x ciala (\d+)", sea),
                           ("Inni: reszta wplywow bez nazwy", r"Inni: .*?\+ reszta ([+-]?\d+)", sea)):
        m = mean_of(pat, src)
        rep.add(name + " (srednia 28 dob)", f"{m:.0f}" if m is not None else "brak linii", "liczba (wejscie fali 3 / E3/8d)", None)
    # fala 2 - kontrolki "prog 0" tego drzewa (gdy kawalki w logu)
    for name, pat in (("platnik: bez platnika", r"bez platnika (\d+)"), ("platnik: miasto ponizej zapasu kupcow przez nasza wyplate", r"ponizej zapasu kupcow przez nasza wyplate (\d+)"),
                      ("awanse: bez odbiorcy", r"awanse \(164a-4\): .*?bez odbiorcy \(prog 0\) (\d+)"), ("kapital: bez odbiorcy", r"kapital \(164a-6\): .*?bez odbiorcy \(prog 0\) (\d+)")):
        vals = [num(pat, sea[d]) for d in sea]
        vals = [x for x in vals if x is not None]
        rep.add(name + " (suma)", sum(vals) if vals else "brak linii", "0", (sum(vals) == 0) if vals else None)
    wer = [num(r"bez odbiorcy \(prog 0\) (\d+); ochotnicy", s) for s in lines if s.startswith("Werbunek: dzien")]
    wer = [x for x in wer if x is not None]
    rep.add("werbunek: bez odbiorcy (suma)", sum(wer) if wer else "brak linii", "0", (sum(wer) == 0) if wer else None)
    errs = [s for s in lines if s.startswith("ERROR in ")]
    rep.add("bledy Armoury (ERROR in)", len(errs), "0", len(errs) == 0)
    rep.show()
    return 0


# ------------------------------------------------------------ E5/A6 (198): blok etap5 - lup w bitwie tylko z kiesy pokonanego
# scalenie porcji A: dawna minimalna etap5_main A6 jako blok etap5_a6 wolany z etap5_main E5/0 (okno: doby kampanii 1-until)
def etap5_a6(rep, lines, start, win, until=120, **kw):
    """E5/A6 (198): prog "bitwy z niczego netto = 0 +- 1 tys./d" (projekt etapu 5, rozdz. 3 "E5/A6" Test). Niezalezny pomiar z ksiegi pieniadza:
    ZRODLA "bitwy" (wyplata gry zwyciezcom - zdarzenie z niczego) - UJSCIA "bitwy" (wodzowie pokonanych - gra i A6) + N4 (kiesy partii bez wodza
    w oknie bitew: zwyciezcy +, pokonani - 3.2) - statki zatopione wyplacone z niczego (B5 - z linii "Lup w bitwie (A6)", bo 164a-0 liczy tez
    udzialy, ktorych gra nie wyplaca). Srednia 28 ostatnich dob. Linia A6 (wlasny rachunek paczki) i 164a-0 - INFO."""
    inwin = lambda d: 1 <= run_day(d, start) <= until
    cau = {d: x for d, x in by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien").items() if inwin(d)}
    a6 = {d: x for d, x in by_day(lines, "Lup w bitwie (A6): dzien").items() if inwin(d)}
    days = sorted(cau)
    land, ships164, miss, fb = {}, {}, 0, 0
    for d in days:
        s = cau[d]
        src = num(r"statki NavalDLC inne \d+, bitwy (\d+),", s)
        out = num(r"dostep do targu \d+\), bitwy (\d+), inne", s)
        n4 = num(r"\bN4=(-?\d+)", s)
        sh = num(r"wyplacone z niczego (\d+)", a6[d]) if d in a6 else None
        ships164[d] = num(r"informacja 164a-0: (-?\d+)", s)
        if sh is None and ships164[d] is not None:
            sh = ships164[d]; fb += 1                  # log sprzed A6 (bez linii A6) - statki z 164a-0 (z udzialami bez wyplaty - zawyza statki)
        if src is None or out is None or n4 is None or sh is None:
            miss += 1
            continue
        land[d] = src - out + n4 - sh
    win = last_window(land)
    if win:
        m = st.mean(land[d] for d in win)
        worst = max(win, key=lambda d: abs(land[d]))
        rep.add("E5/A6: bitwy z niczego netto na ladzie (ksiega; srednia 28 dob)", f"{m:+.0f}/d (najgorsza doba {land[worst]:+d}; dob {len(win)}"
                + (f"; statki z 164a-0 w {fb} dobach - log bez linii A6" if fb else "") + ")",
                "0 +- 1 tys./d", abs(m) <= 1000)
    else:
        rep.add("E5/A6: bitwy z niczego netto na ladzie (ksiega; srednia 28 dob)", f"brak linii (dob bez kompletu: {miss})", "0 +- 1 tys./d", None)
    def mean_a6(pat):
        vals = [num(pat, a6[d]) for d in last_window(a6)]
        vals = [x for x in vals if x is not None]
        return st.mean(vals) if vals else None
    for name, pat in (("A6: netto z niczego na ladzie (wlasny rachunek)", r"netto z niczego na ladzie ([+-]?\d+)"),
                      ("A6: bez A6 byloby z niczego", r"bez A6 byloby ([+-]?\d+)"),
                      ("A6: wodzowie zdjeci z partii zaplacili", r"wodzowie zdjeci z partii \(A6 - niewola, ucieczka\) (\d+)"),
                      ("A6: obciete zwyciezcom (pokonany bez zlota)", r"obciete zwyciezcom \(pokonany bez zlota albo poza swiatem\) (\d+)"),
                      ("A6: zaliczone wodzom - oddane przy pojmaniu (RealisticCaptivity; recenzja A6 (1))", r"przy pojmaniu - RealisticCaptivity\) (\d+)"),
                      ("A6: w tym gracz - zaliczka z pojmania", r"przy pojmaniu - RealisticCaptivity\) \d+ \(w tym gracz (\d+)\)"),
                      ("A6: podstawa 1/9 korony z czesci wodzow (178; recenzja A6 (2a))", r"zgloszona do podstawy (\d+)"),
                      ("A6: statki zatopione wyplacone z niczego (B5)", r"wyplacone z niczego (\d+)")):
        v = mean_a6(pat)
        rep.add(name + " (srednia 28 dob)", f"{v:.0f}" if v is not None else "brak linii", "INFO", None)
    s164 = [ships164[d] for d in last_window(days) if ships164.get(d) is not None]
    rep.add("statki zatopione w bitwach (164a-0, z udzialami bez wyplaty; srednia 28 dob)", f"{st.mean(s164):.0f}" if s164 else "brak linii", "INFO", None)
    stum = sum(num(r"wyjatki zlapane (\d+)", a6[d], int, 0) or 0 for d in a6)
    rep.add("A6: wyjatki zlapane (suma)", stum, "INFO (0 oczekiwane)", None)


# ------------------------------------------------------------ E4/0 (188): progi paczki pomiaru ludzi, jencow i zimy (projekt etapu 4, rozdz. 3 "E4/0" Test)
E4_KINDS = ["partie rodow", "garnizony", "milicje", "bandy", "karawany", "tabory wsi", "inne"]
F_ = r"(-?\d+(?:\.\d+)?)"
SF = r"([+-]?\d+(?:\.\d+)?)"


def e4_series(lines, prefix, shift=0):
    """{dzien wedlug ksiegi ludzi (dzien gry - 1): linia} - shift=1 dla linii z dniem gry (Wyrzutki, Przegrani (H3), Prawo jenca (I1), Budzet rodow (166))."""
    d = {}
    for s in lines:
        if s.startswith(prefix):
            k = num(r"dzien (\d+)", s)
            if k is not None:
                d[k - shift] = s
    return d


def fnum(pat, s, default=None):
    if s is None:
        return default
    m = re.search(pat, s)
    if not m:
        return default
    try:
        return float(m.group(1))
    except ValueError:
        return default


def e4_campfever_by_krainy(lines):
    """Zmarli z zarazy z linii "CampFever:" (dzien w tej linii to doba oblezenia) przypisani do najblizszej NASTEPNEJ linii "Krainy (4.15)" (CampFever
    biegnie na poczatku ticku dobowego, ksiega ludzi na koncu) -> {dzien Krainy: zmarli}."""
    out, acc = {}, 0
    rx = re.compile(r"^CampFever: \S+ dzien \d+ - oblegajacy \d+ chorych/(\d+) zmarlych, obroncy \d+/(\d+)")
    for s in lines:
        m = rx.match(s)
        if m:
            acc += int(m.group(1)) + int(m.group(2))
            continue
        if s.startswith("Krainy (4.15): dzien"):
            k = num(r"dzien (\d+)", s)
            if k is not None:
                out[k] = acc
            acc = 0
    return out


def etap4_main(logp, start_arg, until=120):
    """E4/0 (188): progi T1-T10 z projektu etapu 4 (rozdz. 3 "E4/0" Test) mierzalne w biegu 120 dob (okno: doby kampanii 1-until, domyslnie 120).
    T9 (bieg zimowy B) i baza konca etapu (wojsko lordow d300-364, zalogi d364) wymagaja innego biegu / 364 dob - INFO.
    Doba kampanii linii E4/0 = dzien z linii - start + 1 (linie z dniem gry - Wyrzutki, H3, I1, Budzet - normalizowane o -1)."""
    lines = read(logp)
    rep = Report()
    hea = e4_series(lines, "Hearth (E4/0): dzien")
    pro = e4_series(lines, "Dobrobyt (E4/0): dzien")
    par = e4_series(lines, "Partie (E4/0): dzien")
    jen = e4_series(lines, "Jency (E4/0): dzien")
    kra = e4_series(lines, "Krainy (4.15): dzien")
    swl = e4_series(lines, "Straz i Wolni Ludzie (4.15): dzien")
    zim = e4_series(lines, "Zima (E4/0): dzien")
    lud = e4_series(lines, "Ludzie: dzien")
    zal = e4_series(lines, "Zalogi: przyrost bez werbunku (169c): dzien")
    inn = e4_series(lines, "Wzrost Innych (169c): dzien")
    e42 = e4_series(lines, "Inni z poleglych (E4/2): dzien")       # E4/2: wighty z odbitych (poza oknem W8), milicja przejetej osady
    wyr = e4_series(lines, "Wyrzutki: dzien", 1)
    pwl = e4_series(lines, "Pula wyrzutkow (E4/3): dzien", 1)     # recenzja E4/9 R1-4: odplyw puli do Strazy (T7)
    h3 = e4_series(lines, "Przegrani (H3): dzien", 1)
    i1 = e4_series(lines, "Prawo jenca (I1): dzien", 1)
    bud = e4_series(lines, "Budzet rodow (166): dzien", 1)
    start = start_arg
    if start is None:
        for s in lines:
            if s.startswith("Pomiary 169c: okna"):
                start = num(r"start kampanii dzien (\d+)", s)
                if start is not None:
                    break
    alld = sorted(hea)
    if start is None:
        start = alld[0] if alld else 0
    win = [d for d in alld if 1 <= run_day(d, start) <= until]
    print(f"plik: {logp} | doby z linia Hearth (E4/0): {len(alld)} (doby kampanii {run_day(alld[0], start) if alld else '-'}-{run_day(alld[-1], start) if alld else '-'})"
          f" | start kampanii {start} | okno progow: doby kampanii 1-{until} ({len(win)} dob)")
    if not alld:
        rep.add("linia Hearth (E4/0)", "brak linii", "codziennie (PeopleMeasureEnabled + LogEnabled)", False)
        rep.show()
        return 0
    measured = [d for d in win if "RESZTA" in hea[d]]          # bez pierwszej doby sesji (migawka)

    # ---------------- T1: bledy, linia startowa, potkniecia
    errs = [s for s in lines if s.startswith("ERROR in ")]
    rep.add("T1: bledy naszych modow (ERROR in)", len(errs), "0", len(errs) == 0)
    stl = next((s for s in lines if s.startswith("Pomiar ludzi (E4/0): okna wpiete:")), None)
    if stl is None:
        rep.add("T1: linia startowa Pomiar ludzi (E4/0)", "brak linii", "jest, BRAK: -, wlasciciele 11 celow", False)
    else:
        brak = re.search(r"; BRAK: (.*?); wlasciciele", stl)
        ncel = num(r"wlasciciele latek na (\d+) celach", stl)
        ok = brak is not None and brak.group(1).strip() == "-" and ncel == 11
        rep.add("T1: linia startowa Pomiar ludzi (E4/0)", f"BRAK: {brak.group(1) if brak else '?'}; celow {ncel}", "BRAK: - i wlasciciele 11 celow", ok)
        own = re.search(r"wlasciciele latek na \d+ celach: (.*?) \| dane 169c", stl)
        rep.add("T1: wlasciciele latek 11 celow (U25)", own.group(1)[:400] if own else "?", "lista (obce latki na celach pomiaru)", None)
    stum_days = [s for s in lines if s.startswith("Pomiar ludzi (E4/0): potkniecia dzis")]
    stum = sum(num(r"potkniecia dzis (\d+)", s, int, 0) or 0 for s in stum_days)
    stum += sum(num(r"\| potkniecia (\d+)", par[d], int, 0) or 0 for d in win if d in par)
    sites = []
    for s in stum_days:
        m = re.search(r"\(sesja \d+; (.*)\) - pierwszy blad", s)
        if m:
            for part in m.group(1).split(", "):
                nm = part.rsplit(" ", 1)[0]
                if nm not in sites:
                    sites.append(nm)
    rep.add("T1: potkniecia E4/0 (okno)", f"{stum}" + (f" (miejsca: {', '.join(sites[:8])})" if sites else ""), "0 (najwyzej 5 w biegu, z nazwa miejsca)", stum <= 5)
    for name, src in (("Hearth", hea), ("Dobrobyt", pro), ("Partie", par), ("Jency", jen), ("Krainy", kra), ("Straz i Wolni Ludzie", swl), ("Zima", zim)):
        miss = [d for d in win if d not in src]
        rep.add(f"linia {name} codziennie", f"brak w {len(miss)} z {len(win)} dob", "0", len(miss) == 0)

    # ---------------- T2: budzet hearth zamkniety
    rest = [fnum(r"RESZTA " + SF, hea[d]) for d in measured]
    rest = [x for x in rest if x is not None]
    if rest:
        share = sum(1 for x in rest if abs(x) <= 1.0) / len(rest)
        mean = st.mean(rest)
        mabs = st.mean(abs(x) for x in rest)
        worst = sorted(((abs(fnum(r"RESZTA " + SF, hea[d]) or 0), run_day(d, start)) for d in measured), reverse=True)[:3]
        rep.add("T2: |RESZTA| <= 1 hearth/d", f"{100 * share:.1f}% dob ({len(rest)} dob; najgorsze: " + ", ".join(f"d{w[1]} {w[0]:.2f}" for w in worst) + ")",
                ">= 95% dob", share >= 0.95)
        rep.add("T2: RESZTA srednio", f"{mean:+.3f} hearth/d (srednia |RESZTA| {mabs:.3f})", "|srednia| <= 0.2 hearth/d (siew poza RESZTA - U10)", abs(mean) <= 0.2)
    else:
        rep.add("T2: RESZTA", "brak dob z bilansem (tylko pierwsza doba sesji?)", "|RESZTA| <= 1 w >= 95% dob", None)
    nov = [fnum(r"powrot z puli w regionie bez wsi \(zeszli z puli, hearth bez zmian\) " + F_, hea[d]) or 0 for d in measured]
    rep.add("powrot z puli w regionie bez wsi (srednio, ludzie/d)", f"{st.mean(nov):.2f}" if nov else "-", "INFO - ludzie znikaja z puli bez hearth (do E4/3)", None)

    # ---------------- T3: rzad wielkosci zrodel hearth (3x od szacunku projektu = blad okna albo przyczyna do opisania)
    def mean_pat(pat, days):
        v = [fnum(pat, hea[d]) for d in days]
        v = [x for x in v if x is not None]
        return st.mean(v) if v else None
    d30 = [d for d in measured if run_day(d, start) <= 30]
    # recenzja E4/3: stan jednostki z dziennej linii "Ludzie (ksiega)" (MCM), nie z linii startowej (ta powstaje przed MCM)
    e43_state = e43_unit_state(lines, win)
    e43 = e43_state[0] in ("on", "mixed")
    for name, pat, days, exp in (("T3: stala gry - baza (d1-30)", r"stala gry: baza " + SF, d30, 720.0),
                                 ("T3: tabory wsi (hearth/d)", r"- tabory wsi -" + F_, measured, 220.0),
                                 ("T3: rozbite tabory do swojej wsi (H3, hearth/d)", r"rozbite tabory do swojej wsi \+" + F_, measured, 165.0),
                                 ("T3: wyrzutki w las - bieda (hearth/d)", r"wyrzutki w las \(bieda\) -" + F_, measured, 13.0)):
        mixed = False
        if e43 and "stala gry" not in name:
            exp, name = exp / E43_UNIT_RATIO, name + " [E4/3: 1/k - szacunek E4/0 / 95]"
            if e43_state[0] == "mixed":
                mixed, name = True, name + " [jednostka wlaczana i wylaczana w biegu - INFO]"
        m = mean_pat(pat, days)
        rep.add(name, f"{m:.1f}" if m is not None else "brak linii", f"ok. {exp:.3g} (od {exp / 3:.3g} do {exp * 3:.3g})",
                (exp / 3 <= abs(m) <= exp * 3) if m is not None and not mixed else None)
    dec = [fnum(r"dekret Growth BK " + SF, hea[d]) or 0 for d in measured]
    rep.add("T3: dekret Growth BK (suma)", f"{sum(dec):.2f}" if dec else "-", "0 (dekret tylko z konsoli)", (abs(sum(dec)) < 0.01) if dec else None)
    flo = [(fnum(r"podloga 10 \+" + F_, hea[d]) or 0, num(r"podloga 10 \+[\d.]+ w (\d+) wsiach", hea[d], int, 0) or 0) for d in measured]
    rep.add("T3/T10: podloga 10 (hearth, wsio-dni)", f"{sum(x[0] for x in flo):.2f} hearth w {sum(x[1] for x in flo)} wsio-dniach" if flo else "-", "INFO - pojedyncze wsie (R17)", None)
    odd = [fnum(r"niezgodnosc ticku z modelem " + SF, hea[d]) or 0 for d in measured]
    rep.add("T3: niezgodnosc ticku wsi z modelem (suma)", f"{sum(odd):.2f}" if odd else "-", "0 (inny prefiks na Village.DailyTick?)", (abs(sum(odd)) < 0.5) if odd else None)

    # ---------------- T4: Inni - miejsca = nekromancja, skasowani = wzieci przez Innych
    ctl = [num(r"kontrola z oknem: razem - nekromancja = (-?\d+)", inn[d]) for d in win if d in inn]
    ctl = [x for x in ctl if x is not None]
    unk = sum(abs(num(r"miejsce nieznane - wyjatek ([+-]?\d+)", inn[d], int, 0) or 0) for d in win if d in inn)
    # kontrola != 0 tylko gdy PlaceOf rzucil wyjatek (nekromancja poza "razem") albo klucz PeopleMeasureEnabled zmieniony w trakcie doby
    rep.add("T4: nekromancja wedlug miejsca - kontrola (suma |roznic|)", (f"{sum(abs(x) for x in ctl)} (miejsce nieznane - wyjatek: {unk})") if ctl else "brak linii (169c wylaczone?)",
            "0", (sum(abs(x) for x in ctl) == 0) if ctl else None)
    bey = [re.search(r"IsBeyondTheWall ROT: (TAK|BRAK)", inn[d]) for d in win if d in inn]
    bey = [m.group(1) for m in bey if m]
    rep.add("T4: IsBeyondTheWall ROT (miejsce \"za Murem\")", (f"TAK w {bey.count('TAK')} z {len(bey)} dob") if bey else "brak odcinka E4/0 w Wzrost Innych",
            "TAK codziennie (BRAK = \"za Murem\" zawsze 0 - podzial do E4/2 bez wartosci)", (bey.count("TAK") == len(bey)) if bey else None)
    cl = [re.search(r"jency Innych skasowani przez ROT (\d+) \(w nicosc; wzieci przez Innych w oknie jencow E4/0 (\d+)\)", inn[d]) for d in win if d in inn]
    cl = [(int(m.group(1)), int(m.group(2))) for m in cl if m]
    if cl:
        a, b = sum(x[0] for x in cl), sum(x[1] for x in cl)
        rep.add("T4: jency Innych skasowani przez ROT = wzieci przez Innych (W8 = W10)", f"skasowani {a}, wzieci {b}", "roznica 0", a == b)
    else:
        rep.add("T4: jency Innych skasowani / wzieci", "brak linii", "roznica 0", None)

    # ---------------- T5: zaraza - Krainy (z rosteru + nietrafione) = linie CampFever
    cf = e4_campfever_by_krainy(lines)
    t5 = []
    for d in win:
        if d not in kra:
            continue
        m = re.search(r"zaraza razem dzis (\d+) \(z rosteru\), nietrafione HitRandom (\d+)", kra[d])
        if m:
            t5.append((int(m.group(1)), int(m.group(2)), cf.get(d, 0)))
    if t5:
        dead, unhit, camp = sum(x[0] for x in t5), sum(x[1] for x in t5), sum(x[2] for x in t5)
        bad = sum(1 for x in t5 if x[0] + x[1] != x[2])
        rep.add("T5: zmarli z zarazy (Krainy) + nietrafione = CampFever", f"{dead} + {unhit} = {dead + unhit} wobec CampFever {camp} (dob niezgodnych {bad})", "kontrola 0", dead + unhit == camp and bad == 0)
    else:
        rep.add("T5: zaraza", "brak linii Krainy", "kontrola 0", None)

    # ---------------- T6: Straz i Wolni Ludzie - reszta rownania <= 10% obrotu (28 dob)
    e49_on = any(x.startswith("Straz z Westeros (E4/9): dzien") for x in lines)
    for g in ("Nocna Straz", "Wolni Ludzie"):
        if g == "Nocna Straz" and e49_on:
            # E4/9 (11.2): ruch Strazy ma od E4/9 wlasna linie z kontrola kodu (Mur, pule, ludnosc, dozywotnio) - prog w bloku E4/9 (K8)
            rep.add("T6: Nocna Straz - reszta / obrot", "ocena na kontroli linii \"Straz z Westeros (E4/9)\" - wiersz E4/9 K8", "<= 10% (K8)", None)
            continue
        rx = re.compile(re.escape(g) + r": stan (\d+) \(partie (\d+), zalogi (\d+)\), zmiana ([+-]\d+) = zwerbowani (\d+) - zabici (\d+) - zmarli z zarazy (\d+) - dezercja (\d+) \+ nowe partie (\d+) \+ reszta ([+-]\d+)")
        rows = []
        for d in last_window([x for x in win if x in swl]):
            m = rx.search(swl[d])
            if m:
                v = [int(x) for x in m.groups()]
                rows.append((abs(v[9]), v[4] + v[5] + v[6] + v[7] + v[8]))
        if rows:
            r_, t_ = st.mean(x[0] for x in rows), st.mean(x[1] for x in rows)
            rep.add(f"T6: {g} - reszta / obrot (28 dob)", f"{r_:.1f} / {t_:.1f} = " + (f"{100 * r_ / t_:.1f}%" if t_ > 0 else "-"), "<= 10% (albo nazwana w linii)",
                    (r_ <= 0.10 * t_) if t_ > 0 else None)
        else:
            rep.add(f"T6: {g}", "brak linii", "<= 10%", None)

    # ---------------- T7: ksiega ludzi pod bronia + pula + jency (metoda C 4) i rownanie jencow
    def L(d, pat, src):
        return fnum(pat, src.get(d)) or 0.0
    t7, comp, t7j = [], {}, []
    for d in [x for x in win if x in lud and (x - 1) in lud and x in hea and "RESZTA" in hea[x]]:
        def state(dd):
            s = lud[dd]
            sold = sum(fnum(re.escape(k) + r" (\d+) \(\d+ partii", s) or 0 for k in E4_KINDS)
            m = re.search(r"jency w partiach (\d+), w lochach osad (\d+) \| wyrzutki w puli (\d+)", s)
            return sold + (sum(int(x) for x in m.groups()) if m else 0)
        dS = state(d) - state(d - 1)
        s_lud = lud[d]
        newp = {}
        mp = re.search(r"nowe \(ludzie przy utworzeniu\): (.*?) \| zniknely", par.get(d, ""))
        if mp:
            for nm, n_, men in re.findall(r"([a-zA-Z ]+?) (\d+) \((-?\d+)\)", mp.group(1)):
                newp[nm.strip()] = int(men)
        src = {
            "werbunek (bez wcielonych jencow AI)": L(d, r"zwerbowani (\d+) \(", lud) - L(d, r"wcieleni jency AI \(partie rodow\) (\d+)", h3),
            "zalogi od notabli (bez podwojnych z Ludzie)": max(0.0, L(d, r"werbunek gry od notabli " + SF, zal) - L(d, r"od notabli bez wodza - garnizony, karawany (\d+)", lud)),
            "zalogi z modelu": L(d, r"przyrost z modelu zalogi gry \(bez werbunku i bez zlota\) " + SF, zal),
            "milicja z modelu": L(d, r"milicja \(E4/0\): z modelu w ticku " + SF, zal),
            "nowe partie (rody, karawany, konwoje, Inni, inne)": sum(newp.get(k, 0) for k in ("partie rodow", "karawany", "konwoje BK", "Inni", "inne")),
            "Inni: nekromancja i rajd": L(d, r"nekromancja ROT w oknie OnMapEventEnded " + SF, inn) + L(d, r"rajd ROT \(Hearth/2 z niczego\) " + SF, inn)
                                        + L(d, r"wstali zabici przy spustoszeniu \(E4/5, K7 - nie z niczego\) " + SF, inn),   # E4/5 po recenzji: K7 osobno w oknie W9
            "Inni E4/2: wighty z odbitych, zywi z odbicia osady": L(d, r"\(f\) odbici jency: .*?-> wstalo (\d+)", e42) + L(d, r"odbicie osady \(ROT: [^)]*\) \d+ \(\+(\d+) zywych", e42),
            "tabory i lodzie z hearth": L(d, r"- tabory wsi -[\d.-]+ \(nowe \d+, uzupelnienia \d+; ludzi (-?\d+)", hea) + L(d, r"- lodzie rybackie -[\d.-]+ \(nowe \d+, uzupelnienia \d+; ludzi (-?\d+)", hea),
            "pula z hearth (rabunki, bieda)": L(d, r"rabunki (\d+), bieda/wojna", wyr) + L(d, r"bieda/wojna " + F_, wyr),
            "jency z rabunku BK": L(d, r"z rabunku BK \+(\d+)", jen),
            "pojmani z wsi (E4/9, z konta UCHODZCY)": L(d, r"pojmani z wsi \(E4/9\) \+(\d+)", jen),
        }
        snk = {
            "zabici": L(d, r"dzis: zabici (\d+)", lud),
            "zaraza": L(d, r"zaraza razem dzis (\d+)", kra),
            "H3 domy (BK, wies, szablon, wighty)": L(d, r"domy: do domu BK (\d+)", h3) + L(d, r"do wsi \(tabory\) (\d+) ludzi", h3) + L(d, r"do wsi \(prosci i bandyci\) (\d+) ludzi", h3)
                                                   + L(d, r"hearth, z szablonu (\d+) \(garnizony", h3) + L(d, r"nigdy do BK ani wsi\): rozbici (\d+)", h3) + L(d, r"nigdy do BK ani wsi\): rozbici \d+, inna droga (\d+)", h3),
            "pula do hearth i z szablonu": L(d, r"\| powrot do wsi " + F_, wyr) + L(d, r"powrot z puli:.*?z szablonu " + F_, wyr) + L(d, r"powrot z puli:.*?wighty do niczego " + F_, wyr),
            # recenzja E4/9 R1-4: brat Strazy z puli (WatchLaw.Levy krok 2, OutlawLaw._pWatch) - wchodzi do zrodel jako rekrut (zwerbowani, zalogi/milicja z modelu,
            # nowe partie), a stan sie nie zmienia (partia +1, pula -1); bez tego odplywu kazdy taki brat daje reszcie T7 -1
            "pula do Strazy (E4/9)": L(d, r"do Strazy \(E4/9\) " + F_, pwl),
            # E4/10: w linii I1 po "znikneli N" stoi teraz ", do karczmy (E4/10)" - wzorzec przyjmuje oba uklady (stary log bez E4/10 tez)
            "I1 (znikneli, wighty, straceni, niewolnicy)": L(d, r"znikneli (\d+), (?:do karczmy|Inni)", i1) + L(d, r"Inni \(wighty - do niczego\) (\d+), straceni", i1) + L(d, r"straceni \([^)]*\) (\d+)", i1)
                                                          + L(d, r"krainy z niewola: niewolnikow (\d+)", i1),
            # E4/10 (recenzja R1-2/R2-1): rozbici H3 + uwolnieni jency (suma w linii H3) i I1, ktorzy staneli w karczmie - zeszli ze stanu (sklad, loch), nie ma ich w domach;
            # zwolnieni 166 i rozwiazane partie juz w calosci ("zwolnieni: do wsi", "rozwiazane ... zdrowi"), najem z karczmy - w "zwerbowani"
            "E4/10: do karczmy (H3 rozbici + uwolnieni, I1)": L(d, r", do karczmy \(E4/10\) (\d+) \(rozbici", h3) + L(d, r", do karczmy \(E4/10\) (\d+) \(w tym", i1),
            "dezercja poza pula": max(0.0, L(d, r"dezercja (\d+) \(partie rodow", lud) - L(d, r"naplyw: dezercja (\d+)", wyr)),
            "zwolnieni 166 (do wsi i zniklo)": L(d, r"zwolnieni: do wsi (\d+) ludzi", bud) + L(d, r"zwolnieni: do wsi \d+ ludzi \([^)]*\), zniklo (\d+)", bud),
            "partie rozwiazane i usuniete (sklad, jency)": L(d, r"rozwiazane \d+ \(przy rozwiazaniu zdrowi (\d+)", par) + L(d, r"rozwiazane \d+ \(przy rozwiazaniu zdrowi \d+, ranni (\d+)", par)
                                                          + L(d, r"ranni \d+, jency (\d+) - znikaja", par) + L(d, r"usuniete bez zniszczenia \d+ \(ostatni stan (\d+)", par),
            "jency bez odbiorcy (gra) i skasowani przez ROT": L(d, r"bez odbiorcy \(gra usuwa[^)]*\) (\d+)", jen) + L(d, r"jency Innych skasowani przez ROT (\d+)", inn),
            "E4/2: odbici przez Innych (polegli), spalona milicja, wighty rozwiazanych band": L(d, r"odbici przez Innych - polegli \(E4/2\) (\d+)", jen) + L(d, r"\(d\) przejecie osady: .*?, spalone (\d+);", e42)
                                                          + L(d, r"\(e\) wighty rozwiazanych band - do niczego, nie do puli wyrzutkow (\d+)", e42),
        }
        res = dS - (sum(src.values()) - sum(snk.values()))
        t7.append(res)
        for k, v in src.items():
            comp.setdefault("+ " + k, []).append(v)
        for k, v in snk.items():
            comp.setdefault("- " + k, []).append(v)
        comp.setdefault("= zmiana stanu", []).append(dS)
        # rownanie jencow (szeregowi): zmiana = wzieci + z rabunku BK - zabrani z niewoli pokonanych - uwolnieni H3 - I1 - wcieleni - skasowani ROT - jency rozwiazanych
        j = jen.get(d, "")
        jd = num(r"\(zmiana ([+-]\d+)\)", j)
        if jd is not None:
            jin = L(d, r"wzieci do niewoli \+(\d+)", jen) + L(d, r"z rabunku BK \+(\d+)", jen) + L(d, r"pojmani z wsi \(E4/9\) \+(\d+)", jen)   # E4/9: nasze J nie sa juz w "z rabunku BK"
            jout = (L(d, r"z niewoli pokonanych zabrano (\d+)", jen) + L(d, r"uwolnieni jency bez odbiorcy (\d+)", h3)
                    + L(d, r"do domu (\d+) \(ludnosc BK", i1) + L(d, r"na Mur (\d+)", i1) + L(d, r"bez domu: do puli wyrzutkow (\d+)", i1) + L(d, r"znikneli (\d+), (?:do karczmy|Inni)", i1)
                    + L(d, r"Inni \(wighty - do niczego\) (\d+), straceni", i1) + L(d, r"straceni \([^)]*\) (\d+)", i1) + L(d, r"krainy z niewola: niewolnikow (\d+)", i1)
                    + L(d, r", do karczmy \(E4/10\) (\d+) \(w tym", i1)      # E4/10: I1 do karczmy (uwolnieni H3 do karczmy juz w "uwolnieni jency bez odbiorcy")
                    + L(d, r"wcieleni jency AI \(partie rodow\) (\d+)", h3) + L(d, r"werbunek: z puli \d+, jency (\d+)", wyr)
                    + L(d, r"jency Innych skasowani przez ROT (\d+)", inn) + L(d, r"ranni \d+, jency (\d+) - znikaja", par))
            t7j.append((jd - (jin - jout), jin + jout))
    if t7:
        m = st.mean(t7)
        rep.add("T7: reszta ksiegi ludzi pod bronia z kontem JENCY (srednio)", f"{m:+.1f} ludzi/d ({len(t7)} dob)", "|srednia| < 100/d (dzis ok. +283; inaczej okno przed E4/7)", abs(m) < 100)
        for k in sorted(comp):
            rep.add("T7 skladnik: " + k, f"{st.mean(comp[k]):.1f}/d", "INFO (srednia okna)", None)
    else:
        rep.add("T7: ksiega ludzi pod bronia", "brak kolejnych dob linii Ludzie + Hearth", "|srednia| < 100/d", None)
    if t7j:
        r_, t_ = st.mean(abs(x[0]) for x in t7j), st.mean(x[1] for x in t7j)
        rep.add("T7: rownanie jencow - reszta / obrot", f"{r_:.1f} / {t_:.1f} = " + (f"{100 * r_ / t_:.1f}%" if t_ > 0 else "-") + f" (srednia reszta {st.mean(x[0] for x in t7j):+.1f})",
                "<= 10% obrotu (inaczej okno przed E4/7: wymiana partia-zaloga, patrole BK, ucieczki, okupy)", (r_ <= 0.10 * t_) if t_ > 0 else None)
    else:
        rep.add("T7: rownanie jencow", "brak linii Jency", "<= 10% obrotu", None)

    # ---------------- T8: czas - (a) tekst projektu: linie Hearth + Partie + Jency; (b) zasada kosztu paczki (<= 30 ms/dobe): calosc mierzona -
    # wszystkie linie E4/0 + model hearth wolany w oknie W1 (571 wywolan na dobe z postfiksami BK i BKROT). Ciala pozostalych okien (W2-W11, W8/W9,
    # nasluchy) bez stopera - drobne odczyty na zdarzenie; ich koszt widac tylko w s/dobe (INFO nizej).
    tt, tall, tparts = [], [], {}
    for d in measured:
        a = fnum(r"\| czas " + F_ + r" ms \(linia Hearth", hea[d])
        if a is None:
            continue
        b = fnum(r"czas E4/0 w ksiedze ludzi " + F_ + " ms", par.get(d)) or 0
        c = fnum(r"\| czas " + F_ + " ms\\.", jen.get(d)) or 0
        tt.append(a + b + c)
        comp = {"linia Hearth": a,
                "model hearth w oknie W1": fnum(r"model hearth w oknie ticku wsi " + F_ + " ms", hea[d]) or 0,
                "Partie (migawka partii E4/0)": b,
                "Jency": c,
                "Dobrobyt": fnum(r"\| czas " + F_ + " ms\\.", pro.get(d)) or 0,
                "Zima": fnum(r"\| czas " + F_ + " ms\\.", zim.get(d)) or 0,
                "Krainy i Straz": fnum(r"czas linii Krainy i Straz " + F_ + " ms", swl.get(d)) or 0}
        for k, v in comp.items():
            tparts.setdefault(k, []).append(v)
        tall.append(sum(comp.values()))
    rep.add("T8: czas linii Hearth + Partie + Jency (srednio)", f"{st.mean(tt):.1f} ms/d (max {max(tt):.1f})" if tt else "brak", "<= 30 ms/dobe (tekst projektu)", (st.mean(tt) <= 30) if tt else None)
    rep.add("T8: calosc E4/0 - wszystkie linie + model hearth w oknie W1 (srednio)", f"{st.mean(tall):.1f} ms/d (max {max(tall):.1f})" if tall else "brak",
            "<= 30 ms/dobe (zasada kosztu paczki)", (st.mean(tall) <= 30) if tall else None)
    if tparts:
        rep.add("T8: skladniki calosci (srednio, ms/d)", ", ".join(f"{k} {st.mean(v):.1f}" for k, v in tparts.items()), "INFO", None)
    rep.add("T8: sekundy doby wobec ostatniego biegu 120 dob", "wymaga biegu bazowego tego drzewa", "<= +3% - INFO", None)

    # ---------------- T9: zima (bieg B) - bez progu, dane do E4/1 i E4/6
    wint = [s for s in lines if s.startswith("Klimat: dzien") and "nowa pora Winter" in s]
    wd = num(r"Klimat: dzien (\d+)", wint[0]) if wint else None
    rep.add("T9: Klimat - nowa pora Winter", f"doba kampanii {run_day(wd, start)}" if wd is not None else "brak zimy w biegu (bieg A - proba zimy pominieta)",
            "bieg B: najpozniej 10. doba - INFO", None)
    wdays = [d for d in win if d in zim and "ciecie plonu wsi dzis" in zim[d]]
    if wdays:
        cut = [fnum(r"swiat srednio " + F_ + "%", zim[d]) for d in wdays]
        cut = [x for x in cut if x is not None]
        rep.add("T9: ciecie plonu wsi zima (swiat, srednio)", f"{st.mean(cut):.1f}% w {len(wdays)} dobach zimy" if cut else "-", "INFO", None)
    food = [(run_day(d, start), fnum(r", " + SF + r"%\) \| warownie", zim[d])) for d in win if d in zim]
    food = [x for x in food if x[1] is not None]
    if food:
        rep.add("T9: zywnosc wsi swiata wobec pierwszej doby (d30/d60/d120)", ", ".join(f"d{n}: {v:+.1f}%" for n, v in food if n in (30, 60, 120)) or f"ostatnia d{food[-1][0]}: {food[-1][1]:+.1f}%",
                "INFO (dane do E4/1, E4/6)", None)
    rep.add("T9: zapas warowni AI (mediana, minimum) d30/d60/d120", "z ludzie-regiony.csv (zywnosc, bilans_zywnosci, gracz) - narzedzie biegu B", "INFO (U27: jedna definicja)", None)

    # ---------------- T10: odczyty bez progu (dane do E4/3, E4/4, E4/8)
    cul = sum(num(r"zmiana kultury osad: (\d+)", hea[d], int, 0) or 0 for d in measured)
    rep.add("T10: zmiany kultury osad (suma)", cul, "INFO - oczekiwane 0; > 0 = dowod do E4/3", None)
    conv = [re.search(r"konwoje BK (\d+) \((-?\d+)\)", par[d]) for d in win if d in par]
    conv = [(int(m.group(1)), int(m.group(2))) for m in conv if m]
    rep.add("T10: konwoje BK nowe (partie, ludzie)", f"{sum(x[0] for x in conv)}, {sum(x[1] for x in conv)}" if conv else "-", "INFO (R19, E4/8)", None)
    game = [re.search(r"rozwiazane przez gre bez bitwy (\d+), zdrowi (\d+), ranni (\d+)", par[d]) for d in win if d in par]
    game = [tuple(int(x) for x in m.groups()) for m in game if m]
    rep.add("T10: tabory i lodzie rozwiazane przez gre (partie, zdrowi, ranni)", f"{sum(x[0] for x in game)}, {sum(x[1] for x in game)}, {sum(x[2] for x in game)}" if game else "-", "INFO (R18, E4/3)", None)
    wig = [num(r"w tym wighty z rozwiazanych band do puli (\d+)", par[d], int, 0) or 0 for d in win if d in par]
    rep.add("T10: wighty z rozwiazanych band do puli (suma)", sum(wig) if wig else "-", "INFO (druga droga wightow, C 8)", None)

    # ---------------- E4/3 (108n): jednostka 1/k, punkty powrotu, tabela krain, powrot wyrzutkow do domu
    if any(x.startswith("PeopleUnit (E4/3):") for x in lines):
        etap4_e43(rep, lines, start, win, hea, par, lud, until, e43_state)

    # ---------------- E4/4 (109n): przyrost naturalny, miasta jako stan, podloga 10
    if any(x.startswith("PopulationLaw: przyrost naturalny wsi (E4/4)") for x in lines):
        etap4_e44(rep, lines, start, win, hea, lud, until)

    # ---------------- E4/5 (113n): spustoszenie, konto UCHODZCY, powrot do roku, plon, wsie spladrowane produkuja
    if any(x.startswith("Devastation (E4/5):") for x in lines):
        etap4_e45(rep, lines, start, win, hea, lud, until, logp)

    # ---------------- E4/6 (191): spichlerze okregow, glod, uchodzcy z glodu, H w przyroscie
    if any(x.startswith("Famine (E4/6):") for x in lines):
        etap4_e46(rep, lines, start, win, until)
    # ---------------- E4/7 (164b-1): pochodzenie, pobor z wsi, polegly trwale, wolne rece, okno zniw, menu osady
    if any(x.startswith("Pochodzenie (E4/7): wpiete") for x in lines):
        etap4_e47(rep, lines, start, win, hea, lud, until)
    # ---------------- E4/8 (164b-2): zalogi bez modelu, milicja z wolnych rak, nowe druzyny i karawany z ludzi, wedrowcy BK, miasta R2
    if any(x.startswith(E48_START) for x in lines):
        etap4_e48(rep, lines, start, win, hea, until)
    # ---------------- E4/10 (167): weterani - karczma tylko z puli zolnierzy, ktorzy odeszli ze sluzby zywi
    if any(x.startswith("Weterani (E4/10): wpiete") for x in lines):
        etap4_e410(rep, lines, start, win, lud, until)
    # ---------------- E4/11 (193): ksiega wiosek pobocznych, plon okregu z pracy wiosek, zywnosc warowni od ludzi, menu okregu
    if any(x.startswith("VillageBook (E4/11):") for x in lines):
        etap4_e411(rep, lines, start, win, until, logp)
    # ---------------- E4/9 (192): Straz z Westeros i dozywotnio, H1, niewola w kanonie, jency z rabunku z ludzi wsi, DLUG 4, Wolni Ludzie
    if any(x.startswith(E49_START) for x in lines):
        etap4_e49(rep, lines, start, win, until)

    # ---------------- baza konca etapu (364 doby) - INFO
    l300 = [num(r"partie rodow (\d+) \(\d+ partii", lud[d]) for d in lud if 300 <= run_day(d, start) <= 364]
    l300 = [x for x in l300 if x is not None]
    rep.add("baza konca etapu: wojsko lordow srednio d300-364", f"{st.mean(l300):.0f}" if l300 else "wymaga biegu 364 dob", "INFO (U20; prog etapu 95-115 tys.)", None)
    z364 = [num(r"\| zalogi (\d+) ludzi", zal[d]) for d in zal if run_day(d, start) == 364]
    rep.add("baza konca etapu: zalogi w d364", z364[0] if z364 and z364[0] is not None else "wymaga biegu 364 dob", "INFO (U20; prog etapu >= 85% biegu A)", None)
    rep.show()
    return 0


# E4/3: baza z biegu E4/2 120 dob bez E4/3 (scratchpad kopia-e42-120, Armoury-2026-10-11_02-17-11.log): doba 1 - bandy nowe 382 (6 231 ludzi),
# puste usuniete 375; doba 120 - band 826, ludzi 14 328, pula 3 053; "Ludzie:" d1 -> d4 -0.8%. Stosunek jednostki: 0.5 hearth za czlowieka wobec 1/191.
# d91-120 (linia Wyrzutki): narodziny band 224 ludzi/d (22.9 bandy), werbunek z puli 82.1/d, odmowione 0.47/d (8 z 30 dob > 0) - bandy biora z puli
# tyle, ile chca (popyt), nie tyle, ile pula ma (recenzja 1 E4/3).
E43_UNIT_RATIO = 95.0
E43_BASE = {"bands120": 826, "men120": 14328, "pool120": 3053, "new1": 382, "newmen1": 6231, "empty1": 375,
            "born90": 224.0, "recruit90": 82.1, "refused90": 0.47}
# recenzja 1 E4/3: prog band z tolerancja szumu miedzy biegami (wojna w innym miejscu = kilka procent band wiecej bez udzialu E4/3)
E43_BAND_TOL = 1.05
E43_UNIT_ON = "E4/3 jednostka ludzi (1 czlowiek = 1/k osady): CZYNNA"
E43_UNIT_OFF = "E4/3 jednostka ludzi (1 czlowiek = 1/k osady): wylaczona"


def e43_unit_state(lines, win):
    """E4/3 (recenzja 2): stan jednostki ludzi wedlug DZIENNEJ linii "Ludzie (ksiega)" (odcinek E4/3 czyta ustawienia z MCM), nie wedlug linii startowej
    "PeopleUnit (E4/3)" - ta powstaje przy ladowaniu modulow, zanim MCM wejdzie (OnGameStart), wiec pokazuje ustawienia startowe, nie stan gry.
    -> (stan "on" / "off" / "mixed" / "none", doby czynne, doby wylaczone, doby z R18 wylaczonym)."""
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    on = off = r18off = 0
    for d in win:
        x = kb.get(d)
        if not x:
            continue
        if E43_UNIT_ON in x:
            on += 1
        elif E43_UNIT_OFF in x:
            off += 1
        if "rozwiazane tabory do wsi (R18): NIE" in x:
            r18off += 1
    state = "none" if on + off == 0 else ("on" if off == 0 else ("off" if on == 0 else "mixed"))
    return state, on, off, r18off


def etap4_e43(rep, lines, start, win, hea, par, lud, until, ust):
    """E4/3 (108n): progi z projektu etapu 4 rozdz. 3 "E4/3" Test i zadania 11.10 (powrot wyrzutkow do domu: bandy po 120 dobach nie wiecej niz bez E4/3,
    pula nie rosnie bez konca). Baza - E43_BASE (bieg E4/2 120 dob). ust = e43_unit_state(): progi zalezne od jednostki licza sie tylko, gdy linia
    "Ludzie (ksiega)" pokazuje CZYNNA kazdej doby; progi puli i band - tylko, gdy powrot rozbitych jest wlaczony (linia puli: "tempo 1/N dob")."""
    state, on, off, r18off = ust
    unit = state == "on"
    unote = "" if unit else " [jednostka nie CZYNNA kazdej doby wedlug linii Ludzie (ksiega) - INFO]"

    def ua(name, val, exp, ok):
        rep.add(name + unote, val, exp, ok if unit else None)

    pul = next((x for x in lines if x.startswith("PeopleUnit (E4/3):")), "")
    rep.add("E4/3: linia startowa PeopleUnit - latki wpiete", pul[:260], "BRAK: - (wlacznik w tej linii = ustawienia przed MCM, nie stan gry)", "; BRAK: -." in pul)
    rep.add("E4/3: jednostka ludzi wedlug linii Ludzie (ksiega) (stan z MCM)", f"CZYNNA {on} dob, wylaczona {off} dob" if on + off else "brak odcinka E4/3 w linii Ludzie (ksiega)",
            "CZYNNA kazdej doby (wylaczona caly bieg = bieg porownawczy, progi jednostki INFO; zmiana w trakcie kampanii = NIE - skok hearth, Ryzyko CHANGELOG)",
            True if state == "on" else (None if state == "off" else False))
    ical = next((i for i, x in enumerate(lines) if x.startswith("PopulationLaw: kalibracja ludnosci")), None)
    iseed = next((i for i, x in enumerate(lines) if x.startswith("Wyrzutki: pula poczatkowa")), None)
    ua("E4/3: kalibracja PRZED pula wyrzutkow", f"kalibracja linia {ical}, pula linia {iseed}", "kalibracja wczesniej (nowa kampania)",
       (ical < iseed) if ical is not None and iseed is not None else None)
    cal = lines[ical] if ical is not None else ""
    cut = re.search(r"obcieto \(pulap (\d+) ludzi na punkt hearth wsi, E4/3\): (.*?); poza mapa razem ([\d.]+) mln", cal)
    rep.add("E4/3: pulap gestosci wsi (kalibracja)", (f"pulap {cut.group(1)}: {cut.group(2)[:160]}; poza mapa {cut.group(3)} mln") if cut else ("brak obciec" if cal else "brak linii kalibracji"),
            "summer obciete (4500 -> 650), inne bez zmian", (cut is not None and "summer" in cut.group(2)) if cal else None)
    lu1 = next((x for x in lines if x.startswith("Ludnosc: dzien")), None)
    if lu1:
        sm = fnum(r"summer ([\d.]+)M", lu1)
        ff = fnum(r"freefolk ([\d.]+)M", lu1)
        rep.add("E4/3: Wyspy Letnie w pierwszej linii Ludnosc", f"{sm} mln" if sm is not None else "brak", "ok. 0.11 mln (0.08-0.15)", (0.08 <= sm <= 0.15) if sm is not None else None)
        rep.add("E4/3: Wolni Ludzie w pierwszej linii Ludnosc", f"{ff} mln" if ff is not None else "brak", "0.30 mln (0.27-0.33)", (0.27 <= ff <= 0.33) if ff is not None else None)
    ld = sorted(d for d in lud if 1 <= run_day(d, start) <= 4)
    if len(ld) >= 2:
        a, b = fnum(r"ludnosc " + F_ + " mln", lud[ld[0]]), fnum(r"ludnosc " + F_ + " mln", lud[ld[-1]])
        if a and b:
            ch = 100.0 * (b - a) / a
            ua("E4/3: Ludzie d1 -> d4 (spadek na starcie)", f"{a:.3f} -> {b:.3f} mln ({ch:+.2f}%)", "bez spadku 0.6-1% (> -0.3%; bez E4/3: -0.8%)", ch > -0.3)
    measured = [d for d in win if d in hea and "RESZTA" in hea[d]]
    for name, pat in (("E4/3: rozwiazane tabory i lodzie do swojej wsi (R18, hearth/d)", r"rozwiazane tabory i lodzie do swojej wsi \(R18\) \+" + F_),
                      ("E4/3: gracz w Hearth - pobor, zadania, incydenty (hearth/d)", r"gracz \(pobor wymuszony, zadania, incydenty\) " + SF)):
        v = [fnum(pat, hea[d]) for d in measured]
        v = [x for x in v if x is not None]
        rep.add(name, f"{st.mean(v):.3f}" if v else "brak", "INFO (nazwane, poza RESZTA)", None)
    ctl = [num(r"kontrola zdrowi \+ ranni - wrocili = (-?\d+)", par[d]) for d in win if d in par]
    ctl = [x for x in ctl if x is not None]
    rep.add("E4/3: Partie R18 - zdrowi + ranni - wrocili (suma |roznic|)" + (f" [R18 wylaczone w {r18off} dobach - INFO]" if r18off else ""),
            sum(abs(x) for x in ctl) if ctl else "brak", "0", (sum(abs(x) for x in ctl) == 0) if ctl and not r18off else None)
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    pts = {"LosersFlee.SendHome": [], "ClanBudget.ToVillage": [], "OutlawLaw.ReturnHome": []}
    tot = []
    for d in win:
        x = kb.get(d)
        if not x:
            continue
        for k in pts:
            v = fnum(re.escape(k) + r" " + F_, x)
            if v is not None:
                pts[k].append(v)
        v = fnum(r"trzy punkty powrotu do DOM, razem " + F_, x)
        if v is not None:
            tot.append(v)
    rep.add("E4/3: trzy punkty powrotu do DOM (ludzie/d)", (", ".join(f"{k} {st.mean(v):.1f}" for k, v in pts.items() if v)
                                                          + (f"; razem bez podwojenia {st.mean(tot):.1f}" if tot else "")) or "brak odcinka E4/3 w Ludzie (ksiega)",
            "INFO - widoczne wedlug punktu (166/W4c/179 w ClanBudget.ToVillage; prosci i bandyci z LosersFlee tylko w LosersFlee.SendHome)", None)
    pw = e4_series(lines, "Pula wyrzutkow (E4/3): dzien", 1)
    pdays = sorted(d for d in pw if 1 <= run_day(d, start) <= until)
    pc = [fnum(r"kontrola: zmiana - \(naplyw - odplyw\) = " + SF, pw[d]) for d in pdays]
    pc = [x for x in pc if x is not None]
    rep.add("E4/3: Pula wyrzutkow - kontrola (najwieksza |roznica|)", f"{max(abs(x) for x in pc):.2f} ({len(pc)} dob)" if pc else "brak linii", "<= 1 czlowiek kazdej doby",
            (max(abs(x) for x in pc) <= 1.0) if pc else None)
    # powrot rozbitych wylaczony (Outlaw Routed Home Days 0 -> "tempo 1/- dob") - progi puli i band nie mierza E4/3
    toff = sum(1 for d in pdays if "tempo 1/- dob" in pw[d])
    tnote = f" [powrot rozbitych wylaczony (Outlaw Routed Home Days 0) w {toff} dobach - INFO]" if toff else ""
    if pdays:
        def stock(dd):
            return fnum(r"\| stan " + F_, pw.get(dd))
        def near(n):
            c = [d for d in pdays if run_day(d, start) <= n]
            return c[-1] if c else None
        s60, s90, s120 = stock(near(60)), stock(near(90)), stock(near(until))
        home = [fnum(r"= powrot do domu " + F_, pw[d]) for d in pdays[-28:]]
        home = [x for x in home if x is not None]
        dwell = [fnum(r"srednio w puli " + F_ + " dob", pw[d]) for d in pdays[-28:]]
        dwell = [x for x in dwell if x is not None]
        rep.add("E4/3: pula wyrzutkow d60 / d90 / d" + str(until) + tnote,
                f"{s60} / {s90} / {s120}; powrot do domu ostatnie 28 dob {st.mean(home):.1f}/d" if home else f"{s60} / {s90} / {s120}",
                f"d{until} <= baza bez E4/3 ({E43_BASE['pool120']}) i <= 1.25 x d90 (nie rosnie bez konca)",
                (s120 <= E43_BASE["pool120"] and (s90 is None or s90 <= 0 or s120 <= 1.25 * s90)) if s120 is not None and not toff else None)
        rep.add("E4/3: srednio w puli (stan / odplyw, ostatnie 28 dob)", f"{st.mean(dwell):.0f} dob" if dwell else "-", "INFO (rozbici wracaja w kilka tygodni)", None)
        # recenzja 2: zolnierz schodzi z puli tylko wtedy, gdy przyjmie go BK - czy ograniczeniem jest tempo, czy przyjmowanie przez BK
        sw = [re.search(r"zolnierze do ludnosci BK (\d+) z (\d+) chetnych", pw[d]) for d in pdays[-28:]]
        sw = [(int(m.group(1)), int(m.group(2))) for m in sw if m]
        if sw:
            acc, want = sum(x[0] for x in sw), sum(x[1] for x in sw)
            rep.add("E4/3: zolnierze z puli do ludnosci BK - przyjeci z chetnych (ostatnie 28 dob)",
                    f"{acc / len(sw):.1f} z {want / len(sw):.1f}/d ({100.0 * acc / want:.0f}%)" if want else f"{acc / len(sw):.1f} z 0/d",
                    "INFO (ponizej ok. 80% - pula trzyma zolnierzy, ktorych BK nie przyjmuje; nie tempo)", None)
    wyr = e4_series(lines, "Wyrzutki: dzien", 1)
    wd = sorted(d for d in wyr if 1 <= run_day(d, start) <= until)
    if wd:
        last = wyr[wd[-1]]
        b, m = num(r"\| band (\d+), ludzi (\d+)", last), num(r"\| band \d+, ludzi (\d+)", last)
        lb, lm = round(E43_BASE["bands120"] * E43_BAND_TOL), round(E43_BASE["men120"] * E43_BAND_TOL)
        rep.add(f"E4/3: bandy w d{run_day(wd[-1], start)} (liczba, ludzie)" + tnote, f"{b}, {m}",
                f"nie wiecej niz bez E4/3 z tolerancja {E43_BAND_TOL:.2f} (szum miedzy biegami): baza {E43_BASE['bands120']}, {E43_BASE['men120']} -> <= {lb}, {lm}",
                (b <= lb and m <= lm) if b is not None and m is not None and run_day(wd[-1], start) >= until and not toff else None)
        # recenzja 1: czy powrot w ogole ogranicza bandy - narodziny i werbunek z puli oraz odmowy w ostatnich 30 dobach okna wobec bazy
        w30 = [d for d in wd if run_day(d, start) > until - 30]
        born, rec, ref = [], [], []
        for d in w30:
            nb = re.search(r"bandy nowe \d+ \((\d+) ludzi\), odmowione (\d+)", wyr[d])
            rc = num(r"werbunek: z puli (\d+)", wyr[d])
            if nb:
                born.append(int(nb.group(1)))
                ref.append(int(nb.group(2)))
            if rc is not None:
                rec.append(rc)
        if born and rec:
            a0, a1 = run_day(w30[0], start), run_day(w30[-1], start)
            rep.add(f"E4/3: bandy biora z puli d{a0}-{a1} (ludzi/d): narodziny, werbunek; odmowione/d",
                    f"{st.mean(born):.0f}, {st.mean(rec):.0f}; {st.mean(ref):.2f}",
                    f"INFO wobec bez E4/3 d91-120: {E43_BASE['born90']:.0f}, {E43_BASE['recruit90']:.0f}; {E43_BASE['refused90']:.2f} "
                    "(odmowione ok. 0 i ten sam pobor = bandy ograniczone popytem, nie pula - powrot nie zmniejszy band)", None)
        first = wyr[wd[0]]
        nb = re.search(r"bandy nowe (\d+) \((\d+) ludzi\), odmowione (\d+), puste usuniete (\d+)", first)
        rep.add("E4/3: bandy d1 - nowe (ludzi), odmowione, puste usuniete", (f"{nb.group(1)} ({nb.group(2)}), {nb.group(3)}, {nb.group(4)}") if nb else "brak",
                f"INFO wobec bez E4/3: {E43_BASE['new1']} ({E43_BASE['newmen1']}), puste {E43_BASE['empty1']} (geografia band - 108 ryzyko 1)", None)


def etap4_e47(rep, lines, start, win, hea, lud, until):
    """E4/7 (164b-1): progi T1-T15 z planu kodu docs/etap4/PLAN-KODU-E47.md rozdz. 9 (stan z linii dziennych, nie startowej - poprawka R2-1 E4/3).
    Linie: "Pochodzenie (E4/7): dzien" (kontrola, wejscia/wyjscia, z niczego / w nicosc, DeduceManpower, czas), "Wolne rece (E4/7): dzien" (a, prog, L, hamulec),
    "Ludzie (ksiega)" odcinek E4/7 (rownanie krain), "Pobor:" dopisek E4/7 (chec, odmowione przez ksiege, pula BK = 0), "Ludzie:" (wojsko lordow, ludnosc netto)."""
    stl = next((x for x in lines if x.startswith("Pochodzenie (E4/7): wpiete")), "")
    rep.add("E4/7 T1: linia startowa (latki ksiegi pochodzenia)", stl[:260], "\"BRAK: -\"", ("BRAK: -" in stl) if stl else False)
    poc = e4_series(lines, "Pochodzenie (E4/7): dzien")
    wr = e4_series(lines, "Wolne rece (E4/7): dzien")
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    pdays = [d for d in sorted(poc) if 1 <= run_day(d, start) <= until]
    if not pdays:
        rep.add("E4/7: linie dzienne \"Pochodzenie (E4/7)\"", "brak (przed siewem albo People Origin Enabled wylaczone)", "kazdej doby po siewie", False)
        return
    stum = [num(r"\| potkniecia (\d+) \(razem", poc[d], int, 0) or 0 for d in pdays]
    fst = [num(r"\| potkniecia (\d+)\.", wr[d], int, 0) or 0 for d in sorted(wr) if 1 <= run_day(d, start) <= until]
    rep.add("E4/7 T1: potkniecia ksiegi pochodzenia i wolnych rak (suma dob)", f"Pochodzenie {sum(stum)}, Wolne rece (najwiecej) {max(fst) if fst else 0}",
            "<= 5 (z nazwa miejsca w ERROR)", sum(stum) + (max(fst) if fst else 0) <= 5)
    # T2 - kontrola kodu stanu kont
    ctl = [fnum(r"kontrola: zmiana stanu - \(wejscia - wyjscia \+ z - q\) = " + SF, poc[d]) for d in pdays]
    ctl = [x for x in ctl if x is not None]
    rep.add("E4/7 T2: Pochodzenie - kontrola (najwieksza |roznica|)", f"{max(abs(x) for x in ctl):.2f} ({len(ctl)} dob)" if ctl else "brak (pierwsza doba sesji)",
            "<= 1 czlowiek kazdej doby", (max(abs(x) for x in ctl) <= 1.0) if ctl else None)
    # T3 - rownanie krain (odcinek E4/7 w Ludzie (ksiega)); przy RESZCIE hearth != 0 (D3) - INFO
    rq = [re.search(r"rownanie krain: najgorsza kraina (\S+) reszta " + SF + r" \(" + SF + r"% ludzi krainy\)", kb[d]) for d in pdays if d in kb]
    rq = [(m.group(1), float(m.group(2)), float(m.group(3))) for m in rq if m]
    hrest = [fnum(r"\| RESZTA " + SF, hea[d]) for d in pdays if d in hea]
    hrest = [x for x in hrest if x is not None]
    d3 = (st.mean(abs(x) for x in hrest) <= 1.0) if hrest else False
    if rq:
        worst = max(rq, key=lambda x: abs(x[2]))
        rep.add("E4/7 T3: rownanie krain - najgorsza kraina (|reszta| % ludzi krainy)", f"{worst[0]} {worst[2]:+.3f}% (reszta {worst[1]:+.0f}); RESZTA hearth srednio {st.mean(abs(x) for x in hrest):.1f}/d" if hrest else f"{worst[0]} {worst[2]:+.3f}%",
                "<= 0.1% kazdej doby; przy RESZCIE hearth != 0 (D3) - INFO", (abs(worst[2]) <= 0.1) if d3 else None)
    else:
        rep.add("E4/7 T3: rownanie krain", "brak odcinka E4/7 w Ludzie (ksiega)", "<= 0.1%", None)
    # T4/T5 - w nicosc i z niczego wobec ruchu brutto (srednia ostatnich 28 dob okna)
    last = pdays[-28:]
    W = [fnum(r"wejscia " + F_ + r", wyjscia", poc[d]) or 0 for d in last]
    X = [fnum(r", wyjscia " + F_ + r"\)", poc[d]) or 0 for d in last]
    Q = [fnum(r"w nicosc -" + F_, poc[d]) or 0 for d in last]
    Z = [fnum(r"z niczego \+" + F_, poc[d]) or 0 for d in last]
    gross = st.mean(W) + st.mean(X) if W else 0
    rep.add("E4/7 T4: w nicosc bez nazwy (srednia 28 dob)", f"{st.mean(Q):.0f}/d wobec ruchu brutto {gross:.0f}/d ({100.0 * st.mean(Q) / gross:.2f}%)" if gross else "-",
            "<= 1% ruchu brutto SLUZBA + JENCY (P rozdz. 4; z30A +1072/d bez nazwy)", (st.mean(Q) <= 0.01 * gross) if gross else None)
    zk = re.search(r"z niczego \+[\d.]+ \(([^)]*)\)", poc[last[-1]])
    rep.add("E4/7 T5: z niczego (srednia 28 dob, wedlug rodzaju ostatniej doby)", f"{st.mean(Z):.0f}/d ({100.0 * st.mean(Z) / gross:.1f}% ruchu) [{zk.group(1)[:200] if zk else '-'}]" if gross else "-",
            "INFO - nazwane wedlug rodzaju (szablony, model zalog, milicja, rabunek BK, gracz - zamyka E4/8/E4/9)", None)
    # T6 - punkt zdjecia: DeduceManpower = werbunek od notabli + karczma + gracz ("Ludzie:"), poza oknem 0, pobor na podlodze 0
    dm = [fnum(r"DeduceManpower: wywolan \d+ \(ludzi " + F_, poc[d]) or 0 for d in pdays]
    out = [num(r"poza oknem (\d+)", poc[d], int, 0) or 0 for d in pdays]
    flo = [fnum(r"pobor na podlodze \d+ \(ludzi " + F_, poc[d]) or 0 for d in pdays]
    a5 = [fnum(r"\(wsie [\d.]+, miasta [\d.]+, zamki [\d.]+, karczmy AI [\d.]+, A5 " + F_, poc[d]) or 0 for d in pdays]
    rec = []
    for d in pdays:
        x = lud.get(d)
        if not x:
            continue
        m = re.search(r"od notabli do partii rodow (\d+), od notabli bez wodza - garnizony, karawany (\d+), z karczmy (\d+), bez osady - jency albo ochotnicy z mapy \d+, gracz (\d+)", x)
        if m:
            rec.append(sum(int(g) for g in m.groups()))
    sdm, srec = sum(dm), sum(rec)
    rep.add("E4/7 T6: DeduceManpower (ludzie) wobec werbunku od notabli + karczma + gracz (Ludzie:)", f"{sdm:.0f} wobec {srec} ({100.0 * (sdm - srec) / srec:+.2f}%); A5 {st.mean(a5):.0f}/d" if srec else f"{sdm:.0f} wobec -",
            "|roznica| <= 2% (A5 osobno - bez zdarzenia gry)", (abs(sdm - srec) <= 0.02 * srec) if srec else None)
    rep.add("E4/7 T6: DeduceManpower poza oknem (suma)", sum(out), "0", sum(out) == 0)
    rep.add("E4/7 T6: pobor na podlodze (suma ludzi)", f"{sum(flo):.0f}", "0 (wies ponizej 50 hearth / bez przelicznika - rekrut bez pochodzenia)", sum(flo) == 0)
    # T7 - pierwsza doba po siewie: regiony ponad progiem
    wdays = [d for d in sorted(wr) if 1 <= run_day(d, start) <= until]
    if wdays:
        w1 = wr[wdays[0]]
        ov = num(r"regionow ponad progiem (\d+) z (\d+)", w1)
        top = re.search(r"\| top 8:(.*?)\| siew", w1)
        rep.add("E4/7 T7: pierwsza doba wolnych rak - regiony ponad progiem", f"{ov} (top: {top.group(1)[:220].strip() if top else '-'})", "<= 5 (Straz poza lista; L = 1.00 przy a <= f z definicji)",
                (ov <= 5) if ov is not None else None)
        lmin = [fnum(r"L: min " + F_, wr[d]) for d in wdays]
        lmin = [x for x in lmin if x is not None]
        lo = [num(r"regionow z L < 1 (\d+)", wr[d], int, 0) or 0 for d in wdays]
        brk = [num(r"hamulec - bez nowych ochotnikow: (\d+)", wr[d], int, 0) or 0 for d in wdays]
        rep.add("E4/7: wolne rece - L min (cale okno) / regiony z L < 1 / z hamulcem (ostatnia doba)", f"{min(lmin) if lmin else '-'} / {lo[-1]} / {brk[-1]}", "INFO (L >= 0.5 z definicji)", None)
    else:
        rep.add("E4/7 T7: wolne rece", "brak linii \"Wolne rece (E4/7)\"", "<= 5 regionow ponad progiem w d1", None)
    # T8 - Pobor: chec x0.25-0.45 (d28-d120); hamulec ksiegi i BK osobno
    pob = [s for s in lines if s.startswith("Pobor: dzien")]
    pk = [(num(r"Pobor: dzien (\d+)", s), s) for s in pob]
    pk = [(d, s) for d, s in pk if d is not None and 28 <= run_day(d, start) <= until]
    ch = [fnum(r"chec do sluzby srednio x" + F_, s) for d, s in pk]
    ch = [x for x in ch if x is not None]
    rep.add(f"E4/7 T8: Pobor - chec srednio d28-{until}", f"x{st.mean(ch):.2f}" if ch else "brak linii", "x0.25-0.45 (z30A x0.30-0.32; P x0.3-0.4)", (0.25 <= st.mean(ch) <= 0.45) if ch else None)
    rf = [(num(r"\((\d+) losowan miejsc", s, int, 0) or 0, num(r"odmowione przez ksiege \(a >= f\) (\d+) losowan", s, int, 0) or 0, num(r"pula BK = 0 przy wolnych rekach (\d+) osad", s, int, 0) or 0) for d, s in pk]
    if rf:
        n_all = sum(x[0] for x in rf) + sum(x[1] for x in rf)
        rep.add("E4/7 T8: hamulec ksiegi / pula BK = 0 przy wolnych rekach", f"ksiega {sum(x[1] for x in rf)} z {n_all} losowan ({100.0 * sum(x[1] for x in rf) / n_all:.1f}%), BK = 0: {st.mean(x[2] for x in rf):.0f} osad/d" if n_all else "-",
                "INFO; hamulec BK > 30% losowan -> zapis do STAN-PRAC (odlozone dopasowanie BEE, P rozdz. 7)", None)
    # T9 - wojsko lordow d90-d120
    l90 = [num(r"partie rodow (\d+) \(\d+ partii", lud[d]) for d in lud if 90 <= run_day(d, start) <= min(120, until)]
    l90 = [x for x in l90 if x is not None]
    rep.add("E4/7 T9: wojsko lordow srednio d90-d120", f"{st.mean(l90):.0f}" if l90 else "brak dob 90-120", "95-115 tys. (podloga 85 tys.; z30A 107 tys.)", (95000 <= st.mean(l90) <= 115000) if l90 else None)
    # T10 - Ludnosc BK (169c) d1 -> ostatnia, na rok
    bk = e4_series(lines, "Ludnosc BK (169c): dzien")
    bd = [d for d in sorted(bk) if 1 <= run_day(d, start) <= until]
    if len(bd) >= 2:
        a, b = num(r"\| ludnosc BK (\d+)", bk[bd[0]]), num(r"\| ludnosc BK (\d+)", bk[bd[-1]])
        days = max(1, run_day(bd[-1], start) - run_day(bd[0], start))
        if a and b:
            yr = (b / a - 1.0) * 364.0 / days * 100.0
            rep.add("E4/7 T10: Ludnosc BK (169c) na rok", f"{a} -> {b} ({yr:+.2f}%/rok w {days} dobach)", "<= +2%/rok (INFO trendu - A5 z BK)", yr <= 2.0)
    else:
        rep.add("E4/7 T10: Ludnosc BK (169c)", "brak linii", "<= +2%/rok", None)
    # T11 - netto Ludzie: d28-d120 (razem z uchodzcami, gdy jest)
    net = []
    for d in win:
        if d not in lud or run_day(d, start) < 28:
            continue
        x = lud[d]
        v = num(r"razem z nimi [\d.]+ mln \(([+-]?\d+) ludzi\)", x)
        if v is None:
            v = num(r"ludnosc [\d.]+ mln \(([+-]?\d+) ludzi\)", x)
        if v is not None:
            net.append(v)
    rep.add(f"E4/7 T11: ludnosc netto d28-{until} (ludzi/d)", f"{st.mean(net):+.0f}/d" if net else "brak", "od -720/d (-0.5%/rok, Jeff 11.10) do +1150/d", (-721 <= st.mean(net) <= 1154) if net else None)
    # T12 - koszt
    ms = [fnum(r"\| czas " + F_ + r" ms \(uzgodnienie", poc[d]) for d in pdays]
    ms = [x for x in ms if x is not None]
    rep.add("E4/7 T12: koszt linii E4/7 i uzgodnienia (srednio, najwiecej)", f"{st.mean(ms):.1f} / {max(ms):.1f} ms" if ms else "-", "<= 15 ms/dobe (srednio)", (st.mean(ms) <= 15.0) if ms else None)
    # T13 - zapis i wczytanie
    ld = [x for x in lines if x.startswith("Pochodzenie (E4/7): wczytanie")]
    sv = [x for x in lines if x.startswith("Pochodzenie (E4/7): zapis arm_people")]
    if ld:
        pc = fnum(r"nie znaleziono \d+ \(" + F_ + r"%", ld[-1])
        rep.add("E4/7 T13: wczytanie - nie znaleziono partii", ld[-1][:220], "0-2%", (pc <= 2.0) if pc is not None else None)
    else:
        rep.add("E4/7 T13: wczytanie", "brak linii wczytania (bieg bez zapisu)", "0-2% nie znalezionych", None)
    # recenzja E4/7 R2-3: przyciecie kluczy slownikow - rozmiar nie rosnie z dlugoscia kampanii (plan ok. 150 KB; najwyzej 16 regionow na slownik)
    szs = [num(r"sekcje E4/7 (\d+) znakow", x, int) for x in sv]
    szs = [x for x in szs if x is not None]
    rep.add("E4/7 T13: rozmiar zapisu arm_people - sekcje E4/7 (pierwszy -> ostatni zapis)", f"{szs[0]} -> {szs[-1]} znakow ({len(szs)} zapisow)" if szs else "brak zapisu w biegu",
            "<= 800 000 znakow (przyciecie kluczy R2-3; SaveText w kawalkach po 8000)", (szs[-1] <= 800000) if szs else None)
    # ---------------- poprawki po recenzji E4/7 (R1-1, R1-3, R1-5, R2-1, R2-3)
    tl = [fnum(r"\(w tym ze stanu miast " + F_ + r"\)", kb[d]) or 0 for d in pdays if d in kb]
    tb = [fnum(r"\(w tym do stanu miast " + F_ + r"\)", kb[d]) or 0 for d in pdays if d in kb]
    al = [fnum(r"\| pobor z DOM dzis " + F_ + r" ludzi", kb[d]) or 0 for d in pdays if d in kb]
    ab = [fnum(r", powroty z pochodzeniem \+" + F_ + r" ludzi", kb[d]) or 0 for d in pdays if d in kb]
    if tl:
        rt = sum(tb) / sum(tl) if sum(tl) else 0.0
        ra = sum(ab) / sum(al) if sum(al) else 0.0
        rep.add("E4/7 R1-1: stan miast - pobor ze stanu miast / powroty do stanu miast (suma okna)", f"{sum(tl):.0f} / {sum(tb):.0f} (powrot {100 * rt:.0f}% wobec calego poboru: {100 * ra:.0f}%)",
                "powroty > 0 przy poborze > 0 (pobor z miasta nie jednokierunkowy); udzial INFO", (sum(tb) > 0) if sum(tl) > 0 else None)
    fk = [num(r"przyciecie kluczy \(ponizej [\d.]+ czlowieka albo spoza \d+ najwiekszych\): (\d+) kluczy", poc[d], int, 0) or 0 for d in pdays]
    fm = [fnum(r"najwiekszych\): \d+ kluczy, " + F_ + r" ludzi", poc[d]) or 0 for d in pdays]
    rep.add("E4/7 R2-3: przyciecie kluczy slownikow (srednio na dobe)", f"{st.mean(fk):.0f} kluczy, {st.mean(fm):.1f} ludzi do glownego regionu slownika" if fk else "-",
            "INFO (suma bez zmian; rownanie krain odejmuje Drift - T3)", None)
    nb = [fnum(r"najemnik z karczmy przy hamulcu \(obcy\) " + F_, poc[d]) or 0 for d in pdays]
    rep.add("E4/7 R1-3: najemnicy z karczmy w krainach z hamulcem (obcy, bez zdjecia z DOM)", f"{sum(nb):.0f} (srednio {st.mean(nb):.1f}/d)" if nb else "-", "INFO", None)
    pw = [l for d in pdays for l in [poc[d]] if "pobor wymuszony gracza: zdjeci z DOM" in l]
    if pw:
        pin = sum(fnum(r"pobor wymuszony gracza: zdjeci z DOM " + F_, l) or 0 for l in pw)
        ptk = sum(fnum(r"wzieci z ekranu lupu " + F_, l) or 0 for l in pw)
        # E4/7b (decyzja Jeffa 11.10 12:40): niewzieci wracaja do DOM swojej wsi; "bez powrotu (poza rosterem)" - tylko przy wylaczonym Press Untaken Go Home
        # (stary log sprzed E4/7b: "niewzieci (poza rosterem) N" - liczony jako bez powrotu)
        phm = sum(fnum(r"niewzieci - wrocili do domu " + F_, l) or 0 for l in pw)
        plo = sum((fnum(r"niewzieci bez powrotu \(poza rosterem\) " + F_, l) or 0) + (fnum(r"niewzieci \(poza rosterem\) " + F_, l) or 0) for l in pw)
        dif = pin - (ptk + phm + plo)
        rep.add("E4/7 R1-5: pobor wymuszony gracza - zdjeci z DOM / wzieci z ekranu lupu / niewzieci wrocili do domu / bez powrotu", f"{pin:.0f} / {ptk:.0f} / {phm:.0f} / {plo:.0f}",
                "INFO (proba reczna gracza); bez powrotu 0 przy Press Untaken Go Home", None)
        rep.add("E4/7b: pobor wymuszony gracza - zdjeci z DOM = wzieci + wrocili do domu + bez powrotu (suma okna)", f"roznica {dif:+.1f} ({len(pw)} dob)",
                "|roznica| <= 0.5 + 0.2/dobe (zaokraglenie linii)", abs(dif) <= 0.5 + 0.2 * len(pw))
    sn = [num(r"ekran druzyny bez stanu przed \(_initialData\) (\d+)", poc[d], int, 0) or 0 for d in pdays]
    rep.add("E4/7 R2-1: ekran druzyny bez stanu przed (_initialData)", sum(sn), "0", sum(sn) == 0)
    rep.add("E4/7 T14: menu \"People of this land\"", "proba reczna Jeffa (wies, miasto, zamek)", "INFO - liczby = linia dzienna regionu", None)
    rep.add("E4/7 T15: progi etapow 1-3", "--grupa etap2 / etap3 na tym samym logu", "INFO", None)


# E4/8 (164b-2): baza zalog d120 - bieg A zbioru 31 (WGRANIE 31, SCRATCH kopia-z31-A: zalogi 81.2 tys.); prog N4 >= 85% (bez modelu [S] ok. 67.6 tys. - prog graniczny)
E48_GARRISON_BASE = 81200
E48_START = "Wojsko z ludzi (E4/8): wpiete"


def etap4_e48(rep, lines, start, win, hea, until):
    """E4/8 (164b-2): progi N1-N17 z planu kodu docs/etap4/PLAN-KODU-E48.md rozdz. 13 (stan z linii dziennych, nie startowej - poprawka R2-1 E4/3).
    Linie: "Wojsko z ludzi (E4/8): dzien" (zalogi, A5, milicja, nowe druzyny, karawany, wedrowcy BK, R6, KONTROLA, czas), "Miasta (E4/8 R2): dzien" (naturalne,
    migracja, kontrola, udzial miejski), "Zalogi: przyrost bez werbunku (169c)" (przyrost z modelu, milicja z modelu w ticku), "Hearth (E4/0)" (RESZTA)."""
    stl = next((x for x in lines if x.startswith(E48_START)), "")
    rep.add("E4/8 N1: linia startowa (latki wojska z ludzi)", stl[:260] if stl else "brak", "\"BRAK: -\"", ("BRAK: -" in stl) if stl else False)
    arm = e4_series(lines, "Wojsko z ludzi (E4/8): dzien")
    mia = e4_series(lines, "Miasta (E4/8 R2): dzien")
    zal = e4_series(lines, "Zalogi: przyrost bez werbunku (169c): dzien")
    adays = [d for d in sorted(arm) if 1 <= run_day(d, start) <= until]
    if not adays:
        rep.add("E4/8: linie dzienne \"Wojsko z ludzi (E4/8)\"", "brak", "kazdej doby (LogEnabled)", False)
        return
    stum = [num(r"\| potkniecia (\d+)", arm[d], int, 0) or 0 for d in adays]
    rep.add("E4/8 N1: potkniecia wojska z ludzi (suma dob)", sum(stum), "<= 5 (z nazwa miejsca w linii i w ERROR)", sum(stum) <= 5)
    live = [d for d in adays if "ksiega pochodzenia: CZYNNA" in arm[d]]
    rep.add("E4/8: doby z czynna ksiega pochodzenia (LevyLive)", f"{len(live)} z {len(adays)}", "INFO (przed siewem E4/7 latki ludzi - no-op)", None)
    # N2 - przyrost z modelu zalog = Straz zostawiona do E4/9, kazdej doby
    bad, n2 = [], 0
    for d in adays:
        if d not in zal:
            continue
        mz = num(r"przyrost z modelu zalogi gry \(bez werbunku i bez zlota\) ([+-]?\d+)", zal[d])
        sw = num(r"(?:Straz zostawiona do E4/9|Straz: przybysze z Westeros \(E4/9\)) \+(\d+)", arm[d], int, 0) or 0     # E4/9 (11.2): obie etykiety
        if mz is None:
            continue
        n2 += 1
        if abs(mz - sw) > 0:
            bad.append((run_day(d, start), mz, sw))
    zb = [num(r"przyrost z modelu zablokowany -(\d+)", arm[d], int, 0) or 0 for d in adays]
    rep.add("E4/8 N2: Zalogi (169c) przyrost z modelu - Straz (do E4/9 / przybysze z Westeros E4/9)", f"{n2 - len(bad)} z {n2} dob = 0" + (f"; pierwsze roznice {bad[:3]}" if bad else "") + f"; zablokowane srednio {st.mean(zb):.0f}/d (gorna granica)",
            "= 0 kazdej doby (z30A +105/d)", (len(bad) == 0) if n2 else None)
    # N3 - z niczego wedlug rodzaju (Pochodzenie E4/7) - ostatnie 28 dob
    poc = e4_series(lines, "Pochodzenie (E4/7): dzien")
    last = [d for d in adays if d in poc][-28:]
    kinds = {"zalogi": [], "milicje": [], "karawany": [], "partie rodow": [], "wedrowcy i konwoje BK": []}
    for d in last:
        m = re.search(r"z niczego \+[\d.]+ \(([^)]*)\)", poc[d])
        body = m.group(1) if m else ""
        for k in kinds:
            v = fnum(re.escape(k) + r" " + F_, body)
            kinds[k].append(v or 0.0)
    if last:
        rep.add("E4/8 N3: z niczego wedlug rodzaju (Pochodzenie E4/7, srednio 28 dob)", ", ".join(f"{k} {st.mean(v):.1f}/d" for k, v in kinds.items()),
                "INFO - oczekiwane ~0 (zalogi = Straz, milicje <= 1% ruchu; z30A partie rodow 505/d, zalogi 105/d)", None)
    # N4 - zalogi d120
    zd = [d for d in sorted(zal) if 1 <= run_day(d, start) <= until]
    if zd:
        z = num(r"\| zalogi (\d+) ludzi", zal[zd[-1]])
        thr = int(0.85 * E48_GARRISON_BASE)
        rep.add(f"E4/8 N4: zalogi d{run_day(zd[-1], start)}", f"{z} wobec bazy {E48_GARRISON_BASE} (z31 A d120)", f">= {thr} (85% bazy; wiaze tylko przy d120)",
                (z >= thr) if (z is not None and run_day(zd[-1], start) >= 120) else None)
    # N4 (recenzja E4/8 R1-2): drugi odplyw z zalog - nowe druzyny z zalogi siedziby (a2) - do oceny progu N4 i decyzji K8
    a2 = [num(r"\+ z zalogi siedziby (\d+)", arm[d], int, 0) or 0 for d in adays]
    rep.add("E4/8 N4: odplyw z zalog do nowych druzyn (a2, suma okna / srednio)", f"{sum(a2)} / {st.mean(a2):.1f} na dobe",
            "INFO - szacunek d120 bez modelu (ok. 67.6 tys.) go nie obejmuje; razem z wierszem A5 dowod do K8", None)
    # K8 (Jeff 11.10 16:55): A5 do GarrisonAutoRecruitPerDay na dobe - dobrani ponad limit gry i odmowy (segment "K8:" w linii A5) - przy progu N4
    k8 = [re.search(r"K8: limit (\d+)/d, dobrano (\d+) \(ponad (\d+)/d, w (\d+) twierdzach\), odmowy: zold albo miejsce (\d+) / brak ochotnikow (\d+) / hamulec (\d+), "
                    r"poza K8 \(Straz, Inni, bez poboru\) (\d+)", arm[d]) for d in adays]
    k8 = [tuple(int(x) for x in m.groups()) for m in k8 if m]
    if k8:
        rep.add("E4/8 N4/K8: A5 ponad limit gry - limit / dobrano (suma okna, srednio) / twierdz z dobranym / odmowy: zold albo miejsce, brak ochotnikow, hamulec / poza K8 (srednio)",
                f"{k8[-1][0]}/d (gra {k8[-1][2]}/d) / {sum(x[1] for x in k8)}, {st.mean(x[1] for x in k8):.1f}/d / "
                + " / ".join(f"{st.mean(x[i] for x in k8):.0f}" for i in (3, 4, 5, 6, 7)),
                "INFO - K8 dopelnia zalogi do progu N4 (szacunek d120 bez K8 ok. 83% bazy); przy czynnej ksiedze dobrano > 0", None)
    else:
        rep.add("E4/8 N4/K8: A5 ponad limit gry", "brak segmentu \"K8:\" w linii", "INFO (log sprzed K8)", None)
    # R6 (recenzja E4/8 R1-1/R2-5): rozwiazane bez bitwy do DOM, w tym usuniete bez zniszczenia (RemoveParty - konwoje DTE)
    r6 = [re.search(r"ROZWIAZANE BEZ BITWY \(R6\): partii (\d+) \[.*?w tym usuniete bez zniszczenia - np\. konwoje DTE (\d+)\], do domu " + F_, arm[d]) for d in adays]
    r6 = [(int(m.group(1)), int(m.group(2)), float(m.group(3))) for m in r6 if m]
    if r6:
        rep.add("E4/8 R6: rozwiazane bez bitwy do domu - partii / w tym usuniete bez zniszczenia / ludzi (suma okna)",
                f"{sum(x[0] for x in r6)} / {sum(x[1] for x in r6)} / {sum(x[2] for x in r6):.0f}", "INFO (z31A: ok. 147 partii usunietych bez zniszczenia w 120 dob)", None)
        # scalenie koncowe etapu 4 (R6/W5): czesc R6 do karczmy - ta sama regula i ten sam strumien co rozwiazane partie (linia Weterani: "rozwiazane partie")
        rv = [fnum(r"ROZWIAZANE BEZ BITWY \(R6\):[^|]*?, do karczmy \(E4/10 - jak rozwiazane partie\) " + F_, arm[d]) for d in adays]
        rv = [x for x in rv if x is not None]
        rep.add("E4/8 R6/W5: rozwiazane bez bitwy do karczmy (E4/10, suma okna)", f"{sum(rv):.0f} ({len(rv)} dob z segmentem)" if rv else "brak segmentu (log sprzed scalenia koncowego)",
                "INFO - karawany (konwoje DTE), milicje, Straz: 0; w strumieniu Weterani \"rozwiazane partie\"", None)
    # N5 - wojsko lordow (jak E4/7 T9)
    rep.add("E4/8 N5: wojsko lordow d90-d120", "patrz E4/7 T9 (ta sama linia \"Ludzie:\")", "95-115 tys. (podloga 85)", None)
    # N6 - kontrole
    kc = [fnum(r"\| KONTROLA: zdjete z DOM przyczynami E4/8 [\d.]+ - \(m1 \+ a1 \+ c1 \+ c3 \+ n1w \+ n1e\) = " + SF, arm[d]) for d in adays]
    kc = [x for x in kc if x is not None]
    rep.add("E4/8 N6: KONTROLA linii E4/8 (najwieksza |roznica|)", f"{max(abs(x) for x in kc):.2f} ({len(kc)} dob)" if kc else "-", "<= 1 czlowiek kazdej doby", (max(abs(x) for x in kc) <= 1.0) if kc else None)
    mdays = [d for d in sorted(mia) if 1 <= run_day(d, start) <= until]
    mc = [fnum(r"\| kontrola: X - Y - \(wszystkie ruchy \+ zalozenie\) = " + SF, mia[d]) for d in mdays]
    mc = [x for x in mc if x is not None]
    rep.add("E4/8 N6: kontrola linii Miasta (najwieksza |roznica|)", f"{max(abs(x) for x in mc):.2f} ({len(mc)} dob)" if mc else "brak (pierwsza doba sesji / brak linii)",
            "<= 1 czlowiek kazdej doby", (max(abs(x) for x in mc) <= 1.0) if mc else None)
    # N7 - milicja: tick netto po korekcie E4/8 = "milicja (E4/0): z modelu w ticku" (169c; okna W1/W2 widza stan po korekcie)
    dif = []
    for d in adays:
        if d not in zal:
            continue
        ours = num(r"tick netto \(po korekcie\) ([+-]?\d+)", arm[d])
        theirs = num(r"milicja \(E4/0\): z modelu w ticku ([+-]?\d+)", zal[d])
        if ours is not None and theirs is not None and ("milicja T" in arm[d]):
            dif.append(ours - theirs)
    rep.add("E4/8 N7: milicja - tick E4/8 wobec 169c (srednia roznica, najwieksza)", f"{st.mean(dif):+.2f}/d, max |{max(abs(x) for x in dif)}|" if dif else "brak (milicja wylaczona albo pomiar E4/0 wylaczony)",
            "|srednia| <= 2/d (ulamki _readyMilitia - szum dzienny INFO)", (abs(st.mean(dif)) <= 2.0) if dif else None)
    mil = [num(r"\| milicja (\d+)", zal[d]) for d in zd] if zd else []
    mil = [x for x in mil if x is not None]
    if mil:
        rep.add("E4/8 N7: milicja swiata d1 -> ostatnia", f"{mil[0]} -> {mil[-1]}", "INFO (z30A 63 002 -> 67 283)", None)
    ref = [num(r"odmowione -(\d+)", arm[d], int, 0) or 0 for d in adays]
    up = [num(r"MILICJA: przyrost w oknach \+(\d+)", arm[d], int, 0) or 0 for d in adays]
    home = [num(r"ubytek bez walki do domu -(\d+)", arm[d], int, 0) or 0 for d in adays]
    rep.add("E4/8: milicja - przyrost / odmowione / ubytek do domu (srednio)", f"+{st.mean(up):.0f} / -{st.mean(ref):.0f} / -{st.mean(home):.0f} na dobe", "INFO (z30A model w ticku +92/d netto)", None)
    # N8 - nowe druzyny: kontrola podzialu szablonu
    n8bad, a3s, ns = 0, 0, 0
    for d in adays:
        x = arm[d]
        m = re.search(r"szablon (\d+) = z wolnych rak " + F_ + r" \+ ulamki bez DOM " + F_ + r" \+ z zalogi siedziby (\d+) \+ nie wystawieni (\d+) .*?\+ Straz \"\" (\d+) \+ ksiega bez poboru (\d+)", x)
        if not m:
            continue
        n, a1, fr, a2, a3, a4, off = int(m.group(1)), float(m.group(2)), float(m.group(3)), int(m.group(4)), int(m.group(5)), int(m.group(6)), int(m.group(7))
        ns += n
        a3s += a3
        if abs(n - (a1 + fr + a2 + a3 + a4 + off)) > 1.0:
            n8bad += 1
    rep.add("E4/8 N8: nowe druzyny - szablon = a1 + ulamki + a2 + a3 + a4 + bez poboru", f"doby z roznica > 1: {n8bad}; nie wystawieni {a3s} z {ns} ({100.0 * a3s / ns:.1f}%)" if ns else f"doby z roznica > 1: {n8bad}",
            "0 dob z roznica; udzial nie wystawionych INFO (oczekiwane < 20%)", n8bad == 0)
    # N9 - zboze z niczego
    g = sum(num(r"; zboze (\d+) szt\.", arm[d], int, 0) or 0 for d in adays)
    g3 = sum(num(r"z niczego \(brak zapasu\) (\d+)", arm[d], int, 0) or 0 for d in adays)
    rep.add("E4/8 N9: zboze nowych druzyn z niczego (suma okna)", f"{g3} z {g} ({100.0 * g3 / g:.1f}%)" if g else "0", "<= 5% zboza", (g3 <= 0.05 * g) if g else None)
    # N10 - wedrowcy BK
    w = sum(num(r"WEDROWCY BK \(R19\): wyjazdy (\d+)", arm[d], int, 0) or 0 for d in adays)
    b1 = sum(num(r"dojscia: do domu (\d+)", arm[d], int, 0) or 0 for d in adays)
    b2 = sum(num(r"do innej osady regionu (\d+)", arm[d], int, 0) or 0 for d in adays)
    b3 = sum(num(r"migracja do innego regionu (\d+)", arm[d], int, 0) or 0 for d in adays)
    b4 = sum(num(r"zniszczeni w drodze do domu (\d+)", arm[d], int, 0) or 0 for d in adays)
    b5 = sum(num(r"eskorta konwojow do domu (\d+)", arm[d], int, 0) or 0 for d in adays)
    road = [fnum(r"w drodze teraz " + F_, arm[d]) for d in adays]
    road = [x for x in road if x is not None]
    arr = b1 + b2 + b3 + b4 + b5
    rep.add("E4/8 N10: wedrowcy i konwoje BK - wyjazdy / dojscia (do domu, region, migracja, zniszczeni, eskorta)",
            f"{w} / {arr} ({b1}, {b2}, {b3}, {b4}, {b5}); do domu {100.0 * b1 / max(1, b1 + b2 + b3):.1f}% wedrowcow; w drodze ostatniej doby {road[-1] if road else '-'}",
            "INFO (z30A 93.7% do domu; wyjazdy = dojscia + rozbici + w drodze - bitwy w linii Pochodzenie)", None)
    # N11 - miasta R2
    nat = [(run_day(d, start), fnum(r"\| naturalne " + SF, mia[d])) for d in mdays]
    nat28 = [v for n, v in nat if v is not None and 28 <= n <= until]
    mig = [fnum(r"\| migracja ze wsi \+" + F_, mia[d]) or 0 for d in mdays if 28 <= run_day(d, start) <= until]
    starve = [num(r"bez migracji: glod (\d+)", mia[d], int, 0) or 0 for d in mdays]
    if nat28:
        rep.add(f"E4/8 N11: miasta - naturalne srednio d28-{until}", f"{st.mean(nat28):+.0f}/d", "-100..-250/d (DOK:524 ok. -144/d)", -250.0 <= st.mean(nat28) <= -100.0)
        rep.add(f"E4/8 N11: migracja ze wsi wobec ubytku naturalnego d28-{until}", f"+{st.mean(mig):.0f}/d wobec {-st.mean(nat28):.0f}/d (miasta z glodem bez migracji - srednio {st.mean(starve):.1f})",
                ">= ubytek naturalny (gdy w pokoju h1 = 0)", (st.mean(mig) >= -st.mean(nat28) * 0.95) if st.mean(starve) < 0.5 else None)
    else:
        rep.add("E4/8 N11: miasta R2", "brak dob 28+ z linia \"Miasta (E4/8 R2)\"", "-100..-250/d", None)
    if mdays:
        lm = mia[mdays[-1]]
        u = fnum(r"udzial miejski swiata dzis " + F_ + r"%", lm)
        u0 = fnum(r"\(odniesienie " + F_ + r"%", lm)
        rep.add(f"E4/8 N11: udzial miejski swiata d{run_day(mdays[-1], start)} wobec odniesienia", f"{u}% wobec {u0}%" if u is not None else "-", "+-2 pkt", (abs(u - u0) <= 2.0) if (u is not None and u0 is not None) else None)
    # N12 - rownanie krain z migracja (E4/7 T3)
    rep.add("E4/8 N12: rownanie krain z MigIn/MigOut", "patrz E4/7 T3 (odcinek E4/7 \"Ludzie (ksiega)\" z czlonem migracji E4/8)", "<= 0.1% ludzi krainy", None)
    # N13 - RESZTA linii Hearth
    hr = [fnum(r"\| RESZTA " + SF, hea[d]) for d in adays if d in hea]
    hr = [x for x in hr if x is not None]
    rep.add("E4/8 N13: Hearth (E4/0) RESZTA - srednia (z o13 E4/8 w \"nasze\")", f"{st.mean(hr):+.2f}/d (|max| {max(abs(x) for x in hr):.1f})" if hr else "-", "|srednia| <= 1 hearth/d", (abs(st.mean(hr)) <= 1.0) if hr else None)
    # N14 - netto Ludnosc (E4/7 T11)
    rep.add("E4/8 N14: ludnosc netto d28-120", "patrz E4/7 T11 (ubytek naturalny miast w netto)", "-720..+1150/d", None)
    # N15 - koszt
    ms = [fnum(r"\| czas " + F_ + r" ms\.", arm[d]) for d in adays]
    ms = [x for x in ms if x is not None]
    rep.add("E4/8 N15: koszt latek i linii E4/8 (srednio, najwiecej)", f"{st.mean(ms):.1f} / {max(ms):.1f} ms" if ms else "-", "<= 5 ms/dobe (srednio)", (st.mean(ms) <= 5.0) if ms else None)
    # N16 - zapis: odniesienia tr: po wczytaniu = miasta ze stanem
    if mdays:
        lm = mia[mdays[-1]]
        refs = num(r"odniesien tr: (\d+)", lm)
        towns = num(r"\| miast (\d+) ze stanem", lm)
        act = "R2 WYLACZONE" not in lm
        rep.add("E4/8 N16: odniesienia tr: = miasta ze stanem (ostatnia doba)", f"{refs} / {towns}", "rowne (przy czynnym R2)", (refs == towns) if (act and refs is not None and towns is not None) else None)
    rep.add("E4/8 N17: progi etapow 1-3", "--grupa etap2 / etap3 na tym samym logu", "INFO", None)
    # A5 - limity autowerbunku (od K8 "limit dzienny" = limit K8 GarrisonAutoRecruitPerDay)
    a5 = [re.search(r"A5: twierdz z werbunkiem (\d+) z (\d+) \(ludzi (\d+)\), limit dzienny (\d+), bez ochotnikow (\d+), zold albo miejsce (\d+)", arm[d]) for d in adays]
    a5 = [tuple(int(x) for x in m.groups()) for m in a5 if m]
    if a5:
        rep.add("E4/8 A5: twierdze z werbunkiem / ludzi / limit dzienny / bez ochotnikow / zold (srednio)",
                " / ".join(f"{st.mean(x[i] for x in a5):.0f}" for i in (0, 2, 3, 4, 5)), "INFO - limit dzienny = limit K8 (wiersz N4/K8)", None)


def etap4_e410(rep, lines, start, win, lud, until):
    """E4/10 (167): progi V1-V18 z planu kodu docs/etap4/PLAN-KODU-E410.md rozdz. 9 (stan z linii dziennych "Weterani (E4/10): dzien", nie startowej).
    Linie: "Weterani (E4/10)" (pula, wejscia wedlug strumienia, uprawnieni i udzial, najeci, powroty, karczmy, kontrola, czas), linia siewu / wczytania,
    "Ludzie:" (z karczmy - PeopleLedger.OnTroopRecruited), "Pochodzenie (E4/7)" (karczmy AI / karczma gracza = 0 przy czynnych weteranach),
    "Ludzie (ksiega)" (rownanie krain z kontem WETERANI), "Przegrani (H3)" / "Prawo jenca (I1)" / "Budzet rodow (166)" (do karczmy - te same ludzie co wejscia),
    "Komplet rekruta" (konie konnych weteranow z karczmy)."""
    stl = next((x for x in lines if x.startswith("Weterani (E4/10): wpiete")), "")
    brak = re.search(r"; BRAK: (.*?); wlasciciele", stl)
    rep.add("E4/10 V1: linia startowa (latki karczmy)", stl[:240], "\"BRAK: -\" (L1 bez dosypki, okno AI, okna gracza 2/2)",
            (brak is not None and brak.group(1).strip() == "-" and "okno najmu gracza 2/2" in stl) if stl else False)
    vet = e4_series(lines, "Weterani (E4/10): dzien")
    vdays = [d for d in sorted(vet) if 1 <= run_day(d, start) <= until]
    live = [d for d in vdays if "| pula " in vet[d]]
    if not live:
        rep.add("E4/10: linie dzienne \"Weterani (E4/10)\" po siewie", f"brak ({vet[vdays[-1]][:160] if vdays else 'brak linii'})", "kazdej doby po siewie", False)
        return
    wait = num(r"\| powroty \d+ po (\d+) dobach", vet[live[-1]], int, 120) or 120
    mint = num(r"ponizej tieru (\d+) do domu", vet[live[-1]], int, 3) or 3      # stan gry z linii dziennej (linia startowa - przed MCM)
    stum = [num(r"\| potkniecia (\d+) \(razem", vet[d], int, 0) or 0 for d in vdays]
    rep.add("E4/10 V1: potkniecia E4/10 (suma dob)", sum(stum), "<= 5 (z nazwa miejsca w ERROR PeopleBook.E4/10.*)", sum(stum) <= 5)
    tl = next((x for x in lines if x.startswith("Weterani (E4/10): siew dnia") or (x.startswith("Weterani (E4/10): z zapisu"))), "")
    zap = re.search(r"zapas wspolny: piesi tiery (\d+)-(\d+), konni tiery (\d+|brak)", tl)
    if zap:
        lo = min(int(zap.group(1)), int(zap.group(3)) if zap.group(3).isdigit() else 99)
        rep.add("E4/10 V1: najnizszy tier zapasu najemnikow (linia siewu/wczytania)", f"{lo} (piesi {zap.group(1)}-{zap.group(2)}, konni od {zap.group(3)})",
                f"= Veteran Min Tier ({mint})", lo == mint)
    else:
        rep.add("E4/10 V1: typy najemnikow", "brak linii siewu/wczytania", f"najnizszy tier zapasu = {mint}", None)
    # V2 - siew w d1-d2
    sd = [d for d in live if "[siew dnia" in vet[d]]
    if sd:
        sl = vet[sd[0]]
        m = re.search(r"starzy zolnierze z karczm (\d+) w (\d+) miastach.*?ze stanu ludzi miast \(E4/4\) (\d+), bez zdjecia \([^)]*\) (\d+)", sl)
        if m:
            n, tw, n1, n2 = (int(x) for x in m.groups())
            rep.add("E4/10 V2: siew (doba kampanii, ludzie, miasta, ze stanu miast, bez zdjecia)", f"d{run_day(sd[0], start)}: {n} w {tw} miastach, ze stanu {n1}, bez zdjecia {n2}",
                    "d1-d2, N > 0 w >= 90 miastach, bez zdjecia 0 (przy E4/4 i E4/7)", run_day(sd[0], start) <= 2 and n > 0 and tw >= 90 and n2 == 0)
    else:
        ld = next((x for x in lines if x.startswith("Weterani (E4/10): z zapisu")), None)
        rep.add("E4/10 V2: siew", "w logu tylko wczytanie" if ld else "brak linii siewu", "d1-d2 nowej kampanii", None)
    # V3 - kontrola puli
    ctl = [num(r"\(roznica (-?\d+)\)", vet[d], int) for d in live]
    ctl = [x for x in ctl if x is not None]
    rep.add("E4/10 V3: kontrola puli (najwieksza |roznica|)", f"{max(abs(x) for x in ctl)} ({len(ctl)} dob)" if ctl else "-", "0 kazdej doby", (max(abs(x) for x in ctl) == 0) if ctl else None)
    # V4 - udzial do karczmy
    U = sum(num(r"\| uprawnieni (\d+), do karczmy", vet[d], int, 0) or 0 for d in live)
    W = sum(num(r"\| uprawnieni \d+, do karczmy (\d+)", vet[d], int, 0) or 0 for d in live)
    tgt = fnum(r"\(cel " + F_ + r"%\)", vet[live[-1]])
    if U and tgt is not None:
        rep.add("E4/10 V4: udzial do karczmy (suma W / suma U)", f"{W} / {U} = {100.0 * W / U:.2f}% (cel {tgt:.1f}%)", "cel +- 1 pkt proc.", abs(100.0 * W / U - tgt) <= 1.0)
    else:
        rep.add("E4/10 V4: udzial do karczmy", f"uprawnionych {U}", "cel +- 1 pkt proc.", None)
    # V5 - najem bez okna / ponad pule / przyrost obcy
    nw = sum(num(r"najem bez okna (\d+)", vet[d], int, 0) or 0 for d in live)
    ov = sum(num(r"najem ponad pule (\d+)", vet[d], int, 0) or 0 for d in live)
    fo = sum(num(r"przyrost obcy (\d+)", vet[d], int, 0) or 0 for d in live)
    rep.add("E4/10 V5: najem bez okna / ponad pule / przyrost obcy (suma)", f"{nw} / {ov} / {fo}", "0 / 0 / 0", nw == 0 and ov == 0 and fo == 0)
    # V6 - najdluzej czeka
    old = [num(r"najdluzej czeka (\d+) dob", vet[d], int, 0) or 0 for d in live]
    rep.add("E4/10 V6: najdluzej czeka (najwiecej, poza oblezonymi)", max(old), f"<= {wait} (Veteran Wait Days)", max(old) <= wait)
    # V7 - z karczmy tylko z puli. Recenzja R1-1: bez doby siewu (liczniki najmu linii Weterani obejmuja 24 h przed siewem, gdy paczka byla nieczynna, a "z karczmy"
    # w Ludzie: liczy najem z karczm gry tych samych 24 h) i bez dob z paczka nieczynna/wylaczona (najem nie zdejmuje z puli - jak V11)
    def vet_on(d):
        return "[siew dnia" not in vet[d] and " - NIECZYNNE" not in vet[d] and "WYLACZONE" not in vet[d]
    diffs = []
    skip7 = sum(1 for d in live if not vet_on(d))
    for d in live:
        if not vet_on(d):
            continue
        x = lud.get(d)
        if not x:
            continue
        zk = num(r", z karczmy (\d+)", x)
        h1 = num(r"najeci \d+: lordowie (\d+)", vet[d], int, 0) or 0
        h2 = num(r"karawany (\d+), gracz", vet[d], int, 0) or 0
        ho = num(r"inne partie (\d+);", vet[d], int, 0) or 0
        if zk is not None:
            diffs.append((d, zk, h1 + h2 + ho))
    if diffs:
        worst = max(diffs, key=lambda t: abs(t[1] - t[2]))
        rep.add("E4/10 V7: \"z karczmy\" (Ludzie:) = najeci z puli (lordowie + karawany + inne)", f"najgorsza doba d{run_day(worst[0], start)}: {worst[1]} wobec {worst[2]} ({len(diffs)} dob; pominiete: siew / nieczynne {skip7})",
                "|roznica| <= 1 kazdej doby", abs(worst[1] - worst[2]) <= 1)
    else:
        rep.add("E4/10 V7: z karczmy tylko z puli", "brak linii Ludzie:", "|roznica| <= 1", None)
    # V8 - INFO najem/d
    h28 = [num(r"\| najeci (\d+):", vet[d], int, 0) or 0 for d in live if 28 <= run_day(d, start) <= until]
    hk = [num(r"; konni (\d+); tier karczmy", vet[d], int, 0) or 0 for d in live if 28 <= run_day(d, start) <= until]
    rep.add(f"E4/10 V8: najem z karczmy d28-{until} (srednio na dobe, w tym konni)", f"{st.mean(h28):.0f}/d (konni {st.mean(hk):.0f}/d)" if h28 else "brak dob 28+",
            "INFO (z30A 200/d, konni 81/d; plan [S] 30-60/d przy progu tieru 3)", None)
    # V9 - konni najeci <= konni weterani (siew + wejscia konne); konie z karczmy w Komplet rekruta
    seedh = num(r"\[siew dnia \d+ - starzy zolnierze z karczm \d+ w \d+ miastach \(konni (\d+)", vet[sd[0]], int, 0) if sd else 0
    inh = sum(num(r"\(konni (\d+)\) \| uprawnieni", vet[d], int, 0) or 0 for d in live)
    hh = sum(num(r"; konni (\d+); tier karczmy", vet[d], int, 0) or 0 for d in live)
    rep.add("E4/10 V9: konni najeci (suma) wobec konnych weteranow (siew + wejscia konne)", f"{hh} wobec {(seedh or 0) + inh}", "najeci <= konni weterani (typ konny tylko z konnego weterana; kon przy najmie dalej z wzorca, kon zwolnionego w zbrojowni pana - krok sprzetu)",
            hh <= (seedh or 0) + inh)
    kom = [s for s in lines if s.startswith("Komplet rekruta: dzien")]
    kh = sum(num(r"konie konnych weteranow z karczmy \(167\) (\d+)", s, int, 0) or 0 for s in kom)
    kold = sum(num(r"konie i rzedy najemnikow z wzorca \(160\) (\d+)", s, int, 0) or 0 for s in kom)
    rep.add("E4/10 V9: Komplet rekruta - konie konnych weteranow z karczmy (167) / po staremu (160)", f"{kh} / {kold}", "INFO (przy czynnych weteranach etykieta 167; 160 tylko przed siewem)", None)
    # V10 - rownanie krain z WETERANI (ta sama liczba co E4/7 T3)
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    rq = [re.search(r"rownanie krain: najgorsza kraina (\S+) reszta " + SF + r" \(" + SF + r"% ludzi krainy\)", kb[d]) for d in live if d in kb]
    rq = [(m.group(1), float(m.group(3))) for m in rq if m]
    if rq:
        w = max(rq, key=lambda x: abs(x[1]))
        rep.add("E4/10 V10: rownanie krain z kontem WETERANI (najgorsza kraina)", f"{w[0]} {w[1]:+.3f}%", "<= 0.1% kazdej doby (przy RESZCIE hearth != 0 - INFO jak E4/7 T3)", abs(w[1]) <= 0.1)
    # V11 - karczma E4/7 milknie przy czynnych weteranach
    poc = e4_series(lines, "Pochodzenie (E4/7): dzien")
    # recenzja R1-1: bez doby siewu (np. Veterans Enabled wlaczone w kampanii z juz zasianym E4/7 - karczmy E4/7 liczyly 24 h przed siewem)
    ta = [fnum(r"karczmy AI " + F_ + r", A5", poc[d]) or 0 for d in live if d in poc and vet_on(d)]
    tp = [fnum(r"karczma gracza " + F_, poc[d]) or 0 for d in live if d in poc and vet_on(d)]
    vh = [fnum(r"z karczmy \(WETERANI, E4/10\) " + F_, poc[d]) or 0 for d in live if d in poc]
    if ta or tp:
        rep.add("E4/10 V11: Pochodzenie - karczmy AI / karczma gracza (E4/7) przy czynnych weteranach; z karczmy (WETERANI)", f"{sum(ta):.0f} / {sum(tp):.0f}; {sum(vh):.0f}",
                "0 / 0 (Z8 - bez drugiego zdjecia stanu miasta); T2 i T6 - wiersze E4/7", sum(ta) == 0 and sum(tp) == 0)
    # krzyzowe: do karczmy w liniach wolajacych = wejscia wedlug strumienia (sumy okna; linie z dniem gry - shift 1)
    def ssum(pref, pat, shift):
        ser = e4_series(lines, pref, shift)
        return sum(num(pat, ser[d], int, 0) or 0 for d in ser if 1 <= run_day(d, start) <= until)
    h3t = ssum("Przegrani (H3): dzien", r", do karczmy \(E4/10\) (\d+) \(rozbici", 1)
    i1t = ssum("Prawo jenca (I1): dzien", r", do karczmy \(E4/10\) (\d+) \(w tym", 1)
    b6t = ssum("Budzet rodow (166): dzien", r"z tego do karczmy \(E4/10\) (\d+)", 1)
    def vsum(name):
        return sum(num(re.escape(name) + r" (\d+)", vet[d], int, 0) or 0 for d in live)
    vh3 = vsum("rozbici H3") + vsum("uwolnieni jency")
    vi1 = vsum("prawo jenca I1") + vsum("zostawieni po bitwie gracza")
    v166 = vsum("zwolnieni 166")
    ok = all(abs(a - b) <= max(5, 0.01 * max(a, b)) for a, b in ((h3t, vh3), (i1t, vi1), (b6t, v166)))
    rep.add("E4/10: do karczmy w liniach H3 / I1 / 166 wobec wejsc linii Weterani (sumy okna)", f"H3 {h3t} vs {vh3}, I1 {i1t} vs {vi1}, 166 {b6t} vs {v166}",
            "|roznica| <= max(5, 1%) (krawedzie okna)", ok)
    # wejscia wedlug strumienia (INFO)
    names = ["zwolnieni 166", "przepelnienie W4c", "rycerze 179", "rozbici H3", "uwolnieni jency", "prawo jenca I1", "zostawieni po bitwie gracza", "rozwiazane partie"]
    rep.add("E4/10: wejscia do puli wedlug strumienia (suma okna)", ", ".join(f"{n} {vsum(n)}" for n in names), "INFO (plan 0 pkt 14: do domu ok. 515/d, z progiem tieru 3 do karczm 30-60/d)", None)
    bel = sum(num(r"ponizej tieru \d+ do domu (\d+)", vet[d], int, 0) or 0 for d in live)
    nt = sum(num(r", bez miasta (\d+)", vet[d], int, 0) or 0 for d in live)
    rep.add("E4/10: ponizej tieru do domu / bez miasta (suma okna)", f"{bel} / {nt}", "INFO (bez miasta oczekiwane ~0)", None)
    bk = [num(r"powroty (\d+) po", vet[d], int, 0) or 0 for d in live]
    pool = [num(r"\| pula (\d+) w", vet[d], int, 0) or 0 for d in live]
    rep.add("E4/10: pula (pierwsza / ostatnia doba / najwiecej), powroty (suma)", f"{pool[0]} / {pool[-1]} / {max(pool)}, powroty {sum(bk)}", "INFO (plan [S] ok. 1-3 tys.)", None)
    # V12, V13, V14 - INFO (wiersze E4/7 T9, T10, --grupa etap3)
    rep.add("E4/10 V12/V13: wojsko lordow d90-120, Ludnosc BK (169c)", "wiersze E4/7 T9 i T10 w tym raporcie", "95-115 tys. (V12); <= +2%/rok (V13)", None)
    rep.add("E4/10 V14: zloto najmu do miast, progi etapu 3", "--grupa etap3 na tym samym logu (najemnicy z karczmy w ksiedze pieniadza)", "INFO (z30A 43.9 tys. zl/d)", None)
    # V15 - koszt
    ms = [fnum(r"\| czas " + F_ + r" ms\.", vet[d]) for d in live]
    ms = [x for x in ms if x is not None]
    rep.add("E4/10 V15: koszt (siew, powroty, Sync) srednio / najwiecej", f"{st.mean(ms):.2f} / {max(ms):.2f} ms" if ms else "-", "<= 3 ms/dobe (srednio)", (st.mean(ms) <= 3.0) if ms else None)
    # V16 - zapis i wczytanie
    ld = [i for i, x in enumerate(lines) if x.startswith("Weterani (E4/10): z zapisu")]
    sv = [x for x in lines if x.startswith("Weterani (E4/10): zapis arm_people")]
    if ld:
        i = ld[-1]
        x = lines[i]
        n = num(r"z zapisu (\d+) ludzi", x)
        unk = num(r"nieznane miasta (\d+)", x)
        prev = next((num(r"\| pula (\d+) w", lines[j]) for j in range(i - 1, -1, -1) if lines[j].startswith("Weterani (E4/10): dzien") and "| pula " in lines[j]), None)
        rep.add("E4/10 V16: wczytanie - pula z zapisu / ostatnia linia przed / nieznane miasta", f"{n} / {prev if prev is not None else '-'} / {unk}",
                "pula z zapisu = ostatnia linia przed zapisem (gdy w tym samym logu), nieznane miasta 0", unk == 0 and (prev is None or n == prev))
    else:
        rep.add("E4/10 V16: wczytanie", "brak linii wczytania (bieg bez zapisu)", "0 bledow, nieznane miasta 0", None)
    if sv:
        sz = [num(r"sekcje E4/10 (\d+) znakow", x, int) for x in sv]
        sz = [x for x in sz if x is not None]
        rep.add("E4/10 V16: rozmiar sekcji E4/10 w arm_people (pierwszy -> ostatni zapis)", f"{sz[0]} -> {sz[-1]} znakow" if sz else "-", "INFO (plan ok. 65 KB)", None)
    # V17 - INFO rody najemne
    hm = sum(num(r"w tym rody najemne (\d+)", vet[d], int, 0) or 0 for d in live)
    rep.add("E4/10 V17: najem rodow najemnych (suma okna)", hm, "INFO (185 kontrakt, Spoils - linie wlasne)", None)
    rep.add("E4/10 V18: menu \"People of this land\" - weterani", "proba reczna Jeffa (miasto: In this town's tavern ...)", "INFO - liczba = pula tego miasta", None)


def etap4_e44(rep, lines, start, win, hea, lud, until):
    """E4/4 (109n): progi z projektu etapu 4 rozdz. 3 "E4/4" Test (U12) i decyzji Jeffa 11.10 (pokoj +0.2..+0.8%/rok, ciagla wojna do -0.5%/rok).
    Poprawka po recenzji E4/4: przyrost naturalny dotyczy tylko ludzi WSI (miasta stoja - stan do E4/8), a decyzja Jeffa 11.10 mowi o pokoju - prog
    0.2-0.8%/rok sprawdzany na wsiach regionow w POKOJU (segment "pokoj: g ...% rocznie, przyrost +N ludzi (+R% rocznie) z P mln ludzi" linii
    "Ludzie: przyrost naturalny"; R = przyrost doby x 364 / P - w ludziach/d to P x 0.2..0.8% / 364). Dawny prog 290-1150 ludzi/d od 52.5 mln calej
    ludnosci (U12) - gorna granica nieosiagalna (wsie 41.7 mln x 0.8% / 364 = 916), dolna mieszala pokoj z wojna. Przyrost razem i wojna - INFO.
    Netto "Ludzie:" - INFO: przed E4/5 rabunek gry zdejmuje hearth w nicosc (zbior 30 test A d28-120: ok. -36 tys. ludzi/d) - prog netto (pokoj
    +0.2..+0.8, ciagla wojna >= -0.5%/rok) liczy sie w tescie razem z E4/5."""
    stl = next((x for x in lines if x.startswith("PopulationLaw: przyrost naturalny wsi (E4/4)")), "")
    rep.add("E4/4: linia startowa przyrostu (latka i okno podlogi)", stl[:240], "latka wpieta i \"okno na Village.DailyTick wpiete\", bez BRAK",
            ("wpieta" in stl and "okno na Village.DailyTick wpiete" in stl and "BRAK" not in stl) if stl else False)
    mod = next((x for x in lines if x.startswith("PopulationLaw: czynny model dobrobytu gry")), None)
    if mod:
        post = re.search(r"postfiksy w kolejnosci biegu: (.*?); prefiksy", mod)
        last = post.group(1).split(" -> ")[-1] if post else ""
        rep.add("E4/4: GrowthPostfix ostatni na CalculateHearthChange", (post.group(1)[:300] if post else "?"), "ostatni: PopulationLaw.GrowthPostfix (BK 400 -> BKROT 200 -> ScorchedEarth -> nasz)",
                "PopulationLaw.GrowthPostfix(" in last)
    else:
        rep.add("E4/4: GrowthPostfix ostatni na CalculateHearthChange", "brak linii pierwszej doby", "linia \"czynny model dobrobytu gry\" w pierwszej dobie", None)
    prz = e4_series(lines, "Ludzie: przyrost naturalny")
    pw = [d for d in win if d in prz and run_day(d, start) >= 28]
    off = sum(1 for d in win if d in prz and prz[d].startswith("Ludzie: przyrost naturalny WYLACZONY"))   # E4/6: tylko poczatek linii (odcinek glodu)
    rep.add("E4/4: przyrost naturalny czynny (linia dobowa)", f"czynny {sum(1 for d in win if d in prz) - off} dob, WYLACZONY {off} dob", "czynny kazdej doby",
            (off == 0 and any(d in prz for d in win)) if win else None)
    nat = [fnum(r"^Ludzie: przyrost naturalny " + SF + r" ludzi", prz[d]) for d in pw]
    nat = [x for x in nat if x is not None]
    tot = [re.search(r"pokoj i wojna \(.*?\): razem " + SF + r"% rocznie z " + F_ + r" mln ludzi wsi", prz[d]) for d in pw]
    tot = [(float(t.group(1)), float(t.group(2))) for t in tot if t]
    if nat:
        m = st.mean(nat)
        rest = (f" = {st.mean(r for r, _ in tot):+.3f}%/rok z {st.mean(p for _, p in tot):.2f} mln ludzi wsi" if tot else " (brak segmentu \"pokoj i wojna\")")
        rep.add(f"E4/4: przyrost naturalny razem d28-{until} (ludzi/d)", f"{m:+.0f} (min {min(nat):+.0f}, max {max(nat):+.0f}){rest}",
                "INFO (wsie w pokoju i w wojnie razem, miasta stoja; prog Jeffa 11.10 - wiersz \"w POKOJU\")", None)
    else:
        rep.add(f"E4/4: przyrost naturalny razem d28-{until} (ludzi/d)", "brak linii", "INFO", None)

    def side(name):
        """[(g, ludzi/d, % rocznie, mln ludzi, wsi)] z segmentu pokoj/wojna dob d28-until (doby bez wsi tej strony pominiete)."""
        out = []
        for d in pw:
            mm = re.search(name + r": g " + SF + r"% rocznie, przyrost " + SF + r" ludzi \(" + SF + r"% rocznie\) z " + F_ + r" mln ludzi w (\d+) wsiach", prz[d])
            if mm:
                out.append((float(mm.group(1)), float(mm.group(2)), float(mm.group(3)), float(mm.group(4)), int(mm.group(5))))
        return out

    ps = side("pokoj")
    if ps:
        rp, gp, mp, pp = st.mean(x[2] for x in ps), st.mean(x[0] for x in ps), st.mean(x[1] for x in ps), st.mean(x[3] for x in ps)
        rep.add(f"E4/4: przyrost naturalny wsi w POKOJU d28-{until} (% rocznie)",
                f"{rp:+.3f}%/rok (g {gp:+.3f}); {mp:+.0f} ludzi/d z {pp:.2f} mln (prog {pp * 1e6 * 0.002 / 364:.0f}-{pp * 1e6 * 0.008 / 364:.0f}/d); "
                f"wsi {min(x[4] for x in ps)}-{max(x[4] for x in ps)}, dob {len(ps)}",
                "0.2-0.8%/rok (decyzja Jeffa 11.10: w pokoju rosnie 0.2-0.8%/rok; tu przyrost naturalny - netto w pokoju w tescie razem z E4/5)", 0.2 <= rp <= 0.8)
    else:
        rep.add(f"E4/4: przyrost naturalny wsi w POKOJU d28-{until} (% rocznie)", "brak segmentu albo brak wsi w pokoju", "0.2-0.8%/rok", None)
    ws = side("wojna")
    rep.add(f"E4/4: przyrost naturalny wsi w WOJNIE d28-{until} (% rocznie)",
            (f"{st.mean(x[2] for x in ws):+.3f}%/rok (g {st.mean(x[0] for x in ws):+.3f}); {st.mean(x[1] for x in ws):+.0f} ludzi/d z {st.mean(x[3] for x in ws):.2f} mln; "
             f"wsi {min(x[4] for x in ws)}-{max(x[4] for x in ws)}, dob {len(ws)}") if ws else "-",
            "INFO (nizej niz pokoj: wojna U +0.3 -> g -0.18; prog wojny - netto >= -0.5%/rok - w tescie razem z E4/5)", None)
    gv = [fnum(r"\(g srednio " + SF + r"% rocznie", prz[d]) for d in pw]
    gv = [x for x in gv if x is not None]
    rep.add("E4/4: g srednio (pkt % rocznie, wazone ludnoscia wsi)", f"{st.mean(gv):+.3f}" if gv else "-", "INFO (pokoj i wojna razem - prog na wierszu \"w POKOJU\")", None)
    stb = [re.search(r"\| potkniecia: rachunek wsi (\d+), linia dobowa (\d+), podloga (\d+)(?:, miasta (\d+))?", prz[d]) for d in win if d in prz]
    stb = [sum(int(g or 0) for g in x.groups()) for x in stb if x]
    rep.add("E4/4: potkniecia przyrostu (rachunek wsi, linia dobowa, podloga, miasta)", f"{sum(stb)} w {len(stb)} dobach", "0", (sum(stb) == 0) if win else None)
    ref = [fnum(r"osobno powrot uchodzcow " + SF + r" ludzi", prz[d]) or 0 for d in pw]
    rep.add("E4/4: powrot uchodzcow +0.5 ponizej 40 (ludzie/d, z niczego do E4/5)", f"{st.mean(ref):+.0f}" if ref else "-", "INFO - znika w E4/5 (uchodzcy z ksiegi)", None)
    fl = [fnum(r"podloga z niczego " + F_ + r" ludzi", prz[d]) or 0 for d in win if d in prz]
    fp = [fnum(r"z puli wyrzutkow regionu " + F_ + r" ludzi", prz[d]) or 0 for d in win if d in prz]
    rep.add("E4/4: podloga 10 z niczego (R17, suma ludzi)", f"{sum(fl):.1f} (z puli wyrzutkow {sum(fp):.1f})" if fl else "-", "0 (oczekiwane; > 0 = nazwane, do E4/5 - uchodzcy)",
            (sum(fl) < 0.5) if fl else None)
    gm = [d for d in win if d in hea and "RESZTA" in hea[d]]
    withg = sum(1 for d in gm if "przyrost naturalny (E4/4)" in hea[d])
    rep.add("E4/4: tick wsi = przyrost naturalny (stala gry nie wchodzi) w linii Hearth", f"{withg} z {len(gm)} dob", "kazdej doby", (withg == len(gm)) if gm else None)
    dup = [num(r"wsie powtorzone w Settlement\.All - liczone raz \(E4/4\): (\d+)", hea[d], int, 0) or 0 for d in gm]
    dtx = next((re.search(r"liczone raz \(E4/4\): \d+ \((.*?)\)(?: \||\.$)", hea[d]) for d in gm if "liczone raz (E4/4)" in hea[d]), None)
    rep.add("E4/4: wsie powtorzone w Settlement.All (liczone raz)", f"{max(dup) if dup else 0}" + (f" - {dtx.group(1)[:200]}" if dtx else ""), "INFO (zbior 30: 571 wpisow, 569 wsi w regionach - zrodlo dawnej RESZTY +677)", None)
    net = [num(r"swiat: ludnosc [\d.]+ mln \(([+-]?\d+) ludzi\)", lud[d]) for d in win if d in lud and run_day(d, start) >= 28]
    net = [x for x in net if x is not None]
    raid = [num(r"tabory i lodzie [+-]?\d+, rabunek ([+-]?\d+), dekret", hea[d]) for d in gm if run_day(d, start) >= 28]
    raid = [x for x in raid if x is not None]
    if net:
        mn = st.mean(net)
        rm = st.mean(raid) if raid else 0.0
        rep.add(f"E4/4: netto \"Ludzie:\" d28-{until} (ludzi/d)", f"{mn:+.0f} = {100.0 * mn * 364 / 52.5e6:+.2f}%/rok od 52.5 mln; w tym rabunek gry {rm:+.0f}/d",
                "INFO do E4/5 (rabunek w nicosc); prog etapu Jeffa 11.10: pokoj +0.2..+0.8%/rok, ciagla wojna >= -0.5%/rok - test razem z E4/5", None)
        rep.add(f"E4/4: netto bez rabunku gry d28-{until} (ludzi/d)", f"{mn - rm:+.0f} = {100.0 * (mn - rm) * 364 / 52.5e6:+.2f}%/rok",
                "INFO (przyrost naturalny - tabory do konta TABOR - wyrzutki; smierc w ksiedze od E4/7)", None)
    tw = [x for x in lines if x.startswith("PopulationLaw: ludnosc miast zapisana jako stan (E4/4")]
    rep.add("E4/4: ludnosc miast jako stan (zalozenie)", tw[0][:200] if tw else "brak linii", "jedna linia zalozenia na kampanie (nowa: przy kalibracji)", (len(tw) >= 1) if win else None)
    lastp = prz.get(win[-1]) if win else None
    drift = re.search(r"\| miasta: (.*?)(?: \||\.$)", lastp) if lastp else None
    rep.add(f"E4/4: miasta d{until} (stan wobec dobrobytu)", drift.group(1)[:200] if drift else "-", "INFO (stan stoi; dryf dobrobytu bez wplywu na ksiege)", None)


# E4/5: szacunki planu (docs/etap4/PLAN-KODU-E45.md rozdz. 11, z LOG-A zbioru 30): rabunek ok. 150 trafionych/d, zerowanie ok. 150/d, lup gry ok. 30 zl na krok
E45_Q = 1.0 - (1.0 - 0.013) ** (1.0 / 364.0)     # dzienna smierc uchodzcow przy 1.3%/rok i roku 364 dob


def ludzie_csv_of(logp):
    """<katalog>/Armoury-<znacznik>.log -> <katalog>/<znacznik>/ludzie-regiony.csv (Log.Csv: katalog tematyczny sesji)."""
    d, f = os.path.split(logp)
    stem = os.path.splitext(f)[0].replace("Armoury-", "")
    return os.path.join(d, stem, "ludzie-regiony.csv")


def etap4_e45(rep, lines, start, win, hea, lud, until, logp):
    """E4/5 (113n): progi S1-S21 z planu kodu E4/5 (rozdz. 11) mierzalne w jednym biegu 120 dob. S6/S14 (porownanie z biegiem E4/4 bez E4/5),
    S19 (zapis 9 dob), S20 (grupy etap2/etap3) i S21 (bieg z DevastationEnabled=false) - INFO z opisem. Linia dzienna "Ludzie (spustoszenie)",
    odcinek "E4/5 konto UCHODZCY" w "Ludzie (ksiega)", "Hearth (E4/0)", "Ludzie:" (razem z uchodzcami), CSV ludzie-regiony.csv (4 kolumny E4/5)."""
    dev = e4_series(lines, "Ludzie (spustoszenie): dzien")
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    e42 = e4_series(lines, "Inni z poleglych (E4/2): dzien")
    on = [d for d in win if d in dev and "| CZYNNE" in dev[d]]
    off = [d for d in win if d in dev and "WYLACZONE" in dev[d]]
    d31 = [d for d in on if run_day(d, start) >= 31]
    # S1
    errs = [s for s in lines if s.startswith("ERROR in Devastation")]
    stb = [num(r"\| potkniecia (\d+) \(razem", dev[d], int, 0) or 0 for d in on]
    rep.add("E4/5 S1: bledy i potkniecia Devastation", f"bledy {len(errs)}, potkniecia {sum(stb)} w {sum(1 for x in stb if x)} dobach",
            "0 bledow; potkniecia 0 (najwyzej 5 w biegu, z nazwa miejsca)", len(errs) == 0 and sum(stb) <= 5)
    # S2
    stl = next((s for s in lines if s.startswith("Devastation (E4/5):")), "")
    brak = re.search(r"; BRAK: (.*?)(?: \(bez latki|\.$)", stl)
    wired = re.search(r"; wpiete: (.*?); BRAK", stl)
    nw = len(wired.group(1).split(", ")) if wired else 0
    rep.add("E4/5 S2: linia startowa (stan, 5 celow)", f"{'CZYNNE' if ' CZYNNE' in stl else stl[34:80]}; wpiete {nw}; BRAK: {brak.group(1) if brak else '?'}",
            "CZYNNE, wpiete 5, BRAK: -", " CZYNNE" in stl and nw == 5 and brak is not None and brak.group(1).strip() == "-")
    rep.add("E4/5: linia dzienna CZYNNE", f"czynne {len(on)} dob, WYLACZONE {len(off)} dob z {len(win)}", "czynne kazdej doby z ruchem (bieg zwykly)",
            (len(off) == 0 and len(on) > 0) if win else None)
    # S3
    chk = [fnum(r"uchodzcy [\d.]+ \(kontrola " + SF + r"\)", dev[d]) for d in on]
    chk = [x for x in chk if x is not None]
    cap = [fnum(r"\+ pojmani " + F_ + r" \+ las", dev[d]) or 0 for d in on]
    rep.add("E4/5 S3: kontrola trafionych (= zabici + pojmani + las + uchodzcy)", f"max |kontrola| {max((abs(x) for x in chk), default=0):.4f} w {len(chk)} dobach; pojmani razem {sum(cap):.1f}",
            "|kontrola| <= 0.01 czl. kazdej doby; pojmani przy uderzeniu 0 (E4/9: pojmani z uchodzcow przy koncu rabunku - E4/9 K16)", (max((abs(x) for x in chk), default=0) <= 0.01 and sum(cap) == 0) if chk else None)
    # S4
    c1 = [fnum(r"konto UCHODZCY: wczoraj .*?\(kontrola " + SF + r"\)", dev[d]) for d in on]
    c1 = [x for x in c1 if x is not None]
    c2 = [fnum(r"E4/5 konto UCHODZCY: .*?\(kontrola " + SF + r"\)", kb[d]) for d in win if d in kb]
    c2 = [x for x in c2 if x is not None]
    same = 0; diff = 0
    for d in on:
        a = fnum(r"konto UCHODZCY: .*?= " + F_ + r"(?: \(kontrola [^)]*\))? w \d+ wsiach", dev[d])
        b = fnum(r"E4/5 konto UCHODZCY: " + F_ + r" ludzi", kb.get(d))
        if a is None or b is None:
            continue
        if abs(a - b) <= 0.1: same += 1
        else: diff += 1
    m1 = max((abs(x) for x in c1), default=0); m2 = max((abs(x) for x in c2), default=0)
    rep.add("E4/5 S4: kontrola konta UCHODZCY (obie linie)", f"max |kontrola| spustoszenie {m1:.4f} ({len(c1)} dob), ksiega {m2:.4f} ({len(c2)} dob); ta sama liczba {same}, rozna {diff}",
            "|kontrola| <= 0.01 kazdej doby i ta sama liczba w obu liniach", (m1 <= 0.01 and m2 <= 0.01 and diff == 0) if (c1 or c2) else None)
    # S5
    gm = [d for d in win if d in hea and "RESZTA" in hea[d]]
    rg = [(d, fnum(r" - rabunek gry -" + F_, hea[d]) or 0.0, num(r"pod spustoszeniem E4/5 krokow (\d+)", hea[d], int, 0) or 0) for d in gm if d in on]
    bad = [run_day(d, start) for d, h, _ in rg if abs(h) >= 0.005]
    steps = sum(x[2] for x in rg)
    rep.add("E4/5 S5: \"rabunek gry\" w Hearth (E4/0) przy CZYNNYM", f"niezerowy w {len(bad)} dobach" + (f" (d{', d'.join(str(x) for x in bad[:6])})" if bad else "") + f"; krokow pod spustoszeniem {steps}",
            "0.00 hearth kazdej doby; krokow pod spustoszeniem > 0 w dobach z rabunkami", (len(bad) == 0 and steps > 0) if rg else None)
    # S6 / S14 (porownanie z biegiem E4/4)
    rest = [fnum(r"RESZTA " + SF, hea[d]) for d in gm]
    rest = [x for x in rest if x is not None]
    rep.add("E4/5 S6: RESZTA Hearth (E4/0) srednio (porownac z biegiem E4/4)", f"{st.mean(rest):+.3f} hearth/d, |RESZTA| <= 1 w {100.0 * sum(1 for x in rest if abs(x) <= 1) / len(rest):.1f}% dob" if rest else "-",
            "roznica do biegu E4/4 <= 0.2 hearth/d (bieg bazowy E4/4 osobno - T2 wyzej)", None)
    # S7
    rh = [fnum(r"osobodni lupienia [\d.]+ x [\d.]+ -> trafieni " + F_, dev[d]) or 0.0 for d in d31]
    rs = [num(r"krokow gry (\d+)", dev[d], int, 0) or 0 for d in on]
    rall = [fnum(r"osobodni lupienia [\d.]+ x [\d.]+ -> trafieni " + F_, dev[d]) or 0.0 for d in on]
    per_raid = (sum(rall) / (sum(rs) / 20.0)) if sum(rs) > 0 else None
    rep.add(f"E4/5 S7: trafieni z rabunku d31-{until} (ludzi/d)", (f"{st.mean(rh):.0f}/d" if rh else "-") + (f"; na pelny rabunek (kroki/20) {per_raid:.0f}" if per_raid else ""),
            "50-500/d (szacunek 150); na pelny rabunek 60-400 - ponizej/ponad = sprawdzic okno, nie stroic na slepo",
            (50 <= st.mean(rh) <= 500 and (per_raid is None or 60 <= per_raid <= 400)) if rh else None)
    # S8
    fh = [fnum(r"\| zerowanie: partii \d+, osobodni [\d.]+ x [\d.]+ -> trafieni " + F_, dev[d]) or 0.0 for d in d31]
    if fh:
        m = st.mean(fh)
        rep.add(f"E4/5 S8: trafieni z zerowania d31-{until} (ludzi/d)", f"{m:.0f}/d", "50-450/d (szacunek 150); > 1150/d = Devastation Per Forager Day w dol (decyzja, nie blad)",
                50 <= m <= 450, decide=m > 1150)
    else:
        rep.add(f"E4/5 S8: trafieni z zerowania d31-{until} (ludzi/d)", "-", "50-450/d", None)
    # S9
    tot = [re.search(r"\| trafieni " + F_ + r" = zabici " + F_ + r" \+ pojmani " + F_ + r" \+ las " + F_ + r" \+ uchodzcy " + F_, dev[d]) for d in on]
    tot = [tuple(float(g) for g in x.groups()) for x in tot if x]
    sl = [re.search(r"najezdzcy z niewola: trafieni " + F_ + r", zabici " + F_, dev[d]) for d in on]
    sl = [tuple(float(g) for g in x.groups()) for x in sl if x]
    if tot and sum(t[0] for t in tot) > 0:
        T = sum(t[0] for t in tot); K = sum(t[1] for t in tot); O = sum(t[3] for t in tot)
        ST = sum(x[0] for x in sl); SK = sum(x[1] for x in sl)
        outlaw = any(x.startswith("Pula wyrzutkow (E4/3)") for x in lines)
        rep.add("E4/5 S9: zabici / trafieni (okno)", f"{100 * K / T:.2f}% ({K:.0f} z {T:.0f})" + (f"; najezdzcy z niewola {100 * SK / ST:.2f}% ({SK:.0f} z {ST:.0f})" if ST > 0 else ""),
                "1.5-5% (najezdzcy z niewola ok. 5%)", 1.5 <= 100 * K / T <= 5.0)
        rep.add("E4/5 S9: las / trafieni (okno)", f"{100 * O / T:.2f}%", "2-5% przy prawie wyrzutkow" + ("" if outlaw else " (prawo wyrzutkow wylaczone - 0)"),
                (2.0 <= 100 * O / T <= 5.0) if outlaw else (O == 0))
    else:
        rep.add("E4/5 S9: zabici / las wobec trafionych", "brak trafionych", "zabici 1.5-5%, las 2-5%", None)
    # S10 / S11 (CSV)
    lin = [fnum(r"kontrola liniowosci max " + F_, dev[d]) for d in on]
    lin = [x for x in lin if x is not None]
    rep.add("E4/5 S11: kontrola liniowosci w linii (max)", f"{max(lin, default=0):.2e}", "<= 1e-6", (max(lin, default=0) <= 1e-6) if lin else None)
    cp = ludzie_csv_of(logp)
    rows = []
    if os.path.isfile(cp):
        with open(cp, encoding="utf-8", errors="replace", newline="") as f:
            rows = [r for r in csv.DictReader(f, delimiter=";")]
    byreg = {}
    for r in rows:
        try:
            dd = int(r["dzien"])
        except (KeyError, ValueError):
            continue
        if not (1 <= run_day(dd, start) <= until):
            continue
        u = r.get("uchodzcy"); dn = r.get("dni_od_spustoszenia")
        if u is None:
            continue
        try:
            uu = float(u or 0); dd2 = int(dn) if dn not in (None, "") else None
        except ValueError:
            continue
        byreg.setdefault(r.get("region_id"), {})[dd] = (uu, dd2)
    # poprawka po recenzji E4/6 (R2-4): wiersze regionow, ktorych konto uchodzcow E4/6 zmienil tej doby (brama powrotu, czesc z glodu liczona od odzyskania wsi,
    # zgony i ucieczki z glodu) - kolumna konto_e46 w spichlerze-regiony.csv; S10/S11 opisuja sama regule E4/5, wiec je pomijaja
    e46_skip = set()
    fp = os.path.join(os.path.dirname(cp), "spichlerze-regiony.csv")
    if os.path.isfile(fp):
        with open(fp, encoding="utf-8", errors="replace", newline="") as f:
            for r in csv.DictReader(f, delimiter=";"):
                if (r.get("konto_e46") or "").strip() == "1":
                    try:
                        e46_skip.add((r.get("region_id"), int(r["dzien"])))
                    except (KeyError, ValueError):
                        pass
    if byreg:
        s10_bad = s10_n = 0; s11 = []; skipped = 0
        for reg, ser in byreg.items():
            for dd in sorted(ser):
                if dd - 1 not in ser:
                    continue
                u0, d0 = ser[dd - 1]; u1, d1 = ser[dd]
                if d1 is None:
                    continue
                if (reg, dd) in e46_skip:
                    skipped += 1
                    continue
                if d1 < 30:
                    s10_n += 1
                    if u1 < u0 * (1 - E45_Q) - 0.5: s10_bad += 1
                elif u0 >= 1000 and 30 <= d1 < 364 and d0 is not None and d1 == d0 + 1:
                    exp = u0 * (1 - E45_Q) * (1 - 1.0 / max(1, 364 - d1))
                    if exp > 0: s11.append(u1 / exp - 1)
        rep.add("E4/5 S10: powrot w regionach z najazdem < 30 dob (CSV)", f"zlamane {s10_bad} z {s10_n} wierszy" + (f" (pominiete - E4/6 na koncie regionu: {skipped} wierszy S10/S11)" if skipped else ""),
                "0 (uchodzcy(t) >= uchodzcy(t-1) x (1 - q) - 0.5)",
                (s10_bad == 0) if s10_n else None)
        out = sum(1 for x in s11 if abs(x) > 0.01)
        rep.add("E4/5 S11: liniowosc powrotu (CSV, regiony >= 1000 uchodzcow, 30 <= d < 364)", (f"{len(s11)} wierszy, poza +-1%: {out}; max |odchylka| {max((abs(x) for x in s11), default=0) * 100:.3f}%") if s11 else "brak takich regionow w oknie",
                "+-1% kazdy wiersz", (out == 0) if s11 else None)
    else:
        rep.add("E4/5 S10/S11: CSV ludzie-regiony.csv", f"brak pliku albo kolumn E4/5 ({cp})", "plik z kolumnami uchodzcy, dni_od_spustoszenia", None)
    # S12
    th = [(num(r"wsi z >= 1/3 uchodzcow (\d+)", dev[d], int, 0) or 0, fnum(r"najwyzszy mnoznik wsrod nich " + F_, dev[d])) for d in on]
    withth = [x for x in th if x[0] > 0]
    if withth:
        mk = max(x[1] for x in withth if x[1] is not None)
        rep.add("E4/5 S12: plon wsi z >= 1/3 uchodzcow (najwyzszy mnoznik)", f"{mk:.4f} w {len(withth)} dobach (max {max(x[0] for x in withth)} wsi)", "<= 0.67", mk <= 0.67)
    else:
        lw = [re.search(r"najnizszy mnoznik " + F_ + r" \(.*?, udzial uchodzcow " + F_ + r"\)", dev[d]) for d in on]
        lw = [(float(x.group(1)), float(x.group(2))) for x in lw if x and float(x.group(2)) > 0]
        err = max((abs(m - (1 - u)) for m, u in lw), default=None)
        rep.add("E4/5 S12: plon - brak wsi z >= 1/3 uchodzcow w oknie", (f"kontrola wzoru: max |mnoznik - (1 - udzial)| {err:.5f} w {len(lw)} dobach") if err is not None else "brak wsi z uchodzcami",
                "INFO; wzor +-1e-3 (elastycznosc 1)", (err <= 1e-3) if err is not None else None)
    # S13
    # poprawka po recenzji E4/5 (R2): woz wsi rusza tylko ze wsi Normal (gra TickVillageThink), a TickProductions staje przy zapasie >= 1.5 W - wies Looted
    # produkuje ok. 7.5 doby, potem stoi z towarem w magazynie. Prog liczony na podmianach z OTWARTA bramka; udzial dob z zamknieta bramka - INFO
    lt = [re.search(r"Looted dzis (\d+), produkowaly (\d+) \(sztuk (\d+); podmian stanu (\d+), bramka magazynu 1\.5 W zamknieta (\d+)", dev[d]) for d in on]
    lt = [tuple(int(g) for g in x.groups()) for x in lt if x]
    lt = [x for x in lt if x[3] > 0]
    if lt:
        Ls = sum(x[0] for x in lt); Ps = sum(x[1] for x in lt); Ws = sum(x[3] for x in lt); Gs = sum(x[4] for x in lt); Os = Ws - Gs
        rep.add("E4/5 S13: wsie Looted produkuja (podmiany z otwarta bramka 1.5 W)",
                (f"produkowaly {Ps} z {Os} ({100.0 * Ps / Os:.0f}%)" if Os > 0 else f"produkowaly {Ps}, otwartych 0") + f"; sztuk {sum(x[2] for x in lt)}, podmian {Ws}, Looted dzis razem {Ls} w {len(lt)} dobach",
                ">= 80% podmian z otwarta bramka", (Ps >= 0.8 * Os) if Os > 0 else None)
        rep.add("E4/5 S13: bramka magazynu 1.5 W zamknieta we wsiach Looted (INFO)", f"{Gs} z {Ws} podmian ({100.0 * Gs / Ws:.0f}%)",
                "INFO - towar wsi Looted czeka w magazynie na powrot do Normal (woz rusza tylko ze wsi Normal)", None)
    else:
        rep.add("E4/5 S13: wsie Looted produkuja", "brak dob z podmiana stanu wsi Looted", ">= 80% podmian z otwarta bramka", None)
    rep.add("E4/5 S14: Ruda/Drewno/Towary d1 wobec biegu E4/4", "porownac linie \"Towary\"/OreLedger d1 z biegiem E4/4", "+-3% (w d1 brak uchodzcow, mnoznik 1)", None)
    # S15 - ludnosc z uchodzcami (decyzja Jeffa 11.10 (4): w ciaglej wojnie moze spadac do -0.5%/rok)
    net = [num(r"razem z nimi [\d.]+ mln \(([+-]?\d+) ludzi\)", lud[d]) for d in win if d in lud and run_day(d, start) >= 28]
    net = [x for x in net if x is not None]
    kill = [fnum(r"\| trafieni [\d.]+ = zabici " + F_, dev[d]) or 0 for d in on if run_day(d, start) >= 28]
    # poprawka po recenzji E4/5 (R1): "las" (2-5% trafionych, OutlawLaw.AddFled) zyje w puli wyrzutkow - nie ma go ani w ludnosci, ani w uchodzcach
    fled = [fnum(r"\| trafieni [\d.]+ = zabici [\d.]+ \+ pojmani [\d.]+ \+ las " + F_, dev[d]) or 0 for d in on if run_day(d, start) >= 28]
    died = [fnum(r" - zmarli " + F_ + r" =", dev[d]) or 0 for d in on if run_day(d, start) >= 28]
    # E4/6: zmarli z glodu (gospodarze, mieszczanie; uchodzcy sa juz w "zmarli" konta) obnizaja dolna granice jak zabici
    fam = e4_series(lines, "Spichlerze (E4/6): dzien")
    starv = [(fnum(r"zmarli z glodu: gospodarze " + F_, fam.get(d)) or 0) + (fnum(r", mieszczanie " + F_, fam.get(d)) or 0) for d in on if run_day(d, start) >= 28]
    if net:
        mk = st.mean(kill) if kill else 0; mf = st.mean(fled) if fled else 0; md = st.mean(died) if died else 0
        ms = st.mean(starv) if starv else 0
        mn = st.mean(net); lo = 290 - mk - mf - md - ms
        rep.add(f"E4/5 S15: ludnosc razem z uchodzcami d28-{until} (ludzi/d)",
                f"{mn:+.0f}/d = {100.0 * mn * 364 / 52.5e6:+.2f}%/rok od 52.5 mln (zabici {mk:.0f}/d, las {mf:.1f}/d, zmarli uchodzcy {md:.1f}/d, zmarli z glodu E4/6 {ms:.1f}/d; dawniej rabunek gry ok. -28.8 tys./d = -20%/rok)",
                f"pas E4/4 290-1150/d albo nizej tylko o zabitych, las, zmarlych uchodzcow i zmarlych z glodu (dolna granica {lo:.0f}); nigdy ponizej -0.5%/rok (-721/d)",
                (lo <= mn <= 1150) and mn >= -721)
    else:
        rep.add(f"E4/5 S15: ludnosc razem z uchodzcami d28-{until}", "brak \"razem z nimi\" w linii Ludzie:", "290-1150/d", None)
    # S16 - lup z niczego na krok (co do monety jak w grze)
    lp = [(num(r"lup z niczego (\d+) zl", hea[d], int, 0) or 0, (num(r" - rabunek gry -[\d.]+ \(krokow (\d+)", hea[d], int, 0) or 0) + (num(r"pod spustoszeniem E4/5 krokow (\d+)", hea[d], int, 0) or 0)) for d in gm]
    G = sum(x[0] for x in lp); S = sum(x[1] for x in lp)
    rep.add("E4/5 S16: lup z niczego na krok rabunku (Hearth E4/0, W6 + spustoszenie)", f"{G / S:.1f} zl/krok ({G} zl, {S} krokow)" if S else "-", "+-30% wobec LOG-A ok. 30 zl/krok (21-39)",
            (21 <= G / S <= 39) if S else None)
    # S17 - K7 rajd Innych
    rb = [re.search(r"\(b\) rajd: .*?zabici przy spustoszeniu (\d+), wstalo (\d+), do puli (\d+), spalone (\d+)", e42[d]) for d in win if d in e42]
    rb = [tuple(int(g) for g in x.groups()) for x in rb if x]
    if rb:
        D_ = sum(x[0] for x in rb); R_ = sum(x[1] for x in rb); P_ = sum(x[2] for x in rb); B_ = sum(x[3] for x in rb)
        rep.add("E4/5 S17: rajd Innych - zabici = wstalo + pula + spalone", f"zabici {D_} = wstalo {R_} + pula {P_} + spalone {B_} (kontrola {D_ - R_ - P_ - B_})", "kontrola 0; poza Murem wstalo 0", D_ - R_ - P_ - B_ == 0)
    else:
        rep.add("E4/5 S17: rajd Innych (K7)", "brak odcinka (b) z zabitymi", "kontrola 0", None)
    # S18 - czas
    tm = [fnum(r"\| czas " + F_ + r" ms\.$", dev[d]) for d in on]
    tm = [x for x in tm if x is not None]
    rep.add("E4/5 S18: czas linii Ludzie (spustoszenie)", f"{st.mean(tm):.2f} ms/d (max {max(tm):.1f})" if tm else "-", "<= 3 ms/d srednio; s/dobe biegu <= +3% wobec E4/4 (autotest)", (st.mean(tm) <= 3.0) if tm else None)
    # S19-S21 i informacje
    zz = [num(r"zegary regionow (\d+)", kb[d], int, 0) or 0 for d in win if d in kb]
    rep.add("E4/5 S19: zapis 9 dob (konto i zegary po wczytaniu)", f"zegary regionow w ksiedze: ostatnio {zz[-1] if zz else '-'}", "autotest zapisu: konto UCHODZCY po wczytaniu = stan doby zapisu (+- ruch doby)", None)
    rep.add("E4/5 S20: progi etapow 1-3", "--grupa etap2 / etap3 na tym samym logu", "bez zmian", None)
    rep.add("E4/5 S21: wylacznik (bieg 40 dob z DevastationEnabled=false)", f"doby WYLACZONE w tym biegu: {len(off)}", "osobny autotest: pierwsza doba jedna linia WYLACZONE ... wrocilo N, potem rabunek gry, konto 0", None)
    rb2 = [(fnum(r"powrot: ludzi " + F_, dev[d]) or 0) for d in on]
    acc = [fnum(r"konto UCHODZCY: .*?= " + F_ + r"(?: \(kontrola [^)]*\))? w", dev[d]) for d in on]
    acc = [x for x in acc if x is not None]
    rep.add("E4/5: uchodzcy - konto i powrot (INFO)", (f"konto d{run_day(on[-1], start)} {acc[-1]:.0f} (max {max(acc):.0f}); powrot razem {sum(rb2):.0f} ludzi") if acc else "-",
            "INFO (powrot rusza po 30 dobach spokoju; w biegu 728 dob: wsie spalone w d1-364 bez kolejnego najazdu - >= 95% w domu)", None)
    sp = [num(r"od ponad 90 dob (\d+)", dev[d], int, 0) or 0 for d in on]
    rep.add("E4/5: spirala na pograniczu (regiony > 25% uchodzcow i najazd < 30 dob od > 90 dob)", f"max {max(sp, default=0)}", "INFO - decyzja Jeffa dopiero z liczbami (rozdz. 12 pkt 18)", None)


def etap4_e46(rep, lines, start, win, until):
    """E4/6 (191): progi z projektu etapu 4 rozdz. 3 "E4/6" Test i rozdz. 4 (prog konca etapu dla zimy) - linia dzienna "Spichlerze (E4/6)", linia startowa
    "Famine (E4/6)", odcinek "glod: zmarli z glodu" w "Ludzie (ksiega)", "Ludzie: przyrost naturalny" (H). F6/F7 (zima) wiaza tylko w biegu z zima
    (dzienne "zima: TAK"); w biegu zwyklym - INFO. F8 (glodne okregi tylko spustoszone/oblezone) - doby bez zimy."""
    fam = e4_series(lines, "Spichlerze (E4/6): dzien")
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    prz = e4_series(lines, "Ludzie: przyrost naturalny")
    on = [d for d in win if d in fam and "| CZYNNE" in fam[d]]
    off = [d for d in win if d in fam and "| WYLACZONE" in fam[d]]
    # F1 bledy i potkniecia
    errs = [x for x in lines if x.startswith("ERROR in Famine")]
    stb = [num(r"\| potkniecia (\d+) \(razem", fam[d], int, 0) or 0 for d in on]
    rep.add("E4/6 F1: bledy i potkniecia Famine", f"bledy {len(errs)}, potkniecia {sum(stb)} w {sum(1 for x in stb if x)} dobach",
            "0 bledow; potkniecia 0 (najwyzej 5 w biegu, z nazwa miejsca)", len(errs) == 0 and sum(stb) <= 5)
    # F2 linia startowa
    stl = next((x for x in lines if x.startswith("Famine (E4/6):")), "")
    brak = re.search(r"; BRAK: (.*?)\.$", stl)
    rep.add("E4/6 F2: linia startowa (stan, latka spichlerza warowni)", f"{'CZYNNE' if ' CZYNNE' in stl else stl[40:90]}; BRAK: {brak.group(1) if brak else '?'}",
            "CZYNNE, wpiete: spichlerz warowni, BRAK: -", " CZYNNE" in stl and "wpiete: spichlerz warowni" in stl and brak is not None and brak.group(1).strip() == "-")
    # F3 linia dzienna
    rep.add("E4/6 F3: linia dzienna CZYNNE", f"czynne {len(on)} dob, WYLACZONE {len(off)} dob z {len(win)}", "czynne kazdej doby", (len(off) == 0 and len(on) == len(win)) if win else None)
    if not on:
        return
    # F4 kontrola spichlerzy (racje: stan = wczoraj + plon - zjedli - nadwyzka)
    bad = []
    for d in on:
        c = fnum(r"kontrola spichlerzy " + SF, fam[d])
        tot = fnum(r"spichlerze swiata " + F_ + r" mln racji", fam[d])
        if c is None or tot is None:
            continue
        if abs(c) > max(1.0, 1e-9 * tot * 1e6):
            bad.append(run_day(d, start))
    rep.add("E4/6 F4: kontrola spichlerzy", f"poza tolerancja {len(bad)} dob" + (f" (d{', d'.join(str(x) for x in bad[:6])})" if bad else ""), "|kontrola| <= max(1, 1e-9 stanu) kazdej doby", len(bad) == 0)
    # F5 zalozenie spichlerzy
    first = on[0]
    sd = num(r"zalozone dzis (\d+)", fam[first], int, 0) or 0
    wn = num(r"\| wsie (\d+) \(bez przelicznika", fam[first], int, 0) or 0
    days0 = fnum(r"mln racji = " + F_ + r" dni potrzeby", fam[first])
    rep.add("E4/6 F5: spichlerze zalozone w pierwszej dobie (INFO)", f"zalozone {sd} z {wn} wsi; dni potrzeby swiata d{run_day(first, start)} {days0}",
            "INFO - nowa kampania: wszystkie wsie, start Famine Start Share sufitu (1 = 728 dni)", None)
    # F6/F7 zima
    wdays = [d for d in on if "zima: TAK" in fam[d]]
    starve = None
    for d in wdays:
        m = re.search(r"pierwsza doba glodu warowni AI w tej zimie: (\d+)", fam[d])
        if m:
            starve = int(m.group(1))
            break
    if wdays:
        rep.add("E4/6 F6: pierwsza doba glodu warowni AI w zimie (prog etapu)", f"{starve if starve is not None else '-'} (zima w {len(wdays)} dobach okna)",
                "> 30 albo brak (zadna warownia AI nie glodzi przed 30. doba zimy)", starve is None or starve > 30)
    else:
        rep.add("E4/6 F6: pierwsza doba glodu warowni AI w zimie (prog etapu)", "brak zimy w biegu", "bieg zimowy 120 dob (proba zimy) - INFO tutaj", None)
    med = [(run_day(d, start), fnum(r"\): mediana " + F_ + r" dni, minimum", fam[d]), fnum(r" dni, minimum " + F_, fam[d])) for d in (wdays or on)]
    med = [x for x in med if x[1] is not None]
    if med:
        worst = min(med, key=lambda x: x[1])
        pts = ", ".join(f"d{n}: {m:.0f}" for n, m, _ in med if n in (30, 60, 120)) or f"ostatnia d{med[-1][0]}: {med[-1][1]:.0f}"
        rep.add("E4/6 F7: mediana zapasu warowni AI wedlug U27 (prog etapu)" + ("" if wdays else " - bieg bez zimy"),
                f"najnizsza {worst[1]:.0f} dni (d{worst[0]}); {pts}; minimum warowni najnizsze {min(x[2] for x in med if x[2] is not None):.0f}",
                ">= 364 kazdej doby zimy (U27: (Food x Q + spichlerze) / ubytek, bez ubytku = 364)", (worst[1] >= 364) if wdays else None)
    else:
        rep.add("E4/6 F7: mediana zapasu warowni AI wedlug U27", "brak odcinka zapasu w linii", ">= 364 w zimie", None)
    # F8 glodne okregi tylko spustoszone albo oblezone (doby bez zimy)
    nw = [d for d in on if "zima: NIE" in fam[d]]
    rx = re.compile(r"okregi glodne (\d+): oblezone (\d+), spustoszone (\d+), tylko zima (\d+), bez przyczyny (\d+)")
    rows = [rx.search(fam[d]) for d in nw]
    rows = [tuple(int(g) for g in m.groups()) for m in rows if m]
    if rows:
        none_ = sum(r[4] + r[3] for r in rows)
        rep.add("E4/6 F8: glodne okregi bez zimy - tylko spustoszone albo oblezone", f"okrego-dni glodnych {sum(r[0] for r in rows)} (oblezone {sum(r[1] for r in rows)}, spustoszone {sum(r[2] for r in rows)}), bez przyczyny {none_}",
                "bez przyczyny 0 (bieg zwykly 120 dob)", none_ == 0)
    else:
        rep.add("E4/6 F8: glodne okregi bez zimy", "brak dob bez zimy w oknie", "bez przyczyny 0", None)
    # F9 zgony z glodu w ksiedze (R3) = linia spichlerzy
    diff = []
    dead = 0.0
    for d in on:
        a = fnum(r"zmarli z glodu: gospodarze " + F_, fam[d])
        if a is None:
            continue
        dead += a
        b = fnum(r"glod: zmarli z glodu \(E4/6, R10 - ubytek trwaly\) " + F_, kb.get(d)) if d in kb else None
        if b is None:
            b = 0.0
        diff.append(abs(a - b))
    rep.add("E4/6 F9: zmarli z glodu (gospodarze) w ksiedze ludzi = linia spichlerzy", f"razem {dead:.1f}; max |roznica| {max(diff, default=0):.3f} w {len(diff)} dobach",
            "<= 0.1 kazdej doby (zaokraglenie ksiegi do 0.1; smierc przez PeopleBook.Move, MFamineDead)", (max(diff, default=0) <= 0.1) if diff else None)
    # poprawka po recenzji E4/6 (R1-5, R2-3): mieszczanie przy Food 0 - w ksiedze (MFamineTown, stan miasta) jak gospodarze
    diff_t = []
    dead_t = 0.0
    for d in on:
        a = fnum(r", mieszczanie " + F_ + r" \(miast", fam[d])
        if a is None:
            continue
        dead_t += a
        b = fnum(r"glod: zmarli mieszczanie przy Food 0 \(E4/6, R10 - stan miasta\) " + F_, kb.get(d)) if d in kb else None
        diff_t.append(abs(a - (b or 0.0)))
    rep.add("E4/6 F9b: zmarli z glodu mieszczanie w ksiedze ludzi = linia spichlerzy", f"razem {dead_t:.1f}; max |roznica| {max(diff_t, default=0):.3f} w {len(diff_t)} dobach",
            "<= 0.1 kazdej doby (TownDie -> licznik ksiegi MFamineTown)", (max(diff_t, default=0) <= 0.1) if diff_t else None)
    # F10 czas
    tm = [fnum(r"\| czas " + F_ + r" ms\.$", fam[d]) for d in on]
    tm = [x for x in tm if x is not None]
    rep.add("E4/6 F10: czas linii Spichlerze (E4/6)", f"{st.mean(tm):.1f} ms/d (max {max(tm):.1f})" if tm else "-", "<= 30 ms/d srednio (zasada kosztu paczki)", (st.mean(tm) <= 30.0) if tm else None)
    # F11 uchodzcy z glodu, przeprowadzki, miejsca pobytu (INFO)
    fl = sum(fnum(r"\| uciekli z glodu " + F_, fam[d]) or 0 for d in on)
    mv = sum(num(r"przeprowadzki kont (\d+)", fam[d], int, 0) or 0 for d in on)
    nr = sum(num(r"bez regionu z zapasem w zasiegu (\d+)", fam[d], int, 0) or 0 for d in on)
    last = fam[on[-1]]
    pl = re.search(r"miejsca pobytu poza swoim regionem \((\d+)\): (.*?) \|", last)
    rep.add("E4/6 F11: uchodzcy z glodu (INFO)", f"uciekli {fl:.0f}, przeprowadzki kont {mv}, bez regionu z zapasem {nr}; miejsca pobytu d{run_day(on[-1], start)}: {pl.group(2)[:200] if pl else '-'}",
            "INFO - miejsce pobytu w linii (GL 2.6)", None)
    # F12 zmarli z glodu wedlug grupy i warownie przy Food 0 (INFO)
    hz = sum(fnum(r"zmarli z glodu: gospodarze " + F_, fam[d]) or 0 for d in on)
    rz = sum(fnum(r", uchodzcy " + F_ + r", mieszczanie", fam[d]) or 0 for d in on)
    tz = sum(fnum(r", mieszczanie " + F_, fam[d]) or 0 for d in on)
    sz = sum(fnum(r"schronieni w murach " + F_, fam[d]) or 0 for d in on)
    fz = max((num(r"warownie przy Food 0: (\d+)", fam[d], int, 0) or 0 for d in on), default=0)
    rep.add("E4/6 F12: zmarli z glodu i warownie przy Food 0 (INFO)", f"gospodarze {hz:.0f}, uchodzcy {rz:.0f}, mieszczanie {tz:.0f}, schronieni {sz:.0f}; warowni przy Food 0 najwiecej {fz}",
            "INFO (R3 - ubytek trwaly; mieszczanie ze stanu miasta E4/4)", None)
    # F13 glod w przyroscie (H) i przednowek (INFO)
    hw = [num(r"wsi z brakiem jedzenia (\d+)", prz[d], int, 0) or 0 for d in on if d in prz]
    rn = [num(r"przednowek \(90%\): wsi (\d+)", fam[d], int, 0) or 0 for d in on]
    rep.add("E4/6 F13: glod w przyroscie (H) i przednowek (INFO)", f"wsi z brakiem jedzenia (H > 0) najwiecej {max(hw, default=0)}; przednowek najwiecej {max(rn, default=0)} wsi",
            "INFO - bieg zwykly: 0 poza spustoszonymi; zima: przednowek dopiero po wyczerpaniu spichlerzy (lata)", None)


def session_file_of(logp, name):
    """<katalog>/Armoury-<znacznik>.log -> <katalog>/<znacznik>/<name> (Log.Csv i pliki tematyczne: katalog sesji)."""
    d, f = os.path.split(logp)
    stem = os.path.splitext(f)[0].replace("Armoury-", "")
    return os.path.join(d, stem, name)


def etap4_e411(rep, lines, start, win, until, logp):
    """E4/11 (193): progi G1-G14 i G-ETAP z planu kodu E4/11 (rozdz. 9) - linia startowa "VillageBook (E4/11)", linie "Ksiega wiosek (E4/11)" (start sesji,
    pierwsza doba, doba), ognie widoku "Wioski: ogien nad ... (ksiega|lancuch gry)" z pliku tematycznego wioski.log, wiersze wioski-okregi.csv. G9 (bieg zimowy),
    G11 (zapis 9 dob), G12 (menu - reczna proba Jeffa), G13 (wylaczniki - autotest 40 dob) i G14 (grupy etapow) - INFO z opisem albo z danymi, gdy sa w logu."""
    kw = e4_series(lines, "Ksiega wiosek (E4/11): dzien")
    on = [d for d in win if d in kw and "| CZYNNA" in kw[d]]
    off = [d for d in win if d in kw and "| WYLACZONA" in kw[d]]
    # G1 bledy i potkniecia
    errs = [x for x in lines if x.startswith("ERROR in VillageBook") or x.startswith("ERROR in Devastation.Strike(E4/11)") or x.startswith("ERROR in Famine.TryFlee(E4/11)")
            or x.startswith("ERROR in PeopleMenu.Consequence(E4/11)") or x.startswith("ERROR in SyncData.arm_district")]
    stb = [num(r"\| potkniecia (\d+) \(razem", kw[d], int, 0) or 0 for d in on]
    rep.add("E4/11 G1: bledy i potkniecia VillageBook", f"bledy {len(errs)}, potkniecia {sum(stb)} w {sum(1 for x in stb if x)} dobach" + (f" (pierwszy: {errs[0][:120]})" if errs else ""),
            "0 bledow; potkniecia 0 (najwyzej 5 w biegu, z nazwa miejsca)", len(errs) == 0 and sum(stb) <= 5)
    # G2 linia startowa i pierwsza doba
    stl = next((x for x in lines if x.startswith("VillageBook (E4/11):")), "")
    brak = re.search(r"; BRAK: (.*?); wlasciciele", stl)
    own = re.search(r"wlasciciele latek celu: (.*?) \(U25\)", stl)
    rep.add("E4/11 G2: linia startowa (latka zywnosci warowni)", f"BRAK: {brak.group(1) if brak else '?'}; wlasciciele: {own.group(1)[:200] if own else '?'}",
            "BRAK: - (wpiete: zywnosc warowni od ludzi)", brak is not None and brak.group(1).strip() == "-")
    fd = next((x for x in lines if x.startswith("Ksiega wiosek (E4/11): uklad")), None)
    if fd is None:
        rep.add("E4/11 G2: pierwsza doba (uklad, okregi, n)", "brak linii pierwszej doby", "wiosek 2446 w 425 okregach, odrzucone 0, wsi gry brak 0, okregi 571, n dla wszystkich", False)
    else:
        m = re.search(r"wiosek (\d+) w (\d+) okregach \(odrzucone (\d+), wsi gry brak (\d+)", fd)
        ok_n = re.search(r"\| okregi (\d+):", fd)
        saved = num(r"z zapisu (\d+) okregow, nowe", fd, int, 0) or 0
        new = num(r", nowe (\d+)", fd, int, 0) or 0
        tot = int(ok_n.group(1)) if ok_n else 0
        shr = num(r"pomniejszonymi (\d+)", fd, int, 0)
        vals = tuple(int(g) for g in m.groups()) if m else None
        ok = vals is not None and vals[0] == 2446 and vals[1] == 425 and vals[2] == 0 and vals[3] == 0 and tot == 571 and saved + new == tot
        rep.add("E4/11 G2: pierwsza doba (uklad, okregi, n)", f"wiosek/okregi/odrzucone/brak wsi {vals}; okregi {tot}, n z zapisu {saved} + nowe {new}; pomniejszone {shr} (INFO)",
                "wiosek 2446 w 425 okregach, odrzucone 0, wsi gry brak 0, okregi 571, n dla wszystkich (TSV b742f307)", ok)
    # poprawka po recenzji (R1-3): bieg z ksiega wylaczona (G13) - wiersz CZYNNA jako INFO, G13 oceniany przed wyjsciem z bloku
    if off and not on:
        rep.add("E4/11: linia dzienna CZYNNA", f"czynna 0 dob, WYLACZONA {len(off)} dob z {len(win)} - bieg z ksiega wylaczona (patrz G13)", "czynna w kazdej dobie z ruchem (bieg zwykly)", None)
    else:
        rep.add("E4/11: linia dzienna CZYNNA", f"czynna {len(on)} dob, WYLACZONA {len(off)} dob z {len(win)}", "czynna w kazdej dobie z ruchem (bieg zwykly)", (len(off) == 0 and len(on) > 0) if win else None)
    e411_g13(rep, lines, kw, on, off, start, logp)
    if not on:
        return
    # G3 kontrola ksiegi
    r1 = [fnum(r"max \|r1\| " + F_, kw[d]) for d in on]
    r2 = [fnum(r"max \|r2\| " + F_, kw[d]) for d in on]
    r1 = [x for x in r1 if x is not None]; r2 = [x for x in r2 if x is not None]
    rep.add("E4/11 G3: kontrola ksiegi wiosek (w domu - DOM, poza domem - konto)", f"max |r1| {max(r1, default=0):.6f}, max |r2| {max(r2, default=0):.6f} w {len(r1)} dobach",
            "<= 0.01 czlowieka kazdej doby (z definicji)", (max(r1, default=0) <= 0.01 and max(r2, default=0) <= 0.01) if r1 else None)
    # G4 k1/k2/k3 i d0
    badk = []; d0 = 0; kp = 0; late = 0
    for d in on:
        x = kw[d]
        k = [num(r"k1 (\d+)", x, int, 0) or 0, num(r"k2 (\d+)", x, int, 0) or 0, num(r"k3 (\d+)", x, int, 0) or 0]
        zast = fnum(r"zastepczy \+" + F_, x) or 0.0
        d0 += num(r"doplyw bez zrodla d0 (\d+)", x, int, 0) or 0
        kp += num(r"\(okreg na pulapie\) kp (\d+)", x, int, 0) or 0          # poprawka po recenzji (R1-4): nazwany nadmiar - INFO
        late += num(r"konto znane po zwolnieniu pulapu (\d+)", x, int, 0) or 0
        if sum(k) > 0 and zast <= 0.0:
            badk.append((run_day(d, start), k))
    rep.add("E4/11 G4: k1 = k2 = k3 = 0, doplyw bez zrodla d0 = 0", f"doby z k > 0 (bez przydzialu zastepczego) {len(badk)}" + (f" {badk[:5]}" if badk else "") + f"; d0 razem {d0}; INFO: konto bez miejsca kp {kp} okrego-dni, umieszczone po zwolnieniu pulapu {late}",
            "0 kazdej doby (poza doba przydzialu zastepczego); d0 = 0", len(badk) == 0 and d0 == 0)
    # G5 osady x S - trafieni
    bad5 = []; over = 0.0; hits = 0.0
    for d in on:
        x = kw[d]
        r3 = fnum(r"trafieni dzis r3 " + SF, x)
        h = fnum(r"\(trafieni " + F_, x) or 0.0
        ov = fnum(r"nadmiar ludzi " + F_, x) or 0.0
        sk = fnum(r"bez ksiegi " + F_, x) or 0.0
        if r3 is None:
            continue
        hits += h; over += ov
        if abs(r3 + ov + sk) > max(0.01, 0.001 * h):
            bad5.append(run_day(d, start))
    rep.add("E4/11 G5: osady x S - trafieni Devastation (poza nadmiarem nazwanym)", f"doby poza tolerancja {len(bad5)}" + (f" (d{', d'.join(str(x) for x in bad5[:6])})" if bad5 else "")
            + f"; trafieni razem {hits:.0f}, nadmiar ponad pulap {over:.1f} ludzi (INFO)", "|r3 + nadmiar + bez ksiegi| <= max(0.01, 0.1% trafionych) kazdej doby", len(bad5) == 0)
    # G6 ogien z ksiegi (plik tematyczny wioski.log)
    wl = session_file_of(logp, "wioski.log")
    fires = []
    if os.path.exists(wl):
        fires = [x for x in read(wl) if x.startswith("Wioski: ogien nad")]
    fb = sum(1 for x in fires if x.endswith("(ksiega)."))
    fg = sum(1 for x in fires if x.endswith("(lancuch gry)."))
    if fb + fg:
        rep.add("E4/11 G6: ogien na wiosce z ksiegi", f"ognie {fb + fg}: (ksiega) {fb}, (lancuch gry) {fg} = {100.0 * fb / (fb + fg):.1f}%", ">= 95% ogni z ksiegi (bieg z widokiem; lancuch gry tylko przy ksiedze nieczynnej)",
                fb >= 0.95 * (fb + fg))
    else:
        rep.add("E4/11 G6: ogien na wiosce z ksiegi", "brak ogni z dopiskiem w wioski.log" + ("" if os.path.exists(wl) else f" (brak pliku {wl})"), "INFO bez ekranu mapy (autotest bez widoku)", None)
    # G7 stany wiosek (INFO)
    sp = sum(num(r"\| spalone dzis (\d+)", kw[d], int, 0) or 0 for d in on)
    ab = sum(num(r"\| opuszczone dzis (\d+)", kw[d], int, 0) or 0 for d in on)
    rb = sum(num(r"\| odbudowane dzis (\d+)", kw[d], int, 0) or 0 for d in on)
    last = kw[on[-1]]
    cz = num(r"czesciowo puste (\d+)\)", last, int, 0)
    sp_now = num(r", spalone (\d+), opuszczone", last, int, 0)
    rep.add("E4/11 G7: wioski spalone / opuszczone / odbudowane / czesciowo puste (INFO)", f"spalone {sp}, opuszczone {ab}, odbudowane {rb} w biegu; d{run_day(on[-1], start)}: spalone {sp_now}, czesciowo puste {cz}",
            "INFO (z31A: czesciowo pustych ok. 50-80, spalonych 0-20 w 120 dob)", None)
    # G8 zywnosc warowni
    # poprawka po recenzji (R1-1): dotad G8 porownywal korekte z liczba wsi z korekta > 0 (tautologia) - teraz wsie z korekta > 0 (g1) wobec wsi Looted pod korekta
    # (LootedOn i przelicznik ludzi; kazda ma ludzi w domu albo dno YieldFactor > 0, wiec powinna dac > 0); przy LootedOn false - INFO z nazwana przyczyna
    rx8 = re.compile(r"zywnosc warowni od ludzi: warowni (\d+), Food swiata " + SF + r"/d \(spladrowane \+" + F_ + r" w (\d+) wsiach z (\d+) pod korekta, Looted razem (\d+), uchodzcy i rece -"
                     + F_ + r" w (\d+)\)( \[wies spladrowana bez korekty: (.*?)\])?")
    r8 = [(d, rx8.search(kw[d])) for d in on]
    r8 = [(d, m) for d, m in r8 if m]
    if r8:
        f_all = [float(m.group(2)) for _, m in r8]
        offr = sorted({m.group(10) for _, m in r8 if m.group(10)})
        lexp = sum(int(m.group(5)) for _, m in r8 if not m.group(10))
        lall = sum(int(m.group(6)) for _, m in r8)
        bad8 = [run_day(d, start) for d, m in r8 if not m.group(10) and int(m.group(4)) < int(m.group(5))]
        txt = (f"Food swiata srednio {st.mean(f_all):+.2f}/d (min {min(f_all):+.2f}, max {max(f_all):+.2f}); wsio-dni Looted {lall}, pod korekta {lexp}; doby z wsia pod korekta bez korekty > 0: {len(bad8)}"
               + (f" (d{', d'.join(str(x) for x in bad8[:6])})" if bad8 else "") + (f"; korekta Looted wylaczona: {', '.join(offr)}" if offr else ""))
        ok8 = None if (offr and lexp == 0) or lall == 0 else len(bad8) == 0
        rep.add("E4/11 G8: zywnosc warowni od ludzi", txt, "kazda wies Looted pod korekta (LootedOn, przelicznik ludzi) daje warowni > 0 (INFO: brak wsi Looted albo LootedOn false z przyczyna)", ok8)
    else:
        rep.add("E4/11 G8: zywnosc warowni od ludzi", "odcinek WYLACZONA albo brak" if any("zywnosc warowni od ludzi: WYLACZONA" in kw[d] for d in on) else "brak odcinka", "korekta = suma (nasze - gra)", None)
    rep.add("E4/11 G9: bieg zimowy - progi E4/6 F6/F7", "patrz wiersze E4/6 F6/F7 tego biegu", "nie gorzej niz bieg E4/6 bez E4/11", None)
    # G10 czas
    tm = [fnum(r"\| czas " + F_ + r" ms\.$", kw[d]) for d in on]
    tm = [x for x in tm if x is not None]
    rs = [x for x in lines if x.startswith("Ksiega wiosek (E4/11): start sesji")]
    pl = [fnum(r"czas: plik " + F_ + r" ms", x) for x in rs]
    pl = [x for x in pl if x is not None]
    rep.add("E4/11 G10: czas linii i wczytania pliku", f"linia {st.mean(tm):.2f} ms/d (max {max(tm):.1f}); plik ukladu po stronie kampanii {max(pl, default=0):.1f} ms" if tm else "-",
            "<= 3 ms/d srednio; wczytanie pliku <= 50 ms", (st.mean(tm) <= 3.0 and max(pl, default=0) <= 50.0) if tm else None)
    # G11 zapis (wczytanie z kluczem)
    ld = [x for x in rs if "z zapisu: n " in x]
    if ld:
        okz = all("uklad zgodny" in x for x in ld)
        rep.add("E4/11 G11: wczytanie zapisu (arm_district)", "; ".join(re.search(r"z zapisu: (.*?);", x).group(1)[:120] for x in ld if re.search(r"z zapisu: (.*?);", x)),
                "n 571 okregow, puste osady M, uklad zgodny; G3/G4 od pierwszej doby po wczytaniu", okz)
    else:
        rep.add("E4/11 G11: wczytanie zapisu (arm_district)", "brak wczytania z kluczem w tym logu", "autotest zapisu 9 dob", None)
    rep.add("E4/11 G12: menu okregu (People of this land, takze village_looted)", "reczna proba Jeffa", "liczby = linia/CSV tego okregu", None)
    rep.add("E4/11 G14: progi etapow 1-3 i E4/5 / E4/6", "patrz wiersze E4/5 S, E4/6 F oraz --grupa etap2 / etap3", "bez zmian (E4/11 nie rusza ludzi ani hearth)", None)
    # G-ETAP: spalona wioska wraca rok po ostatnim najezdzie (wioski-okregi.csv, rok = 1, najazd w d1-d364)
    cp = session_file_of(logp, "wioski-okregi.csv")
    rows = []
    if os.path.exists(cp):
        try:
            with open(cp, encoding="utf-8", errors="replace") as fh:
                rows = [r for r in csv.DictReader(fh, delimiter=";") if r.get("rok") == "1"]
        except Exception as ex:
            rows = []
            rep.add("E4/11 G-ETAP: odczyt wioski-okregi.csv", f"blad: {ex}", "plik CSV czytelny", False)
    sel = []
    for r in rows:
        try:
            lastd = int(r["ostatni_najazd"])
        except (KeyError, ValueError):
            continue
        if 1 <= run_day(lastd - 1, start) <= 364:
            sel.append(r)
    if not sel:
        rep.add("E4/11 G-ETAP: spalona wioska (takze poboczna) wraca po roku", f"wierszy z rocznica (najazd d1-d364): 0 (plik {'jest' if os.path.exists(cp) else 'brak'})",
                "bieg 728 dob: 100% wierszy TAK poza nazwanymi (INFO w krotszym biegu)", None)
        return
    named_siege = [r for r in sel if r["wynik_rok"] in ("NIE-oblezenie", "NIE-pobyt-oblezony")]
    # poprawka po recenzji (R1-2): czesc konta z glodu nie wstrzymuje powrotu czesci z rabunku - "NIE-glod" (logi sprzed poprawki) liczone jak NIE
    named_info = [r for r in sel if r["wynik_rok"] == "NIE-brama-E46"]
    tak = [r for r in sel if r["wynik_rok"] == "TAK"]
    bad = [r for r in sel if r["wynik_rok"] in ("NIE", "NIE-glod")]
    badtak = []
    for r in tak:
        try:
            n = float(r["n"]); pr = float(r["puste_r"]); kr = float(r["konto_r"]); sp_ = int(r["spalone"]); od = int(r["odbudowa"])
            fam = float(r["konto_f"]) > 0.0 or float(r["puste_f"]) > 0.0
        except (KeyError, ValueError):
            continue
        if kr >= 1.0 or pr > 0.01 * n or sp_ > 0 or (od > 0 and not fam):
            badtak.append(r["okreg"])
    rated = len(sel) - len(named_siege) - len(named_info)
    rep.add("E4/11 G-ETAP: spalona wioska (takze poboczna) wraca po roku", f"wierszy {len(sel)}: TAK {len(tak)}, NIE bez przyczyny {len(bad)}, nazwane: oblezenie/pobyt {len(named_siege)} (pominiete), brama E4/6 {len(named_info)} (INFO)"
            + (f"; TAK z resztkami: {', '.join(badtak[:5])}" if badtak else "") + (f"; NIE: {', '.join(r['okreg'] for r in bad[:5])}" if bad else ""),
            "100% wierszy ocenianych TAK (konto_r < 1, puste_r = 0, spalone = 0, odbudowa = 0)", rated > 0 and len(bad) == 0 and len(badtak) == 0 and len(tak) == rated)
    burnt = [x for d in on for x in re.findall(r"\| spalone dzis \d+: (.*?) \|", kw[d])]
    rep.add("E4/11 G-ETAP: wioski spalone w biegu (INFO)", "; ".join(burnt)[:400] if burnt else "brak list spalonych w linii", "INFO - lista z doba spalenia i odbudowy (linia dzienna)", None)


def e411_g13(rep, lines, kw, on, off, start, logp):
    """E4/11 G13 (poprawka po recenzji R1-3): wylaczniki. VillageBookEnabled=false - linia WYLACZONA jest, puste osady wyzerowane tylko w pierwszej linii sesji
    (dalsze linie z liczba okregow > 0 = ksiega pisala przy wylaczonym kluczu), ogien widoku bez "(ksiega)". TownFoodByVillagePeople=false - odcinek
    "zywnosc warowni od ludzi: WYLACZONA (Town Food By Village People)" w kazdej dobie CZYNNA (korekta 0 z definicji - postfiks wychodzi przy kluczu)."""
    if off:
        offl = [kw[d] for d in off]
        sess = sum(1 for x in lines if x.startswith("Ksiega wiosek (E4/11): start sesji"))
        nz = [num(r"puste osady wyzerowane w (\d+) okregach", x, int, 0) or 0 for x in offl]
        late = sum(1 for i, x in enumerate(nz) if x > 0 and i > 0)
        fires = []
        wl = session_file_of(logp, "wioski.log")
        if os.path.exists(wl) and not on:
            fires = [x for x in read(wl) if x.startswith("Wioski: ogien nad") and x.endswith("(ksiega).")]
        ok = late <= max(0, sess - 1) and not fires
        rep.add("E4/11 G13: wylacznik ksiegi (Village Book Enabled = false)", f"linie WYLACZONA {len(offl)} (sesji {sess}); linie z zerowaniem > 0 po pierwszej: {late}; ognie '(ksiega)' przy wylaczonej: {len(fires)}",
                "pierwsza linia zeruje puste osady, dalej 0 (najwyzej jedna linia z zerowaniem na wczytanie); ogien tylko '(lancuch gry)'", ok)
    else:
        rep.add("E4/11 G13: wylacznik ksiegi (Village Book Enabled = false)", "brak dob WYLACZONA (bieg zwykly)", "autotest 40 dob z kluczem false", None)
    if on:
        tf = [d for d in on if "zywnosc warowni od ludzi: WYLACZONA (Town Food By Village People)" in kw[d]]
        if tf:
            rep.add("E4/11 G13: wylacznik zywnosci warowni (Town Food By Village People = false)", f"doby z odcinkiem WYLACZONA {len(tf)} z {len(on)} dob CZYNNA",
                    "w kazdej dobie biegu z kluczem false (korekta 0)", True if len(tf) == len(on) else None)
        else:
            rep.add("E4/11 G13: wylacznik zywnosci warowni (Town Food By Village People = false)", "klucz wlaczony w calym biegu", "autotest z kluczem false", None)

# E4/9 (192): linia startowa; baza Strazy d120 - bieg A zbioru 31 (z31A, przed E4/9: zapas Strazy 8 691 - plan kodu E4/9 rozdz. 11 K7); kultury z niewola (LosersFlee)
E49_START = "Krainy kanonu (E4/9): wpiete"
E49_WATCH_BASE = 8691
E49_SLAVE = {"ghiscari", "volantine", "lyseni", "myrish", "tyroshi", "qohorik", "norvos", "valyrian", "khuzait", "sturgia", "qartheen", "?"}
# udzial krain w ludziach Westeros (tabela PopulationLaw, mln): K4 - 3 najwieksze krainy rozkladu "z ludnosci" w +-2 pkt
E49_WESTEROS = {"reach": 8.0, "vlandia": 4.0, "river": 3.5, "battania": 3.0, "vale": 3.0, "stormlands": 2.5, "aserai": 1.5, "dragonstone": 0.85, "crownlands": 0.6, "sturgia": 0.5}


def etap4_e49(rep, lines, start, win, until):
    """E4/9 (192): progi K1-K25 z planu kodu docs/etap4/PLAN-KODU-E49.md rozdz. 11 (stan z linii dziennych, nie startowej). Linie: "Straz z Westeros (E4/9): dzien",
    "Niewolni (E4/9): dzien", "Ludzie (spustoszenie)" (odcinek "pojmani (E4/9)"), "Jency (E4/0)" (odcinek "pojmani z wsi (E4/9)"), "Dlugi (168)" (odcinek "DLUG 4 (E4/9)"),
    "Wolne rece (E4/7)" (prog Zelaznych), "Pula wyrzutkow (E4/3)" ("do Strazy"), "Straz i Wolni Ludzie (4.15)", "Ludzie (ksiega)" (rownanie krain), kalibracja PopulationLaw."""
    stl = next((x for x in lines if x.startswith(E49_START)), "")
    wl = e4_series(lines, "Straz z Westeros (E4/9): dzien")
    nw = e4_series(lines, "Niewolni (E4/9): dzien")
    dev = e4_series(lines, "Ludzie (spustoszenie): dzien")
    jen = e4_series(lines, "Jency (E4/0): dzien")
    dl = e4_series(lines, "Dlugi (168): dzien")
    fh = e4_series(lines, "Wolne rece (E4/7): dzien")
    swl = e4_series(lines, "Straz i Wolni Ludzie (4.15): dzien")
    kb = e4_series(lines, "Ludzie (ksiega): dzien")
    wdays = [d for d in sorted(wl) if 1 <= run_day(d, start) <= until]
    ndays = [d for d in sorted(nw) if 1 <= run_day(d, start) <= until]
    # K1 bledy, linia startowa, potkniecia
    brak = re.search(r"; BRAK: (.*?); wlasciciele", stl)
    rep.add("E4/9 K1: linia startowa (latki ExecuteCapture i AiHourlyTick)", stl[:300], "\"BRAK: -\"", (brak is not None and brak.group(1).strip() == "-") if stl else False)
    errs = [x for x in lines if x.startswith("ERROR in WatchLaw") or x.startswith("ERROR in ThrallLaw") or ("(E4/9" in x and x.startswith("ERROR in "))
            or x.startswith("ERROR in PeopleBook.E4/7.WallTake") or x.startswith("ERROR in PeopleBook.E4/7.ThrallsDaily") or x.startswith("ERROR in PeopleBook.E4/7.SeedThralls")
            or x.startswith("ERROR in PeopleBook.E4/7.CanonEndDay") or x.startswith("ERROR in PeopleBook.E4/7.ImportCanonPart") or x.startswith("ERROR in PeopleBook.RefugeesToCaptives")
            or x.startswith("ERROR in Devastation.TakeCaptives")]
    stw = [num(r"\| potkniecia (\d+)", wl[d], int, 0) or 0 for d in wdays]
    stn = [num(r"\| potkniecia (\d+)\.", nw[d], int, 0) or 0 for d in ndays]
    rep.add("E4/9 K1: bledy i potkniecia E4/9", f"bledy {len(errs)}, potkniecia Straz {sum(stw)}, Niewolni {sum(stn)}" + (f" (pierwszy: {errs[0][:140]})" if errs else ""),
            "0 bledow; potkniecia <= 5 z nazwa miejsca", len(errs) == 0 and sum(stw) + sum(stn) <= 5)
    if not wdays:
        rep.add("E4/9: linie dzienne \"Straz z Westeros (E4/9)\"", "brak", "kazdej doby (LogEnabled)", False)
        return
    live = [d for d in wdays if "| prawo CZYNNE" in wl[d]]
    rep.add("E4/9: doby z czynnym prawem Strazy (WatchLaw + ksiega pochodzenia)", f"{len(live)} z {len(wdays)}", "INFO (przed siewem E4/7 - nieczynne)", None)
    rx = re.compile(r"przybylo " + F_ + r" = z Muru " + F_ + r" \+ z pul wyrzutkow " + F_ + r" \(zolnierze " + F_ + r", prosci " + F_ + r"; budzet (\d+)%: " + F_
                    + r" w (\d+) regionach, wykorzystano " + F_ + r"%\) \+ z ludnosci " + F_ + r" \(.*?\) \+ bez pokrycia " + F_)
    rows = []
    for d in live:
        m = rx.search(wl[d])
        if m:
            v = [float(x) for x in m.groups()]
            rows.append((d, v))
    # K2 przybylo = Mur + pule + ludnosc + bez pokrycia; bez pokrycia <= 1% (28 dob)
    bad2 = [(run_day(d, start), v[0], v[1] + v[2] + v[9] + v[10]) for d, v in rows if abs(v[0] - (v[1] + v[2] + v[9] + v[10])) > 0.3]
    last = rows[-28:]
    nsum, zsum = sum(v[0] for _, v in last), sum(v[10] for _, v in last)
    rep.add("E4/9 K2: przybylo = Mur + pule + ludnosc + bez pokrycia (kazdej doby)", f"{len(rows) - len(bad2)} z {len(rows)} dob" + (f"; roznice {bad2[:3]}" if bad2 else ""),
            "kazdej doby (+-0.3 - zaokraglenie linii)", (len(bad2) == 0) if rows else None)
    rep.add("E4/9 K2: bez pokrycia / przybylo (ostatnie 28 dob)", f"{zsum:.1f} / {nsum:.1f} = " + (f"{100 * zsum / nsum:.2f}%" if nsum > 0 else "-"), "<= 1% (podloga 50 hearth w calym Westeros)",
            (zsum <= 0.01 * nsum) if nsum > 0 else None)
    # K3 pule: wykorzystano <= 100%; z pul > 0 w d2-d120
    over = [(run_day(d, start), v[8]) for d, v in rows if v[8] > 100.05]
    pool2 = [v[2] for d, v in rows if run_day(d, start) >= 2]
    rep.add("E4/9 K3: budzet pul - wykorzystano <= 100% kazdej doby", f"przekroczen {len(over)}" + (f" ({over[:3]})" if over else "") + f"; budzet srednio {st.mean([v[6] for _, v in rows]):.1f}/d w {st.mean([v[7] for _, v in rows]):.0f} regionach" if rows else "-",
            "<= 100%", (len(over) == 0) if rows else None)
    rep.add("E4/9 K3: z pul wyrzutkow w d2-d" + str(until), f"razem {sum(pool2):.0f} ({sum(1 for x in pool2 if x > 0)} z {len(pool2)} dob > 0; zolnierze {sum(v[3] for _, v in rows):.0f}, prosci {sum(v[4] for _, v in rows):.0f})",
            "> 0 (RK 1.2: X = 3%)", (sum(pool2) > 0) if pool2 else None)
    # K4 rozklad "z ludnosci" od wczytania (ostatnia linia) - 3 najwieksze krainy w +-2 pkt od udzialu w ludziach Westeros
    ml = re.search(r"\(rozklad: ([^)]*)\)", wl[live[-1]]) if live else None
    tot = sum(E49_WESTEROS.values())
    if ml and ml.group(1).strip() != "-":
        got = {k: float(v) for k, v in re.findall(r"([a-z_]+) ([\d.]+)%", ml.group(1))}
        top3 = sorted(E49_WESTEROS, key=lambda k: -E49_WESTEROS[k])[:3]
        diffs = [(k, got.get(k, 0.0), 100.0 * E49_WESTEROS[k] / tot) for k in top3]
        rep.add("E4/9 K4: rozklad z ludnosci od wczytania - 3 najwieksze krainy (log / tabela)", ", ".join(f"{k} {a:.1f}/{b:.1f}%" for k, a, b in diffs), "+-2 pkt (RK:215)",
                all(abs(a - b) <= 2.0 for _, a, b in diffs))
    else:
        rep.add("E4/9 K4: rozklad z ludnosci", "brak poboru z ludnosci (Mur i pule pokryly)" if live else "-", "+-2 pkt - INFO bez poboru", None)
    # K5 zwolnieni 166 do domu = 0 i do domu z hearth = 0
    rel = [num(r"zwolnieni 166 do domu (\d+)", wl[d], int, 0) or 0 for d in live]
    hh = [fnum(r"do domu z hearth " + F_, wl[d]) or 0 for d in live]
    rep.add("E4/9 K5: bracia - zwolnieni 166 do domu / do domu z hearth (suma)", f"{sum(rel)} / {sum(hh):.1f}", "0 / 0 kazdej doby (tylko na Mur - U5)", sum(rel) == 0 and sum(hh) == 0)
    # K6 rabunki Strazy = 0 od d2; bezpiecznik 0
    rr = [(run_day(d, start), num(r"rabunki Strazy dzis (\d+)", wl[d], int, 0) or 0) for d in wdays]
    fz = sum(num(r"bezpiecznik (\d+)", wl[d], int, 0) or 0 for d in wdays)
    rj = sum(num(r"odrzucone opcje rabunku (\d+)", wl[d], int, 0) or 0 for d in wdays)
    late = [x for x in rr if x[0] >= 2 and x[1] > 0]
    rep.add("E4/9 K6: H1 - rabunki Strazy od d2 / bezpiecznik", f"{sum(x[1] for x in late)} / {fz} (odrzucone opcje rabunku razem {rj})" + (f"; doby {late[:4]}" if late else ""),
            "0 / 0 (RK 0.1.2: 10 rabunkow w 28 dobach przed H1)", len(late) == 0 and fz == 0)
    # K7 Straz w d120 >= 85% biegu bazy
    sd = [d for d in sorted(swl) if 1 <= run_day(d, start) <= until]
    ws = num(r"Nocna Straz: stan (\d+)", swl[sd[-1]]) if sd else None
    if ws is not None and run_day(sd[-1], start) >= 120:
        rep.add(f"E4/9 K7: Straz w d{run_day(sd[-1], start)}", f"{ws} wobec bazy z31A {E49_WATCH_BASE}", f">= {int(0.85 * E49_WATCH_BASE)} (85%)", ws >= 0.85 * E49_WATCH_BASE)
    else:
        rep.add("E4/9 K7: Straz w d120", f"{ws} (d{run_day(sd[-1], start) if sd else '-'})" if ws is not None else "brak linii", "INFO - wymaga biegu 120 dob", None)
    # K8 kontrola linii |.| <= 1 kazdej doby; w nicosc (q + znikniecie) <= 10% obrotu (28 dob)
    kc = [(run_day(d, start), fnum(r"\) = " + SF + r" \| potkniecia", wl[d])) for d in live]
    kc = [x for x in kc if x[1] is not None]
    badk = [x for x in kc if abs(x[1]) > 1.0]
    rep.add("E4/9 K8: kontrola linii Straz z Westeros", f"max |kontrola| {max((abs(x[1]) for x in kc), default=0):.2f} w {len(kc)} dobach" + (f"; ponad 1: {badk[:4]}" if badk else ""),
            "|.| <= 1 czlowiek kazdej doby", (len(badk) == 0) if kc else None)
    tq = []
    for d in live[-28:]:
        m = re.search(r"\(wejscia " + F_ + r" - wyjscia " + F_ + r" - w nicosc " + F_ + r" - przy zniknieciu partii " + F_ + r"\)", wl[d])
        if m:
            a, b, q, g = (float(x) for x in m.groups())
            tq.append((q + g, a + b))
    if tq:
        q_, t_ = sum(x[0] for x in tq), sum(x[1] for x in tq)
        rep.add("E4/9 K8: w nicosc (niedobor slownika + resztka przy zniknieciu) / obrot (28 dob)", f"{q_:.1f} / {t_:.1f} = " + (f"{100 * q_ / t_:.1f}%" if t_ > 0 else "-"),
                "<= 10% obrotu (dzis T6: 60-67% ruchu bez nazwy)", (q_ <= 0.10 * t_) if t_ > 0 else None)
    # K9 dezercja i do lasu: 100% do regionow Polnocy
    wn = sum(fnum(r"do lasu: pula Polnocy " + F_, wl[d]) or 0 for d in live)
    wo = sum(fnum(r"inne regiony " + F_ + r" \| zwolnieni", wl[d]) or 0 for d in live)
    rep.add("E4/9 K9: dezerterzy i rozbici Strazy do lasu - pula Polnocy / inne", f"{wn:.1f} / {wo:.1f}", "100% Polnoc (inne 0)", (wo <= 0.05) if wn + wo > 0 else None)
    # K10 pula i bandy - INFO; "do Strazy" z linii puli
    pl = e4_series(lines, "Pula wyrzutkow (E4/3): dzien", 1)     # dzien gry (jak "Wyrzutki") - shift 1
    tw = [fnum(r"do Strazy \(E4/9\) " + F_, pl[d]) or 0 for d in sorted(pl) if 1 <= run_day(d, start) <= until]
    wy = e4_series(lines, "Wyrzutki: dzien", 1)
    wyd = [d for d in sorted(wy) if 1 <= run_day(d, start) <= until]
    band = [(num(r"\| band (\d+), ludzi (\d+)", wy[d]), num(r"\| band \d+, ludzi (\d+)", wy[d])) for d in wyd]
    rep.add("E4/9 K10: pula wyrzutkow - do Strazy (srednio/d) i bandy (d1 -> ostatnia: band, ludzi)", f"{st.mean(tw):.1f}/d" if tw else "-" + "",
            "INFO wobec bazy (RK 1.2: 3% - bandy ok. -17..-19%); prog band E4/3 (plateau w wojnie <= 1.5 x pokoj) - wiersze E4/3", None)
    if band:
        rep.add("E4/9 K10: bandy d1 -> ostatnia", f"{band[0][0]}/{band[0][1]} -> {band[-1][0]}/{band[-1][1]}", "INFO", None)
    # K11 nowa kampania d1: niewolni Zelaznych = 27 000 x PopulationScale (+-1); kalibracja "sturgia ...M (...) + niewolni N"
    op = next((x for x in lines if x.startswith("Niewolni (E4/9): bilans otwarcia nowej kampanii")), None)
    cal = next((x for x in lines if x.startswith("PopulationLaw: kalibracja ludnosci")), "")
    mc = re.search(r"sturgia ([\d.]+)M \([^)]*\) \+ niewolni (\d+)", cal)
    if op:
        mo = re.search(r"Zelazne Wyspy (\d+) \(czesc tabeli (\d+)", op)
        n, tab = (int(mo.group(1)), int(mo.group(2))) if mo else (None, None)
        exp = 27000.0 * tab / 500000.0 if tab else None
        rep.add("E4/9 K11: bilans otwarcia niewolnych Zelaznych (NK)", f"{n} (oczekiwane {exp:.0f}); kalibracja: " + (f"sturgia {mc.group(1)}M + niewolni {mc.group(2)}" if mc else "brak dopisku"),
                "27 000 x PopulationScale +-1 i dopisek w kalibracji", (n is not None and exp is not None and abs(n - exp) <= 1 and mc is not None))
    else:
        rep.add("E4/9 K11: bilans otwarcia niewolnych Zelaznych", "brak (stary zapis albo bieg nie od nowej kampanii)", "INFO - tylko nowa kampania", None)
    # K12 kontrola "Niewolni" |.| <= 0.01; K13 smierc 5%/rok; K14 tylko krainy z niewola
    nck = [(run_day(d, start), fnum(r"kontrola: T - Twczoraj - \(s \+ b \+ bilans - d\) = " + SF, nw[d])) for d in ndays]
    nck = [x for x in nck if x[1] is not None]
    badn = [x for x in nck if abs(x[1]) > 0.01]
    rep.add("E4/9 K12: kontrola linii Niewolni", f"max |kontrola| {max((abs(x[1]) for x in nck), default=0):.3f} w {len(nck)} dobach" + (f"; {badn[:4]}" if badn else ""),
            "|.| <= 0.01 kazdej doby", (len(badn) == 0) if nck else None)
    yr = []
    for d in ndays:
        t = num(r"\| stan (\d+) \(bilans", nw[d]); dd = fnum(r"smierc -" + F_, nw[d]); pc = fnum(r"smierc -[\d.]+ \(" + F_ + r"%/rok", nw[d])
        if t is not None and dd is not None and pc is not None and t + dd >= 1000 and dd > 0:
            yr.append((100.0 * dd / (t + dd) * 364.0, pc))
    if yr:
        m_ = st.mean(x[0] for x in yr)
        rep.add("E4/9 K13: smierc niewolnych (d / T x 364)", f"{m_:.2f}%/rok ({len(yr)} dob; ustawienie {yr[-1][1]:.0f}%)", "ustawienie +-0.1 pkt (5%)", abs(m_ - yr[-1][1]) <= 0.1)
    else:
        rep.add("E4/9 K13: smierc niewolnych", "za malo niewolnych w biegu (T < 1000)", "INFO", None)
    held = {}
    for d in ndays:
        mh = re.search(r"\) w krainach: (.*?) \| dzis", nw[d])
        if mh and mh.group(1).strip() != "-":
            for k, v in re.findall(r"([a-z_?]+) (\d+)", re.sub(r"\([^)]*\)", "", mh.group(1))):
                held[k] = max(held.get(k, 0), int(v))
    outside = [k for k, v in held.items() if v > 0 and k not in E49_SLAVE]
    i1 = any(x.startswith("Prawo jenca (I1)") for x in lines)
    po = sum(fnum(r"poza kanonem \(prawo wylaczone albo osada bez niewoli\) " + F_, nw[d]) or 0 for d in ndays)
    rep.add("E4/9 K14: niewolni tylko w krainach kanonu / poza kanonem (suma)", f"krainy trzymania {sorted(held)}; spoza listy {outside}; poza kanonem {po:.1f}",
            "spoza listy 0; poza kanonem 0 przy I1" + ("" if i1 else " (I1 nieaktywne - INFO)"), (len(outside) == 0 and (po <= 0.5 or not i1)) if ndays else None)
    # K15 prog wolnych rak Zelaznych w d1 NK 39-40% (zrodlo ksiega)
    fd = [d for d in sorted(fh) if 1 <= run_day(d, start) <= until]
    if op and fd:
        mf = re.search(r"Zelazne Wyspy ([\d.]+)% \(niewolni ksiegi ([\d-]+) / BK [^-]*- zrodlo: ([a-z]+)", fh[fd[0]])
        ok = mf is not None and 39.0 <= float(mf.group(1)) <= 40.05 and mf.group(3) == "ksiega"
        rep.add(f"E4/9 K15: prog wolnych rak Zelaznych d{run_day(fd[0], start)} (NK)", f"{mf.group(1)}% (niewolni ksiegi {mf.group(2)}, zrodlo {mf.group(3)})" if mf else "brak odczytu",
                "39-40% ze zrodla ksiega (z31A: 30.5% z BK)", ok)
    else:
        rep.add("E4/9 K15: prog wolnych rak Zelaznych", "brak NK albo linii Wolne rece", "INFO - tylko nowa kampania", None)
    # K16 pojmani w linii spustoszenia = pojmani w linii Jency; z niczego (BK) = 0 przy czynnym prawie
    k16, bk0 = [], []
    for d in sorted(dev):
        if not (1 <= run_day(d, start) <= until):
            continue
        md = re.search(r"pojmani \(E4/9\): rabunkow wygranych z pojmanymi (\d+), z uchodzcow wsi do lochu najezdzcy \+(\d+)", dev[d])
        if not md:
            continue
        mj = re.search(r"pojmani z wsi \(E4/9\) \+(\d+) w (\d+) rabunkach", jen.get(d, ""))
        if mj:
            k16.append((run_day(d, start), int(md.group(2)), int(mj.group(1))))
        b = num(r"z rabunku BK \+(\d+)", jen.get(d, ""))
        if b is not None:
            bk0.append((run_day(d, start), b))
    badj = [x for x in k16 if x[1] != x[2]]
    rep.add("E4/9 K16: pojmani - linia spustoszenia = linia Jency (kazdej doby)", f"{len(k16) - len(badj)} z {len(k16)} dob; razem {sum(x[1] for x in k16)}" + (f"; roznice {badj[:3]}" if badj else ""),
            "kontrola 0 kazdej doby", (len(badj) == 0) if k16 else None)
    rep.add("E4/9 K16: jency z rabunku BK z niczego przy czynnym prawie (suma)", f"{sum(x[1] for x in bk0)} w {len(bk0)} dobach", "0 kazdej doby (z31A: BK 18.5/d)", (sum(x[1] for x in bk0) == 0) if bk0 else None)
    # K17 pojmani tylko u najezdzcow z niewola; J <= plan + 0.5 na wolanie
    ns, jp = 0, []
    for d in sorted(dev):
        if not (1 <= run_day(d, start) <= until):
            continue
        m = re.search(r"z uchodzcow wsi do lochu najezdzcy \+(\d+) \(wyliczone \d+% trafionych przy niewoli " + F_ + r";.*?nie z niewola (\d+), gracz \d+, wolan BK (\d+)\)", dev[d])
        if m:
            ns += int(m.group(3))
            jp.append((run_day(d, start), int(m.group(1)), float(m.group(2)), int(m.group(4))))
    badp = [x for x in jp if x[1] > x[2] + 0.5 * max(1, x[3])]
    rep.add("E4/9 K17: pojmani tylko z niewola / J <= plan + 0.5 na rabunek", f"nie z niewola {ns}; ponad plan {len(badp)} dob" + (f" {badp[:3]}" if badp else ""), "0 / 0", (ns == 0 and len(badp) == 0) if jp else None)
    # K18 E4/5 S3 i kontrola konta UCHODZCY z czlonem pojmanych - wiersze E4/5 S3/S4 (kod liczy kontrole z pojmanymi)
    withp = sum(1 for d in dev if "- pojmani " in dev[d] and "konto UCHODZCY: wczoraj" in dev[d])
    rep.add("E4/9 K18: konto UCHODZCY z czlonem pojmanych", f"doby z czlonem w linii spustoszenia {withp}", "kontrola |.| <= 0.01 - wiersz E4/5 S4; pojmani przy uderzeniu 0 - E4/5 S3", None)
    # K19 rownanie krain z T i W
    rk = [fnum(r"rownanie krain: najgorsza kraina \S+ reszta \S+ \(" + SF + r"% ludzi krainy\)", kb[d]) for d in sorted(kb) if 1 <= run_day(d, start) <= until]
    rk = [x for x in rk if x is not None][-28:]
    seg = sum(1 for d in kb if "| E4/9: NIEWOLNI" in kb[d])
    rep.add("E4/9 K19: rownanie krain z T i W (najgorsza kraina, 28 dob)", f"max |reszta| {max((abs(x) for x in rk), default=0):.3f}% ludzi krainy; odcinek E4/9 w {seg} dobach",
            "<= 0.1% (przy RESZTA hearth != 0 - INFO, wiersze E4/7)", (max((abs(x) for x in rk), default=0) <= 0.1) if rk else None)
    # K20 DLUG 4
    rx4 = re.compile(r"DLUG 4 \(E4/9\): rodow w zajeciu (\d+), z odchodzacymi do domu (\d+), odeszli do domu (\d+) \(limit zoldu (\d+), zalegly zold (\d+); bez wsi z danymi BK - do lasu (\d+)\)")
    sz, home, woods = 0, 0, 0
    for d in sorted(dl):
        if not (1 <= run_day(d, start) <= until):
            continue
        m = rx4.search(dl[d])
        if m:
            sz += int(m.group(1)); home += int(m.group(3)); woods += int(m.group(6))
    if sz > 0 and home + woods > 0:
        rep.add("E4/9 K20: DLUG 4 - do lasu / odchodzacy (rody w zajeciu)", f"{woods} / {home + woods} (rodo-dni zajec {sz})", "do lasu <= 5%, do domu > 0", woods <= 0.05 * (home + woods) and home > 0)
    else:
        rep.add("E4/9 K20: DLUG 4", f"rodo-dni zajec {sz}, odchodzacych {home + woods}", "INFO - brak zajec albo odchodzacych w biegu (b728: zajecia w 554 dobach)", None)
    # K21 rownanie jencow - wiersz T7 (parser z czlonem pojmanych)
    rep.add("E4/9 K21: rownanie jencow E4/0 z czlonem pojmanych", "wiersz T7: rownanie jencow (jin + pojmani z wsi E4/9)", "jak dotad (<= 10% obrotu)", None)
    # K22 koszt
    tm = [fnum(r"\| czas " + F_ + r" ms \(Levy", wl[d]) for d in wdays]
    tm = [x for x in tm if x is not None]
    rep.add("E4/9 K22: czas Levy i linii Strazy (srednio)", f"{st.mean(tm):.2f} ms/d (max {max(tm):.1f})" if tm else "-", "<= 3 ms/dobe; s/dobe <= +2% wobec biegu bez E4/9 (autotest)", (st.mean(tm) <= 3.0) if tm else None)
    # K23 zapis - linia "z zapisu"
    ld = next((x for x in lines if x.startswith("Krainy kanonu (E4/9): z zapisu")), None)
    rep.add("E4/9 K23: zapis z doby 362 (9 dob)", ld[:200] if ld else "brak linii \"z zapisu\" w tym biegu", "autotest zapisu: 0 bledow, niewolni i Mur = stan doby zapisu", None)
    rep.add("E4/9 K24: progi etapow 1-3, wojsko lordow w wojnie 95-115 tys.", "--grupa etap2 / etap3 na tym samym logu", "bez zmian", None)
    # K25 Wolni Ludzie
    mfree = re.search(r"freefolk ([\d.]+)M", cal)
    fr = [fnum(r"Wolni Ludzie: stan " + F_, swl[d]) for d in sd]
    fr = [x for x in fr if x is not None]
    fb = sum(1 for d in fd if re.search(r"\(freefolk\)[^;]*HAMULEC", fh[d]))
    f100 = all("Wolni Ludzie 100%" in fh[d] for d in fd) if fd else False
    rep.add("E4/9 K25: Wolni Ludzie - kalibracja / prog 100% / hamulec w top 8", f"{mfree.group(1) if mfree else '?'}M / {'TAK' if f100 else 'NIE'} / {fb} dob",
            "0.30M / TAK / 0", (mfree is not None and abs(float(mfree.group(1)) - 0.30) < 0.005 and f100 and fb == 0) if (mfree and fd) else None)
    rep.add("E4/9 K25: Wolni Ludzie - stan d1 -> ostatnia", f"{fr[0]:.0f} -> {fr[-1]:.0f}" if fr else "-", "INFO", None)

# ============================================================ ETAP 5 (E5/0, paczka 195): szkielet grupy etap5 + blok E5/0
def _mean(xs):
    xs = [x for x in xs if x is not None]
    return sum(xs) / len(xs) if xs else None


def _last28(days):
    return days[-28:] if len(days) > 28 else list(days)


def _grp(pat, s, g=0, default="-"):
    m = re.search(pat, s) if s else None
    return m.group(g) if m else default


def etap5_main(logp, start_arg, until=120, csvp=None):
    """E5/0 (195): grupa --grupa etap5 (projekt etapu 5 rozdz. 4; plan kodu E5/0 rozdz. 8). Okno: doby kampanii 1-until (--do-doby), start kampanii
    jak etap4_main (linia "Pomiary 169c: ... start kampanii dzien N", inaczej pierwsza doba logu). Kolejne paczki dopisuja WLASNE bloki
    (etap5_<paczka>(rep, lines, start, win, **kw)); kazdy blok rusza tylko przy swojej linii startowej - brak linii = wiersz INFO "brak linii startowej",
    nigdy wyjatek. Bieg B - kontrola wartosci czynnych K3 w bloku B1. Wiersze etapu (P rozdz. 4) - INFO "stan wyjsciowy" (wiaza paczki, ktore ich dotycza)."""
    lines = read(logp)
    rep = Report()
    start = start_arg
    if start is None:
        for s in lines:
            if s.startswith("Pomiary 169c: okna"):
                start = num(r"start kampanii dzien (\d+)", s)
                if start is not None:
                    break
    alld = sorted(by_day(lines, "Pieniadz swiata: dzien"))
    if start is None:
        start = alld[0] if alld else 0
    win = [d for d in alld if 1 <= run_day(d, start) <= until]
    print(f"plik: {logp} | doby z linia Pieniadz swiata: {len(alld)} (doby kampanii {run_day(alld[0], start) if alld else '-'}-{run_day(alld[-1], start) if alld else '-'})"
          f" | start kampanii {start} | okno progow: doby kampanii 1-{until} ({len(win)} dob)")
    try:
        etap5_rows(rep, lines, start, win, logp, csvp)
    except Exception as e:   # narzedzie nigdy nie przerywa raportu
        rep.add("etap 5: wiersze etapu", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    # kolejne paczki dopisuja tu swoje bloki (A1: etap5_a1(rep, lines, start, win)); bieg B - kontrola wartosci czynnych K3 w bloku B1
    for blk in (etap5_e50, etap5_a6):   # scalenie porcji A: A6 (lup z kiesy pokonanego) - ten sam ksztalt bloku co E5/0
        try:
            blk(rep, lines, start, win, until=until, logp=logp)
        except Exception as e:
            rep.add(f"blok {blk.__name__}", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    # E5/A1 (111', kasy miast): blok A1 z wlasnym oknem (doby kampanii 2-until) - wolany z etap5_main E5/0 (scalenie porcji A)
    if any(s.startswith("TownPurse (E5/A1):") for s in lines):
        try:
            etap5_a1(rep, lines, start, until, logp)
        except Exception as e:
            rep.add("blok etap5_a1", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    else:
        rep.add("E5/A1: linia startowa TownPurse (E5/A1)", "brak w logu", "jest od wgrania E5/A1", None)
    # E5/A2-A5 (scalenie porcji A): bloki z wlasnymi liniami; kazdy we wlasnym try (narzedzie nigdy nie przerywa raportu)
    bud = e4_series(lines, "Budzet rodow (166): dzien", 1)   # dzien gry - 1 = dzien linii ksiegi ("Obieg", "Pieniadz swiata") tej samej doby
    try:
        if any("-> dwor (E5/A2):" in s for s in bud.values()):
            etap5_a2(rep, lines, start, until, logp, bud)
        else:
            rep.add("E5/A2: linia sprzetu niewydanego (Budzet rodow 166)", "brak w logu", "jest od wgrania E5/A2 (LogEnabled, ClanBudgetEnabled)", None)
    except Exception as e:
        rep.add("blok etap5_a2", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    # E5/A3 (163 + 164c): markietani (linia z dniem gry - klucz = dzien ksiegi, jak 166) i notable (dzien ksiegi)
    mar = e4_series(lines, "Markietani (E5/A3): dzien", 1)
    nob = by_day(lines, "Notable (E5/A3): dzien")
    try:
        if mar or nob:
            etap5_a3(rep, lines, start, until, mar, nob)
        else:
            rep.add("E5/A3: linie Markietani / Notable (E5/A3)", "brak w logu", "sa od wgrania E5/A3 (LogEnabled)", None)
    except Exception as e:
        rep.add("blok etap5_a3", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    # E5/A4 (196): osada oblezona nie placi nic (linia z dniem gry - klucz = dzien ksiegi, jak 166)
    obl = e4_series(lines, "Oblezone (E5/A4): dzien", 1)
    try:
        if obl:
            etap5_a4(rep, lines, start, until, obl, bud)
        else:
            rep.add("E5/A4: linia Oblezone (E5/A4)", "brak w logu", "jest od wgrania E5/A4 (LogEnabled)", None)
    except Exception as e:
        rep.add("blok etap5_a4", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    # E5/B1 (173): kiesa ludu czynna - blok rusza przy linii "Kiesa ludu (E5/B1): CZYNNA" (plan kodu E5/B1 rozdz. 11, progi B1-T1..T20)
    try:
        if any(x.startswith("Kiesa ludu (E5/B1):") for x in lines):
            etap5_b1(rep, lines, start, until, logp)
        else:
            rep.add("E5/B1: linia Kiesa ludu (E5/B1)", "brak w logu", "jest od wgrania E5/B1 (bieg B bez kiesy czynnej = NIE w bloku B1)", None)
    except Exception as e:
        rep.add("blok etap5_b1", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    # E5/A5 (197): BetterEconomy - prawdziwa wplata gracza i dar dla notabli (dopisek linii kampanii BEE 170 i linie akcji gracza)
    try:
        if any(x.startswith("BEE domkniecie (170)") for x in lines):
            etap5_a5(rep, lines)
        else:
            rep.add("E5/A5: linie BEE domkniecie (170)", "brak w logu", "sa przy BetterEconomy v1.4.5 (LogEnabled)", None)
    except Exception as e:
        rep.add("blok etap5_a5", f"wyjatek narzedzia: {e}", "bez wyjatku", False)
    rep.show()
    return 0


def etap5_b1(rep, lines, start, until, logp):
    """E5/B1 (173, kiesa ludu czynna; plan kodu docs/etap5/PLAN-KODU-E5B1.md rozdz. 11, progi B1-T1..T20). Okno: doby kampanii 2-until; sr28 - ostatnie 28 dob okna.
    Linie: "Kiesa ludu (E5/B1): CZYNNA" (stan), "Kiesa ludu: dzien" (doba), "Podstawa robocizny (E5/B1)", "Prawa panskie (E5/B1)", "Zadowolenie ludu (R14)",
    "Kiesa ludu (E5/B1): zapasy startowe", "Gotowka wsi (E5/0)", "Przeplywy osad (kasy miast|kasy zamkow)", "Pieniadz swiata (bilans - przyczyny)"."""
    st_on = [x for x in lines if x.startswith("Kiesa ludu (E5/B1): CZYNNA")]
    st_off = [x for x in lines if x.startswith("Kiesa ludu (E5/B1): NIECZYNNA")]
    stl = next((x for x in lines if x.startswith("Kiesa ludu (E5/B1): okna wpiete:")), None)
    kie = by_day(lines, "Kiesa ludu: dzien")
    win = sorted(d for d in kie if 2 <= run_day(d, start) <= until)
    full = [d for d in win if "doba niepelna" not in kie[d] and "czeka na kalibracje" not in kie[d]]
    w28 = _last28(full)
    # ---- B1-T1 bledy i potkniecia
    errs = [x for x in lines if x.startswith("ERROR in TownFolk")]
    sites = sorted(set(x[len("ERROR in "):].split(":")[0] for x in errs))
    stum = sum(num(r"\| potkniecia (\d+) \(sesja", kie[d], int, 0) or 0 for d in win)
    brak = re.search(r"; BRAK: (.*?); klucz przy starcie", stl) if stl else None
    rep.add("B1-T1: bledy, potkniecia, okna B1 (linia startowa)",
            f"ERROR in TownFolk {len(errs)}" + (f" ({', '.join(sites[:6])})" if sites else "") + f"; potkniecia {stum}; BRAK: {brak.group(1) if brak else '?'}",
            "0 bledow; <= 5 potkniec w biegu; BRAK: -", len(errs) == 0 and stum <= 5 and brak is not None and brak.group(1).strip() == "-")
    # ---- B1-T2 czynna kazdej doby (bieg B bez kiesy czynnej - NIE, P K3)
    a1 = bool(st_on) and all(" | A1: tak" in x for x in st_on)
    miss = [run_day(d, start) for d in range(min(win) if win else 0, (max(win) + 1) if win else 0) if d not in kie]
    rep.add("B1-T2: kiesa czynna kazdej doby (linia stanu CZYNNA, A1 tak, linia doby)",
            f"linii CZYNNA {len(st_on)}, NIECZYNNA {len(st_off)}; dob z linia {len(win)} (pelnych {len(full)}), brak w {len(miss)}" + (f" (d{', d'.join(str(x) for x in miss[:5])})" if miss else ""),
            "CZYNNA, A1 tak, linia kazdej doby 2-until; NIE, gdy bieg B bez kiesy czynnej", bool(st_on) and a1 and not st_off and len(miss) == 0 and len(win) > 0)
    if st_on:
        rep.add("B1-T2b: klucze B w linii stanu (P K3)", _grp(r"klucze B: ([^|]+)", st_on[-1], 1).strip()[:200], "INFO (progi w blokach B3/B5)", None)
        rep.add("B1-T2c: stawki i przelaczniki (linia stanu)", (_grp(r"stawki: ([^|]+)", st_on[-1], 1).strip() + " | " + _grp(r"kotwica: ([^|]+)", st_on[-1], 1).strip())[:300], "INFO", None)
        rep.add("B1-T2d: kategorie lepsze i zakupy wsi (znalezione)", _grp(r"kategorie \"lepsze\": ([^|]+)", st_on[-1], 1).strip()[:200], "INFO (BRAK = lista do poprawy)", None)
    if not full:
        return
    # ---- B1-T3 kontrola zamknieta kies
    bad3 = []
    for d in full:
        m = re.search(r"kontrola: zmiana kies (-?\d+) = wplywy (-?\d+) - wydatki (-?\d+), r = (-?\d+|-)", kie[d])
        if m and m.group(4) != "-" and abs(int(m.group(4))) > 1:
            bad3.append((run_day(d, start), int(m.group(4))))
    rep.add("B1-T3: kontrola zamknieta kies ludu (zmiana = wplywy - wydatki)", f"poza progiem {len(bad3)} z {len(full)} dob" + (f" (np. d{bad3[0][0]}: r = {bad3[0][1]})" if bad3 else ""),
            "|r| <= 1 zl kazdej doby", len(bad3) == 0)
    # ---- B1-T4 reszta ksiegi (etap 3)
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    cd = [d for d in full if d in cau]
    rv = num(r"srednia 28 dob ([+-]?\d+)", cau[cd[-1]]) if cd else None
    rep.add("B1-T4: reszta ksiegi R (srednia 28 dob, ostatnia doba okna)", f"{rv}" if rv is not None else "brak linii", "|R| < 10 000 (prog etapu 3)", (abs(rv) < 10000) if rv is not None else None)
    # ---- B1-T5 "zakupy" z niczego 0 (miasta i zamki)
    pm, pz = by_day(lines, "Przeplywy osad (kasy miast): dzien"), by_day(lines, "Przeplywy osad (kasy zamkow): dzien")
    nz = [run_day(d, start) for d in full if (num(r'"zakupy" mieszkancow ([+-]?\d+)', pm.get(d, ""), int, 0) or 0) != 0 or (num(r'"zakupy" mieszkancow ([+-]?\d+)', pz.get(d, ""), int, 0) or 0) != 0]
    lf = _mean([num(r"lud za towar \(E5/B1\) ([+-]?\d+)", pm.get(d, ""), int) for d in w28])
    rep.add("B1-T5: \"zakupy\" z niczego (kasy miast i zamkow)", f"dob != 0: {len(nz)} z {len(full)}" + (f" (d{', d'.join(str(x) for x in nz[:5])})" if nz else "") + (f"; lud za towar sr28 {lf:.0f}/d" if lf is not None else ""),
            "0 kazdej doby (A1-T4 dalej)", len(nz) == 0)
    # ---- B1-T6 zjedzone bez zaplaty < 1% wydatku gry
    q6 = _mean([fnum(r"zjedzone bez zaplaty \d+ \(([\d.]+)%", kie[d]) for d in w28])
    rep.add("B1-T6: zjedzone bez zaplaty (sr28)", f"{q6:.2f}% wydatku gry" if q6 is not None else "-", "< 1% (miasta + zamki)", (q6 < 1.0) if q6 is not None else None)
    # ---- B1-T7 stac jedzenie - kazde miasto (prog projektu 0 - spodziewane NIE: rozklad, nie stawki - plan rozdz. 7.3, raport dla Jeffa)
    n7 = [num(r"miast ze stac jedzenie < 95%: (\d+)", kie[d], int) for d in full]
    n7 = [x for x in n7 if x is not None]
    n28 = _mean([num(r"miast ze stac jedzenie < 95%: (\d+)", kie[d], int) for d in w28])
    d40 = [d for d in full if run_day(d, start) <= 40]
    t40 = _mean([num(r"miast ze stac jedzenie < 95%: (\d+)", kie[d], int) for d in d40[-14:]]) if d40 else None
    rep.add("B1-T7: miast ze stac jedzenie < 95% (sr28, max, trend wobec d40)",
            (f"sr28 {n28:.1f}, max {max(n7)}" if n7 and n28 is not None else "-") + (f", d27-40 {t40:.1f}" if t40 is not None else ""),
            "prog projektu 0 kazdej doby - spodziewane NIE (rozklad dochodu miast, plan rozdz. 7.3) - raport dla Jeffa", (max(n7) == 0) if n7 else None, decide=True)
    # ---- B1-T8 pelny koszyk swiata
    pw = _mean([fnum(r"pelny W/R/P ([\d.]+)%", kie[d]) for d in w28])
    rep.add("B1-T8: pelny koszyk W (waga potrzeb, sr28)", f"W {pw:.1f}%" if pw is not None else "-", ">= 98% (P) - spodziewane NIE (plan 7.1), raport", (pw >= 98) if pw is not None else None, decide=True)
    # ---- B1-T9 podzamcze
    c9 = _mean([fnum(r"\| podzamcze \d+: .*?stac ([\d.]+)%", kie[d]) for d in w28])
    nc = _mean([num(r"zamkow bez kredytu (\d+)", kie[d], int) for d in w28])
    rep.add("B1-T9: podzamcze - stac (sr28, z kuchnia pana)", (f"{c9:.1f}%" if c9 is not None else "-") + (f"; zamkow bez kredytu dworu sr28 {nc:.0f}" if nc is not None else ""),
            ">= 95% (INFO, gdy NIE - liczba zamkow bez dworu)", (c9 >= 95) if c9 is not None else None)
    # ---- B1-T10/T11 wies: pan w utargu i w gotowce (Gotowka wsi - linia przy LogEnabled i ksiedze obiegu)
    gw = by_day(lines, "Gotowka wsi (E5/0): dzien")
    ut, gg, worst = [], [], []
    for d in w28:
        x = gw.get(d)
        if not x:
            continue
        for mm in re.finditer(r"(niska|zwykla|wysoka|zwolnienie): wsi \d+, G -?\d+ \([^)]*\), pan [^,]+, pan z majatkami ([\d.]+)%, w utargu: pan ([\d.]+)% / majatki ([\d.]+)%", x):
            if mm.group(1) == "zwykla":
                ut.append(float(mm.group(3)) + float(mm.group(4))); gg.append(float(mm.group(2)))
            worst.append(float(mm.group(2)))
    u10, g11 = _mean(ut), _mean(gg)
    rep.add("B1-T10: wies - pan z majatkami w utargu (zwykla, sr28)", f"{u10:.1f}%" if u10 is not None else "brak linii Gotowka wsi", "35% +-2 pp (niska 25, wysoka 45)", (abs(u10 - 35) <= 2) if u10 is not None else None)
    rep.add("B1-T11: wies - pan z majatkami w gotowce G (zwykla sr28; najwiecej z polityk)", (f"{g11:.1f}%" if g11 is not None else "-") + (f"; max {max(worst):.1f}%" if worst else ""),
            "zwykla 28-45%, kazda <= 50%", (28 <= g11 <= 45 and max(worst) <= 50) if (g11 is not None and worst) else None)
    # ---- B1-T12 renta 20% wsi zastapiona oplatami
    pr = by_day(lines, "Prawa panskie (E5/B1): dzien")
    fee = _mean([num(r"oplaty stale wsi (\d+)", pr[d], int) for d in w28 if d in pr])
    rep.add("B1-T12: oplaty stale wsi (sr28; renta 20% = 0 przy kiesie czynnej)", f"{fee:.0f}/d" if fee is not None else "brak linii Prawa panskie", "5-15 tys./d [R 9.0]",
            (5000 <= fee <= 15000) if fee is not None else None)
    # ---- B1-T13 zakupy wsi
    vb = [(num(r"wsie \d+: zakupy (\d+) z (\d+)", kie[d], int), num(r"wsie \d+: zakupy \d+ z (\d+)", kie[d], int)) for d in w28]
    vb = [(a, b) for a, b in vb if a is not None and b]
    q13 = sum(a for a, _ in vb) / sum(b for _, b in vb) * 100 if vb else None
    rep.add("B1-T13: zakupy wsi wobec potrzeby (sr28)", f"{q13:.1f}%" if q13 is not None else "-", ">= 75% (INFO; KL 90% - wsie bez gotowki)", None)
    # ---- B1-T14 zapasy startowe
    sl = [x for x in lines if x.startswith("Kiesa ludu (E5/B1): zapasy startowe")]
    if sl:
        tot = [int(t) for t in re.findall(r"\) = (\d+) \(miast", sl[0])]
        rep.add("B1-T14: zapasy startowe (linia jednorazowa)", f"linii {len(sl)}; razem {sum(tot)}; " + _grp(r"zapasy startowe ([^-]+)", sl[0], 1).strip()[:120],
                "nowa kampania: 5-10 mln z daru; zapis z d362: \"z nadwyzki\"", (5e6 <= sum(tot) <= 10e6) if "z daru" in sl[0] else None)
    else:
        rep.add("B1-T14: zapasy startowe", "brak linii", "1 linia (nowa kampania albo stary zapis)", False)
    # ---- B1-T15 R14
    r14 = by_day(lines, "Zadowolenie ludu (R14): dzien")
    k15 = _mean([num(r"korekty (\d+)", r14[d], int) for d in w28 if d in r14])
    rep.add("B1-T15: R14 - korekty zadowolenia (sr28; tylko osady z Afford < 0.98)", f"{k15:.0f}/d" if k15 is not None else "brak linii", "INFO", None)
    # ---- B1-T16 koszt
    cost = [(fnum(r"czas: latki ([\d.]+) ms", kie[d]) or 0) + (fnum(r", doba ([\d.]+) ms", kie[d]) or 0) for d in full if "czas: latki" in kie[d]]
    if cost:
        rep.add("B1-T16: koszt (latki + doba) srednio", f"{sum(cost) / len(cost):.1f} ms/dobe (max {max(cost):.1f})", "<= 50 ms (INFO; optymalizacja na koniec - decyzja 08.10)", None)
    # ---- B1-T17/T18/T19 odsylacze
    rep.add("B1-T17: S4 jedzenie warowni", "--grupa etap4 (F6/F7) i linia \"Jedzenie z targu (E5/0)\"", "warownie miast glodne 0 przed 30. doba zimy", None)
    rep.add("B1-T18: progi etapow 2-4", "--grupa etap2 / etap3 / etap4 na tym samym logu", "glowy < 5 000 najwyzej 10; 0 bankructw; wojsko lordow w wojnie 95-115 tys. (podloga 85)", None)
    # ---- B1-T20 haki 75/25: wojsko w dochodzie ludu wobec podstawy robocizny (75%)
    lab = by_day(lines, "Podstawa robocizny (E5/B1): dzien")
    xa = sum(num(r"\| dochod ludu miast z zewnatrz i z kas -?\d+: wojsko (\d+)", kie[d], int, 0) or 0 for d in w28)
    ya = sum(fnum(r"zolnierze i zalogi \(75%\) ([\d.]+)", lab.get(d, ""), 0.0) or 0.0 for d in w28)
    q20 = xa / (xa + ya) * 100 if xa + ya > 0 else None
    rep.add("B1-T20: haki 75/25 - czesc ludu w wydatkach zolnierzy w miastach (sr28)", f"{q20:.2f}%" if q20 is not None else "-", "25% +-1 pp", (abs(q20 - 25) <= 1) if q20 is not None else None)


def etap5_rows(rep, lines, start, win, logp, csvp=None):
    """Wiersze etapu 5 (projekt rozdz. 4) w szkielecie - w E5/0 INFO "stan wyjsciowy"; paczki, ktore je wiaza, zmieniaja werdykt we wlasnym bloku."""
    w28 = _last28(win)
    # panowie samych zamkow (budzet-rodow.csv): udzial w D swiata i w D stalym, mediana kiesy glowy - rody z wojna >= 21 z 28 dob (wiaze B3)
    csvp = csvp or csv_path_of(logp)
    rows = read_csv(csvp)
    if rows:
        byd = {}
        for r in rows:
            try:
                byd.setdefault(int(r.get("dzien") or 0), []).append(r)
            except ValueError:
                continue
        cd = sorted(d for d in byd if run_day(d, start) >= 1)
        wd = [d for d in cd if 252 <= run_day(d, start) <= 364] or cd[-28:]

        def fv(v):
            try:
                return float(v)
            except (TypeError, ValueError):
                return None
        warc = {}
        for d in wd:
            for r in byd[d]:
                if r.get("rodzaj") == "zamek" and r.get("wojna") == "1":
                    warc[r.get("rod_id")] = warc.get(r.get("rod_id"), 0) + 1
        need = min(21, max(1, len(wd) * 3 // 4))
        shD, shS, kies = [], [], []
        for d in wd:
            tD = tS = zD = zS = 0.0
            for r in byd[d]:
                if r.get("rodzaj") == "gracz":
                    continue
                D, S = fv(r.get("D")) or 0.0, fv(r.get("d_staly")) or 0.0
                tD += D
                tS += S
                if r.get("rodzaj") == "zamek":
                    zD += D
                    zS += S
                    if warc.get(r.get("rod_id"), 0) >= need:
                        k = fv(r.get("kiesa_glowy"))
                        if k is not None:
                            kies.append(k)
            if tD:
                shD.append(zD / tD)
            if tS:
                shS.append(zS / tS)
        kies.sort()
        rep.add("etap 5 (B3): panowie samych zamkow - udzial w D / D stalym, mediana kiesy glowy",
                f"D {(_mean(shD) or 0) * 100:.1f}%, D staly {(_mean(shS) or 0) * 100:.1f}%, mediana kiesy (wojna >= {need} dob okna) {kies[len(kies) // 2] if kies else '-'}"
                f" (doby {run_day(wd[0], start) if wd else '-'}-{run_day(wd[-1], start) if wd else '-'})", "stan wyjsciowy (wiaze B3)", None)
    else:
        rep.add("etap 5 (B3): panowie samych zamkow", f"brak {csvp}", "stan wyjsciowy (wiaze B3)", None)
    obg = by_day(lines, "Obieg: dzien")
    sak = _mean([fnum(r"= ([\d.]+) dni zoldu naliczonego", obg[d]) for d in w28 if d in obg])
    rep.add("etap 5 (A3): sakiewki zolnierzy w dniach zoldu", f"sr28 {sak:.1f}" if sak is not None else "brak dopisku (TownFolkMode 0 albo bieg sprzed E5/0)",
            "stan wyjsciowy; <= 15 dni po A3 (dzis ok. 29)", None)
    pw = by_day(lines, "Pieniadz swiata: dzien")
    pd = [d for d in win if d in pw]
    if len(pd) >= 2:
        a = pd[-29] if len(pd) > 28 else pd[0]
        za, zb = num(r"\| razem (\d+)", pw[a]), num(r"\| razem (\d+)", pw[pd[-1]])
        ch = (zb - za) / za * 100 if za and zb is not None else None
        rep.add("etap 5 (C1): zloto swiata na 28 dob", f"{ch:+.2f}% (d{run_day(a, start)} -> d{run_day(pd[-1], start)})" if ch is not None else "-",
                "stan wyjsciowy; < 0.1% po C1", None)
    dow = by_day(lines, "Dowoz (skutki): dzien")
    zat = _mean([num(r"wsie zamkowe (\d+) z", dow[d]) for d in w28 if d in dow])
    rep.add("etap 5 (B4): zatkane magazyny wsi zamkowych", f"sr28 {zat:.1f}" if zat is not None else "brak linii", "stan wyjsciowy (lato/zima osobno - wiaze B4)", None)
    prz = by_day(lines, "Przeplywy osad: dzien")
    uns = _mean([num(r"towar niesprzedany po wizycie: w miastach (\d+) szt", prz[d]) for d in w28 if d in prz])
    rep.add("etap 5 (kazdy zbior): towar wsi niesprzedany w miastach", f"sr28 {uns:.0f} szt./d" if uns is not None else "brak linii",
            "stan wyjsciowy (miasta kupuja >= 98% plonu)", None)
    lud = by_day(lines, "Ludzie: dzien")
    arm = _mean([num(r"partie rodow (\d+)", lud[d]) for d in w28 if d in lud])
    rep.add("etap 5 (kazdy zbior): wojsko lordow", f"sr28 {arm:.0f}" if arm is not None else "brak linii", "stan wyjsciowy (w wojnie 95-115 tys., podloga 85)", None)


def etap5_e50(rep, lines, start, win, **kw):
    """E5/0 (195): progi E50-T1..T10 (plan kodu E5/0 rozdz. 8) - linie "Kiesa ludu (E5/0)", "Kiesa ludu (na sucho)", "Nadwyzka kas (E5/0)", "Bitwy (E5/0)",
    "Notable (E5/0)", "Jedzenie z targu (E5/0)", "Zadowolenie BK (E5/0)", "Gotowka wsi (E5/0)" i dopisek "Obieg". Doby niepelne (pierwsza w sesji) - poza progami."""
    stl = next((s for s in lines if s.startswith("Kiesa ludu (E5/0): okna wpiete:")), None)
    if stl is None:
        rep.add("E5/0: linia startowa Kiesa ludu (E5/0)", "brak linii startowej", "jest (paczka E5/0 w DLL)", None)
        return
    kie = by_day(lines, "Kiesa ludu (na sucho): dzien")
    full = [d for d in win if d in kie and "doba niepelna" not in kie[d] and "czeka na kalibracje" not in kie[d]]
    w28 = _last28(full)
    # ---- T1 bledy, linia startowa, potkniecia, linia sesji
    errs = [s for s in lines if s.startswith("ERROR in TownFolk")]
    sites = sorted(set(s[len("ERROR in "):].split(":")[0] for s in errs))
    brak = re.search(r"; BRAK: (.*?); wlasciciele", stl)
    stum = sum(num(r"\| potkniecia (\d+) \(sesja", kie[d], int, 0) or 0 for d in win if d in kie)
    sess = [s for s in lines if s.startswith("Kiesa ludu (E5/0): sesja od dnia")]
    ok1 = len(errs) == 0 and brak is not None and brak.group(1).strip() == "-" and stum <= 5 and len(sess) > 0
    rep.add("E50-T1: bledy, linia startowa, potkniecia, linia sesji",
            f"ERROR in TownFolk {len(errs)}" + (f" ({', '.join(sites[:6])})" if sites else "") + f"; BRAK: {brak.group(1) if brak else '?'}; potkniecia {stum}; linii sesji {len(sess)}",
            "0 bledow; BRAK: -; potkniecia <= 5 w biegu; linia sesji jest", ok1)
    rep.add("E50-T1: wlasciciele latek celow (linia startowa)", _grp(r"wlasciciele latek celow: (.*?); tryb przy starcie", stl, 1)[:400], "lista (INFO)", None)
    if not full:
        live = any(x.startswith("Kiesa ludu (E5/B1): CZYNNA") for x in lines)   # E5/B1: kiesa czynna - model na sucho nie biegnie (progi w bloku etap5_b1)
        rep.add("E5/0: linia Kiesa ludu (na sucho)", f"pelnych dob {len(full)} z {len(win)}" + ("; kiesa czynna E5/B1 - progi w bloku B1" if live else ""),
                "kazda doba po kalibracji (przy kiesie czynnej - INFO)", None if (live or not win) else False)
        return
    miss = [run_day(d, start) for d in win if d not in kie]
    rep.add("E5/0: linia Kiesa ludu (na sucho) codziennie", f"brak w {len(miss)} z {len(win)} dob" + (f" (d{', d'.join(str(x) for x in miss[:5])})" if miss else ""),
            "0", len(miss) == 0)
    # ---- T2 koszt
    cost = []
    for d in full:
        a, b = fnum(r"czas: latki ([\d.]+) ms", kie[d]), fnum(r", doba ([\d.]+) ms", kie[d])
        if a is not None and b is not None:
            cost.append((a + b, run_day(d, start)))
    if cost:
        m = sum(c for c, _ in cost) / len(cost)
        mx = max(cost)
        rep.add("E50-T2: koszt (latki + doba) srednio", f"{m:.1f} ms/dobe ({len(cost)} dob)", "<= 15 ms", m <= 15)
        rep.add("E50-T2: koszt najwiekszy", f"{mx[0]:.1f} ms (d{mx[1]})", "<= 50 ms (INFO)", None)
    # ---- T3 kontrola na sucho
    bad3 = []
    for d in full:
        q = fnum(r"kontrola: r=-?[\d.]+ \(([\d.]+)% dochodu\)", kie[d])
        if q is not None and q > 1.0:
            bad3.append((run_day(d, start), q))
    rep.add("E50-T3: kontrola na sucho (zamknieta)", f"poza progiem {len(bad3)} z {len(full)} dob" + (f" (np. d{bad3[0][0]}: {bad3[0][1]}%)" if bad3 else ""),
            "|r| <= 1% dochodu kazdej pelnej doby", len(bad3) == 0)
    # ---- T4 bitwy wobec ksiegi + A6
    bit = by_day(lines, "Bitwy (E5/0): dzien")
    bal = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    sc1 = sa = sx = sy = soff = noff = n3 = 0
    es = []
    for d in w28:
        if d not in bit:
            continue
        s = bit[d]
        sc1 += num(r"zwyciezcy dostali -?\d+ \(wodzowie (\d+)", s, int, 0) or 0
        sa += num(r"pula z pokonanych \(zeszlo z kies\): wodzowie (\d+)", s, int, 0) or 0
        # recenzja E5/0 (1-1): wodzowie zdjeci z partii (A6 - obciazenie w nawiasie CommitGoldChanges) i partie lordow z wodzem zdjetym
        soff += num(r"zdjeci z partii - A6 - (\d+) w \d+", s, int, 0) or 0
        noff += num(r"zdjeci z partii - A6 - \d+ w (\d+)", s, int, 0) or 0
        n3 += num(r"partie lordow z wodzem zdjetym \(niewola, ucieczka\) (\d+)", s, int, 0) or 0
        x = y = None
        if d in bal:
            mz = re.search(r"\| ZRODLA .*?\bbitwy (-?\d+)", bal[d])
            mu = re.search(r"\| UJSCIA .*?\bbitwy (-?\d+)", bal[d])
            x = int(mz.group(1)) if mz else None
            y = int(mu.group(1)) if mu else None
        if x is None:
            x = num(r'zrodla "bitwy" (-?\d+)', s, int, None)
        if y is None:
            y = num(r"ujscia (-?\d+) \(a - y", s, int, None)
        sx += x or 0
        sy += y or 0
        es.append(num(r"z niczego netto na ladzie (-?\d+)", s, int, None))

    def within(v, ref, tol):
        return abs(v - ref) <= tol * max(abs(ref), 1) if (v or ref) else True
    rep.add("E50-T4: bitwy - wodzowie zwyciezcow (c1) wobec zrodel \"bitwy\" ksiegi (sr28)", f"c1 {sc1} wobec {sx} ({len(w28)} dob)", "+-2%", within(sc1, sx, 0.02) if w28 else None)
    rep.add("E50-T4: bitwy - wodzowie pokonanych (a) wobec ujsc \"bitwy\" ksiegi (sr28)", f"a {sa} wobec {sy}", "+-2%", within(sa, sy, 0.02) if w28 else None)
    rep.add("E50-T4: wodzowie zdjeci z partii - obciazenie A6 (sr28; INFO)", f"{soff} zl w {noff} obciazeniach; partii lordow z wodzem zdjetym {n3}",
            "INFO (bez A6: 0 zl - gra ich nie obciaza; z A6 > 0, w a i w ujsciach \"bitwy\")", None)
    em = _mean(es)
    if em is not None:
        rep.add("E50-T4: A6 - z niczego netto na ladzie sr28", f"{em:.0f}/d", "<= 5 000/d - inaczej A6 POTRZEBNE (Jeff 11.10 16:20: A6 i tak w porcji A)",
                em <= 5000, decide=em > 5000)
    # ---- T5 wydatek z petli BK wobec ConsumedValue
    bad5, nob, lfn, lfc = [], [], [], 0
    for d in full:
        q = fnum(r"\(roznica (-?[\d.]+)%\)", kie[d])
        if q is not None and abs(q) > 2.0:
            bad5.append((run_day(d, start), q))
        nob.append(num(r"osad z konsumpcja bez BK (\d+)", kie[d], int, 0) or 0)
        # recenzja E5/0 (2-1): dokup jedzenia BK (zapas <= 10%) - nadwyzka petli nad ConsumedValue zdjeta z wydatku jedzenia przed porownaniem
        lfn.append(num(r"dokup jedzenia BK \(zapas <= 10%\): osad (\d+)", kie[d], int, 0) or 0)
        if d in w28:
            lfc += num(r"petla ponad ConsumedValue zdjeta z wydatku jedzenia (\d+)", kie[d], int, 0) or 0
    rep.add("E50-T5: wydatek z petli BK wobec ConsumedValue", f"poza progiem {len(bad5)} z {len(full)} dob" + (f" (np. d{bad5[0][0]}: {bad5[0][1]}%)" if bad5 else ""),
            "+-2% kazdej doby (K1: budzet raz na kategorie)", len(bad5) == 0)
    rep.add("E50-T5: osad z konsumpcja bez BK (INFO)", f"najwiecej {max(nob) if nob else '-'}", "INFO (d120 zbioru 31: 0)", None)
    rep.add("E50-T5: dokup jedzenia BK - osad (najwiecej na dobe), zdjete z wydatku jedzenia (sr28; INFO)",
            f"osad najwiecej {max(lfn) if lfn else '-'}; zdjete {lfc / max(len(w28), 1):.0f}/d", "INFO (oblezenia, glod - zapas warowni <= 10%)", None)
    # ---- T6 notable wobec "Obieg: notable"
    nt = by_day(lines, "Notable (E5/0): dzien")
    obn = by_day(lines, "Obieg: notable i karawany: dzien")
    so = si = lo = li = ko = ki = 0
    for d in w28:
        if d not in nt or d not in obn:
            continue
        s = nt[d]
        # recenzja E5/0 (1-2): przelewy notabl <-> kasa osady (A3) osobno - do kontroli z ksiega tylko "w nicosc / z niczego"
        ko += (num(r"do kasy osady (\d+) /", s, int, 0) or 0) + (num(r"do kasy osady targowej (\d+) /", s, int, 0) or 0)
        ki += (num(r"/ z kasy osady (\d+)", s, int, 0) or 0) + (num(r"/ z kasy osady targowej (\d+)", s, int, 0) or 0)
        so += (num(r"miasta: w nicosc (\d+)", s, int, 0) or 0) + (num(r"wsie: w nicosc (\d+)", s, int, 0) or 0)
        si += (num(r"miasta: w nicosc \d+ / z niczego (\d+)", s, int, 0) or 0) + (num(r"wsie: w nicosc \d+ / z niczego (\d+)", s, int, 0) or 0)
        lo += abs(num(r"nadwyzka w nicosc (-?\d+)", obn[d], int, 0) or 0)
        li += num(r"dosypka z niczego \+?(-?\d+)", obn[d], int, 0) or 0
    rep.add("E50-T6: notable - w nicosc wobec Obieg (sr28)", f"{so} wobec {lo}", "+-1%", within(so, lo, 0.01) if w28 else None)
    rep.add("E50-T6: notable - z niczego wobec Obieg (sr28)", f"{si} wobec {li}", "+-1%", within(si, li, 0.01) if w28 else None)
    rep.add("E50-T6: notable - przez kase osady (A3; sr28; INFO)", f"do kasy {ko}, z kasy {ki}", "INFO (bez A3: 0; z A3 - pasmo w kasach, w nicosc / z niczego ok. 0)", None)
    # ---- T7 gotowka wsi: podzial wedlug polityki = liczby swiata
    gw = by_day(lines, "Gotowka wsi (E5/0): dzien")
    bad7, share = [], []
    for d in full:
        if d not in gw:
            continue
        m7 = re.search(r"kontrola podzialu: utarg (-?\d+) wobec (-?\d+), przelewy (-?\d+) wobec (-?\d+), sakiewki (-?\d+) wobec (-?\d+)", gw[d])
        if not m7:
            continue
        v = [int(x) for x in m7.groups()]
        if abs(v[0] - v[1]) > 1 or abs(v[2] - v[3]) > 1 or abs(v[4] - v[5]) > 1:
            bad7.append(run_day(d, start))
        g = num(r"wplynelo (-?\d+)", gw[d], int, 0) or 0
        t, r_ = num(r"licznik podatku wsi (-?\d+)", gw[d], int, 0) or 0, num(r"\+ renta (-?\d+)\)", gw[d], int, 0) or 0
        if d in w28 and g > 0:
            share.append((t + r_) / g)
    nd7 = sum(1 for d in full if d in gw)
    rep.add("E50-T7: gotowka wsi - suma wedlug polityki = swiat", f"rozjazd w {len(bad7)} z {nd7} dob" + (f" (d{', d'.join(str(x) for x in bad7[:5])})" if bad7 else ""),
            "+-1 zl kazdej doby (jedna definicja z Przeplywy osad)", len(bad7) == 0 if nd7 else None)
    rep.add("E50-T7: udzial pana (licznik + renta) w gotowce wsi sr28 (INFO)", f"{(_mean(share) or 0) * 100:.1f}%" if share else "-", "INFO (rozpoznanie A: 86.2%)", None)
    # ---- T8 gra bez zmian
    rep.add("E50-T8: gra bez zmian (reszta ksiegi < 10 tys./d)", "--grupa etap3 na tym samym logu; recenzja diffu \"tylko odczyt\"", "INFO", None)
    # ---- T9 odczyty do kalibracji B (sr28 i ostatnia doba)
    last = full[-1]
    k = kie[last]
    mav = _mean([fnum(r"kotwica m srednio ([\d.]+)", kie[d]) for d in w28])
    kav = _mean([fnum(r"kasy miast po B1 \(szacunek\): (-?\d+)", kie[d]) for d in w28])
    rep.add("E50-T9: stac jedzenie W/R/P (ostatnia doba)", _grp(r"stac jedzenie W/R/P ([^,|]+)", k, 1).strip(), "INFO", None)
    rep.add("E50-T9: pelny koszyk W/R/P; miast < 95% (ostatnia doba)", _grp(r"pelny W/R/P [^,|]+, miast ze stac jedzenie < 95%: \d+, pelny < 95%: \d+", k), "INFO", None)
    rep.add("E50-T9: kotwica m srednio sr28; zjedzone bez zaplaty (ostatnia doba)",
            (f"m {mav:.2f}" if mav is not None else "m -") + "; " + _grp(r"zjedzone bez zaplaty (\d+ \([^)]*\))", k, 1), "INFO", None)
    rep.add("E50-T9: kasy miast po B1 (szacunek) sr28", f"{kav:.0f}/d" if kav is not None else "-", "INFO (rachunek przed testem B)", None)
    nad = by_day(lines, "Nadwyzka kas (E5/0): dzien")
    if last in nad:
        s = nad[last]
        rep.add("E50-T9: nadwyzka D4 i pas ponizej zapasu (ostatnia doba)", _grp(r"ponad zapasem \d+ w \d+ miastach", s) + "; "
                + _grp(r"zakupy kas miast z pasa ponizej zapasu: [^|]+", s)[:160], "INFO", None)
        rep.add("E50-T9: korona - potrzeba R15, K3", _grp(r"potrzeba R15 dzis: \d+ krolestw", s) + "; " + _grp(r"K3: [^|]+", s)[:140], "INFO", None)
        rep.add("E50-T9: udzial zysku kupcow zabierany przez pana i korone (Jeff 16:10)", _grp(r"udzial zysku kupcow zabierany przez pana i korone: [^|]+", s)[:260],
                "INFO (odniesienie historyczne ok. 10% - decyzja Jeffa po tescie)", None)
        rep.add("E50-T9: zaulki - kasy ponizej zapasu (Jeff 16:10)", _grp(r"zaulki \(od startu sesji\): [^|]+", s)[:240], "INFO", None)
    fo = by_day(lines, "Jedzenie z targu (E5/0): dzien")
    if last in fo:
        rep.add("E50-T9: zamki z polka jedzenia < 10 dni zuzycia (B4)", _grp(r"polka < 10 dni zuzycia: \d+ z \d+ \(mediana [^)]*\)", fo[last]), "INFO (stan wyjsciowy B4)", None)
    sa_ = by_day(lines, "Zadowolenie BK (E5/0): dzien")
    if last in sa_:
        rep.add("E50-T9: zadowolenie BK (R14)", _grp(r"srednie Food / Industrial / General / Luxury \(0\.\.1\) [^|]+", sa_[last])[:220], "INFO", None)
    if sess:
        rep.add("E50-T9: modele czynne (lojalnosc BK, warstwa BK)", _grp(r"modele czynne: (.*?) \| zapasy startowe", sess[-1], 1)[:300], "INFO (RB 7 pkt 2 i 4)", None)
    if last in gw:
        rep.add("E50-T9: gotowka wsi wedlug polityki BK (ostatnia doba)", _grp(r"wedlug polityki BK: (.*?) \| kontrola podzialu", gw[last], 1)[:300], "INFO (wiaze B1, C3)", None)
    # ---- T10 zapis: linia sesji po wczytaniu, kalibracja od nowa, 0 bledow
    loads = [s for s in sess if (num(r"sesja od dnia (\d+)", s, int, None) or start) - start + 1 > 2]
    if loads:
        rep.add("E50-T10: zapis - linia sesji po wczytaniu, 0 bledow", f"sesji z zapisu {len(loads)}, ERROR in TownFolk {len(errs)}", "linia sesji, 0 bledow", len(errs) == 0)
    else:
        rep.add("E50-T10: zapis (9 dob)", "bieg nowej kampanii - prog z biegu z zapisu", "INFO", None)

def etap5_a1(rep, lines, start, until, logp):
    """E5/A1 (111', kasy miast; plan kodu docs/etap5/PLAN-KODU-E5A1.md rozdz. 9.1, progi A1-T1..T22). Stan A1 - z dopisku dziennego w linii
    "Kasy miast (169c)" (" | E5/A1: ..."), nie z linii startowej (ta powstaje przed MCM). Okno: doby kampanii 2-until (doba 1 = dar startowy
    i pierwsze ticki); sr28 = ostatnie 28 dob okna. Dzien linii ksiegi (169c, Przeplywy, Zawor zamkow, przyczyny, Towary) = dzien gry - 1;
    linie z dniem gry (Korona 165, Budzet rodow 166) przesuniete o 1."""
    k169 = by_day(lines, "Kasy miast (169c): dzien")
    pt = by_day(lines, "Przeplywy osad (kasy miast): dzien")
    pz = by_day(lines, "Przeplywy osad (kasy zamkow): dzien")
    pv = by_day(lines, "Przeplywy osad: dzien")
    zz = by_day(lines, "Zawor zamkow (110): dzien")
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    tow = by_day(lines, "Towary (utarg wozow): dzien")
    e50 = by_day(lines, "Nadwyzka kas (E5/0): dzien")
    kor = e4_series(lines, "Korona: wplywy dnia (165): dzien", 1)
    bud = e4_series(lines, "Budzet rodow (166): dzien", 1)
    win = [d for d in sorted(k169) if 2 <= run_day(d, start) <= until]
    last28 = win[-28:]
    seg = {}
    for d in win:
        s = k169[d]
        i = s.find(" | E5/A1:")
        seg[d] = s[i:] if i >= 0 else None

    def vals(src, days, pat, cast=int):
        out = {}
        for d in days:
            s = src.get(d)
            if s is None:
                continue
            v = num(pat, s, cast)
            if v is not None:
                out[d] = v
        return out

    # ---------------- A1-T1: bledy i potkniecia
    errs = [s for s in lines if s.startswith("ERROR in ") and "TownPurse" in s]
    stum = vals(seg, win, r"\| potkniecia (\d+)")
    rep.add("E5/A1 A1-T1: bledy TownPurse (ERROR in) / potkniecia w biegu", f"{len(errs)} / {sum(stum.values())}", "0 bledow; potkniecia <= 5",
            len(errs) == 0 and sum(stum.values()) <= 5)

    # ---------------- A1-T2: linia startowa
    stl = next((s for s in lines if s.startswith("TownPurse (E5/A1): kasy miast jako")), None)
    if stl is None:
        rep.add("E5/A1 A1-T2: linia startowa", "brak", "jest, BRAK: -, modele >= 1, konsumpcja: tak", False)
    else:
        mod = num(r"w (\d+) modelach", stl, int, 0) or 0
        ok2 = "BRAK: -" in stl and mod >= 1 and "konsumpcja: tak" in stl
        rep.add("E5/A1 A1-T2: linia startowa", f"modele {mod}, konsumpcja {'tak' if 'konsumpcja: tak' in stl else 'BRAK'}, "
                + ("BRAK: -" if "BRAK: -" in stl else "BRAK: " + (re.search(r"BRAK: ([^|]*)", stl).group(1).strip() if re.search(r"BRAK: ([^|]*)", stl) else "?")),
                "BRAK: -, modele >= 1, konsumpcja: tak", ok2)

    # ---------------- A1-T3: tryb czynny kazdej doby
    modes = {}
    for d in win:
        s = seg.get(d)
        if s is None:
            continue
        m = num(r"E5/A1: tryb regulatora (\d)", s)
        if m is None:
            m = 0 if "E5/A1: WYLACZONE" in s else (-1 if "E5/A1: NIECZYNNE" in s else None)
        if m is not None:
            modes[d] = m
    miss3 = [d for d in win if d not in modes]
    mset = sorted(set(modes.values()))
    mode = mset[0] if len(mset) == 1 else (2 if 2 in mset else (mset[0] if mset else None))
    if not win:
        rep.add("E5/A1 A1-T3: tryb regulatora kazdej doby", "brak dob w oknie", "2 kazdej doby", None)
    elif len(mset) > 1 or miss3:
        rep.add("E5/A1 A1-T3: tryb regulatora kazdej doby", f"tryby {mset}, bez dopisku {len(miss3)} z {len(win)} dob", "jeden tryb caly bieg (2; zmiana w biegu = NIE)", False)
    elif mset == [2]:
        rep.add("E5/A1 A1-T3: tryb regulatora kazdej doby", f"2 w {len(win)} dobach", "2 kazdej doby (Jeff 16:10)", True)
    elif mset == [1]:
        rep.add("E5/A1 A1-T3: tryb regulatora kazdej doby", f"1 w {len(win)} dobach - bieg awaryjny", "2 (1 = bieg awaryjny: T6/T7 wedlug trybu 1)", None)
    else:
        rep.add("E5/A1 A1-T3: tryb regulatora kazdej doby", f"{mset[0]} w {len(win)} dobach ({'WYLACZONE' if mset[0] == 0 else 'NIECZYNNE - latki 110 BRAK'})",
                "2 (0 caly bieg = bieg porownawczy - progi INFO)", None if mset[0] == 0 else False)
    live = mode in (1, 2)

    def zero_each(name, src, pat, base, rule="0 kazdej doby okna"):
        v = vals(src, win, pat)
        bad = [d for d, x in v.items() if x != 0]
        if not v:
            rep.add(name, "brak linii", rule, None)
            return v
        rep.add(name, f"niezerowe w {len(bad)} z {len(v)} dob" + (f" (najwiecej {max(abs(v[d]) for d in bad)}, doba {run_day(bad[0], start)})" if bad else "") + f"; dzis (baza) {base}",
                rule, (len(bad) == 0) if live else None)
        return v

    # ---------------- A1-T4/T5/T6: kasy miast - "zakupy", kasowanie, dosypka
    zero_each("E5/A1 A1-T4: \"zakupy\" w kasach miast", pt, r'"zakupy" mieszkancow ([+-]?\d+)', "425 318 (L:31722)")
    zero_each("E5/A1 A1-T5: regulator kas miast - skasowal", pt, r"regulator kasy [+-]?\d+ \[P\] \(dosypal \d+, skasowal (\d+)", "248 065 (L:31686)")
    if mode == 1:
        dos = vals(pt, last28, r"regulator kasy [+-]?\d+ \[P\] \(dosypal (\d+), skasowal")
        m6 = st.mean(dos.values()) if dos else None
        rep.add("E5/A1 A1-T6: dosypka kas miast (tryb 1, sr28)", f"{m6:.0f}/d" if m6 is not None else "brak linii", "<= 35 000/d (2 x dzis 17 057); wiecej - DECYZJA",
                (m6 <= 35000) if m6 is not None else None, decide=True)
    else:
        zero_each("E5/A1 A1-T6: dosypka kas miast (tryb 2)", pt, r"regulator kasy [+-]?\d+ \[P\] \(dosypal (\d+), skasowal", "sr28 17 057")

    # ---------------- A1-T7: dosypka kas zamkow i linia 110 z "tryb 2"
    dz = vals(pz, win, r"dosypal (\d+), skasowal")
    zt2 = [d for d in win if d in zz and "dosypka zablokowana (tryb 2" in zz[d]]
    if not zz:
        rep.add("E5/A1 A1-T7: dosypka kas zamkow (tryb 2)", "brak linii Zawor zamkow (110) - Castle Purse Enabled wylaczone?", "tryb 2: dosypal 0, linia 110 z \"tryb 2\"", None)
    elif mode == 2:
        badz = [d for d, x in dz.items() if x != 0]
        rep.add("E5/A1 A1-T7: dosypka kas zamkow (tryb 2)", f"dosypal != 0 w {len(badz)} z {len(dz)} dob; linia 110 z \"tryb 2\" w {len(zt2)} z {len([d for d in win if d in zz])} dob",
                "dosypal 0 kazdej doby; linia 110 z \"dosypka zablokowana (tryb 2\"", len(badz) == 0 and len(zt2) == len([d for d in win if d in zz]))
    else:
        m7 = st.mean([dz[d] for d in last28 if d in dz]) if any(d in dz for d in last28) else None
        rep.add("E5/A1 A1-T7: dosypka kas zamkow", f"sr28 {m7:.0f}/d (tryb {mode})" if m7 is not None else "brak linii", "INFO poza trybem 2 (dzis ok. 1 tys./d)", None)

    # ---------------- A1-T8: zawor - suma i podzial
    vre = re.compile(r"zeszlo (\d+) = panom (\d+) \(przed dekretem (\d+); w krolestwach przed dekretem (\d+);.*?\+ koronie (\d+)")
    vz = {}
    for d in win:
        s = seg.get(d)
        m = vre.search(s) if s else None
        if m:
            vz[d] = [int(x) for x in m.groups()]
    off8 = [d for d in win if seg.get(d) and "zawor: NIE DZIALA" in seg[d]]
    if vz:
        badsum = [d for d, v in vz.items() if v[0] != v[1] + v[4]]
        k28 = sum(vz[d][4] for d in last28 if d in vz)
        lbk28 = sum(vz[d][3] for d in last28 if d in vz)
        ratio = k28 / (lbk28 + k28) if lbk28 + k28 > 0 else None
        rep.add("E5/A1 A1-T8: zawor D = L + K; udzial korony sr28 K / (LBk + K)",
                f"D != L + K w {len(badsum)} z {len(vz)} dob; udzial korony " + (f"{ratio:.3f}" if ratio is not None else "-") + (f"; NIE DZIALA w {len(off8)} dobach" if off8 else ""),
                "0 dob; 0.32-0.34 (1/3 - klucz 0.67)", len(badsum) == 0 and ratio is not None and 0.32 <= ratio <= 0.34 if live else None)
    else:
        rep.add("E5/A1 A1-T8: zawor D = L + K; udzial korony", "brak liczb zaworu" + (f" (NIE DZIALA w {len(off8)} dobach - Population Rent Enabled?)" if off8 else ""),
                "zawor czynny w biegu A", None if not live else False)

    # ---------------- A1-T9: "10 miast bez placenia"
    v9 = vals(seg, win, r"miast z nadwyzka bez zaworu \(10 miast\) (\d+)")
    bad9 = [d for d, x in v9.items() if x != 0]
    rep.add("E5/A1 A1-T9: \"10 miast bez placenia\"", f"niezerowe w {len(bad9)} z {len(v9)} dob" + (f" (najwiecej {max(v9[d] for d in bad9)})" if bad9 else "") if v9 else "brak liczb",
            "0 kazdej doby (dzis 10, RB 5.4)", (len(bad9) == 0) if (v9 and live) else None)

    # ---------------- A1-T10: tarcze
    held = vals(k169, win, r"pod tarcza zoldu \(zold i wydatki ludzi w drodze do pana\) (\d+)")
    badh = [d for d, x in held.items() if x != 0]
    court = [d for d in win if d in bud and (num(r"tarcza dworu wlaczona: znacznik (\d+)", bud[d], int, 0) or 0) > 0]
    rep.add("E5/A1 A1-T10: tarcze 107b/162m", f"znacznik zoldu != 0 w {len(badh)} z {len(held)} dob; znacznik dworu w 166 w {len(court)} dobach",
            "0 (dzis pod tarcza 2 863 805, L:31686)", (len(badh) == 0 and len(court) == 0) if live else None)

    # ---------------- A1-T11: BK rynek osady
    bk = vals(cau, last28, r"BK rynek osady ([+-]?\d+)")
    m11 = st.mean(bk.values()) if bk else None
    rep.add("E5/A1 A1-T11: BK rynek osady (sr28)", f"{m11:.0f}/d" if m11 is not None else "brak linii", "|x| <= 2 000/d (dzis -34 610 L:31718, sr28 -30 495)",
            (abs(m11) <= 2000) if (m11 is not None and live) else None)

    # ---------------- A1-T12: dar startowy (linia raz; dzien = najblizsza linia 169c po niej - ten sam blok)
    trims = []
    for i, s in enumerate(lines):
        if s.startswith("TownPurse (E5/A1):") and "dar startowy w kasach miast" in s and "zdjeto" in s:
            day = None
            for s2 in lines[i + 1:i + 4000]:
                if s2.startswith("Kasy miast (169c): dzien"):
                    day = num(r"dzien (\d+)", s2)
                    break
            trims.append((s, day))
    if not trims:
        rep.add("E5/A1 A1-T12: dar startowy", "brak linii przyciecia", "nowa kampania: 1 linia, zdjeto 8-20 mln; zapis z doby 362: przyciecie 0", None)
    else:
        for s, day in trims:
            cut = num(r"zdjeto (\d+)", s, int, 0) or 0
            old = "zapis z" in s or "przyciecie wlaczone" in s
            sink = num(r"dar startowy kas miast przyciety \(E5/A1\) (\d+)", cau.get(day, "")) if day is not None else None
            if old:
                rep.add("E5/A1 A1-T12: dar startowy (stary zapis)", f"zdjeto {cut}", "0 (0.75^wiek)", cut == 0)
            else:
                okc = 8000000 <= cut <= 20000000
                if sink is None:
                    rep.add("E5/A1 A1-T12: dar startowy (nowa kampania)", f"zdjeto {cut}; ujscie w przyczynach: brak (pierwsza doba ksiegi bez linii)",
                            "zdjeto 8-20 mln ([S] ok. 14 mln); ujscie = zdjeto +-1", okc)
                else:
                    rep.add("E5/A1 A1-T12: dar startowy (nowa kampania)", f"zdjeto {cut}, ujscie {sink}", "zdjeto 8-20 mln ([S] ok. 14 mln); ujscie = zdjeto +-1",
                            okc and abs(sink - cut) <= 1)
        if len(trims) > 1:
            rep.add("E5/A1 A1-T12: linie przyciecia w logu", len(trims), "1 na kampanie (kolejne sesje: flaga arm_townpurse)", None)

    # ---------------- A1-T13: reszta ksiegi
    r28 = vals(cau, win, r"srednia 28 dob ([+-]?\d+)")
    if r28:
        dl = sorted(r28)[-1]
        rep.add("E5/A1 A1-T13: reszta ksiegi R (srednia 28 dob, ostatnia doba)", f"{r28[dl]} (doba {run_day(dl, start)})", "|R| < 10 000 (etap 3; dzis sr28 -1 302)", abs(r28[dl]) < 10000)
    else:
        rep.add("E5/A1 A1-T13: reszta ksiegi R", "brak linii", "|R| < 10 000", None)

    # ---------------- A1-T14: nasze zakupy z pasa ponizej zapasu
    # poprawka po recenzji: E5/0 liczy jako "nasze sciezki" kazda ujemna kwote kasy miasta z MoneyLedger.Note/Note169 - takze sciezki swiadomie
    # bez BuyFloor (likwidacja RecruitKit, zwrot 171 GarrisonCarts, zwrot dokupu BK CaravanBulk, wedrowcy 36b). Prog 0 (NIE) tylko, gdy linia E5/0
    # podaje je osobno ("zwroty i likwidacje" - do dopisania przy scaleniu, CHANGELOG A1 "Scalanie"); bez rozbicia - INFO
    v14 = vals(e50, win, r"nasze sciezki (\d+)")
    if v14:
        b14 = [d for d, x in v14.items() if x != 0]
        split14 = any("zwroty i likwidacje" in (e50.get(d) or "") for d in v14)
        if split14:
            rep.add("E5/A1 A1-T14: nasze zakupy kas miast ponizej zapasu (E5/0, bez zwrotow i likwidacji)", f"niezerowe w {len(b14)} z {len(v14)} dob", "0 (BuyFloor)",
                    (len(b14) == 0) if live else None)
        else:
            rep.add("E5/A1 A1-T14: nasze sciezki kas miast ponizej zapasu (E5/0 bez rozbicia)", f"niezerowe w {len(b14)} z {len(v14)} dob, suma {sum(v14.values())}",
                    "INFO - E5/0 liczy tu tez sciezki bez BuyFloor (RecruitKit, zwrot 171, zwrot CaravanBulk, wedrowcy 36b); 0 (NIE) po rozbiciu przy scaleniu", None)
    else:
        q = vals(seg, win, r"zapytan (\d+), przy zapasie \(0\)")
        z = vals(seg, win, r"zapytan \d+, przy zapasie \(0\) (\d+)")
        rep.add("E5/A1 A1-T14: nasze zakupy kas miast ponizej zapasu", f"bez linii E5/0; zapytan {sum(q.values())}, przy zapasie (0) {sum(z.values())}",
                "INFO (z budowy: BuyFloor) - 0 z linii E5/0 po scaleniu", None)

    # ---------------- A1-T15/T17: stan ustalony kas i zawor razem
    sp = vals(seg, last28, r"\| zapas kupcow \d+, ponad zapasem (\d+)")
    d28 = [vz[d][0] for d in last28 if d in vz]
    if sp and d28:
        m15 = st.mean(sp.values()); m17 = st.mean(d28)
        rep.add("E5/A1 A1-T15: kasy ponad zapasem wobec zawor / 0.07 (sr28)", f"{m15:.0f} wobec {m17 / 0.07:.0f} (stosunek {m15 / (m17 / 0.07):.2f})" if m17 > 0 else f"{m15:.0f}",
                "INFO (ok. 1 = stan ustalony; P111)", None)
        rep.add("E5/A1 A1-T17: zawor razem (sr28)", f"{m17:.0f}/d", "INFO 250-550 tys./d ([S] ok. 381 tys. = panom 254 + koronie 127; renta dzis 487 312)", None)
    else:
        rep.add("E5/A1 A1-T15/T17: stan kas i zawor (sr28)", "brak liczb", "INFO", None)

    # ---------------- A1-T16: korona (165)
    k16 = vals(kor, last28, r"prawa trzecich \(178\) (\d+)")
    rep.add("E5/A1 A1-T16: korona - wplywy \"nasze\" (165, sr28)", f"{st.mean(k16.values()):.0f}/d" if k16 else "brak linii",
            "INFO (w wojnie >= bieg bazowy - prog B2; dzis 256 287/d)", None)

    # ---------------- A1-T18: rody
    rep.add("E5/A1 A1-T18: rody (glowy, bankruci, wojsko)", "--grupa etap2 na tym samym logu", "progi etapu 2 ([S] rody ok. -89 tys./d bez A3)", None)

    # ---------------- A1-T19/T21: zakup plonu i zaulki (Jeff 16:10)
    uns = vals(pv, win, r"towar niesprzedany po wizycie: w miastach (\d+) szt")
    sold = vals(tow, win, r"za (\d+) szt\.")
    su, ss = sum(uns.values()), sum(sold.values())
    share = (100.0 * su / ss) if ss > 0 else None
    rep.add("E5/A1 A1-T19: towar wsi niesprzedany w miastach", f"{su} szt. wobec sprzedanych {ss} ({share:.3f}%)" if share is not None else f"{su} szt. (brak linii Towary (utarg wozow))",
            "< 0.1% (dzis 0 szt.)", (share < 0.1) if (share is not None and live) else None)
    zre = re.compile(r"zaulki: kas ponizej zapasu (\d+), ponizej 1000 (\d+), przy 0 (\d+); najdluzej ponizej zapasu (\d+) dob")
    zl = {}
    for d in win:
        s = seg.get(d)
        m = zre.search(s) if s else None
        if m:
            zl[d] = [int(x) for x in m.groups()]
    w7 = vals(seg, win, r"miast >= 7 dob (\d+)")
    if zl:
        zero_days = [d for d, v in zl.items() if v[2] > 0]
        dl = sorted(zl)[-1]
        ok21 = len(zero_days) == 0 and (share is None or share <= 0.1)
        rep.add("E5/A1 A1-T21: zaulki (Jeff 16:10)", f"kasy przy 0 w {len(zero_days)} dobach; ostatnia doba: ponizej zapasu {zl[dl][0]}, ponizej 1000 {zl[dl][1]}, przy 0 {zl[dl][2]},"
                f" najdluzsza seria {max(v[3] for v in zl.values())} dob, miast >= 7 dob {w7.get(dl, '-')}; towar niesprzedany " + (f"{share:.3f}%" if share is not None else "-"),
                "kasy przy 0: 0 kazdej doby; towar wsi niesprzedany <= 0.1% (serie > 7 dob z towarem niesprzedanym w tym samym miescie - bez licznika na miasto, INFO);"
                " przekroczenie -> bieg z Town Purse Regulator 1 i raport dla Jeffa (dzis 15 z 97 ponizej celu)", ok21 if live else None)
    else:
        rep.add("E5/A1 A1-T21: zaulki", "brak liczb", "jw.", None)

    # ---------------- A1-T22: udzial zysku kupcow (INFO)
    u = {}
    for d in win:
        s = seg.get(d)
        m = re.search(r"udzial zysku kupcow .*? dzis (-|[\d.]+)%.*?srednia 28 dob (-|[\d.]+)%", s) if s else None
        if m:
            u[d] = m.groups()
    if u:
        dl = sorted(u)[-1]
        dv = [float(x[0]) for x in u.values() if x[0] != "-"]
        rep.add("E5/A1 A1-T22: udzial zysku kupcow zabierany przez pana i korone", f"srednia 28 dob {u[dl][1]}% (ostatnia doba); srednia dziennych {st.mean(dv):.1f}%" if dv else f"srednia 28 dob {u[dl][1]}%",
                "INFO (odniesienie historyczne ok. 10%; decyzja Jeffa po tescie)", None)
    else:
        rep.add("E5/A1 A1-T22: udzial zysku kupcow", "brak liczb", "INFO", None)

    # ---------------- A1-T20: zakupy dla nas - towar zostaje u sprzedajacych (INFO wobec biegu bazowego)
    # poprawka po recenzji: wedrowcy 36b bez BuyFloor (reszta ladunku szlaby na polke celu bez zaplaty); doszly warsztaty zbrojne (WorkshopLaw - "brak zlota/kupca")
    names = ("Paser: dzien", "Kontrakty surowca (174): dzien", "Karawany bez amunicji (172b): dzien", "Warsztaty: dzien")
    have = [n.split(":")[0] for n in names if any(s.startswith(n) for s in lines)]
    q = vals(seg, win, r"zapytan (\d+), przy zapasie \(0\)")
    z = vals(seg, win, r"zapytan \d+, przy zapasie \(0\) (\d+)")
    wsk = vals(by_day(lines, "Warsztaty: dzien"), win, r"brak zlota/kupca (\d+)")
    rep.add("E5/A1 A1-T20: zakupy dla nas - towar u sprzedajacych", f"linie: {', '.join(have) if have else '-'}; BuyFloor: zapytan {sum(q.values())}, przy zapasie (0) {sum(z.values())}"
            + (f"; warsztaty zbrojne - brak zlota/kupca sr {st.mean(wsk.values()):.1f}/d (baza z31-A 0 kazdej doby)" if wsk else ""),
            "INFO wobec biegu bazowego (liczniki \"brak kasy miasta\"); skok x3 -> wylaczyc Town Pays From Surplus i powtorzyc", None)
def etap5_a2(rep, lines, start, until, logp, bud):
    """E5/A2 (162, PLAN 5.2 - dwor pelny, R8): projekt rozdz. 3 "E5/A2" Test: dwor rosnie o przelew ze sprzetu, nic w nicosc, gracz bez dworu,
    D staly bez zmian definicji; ryzyko (rozdz. 3): wiecej zlota do kas siedzib - udzial panow zamkow (prog B3) jako INFO. Przelew zaczyna sie
    po AiGearDaysCap (30) dobach od startu kampanii (przydzial musi dojsc do pulapu) - progi przelewu w dobach 31-until.
    Recenzja A2 (uwaga 1): A2-10/A2-11 - sprzet wplacony do kasy WLASNEJ siedziby wraca jako "wlasne" (zapas w dopisku linii "D staly (169c)")."""
    obg = by_day(lines, "Obieg: dzien")
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    win = [d for d in sorted(bud) if 1 <= run_day(d, start) <= until]
    late = [d for d in win if run_day(d, start) >= 31]
    a2re = re.compile(r"-> dwor \(E5/A2\): przeterminowane dzis (\d+) u (\d+) rodow, wplacone do kas siedzib (\d+) u (\d+) rodow \(miasta (\d+) / zamki (\d+)\)"
                      r"; zostalo w kiesie glowy: ponizej rezerwy rodu \(wojna R, pokoj \d+ D\) (\d+), ustapilo zoldowi (\d+), kiesa glowy \(zold dnia, podloga\) (\d+)"
                      r", dluznicy \(polowa\) (\d+), najemnicy bez dworu (\d+), bez siedziby (\d+); udzial ludu \(hak B1\) (\d+)%")
    obre = re.compile(r"dwor \(166/162m\) do kas siedzib (\d+) \(w tym miasta pod tarcza dworu (\d+), zamki (\d+); w tym sprzet niewydany -> dwor \(E5/A2\) (\d+)"
                      r": miasta (\d+) / zamki (\d+)\)")
    rows = {}
    for d in win:
        m = a2re.search(bud[d])
        if m:
            rows[d] = [int(x) for x in m.groups()]
    off = [d for d in win if "-> dwor (E5/A2): wylaczone" in bud[d]]
    miss = [d for d in win if d not in rows and d not in off]
    rep.add("E5/A2 A2-1: linia sprzetu niewydanego codziennie", f"brak/nieczytelna w {len(miss)} z {len(win)} dob" + (f", wylaczone w {len(off)}" if off else ""),
            "0 (dopisek linii Budzet rodow (166))", len(miss) == 0 if win else None)
    if off:
        rep.add("E5/A2 A2-2: paczka czynna", f"wylaczone w {len(off)} dobach (Court Gets Lapsed Arms Share / Household Minimal)", "czynna w biegu zbioru A - progi przelewu INFO", None)

    def mean_late(i):
        v = [rows[d][i] for d in late if d in rows]
        return st.mean(v) if v else None
    paid = mean_late(2)
    rep.add("E5/A2 A2-3: sprzet niewydany -> dwor (srednia dob 31-%d)" % until,
            f"{paid:.0f}/d (przeterminowane {mean_late(0):.0f}/d u {mean_late(1):.0f} rodow; miasta {mean_late(4):.0f} / zamki {mean_late(5):.0f})" if paid is not None else "brak dob >= 31",
            "> 0 (bieg z31-A: przydzial 516 615 wobec 35 155 wydanych w d120 - oczekiwane ok. 300-480 tys./d [S])", (paid > 0) if (paid is not None and not off) else None)
    # A2-4: dwor rosnie o przelew - ta sama liczba w linii 166 i w Obiegu tej doby; dwor razem >= sprzet; miasta + zamki = sprzet
    bad, noobg = [], 0
    for d in win:
        if d not in rows:
            continue
        if d not in obg:   # pierwsza doba sesji: ksiega pisze sam stan, bez linii "Obieg"
            noobg += 1
            continue
        r = rows[d]
        tot = num(r"dwor \(162m\): wplacone do kas siedzib (\d+)", bud[d])
        o = obre.search(obg[d])
        ok = (tot is not None and tot >= r[2] and r[4] + r[5] == r[2] and o is not None and int(o.group(4)) == r[2] and int(o.group(1)) == tot
              and int(o.group(5)) == r[4] and int(o.group(6)) == r[5])
        if not ok:
            bad.append(run_day(d, start))
    rep.add("E5/A2 A2-4: dwor rosnie o przelew ze sprzetu (166 = Obieg, dwor >= sprzet, miasta + zamki)",
            f"niezgodne {len(bad)} z {len(rows) - noobg} dob" + (f" (doby {bad[:6]})" if bad else "") + (f"; bez linii Obieg {noobg} (pierwsza doba sesji)" if noobg else ""),
            "0", (len(bad) == 0) if len(rows) > noobg else None)
    # A2-5: nic w nicosc - reszta ksiegi pieniadza (przelew glowa -> kasa siedziby w naszym ticku, pozycja MBudget)
    cd = [d for d in sorted(cau) if 1 <= run_day(d, start) <= until]
    rv = num(r"srednia 28 dob ([+-]?\d+)", cau[cd[-1]]) if cd else None
    rep.add("E5/A2 A2-5: nic w nicosc - reszta ksiegi R (srednia 28 dob, ostatnia doba okna)", f"{rv}" if rv is not None else "brak linii", "|R| < 10 000 (prog etapu 3; --grupa etap3)",
            (abs(rv) < 10000) if rv is not None else None)
    # A2-6: glowy rodow < 5 000 (przelew z kiesy glowy najwyzej do podlogi rodziny) - warunek etapu 2
    heads = [num(r"glowy < 5000 \(rody z budzetem\): (\d+)", bud[d]) for d in win]
    heads = [x for x in heads if x is not None]
    rep.add("E5/A2 A2-6: glowy rodow < 5 000 (najwiecej w oknie)", f"{max(heads)}" if heads else "brak linii", "<= 10 (warunek etapu 2)", (max(heads) <= 10) if heads else None)
    # A2-7: hak B1 = 0 do kiesy ludu; A2-8: gracz bez dworu (Eligible - gracz bez budzetu; linia 166 to wypisuje)
    folk = [rows[d][12] for d in rows]
    b1 = any(x.startswith("Kiesa ludu (E5/B1): CZYNNA") for x in lines)   # E5/B1: hak czynny - CourtFolkShare (20%) przy kiesie czynnej
    if b1:
        rep.add("E5/A2 A2-7: udzial ludu w dworze (hak B1)", f"najmniej {min(folk)}%, najwiecej {max(folk)}%" if folk else "brak",
                "E5/B1 czynne: Court Folk Share (domyslnie 20%) w dobach kiesy czynnej (INFO - 0% w dobach przed CZYNNA)", None)
    else:
        rep.add("E5/A2 A2-7: udzial ludu w dworze (hak B1)", f"najwiecej {max(folk)}%" if folk else "brak", "0% do B1 (TownFolkLive)", (max(folk) == 0) if folk else None)
    pl = sum(1 for d in win if "gracz - bez budzetu" in bud[d])
    rep.add("E5/A2 A2-8: gracz bez dworu", f"linia 'gracz - bez budzetu' w {pl} z {len(win)} dob", "kazda doba ([D] 08.10)", (pl == len(win)) if win else None)
    # A2-9: bledy i potkniecia paczki (licznik potkniec - na sesje)
    errs = [s for s in lines if s.startswith("ERROR in ") and "CourtFull" in s]
    stum = max([num(r"potkniecia E5/A2 (\d+)", bud[d], int, 0) or 0 for d in win] or [0])
    rep.add("E5/A2 A2-9: bledy CourtFull / potkniecia E5/A2", f"bledy {len(errs)}, potkniecia {stum}", "0 / 0", len(errs) == 0 and stum == 0)
    # A2-10 / A2-11 (recenzja A2, uwaga 1): sprzet wplacony do kasy WLASNEJ siedziby wraca zaworem jako "wlasne", nie "ziemia" (zapas CourtFull.Own/OwnBack,
    # dopisek linii "D staly (169c)"): zapas zamyka sie z doby na dobe (zapas = wczoraj zapas + wczoraj wplacone - wrocilo - korona i danina - ponad zawor
    # - przyciete - utracone) i po zbudowaniu zapasu (doby 45-until) wraca > 0 jako "wlasne". Recenzja E5/A5: wplaty gracza do kasy wlasnej osady
    # (akcja z menu, poza blokiem doby) wchodza do zapasu przy najblizszej dobie - linia tej doby podaje je osobno ("wplaty gracza ... od wczorajszej linii")
    sdl = by_day(lines, "D staly (169c): dzien")
    owre = re.compile(r"\[E5/A2 zapas wlasny[^\]]*?: wrocilo dzis (\d+), do korony i daniny (\d+), ponad zawor dnia (\d+), przyciete \(osada wydala\) (\d+)"
                      r", utracone \(nowy pan\) (\d+), zapas (\d+) w (\d+) osadach, wplacone dzis (\d+)")
    owlate = re.compile(r"wplaty gracza do kasy wlasnej osady \(E5/A5\) od wczorajszej linii (\d+)")
    ow = {}
    for d in sorted(sdl):
        if 1 <= run_day(d, start) <= until:
            m = owre.search(sdl[d])
            if m:
                ow[d] = [int(x) for x in m.groups()] + [num(owlate, sdl[d], int, 0) or 0]
    if not ow:
        rep.add("E5/A2 A2-10: zapas wlasny (sprzet do wlasnej siedziby) w linii D staly (169c)", "brak dopisku",
                "jest przy czynnej paczce i D stalym (Clan Income Book Stable D)", False if (rows and not off and sdl) else None)
    else:
        bad, pairs, resets = [], 0, 0
        for d in sorted(ow):
            if d - 1 not in ow:
                continue
            y0, r = ow[d - 1], ow[d]
            outs = r[0] + r[1] + r[2] + r[3] + r[4]
            late = r[8]   # recenzja E5/A5: wplaty gracza od wczorajszej linii (w zapasie od dzis)
            exp = y0[5] + y0[7] + late - outs
            # wczytanie zapisu w tej samej sesji (Reset) - zapas od zera (najwyzej wplaty gracza po wczytaniu, przed doba)
            if abs(r[5] + outs - late) <= 5 and (y0[5] > 0 or y0[7] > 0) and abs(r[5] - exp) > 5 + max(0, y0[6] - r[6]):
                resets += 1
                continue
            pairs += 1
            if abs(r[5] - exp) > 5 + max(0, y0[6] - r[6]):   # zaokraglenia 7 liczb linii + reszta < 1 zl kazdej osady, ktora wyszla z zapasu
                bad.append(run_day(d, start))
        rep.add("E5/A2 A2-10: zapas wlasny zamyka sie z doby na dobe (zapas = wczoraj zapas + wplacone - wrocilo - korona - ponad zawor - przyciete - utracone)",
                f"niezgodne {len(bad)} z {pairs} par dob" + (f" (doby {bad[:6]})" if bad else "") + (f"; wczytania (zapas od zera) {resets}" if resets else ""),
                "0 (tolerancja 5 zl + 1 zl na osade, ktora wyszla z zapasu)", (len(bad) == 0) if pairs else None)
        lw = [d for d in ow if run_day(d, start) >= 45]
        if lw:
            back = st.mean(ow[d][0] for d in lw); inn = st.mean(ow[d][7] for d in lw)
            stock = ow[max(lw)][5]
            rep.add("E5/A2 A2-11: sprzet do wlasnych siedzib wraca jako wlasne (srednia dob 45-%d)" % until,
                    f"wrocilo jako wlasne {back:.0f}/d, wplacone do wlasnych siedzib {inn:.0f}/d, zapas na koncu {stock} w {ow[max(lw)][6]} osadach",
                    "> 0, gdy wplacone > 0 (inaczej zwrot wlasnego zlota szedlby do D stalego jako ziemia)", (back > 0) if inn > 0 else None)
            crown = st.mean(ow[d][1] for d in lw); cap = st.mean(ow[d][2] for d in lw); trim = st.mean(ow[d][3] for d in lw); lost = st.mean(ow[d][4] for d in lw)
            share = (100.0 * inn / paid) if paid else 0.0
            rep.add("E5/A2 INFO: zapas wlasny - odplywy poza wlasne i udzial wlasnych siedzib w przelewie (srednia dob 45-%d)" % until,
                    f"korona i danina {crown:.0f}, ponad zawor dnia {cap:.0f}, przyciete {trim:.0f}, utracone {lost:.0f}; do wlasnych siedzib {share:.0f}% przelewu sprzetu",
                    "INFO (recenzja A2: ok. 93% przelewu do wlasnych siedzib - budzet-rodow.csv z31-A d120; zamek 1/3 do korony)", None)
    # INFO: co zostalo w kiesie glowy i dwor bez sprzetu (srednie dob 31-until)
    if late and rows and paid is not None:
        rep.add("E5/A2 INFO: zostalo w kiesie glowy (srednia dob 31-%d)" % until,
                f"ponizej rezerwy {mean_late(6):.0f}, ustapilo zoldowi {mean_late(7):.0f}, kiesa glowy (zold dnia) {mean_late(8):.0f}, dluznicy {mean_late(9):.0f}"
                f", najemnicy {mean_late(10):.0f}, bez siedziby {mean_late(11):.0f}",
                "INFO (regula R8: rezerwa rodu, zold w wojnie, zold dnia w kiesie glowy)", None)
        cv = [num(r"dwor \(162m\): wplacone do kas siedzib (\d+)", bud[d]) for d in late]
        cv = [x for x in cv if x is not None]
        if cv:
            rep.add("E5/A2 INFO: dwor razem / sam dwor (srednia dob 31-%d)" % until, f"{st.mean(cv):.0f} / {st.mean(cv) - paid:.0f}",
                    "INFO (bieg z31-A bez A2: dwor 175-190 tys./d w dobach 31-120, L:31679)", None)
    # INFO (ryzyko - prog B3): udzial panow samych zamkow w D stalym i mediana kies ich glow (budzet-rodow.csv, rodzaj=zamek; wzor e5A/csv.py)
    rws = read_csv(csv_path_of(logp))
    if rws:
        byd = {}
        for x in rws:
            try:
                dz = int(x["dzien"])
            except (ValueError, KeyError, TypeError):
                continue
            if 1 <= run_day(dz, start) <= until:
                byd.setdefault(dz, []).append(x)
        ser = []
        for dz in sorted(byd):
            tot = zam = 0.0
            kies = []
            for x in byd[dz]:
                try:
                    v = float(x.get("d_staly") or "nan")
                except ValueError:
                    continue
                if v != v:
                    continue
                tot += v
                if x.get("rodzaj") == "zamek":
                    zam += v
                    try:
                        kies.append(float(x.get("kiesa_glowy")))
                    except (TypeError, ValueError):
                        pass
            ser.append((dz, zam / tot if tot else 0.0, st.median(kies) if kies else 0.0))
        w = ser[-28:]
        if w:
            ch = (w[-1][2] / w[0][2] - 1) * 100 if w[0][2] else 0.0
            rep.add("E5/A2 INFO: panowie samych zamkow - udzial w d_staly, mediana kiesy glowy (ostatnie 28 dob okna)",
                    f"{st.mean(x[1] for x in w) * 100:.1f}%; mediana {w[0][2]:.0f} -> {w[-1][2]:.0f} ({ch:+.1f}%)",
                    "INFO - ryzyko A2 (wiekszy zawor kas siedzib); prog w B3; bieg z31-A d93-120: 24.3%, -2.5%", None)
    else:
        rep.add("E5/A2 INFO: panowie samych zamkow (budzet-rodow.csv)", "brak pliku CSV", "INFO", None)


def etap5_a3(rep, lines, start, until, mar, nob):
    """E5/A3 (163 + 164c, PLAN 5.3 - markietani R9, notable R10): projekt rozdz. 3 "E5/A3" Test i rozdz. 4 (sakiewki <= 15 dni zoldu).
    Linie: "Markietani (E5/A3): dzien N" (dzien gry - jak 166; mar ma klucz = dzien ksiegi), "Notable (E5/A3): dzien N" (dzien ksiegi), "Obieg: dzien"
    (sakiewki, zold), "Obieg: notable i karawany" (pasmo, nowi notable, karawany), "Pieniadz swiata (bilans - przyczyny)" (korekta zrodel E5/A3).
    Doba 1 kampanii to sam stan ksiegi (bez Obiegu) - progi przeplywow od doby 2."""
    obg = by_day(lines, "Obieg: dzien")
    onk = by_day(lines, "Obieg: notable i karawany: dzien")
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    mwin = [d for d in sorted(mar) if 1 <= run_day(d, start) <= until]
    mre = re.compile(r"stawka ([\d.]+)% \(pelna ([\d.]+)%; ([^)]*)\) \| w polu (\d+) partii, wydaly (\d+): z sakiewek (\d+) \(gracz (\d+)\) -> kasy miast (\d+) w (\d+) miastach"
                     r" \(([\d.]+)%\), kiesy wsi (\d+) w (\d+) wsiach \| we wlasnych miastach rodow \(K3\) (\d+) \| bez niewrogiego miasta -> najblizsze miasto (\d+) partii \((\d+)\)"
                     r" \| bez niewrogiej wsi w stanie Normal -> miasto (\d+) partii \((\d+)\)")
    rows, off = {}, []
    for d in mwin:
        s = mar[d]
        if "| wylaczone (Sutlers Enabled" in s:
            off.append(d)
            continue
        m = mre.search(s)
        if m:
            g = m.groups()
            rows[d] = {"rate": float(g[0]), "full": float(g[1]), "ramp": g[2], "n": int(g[3]), "paid": int(g[4]), "tot": int(g[5]), "pl": int(g[6]),
                       "town": int(g[7]), "towns": int(g[8]), "vill": int(g[10]), "vills": int(g[11]), "k3": int(g[12]), "nt": int(g[13]), "ntg": int(g[14]),
                       "nv": int(g[15]), "nvg": int(g[16]), "ms": num(r"koszt ([\d.]+) ms", s, float, 0.0) or 0.0,
                       "days": num(r"sakiewki partii rodow \d+ = ([\d.]+) dni zoldu", s, float),
                       "kdays": num(r"w tym oszczednosci K1 \d+ = ([\d.]+) dni", s, float)}
    miss = [d for d in mwin if d not in rows and d not in off]
    rep.add("E5/A3 A3-1: linia Markietani (E5/A3) codziennie", f"brak/nieczytelna w {len(miss)} z {len(mwin)} dob" + (f", wylaczone w {len(off)}" if off else ""),
            "0 (LogEnabled; paczka czynna w biegu zbioru A)", (len(miss) == 0) if mwin else None)
    # A3-2 (rozdz. 4): sakiewki zolnierzy <= 15 dni zoldu - "Obieg": stan sakiewek / (zold partii + zold zalog) naliczony tej doby; ostatnia doba okna
    od = [d for d in sorted(obg) if 2 <= run_day(d, start) <= until]
    sd = None
    if od:
        o = obg[od[-1]]
        purse, zp, zz = num(r"sakiewki ludzi \[P\]: stan (\d+)", o), num(r"zold partii (\d+)", o), num(r"zold zalog (\d+)", o)
        if purse is not None and zp and zz is not None:
            sd = purse / float(zp + zz)
    ramp_on = any(r["ramp"].startswith("rozruch") for r in rows.values())
    lastm = rows[mwin[-1]] if mwin and mwin[-1] in rows else None
    rep.add("E5/A3 A3-2: sakiewki <= 15 dni zoldu (Obieg: stan / zold partii i zalog, ostatnia doba okna)",
            (f"{sd:.1f} dni" if sd is not None else "brak linii Obieg") + (f"; partie rodow (linia markietanow) {lastm['days']:.1f} dni" if lastm and lastm["days"] is not None else "")
            + (f", w tym oszczednosci K1 {lastm['kdays']:.1f} dni (markietani ich nie ruszaja)" if lastm and lastm["kdays"] is not None else "")
            + ("; rozruch w biegu (stary zapis)" if ramp_on else ""),
            "<= 15 (oczekiwane 5-12 [S]; bieg z31-A bez A3: d40 12.8, d120 29.3)", (sd <= 15.0) if (sd is not None and rows and not off) else None)
    late = [d for d in mwin if d in rows and run_day(d, start) >= 2]
    if late:
        tot = sum(rows[d]["tot"] for d in late) / len(late)
        town = sum(rows[d]["town"] for d in late)
        vill = sum(rows[d]["vill"] for d in late)
        share = 100.0 * town / (town + vill) if (town + vill) else 0.0
        rep.add("E5/A3 A3-3: markietani wydaja w polu (srednia dob 2-%d)" % until,
                f"{tot:.0f}/d (do kas miast {town / len(late):.0f}, do kies wsi {vill / len(late):.0f}; partii w polu {st.mean(rows[d]['n'] for d in late):.0f}"
                f", wydalo {st.mean(rows[d]['paid'] for d in late):.0f})",
                "> 0 (rachunek [S]: ok. 100-190 tys./d, ile dzis przybywa sakiewkom)", tot > 0)
        full = rows[late[-1]]["full"]
        rep.add("E5/A3 A3-4: podzial miasto / wies (Sutler Town Share)", f"miasta {share:.1f}% (czesc wsi bez odbiorcy -> miasto: {sum(rows[d]['nvg'] for d in late)})",
                "85% (+- 1 pp; wyzej tylko o czesc bez niewrogiej wsi w stanie Normal)", (84.0 <= share <= 100.0) if (town + vill) else None)
        rep.add("E5/A3 A3-5: markietani bez niewrogiego miasta -> najblizsze miasto (licznik)",
                f"partii {sum(rows[d]['nt'] for d in late)}, zl {sum(rows[d]['ntg'] for d in late)} w {len(late)} dobach",
                "INFO (regula R9: brak odbiorcy - kasa najblizszego miasta, licznik)", None)
        rep.add("E5/A3 A3-6: K3 - markietani we wlasnych miastach rodow (srednia)", f"{st.mean(rows[d]['k3'] for d in late):.0f}/d; gracz razem {sum(rows[d]['pl'] for d in late)}",
                "INFO (ten sam licznik co OnLeft: CrownIncome.NoteOwnSpend; gracz = AI)", None)
        ms = [rows[d]["ms"] for d in late]
        rep.add("E5/A3 A3-7: koszt petli markietanow (srednia / najwiecej)", f"{st.mean(ms):.1f} / {max(ms):.1f} ms", "<= 20 ms/dobe (projekt rozdz. 5.2 pkt 3 [S])",
                st.mean(ms) <= 20.0)
        rr = [d for d in late if rows[d]["ramp"].startswith("rozruch")]
        rep.add("E5/A3 INFO: stawka i rozruch", f"stawka pelna {full:.1f}%; doby rozruchu w oknie {len(rr)}"
                + (f" (stawka {rows[rr[0]]['rate']:.1f}% -> {rows[rr[-1]]['rate']:.1f}%)" if rr else ""),
                "INFO (rozruch tylko w zapisie z sakiewkami > 15 dni zoldu: od 1% do pelnej w Sutler Ramp Days)", None)
    ru = [s for s in lines if s.startswith("Markietani (E5/A3): rozruch")]
    rep.add("E5/A3 INFO: decyzja rozruchu (raz na kampanie)", ru[-1][:200] if ru else "brak linii", "INFO (nowa kampania: bez rozruchu)", None)
    # notable (164c): pasmo i nowi notable bez zlota z niczego / w nicosc, kapital karawan z kies i kas (= korekta zrodel), kontrolki prog 0
    nwin = [d for d in sorted(nob) if 2 <= run_day(d, start) <= until]
    kwin = [d for d in sorted(onk) if 2 <= run_day(d, start) <= until]
    if kwin:
        bo = sum(abs(num(r"pasmo 4500-10500: nadwyzka w nicosc ([-+]?\d+)", onk[d], int, 0) or 0) for d in kwin)
        bi = sum(abs(num(r"dosypka z niczego ([-+]?\d+)", onk[d], int, 0) or 0) for d in kwin)
        nn = sum(abs(num(r"nowi notable ([-+]?\d+)", onk[d], int, 0) or 0) for d in kwin)
        rep.add("E5/A3 A3-8: pasmo notabli - w nicosc / z niczego (Obieg, doby 2-%d)" % until, f"{bo} / {bi}",
                "0 / 0 (bieg z31-A bez A3: ok. -107 tys. / +69 tys. na dobe)", (bo == 0 and bi == 0) if nwin else None)
        rep.add("E5/A3 A3-9: nowi notable z niczego (Obieg, doby 2-%d)" % until, f"{nn}", "0 (po starcie gry; tworzenie swiata - bilans otwarcia)",
                (nn == 0) if nwin else None)
    nre = re.compile(r"nadwyzka do kas (\d+) \(notable miast (\d+) / wsi (\d+); zdarzen (\d+)\), brak z kas (\d+) \(notable miast (\d+) / wsi (\d+); zdarzen (\d+), nalezne (\d+)\)"
                     r", uciete (\d+) \(kasa bez nadwyzki u (\d+) notabli - sila nie zeszla (\d+) pkt")
    cre = re.compile(r"kapital karawan notabli[^:]*: (\d+) karawan, gra dalaby (\d+), z kies notabli (\d+), z kas miast startu (\d+), uciete ([-\d]+) \(bez kapitalu (\d+)\)")
    kre = re.compile(r"bez kasy handlu - jak w grze \(prog 0\): zdarzen (\d+), w nicosc (\d+), z niczego (\d+) \| kontrola \(prog 0\): zdarzenia w kontekscie niedopasowane (\d+)"
                     r", stos przepelniony (\d+), karawana bez odczytu modelu (\d+)")
    nb, cb, kb, bad_corr, nofix = [], [], [], [], 0
    for d in nwin:
        s = nob[d]
        m, c, k = nre.search(s), cre.search(s), kre.search(s)
        if m:
            nb.append([int(x) for x in m.groups()])
        if c:
            cv = [int(x) for x in c.groups()]
            cb.append(cv)
            corr = num(r"minus kapital karawan notabli z kies notabli i kas miast startu \(E5/A3\) (\d+)", cau.get(d, ""))
            if corr is None:
                nofix += 1
            elif corr != cv[2] + cv[3]:
                bad_corr.append(run_day(d, start))
        if k:
            kb.append([int(x) for x in k.groups()])
    if not nwin:
        rep.add("E5/A3: linia Notable (E5/A3)", "brak w logu", "jest od wgrania E5/A3 (LogEnabled)", None)
    else:
        if nb:
            rep.add("E5/A3 INFO: pasmo przez kasy miast handlu (suma dob 2-%d)" % until,
                    f"do kas {sum(x[0] for x in nb)} (notable miast {sum(x[1] for x in nb)} / wsi {sum(x[2] for x in nb)}), z kas {sum(x[4] for x in nb)}"
                    f" (miast {sum(x[5] for x in nb)} / wsi {sum(x[6] for x in nb)}), nalezne {sum(x[8] for x in nb)}, uciete {sum(x[9] for x in nb)}"
                    f" (notabli ponizej pasma z sila: najwiecej {max(x[10] for x in nb)} na dobe)",
                    "INFO (miasta i wsie osobno - projekt Test A3; uciete = kasa bez nadwyzki, sila nie schodzi)", None)
        # scalenie porcji A (recenzja A3 R2-1 b): przelewy pasma z kasa osady w linii "Notable (E5/0)" = pasmo przez kasy w linii A3 (ta sama doba, +-1 zl;
        # miasta + wsie razem - podzial miasto/wies liczony w obu liniach inaczej: E5/0 wedlug osady notabla, A3 wedlug kasy handlu)
        nt0 = by_day(lines, "Notable (E5/0): dzien")
        ncmp, nbad = 0, []
        for d in nwin:
            m, s0 = nre.search(nob[d]), nt0.get(d)
            if not m or not s0:
                continue
            ko = (num(r"do kasy osady (\d+) /", s0, int, 0) or 0) + (num(r"do kasy osady targowej (\d+) /", s0, int, 0) or 0)
            ki = (num(r"/ z kasy osady (\d+)", s0, int, 0) or 0) + (num(r"/ z kasy osady targowej (\d+)", s0, int, 0) or 0)
            ncmp += 1
            if abs(ko - int(m.group(1))) > 1 or abs(ki - int(m.group(5))) > 1:
                nbad.append(run_day(d, start))
        if ncmp:
            rep.add("E5/A3 + E5/0: pasmo przez kasy - E5/0 \"do kasy / z kasy osady\" = A3 \"do kas / z kas\" (kazda doba)",
                    f"niezgodne {len(nbad)} z {ncmp} dob" + (f" (doby {nbad[:6]})" if nbad else ""), "0 (+-1 zl)", len(nbad) == 0)
        else:
            rep.add("E5/A3 + E5/0: pasmo przez kasy - E5/0 wobec A3", "brak linii Notable (E5/0) w dobach linii A3", "INFO (E5/0 wylaczone - Town Folk Mode 0)", None)
        if cb:
            rep.add("E5/A3 A3-10: kapital karawan notabli z kies i kas = korekta zrodel bilansu (kazda doba)",
                    f"niezgodne {len(bad_corr)} z {len(cb)} dob" + (f" (doby {bad_corr[:6]})" if bad_corr else "") + (f"; bez linii bilansu {nofix}" if nofix else "")
                    + f"; razem karawan {sum(x[0] for x in cb)}, gra dalaby {sum(x[1] for x in cb)}, z kies {sum(x[2] for x in cb)}, z kas {sum(x[3] for x in cb)}"
                    + f", bez kapitalu {sum(x[5] for x in cb)}",
                    "0 (kapital z niczego 0: O17 widzi go jako nadany, korekta zrodel zdejmuje go w calosci)", len(bad_corr) == 0)
        if kb:
            nt = sum(x[0] for x in kb)
            ctrl = sum(x[3] + x[4] + x[5] for x in kb)
            rep.add("E5/A3 A3-11: notable bez kasy handlu (jak w grze) / kontrolki",
                    f"zdarzen {nt} (w nicosc {sum(x[1] for x in kb)}, z niczego {sum(x[2] for x in kb)}); kontrolki {ctrl}", "0 / 0 (prog 0)", nt == 0 and ctrl == 0)
    if kwin:
        def cars(rd):
            ds = [d for d in kwin if run_day(d, start) <= rd]
            return num(r"karawany notabli \[P\]: (\d+) karawan", onk[ds[-1]]) if ds else None
        rep.add("E5/A3 A3-12: karawany notabli (d40 / ostatnia doba okna)", f"{cars(40)} / {cars(until)}",
                ">= 90% biegu bez A3 (z31-A: d40 750, d120 914 - bez bazy tylko odczyt; test po scaleniu z E4/8)", None)
    st0 = [s for s in lines if s.startswith("Notable (E5/A3): start")]
    rep.add("E5/A3 INFO: bilans otwarcia i latki (linia startowa)", st0[0][st0[0].find("| latki"):][:260] if st0 else "brak linii",
            "nowa kampania: notable n > 0 (zloto gry), karawany startowe m > 0; latki BRAK: -", None)
    errs = [s for s in lines if s.startswith("ERROR in ") and ("Sutlers" in s or "NotableGold" in s)]
    stm = max([num(r"potkniecia (\d+)", mar[d], int, 0) or 0 for d in mwin] + [num(r"potkniecia (\d+)", nob[d], int, 0) or 0 for d in nwin] + [0])
    rep.add("E5/A3 A3-13: bledy Sutlers / NotableGold, potkniecia", f"bledy {len(errs)}, potkniecia {stm}", "0 / 0", len(errs) == 0 and stm == 0)
    cd = [d for d in sorted(cau) if 1 <= run_day(d, start) <= until]
    rv = num(r"srednia 28 dob ([+-]?\d+)", cau[cd[-1]]) if cd else None
    rep.add("E5/A3 A3-14: nic w nicosc - reszta ksiegi R (srednia 28 dob, ostatnia doba okna)", f"{rv}" if rv is not None else "brak linii",
            "|R| < 10 000 (prog etapu 3; --grupa etap3)", (abs(rv) < 10000) if rv is not None else None)


def etap5_a4(rep, lines, start, until, obl, bud):
    """E5/A4 (196, PLAN 5.4 - osada oblezona nie placi nic, R22): projekt rozdz. 3 "E5/A4" Test - wplaty z oblezonych osad 0; miasta z zywym panem
    i nadwyzka > 0 bez zaworu 0 ("10 miast" - z definicji A1, prog A1-T9; tu tylko odczyt, gdy dopisek A1 jest w logu); wplaty z kas ponizej zapasu 0
    (z definicji: pobor = min(nalezne, kasa - prog); zamki - prog B-4 max(20 000, zapas kupcow), miasta - zapas kupcow od A1).
    Linia "Oblezone (E5/A4): dzien N" (dzien gry; obl ma klucz = dzien ksiegi): liczniki od poprzedniej linii - renta miast z naszego bloku doby,
    polityki korony z dziennego ticku rodu krola w grze (tylko prawdziwe rozliczenie, bez prognozy ekranu). Zloto nie rusza - mniej przelewow;
    reszta ksiegi - wiersze A3-14 / A1-T13 / --grupa etap3. Doby oczekiwane - z linii "Budzet rodow (166)" (bud, ten sam klucz), a bez niej - z samej linii A4.
    Recenzja A4: wstrzymanie tylko przy kasie prawdziwym pieniadzem (miasta - A1, zamki - 110), inaczej osada placi jak dotad (licznik "kasa pod
    regulatorem gry"); A4-3 czyta KONTROLE niezalezna od bramki (co naprawde zeszlo z kas oblezonych: renta, zawor 110, danina, polityki) -
    "bez wyjasnienia" (ponad to, co bramka jawnie przepuscila) = 0 w kazdej dobie, przy kazdym stanie klucza."""
    win = sorted(set(d for d in obl if 1 <= run_day(d, start) <= until) | set(d for d in bud if 1 <= run_day(d, start) <= until))
    kinds = ("renta miast", "Crown Duty", "War Tax", "Road Tolls")

    def part(s, head):
        i = s.find(head)
        if i < 0:
            return None
        seg = s[i + len(head):]
        j = seg.find(" | ")
        seg = seg if j < 0 else seg[:j]
        out = []
        for k in kinds:
            m = re.search(re.escape(k) + r" (\d+) \((\d+)\)", seg)
            if not m:
                return None
            out.append((int(m.group(1)), int(m.group(2))))
        m = re.search(r"razem (\d+) z (\d+) osad", seg)
        if not m:
            return None
        return out, int(m.group(1)), int(m.group(2))
    rows, off = {}, []
    for d in win:
        s = obl.get(d)
        if s is None:
            continue
        if "| Besieged Pay Nothing: nie" in s:
            off.append(d)
        h = part(s, "| wstrzymane (oblezona osada nie placi - zostaje w jej kasie): ")
        p = part(s, "| zaplacone mimo oblezenia - wylacznik wylaczony: ")
        g = part(s, "| zaplacone mimo oblezenia - kasa pod regulatorem gry (miasta bez A1, zamki bez 110): ")
        m = re.search(r"oblezone teraz: miasta (\d+), zamki (\d+); wsie w rabunku lub spladrowane (\d+)", s)
        f = re.search(r"ponad zapasem kupcow \(D4\): (\d+) z (\d+) zamkow", s)
        c = re.search(r"kontrola \(niezalezna od bramki[^)]*\): renta (\d+), zawor 110 (\d+), danina (\d+), polityki (\d+); razem (\d+) z (\d+) osad"
                      r"; w tym zaplacone jawnie \(wyzej\) (\d+); bez wyjasnienia (\d+) z (\d+) osad", s)
        r = re.search(r"kasa prawdziwym pieniadzem \(wstrzymanie tylko tam\): miasta \(A1\) (tak|nie), zamki \(110\) (tak|nie)", s)
        if h and p and g and m and f and c and r:
            rows[d] = {"held": h, "paid": p, "reg": g, "towns": int(m.group(1)), "castles": int(m.group(2)), "vill": int(m.group(3)),
                       "floor": int(f.group(1)), "castleN": int(f.group(2)), "stum": num(r"potkniecia (\d+)", s, int, 0) or 0,
                       "ctl": [int(c.group(i)) for i in range(1, 10)], "townReal": r.group(1) == "tak", "castleReal": r.group(2) == "tak"}
    miss = [d for d in win if d not in rows]
    rep.add("E5/A4 A4-1: linia Oblezone (E5/A4) codziennie", f"brak/nieczytelna w {len(miss)} z {len(win)} dob" + (f", wylacznik wylaczony w {len(off)}" if off else ""),
            "0 (LogEnabled; paczka czynna w biegu zbioru A)", (len(miss) == 0) if win else None)
    on = [d for d in win if d in rows and d not in off]
    if off:
        rep.add("E5/A4 A4-2: wylacznik Besieged Pay Nothing", f"wylaczony w {len(off)} z {len(win)} dob", "wlaczony caly bieg zbioru A (wylaczony - A4-3 INFO)", None)
    # A4-3 (recenzja): kontrola niezalezna od bramki - co naprawde zeszlo z kas oblezonych osad ponad to, co bramka jawnie przepuscila (kasa pod
    # regulatorem gry) = 0 w kazdej dobie z wlaczonym kluczem (zgubiona bramka po scaleniu, nowa droga poboru bez Holds, zawor 110 / Levies bez
    # warunku oblezenia). Doby z kluczem wylaczonym - INFO: zawor A1 (po scaleniu) placi panu dekretem BK (High 1.3) od czesci pana, wiec w miescie
    # bez krolestwa czesc pana moze przekroczyc kwote bramki (pay) - "bez wyjasnienia" do 30% renty bez bledu.
    if on:
        cs = [sum(rows[d]["ctl"][i] for d in on) for i in range(9)]
        bad = [d for d in on if rows[d]["ctl"][7] > 0]
        rep.add("E5/A4 A4-3: kontrola - zeszlo z kas oblezonych bez wyjasnienia (suma dob z wlaczonym kluczem; niezalezne od bramki)",
                f"bez wyjasnienia {cs[7]} zl (osado-dni {cs[8]}; dob z > 0: {len(bad)}" + (f", pierwsza {bad[0]}" if bad else "") + ")"
                + f" | zeszlo razem {cs[4]} (renta {cs[0]}, zawor 110 {cs[1]}, danina {cs[2]}, polityki {cs[3]}), w tym jawnie przez bramke {cs[6]}",
                "0 (projekt Test A4: wplaty z oblezonych osad 0 - poza jawnie przepuszczonymi przy kasie pod regulatorem gry)", cs[7] == 0 and cs[8] == 0)
    if off:
        ofr = [d for d in off if d in rows]
        co = [sum(rows[d]["ctl"][i] for d in ofr) for i in range(9)]
        rep.add("E5/A4 INFO: kontrola w dobach z wylaczonym kluczem", f"zeszlo {co[4]}, w tym jawnie {co[6]}, bez wyjasnienia {co[7]} (osado-dni {co[8]})",
                "INFO - wszystko placi jak przed paczka; > 0 = zawor 110 / Levies bez warunku oblezenia albo dekret High w miescie bez krolestwa (A1)", None)
    if rows:
        ds = [d for d in win if d in rows]
        rg = [sum(rows[d]["reg"][0][i][0] for d in ds) for i in range(len(kinds))]
        rgn = [sum(rows[d]["reg"][0][i][1] for d in ds) for i in range(len(kinds))]
        tr = len([d for d in ds if rows[d]["townReal"]]); cr = len([d for d in ds if rows[d]["castleReal"]])
        rep.add("E5/A4 A4-3b: zaplacone mimo oblezenia - kasa pod regulatorem gry (suma okna)",
                ", ".join(f"{kinds[i]} {rg[i]} ({rgn[i]})" for i in range(len(kinds)))
                + f" | kasa prawdziwym pieniadzem: miasta (A1) w {tr} z {len(ds)} dob, zamki (110) w {cr} z {len(ds)} dob",
                "INFO - bez A1 oblezone miasta placa jak dotad (regulator gry skasowalby wstrzymane); zbior A po scaleniu z A1 (A1 i 110 czynne): 0", None)
    if off:
        pz = sum(rows[d]["paid"][1] for d in off if d in rows)
        rep.add("E5/A4 INFO: zaplacone mimo oblezenia w dobach z wylaczonym kluczem", f"{pz} zl w {len([d for d in off if d in rows])} dobach",
                "INFO - stan sprzed paczki (renta miast i polityki korony z oblezonych)", None)
    if rows:
        ds = [d for d in win if d in rows]
        held = [sum(rows[d]["held"][0][i][0] for d in ds) for i in range(len(kinds))]
        heldn = [sum(rows[d]["held"][0][i][1] for d in ds) for i in range(len(kinds))]
        tw, cs = [rows[d]["towns"] for d in ds], [rows[d]["castles"] for d in ds]
        rep.add("E5/A4 A4-4: wstrzymane - oblezona osada nie placi (suma okna; zostaje w jej kasie)",
                ", ".join(f"{kinds[i]} {held[i]} ({heldn[i]})" for i in range(len(kinds)))
                + f"; razem {sum(held)} = {sum(held) / len(ds):.0f}/d; osado-dni ze wstrzymaniem {sum(rows[d]['held'][2] for d in ds)}"
                + f" | oblezone przy linii: miasta srednio {st.mean(tw):.2f} (najwiecej {max(tw)}), zamki {st.mean(cs):.2f} (najwiecej {max(cs)})",
                "INFO ([S] z31-A: ok. 1.3 oblezonego miasta na dobe w d93-120 x ok. 5 tys. renty = ok. 6.5 tys./d renty; polityki - tylko krolestwa z nimi)", None)
        # A4-5: drogi "od dawna" - zawor zamkow 110 i zawor A1 pomijaja oblezone (odczyt liczby z ich linii)
        # recenzja scalenia A: linie 110 i 169c maja juz numer doby ksiegi (dzien gry - 1: CastlePurse.Daily, Measure169c.Daily) - bez przesuniecia,
        # jak w etap5_a1; przesuniecie tylko dla linii "Oblezone (E5/A4)" (dzien gry) - inaczej A4-5/6/9 czytaly 110/169c z nastepnego bloku doby
        z110 = by_day(lines, "Zawor zamkow (110): dzien")
        k169 = by_day(lines, "Kasy miast (169c): dzien")
        zc = [num(r"oblezone (\d+), bez pana", z110[d], int) for d in win if d in z110]
        zc = [x for x in zc if x is not None]
        ac = [num(r"bez poboru: kasa nie ponad zapasem \d+, oblezone (\d+)", k169[d], int) for d in win if d in k169]
        ac = [x for x in ac if x is not None]
        rep.add("E5/A4 A4-5: zawor zamkow (110) i zawor kas miast (A1) - oblezone pominiete (suma okna)",
                (f"zamki {sum(zc)} w {len(zc)} dobach" if zc else "zamki: brak liczby w linii 110") + "; "
                + (f"miasta (A1) {sum(ac)} w {len(ac)} dobach" if ac else "miasta: brak dopisku E5/A1 (A1 nie w logu - oblezone miasta placa jak dotad, A4-3b)"),
                "INFO (regula od dawna, bez wylacznika A4; danina i clo - KingdomTreasury.Levies)", None)
        # A4-6: "10 miast bez placenia" - z definicji A1 (prog A1-T9); tu tylko gdy dopisek A1 jest w logu
        v9 = [num(r"miast z nadwyzka bez zaworu \(10 miast\) (\d+)", k169[d], int) for d in win if d in k169]
        v9 = [x for x in v9 if x is not None]
        rep.add("E5/A4 A4-6: miasta z zywym panem i nadwyzka bez zaworu (\"10 miast\")",
                (f"niezerowe w {len([x for x in v9 if x])} z {len(v9)} dob" + (f" (najwiecej {max(v9)})" if any(v9) else "")) if v9 else "brak dopisku E5/A1 w logu",
                "0 (z definicji A1 - zawor od nadwyzki, bez filtra ludnosci; prog A1-T9)", (not any(v9)) if v9 else None)
        # A4-9 (recenzja R1-2, krok scalania 2): zawor A1 przez bramke R22 - bez kroku 2 zawor A1 pomija oblezone przed bramka (nic nie wyplywa,
        # wiec A4-3 tego nie widzi): oblezone w zaworze A1 (_vSiege) > 0, a "wstrzymane: renta miast" 0 w dobach z kluczem i A1
        dd = [d for d in win if d in rows and d in k169 and d not in off and rows[d]["townReal"]]
        if dd:
            vs = sum(num(r"bez poboru: kasa nie ponad zapasem \d+, oblezone (\d+)", k169[d], int, 0) or 0 for d in dd)
            hn = sum(rows[d]["held"][0][0][1] for d in dd)
            rep.add("E5/A4 A4-9: zawor kas miast A1 przez bramke R22 (krok scalania 2)",
                    f"oblezone w zaworze A1 {vs}, wstrzymana renta miast {hn} miasto-dni (w {len(dd)} dobach z kluczem i A1)",
                    "> 0, gdy oblezone > 0 (0 = zawor A1 pomija oblezone przed bramka - krok scalania 2 nie zrobiony)", (hn > 0) if vs > 0 else None)
        last = rows[ds[-1]]
        fl = [rows[d]["floor"] for d in ds]
        rep.add("E5/A4 A4-7: prog podatkow korony zamkow ponad zapasem kupcow (D4)",
                f"ostatnia doba {last['floor']} z {last['castleN']} zamkow (najwiecej {max(fl)})",
                "INFO - B-4 PayFloor = max(20 000, zapas kupcow): zamki z zapasem < 20 000 placa Crown Duty / War Tax od 20 000, nie od D4"
                " (decyzja po tescie A; wplaty z kas ponizej progu - 0 z definicji)", None)
    errs = [s for s in lines if s.startswith("ERROR in ") and "BesiegedPay" in s]
    stm = max([rows[d]["stum"] for d in rows] + [0])
    rep.add("E5/A4 A4-8: bledy BesiegedPay, potkniecia", f"bledy {len(errs)}, potkniecia {stm}", "0 / 0", len(errs) == 0 and stm == 0)
    st0 = [s for s in lines if s.startswith("Oblezone (E5/A4): start")]
    rep.add("E5/A4 INFO: linia startowa", st0[0][st0[0].find("| wylacznik"):][:420] if st0 else "brak linii",
            "wylacznik tak; Population Rent Enabled tak; Policy Income Conserved tak; kasy miast A1 tak (zbior A), Castle Purse Enabled tak, latka regulatora 110 wpieta", None)


def etap5_a5(rep, lines):
    """E5/A5 (197, R23 - BetterEconomy: prawdziwa wplata gracza do kasy osady i dar dla notabli): projekt rozdz. 3 "E5/A5" Test - zbior A + reczne
    sprawdzenie Jeffa (autotest nie klika menu): przelew w ksiedze, nic z niczego. Linia startowa BEE (170) - prefiksy Try* wpiete; linia kampanii
    BEE (170) - dopisek "E5/A5 (prawdziwa wplata i dar dla notabli)"; kazda akcja gracza - linia "BEE (E5/A5) wplata|wplata odmowiona|dar: dzien N | ..."
    ze zlotem gracza i odbiorcy przed i po (gracz traci dokladnie tyle, ile dostaje kasa osady / notable - inaczej dopisek ROZNICA)."""
    st = [x for x in lines if x.startswith("BEE domkniecie (170): BetterEconomy")]
    camp = [x for x in lines if x.startswith("BEE domkniecie (170): kampania")]
    pays = [x for x in lines if x.startswith("BEE (E5/A5) wplata: dzien")]
    refs = [x for x in lines if x.startswith("BEE (E5/A5) wplata odmowiona: dzien")]
    gifts = [x for x in lines if x.startswith("BEE (E5/A5) dar: dzien")]
    labels = ("prawdziwa wplata do kasy miasta (E5/A5)", "prawdziwa wplata do kasy zamku (E5/A5)")
    # A5-1: prefiksy Try* wpiete (linia startowa BEE 170; bez BEE albo inna wersja - INFO)
    if not st:
        rep.add("E5/A5 A5-1: prefiksy Try* wplaty (linia startowa BEE 170)", "brak linii startowej BEE", "jest przy BetterEconomy w grze", None)
    else:
        x = st[-1]
        if "nieobecny" in x or "NIC nie wpiete" in x:
            rep.add("E5/A5 A5-1: prefiksy Try* wplaty (linia startowa BEE 170)", x[:200], "BetterEconomy v1.4.5 obecny (inaczej paczka nieczynna - INFO)", None)
        else:
            m = re.search(r"wpiete (\d+)/(\d+)", x)
            head = x.split(" | BRAK:")[0]
            ok = all(l in head for l in labels)
            rep.add("E5/A5 A5-1: prefiksy Try* wplaty (linia startowa BEE 170)",
                    (f"wpiete {m.group(1)}/{m.group(2)}" if m else "?") + "; " + ", ".join(("tak" if l in head else "NIE") + " - " + l for l in labels)
                    + (" | BRAK: " + x.split(" | BRAK:")[1][:200] if " | BRAK:" in x else ""),
                    "oba wpiete (18/18 przy pelnym 170)", ok)
    # A5-2: dopisek linii kampanii (kazda sesja: start / wczytanie)
    rows = []
    for x in camp:
        i = x.find("E5/A5 (prawdziwa wplata i dar dla notabli): ")
        rows.append(x[i:] if i >= 0 else None)
    if not camp:
        rep.add("E5/A5 A5-2: linia kampanii BEE (170) - dopisek E5/A5", "brak linii kampanii", "jest w kazdej sesji", None)
    else:
        bad, off = [], 0
        for r in rows:
            if r is None:
                bad.append("brak dopisku")
                continue
            if "wylacznik wylaczony" in r:
                off += 1
                continue
            if not ("prefiksy Try*: miasto tak, zamek tak" in r and "okno kwot przy opcjach wplaty 2/2" in r and "opcje daru dla notabli 2/2" in r):
                bad.append(r[:160])
        last = rows[-1] or "brak dopisku"
        rep.add("E5/A5 A5-2: linia kampanii BEE (170) - dopisek E5/A5 (sesje)",
                f"sesji {len(camp)}, niepelnych {len(bad)}" + (f", wylacznik wylaczony w {off}" if off else "") + " | ostatnia: " + last[:300],
                "kazda sesja: wylacznik wlaczony, prefiksy Try* tak/tak, okno kwot 2/2, opcje daru 2/2", len(bad) == 0 and off == 0)
        m = re.search(r"kasa prawdziwym pieniadzem: miasta \(A1\) (tak|nie).*?, zamki \(110\) (tak|nie)", last)
        rep.add("E5/A5 A5-3: kasa osady prawdziwym pieniadzem (wplata otwarta tylko tam)",
                (f"miasta (A1) {m.group(1)}, zamki (110) {m.group(2)}" if m else "brak w dopisku") + " | przerwa po darze: "
                + (re.search(r"przerwa po darze: ([^;]+)", last).group(1)[:90] if re.search(r"przerwa po darze: ([^;]+)", last) else "?"),
                "INFO - zbior A po scaleniu z A1: miasta tak, zamki tak (bez A1 wplata do miast zamknieta z powodem - regulator gry)", None)
    # A5-4: wplaty - gracz traci tyle, ile dostaje kasa osady
    if pays:
        diff = [x for x in pays if "| ROZNICA" in x]
        tot = sum(num(r"\| gracz \d+ -> \d+ \(-(\d+)\)", x, int, 0) or 0 for x in pays)
        got = sum(num(r"\| kasa osady \d+ -> \d+ \(\+(-?\d+)\)", x, int, 0) or 0 for x in pays)
        tw = len([x for x in pays if "(miasto)" in x]); cs = len([x for x in pays if "(zamek)" in x])
        rep.add("E5/A5 A5-4: wplaty gracza do kasy osady - gracz traci tyle, ile dostaje kasa",
                f"wplat {len(pays)} (miasta {tw}, zamki {cs}), gracz -{tot}, kasy +{got}, linii z ROZNICA {len(diff)}" + (f" (pierwsza: {diff[0][:160]})" if diff else ""),
                "ROZNICA 0 i suma gracza = suma kas (nic z niczego, nic w nicosc)", len(diff) == 0 and tot == got)
    else:
        rep.add("E5/A5 A5-4: wplaty gracza do kasy osady", "brak linii wplat", "INFO - autotest nie klika menu; reczny test Jeffa: wplata do wlasnego zamku/miasta -> linia z ROZNICA 0", None)
    # A5-4b (recenzja A5): wplata to pieniadze rodu gracza w kasie WLASNEJ osady - cala do zapasu "wlasnego" (CourtFull.OwnPlayer; wraca zaworem jako wlasne
    # D stalego, nie ziemia - inaczej zawyza StableLand gracza: powinnosci 165, limit Banku 168); linia "D staly" doby po wplacie podaje ja w zapasie
    if pays:
        own_re = re.compile(r"\| do zapasu wlasnego (\d+)")
        late_re = re.compile(r"wplaty gracza do kasy wlasnej osady \(E5/A5\) od wczorajszej linii (\d+)")
        bad, off, tot_on, own_on, pend, before, late_sum, sdn = [], 0, 0, 0, 0, 0, 0, 0
        for x in lines:
            if x.startswith("BEE (E5/A5) wplata: dzien"):
                o = num(own_re, x, int, None)
                pd = num(r"\| gracz \d+ -> \d+ \(-(\d+)\)", x, int, 0) or 0
                if o is None:
                    bad.append("brak pola: " + x[:120])
                    continue
                pend += o
                if "(D staly wylaczony)" in x:
                    off += 1
                    continue
                tot_on += pd; own_on += o
                if o != pd:
                    bad.append(x[:160])
            elif x.startswith("D staly (169c): dzien"):
                sdn += 1
                late_sum += num(late_re, x, int, 0) or 0
                before += pend; pend = 0
        close = (late_sum == before) if sdn else None
        rep.add("E5/A5 A5-4b: wplaty do zapasu wlasnego (wraca zaworem jako wlasne, nie ziemia D stalego)",
                f"wplaty {tot_on} -> do zapasu {own_on}, niezgodnych linii {len(bad)}" + (f" (pierwsza: {bad[0]})" if bad else "")
                + (f", przy D stalym wylaczonym {off}" if off else "")
                + (f"; w liniach D staly weszlo do zapasu {late_sum} z {before} wplaconych przed nimi" if sdn else "; brak linii D staly")
                + (f" (po ostatniej linii jeszcze {pend})" if pend else ""),
                "do zapasu = wplata w kazdej linii; w liniach D staly = suma wplat przed nimi (wyjatek: wczytanie zapisu miedzy wplata a doba - ta wplata wypada z zapasu)",
                len(bad) == 0 and close is not False)
    # A5-5: dary - gracz traci tyle, ile dostaja notable
    if gifts:
        diff = [x for x in gifts if "| ROZNICA" in x]
        tot = sum(num(r"\| gracz \d+ -> \d+ \(-(\d+)\)", x, int, 0) or 0 for x in gifts)
        got = sum(num(r"\| notable razem \+(-?\d+)", x, int, 0) or 0 for x in gifts)
        rel = sum(num(r"\| relacja razem \+(-?\d+)", x, int, 0) or 0 for x in gifts)
        nn = sum(num(r"\| notabli (\d+)", x, int, 0) or 0 for x in gifts)
        rep.add("E5/A5 A5-5: dary dla notabli - gracz traci tyle, ile dostaja notable",
                f"darow {len(gifts)}, notabli razem {nn}, gracz -{tot}, notable +{got}, relacja razem +{rel}, linii z ROZNICA {len(diff)}" + (f" (pierwsza: {diff[0][:160]})" if diff else ""),
                "ROZNICA 0 i suma gracza = suma notabli; relacja jak nadplata w barterze (najwyzej ok. +3 na notabla)", len(diff) == 0 and tot == got)
    else:
        rep.add("E5/A5 A5-5: dary dla notabli", "brak linii darow", "INFO - autotest nie klika menu; reczny test Jeffa: dar w miescie i we wsi -> linia z ROZNICA 0", None)
    # A5-6: odmowy (ekrany BK, konsola, menu) - INFO
    why = {}
    for x in refs:
        k = x.split("| powod: ", 1)[1][:70] if "| powod: " in x else "?"
        why[k] = why.get(k, 0) + 1
    rep.add("E5/A5 A5-6: odmowy wplaty (powody)", (f"{len(refs)}: " + "; ".join(f"{k} x{v}" for k, v in sorted(why.items(), key=lambda t: -t[1])[:5])) if refs else "0",
            "INFO - zloto nie schodzi przy odmowie; \"Closed for now\" = kasa pod regulatorem gry (miasta bez A1)", None)
    # A5-7: bledy i potkniecia
    errs = [x for x in lines if x.startswith("ERROR in BeeGifts")]
    stm = max([num(r"potkniecia (\d+)", x, int, 0) or 0 for x in pays + gifts] + [num(r"E5/A5 \(prawdziwa.*?potkniecia (\d+)", x, int, 0) or 0 for x in camp] + [0])
    rep.add("E5/A5 A5-7: bledy BeeGifts, potkniecia", f"bledy {len(errs)}, potkniecia {stm}" + (f" (pierwszy: {errs[0][:160]})" if errs else ""), "0 / 0", len(errs) == 0 and stm == 0)


def main(argv):
    args = argv[1:]
    group, logp, base_in, base_out, csvp, start_arg = None, None, None, None, None, None
    until_arg = 120
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
        if a == "--do-doby":
            until_arg = int(args[i + 1]); i += 2; continue
        logp = a; i += 1
    if group == "etap3" and logp:
        return etap3_main(logp, start_arg)   # 3.1 (31, E3/0)
    if group == "etap4" and logp:
        return etap4_main(logp, start_arg, until_arg)   # E4/0 (188)
    if group == "etap5" and logp:
        return etap5_main(logp, start_arg, until_arg, csvp)   # E5/0 (195): szkielet etapu 5 + blok E5/0; kolejne paczki dopisuja bloki
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
