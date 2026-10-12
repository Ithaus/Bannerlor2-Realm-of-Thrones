# Badanie B: deterministyczny wybor wiary, zajecia i historii dla kazdej wioski + kontrola + 20 przykladowych dymkow.
# Wejscie: wioski_b.tsv (b_dane.py), ../wiara.csv, ../zajecia.csv, ../historia.csv.
# Wyjscie: ../dymki-przyklady.txt, ../wioski-lore-przypis.tsv (uid -> gotowe zdania), statystyki na ekran.
# Hash: FNV-1a 32 bit po bajtach UTF-8 napisu "<uid>|<sol>" (ten sam w C#: patrz B.md).
import os, csv, statistics
from collections import defaultdict, Counter

HERE = os.path.dirname(os.path.abspath(__file__))
import sys
TAG = sys.argv[1] if len(sys.argv) > 1 else ""   # podfolder wynikow (np. v4-proba)
BASE = os.path.dirname(HERE)
LORE = os.path.join(BASE, TAG) if TAG else BASE
os.makedirs(LORE, exist_ok=True)
SUF = ("_" + TAG) if TAG else ""

def fnv1a(s):
    h = 0x811C9DC5
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h

def H(uid, salt): return fnv1a(uid + "|" + salt)

def read_csv(name):
    with open(os.path.join(BASE, name), encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))

# ---------------- dane ----------------
V = list(csv.DictReader(open(os.path.join(HERE, "wioski_b%s.tsv" % SUF), encoding="utf-8"), delimiter="\t"))
ORDER = {v["uid"]: int(float(v["order"] or 0)) for v in V}          # kolejnosc palenia z pliku ukladu (b_dane.py)
CRC = open(os.path.join(HERE, "layout_crc32%s.txt" % SUF)).read().split()[0]  # crc32 pliku ukladu, do naglowka kontraktu

W = read_csv("wiara.csv"); Z = read_csv("zajecia.csv"); HI = read_csv("historia.csv")
GROUP = dict(kv.split("=") for kv in [r for r in Z if r["table"] == "group"][0]["text"].split())
CROPS = {r["group"]: r["text"] for r in Z if r["table"] == "crops"}
FARMERS = {r["group"]: r["text"] for r in Z if r["table"] == "farmers"}
FISH = [r for r in Z if r["table"] == "fish"]
MAIN = [r for r in Z if r["table"] == "main"]
PLACE = [r for r in Z if r["table"] == "place"]
FMAP = defaultdict(list)
for r in W:
    if r["table"] == "map": FMAP[r["key"]].append((r["faith"], int(r["weight"])))
FLINES = [r for r in W if r["table"] == "line"]

LIVESTOCK = {"cattle_farm", "sheep_farm", "swine_farm", "europe_horse_ranch", "vlandian_horse_ranch", "battanian_horse_ranch",
             "sturgian_horse_ranch", "desert_horse_ranch", "steppe_horse_ranch"}
GRAIN = {"wheat_farm", "flax_plant"}

def seat_mid(seat):
    return "the " + seat[4:] if seat.startswith("The ") else seat

def cap(s): return s[:1].upper() + s[1:]

def place_water(place):
    if place in ("coast", "bay", "harbour"): return "coast"
    if place == "lake": return "lake"
    return "river"

def kind_guess(v):
    if v.get("kind4"): return v["kind4"]   # plik v4: prawdziwy rodzaj obrazka, zgadywanie ponizej niepotrzebne
    # TYMCZASOWE do przykladow: plik 2449 nie ma jeszcze kolumny rodzaju v4 (mill/windmill/farm/granary/fishing/village).
    # W modzie brac kolumne z pliku v4 - ta funkcja wtedy znika.
    p, vt, c = v["place"], v["vtype"], v["cls"]
    if vt in ("fisherman", "whaler") and p in ("coast", "lake", "river"): return "fishing"
    if vt in GRAIN and p in ("river", "bridge", "ford") and v["river"]: return "mill"
    if vt in GRAIN and c == "H" and p == "crossroad": return "granary"
    if c == "F": return "farm"
    return "village"

# ---------------- wiara ----------------
def faith_of(v):
    reg = v["region"]
    keys = []
    if v["river"]: keys.append("river:%s@%s" % (v["river"], reg))
    keys += ["seat:" + v["seat"], "region:" + reg]
    for k in keys:
        if k in FMAP:
            opts = FMAP[k]; x = H(v["uid"], "faith") % 100; acc = 0
            for f, w in opts:
                acc += w
                if x < acc: return f, k
    return None, None

def faith_line(v, faith):
    c = [r for r in FLINES if r["faith"] == faith and r["cls"] in (v["cls"], "*") and (not r["region"] or v["region"] in r["region"].split("|"))]
    if not c: c = [r for r in FLINES if r["faith"] == faith]
    r = c[H(v["uid"], "faithline") % len(c)]
    return r["text"].replace("{DISTRICT}", seat_mid(v["district"])).replace("{SEAT}", seat_mid(v["seat"]))

# ---------------- zajecia ----------------
def best(rows, fields):
    # fields: lista (nazwa, wartosc, waga); wygrywa wiersz o najwyzszej sumie wag dokladnych trafien, '*' = kazdy
    # zwraca WSZYSTKIE wiersze o najwyzszym wyniku (warianty); wybor wariantu robi wolajacy z hasha
    top, ts = [], -1
    for r in rows:
        s = 0; ok = True
        for name, val, w in fields:
            rv = r[name]
            if rv == "*": continue
            if val in rv.split("|"): s += w
            else: ok = False; break
        if not ok: continue
        if s > ts: top, ts = [r], s
        elif s == ts: top.append(r)
    return top

def pick(rows, uid, salt):
    return rows[H(uid, salt) % len(rows)] if rows else None

def work_line(v, kind):
    g = GROUP.get(v["region"], "W"); vt = v["vtype"]
    k = "village" if (kind == "farm" and vt in LIVESTOCK) else kind
    m = pick(best(MAIN, [("kind", k, 4), ("vtype", vt, 2), ("group", g, 1)]), v["uid"], "work")
    river_or = v["river"] or "the river"
    fish = best(FISH, [("place", place_water(v["place"]), 2), ("group", g, 1)])[0]["text"]
    t = (m["text"].replace("{CROPS}", CROPS[g]).replace("{FARMERS}", FARMERS[g]).replace("{SEAT}", seat_mid(v["seat"]))
         .replace("{RIVER_OR}", river_or).replace("{FISH}", fish))
    pl = pick(best(PLACE, [("place", v["place"], 2), ("group", g, 1)]), v["uid"], "workplace")
    extra = ""
    if (pl and pl["text"] and ". " not in t.strip()          # drugie zdanie tylko po jednozdaniowym zajeciu
            and not (kind == "fishing" and v["place"] in ("coast", "river", "lake"))):
        extra = pl["text"].replace("{SEAT}", seat_mid(v["seat"])).replace("{RIVER_OR}", river_or)
    if extra and len(t) + 1 + len(extra) > 125: extra = ""   # dymek: zdanie zajecia najwyzej 125 znakow
    return cap(t) + ((" " + extra) if extra else ""), m["source"]

# ---------------- historia ----------------
PLACES_OK = {"bridge": ["bridge", "river"], "ford": ["ford", "river"], "crossroad": ["crossroad", "road"], "road": ["road"],
             "river": ["river"], "mouth": ["river"], "fork": ["river"], "coast": ["coast"], "bay": ["coast"], "harbour": ["coast"],
             "lake": ["lake"], "field": ["field"]}

def hist_pool(v):
    ok = set(PLACES_OK.get(v["place"], [])) | {"any"}
    out = []
    for t in HI:
        if v["region"] not in t["region"].split("|"): continue
        if t["place"] not in ok: continue
        if t["seats"] and v["seat"] not in t["seats"].split("|"): continue
        nd = t["needs"]
        if nd == "RIVER" and not v["river"]: continue
        if nd.startswith("RIVER=") and v["river"] != nd[6:]: continue
        out.append(t)
    return out

def fill_hist(t, v):
    s = (t["text"].replace("{VILLAGE}", v["name"]).replace("{DISTRICT}", seat_mid(v["district"]))
         .replace("{SEAT}", seat_mid(v["seat"])).replace("{RIVER}", v["river"]))
    return cap(s)

# E7/L P7: tematy zdan - historia nie powtarza rzeczy, o ktorej juz mowi zdanie zajecia / miejsca albo wiary tej samej wioski
# (np. "crossroads inn" + "one alehouse at a time; carters still stop here"). "sept" celowo poza lista (wiara i historia - dopuszczalne).
import re
TOPICS = {
    "inn": r"\binns?\b|alehouse|tavern|waystation|innkeeper",
    "carters": r"carter|drover|wagon|caravan",
    "landing": r"landing|quay|wharf|jetty|harbou?r",
    "barges": r"barge|boatmen|ferry|ferrym",
    "fishers": r"fisher|fishing|\bnets\b|\beels?\b",
    "bridge": r"\bbridges?\b",
    "toll": r"\btolls?\b",
    "smithy": r"smithy|smiths?\b|\bforge",
    "mill": r"\bmills?\b|miller|mill wheel|windmill",
    "maze": r"\bmaze",
    "blind_god": r"blind god",
    "weirwood": r"weirwood|heart tree",
    "red_god": r"r'hllor|red priest|red god",
    "moonsingers": r"moonsinger",
    "well": r"village well|\bthe well\b",
}
TOPIC_RX = {k: re.compile(v) for k, v in TOPICS.items()}

def topics(s):
    s = s.lower()
    return {k for k, rx in TOPIC_RX.items() if rx.search(s)}

def assign_history(V, avoid=None):
    """avoid: uid -> zbior tematow zdan zajecia i wiary (P7). Kolejnosc prob: wolny szablon bez tematu; szablon bez tematu (powtorka
    w okregu); wolny szablon z tematem (zgrzyt liczony); szablon z hasha."""
    avoid = avoid or {}
    by_d = defaultdict(list)
    for v in V: by_d[v["village_id"]].append(v)
    res = {}; forced = 0; clash = 0
    for d, vs in by_d.items():
        used = set()
        for v in sorted(vs, key=lambda v: (ORDER.get(v["uid"], 0), v["uid"])):
            pool = hist_pool(v)
            n = len(pool); i = H(v["uid"], "hist") % n
            av = avoid.get(v["uid"], set())
            order = [pool[(i + step) % n] for step in range(n)]
            ok = lambda t: not (topics(fill_hist(t, v)) & av)
            t = next((t for t in order if t["id"] not in used and ok(t)), None)
            if t is None:
                t = next((t for t in order if ok(t)), None)
                if t is not None: forced += 1
            if t is None:
                t = next((t for t in order if t["id"] not in used), None)
                if t is not None: clash += 1
            if t is None:
                t = pool[i]; forced += 1; clash += 1
            used.add(t["id"]); res[v["uid"]] = (t, n)
    return res, forced, clash

# ---------------- dymek ----------------
COMPANIES = {"Second Sons", "Stormcrows", "Stalwart Shields", "Golden Company"}
def holder(v):
    o = v["seat_owner"].split(",")[0].strip()
    if not o: return ""
    if o in COMPANIES: return "held by the %s from %s" % (o, v["seat"])
    return "held by House %s of %s" % (o, v["seat"])

def tooltip(v, kind, faith_t, work_t, hist_t):
    n = int(v["settlements"]); souls = n * 250
    lines = [v["name"].upper(),
             "A village of the %s lands, %s" % (v["district"][4:] if v["district"].startswith("The ") else v["district"], holder(v)),
             "{:,} souls in {} settlements".format(souls, n),
             work_t, faith_t, hist_t]
    return "\n".join(lines)

def main():
    # E7/L P7: najpierw wiara i zajecie (ich tematy omija historia), potem historia
    FW = {}
    for v in V:
        kind = kind_guess(v)
        f, key = faith_of(v); ft = faith_line(v, f)
        wt, wsrc = work_line(v, kind)
        FW[v["uid"]] = (kind, f, key, ft, wt)
    HR, forced, clash = assign_history(V, {u: topics(x[3]) | topics(x[4]) for u, x in FW.items()})
    rows = []; flen = []; wlen = []; hlen = []; pools = []
    faith_c = Counter(); hist_use = Counter(); unresolved = 0
    for v in V:
        kind, f, key, ft, wt = FW[v["uid"]]
        t, n = HR[v["uid"]]; ht = fill_hist(t, v)
        for s in (ft, wt, ht):
            if "{" in s or "}" in s: unresolved += 1
        faith_c[f] += 1; hist_use[t["id"]] += 1; pools.append(n)
        flen.append(len(ft)); wlen.append(len(wt)); hlen.append(len(ht))
        rows.append(dict(uid=v["uid"], name=v["name"], region=v["region"], district=v["district"], seat=v["seat"], place=v["place"],
                         cls=v["cls"], vtype=v["vtype"], kind_guess=kind, river=v["river"], faith=f, faith_key=key,
                         faith_text=ft, work_text=wt, hist_id=t["id"], hist_pool=n, hist_text=ht))
    with open(os.path.join(LORE, "wioski-lore-przypis.tsv"), "w", encoding="utf-8", newline="") as fo:
        w = csv.DictWriter(fo, list(rows[0].keys()), delimiter="\t", lineterminator="\n"); w.writeheader(); w.writerows(rows)
    # kontrakt TECHNIKA.md rozdz. 5: kolumny B (livelihood, faith, history) do zlaczenia z kolumnami badania A po uid
    with open(os.path.join(LORE, "arm_map_village_lore-B.tsv"), "w", encoding="utf-8", newline="\n") as fo:
        fo.write("# Armoury map village lore, part B (livelihood, faith, history). Generated by b_dymki.py, do not edit by hand.\n")
        fo.write("# layout_crc32: %s\n" % CRC)
        fo.write("#uid\tlivelihood\tfaith\thistory\n")
        for r in rows:
            fo.write("\t".join((r["uid"], r["work_text"], r["faith_text"], r["hist_text"])) + "\n")

    print("wiosek", len(rows), "nierozwiazane wstawki", unresolved, "historia: powtorki wymuszone w okregu", forced,
          "| P7 zgrzyty tematu (pula bez wyjscia)", clash)
    print("pula historii na wioske: min %d mediana %d max %d; ponizej 8: %d" % (min(pools), statistics.median(pools), max(pools),
          sum(1 for p in pools if p < 8)))
    print("dlugosc: wiara max %d, zajecie max %d, historia max %d" % (max(flen), max(wlen), max(hlen)))
    print("wiary:", faith_c.most_common())
    print("najczesciej uzyte szablony historii:", hist_use.most_common(8))
    print("szablonow historii uzytych:", len(hist_use), "z", len(HI))
    # pula na krainie bez szablonow siedzib (ogolna)
    gen = Counter()
    for t in HI:
        if t["seats"]: continue
        for k in t["region"].split("|"): gen[k] += 1
    print("szablony ogolne (bez siedzib) na kraine:", dict(sorted(gen.items())))

    # 20 przykladow z roznych krain (deterministycznie), rozne miejsca
    byreg = defaultdict(list)
    for r, v in zip(rows, V): byreg[r["region"]].append((r, v))
    regs = sorted(byreg, key=lambda k: -len(byreg[k]))[:20]
    used_place = Counter(); out = []
    for reg in regs:
        cand = sorted(byreg[reg], key=lambda rv: H(rv[0]["uid"], "example"))
        pick = None
        for r, v in cand:
            if used_place[r["place"]] < 4 and (r["hist_pool"] >= 1):
                pick = (r, v); break
        pick = pick or cand[0]
        r, v = pick; used_place[r["place"]] += 1
        out.append("[%s | %s | %s | %s | %s %s | szablon %s]\n%s" % (
            reg, r["place"], r["cls"], r["vtype"], "rodzaj v4" if v.get("kind4") else "rodzaj (przyblizony)",
            r["kind_guess"], r["hist_id"],
            tooltip(v, r["kind_guess"], r["faith_text"], r["work_text"], r["hist_text"])))
    with open(os.path.join(LORE, "dymki-przyklady.txt"), "w", encoding="utf-8", newline="\n") as fo:
        fo.write("# 20 przykladowych dymkow (po angielsku, jak w grze). Linia 2 (pan okregu) to podglad badania A z settlements.xml;\n")
        fo.write("# linie 4-6 to badanie B: zajecie (pkt 4), wiara (pkt 5), historia (pkt 6). Rodzaj obrazka: z kolumny model pliku v4, a dla pliku 2449 przyblizony (kind_guess).\n\n")
        fo.write("\n\n".join(out) + "\n")
    print("przyklady:", len(out), dict(used_place))

if __name__ == "__main__":
    main()
