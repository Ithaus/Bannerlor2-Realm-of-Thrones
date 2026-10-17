"""KS-R4 koszt: projekcja NKS-9 / NKS-9b / NKS-28 po cieciach (szacunki ms na dobe; dane dob z logu rundy 3).
uzycie: python -I cost_whatif.py <Armoury-*.log>
Szacunki (low, mid, high) - z kodu i mikro-pomiaru bench/KsBench.cs (r4). Wszystko odejmowane od zmierzonych liczb kazdej doby, z podloga 0.15 x wartosci.
"""
import re, sys

log = sys.argv[1]
rk = re.compile(r"Kalibracja startu \(KS\): dzien (\d+) \| stan (\S+).*?\| Plan: (w toku|czeka).*?czas doby KS ([\d.]+) ms.*?tabela dob KS-H ([\d.]+) ms")
rz = re.compile(r"Koszyk stanow \(ZC4\): dzien (\d+) \|.*?czas hakow od ostatniej linii: popyt ceny ([\d.]+) ms \((\d+) wywolan\), slowniki budzetu ([\d.]+) ms, drabina ([\d.]+) ms, zakupy wsi ([\d.]+) ms, przetwory ZC5 ([\d.]+) ms, razem ([\d.]+) ms")
rh = re.compile(r"Kupcy zywnosci \(KS-H\) dzien (\d+) \| CZYNNE .*?\| czas ([\d.]+) ms")
rr = re.compile(r"KS: rampa dzien \d+ \| doba rampy \d+ \(A (\d+), D (\d+)")
ks, zc, kh = {}, {}, {}
A = D = None
for line in open(log, encoding='utf-8', errors='replace'):
    if 'Kalibracja startu (KS): dzien' in line:
        m = rk.search(line)
        if m: ks[int(m.group(1))] = dict(state=m.group(2), plan=m.group(3), ms=float(m.group(4)), daytab=float(m.group(5)))
    elif 'Koszyk stanow (ZC4): dzien' in line:
        m = rz.search(line)
        if m:
            g = m.groups(); zc[int(g[0])] = dict(popyt=float(g[1]), slown=float(g[3]), drab=float(g[4]), wsie=float(g[5]), zc5=float(g[6]))
    elif 'Kupcy zywnosci (KS-H) dzien' in line:
        m = rh.search(line)
        if m: kh[int(m.group(1))] = float(m.group(2))
    elif A is None and 'KS: rampa dzien' in line:
        m = rr.search(line)
        if m: A, D = int(m.group(1)), int(m.group(2))

def cls(d):
    t = d + 1; c = ''
    if ks[d]['plan'] == 'w toku' and (d - 1 not in ks or ks[d - 1]['plan'] != 'w toku'): c += 'P'
    if d - 1 in ks and ks[d - 1]['plan'] == 'w toku' and ks[d]['plan'] == 'czeka': c += 'C'
    if t > D and (t - D) % 7 == 0: c += 'S'
    return c

RAMP_END = D + 14            # t >= RampDemand (A + 28)
CTL_END = D + 49
app = sorted(d for d in ks if ks[d]['state'] == 'ZASTOSOWANY')
first_apply = app[0]

# ciecia [low, mid, high] ms na dobe
CUT = {
    # doba KS (StartCalibration.Daily)
    'K1_ramp_after_end': (0.20, 0.30, 0.40),      # po koncu rampy; w rampie tablica RampZ (0.8 x)
    'K2_zc4build_scan': (0.30, 0.40, 0.50),
    'K3_shelfline_ix': (0.05, 0.08, 0.12),        # tylko linia (LogGate.Lines); GetBasePriceFactor ok. 35-45 ns (RawPrice.cs:92)
    'K4a_rule_active': (0.08, 0.12, 0.20),
    'K4b_rule_income_S': (3.2, 3.6, 3.9),         # doby kroku A7
    'K5_snapshot_pool': (0.05, 0.08, 0.12),
    'K6_produce_arrays': (0.0, 0.0, 0.0),        # P0 juz tablice - bez ciecia (zmierzyc stoperem)
    'K8_lines_offthread': (0.08, 0.12, 0.15),
    'B1_fields_every2': (0.30, 0.37, 0.45),        # pola mieszczan co 2 doby partia 2 dob (ok. 550 przelewow + AddToCounts/d) - nie bit w bit
    'K7_floor_lines': (0.05, 0.10, 0.15),
    'C1_planlines_worker': (17.0, 20.0, 22.0),    # doba odbioru Planu
    'C34_refresh_lines_worker': (0.6, 1.0, 1.5),
    # haki
    'W1_neartowns_cache': None,                   # zmierzone: nadwyzka dob 4 i 5 tygodnia (dzien % 7) ponad mediane innych dob
    'W2_ztown_memo': (0.45, 0.65, 0.85),
    'W3_factor_cache': (0.55, 0.85, 1.10),
    'W4_scratch': (0.05, 0.10, 0.15),
    'Z_zc5': (0.20, 0.28, 0.38),
    'S_dict_after_ctl': (0.30, 0.35, 0.40),       # po D+49
    'L_ladder_arrays': (0.05, 0.10, 0.12),
    'H_ksh': (0.25, 0.35, 0.45),
}

BATCH = False
def project(ix):
    k_ms, tot, khs = [], [], []
    import statistics as st
    # nadwyzka wsi w doby przebudowy NearTowns (dzien % 7 in 4,5 w tym biegu) - z danych
    base_w = st.median([zc[d]['wsie'] for d in app if d in zc and d >= RAMP_END and d % 7 not in (4, 5)])
    for d in app:
        c = cls(d)
        x = ks[d]['ms']
        cut = 0.0
        for k in ('K2_zc4build_scan', 'K3_shelfline_ix', 'K4a_rule_active', 'K5_snapshot_pool', 'K6_produce_arrays', 'K7_floor_lines', 'K8_lines_offthread') + (('B1_fields_every2',) if BATCH else ()):
            cut += CUT[k][ix]
        cut += CUT['K1_ramp_after_end'][ix] * (1.0 if d >= RAMP_END else 0.8)
        if 'S' in c: cut += CUT['K4b_rule_income_S'][ix]
        if 'C' in c and d > first_apply + 1: cut += CUT['C1_planlines_worker'][ix] + CUT['C34_refresh_lines_worker'][ix]
        y = max(0.15 * x, x - cut)
        k_ms.append(y)
        if d in zc:
            z = zc[d]
            w = z['wsie']
            if d >= RAMP_END and d % 7 in (4, 5) and w > base_w: w = base_w + 0.0
            w = max(0.15 * w, w - CUT['W2_ztown_memo'][ix] - CUT['W3_factor_cache'][ix] - CUT['W4_scratch'][ix]) if w > 0.5 else w
            s_ = max(0.15 * z['slown'], z['slown'] - (CUT['S_dict_after_ctl'][ix] if d > CTL_END else 0.0))
            dr = max(0.15 * z['drab'], z['drab'] - CUT['L_ladder_arrays'][ix])
            c5 = max(0.15 * z['zc5'], z['zc5'] - CUT['Z_zc5'][ix]) if z['zc5'] > 0.2 else z['zc5']
            h = kh.get(d, 0.0)
            h2 = max(0.15 * h, h - CUT['H_ksh'][ix]) if h > 0 else 0.0
            tot.append(y + z['popyt'] + s_ + dr + w + c5 + h2 + ks[d]['daytab'])
            if d in kh: khs.append(h2)
    def q(v, p): v = sorted(v); return v[min(len(v) - 1, int(p * (len(v) - 1)))]
    return q(k_ms, .5), q(k_ms, .95), (sorted(tot)[len(tot) // 2] if tot else float('nan')), (sorted(khs)[len(khs) // 2] if khs else float('nan'))

print('A', A, 'D', D, 'koniec rampy', RAMP_END, 'koniec sterowania', CTL_END, 'dob zastosowanych', len(app))
for b in (False, True):
    BATCH = b
    print('--- ciecia dokladne (bit w bit)' if not b else '--- + B1 pola mieszczan co 2 doby (partia)')
    for ix, nm in enumerate(('low', 'mid', 'high')):
        m, q95, t, h = project(ix)
        print('%-4s: NKS-9 mediana %.2f ms (prog 3), q95 %.2f ms (prog 15) | NKS-9b mediana %.2f ms (prog 10) | NKS-28 KS-H mediana %.2f ms (prog 1.5)' % (nm, m, q95, t, h))
