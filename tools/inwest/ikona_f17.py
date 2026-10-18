"""Uzycie: python -I tools/inwest/ikona_f17.py <Armoury/ModuleData> <Logs/<test>> <Armoury-*.log> <doba ks-plan> <doba indeksow cen KS albo 0> [filtr papirusu 0/1] [n = 1: 0/1]

f5-zajecia (noc 17/18.10): szacunek offline ikony okregu wedlug F17 projektu (A9) - czesc okregu = osady wioski (v6) z zajeciem ziemi (start), wies gry = typ;
wartosc = udzial x g(zajecie)/g(typ) x n(zajecie) x suma(stawka x Value x indeks ceny). Prog x1.10. Indeks 0 = ceny bazowe (gra przed KS), inaczej mediana
swiata z ks-miasta-towary.csv tej doby. Recenzja f5: ostatni argument 1 - n = 1 (ikona pod KS bez n neutralnego startu - KS go nie stosuje; tak liczy
VillageCrafts.F17Value po recenzji). Przyklad (fin88): ... Logs/2026-10-18_03-13-38 D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-18_03-13-38.log 109075 109151 1"""
import csv, sys, re, collections, statistics
md, logs, logp, dplan, dpr = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5]
FILT = int(sys.argv[6]) if len(sys.argv) > 6 else 0
N1 = int(sys.argv[7]) if len(sys.argv) > 7 else 0          # recenzja f5: 1 - n = 1 (F17Value pod KS po recenzji)
RATES = {
 'wheat_farm': {'grain': 50, 'cow': 0.2, 'sheep': 0.4, 'hog': 0.8, 'Papyrus': 0.5},
 'lumberjack': {'hardwood': 18, 'mead': 2}, 'clay_mine': {'clay': 10, 'limestone': 8}, 'salt_mine': {'salt': 15},
 'iron_mine': {'iron': 10}, 'fisherman': {'fish': 28}, 'vineyard': {'grape': 11}, 'flax_plant': {'flax': 18},
 'date_farm': {'date_fruit': 8, 'spice': 0.5}, 'olive_trees': {'olives': 12}, 'silk_plant': {'cotton': 8},
 'silver_mine': {'silver': 3, 'marble': 0.8, 'gold_ore': 0.2}, 'trapper': {'fur': 1.4},
 'cattle_farm': {'cow': 2, 'butter': 4, 'cheese': 4, 'Egg': 1.5}, 'sheep_farm': {'sheep': 4, 'wool': 10, 'butter': 2, 'cheese': 2},
 'swine_farm': {'hog': 8, 'butter': 2, 'cheese': 2},
 'whaler': {'WhaleMeat': 6, 'whale_oil': 2}, 'walrus_hunter': {'walrus_tusk': 0.5, 'fish': 6},
}
# wartosci towarow z linii Towary (w grze)
val = {}
pat = re.compile(r"Towary: dzien \d+ - (\S+) \((\d+) d\)")
with open(logp, encoding='utf-8', errors='replace') as f:
    for i, line in enumerate(f):
        m = pat.search(line)
        if m: val[m.group(1)] = int(m.group(2))
        if i > 400000: break
# n zajec z linii startu neutralnego
nmap = {}
with open(logp, encoding='utf-8', errors='replace') as f:
    for line in f:
        if 'start neutralny (A6)' in line:
            for m in re.finditer(r"(\w+) ([\d.]+) \(([\d.]+): ", line): nmap[m.group(1)] = float(m.group(2))
            break
if N1: nmap = {}
g = {}
with open(logs + '/plon-wsi.csv', encoding='utf-8') as f:
    r = csv.reader(f, delimiter=';'); next(r)
    for x in r:
        if x[8]: g[x[3]] = float(x[8])
land = {}
for line in open(md + '/arm_ks_land.tsv', encoding='utf-8'):
    if line.startswith('#'): continue
    c = line.rstrip('\n').split('\t')
    if c[0] == 'S': land[c[2]] = c[12]
types, people, cult = {}, {}, {}
HOT = {'aserai','ghiscari','qartheen','volantine','lyseni','myrish','tyroshi','valyrian','summer'}
with open(logs + '/ks-plan.csv', encoding='utf-8', errors='replace') as f:
    for rr in csv.DictReader(f, delimiter=';'):
        if rr['rodzaj'] != 'wies' or rr['dzien'] != dplan: continue
        types[rr['okreg_id']] = rr['typ']; people[rr['okreg_id']] = float(rr['ludzie']); cult[rr['okreg_id']] = rr['kraina']
hdr = None; side = collections.defaultdict(list)
for line in open(md + '/arm_map_villages.tsv', encoding='utf-8'):
    if line.startswith('#id'): hdr = line[1:].rstrip('\n').split('\t'); continue
    if line.startswith('#') or not line.strip(): continue
    c = line.rstrip('\n').split('\t'); d = dict(zip(hdr, c))
    st = land.get(d['uid'])
    if d.get('ks_start') and d['ks_start'] != '-': st = d['ks_start']
    side[d['village_id']].append((int(d['settlements']), st))
# indeksy cen: mediana swiata na dobe dpr (0 = bez indeksu)
ix = collections.defaultdict(list)
if dpr != '0':
    with open(logs + '/ks-miasta-towary.csv', encoding='utf-8') as f:
        r = csv.reader(f, delimiter=';'); next(r)
        for x in r:
            if x[0] == dpr: ix[x[3]].append(float(x[4]))
ixm = {k: statistics.median(v) for k, v in ix.items()}
used = set(types.values())
def V(c, hot=True):
    return sum(rt * max(1, val.get(it, 1)) * ixm.get(it, 1.0) for it, rt in RATES.get(c, {}).items() if hot or it != 'Papyrus')
diff = 0; n = 0; pairs = collections.Counter()
for v, t in types.items():
    P = people.get(v, 0)
    if P <= 0 or t.endswith('horse_ranch') or t not in RATES: continue
    n += 1
    tot = max(1.0, round(P / 250.0))
    sv = side.get(v, []); sf = sum(s for s, _ in sv); mf = max([s for s, _ in sv] + [0])
    if sf + mf > tot and sv:
        k = tot / (sf + mf); sv = [(s * k, st) for s, st in sv]; sf = sum(s for s, _ in sv)
    vals = collections.Counter()
    gD = g.get(t, 1.0)
    vals[t] += max(0.0, tot - sf) / tot * nmap.get(t, 1.0) * V(t, cult.get(v) in HOT or FILT == 0)
    for s, st in sv:
        c = st if (st in used and st in RATES) else t
        if c == 'cattle_farm:maslo': c = 'cattle_farm'
        vals[c] += s / tot * (g.get(c, gD) / gD if c != t else 1.0) * nmap.get(c, 1.0) * V(c, cult.get(v) in HOT or FILT == 0)
    best, bv = vals.most_common(1)[0]
    if best != t and vals[t] > 0 and bv < 1.1 * vals[t]: best = t
    if best != t: diff += 1; pairs[(t, best)] += 1
print('indeksy', dpr, 'okregow', n, 'inna niz typ', diff, pairs.most_common(10))
