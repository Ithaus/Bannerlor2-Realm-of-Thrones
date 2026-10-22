# Raport nocny 21/22.10 – wersja 103

## W skrócie

**Wersja 103 jest wgrana (22.10, 08:10 czasu polskiego) i sprawdzona. Możesz grać.**

- **Wgranie:** 312 plików, każdy z zgodną sumą kontrolną. Kopia wersji 102 leży na dysku D.
- **Testy w nocy:** 13 przebiegów (pełne prologi po 230 dni, wczytania zapisu, bitwy, 160 dni na starym zapisie, testy Kroniki). Wszystkie przeszły bez błędu modu i bez wywrotki gry.
- **Ostatni test finalnej wersji:** pełny prolog i wczytanie, kod 0, 0 błędów.
- **Twoje zapisy** przez całą noc były nietknięte.

Wersja 103 to wersja 102 plus wszystko, o co prosiłeś wieczorem. Niedomknięte zostały tylko trzy cele gospodarki i 5 nagrań lektora (szczegóły niżej).

## Co nowego w 103

### 1. Kronika Westeros – 117 wydarzeń
- **Treść:** okna z obrazkiem i lektorem (David) według książek, a po nich według serialu, aż do końca.
- **Start:** od około 10. dnia opowieści, czyli mniej więcej od 198. dnia kampanii z prologiem.
- **Na teście w grze w pierwszych 40 dniach opowieści było 10 okien**, m.in. Barristan rzuca biały płaszcz, chorągwie w Winterfell, stos Khala Drogo, czerwona kometa, Król Północy, chłopiec zwany Arry.
- **Wybory:** po niektórych oknach pojawia się lista wyboru. Ostatnia opcja to zawsze wersja z książki.
- **Skutki w świecie:** relacje, sława, wieści i plotki, zbiórki wojsk, bandy, ukrywanie i powrót postaci. Ważne postacie z wydarzeń ROT są chronione, żeby nie zginęły przed swoim wydarzeniem.
- **Tempo:** najwyżej 2 okna na dobę, z odstępem co najmniej 6 godzin.
- **W MCM** (grupa „Chronicle of Westeros”) możesz wyłączyć całość, same skutki w świecie albo wybory.

### 2. Dziennik wiadomości
- **Pozycja:** w menu osady stoi na prawo od menu, więc już go nie zasłania. Na mapie stoi obok sylwetki armii.
- **Wygląd:** ma ciemną ramkę i kończy się nad dolnym paskiem.
- **Okna wydarzeń:** przy oknie wydarzenia ROT albo Kroniki dziennik się chowa i wraca po jego zamknięciu.
- **Handel:** linie o handlu karawan i wieśniaków nad osadami („Caravan of X Sold …”) są ukryte.
- **Wszystko pod przełącznikami w MCM.**

### 3. Czcionka nazw osad
Nazwy miast, zamków i wsi na mapie są pismem w stylu Gry o Tron, tym samym, którego ROT używa w tytułach. Wyłączysz to w MCM („Map Settlement Names Rot Font”); zmiana działa po ponownym uruchomieniu gry.

### 4. Gospodarka
- **Wsie (część 4):**
  - jedzenie kupowane rozsądniej;
  - danina panów nie zabiera wsi pieniędzy na jedzenie z najbliższych dni;
  - zamki z zaległościami szybciej obniżają wypłaty.
- **Biedne wsie (część 5):** dopóki wieś nie ma zapasu na czarną godzinę, pan nie zabiera jej swojej części z wozu, a czynsz idzie tylko z nadwyżki. Złoto nigdzie nie powstaje z niczego.
- **Miasta:**
  - firmy nie płacą ponad dopływ, gdy kasa jest pusta;
  - chleb ze skarbca korony dla głodnych miast;
  - pełna premia dróg Valyrii.
- **Wyniki** (pełny prolog 230 dni, porównanie z 102):

| | 102 | 103 | Cel |
|---|---|---|---|
| Wsie z pustą kiesą | 13,2% | 8,2% | 5% |
| Jedzenie wsi bez pokrycia (Westeros) | 12,5% | w normie | 10% |
| Miasta, które stać na całe jedzenie | 75% | w normie | 78% |
| Kiesy wsi topnieją | nie | nie | nie |

  - **Niedomknięte:** wsie z pustą kiesą (8,2%, cel 5%), zakupy narzędzi przez wsie oraz zaległości zamków (21 tys. przy celu 20 tys.).
  - **Kto zostaje biedny:** głównie wsie, do których nie wracają wozy (Wolni Ludzie, Żelazne Wyspy) i wsie z bardzo małym zapasem.

### 5. Drobne
- **Ludzie Góry** naprawdę palą wsie Dorzecza, a Beric rusza w pościg dopiero po pierwszej spalonej wsi, jak w książce.
- **Tablica ogłoszeń:** naprawiony błąd samej gry w zadaniu z siekierami (dzielenie przez zero przy pustych półkach).
- **Optymalizacja:** krótsza przerwa przy zmianie doby, a liczby w grze bez zmian (sprawdzone w trybie kontrolnym: ponad 600 tys. porównań, 0 różnic). Jedno przyspieszenie wyłączyłem, bo przesuwało losowanie gry.

## Co zostało (do kolejnych prac)
- **Lektor:** 5 okien Kroniki bez aktualnego nagrania; 4 mają starszy głos, więc gra ich nie odtwarza. Konto ElevenLabs ma 256 kredytów, plan odnawia się około 21.11 (albo doładowanie). Jeden skrypt nagrywa wtedy tylko te 5 okien.
- **Gospodarka:** wsie z pustą kiesą (8,2% → cel 5%), zakupy narzędzi, zaległości kilku zamków.
- **Obrazki prologu 04–23:** prompty są w `docs/PROMPTY-MIDJOURNEY-2026-10-21.pdf`. Gotowe pliki wrzucam do gry jednym skryptem.

## Ważne dla Ciebie
- **Zacznij nową kampanię z prologiem.** Scen prologu, orszaku Roberta i zmian wojen nie ma w kampaniach zaczętych wcześniej.
- **Pierwsze uruchomienie:** może trwać chwilę dłużej, bo dochodzi 81 MB nagrań Kroniki.
