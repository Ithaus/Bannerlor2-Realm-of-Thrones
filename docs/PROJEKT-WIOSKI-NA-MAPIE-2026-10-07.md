# PROJEKT: nazwane wioski na mapie (07.10; teren + wyglad + nazwy + ksiega + prototyp v1 + krytyk + prototyp v2; NIC NIE ZAKODOWANE)

**Polecenie Jeffa (07.10, doslownie):** "pomysl dobry, ale nie nazywaj tego przysiolek tylko Wioska XYZ spalona i plonie na mapie,
rozklad wioske zeby nie byl losowy bezadzijeny, zeby to wygladalo na mapie ze te wioski maja sens, skoro sa drogi to pewnie wzdluz
drog czesc kilka po bokach itp. zeby to nie bylo losowe rzucanie wioskami".

**Co to znaczy w projekcie:** kazdy dodatkowy obrazek na mapie to **WIOSKA Z NAZWA** (w grze po angielsku: "Brackenford is burning",
"Brackenford has been burned"). Widac, jak plonie (w trakcie rabunku) i ze jest spalona (po). Stoi tam, gdzie wies miala powod stac:
przy moscie, brodzie, skrzyzowaniu, ujsciu, w zatoce, wzdluz drogi, rzeki i brzegu, kilka na uboczu przy polach. Nigdy losowo.
W kodzie, MCM i tekstach NIE uzywamy slowa "hamlet" / "przysiolek" - tylko "village" / "map village" (wioska).

**Stan:** tylko projekt i prototyp danych. Gra i repo tylko czytane, nic nie zbudowane, nic nie wgrane, nic nie zacommitowane.
Obrazki dla Jeffa: `docs/obrazki/wioski-na-mapie/` (rozdz. 15).

Oznaczenia sciezek:
- SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`
- W = `SCR\dzien-6\wioski-na-mapie` (a-teren, b-wyglad, c-nazwy, d-ksiega, p-prototyp, p-prototyp\v2, k-krytyk)
- dec = `SCR\dzien-6\autotest\dec` (dekompilacja gry 1.4.8), TCS = `SCR\ore-supply\cs` (TaleWorlds.CampaignSystem)
- [PROJ] = wartosc projektu (decyzja projektu, nie liczba ze zrodla); [ZMIERZONE] = z pliku mapy / kodu / prototypu; [AUTOTEST] = do zmierzenia w grze.

---

## 0. W skrocie

1. **Ok. 1 500 nazwanych wiosek na calej mapie** (prototyp v2: 1 483 w 437 z 571 okregow; zwykle 2-5 na okreg, najwiecej 12).
   Kazda to gromada osad okregu (osada = ok. 250 ludzi, PROJEKT-RABUNEK pkt 1).
2. **Rozstawienie z sensem, nie losowe** (rozdz. 2): najpierw miejsca szczegolne (most, brod przy drodze, skrzyzowanie WIDOCZNYCH
   drog, ujscie, widly, przystan, zatoka), potem sznur wzdluz widocznej drogi, rzeki, jeziora i brzegu (po jednej stronie, w
   nierownych odstepach, z przerwami na slabym gruncie), na koncu kilka na uboczu przy polach. Nigdy w gorach, na bagnie, w wodzie,
   w puszczy, miedzy drzewami, na pustyni bez wody, na stromym stoku ani na drodze. Sprawdzany caly obrys wioski, nie srodek.
3. **Nazwy pasuja do miejsca i krainy** (rozdz. 4): Hag's Bridge przy moscie, Crosswood i Golden Cross na skrzyzowaniu, Ironpool i
   Honeypool nad rzeka, Sandbridge nad Greenblood, Aleni Noylys pod Volantis. Unikalne, nie podobne do osad gry, bez powtorzen w kadrze.
4. **Wyglad** (rozdz. 5): domy w stylu wsi-matki (andal / fm / Essos), mniejsze; poziom 3/2/1 = ile domow stoi; **plonie** = ogien i
   dym z czastek gry; **spalona** = siatka zgliszcz gry + przyciemnione domy; dymek po najechaniu myszka; napisy nad plonacymi i
   spalonymi. Bez fizyki - nie lapie klikniec, nie przeszkadza w ruchu.
5. **Jedno zrodlo prawdy: ksiega ludzi 113** (rozdz. 6). Warstwa wiosek nie rusza ludzi, spichlerza ani hearth; zapisuje tylko,
   GDZIE w okregu sa puste osady. Mapa, dymek, menu i okienko rabunku czytaja te same liczby.
6. **Kazdy rabunek widac na mapie (100% okregow):** ogien na nazwanej wiosce, w ktorej naprawde pala, albo - gdzie wioski nie ma
   (Zelazne Wyspy, Qarth, step Dothrakow, Skagos, male okregi Strazy) - na samej wsi gry. Kazda osada nalezy do jednego nazwanego
   miejsca: do wioski albo do wsi gry (ktora jest glowna wioska okregu).
7. **Sejw:** jeden maly wpis (`arm_district`, typowo < 2 KB) - potrzebny, bo z sum ksiegi nie da sie odtworzyc, KTORA wioska
   splonela (rozdz. 7). Pozycje, nazwy i wyglad nie ida do sejwu (plik danych moda).
8. **Wydajnosc:** doba +0 s (stan liczony tylko przy zdarzeniach); na ekranie zwykle 90, najwyzej ok. 220 wiosek; po
   oddaleniu kamery wioski znikaja (rozdz. 8). Do zmierzenia w autotescie.
9. **Paczki** (rozdz. 11): W0 dane -> W1 proba wygladu (tylko autotest) -> W2 wioski na mapie (sam widok, bez ksiegi; moze wejsc
   wczesnie) -> [108-113 wedlug STAN-PRAC] -> W3 ksiega wiosek (spalona, mniejsza, odbudowa, menu, komunikaty) -> W4 napisy na
   mapie -> spichlerz -> rabunek osada po osadzie z nazwami wiosek w okienku.
10. **Do decyzji Jeffa tylko 3 sprawy kierunkowe** (rozdz. 12): ile wiosek (sens czy liczba), napisy (tylko plonace/spalone czy
    wszystkie), czy sam widok W2 wgrac wczesniej, przed ksiega ludzi.

---

## 1. Co widzi gracz

### 1.1 Mapa z bliska i z daleka
- Z bliska (kamera nisko) miedzy wsiami, zamkami i miastami ROT stoja **mniejsze wioski** - domy w stylu wsi gry, do ktorej naleza
  (Polnoc: drewniane domy fm/andal, Reach i Dorzecze: andal, Essos: siatki kultur Essos). Wioska jest zawsze mniejsza od wsi gry.
- Wioski stoja **sznurem wzdluz drog i rzek** (raz gesciej, raz rzadziej, zwykle po tej stronie, gdzie lepszy grunt), **przy mostach
  i na skrzyzowaniach** (wieksze, poziom 3), **u ujsc rzek i w zatokach**, kilka **na uboczu przy polach** (male, poziom 1).
- Lasy (Wolfswood, Kingswood), gory, pustynie bez wody, bagna Przesmyku i Czerwone Pustkowie zostaja puste.
- Z daleka (wysoka kamera, ok. z > 160) wioski znikaja - zostaja wsie, zamki i miasta gry. Tak samo gra chowa tabliczki wsi.

### 1.2 Najechanie myszka (dymek)
```
Brackenford
A village of the Whitebrook district
24 settlements, about 6,000 souls
State: Standing            (albo: Burning / Partly burned / Burned / Abandoned / Being rebuilt)
```
Kursor zostaje zwykly (wioski nie da sie kliknac). Klik idzie w teren pod spodem - jedziesz tam, gdzie kliknales.

### 1.3 Rabunek: wioska plonie
- Ktos rabuje wies gry (gracz, lord AI, Inni; Nocna Straz nie pali wsi za Murem - decyzja H1): **ogien i dym** pojawiaja sie nad **najblizsza droga wioska tego
  okregu** (od strony, z ktorej przyszedl wrog). Wies gry plonie jak dzis (tam stoi oboz napastnika i skrzyzowane miecze gry).
- Komunikat (tylko sprawy gracza - jego lenna albo jego rabunek; ustawienie MCM rozszerza na krolestwo):
  "Smoke over Brackenford: Ser Gregor is burning your village in the Whitebrook district."
- Nad plonaca wioska (z bliska) napis: **"Brackenford is burning"**.
- Duza armia (2 000) pali naraz 10 osad - plona wtedy 1-2 wioski jednoczesnie.

### 1.4 Po rabunku: dym, wioska maleje, spalona
- Przez czujnosc wsi gry (2-17 dob, PROJEKT-RABUNEK pkt 6) nad trafionymi wioskami **unosi sie dym**.
- Wioska **maleje**: im mniej ludzi w domu, tym mniej domow stoi (poziom 3 -> 2 -> 1).
- Gdy splonie albo opustoszeje **polowa jej osad** (decyzja Jeffa), wioska jest **spalona**: zgliszcza, ciemne domy, napis
  **"Brackenford has been burned"** i komunikat "Brackenford in the Whitebrook district has been burned by Ser Gregor."
- Ile to trwa (rabunek osada po osadzie, wioska przy drodze 24 osady, spalona od 12) [ZMIERZONE z PROJEKT-RABUNEK]:
  - lord AI (112 ludzi, 1 osada na sesje w ok. 4.6 doby): ok. 12 sesji - pogranicze w dlugiej wojnie, kilka miesiecy;
  - armia 2 000 (10 osad na sesje): 2 sesje;
  - gracz 300 (osada w ok. 1.8 doby): ok. 22 doby ciaglego palenia;
  - plonie za to KAZDA sesja, od pierwszej osady.

### 1.5 Odbudowa
- Ludzie wracaja z ucieczki (powroty 113, ok. 0.1% dziennie x bezpieczenstwo). Osady stana znowu, gdy wroca ich ludzie.
- Spalona wioska wstaje po ok. **2 latach** pokoju (1.6-3.2 roku wedlug bezpieczenstwa, d-ksiega). Najpierw wioski przy
  mostach i skrzyzowaniach, potem przy drogach, ubocze na koncu (tak bylo historycznie: odbudowa od rynku i traktu).
- Komunikat: "Brackenford in the Whitebrook district has been rebuilt. Its people have come home."
- W czasie oblezenia, rabunku, czujnosci i przy pustym spichlerzu nikt nie wraca - wioska zostaje spalona.

### 1.6 Menu okregu
W menu wsi gry (takze spalonej, "village" i "village_looted") opcja **"Look over the district"**:
```
Villages of the Whitebrook district (7 villages, 243 settlements, 227 standing):
Hag's Bridge, by the bridge: burning now (Ser Gregor's men) - 2 of 40 settlements burned.
Long Croft, on the road: burned - 14 of 24 settlements are ash; its people shelter in Winterfell.
Crosswood, at the crossroads: 40 settlements, all standing, about 10,000 souls.
...
Whitebrook (the district village): 67 settlements, all standing.
The granary holds food for about 210 days.
```
Spichlerz i linie rabunku dopinaja ich paczki (rozdz. 11).

### 1.7 Gdzie wioski nie ma
- W 134 z 571 okregow nie zmiescila sie zadna wioska z sensem: 59 nie ma ani kawalka dozwolonego ladu (Zelazne Wyspy, Qarth,
  Dothrakowie bez wody, Skagos), 10 to male okregi Strazy i za Murem (plan 0), 65 to ciasne wybrzeza, wyspy i okregi zjedzone
  przez gory, puszcze i bliskosc osad gry. Tam wszystkie osady
  trzyma wies gry: plonie ona sama (jak dzis), a trwale spalona jest wtedy, gdy splonie polowa jej osad (rozdz. 5.5).
- Rownina bez drog namalowanych (Reach na polnoc od Highgarden, poludnie od Oldtown, wnetrze Essos) jest rzadko obsadzona - ROT nie
  namalowal tam drog, a "losowego rzucania" Jeff nie chce (decyzja 12.1).

---

## 2. Zasady rozstawienia (prototyp v2, `W\p-prototyp\v2\generator.py`)

### 2.1 Skala i co znaczy obrazek
- 1 jedn. mapy = ok. **2.84 km** (Mur 170 jedn.; widelki 2.45-4.6) [ZMIERZONE, a-teren].
- Prawdziwe wsie stoja co 1.5-3 km = 0.5-1 jedn. - nie do narysowania. Obrazek to wiec **osrodek gromady osad**: stoi tam, gdzie
  lezalaby najwieksza z nich (przy przeprawie, na skrzyzowaniu, przy drodze).
- Mediana odstepu miedzy wioskami w v2: **10 jedn. (ok. 28 km)**; najblizsza wioska od wsi gry: mediana 7.8, 10% 6.3, 90% 16.5 jedn.

### 2.2 Dozwolony lad (warunek konieczny)
- Siatka ruchu ROT (navmesh) Plain / Steppe / Desert / Forest; nie Mountain (w ROT to tez koryta rzek i plot zeglugi), CoastalSea,
  Water, bagno (MUD w Przesmyku).
- Nie w gorach (warstwy skaly/lodu i nachylenie, nie sama tekstura), nie glebiej niz 3 jedn. w puszczy (flora.bin, od 0.15 drzewa /
  jedn.^2), nie na pustyni bez wody (takze suchy step = sciana Desert z trawa), nie w kepie drzew.
- **Przynaleznosc:** najwyzej 25 jedn. od swojej wsi gry i najwyzej 1.25 x dalej od niej niz od najblizszej innej wsi gry - inaczej
  gracz widzialby ogien przy innej wsi niz napadnieta.
- Pasy regionow: Zelazne Wyspy tylko 1-4.5 jedn. od morza; Qarth tylko brzeg / rzeka / jezioro (4.5 jedn.); Dothrakowie tylko nad
  rzeka / jeziorem (5 jedn.), najwyzej 3 na okreg, bez uboczy.
- **Historycznie:** wies zyje z pola, wody i drogi do targu w swoim okregu; w gorach, bagnie i puszczy siedza pojedyncze zagrody,
  nie wsie; kto mieszka blizej innej wsi, ten do niej nalezy.

### 2.3 Obrys (caly, nie srodek)
- Prostokat **2.6 x 1.4 jedn.** (promien 1.48; domy ROT rozchodza sie 1.0-1.9 jedn. od srodka przy skali 1, u nas skala 0.6-0.8),
  91 probek, z obrotem wioski. Warunki: 100% scian Plain/Steppe/Desert/Forest; zero morza, jeziora, koryta (< 0.9 od sciezki rzeki) i
  pasa drogi namalowanej; roznica wysokosci <= 0.25 (mediana 0.055); zero drzew z flora.bin w obrysie + 0.3; srodek >= 1.8 jedn. od
  osi drogi namalowanej.
- Po to, zeby w grze domy nie stanely w rzece, na drodze, na skarpie ani miedzy drzewami (v1 mial 286 / 187 / 48 / 1 185 takich).
- **Rozmiar obrysu w grze nie jest zmierzony** - rozstrzyga W1 (rozdz. 11).

### 2.4 Kolejnosc stawiania (cala mapa, faza po fazie)
1. **H - wezly** (priorytet): most 10, brod przy drodze 9, ujscie / przystan 8, widly rzek / skrzyzowanie drog namalowanych 7, zatoka 6,
   rozstaje (2 drogi namalowane + drozka) 5. Najwyzej 1/4 planu okregu. Kotwice blizej niz 10 jedn. = jedna wioska (most + ujscie +
   skrzyzowanie = jedna duza wioska, nie cztery ikony). Wioska stoi w pierscieniu 1.5-5.5 jedn. od kotwicy (nie na moscie).
   **Historycznie:** przeprawa i skrzyzowanie skupiaja ruch, myto, targ, karczme, kuznie - tam rosly najwieksze wsie.
2. **R - wzdluz linii**, w kolejnosci wagi: droga namalowana glowna 1.0, lokalna 0.9, czesciowo malowana 0.75-0.85, rzeka 0.8 (Polnoc
   0.95, Dorne 1.05), brzeg jeziora 0.7-1.0, brzeg morza 0.6-1.1, droga wyliczona 0.3 (rozdz. 2.5).
   - Krok dobrany do okregu, losowany x 0.7-1.8 (nierowne odstepy).
   - Strona linii wedlug oceny gruntu (lepsza strona wygrywa kilka razy pod rzad - bez "zamka blyskawicznego").
   - Przerwa w lancuchu, gdzie grunt jest slaby (< 0.5 sredniej okregu).
   - Jeden rzad w korytarzu droga + rzeka (wioska odpada, gdy inna stoi w poprzek blizej niz 12 jedn.).
   - 1.5-3 jedn. z boku drogi (domy nie na drodze), nad rzeka do 5.2, nad morzem do 4.2, nad jeziorem do 3.8 (szerokie koryta).
   **Historycznie:** wies ulicowa ciagnie sie wzdluz traktu i rzeki, ale nie w rownych odstepach i nie po obu stronach naraz.
3. **Dopelnienie** - gesciej (krok minimalny) tylko wzdluz drog namalowanych, rzek, jezior i brzegu; nigdy na uboczu.
4. **F - kilka na uboczu** przy polach: 3.5-10 jedn. od drogi (najwyzej dzien drogi), najwyzej 1.5 jedn. w las; udzial wedlug profilu
   regionu liczony wobec postawionych H + R (zawsze wolno 1). **Historycznie:** zagrody i male wsie przy polach, z drozka do traktu.

### 2.5 Drogi widoczne i wyliczone
- ROT namalowal drogi w terenie (terrain.bin, 10 016 jedn., 122 skrzyzowania) glownie miedzy twierdzami; do wsi gry dochodzi ich
  malo (39 z 571 wsi ma brame <= 3 jedn. od drogi) [ZMIERZONE, a-teren].
- a-teren wyliczyl siec drozek wies -> twierdza i traktow (najtansze sciezki). **W grze ich nie widac**, wiec:
  - waga 0.3 (ostatnie w kolejce), wioska przy nich tylko tam, gdzie w promieniu 15 jedn. nie ma drogi namalowanej ani rzeki,
    najwyzej 2 na sciezke wies -> twierdza;
  - skrzyzowanie (kotwica i nazwa "-cross") tylko przy wezle z >= 3 odcinkami namalowanymi;
  - cechy nazwy "na drodze" / "skrzyzowanie" tylko przy farbie.
- Wynik: przy niewidocznej drodze stoi 17% wiosek R (v1: 34%), u Dothrakow 0%.
- Siec wyliczona sluzy dalej do kolejnosci palenia i "odleglosci po drodze" (ksiega, rozdz. 6).

### 2.6 Odstepy
- Wioska - wioska wedlug profilu (wieksza z dwoch): gesty 6.5, zwykly 7, rzadki 9, woda 7, brzeg 6.5 (ubocze 9-13); kazda wioska >= 8
  od wioski H. Od miasta 12, od zamku 9, od wsi gry i kryjowki 6, od mostu 2 jedn.
- v1 (odstep 4.5) dawal dywan i siatke (693 wiosek z >= 6 sasiadami w 10 jedn.); v2: 0.

### 2.7 Liczba na okreg
- Gorna granica: min(plan d-ksiega = round(n/24), 16, 1 na 60 jedn.^2 dozwolonego ladu, Dothrakowie 3); mala wyspa z jakimkolwiek
  dozwolonym ladem: 1, jesli obrys sie zmiesci.
- Postawione: tyle, ile zmiesci sie z sensem (reguly 2.2-2.6). Glowny powod brakow: w 216 okregach jest za malo widocznych linii
  (drogi namalowanej, rzeki, jeziora, brzegu) - cel R 1 090, postawione 400.

### 2.8 Profile regionow i wynik v2 [ZMIERZONE, `W\p-prototyp\v2\wyniki\raport.txt`]

| Region | Profil (odstep, ubocze) | Okregi | Plan d-ksiega | Po sufitach | Postawione | Mediana odstepu | H / R / F |
|---|---|---|---|---|---|---|---|
| Polnoc | rzadki (9, 30%) | 57 | 451 | 316 | 173 | 13.0 | 19 / 63 / 18% |
| Dorzecze | gesty (6.5, 15%) | 43 | 528 | 253 | 172 | 8.7 | 18 / 69 / 13% |
| Reach | gesty | 40 | 1 141 | 282 | 146 | 9.3 | 23 / 62 / 15% |
| Norvos, Pentos, Myr, Qohor | zwykly (7, 20%) | 50 | 570 | 517 | 309 | 8.6-10.3 | |
| Zachod | zwykly | 33 | 603 | 113 | 83 | 8.8 | 29 / 58 / 13% |
| Volantis | zwykly | 18 | 616 | 185 | 77 | 10.1 | 13 / 66 / 21% |
| Dolina | zwykly | 31 | 473 | 108 | 77 | 10.5 | 21 / 66 / 13% |
| Zatoka Niewolnicza (Ghis) | woda (7, ubocze tylko przy wodzie) | 26 | 282 | 141 | 71 | 9.1 | 17 / 82 / 1% |
| Ziemie Burzy | zwykly | 31 | 389 | 89 | 61 | 9.8 | 34 / 52 / 13% |
| Dorne | woda | 38 | 229 | 58 | 52 | 12.6 | 27 / 73 / 0% |
| Braavos | zwykly | 13 | 285 | 79 | 48 | 8.6 | 21 / 71 / 8% |
| Tyrosh, Lys, Lorath | | 36 | 293 | 176 | 118 | 9.8-10.4 | |
| Ziemie Korony | gesty | 11 | 27 | 27 | 22 | 9.5 | |
| Smocza Skala | zwykly | 23 | 104 | 18 | 14 | 15.0 | |
| Dothrakowie | rzadki, tylko woda | 17 | 127 | 11 | 10 | 13.0 | 20 / 80 / 0% |
| Zelazne Wyspy | brzeg (tylko pas brzegu) | 28 | 84 | 17 | 2 | - | |
| Qarth | woda, tylko brzeg / rzeka | 11 | 404 | 6 | 2 | - | |
| reszta (Sarnor, Ibben, za Murem, Yi Ti, Valyria, Wyspy Letnie) | | 50 | 163 | 63 | 46 | | |
| **Razem** | | **571** | **6 778** | **2 462** | **1 483** | **10.0** | **H 280, R 991, F 212** |

Miejsca: droga 466, rzeka 322, ubocze 212, brzeg 201, skrzyzowanie 98, most 69, rozstaje 45, zatoka 43, przystan 13, ujscie 6,
widly 5, jezioro 2. Na okreg: mediana 3 (wsrod okregow z wioskami), najwiecej 12.
Sprawdziany na gotowych wioskach (cala mapa): 0 naruszen kazdej reguly z 2.2-2.6 i z nazw (rozdz. 4).

### 2.9 Obrot
- Ulica wioski (dluga os) wzdluz drogi / rzeki / brzegu, front (do drogi, wody, przeprawy) z pliku (`road_deg`, `front_deg`), +-5 st.
  z ziarna polozenia. Widok liczy obrot z `road_deg` i dlugiej osi wsi-matki (os glowna rozkladu domow), wiec nie trzeba recznej
  poprawki 0/90/180 st. na prefab - sprawdza W1.

### 2.10 Powtarzalnosc i aktualizacja ROT
- Ziarna z id wsi (lancuch, ubocze) i z polozenia (obrot), kolejnosc faz po id okregow; trzy przebiegi daja ten sam plik.
- Uklad liczy Python raz (a-teren 112 s + v2 21-26 s) i wpisuje do pliku moda (rozdz. 7.1). Gra nic nie losuje.
- Nowa wersja mapy ROT: ponowny przebieg; mod sprawdza CRC sceny (`IMapScene.GetSceneXmlCrc`) zapisane w naglowku pliku; gdy sie nie
  zgadza - wioski wylaczone (wpis w logu), ksiega liczy dalej (wszystko przy wsiach gry).

---

## 3. Ile osad w wiosce (ZMIANA wobec v2 - decyzja projektu)

v2 przydzielal osady tak, by obrazki wziely jak najwiecej (do 48, wezly do 72; mediana 48, w obrazkach 39.5% osad, w 130 okregach
100% - wies gry pusta). To psuje dwie rzeczy, ktore Jeff przyjal: "obrazek = gromada ok. 24 osad" i "spalony po spaleniu polowy".
Przy 48 osadach wioska spalilaby sie dopiero po 24 osadach - lord AI (1 osada na sesje) prawie nigdy by jej nie spalil.

**Zasada [PROJ]:**
- wielkosc wioski wedlug miejsca: **wezel (H) 40 osad, przy linii (R) 24, na uboczu (F) 12** (wagi d-ksiegi 2.0 / 1.2 / 0.6;
  srednio ok. 24, jak przyjal Jeff);
- **wies gry trzyma reszte okregu, ale co najmniej tyle, ile najwieksza jej wioska** - to glowna wioska okregu (dwor, kosciol,
  targ), zawsze najwieksza; w malym okregu wszystkie wioski zmniejszone w tej samej proporcji;
- liczbe osad n = round(L0 / 250) liczy gra (PROJEKT-RABUNEK pkt 1); plik danych daje tylko klase miejsca, wielkosci wylicza mod.

Wynik na v2 [ZMIERZONE, okregi.tsv]: w wioskach ok. 36 tys. z 168 tys. osad (21.6%), reszta przy wsiach gry; 66 okregow
zmniejszonych proporcjonalnie. Poziom swiezy: 3 - 239 (wezly), 2 - 961 (przy linii), 1 - 283 (ubocze i male okregi) - wezly
wygladaja na duze, ubocze na male, jak w rzeczywistosci.

Dlaczego wioski pala sie pierwsze (rozdz. 6.2): napastnik rozpuszcza oddzialy po okolicy wzdluz drog; osady wokol wsi gry (dwor,
wieza, milicja) bronia sie najdluzej. Dzieki temu kazdy rabunek widac najpierw na nazwanej wiosce.

---

## 4. Nazwy (`W\c-nazwy` + nakladka `W\p-prototyp\v2\nazwy_v2.py`)

- **Krainy:** 30 regionow z kultury osady (battania = Polnoc itd.; Dorne w 3 odmianach: piaski, Czerwone Gory, Rhoynarowie nad
  Greenblood i Scourge). Rdzenie i koncowki z 790 nazw osad ROT i ok. 400 miejsc z ksiazek.
- **Nazwa pasuje do miejsca:** -ford tylko przy brodzie, -bridge przy moscie, Cross-/-cross na widocznym skrzyzowaniu, -haven/Salt-/
  -strand nad morzem, -mill/-weir/-bend przy rzece, -mire/-fen na bagnie, -wood/-holt/-hurst/-den tylko w lesie, -fell/-crag na
  wzgorzach, -field/-ton na rowninie; przy rzece z nazwa tez "Mander Mill", "Redfork Bend", "Noyne Ferry".
- **Essos:** sylaby kazdej kultury z prawdziwych nazw ROT i ksiazek (Maenogyr, Aleni Noylys, Iksa Sareva, Qaleznak); przy moscie,
  brodzie i morzu rosnie udzial nazw angielskich (Wreckstrand, Qhoyne Bend).
- **Czyste:** unikalne na cala mape; nie rowne i nie rozne o 1 litere od zadnej osady ROT ani nazwy z ksiazek (od 4 liter, tez
  kazde slowo w Essos); bez wspolnych 5 pierwszych liter z osada gry blizej niz 30 jedn.; zakaz nazw rodow, postaci, krolestw,
  tytulow BK (5 658 zakazanych); bez slow smiesznych, ognia ("Ashburn is burning"), wspolczesnych, z gier i polskich wulgaryzmow;
  tylko ASCII.
- **Bez powtorzen w kadrze:** pierwszy czlon nie powtarza sie w promieniu 30 jedn.; ta sama koncowka najwyzej 2 razy w 20 jedn. i
  ani razu w 12; ten sam dzierzawca ("Crofter's") raz na 60 jedn.; bez "X's Den".
- **Wynik v2:** 1 483 nazwy, wszystkie rozne, 0 naruszen; w kadrze 100 x 60 jedn. 90% kadrow ma najwyzej 9% powtorzonych czlonow.
- **Zamrozenie:** nazwy zaleza od calego ukladu (unikalnosc). Zamrazamy je (`generator.py --zamroz-nazwy`) dopiero po decyzji 12.1
  i po W1; potem kazdy przebieg zachowuje nazwy wiosek, ktore stoja w tym samym miejscu (`uid`).
- Przed zamrozeniem: przejrzec okiem cala liste (1 483 - da sie w pol godziny), dopisac zle slowa do czarnej listy.
- Generator nazw w C# (`c-nazwy\cs\VillageNameGen.cs`, zgodny z Pythonem 16 899 / 16 899) **nie jest potrzebny w modzie** - nazwy
  ida w pliku danych. Zostaje jako zapas.

---

## 5. Wyglad (`W\b-wyglad` + poprawki krytyka K1-K8)

### 5.1 Skad model wioski
- Encja wsi-matki w scenie ROT to osobna kompozycja (nie prefab): prefab ikony z Calradii + domy ROT (andal_village3..10,
  fm_village1..4) + siatka zgliszcz gry + kula kolizji `_bo` / `bo_village`.
- **Dwie drogi, wybor w W1:**
  - (a) `GameEntity.CopyFrom(scena, encja matki, createPhysics:false, callScriptCallbacks:false)` (GameEntity.cs:369-372; precedens
    NavalDLC VisualShipFactory.cs:136) - wyglad 1:1 z matka, ale kopiuje natywny skrypt "Town Entity Manager" (563 wsi), ktorego
    kodu nie widac (tylko TaleWorlds.Native.dll);
  - (b) `GameEntity.Instantiate` prefabu domow wsi-matki (`dekor_matki` w pliku danych) + siatka zgliszcz z prefabu ikony, zawsze
    z `GameEntity.PrefabExists` (bez tego czerwony TEMP - NightRest.cs:583-584).
  - Projekt: (b) dla 307 wsi ROT, ktore i tak pokazuja tylko domy andal/fm; (a) dla Essos i kultur z siatkami poziomow - chyba ze W1
    pokaze, ze skrypt na kopii niczemu nie szkodzi (wtedy (a) wszedzie).
- Kopia **zawsze**: nowa nazwa `arm_mapvillage_<uid>` zaraz po utworzeniu (gra szuka osad po nazwie encji: SettlementVisual.cs:465,
  MapScene.cs:606), `RemoveTag("village")`, usuniete dzieci `_bo` i `bo_village` (`Remove(112)` jak SettlementVisual.cs:539-542),
  bez ciala fizyki. Nie dopisywana do `MapScreen.VisualsOfEntities`, wiec menedzery osad i partii jej nie widza.
- Flaga `not_affected_by_season` dziedziczona po matce: na Polnocy, za Murem i na Skagos snieg jak na matce.
- Gotowosc: `SetReadyToRender(true)`, `SetEntityEnvMapVisibility(false)`, po pokazaniu `CheckResources(true, false)` (scena mapy
  laduje tylko widoczne; SettlementVisual.cs:439, :589-591).

### 5.2 Polozenie
- Wysokosc z `Campaign.Current.MapSceneWrapper.GetTerrainHeightAndNormal` (sam teren, nie ciala), opuszczenie o 0.02.
- Wioska stoi prosto (os "gora" pionowa albo normalna terenu - stok w obrysie <= 0.25 i tak); kazdy dom na wysokosci terenu albo
  `EntityFlags.AlignToTerrain` - do proby w W1 (krytyk K5).
- Obrot z `road_deg` i dlugiej osi wsi-matki (rozdz. 2.9).

### 5.3 Poziomy 1/2/3 (wioska maleje)
- Poziom z ludzi w domu wioski [PROJ, d-ksiega]: **3 od 9 000, 2 od 5 000, ponizej 1** (gra dla prawdziwych wsi: hearth 200 / 600,
  SettlementVisual.cs:1226-1238). Histereza 3% w pamieci - ikona nie miga.
- Pokazanie: algorytm gry (maska "civilian" / "looted" + "level_N", `SettlementVisual.RefreshLevelMask` :1189-1224, widocznosc
  dzieci `(GetUpgradeLevelMask() & maska) == maska` :1240-1268) - na kopii z nasza maska. NIE `SetUpgradeLevelMask` (wpisuje encji jej
  poziomy) i NIE `Scene.SetUpgradeLevelVisibility` (cala scena).
- 307 wsi ROT ma ten sam wyglad na 1/2/3 (domy bez poziomow). Tam poziom = **ile domow stoi**: poziom 2 bez ok. 1/3 domow, poziom 1
  bez ok. 2/3 (stale z ziarna uid) + skala 0.8 / 0.7 / 0.6 (zawsze mniejsza od matki). 17 wsi Dothrakow: 1 siatka (3 duplikaty usuniete).

### 5.4 Plonie, dym, spalona
- **Plonie** (krok rabunku pali osady tej wioski): zdjecie DoNotTick, `AddParticleSystemComponent("psys_fire_smoke_env_point")`,
  zdjecie z predisplay - dokladnie jak gra dla rabowanej wsi (SettlementVisual.cs:405-421; flagi 0x20000000 DoNotTick,
  0x10000000 Ignore).
- **Dym** (wioska trafiona w biezacej czujnosci wsi gry): `"map_icon_village_plunder_fx"` (:423-432).
- **Spalona** (>= 50% osad pustych): maska "looted" + poziom z tego, co zostalo (gra laczy oba, :1196); dla domow ROT dodatkowo
  przyciemnienie `GameEntity.SetFactorColor` (GameEntity.cs:272) na dzieciach - skrypt "Town Entity Manager" ma pole Override Factor
  Color i moze to nadpisac (W1). Dothrakowie nie maja siatki zgliszcz - tylko przyciemnienie.
- Zmiana stanu = jedno przelaczenie (CLAUDE.md 7: nic co klatke). Widok przelicza tylko wioski z brudnych okregow, 4 razy na sekunde.

### 5.5 Wies gry (glowna wioska okregu)
- Plonie i dymi jak dzis (gra: MapEvent.IsRaid, stan Looted 2-17 dob).
- **Nowe:** trwale wyglada na spalona, gdy splonie polowa JEJ osad (rozdz. 3) - postfiks na `SettlementVisual.RefreshLevelMask`
  dopisuje "looted" do maski (tylko przy zmianie stanu, gra wola to przy brudnej masce). Bez tego wies gry wygladalaby na cala, gdy
  wszystkie wioski wokol leza w popiele, a w 134 okregach bez wiosek trwalego spalenia nie byloby widac wcale (zasada 100%).
  Poziom wsi gry zostaje z gry (hearth calego okregu, po 108/113 = ludzie).

### 5.6 Nazwa na mapie
- **Dymek po najechaniu** (wszystkie wioski): wlasny komponent `CampaignEntityVisualComponent` (dziedziczy `EntityVisualManagerBase`,
  rejestrowany `SandBoxViewSubModule.SandBoxViewVisualManager.AddEntityComponent<T>()`, Priority 100 - partie 10 i osady 40 maja
  pierwszenstwo; MapScreen.cs:1588-1653). Szuka wioski przy punkcie terenu pod kursorem w obroconym prostokacie obrysu + 0.3 (siatka
  komorek 50 x 50); ustawia tylko `hoveredVisual`, `IsMobileEntity = true` (kursor bez raczki), klik idzie w teren. Tresc:
  `InformationManager.ShowTooltip(typeof(List<TooltipProperty>), ...)`, budowana tylko przy zmianie najechania.
- **Napisy nad wioskami** (W4): wlasna warstwa Gauntlet (jak "MapNameplateLayer", GauntletMapBasicView.cs:26), pozycja z
  `MBWindowManager.WorldToScreenInsideUsableArea`, tylko z bliska. Domyslnie **tylko wioski plonace i spalone** w kadrze (najwyzej
  ok. 30): "Brackenford is burning" / "Brackenford has been burned". Wszystkie nazwy z bliska - ustawienie (decyzja 12.2); gra odswieza
  1 066 tabliczek co klatke (SettlementNameplatesVM.cs:243), dla 1 500 wiosek limit 60 najblizszych.

### 5.7 Widocznosc i tworzenie
- Kopie tworzone leniwie, gdy kamera sie zbliza (komorki 50 x 50); pokazane w promieniu (z kamery + 120) i przy z <= 160 (ustawienie).
  Pierwsze wypelnienie po wczytaniu bez limitu (wioski nie wyskakuja), potem po 48 na cwierc sekundy.
- ScoutingFog (HideSettlements): wioska chowana razem z niewidoczna matka (`Settlement.IsVisible`).

### 5.8 Co trzeba poprawic w szkicu B (`W\b-wyglad\szkic\WioskiNaMapie.cs`) przy pisaniu W2/W3
- K1 (BLAD): zdarzenia rabunku wolaja tylko `MarkDistrictDirty` - ogien by sie nie zapalil. Kazde zdarzenie najpierw pyta ksiege
  (`MapVillages.StateOf`), potem brudzi okreg.
- K2: widok NIC nie liczy sam (bez wlasnego wzoru "spalone = n x uchodzcy / L", bez wlasnej kolejnosci i poziomow) - tylko czyta
  `StateOf(seat, i)`; z `HamletState` zostaje pamiec podreczna widoku.
- K3: poziom przez ukrywanie domow + przyciemnienie (5.3, 5.4); K4: dymek na prostokacie obrysu; K5: wysokosc domow; K6: wybor
  CopyFrom / Instantiate (5.1); K7: zero slowa "hamlet" (41 wystapien w szkicu) - `MapVillages`, `arm_mapvillage_*`, "Map Villages".
- Referencje: Armoury dostaje `SandBox.View.dll` i `SandBox.dll` (`Private=false`, zmiana csproj + `libs/README.md`); dziedziczenia
  nie da sie zrobic refleksja (dzis Armoury siega do SandBox.View refleksja, NightRest.cs:568-575).

---

## 6. Powiazanie z ksiega ludzi i rabunkiem (`W\d-ksiega`; jedno zrodlo prawdy)

### 6.1 Zasada
- Ludzi rusza **tylko ksiega 113** (Strike: zabici / w las / jency / uchodzcy; powroty; konto "u:" w `arm_people`).
- Warstwa wiosek (`MapVillages`) trzyma tylko **b_w = liczbe pustych osad kazdej wioski** (i wsi gry). Przyrost b tylko z wpiecia w
  Strike (trafieni / ludzie na osade S_v = L / n); spadek tylko z uzgodnienia z kontem uchodzcow. Nie rusza hearth, kont, spichlerza.
- Mapa (B), dymek, menu, okienko rabunku i komunikaty czytaja `StateOf(seat, i)` - te same liczby. Spor B / D rozstrzygniety na
  korzysc D (krytyk K2).
- **Poprawka do PROJEKT-RABUNEK pkt 1 (blad B1 z d-ksiegi):** "puste = round(n x uchodzcy / L)" zaniza o udzial zabitych i
  zbieglych w las (1 spalona osada -> 0.90; 31 -> 28.08). Liczba pustych osad idzie z trafien (suma b_w); wzor tylko jako przydzial
  zastepczy, gdy brak zapisu.

### 6.2 Lancuch palenia (ktore osady plona)
- Napastnik stoi przy wsi gry (RaidEventComponent.cs:113-117).
- Pierwsza wioska sesji = najmniejsze `droga(wies gry -> w) x (1 - 0.3 cos theta)` wsrod wiosek z wolnym miejscem; theta = kat miedzy
  kierunkiem wioski a kierunkiem najblizszej twierdzy frakcji napastnika (wrog pali od swojej strony; bez twierdzy cos = 0).
- Wolne miejsce wioski = 0.9 x n_w - b_w (ten sam pulap 90% co Room 113, Devastation.cs:129, :194-195).
- Wioska plonie do swojego pulapu, potem nastepna najblizsza **po drodze** (Dijkstra po grafie z pliku: `neighbors`, `road_dist`).
- **Wies gry na koncu lancucha** (rozdz. 3). Okreg bez wiosek = wszystko w wsi gry.
- Krok m osad (PROJEKT-RABUNEK pkt 2): plan kroku = poczatek lancucha pokrywajacy m osad; te wioski PLONA przez caly krok.
- Rabunek gry bez paczki osada po osadzie (samo 113): Settle -> Strike na koncu (Devastation.cs:349-368) tym samym lancuchem; plonie
  pierwsza wioska lancucha, poki `Settlement.IsUnderRaid`. Punkt wroga P zapamietany w RaidState (Devastation.cs:287-303).
- Zerowanie przez marsz armii (ScorchedEarth): od wioski najblizej obozu (ScorchedEarth.cs:75-91) - bez ognia, wioska tylko maleje.
- Glod (gdy jest spichlerz): uchodzcy z glodu oprozniaja najpierw ubocze (F, od najdalszej), potem R, potem H - przyczyna "abandoned".
- Odleglosc na mapie decyduje tylko o kolejnosci; czas kroku zostaje z PROJEKT-RABUNEK (2.6 km / sqrt(u) / 20 km na dobe).

### 6.3 Stany wioski

| Stan | Kiedy | Na mapie |
|---|---|---|
| stoi, poziom 3/2/1 | ludzie w domu wioski >= 9 000 / >= 5 000 / mniej | domy wedlug poziomu |
| plonie | nalezy do planu biezacego kroku czynnej sesji (gracz albo AI); bez paczki osada po osadzie: pierwsza z lancucha przy IsUnderRaid | ogien + dym, napis "X is burning" |
| dymi | dostala b w biezacej czujnosci wsi gry (Looted) | dym (pamiec, bez zapisu - po wczytaniu w czujnosci dymu brak, najwyzej 17 dob) |
| spalona | b_w >= 0.5 n_w z rabunku / zerowania | zgliszcza, ciemne domy, napis "X has been burned" |
| opuszczona | b_w >= 0.5 n_w, prog przekroczony przez glod | jak spalona, bez dymu; dymek "Abandoned" |
| odbudowa | b_w spada (powroty) ponizej 0.5 n_w | normalny wyglad, poziom z ludzi; komunikat "rebuilt" |

Wioska moze plonac i byc spalona naraz. Okreg wypalony (Z = 0, PROJEKT-RABUNEK pkt 6): wszystkie na 0.9 n_w.

### 6.4 Odbudowa
- Ubytek konta uchodzcow (powroty 113 Devastation.cs:521-523; smierc uchodzcow w PROJEKT-GLOD 2.5) zdejmuje b proporcjonalnie do
  b_w x waga [PROJ]: wezel 1.5, przy linii 1.0, ubocze 0.5, wies gry 1.5. Konto 0 -> wszystkie b = 0.
- Od 90% do 50% pustych: 587 / 734 / 864 / 1 175 dob przy bezpieczenstwie 100 / 50 / 20 / w wojnie = 1.6 / 2.0 / 2.4 / 3.2 roku
  (kalendarz Armoury 364 dni) [ZMIERZONE, d-ksiega sym.py] - zgodne z "odbudowa ok. 2 lata".
- Bez powrotow w BeingRaided / Looted / oblezeniu (ReturnRate :476-478) i przy spichlerzu < 90 dni (PROJEKT-GLOD 2.6).

### 6.5 AI
- Ta sama regula (lord 1 osada, Inni 1 000 5 osad, armia 2 000 10 osad na krok, od strony swojej frakcji). AI nie widzi wiosek;
  cel wybiera jak dotad (tylko Normal i bez MapEvent, AiMilitaryBehavior.cs:424-431). Bez zmian w AI (CLAUDE.md 8.3).
- Komunikaty o AI tylko wedlug filtra (rozdz. 10).

### 6.6 Rabunek osada po osadzie (PROJEKT-RABUNEK) - co zmienia w nim ten projekt
- Plan kroku z `MapVillages.Plan` (nazwy wiosek zamiast "settlement K"); w sesji pole `cur` (biezaca wioska).
- Okienko po osadzie: tytul "{VILLAGE} of the {DISTRICT} district" (po progu: "... has been burned"), zdanie "{VILLAGE}: {BURNED} of its
  {NW} settlements are ash." i "The next settlement lies in {NEXT_VILLAGE}; with {MEN} men burning it will take about {DAYS} days."
  (pelny tekst: d-ksiega rozdz. 5; teksty w jednym pliku - rozdz. 9).
- Pasek menu: "{DISTRICT}: {VILLAGE} is burning - settlement {K}, about {X} days left".
- Koniec: "{X} settlements burned in {D} days: 6 in Brackenford, 4 in Hawthorn End. Burned to the ground: Brackenford."
- Liczba "stoi / spalone" w okienku = suma b_w (nie wzor z uchodzcow) - spojnosc z poprawka B1.

### 6.7 Gdy czegos brakuje
- 113 wylaczone (MCM): Abandon zeruje konto -> b = 0; widok w "trybie gry": ogien na pierwszej wiosce lancucha przy IsUnderRaid, dym
  przy Looted, nic trwalego.
- Wioski wylaczone (MCM): bez ikon i komunikatow; ksiega wiosek liczy dalej (koszt ok. 0), po wlaczeniu stan jest dobry.
- Plik danych nie pasuje do sceny (CRC): wioski wylaczone, wszystkie osady w wsiach gry, wpis w logu.
- Armoury usuniety: ikony znikaja (nie ma ich w scenie), klucz w sejwie zostaje bez czytelnika.

---

## 7. Sejw

### 7.1 Co NIE idzie do sejwu
- Pozycje, nazwy, klasy, sasiedzi, wyglad: plik `Armoury\ModuleData\arm_map_villages.tsv` (generator; dzis
  `W\p-prototyp\v2\wyniki\arm_map_villages.tsv`, 1 483 wiersze, 320 KB). Kolumny: `id village_id name x y road_deg settlements order
  kind uid class side level road_dist toward_village neighbors front_deg half_len half_wid face` + naglowek z CRC sceny i crc pliku.
- Encje na mapie: scena mapy czytana od nowa przy kazdym wczytaniu (MapScene.cs:206, :210) - obrazki stawiane od nowa.
- Plonie: z `MapEvent` / sesji rabunku (gra je zapisuje); dym: pamiec.

### 7.2 Co idzie (jeden klucz tekstowy `arm_district` w ArmouryBehavior.SyncData, wspolny z PROJEKT-RABUNEK pkt 10)
- Sekcje z prefiksem, kazda paczka czyta i pisze tylko swoje:
  - `|L:<crc32 pliku ukladu>` (W3);
  - `|n:<idWsi>=<n>` - liczba osad okregu z pierwszego odczytu (W3; PROJEKT-RABUNEK pkt 1 uzywa tej samej);
  - `|w:<idWsi>=b0,b1,...,bW,bWsi` - puste osady wiosek i wsi gry, 2 miejsca po przecinku, tylko okregi z jakimkolwiek b > 0 (W3);
  - `|a:<idWsi>=i,j` - wioski >= 50% z przyczyny glodu (W3, gdy jest spichlerz);
  - sesje rabunku, czujnosc, uprowadzeni, pole `cur` (paczka osada po osadzie).
- **Dlaczego potrzebny (CLAUDE.md "nic do sejwu bez potrzeby"):** ksiega 113 trzyma tylko sumy okregu; z nich wynika ILE osad jest
  pustych, ale nie GDZIE. Bez zapisu po kazdym wczytaniu spalona bylaby inna wioska niz przed zapisem (zalezy od kierunku wroga, obozow
  armii, glodu).
- Rozmiar: wojna w kilkudziesieciu okregach < 2 KB; najgorzej (571 okregow) ok. 50 KB.
- **Uzgodnienie dobowe** (po Devastation.Daily i Famine.Daily, przed PeopleLedger.Daily): oczekiwane = konto wczoraj + uchodzcy z
  dzisiejszych Strike; konto mniejsze -> osady wstaja; wieksze bez Strike -> przydzial "abandoned"; konto 0 -> b = 0. b nigdy nie
  rozjedzie sie z ksiega.
- **Brak klucza / stary sejw / inny crc ukladu / smieci** (NaN, ujemne, ponad n_w, zla dlugosc): przydzial zastepczy z ksiegi
  (`T0 = min(0.9 n, n x uchodzcy / ((1 - zabici% - las%) x L))`) wzdluz lancucha od wsi gry; w logu "przydzial zastepczy w N okregach".
  Sejw sprzed 113 (bez konta "u:") = wszystko stoi. Nowa kampania niepotrzebna.
- Cofniecie DLL: gra przy nastepnym zapisie buduje dane zachowan od nowa (CampaignBehaviorDataStore.cs:65-77) - arm_district przepada;
  po powrocie przydzial zastepczy (te same sumy, inny rozklad).
- Reset w konstruktorze ArmouryBehavior, jak reszta stanu.

---

## 8. Wydajnosc

| Co | Ile | Zrodlo |
|---|---|---|
| Wiosek w promieniu 150 jedn. od wsi gry | srednio 90, 90% 174, max 222 (v1: 259 / 486 / 670) | [ZMIERZONE] v2 K8 |
| w promieniu 200 | 151 / 268 / 344 | jw. |
| Encji na wioske | kopia ok. 10 (mediana 13); prefab domow + zgliszcza 2-3 | [ZMIERZONE] b-wyglad koszt_kopii |
| Encji na ekranie | do ok. 2 500 przy kopii, ok. 700 przy prefabach | [SZACUNEK] |
| Cala mapa objechana | ok. 16 tys. encji (scena ma dzis 53 550) | [SZACUNEK] |
| Doba | +0 s - stan tylko przy zdarzeniach (Strike, start/koniec rabunku, uzgodnienie dobowe po okregach z b > 0) | projekt |
| Tick widoku | 4 x na sekunde: lista pokazanych + komorki w zasiegu; najechanie 9 komorek na klatke | b-wyglad |
| Wczytanie mapy | pierwsze wypelnienie 100-300 kopii; szacunek 0.05-0.2 ms na kopie = 5-60 ms | [AUTOTEST] |
| Czastki | tyle, ile rabunkow naraz + dym nad swiezo trafionymi - dziesiatki, jak dzis | b-wyglad |
| Sejw | typowo < 2 KB | d-ksiega |

Do zmierzenia w W1: klatki przy 3 wysokosciach kamery (cel: spadek < 5% przy domyslnej), czas wczytania, RAM. Jesli klatki spadna:
mniejszy promien, prefab domow zamiast kopii, chowanie wiosek przy szybkim ruchu kamery.

---

## 9. Teksty w grze (po angielsku; JEDEN plik zrodlowy: `W\c-nazwy\cs\VillageTexts.xml` + `VillageTexts.cs`, 36 szablonow)

- Napisy nad wioska: "{VILLAGE} is burning", "{VILLAGE} has been burned", "{VILLAGE} lies abandoned", "{VILLAGE} is being rebuilt".
- Dymek: "A village of the {DISTRICT} district", "{PEOPLE} souls in {SETTLEMENTS} settlements", "{RAIDER} is putting it to the
  torch.", "Burned {DAYS} days ago. {RETURNED} of {PEOPLE} souls have come back."
- Komunikaty: "Smoke over {VILLAGE}: {RAIDER} is burning your village in the {DISTRICT} district.", "{VILLAGE} in the {DISTRICT}
  district has been burned by {RAIDER}.", "Your men have burned {VILLAGE}.", "The people of {VILLAGE} have left their homes.",
  "{VILLAGE} in the {DISTRICT} district has been rebuilt. Its people have come home."
- Menu okregu: "Look over the district", "Villages of the {DISTRICT} district", linie z opisem miejsca (at the crossroads / by the
  bridge / at the ford / by the harbour / on the road / by the river / on the coast / among the fields).
- Okienko i pasek rabunku osada po osadzie: d-ksiega rozdz. 5 (do scalenia z tym plikiem w paczce rabunku).
- Slowa: wioska na mapie = "village" (z nazwa), osada 250 ludzi = "settlement", wies gry = "district" (w menu "the district village").
  Nigdzie "hamlet".

---

## 10. MCM (po angielsku; `python tools/gen_mcm.py` po dodaniu)

| Ustawienie | Domyslnie | Paczka |
|---|---|---|
| Map Villages Enabled | on | W2 |
| Map Villages Hide Above Camera Height | 160 | W2 |
| Map Village Names (Hover only / Burning and burned / All nearby) | Burning and burned (decyzja 12.2) | W2 / W4 |
| Map Village Messages (None / Own fiefs / Kingdom) | Own fiefs | W3 |
| Settlements Per Map Village (wioska przy linii; wezel x 5/3, ubocze x 1/2) | 24 | W3 |

Prog spalenia 50% to decyzja Jeffa - stala w kodzie, nie ustawienie.

---

## 11. Paczki do zrobienia w kolejnosci (numery nada skladanie; W = robocze)

| Krok | Paczka | Co zobaczy gracz | Wymaga | Sejw / kampania | Proba |
|---|---|---|---|---|---|
| W0 | **Dane** (Python, offline): v2 + wielkosci 40/24/12 (rozdz. 3) + przejrzenie nazw + zamrozenie nazw + CRC sceny -> `Armoury\ModuleData\arm_map_villages.tsv` | nic | decyzja 12.1; po W1 ewentualnie inny obrys (przebieg 21 s) | - | sprawdziany generatora 0 naruszen; md5 powtarzalny |
| W1 | **Proba wygladu** (TYLKO autotest, nigdy na stale): ok. 30 wiosek wokol 3 okregow (Whitebrook, Haybale, Merling's Cliff) | nic (DLL Jeffa przywracana, zgoda na autotest) | wstepny plik W0 | nie | zrzuty 3-4 miejsc: rozmiar obrysu w grze, os frontu, CopyFrom vs prefab, skrypt "Town Entity Manager", ukrywanie domow, SetFactorColor, AlignToTerrain, ogien/dym na kopii i na wsi gry naraz, klatki x 3 wysokosci, czas wczytania, RAM |
| W2 | **Wioski na mapie (widok)**: wszystkie wioski, dymek z nazwa, ogien na pierwszej wiosce lancucha przy rabunku, dym przy czujnosci; MCM on/off | nazwane wioski z sensem; ogien przy kazdym rabunku | W0, W1; NIE wymaga 108-113 | nic do sejwu; dziala na obecnym zapisie | autotest 40 dob + Jeff: mapa z bliska, najechanie, rabunek |
| (108-113) | **Ksiega ludzi** - grupy 3 (BetterEconomy + 108 + 109), 4 (110-112 + 114), 5 (113) wedlug STAN-PRAC | (ich opisy) | (ich kolejnosc) | arm_people itd. | (ich proby) |
| W3 | **Ksiega wiosek**: b_w, wpiecie w Strike 113 i RaidState (kierunek wroga), uzgodnienie dobowe, poziomy, spalona / opuszczona / odbudowa, komunikaty z filtrem, menu "Look over the district", trwale spalenie wsi gry (5.5), log "Wioski: dzien N" | wioska maleje, zostaje spalona, wstaje po latach; menu okregu | 113 wgrane i sprawdzone; W2 | `arm_district` (L, n, w, a); bez nowej kampanii | autotest 40 dob (rabunki AI, zerowanie armii) + suma b = ksiega co do ulamka |
| W4 | **Napisy na mapie**: "X is burning" / "X has been burned" nad wioskami w kadrze; tryb "All nearby" | nazwy plonacych i spalonych na mapie | W3 (dla "burned"); W2 | nic | zrzuty; brak nakladania na tabliczki gry; XML Gauntlet bez bledu |
| S | **Spichlerz krok A** (PROJEKT-GLOD; 4 bramki produkcji, elastycznosc 1.0) | (jego opis) | 108, 109, 113 | (jego) | (jego) |
| S+ | wpiecie "abandoned" z glodu w W3 (jesli S wejdzie po W3 - w paczce S; jesli przed - w W3) | wioski opuszczone z glodu | S, W3 | sekcja `a:` | autotest z glodem |
| R | **Rabunek osada po osadzie** (PROJEKT-RABUNEK) + wpiecie wiosek: plan kroku z nazwami, okienko i pasek z nazwami, pole `cur` | okienko "Brackenford of the Whitebrook district..." | 113, S, W3 | arm_district (sesje, czujnosc) | wedlug PROJEKT-RABUNEK pkt 11 |

Kolejnosc zalecana: W0 + W1 od razu (nie dotykaja gry Jeffa) -> W2 jako osobny test, kiedy Jeff zechce (decyzja 12.3; nie razem z
inna grupa - CLAUDE.md 8.2) -> grupy 3-5 -> W3 -> W4 -> S -> R. Kazda paczka: autor + niezalezny recenzent, build kod 0, kontrola
calosci (CLAUDE.md 8.0), wpis CHANGELOG, wylacznik w MCM, wgranie tylko na "wgraj".

Pliki kodu (plan):
- NOWY `Armoury\src\MapVillages.cs` (ksiega: wczytanie pliku, Apportion, OnStrike, Reconcile, Plan, StateOf, Export/Import, Reset,
  teksty, log) - W3; w W2 tylko wczytanie pliku i "tryb gry".
- NOWY `Armoury\src\MapVillagesView.cs` (komponent widoku: kopie, poziomy, ogien, dym, dymek, rejestracja z OnApplicationTick) - W2.
- NOWY `Armoury\src\MapVillageLabels.cs` + `Armoury\GUI\Prefabs\ArmMapVillageLabels.xml` - W4.
- `Devastation.cs` (113): wpiecie OnStrike po :258 w try/licznik potkniec; punkt P w RaidState; Forage z pozycja (ScorchedEarth.cs:91) - W3.
- `ArmouryBehavior.cs`: Reset, Reconcile w dobie, SyncData arm_district - W3. `Settings.cs` + gen_mcm.py - W2/W3/W4.
- `Armoury.csproj` + `libs/README.md`: SandBox.View.dll, SandBox.dll - W2.
- Bledy per obiekt: licznik potkniec, nigdy globalny wylacznik (CLAUDE.md 7, `_tentBroken`).
- Watki: Strike w ticku kampanii, widok w watku glownym - tablice b pod blokada (jak `_scars` w 113, Devastation.cs:65-66).

---

## 12. Decyzje Jeffa (tylko to, co zmienia gre; parametry decyduje projekt)

1. **Ile wiosek: sens czy liczba?** v2 stawia ok. 1 500 wiosek tylko tam, gdzie widac powod (most, skrzyzowanie, droga, rzeka,
   brzeg). Zyzne rowniny bez drog namalowanych przez ROT (na polnoc od Highgarden, na poludnie od Oldtown, wnetrze Essos) i Zelazne
   Wyspy zostaja prawie puste. Zluzowanie wszystkich regul naraz daje ok. 2 200, ale wraca czesc bledow v1 (wioski cudzej wsi, przy
   niewidocznych drogach, miedzy drzewami). **Zalecenie: sens (v2).**
2. **Napisy na mapie:** tylko nad wioskami plonacymi i spalonymi (reszta nazw w dymku po najechaniu) - **zalecenie** - czy nazwy
   wszystkich wiosek z bliska (wiecej napisow, moga zaslaniac nazwy wsi gry).
3. **Kiedy wgrac:** sam widok (W2: wioski z nazwami + ogien przy rabunku, bez trwalego spalenia) moze wejsc wczesniej, osobnym testem,
   przed ksiega ludzi 108-113; trwale spalenie i odbudowa dopiero po 113 (W3). Czy wgrywac W2 wczesniej?

Rozstrzygniete przez projekt (bez pytania): wielkosc wioski 40/24/12 osad wedlug miejsca, wies gry jako glowna wioska okregu i
trwale spalona przy polowie jej osad, wioski pala sie przed wsia gry, kierunek wroga, komunikaty tylko o sprawach gracza, zapis
`arm_district`, odbudowa najpierw wezly.

---

## 13. Ryzyka

1. **Nic nie widziane w grze.** Wyglad, ogien, dymek i klatki to kod gry przeczytany w dekompilacji (sprawdzony przez krytyka) i
   szkic, ktory sie kompiluje (Build succeeded), ale nie byl uruchomiony. Rozstrzyga W1.
2. **Rozmiar obrysu w grze** (2.6 x 1.4 przyjete z rozrzutu domow x skala): za maly - domy wejda na droge / w drzewa; za duzy - jeszcze
   mniej wiosek. W1 + ponowny przebieg W0 (21 s).
3. **Skrypt "Town Entity Manager" na kopii** (CopyFrom): nie wiadomo, czy tyka, nadpisuje kolor albo przeszkadza przy usuwaniu.
   Zapas: prefab domow (5.1 b).
4. **Dwa ognie:** w trakcie rabunku plonie wies gry (gra) i wioska (my). Jesli w W1 wyglada to zle - gaszenie ognia wsi gry, gdy
   pala sie osady wioski (ten sam postfiks co 5.5); decyzja po zrzucie.
5. **Pas Mountain wzdluz rzek i granica CoastalSea** moga byc w grze szersze / wezsze niz widoczna woda - reguly obrysu moga byc za
   ostre (Zelazne Wyspy: 2 wioski).
6. **Liczba wiosek** (1 483, mediana 3 na okreg) duzo mniejsza niz przyjete "ok. 10" - decyzja 12.1; w wioskach ok. 22% osad, reszta
   w wsiach gry (rozdz. 3). Trzeba to jasno pokazac Jeffowi (for_jeff).
7. **Wies gry: czujnosc vs spalenie** - wies gry jest Looted 2-17 dob (gra), a wioska spalona przez lata; po zmianie 5.5 wies gry tez
   bywa trwale spalona. Dwa rozne obrazy - wytlumaczone w tekscie dla Jeffa (dym = swieze, zgliszcza = spalona).
8. **Postfiks na SettlementVisual.RefreshLevelMask** (5.5) to latka na kod wizerunku mapy: tylko przy zmianie maski, bez pracy co
   klatke; zle zrobiona moze migac ikona wsi. Proba w W3.
9. **Nowe referencje** SandBox.View / SandBox: przy aktualizacji gry kod moze sie nie zbudowac.
10. **Zalew komunikatow** przy wielu wojnach - filtr (domyslnie tylko lenna gracza i jego rabunki).
11. **Kolejnosc palenia i kierunek wroga** to regula projektu [PROJ] (najblizsza twierdza frakcji); bandy i Inni bez twierdzy pala od
   wsi gry.
12. **Cofniecie DLL / zmiana ukladu** gubi rozklad spalonych (przydzial zastepczy - te same sumy, inny rozklad).
13. **Nazwy:** czarne listy reczne, pelnej listy nikt jeszcze nie przejrzal okiem (W0). Kazda zmiana rozstawienia przed zamrozeniem
   przetasuje czesc nazw.
14. **Mgla ScoutingFog:** ogien pod mgla moze byc niewidoczny - niesprawdzone.
15. **Napisy (W4):** blad XML Gauntlet przy ladowaniu, nakladanie na tabliczki gry - ryzyko srednie, dlatego osobna paczka po W3.
16. **Aktualizacja ROT:** ponowny przebieg a-teren (112 s) + generator (26 s); do tego czasu wioski wylaczone przez CRC (gra dziala).
17. **Watki:** Strike w ticku kampanii, odczyty widoku i menu w watku glownym - blokada jak `_scars`.
18. **Zaleznosc od nieistniejacego kodu:** paczki S (spichlerz) i R (rabunek osada po osadzie) sa tylko projektami; W3 dziala bez nich
   (rabunek gry + 113), wpiecia S+ i R dochodza pozniej.

---

## 14. Co sprawdzone w kodzie gry (krytyk, zgodne z B i D)

- Najechanie i klik: MapScreen.cs:1588-1597 (petla komponentow wola OnVisualIntersected takze bez trafienia fizyki), :1608
  (IsMobileEntity), :1617-1622 (klik w teren), :371-390 (OnHover raz na zmiane); SandBoxViewVisualManager.cs:83-88 (sortowanie wg
  Priority); SettlementVisualManager.cs:91-118 (osady maja pierwszenstwo).
- Ogien i poziomy: SettlementVisual.cs:405-438 (czastki, flagi), :1189-1238 (maska, poziomy z hearth), :1240-1268 (scisla rownosc maski).
- Kopia i cykl zycia: GameEntity.cs:369-372 (CopyFrom), :272 (SetFactorColor); MapScreen.cs:731-735 (OnFinalize komponentow);
  SandBoxViewSubModule.cs:140, :147 (nowy menedzer przy starcie kampanii i wczytaniu).
- Ksiega: RaidEventComponent.cs:113-117 (napastnik), Settlement.cs:422-432 (IsRaided), :446-456 (IsUnderRaid),
  CampaignBehaviorDataStore.cs:65-77 (dane zachowan od nowa przy zapisie), Devastation.cs (_scars :66, :243; MaxShare :129; Room
  :184; Strike :221; ReturnRate :473; Abandon :540).

---

## 15. Pliki i obrazki

Obrazki dla Jeffa (kopie z v2, `docs/obrazki/wioski-na-mapie/`; legenda na obrazku po polsku: fiolet = wezel, bialy = przy drodze /
rzece / brzegu, czarny = ubocze; droga ciagla = widoczna w grze, przerywana = niewidoczna):
- `01-reach-przed-i-po.png` - Reach kolo Highgarden: po lewej v1 (dywan 204 wiosek), po prawej v2 (49 wiosek przy Manderze, drogach,
  mostach, skrzyzowaniach).
- `02-dorzecze-trident.png` - Dorzecze (Riverrun - Fairmarket - Harrenhal): sznury wzdluz drog i Red Fork, skrzyzowania, mosty,
  puste lasy.
- `03-okreg-whitebrook-zblizenie.png` - jeden okreg Polnocy w duzej skali z opisem, dlaczego kazda wioska stoi tam, gdzie stoi
  (most Hag's Bridge, skrzyzowanie Crosswood, wioski przy drodze, nad rzeka, na uboczu).
- `04-dorne-greenblood.png` - Dorne: wioski tylko nad Greenblood i przy brzegu, pustynia pusta.

Wyniki pracy (scratchpad, tylko odczyt gry):
- `W\a-teren\A-TEREN.md` - czytniki terrain.bin / navmesh.bin / flora.bin / scene.xscene, drogi, rzeki, mosty, brody, skala, okregi.
- `W\b-wyglad\szkic\WioskiNaMapie.cs` (+ `wyniki\*.txt`) - szkic widoku (do przepisania wedlug 5.8).
- `W\c-nazwy\name_gen.py`, `tablice.py`, `cs\VillageTexts.xml` - generator nazw i teksty.
- `W\d-ksiega\wzor.py`, `sym.py`, `sym-wynik.txt` - ksiega wiosek i model okregu.
- `W\p-prototyp\P-PROTOTYP.md` (v1), `W\k-krytyk\` (krytyka), `W\p-prototyp\v2\P-PROTOTYP-V2.md` + `generator.py`, `nazwy_v2.py`,
  `wyniki\arm_map_villages.tsv`, `wyniki\okregi.tsv`, `wyniki\raport.txt`, `wyniki\porownanie\v1-v2-*.png`.
- Powiazane w repo: `docs/PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md`, `docs/PROJEKT-GLOD-2026-10-07.md`,
  `docs/WIECEJ-WSI-WYKONALNOSC-2026-10-07.md`, `docs/paczki/108..113-*.md`, `docs/STAN-PRAC.md`.
