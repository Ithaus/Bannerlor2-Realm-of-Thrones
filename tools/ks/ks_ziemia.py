# KS (kalibracja startu, docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md rozdz. 2.2 i 11.1) - ZIEMIA OKREGOW Z MAPY dla gry: Armoury/ModuleData/arm_ks_land.tsv.
# Deterministyczny skrypt (te same wejscia = ten sam plik). Liczy dokladnie tak jak model v2 (scratchpad roz/kalibracja/ks_model2.py, ks_tabele.py, ks_tabele2.py):
#  - wies gry = 20 blokow: blok 0 = punkt wsi gry (ks_wsie_teren.tsv), bloki 1..19 = mieszanka ziemi CALEGO okregu z rastra (okregi.npz label + teren.npz + flora.npz:
#    teren, brzeg do 3 jedn., rzeka do 3 jedn., gory do 2 jedn., strefa klimatu z kultury), wazona przydatnoscia (habit), rozdzielona najwiekszymi resztami;
#  - wioska poboczna = jej punkt (teren_wiosek.tsv) i zajecie startowe (symulacja_upraw.tsv; 4 wioski New Ibbish bez wiersza - regula PO A3: obrazek "fishing" ->
#    rybak, w okregu wielorybnikow wielorybnik);
#  - pola mieszczan = 20 blokow z rdzenia twierdzy miasta (okregi.npz fort_core);
#  - powierzchnia uzytkowa okregu i pol mieszczan w ha (komorka 807 ha, tylko ziemia z najlepszym L >= 0.3 dla zajec pola);
#  - rynek (W = Westeros, E = Essos) wedlug skladowych grafu tras twierdz (geo-trasy.tsv), jak zone_raw modelu; twierdza poza duza skladowa - wedlug krainy.
# Uruchom (z katalogu repo): python -I tools/ks/ks_ziemia.py --roz <scratchpad roz> --teren <katalog a-teren> --uklad Armoury/ModuleData/arm_map_villages.tsv
#                            --out Armoury/ModuleData/arm_ks_land.tsv [--check <ks_dump.json>]
# --check: porownuje rozklad ziemi (spread) z blokami modelu v2 przy tej samej liczbie blokow (test zgodnosci z modelem).
import argparse, collections, csv, json, os, sys, zlib
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('--roz', required=True)
ap.add_argument('--teren', required=True)
ap.add_argument('--uklad', required=True)
ap.add_argument('--out', required=True)
ap.add_argument('--check', default=None)
A = ap.parse_args()
ROZ = A.roz; AT = A.teren
J = os.path.join

ISLE_CULTURES = ('sturgia', 'skagosi')
WEST = {'battania', 'freefolk', 'nightswatch', 'skagosi', 'sturgia', 'river', 'vale', 'vlandia', 'crownlands', 'stormlands', 'dragonstone', 'reach', 'aserai'}
HA_PER_CELL = 807.0

# ---------------------------------------------------------------- tabele przydatnosci (kopia ks_tabele.py - tylko zajecia pola do wag i powierzchni)
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
FIELD = ('wheat_farm', 'flax_plant', 'silk_plant', 'cattle_farm', 'cattle_farm:maslo', 'garden', 'citrus', 'vineyard', 'swine_farm')
FIELD_USE = ('wheat_farm', 'cattle_farm', 'sheep_farm', 'olive_trees', 'date_farm', 'vineyard', 'garden')

def coast(o): return o['kind'] == 'coast' or o['d_sea'] <= 3 or o.get('model') == 'fishing'
def river(o): return o['kind'] in ('river', 'bridge', 'ford') or o['d_river'] <= 3

def water_mult(t, o):
    m = 1.0
    if river(o):
        m *= {'flax_plant': 1.3, 'silk_plant': 1.5, 'cattle_farm': 1.2, 'wheat_farm': 1.1, 'date_farm': 1.3}.get(t, 1.0)
        if t == 'wheat_farm' and o['zone'] in ('5-srodz', '6-goraco', '7-tropik'): m *= 1.5
    else:
        if t in ('silk_plant', 'date_farm') and o['zone'] in ('5-srodz', '6-goraco'): m *= 0.4
        if t == 'wheat_farm' and o['terr'] == 'piasek': m *= 0.3
    if coast(o): m *= {'olive_trees': 1.2}.get(t, 1.0)
    return m

def land(t, o):
    if t == 'cattle_farm:maslo': t = 'cattle_farm'
    if t in CROPS:
        w = ZW.get(o['zone'], {}).get(t, 0.0) * TW.get(o['terr'], {}).get(t, 1.0) * water_mult(t, o)
        if o.get('model') == 'fishing' and t in ('wheat_farm', 'flax_plant', 'silk_plant', 'vineyard', 'olive_trees', 'date_farm', 'cattle_farm', 'swine_farm'):
            w *= 0.5
        return min(1.5, w)
    if t == 'garden':
        zf = {'1-arktyka': 0.0, '2-zimno': 0.8, '3-umiark': 1.0, '4-cieplo': 1.0, '5-srodz': 0.6, '6-goraco': 0.3, '7-tropik': 0.2}.get(o['zone'], 0.0)
        return min(1.5, zf * {'snieg': 0, 'piasek': 0.2, 'step': 0.6, 'las': 0.7, 'gory': 0.6, 'bagno': 0.5}.get(o['terr'], 1.0) * (1.1 if river(o) else 1.0)
                   * (0.5 if o.get('model') == 'fishing' else 1.0))
    return 0.0

def land2(t, o):
    if o.get('zone') in HOT and o.get('terr') == 'piasek' and river(o) and t in FIELD:
        o = dict(o, terr='trawa')
    return land(t, o)

def habit(o):
    best = max(land2(c, o) for c in FIELD_USE)
    return 0.05 + max(best, 0.6 if coast(o) else 0.0)

def usable_ha(cls):
    return sum(c['cells'] * HA_PER_CELL for c in cls if max(land2(t, c) for t in FIELD_USE) >= 0.3)

def spread(cls, nb):
    if nb <= 0 or not cls: return []
    w = np.array([c['cells'] * habit(c) for c in cls]); w = w / w.sum() * nb
    base = np.floor(w).astype(int); rest = nb - base.sum()
    for j in np.argsort(-(w - base))[:rest]: base[j] += 1
    out = []
    for c, k in zip(cls, base): out += [c] * int(k)
    return out

def zone_of(c, y):
    if c in ('freefolk', 'skagosi', 'nightswatch'): z = '1-arktyka'
    elif c in ('battania', 'ibbenese', 'nord', 'sturgia'): z = '2-zimno'
    elif c in ('vale', 'river', 'empire', 'sarnor', 'norvos', 'crownlands', 'dragonstone', 'vlandia', 'qohorik', 'yiti'): z = '3-umiark'
    elif c in ('reach', 'stormlands'): z = '5-srodz' if y < 245 else '4-cieplo'
    elif c in ('pentoshi', 'khuzait'): z = '4-cieplo'
    elif c in ('tyroshi', 'myrish', 'lyseni', 'volantine', 'valyrian'): z = '5-srodz'
    elif c in ('aserai', 'ghiscari', 'qartheen'): z = '6-goraco'
    elif c == 'summer': z = '7-tropik'
    else: z = '3-umiark'
    return '2-wyspy' if c in ISLE_CULTURES else z

# ---------------------------------------------------------------- dane
def settlements():
    rows = [r for r in csv.reader(open(J(ROZ, 'zycie', 'ludnosc.tsv'), encoding='utf-8'), delimiter='\t') if r and not r[0].startswith('#')]
    h = rows[0]; ix = {k: i for i, k in enumerate(h)}
    out = collections.OrderedDict()
    for r in rows[1:]:
        if r[0] != 'osada': continue
        g = lambda k: r[ix[k]]
        try: ppl = float(g('ludzie_ksiega'))
        except ValueError: ppl = 0.0
        out[g('id')] = dict(id=g('id'), name=g('nazwa'), kind=g('rodzaj'), cul=g('kultura'), land=g('kraina_start') or g('kultura'), reg=g('region_id'),
                            vtype=g('typ_wsi'), people=ppl)
    return out
ST = settlements()
ML = {r['vid']: r for r in csv.DictReader(open(J(ROZ, 'kalibracja', 'ks_wsie_teren.tsv'), encoding='utf-8'), delimiter='\t')}
TER = {r['uid']: r for r in csv.DictReader(open(J(ROZ, 'inwestycja', 'teren_wiosek.tsv'), encoding='utf-8'), delimiter='\t')}
SIM = {r['uid']: r for r in csv.DictReader(open(J(ROZ, 'inwestycja', 'symulacja_upraw.tsv'), encoding='utf-8'), delimiter='\t')}

# uklad wiosek gry (uid, osady w kolejnosci pliku) - crc jak VillageBook.LayoutCrcOf ("uid:osady;" w kolejnosci pliku, zlib crc32)
lay = []
for line in open(A.uklad, encoding='utf-8'):
    if line.startswith('#') or not line.strip(): continue
    c = line.rstrip('\n').rstrip('\r').split('\t')
    lay.append((c[1], c[9], c[6]))   # village_id, uid, settlements
layout_crc = zlib.crc32(''.join('%s:%s;' % (u, s) for v, u, s in lay).encode('utf-8')) & 0xFFFFFFFF

# ---------------------------------------------------------------- raster: klasy ziemi okregow i rdzeni twierdz (jak land_classes modelu)
TN = ['snieg', 'piasek', 'las', 'step', 'gory', 'bagno', 'trawa']
_T = np.load(J(AT, 'teren.npz')); _F = np.load(J(AT, 'flora.npz')); _OK = np.load(J(AT, 'okregi.npz'))
_snowy = ((_T['w10_Snow'].astype(np.int32) + _T['w09_ICE'].astype(np.int32)) >= 128) | _T['snow']
_CODE = np.select([_snowy, _T['w05_Sand'] >= 128, _F['las_razem'] | _T['forest'], _T['w04_Steppe'] >= 128, _T['mountain'], _T['swamp']], [0, 1, 2, 3, 4, 5], 6).astype(np.int32)
_KEY = (_CODE * 8 + (_T['d_sea'].astype(np.float32) <= 3) * 4 + (_T['d_river'].astype(np.float32) <= 3) * 2 + (_T['d_mount'].astype(np.float32) <= 2)).ravel()
_YY = np.repeat(np.arange(_CODE.shape[0], dtype=np.float32), _CODE.shape[1])
def land_classes(labarr, ids, cul_of):
    flat = labarr.ravel(); idx = np.flatnonzero(flat >= 0); L = flat[idx]
    order = np.argsort(L, kind='stable'); idx = idx[order]; L = L[order]
    bounds = np.searchsorted(L, np.arange(len(ids) + 1))
    out = {}
    for i, vid in enumerate(ids):
        a, b = bounds[i], bounds[i + 1]
        if b <= a: continue
        cul = cul_of(vid)
        ks = _KEY[idx[a:b]]; ys = _YY[idx[a:b]]
        zs = np.where(ys < 245, 0, 1) if cul in ('reach', 'stormlands') else np.zeros(b - a, dtype=np.int64)
        u, cnt = np.unique(ks * 2 + zs, return_counts=True)
        cls = []
        for k, n in zip(u, cnt):
            zbit = int(k) & 1; kc = int(k) >> 1
            terr = TN[kc >> 3]; co = bool(kc & 4); rv = bool(kc & 2); mo = bool(kc & 1)
            cls.append(dict(zone=zone_of(cul, 200 if zbit == 0 else 300), terr=terr, kind='coast' if co else ('river' if rv else 'village'), model='village', main=True,
                            d_sea=1.0 if co else 50.0, d_river=1.0 if rv else 50.0, d_mount=1.0 if mo else 50.0, cells=int(n)))
        out[vid] = cls
    return out
CUL = lambda v: ST.get(v, {}).get('cul') or ''
DLAND = land_classes(_OK['label'], [str(v) for v in _OK['village_ids']], CUL)
_FL = np.where(_OK['fort_core'], _OK['fort_label'], -1)
TLAND = land_classes(_FL, [str(v) for v in _OK['fort_ids']], CUL)

# ---------------------------------------------------------------- rynki (zone_raw modelu: skladowe grafu tras twierdz)
E = []
for r in csv.DictReader(open(J(ROZ, 'geo-dane', 'geo-trasy.tsv'), encoding='utf-8'), delimiter='\t'):
    if r['rodzaj'] != 'twierdza-twierdza' or r['polaczenie'] == 'brak': continue
    try: dl = float(r['dl_trasy']) if r['polaczenie'] == 'lad' else float(r['dl_morze'])
    except ValueError: continue
    E.append((r['od'], r['do']))
G = collections.defaultdict(list)
for a, b in E: G[a].append(b); G[b].append(a)
comp = {}
for sid, s in ST.items():
    if s['kind'] not in ('miasto', 'zamek') or sid in comp: continue
    stk = [sid]; comp[sid] = sid
    while stk:
        u = stk.pop()
        for v in G[u]:
            if v not in comp: comp[v] = sid; stk.append(v)
size = collections.Counter(comp.values())
ZNAME = {}
for c, n in size.most_common():
    if n < 10: continue
    w = sum(1 for k, v in comp.items() if v == c and ST.get(k, {}).get('land') in WEST)
    ZNAME[c] = 'W' if w > size[c] / 2 else 'E'
def market(fid):
    c = comp.get(fid)
    if c in ZNAME: return ZNAME[c]
    return 'W' if ST.get(fid, {}).get('land') in WEST else 'E'

def f1(x): return '%.1f' % float(x)

# ---------------------------------------------------------------- wiersze
rows = []
HDR = ['rodzaj', 'okreg', 'uid', 'rynek', 'strefa', 'teren', 'miejsce', 'model', 'd_sea', 'd_river', 'd_mount', 'blok0', 'start', 'ha_okregu']
nmain = nside = ntown = 0
side_of = collections.defaultdict(list)
for v, u, s in lay: side_of[v].append(u)
for vid, s in ST.items():
    if s['kind'] != 'wies': continue
    ml = ML.get(vid)
    if ml is None: continue
    mz = '2-wyspy' if ml['culture'] in ISLE_CULTURES else ml['zone']
    pt = dict(zone=mz, terr=ml['terr'], kind=ml['kind'], model='village', d_sea=float(ml['d_sea']), d_river=float(ml['d_river']), d_mount=float(ml['d_mount']), main=True)
    cls = DLAND.get(vid, [])
    lands = [pt] + (spread(cls, 19) if cls else [pt] * 19)
    area = usable_ha(cls) if cls else 0.0
    mk = market(s['reg'])
    for k, o in enumerate(lands):
        rows.append(['M', vid, vid + '#main' + str(k), mk, o['zone'], o['terr'], o['kind'], 'village', f1(o['d_sea']), f1(o['d_river']), f1(o['d_mount']),
                     '1' if k == 0 else '0', '-', '%.0f' % area])
        nmain += 1
    for u in side_of.get(vid, []):
        t = TER.get(u)
        if t is None: continue
        sim = SIM.get(u)
        if sim is not None: start = sim['typ_wioski']; zone = '2-wyspy' if t.get('culture') in ISLE_CULTURES else sim['strefa']
        else:
            start = ('whaler' if t['dtype'] == 'whaler' else 'fisherman') if t['model'] == 'fishing' else t['dtype']
            zone = '2-wyspy' if t.get('culture') in ISLE_CULTURES else t['zone']
        terr = sim['teren'] if sim is not None else t['terr']
        kind = sim['kind'] if sim is not None else t['kind']
        model = sim['model'] if sim is not None else t['model']
        rows.append(['S', vid, u, mk, zone, terr, kind, model, f1(t.get('d_sea') or 255), f1(t.get('d_river') or 255), f1(t.get('d_mount') or 255), '0', start, '%.0f' % area])
        nside += 1
for tid, s in ST.items():
    if s['kind'] != 'miasto': continue
    cls = TLAND.get(tid, [])
    if not cls: continue
    area = usable_ha(cls)
    mk = market(tid)
    for k, o in enumerate(spread(cls, 20)):
        rows.append(['T', tid, tid + '#pola' + str(k), mk, o['zone'], o['terr'], o['kind'], 'village', f1(o['d_sea']), f1(o['d_river']), f1(o['d_mount']),
                     '0', 'garden', '%.0f' % area])
        ntown += 1

body = '\n'.join('\t'.join(r) for r in rows) + '\n'
data_crc = zlib.crc32(body.encode('utf-8')) & 0xFFFFFFFF
head = ['# Armoury: KS (start calibration) land of the districts from the map - generated by tools/ks/ks_ziemia.py, do not edit by hand.',
        '# One row = one block of work: M = game village (block 0 = the village point, 1..19 = the mix of the whole district land), S = side village (its point',
        '# and start craft), T = town fields (20 blocks of the stronghold core). Market W = Westeros, E = Essos. ha_okregu = usable land of the district (ha).',
        '# layout_crc: %08X' % layout_crc,
        '# data_crc: %08X' % data_crc,
        '#' + '\t'.join(HDR)]
with open(A.out, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(head) + '\n' + body)
print('arm_ks_land: wsie gry %d blokow (%d okregow), wioski poboczne %d, pola mieszczan %d blokow; uklad crc %08X, dane crc %08X -> %s' % (
    nmain, nmain // 20, nside, ntown, layout_crc, data_crc, A.out))

# ---------------------------------------------------------------- test zgodnosci z modelem v2 (rozklad ziemi przy tej samej liczbie blokow)
if A.check:
    D = json.load(open(A.check, encoding='utf-8'))
    bad = tot = 0
    def key(o): return (o['zone'], o['terr'], o['kind'], round(o['d_sea'], 1), round(o['d_river'], 1), round(o['d_mount'], 1))
    for d in D['dist']:
        mains = [b for b in d['blocks'] if b['main']]
        if d['type'] == 'town_fields':
            want = [key(b['o']) for b in mains]
            got = [key(o) for o in spread(TLAND.get(d['town'], []), 20)]
        else:
            want = [key(b['o']) for b in mains[1:]]
            cls = DLAND.get(d['id'], [])
            got = [key(o) for o in spread(cls, len(mains) - 1)] if cls else want
        tot += 1
        if want != got:
            bad += 1
            if bad <= 5: print('ROZNICA', d['id'], len(want), len(got), want[:3], got[:3])
    print('test zgodnosci z modelem: okregow %d, roznych %d' % (tot, bad))
