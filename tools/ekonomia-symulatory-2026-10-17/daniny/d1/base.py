import csv,sys,collections
P=sys.argv[1]
def rd(f):
    with open(P+'/'+f,encoding='utf-8',errors='replace') as h:
        r=csv.reader(h,delimiter=';'); hd=next(r)
        for row in r:
            if len(row)<len(hd): continue
            yield dict(zip(hd,row))
def fl(x):
    try: return float(x)
    except: return 0.0
# towns
rows=list(rd('kasy-miast.csv'))
days=sorted(set(int(r['dzien']) for r in rows)); last=days[-min(28,len(days)):]
K=collections.defaultdict(lambda: collections.defaultdict(float))
for r in rows:
    if int(r['dzien']) in last:
        k=r['krolestwo']
        for c in ('kasa','zapas_kupcow','zawor_do_pana','do_korony','pobrane','doplyw_netto'):
            K[k][c]+=fl(r[c])/len(last)
        K[k]['ntown']+=1/len(last)
tot=collections.defaultdict(float)
for k in K:
    for c in K[k]: tot[c]+=K[k][c]
print('towns last28', last[0], last[-1], {c:round(v) for c,v in tot.items()})
# folk
rows=list(rd('kiesa-ludu.csv'))
fd=sorted(set(int(r['dzien']) for r in rows)); fl4=fd[-4:]
F=collections.defaultdict(lambda: collections.defaultdict(float))
for r in rows:
    if int(r['dzien']) in fl4:
        k=r['krolestwo']; t=r['rodzaj']
        for c in ('dochod','wydatki','kiesa_W','kiesa_R','kiesa_P','kasa','zapas_D4','koszyk'):
            F[k][t+'_'+c]+=fl(r[c])/len(fl4)
ft=collections.defaultdict(float)
for k in F:
    for c in F[k]: ft[c]+=F[k][c]
print('folk days',fl4,{c:round(v) for c,v in ft.items()})
# villages
pw=list(rd('plon-wsi.csv')); pd=max(int(r['dzien']) for r in pw)
vk={r['wies_id']:r['krolestwo'] for r in pw if int(r['dzien'])==pd}
vp={r['wies_id']:fl(r['ludzie']) for r in pw if int(r['dzien'])==pd}
import os
kb=list(rd('ks-budzety.csv')) if os.path.exists(P+'/ks-budzety.csv') else []; bd=sorted(set(int(r['dzien']) for r in kb)) or [0]
V=collections.defaultdict(lambda: collections.defaultdict(float))
for r in kb:
    if int(r['dzien'])==bd[-1] and r['rodzaj']=='wies':
        k=vk.get(r['osada_id'],'?'); V[k]['n']+=1; V[k]['utarg']+=fl(r['utarg_28d_zl_d']); V[k]['kiesa']+=fl(r['do_kiesy_28d_zl_d']); V[k]['ludzie']+=vp.get(r['osada_id'],0)
vt=collections.defaultdict(float)
for k in V:
    for c in V[k]: vt[c]+=V[k][c]
print('vill day',bd[-1],pd,{c:round(v) for c,v in vt.items()})
import json
json.dump({'T':K,'F':F,'V':V},open(sys.argv[2],'w'),indent=0)
