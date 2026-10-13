export const meta = {
  name: 'raport-roczny',
  description: 'Raport z biegu rocznego 364 doby: wojsko, pieniadz rodow i koron, gospodarka, wojna, progi - z niezalezna weryfikacja i synteza dla Jeffa',
  phases: [
    { title: 'Analiza', detail: '5 analitykow, kazdy jedna dziedzina, liczby z logu i csv' },
    { title: 'Weryfikacja', detail: 'niezalezne przeliczenie kluczowych liczb i proba obalenia wnioskow' },
    { title: 'Synteza', detail: 'jeden raport z rozdzialem prostym jezykiem' },
  ],
}

const REPO = String.raw`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`
const INT = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int`
const SP = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad`
const LOGDIR = String.raw`D:\Backup-Bannerlord\logs\2026-10-13-noc34-35`
const OUT = SP + String.raw`\raport-rok`

const WSP = String.raw`
Pracujesz przy modach do gry Mount & Blade II Bannerlord (Realm of Thrones + BannerKings + nasze mody Armoury i CrashScribe). Wlasciciel: Jeff - gracz, nie programista.
NAJPIERW przeczytaj zasady pracy: ${REPO}\CLAUDE.md (obowiazuja cie: nie pisz "dziala" bez dowodu z logu, obieg zamkniety - nic z niczego i nic w nicosc, prosty jezyk).
TO JEST PRACA TYLKO DO ODCZYTU: nie uruchamiaj gry, nie buduj, nie zmieniaj kodu, ustawien ani zapisow, zadnych commitow i pushow. Pisz wylacznie do katalogu ${OUT} (istnieje).

DANE BIEGU ROCZNEGO (nowa kampania, 364 doby; dzien gry 108837 = doba 1; sam swiat AI - rod gracza stoi i nic nie robi; kod = galaz w-toku/zbior-e8 9e2e0e6: WGRANIE 36 + poprawki wojska i zelaza "rundy 3" + reguly slubow NPC; wynik: 364/364 dob, 0 linii ERROR w logu Armoury, srednio 12.0 s na dobe):
- log Armoury: ${LOGDIR}\Armoury-2026-10-13_06-24-10.log (119 MB, 129 tys. linii - NIE czytaj go w calosci narzedziem Read; uzywaj Grep i skryptow python)
- log CrashScribe: ${LOGDIR}\session-2026-10-13_06-24-09.log (m.in. KRONIKA WOJEN, linie "Swaty ROT (Z.3)")
- tabele: ${LOGDIR}\Logs\2026-10-13_06-24-10\ (budzet-rodow.csv, balans-krolestw.csv, kasy-miast.csv, kiesa-ludu.csv, konie-krolestwa.csv, ludnosc-bk.csv, ludzie-regiony.csv, spichlerze-regiony.csv, wioski-okregi.csv, h3-domy.csv oraz bitwy.log, finanse.log, handel.log, budowy.log, warsztaty.log, wioski.log, zakupy.log, noc.log, unikaty.log, balans.log, start.log)
- gotowe progi tego biegu: ${SP}\test\progi-rok5\etap2.txt ... etap8.txt (kolumny: co | TAK/NIE | wartosc | prog)
POROWNANIE - poprzedni bieg roczny (WGRANIE 34, bez poprawek rundy 3): log ${LOGDIR}\Armoury-2026-10-13_02-15-21.log, tabele ${LOGDIR}\Logs\2026-10-13_02-15-21\, progi ${SP}\test\progi-rok364\, opis ${REPO}\docs\test-zbiorczy\TRIAZ-3-bieg-roczny-2026-10-13.md. Test 120 dob rundy 3: progi ${SP}\test\progi-fin4\.
KOD (do zrozumienia, co znaczy linia logu albo kolumna): ${INT}\Armoury\src oraz parser progow ${INT}\tools\sprawdz_logi.py (grupy etap2..etap8 - sa tam gotowe wzorce linii; korzystaj z nich).

JAK PRACOWAC: skrypty zapisuj w ${OUT}\skrypty\<twoj-klucz>\ i uruchamiaj jako "python -I skrypt.py" z ustawionym PYTHONIOENCODING=utf-8. Kazda liczba w twoim pliku ma miec zrodlo (plik + numer linii albo kolumna csv + doba) i znacznik [P] = pomiar albo [S] = szacunek. Czego nie da sie ustalic z danych - napisz wprost "nie wiem", nie zgaduj. Pliki pisz po polsku BEZ polskich znakow diakrytycznych (jak cale docs), krotkie zdania, tabele. Rok = 364 doby; kwoty w zlocie (denarach gry).
`

const LENSES = [
  { key: 'wojsko', title: 'WOJSKO', q: String.raw`
1. Ile jest wojska: swiat razem, druzyny lordow (krolestwa w wojnie i wszystkie), zalogi, kompanie najemne, bandy - w dobach 1, 91, 182, 273, 364 i srednia z ostatnich 28 dob. To samo dla poprzedniego biegu rocznego (tabela obok siebie).
2. Tabela KROLESTW (kazde krolestwo jeden wiersz): ludzi w druzynach i w zalogach na starcie i na koncu, limit druzyn, ile % limitu, liczba rodow, ile dob w wojnie, sredni tier, udzial jazdy.
3. Sklad i wyposazenie: tiery, jazda (t3+, z koniem i uprzeza, czekajacy na konia), braki broni / zbroi / koni, odziez - na ile wojsko jest "prawdziwe".
4. Przeplyw ludzi na dobe: werbunek, awanse, polegli, ranni, zwolnieni (z jakiego powodu), dezerterzy, jency. Czy wojsko topnieje, stoi czy rosnie w ostatnim kwartale?
5. ANATOMIA LUKI do progu 85 tys. (pasmo projektu 95-115 tys. w druzynach lordow krolestw w wojnie; wynik tego biegu: srednia 28 dob 79 409, ostatnia doba 73 578): dla kazdego rodu w wojnie na koniec biegu ustal, co go trzyma ponizej limitu druzyny - (a) brak zlota na zold (kiesa, pulap zoldu 166, rod na suficie), (b) ma zloto, ale nie werbuje (brak rekruta w zasiegu? zachowanie AI? limit?), (c) swieze straty w bitwie, (d) niewola / brak dowodcy, (e) inne. Policz, ile ludzi brakuje do limitu w kazdej grupie i ile zlota dziennie trzeba by dostarczyc grupie (a), zeby ja dopelnic. Uzyj budzet-rodow.csv (kolumny kiesa_glowy, pulap, zold_partii, ludzi_partie, limit_partii, na_suficie, zwolnieni_na_sucho, renta_180, warunek_180 itd. - znaczenie kolumn sprawdz w kodzie) i linii logu o werbunku / zwolnieniach.
6. Dlaczego ostatnia doba (73.6 tys.) jest nizsza od sredniej 28 dob (79.4 tys.) - trend czy jedna bitwa?` },
  { key: 'pieniadz', title: 'PIENIADZ RODOW I KORON', q: String.raw`
1. Ile ZARABIAJA i WYDAJA rody: z budzet-rodow.csv policz dla kazdego rodu srednie dzienne wplywy wedlug zrodel (lenna i podatki, renty korony, zwrot zoldu, warsztaty / karawany, lupy i okupy, jednorazowe) i wydatki (zold druzyn, zold zalog, inne), kiese na starcie i na koncu. Daj: tabele KROLESTW (suma rodow krolestwa: wplywy / wydatki / saldo na dobe, zloto rodow start -> koniec, ile rodow na minusie, ile na suficie zoldu), 15 rodow najbogatszych i 15 najbiedniejszych na koniec (z krolestwem i liczba ludzi), rozklad (mediana, kwartyle).
2. Ile zarabiaja i wydaja KORONY (skarbce krolestw): wplywy wedlug zrodel (podatki, myta, podatek wojenny 184, daniny wsi i kupcow, inne), wydatki (zwrot zoldu, renty 180, najemnicy, inne), stan skarbca start -> koniec, dlug w Banku - tabela krolestw.
3. GDZIE LEZY ZLOTO SWIATA: stany w dobach 1, 182, 364 wedlug posiadaczy (rody, korony, kasy kupcow miast, kiesy ludu miast / podzamczy / wsi, notable, karawany, kompanie najemne, Bank, bandy, gracz, inne) - z linii "Pieniadz swiata (bilans)" i tabel. Kto zyskal, kto stracil w ciagu roku i ile na dobe.
4. Czy pieniadz KRAZY: glowne strumienie na dobe (zold -> lud i kupcy, podatki -> panowie i korony, handel, lupy). Czy ktorys zbiornik rosnie bez konca albo wysycha?
5. Kontrole zamkniecia: linie bilansu pieniadza ("z niczego" / "w nicosc" / reszta kontroli r) - suma roczna i najgorsza doba; czy cos powstaje z niczego albo znika.
6. Porownaj z poprzednim biegiem rocznym: co poprawki rundy 3 zmienily w pieniadzach rodow (rody z lennem bez renty, zadatek kompanii najemnych, pulapka rent).` },
  { key: 'gospodarka', title: 'GOSPODARKA TOWAROWA I LUDZIE MIAST', q: String.raw`
1. Jedzenie: glod w miastach, zamkach i wsiach (ile osad, ile dob), spichlerze, "stac jedzenie", zjedzone bez zaplaty - trend w roku.
2. Towary i ceny: indeks cen w czasie (zywnosc, surowce, wyroby, bron i zbroje, konie), miasta bez zelaza / rudy / narzedzi / drewna / odziezy / koni - ile i jak dlugo; najwieksze niedobory i nadwyzki na koniec roku; bilans towarow (linie "Towary (bilans)") - co powstaje i znika poza produkcja i zuzyciem.
3. Produkcja: wsie (plony, wydobycie), warsztaty (ile pracuje, ile stoi i dlaczego - warsztaty.log), budowy (budowy.log: ile poziomow ukonczono, ile budow stoi).
4. Handel: karawany (liczba w czasie, zysk, rozbite), wozy wsi i wozy surowcow rundy 3 (czy dowoza zelazo), kasy kupcow miast (kasy-miast.csv: ile miast z pusta kasa, trend), handel.log, zakupy.log.
5. Ludzie: ludnosc miast i wsi (ludnosc-bk.csv, ludzie-regiony.csv), dobrobyt, lojalnosc jesli jest w logu, kiesy ludu (kiesa-ludu.csv: stan, przyrost, ile miast biednych).
6. Czy gospodarka TRZYMA SENS: czy ceny odpowiadaja niedoborom, czy bogate miasta sa bogate z powodu, czy biedne maja szanse sie podniesc, czy cos stoi w miejscu przez caly rok (np. 31 najbiedniejszych miast).
7. Porownaj z poprzednim biegiem rocznym (zelazo, ruda, kasy miast).` },
  { key: 'wojna', title: 'WOJNA I SWIAT', q: String.raw`
1. Wojny roku: z KRONIKI WOJEN (log CrashScribe) i linii E7/0 - kto z kim, od kiedy do kiedy, ile trwala, jak sie skonczyla (pokoj, trybut, nic); ile krolestw w wojnie w czasie; czy sa wojny bez konca i bez wyniku.
2. Bitwy i oblezenia: bitwy.log i balans-krolestw.csv - liczba bitew lordow na dobe, wygrane / przegrane wedlug krolestw, szturmy udane i odparte, KTORE miasta i zamki zmienily pana (kiedy, od kogo do kogo), ile razy; czy ktores krolestwo rosnie jak kula sniezna albo znika.
3. Mapa na koniec roku wobec startu: twierdze kazdego krolestwa start -> koniec (tabela), rody, ktore zmienily krolestwo, upadle krolestwa i rody.
4. Napady na wsie, spustoszenie, uchodzcy, wyrzutki i bandy: ksiega ludzi (linie "Ludzie"), pula wyrzutkow (prog E4/3 NIE: 11 869 w d364 wobec bazy 3 053; bandy 819 / 17 279 ludzi) - skad sie biora, czy wracaja, czy to rosnie bez konca i czy to grozi swiatu w roku 2.
5. Jency i okupy, smierc i narodziny bohaterow, sluby NPC (linie "Swaty ROT (Z.3)": ile w roku, ilu wolnych zostalo), turnieje.
6. Czy wojna TRZYMA LOGIKE: czy wygrywa silniejszy i bogatszy, czy armie maja co jesc i czym placic, czy przegrywajacy traci ziemie i dochod, czy pokoj przychodzi, gdy strona jest wyczerpana, czy zima / pory roku cos zmieniaja (w pierwszym roku nie ma zimy - sprawdz, jaka pora panuje), czy Nocna Straz / Wolni Ludzie / Dothrakowie zachowuja sie zgodnie ze swiatem.
7. Porownaj z poprzednim biegiem rocznym.` },
  { key: 'progi', title: 'PROGI, BLEDY, CZAS', q: String.raw`
1. Przejdz WSZYSTKIE pozycje NIE w szesciu plikach progow tego biegu (${SP}\test\progi-rok5\). Dla kazdej: wartosc teraz, wartosc w poprzednim biegu rocznym (${SP}\test\progi-rok364\) i w tescie 120 dob (${SP}\test\progi-fin4\), werdykt: REGRES (gorzej niz bylo - wskaz podejrzana zmiane w rundzie 3), ZNANE (bylo i jest; czy ma wpis w docs/STAN-PRAC.md albo TRIAZ-3), LEPIEJ, PROG DO ZMIANY (prog pisany pod 120 dob albo pod stan sprzed zmian - uzasadnij). Posortuj wedlug wagi dla gracza.
2. Pozycje, ktore byly NIE w poprzednim biegu, a teraz sa TAK (co runda 3 naprawila) - lista.
3. Nowe progi rundy 3 (bloki etap8_p3woj, etap8_p3_zelazo): wynik kazdego, w tym P3W1-2 i P3W2-1.
4. Bledy: linie ERROR / WARN / "potkniecie" / wyjatki w obu logach (rodzaje, liczba, czy sa nowe wobec poprzedniego biegu); 23 "bledy CrashScribe" przy starcie (wyjatki Harmony innych modow) - czy te same co zawsze; plik hang-2026-10-13_07-39-37.log i wywrotka silnika przy wyjsciu (C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\) - czy to znany blad zamykania.
5. Czas: sekundy na dobe w czasie (czy rosnie z wiekiem kampanii), profil doby (linie profilu - co kosztuje najwiecej), koszt nowych rzeczy rundy 3 i slubow.
6. Kontrole spojnosci ksiag (ludzie, konie, towary, pieniadz, kiesy): ktore maja reszte rozna od zera, kiedy i ile.` },
]

const A_SCHEMA = {
  type: 'object',
  properties: {
    streszczenie: { type: 'string', description: 'do 12 zdan prostym jezykiem: najwazniejsze ustalenia' },
    liczby: { type: 'array', items: { type: 'object', properties: { nazwa: { type: 'string' }, wartosc: { type: 'string' }, zrodlo: { type: 'string' } }, required: ['nazwa', 'wartosc', 'zrodlo'] } },
    niespojnosci: { type: 'array', items: { type: 'object', properties: { tytul: { type: 'string' }, opis: { type: 'string' }, dowod: { type: 'string' }, waga: { type: 'string', enum: ['wysoka', 'srednia', 'niska'] } }, required: ['tytul', 'opis', 'dowod', 'waga'] } },
    co_trzyma_sens: { type: 'array', items: { type: 'string' } },
    plik: { type: 'string' },
  },
  required: ['streszczenie', 'liczby', 'niespojnosci', 'co_trzyma_sens', 'plik'],
}

const V_SCHEMA = {
  type: 'object',
  properties: {
    potwierdzone: { type: 'array', items: { type: 'string' } },
    obalone: { type: 'array', items: { type: 'object', properties: { co: { type: 'string' }, dlaczego: { type: 'string' }, poprawna_wartosc: { type: 'string' } }, required: ['co', 'dlaczego', 'poprawna_wartosc'] } },
    niespojnosci_po_weryfikacji: { type: 'array', items: { type: 'object', properties: { tytul: { type: 'string' }, werdykt: { type: 'string', enum: ['prawdziwa', 'czesciowo', 'artefakt'] }, uzasadnienie: { type: 'string' } }, required: ['tytul', 'werdykt', 'uzasadnienie'] } },
    braki: { type: 'array', items: { type: 'string' } },
    plik: { type: 'string' },
  },
  required: ['potwierdzone', 'obalone', 'niespojnosci_po_weryfikacji', 'braki', 'plik'],
}

const results = await pipeline(
  LENSES,
  (l) => agent(String.raw`${WSP}
TWOJA DZIEDZINA: ${l.title} (klucz: ${l.key}).
Pytania, na ktore masz odpowiedziec liczbami z danych:${l.q}

Jeff zapytal doslownie: "na jakim jestesmy etapie, daj mi raport z rocznego testu: ile jest wojska, ile dane rody i panstwa zarabiaja, itp. - czy gra jest spojna i calosciowo trzyma sens i logike ekonomii i wojny". Twoj plik jest jednym rozdzialem odpowiedzi.
Na koncu pliku dwie listy: "CO TRZYMA SENS" (rzeczy, ktore dzialaja logicznie - z liczba na dowod) i "NIESPOJNOSCI" (rzeczy, ktore nie trzymaja sensu ekonomii albo wojny - kazda z dowodem, waga wysoka / srednia / niska i jednym zdaniem, co by ja naprawilo). Badz uczciwy w obie strony: nie upiekszaj i nie szukaj dziury na sile.
Zapisz plik ${OUT}\${l.key}.md i zwroc strukture (pole plik = pelna sciezka).`, { label: 'analiza:' + l.key, phase: 'Analiza', schema: A_SCHEMA }),
  (a, l) => {
    if (!a) return null
    return agent(String.raw`${WSP}
JESTES NIEZALEZNYM WERYFIKATOREM rozdzialu "${l.title}" (plik analityka: ${OUT}\${l.key}.md). Nie ufaj mu.
(1) Wybierz 10 liczb, na ktorych stoja jego wnioski (w tym te, ktore trafia do raportu dla Jeffa), i policz je SAM od zera wlasnym skryptem z surowych danych (skrypty w ${OUT}\skrypty\${l.key}-wer\). Skrypty analityka wolno ci przeczytac dopiero po wlasnym wyniku - zeby wyjasnic roznice.
(2) Kazda jego NIESPOJNOSC sprobuj OBALIC: czy to nie artefakt pomiaru, definicji linii logu, biegu bez gracza, pierwszego roku kampanii albo porownania z niewlasciwa baza? Werdykt: prawdziwa / czesciowo / artefakt.
(3) Czego brakuje: wazna czesc pytania Jeffa bez odpowiedzi, liczba bez zrodla, wniosek szerszy niz dane.
Streszczenie analityka: ${a.streszczenie}
Jego niespojnosci: ${JSON.stringify(a.niespojnosci)}
Zapisz ${OUT}\${l.key}-weryfikacja.md (tabela: liczba analityka | twoja | zgodne?; potem niespojnosci z werdyktem; potem braki) i zwroc strukture.`, { label: 'weryfikacja:' + l.key, phase: 'Weryfikacja', schema: V_SCHEMA }).then(v => ({ key: l.key, title: l.title, analiza: a, weryfikacja: v }))
  }
)

const ok = results.filter(Boolean)
log('rozdzialy gotowe: ' + ok.map(r => r.key).join(', ') + (ok.length < LENSES.length ? ' (BRAKUJE ' + (LENSES.length - ok.length) + ')' : ''))

phase('Synteza')
const S_SCHEMA = {
  type: 'object',
  properties: {
    rozdzial0: { type: 'string', description: 'pelny tekst rozdzialu 0 dla Jeffa, prostym jezykiem' },
    niespojnosci: { type: 'array', items: { type: 'object', properties: { tytul: { type: 'string' }, waga: { type: 'string' }, co_zrobic: { type: 'string' } }, required: ['tytul', 'waga', 'co_zrobic'] } },
    plik: { type: 'string' },
  },
  required: ['rozdzial0', 'niespojnosci', 'plik'],
}
const synth = await agent(String.raw`${WSP}
JESTES AUTOREM RAPORTU KONCOWEGO dla Jeffa. Wyjatek od zasady "tylko OUT": wolno ci zapisac JEDEN plik w repo: ${REPO}\docs\test-zbiorczy\RAPORT-ROCZNY-2026-10-13.md (bez commita).
Materialy: rozdzialy analitykow i weryfikacje w ${OUT}\ (wojsko, pieniadz, gospodarka, wojna, progi + pliki *-weryfikacja.md) - przeczytaj WSZYSTKIE. Gdzie weryfikator obalil liczbe albo uznal niespojnosc za artefakt - uzyj wersji weryfikatora i zaznacz to. Do pytania "na jakim jestesmy etapie": ${REPO}\docs\STAN-PRAC.md od wpisu "WGRANIE 34" do konca oraz ${REPO}\docs\PLAN-DO-KONCA-MODA-2026-10-09.md (co bylo w planie, co zrobione, co zostalo).
Wyniki ze struktur (skrot): ${JSON.stringify(ok.map(r => ({ dziedzina: r.title, streszczenie: r.analiza.streszczenie, obalone: r.weryfikacja ? r.weryfikacja.obalone : 'BRAK WERYFIKACJI', niespojnosci: r.weryfikacja ? r.weryfikacja.niespojnosci_po_weryfikacji : r.analiza.niespojnosci, braki: r.weryfikacja ? r.weryfikacja.braki : [] })))}

UKLAD PLIKU:
0. "Dla Jeffa - prostym jezykiem" (najwyzej 60 linii, zdania jak do gracza, bez nazw kluczy i skrotow): (a) na jakim etapie jestesmy - co z planu zrobione, co wgrane, co czeka; (b) wojsko - ile, gdzie, czy rosnie, czego brakuje do 85 tys. i dlaczego; (c) kto ile zarabia - krolestwa i rody w kilku liczbach, kto bogaty, kto biedny, gdzie lezy zloto swiata; (d) gospodarka - jedzenie, towary, ceny, handel; (e) wojna - ile wojen, kto wygrywa, czy mapa sie zmienia; (f) WERDYKT: czy swiat jest spojny - co trzyma sens (5-8 punktow), co NIE trzyma sensu (lista wedlug wagi, kazda jednym zdaniem z liczba); (g) co proponujemy naprawic przed koncem moda i w jakiej kolejnosci.
1. Wojsko (tabela krolestw, przeplyw ludzi, anatomia luki do 85 tys.). 2. Pieniadz rodow i koron (tabela krolestw, rody najbogatsze i najbiedniejsze, gdzie lezy zloto, strumienie). 3. Gospodarka. 4. Wojna i swiat. 5. Progi, bledy, czas. 6. Lista niespojnosci z waga, dowodem i propozycja. 7. Czego nie wiemy (uczciwie).
Tylko liczby ze zrodlem z rozdzialow; nic nowego nie zgaduj. Jesli rozdzialy sobie przecza - sprawdz w danych i napisz, ktory ma racje.
Zwroc: rozdzial0 (pelny tekst rozdzialu 0, tak jak w pliku), niespojnosci, plik.`, { label: 'synteza', phase: 'Synteza', schema: S_SCHEMA })

return { synth, rozdzialy: ok.map(r => ({ key: r.key, streszczenie: r.analiza.streszczenie, liczby: r.analiza.liczby.slice(0, 14), obalone: r.weryfikacja ? r.weryfikacja.obalone : null, braki: r.weryfikacja ? r.weryfikacja.braki : null })) }
