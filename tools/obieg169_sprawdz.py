# -*- coding: utf-8 -*-
"""
Paczka 169 - KSIEGA OBIEGU: sprawdzian logu autotestu (docs/paczki/169-ksiega-obiegu.md, rozdz. 11, testy T1-T17).

Uzycie (z korzenia repo):
    python tools/obieg169_sprawdz.py <Armoury-*.log> [--csv <budzet-rodow.csv>] [--od 11] [--do 40]

Wypisuje "Tn OK|CZESCIOWO|FAIL|POMINIETY ..." dla kazdego testu i konczy kodem 0 (zero FAIL) albo 1.
Bez zaleznosci (sama biblioteka standardowa). Testy wymagajace innego przebiegu (B, C, D) albo innego narzedzia
(T12 - tools/zapis/napisy.py z repo autotestu) sa POMINIETE z powodem.

Czyta tylko linie glownego logu Armoury ("[HH:MM:SS] tekst"). Numer doby: linie ksiegi pieniadza i obiegu maja
"dzien N" = (int)ToDays - 1; linie modulow bloku (Korona, Zold, IronBank, Ludnosc) maja (int)ToDays - dlatego modul
przypisujemy do bloku po kolejnosci (linie modulow przed "Pieniadz swiata:" tej samej doby), a nie po numerze.
"""
import re
import sys
import csv as csvmod

NUM = r"[-+]?\d+"


def ival(s):
    try:
        return int(s)
    except Exception:
        return None


def kv(text):
    """Segment 'kontrola: A=1 B=-2 ...' -> slownik (wartosc '-' = None)."""
    out = {}
    i = text.rfind("kontrola:")
    if i < 0:
        return out
    for m in re.finditer(r"([A-Za-z0-9_+]+)=([-+]?\d+|-)", text[i:]):
        out[m.group(1)] = None if m.group(2) == "-" else int(m.group(2))
    return out


def find(pat, text, cast=int):
    m = re.search(pat, text)
    if not m:
        return None
    try:
        return cast(m.group(1))
    except Exception:
        return None


class Block(object):
    def __init__(self):
        self.day = None
        self.mod = {}          # linie modulow bloku (przed "Pieniadz swiata:")
        self.lines = {}        # linie ksiegi tej doby


def parse(path):
    blocks = []
    pending = {}
    cur = None
    startup = []
    errors = []
    with open(path, encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.rstrip("\r\n")
            m = re.match(r"^\[\d\d:\d\d:\d\d\] (.*)$", line)
            msg = m.group(1) if m else line
            if " ERROR in " in line or msg.startswith("ERROR in "):
                errors.append(msg)
            if msg.startswith("Obieg (169):"):
                startup.append(msg)
                continue
            for key in ("Ludnosc: dzien", "Korona: dzien", "Zold: dzien", "IronBank: dzien", "Warsztaty towarowe: dzien",
                        "Budzet rodow (na sucho)", "Dlugi (na sucho)", "Budzet rodow: wczytano"):
                if msg.startswith(key):
                    pending.setdefault(key, []).append(msg)
            if msg.startswith("Pieniadz swiata: dzien"):
                cur = Block()
                cur.day = find(r"dzien (\d+)", msg)
                cur.mod = pending
                pending = {}
                cur.lines["stan"] = msg
                blocks.append(cur)
                continue
            if cur is None:
                continue
            for key, tag in (("Pieniadz swiata (bilans): dzien", "bilans"),
                             ("Pieniadz swiata (bilans - przyczyny): dzien", "przyczyny"),
                             ("Pieniadz swiata (rody): dzien", "rody"),
                             ("Pieniadz swiata (rody - przyczyny): dzien", "rodyp"),
                             ("Przeplywy osad (kasy miast): dzien", "kasyM"),
                             ("Przeplywy osad (kasy zamkow): dzien", "kasyZ"),
                             ("Przeplywy osad (kiesy wsi): dzien", "kasyW"),
                             ("Przeplywy osad (przyczyny): dzien", "kasyP"),
                             ("Obieg: dzien", "obieg"),
                             ("Obieg: notable i karawany: dzien", "notable"),
                             ("Obieg: BK i BetterEconomy: dzien", "bk"),
                             ("Obieg: okna (kontrolka): dzien", "okna")):
                if msg.startswith(key):
                    cur.lines[tag] = msg
    return blocks, startup, errors


def res(name, status, text):
    print("%s %s %s" % (name, status, text))
    return status


def in_range(b, lo, hi, first):
    if b.day is None or first is None:
        return False
    rel = b.day - first + 1
    return lo <= rel <= hi


def windows_of(okna):
    """'Obieg: okna (kontrolka)' -> {nazwa: (wywolan, trafien) | None dla BRAK}."""
    out = {}
    if not okna:
        return out
    seg = okna.split(" | ")
    if len(seg) < 2:
        return out
    for part in seg[1].split(", "):
        m = re.match(r"^(\S+) (\d+)/(\d+)$", part.strip())
        if m:
            out[m.group(1)] = (int(m.group(2)), int(m.group(3)))
        else:
            m = re.match(r"^(\S+) .*BRAK", part.strip())
            if m:
                out[m.group(1)] = None
    return out


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    path = argv[1]
    csv_path = None
    lo, hi = 11, 40
    i = 2
    while i < len(argv):
        if argv[i] == "--csv" and i + 1 < len(argv):
            csv_path = argv[i + 1]; i += 2; continue
        if argv[i] == "--od" and i + 1 < len(argv):
            lo = int(argv[i + 1]); i += 2; continue
        if argv[i] == "--do" and i + 1 < len(argv):
            hi = int(argv[i + 1]); i += 2; continue
        i += 1
    blocks, startup, errors = parse(path)
    full = [b for b in blocks if "przyczyny" in b.lines]
    first = blocks[0].day if blocks else None
    results = []
    if not full:
        print("Brak linii 'Pieniadz swiata (bilans - przyczyny)' - ksiega obiegu wylaczona albo log sprzed paczki 169.")
        return 1
    aborted_days = set()
    for b in full:
        ok = kv(b.lines.get("okna", ""))
        m = re.search(r"rozliczenia rodow przerwane wyjatkiem (\d+)", b.lines.get("okna", ""))
        if m and int(m.group(1)) > 0:
            aborted_days.add(b.day)

    # T1 druk: arytmetyka linii
    bad = []
    prev_total = None
    for b in blocks:
        tot = find(r"\| razem (\d+)", b.lines.get("stan", ""))
        if "przyczyny" in b.lines:
            k = kv(b.lines["przyczyny"])
            z = k.get("Z")
            old = find(r"zmiana sumy (" + NUM + r") \[P\]", b.lines.get("bilans", ""))
            if None in (z, k.get("S"), k.get("U"), k.get("N"), k.get("R")):
                bad.append("%s: brak liczb" % b.day)
            elif z - k["S"] + k["U"] - k["N"] - k["R"] != 0 or "ARYTMETYKA OK" not in b.lines["przyczyny"]:
                bad.append("%s: arytmetyka" % b.day)
            elif old is not None and old != z:
                bad.append("%s: Z %d != zmiana sumy %d" % (b.day, z, old))
            elif prev_total is not None and tot is not None and tot - prev_total != z:
                bad.append("%s: Z %d != zmiana razem %d" % (b.day, z, tot - prev_total))
        prev_total = tot
    results.append(res("T1", "OK" if not bad else "FAIL", "druk - arytmetyka linii w %d dobach%s" % (len(full), ("; " + "; ".join(bad[:5])) if bad else "")))

    # T2 druk: refaktor OldBalance i komplet pozycji
    bad = []
    for b in full:
        k = kv(b.lines["przyczyny"])
        r0old = find(r"\+ reszta (" + NUM + r") \[R\]", b.lines.get("bilans", ""))
        if r0old is not None and k.get("R0") != r0old:
            bad.append("%s: R0 %s != stara reszta %s" % (b.day, k.get("R0"), r0old))
            continue
        try:
            zs = sum(k["Z%d" % j] for j in range(1, 6))
            us = sum(k["U%d" % j] for j in range(1, 10))
            if k["R0"] - k["R"] != zs - us + k["N"] - k["K1"]:
                bad.append("%s: R0-R nie zgadza sie z pozycjami" % b.day)
        except Exception:
            bad.append("%s: brak pozycji w kontroli" % b.day)
    results.append(res("T2", "OK" if not bad else "FAIL", "druk - refaktor i komplet pozycji%s" % (("; " + "; ".join(bad[:5])) if bad else "")))

    # T3 rozbicie reszty (CEL)
    rs = [kv(b.lines["przyczyny"]) for b in full if in_range(b, lo, hi, first) and b.day not in aborted_days]
    if not rs:
        results.append(res("T3", "POMINIETY", "brak dob %d-%d" % (lo, hi)))
    else:
        mr = sum(x["R"] for x in rs) / float(len(rs))
        mr0 = sum(x["R0"] for x in rs) / float(len(rs))
        st = "OK" if abs(mr) < 10000 else ("CZESCIOWO" if abs(mr) <= 30000 else "FAIL")
        if abs(mr0) > 0 and abs(mr) > 0.2 * abs(mr0) and st == "OK":
            st = "CZESCIOWO"
        if mr > 10000:
            st = "FAIL"
        results.append(res("T3", st, "srednia R %.0f, srednia R0 %.0f (dob %d; R stale dodatnie > 10000 = FAIL)" % (mr, mr0, len(rs))))

    # T4 rozbicie "w tym"
    bad = []
    for b in full:
        k = kv(b.lines["przyczyny"])
        if k.get("fromW") is None or k.get("from") is None or k["fromW"] > k["from"] or k["toW"] > k["to"]:
            bad.append(str(b.day))
    results.append(res("T4", "OK" if not bad else "FAIL", "inne >= 0%s" % ((" - zle doby: " + ", ".join(bad[:10])) if bad else "")))

    # T5 kasy
    bad = []
    tm1 = []
    for b in full:
        if "kasyP" not in b.lines:
            bad.append("%s: brak linii" % b.day); continue
        k = kv(b.lines["kasyP"])
        for tag, key in (("kasyM", "TM0"), ("kasyZ", "TZ0"), ("kasyW", "TW0")):
            old = find(r"reszta - [^|;]*? (" + NUM + r") \[R\]", b.lines.get(tag, ""))
            if old is not None and k.get(key) != old:
                bad.append("%s: %s %s != %s" % (b.day, key, k.get(key), old))
        if in_range(b, lo, hi, first) and k.get("TM1") is not None:
            tm1.append(abs(k["TM1"]))
    avg = sum(tm1) / float(len(tm1)) if tm1 else 0
    st = "FAIL" if bad else ("OK" if avg < 30000 else "CZESCIOWO")
    results.append(res("T5", st, "kasy: srednia |TM1| %.0f (cel 4.2: < 10000 po 164)%s" % (avg, ("; " + "; ".join(bad[:5])) if bad else "")))

    # T6 kontrolki okien
    must = ["notable-dochod", "karawany-notabli", "pasmo-notabli", "awanse", "werbunek", "prowizja", "lup-z-cial", "nowe-karawany",
            "kapital-karawan", "warsztaty-wyrob", "warsztaty-wsad", "stan-bohatera", "partie-znikaja", "linie-modelu", "porty", "kopalnie",
            "rynek-osady", "konwoje", "kupno-BK", "myto"]
    calls = {}
    hits = {}
    missing = set()
    nocap = 0
    modelbad = []
    for b in full:
        w = windows_of(b.lines.get("okna", ""))
        for n, v in w.items():
            if v is None:
                missing.add(n)
            else:
                calls[n] = calls.get(n, 0) + v[0]
                hits[n] = hits.get(n, 0) + v[1]
        nocap += find(r"nowe karawany bez odczytu kapitalu (\d+)", b.lines.get("okna", "")) or 0
        kr = kv(b.lines.get("rodyp", ""))
        if kr.get("model") is not None and kr.get("rozliczenia") is not None and kr["model"] != kr["rozliczenia"]:
            modelbad.append("%s: %s/%s" % (b.day, kr["model"], kr["rozliczenia"]))
    dead = [n for n in must if n not in missing and calls.get(n, 0) == 0]
    notes = []
    if dead:
        notes.append("0 wywolan: " + ", ".join(dead))
    if missing:
        notes.append("BRAK: " + ", ".join(sorted(missing)))
    if hits.get("kapital-karawan", 0) != hits.get("nowe-karawany", 0):
        notes.append("kapital-karawan %d != nowe-karawany %d" % (hits.get("kapital-karawan", 0), hits.get("nowe-karawany", 0)))
    if nocap:
        notes.append("nowe karawany bez odczytu kapitalu %d" % nocap)
    if modelbad:
        notes.append("linie-modelu != rozliczenia: " + "; ".join(modelbad[:5]))
    st = "FAIL" if (dead or nocap or modelbad or hits.get("kapital-karawan", 0) != hits.get("nowe-karawany", 0)) else "OK"
    results.append(res("T6", st, "okna (BRAK dopuszczalny tylko przy nieobecnym modzie - linia startowa: %s)%s" % (
        "jest" if startup else "BRAK", ("; " + "; ".join(notes)) if notes else "")))

    # T7 wielkosci
    notes = []
    st = "OK"
    for b in full:
        rel = (b.day - first + 1) if first is not None else 0
        t = b.lines.get("notable", "")
        if not t:
            continue
        wage = find(r"zold w nicosc -(\d+)", t)
        nkar = find(r"karawany notabli \[P\]: (\d+) karawan", t)
        short = find(r"niedoplata (" + NUM + r")", t)
        if short is not None and short < 0:
            st = "FAIL"; notes.append("%s: niedoplata %d < 0" % (b.day, short))
        if rel >= 10:
            if wage is not None and not (60000 <= wage <= 220000):
                if st == "OK": st = "CZESCIOWO"
                notes.append("%s: zold karawan notabli %d" % (b.day, wage))
            if nkar is not None and not (500 <= nkar <= 1200):
                if st == "OK": st = "CZESCIOWO"
                notes.append("%s: karawan %d" % (b.day, nkar))
        ws = (b.mod.get("Warsztaty towarowe: dzien") or [""])[-1]
        X = find(r"do notabli z niczego \+(\d+)", ws)
        Y = find(r"z kies notabli w nicosc -(\d+)", ws)
        bin_ = find(r"dosypka z niczego \+(\d+)", t) or 0
        bout = find(r"nadwyzka w nicosc -(\d+)", t) or 0
        inc = find(r"wyplata zgloszona jako z niczego \+(\d+)", t) or 0
        new = find(r"nowi notable \+(\d+)", t) or 0
        if X:
            r = (bin_ + inc + new) / float(X)
            if not (0.90 <= r <= 1.00):
                if st == "OK": st = "CZESCIOWO"
                notes.append("%s: pasmo+dochod+nowi / X = %.2f" % (b.day, r))
        if Y:
            r = bout / float(Y)
            if not (0.90 <= r <= 1.00):
                if st == "OK": st = "CZESCIOWO"
                notes.append("%s: pasmo out / Y = %.2f" % (b.day, r))
        if 31 <= rel <= 40:
            if not (0.8 * 578000 <= bin_ + inc + new <= 1.2 * 578000):
                notes.append("%s: pasmo+dochod+nowi %d wobec 578 tys. (B1)" % (b.day, bin_ + inc + new))
            if not (0.8 * 367000 <= bout <= 1.2 * 367000):
                notes.append("%s: pasmo out %d wobec 367 tys. (B1)" % (b.day, bout))
    results.append(res("T7", st, "wielkosci%s" % (("; " + "; ".join(notes[:8])) if notes else "")))

    # T8 Obieg = zrodla
    bad = []
    for b in full:
        o = b.lines.get("obieg", "")
        if not o:
            continue
        z = (b.mod.get("Zold: dzien") or [""])[-1]
        pl = find(r"zold partii (\d+)", o)
        zl = find(r"partie rodow: naliczony (\d+)", z)
        if pl is not None and zl is not None and pl != zl:
            bad.append("%s: zold partii %d != Zold %d" % (b.day, pl, zl))
        lud = (b.mod.get("Ludnosc: dzien") or [""])[-1]
        rz = find(r"renty zaplacone (\d+)", lud)
        rv = find(r"renta wsi (\d+)", o); rt = find(r"zawor miast (\d+)", o)
        if rz is not None and rv is not None and rt is not None and rv + rt != rz:
            bad.append("%s: renty %d+%d != Ludnosc %d" % (b.day, rv, rt, rz))
        kor = b.mod.get("Korona: dzien") or []
        for k in kor:
            if "powinnosci wasali" in k:
                v = find(r"powinnosci wasali (\d+) zl", k)
                if v is not None and find(r"powinnosci do korony (\d+)", o) != v:
                    bad.append("%s: powinnosci" % b.day)
            if "zwrot zoldu ze skarbcow" in k:
                v = find(r"w wojnie: (\d+) zl", k)
                if v is not None and find(r"zwrot zoldu od korony (\d+)", o) != v:
                    bad.append("%s: zwrot zoldu" % b.day)
            if "danina wojenna z kas osad" in k:
                sub = find(r"danina wojenna z kas osad (\d+)", k); cus = find(r"clo od handlu miast (\d+)", k)
                mint = find(r"mennica (\d+)", k); mon = find(r"monopole (\d+)", k)
                if sub is not None and find(r"danina wojenna (\d+)", o.split("korona [P]")[-1]) != sub:
                    bad.append("%s: danina" % b.day)
                if cus is not None and find(r"skarbce dostaly (\d+) z kas miast", o) != cus:
                    bad.append("%s: clo" % b.day)
                taken = find(r"z licznika cel zdjeto (\d+)", o)
                if cus is not None and taken is not None and taken < cus:
                    bad.append("%s: U9 %d < clo %d" % (b.day, taken, cus))
                mm = find(r"mennica i monopole krolow (\d+)", o)
                if mint is not None and mon is not None and mm != mint + mon:
                    bad.append("%s: mennica i monopole" % b.day)
        ib = (b.mod.get("IronBank: dzien") or [""])[-1]
        if ib:
            m1 = re.search(r"nowe pozyczki (\d+) \((\d+)\), splaty (\d+) \((\d+)\)", ib)
            m2 = re.search(r"Bank \[P\]: pozyczki (\d+) \((\d+)\), splaty (\d+) \((\d+)\)", o)
            if m1 and m2 and m1.groups() != m2.groups():
                bad.append("%s: Bank" % b.day)
        stan = b.lines.get("stan", "")
        for lab, olab in (("kasy miast", "kasy miast \\[P\\]: stan"), ("kasy zamkow", "kasy zamkow \\[P\\]: stan"), ("sakiewki ludzi", "sakiewki ludzi \\[P\\]: stan"),
                          ("skarbce krolestw", "skarbce razem")):
            a = find(lab + r" (\d+)", stan); c = find(olab + r" (\d+)", o)
            if a is not None and c is not None and a != c:
                bad.append("%s: %s %d != %d" % (b.day, lab, c, a))
    results.append(res("T8", "OK" if not bad else "FAIL", "Obieg = linie modulow%s" % (("; " + "; ".join(bad[:8])) if bad else "")))

    # T9 D
    notes = []
    st = "OK"
    k_run = 0
    for b in blocks:
        t = (b.mod.get("Budzet rodow (na sucho)") or [""])[-1]
        if not t:
            continue
        k_run += 1
        avgd = find(r"srednio (\d+) zmierzonych dob", t)
        full_n = find(r"pelny u (\d+) rodow", t)
        if avgd is not None and k_run <= 28 and avgd > k_run:
            st = "FAIL"; notes.append("przebieg %d: srednio %d dob" % (k_run, avgd))
        if full_n is not None and k_run < 28 and full_n > 0 and not any(b2.mod.get("Budzet rodow: wczytano") for b2 in blocks):
            st = "FAIL"; notes.append("przebieg %d: pelny u %d rodow" % (k_run, full_n))
    if csv_path:
        try:
            with open(csv_path, encoding="utf-8", errors="replace") as f:
                rows = list(csvmod.DictReader(f, delimiter=";"))
            neg = [r for r in rows if r.get("D") not in (None, "", "-") and float(r["D"]) < 0]
            sal = [r for r in rows if r.get("saldo_modelu")]
            if neg:
                st = "FAIL"; notes.append("D < 0 w %d wierszach" % len(neg))
            if not sal:
                if st == "OK": st = "CZESCIOWO"
                notes.append("kolumna saldo_modelu pusta")
        except Exception as e:
            notes.append("CSV: %s" % e)
    else:
        notes.append("bez --csv (D < 0 i saldo_modelu nie sprawdzone); przebiegi B i C - osobne logi")
    results.append(res("T9", st if k_run else "POMINIETY", "D na rod (%d przebiegow)%s" % (k_run, ("; " + "; ".join(notes[:6])) if notes else "")))

    # T10 budzet i dlugi
    bad = []
    for b in blocks:
        t = (b.mod.get("Budzet rodow (na sucho)") or [""])[-1]
        d = (b.mod.get("Dlugi (na sucho)") or [""])[-1]
        if t:
            rody = find(r"kontrola: rody=(\d+)", t); pw = find(r"pokoj\+wojna=(\d+)", t)
            if rody is not None and pw is not None and rody != pw:
                bad.append("%s: pokoj+wojna %d != rody %d" % (b.day, pw, rody))
            if "ponad<=rody NIE" in t:
                bad.append("%s: ponad > rody" % b.day)
            rel = find(r"zwolnionych by dzis[^:]*: (\d+) ludzi", t); o3 = find(r"od 3 dob: (\d+)", t)
            if rel and not o3:
                bad.append("%s: zwolnieni bez rodow od 3 dob" % b.day)
        ib = (b.mod.get("IronBank: dzien") or [""])[-1]
        if d and ib:
            n1 = find(r"dluznikow (\d+) \(bankrutow", ib); s1 = find(r"dlug razem (\d+)", ib)
            n2 = find(r"Bank dzis a 8\.2: dluznikow (\d+)", d); s2 = find(r"Bank dzis a 8\.2: dluznikow \d+, dlug (\d+)", d)
            if None not in (n1, n2) and n1 != n2:
                bad.append("%s: dluznikow %d != Bank %d" % (b.day, n2, n1))
            if None not in (s1, s2) and abs(s1 - s2) > n1:
                bad.append("%s: dlug %d != Bank %d" % (b.day, s2, s1))
    results.append(res("T10", "OK" if not bad else "FAIL", "budzet i dlugi%s (limit Banku przy pozyczce - recznie z linii 'IronBank: ... pozycza')" % (("; " + "; ".join(bad[:6])) if bad else "")))

    # T11 sam log
    own = [e for e in errors if "CirculationWindows" in e or "ClanIncomeBook" in e or re.search(r"MoneyLedger\.(BalanceCauses|ClanCauses|ClassCauses|Obieg|Note169|ClanTick|BlockWorld|ClearLast)", e)]
    stum = []
    for b in full:
        v = find(r"potkniecia (\d+)", b.lines.get("okna", ""))
        if v:
            stum.append((b.day, v))
    st = "OK"
    if own or any(v >= 5 for _, v in stum):
        st = "FAIL"
    elif stum:
        st = "CZESCIOWO"
    results.append(res("T11", st, "bledy nowego kodu %d, doby z potknieciami %d%s (grep z rozdz. 12 - recznie na kodzie)" % (
        len(own), len(stum), ("; " + own[0][:160]) if own else "")))

    # T12 zapis
    results.append(res("T12", "POMINIETY", "zapis - tools/zapis/napisy.py z repo autotestu (napisy > 32767 B, klucz arm_clanincome)"))

    # T13 wydajnosc
    bad = []
    for b in full:
        o = b.lines.get("okna", "")
        okn = find(r"okna ok\. ([\d.]+) ms", o, float); day_ = find(r"przeliczenie doby ([\d.]+) ms", o, float); pr = find(r"probki swiata ([\d.]+) ms \(", o, float)
        if okn is not None and okn >= 50: bad.append("%s: okna %.1f ms" % (b.day, okn))
        if day_ is not None and day_ >= 30: bad.append("%s: doba %.1f ms" % (b.day, day_))
        if pr is not None and pr >= 20: bad.append("%s: probki %.1f ms" % (b.day, pr))
    results.append(res("T13", "OK" if not bad else "CZESCIOWO", "koszt%s (dlugosc doby gry - porownanie z T3 recznie)" % (("; " + "; ".join(bad[:6])) if bad else "")))

    # T14 wylacznik
    gaps = [b.day for b in blocks if "bilans" in b.lines and "przyczyny" not in b.lines]
    results.append(res("T14", "POMINIETY" if not gaps else "OK", ("doby bez nowych linii (wylacznik): %s" % gaps[:10]) if gaps else "wymaga przebiegu z wylaczonym CirculationLedgerEnabled / CirculationProbeEnabled"))

    # T15 nasz tick (RB)
    rbs = [(b.day, kv(b.lines["przyczyny"]).get("RB")) for b in full if in_range(b, lo, hi, first) and b.day not in aborted_days]
    rbs = [(d, v) for d, v in rbs if v is not None]
    if not rbs:
        results.append(res("T15", "POMINIETY", "brak RB w dobach %d-%d" % (lo, hi)))
    else:
        m = sum(abs(v) for _, v in rbs) / float(len(rbs))
        worst = max(rbs, key=lambda x: abs(x[1]))
        st = "OK" if m < 2000 else ("CZESCIOWO" if m <= 10000 else "FAIL")
        if abs(worst[1]) > 50000:
            st = "FAIL"
        results.append(res("T15", st, "srednia |RB| %.0f, najgorsza doba %s: %d" % (m, worst[0], worst[1])))

    # T16 probki swiata
    daily = ["notable-dochod", "karawany-notabli", "pasmo-notabli", "awanse", "werbunek", "prowizja", "rynek-osady", "konwoje",
             "warsztaty-wyrob", "warsztaty-wsad", "kupno-BK", "myto"]
    tot = 0; okn = 0; badw = {}
    sess = {}
    for b in full:
        o = b.lines.get("okna", "")
        m = re.search(r"probki swiata wokol okien \[P\]: (\d+) \(zgodne (\d+); rozjazd (\d+)(?:: ([^)]*))?\)", o)
        if m:
            tot += int(m.group(1)); okn += int(m.group(2))
            if m.group(4):
                for part in m.group(4).split(", "):
                    mm = re.match(r"(\S+) ([-+]?\d+)", part)
                    if mm:
                        badw.setdefault(mm.group(1), []).append("%s:%s" % (b.day, mm.group(2)))
        m = re.search(r"od startu wg okien: ([^|]*)", o)
        if m and m.group(1).strip() not in ("-", ""):
            for part in m.group(1).split(", "):
                mm = re.match(r"(\S+) (\d+)", part.strip())
                if mm:
                    sess[mm.group(1)] = max(sess.get(mm.group(1), 0), int(mm.group(2)))
    notes = []
    st = "OK"
    if tot == 0:
        results.append(res("T16", "POMINIETY", "brak probek (CirculationProbeEnabled wylaczony?)"))
    else:
        few = [n for n in daily if (n not in ("myto", "kupno-BK") or hits.get(n, 0) > 0) and sess.get(n, 0) < 5 and n not in missing]
        if few:
            st = "FAIL"; notes.append("mniej niz 5 probek: " + ", ".join(few))
        if okn < 0.95 * tot:
            st = "FAIL"; notes.append("zgodnych %d z %d (< 95%%)" % (okn, tot))
        multi = {k: v for k, v in badw.items() if len(v) > 1}
        if multi:
            st = "FAIL"; notes.append("okna z rozjazdem w > 1 probce: " + "; ".join("%s %s" % (k, ",".join(v[:4])) for k, v in multi.items()))
        results.append(res("T16", st, "probki %d, zgodne %d%s" % (tot, okn, ("; " + "; ".join(notes)) if notes else "")))

    # T17 okno rodu
    results.append(res("T17", "OK" if not aborted_days else "FAIL", "rozliczenia przerwane wyjatkiem: %s" % (sorted(aborted_days) if aborted_days else "0")))

    return 1 if "FAIL" in results else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
