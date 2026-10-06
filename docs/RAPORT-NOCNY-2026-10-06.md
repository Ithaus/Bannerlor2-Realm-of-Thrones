# Raport nocny 2026-10-05 / 06

Godziny wedlug zegara komputera (ustawiony na czas pacyficzny). Stan na 02:30; rozdzial 3 bedzie uzupelniony, gdy skoncza sie
recenzje w tle.

## 0. W skrocie

- **Do gry weszla w nocy tylko paczka logow (wpis 102).** W grze jest wpis 101 (woz x2) + wpis 102 (ksiegi pieniadza i ludzi).
  DLL md5 `bf0945fcb4987f4bc54e49a6467895ab`. Zadna zmiana zasad gry nie zostala wgrana bez Ciebie.
- **Trzy paczki sa gotowe i zlozone w lancuch na aktualnym kodzie:** 103 karawany -> 104 zapas startowy -> 105 mineral BK.
  Kazde ogniwo buduje sie z kodem 0; niezalezny przeglad kolizji miedzy nimi: "czysty". Wgrywam po jednej, po Twoim tescie.
- **Cztery rzeczy sa jeszcze w recenzji** (paser, skrypt BetterEconomy, zold i skarbiec, reguly krain i dlugu) - noc przerwala sie
  dwa razy, szczegoly w rozdziale 5.
- **Nie zrobione:** paczka "przyrost naturalny" (odpuszczona, zeby nie palic limitu tygodniowego - jest na 42%).

## 1. Co jest w grze i co testowac jako pierwsze

Nowa kampania, 15-20 dob, potem "sprawdz logi". Sprawdzam wtedy:

| Co | Linia logu | Czego oczekuje |
|---|---|---|
| Woz x2 (wpis 101) | "Dowoz:", "Dowoz (skutki):" | zatkane magazyny wsi zamkowych blizej wsi miejskich (bylo 17% wobec 8%); mniej miast bez rudy niz 60 z 97 |
| Ksiega pieniadza (102) | "Pieniadz swiata:", "Pieniadz swiata (bilans):" | pierwszy raz: ile zlota jest na swiecie, kto je trzyma, ile powstaje z niczego i ile znika |
| Przeplywy osad (102) | "Przeplywy osad:" + 3 linie kas | ile miasta i zamki placa taborom wsi, jak dzieli sie utarg, zold; rozbicie zmian kas miast / zamkow / wsi |
| Ksiega ludzi (102) | "Ludzie:", "Ludzie (regiony):" + plik `ludzie-regiony.csv` w katalogu sesji logow | ludnosc, zolnierze, zabici, zwerbowani, odsetek mezczyzn pod bronia na region |
| Ksiega rudy i drewna (102) | "Ruda:", "Drewno:" | nowa pozycja "budowy", "tabory" rozbite na wiesniakow / karawany / lordow |
| Warsztaty (102) | "Warsztaty:" | "brak surowca N [ruda, drewno, skora, len/welna]" - wreszcie wiadomo, ktorego surowca brakuje |

Te liczby zastapia szacunki z fundamentu ekonomii (`docs/EKONOMIA-FUNDAMENT-2026-10-05.md`) - od nich zalezy kazdy dalszy krok.

## 2. Lancuch gotowych paczek

Galezie w repo i na GitHubie (galaz robocza i gra sa bez zmian):

| Nr | Galaz | Co zmienia | Recenzja paczki | Nowa kampania |
|---|---|---|---|---|
| 103 | `paczki/103-karawany` | karawany sprzedaja miastom rude, drewno, skory, len, plotno, welne do zapasu docelowego i kupuja tylko z nadwyzek | poprawione i gotowe | zalecana |
| 104 | `paczki/104-zapas-startowy` | ruda i drewno na starcie kampanii przeliczone na ladunki (koniec masy x10) | poprawione i gotowe | WYMAGANA |
| 105 | `paczki/105-mineral-bk` | BK nie dopisuje mineralu wsi gorniczej dwa razy; mnoznik rudy 3 -> 6 (wydobycie rudy bez zmian) | poprawione i gotowe | WYMAGANA |

Gotowe teksty wpisow CHANGELOG (ze wszystkimi ryzykami i liniami logu): `docs/paczki/103-karawany.md`, `104-zapas-startowy.md`,
`105-mineral-bk.md`.

### 103 - karawany
- **Regula:** karawana wjezdzajac do miasta sprzedaje mu surowiec, ktorego miastu brakuje do 10 dob zuzycia (placi kasa miasta
  ponad 20 000). Wyjezdzajac kupuje tylko to, czego miasto ma ponad dwukrotny zapas, do polowy udzwigu. Stary handel BK tymi
  towarami (wedle indeksu ceny, po 1-3 sztuki) jest zamkniety.
- **Po czym poznac:** start "CaravanBulk: ... regula CZYNNA"; co dobe "Karawany:" i "Karawany (stan):". Glowna liczba:
  "ruda: bez towaru" - z 60 miast w dol do kilku-kilkunastu w 10-20 dob (SZACUNEK z prostego modelu, nie pomiar).
- **Ryzyka:** kierunek jazdy karawan sie nie zmienia - regiony bez kopaln i bez karawan z daleka moga zostac puste. Zapas zamkow
  (2641 ladunkow rudy) zostaje, bo karawany do zamkow nie wjezdzaja. Kolejnosc zakupu to "najcenniejszy kilogram pierwszy",
  wiec plotno i welna ida przed ruda (pytanie 1).

### 104 - zapas startowy
- **Regula:** raz, przy starcie nowej kampanii, liczba sztuk rudy i drewna na calym swiecie jest dzielona przez 10. Kilogramow
  zostaje tyle samo - znika blad jednostki. Stare zapisy bez zmian.
- **Po czym poznac:** 4 linie "StartStock:" przy starcie; "Ruda: ... razem" pierwszej doby ponizej 900 ladunkow (dzis ok. 7000).
- **Skutek zamierzony, ale widoczny:** miasta zaczna z ok. 200 ladunkami rudy zamiast ok. 2000. "Warsztaty: brak surowca" i
  "miast bez towaru" WZROSNA w pierwszych dobach - to prawdziwy stan, ktory zapas x10 maskowal.

### 105 - mineral BK
- **Regula:** BannerKings ma mineral wsi gorniczej na liscie produkcji dwa razy i dopisuje go dwa razy dziennie. Latka zdejmuje
  powtorzenie; mnoznik rudy idzie z 3 na 6, wiec rudy jest tyle samo co dzis (ok. 7.4 ladunku na kopiaca wies).
- **Po czym poznac:** "MineralOnce: ... latka wpieta", co dobe "Mineraly (dubel BK):"; przy starcie "wydobycie rudy x6.0";
  w "Ruda:" stosunek "wsie dopisaly" do "model" ok. 1 (dzis ok. 2).
- **Skutek uboczny:** sol (12 wsi), glina (16) i srebro (30) spadna o polowe (pytanie 2). Jednorazowo ok. +170 tys. d podatku
  poczatkowego dla panow 26 wsi z ruda (SZACUNEK autora paczki).

### Przeglad kolizji miedzy paczkami (niezalezny, 06.10)

Werdykt: **czysty** - kolejnosc 103 -> 104 -> 105 zostaje, kazde osobno, bez poprawek w kodzie. Sprawdzone: scalenie rownowazne
paczkom, zadnych wspolnych celow latek Harmony, klucz zapisu unikalny, handel karawan nie jest w ksiedze pieniadza liczony
podwojnie ani jako zloto z niczego. Trzy uwagi wazne dla testow:

1. **Mnoznik 6 moze nie wejsc, jesli zapiszesz ustawienia Armoury w MCM przed wgraniem 105.** Dzis klucza `MineOutputMultiplier`
   nie ma w Twoim pliku ustawien, wiec zadziala domyslne 6. Zapis ustawien w MCM wpisalby tam 3 i po 105 wydobycie rudy spadloby
   o polowe. Nic nie musisz robic - przed wgraniem 105 sprawdze plik i w razie czego poprawie wartosc.
2. **Po 104 karawany przez pierwsze doby nie maja czego wiezc.** Miasta zaczynaja z 1/10 zapasu i prawie zadne nie ma nadwyzki.
   To nie blokada - ruch rusza, gdy miasta przy kopalniach przekrocza prog. Jesli rozruch potrwa ponad 7-10 dob, jest gotowa
   prosta poprawka.
3. **Test 103 pokaze mechanike, nie skutecznosc.** Swiat ma wtedy jeszcze zapas x10; prawdziwy niedobor widac dopiero od 104.
   Propozycja: test 103 krotki (5-7 dob - czy latki wpiete, czy sprzedaja i kupuja, czy pieniadz sie zgadza), test 104 pelny
   (15-20 dob). Po cofnieciu z 104 do starszego DLL trzeba zalozyc nowa kampanie.

## 3. Paczki w recenzji (uzupelnie po zakonczeniu)

| Paczka | Stan na 02:30 |
|---|---|
| PASER - bandy sprzedaja zrabowany ladunek w miescie | kod i wpis gotowe, recenzja trwa |
| BETTERECONOMY - skrypt zamykajacy 13 ujsc zlota (z kopia pliku i skryptem cofajacym; nie uruchamiany) | skrypt i opis gotowe, recenzja trwa |
| ZOLD I SKARBIEC - zold do sakiewek ludzi i kas osad, skarbiec krolestwa zwraca polowe zoldu w wojnie | autor konczy, potem recenzja |
| REGULY KRAIN I DLUGU - Nocna Straz, Zelazne Wyspy, Wolni Ludzie, nieumarli, dlug bez utraty lenna (dokument, bez kodu) | dokument ok. 100 KB, autor konczy, potem recenzja |

## 4. Wyniki badan z wieczora

- **Nieumarli:** dzis rosna glownie z niczego (ROT daje +100 przy narodzinach bandy, +2 dziennie i ochotnikow z mapy); z
  pokonanych pochodzi najwyzej 25%. Twoja zasada "tylko z poleglych" wymaga zmiany - lista poprawek w `docs/STAN-PRAC.md`.
- **Aneks demografii** (`docs/DEMOGRAFIA-ANEKS-2026-10-05.md`, PDF w `Dokumenty\Bannerlord-dokumenty`): spalona wies jako
  spustoszenie ulamka okregu, male krainy, widok ksiegi ludzi w menu osady, bandyci i kryjowki. Na koncu 26 pytan do Ciebie.

## 5. Co poszlo nie tak

- **17:00** - limit sesji doszedl do 100% (zadania w tle przekroczyly Twoja granice 90%). Przeglad karawan i redakcja aneksu
  padly; wznowione po odnowieniu limitu, oba skonczone.
- **18:39-18:55** - etap 2: po kwadransie pracy wszyscy czterej autorzy padli na bledzie dostepu do modelu (403), druga proba
  tez. Wznowione o 22:27.
- **23:12** - sesja zgasla w trakcie etapu 2 (trzy recenzje w toku, zold niedokonczony). Praca stala do 01:58, kiedy napisales;
  wznowiona o 01:59. Autorzy trzech paczek musieli czesc pracy powtorzyc. Od 02:00 komputer ma blokade usypiania na czas pracy.
- **Limit tygodniowy:** 36% wieczorem, 42% teraz. Dlatego nie uruchomilem etapu 3 w pelnym ksztalcie: zamiast drugiego przegladu
  wszystkich paczek zrobilem scalenie sam i zlecilem jeden przeglad kolizji; paczka "przyrost naturalny" odlozona.

## 6. Pytania do Ciebie

1. **Karawany: czy ruda ma byc kupowana pierwsza**, przed plotnem i welna? Rekomendacja: zostawic na test 103; zmienic, jesli w
   logu ruda przegrywa miejsce w jukach.
2. **Sol, glina i srebro spadna o polowe po 105 - wyrownac mnoznikiem?** Rekomendacja: nie wyrownywac w tescie 105, popatrzec na
   ceny i utarg wsi; wyrownac dopiero, gdy sol wyraznie zdrozeje.
3. **Ile limitu tygodniowego moge wydawac na prace w tle?** Jedna noc kosztowala ok. 10 punktow. Rekomendacja: do konca tygodnia
   bez nocnych zadan wieloosobowych, tylko pojedyncze recenzje przy wgrywaniu.
4. **Czy jency Innych wstaja jako upiory?** (otwarte z regul nieumarlych)
5. **Danina zywnosci dla zamku** - czy zamek ma dostawac czesc plonu wsi w naturze, skoro chlop wozi juz na targ miasta?
6. Pozostale: 26 pytan w aneksie demografii - mozna odpowiadac partiami.

## 7. Jak wgrywam ogniwo

Piszesz "wgraj 103" (gra zamknieta). Ja: przenosze galaz paczki na galaz robocza, generuje MCM, buduje (kod 0), robie kopie
`Armoury.dll.bak-<data>-przed-103`, wgrywam, sprawdzam md5, dopisuje wpis CHANGELOG, commit i push. Potem Twoj test i
"sprawdz logi". Kolejne ogniwo dopiero po wyniku poprzedniego.
