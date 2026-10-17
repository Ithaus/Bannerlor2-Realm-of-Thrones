import csv,sys,math,statistics as st
rows=list(csv.DictReader(open(sys.argv[1],encoding='utf-8',errors='replace'),delimiter=';'))
days=sorted(set(int(r['dzien']) for r in rows))
print('days',days)
def f(x):
    try: return float(x)
    except: return None
for d in days:
    R=[r for r in rows if int(r['dzien'])==d]
    ix=[f(r['zboze_indeks']) for r in R]
    sd=[f(r['zboze_dni_polki']) for r in R]
    lam=[f(r['lambda']) for r in R if f(r['lambda']) is not None]
    sdv=[x for x in sd if x is not None]
    ixv=sorted(ix)
    q=lambda a,p: sorted(a)[int(p*(len(a)-1))]
    # predicted index from shelf days (steady state, no constant)
    pred=[]; err=[]
    for i,s in zip(ix,sd):
        if s is None: continue
        p=10.0 if s<=0 else min(10,max(0.1,(1/(0.14*s))**0.6))
        pred.append(p); err.append(math.log(max(i,0.1)/p))
    nceil=sum(1 for x in ix if x>=9.5); nfl=sum(1 for x in ix if x<=0.11)
    n0=sum(1 for x in sdv if x<0.5)
    nin=sum(1 for x in sdv if 2.5<=x<=5)  # around start 3.56
    print(d,'lam %.2f'%st.median(lam) if lam else '-', 'idx q10/med/q90 %.2f/%.2f/%.2f'%(q(ixv,.1),q(ixv,.5),q(ixv,.9)),
          'shelfdays q10/med/q90 %.1f/%.1f/%.1f'%(q(sdv,.1),q(sdv,.5),q(sdv,.9)),
          'ceil',nceil,'floor',nfl,'empty(<0.5d)',n0,'in2.5-5d',nin,'of',len(sdv),
          'logerr med %.2f'%st.median(err) if err else '')
