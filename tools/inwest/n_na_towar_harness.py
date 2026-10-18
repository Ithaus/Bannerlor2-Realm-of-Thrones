"""Uzycie: python -I tools/inwest/n_na_towar_harness.py (blad kopii lancucha na towar; _wies.py - na wies i towar). Harness f5-zajecia (offline, synthetic): mirror of VillageCrafts per-good n (NeutralGoods) + pomiar pelny + FixPerGood.
World: districts of crafts with multi-good lists, side villages with other crafts (adds in host), per-village per-good game multipliers
(buildings/perks only for type goods), chain factors, kitchen grain (unscaled), fruit base r1 (unscaled) on outside adds, W2 (grain->fish) yesterday.
NeutralGoods computes with an imperfect chain copy (ChainFactor error on some goods) - the runtime uses the true chain. The fix must make every good exact."""
import random
random.seed(7)
CRAFTS = {'wheat': {'grain': 50, 'cow': .2, 'sheep': .4, 'hog': .8, 'papyrus': .5}, 'fish': {'fish': 28}, 'cattle': {'cow': 2, 'butter': 4, 'cheese': 4, 'egg': 1.5},
          'sheep': {'sheep': 4, 'wool': 10, 'butter': 2, 'cheese': 2}, 'date': {'date': 8, 'spice': .5}, 'silk': {'cotton': 8}, 'walrus': {'tusk': .5, 'fish': 6}}
KITCHEN = 3.0
G = {'wheat': 1.0, 'fish': 1.25, 'cattle': 1.14, 'sheep': .78, 'date': .95, 'silk': 1.46, 'walrus': 5.8}
goods = sorted({g for c in CRAFTS.values() for g in c} | {'grain'})
chain_err = {g: 1.0 + random.uniform(-0.04, 0.04) for g in goods}       # ChainFactor copy error (NeutralGoods only)
dists = []
for i in range(400):
    D = random.choice(list(CRAFTS))
    hot = random.random() < 0.3
    sides = [(random.uniform(0.02, 0.2), random.choice(list(CRAFTS))) for _ in range(random.randint(0, 5))]
    tot = 1.0
    sh = sum(s for s, _ in sides)
    if sh > 0.8: sides = [(s * 0.8 / sh, c) for s, c in sides]
    mult = {g: random.uniform(0.7, 1.6) for g in goods}       # game multipliers for TYPE goods (buildings, perks, culture)
    sfac = random.uniform(-0.1, 0.25)                         # factors on outside goods (policies NavalDLC)
    chain = {g: random.uniform(0.6, 1.4) for g in goods}     # true chain after crafts step (P1, hands, valve...)
    lvl = random.choice([1, 1.5, 2])
    u = random.choice([0, 0, 0, 0.15, 0.3])                  # W2 yesterday
    fruit = {g: (random.uniform(0, 2) if random.random() < 0.15 else 0) for g in goods}   # unscaled r1 of outside goods (VillageFruit)
    dists.append(dict(D=D, hot=hot, sides=sides, mult=mult, sfac=sfac, chain=chain, lvl=lvl, u=u, fruit=fruit))
def blocked(d, g): return g == 'papyrus' and not d['hot']
def type_base(d, g):     # probe: game result for a type good (rate x lvl x mult), kitchen grain for non-wheat
    if g == 'grain' and d['D'] != 'wheat': return KITCHEN * d['lvl'] * d['mult']['grain']
    return CRAFTS[d['D']].get(g, 0) * d['lvl'] * d['mult'][g]
def other_of(d): return sum(s for s, c in d['sides'] if c != d['D'])
def adds_of(d):          # craft -> part x g/g (N=1)
    a = {}
    for s, c in d['sides']:
        if c != d['D']: a[c] = a.get(c, 0) + s * G[c] / G[d['D']]
    return a
def type_goods(d):
    t = dict(CRAFTS[d['D']]);
    if d['D'] != 'wheat': t['grain'] = KITCHEN
    return t
# ---- NeutralGoods per-good (imperfect chain)
tgt, aT, aA = {}, {}, {}
w2 = []
for d in dists:
    o = other_of(d)
    for g in CRAFTS[d['D']]:
        q = type_base(d, g) * d['chain'][g] * chain_err[g]
        if blocked(d, g): q = 0
        tgt[g] = tgt.get(g, 0) + q; aT[g] = aT.get(g, 0) + q * (1 - o)
    if d['u'] > 0: w2.append((d, d['u'] * type_base(d, 'grain') * d['chain']['grain'] * chain_err['grain']))
    for c, k in adds_of(d).items():
        for g, r in CRAFTS[c].items():
            if blocked(d, g): continue
            A = k * r * d['lvl']
            intype = g in type_goods(d)
            fac = (1 + (d['mult'][g] - 1)) if intype else (1 + d['sfac'])
            aA[g] = aA.get(g, 0) + A * fac * d['chain'][g] * chain_err[g]
t = dict(tgt)
for d, x in w2: t['grain'] = t.get('grain', 0) - x; t['fish'] = t.get('fish', 0) + x
ng = {g: (t.get(g, 0) / (aT.get(g, 0) + aA.get(g, 0)) if t.get(g, 0) > 0 and aT.get(g, 0) + aA.get(g, 0) > 0 else 1.0) for g in set(t) | set(aT) | set(aA)}
# ---- runtime measurement (true chain), mirror of Base + Collect
def measure(ng):
    B, Aft, U = {}, {}, {}
    for d in dists:
        o = other_of(d); tg = type_goods(d); adds = adds_of(d)
        addA = {}
        for c, k in adds.items():
            for g, r in CRAFTS[c].items(): addA[g] = addA.get(g, 0) + k * r * d['lvl'] * ng.get(g, 1.0)
        items = [g for g in tg if not blocked(d, g)] + [g for g in addA if g not in tg and not blocked(d, g)]
        for g in items:
            ch = d['chain'][g]
            if g in tg:
                before = type_base(d, g)                          # model result before crafts step (with game multipliers)
                kitchen = g == 'grain' and d['D'] != 'wheat'
                F = 1.0 if kitchen else (1 - o) * ng.get(g, 1.0)
                mid = before * F
                if g in addA and addA[g] > 0: mid += addA[g] * d['mult'][g] if F > 1e-3 and before > 0 else addA[g]
                fin = mid * ch
                bf = before * ch
                unsc = (before * F * ch) if kitchen else 0.0
            else:
                r1 = d['fruit'][g]
                mid = r1 * (1 + d['sfac']) + addA[g] * (1 + d['sfac'])
                fin = mid * ch
                bf = r1 * (1 + d['sfac']) * ch
                unsc = r1 * (1 + d['sfac']) * ch
            if g == 'grain' and d['u'] > 0: B['fish'] = B.get('fish', 0) + d['u'] * bf; bf -= d['u'] * bf
            B[g] = B.get(g, 0) + bf; Aft[g] = Aft.get(g, 0) + fin; U[g] = U.get(g, 0) + unsc
    return B, Aft, U
def devs(B, A): return {g: (A.get(g, 0) - b) / b for g, b in B.items() if b >= 1}
B, A, U = measure(ng)
d0 = devs(B, A)
print('przed poprawka (n z rachunku, blad lancucha +-4%):', 'najwieksza %.3f%% w %s' % max(((abs(v) * 100, g) for g, v in d0.items())))
# ---- FixPerGood (mirror)
ng2 = dict(ng)
for g in B:
    b, a, u = B[g], A.get(g, 0), U.get(g, 0)
    S = a - u
    if b <= 1e-9 or abs((a - b) / b) <= 0.001 or S <= 1e-9 or g not in ng2: continue
    rr = (b - u) / S; rc = max(0.5, min(2.0, rr)); ng2[g] = max(0.2, min(5.0, ng2[g] * rc))
B2, A2, U2 = measure(ng2)
d1 = devs(B2, A2)
print('po poprawce:', 'najwieksza %.6f%% w %s' % max(((abs(v) * 100, g) for g, v in d1.items())), '| towarow', len(d1))
# craft-value view (NZW-1): value-weighted per craft
