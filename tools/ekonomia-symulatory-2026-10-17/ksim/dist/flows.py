# World transport of food by carrier (Towary lines, Dowoz (wozy), 215, Karawany) - window averages. Read-only.
import re, sys, collections, statistics as st
LOG = sys.argv[1]
D0 = int(sys.argv[2]) if len(sys.argv) > 2 else 109123
D1 = int(sys.argv[3]) if len(sys.argv) > 3 else 109195
FOOD = ['grain', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'meat', 'honey', 'bread', 'beer', 'wine', 'pie', 'oil']
rt = re.compile(r'\] Towary: dzien (\d+) - (\S+) \(')
acc = collections.defaultdict(lambda: collections.defaultdict(list))
wag = []; exp = []; car = []; vsell = collections.defaultdict(list)
def num(pat, s):
    m = re.search(pat, s); return float(m.group(1)) if m else 0.0
with open(LOG, encoding='utf-8', errors='replace') as f:
    for line in f:
        if '] Towary: dzien' in line:
            m = rt.search(line)
            if not m: continue
            d = int(m.group(1)); g = m.group(2)
            if g not in FOOD or not (D0 <= d <= D1): continue
            a = acc[g]
            a['prod'].append(num(r'\| produkcja (\d+)', line)); a['cons'].append(num(r'\| zuzycie (\d+)', line))
            a['wagons'].append(num(r'wozy do osad (\d+)', line))
            a['car_buy'].append(num(r'karawany kupily (\d+)', line)); a['car_sell'].append(num(r'karawany kupily \d+, sprzedaly (\d+)', line))
            a['lord_buy'].append(num(r'lordowie kupili (\d+)', line)); a['lord_dump'].append(num(r'lordowie porzucili na polki miast (\d+)', line))
            a['oth_buy'].append(num(r'inne partie kupily (\d+)', line))
            a['vil_buy'].append(num(r'wsie \(zakupy w miescie, E5/B1\) (\d+)', line))
            a['town_stock'].append(num(r'posiadacze \[miasta (\d+)', line)); a['car_hold'].append(num(r'karawany (\d+), lordowie', line))
        elif '] Dowoz (wozy): dzien' in line:
            d = int(num(r'dzien (\d+)', line))
            if D0 <= d <= D1:
                wag.append((num(r'CZYNNY: (\d+) wozow', line), num(r'do wlasnego miasta (\d+)', line), num(r'do innego (\d+)', line), num(r'srednio (\d+) jedn', line), num(r'bez miasta w zasiegu (\d+)', line)))
        elif '] Przewoz kupcow wozem (215): dzien' in line:
            d = int(num(r'dzien (\d+)', line))
            if D0 <= d <= D1:
                exp.append((num(r'kupione (\d+) szt', line), num(r'srednio ([\d.]+) dob drogi', line), num(r'w drodze (\d+) szt', line)))
        elif '] Karawany: dzien' in line:
            d = int(num(r'dzien (\d+)', line))
            if D0 <= d <= D1:
                car.append((num(r'wjazdy karawan do miast (\d+)', line), num(r'wyjazdy z miast (\d+)', line)))
        elif '] Towary (utarg wozow): dzien' in line:
            d = int(num(r'dzien (\d+)', line))
            if D0 <= d <= D1:
                for g in FOOD:
                    m = re.search(r'; ' + g + r' (\d+) szt\.', line) or re.search(r'\| ' + g + r' (\d+) szt\.', line)
                    vsell[g].append(float(m.group(1)) if m else 0.0)
print('window', D0, '..', D1, 'log', LOG.split('/')[-1])
print('%-10s %7s %7s %8s %7s %7s %7s %7s %7s %7s %9s %8s' % ('good', 'prod', 'cons', 'wag_sold', 'vilbuy', 'car_buy', 'car_sel', 'lordbuy', 'lorddmp', 'oth_buy', 'townstock', 'car_hold'))
tot = collections.defaultdict(float)
for g in FOOD:
    a = acc[g]
    if not a['prod']: continue
    m = {k: st.mean(v) for k, v in a.items()}
    ws = st.mean(vsell[g]) if vsell[g] else 0
    print('%-10s %7.0f %7.0f %8.0f %7.0f %7.0f %7.0f %7.0f %7.0f %7.0f %9.0f %8.0f' % (g, m['prod'], m['cons'], ws, m['vil_buy'], m['car_buy'], m['car_sell'], m['lord_buy'], m['lord_dump'], m['oth_buy'], a['town_stock'][-1], a['car_hold'][-1]))
    for k in ('prod', 'cons', 'car_buy', 'car_sell', 'lord_buy', 'lord_dump', 'vil_buy'): tot[k] += m[k]
    tot['wag'] += ws
print('raw food (grain..honey) + processed: prod %.0f cons %.0f wagons sold %.0f caravans bought %.0f sold %.0f lords bought %.0f dumped %.0f villages bought %.0f' % (tot['prod'], tot['cons'], tot['wag'], tot['car_buy'], tot['car_sell'], tot['lord_buy'], tot['lord_dump'], tot['vil_buy']))
if wag:
    n = len(wag)
    print('village wagons/day: departures %.0f, to own town %.0f, to other town %.0f (%.0f%%), avg road %.0f units, no town in range %.1f (n=%d days)' % (
        st.mean(w[0] for w in wag), st.mean(w[1] for w in wag), st.mean(w[2] for w in wag), 100 * sum(w[2] for w in wag) / max(1, sum(w[0] for w in wag)), st.mean(w[3] for w in wag), st.mean(w[4] for w in wag), n))
if exp:
    print('merchant export by cart (215, arms only): bought %.0f szt/d, avg %.1f days, in transit %.0f szt' % (st.mean(e[0] for e in exp), st.mean(e[1] for e in exp), st.mean(e[2] for e in exp)))
if car:
    print('caravans: entries to towns %.0f/d, departures %.0f/d' % (st.mean(c[0] for c in car), st.mean(c[1] for c in car)))
