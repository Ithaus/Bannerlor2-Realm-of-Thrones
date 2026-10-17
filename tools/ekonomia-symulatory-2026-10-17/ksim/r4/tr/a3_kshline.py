import re, collections, statistics as S
L='D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_09-07-09.log'
rx=re.compile(r'Kupcy zywnosci \(KS-H\) dzien (\d+) \| CZYNNE \| wozow w (\d+) \(lad (\d+), morze (\d+)\), kupione (\d+) szt\. za (\d+) zl \(([^)]*)\), fracht (\d+) zl.*?dowiezione (\d+),.*?kasa celu za mala (\d+) szt\. \(zamowione (\d+)\) \| w drodze (\d+) szt\. w (\d+) wozach.*?psucie w drodze (\d+).*?z dalszych miast \(12 najblizszych bez nadwyzki\) (\d+) wozow, z zapasu kupcow \(.*?\) (\d+) wozow, (\d+) zl.*?dowiezione loty (\d+), wartosc u celu (\d+) zl, zaplata \+ fracht (\d+) zl, wynik (-?\d+) zl, mediana marzy (-?[\d.]+)%, ze strata (\d+), pominiete bez zysku (\d+) \| kasy miast z brakiem / zapas kupcow: q10 ([\d.]+), mediana ([\d.]+) \((\d+) miast\)')
rows=[]
for line in open(L,encoding='utf-8',errors='replace'):
    if 'Kupcy zywnosci (KS-H) dzien' not in line: continue
    m=rx.search(line)
    if not m: print('nomatch',line[:120]); continue
    g=m.groups()
    d=dict(day=int(g[0]),lots=int(g[1]),land=int(g[2]),sea=int(g[3]),bought=int(g[4]),gold=int(g[5]),fr=int(g[7]),arr=int(g[8]),poor=int(g[9]),ordered=int(g[10]),transit=int(g[11]),carts=int(g[12]),spoil=int(g[13]),far=int(g[14]),res=int(g[15]),resg=int(g[16]),arrlots=int(g[17]),val=int(g[18]),cost=int(g[19]),noprof=int(g[23]),q10=float(g[24]),cmed=float(g[25]),nshort=int(g[26]))
    for kv in g[6].split(','):
        k,v=kv.strip().split(' '); d['g_'+k]=int(v)
    rows.append(d)
W=[r for r in rows if 109141<=r['day']<=109168]
print('dni',len(rows),'okno',len(W))
keys=['lots','land','sea','bought','gold','fr','arr','poor','ordered','transit','carts','spoil','far','res','resg','arrlots','val','cost','noprof','nshort']
for k in keys: print('%-8s srednio %10.1f'%(k,S.mean(r[k] for r in W)))
gs=collections.Counter()
for r in W:
    for k,v in r.items():
        if k.startswith('g_'): gs[k]+=v
print({k:round(v/len(W)) for k,v in gs.most_common()})
print('poor/ordered %.1f%%  noprof/(arrlots+noprof) %.1f%%  sea share lots %.1f%%'%(100*sum(r['poor'] for r in W)/sum(r['ordered'] for r in W),100*sum(r['noprof'] for r in W)/sum(r['noprof']+r['lots'] for r in W),100*sum(r['sea'] for r in W)/sum(r['lots'] for r in W)))
print('avg transit days ~ transit/bought = %.1f'%(S.mean(r['transit'] for r in W)/S.mean(r['bought'] for r in W)))
