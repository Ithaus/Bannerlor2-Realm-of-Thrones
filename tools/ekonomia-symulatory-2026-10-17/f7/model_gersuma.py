# Model odtworzeniowy f7-kasy (gersuma z zapasu kas miast z KS, Armoury/src/DsTowns.cs) na danych testu fin91 (Armoury-2026-10-18_06-29-09):
# kasy-miast.csv (kasa, zapas kupcow, wplaty do pana i korony = firma), kiesa-obieg.csv (R/P ponizej pasma - "domy najpierw"), m6-fin91.csv (doba;M6 pelna;M6 zlecona z linii f5 -
# udzial M6, ktory naprawde schodzi z kasy). Stara kasa = z CSV; nowa = stara - skumulowana gersuma + M6 niewyplacone przy mniejszej nadwyzce (sprzezenie). Firma bez zmian
# (gersuma nie zmienia stawki), zaleglosci i oblezenia pominiete (rzadkie). Tylko narzedzie - nie czesc moda.
# Uzycie: python -I model_gersuma.py <katalog CSV fin91> m6-fin91.csv [udzial 0.5] [dni firmy w progu 28] [okno przegladu 14] [domy najpierw 1] [minimum okna 1]
#         [firma z ostatnich 7 dob 7] [rozlozona na okno 1]
import csv, sys, collections

L = sys.argv[1]; M6 = sys.argv[2]
SHARE = float(sys.argv[3]) if len(sys.argv) > 3 else 0.5
KD = float(sys.argv[4]) if len(sys.argv) > 4 else 28.0
DAYS = int(sys.argv[5]) if len(sys.argv) > 5 else 14
HOUSES = (sys.argv[6] if len(sys.argv) > 6 else '1') == '1'
FW = int(sys.argv[8]) if len(sys.argv) > 8 else 7
SPREAD = (sys.argv[9] if len(sys.argv) > 9 else '1') == '1'
cleft = collections.defaultdict(float); cper = collections.defaultdict(float); keepAt = {}
MINW = (sys.argv[7] if len(sys.argv) > 7 else '1') == '1'
smin = {}
FIX = 109131      # firmy ustalone (rampa F3 28 dob)
KS = 109126       # pierwsza doba f5 (miasta z regula 97)

km = list(csv.DictReader(open(L + '/kasy-miast.csv', encoding='utf-8'), delimiter=';'))
ko = list(csv.DictReader(open(L + '/kiesa-obieg.csv', encoding='utf-8'), delimiter=';'))
e = {}
for line in open(M6):
    d, f, z = line.strip().split(';'); f = int(f); z = int(z)
    e[int(d)] = (z / f) if f > 0 else 1.0
by = collections.defaultdict(dict)
for r in km: by[int(r['dzien'])][r['miasto_id']] = r
days = sorted(by)
kd = collections.defaultdict(dict)
for r in ko:
    if r['rodzaj'] == 'miasto': kd[int(r['dzien'])][r['osada_id']] = r
kdays = sorted(kd)

def poor(town, d):
    ds = [x for x in kdays if x <= d]
    if not ds: return False
    o = kd[ds[-1]].get(town)
    if o is None: return False
    kr = float(o['kiesa_R']); kp = float(o['kiesa_P']); nr = float(o['potrzeba_R']); np_ = float(o['potrzeba_P'])
    return (nr > 0 and kr < 21 * nr) or (np_ > 0 and kp < 30 * np_)

towns = sorted(by[days[-1]])
X = collections.defaultdict(float)          # dodatkowy odplyw skumulowany (gersuma - mniejsze M6)
sg0 = {}; sgd = {}
paid = collections.defaultdict(float); paidDay = collections.defaultdict(float); payN = collections.Counter()
hist = collections.defaultdict(list)        # lp+cr na dobe (firma - szacunek)
for d in days:
    R = by[d]
    for t in towns:
        r = R.get(t)
        if r is None: continue
        C = int(r['kasa']); res = int(r['zapas_kupcow'])
        hist[t].append(int(r['zawor_do_pana']) + int(r['do_korony']))
        if d <= FIX: continue
        ns = min(1.0, (d - FIX) / 28.0)
        Cp = C - X[t]
        # sprzezenie M6: mniejsza nadwyzka - mniej M6 (zostaje w kasie)
        ef = e.get(d, 1.0)
        dm = (max(0, C - 1.5 * res) - max(0, Cp - 1.5 * res)) / 28.0 * ef * ns
        X[t] -= dm; Cp = C - X[t]
        if d < KS: continue
        if SPREAD and cleft[t] > 0:
            want = min(cleft[t], cper[t]); keep0 = 1.5 * res + KD * (sum(hist[t][-7:]) * 52.0) / 364.0
            pay0 = max(0.0, min(want, Cp - keep0))
            cleft[t] -= want; X[t] += pay0; paid[t] += pay0; paidDay[d] += pay0
            if t in sg0: sg0[t] += want - pay0
            Cp = C - X[t]
        q = Cp > 1.5 * res and not (HOUSES and poor(t, d))
        if not q: sg0.pop(t, None); sgd.pop(t, None); continue
        if t not in sgd: sg0[t] = Cp; sgd[t] = d; smin[t] = Cp; continue
        smin[t] = min(smin[t], Cp)
        span = d - sgd[t]
        if span < DAYS: continue
        grow = (Cp - sg0[t]) / span
        F = (sum(hist[t][-7:]) * 52.0 if FW == 7 else sum(hist[t][-28:]) * 13.0)
        pay = 0.0
        if grow > 0:
            keep = 1.5 * res + KD * F / 364.0
            base = (smin[t] if MINW else Cp) - (cleft[t] if SPREAD else 0)
            if base > keep: pay = SHARE * (base - keep)
        if SPREAD:
            cleft[t] += pay; cper[t] = cleft[t] / DAYS
            sg0[t] = Cp - cleft[t]; sgd[t] = d; smin[t] = Cp
            continue
        X[t] += pay; paid[t] += pay; paidDay[d] += pay
        if pay > 0: payN[d] += 1
        sg0[t] = Cp - pay; sgd[t] = d; smin[t] = Cp - pay

def world(d, new):
    R = by[d]; tot = 0; over = 0; below = 0; b15 = 0
    for t, r in R.items():
        C = int(r['kasa']) - (X_at[d][t] if new else 0); res = int(r['zapas_kupcow'])
        tot += C; over += max(0, C - 1.5 * res); below += C < res; b15 += C <= 1.5 * res
    return tot, over, below, b15

# drugi przebieg - zapamietaj X na dobe
X.clear(); sg0.clear(); sgd.clear(); paid.clear(); paidDay.clear(); payN.clear(); hist.clear(); cleft.clear(); cper.clear()
X_at = {}
for d in days:
    R = by[d]
    for t in towns:
        r = R.get(t)
        if r is None: continue
        C = int(r['kasa']); res = int(r['zapas_kupcow'])
        hist[t].append(int(r['zawor_do_pana']) + int(r['do_korony']))
        if d <= FIX: continue
        ns = min(1.0, (d - FIX) / 28.0)
        Cp = C - X[t]
        ef = e.get(d, 1.0)
        dm = (max(0, C - 1.5 * res) - max(0, Cp - 1.5 * res)) / 28.0 * ef * ns
        X[t] -= dm; Cp = C - X[t]
        if d < KS: continue
        if SPREAD and cleft[t] > 0:
            want = min(cleft[t], cper[t]); keep0 = 1.5 * res + KD * (sum(hist[t][-7:]) * 52.0) / 364.0
            pay0 = max(0.0, min(want, Cp - keep0))
            cleft[t] -= want; X[t] += pay0; paid[t] += pay0; paidDay[d] += pay0
            if t in sg0: sg0[t] += want - pay0
            Cp = C - X[t]
        q = Cp > 1.5 * res and not (HOUSES and poor(t, d))
        if not q: sg0.pop(t, None); sgd.pop(t, None); continue
        if t not in sgd: sg0[t] = Cp; sgd[t] = d; smin[t] = Cp; continue
        smin[t] = min(smin[t], Cp)
        span = d - sgd[t]
        if span < DAYS: continue
        grow = (Cp - sg0[t]) / span
        F = (sum(hist[t][-7:]) * 52.0 if FW == 7 else sum(hist[t][-28:]) * 13.0)
        pay = 0.0
        if grow > 0:
            keep = 1.5 * res + KD * F / 364.0
            base = (smin[t] if MINW else Cp) - (cleft[t] if SPREAD else 0)
            if base > keep: pay = SHARE * (base - keep)
        if SPREAD:
            cleft[t] += pay; cper[t] = cleft[t] / DAYS
            sg0[t] = Cp - cleft[t]; sgd[t] = d; smin[t] = Cp
            continue
        X[t] += pay; paid[t] += pay; paidDay[d] += pay
        if pay > 0: payN[d] += 1
        sg0[t] = Cp - pay; sgd[t] = d; smin[t] = Cp - pay
    X_at[d] = dict(X)
for d in days:
    X_at[d] = collections.defaultdict(float, X_at[d])

print('share %.2f keepDays %.0f co %d dob, domy najpierw %s, minimum okna %s' % (SHARE, KD, DAYS, HOUSES, MINW))
print('doba   kasy_stare  kasy_nowe  ponad1.5_stare ponad1.5_nowe  ponizej_zapasu st/nw  <=1.5 st/nw  gersuma_dnia(n)')
for d in days[::14] + [days[-1]]:
    a = world(d, False); b = world(d, True)
    print('%d %11.0f %10.0f %14.0f %13.0f %8d/%-4d %6d/%-4d %10.0f (%d)' % (d, a[0], b[0], a[1], b[1], a[2], b[2], a[3], b[3], paidDay[d], payN[d]))
d1 = days[-1]; d0 = d1 - 28
ga = (world(d1, False)[0] - world(d0, False)[0]) / 28; gb = (world(d1, True)[0] - world(d0, True)[0]) / 28
pw = sum(v for k, v in paidDay.items() if d0 < k <= d1) / 28
print('przyrost kas swiata ostatnie 28 dob: stare %+.0f/d, nowe %+.0f/d; gersuma w oknie %.0f/d; gersuma razem %.0f' % (ga, gb, pw, sum(paid.values())))
bel_a = max(world(d, False)[2] for d in days if d > d1 - 28); bel_b = max(world(d, True)[2] for d in days if d > d1 - 28)
print('ponizej zapasu (najwiecej w oknie): stare %d, nowe %d; na koniec %d / %d' % (bel_a, bel_b, world(d1, False)[2], world(d1, True)[2]))
top = sorted(paid.items(), key=lambda kv: -kv[1])[:8]
print('najwieksze gersumy:', ', '.join('%s %.0f' % (by[d1][t]['miasto'], v) for t, v in top))

# diagnostyka: miasta ponizej zapasu w nowym, a nie w starym
seen = set()
for d in days:
    if d <= d1 - 28: continue
    for t, r in by[d].items():
        C = int(r['kasa']); res = int(r['zapas_kupcow'])
        if C - X_at[d][t] < res and C >= res and t not in seen:
            seen.add(t)
            print('ponizej tylko w nowym: %s doba %d kasa %d nowa %.0f zapas %d gersumy %.0f X %.0f' % (r['miasto'], d, C, C - X_at[d][t], res, paid[t], X_at[d][t]))

# miasta z gersuma: najmniejsza kasa / zapas po pierwszej gersumie (nowy) wobec starego w tych samych dobach
rows = []
for t, v in paid.items():
    if v <= 0: continue
    ds = [d for d in days if d > FIX and X_at[d][t] > 0]
    if not ds: continue
    mn = min((int(by[d][t]['kasa']) - X_at[d][t]) / max(1, int(by[d][t]['zapas_kupcow'])) for d in ds)
    mo = min(int(by[d][t]['kasa']) / max(1, int(by[d][t]['zapas_kupcow'])) for d in ds)
    rows.append((mn, mo, by[d1][t]['miasto'], v))
rows.sort()
print('miasta z gersuma %d; najmniejsza kasa/zapas po gersumie (nowy / stary):' % len(rows), ', '.join('%s %.2f/%.2f' % (r[2], r[0], r[1]) for r in rows[:8]))
