import re,sys
L=r'D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_01-15-30.log'
rz=re.compile(r'Koszyk stanow \(ZC4\): dzien (\d+)')
rv=re.compile(r'wsie: zakupow (\d+) za (\d+) zl, sztuk (\d+) \(chciane (\d+), kupione (\d+); zboze i sol (\d+)/(\d+), reszta jedzenia (\d+)/(\d+), rzeczy (\d+)/(\d+)\), ziarno na piwo domowe (\d+) szt., bez kiesy (\d+), bez towaru (\d+)')
rk=re.compile(r'(Westeros|Essos) wartosc przy cenach polki chciana (\d+) / zaplacona z Planu (\d+) / kupiona (\d+) zl, kcal koszyka kupione ([\d.]+)% / robione w domu ([\d.]+)% / bez pokrycia ([\d.]+)%, wplyw do kies wsi \(utarg doby\) (\d+) zl')
ru=re.compile(r'wsie ([\d.]+)% chcianych sztuk \((\d+) szt.\)')
rp=re.compile(r'kiesy wsi zastosowanych razem (\d+) zl')
re2=re.compile(r'zakupy wsi w miescie obok \(brak na polce targu\) (\d+) szt')
rpl=re.compile(r'KS: pieniadze planu \(krok 8\) doba (\d+).*?Westeros:.*?koszyk (\d+), zaplacony (\d+).*?kcal koszyka: niezaplacone ([\d.]+)%, robione w domu ([\d.]+)%, bez pokrycia ([\d.]+)%.*?Essos:.*?koszyk (\d+), zaplacony (\d+).*?kcal koszyka: niezaplacone ([\d.]+)%, robione w domu ([\d.]+)%, bez pokrycia ([\d.]+)%')
rows={}; plans=[]
for s in open(L,encoding='utf-8',errors='replace'):
    m=rz.search(s)
    if m:
        d=int(m.group(1)); r={}
        v=rv.search(s)
        if v: r['v']=[int(x) for x in v.groups()]
        for k in rk.finditer(s): r[k.group(1)]=[float(x) for x in k.groups()[1:]]
        u=ru.search(s); r['ug']=int(u.group(2)) if u else 0
        p=rp.search(s); r['purse']=int(p.group(1)) if p else 0
        e=re2.search(s); r['else']=int(e.group(1)) if e else 0
        rows[d]=r; continue
    m=rpl.search(s)
    if m: plans.append(m.groups())
for p in plans[-6:]: print('PLAN',p)
w0,w1=109140,109167
def avg(lst): return sum(lst)/len(lst) if lst else float('nan')
for mk in ('Westeros','Essos'):
    ks=[rows[d][mk] for d in rows if w0<=d<=w1 and mk in rows[d] and rows[d]['v'][3]>0]
    print(mk,'window n',len(ks),'kb %.1f kh %.1f unc %.1f'%(avg([k[3] for k in ks]),avg([k[4] for k in ks]),avg([k[5] for k in ks])),
      'zl want %.0f planpaid %.0f bought %.0f inflow %.0f'%(avg([k[0] for k in ks]),avg([k[1] for k in ks]),avg([k[2] for k in ks]),avg([k[6] for k in ks])))
vs=[rows[d]['v'] for d in rows if w0<=d<=w1 and 'v' in rows[d]]
n=len(vs)
S=[sum(v[i] for v in vs)/n for i in range(14)]
print('window per day: zakupow %.0f zl %.0f szt %.0f want %.0f got %.0f | r0 %.0f/%.0f r1 %.0f/%.0f r2 %.0f/%.0f brewgrain %.0f broke %.0f nogoodsEv %.0f'%tuple(S))
print('ungold pcs/d %.0f  elsewhere %.0f'%(avg([rows[d]['ug'] for d in rows if w0<=d<=w1]),avg([rows[d]['else'] for d in rows if w0<=d<=w1])))
for d in sorted(rows):
    if d%7==0 or d in (w0,w1):
        r=rows[d]
        if 'Westeros' in r: print(d,'purse',r['purse'],'W kb/kh/unc',r['Westeros'][3:6],'E',r['Essos'][3:6],'inflow',r['Westeros'][6]+r['Essos'][6],'spent',r['v'][1] if 'v' in r else '-')
print('--- residual other net outflow from village purses (purse_t - purse_{t-1} = inflow - zc4 - other)')
ds=sorted(d for d in rows if 'Westeros' in rows[d] and 'v' in rows[d])
res=[]
for a,b in zip(ds,ds[1:]):
    if b!=a+1: continue
    rb=rows[b]; inflow=rb['Westeros'][6]+rb['Essos'][6]; z=rb['v'][1]
    other=inflow-z-(rb['purse']-rows[a]['purse'])
    res.append((b,inflow,z,rb['purse']-rows[a]['purse'],other))
for w in [(109124,109139),(109140,109167),(109168,109195)]:
    rr=[r for r in res if w[0]<=r[0]<=w[1]]
    if rr: print(w,'n',len(rr),'inflow %.0f zc4 %.0f dpurse %.0f other %.0f'%tuple(sum(r[i] for r in rr)/len(rr) for i in range(1,5)))
