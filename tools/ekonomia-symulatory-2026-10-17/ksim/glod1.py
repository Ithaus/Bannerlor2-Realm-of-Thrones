import csv,sys,collections
p=r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30/ks-miasta.csv'
rows=list(csv.DictReader(open(p,encoding='utf-8-sig'),delimiter=';'))
by=collections.defaultdict(list)
for r in rows: by[int(r['dzien'])].append(r)
def f(x):
    try: return float(x)
    except: return 0.0
print('day mk  want got ungold nogoods else purse  got% ungold% nogoods% else%')
for d in sorted(by):
    for mk in ('Westeros','Essos'):
        rr=[r for r in by[d] if r['rynek']==mk]
        W=sum(f(r['wsie_chciane_szt_7d']) for r in rr); G=sum(f(r['wsie_kupione_szt_7d']) for r in rr)
        U=sum(f(r['wsie_bez_kiesy_szt_7d']) for r in rr); N=sum(f(r['wsie_bez_towaru_szt_7d']) for r in rr); E=sum(f(r['wsie_w_innym_miescie_szt_7d']) for r in rr)
        P=sum(f(r['kiesy_wsi_zl']) for r in rr)
        if W>0: print(d,mk[:4],int(W),int(G),int(U),int(N),int(E),int(P),'%.1f %.1f %.1f %.1f'%(100*G/W,100*U/W,100*N/W,100*E/W))
        else: print(d,mk[:4],0,0,0,0,0,int(P))
