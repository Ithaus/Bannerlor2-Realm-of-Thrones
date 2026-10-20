## 2026-10-20 (p5-warsztaty: R3-11b - cena warsztatu po skoku zysku na starcie kampanii)

**Mod:** Armoury | **Galaz:** `w-toku/p5-warsztaty` (od `5189c0fe` = kandydat WGRANIA 92) | **Tor:** S (bez gry - zakaz uruchamiania; build Armoury kod 0, 0 bledow, gen_mcm uruchomiony: Armoury 2065 ustawien) | **Klucz (nowy, domyslnie TAK; w Armoury.json Jeffa go nie ma - wchodzi domyslny z kodu):** `WorkshopPriceForgetsStartup` ("Workshop Price Forgets Startup", grupa Workshops, zaraz pod Workshop Trade Profit Days) | **Zapis:** ten sam klucz `arm_wstrade` (SaveText.Sync), do kazdego warsztatu dopisane 2 pola na koncu: doby pamieci i znacznik rozruchu; starsza DLL czyta 4 pierwsze pola jak dotad, stary zapis bez pol = bez zmian | **Pliki:** NOWY `Armoury/src/WorkshopStartup.cs`; `WorkshopTrade.cs` (wpiecia: pole Mem/Warm w rachunku, CloseDay, okno ceny, RecOf, zapis, sluchacz doby), `Settings.cs`, `McmSettings.cs` (gen_mcm).

### Problem / zrodlo pomyslu
POMYSLY-35 nr 35 (R3-11b), Jeff 20.10: "pozostale tak". Zrodla: `docs/audyt-2026-10-07/1-ekonomia-i-przeplywy.md` (DROBNE 2), `docs/paczki/120-poprawki-po-audycie.md` (pkt 3 - poprawka czesciowa, -10% po 60 dobach), `docs/STAN-PRAC.md` (pamiec roku ceny warsztatu "dalej decyzja projektu").
Cena kupna warsztatu = 3 lata zysku z lepszej z dwoch pamieci (ok. 30 dob i rok). Pamiec roku w pierwszym roku to srednia WSZYSTKICH dob od startu, wiec skok z pierwszych tygodni (puste albo przepelnione polki) siedzi w cenie miesiacami.
Dowod z logu (nowa kampania, `Armoury-2026-10-19_20-56-20.log`, przyklad Lannisport w linii "Warsztaty towarowe"): kuznia zarobila ok. 5 190 d w dobach 0-30 (pamiec roku 167.4 d na dobe w 30. dobie; w 5. dobie jednego dnia ok. 660 d), a w dobach 31-119 srednio ok. 27.5 d na dobe. Cena kupna dla gracza: doba 60 - 56 796, doba 90 - 45 341, doba 119 - 39 342. Liczona z samych dob po rozruchu byloby ok. 25 500 / 23 800 / 22 700 (x2.2 / x1.9 / x1.7 taniej; rachunek z liczb logu, bez punktu wyjscia z cen w 30. dobie). Audyt 07.10: garbarnia z 7-dobowym skokiem x3.6 / x2.1 / x1.6 w 30. / 60. / 100. dobie.

### Zmiana
Regula "rozruch rynku" (wybrana z dwoch wskazowek: pomijanie pierwszych dob; "w pierwszym roku nizsza pamiec" odrzucona, bo zdejmuje na caly rok ochrone przed tanim kupnem warsztatu, ktory miesiac stal):
- Pierwsze 30 dob kampanii (wiek od `CampaignStartTime` modelu czasu, jak ColdStart/Hunger) to rozruch. W tym czasie pamieci warsztatu i cena ida JAK DOTAD (cena idzie za biezacym zarobkiem), a warsztat dostaje znacznik "pamiec ma doby rozruchu".
- Pierwsza doba warsztatu po rozruchu (pierwszy CloseDay z wiekiem >= 30) zaczyna obie pamieci od nowa - dokladnie jak doba 1 nowej kampanii: pamiec miesiaca od oczekiwanego wyniku z cen (juz ulozonego) targu, pamiec roku z samych prawdziwych dob. Skok z rozruchu przestaje liczyc sie w cenie z dnia na dzien, a nie po pol roku.
- Licznik dob warsztatu (Days) zostaje; nowe pole Mem = doby w pamieci (dzielnik srednich, okno "over the last N days" w rozmowie, `RecOf`). Bez rozruchu Mem = Days, wiec wszystko poza regula liczy sie co do grosza jak dotad.
- Dotyczy 100% warsztatow pod paczka warsztatow (bez ukrytych rzemieslnikow), ktorych pamiec zlapala choc jedna dobe rozruchu - takze kupionych przez gracza i zmienionych w rozruchu. Kampania po rozruchu / zapis bez znacznika (kampania Jeffa) - nic sie nie zmienia.
- Symulacja (replika CloseDay, garbarnia z audytu: 7 dob 1456 d, potem 86 d): cena w dobie 31 / 60 / 100 / 199 - dzis 454 277 / 265 589 / 197 598 / 146 273, z regula 93 912 we wszystkich (= cena bez skoku); doby 0-29 bez zmian.
- Linia logu `Warsztaty - rozruch rynku (R3-11b): dzien kampanii N | regula CZYNNA ... | w rozruchu dzis X warsztatow; pamiec od nowa dzis Y, w sesji Z | cena kupna dla gracza w K warsztatach po rozruchu: z regula mediana A, bez reguly B; razem S1 wobec S2 (bez reguly xR); taniej o ponad 25%: L, drozej niz bez reguly: M; najwieksza roznica: <miasto typ cena zamiast cena (zysk...)> | potkniecia dzis D, w sesji E.` Porownanie "bez reguly" liczy obok stary wzor (tylko w sesji, w ktorej minal rozruch, najwyzej rok, nic w zapisie). Doba przejscia - zawsze; poza nia w grze co QuietLogEveryDays dob (u Jeffa 7), w autotescie co dobe. Kampania bez warsztatow w rozruchu - jedna linia na sesje "zaden warsztat nie ma w pamieci dob rozruchu ... ceny bez zmian".

### Ryzyko / co sprawdzic (kontrola calosci w kodzie)
- **Zamknieta ekonomia:** zmienia sie tylko liczba w wycenie (`Build`: cena kupna gracza i odkupu notabla). Zadne zloto ani sztuka nie powstaje i nie znika; utrzymanie, place, cena sprawiedliwa, kapital - bez zmian.
- **Kolizje / inni czytelnicy pamieci:** `RecOf` podaje teraz doby PAMIECI (tak mowi jego opis: "liczba dob w sredniej"). Skutek w nowej kampanii: zmiana branzy za cena (E3-D3, "tracacy od 60 dob w obu pamieciach") i "zmierzony" wynik F4 (`MeasuredOf`, >= 60 dob) licza 60 dob od konca rozruchu, czyli najwczesniej ok. 90. doba zamiast 60. Za to warsztat, ktory zarobil w rozruchu, a potem traci, nie jest juz latami chroniony zyskiem z rozruchu (dzis taki warsztat z 5 000 d z rozruchu i -10 d na dobe mialby pamiec roku > 0 przez ponad rok). Stara kampania - bez zmian (Mem = Days).
- **Przejscie:** w dobie 30 pamiec ma 1 dobe, wiec cena przez pierwsze dni po rozruchu skacze tak samo jak w pierwszych dniach nowej kampanii (punkt wyjscia z cen targu tlumi jeden dobry albo zly dzien). Notabl odkupuje wedle gorszej z pamieci - obie od konca rozruchu, regula "kupno i odsprzedaz w tej samej chwili bez zysku" zostaje.
- **Zapis:** pola dopisane na koncu wiersza; Import przyjmuje 3-6 pol, warsztaty nieruszone w sesji wracaja do zapisu ze wszystkimi polami. Zapis w rozruchu i wczytanie przy wieku >= 30 - znacznik z zapisu, przejscie nastapi. Starsza DLL na nowym zapisie - czyta jak dotad (traci tylko znacznik i Mem).
- **Wylaczenie klucza w trakcie:** pamieci juz zaczete od nowa zostaja (nic nie wraca). Wylaczony w rozruchu, wlaczony po nim - warsztaty ze znacznikiem z wczesniejszych dob zaczna pamiec od nowa w pierwszej dobie po wlaczeniu (tez zgubia doby po rozruchu) - przypadek reczny, opisany.
- **Watki / koszt:** CloseDay i linia doby - watek glowny (ticki dobowe). Na warsztat na dobe jedno wyszukanie w slowniku porownania; linia - ok. 291 wycen raz na dobe (jak linia "Warsztaty towarowe"). Wyjatek - licznik potkniec, funkcja nie gasnie.
- **Nie rusza:** cen w samym rozruchu (dalej za biezacym zarobkiem - w pierwszym miesiacu warsztat moze byc drogi), danych ROT/XML, linii uzbrojenia, ukrytych rzemieslnikow.

### Status
DO SPRAWDZENIA (nie w grze; build OK).

### Decyzje (podjete samodzielnie - Jeff: "ekonomie projektuje Claude")
1. Rozruch = 30 dob kampanii (stala w kodzie; tyle trwal skok kuzni Lannisportu w tescie 19.10, garbarnia z audytu - 7 dob). Wylacznik: `WorkshopPriceForgetsStartup`.
2. Po rozruchu OBIE pamieci od nowa (nie tylko pamiec roku): sama pamiec roku zostawila by pamiec miesiaca ze skokiem jeszcze na 2 miesiace (kuznia: ok. x2.9 w 60. dobie).
3. W samym rozruchu cena jak dotad - nikt nie zna "prawdziwego" zysku przy pustych polkach, a zadanie mowi o tym, zeby skok nie trzymal ceny pol roku.
4. Kampania Jeffa (po rozruchu) bez zmian - regula dziala od nowej kampanii.

### Test w grze (autotest, nowa kampania, >= 60 dob)
- `Warsztaty - rozruch rynku (R3-11b): dzien kampanii` 0-29: "w rozruchu dzis" ok. 291, "pamiec od nowa dzis 0".
- Doba 30: "pamiec od nowa dzis" ok. 291 (wszystkie warsztaty rozliczone tego dnia), "w sesji" tyle samo.
- Doby 31+: "z regula mediana" <= "bez reguly", "razem ... (bez reguly xR)" z R > 1 i "taniej o ponad 25%" > 0 przez pierwsze miesiace; "potkniecia ... 0".
- Linia "Warsztaty towarowe" (przyklad Lannisport): od 31. doby "zysk sredni z N dob (pamiec M)" z N < M (pamiec od konca rozruchu).
- Kampania Jeffa / zapis po rozruchu: jedna linia "zaden warsztat nie ma w pamieci dob rozruchu ... ceny bez zmian".

### Dla Jeffa (po ludzku)
Na poczatku nowej kampanii rynek sie dopiero uklada: polki sa puste albo przepelnione, wiec niektore warsztaty przez kilka tygodni zarabiaja kilka razy wiecej niz pozniej. Dotad warsztat pamietal te dni przez caly rok i przez pol roku kosztowal 2-3 razy za duzo (np. kuznia w Lannisporcie: 57 tys. w 60. dniu, choc zarabiala na 25 tys.). Teraz pierwszy miesiac kampanii to "rozruch": po nim kazdy warsztat liczy swoj zarobek od nowa, juz z ulozonego rynku, wiec od drugiego miesiaca cena odpowiada temu, co warsztat naprawde zarabia. W samym pierwszym miesiacu cena idzie jak dotad za biezacym zarobkiem. Twoja obecna kampania jest juz po rozruchu, wiec u Ciebie nic sie nie zmienia - regula zadziala w nowej kampanii.
