# Raport nocny 2026-10-05 / 06

Godziny wedlug zegara komputera (ustawiony na czas pacyficzny). Wersja 2 - komplet, stan na 03:45.

## 0. W skrocie

- **Do gry weszla w nocy tylko paczka logow (wpis 102).** W grze jest wpis 101 (woz x2) + wpis 102 (ksiegi pieniadza i ludzi).
  DLL md5 `bf0945fcb4987f4bc54e49a6467895ab`. Zadna zmiana zasad gry nie zostala wgrana bez Ciebie.
- **Piec paczek kodu czeka w lancuchu na aktualnym kodzie:** 103 karawany -> 104 zapas startowy -> 105 mineral BK -> 106 paser ->
  107 zold i skarbiec. Kazda ma niezalezna recenzje ("poprawione i gotowe"), kazde ogniwo buduje sie z kodem 0. Wgrywam po
  jednej, po Twoim tescie.
- **Poza lancuchem gotowe:** skrypt zamykajacy 13 ujsc zlota BetterEconomy (nie uruchomiony) i projekt regul krain i dlugu
  (dokument, 5 regul gotowych do zakodowania).
- **Nie zrobione:** paczka "przyrost naturalny" i przeglad kolizji ogniw 106-107 z reszta lancucha (odlozone, zeby nie palic
  limitu tygodniowego - patrz rozdzial 5).
- **Pierwszy krok dla Ciebie:** nowa kampania, 15-20 dob, "sprawdz logi" (rozdzial 1).

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

**Uwaga do odczytu:** dwoch recenzentow niezaleznie zauwazylo, ze linia "Pieniadz swiata (bilans):" najpewniej liczy zold dwa
razy (raz licznikiem zoldu, raz jako ubytek z kiesy glowy rodu). "Ujscia" beda wtedy zawyzone, a "reszta" dodatnia o mniej wiecej
cala pozycje zoldu. Potwierdze to na Twoim pierwszym logu i poprawie osobnym, malym wpisem. Stan kas i sumy sa od tego niezalezne.

## 2. Lancuch paczek kodu

Galezie w repo i na GitHubie (galaz robocza i gra sa bez zmian). Gotowe teksty wpisow CHANGELOG z pelna lista ryzyk i linii logu
sa w `docs/paczki/`.

| Nr | Galaz | Co zmienia | Nowa kampania |
|---|---|---|---|
| 103 | `paczki/103-karawany` | karawany sprzedaja miastom rude, drewno, skory, len, plotno, welne do zapasu docelowego i kupuja tylko z nadwyzek | zalecana |
| 104 | `paczki/104-zapas-startowy` | ruda i drewno na starcie kampanii przeliczone na ladunki (koniec masy x10) | WYMAGANA |
| 105 | `paczki/105-mineral-bk` | BK nie dopisuje mineralu wsi gorniczej dwa razy; mnoznik rudy 3 -> 6 (wydobycie rudy bez zmian) | WYMAGANA |
| 106 | `paczki/106-paser` | bandy sprzedaja zrabowany ladunek paserowi w miescie; koniec zlota z niczego przy wejsciu bandy do kryjowki | nie |
| 107 | `paczki/107-zold-i-skarbiec` | zold trafia do sakiewek ludzi i kas osad zamiast znikac; skarbiec krolestwa w wojnie zwraca rodom polowe zoldu | nie |

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

### 106 - paser
- **Regula:** raz na dobe banda w promieniu 20 od miasta sprzedaje paserowi towary handlowe i zwierzeta hodowlane za polowe ceny
  skupu, z kasy miasta ponad 20 000; towar wraca na polke miasta. Zywnosci, zbroi i koni paser nie bierze. Gra nie dopisuje juz
  bandzie i kryjowce po 25% wartosci jukow z niczego przy wejsciu do kryjowki; w zamian banda odklada w kryjowce 25% wlasnej
  kiesy - prawdziwa monete, ktora znajdzie ten, kto kryjowke oczysci.
- **Poprawka recenzenta:** banda urodzona w srodku dziennego ticku gry dostawala 2,5 zl na czlowieka z niczego.
- **Po czym poznac:** start "OutlawLaw: ... wpiete: ..., zloto kryjowek"; co dobe "Paser:" (bandy z ladunkiem, ile w zasiegu
  miasta, sztuki, zaplata, stan kies i jukow).
- **Slabosc:** przy promieniu 20 w zasiegu miasta lezy 4% kryjowek i 11% wsi, wiec sprzeda pewnie 5-15% band z ladunkiem
  (SZACUNEK z mapy, nie pomiar) - wiekszosc lupu nie wroci na rynek (pytanie 3). Zrabowana zywnosc zostaje w jukach na zawsze.

### 107 - zold i skarbiec
- **Regula:** zold, ktory partia faktycznie dostaje, idzie do sakiewek jej ludzi (wydaja go w miastach); zold zalogi idzie do
  kasy jej miasta albo zamku. Dotyczy tez Twoich ludzi. Skarbiec krolestwa w wojnie zwraca rodom 50% faktycznie zaplaconego
  zoldu, dopoki ma z czego.
- **Poprawka recenzenta:** gdy rod nie mial na zold, a gra zapisywala brak jako dlug wobec korony, paczka autora przekazywala
  dalej zloto z niczego (w probie 786 z 4000 rodow; po poprawce 0 z 12 000).
- **Po czym poznac:** start "SoldierPay: zold do obiegu ... rozliczenie rodu wpiete"; pierwsza doba "latki modelu finansow ...
  saldo rodu z ROT.Models.ROTClanFinanceModel (wpiete teraz)" (slowo BRAK = zglosic); co dobe "Zold:" i "Korona: ... zwrot zoldu".
- **Ryzyka:** gra co dobe kasuje czesc zlota z kas osad (regulator). Bez wlaczonej tarczy zoldu zjada on ok. 81% zoldu wplaconego
  do kas miast i ok. 99% w zamkach (pytania 4 i 5). Skarbce krolestw (ok. 2 mln kazdy) starcza na 106-740 dob zwrotow (mediana
  354). Bogatsze rody to wieksze partie lordow.

### Przeglad kolizji miedzy paczkami 103-105 (niezalezny, 06.10)

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

**Ogniwa 106 i 107** sa zlozone na ogniwie 105 i zbudowane (kod 0). Paser wszedl bez konfliktow; zold wymagal pogodzenia z
paserem w ksiedze pieniadza (obie paczki dopisaly wlasne pozycje - przenumerowane wedlug wskazowki recenzenta). Te dwa ogniwa NIE
maja jeszcze przegladu kolizji z ogniwami 103-105 - zrobie go jednym watkiem tuz przed wgraniem 106.

## 3. Gotowe poza lancuchem

### BetterEconomy - zamkniecie 13 ujsc zlota (`tools/bee/`)
- `zamknij-ujscia-bee.ps1` zmienia dokladnie 13 wartosci w `better_economy_settings.xml` (robi kopie pliku, sprawdza, czy gra
  jest zamknieta, zachowuje kodowanie); `cofnij-ujscia-bee.ps1` je cofa. Proby na kopiach: 227 sprawdzen, 0 niezaliczonych.
  Na prawdziwym pliku nic nie bylo uruchamiane. Opis: `tools/bee/OPIS.md`.
- **Zbrojownia:** domyslnie zamknieta tylko dla AI - zgodnie z Twoja decyzja "funkcje gracza bez zmian".
- **Test:** nowa kampania niepotrzebna, 10-12 dob. Potrzebna sesja "przed" i sesja "po" z TEGO SAMEGO zapisu przez te same doby -
  inaczej liczb nie da sie porownac. Miernik: `skoki-bee.py` (skoki zlota w zamkach ze 100-108 do ok. 6-7 na 12 dob).
- **Ryzyko:** aktualizacja BetterEconomy albo "sprawdz spojnosc plikow" w Steam przywraca plik i ujscia otwieraja sie bez
  ostrzezenia - po kazdej aktualizacji trzeba puscic skrypt ponownie.

### Reguly krain i dlugu (`docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md`)
- Sam projekt, bez kodu: 5 regul gotowych do zakodowania w kolejnosci R1-R13 - Nocna Straz (rekrut z krolestw Westeros, najpierw
  z pul wyrzutkow), niewolni Zelaznych Wysp, Wolni Ludzie 300 tys. (walcza wszyscy dorosli), nieumarli tylko z poleglych, dlug
  bez utraty lenna (wierzyciel pobiera dochod wsi, lord wyprzedaje majatek).
- **Najwazniejsze poprawki recenzenta:** BannerKings ogranicza zold partii wedlug kiesy glowy rodu w sposob ciagly, wiec
  "podloga kiesy" dluznika nie chroni wojska calkiem, tylko wyznacza jego wielkosc (38 000 zl = ok. 135 ludzi na partie); sama
  pierwsza regula nieumarlych w nowej kampanii zostawialaby Innym 216 trupow zamiast 616; sprzedaz warsztatu dluznika tworzyla
  zloto z niczego.
- 8 pytan do Ciebie z rekomendacjami: rozdzial 9 tego dokumentu.

## 4. Wyniki badan z wieczora

- **Nieumarli:** dzis rosna glownie z niczego (ROT daje +100 przy narodzinach bandy, +2 dziennie i ochotnikow z mapy); z
  pokonanych pochodzi najwyzej 25%. Twoja zasada "tylko z poleglych" wymaga zmiany - reguly R2-R4 w dokumencie regul.
- **Aneks demografii** (`docs/DEMOGRAFIA-ANEKS-2026-10-05.md`, PDF w `Dokumenty\Bannerlord-dokumenty`): spalona wies jako
  spustoszenie ulamka okregu, male krainy, widok ksiegi ludzi w menu osady, bandyci i kryjowki. Na koncu 26 pytan do Ciebie.

## 5. Co poszlo nie tak

- **ok. 17:00** - limit sesji doszedl do 100% (zadania w tle przekroczyly Twoja granice 90%). Przeglad karawan i redakcja aneksu
  padly; wznowione po odnowieniu limitu, oba skonczone.
- **18:39-18:55** - etap 2: po kwadransie pracy wszyscy czterej autorzy padli na bledzie dostepu do modelu (403), druga proba
  tez. Wznowione o 22:27.
- **23:12** - sesja zgasla w trakcie etapu 2 (trzy recenzje w toku, zold niedokonczony). Praca stala do 01:58, kiedy napisales;
  wznowiona o 01:59, skonczona o 03:20. Trzech autorow musialo czesc pracy powtorzyc. Od 02:00 komputer ma blokade usypiania
  na czas pracy.
- **Limit tygodniowy:** 36% wieczorem, po calej nocy wyraznie wiecej (aktualna liczba w wiadomosci w rozmowie). Dlatego etap 3
  nie poszedl w pelnym ksztalcie: scalenie paczek zrobilem sam, zlecilem jeden przeglad kolizji zamiast drugiego przegladu
  wszystkiego, a paczka "przyrost naturalny" jest odlozona.

## 6. Pytania do Ciebie

**A. Przed kolejnymi ogniwami (krotkie decyzje)**

1. **Karawany (103): czy ruda ma byc kupowana pierwsza**, przed plotnem i welna? Rekomendacja: zostawic na test; zmienic, jesli
   w logu ruda przegrywa miejsce w jukach.
2. **Mineral (105): sol, glina i srebro spadna o polowe - wyrownac mnoznikiem?** Rekomendacja: nie w tescie 105; wyrownac
   dopiero, gdy sol wyraznie zdrozeje albo utarg tych wsi siadzie.
3. **Paser (106): promien 20 czy 40?** Przy 20 dziala dla ok. 5-15% band, przy 40 w zasiegu jest 26% kryjowek i 50% wsi.
   Ten sam promien pozwala bandom kupowac sprzet. Rekomendacja: 40 - przy 20 paczka prawie nic nie zmienia.
4. **Zold (107): wlaczyc tarcze zoldu?** Bez niej regulator kas skasuje wiekszosc zoldu wplaconego do kas osad i test niczego nie
   pokaze. Rekomendacja: wlaczyc na test; docelowo naprawic sam regulator (krok K6 fundamentu).
5. **Zold (107): zold zalogi zamku do kasy zamku czy najblizszego miasta?** Twoja decyzja mowila "do kasy osady". Rekomendacja:
   zostawic zamek, ocenic po pierwszym logu.
6. **Ile limitu tygodniowego moge wydawac na prace w tle?** Rekomendacja: do konca tygodnia bez nocnych zadan wieloosobowych,
   tylko pojedyncze recenzje przy wgrywaniu.

**B. Do regul krain (przed kodowaniem, nie pilne)** - 8 pytan z rekomendacjami w rozdziale 9 dokumentu regul. Najwazniejsze:
czy jency Innych wstaja jako trupy (rekomendacja: tak); bracia Nocnej Strazy bez zoldu (tak - Straz ma 698 zl rent przy 14.4 tys.
zoldu dziennie); ile wierzyciel zostawia dluznikowi w kiesie (38 000 zl); odsetki zamrozone w dniu zajecia (tak).

**C. Pozostale** - 26 pytan w aneksie demografii; danina zywnosci dla zamku, skoro chlop wozi juz plon na targ miasta.

## 7. Jak wgrywam ogniwo

Piszesz "wgraj 103" (gra zamknieta). Ja: przenosze galaz paczki na galaz robocza, generuje MCM, buduje (kod 0), robie kopie
`Armoury.dll.bak-<data>-przed-103`, wgrywam, sprawdzam md5, dopisuje wpis CHANGELOG, commit i push. Potem Twoj test i
"sprawdz logi". Kolejne ogniwo dopiero po wyniku poprzedniego. Skrypt BetterEconomy uruchamiam tak samo - na Twoje slowo.
