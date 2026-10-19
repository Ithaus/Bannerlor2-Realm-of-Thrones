# Raport nocny 18/19.10 - dla Jeffa (prosto)

## Najwazniejsze w trzech zdaniach

1. **Gra jest gotowa: WGRANIE 83** (wgrane 18.10 ok. 19:25 Twojego czasu; galaz `w-toku/noc-wgranie-83` = d14efe4d; Armoury ea276fdb, CrashScribe b88901d9, RealisticCaptivity 293ba089, GrandTourney f181fc51 + CrashScribe.settings.xml). W srodku wszystko, o co prosiles na noc: cofniecie Polnocy, koniec sojuszu Dorzecza ze Stannisem, droga do kazdej wioski, pelny audyt gospodarki z poprawkami i Twoje 4 decyzje (1b, 2c, 3a, 4b).
2. **Testy:** 160 dob Twojej sciezki bez bledow (gospodarka lepsza niz w kazdym wczesniejszym tescie), test przywrocenia wlosci w grze, zdjecia drog w grze i test z bitwa - wszystko 0 bledow.
3. **Twoj zapis jest bezpieczny:** kopie wszystkich zapisow w `D:\Backup-Bannerlord\zapisy-jeffa\2026-10-18-po-sesji-81b`, poprzednie pliki modow obok jako `*.bak-2026-10-18-wgranie83-przed` i w `D:\Backup-Bannerlord\wgrane\2026-10-18-wgranie83-przed`.

## Co sie stanie przy pierwszym wczytaniu Twojego zapisu

**Cofniecie Polnocy - "wlosci jak w ksiazce"** (raz na zapis, w pierwszej godzinie, gdy Twoja druzyna stoi na mapie - nie w miescie, nie w bitwie, nie w armii; jesli stoisz w miescie, poczeka, az wyjdziesz):
- **Krolewska Przystan i Czerwona Twierdza wracaja do Joffreya.** Zaloga Polnocy (ok. 960 ludzi) przechodzi do druzyn Polnocy. Druzyny Polnocy spod Przystani (do 60 jedn.) laduja pod Harrenhal - jednorazowa poprawka fabuly, zeby nie zdobyly miasta znowu za godzine. Koronie wraca 1 125 434 zl zabrane ze skarbca przy zdobyciu.
- **Piec zamkow Stannisa wraca do jego lennikow:** Sharp Point (bar Emmon), Stonedance (Massey), Sweetport (Sunglass), Cape Wrath (Seaworth), Fellwood (Fell). Zabral je Renly.
- **Starfall wraca do Daynow** (Dorne bylo w tej wojnie neutralne).
- **Harrenhal zostaje u Boltona** - tak jest w ksiazce i w skrypcie ROT. Essos bez zmian.
- **Kazda oddana twierdza dostaje zaloge** z druzyn swojego nowego krolestwa (miasto do 400, zamek do 200; dawca zostawia sobie co najmniej polowe) - prawdziwi ludzie, nic z niczego.
- Bohaterowie bez druzyny, ktorzy siedza w oddawanej twierdzy, sa wyprowadzani do swoich (Robb ani nikt z fabuly nie wpadnie do niewoli).
- Dostaniesz wiadomosc po angielsku "The chronicles are set right: ...".
- Sprawdzone w grze: w tescie 2 zamki przeszly do rodow Stannisa, kazdy dostal 200 ludzi zalogi, 0 bledow. Krolewskiej Przystani nie dalo sie sprawdzic w tescie (tam nalezy do Joffreya), sprawdze ja w Twoim logu.

**Sojusze jak w kronikach:**
- **Sojusz Dorzecza ze Stannisem byl naprawde** (potwierdzony w tescie) - zawarlo go wbudowane AI sojuszy gry, nie ROT ani nasz mod. Zostanie zerwany w pierwszej godzinie, bez kar do relacji.
- Od teraz w Westeros dozwolone tylko: Polnoc + Dorzecze; Renly + Tyrellowie (do smierci Renly'ego); Korona + Tyrellowie (po jej smierci). Stannis i Nocna Straz - zadnych sojuszy (Straz + Skagos tez zerwany). Twoje krolestwo (gdy bedziesz krolem) jest poza ta regula. Essos bez zmian.
- **Po smierci Renly'ego Tyrellowie wypowiedza wojne Polnocy** - ta wojna jest w ROT, tylko nic jej nie uruchamialo.
- Kronika wojen w logu ma teraz czesc "SOJUSZE".

**Drogi do kazdej wioski:**
- Kazda z 1957 wiosek ma droge (wczesniej 1109 bylo bez drogi). Drogi prowadza do traktow, do innych drog, do wsi gry i pod bramy miast i zamkow; wioski za wioska dolaczaja do jednej drogi. Wioski tuz przy trakcie dostaja krotka drozke odchodzaca od traktu pod katem (bez prostych kikutow).
- Bez drogi zostaly tylko 3 wsie gry na wyspach bez ladowego polaczenia (Blackcliff, Long Sigh, Ghozai).
- **Drogi nie spowalniaja gry:** rysujemy tylko kawalki wokol kamery (w scenie ok. 2-4 tys. zamiast 74 tys.). W tescie klatki z drogami: srednio ok. 9 ms (wczesniej 21 ms).

**MCM juz nie zamyka gry** po zmianie ustawienia (wyjatek: "Map Speed Buttons", ale przy Better Time ich nie uzywasz).

## Twoje 4 decyzje - zrobione

1. **(1b) Pomoc korony do zoldu wedlug potrzeby** - bogaty nic, sredni czesc, biedny calosc (ta sama miara co renty i zwrot). Szacunek z testu: ok. 73 tys. zl dziennie zostaje w skarbcach na wojne zamiast do bogatych i srednich rodow; armie bez zwolnien (zold dalej placony).
2. **(2c) Danina Wolnych Miast dla Dothrakow - wylaczona.** Dothrakowie zyja z lupow, okupow i najazdow.
3. **(3a) Zysk Banku Zelaznego ponad zapas idzie do Braavos** co 28 dni: polowa do miasta (lud i kupcy - wydaja w Braavos), polowa do rodow Braavos z lennami. Bank zachowuje zapas na pozyczki.
4. **(4b) Podaz jedzenia i soli idzie za cena:** co 7 dni najwyzej 5% ludzi przechodzi do zajecia, ktorego brakuje (oliwki, mieso, winogrona w Essos), i z tego, ktorego jest za duzo (sol, ryby). Bez kosztow i bez zlota z niczego. Dziala po starcie kalibracji (u Ciebie kalibracja ruszyla od nowa 109044, wiec ok. 7 tygodni).

Kazda decyzja ma przelacznik w MCM (Armoury): "Crown War Aid Cash By Need", "Gift Free Cities To Dothraki", "Iron Bank Dividends", "Ks Food Hands Follow Market".

## Audyt gospodarki - co znalazlem i naprawilem

Cztery niezalezne przeglady (pieniadze, towary, kolejnosc zmian, Twoje zasady), kazda uwaga sprawdzona przez osobnego weryfikatora. Naprawione:
- **BannerKings niszczyl zloto:** zakupy dworow co 91 dni (ok. 240 tys. zl znikalo - glowny powod, dla ktorego zlota na swiecie ubywalo) i "ugoda z grupa interesu" (zloto do nikogo). Teraz miasto dostaje zaplate, a grupa - pieniadze.
- **Po kazdym wczytaniu pierwszy dzien liczyl dochody rodow ok. 2 x za duzo** (limit wojska, dwor, kredyt) - naprawione; dwie miary po wczytaniu nie startuja juz od zera.
- **Sterowanie rynkiem jedzenia liczylo czesc towaru dwa razy**; **glodne miasta odrzucaly male dostawy** - naprawione.
- **Mieso z niczego** (dar BannerKings dla bandy herszta) i **towar zrzucany przez zwycieska karawane** - zaksiegowane / na polke.
- **Falszywa "bieda" Korony** po zdobyciu Przystani (jednorazowy przelew liczony jak codzienna strata) - naprawione.
- Wynik testu 160 dob: wszystkie wiersze o "akumulatorach" (kto tylko zbiera) - w porzadku; zgony z glodu 0; odsiecz zawsze; ksiega skor i miesa domknieta.

## Do Twojej decyzji (nic nie zmienialem)

1. **Osobny termin dla wojen fabularnych.** Glowna przyczyna, ze Polnoc wygrywa: rozciagniecie fabuly ROT x4.33 sprawia, ze Korona walczy sama z Polnoca i Dorzeczem ok. 325 dni zamiast ok. 70 (smierc Renly'ego dopiero ok. dnia 519). Moja rada: NIE przesuwac samych wojen (wtedy Stannis i Renly ruszyliby na Korone przed smiercia Renly'ego - Korona na 4 frontach), tylko przyspieszyc cala sekwencje Renly -> Tyrellowie -> Czarny Nurt. Do omowienia.
2. **Regula skarbca stolicy** (zdobywca bierze 1/3 nadwyzki skarbca) - napedza kule sniezna zwyciezcy. Zostawic / zmniejszyc / wylaczyc?
3. **Tarczowe W6** (pomoc zbierana przez korone od lennikow) dalej trafia tez do bogatych rodow - objac je zasada "bogaty nic"? (dzis ok. 41 zl/d - malo).
4. **"Bez miary"**: Nocna Straz, Wolni Ludzie i dluznicy Banku dostaja pelny zwrot i pelne renty mimo kiesy 3-7 x rezerwy (ok. 15-18 tys./d). Mierzyc ich zlotem jak innych?
5. **Spichlerze wsi na 2 lata, pelne od startu i bez psucia** (glod prawie niemozliwy). Sufit ok. 1 rok z lekkim psuciem?
6. **Pelna regula zmiany zajec wiosek (A7)** - dzis dziala mala regula jedzenia (decyzja 4b); pelna A7 (mlyn -> tartak z kosztem przebudowy) wymaga osobnego testu.
7. **Zwykli jency w lochu Krolewskiej Przystani** zostaja w lochu przy oddaniu miasta (moga to byc zolnierze Korony). Wypuscic ich do Korony?
8. **Nadanie wojenne korony (DS)** nie patrzy na pelny skarbiec - krol z pelnym skarbcem dalej bierze podatek wojenny od ludu. Najpierw "zyc z wlasnego skarbca"?

## Jak to wylaczyc, gdyby cos przeszkadzalo (MCM -> Armoury)

- Cofniecie wlosci: "Book Holdings Restore" (dziala raz; po wykonaniu nic nie robi), zaloga: "Book Holdings Garrison".
- Drogi boczne: "Map Side Roads"; tryb kawalkow: "Map Side Roads Chunks"; wszystkie wioski: "Map Side Roads All Villages".
- Sojusze (CrashScribe): "Canon Alliances", zrywanie: "Canon Alliances Break On Load", wojna Tyrellow: "Canon Reach North War".

## Co sprawdze w Twoim pierwszym logu po wczytaniu

Linie "Wlosci z kronik" (Krolewska Przystan, zaloga, Polnoc pod Harrenhal, zwrot skarbca), "Sojusze kanonu" (zerwany Dorzecze + Dragonstone), drogi i klatki ("Drogi boczne ... komorki"), "Doplata wedlug potrzeby rodu (f20)", bledy.
