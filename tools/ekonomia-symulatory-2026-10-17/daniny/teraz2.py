import csv,re,sys,collections
sys.stdout.reconfigure(encoding='utf-8')
D=r'D:/Backup-Bannerlord/logi-jeffa/2026-10-16/2026-10-16_10-14-41/budzet-rodow.csv'
L=r'D:/Backup-Bannerlord/logi-jeffa/2026-10-16/Armoury-2026-10-16_10-14-41.log'
rows=[r for r in csv.DictReader(open(D,encoding='utf-8'),delimiter=';') if r['dzien']=='109025']
def f(x):
    try: return float(x)
    except: return 0.0
K=collections.defaultdict(collections.Counter)
for r in rows:
    c=K[r['krolestwo'] or '(bez)']
    for col in ('sr_wplyw_doby','sr_jednorazowe','zold_partii','zold_zalog','ludzi_partie','ludzi_zalogi','dlug_bank','G'): c[col]+=f(r[col])
txt=open(L,encoding='utf-8',errors='replace').read()
m=re.search(r'Korona: wplywy dnia \(165\): dzien 109025 .*?na krolestwo \(wplywy\+zapas, wydatki\): ([^\n]*)',txt)
crown={}
for seg in re.split(r',\s(?=[A-Z])',m.group(1)):
    mm=re.match(r'\s*(.+?) (\d+)\+(\d+)',seg)
    if not mm: continue
    d=dict(inc=int(mm.group(2)))
    for key,pat in (('p1',r'danina P1 (\d+)'),('zwrot',r'zwrot (\d+)/'),('renty',r'renty (\d+)')):
        x=re.search(pat,seg); d[key]=int(x.group(1)) if x else 0
    crown[mm.group(1)]=d
sk={}
for line in txt.splitlines():
    mm=re.search(r'Skarbce: dzien 109028 - (.+?)(?: \(WOJNA\))?: skarbiec (\d+)',line)
    if mm: sk[mm.group(1)]=int(mm.group(2))
out=[]
for k,c in K.items():
    if k=='(bez)': continue
    cr=crown.get(k,{})
    army=c['zold_partii']+c['zold_zalog']
    reg=c['sr_wplyw_doby']-c['sr_jednorazowe']
    out.append((k,int(c['ludzi_partie']+c['ludzi_zalogi']),int(army),int(reg),cr.get('inc',0),cr.get('p1',0),cr.get('zwrot',0),sk.get(k,0),int(c['dlug_bank'])))
out.sort(key=lambda x:-x[2])
print('krolestwo;wojsko;koszt_wojska_d;dochod_rodow_staly_d;korona_wplyw_d;w_tym_danina_od_oszczednosci;korona_zwrot_zoldu_d;skarbiec;dlug_Bank')
T=[0]*8
for o in out:
    print(';'.join(map(str,o)))
    for i in range(8): T[i]+=o[i+1]
print('RAZEM;'+';'.join(map(str,T)))
