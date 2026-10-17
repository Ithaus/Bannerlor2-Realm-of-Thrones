import re,sys,statistics as st
ks={}; zc={}; state={}; planstart=set(); prof={}
rk=re.compile(r"Kalibracja startu \(KS\): dzien (\d+) \| stan (\S+).*?\| Plan: (\S+ ?[^,]*),.*czas doby KS ([\d.]+) ms")
rz=re.compile(r"Koszyk stanow \(ZC4\): dzien (\d+) \|.*czas hakow od ostatniej linii: popyt ceny ([\d.]+) ms \((\d+) wywolan\), slowniki budzetu ([\d.]+) ms, drabina ([\d.]+) ms, zakupy wsi ([\d.]+) ms, przetwory ZC5 ([\d.]+) ms, razem ([\d.]+) ms \(w dobie KS: mapa koszyka ([\d.]+) ms")
rp=re.compile(r"Profil doby \(E8/0\): dzien (\d+) \| takt doby ([\d.]+) ms")
for line in open(sys.argv[1],encoding='utf-8',errors='replace'):
    m=rk.search(line)
    if m: ks[int(m.group(1))]=(float(m.group(4)),m.group(2),m.group(3)); continue
    m=rz.search(line)
    if m: zc[int(m.group(1))]=tuple(float(x) for x in m.groups()[1:]); continue
    m=rp.search(line)
    if m: prof[int(m.group(1))]=float(m.group(2))
days=sorted(ks)
print('KS days',len(days),days[0],days[-1])
app=[d for d in days if ks[d][1]=='ZASTOSOWANY']
pre=[d for d in days if ks[d][1]!='ZASTOSOWANY']
def s(v): v=sorted(v); return 'n%d med %.2f mean %.2f q90 %.2f max %.2f'%(len(v),v[len(v)//2],sum(v)/len(v),v[int(.9*(len(v)-1))],v[-1])
print('KS ms applied',s([ks[d][0] for d in app]))
print('KS ms pre',s([ks[d][0] for d in pre]) if pre else '-')
print('KS ms all',s([ks[d][0] for d in days]))
# by day mod 7
for r in range(7):
    v=[ks[d][0] for d in app if d%7==r]
    if v: print(' applied day%%7==%d'%r,s(v))
names=['popyt','n_wyw','slowniki','drabina','wsie','zc5','razem','mapa']
zd=[d for d in sorted(zc) if d in ks]
print('ZC4 days',len(zd))
for i,nm in enumerate(names):
    print(' ',nm,s([zc[d][i] for d in zd]))
tot=[ks[d][0]+zc[d][6] for d in zd]
print('KS+hooks',s(tot))
# mapa inside KS? fraction
print('KS minus mapa',s([ks[d][0]-zc[d][7] for d in zd]))
print('prof',s(list(prof.values())))
# show time series of last 20 applied days
for d in app[-16:]:
    z=zc.get(d)
    print(d,d%7,ks[d][0],ks[d][2], z if z else '')
print('--- parity / ramp phases')
for par in (0,1):
    v=[zc[d] for d in zd if d%2==par and d>=109132]
    print('parity',par,'n',len(v),'zc5 med %.2f'%st.median([x[5] for x in v]),'wsie med %.2f'%st.median([x[4] for x in v]),'razem %.2f'%st.median([x[6] for x in v]))
for lo,hi in ((109104,109117),(109118,109131),(109132,109160),(109161,109196)):
    v=[zc[d] for d in zd if lo<=d<=hi]; k=[ks[d][0] for d in zd if lo<=d<=hi and d%7 not in (0,1)]
    if v: print(lo,hi,'wsie %.2f zc5 %.2f slown %.2f popyt %.2f razem %.2f mapa %.2f | KS ordinary med %.2f'%tuple([st.median([x[i] for x in v]) for i in (4,5,2,0,6,7)]+[st.median(k)]))
print('--- steady (>=109132)')
sd=[d for d in zd if d>=109132]
print('KS',s([ks[d][0] for d in sd])); print('KS+hooks',s([ks[d][0]+zc[d][6] for d in sd]))
print('KS ordinary-only (d%7 not 0/1)',s([ks[d][0] for d in sd if d%7 not in (0,1)]))
print('KS-mapa ordinary',s([ks[d][0]-zc[d][7] for d in sd if d%7 not in (0,1)]))
print('applied window all (NKS-9 if window=applied):',s([ks[d][0] for d in app]), 'KS+hooks applied', s([ks[d][0]+zc[d][6] for d in zd]))
od=[d for d in sd if d%7 not in (0,1)]
q=lambda v,p: sorted(v)[int(p*(len(v)-1))]
for nm,f_ in (('KS',lambda d: ks[d][0]),('KS+hooks',lambda d: ks[d][0]+zc[d][6]),('mapa',lambda d: zc[d][7]),('wsie',lambda d: zc[d][4]),('zc5',lambda d: zc[d][5]),('popyt',lambda d: zc[d][0]),('slown',lambda d: zc[d][2]),('drab',lambda d: zc[d][3]),('razem',lambda d: zc[d][6])):
    v=[f_(d) for d in od]; print('ordinary',nm,'q50 %.2f q60 %.2f q70 %.2f q80 %.2f'%(q(v,.5),q(v,.6),q(v,.7),q(v,.8)))
# simulate: what cut on ordinary days needed so overall median <= target
import itertools
def med_after(cutKS,cutH):
    v=[]
    for d in sd:
        k=ks[d][0]-(cutKS if d%7 not in (0,1) else cutKS)  # assume same cut on all days
        v.append(k)
    return st.median(v)
for c in (0.5,0.75,1.0,1.25):
    print('cut KS all days %.2f -> median %.2f'%(c,st.median([ks[d][0]-c for d in sd])))
for c in (1.5,2.0,2.5,3.0):
    print('cut KS+hooks all days %.2f -> median %.2f'%(c,st.median([ks[d][0]+zc[d][6]-c for d in sd])))
