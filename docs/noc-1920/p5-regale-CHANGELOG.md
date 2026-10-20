# p5-regale - regale gornicze: udzial pana w sprzedazy rudy (R3-09f, POMYSLY-35 pkt 30), galaz w-toku/p5-regale na 5189c0fe (kandydat WGRANIA 92)

## Dla Jeffa (prostymi slowami)
Pan wsi gorniczej bierze teraz z rudy wiecej niz ze zboza: oprocz zwyklego podatku (45% utargu) dostaje jeszcze 1/8 tego, co woz wsi
dostal na targu za zelazo, srebro albo zloto - tyle bral krol czeski z kopaln srebra w Kutnej Horze. Pieniadz idzie z kasy miasta (zaplata
za rude) przez sakwe wozu i kiese wsi do pana; nic nie powstaje z niczego. Kopalnia, ktora ma dlug u kupcow za drewno albo za malo
pieniedzy na tydzien drewna do dymarki, jest z tego zwolniona (tak panowie zwalniali kopalnie w biedzie), zeby wytop nie stanal.
Dotyczy tez Twoich wsi gorniczych. Efekt w zlocie bedzie raczej skromny (dzis kopalnie maja malo gotowki), dokladne liczby pokaze test.

## Problem / zrodlo pomyslu
- Audyt 12 D-13 (`docs/audyt-2026-10-09/12-DOCHODY-RODOW.md:209`, krytyka pkt 24 `:478`) i audyt 15 (lista "z raportu 12 bez zmian: D-13 regale
  gornicze (do pomiaru)", `15-BILANS-PANOW-ZAMKOW.md:285`): pan wsi gorniczej bral z utargu za rude ten sam udzial co z plonu, "Lannister >> inni"
  sie nie dzialo - dochod z ziemi na rod Westerlands 1 530, Polnoc 1 952, Dolina 1 966. Historycznie 1/10-1/4 wartosci kruszcu (stannaries,
  Kutna Hora). W audycie bez liczby dla gry.
- Stan w kodzie (WGRANIE 91): udzial pana z utargu wozu = BK 70% x `VillageLordScale` 0.5 + `VillageLordExtraShare` 0.1 = 45% (zwykly dekret;
  `TownFolk.Live.cs` VillageTaxPostfix), dla rudy tak samo. Log 160 dob na WGRANIU 91 (`Armoury-2026-10-20_00-44-29.log`, doba 109234-109235):
  "Utarg wsi (112)": pan 35.6-37% + majatki BK ok. 8%, wies 55-56%. Linia "Zelazo (lupy)": wozy kopaln z dymarka (F4b) - 28 ostatnich dob
  60 powrotow, 332 624 zl (ok. 11 900 zl/d); kiesy kopaln mediana 238 zl, 15 z 25 ponizej kosztu drewna doby, "brak zlota wsi" 820 ladunkow,
  "pusta kiesa wsi" 1009 - kopalnie zelaza sa dzis bez gotowki (stad zwolnienie w regule).

## Zmiana (Armoury; nowe pliki `MineRegale.cs`, `Bloomery.R9f.cs`; klucze MCM w grupie "Crafts, raw materials and horses (stage 6)")
1. **Sprzedaz wozu** (`SellGoodsForTradeAction.ApplyInternal` - miasto; zamek przez CastleMarket ta sama metoda): prefiks First zapisuje ladunek
   wozu wsi z kruszcem (`iron` - lupa przy Bloomery albo ruda, `silver`, `gold_ore`) i cene kazdej pozycji `Town.GetItemPrice(.., woz, true)` - ta
   sama cena, od ktorej sprzedaje gra i BK SellGoodsPatch (sprawdzone w dekompilacji: BK liczy kazda pozycje ta sama metoda); postfiks First
   (przed zwrotem nadplaty MarketCarts i zakupami wsi przez woz, ktore zmieniaja tylko zloto) - sprzedane sztuki z roznicy ladunku, wartosc
   kruszcu i calego sprzedanego ladunku na konto wozu (kilka miast - sumy).
2. **Powrot wozu do wsi macierzystej** (`VillageTakings.EnterPostfix`, po 112 i F4b, PRZED pracami okregu, wioska gracza i daninami DS):
   utarg za kruszec = oddany utarg x (wartosc kruszcu / wartosc ladunku); podstawa = utarg za kruszec - rachunek za drewno pokryty w tym oknie
   (F4b: d = min(rachunek, utarg) - jak podatek pana, od tego, co zostalo po drewnie); **regale = `MineRegaleShare` (0.125 = 1/8) x podstawa**.
   Placi czesc wsi z TEGO utargu: najwyzej przyrost kiesy wsi w oknie (nigdy z oszczednosci). Zloto: `Village.ChangeGold(-x)` i
   `TradeTaxAccumulated += x` (licznik, z ktorego pan zbiera podatek wsi) - dokladnie tyle, ile kiesa oddala.
3. **Zwolnienie** (kopalnia z dymarka): dlug u kupcow za drewno (f5-ruda, `F5Credit.Owed > 0`) - regale 0; inaczej regale tylko z kiesy ponad
   zapas 7 dob drewna dymarki (potrzeba doby ponad las okregu x cena drewna w miescie handlowym - ta sama miara co "kiesy kopaln ponizej kosztu
   drewna doby"). Srebro i zloto (bez dymarki) - bez zapasu.
4. Klucze: `MineRegale` (TAK), `MineRegaleShare` (0.125, suwak 0..0.5, `tools/gen_mcm.py` RANGES). Wymaga `VillageTakingsWhole`. Klucz NIE -
   prefiks nie zapisuje kont, przy powrocie nic nie pobiera (utarg jak dotad).
5. Zapis `arm_regale` (SaveText.Sync): "1|idWozu~kruszec~ladunek~doba" - konta wozow w drodze (sprzedaly kruszec, jeszcze nie w domu). Brak klucza
   (stary zapis, Ironman Jeffa) = pusto; starsza DLL klucz pomija. Reset bez wpiecia w konstruktor ArmouryBehavior (zmiana obiektu Campaign;
   wczytanie czysci w Import). Konta wozow, ktorych nie ma na mapie, zamyka linia doby.
6. `VillageTakings.RegaleTaken`: `SplitToday` i utarg doby wedlug wsi (`ForEachVillageToday`, alfa okregu KS) widza czesc wsi po regale.
7. Wpiecia w plikach wspolnych: `SubModuleMain` (ApplyAll), `ArmouryBehavior` (SyncData `arm_regale`, Daily po DsVillage.Daily),
   `VillageTakings.EnterPostfix` (2 linie: Open przed CoalFirst, AtCart po nim), `Settings.cs` (2 pola przy `BloomeryCoalBeforeLordShare`).

## Ryzyko / kontrola calosci (w kodzie)
- **Podwojne liczenie DS**: DsVillage.DuesAtCart biegnie PO regale i bierze `part` = przyrost kiesy w oknie (juz bez regale) - udzial korony W4
  liczony od czesci wsi netto, dziesiecina 5% od calego utargu jak dotad; regale w DS wchodzi do "pan" (`_cLord = handed - part - ...`). Czynsz
  staly / renta wsi licza od kiesy, ktora juz nie ma regale. Kopalnie BK w miastach (polowa ceny rudy jako danina) - inna droga (warsztat miasta),
  tu tylko wozy wsi. Nic nie liczy sie drugi raz.
- **Ksiega pieniadza**: przelew kiesa wsi -> licznik wsi w srodku okna powrotu wozu (jak F4b w druga strone) - suma "kiesa + podatek" okna bez
  zmian, bez Note169 (to nie odplyw z wsi); "Pieniadz swiata" liczy kiesy i liczniki wsi razem.
- **Linia "Utarg wsi (112)"** pokazuje udzial pana PRZED regale (krok 112) - regale tylko w nowej linii (nie ruszalem wzorcow tej linii).
- **Kopalnie bez gotowki**: mniej dla wsi = mniej na drewno i chleb gornikow; zwolnienie (dlug / 7 dob drewna) to ogranicza, ale autotest musi
  pokazac, ze "brak zlota wsi" i "pusta kiesa wsi" w linii zelaza nie rosna. F5 (splata kredytu z przyrostu kiesy) - przy dlugu regale 0.
- **Prace okregu / wioska gracza**: regale przed nimi - DistrictWorks dzieli pule z licznika i kiesy w tej samej sumie (zmienia sie tylko
  proporcja); SideVillageFief.AtCart to po r4-wioska tylko licznik linii (regale wejdzie do jego "podatku pana z okien wozow"), naleznosc
  Twojej wioski bocznej liczy osobno Due z wartosci produkcji - bez zmian.
- **Okno niedomkniete 112** (Whole112 false, w logu 0) - konto wozu zostaje do nastepnego powrotu (udzial kruszcu liczony z obu kursow).
- **Watki**: sprzedaz i powrot to zdarzenia wejscia do osady (watek gry); konta pod lockiem, migawka tylko dla wozow z kruszcem (alokacja
  rzadka, ok. kilkadziesiat sprzedazy na dobe), bez pracy w PartiesThink.
- Wyjatki liczone per miejsce (pierwszy z kazdego do logu), bez globalnych wylacznikow.

## Status
DO SPRAWDZENIA - build Armoury kod 0, 0 Error(s). Nie wgrane, nie testowane w grze (zakaz uruchamiania gry w tym zadaniu).

## Decyzje (projektuje Claude)
- Stawka 1/8 (urbura z Kutnej Hory, Ius regale montanorum ok. 1300; srodek przedzialu audytu 1/10-1/4) zamiast 1/5 z wskazowki: kopalnie zelaza sa
  dzis bez gotowki (mediana kiesy 238 zl), 1/5 zjadloby ok. 36% ich czesci utargu. Ta sama stawka dla zelaza, srebra i zlota.
- Regale od calej wartosci kruszcu (nie od czesci wsi), ale placone z czesci wsi tego utargu - pan ma z rudy 45% + 12.5% = 57.5%, ze zboza 45%.
- Podstawa po rachunku za drewno (F4b) - ta sama kolejnosc co podatek pana; zwolnienie kopalni w biedzie ("Freiung") zamiast twardego poboru.
- Majatki BK nie dostaja udzialu w regale (BK liczy je tylko od swojego podatku przy powrocie wozu).
- Kruszce: `iron`, `silver`, `gold_ore` (te, ktore daja kopalnie wsi w ROT - tabele KS i RockGold); sol, glina, kamien - bez regale.

## Test w grze (autotest)
- Linia doby `Regale gornicze (R3-09f): dzien N | CZYNNE, stawka 0.125 ...` codziennie; `sprzedaze wozow z kruszcem` > 0 (zelazo, srebro),
  `powroty z kruszcem` > 0, `pobrane` > 0 w wiekszosci dob; `kontrola: kiesy wsi -X, liczniki panow +X, reszta 0` i
  `nalezne = pobrane + zwolnione: reszta 0` w kazdej dobie; `potkniecia` brak.
- Linia "Zelazo (lupy)": "brak zlota wsi", "pusta kiesa wsi" i "kiesy kopaln ponizej kosztu drewna doby" nie gorsze niz bez paczki o wiecej niz 20%
  (punkt odniesienia: log 160 dob WGRANIA 91, 15 z 25, 820 / 1009 ladunkow).
- "Utarg wsi (112)": "bez odbiorcy razem 0" bez zmian; "Daniny wsi (DS)" platnicy - odbiorcy = 0; "Pieniadz swiata" bez nowej roznicy.
- Po zapisie i wczytaniu: `konta wozow w drodze` nie spada do 0 (konta z zapisu).
- Klucz NIE (MCM): linia "WYLACZONE w ustawieniach", `pobrane 0`.
