#!/usr/bin/bash
# scalanie galezi do zb\int wedlug pamieci "scalanie-galezi-rot-patch-fuzz"; $1 = galaz (w-toku/...), $2 = baza galezi
B="$1"; BASE="$2"; TMP=$(mktemp -d)
for f in Armoury/src/ArmouryBehavior.cs tools/sprawdz_logi.py; do
  if git diff --name-only --diff-filter=U | grep -qx "$f"; then
    git checkout --ours "$f"
    git diff "$BASE" "$B" -- "$f" > "$TMP/p.patch"
    patch -p1 --fuzz=3 --no-backup-if-mismatch < "$TMP/p.patch" | grep -i "fail\|succeed with fuzz" ; echo "patch $f rc=${PIPESTATUS[0]}"
    if [ -f "$f.rej" ]; then
      python -I - "$f" <<'PY'
import re,sys
f=sys.argv[1]
r=open(f+'.rej',encoding='utf-8').read().splitlines()
plus=[l[1:] for l in r if l.startswith('+') and not l.startswith('+++')]
minus=[l[1:] for l in r if l.startswith('-') and not l.startswith('---')]
t=open(f,encoding='utf-8',newline='').read()
added=[]
for pl in plus:
    if '.Reset();' not in pl: continue
    mi=[m for m in minus if '.Reset();' in m]
    old=set(re.findall(r'(\w+)\.Reset\(\)',mi[0])) if mi else set()
    for x in re.findall(r'(\w+)\.Reset\(\)',pl):
        if x not in old and (x+'.Reset();') not in t: added.append(x)
other=[l for l in plus if '.Reset();' not in l]
if other: print('UWAGA: rej ma inne linie niz Reset():', len(other)); sys.exit(2)
a='GoodsRates.Reset();'
assert t.count(a)==1, 'kotwica'
t=t.replace(a, a+''.join(' '+x+'.Reset();' for x in added))
open(f,'w',encoding='utf-8',newline='').write(t)
print('Reset dodane:', added)
PY
      [ $? -eq 0 ] && rm "$f.rej"
    fi
    python -I -c "
import sys
f=sys.argv[1]; b=open(f,'rb').read().replace(b'\r\n',b'\n').replace(b'\n',b'\r\n'); open(f,'wb').write(b)" "$f"
  fi
done
if git diff --name-only --diff-filter=U | grep -qx CHANGELOG.md; then
  python -I "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/dbc3d3d1-6a1e-4825-afe7-38583389c73c/scratchpad/union_changelog.py" . "$B" 2>&1 | tail -1
fi
python -I "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/c36b73ca-1de0-4b1b-80ce-71bd02afe545/scratchpad/scal/sprawdz_narzedzie.py" tools/sprawdz_logi.py
echo "zostale konflikty:"; git diff --name-only --diff-filter=U; ls Armoury/src/*.rej tools/*.rej 2>/dev/null
