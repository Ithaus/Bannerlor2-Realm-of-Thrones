import csv, sys, collections, statistics as S
D='D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/'
land={}; name={}; mk={}
for r in csv.DictReader(open(D+'ks-miasta.csv',encoding='utf-8'),delimiter=';'):
    land[r['miasto_id']]=r['kraina']; name[r['miasto_id']]=r['miasto']; mk[r['miasto_id']]=r['rynek']
gold={}
for r in csv.DictReader(open(D+'kasy-miast.csv',encoding='utf-8'),delimiter=';'):
    gold[(int(r['dzien']),r['miasto_id'])]=(float(r['kasa']),float(r['zapas_kupcow']))
good=sys.argv[1] if len(sys.argv)>1 else 'grain'
days=[int(x) for x in (sys.argv[2].split(',') if len(sys.argv)>2 else ['109144','109151','109158','109165'])]
rows=collections.defaultdict(list)
for r in csv.DictReader(open(D+'ks-miasta-towary.csv',encoding='utf-8'),delimiter=';'):
    if r['towar']!=good or int(r['dzien']) not in days: continue
    rows[r['miasto_id']].append(r)
def f(x):
    try: return float(x)
    except: return float('nan')
out=[]
for t,rs in rows.items():
    ix=S.mean(f(r['indeks']) for r in rs); sh=S.mean(f(r['polka_szt']) for r in rs); dp=S.mean(f(r['popyt_zaplacony_szt_d']) for r in rs)
    off=S.mean(f(r['odbior_7d_szt_d']) for r in rs); inc=S.mean(f(r['w_drodze_szt']) for r in rs); kx=S.mean(f(r['kx']) for r in rs); T=f(rs[0]['cel_dni'])
    g=[gold.get((int(r['dzien']),t)) for r in rs]; g=[x for x in g if x]
    gk=S.mean(x[0] for x in g) if g else float('nan'); rv=S.mean(x[1] for x in g) if g else float('nan')
    b=max(dp,off)
    out.append((mk[t],land[t],name[t],ix,sh,dp,off,inc,kx,T,sh/max(b,1e-9),gk,rv))
out.sort(key=lambda x:(x[0],x[1],-x[3]))
print('rynek kraina miasto | ix polka popyt odbior wdrodze kx T* dni_polki | kasa zapas kasa/zapas budzet15%')
for o in out:
    bud=0.15*max(0,o[11]-o[12])
    print('%s %-11s %-22s | %.2f %7.0f %6.1f %6.1f %6.0f %.2f %4.1f %6.1f | %8.0f %8.0f %.2f %7.0f'%(o[0][0],o[1],o[2][:22],o[3],o[4],o[5],o[6],o[7],o[8],o[9],o[10],o[11],o[12],o[11]/max(o[12],1),bud))
# land aggregates
agg=collections.defaultdict(lambda:[0,0,0,0,0,[]])
for o in out:
    a=agg[(o[0],o[1])]; a[0]+=o[4]; a[1]+=o[5]; a[2]+=o[6]; a[3]+=o[7]; a[5].append(o[3])
print()
print('kraina: polka popyt odbior wdrodze dni ix_med')
for k,a in sorted(agg.items()):
    print('%s %-11s %8.0f %7.1f %7.1f %7.0f %6.1f %.2f'%(k[0][0],k[1],a[0],a[1],a[2],a[3],a[0]/max(1,max(a[1],a[2])),S.median(a[5])))
