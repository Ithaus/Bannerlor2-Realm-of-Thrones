import csv,collections,statistics as st
D="D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/"
rows=list(csv.DictReader(open(D+"ks-miasta-towary.csv",encoding="utf-8"),delimiter=';'))
def f(x):
    try: return float(x)
    except: return 0.0
agg=collections.defaultdict(lambda: collections.defaultdict(float))
idx=collections.defaultdict(list)
for r in rows:
    k=(r['dzien'],r['rynek'],r['towar'])
    a=agg[k]
    S=f(r['polka_szt']);d=f(r['popyt_zaplacony_szt_d']);o=f(r['odbior_7d_szt_d']);w=f(r['w_drodze_szt']);T=f(r['cel_dni'])
    a['S']+=S;a['d']+=d;a['o']+=o;a['w']+=w;a['n']+=1
    if d>0:
        days=S/d
        if days<0.5*T: a['d_lt05T']+=d
        if days<0.5: a['d_lt05d']+=d
        a['dT']+=d*T
    a['ix_d']+=f(r['indeks'])*d
    idx[k].append(f(r['indeks']))
    if S>3*d*T and d>0: a['S_over3T']+=S
    if d==0: a['S_nodem']+=S
for k in sorted(agg):
    if k[2] not in ('meat','cheese','butter','grape','olives','fish','grain','salt'): continue
    if k[0] not in ('109144','109165','109193'): continue
    a=agg[k]
    print(k[0],k[1],k[2].ljust(7),"shelf %6.0f dem %6.0f offt7 %6.0f trans %6.0f | shelf days %.2f T* %.1f | d share <0.5T* %.0f%% <0.5d %.0f%% | idx med %.2f dem-wtd %.2f | shelf in towns >3T* %.0f%%, shelf no-dem %.0f"%(a['S'],a['d'],a['o'],a['w'],a['S']/max(1,a['d']),a['dT']/max(1,a['d']),100*a['d_lt05T']/max(1,a['d']),100*a['d_lt05d']/max(1,a['d']),st.median(idx[k]),a['ix_d']/max(1,a['d']),100*a['S_over3T']/max(1,a['S']),a['S_nodem']))
