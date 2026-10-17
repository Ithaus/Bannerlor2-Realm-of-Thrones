# Model pieniadza DS (daniny sredniowieczne) - zasady ZAIMPLEMENTOWANE w galezi w-toku/noc-ds, czytane z kodu C#, nie tylko z projektu.
# v2 (17.10, po recenzji): F1 - roboty gminy (krok 5 wydaje caly budzet: towar jak dzis, reszta place ludu miast targowych 3/4 + material kas 1/4), K5 przy samym F1
# (skarbiec ponad zapas wojny przed Bankiem; poduszka = raty Banku 28 dob); F3 - podstawa pomocy = wlasna sprzedaz kas, ciecie K3 zostaje (bez +zwrotu), firma korony
# minus pomoc (g = 1) ponad dawna danine miast (tylko krolestwa w wojnie), czesc pana z oszczednosci ludu placi lud (kasy jej nie placa), podatek od skarbca ludu (czesc
# korony i kas) zniesiony, drugi rzad: lud wydaje zatrzymane (30% na towar do kas, 70% place ludu - 184 od nich). v1 (przed recenzja) - dsmodel_v1.py.
# Wejscie: ds_<run>.json z dsparse.py (te same trzy logi co model.py) + plon-wsi.csv (wlasnosc wsi) + ks-budzety.csv (utarg wsi KS - proxy udzialu krolestw).
# Uzycie: python -I dsmodel.py   -> wynik na ekran i do model_ds_out.txt w tym folderze. Tylko liczy.
import csv
import json
import math
import os
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__)) + os.sep
CSV_KS = 'D:/Backup-Bannerlord/logi-testow/2026-10-17_06-07-51/'
CSV_JEFF = 'D:/Backup-Bannerlord/logi-jeffa/2026-10-16/2026-10-16_10-14-41/'
RUNS = (('ks', 'KS r3 (06-07-51)', CSV_KS), ('base', 'BAZA (19-02-22)', CSV_KS), ('jeff', 'JEFF 15/16.10', CSV_JEFF))

# ---------------------------------------------------------------- stale z kodu (Settings.cs, domyslne)
TITHE = 0.05            # TitheVillageShare (TempleTithe tak): dziesiecina 5% oddanego utargu, najwyzej czesc wsi (DsVillage.DuesAtCart -> TitheVillageAtCart)
WAR_SHARE = 0.10        # DsWarShareVillage: W4 = g x 0.10 x Ramp x (czesc wsi - dziesiecina nominalna); markietani: g x 0.10 x Ramp x czesc wsi
G_WAR, G_WARTAX = 1.0, 1.5   # DsVillage.G (F1): wojna 1, z polityka War Tax 1.5, pokoj 0; F3 - g z nadania (DsSeason)
CHEST_DAYS, SPEND_DAYS, BK_SHARE, RAMP_DAYS = 21, 15, 0.5, 28
NV_HEAD = 0.07          # VillagePurchasePerHead (koszyk dnia = stara lista)
RENT_HEAD_Y = 4.0       # VillageDuesPerHeadYear
# F3
AIDES_RATE, AIDES_MAX = 0.05, 0.10
TALL_RATE, TALL_MAX, TALL_MAX_POL = 0.05, 0.10, 0.15
MOV_RATE, QUARTER = 0.10, 91
CUST_MULT, VASSAL_WAR, VASSAL_OLD = 2.0, 10.0, 3.0
GRANT_STEP, GRANT_MAX, RENTS_KEEP = 0.5, 2.0, 0.5
LOAN_DAYS_OLD, LOAN_DAYS_PLEDGED, BANK_FLOOR = 180.0, 365.0, 2000000.0
REFUND = 0.70
LIFE_SHARE, COURT_SHARE = 0.75, 0.80   # Podstawa robocizny: zolnierze (75%), dwor (80%) - v2: NoteSales bierze to samo (wlasna sprzedaz kasy)
WORKS_WAGE = 0.75       # roboty gminy (DsWorks): 3/4 place ludu miasta targowego, 1/4 material do kasy
LAG_B, LAG_W = 21, 28   # obieg przez miasta: B - zakupy wsi w miastach (towar), W - roboty gminy (place ludu, potem wydatki ludu) - dluzsze opoznienie
FOLK_GOODS = 0.30       # lud: wydatek z nadwyzki 70% place (inni z ludu), 30% towar (kasy) - TownFolk.Live krok 5
K5_DAYS = 28            # K5: poduszka skarbca = raty Banku z 28 dob (CrownBorrow.InstalmentsAhead)

out = []


def P(*a):
    out.append(' '.join(str(x) for x in a))


def k1(x):
    return '%.1f' % (x / 1000.0)


# ---------------------------------------------------------------- wejscie
def load(run):
    j = json.load(open(HERE + 'ds_%s.json' % run))
    w = {int(d): v for d, v in j['w'].items()}
    k = {int(d): v for d, v in j['k'].items()}
    days = sorted(d for d in w if len(w[d]) > 40 and d in k and len(k[d]) > 20)
    return w, k, days


def mean(w, days, key, default=0.0):
    v = [w[d][key] for d in days if key in w[d]]
    return sum(v) / len(v) if v else default


def village_share(csvdir):
    """Udzial krolestw w utargu wsi: wlasnosc wsi z plon-wsi.csv (ostatnia doba), utarg na wies z ks-budzety.csv KS (doba 109179; brak - ludzie)."""
    own, ppl = {}, {}
    with open(csvdir + 'plon-wsi.csv', encoding='utf-8', errors='replace') as h:
        r = csv.reader(h, delimiter=';'); hd = next(r)
        for row in r:
            x = dict(zip(hd, row))
            own[x['wies_id']] = x['krolestwo']
            try:
                ppl[x['wies_id']] = float(x['ludzie'])
            except ValueError:
                pass
    ut = {}
    with open(CSV_KS + 'ks-budzety.csv', encoding='utf-8', errors='replace') as h:
        r = csv.reader(h, delimiter=';'); hd = next(r)
        for row in r:
            x = dict(zip(hd, row))
            if x.get('rodzaj') != 'wies' or x.get('dzien') != '109179':
                continue
            try:
                ut[x['osada_id']] = float(x['utarg_28d_zl_d'])
            except ValueError:
                pass
    tot_ut = sum(ut.values()); tot_p = sum(ppl.values())
    per_p = tot_ut / tot_p if tot_p > 0 else 0
    s = defaultdict(float)
    for v, kname in own.items():
        s[kname] += ut.get(v, ppl.get(v, 0) * per_p)
    t = sum(s.values())
    return {kname: x / t for kname, x in s.items()} if t > 0 else {}


# ---------------------------------------------------------------- F1: wies (swiat)
def f1_world(w, days):
    m = lambda key: mean(w, days, key)
    H = m('u_oddane'); Pv = m('v_utarg'); prz = m('v_przelewy'); zw = m('v_166'); ksh = m('v_ksh')
    mk = m('mk_do_kies') or m('mk_wsie')
    lev = -m('v_korona'); hv = m('hv_pobrano'); hvc = m('hv_korony'); hvl = m('hv_glowy'); hvt = m('hv_miasta') + m('hv_zamki')
    w7v = m('w7_wsie'); bk = -m('bk_rynek_wsi'); lst = m('kl_wsie_zakupy'); p5 = m('p5v_kupione'); lo = m('lo_wsi')
    rent = -m('v_renty'); ti_paid = m('ti_wsie'); ti_alm = m('ti_jalm_wsi')
    nv = m('kl_wsie_chciane')
    return dict(H=H, Pv=Pv, prz=prz, zw=zw, ksh=ksh, mk=mk, lev=lev, hv=hv, hvc=hvc, hvl=hvl, hvt=hvt, w7v=w7v, bk=bk, lst=lst, p5=p5, lo=lo, rent=rent,
                ti_net=ti_paid - ti_alm, nv=nv, inflow=Pv + prz + zw + ksh + mk, stan=m('v_stan'), med=m('v_med'), pod1000=m('v_pod1000'),
                world=m('sw_razem'))


def war_share_today(kd, share):
    """Czesc utargu wsi w krolestwach w wojnie (g > 0) i sredni g (War Tax - pulap 184 = 6%)."""
    s_war = 0.0; s_g = 0.0
    for name, x in kd.items():
        sh = share.get(name, 0.0)
        if x.get('war', 0):
            g = G_WARTAX if x.get('cap184', 0) >= 0.055 else G_WAR
            s_war += sh; s_g += sh * g
    return s_war, s_g


def f1_purse_sim(V, gshare, chest, start, days=120, absorb='A'):
    """Kiesy wsi swiata dzien po dniu od wlaczenia DS (rampa 28 dob). absorb: 'A' - zakupy wsi ograniczone towarem jak dzis (rynek BK, P5, lista - stale),
    'B' - krok 5 wydaje caly budzet (towar zawsze jest; obieg przez miasta jak w projekcie z koszykiem KS). Zwraca liste (doba, kiesy, wydatek kroku 5, W4)."""
    Pk = start; res = []
    for t in range(0, days + 1):
        r = min(1.0, t / float(RAMP_DAYS))
        w4 = r * WAR_SHARE * gshare * (max(0.0, V['Pv'] - TITHE * V['H']) + V['mk'])
        old = (1 - r) * (V['lev'] + V['hv'] + V['w7v'])
        step5 = r * max(0.0, Pk - chest) / SPEND_DAYS
        buy_bk = min(V['bk'], (1 - r) * V['bk'] + BK_SHARE * step5)
        buy_p5 = min(V['p5'], (1 - r) * V['p5'] + (1 - BK_SHARE) * step5)
        works = 0.0
        if absorb == 'A':
            # rynek BK i P5 kupuja tyle, ile dzis (towar z nadwyzka podazy, lista dostatku) - pod warunkiem budzetu; stara lista wsi bez zmian
            spend = buy_bk + buy_p5
        else:
            # B: krok 5 wydaje caly budzet (towar zawsze jest); W (v2, kod): towar jak w A, reszta budzetu - roboty gminy (DsWorks), niewydany budzet BK - nastepnego dnia
            spend = (1 - r) * (V['bk'] + V['p5']) + step5
            if absorb == 'W':
                works = max(0.0, spend - buy_bk - buy_p5)
        dP = V['inflow'] - V['rent'] - V['ti_net'] - V['lst'] - V['lo'] - old - w4 - spend
        res.append((t, Pk, spend, w4, works))
        Pk = max(0.0, Pk + dP)
    return res


# ---------------------------------------------------------------- F1: korona i panowie na krolestwo (doba po dobie)
def f1_kingdoms(w, k, days, share, r=1.0, k5=True):
    """Na krolestwo i dobe: wplyw dnia po F1 (danina wsi x (1 - r) + W4 x r), zwrot zoldu (pierwszy z wydatkow wojska) i Bank, renty, panowie.
    Danina wsi i W4 przydzielone wedlug udzialu utargu wsi krolestw w wojnie tej doby. Czesc korony podatku od nadwyzki wsi szla do zapasu (FolkGuardCrownToReserve)."""
    rows = defaultdict(lambda: defaultdict(float))
    kd_bank_old = kd_bank_new = kd_n = 0
    free = {}                                    # K5 (v2): skarbiec ponad zapas wojny (od pierwszej doby okna), zuzywany na brak zwrotu
    for d in days:
        kd = k[d]; ww = w[d]
        lev = -ww.get('v_korona', 0.0); hvc = ww.get('hv_korony', 0.0); hvl = ww.get('hv_glowy', 0.0); w7v = ww.get('w7_wsie', 0.0)
        H = ww.get('u_oddane', 0.0); Pv = ww.get('v_utarg', 0.0); mk = ww.get('mk_do_kies', ww.get('mk_wsie', 0.0))
        base4 = WAR_SHARE * (max(0.0, Pv - TITHE * H) + mk)
        s_war, _ = war_share_today(kd, share)
        s_all = sum(share.get(n, 0) for n in kd) or 1.0
        for name, x in kd.items():
            if 'wpl' not in x:
                continue
            sh = share.get(name, 0.0)
            war = x.get('war', 0)
            g = (G_WARTAX if x.get('cap184', 0) >= 0.055 else G_WAR) if war else 0.0
            lev_k = lev * sh / s_war if war and s_war > 0 else 0.0
            w4_k = g * base4 * sh
            inc = x['wpl'] + x['zapas']
            pre = x['rata186'] + x['dar_out'] + x['kontr'] + x.get('wezw', 0)
            need = pre + x['zwrot_nal'] + x['W5'] + x['M1'] + x['M3'] + x.get('dwor', 0)
            inc2 = inc - r * lev_k + r * w4_k
            bank_old = x['kred186']
            bank_new = max(0.0, x['zwrot_nal'] - max(0.0, inc2 - pre)) if war else 0.0
            tr = 0.0
            if k5 and bank_new > 0:
                if name not in free:
                    free[name] = max(0.0, x.get('skarb', 0) - max(x.get('rez', 0), 500000.0) - K5_DAYS * x.get('rata186', 0))
                tr = min(bank_new, free[name]); free[name] -= tr; bank_new -= tr
            # stary kredyt dnia (zwrot ponad wplyw przy dzisiejszych wplywach) - model liczy go tak samo, zeby roznica byla czysta
            bank_old_m = max(0.0, x['zwrot_nal'] - max(0.0, inc - pre)) if war else 0.0
            rents2 = max(0.0, x['renty'] + (inc2 - inc)) if x['renty'] > 0 or inc2 > inc else 0.0
            dl = (rents2 - x['renty']) - r * (hvl * sh / s_all + w7v * sh / s_all)
            R = rows[name]
            R['n'] += 1; R['war'] += war; R['inc'] += inc; R['inc2'] += inc2; R['need'] += need; R['rents'] += x['renty']; R['rents2'] += rents2
            R['lev'] += lev_k; R['w4'] += w4_k; R['hvc'] += r * hvc * sh / s_all
            R['bank_log'] += bank_old; R['bank_m0'] += bank_old_m; R['bank_m1'] += bank_new; R['lords'] += dl; R['k5'] += tr
            R['skarb'] += x.get('skarb', 0); R['dlug'] += x.get('dlug186', 0)
            R['bankdays0'] += 1 if bank_old_m > 0.5 else 0; R['bankdays1'] += 1 if bank_new > 0.5 else 0
            if war:
                kd_n += 1; kd_bank_old += 1 if bank_old > 0 else 0; kd_bank_new += 1 if bank_new > 0.5 else 0
    for R in rows.values():
        n = R['n']
        for key in list(R.keys()):
            if key not in ('n', 'war', 'bankdays0', 'bankdays1'):
                R[key] /= n
    return rows, kd_n, kd_bank_old, kd_bank_new


def bank_sim(rows, days_list=(28, 56, 90), extra=None):
    """Dlug koron w Banku (186) po N dobach: start = dlug na koniec logu, kredyt dnia = srednia modelu, rata = 1/182 najwiekszego dlugu (najwyzej 30% wplywu),
    odsetki 25% rocznie. Zwraca {doba: (dlug bez DS, dlug z DS)}."""
    res = {}
    for key in ('bank_m0', 'bank_m1'):
        debt = {n: R['dlug'] for n, R in rows.items()}
        peak = dict(debt)
        for t in range(1, max(days_list) + 1):
            r = min(1.0, t / float(RAMP_DAYS)) if key == 'bank_m1' else 0.0
            for n, R in rows.items():
                lend = R['bank_m0'] + r * (R['bank_m1'] - R['bank_m0']) if key == 'bank_m1' else R['bank_m0']
                debt[n] = debt[n] * (1 + 0.25 / 364) + lend
                peak[n] = max(peak[n], debt[n])
                pay = min(peak[n] / 182.0, 0.3 * max(1.0, R['inc2' if key == 'bank_m1' else 'inc']), debt[n])
                debt[n] -= pay
                if debt[n] < 1:
                    debt[n] = 0; peak[n] = 0
            if t in days_list:
                res.setdefault(t, {})[key] = sum(debt.values())
    return res


# ---------------------------------------------------------------- F3: korona na krolestwo (nadanie g, kaskada K5 -> L1 -> Bank)
def f3_kingdoms(w, k, days, share, V):
    m = lambda key: mean(w, days, key)
    # bazy swiata
    S = m('pr_lud') + m('pr_wsie') + m('pr_zycie') + m('pr_dwor') + m('pr_sale')   # v2: DsTowns.NoteSales = (1 - svc) life + (1 - cs) court + sale + lud + wsie (wlasna sprzedaz kasy)
    S_v1 = m('pr_lud') + m('pr_wsie') + m('pr_zycie') / LIFE_SHARE + m('pr_dwor') / COURT_SHARE + m('pr_sale')
    S_spec = m('t_lud_zapl') + m('mk_miasta') + m('kl_wsie_zakupy') - m('bk_rynek_wsi')                    # baza projektu 9.5 (bez partii i karawan)
    eW, eR, eP, eC = m('pp_W') / 0.01, m('pp_R') / 0.02, m('pp_P') / 0.03, m('pp_podz') / 0.03
    E = 0.5 * eW + eR + 1.5 * eP + eC
    Mfolk = m('p1_nadw')
    Mcof = max(0.0, m('nk_kasy') - 1.5 * m('nk_zapas')) + max(0.0, m('nk_zkasy') - 1.5 * m('nk_zzapas'))
    base4 = WAR_SHARE * (max(0.0, V['Pv'] - TITHE * V['H']) + V['mk'])
    lev_t = m('kd_danina') + m('v_korona')            # danina miast 1%/d (kd_danina = miasta + wsie)
    cust = m('kd_clo'); vas = m('kd_wasale'); owncut = m('kd_owncut')
    cap_k = m('b_kap')
    # srednie na krolestwo
    K = defaultdict(lambda: defaultdict(float)); n = defaultdict(int)
    for d in days:
        for name, x in k[d].items():
            if 'wpl' not in x:
                continue
            n[name] += 1
            for key in ('wpl', 'zapas', 'P1', 't184', 'rata186', 'dar_out', 'kontr', 'wezw', 'zwrot_nal', 'W5', 'M1', 'M3', 'dwor', 'renty', 'kred186', 'skarb', 'dlug186', 'war'):
                K[name][key] += x.get(key, 0)
            K[name]['w5need'] += x.get('w5need', 0)
            K[name]['s184'] += x.get('s184', 0); K[name]['cap184'] = max(K[name]['cap184'], x.get('cap184', 0))
            K[name]['rez'] = x.get('rez', K[name]['rez'])
    for name in K:
        for key in list(K[name].keys()):
            if key not in ('cap184', 'rez'):
                K[name][key] /= n[name]
    # przydzialy
    tot_wpl = sum(max(0, x['wpl'] - x['P1'] - x['t184']) for x in K.values()) or 1
    tot_war_wpl = sum(max(0, x['wpl'] - x['P1'] - x['t184']) for x in K.values() if x['war'] > 0.5) or 1
    tot_zw = sum(x['zwrot_nal'] for x in K.values()) or 1
    # zarobek wazony krolestwa = 184 / s (pobor 184 = s x zarobek wazony); oszczednosci ponad zapas ~ P1 / s (stawka P1 = 2.5%/d x s / 3%);
    # krolestwa z s = 0 (luz, pokoj) - z udzialu "nasze bez P1 i 184" (zawor, clo, powinnosci) w proporcji krolestw z s > 0
    own_of = {nm: max(0.0, x['wpl'] - x['P1'] - x['t184']) for nm, x in K.items()}
    ek = {nm: (x['t184'] / x['s184'] if x['s184'] > 0.001 and x['t184'] > 0 else None) for nm, x in K.items()}
    mk_ = {nm: (x['P1'] / x['s184'] if x['s184'] > 0.001 and x['P1'] > 0 else None) for nm, x in K.items()}
    for dct in (ek, mk_):
        ok = [nm for nm, v in dct.items() if v is not None]
        ratio = sum(dct[nm] for nm in ok) / max(1.0, sum(own_of[nm] for nm in ok))
        for nm, v in list(dct.items()):
            if v is None:
                dct[nm] = own_of[nm] * ratio
    tot_ek = sum(ek.values()) or 1
    tot_mk = sum(mk_.values()) or 1
    rows = {}
    for nm, x in K.items():
        war = x['war'] > 0.5
        own = max(0, x['wpl'] - x['P1'] - x['t184'])
        cust_k = cust * own / tot_wpl; vas_k = vas * own / tot_wpl
        levt_k = lev_t * own / tot_war_wpl if war else 0.0
        levv_k = V['lev'] * share.get(nm, 0) if war else 0.0
        Sk = S * ek[nm] / tot_ek; Ek = E * ek[nm] / tot_ek; Mk = (Mfolk + Mcof) * mk_[nm] / tot_mk; Vk = base4 * share.get(nm, 0)
        cap = TALL_MAX_POL if x['cap184'] >= 0.055 else TALL_MAX
        inc = x['wpl'] + x['zapas'] - x['P1'] - x['t184'] - levt_k - levv_k - vas_k - cust_k + (cust_k * CUST_MULT + vas_k * VASSAL_WAR / VASSAL_OLD if war else cust_k + vas_k)
        uplift = 0.0                                                 # v2: ciecie K3 zostaje przy firmach (recenzja) - zwrot jak dzis
        # v2: firma korony minus pomoc od sprzedazy przy g = 1 ponad dawna danine miasta (tylko krolestwa w wojnie w oknie - DsTowns.NoteSales liczy doby wojny)
        aidcut = max(0.0, min(AIDES_MAX, AIDES_RATE) * Sk - levt_k) if war else 0.0
        inc -= aidcut
        refund = x['zwrot_nal'] + uplift
        pre = x['rata186'] + x['dar_out'] + x['kontr'] + x['wezw']
        gap_res = max(0.0, x['rez'] - x['skarb']) / QUARTER if x['rez'] > 0 else 0.0
        need = pre + refund + x['w5need'] + RENTS_KEEP * x['renty'] + gap_res

        def Y(g):
            return min(AIDES_MAX, g * AIDES_RATE) * Sk + min(cap, g * TALL_RATE) * Ek + g * MOV_RATE * Mk / QUARTER + g * Vk
        if war:
            g = GRANT_MAX
            x_ = GRANT_STEP
            while x_ <= GRANT_MAX + 1e-9:
                if Y(x_) >= need - inc:
                    g = x_; break
                x_ += GRANT_STEP
            if x['cap184'] >= 0.055:
                g = min(GRANT_MAX, g + GRANT_STEP)
        else:
            g = 0.0
        y = Y(g)
        y_aid = min(AIDES_MAX, g * AIDES_RATE) * Sk; y_tal = min(cap, g * TALL_RATE) * Ek; y_mov = g * MOV_RATE * Mk / QUARTER; y_vil = g * Vk
        inc3 = inc + y
        avail = max(0.0, inc3 - pre)
        gap = max(0.0, refund - avail) if war else 0.0
        w5paid = min(x['w5need'], y, max(0.0, inc3 - pre - refund))
        rest = max(0.0, inc3 - pre - refund - w5paid - x['M1'] - x['M3'] - x['dwor'])
        free = max(0.0, x['skarb'] - max(x['rez'], 500000) - K5_DAYS * x['rata186'])   # v2 (DsSeason.TreasuryForRefund): bez calego dlugu, tylko raty Banku 28 dob
        room = 0.5 * g * MOV_RATE * Mk + 45.5 * g * Vk              # L1: srednio pol wymiaru dziesiatej niezebrane + W4 do konca kwartalu
        tr_days = free / gap if gap > 0 else float('inf')
        rows[nm] = dict(war=war, g=g, inc=inc, y=y, y1=Y(1.0), need=need, refund=refund, uplift=uplift, gap=gap, free=free, tr_days=tr_days, room=room,
                        rents=x['renty'], rents3=rest, before=x['wpl'] + x['zapas'], w5need=x['w5need'], w5paid=w5paid, vas_extra=vas_k * (VASSAL_WAR / VASSAL_OLD - 1) if war else 0,
                        bank_log=x['kred186'], limit=LOAN_DAYS_PLEDGED * (x['wpl'] + x['zapas']), dlug=x['dlug186'],
                        y_aid=y_aid, y_tal=y_tal, y_mov=y_mov, y_vil=y_vil, p1=x['P1'], t184=x['t184'], aidcut=aidcut, levt=levt_k, s_tal=min(cap, g * TALL_RATE))
    bases = dict(S=S, S_v1=S_v1, S_spec=S_spec, E=E, M=Mfolk, Mcof=Mcof, V=base4, lev_t=lev_t, cust=cust, vas=vas, owncut=owncut, pool=max(0.0, cap_k - BANK_FLOOR) / 60.0,
                 hl_kor=m('hl_korony'), hl_kas=m('hl_miasta') + m('hl_zamki'), hl_pan=m('hl_glowy'), w7_lud=m('w7_lud'))
    return rows, bases


# ---------------------------------------------------------------- przebieg
summary = {}
for run, lab, csvdir in RUNS:
    w, k, days = load(run)
    share = village_share(csvdir)
    win = days[-28:]
    P('=' * 30, lab, '- doby %d-%d (%d), okno = ostatnie 28 dob %d-%d' % (days[0], days[-1], len(days), win[0], win[-1]))
    V = f1_world(w, win)
    s_war_m = sum(war_share_today(k[d], share)[0] for d in win) / len(win)
    s_g_m = sum(war_share_today(k[d], share)[1] for d in win) / len(win)
    nv = V['nv'] if V['nv'] > 0 else 71544.0          # KS zastapil stara liste koszykiem - koszyk dnia z BAZY (te same wsie)
    rent_d = max(V['rent'], RENT_HEAD_Y / 364.0 * nv / NV_HEAD)
    chest = CHEST_DAYS * (nv + rent_d)
    w4 = WAR_SHARE * s_g_m * (max(0.0, V['Pv'] - TITHE * V['H']) + V['mk'])
    P('F1 WIES (swiat, zl/d): utarg oddany %s, czesc wsi %s, inne wplywy %s (przelewy %s, zwolnieni %s, markietani %s, KS-H %s) -> wplyw do kies %s' % (
        k1(V['H']), k1(V['Pv']), k1(V['prz'] + V['zw'] + V['mk'] + V['ksh']), k1(V['prz']), k1(V['zw']), k1(V['mk']), k1(V['ksh']), k1(V['inflow'])))
    P('   dzis: danina korony od kiesy %s, podatek od nadwyzki %s (korona %s do zapasu, panowie %s, kasy %s), W7 ze wsi %s | rynek BK %s, P5 %s, lista wsi %s, pozyczki %s, czynsz %s, dziesiecina netto %s' % (
        k1(V['lev']), k1(V['hv']), k1(V['hvc']), k1(V['hvl']), k1(V['hvt']), k1(V['w7v']), k1(V['bk']), k1(V['p5']), k1(V['lst']), k1(V['lo']), k1(V['rent']), k1(V['ti_net'])))
    P('   DS po rampie: stare pobory 0 (uwalnia %s), W4 przy wozie i od markietanow %s (udzial utargu w krolestwach w wojnie %.0f%%, sredni g %.2f) -> wsie zatrzymuja %s; skrzynia swiata 21 x (koszyk %s + czynsz %s) = %s' % (
        k1(V['lev'] + V['hv'] + V['w7v']), k1(w4), 100 * s_war_m, s_g_m / s_war_m if s_war_m else 0, k1(V['lev'] + V['hv'] + V['w7v'] - w4), k1(nv), k1(rent_d), k1(chest)))
    start = w[days[-1]].get('v_stan', V['stan'])
    simA = f1_purse_sim(V, s_g_m, chest, start, absorb='A')
    simB = f1_purse_sim(V, s_g_m, chest, start, absorb='B')
    simW = f1_purse_sim(V, s_g_m, chest, start, absorb='W')
    drift_old = (w[days[-1]].get('v_stan', 0) - w[days[-29]].get('v_stan', 0)) / 28.0 if len(days) > 29 else 0
    P('   kiesy wsi (start = koniec logu %s; dryf dzis %+.1f tys./d, mediana %.0f, wsi < 1000: %.0f):' % (k1(start), drift_old / 1000, V['med'], V['pod1000']))
    for t in (0, 28, 56, 90, 120):
        a = simA[t]; c = simW[t]
        P('     doba %3d: A (stary kod: towar jak dzis, bez robot) kiesy %6s, krok 5 kupil %5s/d | W (v2: towar jak dzis + roboty gminy) kiesy %6s, krok 5 %5s/d (w tym roboty %5s/d) | W4 %s/d' % (
            t, k1(a[1]), k1(a[2]), k1(c[1]), k1(c[2]), k1(c[4]), k1(a[3])))
    dA = (simA[120][1] - simA[60][1]) / 60.0
    dW = (simW[120][1] - simW[60][1]) / 60.0
    P('   dryf doby 61-120: A %+.1f tys./d (%+.2f%%/d stanu), W %+.1f tys./d (%+.2f%%/d - bramka NDS-3a/3b +-0.2%%/d); kiesy wsi / zloto swiata w dobie 120: A %.1f%%, W %.1f%% (dzis %.1f%%; NDS-3e <= 3%%)' % (
        dA / 1000, 100 * dA / max(1, simA[60][1]), dW / 1000, 100 * dW / max(1, simW[60][1]), 100 * simA[120][1] / V['world'], 100 * simW[120][1] / V['world'], 100 * start / V['world']))
    wk = [x[4] for x in simW[61:121]]
    P('   W: roboty gminy srednio doby 61-120 %s/d = place ludu miast targowych %s + material do kas %s (towar z rynku BK i P5 jak dzis %s/d)' % (
        k1(sum(wk) / len(wk)), k1(WORKS_WAGE * sum(wk) / len(wk)), k1((1 - WORKS_WAGE) * sum(wk) / len(wk)), k1(V['bk'] + V['p5'])))
    # korona i panowie
    rows, kdn, kdb0, kdb1 = f1_kingdoms(w, k, win, share)
    rowsN, _, _, _ = f1_kingdoms(w, k, win, share, k5=False)
    T = defaultdict(float)
    for R in rows.values():
        for key, v in R.items():
            T[key] += v
    dinc = T['inc2'] - T['inc']
    D_st = mean(w, win, 'D_staly')
    P('F1 KORONA (doba >= 28, rampa 1; bez KS nic nie wraca obiegiem - wsie nie maja na co wydac): wplyw dnia + 1/450 %s -> %s (%+.1f tys., %+.1f%%): danina wsi -%s, W4 +%s; zapas: czesc korony podatku od nadwyzki wsi -%s/d' % (
        k1(T['inc']), k1(T['inc2']), dinc / 1000, 100 * dinc / T['inc'], k1(T['lev']), k1(T['w4']), k1(V['hvc'])))
    bdN = sum(R['bankdays1'] for R in rowsN.values())
    P('   potrzeba przed rentami %s (zwrot zawsze pierwszy z wydatkow wojska); renty 180 %s -> %s (%+.1f tys.); Bank na zwrot (model, wplyw dnia vs zwrot): %s -> %s zl/d, krolestwo-dob z kredytem %d -> %d z %d (%.1f%% -> %.1f%%; log: %d); '
      'K5 przy F1 (v2): skarbiec ponad zapas wojny pokrywa %s/d, bez K5 bylo %s/d i %d krolestwo-dob (%.1f%%)' % (
        k1(T['need']), k1(T['rents']), k1(T['rents2']), (T['rents2'] - T['rents']) / 1000, k1(T['bank_m0']), k1(T['bank_m1']),
        sum(R['bankdays0'] for R in rows.values()), sum(R['bankdays1'] for R in rows.values()), kdn, 100.0 * sum(R['bankdays0'] for R in rows.values()) / max(1, kdn),
        100.0 * sum(R['bankdays1'] for R in rows.values()) / max(1, kdn), kdb0, k1(T['k5']), k1(sum(R['bank_m1'] for R in rowsN.values())), bdN, 100.0 * bdN / max(1, kdn)))
    P('   panowie: renty 180 %+.1f, podatek od nadwyzki wsi -%s, W7 ze wsi -%s -> razem %+.1f tys./d (%+.1f%% D stalego %s); kasy miast i zamkow -%s/d' % (
        (T['rents2'] - T['rents']) / 1000, k1(V['hvl']), k1(V['w7v']), T['lords'] / 1000, 100 * T['lords'] / D_st if D_st else 0, k1(D_st), k1(V['hvt'])))
    # doby rampy (statycznie, A: bez obiegu; B: obieg przez dzisiejsze pobory miast z opoznieniem 21 dob, udzial korony 0.59)
    sc = 0.59
    line = []
    yB = yW = 0.0
    for t in range(1, 121):
        r = min(1.0, t / float(RAMP_DAYS))
        xB = simB[t][2] - (V['bk'] + V['p5']) * (1 - r)          # dodatkowy wydatek wsi w miastach (B)
        xW = simW[t][2] - (V['bk'] + V['p5']) * (1 - r)          # W: to samo zloto, ale roboty gminy - place ludu (dluzsza droga do poborow miast)
        yB += (xB - yB) / float(LAG_B)
        yW += (xW - yW) / float(LAG_W)
        if t in (28, 56, 90, 120):
            dcA = -r * T['lev'] + r * T['w4']
            dcB = dcA + sc * 0.98 * yB
            dcW = dcA + sc * 0.98 * yW
            dlA = r * (T['rents2'] - T['rents']) / max(1e-9, 1.0) * 1.0 - r * (V['hvl'] + V['w7v'])
            dlB = dlA + (1 - sc) * 0.98 * yB + sc * 0.98 * yB
            dlW = dlA + (1 - sc) * 0.98 * yW + sc * 0.98 * yW
            line.append('doba %d: korona A %+.1f / W %+.1f, panowie A %+.1f / W %+.1f' % (t, dcA / 1000, dcW / 1000, dlA / 1000, dlW / 1000))
            summary.setdefault(run + '_ramp', {})[t] = (dcA, dcW, dlA, dlW)
    P('   rampa (tys. zl/d; A - stary kod bez robot, W - v2 z robotami gminy, obieg przez dzisiejsze pobory miast, udzial korony 0.59, opoznienie %d dob): ' % LAG_W + '; '.join(line))
    bs = bank_sim(rows)
    P('   dlug koron w Banku (186; start %s): ' % k1(T['dlug']) + ', '.join('doba %d: bez DS %s, z DS %s' % (t, k1(bs[t]['bank_m0']), k1(bs[t]['bank_m1'])) for t in sorted(bs)))
    # tabela krolestw F1
    P('   %-34s %7s %7s %7s %7s %7s %7s %7s %7s %6s' % ('krolestwo (F1, 28 dob)', 'wplyw', 'po F1', 'danina', 'W4', 'potrz', 'renty', 'po F1', 'Bank/d', 'B-dob'))
    for nm, R in sorted(rows.items(), key=lambda kv: -kv[1]['inc']):
        P('   %-34s %7s %7s %7s %7s %7s %7s %7s %7s %3d->%-3d' % (nm[:34], k1(R['inc']), k1(R['inc2']), k1(R['lev']), k1(R['w4']), k1(R['need']), k1(R['rents']), k1(R['rents2']),
                                                              k1(R['bank_m1']), R['bankdays0'], R['bankdays1']))
    worst = sorted(((R['inc2'] - R['inc']) / R['inc'] if R['inc'] else 0, nm) for nm, R in rows.items())[:5]
    P('   najwiekszy spadek wplywu dnia: ' + ', '.join('%s %.1f%%' % (nm[:22], 100 * x) for x, nm in worst))
    # F3
    rows3, B3 = f3_kingdoms(w, k, win, share, V)
    T3 = defaultdict(float)
    for R in rows3.values():
        for key in ('inc', 'y', 'need', 'refund', 'uplift', 'gap', 'rents', 'rents3', 'before', 'w5need', 'w5paid', 'vas_extra', 'bank_log', 'room', 'y_aid', 'y_tal', 'y_mov', 'y_vil', 'p1', 't184', 'aidcut', 'levt'):
            T3[key] += R[key]
    gs = defaultdict(int)
    for R in rows3.values():
        gs[R['g']] += 1
    P('F3 KORONA v2 (DsTowns wlaczone, po oknie 28 dob i po rampie; F1 tez wlaczone): bazy swiata zl/d - wlasna sprzedaz kas (v2) %s (v1: %s, projekt 9.5: %s), zarobek wazony %s, oszczednosci ponad zapas %s + kasy ponad 1.5 zapasu %s, W4 na g %s; ciecie K3 dzis %s - zostaje (zwrot +%s); firma korony minus pomoc g=1 ponad danine miast %s/d' % (
        k1(B3['S']), k1(B3['S_v1']), k1(B3['S_spec']), k1(B3['E']), k1(B3['M']), k1(B3['Mcof']), k1(B3['V']), k1(B3['owncut']), k1(T3['uplift']), k1(T3['aidcut'])))
    P('   dzis wplyw + 1/450 %s; F3: zwykle (firmy, clo x2, pomoc lennikow 10%%, 1/9, inne, 1/450) %s + nadanie %s = %s (%.0f%% dzis); potrzeba sesji %s (w niej zwrot %s); g krolestw: %s' % (
        k1(T3['before']), k1(T3['inc']), k1(T3['y']), k1(T3['inc'] + T3['y']), 100 * (T3['inc'] + T3['y']) / T3['before'], k1(T3['need']), k1(T3['refund']),
        ', '.join('%.1f: %d' % (g, n_) for g, n_ in sorted(gs.items()))))
    P('   renty 180 %s -> %s; W5 potrzeba %s, z garnka wplywow wojennych %s; pomoc lennikow ponad dzisiejsze 3%% (panowie) %s; brak na zwrot po nadaniu %s zl/d; Bank dzis na zwrot %s; pula Banku dla koron %s/d' % (
        k1(T3['rents']), k1(T3['rents3']), k1(T3['w5need']), k1(T3['w5paid']), k1(T3['vas_extra']), k1(T3['gap']), k1(T3['bank_log']), k1(B3['pool'])))
    folk_old = T3['p1'] + T3['t184'] + B3['hl_kor'] + B3['hl_kas']     # v2: lud placil tez czesc korony i kas podatku od skarbca ludu (zniesiony); czesc pana + W7 placi dalej (firma z oszczednosci)
    folk_new = T3['y_tal'] + T3['y_mov'] * (B3['M'] / max(1.0, B3['M'] + B3['Mcof']))
    kl = mean(w, win, 'sw_razem')
    folk_now = mean(w, win, 'kl_ovW') + mean(w, win, 'kl_ovR') + mean(w, win, 'kl_ovP') + mean(w, win, 'kl_ovC')
    dF = folk_old - folk_new
    s_avg = sum(R['s_tal'] * R['y_tal'] for R in rows3.values()) / max(1.0, T3['y_tal'])
    P('   nadanie wedlug skladnikow: pomoc od sprzedazy %s, 184 %s, dziesiata %s, W4 %s | lud miast i podzamczy: dzis P1 + 184 + podatek od skarbca (korona %s, kasy %s) %s -> 184 + dziesiata ludu %s = zatrzymuje %+.1f tys./d '
      '(czesc pana %s + W7 z ludu %s placi dalej - firma z oszczednosci ludu; oszczednosci ponad zapas %s = %.1f%% zlota swiata; bez drugiego rzedu +%.1f pkt w 92 doby)' % (
        k1(T3['y_aid']), k1(T3['y_tal']), k1(T3['y_mov']), k1(T3['y_vil']), k1(B3['hl_kor']), k1(B3['hl_kas']), k1(folk_old), k1(folk_new), dF / 1000,
        k1(B3['hl_pan']), k1(B3['w7_lud']), k1(folk_now), 100 * folk_now / kl, 100 * 92 * dF / kl))
    cof_over = max(0.0, mean(w, win, 'nk_kasy') - mean(w, win, 'nk_zapas'))
    mov_cof = T3['y_mov'] * (B3['Mcof'] / max(1.0, B3['M'] + B3['Mcof']))
    extra_cof = T3['y_aid'] - T3['levt'] - T3['aidcut'] + mov_cof + B3['hl_kas']
    back2 = FOLK_GOODS * dF                                       # drugi rzad (stan ustalony): lud wydaje zatrzymane - 30% na towar do kas
    tal2 = s_avg * (1 - FOLK_GOODS) * dF                           # ... 70% place ludu - 184 od nich do korony
    P('   kasy miast (v2): pomoc od sprzedazy %s - danina miast %s - firma korony mniejsza %s + dziesiata kas %s + utracony udzial kas w podatku od skarbca ludu %s = %+.1f tys./d wiecej z kas niz dzis '
      '(czesc pana z oszczednosci ludu placi lud, nie kasa); drugi rzad: lud wydaje zatrzymane %s/d -> do kas %s/d, 184 od plac %s/d (s sr. %.3f) -> kasy netto %+.1f tys./d; kasy ponad zapas kupcow %s -> %s dob do zapasu' % (
        k1(T3['y_aid']), k1(T3['levt']), k1(T3['aidcut']), k1(mov_cof), k1(B3['hl_kas']), extra_cof / 1000, k1(dF), k1(back2), k1(tal2), s_avg, (extra_cof - back2) / 1000, k1(cof_over),
        ('%.0f' % (cof_over / (extra_cof - back2))) if extra_cof - back2 > 0 else 'nie spada'))
    P('   lud po drugim rzedzie: zatrzymuje %+.1f tys./d (po 184 od plac i towarze do kas) = +%.1f pkt zlota swiata w 92 doby (bramka ND3-3 lud <= 12%%; dzis %.1f%%); korona + drugi rzad %+.1f tys./d' % (
        (dF - back2 - tal2) / 1000, 100 * 92 * (dF - back2 - tal2) / kl, 100 * folk_now / kl, tal2 / 1000))
    # stan ustalony wielu obrotow: lud wydaje tempo x (oszczednosci ponad zapas) dziennie, z kazdego obrotu 30% wycieka na towar do kas (reszta - place innych z ludu);
    # przyrost oszczednosci S* = dF / (tempo x 0.3), stala czasu 1 / (tempo x 0.3) dob
    for lab_, rate_ in (('1/15 (DsFolkSurplusSpendDays 15, projekt)', 1.0 / 15), ('15%/d (FolkSurplusSpendShare - jak dzis)', 0.15)):
        Sst = dF / (rate_ * FOLK_GOODS); tau = 1.0 / (rate_ * FOLK_GOODS)
        s92 = Sst * (1 - math.exp(-92.0 / tau))
        P('   lud - wiele obrotow, tempo zbytkow %s: oszczednosci +%s mln w stanie ustalonym (stala %.0f dob), w 92 doby +%s mln = +%.1f pkt zlota swiata -> lud ok. %.1f%% (ND3-3 <= 12%%)' % (
            lab_, '%.1f' % (Sst / 1e6), tau, '%.1f' % (s92 / 1e6), 100 * s92 / kl, 100 * (folk_now + s92) / kl))
        summary.setdefault(run + '_folk', {})[lab_] = (Sst, s92, 100 * (folk_now + s92) / kl)
    summary[run + '_f3'] = dict(inc=T3['inc'], y=T3['y'], before=T3['before'], rents=T3['rents'], rents3=T3['rents3'], gap=T3['gap'], extra_cof=extra_cof - back2, dF=dF - back2 - tal2,
                                aidcut=T3['aidcut'], S=B3['S'], gs=dict(gs), tal2=tal2)
    P('   %-34s %3s %7s %7s %7s %7s %7s %7s %8s %7s %7s %7s' % ('krolestwo (F3)', 'g', 'dzis', 'zwykle', 'nadanie', 'potrz', 'zwrot', 'luka', 'skarb dni', 'L1/d', 'renty', 'po F3'))
    for nm, R in sorted(rows3.items(), key=lambda kv: -kv[1]['before']):
        td = '-' if R['gap'] <= 0 else ('%.0f' % R['tr_days'] if R['tr_days'] < 9999 else '>9999')
        P('   %-34s %3.1f %7s %7s %7s %7s %7s %7s %8s %7s %7s %7s' % (nm[:34], R['g'], k1(R['before']), k1(R['inc']), k1(R['y']), k1(R['need']), k1(R['refund']), k1(R['gap']), td,
                                                                     k1(R['room'] / QUARTER), k1(R['rents']), k1(R['rents3'])))
    gaps = [(R['gap'], nm, R) for nm, R in rows3.items() if R['gap'] > 0]
    P('   luki zwrotu po nadaniu: %s w %d krolestwach: %s' % (k1(sum(x for x, _, _ in gaps)), len(gaps), ', '.join(
        '%s %s (skarbiec ponad zapas na %s dob, potem L1 do %s/d, reszta Bank)' % (nm[:16], k1(x), ('%.0f' % R['tr_days']) if R['tr_days'] < 9999 else '>9999', k1(R['room'] / QUARTER))
        for x, nm, R in sorted(gaps, reverse=True))))
    summary[run] = dict(V=V, T=dict(T), T3=dict(T3), simA=[(x[0], x[1]) for x in simA[::28]], simW=[(x[0], x[1], x[4]) for x in simW[::28]], chest=chest, w4=w4, dW=dW,
                        shareW=simW[120][1] / V['world'], bank0=sum(R['bankdays1'] for R in rowsN.values()), bank1=sum(R['bankdays1'] for R in rows.values()), kdn=kdn, k5=T['k5'])
    P()

open(HERE + 'model_ds_out.txt', 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
print('\n'.join(out))
