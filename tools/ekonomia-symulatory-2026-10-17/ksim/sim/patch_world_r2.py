# round 2: sea links for the two towns without any road in world.json (Lys - island, Evenfall Hall - Tarth): the game's days table (TradeCarts.Road over
# MapDistanceModel, land + sea) has a route; the geo graph had none. Link = 1.25 x straight line to the nearest connected town of the same land/market,
# then the hub's land+sea distances. Writes world_r2.json (dsea only; dland unchanged - no land road).
import json, math, os
HERE = os.path.dirname(os.path.abspath(__file__))
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
T = W['towns']; nm = {t['name']: i for i, t in enumerate(T)}
ds = W['dsea']; NT = len(T)
for isl, hub in (('Lys', 'Aquos Dhaen'), ('Evenfall Hall', "Storm's End")):
    i, h = nm[isl], nm[hub]
    link = 1.25 * math.dist(T[i]['pos'], T[h]['pos'])
    n = 0
    for j in range(NT):
        if j == i: continue
        dh = 0.0 if j == h else ds[h][j]
        if dh is None or dh < 0: continue
        d = link + dh
        if ds[i][j] < 0 or d < ds[i][j]: ds[i][j] = d; ds[j][i] = d; n += 1
    print('%s <-> %s: link %.0f, %d towns reachable' % (isl, hub, link, n))
json.dump(W, open(os.path.join(HERE, 'world_r2.json'), 'w', encoding='utf-8'))
