import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['ownfirst'] = 0
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
K = sim.trade_mod; i = sim.GI['grain']; c = sim.gcat[i]
q = [t for t in range(sim.NT) if sim.T[t]['name'] == 'Qarth'][0]
while sim.day < 109147: sim.step()
# replicate the module for grain/Essos exactly, printing dst order and allocation
day = sim.day; o_ = K.o; sl = 2
ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == 1]
offer = {}
for t in ts:
    d = max(0.0, sim.dterm(t, i)); T = 7.14 * K.tsi[t][i] ** (-1 / 0.6)
    sur = sim.shelf[t][i] - o_['ksrc'] * T * d - 5
    if sur > 1: offer[t] = sur * o_['offer'] * sl
print('day', day, 'parity', day % 2, 'total offer', round(sum(offer.values())))
dst = []
for t in ts:
    if K.slot[t] != day % sl: continue
    d = sim.dterm(t, i)
    if d <= 0: continue
    T = 7.14 * K.tsi[t][i] ** (-1 / 0.6); H = sim.shelf[t][i] + K.inc[t][i]
    if H < T * d: dst.append((sim.idx[t][c], t, d, T, H))
dst.sort(reverse=True)
print('dests', [(sim.T[t]['name'], round(Id, 1), round(d)) for (Id, t, d, T, H) in dst])
print('Qarth slot', K.slot[q], 'in dst', any(x[1] == q for x in dst), 'H', sim.shelf[q][i] + K.inc[q][i], 'T*d', 3.6 * sim.dterm(q, i))
