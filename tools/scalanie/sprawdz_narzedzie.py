# po scaleniu: zle wstawki dispatchu (if group ... bez return w nastepnej linii) i zdublowane def w tools/sprawdz_logi.py
import re,collections,sys,py_compile,shutil,tempfile,os
p=sys.argv[1] if len(sys.argv)>1 else 'tools/sprawdz_logi.py'
t=open(p,encoding='utf-8').read().splitlines()
bad=[(i+1,l.strip()[:80]) for i,l in enumerate(t) if re.match(r'\s+if group (==|in) .* and logp:\s*$',l) and not (t[i+1].strip().startswith('return') if i+1<len(t) else False)]
defs=collections.Counter(re.match(r'def (\w+)\(',l).group(1) for l in t if re.match(r'def \w+\(',l))
dup=[k for k,v in defs.items() if v>1]
grp=collections.Counter(m.group(1) for l in t for m in [re.match(r'\s+if group == "(\w+)" and logp:',l)] if m)
dg=[k for k,v in grp.items() if v>1]
d=tempfile.mkdtemp(); q=os.path.join(d,'x.py'); shutil.copy(p,q); py_compile.compile(q,doraise=True)
print('zle wstawki',bad,'| zdublowane def',dup,'| zdublowane grupy',dg,'| kompilacja ok')
sys.exit(1 if bad or dup or dg else 0)
