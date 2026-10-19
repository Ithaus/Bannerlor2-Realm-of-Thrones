#!/bin/bash
# run.sh <tag> <budzety.csv> [VAR=val ...] - harness Planu f26-wolni (kopia h4-feedswiat + slad okregu KS_D2 i KS_HTP=1 - klucz Ks Home Work Trade Prices); klucze jak w grze fin98 (KS_FEED=2 KS_COLD=1 KS_STAPLE=1 KS_SLOW=1 KS_CHEAP=1 KS_FMAX=1.25 KS_CLOTH=1 KS_CREDIT=1 KS_CLEAR=1); alfa i budzety z ks-budzety.csv biegu (ostatnia doba); KS_ALPHA=plik id;alfa nadpisuje okregi. Wynik out/<tag>.out i out/<tag>.d2.csv
S="C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad"
H="$(cd "$(dirname "$0")" && pwd)"
TAG="$1"; BUD="$2"; shift 2
cd "$H" || exit 1; mkdir -p out
export KS_ITEMS1="$S/kmoney/h4/out/items_r3.json" KS_BUDGETS="$BUD" KS_GOLD="$S/kmoney/h4/out/gold28.csv" KS_WORLD="$S/ksim/sim/world_r2.json"
export KS_PLANS=${KS_PLANS:-5} KS_D2="out/$TAG.d2.csv" KS_FEED=2 KS_COLD=1 KS_STAPLE=1 KS_SLOW=1 KS_CHEAP=1 KS_FMAX=1.25 KS_CLOTH=1 KS_CREDIT=1 KS_CLEAR=1
for kv in "$@"; do export "$kv"; done
"$H/bin/Release/net8.0/h.exe" "$S/ks/ks_dump.json" 0.47 full > "out/$TAG.out" 2>&1
echo "rc=$? $TAG"
