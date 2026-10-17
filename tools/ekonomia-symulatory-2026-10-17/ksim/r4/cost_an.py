"""KS-R4 koszt: rozklad czasu doby KS, hakow ZC4/ZC5 i KS-H z linii logu (tylko odczyt logu).
uzycie: python -I cost_an.py <Armoury-*.log>
"""
import re, sys, statistics as st

log = sys.argv[1]
rk = re.compile(r"Kalibracja startu \(KS\): dzien (\d+) \| stan (\S+).*?\| Plan: (w toku|czeka).*?czas doby KS ([\d.]+) ms.*?tabela dob KS-H ([\d.]+) ms")
rz = re.compile(r"Koszyk stanow \(ZC4\): dzien (\d+) \|.*?wsie: zakupow (\d+) za (\d+) zl, sztuk (\d+).*?miasta: slowniki budzetu (\d+).*?drabina \(kiesy nie stac na cale jedzenie\) (\d+).*?czas hakow od ostatniej linii: popyt ceny ([\d.]+) ms \((\d+) wywolan\), slowniki budzetu ([\d.]+) ms, drabina ([\d.]+) ms, zakupy wsi ([\d.]+) ms, przetwory ZC5 ([\d.]+) ms, razem ([\d.]+) ms \(w dobie KS: mapa koszyka ([\d.]+) ms.*?potrzeby ([\d.]+) ms w (\d+) miastach.*?ceny i polka ([\d.]+) ms")
rh = re.compile(r"Kupcy zywnosci \(KS-H\) dzien (\d+) \| CZYNNE \| wozow w (\d+) \(lad (\d+), morze (\d+)\), kupione (\d+) szt.*?dowiezione (\d+),.*?w drodze (\d+) szt\. w (\d+) wozach.*?kupcy: dowiezione loty (\d+).*?pominiete bez zysku (\d+).*?\| czas ([\d.]+) ms")
rr = re.compile(r"KS: rampa dzien \d+ \| doba rampy \d+ \(A (\d+), D (\d+)")
rp = re.compile(r"Kalibracja startu \(KS\): plan doba (\d+) \|.*?czas ([\d.]+) s \(watek roboczy\).*?zrzut (\d+) ms")
rprod = re.compile(r"KS: produkcja \(zastosowane\) dzien (\d+) .*?przeliczone dzis (\d+)")
rcsv = re.compile(r"\[[\d:]+\] KS: (ks-plan\.csv|ks-budzety\.csv|ks-miasta\.csv|ks-miasta-towary\.csv) (\d+) wierszy \(doba (\d+)")
rprof = re.compile(r"Profil doby \(E8/0\): dzien (\d+) \| takt doby ([\d.]+) ms.*?TownFolk\.Daily ([\d.]+)")
ks, zc, kh, prod, plans, csv, prof = {}, {}, {}, {}, {}, [], {}
A = D = None
cur_day = None
for line in open(log, encoding='utf-8', errors='replace'):
    if 'Kalibracja startu (KS): dzien' in line:
        m = rk.search(line)
        if m: ks[int(m.group(1))] = dict(state=m.group(2), plan=m.group(3), ms=float(m.group(4)), daytab=float(m.group(5)))
        continue
    if 'Koszyk stanow (ZC4): dzien' in line:
        m = rz.search(line)
        if m:
            g = m.groups()
            zc[int(g[0])] = dict(vbuy=int(g[1]), dictN=int(g[4]), ladN=int(g[5]), popyt=float(g[6]), popN=int(g[7]), slown=float(g[8]), drab=float(g[9]), wsie=float(g[10]), zc5=float(g[11]), razem=float(g[12]), mapa=float(g[13]), needs=float(g[14]), redo=int(g[15]), prices=float(g[16]))
        continue
    if 'Kupcy zywnosci (KS-H) dzien' in line:
        m = rh.search(line)
        if m:
            g = m.groups()
            kh[int(g[0])] = dict(lots=int(g[1]), land=int(g[2]), sea=int(g[3]), bought=int(g[4]), arr=int(g[5]), transit=int(g[6]), carts=int(g[7]), arrLots=int(g[8]), noprofit=int(g[9]), ms=float(g[10]))
        continue
    if A is None and 'KS: rampa dzien' in line:
        m = rr.search(line)
        if m: A, D = int(m.group(1)), int(m.group(2))
        continue
    if 'plan doba' in line and 'Kalibracja startu' in line:
        m = rp.search(line)
        if m: plans[int(m.group(1))] = (float(m.group(2)), int(m.group(3)))
        continue
    if 'KS: produkcja (zastosowane)' in line:
        m = rprod.search(line)
        if m: prod[int(m.group(1))] = int(m.group(2))
        continue
    m = rcsv.search(line)
    if m: csv.append((m.group(1), int(m.group(2)), int(m.group(3)), line[1:9])); continue
    if 'Profil doby (E8/0)' in line:
        m = rprof.search(line)
        if m: prof[int(m.group(1))] = (float(m.group(2)), float(m.group(3)))

def q(v, p):
    v = sorted(v); return v[min(len(v) - 1, int(p * (len(v) - 1)))] if v else float('nan')
def s(v):
    v = list(v)
    if not v: return '-'
    return 'n%d med %.2f mean %.2f q90 %.2f q95 %.2f max %.2f' % (len(v), q(v, .5), sum(v) / len(v), q(v, .9), q(v, .95), v and max(v))

print('log', log)
print('A', A, 'D', D, 'plans', sorted(plans)[:3], '...', len(plans))
app = sorted(d for d in ks if ks[d]['state'] == 'ZASTOSOWANY')
print('KS applied', s(ks[d]['ms'] for d in app))
# klasy dob: P = doba startu Planu (w toku), C = doba odbioru Planu (dzien po starcie), S = krok reguly A7 (today - D) % 7 == 0, R = RegCycle 28, W = TownsCsv/GoodsCsv (day % 7 == 0)
def cls(d):
    t = d + 1
    c = ''
    if ks[d]['plan'] == 'w toku' and (d - 1 not in ks or ks[d - 1]['plan'] != 'w toku'): c += 'P'
    if d - 1 in ks and ks[d - 1]['plan'] == 'w toku' and ks[d]['plan'] == 'czeka': c += 'C'
    if D is not None and t > D and (t - D) % 7 == 0: c += 'S'
    if D is not None and t > D and (t - D) % 28 == 0: c += 'R'
    if d % 7 == 0 and D is not None and t >= D: c += 'W'
    return c or 'o'
groups = {}
for d in app:
    groups.setdefault(cls(d), []).append(d)
for c in sorted(groups):
    v = [ks[d]['ms'] for d in groups[c]]
    print(' class %-5s' % c, s(v))
steady = [d for d in app if D is not None and d >= D + 14]
print('steady (>= D+14):', len(steady))
ordn = [d for d in steady if cls(d) == 'o']
print('ordinary steady KS', s(ks[d]['ms'] for d in ordn))
print('ordinary steady KS - mapa', s(ks[d]['ms'] - zc[d]['mapa'] for d in ordn if d in zc))
for k in ('popyt', 'slown', 'drab', 'wsie', 'zc5', 'razem', 'mapa', 'needs', 'prices', 'vbuy', 'dictN', 'ladN', 'redo', 'popN'):
    print('  ZC4 %-7s' % k, s(zc[d][k] for d in steady if d in zc))
for k in ('ms', 'lots', 'land', 'sea', 'arrLots', 'noprofit', 'carts', 'bought'):
    print('  KS-H %-8s' % k, s(kh[d][k] for d in steady if d in kh))
# NKS-9b skladniki doby (mediana) i suma
tot = [ks[d]['ms'] + zc[d]['razem'] + kh.get(d, {'ms': 0})['ms'] + ks[d]['daytab'] for d in app if d in zc]
print('NKS-9b sum (applied days with ZC4)', s(tot))
# regresja wsie ~ vbuy
pairs = [(zc[d]['vbuy'], zc[d]['wsie']) for d in steady if d in zc and zc[d]['vbuy'] > 0]
if pairs:
    xs, ys = zip(*pairs)
    mx, my = st.mean(xs), st.mean(ys)
    b = sum((x - mx) * (y - my) for x, y in pairs) / max(1e-9, sum((x - mx) ** 2 for x in xs))
    print('wsie vs zakupow: mean zakupow %.0f, us/zakup %.2f, slope us/zakup %.2f, intercept %.2f ms' % (mx, 1000 * my / mx, 1000 * b, my - b * mx))
# KS-H ms ~ lots
pairs = [(kh[d]['lots'], kh[d]['ms']) for d in steady if d in kh]
if pairs:
    xs, ys = zip(*pairs)
    mx, my = st.mean(xs), st.mean(ys)
    b = sum((x - mx) * (y - my) for x, y in pairs) / max(1e-9, sum((x - mx) ** 2 for x in xs))
    print('KS-H ms vs lots: mean lots %.0f, slope us/lot %.2f, intercept %.2f ms (lots=0 base)' % (mx, 1000 * b, my - b * mx))
# wsie by weekday class (NearTowns clear every 7 days)
for r in range(7):
    v = [zc[d]['wsie'] for d in steady if d in zc and d % 7 == r]
    w = [kh[d]['ms'] for d in steady if d in kh and d % 7 == r]
    print('  day%%7==%d wsie %s | KS-H %s' % (r, s(v), s(w)))
print('plans (doba: watek s, zrzut ms):', {k: plans[k] for k in sorted(plans)})
print('csv:', csv)
print('profile takt', s(p[0] for p in prof.values()), '| TownFolk.Daily', s(p[1] for p in prof.values()))
# what-if: targets
def med_after(cut_o, cut_S, cut_P, cut_C):
    v = []
    for d in app:
        c = cls(d); x = ks[d]['ms']
        if 'C' in c: x -= cut_C
        elif 'P' in c: x -= cut_P
        if 'S' in c: x -= cut_S
        x -= cut_o
        v.append(max(0.1, x))
    return q(v, .5), q(v, .95)
for cuts in ((1.5, 0, 0, 0), (1.5, 4.0, 0, 0), (1.5, 4.0, 5.0, 14.0), (1.7, 4.0, 5.0, 16.0), (2.0, 4.0, 6.0, 18.0)):
    print('what-if cut ordinary/all %.1f, step %.1f, PlanStart %.1f, PlanCollect %.1f -> median %.2f q95 %.2f' % (cuts + med_after(*cuts)))
