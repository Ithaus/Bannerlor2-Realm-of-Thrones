import csv,collections
p=r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30/ks-miasta.csv'
rows=list(csv.DictReader(open(p,encoding='utf-8-sig'),delimiter=';'))
def f(x):
    try: return float(x)
    except: return float('nan')
W=[109144,109151,109158,109165]
for mk in ('Westeros','Essos'):
    cls=collections.defaultdict(lambda:[0]*7)
    for r in rows:
        if int(r['dzien']) not in W or r['rynek']!=mk: continue
        gd=f(r['zboze_dni_polki'])
        k='A <1d' if gd<1 else 'B 1-7d' if gd<7 else 'C 7-30d' if gd<30 else 'D >=30d'
        a=cls[k]; a[0]+=1/len(W)
        for i,c in enumerate(['wsie_chciane_szt_7d','wsie_kupione_szt_7d','wsie_bez_kiesy_szt_7d','wsie_bez_towaru_szt_7d','wsie_w_innym_miescie_szt_7d','kiesy_wsi_zl']): a[i+1]+=f(r[c])/len(W)
    T=sum(a[1] for a in cls.values())
    print(mk)
    for k in sorted(cls):
        a=cls[k]
        print('  %-8s towns %4.1f want/d %6.0f (%4.1f%%) got %4.1f ungold %4.1f nogoods %4.1f else %4.1f purse %7.0f purse/want_d %.1f d'%(k,a[0],a[1]/7,100*a[1]/T,100*a[2]/a[1],100*a[3]/a[1],100*a[4]/a[1],100*a[5]/a[1],a[6],a[6]/(a[1]/7)))
# glut/empty town examples at last day
last=max(int(r['dzien']) for r in rows)
rr=[r for r in rows if int(r['dzien'])==last]
rr.sort(key=lambda r:f(r['zboze_dni_polki']))
print('empty examples:',[(r['miasto'],r['zboze_indeks'],r['zboze_polka_szt'],r['wsie_chciane_szt_7d'],r['wsie_bez_towaru_szt_7d'],r['wsie_bez_kiesy_szt_7d']) for r in rr[:8]])
print('glut examples:',[(r['miasto'],r['zboze_indeks'],r['zboze_polka_szt'],r['zboze_dni_polki'],r['wsie_chciane_szt_7d'],r['wsie_bez_kiesy_szt_7d'],r['kiesy_wsi_zl']) for r in rr[-8:]])
