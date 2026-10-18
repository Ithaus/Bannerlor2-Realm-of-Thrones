# -*- coding: utf-8 -*-
"""
ZAJAZDY PRZY SZLAKACH (sw-zajazdy, Jeff 16.10): generator pliku Armoury/ModuleData/arm_roadside_inns.tsv.

Jeff: "osobne budynki przy szlakach, ale nie za gesto, im mniej ludzi tym mniej zajazdow, zeby nie wygladalo jak armia zajazdow".

Wejscie (sonda geografii 16.10, D:/Backup-Bannerlord/geo-dane-2026-10-16; uklad wiosek v5):
  geo-trasy.tsv          - trasy AI gry miedzy twierdzami (twierdza-twierdza, polaczenie lad)
  geo-trasy-punkty.tsv   - punkty tych tras (co ok. 1 jedn.), kolumna droga = punkt na malowanej drodze ROT
  geo-siatka.tsv         - siatka 2 jedn.: nawig, teren, wies_gry (najblizsza wies gry = okreg), d_twierdza, d_wies_gry, kultura
  arm_map_villages.tsv   - wioski poboczne (v5): polozenie, osady (po ok. 250 ludzi), wies gry
Wynik: jeden wiersz = jeden zajazd (deterministycznie - ten sam plik = te same zajazdy w kazdej kampanii).

Regula (jedna dla calego swiata, bez mnoznikow krain):
  1. kandydaci = punkty tras lad twierdza-twierdza, nie blizej niz MIN_FORT od twierdzy, MIN_VIL od wsi gry, MIN_SIDE od wioski pobocznej,
     na ladzie przejezdnym (siatka nawig = tak), nie za Murem (kultury freefolk / nightswatch - tam nikt nie trzyma zajazdow);
  2. ludzie wokol = suma ludzi wiosek pobocznych w promieniu R_PEOPLE (osady x 250); ponizej P_MIN - bez zajazdu;
  3. odstep miedzy zajazdami = DAY_MARCH x sqrt(P_REF / ludzie) w granicach [SP_MIN, SP_MAX] - im mniej ludzi, tym rzadziej;
  4. wybor zachlanny: najpierw rozdroza (punkt, przy ktorym biegnie >= 3 tras), potem malowana droga, potem wiecej ludzi; zajazd wchodzi,
     gdy zaden wybrany nie stoi blizej niz wiekszy z obu odstepow;
  5. budynek obok drogi (OFFSET jedn. w bok, strona z ladem), front do drogi; postoj partii (miejsce wejscia) = punkt na trasie;
  6. jakosci: 1 (wspolna izba) - zawsze; 2 (porzadna karczma) przy ludziach >= P_MID; 3 (dobry dom) tylko rozdroze przy ludziach >= P_RICH;
  7. okreg = wies gry najblizsza budynkowi (siatka wies_gry) - do jej kiesy ida pieniadze za noclegi.
Uzycie:
  python -I tools/zajazdy/zajazdy_gen.py --geo D:/Backup-Bannerlord/geo-dane-2026-10-16 --wioski Armoury/ModuleData/arm_map_villages.tsv
                                         --out Armoury/ModuleData/arm_roadside_inns.tsv
"""
import argparse
import math
import os
import sys
import zlib
from collections import defaultdict

SCENE_CRC = "146201570"     # ROT 8.1.8 Main_map (jak arm_map_villages.tsv)
MIN_FORT = 10.0             # od twierdzy (miasta maja wlasne karczmy - TownInns)
MIN_VIL = 5.0               # od wsi gry
MIN_SIDE = 2.6              # od wioski pobocznej (obrazki ~2.6 x 1.4 jedn.)
R_PEOPLE = 30.0             # promien liczenia ludzi wokol
P_MIN = 12000               # ponizej - bez zajazdu
P_REF = 40000               # przy tylu ludziach odstep = dzien marszu
P_MID = 45000               # od tylu - druga jakosc (porzadna karczma)
P_RICH = 110000             # od tylu (i rozdroze) - trzecia jakosc (dobry dom)
DAY_MARCH = 45.0            # dzien marszu (sonda 16.10: 45 jedn.)
SP_MIN = 30.0
SP_MAX = 110.0
CROSS_R = 1.6               # rozdroze: inne trasy w tym promieniu
CROSS_MIN = 3               # tyle roznych tras = rozdroze
OFFSET = 0.9                # budynek tyle jedn. w bok od drogi
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


def read_tsv(path, comment="#"):
    with open(path, encoding="utf-8") as f:
        head = None
        for line in f:
            if not line.strip() or line.startswith(comment):
                if line.startswith("#id\t"):
                    head = line[1:].rstrip("\n").split("\t")
                continue
            parts = line.rstrip("\n").split("\t")
            if head is None:
                head = parts
                continue
            yield dict(zip(head, parts))


def fl(v, d=None):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--geo", required=True)
    ap.add_argument("--wioski", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()

    # 1. trasy lad twierdza-twierdza
    routes = {}
    for r in read_tsv(os.path.join(a.geo, "geo-trasy.tsv")):
        if r.get("rodzaj") == "twierdza-twierdza" and r.get("polaczenie") == "lad":
            routes[r["trasa"]] = r
    pts = defaultdict(list)
    for p in read_tsv(os.path.join(a.geo, "geo-trasy-punkty.tsv")):
        if p["trasa"] in routes:
            pts[p["trasa"]].append((int(p["seq"]), float(p["x"]), float(p["y"]), p.get("droga") == "1"))
    for k in pts:
        pts[k].sort()

    # 2. siatka (tylko komorki przejezdne)
    grid = {}
    for g in read_tsv(os.path.join(a.geo, "geo-siatka.tsv")):
        if g.get("nawig") != "tak":
            continue
        grid[(int(g["x"]) // 2, int(g["y"]) // 2)] = (g["teren"], g["wies_gry"], fl(g["d_wies_gry"], 99.0), fl(g["d_twierdza"], 99.0), g["kultura"], g["twierdza"])

    def cell(x, y):
        return grid.get((int(round(x)) // 2, int(round(y)) // 2))

    # 3. wioski poboczne - ludzie wokol
    side = []
    for v in read_tsv(a.wioski):
        x, y, n = fl(v.get("x")), fl(v.get("y")), fl(v.get("settlements"), 0)
        if x is None or y is None:
            continue
        side.append((x, y, n * 250.0, v.get("village_id", "")))
    B = 10.0
    sb = defaultdict(list)
    for i, (x, y, n, vid) in enumerate(side):
        sb[(int(x // B), int(y // B))].append(i)

    def near_side(x, y, r):
        res = []
        rr = int(math.ceil(r / B))
        cx, cy = int(x // B), int(y // B)
        for ix in range(cx - rr, cx + rr + 1):
            for iy in range(cy - rr, cy + rr + 1):
                for i in sb.get((ix, iy), ()):
                    sx, sy, n, vid = side[i]
                    d = math.hypot(sx - x, sy - y)
                    if d <= r:
                        res.append((d, n, vid))
        return res

    # 4. punkty wszystkich tras w siatce 2 jedn. (rozdroza)
    ph = defaultdict(set)
    for rid, l in pts.items():
        for (_, x, y, _) in l:
            ph[(int(x // 2), int(y // 2))].add(rid)

    def crossing(x, y):
        ids = set()
        cx, cy = int(x // 2), int(y // 2)
        for ix in (cx - 1, cx, cx + 1):
            for iy in (cy - 1, cy, cy + 1):
                ids |= ph.get((ix, iy), set())
        return len(ids)

    # 5. kandydaci (scalone co 1 jedn.)
    cand = {}
    rej = defaultdict(int)
    for rid in sorted(pts, key=lambda s: int(s)):
        l = pts[rid]
        cum = [0.0]
        for i in range(1, len(l)):
            cum.append(cum[-1] + math.hypot(l[i][1] - l[i - 1][1], l[i][2] - l[i - 1][2]))
        total = cum[-1] if cum else 0.0
        for i, (seq, x, y, road) in enumerate(l):
            key = (int(round(x)), int(round(y)))
            if key in cand:
                continue
            if cum[i] < MIN_FORT or total - cum[i] < MIN_FORT:
                rej["przy bramie"] += 1
                continue
            c = cell(x, y)
            if c is None:
                rej["poza ladem"] += 1
                continue
            teren, vid, dvil, dfort, cult, fort = c
            if cult in NO_INN_CULTURES:
                rej["za Murem"] += 1
                continue
            if dfort < MIN_FORT:
                rej["blisko twierdzy"] += 1
                continue
            if dvil < MIN_VIL:
                rej["blisko wsi gry"] += 1
                continue
            ns = near_side(x, y, R_PEOPLE)
            if any(d < MIN_SIDE for d, _, _ in ns):
                rej["blisko wioski"] += 1
                continue
            people = sum(n for _, n, _ in ns)
            if people < P_MIN:
                rej["malo ludzi"] += 1
                continue
            # kierunek drogi z sasiednich punktow
            i0, i1 = max(0, i - 3), min(len(l) - 1, i + 3)
            dx, dy = l[i1][1] - l[i0][1], l[i1][2] - l[i0][2]
            dl = math.hypot(dx, dy)
            if dl < 0.5:
                rej["bez kierunku"] += 1
                continue
            cand[key] = dict(x=x, y=y, road=road, people=people, cross=crossing(x, y), dx=dx / dl, dy=dy / dl, vid=vid, cult=cult,
                             teren=teren, rid=rid)

    def spacing(p):
        return max(SP_MIN, min(SP_MAX, DAY_MARCH * math.sqrt(P_REF / max(1.0, p))))

    order = sorted(cand.values(), key=lambda c: (-(1 if c["cross"] >= CROSS_MIN else 0), -(1 if c["road"] else 0), -c["people"], c["x"], c["y"]))
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
        if not ok:
            continue
        # budynek obok drogi: strona z ladem (siatka), dalej od wiosek
        best = None
        for side_sign in (1, -1):
            bx = c["x"] - c["dy"] * OFFSET * side_sign
            by = c["y"] + c["dx"] * OFFSET * side_sign
            bc = cell(bx, by)
            if bc is None:
                continue
            dmin = min([d for d, _, _ in near_side(bx, by, 6.0)] or [99.0])
            sc = dmin
            if best is None or sc > best[0]:
                best = (sc, bx, by, side_sign, bc)
        if best is None:
            rej["budynek bez ladu"] += 1
            continue
        _, bx, by, side_sign, bc = best
        c["bx"], c["by"] = bx, by
        c["vid"] = bc[1] or c["vid"]
        # front (lokalna +Y) do drogi: z budynku ku punktowi postoju
        fx, fy = c["x"] - bx, c["y"] - by
        c["front"] = math.degrees(math.atan2(fy, fx)) - 90.0
        c["sp"] = r
        chosen.append(c)
        hb[(cx, cy)].append(c)

    # 6. nazwy, jakosci, id
    used = set()
    rows = []
    for i, c in enumerate(sorted(chosen, key=lambda c: (c["x"], c["y"]))):
        uid = "inn#%08x" % (zlib.crc32(("%.1f,%.1f" % (c["x"], c["y"])).encode()) & 0xFFFFFFFF)
        h = fnv(uid)
        cross = c["cross"] >= CROSS_MIN
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
        kind = "crossroad" if cross else ("road" if c["road"] else "track")
        rows.append((uid, name, c, tiers, kind))

    # 7. zapis
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Armoury: roadside inns (sw-zajazdy, Jeff 16.10: 'not too dense - the fewer people, the fewer inns'). One row = one inn.\n")
        f.write("# scene_xml_crc: %s\n" % SCENE_CRC)
        f.write("# Generated by tools/zajazdy/zajazdy_gen.py from the geography probe 16.10 (AI routes between fortresses, ROT 8.1.8) and map villages v5\n")
        f.write("# - do not edit. x/y = the stopping place on the road (where a party enters); bx/by = the building, front_deg = its front (local +Y) yaw.\n")
        f.write("# Rule (one for the whole world): spacing = %.0f x sqrt(%d / people within %.0f), clamped to %.0f..%.0f; no inn below %d people,\n"
                % (DAY_MARCH, P_REF, R_PEOPLE, SP_MIN, SP_MAX, P_MIN))
        f.write("# none within %.0f of a fortress, %.0f of a game village, %.1f of a map village, none beyond the Wall. tiers: 1 common room; 2 + decent\n"
                % (MIN_FORT, MIN_VIL, MIN_SIDE))
        f.write("# inn from %d people; 3 + good house only at a crossroads from %d people. village_id = district whose purse takes the lodging money.\n"
                % (P_MID, P_RICH))
        f.write("#id\tuid\tname\tx\ty\tbx\tby\tfront_deg\tvillage_id\tpeople\ttiers\tkind\tspacing\tculture\n")
        for i, (uid, name, c, tiers, kind) in enumerate(rows):
            f.write("%d\t%s\t%s\t%.2f\t%.2f\t%.2f\t%.2f\t%.1f\t%s\t%d\t%d\t%s\t%.1f\t%s\n"
                    % (i + 1, uid, name, c["x"], c["y"], c["bx"], c["by"], c["front"], c["vid"], int(c["people"]), tiers, kind, c["sp"], c["cult"]))

    # podsumowanie
    tiers_n = defaultdict(int)
    kinds = defaultdict(int)
    cult_n = defaultdict(int)
    for uid, name, c, tiers, kind in rows:
        tiers_n[tiers] += 1
        kinds[kind] += 1
        cult_n[c["cult"]] += 1
    print("trasy %d, punkty %d, kandydaci %d, zajazdy %d" % (len(routes), sum(len(v) for v in pts.values()), len(cand), len(rows)))
    print("odrzucone punkty: " + ", ".join("%s %d" % kv for kv in sorted(rej.items())))
    print("jakosci: " + ", ".join("%d: %d" % kv for kv in sorted(tiers_n.items())) + " | rodzaj: " + ", ".join("%s %d" % kv for kv in sorted(kinds.items())))
    print("kultury: " + ", ".join("%s %d" % kv for kv in sorted(cult_n.items(), key=lambda kv: -kv[1])))
    peo = sorted(c["people"] for _, _, c, _, _ in rows)
    if peo:
        print("ludzie wokol: min %d, mediana %d, max %d" % (peo[0], peo[len(peo) // 2], peo[-1]))


if __name__ == "__main__":
    main()
