# Symulacja przydzialu rodow kanonu (VillageHolders.CanonPool + Build krok (b) D2) na starcie kampanii ROT, bez majatkow / tytulow BK
# (gorna granica: kazda wioska bez (a)/(a2) dostaje rod kanonu). Kopia logiki C# - do raportu liczb, nie do gry.
# Uzycie: python -I tools/herby/symulacja_startu.py <repo> "<gra>/Modules" [NearSeats=2] [SeatRadius=60]
import sys, os, re, collections, glob
import xml.etree.ElementTree as ET

W = sys.argv[1]
M = sys.argv[2]
NEAR = int(sys.argv[3]) if len(sys.argv) > 3 else 2
RAD = float(sys.argv[4]) if len(sys.argv) > 4 else 1e9


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
                              merc=(f.get('is_mercenary') or '') == 'true' or (f.get('is_minor_faction') or '') == 'true')
heroes = {}
for p in glob.glob(os.path.join(M, 'ROT-Content', 'ModuleData', '*.xml')):
    try:
        r = rx(p)
    except Exception:
        continue
    for n in r.iter('NPCCharacter'):
        pass
    for h in r.iter('Hero'):
        if h.get('id') and h.get('faction'):
            heroes[h.get('id')] = h.get('faction').replace('Faction.', '')
kings = {}
for p in glob.glob(os.path.join(M, 'ROT-Content', 'ModuleData', '*kingdom*.xml')):
    for k in rx(p).iter('Kingdom'):
        o = (k.get('owner') or '').replace('Hero.', '')
        kings[k.get('id')] = heroes.get(o)
# rody
houses, dist_area, names = [], {}, {}
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
                           ccs=bool(g('banner_code_ccs')), code=g('banner_code'), words=g('words')))
    elif sec == 'district':
        if g('area'):
            dist_area[g('district')] = g('area')
by_clan = {}
for h in houses:
    for c in h['clans']:
        by_clan.setdefault(c, h)
by_area = collections.defaultdict(list)
for h in houses:
    if h['holder'] and h['area']:
        by_area[h['area']].append(h)
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


stats = collections.Counter()
used_houses = collections.Counter()
area_v = collections.Counter()
area_houses = collections.defaultdict(set)
same_neighbour = 0
pairs = 0
samples = []
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
    pool = []
    if area:
        lh = by_clan.get(lord)
        lbase = lh['base'] if lh else None
        here, seated, sworn, rest = [], [], [], []
        px, py = pos[d]
        for h in by_area[area]:
            if h is lh:
                continue
            c = live(h)
            if c:
                cc = clans[c]
                if c == lord or cc['kingdom'] != realm or not realm or kings.get(realm) == c or (not ruler and cc['tier'] > clans[lord]['tier']) or cc['merc']:
                    continue
            if d in h['seats'] or b in h['seats']:
                here.append(h)
                continue
            sp = next((pos[s] for s in h['seats'] if s in pos), None)
            if sp and (sp[0] - px) ** 2 + (sp[1] - py) ** 2 <= RAD * RAD:
                seated.append(((sp[0] - px) ** 2 + (sp[1] - py) ** 2, h['key'], h))
            k = fnv(d, h['key'])
            (sworn if lbase and h['liege_base'] == lbase else rest).append((k, h['key'], h))
        here.sort(key=lambda h: h['key'])
        pool = list(here)
        taken = set(id(h) for h in here)
        n = 0
        for _, _, h in sorted(seated, key=lambda x: (x[0], x[1])):
            if n >= NEAR:
                break
            if id(h) not in taken:
                taken.add(id(h)); pool.append(h); n += 1
        for _, _, h in sorted(sworn, key=lambda x: (x[0], x[1])) + sorted(rest, key=lambda x: (x[0], x[1])):
            if id(h) not in taken:
                taken.add(id(h)); pool.append(h)
    if not pool:
        stats['lore/realm (no canon)'] += len(uids)
        continue
    got = []
    for j, u in enumerate(uids):
        h = pool[j % len(pool)]
        got.append(h)
        stats['canon'] += 1
        c = live(h)
        stats['banner ' + ('rot live clan' if c else ('rot file' if h['source'] == 'rot' else 'generated'))] += 1
        used_houses[h['key']] += 1
        area_v[area] += 1
        area_houses[area].add(h['key'])
        lord_code = clans[lord]['banner']
        hcode = clans[c]['banner'] if c else h['code']
        if hcode != lord_code:
            stats['two pennants'] += 1
        if h['words']:
            stats['with words'] += 1
        if len(samples) < 12 and j == 0 and len(samples) * 40 < len(dist_area) and hash(d) % 7 == 0:
            samples.append('%s (%s, pan %s): %s' % (name.get(d, d), area, lord, h['name']))
    for a_, b_ in zip(got, got[1:]):
        pairs += 1
        if a_ is b_:
            same_neighbour += 1
print('wioski:', sum(len(v) for v in dist_uids.values()), dict(stats))
print('roznych rodow:', len(used_houses), 'najwiecej wiosek jednego rodu:', used_houses.most_common(8)); import statistics; v=sorted(used_houses.values()); print('rozklad: mediana', statistics.median(v), 'p90', v[int(len(v)*0.9)], 'max', v[-1])
print('sasiednie wioski okregu z tym samym rodem: %d z %d par' % (same_neighbour, pairs))
for a in sorted(area_v):
    print('  %-12s wiosek %4d  rodow %3d (w pliku %d)' % (a, area_v[a], len(area_houses[a]), len(by_area[a])))
print('wladcy krolestw (z pliku):', len([k for k in kings.values() if k]))
for s in samples:
    print(' ', s)
