import re, collections, sys
L='D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_09-07-09.log'
pday=sys.argv[1] if len(sys.argv)>1 else '109165'; dday=sys.argv[2] if len(sys.argv)>2 else '109171'
plan=None; dl=None
for line in open(L,encoding='utf-8',errors='replace'):
    if 'KS: korytarze planu doba '+pday+' ' in line: plan=line
    if 'Kupcy zywnosci (KS-H) dzien '+dday+' ' in line: dl=line
def parse(seg):
    out={}
    for m in re.finditer(r'([a-z_]+)>([a-z_]+) ([A-Za-z_]+) (\d+)(?: \((\d+) d( morzem)?\))?',seg):
        out[(m.group(1),m.group(2),m.group(3))]=(float(m.group(4)),int(m.group(5)) if m.group(5) else None,bool(m.group(6)))
    return out
capped=parse(plan.split('korytarze >= 20 szt./d:')[1].split('| korytarze bez sufitu')[0])
free=parse(plan.split('korytarze bez sufitu dowozu >= 20 szt./d:')[1].split('| miasta (psujace')[0])
dlv=parse(dl.split('korytarze (dowiezione, srednia 28 dob, szt./d):')[1])
mk={}
E=set('volantine qartheen ghiscari pentoshi khuzait ibbenese myrish tyroshi lyseni empire norvos qohorik nord sarnor valyrian yiti'.split())
def M(l): return 'E' if l in E else 'W'
print('free corridors (>=20):',len(free),' sum',round(sum(v[0] for v in free.values())))
print('capped corridors (>=20):',len(capped),' sum',round(sum(v[0] for v in capped.values())))
inter={k:v for k,v in dlv.items() if k[0]!=k[1]}; intra={k:v for k,v in dlv.items() if k[0]==k[1]}
print('delivered list entries',len(dlv),' inter-land sum',round(sum(v[0] for v in inter.values())),' intra-land sum',round(sum(v[0] for v in intra.values())))
# by good
bg=collections.defaultdict(lambda:[0,0,0,0])
for k,v in free.items(): bg[(M(k[0]),k[2])][0]+=v[0]
for k,v in inter.items(): bg[(M(k[0]),k[2])][1]+=v[0]
for k,v in intra.items(): bg[(M(k[0]),k[2])][2]+=v[0]
for k,v in capped.items(): bg[(M(k[0]),k[2])][3]+=v[0]
print('rynek towar | plan bez sufitu | dowiezione miedzy krainami | wewnatrz krain | plan z sufitem')
for k in sorted(bg): print('%s %-10s %7.0f %7.0f %7.0f %7.0f'%(k[0],k[1],*bg[k]))
# per free corridor ratio
print()
rows=[]
for k,v in free.items():
    d=dlv.get(k,(0,None,False))[0]
    rows.append((v[0],d,k,v[1],v[2]))
rows.sort(reverse=True)
below=sum(1 for r in rows if r[1]<0.8*r[0])
print('free corridors below 0.8:',below,'of',len(rows))
for r in rows[:45]: print('%-12s>%-12s %-10s plan %5.0f dowoz %5.0f (%.2f) %s d %s'%(r[2][0],r[2][1],r[2][2],r[0],r[1],r[1]/r[0],r[3],'morze' if r[4] else 'lad'))
# delivered flows not in plan
print()
nop=[(v[0],k) for k,v in inter.items() if k not in free]; nop.sort(reverse=True)
print('inter-land delivered not in plan free list: sum',round(sum(x[0] for x in nop)),nop[:15])
print()
imp=collections.defaultdict(lambda:[0,0]); exp=collections.defaultdict(lambda:[0,0])
for k,v in free.items(): imp[(k[1],k[2])][0]+=v[0]; exp[(k[0],k[2])][0]+=v[0]
for k,v in inter.items(): imp[(k[1],k[2])][1]+=v[0]; exp[(k[0],k[2])][1]+=v[0]
for nm,dd in (('IMPORT kraina',imp),('EXPORT kraina',exp)):
    rows=sorted(dd.items(),key=lambda x:-x[1][0])
    n=sum(1 for k,v in rows if v[0]>=20); b=sum(1 for k,v in rows if v[0]>=20 and v[1]<0.8*v[0])
    print(nm,': >=20 plan',n,' below 0.8',b)
    for k,v in rows[:25]: print('   %-12s %-10s plan %5.0f dowoz %5.0f (%.2f)'%(k[0],k[1],v[0],v[1],v[1]/max(v[0],1)))
