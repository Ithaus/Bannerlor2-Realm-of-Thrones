# Rozwoj postaci w Twojej grze: co ile awans, ile punktow na poziom, jak dziala nauka (14.10)

Oznaczenia: **[K]** z kodu (plik:linia), **[U]** z Twoich ustawien (plik), **[P]** z logu albo naglowka zapisu gry, **[S]** szacunek wyliczony z liczb [K]/[P] (skrypt podany).
Gra 1.4.8 + Twoj zestaw modow (ROT 8.1.8, Banner Kings, NavalDLC, RBM, Armoury...). Nic nie zmieniano, gry nie uruchamiano, bez commita.
Synteza trzech raportow: `scratchpad\rozwoj\gra\RAPORT.md` (kod gry, kreator ROT), `rozwoj\mody\RAPORT.md` (co zmieniaja mody), `rozwoj\praktyka\RAPORT.md`
(logi, zapisy, czas); tabele w tym pliku policzone od nowa skryptami `rozwoj\synteza\tabela_synteza.py` i `nauka.py` (wynik `nauka_wynik.txt`).
`scratchpad` = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad`.

Skroty plikow kodu: `DCDM` = `TaleWorlds.CampaignSystem.GameComponents\DefaultCharacterDevelopmentModel.cs`, `HD` = `TaleWorlds.CampaignSystem.CharacterDevelopment\HeroDeveloper.cs`,
`CCC` = `TaleWorlds.CampaignSystem.CharacterCreationContent\CharacterCreationContent.cs` (dekompilacja gry `dec32\cs_full`); `RCCB` = `ROTCharacterCreationCampaignBehavior.cs`
(ROT.dll, `rozwoj\gra\dec`); `BKEdu` = `BannerKings.Behaviours.BKEducationBehavior.cs` (BannerKings.dll, `rozwoj\mody\dec\bk`); `BKF` = dekompilacja BK `e4tools\bkfull`.

## 0. Dla Jeffa (prosto)

1. **Awans liczy sie z PD postaci, a te zbierasz 1:1 z umiejetnosci.** Kazdy punkt doswiadczenia dla dowolnej umiejetnosci (cios, marsz, handel, rozmowa) wpada w calosci do licznika postaci. Dzieje sie to, zanim zadziala tempo nauki [K] HD:204-207, 224-229.
2. **Co ile poziom:** poziom 2 = 1 001 PD, a kazdy nastepny kosztuje ok. 20% wiecej + 1 000 PD. Lacznie: L10 = 80 tys., L20 = 833 tys., L30 = 5,8 mln, L40 = 36,5 mln. **Maks. poziom 62** (2 mld PD, w praktyce nieosiagalny) [K] DCDM:81-93, 113-120.
3. **Punkty:** co poziom **+1 skupienia (focus)**. Co 4. poziom (4, 8, 12 ... 60) **+1 cechy (attribute)**. **Banner Kings dorzuca +1 skupienia na kazdym 5. poziomie** (5, 10 ... 60). Z poziomow do L62 razem 74 skupienia i 15 cech [K] HD:336-344, BKEdu:88-93.
4. **Maksima:** cecha 10, skupienie 5 na umiejetnosc [K] DCDM:59-61. U Ciebie jest **7 cech** (6 z gry + Wisdom z BK; Wisdom ma w oknie postaci osobny kafelek z "+", doklejony przez BK) i **24 umiejetnosci** (18 + 3 morskie + 3 BK). Nawet na L62 masz ok. 36-39 z 70 cech i 87-93 ze 120 skupien, wiec wszystkiego nie wymaksujesz [K/S].
5. **Start (kreator ROT):** 20 punktow cech na karcie (z Wisdom 2; z rodzicami z Braavos 19) + 1/2/3/4 wolne. Do tego 11 skupien przypisanych + 2/4/6/8 wolnych. Ile wolnych, zalezy od wieku startu 20/30/40/50 [K] RCCB:479-491, 647-659, 4887-4890, 7477-7603.
6. **Limit nauki = 10 x (cecha - 1) + 30 x skupienie.** Do limitu umiejetnosc rosnie w tempie 1,25 x (1 + 0,4 x cecha + skupienie). Za limitem tempo spada, a umiejetnosc staje na **limit + 4 x cecha + 10 x skupienie** [K] DCDM:206-244.
7. **Przyklad: cecha 5, skupienie 3:** limit 130, tempo x7,5, umiejetnosc staje na 180. **Poziomu postaci (np. 20) nie ma w tych wzorach.** Liczy sie tylko cecha i skupienie, a poziom daje Ci tylko punkty do rozdania [K] DCDM:206-244.
8. **Limit nie hamuje awansu:** umiejetnosc zatrzymana na limicie dalej nabija PD postaci [K] HD:204-207.
9. **Mody:** **zaden mod nie zmienia** krzywej poziomow, maksimow ani wzorow nauki. Czynny model Banner Kings liczy dokladnie jak gra.
   - Punkty zmienia tylko BK: +1 skupienia co 5 poziomow, ksiazki daja skupienie, a styl zycia BK je zjada.
   - Tempo PD zmieniaja: RBM i Armoury (walka, strzal, jedzenie, marsz), BK (Charm z relacji), Spoils (Roguery).
   - TournamentsXPanded u Ciebie nie dziala [K/U].
10. **Twoje tempo z poprzednich kampanii:** 7-10 tys. PD na dobe (L29 po 672 dobach, L34 po 1 237). Do L5 awans szybciej niz co dobe, L10 co 3-4 doby, L20 co 3-4 tygodnie, L30 co 4-5 miesiecy, L35 mniej wiecej co rok, a L40 -> 41 co 2-3 lata. To srednie z calych kampanii: na poczatku kampanii realnie wolniej [P/S].
11. **Rok 364 doby:** awans liczy sie z dob, nie z lat, wiec na dobe nic sie nie zmienia. Zmienia sie wiek: postac starzeje sie 4,33x wolniej.
    - Po 1. roku masz ok. L26-27 (a nie L18-20).
    - Do starosci (od 20 do 67 lat = 17 tys. dob) dojdziesz do ok. L47-48 zamiast L38-40 [S].

## 1. Tabela poziomow (1 -> 62)

Wzor [K] DCDM:81-93: `prog[1] = 1; krok = 1000; dla L = 2..62: prog[L] = prog[L-1] + krok, potem krok = krok + 1000 + krok/5` (dzielenie calkowite).
Awans, gdy PD lacznie >= prog (petla moze dac kilka poziomow naraz) [K] HD:252-269. Powyzej 62 prog = int.MaxValue, petla staje -> **maks. L62** [K] DCDM:113-120.
Kontrola: prog[62] = 2 027 685 990 < int.MaxValue, bez przepelnienia (skrypt).

Punkty "na tym poziomie": +1 skupienia zawsze [K] HD:338, DCDM:67; +1 cechy gdy poziom dzieli sie przez 4 [K] HD:339-342, DCDM:65;
+1 skupienia od Banner Kings gdy poziom dzieli sie przez 5 (dla kazdego bohatera, takze gracza) [K] BKEdu:31, 88-93.
Ostatnia kolumna [S]: PD do nastepnego poziomu / Twoje srednie tempo z dwoch kampanii Ironman (9 784 i 7 106 PD na dobe, rozdz. 5);
rok = 364 doby. To srednie z calych kampanii, a tempo rosnie z czasem - na poczatku kampanii realnie wolniej, niz pokazuje tabela.

| Poziom | PD lacznie, zeby go miec | PD z tego poziomu do nastepnego | Dostajesz na tym poziomie | Razem z poziomow (skupienie / cecha) | Dni do nastepnego w Twoim tempie [S] |
|---:|---:|---:|---|---:|---:|
| 1 | 1 | 1 000 | +1 skup. | 1 / 0 | <1 |
| 2 | 1 001 | 2 200 | +1 skup. | 2 / 0 | <1 |
| 3 | 3 201 | 3 640 | +1 skup. | 3 / 0 | <1 |
| 4 | 6 841 | 5 368 | +1 skup., +1 cecha | 4 / 1 | <1 |
| 5 | 12 209 | 7 441 | +2 skup. (1 gra + 1 BK) | 6 / 1 | <1 - 1 |
| 6 | 19 650 | 9 929 | +1 skup. | 7 / 1 | 1 |
| 7 | 29 579 | 12 914 | +1 skup. | 8 / 1 | 1 - 2 |
| 8 | 42 493 | 16 496 | +1 skup., +1 cecha | 9 / 2 | 2 |
| 9 | 58 989 | 20 795 | +1 skup. | 10 / 2 | 2 - 3 |
| 10 | 79 784 | 25 954 | +2 skup. (1 gra + 1 BK) | 12 / 2 | 3 - 4 |
| 11 | 105 738 | 32 144 | +1 skup. | 13 / 2 | 3 - 5 |
| 12 | 137 882 | 39 572 | +1 skup., +1 cecha | 14 / 3 | 4 - 6 |
| 13 | 177 454 | 48 486 | +1 skup. | 15 / 3 | 5 - 7 |
| 14 | 225 940 | 59 183 | +1 skup. | 16 / 3 | 6 - 8 |
| 15 | 285 123 | 72 019 | +2 skup. (1 gra + 1 BK) | 18 / 3 | 7 - 10 |
| 16 | 357 142 | 87 422 | +1 skup., +1 cecha | 19 / 4 | 9 - 12 |
| 17 | 444 564 | 105 906 | +1 skup. | 20 / 4 | 11 - 15 |
| 18 | 550 470 | 128 087 | +1 skup. | 21 / 4 | 13 - 18 |
| 19 | 678 557 | 154 704 | +1 skup. | 22 / 4 | 16 - 22 |
| 20 | 833 261 | 186 644 | +2 skup. (1 gra + 1 BK), +1 cecha | 24 / 5 | 19 - 26 |
| 21 | 1 019 905 | 224 972 | +1 skup. | 25 / 5 | 23 - 32 |
| 22 | 1 244 877 | 270 966 | +1 skup. | 26 / 5 | 28 - 38 |
| 23 | 1 515 843 | 326 159 | +1 skup. | 27 / 5 | 33 - 46 |
| 24 | 1 842 002 | 392 390 | +1 skup., +1 cecha | 28 / 6 | 40 - 55 |
| 25 | 2 234 392 | 471 868 | +2 skup. (1 gra + 1 BK) | 30 / 6 | 48 - 66 |
| 26 | 2 706 260 | 567 241 | +1 skup. | 31 / 6 | 58 - 80 |
| 27 | 3 273 501 | 681 689 | +1 skup. | 32 / 6 | 70 - 96 |
| 28 | 3 955 190 | 819 026 | +1 skup., +1 cecha | 33 / 7 | 84 - 115 |
| 29 | 4 774 216 | 983 831 | +1 skup. | 34 / 7 | 101 - 138 |
| 30 | 5 758 047 | 1 181 597 | +2 skup. (1 gra + 1 BK) | 36 / 7 | 121 - 166 |
| 31 | 6 939 644 | 1 418 916 | +1 skup. | 37 / 7 | 145 - 200 |
| 32 | 8 358 560 | 1 703 699 | +1 skup., +1 cecha | 38 / 8 | 174 - 240 |
| 33 | 10 062 259 | 2 045 438 | +1 skup. | 39 / 8 | 209 - 288 |
| 34 | 12 107 697 | 2 455 525 | +1 skup. | 40 / 8 | 251 - 346 |
| 35 | 14 563 222 | 2 947 630 | +2 skup. (1 gra + 1 BK) | 42 / 8 | 301 - 415 (0,8 - 1,1 roku) |
| 36 | 17 510 852 | 3 538 156 | +1 skup., +1 cecha | 43 / 9 | 362 - 498 (1,0 - 1,4 roku) |
| 37 | 21 049 008 | 4 246 787 | +1 skup. | 44 / 9 | 434 - 598 (1,2 - 1,6 roku) |
| 38 | 25 295 795 | 5 097 144 | +1 skup. | 45 / 9 | 521 - 717 (1,4 - 2,0 roku) |
| 39 | 30 392 939 | 6 117 572 | +1 skup. | 46 / 9 | 625 - 861 (1,7 - 2,4 roku) |
| 40 | 36 510 511 | 7 342 086 | +2 skup. (1 gra + 1 BK), +1 cecha | 48 / 10 | 750 - 1 033 (2,1 - 2,8 roku) |
| 41 | 43 852 597 | 8 811 503 | +1 skup. | 49 / 10 | 901 - 1 240 (2,5 - 3,4 roku) |
| 42 | 52 664 100 | 10 574 803 | +1 skup. | 50 / 10 | 1 081 - 1 488 (3,0 - 4,1 roku) |
| 43 | 63 238 903 | 12 690 763 | +1 skup. | 51 / 10 | 1 297 - 1 786 (3,6 - 4,9 roku) |
| 44 | 75 929 666 | 15 229 915 | +1 skup., +1 cecha | 52 / 11 | 1 557 - 2 143 (4,3 - 5,9 roku) |
| 45 | 91 159 581 | 18 276 898 | +2 skup. (1 gra + 1 BK) | 54 / 11 | 1 868 - 2 572 (5,1 - 7,1 roku) |
| 46 | 109 436 479 | 21 933 277 | +1 skup. | 55 / 11 | 2 242 - 3 086 (6,2 - 8,5 roku) |
| 47 | 131 369 756 | 26 320 932 | +1 skup. | 56 / 11 | 2 690 - 3 704 (7,4 - 10,2 roku) |
| 48 | 157 690 688 | 31 586 118 | +1 skup., +1 cecha | 57 / 12 | 3 228 - 4 445 (8,9 - 12,2 roku) |
| 49 | 189 276 806 | 37 904 341 | +1 skup. | 58 / 12 | 3 874 - 5 334 (10,6 - 14,7 roku) |
| 50 | 227 181 147 | 45 486 209 | +2 skup. (1 gra + 1 BK) | 60 / 12 | 4 649 - 6 401 (12,8 - 17,6 roku) |
| 51 | 272 667 356 | 54 584 450 | +1 skup. | 61 / 12 | 5 579 - 7 681 (15,3 - 21,1 roku) |
| 52 | 327 251 806 | 65 502 340 | +1 skup., +1 cecha | 62 / 13 | 6 695 - 9 217 (18,4 - 25,3 roku) |
| 53 | 392 754 146 | 78 603 808 | +1 skup. | 63 / 13 | 8 034 - 11 061 (22,1 - 30,4 roku) |
| 54 | 471 357 954 | 94 325 569 | +1 skup. | 64 / 13 | 9 641 - 13 273 (26,5 - 36,5 roku) |
| 55 | 565 683 523 | 113 191 682 | +2 skup. (1 gra + 1 BK) | 66 / 13 | 11 569 - 15 928 (31,8 - 43,8 roku) |
| 56 | 678 875 205 | 135 831 018 | +1 skup., +1 cecha | 67 / 14 | 13 883 - 19 114 (38,1 - 52,5 roku) |
| 57 | 814 706 223 | 162 998 221 | +1 skup. | 68 / 14 | 16 660 - 22 936 (45,8 - 63,0 roku) |
| 58 | 977 704 444 | 195 598 865 | +1 skup. | 69 / 14 | 19 992 - 27 524 (54,9 - 75,6 roku) |
| 59 | 1 173 303 309 | 234 719 638 | +1 skup. | 70 / 14 | 23 990 - 33 029 (65,9 - 90,7 roku) |
| 60 | 1 408 022 947 | 281 664 565 | +2 skup. (1 gra + 1 BK), +1 cecha | 72 / 15 | 28 788 - 39 635 (79,1 - 108,9 roku) |
| 61 | 1 689 687 512 | 337 998 478 | +1 skup. | 73 / 15 | 34 546 - 47 562 (94,9 - 130,7 roku) |
| 62 | 2 027 685 990 | - (maks.) | +1 skup. | 74 / 15 | - |

Uwaga o poziomie 1: po kreatorze postac ma poziom 0 i 0 PD [K] HD:128-139 (ClearHero), CCC:41-44, 151-153; pierwszy 1 PD daje L1 i +1 skupienia.
W Twoich zapisach L1 jest juz w dobie 0,04 [P] (naglowki .sav) - w praktyce startujesz z L1.

## 2. Punkty cech i skupienia - skad i ile

| Skad | Skupienie | Cechy | Zrodlo |
|---|---|---|---|
| Kreator: start | 0 (kreator zeruje pule) | +2 do kazdej z 6 cech gry (Wisdom: patrz wiersz BK nizej) | [K] CCC:151-162, HD:128-139; BK wyjmuje Wisdom z `Attributes.All` (rozdz. 7) |
| Kreator ROT: rodzice | +1 w 3 umiejetnosciach (= 3) | +2 (89 opcji); +1 w 4 opcjach Braavos | [K] RCCB:479-491, 647-659 |
| Kreator ROT: dziecinstwo, edukacja, mlodosc, doroslosc | po +2 (razem 8) | po +1 (razem 4) | [K] RCCB:4887-4890, 155-159 |
| Kreator: wiek 20 / 30 / 40 / 50 | wolne 2 / 4 / 6 / 8 | wolne 1 / 2 / 3 / 4 | [K] RCCB:7477-7603 |
| Banner Kings: Wisdom | - | Wisdom min. 2 | [K] BKF `BannerKings.Behaviours\BKSkillBehavior.cs`:93-102 (wolane po kreatorze: :32, 80) |
| Kazdy poziom | +1 | - | [K] HD:338 |
| Co 4 poziomy (4, 8 ... 60) | - | +1 | [K] HD:339-342 |
| BK: co 5 poziomow (5, 10 ... 60) | +1 | - | [K] BKEdu:88-93 |
| BK: ksiazka nie-umiejetnosci | +1 skupienia w jej umiejetnosci (gdy < 5), inaczej +1 000 PD | - | [K] BKF `BannerKings.Managers.Education.Books\BookType.cs`:44-58 |
| BK: styl zycia (lifestyle) | **-1 niewydany punkt za kazdy perk stylu** | - | [K] BKF `...\Lifestyles\Lifestyle.cs`:18, 93-108 |
| Kreator: premia kultury (+1 skupienia, +10) | w kodzie tylko wyswietlana - nie wiem, czy ja dostajesz | - | [K] `CharacterCreationCultureVM.cs`:154-155 (rozdz. 7) |

Kazda opcja kreatora daje tez +10 do swoich umiejetnosci, ale PD postaci sa po niej zerowane - umiejetnosci z kreatora nie licza sie do poziomu [K] CCC:118.
Koszt 1 punktu skupienia = 1 punkt [K] HD:407-410. Maks. cecha 10 [K] DCDM:61, HD:367; maks. skupienie 5 na umiejetnosc [K] DCDM:59, HD:400.

Ile masz razem (kreator ROT, start w wieku 20; Braavos -1 cecha) [S z liczb K]:

| Moment | Cechy (z 70 mozliwych) | Skupienie (z 120 mozliwych) |
|---|---:|---:|
| po kreatorze (L1 po pierwszym PD) | 20 + 1 wolna = 21 | 11 + 2 wolne + 1 = 14 |
| L10 | 21 + 2 = 23 | 13 + 12 = 25 |
| L20 | 21 + 5 = 26 | 13 + 24 = 37 |
| L30 | 21 + 7 = 28 | 13 + 36 = 49 |
| L40 | 21 + 10 = 31 | 13 + 48 = 61 |
| L62 (maks.) | 21 + 15 = 36 (start w wieku 50: 39) | 13 + 74 = 87 (start w wieku 50: 93) |

Bez perkow stylu zycia BK (kazdy zabiera 1 skupienie) i bez ksiazek. 7 cech: Vigor, Control, Endurance, Cunning, Social, Intelligence [K] `rozwoj\gra\dec\DefaultCharacterAttributes.cs`:56-61
+ Wisdom [K] BKF `BannerKings.Managers.Skills\BKAttributes.cs`:47-48. 24 umiejetnosci: 18 gry [K] `rozwoj\gra\dec\DefaultSkills.cs`:88-105, 3 NavalDLC [K] `rozwoj\gra\dec\NavalSkills.cs`:44-58,
3 BK (Scholarship, Theology, Lordship - wszystkie od Wisdom) [K] BKF `BannerKings.Managers.Skills\BKSkills.cs`:29-33; lista 24 w logu [P] `Armoury-2026-10-14_03-24-46.log`:729.

Postacie NPC (lordowie, nowi bohaterowie): pula = (poziom - 1) skupien + FocusPointsAtStart 5 oraz (poziom - 1)/4 + 15 cech [K] HD:282-286, DCDM:63, 69;
startowe PD NPC = max(1, suma(2 x umiejetnosc^2,2) - 2000) [K] HD:288-292.

## 3. Ktore mody co zmieniaja (u Ciebie)

**Ktory model rozwoju jest czynny.** Gra bierze ostatni dodany model [K] `TaleWorlds.CampaignSystem\GameModels.cs`:261. Kolejnosc: modele gry, potem `InitializeGameStarter` wszystkich modulow,
potem `OnGameStart` wszystkich modulow [K] `TaleWorlds.CampaignSystem\Campaign.cs`:1384-1391. NavalDLC dodaje swoj model w `InitializeGameStarter`
[K] `rozwoj\mody\dec\naval\NavalDLCSubModule.cs`:65-71, 196, a Banner Kings w `OnGameStart` [K] `rozwoj\mody\dec\bk\BannerKings.Main.cs`:52, 215 - **wygrywa BKLearningModel**
(sprawdzone w tej syntezie; raport `gra` rozdz. 10 wskazywal na NavalDLC po kolejnosci modulow - to nie ma tu znaczenia, bo BK dodaje model w innej fazie).
BKLearningModel dziedziczy po modelu gry i **nie nadpisuje zadnej metody** [K] BKF `BannerKings.Models.Vanilla\BKLearningModel.cs`:14-91 - liczy jak gra 1.4.8.
Skutek uboczny: premia NavalDLC +6 skupienia na start (dla NPC) przepada [K] `rozwoj\mody\dec\naval\NavalCharacterDevelopmentModel.cs`:10, 22. Gracza to nie dotyczy:
kreator zeruje pule punktow (`ClearHero`: `UnspentFocusPoints = 0`) [K] HD:128-139, CCC:41-44, 151-153.
Zaden mod nie latkuje Harmony metod modelu rozwoju (skan DLL `rozwoj\mody\skan.txt`; powtorzony w weryfikacji `rozwoj\weryfikacja\skan.txt`, rozdz. 7).
BK latkuje za to liste cech gry (`Attributes.All` bez Wisdom) i okno postaci (kafelek Wisdom z "+") - patrz rozdz. 7; na liczby z tego pliku to nie wplywa.

| Mod | Co zmienia | U Ciebie | Zrodlo |
|---|---|---|---|
| **Gra 1.4.8** | krzywa poziomow, 1 skupienie/poziom, cecha co 4, maks. 10 / 5, L62, wzory nauki | **dziala** (przez model BK) | [K] DCDM:59-69, 81-93, 113-120, 206-244 |
| **Banner Kings** - model rozwoju | wlasne reguly (sufit 500, "aptitude", min. 0,05) maja inne sygnatury niz metody gry | **martwy kod** - liczy jak gra | [K] BKF `BKLearningModel.cs`:42-91 |
| **Banner Kings** - awans | +1 skupienia na poziomach 5, 10 ... 60 | dziala | [K] BKEdu:31, 88-93 |
| **Banner Kings** - Wisdom i 3 umiejetnosci | 7. cecha (min. 2), Scholarship / Theology / Lordship; Wisdom wyjeta z listy cech gry `Attributes.All`, a w oknie postaci doklejony jej kafelek z "+" (punkty cech mozna na nia wydac - z kodu) | dziala | [K] BKF `BKSkillBehavior.cs`:93-102, `BKSkills.cs`:29-33; `rozwoj\weryfikacja\dec\BannerKings.Behaviours.Patches.AttributesPatch.cs`:10, 26-28, `...CharacterDeveloperWisdomTilePatch.cs`:24, 65-76 |
| **Banner Kings** - ksiazki | umiejetnosci: 1 500 PD; inne: 300 PD + 1 skupienia (przy pelnym +1 000 PD) | dziala | [K] BKF `BookType.cs`:44-58 |
| **Banner Kings** - styl zycia | perk = 1 niewydany punkt skupienia; wymog: suma 2 umiejetnosci >= 80 x (perki + 1) | dziala | [K] BKF `Lifestyle.cs`:18, 93-108 |
| **Banner Kings** - perki Tutor / Teacher | dzieci rodu przy dorosnieciu: +1 cechy / +1 skupienia | dziala | [K] BKEdu:178-179, 430, 447 |
| **Banner Kings** - Charm z relacji | ponizej progu 0 PD, powyzej x mnoznik | prog **5**, mnoznik **0,5** | [U] `ModSettings\Global\BannerKings\BannerKings.json`:48-49; [K] BKF `BKDiplomacyModel.cs`:74-86 |
| **Banner Kings** - Gladiator | +200% PD w turnieju, ale sprawdza styl **ofiary** | dziala odwrotnie (blad BK?) | [K] BKF `BannerKings.Patches\VanillaModelTweakPatches.cs`:505-521 |
| **NavalDLC** | 3 umiejetnosci (Mariner = END+CNG, Boatswain = CTR+SOC, Shipmaster = VIG+INT); +50% PD z ciosu na morzu (nie-bohaterowie); +6 skupienia na start NPC | umiejetnosci i PD: tak; **+6: nie** (przegrywa z BK) | [K] `NavalSkills.cs`:44-58, `NavalDLCCombatXpModel.cs`:21-31 |
| **ROT-Core** - kreator | zastepuje kreator gry (rodzice +2 cechy i 3 skupienia; 4 etapy po +1 cechy i +2 skupienia) | dziala | [K] `ROTCharacterCreationPatches.cs`:16-22, RCCB (rozdz. 2) |
| **ROT-Core** - sluzba u lorda | PD x `GlobalXPBonus` | **1,00000072** = bez zmian (domyslnie 2,5) | [U] `RealmOfThrones\RealmOfThrones_v1.json`:18; [K] `EnlistmentPatches.cs`:629-637 |
| **RBM** (Combat) | PD z ciosu = 0,4 x (sila ofiary + 0,5) x (sila atakujacego + 0,5) x 30; mnoznik trudnosci strzalu 1,25-3,0 | wlaczony (`Enabled 1`) | [U] `Configs\RBM\config.xml`:15-16; [K] `RBMCombat\CampaignChanges.cs`:97-164 |
| **Armoury** - FairXp | arena 20%, turniej 50%, bitwa: PD x (obrazenia + HP za zabicie)/30, w granicach 0,25-8 | dziala | [U] `Armoury.json`:43-46; [K] `FairXpPatch.cs`:38-56; [P] `Armoury-2026-10-14_03-56-00.log`:15 |
| **Armoury** - XpFair | strzal 10/20/17/10 PD tylko w bitwie (zamiast RBM 30/60/50/30), Steward z jedzenia, Scouting 10/h, paser 25, RBL 0 | dziala (domyslne) | [K] `XpFair.cs`:24-46; [P] tamze:245 |
| **Armoury** - kuznia | PD kucia za bron max(50, 500 x tier) | dziala | [U] `Armoury.json`:260-261 |
| **Armoury** - DonationXpLaw | oddany sprzet nie daje PD | dziala (domyslne) | [K] `Settings.cs`:431 |
| **TournamentsXPanded** | turniej 0,35, arena 0,2 | **nie dziala** (w 1.4.8 tych stalych nie ma w tej metodzie; RBM i tak ja zdejmuje) | [U] `TournamentsXPanded.json`:52-55; [K] `DefaultCombatXpModel.cs`:46, 60-69 |
| **Spoils of War** | Roguery 1,5 x 1,0 PD na godzine lupu; dar sprzetu Trade / Charm | Roguery tak; dar **nie** (blokuje Armoury) | [U] `RealisticLootSettings.json`:68; [K] `LootCollectionBehavior.cs`:2899-2909 |
| **RealisticBannerlord** | naprawa Crafting 30; szpieg Roguery 50/150; glos Charm 100 | naprawa tak; reszta **nie** (Armoury RBL = 0) | [K] `EquipmentRepairBehavior.cs`:56 |
| **RealisticCaptivity** | w niewoli: praca Athletics 25 PD/dzien, warta 40; zanik miesni -1 poziom umiejetnosci walki (min. 20) | dziala (tylko w niewoli) | [K] `McmSettings.cs`:184-192, 376, 408 |
| **CrashScribe** - HeroSinew | lordom AI podnosi Athletics / Bow / Crossbow do wymogu sprzetu (bez PD postaci) | tylko AI | [K] `Mends.cs`:4575-4614 |
| **BKROTPatch**, **BirthAndDeath**, GrandTourney, ForgeView, DTE, StrategicCampaignAI145, Diplomacy i reszta | nic w rozwoju postaci (BirthAndDeath decyduje tylko, czy postac sie starzeje) | - | skan DLL `rozwoj\mody\skan.txt` |
| **FastMode**, **AIInfluence** | FastMode: PD umiejetnosci x0,3 | **wylaczone** | [U] `LauncherData.xml`; [P] `rgl_log_32272.txt`:88 |
| Pliki MCM `BetterAttributePoints`, `BetterAttributes`, `DetailedCharacterCreation` | stare ustawienia | **modow nie ma** - nie dzialaja | `ModSettings\Global` vs `Modules` |

Mnoznik ogolny PD (GenericXpModel) = gra: 1,0 (towarzysz gracza z perkiem Natural Leader 1,2) - latka RBM dotyczy tylko trybu fabularnego, a Ty grasz Sandbox
[K] `SandBoxManager.cs`:226, `DefaultGenericXpModel.cs`:8-15, `RBMCombat\CampaignChanges.cs`:86-95; [P] `CrashScribe\session-2026-10-14_03-55-59.log`:353. Dziala tylko na PD umiejetnosci, nie postaci.

## 4. Nauka: limit, tempo, sufit (wzory i przyklady)

Wzory [K] DCDM:206-244 (sumowanie czynnikow: ExplainedNumber = baza + baza x suma czynnikow):
- **C** = cecha umiejetnosci (umiejetnosci morskie: srednia z 2 cech), **S** = skupienie w tej umiejetnosci (0-5), **s** = obecny poziom umiejetnosci.
- **Limit nauki** = max(0, (C - 1) x 10) + 30 x S.
- **Tempo nauki** (mnoznik PD umiejetnosci) = 1,25 x (1 + 0,4 x C + S - K), min. 0; K = 0 do limitu, a ponad limit K = 1 + 0,1 x (s - limit).
- **Sufit** (tempo spada do 0) = limit + 4 x C + 10 x S = 14 x C - 10 + 40 x S. Absolutny sufit (C 10, S 5) = **330**.
- **Poziomu postaci nie ma w zadnym z tych wzorow** - dziala tylko przez punkty, ktore za niego dostajesz.

Limit / tempo / sufit dla calych cech (komorka: `limit / tempo / sufit`) [K] wzory wyzej, [S] skrypt `nauka.py`:

| Cecha \ Skupienie | 0 | 1 | 2 | 3 | 4 | 5 |
|---:|---|---|---|---|---|---|
| 1 | 0 / 1,75 / 4 | 30 / 3,00 / 44 | 60 / 4,25 / 84 | 90 / 5,50 / 124 | 120 / 6,75 / 164 | 150 / 8,00 / 204 |
| 2 | 10 / 2,25 / 18 | 40 / 3,50 / 58 | 70 / 4,75 / 98 | 100 / 6,00 / 138 | 130 / 7,25 / 178 | 160 / 8,50 / 218 |
| 3 | 20 / 2,75 / 32 | 50 / 4,00 / 72 | 80 / 5,25 / 112 | 110 / 6,50 / 152 | 140 / 7,75 / 192 | 170 / 9,00 / 232 |
| 4 | 30 / 3,25 / 46 | 60 / 4,50 / 86 | 90 / 5,75 / 126 | 120 / 7,00 / 166 | 150 / 8,25 / 206 | 180 / 9,50 / 246 |
| 5 | 40 / 3,75 / 60 | 70 / 5,00 / 100 | 100 / 6,25 / 140 | **130 / 7,50 / 180** | 160 / 8,75 / 220 | 190 / 10,00 / 260 |
| 6 | 50 / 4,25 / 74 | 80 / 5,50 / 114 | 110 / 6,75 / 154 | 140 / 8,00 / 194 | 170 / 9,25 / 234 | 200 / 10,50 / 274 |
| 7 | 60 / 4,75 / 88 | 90 / 6,00 / 128 | 120 / 7,25 / 168 | 150 / 8,50 / 208 | 180 / 9,75 / 248 | 210 / 11,00 / 288 |
| 8 | 70 / 5,25 / 102 | 100 / 6,50 / 142 | 130 / 7,75 / 182 | 160 / 9,00 / 222 | 190 / 10,25 / 262 | 220 / 11,50 / 302 |
| 9 | 80 / 5,75 / 116 | 110 / 7,00 / 156 | 140 / 8,25 / 196 | 170 / 9,50 / 236 | 200 / 10,75 / 276 | 230 / 12,00 / 316 |
| 10 | 90 / 6,25 / 130 | 120 / 7,50 / 170 | 150 / 8,75 / 210 | 180 / 10,00 / 250 | 210 / 11,25 / 290 | 240 / 12,50 / 330 |

**Przyklad z pytania: cecha 5, skupienie 3, postac na poziomie 20.** Limit 130, tempo x7,5, sufit 180 - tak samo na L1, L20 i L60.
Ponad limitem tempo spada [K] DCDM:237-242: umiejetnosc 130 -> x7,5; 131 -> x6,125; 140 -> x5,0; 150 -> x3,75; 160 -> x2,5; 170 -> x1,25; 179 -> x0,125; **180 -> 0** [S] `nauka_wynik.txt`.
Na L20 (start w wieku 20, rozdz. 2) masz razem ok. 26 cech i 37 skupien do rozdzielenia na 7 cech i 24 umiejetnosci - czyli np. 5 skupien w 7 umiejetnosciach
albo 3 skupienia w 12.

Inne przyklady [S] `nauka_wynik.txt`:
- nowa umiejetnosc przy cesze 2 i 0 skupienia (tez BK Scholarship przy Wisdom 2): limit 10, tempo x2,25, sufit 18;
- Mariner przy Endurance 4 i Cunning 2 (srednia 3), skupienie 1: limit 50, tempo x4,0, sufit 72;
- cecha 10, skupienie 5: limit 240, tempo x12,5, sufit 330.

**PD umiejetnosci na poziom umiejetnosci** [K] DCDM:95-103 (`xp[0] = 30; n += 10 + i`; tryb Fast x0,3 wylaczony [K] DCDM:104-110, [U] LauncherData.xml):
1 poziom kosztuje 30 + 10 x (s - 1) + (s - 1) x s / 2 PD umiejetnosci.

| Poziom umiejetnosci | PD umiejetnosci na ten 1 poziom | PD umiejetnosci lacznie od 0 |
|---:|---:|---:|
| 1 | 30 | 30 |
| 10 | 165 | 915 |
| 25 | 570 | 6 350 |
| 50 | 1 745 | 34 575 |
| 75 | 3 545 | 100 300 |
| 100 | 5 970 | 219 150 |
| 130 | 9 705 | 453 895 |
| 150 | 12 695 | 678 725 |
| 180 | 17 930 | 1 138 470 |
| 200 | 21 920 | 1 538 300 |
| 250 | 33 645 | 2 922 875 |
| 300 | 47 870 | 4 957 450 |
| 330 | 57 605 | 6 542 195 |

**PD umiejetnosci -> PD postaci** [K] HD:198-234: gra najpierw dodaje surowe PD do postaci (zaokraglone przy kazdym zdarzeniu [K] HD:228), a dopiero potem mnozy je
przez tempo nauki dla umiejetnosci. Czyli **PD postaci = PD umiejetnosci / tempo nauki**. Przyklad (cecha 5, skupienie 3): umiejetnosc 120 -> 121 kosztuje 8 490 PD umiejetnosci
= ok. 1 132 PD postaci (tempo 7,5); 170 -> 171 kosztuje 16 265 = ok. 13 012 PD postaci (tempo 1,25) [S]. Im wyzsze tempo nauki, tym mniej PD postaci za ten sam poziom umiejetnosci.

Ile PD postaci daje wbicie JEDNEJ umiejetnosci od 0 [S] `nauka_wynik.txt` (zgodne z raportem `gra` rozdz. 9):

| Cecha / skupienie | Limit | Sufit | PD postaci 0 -> limit | PD postaci 0 -> sufit |
|---|---:|---:|---:|---:|
| 2 / 0 | 10 | 18 | 407 | 6 908 |
| 3 / 1 | 50 | 72 | 8 644 | 94 671 |
| 5 / 3 | 130 | 180 | 60 519 | 641 362 (= ok. L18) |
| 5 / 5 | 190 | 260 | 132 839 | 1 404 520 (= ok. L22) |
| 10 / 5 | 240 | 330 | 207 837 | 2 334 855 (= ok. L25) |

## 5. Tempo w praktyce i rok 364 doby

**Pomiar.** Logi nie zapisuja poziomu postaci; poziom gracza jest tylko w naglowku zapisu [K] `SaveHandler.cs`:187, 197. Twoje kampanie [P] (naglowki .sav, raport `praktyka` rozdz. 3):

| Zapis | Postac | Doba | Poziom | PD na dobe [S] |
|---|---|---:|---:|---:|
| `IronmangeCUdegikFtb.sav` (30.08) | Przemyslaw | 671,8 | 29 | 7 106 - 8 571 |
| `IronmanPEh4X2ByGArW.sav` (04.10) | Ithauser | 1 237,5 | 34 | 9 784 - 11 768 |

Postac autotestu stojaca w miescie: 1 364 PD w 727 dobach -> L2 [P] (`autotest-2026-10-13-2345.sav`, ksiega Armoury E8/P1). Przecietny lord AI: 484 PD na dobe
(rok 1: 668, rok 2: 325) [P]; typowy lord ma L22-27 i zyskuje 0,4-0,9 poziomu w 1. roku [S].

**Rok 364 doby** [U] `Armoury.json`:47-48 (`LongYearEnabled true`, `WeeksPerSeason 13`); [P] `Armoury-2026-10-13_21-14-13.log`:210:
- PD i awanse licza sie od dob gry, nie od lat - **na dobe kalendarz nic nie zmienia**.
- Wiek: 1 rok wieku = 364 doby (bez zmiany kalendarza 84), postac starzeje sie **4,33x wolniej** [K] `Hero.cs`:472-482, `CampaignTime.cs`:188, 197.
- Starosc od 67 lat (ROT) [K] `ROTAgeModel.cs`:18; polowa postaci umiera przed 83,6 rokiem zycia - w latach tak samo w obu kalendarzach [S] (`DefaultHeroDeathProbabilityCalculationModel.cs`:18-31).
- BirthAndDeath = tylko przelacznik cyklu zycia (domyslnie wlaczony) [K] `BirthAndDeathOptionsProvider.cs`:9-20, `CampaignOptions.cs`:228.

Poziom gracza od L1 po N latach w Twoim tempie (7 106 - 9 784 PD na dobe) [S] `rozwoj\praktyka\poziomy_wynik.txt`:

| Po | rok 364 doby | rok 84 doby (dla porownania) |
|---|---|---|
| 1 roku | L25,7 - 27,4 | L18,4 - 19,9 |
| 2 latach | L29,4 - 31,1 | L21,8 - 23,4 |
| 3 latach | L31,6 - 33,3 | L23,8 - 25,5 |
| 5 latach | L34,3 - 36,1 | L26,5 - 28,2 |
| 10 latach | L38,1 - 39,9 | L30,2 - 31,9 |
| do starosci: od 20 do 67 lat | L46,6 - 48,3 (17 108 dob) | L38,5 - 40,3 (3 948 dob) |

W tym, co realnie zagrasz (1-3 tys. dob), poziomy sa te same w obu kalendarzach - zmienia sie tylko wiek: 1 237 dob to +3,4 roku zamiast +14,7 roku.
Start w wieku 50: przy 84 dobach starosc przyszlaby po 1 428 dobach, przy 364 dopiero po 6 188 [S].

## 6. Czego nie wiem

1. **Zaden log nie wypisuje czynnego modelu rozwoju** - "wygrywa Banner Kings" to wniosek z kodu (kolejnosc faz sprawdzona). Dla Ciebie zmienia to tylko start NPC (5, nie 11 skupien). Sprawdzenie w grze: nowy dorosly bohater powinien miec 5 startowych skupien.
2. **Premia kultury w kreatorze (+1 skupienia, +10)** - w kodzie jest tylko jej wyswietlenie (`CharacterCreationCultureVM.cs`:154-155); nikt nie wola `GetFocusToAddByCulture`. Czy ROT daje ja inna droga - nie wiem.
3. **Ile dni realnie zajmie awans w nowej kampanii** - tempo mam z 2 kampanii Ironman sprzed 09.10 (przed m.in. "oddany sprzet nie uczy"); obecne moze byc nizsze, o ile - nie wiem. Tempo rosnie tez z czasem, wiec kolumna dni w rozdz. 1 to przyblizenie.
4. **Twoje dokladne PD postaci** - zapis jest skompresowany, naglowek podaje tylko poziom.
5. **Czy okno postaci pokazuje L0 przed pierwszym PD** - kod daje 0, zapisy pokazuja L1 juz w dobie 0,04.
6. **Czy Wisdom da sie podnosic punktami cech w oknie postaci** - z kodu TAK: BK wyjmuje Wisdom z listy `Attributes.All` (latka na getter), ale dokleja jej kafelek z "+" do okna postaci, a "Zatwierdz" zapisuje kazdy kafelek przez `AddAttribute` (maks. 10) - rozdz. 7. W grze (czy kafelek sie wyswietla) nie sprawdzone. (Poprzednie zdanie "nalezy do `Attributes.All`" bylo bledne.)
7. **PD za cios w bitwie przy RBM liczbowo** - zalezy od "sily" jednostek RBM, nie liczylem; jest tylko wzor.
8. **Ksiazki ROT z BKROTPatch** - ktore umiejetnosci i ile ich jest; mechanika jak w BK.
9. **BK Gladiator patrzy na styl zycia ofiary** - nie wiem, czy celowo.
10. **Prawdziwe poziomy lordow w czasie** - zaden log nie zapisuje `Hero.Level`; liczby lordow to szacunek.
11. **Czy w nowej kampanii wlaczysz cykl zycia (BirthAndDeath) i jaki wiek startu wybierzesz** - wybor w grze, nie w plikach.

## 7. Weryfikacja (14.10, drugi przeglad - sceptyk; tylko odczyt, gry nie uruchamiano, bez commita)

`WER` = `scratchpad\rozwoj\weryfikacja`: moje dekompilacje `WER\dec` (ilspycmd 9.1 z zainstalowanych DLL), skrypty `WER\sprawdz.py` -> `WER\sprawdz_wynik.txt`
i `WER\skan.py` -> `WER\skan.txt`, kopia tego pliku sprzed poprawek `WER\ROZWOJ-POSTACI.przed-weryfikacja.md`.

**Sprawdzone samodzielnie (zgodne z plikiem):**
1. **Wzor PD na poziom** [K] DCDM:81-93, przeliczony recznie krok po kroku (krok = krok + 1 000 + krok/5, dzielenie calkowite):
   - L2: 1 + 1 000 = **1 001** PD lacznie; krok do L3 = 1 000 + 1 000 + 200 = 2 200.
   - L10: 3 201 -> 6 841 -> 12 209 -> 19 650 -> 29 579 -> 42 493 -> 58 989 -> **79 784**; krok do L11 = 25 954.
   - L25: 1 842 002 + 392 390 = **2 234 392**; krok do L26 = 471 868.
   - Skrypt (arytmetyka int32 jak w C#) potwierdza wszystkie 62 wiersze tabeli rozdz. 1: PD lacznie, PD do nastepnego, sumy punktow.
     Kolumna dni przy L54-61 rozni sie o 1-3 doby tylko przez zaokraglenie tempa (7 106 zamiast 7 106,6) - bez poprawki.
2. **Punkty na poziom** [K] HD:336-344: +`FocusPointsPerLevel` (= 1, DCDM:67) zawsze; +1 cechy gdy `poziom % LevelsPerAttributePoint` (= 4, DCDM:65) == 0;
   BK +1 skupienia gdy `poziom % 5 == 0` - sprawdzone w zainstalowanym BannerKings.dll [K] `rozwoj\mody\dec\bk\BannerKings.Behaviours.BKEducationBehavior.cs`:31, 88-93
   (dekompilacja `BKF` to ten sam build - rozni sie tylko nazwami zmiennych). Do L62: 62 + 12 = 74 skupienia, 15 cech. Maks. L62: `SkillsRequiredForLevel(63)` = int.MaxValue,
   `CheckLevel` staje [K] DCDM:113-120, HD:252-269.
3. **PD postaci i nauka:** PD postaci = surowe PD umiejetnosci 1:1, przed mnoznikiem i tempem [K] HD:198-235. Wzory [K] DCDM:206-244 z arytmetyka `ExplainedNumber`
   (wynik = baza + baza x suma czynnikow) [K] `cs_full\TaleWorlds.CampaignSystem\ExplainedNumber.cs`:122, 156. Skrypt: cecha 5 / skupienie 3 = limit 130, tempo 7,5, sufit 180;
   cecha 10 / skupienie 5 = 240 / 12,5 / 330. Tabela PD umiejetnosci i "PD postaci 0 -> limit / sufit" (rozdz. 4) zgodne (roznica 1 PD z zaokraglenia).
4. **Ktory model rozwoju naprawde liczy w Twojej grze (1.4.8):**
   - gra bierze ostatni dodany model: `GameModelsManager.GetGameModel<T>` idzie od konca listy [K] `WER\dec\GameModelsManager.cs`:15-25 (TaleWorlds.Core.dll), wolane w `GameModels.cs`:261;
   - kolejnosc dodawania: modele gry (`SandBoxManager.Initialize`) -> `InitializeGameStarter` WSZYSTKICH modulow -> `OnGameStart` WSZYSTKICH modulow [K] `Campaign.cs`:1384, 1385, 1391;
     obie petle ida po modulach w kolejnosci ladowania [K] `WER\dec\MBGameManager.cs`:102-106, 159-164; `SandBoxGameManager` ich nie nadpisuje [K] `WER\dec\SandBoxGameManager.cs`:19-123;
   - NavalDLC dodaje swoj model w `InitializeGameStarter` [K] `rozwoj\mody\dec\naval\NavalDLCSubModule.cs`:65-71, 196, a BK w `OnGameStart`, bez zadnego warunku
     [K] `WER\dec\bkinst\BannerKings\Main.cs`:52, 215 -> **wygrywa BKLearningModel i to niezaleznie od kolejnosci modulow** (u Ciebie BK 11., NavalDLC 13.
     [U] `LauncherData.xml`, [P] `rgl_log_32272.txt`:88 - tu bez znaczenia, bo kazde InitializeGameStarter idzie przed kazdym OnGameStart);
   - BKLearningModel w zainstalowanym DLL: zero `override`; jego wlasne metody (sufit 500, "aptitude", Magnum Opus, min. 0,05) maja inne sygnatury i nikt ich nie wola
     [K] `WER\dec\BKLearningModel.cs`:14-107. Pelna dekompilacja zainstalowanego BK z referencjami (`WER\dec\bkinst`, 757 plikow, wszystkie atrybuty Harmony odczytane):
     zadnej latki na `HeroDeveloper`, `CharacterDevelopmentModel`, `AddSkillXp`, `GetFocusFactor`;
   - skan DLL wszystkich 37 czynnych modulow [K] `WER\skan.txt`: nazwy metod modelu rozwoju maja tylko BK i NavalDLC; pozostale trafienia "HeroDeveloper" to ROT (sluzba u lorda, u Ciebie x1),
     Armoury (licznik PD + kuznia), CrashScribe i RealisticCaptivity (`SetInitialSkillLevel`), StoryMode (nieczynny w Sandbox).
5. **Twoje ustawienia MCM** [U] `Configs\ModSettings\Global\<mod>\*.json`: zaden klucz nie zmienia krzywej poziomow, punktow na poziom, maksimow ani wzorow nauki.
   Klucze o PD w czynnych modach: BK `CharmXpMultiplier` 0.5 i `CharmXpThreshold` 5; ROT `GlobalXPBonus` 1.00000072; Spoils `RogueryXpGainRate` 1.0;
   Armoury `ArenaXpPercent` 20, `TournamentXpPercent` 50, `WeaponXpCapPerTier` 500; TXP 0.35 / 0.2 - nie dziala, bo transpiler szuka stalej 0.33 w `GetXpFromHit`
   [K] `rozwoj\mody\dec\txp\...\GetXpFromHit_TournamentAdj.cs`:14, 45-66, a w 1.4.8 stala siedzi w `GetXpfMultiplierForMissionType` [K] `DefaultCombatXpModel.cs`:46, 60-69.
   Foldery MCM `BetterAttributePoints`, `BetterAttributes`, `DetailedCharacterCreation` - tych modow nie ma w `Modules`. RBM `RBMCombat Enabled 1` [U] `Configs\RBM\config.xml`:15-16.

**Poprawione:**
1. **Rozdz. 6 pkt 6 (bylo bledne):** Wisdom NIE nalezy do `Attributes.All`. BK ma latke Harmony na getter `Attributes.All`, ktora ja z tej listy wyjmuje
   [K] `WER\dec\BannerKings.Behaviours.Patches.AttributesPatch.cs`:10, 26-28, i dokleja kafelek Wisdom z "+" w oknie postaci
   [K] `WER\dec\BannerKings.Behaviours.Patches.CharacterDeveloperWisdomTilePatch.cs`:24, 65-76; "Zatwierdz" zapisuje kazdy kafelek przez `AddAttribute` (maks. 10)
   [K] `WER\dec\CharacterDeveloperHeroItemVM.cs`:625-636, `WER\dec\CharacterAttributeItemVM.cs`:304, 308-314. Liczby sie nie zmieniaja: kreator daje +2 tylko 6 cechom gry,
   Wisdom = 2 ustawia BK po kreatorze [K] BKF `BKSkillBehavior.cs`:93-102, wiec dalej 20 cech na karcie i 70 maks. Dopisane w rozdz. 0 pkt 4, rozdz. 2 i rozdz. 3.
2. **Rozdz. 0 pkt 10:** "pierwsze poziomy co kilka dni" bylo nieprecyzyjne - w Twoim tempie L1-L5 wpadaja szybciej niz co dobe, L10 co 3-4 doby (rozdz. 1);
   dopisane, ze to srednie z calych kampanii (na poczatku wolniej).
3. **Rozdz. 0 pkt 11 i rozdz. 5:** "ok. L48 zamiast L40" bylo liczone tylko z szybszej kampanii. Dla obu: L46,6-48,3 (rok 364 d) zamiast L38,5-40,3 (rok 84 d)
   [S] `WER\sprawdz_wynik.txt`. "Cale zycie" zmienione na "do starosci" (67 lat to w ROT poczatek starosci [K] `rozwoj\praktyka\dec_rot\ROTAgeModel.cs`:18, nie smierc).
4. **Rozdz. 0 pkt 5:** dopisane "z rodzicami z Braavos 19" (rozdz. 2: +1 zamiast +2 cech w 4 opcjach Braavos).
5. **Rozdz. 0:** dluzsze zdania podzielone, pkt 9 i 11 na podpunkty - poza pkt 1-4 wyzej tresc bez zmian.

**Nadal nie wiem:** czynnego modelu nie potwierdza zaden log - szukalem "BKLearningModel" / "CharacterDevelopmentModel" w `CrashScribe\session-2026-10-1*.log`
i dwoch ostatnich `Modules\Armoury\Armoury-*.log`: zero trafien (to wniosek z kodu). Czy kafelek Wisdom wyswietla sie w grze - nie sprawdzalem (gra w tescie).
Zrodel PD z modow (RBM, Armoury, Spoils, RBL, RC) nie liczylem od nowa - sprawdzilem tylko klucze MCM i TXP. Reszta jak w rozdz. 6.
