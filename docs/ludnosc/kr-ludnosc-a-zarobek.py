import csv, collections, sys
sys.stdout.reconfigure(encoding='utf-8')
D=r"D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Logs\2026-10-13_06-24-10"
def fl(v):
    try: return float(v.replace(',','.'))
    except: return 0.0
def rd(n):
    with open(D+"\\"+n,encoding='utf-8-sig',newline='') as f:
        return list(csv.DictReader(f,delimiter=';'))
# ludnosc regionow
reg=rd('ludzie-regiony.csv'); days=sorted({int(x['dzien']) for x in reg}); d0,d1=days[0],days[-1]
K=collections.defaultdict(lambda: collections.defaultdict(float))
for x in reg:
    d=int(x['dzien'])
    if d==d0:
        k=x['krolestwo'] or '(bez krolestwa)'; K[k]['lud0']+=fl(x['ludzie']); K[k]['twierdze0']+=1
    if d==d1:
        k=x['krolestwo'] or '(bez krolestwa)'; g=K[k]; g['lud']+=fl(x['ludzie']); g['lud_miasta']+=fl(x['ludzie_osada']); g['lud_wsie']+=fl(x['ludzie_wsie']); g['twierdze']+=1; g['wsie']+=fl(x['wsie'])
        if x['rodzaj']=='miasto': g['miasta']+=1; g['dobrobyt']+=fl(x['dobrobyt'])
# rody: srednia 28 dob
bud=rd('budzet-rodow.csv'); bd=sorted({int(x['dzien']) for x in bud}); win=set(bd[-28:])
for x in bud:
    if int(x['dzien']) in win and x['rodzaj']!='gracz':
        k=x['krolestwo'] or '(bez krolestwa)'; g=K[k]
        for a,b in (('ziemia','d_ziemia'),('korona','d_korona'),('wplyw','wplyw_doby'),('zoldp','zold_partii'),('zoldz','zold_zalog'),('ludzp','ludzi_partie'),('ludzz','ludzi_zalogi')):
            g[a]+=fl(x[b])/28
# lud: dochod mieszczan i podzamczy (ostatnie 4 probki)
kl=rd('kiesa-ludu.csv'); kd=sorted({int(x['dzien']) for x in kl}); kw=set(kd[-4:])
for x in kl:
    if int(x['dzien']) in kw:
        k=x['krolestwo'] or '(bez krolestwa)'; g=K[k]; g['lud_dochod']+=fl(x['dochod'])/len(kw); g['kiesy']+=(fl(x['kiesa_W'])+fl(x['kiesa_R'])+fl(x['kiesa_P']))/len(kw)
# kasy miast: zawor do pana i do korony (28 dob)
km=rd('kasy-miast.csv'); md=sorted({int(x['dzien']) for x in km}); mw=set(md[-28:])
for x in km:
    if int(x['dzien']) in mw:
        k=x['krolestwo'] or '(bez krolestwa)'; g=K[k]; g['zawor']+=fl(x['zawor_do_pana'])/28; g['dokorony']+=fl(x['do_korony'])/28; g['kasa']+=fl(x['kasa'])/28
rows=sorted(K.items(), key=lambda kv:-kv[1]['lud'])
tot=collections.defaultdict(float)
print(f"{'krolestwo':34s} {'ludzie':>8s} {'miast':>5s} {'twier':>5s} {'wsi':>4s} | {'ziemia':>7s} {'od kor.':>7s} {'wplyw':>8s} | {'lud zar.':>8s} | na 1000 ludzi: {'ziemia':>6s} {'wplyw':>6s} {'lud':>6s} | {'wojsko':>6s} {'na 1000':>7s}")
for k,g in rows:
    if g['lud']<=0: continue
    p=g['lud']/1000; woj=g['ludzp']+g['ludzz']
    print(f"{k[:34]:34s} {g['lud']/1000:7.0f}k {g['miasta']:5.0f} {g['twierdze']:5.0f} {g['wsie']:4.0f} | {g['ziemia']/1000:6.1f}k {g['korona']/1000:6.1f}k {g['wplyw']/1000:7.1f}k | {g['lud_dochod']/1000:7.1f}k | {g['ziemia']/p:13.1f} {g['wplyw']/p:6.1f} {g['lud_dochod']/p:6.1f} | {woj/1000:5.1f}k {woj/p:7.1f}")
    for a in ('lud','ziemia','korona','wplyw','lud_dochod','ludzp','ludzz','miasta','twierdze','wsie'): tot[a]+=g[a]
p=tot['lud']/1000; woj=tot['ludzp']+tot['ludzz']
print(f"{'RAZEM':34s} {tot['lud']/1000:7.0f}k {tot['miasta']:5.0f} {tot['twierdze']:5.0f} {tot['wsie']:4.0f} | {tot['ziemia']/1000:6.1f}k {tot['korona']/1000:6.1f}k {tot['wplyw']/1000:7.1f}k | {tot['lud_dochod']/1000:7.1f}k | {tot['ziemia']/p:13.1f} {tot['wplyw']/p:6.1f} {tot['lud_dochod']/p:6.1f} | {woj/1000:5.1f}k {woj/p:7.1f}")
print('doby',d0,d1,'| lud start razem', sum(g['lud0'] for g in K.values())/1000)
