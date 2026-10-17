# Per importer land x good: why KS-H does not deliver the Plan corridor (baseline rules, game snapshots, sim day table, inferred wars).
import sys, collections, math
sys.path.insert(0, r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/ksim/r4/tr')
import a5_replay as R

days_list = [109144, 109151, 109158, 109165]
c12, cfar = R.mkcand(12)
targets = [('qartheen', 'grain'), ('crownlands', 'grain'), ('aserai', 'grain'), ('tyroshi', 'grape'), ('empire', 'fish'), ('pentoshi', 'fish'), ('myrish', 'olives'),
           ('qartheen', 'olives'), ('ghiscari', 'grain'), ('ghiscari', 'olives'), ('aserai', 'grape'), ('dragonstone', 'grain'), ('volantine', 'fish')]
EXP = {'grain': ['volantine', 'reach', 'river', 'vlandia', 'khuzait', 'myrish'], 'grape': ['volantine', 'myrish', 'stormlands', 'reach'], 'fish': ['lyseni', 'qartheen', 'tyroshi', 'reach'],
       'olives': ['tyroshi', 'volantine', 'lyseni', 'stormlands']}
for d in days_list:
    st, g = R.load(d)
print('miasto (kraina) towar | dni polki | kandydaci 12: kraina eksportera w 12? | powody odrzucenia 12 najblizszych (srednio z 4 dob)')
for (L, gname) in targets:
    tows = [t for t in range(R.NT) if R.land.get(R.ids[t]) == L]
    for t in tows:
        why = collections.Counter(); expin = []
        for d in days_list:
            st, gold = R.load(d) if d != days_list[-1] else (st, g)
            x = st.get((R.ids[t], gname))
            if not x: continue
            for s in c12[t]:
                xs = st.get((R.ids[s], gname))
                if not xs: continue
                b = max(xs['dp'], xs['off']); sur = xs['sh'] - 1.25 * xs['T'] * b - 5
                Ls = R.days[s][t]; tau = 0.0025 if R.sea[s][t] else 0.025
                if sur <= 1: why['bez nadwyzki'] += 1
                elif R.atwar(s, t): why['wojna'] += 1
                elif x['ix'] < xs['ix'] * (1 + tau * Ls) * 1.03: why['indeks'] += 1
                else: why['OK'] += 1
        exp12 = sorted(set(R.land[R.ids[s]] for s in c12[t] if R.land[R.ids[s]] in EXP.get(gname, [])))
        firstexp = None
        allc = sorted([(R.days[s][t], s) for s in range(R.NT) if s != t and R.T[s]['mk'] == R.T[t]['mk'] and R.days[s][t] >= 0])
        for dd, s in allc:
            if R.land[R.ids[s]] in EXP.get(gname, []) and not R.atwar(s, t):
                firstexp = (R.T[s]['name'], dd, 'm' if R.sea[s][t] else 'l', [i for i, (_, ss) in enumerate(allc) if ss == s][0] + 1); break
        print('%-15s (%-10s) %-6s | %5.1f d | eksporter w 12: %-25s najblizszy eksporter (nie wrog): %s | %s' % (
            R.T[t]['name'], L, gname, x['sh'] / max(1e-9, max(x['dp'], x['off'])) if x else -1, ','.join(exp12) or '-', firstexp,
            ', '.join('%s %.1f' % (k, v / len(days_list)) for k, v in why.most_common())))
