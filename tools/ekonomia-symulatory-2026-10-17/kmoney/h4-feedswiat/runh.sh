#!/bin/bash
# runh.sh <tag> [VAR=value ...] - harness Planu feed-swiat (kopia kmoney h4) na ks_dump.json 16.10; budzety i alfa jak runh.sh fali 17.10 (dane: scratchpad sesji
# 6ba92574 - kopia D:/Backup-Bannerlord/scratch-2026-10-17; ks-budzety.csv z logi-testow). Wynik: out/<tag>.out (+ csv okregow). Podsumowanie: python -I summ.py <tag>...
# Przelaczniki: KS_FEED=0/1/2 (prog wyl./strefy zimne/swiat), KS_COLD=1 (zimna ziemia), KS_STAPLE=1, KS_SLOW=1, KS_CLEAR=1 (klucze feed-swiat),
# KS_CHEAP=1 KS_FMAX=1.25 KS_CLOTH=1 KS_CREDIT=1 (klucze scalenia 100eaedf jak w grze), KS_PLANS=n, KS_MUMAX, KS_NOMU=1, KS_NOETA=1, KS_PGEQ=1, KS_EQDBG=plik.csv,
# KS_VILMU=1 (recenzja: klucz Ks Plan Markets Clear Villages - narzut takze w koszyku wsi).
S="C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad"
H="$(cd "$(dirname "$0")" && pwd)"
TAG="$1"; shift 1
cd "$H" || exit 1
mkdir -p out
export KS_ITEMS1="$S/kmoney/h4/out/items_r3.json" KS_BUDGETS=D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30/ks-budzety.csv KS_ALPHA="$S/kmoney/h4/out/alpha_net2.csv" KS_GOLD="$S/kmoney/h4/out/gold28.csv"
export KS_WORLD="$S/ksim/sim/world_r2.json" KS_PLANS=${KS_PLANS:-5} KS_OUT="out/$TAG.csv"
for kv in "$@"; do export "$kv"; done
"$H/bin/Release/net8.0/h.exe" "$S/ks/ks_dump.json" 0.47 full > "out/$TAG.out" 2>&1
echo "rc=$? $TAG"
