# Balance by kraina (land) of grain and fish: Plan to-market supply vs paid demand of towns of that kraina; shelves; index. Read-only.
import csv, collections, sys, os, statistics as st
D = r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30'
PLAN_DAY = sys.argv[1] if len(sys.argv) > 1 else '109179'
DAY = int(sys.argv[2]) if len(sys.argv) > 2 else 109193
OUT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, OUT)
import geo
names, land, sea, vbound, pos = geo.load()

km = [r for r in csv.DictReader(open(D + '/ks-miasta.csv', encoding='utf-8'), delimiter=';') if int(r['dzien']) == DAY]
tk = {}
sup = collections.defaultdict(lambda: collections.defaultdict(float))
supv = collections.defaultdict(lambda: collections.defaultdict(float))
ppl = collections.defaultdict(float); hands = collections.defaultdict(float)
eat = collections.defaultdict(lambda: collections.defaultdict(float)); prodk = collections.defaultdict(lambda: collections.defaultdict(float))
mk_of = {}
with open(D + '/ks-plan.csv', encoding='utf-8') as f:
    seen = set()
    for r in csv.DictReader(f, delimiter=';'):
        if r['dzien'] != PLAN_DAY: continue
        o = r['okreg_id']; k = r['kraina']; mk_of[k] = r['rynek']
        if o.endswith('#pola'): tk[o.split('#')[0]] = k
        sup[k][r['towar']] += float(r['na_targ_szt_d'])
        if not o.endswith('#pola'): supv[k][r['towar']] += float(r['na_targ_szt_d'])
        eat[k][r['towar']] += float(r['zjedzone_w_okregu_kg_d']); prodk[k][r['towar']] += float(r['produkcja_kg_d'])
        if o not in seen:
            seen.add(o); ppl[k] += float(r['ludzie']); hands[k] += float(r['rece_pole'])
dem = collections.defaultdict(float); shelf = collections.defaultdict(float); idx = collections.defaultdict(list); tp = collections.defaultdict(float); nt = collections.Counter()
want = collections.defaultdict(float); got = collections.defaultdict(float); nog = collections.defaultdict(float); nogd = collections.defaultdict(float)
wk = [r for r in csv.DictReader(open(D + '/ks-miasta.csv', encoding='utf-8'), delimiter=';') if 109123 < int(r['dzien']) <= DAY]
for r in km:
    k = tk.get(r['miasto_id'], '?')
    dem[k] += float(r['zboze_popyt_zaplacony_szt_d']); shelf[k] += float(r['zboze_polka_szt']); idx[k].append(float(r['zboze_indeks'])); tp[k] += float(r['ludzie']); nt[k] += 1
for r in wk:
    k = tk.get(r['miasto_id'], '?')
    want[k] += float(r['wsie_chciane_szt_7d']); got[k] += float(r['wsie_kupione_szt_7d']); nog[k] += float(r['wsie_bez_kiesy_szt_7d']); nogd[k] += float(r['wsie_bez_towaru_szt_7d'])
print('plan', PLAN_DAY, 'towns day', DAY, '| grain szt/d: to market (villages+town fields) vs paid town demand (incl. village purchases attributed to the town); shelf; idx median; villages wanted/bought/no-gold/no-goods (all goods, 10 wk)')
print('%-14s %-8s %3s %7s %7s %7s %5s %8s %6s %5s | %8s %5s %5s %5s' % ('kraina', 'rynek', 'nt', 'ppl_M', 'sup', 'dem', 'S/D', 'shelf', 'days', 'idx', 'want', 'got%', 'nog%', 'nogd%'))
rows = []
for k in set(list(sup) + list(dem)):
    s = sup[k]['grain']; d = dem[k]
    rows.append((k, mk_of.get(k, '?'), nt[k], ppl[k] / 1e6, s, d, s / d if d else float('inf'), shelf[k], shelf[k] / d if d else float('nan'), st.median(idx[k]) if idx[k] else float('nan'),
                 want[k], got[k] / want[k] if want[k] else 0, nog[k] / want[k] if want[k] else 0, nogd[k] / want[k] if want[k] else 0))
for r in sorted(rows, key=lambda r: (r[1], r[6])):
    print('%-14s %-8s %3d %7.2f %7.0f %7.0f %5.2f %8.0f %6.1f %5.2f | %8.0f %5.2f %5.2f %5.2f' % r)
for mk in ('Westeros', 'Essos'):
    rs = [r for r in rows if r[1] == mk]
    S = sum(r[4] for r in rs); Dd = sum(r[5] for r in rs)
    short = sum(max(0, r[5] - r[4]) for r in rs); surp = sum(max(0, r[4] - r[5]) for r in rs)
    print(mk, 'sup %.0f dem %.0f S/D %.2f | inter-kraina need (sum of deficits) %.0f szt/d, sum of surpluses %.0f szt/d' % (S, Dd, S / Dd, short, surp))
# fish
print('\nfish by kraina: sup vs (no town demand col for fish) - production kg/d, eaten in district, to market szt/d')
for k in sorted(sup, key=lambda k: -sup[k]['fish'])[:12]:
    print('%-14s fish to market %6.0f szt/d | grain to market %6.0f' % (k, sup[k]['fish'], sup[k]['grain']))
