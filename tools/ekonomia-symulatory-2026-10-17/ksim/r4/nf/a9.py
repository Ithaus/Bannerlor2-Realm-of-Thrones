import csv,collections,statistics as st,math
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
rows=list(csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';'))
f=float
start={('W','cheese'):0.71,('E','cheese'):1.23,('W','butter'):1.0,('E','butter'):1.44,('W','olives'):0.70,('E','olives'):0.70}
nks7={('W','meat'):3.09,('E','meat'):3.70,('W','cheese'):2.42,('E','cheese'):2.09,('W','butter'):1.81,('E','butter'):2.06,('W','grape'):2.74,('E','grape'):1.93,('W','olives'):2.16,('E','olives'):1.49}
win=['109144','109151','109158','109165','109172']
for g in ['meat','cheese','butter','grape','olives']:
  for mk in ['Westeros','Essos']:
    m=mk[0]; s0=start.get((m,g),1.0)
    inst=[];agg=[];cmed=[];lvl=[]
    for d in win:
        R=[r for r in rows if r['dzien']==d and r['towar']==g and r['rynek']==mk]
        ix=[f(r['indeks']) for r in R]; inst.append(st.median(ix))
        S=sum(f(r['polka_szt']) for r in R); Dp=sum(f(r['popyt_zaplacony_szt_d']) for r in R); T=f(R[0]['cel_dni'])
        agg.append((Dp/(0.14*max(1,S)))**0.6); lvl.append(S/(T*Dp))
        c=[f(r['indeks']) for r in R if f(r['popyt_zaplacony_szt_d'])>0 and f(r['polka_szt'])>=T*f(r['popyt_zaplacony_szt_d'])]
        if c: cmed.append(st.median(c))
    I7=nks7[(m,g)]; Ii=st.median(inst); Ia=st.median(agg)
    tot=math.log(I7/s0)
    lag=math.log(I7/Ii); level=math.log(Ia/s0); dist=math.log(Ii/Ia)
    print(f"{m} {g:7s} start {s0:.2f} NKS7 {I7:.2f} (x{I7/s0:.2f}) | inst med {Ii:.2f} | agg-formula {Ia:.2f} (stock/target {st.median(lvl):.2f}) | adequate-shelf towns med {st.median(cmed) if cmed else float('nan'):.2f} | log-shares: EMA lag {lag/tot:.0%}, stock level {level/tot:.0%}, distribution+formula {dist/tot:.0%}")
