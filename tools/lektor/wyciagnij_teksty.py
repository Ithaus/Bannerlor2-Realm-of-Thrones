# Wyciaga teksty okien wydarzen ROT (Title / UpperText / LowerText) z dekompilacji rot-events-all.cs do teksty.json.
import re, sys, json

src, dst = sys.argv[1], sys.argv[2]
t = open(src, encoding='utf-8', errors='replace').read()
parts = re.split(r'\n(?=(?:public|internal)[^\n]*\bclass\s+\w+)', t)
STR = r'"\{=([A-Za-z0-9_]+)\}((?:[^"\\]|\\.)*)"'


def grab(p, field):
    return re.findall(field + r'\s*=\s*\(\(object\)new TextObject\(' + STR, p)


def clean(s):
    return s.replace('\\n', '\n').replace('\\"', '"').replace('\\\\', '\\')


out = []
for p in parts:
    m = re.search(r'class\s+(\w+)', p)
    if not m:
        continue
    cls = m.group(1)
    ti, up, lo = grab(p, 'Title'), grab(p, 'UpperText'), grab(p, 'LowerText')
    n = max(len(ti), len(up), len(lo))
    for i in range(n):
        def g(L):
            if i < len(L):
                return L[i]
            return L[-1] if L else ('', '')
        a, b, c = g(ti), g(up), g(lo)
        if not (a[1] or b[1] or c[1]):
            continue
        out.append({'cls': cls, 'n': i, 'title_id': a[0], 'title': clean(a[1]), 'upper_id': b[0], 'upper': clean(b[1]),
                    'lower_id': c[0], 'lower': clean(c[1])})
json.dump(out, open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(len(out), sum(len(x['title']) + len(x['upper']) + len(x['lower']) for x in out))
for x in out:
    print(x['cls'], x['n'], '|', x['title'][:45].encode('ascii', 'replace').decode(), '|', len(x['upper']) + len(x['lower']))
