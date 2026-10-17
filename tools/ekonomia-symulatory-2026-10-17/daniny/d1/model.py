# Design 1 (daniny zwyczajowe): crown income per kingdom before/after, static on today's bases (last 28 days)
import json,sys,re,collections,csv
D=r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/daniny/'
run=sys.argv[1]  # ks / jeff
def table(fn,tag,cols):
    out={}; on=False
    for line in open(D+fn,encoding='utf-8'):
        if line.startswith(tag): on=True; continue
        if on and (line.startswith('===') or line.startswith('jeff days') or line.startswith('ks days')) : break
        if not on or line.startswith('krolestwo') or line.startswith('RAZEM') or line.startswith('kingdom-days'): continue
        m=re.match(r'^(.+?)\s{2,}(-?\d.*)$',line.rstrip())
        if not m: continue
        nums=re.findall(r'-?\d+',m.group(2).replace('|',' '))
        out[m.group(1).strip()]=dict(zip(cols,map(float,nums)))
    return out
kc=table('kcrown.txt','===  %s crown'%run,['wpl','zapas','P1','t184','zwrot','renty','W5','W5b','kred','dKred','dRent0','dShort','skarb0','skarbEnd','dlug186','wojsko','minus'])
kw=table('kingdom_wages.txt','%s days'%run,['n','wojna','zold_p','zold_z','D','d_ziemia','d_korona','zwrot','renta180','w5','w5b','m1','w7','G','G_lt_R','dlug_bank','ludzi_p','limit_p'])
B=json.load(open(D+'d1/%s_base.json'%run))
T,F,V=B['T'],B['F'],B['V']
C={'ks':dict(cast_crown=28.9e3,cast_lord=58.7e3,hoard_tc_lord=47.0e3,hoard_tc_crown=65.3e3,hoard_v_lord=11.8e3,hoard_v_crown=16.3e3,
             levy_v=47.7e3,levy_t=43.0e3,customs=10.5e3,customs_war=20e3,vassal=4.6e3,rich=41.3e3,toll=7.6e3,sales=1.09e6,base184=618e3,other=78.3e3,
             vpurse=2.61e6,vtak=268.5e3),
   'jeff':dict(cast_crown=44.3e3,cast_lord=90.0e3,hoard_tc_lord=60.2e3,hoard_tc_crown=48.4e3,hoard_v_lord=31.9e3,hoard_v_crown=25.7e3,
             levy_v=88.2e3,levy_t=35.6e3,customs=10.2e3,customs_war=19e3,vassal=7.4e3,rich=33.7e3,toll=7.9e3,sales=1.09e6*0.85,base184=618e3*0.82,other=47.9e3,
             vpurse=4.73e6,vtak=271.3e3)}[run]
# village purses per kingdom
vp=collections.defaultdict(float)
if run=='ks':
    P='D:/Backup-Bannerlord/logi-testow/2026-10-17_06-07-51'
    tk={}
    for r in csv.DictReader(open(P+'/kasy-miast.csv',encoding='utf-8',errors='replace'),delimiter=';'): tk[r['miasto_id']]=r['krolestwo']
    for r in csv.DictReader(open(P+'/ks-miasta.csv',encoding='utf-8',errors='replace'),delimiter=';'):
        if r['dzien']=='109179': vp[tk.get(r['miasto_id'],'?')]+=float(r['kiesy_wsi_zl'] or 0)
    s=sum(vp.values()); vp={k:v*C['vpurse']/s for k,v in vp.items()}
    vt={k:V[k]['utarg'] for k in V}
else:
    P='D:/Backup-Bannerlord/logi-jeffa/2026-10-16/2026-10-16_10-14-41'
    pw=list(csv.DictReader(open(P+'/plon-wsi.csv',encoding='utf-8',errors='replace'),delimiter=';'))
    d=max(int(r['dzien']) for r in pw); pp=collections.defaultdict(float)
    for r in pw:
        if int(r['dzien'])==d: pp[r['krolestwo']]+=float(r['ludzie'] or 0)
    s=sum(pp.values()); vp={k:v/s*C['vpurse'] for k,v in pp.items()}; vt={k:v/s*C['vtak'] for k,v in pp.items()}
st=sum(vt.values()); vt={k:v*C['vtak']/st for k,v in vt.items()}
def g(dct,k,c): return dct.get(k,{}).get(c,0.0)
ks=sorted(kc, key=lambda k:-(kc[k]['zwrot']))
sumT=lambda c: sum(T[k][c] for k in T)
sumF=lambda c: sum(F[k].get(c,0) for k in F)
castsur={k:max(0,g(F,k,'zamek_kasa')-g(F,k,'zamek_zapas_D4')) for k in F}; scs=sum(castsur.values()) or 1
folksav={k:g(F,k,'miasto_kiesa_W')+g(F,k,'miasto_kiesa_R')+g(F,k,'miasto_kiesa_P')+g(F,k,'zamek_kiesa_W') for k in F}; sfs=sum(folksav.values()) or 1
doch={k:g(F,k,'miasto_dochod')+g(F,k,'zamek_dochod') for k in F}; sd=sum(doch.values()) or 1
wyd={k:g(F,k,'miasto_wydatki')+g(F,k,'zamek_wydatki') for k in F}; sw=sum(wyd.values()) or 1
dop=sumT('doplyw_netto') or 1
G={k:kw.get(k,{}).get('G',0) for k in kc}; sg=sum(G.values()) or 1
rows=[]; tot=collections.defaultdict(float)
for k in ks:
    c=kc[k]
    need=c['zwrot']+c['W5']+c['W5b']+C['other']*c['zwrot']/max(1,sum(kc[x]['zwrot'] for x in kc))
    before=c['wpl']+c['zapas']
    farm=g(T,k,'do_korony')+C['cast_crown']*castsur.get(k,0)/scs
    subV=(vp.get(k,0)+91*vt.get(k,0))/15/91
    above=0.72*folksav.get(k,0)+max(0,g(T,k,'kasa')-g(T,k,'zapas_kupcow'))
    subT=above/10/91
    sales=C['sales']*(0.5*wyd.get(k,0)/sw+0.5*g(T,k,'doplyw_netto')/dop)
    aides=0.05*sales
    t184=0.10*C['base184']*doch.get(k,0)/sd
    cust=C['customs_war']*g(T,k,'doplyw_netto')/dop
    aid=0.10*kw.get(k,{}).get('d_ziemia',0)
    toll=C['toll']*vt.get(k,0)/C['vtak']
    rich=C['rich']*G[k]/sg
    rel=c['zapas']
    after=farm+subV+subT+aides+t184+cust+aid+toll+rich+rel
    r=dict(k=k,before=before,need=need,rents=c['renty'],kred=c['kred'],debt=c['dlug186'],after=after,farm=farm,subV=subV,subT=subT,aides=aides,t184=t184,cust=cust,aid=aid,toll=toll,rich=rich,rel=rel,P1=c['P1'],
           rents_after=max(0,after-need),gap=max(0,need-after),wages=kw.get(k,{}).get('zold_p',0)+kw.get(k,{}).get('zold_z',0),D=kw.get(k,{}).get('D',0))
    rows.append(r)
    for x in r:
        if x!='k': tot[x]+=r[x]
fmt='%-32s'+' %8s'*16
print(fmt%('krolestwo','przed','potrzeb','renty','kred186','po','farma','subW','subM','aides','t184','clo','aid','rich+opl','renty_po','luka','zold'))
for r in rows+[dict(tot,k='RAZEM')]:
    print(fmt%(r['k'][:32],*[('%.1f'%(r[x]/1000)) for x in ('before','need','rents','kred','after','farm','subV','subT','aides','t184','cust','aid')],'%.1f'%((r['rich']+r['toll']+r['rel'])/1000),*[('%.1f'%(r[x]/1000)) for x in ('rents_after','gap','wages')]))
json.dump(rows,open(D+'d1/%s_model.json'%run,'w'))
