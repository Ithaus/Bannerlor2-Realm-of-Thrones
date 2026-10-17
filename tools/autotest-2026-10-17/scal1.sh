#!/usr/bin/bash
# scal1.sh <klucz> - scalenie w-toku/noc-<klucz> do zb/int (w-toku/zbior-e8): merge, scal.sh, kontrola narzedzia, gen_mcm, build ruszonych modow, commit
K="$1"; BR="w-toku/noc-$K"; BASE="${2:-39f8fe72}"
Z="/c/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb"
LIBS='C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/lancuch/libs'
CHK="C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/c36b73ca-1de0-4b1b-80ce-71bd02afe545/scratchpad/scal/sprawdz_narzedzie.py"
cd "$Z/int" || exit 1
if [ -z "$(git diff --name-only --diff-filter=U)" ] && [ ! -f .git/MERGE_HEAD ] && ! git rev-parse -q --verify MERGE_HEAD >/dev/null; then
  git merge --no-ff --no-commit "$BR" 2>&1 | tail -3
fi
U=$(git diff --name-only --diff-filter=U)
if [ -n "$U" ]; then
  echo "konflikty: $U"
  bash /c/Users/GAME/Bannerlor2-Realm-of-Thrones/tools/scalanie/scal.sh "$BR" "$BASE" 2>&1 | grep -v "^\s*File\|^\s*\^" | tail -8
fi
# McmSettings.cs: --ours + gen_mcm; Settings.cs i inne - zostaw do recznego, jesli znaczniki
if git diff --name-only --diff-filter=U | grep -qx Armoury/src/McmSettings.cs; then git checkout --ours Armoury/src/McmSettings.cs; fi
M=$(grep -l "^<<<<<<<\|^>>>>>>>" $(git diff --name-only 2>/dev/null; git diff --cached --name-only 2>/dev/null) 2>/dev/null | sort -u | grep -v McmSettings.cs)
if [ -n "$M" ]; then echo "ZNACZNIKI KONFLIKTU w: $M"; exit 2; fi
python -I "$CHK" tools/sprawdz_logi.py 2>&1 | tail -1 | grep -q "kompilacja ok" || { echo "NARZEDZIE: blad (dispatch w petli?)"; python -I "$CHK" tools/sprawdz_logi.py 2>&1 | tail -4; exit 3; }
python -I - <<'PY' || { echo "NARZEDZIE: zdublowane nazwy globalne"; exit 3; }
import re,io,collections,sys
L=io.open('tools/sprawdz_logi.py',encoding='utf-8').read().splitlines()
d=collections.defaultdict(list)
for i,l in enumerate(L):
    m=re.match(r'^([A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)',l)
    if m: d[m.group(1)].append(i+1)
x={k:v for k,v in d.items() if len(v)>1}
print('globalne zdublowane:',x); sys.exit(1 if x else 0)
PY
python -I tools/gen_mcm.py | tail -1
rcs=""
for m in Armoury CrashScribe RealisticCaptivity GrandTourney ForgeView; do
  if git diff --cached --name-only HEAD | grep -q "^$m/" || git diff --name-only HEAD | grep -q "^$m/"; then
    dotnet build $m/$m.csproj -c Release -v q --nologo "-p:GameLibs=$LIBS" > "$Z/build-int-$m.log" 2>&1; rc=$?
    rcs="$rcs $m=$rc"; [ $rc -eq 0 ] || { echo "BUILD $m rc=$rc"; grep -m5 "error" "$Z/build-int-$m.log"; exit 4; }
  fi
done
echo "buildy:$rcs"
git add -A Armoury CrashScribe RealisticCaptivity GrandTourney ForgeView tools CHANGELOG.md 2>/dev/null
git reset -q -- '*/build.log' '*.log' 2>/dev/null
git status --short | grep -v "^??" | grep -v "^[MAD]  " | head
git commit -q -m "Scalenie $BR

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" && git log --oneline -1 | cut -c1-70
