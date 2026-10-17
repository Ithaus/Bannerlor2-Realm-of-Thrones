import csv,collections,statistics as st,math
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
rows=list(csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';'))
f=float
V={'grape':3,'olives':5,'meat':9,'butter':24,'cheese':13,'fish':10,'grain':3,'salt':None}
res=collections.defaultdict(lambda: collections.defaultdict(list))
for r in rows:
    if int(r['dzien'])<109144: continue
    d=f(r['popyt_zaplacony_szt_d']); S=f(r['polka_szt']); T=f(r['cel_dni']); ix=f(r['indeks'])
    if d<0.5 or ix<=0: continue
    days=S/d
    pred=min(10,max(0.1,(d/(0.14*S+0.3))**0.6))
    lr=ix/pred
    b='short<0.5T*' if days<0.5*T else ('mid' if days<T else ('ok T*-2T*' if days<2*T else 'glut>2T*'))
    sz='small dp<10' if d<10 else 'big dp>=10'
    res[r['towar']][b].append(lr); res[r['towar']][sz+' '+('glut' if days>=T else 'notglut')].append(lr)
for g in ['grain','fish','salt','olives','meat','cheese','butter','grape']:
    print(g.ljust(7)," | ".join(f"{k}: x{st.median(v):.2f} (n{len(v)})" for k,v in sorted(res[g].items())))
