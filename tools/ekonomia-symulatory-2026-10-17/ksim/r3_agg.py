import csv,sys,math
rows=list(csv.DictReader(open(sys.argv[1],encoding='utf-8',errors='replace'),delimiter=';'))
def f(x):
    try: return float(x)
    except: return 0.0
def wmed(pairs):
    pairs=sorted(pairs); tot=sum(w for _,w in pairs); c=0
    for v,w in pairs:
        c+=w
        if c>=tot/2: return v
for d in (109137,109165,109193):
    for m in ('Westeros','Essos'):
        R=[r for r in rows if int(r['dzien'])==d and r['rynek']==m]
        D=sum(f(r['zboze_popyt_zaplacony_szt_d']) for r in R); S=sum(f(r['zboze_polka_szt']) for r in R)
        agg=(0.14*S/D)**-0.6
        med=sorted(f(r['zboze_indeks']) for r in R)[len(R)//2]
        dwm=wmed([(f(r['zboze_indeks']),f(r['zboze_popyt_zaplacony_szt_d'])) for r in R])
        dmean=sum(f(r['zboze_indeks'])*f(r['zboze_popyt_zaplacony_szt_d']) for r in R)/D
        # what index if shelves redistributed proportional to D: agg. What share of D is in towns at index>=3
        hi=sum(f(r['zboze_popyt_zaplacony_szt_d']) for r in R if f(r['zboze_indeks'])>=3)/D
        lo=sum(f(r['zboze_popyt_zaplacony_szt_d']) for r in R if f(r['zboze_indeks'])<=0.7)/D
        fm=sorted(f(r['indeks_jedzenia_mediana']) for r in R)
        print(d,m,'towns',len(R),'shelf/D %.1f d -> even-spread idx %.2f | median town %.2f, D-weighted median %.2f, D-weighted mean %.2f | D share idx>=3: %.0f%%, idx<=0.7: %.0f%% | food-median idx q25/med/q75 %.2f/%.2f/%.2f'%(S/D,agg,med,dwm,dmean,100*hi,100*lo,fm[len(fm)//4],fm[len(fm)//2],fm[3*len(fm)//4]))
