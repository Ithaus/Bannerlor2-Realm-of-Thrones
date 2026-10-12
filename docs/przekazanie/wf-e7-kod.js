export const meta = {
  name: 'e7-kod-dwa-strumienie',
  description: 'Etap 7 kod (projekt v2): strumien A (E7/0, O1, T, C, K, O2) i strumien B (L, B, W1, W2, W3, W5, D0) rownolegle w dwoch drzewach od w-toku/e5-wsie; autor + recenzent z poprawka po kazdej paczce, na koncu przeglad kolizji strumienia i DLL; R1/R2/W4 czekaja na E5/A7, D1/D2 na test i Jeffa',
  phases: [ { title: 'Strumien A' }, { title: 'Strumien B' }, { title: 'Przeglad' } ],
}
const S3 = 'C:\\Users\\GAME\\AppData\\Local\\Temp\\claude\\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\\3cf3e0ac-5529-4b68-a794-0edec69cfda7\\scratchpad'
const LANC = S3 + '\\lancuch'
const LIBS = LANC + '\\libs'
const SC = 'C:\\Users\\GAME\\AppData\\Local\\Temp\\claude\\C--Users-GAME-Bannerlor2-Realm-of-Thrones\\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\\scratchpad'
const G = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord'
const REPO = 'C:\\Users\\GAME\\Bannerlor2-Realm-of-Thrones'
const PROJ = REPO + '\\docs\\etap7\\PROJEKT-ETAP7-MAPA-DROGI-WIOSKI-2026-10-12.md'
const mkBase = (dir, br) => `Mody Bannerlorda 1.4.8 (Realm of Thrones), mody Armoury (i GrandTourney/CrashScribe, jesli paczka ich dotyczy - te same drzewo). ETAP 7 - projekt v2 (WIAZACY): ${PROJ} (rozdz. 3 - opis kazdej paczki: zakres, okna latek plik:linia, klucze, linie logu, test, NK, koszt; rozdz. 8 - krytyka) + ${REPO}\\docs\\etap7\\KRYTYKA-E7.md. Decyzje Jeffa (${REPO}\\docs\\STAN-PRAC.md): tempo marszu ZOSTAJE (7.2/7.3 odrzucone) - zadna paczka nie zmienia predkosci poza dokladnym odwzorowaniem kar gry (E7/T); premia za droge D2 NIE teraz; zamknieta ekonomia, "nie robimy sztucznych praw", drobne bledy to etap 8 (ZADNYCH poprawek "przy okazji" - zauwazone wpisz w CHANGELOG paczki jako "poza zakresem"), Jeff chce szybszej gry (koszt na dobe/klatke - zadnych petli co klatke, UI tylko przy otwarciu), Jeff gra dopiero nowa kampanie (NK dozwolone, stary zapis ma sie wczytac bez bledu), napisy w grze po angielsku. Pulapki CLAUDE.md 7 (SwitchToMenu w menu oczekiwania - CTD; wizerunki na mapie co klatke - CTD; globalne wylaczniki po jednym wyjatku; latki DefaultClanFinanceModel tylko w kampanii). Drzewo ${dir}: przy pierwszej paczce utworz "git -C ${LANC} worktree add ${dir} -b ${br} w-toku/e5-wsie" (jesli istnieje - zglos i uzyj). Pliki .cs CRLF, sprawdz_logi.py LF (progi do --grupa etap7 - utworz grupe, jesli jej nie ma, wzorem etap5/etap6). Komentarze po polsku bez polskich znakow. Kazda paczka z wylacznikiem (Settings + "python tools/gen_mcm.py"). Gry nie uruchamiaj. Dekompilacja: ilspycmd; gra ${SC}\\dec32\\cs_full\\, BK ${SC}\\e4tools\\bkfull\\, reszta ${G}\\Modules\\*\\bin\\Win64_Shipping_Client, ROT XML ${G}\\Modules\\ROT-*\\ModuleData (tylko odczyt). Ustawienia/zapisy Jeffa tylko odczyt. Build: cd "${dir}\\Armoury" && dotnet build Armoury.csproj -c Release -v q --nologo "-p:GameLibs=${LIBS}" > build.log 2>&1; echo rc=$? (inne mody analogicznie w swoich folderach). CHANGELOG (Status: DO SPRAWDZENIA - test zbiorczy etapow 5-8). Commity koncza sie "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>". Oszczednie - czytaj tylko potrzebne fragmenty. Nie uzywaj spawn_task.`
const A = { dir: SC + '\\e7\\a', br: 'w-toku/e7-a', phase: 'Strumien A', pk: [
  'E7/0 (S): pomiar etapu 7, tylko log (projekt rozdz. 3 E7/0)',
  'E7/O1 (S, v2 przebudowana): sen do switu - spiacy nie dostaje rozkazow (projekt rozdz. 3 E7/O1; po E7/0; BK budzi karawany, Wayfinder i BK na SetMove* - sprawdz zachowanie po prefiksie false w tej wersji Harmony)',
  'E7/T (S): kary terenu dokladnie jak gra (projekt rozdz. 3 E7/T, PLAN 7.4)',
  'E7/C (S): jedna skala czasu (projekt rozdz. 3 E7/C, PLAN 7.1)',
  'E7/K (S): turnieje, sojusze i rozejm w roku 364 (projekt rozdz. 3 E7/K, PLAN 7.7) - pliku ustawien Diplomacy Jeffa NIE zmieniaj (to jego decyzja - pytanie 3); kod ma dzialac z obecnym plikiem',
  'E7/O2 (S, v2): namioty i ogniska przy napadzie na oboz (projekt rozdz. 3 E7/O2; CampBattlePropsEnabled - Jeff 09.10 "wlaczyc"; bez fizyki, nie pod nogami gracza)',
] }
const B = { dir: SC + '\\e7\\b', br: 'w-toku/e7-b', phase: 'Strumien B', pk: [
  'E7/L (M, Python): dane lore 2 446 wiosek (projekt rozdz. 3 E7/L, PLAN 7.8)',
  'E7/B (S): licznik przejsc przez Bliznaki (projekt rozdz. 3 E7/B, PLAN 7.12) - sam licznik, bez myta',
  'E7/W1 (S, v2): komunikat "plonie Twoja wioska" i pamiec wojny (projekt rozdz. 3 E7/W1; "burned" tylko przy stanie spalonym w ksiedze E4/5, przy splądrowaniu "plundered")',
  'E7/W2 (S-M): dymek wioski - herb, rycerz, wiara, historia (projekt rozdz. 3 E7/W2; po L i W1; UIExtenderEx ostroznie - ForgeView tez na nim, nie dotykac cudzych mixinow)',
  'E7/W3 (S): menu okregu "Look over the district" (projekt rozdz. 3 E7/W3; po W2)',
  'E7/W5 (S): proporczyk pana nad wioskami (projekt rozdz. 3 E7/W5; raz przy zmianie stanu, nigdy co klatke)',
  'E7/D0 (S): przeniesienie proby drog z galezi w-toku/p2-drogi-proba (95f05be) do tego drzewa jako osobny wylacznik DOMYSLNIE WYLACZONY - tylko do zdjec w tescie zbiorczym (projekt rozdz. 3 E7/D0)',
] }
async function stream(S) {
  const base = mkBase(S.dir, S.br)
  const out = []
  for (let i = 0; i < S.pk.length; i++) {
    const tag = S.pk[i].split(' ')[0]
    const code = await agent(`${base}\n\n${i === 0 ? '' : 'Drzewo juz istnieje (poprzednie paczki w commitach). '}Zakoduj paczke ${S.pk[i]}. Najpierw sprawdz w kodzie i dekompilacji wszystko, na czym paczka sie opiera (okna latek z projektu moga byc nieaktualne - zweryfikuj). Build kod 0, gen_mcm, CHANGELOG (z "Ryzyko / co sprawdzic" - kontrola calosci: regresje, kolizje, spojnosc, cudzy kod), commit. Zwroc krotko (max 10 linii).`, { label: 'kod-' + tag, phase: S.phase })
    const rev = await agent(`${base}\n\nRECENZJA Z POPRAWKA paczki ${S.pk[i]}: diff jej commitu w ${S.dir} ("git -C ${S.dir} log --oneline -3"). Sprawdz: sygnatury i kolejnosc latek w dekompilacji (nasze i cudze - BK, ROT, RBL, Diplomacy, UIExtenderEx), czy nic nie zmienia tempa marszu wbrew decyzji, zamknieta ekonomia, wylacznik (off = stan sprzed paczki), NK (stary zapis wczytuje sie bez bledu), koszt na dobe i na klatke, pulapki CLAUDE.md 7, zapis/odczyt stanu i czyszczenie miedzy kampaniami, progi w sprawdz_logi (py_compile poza repo), napisy po angielsku. Prawdziwe problemy popraw sam (build, commit), nieprawdziwe odrzuc. Zwroc krotko (max 6 linii).\n\nOpis autora:\n${code}`, { label: 'rec-' + tag, phase: S.phase })
    out.push({ pk: tag, code, rev })
  }
  const all = await agent(`${base}\n\nPRZEGLAD CALOSCI ${S.br} (zasada 0 - kolizje miedzy paczkami strumienia): diff "git -C ${S.dir} diff w-toku/e5-wsie..HEAD". Szukaj kolizji MIEDZY paczkami (ta sama latka/stan pisany dwa razy, kolejnosc w dobie, klucze Settings/McmSettings bez rozjazdu po gen_mcm, unikalne klucze SyncData, sprawdz_logi bez zdublowanych nazw progow). Prawdziwe problemy popraw (build, commit). Na koniec build i skopiuj ${S.dir}\\Armoury\\bin\\Release\\Armoury.dll do ${SC}\\test\\Armoury-${S.br.split('/')[1]}.dll (podaj md5; inne zmienione mody tez, z md5). Zwroc krotko (max 8 linii).\n\nPaczki:\n${out.map(o => o.pk + ': ' + o.code.slice(0, 500)).join('\n---\n')}`, { label: 'przeglad-' + S.br.split('/')[1], phase: 'Przeglad' })
  return { out, all }
}
const [ra, rb] = await parallel([() => stream(A), () => stream(B)])
return { a: ra, b: rb }
