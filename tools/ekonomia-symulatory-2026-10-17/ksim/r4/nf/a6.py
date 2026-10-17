import csv,collections,statistics as st,math
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
rows=list(csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';'))
by={}
for r in rows: by[(r['dzien'],r['miasto_id'],r['towar'])]=r
days=sorted(set(r['dzien'] for r in rows))
f=float
for g in ['grape','olives','cheese','meat','butter']:
    a=[];b=[]
    for r in rows:
        if r['towar']!=g: continue
        d=f(r['popyt_zaplacony_szt_d']); S=f(r['polka_szt'])
        if d<1: continue
        i=days.index(r['dzien'])
        if i==0: continue
        p=by.get((days[i-1],r['miasto_id'],g))
        if not p: continue
        Sp=f(p['polka_szt'])
        # model: supply EMA ~ 0.85^7 weight of old snapshot
        w=0.85**7
        Seff=(1-w)*S+w*Sp  # crude
        pred=(d/(0.14*Seff+0.5))**0.6
        pred0=(d/(0.14*S+0.5))**0.6
        ix=f(r['indeks'])
        if ix>0: a.append(math.log(ix/min(10,max(0.1,pred0)))); b.append(math.log(ix/min(10,max(0.1,pred))))
    print(g,"median log err no-lag %.2f, with lag %.2f; mean abs %.2f -> %.2f"%(st.median(a),st.median(b),st.mean(map(abs,a)),st.mean(map(abs,b))))
# grape towns with >16 d and idx>1.2
print()
for r in rows:
    if r['towar']!='grape' or r['dzien']!='109193': continue
    d=f(r['popyt_zaplacony_szt_d']); S=f(r['polka_szt'])
    if d>1 and S/d>12 :
        i=days.index(r['dzien']); p=by.get((days[i-1],r['miasto_id'],'grape'))
        print(r['miasto_id'],r['rynek'],"idx",r['indeks'],"shelf",S,"dp",d,"days %.1f"%(S/d),"prev shelf",p['polka_szt'] if p else '-', "prev idx", p['indeks'] if p else '-', "trans",r['w_drodze_szt'],"offt",r['odbior_7d_szt_d'])
