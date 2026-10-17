import csv,collections,statistics as st,sys
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
day=sys.argv[1]
land={};name={}
for r in csv.DictReader(open(D+"ks-miasta.csv",encoding="utf-8"),delimiter=';'):
    land[r['miasto_id']]=r['kraina']; name[r['miasto_id']]=r['miasto']
rows=[r for r in csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';') if r['dzien']==day]
f=float
for g in ['meat','cheese','butter','grape','olives']:
  for mk in ['Westeros','Essos']:
    R=[r for r in rows if r['towar']==g and r['rynek']==mk]
    T=f(R[0]['cel_dni'])
    Sd=sum(f(r['polka_szt']) for r in R); Dd=sum(f(r['popyt_zaplacony_szt_d']) for r in R); O=sum(f(r['odbior_7d_szt_d']) for r in R); W=sum(f(r['w_drodze_szt']) for r in R)
    src=0;offer=0;defic=0;short_d=0;glutS=0;okS=0
    srcl=collections.Counter(); shl=collections.Counter(); dl=collections.Counter(); sl=collections.Counter()
    adequate_dp=0; adequate_o=0
    ix=[f(r['indeks']) for r in R]
    for r in R:
        S=f(r['polka_szt']);d=f(r['popyt_zaplacony_szt_d']);o=f(r['odbior_7d_szt_d']);w=f(r['w_drodze_szt'])
        L=land.get(r['miasto_id'],'?')
        dl[L]+=d; sl[L]+=S
        if S>1.25*T*d+5:
            src+=1; offer+=0.5*(S-1.25*T*d); srcl[L]+=S-1.25*T*d
        if S>=T*d: adequate_dp+=d; adequate_o+=o
        if S+w<0.5*T*d: short_d+=d
        if S<T*d: defic+=T*d-S; shl[L]+=T*d-S
    agg=(Dd/(0.14*max(1,Sd)))**0.6
    print(f"{day} {mk[:1]} {g:7s} T*={T:4.1f} shelf {Sd:6.0f}+trans {W:5.0f} | dp {Dd:5.0f} offt {O:5.0f} ({O/max(1,Dd):.0%}) | stock/target {Sd/(T*Dd):.2f} (+trans {(Sd+W)/(T*Dd):.2f}) | idx med {st.median(ix):.2f} agg {agg:.2f} | towns>1.25T* {src}/{len(R)} offer {offer:5.0f} vs deficit {defic:6.0f} | dp in towns <0.5T* incl trans {short_d/max(1,Dd):.0%} | offt/dp where shelf>=T*: {adequate_o/max(1,adequate_dp):.2f} (dp {adequate_dp:.0f})")
    top=lambda c: ", ".join(f"{k} {v:.0f}" for k,v in c.most_common(4))
    print("    surplus>1.25T* by land:",top(srcl),"| deficit<T* by land:",top(shl))
