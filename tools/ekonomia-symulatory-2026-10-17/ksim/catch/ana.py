# analysis of prototype per-catchment TSVs: args tsv...
import csv, sys
def load(p):
    rows=[r for r in csv.DictReader(open(p,encoding='utf-8'),delimiter='\t') if r['id']!='P2']
    return {r['id']:r for r in rows}
f=lambda r,k: float(r[k])
foods=['grain','fish','meat','cheese','butter','Egg','apple','carrot','orange','grape','olives','honey']
kc={'grain':3300,'fish':1100,'meat':2200,'cheese':3500,'butter':7000,'Egg':1450,'apple':500,'carrot':400,'orange':450,'grape':700,'olives':1150,'honey':3000}
def summary(nm, X):
    for mk in ('0','1'):
        Dt=0; sh=0; shn=0; gl=0; nb=0; nl=0; n=0
        for k,r in X.items():
            if r['mk']!=mk: continue
            D=sum(f(r,g+'_D')*kc[g] for g in foods); S=sum(f(r,g+'_S')*kc[g] for g in foods); Sn=sum(f(r,g+'_Snet')*kc[g] for g in foods)
            if D<=0: continue
            n+=1; Dt+=D; sh+=max(0,D-S); shn+=max(0,D-Sn); gl+=max(0,Sn-D)
            if 0.95<=Sn/D<=1.05: nb+=1
            elif Sn/D<0.95: nl+=1
        print('%-8s %s kcal: own shortfall %.1f%%, after corridors short %.1f%% glut %.1f%%, catchments in 0.95-1.05 %d/%d, below %d'%(nm,'WE'[int(mk)],100*sh/Dt,100*shn/Dt,100*gl/Dt,nb,n,nl))
if __name__=='__main__':
    for p in sys.argv[1:]: summary(p, load(p))
