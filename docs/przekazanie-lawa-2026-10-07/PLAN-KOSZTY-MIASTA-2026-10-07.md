# Plan: koszty w miescie z dniowek historycznych i dobrobytu (07.10)

Decyzja Jeffa 07.10: "stawka robocizny powinna byc zalezna od dobrobytu miasta"; "nie tylko kowala, rowniez wszelkie koszty w danym
miescie powinny byc zalezne od dobrobytu i stawek historycznych"; "na stawkach w naszej grze - nasze stawki sa oparte o stawki historyczne -
wszystko, co dotyczy pieniadza, placenia musi byc spojne".

Zrodlo: spis 60 miejsc w kodzie naszych modow, w ktorych powstaje koszt, placa, oplata, czynsz albo cena uslugi (przeszukanie galezi
n131b-lawa-naprawcza-z-materialem, 07.10). Numery wierszy spisu w nawiasach [n].

## 1. Jedna regula

1. **Tabela dniowek historycznych (juz jest w Settings):** czeladnik / mistrz t1 3 d (`HistMasterWageT1`, `WorkshopWagePerDay`), mistrz
   + 1.5 d na tier (`HistMasterWagePerTier`, platnerz t6 10.5 d), zysk mistrza 25% (`HistProfitPercent`). Brakuje: robotnik niewykwalifikowany
   (Anglia ok. 1300: ok. 1.5 d) i straznik / wartownik - dopisac jako `Hist*` przy pierwszej paczce, ktora ich potrzebuje.
2. **Wskaznik plac miasta:** `MendMaterial.LocalWage(miasto)` = dobrobyt / `TownWageRefProsperity` (4800 = mediana 97 miast w tescie Jeffa),
   w granicach 0.5-1.5. Przy wiekszej liczbie uzytkownikow przeniesc do osobnej klasy (np. `TownWage`), nie kopiowac wzoru.
3. **Kazdy koszt pracy ludzi w miescie** = dni roboty x dniowka z tabeli x wskaznik miasta (+ zysk mistrza, gdy mistrz sprzedaje swoja robote).
   **Kazda oplata za usluge miasta** (wynajem kuzni, dom, organizacja turnieju) = kwota historyczna x wskaznik miasta.
4. **Czego regula nie rusza (z powodem):**
   - ceny towarow na targu - ustala je podaz i popyt (115); dobrobyt juz podnosi popyt (`SupplyDemandRefProsperity`) - drugi raz bylby podwojny;
   - wartosc przedmiotow (`HistCost`) - jedna w calym swiecie, rynek miasta przesuwa cene podaza i popytem;
   - podatki, cla, danina, czynsz od ludnosci jako PROCENT kasy albo dochodu [31-38] - proporcjonalne same z siebie; czynsz od ludnosci
     [31] juz rosnie z dobrobytem (liczba mieszczan = dobrobyt x k) - wskaznik dalby podwojnie;
   - zold wojska [22-23, 27-28] - zalezy od zolnierza, nie od miasta; okupy [18, 48-52] i Bank Zelazny [42] - nie sa kosztem miasta.

## 2. Stan po spisie

| Grupa | Miejsca | Historyczne? | Dobrobyt? | Co zrobic |
|---|---|---|---|---|
| Naprawy u kowali miasta [1-9] | lawa, polki wojska, uprzaz, kwatermistrz, ludzie, AI | TAK (paczka n131b, czesc 2) | TAK | gotowe - paczka "naprawy u kowali miasta" |
| Place i utrzymanie warsztatow [19-21] | 3 d za roboczodzien, 4 d utrzymania na dobe | tak | NIE | x wskaznik miasta |
| Wynajem kuzni [10-12] | oplata za projekt 3 + 2 x tier; karnet dnia BK ok. 200 za dzien | czesciowo | tylko BK | jedna stawka: dzien kuzni = kwota historyczna x wskaznik; karnet BK (ok. 70 dniowek!) do tej samej stawki |
| Budowy [25] | 48 / 24 d za punkt, 75% robota | skala pensow, nie Hist* | NIE | robota z dniowek x wskaznik |
| Zamowienie sprzetu dla ludzi [13] | wartosc x 1.15 | posrednio | NIE | narzut = robota kowala przy zakupie (dni x dniowka x wskaznik) albo zysk mistrza 25% - jedna regula |
| Konie i hodowcy [15], doplata za konia przy werbunku [28] | ceny koni w STAREJ skali gry | NIE | NIE | konie w cenach historycznych - osobna, wieksza paczka (zmiana skali pieniadza) |
| RealisticCaptivity: praca dzienna, warta, domy [16-17, 46] | 1.5 + dobrobyt/5000, 2 + dobrobyt/4000, 4000 + 0.5 x dobrobyt | nie (ale bliskie: ok. 2.5 d / 3.2 d) | tak, wlasnym wzorem | dniowka robotnika / straznika x wskaznik; dom = kwota historyczna x wskaznik; ten sam wskaznik co Armoury (bez kopii wzoru) |
| GrandTourney: oplata gospodarza, pule, wplywy [43-45] | 2000 + 0.5 x dobrobyt, 3000-15000, 200 / lorda | NIE (skala gry) | tak, wlasnym wzorem | kwoty historyczne x wskaznik; nagrody juz historyczne (400-1250 d) - pule spojne z nimi; zwrot 50% przy odwolaniu nie zwraca oplaty (prawdopodobny blad) |
| Czas naprawy (0.6 h / 1.5 h) | - | - | - | zgodny z dniami roboty i rekami kowali miasta (`SmithHours`) - razem z wynajmem kuzni |

## 3. Kolejnosc paczek (jedna zmiana naraz, kazda z proba poza gra i kontrola wg zasady 0)

1. **Naprawy u kowali miasta** - GOTOWE (n131b: 81109ab + b36a6d6, proba 69/69), po paczce kwatermistrza.
2. **Place warsztatow x wskaznik miasta** [19-21] - rusza zysk warsztatow, cene sprawiedliwa (123) i cene kupna warsztatu (116); wymaga autotestu
   40 dob (ceny warsztatow, warsztaty bez zysku, kasy miast).
3. **Wynajem kuzni i czas roboty** [10-12] - jedna stawka dnia kuzni; karnet BK z niej; czas naprawy z dni roboty i rak kowali.
4. **Budowy** [25] - robota z dniowek x wskaznik.
5. **RealisticCaptivity** [16-17, 46] - praca, warta, domy; wskaznik z Armoury przez jedno miejsce (bez drugiego wzoru).
6. **GrandTourney** [43-45] - kwoty w skali historycznej x wskaznik + poprawka zwrotu.
7. **Konie w cenach historycznych** [15, 28] - osobny projekt (zmienia skale cen calej grupy przedmiotow).
8. Zamowienie sprzetu [13] - narzut wedle jednej reguly (przy paczce 3 albo 2).

## 4. Ryzyka do sprawdzenia przy kazdej paczce

- Podwojne liczenie dobrobytu (popyt, liczba mieszczan, rece rzemieslnikow juz od niego zaleza) - wskaznik tylko na STAWCE dniowki, nigdy
  na ilosci.
- Zamknieta ekonomia: kazda placa ma odbiorce (kasa miasta), nic nie powstaje z wiekszego wskaznika.
- MCM Jeffa: nowe klucze nie istnieja w Armoury.json - obowiazuje wartosc z kodu; zmiana domyslnych istniejacych kluczy - sprawdzic plik.
- RealisticCaptivity i GrandTourney to osobne DLL - jeden wzor wskaznika (odczyt z Armoury albo wspolne ustawienie), nie kopia.

## 5. Stan wykonania (07.10, 12:30)

| Paczka | Galaz / commit | Proba | Autotest |
|---|---|---|---|
| 1 naprawy u kowali (lawa, kwatermistrz, ludzie, AI) | n131b 81109ab + b36a6d6 | 69/69 -> w stosie 106/106 | w autotescie 12:15 (naprawy AI 4.06 d / szt.) |
| 2 warsztaty | n131c cb21de2 | 86/86 -> 106/106 | 12:15 OK 40/40, ERROR 0 |
| 3 wynajem kuzni | n131d a9bb933 | 90/90 -> 106/106 | - |
| 4 budowy | n131e 3290b01 | 95/95 -> 106/106 | - |
| 5 niewola (RealisticCaptivity) | n131f 14504a5 | 101/101 -> 106/106 | - |
| 6 turnieje (GrandTourney) | n131g e010a25 | 106/106 | - |
| 7 konie w cenach historycznych | - | - | - (osobny projekt) |
| 8 zamowienie sprzetu (narzut) | - | - | - |

Wpisy: TU\CHANGELOG-wpis.md (1), TU\CHANGELOG-wpis-2-warsztaty.md (2), TU\CHANGELOG-wpisy-3-6.md (3-6). Nic nie wgrane na stale, nic nie wypchniete.

## 6. NA POZNIEJ (Jeff 07.10: "zapisz to na pozniej") - kasy miast w tarapatach: dlugi, pozyczki, podatek nadzwyczajny

Pomysl Jeffa: miasto trzyma rezerwe; gdy ma dlugi - splaca i pozycza, a dobrobyt spada; gdy jest niewyplacalne, samo (nie lord) podnosi
podatki - dobrobyt mieszczan spada, rosnie niezadowolenie.
Co jest dzis: dekret podatkowy lenna BK (lord: niski / zwykly / wysoki / zwolnienie; wysoki kosztuje lojalnosc; renty PopulationLaw 0.7 / 1 /
1.3 / 0); lojalnosc -> bunty miast (gra); kara BK za kase < 20 000 (do -2 dobrobytu dziennie); Bank Zelazny tylko dla lordow; rezerwa miasta
TownRentFloorGold 20 000 dla rent, podatkow korony, cel, skupu u pasera, karawan, skupu domu i utargu turnieju. Brak: dlugow i pozyczek miasta,
podatku nadzwyczajnego miasta.
Szkic (zamknieta ekonomia - bogactwo mieszczan = sakiewki notabli miasta): (1) najpierw wstrzymac wydatki odkladalne (jest); (2) zbiorka
nadzwyczajna od notabli do kasy miasta - koszt: lojalnosc, relacje z notablami, dobrobyt (mniej wydaja); (3) w ostatecznosci pozyczka (bogaci
notable albo Bank Zelazny), splata z przyszlych nadwyzek z odsetkami - splata zabiera rente lordowi. Najpierw rozpoznanie: ile miast i jak
czesto spada pod rezerwe (autotest 12:15, doba 40: 0 z 97, kasy razem 8.1 mln), ile maja notable, czy dekret podatkowy da sie powiazac ze
stanem kasy. Duzy projekt: audyt + autotest.
