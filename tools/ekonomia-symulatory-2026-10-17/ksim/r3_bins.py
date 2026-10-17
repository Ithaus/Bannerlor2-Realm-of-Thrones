import csv,sys,math,statistics as st
rows=list(csv.DictReader(open(sys.argv[1],encoding='utf-8',errors='replace'),delimiter=';'))
def f(x):
    try: return float(x)
    except: return None
R=[r for r in rows if int(r['dzien'])>=int(sys.argv[2])]
bins=[(-1,0.05),(0.05,0.5),(0.5,1.5),(1.5,3),(3,5),(5,8),(8,15),(15,30),(30,60),(60,120),(120,400)]
for lo,hi in bins:
    a=[(f(r['zboze_indeks']),f(r['zboze_dni_polki'])) for r in R if f(r['zboze_dni_polki']) is not None and lo<f(r['zboze_dni_polki'])<=hi]
    if not a: continue
    ix=sorted(x for x,_ in a); s=st.median([y for _,y in a])
    pred=min(10,max(0.1,(1/(0.14*s))**0.6)) if s>0 else 10
    q=lambda p: ix[int(p*(len(ix)-1))]
    print('days (%g,%g] n=%d medDays %.2f idx q10/med/q90 %.2f/%.2f/%.2f  pred(steady) %.2f'%(lo,hi,len(a),s,q(.1),q(.5),q(.9),pred))
# per-town last day list sorted
last=max(int(r['dzien']) for r in rows)
L=[r for r in rows if int(r['dzien'])==last]
L.sort(key=lambda r: f(r['zboze_dni_polki']) or 0)
print('--- last day',last)
for r in L[:12]+L[-12:]:
    print(r['miasto'],r['rynek'],'idx',r['zboze_indeks'],'polka',r['zboze_polka_szt'],'popyt',r['zboze_popyt_zaplacony_szt_d'],'dni',r['zboze_dni_polki'],'ludzie',r['ludzie'],'food_med',r['indeks_jedzenia_mediana'],'ceil',r['jedzenie_na_suficie'],'floor',r['jedzenie_na_podlodze'])
