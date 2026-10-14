# -*- coding: utf-8 -*-
# Rozpoznanie OBLEZENIA - CZAS I PRZEBIEG (14.10). Tylko odczyt logow, wynik do robocze/czas.
# Uzycie: python -I oblezenia_czas.py <tag> <katalog_tabel> <wyciag_z_logu> <wyjscie_prefix>
import sys, re, csv, json, statistics
from collections import defaultdict

tag, tdir, extract, outp = sys.argv[1:5]

# ---------------------------------------------------------------- 1. spichlerze: oblezona / zywnosc na dobe
reg = {}                       # region_id -> (nazwa, rodzaj)
day_rows = defaultdict(dict)   # region_id -> dzien -> (oblezona, glodna, zywnosc, bilans, dni_U27)
with open(tdir + '/spichlerze-regiony.csv', encoding='utf-8', errors='replace') as f:
    r = csv.reader(f, delimiter=';')
    h = next(r)
    ix = {k: i for i, k in enumerate(h)}
    for row in r:
        try:
            d = int(row[ix['dzien']])
        except Exception:
            continue
        rid = row[ix['region_id']]
        reg[rid] = (row[ix['region']], row[ix['rodzaj']])
        def fl(k):
            try: return float(row[ix[k]])
            except Exception: return None
        day_rows[rid][d] = (row[ix['oblezona']] == '1', row[ix['glodna_warownia']] == '1', fl('zywnosc'), fl('bilans_zywnosci'), fl('dni_zapasu_U27'))

days_all = sorted({d for m in day_rows.values() for d in m})
D0, D1 = days_all[0], days_all[-1]

# ---------------------------------------------------------------- 2. ludzie-regiony: pan, zaloga, milicja na dobe
own = defaultdict(dict)        # region_id -> dzien -> (krolestwo, rod, zaloga, milicja)
with open(tdir + '/ludzie-regiony.csv', encoding='utf-8', errors='replace') as f:
    r = csv.reader(f, delimiter=';')
    h = next(r)
    ix = {k: i for i, k in enumerate(h)}
    for row in r:
        try:
            d = int(row[ix['dzien']])
        except Exception:
            continue
        rid = row[ix['region_id']]
        if rid not in reg:
            continue
        def fl(k):
            try: return float(row[ix[k]])
            except Exception: return None
        own[rid][d] = (row[ix['krolestwo']], row[ix['rod']], fl('zaloga'), fl('milicja'))

# ---------------------------------------------------------------- 3. bitwy.log B175 pod murami
rx175 = re.compile(r'B175 dzien (\d+) \| (Siege|SallyOut|SiegeOutside|BlockadeBattle|BlockadeSallyOutBattle) \| kontekst (\w+) \| region (\S+) \(([^)]*)\) \| '
                   r'A: krol=(\S+) rodzaj=(\S+) partii (\d+) ludzi (\d+) moc ([\d.]+) .*? straty (\d+) \| '
                   r'O: krol=(\S+) rodzaj=(\S+) partii (\d+) ludzi (\d+) moc ([\d.]+) .*? straty (\d+) \| '
                   r'stosunek sil (\S+) \(([^)]*)\) \| wygrywa (\w+)(.*)$')
bat = []
with open(tdir + '/bitwy.log', encoding='utf-8', errors='replace') as f:
    for line in f:
        if 'B175 dzien' not in line:
            continue
        m = rx175.search(line.rstrip())
        if not m:
            continue
        g = m.groups()
        tail = g[20]
        mo = re.search(r'osada (\S+?)(?: \(zdobyta (\w+)\))?\.$', tail)
        osada = mo.group(1) if mo else g[3]
        zdob = mo.group(2) if mo and mo.group(2) else None
        try: ratio = float(g[17])
        except Exception: ratio = None
        bat.append(dict(day=int(g[0]), typ=g[1], region=g[3], osada=osada, zdobyta=zdob,
                        A_krol=g[5], A_rodzaj=g[6], A_partii=int(g[7]), A_ludzi=int(g[8]), A_moc=float(g[9]), A_straty=int(g[10]),
                        O_krol=g[11], O_rodzaj=g[12], O_partii=int(g[13]), O_ludzi=int(g[14]), O_moc=float(g[15]), O_straty=int(g[16]),
                        ratio=ratio, ocena=g[18], wygrywa=g[19]))

# ---------------------------------------------------------------- 4. wyciag z logu Armoury
rxday = re.compile(r'dzien (\d{6})')
rxO0 = re.compile(r'Okup oblezenia \(O0\): dzien (\d+) \| osada (\S+) \((.*?), (miasto|zamek), krolestwo (\S+), rod (\S+)\) \| oblegajacy: frakcja (\S+), wodz (.*?) \(armia (tak|nie)\), '
                  r'druzyn (\d+) \(ludzi (\d+), Inni (\d+)\), oblezenie ([\d.]+) dob, szturmow bez zdobycia (\d+) .*?\| stosunek gry (pusta warownia|[\d.]+) '
                  r'\(oblegajacy (\d+) / obroncy (\d+) x przewaga ([\d.]+)\^(-?[\d.]+); maszyny (\d+)(, taran)?(, wieza)?; szansa na mysl ([\d.]+)%\) \| mury (pusta warownia|[\d.]+) .*\| wynik (.*)\.$')
rxO1 = re.compile(r'Okup oblezenia \(O1\): dzien (\d+) \| osada (\S+) \((.*?), (miasto|zamek)\) \| rodzaj (.*?) \| placi: krolestwo (\S+), .*?\| bierze: frakcja (\S+), wodz (.*?) \| powod: (.*?) \| przy decyzji: okup (\d+)')
rxCF = re.compile(r'CampFever: oblezenie (\S+) dzien (\d+) - zaraza wstaje')
rxCF2 = re.compile(r'CampFever: (\S+) dzien (\d+) - oblegajacy (\d+) chorych/(\d+) zmarlych, obroncy (\d+)/(\d+)')
rxL0 = re.compile(r'Lup miast \(L0\): dzien (\d+) \| osada (\S+) \((.*?), (miasto|zamek)\) \| bitwa (\S+) \| zdobycie (\S+)')
rxL5 = re.compile(r'Karawany w zdobytym miescie \(L5b-2\): dzien (\d+) \| osada (.*?) \((\S+)\) zdobyta przez (.*?) \((.*?), wodz')
rxA11 = re.compile(r'Karawany BK przy koncu oblezenia \(A11b\): dzien (\d+) godz\. (\d+) \| koniec oblezenia (.*?) \((\S+)\)')
rxObl = re.compile(r'Oblezone \(E5/A4\): dzien (\d+) .*?oblezone teraz: miasta (\d+), zamki (\d+)')
O0, O1, CF, CF2, L0, L5, A11, OBL = [], [], [], [], [], [], [], []
cur = None
with open(extract, encoding='utf-8', errors='replace') as f:
    for line in f:
        line = line.rstrip()
        m = rxObl.search(line)
        if m:
            cur = int(m.group(1)); OBL.append((cur, int(m.group(2)), int(m.group(3)))); continue
        m = rxO0.search(line)
        if m:
            g = m.groups()
            O0.append(dict(day=int(g[0]), osada=g[1], nazwa=g[2], rodzaj=g[3], krol=g[4], frakcja=g[6], wodz=g[7], armia=g[8], druzyn=int(g[9]), ludzi=int(g[10]),
                           dob=float(g[12]), bez_zdob=int(g[13]), stosunek=(None if g[14] == 'pusta warownia' else float(g[14])), att=int(g[15]), dfn=int(g[16]),
                           adv=float(g[17]), w=float(g[18]), maszyny=int(g[19]), taran=bool(g[20]), wieza=bool(g[21]), szansa=float(g[22]),
                           mury=(None if g[23] == 'pusta warownia' else float(g[23])), wynik=g[24]))
            continue
        m = rxO1.search(line)
        if m:
            g = m.groups()
            O1.append(dict(day=int(g[0]), osada=g[1], nazwa=g[2], rodzaj=g[3], typ=g[4], placi=g[5], bierze=g[6], wodz=g[7], powod=g[8], okup=int(g[9])))
            continue
        m = rxCF.search(line)
        if m:
            CF.append(dict(day=cur, osada=m.group(1), siege_day=int(m.group(2)))); continue
        m = rxCF2.search(line)
        if m:
            CF2.append(dict(day=cur, osada=m.group(1), siege_day=int(m.group(2)), chorzy=int(m.group(3)), zmarli=int(m.group(4)), ob_ch=int(m.group(5)), ob_zm=int(m.group(6)))); continue
        m = rxL0.search(line)
        if m:
            L0.append(dict(day=int(m.group(1)), osada=m.group(2), nazwa=m.group(3), rodzaj=m.group(4), bitwa=m.group(5), zdobycie=m.group(6))); continue
        m = rxL5.search(line)
        if m:
            L5.append(dict(day=int(m.group(1)), nazwa=m.group(2), osada=m.group(3), przez=m.group(4), frakcja=m.group(5))); continue
        m = rxA11.search(line)
        if m:
            A11.append(dict(day=int(m.group(1)), godz=int(m.group(2)), nazwa=m.group(3), osada=m.group(4))); continue

# ---------------------------------------------------------------- 5. epizody oblezen (ciagi dob z oblezona=1)
eps = []
for rid, m in day_rows.items():
    ds = sorted(m)
    run = None
    for d in ds:
        if m[d][0]:
            if run and d == run[-1] + 1:
                run.append(d)
            else:
                if run: eps.append((rid, run))
                run = [d]
        else:
            if run: eps.append((rid, run)); run = None
    if run: eps.append((rid, run))

def owner(rid, d):
    return own.get(rid, {}).get(d)

out = []
for rid, run in eps:
    s, e = run[0], run[-1]
    name, kind = reg[rid]
    rows = [day_rows[rid][d] for d in run]
    food0 = rows[0][2]; bil0 = rows[0][3]
    fmin = min((x[2] for x in rows if x[2] is not None), default=None)
    glod = sum(1 for x in rows if x[1])
    o_before = owner(rid, s - 1) or owner(rid, s)
    o_after = owner(rid, e + 1)
    zal0 = (owner(rid, s) or (None, None, None, None))
    # bitwy w oknie [s-1, e+1] dla tej osady
    bb = [b for b in bat if (b['osada'] == rid or b['region'] == rid) and s <= b['day'] <= e + 2]
    assaults = [b for b in bb if b['typ'] == 'Siege']
    sallies = [b for b in bb if b['typ'] in ('SallyOut', 'BlockadeSallyOutBattle')]
    outside = [b for b in bb if b['typ'] == 'SiegeOutside']
    o0 = [x for x in O0 if x['osada'] == rid and s <= x['day'] <= e + 2]
    o1 = [x for x in O1 if x['osada'] == rid and s <= x['day'] <= e + 2]
    l0 = [x for x in L0 if x['osada'] == rid and s <= x['day'] <= e + 2]
    cf = [x for x in CF if x['osada'] == rid and x['day'] is not None and s <= x['day'] <= e + 2]
    captured = bool(l0) or any(b['zdobyta'] == 'tak' for b in assaults)
    kchange = (o_before is not None and o_after is not None and o_before[0] != o_after[0])
    if captured:
        how = 'zdobyte szturmem' if any(x['bitwa'] == 'szturm' for x in l0) or any(b['zdobyta'] == 'tak' for b in assaults) else 'zdobyte po wypadzie'
    elif o1:
        how = 'okup (O1)'
    elif any(b['typ'] in ('SallyOut', 'BlockadeSallyOutBattle') and b['wygrywa'] == 'A' for b in sallies):
        how = 'wypad - pobity oblegajacy'
    elif any((b['O_rodzaj'] == 'zaloga' and b['wygrywa'] == 'O') or (b['O_rodzaj'] != 'zaloga' and b['wygrywa'] == 'A') for b in outside):
        how = 'pod murami - pobity oblegajacy'
    elif kchange:
        how = 'zmiana pana bez szturmu'
    elif assaults:
        how = 'szturm bez zdobycia, potem odstapione'
    elif e == D1:
        how = 'trwa na koncu biegu'
    else:
        how = 'odstapione bez bitwy'
    first_ass = min((b['day'] for b in assaults), default=None)
    out.append(dict(region=rid, nazwa=name, rodzaj=kind, start=s, koniec=e, dob=len(run), wynik=how, kchange=kchange,
                    pan_przed=(o_before[0] if o_before else None), pan_po=(o_after[0] if o_after else None),
                    zaloga0=zal0[2], milicja0=zal0[3], zywnosc0=food0, bilans0=bil0, zywnosc_min=fmin, dob_glodu=glod,
                    dni_jedzenia0=(round(food0 / -bil0, 1) if food0 is not None and bil0 is not None and bil0 < 0 else None),
                    szturmy=len(assaults), szturm_dni=[b['day'] - s for b in assaults], szturm_wyn=[b['wygrywa'] + ('/' + (b['zdobyta'] or '-')) for b in assaults],
                    szturm_ratio=[round(b['A_moc'] / b['O_moc'], 2) if b['O_moc'] > 0 else None for b in assaults], szturm_A=[b['A_ludzi'] for b in assaults], szturm_O=[b['O_ludzi'] for b in assaults],
                    szturm_Astr=[b['A_straty'] for b in assaults], szturm_Ostr=[b['O_straty'] for b in assaults],
                    wypady=[(b['day'] - s, b['wygrywa'], b['A_ludzi'], b['O_ludzi']) for b in sallies],
                    pod_murami=[(b['day'] - s, b['wygrywa'], b['A_ludzi'], b['O_ludzi'], b['O_rodzaj']) for b in outside],
                    l0=[x['day'] for x in l0],
                    o0=[(x['day'] - s, x['dob'], x['stosunek'], x['mury'], x['adv'], x['w'], x['maszyny'], x['taran'], x['wieza'], x['szansa'], x['att'], x['dfn'], x['wynik'][:40]) for x in o0],
                    okup=[(x['day'] - s, x['okup'], x['powod']) for x in o1],
                    campfever=[x['siege_day'] for x in cf],
                    pierwszy_szturm=(first_ass - s if first_ass is not None else None)))

# zdobycia bez epizodu (krotsze niz doba - miedzy migawkami)
caught = set()
for o in out:
    for b in bat:
        pass
cap_days = defaultdict(list)
for x in L0:
    cap_days[x['osada']].append(x['day'])
lost = []
for rid, dl in cap_days.items():
    for d in dl:
        if not any(o['region'] == rid and o['start'] - 1 <= d <= o['koniec'] + 2 for o in out):
            ass = [b for b in bat if b['typ'] == 'Siege' and b['osada'] == rid and abs(b['day'] - d) <= 1]
            lost.append(dict(region=rid, nazwa=reg.get(rid, ('?', '?'))[0], rodzaj=reg.get(rid, ('?', '?'))[1], dzien=d,
                             A=[b['A_ludzi'] for b in ass], O=[b['O_ludzi'] for b in ass], ratio=[round(b['A_moc'] / b['O_moc'], 2) if b['O_moc'] > 0 else None for b in ass]))

json.dump(dict(tag=tag, D0=D0, D1=D1, epizody=out, zdobycia_bez_epizodu=lost, O0=O0, O1=O1, CF=CF, CF2=CF2, L0=L0, L5=L5, A11=A11,
               bitwy=[b for b in bat], OBL=OBL), open(outp + '.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=0)

# ---------------------------------------------------------------- 6. podsumowanie na ekran
def q(v, p):
    v = sorted(v)
    if not v: return None
    k = (len(v) - 1) * p
    lo = int(k); hi = min(lo + 1, len(v) - 1)
    return round(v[lo] + (v[hi] - v[lo]) * (k - lo), 1)
print('==', tag, 'doby', D0, '-', D1, '(', D1 - D0 + 1, ')')
print('epizodow oblezen (migawka doby):', len(out), ' zdobyc z L0:', len(L0), ' zdobycia bez epizodu (oblezenie < 1 doby):', len(lost))
by = defaultdict(list)
for o in out:
    by[(o['rodzaj'], o['wynik'])].append(o['dob'])
for k in sorted(by):
    v = by[k]
    print(' %-8s %-42s n=%3d  dob: mediana %s p90 %s max %s suma %d' % (k[0], k[1], len(v), q(v, .5), q(v, .9), max(v), sum(v)))
for kind in ('miasto', 'zamek'):
    v = [o['dob'] for o in out if o['rodzaj'] == kind]
    print(' %s: n=%d mediana %s p90 %s max %s suma dob %d' % (kind, len(v), q(v, .5), q(v, .9), max(v) if v else None, sum(v)))
v = [o['dob'] for o in out]
print(' wszystkie: n=%d mediana %s p75 %s p90 %s max %s' % (len(v), q(v, .5), q(v, .75), q(v, .9), max(v)))
cap = [o for o in out if o['wynik'].startswith('zdobyte')]
print(' zdobyte: n=%d  dob do konca: mediana %s p90 %s max %s' % (len(cap), q([o['dob'] for o in cap], .5), q([o['dob'] for o in cap], .9), max([o['dob'] for o in cap]) if cap else None))
fa = [o['pierwszy_szturm'] for o in out if o['pierwszy_szturm'] is not None]
print(' pierwszy szturm (doba od startu migawki): n=%d mediana %s p10 %s p90 %s min %s max %s' % (len(fa), q(fa, .5), q(fa, .1), q(fa, .9), min(fa) if fa else None, max(fa) if fa else None))
print(' szturmy B175 Siege:', sum(1 for b in bat if b['typ'] == 'Siege'), ' wygrywa:', {w: sum(1 for b in bat if b['typ'] == 'Siege' and b['wygrywa'] == w) for w in ('A', 'O', 'brak')})
print(' wypady:', sum(1 for b in bat if b['typ'] in ('SallyOut', 'BlockadeSallyOutBattle')), {w: sum(1 for b in bat if b['typ'] in ('SallyOut', 'BlockadeSallyOutBattle') and b['wygrywa'] == w) for w in ('A', 'O', 'brak')})
print(' pod murami:', sum(1 for b in bat if b['typ'] == 'SiegeOutside'), {w: sum(1 for b in bat if b['typ'] == 'SiegeOutside' and b['wygrywa'] == w) for w in ('A', 'O', 'brak')})
print(' O0 linii', len(O0), ' O1 okupow', len(O1), ' CampFever zaraza wstaje', len(CF))
