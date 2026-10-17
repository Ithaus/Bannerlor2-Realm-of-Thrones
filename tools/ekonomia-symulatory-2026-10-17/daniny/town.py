import re
from collections import defaultdict
items=[('prowizja',r'prowizja od sprzedazy partiom \(do licznika cel (\d+)'),('bk_wyroby',r'warsztaty BK - kasa placi za wyroby (-?\d+)'),('bk_wsad',r'warsztaty placa za wsad ([+-]?\d+)'),
('bk_rynek_wsi',r'BK rynek wsi \(wsie placa miastom\) ([+-]?\d+)'),('skup_zbroj',r'skup nadwyzek zbrojowni (-?\d+)'),('inne',r'inne ([+-]?\d+), sakwy'),('naprawy',r'naprawy ([+-]?\d+)')]
sets={'base':['D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-16_19-02-22.log'],'ks':['D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_06-07-51.log']}
for n,ps in sets.items():
    data=defaultdict(dict)
    for p in ps:
        for line in open(p,encoding='utf-8',errors='replace'):
            b=line[11:]
            if b.startswith('Przeplywy osad (przyczyny)'):
                d=int(re.search(r'dzien (\d+)',b).group(1)); seg=b.split('| kasy zamkow')[0]
                for m,rx in items:
                    mm=re.search(rx,seg)
                    if mm: data[d][m]=float(mm.group(1))
            if b.startswith('Kasy zaulkow'):
                pass
    days=sorted(data)[-28:]
    print(n,{m:round(sum(data[d].get(m,0) for d in days)/len(days)) for m,_ in items})
