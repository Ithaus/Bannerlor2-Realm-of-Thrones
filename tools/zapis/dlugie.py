# Rozbior dlugich napisow Armoury z zapisu (tylko odczyt).
import sys, struct, zlib, collections
sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')
sys.path.insert(0, sys.argv[0].rsplit('/',1)[0])
from napisy import strings_of, r3

def longs(path):
    b = strings_of(path)
    i = 0
    nf = struct.unpack_from('<i', b, i)[0]; i += 4 + 10 * nf
    ne = struct.unpack_from('<i', b, i)[0]; i += 4
    out = []
    for k in range(ne):
        i += 9
        tl = struct.unpack_from('<i', b, i)[0]
        if 4 + tl > 20000: out.append(b[i+4:i+4+tl].decode('utf-8', 'replace'))
        i += 4 + tl
    return out

for path in sys.argv[1:]:
    print('==', path)
    for s in longs(path):
        if '>' in s[:80] and '~' in s:
            recs = [r for r in s.split('~') if r]
            per = collections.Counter(r.split('>')[0] for r in recs)
            tmpl = sum(1 for r in recs if r.split('>')[2:3] == ['T'])
            items = sum(len(r.split('>')[3].split(',')) for r in recs if len(r.split('>')) == 4 and r.split('>')[3])
            print('  RecruitKit: %d B, kompletow %d (wzorzec %d), notabli %d, przedmiotow %d, najwiecej u jednego %s' % (len(s), len(recs), tmpl, len(per), items, per.most_common(5)))
            dist = collections.Counter(min(v, 30) for v in per.values())
            print('    kompletow na notabla (30 = 30+):', sorted(dist.items()))
        elif s[:3] == 'v1;':
            print('  OutlawLaw: %d B, regionow %d' % (len(s), s.count('|')))
        elif ',' in s[:100] and ';' in s:
            recs = [r for r in s.split(';') if r]
            parties = collections.Counter(r.split(',')[0] for r in recs)
            cnt = sum(int(r.split(',')[-1]) for r in recs if r.split(',')[-1].isdigit())
            print('  party,item,stan,ile: %d B, wpisow %d, druzyn %d, sztuk %d, najwiecej %s' % (len(s), len(recs), len(parties), cnt, parties.most_common(4)))
            kinds = collections.Counter(p.split('_party')[0].split('_')[0] if '_party' in p else p[:12] for p in parties)
            print('    rodzaje identyfikatorow:', kinds.most_common(8))
        else:
            print('  inny: %d B: %s' % (len(s), s[:200]))
