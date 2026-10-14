# Szacunek skutku zestawu OBA (R1 cierpliwosc x4.33 + stolica, R2 odparcie szturmu, S1 mury w bitwie ze szturmu) na biegu fin30.
# Wejscie: robocze/czas/fin30-epizody.csv (206 oblezen, [P]), lista stolic z linii L5b-1 (7 zdobyc stolic, [P]).
# Wyjscie: robocze/projekt-oba/oba-zdobycia.tsv (57 zdobyc z flagami) + podsumowanie na stdout.
# Uruchom: python -I szacunek_oba.py <fin30-epizody.csv> <katalog_wyjscia>
import csv, sys, ast, math, statistics as st, os

src, out = sys.argv[1], sys.argv[2]
rows = list(csv.DictReader(open(src, encoding='utf-8'), delimiter=';'))
L_LIST = [0.15, 0.18, 0.20, 0.22, 0.25]
T1_DAYS = 4.3   # wariant T1: budowa obozu AI x0.5 -> decyzja o ok. 4.3 doby pozniej [S rozdz. 4.1 ROZPOZNANIE-CZAS]
CAPITALS = {("Lorath", 130), ("Totava", 233), ("Iksa Calo", 268), ("Iksa Keria", 297), ("Vaes Dothrak", 415),
            ("King's Landing", 516), ("Silverhill", 603)}   # start oblezenia (doba biegu) zdobytych stolic, L5b-1 [P]
STORY = {"Harrenhal"}                                         # szturm fabularny ROT (bez O0) - poza regulami

def lit(s):
    try: return ast.literal_eval(s)
    except Exception: return []

def thr(kind): return 100 if kind == 'miasto' else 30

# --- wymiana w szturmie: oblegajacy straceni na obronce (zdobycia, B175) [P]
ex = []
for r in rows:
    if r['wynik'] != 'zdobyte szturmem': continue
    a, o = lit(r['szturm_A_straty']), lit(r['szturm_O_straty'])
    if a and o and o[-1] >= 150 and a[-1] > 0: ex.append(a[-1] / o[-1])
E = st.median(ex)

# --- hazard konca oblezenia bez szturmu (wypad, odsiecz, odstapienie) - na dobe, do R1 [P -> S]
exp_days, events = 0.0, 0
for r in rows:
    d = int(r['dob']); o0 = lit(r['O0'])
    first = o0[0][1] if o0 else None
    if first is not None:
        exp_days += first
    else:
        exp_days += d
        if r['wynik'] in ('wypad - pobity oblegajacy', 'odstapione bez bitwy', 'pod murami - pobity oblegajacy'): events += 1
H = events / exp_days

# --- powrot: oblezenie przegrane sila (wypad / pod murami / szturm bez zdobycia), a ta sama twierdza zdobyta pozniej w biegu [P]
by = {}
for r in rows: by.setdefault(r['nazwa'], []).append(r)
fail, back = 0, 0
for name, eps in by.items():
    eps.sort(key=lambda x: int(x['start_doba_biegu']))
    for i, r in enumerate(eps):
        if r['wynik'] in ('wypad - pobity oblegajacy', 'pod murami - pobity oblegajacy', 'szturm bez zdobycia, potem odstapione'):
            if int(r['start_doba_biegu']) > 600: continue      # za malo czasu do konca biegu
            fail += 1
            later = [x for x in eps[i+1:] if int(x['start_doba_biegu']) - int(r['koniec_dzien']) + 108835 <= 120]
            if any(x['wynik'].startswith('zdobyte') for x in later): back += 1
REB = back / fail if fail else 0.0

def r1_wait(o0):
    """Doba, od ktorej R1 pozwala na szturm (stosunek z pelnymi murami <= 1), albo None (nigdy - mury > podloga)."""
    if not o0: return 0.0
    age, ratio, mury, adv, w, eng, ram, tower = o0[0][1], o0[0][2], o0[0][3], o0[0][4], o0[0][5], o0[0][6], o0[0][7], o0[0][8]
    if mury > 1: return age
    m = (1.0 if ram else 1.25) * (1.0 if tower else 1.25)
    need = abs(math.log(mury)) / math.log(adv)                 # o ile wykladnik musi spasc ponizej w0
    if need >= 0.8 * m: return None                            # podloga 0 - nigdy (dopoki sily stoja)
    return max(age, 416 / 24 + need / (0.00462 * m) / 24)

res = []
for r in rows:
    if not r['wynik'].startswith('zdobyte'): continue
    name, start, kind = r['nazwa'], int(r['start_doba_biegu']), r['rodzaj']
    o0 = lit(r['O0']); mury = o0[0][3] if o0 else None
    cap = (name, start) in CAPITALS
    aL, aS = lit(r['szturm_A_ludzi']), lit(r['szturm_A_straty'])
    fA = (aS[-1] / aL[-1]) if aL and aL[-1] else 0.0
    # S1: przegrana zalogi w bitwie ze szturmu (pod murami, strona twierdzy broni, wygrywa oblegajacy)
    zal = float(r['zaloga0'] or 0) + float(r['milicja0'] or 0)
    s1 = None
    for p in lit(r['pod_murami']):
        if p[4] == 'zaloga' and p[1] == 'A':
            share = min(1.0, zal / p[3]) if p[3] else 0
            if share >= 0.5:
                f = E * max(0, p[3] - thr(kind)) / p[2]
                s1 = max(s1 or 0, f)
    wait = r1_wait(o0)
    res.append(dict(name=name, start=start, kind=kind, dob=int(r['dob']), wynik=r['wynik'], cap=cap, story=name in STORY,
                    mury=mury, fA=fA, s1=s1, wait=wait, age=(o0[0][1] if o0 else None)))

def scen(L):
    kept, rep_cap, r1_stop, r1_late, times = 0.0, 0, 0.0, 0, []
    for x in res:
        if x['story'] or x['wynik'] == 'zdobyte po wypadzie':
            kept += 1; times.append(x['dob']); continue
        p = 1.0
        # R1: stolica - wykladnik stale w0 (szturm tylko przy murach > 1); reszta - czekanie do doby wait
        if x['cap'] and x['mury'] is not None and x['mury'] <= 1: p = 0.0
        elif x['wait'] is None: p = 0.0
        elif x['wait'] > (x['age'] or 0) + 0.01:
            p = math.exp(-H * (x['wait'] - x['age'])); r1_late += 1
        r1_stop += 1 - p
        # R2 / S1: odwrot przy stratach >= L
        rep = (x['s1'] is not None and x['s1'] >= L) or x['fA'] >= L
        if rep: rep_cap += 1; p_cap = 0.0
        else: p_cap = p
        kept += p_cap
        if p_cap > 0: times.append(x['dob'] + ((x['wait'] - x['age']) if (x['wait'] and x['age'] and x['wait'] > x['age']) else 0))
    lost = len(res) - kept
    back = REB * lost * (kept / len(res))                       # powrot: czesc ocalonych pada pozniej, z ta sama szansa sukcesu
    return kept, rep_cap, r1_stop, r1_late, back, times

# szturmy bez rozstrzygniecia (18) - ile staloby sie odpartymi
inc = []
for r in rows:
    if r['wynik'] == 'szturm bez zdobycia, potem odstapione':
        aL, aS = lit(r['szturm_A_ludzi']), lit(r['szturm_A_straty'])
        inc.append((aS[-1] / aL[-1]) if aL and aL[-1] else 0.0)
# bitwy ze szturmu wygrane dzis przez strone twierdzy (14) - zostaja obrona

os.makedirs(out, exist_ok=True)
with open(os.path.join(out, 'oba-zdobycia.tsv'), 'w', encoding='utf-8', newline='') as f:
    w = csv.writer(f, delimiter='\t')
    w.writerow(['osada', 'start', 'rodzaj', 'dob', 'wynik', 'stolica', 'fabula', 'mury_O0', 'doba_O0', 'R1_doba_szturmu', 'straty_A_szturm', 'S1_straty_A_szac'])
    for x in res:
        w.writerow([x['name'], x['start'], x['kind'], x['dob'], x['wynik'], int(x['cap']), int(x['story']),
                    '' if x['mury'] is None else round(x['mury'], 3), x['age'] if x['age'] is not None else '',
                    'nigdy' if x['wait'] is None else round(x['wait'], 1), round(x['fA'], 3), '' if x['s1'] is None else round(x['s1'], 3)])

print('zdobycia fin30', len(res), '| wymiana E (mediana obl./obronce)', round(E, 2), 'z', len(ex), '| hazard konca bez szturmu', round(H, 4), '/dobe',
      '| powrot po porazce sila (<=120 dob)', back, '/', fail, '=', round(REB, 2))
print('stolice zdobyte', sum(x['cap'] for x in res), '| S1 (zaloga >= 50% obrony w bitwie ze szturmu)', sum(x['s1'] is not None for x in res))
print('fA zdobyc: kwartyle', [round(q, 3) for q in st.quantiles([x['fA'] for x in res if not x['story']], n=4)])
for L in L_LIST:
    kept, rep, r1s, r1l, back, times = scen(L)
    kT = kept * math.exp(-H * T1_DAYS); bT = REB * (len(res) - kT) * (kT / len(res))
    print(f'   wariant +T1: zdobyc {kT:.1f} + powrot {bT:.1f} = {kT+bT:.1f}, czas zdobycia mediana {st.median(times)+T1_DAYS:.1f}')
    increp = sum(1 for f in inc if f >= L)
    print(f'L={L:.2f}: zdobyc zostaje {kept:.1f} (+ powrot {back:.1f} = {kept+back:.1f}) | odparte zdobycia {rep} + bez rozstrzygniecia -> odparte {increp}/{len(inc)}'
          f' | R1 zatrzymuje {r1s:.1f} (pozniej {r1l}) | czas zdobycia mediana {st.median(times):.1f} p90 {st.quantiles(times, n=10)[-1]:.1f}')
caps = [x for x in res if x['cap']]
for L in L_LIST:
    keep = [x['name'] for x in caps if not ((x['mury'] is not None and x['mury'] <= 1) or (x['s1'] is not None and x['s1'] >= L) or x['fA'] >= L)]
    print(f'L={L:.2f}: stolice zdobyte nadal: {keep}')
