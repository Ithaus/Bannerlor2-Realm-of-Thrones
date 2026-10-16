# Symulacja przydzialu rodow kanonu (VillageHolders.CanonPool + Build krok (b) D2) na starcie kampanii ROT, bez majatkow / tytulow BK
# (gorna granica: kazda wioska bez (a)/(a2) dostaje rod kanonu). Kopia logiki C# - do raportu liczb, nie do gry.
# Po recenzji D2 (16.10): (0) siedziba w okregu z kazdej krainy, rod wyspy tylko na swojej wyspie (okreg wyspy - najpierw i, gdy sa, wylacznie rody
# wyspy), punkt rodu = siedziba z pliku -> siedziba klanu ROT (initial_home_settlement) -> osada podkrainy (anchor_settlement_id); rod z punktem dalej
# niz FAR (150) tylko, gdy lista bylaby pusta. Raport: odleglosci przydzialow od wsi gry, rody wysp, najwiecej wiosek jednego rodu.
# Jeden rod najwyzej PER wiosek okregu (bez punktu PER_ANY), reszta do rodow krolestwa (c).
# Uzycie: python -I tools/herby/symulacja_startu.py <repo> "<gra>/Modules" [NearSeats=2] [SeatRadius=60] [FarRadius=150] [PER=2] [PER_ANY=1]
import sys, os, re, collections, glob, statistics
import xml.etree.ElementTree as ET

W = sys.argv[1]
M = sys.argv[2]
NEAR = int(sys.argv[3]) if len(sys.argv) > 3 else 2
RAD = float(sys.argv[4]) if len(sys.argv) > 4 else 60.0
FAR = float(sys.argv[5]) if len(sys.argv) > 5 else 150.0
PER = int(sys.argv[6]) if len(sys.argv) > 6 else 2          # VillageHolders.MaxPerDistrict
PER_ANY = int(sys.argv[7]) if len(sys.argv) > 7 else 1      # MaxPerDistrictAnywhere (rod bez punktu)
ESSOS = {'Braavos', 'Lys', 'Volantis', 'SlaversBay'}


def rx(p):
    t = open(p, encoding='utf-8-sig').read()
    t = re.sub(r'<!--.*?-->', '', t, flags=re.S)
    return ET.fromstring(t)


# osady
root = rx(os.path.join(M, 'ROT-Map', 'ModuleData', 'settlements.xml'))
pos, owner, bound, name = {}, {}, {}, {}
for s in root.iter('Settlement'):
    sid = s.get('id')
    pos[sid] = (float(s.get('posX') or 0), float(s.get('posY') or 0))
    name[sid] = re.sub(r'\{=[^}]*\}', '', s.get('name') or '')
    if s.get('owner'):
        owner[sid] = s.get('owner').replace('Faction.', '')
    comp = s.find('Components')
    if comp is not None:
        for ch in comp:
            if ch.tag == 'Village' and ch.get('bound'):
                bound[sid] = ch.get('bound').replace('Settlement.', '')
# klany
clans = {}
for f in rx(os.path.join(M, 'ROT-Content', 'ModuleData', 'spclans.xml')).iter('Faction'):
    clans[f.get('id')] = dict(kingdom=(f.get('super_faction') or '').replace('Kingdom.', ''), tier=int(f.get('tier') or 0),
                              owner=(f.get('owner') or '').replace('Hero.', ''), banner=f.get('banner_key') or '',
                              home=(f.get('initial_home_settlement') or '').replace('Settlement.', ''),
                              merc=(f.get('is_mercenary') or '') == 'true' or (f.get('is_minor_faction') or '') == 'true')
heroes = {}
for p in glob.glob(os.path.join(M, 'ROT-Content', 'ModuleData', '*.xml')):
    try:
        r = rx(p)
    except Exception:
        continue
    for h in r.iter('Hero'):
        if h.get('id') and h.get('faction'):
            heroes[h.get('id')] = h.get('faction').replace('Faction.', '')
kings = {}
for p in glob.glob(os.path.join(M, 'ROT-Content', 'ModuleData', '*kingdom*.xml')):
    for k in rx(p).iter('Kingdom'):
        o = (k.get('owner') or '').replace('Hero.', '')
        kings[k.get('id')] = heroes.get(o)
# rody
houses, dist_area, dist_isle = [], {}, {}
sec = None
cols = {}
for line in open(os.path.join(W, 'Armoury', 'ModuleData', 'arm_houses_canon.tsv'), encoding='utf-8'):
    line = line.rstrip('\n')
    if line.startswith('#'):
        b = line[1:].split('\t')
        if b[0] in ('house', 'district', 'names'):
            sec = b[0]
            cols = {n: i for i, n in enumerate(b)}
        continue
    f = line.split('\t')
    g = lambda c: f[cols[c]] if cols.get(c) is not None and cols[c] < len(f) else ''
    if sec == 'house':
        houses.append(dict(name=g('house'), key=g('key'), area=g('area'), seats=[x for x in g("seat_settlement_id").split(';') if x], liege_base=g('liege_base'),
                           base=g('base'), clans=[x for x in g('rot_clan_id').split(';') if x], holder=g('holder') == 'yes', source=g('source'),
                           ccs=bool(g('banner_code_ccs')), code=g('banner_code'), words=g('words'), anchor=g('anchor_settlement_id'), sub=g('subregion')))
    elif sec == 'district':
        if g('area'):
            dist_area[g('district')] = g('area')
        if g('subregion'):
            dist_isle[g('district')] = g('subregion')
by_clan = {}
for h in houses:
    for c in h['clans']:
        by_clan.setdefault(c, h)
by_area = collections.defaultdict(list)
by_seat = collections.defaultdict(list)
for h in houses:
    if h['holder'] and h['area']:
        by_area[h['area']].append(h)
        for s in h['seats']:
            by_seat[s].append(h)
# wioski poboczne
dist_uids = collections.defaultdict(list)
for line in open(os.path.join(W, 'Armoury', 'ModuleData', 'arm_map_villages.tsv'), encoding='utf-8'):
    if line.startswith('#'):
        continue
    f = line.rstrip('\n').split('\t')
    dist_uids[f[1]].append((int(f[7]), f[9]))


def fnv(a, b):
    h = 2166136261
    for ch in a + '|' + b:
        h ^= ord(ch)
        h = (h * 16777619) & 0xffffffff
    return h


def live(h):
    for c in h['clans']:
        if c in clans:
            return c
    return None


def seat_pos(h):
    for s in h['seats']:
        if s in pos:
            return pos[s]
    c = live(h)
    if c and clans[c]['home'] in pos:
        return pos[clans[c]['home']]
    return None


def ref_pos(h):
    sp = seat_pos(h)
    if sp:
        return sp
    return pos.get(h['anchor']) if h['anchor'] else None


def usable(h, lord, lh, realm, ruler):
    if h is lh:
        return False
    c = live(h)
    if c:
        cc = clans[c]
        if c == lord or cc['kingdom'] != realm or not realm or kings.get(realm) == c or (not ruler and cc['tier'] > clans[lord]['tier']) or cc['merc']:
            return False
    return True


def d2(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2


stats = collections.Counter()
used_houses = collections.Counter()
area_v = collections.Counter()
area_houses = collections.defaultdict(set)
same_neighbour = 0
pairs = 0
far_seat, far_ref = [], []
watch = collections.defaultdict(list)
isle_out, isle_in_mainland = [], []
for d, lst in sorted(dist_uids.items()):
    lst.sort()
    uids = [u for _, u in lst]
    b = bound.get(d)
    lord = owner.get(b)
    if not lord:
        stats['no lord'] += len(uids)
        continue
    realm = clans.get(lord, {}).get('kingdom')
    ruler = kings.get(realm) == lord
    area = dist_area.get(d)
    isle = dist_isle.get(d, '')
    lh = by_clan.get(lord)
    lbase = lh['base'] if lh else None
    px, py = pos[d]
    here, island, seated, sworn, rest, far = [], [], [], [], [], []
    taken = set()
    for h in by_seat.get(d, []) + by_seat.get(b, []):
        if id(h) not in taken and usable(h, lord, lh, realm, ruler):
            here.append(h)
            taken.add(id(h))
    for h in (by_area[area] if area else []):
        if id(h) in taken or not usable(h, lord, lh, realm, ruler):
            continue
        rp = ref_pos(h)
        dd = d2(rp, (px, py)) if rp else 1e18
        if h['sub']:
            (island if h['sub'] == isle else far).append(((fnv(d, h['key']) if h['sub'] == isle else dd), h['key'], h))
            continue
        if rp and dd > FAR * FAR:
            far.append((dd, h['key'], h))
            continue
        sp = seat_pos(h)
        if sp and d2(sp, (px, py)) <= RAD * RAD:
            seated.append((d2(sp, (px, py)), h['key'], h))
        (sworn if lbase and h['liege_base'] == lbase else rest).append((fnv(d, h['key']), h['key'], h))
    pool = sorted(here, key=lambda h: h['key'])
    tk = set(id(h) for h in pool)
    for _, _, h in sorted(island, key=lambda x: (x[0], x[1])):
        if id(h) not in tk:
            tk.add(id(h)); pool.append(h)
    if not isle or not island:
        n = 0
        for _, _, h in sorted(seated, key=lambda x: (x[0], x[1])):
            if n >= NEAR:
                break
            if id(h) not in tk:
                tk.add(id(h)); pool.append(h); n += 1
        for _, _, h in sorted(sworn, key=lambda x: (x[0], x[1])) + sorted(rest, key=lambda x: (x[0], x[1])):
            if id(h) not in tk:
                tk.add(id(h)); pool.append(h)
    if not pool:
        for _, _, h in sorted(far, key=lambda x: (x[0], x[1])):
            if id(h) not in tk:
                tk.add(id(h)); pool.append(h)
        if pool:
            stats['districts from far fallback'] += 1
    if not pool:
        stats['lore/realm (no canon)'] += len(uids)
        continue
    got = []
    cnt = collections.Counter()
    for j, u in enumerate(uids):
        h = pool[j % len(pool)]
        if cnt[id(h)] >= (PER if ref_pos(h) else PER_ANY):
            stats['over per-district limit -> realm houses (c)'] += 1    # jak C#: reszta wiosek do rodow krolestwa, bez rycerzy z lore
            continue
        cnt[id(h)] += 1
        got.append(h)
        stats['canon'] += 1
        c = live(h)
        stats['banner ' + ('rot live clan' if c else ('rot file' if h['source'] == 'rot' else h['source']))] += 1
        used_houses[h['key']] += 1
        area_v[area] += 1
        area_houses[area].add(h['key'])
        lord_code = clans[lord]['banner']
        hcode = clans[c]['banner'] if c else h['code']
        if hcode != lord_code:
            stats['two pennants'] += 1
        sp = None
        for s in h['seats']:
            if s in pos:
                sp = pos[s]
                break
        if sp:
            far_seat.append((d2(sp, (px, py)) ** 0.5, h['key'], name.get(d, d)))
        rp = ref_pos(h)
        if rp:
            far_ref.append((d2(rp, (px, py)) ** 0.5, h['key'], name.get(d, d)))
        if h['sub'] and h['sub'] != isle:
            isle_out.append((h['key'], name.get(d, d)))
        if isle and h['sub'] != isle:
            isle_in_mainland.append((name.get(d, d), h['key']))
        for wk in ('Magnar', 'Karstark', 'Redwyne', 'Qorgyle', 'Tarth', 'Toland', 'Fenn', 'Boggs|North', 'Quagg', 'Peat', 'Blackmyre', 'Wull', 'Harclay',
                   'Liddle', 'Knott', 'Burley', 'Norrey', 'Sunderland', 'Penrose', 'Seaworth', 'Tagaros', 'Whent', 'Clegane', 'Darry of Darry', 'Meadows',
                   'Thorne', 'Tollett', 'Melane', 'Lannister of Lannisport', 'Lanny', 'Lantell', 'Lannett'):
            if wk in h['key']:
                watch[wk].append('%s (%.0f)' % (name.get(d, d), (d2(rp, (px, py)) ** 0.5) if rp else -1))
    for a_, b_ in zip(got, got[1:]):
        pairs += 1
        if a_ is b_:
            same_neighbour += 1
print('wioski:', sum(len(v) for v in dist_uids.values()), dict(stats))
v = sorted(used_houses.values())
wes = collections.Counter({k: n for k, n in used_houses.items() if next((h['area'] for h in houses if h['key'] == k), '') not in ESSOS})
print('roznych rodow:', len(used_houses), '| najwiecej wiosek jednego rodu:', used_houses.most_common(8))
print('rozklad: mediana', statistics.median(v), 'p90', v[int(len(v) * 0.9)], 'max', v[-1], '| Westeros max:', wes.most_common(3))
print('sasiednie wioski okregu z tym samym rodem: %d z %d par' % (same_neighbour, pairs))
for lab, arr in (('siedziba z pliku', far_seat), ('punkt rodu (siedziba / klan / podkraina)', far_ref)):
    print('przydzialy z punktem "%s": %d, dalej niz 150: %d, 200: %d, 300: %d; najdalej: %s' % (
        lab, len(arr), sum(1 for x in arr if x[0] > 150), sum(1 for x in arr if x[0] > 200), sum(1 for x in arr if x[0] > 300),
        ', '.join('%s @ %s %.0f' % (k, n, dd) for dd, k, n in sorted(arr, reverse=True)[:5])))
print('rod wyspy poza swoja wyspa:', len(isle_out), isle_out[:6])
print('okreg wyspy z rodem spoza wyspy:', len(isle_in_mainland), isle_in_mainland[:8])
for a in sorted(area_v):
    print('  %-12s wiosek %4d  rodow %3d (w pliku %d)' % (a, area_v[a], len(area_houses[a]), len(by_area[a])))
print('wladcy krolestw (z pliku):', len([k for k in kings.values() if k]))
for wk in sorted(watch):
    print('  %-24s %3d: %s' % (wk, len(watch[wk]), ', '.join(watch[wk][:8])))
