# PROJEKT C: LUD POZYCZA I PLACI PODATKI (pomoc wojenna z oszczednosci + pozyczka miejska) - 13.10.2026

Jeden z trzech niezaleznych projektow odpowiedzi na pytanie Jeffa (13.10): "co zrobic z kasa, ktora lezy; w prawdziwej ekonomii pieniadz
cyrkuluje i ma zamkniety obieg - ludzie moga oszczedzac, ale tez wydaja, inwestuja itp. Jak to zrobic w ramach silnika gry, aby mialo to sens?"
PRACA PROJEKTOWA - zadnej zmiany w kodzie. Kierunek C: lud pozycza panom i placi podatek nadzwyczajny; pieniadz wraca do ludu zoldem
wydanym w miescie, odsetkami i splata.

**Zrodla.** ROZP = `docs\kiesa-ludu\ROZPOZNANIE-OBIEG-KIESY-2026-10-13.md`. LOG = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log`
(bieg roczny 364 doby, WGRANIE 36; `:NNN` = numer linii). CSV = `...\Logs\2026-10-13_02-15-21\` (kiesa-ludu.csv, kasy-miast.csv, budzet-rodow.csv).
SRC = kod WGRANIA 36 (`...\scratchpad\zb\int\Armoury\src`). G = dekompilacja gry (`dec32\cs_full`), BK = dekompilacja BannerKings (`e4tools\bkfull`).
TRIAZ = `docs\test-zbiorczy\TRIAZ-3-bieg-roczny-2026-10-13.md` (O1-7). Skrypty tego projektu: `...\scratchpad\kiesa\c1.py` (lokata i potrzeba
wedlug krolestw), `c2.py` (zdolnosc pozyczki, rody w potrzebie), `c3.py` (podzamcza, sciezka lokaty w czasie), `c4.py` (miasta z lokata i bez,
obciazenie miast, kiesy w dobach 1 / 182 / 364). Kwoty w zl na dobe calego
swiata, 1 zl = 1 pens, rok = 364 doby. "Lokata" = kiesa ponad zapas dni (wyrobnicy 10, rzemieslnicy 21, rodziny kupieckie 30 dni potrzeby).
**Uwaga:** liczby rownowagi sa z rachunku na danych biegu, nie z symulacji obiegu ani z biegu z ta zmiana - tak samo jak poprawka B w TRIAZ.

---

## 0. Dziesiec zdan prostym jezykiem

1. W zamknietym swiecie ktos moze odkladac tylko wtedy, gdy ktos inny wydaje wiecej, niz zarabia; lud odlozyl w rok 30 mln, bo korony wydaly
   stare skarby (-31.6 mln) - i tego zlota panom teraz brakuje na zold.
2. Lud nie wyda tego na targu, bo polki sa puste; w tym projekcie polek nie ruszamy - pieniadz ma wrocic do panow tak, jak wracal w sredniowieczu:
   podatkiem wojennym od majatku i pozyczka.
3. **Pomoc wojenna:** gdy krolestwo jest w wojnie, mieszczanie oddaja koronie codziennie 1 czesc na 100 tego, co maja ponad zapas na czarna godzine
   (rzemieslnicy 2, rodziny kupieckie 3); pieniedzy na chleb nikt nie rusza, biedne miasto nie placi nic.
4. Korona jeszcze tego samego dnia oddaje to rodom: zwrotem zoldu i rentami - tymi samymi drogami, ktore juz dzialaja.
5. To jest ta sama danina wojenna, ktora od 04.10 placa kupcy (1% dziennie) i wsie (2.5% dziennie); lud miast zostal poza nia przez przypadek
   budowy etapu 5 (jego kiesa lezy poza kasa osady).
6. **Pozyczka miejska:** pan, ktoremu w wojnie brakuje na zold, pozycza najpierw od rady wlasnego miasta (12% rocznie, najwyzej 120 dni renty,
   ktora to miasto mu placi), a dopiero potem w Zelaznym Banku (30-40%); splaca jak kredyt wojenny - z lupow i po pokoju.
7. **Granica:** pomoc wojenna kosztuje pana miasta do 10 punktow lojalnosci; miasto na skraju buntu (lojalnosc 30 i mniej) nie placi wcale.
8. Uczciwie: sama pozyczka obiegu NIE zamyka - odsetki plyna od panow do ludu, a panom brakuje dochodu, nie gotowki (zdolnosc pozyczki to 4.7 mln
   wobec 23.8 mln lokaty); obieg zamyka podatek, pozyczka jest pomostem na pol roku i sposobem, w jaki lud "inwestuje".
9. Skutek: kiesy ludu przestaja rosnac i schodza z 36.5 do ok. 18-22 mln (44-53 dni zakupow zamiast 87), do koron wraca 55-80 tys. zl dziennie,
   do rodow 50-73 tys.; trwale daje to ok. 1.5-3.3 tys. ludzi wojska (gorna granica 7 tys.), przejsciowo jeszcze 3-5 tys. z pozyczek.
10. To nie odbuduje 93 tys. wojska: dwie trzecie zwroconego zlota trafi do rodow, ktore i tak maja luz w budzecie, a drugi zbiornik
    (kompanie najemne, 49 tys. dziennie) zostaje otwarty - to osobne sprawy (TRIAZ O1-2, O1-4).

---

## 1. Rachunek, ktory rzadzi tym kierunkiem

### 1.1 Co musi byc spelnione, zeby kiesy przestaly rosnac

Bilans ludu na dobe: przyrost kies = wplywy - towar - podatki. Koniec roku (d301-364): 524 782 - 455 747 (towar miast 404 707 + zamkow 51 040)
- 30 715 (pan 14 313, korona 13 447, material gmin ok. 3 000) = **+38 320** (ROZP 1.1). Srednia roczna +89 940; w tym miasta +83 035 (ROZP 3.1).
Towar jest ograniczony polka (ROZP 1.3 pkt 1-3), wiec jedyna pozycja, ktora ten projekt moze zmienic, to "podatki i pozyczki".

Niech T = podatek ludu dla panow i korony, L = nowe pozyczki ludu, P = splaty kapitalu, I = odsetki dla ludu, F = nadwyzka ludu (to, co dzis odklada).
Kiesy stoja, gdy **T + L - P - I = F**.
- Przy stalym dlugu (L = P) zostaje **T = F + I**: pozyczka nic nie zamyka, a odsetki powiekszaja to, co trzeba zebrac podatkiem.
- Pozyczka zamyka obieg tylko wtedy, gdy dlug rosnie bez konca (L > P + I). Przy 12% rocznie i F = 29 mln rocznie dlug po 10 latach = 29 / 0.12 x (e^1.2 - 1)
  = 561 mln - 2.7 raza wiecej niz cale zloto swiata (206 mln, LOG :128970). Tak skonczyly Bardi i Peruzzi (ROZP 5 pkt 5).

Wniosek: **zamkniecie obiegu = podatek (regula R1). Pozyczka (R2) przesuwa zapas raz i w czasie.** Kazdy, kto obiecuje, ze sama pozyczka wystarczy, myli sie o znak odsetek.

### 1.2 Ile panowie w ogole moga pozyczyc

| Liczba | Wartosc | Zrodlo |
|---|---:|---|
| Lokata ludu miast (kiesa W ponad 10 dni) | 23 783 334 | LOG :129003 |
| Miasta z lokata / bez | 55 / 42 z 97 | c1.py (kiesa-ludu.csv, migawka d364) |
| Rody AI ponad pulapem zoldu (srednia 28 dob): brak zoldu ponad pulap | 34 038 zl/d | c2.py (budzet-rodow.csv); LOG :128906: 110 rodow ponad pulapem partii |
| - rody z miastem w potrzebie (G < R albo zold > pulap): 63; brak zoldu / brak do rezerwy R | 20 498 zl/d / 1 884 716 | c2.py |
| - rody z samym zamkiem w potrzebie: 61; brak zoldu / brak do R | 10 448 zl/d / 1 112 631 | c2.py |
| Lokata w miastach rodow w potrzebie | 9 152 907 (38%) | c2.py |
| Renta, ktora te miasta placa swoim panom (zawor, srednia 28 dob) | 62 543 zl/d (wszystkie miasta: 138 609) | c2.py (kasy-miast.csv) |
| Zdolnosc pozyczki pod zastaw 60 / 90 / 120 / 180 dni renty (nie wiecej niz lokata miasta) | 1.91 / 2.77 / 3.60 / 4.94 mln | c2.py |
| Podzamcza: kiesa ponad 10 dni potrzeby / do pozyczenia przy 120 dniach renty zamku (232 zl/d srednio = 30 171 / 130) | 2 713 086 w 60 zamkach / ok. 1.09 mln | c3.py; ROZP 3.2 |
| Dzisiejszy kredyt wojenny Banku: miejsce w pulapie / wyplacone / rody na limicie | 31 131 zl/d u 33 / 8 816 zl/d u 24 / 27 | LOG :128908 |
| Dlug rodow w Banku / dlug koron / kapital Banku | 687 936 / 1 981 075 / 2 832 732 | LOG :128908, :128863 |

Panom brakuje **dochodu**, nie gotowki: caly rozpoznany brak to 34 tys. zl dziennie i ok. 3 mln do rezerw, a lokata to 23.8 mln. Pod zastaw 120 dni renty
miasta i podzamcza moga pozyczyc ok. 4.7 mln (20% lokaty); to starcza na pokrycie calego braku zoldu przez ok. 150 dob (4.7 mln / 30.9 tys.), potem dlug stoi na limicie.

### 1.3 Ile wynosi nadwyzka F w rownowadze

- Stan z konca roku: +38.3 tys./d netto wszystkich miast i podzamczy; same miasta z lokata +46.5 tys./d (55 miast: dodatnie 75.9, ujemne -29.5; c4.py - srednia
  z 8 ostatnich migawek tygodniowych, liczba chwiejna), 42 miasta bez lokaty -10.0 tys./d (biednieja dalej - ten projekt im nie pomaga, rozdz. 5).
- Echo: zwrocone zloto panowie wydaja, a czesc tego wraca do ludu i znow nie ma czego kupic. Z kazdej zlotowki zoldu partii lud dostaje 25.5%
  ((uslugi 67 553 + 21.7% x 256 437 u kupcow) / 483 407; ROZP 1.1, 3.2; 21.7% = (158 870 + 33 417 + 83 338) / 1 272 205), z dworu 37.4% (20% + 80% x 21.7%).
  Dla calego zwroconego strumienia echo = 0.19-0.28 (rachunek w 3.2).
- **Rownowaga: F = 38.3..46.5 / (1 - 0.19..0.28) = 47-65 tys. zl/d (srodek 55 tys.)** przy stanie z konca roku; srednia roczna (83 tys./d miasta) to gorna granica -
  taka nadwyzka byla, dopoki korony wydawaly stare skarby (71 tys./d srednio, 38 tys./d pod koniec; ROZP 1.2).
- W zadaniu jest "80 tys. zl/d". Ponizej licze **oba konce: 80 tys. (jak w zadaniu) i 55 tys. (stan z konca roku z echem)**.

---

## 2. Reguly

### R1. POMOC WOJENNA - lud oddaje koronie czesc oszczednosci (zamyka obieg)

**Prostym jezykiem.** Krolestwo w wojnie potrzebuje pieniedzy. Poborca przychodzi do miasta i do podzamcza i bierze dla korony czesc tego, co ludzie
maja w skrzyniach PONAD zapas na czarna godzine. Zapasu na chleb nie rusza. Korona tego samego dnia oddaje to swoim rodom za sluzbe.

| | |
|---|---|
| Kto placi | kazda klasa ludu miasta osobno (wyrobnicy W, rzemieslnicy R, rodziny kupieckie P) i podzamcze zamku - ze swojej kiesy |
| Od czego | od kiesy ponad zapas dni: W 10, R 21, P 30, podzamcze 10 dni potrzeby - ta sama liczba, od ktorej dzis liczy sie "oferta nadwyzki" (SRC TownFolk.Live.cs:809-829, TownFolk.cs:54, Settings.cs:1613) |
| Ile | dziennie W 1% / R 2% / P 3% / podzamcze 1% tej nadwyzki x (s / 3%), gdzie s = stawka podatku wojennego ludu 184 tego krolestwa (0-3%, z polityka War Tax do 6% - wtedy pomoc x2). s rosnie o 0.5 pkt na tydzien, gdy korona pozycza albo nie starcza jej na zwrot zoldu, w pokoju spada do zera w 4 tygodnie (SRC KingdomTreasury.WarTax.cs:240-250) |
| Komu | skarbiec krolestwa wlasciciela osady (`Kingdom.KingdomBudgetWallet`) jako wplyw dnia 165 - dalej zwrot zoldu i renty 180 tego samego dnia |
| Kiedy NIE | pokoj (s = 0); rod bez krolestwa; osada oblezona; kiesa ponizej zapasu; lojalnosc 30 i mniej (R3); kiesa ludu nieczynna |
| Kolejnosc w dobie | w bloku kiesy ludu, zaraz po podatkach pana i podatku 184, PRZED oferta nadwyzki i placami z nadwyzki R/P - czyli przed `KingdomTreasury.Levies` i `CrownIncome.Begin` (SRC ArmouryBehavior.cs:1600, :1638, :1641) |

**Skad stawka 1%.** (a) To liczba juz uzywana dla miast: danina wojenna z nadwyzki kas kupcow 1% dziennie (`LaySubsidyTownShare`, Settings.cs:948; wsie 2.5%, :949) -
jedna regula na zjawisko "danina wojenna z zapasu ponad potrzebe", trzeci platnik. (b) Zapas w rownowadze = F / stawka: przy 1% lud trzyma ponad zapas
rowno 100 dni swojej nadwyzki. (c) Rachunek wariantow (c3.py, start 23.78 mln):

| Stawka W dziennie (pelna; skuteczna przy sredniej s = 2.54% z 3%, LOG :128861) | Lokata w rownowadze przy F = 55 / 80 tys. (pelna - skuteczna) | Pomoc w dobie 1, stary zapis (skuteczna) | Polowa drogi po |
|---|---|---:|---:|
| 0.5% (0.42%) | 11.0-13.0 / 16.0-18.9 mln | 101 tys. | 139-164 dobach |
| **1% (0.85%)** | **5.5-6.5 / 8.0-9.4 mln** | **201 tys.** | **69-82 dobach** |
| 2% (1.69%) | 2.8-3.2 / 4.0-4.7 mln | 403 tys. | 35-41 dobach |

0.5% zostawia lokate prawie tak duza jak dzis (16-19 z 23.8 mln) - kiesy "przestaja rosnac", ale zloto dalej lezy. 2% to wstrzas (wplywy skarbcow x2 w dobie 1)
i zapas rowny poltora miesiaca nadwyzki. 1% jest liczba posrodku i ta sama co u kupcow.

**Zapas oszczednosci, ktory zostaje (czy ma sens).** Kiesy miast = 36.50 - 23.78 + lokata w rownowadze = 12.72 + (5.5..9.4) = **18.2-22.1 mln** = 44-53 dni
prawdziwych zakupow (417 172 zl/d, LOG :129003) zamiast 87 dni dzis. Na glowe wyrobnika: (31.63 - 23.78 + 5.5..9.4) mln / 1 337 515 = 10.0-12.9 zl = 7-9 dniowek
po 1.5 d. Historycznie podatek od ruchomosci nie siegal majatku ponizej 6 szylingow = 72 d na gospodarstwo w miescie (ROZP 5 pkt 2) - nasz zapas jest ponizej tego progu,
czyli "wyrobnik ma na dwa tygodnie, nie na kwartal".

**Dlaczego to nie jest "sztuczne prawo".** Pietnastka i dziesiata byly wlasnie podatkiem od ruchomosci (nie od zarobku), uchwalanym na wojne, z wolna kwota
na rzeczy niezbedne (ROZP 5 pkt 3). Jeff przyjal te regule 04.10 dla kupcow i wsi i 09.10 C ("podatek wojenny korony ze skutkami dla gospodarki").
Dzisiejszy podatek 184 bierze od ZAROBKU do 3% i jest nasycony: 20 z 26 krolestw w wojnie stoi na pulapie, a daje 12 925 zl/d (LOG :128861) - podstawa jest zla, nie stawka.

**Gdzie w kodzie (bez nowego systemu).**
- `KingdomTreasury.WarTax.cs`: nowa metoda `FolkWarAid(Settlement st, int cls, long excess)` obok `FolkWarTax` (:147) - te same bramki (`WtOn` :86, krolestwo, wojna `WtAtWar` :111,
  `WtCap` :91, oblezenie `BesiegedPay.Holds` z nowym rodzajem `KFolkAid` obok `KFolkWarTax`, BesiegedPay.cs:55), ten sam odbiorca i to samo okno wplywow dnia
  (`_wtWindow` :58, :175 -> `CrownIncome.Begin`). Liczniki dnia na klase i dopisek do linii "Korona: podatek wojenny ludu (184)" (:289-308).
- `TownFolk.Live.cs`: w `TownLive` krok 5 (:809-829) i `CastleLiveDay` krok 5 (:890-893) - po wyliczeniu nadwyzki `ov` klasy wolanie `PayAid(f, c, cls, ov)` (kopia `PayCrown` :286-294:
  zdjecie z kiesy, `_ctlOut`, `LDayOut`), potem oferta i place z reszty. Pozycje w linii "Kiesa ludu" (wydatki, lokata przed / po).
- `MoneyLedger.Obieg.cs`: licznik "pomoc wojenna ludu" w linii "Obieg" (jak `LastFolkWarTax`, WarTax.cs:66). Nowych posiadaczy zlota nie ma.
- `Settings.cs` + `python tools/gen_mcm.py`: `FolkWarAid` (wylacznik), `FolkWarAidShare` (0.01).

### R2. POZYCZKA MIEJSKA - pan w potrzebie pozycza najpierw od wlasnego miasta (pomost i "inwestycja" ludu)

**Prostym jezykiem.** Pan w wojnie, ktoremu brakuje na zold, idzie do rady swojego miasta (albo do ludzi podzamcza). Rada pozycza mu z oszczednosci mieszczan,
bo ma zastaw: rente, ktora miasto i tak mu placi. Bierze 12% rocznie zamiast 30-40% Banku. Pan placi odsetki, a dlug oddaje z lupow i po pokoju.
Nie placi - wierzyciele zajmuja mu dochod wsi, tak jak dzis robi to Bank.

To jest **ten sam kredyt wojenny co dzis (paczka 168)** - jedna regula na zjawisko "pan nie ma na zold w wojnie" - z drugim wierzycielem, ktory stoi przed Bankiem.

| | |
|---|---|
| Kto pozycza komu | lud miasta / podzamcza (klasy proporcjonalnie do swojej nadwyzki) -> glowa rodu, ktory jest panem tej osady. Rod bez wlasnej osady z lokata - jak dzis, tylko Bank |
| Kiedy (AI) | warunki kredytu wojennego bez zmian: wojna, G < R, bez zaleglosci i zajecia, D pokrywa dwor i powinnosci, zold naliczony ponad pulap (SRC DebtLadder.cs:225-244, ClanBudget.cs:380-400) |
| Ile dziennie | brak zoldu ponad pulap, najwyzej 0.40 D dziennie razem z Bankiem (`WarCreditMaxShareD`, Settings.cs:1124); najpierw osada rodu z najwieksza lokata, reszta z Banku |
| Limit | dlug wobec jednej osady najwyzej `TownLoanRentDays` = **120 dni** sredniej renty, ktora ta osada placi temu panu (28 dob: czesc pana z zaworu + podatek od zarobku ludu + farma gminy), i nie wiecej, niz lud ma ponad zapas PO pomocy wojennej |
| Cena | **12% rocznie**, bez oplaty za udzielenie; odsetki doliczane co dobe (rok z kalendarza gry, jak `IronBank.DaysPerYearNow`) |
| Splata | jak kredyt wojenny: w wojnie odsetki z dochodu stalego D i 50% jednorazowych wplywow (lup, okupy) po Banku; po pokoju raty z D. Wszystkie raty rodu razem najwyzej 15% D (`AllInstalmentsMaxShare`, Settings.cs:1130) |
| Niewyplacalnosc | drabina 168 bez zmian: zaleglosc (placi, ile ma ponad 3 dni zoldu) -> trzecia z rzedu: zajecie dochodu wsi i kiesy ponad 38 000 -> wyprzedaz; lenno zostaje (DebtLadder.cs:543-570, :579-611). Dluznik: dwor -50%, budowy 0 (ClanBudget.cs:328) |
| Zloto wraca do | kies ludu tej osady w proporcji, w jakiej klasy pozyczyly; odsetki sa zarobkiem ludu (pan i korona biora od nich swoje 1/2/3% i 184), kapital nie |

**Skad 120 dni i 12%.** Zdolnosc pozyczki rodow w potrzebie: 60 dni 1.9 mln, 90 dni 2.8 mln, 120 dni 3.6 mln, 180 dni 4.9 mln (c2.py) wobec ich braku do rezerwy 1.88 mln
i braku zoldu 20.5 tys./d. 120 dni pokrywa dopelnienie rezerwy i ok. 84 doby braku zoldu (1.88 + 84 x 0.0205 = 3.6 mln). Koszt dlugu stojacego na limicie:
120 x 12% / 364 = 4.0% renty z tego miasta - do udzwigniecia na stale; przy 180 dniach 5.9%, a dlug splacalby sie z 15% D ponad rok. 12%: renty miejskie 5-12%,
krol u kupcow 15-40% (ROZP 5 pkt 5-6); Bank bierze od rodu z lennem 30% + 10 pp przy biegnacym dlugu (Settings.cs:971, :973). Wierzyciel, ktory siedzi na zastawie,
bierze mniej niz obcy bank; ponizej Banku, zeby pan mial powod isc najpierw do swoich.

**Liczby swiata.** Pozyczki brutto najwyzej 30.9 tys./d (20.5 panowie miast + 10.4 panowie zamkow) przez ok. 150 dob; dlug w rownowadze na limicie ok. 4.7 mln (miasta 3.6 + podzamcza 1.1);
odsetki 4.7 mln x 12% / 364 = **1 549 zl/d od panow do ludu** - na stale. Kredyt wojenny Banku (8 816 zl/d) schodzi prawie do zera u rodow z bogatymi miastami;
kapital Banku wraca w strone 5 mln i rosnie pula dla koron (dzis 14 143 zl/d, przycieta o 11 021 - LOG :128863).

**Dlaczego nie pozyczka dla korony.** Korona dostaje pomoc wojenna (R1). Z 8 koron, ktore pozyczaly w d364 (potrzeba razem 25 164 zl/d), cztery maja bogate miasta
i ich pomoc pokrywa potrzebe (Riverlands: potrzeba 3.9 tys., nadwyzka ludu 9.0 tys./d; Targaryen 3.0 / 5.2; Qarth 0.7 / 1.4; Dragonstone 5.8 / 2.5 - czesciowo),
a cztery (Pentos, Qohor, Braavos, Lys - potrzeba ok. 11.9 tys./d) maja miasta bez lokaty, wiec ich lud i tak nic by nie pozyczyl (c2.py). Tym zostaje Bank,
ktorego pula (14.1 tys./d) bez czterech pierwszych prawie starcza. Osobna ksiega dlugu korony wobec miast bylaby systemem bez uzytkownika.

**Gdzie w kodzie.**
- `DebtLadder.cs`: nowy rodzaj roszczenia `KTown = 6` w istniejacej ksiedze (`Claim`, :49): wierzyciel = id osady, pole `Rate` (dla pozostalych rodzajow 0), odsetki w `DayClan` (:471),
  w wojnie rata = odsetki + udzial z jednorazowych po Banku (:494-506), kolejnosc w `Ordered` (:573) razem z okupami wedlug wieku, wyplata w `Deliver` (:797) - nowa galaz `KTown` ->
  `TownFolk.Repay(st, kapital, odsetki)`; odbiorca zapasowy (kiesa nieczynna, osada nie do znalezienia) = kasa tej osady, dalej `SpareTown` (:869). Limit: nowa `TownCreditRoom`
  (warunki wspolne z `WarCreditRoom` :225). Wymarly rod - `Inherit` (:901) bez zmian. Zapis: siodme pole roszczenia i rodzaj 6 w `Export` / `Import` (:1147, :1193, warunek `Kind <= KGreat` :1222).
- `ClanBudget.cs:380-400`: przed `DebtLadder.LendWar` - pozyczka z osad rodu (miejsce w pulapie = miejsce miasta + miejsce Banku, wyplata najpierw z miasta).
- `TownFolk.Live.cs`: `LendOut(st, kwota)` (z kies klas ponad zapas -> `ChangeHeroGold` glowy, `_ctlOut`, `CirculationWindows.NoteHeroGold` - wzor `PayLord` :298-319 i `IronBank.LendKw`, IronBank.cs:135-157)
  i `Repay` (kapital: `AddPurse` bez zarobku okna; odsetki: `Credit` jak zarobek; `_ctlIn`). Srednia renty pana z osady (28 dob) - z liczb, ktore juz zapisuje zawor
  (`TownPurse.Valve`, TownPurse.cs:397; `PopulationLaw.RentToday`, TownFolk.Live.cs:315).
- Gracz: pozycja w menu miasta i zamku (rozdz. 6-7), wzor `IronBank.BorrowDialog` (IronBank.cs:549-581).
- `Settings.cs` + gen_mcm: `TownLoans`, `TownLoanRentDays` (120), `TownLoanRate` (12).

### R3. GRANICA - miasto, z ktorego biora, burzy sie

**Prostym jezykiem.** Ludzie nie lubia poborcow. Osada, ktora placi pomoc wojenna, traci do swojego pana lojalnosc; gdy jest juz na skraju buntu, poborcy z niej wychodza.

- **Kara:** w dniu, w ktorym osada zaplacila pomoc, zmiana lojalnosci -1.0 x (s / 3%) - przy pelnej stawce -1.0 na dobe, z polityka War Tax (s do 6%) -2.0.
  Dryf gry ciagnie lojalnosc do 50 z sila 0.1 na punkt (G DefaultSettlementLoyaltyModel.cs:294; BK BKLoyaltyModel.cs:318), wiec trwale to **-10 punktow** (-20 z War Tax).
  Dla skali: dekret BK "High tax" kosztuje (udzial rzemieslnikow + 0.8 x udzial chlopow) x 4 na dobe (BKLoyaltyModel.cs:102-106), czyli rzedu -1.5 do -3 [S]; pomoc to ok. polowa tego.
- **Granica:** osada z lojalnoscia <= 30 nie placi pomocy (i nie ma kary) - gra wchodzi w stan buntu przy 25, a bunt wybucha przy 15, gdy milicja ma 1.4 x sile zalogi
  (G DefaultSettlementLoyaltyModel.cs:64-66, RebellionsCampaignBehavior.cs:38, :189). **Zamki tez moga sie zbuntowac** - BK liczy to samo dla zamkow (BK BKSettlementBehavior.cs:674-707),
  wiec kara i granica obejmuja podzamcza.
- Kara spada na PANA osady, choc bierze korona - to celowe i historyczne (bunty miejskie przeciw krolewskim podatkom bily w miejscowego pana). Hamulec panski juz istnieje w BK:
  namiestnik AI przy lojalnosci ponizej 30 + 8 x hojnosc przestawia dekret na "Low" (BKSettlementBehavior.cs:400-414), co u nas tnie czesc pana z zaworu do 70% (`RentTaxLow`, Settings.cs:737;
  `PopulationLaw.TaxDecree`, PopulationLaw.cs:1149-1173) i daje lojalnosc z powrotem.
- Pozyczka (R2) jest dobrowolna - bez kary.

**Gdzie w kodzie.** Latka Harmony (postfiks) na `CalculateLoyaltyChange(Town, bool)` CZYNNEGO modelu lojalnosci (`Campaign.Current.Models.SettlementLoyaltyModel.GetType()`; u Jeffa BK
`BKLoyaltyModel`, rejestrowany w BK Main.cs:197): `__result.Add(-x, "War aid from the people's savings")` - wtedy gracz widzi pozycje w dymku lojalnosci. **Zakladac dopiero w kampanii
(OnSessionLaunched, wzor `CrownIncome.EnsureHooks`, CrownIncome.cs:534):** `BKLoyaltyModel` ma pole statyczne `StarvingText = GameTexts.FindText("str_starving")` (BKLoyaltyModel.cs:32) -
ta sama pulapka co `DefaultClanFinanceModel` (CLAUDE.md rozdz. 7): latka zalozona w SubModule uruchamia konstruktor statyczny przed gra i klasa ginie na caly proces.
Znacznik "osada placila wczoraj" - slownik sesji ustawiany w `FolkWarAid`, czyszczony na poczatku nastepnego bloku doby.

### R4. KARTA MIEJSKA - wykup przywileju (tylko na slowo Jeffa; opisana, niezalecana teraz)

Miasto placi swojemu panu z gory za rok wolnosci od jego podatkow od zarobku (1/2/3%) i farmy gminy: cena 300 dni tych podatkow (srednia 28 dob), przez 364 doby `PayLord`
(TownFolk.Live.cs:298) dla tej osady nic nie pobiera, lojalnosc +0.5 na dobe. Pan w potrzebie (G < R) sprzedaje karte PRZED pozyczka (nie placi odsetek, oddaje przyszly dochod).
Bez ksiegi dlugu: nie ma czego nie splacic; nowy pan po zdobyciu karty nie uznaje.
Rachunek: podatki panskie ludu 9 200 + farma 3 104 + podzamcza 1 442 = 13 746 zl/d (LOG :129005); gdyby sprzedali wszyscy: 13 746 x 300 = 4.1 mln rocznie, rody w potrzebie (ok. 45% renty) -
**ok. 1.8 mln rocznie = 5 tys. zl/d**, i to tylko przesuniete w czasie. Maly pieniadz, glownie klimat i wybor gracza.
Sprawdzone i odrzucone jako nosniki przywileju: dekret podatkowy BK (namiestnik AI nadpisuje go co tydzien wedlug lojalnosci - BKSettlementBehavior.cs:400-414 - a zysk z "Low" zostaje u kupcow,
nie u ludu, ktory placi) i autonomia BK (sama dryfuje do celu ze stabilnosci, +-0.005 na dobe - BKStabilityModel.cs:70-81 - kupiona wartosc znika).

---

## 3. Przeplywy z liczbami

### 3.1 Kiesa ludu na dobe (swiat: 97 miast + 130 podzamczy)

| Pozycja | dzis (d301-364, ROZP 1.1) | rownowaga, F = 55 tys. | rownowaga, F = 80 tys. |
|---|---:|---:|---:|
| Wplywy | 524 782 | ok. 541 000 (+ echo 10-16 tys.) | ok. 566 000 |
| Towar z polek (miasta + zamki) | 455 747 | 455 747 (polka - bez zmian) | 455 747 |
| Podatki pana, farma, material gmin | 17 268 | ok. 17 500 | ok. 17 700 |
| Podatek wojenny od zarobku (184) | 13 447 | ok. 13 800 | ok. 14 400 |
| **Pomoc wojenna z oszczednosci (R1)** | 0 | **55 000** | **80 000** |
| Odsetki od pozyczek miejskich (wplyw, R2) | 0 | +1 549 | +1 549 |
| **Przyrost kies** | **+38 320** | **ok. 0** | **ok. 0** |
| Lokata (stan) | 23.78 mln, rosnie | 5.5-6.5 mln, stoi | 8.0-9.4 mln, stoi |
| Roszczenia ludu wobec panow (stan, nie zloto) | 0 | do 4.7 mln | do 4.7 mln |

Wiersz 80 tys. wymaga wplywow ludu wyzszych o 41 tys. niz dzis - tak bylo srednio w roku (539 133), gdy korony wydawaly skarby; przy samym echu z dzisiejszego poziomu wychodzi 55 tys.

### 3.2 Droga zwroconego zlota (na kazde 80 tys.; dla 55 tys. mnozyc przez 0.69)

| Krok | Kwota | Rachunek / zrodlo |
|---|---:|---|
| Lud -> skarbce koron | 80 000 | wedlug nadwyzki ludu krolestw (c3.py): The North 27%, Baratheon KL 18%, Riverlands 13%, Targaryen 7.5%, Aegon 6.7%, Dothraki 5.7%, Dorne 5.1%, Vale 3.9%, Dragonstone 3.6%, Sarnor 3.4%, Qarth 2.1%; Wolne Miasta ok. 0. W pierwszym roku na starym zapisie raczej wedlug lokaty: North 17%, KL 15%, Vale 10%, Reach 10%, Riverlands 9%, Dothraki 8% |
| - z tego dopelnienie zwrotu zoldu do 100% w krolestwach z zamoznymi miastami | 5 841 | nalezny minus wyplacony: Riverlands 1 695, Targaryen 1 317, Qarth 290, Dragonstone 2 539 (LOG :128862); caly swiat: 227 980 - 216 930 = 11 050 |
| - z tego zamiast nowego kredytu Banku (dlug tych koron przestaje rosnac) | 7 470 | 2 167 + 1 685 + 369 + 3 249 (LOG :128863) |
| - z tego renty korony 180 | 66 689 | reszta wplywow dnia idzie rentami wedlug lenn (CrownRents.cs:15-19); dzis 159 280 (LOG :128859) -> ok. 226 tys. (+42%) |
| **Rody dostaja (D "korona")** | **72 530** | 5 841 + 66 689; D razem 643 605 (LOG :128906) -> ok. 716 tys. (+11%) |
| - pulap zoldu rosnie o | 43 518 | 0.60 D (`WarWageShare`, Settings.cs:1030) |
| - dwor / sprzet / budowy | 14 506 / 12 330 / 7 253 | 0.20 / 0.17 / 0.10 D (Settings.cs:1032, :1034, :565); w wojnie dwor ustepuje zoldowi |
| **Zold naprawde wydany** | **13 500-20 200** | 2.2-3.3 tys. ludzi x 6.22 zl; TRIAZ O1-7 pkt 5: 0.3-0.45 tys. ludzi na 10 tys. zl/d, bo na pulapie jest 117 z 332 rodow (33% dochodu) |
| Zostaje w kiesach rodow z luzem | 18 000-25 000 | 72.5 - zold - dwor - sprzet - budowy; czesc wchlania wyzsza rezerwa R (20 x D: +1.45 mln jednorazowo) |
| Echo do ludu | 15 000-22 700 (0.19-0.28 kwoty) | zold x 25.5% + dwor x 37.4% + sprzet x ok. 15% [S] + budowy x ok. 60% [S] = (3.4..11.1) + 5.4 + 1.9 + 4.4; gorna liczba przy pelnym wykorzystaniu pulapu |

**Ile ludzi wojska.**

| | F = 55 tys. | F = 80 tys. |
|---|---:|---:|
| Trwale z pomocy, realnie (0.3-0.45 tys. na 10 tys. zl/d do rodow) | **+1.5 do +2.2 tys.** | **+2.2 do +3.3 tys.** |
| Gorna granica (caly nowy pulap zoldu wydany: 0.60 x kwota / 6.22 zl) | +4.8 tys. | +7.0 tys. |
| Pierwszy rok na starym zapisie: dodatkowo z zejscia lokaty 23.8 -> 7-10 mln (14-17 mln = 38-45 tys. zl/d ponad F, z tego 91% do rodow) | +1.0 do +1.8 tys., gasnie | jak obok |
| Przejsciowo z pozyczek (R2): brak zoldu 20.5-30.9 tys. zl/d pokryty w calosci przez ok. 150 dob | +3.3 do +5.0 tys. nie zwolnionych przez ok. 5 miesiecy | jak obok |

Dla porownania: partie rodow 93 -> 62 tys. w rok, a zold partii 382 562 zl/d (LOG :128895). C zamyka jeden z dwoch czynnych zbiornikow; nie cofa dwoch trzecich spadku,
ktore TRIAZ O1-7 przypisuje drozszemu zolnierzowi (4.84 -> 6.42 zl na czlowieka), i nie rusza kompanii najemnych (+49 tys. zl/d).

### 3.3 Przejscie (stary zapis z d364; c3.py, stawka skuteczna 0.85%)

| Doba po wlaczeniu | Lokata, F = 55 / 80 tys. | Pomoc do koron na dobe, F = 55 / 80 tys. |
|---|---|---|
| 1 | 23.8 mln | 201 tys. (+ podzamcza ok. 23 tys.) - skarbce +50% wplywow dnia (401 332, LOG :128862) |
| 30 | 19.9 / 20.5 mln | 169 / 175 tys. |
| 120 | 12.7 / 14.6 mln | 108 / 124 tys. |
| 364 | 7.3 / 10.1 mln | 62 / 86 tys. |
| rownowaga | 6.5 / 9.4 mln | 55 / 80 tys. |

Nowa kampania (na taka czeka Jeff): lokaty na starcie nie ma, s rosnie od zera po 0.5 pkt na tydzien (pelna stawka po 6 tygodniach wojny), wiec wstrzasu nie ma;
pomoc od poczatku zawraca to, co w biegu rocznym odlozylo sie w kiesach (196 tys. zl/d w pierwszym miesiacu, ROZP 1.2).

---

## 4. Dowod zamknietego obiegu

Zadne zloto nie powstaje i nie znika; zadna sztuka towaru nie jest ruszana (projekt nie dotyka polek, cen ani produkcji). Dlug to zapis, nie zloto: umorzony dlug niczego nie kasuje.

| Ruch | Platnik (zdjete) | Odbiorca (dopisane) | Kontrola |
|---|---|---|---|
| Pomoc wojenna | kiesa klasy ludu osady (`AddPurse(f, c, -t)`) | skarbiec krolestwa (`KingdomBudgetWallet += t`), wplyw dnia 165 | "Kiesa ludu: kontrola: zmiana kies = wplywy - wydatki, r = 0" (`_ctlOut`); "Korona: wplywy dnia (165)"; "Pieniadz swiata" (suma bez zmian); ulamek ponizej 1 zl czeka jako niezaplacona reszta (jak `_wtCarry`, WarTax.cs:59) |
| Skarbiec -> rody | skarbiec | glowy rodow: zwrot zoldu (KingdomTreasury.cs:219), renty 180 (CrownRents.cs:44) | istniejace linie - bez zmian w kodzie |
| Pozyczka miejska - wyplata | kiesy klas ludu osady, tylko ponad zapas | glowa rodu (`ChangeHeroGold`) + zapis roszczenia `KTown` | `_ctlOut`; "Dlugi (168)"; nie jest dochodem rodu (nie wchodzi do D - jak `LendKw`) |
| Pozyczka - odsetki i splata | glowa rodu / rodzina (`Collect`, DebtLadder.cs:879) / zajety dochod wsi | kiesy klas tej osady w proporcji udzielenia; kiesa nieczynna albo osada nie do znalezienia -> kasa osady, dalej najblizsze miasto (`SpareTown`) | `_ctlIn`; licznik "bez odbiorcy" = 0 (prog 0, jak dzis) |
| Dlug wymarlego rodu | - | przechodzi na nowych panow jego wsi (istniejace `Inherit`); bez wsi - "dlug bez platnika" w ksiedze | linia "Dlugi (168)" |
| Karta (R4) | kiesy klas ludu | glowa rodu, jako wplyw jednorazowy (poza D - nie pompuje pulapu) | `_ctlOut` |
| Zold wraca do ludu | sakiewki zolnierzy, kasy siedzib | lud 25% uslug + robocizna u kupcow (istniejace `EarnService`, robocizna handlu) | bez zmian |

Zamkniecie petli: lud -> skarbiec -> rod -> zolnierz -> miasto (uslugi i targ) -> lud. Kazde ogniwo poza pierwszym juz istnieje i jest w ksiedze "Pieniadz swiata" i "Obieg".

---

## 5. Miasta biedne, zamki, wsie, rody bez lenna, pokoj

| Przypadek | Co sie dzieje |
|---|---|
| **Miasto biedne** (42 z 97 bez lokaty; 31 z kiesa ponizej 2 dni koszyka - ROZP 2) | nie placi pomocy (nic ponad zapas), nic nie pozycza; kary lojalnosci nie ma. Jego pan zostaje przy Banku, ktory ma wiecej wolnego kapitalu. **Projekt C im nie pomaga:** te miasta biednieja dalej (-10 tys. zl/d), bo zwrocone zloto wydaje wojsko tam, gdzie stoi - w tych samych bogatych miastach |
| **Zamek** | podzamcze = "miasto" zamku: pomoc 1% dziennie nadwyzki ponad 10 dni potrzeby (60 ze 130 podzamczy ma nadwyzke, razem 2.71 mln; w rownowadze prawie nic - kiesy podzamczy spadaja od d210, ROZP 2), pozyczka dla pana zamku pod 120 dni renty zamku (ok. 1.09 mln). Kara i granica lojalnosci tak samo (zamki w BK moga sie buntowac). Zamek bez czynnej kiesy podzamcza (`CastlePurseEnabled` wylaczone) - nic |
| **Wies** | bez zmian: juz placi danine wojenna 2.5% dziennie nadwyzki (180 tys. zl/d, ROZP 3.2) i dlatego jej kiesy stoja od d90 |
| **Rod bez lenna / z samymi wsiami** | bez zmian: kredyt wojenny tylko w Banku; pomoc dociera do niego zwrotem zoldu |
| **Krolestwo w pokoju** | s spada do zera w 4 tygodnie -> pomocy nie ma; nowych pozyczek nie ma (kredyt wojenny jest tylko w wojnie), dlug splacany ratami. **Nie wiadomo, czy lud w pokoju odklada** - bieg roczny pokoju nie zmierzyl (25 z 30 krolestw w wojnie caly rok). Jesli tak, lokata w pokoju rosnie i czeka na nastepna wojne - jak skarb, z ktorego w sredniowieczu finansowano pierwsza kampanie |
| **Rod bez krolestwa, buntownicy** | bez pomocy (nie ma korony - tak samo jak 184, WarTax.cs:152); bez pozyczki (budzet 166 ich nie obejmuje) |
| **Osada oblezona** | nie placi pomocy i nie pozycza (bramka R22 jak dla 184) |
| **Osada zmienia pana** (zdobycie, nadanie) | pomoc od nastepnej doby idzie do korony nowego pana; dlug starego pana zostaje jego dlugiem bez zastawu (splaca z D wedlug drabiny - lud tej osady dalej dostaje raty); nowy pan nie jest winien nic i ma caly swoj limit |
| **Miasto niewolnicze** | bez wyjatku: placa kiesy klas, ktore maja zloto (praca niewolnych zapisana wlascicielom R/P - TownFolk.Live.cs:141-146) |

---

## 6. Gracz

Ta sama regula co AI wszedzie, gdzie gracz ma te same liczby.

- **Jego miasta i zamki:** lud placi pomoc koronie jego krolestwa wedlug tej samej stawki s; kara lojalnosci spada na niego. Jesli gracz jest krolem, pomoc wplywa do skarbca krolestwa
  (nie do jego kiesy) i idzie do rodow - takze do jego rodu - zwrotem i rentami na tych samych warunkach (CrownRents.cs:17). Gracz bez krolestwa - jego miasta nie placa nikomu.
- **Jego dlug:** w menu wlasnego miasta i zamku pozycja "Ask the council for a war loan" - pozycza recznie (25 / 50 / 100% wolnego limitu) na 12%, pod ten sam limit 120 dni renty z tej osady.
  Bez warunku G < R (gracz nie ma budzetu 166 i sam decyduje - tak samo jak dzis w Braavos, IronBank.cs:549). Splata i skutki wedlug drabiny 168, ktora juz obejmuje gracza
  (`PlayerSameLadder` = true, Settings.cs:1138): raty z dochodu stalego w wojnie i w pokoju, trzy zaleglosci -> zajecie dochodu wsi i kiesy ponad 38 000; nigdy jego ekwipunek.
  Splata reczna: druga pozycja "Repay the council".
- **Furtka, ktora trzeba zamknac:** gracz pozycza do limitu i oddaje miasto wasalowi. Dlatego dlug jest roszczeniem w ksiedze rodu (nie znika z utrata zastawu) - to jest powod,
  dla ktorego R2 siedzi w drabinie 168, a nie w prostym "miasto potraca sobie z renty".
- **Jego inwestycje:** gracz nie staje sie wierzycielem ludu ani panow - tego w kierunku C nie ma (pozyczki gracza dla AI to osobny pomysl z PLAN rozdz. 3).
- **Karta (R4, jesli Jeff zechce):** pozycja "Sell the town a charter of liberties" - gracz bierze gotowke z gory i przez rok nie dostaje podatkow od zarobku ludu z tej osady.

---

## 7. Co widzi gracz (napisy po angielsku)

| Gdzie | Napis |
|---|---|
| Komunikat, raz na wojne, osada gracza zaczyna placic | "War aid: while the realm is at war, the people of {TOWN} give the crown one part in a hundred of their savings each day (craftsmen two, merchant families three). Their bread money is not touched. It costs you their goodwill - up to 1 loyalty a day." |
| Komunikat, poborcy wychodza | "The tax gatherers have left {TOWN}: the town is too close to revolt to pay the war aid." |
| Dymek lojalnosci osady (pozycja z latki R3) | "War aid from the people's savings" -1.0 |
| Okno "People of this land" w miescie i zamku (nowe zdanie pod zdaniem o chlebie, wzor `TownFolk.BreadLine`, TownFolk.Live.cs:1468; wylacznik `PeopleMenuShowsSavings`) | "Savings: the people hold 1,720,000 denars, 1,489,000 of it above what they keep for hard days. War aid to the crown yesterday: 12,600 denars. Lent to their lord: 124,000 denars at 12 parts in 100 a year." |
| To samo okno, osada biedna | "Savings: the people have nothing laid by beyond their bread money. They pay no war aid." |
| Menu wlasnego miasta / zamku | "Ask the council for a war loan" / "Repay the council" |
| Okno pozyczki | "The council of {TOWN} will lend you up to {X} denars at 12 parts in 100 a year, against the rents the town pays you. You pay the interest out of your steady income and the debt itself from spoils and after the peace. Miss three instalments and your creditors take the income of your villages." |
| Splata | "Your debt to the council of {TOWN} is paid." |
| Bank w Braavos (istniejacy opis dlugow, DebtLadder.cs:297) | "Other debts: {N} gold." - obejmie dlug wobec miast bez zmiany |
| Krol-gracz, raz na tydzien, gdy pomoc > 0 | "War aid from the towns of the realm brought the treasury {N} denars this week." |

Bez `GameMenu.SwitchToMenu` z wnetrza opcji (CLAUDE.md rozdz. 7) - okna przez `ShowInquiry` / `ShowMultiSelectionInquiry`, jak PeopleMenu i Bank.

---

## 8. Skutki uboczne

| Obszar | Skutek | Dlaczego |
|---|---|---|
| Ceny | brak bezposredniego | cena nie widzi kiesy ludu (G DefaultSettlementEconomyModel.cs:61-73, BK BKEconomyModel.cs:535-554 - ROZP 1.3 pkt 8). Posrednio wiecej wojska = wiecej zakupow zolnierzy na tych samych polkach |
| Glod | brak | pomoc i pozyczka biora tylko ponad zapas dni; "stac jedzenie" liczy sie z kiesy wobec potrzeby (TownFolk.Live.cs:729-738), a zapas zostaje caly. Prog testu: bez pogorszenia |
| Oferta nadwyzki ludu (15% lokaty dziennie na "lepszy towar") | spada z 3.6 do ok. 1.0-1.4 mln zl/d | mniejsza lokata; dzis i tak nie kupuje ani sztuki wiecej (ROZP 1.3 pkt 2). Gdy inny projekt da towar na polki, lud wyda go najpierw (15% dziennie wobec 1% pomocy) i pomoc sama zmaleje - C nie przeszkadza kierunkom "lud wydaje" i "lud inwestuje" |
| Zadowolenie BK | bez zmian | zalezy od towaru i od "stac" (R14), nie od zapasu ponad potrzebe |
| Lojalnosc | -10 w osadach placacych (-20 z polityka War Tax) | R3; miasta przy 30 przestaja placic |
| Dekret podatkowy BK | wiecej miast na "Low" (namiestnik AI ponizej 30 + 8 x hojnosc) | czesc pana z zaworu x 0.7 w tych miastach (posrednia strata renty panow; nie zmierzona) |
| Kasy miast (kupcy) | rosna wplywy z zoldu, dworu i sprzetu -> wiekszy zawor panu i koronie | 78% zwroconego zlota wydane w miastach trafia do kas kupcow |
| Renty korony 180 | +42% (159 -> ok. 226 tys.) | reszta wplywow dnia |
| Stawka s (184) | zostaje na pulapie w wojnie | skarbiec nie rosnie (wszystko wyplacane tego samego dnia), wiec warunek spadku "skarbiec > 2 x zapas wojny" prawie nie zachodzi (WarTax.cs:245). Pomoc jest wiec w praktyce stala w wojnie, nie "wedlug potrzeby" - mowie to wprost |
| Zelazny Bank | mniej pozyczek dla rodow i 4 koron; kapital wraca do 5 mln, zysk ponad 5 mln do kasy Braavos (istniejace 1/180 dziennie) | R2 i R1 |
| Rody-dluznicy | wiecej rodow z etykieta "dluznik" (dwor -50%, budowy 0) | dlug wobec miasta liczy sie jak kazdy inny (ClanBudget.cs:328) - ok. 60-120 rodow zamiast 61 |
| Kiesy bogatych rodow | rosna o 18-25 tys. zl/d | renty ida wedlug lenn, nie wedlug potrzeby - patrz ryzyko 1 |

---

## 9. Ryzyka i slabosci (uczciwie)

1. **Zloto przeniesie sie z kies ludu do kies rodow, ktore go nie potrzebuja.** Renty 180 dziela wedlug lenn, a 203 z 332 rodow ma luz pulapu (TRIAZ O1-7 pkt 5). Z 72.5 tys. dla rodow
   na zold pojdzie realnie 13.5-20 tys.; 18-25 tys. dziennie zostanie w kiesach glow. To nowy zbiornik (dzis 77 rodow "trzonu" uroslo z 21.8 do 25.3 mln, TRIAZ O1-1).
   Lekarstwo jest poza tym projektem (rozdzial rent, O1-2). Bez niego C zamienia "pieniadz lezy u ludu" na "pieniadz lezy u Boltonow" w jednej trzeciej.
2. **Wieczna wojna = wieczny podatek i wieczne -10 lojalnosci.** W tym swiecie 25 z 30 krolestw walczy caly rok. Miasta z obcym panem (kara kultury do -3 na dobe) sa blisko progu i przestana placic;
   czy fala buntow ruszy mimo granicy 30 - **nie wiem, bo lojalnosci miast nie ma w zadnym logu biegu** (26 trafien "lojalnosc" w LOG to wylacznie zdobycia). Pomiar musi wejsc razem z R1 (paczka K1).
3. **Pozyczka jest pomostem, nie rozwiazaniem.** Zdolnosc 4.7 mln, potem dlug stoi na limicie i kosztuje panow 1.5 tys. zl/d na zawsze; w swiecie bez pokoju nigdy nie jest splacana z "po pokoju".
   Trafia tylko do panow bogatych miast (38% lokaty lezy w miastach panow w potrzebie; pan biednego miasta nie dostaje nic).
4. **Lud dalej nie ma na co wydawac.** C niczego ludowi nie daje poza odsetkami (1.5 tys. zl/d): w miastach wojennych zabiera ok. jednej trzeciej wplywow (30 miast z dodatnim saldem: mediana 31%,
   najwiecej 61%; c4.py - zgrubnie, 8 migawek), bo tyle wynosi to, czego nie da sie tam wydac. Historycznie to obraz miasta w latach wojny, ale to podatek, nie zycie gospodarcze. Kierunki "lud wydaje / inwestuje" odpowiadaja na pytanie Jeffa
   pelniej; C jest tym, co zostaje, gdy polek nie da sie zapelnic.
5. **Niedopasowanie geograficzne.** Korony, ktore pozyczaja najwiecej (Pentos, Qohor, Braavos, Lys), maja miasta bez grosza - pomocy nie dostana. Zloto wraca do Polnocy, Krolewskiej Przystani i Dorzecza.
6. **Liczba rownowagi jest chwiejna.** F to roznica dwoch duzych liczb (wplywy 525 tys. - wydatki 486 tys.); spadek wplywow ludu o 7% zeruje ja. Przedzial 47-83 tys. to nie blad pomiaru, tylko niepewnosc modelu.
7. **Wstrzas na starym zapisie:** +200 tys. zl/d do skarbcow w dobie 1 (renty x 2.2 przez pierwszy miesiac: 159 + ok. 190 tys.). W nowej kampanii go nie ma.
8. **Pokoj niezmierzony** - jesli lud odklada takze w pokoju, lokata urosnie do nastepnej wojny (wtedy 1% dziennie od duzego zapasu = duzy pierwszy rzut; to akurat ma sens historyczny).
9. **Dwa pobory od tego samego ludu w wojnie** (184 od zarobku do 3% i pomoc od oszczednosci) - dla gracza to dwie liczby do zrozumienia. Po tescie rocznym mozna 184 wygasic (13 tys. zl/d), zostawiajac jedna podstawe;
   stawke s i tak trzeba zostawic jako regulator.
10. **Cofniecie DLL:** stary kod przy wczytaniu odrzuca roszczenia rodzaju 6 (DebtLadder.cs:1222) - dlug wobec miast znika z ksiegi, zloto zostaje u pana. Przed cofnieciem wylaczyc `TownLoans` i poczekac na splate albo pogodzic sie ze strata ludu.

---

## 10. Koszt czasu, wylaczniki, zapis

| | Koszt na dobe | Wylacznik (wylaczony = stan sprzed paczki) | Zapis |
|---|---|---|---|
| R1 pomoc | 97 x 3 + 130 = 421 wolan po kilkadziesiat ns w istniejacej petli = ok. 0.03 ms | `FolkWarAid` (pobor 0); stawka `FolkWarAidShare` | nic nowego: stawka s jest w kluczu 165 (szoste pole), reszta ponizej 1 zl na osade zyje w sesji |
| R3 granica | postfiks: jedno wyszukanie w slowniku na liczenie lojalnosci (227 osad na dobe + dymki) = ok. 0.01 ms | `FolkWarAidLoyalty` (kara 0; granica 30 zostaje przy poborze) | nic (znacznik "placila wczoraj" w sesji; po wczytaniu najwyzej jedna doba bez kary) |
| R2 pozyczka | do 124 rodow w potrzebie x kilka lenn + roszczenia w istniejacej petli drabiny = ok. 0.03-0.05 ms | `TownLoans` (nowych pozyczek brak, stare splacane dalej); `TownLoanRentDays`, `TownLoanRate` | klucz `arm_debt168`: rodzaj 6 i siodme pole (stopa, udzialy klas) - stary zapis wczytuje sie bez zmian |
| R4 karta | pomijalny | `TownCharters` (domyslnie wylaczony) | `arm_townfolk`: jedno pole na osade (doba konca karty) |
| **Razem** | **ponizej 0.1 ms** wobec 8.3 ms latek i 2.4 ms bloku kiesy dzis (LOG :129003) | | |

Po dodaniu kluczy: `python tools/gen_mcm.py`. Klucze sa nowe, wiec pulapka "MCM trzyma stara wartosc" (CLAUDE.md rozdz. 7) ich nie dotyczy.
Wyjatki per osada / per rod z licznikiem potkniec, bez globalnych wylacznikow (CLAUDE.md rozdz. 7). Wylaczenie kiesy ludu (`TownFolkLive`) gasi R1 (bramka `WtOn`) i wstrzymuje nowe pozyczki; raty ida wtedy do kasy osady.

---

## 11. Wielkosc i paczki

Calosc: **M** (K1 + K2 = S, K3 = M, K4 = S na slowo). Jedna zmiana naraz, w tej kolejnosci:

| Paczka | Co | Wielkosc / tor | Pliki | Test |
|---|---|---|---|---|
| **K1** | R1 pomoc wojenna + granica poboru (lojalnosc <= 30) + pomiar lojalnosci w linii (srednia, ile osad <= 30 i <= 25, ile wstrzymanych) + zdanie w "People of this land" + komunikaty | **S** (tor S: sam + autotest 120 dob; ok. 120 linii) | KingdomTreasury.WarTax.cs, TownFolk.Live.cs, BesiegedPay.cs, MoneyLedger.Obieg.cs, PeopleMenu.cs, Settings.cs, sprawdz_logi.py | 120 dob na zapisie z d364: lokata, wplywy koron, renty, zold, lojalnosc (sam pomiar) |
| **K2** | R3 kara lojalnosci (latka na czynny model, zakladana w kampanii) | **S** (ok. 60 linii) - osobno, bo to jedyna latka na cudzy model | nowy maly plik + ArmouryBehavior (EnsureHooks) | 120 dob: osady w stanie buntu nie wiecej niz w K1 + 2 |
| **K3** | R2 pozyczka miejska (AI i gracz) | **M** (tor M: 1 przebieg z 1 recenzja; ok. 300 linii) - tylko na slowo Jeffa (PLAN rozdz. 3 "kasy miast z dlugami, pozyczkami...") | DebtLadder.cs, ClanBudget.cs, TownFolk.Live.cs, menu, Settings.cs | 120 dob + wczytanie zapisu z dlugiem; reczna pozyczka gracza |
| **K4** | R4 karta miejska | **S** - tylko na slowo Jeffa; nie zalecam teraz (5 tys. zl/d) | TownFolk.Live.cs, menu | - |

K1 to dopracowana poprawka B z TRIAZ O1-7 (ten sam pomysl: danina takze z kies ludu) - **nie robic obu**; roznice: stawka z regulatora s (bez wstrzasu w nowej kampanii), wagi klas 1/2/3, granica lojalnosci, pomiar.
Bieg roczny po K1 + K2, przed decyzja o K3.

---

## 12. Progi do testu rocznego (nowa kampania, 364 doby; w nawiasie stan z biegu 13.10)

| # | Prog | Linia logu |
|---|---|---|
| 1 | Kiesy ludu miast: zmiana d182 -> d364 w przedziale +-1.5 mln (bylo +11.7 mln: 24.89 -> 36.59; c4.py); stan d364 <= 24 mln (36.6) | "Pieniadz swiata" |
| 2 | Lokata W d364 <= 11 mln (23.8) i nie rosnie w ostatnich 90 dobach | "Kiesa ludu" |
| 3 | Pomoc wojenna pobrana d301-364: 40-110 tys. zl/d; kontrola kies r = 0 kazdej doby | "Korona: podatek wojenny ludu (184)", "Kiesa ludu" |
| 4 | "Stac jedzenie" swiat >= 98% i miast ponizej 95% nie wiecej niz 10 (98.2% / 10); "zjedzone bez zaplaty" <= 2.5% wydatku gry (2.27%) | "Kiesa ludu" |
| 5 | Zwrot zoldu wyplacony >= 98% naleznego (95.2%); renty 180 d301-364 >= 190 tys. zl/d (159) | "Korona: wplywy dnia (165)", "Renty korony (180)" |
| 6 | Dlug koron w Banku d364 <= d182; pozyczaja tylko krolestwa bez lokaty miast | "Kredyt korony (186)" |
| 7 | D rodow razem d364 >= 690 tys. (644); zold partii naliczony >= 395 tys. zl/d (383); ludzie w partiach rodow >= 64 tys. (62.5) - progi ostrozne, bo C daje +1.5-3.3 tys. ludzi | "Budzet rodow (166)", "Zold", "Skarbce" |
| 8 | Kiesy rodow z luzem pulapu: przyrost roczny <= +6 mln (ryzyko 1); kompanie najemne osobno | budzet-rodow.csv |
| 9 | Lojalnosc: srednia osad placacych >= 40; osad w stanie buntu (<= 25) nie wiecej niz w biegu bez kary + 2; buntow wywolanych w osadach placacych 0 | nowy dopisek K1 |
| 10 | Suma zlota swiata: zmiana w 28 dobach w progu 0.1% (po poprawce bledu wezwan 168 z TRIAZ O1-7 A) | "Pieniadz swiata (bilans)" |
| 11 | Czas: blok kiesy ludu + nowe wolania <= dzisiejszy + 0.2 ms | "Kiesa ludu: czas" |
| 12 | K3: dlug wobec miast <= 6 mln; zaleglosci z tego tytulu <= 3 rody naraz; "bez odbiorcy" 0; kredyt wojenny Banku mniejszy niz bez K3 | "Dlugi (168)" |

---

## 13. Co sprawdzilem w cudzym kodzie (kazda rzecz, na ktorej stoje)

| Twierdzenie | Gdzie | Wynik |
|---|---|---|
| Czynny model lojalnosci to BK; metoda publiczna, zwraca strukture z pozycjami | BK Main.cs:197; BKLoyaltyModel.cs:77 | tak - postfiks z `ref ExplainedNumber __result` moze dopisac pozycje |
| Klasa modelu ma pola statyczne czytajace teksty gry | BKLoyaltyModel.cs:32 (`GameTexts.FindText`) | tak - latka tylko w kampanii |
| Dryf lojalnosci 0.1 na punkt od 50 | BKLoyaltyModel.cs:318; G DefaultSettlementLoyaltyModel.cs:294 | tak - stala kara x daje trwale -10x |
| Progi buntu 25 / 15, milicja 1.4 x zaloga | G DefaultSettlementLoyaltyModel.cs:64-66; RebellionsCampaignBehavior.cs:38, :189 | tak |
| Zamki nie buntuja sie w grze, ale buntuja sie w BK | G RebellionsCampaignBehavior.cs:106 (`IsTown`); BK BKSettlementBehavior.cs:626, :674-707 | tak - kara i granica musza objac zamki |
| Dekret podatkowy BK: lojalnosc +-(R + 0.8 x chlopi) x 4; AI ustawia go wedlug lojalnosci (80 / 30 + 8 x hojnosc), tylko z namiestnikiem | BKLoyaltyModel.cs:91-108; BKSettlementBehavior.cs:400-414 | tak - gotowy hamulec panski; nie nadaje sie na "kupiony przywilej" |
| Autonomia BK: +2 x lojalnosc, -60% podatku, sama dryfuje do celu | BKLoyaltyModel.cs:149; BKTaxModel.cs:194-197; BKStabilityModel.cs:70-81 | tak - nie da sie jej "kupic" na stale |
| BK nie ma wlasnych pozyczek ani dlugow miast | przeszukanie nazw plikow BK (Loan, Debt, Charter, Privilege) | brak (sa tylko przywileje najemnikow i rady) |
| Cena i popyt nie widza kiesy ludu | za ROZP 1.3 pkt 8 (G DefaultSettlementEconomyModel.cs:61-73; BK BKEconomyModel.cs:535-554) | nie sprawdzalem ponownie |
| Kredyt wojenny, drabina dlugu, roszczenia wielu wierzycieli z pulapem 15% D istnieja i obejmuja gracza | SRC DebtLadder.cs:20-42, :471-570, :797-867; Settings.cs:1138 | tak |
| Podatek 184 wplywa do skarbca jako wplyw dnia przed `CrownIncome.Begin` | SRC WarTax.cs:174-175, :183; ArmouryBehavior.cs:1600, :1641 | tak |
| Reszta wplywow dnia korony idzie rentami tego samego dnia | SRC CrownRents.cs:15-19; LOG :128862 ("zostalo z wplywow dnia w skarbcach 51") | tak |

Nie sprawdzone: czy RealisticBannerlord albo ROT podmieniaja model lojalnosci po BK (dlatego latka na typ czynnego modelu, nie na nazwe klasy); jak czesto gra liczy lojalnosc poza tickiem dobowym (dymki) - bez znaczenia dla kosztu.

---

## 14. Rozwazone i odrzucone (z rachunkiem)

| Wariant | Dlaczego nie |
|---|---|
| Sama pozyczka, bez podatku | znak odsetek (1.1): dlug 561 mln po 10 latach albo stop na limicie 4.7 mln i kiesy rosna dalej |
| Bank Zelazny przyjmuje lokaty miast (najmniej kodu: lokata -> kapital Banku) | Bank nie ma komu pozyczyc: limity dluznikow (15 dni dochodu z ziemi, 27 rodow na limicie; korony 180 dni podatku) wiaza, nie kapital - caly popyt to ok. 25-35 tys. zl/d brutto. Lokata 20 mln lezalaby w Banku zamiast w miastach |
| Miasto potraca sobie rate z renty u zrodla (zastaw uzytkowy, bez drabiny) | prostsze, ale gracz moze pozyczyc i oddac miasto; dlug musi isc za rodem, wiec i tak trzeba roszczenia w ksiedze |
| Pozyczka miast dla korony | zbedna obok pomocy (R2, akapit "dlaczego nie") |
| Pomoc najpierw dla pana miasta w potrzebie, reszta koronie | lepsza zamiana na wojsko (brak zoldu panow miast 20.5 tys. zl/d pokryty trwale = +3.3 tys. ludzi), ale druga regula na zjawisko "pan nie ma na zold" obok kredytu wojennego i trwale silniejsze rody bogatych miast. **To jest pytanie o kierunek - punkt 15.2** |
| Podatek raz na 28 dob ("dziesiata" jak w zrodlach) | 10% co 28 dob = 0.36% dziennie -> lokata w rownowadze 15-22 mln, czyli prawie tyle co dzis |
| Podniesc stawke 184 od zarobku | zeby wziac 55-80 tys. zl/d z zarobku 525 tys., trzeba 10-15% od kazdego, takze od biednych miast, ktorym brakuje na chleb; podatek od zapasu ponad potrzebe bierze tylko od tych, ktorzy maja |

---

## 15. Pytania do Jeffa - tylko kierunek rozgrywki

1. **Czy mieszczanie maja w wojnie oddawac koronie czesc oszczednosci** - tak jak juz oddaja kupcy i wsie? (Rada: tak. To jest zamkniecie obiegu; reszta to dodatki.)
2. **Komu:** koronie, ktora dzieli miedzy wszystkich wasali (prosto, jedna regula) - czy najpierw panu tego miasta, gdy brakuje mu na zold (wiecej wojska, ale panowie bogatych miast rosna w sile)? (Rada: koronie.)
3. **Czy miasta maja sie od tego burzyc** (do -10 lojalnosci, a na skraju buntu przestaja placic)? (Rada: tak - inaczej to podatek bez ceny.)
4. **Pozyczka od wlasnego miasta** - chcesz jej, takze dla siebie w menu miasta? To pomysl, ktory 07.10 odlozyles "na Twoje slowo". (Rada: po tescie K1; daje wybor i klimat, obiegu nie zamyka.)
5. **Karty miejskie** - na pozniej? (Rada: tak, na pozniej.)
