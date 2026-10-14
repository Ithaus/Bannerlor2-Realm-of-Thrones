import sys, re, os
d = sys.argv[1]
for fn in sorted(os.listdir(d)):
    if not fn.endswith('.cs'):
        continue
    src = open(os.path.join(d, fn), encoding='utf-8').read()
    for m in re.finditer(r'public override (\S+(?:<[^>]+>)?) (\w+)\(', src):
        name = m.group(2)
        body = src[m.end():m.end() + 1500]
        g = re.search(r'TryEnter\("[^"]+"\)\)\s*\{\s*return ([^;]+);', body)
        if not g:
            continue
        selfcall = re.search(r'(?<![\.\w])' + name + r'\(', body)
        print(fn, name, '| guard-fail ->', g.group(1)[:70], '| wywolanie siebie (bez base.):', bool(selfcall))
