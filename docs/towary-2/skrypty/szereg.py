# szereg.py LOG PREFIX REGEX1 [REGEX2...] -> dzien;wartosci (pierwsza linia z prefiksem dla danego dnia)
import sys, re
log, prefix = sys.argv[1], sys.argv[2]
rx = [re.compile(r) for r in sys.argv[3:]]
dz = re.compile(r'dzien (\d{6})')
seen = set()
with open(log, encoding='utf-8', errors='replace') as f:
    for line in f:
        i = line.find('] ')
        if i < 0 or not line.startswith(prefix, i + 2):
            continue
        m = dz.search(line)
        if not m: continue
        d = int(m.group(1)) - 108836
        if d in seen: continue
        seen.add(d)
        out = []
        for r in rx:
            mm = r.search(line)
            out.append(';'.join(mm.groups()) if mm else '-')
        print(d, '|', ' | '.join(out))
