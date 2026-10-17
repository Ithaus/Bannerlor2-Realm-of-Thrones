import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim as S
W = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'world.json')))
P = {}
for kv in sys.argv[1:]:
    k, v = kv.split('='); P[k] = float(v)
s = S.Sim(W, P, start=109123)
for d in range(56): s.step()
s.reset_counters()
for d in range(14): s.step()
nd = s.c_days
g = s.iGrain
print('grain prod/cons per market per day: W %.0f/%.0f  E %.0f/%.0f' % (s.c_prod[0][g]/nd, s.c_cons[0][g]/nd, s.c_prod[1][g]/nd, s.c_cons[1][g]/nd))
print('sinks per day:', {k: round(v/nd) for k, v in sorted(s.c_sink.items())})
print('purse: inflow %.0f spent %.0f other %.0f rev %.0f | deps/d %.1f other-town %.2f mean dist %.0f' % (s.c_inflow/nd, s.c_spent/nd, s.c_other/nd, s.c_rev/nd, s.c_dep/nd, s.c_other_town/max(1,s.c_dep), s.c_wdist/max(1,s.c_dep)))
for gn in ('grain','fish','beer','salt','cheese'):
    i=s.GI[gn]; print('  sinks of', gn, {k[0]: round(v/nd) for k, v in s.c_sg.items() if k[1]==i}, 'ledger', {k: round(W['sinks'][gn].get(k,0)) for k in ('town','castle','vil','court','party','lords_buy','craft','bkcraft','wsh')})
print('trade', {k: round(v/nd) for k, v in s.c_trade.items()})
for i, gn in enumerate(s.G):
    pw = s.c_prod[0][i]/nd; cw = s.c_cons[0][i]/nd; pe = s.c_prod[1][i]/nd; ce = s.c_cons[1][i]/nd
    stw = sum(s.shelf[t][i] for t in range(s.NT) if s.T[t]['mk']==0); ste = sum(s.shelf[t][i] for t in range(s.NT) if s.T[t]['mk']==1)
    ob = W['rynki']['109193']
    o0 = ob['0'].get(gn, [0,0,0]); o1 = ob['1'].get(gn, [0,0,0])
    print('%-10s W sim %6.0f/%6.0f/%7.0f obs %6.0f/%6.0f/%7.0f | E sim %6.0f/%6.0f/%7.0f obs %6.0f/%6.0f/%7.0f' % (gn, pw, cw, stw, o0[0], o0[1], o0[2], pe, ce, ste, o1[0], o1[1], o1[2]))
