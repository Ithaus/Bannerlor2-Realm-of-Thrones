# -*- coding: utf-8 -*-
# SPRAWDZENIE UKLADU WIOSEK v7 (f10-uklad, recenzja 18.10) - niezalezne od generatora (tylko pliki wynikowe + sonda gry), te same miary co recenzja:
#   - wioska na ziemi innego okregu (najblizsza wies gry komorki sondy geo-siatka.tsv, kolumna wies_gry, to wies gry innego okregu),
#   - wioska na ziemi innej kultury / innego krolestwa (kolumny kultura / krolestwo komorki wobec komorki wsi gry okregu),
#   - odleglosc od wlasnej wsi gry (mediana / 90% / max) i "daleko obca" (> 2 x odleglosc do najblizszej wsi gry i > 40 jedn.),
#   - odleglosc minimalna: naglowek pliku min_distance wobec wioska-wioska i wioska-wies gry, od miasta / zamku,
#   - zajazdy: budynek - najblizsza wioska poboczna / wies gry.
# Uzycie (z katalogu repo): python -I tools/wioski-v7/sprawdz_v7.py [--geo <geo-siatka.tsv>] [--teren <a-teren>] [--uklad <plik>] [--stary <plik v6>]
#   [--zajazdy <arm_roadside_inns.tsv>]. Kod wyjscia 1, gdy prog nie jest spelniony (progi w PROGI ponizej; ten sam zestaw co raport generatora).
import argparse, io, json, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
MD = os.path.join(REPO, "Armoury", "ModuleData")

# progi (recenzja 18.10): ziemia sasiada tylko w pasie przy granicy okregu, nigdy za granica kultury, do 40 jedn. od wlasnej wsi gry
PROGI = dict(cult=0, king=0, far=0, dmax=40.0, town=12.0, castle=9.0)


def read_layout(path):
    cols, rows, head = None, [], {}
    for raw in io.open(path, encoding="utf-8-sig"):
        line = raw.rstrip("\r\n")
        if not line.strip():
            continue
        if line[0] == "#":
            if line[1:].startswith("id\t"):
                cols = line[1:].split("\t")
            elif ":" in line:
                k, v = line[1:].split(":", 1)
                head[k.strip()] = v.strip()
            continue
        rows.append(dict(zip(cols, line.split("\t"))))
    return head, rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--geo", default="D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-siatka.tsv")
    ap.add_argument("--teren", default="D:/Backup-Bannerlord/drogi-siec/a-teren")
    ap.add_argument("--uklad", default=os.path.join(MD, "arm_map_villages.tsv"))
    ap.add_argument("--stary", default=os.path.join(MD, "arm_map_villages_v6.tsv"))
    ap.add_argument("--zajazdy", default=os.path.join(MD, "arm_roadside_inns.tsv"))
    a = ap.parse_args()
    geo = {}
    with io.open(a.geo, encoding="utf-8") as f:
        hd = f.readline().rstrip("\r\n").split("\t")
        ix, iy, inav, ik, ic, iw = (hd.index(k) for k in ("x", "y", "nawig", "krolestwo", "kultura", "wies_gry"))
        for line in f:
            c = line.rstrip("\r\n").split("\t")
            if c[inav] != "tak" or c[iw] == "-":
                continue
            geo[(int(c[ix]) // 2, int(c[iy]) // 2)] = (c[iw], c[ic], c[ik])

    def cell(x, y):
        bx, by = int(round(x / 2.0)), int(round(y / 2.0))
        for rr in range(0, 4):
            best = None
            for dx in range(-rr, rr + 1):
                for dy in range(-rr, rr + 1):
                    g = geo.get((bx + dx, by + dy))
                    if g is not None:
                        d = (2 * (bx + dx) - x) ** 2 + (2 * (by + dy) - y) ** 2
                        if best is None or d < best[0]:
                            best = (d, g)
            if best is not None:
                return best[1]
        return None

    osady = json.load(io.open(os.path.join(a.teren, "osady.json"), encoding="utf-8"))["osady"]
    mainv = {o["id"]: (o["x"], o["y"]) for o in osady if o["kind"] == "village"}
    mcult = {o["id"]: (o.get("culture") or "") for o in osady if o["kind"] == "village"}
    towns = [(o["x"], o["y"]) for o in osady if o["kind"] == "town"]
    castles = [(o["x"], o["y"]) for o in osady if o["kind"] == "castle"]
    home = {}
    for vid, (x, y) in mainv.items():
        g = cell(x, y)
        home[vid] = (g[1] if g else mcult[vid], g[2] if g else "-")      # kultura i krolestwo ziemi wsi gry (te same kolumny sondy co ziemia wioski)
    mlist = sorted(mainv.items())

    def measure(path):
        head, rows = read_layout(path)
        res = dict(n=len(rows), dist=0, cult=0, king=0, far=0, dmain=[], cult_pairs={}, ss=[], sm=[], town=[], castle=[], head=head, pts=[])
        pts = [(float(r["x"]), float(r["y"])) for r in rows]
        res["pts"] = pts
        for k, r in enumerate(rows):
            x, y = pts[k]
            vid = r["village_id"]
            g = cell(x, y)
            mx, my = mainv[vid]
            dm = math.hypot(x - mx, y - my)
            res["dmain"].append(dm)
            dn = min(math.hypot(x - px, y - py) for _, (px, py) in mlist)
            if dm > 2.0 * dn and dm > 40.0:
                res["far"] += 1
            if g is None:
                continue
            if g[0] != vid:
                res["dist"] += 1
            hc, hk = home[vid]
            if g[1] != hc:
                res["cult"] += 1
                key = "%s -> %s" % (hc, g[1])
                res["cult_pairs"][key] = res["cult_pairs"].get(key, 0) + 1
            if g[2] != hk:
                res["king"] += 1
        for k, (x, y) in enumerate(pts):
            res["ss"].append(min(math.hypot(x - px, y - py) for j, (px, py) in enumerate(pts) if j != k))
            res["sm"].append(min(math.hypot(x - px, y - py) for _, (px, py) in mlist))
            res["town"].append(min(math.hypot(x - px, y - py) for (px, py) in towns))
            res["castle"].append(min(math.hypot(x - px, y - py) for (px, py) in castles))
        return res

    def pct(a_, p):
        s = sorted(a_)
        return s[min(len(s) - 1, int(p * len(s)))]

    new = measure(a.uklad)
    old = measure(a.stary) if os.path.exists(a.stary) else None
    out, bad = [], []
    md = float(new["head"].get("min_distance", "0") or 0)
    td = float(new["head"].get("target_distance", "0") or 0)
    for nm, r in (("v6", old), ("v7", new)):
        if r is None:
            continue
        dm = r["dmain"]
        out.append("%s: wiosek %d | na ziemi innego okregu (wies_gry sondy) %d | innej kultury %d | innego krolestwa %d | od wlasnej wsi gry mediana %.1f / 90%% %.1f / max %.1f"
                   " | daleko obca (> 2 x najblizsza wies gry i > 40) %d" % (nm, r["n"], r["dist"], r["cult"], r["king"], pct(dm, 0.5), pct(dm, 0.9), max(dm), r["far"]))
        out.append("%s: wioska-wioska min %.2f / 10%% %.2f / mediana %.2f | wioska-wies gry min %.2f / 10%% %.2f | miasto min %.2f | zamek min %.2f" % (
            nm, min(r["ss"]), pct(r["ss"], 0.1), pct(r["ss"], 0.5), min(r["sm"]), pct(r["sm"], 0.1), min(r["town"]), min(r["castle"])))
        if r["cult_pairs"]:
            out.append("%s: innej kultury (okreg -> ziemia): " % nm + ", ".join("%s %d" % kv for kv in sorted(r["cult_pairs"].items(), key=lambda kv: -kv[1])[:16]))
    if old is not None:
        mvd = [math.hypot(new["pts"][k][0] - old["pts"][k][0], new["pts"][k][1] - old["pts"][k][1]) for k in range(min(len(new["pts"]), len(old["pts"])))]
        # nowe zlamania wobec v6 (wioska, ktora w v6 stala na swojej ziemi / kulturze, a w v7 nie)
        out.append("przeniesione wobec v6 (> 0.5 jedn.): %d" % sum(1 for v in mvd if v > 0.5))
    ss_min, sm_min = min(new["ss"]), min(new["sm"])
    below_t = sum(1 for v in new["ss"] if td > 0 and v < td - 1e-6)
    below_tm = sum(1 for v in new["sm"] if td > 0 and v < td - 1e-6)
    out.append("naglowek: min_distance %.2f, target_distance %.2f | wioska-wioska ponizej min_distance %d, wioska-wies gry ponizej %d | ponizej target_distance: wioska-wioska %d, wioska-wies gry %d" % (
        md, td, sum(1 for v in new["ss"] if v < md - 1e-6), sum(1 for v in new["sm"] if v < md - 1e-6), below_t, below_tm))
    if md <= 0 or ss_min < md - 1e-6 or sm_min < md - 1e-6:
        bad.append("odleglosc ponizej naglowka min_distance")
    if new["cult"] > max(PROGI["cult"], old["cult"] if old else 0):
        bad.append("ziemia innej kultury %d > v6 %d" % (new["cult"], old["cult"] if old else 0))
    if new["king"] > max(PROGI["king"], old["king"] if old else 0):
        bad.append("ziemia innego krolestwa %d > v6 %d" % (new["king"], old["king"] if old else 0))
    if new["far"] > max(PROGI["far"], old["far"] if old else 0):
        bad.append("daleko obca %d > v6 %d" % (new["far"], old["far"] if old else 0))
    if max(new["dmain"]) > max(PROGI["dmax"], max(old["dmain"]) if old else 0) + 1e-6:
        bad.append("od wlasnej wsi gry max %.1f > %.1f" % (max(new["dmain"]), max(PROGI["dmax"], max(old["dmain"]) if old else 0)))
    if min(new["town"]) < PROGI["town"] - 0.01 or min(new["castle"]) < PROGI["castle"] - 0.01:     # 0.01 - zapis 2 miejsca po przecinku
        bad.append("blizej warowni niz %.0f / %.0f" % (PROGI["town"], PROGI["castle"]))
    if os.path.exists(a.zajazdy):
        inns = []
        for raw in io.open(a.zajazdy, encoding="utf-8"):
            if raw.startswith("#") or not raw.strip():
                continue
            c = raw.rstrip("\r\n").split("\t")
            try:
                inns.append((float(c[5]), float(c[6]), c[11]))
            except (IndexError, ValueError):
                pass
        if inns:
            dv = [min(min(math.hypot(bx - px, by - py) for (px, py) in new["pts"]), min(math.hypot(bx - px, by - py) for _, (px, py) in mlist)) for (bx, by, _) in inns]
            lim = max(md, td)
            out.append("zajazdy %d: budynek - najblizsza wioska / wies gry min %.2f, mediana %.2f, blizej niz %.2f (odleglosc wioska-wioska ukladu) %d; rodzaj: %s" % (
                len(inns), min(dv), pct(dv, 0.5), lim, sum(1 for v in dv if v < lim - 1e-6),
                ", ".join("%s %d" % (k, sum(1 for i in inns if i[2] == k)) for k in sorted(set(i[2] for i in inns)))))
            if sum(1 for v in dv if v < lim - 1e-6) > 0:
                bad.append("zajazd blizej wioski niz %.2f" % lim)
    out.append("WYNIK: " + ("TAK" if not bad else "NIE - " + "; ".join(bad)))
    print("\n".join(out))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
