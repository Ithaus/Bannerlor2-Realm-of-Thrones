import csv,sys
rows=list(csv.DictReader(open(sys.argv[1],encoding='utf-8',errors='replace'),delimiter=';'))
def f(x):
    try: return float(x)
    except: return 0.0
for d in sorted(set(int(r['dzien']) for r in rows)):
    out=[]
    for m in ('Westeros','Essos'):
        R=[r for r in rows if int(r['dzien'])==d and r['rynek']==m]
        D=sum(f(r['zboze_popyt_zaplacony_szt_d']) for r in R); S=sum(f(r['zboze_polka_szt']) for r in R)
        emptyD=sum(f(r['zboze_popyt_zaplacony_szt_d']) for r in R if f(r['zboze_dni_polki'])<0.5)
        glutS=sum(f(r['zboze_polka_szt']) for r in R if f(r['zboze_dni_polki'])>30)
        want=sum(f(r['wsie_chciane_szt_7d']) for r in R); got=sum(f(r['wsie_kupione_szt_7d']) for r in R)
        ng=sum(f(r['wsie_bez_kiesy_szt_7d']) for r in R); nn=sum(f(r['wsie_bez_towaru_szt_7d']) for r in R); el=sum(f(r['wsie_w_innym_miescie_szt_7d']) for r in R)
        out.append('%s D=%.0f/d shelf=%.0f (%.1f d) Dempty=%.0f%% glutShelf=%.0f%% vil want/got/nogold/nogoods/else %.0f/%.0f/%.0f/%.0f/%.0f'%(m,D,S,S/D if D else 0,100*emptyD/D if D else 0,100*glutS/S if S else 0,want,got,ng,nn,el))
    print(d,' | '.join(out))
