import csv,re,sys,collections
sys.stdout.reconfigure(encoding='utf-8')
D=r'D:/Backup-Bannerlord/logi-jeffa/2026-10-16/2026-10-16_10-14-41/budzet-rodow.csv'
L=r'D:/Backup-Bannerlord/logi-jeffa/2026-10-16/Armoury-2026-10-16_10-14-41.log'
rows=[r for r in csv.DictReader(open(D,encoding='utf-8'),delimiter=';') if r['dzien']=='109025']
def f(x):
    try: return float(x)
    except: return 0.0
K=collections.defaultdict(lambda: collections.Counter())
for r in rows:
    k=r['krolestwo'] or '(bez)'
    c=K[k]; c['rody']+=1
    for col in ('wplyw_doby','renty','zwrot_korony','mennica_monopole','zold_partii','zold_zalog','zold_karawan','ludzi_partie','ludzi_zalogi','kiesa_glowy','G','dlug_bank','dlug_korona','d_staly','d_ziemia','d_korona','d_kontrakt','d_majatek'):
        c[col]+=f(r[col])
txt=open(L,encoding='utf-8',errors='replace').read()
m=re.search(r'Korona: wplywy dnia \(165\): dzien 109025 .*?na krolestwo \(wplywy\+zapas, wydatki\): (.*)',txt)
crown={}
for seg in re.split(r',\s(?=[A-Z])',m.group(1)):
    mm=re.match(r'\s*(.+?) (\d+)\+(\d+)',seg)
    if not mm: continue
    name=mm.group(1); d=dict(inc=int(mm.group(2)),res=int(mm.group(3)))
    for key,pat in (('p1',r'danina P1 (\d+)'),('w184',r'\(184 (\d+)\)'),('zwrot',r'zwrot (\d+)/'),('renty',r'renty (\d+)'),('w5',r'doplata W5 (\d+)'),('kred',r'kredyt186 \+(\d+)')):
        x=re.search(pat,seg); d[key]=int(x.group(1)) if x else 0
    crown[name]=d
sk={}
for mm in re.finditer(r'Skarbce: dzien 109028 - (.+?)(?: \(WOJNA\))?: skarbiec (\d+).*?(?:dlug w Banku \(186\) (\d+))?.*?krol (.+?) kiesa (\d+).*?kiesy razem (\d+).*?wojsko rodow (\d+)',txt):
    sk[mm.group(1)]=dict(skarb=int(mm.group(2)),bank=int(mm.group(3) or 0),kiesy=int(mm.group(6)),wojsko=int(mm.group(7)))
print('krolestwo;rody;wojsko_ludzi;zold_partii_d;zold_zalog_d;koszt_wojska_d;wplyw_rodow_d;korona_wplyw_d;w_tym_danina_P1;zwrot_zoldu;renty_korony;skarbiec;dlug_bank;kiesy_rodow')
tot=collections.Counter()
for k,c in sorted(K.items(), key=lambda kv:-(kv[1]['zold_partii']+kv[1]['zold_zalog'])):
    cr=crown.get(k,{}); s=sk.get(k,{})
    army=c['zold_partii']+c['zold_zalog']
    line=[k,int(c['rody']),int(c['ludzi_partie']+c['ludzi_zalogi']),int(c['zold_partii']),int(c['zold_zalog']),int(army),int(c['wplyw_doby']),cr.get('inc',0),cr.get('p1',0),cr.get('zwrot',0),cr.get('renty',0),s.get('skarb',0),s.get('bank',0),s.get('kiesy',0)]
    print(';'.join(map(str,line)))
    for i,n in enumerate(('rody','ludzi','zp','zz','army','wpl','cinc','p1','zw','rent','skarb','bank','kiesy')): tot[n]+=line[i+1]
print('RAZEM;'+';'.join(str(tot[n]) for n in ('rody','ludzi','zp','zz','army','wpl','cinc','p1','zw','rent','skarb','bank','kiesy')))
