# Replay of KsFoodCore.Decide on game snapshots (ks-miasta-towary + kasy-miast) with the simulator's day table (world_r2) - attribution of KS-H limits.
import json, math, csv, sys, collections
K = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/ksim/'
D = 'D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/'
W = json.load(open(K + 'sim/world_r2.json'))
T = W['towns']; NT = len(T)
VAL = dict(zip(W['goods'], W['val']))
GOODS = ['grain', 'fish', 'meat', 'cheese', 'butter', 'grape', 'olives', 'salt']   # C# order Z (subset)
PER = {'grain': False, 'fish': True, 'meat': True, 'cheese': False, 'butter': True, 'grape': True, 'olives': False, 'salt': False}
STAPLE = {'grain', 'salt'}
land = {}
for r in csv.DictReader(open(D + 'ks-miasta.csv', encoding='utf-8'), delimiter=';'): land[r['miasto_id']] = r['kraina']
ids = [t['id'] for t in T]
order = sorted(range(NT), key=lambda i: ids[i])          # C# index = StringId order
rank = {i: r for r, i in enumerate(order)}


def fnv(s):
    h = 2166136261
    for c in s: h = ((h ^ ord(c)) * 16777619) & 0xffffffff
    return h


dl = W['dland']; ds = W['dsea']; sp = 41.0; dk = 1.18
days = [[-1.0] * NT for _ in range(NT)]; sea = [[False] * NT for _ in range(NT)]
for a in range(NT):
    for b in range(NT):
        if a == b or T[a]['mk'] != T[b]['mk']: continue
        l = dl[a][b]; s = ds[a][b]; lo = l >= 0; ao = s >= 0
        se = (not lo) or (ao and l > 1.15 * s); road = (s if ao else -1) if se else l
        if road < 0: continue
        days[a][b] = max(1.0, math.ceil(road * dk / (sp * 4.0))) + 1.0 if se else max(1.0, math.ceil(road * dk / sp)); sea[a][b] = se


def mkcand(n12):
    c = []; cf = []
    for t in range(NT):
        l = sorted([(days[s][t], rank[s], s) for s in range(NT) if s != t and T[s]['mk'] == T[t]['mk'] and days[s][t] >= 0])
        c.append([s for _, _, s in l[:n12]]); cf.append([s for _, _, s in l[n12:]])
    return c, cf


def load(day):
    st = {}
    for r in csv.DictReader(open(D + 'ks-miasta-towary.csv', encoding='utf-8'), delimiter=';'):
        if int(r['dzien']) != day: continue
        st[(r['miasto_id'], r['towar'])] = dict(sh=float(r['polka_szt']), inc=float(r['w_drodze_szt']), ix=float(r['indeks']), dp=float(r['popyt_zaplacony_szt_d']),
                                               off=float(r['odbior_7d_szt_d']), kx=float(r['kx']), T=float(r['cel_dni']))
    g = {}
    for r in csv.DictReader(open(D + 'kasy-miast.csv', encoding='utf-8'), delimiter=';'):
        if int(r['dzien']) == day: g[r['miasto_id']] = (float(r['kasa']), float(r['zapas_kupcow'])); KING[r['miasto_id']] = r['krolestwo']
    return st, g


KING = {}
# wojny wywnioskowane (log: "Pentos wzywa Braavos przeciw Myr", "Dragonstone wzywa Riverlands przeciw Stormlands"; reszta - brak dowozu KS-H i bramy Planu przy nadwyzce)
WARS = {frozenset(p) for p in [('The Reach', 'Dorne'), ('Myr', 'Pentos'), ('House Targaryen', 'Qarth'), ('Lys', 'Tyrosh'), ('Dothraki Horde', 'House Targaryen'), ('Lys', 'Lorath'),
                                ('Lys', 'Dothraki Horde'), ('Stormlands', 'Dragonstone'), ('Volantis', 'Dothraki Horde'), ('Tyrosh', 'House Targaryen'), ('Stormlands', 'Summer Isles')]}


def atwar(s, t):
    a = KING.get(ids[s]); b = KING.get(ids[t])
    return a is not None and b is not None and a != b and frozenset((a, b)) in WARS


def decide(day, st, gold, o):
    cand, candfar = o['cand']
    today = day + 1
    sh = {}; inc = {}; ix = {}; dz = {}; dzB = {}; Ts = {}
    for t in range(NT):
        for gname in GOODS:
            x = st.get((ids[t], gname))
            if not x: continue
            k = (t, gname); sh[k] = int(x['sh']); inc[k] = x['inc']; ix[k] = x['ix']; Ts[k] = x['T']
            b = max(x['dp'], x['off']); dzB[k] = b; dz[k] = b * (x['kx'] if o.get('kx', 1) else 1.0)
    budget = [0.0] * NT; bshort = [0.0] * NT
    for t in range(NT):
        g, r = gold.get(ids[t], (0.0, 0.0))
        if o.get('nobudget'): budget[t] = bshort[t] = 1e18; continue
        cs = o.get('coffer', 0.15); rr = r * o.get('resk', 1.0)
        budget[t] = cs * max(0.0, g - rr); bshort[t] = max(budget[t], min(cs * max(0.0, g - 0.5 * rr), budget[t] + 0.05 * rr))
    offer = {}
    for k in sh:
        sur = sh[k] - o.get('keep', 1.25) * Ts[k] * dzB[k] - 5
        shr = 1.0 if (o.get('share_staple') and k[1] in STAPLE) else o.get('share', 0.5)
        offer[k] = math.floor(shr * sur) if sur > 1 else 0
    spent = [0.0] * NT; flows = []; stat = collections.Counter()

    def lotprice(s, gname, n):
        v = VAL[gname]; p0 = v * ix[(s, gname)]
        if not o.get('slope', 1): return n * max(1, math.ceil(p0 - 1e-9))
        V0 = max(1.0, sh[(s, gname)]) * v; den0 = 0.04 * V0 * 1.5 + 2.0

        def P(j): return max(1, math.ceil(p0 * (den0 / max(1e-6, den0 - 0.04 * min(j, max(0, sh[(s, gname)] - 1)) * v)) ** 0.6 - 1e-9))
        if n < 8: return sum(P(j) for j in range(n))
        m1 = n // 8; m2 = n // 2
        return m1 * P(0) + (m2 - m1) * P(m1) + (n - m2) * P(m2)

    def dunit(t, gname):
        k = (t, gname); p = VAL[gname] * ix[k]
        if sh[k] <= 5 and not o.get('nocap'):
            si = (7.0 / Ts[k]) ** 0.6; p = min(p, math.ceil(1.5 * si * VAL[gname]))
        return max(1, math.ceil(p - 1e-9))

    for gname in GOODS:
        lim = 7.0 if PER[gname] else 30.0
        dst = []
        for t in range(NT):
            k = (t, gname)
            if k not in sh: continue
            if not o.get('allreview') and (fnv(ids[t]) & 1) != (today & 1): continue
            d = dz[k]
            if not d > 0: continue
            if sh[k] + inc[k] >= (Ts[k] + lim + 2) * d: continue
            dst.append(t)
        dst.sort(key=lambda t: (-ix[(t, gname)], rank[t]))
        for t in dst:
            k = (t, gname); d = dz[k]; Tt = Ts[k]; Hc = sh[k] + inc[k]
            need0 = max(0.0, (Tt + 2 + 2) * d - Hc)
            c = []

            def add(s):
                ks = (s, gname)
                if offer.get(ks, 0) < 1: stat['c: bez nadwyzki'] += 1; return False
                L = days[s][t]
                if not (L >= 0) or L > lim: stat['c: zasieg'] += 1; return False
                if o.get('war', 1) and atwar(s, t): stat['c: wojna'] += 1; return False
                tau = 0.0025 if sea[s][t] else 0.025
                cc = ix[ks] * (1 + tau * L)
                if not o.get('nomargin') and ix[k] < cc * 1.03: stat['c: indeks'] += 1; return False
                c.append((cc, rank[s], s)); return True
            for s in cand[t]: add(s)
            if (not c or o.get('faralways')) and candfar[t]:
                nf = 0
                for s in candfar[t]:
                    if nf >= o.get('nfar', 4): break
                    L = days[s][t]
                    if not (L >= 0): continue
                    if L > lim: break
                    if add(s): nf += 1
            stat['cele'] += 1
            if not c: stat['cel bez kandydata'] += 1; stat['cel bez kandydata szt'] += need0; stat['~bez kand. ' + gname] += need0; continue
            c.sort()
            shortStall = Hc < Tt * d
            du = dunit(t, gname)
            used = 0
            for cc, _, s in c:
                if used >= o.get('maxsrc', 3): stat['3 zrodla'] += 1; break
                ks = (s, gname); L = days[s][t]
                want = (Tt + L + 2) * d - Hc
                q = min(offer[ks], want)
                if q < max(5.0, 0.5 * d): continue
                n = int(math.floor(q)); se = sea[s][t]; tau = 0.0025 if se else 0.025
                cost = lotprice(s, gname, n); fr = round(cost * tau * L)
                if not o.get('noprofit'):
                    n_0 = n; tries = 0
                    while n > 0 and cost + fr > math.floor(n * du * 0.97) and tries < 3:
                        n //= 2; tries += 1
                        if n < max(5.0, 0.5 * d): n = 0; break
                        cost = lotprice(s, gname, n); fr = round(cost * tau * L)
                    if n <= 0 or cost + fr > math.floor(n * du * 0.97): stat['bez zysku loty'] += 1; stat['bez zysku szt'] += n_0; continue
                    if n < n_0: stat['polowienie szt'] += n_0 - n
                avail = (bshort[t] if (shortStall and gname in STAPLE) else budget[t]) - spent[t]
                if cost + fr > avail:
                    nf_ = int(math.floor(n * avail / (cost + fr))) if cost + fr > 0 and avail > 0 else 0
                    stat['kasa celu szt'] += n - max(0, nf_)
                    if nf_ < max(5.0, 0.5 * d): continue
                    n = nf_; cost = lotprice(s, gname, n); fr = round(cost * tau * L)
                    if cost + fr > avail + 1: continue
                if n <= 0: continue
                flows.append((s, t, gname, n, days[s][t], se))
                used += 1; offer[ks] -= n; sh[ks] -= n; Hc += n; spent[t] += cost + fr
    return flows, stat


if __name__ == '__main__':
    days_list = [109144, 109151, 109158, 109165]
    c12 = mkcand(12); call = mkcand(200)
    variants = [('baza (kod gry)', {}),
                ('kandydaci: wszystkie miasta rynku w zasiegu', {'cand': call}),
                ('dalsze zawsze (12 + 4 dalsze)', {'faralways': 1}),
                ('bez ciecia ceny pustej polki (DestUnit bez 1.5x startu)', {'nocap': 1}),
                ('bez rachunku zysku', {'noprofit': 1}),
                ('kasa celu bez limitu', {'nobudget': 1}),
                ('kasa 15% kasy bez odjecia zapasu kupcow', {'resk': 0.0}),
                ('oferta zrodla 100% nadwyzki', {'share': 1.0}),
                ('zapas zrodla 1.0 T*', {'keep': 1.0}),
                ('wszystkie miasta co dobe', {'allreview': 1}),
                ('bez warunku indeksu', {'nomargin': 1}),
                ('A: wszyscy kandydaci + bez ciecia ceny + kasa bez zapasu', {'cand': call, 'nocap': 1, 'resk': 0.0}),
                ('A + oferta 100%', {'cand': call, 'nocap': 1, 'resk': 0.0, 'share': 1.0}),
                ('sufit fizyczny: bez kasy, bez zysku, wszyscy kandydaci, oferta 100%, co dobe', {'cand': call, 'nobudget': 1, 'noprofit': 1, 'share': 1.0, 'allreview': 1})]
    variants.insert(1, ('bez wojen', {'war': 0}))
    variants.append(('B: dalsze zawsze + bez ciecia ceny + kasa bez zapasu + oferta 100%', {'faralways': 1, 'nfar': 8, 'nocap': 1, 'resk': 0.0, 'share': 1.0}))
    import re
    LOG = 'D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_09-07-09.log'
    FREE = {}
    for line in open(LOG, encoding='utf-8', errors='replace'):
        if 'KS: korytarze planu doba 109165 ' in line:
            seg = line.split('korytarze bez sufitu dowozu >= 20 szt./d:')[1].split('| miasta (psujace')[0]
            for m in re.finditer(r'([a-z_]+)>([a-z_]+) ([A-Za-z_]+) (\d+)', seg): FREE[(m.group(1), m.group(2), m.group(3))] = float(m.group(4))
    SEAFIX = [('S1: statek, gdy szybszy (doby, nie droga x1.15)', {}), ('S2: S1 + dalsze zawsze (8)', {'faralways': 1, 'nfar': 8}),
              ('S3: S1 + wszyscy kandydaci', {'cand': 'all'}), ('S4: S2 + kasa bez zapasu + bez ciecia ceny', {'faralways': 1, 'nfar': 8, 'resk': 0.0, 'nocap': 1}),
              ('S5: S4 + oferta 100%', {'faralways': 1, 'nfar': 8, 'resk': 0.0, 'nocap': 1, 'share': 1.0}),
              ('S6: S2 + kredyt kupiecki (kasa celu bez limitu) + oferta 100% zboza i soli', {'faralways': 1, 'nfar': 8, 'nobudget': 1, 'share_staple': 1}),
              ('S7: S1 + kredyt kupiecki', {'nobudget': 1}), ('S8: S2 + kredyt kupiecki', {'faralways': 1, 'nfar': 8, 'nobudget': 1})]
    if len(sys.argv) > 1: variants = [v for v in variants if any(a in v[0] for a in sys.argv[1:])]
    snaps = {d: load(d) for d in days_list}
    res = {}
    for nm, o0 in variants:
        o = dict(o0); o.setdefault('cand', c12)
        tot = collections.Counter(); inter = collections.Counter(); intra = collections.Counter(); st_all = collections.Counter(); corr = collections.Counter()
        for d in days_list:
            st, g = snaps[d]
            fl, stt = decide(d, st, g, o)
            st_all.update(stt)
            for s, t, gn, n, L, se in fl:
                tot[gn] += n
                ls, lt = land[ids[s]], land[ids[t]]
                if ls != lt: inter[gn] += n; corr[(ls, lt, gn)] += n
                else: intra[gn] += n
        nd = len(days_list)
        res[nm] = corr
        print('== %s: kupione/dobe %d (zboze %d, ryby %d, mieso %d, ser %d, maslo %d, winogrona %d, oliwki %d, sol %d) miedzy krainami %d, wewnatrz %d' % (
            nm, sum(tot.values()) / nd, tot['grain'] / nd, tot['fish'] / nd, tot['meat'] / nd, tot['cheese'] / nd, tot['butter'] / nd, tot['grape'] / nd, tot['olives'] / nd,
            tot['salt'] / nd, sum(inter.values()) / nd, sum(intra.values()) / nd))
        print('   ' + ', '.join('%s %d' % (k, v / nd) for k, v in sorted(st_all.items())))
        fr8 = [k for k in FREE if k[2] in GOODS]
        ok = sum(1 for k in fr8 if corr.get(k, 0) / nd >= 0.8 * FREE[k])
        imp = collections.defaultdict(lambda: [0.0, 0.0])
        for k in fr8: imp[(k[1], k[2])][0] += FREE[k]
        for k, v in corr.items(): imp[(k[1], k[2])][1] += v / nd
        iok = sum(1 for k, v in imp.items() if v[0] >= 20 and v[1] >= 0.8 * v[0]); inn = sum(1 for k, v in imp.items() if v[0] >= 20)
        vol = sum(min(FREE[k], corr.get(k, 0) / nd) for k in fr8) / max(1, sum(FREE[k] for k in fr8))
        print('   NKS-24 (8 towarow): korytarzy planu %d, >= 0.8: %d; krainy importu >= 0.8: %d z %d; pokrycie wolumenu planu na tych samych korytarzach %.0f%%; qartheen zboze %.0f / %.0f, crownlands zboze %.0f / %.0f, aserai zboze %.0f / %.0f' % (
            len(fr8), ok, iok, inn, 100 * vol, imp[('qartheen', 'grain')][1], imp[('qartheen', 'grain')][0], imp[('crownlands', 'grain')][1], imp[('crownlands', 'grain')][0], imp[('aserai', 'grain')][1], imp[('aserai', 'grain')][0]))
        top = sorted(corr.items(), key=lambda x: -x[1])[:10]
        print('   korytarze: ' + ', '.join('%s>%s %s %d' % (a, b, c, v / nd) for (a, b, c), v in top))
    for a in range(NT):
        for b in range(NT):
            if a == b or T[a]['mk'] != T[b]['mk']: continue
            l = dl[a][b]; s_ = ds[a][b]
            ld = max(1.0, math.ceil(l * dk / sp)) if l >= 0 else -1.0
            sd = max(1.0, math.ceil(s_ * dk / (sp * 4.0))) + 1.0 if (s_ >= 0 and (l < 0 or s_ < l - 1e-6)) else -1.0
            if sd >= 0 and (ld < 0 or sd < ld): days[a][b] = sd; sea[a][b] = True
            elif ld >= 0: days[a][b] = ld; sea[a][b] = False
    c12s = mkcand(12); calls = mkcand(200)
    for nm, o0 in SEAFIX:
        o = dict(o0); o['cand'] = calls if o.get('cand') == 'all' else c12s
        tot = collections.Counter(); inter = collections.Counter(); corr = collections.Counter(); st_all = collections.Counter()
        for d in days_list:
            st, g = snaps[d]
            fl, stt = decide(d, st, g, o); st_all.update(stt)
            for s1, t1, gn, n, L, se in fl:
                tot[gn] += n
                if land[ids[s1]] != land[ids[t1]]: inter[gn] += n; corr[(land[ids[s1]], land[ids[t1]], gn)] += n
        nd = len(days_list); res[nm] = corr
        fr8 = [k for k in FREE if k[2] in GOODS]
        ok = sum(1 for k in fr8 if corr.get(k, 0) / nd >= 0.8 * FREE[k])
        imp = collections.defaultdict(lambda: [0.0, 0.0])
        for k in fr8: imp[(k[1], k[2])][0] += FREE[k]
        for k, v in corr.items(): imp[(k[1], k[2])][1] += v / nd
        iok = sum(1 for k, v in imp.items() if v[0] >= 20 and v[1] >= 0.8 * v[0]); inn = sum(1 for k, v in imp.items() if v[0] >= 20)
        vol = sum(min(FREE[k], corr.get(k, 0) / nd) for k in fr8) / max(1, sum(FREE[k] for k in fr8))
        print('== %s: kupione/dobe %d (zboze %d) miedzy krainami %d | NKS-24: >= 0.8 %d z %d; krainy importu %d z %d; wolumen %.0f%%; qartheen zboze %.0f / %.0f, crownlands %.0f / %.0f, aserai %.0f / %.0f' % (
            nm, sum(tot.values()) / nd, tot['grain'] / nd, sum(inter.values()) / nd, ok, len(fr8), iok, inn, 100 * vol, imp[('qartheen', 'grain')][1], imp[('qartheen', 'grain')][0],
            imp[('crownlands', 'grain')][1], imp[('crownlands', 'grain')][0], imp[('aserai', 'grain')][1], imp[('aserai', 'grain')][0]))
        print('   ' + ', '.join('%s %d' % (k, v / nd) for k, v in sorted(st_all.items())))
    json.dump({nm: {'|'.join(k): v / len(days_list) for k, v in r.items()} for nm, r in res.items()}, open(K + 'r4/tr/a5_out.json', 'w'))
