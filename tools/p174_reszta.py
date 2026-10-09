# Paczka 174 (spec. rozdz. 1.3 i 6): reszta polek uzbrojenia w dziesiatkach dob - tylko odczyt logu Armoury.
# Zmiana polek bez koni ("Rynek broni: na polkach") = warsztaty + strzelarze + skup + nadwyzki i tabory zalog - ZakupyAI - zakupy notabli + RESZTA.
# Od 174.0 dodatkowo "reszta skorygowana" = reszta + odziez zjedzona przez mieszczan (linia "Uzbrojenie (ujscia 174)") - zmiana amunicji w karawanach;
# prog testu 174.0: |reszta skorygowana d31-40| <= 150 szt./d.
# Uzycie: python tools/p174_reszta.py <Armoury.log>
import re, sys

L = sys.argv[1]
cur = dict(w=0, s=0, skup=0, zal=0, zak=0, och=0, garment=0, ammo=None)
rows = []

def ints(p, t):
    m = re.search(p, t)
    return [int(x) for x in m.groups()] if m else None

for ln in open(L, encoding='utf-8', errors='replace'):
    m = re.search(r'\] (.*)$', ln.rstrip('\n'))
    if not m:
        continue
    t = m.group(1)
    if t.startswith('Rynek broni: na polkach'):
        body = re.search(r'\[(.*?)\]', t).group(1)
        d = {k: int(v) for k, v in re.findall(r'(\w+) (\d+)', body)}
        tot = sum(v for k, v in d.items() if k != 'Horse')
        rows.append(dict(tot=tot, by=d, ammo=cur['ammo'], **{k: cur[k] for k in ('w', 's', 'skup', 'zal', 'zak', 'och', 'garment')}))
        for k in ('w', 's', 'skup', 'zal', 'zak', 'och', 'garment'):
            cur[k] = 0
    elif t.startswith('Warsztaty: dzien'):
        x = ints(r'wykonano (\d+) szt', t); cur['w'] += x[0] if x else 0
    elif t.startswith('Strzelarze (172): dzien'):
        a = ints(r'strzaly: zrobiono (\d+) snopow', t); b = ints(r'belty: zrobiono (\d+) snopow', t)
        cur['s'] += (a[0] if a else 0) + (b[0] if b else 0)
        c = ints(r'w taborach karawan: strzaly (\d+), belty (\d+)', t)
        if c:
            cur['ammo'] = c[0] + c[1]
    elif t.startswith('Skup sprzetu (doba)'):
        for p in (r'lordowie AI \(SellItemsAction\) (\d+) szt', r'inne partie \(SellItemsAction\) (\d+) szt', r'sakiewki ludzi (\d+) szt', r'notable \(rzeczy ochotnikow\) (\d+) szt'):
            x = ints(p, t); cur['skup'] += x[0] if x else 0
    elif t.startswith('Zbrojownie zalog (171): dzien'):
        x = ints(r'nadwyzki zalog: sprzedalo \d+ z \d+ zalog w kolejce, (\d+) szt', t); cur['zal'] += x[0] if x else 0
        x = ints(r'tabor sprzedany (\d+) szt', t); cur['zal'] += x[0] if x else 0
    elif t.startswith('ZakupyAI: dzien'):
        x = ints(r'(\d+) szt\. kupionych', t); cur['zak'] += x[0] if x else 0
    elif t.startswith('Ochotnicy: dzien'):
        x = ints(r'awanse z kupionym sprzetem \d+ \((\d+) szt', t); cur['och'] += x[0] if x else 0
    elif t.startswith('Uzbrojenie (ujscia 174): dzien'):
        x = ints(r'odziez \(garment\) (-?\d+) szt', t); cur['garment'] += x[0] if x else 0

# interwaly: zmiana polek miedzy migawkami k-1 i k wobec przeplywow zapisanych przed migawka k
out = []
for k in range(1, len(rows)):
    r, p = rows[k], rows[k - 1]
    dpol = r['tot'] - p['tot']
    doplyw = r['w'] + r['s'] + r['skup'] + r['zal']
    odplyw = r['zak'] + r['och']
    reszta = dpol - doplyw + odplyw
    dammo = (r['ammo'] - p['ammo']) if (r['ammo'] is not None and p['ammo'] is not None) else 0
    skor = reszta + r['garment'] + dammo
    out.append((k + 1, dpol, doplyw, odplyw, reszta, dammo, r['garment'], skor))

def avg(a, b, i):
    s = [o[i] for o in out if a <= o[0] <= b]
    return sum(s) / len(s) if s else float('nan')

print('migawek', len(rows), 'interwalow', len(out))
for a, b in ((2, 40), (2, 10), (11, 20), (21, 30), (31, 40)):
    print('d%d-%d: zmiana polek %.0f, doplyw %.0f, odplyw %.0f, RESZTA %.0f, amunicja karawan %+.0f, odziez mieszczan %.0f, RESZTA SKORYGOWANA %.0f'
          % (a, b, avg(a, b, 1), avg(a, b, 2), avg(a, b, 3), avg(a, b, 4), avg(a, b, 5), avg(a, b, 6), avg(a, b, 7)))
if rows:
    print('amunicja w karawanach d1 -> koniec:', rows[0]['ammo'], '->', rows[-1]['ammo'])
