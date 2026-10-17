import re,collections
L=r'D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_01-15-30.log'
w0,w1=109140,109167
rt=re.compile(r'Towary: dzien (\d+) - (\S+) \(\d+ d\): zapas (\d+) .*?wsie \(zakupy w miescie, E5/B1\) (\d+)')
rq=re.compile(r'Koszyk stanow \(ZC4\): dzien (\d+) .*?potrzeba szt\./d przy lambdzie \(miasta \+ wsie; w nawiasie wczoraj z ksiegi towarow: mieszczanie / zuzycie razem\): ([^|]*)')
ru=re.compile(r'KS: koszyk niezaplacony \(plan doba (\d+).*?\| Westeros wsie: ([^;]*); miasta:[^|]*\| Essos wsie: ([^;]*);')
buy=collections.defaultdict(list); paid=collections.defaultdict(list); unp={}
for s in open(L,encoding='utf-8',errors='replace'):
    if 'Towary: dzien' in s and 'E5/B1' in s:
        m=rt.search(s)
        if m and w0<=int(m.group(1))<=w1: buy[m.group(2)].append(int(m.group(4)))
        continue
    if 'Koszyk stanow (ZC4): dzien' in s:
        m=rq.search(s)
        if m and w0<=int(m.group(1))<=w1:
            for it in m.group(2).split(', '):
                mm=re.match(r'\s*(\S+) ([\d]+)\+([\d]+)',it)
                if mm: paid[mm.group(1)].append(int(mm.group(3)))
        continue
    if 'KS: koszyk niezaplacony' in s:
        m=ru.search(s)
        if m and w0-7<=int(m.group(1))<=w1:
            u=collections.Counter()
            for part in (m.group(2),m.group(3)):
                for it in part.split(', '):
                    mm=re.match(r'\s*(\S+) (\d+)',it)
                    if mm: u[mm.group(1)]+=int(mm.group(2))
            unp[int(m.group(1))]=u
U=collections.Counter()
for d in unp: 
    for k,v in unp[d].items(): U[k]+=v/len(unp)
kc={'grain':33,'meat':22,'WhaleMeat':22,'fish':11,'cheese':36,'butter':72,'Egg':14.5,'apple':5,'carrot':4,'orange':4.5,'grape':7,'olives':11.5,'date_fruit':28,'oil':88,'honey':30,'beer':4,'wine':7.5,'mead':10}
items=['grain','salt','beer','wine','mead','meat','fish','cheese','butter','Egg','apple','carrot','orange','grape','olives','date_fruit','oil','honey','spice','linen','felt','leather','tools','pottery']
TW=TP=TB=0
print('%-10s %7s %7s %7s %7s | %6s %6s'%('item','paid','unpaid','full','bought','b/paid','b/full'))
for it in items:
    p=sum(paid[it])/len(paid[it]) if paid[it] else 0; u=U.get(it,0); b=sum(buy[it])/len(buy[it]) if buy[it] else 0
    f=p+u
    print('%-10s %7.0f %7.0f %7.0f %7.0f | %6.2f %6.2f'%(it,p,u,f,b,(b/p if p else 0),(b/f if f else 0)))
    k=kc.get(it,0); TW+=f*k; TP+=p*k; TB+=min(b,f*1.5)*k
print('kcal (k): full %.0f paid %.0f (%.1f%%) bought(ledger, all village buys) %.0f (%.1f%% of full)'%(TW,TP,100*TP/TW,TB,100*TB/TW))
g=lambda it: (sum(paid[it])/len(paid[it]), U.get(it,0), sum(buy[it])/len(buy[it]))
pg,ug,bg=g('grain')
print('grain: full %.0f, paid %.0f, bought %.0f -> shortfall vs paid %.0f pcs = %.0f k kcal = %.1f pts of basket kcal'%(pg+ug,pg,bg,pg-bg,(pg-bg)*33,100*(pg-bg)*33/TW))
