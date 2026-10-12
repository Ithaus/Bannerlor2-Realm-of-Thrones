export const meta = {
  name: 'e8-kod-dwa-watki',
  description: 'Etap 8 kod (projekt v2 + odpowiedzi Jeffa 12.10 19:15): watek A Armoury po kolei (E8/0, P1-P3, S, X, W, A, Z, L, C, R, K, D1, D2, B, H jezdziec = kon + uprzaz, G1, G2, Q, T, E, O1, O2, O3, O5, O6) w w-toku/e8-a, watek B CrashScribe/RC/ForgeView (M, RC) w w-toku/e8-b - oba od w-toku/zbior-5-8; autor + recenzent po kazdej paczce, przeglad kolizji na koncu',
  phases: [ { title: 'Watek A' }, { title: 'Watek B' }, { title: 'Przeglad' } ],
}
const S3 = 'C:\\Users\\GAME\\AppData\\Local\\Temp\\claude\\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\\3cf3e0ac-5529-4b68-a794-0edec69cfda7\\scratchpad'
const LANC = S3 + '\\lancuch'
const LIBS = LANC + '\\libs'
const SC = 'C:\\Users\\GAME\\AppData\\Local\\Temp\\claude\\C--Users-GAME-Bannerlor2-Realm-of-Thrones\\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\\scratchpad'
const G = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord'
const REPO = 'C:\\Users\\GAME\\Bannerlor2-Realm-of-Thrones'
const PROJ = REPO + '\\docs\\etap8\\PROJEKT-ETAP8-DROBNE-BLEDY-OPTYMALIZACJA-2026-10-12.md'
const mk = (dir, br) => `Mody Bannerlorda 1.4.8 (Realm of Thrones), mody Armoury / CrashScribe / RealisticCaptivity / GrandTourney / ForgeView (to samo repo). ETAP 8 - projekt v2 (WIAZACY): ${PROJ} (rozdz. 3 - opis kazdej paczki: zakres, okna latek plik:linia, klucze, linie logu, progi sprawdz_logi --grupa etap8, ryzyka, NK, koszt; rozdz. 6 - ODPOWIEDZI JEFFA 12.10 19:15 UTC - WIAZACE, maja pierwszenstwo przed rada projektu; rozdz. 8 - krytyka) + ${REPO}\\docs\\etap8\\KRYTYKA-E8.md. Zasady Jeffa: zamknieta ekonomia (nic z niczego, nic w nicosc), bez sztucznych doplat, ogolny poziom cen zostaje, kazda zmiana = kontrola calosci (CLAUDE.md 8.0), tylko zakres paczki, Jeff gra dopiero nowa kampanie (NK dozwolone, stary zapis wczytuje sie bez bledu), napisy po angielsku, komentarze po polsku bez polskich znakow. Pulapki CLAUDE.md 7 (SwitchToMenu w menu oczekiwania; wizerunki co klatke; globalne wylaczniki po jednym wyjatku - licz potkniecia; ozywianie martwego kodu - policz, ile pracy zacznie wykonywac; latki DefaultClanFinanceModel tylko w kampanii; domyslna przy kluczu w Armoury.json Jeffa - grep, plik tylko odczyt). Drzewo ${dir}: przy pierwszej paczce utworz "git -C ${LANC} worktree add ${dir} -b ${br} w-toku/zbior-5-8" (jesli istnieje - zglos i uzyj; w zbior-5-8 rownolegle pracuje inny workflow - NIE ruszaj drzewa ${SC}\\zb\\58). Pliki .cs CRLF, sprawdz_logi.py LF. Kazda paczka z wylacznikiem (Settings + "python tools/gen_mcm.py"). Gry nie uruchamiaj. Dekompilacja: ilspycmd; gra ${SC}\\dec32\\cs_full\\, BK ${SC}\\e4tools\\bkfull\\, reszta ${G}\\Modules\\*\\bin\\Win64_Shipping_Client. Build: cd "${dir}\\<Mod>" && dotnet build <Mod>.csproj -c Release -v q --nologo "-p:GameLibs=${LIBS}" > build.log 2>&1; echo rc=$?. CHANGELOG (Status: DO SPRAWDZENIA - test zbiorczy etapow 5-8). Commity koncza sie "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>". Oszczednie. Nie uzywaj spawn_task.`
const A = { dir: SC + '\\e8\\a', br: 'w-toku/e8-a', phase: 'Watek A', pk: [
  'E8/0', 'E8/P1', 'E8/P2', 'E8/P3', 'E8/S', 'E8/X', 'E8/W', 'E8/A', 'E8/Z', 'E8/L', 'E8/C', 'E8/R', 'E8/K', 'E8/D1', 'E8/D2', 'E8/B',
  'E8/H (NOWA - odpowiedz Jeffa 7: jezdziec = kon + UPRZAZ; sprawdz dzisiejsza regule awansu na jezdzca i dosadzania - Stables, Remount, AiMount, CavalryTemplate K3, zbrojownie zalog, DTE, gracz; jesli sam kon wystarcza - wymog uprzezy HorseHarness, jedna regula dla AI i gracza, uprzaz z prawdziwej produkcji; linia logu: jezdzcy bez uprzezy, skutek dla udzialu jazdy; klucz RiderNeedsHarness)',
  'E8/G1', 'E8/G2', 'E8/Q', 'E8/T', 'E8/E', 'E8/O1', 'E8/O2', 'E8/O3', 'E8/O5', 'E8/O6',
] }
const B = { dir: SC + '\\e8\\b', br: 'w-toku/e8-b', phase: 'Watek B', pk: [ 'E8/M', 'E8/RC' ] }
async function stream(S) {
  const base = mk(S.dir, S.br)
  const out = []
  for (let i = 0; i < S.pk.length; i++) {
    const tag = S.pk[i].split(' ')[0]
    const code = await agent(`${base}\n\n${i === 0 ? '' : 'Drzewo juz istnieje (poprzednie paczki w commitach). '}Zakoduj paczke ${S.pk[i]} wedlug projektu (rozdz. 3 ${tag}) i odpowiedzi Jeffa (rozdz. 6). Jesli projekt mowi, ze paczka czeka na cos, czego nie ma (np. tylko po tescie - O4) - nie koduj, zwroc "pominieta: powod". Najpierw sprawdz w kodzie i dekompilacji wszystko, na czym paczka sie opiera. Build kod 0, gen_mcm, progi sprawdz_logi --grupa etap8, CHANGELOG (z "Ryzyko / co sprawdzic"), commit. Zwroc krotko (max 10 linii).`, { label: 'kod-' + tag, phase: S.phase })
    if (/^pomini/i.test((code || '').trim())) { out.push({ pk: tag, code, rev: '-' }); continue }
    const rev = await agent(`${base}\n\nRECENZJA Z POPRAWKA paczki ${S.pk[i]}: diff jej commitu w ${S.dir} ("git -C ${S.dir} log --oneline -3"). Sprawdz zgodnosc z projektem i ODPOWIEDZIAMI JEFFA (rozdz. 6), platnika i odbiorce kazdego zlota i sztuki, sygnatury i kolejnosc latek w dekompilacji (nasze i cudze), wylacznik (off = stan sprzed), NK, koszt, pulapki CLAUDE.md 7, zapis/Reset, progi sprawdz_logi (py_compile poza repo), napisy po angielsku. Prawdziwe problemy popraw sam (build, commit), nieprawdziwe odrzuc. Zwroc krotko (max 6 linii).\n\nOpis autora:\n${code}`, { label: 'rec-' + tag, phase: S.phase })
    out.push({ pk: tag, code, rev })
  }
  return out
}
const [ra, rb] = await parallel([() => stream(A), () => stream(B)])
const summary = [...(ra || []), ...(rb || [])].map(o => o.pk + ': ' + (o.code || '').slice(0, 300)).join('\n---\n')
const all = await agent(`${mk(A.dir, A.br)}\n\nPRZEGLAD KOLIZJI ETAPU 8 (z poprawka): drzewa ${A.dir} (w-toku/e8-a) i ${B.dir} (w-toku/e8-b), diff kazdego od w-toku/zbior-5-8. Szukaj kolizji miedzy paczkami (ta sama latka/stan dwa razy, kolejnosc w dobie, klucze Settings/McmSettings bez rozjazdu po gen_mcm, unikalne klucze SyncData, Reset, zdublowane progi sprawdz_logi) i miedzy watkami (te same pliki - przygotuj opis konfliktow do scalenia). Prawdziwe problemy popraw (build, commit). Na koniec build i skopiuj Armoury.dll z ${A.dir} do ${SC}\\test\\Armoury-e8a.dll oraz zmienione mody z ${B.dir} do ${SC}\\test\\e8b\\ (md5). Zwroc krotko (max 8 linii).\n\nPaczki:\n${summary}`, { label: 'przeglad-e8', phase: 'Przeglad' })
return { a: ra, b: rb, all }
