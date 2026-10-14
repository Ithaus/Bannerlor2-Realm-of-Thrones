# Szacunek redaktora dla PROJEKT-WYBRANY (OBA + przeszczepy) na biegu fin30.
# Wejscie: ../czas/fin30-epizody.csv (206 oblezen [P]), ../projekt-oba/oba-zdobycia.tsv (57 zdobyc z flagami OBA [S]).
# Co liczy:
#  1) udzial strat oblegajacego w fazie murow - trzy modele: (a) fA cale straty szturmu (OBA), (b) rowna wymiana
#     (sedziowie: dolna granica), (c) wymiana wazona ciosami gry w fazie murow i po murach [K DefaultCombatSimulationModel.cs:249-262];
#  2) OB2 z losowym progiem armii U[L-0.05, L+0.05] (przeszczep: mniejsza wrazliwosc) i z twardym progiem L (OBA);
#  3) T2 = budowa obozu AI x0.5 i klasa twierdzy (miasto x1.5, stolica x2.5 czasu budowy) -> dodatkowe doby przed decyzja,
#     przezycie oblezenia exp(-H x doby), H = 0.061/dobe [P OBA szacunek-wynik.txt].
# Uruchom: python -I szacunek_wybrany.py <fin30-epizody.csv> <oba-zdobycia.tsv>
import csv, sys, ast, math, statistics as st

ep_path, oba_path = sys.argv[1], sys.argv[2]
eps = list(csv.DictReader(open(ep_path, encoding='utf-8'), delimiter=';'))
oba = list(csv.DictReader(open(oba_path, encoding='utf-8'), delimiter='\t'))
H, REB = 0.061, 0.29          # hazard konca bez szturmu [P OBA], powrot 26/91 [P OBA]
T0_MED, MEN_MED = 4.3, 2352   # gotowosc obozu przy x0.25 i 2352 ludziach [S ROZPOZNANIE-CZAS 4.1]

def lit(s):
    try: return ast.literal_eval(s)
    except Exception: return []

def rnd(x):                    # MathF.Round w .NET = do parzystej
    return round(x)

def hits_wall(D, A, adv):
    d = rnd(0.5 + max(1 + D ** 0.3 * 0.7 * adv, (D + 1) / (A + 1))) * 2
    a = rnd(1.5 + D ** 0.3) * 2
    return d, a

def hits_open(D, A):
    if D <= 10:
        d = max(rnd(min(A * 3, D * 0.3)), 1); a = max(rnd(min(D * 3, A * 0.3)), 1)
    else:
        d = rnd(min(A * 2, D ** 0.6)); a = rnd(min(D * 2, A ** 0.6))
    return d, a

# indeks epizodow: (nazwa, start) -> wiersz
idx = {(e['nazwa'], int(e['start_doba_biegu'])): e for e in eps}
caps = []
for x in oba:
    e = idx[(x['osada'], int(x['start']))]
    aL, oL, aS, oS = lit(e['szturm_A_ludzi']), lit(e['szturm_O_ludzi']), lit(e['szturm_A_straty']), lit(e['szturm_O_straty'])
    o0 = lit(e['O0']); adv = o0[0][4] if o0 else 4.255
    kind = x['rodzaj']; thr = 100 if kind == 'miasto' else 30
    fA = float(x['straty_A_szturm'] or 0)
    fu = fh = fA
    if aL and oL and oS and aL[-1] > 0 and oS[-1] > 0:
        A, O, AS, OS = aL[-1], oL[-1], aS[-1], oS[-1]
        Dw = max(0, min(OS, O - thr)); Dp = max(0, OS - Dw)
        fu = fA * (Dw / OS)
        if Dw > 0:
            dw, aw = hits_wall((O + thr) / 2, A, adv)
            dp, ap = hits_open(max(1, min(thr, O) / 2), max(1, A - AS))
            rw, rp = dw / max(aw, 1), dp / max(ap, 1)
            fh = fA * (rw * Dw) / (rw * Dw + rp * Dp)
        else:
            fh = 0.0
    s1 = float(x['S1_straty_A_szac']) if x['S1_straty_A_szac'] else None
    men = aL[-1] if aL and aL[-1] > 0 else MEN_MED
    T0 = min(8.0, max(2.5, T0_MED * math.sqrt(MEN_MED / men)))
    cap = x['stolica'] == '1'
    k = 4 if cap else (2 if kind == 'miasto' else 1)
    caps.append(dict(n=x['osada'], kind=kind, cap=cap, story=x['fabula'] == '1', sally=x['wynik'] == 'zdobyte po wypadzie',
                     mury=float(x['mury_O0']) if x['mury_O0'] else None, age=float(x['doba_O0']) if x['doba_O0'] else None,
                     r1=x['R1_doba_szturmu'], dob=int(x['dob']), fA=fA, fu=fu, fh=fh, s1=s1, extra=T0 * k))

# szturmy bez rozstrzygniecia (18): czy bylyby odparte
inc = []
for e in eps:
    if e['wynik'] == 'szturm bez zdobycia, potem odstapione':
        aL, aS = lit(e['szturm_A_ludzi']), lit(e['szturm_A_straty'])
        inc.append((aS[-1] / aL[-1]) if aL and aL[-1] else 0.0)

def p_rep(f, L, rand):
    if rand: return min(1.0, max(0.0, (f - (L - 0.05)) / 0.10))
    return 1.0 if f >= L else 0.0

def scen(model, L, rand, t2):
    kept = rep = 0.0; times = []
    for c in caps:
        if c['story'] or c['sally']:
            kept += 1; times.append(c['dob']); continue
        alive = 1.0
        if c['cap'] and c['mury'] is not None and c['mury'] <= 1: alive = 0.0
        elif c['r1'] == 'nigdy': alive = 0.0
        age = c['age'] or 0.0
        day = float(c['r1']) if c['r1'] not in ('', 'nigdy') else age
        if t2: day = max(day, age + c['extra'])
        if alive > 0 and day > age + 0.01: alive *= math.exp(-H * (day - age))
        f = c[model]
        if c['s1'] is not None: f = max(f, c['s1'])
        pr = p_rep(f, L, rand)
        rep += alive * pr; k = alive * (1 - pr); kept += k
        if k > 0.0001: times.append(c['dob'] + (day - age))
    lost = len(caps) - kept
    back = REB * lost * (kept / len(caps))
    incr = sum(p_rep(f, L, rand) for f in inc)
    return kept, back, rep, incr, times

print('zdobycia fin30', len(caps), '| bez rozstrzygniecia', len(inc))
print('udzial strat w fazie murow (model c / a): mediana', round(st.median([c['fh'] / c['fA'] for c in caps if c['fA'] > 0.01]), 3),
      '| (model b / a) mediana', round(st.median([c['fu'] / c['fA'] for c in caps if c['fA'] > 0.01]), 3))
for th in (0.18, 0.20, 0.22, 0.25):
    print(f'  ile zdobyc przekracza prog {th:.2f}: a {sum(c["fA"] >= th for c in caps if not c["story"])}'
          f' | b {sum(c["fu"] >= th for c in caps if not c["story"])} | c {sum(c["fh"] >= th for c in caps if not c["story"])}')
print()
print('model;L;prog;T2;zdobyc+powrot;odparte(zdobycia+bez_rozstrz);udzial_odpartych;mediana_dob_zdobycia;p90')
for model in ('fA', 'fu', 'fh'):
    for L in (0.18, 0.20, 0.22, 0.25):
        for rand in (False, True):
            for t2 in (False, True):
                kept, back, rep, incr, times = scen(model, L, rand, t2)
                tot_rep = rep + incr
                share = tot_rep / (tot_rep + kept) if (tot_rep + kept) else 0
                print(f'{model};{L:.2f};{"losowy" if rand else "twardy"};{"tak" if t2 else "nie"};{kept + back:.1f};{tot_rep:.1f};{share:.2f};'
                      f'{st.median(times):.1f};{st.quantiles(times, n=10)[-1]:.1f}')
print()
# skutek wedlug klasy, model c, L 0.22 losowy, T2
for cls in ('zamek', 'miasto'):
    ts = []
    for c in caps:
        if c['kind'] != cls or c['story'] or c['sally']: continue
        age = c['age'] or 0.0
        day = float(c['r1']) if c['r1'] not in ('', 'nigdy') else age
        day = max(day, age + c['extra'])
        ts.append(c['dob'] + (day - age))
    print(cls, 'doba zdobycia z T2 (gdyby zdobyte): mediana', round(st.median(ts), 1), 'p10/p90', [round(q, 1) for q in st.quantiles(ts, n=10)][0::8])
print('stolice: dodatkowe doby budowy (T2, x2.5):', [round(c['extra'], 1) for c in caps if c['cap']])
