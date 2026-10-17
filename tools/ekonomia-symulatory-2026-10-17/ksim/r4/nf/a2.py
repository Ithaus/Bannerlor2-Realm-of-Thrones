import csv,collections,statistics as st,math
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
rows=list(csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';'))
def f(x):
    try: return float(x)
    except: return 0.0
# formula check: idx vs (d/(0.14 S + c))^0.6
by=collections.defaultdict(list)
for r in rows:
    S=f(r['polka_szt']);d=f(r['popyt_zaplacony_szt_d']);ix=f(r['indeks'])
    if d<=0.5: continue
    pred=min(10,max(0.1,(d/(0.14*S+0.5))**0.6))
    by[r['towar']].append((ix,pred,S/d))
for g,l in by.items():
    lr=[math.log(a/b) for a,b,_ in l if a>0 and b>0]
    print(g, "n",len(l),"median log(actual/pred) %.2f  (ratio %.2f), mean abs %.2f"%(st.median(lr),math.exp(st.median(lr)),st.mean([abs(x) for x in lr])))
# bin by shelf days
print()
for g in ['meat','cheese','grape','olives']:
    l=sorted(by[g],key=lambda x:x[2])
    bins=[(0,0.25),(0.25,0.5),(0.5,1),(1,2),(2,4),(4,8),(8,16),(16,1e9)]
    s=g+": "
    for lo,hi in bins:
        sub=[a for a,b,dd in l if lo<=dd<hi]
        if sub: s+="[%g-%g d] n%d idx %.2f; "%(lo,hi,len(sub),st.median(sub))
    print(s)
