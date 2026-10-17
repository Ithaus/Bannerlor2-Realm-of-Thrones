import re,sys,collections
L="D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_09-07-09.log"
G=sys.argv[1].split(',')
pat=re.compile(r"Towary: dzien (\d+) - (\S+) \(\d+ d\): zapas (\d+) \(([-+]\d+)\) \| produkcja (\d+) \[(.*?)\] \| zuzycie (\d+) \[(.*?)\] \|.*?przewoz \[(.*?)\] \| posiadacze \[(.*?)\]")
def kv(s):
    d={}
    for m in re.finditer(r"([^,;\[\]()]+?) (\d+)(?: \(|,|$|;)",s):
        d[m.group(1).strip()]=int(m.group(2))
    return d
out=collections.defaultdict(dict)
for line in open(L,encoding='utf-8',errors='replace'):
    if 'Towary: dzien' not in line: continue
    m=pat.search(line)
    if not m: continue
    d=int(m.group(1)); g=m.group(2)
    if g not in G: continue
    out[g][d]=m
for g in G:
    print("=====",g)
    for d in sorted(out[g]):
        if (d-109074)%7!=0: continue
        m=out[g][d]
        hold=kv(m.group(10)); prod=m.group(5); cons=m.group(7)
        print(d, "stock",m.group(3),m.group(4),"prod",prod,"cons",cons,"| hold:"," ".join("%s=%s"%(k[:10],v) for k,v in hold.items()))
