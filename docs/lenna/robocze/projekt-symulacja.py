# Symulacja reguly nadawania zdobytych twierdz (PROJEKT-LENNA) na tabelach biegu.
# Uzycie: python -I projekt-symulacja.py <tabele-dir> <spclans.xml> <etykieta> [wariant: N (wybrany) | korona50 | korona_zdobywca | zdobywca100 | bez_dejure | bez_pulapu]
# Wejscie: ludzie-regiony.csv (wlasciciel twierdz co dobe), budzet-rodow.csv (rody, krolestwo, rodzaj co dobe), bitwy.log (zwyciezki szturm: wodz armii),
#          spclans.xml ROT (tier startowy rodu). Wynik: kazde zdobycie - kandydaci, oceny, zwyciezca reguly vs gra; rozklad na koniec; progi.
import sys, re, collections

tdir, xmlp, label = sys.argv[1], sys.argv[2], sys.argv[3]
variant = sys.argv[4] if len(sys.argv) > 4 else "N"
D0 = 108835

# --- parametry reguly ---
BASE = 100.0
CAPTURER = 50.0
PARTICIPANT = 25.0      # w symulacji nieznani (log nie ma skladu armii)
LANDLESS = 50.0
DEJURE = 100.0          # w symulacji: rod, ktory mial twierdze w dniu 1 (BK nie przenosi tytulu de jure przy zdobyciu ani nadaniu)
CROWN = 50.0
CROWN_SHARE = 0.20
TOWN_W, CASTLE_W = 2, 1
TOWN_CAP = 99           # osobnego pulapu miast brak - miasto = 2 punkty w pulapie
def allowance(t):       # "przydzial" wedlug tieru (punkty: miasto 2, zamek 1)
    return max(1, t - 1)
def cap(t):
    return allowance(t) + 2
if variant == "bez_pulapu":
    def cap(t):
        return 999
if variant == "zdobywca100":
    CAPTURER = 100.0
if variant == "bez_dejure":
    DEJURE = 0.0
CROWN_CAPTURES = variant in ("korona_zdobywca",)   # L1: korona NIE dostaje premii zdobywcy (krol nagradza tych, ktorzy szli z nim)
if variant == "korona50":
    CROWN = 50.0
else:
    CROWN = 25.0

# --- tier z XML ROT ---
tier = {}
s = open(xmlp, encoding="utf-8", errors="replace").read()
for m in re.finditer(r'<Faction\s([^>]*)>', s):
    a = m.group(1)
    i = re.search(r'\bid="([^"]*)"', a); t = re.search(r'\btier="([^"]*)"', a)
    if i and t:
        tier[i.group(1)] = int(t.group(1))

# --- rody: dzien -> {rod_name: (rod_id, krol, rodzaj)} ---
clans_by_day = {}
name2id = {}
with open(tdir + "/budzet-rodow.csv", encoding="utf-8", errors="replace") as f:
    f.readline()
    for ln in f:
        p = ln.split(";", 6)
        try:
            d = int(p[0]) - D0   # budzet: kolumna dzien jak w ludzie-regiony
        except ValueError:
            continue
        rid, rn, kr, rk = p[2], p[3], p[4], p[5]
        clans_by_day.setdefault(d, {})[rn] = (rid, kr, rk)
        name2id[rn] = rid

# --- twierdze co dobe ---
own = {}      # dzien -> {sid: (krol, rod)}
kind, sname = {}, {}
with open(tdir + "/ludzie-regiony.csv", encoding="utf-8", errors="replace") as f:
    f.readline()
    for ln in f:
        p = ln.split(";", 7)
        try:
            d = int(p[0]) - D0
        except ValueError:
            continue
        sid = p[1]; sname[sid] = p[2]; kind[sid] = p[3]
        own.setdefault(d, {})[sid] = (p[5], p[6])
days = sorted(own)
DAY1 = days[0]

# --- zwyciezkie szturmy: (dzien, osada) -> wodz ---
cap_leader = {}
rx = re.compile(r"Bitwa: dzien (\d+) - Siege pod (.+?): (.+?) \((.+?)\) \d+ ludzi vs .*; wygrywa atakujacy")
with open(tdir + "/bitwy.log", encoding="utf-8", errors="replace") as f:
    for ln in f:
        if " - Siege pod " not in ln or "H3" in ln:
            continue
        m = rx.search(ln)
        if m:
            d = int(m.group(1)) - D0
            leader = m.group(3)
            kr = m.group(4).split(", partii")[0]
            cap_leader[(d, m.group(2))] = (leader, kr)

def clan_of_leader(leader, kr, d):
    best = None
    toks = set(re.split(r"[ ,\-]+", leader))
    for rn, (rid, k, rk) in clans_by_day.get(d, clans_by_day[max(x for x in clans_by_day if x <= d)]).items():
        if k != kr:
            continue
        parts = [x for x in re.split(r"[ ,\-]+", rn) if x]
        if parts and all(x in toks for x in parts if x not in ("Nights", "Watch")):
            if best is None or len(parts) > len(best[1]):
                best = (rn, parts)
    return best[0] if best else None

# --- zdarzenia zmiany wlasciciela ---
events = []   # (d, sid, old(kr,rod), new(kr,rod))
prev = own[DAY1]
for d in days[1:]:
    cur = own[d]
    for sid, o in cur.items():
        if prev.get(sid) != o:
            events.append((d, sid, prev.get(sid), o))
    prev = cur

# zdobycia z decyzja: zmiana krolestwa (rod inny), potem do 10 dob pierwsza zmiana rodu w tym krolestwie
hist = collections.defaultdict(list)
for (d, sid, o, n) in events:
    hist[sid].append((d, o, n))
captures = {}   # (decision_day, sid) -> info
story_days = {536}
for sid, h in hist.items():
    for i, (d, o, n) in enumerate(h):
        if o is None or o[0] == n[0] or o[1] == n[1]:
            continue
        if d in story_days:
            continue
        final, fday = n[1], d
        if i + 1 < len(h):
            d2, o2, n2 = h[i + 1]
            if n2[0] == n[0] and d2 - d <= 10:
                final, fday = n2[1], d2
        lead = cap_leader.get((d, sname[sid])) or cap_leader.get((d - 1, sname[sid])) or cap_leader.get((d + 1, sname[sid]))
        captures[(fday, sid)] = dict(day=d, fday=fday, sid=sid, kr=n[0], king=n[1] if fday > d else None, game=final, leader=lead)

# wladca krolestwa: rod dnia zdobycia, gdy decyzja dobe pozniej; inaczej z innego zdobycia tego krolestwa / mapa
ruler = {}
for c in captures.values():
    if c["king"]:
        ruler.setdefault(c["kr"], collections.Counter())[c["king"]] += 1
RULER_FIX = {"Stormlands": "Baratheon,Renly", "Dragonstone": "Baratheon,Stannis", "Volantis": "Melane", "House Targaryen": "Targaryen"}
def ruler_of(kr):
    if kr in RULER_FIX:
        return RULER_FIX[kr]
    if kr in ruler:
        return ruler[kr].most_common(1)[0][0]
    return None

# parostwo (BK): rod z twierdza w dniu 1 (gentry BK - bez lenna - nie)
peer = set(rod for (_, rod) in own[DAY1].values())

# --- symulacja ---
sim = dict((sid, o) for sid, o in own[DAY1].items())
decision_keys = set(captures)
log = []
results = []
for d in days[1:]:
    # najpierw zmiany, ktore nie sa decyzja (zdobycie przez krola, fabula, odejscia rodow) - jak w biegu
    for (ed, sid, o, n) in [e for e in events if e[0] == d]:
        if (d, sid) in decision_keys:
            continue
        # zmiana rodu wewnatrz krolestwa tuz po zdobyciu (decyzja) obslugujemy nizej; inne - jak w grze
        if o and n and o[0] == n[0] and o[1] != n[1]:
            # zmiana rodu w obrebie krolestwa nie bedaca decyzja (rzadkie) - jak w grze
            sim[sid] = n
        elif o and n and o[0] != n[0] and o[1] == n[1]:
            # rod odchodzi z lennem: w symulacji odchodzi z tym, co MA w symulacji (wszystkie jego twierdze tego krolestwa)
            for s2, (k2, r2) in list(sim.items()):
                if r2 == n[1] and k2 == o[0]:
                    sim[s2] = (n[0], r2)
            sim[sid] = n
        else:
            sim[sid] = n
    for key in sorted(k for k in decision_keys if k[0] == d):
        c = captures[key]
        sid, kr = c["sid"], c["kr"]
        istown = kind[sid] == "miasto"
        w = TOWN_W if istown else CASTLE_W
        rul = ruler_of(kr)
        # ciezar rodow krolestwa w symulacji (bez X)
        W = collections.Counter(); T = collections.Counter()
        tot = 0
        for s2, (k2, r2) in sim.items():
            if k2 != kr or s2 == sid:
                continue
            ww = TOWN_W if kind[s2] == "miasto" else CASTLE_W
            W[r2] += ww; tot += ww
            if kind[s2] == "miasto":
                T[r2] += 1
        cd = clans_by_day.get(d) or clans_by_day[max(x for x in clans_by_day if x <= d)]
        cands = [rn for rn, (rid, k, rk) in cd.items() if k == kr and rk != "najemnik" and rk != "gracz" and (rn in peer or W[rn] > 0)]
        if not cands:
            cands = [rn for rn, (rid, k, rk) in cd.items() if k == kr and rk not in ("najemnik", "gracz")]
        capclan = clan_of_leader(*c["leader"], d - (d - c["day"])) if c["leader"] else None
        crown_share = (W[rul] / tot) if (rul and tot) else 0.0
        rows = []
        for rn in cands:
            t = tier.get(cd[rn][0], 3)
            P = allowance(t); Cp = cap(t)
            raw = BASE
            parts = ["baza 100"]
            if rn == capclan and (rn != rul or CROWN_CAPTURES):
                raw += CAPTURER; parts.append("zdobywca +%d" % CAPTURER)
            if W[rn] == 0:
                raw += LANDLESS; parts.append("bez ziemi +50")
            if DEJURE and own[DAY1].get(sid, (None, None))[1] == rn:
                raw += DEJURE; parts.append("de jure +100")
            if rn == rul and crown_share < CROWN_SHARE:
                raw += CROWN; parts.append("domena korony %.0f%% < 20%% +%d" % (100 * crown_share, CROWN))
            fac = P / (P + W[rn])
            over = (W[rn] + w > Cp) or (istown and T[rn] + 1 > TOWN_CAP)
            sc = (1.0 if over else raw) * fac
            if sname[sid] == "King's Landing" and rn == rul:
                sc *= 999
            rows.append((sc, rn, t, W[rn], T[rn], P, Cp, over, parts))
        # sortowanie: ocena malejaco, remis - mniejszy ciezar, potem nazwa (w grze: kolejnosc listy krolestwa - tu nazwa)
        rows.sort(key=lambda r: (-r[0], r[3], r[1]))
        win = rows[0][1] if rows else None
        results.append(dict(c=c, rows=rows[:5], win=win, capclan=capclan, rul=rul, crown_share=crown_share, n=len(rows)))
        sim[sid] = (kr, win)

# --- wydruk ---
print("== %s (%s): %d zdobyc z decyzja (bez fabuly d536)" % (label, variant, len(results)))
print("dzien;osada;rodzaj;krolestwo;zdobywca(wodz);gra_dala;regula_daje;W_przed/pulap;3 pierwsze (ocena, tier, ciezar, miasta)")
for r in results:
    c = r["c"]; rows = r["rows"]
    top = " | ".join("%s %.0f (t%d, W%d, m%d%s)" % (x[1], x[0], x[2], x[3], x[4], ", PULAP" if x[7] else "") for x in rows[:3])
    wr = [x for x in rows if x[1] == r["win"]][0]
    tie = len(rows) > 1 and abs(rows[0][0] - rows[1][0]) < 0.5
    print("%d;%s;%s;%s;%s;%s;%s%s;%d/%d;%s;[%s]" % (c["fday"], sname[c["sid"]], kind[c["sid"]], c["kr"], r["capclan"] or "-", c["game"], r["win"], " (REMIS)" if tie else "", wr[3], wr[6], top, ", ".join(wr[8])))

def summarize(owner_of, tag):
    by_k = collections.defaultdict(collections.Counter)
    for r in results:
        by_k[r["c"]["kr"]][owner_of(r)] += 1
    print("\n-- %s: komu zdobycze, wg krolestwa --" % tag)
    for k, cnt in sorted(by_k.items(), key=lambda x: -sum(x[1].values())):
        n = sum(cnt.values())
        top = cnt.most_common(1)[0]
        print("%s: %d -> %s | najwiekszy udzial %.0f%%" % (k, n, ", ".join("%s %d" % x for x in cnt.most_common()), 100.0 * top[1] / n))
summarize(lambda r: r["c"]["game"], "GRA (bieg)")
summarize(lambda r: r["win"], "REGULA")

# koncentracja na koniec symulacji (tylko twierdze; krolestwa jak w symulacji)
cnt = collections.Counter(); cntT = collections.Counter()
for s2, (k2, r2) in sim.items():
    cnt[(k2, r2)] += 1
    if kind[s2] == "miasto":
        cntT[(k2, r2)] += 1
cntG = collections.Counter(); cntGT = collections.Counter()
for s2, (k2, r2) in own[days[-1]].items():
    cntG[(k2, r2)] += 1
    if kind[s2] == "miasto":
        cntGT[(k2, r2)] += 1
print("\n-- najwiecej twierdz na koniec: gra vs regula (twierdze/miasta) --")
print("GRA:    " + ", ".join("%s/%s %d/%d" % (k[0], k[1], v, cntGT[k]) for k, v in cntG.most_common(8)))
print("REGULA: " + ", ".join("%s/%s %d/%d" % (k[0], k[1], v, cntT[k]) for k, v in cnt.most_common(8)))
print("najwiecej miast w jednym rodzie: gra %d, regula %d" % (max(cntGT.values()), max(cntT.values())))
wins = collections.Counter(r["win"] for r in results)
print("rodow z nadaniem: gra %d, regula %d" % (len(set(r["c"]["game"] for r in results)), len(wins)))
print("zdobywca dostal: gra %d, regula %d z %d (ze znanym wodzem)" % (
    sum(1 for r in results if r["capclan"] and r["c"]["game"] == r["capclan"]),
    sum(1 for r in results if r["capclan"] and r["win"] == r["capclan"]),
    sum(1 for r in results if r["capclan"])))
print("korona dostala: gra %d, regula %d" % (sum(1 for r in results if r["c"]["game"] == r["rul"]), sum(1 for r in results if r["win"] == r["rul"])))
print("remisy na czele (rozstrzyga glos rady): %d" % sum(1 for r in results if len(r["rows"]) > 1 and abs(r["rows"][0][0] - r["rows"][1][0]) < 0.5))
big = [k for k, v in collections.Counter(r["c"]["kr"] for r in results).items() if v >= 4]
for tag, f in (("gra", lambda r: r["c"]["game"]), ("regula", lambda r: r["win"])):
    worst = []
    for k in big:
        cnt = collections.Counter(f(r) for r in results if r["c"]["kr"] == k)
        n = sum(cnt.values()); top = cnt.most_common(1)[0]
        worst.append((100.0 * top[1] / n, k, top[0], top[1], n))
    worst.sort(reverse=True)
    print("udzial najwiekszego rodu w zdobyczach krolestw z >= 4 zdobyczami (%s): " % tag + "; ".join("%s %s %d/%d = %.0f%%" % (w[1], w[2], w[3], w[4], w[0]) for w in worst))
nc = [r for r in results if r["capclan"] and r["capclan"] != r["rul"] and any(x[1] == r["capclan"] and not x[7] for x in r["rows"] + [])]
nc_all = [r for r in results if r["capclan"] and r["capclan"] != r["rul"]]
print("zdobywca spoza rodu krola: dostal %d z %d (gra %d)" % (sum(1 for r in nc_all if r["win"] == r["capclan"]), len(nc_all), sum(1 for r in nc_all if r["c"]["game"] == r["capclan"])))
ratio_g = len(set(r["c"]["game"] for r in results)) / max(1, len(results)); ratio_r = len(set(r["win"] for r in results)) / max(1, len(results))
print("rozproszenie (rodow z nadaniem / nadan): gra %.2f, regula %.2f" % (ratio_g, ratio_r))
close = sum(1 for r in results if len(r["rows"]) > 1 and r["rows"][0][0] > 0 and r["rows"][1][0] >= 0.7 * r["rows"][0][0])
print("bliskie (2. ma >= 0.7 oceny 1. - rada moze przewazyc, N4): %d z %d" % (close, len(results)))
print("ponad pulapem przy nadaniu (regula): %d" % sum(1 for r in results if [x for x in r["rows"] if x[1] == r["win"]][0][7]))
