# per-village year (x364), shares of handed H (baseline world proportions, per day 275.7k handed)
Hb=275.7
sh=dict(toll=12.6/Hb, counter=99.7/Hb, estates=24.0/Hb, purse=151.6/Hb, transfers=22.0/Hb, sut_p=38.6/Hb, sut_l=18.8/Hb, released=11.8/Hb,
        dues=10.3/Hb, levy=75.4/Hb, hoard_c=24.9/Hb, hoard_l=25.6/Hb, hoard_t=12.6/Hb, bk=39.1/Hb, basket=13.4/Hb, tithe=6.7/Hb, loans=5.1/Hb, w7=1.4/Hb, other=6.6/Hb)
gross=1+sh['toll']+sh['transfers']+sh['sut_p']+sh['sut_l']+sh['released']
lord0=sh['counter']+sh['sut_l']+sh['dues']+sh['hoard_l']+sh['w7']+sh['toll']/2
crown0=sh['levy']+sh['hoard_c']+sh['toll']/2
town0=sh['hoard_t']+sh['tithe']
goods0=sh['bk']+sh['basket']+sh['other']
kept0=gross-lord0-sh['estates']-crown0-town0-goods0-sh['loans']
champ=1.2*(1/3)*(1/8); mult=0.6/16; tithe_k=0.12; tithe_mkt=0.05
rent=lord0-sh['toll']/2-champ-mult
inflow=1-sh['estates']-champ-tithe_mkt+sh['transfers']+sh['sut_p']+sh['sut_l']+sh['released']
print('gross %.3f lord0 %.3f crown0 %.3f town0 %.3f goods0 %.3f kept0 %.3f | rent %.3f inflow %.3f champ %.3f mult %.3f'%(gross,lord0,crown0,town0,goods0,kept0,rent,inflow,champ,mult))
for name,H in (('bieda p10',60),('mediana',304),('srednia baza',483),('bogata p90',1117)):
    y=364*H/1000
    for g,lab in ((0,'pokoj'),(1,'wojna g1'),(2,'wojna g2')):
        sub=g*(1/15)*(1+ (7.3 if H==483 else 0)/ (91*H))  # purse ~ small
        left=inflow-rent-sub
        if lab=='pokoj':
            print('%-13s H %4d/d: PRZED gross %6.1fk lord %6.1fk notable %5.1fk crown %6.1fk towns %5.1fk goods %5.1fk kept %4.1fk'%(name,H,gross*y,lord0*y,sh['estates']*y,crown0*y,town0*y,goods0*y,kept0*y))
        print('   PO %-8s: in-kind tithe %5.1fk champart %5.1fk multure %5.1fk | rent %6.1fk crown subs %5.1fk + toll %4.1fk | left for basket/tools/savings %6.1fk (alpha~%.2f)'%(lab,tithe_k*y,champ*y,mult*y,rent*y,sub*y,sh['toll']/2*y,left*y,0.95*left))
