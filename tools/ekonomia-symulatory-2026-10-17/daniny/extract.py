import sys, re, os
src, out, days = sys.argv[1], sys.argv[2], sys.argv[3].split(',')
pref = ["Przeplywy osad","Kasy miast (169c)","Nadwyzka kas","Kasy zaulkow","Kiesa ludu","Prawa panskie","Podatek od nadwyzki","Danina z oszczednosci","Danina pokoju","Dziesiecina","Straznik obiegu","Pozyczki","Zbytki ludu","Kupione z oferty","Podstawa robocizny","Roboty ludu","Notable","Obieg","Werbunek","Korona","Renty korony","Skarbce:","Kredyt korony","Doplata wedlug potrzeby","Tarczowe","Stawka wojenna pana","Dary koron","Chleb biedoty","Rynek i korona","Monopole korony","Myto","Ludnosc:","Majatki BK","Utarg wsi","Gotowka wsi","Zawor zamkow","Wyrownanie panow","Pompy","Pieniadz swiata","D staly","Miara historyczna","Budzet rodow (166)","Kupcy zywnosci","Markietani","IronBank: dzien","Oplata za werbunek","Marza kupcow","Karawany - zysk","Dwor - towar","Rody z luzem","Noc-ekonomia (NE): obieg","Wydatki rycerzy","Rada (3.5)","Oblezone (E5/A4)","Budowy oplacone","Weterani - pieniadze","KS: pieniadze planu","Lojalnosc za danine","Zold:","Targ przy zamku","Karczmy","Wywoz kupcow","Podzamcze"]
fo = open(out,'w',encoding='utf-8')
with open(src,encoding='utf-8',errors='replace') as f:
    for line in f:
        body = line[11:] if len(line)>11 else line
        if not any(body.startswith(p) for p in pref): continue
        if not any(('dzien '+d) in line[:200] or ('doba '+d) in line[:200] for d in days): continue
        fo.write(line)
fo.close()
print(os.path.getsize(out))
