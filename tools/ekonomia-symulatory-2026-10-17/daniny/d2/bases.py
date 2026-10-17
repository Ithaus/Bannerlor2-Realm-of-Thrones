# D2 (projekt 2: robocizna i danina w naturze) - podstawy na krolestwo z CSV testu KS r3 (06-07-51), ostatnie 28 dob.
# Czyta tylko pliki z D:/Backup-Bannerlord/logi-testow/2026-10-17_06-07-51 i teksty rodzenstwa (kcrown.txt, kingdom_wages.txt).
import csv, json, re, sys
from collections import defaultdict

DIR = 'D:/Backup-Bannerlord/logi-testow/2026-10-17_06-07-51/'
SIB = 'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/daniny/'
D0, D1 = 109167, 109194

def rows(name):
    with open(DIR + name, encoding='utf-8', errors='replace') as f:
        r = csv.reader(f, delimiter=';')
        h = next(r)
        for x in r:
            if len(x) < len(h) - 2:
                continue
            yield dict(zip(h, x))

def fnum(s):
    try:
        return float(s)
    except Exception:
        return 0.0

K = defaultdict(lambda: defaultdict(float))

# 1. kasy miast: doplyw netto (zysk kupcow przed poborami), zawor pan/korona, kasa, zapas
days = set()
for x in rows('kasy-miast.csv'):
    d = int(x['dzien'])
    if d < D0 or d > D1:
        continue
    days.add(d)
    k = x['krolestwo'] or '(bez)'
    K[k]['N'] += fnum(x['doplyw_netto'])
    K[k]['valve_lord'] += fnum(x['zawor_do_pana'])
    K[k]['valve_crown'] += fnum(x['do_korony'])
    K[k]['taken'] += fnum(x['pobrane'])
    if d == D1:
        K[k]['coffer'] += fnum(x['kasa'])
        K[k]['reserve'] += fnum(x['zapas_kupcow'])
        K[k]['coffer_spare'] += max(0.0, fnum(x['kasa']) - fnum(x['zapas_kupcow']))
        K[k]['towns'] += 1
nd = len(days)
for k in K:
    for m in ('N', 'valve_lord', 'valve_crown', 'taken'):
        K[k][m] /= nd

# 2. kiesa-obieg: zarobek ludu (okno 7 dob), danina P1, kiesy ponad zapas - miasta i zamki, 4 ostatnie tygodnie
wk = set()
for x in rows('kiesa-obieg.csv'):
    d = int(x['dzien'])
    if d < D0 or d > D1:
        continue
    wk.add(d)
    k = x['krolestwo'] or '(bez)'
    n = max(1.0, fnum(x['dob_okna']))
    kind = 'T' if x['rodzaj'] == 'miasto' else 'C'
    K[k]['earn_' + kind] += fnum(x['zarobek_okna']) / n
    K[k]['p1_' + kind] += fnum(x['danina_okna']) / n
    if d == max(wk):
        pass
last_wk = max(wk)
for x in rows('kiesa-obieg.csv'):
    if int(x['dzien']) != last_wk:
        continue
    k = x['krolestwo'] or '(bez)'
    K[k]['over_res'] += fnum(x['ponad_zapas_W']) + fnum(x['ponad_zapas_R']) + fnum(x['ponad_zapas_P'])
    K[k]['folk_purse'] += fnum(x['kiesa_W']) + fnum(x['kiesa_R']) + fnum(x['kiesa_P'])
nw = len(wk)
for k in K:
    for m in ('earn_T', 'earn_C', 'p1_T', 'p1_C'):
        K[k][m] /= nw

# 3. wsie: ks-budzety (srednie 28 dob utargu oddanego i do kiesy) + plon-wsi (krolestwo, ludzie)
vk, vp = {}, {}
for x in rows('plon-wsi.csv'):
    if int(x['dzien']) == 109179 or x['wies_id'] not in vk:
        vk[x['wies_id']] = x['krolestwo'] or '(bez)'
        vp[x['wies_id']] = fnum(x['ludzie'])
vil = []
for x in rows('ks-budzety.csv'):
    if int(x['dzien']) != 109179 or x['rodzaj'] != 'wies':
        continue
    h = fnum(x['utarg_28d_zl_d']) if x['utarg_28d_zl_d'] not in ('-', '') else 0.0
    kk = fnum(x['do_kiesy_28d_zl_d']) if x['do_kiesy_28d_zl_d'] not in ('-', '') else 0.0
    k = vk.get(x['osada_id'], '(bez)')
    K[k]['handed'] += h
    K[k]['to_purse'] += kk
    K[k]['villages'] += 1
    K[k]['vil_people'] += vp.get(x['osada_id'], 0.0)
    vil.append((x['osada_id'], k, h, kk, vp.get(x['osada_id'], 0.0)))

# 4. rody: G, R, d_ziemia, d_korona, zold - srednie 28 dob; G-R z ostatniej doby
gr = defaultdict(float)
for x in rows('budzet-rodow.csv'):
    d = int(x['dzien'])
    if x['rodzaj'] in ('gracz',) or d < D0 or d > D1:
        continue
    k = x['krolestwo'] or '(bez)'
    if d == D1:
        G, R = fnum(x['G']), fnum(x['R'])
        K[k]['G'] += G
        K[k]['G_over_R'] += max(0.0, G - R)
        K[k]['houses'] += 1
        K[k]['debt_crown_house'] += fnum(x['dlug_korona'])
        K[k]['own'] += fnum(x['sr_wlasne'])
        K[k]['d_staly'] += fnum(x['d_staly'])
        K[k]['pulap'] += fnum(x['pulap'])
        K[k]['wages_now'] += fnum(x['zold_partii']) + fnum(x['zold_zalog'])

# 5. teksty rodzenstwa: korona i rody na krolestwo (KS, ostatnie 28 dob)
def parse_block(path, tag, cols):
    out = {}
    txt = open(path, encoding='utf-8').read().splitlines()
    on = False
    for ln in txt:
        if ln.startswith('===') or ln.startswith('ks days') or ln.startswith('jeff days') or ln.startswith('base'):
            on = (tag in ln)
            continue
        if not on or ln.startswith('krolestwo') or ln.startswith('RAZEM') or ln.startswith('kingdom-days'):
            continue
        m = re.match(r'^(.{34,42}?)\s+(-?\d.*)$', ln)
        if not m:
            continue
        name = m.group(1).strip()
        nums = re.findall(r'-?\d+', m.group(2).replace('|', ' '))
        out[name] = [float(v) for v in nums]
    return out

kc = parse_block(SIB + 'miasta-korona/kcrown.txt', 'ks crown', None)
kw = parse_block(SIB + 'kingdom_wages.txt', 'ks days', None)
CC = ['wpl', 'zapas', 'P1', 't184', 'zwrot', 'renty', 'W5', 'W5b', 'kred', 'dKred', 'dRent0', 'dShort', 'skarb0', 'skarbEnd', 'dlug186', 'wojsko', 'minus']
WC = ['n', 'wojna', 'zold_p', 'zold_z', 'D', 'd_ziemia', 'd_korona', 'zwrot', 'renta180', 'w5', 'w5b', 'm1', 'w7', 'G', 'G_lt_R', 'dlug_bank', 'ludzi_p', 'limit_p']
for name, v in kc.items():
    for i, c in enumerate(CC):
        if i < len(v):
            K[name]['c_' + c] = v[i]
for name, v in kw.items():
    for i, c in enumerate(WC):
        if i < len(v):
            K[name]['w_' + c] = v[i]

json.dump({k: dict(v) for k, v in K.items()}, open(SIB + 'd2/bases.json', 'w'), indent=1)
json.dump(vil, open(SIB + 'd2/villages.json', 'w'))
tot = defaultdict(float)
for k, v in K.items():
    for m, x in v.items():
        tot[m] += x
print('days kasy', nd, 'weeks obieg', nw, 'villages', len(vil))
for m in sorted(tot):
    print('%-18s %14.0f' % (m, tot[m]))
