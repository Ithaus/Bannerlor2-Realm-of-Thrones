export const meta = {
  name: 'szybkie-wlaczenia-i-drobne-bledy',
  description: 'Inwentarz 14.10: A8/A9 szybkie wlaczenia (jazda AI, amunicja AI, sufit ceny AI, luki Zlotej Kompanii) i A12-A14 drobne bledy (etap 8, bieg dwuletni, audyt) - dwoch autorow rownolegle, dwie recenzje z poprawka',
  phases: [ { title: 'Kod' }, { title: 'Recenzja' } ],
}
const S3 = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`
const LIBS = S3 + String.raw`\lancuch\libs`
const SC = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`
const REPO = String.raw`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`
const INW = REPO + String.raw`\docs\CO-ZOSTALO-DO-KONCA-MODA-2026-10-14.md`
const INWD = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\inwentarz`
const LOGS = String.raw`D:\Backup-Bannerlord\logs\2026-10-14-noc`
const BASE = 'aca89cc'
const envF = (DZ, BR) => String.raw`Mody Bannerlorda 1.4.8 (Realm of Thrones + BannerKings + DTE + nasze Armoury / CrashScribe / RC). Przeczytaj ${REPO}\CLAUDE.md (caly; zasada 0, obieg zamkniety, pulapki rozdz. 7, zasada 4 - ozywianie martwego kodu: policz, ile pracy przybedzie). Katalog roboczy = folder gry: NIC tam nie zapisuj, gry NIE uruchamiaj, niczego nie wgrywaj.
DRZEWO: ${DZ} (galaz ${BR}, od w-toku/zbior-e8 ${BASE} = WGRANIE 51). Pracuj TYLKO w nim. Rownolegle inne zespoly robia: wojsko w pokoju (ClanBudget PeaceWageShare), plotno (TownFolk.Luxury, TownCrafts, CaravanBulk), drogi (MapRoadsProbe) - tych miejsc nie ruszaj. Napisy po angielsku, komentarze po polsku bez polskich znakow, .cs z CRLF, kazda zmiana zachowania pod kluczem MCM (off = ${BASE}); Jeffa Armoury.json: sprawdz, czy klucz tam jest (Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json - tylko ODCZYT) - jesli jest, zmiana domyslnej NIE zadziala (CLAUDE.md rozdz. 7) - opisz w CHANGELOG, co Jeff musi przestawic w MCM. Po Settings.cs "python tools/gen_mcm.py" ("python3" to atrapa). Build: cd "${DZ}\<Mod>" && dotnet build <Mod>.csproj -c Release -v q --nologo "-p:GameLibs=${LIBS}" > build.log 2>&1; echo rc=$? (kod 0). Commit konczy sie "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>". Nie uzywaj spawn_task. ZWRACAJ KROTKO (max 25 linii).
ZRODLA: inwentarz ${INW} (pozycje A8, A9, A12, A13, A14 - z dowodami; szczegoly w ${INWD}\{etapy1-4,etapy5-8,noc}\*.md), raport biegu dwuletniego ${REPO}\docs\test-zbiorczy\BIEG-DWULETNI-2026-10-14.md (rozdz. 7 lista NIE), audyt ${REPO}\docs\test-zbiorczy\audyt-integracji-2026-10-14\; logi ${LOGS}\Armoury-2026-10-13_21-14-13.log (dwuletni) i _20-41-38 (120 dob). Jeff dal pelne uprawnienia - decyzje "DECYZJA (pelne uprawnienia)" w CHANGELOG z liczbami.`
const DA = SC + String.raw`\zb\swl`, BA = 'w-toku/szybkie-wlaczenia'
const DB = SC + String.raw`\zb\drb`, BB = 'w-toku/drobne-bledy-14'
const zA = String.raw`TWOJE: A8 jazda AI (cel planu 6.14: jazda t3+ ok. 27.5%, dzis 17.6%; Dothrakowie 60%; AiMountMarketSharePercent 25 -> 40 i co jeszcze wiaze - konie na targu, uprzaz H-1/H-4b), A9 klucze "W CIENIU" (AiAmmoUse - odzysk amunicji AI jak gracza, AiGearMaxPriceFactor - sufit ceny zakupow AI; plan 8.4, 8.8) - wlaczyc po sprawdzeniu, ze kod dziala (przejdz, policz skutek na logu), plan 1.6 luki Zlotej Kompanii (Army175GoldenBows) - wlaczyc, jesli kod gotowy. Kazde: przyczyna, decyzja, liczby, linia logu / prog (grupa szybkie), CHANGELOG, commit. NIE wypychaj.`
const zB = String.raw`TWOJE: A12-A14 z inwentarza - drobne bledy etapu 8, NIE z biegu dwuletniego (m.in. C-4b kasy ponizej zapasu, H-4b reszta uprzezy, P3-2, Q-3 zawracanie partii) i z audytu integracji (DROBNE niezrobione), oraz L5b-2 karawany wroga: 0 zajec w 39 zdobyciach - sprawdz, czy kod moze sie odpalic (jesli nie - napraw). Zrob tyle, ile sie da bezpiecznie w ok. 2 h, od najwazniejszych; kazda: przyczyna z kodu i logu, poprawka pod kluczem albo bez (gdy to oczywisty blad - opisz), linia logu / prog (grupa drobne14), wpis CHANGELOG. Na koniec lista: zrobione / niezrobione z powodem. NIE wypychaj.`
phase('Kod')
const [a, b] = await parallel([
  () => agent(`${envF(DA, BA)}\n\nJESTES AUTOREM.\n${zA}\n\nZwroc: HEAD, kazda pozycja (zrobione / nie + dlaczego), klucze, liczby, ryzyka.`, { label: 'autor-szybkie', phase: 'Kod' }),
  () => agent(`${envF(DB, BB)}\n\nJESTES AUTOREM.\n${zB}\n\nZwroc: HEAD, lista zrobione / niezrobione, klucze, ryzyka.`, { label: 'autor-drobne', phase: 'Kod' }),
])
phase('Recenzja')
const rv = (DZ, BR, z, opis, k) => `${envF(DZ, BR)}\n\nJESTES RECENZENTEM Z POPRAWKA.\n${z}\n\nDiff: "git -C ${DZ} diff ${BASE}..HEAD". Sprawdz: kazda zmiana ma przyczyne w kodzie i logu; obieg zamkniety; ozywiony kod - ile pracy (zasada 4); klucze off = ${BASE}; Armoury.json Jeffa (czy domyslne zadzialaja); build kod 0 (kazdy ruszony mod); gen_mcm; progi (--grupa ${k}) na logu dwuletnim (py_compile kopii). Popraw (commit). Na koniec wypchnij DWOMA OSOBNYMI poleceniami: "git -C ${DZ} push origin ${BR}" i "git -C ${REPO} push origin ${BR}". Zwroc: HEAD, co poprawiles, oczekiwane liczby w tescie 120 dob, ktore mody przebudowac, ryzyka.\n\nOpis autora:\n${opis}`
const [ra, rb] = await parallel([
  () => agent(rv(DA, BA, zA, a, 'szybkie'), { label: 'rec-szybkie', phase: 'Recenzja' }),
  () => agent(rv(DB, BB, zB, b, 'drobne14'), { label: 'rec-drobne', phase: 'Recenzja' }),
])
return { a, b, ra, rb }
