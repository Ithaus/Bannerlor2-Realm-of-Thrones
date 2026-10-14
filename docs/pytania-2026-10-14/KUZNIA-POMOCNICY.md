# Kuznia gracza: ilu pomocnikow mozesz najac, za ile i ilu najwiecej (14.10)

Oznaczenia: **[K]** z kodu (plik:linia, drzewo `zb\int\Armoury\src`), **[U]** z Twoich ustawien (`Armoury.json` z 13.10; brak klucza = wartosc
domyslna z `Settings.cs`), **[P]** z logu (W53 = `Armoury-2026-10-14_03-24-46.log`, 120 dob; dwuletni = `Armoury-2026-10-13_21-14-13.log`),
**[S]** szacunek / rachunek (skrypt `scratchpad\kuznia\projekt\tabela.py`), **[S z H]** szacunek z obrazu historycznego.
Zakres (Twoje doprecyzowanie 14.10, wiazace): **tylko Twoja kuznia.** Warsztaty AI, rzemioslo miast, strzelarze, naprawy AI i mistrzowie Qohoru
zostaja tak, jak sa (dane historyczne) - w ich regulach nic nie zmieniam. Bez zmian kodu, bez commita, gry nie uruchamialem.
Synteza dwoch raportow: `scratchpad\kuznia\place\RAPORT.md` (stawki, rece miast, log) i `scratchpad\kuznia\praca\RAPORT.md` (kolejka kuzni,
historia, plan); tabele policzone od nowa. `scratchpad` = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad`.
`d` = pens (zloto gry). "Srednie miasto" = poziom plac 1.0 (dobrobyt 4 800).

## 0. Dla Jeffa (prosto)

1. **Dzis nie najmiesz nikogo.** Jest tylko towarzysz przy miechu: skraca robote najwyzej o 30% i tylko w menu "Forge", ktore masz wylaczone. W zakladce CRAFT, przy mieczach i przy lukach nic nie daje [K Forge.cs:291-299; U ForgeArmourEnabled false].
2. **Propozycja: najmujesz czeladnikow od kowali miasta, w ktorym kujesz.** Pracuja z Toba przy Twojej sztuce tylko wtedy, gdy jestes w miescie, i w nocy spia, tak jak Ty (zasada z 05.10 zostaje).
3. **Ilu przy jednej sztuce, poza Toba** (tylu, ilu moze robic ja naraz): kolczuga **7**, plyta **5**, skora i przeszywanica **4**, miecz, kusza i tarcza **2**, helm i luk **1**, strzaly i belty **5** [S z H].
4. **Ilu da miasto:** czwarta czesc swoich kowali, ale zawsze co najmniej 1, bo reszta musi robic u swoich mistrzow. Najmniejsze miasta 1-4 (Wyspy Letnie 1, Skagos 2, miasto Nocnej Strazy 4), miasto sredniej wielkosci 5, przecietne 7, miasta krolestwa Kings Landing srednio 10, duze 15-21, najwieksze 46. W zamku i we wsi 0, bo tam nie ma cechu kowali. Zostaje wtedy towarzysz [S z P W53:323, :710].
5. **Ilu chcesz, wybierasz w menu kuzni. Na start jest 0, wiec bez Twojego wyboru nic nie placisz.** Gra najmuje najmniej z trzech liczb: Twoj wybor, limit sztuki i to, ile da miasto. Najmuje tez tylko tylu, na ilu Cie stac. Kogo nie mialaby gdzie postawic, tego nie najmuje i nie placi mu. Towarzysz staje przy sztuce, gdy po najetych zostaje wolne miejsce.
6. **Stawka jednego pomocnika jest taka sama, jaka placi mu warsztat AI za ten sam czas:** dniowka mistrza wedlug tieru sztuki razy poziom plac miasta. Za dobe przy kowadle (18 h) w srednim miescie: t3 **6.75 d**, t4 8.4 d, t5 10.1 d, t6 **11.8 d**. W biednym miescie polowa tego, w bogatym do 1.5 raza [K WorkshopLaw.cs:154-160; S].
7. **Komu ida pieniadze:** do kiesy ludu tego miasta (70% czeladz, 30% mistrzowie), tak samo jak place warsztatow. Ci ludzie nie pracuja wtedy w warsztatach miasta: ich godziny schodza z puli kowali i nazajutrz warsztaty miasta maja o tyle mniej rak, tak jak przy naprawach. Kuznia kosztuje jak dotad 3 d/dobe i placisz ja do kasy miasta [K TownFolk.Live.cs:132; SmithHours.cs:53-59; WorkshopLaw.cs:1026; Forge.cs:383-409].
8. **Czy da sie najac 20? Przy jednej sztuce nie.** Najwiecej jest 7 (przy kolczudze), a reszta stalaby bez roboty. 20 ludzi kosztowaloby w srednim miescie **135 d/dobe przy zbroi t3 i 236 d przy t6** (w bogatym 202 i 354 d). Tylu dostaniesz tylko w duzym miescie, ktore ma ok. 80 kowali lub wiecej. Sens ma to dopiero przy kilku sztukach naraz. To osobny krok 2, ktory zaczne na Twoje slowo [S].
9. **Co to daje** (place jak w srednim miescie, pelna obsada sztuki): kolczuga t3 **z 45 do 5.7 doby** (placisz ludziom 268 d), ale tylko w miescie, ktore ma co najmniej 28 kowali. Miasto sredniej wielkosci (21 kowali) da 5 ludzi, wiec **z 45 do 7.6 doby** (255 d). Plyta t6 **ze 128 do 21 dob** (1 260 d), miecz t5 z 7.1 do 2.4 doby (48 d), helm t6 z 4.1 do 2.1 doby (24 d), luk t4 z 1.5 do 0.8 doby (6 d), kusza t4 z 7.1 do 2.4 doby (40 d) [S].
10. **Wlasne kucie dalej wychodzi taniej niz zakup,** przy pelnej obsadzie o 24-34% ceny. Zostaje Ci zysk wytworcy (20% ceny) i praca Twoich rak. Samemu wychodzi jeszcze taniej (46-66%), wiec pomocnicy nie daja przewagi na sztuce, kupujesz nimi tylko czas. Pomocnicy dostaja pelna stawke, a kupiec placi najwyzej 10% ceny nowej sztuki, wiec na kuciu nie zbudujesz maszyny do pieniedzy [K HistoricalPrices.cs:262-274; SellByCondition.cs:265-275; Settings.cs:309].
11. **Zamowienia lordow (tylko t2-t5): termin i zaplata bez zmian, ale oddajesz je szybciej.** Kolczuga t3 na zamowienie daje samemu ok. 9.5 d zysku na dobe, a z 5-7 ludzmi 38-50 d na dobe. Placi lord ze swojej kiesy. Ofert jest malo: pojawiaja sie tylko przy wjezdzie do miasta, a w ksiedze masz najwyzej 3. Po tescie sprawdze w logu, ile to daje na dobe [K Orders.cs:45-47, :179, :254-255; U OrderMaxTier 5, MaxAcceptedOrders 3; S].
12. **PD kowalstwa: tyle samo za sztuke, tylko przychodza szybciej. Jakosc i ryzyko pekniecia dalej zaleza od Twojej umiejetnosci.**
13. **Poprawka do liczb z rana:** plyta t6 sturgian_fortified kuta samemu trwa **128 dob, nie 109**. Jest lepsza od przecietnej plyty t6 (jakosc 1.17), a dni pracy rosna z jakoscia. 109 dob to przecietna plyta t6. Jakosc 1.17 wychodzi z jej ceny w pensach i z jej kosztu w skali gry, z obu tak samo [S z P W53:252, :257].
14. **Wdrozenie:** tor M, ok. 6 h: kod 3 h, budowa i recenzja 1 h, autotest 1.5 h, wpisy 0.5 h.

## 1. Regula (jedna, tylko kuznia gracza)

Co godzine pracy nad Twoja sztuka (`ArmouryBehavior.AdvanceProjects`, ta sama petla co dzis [K ArmouryBehavior.cs:1517-1585]):

    rece    = 1 (Ty) + najeci + towarzysz (jego ulga 0..1 = kowalstwo / 150, bez placy; tylko gdy najeci < limit sztuki)
    najeci  = najmniej z:  Twoj wybor w menu,
                           limit sztuki (rozdz. 2.1),
                           do najecia w miescie = floor(kowale miasta x 0.25), w miescie co najmniej 1; zamek i wies 0 (st.IsTown),
                           wolne godziny kowali miasta tej doby (SmithHours.Available),
                           ilu stac Cie na te godzine
    postep  = rece godzin pracy sztuki (dzis 1)
    placa   = najeci x dniowka(sztuka, miasto) / 16 za kazda godzine

- **Tylko gdy jestes przy kowadle** (`atForge`). Z dala robota i tak stoi (`ForgeOnlyWhileThere true` [K Settings.cs:39; ArmouryBehavior.cs:1510]),
  a najeci nie pracuja i nie biora pieniedzy. Noca (23-5) nikt nie pracuje [K ArmouryBehavior.cs:1550; U WorkshopNightRest true].
- **Obejmuje 100% Twojego kucia:** zbroje z CRAFT ("bk"), luki, kusze i amunicje z `Forge.Smith` ("bk"), bron gry ("van") i wlasny projekt z menu Forge ("").
  Czas pracy schodzi w jednym miejscu, `p.DaysLeft -= 1f / 24f` [K ArmouryBehavior.cs:1569]. Kazda z tych czterech drog przez nie przechodzi.
- **Co sie NIE zmienia:** `WorkshopLaw` (warsztaty AI), `TownCrafts`, `TownFletchers`, `AiWear` (naprawy lordow) i `QohorMasters`: w tych plikach nic nie ruszam.
  Wspolny punkt to istniejaca pula godzin kowali `SmithHours`, z ktorej juz dzis biora naprawy i Qohor. Godziny, ktore wezmiesz, warsztaty
  tego miasta maja nazajutrz mniej, wedlug juz istniejacej reguly [K WorkshopLaw.cs:1026]. Regula AI zostaje ta sama, zmienia sie tylko to, ze
  czlowiek, ktory pracuje u Ciebie, tego dnia nie pracuje w warsztacie (nic z niczego).

## 2. Ilu: trzy granice

### 2.1 Limit sztuki: ilu ludzi moze ja robic naraz [S z H]

| Rodzaj (ItemType + MaterialType) | Najetych najwyzej (poza Toba) | Przyklady | Dlaczego |
|---|---|---|---|
| Kolczuga (Body/Head/Leg/Hand/Harness, Chainmail) | 1 na 1.5 kg wagi kosztowej, 1-7 | koszula 10.2 kg -> 7, kaptur 3 kg -> 2, rekawice 1 kg -> 1 | drut, kolka i panele (przod, tyl, rekawy) robi sie rownolegle i laczy na koncu |
| Plyta (Plate) | 1 na 5 kg, 1-5 | 26 kg -> 5, 20 kg -> 4, helm 3 kg -> 1 | jedna duza czesc (helm, korpus, rece, nogi) = jeden platnerz, plus polerownik; Mediolan ok. 1450: kilku mistrzow przy jednej zbroi (KHM) |
| Skora (Leather) i przeszywanica (None) | 1 na 3 kg, 1-4 | skorznia 8 kg -> 3 | ciecie i szycie paneli |
| Bron jedno-, dwureczna, drzewcowa | 2 | miecz t5 -> 2 | mistrz + mlotowy + szlifierz |
| Tarcza, kusza, plaszcz | 2 | kusza t4 -> 2 | kusza: luczysko, loze i zamek osobno |
| Bron miotana (stos) | 3 | | seria |
| Luk | 1 | luk t4 -> 1 | luk ksztaltuje reka lukmistrza, pomocnik szykuje drewno i cieciwe |
| Strzaly i belty (seria 3 snopow) | 5 | | grotnicy i strzelarze robia swoje sztuki obok siebie |

Waga to `ArmsPricing.CostWeight` (przy uprzezy liczy sie waga prawdziwa) [K ArmsPricing.cs:141-152]. Zaokraglam do calych, najmniej 1.
Dla porownania: warsztat AI ma dzis srednio 2.9-3.3 czlowieka przy sztuce w robocie [P W53 9019/3086, dwuletni 10399/3144], warsztat notabla
6 roboczodni, Qohor 4 mistrzow przy jednym zleceniu [K Settings.cs:552, :596]. Przy obsadzie 1+1 do 1+5 jestes w skali warsztatu notabla i druzyny Qohoru.
Kolczuga (1+7) to wiecej niz srednia linia AI, ale linia AI nie ma limitu ludzi na sztuke [K WorkshopLaw.cs:262], a w duzych miastach ma ich wiecej.

### 2.2 Limit miasta: ilu kowali puszcza do Ciebie [S z P]

- Kowale miasta = rece warsztatow zbrojnych x 40% (platnerze 20% + miecznicy 20%) [K SmithHours.cs:35-40; Settings.cs:613-614].
- **Kowale sa zajeci w 97-99%** (bezczynnych w warsztatach zbrojnych: mediana 1.8% w W53, 3.9% w dwuletnim) [P W53:45508 i seria]. "Od reki" wolny
  jest w srednim miescie ok. 1 kowal. Dlatego pomocnika bierzesz z warsztatu, a nie z bezrobocia: **do najecia jest 1/4 kowali miasta**
  (`ForgeHandsTownShare 0.25`). Czeladnik (journeyman, od "journee") historycznie najmowal sie na dni, ale wiekszosc miala stale miejsce u
  mistrza. Przy 1/4 warsztaty miasta dalej robia 3/4 swojej roboty [S].

| Miasto | Rece zbrojne [P] | Kowali [S] | Do najecia [S] |
|---|---|---|---|
| male (Wyspy Letnie, Yi Ti) | 8 | 3 | 1 (podloga) |
| male (Zelazne Wyspy sr. 32, North 48, Dorne 42) | 32-48 | 13-19 | 3-4 |
| srednie (mediana W53) | 53 | 21 | **5** |
| srednia swiata (7 697 / 97) | 79 | 31.7 | 7 (floor 7.9) |
| Kings Landing (sr. 102) | 102 | 41 | 10 |
| duze (Volantis 152, Braavos 184, Reach 192, Qarth 211) | 150-210 | 60-85 | **15-21** |
| najwieksze | 460 | 184 | 46 |

[P W53:323 (min/mediana/max 8/53/460 i sumy krolestw), W53:710; S: srednie na miasto wedlug krolestw z raportu `place` rozdz. 3.2.]
Zamek i wies: **0**. Pule kowali biora tylko miasta (naprawy AI i Twoich ludzi wymagaja `st.IsTown` [K AiWear.cs:918; TroopSelfMend.cs:131]), a menu kuzni
jest tylko w menu miasta [K SmithMenu.cs:49]. **Uwaga dla kodu:** `SmithHours.Smiths(zamek.Town)` nie daje 0 (dawny wzor rak: najmniej 6 x 1.3, czyli 3-4 "kowali"
[K WorkshopLaw.cs:780-811]), wiec `ForHire` musi sam sprawdzic `st.IsTown` (rozdz. 12).

### 2.3 Pieniadze

Placisz co godzine. Jesli na godzine Ci nie starczy, robisz ja sam (z towarzyszem), a komunikat pojawia sie raz na dobe, tak jak przy kuzni [K Forge.cs:388-392].

### 2.4 Czego nie biore: limitu "prowadzenia" wedlug kowalstwa

Raport `praca` proponowal trzecia granice: 1 + kowalstwo/50 ludzi. **Odrzucam ja.** Kowalstwo juz decyduje, co w ogole mozesz kuc (prog
sztuki), jaka wyjdzie jakosc i czy sztuka peknie [K Forge.cs:54-69, 126-170]. Trzecia bramka liczylaby to samo drugi raz. Do tego na
poczatku nowej kampanii odpowiedz na "ilu moge najac" brzmialaby "jednego". Umiejetnosc nie przyspiesza tez Twoich rak, zgodnie z C5
("jak u kowali AI").

## 3. Stawka i obieg zlota

**Stawka za godzine najetej reki** = `WorkshopLaw.DayWage(sztuka, miasto) / WorkHoursPerManDay`, czyli dniowka mistrza wedlug tieru
(`3 + 1.5 x (tier - 1)`; strzaly i belty: dniowka t1) razy poziom plac miasta (dobrobyt / 4 800, w granicach 0.5-1.5) i podzielona przez 16 h
[K WorkshopLaw.cs:154-160; HistoricalPrices.cs:255-259, :286-290; TownWage.cs:17-25; U brak kluczy, domyslne Settings.cs:215, :714-717].
To ta sama liczba, ktora siedzi w wartosci sztuki, w placach warsztatow AI i w naprawach u kowali (jedna regula placenia, 07.10).

| Tier sztuki | Dniowka (16 h) | 1 reka na dobe przy kowadle (18 h): biedne / srednie / bogate | 20 rak na dobe: biedne / srednie / bogate |
|---|---|---|---|
| t1, amunicja | 3.0 | 1.69 / 3.38 / 5.06 | 34 / 68 / 101 |
| t2 | 4.5 | 2.53 / 5.06 / 7.59 | 51 / 101 / 152 |
| t3 (kolczuga) | 6.0 | 3.38 / **6.75** / 10.12 | 68 / **135** / 202 |
| t4 (luk, kusza) | 7.5 | 4.22 / 8.44 / 12.66 | 84 / 169 / 253 |
| t5 (miecz) | 9.0 | 5.06 / 10.12 / 15.19 | 101 / 202 / 304 |
| t6 (plyta, helm) | 10.5 | 5.91 / **11.81** / 17.72 | 118 / **236** / 354 |

[S z K]. Do tego kuznia: 3 d/dobe w srednim miescie (2 d w biednym, 4 d w bogatym), jedna niezaleznie od liczby ludzi [U ForgeFeeBase 3; K Forge.cs:405-409].

**Czemu nie taniej (np. 3 d jak czeladnik warsztatu towarowego):** wartosc sztuki zawiera robocizne liczona ta dniowka [K HistoricalPrices.cs:262-282].
Gdyby reka kosztowala mniej, najmowanie stawaloby sie maszyna do pieniedzy. Przy plycie t6 z 5 najetymi cudze rece robia 5/6 z 144 roboczodni:
120 x (10.5 - 3) = ok. 900 d zysku na kazdej sztuce [S]. Przy tej samej stawce co w AI pomocnicy zarabiaja dokladnie tyle, ile dokladaja do wartosci.

**Obieg (zamkniety, kazde zloto ma platnika i odbiorce):**
1. Gracz -> kasa miasta: przelew gry (`Pay.ToSettlement` / `GiveGoldAction`, ksiega widzi przelew).
2. Kasa -> kiesa ludu: `TownFolk.EarnFromTown(st, placa, KCraftPaid)`, podzial W 70% (czeladz, wyrobnicy) / R 30% (mistrzowie)
   [K TownFolk.Live.cs:132, :214-227]. To ta sama droga co Twoja zaplata kowalom za naprawy (`MendMaterial.PlayerLaborToFolk` [K MendMaterial.cs:424-439]).
   **Bez `TownFolk.Note(KCraftPaid)`**: Twoja sztuka to nie wyrob miasta, wiec doplata ludu do 35% wartosci wyrobow nie maleje o Twoje place.
   Tak samo jest przy naprawach [K TownFolk.Live.cs:818; MendMaterial.cs:395-398].
3. Godziny -> `SmithHours.Use(miasto, najeci x 1 h)` [K SmithHours.cs:53-59]. Nazajutrz warsztaty tego miasta maja o tyle mniej rak [K WorkshopLaw.cs:1026].
   Skala: 5 rak x 18 h = 90 h, czyli ok. 27% puli kowali sredniego miasta (21 x 16 h = 336 h) przez kilka dob [S].
4. Kuznia: bez zmian, kasa osady [K Forge.cs:383-398; Patches.cs:750-766]. Krotsza robota oznacza mniej oplaconych dob kuzni.
5. Zamek i wies: nikogo nie najmujesz, wiec nie ma przeplywu.

## 4. Tabela: 0 / 5 / 10 / 20 pomocnikow (srednie miasto)

Zalozenia: poziom plac 1.0, kuznia 3 d/dobe, lawa 18 h na dobe, roboczodzien 16 h, miasto daje co najmniej tylu, ile wynosi limit sztuki.
Material i cena wedlug `HistCost` = (material + roboczodni x dniowka) x 1.25 [K HistoricalPrices.cs:262-282; U HistProfitPercent 25 domyslne].
"Koszt na dobe" = place rak + kuznia. "Koszt calkowity" = place + kuznia (bez materialu). "Razem" = z materialem. Liczby [S] z `tabela.py`.

| Sztuka | Chcesz | Najetych | Dob w osadzie | Koszt na dobe | Koszt calkowity | Razem z materialem | Cena rynkowa | Taniej niz zakup o |
|---|---|---|---|---|---|---|---|---|
| **Kolczuga t3** (10.2 kg, jakosc 1.0; 51 roboczodni = 816 h; material 76) | 0 | 0 | 45.3 | 3.0 | 138 | 214 | 477 [S z K] | 263 (55%) |
| | 5 | 5 | 7.6 | 36.8 | 279 | 355 | 477 | 122 (26%) |
| | 10 / 20 | 7 (limit) | 5.7 | 50.2 | 286 | 362 | 477 | 115 (24%) |
| **Plyta t6** sturgian_fortified (26 kg, jakosc 1.17; 144 roboczodni = 2 304 h; material 472) | 0 | 0 | 128.0 | 3.0 | 387 | 859 | 2 480 [P W53:257] | 1 621 (65%) |
| | 5 / 10 / 20 | 5 (limit) | 21.3 | 62.1 | 1 326 | 1 798 | 2 480 | 682 (28%) |
| (plyta t6 przecietna, jakosc 1.0; 123 roboczodni) | 0 -> 5 | 0 -> 5 | 109.3 -> 18.2 | 3.0 -> 62.1 | 330 -> 1 133 | 802 -> 1 605 | 2 204 [S z K] | 64% -> 27% |
| **Miecz t5** battania_sword_5_t5 (1 kg; 8 roboczodni = 128 h; material 11) | 0 | 0 | 7.1 | 3.0 | 24 | 35 | 104 [P W53:257] | 69 (66%) |
| | 5 / 10 / 20 | 2 (limit) | 2.4 | 23.2 | 57 | 68 | 104 | 36 (34%) |
| **Helm t6** casterly_heavy_helm (3 kg; 4.6 roboczodnia = 74 h; material 54) | 0 | 0 | 4.1 | 3.0 | 15 | 69 | 129 [P W53:257] | 60 (46%) |
| | 5 / 10 / 20 | 1 (limit) | 2.1 | 14.8 | 33 | 88 | 129 | 41 (32%) |
| **Luk t4** (ok. 1 kg; 1.7 roboczodnia = 27 h; material 8) | 0 | 0 | 1.5 | 3.0 | 6 | 14 | 26 [S z K] | 12 (46%) |
| | 5 / 10 / 20 | 1 (limit) | 0.8 | 11.4 | 9 | 17 | 26 | 9 (33%) |
| **Kusza t4** (ok. 3 kg; 8 roboczodni = 128 h; material 10) | 0 | 0 | 7.1 | 3.0 | 24 | 34 | 87 [S z K] | 53 (61%) |
| | 5 / 10 / 20 | 2 (limit) | 2.4 | 19.9 | 49 | 59 | 87 | 28 (33%) |

- **10 i 20 daja to samo co limit sztuki.** Nadwyzki gra nie najmuje i nie placi jej. W srednim miescie (21 kowali, do najecia 5) kolczuga przy
  "10 / 20" dostaje 5 rak, wiec wychodzi 7.6 doby, nie 5.7.
- Jakosc plyty sturgian (1.17), miecza (1.0) i helmu (1.03) wyliczylem z ich cen w logu, bo to jedyna niewiadoma we wzorze. Kolczuga t3, luk t4 i
  kusza t4 to sztuki przykladowe o jakosci 1.0 (wagi szacowane), a ich cena wynika ze wzoru, nie z logu [S].
- Cena rynkowa to wartosc sztuki. Cena na straganie miasta dodatkowo plywa z cenami surowcow, popytem i polka [K ArmsPricing.cs:14-37].

## 5. Czy kucie z pomocnikami nie jest tansze niz zakup?

**Jest, przy pelnej obsadzie o 24-34% ceny (samemu o 46-66%).** Rozklad dla kolczugi t3: cena 477 d to material 76 + robocizna 306 + zysk wytworcy 95.
Z 7 pomocnikami placisz: material 76 + place 268 (7/8 robocizny) + kuznia 18 = 362 d. Roznica 115 d = zysk wytworcy 95 + Twoja 1/8 robocizny 38 - kuznia 18 [S].

**Dlaczego to w porzadku i nie trzeba tego wyrownywac:**
1. **Zysk wytworcy (25% kosztu, czyli 20% ceny)** bierze kazdy warsztat AI od robocizny swoich czeladnikow. Kiedy najmujesz ludzi, jestes takim samym warsztatem.
2. **Twoich rak nie placisz sam sobie.** Ich udzial maleje z liczba ludzi (1/(N+1)), wiec im wiecej najmujesz, tym blizej Ci do kosztu warsztatu AI.
3. **Nie ma taniej sily roboczej.** Pomocnik dostaje dokladnie stawke AI za te godziny (rozdz. 3).
4. **Na odsprzedazy nie zarobisz.** Kupiec placi za bron i zbroje najwyzej 10% ceny nowej sztuki [K Settings.cs:309 SellCapPercentOfNewAsk 10, brak w json],
   a do tego dziala kara handlowa BK x5 [P W53:332]. Kucie to Twoj wlasny sprzet i zamowienia lordow, nie handel.
5. **Zamowienia lordow** placa 1.35 x wartosc [K Orders.cs:167, :254; U OrderPayMultiplier 1.35], tylko za sztuki t2-t5 o wartosci (w starej monecie)
   do 12 000 [K Orders.cs:92-93; U OrderMinTier 2, OrderMaxTier 5, OrderMaxItemValue 12000] - plyty t6 lord nie zamowi. Kolczuga t3: zaplata 643 d.
   Samemu zostaje Ci 643 - 214 = 429 d po 45 dobach (9.5 d na dobe), z 5 rekami 288 d po 7.6 doby (38 d na dobe), z 7 rekami 281 d po 5.7 doby
   (50 d na dobe) [S]. **Zysk na sztuce maleje,** bo placisz ludziom, a rosnie tempo, wiec zysk na dobe rosnie 4-5 razy. Placi lord ze swojej kiesy
   (najwyzej tyle, ile ma [K Orders.cs:255]). Ofert jest malo: tylko przy wjezdzie do miasta, z szansa 50%, jedno miasto najwyzej raz na 3 doby,
   oferta zyje 5 dob, w ksiedze najwyzej 3 [K Orders.cs:33-56, :179; U]. Jesli log pokaze, ze zamowienia staly sie maszyna do pieniedzy,
   poprawka nalezy do zamowien (narzut 1.35), nie do pomocnikow.
6. **Bogate miasto samo to wyrownuje.** Przy poziomie plac 1.5 kolczuga z 7 rekami kosztuje ok. 402 + 24 + 76 = 502 d, czyli wiecej niz cena 477 d.
   W biednym miescie rece sa za pol ceny, ale jest ich tylko 1-3 [S].

## 6. PD kowalstwa, jakosc, zamowienia, towarzysz

- **PD: suma za sztuke bez zmian, przychodzi szybciej.** 40% PD "w trakcie" (wlasny projekt i bron gry) liczy sie co godzine. Teraz bedzie
  mnozone przez liczbe rak tej godziny [K ArmouryBehavior.cs:1585; U XpShareWhileWorking 0.4]. Bez tego przy 6 rekach stracilbys 5/6 z tych 40%.
  Sztuki "bk" (zbroja z CRAFT, luki, kusze, amunicja) dostaja PD przy kliknieciu i tu nic sie nie zmienia.
- **Jakosc, ryzyko pekniecia i prog sztuki** liczy sie z Twojego kowalstwa, jak dotad. Pomocnicy tego nie zmieniaja.
- **Stamina:** bez zmian (zuzywasz ja przy rozpoczeciu).
- **Zamowienia lordow:** termin zostaje 12 dob + doby Twojej pracy w pojedynke [K ForgeClock.cs:66-72]. Pomocnicy daja zapas, a za brak rak nie ma kary.
- **Towarzysz przy miechu** wchodzi w te sama regule. Liczy sie jako jedna z rak z waga rowna swojej uldze (0..1, pelna przy kowalstwie 150
  [K Settings.cs:1410; Helper.cs:55-60]), nie bierze placy i staje przy sztuce, gdy po najetych zostaje wolne miejsce w limicie sztuki.
  Twoj wybor ma pierwszenstwo: gra sama bierze towarzysza juz od kowalstwa 20 (ulga 0.13) [K Helper.cs:49], a przy luku i helmie jest jedno miejsce,
  wiec slaby towarzysz wypychalby oplaconego czeladnika (1.13 reki zamiast 2). Bez najetych stoi przy kazdej sztuce, jak w poprzedniej wersji projektu. Dawne `days *= (1 - relief x HelperTimeRelief)`
  w `Forge.Begin` [K Forge.cs:299] usuwam, inaczej liczylby sie dwa razy. Ulga staminy zostaje [U HelperStaminaRelief 0.4].
  **To zmiana zachowania:** towarzysz pomaga teraz we wszystkich czterech drogach. Towarzysz z kowalstwem 150 to pelna reka, czyli robota idzie
  2 razy szybciej. Trzeba to opisac w CHANGELOG.

## 7. Klucze MCM

Klucze dopisuje sie do `Settings.cs` i trzeba potem uruchomic `python tools/gen_mcm.py`. W Twoim `Armoury.json` ich nie ma, wiec zadziala wartosc domyslna.

| Klucz | Domyslnie | Opis w MCM (po angielsku) |
|---|---|---|
| `ForgeHiredHands` | true | Hire hands at your forge: journeymen of the town's smiths work your piece with you while you stay in the town, each paid what a town workshop pays for the same hours (the master's day wage for the piece's tier, as dear as the town is prosperous). The pay goes to the town's craftsmen, and those hours are missing from the town's workshops. Off = you work alone, and a companion at the bellows shortens only an own Forge project, as before |
| `ForgeHandsTownShare` | 0.25 | The share of a town's smiths who will leave their masters to work at your forge (at least one in any town; none in a castle or village) |
| `ForgeHandsMaxPerPiece` | 7 | The most hired hands at one piece. A mail shirt takes the most; plate, weapons and bows take fewer by their kind |

Limity rodzajow (rozdz. 2.1) to stale w kodzie z komentarzem [S z H], zeby nie mnozyc dziesieciu suwakow. Twoj wybor ("ilu chcesz") jest
zapisywany w zapisie gry (`arm_forge_hands`, tak jak `arm_smith_helper` [K ArmouryBehavior.cs:497]), nie w MCM. **Domyslnie 0**, wiec bez Twojego
wyboru nie pojawia sie zaden koszt.
Menu kuzni: obok "Choose your helper at the bellows" [K SmithMenu.cs:156-157] dochodzi "Hire hands at the forge" z wyborem None / 1 / 2 / 3 / 5 / 7 /
"As many as the work takes". Opis pokazuje limit sztuki, liczbe rak do najecia w tym miescie, stawke jednego na dobe i przewidywany czas.

## 8. Linie logu

1. Start sesji:
   `Kuznia gracza - rece: CZYNNE - udzial kowali miasta 0.25, najwyzej 7 przy sztuce; rodzaje: kolczuga 1/1.5 kg (1-7), plyta 1/5 kg (1-5), skora i przeszywanica 1/3 kg (1-4), bron, tarcza, kusza, plaszcz 2, miotana 3, luk 1, strzaly i belty 5; stawka = dniowka warsztatu AI / 16 h.`
2. Przy kazdej sztuce, gdy zaczynaja ja reczni albo zmienia sie ich liczba:
   `Kuznia gracza - rece: <osada> | <item> | limit sztuki 7, kowali miasta 21.4 -> do najecia 5, wybrane 7 -> najetych 5 + towarzysz 0.60 | dniowka 6.00 d x poziom plac 1.00 = 6.75 d na reke na dobe | sam 816 h = 45.3 doby -> z rekami ok. 6.9 doby.`
3. Raz na dobe (gdy ktos byl najety):
   `Kuznia gracza - rece (doba N): <osada> | godzin najetych 90 (pula kowali 342 h, wolne przed 342 h) | place 34 d -> kiesa ludu 34 (W 24 / R 10), kasa 0 | kuznia 3 d | PD w trakcie x6.6 | bez zlota 0 h | zostalo 697 h.`
4. Napisy dla gracza (po angielsku): `Five journeymen of <town> work the <item> with you - about 7 days instead of 45, 34 gold a day.` oraz
   `You cannot pay the journeymen in <town> - they wait, and you work alone.` (raz na dobe).

Co sprawdzic po tescie: suma `place` w linii 3 = spadek zlota gracza - kuznia; `kiesa ludu` + `kasa` = `place`; godziny najetych = rece x godziny pracy;
dni sztuki zgadzaja sie z linia `Kuznia jak u kowali (C5)`.

## 9. Plan kodu (tor M: jedna zmiana, jedna recenzja, autotest; ok. 6 h)

| # | Plik | Co | ok. h |
|---|---|---|---|
| 1 | **nowy** `Armoury/src/ForgeHands.cs` | `Cap(item)` (rozdz. 2.1), `ForHire(settlement)` (rozdz. 2.2; najpierw `st.IsTown`, inaczej 0 - pula zamku nie jest zerem), `Hour(p, st)` -> rece tej godziny (najeci pierwsi, towarzysz w wolne miejsce); place: `Pay.ToSettlement` + `TownFolk.EarnFromTown(KCraftPaid)` bez `Note`, reszta kasa; `SmithHours.Use`; grosze `MBRandom.RoundRandomized` jak `WorkshopLaw.cs:323`; liczniki doby i linie 1-3 logu; w ostatniej godzinie placa tylko za faktycznie zuzyta czesc (`min(rece, DaysLeft x 24)`) | 2 |
| 2 | `ArmouryBehavior.cs` | :1569 `p.DaysLeft -= rece / 24f` (tylko `atForge`, zdalnie 1); :1585 PD w trakcie x rece; `SyncData` `arm_forge_hands` (wzor :497) | 0.5 |
| 3 | `Forge.cs` | :291-299 przy `ForgeHiredHands` bez skracania dni ulga towarzysza (zostaje stamina, PD i komunikat) | 0.25 |
| 4 | `SmithMenu.cs` | opcja "Hire hands at the forge" obok :156-157 (wybor, opis z liczbami); menu czekania (:295-332): "with N hands: about X days" | 1 |
| 5 | `ForgeClock.cs`, `Settings.cs`, `tools/gen_mcm.py` | `NoteQueued` (:75-91): przewidywanie z rekami; 3 klucze z rozdz. 7 + gen_mcm | 0.5 |
| 6 | budowa, recenzja, autotest, wpisy | miasto srednie: kolczuga z CRAFT, wybor 7 -> 5 najetych, kilka dob czekania; sprawdzenie linii 1-3, przeplywu zlota (gracz -> kasa -> kiesa), puli kowali, PD; CHANGELOG + STAN-PRAC | 2 |

**Ryzyko / co sprawdzic** (zasada glowna 05.10):
- Czas schodzi tylko w jednym miejscu (:1569), wiec wszystkie 4 drogi sa objete. `HandOver` i `CollectReadyProjects` zostaja bez zmian.
- PD: trzeba je mnozyc przez rece, inaczej strata (patrz rozdz. 6).
- Pula kowali: najete godziny zmniejszaja tej doby wolne godziny napraw Twoich ludzi w tym miescie (`TroopSelfMend` bierze z `SmithHours.Available`
  [K TroopSelfMend.cs:141]), a nazajutrz rece warsztatow. To zamierzone (te same rece), ale do sprawdzenia w logu doby.
- Kiesa ludu: `EarnFromTown` bez `Note(KCraftPaid)`. Inaczej kasa miasta zarabialaby na Twoich placach, bo doplata do 35% by malala.
- Zapis: wybor jest w `SyncData`. Grosze rozliczane co godzine `RoundRandomized`, bez ulamkow w pamieci.
- Wylacznik `ForgeHiredHands = false` daje dokladnie dzisiejsze zachowanie, razem z dawna ulga towarzysza.
- Gdy Cie nie ma, nikt nie pracuje i nic nie placisz. Zasada 05.10 zostaje.

## 10. Krok 2 (nie na start): kilka sztuk naraz, czyli "20 rak"

Dzis w osadzie tyka tylko najstarsza sztuka (`busyForge` [K ArmouryBehavior.cs:1517]). Zeby 20 rak mialo sens, reka ponad limit pierwszej sztuki
szlaby do nastepnej w kolejce, kazda do swojego limitu. Przyklad [S]: duze miasto (80+ kowali, 20 do najecia), trzy kolczugi t3: Ty + 7 przy pierwszej,
7 przy drugiej, 6 przy trzeciej. Koszt 135 d/dobe za rece plus kuznia. Wymaga zmiany `busyForge`, osobnej doby kuzni dla kazdej lawy (wiecej
ludzi to wiecej palenisk) i PD tylko za sztuke, przy ktorej stoisz. To juz jest "Twoj warsztat" jak u AI. Proponuje go dopiero po sprawdzeniu
kroku 1 i tylko na Twoje slowo, ok. 4-6 h dodatkowo.

## 11. Czego nie wiem / co jest szacunkiem

- Limity ludzi przy sztuce (7 / 5 / 4 / 2 / 1 / 5) to szacunek z ogolnego obrazu warsztatow (Mediolan: kilku mistrzow przy jednej zbroi;
  Greenwich 1515: ok. 11 osob na caly warsztat; kolczuga: kilkadziesiat tysiecy kolek w panelach). Zapisu z epoki "przy kolczudze pracowalo N ludzi"
  nie mam [S z H]. Zrodla: raport `praca` rozdz. 2.1 i 8.
- Udzial 1/4 kowali do najecia to moja decyzja projektowa, nie liczba z cechow. Limitow czeladnikow na mistrza dla platnerzy nie znalazlem [S].
- Liczby kowali konkretnego miasta nie ma w logu: sa tylko min/mediana/max (8/53/460) i sumy krolestw. Tabela w rozdz. 2.2 to szacunek [S z P].
- Dni pracy plyty sturgian (144), miecza (8.0) i helmu (4.6) wyliczylem z ich cen w logu wzorem `HistCost`, bo jakosci sztuki log nie podaje. Dokladne dni
  pokaze linia `Kuznia jak u kowali (C5)` po pierwszym kuciu. Kolczuga t3, luk t4 i kusza t4 to sztuki przykladowe, nie konkretne id [S].
- W logach z 14.10 nie ma zadnego Twojego kucia ani linii C5 (C5 jest nowsze niz logi), wiec wszystko o kolejce pochodzi z kodu [P oba logi].
- Lukmistrzow i strzelarzy biore z tej samej puli kowali (`SmithHours` liczy platnerzy i miecznikow; osobnej puli godzin lucznikow w kodzie nie ma).
  To uproszczenie; przy luku chodzi o 1 reke, przy strzalach o 5 [S].
- Czy zakladka CRAFT BK pozwala kuc w zamku, nie sprawdzalem. Jesli pozwala, i tak nie najmiesz tam nikogo (rozdz. 2.2).
- Poza zakresem, nie ruszam: `Forge.Smith` daje 60% ProjectXp, a 40% "w trakcie" nigdy nie przychodzi [K Forge.cs:628, :643; ArmouryBehavior.cs:1585].
  Napis `ProjectSummary` nie ostrzega, ze zegar stoi bez Ciebie przy `ForgeOnlyWhileThere` [K ArmouryBehavior.cs:1467 vs :1510].

## 12. Weryfikacja (sceptyk, 14.10)

Sprawdzone samodzielnie w drzewie `zb\int\Armoury\src`, w Twoim `Armoury.json` (tylko odczyt, 334 klucze) i w logach. `tabela.py` przeliczone ponownie,
wyniki te same. Kodu nie zmienialem, gry nie uruchamialem.

**Zgadza sie (sprawdzone):**
- **Stawka:** `DayWage = DayWageOf x TownWage.Index` [K WorkshopLaw.cs:154-160]; `WageFor = 3 + 1.5 x (tier - 1)` [K HistoricalPrices.cs:255-259];
  indeks = dobrobyt / 4 800 w granicach 0.5-1.5 [K TownWage.cs:17-25]. Kluczy plac w json brak [U]. Czeladnik na dobe lawy (18/16 dniowki):
  t3 6.75 d, t6 11.81 d; 20 ludzi 135 / 236 d, w bogatym 202 / 354 d [S].
- **Godziny:** `WorkHours = SmithDays x 16 x sztuk` [K ForgeClock.cs:53-59; U WorkHoursPerManDay brak = 16]. Kolczuga t3 10.2 kg: 5 dni/kg = 51 roboczodni
  [K ArmsPricing.cs:243] = 816 h = 45.3 doby. Plyta t6: `w x (1.5 + 1.0 x 3) + 6` = 123 roboczodni x jakosc [K ArmsPricing.cs:242, :290] = 1 968 h
  (109 dob) przy jakosci 1.0. Sturgian: jakosc 1.17 z ceny 2 480 d [P W53:257], a niezaleznie takze z kosztu w skali gry 32 960 [P W53:252]
  (pasuje do 144 roboczodni, do 123 nie). Wychodzi 2 304 h = 128 dob. Miecz t5 8 dni = 128 h [K :64], luk t4 1.7 dnia = 27 h [K :71, :277],
  helm t6 (3 x 1.0 + 1.5) x 1.03 = 74 h [K :275-276; Settings.cs:735].
- **Jeden punkt zegara:** `p.DaysLeft -= 1f / 24f` [K ArmouryBehavior.cs:1569]. Grep po wszystkich plikach: poza tym `DaysLeft` tylko powstaje, jest
  czytane z zapisu i przycinane w "van" przy wylaczonym C5 [K :1442, :1499, :1595; Project.cs:28].
- **Obieg:** `EarnFromTown` -> `FromCoffer` -> `Credit` (W70/R30) nie rusza `f.Win[KCraftPaid]`. Doplate do 35% zmniejsza tylko `Note`
  [K TownFolk.Live.cs:132, :143-160, :214-228, :818; TownFolk.cs:208-217]. Twoje place nie zmniejszaja doplaty ludu (jak naprawy [K MendMaterial.cs:395-398, :424-439]).
- **Pula kowali:** `Smiths = TownHands x 0.4` [K SmithHours.cs:35-40; Settings.cs:613-614]. Godziny z doby D odejmuja sie w D+1 od rak platnerzy
  i miecznikow, najwyzej do zera [K WorkshopLaw.cs:1024-1044]. Kowale sa zajeci: w ostatniej dobie W53 bezczynnych 37 z 9 074 roboczodni (0.4%) [P W53:45508].
- **Odsprzedaz:** sufit 10% obejmuje tez NOWA sztuke (`newWorth = max(Value, ItemValue)`) [K SellByCondition.cs:265-275; U SellPriceByCondition,
  RetailFromWorth brak = true].
- **Brak kucia gracza w logach:** 0 linii "Kuznia jak u kowali", "Kuznia: doba w", "Projekt dodany" w W53 i w dwuletnim [P grep].

**Trzy miasta i zamek.** Biore krolestwa z jednym miastem, wiec liczba rak z logu jest liczba tego miasta [P W53:323 rece, W53:710 liczba miast].
Kowale i najem [S z K]. Wolnych od reki (bezczynnych) kowali jest w kazdym z nich 0, dlatego najem idzie z 1/4 obsady warsztatow.

| Osada | Rece zbrojne [P] | Kowali (x0.4) | Pula godzin na dobe (x16) | Do najecia (1/4, min 1) | 18 h najetych = czesc puli |
|---|---|---|---|---|---|
| miasto Wysp Letnich | 8 | 3.2 | 51 | 1 (podloga) | 35% |
| miasto Skagos | 24 | 9.6 | 154 | 2 | 23% |
| miasto Nocnej Strazy | 42 | 16.8 | 269 | 4 | 27% |
| (skala) Kings Landing, srednio na miasto z 7 | 102 | 40.8 | 653 | 10 | 28% |
| zamek | 0 wedlug reguly | - | - | 0 | - |

Zamek w kodzie NIE ma zera: `TownHands` zamku spada na dawny wzor (dobrobyt / 170, najmniej 6) x 1.3 = ok. 8-11 rak, czyli 3-4 "kowali" i 1 do najecia
[K WorkshopLaw.cs:780-811; Settings.cs:597-598; P dobrobyt zamku 1 043-1 409, W53:818, :45826]. Tej puli nikt nie uzywa (naprawy tylko w miescie
[K AiWear.cs:918; TroopSelfMend.cs:131]), a dawny odnosnik WorkshopLaw.cs:867 to tylko suma swiata. Dlatego `ForHire` musi sam sprawdzic `st.IsTown`
(dopisane w rozdz. 2.2 i 9).

**Poprawione w tym pliku:**
1. Rozdz. 0 p. 4 i rozdz. 2.2: najmniejsze miasta daja 1-4, nie 1-3 (North 48 rak -> 4, Dorne 42 -> 4). Przecietne miasto daje 7, nie 8:
   79.4 x 0.4 = 31.7 kowala, floor(7.9) = 7.
2. Rozdz. 0 p. 9: 5.7 doby kolczugi wymaga 7 najetych, czyli miasta z co najmniej 28 kowalami (ok. 70 rak). Miasto z mediany (21 kowali) da 5:
   7.6 doby i 255 d. "Srednie miasto" znaczylo w pliku dwie rzeczy (poziom plac 1.0 i wielkosc z mediany).
3. Rozdz. 0 p. 10: zysk wytworcy to 20% ceny (25% narzutu na koszt), nie "25% w cenie". Dopisane, ze samemu jest taniej, wiec pomocnicy kupuja tylko czas.
4. Rozdz. 0 p. 11 (nowy) i rozdz. 5 p. 5: przyklad zamowienia na plyte t6 byl niemozliwy. Lord zamawia tylko t2-t5 [U OrderMaxTier 5; K Orders.cs:92],
   a sturgian ma 51 200 w starej monecie przy suficie 12 000 [P W53:252; U OrderMaxItemValue]. Nowy przyklad: kolczuga t3.
5. Rozdz. 3: "1 080 d" liczylo wszystkie 144 roboczodni jako cudze. Przy 5 najetych cudze jest 5/6, czyli 120 roboczodni -> ok. 900 d.
6. Rozdz. 8 linia 3: 90 h x 6/16 = 34 d placy (bylo 51), zostalo 816 - 6.6 x 18 = 697 h (bylo 226), PD x6.6.
7. **Zmiana reguly** (rozdz. 1, 6, 9): towarzysz nie zajmuje miejsca przed oplaconym czeladnikiem. Gra bierze go sama juz od kowalstwa 20
   (ulga 0.13) [K Helper.cs:49, :55-60]. Przy luku i helmie jest jedno miejsce, wiec wypychalby pelna, oplacona reke (1.13 reki zamiast 2).
   Teraz najeci pierwsi, a towarzysz staje w wolne miejsce. Bez najetych stoi przy kazdej sztuce.
8. Rozdz. 0 p. 7: godziny najetych odejmuja sie warsztatom nazajutrz [K WorkshopLaw.cs:1026], nie "tego dnia". Rozdz. 0 p. 5: dopisany Twoj wybor
   (na start 0) jako pierwsza granica. Rozdz. 2.1: obsada 1+7 jest wieksza od sredniej linii AI (2.9-3.3), nie "w tej samej skali".
9. Rozdz. 6: HelperSkillForFullRelief to Settings.cs:1410, nie :1403.

**Exploity - sprawdzone:**
- **Tansza zbroja bez ryzyka: nie.** Z pomocnikami sztuka kosztuje wiecej niz samemu (kolczuga 214 -> 355-362 d) i tylko ok. 5% mniej niz koszt
  warsztatu AI (382 d = material + place), bo Twoja reka jest darmowa (1/8 roboty przy 7 najetych), a placisz kuznie. Jakosc i pekniecie losuja sie
  jak dotad. Odsprzedaz: najwyzej 10% ceny nowej.
- **Nieskonczone rece: nie.** Granice: 7 przy sztuce, 1/4 kowali miasta, wolne godziny puli, jedna sztuka naraz w osadzie (`busyForge`
  [K ArmouryBehavior.cs:1517-1533]) i tylko gdy jestes w tej osadzie. W calym swiecie pracuje naraz najwyzej 7 najetych. Warsztaty traca te godziny
  nazajutrz, wiec nic z niczego.
- **Zamowienia lordow: nie maszyna do pieniedzy, ale wyrazny skok.** Zysk na dobe x4-5 (kolczuga t3: 9.5 -> 38-50 d). Placi lord ze swojej kiesy,
  najwyzej tyle, ile ma [K Orders.cs:255]. Ofert jest malo [K Orders.cs:33-56, :179]. Do sprawdzenia po tescie: suma zaplat zamowien na dobe.
  Poprawka, jesli bedzie trzeba, nalezy do narzutu 1.35, nie do pomocnikow.
- **Podloga "co najmniej 1":** w najmniejszym miescie bierze 35% puli kowali (Wyspy Letnie). Dopuszczam, bo to jeden czlowiek, ale to wiecej niz 1/4.
- **Ludzie a godziny:** 1/4 kowali liczona w ludziach pracuje po 18 h, a dzien kowala ma 16 h, wiec bierze ok. 28% ich godzin, nie 25%. Place
  i pula licza sie od godzin, wiec obieg sie zgadza. Zostawiam.
