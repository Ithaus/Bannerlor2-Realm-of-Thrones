export const meta = {
  name: 'lup-zdobytego-miasta',
  description: 'Jak dzis dziala lup po zdobyciu miasta (gra, BannerKings, ROT, nasz mod, pomiar z biegu rocznego) i propozycja: lupi sie prawdziwe bogactwo miasta - bez kodu',
  phases: [
    { title: 'Rozpoznanie', detail: 'kod gry i modow, kod Armoury, pomiar z biegu rocznego' },
    { title: 'Podwazenie', detail: 'niezalezne sprawdzenie ustalen' },
    { title: 'Propozycja', detail: 'jedna regula w obiegu zamknietym, rozdzial prostym jezykiem i pytania o kierunek' },
  ],
}

const REPO = String.raw`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`
const SC = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`
const INT = SC + String.raw`\zb\int`
const SP = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad`
const LOGDIR = String.raw`D:\Backup-Bannerlord\logs\2026-10-13-noc34-35`
const OUT = SP + String.raw`\lup-miast`

const WSP = String.raw`
Pracujesz przy modach do gry Mount & Blade II Bannerlord (Realm of Thrones + BannerKings + nasze mody Armoury i CrashScribe). Wlasciciel: Jeff - gracz, nie programista.
NAJPIERW przeczytaj zasady pracy: ${REPO}\CLAUDE.md (obowiazuja cie: zasada 0 - kazda zmiana to sprawdzenie calego systemu; obieg zamkniety - nic z niczego i nic w nicosc, zadnych sztucznych doplat; prosty jezyk; napisy w grze po angielsku).
TO JEST PRACA TYLKO DO ODCZYTU I PROJEKTU: nie uruchamiaj gry, nie buduj, nie zmieniaj kodu, ustawien ani zapisow, zadnych commitow. Pisz wylacznie do katalogu ${OUT} (istnieje), chyba ze polecenie mowi inaczej.
PYTANIE JEFFA (13.10): "A co, jak miasto zostanie zdobyte - jak dziala pladrowanie? Bo powinno dac sie pladrowac to, co jest prawdziwym bogactwem miasta. I jak to dziala teraz?"
ZRODLA: kod gry (dekompilacja) ${SC}\dec32\cs_full ; BannerKings ${SC}\e4tools\bkfull ; Realm of Thrones ${SC}\dec37\rot (inne dekompilacje modow szukaj w ${SC}) ; nasz kod ${INT}\Armoury\src (galaz w-toku/zbior-e8) ; dokumenty ${REPO}\docs (STAN-PRAC.md, CHANGELOG, audyty obiegu zlota - szukaj "O10", "lup z oblezen", "SiegeAftermath", "ujscia").
Bieg roczny do pomiaru (364 doby, nowa kampania, sam swiat AI): log ${LOGDIR}\Armoury-2026-10-13_06-24-10.log (119 MB - nie czytaj w calosci, uzywaj Grep i python -I), log CrashScribe ${LOGDIR}\session-2026-10-13_06-24-09.log, tabele ${LOGDIR}\Logs\2026-10-13_06-24-10\ (bitwy.log, kasy-miast.csv, kiesa-ludu.csv, ludnosc-bk.csv, balans-krolestw.csv, finanse.log ...).
Kazde twierdzenie o kodzie: plik i numer linii. Kazda liczba: zrodlo i znacznik [P] pomiar / [S] szacunek. Czego nie wiesz - napisz "nie wiem". Pliki po polsku BEZ polskich znakow diakrytycznych.
`

const R_SCHEMA = {
  type: 'object',
  properties: {
    streszczenie: { type: 'string' },
    ustalenia: { type: 'array', items: { type: 'object', properties: { twierdzenie: { type: 'string' }, dowod: { type: 'string' } }, required: ['twierdzenie', 'dowod'] } },
    plik: { type: 'string' },
  },
  required: ['streszczenie', 'ustalenia', 'plik'],
}

phase('Rozpoznanie')
const READERS = [
  { key: 'gra-i-mody', p: String.raw`
TWOJE ZADANIE: jak dziala zdobycie miasta / zamku w SILNIKU i w cudzych modach - krok po kroku, co do zlota i towaru.
1. Gra: SiegeAftermathCampaignBehavior, SiegeAftermathAction, model lupow po oblezeniu (DefaultSiegeAftermathModel albo podobny), ChangeOwnerOfSettlementAction, lup z bitwy o mury (garnizon, milicja - PlayerEncounter / MapEvent / LootCollector), jency. Trzy wybory zdobywcy (Devastate / Pillage / Show Mercy): co kazdy robi z dobrobytem, lojalnoscia, bezpieczenstwem, ludnoscia, budowami w toku, zapasem zywnosci; ILE zlota dostaje zdobywca i jego armia, SKAD ono pochodzi (czy jest zdejmowane z kogokolwiek - Town.Gold czyli kasa kupcow, kiesa pana, skarbiec - czy powstaje z niczego wedlug wzoru od dobrobytu); jak wybiera AI; co widzi gracz (okno, napisy).
2. Co przy zmianie pana dzieje sie z: towarem na polkach targu (ItemRoster miasta), kasa kupcow (Town.Gold), warsztatami i ich wlascicielami, karawanami w miescie, garnizonem, budowami, notablami, jencami w lochu, skladem / stash poprzedniego pana.
3. BannerKings: co dodaje przy zdobyciu (BKSettlementBehavior, BKGentryBehavior, BKReligionsBehavior i inne sluchajace OnSiegeAftermathApplied / OnSettlementOwnerChanged): ludnosc i klasy, niewolnicy, tytuly, stabilnosc, zloto.
4. Realm of Thrones i BKROTPatch: czy cos zmieniaja w tym miejscu.
5. Napad na wies (raid) dla porownania - w dwoch zdaniach, co silnik robi z lupem wsi.
Zapisz ${OUT}\rozpoznanie-gra-i-mody.md.` },
  { key: 'armoury', p: String.raw`
TWOJE ZADANIE: co przy zdobyciu miasta / zamku robi dzis NASZ mod Armoury i jakie jest w nim "prawdziwe bogactwo miasta".
1. Wszystko, co slucha zdobycia albo zmiany pana: ArmouryBehavior.cs (OnSettlementOwnerChangedEvent ok. linii 727-728 i inne zdarzenia oblezen), CirculationWindows.cs (okno O10 "lup z oblezen" ok. linii 619 i 1822 - co dokladnie robi z lupem silnika: skad bierze zloto, komu daje, czy zamyka obieg), BattlePlunder.cs, CasualtyLoot.cs, LootPrices.cs, SpoilsCompany.cs, SpoilsSeal.cs, UniqueSpoils.cs, BesiegedPay.cs, Devastation.cs (czy dotyczy tylko wsi), DistrictRaid, oraz latki na SiegeAftermath* jesli sa. Opisz krok po kroku, co sie dzieje z kazda moneta i kazda sztuka towaru w chwili zdobycia - dzis.
2. SPIS BOGACTWA MIASTA w naszym modelu - kazdy zbiornik osobno: kiesy ludu (klasy wyrobnicy / rzemieslnicy / rodziny kupieckie; zapas i lokata ponad zapas), kasa kupcow (Town.Gold), kasa gminy / akcyza, towar na polkach targu, spichlerz i zapasy zywnosci, sklady warsztatow, zbrojownia i zapas zalogi, skarbiec / kiesa pana i korony trzymana w miescie (jesli jest), notable, karawany stojace w miescie, bank (jesli ma oddzial), ludzie (ksiega ludzi - jency, niewolnicy), unikaty. Dla kazdego: gdzie w kodzie, co sie z nim dzieje przy zmianie pana DZIS (zostaje? przechodzi? znika?), i ile to jest zlota w skali swiata na koniec biegu rocznego (z logu / csv; np. kiesy ludu miast razem ok. 36 mln, kasy kupcow - sprawdz).
3. Dziury obiegu w tym miejscu dzis: czy cos powstaje z niczego (lup silnika?) albo znika (spalony dobrobyt, wyczyszczone budowy) - co mowia nasze kontrole bilansu w dniach zdobyc.
4. Co juz zdecydowano w docs o lupie miast (STAN-PRAC, projekty etapow 7-8, "Spoils", okienko rabunku R2 dla wsi) - zeby propozycja nie dublowala i nie lamala wczesniejszych decyzji Jeffa.
Zapisz ${OUT}\rozpoznanie-armoury.md.` },
  { key: 'pomiar', p: String.raw`
TWOJE ZADANIE: POMIAR z biegu rocznego - co naprawde stalo sie przy kazdym zdobyciu miasta i zamku.
1. Lista wszystkich zdobyc (miasta i zamki osobno): doba, osada, kto zdobyl (rod, krolestwo, armia - ilu ludzi), od kogo, czy szturm czy glod / poddanie, jaki wybor po oblezeniu (jesli widac). Zrodla: bitwy.log, balans-krolestw.csv (szturmy_zdobyte), linie logu o zmianie pana i o lupie z oblezen (okno "oblezenie" w liniach obiegu), KRONIKA w logu CrashScribe.
2. Dla kazdego zdobytego MIASTA (i 10 przykladowych zamkow): stan 3 doby przed i 3, 14, 30 dob po - kasa kupcow (kasy-miast.csv), kiesy ludu (kiesa-ludu.csv), ludnosc i dobrobyt (ludnosc-bk.csv), zapas zywnosci, liczba sztuk na polkach jesli jest. Ile zlota dostal zdobywca jako lup (linia okna oblezenia / finanse.log / budzet-rodow.csv zdobywcy w tej dobie - kolumny lupow) i czy ta kwota zostala komus zdjeta.
3. Suma roczna: ile zlota lupu z oblezen wyplacono, skad pochodzilo, ile "bogactwa miasta" lezalo w zdobytych miastach w chwili zdobycia (kiesy + kasa + wartosc towaru, jesli da sie policzyc) - czyli ile daloby sie zlupic naprawde.
4. Czy miasto po zdobyciu biednieje naprawde (ceny, glod, ludnosc), czy tylko zmienia flage.
Skrypty w ${OUT}\skrypty\. Zapisz ${OUT}\pomiar-zdobycia.md.` },
]
const recon = (await parallel(READERS.map(r => () => agent(WSP + r.p + '\nZwroc strukture (plik = pelna sciezka).', { label: 'rozpoznanie:' + r.key, phase: 'Rozpoznanie', schema: R_SCHEMA })))).filter(Boolean)
log('rozpoznan: ' + recon.length + ' z 3')

phase('Podwazenie')
const V_SCHEMA = {
  type: 'object',
  properties: {
    obalone: { type: 'array', items: { type: 'object', properties: { twierdzenie: { type: 'string' }, dlaczego: { type: 'string' }, poprawka: { type: 'string' } }, required: ['twierdzenie', 'dlaczego', 'poprawka'] } },
    potwierdzone_kluczowe: { type: 'array', items: { type: 'string' } },
    braki: { type: 'array', items: { type: 'string' } },
    plik: { type: 'string' },
  },
  required: ['obalone', 'potwierdzone_kluczowe', 'braki', 'plik'],
}
const ver = await agent(String.raw`${WSP}
JESTES WERYFIKATOREM. Trzy pliki rozpoznania: ${OUT}\rozpoznanie-gra-i-mody.md, ${OUT}\rozpoznanie-armoury.md, ${OUT}\pomiar-zdobycia.md. Nie ufaj im.
Sprawdz w kodzie i danych SAM kazde twierdzenie, od ktorego zalezy odpowiedz na pytanie Jeffa: (1) skad pochodzi zloto lupu po oblezeniu i komu jest zdejmowane (czy z niczego), (2) co sie dzieje z kasa kupcow, kiesami ludu i towarem przy zmianie pana, (3) co robi nasze okno "lup z oblezen", (4) co robia trzy wybory zdobywcy i jak wybiera AI, (5) co dodaje BannerKings, (6) 5 najwazniejszych liczb pomiaru (policz wlasnym skryptem). Szukaj tez rzeczy pominietych: innej latki na te same metody (Harmony w cudzych modach), przypadku zamku, przypadku gracza-obroncy, miasta przejetego bez szturmu (bunt, traktat, wymiana), miasta zbuntowanego.
Streszczenia: ${JSON.stringify(recon.map(r => r.streszczenie))}
Zapisz ${OUT}\weryfikacja.md i zwroc strukture.`, { label: 'podwazenie', phase: 'Podwazenie', schema: V_SCHEMA })

phase('Propozycja')
const P_SCHEMA = {
  type: 'object',
  properties: {
    rozdzial0: { type: 'string', description: 'pelny tekst rozdzialu 0 dla Jeffa: jak jest dzis + propozycja, prostym jezykiem' },
    pytania: { type: 'array', items: { type: 'string' } },
    plik: { type: 'string' },
  },
  required: ['rozdzial0', 'pytania', 'plik'],
}
const prop = await agent(String.raw`${WSP}
JESTES AUTOREM ODPOWIEDZI I PROPOZYCJI (bez kodu). Wyjatek od zasady "tylko OUT": zapisz JEDEN plik w repo: ${REPO}\docs\lup-miast\LUP-ZDOBYTEGO-MIASTA-2026-10-13.md (utworz katalog; bez commita).
Materialy: ${OUT}\rozpoznanie-gra-i-mody.md, ${OUT}\rozpoznanie-armoury.md, ${OUT}\pomiar-zdobycia.md, ${OUT}\weryfikacja.md (gdzie weryfikator cos obalil - obowiazuje jego wersja: ${JSON.stringify(ver ? ver.obalone : 'BRAK WERYFIKACJI - sprawdz sam kluczowe twierdzenia')}). Przeczytaj tez projekt obiegu kiesy ludu ${REPO}\docs\kiesa-ludu\PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md (Jeff zgodzil sie 13.10 na danine wojenna z oszczednosci ludu, budowe miasta przez lud i zbytki) - lup ma do niego pasowac: to te same zbiorniki.
UKLAD PLIKU:
0. "Dla Jeffa - prostym jezykiem" (najwyzej 45 linii): CZESC A - jak to dziala DZIS, krok po kroku: co dostaje zdobywca, skad to zloto jest, co traci miasto, co robia trzy wybory, co z kupcami, ludem, towarem; uczciwie: co jest prawdziwe, a co "z niczego" albo "tylko flaga". CZESC B - propozycja: JEDNA regula "lupi sie to, co w miescie naprawde lezy": z ktorych zbiornikow i ile przy kazdym z trzech wyborow (lagodnosc / grabiez / spustoszenie), kto dostaje (wodz, rody w armii wedlug ludzi, zolnierze - a przez nich miasta, w ktorych wydaja), co widzi gracz (okno z prawdziwymi kwotami - napisy po angielsku), jak wybiera AI (z biezacej potrzeby, np. zalegly zold), co potem z miastem (biedniejsze naprawde: kupcy bez kasy, lud bez oszczednosci, ceny, lojalnosc, czas odbudowy), ile to zlota w skali swiata (z pomiaru). CZESC C - czego nie wiemy.
1. Jak jest dzis - z dowodami (plik:linia, liczby z biegu). 2. Spis bogactwa miasta i co sie z nim dzieje przy zdobyciu dzis. 3. Regula - szczegoly, parametry z rachunkiem (parametry wybierasz ty - Jeffa pytamy tylko o kierunek), dowod zamknietego obiegu (tabela: platnik / odbiorca / towar). 4. Sto procent przypadkow (miasto, zamek, AI-AI, gracz zdobywa, gracz broni, miasto przejete bez szturmu, bunt, miasto juz zlupione miesiac temu, wies - odniesienie do istniejacego rabunku wsi, obrona odparta). 5. Zgodnosc z innymi regulami (danina wojenna, kiesa ludu, kasy miast, BannerKings: ludnosc i stabilnosc, okno "lup z oblezen", lupy z bitwy). 6. Paczki wdrozenia (tor S / M / L wedlug ryzyka), progi testu. 7. Ryzyka. 8. Pytania do Jeffa - TYLKO kierunek, najwyzej 5, kazde z rada.
Zwroc: rozdzial0 (pelny tekst), pytania, plik.`, { label: 'propozycja', phase: 'Propozycja', schema: P_SCHEMA })

return { prop, obalone: ver ? ver.obalone : null, braki: ver ? ver.braki : null, rozpoznanie: recon.map(r => r.streszczenie) }
