# RAPORT NOCNY 19/20.10 - dla Jeffa

Dzien dobry. W grze jest **WGRANIE 90** (wgrane 19.10 ok. 23:25 zegara komputera, po autotestach; WGRANIE 89 o 20:15 + runda 2). Twoj zapis Ironman z 15:48 jest caly
(kopie: `D:\Backup-Bannerlord\zapisy-jeffa\2026-10-19-po-sesji-88` i `D:\Backup-Bannerlord\zawieszenie-2026-10-19-1609\zapis`).

## 1. Zawieszenie z 16:09 - naprawione

**Co sie stalo (prosto):** gra co chwile sprzata pamiec i na ten czas na ulamek sekundy zatrzymuje swoje watki. O 16:09 zatrzymala glowny watek gry
akurat na funkcji, ktora ma 1 bajt kodu i na ktora Harmony (laty modow) wpisal 5-bajtowy skok. Skok nadpisal bajt "opisu stosu", ktory lezy zaraz za tym
1 bajtem. Windows 11 (25H2) uznal ten opis za zepsuty i zglosil blad. Obsluga bledu chciala pamieci, a pamiec czekala na koniec sprzatania, wiec gra
stanela na zawsze. Takich funkcji bylo w grze 142, kilka z nich gra wola co klatke. Dlatego to bylo rzadkie i losowe. Crash z 10:55 (okno kuzni) to
najpewniej to samo.

**Naprawa (CrashScribe):**
- nasze male funkcje nie dostaja juz laty (94 z 142 znikaja od razu);
- pozostale (gra, BLSE, ButterLib, Banner Kings) CrashScribe naprawia w pamieci: przestawia skok tak, ze bajt opisu znow jest poprawny,
  a lata dziala dokladnie tak samo. Pisze tylko przy dokladnie pasujacym wzorcu, inaczej nic nie rusza;
- straznik co 30 s pilnuje, czy jakis mod nie wpisal skoku na nowo, a jesli tak, poprawia go od razu;
- w osobnym programie testowym (ta sama Harmony, ten sam Windows) bez naprawy zawieszenie przychodzilo w kilkanascie sekund, a z naprawa
  program chodzil 11 minut przy 1,6 mln sprzatan bez zaciecia.

**W autotestach w grze:** 44 takie funkcje znalezione, 44 naprawione, 0 pominietych; trzy wolane co klatke (OnApplicationTick, AfterAsyncTickTick,
OnNetworkTick) naprawione. Przez 160 dob: 0 zawieszen.
Wylacznik awaryjny: `Modules\CrashScribe\ModuleData\CrashScribe.settings.xml` -> `UnwindRepair` = false.

## 2. Twoje punkty z wieczora - co jest w grze

Wszystko ponizej jest w grze (WGRANIE 89 i 90), kazda zmiana ma wlasny wylacznik w MCM (domyslnie wlaczony). Szczegoly kazdej: `docs/noc-1920/<punkt>-CHANGELOG.md`
w galezi `w-toku/noc-wgranie-90`.

1. **Relacje po bandytach** - zamiast kilkudziesieciu okienek jest JEDNO, np. "Villages around Oldstones are grateful you beat the bandits:
   relation +2 with 14 notables in 5 villages (...)". Zrodla byly dwa: nasze "wdzieczne wioski" i mod Dynamic Reinforcements (lordowie z odsieczy) -
   oba daja teraz po jednym okienku, z jednym zdaniem "po co" (lepsza relacja = wiecej ochotnikow / chetniejsza odsiecz). Relacje bez zmian.
   MCM: Bandit Relief Relation Summary, Reinforcement Relation Summary. W autotescie: 1 komunikat zamiast 20.
2. **Kuznia / wytapianie** - surowka (crude) jest juz TYLKO do rafinacji: czesci, ktore chcialy surowke, kosztuja przy kuciu tyle samo zelaza kutego
   (miecz Starkow: 3 kute + 3 zelaza zamiast 1 surowki + 2 kutych + 3 zelaza). Przetop oddaje metal wedlug czesci broni - z broni z surowki/zelaza
   nigdy nie wyjdzie stal. Tarcze t5-t6 bez aksamitu (skora). MCM: Smith No Crude Input, Smelt Weapon By Parts, Shield No Velvet i 3 inne.
3. **Plotki w gospodach** - bez kwot ("a fair haul", "rich spoils", "a huge hoard"...), zamiast "Broken Men" imie i nazwisko herszta i "his bandits"
   (bekarty krain: Snow, Rivers, Hill...; Wolni Ludzie z przydomkiem; Essos "z miasta"), ta sama banda zawsze z tym samym imieniem. Ramka: ROT ma okno
   bez przewijania, wiec plotki ida na strony ("Hear more" / "Enough talk") i kazda strona sie miesci.
4. **Tarcze band** - w bitwie bandyci maja herby nakradzione z najblizszych wsi i warowni albo herb pana ziemi, z ktorej zdezerterowali; nigdy herb
   bandytow, nigdy Twoj ani lorda, ktory przyszedl Ci z odsiecza. W autotescie: 30 bandytow, 10 roznych herbow. MCM: Band Shields Stolen Arms.
5. **Za dlugie napisy** - przyciski naszych menu (inwestowanie, karczmy, kuznia, Qohor, ksiega zamowien, kwatermistrz...) maja krotkie napisy,
   kwoty i wyjasnienia sa w dymku po najechaniu myszka. Mierzone na czcionkach gry. MCM: Short Button Labels.
6. **Drogi** - trakt +15% z dwa razy szerszym polem (ok. 6 km od srodka drogi - lekkie zejscie dalej daje premie), drogi do wiosek +7.5%;
   w podpowiedzi predkosci linia "Road" / "Side road". W autotescie: Twoja partia na drodze bocznej dostawala pelne +7.5%.
7. **Wioska poboczna na start** - w Banner Kings "Custom Start" wybierz **"Indebted Lord"**: zamiast wsi glownej dostajesz wioske poboczna w swoim
   krolestwie. Jest Twoja (Twoj herb przy niej, w dymku "This village is yours"), daje dochod (to, co szlo panu z tej wioski - osobna linia w
   finansach), a w menu wsi masz "Visit <wioska> (your village)": nocleg pod dachem (za darmo; po pelnym snie +5% predkosci do zmierzchu i +2 morale),
   "Invest in its works", "Look over your village". W autotescie (furtka): 10/10 sprawdzen.
8. **Towarzysze** - zold wedlug Twojej tabeli (poz. 1 = 1 d, 5 = 3, 10 = 8, 15 = 15, 20 = 25, 25 = 40, 30 = 60 d na dobe) dla towarzyszy i rodziny,
   do ich wlasnych kies; najem = polowa wartosci sprzetu + 20 dni zoldu. W miescie kupuja za swoje zloto lepszy sprzet (puste albo wyraznie gorsze
   sloty, po Twoich zolnierzach, zostawiaja rezerwe 30 dni zoldu, zdjete rzeczy ida do Twojego ekwipunku). Cassel (poz. ok. 19) ok. 23 d zamiast 100.
9. **Niewola** - zdobywca zabiera bron, zbroje, konia z uprzeza i zloto przy sobie (skarbiec, warsztaty, lenna zostaja). Do miasta/zamku/wsi masz
   stroj cywilny, na mapie i w bitwie jestes w bieliznie i pieszo, dopoki czegos nie kupisz. Kto uwolni Cie sila, oddaje caly rynsztunek, ktory
   zdobywca jeszcze mial. MCM (Realistic Captivity): Captor Takes Arms And Purse, Gear Back From Victors.
10. **Arakhy** - bron Dothrakow tylko u Dothrakow; bandy z hersztem sprzedaja ja paserowi. Nowe linie w logu po kazdej bitwie powiedza, skad
    sie bierze, jesli jeszcze ja zobaczysz.
11. **Reszta wycen** - XP Handlu z magazynu, lup AI i kryjowek, kryjowka nocna, paser Spoils, ksiazka z celu BK, stara wartosc towarow BK z definicji
    (przy okazji zalatany uspiony blad: zadanie gangu z futrem moglo zadac 10 000 futer).
12. **A7 (zmiana zajec wiosek)** - przetestowane osobno: sama regula dziala, ale kasy miast rosna 2x szybciej -> zostaje WYLACZONA.
13. **Z planu do konca moda (runda 2, WGRANIE 90):**
    - 6.10 - w NOWEJ kampanii wsie dostaja typ wedlug klimatu (bawelna/len, winnice, gaje) ZANIM gra zbuduje warsztaty w miastach - warsztaty od
      startu pasuja do surowca okolicy (45 wsi). MCM: Climate Before Workshops.
    - 6.9 - wielblady tylko w Qarthu (Dorne jezdzi swoimi rumakami), stajnie Qarthu kupuja wielblady dla swoich jezdzcow (w granicach targu),
      egzotyczne wierzchowce juz nie znikaja w nicosc z polek ani zbrojowni (ida do targu, ktory je trzyma, albo do taboru). MCM: Camels Only Qarth,
      Exotic Mounts Never Vanish, Qarth Stables Buy Camels.
    - 1.7 - ladry (zbroja konia) bohaterow wymagaja Jazdy, tak jak ekran ekwipunku gry z ROT - nasza regula i ekran mowia to samo (500/500).
      MCM: Hero Barding Needs Riding.

## 3. Co sprawdzone, a co nie

Sprawdzone w autotestach (wszystkie: kod 0, 0 bledow naszego kodu, 0 zawieszen gry):
- zapis z autotestu, 5 dob + 4 bitwy (wygrane, odwrot, przegrana) - wszystkie nowe linie w logu sa i maja sens;
- zapis z autotestu, 160 dob x8 - dwa razy (n20b, n20c); porownanie 119 grup progow z ta sama wersja bez dzisiejszych zmian (fin102):
  bez potwierdzonych regresji. W obu biegach gorzej niz w bazie tylko ruda w miastach (43 miasta bez rudy przy progu 40), ale tyle samo
  bylo przy WGRANIU 87-88 (53-57) - obserwuje;
- NOWA kampania 120 dob x8 na 89 i dla porownania na 88: tyle samo niezaliczonych progow (118 / 118), 10 w jedna, 10 w druga strone
  (zmiennosc), wiec dzisiejsze zmiany nie psuja startu kampanii; na 90 nawet mniej (113);
- WGRANIE 90: 5 dob + 4 bitwy, nowa kampania 120 dob i 160 dob z zapisu - kod 0, 0 bledow; jedyne do obserwacji: NO-4 (nowy dlug snu AI o swicie)
  pojedyncze skoki 47 / 41 przy progu 40 (srednio 8-9).

Nie sprawdzone w grze (sprawdzone tylko w kodzie, w dekompilacji i przez recenzje):
- **niewola** - w przegranej bitwie autotestu bohater uciekl z pola, wiec do niewoli nie trafil;
- **start "Indebted Lord" w kreatorze postaci** - autotest nie klika okna startu BK; cala nasza droga od wywolania BK sprawdzona furtka (10/10);
- **zakupy towarzyszy** - w autotescie towarzysz mial za malo zlota ponad rezerwe, wiec nic nie kupil (logika i probka zakupu sa w logu);
- okna i dymki (napisy, plotki na strony) - autotest nie otwiera okien; dlugosci policzone na czcionkach gry.

## 4. Decyzje, ktore podjalem sam (kazda ma wylacznik w MCM)

- Naprawa opisu stosu obejmuje tez CUDZE funkcje (gra, BLSE, ButterLib, BK) - bo funkcja, na ktorej gra stanela, byla cudza.
- Relacje: tez lordowie z odsieczy (Dynamic Reinforcements) dostaja jedno okienko zbiorcze, nie tylko wioski.
- Kuznia: bron z 1 sztaba metalu po przetopie oddaje te 1 sztabe (bez losowania) - **pytanie do Ciebie nizej**.
- Plotki: liczby ludzi i ceny towarow zostaja liczbami (to widac golym okiem), kwoty lupu - slowami.
- Wioska poboczna: nocleg we wlasnej wiosce za darmo, ale skromny (bez karczmy); w wojnie z panem okregu albo w spalonej wsi - jak w obozie.
- Towarzysze: zold z tabeli dotyczy tez rodziny (zona/maz, dzieci, rodzenstwo) w Twoich partiach; kupuja po Twoich zolnierzach.
- Niewola: zloto, ktore zdobywca zabral, nie wraca nawet przy odbiciu (jego sakiewka idzie do zwyciezcow); rynsztunek wraca.
- A7 (zmiana zajec wiosek) zostaje WYLACZONA - podwaja przyrost kas miast.

## 5. Co zostalo

**Pytania do Ciebie (wystarczy numer i odpowiedz):**
1. Przetop broni z 1 sztaby metalu (np. prosta pika) oddaje cala sztabe - zostawic tak, czy oddawac czesc?
2. Aksamit (Twoja decyzja 3c "tani w miastach z tkalnia i okolicy"): nowa linia logu pokazuje, ze tani jest tylko w 32% miast okolicy tkalni (cel 50%).
   Robimy paczke, zeby aksamit plynal z tkalni do okolicy?
3. A7 (zmiana zajec wiosek): wlaczamy dopiero razem z odplywem pieniedzy z kas miast (osobna paczka) - tak?
4. Z planu do konca moda, bez Twojego slowa od dawna: (a) start BK "zeglarz" dalej 10 000 zl? (b) zyznosc krain - porzucamy? (c) Wolni Ludzie
   ok. 8,5 tys. pod bronia (1,8% ludzi) - zostaje? (d) drogi Valyrii (ciemniejsze trakty Essos) - porzucamy? (e) zlecenie kowalskie lorda, ktory
   nie jest glowa rodu (czesto pusta kiesa) - placic inaczej?
5. Okno wyniku kucia (Craft Result Popup): crash z 10:55 to najpewniej to samo zawieszenie, ktore naprawilem - jesli je wylaczyles, mozesz
   wlaczyc z powrotem.

**Do sprawdzenia przez Ciebie w grze (autotest tego nie potrafi):** niewola (rynsztunek, zloto, zwrot przy odbiciu), start "Indebted Lord"
w kreatorze, zakupy towarzyszy w miescie, wyglad przyciskow i plotek na strony, premia drogi bocznej przy jezdzie. Lista dawnych recznych
sprawdzen (PLAN 8.16) jest w `docs/PLAN-DO-KONCA-MODA-2026-10-09.md`.

**Otwarte prace z planu do konca moda** (audyt dzis w nocy: 212 pozycji - 137 w grze, 35 czeka na Twoje slowo, 21 czesciowo, 12 porzucone,
5 otwarte, 2 nieznane): dwuletni bieg od nowej kampanii (wojsko lordow w 2. roku), sakiewki zolnierzy 21 dni zoldu (prog 15), koszyk jedzenia
ludu 88,5% (prog 95%), len i plotno spadaja w dlugim biegu, test inwazji Innych (4-letni), optymalizacja na koniec (GC ok. 4000 sprzatan na
minute przy x8).
