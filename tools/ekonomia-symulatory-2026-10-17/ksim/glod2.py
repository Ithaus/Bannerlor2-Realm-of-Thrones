import csv,collections
p=r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30/ks-miasta.csv'
rows=list(csv.DictReader(open(p,encoding='utf-8-sig'),delimiter=';'))
def f(x):
    try: return float(x)
    except: return float('nan')
days=sorted({int(r['dzien']) for r in rows})
for d in days[2:]:
    rr=[r for r in rows if int(r['dzien'])==d]
    cls=collections.defaultdict(lambda:[0]*8)
    for r in rr:
        gd=f(r['zboze_dni_polki']); gi=f(r['zboze_indeks'])
        if gd!=gd: k='?'
        elif gd<1: k='A pusta<1d'
        elif gd<7: k='B 1-7d'
        elif gd<30: k='C 7-30d'
        else: k='D >=30d'
        a=cls[k]
        a[0]+=1; a[1]+=f(r['wsie_chciane_szt_7d']); a[2]+=f(r['wsie_kupione_szt_7d']); a[3]+=f(r['wsie_bez_kiesy_szt_7d']); a[4]+=f(r['wsie_bez_towaru_szt_7d']); a[5]+=f(r['wsie_w_innym_miescie_szt_7d']); a[6]+=f(r['kiesy_wsi_zl'])
        x=f(r['kiesy_wsi_zmiana_7d_zl']); a[7]+= x if x==x else 0
    print('day',d)
    TW=sum(a[1] for a in cls.values())
    for k in sorted(cls):
        a=cls[k]
        if a[1]<=0: print(' ',k,'towns',a[0],'want 0 purse',int(a[6])); continue
        print('  %-11s towns %2d want %6d (%4.1f%% of all) got %4.1f%% ungold %4.1f%% nogoods %4.1f%% else %4.1f%% purse %8d dPurse7d %8d purse/want7d %.2f'%(k,a[0],a[1],100*a[1]/TW,100*a[2]/a[1],100*a[3]/a[1],100*a[4]/a[1],100*a[5]/a[1],a[6],a[7],a[6]/a[1]))
