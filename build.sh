#!/usr/bin/env bash
# Buduje wszystkie piec modow. Wymaga .NET SDK i bibliotek gry w libs/ (patrz libs/README.md).
set -e
cd "$(dirname "$0")"
for m in Armoury RealisticCaptivity GrandTourney ForgeView CrashScribe; do
  echo "=== $m ==="
  dotnet build "$m/$m.csproj" -c Release -v q --nologo
done
echo
echo "Gotowe. DLL-e leza w <Mod>/bin/Release/<Mod>.dll"
