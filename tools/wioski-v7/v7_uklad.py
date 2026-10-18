# -*- coding: utf-8 -*-
# UKLAD WIOSEK v7 - WIOSKI PO CALYM LADZIE OKREGU (18.10, paczka f10-uklad). Jeff 18.10: "rozmieszczenie wiosek to zle wyglada, one sa za blisko nawet
# w terenach gdzie jest duzo ludzi ... sa upakowane obok siebie a potem masa terenu pusta, zwieksz dwukrotnie minimalna odleglosc wioski od wioski" oraz
# "jak robisz wioski - wykorzystaj mase terenu, boczne wioski naprawde masz gdzie, zeby to wygladalo naturalnie, a nie jak blad".
#
# Wejscie (tylko odczyt): Armoury/ModuleData/arm_map_villages_v6.tsv i arm_map_village_lore_v6.tsv (uklad v6 bez zmian - stary uklad, jak _v5 / _v4),
#   arm_ks_land.tsv (strefa klimatu i zajecie startowe KS wiersza S wioski), tools/wioski-v5/okregi_n_v4.tsv (regula L4), rastry mapy ROT (katalog a-teren:
#   teren.npz, flora.npz, okregi.npz, drogi_raster.npz, osady.json - D:/Backup-Bannerlord/drogi-siec/a-teren) i sonda terenu z gry (geo-siatka.tsv, kolumna nawig -
#   D:/Backup-Bannerlord/geo-dane-2026-10-16).
# Wyjscie: Armoury/ModuleData/arm_map_villages.tsv (v7), arm_map_village_lore.tsv (lore v7), tools/wioski-v7/raport-v7.txt, wioski-v7.tsv (przeniesione wioski)
#   i (opcja --png <katalog>) obrazki v6 / v7 do przegladu. Przebieg deterministyczny (te same wejscia = te same pliki).
#
# Uzycie (z katalogu repo): python -I tools/wioski-v7/v7_uklad.py [--teren <a-teren>] [--geo <geo-siatka.tsv>] [--png <katalog>]
#
# REGULA:
#  1. Odleglosc minimalna D = 2 x najmniejsza odleglosc wioska-wioska ukladu v6 (6.13 -> 12.26 jedn.) - miedzy kazdymi dwiema wioskami pobocznymi i miedzy wioska
#     poboczna a wsia gry (v6: 6.00). Wsie gry stoja tam, gdzie postawila je gra (pary wies gry - wies gry ponizej D zostaja - dane ROT).
#  2. Ziemia okregu: raster okregow KS (okregi.npz label; ziemia dalej niz 45 jedn. drogi - do najblizszej wsi gry do 60 jedn., jak v6). Miejsce pod wioske:
#     lad przejezdny w rastrze (bez bagna, rzeki, stromizny >= 0.35 w kwadracie 3x3; bez morza i gor - w ROT takze klify brzegu - w kwadracie 3x3) i w sondzie gry
#     (nawig 7 z 9 komorek 2 jedn.; brzeg 5 z 9), od miasta >= 14, zamku >= 11, kryjowki >= 5, poza malowana droga (>= 1.5 jedn. od jej srodka).
#  3. Docelowy odstep okregu S = 1.07 x sqrt(ziemia okregu / (wioski + wies gry)) w granicach D..45 (rownomierne pokrycie ziemi okregu).
#  4. Zostaja na miejscu (kolejnosc: wezly H - skrzyzowania, mosty, porty; przysiolki v6; wieksze; uid): wioska, ktora stoi >= D od wsi gry i >= max(D, 0.75 x S)
#     od wiosek juz zostawionych - reszta skupiska idzie na wolny lad.
#  5. Nowe miejsca (okregi od najciasniejszego): zachlannie komorka o najwiekszym wyniku = min(odleglosc od najblizszej wioski, S) / S + 0.35 x atrakcyjnosc
#     + 0.12 x szum (deterministyczny) przy odleglosci >= D. Atrakcyjnosc miejsca (jak prawdziwe siedliska): rzeka 1.0, brzeg morza / jeziora 0.9, pola i laki 0.55,
#     step 0.45, skraj lasu 0.5 (drwale, weglarze), wzgorza 0.4 (pasterze), gleboka puszcza 0.25, snieg 0.25, piasek 0.1; dojazd z malowanej drogi (3..12 jedn.)
#     +0.1. Pustka idzie pierwsza (do odstepu S nic poza nia sie nie liczy), wiec wioski rozchodza sie po calym ladzie okregu bez siatek i pierscieni.
#     Okreg bez miejsca na swojej ziemi: ziemia sasiadow na tym samym ladzie (spojna skladowa ladu ze wsia gry, do 75 jedn. od wsi gry); dalej odleglosc
#     minimalna obnizana stopniami (0.9 D ... do odleglosci v6) - kazdy taki przypadek w raporcie; bez zadnego miejsca - wioska zostaje w miejscu v6.
#  6. Ktora wioska na ktore miejsce: najmniejszy koszt (metoda wegierska w okregu): przesuniecie / S + 1.5 (wioska rybacka v6 nie nad woda) + 0.4 (przysiolek v6
#     nie na dzikiej ziemi). uid bez zmian (zapis v6 / v5 / v4 przechodzi po uid - VillageBook.OldLayout).
#  7. Przeniesiona wioska dostaje miejsce (kind) i obrazek z ziemi: brzeg / jezioro (rybacka tylko, gdy byla rybacka w v6), rzeka (mlyn wodny albo wies), pole
#     (zagroda, wiatrak, spichlerz, wies wedlug wielkosci); na dzikiej ziemi (las, wzgorza, step, snieg, piasek) - przysiolek z zajeciem z ziemi jak v6
#     (owce / kozy, bydlo, swinie, drwale / weglarze, traperzy, mysliwi). Ludzie: osady okregu przeniesionych wiosek rozdzielone na nie wedlug miejsca
#     (rzeka 1.15, jezioro 1.15, brzeg 1.10, pole 0.75, przysiolek 0.40) x czynnik z uid (0.70..1.45), przysiolek 4..12, wioska 6..najwieksza v6 okregu -
#     SUMA OSAD WIOSEK KAZDEGO OKREGU = v6, LICZBA WIOSEK OKREGU = v6 (wies gry i ludzie okregu bez zmian; L4 jak v6). Wioski zostawione - jak v6.
#  8. Ziemia KS (kolumny ks_*) - kazda przeniesiona wioska (nie tylko przysiolek): teren, miejsce, obrazek, odleglosci punktu i zajecie startowe: przysiolek - zajecie
#     z ziemi (tabela swiata KS, jak v6); wioska - zajecie v6 (wiersz S arm_ks_land.tsv), gdy ziemia je niesie (stadnina, kopalnia - zawsze), inaczej najlepsze
#     z tabeli (pszenica, gdy >= 80% najlepszego). Nakladka KS (VillageHamlets.OverlayKs) czyta kazdy wiersz z ks_*.
#  9. Nazwy: Westeros - przysiolek z nowym zajeciem dostaje nowa nazwe (jak v6), reszta stara. Lore: wioska zostawiona - bez zmian; przeniesiona - zdanie miejsca
#     (droga, skrzyzowanie, most, brod) zastapione zdaniem nowego miejsca, mlyn / rybacy bez wody - zdanie bez mlyna / sieci, historia "o drodze" - inna;
#     przysiolek - zajecie i historia jak v6. order / toward_village / neighbors / level - regula generatora v4 (jak v6).
import argparse, io, json, math, os, re, sys, zlib
from collections import Counter, defaultdict, deque
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
MD = os.path.join(REPO, "Armoury", "ModuleData")
IN_LAYOUT = os.path.join(MD, "arm_map_villages_v6.tsv")
IN_LORE = os.path.join(MD, "arm_map_village_lore_v6.tsv")
IN_KSLAND = os.path.join(MD, "arm_ks_land.tsv")
IN_NGEN = os.path.join(REPO, "tools", "wioski-v5", "okregi_n_v4.tsv")
OUT_LAYOUT = os.path.join(MD, "arm_map_villages.tsv")
OUT_LORE = os.path.join(MD, "arm_map_village_lore.tsv")
OUT_REPORT = os.path.join(HERE, "raport-v7.txt")
OUT_LIST = os.path.join(HERE, "wioski-v7.tsv")

ap = argparse.ArgumentParser()
ap.add_argument("--teren", default="D:/Backup-Bannerlord/drogi-siec/a-teren")
ap.add_argument("--geo", default="D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-siatka.tsv")
ap.add_argument("--png", default=None)
ap.add_argument("--dry", action="store_true", help="bez zapisu plikow danych (proba parametrow)")
ap.add_argument("--dump", default=None, help="zrzut rastrow do .npz (proby)")
ap.add_argument("--diag", action="store_true", help="okregi bez miejsca przy D (proby)")
A = ap.parse_args()

SLOPE_MAX = 0.35
D_TOWN, D_CASTLE, D_HIDE = 10.0, 8.0, 5.0      # od miasta / zamku / kryjowki (v6: wioska-miasto min 12.02, wioska-zamek min 9.02; wies gry-miasto min 5.44)
D_PAINT = 1.5            # od srodka malowanej drogi (wioska nie stoi na drodze)
S_HEX = 1.07             # odstep heksagonalny: sqrt(2 / sqrt(3)) = 1.0746
S_CAP = 45.0
KEEP_K = 0.75            # wioska zostaje, gdy >= max(D, KEEP_K x S) od zostawionych
BETA, GAMMA = 0.35, 0.25 # atrakcyjnosc i szum w wyniku miejsca (rozproszenie)
BETA_T, GAMMA_T = 0.08, 0.06   # krok a (ciasno): woda / pola i szum ledwie rozstrzygaja - najpierw wszystkie wioski musza sie zmiescic przy D
R_TERR = 100.0           # ziemia okregu v7 (Voronoi wazony) najdalej tyle od wsi gry
A_EFF = 120.0            # komorek miejsc na wioske (pojemnosc ziemi przy D: ciasno ok. 114)
TERR_IT, TERR_STEP, TERR_WMAX = 40, 6.0, 60.0
TIGHT_FULL = 0.80        # okreg ciasny: wiosek > 0.80 x pojemnosci jego ziemi przy D (bez wiosek pobocznych) - wszystkie ukladane od nowa
FB_PEN = 0.5             # kara ziemi sasiada (plus odleglosc od wsi gry / R_FB)
FISH_BONUS = 0.5         # wioska rybacka v6 nad woda (rozproszenie)
RELAX_SWEEPS, RELAX_GAIN, RELAX_FB_R = 6, 0.01, 30.0   # przebiegi rozproszenia, najmniejszy zysk przejscia, ziemia sasiada wokol obecnego miejsca
VOR_MAX = 60.0
R_FB = 100.0             # ziemia sasiadow: do tylu jedn. od wsi gry okregu (ten sam lad)
R_FB2 = 120.0            # dalej - tylko przy odleglosci obnizonej do RELAX_FLOOR x D
RELAX_FLOOR = 0.85       # ponizej 0.85 D - dopiero gdy dalej tez nie ma miejsca (wyspy, waskie pasy ladu)
CAP = 64.0               # zasieg stempla odleglosci (> S_CAP)
FISH_SEA = 3.0
FISH_SITE = 4.0          # wioska rybacka v6 nad woda: do 4 jedn. od wody (widok szuka brzegu do 5 jedn.)
HILL_SLOPE = 0.20        # wzgorza (pasterze koz) od tego nachylenia (albo od 0.12 przy gorach do 8 jedn.)
WEIGHT = {"river": 1.15, "lake": 1.15, "coast": 1.10, "field": 0.75, "hamlet": 0.40}
H_LO, H_HI, V_LO = 4, 12, 6
S_MAX = 36
WESTEROS = {"battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "sturgia", "crownlands", "dragonstone", "freefolk", "nightswatch", "skagosi"}


def fnv1a(s):
    h = 0x811C9DC5
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def u01(s):
    return fnv1a(s) / 4294967296.0


# ---------------------------------------------------------------- tabela swiata KS (kopia ks_ziemia.py / KsTab.LandRaw: zajecia pola i dzikiej ziemi)
CROPS = ['wheat_farm', 'flax_plant', 'silk_plant', 'vineyard', 'olive_trees', 'date_farm', 'cattle_farm', 'sheep_farm', 'swine_farm', 'lumberjack', 'trapper']
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
    'snieg': {'wheat_farm': 0, 'flax_plant': 0, 'silk_plant': 0, 'vineyard': 0, 'olive_trees': 0, 'date_farm': 0, 'cattle_farm': 0.3, 'swine_farm': 0.3, 'sheep_farm': 0.6, 'trapper': 1.3},
    'piasek': {'wheat_farm': 0.3, 'flax_plant': 0, 'vineyard': 0.3, 'olive_trees': 1.0, 'date_farm': 1.3, 'cattle_farm': 0.2, 'swine_farm': 0, 'sheep_farm': 0.6, 'lumberjack': 0, 'trapper': 0},
    'step': {'wheat_farm': 0.8, 'flax_plant': 0.5, 'sheep_farm': 1.3, 'cattle_farm': 0.8, 'swine_farm': 0.5, 'lumberjack': 0, 'trapper': 0},
    'las': {'wheat_farm': 0.6, 'lumberjack': 1.5, 'trapper': 1.3, 'swine_farm': 1.3, 'vineyard': 0.5, 'olive_trees': 0.5, 'date_farm': 0, 'silk_plant': 0.3},
    'gory': {'wheat_farm': 0.5, 'flax_plant': 0.4, 'silk_plant': 0.3, 'sheep_farm': 1.3, 'vineyard': 1.2, 'olive_trees': 0.9, 'date_farm': 0.3, 'cattle_farm': 0.7},
    'bagno': {'wheat_farm': 0.5, 'vineyard': 0, 'olive_trees': 0, 'sheep_farm': 0.5},
}
HOT = ('5-srodz', '6-goraco', '7-tropik')
FIELD = ('wheat_farm', 'flax_plant', 'silk_plant', 'cattle_farm', 'garden', 'citrus', 'vineyard', 'swine_farm')
HUNT_Z = {'1-arktyka': 1.0, '2-zimno': 0.8, '2-wyspy': 0.3, '3-umiark': 0.4, '4-cieplo': 0.3, '5-srodz': 0.2, '6-goraco': 0.2, '7-tropik': 0.4}
HUNT_T = {'las': 1.2, 'snieg': 0.8, 'gory': 0.7, 'step': 0.6, 'bagno': 0.6, 'trawa': 0.3, 'piasek': 0.1}
WILD = ['sheep_farm', 'cattle_farm', 'swine_farm', 'lumberjack', 'trapper', 'hunter']
PREF = {'sheep_farm': 1.0, 'lumberjack': 1.0, 'trapper': 0.95, 'hunter': 0.85, 'cattle_farm': 0.8, 'swine_farm': 0.75, 'fisherman': 1.1}
FARM = ['wheat_farm', 'flax_plant', 'silk_plant', 'vineyard', 'olive_trees', 'date_farm', 'cattle_farm', 'sheep_farm', 'swine_farm']


def coast(o):
    return o['kind'] == 'coast' or o['d_sea'] <= 3 or o.get('model') == 'fishing'


def river(o):
    return o['kind'] in ('river', 'bridge', 'ford') or o['d_river'] <= 3


def water_mult(t, o):
    m = 1.0
    if river(o):
        m *= {'flax_plant': 1.3, 'silk_plant': 1.5, 'cattle_farm': 1.2, 'wheat_farm': 1.1, 'date_farm': 1.3}.get(t, 1.0)
        if t == 'wheat_farm' and o['zone'] in HOT:
            m *= 1.5
    else:
        if t in ('silk_plant', 'date_farm') and o['zone'] in ('5-srodz', '6-goraco'):
            m *= 0.4
        if t == 'wheat_farm' and o['terr'] == 'piasek':
            m *= 0.3
    if coast(o):
        m *= {'olive_trees': 1.2}.get(t, 1.0)
    return m


def land(t, o):
    """Przydatnosc ziemi dla zajecia (ks_ziemia.land + land2, KsTab.LandRaw)."""
    if o.get('zone') in HOT and o.get('terr') == 'piasek' and river(o) and t in FIELD:
        o = dict(o, terr='trawa')
    if t == 'hunter':
        return min(1.5, HUNT_Z.get(o['zone'], 0.3) * HUNT_T.get(o['terr'], 0.3))
    if t == 'fisherman':
        b = 1.0 if coast(o) else (0.4 if river(o) else 0.0)
        if o['zone'] == '2-wyspy' and b > 0:
            b *= 1.2
        return b
    if t in CROPS:
        w = ZW.get(o['zone'], {}).get(t, 0.0) * TW.get(o['terr'], {}).get(t, 1.0) * water_mult(t, o)
        if o.get('model') == 'fishing' and t in ('wheat_farm', 'flax_plant', 'silk_plant', 'vineyard', 'olive_trees', 'date_farm', 'cattle_farm', 'swine_farm'):
            w *= 0.5
        return min(1.5, w)
    return 0.0


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


def read_ks(path):
    zone, dzone, start = {}, {}, {}
    for raw in io.open(path, encoding="utf-8"):
        if raw.startswith("#"):
            continue
        c = raw.rstrip("\r\n").split("\t")
        if len(c) < 14:
            continue
        if c[0] == "S":
            zone[c[2]] = c[4]
            start[c[2]] = c[12]
        elif c[0] == "M" and c[2].endswith("#main0"):
            dzone[c[1]] = c[4]
    return zone, dzone, start


def recompute(rows_d):
    """order / toward_village / neighbors okregu wedlug regul generatora v4 (kopia v6_przysiolki.recompute)."""
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


class PtGrid:
    """Siatka punktow (dokladna odleglosc do najblizszego)."""
    def __init__(self, cell=16.0):
        self.cell = cell
        self.g = defaultdict(list)

    def add(self, x, y, tag=None):
        self.g[(int(x // self.cell), int(y // self.cell))].append((x, y, tag))

    def remove(self, x, y, tag):
        l = self.g[(int(x // self.cell), int(y // self.cell))]
        for k, p in enumerate(l):
            if p[2] == tag:
                del l[k]
                return

    def nearest(self, x, y, rmax=80.0, skip=None):
        best = 1e9
        rr = int(math.ceil(rmax / self.cell))
        gx, gy = int(x // self.cell), int(y // self.cell)
        for ix in range(gx - rr, gx + rr + 1):
            for iy in range(gy - rr, gy + rr + 1):
                for (px, py, tg) in self.g.get((ix, iy), ()):
                    if skip is not None and tg == skip:
                        continue
                    d = math.hypot(px - x, py - y)
                    if d < best:
                        best = d
        return best


def nn_list(pts, others=None):
    P = np.array(pts, dtype=np.float64)
    Q = P if others is None else np.array(others, dtype=np.float64)
    out = np.empty(len(P))
    for i in range(len(P)):
        d = np.hypot(Q[:, 0] - P[i, 0], Q[:, 1] - P[i, 1])
        if others is None:
            d[i] = 1e9
        out[i] = d.min()
    return out


def stats(a):
    s = np.sort(np.asarray(a))
    n = len(s)
    return dict(min=float(s[0]), p10=float(s[n // 10]), med=float(np.median(s)), mean=float(s.mean()), max=float(s[-1]))


def crowd(pts, rad=10.0):
    P = np.array(pts)
    cnt = []
    for i in range(len(P)):
        d = np.hypot(P[:, 0] - P[i, 0], P[:, 1] - P[i, 1])
        cnt.append(int((d <= rad).sum()) - 1)
    return sum(1 for k in cnt if k >= 3), max(cnt), sum(cnt) / float(len(cnt))


def hungarian(cost):
    """Minimalny koszt przydzialu (macierz n x m, n <= m) - klasyczna metoda wegierska (potencjaly), zwraca kolumne dla kazdego wiersza."""
    n, m = len(cost), len(cost[0])
    INF = float("inf")
    u = [0.0] * (n + 1); v = [0.0] * (m + 1); p = [0] * (m + 1); way = [0] * (m + 1)
    for i in range(1, n + 1):
        p[0] = i
        j0 = 0
        minv = [INF] * (m + 1)
        used = [False] * (m + 1)
        while True:
            used[j0] = True
            i0 = p[j0]; delta = INF; j1 = 0
            for j in range(1, m + 1):
                if not used[j]:
                    cur = cost[i0 - 1][j - 1] - u[i0] - v[j]
                    if cur < minv[j]:
                        minv[j] = cur; way[j] = j0
                    if minv[j] < delta:
                        delta = minv[j]; j1 = j
            for j in range(m + 1):
                if used[j]:
                    u[p[j]] += delta; v[j] -= delta
                else:
                    minv[j] -= delta
            j0 = j1
            if p[j0] == 0:
                break
        while True:
            j1 = way[j0]
            p[j0] = p[j1]
            j0 = j1
            if j0 == 0:
                break
    res = [-1] * n
    for j in range(1, m + 1):
        if p[j] > 0:
            res[p[j] - 1] = j - 1
    return res


# ---------------------------------------------------------------- nazwy przysiolkow (Westeros, po angielsku - jak v6)
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
            continue
        nm = (s + " " + e.capitalize()) if e in SEP_WORD else (s + e)
        if nm not in used:
            return nm
    return stems[h % len(stems)] + " Hamlet " + str(h % 97)


# ---------------------------------------------------------------- lore (po angielsku)
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
        "Woodcutters: they fell timber at the edge of the woods and cart the logs to the nearest market.",
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
    ],
}
PLACE_LINE = {
    "hills": "The hamlet clings to the hills, a long way from the main road.",
    "forest": "The hamlet stands in a clearing in the woods, a long way from the main road.",
    "steppe": "The hamlet lies alone on the open grass, a day's ride from its neighbours.",
    "moor": "The hamlet stands alone on the cold moor, a long way from the main road.",
    "desert": "The hamlet sits by a lonely well among the stones, far from any road.",
    "coast": "The hamlet hides in a cove of a lonely shore.",
    "lake": "The hamlet stands on a lonely lakeshore.",
    "wilds": "The hamlet stands alone in the wilds, a long way from the main road.",
}
HIST = [
    "Its families came from the crowded villages by the road, looking for land no one had claimed.",
    "Few travellers find the track here; the lord's steward comes once a year for his due.",
    "In a hard winter the hamlet can be cut off for weeks, and they keep a store of salted meat against it.",
    "Outlaws pass by now and then, and the hamlet has learned to look the other way.",
    "The oldest woman of the hamlet remembers when there was nothing here but wind.",
    "They pay their tithe in kind, and the lord's men rarely bother to climb up for more.",
]
# zdania miejsca przeniesionej wioski (drugie zdanie zajecia; "field" - jedno z kilku)
NEW_PLACE = {
    "river": ["Ferrymen and eel traps on the river.", "A landing on the river for barges and boats.", "Its fields run down to the river, and the river floods them every spring."],
    "coast": ["Boats drawn up on the shingle, and salt fish for the markets.", "Salt fish and seaweed are dried along the shore."],
    "lake": ["Reed-cutters and boats on the lake.", "Its boats go out on the lake at dawn."],
    "field": ["A cart track leads to the village from the main road.", "Its fields reach out to the edge of the woods.",
              "The village stands among its own fields, away from the dust of the road.", "Its people cleared this land three generations ago.",
              "A lane between hedgerows leads off the main road to the village."],
}
NEW_PLACE_WILD = {
    "forest": ["The village stands at the edge of the woods, and its men cut timber in the winter.", "Its fields are cleared out of the forest, a little more every year."],
    "hills": ["Its fields climb the hillsides in narrow terraces.", "Sheep graze the hills above the village fields."],
    "steppe": ["Its herds graze the open grass all around it.", "The village stands alone on the open grass, its fields fenced against the herds."],
    "moor": ["The village huddles against the wind on the cold moor.", "Its people dig peat on the moor and grow what the cold allows."],
    "desert": ["The village lives around its wells.", "Its fields are watered from deep wells, and the sand is never far."],
    "wilds": ["The village stands well off the main road.", "A long track leads to the village from the main road."],
}
NEW_HIST = [
    "Its people moved out from the crowded villages by the road to clear land of their own.",
    "Travellers rarely turn off the main road to come here, and the village likes it that way.",
    "The steward of the district rides out once a season to count the sheaves.",
    "In the old days this was all waste and woodland; the first families came with axes and oxen.",
    "The village keeps to itself; news from the road reaches it a week late.",
    "Its well has never run dry, and the village is proud of it.",
]
# zdania miejsca lore v4 (tools/wioski-lore/b-skrypty/b_wiara_zajecia.py, tabela PLACE) - zdejmowane z konca zajecia przeniesionej wioski
OLD_PLACE_RX = [re.compile(p) for p in [
    r"Bridge tolls, and a smithy for passing riders\.$", r"Bridge tolls for .+?, paid in coin or in kind\.$", r"Ox teams help carts across the ford, for a coin\.$",
    r"An inn at the crossroads, and a market on feast days\.$", r"An inn at the crossroads, busiest before winter\.$", r"Caravans rest at the crossroads market\.$",
    r"An alehouse on the road serves carters and riders\.$", r"A well and a stable for the caravans on the road\.$", r"Ferrymen and eel traps on .+?\.$",
    r"Boats drawn up on the shingle, and salt fish for the markets\.$", r"Reed-cutters and boats on the lake\.$",
    r"A smithy on the road shoes the horses of passing riders\.$", r"A waystation on the road sells bread and ale\.$",
    r"Caravan drivers water their beasts at the village well\.$", r"A landing on .+? for barges and boats\.$", r"Salt fish and seaweed are dried along the shore\.$",
    r"Carters and drovers meet at the crossroads inn\.$", r"A smithy and an alehouse by the bridge\.$",
]]
ROAD_HIST = re.compile(r"\broad\b|\broads\b|crossroad|bridge|\bford\b|riders|travellers on|passing", re.I)
MILL_RX = [(re.compile(r"\s*The mill wheel turns on [^.]+\."), ""), (re.compile(r"\s*The mill turns on [^.]+\."), ""), (re.compile(r" and millers?:"), ":"),
           (re.compile(r" and a miller:"), ":")]


def main():
    head, cols, rows = read_layout(IN_LAYOUT)
    need = ["id", "village_id", "name", "x", "y", "road_deg", "settlements", "order", "kind", "uid", "class", "side", "level", "road_dist",
            "toward_village", "neighbors", "front_deg", "half_len", "half_wid", "face", "model", "hamlet", "ks_start", "ks_terr", "ks_kind", "ks_model",
            "ks_dsea", "ks_driver", "ks_dmount", "v5_order", "v5_x", "v5_y", "v5_settlements"]
    miss = [c for c in need if c not in cols]
    if miss:
        print("BRAK kolumn w v6: " + ",".join(miss))
        return 2
    ci = {c: i for i, c in enumerate(cols)}
    ngen = read_ngen(IN_NGEN)
    kzone, dzone, kstart = read_ks(IN_KSLAND)
    for r in rows:
        r["_x"], r["_y"] = float(r["x"]), float(r["y"])
        r["_s"] = int(r["settlements"])
        r["_h6"] = r["hamlet"] != "-"
    dist = defaultdict(list)
    for r in rows:
        dist[r["village_id"]].append(r)
    s6_sum = {d: sum(r["_s"] for r in rs) for d, rs in dist.items()}

    # ---------- rastry i sonda
    J = lambda n: os.path.join(A.teren, n)
    T = np.load(J("teren.npz")); F = np.load(J("flora.npz")); OK = np.load(J("okregi.npz")); DR = np.load(J("drogi_raster.npz"))
    walk, sea, mnt, swp = T["walk"], T["sea"], T["mountain"], T["swamp"]
    rivm = T["river"] > 0
    slope = T["slope"].astype(np.float32)
    dsea, driv, dmnt = T["d_sea"].astype(np.float32), T["d_river"].astype(np.float32), T["d_mount"].astype(np.float32)
    snowy = ((T["w10_Snow"].astype(np.int32) + T["w09_ICE"].astype(np.int32)) >= 128) | T["snow"]
    forest = F["las_razem"] | T["forest"]
    deep = F["glebokosc_puszczy"].astype(np.float32)
    CODE = np.select([snowy, T["w05_Sand"] >= 128, forest, T["w04_Steppe"] >= 128, mnt, swp], [0, 1, 2, 3, 4, 5], 6).astype(np.int32)
    TN = ['snieg', 'piasek', 'las', 'step', 'gory', 'bagno', 'trawa']
    lab = OK["label"].astype(np.int32); rdist = OK["dist"].astype(np.float32)
    vids = [str(v) for v in OK["village_ids"]]
    vidx = {v: i for i, v in enumerate(vids)}
    face = T["face"]
    H, W = walk.shape
    passable = walk & (~sea) & (~mnt)

    def roll_any(m, rad):
        out = m.copy()
        for dy in range(-rad, rad + 1):
            for dx in range(-rad, rad + 1):
                if dx or dy:
                    out |= np.roll(np.roll(m, dy, axis=0), dx, axis=1)
        return out

    bad = ((~walk) & (~sea) & (~mnt)) | swp | rivm | (slope >= SLOPE_MAX)
    near = sea | mnt | (~walk)
    # kwadrat 3x3 bez bagna, rzeki, stromizny, morza i ladu nieprzejezdnego; gory (w ROT takze sciany rzek i klify) - sama komorka (sonda gry i tak
    # wymaga 7 z 9 komorek 2 jedn. przejezdnych wokol)
    badw = roll_any(bad | sea | (~walk), 1) | mnt
    # brzeg (rybacy, wioski nad woda): sama komorka ladowa bez gor, 1.4..FISH_SEA jedn. od wody, bez bagna / rzeki / stromizny w 3x3
    shore_ok = walk & (~sea) & (~mnt) & (~roll_any(bad, 1)) & (dsea >= 1.4) & (dsea <= FISH_SEA)
    badw &= ~shore_ok
    # sonda gry: nawig 2 jedn. (srodek tak i 7 z 9; brzeg 5 z 9)
    nav = np.zeros(((H // 2) + 3, (W // 2) + 3), dtype=bool)
    with io.open(A.geo, encoding="utf-8") as f:
        f.readline()
        for line in f:
            c = line.split("\t", 3)
            if c[2] == "tak":
                xi, yi = int(c[0]) // 2, int(c[1]) // 2
                if 0 <= yi < nav.shape[0] and 0 <= xi < nav.shape[1]:
                    nav[yi, xi] = True
    navc = np.zeros(nav.shape, dtype=np.int32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            navc += np.roll(np.roll(nav, dy, axis=0), dx, axis=1).astype(np.int32)
    navw = nav & (navc >= 7)
    navs = nav & (navc >= 5)

    def probe_ok(x, y):
        xi, yi = int(round(x / 2.0)), int(round(y / 2.0))
        if not (0 <= yi < navw.shape[0] and 0 <= xi < navw.shape[1]):
            return False
        ix, iy = int(math.floor(x)), int(math.floor(y))
        coastal = 0 <= iy < H and 0 <= ix < W and bool(shore_ok[iy, ix])
        return bool(navs[yi, xi]) if coastal else bool(navw[yi, xi])

    # sonda na rastrze 1 jedn. (komorka -> komorka 2 jedn.)
    yy, xx = np.mgrid[0:H, 0:W]
    pxi = np.clip(np.rint((xx + 0.5) / 2.0).astype(np.int32), 0, navw.shape[1] - 1)
    pyi = np.clip(np.rint((yy + 0.5) / 2.0).astype(np.int32), 0, navw.shape[0] - 1)
    probe_r = np.where(shore_ok, navs[pyi, pxi], navw[pyi, pxi])
    cx = (xx + 0.5).astype(np.float32); cy = (yy + 0.5).astype(np.float32)

    osady = json.load(io.open(J("osady.json"), encoding="utf-8"))["osady"]
    towns = [(o["x"], o["y"]) for o in osady if o["kind"] == "town"]
    castles = [(o["x"], o["y"]) for o in osady if o["kind"] == "castle"]
    hides = [(o["x"], o["y"]) for o in osady if o["kind"] == "hideout"]
    mainv = {o["id"]: (o["x"], o["y"]) for o in osady if o["kind"] == "village"}
    cult = {o["id"]: (o.get("culture") or "") for o in osady if o["kind"] == "village"}

    def stamp(d, x, y, cap=CAP):
        x0, x1 = int(max(0, x - cap)), int(min(W, x + cap + 1)); y0, y1 = int(max(0, y - cap)), int(min(H, y + cap + 1))
        if x0 >= x1 or y0 >= y1:
            return
        sub = np.sqrt((cx[y0:y1, x0:x1] - x) ** 2 + (cy[y0:y1, x0:x1] - y) ** 2)
        np.minimum(d[y0:y1, x0:x1], sub, out=d[y0:y1, x0:x1])

    def dist_raster(pts, cap=CAP):
        d = np.full((H, W), 1e6, dtype=np.float32)
        for (x, y) in pts:
            stamp(d, x, y, cap)
        return d

    d_town = dist_raster(towns, 20); d_castle = dist_raster(castles, 20); d_hide = dist_raster(hides, 10)
    painted = DR["painted"]
    d_paint = np.full((H, W), 1e6, dtype=np.float32)
    pys, pxs = np.nonzero(painted)
    for (y, x) in zip(pys.tolist(), pxs.tolist()):
        stamp(d_paint, x + 0.5, y + 0.5, 13)
    # okreg komorki: raster okregow KS; poza nim (-1) - najblizsza wies gry do VOR_MAX (jak v6); rdzenie twierdz (-2) poza
    tlab = lab.copy()
    vbest = np.full((H, W), 1e6, dtype=np.float32)
    vlab = np.full((H, W), -1, dtype=np.int32)
    for vid, (x, y) in mainv.items():
        if vid not in vidx:
            continue
        x0, x1 = int(max(0, x - VOR_MAX)), int(min(W, x + VOR_MAX + 1)); y0, y1 = int(max(0, y - VOR_MAX)), int(min(H, y + VOR_MAX + 1))
        sub = np.sqrt((cx[y0:y1, x0:x1] - x) ** 2 + (cy[y0:y1, x0:x1] - y) ** 2)
        upd = (sub < vbest[y0:y1, x0:x1]) & (sub <= VOR_MAX)
        vbest[y0:y1, x0:x1][upd] = sub[upd]
        vlab[y0:y1, x0:x1][upd] = vidx[vid]
    vor = (lab == -1) & (vlab >= 0)
    tlab[vor] = vlab[vor]
    # lad: spojne skladowe (walk bez morza, 4-sasiedztwo; gory - w ROT pasy rzek - lacza, mosty i tak przecinaja)
    comp = np.full((H, W), -1, dtype=np.int32)
    landm = walk & (~sea)
    ncomp = 0
    flat = landm.ravel()
    cflat = comp.ravel()
    for start in np.flatnonzero(flat):
        if cflat[start] >= 0:
            continue
        q = deque([start]); cflat[start] = ncomp
        while q:
            k = q.popleft()
            y, x = divmod(k, W)
            for nk, ok_ in ((k - 1, x > 0), (k + 1, x < W - 1), (k - W, y > 0), (k + W, y < H - 1)):
                if ok_ and flat[nk] and cflat[nk] < 0:
                    cflat[nk] = ncomp; q.append(nk)
        ncomp += 1
    comp = cflat.reshape(H, W)

    def comp_at(x, y):
        ix, iy = int(math.floor(x)), int(math.floor(y))
        best = None
        for rr in range(0, 6):
            for dy in range(-rr, rr + 1):
                for dx in range(-rr, rr + 1):
                    jx, jy = ix + dx, iy + dy
                    if 0 <= jx < W and 0 <= jy < H and comp[jy, jx] >= 0:
                        return int(comp[jy, jx])
        return -1

    # atrakcyjnosc miejsca (0..1.1): woda, pola, skraj lasu, wzgorza
    edge_f = forest & roll_any(~forest, 3)
    edge_n = (~forest) & roll_any(forest, 2)
    hills = ((slope >= 0.12) | (dmnt <= 5)) & (~forest)
    attr = np.full((H, W), 0.2, dtype=np.float32)
    attr = np.where(CODE == 6, np.maximum(attr, np.where(slope < 0.12, 0.55, 0.45)), attr)
    attr = np.where(CODE == 3, np.maximum(attr, 0.45), attr)
    attr = np.where(hills, np.maximum(attr, 0.4), attr)
    attr = np.where(edge_f | edge_n, np.maximum(attr, 0.5), attr)
    attr = np.where(forest & (deep >= 4), np.minimum(attr, 0.25), attr)
    attr = np.where(CODE == 0, np.minimum(attr, 0.25), attr)
    attr = np.where((CODE == 1) & (driv > 6), 0.1, attr)
    attr = np.where((driv <= 6) & (driv >= 1.0), np.maximum(attr, 0.6), attr)
    attr = np.where((driv <= 4) & (driv >= 1.0), np.maximum(attr, 1.0), attr)
    attr = np.where(dsea <= 3.5, np.maximum(attr, 0.9), attr)
    attr = attr + np.where((d_paint >= 3) & (d_paint <= 12), 0.1, 0.0).astype(np.float32)
    # szum deterministyczny (gladki w skali ok. 6 jedn. + drobny) - nieregularne odstepy, bez siatek
    hsh = ((xx.astype(np.int64) * 73856093) ^ (yy.astype(np.int64) * 19349663) ^ 0x5bd1e995) & 0xFFFFFFFF
    hsh = (hsh * 2654435761) & 0xFFFFFFFF
    fine = (hsh >> 8).astype(np.float64) / float(1 << 24)
    coarse = np.zeros((H // 6 + 2, W // 6 + 2))
    gy_, gx_ = np.mgrid[0:coarse.shape[0], 0:coarse.shape[1]]
    ch = ((gx_.astype(np.int64) * 83492791) ^ (gy_.astype(np.int64) * 2971215073) ^ 0x27d4eb2d) & 0xFFFFFFFF
    coarse = ((ch * 2654435761) & 0xFFFFFFFF).astype(np.float64) / 4294967296.0
    fy = (yy / 6.0); fx = (xx / 6.0)
    iy0 = np.floor(fy).astype(np.int32); ix0 = np.floor(fx).astype(np.int32)
    ty = fy - iy0; tx = fx - ix0
    sm = (coarse[iy0, ix0] * (1 - tx) * (1 - ty) + coarse[iy0, ix0 + 1] * tx * (1 - ty) + coarse[iy0 + 1, ix0] * (1 - tx) * ty + coarse[iy0 + 1, ix0 + 1] * tx * ty)
    noise = (0.7 * sm + 0.3 * fine).astype(np.float32)

    site_ok = (~badw) & probe_r & (d_town >= D_TOWN) & (d_castle >= D_CASTLE) & (d_hide >= D_HIDE) & (d_paint >= D_PAINT) & (tlab != -2)
    if A.dump:
        np.savez_compressed(A.dump, site_ok=site_ok, tlab=tlab, comp=comp, attr=attr, noise=noise, badw=badw, probe_r=probe_r, d_town=d_town, d_castle=d_castle,
                            d_paint=d_paint, passable=passable, CODE=CODE, slope=slope, dmnt=dmnt, driv=driv, dsea=dsea, near=near, bad=bad)

    # ---------- v6: miary
    side6 = [(r["_x"], r["_y"]) for r in rows]
    mains = [mainv[v] for v in sorted(mainv)]
    nn6_ss = nn_list(side6)
    nn6_sm = nn_list(side6, mains)
    v6_min = float(nn6_ss.min())
    D = round(2.0 * v6_min + 0.004, 2)          # 2 x 6.13 = 12.26
    D_LOW = v6_min                              # dolna granica obnizania (odleglosc v6)

    # ---------- ziemia okregow v7 (Voronoi wazony wsi gry): komorka miejsca nalezy do wsi gry o najmniejszej (odleglosc - waga) na tym samym ladzie (spojna
    # skladowa) do R_TERR jedn.; waga rosnie okregowi, ktorego ziemia nie miesci jego wiosek przy D (pojemnosc = komorki / A_EFF), i powoli maleje okregowi
    # z zapasem - gesty okreg dostaje pas ziemi sasiadow, ktorzy maja jej za duzo (nadmiar rozchodzi sie po calym ladzie, a nie za jedna granica);
    # ziemia miedzy okregami (droga > 45 jedn.) tez ma gospodarza
    main_list = [(vid, mainv[vid]) for vid in sorted(mainv) if vid in vidx]
    wins = []
    for vid, (x, y) in main_list:
        cm = comp_at(x, y)
        x0, x1 = int(max(0, x - R_TERR)), int(min(W, x + R_TERR + 1)); y0, y1 = int(max(0, y - R_TERR)), int(min(H, y + R_TERR + 1))
        dd = np.sqrt((cx[y0:y1, x0:x1] - x) ** 2 + (cy[y0:y1, x0:x1] - y) ** 2)
        if cm >= 0:
            ok_ = site_ok[y0:y1, x0:x1] & (comp[y0:y1, x0:x1] == cm) & (dd <= R_TERR)
        else:
            ok_ = np.zeros(dd.shape, dtype=bool)
        wins.append((vidx[vid], vid, x0, x1, y0, y1, np.where(ok_, dd, 1e6).astype(np.float32)))
    want = {vid: len(dist.get(vid, [])) + 1 for vid, _ in main_list}
    wgt = {vid: 0.0 for vid, _ in main_list}
    terr_hist = []
    for it in range(TERR_IT):
        best = np.full((H, W), 1e6, dtype=np.float32)
        tl = np.full((H, W), -1, dtype=np.int32)
        for li_, vid, x0, x1, y0, y1, dd in wins:
            v = dd - np.float32(wgt[vid])
            sb = best[y0:y1, x0:x1]
            upd = (v < sb) & (dd < 1e5)
            sb[upd] = v[upd]
            tl[y0:y1, x0:x1][upd] = li_
        cnt = np.bincount(tl[tl >= 0], minlength=len(vids))
        over = 0
        for li_, vid, *_ in wins:
            r_ = want[vid] / max(0.5, cnt[li_] / A_EFF)
            if r_ > 1.0:
                over += 1
            wgt[vid] = float(np.clip(wgt[vid] + TERR_STEP * float(np.clip(math.log(max(1e-3, r_)), -1.0, 1.0)), -TERR_WMAX, TERR_WMAX))
        terr_hist.append(over)
    terr_lab = tl
    if A.diag:
        print("DIAG terytoria - okregow ponad pojemnosc po iteracjach:", terr_hist)
        ov = []
        for li_, vid, *_ in wins:
            r_ = want[vid] / max(0.5, cnt[li_] / A_EFF)
            if r_ > 1.0:
                ov.append((r_, vid, cult.get(vid), want[vid], int(cnt[li_]), round(wgt[vid], 1)))
        ov.sort(reverse=True)
        print("DIAG komorek w terytoriach", int(cnt.sum()), "site_ok", int(site_ok.sum()), "chce", sum(want.values()), "pojemnosc przy A_EFF", round(cnt.sum() / A_EFF))
        print("DIAG ponad:", len(ov), "nadmiar wiosek", round(sum(w_ - c_ / A_EFF for _, _, _, w_, c_, _ in ov), 1))
        for o_ in ov[:25]:
            print("DIAG  %.2f %s %s want %d cells %d w %.1f" % o_)
    flat_l = terr_lab.ravel()
    idx_all = np.flatnonzero(flat_l >= 0)
    srt = idx_all[np.argsort(flat_l[idx_all], kind="stable")]
    bnd = np.searchsorted(flat_l[srt], np.arange(len(vids) + 1))
    cells_li = {}
    for li_ in range(len(vids)):
        seg = srt[bnd[li_]:bnd[li_ + 1]]
        cells_li[li_] = (seg // W, seg % W)
    A_d = np.bincount(flat_l[idx_all], minlength=len(vids))
    S_d, K_d = {}, {}
    for did, rs in dist.items():
        li = vidx.get(did)
        a = float(A_d[li]) if li is not None else 0.0
        n = len(rs) + 1
        s = S_HEX * math.sqrt(a / n) if a > 0 else D
        S_d[did] = max(D, min(S_CAP, s))
        K_d[did] = max(D, KEEP_K * S_d[did])

    # pojemnosc ziemi okregu przy D bez wiosek pobocznych (tylko wsie gry; ciasne upakowanie - szacunek). Okreg ciasny (wiosek > TIGHT_FULL x pojemnosci):
    # zadna wioska nie zostaje w miejscu v6 - caly okreg ukladany od nowa (zostawione stoja byle jak i zabieraja miejsce)
    cells_d = {}
    for did in dist:
        li = vidx.get(did)
        cells_d[did] = cells_li.get(li, (np.zeros(0, dtype=np.int64), np.zeros(0, dtype=np.int64))) if li is not None else (np.zeros(0, dtype=np.int64), np.zeros(0, dtype=np.int64))

    def in_terr(did, x, y):
        li = vidx.get(did, -99)
        ix, iy = int(math.floor(x)), int(math.floor(y))
        seen = False
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                jx, jy = ix + dx, iy + dy
                if 0 <= jx < W and 0 <= jy < H and terr_lab[jy, jx] >= 0:
                    seen = True
                    if terr_lab[jy, jx] == li:
                        return True
        return (not seen) and 0 <= iy < H and 0 <= ix < W and tlab[iy, ix] == li
    MX = np.array([mainv[v] for v in sorted(mainv)], dtype=np.float64)

    def cap_full(ys, xs):
        if len(xs) == 0:
            return 0
        px, py = xs + 0.5, ys + 0.5
        dn = np.full(len(xs), 1e6)
        m = (np.abs(MX[:, 0] - px.mean()) < 150) & (np.abs(MX[:, 1] - py.mean()) < 150)
        for (x, y) in MX[m]:
            np.minimum(dn, np.hypot(px - x, py - y), out=dn)
        n = 0
        while True:
            ok = dn >= D
            if not ok.any():
                return n
            k = int(np.argmin(np.where(ok, dn, 1e9)))
            x, y = px[k], py[k]
            sel = (np.abs(px - x) < D + 0.1) & (np.abs(py - y) < D + 0.1)
            dn[sel] = np.minimum(dn[sel], np.hypot(px[sel] - x, py[sel] - y))
            n += 1

    capf_d = {did: cap_full(*cells_d[did]) for did in dist}
    tight_full = {did for did, rs in dist.items() if len(rs) > TIGHT_FULL * capf_d[did]}

    # ---------- 4. zostaja na miejscu
    grid = PtGrid(16.0)
    for vid, (x, y) in mainv.items():
        grid.add(x, y, "M:" + vid)
    order_keep = sorted(range(len(rows)), key=lambda i: (0 if rows[i]["model"] == "fishing" else (1 if rows[i]["class"] == "H" else (2 if rows[i]["_h6"] else 3)),
                                                         -rows[i]["_s"], rows[i]["uid"]))
    kept = set()
    keptK = {}
    sgrid = PtGrid(16.0)
    for i in order_keep:
        r = rows[i]
        x, y = r["_x"], r["_y"]
        if r["village_id"] in tight_full and r["model"] != "fishing":
            continue                                  # okreg ciasny - ukladany od nowa (rybacy na brzegu zostaja, gdy maja miejsce)
        if not in_terr(r["village_id"], r["_x"], r["_y"]):
            continue                                  # ziemia oddana sasiadowi (Voronoi wazony)
        if grid.nearest(x, y, rmax=D + 1) < D:
            continue                                  # za blisko wsi gry
        kd = K_d[r["village_id"]]
        ok_ = True
        rr = int(math.ceil(S_CAP / sgrid.cell))
        gx, gy = int(x // sgrid.cell), int(y // sgrid.cell)
        for ix in range(gx - rr, gx + rr + 1):
            if not ok_:
                break
            for iy in range(gy - rr, gy + rr + 1):
                for (px, py, tg) in sgrid.g.get((ix, iy), ()):
                    need_ = max(D, min(kd, keptK[tg]))
                    if math.hypot(px - x, py - y) < need_:
                        ok_ = False
                        break
                if not ok_:
                    break
        if not ok_:
            continue
        kept.add(i)
        keptK[i] = kd
        sgrid.add(x, y, i)
        grid.add(x, y, i)

    # ---------- 5. nowe miejsca: (a) najpierw tak, zeby wszystkie zmiescily sie przy D (ciasno, okregi od najciasniejszego, swoja ziemia, potem ziemia sasiadow
    #            na tym samym ladzie), (b) potem rozproszenie (6.) - kazda przeniesiona wioska przechodzi na lepsze miejsce swojej ziemi, dopoki jest pustka
    dnear = np.full((H, W), 1e6, dtype=np.float32)
    for vid, (x, y) in mainv.items():
        stamp(dnear, x, y)
    for i in kept:
        stamp(dnear, rows[i]["_x"], rows[i]["_y"])
    moved_by_d = defaultdict(list)
    for i, r in enumerate(rows):
        if i not in kept:
            moved_by_d[r["village_id"]].append(i)
    for did in moved_by_d:
        moved_by_d[did].sort(key=lambda i: (-rows[i]["_s"], rows[i]["uid"]))
    # pojemnosc ziemi okregu przy odleglosci D (ciasne upakowanie na wolnej ziemi okregu - szacunek, okregi osobno)
    def capacity(ys, xs):
        if len(xs) == 0:
            return 0
        px, py = cx[ys, xs], cy[ys, xs]
        dn = dnear[ys, xs].copy()
        n = 0
        while True:
            ok = dn >= D
            if not ok.any():
                return n
            k = int(np.argmin(np.where(ok, dn, 1e9)))
            x, y = px[k], py[k]
            sel = (np.abs(px - x) < D + 0.1) & (np.abs(py - y) < D + 0.1)
            dn[sel] = np.minimum(dn[sel], np.sqrt((px[sel] - x) ** 2 + (py[sel] - y) ** 2))
            n += 1

    if A.diag:
        ys_all, xs_all = np.nonzero(site_ok)
        print("DIAG pojemnosc calego ladu przy D (wsie gry + zostawione):", capacity(ys_all, xs_all), "przeniesionych", sum(len(v) for v in moved_by_d.values()))
    cap_d = {did: capacity(*cells_d[did]) for did in moved_by_d}
    ratio = {did: len(moved_by_d[did]) / max(0.5, float(cap_d[did])) for did in moved_by_d}
    sites = defaultdict(list)                    # okreg -> lista miejsc (x, y, ix, iy, droga)
    exc = Counter()
    exc_list = []

    def try_place(did, ys, xs, dmin, pen=None):
        if len(xs) == 0:
            return None
        dn = dnear[ys, xs]
        okc = dn >= dmin
        if not okc.any():
            return None
        # ciasno: blisko sasiadow (odleglosc tuz ponad D), z woda / polami i szumem
        sc = -np.minimum(dn, 3.0 * dmin) / dmin + BETA_T * attr[ys, xs] + GAMMA_T * noise[ys, xs]
        if pen is not None:
            sc = sc - pen
        sc = np.where(okc, sc, -1e9)
        idx = np.argsort(-sc, kind="stable")[:300]
        for k in idx.tolist():
            if sc[k] < -1e8:
                break
            x0, y0 = float(xs[k]) + 0.5, float(ys[k]) + 0.5
            jx = (u01("%s|%d|%d|jx" % (did, xs[k], ys[k])) - 0.5) * 0.6
            jy = (u01("%s|%d|%d|jy" % (did, xs[k], ys[k])) - 0.5) * 0.6
            x1, y1 = x0 + jx, y0 + jy
            jxi, jyi = int(math.floor(x1)), int(math.floor(y1))
            if not (0 <= jxi < W and 0 <= jyi < H) or not site_ok[jyi, jxi] or not probe_ok(x1, y1):
                continue
            if grid.nearest(x1, y1, rmax=dmin + 1) < dmin:
                continue
            return (x1, y1, jxi, jyi)
        return None

    def commit(did, p, how):
        x1, y1, ix, iy = p
        tag = "N:%s:%d" % (did, len(sites[did]))
        grid.add(x1, y1, tag)
        stamp(dnear, x1, y1)
        sites[did].append((x1, y1, ix, iy, how))

    main_comp = {}

    def fb_cells(did, rfb=None, around=None):
        mx, my = mainv.get(did, (None, None))
        li = vidx.get(did, -99)
        if mx is None:
            return None
        if did not in main_comp:
            main_comp[did] = comp_at(mx, my)
        cm = main_comp[did]
        if cm < 0:
            return None
        R = rfb or R_FB
        bx, by = (around if around is not None else (mx, my))
        x0, x1 = int(max(0, bx - R)), int(min(W, bx + R + 1)); y0, y1 = int(max(0, by - R)), int(min(H, by + R + 1))
        sub = site_ok[y0:y1, x0:x1] & (comp[y0:y1, x0:x1] == cm) & (terr_lab[y0:y1, x0:x1] != li)
        dd = np.sqrt((cx[y0:y1, x0:x1] - mx) ** 2 + (cy[y0:y1, x0:x1] - my) ** 2)
        sub &= dd <= (R if around is None else R_FB2)
        if around is not None:
            sub &= np.sqrt((cx[y0:y1, x0:x1] - bx) ** 2 + (cy[y0:y1, x0:x1] - by) ** 2) <= R
        ys2, xs2 = np.nonzero(sub)
        return (ys2 + y0, xs2 + x0, (dd[ys2, xs2] / R_FB).astype(np.float32))

    need = {did: len(moved_by_d[did]) for did in moved_by_d}
    for did in sorted(moved_by_d, key=lambda d: (-ratio[d], d)):
        ys, xs = cells_d[did]
        fb = None
        # wioski rybackie v6: najpierw tyle miejsc nad woda (morze / jezioro do FISH_SEA), swoja ziemia, potem sasiedzi - rybacy zostaja rybakami
        fish_need = sum(1 for i in moved_by_d[did] if rows[i]["model"] == "fishing")
        if fish_need:
            wm = dsea[ys, xs] <= FISH_SITE
            wys, wxs = ys[wm], xs[wm]
            fbw = None
            while fish_need > 0 and need[did] > 0:
                p = try_place(did, wys, wxs, D)
                how = "own"
                if p is None:
                    if fbw is None:
                        f_ = fb_cells(did)
                        if f_ is not None:
                            mw = dsea[f_[0], f_[1]] <= FISH_SITE
                            fbw = (f_[0][mw], f_[1][mw], f_[2][mw])
                    if fbw is not None and len(fbw[0]):
                        p = try_place(did, fbw[0], fbw[1], D, pen=FB_PEN + fbw[2])
                        how = "fb"
                if p is None:
                    break
                commit(did, p, how)
                need[did] -= 1
                fish_need -= 1
        while need[did] > 0:
            p = try_place(did, ys, xs, D)
            how = "own"
            if p is None:
                if fb is None:
                    fb = fb_cells(did)
                if fb is not None:
                    p = try_place(did, fb[0], fb[1], D, pen=FB_PEN + fb[2])
                    how = "fb"
            if p is None:
                break
            commit(did, p, how)
            need[did] -= 1
    if A.diag:
        for did in sorted((d for d in moved_by_d if need[d] > 0), key=lambda d: (-ratio[d], d)):
            fb = fb_cells(did)
            print("DIAG", did, cult.get(did, "?"), "moved", len(moved_by_d[did]), "cap", cap_d[did], "ratio %.2f" % ratio[did], "left", need[did],
                  "cells", len(cells_d[did][0]), "fb", (len(fb[0]) if fb is not None else -1))
    # reszta: odleglosc obnizana stopniami (swoja ziemia, potem sasiedzi), az do odleglosci v6; bez miejsca - zostaje w v6
    for did in sorted((d for d in moved_by_d if need[d] > 0), key=lambda d: (-ratio[d], d)):
        ys, xs = cells_d[did]
        fb = fb_cells(did)
        fb2 = None
        while need[did] > 0:
            p, how = None, None
            f = 0.95
            # najpierw 0.95 / 0.90 / 0.85 D na swojej ziemi i u sasiadow (do R_FB), potem 0.85 D dalej (do R_FB2), na koncu do odleglosci v6 (wyspy, waskie pasy ladu)
            while p is None and f >= RELAX_FLOOR - 1e-6:
                p = try_place(did, ys, xs, D * f)
                how = "relax%.2f" % f
                if p is None and fb is not None:
                    p = try_place(did, fb[0], fb[1], D * f, pen=FB_PEN + fb[2])
                    how = "fb-relax%.2f" % f
                f -= 0.05
            if p is None:
                if fb2 is None:
                    fb2 = fb_cells(did, rfb=R_FB2)
                if fb2 is not None:
                    fb2p = (fb2[0], fb2[1], FB_PEN + fb2[2] * (R_FB / R_FB2))
                    p = try_place(did, fb2[0], fb2[1], D * RELAX_FLOOR, pen=fb2p[2])
                    how = "far-relax%.2f" % RELAX_FLOOR
            f = RELAX_FLOOR - 0.05
            while p is None and D * f >= D_LOW - 1e-6:
                p = try_place(did, ys, xs, D * f)
                how = "relax%.2f" % f
                if p is None and fb is not None:
                    p = try_place(did, fb[0], fb[1], D * f, pen=FB_PEN + fb[2])
                    how = "fb-relax%.2f" % f
                f -= 0.05
            need[did] -= 1
            if p is None:
                exc["bez miejsca - zostaje w miejscu v6"] += 1
                sites[did].append(None)
                continue
            exc[how] += 1
            exc_list.append((did, how))
            commit(did, p, how)

    # ---------- 6. przydzial wiosek do miejsc (metoda wegierska w okregu)
    newpos = {}
    for did, lst in moved_by_d.items():
        st_ = sites[did]
        real = [s for s in st_ if s is not None]
        stay = len(st_) - len(real)
        vill = list(lst)
        # bez miejsca: zostaje najmniej ruchliwa (najmniejsza) - w miejscu v6
        if stay > 0:
            vill.sort(key=lambda i: (rows[i]["_s"], rows[i]["uid"]))
            for i in vill[:stay]:
                newpos[i] = None
            vill = vill[stay:]
        if not vill:
            continue
        S = S_d[did]
        cost = []
        for i in vill:
            r = rows[i]
            fishv = r["model"] == "fishing"
            row = []
            for (x1, y1, ix, iy, how) in real:
                c = math.hypot(x1 - r["_x"], y1 - r["_y"]) / S
                wat = float(dsea[iy, ix]) <= FISH_SITE
                wild = CODE[iy, ix] in (0, 1, 2, 4, 5) or float(slope[iy, ix]) >= HILL_SLOPE or (float(slope[iy, ix]) >= 0.12 and float(dmnt[iy, ix]) <= 8 and float(driv[iy, ix]) > 4)
                if fishv and not wat:
                    c += 1.5
                if r["_h6"] and not wild:
                    c += 0.4
                row.append(c)
            cost.append(row)
        asg = hungarian(cost)
        for k, i in enumerate(vill):
            newpos[i] = real[asg[k]]

    # ---------- 6b. rozproszenie: kazda przeniesiona wioska po kolei przechodzi na najlepsze miejsce swojej ziemi (pustka do odstepu S wioski, woda, pola,
    #             skraj lasu, wzgorza, szum), zawsze >= D od wszystkich - wioski rozchodza sie z ciasnego upakowania po calym ladzie okregu; kilka przebiegow
    allx = []; ally = []; owner = []
    for vid, (x, y) in mainv.items():
        allx.append(x); ally.append(y); owner.append(-1)
    for i in kept:
        allx.append(rows[i]["_x"]); ally.append(rows[i]["_y"]); owner.append(-1)
    pidx = {}
    for i in sorted(newpos):
        if newpos[i] is None:
            allx.append(rows[i]["_x"]); ally.append(rows[i]["_y"]); owner.append(-1)
            continue
        pidx[i] = len(allx)
        allx.append(newpos[i][0]); ally.append(newpos[i][1]); owner.append(i)
    PX = np.array(allx, dtype=np.float64); PY = np.array(ally, dtype=np.float64)
    S2 = {}
    for did, rs in dist.items():
        li = vidx.get(did)
        a = float(A_d[li]) if li is not None else 0.0
        S2[did] = max(D, min(S_CAP, S_HEX * math.sqrt(a / (len(rs) + 1)) if a > 0 else D))
    moves = Counter()
    relax_order = sorted(pidx, key=lambda i: fnv1a(rows[i]["uid"] + "|relax"))
    for sweep in range(RELAX_SWEEPS):
        nmove = 0
        for i in relax_order:
            r = rows[i]
            did = r["village_id"]
            k0 = pidx[i]
            x_cur, y_cur = PX[k0], PY[k0]
            ys, xs = cells_d[did]
            pen = np.zeros(len(xs), dtype=np.float32)
            how0 = newpos[i][4]
            if how0 != "own":
                fb = fb_cells(did, rfb=RELAX_FB_R, around=(x_cur, y_cur))
                if fb is not None and len(fb[0]):
                    ys = np.concatenate([ys, fb[0]]); xs = np.concatenate([xs, fb[1]])
                    pen = np.concatenate([pen, FB_PEN + fb[2]])
            if r["model"] == "fishing" and dsea[int(math.floor(y_cur)), int(math.floor(x_cur))] <= FISH_SITE:
                wm = dsea[ys, xs] <= FISH_SITE                  # rybacy nad woda zostaja nad woda
                ys, xs, pen = ys[wm], xs[wm], pen[wm]
            if len(xs) == 0:
                continue
            Sv = S2[did] * (0.8 + 0.4 * u01(r["uid"] + "|S"))
            dmin_i = D if how0 in ("own", "fb") else D * float(how0.split("relax")[1])
            ccx = xs.astype(np.float64) + 0.5; ccy = ys.astype(np.float64) + 0.5
            x0b, x1b, y0b, y1b = ccx.min() - Sv - 1, ccx.max() + Sv + 1, ccy.min() - Sv - 1, ccy.max() + Sv + 1
            sel = (PX >= x0b) & (PX <= x1b) & (PY >= y0b) & (PY <= y1b)
            sel[k0] = False
            qx, qy = PX[sel], PY[sel]
            if len(qx):
                dn = np.full(len(xs), 1e6)
                for c0 in range(0, len(qx), 64):
                    d2 = (ccx[:, None] - qx[None, c0:c0 + 64]) ** 2 + (ccy[:, None] - qy[None, c0:c0 + 64]) ** 2
                    np.minimum(dn, np.sqrt(d2.min(axis=1)), out=dn)
            else:
                dn = np.full(len(xs), 1e6)
            fishv = r["model"] == "fishing"
            sc0 = np.minimum(dn, Sv) / Sv + BETA * attr[ys, xs] + GAMMA * noise[ys, xs] - pen
            if fishv:
                sc0 = sc0 + np.where(dsea[ys, xs] <= FISH_SITE, FISH_BONUS, 0.0)
            # wynik obecnego miejsca (ta sama miara)
            cix, ciy = int(math.floor(x_cur)), int(math.floor(y_cur))
            d_cur = np.sqrt((qx - x_cur) ** 2 + (qy - y_cur) ** 2).min() if len(qx) else 1e6
            in_own = 0 <= ciy < H and 0 <= cix < W and terr_lab[ciy, cix] == vidx.get(did, -99)
            mvx_, mvy_ = mainv.get(did, (x_cur, y_cur))
            pen_cur = 0.0 if in_own else FB_PEN + math.hypot(x_cur - mvx_, y_cur - mvy_) / R_FB
            sc_cur = min(d_cur, Sv) / Sv + BETA * float(attr[ciy, cix]) + GAMMA * float(noise[ciy, cix]) - pen_cur + (FISH_BONUS if fishv and dsea[ciy, cix] <= FISH_SITE else 0.0)
            passes = [D] if how0 in ("own", "fb") else [D, dmin_i]
            done = False
            for dm in passes:
                sc = np.where(dn >= dm, sc0, -1e9)
                base = sc_cur if d_cur >= dm - 1e-6 else -1e8
                idx = np.argsort(-sc, kind="stable")[:60]
                for k in idx.tolist():
                    if sc[k] < -1e8 or sc[k] <= base + RELAX_GAIN:
                        break
                    jx = (u01("%s|%d|%d|rx" % (r["uid"], xs[k], ys[k])) - 0.5) * 0.6
                    jy = (u01("%s|%d|%d|ry" % (r["uid"], xs[k], ys[k])) - 0.5) * 0.6
                    x1, y1 = float(xs[k]) + 0.5 + jx, float(ys[k]) + 0.5 + jy
                    jxi, jyi = int(math.floor(x1)), int(math.floor(y1))
                    if not (0 <= jxi < W and 0 <= jyi < H) or not site_ok[jyi, jxi] or not probe_ok(x1, y1):
                        continue
                    dd_ = np.sqrt((PX - x1) ** 2 + (PY - y1) ** 2)
                    dd_[k0] = 1e9
                    if dd_.min() < dm:
                        continue
                    own_new = terr_lab[jyi, jxi] == vidx.get(did, -99)
                    PX[k0], PY[k0] = x1, y1
                    newpos[i] = (x1, y1, jxi, jyi, ("own" if own_new else "fb") if dm >= D - 1e-6 else how0)
                    nmove += 1
                    moves[sweep] += 1
                    done = True
                    break
                if done:
                    break
        if nmove == 0:
            break

    # ---------- 7. miejsce, obrazek, zajecie przeniesionych wiosek
    used_names = set(r["name"] for r in rows)
    sea_comp = {}

    def water_kind(ix, iy):
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

    moved = [i for i in range(len(rows)) if i in newpos and newpos[i] is not None]
    stayed_v6 = [i for i in range(len(rows)) if i in newpos and newpos[i] is None]
    for i in moved:
        r = rows[i]
        x1, y1, ix, iy, how = newpos[i]
        zone = kzone.get(r["uid"]) or dzone.get(r["village_id"]) or "3-umiark"
        terr = TN[int(CODE[iy, ix])]
        ds_, dr_, dm_ = float(min(255.0, dsea[iy, ix])), float(min(255.0, driv[iy, ix])), float(min(255.0, dmnt[iy, ix]))
        sl = float(slope[iy, ix])
        hilly = sl >= HILL_SLOPE or (sl >= 0.12 and dm_ <= 8 and dr_ > 4)   # w ROT sciany rzek to gory - przy rzece d_mount nic nie mowi o wzgorzach
        if ds_ <= FISH_SEA or (ds_ <= FISH_SITE and r["model"] == "fishing"):
            place = water_kind(ix, iy)
            cat = place
        elif dr_ <= 3.0 and terr in ("trawa", "step", "las"):
            place = "river"; cat = "river"
        elif terr in ("trawa", "step") and not hilly:
            place = "field"; cat = "field"
        else:
            cat = "hamlet"
            place = {"las": "forest", "snieg": "moor", "piasek": "desert"}.get(terr, "hills" if hilly else ("steppe" if terr == "step" else "wilds"))
        h = dict(x=x1, y=y1, ix=ix, iy=iy, how=how, zone=zone, terr=terr, d_sea=ds_, d_river=dr_, d_mount=dm_, slope=sl, place=place, cat=cat, hilly=hilly,
                 deep=float(deep[iy, ix]), face=int(face[iy, ix]))
        r["_n"] = h

    # ---------- osady: przeniesione wioski okregu dziela sume swoich osad v6 wedlug miejsca (suma okregu = v6)
    size_fallback = 0
    for did, rs in dist.items():
        mv = [r for r in rs if "_n" in r]
        for r in rs:
            r["_s7"] = r["_s"]
        if not mv:
            continue
        T_ = sum(r["_s"] for r in mv)
        cap_d = min(S_MAX, max(r["_s"] for r in rs))
        lo = {r["uid"]: (H_LO if r["_n"]["cat"] == "hamlet" else min(V_LO, cap_d)) for r in mv}
        hi = {r["uid"]: cap_d for r in mv}
        if sum(lo.values()) > T_ or sum(hi.values()) < T_:
            size_fallback += 1
            continue
        w = {r["uid"]: WEIGHT[r["_n"]["cat"]] * (0.70 + 0.75 * u01(r["uid"] + "|v7")) for r in mv}
        val = {u: 0.0 for u in w}
        fixed_ = {}
        free = set(w)
        tot = float(T_)
        for _ in range(20):
            ws = sum(w[u] for u in free)
            rem = tot - sum(fixed_.values())
            ch = False
            for u in list(free):
                v = rem * w[u] / ws if ws > 0 else 0.0
                if v < lo[u]:
                    fixed_[u] = lo[u]; free.discard(u); ch = True
                elif v > hi[u]:
                    fixed_[u] = hi[u]; free.discard(u); ch = True
                else:
                    val[u] = v
            if not ch:
                break
        for u, v in fixed_.items():
            val[u] = float(v)
        iv = {u: int(math.floor(val[u])) for u in val}
        rest = T_ - sum(iv.values())
        for u in sorted(val, key=lambda u: (-(val[u] - iv[u]), u)):
            if rest <= 0:
                break
            if iv[u] < hi[u]:
                iv[u] += 1; rest -= 1
        guard = 0
        while rest != 0 and guard < 1000:
            guard += 1
            for u in sorted(iv):
                if rest > 0 and iv[u] < hi[u]:
                    iv[u] += 1; rest -= 1
                elif rest < 0 and iv[u] > lo[u]:
                    iv[u] -= 1; rest += 1
                if rest == 0:
                    break
        if rest != 0:
            size_fallback += 1
            continue
        for r in mv:
            r["_s7"] = iv[r["uid"]]

    # wioska na dzikiej ziemi wieksza niz H_HI osad to nie przysiolek - wies w lesie / na wzgorzach (zajecie v6, gdy ziemia je niesie)
    for i in moved:
        if rows[i]["_n"]["cat"] == "hamlet" and rows[i]["_s7"] > H_HI:
            rows[i]["_n"]["cat"] = "wild"
    # obrazek, poziom, zajecie i nazwa
    occ_n = Counter(); start_n = Counter(); start6_n = Counter(); place_n = Counter(); model_n = Counter(); renamed = 0
    for i in moved:
        r = rows[i]; h = r["_n"]
        people = r["_s7"] * 250
        lv = 1 if h["cat"] == "hamlet" else (3 if people >= 9000 else (2 if people >= 5000 else 1))
        fishv = r["model"] == "fishing"
        cat = h["cat"]
        uq = u01(r["uid"] + "|model7")
        if cat in ("coast", "lake"):
            model = "fishing" if fishv else ("farm" if lv == 1 else "village")
        elif cat == "river":
            model = "farm" if lv == 1 else ("mill" if uq < 0.5 else "village")
        elif cat == "field":
            model = "farm" if lv == 1 else (("granary" if uq < 0.3 else "village") if lv == 3 else ("windmill" if uq < 0.3 else ("granary" if uq < 0.45 else "village")))
        elif cat == "wild":
            model = "farm" if lv == 1 else "village"
        else:
            model = "farm"
        o = dict(zone=h["zone"], terr=h["terr"], kind=("coast" if cat in ("coast", "lake") else ("river" if h["d_river"] <= 3 else "field")), model=model,
                 d_sea=h["d_sea"], d_river=h["d_river"], d_mount=h["d_mount"])
        old_start = (r["ks_start"] if r["_h6"] and r["ks_start"] != "-" else kstart.get(r["uid"], "")) or "wheat_farm"
        start6_n[old_start] += 1
        occ = "-"
        if cat == "hamlet":
            best, bs = None, -1.0
            for t in WILD:
                if (t == "lumberjack" and h["terr"] != "las") or (t == "trapper" and h["terr"] not in ("las", "snieg")):
                    continue
                v = land(t, o) * PREF[t]
                if v > bs + 1e-9:
                    best, bs = t, v
            if bs <= 0:
                best = "hunter"
            hil = h["hilly"] or h["terr"] == "piasek"
            occ = {"sheep_farm": "goatherds" if hil else "shepherds", "trapper": "trappers", "hunter": "hunters", "cattle_farm": "drovers",
                   "swine_farm": "swineherds"}.get(best)
            if best == "lumberjack":
                occ = "charcoal" if h["deep"] >= 3 and fnv1a(r["uid"] + "|coal") % 2 == 0 else "woodcutters"
            start = best
        else:
            special = old_start not in CROPS and old_start not in ("fisherman", "whaler", "hunter")
            if special:
                start = old_start                      # stadnina, kopalnia - zasob okregu
            elif model == "fishing":
                start = "whaler" if old_start == "whaler" else "fisherman"
            elif old_start in FARM and land(old_start, o) >= 0.45:
                start = old_start
            else:
                sc = {t: land(t, o) for t in FARM}
                bt = max(sorted(sc), key=lambda t: sc[t])
                start = "wheat_farm" if sc["wheat_farm"] >= 0.8 * sc[bt] else bt
        h.update(level=lv, model=model, o=o, occ=occ, start=start)
        occ_n[occ] += 1; start_n[start] += 1; place_n[h["place"]] += 1; model_n[model] += 1
        cul = cult.get(r["village_id"], "")
        nm = r["name"]
        if cat == "hamlet" and cul in WESTEROS:
            if not (r["_h6"] and r["hamlet"] == occ):
                nm = hamlet_name(occ, h["place"], r["uid"], used_names)
                used_names.add(nm)
        if nm != r["name"]:
            renamed += 1
        h["name"] = nm
        h["culture"] = cul
        # orientacja: front do wsi gry; droga od wsi gry z rastra okregow (jak v6)
        mvx, mvy = mainv.get(r["village_id"], (h["x"], h["y"]))
        ang = math.degrees(math.atan2(mvy - h["y"], mvx - h["x"]))
        rd = float(rdist[h["iy"], h["ix"]])
        if not (0 < rd < 1000) or lab[h["iy"], h["ix"]] != vidx.get(r["village_id"], -99):
            rd = 1.3 * math.hypot(mvx - h["x"], mvy - h["y"])
        fr = ang - 90.0
        while fr <= -180.0:
            fr += 360.0
        while fr > 180.0:
            fr -= 360.0
        h.update(front=fr, road_deg=ang % 180.0, rd=rd)

    # ---------- kolumny okregu: toward_village (przodek, ktory zostal), road_dist, order, neighbors (regula v4, jak v6)
    by_uid = {r["uid"]: r for r in rows}
    mset = {rows[i]["uid"] for i in moved}
    for r in rows:
        r["_tw0"] = r["toward_village"] if r["toward_village"] != "-" else ""
        r["_rd"] = r["road_dist"]
    for r in rows:
        if r["uid"] in mset:
            r["_tw"] = ""
            r["_rd"] = "%.1f" % r["_n"]["rd"]
            continue
        t, seen = r["_tw0"], set()
        while t and (t in mset) and t not in seen:
            seen.add(t)
            t = by_uid[t]["_tw0"] if t in by_uid else ""
        r["_tw"] = t if t in by_uid and t not in mset else ""
    for did, rs in dist.items():
        recompute(rs)
    for r in rows:
        if r["uid"] not in mset:
            people = r["_s7"] * 250
            r["_lv"] = int(r["level"])
        else:
            r["_lv"] = r["_n"]["level"]

    # ---------- miary v7
    pos7 = [(r["_n"]["x"], r["_n"]["y"]) if r["uid"] in mset else (r["_x"], r["_y"]) for r in rows]
    nn7_ss = nn_list(pos7)
    nn7_sm = nn_list(pos7, mains)

    def coverage(pts_side):
        allp = list(pts_side) + mains + towns + castles
        dr = dist_raster(allp, 80)
        land_d = passable & (tlab >= 0)
        tot = float(land_d.sum())
        sh = {X: float((dr[land_d] <= X).sum()) / tot for X in (8, 12, 16, 20, 25)}
        # najwieksze pustki: maksima odleglosci z tlumieniem w promieniu 40 jedn.
        dd = np.where(land_d, dr, 0.0)
        gaps = []
        work = dd.copy()
        for _ in range(12):
            k = int(np.argmax(work))
            v = float(work.ravel()[k])
            if v <= 0:
                break
            gy2, gx2 = divmod(k, W)
            li2 = int(tlab[gy2, gx2])
            gaps.append((v, gx2 + 0.5, gy2 + 0.5, cult.get(vids[li2], "?") if li2 >= 0 else "-", vids[li2] if li2 >= 0 else "-"))
            x0, x1 = max(0, gx2 - 40), min(W, gx2 + 41); y0, y1 = max(0, gy2 - 40), min(H, gy2 + 41)
            work[y0:y1, x0:x1] = 0.0
        # pokrycie okregow: udzial ziemi okregu w 15 jedn. od osady
        per = []
        lt = tlab[land_d]
        within = (dr[land_d] <= 15)
        tot_d = np.bincount(lt, minlength=len(vids)).astype(np.float64)
        in_d = np.bincount(lt, weights=within.astype(np.float64), minlength=len(vids))
        for did in dist:
            li3 = vidx.get(did)
            if li3 is not None and tot_d[li3] >= 200:
                per.append(in_d[li3] / tot_d[li3])
        per.sort()
        return sh, gaps, per, float(np.percentile(dr[land_d], 95)), float(np.percentile(dr[land_d], 99))

    cov6 = coverage(side6)
    cov7 = coverage(pos7)

    # ---------- zapis ukladu v7 (kolejnosc wierszy jak v6 = v5; nowe kolumny v6_* na koncu)
    v6cols = ["v6_order", "v6_x", "v6_y", "v6_settlements"]
    out = ["# Armoury: named map villages (layout v7: villages spread over the whole land of their district - from layout v6, the same villages and people per district). One row = one map village"]
    for hline in head[1:]:
        if hline[1:].startswith("id\t"):
            out.append("# v7 (tools/wioski-v7/v7_uklad.py from arm_map_villages_v6.tsv): minimum distance village-village and village-game village %.2f (2 x v6 %.2f); %d villages"
                       % (D, v6_min, len(moved)))
            out.append("# of crowded clusters moved to the empty land of their district (water, fields, forest edges, hills); people of the moved villages of a district")
            out.append("# shared among them by their new place (district sum = v6). ks_* = land of the point for KS for every moved village (and v6 hamlets); hamlet = occupation.")
            out.append("# v6_order / v6_x / v6_y / v6_settlements = the same row in layout v6 (every row): a save of layout v6 maps by uid from this file alone.")
            out.append("# min_distance: %.2f" % D)
            out.append(hline + "\t" + "\t".join(v6cols))
            continue
        out.append(hline)
    for r in rows:
        c = list(r["_c"])
        c[ci["settlements"]] = str(r["_s7"])
        c[ci["level"]] = str(r["_lv"])
        c[ci["order"]] = str(r["_order"])
        c[ci["toward_village"]] = r["_tw"] or "-"
        c[ci["neighbors"]] = ",".join(r["_nb"]) or "-"
        if r["uid"] in mset:
            h = r["_n"]; o = h["o"]
            c[ci["name"]] = h["name"]
            c[ci["x"]] = "%.2f" % h["x"]
            c[ci["y"]] = "%.2f" % h["y"]
            c[ci["road_deg"]] = "%.1f" % h["road_deg"]
            c[ci["kind"]] = h["place"]
            c[ci["class"]] = "F"
            c[ci["side"]] = "0"
            c[ci["road_dist"]] = "%.1f" % h["rd"]
            c[ci["front_deg"]] = "%.1f" % h["front"]
            c[ci["half_len"]] = "1.10" if h["level"] == 1 else "1.30"
            c[ci["half_wid"]] = "0.70"
            c[ci["face"]] = str(h["face"])
            c[ci["model"]] = h["model"]
            c[ci["hamlet"]] = h["occ"]
            c[ci["ks_start"]] = h["start"]
            c[ci["ks_terr"]] = o["terr"]
            c[ci["ks_kind"]] = o["kind"]
            c[ci["ks_model"]] = o["model"]
            c[ci["ks_dsea"]] = "%.1f" % o["d_sea"]
            c[ci["ks_driver"]] = "%.1f" % o["d_river"]
            c[ci["ks_dmount"]] = "%.1f" % o["d_mount"]
        c += [r["_c"][ci[k]] for k in ("order", "x", "y", "settlements")]
        out.append("\t".join(c))
    crc7 = zlib.crc32("".join("%s:%d;" % (r["uid"], r["_s7"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF
    crc6 = zlib.crc32("".join("%s:%d;" % (r["uid"], r["_s"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF
    crc5c = zlib.crc32("".join("%s:%s;" % (r["uid"], r["v5_settlements"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF
    if not A.dry:
        with io.open(OUT_LAYOUT, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(out) + "\n")

    # ---------- lore v7: wiersze v6; przeniesione - zdanie miejsca, mlyn / sieci bez wody, historia "o drodze"; przysiolki jak v6
    lore_out, lore_rows, lore_seen, lore_moved, knight_ren, lore_mill, lore_fish, lore_hist = [], 0, set(), 0, 0, 0, 0, 0
    lcols = None
    for raw in io.open(IN_LORE, encoding="utf-8-sig"):
        line = raw.rstrip("\r\n")
        if not line.strip():
            continue
        if line[0] == "#":
            if line[1:].strip().startswith("layout_crc:"):
                lore_out.append("# layout_crc: %08X" % crc7)
                lore_out.append("# v7: rows of the v6 lore (layout_crc %08X); villages moved by tools/wioski-v7/v7_uklad.py get the sentence of their new place (hamlets: a new"
                                " livelihood and history as in v6), the rest unchanged." % crc6)
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
        if uid in mset and lcols is not None:
            li = {k: j for j, k in enumerate(lcols)}
            h = r["_n"]
            while len(c) < len(lcols):
                c.append("")
            if h["cat"] == "hamlet":
                lv = LIVE[h["occ"]]
                if not (r["_h6"] and r["hamlet"] == h["occ"]):
                    c[li["livelihood"]] = lv[fnv1a(uid + "|live") % len(lv)] + " " + PLACE_LINE[h["place"]]
                    c[li["history"]] = HIST[fnv1a(uid + "|hist") % len(HIST)]
                else:
                    c[li["livelihood"]] = lv[fnv1a(uid + "|live") % len(lv)] + " " + PLACE_LINE[h["place"]]
            else:
                live = c[li["livelihood"]].strip()
                if r["_h6"]:
                    live = "Farmers: grain, beans and a few cattle on the land they cleared."   # dawny przysiolek na polu / nad woda
                for rx in OLD_PLACE_RX:
                    live2 = rx.sub("", live).strip()
                    if live2 != live:
                        live = live2
                        break
                if h["model"] != "mill" and ("mill" in live.lower()):
                    for rx, rp in MILL_RX:
                        live = rx.sub(rp, live)
                    lore_mill += 1
                if h["model"] != "fishing" and live.startswith("Fishers"):
                    live = "Farmers: grain, beans and a few cattle; the old men still mend nets they no longer use."
                    lore_fish += 1
                np_ = NEW_PLACE_WILD.get(h["place"], NEW_PLACE["field"]) if h["cat"] == "wild" else NEW_PLACE[h["cat"] if h["cat"] in NEW_PLACE else "field"]
                live = (live + " " + np_[fnv1a(uid + "|place7") % len(np_)]).strip()
                c[li["livelihood"]] = live
                if ROAD_HIST.search(c[li["history"]] or ""):
                    c[li["history"]] = NEW_HIST[fnv1a(uid + "|hist7") % len(NEW_HIST)]
                    lore_hist += 1
            old = r["name"]
            kn = c[li["knight"]]
            if h["name"] != old and kn.endswith(" of " + old):
                c[li["knight"]] = kn[: -len(old)] + h["name"]
                knight_ren += 1
            lore_moved += 1
            line = "\t".join(c)
        lore_out.append(line)
        lore_rows += 1
        lore_seen.add(uid)
    if not A.dry:
        with io.open(OUT_LORE, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lore_out) + "\n")

    # ---------- lista przeniesionych wiosek
    lst = ["uid\tokreg\tkultura\tnazwa_v6\tnazwa_v7\tmiejsce\tzajecie\tks_start_v6\tks_start\tobrazek\tteren\tx6\ty6\tx7\ty7\tprzesuniecie\tosady6\tosady7\tdroga"]
    for i in moved:
        r = rows[i]; h = r["_n"]
        old_start = (r["ks_start"] if r["_h6"] and r["ks_start"] != "-" else kstart.get(r["uid"], "")) or "-"
        lst.append("\t".join([r["uid"], r["village_id"], h["culture"], r["name"], h["name"], h["place"], h["occ"], old_start, h["start"], h["model"], h["terr"],
                              "%.2f" % r["_x"], "%.2f" % r["_y"], "%.2f" % h["x"], "%.2f" % h["y"], "%.1f" % math.hypot(h["x"] - r["_x"], h["y"] - r["_y"]),
                              str(r["_s"]), str(r["_s7"]), h["how"]]))
    if not A.dry:
        with io.open(OUT_LIST, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lst) + "\n")

    # ---------- raport
    rep = []
    s6ss, s7ss, s6sm, s7sm = stats(nn6_ss), stats(nn7_ss), stats(nn6_sm), stats(nn7_sm)
    c6, c7 = crowd(side6), crowd(pos7)
    dsum_bad = sum(1 for d, rs in dist.items() if sum(r["_s7"] for r in rs) != s6_sum[d])
    l4_bad = sum(1 for d, rs in dist.items() if d in ngen and sum(r["_s7"] for r in rs) + max(r["_s7"] for r in rs) > ngen[d]
                 and not (sum(r["_s"] for r in rs) + max(r["_s"] for r in rs) > ngen[d]))
    mvd = [math.hypot(rows[i]["_n"]["x"] - rows[i]["_x"], rows[i]["_n"]["y"] - rows[i]["_y"]) for i in moved]
    below_ss = int((nn7_ss < D - 1e-6).sum()); below_sm = int((nn7_sm < D - 1e-6).sum())
    mm = nn_list(mains)
    rep.append("UKLAD v7 (tools/wioski-v7/v7_uklad.py) z %s" % os.path.basename(IN_LAYOUT))
    rep.append("odleglosc minimalna D = 2 x min v6 wioska-wioska %.2f = %.2f jedn. (v6 wioska-wies gry min %.2f); odstep docelowy okregu S = %.2f x sqrt(ziemia / (wioski + wies gry)) w granicach D..%.0f:"
               " mediana %.1f, 10%% %.1f, 90%% %.1f" % (v6_min, D, s6sm["min"], S_HEX, S_CAP, float(np.median(list(S_d.values()))),
                                                     float(np.percentile(list(S_d.values()), 10)), float(np.percentile(list(S_d.values()), 90))))
    rep.append("wiosek pobocznych %d (te same uid, okregow %d); zostaly w miejscu v6 %d, przeniesione %d = %.1f%% (bez miejsca - zostaly w v6 mimo regul %d)" % (
        len(rows), len(dist), len(kept) + len(stayed_v6), len(moved), 100.0 * len(moved) / len(rows), len(stayed_v6)))
    rep.append("przeniesione na: ziemie swojego okregu %d, ziemie sasiada na tym samym ladzie %d, z obnizona odlegloscia %d (%s)" % (
        sum(1 for i in moved if rows[i]["_n"]["how"] == "own"), sum(1 for i in moved if rows[i]["_n"]["how"] == "fb"),
        sum(1 for i in moved if rows[i]["_n"]["how"] not in ("own", "fb")), ", ".join("%s %d" % kv for kv in sorted(Counter(rows[i]["_n"]["how"] for i in moved if rows[i]["_n"]["how"] not in ("own", "fb")).items())) or "-"))
    rep.append("przesuniecie: min %.1f, mediana %.1f, 90%% %.1f, max %.1f jedn." % (min(mvd), float(np.median(mvd)), float(np.percentile(mvd, 90)), max(mvd)) if mvd else "przesuniecie: -")
    rep.append("najblizsza wioska (wioska-wioska): v6 min %.2f / 10%% %.2f / mediana %.2f / srednia %.2f; v7 min %.2f / 10%% %.2f / mediana %.2f / srednia %.2f" % (
        s6ss["min"], s6ss["p10"], s6ss["med"], s6ss["mean"], s7ss["min"], s7ss["p10"], s7ss["med"], s7ss["mean"]))
    rep.append("najblizsza wies gry (wioska-wies gry): v6 min %.2f / 10%% %.2f / mediana %.2f; v7 min %.2f / 10%% %.2f / mediana %.2f" % (
        s6sm["min"], s6sm["p10"], s6sm["med"], s7sm["min"], s7sm["p10"], s7sm["med"]))
    rep.append("pary ponizej D: wioska-wioska v6 %d -> v7 %d, wioska-wies gry v6 %d -> v7 %d; wies gry - wies gry (dane ROT, bez zmian) %d wsi ponizej D, min %.2f" % (
        int((nn6_ss < D).sum()), below_ss, int((nn6_sm < D).sum()), below_sm, int((mm < D).sum()), float(mm.min())))
    rep.append("skupiska (wiosek z >= 3 sasiadami w 10 jedn.): v6 %d, v7 %d; najwiecej sasiadow v6 %d, v7 %d; srednio v6 %.2f, v7 %.2f" % (c6[0], c7[0], c6[1], c7[1], c6[2], c7[2]))
    for nm_, cv in (("v6", cov6), ("v7", cov7)):
        sh, gaps, per, p95, p99 = cv
        rep.append("pokrycie ladu okregow (%s; osady = wioski, wsie gry, miasta, zamki): w 8 jedn. %.1f%%, 12 %.1f%%, 16 %.1f%%, 20 %.1f%%, 25 %.1f%%; odleglosc do osady 95%% %.1f, 99%% %.1f;"
                   " udzial ziemi okregu w 15 jedn. od osady: 10%% okregow %.0f%%, mediana %.0f%%" % (nm_, 100 * sh[8], 100 * sh[12], 100 * sh[16], 100 * sh[20], 100 * sh[25], p95, p99,
                                                                                              100 * per[len(per) // 10], 100 * per[len(per) // 2]))
        rep.append("najwieksze pustki %s (odleglosc do osady, x, y, kultura okregu): " % nm_ + "; ".join("%.1f @ %.0f,%.0f %s" % (g[0], g[1], g[2], g[3]) for g in gaps[:8]))
    rep.append("okregow o innej liczbie wiosek niz v6 0 (uid bez zmian); okregow o innej sumie osad wiosek niz v6 %d; L4 zlamane przez v7 %d; okregow z osadami v6 (bez miejsca na podzial) %d" % (
        dsum_bad, l4_bad, size_fallback))
    hs7 = [rows[i]["_s7"] for i in moved]
    rep.append("osady wiosek swiata: v6 %d, v7 %d; osady przeniesionych wiosek: min %d, mediana %d, max %d (v6: mediana %d)" % (
        sum(r["_s"] for r in rows), sum(r["_s7"] for r in rows), min(hs7), int(np.median(hs7)), max(hs7), int(np.median([rows[i]["_s"] for i in moved]))))
    rep.append("miejsce przeniesionych: " + ", ".join("%s %d" % kv for kv in place_n.most_common()) + "; obrazek: " + ", ".join("%s %d" % kv for kv in model_n.most_common()))
    rep.append("zajecia przysiolkow (przeniesione na dzika ziemie): " + ", ".join("%s %d" % kv for kv in occ_n.most_common() if kv[0] != "-"))
    hall7 = sum(1 for r in rows if (r["uid"] in mset and r["_n"]["occ"] != "-") or (r["uid"] not in mset and r["_h6"]))
    rep.append("przysiolkow: v6 %d, v7 %d (zostale v6 %d + nowe na dzikiej ziemi %d)" % (sum(1 for r in rows if r["_h6"]), hall7,
                                                                                         sum(1 for r in rows if r["uid"] not in mset and r["_h6"]),
                                                                                         sum(1 for i in moved if rows[i]["_n"]["occ"] != "-")))
    rep.append("zajecie startowe KS przeniesionych (ks_start): v6 " + ", ".join("%s %d" % kv for kv in start6_n.most_common(10)) + " -> v7 " + ", ".join("%s %d" % kv for kv in start_n.most_common(10)))
    pw6, pw7 = Counter(), Counter()
    for r in rows:
        st6 = (r["ks_start"] if r["_h6"] and r["ks_start"] != "-" else kstart.get(r["uid"], "")) or "?"
        st7 = r["_n"]["start"] if r["uid"] in mset else st6
        pw6[st6] += r["_s"]; pw7[st7] += r["_s7"]
    t6, t7 = float(sum(pw6.values())), float(sum(pw7.values()))
    keys_ = [k for k, _ in (pw6 + pw7).most_common(9)]
    rep.append("ludzie wioskowi wedlug zajecia startowego KS (udzial osad, wszystkie wioski): " + ", ".join("%s %.1f%% -> %.1f%%" % (k, 100 * pw6[k] / t6, 100 * pw7[k] / t7) for k in keys_))
    dmain6 = [math.hypot(r["_x"] - mainv[r["village_id"]][0], r["_y"] - mainv[r["village_id"]][1]) for r in rows]
    dmain7 = [math.hypot(pos7[k][0] - mainv[r["village_id"]][0], pos7[k][1] - mainv[r["village_id"]][1]) for k, r in enumerate(rows)]
    out_lab7 = sum(1 for k, r in enumerate(rows) if tlab[int(pos7[k][1]), int(pos7[k][0])] != vidx.get(r["village_id"], -99))
    out_ter7 = sum(1 for k, r in enumerate(rows) if not in_terr(r["village_id"], pos7[k][0], pos7[k][1]))
    rep.append("od wlasnej wsi gry: v6 mediana %.1f / 90%% %.1f / max %.1f, v7 mediana %.1f / 90%% %.1f / max %.1f jedn.; poza ziemia okregu KS (raster okregow) v6 0, v7 %d;"
               " poza ziemia okregu v7 (Voronoi wazony, okregow ponad pojemnosc %d z %d) %d" % (
                   float(np.median(dmain6)), float(np.percentile(dmain6, 90)), max(dmain6), float(np.median(dmain7)), float(np.percentile(dmain7, 90)), max(dmain7),
                   out_lab7, terr_hist[-1] if terr_hist else -1, len(wins), out_ter7))
    fish6 = sum(1 for r in rows if r["model"] == "fishing")
    fish7 = sum(1 for r in rows if (r["_n"]["model"] if r["uid"] in mset else r["model"]) == "fishing")
    rep.append("wioski z obrazkiem rybackim (HamletKinds poza KS): v6 %d, v7 %d" % (fish6, fish7))
    rep.append("wioski z ziemia KS (kolumny ks_*): v6 %d, v7 %d" % (sum(1 for r in rows if r["ks_start"] != "-"),
                                                                    sum(1 for r in rows if r["uid"] in mset or r["ks_start"] != "-")))
    rep.append("nazwy: zmienione %d (Westeros - przysiolki z nowym zajeciem); rycerz 'of <nazwa>' przemianowany %d" % (renamed, knight_ren))
    rep.append("lore v7: wierszy %d (wiosek %d, bez lore %d), przeniesionych z nowym zdaniem miejsca %d (mlyn bez wody %d, rybacy bez wody %d, historia 'o drodze' %d), bramka layout_crc %08X" % (
        lore_rows, len(rows), len(set(by_uid) - lore_seen), lore_moved, lore_mill, lore_fish, lore_hist, crc7))
    rep.append("od warowni (przeniesione): miasto min %.1f (prog %.0f), zamek min %.1f (%.0f), kryjowka min %.1f (%.0f); od malowanej drogi min %.1f (%.1f)" % (
        min(float(d_town[rows[i]["_n"]["iy"], rows[i]["_n"]["ix"]]) for i in moved), D_TOWN, min(float(d_castle[rows[i]["_n"]["iy"], rows[i]["_n"]["ix"]]) for i in moved), D_CASTLE,
        min(float(d_hide[rows[i]["_n"]["iy"], rows[i]["_n"]["ix"]]) for i in moved), D_HIDE, min(float(d_paint[rows[i]["_n"]["iy"], rows[i]["_n"]["ix"]]) for i in moved), D_PAINT))
    rep.append("na ladzie (przeniesione): raster %d/%d, sonda gry %d/%d" % (
        sum(1 for i in moved if not badw[rows[i]["_n"]["iy"], rows[i]["_n"]["ix"]]), len(moved), sum(1 for i in moved if probe_ok(rows[i]["_n"]["x"], rows[i]["_n"]["y"])), len(moved)))
    rep.append("crc ukladu (uid:osady; jak VillageBook): v6 %08X, v7 %08X; kolumny v5_* pliku v7 crc %08X (v5 68BB06AD)" % (crc6, crc7, crc5c))
    if exc_list:
        rel = [i for i in moved if rows[i]["_n"]["how"] not in ("own", "fb")]
        rep.append("obnizona odleglosc (po rozproszeniu %d wiosek) wedlug kultury okregu: " % len(rel) + ", ".join("%s %d" % kv for kv in Counter(cult.get(rows[i]["village_id"], "?") for i in rel).most_common())
                   + " | okregi: " + ", ".join("%s %d" % kv for kv in Counter(rows[i]["village_id"] for i in rel).most_common(25)))
    if not A.dry:
        with io.open(OUT_REPORT, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(rep) + "\n")
    print("\n".join(rep))

    # ---------- obrazki do przegladu (opcja)
    if A.png:
        try:
            from PIL import Image, ImageDraw
            os.makedirs(A.png, exist_ok=True)
            base = np.zeros((H, W, 3), dtype=np.uint8)
            base[:] = (40, 60, 90)
            base[passable] = (150, 150, 130)
            base[passable & forest] = (95, 125, 85)
            base[passable & (CODE == 0)] = (215, 215, 220)
            base[passable & (CODE == 1)] = (200, 180, 120)
            base[mnt & walk] = (110, 100, 95)
            base[painted] = (200, 120, 60)
            base[rivm] = (60, 110, 200)
            col7 = []
            for r in rows:
                if r["uid"] in mset:
                    hw = r["_n"]["how"]
                    col7.append((220, 20, 20) if hw == "own" else ((255, 140, 0) if hw == "fb" else (255, 255, 0)))
                else:
                    col7.append((150, 0, 150))
            okmask = np.zeros((H, W, 3), dtype=np.uint8)
            base2 = base.copy()
            base2[site_ok] = (base2[site_ok] * 0.8 + np.array([40, 40, 0]) * 0.2).astype(np.uint8)
            for nm_, pts in (("v6", side6), ("v7", pos7)):
                im = Image.fromarray((base2 if nm_ == "v7" else base)[::-1].copy())
                dr_ = ImageDraw.Draw(im)
                for (x, y) in towns:
                    dr_.rectangle([x - 3, H - y - 3, x + 3, H - y + 3], fill=(255, 255, 255))
                for (x, y) in castles:
                    dr_.rectangle([x - 2, H - y - 2, x + 2, H - y + 2], fill=(230, 230, 230))
                for (x, y) in mains:
                    dr_.ellipse([x - 2.5, H - y - 2.5, x + 2.5, H - y + 2.5], fill=(0, 0, 0))
                for k, (x, y) in enumerate(pts):
                    dr_.ellipse([x - 1.8, H - y - 1.8, x + 1.8, H - y + 1.8], fill=(col7[k] if nm_ == "v7" else (220, 20, 20)))
                im.save(os.path.join(A.png, "uklad-%s.png" % nm_))
            print("obrazki: " + A.png)
        except Exception as e:
            print("obrazki: blad %s" % e)

    badrun = dsum_bad > 0 or lore_rows != len(rows) or crc7 == crc6 or crc5c != 0x68BB06AD or any(not probe_ok(rows[i]["_n"]["x"], rows[i]["_n"]["y"]) for i in moved)
    return 1 if badrun else 0


if __name__ == "__main__":
    sys.exit(main())
