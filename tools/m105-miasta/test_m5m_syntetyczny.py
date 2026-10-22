"""m105-miasta: test syntetyczny grupy progow m105miasta (tools/sprawdz_logi.py --grupa m105miasta). Dwa biegi zlozone z linii jak w kodzie C# (KsFolkIncome.DayLine,
TownFolk.BreadLine, KsLordFood.Line, MapRoadKinds.Daily, MarketCarts.Daily, linia KS-H) i kasy-miast.csv: "dobry" - kazdy wiersz M5M TAK, "zly" - kazdy wiersz NIE.
Druga czesc (poprawki po recenzji, R2): grupa nglod, NGL-6f - splaty rachunkow ze skarbca korony (segment m105 linii pana) liczone razem z panem wobec linii KS-H.
Uzycie (z katalogu repo albo skadkolwiek): python -I tools/m105-miasta/test_m5m_syntetyczny.py   (kod wyjscia 0 = zgodnie z oczekiwaniem)."""
import os, re, subprocess, sys, shutil, tempfile

W = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HERE = tempfile.mkdtemp(prefix="m105m-")
D0 = 108648
N = 240


def f5(d, g1):
    s = (f"Dochod ludu wedlug koszyka (f5-lud): dzien {d} | klucze: ... | czas 0.50 ms | potkniecia 0 | P3 sufit firm (Ks Farm Surplus Cap Net TAK): przyciete sufitem 0 "
         f"(-0 zl/rok), obnizki z dochodu 0 (-0 zl/rok), firmy od zera 0 (+0 zl/rok); od startu sesji: przyciete 0 (-0), obnizki 0 (-0), od zera 0 (+0)")
    if g1:
        s += " | m105 szybka obnizka (Ds Farm Cut Below Reserve TAK): dzis 1 (-1000 zl/rok), od startu sesji 5 (-5000 zl/rok), miast z KS ponizej zapasu 2, z firma ponad doplyw 0"
    return s + "."


def bread(d, hoard, left, ctl, below):
    return (f"Chleb biedoty (M3): dzien {d} | Bread For The Poor tak; ... | zaplacono 9000: korona 7000 (krolestw 2, z reszty wplywow dnia przed rentami; w tym ze skarbca ponad "
            f"zapas wojny {hoard}), pan zastepczo 2000 (panow 1; najnizsze G/R placacego pana 1.35), nie stac nikogo 300 zl w 1 osadach | na krolestwo (korona/pan/nie stac "
            f"nikogo): X 7000/2000/300 | m105 skarbiec korony (Crown Hoard Feeds Hungry TAK): ze skarbca {hoard} zl (krolestw 1, osad 2), nie stac nikogo po skarbcu 300 zl "
            f"(w tym przy wolnym limicie skarbca {left} zl - oczekiwane 0), kontrola skarbiec -{hoard - ctl} + kiesy ludu {hoard} = {ctl}, ponizej zapasu wojny po wyplacie "
            f"{below}, limit doby 1/30 nadwyzki skarbca (wspolny z rachunkami KS-H).")


def lord(d, paid):
    return (f"Pan miasta placi za dowoz (KS): dzien {d} | klucz Ks Lord Funds Food tak | ... | zaleglosc nawiasu 0 | m105 skarbiec korony: rachunki splacone ze skarbca {paid} "
            f"zl (miast 1, krolestw 1, kandydatow po panie bez kasy ponad podloge 1), kontrola skarbiec -{paid} + wierzyciele {paid} = 0, ponizej zapasu wojny po wyplacie 0 "
            f"| czas 0.050 ms | potkniecia 0.")


def vr(d, avg, seg):
    s = (f"Drogi Valyrii (VR): dzien {d} | klucze: Valyria tak, szlaki tak | partio-godzin w marszu 100: na drodze Valyrii 2 (2.0%), na szlaku karawan 0 (0.0%), na zwyklym "
         f"trakcie 10 (10.0%) | liczenia predkosci z premia: Valyria ok. 5, szlak ok. 0 | odcinkow V 40 + mosty 7, C 9 | audyt Valyria: 6 partii, premia rzeczywista sr. "
         f"{avg:.2f}% (min {avg:.2f}, maks 25.00), z wpisem 'Valyrian road' 6, oczekiwane 25.0%")
    if seg:
        s += ", podloga z premia (m105) 1"
    s += " | audyt szlak: 0 partii, oczekiwane 7.5%"
    if seg:
        s += ", podloga z premia (m105) 0, liczenia z premia od podlogi gry (m105, wszystkie drogi) ok. 3"
    return s + " | siec AI drog: mosty Valyrii 7, krawedzie Valyrii 761 jedn., szlakow karawan 175 jedn. (klasy tak) | potkniecia 0."


def cart(d, new, exp):
    return (f"Dowoz (wozy): dzien {d} - wybor miasta CZYNNY: 50 wozow (wsi zamkowych 10), ... , niezgodne 0, cena rosla 0 | m105 zloto zarezerwowane (Village Cart Gold "
            f"Aware TAK): wozow z rezerwa {new}, zarezerwowane 250000 zl u 30 miast, wybor zmieniony 3, wygasle {exp} | woz x2.0 dla wsi wszystkich.")


def ksh(d, over, early):
    return (f"Kupcy zywnosci (KS-H) dzien {d} | kredyt kupiecki TAK: otwarte 50000 zl, splacone 100 zl, starsze niz 30 dni {over} zl ({1 if over else 0} miast), przepadle 0 "
            f"| splacone przez panow miast (glod, zalegle rachunki) 0 zl w 0 splatach, kasy zrodel przyjely 0 zl, w tym przed zwloka (wczesna splata f4) 0 zl w 0 splatach; "
            f"najstarszy rachunek 21 dni, rachunki starsze niz 20 dni {early} zl | kredyt w drodze: wozow 0.")


def build(dirp, good):
    if os.path.isdir(dirp):
        shutil.rmtree(dirp)
    tag = "2026-10-22_00-00-00"
    os.makedirs(os.path.join(dirp, tag))
    L = []
    for i in range(N):
        d = D0 + i
        L.append(f5(d, good or i != 100))                                   # zly: brak segmentu G1 w dobie 100
        L.append(bread(d, 4000, 0 if good else (500 if i == 150 else 0), 0 if good else (7 if i == 151 else 0), 0))
        L.append(lord(d, 1000))
        L.append(vr(d, 25.0 if good else 24.0, True))
        L.append(cart(d, 20, 0 if good else 2))                             # zly: 10% wygaslych
        L.append(ksh(d, 0 if good else (500 if i == 200 else 0), 5000 if good else 30000))
    if not good:
        L.append("ERROR in MarketCarts.Daily: System.Exception: test")       # recenzja R3: cala klasa MarketCarts (dotad tylko MarketCarts.Choose)
    with open(os.path.join(dirp, f"Armoury-{tag}.log"), "w", encoding="utf-8", newline="\n") as fh:
        for s in L:
            fh.write("[12:00:00] " + s + "\n")
    with open(os.path.join(dirp, tag, "kasy-miast.csv"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("dzien;dzien_gry;miasto_id;miasto;kasa;zapas_kupcow;zawor_do_pana;do_korony;pobrane;doplyw_netto\n")
        for i in range(N):
            d = D0 + i
            # miasto A zawsze ponad zapasem; miasto B - w zlym biegu od doby 150 ponizej zapasu z poborem ponad doplyw
            fh.write(f"{d};{d + 1};town_A;A;300000;100000;120000;60000;180000;200000\n")
            if good:
                fh.write(f"{d};{d + 1};town_B;B;250000;100000;120000;60000;180000;200000\n")
            else:
                low = i >= 150
                for tid in ("B", "C", "D"):                                   # 3 miasta > prog M5M-1 (<= 2)
                    fh.write(f"{d};{d + 1};town_{tid};{tid};{50000 if low else 250000};100000;{20000 if low else 120000};{10000 if low else 60000};"
                             f"{30000 if low else 180000};{5000 if low else 200000}\n")
    return os.path.join(dirp, f"Armoury-{tag}.log")


def verdicts(logp):
    r = subprocess.run([sys.executable, "-I", os.path.join(W, "tools", "sprawdz_logi.py"), "--grupa", "m105miasta", logp], capture_output=True)
    out = r.stdout.decode("utf-8", "replace") + r.stderr.decode("utf-8", "replace")
    v = {}
    for l in out.splitlines():
        m = re.match(r"(M5M-[0-9S][0-9b]*)[:/].*?\| (TAK|NIE|INFO)\s*\|", l)
        if m:
            v[m.group(1)] = m.group(2)
    return v, out


EXP_GOOD = {"M5M-S": "TAK", "M5M-0": "TAK", "M5M-1": "TAK", "M5M-2": "TAK", "M5M-3": "TAK", "M5M-3b": "INFO", "M5M-4": "TAK", "M5M-5": "TAK", "M5M-5b": "INFO",
            "M5M-6": "TAK", "M5M-7": "TAK", "M5M-8": "TAK", "M5M-9": "TAK", "M5M-9b": "INFO"}
EXP_BAD = {"M5M-S": "TAK", "M5M-0": "NIE", "M5M-1": "NIE", "M5M-2": "NIE", "M5M-3": "NIE", "M5M-3b": "INFO", "M5M-4": "NIE", "M5M-5": "NIE", "M5M-5b": "INFO",
           "M5M-6": "NIE", "M5M-7": "NIE", "M5M-8": "NIE", "M5M-9": "NIE", "M5M-9b": "INFO"}
bad = 0
for name, good, exp in (("dobry", True, EXP_GOOD), ("zly", False, EXP_BAD)):
    lp = build(os.path.join(HERE, "syn", name), good)
    v, out = verdicts(lp)
    diff = {k: (exp[k], v.get(k)) for k in exp if v.get(k) != exp[k]}
    print(f"{name}: {'OK' if not diff and 'Traceback' not in out else 'BLAD'} {diff if diff else ''}")
    if diff or "Traceback" in out:
        bad += 1
        print(out[-3000:])


# ---- czesc 2 (recenzja R2): NGL-6f - linia pana "splacone przez panow" + segment m105 "rachunki splacone ze skarbca" = linia KS-H doby o 1 mniejszej
def nglod_log(path, hoard_seg, ksh_paid):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        for i in range(30):
            d = D0 + i
            seg = (" | m105 skarbiec korony: rachunki splacone ze skarbca 1000 zl (miast 1, krolestw 1, kandydatow po panie bez kasy ponad podloge 1), kontrola skarbiec "
                   "-1000 + wierzyciele 1000 = 0, ponizej zapasu wojny po wyplacie 0") if hoard_seg else ""
            fh.write(f"[12:00:00] Kupcy zywnosci (KS-H) dzien {d - 1} | kredyt kupiecki TAK: otwarte 50000 zl | splacone przez panow miast (glod, zalegle rachunki) "
                     f"{ksh_paid} zl w 2 splatach, kasy zrodel przyjely {ksh_paid} zl | kredyt w drodze: wozow 0.\n")
            fh.write(f"[12:00:00] Pan miasta placi za dowoz (KS): dzien {d} | klucz Ks Lord Funds Food tak | tryb RACHUNKI | KS-H czynne | rachunki: otwarte 5000 zl, "
                     f"splacone przez panow 2000 zl, kasy zrodel (wierzyciele) przyjely 2000 zl{seg} | czas 0.050 ms | potkniecia 0.\n")


def ngl6f(path):
    r = subprocess.run([sys.executable, "-I", os.path.join(W, "tools", "sprawdz_logi.py"), "--grupa", "nglod", path], capture_output=True)
    out = r.stdout.decode("utf-8", "replace") + r.stderr.decode("utf-8", "replace")
    m = re.search(r"^NGL-6f:.*?\| (TAK|NIE|INFO)\s*\|", out, re.M)
    return (m.group(1) if m else None), out


os.makedirs(os.path.join(HERE, "ngl"), exist_ok=True)
for name, hs, kp, exp in (("pan + skarbiec = KS-H", True, 3000, "TAK"), ("bez skarbca (log sprzed m105)", False, 2000, "TAK"),
                          ("KS-H ze skarbcem, linia pana bez segmentu", False, 3000, "NIE")):
    lp = os.path.join(HERE, "ngl", "Armoury-ngl.log")
    nglod_log(lp, hs, kp)
    v, out = ngl6f(lp)
    print(f"NGL-6f {name}: {'OK' if v == exp and 'Traceback' not in out else 'BLAD'} ({v}, oczekiwane {exp})")
    if v != exp or "Traceback" in out:
        bad += 1
        print(out[-3000:])
shutil.rmtree(HERE, ignore_errors=True)
sys.exit(1 if bad else 0)
