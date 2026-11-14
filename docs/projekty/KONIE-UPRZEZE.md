# PROJEKT: konie, uprzęże, waga, zbrojownia (stash) i HUD koni

Status: **tylko projekt** (15.11). Nie ma kodu ani wgrania.
Wersja 2: poprawiona według krytyki (kontrola w kodzie `nscal` i dekompilacji `dek`/`dte`).

Znaczniki: **[K]** to pewne z kodu albo dekompilacji, **[P]** to przypuszczenie do sprawdzenia przed kodem, **[D]** to decyzja Jeffa.
Napisy w grze są po angielsku. Nazwy poniżej są robocze.

Polecenie Jeffa (15.11, zrzuty okna handlu z tooltipem PARTY SPEED: "Footmen on horses +0.67", "Cargo within capacity -0.08", "Horse quality: spare horses -0.16"; pasek mapy 46 ludzi):
> komplet koń + uprząż = 1 powinien liczyć się, że żołnierz ma na czym jechać; uprzęże założone na konie nie powinny być wliczane do wagi; przenoszę zbroje i broń do stash i ubywa mi udźwigu, a nadal wiozę wszystko; HUD: konie np. 23/31 używane przez ludzi, 21/31 uprzęży, ile koni jucznych 4/5 zanim będą kary za herding; dalej sprawdzamy umiejętności konia i uprzęży; używane konie i uprzęże trzeba zaznaczyć albo wyjąć z ekwipunku (stash), nie znikają; znikają tylko konie jazdy i konnych łuczników oraz zbroje.

---

## 0. Skąd się biorą błędy (stan dziś)

| Objaw u Jeffa | Przyczyna | Pewność |
|---|---|---|
| "Footmen on horses +0.67" liczy konie bez uprzęży | Gra liczy `num10 = Min(piechurzy, ItemRoster.NumberOfMounts)` i nie patrzy na uprząż ani na Riding (`DefaultPartySpeedCalculatingModel.cs:108,133,420-426`). Nasze HorsePace i MarchPace też biorą sam `NumberOfMounts`. | [K] |
| Konie jazdy nie są w tej liczbie | Konie i uprzęże jazdy leżą w zbrojowni DTE (`ArmyArmory.Armory`), a gra liczy `NumberOfMounts` tylko z `MobileParty.ItemRoster`. Ze zrzutu: +0.67 = 0.15 × 43/46 × 4.8, czyli w ładunku jest **43** koni. | [K] |
| Uprząż na koniu waży | Koń waży 0, uprząż waży jak pancerz (`DefaultInventoryCapacityModel.cs:41-47`). Nasz klucz `HarnessOnSpareHorses` nie zdejmuje wagi, tylko dopisuje udźwig ("Harness worn by spare horses", `PackMarket.cs:420-425`). Kara "Cargo within capacity" więc zostaje. Nie sprawdza też rodziny konia ani Riding. | [K] |
| Przenoszę rzeczy do stash i ubywa mi wagi | Zbrojownia DTE to osobny statyczny `ItemRoster` (`ArmyArmory.cs:24`). Gra liczy wagę tylko z `MobileParty.ItemRoster`. Wszystko w stash waży więc 0, a wojsko dalej to wiezie. | [K] |
| "Horse quality: spare horses -0.16" | Nasz wpis (`HorsePace.cs:80`). Bierze najlepsze luzaki i nie wie o uprzęży ani umiejętności. | [K] |
| Uprzęży nikt nie sprawdza pod Riding | DTE nie sprawdza Riding wcale. Nasz SkillLawWard sprawdza konia, uprzęży nie (K5). Uprzęże ROT mają wymóg 40-150 (typowo 70 i 100). | [K] |
| Trzy różne reguły "kto jedzie" | HorsePace sadza jeźdźca bez uprzęży; KitPreview liczy go jako pieszego; `PackMarket.UnderPacks` liczy konie pod piechurami bez uprzęży i bez Riding; `PackMarket.Riders()` liczy konia z pierwszego ekwipunku bojowego **wzorca**, z rannymi, a nie ze zbrojowni. | [K] |

---

## 1. Zasada: wierzchowiec = komplet koń + uprząż

**Para marszowa** to jeden koń i jedna uprząż, która do niego pasuje (`SaddleLaw.Fits`: ta sama rodzina, a samo siodło pasuje do każdego konia).

- **Do pary nie wchodzą:** siodła transportowe (`CampCart.IsPackSaddle`), wielbłądy i smoki spoza `Stables.IsPlainMount`, zwierzęta juczne. [K: funkcje istnieją]
- **Wymóg pary** = max(Difficulty konia, Difficulty uprzęży). Porównujemy go z Riding żołnierza (`ItemReq`). [K]
- **Jedna funkcja** `MarchPairs.Of(party)` daje jeden rekord. Czytają go wszyscy: korekta prędkości gry, HorsePace, MarchPace (sufit kolumny i "riding double"), PackMarket (udźwig), KitPreview, waga i HUD. Nikt inny nie liczy "kto jedzie" po swojemu.

### 1.1 Skąd biorą się konie i uprzęże (dwa źródła)

| Kto | Źródło koni i uprzęży | Dlaczego |
|---|---|---|
| Jazda i konni łucznicy (koń we wzorcu) | **Zbrojownia DTE**, czyli to, co liczy KitPreview (`Rode`/`Saddles`) | Tam DTE trzyma ich kit i stamtąd wydaje go w bitwie |
| Piechota | **Ładunek** (`MobileParty.ItemRoster`) | Stamtąd liczy gra (`num10`) i dzisiejsze `UnderPacks` |
| Jeźdźcy bez konia (`Unhorsed` z HorsePace) | Jak piechota: ładunek | Wchodzą do puli piechoty, patrz 1.3 |
| Bohaterowie (gracz, towarzysze) | **Własne Equipment**, poza pulą par | Liczeni osobno, jak dziś `RidersHero` w HorsePace |

Otwarte: czy piechur może dostać **wolnego** konia i uprząż ze zbrojowni (sztuki, których KitPreview nikomu nie wydał). Dziś HorsePace krok 1 robi tak tylko dla AI. → **[D11]**. Propozycja: tak, ale dopiero po jeździe, i wtedy taka para też jest "na ludziach" (punkt 3).

### 1.2 Dobór par (algorytm)

Chciwe "najtrudniejsza para do najlepszego jeźdźca" nie daje maksimum. Robimy tak:

1. **Parowanie koń + uprząż w obrębie rodziny** (`SaddleLaw.Fits`). Uprzęże rodzinne idą najpierw do koni swojej rodziny; **samo siodło (uniwersalne) przydzielamy na końcu**, żeby nie "zużyć" go na koniu, który miał własną uprząż. W obrębie rodziny łączymy tak, by wymóg pary był jak najniższy (łatwy koń z łatwą uprzężą).
2. **Wymóg pary** = max(Difficulty konia, Difficulty uprzęży).
3. **Dwa wskaźniki:** pary rosnąco według wymogu, ludzie rosnąco według Riding. Każdy człowiek bierze najłatwiejszą parę, którą umie poprowadzić. To daje największą liczbę jadących.
4. Najpierw jazda ze swojego źródła (zbrojownia), potem piechota z ładunku.

Ranni: **[D9]**. Gra liczy rannych w `num` i `num5`; KitPreview liczy tylko zdrowych; `Riders()` liczy z rannymi. Propozycja: ranny potrzebuje pary albo miejsca na wozie (CampCart); jeśli nie ma ani jednego, idzie pieszo w liczeniu jak dziś gra.
Jeniec na luzaku: **[D2]**. Propozycja: też potrzebuje pary.

### 1.3 Korekty prędkości (bez przepisywania modelu gry)

Wszystkie jako **różnica** (nasz wynik − wynik gry), tymi samymi wzorami co gra, z mokro −30% i perkami (`:140-146`). [K: wzory znane]

| Wpis | Co robi | Uwaga |
|---|---|---|
| "Footmen on horses: no harness or skill" | Zastępuje liczbę `num10` gry liczbą par piechoty | **Zastępuje** dzisiejszy wpis HorsePace "Footmen on horses (riders without horses)"; `Unhorsed` wchodzą do puli piechoty. Inaczej ten sam człowiek dostałby +0.15 dwa razy. |
| "Riders without harness" | Jeździec, który ma konia, ale nie ma pasującej uprzęży ani Riding: zdejmuje "Cavalry" (−0.3 × n/N, mokro −30%) i przechodzi do puli piechoty | Wzór jak dzisiejsze "Riders without horses". Wymagane przez D6, bo gra liczy jazdę z `NumberOfMenWithHorse` według wzorca. |
| "Herding: horses without riders" | **Obowiązkowa.** Gra liczy stado jako `herdSize += max(0, mounts − num10)` (`:175`) z własnego `num10`. Koń bez uprzęży dziś w grze "jedzie", po zmianie idzie do stada. | Liczyć przez `GetHerdingModifier` (wzór ArmyReview.March:362) jako różnicę wobec gry, z perkiem Shepherd. |
| "Footmen on horses (armoury)" (HorsePace) | Zostaje dla AI | Dla gracza dziś `Free = 0` [P: potwierdzić w HorsePace krok 1]. Sprawdzić, czy partie AI doczepione do armii gracza nie dublują się przez `includeFollowers`/`AttachedParties`. |
| "Horse quality: spare horses" | Liczy jakość z koni **w parach piechoty**, nie z najlepszych luzaków | |
| MarchPace.Cap i sufit 311d2 | Z par | |
| MarchPace "riding double" (RideDoubleFewFootmen) | Zostaje. Warunek `prisoners <= mounts` i liczba `riders` biorą jeźdźców z par, nie ze wzorca | Podwójna jazda nie wymaga drugiej pary |

**Jazda bez uprzęży w bitwie** (DTE daje konia nawet bez uprzęży, `:839`) zostaje bez zmian. Na marszu taki jeździec idzie pieszo. → **[D6]**
**Riding uprzęży u jazdy w bitwie** (K5) to osobny, późniejszy etap w Mends. → **[D7]**

**Na morzu** (`isCurrentlyAtSea`) MarchPairs = 0 i żadna z tych korekt nie działa: na statku nikt nie jedzie.

Zasięg na start: gracz i partie jego rodu (partie rodu bez KitPreview: pary tylko z ładunku). Lordowie AI: **[D3]**.

### 1.4 Przykład (ilustracyjny, prawdziwe liczby z E0)

Oparty na zrzucie: 46 ludzi = 2 jazdy + 44 piechurów. Ładunek: 43 koni (wynika z +0.67) i np. 39 uprzęży. Zbrojownia: 2 konie + 2 uprzęże jazdy.

- Jazda: 2 pary ze zbrojowni. Cavalry bez zmian.
- Piechota: 43 konie, 39 uprzęży. 3 uprzęże wymagają Riding 100, najlepszy piechur ma 80. Par piechoty: **36**.
- Jedzie 36 piechurów, pieszo 8: 1 bez konia (44 ludzi, 43 konie), 4 bez uprzęży (43 konie, 39 uprzęży), 3 z za niską umiejętnością.
- "Footmen on horses": dziś 0.15 × 43/46 × 4.8 = **+0.67**, po zmianie 0.15 × 36/46 × 4.8 = **+0.56**; wpis korekty **−0.11**.
- Herding: gra liczy 43 − 43 = 0 koni w stadzie; po zmianie 43 − 36 = **7** wolnych koni w stadzie → wpis korekty Herding.

---

## 2. Waga: uprzęże par nie ważą

- **Koń** waży 0 już w grze. [K]
- **Uprząż w parze piechoty** (leży w ładunku) dostaje wpis "Harness on ridden horses" w `CalculateTotalWeightCarried`, czyli minus jej waga.
- **Uprzęże jazdy** leżą w zbrojowni; ich wagę obsługuje punkt 3 (są "na ludziach", nie ważą).
- **Waga przez model, nie surowa.** Gra liczy wagę przez `InventoryCapacityModel.GetItemEffectiveWeight(..., isCurrentlyAtSea)` (`:109`), a NavalDLC owija model. Każde odjęcie i dodanie liczymy tym samym wywołaniem. **Na morzu zwolnienie wagi uprzęży wyłączone.**
- **Uprząż bez konia albo nie do tego konia** waży jak dotąd.
- **Usuwamy jednocześnie** dopisek do udźwigu "Harness worn by spare horses" (`PackMarket.cs:420-425`), inaczej uprząż liczyłaby się dwa razy. [K]
- **Zasięg jak dziś:** `HarnessPostfix`/`HarnessOne` obejmuje MainParty **i partie rodu gracza**. Nowy wpis musi mieć ten sam zasięg, inaczej partie rodu tracą zwolnienie (regresja).
- **Klucz MCM:** nie zmieniamy znaczenia `HarnessOnSpareHorses` (stara wartość z `Armoury.json` Jeffa nadpisze nową). Nowy klucz np. `RiddenHarnessWeightless`, stary wycofany; potem `tools/gen_mcm.py`.

### 2.1 Udźwig od konia (każdy koń raz, według roli)

| Rola konia | Udźwig | Skąd |
|---|---|---|
| W parze piechoty, koń w **ładunku** | **0 dopłaty** | Gra już daje 20 kg za każdego konia w ładunku (`NumberOfMounts`). Dopłata liczyłaby się dwa razy. |
| W parze piechoty, koń w **stajni** (dopiero E6) | RiderHorseCarryKg 20 | Bo gra przestaje go liczyć |
| W parze jazdy (zbrojownia) | RiderHorseCarryKg 20 | Dziś z `PackMarket.Riders()` (wzorzec, z rannymi). W E3 przełączamy na pary jazdy z MarchPairs. Ranni: zależnie od D9. |
| Koń bez pary pod jukami | SpareHorseCarryKg 60 (gra 20 + nasze 40) | Bez zmian. Patrz D5. |
| Zwierzę juczne | 100 kg | Bez zmian |
| Para z siodłem z sakwami | Wpis "Saddled spare horses (saddlebags)" zostaje, tylko dla par pod człowiekiem | Perk512 |

`UnderPacks` przełączamy na pary: koń w parze nie niesie juków, koń bez pary niesie.

**[D5] opisane poprawnie:** dziś koń bez człowieka **jednocześnie** dostaje +40 kg pod jukami (`UnderPacks`) i jest w stadzie gry. To nie jest alternatywa. Propozycja: zostaje jedno i drugie (realizm: luzak z jukami i tak trzeba prowadzić w stadzie). Nie zabieramy po cichu +40 kg.

**Przykład:** 36 par piechoty z uprzężą po ok. 12 kg = ok. **430 kg** mniej ładunku. Przy 4790/6260 kg kara "Cargo within capacity" wyraźnie spada, a udźwig nie rośnie (usunięty dopisek).

---

## 3. Stash: przedmioty ważą (zamknięcie exploitu)

- **Nowy wpis** w `CalculateTotalWeightCarried`: "Troops' armoury (unused)" = waga zbrojowni DTE gracza **minus** waga tego, co jest "na ludziach".
- **Jedna lista "na ludziach"** dla Escrow (ukrywanie), wagi i HUD. Dziś Escrow (`QuartermasterEscrow`) bierze ją z `QuartermasterLaw.CountNeeds()`/`WornFor` (wzorzec, `QuartermasterLaw.cs:1418`), a nie z KitPreview. Gdyby waga liczyła się z `KitPreview.Given`, a ukrywanie z CountNeeds, w oknie byłyby sztuki ukryte, a ważące, albo widoczne, a nieważące. Do wyboru przed E4 → **[D12]**:
  - (a) przełączyć Escrow na KitPreview (regresja kwatermistrza do sprawdzenia), albo
  - (b) liczyć wagę z CountNeeds.
  Propozycja: (a), bo KitPreview to to, co DTE naprawdę wyda, i to samo pokazuje pasek DTE.
- **Modyfikatory:** `KitPreview.Given` jest kluczowane po `ItemObject`, bez modyfikatora (`Dictionary<ItemObject,int>`). Odejmujemy wagę "na ludziach" po **najlżejszym** egzemplarzu danego przedmiotu, żeby w razie wątpliwości było raczej za ciężko niż za lekko. [P]: czy modyfikator w ogóle zmienia `GetEquipmentElementWeight`; jeśli nie, problem znika.
- **Konie** i tak ważą 0. **Uprzęże par piechoty** zwolnione w punkcie 2.
- **Skutek:** przeniesienie zbroi z ładunku do stash nie zmienia wagi. Liczba w oknie ekwipunku to `MobileParty.TotalWeightCarried` (`SPInventoryVM.cs:3968`), więc zawiera też stash. [K]

### 3.1 Liczenie przy otwartym ekranie (bez sprzeczności)

Test E4 ogląda się właśnie przy otwartym ekranie, więc liczba musi się zgadzać od razu, a nie dopiero po zamknięciu.

- **Zakaz przenoszenia rosterów** przy otwartym ekranie zostaje.
- **Liczenie jest dozwolone:**
  - waga zbrojowni = suma wag `ArmyArmory.Armory` (tanie O(n), przy każdym wołaniu, na wątku głównym);
  - przy aktywnym Escrow doliczamy `_held` (te sztuki są wtedy poza `Armory`);
  - waga "na ludziach" z pamięci (KitPreview 10-17 ms, nie przy każdym wołaniu);
  - pary piechoty liczą się z rosteru tanio (bez KitPreview), więc idą od wersji ładunku przy każdej zmianie.
- **`UpdateVersionNo`** (wzór `MoneyWeight.cs:31-32`; gra pamięta wagę do zmiany wersji, `MobileParty.cs:896-906`): podbijamy **tylko przy zmianie wyniku, nigdy w postfiksie**. Podbicie wersji ładunku unieważnia klucze HorsePace, MarchPace i PackMarket, więc trzeba sprawdzić, że nie powstaje pętla: przeliczenie → wersja → przeliczenie.

### 3.2 Reszta

- **Bez podglądu KitPreview** (np. Own Kit Only wyłączone): cała zbrojownia waży jak ładunek. Bezpieczny kierunek.
- **Kit rannych:** **[D4]**. Propozycja: nie waży (ranny ma kit przy sobie albo na wozie). Wymaga, żeby podgląd liczył też rannych; dziś liczy tylko zdrowych [K].
- **Własność (MenPurse):** rzeczy ludzi w stash też ważą (wszystko wiezie oddział). Podpowiedź wagi rozbija "Troops' armoury: yours X kg, men's Y kg". → **[D13]** (propozycja: ważą).
- **Pierwsze wczytanie po wgraniu E4:** jeśli stash był pełny, waga skoczy i oddział zwolni. Jednorazowy komunikat po angielsku: "Your troops' armoury now counts toward cargo weight: +X kg" i linia w logu.
- **Klucz MCM:** nowy, np. `ArmouryHasWeight`.
- **Zbrojownie lordów AI dalej ważą 0:** **[D3]**. To świadoma asymetria (AI z tym samym taborem jedzie szybciej niż gracz) i trafia do "Ryzyko" w CHANGELOG.

---

## 4. Kto co używa: widok i osobna stajnia piechoty

### Co się dzieje dziś [K]
- DTE zdejmuje sztukę z zapasu dopiero przy wystawieniu agenta w bitwie. Na mapie wszystko leży w `ArmyArmory.Armory`.
- Kwatermistrz (QuartermasterEscrow) na czas otwarcia stash chowa to, co ludzie noszą (według CountNeeds), i zwraca przy zamknięciu. Obejmuje to też konie i uprzęże.
- Luzaki piechoty leżą w ładunku razem z wolnymi końmi.

### Docelowy wygląd
| Gdzie | Co widać |
|---|---|
| Kit jazdy i konnych łuczników (koń, uprząż, zbroja, broń) | **Znika z widoku stash**, bo jest "na ludziach" (Escrow, z listą według D12). Fizycznie zostaje w zbrojowni DTE, żeby rozdzielacz DTE w bitwie go widział. Nie waży. |
| Pary piechoty | **Osobna stajnia "Footmen's mounts"**: własny roster Armoury, otwierany jak stash, opcją w menu miasta i obozu. Wiersze: koń, uprząż. Podpowiedź: "Ridden by: 12 x Northern Spearman, 4 x Levy". Nic nie znika, wszystko widoczne, nie waży. |
| Ładunek (okno handlu) | Tylko **wolne** konie i wolne uprzęże. Można je sprzedać, objuczyć albo oddać. Par ze stajni nie sprzedaje się wprost; trzeba je wyjąć. |
| Stash (zbrojownia DTE) | Tylko **niewydany** zapas. Waży według punktu 3. |

### Zasady stajni piechoty
- **Kiedy przenosimy:** tylko w bezpiecznych chwilach: przy zamknięciu okna ekwipunku lub stash, raz na dobę, po bitwie, po werbunku i po stratach. **Nigdy przy otwartym ekranie i nigdy co klatkę.**
- **Pierwszeństwo jazdy:** jeśli jeździec straci konia, przy następnym przeliczeniu para wraca ze stajni do zbrojowni DTE.
- **Wyjęcie pary przez gracza** dozwolone. Po zamknięciu okna przydział liczy się od nowa.
- **Zapis:** (StringId, modyfikator, liczba). Modyfikatora nie gubimy, w przeciwieństwie do DTE (`ArmyArmoryBehavior.cs:371-379`). [K: wada DTE]
- **Niewola:** stajnia ginie tak jak ładunek partii. **[D8]**
- **Udźwig:** koń w stajni wypada z `NumberOfMounts`, więc gra przestaje dawać za niego 20 kg; wtedy dopłata RiderHorseCarryKg 20 (tabela 2.1).

### Ostrzeżenie
Konie wyjęte z ładunku znikają grze z `NumberOfMounts`. Czytają to m.in.: pasza, HorseFate, PackMarket (w tym `HarnessOne` i `UnderPacks`), MarchPace, HorseState, HorsePace (pole `MountsV`), ArmyReview, Perk512, RogueryXp28, VillageHerds, TwinsCrossing, CitySack.Goods, oraz sam udźwig gry. Każde miejsce trzeba przejrzeć. Najważniejsza jest pasza: konie w stajni dalej jedzą. To największa powierzchnia regresji w projekcie, dlatego ten etap idzie na końcu. Wcześniej jest **widok wirtualny**: te same podpowiedzi "Ridden by" na sztukach w ładunku, bez przenoszenia.

Wybór: tylko widok wirtualny czy fizyczna stajnia → **[D1]**. Jeff chce stajni; wirtualny proponuję jako etap pośredni.

---

## 5. HUD (dolny pasek mapy)

**Źródło liczb:** rekord MarchPairs, ten sam co korekty prędkości. Nigdy `NumberOfMounts` (liczby gry i nasze będą się różnić).

### Mianownik uprzęży → [D10]
Przykład Jeffa "23/31 i 21/31" ma wspólny mianownik 31. Najprostsze czytanie: konie pod ludźmi / wszystkie konie oraz uprzęże na koniach / **wszystkie konie**. Różnica 23 i 21 to wtedy jeźdźcy na koniu bez uprzęży (jazda DTE w bitwie). Dwie wersje:

| Wersja | Horses | Harness | Co mówi Harness |
|---|---|---|---|
| (a) jak u Jeffa | jadące / wszystkie konie | uprzęże na koniach / **wszystkie konie** | ilu koniom brakuje uprzęży |
| (b) | jadące / wszystkie konie | uprzęże na koniach / **wszystkie uprzęże** | ile uprzęży leży nieużywanych |

Przy D6 = "nie" (bez uprzęży nikt nie jedzie) licznik Horses i Harness jest równy (każda para to jeden koń i jedna uprząż), a różnica jest tylko w mianowniku. Propozycja: **(a)**, bo tak to rozumie Jeff, a nieużywane uprzęże pokazuje podpowiedź.

### Pozycje (przykład z 1.4: 38 par, 45 koni wierzchowych, 41 uprzęży, 5 jucznych, 7 wolnych koni w stadzie)

| Pozycja | Wartość (a) | Znaczenie |
|---|---|---|
| Horses | **38/45** | konie pod ludźmi / wszystkie konie wierzchowe (ładunek + zbrojownia) |
| Harness | **38/45** (w wersji b: 38/41) | uprzęże na koniach / mianownik według D10 |
| Pack | **5/39** | juczne / limit Herding liczony z par: ludzie − bydło − wolne konie (46 − 0 − 7 = 39) [P: dokładny wzór z `GetHerdingModifier`] |

"Pack" pokazuje to, o co prosił Jeff: ile jucznych zanim będą kary za herding. Nasz MarchPackAllowance (0.25, czyli ceil(46 × 0.25) = 12) działa tylko wtedy, gdy nikt nie idzie pieszo, więc idzie **tylko do podpowiedzi** jako "baggage pace".

### Podpowiedź (angielski, przykład)
```
Men mounted 38/46 (8 on foot: 1 no horse, 4 no harness, 3 Riding too low)
Cavalry 2/2  |  Footmen 36/44
Horses: 38 ridden, 7 free (in herd)
Harness: 38 worn, 3 unused (need Riding 100)
Pack animals 5/39 before herding penalty (46 men - 7 free horses)
Baggage pace: 12 pack animals allowed while all men ride
Ridden by: 12x Northern Spearman (Riding 40) ...
```

### Technika
- Trzy `MapInfoItemVM` dopisane jak pobożność BK: postfiks RefreshValues dodaje, postfiks UpdatePlayerInfo odświeża. [K: wzór istnieje]
- Obiekty trzymane w polu, nie dodawane drugi raz (lekcja z `BkPietyBar.cs`).
- Liczby czytane z pamięci MarchPairs. Nic nie liczymy w UI.
- Pole tekstu: MaxWidth 52 przy FontSize 20. [K: limit / P: czy "38/45" się mieści]
- "Horses" w górnym rzędzie, "Harness" i "Pack" w dolnym, rozwijanym. Czy górny rząd pomieści jeszcze jedną pozycję: [P], sprawdzić na ekranie.
- Ikony: istniejące `VisualId` (`troops`, `speed`) przez `SetOverriddenVisualId`. Własnego pędzla nie robimy (BK podmienia cały pędzel `MapBar.Right.Icons`). [P]: czy podmiana VisualId działa bez wpisu w pędzlu.

---

## 6. Kolejność wdrożenia (jedna zmiana = jeden test)

| Etap | Co | Test (dowód) |
|---|---|---|
| **E0** | Sama diagnostyka. Linia doby w Armoury.log: "Pary marszowe (gracz)": konie i uprzęże z ładunku i ze zbrojowni osobno, pary jazdy i piechoty, braki umiejętności według typu, wolne konie w stadzie, porównanie z `num10` i stadem gry. Zachowanie gry bez zmian. | Log ręcznie zgodny ze stanem oddziału. Z niego przeliczamy przykład 1.4 i HUD. |
| **E1** | `MarchPairs` i HUD (tylko wyświetlanie) | HUD = linia logu. Brak duplikatów po wczytaniu i po zmianie ekranu. |
| **E2** | Prędkość: korekta Footmen (zastępuje "(riders without horses)"), "Riders without harness", Herding (obowiązkowo), "Horse quality" z par, MarchPace.Cap, sufit 311d2, riding double z par | Podpowiedź PARTY SPEED: Footmen i Herding = HUD = linia logu. Próbki "Konie a tempo (HQ)" i ArmyReview.Test (K9). |
| **E3** | Waga uprzęży par (przez model, bez morza, gracz + ród), usunięcie dopisku udźwigu, `UnderPacks` i `Riders()` na pary, nowy klucz MCM | Okno ekwipunku: ładunek spada o wagę uprzęży par, udźwig nie rośnie (poza zmianą Riders na pary). Partia rodu: waga jak przed zmianą lub mniejsza. |
| **E4** | Waga stash (exploit), jedna lista "na ludziach" (D12), komunikat jednorazowy, nowy klucz MCM | Przy **otwartym** ekranie: zbroja ładunek → stash → ładunek, waga stoi. Wydanie kitu ludziom zmniejsza wagę. Brak pętli VersionNo w logu. |
| **E5** | Widok wirtualny "Ridden by" w ładunku i w "Review the army" | Podpowiedzi zgodne z HUD |
| **E6** | Stajnia "Footmen's mounts" (po D1, D8). Podetapy: 6a roster i zapis z modyfikatorami, 6b przenoszenie w bezpiecznych chwilach, 6c przegląd wszystkich czytelników `NumberOfMounts` (pasza!) i udźwig 20 kg, 6d pierwszeństwo jazdy | Zapis i wczytanie z modyfikatorami. Bitwa: jazda ma konie. Pasza i udźwig bez zmian. |
| **E7** (opcja) | Riding uprzęży u jazdy w bitwie (Mends, K5) | Log SkillLawWard |

Po każdym etapie: wpis w CHANGELOG (z "Ryzyko / co sprawdzić"), nowe ustawienia przez `tools/gen_mcm.py`, po zmianie domyślnej sprawdzić `Armoury.json` Jeffa.

---

## 7. Ryzyka i co sprawdzić

- **Podwójne liczenie:**
  - korekta Footmen zastępuje "Footmen on horses (riders without horses)" i wlicza `Unhorsed`;
  - "Footmen on horses (armoury)" tylko dla AI; sprawdzić AI w armii gracza (`includeFollowers`/`AttachedParties`);
  - E3 usuwa "Harness worn by spare horses";
  - udźwig 20 kg dla par piechoty z ładunku = 0 (gra już daje).
- **Ten sam cel łatki:** MoneyWeight (521), E3 i E4 patchują `CalculateTotalWeightCarried`. Jeden postfiks z kilkoma wpisami, `DeclaredMethod` na klasie deklarującej, jawne Priority. NavalDLC owija Default, więc postfiks na Default działa. [K]
- **Wydajność:** KitPreview 10-17 ms z pamięcią po kluczu; liczony przy zmianie VersionNo ładunku, zbrojowni i składu, nigdy co klatkę. Pary piechoty i suma wagi zbrojowni są tanie. Wątki tiku i modele prędkości tylko czytają pamięć. [P]: czy model prędkości bywa wołany spoza wątku głównego.
- **Kaskada VersionNo:** podbicie tylko przy zmianie wyniku; sprawdzić brak pętli.
- **CTD i UI:**
  - żadnego przenoszenia rosterów przy otwartym ekranie (liczenie wolno);
  - żadnego `SwitchToMenu` z menu oczekiwania, opcja stajni otwiera ekran jak DTE;
  - HUD bez powtórnego dodawania pozycji;
  - żadnego globalnego wyłącznika przy wyjątku: liczymy potknięcia.
- **DTE:**
  - zbrojownia ginie częściowo w niewoli (`ArmyArmoryBehavior.cs:313-350`);
  - zapis DTE gubi modyfikator;
  - pary piechoty w stajni są poza rozdzielaczem DTE (piechota nie dostanie koni w bitwie, dobrze; jazda bez konia nie sięgnie po nie sama, stąd 6d).
- **Gra:** `NumberOfMounts` przy dodawaniu pomija konie z modyfikatorem, a przy przeliczeniu je liczy (`ItemRoster.cs:441-447` wobec `:564-576`). MarchPairs liczymy z rosteru sami. [K]
- **Asymetria gracz/AI:** dopóki AI nie ma par ani wagi zbrojowni (D3), lordowie AI jadą szybciej niż gracz z tym samym taborem. Wpisać do CHANGELOG.
- **Czyszczenie stanu:** pamięć MarchPairs, wagi stash, flaga komunikatu i wpisy HUD zerowane przy nowej kampanii i przy wczytaniu.
- **Przeciążenie po E4:** komunikat jednorazowy + linia w logu (punkt 3.2).
- **Niesprawdzone [P]:**
  - czy modyfikator zmienia wagę przedmiotu;
  - dokładny wzór limitu Herding do HUD;
  - `Free = 0` dla gracza w HorsePace krok 1;
  - szerokość paska i podmiana ikony;
  - wątek wołania modelu prędkości.
- **Rozstrzygnięte w wersji 2 (było [P]):**
  - `PackMarket.Riders()` liczy konia z pierwszego ekwipunku bojowego wzorca, z rannymi (nie ze zbrojowni);
  - Escrow bierze "noszone" z `QuartermasterLaw.CountNeeds()`/`WornFor` (`QuartermasterLaw.cs:1418`), nie z KitPreview.

---

## 8. Decyzje dla Jeffa

| Decyzja | Pytanie | Moja propozycja |
|---|---|---|
| **D1** | Stajnia fizyczna (E6) czy tylko widok wirtualny | Najpierw wirtualny, stajnia potem |
| **D2** | Czy jeniec na luzaku potrzebuje uprzęży | Tak |
| **D3** | Czy pary, umiejętności i waga zbrojowni dotyczą też lordów AI | Później (świadoma asymetria do tego czasu) |
| **D4** | Czy kit rannych w stash waży | Nie waży |
| **D5** | Koń bez pary: dziś jest i w stadzie, i niesie juki (+40 kg). Zostawić oba? | Tak, oba zostają |
| **D6** | Czy jazda bez uprzęży liczy się na marszu jako konna | Nie, jedna reguła (wpis "Riders without harness") |
| **D7** | Riding uprzęży u jazdy w bitwie | Osobny etap później |
| **D8** | Czy stajnia piechoty ginie w niewoli | Tak jak ładunek |
| **D9** | Czy ranny potrzebuje pary (albo miejsca na wozie) | Tak: para albo wóz, inaczej pieszo |
| **D10** | Mianownik uprzęży w HUD: wszystkie konie (jak 23/31, 21/31) czy wszystkie uprzęże | Wszystkie konie |
| **D11** | Czy piechur może dostać wolnego konia i uprząż ze zbrojowni | Tak, po jeździe |
| **D12** | Jedna lista "na ludziach": Escrow przełączyć na KitPreview czy wagę liczyć z CountNeeds | Escrow na KitPreview |
| **D13** | Czy rzeczy ludzi (MenPurse) w stash ważą | Tak, z rozbiciem "yours / men's" |

Pliki źródłowe rozpoznania (tylko odczyt, w scratchpadzie sesji 15.11):
- `nscal\Armoury\src` (PackMarket.cs, HorsePace.cs, KitPreview.cs, QuartermasterLaw.cs, MarchPace.cs, ArmyReview.March.cs, MoneyWeight.cs)
- `dte` (ArmyArmory.cs, ArmyArmoryBehavior.cs)
- `dek\tw-campaign` (DefaultPartySpeedCalculatingModel.cs, DefaultInventoryCapacityModel.cs, MobileParty.cs, ItemRoster.cs, SPInventoryVM.cs)
