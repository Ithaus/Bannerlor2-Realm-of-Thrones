import csv,collections,sys
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
pday=sys.argv[1]; tday=sys.argv[2]
land={}
for r in csv.DictReader(open(D+"ks-miasta.csv",encoding="utf-8"),delimiter=';'): land[r['miasto_id']]=r['kraina']
sup=collections.defaultdict(float); kinds=collections.Counter()
for r in csv.DictReader(open(D+"ks-plan.csv",encoding="utf-8"),delimiter=';'):
    if r['dzien']!=pday: continue
    try: s=float(r['na_targ_szt_d'])
    except: continue
    sup[(r['rynek'],r['kraina'],r['towar'])]+=s
dem=collections.defaultdict(float); off=collections.defaultdict(float); sh=collections.defaultdict(float)
for r in csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';'):
    if r['dzien']!=tday: continue
    k=(r['rynek'],land[r['miasto_id']],r['towar'])
    dem[k]+=float(r['popyt_zaplacony_szt_d']); off[k]+=float(r['odbior_7d_szt_d']); sh[k]+=float(r['polka_szt'])
goods=sys.argv[3].split(',')
for g in goods:
  for mk in ['Westeros','Essos']:
    lands=sorted(set(k[1] for k in list(sup)+list(dem) if k[0]==mk and k[2]==g))
    S=sum(sup[(mk,l,g)] for l in lands); Dm=sum(dem[(mk,l,g)] for l in lands)
    imp=sum(max(0,dem[(mk,l,g)]-sup[(mk,l,g)]) for l in lands)
    print(f"{mk[:1]} {g:9s} Plan supply {S:6.0f} vs town dp {Dm:6.0f}: must cross land borders {imp:5.0f}/d ({imp/max(1,Dm):.0%} of dp)")
    rows=[(l,sup[(mk,l,g)],dem[(mk,l,g)],off[(mk,l,g)],sh[(mk,l,g)]) for l in lands]
    rows.sort(key=lambda x:-(x[1]-x[2]))
    print("    ", "; ".join(f"{l} s{s:.0f}/d{d:.0f}/o{o:.0f}/sh{h:.0f}" for l,s,d,o,h in rows if s>5 or d>5))
