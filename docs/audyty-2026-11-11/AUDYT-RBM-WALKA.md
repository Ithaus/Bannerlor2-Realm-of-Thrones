# AUDYT RBM - WALKA (pancerze, bron, sila ciosu, rany) - 11.11

Tylko czytanie: kod RBM 4.3.5, silnik 1.4.8, ROT, nasze mody (worktree n367c), ustawienia Jeffa, zrzut przedmiotow, logi trafien z 9-11.11.
Gra nie byla uruchamiana. Wszystkie liczby HP to przeliczenie wzorow, nie pomiar - poza statystykami z logow, gdzie to zaznaczono.
Ustawienia RBM Jeffa (`Documents/Mount and Blade II Bannerlord/Configs/RBM/config.xml`) to wartosci fabryczne.

## 1. WERDYKT

Zasady RBM maja sens i nie sa zbyt uproszczone w rdzeniu. Pancerz dziala jak prog: cios slabszy od progu zostawia tylko siniak, mocniejszy przepuszcza nadwyzke. Tak wlasnie wychodzi z badan nad zbrojami, a zwykle "pancerz odejmuje procent" jest gorsze. Osobno liczone sa ciecie, klucie i obuch, osobno twarz, pacha, szyja, rece i nogi; luki bija energia z naciagu i trafiaja w realne przedzialy (91 J dla 100 funtow); zmeczenie w plycie rosnie prawie tak jak w pomiarach.

Klopot nie lezy w zasadach, tylko w danych, ktore do nich wkladamy. ROT mnozy pancerze 2.1-2.4 raza ponad skale, na ktora RBM byl strojony: przeszywanica chroni jak dobra kolczuga, kolczuga jak plyta, a plyta jest poza skala. Skutek: zadna bron reczna nie przebija tulowia rowiesnika i kazdy pada po 9-12 ciosach miecza niezaleznie od tego, co nosi. Jednoczesnie ROT nie daje helmom oslony szyi, a zbrojom daje slabe barki - wiec ludzie w plycie gina od trafien w szyje i barki (w logu: szyja mediana 101 HP na trafienie, glowa 11).

Sa tez bledy. Nasz wlasny: zuzyta bron (stan Zmasakrowany lub Wrak) moze wedlug kodu zadawac zero obrazen, nawet w nagiego - nie widziane jeszcze w grze. Nasz drugi: latka chroniaca tarcze przed strzalami tnie do 1% takze oszczepy, topory rzucane i strzaly olbrzymow. Blad danych ROT: mloty dwureczne (Roberta, Gendry'ego) i maczuga olbrzyma sa zapisane jako topory i bija jak trzonek - trzy razy slabiej niz buzdygan jednoreczny.

Zbyt uproszczone: o tym, czy powalony zolnierz umiera czy jest ranny, decyduje tylko jego poziom i chirurg, nie cios; lezacy ma pancerz slabszy ledwie o cwierc; strzaly z szerokim grotem (wiekszosc lucznikow ROT) prawie nie rania nikogo w pancerzu; oszczep jest na pancerz slabszy od strzaly; kon w ladrach plytowych ROT jest odporny na luki.

## 2. TABELA

Zrodla historyczne: `C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/bad4/zrodla-pancerz-bron.md` i `C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/bad4/zrodla-rany-walka.md`. Liczby Williamsa sa tam cytowane z drugiej reki albo z pamieci - nie byly sprawdzane w ksiazce.

| Zjawisko | Jak jest w RBM / w grze Jeffa | Jak bylo naprawde | Ocena |
|---|---|---|---|
| Zasada pancerza | Prog zalezny od typu broni; ponizej progu tylko uraz tepy, powyzej nadwyzka | Prog energii, nie procent (Williams 2003) | Dobrze |
| Sila ciecia | Miecz 1h przy pelnym zamachu 65 (rekrut) do ok. 120 (umiejetnosc 100); umiejetnosc dodaje 3-23 do fizyki 12-16 | Miecz/topor 1h 60-130 J, 2h do 200-250 J; umiejetnosc daje celnosc, nie energie | Zakres dobry; umiejetnosc podwaja sile - uproszczenie |
| Sila pchniecia | W dzulach, sufit 180 J (1h) / 250 J (2h); wlocznia T4 ok. 138 | Wlocznia 1h 30-60 J, 2h 50-100 J (szacunek) | Zawyzone ok. 2x, ale progi tez sa wysokie |
| Przeszywanica / skora | ROT: mediana 61 (gambeson 10 kg); prog ciecia 233, wloczni 167; miecz 11 HP | 16 warstw lnu: ostrze 80 J, grot 50 J; jack 26 warstw: 200 J | Za mocna ok. 2x (dane ROT, mnoznik 2.2) |
| Kolczuga | ROT: mediana 88; prog ciecia ok. 335; strzala bodkin 88-114 J | Ciecie ponad 200 J; strzala 80-120 J | Strzaly trafione; wrecz nie do ruszenia - zgodne |
| Plyta | ROT: mediana 120; prog strzaly 240-312 J; miecz 7-8 HP, buzdygan ok. 20 | 2 mm: 175 J; nic recznego nie przebija napiersnika | Zasada dobra; wartosc o klase za wysoko |
| Luki | 100 lb + 70 g = 91 J; 180 lb = 151 J; strata 3% na 20 m, 14% na 100 m | 160 lb, 80 g: 110-125 J; ok. 109 J na 25 m (testy Toda 2019) | Dobrze; gora lekko zawyzona; naciag zawsze pelny |
| Strzala szeroka (Cut) | 69% gniazd strzal w wojskach ROT; skora 33: 5 HP, skora 61: 3-4 HP | Lekki luk 40-60 J rani przez przeszywanice | Za slabe |
| Oszczep | Prog 3x pancerz: 162 J daje 10 HP w skore, 8 w kolczuge; pilum 79 / 48 | 100-200 J; przeszywanica - rana pewna, kolczuga - rana prawdopodobna | Zwykly oszczep za slaby; pilum dobrze |
| Szyja | Helmy ROT bez oslony szyi (1 z 245 plytowych); szyja ok. 24; log: 90 trafien, mediana 101 HP | Czepiec kolczy bywa przebijany, obojczyk plytowy trzyma | Blad danych ROT |
| Barki | Ok. 21-26 u wojsk ROT; log: 439 trafien, mediana 45 HP | Naramienniki to pelna plyta | Blad danych ROT, 5x wiecej trafien niz w szyje |
| Rece i nogi | Bez rekawic 98 / 85 / 69% wojsk T1-T3; nogi licza sie w polowie; mnoznik obrazen 0.5-0.7 | Visby: wiekszosc ciec w golenie; Towton: przedramiona; blachy konczyn ok. 1 mm | Sensowne |
| Helm | Log: glowa 345 trafien, mediana 11 HP | Visby: helm zbija rany glowy 8-10x | Dobrze |
| Obuch | Buzdygan przepuszcza 70% urazu, ostrza 25-35%; material pancerza nie wplywa na uraz | Kolczuga prawie nie chroni przed obuchem, plyta rozklada cios | Kierunek dobry, material pominiety |
| Mloty 2h ROT, maczuga olbrzyma | Zapisane jako topor: licza sie jak cios trzonkiem; mlot Roberta ok. 8 HP w plyte, olbrzym ok. 17 | Bron 2h 150-250 J; ciezki uraz przez kazdy pancerz | Blad danych ROT |
| Dobijanie lezacego | Pancerz x0.75 (takze u zatoczonego); wlocznia dalej nie przebija plyty | Powalic, odslonic, dobic w szczeline (Visby: otwory po dobijaniu) | Za slabe |
| Kopia | 154-430 zaleznie od predkosci, z plaskim odcinkiem | 100-250 J realnie przekazane | Gora zawyzona; zostawic |
| Tarcze | ROT: pancerz 1 (3-4 ciosy topora) albo 9 (ok. 27 ciosow); strzala nie przechodzi; u nas pociski 1% | Topor niszczy tarcze w kilka-kilkanascie ciosow; ciezka strzala przechodzi deske i rani reke | Rozrzut danych ROT; nasza latka za szeroka |
| Konie | Ladry plytowe ROT: 82 pancerza, 140 kg; bodkin 3-6 HP w piers; 18% gniazd uprzezy w wojskach | Kon to najlatwiejszy cel lucznikow (Crecy); ladry chronia czesciowo | Za mocne |
| Zmeczenie | Koszt x1.74 dla 42 kg; wyczerpanie po ok. 1.5-2 min walki; pusty pasek: cios i ruch x0.85 | Plyta: chod 2.1-2.3x, bieg 1.9x (Askew 2012); 1-3 min pelnego wysilku | Dobrze |
| Wypoczety ma pancerz x1.3 | Do 70% paska; nasze podwojenie staminy wydluza ten okres ok. 2x | Brak podstaw | Sztuczne; psuje strojenie progow |
| Smierc czy rana | Jeden rzut od poziomu i chirurga: rekrut bez chirurga ginie w 89%, z Medycyna 100 w 47%; obuch zabija jak ostrze | 20-25% rannych umiera w tydzien; ranni do zabitych w starciu 2:1-4:1 (szacunek) | Za proste i za smiertelne |
| Rany bohaterow (nasze) | Czysty rzut 8% / 20%, bez zwiazku z ciosem | Rana zalezy od miejsca i broni | Za proste |
| Zuzyta bron (nasze + RBM) | Wedlug kodu: Zmasakrowany / Wrak moze dac 0 obrazen | - | Blad nasz, niesprawdzony w grze |
| Luki "lore" ROT | Dragonbone i weirwood traca 16% energii wzgledem zwyklego luku | - | Odwrotnie niz w zamysle |

## 3. LISTA POPRAWEK

Jedna zmiana na wgranie. Pomiar: linie `HIT` w `Modules/Armoury/Armoury-*.log` przed i po (mediana HP wedlug czesci ciala i broni), wynik tej samej bitwy testowej.

### A. Samo ustawienie RBM (config.xml, sekcja RBMCombat)

| # | Zmiana | Dla gracza | Ryzyko bitew AI | Koszt | Jak zmierzyc |
|---|---|---|---|---|---|
| A1 | `WeaponTypes/Arrow` i `Bolt`: `ExtraArmorThresholdFactorCut` 2.6 -> 2.0 | Zwykle strzaly rania lekko opancerzonych: skora 33 zmeczony 21 HP zamiast 6; wypoczety dalej 5 | Male; skora 61 i wyzej bez zmian | Zerowy | HIT strzal Cut w tulow |
| A2 | `WeaponTypes/Javelin`: `ExtraArmorThresholdFactorPierce` 3 -> 2 | Oszczep w skore 61: zmeczony 45 HP zamiast 12; kolczuga bez zmian; pila jeszcze mocniejsze | Srednie: harcownicy zyskuja | Zerowy | HIT oszczepow |
| A3 | Pchniecia: `OneHandedPolearm` / `TwoHandedPolearm` 3 -> 2, miecze 3.5 -> 2.5 | Wlocznia znow przebija przeszywanice (ok. 30 HP zamiast 18); kolczuga dalej nie | Srednie: wlocznicy mocniejsi na lekka piechote | Zerowy | HIT pchniec w tulow |
| A4 | `Dagger`: `ExtraArmorThresholdFactorPierce` 3 -> 2 | Sztylet skuteczniejszy na skore i sukno; lezacej plyty nie przebije | Znikome | Zerowy | - |
| A5 | `Global/ArmorThresholdModifier` 1 -> 0.77 | Znosi premie wypoczetego, ale obniza tez progi zmeczonych w calej walce | Duze, globalne | Zerowy | Dlugosc bitwy, straty |

A1 i A2 dzialaja w pelni dopiero po zdjeciu premii x1.3 (C3). A3 to rozwiazanie tymczasowe zamiast B5. A5 odradzam - wole C3. Nie schodzic do 0.6: plyta przestaje trzymac luk 180 lb.

### B. Dane (latki danych w Mends albo XML)

| # | Zmiana | Dla gracza | Ryzyko bitew AI | Koszt | Jak zmierzyc |
|---|---|---|---|---|---|
| B1 | Szyja i barki ROT. Wariant bez ruszania danych: postfiks na pancerz czesci ciala tylko dla szyi i barkow. Wariant danych: helmom plytowym i kolczym `arm` = 0.18-0.25 x `head` | Rycerz nie pada od jednej wloczni w szyje: 130 -> 70 HP; strzala 100 lb w szyje w plycie 43 -> 9 HP | Srednie: ciezka piechota zyje dluzej | Maly; wariant danych musi wejsc po `ArmorSanity`, inaczej najlepszym helmom spadnie `head` | Log: mediana HP szyi (dzis 101) i barkow (45) |
| B2 | Mloty 2h ROT i maczuga olbrzyma: szablon TwoHandedMace albo latka rozrozniajaca rodzimy obuch od ciosu trzonkiem; razem z C5 | Mlot Roberta i olbrzymy bija jak bron obuchowa, nie jak kij | Srednie: Baratheonowie i olbrzymy mocniejsi | Sredni; niesprawdzone, czy czesci zloza sie w nowym szablonie | HIT mlotow; test olbrzyma |
| B3 | Tarcze ROT: pancerz 1 i 9 -> 4-6 | Tarcza nie pada po 3 ciosach topora ani nie wytrzymuje 27 | Male | Maly | Liczba ciosow do rozbicia |
| B4 | Sufit dla ladrow plytowych ROT (body 82 -> ok. 40-50) | Lucznicy znow zatrzymuja szarze, strzelajac w konie | Duze dla jazdy ciezkiej (18% gniazd) | Maly | HIT strzal w konie |
| B5 | Mnoznik skory ROT u zrodla: 2.2 -> ok. 1.1-1.3 | Przeszywanica 61 -> 30-36: wlocznia i miecz rania lekka piechote | Duze: zmienia tiery, receptury, ceny, dobor sprzetu wojsk | Duzy (tor L); musi wejsc przed wczytaniem przedmiotow | Pelny test zbiorczy |
| B6 | Tabela wyjatkow po id: suknie (132 pancerza przy 3 kg), wlosy Dany (150 na glowie) | Suknia nie jest zbroja plytowa | Zadne dla wojsk | Maly | Zrzut przedmiotow |
| B7 | Luki ROT dopisac do podmiany amunicji na bodkiny | Lucznicy ROT skuteczni na kolczuge | Srednie | Maly | HIT strzal |

### C. Nasze latki

| # | Zmiana | Dla gracza | Ryzyko bitew AI | Koszt | Klucz | Jak zmierzyc |
|---|---|---|---|---|---|---|
| C1 | Zuzyta bron: postfiks na wspolczynnik obrazen RBM (mnozenie zamiast odejmowania, zawsze powyzej zera) | Zniszczony miecz bije slabiej, a nie wcale | Poprawia | Maly (tor S) | Bez nowego klucza | Najpierw proba: karta zniszczonego miecza RBM w ekwipunku; cios Wrakiem w nagiego |
| C2 | Oslona tarcz: tylko strzaly i belty, z wyjatkiem amunicji przebijajacej tarcze; spojnie w zuzyciu tarcz | Oszczep i topor rzucany znow rozbijaja tarcze (dzis 33 pkt zamiast 3375); strzala olbrzyma przebija | Srednie: harcownicy zyskuja | Maly | `MissileShieldDamagePercent` + ewentualnie osobny procent dla miotanych | Liczba oszczepow do rozbicia tarczy |
| C3 | Zdjecie premii x1.3 pancerza u wypoczetego (podmiana stalych w kodzie RBM) | Pierwsze minuty bitwy nie sa "odporne"; A1-A3 zaczynaja dzialac | Srednie: bitwy krotsze | Sredni | Nowy klucz, domyslnie wlaczony | HIT z pierwszej minuty kontra pozniejsze |
| C4 | Lezacy (tylko powalony, nie zatoczony): pancerz x0.5 zamiast x0.75 | Dobijanie dziala: wlocznia w lezaca plyte 33 HP zamiast 0 | Srednie: powalony rycerz ginie szybciej | Maly; ten sam postfiks co B1 | Osobny suwak | HIT w lezacych |
| C5 | Stala 1.125 -> 0.1125 dla buzdyganow 2h | -9% dla buzdyganow 2h, -26% dla maula chlopskiego | Znikome (16 przedmiotow) | Maly | - | Razem z B2 |
| C6 | Smierc czy rana zalezna od ciosu (czesc ciala, obuch czy ostrze) w bitwach rozgrywanych | Obuch czesciej rani niz zabija; cios w noge nie zabija | Duze dla strat po bitwie | Sredni; w tym samym miejscu, gdzie juz latamy przezywalnosc, zeby nie liczyc dwa razy; symulacje zostaja przy poziomie i chirurgu | Nowy klucz | Odsetek zabitych do rannych po bitwie |
| C7 | Rany bohaterow zalezne od ciosu | Ciezka rana po ciezkim ciosie, nie z rzutu | Male | Sredni; stan czyscic po misji | Istniejace klucze HeroWounds | Log ran |
| C8 | Krwawienie widzi, czy pancerz zostal przebity | Mniej krwawien przez plyte | Male | Sredni | Klucze Bleed | Niski priorytet |

### Zalecana kolejnosc

1. C1 - jedyny twardy blad, nasz; najpierw tania proba w grze.
2. C2 - nasz blad zakresu.
3. B1 - najlepiej udokumentowane logiem.
4. C3, potem A1 i A2.
5. B2 z C5.
6. C4.
7. B3, B6.
8. Po decyzji Jeffa: B5 (do tego czasu A3), B4, C6, C7.

## 4. CZEGO NIE RUSZAC

- **`ArmorMultiplier` 2 i `ArmorThresholdModifier` 1.** To os calego modelu. Zejscie do 0.6 psuje relacje plyta - luk.
- **Model progowy, strefy twarz / pacha, osobne tory ciecia, klucia i obucha.** Zgodne ze zrodlami.
- **Okno umiejetnosci w sile ciosu.** Uproszczenie, ale zakres 65-120 trafia w realne 60-130 J; ruszenie przestroi wszystko.
- **Kopia** (plaski odcinek i schodek) - gora zawyzona, ale poprawka nie jest warta ryzyka.
- **`MaceBluntModifier` i `BluntTraumaBonus`.** Buzdygan juz jest najlepsza bronia na pancerz.
- **Martwe "czesciowe przebicie" pchniecia w RBM.** Nie dziala i nie szkodzi; nie liczyc na nie.
- **Gole rece i nogi wojsk T1-T3.** To zgodne z polami bitew, nie blad.
- **Tiery pancerzy zatkane na szczycie.** Cecha wzoru RBM; zmiana ruszylaby 55 miejsc w naszym kodzie.
- **`material_type` pancerzy ROT.** Zmiana idzie w ceny, naprawy i dobor sprzetu AI.
- **Przycinanie pancerzy wedlug wagi.** Odrzucone 01.09 ("rzez balansu"); regula "skora ponizej 8 kg" i tak nie trafia w gambesony (10 kg).
- **Odnowa staminy a helmy.** U Jeffa odnowe RBM zastepuje nasza, wiec polowienie za helm nie dziala - nic do naprawy.
- **Ceny.** Liczy je nasz kod, nie RBM; wnioski o cenach RBM sa nieaktualne.

## 5. DECYZJE DLA JEFFA

1. **Twoj lucznik trafia rycerza w plycie w szyje i ten pada od jednej strzaly (od ok. 125 funtow) albo od jednej wloczni.** Czy helm ma chronic szyje, a zbroja barki? Rekomendacja: tak (B1), wariantem bez ruszania danych.
2. **Piechur w przeszywanicy przyjmuje 9-12 ciec miecza i wlocznia go nie przebija - tak samo jak rycerza.** Czy lekka piechota ma byc wyraznie miekksza? To zmiana calej drabiny skor ROT, tej samej klasy co odrzucona 01.09. Rekomendacja: najpierw tanie A3 (progi pchniec), ocenic w grze, dopiero potem B5.
3. **Oszczepnik rzuca w tarcze i nic sie nie dzieje; strzala olbrzyma nie przebija tarczy.** Rekomendacja: naprawic (C2); oszczepy i topory 100%, strzaly i belty zostaja na 1%.
4. **Na poczatku bitwy wszyscy sa o 30% twardsi, a przez nasze podwojenie staminy trwa to dwa razy dluzej.** Rekomendacja: zdjac (C3), z kluczem do cofniecia.
5. **Robert Baratheon bije mlotem trzy razy slabiej niz zwykly zolnierz buzdyganem, olbrzym zadaje plycie 17 HP.** Rekomendacja: naprawic (B2 + C5).
6. **Powalony rycerz lezy, a twoi ludzie klepia go bez skutku.** Rekomendacja: pancerz lezacego x0.5 (C4), tylko dla powalonych.
7. **Lucznicy strzelaja w szarze ciezkiej jazdy i konie w ladrach nic nie czuja.** Czy ladry maja byc do przebicia? Rekomendacja: obnizyc (B4), ale po B1 i C3, bo to mocno oslabia jazde.
8. **Rekrut powalony w bitwie ginie w 9 przypadkach na 10 bez chirurga; cios palka zabija tak samo jak miecz.** Czy o smierci ma decydowac cios? Rekomendacja: tak (C6), ale jako osobny etap - mocno zmienia straty.
9. **Zwykle strzaly (wiekszosc lucznikow) prawie nie rania nikogo w pancerzu.** Rekomendacja: A1 po C3; B7 osobno.
10. **Suknia Daenerys chroni jak zbroja plytowa.** Rekomendacja: tabela wyjatkow (B6). Dothrakowie i Nieskalani (77-99 pancerza przy 4-6 kg) - osobna decyzja; zostawilbym.

## 6. CZEGO NIE SPRAWDZONO

- Niczego w grze. Obrazen wrecz nie ma w zadnym logu poza liniami HIT z 9-11.11.
- Czy blad zuzytej broni wystepuje w grze i czy wojska AI w ogole nosza bron Zmasakrowana lub Wrak.
- Faktycznego pancerza wojsk w bitwie: nasz dobor sprzetu i DTE podmieniaja rynsztunek, tabele sa z XML (T2 w grze nosi raczej 33-48 niz 61, T5 nie nosi plyt 120).
- Rozkladu realnych ciosow: liczby "pelny zamach" to gorna granica.
- Czy czesci mlotow ROT zloza sie w szablonie buzdyganu 2h; czy olbrzymy maja osobne mnozniki.
- Kolejnosci modyfikatorow w grupach zuzycia pancerza (RBM i nasze zuzycie moga sie nakladac - pierwszy krok moze dac od razu -51%).
- HP koni, tabeli kosztow postury dla poszczegolnych broni, obrazen tarcz od ciosow wrecz z mnoznikami postawy.
- Liczb historycznych w oryginalach (Williams, Thordeman) - cytowane z drugiej reki lub z pamieci.
- Liczniki z roznych przegladow roznia sie metoda (np. zwykle strzaly: 164 zestawy / 274 gniazda; zagrozone glownie: 42-44% ciec RBM); rzad wielkosci jest wspolny.
