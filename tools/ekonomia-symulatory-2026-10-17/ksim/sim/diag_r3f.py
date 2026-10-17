# paid D-term components per market and good (Q towns, QV villages, Der ZC5 inputs, One lords+courts) vs Plan to-market output
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF)
s = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, 's1_on.csv'), plan_off=os.path.join(R.H3OUT, 'off.csv'), init='A')
for m in (0, 1):
    for g in ('grain', 'fish', 'meat', 'cheese', 'butter', 'grape', 'olives', 'date_fruit', 'salt', 'orange'):
        i = s.GI[g]; ts = [t for t in range(s.NT) if s.T[t]['mk'] == m]
        Q = sum(s.Q[t][i] for t in ts); QV = sum(s.QV[t][i] for t in ts); Der = sum(s.der[t][i] for t in ts); One = sum(s.one[t][i] for t in ts)
        out = s.P0[m][i]; cs = sum(c['dem'][i] for c in s.C if c.get('mk', 0) == m)
        print('%s %-10s Q %6.0f QV %6.0f Der %6.0f One %5.0f | Dp %6.0f + castles %5.0f vs Plan out %6.0f  ratio %.2f' % ('WE'[m], g, Q, QV, Der, One, Q + QV + Der + One, cs, out, out / max(1, Q + QV + Der + One + cs)))
