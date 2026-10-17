import csv,collections,statistics as st,sys
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
day=sys.argv[1]
rows=[r for r in csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';') if r['dzien']==day]
f=float
for g in ['meat','cheese','butter','grape','olives']:
  for mk in ['Westeros','Essos']:
    R=[r for r in rows if r['towar']==g and r['rynek']==mk]
    T=f(R[0]['cel_dni'])
    cls=collections.defaultdict(list)
    allx=[]
    for r in R:
        d=f(r['popyt_zaplacony_szt_d']); S=f(r['polka_szt']); ix=f(r['indeks'])
        days=S/d if d>0 else 99
        c='A <0.5T*' if days<0.5*T else ('B 0.5-1T*' if days<T else 'C >=T*')
        cls[c].append(ix); allx.append(ix)
    # median if short towns were at T* level priced at their formula with start index
    print(day,mk[:1],g.ljust(7),"T* %.1f med %.2f |"%(T,st.median(allx))," | ".join(f"{k}: n{len(v)} med {st.median(v):.2f}" for k,v in sorted(cls.items())))
