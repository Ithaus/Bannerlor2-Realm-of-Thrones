# Szacunek [S] skutku PROJEKT-OBLEZENIA na biegu fin30 (python -I, tylko odczyt plikow roboczych).
# Wejscie: czas/fin30-epizody.csv (206 oblezen, 113 decyzji O0), projekt/o0-stolica.csv (flaga stolicy z linii O0).
# Model: sily z chwili O0 stale; decyzja wg nowej krzywej wagi murow; przezycie oblezenia od starej do nowej doby decyzji
# wg hazardu zakonczen "nie-szturmowych" z fin30 (wypad, bitwa pod murami, odstapienie); szturm odparty wg regul O3+O4.
import csv, sys, ast, math, statistics as st, os

D = os.path.dirname(os.path.abspath(sys.argv[0]))
EP = os.path.join(D, '..', 'czas', 'fin30-epizody.csv')
CAP = os.path.join(D, 'o0-stolica.csv')

# ---- parametry projektu (rozdz. 2 PROJEKT-OBLEZENIA)
HOLD = {'zamek': float(os.environ.get('HZ', '14')), 'miasto': float(os.environ.get('HM', '21')), 'stolica': float(os.environ.get('HS', '35'))}   # doby pelnej (podwojnej) wagi murow
EASE = {k: 2.0 * v for k, v in HOLD.items()}               # doby spadku wagi z 2.0 x w0 do 1.0 x w0
F_START, F_END = 2.0, 1.0
WALLS_X = float(os.environ.get('WALLS_X', '1.5'))          # O3: ciosy obroncow w szturmie
REPULSE = float(os.environ.get('REPULSE', '0.333'))        # O4: odwrot przy stratach >= tej czesci ludzi
THR = {'zamek': 20, 'miasto': 50}                          # O3: mury dzialaja, dopoki obroncow > THR
COH_DAYS = 100.0                                           # O6: spojnosc -1/d -> armia wytrzymuje ok. 100 dob [S]

rows = list(csv.DictReader(open(EP, encoding='utf-8'), delimiter=';'))
caps = set()
for ln in open(CAP, encoding='utf-8'):
    p = ln.strip().split(';')
    if len(p) >= 5 and p[4] == 'tak':
        caps.add((p[1] if p[1] else p[2]) + '|' + p[0])

# ---- hazard zakonczen nie-szturmowych na dobe oblezenia (Kaplan-Meier), fin30
EVENT = {'wypad - pobity oblegajacy', 'pod murami - pobity oblegajacy', 'odstapione bez bitwy'}
maxd = 40
at = [0] * (maxd + 2); ev = [0] * (maxd + 2)
for r in rows:
    L = int(r['dob'])
    for d in range(1, min(L, maxd) + 1):
        at[d] += 1
    if r['wynik'] in EVENT and L <= maxd:
        ev[L] += 1
h = [0.0] * (maxd + 2)
for d in range(1, maxd + 1):
    h[d] = ev[d] / at[d] if at[d] else 0.0
late = [h[d] for d in range(8, 18) if at[d] >= 10]
H_LATE = st.mean(late) * float(os.environ.get('HLX', '1.0'))

NOHAZ = os.environ.get('NOHAZ') == '1'
def haz(d):
    if NOHAZ:
        return 0.0
    return h[d] if d <= 17 and at[d] >= 10 else H_LATE

def surv(t0, t1):
    s = 1.0
    for d in range(int(math.floor(t0)) + 1, int(math.ceil(t1)) + 1):
        s *= 1.0 - haz(d)
    return s

# ---- 113 decyzji
dec = []
for r in rows:
    if r['O0'] == '[]':
        continue
    o = ast.literal_eval(r['O0'])[0]
    doba, wiek, sg, mury, adv, w, eng, ram, tower, ch, att, de = o[:12]
    w0 = 0.8 * (1.0 if ram else 1.25) * (1.0 if tower else 1.25)
    day_abs = int(r['start_dzien']) + int(doba) - 1
    cls = r['rodzaj']
    capital = any(k.startswith(r['region'] + '|') and abs(int(k.split('|')[1]) - day_abs) <= 2 for k in caps)
    klasa = 'stolica' if capital else cls
    # wymagana waga: s = att / (def * adv^(w0 f)) > 1  ->  f < 1 + ln(mury)/(w0 ln adv)
    if de <= 0:
        fstar = 99.0
    else:
        fstar = 1.0 + math.log(max(mury, 1e-9)) / (w0 * math.log(adv))
    if fstar >= F_START:
        tnew = wiek
    elif fstar < F_END:
        tnew = float('inf')
    else:
        tnew = HOLD[klasa] + (F_START - fstar) / (F_START - F_END) * EASE[klasa]
        tnew = max(tnew, wiek)
    sz = ast.literal_eval(r['szturm_doby_od_startu'])
    dur = (sz[0] - wiek) if sz else 4.0
    A = ast.literal_eval(r['szturm_A_ludzi']); Ast = ast.literal_eval(r['szturm_A_straty']); Ost = ast.literal_eval(r['szturm_O_straty'])
    e = (Ast[0] / Ost[0]) if (Ast and Ost and Ost[0] > 0) else 1.54
    Dmen = float(r['zaloga0'] or 0) + float(r['milicja0'] or 0)
    Amen = A[0] if A else None
    dec.append(dict(n=r['nazwa'], cls=cls, kl=klasa, wiek=wiek, mury=mury, fstar=fstar, tnew=tnew, wynik=r['wynik'],
                    dur=max(1.0, dur), e=e, D=Dmen, A=Amen, start=int(r['start_doba_biegu'])))

# ---- skutek
def repulsed(x, wx=WALLS_X, rep=REPULSE):
    if x['A'] is None or x['A'] <= 0:
        return 0.0
    loss = x['e'] * wx * max(0.0, x['D'] - THR[x['cls']])
    return 1.0 if loss >= rep * x['A'] else 0.0

out = {'zdob': 0.0, 'odparte': 0.0, 'okup': 0.0, 'inne_dotrwalo': 0.0, 'przerwane': 0.0, 'bez_szturmu_mury<1': 0}
czasy = {'zamek': [], 'miasto': [], 'stolica': []}
for x in dec:
    if x['tnew'] == float('inf'):
        out['bez_szturmu_mury<1'] += 1
        continue
    if x['tnew'] > COH_DAYS:
        out['przerwane'] += 1
        continue
    s = surv(x['wiek'], x['tnew'])
    out['przerwane'] += 1 - s
    if x['wynik'] == 'okup (O1)':
        out['okup'] += s
    elif x['wynik'] == 'zdobyte szturmem':
        rp = repulsed(x)
        out['odparte'] += s * rp
        out['zdob'] += s * (1 - rp)
        if rp == 0:
            czasy[x['kl']].append((x['tnew'] + x['dur'], s))
    else:
        out['inne_dotrwalo'] += s

print('hazard dzienny (doba: h, narazone):', ' '.join('%d:%.3f/%d' % (d, h[d], at[d]) for d in range(1, 18)))
print('hazard pozny (doby 8-17) = %.3f' % H_LATE)
print('decyzji', len(dec), 'klasy', {k: sum(1 for x in dec if x['kl'] == k) for k in ('zamek', 'miasto', 'stolica')})
print('fstar >= 2 (szturm od razu):', sum(1 for x in dec if x['fstar'] >= F_START), '| mury<1 (bez szturmu):', out['bez_szturmu_mury<1'])
print('WALLS_X', WALLS_X, 'REPULSE', REPULSE)
for k, v in out.items():
    print('  %-22s %.1f' % (k, v))

def wmed(pairs):
    if not pairs:
        return '-'
    pairs = sorted(pairs)
    tot = sum(w for _, w in pairs); c = 0
    for t, w in pairs:
        c += w
        if c >= tot / 2:
            return '%.0f' % t
for k, v in czasy.items():
    print('  zdobycie (doba oblezenia) %-8s n=%.1f mediana %s min %s max %s' % (k, sum(w for _, w in v), wmed(v),
          '%.0f' % min(t for t, _ in v) if v else '-', '%.0f' % max(t for t, _ in v) if v else '-'))
# odparte w szturmach dzis (zdobyte), bez zmiany czasu
z = [x for x in dec if x['wynik'] == 'zdobyte szturmem']
print('szturmy zdobyte w fin30 z O0:', len(z), '| odparte wg O3+O4 (bez zmian czasu):', int(sum(repulsed(x) for x in z)))
for wx in (1.0, 1.25, 1.5, 2.0):
    for rep in (0.25, 0.333, 0.4):
        print('   WALLS_X %.2f REPULSE %.3f -> odparte %d z %d' % (wx, rep, int(sum(repulsed(x, wx, rep) for x in z)), len(z)))
print('wymiana e (A_straty/O_straty) mediana %.2f' % st.median(x['e'] for x in z))
print('ludzie A/D mediana %.1f' % st.median(x['A'] / x['D'] for x in z if x['A'] and x['D'] > 0))

# ---- przyklady (nazwa: klasa, mury, f*, stara doba O0 -> nowa, ludzie A/D, odparty?)
if os.environ.get('PRZYKLADY') == '1':
    for x in dec:
        if x['wynik'] == 'zdobyte szturmem' and x['n'] in ("King's Landing", 'Yronwood', 'Starfall', 'Willow Wood', 'Duskendale', 'Rosby', 'Harrenhal', 'Cape Wrath', 'Lonely Light', 'Vaes Dothrak', 'Lorath'):
            print('  %-14s %-7s start %3d mury %.2f f* %.2f O0 %.1f -> %s  A %s D %.0f e %.2f odparty %d' % (
                x['n'], x['kl'], x['start'], x['mury'], x['fstar'], x['wiek'], '%.0f' % x['tnew'] if x['tnew'] != float('inf') else 'nigdy',
                x['A'], x['D'], x['e'], repulsed(x)))
    # rok 1 / rok 2 (zdobycia z decyzji, gorna granica bez hazardu)
    y = {1: [0, 0], 2: [0, 0]}
    for x in dec:
        if x['wynik'] != 'zdobyte szturmem' or x['tnew'] == float('inf') or x['tnew'] > COH_DAYS:
            continue
        yr = 1 if x['start'] <= 364 else 2
        y[yr][0] += 1
        y[yr][1] += repulsed(x)
    print('  rok: [zdobyte w fin30 z decyzja dopuszczona, z tego odparte wg O3+O4]', y)

# ---- doby oblezen ponad fin30 (gospodarka: oblezona osada nie placi, karawany staja) [S]
if os.environ.get('DOBY') == '1':
    up = 0.0; ex = 0.0; inf_n = 0
    for x in dec:
        t1 = x['tnew']
        if t1 == float('inf'):
            inf_n += 1
            t1 = HOLD[x['kl']] + EASE[x['kl']]          # mury < 1: czeka do konca cierpliwosci (gorna granica)
        t1 = min(t1, COH_DAYS)
        up += max(0.0, t1 - x['wiek'])
        s = 1.0; d = int(math.floor(x['wiek']))
        while d < t1:
            d += 1
            s *= 1.0 - (H_LATE if d > 17 else haz(d))
            ex += s
    print('  doby oblezen fin30: 1768; dodatkowe doby: gorna granica %.0f (mury<1: %d do konca cierpliwosci), oczekiwane wg hazardu %.0f' % (up, inf_n, ex))
    print('  srednio oblezonych naraz: fin30 2.43 -> %.1f (gorna) / %.1f (hazard)' % ((1768 + up) / 728, (1768 + ex) / 728))
