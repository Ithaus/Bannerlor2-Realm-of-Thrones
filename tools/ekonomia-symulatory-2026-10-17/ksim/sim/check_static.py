# static check: D-term grain per town and village basket size per town vs ks-miasta (lambda 1 day)
import json, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim as S
W = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'world.json')))
s = S.Sim(W, start=109137)
o = W['obs']['109179']; K = {k: i for i, k in enumerate(W['obs_keys'])}
xs = []; ys = []; wa = []; wb = []
rows = []
for t in range(s.NT):
    d = s.dterm(t, s.iGrain); od = o[t][K['zboze_popyt_zaplacony_szt_d']]
    vw = sum(sum(v['bask']) for v in s.Vl if v['trade'] == t) * 7; ow = o[t][K['wsie_chciane_szt_7d']]
    rows.append((W['towns'][t]['name'], d, od, vw, ow))
    if d > 0 and od > 0: xs.append(math.log(d)); ys.append(math.log(od))
    wa.append(vw); wb.append(ow)
def corr(x, y):
    mx = sum(x)/len(x); my = sum(y)/len(y)
    return sum((a-mx)*(b-my) for a, b in zip(x, y))/math.sqrt(sum((a-mx)**2 for a in x)*sum((b-my)**2 for b in y))
print('grain D-term world sim %.0f obs %.0f, corr log %.3f' % (sum(r[1] for r in rows), sum(r[2] for r in rows), corr(xs, ys)))
print('village wanted /7d world sim %.0f obs %.0f, corr %.3f' % (sum(wa), sum(wb), corr(wa, wb)))
rows.sort(key=lambda r: -abs(math.log((r[1]+1)/(r[2]+1))))
for r in rows[:15]: print('%-22s Dsim %7.1f Dobs %7.1f | want sim %7.0f obs %7.0f' % r)
