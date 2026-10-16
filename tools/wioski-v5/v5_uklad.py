# -*- coding: utf-8 -*-
# UKLAD WIOSEK v5 (16.10, Jeff: "niektore sa za blisko i za gesto", "mniej wiosek o 20% ... 20% wiecej ludzi w danej wiosce,
# ale zeby nie bylo identycznie, ze w kazdej jest 4000 ludzi").
# Wejscie (tylko odczyt, z repo): Armoury/ModuleData/arm_map_villages_v4.tsv i arm_map_village_lore_v4.tsv (pliki v4 bez zmian).
# Wyjscie: Armoury/ModuleData/arm_map_villages.tsv (v5), arm_map_village_lore.tsv (v5: te same zdania lore dla wiosek, ktore zostaly,
# nowy naglowek layout_crc) i tools/wioski-v5/raport-v5.txt. Przebieg deterministyczny (ten sam plik za kazdym razem).
#
# Uzycie (z katalogu repo):  python -I tools/wioski-v5/v5_uklad.py [--okregi <okregi.tsv generatora v4>] [--sprawdz]
#   --okregi  - tabela okregow generatora v4 (kolumna osad = osady okregu przy generacji): tylko do oceny w raporcie, ile okregow
#               ksiega pomniejszy (wioski + najwieksza > osady okregu); bez niej ta linia raportu jest pomijana
#   --sprawdz - tylko proba wiernosci: przelicza order / toward_village / neighbors pliku v4 BEZ usuwania i porownuje z plikiem
#               (kod wyjscia 1 przy roznicy) - dowod, ze kolumny v5 licza sie regula generatora v4
#
# D2 MNIEJ I RZADZIEJ: zachlannie usuwana wioska, ktorej najblizsza sasiadka (inna wioska poboczna, dowolny okreg) stoi najblizej:
#   para o najmniejszej odleglosci; z pary odpada ta z mniejsza liczba osad, przy rownej - "field" pierwsza, dalej ta z blizsza
#   druga sasiadka (ciasniej), na koncu uid. Okreg, ktory mial wioski, zostaje z co najmniej jedna; wsi gry nie ruszamy (nie sa w pliku).
#   Cel: 80% wiosek (2446 -> 1957).
# Kolumny przeliczone regula generatora v4 (gen/generator_v4.py, P10): order = kolejnosc wedlug (road_dist, uid) w okregu;
#   toward_village = najblizszy POZOSTALY przodek po drodze (lancuch toward_village v4 z pominieciem usunietych - drogi z rastra
#   generatora nie ma w repo; usunieta wioska lezala na drodze do wioski, wiec nastepna na tej drodze to jej przodek);
#   neighbors = 4 najblizsze po drzewie drog (rd_v + rd_u - 2 rd_wspolnego_przodka, remis uid) - wzor generatora;
#   level = poziom z ludzi wioski (>= 9000 -> 3, >= 5000 -> 2, inaczej 1), jak generator; class, kind, model, polozenie,
#   obrot, obrys, road_dist, id, uid i nazwa bez zmian (to cechy miejsca, nie zaleza od sasiadow).
# D3 ROZNE WIELKOSCI: osady wioski = (osady wiosek okregu w v4 / wiosek okregu w v5) x waga miejsca / srednia waga x czynnik z uid /
#   sredni czynnik, zaokraglone, w granicach 6..36 (1500..9000 ludzi przy 250 na osade). Waga miejsca: skrzyzowanie 1.45, most 1.4,
#   brod 1.35, rzeka 1.15 (przystan / widly - klasa H - 1.35), jezioro 1.15, brzeg 1.1 (port - klasa H - 1.3), droga 1.0, pole 0.6.
#   Czynnik z uid 0.70..1.45 (fnv1a). Ludzie okregu w grze sie nie zmieniaja (n okregu z ludzi, VillageBook.EnsureN) - plik dzieli
#   tylko n miedzy wioski i wies gry, a skoro wiosek jest mniej, srednia wioska jest wieksza.
import io, os, sys, zlib
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
MD = os.path.join(REPO, "Armoury", "ModuleData")
IN_LAYOUT = os.path.join(MD, "arm_map_villages_v4.tsv")
IN_LORE = os.path.join(MD, "arm_map_village_lore_v4.tsv")
OUT_LAYOUT = os.path.join(MD, "arm_map_villages.tsv")
OUT_LORE = os.path.join(MD, "arm_map_village_lore.tsv")
OUT_REPORT = os.path.join(HERE, "raport-v5.txt")

KEEP_SHARE = 0.80
PAIR_R = 14.0                     # pary sasiadow do tej odleglosci (usuwane sa pary do ok. 6.5 jedn.)
S_MIN, S_MAX = 6, 36
F_LO, F_HI = 0.70, 1.45
WEIGHT = {"crossroad": 1.45, "bridge": 1.40, "ford": 1.35, "river": 1.15, "lake": 1.15, "coast": 1.10, "road": 1.00, "field": 0.60}
WEIGHT_H = {"river": 1.35, "coast": 1.30}   # klasa H: przystan rzeczna / widly, port / ujscie / zatoka
DIST_LO, DIST_HI = 0.85, 1.15     # suma osad wiosek okregu v5 wobec v4 w tych granicach
KNEE, KNEE_K = 28.0, 0.5          # ponad 28 osad wzrost o polowe (miekki sufit przed 36)
IN_NGEN = os.path.join(HERE, "okregi_n_v4.tsv")


def read_ngen(path):
    out = {}
    if not os.path.exists(path):
        return out
    for raw in io.open(path, encoding="utf-8"):
        line = raw.rstrip("\r\n")
        if not line or line[0] == "#" or line.startswith("id\t"):
            continue
        c = line.split("\t")
        try:
            out[c[0]] = int(c[1])
        except (IndexError, ValueError):
            pass
    return out


N_GEN = read_ngen(IN_NGEN)


def fnv1a(s):
    h = 0x811C9DC5
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def read_layout(path):
    head, cols, rows = [], None, []
    for raw in io.open(path, encoding="utf-8-sig"):
        line = raw.rstrip("\r\n")
        if not line.strip():
            continue
        if line[0] == "#":
            if line[1:].startswith("id\t"):
                cols = [c.strip() for c in line[1:].split("\t")]
            head.append(line)
            continue
        c = line.split("\t")
        r = dict(zip(cols, c))
        r["_c"] = c
        rows.append(r)
    return head, cols, rows


def num(s):
    return float(s)


def recompute(rows_d):
    """order / toward_village / neighbors okregu wedlug regul generatora v4 (P10). rows_d - POZOSTALE wioski okregu,
    kazda z polem _tw (najblizszy pozostaly przodek albo '')."""
    byu = {r["uid"]: r for r in rows_d}
    for k, r in enumerate(sorted(rows_d, key=lambda r: (num(r["road_dist"]), r["uid"]))):
        r["_order"] = k

    def anc(r):
        out = [r["uid"]]
        seen = {r["uid"]}
        while byu[out[-1]]["_tw"] and byu[out[-1]]["_tw"] not in seen:
            out.append(byu[out[-1]]["_tw"])
            seen.add(out[-1])
        return out

    A = {r["uid"]: anc(r) for r in rows_d}
    for r in rows_d:
        dl = []
        av = A[r["uid"]]
        for u in rows_d:
            if u is r:
                continue
            au = set(A[u["uid"]])
            lca = next((q for q in av if q in au), None)
            rl = num(byu[lca]["road_dist"]) if lca else 0.0
            dl.append((num(r["road_dist"]) + num(u["road_dist"]) - 2 * rl, u["uid"]))
        dl.sort()
        r["_nb"] = [u for _, u in dl[:4]]


def nn_stats(pts):
    """najblizsza sasiadka kazdej wioski (inne wioski poboczne): min, mediana, 10%, srednia."""
    cell = 8.0
    grid = defaultdict(list)
    for i, (x, y) in enumerate(pts):
        grid[(int(x // cell), int(y // cell))].append(i)
    out = []
    for i, (x, y) in enumerate(pts):
        gx, gy = int(x // cell), int(y // cell)
        best = None
        r = 1
        while True:
            for ix in range(gx - r, gx + r + 1):
                for iy in range(gy - r, gy + r + 1):
                    for j in grid.get((ix, iy), ()):
                        if j == i:
                            continue
                        d = ((pts[j][0] - x) ** 2 + (pts[j][1] - y) ** 2) ** 0.5
                        if best is None or d < best:
                            best = d
            if best is not None and best <= r * cell:
                break
            r += 1
            if r > 60:
                break
        out.append(best if best is not None else float("inf"))
    s = sorted(out)
    n = len(s)
    med = s[n // 2] if n % 2 else 0.5 * (s[n // 2 - 1] + s[n // 2])
    return dict(min=s[0], med=med, p10=s[n // 10], mean=sum(s) / n, under5=sum(1 for v in s if v < 5.0),
                under55=sum(1 for v in s if v < 5.5), under6=sum(1 for v in s if v < 6.0))


def crowd(pts, rad=10.0):
    """ile wiosek ma >= 6 sasiadow w promieniu 10 jedn. (miara DYWAN generatora v1/v2) i najwiecej sasiadow."""
    cell = rad
    grid = defaultdict(list)
    for i, (x, y) in enumerate(pts):
        grid[(int(x // cell), int(y // cell))].append(i)
    cnt = []
    for i, (x, y) in enumerate(pts):
        gx, gy = int(x // cell), int(y // cell)
        k = 0
        for ix in (gx - 1, gx, gx + 1):
            for iy in (gy - 1, gy, gy + 1):
                for j in grid.get((ix, iy), ()):
                    if j != i and (pts[j][0] - x) ** 2 + (pts[j][1] - y) ** 2 <= rad * rad:
                        k += 1
        cnt.append(k)
    return sum(1 for k in cnt if k >= 6), max(cnt), sum(cnt) / len(cnt)


def main():
    args = sys.argv[1:]
    okregi = args[args.index("--okregi") + 1] if "--okregi" in args else None
    head, cols, rows = read_layout(IN_LAYOUT)
    need = ["id", "village_id", "name", "x", "y", "settlements", "order", "kind", "uid", "class", "level", "road_dist",
            "toward_village", "neighbors"]
    miss = [c for c in need if c not in cols]
    if miss:
        print("BRAK kolumn w v4: " + ",".join(miss))
        return 2
    ci = {c: i for i, c in enumerate(cols)}
    for r in rows:
        r["_x"], r["_y"] = num(r["x"]), num(r["y"])
        r["_s"] = int(r["settlements"])
    by_uid = {r["uid"]: r for r in rows}
    dist = defaultdict(list)
    for r in rows:
        dist[r["village_id"]].append(r)

    # ---------- proba wiernosci (bez usuwania: przeliczone kolumny = kolumny v4)
    if "--sprawdz" in args:
        bad = Counter()
        for did, rs in dist.items():
            for r in rs:
                r["_tw"] = r["toward_village"] if r["toward_village"] != "-" else ""
            recompute(rs)
            for r in rs:
                if str(r["_order"]) != r["order"]:
                    bad["order"] += 1
                nb = ",".join(r["_nb"]) or "-"
                if nb != r["neighbors"]:
                    bad["neighbors"] += 1
        print("proba wiernosci v4 (bez usuwania): wiosek %d, roznice: %s" % (len(rows), dict(bad) or "brak"))
        return 1 if bad else 0

    # ---------- D2: zachlanne usuwanie
    cell = PAIR_R
    grid = defaultdict(list)
    for i, r in enumerate(rows):
        grid[(int(r["_x"] // cell), int(r["_y"] // cell))].append(i)
    pairs = []
    near = defaultdict(list)                      # i -> [(d, j)] posortowane (druga sasiadka przy remisie)
    for i, r in enumerate(rows):
        gx, gy = int(r["_x"] // cell), int(r["_y"] // cell)
        for ix in (gx - 1, gx, gx + 1):
            for iy in (gy - 1, gy, gy + 1):
                for j in grid.get((ix, iy), ()):
                    if j <= i:
                        continue
                    u = rows[j]
                    d = ((u["_x"] - r["_x"]) ** 2 + (u["_y"] - r["_y"]) ** 2) ** 0.5
                    if d <= PAIR_R:
                        pairs.append((d, r["uid"], u["uid"], i, j))
                        near[i].append((d, j))
                        near[j].append((d, i))
    for i in near:
        near[i].sort()
    pairs.sort()
    alive = [True] * len(rows)
    left = Counter(r["village_id"] for r in rows)
    target = int(round(KEEP_SHARE * len(rows)))
    kept = len(rows)
    removed, blocked = [], 0

    def second(i):
        k = 0
        for d, j in near[i]:
            if alive[j]:
                k += 1
                if k == 2:
                    return d
        return 1e9

    for d, ua, ub, i, j in pairs:
        if kept <= target:
            break
        if not (alive[i] and alive[j]):
            continue
        cand = [k for k in (i, j) if left[rows[k]["village_id"]] > 1]
        if not cand:
            blocked += 1
            continue
        if len(cand) == 2:
            cand.sort(key=lambda k: (rows[k]["_s"], 0 if rows[k]["kind"] == "field" else 1, second(k), rows[k]["uid"]))
        v = cand[0]
        alive[v] = False
        left[rows[v]["village_id"]] -= 1
        kept -= 1
        removed.append((d, rows[v]["uid"], rows[v]["kind"], rows[v]["_s"]))

    keep = [r for k, r in enumerate(rows) if alive[k]]
    keep_set = {r["uid"] for r in keep}

    # ---------- kolumny okregu: toward_village (pozostaly przodek), order, neighbors
    for r in rows:
        r["_tw0"] = r["toward_village"] if r["toward_village"] != "-" else ""
    for r in keep:
        t, seen = r["_tw0"], set()
        while t and t not in keep_set and t not in seen:
            seen.add(t)
            t = by_uid[t]["_tw0"] if t in by_uid else ""
        r["_tw"] = t if t in keep_set else ""
    kdist = defaultdict(list)
    for r in keep:
        kdist[r["village_id"]].append(r)
    for did, rs in kdist.items():
        recompute(rs)

    # ---------- D3: osady wioski
    s_old = {did: sum(r["_s"] for r in rs) for did, rs in dist.items()}

    def weight(r):
        if r["class"] == "H" and r["kind"] in WEIGHT_H:
            return WEIGHT_H[r["kind"]]
        return WEIGHT.get(r["kind"], 1.0)

    for r in keep:
        r["_w"] = weight(r)
        r["_f"] = F_LO + (F_HI - F_LO) * (fnv1a(r["uid"] + "|v5") / 4294967296.0)
    wbar = sum(r["_w"] for r in keep) / len(keep)
    fbar = 0.5 * (F_LO + F_HI)
    clamped = Counter()
    budget_hit = budget_short = 0
    for did, rs in kdist.items():
        base = s_old[did] / float(len(rs))
        raw = [base * (r["_w"] / wbar) * (r["_f"] / fbar) for r in rs]
        # suma okregu blisko v4 (+-15%): wies gry zostaje przy swojej czesci n, a jedna wioska okregu nadal zalezy od miejsca i uid
        c = s_old[did] / sum(raw)
        c = min(DIST_HI, max(DIST_LO, c))
        for r, q in zip(rs, raw):
            q *= c
            if q > KNEE:
                q = KNEE + (q - KNEE) * KNEE_K          # miekki sufit: bez "sciany" wiosek na 36
            v = int(round(q))
            if v < S_MIN:
                v = S_MIN; clamped["min"] += 1
            if v > S_MAX:
                v = S_MAX; clamped["max"] += 1
            r["_s5"] = v
        # regula L4 generatora v4: wies gry nie mniejsza od najwiekszej wioski (wioski + najwieksza <= n okregu z generacji) -
        # inaczej ksiega pomniejsza wszystkie wioski okregu (VillageBook.Split); najpierw schodzi najwieksza
        n_gen = N_GEN.get(did)
        if n_gen is not None:
            hit = False
            while sum(r["_s5"] for r in rs) + max(r["_s5"] for r in rs) > n_gen:
                big = max(rs, key=lambda r: (r["_s5"], r["uid"]))
                if big["_s5"] <= S_MIN:
                    break
                big["_s5"] -= 1
                hit = True
            if hit:
                budget_hit += 1
            if sum(r["_s5"] for r in rs) + max(r["_s5"] for r in rs) > n_gen:
                budget_short += 1
        for r in rs:
            people = r["_s5"] * 250
            r["_lv"] = 3 if people >= 9000 else (2 if people >= 5000 else 1)

    # ---------- zapis ukladu v5 (kolejnosc wierszy jak v4)
    out = []
    out.append("# Armoury: named map villages (layout v5: 20% fewer, sizes by place; from population prototype v4: no ruler lines, picture model, ROT 8.1.8). One row = one map village")
    for h in head[1:]:
        if h[1:].startswith("id\t"):
            out.append("# v5 (tools/wioski-v5/v5_uklad.py from arm_map_villages_v4.tsv): 20% fewer villages - the one whose nearest neighbour stands")
            out.append("# closest goes first (every district keeps at least one); settlements vary by place kind and a factor from the uid (6..36,")
            out.append("# 1500..9000 people); order / toward_village / neighbors / level recomputed by the v4 generator rules; the rest unchanged.")
            out.append(h)
            continue
        out.append(h)
    for r in keep:
        c = list(r["_c"])
        c[ci["settlements"]] = str(r["_s5"])
        c[ci["level"]] = str(r["_lv"])
        c[ci["order"]] = str(r["_order"])
        c[ci["toward_village"]] = r["_tw"] or "-"
        c[ci["neighbors"]] = ",".join(r["_nb"]) or "-"
        out.append("\t".join(c))
    with io.open(OUT_LAYOUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    crc_text = "".join("%s:%d;" % (r["uid"], r["_s5"]) for r in keep)
    crc5 = zlib.crc32(crc_text.encode("utf-8")) & 0xFFFFFFFF
    crc4 = zlib.crc32("".join("%s:%d;" % (r["uid"], r["_s"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF

    # ---------- lore v5: te same wiersze dla pozostalych uid, nowy naglowek bramki
    lore_out, lore_rows, lore_seen = [], 0, set()
    for raw in io.open(IN_LORE, encoding="utf-8-sig"):
        line = raw.rstrip("\r\n")
        if not line.strip():
            continue
        if line[0] == "#":
            if line[1:].strip().startswith("layout_crc:"):
                lore_out.append("# layout_crc: %08X" % crc5)
                lore_out.append("# v5: rows of the v4 lore (layout_crc %08X) kept for the villages of the v5 layout (tools/wioski-v5/v5_uklad.py) - text unchanged." % crc4)
                continue
            lore_out.append(line)
            continue
        uid = line.split("\t", 1)[0].strip()
        if uid in keep_set:
            lore_out.append(line)
            lore_rows += 1
            lore_seen.add(uid)
    with io.open(OUT_LORE, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lore_out) + "\n")

    # ---------- raport
    rep = []
    s4 = nn_stats([(r["_x"], r["_y"]) for r in rows])
    s5 = nn_stats([(r["_x"], r["_y"]) for r in keep])
    c4, c5 = crowd([(r["_x"], r["_y"]) for r in rows]), crowd([(r["_x"], r["_y"]) for r in keep])
    rep.append("UKLAD v5 (tools/wioski-v5/v5_uklad.py) z %s" % os.path.basename(IN_LAYOUT))
    rep.append("wiosek pobocznych: v4 %d -> v5 %d (usunietych %d = %.1f%%; par pominietych - obie ostatnie w okregu: %d)" % (
        len(rows), len(keep), len(removed), 100.0 * len(removed) / len(rows), blocked))
    rep.append("okregow z wioskami: v4 %d, v5 %d (kazdy zostaje z >= 1)" % (len(dist), len(kdist)))
    rep.append("najblizsza sasiadka (inne wioski poboczne, jedn. mapy): v4 min %.2f / 10%% %.2f / mediana %.2f / srednia %.2f; v5 min %.2f / 10%% %.2f / mediana %.2f / srednia %.2f" % (
        s4["min"], s4["p10"], s4["med"], s4["mean"], s5["min"], s5["p10"], s5["med"], s5["mean"]))
    rep.append("wiosek z sasiadka blizej niz 5 / 5.5 / 6 jedn.: v4 %d / %d / %d, v5 %d / %d / %d" % (
        s4["under5"], s4["under55"], s4["under6"], s5["under5"], s5["under55"], s5["under6"]))
    rep.append("gestosc (sasiedzi w promieniu 10 jedn.): wiosek z >= 6 sasiadami v4 %d, v5 %d; najwiecej v4 %d, v5 %d; srednio v4 %.2f, v5 %.2f" % (
        c4[0], c5[0], c4[1], c5[1], c4[2], c5[2]))
    rd = [d for d, _, _, _ in removed]
    rep.append("usuniete: najblizsza sasiadka od %.2f do %.2f jedn.; rodzaj miejsca: %s" % (
        min(rd), max(rd), ", ".join("%s %d" % kv for kv in Counter(k for _, _, k, _ in removed).most_common())))
    per = Counter(r["village_id"] for r in keep)
    lost = sorted(((len(dist[d]) - per[d], d) for d in dist), reverse=True)
    rep.append("okregi z najwiekszym ubytkiem (wiosek mniej): " + ", ".join("%s -%d (%d->%d)" % (d, k, len(dist[d]), per[d]) for k, d in lost[:8]))
    rep.append("wiosek w okregu v5: " + ", ".join("%d: %d" % kv for kv in sorted(Counter(per.values()).items())))
    sv = sorted(r["_s5"] for r in keep)
    n = len(sv)
    hist = Counter(sv)
    top = hist.most_common(1)[0]
    rep.append("osady wioski v5: min %d, mediana %d, max %d, srednia %.2f (ludzi przy 250: %d / %d / %d); v4: min %d, mediana %d, max %d, srednia %.2f" % (
        sv[0], sv[n // 2], sv[-1], sum(sv) / float(n), sv[0] * 250, sv[n // 2] * 250, sv[-1] * 250,
        min(r["_s"] for r in rows), sorted(r["_s"] for r in rows)[len(rows) // 2], max(r["_s"] for r in rows), sum(r["_s"] for r in rows) / float(len(rows))))
    rep.append("najczestsza wartosc v5: %d osad u %d wiosek = %.1f%% (prog 15%%; v4: 16 osad u %d = %.1f%%)" % (
        top[0], top[1], 100.0 * top[1] / n, sum(1 for r in rows if r["_s"] == 16), 100.0 * sum(1 for r in rows if r["_s"] == 16) / len(rows)))
    rep.append("przyciete do granic: do %d - %d wiosek, do %d - %d wiosek" % (S_MIN, clamped["min"], S_MAX, clamped["max"]))
    rep.append("histogram osad v5 (wartosc: wiosek): " + ", ".join("%d: %d" % kv for kv in sorted(hist.items())))
    band = Counter()
    for v in sv:
        band["6-10" if v <= 10 else "11-15" if v <= 15 else "16-20" if v <= 20 else "21-25" if v <= 25 else "26-30" if v <= 30 else "31-36"] += 1
    rep.append("pasma osad v5: " + ", ".join("%s %d (%.0f%%)" % (k, band[k], 100.0 * band[k] / n) for k in ("6-10", "11-15", "16-20", "21-25", "26-30", "31-36")))
    rep.append("poziom obrazka v5: " + ", ".join("%d: %d" % kv for kv in sorted(Counter(r["_lv"] for r in keep).items()))
               + "; v4: " + ", ".join("%s: %d" % (k, v) for k, v in sorted(Counter(r["level"] for r in rows).items())))
    bykind = defaultdict(list)
    for r in keep:
        bykind[(r["class"], r["kind"])].append(r["_s5"])
    rep.append("srednie osady wedlug miejsca (klasa/rodzaj): " + ", ".join("%s/%s %.1f (%d)" % (k[0], k[1], sum(v) / len(v), len(v)) for k, v in sorted(bykind.items(), key=lambda kv: -len(kv[1]))))
    ds = []
    for did, rs in kdist.items():
        ds.append(sum(r["_s5"] for r in rs) / float(s_old[did]))
    ds.sort()
    rep.append("suma osad wiosek okregu v5 / v4 (reszta n okregu zostaje we wsi gry): min %.2f, mediana %.2f, max %.2f" % (ds[0], ds[len(ds) // 2], ds[-1]))
    rep.append("regula L4 (wioski + najwieksza <= n okregu, %s): okregow przycietych %d, nadal ponad n (wszystkie wioski po %d) %d" % (
        os.path.basename(IN_NGEN) if N_GEN else "BRAK pliku - bez reguly", budget_hit, S_MIN, budget_short))
    nmap = dict(N_GEN)
    if okregi and os.path.exists(okregi):
        hdr, nmap = None, {}
        for raw in io.open(okregi, encoding="utf-8"):
            c = raw.rstrip("\r\n").split("\t")
            if hdr is None:
                hdr = c
                continue
            o = dict(zip(hdr, c))
            try:
                nmap[o["id"]] = int(o["osad"])
            except (KeyError, ValueError):
                pass
    if nmap:
        sh4 = sh5 = known = 0
        for did, rs in dist.items():
            if did not in nmap:
                continue
            known += 1
            if sum(r["_s"] for r in rs) + max(r["_s"] for r in rs) > nmap[did]:
                sh4 += 1
            r5 = kdist[did]
            if sum(r["_s5"] for r in r5) + max(r["_s5"] for r in r5) > nmap[did]:
                sh5 += 1
        rep.append("okregi pomniejszone przez ksiege (wioski + najwieksza > osady okregu z generacji v4; w grze n z biezacych ludzi): v4 %d, v5 %d z %d" % (sh4, sh5, known))
    rep.append("crc ukladu (uid:osady; jak VillageBook): v4 %08X, v5 %08X" % (crc4, crc5))
    rep.append("lore v5: wierszy %d (wiosek v5 %d, bez lore %d), bramka layout_crc %08X" % (lore_rows, len(keep), len(keep_set - lore_seen), crc5))
    with io.open(OUT_REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rep) + "\n")
    print("\n".join(rep))
    bad = top[1] > 0.15 * n or not (1950 <= len(keep) <= 1970) or lore_rows != len(keep)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
