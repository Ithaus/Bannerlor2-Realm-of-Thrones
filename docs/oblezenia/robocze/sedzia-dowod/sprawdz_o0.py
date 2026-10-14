import re,sys,statistics as st
rows=[]
for l in open(sys.argv[1],encoding='utf-8',errors='replace'):
    m=re.search(r'oblezenie ([\d.]+) dob.*?stosunek gry ([\d.]+) \(oblegajacy (\d+) / obroncy (\d+) x przewaga ([\d.]+)\^(-?[\d.]+);.*?mury ([\d.]+) \(bez czlonu czasu: obroncy (\d+) x przewaga\^([\d.]+)\).*?stolica (\w+)',l)
    if not m: print('X',l[:200]); continue
    d,r,a,o,adv,w,mury,o0,w0,cap=m.groups()
    kind = 'zamek' if ', zamek,' in l else 'miasto'
    rows.append(dict(d=float(d),r=float(r),a=int(a),o=int(o),adv=float(adv),w=float(w),mury=float(mury),w0=float(w0),cap=cap,kind=kind))
print(len(rows),'w<0',sum(x['w']<0 for x in rows),'mury<=1',sum(x['mury']<=1 for x in rows),'mury<1.5',sum(x['mury']<1.5 for x in rows))
print('doby med',st.median([x['d'] for x in rows]),'q',st.quantiles([x['d'] for x in rows],n=10))
print('mury med',st.median([x['mury'] for x in rows]),'q',[round(q,2) for q in st.quantiles([x['mury'] for x in rows],n=10)])
print('A/O sila med',st.median([x['a']/max(1,x['o']) for x in rows]),'min',min(x['a']/max(1,x['o']) for x in rows if x['o']>0))
print('adv med',st.median([x['adv'] for x in rows]),'w0 rozklad',{k:sum(1 for x in rows if x['w0']==k) for k in set(x['w0'] for x in rows)})
print('stolica',sum(x['cap']=='tak' for x in rows))
# OBA OB1: kiedy decyzja
import math
for lab,H0,rate,floor in [('gra',96,0.02,None),('OBA x4.33',416,0.00462,0.0)]:
    pass
# under OBLEZENIA: f=2 for H days then linear to 1 over 2H; s=A/(O adv^(w0 f)) > 1  -> need f <= ln(A/O)/(w0 ln adv)
def oblez_day(x,H):
    if x['o']==0: return 0
    if x['adv']<=1: return x['d']
    fneed=math.log(x['a']/x['o'])/(x['w0']*math.log(x['adv']))
    if fneed>=2: return x['d']
    if fneed<1: return None
    # f(t)=2 for t<H; 2-(t-H)/(2H) for H<=t<3H
    t=H+(2-fneed)*2*H
    return max(t,x['d'])
for H,kind in [(14,'zamek'),(21,'miasto')]:
    ds=[oblez_day(x,H if not x['cap']=='tak' else 35) for x in rows if x['kind']==kind]
    ok=[d for d in ds if d is not None]
    print(kind,'OBLEZENIA O1: never',sum(d is None for d in ds),'n',len(ds),'med',st.median(ok) if ok else '-', 'imm', sum(1 for x in rows if x['kind']==kind and x['o']>0 and x['adv']>1 and math.log(x['a']/x['o'])/(x['w0']*math.log(x['adv']))>=2))
