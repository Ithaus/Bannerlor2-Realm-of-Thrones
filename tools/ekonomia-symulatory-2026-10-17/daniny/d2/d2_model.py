# D2 - model statyczny na podstawach testu KS r3 (ostatnie 28 dob), krolestwo po krolestwie.
# Reguly D2 (wojna): robocizna c=0.12 (pokoj 0.10), danina z plonu 1/8 calego plonu chlopow (zbierana z czesci na targ), dziesiecina 1/10 czesci na targ,
# czynsz 4 zl/glowe/rok; miasta: firma 1/2 zysku kupcow po akcyzie (pan), akcyza wojenna 10% sprzedazy (korona), 184 W 1/12, R 1/6, P 1/4, podzamcze 1/6;
# subsydium 1/10 ruchomosci ludu ponad poziom normalny i panow ponad R raz na 91 dob; pomoc lennikow 1/3 dochodu z ziemi; cla x2; myto bez zmian.
import json, sys
from collections import defaultdict

W = 'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/daniny/d2/'
B = json.load(open(W + 'bases.json'))
vil = json.load(open(W + 'villages.json'))

P = dict(
    YV=364.2 / 392.5 + 1.0,      # plon calkowity / widoczny (KS: robione w domu 364.2k, ksiega produkcji wsi 392.5k zl/d wartosci bazowej)
    c=0.25, champ=0.25, tithe=0.10, estates=22.7 / (92.0 + 22.7),
    phi=0.656, toll=0.04,
    rent_world=9.6e3, x_world=67.1e3,  # czynsz staly zl/d; inne wplywy wsi (lordowie kupuja, markietani, zwolnieni, notable, fracht KS-H)
    excise=0.10, S_over_N=1.0e6 / 427.3e3, farm=0.5, castle_farm_world=2.0 / 3 * 72.8e3,
    t184_T=0.138, t184_C=1.0 / 6, sub_folk=0.10 / 91 * 0.85, sub_lords=0.10 / 91, aid=1.0 / 3,
    customs_world=2 * 10.5e3, caravan_toll_half_world=1.9e3,
    lord_folk_tax_world=14.4e3, ground_rent_world=12.2e3, excise_lord_world=3.3e3, bk_town_world=19.5e3,
    sutler_vil_world=56.8e3,
    other_need_world=19.4e3 + 16.9e3 + 2.9e3 + 2.2e3 + 6.3e3 + 24.7e3,   # dary, kontrakty, reparacje, dwor, rata 186, chleb M3
)
for a in sys.argv[1:]:
    k, v = a.split('=')
    P[k] = float(v)

def shares(c):
    # D2 v2: wszystkie daniny w naturze z czesci NA TARG (widocznej); dom i nasiona chronione
    D = c                                        # robocizna: c dni pracy na targ idzie na pole dworu (1/5 pokoj, 1/4 wojna)
    Vp = 1.0 - D
    tithe = P['tithe'] * Vp                      # dziesiecina 1/10 plonu chlopow na targ
    champ = P['champ'] * (Vp - tithe)            # danina z plonu: 1/4 reszty po dziesiecinie
    own = Vp - tithe - champ
    lord = D + champ
    return dict(lord=lord, tithe=tithe, own=own, D=D, champ=champ)

sh = shares(P['c'])
tot = defaultdict(float)
for k, v in B.items():
    for m, x in v.items():
        tot[m] += x
H = tot['handed']; N = tot['N']; EC = tot['earn_C']; ET = tot['earn_T']; VP = tot['vil_people']
out = {}
for k, v in B.items():
    h = v.get('handed', 0.0); n = v.get('N', 0.0); et = v.get('earn_T', 0.0); ec = v.get('earn_C', 0.0)
    fh = h / H if H else 0; fn = n / N if N else 0; fe = (et + ec) / (ET + EC) if ET + EC else 0
    S = P['S_over_N'] * max(0.0, n)
    # panowie: ziemia D2
    lots = sh['lord'] * (1 - P['estates']) * h
    rent = P['rent_world'] * (v.get('vil_people', 0.0) / VP if VP else 0)
    toll_v = 0.5 * P['toll'] / (1 - P['toll']) * h
    sut = sh['lord'] * (1 - P['estates']) * P['sutler_vil_world'] * fh
    farm = P['farm'] * max(0.0, n - P['excise'] * S)
    cfarm = P['castle_farm_world'] * (ec / EC if EC else 0)
    other_l = P['lord_folk_tax_world'] * fe + P['ground_rent_world'] * (et / ET if ET else 0) + P['excise_lord_world'] * (et / ET if ET else 0) \
              + P['bk_town_world'] * fn + P['caravan_toll_half_world'] * fn
    land = lots + rent + toll_v + sut + farm + cfarm + other_l
    # korona D2
    aid = P['aid'] * land
    exc = P['excise'] * S
    t184 = P['t184_T'] * et + P['t184_C'] * ec
    subf = P['sub_folk'] * v.get('over_res', 0.0)
    subl = P['sub_lords'] * v.get('G_over_R', 0.0)
    cus = P['customs_world'] * fn
    tollc = toll_v + P['caravan_toll_half_world'] * fn
    crown = aid + exc + t184 + subf + subl + cus + tollc
    refund = v.get('c_zwrot', 0.0); w5 = v.get('c_W5', 0.0) + v.get('c_W5b', 0.0) + v.get('w_m1', 0.0)
    other = P['other_need_world'] * (refund / tot['c_zwrot'] if tot['c_zwrot'] else 0)
    need = refund + w5 + other
    wages = v.get('w_zold_p', 0.0) + v.get('w_zold_z', 0.0)
    before_crown = v.get('c_wpl', 0.0) + v.get('c_zapas', 0.0)
    before_steady = v.get('w_d_ziemia', 0.0) + v.get('w_d_korona', 0.0) + v.get('own', 0.0)
    after_steady = land - aid - subl + refund + w5
    # pozyczki: mieszczanie (90 dob dochodu + 50k/miasto, z nadwyzki kas i kies ponad poziom normalny, po 1/2), Bank (pula swiata 27-40k/d)
    lendable = v.get('coffer_spare', 0.0) + 0.85 * v.get('over_res', 0.0)
    lim = 90 * crown + 50e3 * v.get('towns', 0.0)
    burgh = min(lendable, lim)
    spare_res = max(0.0, v.get('c_skarbEnd', 0.0) - max(500e3, 60 * refund))
    gap = need - crown
    days_cover = (burgh + spare_res) / gap if gap > 0 else float('inf')
    out[k] = dict(handed=h, N=n, earn=et + ec, land=land, lots=lots, farm=farm, aid=aid, exc=exc, t184=t184, subf=subf, subl=subl, cus=cus, tollc=tollc,
                  crown=crown, need=need, refund=refund, w5=w5, wages=wages, before_crown=before_crown, before_steady=before_steady,
                  after_steady=after_steady, gap=gap, burgh=burgh, spare_res=spare_res, days_cover=days_cover, towns=v.get('towns', 0),
                  before_rents=v.get('c_renty', 0.0), before_P1=v.get('c_P1', 0.0), debt186=v.get('c_dlug186', 0.0),
                  valve_l=v.get('valve_lord', 0.0), valve_c=v.get('valve_crown', 0.0), dland=v.get('w_d_ziemia', 0.0) + v.get('own', 0.0), own=v.get('own', 0.0), pulap=v.get('pulap', 0.0))
json.dump({'P': P, 'sh': sh, 'out': out}, open(W + 'd2_out.json', 'w'), indent=1)
print('udzialy widocznego (wojna):', {a: round(b, 4) for a, b in sh.items()})
print('udzialy pokoj:', {a: round(b, 4) for a, b in shares(0.20).items()})
T = defaultdict(float)
for k, o in out.items():
    for m, x in o.items():
        if isinstance(x, float) and m != 'days_cover':
            T[m] += x
print('SWIAT: ' + ', '.join('%s %.0f' % (m, T[m]) for m in ('handed', 'N', 'earn', 'lots', 'farm', 'land', 'aid', 'exc', 't184', 'subf', 'subl', 'cus', 'tollc', 'crown', 'need', 'refund', 'w5', 'wages', 'before_crown', 'before_rents', 'before_steady', 'after_steady', 'gap', 'burgh', 'spare_res', 'own', 'pulap', 'dland')))
hdr = '%-34s %7s %7s %7s %7s %7s %7s %7s | %7s %7s %7s %6s %6s | %7s %7s %5s'
print(hdr % ('krolestwo', 'wojsko', 'kor_prz', 'kor_D2', 'potrz', 'luka', 'mieszcz', 'zapas', 'st_prz', 'st_D2', 'st/wojs', 'pr%', 'D2%', 'zie_prz', 'zie_D2', 'dni'))
for k, o in sorted(out.items(), key=lambda kv: -kv[1]['wages']):
    if o['wages'] < 500 and o['need'] < 500:
        continue
    print('%-34s %7.0f %7.0f %7.0f %7.0f %7.0f %7.0f %7.0f | %7.0f %7.0f %7s %5.0f%% %5.0f%% | %7.0f %7.0f %5s' % (
        k[:34], o['wages'], o['before_crown'], o['crown'], o['need'], o['gap'], o['burgh'], o['spare_res'], o['before_steady'], o['after_steady'], '',
        100 * o['before_steady'] / max(1, o['wages']), 100 * o['after_steady'] / max(1, o['wages']), o['dland'], o['land'],
        ('%.0f' % o['days_cover']) if o['days_cover'] != float('inf') else 'inf'))
