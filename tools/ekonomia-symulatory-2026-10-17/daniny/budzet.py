import csv, sys, collections
f = sys.argv[1]; day = sys.argv[2]
rows = [r for r in csv.DictReader(open(f, encoding='utf-8-sig'), delimiter=';') if r['dzien']==day]
def fl(x):
    try: return float(x)
    except: return 0.0
ai = [r for r in rows if r['rodzaj'] not in ('gracz',) and r['wojna']=='1']
print('rows', len(rows), 'war AI', len(ai), 'rodzaje', collections.Counter(r['rodzaj'] for r in rows))
print('na_suficie values', collections.Counter(r['na_suficie'] for r in ai))
tot = collections.defaultdict(float); cap = collections.defaultdict(float)
for r in ai:
    for k in ['D','d_staly','d_ziemia','d_korona','pulap','zold_partii','zold_zalog','zold_karawan','renta_180','zwrot_korony','ludzi_partie','ludzi_zalogi','limit_partii']:
        tot[k]+=fl(r[k])
        if r['na_suficie'] in ('1','tak','T','True'): cap[k]+=fl(r[k])
print('ALL', {k:round(v) for k,v in tot.items()})
print('AT CEILING', {k:round(v) for k,v in cap.items()})
# by kingdom
kk = collections.defaultdict(lambda: collections.defaultdict(float))
for r in ai:
    k=r['krolestwo']
    for x in ['d_ziemia','d_korona','renta_180','zwrot_korony','zold_partii','zold_zalog','pulap']: kk[k][x]+=fl(r[x])
    kk[k]['n']+=1; kk[k]['cap']+= 1 if r['na_suficie'] in ('1','tak') else 0
for k,v in sorted(kk.items(), key=lambda t:-t[1]['d_ziemia']):
    print(k, {x:round(y) for x,y in v.items()})
