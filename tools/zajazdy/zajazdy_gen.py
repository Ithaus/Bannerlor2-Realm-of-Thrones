# -*- coding: utf-8 -*-
"""
ZAJAZDY PRZY SZLAKACH (sw-zajazdy, Jeff 16.10; f10-uklad, Jeff 18.10): generator pliku Armoury/ModuleData/arm_roadside_inns.tsv.

Jeff 16.10: "osobne budynki przy szlakach, ale nie za gesto, im mniej ludzi tym mniej zajazdow, zeby nie wygladalo jak armia zajazdow".
Jeff 18.10 (f10-uklad): "zajazdy nie sa obok wiosek, bo po co, skoro mozna spac w wiosce i wioskach pobocznych"; droga poboczna odchodzi od drogi glownej,
od krolewskiego traktu - zajazd stoi na rozstajach, gdzie boczna droga odchodzi od glownej, albo na dlugim pustym odcinku drogi miedzy osadami.

Wejscie:
  Armoury/ModuleData/arm_map_roads_geo.tsv  - malowane drogi ROT (311 linii, krolewski trakt i inne trakty; z sondy 16.10, plik moda)
  geo-trasy.tsv, geo-trasy-punkty.tsv       - trasy AI gry miedzy twierdzami (kolumna droga = punkt na malowanej drodze) - boczne drogi bez farby
  geo-siatka.tsv                            - siatka 2 jedn.: nawig, wies_gry (najblizsza wies gry = okreg), kultura
  arm_map_villages.tsv                      - wioski poboczne ukladu v7 (polozenie, osady po 250 ludzi)
  osady.json (katalog a-teren)              - wsie gry, miasta, zamki (polozenie)
Wynik: jeden wiersz = jeden zajazd, deterministycznie (ten sam plik = te same zajazdy w kazdej kampanii). Format kolumn bez zmian (RoadsideInns.ParseRow).

Regula (jedna dla calego swiata):
  1. kandydaci, od najwazniejszych:
     a) rozstaje na malowanej drodze: wezel sieci malowanych drog stopnia >= 3 (takze "T" - koniec jednej linii przy srodku drugiej) i miejsce, gdzie trasa AI
        (boczna droga bez farby) zjezdza z malowanej drogi na dobre (>= ZJAZD_MIN jedn. poza droga); trakt glowny (skladowa sieci >= MAIN_LEN jedn.) przed reszta;
     b) dlugi pusty odcinek malowanej drogi: punkt drogi (co 2 jedn.) dalej niz EMPTY_MIN od kazdej osady (wioski, wsie gry, miasta, zamki), najpustszy pierwszy;
     c) tam, gdzie malowanych drog nie ma (zadnej w NO_PAINT jedn.): punkt trasy AI dalej niz EMPTY_TRACK od osady (kind "track");
  2. budynek: nie blizej niz MIN_VIL od kazdej wioski pobocznej i wsi gry (= odleglosc minimalna ukladu v7, wiec zajazd nigdy nie stoi przy wiosce), MIN_TOWN od miasta
     (miasta maja karczmy), MIN_CASTLE od zamku; na ladzie przejezdnym (siatka nawig, budynek i postoj); nie za Murem (freefolk, nightswatch);
  3. ludzie wokol = suma ludzi wiosek pobocznych w promieniu R_PEOPLE (osady x 250); ponizej P_MIN - bez zajazdu;
  4. odstep = DAY_MARCH x sqrt(P_REF / ludzie) w granicach [SP_MIN, SP_MAX] - im mniej ludzi, tym rzadziej; zajazd wchodzi, gdy zaden wybrany nie stoi blizej
     niz wiekszy z obu odstepow (wybor zachlanny w kolejnosci z pkt 1, potem wiecej ludzi);
  5. budynek ok. OFFSET jedn. obok drogi (z 8 kierunkow: na ladzie, z dala od srodka drogi, najdalej od wiosek), front do postoju; postoj = punkt na drodze;
  6. jakosci: 1 (wspolna izba) zawsze; 2 (porzadna karczma) od P_MID ludzi; 3 (dobry dom) tylko rozstaje od P_RICH ludzi;
  7. okreg = wies gry najblizsza budynkowi (siatka wies_gry) - do jej kiesy ida pieniadze za noclegi.
Potok f10-uklad (trzy kroki, z katalogu repo; G = --geo D:/Backup-Bannerlord/geo-dane-2026-10-16 --teren D:/Backup-Bannerlord/drogi-siec/a-teren
--drogi Armoury/ModuleData/arm_map_roads_geo.tsv):
  1. plan miejsc (ludzie z ukladu v6; bez sprawdzania wiosek pobocznych - uklad v7 dopiero je postawi; wsie gry omijane od razu):
     python -I tools/zajazdy/zajazdy_gen.py G --wioski Armoury/ModuleData/arm_map_villages_v6.tsv --plan-out tools/zajazdy/zajazdy-miejsca.tsv
  2. uklad wiosek v7 (trzyma wioski >= min_village planu od budynku i postoju): python -I tools/wioski-v7/v7_uklad.py
  3. plik zajazdow (miejsca planu, ludzie z ukladu v7, pelne sprawdzenie - budynek >= MIN_VIL od kazdej wioski i wsi gry):
     python -I tools/zajazdy/zajazdy_gen.py G --wioski Armoury/ModuleData/arm_map_villages.tsv --plan-in tools/zajazdy/zajazdy-miejsca.tsv --out Armoury/ModuleData/arm_roadside_inns.tsv
  Dawny plik (sw-zajazdy, uklad v5/v6) zostaje jako arm_roadside_inns_v6.tsv - RoadsideInns czyta go przy ukladzie wiosek v6 i starszym.
"""
import argparse
import io
import json
import math
import os
import sys
import zlib
from collections import defaultdict

SCENE_CRC = "146201570"     # ROT 8.1.8 Main_map (jak arm_map_villages.tsv)
MIN_VIL = 10.0              # od wioski pobocznej i wsi gry (f10-uklad: miejsca zajazdow rezerwowane przed ukladem v7, ktory trzyma od nich wioski tyle z dala)
MIN_TOWN = 15.0             # od miasta (miasta maja wlasne karczmy - TownInns)
MIN_CASTLE = 10.0           # od zamku
R_PEOPLE = 30.0             # promien liczenia ludzi wokol
P_MIN = 12000               # ponizej - bez zajazdu
P_REF = 40000               # przy tylu ludziach odstep = dzien marszu
P_MID = 45000               # od tylu - druga jakosc (porzadna karczma)
P_RICH = 110000             # od tylu (i rozstaje) - trzecia jakosc (dobry dom)
DAY_MARCH = 45.0            # dzien marszu (sonda 16.10: 45 jedn.)
SP_MIN = 30.0
SP_MAX = 110.0
NODE_R = 1.0                # konce linii drog blizej niz tyle = jeden wezel
T_R = 1.2                   # koniec linii przy srodku innej linii (rozstaje "T")
MAIN_LEN = 300.0            # skladowa sieci malowanych drog od tylu jedn. = trakt glowny
ZJAZD_MIN = 4.0             # trasa AI zjezdza z drogi: dalej niz tyle od malowanej drogi
ZJAZD_RUN = 8               # przez co najmniej tyle kolejnych punktow trasy (ok. 1 jedn. na punkt)
EMPTY_MIN = 16.0            # pusty odcinek drogi: punkt drogi dalej niz tyle od wsi gry, miasta i zamku
EMPTY_TRACK = 16.0          # trasa AI bez malowanych drog: dalej niz tyle od osady
NO_PAINT = 15.0             # trasa AI "bez malowanych drog": zadnej malowanej drogi blizej
MERGE_R = 3.0               # kandydaci rozstajow blizej niz tyle = jeden
OFFSET = 1.1                # budynek tyle jedn. obok postoju
ROAD_CLEAR = 0.6            # budynek dalej niz tyle od srodka malowanej drogi
NO_INN_CULTURES = {"freefolk", "nightswatch"}
ESSOS_X = 750.0
ESSOS = {"qohorik", "sarnor", "ghiscari", "norvos", "pentoshi", "volantine", "myrish", "ibbenese", "yiti", "tyroshi", "qartheen",
         "lyseni", "valyrian", "summer", "khuzait", "braavosi", "lorathi", "lhazareen"}

ADJ = ["Red", "Old", "Broken", "Golden", "Sleeping", "Laughing", "Drowned", "Grey", "Weeping", "Kneeling", "Hungry", "Black", "White",
       "Blind", "Merry", "Crooked", "Silver", "Green", "Lucky", "Tired", "Wandering", "Smiling", "Burnt", "Quiet", "Jolly", "Lame",
       "Painted", "Bloody", "Honest", "Frozen"]
NOUN_W = ["Stag", "Boar", "Lion", "Wolf", "Trout", "Rose", "Flagon", "Kettle", "Anvil", "Lantern", "Plough", "Mare", "Crow", "Septon",
          "Maid", "Knight", "Bear", "Shield", "Sheaf", "Tankard", "Hound", "Owl", "Ox", "Raven", "Pilgrim", "Drover", "Fiddler", "Goose",
          "Wheel", "Oak"]
NOUN_E = ["Camel", "Caravan", "Lantern", "Elephant", "Pearl", "Saffron", "Spice", "Cat", "Pomegranate", "Silk", "Lamp", "Ox",
          "Mermaid", "Merchant", "Tiger", "Fountain", "Date Palm", "Horse", "Peacock", "Cistern"]
CROSS_NAMES = ["Crossroads Inn", "Inn at the Crossroads", "Four Ways Inn", "Waymeet Inn", "Old Crossing House"]


def fnv(s):
    h = 2166136261
    for ch in s.encode("utf-8"):
        h ^= ch
        h = (h * 16777619) & 0xFFFFFFFF
    return h


def read_tsv(path, comment="#", head_tag="#id\t"):
    with io.open(path, encoding="utf-8") as f:
        head = None
        for line in f:
            if not line.strip() or line.startswith(comment):
                if line.startswith(head_tag):
                    head = line[1:].rstrip("\r\n").split("\t")
                continue
            parts = line.rstrip("\r\n").split("\t")
            if head is None:
                head = parts
                continue
            yield dict(zip(head, parts))


def fl(v, d=None):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


class Grid:
    def __init__(self, cell):
        self.cell = cell
        self.g = defaultdict(list)

    def add(self, x, y, v):
        self.g[(int(x // self.cell), int(y // self.cell))].append((x, y, v))

    def near(self, x, y, r):
        rr = int(math.ceil(r / self.cell))
        cx, cy = int(x // self.cell), int(y // self.cell)
        for ix in range(cx - rr, cx + rr + 1):
            for iy in range(cy - rr, cy + rr + 1):
                for (px, py, v) in self.g.get((ix, iy), ()):
                    d = math.hypot(px - x, py - y)
                    if d <= r:
                        yield d, px, py, v

    def nearest(self, x, y, rmax):
        best = rmax + 1.0
        for d, _, _, _ in self.near(x, y, rmax):
            if d < best:
                best = d
        return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--geo", required=True)
    ap.add_argument("--teren", required=True)
    ap.add_argument("--wioski", required=True)
    ap.add_argument("--drogi", required=True)
    ap.add_argument("--out", default=None)
    ap.add_argument("--plan-out", dest="plan_out", default=None, help="krok 1: miejsca zajazdow do rezerwy (bez sprawdzenia wiosek pobocznych) -> plik planu")
    ap.add_argument("--plan-in", dest="plan_in", default=None, help="krok 3: miejsca z planu (uklad v7 trzymal od nich wioski z dala)")
    a = ap.parse_args()
    plan_mode = a.plan_out is not None
    if not plan_mode and not a.out:
        print("podaj --out (plik zajazdow) albo --plan-out (plan miejsc)")
        return 2
    min_vil = MIN_VIL

    # 1. siatka gry (2 jedn.): nawig, okreg (wies gry), kultura
    nav = {}
    for g in read_tsv(os.path.join(a.geo, "geo-siatka.tsv")):
        if g.get("nawig") != "tak":
            continue
        nav[(int(g["x"]) // 2, int(g["y"]) // 2)] = (g["wies_gry"], g["kultura"])

    def cell(x, y):
        return nav.get((int(round(x)) // 2, int(round(y)) // 2))

    def land_ok(x, y):
        # srodek i 4 sasiadow siatki 2 jedn. przejezdne (budynek ok. 1.6 jedn. - nie na brzegu wody ani skraju gor)
        if cell(x, y) is None:
            return False
        return all(cell(x + dx, y + dy) is not None for dx, dy in ((1.6, 0), (-1.6, 0), (0, 1.6), (0, -1.6)))

    # 2. osady: wioski poboczne (v7), wsie gry, miasta, zamki
    side = []
    for v in read_tsv(a.wioski):
        x, y, n = fl(v.get("x")), fl(v.get("y")), fl(v.get("settlements"), 0)
        if x is None or y is None:
            continue
        side.append((x, y, n * 250.0, v.get("village_id", "")))
    osady = json.load(io.open(os.path.join(a.teren, "osady.json"), encoding="utf-8"))["osady"]
    mains = [(o["x"], o["y"]) for o in osady if o["kind"] == "village"]
    towns = [(o["x"], o["y"]) for o in osady if o["kind"] == "town"]
    castles = [(o["x"], o["y"]) for o in osady if o["kind"] == "castle"]
    mg = Grid(16.0)
    for (x, y) in mains:
        mg.add(x, y, "M")
    vg = Grid(16.0)
    for (x, y, n, vid) in side:
        vg.add(x, y, ("S", n))
    for (x, y) in mains:
        vg.add(x, y, ("M", 0.0))
    tg = Grid(16.0)
    for (x, y) in towns:
        tg.add(x, y, "T")
    cg = Grid(16.0)
    for (x, y) in castles:
        cg.add(x, y, "C")
    allg = Grid(16.0)
    # pusty odcinek drogi: od osad, ktore stoja tam, gdzie postawila je gra (wsie gry, miasta, zamki) - wioski poboczne uklad v7 i tak trzyma z dala od zajazdow
    for p in mains + towns + castles + ([] if plan_mode else [(x, y) for (x, y, _, _) in side]):
        allg.add(p[0], p[1], 1)

    def people_at(x, y):
        return sum(v[1] for d, _, _, v in vg.near(x, y, R_PEOPLE) if v[0] == "S")

    def settle_d(x, y):
        return allg.nearest(x, y, 60.0)

    # 3. malowane drogi: linie, punkty co ok. 1 jedn., wezly, skladowe
    lines = defaultdict(list)
    for r in read_tsv(a.drogi, head_tag="#road\t"):
        try:
            lines[int(r["road"])].append((int(r["seq"]), float(r["x"]), float(r["y"])))
        except (KeyError, ValueError):
            continue
    for k in lines:
        lines[k].sort()
    rpts = Grid(4.0)                     # punkty drog (do odleglosci od drogi)
    samples = []                         # (x, y, dx, dy, linia)
    for k in sorted(lines):
        l = lines[k]
        for i in range(1, len(l)):
            x0, y0, x1, y1 = l[i - 1][1], l[i - 1][2], l[i][1], l[i][2]
            seg = math.hypot(x1 - x0, y1 - y0)
            n = max(1, int(math.ceil(seg / 1.0)))
            for j in range(n):
                t = j / float(n)
                x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                if seg > 1e-6:
                    samples.append((x, y, (x1 - x0) / seg, (y1 - y0) / seg, k))
                rpts.add(x, y, k)
        rpts.add(l[-1][1], l[-1][2], k)

    def road_d(x, y, r=30.0):
        return rpts.nearest(x, y, r)

    # wezly: konce linii (skupione do NODE_R) + "T" (koniec przy srodku innej linii)
    ends = []
    for k in sorted(lines):
        l = lines[k]
        ends.append((l[0][1], l[0][2], k))
        ends.append((l[-1][1], l[-1][2], k))
    nodes = []                           # [x, y, set(linie), stopien]
    for (x, y, k) in ends:
        for nd in nodes:
            if math.hypot(nd[0] - x, nd[1] - y) <= NODE_R:
                nd[2].add(k)
                nd[3] += 1
                break
        else:
            nodes.append([x, y, {k}, 1])
    for nd in nodes:
        for d, px, py, k in rpts.near(nd[0], nd[1], T_R):
            if k in nd[2]:
                continue
            l = lines[k]
            if math.hypot(l[0][1] - px, l[0][2] - py) > 2.0 and math.hypot(l[-1][1] - px, l[-1][2] - py) > 2.0:
                nd[2].add(k)
                nd[3] += 2               # linia przechodzi przez wezel - dwa ramiona
                break
    # skladowe sieci (linie polaczone wezlami) i ich dlugosc
    par = {k: k for k in lines}

    def find(k):
        while par[k] != k:
            par[k] = par[par[k]]
            k = par[k]
        return k
    for nd in nodes:
        ks = sorted(nd[2])
        for k in ks[1:]:
            ra, rb = find(ks[0]), find(k)
            if ra != rb:
                par[rb] = ra
    llen = {k: sum(math.hypot(l[i][1] - l[i - 1][1], l[i][2] - l[i - 1][2]) for i in range(1, len(l))) for k, l in lines.items()}
    clen = defaultdict(float)
    for k in lines:
        clen[find(k)] += llen[k]

    # 4. trasy AI: zjazd bocznej drogi z malowanej drogi
    routes = {}
    for r in read_tsv(os.path.join(a.geo, "geo-trasy.tsv")):
        if r.get("rodzaj") == "twierdza-twierdza" and r.get("polaczenie") == "lad":
            routes[r["trasa"]] = r
    rp = defaultdict(list)
    for p in read_tsv(os.path.join(a.geo, "geo-trasy-punkty.tsv")):
        if p["trasa"] in routes:
            rp[p["trasa"]].append((int(p["seq"]), float(p["x"]), float(p["y"])))
    for k in rp:
        rp[k].sort()
    junc = []                            # (x, y, rodzaj, skladowa)
    for nd in nodes:
        if nd[3] >= 3:
            junc.append((nd[0], nd[1], "wezel", max(clen[find(k)] for k in nd[2])))
    zjazd_n = 0
    for rid in sorted(rp, key=lambda s: int(s)):
        l = rp[rid]
        dd = [road_d(x, y, 12.0) for (_, x, y) in l]
        for i in range(1, len(l) - ZJAZD_RUN):
            if dd[i - 1] <= 1.2 and dd[i] > 1.2 and all(dd[j] > 1.2 for j in range(i, i + ZJAZD_RUN)) and max(dd[i:i + ZJAZD_RUN]) >= ZJAZD_MIN:
                x, y = l[i - 1][1], l[i - 1][2]
                ks = [k for d, _, _, k in rpts.near(x, y, 1.5)]
                junc.append((x, y, "zjazd", max([clen[find(k)] for k in ks] or [0.0])))
                zjazd_n += 1
            if dd[i] <= 1.2 and dd[i - 1] > 1.2 and all(dd[j] > 1.2 for j in range(max(0, i - ZJAZD_RUN), i)) and max(dd[max(0, i - ZJAZD_RUN):i]) >= ZJAZD_MIN:
                x, y = l[i][1], l[i][2]
                ks = [k for d, _, _, k in rpts.near(x, y, 1.5)]
                junc.append((x, y, "zjazd", max([clen[find(k)] for k in ks] or [0.0])))
                zjazd_n += 1
    merged = []
    for j in sorted(junc, key=lambda t: (0 if t[2] == "wezel" else 1, -t[3], t[0], t[1])):
        if any(math.hypot(m[0] - j[0], m[1] - j[1]) <= MERGE_R for m in merged):
            continue
        merged.append(j)

    # 5. kandydaci
    rej = defaultdict(int)
    cand = []

    def building(x, y, dx=None, dy=None):
        best = None
        for k in range(8):
            ang = k * math.pi / 4.0
            if dx is not None and k % 2 == 1:
                continue                 # przy drodze - tylko w bok i wzdluz (4 kierunki), dalej od srodka drogi
            bx, by = x + OFFSET * math.cos(ang), y + OFFSET * math.sin(ang)
            if not land_ok(bx, by):
                continue
            rd = road_d(bx, by, 3.0)
            if rd < ROAD_CLEAR:
                continue
            sc = vg.nearest(bx, by, 40.0) + 0.3 * rd
            if best is None or sc > best[0]:
                best = (sc, bx, by)
        return best

    def check(x, y, kind, prio, extra):
        c = cell(x, y)
        if c is None:
            rej["postoj poza ladem"] += 1
            return
        if c[1] in NO_INN_CULTURES:
            rej["za Murem"] += 1
            return
        b = building(x, y)
        if b is None:
            rej["budynek bez ladu"] += 1
            return
        _, bx, by = b
        dv = vg.nearest(bx, by, 40.0)
        if not plan_mode and dv < min_vil:
            rej["przy wiosce (< %.2f)" % min_vil] += 1
            return
        if plan_mode and min(mg.nearest(bx, by, 40.0), mg.nearest(x, y, 40.0)) < min_vil + 1.0:
            rej["przy wsi gry (< %.2f)" % (min_vil + 1.0)] += 1        # wsie gry stoja, gdzie postawila je gra - plan omija je od razu
            return
        if tg.nearest(bx, by, 40.0) < MIN_TOWN:
            rej["przy miescie"] += 1
            return
        if cg.nearest(bx, by, 40.0) < MIN_CASTLE:
            rej["przy zamku"] += 1
            return
        people = people_at(x, y)
        if people < P_MIN:
            rej["malo ludzi"] += 1
            return
        bc = cell(bx, by) or c
        cand.append(dict(x=x, y=y, bx=bx, by=by, kind=kind, prio=prio, people=people, vid=bc[0] or c[0], cult=c[1] or bc[1], dv=dv, extra=extra))

    if a.plan_in:
        # krok 3 (koncowy): miejsca zarezerwowane w kroku 1 (uklad v7 trzymal od nich wioski z dala) - te same miejsca, ludzie z ukladu v7, pelne sprawdzenie
        for r in read_tsv(a.plan_in):
            try:
                ex = r["extra"]
                check(float(r["x"]), float(r["y"]), r["kind"], int(r["prio"]), ex if r["kind"] == "crossroad" else float(ex))
            except (KeyError, ValueError):
                rej["zly wiersz planu"] += 1
    else:
        for (x, y, how, cl) in merged:
            check(x, y, "crossroad", 0 if cl >= MAIN_LEN else 1, how)
        seen = set()
        for (x, y, dx, dy, k) in samples:
            key = (int(x // 2), int(y // 2))
            if key in seen:
                continue
            seen.add(key)
            e = settle_d(x, y)
            if e < EMPTY_MIN:
                continue
            check(x, y, "road", 2, e)
        seen = set()
        for rid in sorted(rp, key=lambda s: int(s)):
            for (_, x, y) in rp[rid]:
                key = (int(x // 2), int(y // 2))
                if key in seen:
                    continue
                seen.add(key)
                if road_d(x, y, NO_PAINT) <= NO_PAINT:
                    continue
                e = settle_d(x, y)
                if e < EMPTY_TRACK:
                    continue
                check(x, y, "track", 3, e)

    def spacing(p):
        return max(SP_MIN, min(SP_MAX, DAY_MARCH * math.sqrt(P_REF / max(1.0, p))))

    order = sorted(cand, key=lambda c: (c["prio"], -(c["extra"] if c["kind"] != "crossroad" else 0.0), -c["people"], c["x"], c["y"]))
    chosen = []
    hb = defaultdict(list)
    HB = 20.0
    for c in order:
        r = spacing(c["people"])
        ok = True
        rr = int(math.ceil(SP_MAX / HB))
        cx, cy = int(c["x"] // HB), int(c["y"] // HB)
        for ix in range(cx - rr, cx + rr + 1):
            if not ok:
                break
            for iy in range(cy - rr, cy + rr + 1):
                for o in hb.get((ix, iy), ()):
                    if math.hypot(o["x"] - c["x"], o["y"] - c["y"]) < max(r, o["sp"]):
                        ok = False
                        break
                if not ok:
                    break
        if not ok and not a.plan_in:
            continue                     # krok 3: miejsca planu (odstep sprawdzony w kroku 1) zostaja - tylko odstep w pliku z ludzi ukladu v7
        c["sp"] = r
        c["front"] = math.degrees(math.atan2(c["y"] - c["by"], c["x"] - c["bx"])) - 90.0
        chosen.append(c)
        hb[(cx, cy)].append(c)

    if plan_mode:
        # krok 1: plan miejsc (rezerwa dla ukladu v7 - tools/wioski-v7/v7_uklad.py trzyma wioski >= min_village od budynku i postoju)
        with io.open(a.plan_out, "w", encoding="utf-8", newline="\n") as f:
            f.write("# f10-uklad: places of roadside inns reserved before the village layout v7 (tools/zajazdy/zajazdy_gen.py --plan-out; people from %s).\n"
                    % os.path.basename(a.wioski))
            f.write("# min_village: %.2f\n" % min_vil)
            f.write("#id\tx\ty\tbx\tby\tkind\tprio\textra\tpeople\n")
            for i, c in enumerate(sorted(chosen, key=lambda c: (c["x"], c["y"]))):
                ex = c["extra"] if c["kind"] == "crossroad" else "%.1f" % c["extra"]
                f.write("%d\t%.2f\t%.2f\t%.2f\t%.2f\t%s\t%d\t%s\t%d\n" % (i + 1, c["x"], c["y"], c["bx"], c["by"], c["kind"], c["prio"], ex, int(c["people"])))
        kinds = defaultdict(int)
        for c in chosen:
            kinds[c["kind"]] += 1
        print("PLAN: kandydaci %d, miejsc %d (%s) -> %s" % (len(cand), len(chosen), ", ".join("%s %d" % kv for kv in sorted(kinds.items())), a.plan_out))
        print("odrzucone: " + ", ".join("%s %d" % kv for kv in sorted(rej.items())))
        return 0

    # 6. nazwy, jakosci, id
    used = set()
    rows = []
    for i, c in enumerate(sorted(chosen, key=lambda c: (c["x"], c["y"]))):
        uid = "inn#%08x" % (zlib.crc32(("%.1f,%.1f" % (c["x"], c["y"])).encode()) & 0xFFFFFFFF)
        cross = c["kind"] == "crossroad"
        essos = c["cult"] in ESSOS or c["x"] > ESSOS_X   # Essos na wschodzie mapy ROT (Westeros do x ok. 690, Essos od ok. 815)
        name = None
        for k in range(12):
            hk = fnv(uid + ":" + str(k))
            if cross and k == 0 and (hk % 3) == 0:
                cand_name = CROSS_NAMES[hk % len(CROSS_NAMES)]
            else:
                nouns = NOUN_E if essos else NOUN_W
                cand_name = "The " + ADJ[hk % len(ADJ)] + " " + nouns[(hk // len(ADJ)) % len(nouns)]
                if essos and (hk // 7) % 3 == 0:
                    cand_name = "Caravanserai of the " + cand_name[4:]
            if cand_name not in used:
                name = cand_name
                break
        if name is None:
            name = "The Wayside Inn " + str(i + 1)
        used.add(name)
        tiers = 1
        if c["people"] >= P_MID:
            tiers = 2
        if cross and c["people"] >= P_RICH:
            tiers = 3
        rows.append((uid, name, c, tiers, c["kind"]))

    # 7. zapis
    with io.open(a.out, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Armoury: roadside inns (sw-zajazdy, Jeff 16.10: 'not too dense - the fewer people, the fewer inns'; f10-uklad, Jeff 18.10: 'not next to villages').\n")
        f.write("# scene_xml_crc: %s\n" % SCENE_CRC)
        f.write("# Generated by tools/zajazdy/zajazdy_gen.py from the painted roads of ROT (arm_map_roads_geo.tsv), the AI routes of the geography probe 16.10\n")
        f.write("# and map villages v7 - do not edit. x/y = the stopping place on the road (where a party enters); bx/by = the building, front_deg = its front yaw.\n")
        f.write("# Places: crossroads on a painted road (a junction of painted roads or where a side track leaves it; main roads first), then long empty stretches\n")
        f.write("# of a painted road (%.0f+ from any settlement), then AI tracks where no road is painted (%.0f+ from a settlement). One rule for the whole world:\n" % (EMPTY_MIN, EMPTY_TRACK))
        f.write("# spacing = %.0f x sqrt(%d / people within %.0f), clamped to %.0f..%.0f; no inn below %d people, none within %.2f of a map village or game village,\n"
                % (DAY_MARCH, P_REF, R_PEOPLE, SP_MIN, SP_MAX, P_MIN, min_vil))
        f.write("# %.0f of a town, %.0f of a castle, none beyond the Wall. tiers: 1 common room; 2 + decent inn from %d people; 3 + good house only at a crossroads\n"
                % (MIN_TOWN, MIN_CASTLE, P_MID))
        f.write("# from %d people. village_id = district whose purse takes the lodging money.\n" % P_RICH)
        f.write("#id\tuid\tname\tx\ty\tbx\tby\tfront_deg\tvillage_id\tpeople\ttiers\tkind\tspacing\tculture\n")
        for i, (uid, name, c, tiers, kind) in enumerate(rows):
            f.write("%d\t%s\t%s\t%.2f\t%.2f\t%.2f\t%.2f\t%.1f\t%s\t%d\t%d\t%s\t%.1f\t%s\n"
                    % (i + 1, uid, name, c["x"], c["y"], c["bx"], c["by"], c["front"], c["vid"], int(c["people"]), tiers, kind, c["sp"], c["cult"]))

    # podsumowanie
    tiers_n = defaultdict(int)
    kinds = defaultdict(int)
    cult_n = defaultdict(int)
    how_n = defaultdict(int)
    for uid, name, c, tiers, kind in rows:
        tiers_n[tiers] += 1
        kinds[kind] += 1
        cult_n[c["cult"]] += 1
        if kind == "crossroad":
            how_n[c["extra"] + (" (trakt glowny)" if c["prio"] == 0 else "")] += 1
    dvs = sorted(c["dv"] for _, _, c, _, _ in rows)
    nn = []
    for i, (_, _, c, _, _) in enumerate(rows):
        nn.append(min([math.hypot(c["x"] - o["x"], c["y"] - o["y"]) for j, (_, _, o, _, _) in enumerate(rows) if j != i] or [0.0]))
    print("malowane drogi: linii %d, wezlow stopnia >= 3: %d, zjazdow tras AI z drogi %d, rozstajow po scaleniu %d; trasy AI %d" % (
        len(lines), sum(1 for nd in nodes if nd[3] >= 3), zjazd_n, len(merged), len(routes)))
    print("kandydaci %d, zajazdy %d" % (len(cand), len(rows)))
    print("odrzucone: " + ", ".join("%s %d" % kv for kv in sorted(rej.items())))
    print("jakosci: " + ", ".join("%d: %d" % kv for kv in sorted(tiers_n.items())) + " | rodzaj: " + ", ".join("%s %d" % kv for kv in sorted(kinds.items()))
          + " | rozstaje: " + ", ".join("%s %d" % kv for kv in sorted(how_n.items())))
    print("kultury: " + ", ".join("%s %d" % kv for kv in sorted(cult_n.items(), key=lambda kv: -kv[1])))
    if rows:
        peo = sorted(c["people"] for _, _, c, _, _ in rows)
        print("ludzie wokol: min %d, mediana %d, max %d" % (peo[0], peo[len(peo) // 2], peo[-1]))
        print("od najblizszej wioski / wsi gry (budynek): min %.2f, mediana %.1f (prog %.2f); najblizszy zajazd: min %.1f, mediana %.1f" % (
            dvs[0], dvs[len(dvs) // 2], min_vil, min(nn), sorted(nn)[len(nn) // 2]))


if __name__ == "__main__":
    main()
