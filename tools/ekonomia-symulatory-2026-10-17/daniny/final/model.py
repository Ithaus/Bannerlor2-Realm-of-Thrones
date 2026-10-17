# Model statyczny projektu DS (daniny sredniowieczne) na liczbach z logow (rc.py) i CSV testu KS.
# Tylko liczy - nic nie zapisuje poza wynikiem w tym folderze.
import json, csv, re, statistics as st
from collections import defaultdict
HERE = 'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/daniny/'
CSVD = 'D:/Backup-Bannerlord/logi-testow/2026-10-17_06-07-51/'
R = {l: json.load(open(HERE + 'final/' + l + '_rc.json')) for l in ('ks', 'base', 'jeff')}
out = []
def P(*a): out.append(' '.join(str(x) for x in a))
def g(l, k, w='all'):
    v = R[l][w].get(k); return 0.0 if v is None else v

PHI = 0.72          # utarg swiata / wartosc bazowa produkcji wsi (log KS: 0.69-0.77, srednio ok. 0.72)
TITHE = 0.05        # dziesiecina: 5% oddanego utargu (z czesci wsi), polowa jalmuzna
RAMP = 1.0

for W in ('all', 'last28'):
    P('=' * 20, 'OKNO', W)
    for l in ('ks', 'base', 'jeff'):
        H = g(l, 'u_oddane', W); Pv = g(l, 'v_utarg', W); pan = g(l, 'u_pan', W); maj = g(l, 'u_majatki', W)
        ti = g(l, 'ti_nalezne', W); alm = g(l, 'ti_jalm_wsi', W)
        lev = -g(l, 'v_korona', W); hv = g(l, 'hv_pobrano', W); hvc = g(l, 'hv_korony', W); hvl = g(l, 'hv_glowy', W); hvt = g(l, 'hv_miasta', W) + g(l, 'hv_zamki', W)
        rent = -g(l, 'v_renty', W); mk = g(l, 'mk_do_kies', W); prz = g(l, 'v_przelewy', W); zw = g(l, 'v_166', W); ksh = g(l, 'v_ksh', W)
        bk = -g(l, 'bk_rynek_wsi', W); kz = g(l, 'kl_wsie_zakupy', W); lo = g(l, 'lo_wsi', W)
        inflow = Pv + prz + mk + zw + ksh
        P('--', l, 'H oddane %.0f | do kiesy z wozow %.0f (%.1f%% H) | pan %.0f (%.1f%%) majatki %.0f (%.1f%%)' % (H, Pv, 100 * Pv / H, pan, 100 * pan / H, maj, 100 * maj / H))
        P('   wplyw do kies %.0f = wozy %.0f + przelewy gry %.0f + markietani %.0f + zwolnieni %.0f + KS-H %.0f' % (inflow, Pv, prz, mk, zw, ksh))
        P('   odplywy resztowe: danina korony %.0f, podatek od nadwyzki %.0f (korona %.0f, pan %.0f, kasy %.0f), rynek BK %.0f | stale: renta %.0f, dziesiecina netto %.0f, zakupy wsi %.0f, pozyczki %.0f' % (lev, hv, hvc, hvl, hvt, bk, rent, ti - alm, kz, lo))
        for gg in (0.0, 1.0, 2.0):
            crown = gg / 10 * (Pv - ti) + gg / 10 * mk
            net = (Pv - ti + alm) + (prz + zw + ksh + mk) - crown - rent
            P('   DS g=%.0f: korona przy wozie %.0f | do wydania wsi %.0f/d -> budzet (0.95) %.0f, do skrzyni %.0f | wobec dzis: zakupy %.0f + rynek BK %.0f' % (gg, crown, net, 0.95 * net, 0.05 * net, kz, bk))
    P()

# ---------------- korona i panowie: F1 (wsie) przy dzisiejszych miastach
def crown_town_share(l, W):
    # udzial korony w poborach z miast (resztowe dzis): zawor miast i zamkow, P1, danina miast, podatek od skarbca ludu, 184, clo
    lev_t = g(l, 'kd_danina', W) + g(l, 'v_korona', W)   # kd_danina = miasta + wsie
    cr = g(l, 't_zawor_kor', W) + g(l, 'z_kor', W) + g(l, 'k_P1', W) + lev_t + g(l, 'hl_korony', W) + g(l, 'k_184', W) + g(l, 'kd_clo', W)
    lo = g(l, 't_zawor_pan', W) + g(l, 'z_pan', W) + g(l, 'hl_glowy', W) + g(l, 'pp_lud', W) + g(l, 'pp_podz', W) + g(l, 'pp_farma', W) + g(l, 'w7_razem', W)
    return cr, lo, cr / (cr + lo), lev_t

for W in ('all', 'last28'):
    P('=' * 20, 'F1 KORONA/PANOWIE', W)
    for l in ('ks', 'base', 'jeff'):
        cr, lo, sc, lev_t = crown_town_share(l, W)
        Pv = g(l, 'v_utarg', W); ti = g(l, 'ti_nalezne', W); mk = g(l, 'mk_do_kies', W)
        lev = -g(l, 'v_korona', W); hvc = g(l, 'hv_korony', W); hvl = g(l, 'hv_glowy', W); hvt = g(l, 'hv_miasta', W) + g(l, 'hv_zamki', W)
        w7v = 0.25 * g(l, 'w7_razem', W)
        dayinc = g(l, 'k_wpl', W) + g(l, 'k_zapas', W)
        for gg in (1.0,):
            cs = gg / 10 * (Pv - ti + mk)
            freed = lev + hvc + hvl + hvt + w7v - cs
            save = 0.05 * ((Pv - ti) + g(l, 'v_przelewy', W) + g(l, 'v_166', W) + mk - cs)
            to_towns = freed - save
            dcrown_static = -lev - hvc + cs
            dlord_static = -hvl - w7v
            dcoffer = -hvt
            # obieg: kasy miast dostaja to_towns, resztowe pobory miast oddaja je panom i koronie w udziale sc (98% netto doplywu)
            back = 0.98 * (to_towns)
            dcrown_dyn = dcrown_static + sc * back
            dlord_dyn = dlord_static + (1 - sc) * back
            P('%-5s udzial korony w poborach miast %.2f (korona %.0f / panowie %.0f) | wplyw dnia korony %.0f' % (l, sc, cr, lo, dayinc))
            P('   F1 g=1: korona statycznie %+.0f (danina -%.0f, podatek od nadwyzki -%.0f, nowy udzial przy wozie +%.0f) = %+.1f%% wplywow dnia' % (dcrown_static, lev, hvc, cs, 100 * dcrown_static / dayinc))
            P('           panowie statycznie %+.0f; kasy miast %+.0f; wsie zatrzymuja %.0f/d (%.0f do skrzyni, %.0f na zakupy w miastach)' % (dlord_static, dcoffer, freed, save, to_towns))
            P('           po obiegu przez miasta (dzisiejsze pobory miast): korona %+.0f (%+.1f%%), panowie %+.0f; razem %+.0f' % (dcrown_dyn, 100 * dcrown_dyn / dayinc, dlord_dyn, dcrown_dyn + dlord_dyn))
    P()

# ---------------- F3 swiat: miasta, zamki, korona (g = 1, 1.5, 2)
for W in ('last28', 'all'):
    P('=' * 20, 'F3 KORONA SWIAT', W)
    for l in ('ks', 'base', 'jeff'):
        wpl = g(l, 'k_wpl', W); zap = g(l, 'k_zapas', W)
        P1 = g(l, 'k_P1', W); t184 = g(l, 'k_184', W)
        levv = -g(l, 'v_korona', W); levt = g(l, 'kd_danina', W) - levv
        hlc = g(l, 'hl_korony', W); hvc = g(l, 'hv_korony', W)
        clo = g(l, 'kd_clo', W); was = g(l, 'kd_wasale', W)
        need = g(l, 'k_zwrot', W) + g(l, 'k_W5', W) + g(l, 'k_M1', W) + g(l, 'k_M3', W) + g(l, 'k_dary', W) + g(l, 'k_kontr', W) + g(l, 'k_rata186', W) + g(l, 'k_rep', W) + g(l, 'k_dwor', W)
        # bazy
        eW = g(l, 'pp_W', W) / 0.01; eR = g(l, 'pp_R', W) / 0.02; eP = g(l, 'pp_P', W) / 0.03; eC = g(l, 'pp_podz', W) / 0.03
        Pv = g(l, 'v_utarg', W); ti = g(l, 'ti_nalezne', W); mk = g(l, 'mk_do_kies', W)
        sales = g(l, 't_lud_zapl', W) + g(l, 'mk_miasta', W) + g(l, 'kl_wsie_zakupy', W) - g(l, 'bk_rynek_wsi', W)
        mov = g(l, 'p1_nadw', W) + max(0.0, g(l, 't_kasy', W) - 1.5 * g(l, 't_zapas', W))
        base_inc = wpl + zap - P1 - levv - levt - t184 - was   # zostaje: firmy krolewskie (zawor korony), clo, bogaci, inne, wydanie zapasu
        P('--', l, 'wplyw dnia %.0f + zapas %.0f = %.0f | potrzeba przed rentami %.0f (zwrot %.0f) | renty dzis %.0f' % (wpl, zap, wpl + zap, need, g(l, 'k_zwrot', W), g(l, 'k_renty', W)))
        P('   znika: P1 %.0f, danina miast %.0f, danina wsi %.0f, 184 %.0f (zastapione), powinnosci 3%% %.0f (zastapione); do zapasu znika: skarbiec ludu %.0f + wsi %.0f' % (P1, levt, levv, t184, was, hlc, hvc))
        P('   bazy: zarobek W %.0f R %.0f P %.0f podzamcza %.0f; sprzedaz kas %.0f; ruchomosci ponad zapas %.0f; czesc wsi po dziesiecinie %.0f + markietani %.0f' % (eW, eR, eP, eC, sales, mov, Pv - ti, mk))
        for gg in (1.0, 1.5, 2.0):
            s = min(0.10, 0.05 * gg)
            tall = eW * 0.5 * s + eR * s + eP * 1.5 * s + eC * s
            aid = min(0.10, 0.05 * gg) * sales
            sub = gg * 0.10 * mov / 91.0
            vil = gg / 10 * (Pv - ti + mk)
            cus = clo * 2
            vas = was * 10 / 3.0
            new = base_inc + tall + aid + sub + vil + (cus - clo) + clo + vas
            rents = new - need
            P('   g=%.1f: 184 %.0f, pomoc od sprzedazy %.0f, dziesiata od ruchomosci %.0f, udzial wsi %.0f, clo x2 %.0f, pomoc lennikow 10%% %.0f -> wplyw %.0f (%.0f%% dzis) | renty 180 %.0f (dzis %.0f)' % (gg, tall, aid, sub, vil, cus, vas, new, 100 * new / (wpl + zap), rents, g(l, 'k_renty', W)))
    P()

# ---------------- wsie: rozklad i przyklady (CSV KS ks-budzety, dzien 109123 i 109179)
people = {}; king = {}
with open(CSVD + 'plon-wsi.csv', encoding='utf-8', errors='replace') as h:
    r = csv.reader(h, delimiter=';'); hd = next(r)
    last = {}
    for row in r:
        d = dict(zip(hd, row)); last[d['wies_id']] = d
    for k, d in last.items():
        try: people[k] = float(d['ludzie']); king[k] = d['krolestwo']
        except: pass
rows = defaultdict(dict)
with open(CSVD + 'ks-budzety.csv', encoding='utf-8', errors='replace') as h:
    r = csv.reader(h, delimiter=';'); hd = next(r)
    for row in r:
        d = dict(zip(hd, row))
        if d['rodzaj'] != 'wies': continue
        try: rows[int(d['dzien'])][d['osada_id']] = (float(d['utarg_28d_zl_d']), float(d['do_kiesy_28d_zl_d']), d['alfa'])
        except: pass
W = 'all'; l = 'ks'
Hw = g(l, 'u_oddane', W); Ow = g(l, 'v_przelewy', W) + g(l, 'v_166', W) + g(l, 'v_ksh', W) + g(l, 'mk_do_kies', W); Rw = -g(l, 'v_renty', W)
Pw = g(l, 'v_utarg', W)
ppl_tot = sum(people.values())
for day in (109123, 109179):
    V = rows[day]
    hsum = sum(v[0] for v in V.values())
    al = {0: [], 1: [], 2: []}; meas = []
    recs = []
    for vid, (h, k, a) in V.items():
        if h <= 0: continue
        sh = min(1.0, k / h)
        O = Ow * h / hsum
        Rv = Rw * people.get(vid, 0) / ppl_tot
        for gg in (0, 1, 2):
            kv = (sh - TITHE) * (1 - gg / 10.0) + TITHE / 2
            Ov = O * (1 - (gg / 10.0) * (g(l, 'mk_do_kies', W) / Ow))
            alpha = 0.95 * (kv * PHI + PHI * (Ov - Rv) / h)
            al[gg].append(max(0.0, alpha))
        try: meas.append(float(a))
        except: pass
        recs.append((h, vid, sh, O, Rv))
    def q(x, p): x = sorted(x); return x[int(p * (len(x) - 1))]
    P('=' * 20, 'ALFA Z PRAW (KS, dzien', day, ') wsi', len(recs))
    for gg in (0, 1, 2):
        x = al[gg]; P('  g=%d: p10 %.2f p25 %.2f mediana %.2f p75 %.2f p90 %.2f; ponizej 0.2: %d' % (gg, q(x, .1), q(x, .25), q(x, .5), q(x, .75), q(x, .9), sum(1 for v in x if v < 0.2)))
    if meas: P('  alfa zmierzona dzis (netto): p10 %.2f mediana %.2f p90 %.2f; zero: %d z %d' % (q(meas, .1), q(meas, .5), q(meas, .9), sum(1 for v in meas if v <= 0.001), len(meas)))
    recs.sort()
    n = len(recs)
    for lab, p in (('biedna p10', .10), ('mediana', .50), ('bogata p90', .90)):
        h, vid, sh, O, Rv = recs[int(p * (n - 1))]
        y = 364
        # dzis (struktura swiata KS skalowana utargiem tej wsi)
        lev = -g(l, 'v_korona', W) * h / Hw; hv = g(l, 'hv_pobrano', W) * h / Hw; bk = -g(l, 'bk_rynek_wsi', W) * h / Hw
        ti = TITHE * h; alm = ti / 2
        inn = sh * h + O
        basket_now = g(l, 'kl_wsie_zakupy', W) * h / Hw
        kept_now = inn - lev - hv - bk - Rv - (ti - alm) - basket_now
        P('  %s %s: utarg %.0f zl/d (%.0f/rok), udzial wsi %.2f, ludzie %.0f' % (lab, vid, h, h * y, sh, people.get(vid, 0)))
        P('     DZIS/rok: do kiesy %.0f (wozy %.0f + inne %.0f); korona danina %.0f, podatek od nadwyzki %.0f, rynek BK %.0f, renta %.0f, dziesiecina netto %.0f, koszyk %.0f; zostaje %.0f' % (inn * y, sh * h * y, O * y, lev * y, hv * y, bk * y, Rv * y, (ti - alm) * y, basket_now * y, kept_now * y))
        for gg in (1, 2):
            cs = gg / 10.0 * ((sh - TITHE) * h + O * g(l, 'mk_do_kies', W) / Ow)
            net = (sh - TITHE) * h + alm + O - cs - Rv
            P('     DS g=%d/rok: dziesiecina %.0f (jalmuzna wraca %.0f), korona %.0f, renta %.0f -> do wydania %.0f: koszyk (0.95) %.0f, skrzynia %.0f; alfa %.2f' % (gg, ti * y, alm * y, cs * y, Rv * y, net * y, 0.95 * net * y, 0.05 * net * y, 0.95 * net / (h / PHI)))

# ---------------- F3 krolestwa (KS, ostatnie 28 dob): kcrown.txt + d2/bases.json
kc = {}
blk = None
for line in open(HERE + 'kcrown.txt', encoding='utf-8'):
    if line.startswith('==='):
        blk = line.split()[1]; continue
    if blk != 'ks' or '|' not in line or line.startswith('krolestwo') or line.startswith('RAZEM'): continue
    left = line.split('|')[0]
    m = re.match(r'(.+?)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s*$', left)
    if not m: continue
    nm = m.group(1).strip(); v = [float(x) for x in m.groups()[1:]]
    kc[nm] = dict(wpl=v[0], zap=v[1], P1=v[2], t184=v[3], zwrot=v[4], renty=v[5], W5=v[6], W5b=v[7], kred=v[8], dKred=v[9])
B = json.load(open(HERE + 'd2/bases.json'))
W = 'last28'; l = 'ks'
tot = defaultdict(float)
for k, b in B.items():
    for c in ('coffer_spare', 'earn_T', 'earn_C', 'over_res', 'to_purse', 'handed', 'd_staly', 'valve_crown'):
        tot[c] += b.get(c, 0.0) or 0.0
levv = -g(l, 'v_korona', W); levt = g(l, 'kd_danina', W) - levv
sales_w = g(l, 't_lud_zapl', W) + g(l, 'mk_miasta', W) + g(l, 'kl_wsie_zakupy', W) - g(l, 'bk_rynek_wsi', W)
mov_w = g(l, 'p1_nadw', W)
oth_need = g(l, 'k_M1', W) + g(l, 'k_M3', W) + g(l, 'k_dary', W) + g(l, 'k_kontr', W) + g(l, 'k_rata186', W) + g(l, 'k_rep', W) + g(l, 'k_dwor', W)
zw_tot = sum(v['zwrot'] for v in kc.values())
clo_w = g(l, 'kd_clo', W); was_w = g(l, 'kd_wasale', W)
dst_tot = tot['d_staly']
P('=' * 20, 'F3 KROLESTWA (KS, 28 dob; przydzial: danina miast wg nadwyzki kas, wsi wg kies wsi, sprzedaz kas wg zarobku ludu, ruchomosci wg nadwyzki ludu, clo i pomoc lennikow wg D stalego)')
P('%-34s %8s %8s %8s %8s %8s %8s | %8s %8s' % ('krolestwo', 'dzis', 'g=1', 'g=1.5', 'g=2', 'potrz', 'renty', 'luka g2', 'kred186'))
rows_k = []
for nm, c in kc.items():
    b = B.get(nm)
    if b is None: continue
    sh_cs = (b.get('coffer_spare', 0) or 0) / tot['coffer_spare']; sh_v = (b.get('to_purse', 0) or 0) / tot['to_purse']
    sh_e = ((b.get('earn_T', 0) or 0) + (b.get('earn_C', 0) or 0)) / (tot['earn_T'] + tot['earn_C'])
    sh_m = (b.get('over_res', 0) or 0) / tot['over_res']; sh_d = (b.get('d_staly', 0) or 0) / dst_tot
    before = c['wpl'] + c['zap']
    need = c['zwrot'] + c['W5'] + oth_need * (c['zwrot'] / zw_tot)
    base_k = before - c['P1'] - levt * sh_cs - levv * sh_v - c['t184'] - was_w * sh_d
    res = []
    for gg in (1.0, 1.5, 2.0):
        s = min(0.10, 0.05 * gg)
        eTk = b.get('earn_T', 0) or 0; eCk = b.get('earn_C', 0) or 0
        # srednia waga klas ze swiata (W/R/P w zarobku miast)
        eW = g(l, 'pp_W', W) / 0.01; eR = g(l, 'pp_R', W) / 0.02; eP = g(l, 'pp_P', W) / 0.03
        wavg = (eW * 0.5 + eR * 1.0 + eP * 1.5) / (eW + eR + eP)
        tall = eTk * wavg * s + eCk * s
        aid = min(0.10, 0.05 * gg) * sales_w * sh_e
        sub = gg * 0.10 * mov_w * sh_m / 91.0
        vil = gg / 10 * (g(l, 'v_utarg', W) - g(l, 'ti_nalezne', W) + g(l, 'mk_do_kies', W)) * sh_v
        new = base_k + tall + aid + sub + vil + clo_w * sh_d + was_w * 10 / 3.0 * sh_d
        res.append(new)
    gap2 = max(0.0, need - res[2])
    rows_k.append((nm, before, res, need, c['renty'], gap2, c['kred']))
rows_k.sort(key=lambda x: -x[1])
T = [0.0] * 8
for nm, before, res, need, rent, gap2, kred in rows_k:
    P('%-34s %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f | %8.0f %8.0f' % (nm[:34], before, res[0], res[1], res[2], need, rent, gap2, kred))
    for i, v in enumerate((before, res[0], res[1], res[2], need, rent, gap2, kred)): T[i] += v
P('%-34s %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f | %8.0f %8.0f' % ('RAZEM', *T))
gaps1 = [(nm, need - res[0]) for nm, before, res, need, rent, gap2, kred in rows_k if need - res[0] > 0]
gaps2 = [(nm, need - res[2]) for nm, before, res, need, rent, gap2, kred in rows_k if need - res[2] > 0]
P('luki g=1: %.0f w %d krolestwach; luki g=2: %.0f w %d: %s' % (sum(x for _, x in gaps1), len(gaps1), sum(x for _, x in gaps2), len(gaps2), ', '.join('%s %.1fk' % (n[:14], x / 1000) for n, x in gaps2)))
open(HERE + 'final/model_out.txt', 'w', encoding='utf-8').write('\n'.join(out))
print('\n'.join(out))
