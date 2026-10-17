import json,sys
D=r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/daniny/d1/'
for run in ('ks','jeff'):
    rows=json.load(open(D+run+'_model.json'))
    print('==',run)
    print('%-30s %7s %7s %7s %7s | %7s %7s %7s | %7s %7s %5s'%('krolestwo','przed','potrz','renty','kred','po_g1','renty1','luka1','po_g2','renty2','luka2'))
    T=[0]*10
    for r in rows:
        extra=r['subV']+r['subT']+r['aides']+0.5*r['t184']   # g=2 grants, aides 1/10, tallage 15%
        a2=r['after']+extra
        v=[r['before'],r['need'],r['rents'],r['kred'],r['after'],max(0,r['after']-r['need']),max(0,r['need']-r['after']),a2,max(0,a2-r['need']),max(0,r['need']-a2)]
        T=[t+x for t,x in zip(T,v)]
        print('%-30s'%r['k'][:30]+' %7.1f %7.1f %7.1f %7.1f | %7.1f %7.1f %7.1f | %7.1f %7.1f %5.1f'%tuple(x/1000 for x in v))
    print('%-30s'%'RAZEM'+' %7.1f %7.1f %7.1f %7.1f | %7.1f %7.1f %7.1f | %7.1f %7.1f %5.1f'%tuple(x/1000 for x in T))
