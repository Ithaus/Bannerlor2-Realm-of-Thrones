# round 2: alpha_net feedback - weekly Plan budget, purses, levies over a long run with the C# alpha formula (remeasure=2), with and without the levies in the outflows
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
days = int(sys.argv[1]) if len(sys.argv) > 1 else 120
for split in (1.0, 0.57):
    o = dict(R.DEF); o.update(kwfix=1, remeasure=2, an_split=split, candx=1, res_short=0.0)
    ob = dict(o); ob.update(R.BASE_OFF)
    sim = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, 's1_on.csv'), plan_off=os.path.join(R.H3OUT, 'off.csv'), init='A')
    D = o['D']; rows = []
    while sim.day < D + days:
        sim.step()
        if (sim.day - D) % 14 == 0 and sim.day >= D:
            bud = [sum(sim.budget_v[k] for k, v in enumerate(sim.Vl) if v['mk'] == m) for m in (0, 1)]
            pur = [sum(sim.gold[k] for k, v in enumerate(sim.Vl) if v['mk'] == m) for m in (0, 1)]
            net = [sum((sim.e_in[k] or 0) + sim.e_fr[k] - sim.e_out[k] for k, v in enumerate(sim.Vl) if v['mk'] == m) for m in (0, 1)]
            al = [x for x in getattr(sim, '_a_used', []) if x is not None]
            rows.append('D+%d budget W %.0f E %.0f | net counted W %.0f E %.0f | purses W %.2fM E %.2fM | alpha mean %.3f' % (sim.day - D, bud[0], bud[1], net[0], net[1], pur[0] / 1e6, pur[1] / 1e6, sum(al) / max(1, len(al))))
    print('== levies counted share %.2f' % split); print('\n'.join(rows))
