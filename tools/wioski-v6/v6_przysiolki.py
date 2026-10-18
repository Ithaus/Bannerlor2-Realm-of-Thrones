# -*- coding: utf-8 -*-
# UKLAD WIOSEK v6 - PRZYSIOLKI NA PUSTKOWIACH (17.10 noc, paczka sw-przysiolki; Jeff 16.10: "daj wioski w miejscach odludnych, tez jacys ludzie zyja -
# teraz masa natkanych wiosek i masa pustego miejsca"; STAN-PRAC ### 52 W2: mniej wiosek w skupiskach, male przysiolki - pasterze, drwale, rybacy,
# weglarze - na pustkowiach, ludzie okregu bez zmian; miejsca na ladzie z sondy terenu).
#
# Wejscie (tylko odczyt): Armoury/ModuleData/arm_map_villages_v5.tsv i arm_map_village_lore_v5.tsv (uklad v5 bez zmian - stary uklad, jak _v4),
#   Armoury/ModuleData/arm_ks_land.tsv (strefa klimatu wiersza S wioski - ta sama, co w KS), tools/wioski-v5/okregi_n_v4.tsv (regula L4),
#   rastry mapy ROT (katalog a-teren: teren.npz, flora.npz, okregi.npz, drogi_raster.npz, osady.json - D:/Backup-Bannerlord/drogi-siec/a-teren)
#   i sonda terenu z gry (geo-siatka.tsv: siatka 2 jedn., kolumna nawig; geo-trasy-punkty.tsv: trasy AI miedzy twierdzami - D:/Backup-Bannerlord/geo-dane-2026-10-16).
# Wyjscie: Armoury/ModuleData/arm_map_villages.tsv (v6), arm_map_village_lore.tsv (lore v6) i tools/wioski-v6/raport-v6.txt + przysiolki-v6.tsv.
# Przebieg deterministyczny (te same wejscia = te same pliki).
#
# Uzycie (z katalogu repo): python -I tools/wioski-v6/v6_przysiolki.py [--teren <a-teren>] [--geo <geo-siatka.tsv>] [--dens N --empty E --side S --target U]
#   (--dens/--empty/--side/--target tylko do prob - plik w repo jest z wartosci domyslnych)
#
# REGULA:
#  1. Gestosc wioski = ile osad (wioski poboczne, wsie gry, zamki, miasta) stoi w promieniu 12 jedn. Kandydaci: wioski klasy R/F (nie wezly H: skrzyzowania,
#     mosty, porty zostaja) o gestosci >= DENS_MIN. Zachlannie od najgestszej (gestosc liczona od nowa po kazdym przeniesieniu - skupisko sie przerzedza).
#  2. Miejsce: ten sam okreg (raster okregow KS - okregi.npz label, ta sama ziemia co ha_okregu KS; ziemia poza rastrem - droga > 45 jedn. - do najblizszej
#     wsi gry do 60 jedn., Voronoi), komorka 1 jedn. przejezdna w rastrze (walk; bagno, rzeka, nachylenie >= 0.35 i lad nieprzejezdny poza kwadratem 5x5,
#     morze i gory - w ROT takze klify brzegu - poza 3x3) i w sondzie gry (nawig = tak w srodku i w 7 z 9 komorek 2 jedn.; brzeg rybakow 5 z 9);
#     od wszystkich osad >= EMPTY (pustka), od miast >= 14, zamkow >= 10, wsi gry >= 8, kryjowek >= 5, drog i tras AI >= 4, innych wiosek i przysiolkow >= 9, od starego miejsca
#     >= 10. Wybor: komorka najdalej od wszystkiego (pustka sie zapelnia; ponad 30 jedn. bez roznicy) z premia brzegu (rybacy). Okreg zostaje z co najmniej
#     jedna wioska nie-przysiolkiem, najwyzej MAX_SHARE wiosek okregu idzie na pustkowie i tylko wtedy, gdy pozostale wioski przyjma zwolnione osady
#     (sufit 36, L4). Przeniesien najwyzej TARGET_SHARE.
#  3. Zajecie przysiolka z ziemi miejsca - TA SAMA tabela swiata co KS (KsTab.LandRaw / ks_ziemia.py: strefa x teren x woda): najlepsze z zajec dzikiej ziemi
#     (owce, bydlo, swinie, drwal - tylko las, traper - las i snieg, mysliwy; rybak tylko nad morzem/jeziorem do 3 jedn., brzeg z premia przy wyborze miejsca). Nazwa zajecia: owce na wzgorzach/piasku = kozy (goatherds),
#     drwal w glebi puszczy co drugi = weglarze (charcoal burners). Kolumny ks_* = ziemia punktu dla KS (VillageHamlets.OverlayKs) - strefa zostaje z wiersza S.
#  4. Osady: przysiolek 4..9 osad (6 x czynnik z uid 0.75..1.4); zwolnione osady do pozostalych wiosek okregu wedlug wielkosci (sufit 36, L4: wioski +
#     najwieksza <= n okregu) - SUMA OSAD WIOSEK OKREGU = v5 (wies gry i ludzie okregu bez zmian; reszta, ktorej nie da sie dac, zostaje w przysiolku -
#     przy sprawdzeniu miejsca w kroku 2 zawsze 0).
#  5. uid bez zmian (przysiolek to ta sama wioska z ludzmi przeniesiona w nowe miejsce) - zapis v5 przechodzi na v6 po uid (VillageBook.OldLayout; recenzja:
#     kolumny v5_order / v5_x / v5_y / v5_settlements = wiersz v5 kazdej wioski, wiec wystarczy sam arm_map_villages.tsv - bez pliku _v5 w module).
#     Nazwa: Westeros - nowa nazwa z zajecia i ziemi (angielska), Essos - stara nazwa; rycerz "of <stara nazwa>" -> "of <nowa>". Lore: livelihood i historia
#     przysiolka nowe (angielskie), reszta wiersza bez zmian. order / toward_village / neighbors / level - regula generatora v4 (jak v5_uklad.py).
import argparse, io, json, math, os, sys, zlib
from collections import Counter, defaultdict, deque
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
MD = os.path.join(REPO, "Armoury", "ModuleData")
IN_LAYOUT = os.path.join(MD, "arm_map_villages_v5.tsv")
IN_LORE = os.path.join(MD, "arm_map_village_lore_v5.tsv")
IN_KSLAND = os.path.join(MD, "arm_ks_land.tsv")
IN_NGEN = os.path.join(REPO, "tools", "wioski-v5", "okregi_n_v4.tsv")
OUT_LAYOUT = os.path.join(MD, "arm_map_villages.tsv")
OUT_LORE = os.path.join(MD, "arm_map_village_lore.tsv")
OUT_REPORT = os.path.join(HERE, "raport-v6.txt")
OUT_LIST = os.path.join(HERE, "przysiolki-v6.tsv")

ap = argparse.ArgumentParser()
ap.add_argument("--teren", default="D:/Backup-Bannerlord/drogi-siec/a-teren")
ap.add_argument("--geo", default="D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-siatka.tsv")
ap.add_argument("--trasy", default="D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-trasy-punkty.tsv")
ap.add_argument("--dens", type=int, default=None)
ap.add_argument("--empty", type=float, default=None)
ap.add_argument("--side", type=float, default=None)
ap.add_argument("--target", type=float, default=None)
A = ap.parse_args()

DENS_R = 12.0          # promien gestosci
DENS_MIN = 3           # gestosc kandydata (osad w promieniu 12 jedn.; przy 4 tylko 4% wiosek - skupiska v5 maja 3-5 sasiadow)
TARGET_SHARE = 0.125   # najwyzej tyle wiosek swiata na pustkowia
MAX_SHARE = 0.40       # najwyzej tyle wiosek okregu
EMPTY = 11.0           # pustka: od kazdej osady (wioski, przysiolki, wsie gry, zamki, miasta) co najmniej tyle (sonda 16.10: pustki = ponad 12 jedn.)
D_TOWN, D_CASTLE, D_MAIN, D_HIDE, D_SIDE = 14.0, 10.0, 8.0, 5.0, 9.0
SLOPE_MAX = 0.35
D_ROAD = 4.0           # od drogi (siec drog z rastra drogi_raster: malowane ROT + wyliczone) i od tras AI miedzy twierdzami (sonda; zajazdy stoja na trasach)
MOVE_MIN = 10.0        # przysiolek co najmniej tyle od starego miejsca (inaczej to nie przeniesienie ze skupiska)
VOR_MAX = 60.0         # ziemia poza rastrem okregow KS (droga > 45 jedn.): do najblizszej wsi gry, gdy blizej niz tyle (ten sam okreg - Voronoi)
if A.dens is not None: DENS_MIN = A.dens
if A.empty is not None: EMPTY = A.empty
if A.side is not None: D_SIDE = A.side
if A.target is not None: TARGET_SHARE = A.target
H_BASE, H_LO, H_HI = 6.0, 4, 9
S_MAX = 36
FISH_SEA = 3.0
EMPTY_CAP = 30.0       # pustka ponad tyle jedn. liczy sie jak tyle (dalej tylko premia brzegu i nachylenie)
COAST_BONUS, COAST_BONUS_FISH = 3.0, 8.0
WESTEROS = {"battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "sturgia", "crownlands", "dragonstone", "freefolk", "nightswatch", "skagosi"}


def fnv1a(s):
    h = 0x811C9DC5
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def u01(s):
    return fnv1a(s) / 4294967296.0


# ---------------------------------------------------------------- tabela swiata KS (kopia KsTab / ks_ziemia.py - tylko zajecia dzikiej ziemi)
ZW = {
    '1-arktyka': {'wheat_farm': 0.25, 'cattle_farm': 0.4, 'sheep_farm': 0.6, 'swine_farm': 0.3, 'lumberjack': 1.0, 'trapper': 1.0},
    '2-wyspy': {'wheat_farm': 0.35, 'flax_plant': 0.2, 'cattle_farm': 0.5, 'sheep_farm': 1.0, 'swine_farm': 0.3, 'lumberjack': 0.4, 'trapper': 0.4},
    '2-zimno': {'wheat_farm': 1.0, 'flax_plant': 0.6, 'cattle_farm': 0.8, 'sheep_farm': 1.0, 'swine_farm': 0.7, 'lumberjack': 1.0, 'trapper': 1.0},
    '3-umiark': {'wheat_farm': 1.0, 'flax_plant': 0.8, 'vineyard': 0.35, 'cattle_farm': 1.0, 'sheep_farm': 0.9, 'swine_farm': 0.8, 'lumberjack': 1.0, 'trapper': 0.8},
    '4-cieplo': {'wheat_farm': 1.0, 'flax_plant': 0.6, 'silk_plant': 0.3, 'vineyard': 1.0, 'olive_trees': 0.3, 'cattle_farm': 0.8, 'sheep_farm': 0.7, 'swine_farm': 0.6, 'lumberjack': 0.8, 'trapper': 0.4},
    '5-srodz': {'wheat_farm': 1.0, 'silk_plant': 0.7, 'vineyard': 1.0, 'olive_trees': 1.0, 'date_farm': 0.4, 'cattle_farm': 0.4, 'sheep_farm': 0.8, 'swine_farm': 0.3, 'lumberjack': 0.4},
    '6-goraco': {'wheat_farm': 0.6, 'silk_plant': 1.0, 'vineyard': 0.6, 'olive_trees': 1.0, 'date_farm': 1.0, 'cattle_farm': 0.2, 'sheep_farm': 0.7},
    '7-tropik': {'wheat_farm': 0.4, 'silk_plant': 1.0, 'date_farm': 0.8},
}
TW = {
    'snieg': {'wheat_farm': 0, 'cattle_farm': 0.3, 'swine_farm': 0.3, 'sheep_farm': 0.6, 'trapper': 1.3},
    'piasek': {'wheat_farm': 0.3, 'cattle_farm': 0.2, 'swine_farm': 0, 'sheep_farm': 0.6, 'lumberjack': 0, 'trapper': 0},
    'step': {'wheat_farm': 0.8, 'sheep_farm': 1.3, 'cattle_farm': 0.8, 'swine_farm': 0.5, 'lumberjack': 0, 'trapper': 0},
    'las': {'wheat_farm': 0.6, 'lumberjack': 1.5, 'trapper': 1.3, 'swine_farm': 1.3},
    'gory': {'wheat_farm': 0.5, 'sheep_farm': 1.3, 'cattle_farm': 0.7},
    'bagno': {'wheat_farm': 0.5, 'sheep_farm': 0.5},
}
HUNT_Z = {'1-arktyka': 1.0, '2-zimno': 0.8, '2-wyspy': 0.3, '3-umiark': 0.4, '4-cieplo': 0.3, '5-srodz': 0.2, '6-goraco': 0.2, '7-tropik': 0.4}
HUNT_T = {'las': 1.2, 'snieg': 0.8, 'gory': 0.7, 'step': 0.6, 'bagno': 0.6, 'trawa': 0.3, 'piasek': 0.1}
WILD = ['sheep_farm', 'cattle_farm', 'swine_farm', 'lumberjack', 'trapper', 'hunter']
PREF = {'sheep_farm': 1.0, 'lumberjack': 1.0, 'trapper': 0.95, 'hunter': 0.85, 'cattle_farm': 0.8, 'swine_farm': 0.75, 'fisherman': 1.1}


def river(o):
    return o['kind'] in ('river', 'bridge', 'ford') or o['d_river'] <= 3


def coast(o):
    return o['kind'] == 'coast' or o['d_sea'] <= 3 or o['model'] == 'fishing'


def land_raw(t, o):
    if t == 'hunter':
        return min(1.5, HUNT_Z.get(o['zone'], 0.3) * HUNT_T.get(o['terr'], 0.3))
    if t == 'fisherman':
        b = 1.0 if coast(o) else (0.4 if river(o) else 0.0)
        if o['zone'] == '2-wyspy' and b > 0:
            b *= 1.2
        return b
    w = ZW.get(o['zone'], {}).get(t, 0.0) * TW.get(o['terr'], {}).get(t, 1.0)
    if river(o) and t == 'cattle_farm':
        w *= 1.2
    if o['model'] == 'fishing' and t in ('cattle_farm', 'swine_farm'):
        w *= 0.5
    return min(1.5, w)


# ---------------------------------------------------------------- dane
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


def read_ks_zone(path):
    zone, dzone = {}, {}
    for raw in io.open(path, encoding="utf-8"):
        if raw.startswith("#"):
            continue
        c = raw.rstrip("\r\n").split("\t")
        if len(c) < 14:
            continue
        if c[0] == "S":
            zone[c[2]] = c[4]
        elif c[0] == "M" and c[2].endswith("#main0"):
            dzone[c[1]] = c[4]
    return zone, dzone


def recompute(rows_d):
    """order / toward_village / neighbors okregu wedlug regul generatora v4 (kopia v5_uklad.recompute)."""
    byu = {r["uid"]: r for r in rows_d}
    for k, r in enumerate(sorted(rows_d, key=lambda r: (float(r["_rd"]), r["uid"]))):
        r["_order"] = k

    def anc(r):
        out = [r["uid"]]
        seen = {r["uid"]}
        while byu[out[-1]]["_tw"] and byu[out[-1]]["_tw"] not in seen:
            out.append(byu[out[-1]]["_tw"])
            seen.add(out[-1])
        return out

    Aa = {r["uid"]: anc(r) for r in rows_d}
    for r in rows_d:
        dl = []
        av = Aa[r["uid"]]
        for u in rows_d:
            if u is r:
                continue
            au = set(Aa[u["uid"]])
            lca = next((q for q in av if q in au), None)
            rl = float(byu[lca]["_rd"]) if lca else 0.0
            dl.append((float(r["_rd"]) + float(u["_rd"]) - 2 * rl, u["uid"]))
        dl.sort()
        r["_nb"] = [u for _, u in dl[:4]]


def nn_stats(pts):
    P = np.array(pts, dtype=np.float64)
    out = []
    cell = 8.0
    grid = defaultdict(list)
    for i, (x, y) in enumerate(P):
        grid[(int(x // cell), int(y // cell))].append(i)
    for i, (x, y) in enumerate(P):
        gx, gy = int(x // cell), int(y // cell)
        best = None
        rr = 1
        while True:
            for ix in range(gx - rr, gx + rr + 1):
                for iy in range(gy - rr, gy + rr + 1):
                    for j in grid.get((ix, iy), ()):
                        if j == i:
                            continue
                        d = math.hypot(P[j][0] - x, P[j][1] - y)
                        if best is None or d < best:
                            best = d
            if (best is not None and best <= rr * cell) or rr > 60:
                break
            rr += 1
        out.append(best if best is not None else float("inf"))
    s = sorted(out)
    n = len(s)
    med = s[n // 2] if n % 2 else 0.5 * (s[n // 2 - 1] + s[n // 2])
    return dict(min=s[0], p10=s[n // 10], med=med, mean=sum(s) / n, under6=sum(1 for v in s if v < 6.0), list=out)


def crowd(pts, rad=10.0):
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
    return sum(1 for k in cnt if k >= 3), max(cnt), sum(cnt) / float(len(cnt))


# ---------------------------------------------------------------- nazwy przysiolkow (Westeros, po angielsku)
NAME_STEM = {
    "shepherds": ["Ewe", "Lamb", "Ram", "Wether", "Fleece", "Wool", "Sheep", "Shear"],
    "goatherds": ["Goat", "Buck", "Horn", "Ridge", "Crag", "Scree", "Ibex", "Stone"],
    "drovers": ["Ox", "Kine", "Byre", "Heifer", "Steer", "Bullock", "Drove", "Cow"],
    "swineherds": ["Hog", "Swine", "Boar", "Sow", "Acorn", "Mast", "Pig", "Farrow"],
    "woodcutters": ["Axe", "Timber", "Bough", "Hewer", "Stump", "Log", "Fell", "Oak"],
    "charcoal": ["Coal", "Ash", "Cinder", "Soot", "Ember", "Kiln", "Smoke", "Char"],
    "trappers": ["Snare", "Fur", "Pelt", "Marten", "Sable", "Fox", "Lynx", "Beaver"],
    "hunters": ["Stag", "Hart", "Elk", "Hound", "Deer", "Arrow", "Bow", "Roe"],
    "fishers": ["Net", "Gull", "Herring", "Cod", "Kelp", "Shell", "Oar", "Salt"],
}
NAME_END = {
    "hills": ["fell", "tor", "knoll", "rigg", "crag", "scar", "brow", "hope"],
    "forest": ["wood", "hurst", "shaw", "glade", "holt", "dell", "copse", "grove"],
    "steppe": ["down", "lea", "moor", "field", "heath", "wold", "ley", "mere"],
    "moor": ["moor", "fell", "hope", "garth", "shiel", "cairn", "howe", "rigg"],
    "desert": ["sands", "well", "dune", "rock", "stone", "hollow", "spring", "cairn"],
    "coast": ["cove", "strand", "haven", "shore", "wick", "rock", "point", "bay"],
    "lake": ["mere", "wick", "strand", "shore", "holm", "point", "cove", "landing"],
    "wilds": ["fold", "cote", "garth", "stead", "croft", "hollow", "combe", "ley"],
}
SEP_WORD = {"hollow", "rock", "stone", "point", "well", "spring", "landing", "sands", "dune", "cairn"}


def hamlet_name(occ, place, uid, used):
    stems = NAME_STEM[occ]
    ends = NAME_END.get(place, NAME_END["wilds"]) + NAME_END["wilds"]
    h = fnv1a(uid + "|name")
    for k in range(len(stems) * len(ends)):
        s = stems[(h + k * 7) % len(stems)]
        e = ends[((h >> 8) + k * 3) % len(ends)]
        if e not in SEP_WORD and s[-1].lower() == e[0]:
            continue                                   # bez "Fellley"
        nm = (s + " " + e.capitalize()) if e in SEP_WORD else (s + e)
        if nm not in used:
            return nm
    return stems[h % len(stems)] + " Hamlet " + str(h % 97)


# ---------------------------------------------------------------- lore przysiolka (po angielsku)
LIVE = {
    "shepherds": [
        "Shepherds: a few families keep sheep on the open pasture and bring the wool down to market after the shearing.",
        "Shepherds: their flocks graze land no plough has touched; the fleeces go to the weavers in town.",
        "Shepherds and their dogs: the hamlet lives on wool, mutton and ewe's milk cheese.",
    ],
    "goatherds": [
        "Goatherds: their goats climb where no sheep would go, and the hamlet lives on milk, cheese and hides.",
        "Goatherds: a handful of stone huts, pens of goats and a smoking house for the meat.",
        "Herders of goats and a few hardy sheep on the stony slopes.",
    ],
    "drovers": [
        "Drovers: they fatten cattle on the wide grass and drive them to the markets in the autumn.",
        "Cattle herders: a few families, a long byre and more cattle than people.",
    ],
    "swineherds": [
        "Swineherds: their pigs root for acorns and beech mast in the woods every autumn.",
        "Swineherds: smoked hams and bacon go down to market; the pigs go into the woods.",
    ],
    "woodcutters": [
        "Woodcutters: they fell timber in the deep woods and float or cart the logs to the nearest market.",
        "Woodcutters and sawyers: the sound of axes carries far through the trees.",
        "Foresters: they cut wood for the lord's halls and the townsfolk's hearths.",
    ],
    "charcoal": [
        "Charcoal burners: smoking clamps in the clearings turn the wood into charcoal for the smiths.",
        "Colliers of the woods: their charcoal feeds the forges of the towns; they are black to the elbows.",
    ],
    "trappers": [
        "Trappers: they set snares for marten, fox and beaver and trade the furs for grain and iron.",
        "Trappers and hunters: furs hang drying in every doorway.",
    ],
    "hunters": [
        "Hunters: they live on venison and sell hides and horn to the traders who come once a season.",
        "Hunters and fowlers: the hamlet eats what the bow brings in.",
    ],
    "fishers": [
        "Fishers: a few boats drawn up on the shingle, nets drying and fish smoking over the fires.",
        "Fishers: they take what the water gives and salt it for the market.",
        "Fishers and net-menders: the boats go out at dawn in all but the worst weather.",
    ],
}
PLACE_LINE = {
    "hills": "The hamlet clings to the hills, far from any road.",
    "forest": "The hamlet stands in a clearing deep in the woods, far from any road.",
    "steppe": "The hamlet lies alone on the open grass, a day's ride from its neighbours.",
    "moor": "The hamlet stands alone on the cold moor, far from any road.",
    "desert": "The hamlet sits by a lonely well among the stones, far from any road.",
    "coast": "The hamlet hides in a cove of a lonely shore.",
    "lake": "The hamlet stands on a lonely lakeshore.",
    "wilds": "The hamlet stands alone in the wilds, far from any road.",
}
HIST = [
    "Its families came from the crowded villages by the road, looking for land no one had claimed.",
    "Few travellers find the track here; the lord's steward comes once a year for his due.",
    "In a hard winter the hamlet can be cut off for weeks, and they keep a store of salted meat against it.",
    "Outlaws pass by now and then, and the hamlet has learned to look the other way.",
    "The oldest woman of the hamlet remembers when there was nothing here but wind.",
    "They pay their tithe in kind, and the lord's men rarely bother to climb up for more.",
]


def main():
    head, cols, rows = read_layout(IN_LAYOUT)
    need = ["id", "village_id", "name", "x", "y", "road_deg", "settlements", "order", "kind", "uid", "class", "side", "level", "road_dist",
            "toward_village", "neighbors", "front_deg", "half_len", "half_wid", "face", "model"]
    miss = [c for c in need if c not in cols]
    if miss:
        print("BRAK kolumn w v5: " + ",".join(miss))
        return 2
    ci = {c: i for i, c in enumerate(cols)}
    ngen = read_ngen(IN_NGEN)
    kzone, dzone = read_ks_zone(IN_KSLAND)
    for r in rows:
        r["_x"], r["_y"] = float(r["x"]), float(r["y"])
        r["_s"] = int(r["settlements"])
        r["_rd"] = r["road_dist"]
        r["_h"] = None
    dist = defaultdict(list)
    for r in rows:
        dist[r["village_id"]].append(r)
    s5_sum = {d: sum(r["_s"] for r in rs) for d, rs in dist.items()}

    # ---------- rastry i sonda
    J = lambda n: os.path.join(A.teren, n)
    T = np.load(J("teren.npz")); F = np.load(J("flora.npz")); OK = np.load(J("okregi.npz"))
    walk, sea, mnt, swp = T["walk"], T["sea"], T["mountain"], T["swamp"]
    rivm = T["river"] > 0
    slope = T["slope"].astype(np.float32)
    dsea, driv, dmnt = T["d_sea"].astype(np.float32), T["d_river"].astype(np.float32), T["d_mount"].astype(np.float32)
    snowy = ((T["w10_Snow"].astype(np.int32) + T["w09_ICE"].astype(np.int32)) >= 128) | T["snow"]
    forest = F["las_razem"] | T["forest"]
    deep = F["glebokosc_puszczy"].astype(np.float32)
    CODE = np.select([snowy, T["w05_Sand"] >= 128, forest, T["w04_Steppe"] >= 128, mnt, swp], [0, 1, 2, 3, 4, 5], 6).astype(np.int32)
    TN = ['snieg', 'piasek', 'las', 'step', 'gory', 'bagno', 'trawa']
    lab = OK["label"]; rdist = OK["dist"].astype(np.float32)
    vids = [str(v) for v in OK["village_ids"]]
    vidx = {v: i for i, v in enumerate(vids)}
    face = T["face"]
    H, W = walk.shape
    bad = ((~walk) & (~sea) & (~mnt)) | swp | rivm | (slope >= SLOPE_MAX)
    # kwadrat 5x5 bez zlej komorki (maksimum w oknie przez przesuniecia); morze i gory (w ROT takze klify brzegu) - kwadrat 3x3 (rybacy stoja 1.5-2.5 jedn. od wody)
    near = sea | mnt
    badw = bad | near | (~walk)
    for dy in (-2, -1, 0, 1, 2):
        for dx in (-2, -1, 0, 1, 2):
            if dx == 0 and dy == 0:
                continue
            badw |= np.roll(np.roll(bad, dy, axis=0), dx, axis=1)
            if abs(dx) <= 1 and abs(dy) <= 1:
                badw |= np.roll(np.roll(near, dy, axis=0), dx, axis=1)
    # brzeg dla rybakow (ROT: brzeg to w wiekszosci klify typu Mountain): sama komorka ladowa bez gor, 1.4..FISH_SEA jedn. od wody, bez bagna / rzeki / stromizny w 3x3
    bad3 = bad.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx or dy:
                bad3 |= np.roll(np.roll(bad, dy, axis=0), dx, axis=1)
    shore_ok = walk & (~sea) & (~mnt) & (~bad3) & (dsea >= 1.4) & (dsea <= FISH_SEA)
    badw &= ~shore_ok
    # sonda gry: nawig 2 jedn.
    gx_n, gy_n = W // 2, (H // 2) + 1
    nav = np.zeros((gy_n + 2, gx_n + 2), dtype=bool)
    with io.open(A.geo, encoding="utf-8") as f:
        f.readline()
        for line in f:
            c = line.split("\t", 3)
            if c[2] == "tak":
                xi, yi = int(c[0]) // 2, int(c[1]) // 2
                if 0 <= yi < nav.shape[0] and 0 <= xi < nav.shape[1]:
                    nav[yi, xi] = True
    # sonda: srodek "tak" i co najmniej 7 z 9 komorek 3x3 (brzeg morza: obrys przysiolka 1.1 x 0.7 jedn. na ladzie)
    navc = np.zeros(nav.shape, dtype=np.int32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            navc += np.roll(np.roll(nav, dy, axis=0), dx, axis=1).astype(np.int32)
    navw = nav & (navc >= 7)
    navs = nav & (navc >= 5)          # brzeg: srodek "tak" i 5 z 9 (reszta to woda)

    def probe_ok(x, y):
        xi, yi = int(round(x / 2.0)), int(round(y / 2.0))
        if not (0 <= yi < navw.shape[0] and 0 <= xi < navw.shape[1]):
            return False
        ix, iy = int(math.floor(x)), int(math.floor(y))
        coastal = 0 <= iy < H and 0 <= ix < W and bool(shore_ok[iy, ix])
        return bool(navs[yi, xi]) if coastal else bool(navw[yi, xi])

    osady = json.load(io.open(J("osady.json"), encoding="utf-8"))["osady"]
    towns = [(o["x"], o["y"]) for o in osady if o["kind"] == "town"]
    castles = [(o["x"], o["y"]) for o in osady if o["kind"] == "castle"]
    mains = [(o["x"], o["y"]) for o in osady if o["kind"] == "village"]
    hides = [(o["x"], o["y"]) for o in osady if o["kind"] == "hideout"]
    cult = {o["id"]: (o.get("culture") or "") for o in osady if o["kind"] == "village"}
    yy, xx = np.mgrid[0:H, 0:W]
    cx = (xx + 0.5).astype(np.float32); cy = (yy + 0.5).astype(np.float32)

    def dist_raster(pts, cap=60.0):
        d = np.full((H, W), 1e6, dtype=np.float32)
        for (x, y) in pts:
            stamp(d, x, y, cap)
        return d

    def stamp(d, x, y, cap=60.0):
        x0, x1 = int(max(0, x - cap)), int(min(W, x + cap + 1)); y0, y1 = int(max(0, y - cap)), int(min(H, y + cap + 1))
        if x0 >= x1 or y0 >= y1:
            return
        sub = np.sqrt((cx[y0:y1, x0:x1] - x) ** 2 + (cy[y0:y1, x0:x1] - y) ** 2)
        np.minimum(d[y0:y1, x0:x1], sub, out=d[y0:y1, x0:x1])

    d_town = dist_raster(towns); d_castle = dist_raster(castles); d_main = dist_raster(mains); d_hide = dist_raster(hides)
    # okreg komorki: raster okregow KS; komorki poza nim (-1, nie rdzen twierdzy -2) - najblizsza wies gry do VOR_MAX jedn. (Voronoi)
    tlab = lab.astype(np.int32).copy()
    vbest = np.full((H, W), 1e6, dtype=np.float32)
    vlab = np.full((H, W), -1, dtype=np.int32)
    for o2 in osady:
        if o2["kind"] != "village" or o2["id"] not in vidx:
            continue
        x, y = o2["x"], o2["y"]
        x0, x1 = int(max(0, x - VOR_MAX)), int(min(W, x + VOR_MAX + 1)); y0, y1 = int(max(0, y - VOR_MAX)), int(min(H, y + VOR_MAX + 1))
        sub = np.sqrt((cx[y0:y1, x0:x1] - x) ** 2 + (cy[y0:y1, x0:x1] - y) ** 2)
        upd = (sub < vbest[y0:y1, x0:x1]) & (sub <= VOR_MAX)
        vbest[y0:y1, x0:x1][upd] = sub[upd]
        vlab[y0:y1, x0:x1][upd] = vidx[o2["id"]]
    vor = (lab == -1) & (vlab >= 0)
    tlab[vor] = vlab[vor]
    # z dala od drog i tras AI (przysiolek "far from any road"; zajazdy paczki sw-zajazdy stoja przy trasach - bez nakladania obrazkow)
    DR = np.load(J("drogi_raster.npz"))
    d_road = DR["d_road"].astype(np.float32)
    route = []
    with io.open(A.trasy, encoding="utf-8") as f:
        f.readline()
        for line in f:
            c = line.split("	", 4)
            try:
                route.append((float(c[2]), float(c[3])))
            except (IndexError, ValueError):
                pass
    d_route = np.full((H, W), 1e6, dtype=np.float32)
    for (x, y) in route:
        stamp(d_route, x, y, cap=D_ROAD + 2.0)
    fixed_ok = (~badw) & (d_town >= D_TOWN) & (d_castle >= D_CASTLE) & (d_main >= D_MAIN) & (d_hide >= D_HIDE) & (tlab >= 0) & (d_road >= D_ROAD) & (d_route >= D_ROAD)
    d_fixed = np.minimum(np.minimum(d_town, d_castle), d_main)

    # ---------- gestosc i zachlanne przenoszenie
    fixed_pts = towns + castles + mains
    def build_grid(pts, cell):
        g = defaultdict(list)
        for i, (x, y) in enumerate(pts):
            g[(int(x // cell), int(y // cell))].append(i)
        return g
    fgrid = build_grid(fixed_pts, DENS_R)

    def fixed_count(x, y):
        gx, gy = int(x // DENS_R), int(y // DENS_R); k = 0
        for ix in (gx - 1, gx, gx + 1):
            for iy in (gy - 1, gy, gy + 1):
                for j in fgrid.get((ix, iy), ()):
                    px, py = fixed_pts[j]
                    if (px - x) ** 2 + (py - y) ** 2 <= DENS_R * DENS_R:
                        k += 1
        return k

    pos = [(r["_x"], r["_y"]) for r in rows]
    fixc = [fixed_count(x, y) for (x, y) in pos]
    sgrid = defaultdict(set)
    for i, (x, y) in enumerate(pos):
        sgrid[(int(x // DENS_R), int(y // DENS_R))].add(i)

    def side_count(i):
        x, y = pos[i]
        gx, gy = int(x // DENS_R), int(y // DENS_R); k = 0
        for ix in (gx - 1, gx, gx + 1):
            for iy in (gy - 1, gy, gy + 1):
                for j in sgrid.get((ix, iy), ()):
                    if j != i and (pos[j][0] - x) ** 2 + (pos[j][1] - y) ** 2 <= DENS_R * DENS_R:
                        k += 1
        return k

    def density(i):
        return side_count(i) + fixc[i]

    dens0 = [density(i) for i in range(len(rows))]
    d_side = dist_raster(pos)
    target = int(math.floor(TARGET_SHARE * len(rows)))

    # miejsce na zwolnione osady w okregu (sufit 36 i L4): przysiolek tylko wtedy, gdy pozostale wioski okregu przyjma jego osady (suma okregu = v5)
    def lim_of(did):
        n_gen = ngen.get(did)
        mx5 = max(q["_s"] for q in dist[did])
        return S_MAX if n_gen is None else min(S_MAX, max(n_gen - s5_sum[did], mx5))

    def hsize(r):
        return max(H_LO, min(H_HI, int(round(H_BASE * (0.75 + 0.65 * u01(r["uid"] + "|v6")))), r["_s"]))

    room = {did: sum(lim_of(did) - q["_s"] for q in rs) for did, rs in dist.items()}
    freed_d = Counter()
    moved = []
    per_moved = Counter()
    skipped = Counter()
    tried = set()
    cand_all = [i for i, r in enumerate(rows) if r["class"] in ("R", "F")]
    while len(moved) < target:
        best, bd = None, None
        for i in cand_all:
            if i in tried or rows[i]["_h"] is not None:
                continue
            dn = density(i)
            if dn < DENS_MIN:
                continue
            key = (dn, -rows[i]["_s"], rows[i]["uid"])
            if bd is None or key > bd:
                best, bd = i, key
        if best is None:
            break
        i = best
        tried.add(i)
        r = rows[i]
        did = r["village_id"]
        W_d = len(dist[did])
        left_non = sum(1 for q in dist[did] if q["_h"] is None)
        if left_non <= 1:
            skipped["okreg - ostatnia wioska"] += 1
            continue
        if per_moved[did] + 1 > MAX_SHARE * W_d:
            skipped["okreg - limit 40%"] += 1
            continue
        if freed_d[did] + (r["_s"] - hsize(r)) > room[did] - (lim_of(did) - r["_s"]):
            skipped["okreg - brak miejsca na osady (sufit 36 / L4)"] += 1
            continue
        li = vidx.get(did)
        if li is None:
            skipped["okreg bez rastra"] += 1
            continue
        # pustka bez tej wioski (jej stary punkt zwolniony)
        m = fixed_ok & (tlab == li)
        ys, xs = np.nonzero(m)
        if len(xs) == 0:
            skipped["okreg bez ziemi"] += 1
            continue
        # odleglosc od wiosek: raster bez tej wioski - liczone lokalnie dla komorek kandydatow
        dsd = d_side[ys, xs].copy()
        near_self = np.sqrt((cx[ys, xs] - r["_x"]) ** 2 + (cy[ys, xs] - r["_y"]) ** 2) <= dsd + 1e-3
        if near_self.any():
            sub = np.full(near_self.sum(), 1e6, dtype=np.float32)
            px, py = cx[ys, xs][near_self], cy[ys, xs][near_self]
            for k, p in enumerate(pos):
                if k == i:
                    continue
                if abs(p[0] - r["_x"]) > 120 or abs(p[1] - r["_y"]) > 120:
                    continue
                np.minimum(sub, np.sqrt((px - p[0]) ** 2 + (py - p[1]) ** 2), out=sub)
            dsd[near_self] = sub
        emp = np.minimum(dsd, d_fixed[ys, xs])
        away = np.sqrt((cx[ys, xs] - r["_x"]) ** 2 + (cy[ys, xs] - r["_y"]) ** 2) >= MOVE_MIN
        okc = (dsd >= D_SIDE) & (emp >= EMPTY) & away
        if not okc.any():
            skipped["okreg bez pustki"] += 1
            continue
        idx = np.nonzero(okc)[0]
        # komorka najdalej od wszystkiego; remis - mniejsze nachylenie, potem hash
        # brzeg (morze / jezioro do FISH_SEA jedn.): premia - rybacy na pustych brzegach; wioska rybacka v5 (obrazek fishing albo miejsce coast) - wieksza
        fishv = r["model"] == "fishing" or r["kind"] == "coast"
        cb = COAST_BONUS_FISH if fishv else COAST_BONUS
        shore = (dsea[ys, xs] <= FISH_SEA)
        score = np.minimum(emp, EMPTY_CAP) + np.where(shore, cb, 0.0)
        order = sorted(idx.tolist(), key=lambda k: (-round(float(score[k]), 1), -round(float(emp[k]), 1), float(slope[ys[k], xs[k]]), fnv1a("%s|%d|%d" % (r["uid"], xs[k], ys[k]))))
        pick = None
        for k in order[:400]:
            x0, y0 = float(xs[k]) + 0.5, float(ys[k]) + 0.5
            jx = (u01(r["uid"] + "|jx") - 0.5) * 0.6; jy = (u01(r["uid"] + "|jy") - 0.5) * 0.6
            x1, y1 = x0 + jx, y0 + jy
            jxi, jyi = int(math.floor(x1)), int(math.floor(y1))
            if not (0 <= jxi < W and 0 <= jyi < H) or not fixed_ok[jyi, jxi] or tlab[jyi, jxi] != li or not probe_ok(x1, y1):
                continue
            pick = (x1, y1, int(xs[k]), int(ys[k]), float(emp[k]))
            break
        if pick is None:
            skipped["sonda gry - brak ladu"] += 1
            continue
        x1, y1, ix, iy, e = pick
        # przeniesienie: stary punkt zwolniony, nowy zajety
        g0 = (int(r["_x"] // DENS_R), int(r["_y"] // DENS_R)); sgrid[g0].discard(i)
        pos[i] = (x1, y1)
        sgrid[(int(x1 // DENS_R), int(y1 // DENS_R))].add(i)
        fixc[i] = fixed_count(x1, y1)
        # raster odleglosci od wiosek: lokalnie od nowa wokol starego punktu, stempel nowego
        ox, oy = r["_x"], r["_y"]
        x0_, x1_ = int(max(0, ox - 60)), int(min(W, ox + 61)); y0_, y1_ = int(max(0, oy - 60)), int(min(H, oy + 61))
        loc = np.full((y1_ - y0_, x1_ - x0_), 1e6, dtype=np.float32)
        for k, p in enumerate(pos):
            if abs(p[0] - ox) > 120 or abs(p[1] - oy) > 120:
                continue
            np.minimum(loc, np.sqrt((cx[y0_:y1_, x0_:x1_] - p[0]) ** 2 + (cy[y0_:y1_, x0_:x1_] - p[1]) ** 2), out=loc)
        d_side[y0_:y1_, x0_:x1_] = loc
        stamp(d_side, x1, y1)
        r["_h"] = dict(x=x1, y=y1, ix=ix, iy=iy, empty=e, dens=bd[0], old=(ox, oy))
        moved.append(i)
        per_moved[did] += 1
        room[did] -= lim_of(did) - r["_s"]
        freed_d[did] += r["_s"] - hsize(r)

    # ---------- zajecie, miejsce, nazwa, osady przysiolka
    used_names = set(r["name"] for r in rows)
    occ_n = Counter(); start_n = Counter(); place_n = Counter(); model_n = Counter()
    sea_comp = {}

    def water_kind(ix, iy):
        # najblizsza komorka morza w promieniu 4 i wielkosc jej skladowej (jezioro < 40000 komorek)
        best = None
        for dy in range(-4, 5):
            for dx in range(-4, 5):
                x, y = ix + dx, iy + dy
                if 0 <= x < W and 0 <= y < H and sea[y, x]:
                    d = dx * dx + dy * dy
                    if best is None or d < best[0]:
                        best = (d, x, y)
        if best is None:
            return "coast"
        key = (best[1], best[2])
        for k, v in sea_comp.items():
            if key in v[1]:
                return v[0]
        seen = {key}; q = deque([key]); n = 0
        while q and n < 40000:
            x, y = q.popleft(); n += 1
            for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + ddx, y + ddy
                if 0 <= nx < W and 0 <= ny < H and sea[ny, nx] and (nx, ny) not in seen:
                    seen.add((nx, ny)); q.append((nx, ny))
        kind = "coast" if q else "lake"
        sea_comp[key] = (kind, seen if kind == "lake" else {key})
        return kind

    for i in moved:
        r = rows[i]
        h = r["_h"]
        ix, iy = h["ix"], h["iy"]
        zone = kzone.get(r["uid"]) or dzone.get(r["village_id"]) or "3-umiark"
        terr = TN[int(CODE[iy, ix])]
        o = dict(zone=zone, terr=terr, kind="field", model="farm", d_sea=float(min(255.0, dsea[iy, ix])), d_river=float(min(255.0, driv[iy, ix])),
                 d_mount=float(min(255.0, dmnt[iy, ix])))
        if o["d_river"] <= 3:
            o["kind"] = "river"
        best, bs = None, -1.0
        for t in WILD:
            # drwal tylko w lesie, traper w lesie i na sniegu (zajecie przysiolka z ziemi miejsca; wydajnosc i tak z tabeli swiata)
            if (t == "lumberjack" and terr != "las") or (t == "trapper" and terr not in ("las", "snieg")):
                continue
            v = land_raw(t, o) * PREF[t]
            if v > bs + 1e-9:
                best, bs = t, v
        fisher = False
        if o["d_sea"] <= FISH_SEA:
            of = dict(o, kind="coast", model="fishing")
            vf = land_raw("fisherman", of) * PREF["fisherman"]
            if vf >= bs:
                fisher, best, bs, o = True, "fisherman", vf, of
        if bs <= 0:
            best = "hunter"
        hills = o["d_mount"] <= 4 or float(slope[iy, ix]) >= 0.12 or terr in ("gory", "piasek")
        if best == "sheep_farm":
            occ = "goatherds" if hills else "shepherds"
        elif best == "lumberjack":
            occ = "charcoal" if float(deep[iy, ix]) >= 3 and fnv1a(r["uid"] + "|coal") % 2 == 0 else "woodcutters"
        elif best == "trapper":
            occ = "trappers"
        elif best == "hunter":
            occ = "hunters"
        elif best == "cattle_farm":
            occ = "drovers"
        elif best == "swine_farm":
            occ = "swineherds"
        else:
            occ = "fishers"
        if fisher:
            place = water_kind(ix, iy)
        elif terr == "las":
            place = "forest"
        elif terr == "snieg":
            place = "moor"
        elif terr == "piasek":
            place = "desert"
        elif terr == "step":
            place = "steppe"
        elif hills:
            place = "hills"
        else:
            place = "wilds"
        cul = cult.get(r["village_id"], "")
        if cul in WESTEROS:
            nm = hamlet_name(occ, place, r["uid"], used_names)
        else:
            nm = r["name"]
        used_names.add(nm)
        hs = hsize(r)
        # orientacja: front do wsi gry (kierunek sciezki); droga od wsi gry z rastra okregow
        mvx, mvy = None, None
        for o2 in osady:
            if o2["kind"] == "village" and o2["id"] == r["village_id"]:
                mvx, mvy = o2["x"], o2["y"]
                break
        ang = math.degrees(math.atan2((mvy if mvy is not None else h["y"]) - h["y"], (mvx if mvx is not None else h["x"]) - h["x"]))
        rd = float(rdist[iy, ix])
        if not (rd > 0 and rd < 1000):
            rd = 1.3 * math.hypot((mvx or h["x"]) - h["x"], (mvy or h["y"]) - h["y"])
        fr = ang - 90.0
        while fr <= -180.0:
            fr += 360.0
        while fr > 180.0:
            fr -= 360.0
        h.update(occ=occ, place=place, start=best, o=o, name=nm, hs=hs, front=fr, road_deg=ang % 180.0, rd=rd, face=int(face[iy, ix]),
                 model="fishing" if fisher else "farm", culture=cul, slope=float(slope[iy, ix]), deep=float(deep[iy, ix]))
        occ_n[occ] += 1; start_n[best] += 1; place_n[place] += 1; model_n[h["model"]] += 1

    # ---------- osady: przysiolek maly, zwolnione do pozostalych wiosek okregu (suma okregu = v5)
    fit = Counter()
    for did, rs in dist.items():
        hm = [r for r in rs if r["_h"] is not None]
        for r in rs:
            r["_s6"] = r["_h"]["hs"] if r["_h"] is not None else r["_s"]
        if not hm:
            continue
        freed = sum(r["_s"] - r["_s6"] for r in hm)
        keep = [r for r in rs if r["_h"] is None]
        n_gen = ngen.get(did)
        base = {r["uid"]: r["_s"] for r in keep}
        tot = float(sum(base.values()))
        give = Counter()
        lim = lim_of(did)                                  # L4: wioski + najwieksza <= n okregu (v5 juz ponad - nie wiecej niz najwieksza v5); sufit 36
        for _ in range(freed):
            def ok(r):
                return r["_s6"] + 1 <= lim
            cand = [r for r in keep if ok(r)]
            if not cand:
                break
            # najwieksza zaleglosc wobec udzialu proporcjonalnego do wielkosci v5
            done = sum(give.values())
            pick = max(cand, key=lambda r: (base[r["uid"]] / tot * (done + 1) - give[r["uid"]], r["uid"]))
            pick["_s6"] += 1
            give[pick["uid"]] += 1
        rest = freed - sum(give.values())
        if rest > 0:
            fit["reszta w przysiolkach"] += rest
            # oddane z powrotem przysiolkom (po kolei od najmniejszego), suma okregu = v5
            while rest > 0:
                small = min(hm, key=lambda r: (r["_s6"] - r["_s"] + 1000 if r["_s6"] >= r["_s"] else r["_s6"], r["uid"]))
                if small["_s6"] >= small["_s"]:
                    break
                small["_s6"] += 1
                rest -= 1
        fit["dane wioskom"] += sum(give.values())
    for r in rows:
        if r["_h"] is not None:
            r["_h"]["hs"] = r["_s6"]
        people = r["_s6"] * 250
        r["_lv"] = 1 if r["_h"] is not None else (3 if people >= 9000 else (2 if people >= 5000 else 1))

    # ---------- kolumny okregu: toward_village (pozostaly przodek - nie przysiolek), road_dist, order, neighbors
    by_uid = {r["uid"]: r for r in rows}
    hset = {r["uid"] for r in rows if r["_h"] is not None}
    for r in rows:
        r["_tw0"] = r["toward_village"] if r["toward_village"] != "-" else ""
    for r in rows:
        if r["uid"] in hset:
            r["_tw"] = ""
            r["_rd"] = "%.1f" % r["_h"]["rd"]
            continue
        t, seen = r["_tw0"], set()
        while t and (t in hset) and t not in seen:
            seen.add(t)
            t = by_uid[t]["_tw0"] if t in by_uid else ""
        r["_tw"] = t if t in by_uid and t not in hset else ""
    for did, rs in dist.items():
        recompute(rs)

    # ---------- zapis ukladu v6 (kolejnosc wierszy jak v5; nowe kolumny na koncu)
    newcols = ["hamlet", "ks_start", "ks_terr", "ks_kind", "ks_model", "ks_dsea", "ks_driver", "ks_dmount"]
    # recenzja (sw-przysiolki): wiersz v5 kazdej wioski (order, x, y, settlements - napisy z pliku v5 bez zmian) - zapis v5 przechodzi na v6 po uid z samego
    # arm_map_villages.tsv (VillageBook.OldLayout), takze gdy wgranie nie przeniesie arm_map_villages_v5.tsv
    v5cols = ["v5_order", "v5_x", "v5_y", "v5_settlements"]
    out = ["# Armoury: named map villages (layout v6: hamlets in empty land - from layout v5, the same villages and people per district). One row = one map village"]
    for hline in head[1:]:
        if hline[1:].startswith("id\t"):
            out.append("# v6 (tools/wioski-v6/v6_przysiolki.py from arm_map_villages_v5.tsv): %d villages of the densest clusters moved to empty land of the same" % len(moved))
            out.append("# district as small hamlets (shepherds, goatherds, drovers, swineherds, woodcutters, charcoal burners, trappers, hunters, fishers); the freed")
            out.append("# settlements went to the other villages of the district (district sum = v5). hamlet = occupation; ks_* = land of the hamlet point for KS")
            out.append("# (start craft, terrain, place, picture, distances to sea / river / mountains) - the same world table as arm_ks_land.tsv; '-' = not a hamlet.")
            out.append("# v5_order / v5_x / v5_y / v5_settlements = the same row in layout v5 (every row): a save of layout v5 maps by uid from this file alone.")
            out.append(hline + "\t" + "\t".join(newcols) + "\t" + "\t".join(v5cols))
            continue
        out.append(hline)
    for r in rows:
        c = list(r["_c"])
        c[ci["settlements"]] = str(r["_s6"])
        c[ci["level"]] = str(r["_lv"])
        c[ci["order"]] = str(r["_order"])
        c[ci["toward_village"]] = r["_tw"] or "-"
        c[ci["neighbors"]] = ",".join(r["_nb"]) or "-"
        h = r["_h"]
        if h is None:
            c += ["-"] * len(newcols)
        else:
            c[ci["name"]] = h["name"]
            c[ci["x"]] = "%.2f" % h["x"]
            c[ci["y"]] = "%.2f" % h["y"]
            c[ci["road_deg"]] = "%.1f" % h["road_deg"]
            c[ci["kind"]] = h["place"]
            c[ci["class"]] = "F"
            c[ci["side"]] = "0"
            c[ci["road_dist"]] = "%.1f" % h["rd"]
            c[ci["front_deg"]] = "%.1f" % h["front"]
            c[ci["half_len"]] = "1.10"
            c[ci["half_wid"]] = "0.70"
            c[ci["face"]] = str(h["face"])
            c[ci["model"]] = h["model"]
            o = h["o"]
            c += [h["occ"], h["start"], o["terr"], o["kind"], o["model"], "%.1f" % o["d_sea"], "%.1f" % o["d_river"], "%.1f" % o["d_mount"]]
        c += [r["_c"][ci[k]] for k in ("order", "x", "y", "settlements")]
        out.append("\t".join(c))
    with io.open(OUT_LAYOUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    crc6 = zlib.crc32("".join("%s:%d;" % (r["uid"], r["_s6"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF
    crc5 = zlib.crc32("".join("%s:%d;" % (r["uid"], r["_s"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF
    crc5c = zlib.crc32("".join("%s:%d;" % (r["uid"], int(r["_c"][ci["settlements"]])) for r in rows).encode("utf-8")) & 0xFFFFFFFF   # kolumny v5_* (jak OldLayout)

    # ---------- lore v6: wiersze v5, przysiolki - nowe zajecie i historia, rycerz "of <nowa nazwa>"
    lore_out, lore_rows, lore_seen, lore_h, knight_ren = [], 0, set(), 0, 0
    lcols = None
    for raw in io.open(IN_LORE, encoding="utf-8-sig"):
        line = raw.rstrip("\r\n")
        if not line.strip():
            continue
        if line[0] == "#":
            if line[1:].strip().startswith("layout_crc:"):
                lore_out.append("# layout_crc: %08X" % crc6)
                lore_out.append("# v6: rows of the v5 lore (layout_crc %08X); hamlets (tools/wioski-v6/v6_przysiolki.py) get a new livelihood and history, the rest unchanged." % crc5)
                continue
            if line[1:].startswith("uid\t"):
                lcols = line[1:].split("\t")
            lore_out.append(line)
            continue
        c = line.split("\t")
        uid = c[0].strip()
        r = by_uid.get(uid)
        if r is None:
            continue
        if r["_h"] is not None and lcols is not None:
            li = {k: j for j, k in enumerate(lcols)}
            h = r["_h"]
            while len(c) < len(lcols):
                c.append("")
            lv = LIVE[h["occ"]]
            c[li["livelihood"]] = lv[fnv1a(uid + "|live") % len(lv)] + " " + PLACE_LINE[h["place"]]
            c[li["history"]] = HIST[fnv1a(uid + "|hist") % len(HIST)]
            old = r["name"]
            kn = c[li["knight"]]
            if h["name"] != old and kn.endswith(" of " + old):
                c[li["knight"]] = kn[: -len(old)] + h["name"]
                knight_ren += 1
            lore_h += 1
            line = "\t".join(c)
        lore_out.append(line)
        lore_rows += 1
        lore_seen.add(uid)
    with io.open(OUT_LORE, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lore_out) + "\n")

    # ---------- lista przysiolkow do przegladu
    lst = ["uid\tokreg\tkultura\tstara_nazwa\tnowa_nazwa\tzajecie\tks_start\tmiejsce\tteren\tstrefa\tx5\ty5\tx6\ty6\tprzesuniecie\tgestosc\tpustka\tosady5\tosady6\tsonda"]
    for i in moved:
        r = rows[i]; h = r["_h"]
        lst.append("\t".join([r["uid"], r["village_id"], h["culture"], r["name"], h["name"], h["occ"], h["start"], h["place"], h["o"]["terr"], h["o"]["zone"],
                              "%.2f" % h["old"][0], "%.2f" % h["old"][1], "%.2f" % h["x"], "%.2f" % h["y"],
                              "%.1f" % math.hypot(h["x"] - h["old"][0], h["y"] - h["old"][1]), str(h["dens"]), "%.1f" % h["empty"], str(r["_s"]), str(r["_s6"]),
                              "tak" if probe_ok(h["x"], h["y"]) else "NIE"]))
    with io.open(OUT_LIST, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lst) + "\n")

    # ---------- raport
    rep = []
    p5 = [(r["_x"], r["_y"]) for r in rows]
    p6 = [(r["_h"]["x"], r["_h"]["y"]) if r["_h"] is not None else (r["_x"], r["_y"]) for r in rows]
    s5, s6 = nn_stats(p5), nn_stats(p6)
    c5, c6 = crowd(p5), crowd(p6)
    per5 = Counter(r["village_id"] for r in rows)
    dsum_bad = sum(1 for d, rs in dist.items() if sum(r["_s6"] for r in rs) != s5_sum[d])
    l4_bad = sum(1 for d, rs in dist.items() if d in ngen and sum(r["_s6"] for r in rs) + max(r["_s6"] for r in rs) > ngen[d]
                 and not (sum(r["_s"] for r in rs) + max(r["_s"] for r in rs) > ngen[d]))
    hs = [rows[i]["_s6"] for i in moved]
    mv = [math.hypot(rows[i]["_h"]["x"] - rows[i]["_x"], rows[i]["_h"]["y"] - rows[i]["_y"]) for i in moved]
    rep.append("UKLAD v6 (tools/wioski-v6/v6_przysiolki.py) z %s" % os.path.basename(IN_LAYOUT))
    rep.append("wiosek pobocznych: v5 %d -> v6 %d (te same uid); przeniesionych na pustkowia (przysiolki) %d = %.1f%% (cel najwyzej %.1f%%, gestosc >= %d osad w %.0f jedn.)" % (
        len(rows), len(rows), len(moved), 100.0 * len(moved) / len(rows), 100.0 * TARGET_SHARE, DENS_MIN, DENS_R))
    rep.append("pominieci kandydaci: " + (", ".join("%s %d" % kv for kv in skipped.most_common()) or "-"))
    rep.append("okregow z przysiolkami %d z %d; okregow o innej liczbie wiosek niz v5 0 (uid bez zmian); okregow o innej sumie osad wiosek niz v5 %d; L4 zlamane przez v6 %d" % (
        len(set(rows[i]["village_id"] for i in moved)), len(per5), dsum_bad, l4_bad))
    rep.append("osady wiosek swiata: v5 %d, v6 %d (wies gry i ludzie okregu bez zmian - n okregu z ludzi gry)" % (sum(r["_s"] for r in rows), sum(r["_s6"] for r in rows)))
    rep.append("osady przysiolka: min %d, mediana %d, max %d, suma %d (ludzi przy 250: %d); zwolnione dane innym wioskom okregu %d, zostalo w przysiolkach (sufit 36 / L4) %d" % (
        min(hs), sorted(hs)[len(hs) // 2], max(hs), sum(hs), 250 * sum(hs), fit["dane wioskom"], fit["reszta w przysiolkach"]))
    rep.append("przesuniecie: min %.1f, mediana %.1f, max %.1f jedn.; gestosc przenoszonych (osad w 12 jedn.): %s" % (
        min(mv), sorted(mv)[len(mv) // 2], max(mv), ", ".join("%d: %d" % kv for kv in sorted(Counter(rows[i]["_h"]["dens"] for i in moved).items()))))
    rep.append("najblizsza sasiadka (wioski poboczne): v5 min %.2f / 10%% %.2f / mediana %.2f / srednia %.2f; v6 min %.2f / 10%% %.2f / mediana %.2f / srednia %.2f" % (
        s5["min"], s5["p10"], s5["med"], s5["mean"], s6["min"], s6["p10"], s6["med"], s6["mean"]))
    rep.append("skupiska (wiosek z >= 3 sasiadami w 10 jedn.): v5 %d, v6 %d; najwiecej sasiadow v5 %d, v6 %d; srednio v5 %.2f, v6 %.2f" % (c5[0], c6[0], c5[1], c6[1], c5[2], c6[2]))
    hn = [s6["list"][i] for i in moved]
    rep.append("przysiolki: najblizsza sasiadka min %.1f / mediana %.1f; pustka miejsca (od kazdej osady) min %.1f / mediana %.1f jedn." % (
        min(hn), sorted(hn)[len(hn) // 2], min(rows[i]["_h"]["empty"] for i in moved), sorted(rows[i]["_h"]["empty"] for i in moved)[len(moved) // 2]))
    rep.append("przysiolki na ladzie: raster (walk, bagno/rzeka/nachylenie >= %.2f poza 5x5, morze/gory poza 3x3) %d/%d, sonda gry (nawig 3x3 po 2 jedn.) %d/%d" % (
        SLOPE_MAX, sum(1 for i in moved if not badw[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]), len(moved), sum(1 for i in moved if probe_ok(rows[i]["_h"]["x"], rows[i]["_h"]["y"])), len(moved)))
    rep.append("od drog i tras AI: droga min %.1f, trasa AI min %.1f (prog %.0f jedn.; punktow tras %d)" % (
        min(float(d_road[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]) for i in moved), min(float(d_route[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]) for i in moved), D_ROAD, len(route)))
    rep.append("odleglosci przysiolkow: od miast min %.1f (prog %.0f), zamkow %.1f (%.0f), wsi gry %.1f (%.0f), kryjowek %.1f (%.0f)" % (
        min(float(d_town[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]) for i in moved), D_TOWN, min(float(d_castle[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]) for i in moved), D_CASTLE,
        min(float(d_main[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]) for i in moved), D_MAIN, min(float(d_hide[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]]) for i in moved), D_HIDE))
    vor_n = sum(1 for i in moved if lab[rows[i]["_h"]["iy"], rows[i]["_h"]["ix"]] < 0)
    rep.append("ziemia przysiolka: raster okregow KS %d, poza nim (droga > 45 jedn., najblizsza wies gry do %.0f jedn.) %d" % (len(moved) - vor_n, VOR_MAX, vor_n))
    rep.append("zajecia przysiolkow: " + ", ".join("%s %d" % kv for kv in occ_n.most_common()))
    rep.append("zajecie startowe KS (ks_start, tabela swiata): " + ", ".join("%s %d" % kv for kv in start_n.most_common()))
    rep.append("miejsce (kind): " + ", ".join("%s %d" % kv for kv in place_n.most_common()) + "; obrazek: " + ", ".join("%s %d" % kv for kv in model_n.most_common()))
    cul_n = Counter(rows[i]["_h"]["culture"] for i in moved)
    rep.append("kultura okregu: " + ", ".join("%s %d" % kv for kv in cul_n.most_common()))
    fish5 = sum(1 for r in rows if r["model"] == "fishing"); fish6 = sum(1 for r in rows if (r["_h"]["model"] if r["_h"] else r["model"]) == "fishing")
    rep.append("wioski z obrazkiem rybackim (HamletKinds poza KS): v5 %d, v6 %d" % (fish5, fish6))
    rep.append("nazwy: nowe (Westeros) %d, stare (Essos) %d; rycerz 'of <nazwa>' przemianowany %d" % (
        sum(1 for i in moved if rows[i]["_h"]["name"] != rows[i]["name"]), sum(1 for i in moved if rows[i]["_h"]["name"] == rows[i]["name"]), knight_ren))
    rep.append("crc ukladu (uid:osady; jak VillageBook): v5 %08X, v6 %08X" % (crc5, crc6))
    rep.append("kolumny v5_* pliku v6 (zapis v5 po uid bez arm_map_villages_v5.tsv): crc %08X - %s" % (crc5c, "= v5" if crc5c == crc5 else "INNY NIZ v5 (BLAD)"))
    rep.append("lore v6: wierszy %d (wiosek v6 %d, bez lore %d, przysiolkow z nowym zajeciem %d), bramka layout_crc %08X" % (
        lore_rows, len(rows), len(set(by_uid) - lore_seen), lore_h, crc6))
    with io.open(OUT_REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rep) + "\n")
    print("\n".join(rep))
    badrun = dsum_bad > 0 or lore_rows != len(rows) or any(not probe_ok(rows[i]["_h"]["x"], rows[i]["_h"]["y"]) for i in moved) or crc6 == crc5 or crc5c != crc5
    return 1 if badrun else 0


if __name__ == "__main__":
    sys.exit(main())
