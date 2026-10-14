import sys, io, subprocess, re
tree, other = sys.argv[1], sys.argv[2]
p = tree + '/CHANGELOG.md'
raw = open(p, 'rb').read()
crlf = b'\r\n' in raw
txt = raw.decode('utf-8')
lines = txt.split('\n')
out = []; state = 0; nconf = 0
for ln in lines:
    s = ln.rstrip('\r')
    if s.startswith('<<<<<<< '): state = 1; nconf += 1; continue
    if s.startswith('=======') and state == 1: state = 2; continue
    if s.startswith('>>>>>>> ') and state == 2: state = 0; continue
    out.append(ln)
res = '\n'.join(out)
open(p, 'wb').write(res.encode('utf-8'))
def heads(t): return [l.rstrip('\r') for l in t.split('\n') if l.startswith('## ')]
def show(ref): return subprocess.run(['git', '-C', tree, 'show', ref + ':CHANGELOG.md'], capture_output=True).stdout.decode('utf-8')
ho, ht, hr = heads(show('HEAD')), heads(show(other)), heads(res)
missing = [h for h in set(ho) | set(ht) if h not in hr]
from collections import Counter
dup = [h for h, c in Counter(hr).items() if c > max(Counter(ho)[h], Counter(ht)[h])]
print('konflikty', nconf, '| naglowki HEAD', len(ho), 'e8', len(ht), 'wynik', len(hr), '| brakuje', len(missing), '| zdublowane', len(dup), '| CRLF', crlf)
for h in missing[:5]: print('  BRAK:', h[:100])
for h in dup[:5]: print('  DUBEL:', h[:100])
