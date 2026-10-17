import json, math, csv, sys
K=r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/ksim/sim/'
W=json.load(open(K+'world_r2.json'))
T=W['towns']; NT=len(T)
D='D:/Backup-Bannerlord/logi-testow/2026-10-17_09-07-09/'
land={}
for r in csv.DictReader(open(D+'ks-miasta.csv',encoding='utf-8'),delimiter=';'): land[r['miasto_id']]=r['kraina']
dl=W['dland']; ds=W['dsea']; sp=41.0; dk=1.18
days=[[-1.0]*NT for _ in range(NT)]; sea=[[False]*NT for _ in range(NT)]
for a in range(NT):
    for b in range(NT):
        if a==b or T[a]['mk']!=T[b]['mk']: continue
        l=dl[a][b]; s=ds[a][b]; lo=l>=0; ao=s>=0
        se=(not lo) or (ao and l>1.15*s); road=(s if ao else -1) if se else l
        if road<0: continue
        days[a][b]=max(1.0,math.ceil(road*dk/(sp*4.0)))+1.0 if se else max(1.0,math.ceil(road*dk/sp))
        sea[a][b]=se
idx={t['id']:i for i,t in enumerate(T)}
ordr=sorted(range(NT),key=lambda i:T[i]['id']); rank={t:i for i,t in enumerate(ordr)}
def cand(t):
    l=[(days[s][t],rank[s],s) for s in range(NT) if s!=t and T[s]['mk']==T[t]['mk'] and days[s][t]>=0]
    l.sort(); return l
want=sys.argv[1:] or ['qartheen','ghiscari','aserai','crownlands','empire','tyroshi','myrish']
for i,t in enumerate(T):
    L=land.get(t['id'],'?')
    if L not in want: continue
    l=cand(i)
    print('%s (%s) cand12: '%(t['name'],L)+', '.join('%s/%s %.0f%s'%(T[s]['name'][:10],land.get(T[s]['id'],'?')[:5],d,'m' if sea[s][i] else '') for d,_,s in l[:12]))
    far=[(d,s) for d,_,s in l[12:]]
    print('     far: '+', '.join('%s/%s %.0f%s'%(T[s]['name'][:10],land.get(T[s]['id'],'?')[:5],d,'m' if sea[s][i] else '') for d,s in far[:10]))
json.dump({'days':days,'sea':sea,'ids':[t['id'] for t in T]},open(K+'../r4/tr/days_sim.json','w'))
