# TABELE DLA JEFFA (07.10): zbroja z kuzni, klan najemnikow Spoils, naprawa u kwatermistrza

Teksty z recenzji paczek (polskie znaki zostawione - to tekst dla Jeffa). Paczki: galezie w-toku/zbroja-craft-jak-bron, w-toku/spoils-najemnicy-prawdziwi, w-toku/spoils-kwatermistrz-z-materialem; NIC NIE WGRANE.

## n131-spoils-kwatermistrz-z-materialem (f0aef78, DLL md5 aa13b61c64d38d9993baf7cbcf284680)

NAPRAWA U KWATERMISTRZA (Spoils of War): JAK TO BĘDZIE WYGLĄDAĆ

Rzeczy są prawdziwe, z Twojej gry. W próbie cena na targu była równa wartości rzeczy: ładunek rudy 8 zł, ładunek drewna 4 zł, skóra 40 zł, len 100 zł. W grze ceny zależą od tego, ile towaru leży na targu.

| Co oddajesz do naprawy | Dotąd u kwatermistrza | Teraz (do kasy miasta) | Skąd kowale biorą materiał |
|---|---|---|---|
| Highland Broad Blade (miecz, wart 104 zł), Plundered | 31 zł, pieniądze znikały | 12 zł (11 za pracę + 1 za materiał) | 1 ładunek rudy i 6 ładunków drewna z targu (5 na wytop żelaza, 1 na węgiel); prawie wszystko zostaje u kowali na następne naprawy |
| ten sam miecz, Damaged | 46 zł | 16 zł (15 + 1) | to samo |
| ten sam miecz, Battered | 62 zł | 20 zł (19 + 1) | to samo |
| ten sam miecz, Mangled (wrak) | 124 zł i miecz jak nowy | NIE naprawi | - (kuźnia z własnym materiałem albo przetop) |
| Stark Boots 1 (buty skórzane, wart 28 zł), Plundered | 8 zł | 4 zł (3 + 1) | skóra oraz trochę żelaza na sprzączki |
| Casterly Heavy Helmet (hełm, wart 314 zł), Damaged | 141 zł | 50 zł (47 + 3) | żelazo z rudy, węgiel z drewna, len na podszycie |
| Brigandine over Hauberk (zbroja, warta 2316 zł), Battered | 1389 zł | 463 zł (434 + 29) | ok. 12 kg żelaza z rudy, węgiel, len |
| ta sama zbroja, Mangled (wrak) | 2779 zł | NIE naprawi | - |
| drugi taki sam miecz w tym samym mieście | 31 zł | 12 zł | nic nowego z targu: kowale biorą z tego, co im zostało po pierwszym |
| miasto bez rudy, ale na targu leży wrak zbroi: miecz | 31 zł | 13 zł (11 + 2) | kowale topią wrak z targu na złom |
| to samo miasto: zbroja Battered | 1389 zł | 510 zł (434 + 76) | ten sam złom z wraku |
| miasto bez skóry: buty | 8 zł | czekają, płacisz 0 zł | komunikat: "not enough leather" |
| miasto bez żadnego żelaza (bez rudy, sztab i wraków): miecz i zbroja | 31 / 1389 zł | czekają, płacisz 0 zł | komunikat: "not enough iron" |

WYJAŚNIENIE PROSTO:
- Kto naprawia. Kowale miasta, w którym stoi kwatermistrz, czyli Twojego miasta. Wszystko, co zapłacisz, trafia do kasy tego miasta. Dotąd te pieniądze znikały z gry.
- Za pracę płacisz jedną czwartą tego, ile rzecz straciła na wartości. Tyle samo, ile płaci każdy u kowali w Armoury: Ty w kuźni, Twoi ludzie i lordowie.
- Materiał. Im bardziej zniszczona rzecz, tym więcej materiału. Kowale kupują na targu całą sztukę towaru, np. cały ładunek rudy, zużywają kawałek, a resztę trzymają na następne naprawy w tym mieście. Ty płacisz tylko za zużyty kawałek. Nic nie przepada, bo ten zapas zapisuje się w grze.
- Gdy na targu czegoś brakuje, rzecz zostaje taka, jaka była, a Ty nic nie płacisz. Gra pisze, czego brakuje.
- Wraków (Mangled) kwatermistrz nie odnawia. Wrak naprawisz w kuźni z własnym materiałem albo oddasz na przetop.
- Menu naprawy od razu pokazuje te ceny i pomija wraki. Pod spodem jest dopisek po angielsku, kto naprawia i czego brakuje.
- Wyłącznik "Spoils Quartermaster Repair" jest w MCM Armoury, w grupie "The law of the battlefield", domyślnie włączony. Wyłączony przywraca naprawę jak dotąd w Spoils.

NA CO UWAŻAĆ:
- W wielu miastach nie ma rudy (po 40 dniach gry tak było w 38 z 97 miast). Tam rzeczy z żelazem poczekają, chyba że na targu leży jakiś wrak do przetopienia. Codzienna linia w logu pokaże, ile rzeczy czekało i na co.
- Zwykła ława naprawcza u kowala w Armoury dalej naprawia wraki tanio i bez materiału. To trzeba zamknąć osobną paczką.

## n130-spoils-najemnicy-prawdziwi (383a724, DLL md5 32b66945a61742ec10bcb84080d28d0e)

KLAN NAJEMNIKÓW (Spoils of War), „tylko prawdziwi”. Jak to będzie wyglądać w grze:

| Co robisz / co się dzieje | Dziś | Po zmianie |
|---|---|---|
| Zakładasz klan za 100 000 zł | Do skarbca klanu idzie 50 000, druga połowa znika. Kapitan rusza z ok. 54 żołnierzami znikąd (Spoils pisze „20 troops”, a daje cały gotowy oddział) | Do skarbca idzie całe 100 000, nic nie znika. Kapitan rusza SAM i zbiera ludzi: ochotników we wsiach i miastach (rekrut ok. 20 zł, żołnierz ok. 30-50 zł) i najemników w karczmie (ok. 100 zł). 54 ludzi kosztuje go ok. 1 500-2 500 zł plus żołd |
| Odnawiasz zniszczony klan za 50 000 zł | 25 000 do skarbca, 25 000 znika, znów 54 żołnierzy znikąd | Całe 50 000 do skarbca, przywódca rusza sam i werbuje |
| Najmujesz nową drużynę za 200 000 zł (kolejna 400 000...) | Kapitan dostaje 50 000, 150 000 znika, 54 żołnierzy znikąd | Kapitan dostaje całe 200 000, rusza sam i werbuje |
| Spoils nie zdołał stworzyć przywódcy albo wywrócił się w połowie zakładania | Tracisz pieniądze za nic | Pieniądze wracają do Ciebie (pokaże się komunikat) |
| Klan służy Twojemu królestwu: każdej nocy (BannerKings) | BannerKings wyrzuca kapitana z jego własnej drużyny i stawia w jego miejsce żołnierza znikąd. Drużyna bez wodza się rozpada, a kapitan dostaje nową, znów z 54 ludźmi znikąd | Kapitan zostaje ze swoimi ludźmi. Zwerbowani zostają tymi, kogo zwerbował. Nikt nie pojawia się znikąd |
| Drużyna klanu rozbita, przywódca wolny | Następnego dnia znów 54 żołnierzy znikąd, za każdym razem | Przywódca wychodzi sam. Jeśli ma pieniądze, odbudowuje się werbunkiem; bez pieniędzy chodzi sam (czeka) |
| Przywódca wraca z niewoli | Spoils pisze „z 10 wojownikami”, a w polu jest ich 54 | Wraca sam; zaraz po komunikacie Spoils Armoury pisze, że sam |
| Klan zwolniony z Twojego królestwa, w drodze | Co jakiś czas dostaje 3-7 „ochotników” znikąd, a jego złoto przepada | Takich ochotników nie ma. Ludzie tylko ze wsi, miast i karczmy |
| Werbunek u notabla albo w karczmie | Prawdziwy: człowiek ubywa z ludności osady, zapłata trafia do notabla lub miasta | Bez zmian |
| Wszyscy inni lordowie świata (także 5 klanów najemników ROT) | Odradzają się z gotowym oddziałem; najemnikom ROT w służbie królestwa BannerKings też co noc podmienia wodza | Bez zmian (to osobny, późniejszy krok dla całego świata) |

Co zobaczysz:
- Zaraz po komunikacie Spoils pojawi się komunikat Armoury po angielsku, np. „... takes the field alone - no soldiers out of thin air (54 men would have appeared from nowhere) ... All 100000 den. you paid went to ...”.
- W logu co dobę linia „Spoils - klan najemnikow”: ilu ludzi klan naprawdę zwerbował (od notabli, z karczmy, za ile) i ile prób „z niczego” zablokowano.
- Wyłącznik: MCM Armoury, grupa „The law of the battlefield”, pozycja „Spoils Clan Real Soldiers” (domyślnie włączony). Wyłączony = wszystko jak dziś.

Czego jeszcze nie ma:
- Karczma dziś „rodzi” najemników znikąd dla wszystkich: dla Ciebie, dla każdego lorda i dla klanu.
- Twoja zasada „najemnicy tylko spośród zwolnionych z wojska” wymaga osobnego kroku dla całego świata (konto weteranów). Gdy go zrobimy, obejmie też klan.

W obecnej grze klanu nie masz (logi Spoils od 08.08 nie mają ani jednego założenia). Zmiana zadziała dopiero, gdy go założysz.

## n129-zbroja-craft-jak-bron (2344a61, DLL md5 639228e168e7f2d83b3e372be06ffc95)

**Zbroja z kuźni (zakładka CRAFT) – jak to będzie wyglądać**

Co znaczą słowa (nazwy z gry, liczby z RBM i naszego zużycia):
- **zepsuta** = Rusty / Battered / Ripped – jak zbroja zużyta do 30%: chroni ok. 56% normy, warta 30%
- **słaba** = Dented / Loose / Worn – jak zużyta do 60%: chroni ok. 86%, warta 60%
- **zwykła** – jak dziś
- **dobra** = Fine / Waxed / Tailored – +5 ochrony na każdą osłoniętą część, warta ×1,5
- **bardzo dobra** = Lordly (skóra i sukno: Fine) – +10, warta ×2,5
- **legendarna** = Legendary – +15, warta ×5
- **pęka** – sztuka się nie udaje, cały materiał przepada (jak dziś w Banner Kings)

**Próg** to liczba Smithing, od której kowadło w ogóle pozwala zacząć. Poniżej progu nic nie schodzi. Płyta i kolczuga: tier 3 – 70, tier 4 – 105, tier 5 – 140, tier 6 – 175. Skóra ma próg o 10 niższy, sukno o 20. Szanse są takie same dla każdego materiału, zmienia się tylko próg. Liczby obowiązują po wgraniu 127 (próg 35 na poziom).

**Na 100 prób przy kowadle** (bez perków kowalskich):

| Zbroja | Twoje Smithing (płyta / skóra / sukno) | pęka | zepsuta | słaba | zwykła | dobra | b. dobra | legendarna | średnia wartość udanej sztuki |
|---|---|---|---|---|---|---|---|---|---|
| tier 3 | 70 / 60 / 50 (próg) | 35 | 9 | 14 | 28 | 14 | – | – | 92% zwykłej |
| tier 3 | 100 / 90 / 80 | 29 | 6 | 9 | 37 | 19 | – | – | 103% |
| tier 3 | 130 / 120 / 110 | 12 | 4 | 6 | 50 | 28 | – | – | 110% |
| tier 3 | 170 / 160 / 150 | 0 | 2 | 3 | 59 | 36 | – | – | 116% |
| tier 4 | 105 / 95 / 85 (próg) | 35 | 11 | 16 | 25 | 7 | 5 | – | 95% |
| tier 4 | 135 / 125 / 115 | 35 | 7 | 10 | 32 | 10 | 7 | – | 109% |
| tier 4 | 165 / 155 / 145 | 17,5 | 4,5 | 7 | 46 | 14 | 11 | – | 121% |
| tier 4 | 205 / 195 / 185 | 0 | 2 | 4 | 59 | 19 | 16 | – | 131% |
| tier 5 | 140 / 130 / 120 (próg) | 35 | 13 | 19 | 23 | 7 | 3 | 1 | 93% |
| tier 5 | 170 / 160 / 150 | 35 | 8 | 12 | 30 | 9 | 4,5 | 1,5 | 111% |
| tier 5 | 200 / 190 / 180 | 23 | 5 | 8 | 41,5 | 13 | 7 | 2,4 | 125% |
| tier 5 | 240 / 230 / 220 | 0 | 3 | 4,5 | 58 | 19 | 11 | 4,4 | 140% |
| tier 6 | 175 / 165 / 155 (próg) | 35 | 14,5 | 21 | 20 | 6 | 3 | 0,9 | 88% |
| tier 6 | 205 / 195 / 185 | 35 | 9 | 14 | 28 | 8 | 4 | 1,3 | 105% |
| tier 6 | 235 / 225 / 215 | 29 | 6 | 9 | 37 | 11 | 6 | 2 | 121% |
| tier 6 | 275 / 265 / 255 | 6 | 3 | 5 | 54 | 17,5 | 10 | 3,8 | 136% |

Zbroja tieru 1–3 wychodzi najwyżej dobra, a tieru 4 najwyżej bardzo dobra – tak samo jak broń w grze. Perki kowalskie poprawiają wynik. Tier 6 przy Smithing 275 ze wszystkimi trzema perkami: pęka 6, zepsutych i słabych 0, zwykła 42, dobra 27, bardzo dobra 17, legendarna 8,5.

Dziś ta sama zbroja wychodzi **zawsze zwykła**, a pęka 1% za każdy punkt trudności Banner Kings ponad Twoje Smithing. Napierśnik płytowy tieru 5 przy Smithing 140 dziś pęka 70 razy na 100, a 30 razy wychodzi zwykły. Z paczką pęka 35 razy, a z pozostałych 65 wychodzi: 13 zepsutych, 19 słabych, 23 zwykłe, 6–7 dobrych, 3 bardzo dobre i 1 legendarna.

**Przykład z Twojej gry: „Brigandine over Hauberk”** (napierśnik płytowy, tier 6, tułów 100 / nogi 95 / ręce 100, wartość 2316). Kujesz go od Smithing 175.

| Stan | Ochrona tułów / nogi / ręce | Wartość | W sklepie kupisz za ok. | Sklep zapłaci Ci ok. | Kowal naprawi do zwykłej za |
|---|---|---|---|---|---|
| legendarna | 115 / 110 / 115 | 11 580 | 12 738 | 9 843 | – |
| bardzo dobra (Lordly) | 110 / 105 / 110 | 5 790 | 6 369 | 4 922 | – |
| dobra (Fine) | 105 / 100 / 105 | 3 474 | 3 821 | 2 953 | – |
| zwykła | 100 / 95 / 100 | 2 316 | 2 548 | 1 969 | – |
| słaba (Dented) | 85 / 81 / 85 | 1 390 | 1 529 | 1 182 | 231 |
| zepsuta (Rusty) | 55 / 53 / 55 | 695 | 764 | 591 | 405 |

Ceny sklepu podane są dla miasta ze zwykłą półką. W grze przesunie je podaż i popyt, a za sztukę zawsze płaci kasa miasta.

Na 100 prób tego napierśnika przy Smithing 175 pęka 35. Z reszty wychodzi ok. 15 zepsutych, 21 słabych, 20 zwykłych, 6 dobrych, 3 bardzo dobre i 1 legendarna. Przy Smithing 275 pęka 6, a wychodzi 3 zepsute, 5 słabych, 54 zwykłe, 17–18 dobrych, 10 bardzo dobrych i 4 legendarne.

Hełm tego samego tieru („Casterly Heavy Helmet”, głowa 54, wartość 314):

| Stan | Ochrona głowy | Wartość |
|---|---|---|
| legendarny | 69 | 1570 |
| Lordly | 64 | 785 |
| Fine | 59 | 471 |
| zwykły | 54 | 314 |
| Dented | 46 | 188 |
| Rusty | 30 | 94 |

**Co jeszcze się zmienia:**
- Reguła obejmuje każdą zbroję w zakładce, także zbroje końskie i siodła.
- W zakładce przy zbroi „Difficulty” pokazuje próg, a „Botching Chance” prawdziwą szansę pęknięcia.
- Zepsutą albo słabą zbroję z kuźni naprawiasz na ławce jak zużyty łup. Kowal robi z niej zwykłą za 17,5% / 10% jej wartości, a sam zrobisz to za trochę materiału i staminy.
- Legendarne i inne dobre zbroje po bitwie pokazują zużycie, a naprawa oddaje im ich stan.
- Zardzewiała zbroja nie robi się lepsza od noszenia, a kowal nie naprawia za darmo zbroi, której nie bito.
- Druga kopia tej samej zbroi nie przejmuje stanu pierwszej, więc z jednej legendy nie zrobisz dwóch. Świeżo wykuta lepsza kopia zbroi, którą nosisz już dziś, zachowuje swój stan. To jest poprawka z recenzji.
- Wyłącznik w MCM: „Armour Craft Like Weapons”. Wyłączony daje wszystko jak przed paczką.

**Uwaga – stara dziura, nie z tej paczki:** kowal na ławce naprawia zbroję z sakw bez materiału. Zardzewiały Brigandine z targu (~764) plus kowal (405) daje zwykły, za który sklep zapłaci ~1969, czyli ok. 800 zysku z niczego. Do zamknięcia razem z naprawą u kwatermistrza (paczka 131: materiał z targu wedle stanu).
