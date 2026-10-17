# KSIM - build step: reads the round-3 autotest CSVs + log + ks_dump.json + geo data and writes world.json for sim.py.
# Read-only on all inputs. Python 3 stdlib. Run: python -I build_world.py [csvdir] [log] [plan_day]
import csv, json, math, re, sys, os, heapq, collections

HERE = os.path.dirname(os.path.abspath(__file__))
SCR = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad'
GEO = SCR + '/roz/geo-dane'
DUMP = SCR + '/ks/ks_dump.json'
CSVD = sys.argv[1] if len(sys.argv) > 1 else r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30'
LOG = sys.argv[2] if len(sys.argv) > 2 else r'D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_01-15-30.log'
PLAN_DAY = sys.argv[3] if len(sys.argv) > 3 else '109179'
OUT = os.path.join(HERE, 'world.json')

# ---------------------------------------------------------------- goods of the simulator
# raw goods come from villages / town fields; processed goods are made in towns (ZC5) from raw goods on the same shelf.
G = ['grain', 'fish', 'meat', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey',
     'bread', 'pie', 'beer', 'wine', 'oil', 'mead']
GI = {g: i for i, g in enumerate(G)}
NG = len(G)
# price category of each good (fruit is one category in the game: apple, carrot, orange share one index)
CAT = {g: g for g in G}; CAT['apple'] = CAT['carrot'] = CAT['orange'] = 'fruit'
# meat equivalents of livestock delivered by villages (ledger: cow>6meat, hog>2meat, sheep>meat)
LIVE = {'cow': 6.0, 'hog': 2.0, 'sheep': 1.0, 'WhaleMeat': 1.0}
# ZC5 pairs: output <- input, input pieces per output piece (all pieces 10 kg in RoT: bread/pie 1 kg grain per kg, beer 0.4 kg/l, wine 1.3, oil 5, mead 0.35)
PAIRS = [('bread', 'grain', 1.0, 60.0), ('pie', 'grain', 1.0, 40.0), ('beer', 'grain', 0.4, 150.0), ('wine', 'grape', 1.3, 80.0),
         ('oil', 'olives', 5.0, 15.0), ('mead', 'honey', 0.35, 50.0)]
# kcal per piece (10 kg pieces; KsBasket.Kcal per kg x 10)
KCAL_KG = {'grain': 3300, 'bread': 3300, 'pie': 3300, 'meat': 2200, 'fish': 1100, 'cheese': 3600, 'butter': 7200, 'Egg': 1450, 'apple': 500,
           'carrot': 400, 'orange': 450, 'grape': 700, 'olives': 1150, 'date_fruit': 2800, 'oil': 8800, 'honey': 3000, 'beer': 400, 'wine': 750,
           'mead': 1000, 'salt': 0}
# Plan ladder rungs (KsBasket.Rungs) restricted to simulated goods
RUNGS = [['grain', 'bread', 'pie', 'salt'], ['beer', 'wine', 'mead'], ['meat', 'fish', 'cheese', 'butter', 'Egg'],
         ['apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'oil', 'honey']]


def fnum(x, d=0.0):
    try:
        return float(x)
    except Exception:
        return d


def num_list(s):
    """'grain 2913, meat 623' -> dict"""
    out = {}
    for part in s.split(','):
        part = part.strip()
        m = re.match(r'^([A-Za-z_]+)\s+(-?[0-9.]+)', part)
        if m: out[m.group(1)] = float(m.group(2))
    return out


# ---------------------------------------------------------------- log constants (last occurrence)
def log_lines():
    want = {}
    pats = {
        'zc4': 'Koszyk stanow (ZC4): dzien ', 'unpaid': 'KS: koszyk niezaplacony (plan doba ', 'money': 'KS: pieniadze planu (krok 8) doba ',
        'polki': 'KS: polki startu doba ', 'ceny': 'KS: ceny miast dzien ', 'rynki': 'KS: rynki dzien ', 'natarg': 'KS: na targ (plan doba ',
    }
    towary = {}
    rynki = {}
    with open(LOG, encoding='utf-8', errors='replace') as f:
        for line in f:
            for k, p in pats.items():
                if p in line:
                    if k == 'rynki' and 'dzien 109123' in line: want['rynki0'] = line
                    want[k] = line
            if 'KS: rynki dzien ' in line:
                m = re.search(r'KS: rynki dzien (\d+)', line)
                if m: rynki[int(m.group(1))] = line
            if 'Towary: dzien ' in line:
                m = re.search(r'Towary: dzien (\d+) - (\S+) \(', line)
                if m:
                    towary.setdefault(int(m.group(1)), {})[m.group(2)] = line
    return want, towary, rynki


def parse_towary(line):
    """consumption sinks and transport of one goods-ledger line (pieces/d)"""
    r = {}
    m = re.search(r'\| zuzycie (\d+) \[(.*?)\] \|', line)
    if m:
        r['cons'] = float(m.group(1))
        body = m.group(2)
        for key, pat in [('town', r'mieszczanie (\d+)'), ('castle', r'zamki (\d+)'), ('vil', r'wsie \(zakupy w miescie, E5/B1\) (\d+)'),
                         ('court', r'dwory \(E3-P1\) (\d+)'), ('party', r'zywnosc partii (\d+)'), ('craft', r'rzemioslo miasta \(148\) (\d+)'),
                         ('bkcraft', r'rzemieslnicy BK (\d+)'), ('wsh', r'linie warsztatow (\d+)')]:
            mm = re.search(pat, body)
            r[key] = float(mm.group(1)) if mm else 0.0
    m = re.search(r'produkcja (\d+) \[', line); r['prod'] = float(m.group(1)) if m else 0.0
    m = re.search(r'lordowie kupili (\d+)', line); r['lords_buy'] = float(m.group(1)) if m else 0.0
    m = re.search(r'lordowie porzucili na polki miast (\d+)', line); r['lords_drop'] = float(m.group(1)) if m else 0.0
    m = re.search(r'karawany kupily (\d+), sprzedaly (\d+)', line)
    r['car_buy'], r['car_sell'] = (float(m.group(1)), float(m.group(2))) if m else (0.0, 0.0)
    m = re.search(r'posiadacze \[(.*?)\]', line)
    if m:
        for key, pat in [('h_town', r'miasta (\d+)'), ('h_castle', r'zamki (\d+)'), ('h_car', r'karawany (\d+)')]:
            mm = re.search(pat, m.group(1)); r[key] = float(mm.group(1)) if mm else 0.0
    return r


def main():
    L, TOW, RYN = log_lines()
    # ---- ZC4 line: Q (towns, paid) + QV (villages, Plan-paid), Der, One
    zc = L['zc4']
    m = re.search(r'potrzeba szt\./d przy lambdzie \(miasta \+ wsie; .*?\): (.*?) \| wsie:', zc)
    Qw, QVw = {}, {}
    for part in m.group(1).split(', '):
        mm = re.match(r'^(\S+) ([0-9.]+)\+([0-9.]+)', part.strip())
        if mm: Qw[mm.group(1)] = float(mm.group(2)); QVw[mm.group(1)] = float(mm.group(3))
    m = re.search(r'1:1 \(wojsko, zamki, dwory\) (.*?) \|', zc); ONEw = num_list(m.group(1))
    # ---- unpaid basket (Plan) per market: villages and towns
    up = L['unpaid']
    unpaid = {}
    for mk in ('Westeros', 'Essos'):
        m = re.search(mk + r' wsie: (.*?); miasta: (.*?)(?: \| |\.$|$)', up)
        unpaid[mk] = {'vil': num_list(m.group(1)), 'town': num_list(m.group(2).rstrip('.'))}
    # ---- goods ledger, average of the last 14 logged days (sinks)
    tdays = [d for d in sorted(TOW) if int(os.environ.get('KSIM_SINK0', '109130')) <= d <= int(os.environ.get('KSIM_SINK1', '109195'))]
    sinks = {}
    for g in set(G) | set(LIVE) | {'WhaleMeat'}:
        acc = collections.defaultdict(float); n = 0
        for d in tdays:
            if g in TOW[d]:
                for k, v in parse_towary(TOW[d][g]).items(): acc[k] += v
                n += 1
        if n: sinks[g] = {k: v / n for k, v in acc.items()}
    # ---- dump (needs, item values)
    dump = json.load(open(DUMP, encoding='utf-8'))
    sigma = float(dump['sigma'])
    val = {g: float(dump['gd'][g]['val']) for g in G if g in dump['gd']}
    kgp = {g: float(dump['gd'][g]['kg']) for g in G if g in dump['gd']}
    for g in G:
        val.setdefault(g, 5.0); kgp.setdefault(g, 10.0)
    dd = {x['id']: x for x in dump['dist']}
    dtowns = {x['id']: x for x in dump['towns']}

    # ---- ks-miasta (towns, observed)
    km = list(csv.DictReader(open(CSVD + '/ks-miasta.csv', encoding='utf-8'), delimiter=';'))
    kdays = sorted(set(int(r['dzien']) for r in km))
    towns = {}
    for r in km:
        if int(r['dzien']) == kdays[0]:
            towns[r['miasto_id']] = {'id': r['miasto_id'], 'name': r['miasto'], 'mk': 0 if r['rynek'] == 'Westeros' else 1, 'people': fnum(r['ludzie'])}
    T = sorted(towns)
    TI = {t: i for i, t in enumerate(T)}
    obs = collections.defaultdict(dict)
    for r in km:
        obs[int(r['dzien'])][r['miasto_id']] = {k: r[k] for k in r}

    # ---- estates (craftsmen per town) and kingdom
    craft = {}; kingdom = {}
    sd_days = set()
    for r in csv.DictReader(open(CSVD + '/stany-dostatek.csv', encoding='utf-8'), delimiter=';'):
        sd_days.add(r['dzien'])
    sd_first = max([d for d in sd_days if int(d) <= int(os.environ.get('KSIM_KDAY', '109137'))] or [min(sd_days, key=int)], key=int)
    for r in csv.DictReader(open(CSVD + '/stany-dostatek.csv', encoding='utf-8'), delimiter=';'):
        if r['dzien'] != sd_first: continue
        kingdom[r['osada_id']] = r['krolestwo']
        if r['osada_id'] in towns: craft[r['osada_id']] = fnum(r['rzemieslnicy'])

    # ---- town folk affordability (kiesa-ludu.csv 'stac_jedzenie': share of the food basket the W/R/P purses pay; mean of the lambda-1 days)
    afford = collections.defaultdict(list)
    for r in csv.DictReader(open(CSVD + '/kiesa-ludu.csv', encoding='utf-8'), delimiter=';'):
        if r['rodzaj'] != 'miasto' or int(r['dzien']) < 109137: continue
        x = fnum(r['stac_jedzenie'], None)
        if x is not None: afford[r['osada_id']].append(x)

    # ---- plan (districts): to-market pieces/d, eaten kg/d, people
    pl = collections.defaultdict(dict); dinfo = {}
    for r in csv.DictReader(open(CSVD + '/ks-plan.csv', encoding='utf-8'), delimiter=';'):
        if r['dzien'] != PLAN_DAY: continue
        o = r['okreg_id']
        pl[o][r['towar']] = (fnum(r['produkcja_kg_d']), fnum(r['zjedzone_w_okregu_kg_d']), fnum(r['na_targ_szt_d']))
        dinfo[o] = {'name': r['okreg'], 'kind': r['rodzaj'], 'mk': 0 if r['rynek'] == 'Westeros' else 1, 'land': r['kraina'], 'type': r['typ'], 'people': fnum(r['ludzie'])}

    # ---- budgets: village alpha, takings (utarg) and to-purse at the plan day
    alpha = {}; utarg = {}; topurse = {}
    for r in csv.DictReader(open(CSVD + '/ks-budzety.csv', encoding='utf-8'), delimiter=';'):
        if r['dzien'] != PLAN_DAY or r['rodzaj'] != 'wies': continue
        alpha[r['osada_id']] = fnum(r['alfa'], None) if r['alfa'] not in ('-', '') else None
        utarg[r['osada_id']] = fnum(r['utarg_28d_zl_d'], 0.0) if r['utarg_28d_zl_d'] not in ('-', '') else 0.0
        topurse[r['osada_id']] = fnum(r['do_kiesy_28d_zl_d'], 0.0) if r['do_kiesy_28d_zl_d'] not in ('-', '') else 0.0

    # ---- geo
    routes = list(csv.DictReader(open(GEO + '/geo-trasy.tsv', encoding='utf-8'), delimiter='\t'))
    land = collections.defaultdict(dict); sea = collections.defaultdict(dict); vbound = {}; names = {}
    for r in routes:
        a, b = r['od'], r['do']; names[a] = r['od_nazwa']; names[b] = r['do_nazwa']
        Lg = fnum(r['dl_gry'], None)
        if r['polaczenie'] == 'lad' and Lg is not None:
            if b not in land[a] or land[a][b] > Lg: land[a][b] = Lg; land[b][a] = Lg
        elif r['polaczenie'] == 'morze':
            Ls = fnum(r['dl_morze'], None)
            if Ls is None: Ls = Lg
            if Ls is not None: sea[a][b] = Ls; sea[b][a] = Ls
        if r['rodzaj'] == 'twierdza-wies': vbound[b] = a
    rid = {r['trasa']: (r['od'], r['do']) for r in routes}
    first = {}; lastp = {}
    with open(GEO + '/geo-trasy-punkty.tsv', encoding='utf-8') as f:
        rd = csv.reader(f, delimiter='\t'); next(rd)
        for row in rd:
            t = row[0]
            if t not in rid: continue
            xy = (float(row[2]), float(row[3]))
            if t not in first: first[t] = xy
            lastp[t] = xy
    pos = {}
    for t, (a, b) in rid.items():
        if t in first and a not in pos: pos[a] = first[t]
        if t in lastp and b not in pos: pos[b] = lastp[t]

    def dijk(src, extra=None):
        dist = {src: 0.0}; pq = [(0.0, src)]
        while pq:
            d0, u = heapq.heappop(pq)
            if d0 > dist.get(u, 1e18): continue
            for nb in (land.get(u, {}), extra.get(u, {}) if extra else {}):
                for v, w in nb.items():
                    nd = d0 + w
                    if nd < dist.get(v, 1e18): dist[v] = nd; heapq.heappush(pq, (nd, v))
        return dist

    dland = {t: dijk(t) for t in T}
    dsea = {t: dijk(t, sea) for t in T}

    def euclid(a, b):
        if a not in pos or b not in pos: return None
        return math.hypot(pos[a][0] - pos[b][0], pos[a][1] - pos[b][1])

    def road(t, v):
        """land road from town t to node v: graph (through fortresses) or 1.25 x straight line when the graph links them"""
        g = dland[t].get(v)
        if g is None: return None
        e = euclid(t, v)
        return min(g, 1.25 * e) if e is not None else g

    # ---- villages
    vill = []
    goods_seen = set()
    for o, inf in dinfo.items():
        if inf['kind'] != 'wies': continue
        b = vbound.get(o)
        castle = b is not None and not b.startswith('town') and b not in towns
        if b in towns: trade = b
        else:
            # Village.TradeBound approx.: nearest town by land road of the same kingdom as the castle (KSIM_TB=near: nearest town of any kingdom)
            best = None; bestk = None
            kb = kingdom.get(b, kingdom.get(o))
            for t in T:
                d0 = road(t, o)
                if d0 is None: continue
                if best is None or d0 < best[0]: best = (d0, t)
                if kingdom.get(t) == kb and (bestk is None or d0 < bestk[0]): bestk = (d0, t)
            mode = os.environ.get('KSIM_TB', 'kingdom')
            if mode == 'kingdom' and bestk is not None and bestk[0] <= float(os.environ.get('KSIM_TBMAX', '1e9')): best = bestk
            trade = best[1] if best else None
        if trade is None:
            # island castle village without a land link: nearest town by land+sea
            best = None
            for t in T:
                d0 = dsea[t].get(o)
                if d0 is None: continue
                if best is None or d0 < best[0]: best = (d0, t)
            trade = best[1] if best else T[0]
        out = [0.0] * NG
        nonfood = 0.0     # base value/d of goods the simulator does not model (wood, ore, fur, fibre, hides, clay...) - wagon takings only
        sales_all = 0.0   # base value of everything the village sells (Plan budget base: furs, wood, ore and food alike)
        for good, (pk, ek, ns) in pl[o].items():
            gv = dump['gd'].get(good, {}).get('val', 0.0) if good in dump['gd'] else 0.0
            sales_all += ns * float(gv or 0.0)
            if good not in GI and good not in LIVE: nonfood += ns * float(gv or 0.0)
            goods_seen.add(good)
            if good in GI: out[GI[good]] += ns
            elif good in LIVE: out[GI['meat']] += ns * LIVE[good]
        # basket from needs (dump, scaled by people) minus eaten in the district (plan), kg/d -> pieces/d
        nd = dd.get(o)
        f = inf['people'] / nd['people'] if nd and nd['people'] > 0 else 1.0
        need = {k: v * f for k, v in nd['need0'].items()} if nd else {}
        eaten = {good: ek for good, (pk, ek, ns) in pl[o].items()}
        bkg = {}
        bkg['grain'] = need.get('G', 0) + 0.4 * need.get('beer', 0) * 0.5 - eaten.get('grain', 0)   # half of beer brewed at home (approx. NeedsOfKeep)
        bkg['beer'] = need.get('beer', 0) * 0.5
        for g in ('fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'date_fruit'):
            bkg[g] = need.get(g, 0) - eaten.get(g, 0)
        bkg['grape'] = need.get('grape', 0) + 1.3 * need.get('wine', 0) * 0.5 - eaten.get('grape', 0)
        bkg['wine'] = need.get('wine', 0) * 0.5
        bkg['olives'] = need.get('olives', 0) - eaten.get('olives', 0)
        bkg['oil'] = need.get('swiatlo', 0) * 0.2
        bkg['honey'] = need.get('honey', 0) + 0.35 * need.get('mead', 0) * 0.5 - eaten.get('honey', 0)
        bkg['mead'] = need.get('mead', 0) * 0.5
        meat_own = sum(eaten.get(x, 0) * (0.5 if x in ('cow', 'hog', 'sheep') else 1.0) for x in ('cow', 'hog', 'sheep', 'WhaleMeat'))
        bkg['meat'] = need.get('meat', 0) - meat_own
        bkg['salt'] = need.get('salt', 0)
        bask = [0.0] * NG
        for g, kg in bkg.items():
            if kg > 0: bask[GI[g]] = kg / (sigma * kgp[g])
        vill.append({'id': o, 'name': inf['name'], 'mk': inf['mk'], 'land': inf['land'], 'type': inf['type'], 'people': inf['people'],
                     'bound': b, 'castle': castle, 'trade': TI[trade], 'out': out, 'bask': bask,
                     'alpha': alpha.get(o), 'sales_all': sales_all, 'nonfood': nonfood, 'utarg': utarg.get(o, 0.0), 'topurse': topurse.get(o, 0.0), 'kingdom': kingdom.get(b, kingdom.get(trade, '-'))})

    # scale village baskets per good so that world totals = Plan-paid (ZC4 QV) + unpaid (both markets): the full basket the villages want
    full_w = {}
    for g in G:
        qv = QVw.get(g, 0.0) + (QVw.get('WhaleMeat', 0.0) if g == 'meat' else 0.0)
        u = unpaid['Westeros']['vil'].get(g, 0.0) + unpaid['Essos']['vil'].get(g, 0.0)
        full_w[g] = qv + u
    for g in G:
        s = sum(v['bask'][GI[g]] for v in vill)
        k = full_w[g] / s if s > 0 else 0.0
        if s <= 0 and full_w[g] > 0:   # no need data: spread by people
            P = sum(v['people'] for v in vill)
            for v in vill: v['bask'][GI[g]] = full_w[g] * v['people'] / P
        else:
            for v in vill: v['bask'][GI[g]] *= k

    # per trade town: village baskets scaled to the wants the game counted there (ks-miasta wsie_chciane, lambda 1 weeks) - the basket is an observed input
    # (KSIM_BASKFIT=0 keeps the needs-minus-own estimate); factor clamped to 0.33..3
    basket_fit = {}
    if os.environ.get('KSIM_BASKFIT', '1') == '1':
        lam1 = [d for d in kdays if fnum(obs[d][T[0]]['lambda']) >= 0.999]
        for t in T:
            ow = sum(fnum(obs[d][t]['wsie_chciane_szt_7d']) for d in lam1) / (7.0 * len(lam1)) if lam1 else 0.0
            vs = [v for v in vill if v['trade'] == TI[t]]
            sw = sum(sum(v['bask']) for v in vs)
            if sw > 0 and ow > 0:
                f = max(0.33, min(3.0, ow / sw)); basket_fit[t] = f
                for v in vs: v['bask'] = [x * f for x in v['bask']]

    # Plan-paid share per village: KsBasket.Ladder at start prices, budget = alpha x base value of sales
    start_idx = {}
    m = re.search(r'KS: polki startu doba \d+.*', L.get('polki', ''))
    for mk in (0, 1):
        for g in G: start_idx[(mk, g)] = 1.0
        start_idx[(mk, 'grain')] = 1.5
        start_idx[(mk, 'olives')] = 0.7
    start_idx[(0, 'cheese')] = 1.08; start_idx[(1, 'cheese')] = 1.41; start_idx[(0, 'butter')] = 1.43; start_idx[(1, 'butter')] = 1.50
    for v in vill:
        base = v['sales_all']
        a = v['alpha'] if v['alpha'] is not None else 0.36
        budget = a * base
        paid = [1.0] * NG; left = budget
        for rung in RUNGS:
            cost = sum(v['bask'][GI[g]] * val[g] * start_idx[(v['mk'], g)] for g in rung)
            if cost <= left: left -= cost; continue
            fr = left / cost if cost > 0 else 1.0
            for g in rung: paid[GI[g]] = fr
            for r2 in RUNGS[RUNGS.index(rung) + 1:]:
                for g in r2: paid[GI[g]] = 0.0
            left = 0
            break
        v['paid'] = paid
        v['base_sales'] = base
    # rescale paid per good to the ZC4 world QV (the in-game Plan uses measured money; the shape per village stays from the ladder)
    for g in G:
        i = GI[g]
        tgt = QVw.get(g, 0.0) + (QVw.get('WhaleMeat', 0.0) if g == 'meat' else 0.0)
        for _ in range(30):   # rescale the unclamped villages until the world total matches (clamped at 1 = whole basket paid)
            s = sum(v['bask'][i] * v['paid'][i] for v in vill)
            if s <= 0 or abs(s - tgt) < 1e-6 * max(1.0, tgt): break
            free = sum(v['bask'][i] * v['paid'][i] for v in vill if v['paid'][i] < 1.0)
            fixed = s - free
            if free <= 0:
                # nobody has a partial share: give the rest by basket to villages below 1 (all at 0 -> spread)
                rest = [v for v in vill if v['paid'][i] < 1.0 and v['bask'][i] > 0]
                bs = sum(v['bask'][i] for v in rest)
                if bs <= 0 or tgt <= fixed: break
                for v in rest: v['paid'][i] = min(1.0, (tgt - fixed) / bs)
                continue
            k = max(0.0, (tgt - fixed) / free)
            for v in vill:
                if v['paid'][i] < 1.0: v['paid'][i] = min(1.0, v['paid'][i] * k)

    # ---- town fields (to-market pieces/d straight to the town shelf)
    tf = [[0.0] * NG for _ in T]
    for o, inf in dinfo.items():
        if inf['kind'] == 'wies': continue
        t = o.split('#')[0]
        if t not in TI: continue
        for good, (pk, ek, ns) in pl[o].items():
            if good in GI: tf[TI[t]][GI[good]] += ns
            elif good in LIVE: tf[TI[t]][GI['meat']] += ns * LIVE[good]

    # ---- town basket Q (paid part, pieces/d): needs per capita of the town (dump; market average when missing), scaled to ZC4 Q per good
    def town_kg(n):
        q = {}
        Gn = n.get('G', 0)
        q['grain'] = 0.45 * Gn; q['bread'] = 0.45 * Gn; q['pie'] = 0.10 * Gn
        for g in ('meat', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey', 'beer', 'wine', 'mead'):
            q[g] = n.get(g, 0)
        q['oil'] = n.get('swiatlo', 0) * 0.5
        return q
    pc = {0: collections.Counter(), 1: collections.Counter()}; pp = {0: 0.0, 1: 0.0}
    for t in T:
        dt = dtowns.get(t)
        if dt and dt['people'] > 0:
            mk = towns[t]['mk']; pp[mk] += dt['people']
            for g, x in town_kg(dt['need0']).items(): pc[mk][g] += x
    Q = [[0.0] * NG for _ in T]
    for t in T:
        dt = dtowns.get(t); P = towns[t]['people']; mk = towns[t]['mk']
        if dt and dt['people'] > 0:
            kg = {g: x * P / dt['people'] for g, x in town_kg(dt['need0']).items()}
        else:
            kg = {g: pc[mk][g] / pp[mk] * P for g in pc[mk]}
        for g, x in kg.items():
            if g in GI: Q[TI[t]][GI[g]] = x / (sigma * kgp[g])
    # paid share of town baskets per market (unpaid line) then scale to ZC4 Q world
    for g in G:
        for mk in (0, 1):
            mkn = 'Westeros' if mk == 0 else 'Essos'
            u = unpaid[mkn]['town'].get(g, 0.0)
            s = sum(Q[TI[t]][GI[g]] for t in T if towns[t]['mk'] == mk)
            if s > 0 and u > 0:
                f = max(0.0, 1 - u / s)
                for t in T:
                    if towns[t]['mk'] == mk: Q[TI[t]][GI[g]] *= f
        tgt = Qw.get(g, 0.0) + (Qw.get('WhaleMeat', 0.0) if g == 'meat' else 0.0)
        s = sum(Q[i][GI[g]] for i in range(len(T)))
        k = tgt / s if s > 0 else 0.0
        for i in range(len(T)): Q[i][GI[g]] *= k

    # ---- oil: the needs table has no oil line (lamp light only), so the market split of oil demand (towns and villages) follows the goods ledger
    # (consumption by market over the lambda-1 window); world totals unchanged
    io = GI['oil']
    lc = [0.0, 0.0]
    for d in range(int(kdays[0]) + 14, int(kdays[-1]) + 1):
        e = RYN.get(d)
        if not e: continue
        for mkn, mi in (('Westeros', 0), ('Essos', 1)):
            pass
    for d, line in RYN.items():
        if d < 109137: continue
        for mi, mkn in ((0, 'Westeros'), (1, 'Essos')):
            m = re.search(mkn + r': .*?\boil (\d+)/(\d+)/', line)
            if m: lc[mi] += float(m.group(2))
    if sum(lc) > 0:
        totq = sum(Q[i][io] for i in range(len(T))); totv = sum(v['bask'][io] for v in vill)
        for mi in (0, 1):
            sq = sum(Q[TI[t]][io] for t in T if towns[t]['mk'] == mi); sv = sum(v['bask'][io] for v in vill if v['mk'] == mi)
            fq = (totq * lc[mi] / sum(lc)) / sq if sq > 0 else 1.0
            fv = (totv * lc[mi] / sum(lc)) / sv if sv > 0 else 1.0
            if sq <= 0:   # no oil need in this market's towns: spread its share by town people
                pm = sum(towns[t]['people'] for t in T if towns[t]['mk'] == mi)
                for t in T:
                    if towns[t]['mk'] == mi and pm > 0: Q[TI[t]][io] = totq * lc[mi] / sum(lc) * towns[t]['people'] / pm
                fq = 1.0
            for t in T:
                if towns[t]['mk'] == mi: Q[TI[t]][io] *= fq
            for v in vill:
                if v['mk'] == mi: v['bask'][io] *= fv

    # ---- One (1:1 army, castles, courts) by market-people share (town + villages trading there)
    mkp = [towns[t]['people'] for t in T]
    for v in vill: mkp[v['trade']] += v['people']
    W = sum(mkp)
    one_share = [x / W for x in mkp]

    # ---- per-town fit of the grain D-term (ks-miasta zboze_popyt_zaplacony = lambda (Q + QV + Der) + One, lambda-1 weeks): the village Plan-paid share of the
    # town's villages is scaled (all goods, clamped at 1); what cannot be reached that way scales the town basket Q (KSIM_DFIT=0: off)
    dfit = {}
    if os.environ.get('KSIM_DFIT', '1') == '1':
        lam1 = [d for d in kdays if fnum(obs[d][T[0]]['lambda']) >= 0.999]
        ig, ib, ip, ibe = GI['grain'], GI['bread'], GI['pie'], GI['beer']
        def qvpart(vs): return sum(v['bask'][ig] * v['paid'][ig] + 0.4 * v['bask'][ibe] * v['paid'][ibe] for v in vs)
        for t in T:
            i = TI[t]
            tgt = sum(fnum(obs[d][t]['zboze_popyt_zaplacony_szt_d']) for d in lam1) / len(lam1)
            vs = [v for v in vill if v['trade'] == i]
            qp = Q[i][ig] + Q[i][ib] + Q[i][ip] + 0.4 * Q[i][ibe]
            one_g = ONEw.get('grain', 0.0) * one_share[i]
            need_qv = max(0.0, tgt - qp - one_g)
            for _ in range(6):
                cur = qvpart(vs)
                if cur <= 1e-9:
                    bs = sum(v['bask'][ig] for v in vs)
                    if bs <= 0: break
                    for v in vs: v['paid'][ig] = min(1.0, need_qv / bs)
                    continue
                h = need_qv / cur
                if abs(h - 1) < 1e-3: break
                for v in vs: v['paid'] = [min(1.0, x * h) for x in v['paid']]
            rest = tgt - one_g - qvpart(vs)
            if qp > 0:
                f = max(0.3, min(3.0, rest / qp)) if rest > 0 else 0.3
                Q[i] = [x * f for x in Q[i]]
                dfit[t] = f

    # ---- castles: demand of the castle population (ledger 'zamki' sink) shared by the people of castle villages
    castles = {}
    for v in vill:
        if v['castle']:
            c = castles.setdefault(v['bound'], {'id': v['bound'], 'name': names.get(v['bound'], v['bound']), 'people': 0.0})
            c['people'] += v['people']
    CP = sum(c['people'] for c in castles.values())
    clist = sorted(castles.values(), key=lambda c: c['id'])
    CI = {c['id']: i for i, c in enumerate(clist)}
    for c in clist:
        c['dem'] = [0.0] * NG
        for g in G:
            s = sinks.get(g, {}).get('castle', 0.0)
            c['dem'][GI[g]] = s * c['people'] / CP if CP > 0 else 0.0
    for v in vill:
        v['castle_i'] = CI[v['bound']] if v['castle'] else -1

    # ---- candidate towns of each village wagon (land road <= 400; the sim applies the range) and F2 near towns
    for v in vill:
        cand = []
        for t in T:
            d0 = road(t, v['id'])
            if d0 is None: continue
            if d0 <= 400: cand.append((TI[t], round(d0, 1)))
        if v['bound'] in TI and all(c[0] != TI[v['bound']] for c in cand):
            d0 = dsea[v['bound']].get(v['id']) or 0.0
            cand.append((TI[v['bound']], round(d0, 1)))
        cand.sort(key=lambda x: x[1])
        v['cand'] = cand
        near = []
        for t in T:
            if TI[t] == v['trade']: continue
            e = euclid(t, v['id']); r = road(t, v['id'])
            if e is None or r is None or e > 250 or r > 375: continue
            near.append((r, TI[t]))
        near.sort()
        v['near'] = [x[1] for x in near[:2]]

    # ---- town-town distances (land / land+sea), for trade modules
    dl = [[-1.0] * len(T) for _ in T]; ds = [[-1.0] * len(T) for _ in T]
    for a in T:
        for b in T:
            x = dland[a].get(b); y = dsea[a].get(b)
            dl[TI[a]][TI[b]] = round(x, 1) if x is not None else -1.0
            ds[TI[a]][TI[b]] = round(y, 1) if y is not None else -1.0

    # ---- village purses at the first ks-miasta day: per town total split by takings (utarg)
    d0 = kdays[0]
    for i, t in enumerate(T):
        vs = [v for v in vill if v['trade'] == i]
        tot = fnum(obs[d0][t]['kiesy_wsi_zl'])
        u = sum(max(1.0, v['utarg']) for v in vs)
        for v in vs: v['purse0'] = tot * max(1.0, v['utarg']) / u if u > 0 else 0.0

    tl = []
    for t in T:
        tl.append({'id': t, 'name': towns[t]['name'], 'mk': towns[t]['mk'], 'people': towns[t]['people'], 'craft': craft.get(t, 0.0),
                   'afford': (sum(afford[t]) / len(afford[t])) if afford.get(t) else 1.0,
                   'kingdom': kingdom.get(t, '-'), 'pos': pos.get(t)})
    ryn = {}
    for d, line in RYN.items():
        e = {}
        for mkn in ('Westeros', 'Essos'):
            m = re.search(mkn + r': (.*?)(?: \| |$)', line.split(') | ', 1)[1] if ') | ' in line else line)
            if not m: continue
            gd = {}
            for part in m.group(1).split(', '):
                mm = re.match(r'^(\S+) (\d+)/(\d+)/(\d+)', part.strip())
                if mm: gd[mm.group(1)] = [float(mm.group(2)), float(mm.group(3)), float(mm.group(4))]
            e[0 if mkn == 'Westeros' else 1] = gd
        ryn[d] = e
    obs_out = {}
    keep = ['lambda', 'ludzie', 'zboze_indeks', 'zboze_polka_szt', 'zboze_popyt_zaplacony_szt_d', 'zboze_dni_polki', 'wsie_chciane_szt_7d', 'wsie_kupione_szt_7d',
            'wsie_bez_kiesy_szt_7d', 'wsie_bez_towaru_szt_7d', 'wsie_w_innym_miescie_szt_7d', 'kiesy_wsi_zl', 'indeks_jedzenia_mediana']
    for d in kdays:
        obs_out[d] = [[fnum(obs[d][t][k]) for k in keep] for t in T]
    ceny = {}
    cl = L.get('ceny', '')
    for mkn in ('Westeros', 'Essos'):
        m = re.search(r'\| ' + mkn + r': (.*?)(?: \| |$)', cl)
        if not m: continue
        e = {}
        for part in m.group(1).split(', '):
            mm = re.match(r'^(\S+) ([0-9.]+) \(s(\d+) p(\d+)\)', part.strip())
            if mm: e[mm.group(1)] = [float(mm.group(2)), float(mm.group(3)) / 100, float(mm.group(4)) / 100]
        ceny[0 if mkn == 'Westeros' else 1] = e
    world = {'obs_prices': ceny, 'obs_prices_day': re.search(r'dzien (\d+)', cl).group(1) if cl else None, 'obs_keys': keep, 'obs': obs_out, 'rynki': ryn, 'goods': G, 'cat': [CAT[g] for g in G], 'val': [val[g] for g in G], 'kcal': [KCAL_KG[g] * kgp[g] for g in G], 'sigma': sigma,
             'pairs': PAIRS, 'towns': tl, 'Q': Q, 'one_share': one_share, 'tf': tf, 'villages': vill, 'castles': clist,
             'dland': dl, 'dsea': ds, 'zc4': {'Q': Qw, 'QV': QVw, 'One': ONEw}, 'unpaid': unpaid, 'sinks': sinks,
             'full_w': full_w, 'plan_day': PLAN_DAY, 'csv': CSVD, 'log': LOG, 'kdays': kdays}
    json.dump(world, open(OUT, 'w', encoding='utf-8'))
    # ---- report
    print('towns', len(tl), 'villages', len(vill), 'castles', len(clist), 'castle villages', sum(1 for v in vill if v['castle']))
    print('village basket world (pcs/d):', {g: round(full_w[g]) for g in G if full_w[g] > 0})
    for mk in (0, 1):
        vs = [v for v in vill if v['mk'] == mk]
        paidv = sum(v['bask'][i] * v['paid'][i] * val[G[i]] * start_idx[(mk, G[i])] for v in vs for i in range(NG))
        fullv = sum(v['bask'][i] * val[G[i]] * start_idx[(mk, G[i])] for v in vs for i in range(NG))
        print('market', mk, 'villages', len(vs), 'basket value at start prices %.0f, paid %.0f (%.0f%%)' % (fullv, paidv, 100 * paidv / fullv),
              'to-market grain %.0f' % sum(v['out'][0] for v in vs), 'town fields grain %.0f' % sum(tf[i][0] for i in range(len(T)) if tl[i]['mk'] == mk))
    if dfit: print('D-term fit: town basket factor median %.2f (%d towns at clamp)' % (sorted(dfit.values())[len(dfit) // 2], sum(1 for x in dfit.values() if x in (0.3, 3.0))))
    if basket_fit: print('basket fit per town: median %.2f, min %.2f, max %.2f' % (sorted(basket_fit.values())[len(basket_fit) // 2], min(basket_fit.values()), max(basket_fit.values())))
    print('base sales world %.0f (Plan: W 169765 + E 181372 incl. horses 18036), non-food %.0f' % (sum(v['sales_all'] for v in vill), sum(v['nonfood'] for v in vill)))
    print('measured takings (ks-budzety utarg) %.0f, to purse %.0f' % (sum(v['utarg'] for v in vill), sum(v['topurse'] for v in vill)))
    print('sinks grain', {k: round(x) for k, x in sinks.get('grain', {}).items()})
    print('goods in plan not simulated:', sorted(goods_seen - set(G) - set(LIVE)))


if __name__ == '__main__':
    main()
