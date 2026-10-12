export const meta = {
  name: 'e8-kod-i-zbior-5-8-dokonczenie',
  description: 'Recenzja E8/0; etap 8 w dwoch drzewach Armoury (e8-a, e8-c) + E8/RC (e8-b); rownolegle zbior-5-8: E7/R2, W4, dwa przeglady, scalenie a8-gentry0. Autor + recenzent z poprawka po kazdej paczce, bramka limitu konta przed kazda paczka',
  phases: [ { title: 'Zbior 5-8' }, { title: 'E8 watek A' }, { title: 'E8 watek B i C' } ],
}
const S3 = 'C:\\Users\\GAME\\AppData\\Local\\Temp\\claude\\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\\3cf3e0ac-5529-4b68-a794-0edec69cfda7\\scratchpad'
const LANC = S3 + '\\lancuch'
const LIBS = LANC + '\\libs'
const SC = 'C:\\Users\\GAME\\AppData\\Local\\Temp\\claude\\C--Users-GAME-Bannerlor2-Realm-of-Thrones\\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\\scratchpad'
const G = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord'
const REPO = 'C:\\Users\\GAME\\Bannerlor2-Realm-of-Thrones'
const PROJ8 = REPO + '\\docs\\etap8\\PROJEKT-ETAP8-DROBNE-BLEDY-OPTYMALIZACJA-2026-10-12.md'
const P7 = REPO + '\\docs\\etap7\\PROJEKT-ETAP7-MAPA-DROGI-WIOSKI-2026-10-12.md'
const DZ = SC + '\\zb\\58'
const DA = SC + '\\e8\\a'
const DB = SC + '\\e8\\b'
const DC = SC + '\\e8\\c'

const env = (dir, br) => `Mody Bannerlorda 1.4.8 (Realm of Thrones): Armoury / CrashScribe / RealisticCaptivity / GrandTourney / ForgeView (jedno repo). NAJPIERW przeczytaj ${REPO}\\CLAUDE.md (rozdz. 2, 4, 6, 7, 8 - nie laduje sie sam, bo katalog roboczy sesji to folder gry). Katalog roboczy sesji = folder gry: NIC tam nie zapisuj, gry nie uruchamiaj; pracuj tylko w drzewie ${dir} (galaz ${br}; sciezki bezwzgledne, "git -C ${dir} ..."). Na starcie "git -C ${dir} status --short": niezacommitowane zmiany w plikach sledzonych albo nowe pliki zrodlowe to slad przerwanego agenta - jesli to niedokonczona czesc TWOJEGO zadania, "git checkout -- ." i usun te nowe pliki (build.log zostaw), zacznij od nowa. Zasady Jeffa: zamknieta ekonomia (kazde zloto i kazda sztuka ma platnika i odbiorce - nic z niczego, nic w nicosc), bez sztucznych doplat ("jesli ubywa, to ubywa"), ogolny poziom cen zostaje, kazda zmiana = kontrola calosci (CLAUDE.md 8.0: regresje, kolizje, spojnosc, cudzy kod w dekompilacji), tylko zakres zadania - ZADNYCH poprawek "przy okazji" (zauwazone wpisz w CHANGELOG jako "poza zakresem"), Jeff gra dopiero nowa kampanie (NK dozwolone, stary zapis ma sie wczytac bez bledu), napisy w grze po angielsku, komentarze po polsku bez polskich znakow. Pulapki CLAUDE.md 7 (SwitchToMenu w menu oczekiwania; wizerunki na mapie co klatke; globalne wylaczniki po jednym wyjatku - licz potkniecia; ozywianie martwego kodu - policz, ile pracy zacznie wykonywac; latki DefaultClanFinanceModel tylko w kampanii; zmiana domyslnej przy kluczu zapisanym w Armoury.json Jeffa - grep, plik tylko odczyt). Pliki .cs CRLF, tools/sprawdz_logi.py LF. Kazda paczka z wylacznikiem (Settings.cs + "python tools/gen_mcm.py" w drzewie; "python3" to atrapa). Dekompilacja: ilspycmd; gra ${SC}\\dec32\\cs_full\\, BK ${SC}\\e4tools\\bkfull\\, reszta ${G}\\Modules\\*\\bin\\Win64_Shipping_Client. Build: cd "${dir}\\<Mod>" && dotnet build <Mod>.csproj -c Release -v q --nologo "-p:GameLibs=${LIBS}" > build.log 2>&1; echo rc=$? (kod 0 obowiazkowy, sprawdzany po kodzie wyjscia, nie przez grep). CHANGELOG na gorze (Status: DO SPRAWDZENIA - test zbiorczy etapow 5-8). Commity koncza sie "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>". Polecenie Bash dluzsze niz ok. 8 KB ucina sie - dluzsze skrypty zapisz do pliku poza repo (${SC}\\tmp) i uruchom z pliku. Nie uzywaj spawn_task ani osobnych sesji. Oszczednie - czytaj tylko potrzebne fragmenty.`
const push = (dir, br) => `Na koniec wypchnij galaz DWOMA OSOBNYMI poleceniami: "git -C ${dir} push origin ${br}" (do repo glownego) i potem "git -C ${REPO} push origin ${br}" (GitHub). Jesli zabezpieczenia odrzuca push - nie obchodz, napisz to w wyniku.`
const e8 = `ETAP 8 - projekt v2 (WIAZACY): ${PROJ8} (rozdz. 3 - opis kazdej paczki: zakres, okna latek plik:linia, klucze, linie logu, progi sprawdz_logi --grupa etap8, ryzyka, NK, koszt; rozdz. 5.1 - wspolne pliki i kolejnosc; rozdz. 6 - ODPOWIEDZI JEFFA 12.10 19:15 UTC - WIAZACE, maja pierwszenstwo przed rada projektu i zamieniaja "PO TAK" w TERAZ albo skreslaja czesc; rozdz. 8 - krytyka) + ${REPO}\\docs\\etap8\\KRYTYKA-E8.md. PODZIAL NA DRZEWA (scalenie pozniej, osobny krok): drzewo A ${DA} (w-toku/e8-a) po kolei: E8/0 (zrobione), P1, P2, X, W, A, Z, L, C, R, B, O1, O2, O3, O5, O6; drzewo C ${DC} (w-toku/e8-c, odgalezione od e8-a po E8/0) po kolei: P3, S, Q, G1, G2, T, E, K, D1, D2; drzewo ${DB} (w-toku/e8-b): E8/M (zrobione), E8/RC. E8/H (jezdziec = kon + uprzaz, odpowiedz Jeffa 7) i O4 - dopiero PO scaleniu, nie teraz. Pracuj TYLKO w swoim drzewie; cudzych drzew (takze ${DZ} - tam pracuje inny watek) nie ruszaj. Zeby scalenie bylo czyste: nie przenos i nie formatuj cudzego kodu; nowe pola Settings, bloki sprawdz_logi i wywolania w ArmouryBehavior dopisuj w miejscu wlasciwym dla swojej paczki; jesli paczka potrzebuje wspolnej rzeczy z paczki drugiego drzewa - sprawdz "git -C ${LANC} show w-toku/<druga galaz>:<plik>", czy juz istnieje, i uzyj tej samej nazwy i sygnatury; jesli jeszcze nie ma - zrob minimalna wersje i opisz w CHANGELOG ("wspolne z E8/..").`

async function tryAgent(prompt, opts) {
  let r = await agent(prompt, opts)
  if (r == null) r = await agent(prompt + '\n\n(Druga proba - poprzedni agent padl w polowie; sprawdz git status i git log, co zostawil.)', { ...opts, label: opts.label + '-2' })
  return r
}
async function gate(tag, phase) {
  const r = await agent(`Zadanie techniczne - bramka limitu konta. Nie czytaj zadnych plikow, nie rob nic poza ponizszym.
1. Zaladuj narzedzie: ToolSearch z query "select:mcp__ccd_session_mgmt__get_usage" i wywolaj mcp__ccd_session_mgmt__get_usage (bez argumentow). Jesli narzedzia nie da sie zaladowac albo wywolac - zwroc dokladnie "brak narzedzia" i skoncz.
2. Z wyniku (plan.windows): "5-hour limit" percentUsed = S, "Weekly · all models" percentUsed = T.
3. Jesli T >= 97 - zwroc dokladnie "STOP tydzien T%" i skoncz.
4. Jesli S < 82 - zwroc "ok S% / tydzien T%" i skoncz.
5. Jesli S >= 82 - czekaj na odnowienie limitu: narzedzie PowerShell, polecenie "Start-Sleep -Seconds 540" z timeout 600000, potem znowu get_usage. Powtarzaj, az S < 50 (limit sie odnowil), najwyzej 40 razy. Wtedy zwroc "czekalem N razy, teraz S% / tydzien T%".`, { label: 'limit-' + tag, phase, model: 'haiku', effort: 'low' })
  return (r || 'brak odpowiedzi').trim()
}

async function pack(base, dir, br, desc, phase, authorExtra, reviewExtra) {
  const tag = desc.split(' ')[0]
  const g = await gate(tag, phase)
  if (/STOP/.test(g)) { log(`${tag}: nie zaczeta - ${g}`); return { pk: tag, code: 'NIE ZACZETA - ' + g, rev: '-', stop: true } }
  const code = await tryAgent(`${base}\n\n${authorExtra}`, { label: 'kod-' + tag, phase })
  if (code == null) { log(`${tag}: autor padl dwa razy - watek staje`); return { pk: tag, code: 'BRAK WYNIKU - autor padl dwa razy', rev: '-', stop: true } }
  if (/^pomini/i.test(code.trim())) { log(`${tag}: ${code.trim().slice(0, 120)}`); return { pk: tag, code, rev: '-' } }
  const rev = await tryAgent(`${base}\n\n${reviewExtra}\n\n${push(dir, br)}\n\nOpis autora:\n${code}`, { label: 'rec-' + tag, phase })
  log(`${tag}: gotowe (limit: ${g})`)
  return { pk: tag, code, rev: rev == null ? 'BRAK RECENZJI - recenzent padl dwa razy' : rev }
}

// ---------- etap 8 ----------
const A = { dir: DA, br: 'w-toku/e8-a', phase: 'E8 watek A', pk: [
  'E8/P1', 'E8/P2', 'E8/X', 'E8/W', 'E8/A',
  'E8/Z',
  'E8/L (odpowiedzi Jeffa rozdz. 6: pkt 1 = NIE - nie rozszerzac trzeciej kapitana ani na wraki wedlug zabojcy, ani na zbrojownie i tabor pokonanych, reszta zasad lupu bez zmian; sprawdz w kodzie, ze lup z wrogow zabitych OSOBISCIE przez gracza idzie w 100% do gracza - jesli nie, popraw tutaj; w razie watpliwosci co do zakresu "NIE" - bez zmiany zasady i pytanie do Jeffa w CHANGELOG; pkt 6 = wyciac milicje z daru "Donate to town", sztuki do zalogi)',
  'E8/C (odpowiedz Jeffa 2: konie wedlug kategorii TAK - czesc "PO TAK" wlaczona)',
  'E8/R',
  'E8/B (odpowiedz Jeffa 5: welna 0.15 sztuki z owcy przez PAMIEC ULAMKOW - licznik osady, pelna sztuka przy >= 1, zapis i Reset; wzor N1b arm_mend_frac)',
  'E8/O1', 'E8/O2', 'E8/O3', 'E8/O5', 'E8/O6',
] }
const B = { dir: DB, br: 'w-toku/e8-b', phase: 'E8 watek B i C', pk: [ 'E8/RC' ] }
const C = { dir: DC, br: 'w-toku/e8-c', phase: 'E8 watek B i C', pk: [
  'E8/P3', 'E8/S', 'E8/Q',
  'E8/G1 (odpowiedz Jeffa 3: TAK - karawany BK w miescie co godzine, klucz wlaczony)',
  'E8/G2 (odpowiedz Jeffa 4: TAK - koniec ludzi z niczego u klanow najemnikow ROT i wyrzucania wodza, klucz wlaczony)',
  'E8/T (czesc StartKit.Substitute "kon bez uprzezy" - pytanie 7 - NIE tutaj: robi ja osobna paczka E8/H po scaleniu; reszta paczki normalnie)',
  'E8/E', 'E8/K', 'E8/D1', 'E8/D2',
] }
async function stream(S, first) {
  const base = `${env(S.dir, S.br)}\n\n${e8}`
  const out = []
  for (let i = 0; i < S.pk.length; i++) {
    const tag = S.pk[i].split(' ')[0]
    const pre = (i === 0 && first) ? first + ' ' : 'Drzewo juz istnieje (poprzednie paczki w commitach - "git log --oneline -12"). '
    const r = await pack(base, S.dir, S.br, S.pk[i], S.phase,
      `${pre}Zakoduj paczke ${S.pk[i]} wedlug projektu (rozdz. 3 ${tag}) i odpowiedzi Jeffa (rozdz. 6). Trzymaj sie kolumny "Kiedy" (TERAZ = klucz wlaczony; CIEN = kod teraz, klucz wylaczony, linia liczy skutek; PO TESCIE = nie koduj tej czesci). Gdy cala paczka nie ma teraz nic do kodu - zwroc "pominieta: powod". Najpierw sprawdz w kodzie i dekompilacji wszystko, na czym paczka sie opiera. Build kod 0, gen_mcm, progi sprawdz_logi --grupa etap8, CHANGELOG (z "Ryzyko / co sprawdzic"), commit. Zwroc krotko (max 10 linii).`,
      `RECENZJA Z POPRAWKA paczki ${S.pk[i]}: diff jej commitu w ${S.dir} ("git -C ${S.dir} log --oneline -3"). Sprawdz zgodnosc z projektem i ODPOWIEDZIAMI JEFFA (rozdz. 6), platnika i odbiorce kazdego zlota i sztuki, sygnatury i kolejnosc latek w dekompilacji (nasze i cudze), wylacznik (off = stan sprzed), NK, koszt, pulapki CLAUDE.md 7, zapis/Reset, progi sprawdz_logi (py_compile poza repo), napisy po angielsku. Prawdziwe problemy popraw sam (build kod 0, commit), nieprawdziwe odrzuc. Zwroc krotko (max 6 linii).`)
    out.push(r)
    if (r.stop) break
  }
  return out
}

// ---------- zbior-5-8 ----------
const BRZ = 'w-toku/zbior-5-8'
const E7 = [
  `E7/R2 (M, NK): okienko gracza po kazdej osadzie - projekt ${P7} rozdz. 3 E7/R2 (po R1, ktore jest juz w drzewie; bez SwitchToMenu w menu oczekiwania - CLAUDE.md 7)`,
  `E7/W4 (S): wyglad spalonej wioski i napisy nad spalonymi - projekt ${P7} rozdz. 3 E7/W4 (po R1; zmiana wizerunku RAZ przy zmianie stanu, nigdy co klatke - CLAUDE.md 7)`,
]
async function streamZ() {
  const ph = 'Zbior 5-8'
  const base = `${env(DZ, BRZ)}\n\nDrzewo ${DZ} = scalone etapy 5-7 (e6-p3 + e7-a + e7-b) + etap 5 reszta (A7, A8, BITWY, C1, C2, C4) + E7/R1; opisy paczek: "git -C ${DZ} log --oneline --first-parent w-toku/e6-p3..HEAD" i wpisy na gorze CHANGELOG.md. Rownolegle inne watki pracuja w drzewach ${SC}\\e8\\* - nie ruszaj ich.`
  const out = []
  for (const d of E7) {
    const r = await pack(base, DZ, BRZ, d, ph,
      `Zakoduj paczke ${d}. Najpierw sprawdz w kodzie i dekompilacji wszystko, na czym paczka sie opiera. Build kod 0, gen_mcm, progi w sprawdz_logi (grupa etap7), CHANGELOG (z "Ryzyko / co sprawdzic"), commit. Zwroc krotko (max 10 linii).`,
      `RECENZJA Z POPRAWKA paczki ${d}: diff jej commitu w ${DZ} ("git -C ${DZ} log --oneline -4"). Sprawdz: zgodnosc z projektem, platnik i odbiorca kazdego zlota i kazdej sztuki (brak podwojenia z MoneyLedger/TownPurse/TownFolk/VillageBook/PeopleBook/BeatenPurse A6/B5/KingdomTreasury/CrownIncome/ClanBudget, E4/5 i E7/R1), sygnatury latek w dekompilacji (nasze i cudze - BK, ROT, RBL, Diplomacy, NavalDLC), wylacznik (off = stan sprzed), NK (stary zapis wczytuje sie bez bledu), koszt, pulapki CLAUDE.md 7, zapis/Reset, progi sprawdz_logi (py_compile poza repo), napisy po angielsku, pliki poza DLL (prefaby XML - blad w XML = blad startu gry). Prawdziwe problemy popraw sam (build kod 0, commit), nieprawdziwe odrzuc. Zwroc krotko (max 6 linii).`)
    out.push(r)
    if (r.stop) return { out }
  }
  const g = await gate('przeglady', ph)
  if (/STOP/.test(g)) return { out, stop: g }
  const r1 = await tryAgent(`${base}\n\nPRZEGLAD CALOSCI 1 - ZAMKNIETA EKONOMIA (z poprawka): diff "git -C ${DZ} diff w-toku/e6-p3..HEAD" (scalenie etapu 7 + etap 5 reszta + E7 R1/R2/W4) - czytaj po plikach, nie caly naraz. Szukaj: zloto lub sztuka z niczego / w nicosc, ten sam przeplyw liczony dwa razy (E5/A7 i E7/R1/R2 - jeden platnik lupu z rabunku; A8 Diplomacy a CrownCallToWar 168 i E7/K; C1 a TownPurse A1/regulator; C2 a E4/6 Famine), sprzecznosci z zasadami Jeffa. Prawdziwe problemy popraw (build kod 0, CHANGELOG, commit), nieprawdziwe odrzuc. ${push(DZ, BRZ)} Zwroc krotko (max 8 linii).`, { label: 'przeglad-ekonomia', phase: ph })
  const r2 = await tryAgent(`${base}\n\nPRZEGLAD CALOSCI 2 - KOD I KOLIZJE (z poprawka): diff "git -C ${DZ} diff w-toku/e6-p3..HEAD" - czytaj po plikach. Szukaj: ta sama latka Harmony z dwoch paczek, kolejnosc w dobie, stan pisany dwa razy, klucze Settings/McmSettings (gen_mcm bez rozjazdu), unikalne klucze SyncData, Reset w konstruktorze, sprawdz_logi bez zdublowanych nazw progow (py_compile poza repo), pulapki CLAUDE.md 7, pliki danych i prefaby XML e7-b/W4 (poprawny XML, sciezki w SubModule). Prawdziwe problemy popraw (build kod 0, CHANGELOG, commit), nieprawdziwe odrzuc. ${push(DZ, BRZ)} Zwroc krotko (max 8 linii).\n\nWynik przegladu 1 (ekonomia):\n${r1}`, { label: 'przeglad-kod', phase: ph })
  const mg = await tryAgent(`${base}\n\nSCALENIE DECYZJI JEFFA: "git -C ${DZ} merge --no-ff w-toku/a8-gentry0" (jeden commit bc5e3ef od zbior-5-8 2b30405: nowy rod szlachty BK startuje z 0, klucz GentryStartFromTown = false). Konflikty: CHANGELOG.md - zachowaj WSZYSTKIE wpisy; Settings.cs - suma pol; McmSettings.cs - NIE scalaj recznie, po scaleniu Settings uruchom "python tools/gen_mcm.py"; reszta - nic z zadnej strony nie moze zginac. Sprawdz mechanicznie, ze kazda linia dodana w bc5e3ef (poza CHANGELOG/McmSettings) jest w wyniku. Build Armoury kod 0, commit scalenia. ${push(DZ, BRZ)} Zwroc krotko (max 6 linii): konflikty, HEAD, liczba ustawien gen_mcm.`, { label: 'scalenie-a8-gentry0', phase: ph })
  log('zbior-5-8: R2, W4, przeglady i a8-gentry0 gotowe')
  return { out, r1, r2, mg }
}

// ---------- bieg ----------
const rev0 = tryAgent(`${env(DA, A.br)}\n\n${e8}\n\nRECENZJA Z POPRAWKA paczki E8/0 (profil doby w logu): commit 8a8df38 w ${DA} ("git -C ${DA} show --stat HEAD"; opis autora = tresc commitu i wpis na gorze CHANGELOG.md; poprzednia recenzja zostala przerwana przed zmianami). Sprawdz zgodnosc z projektem (rozdz. 3 E8/0): stopery bez alokacji, suma "nasze" rozlaczna z Cost174 (nic liczone dwa razy), koszt samego profilu <= 1 ms/dobe, opakowanie wywolan Daily w OnDailyTick nie zmienia kolejnosci ani nie gubi zadnego wywolania (porownaj liste wywolan przed i po - mechanicznie), sluchacze godzinowi i zdarzen, [ThreadStatic] wolajacy w SupplyDemand, Reset miedzy kampaniami, wylacznik Stage8ProfileLog (off = stan sprzed), czesc CrashScribe (FirstChance - uwaga: e8-b E8/M rusza te same linie SubModuleMain/Config.cs/settings.xml - opisz, co bedzie konfliktem przy scaleniu), progi sprawdz_logi --grupa etap8 (py_compile poza repo), pulapki CLAUDE.md 7. Prawdziwe problemy popraw sam (build kod 0 Armoury i CrashScribe, commit), nieprawdziwe odrzuc. ${push(DA, A.br)} Zwroc krotko (max 8 linii).`, { label: 'rec-E8/0', phase: 'E8 watek A' })

const res = await parallel([
  () => streamZ(),
  async () => { const r0 = await rev0; log('E8/0: recenzja gotowa'); const a = await stream(A); return { r0, a } },
  async () => {
    const b = await stream(B)
    await rev0
    const c = await stream(C, `Drzewa C jeszcze nie ma - utworz je: "git -C ${LANC} worktree add ${DC} -b w-toku/e8-c w-toku/e8-a" (jesli galaz albo drzewo juz istnieje - zglos i uzyj; w galezi moga juz byc pierwsze paczki drzewa A - to w porzadku).`)
    return { b, c }
  },
])
return { z: res[0], a: res[1], bc: res[2] }
