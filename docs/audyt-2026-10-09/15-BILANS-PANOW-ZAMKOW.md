# AUDYT 15 - BILANS PANOW ZAMKOW: czy pan zamku jako baza produkcyjna ma sensowny bilans i proporcje (09.10.2026, wersja 2 po krytyce)

## 0. Dla Jeffa

1. **Masz racje co do faktow:** okreg zamku (zamek i 2 wsie) to baza produkcyjna. Jego wsie wytwarzaja dziennie towar za ok. 1 190 zl (46% towaru wsi swiata), miasta placa za niego ok. 1 000 zl, a pan bierze z tego ok. 650-680 zl.
2. **Ten dochod juz jest jego czescia produkcji i podatkow jego ludzi - i to czescia za duza:** pan bierze 68% utargu i prawie cala gotowke, ktora zostaje chlopom, a historycznie pan razem z Kosciolem bral 30-50% gotowki wsi (Twoja decyzja z 09.10); sam zamek tez nic nie dawal ("zamek nie jest wart nic rocznie", spisy majatku 1425-1446).
3. **Bilans sie nie zamyka przez wojsko, nie przez dochod:** 525 ludzi przez caly rok kosztuje ok. 2 000 zl dziennie, 3 razy tyle, ile daje cala ziemia; historyczny pan trzymal w pokoju 20-50 ludzi za 10-25% dochodu, a wojne po 40 dniach placil krol z podatkow od wszystkich.
4. **Wartosc ucieka od pana w trzech miejscach:** co czwarta moneta towaru znika (Banner Kings, rozbite tabory), kasa zamku netto kasuje ok. 275-300 zl prawdziwych pieniedzy dziennie, a zold i zakupy jego ludzi zostawiaja w miastach ok. 1 400-1 700 zl dziennie, ktore przez nadwyzke kupcow trafiaja do panow miast.
5. **Twoje 30-50% robie tak:** pan bierze 35% utargu wsi, sept 5%, a wies zatrzymuje 60% i wydaje je w miescie (kiesa ludu, paczka 173); przez polityke podatkowa Banner Kings pan wybiera 25, 35 albo 45%, czyli razem z septem 30, 40 albo 50%.
6. **Bez wyrownania to uderza prawie tylko w panow zamkow:** pieniadze chlopow ida do miast, wiec w rachunku panowie zamkow traca 14% ludzi w wojnie i 32% w pokoju, a panowie miast nic.
7. **Wyrownanie z tych samych pieniedzy (Z15-5, mechanizm gry, nie historia):** pan miasta dzieli nadwyzke kupcow z korona po polowie zamiast 2/3, a korona oddaje to rentami stalymi wedlug rodzaju lenna (miasto 2, zamek 1.5, wies 0.25) - wtedy panowie zamkow traca 1% w wojnie i 4% w pokoju, a placa za to panowie miast i krolowie (ok. 7-15% ich stalego dochodu).
8. **Skutek:** pan zamku utrzyma ok. 415-455 ludzi w wojnie i ok. 130 w pokoju, w nowej kampanii bez bankructw, a 6-7 dzisiejszych bankrutow splaci dlug z dochodu wsi (paczka 168).
9. **Trzy pytania, ktore zmieniaja Twoja gre, sa w rozdz. 6:** Twoj udzial w nadwyzce miasta i waga zamku w rentach; targ przy zamku dla podzamcza; lup z rozbitych taborow, band i karawan.

Status: **TYLKO ODCZYT.** Nic nie zmienione w kodzie, w grze, w ustawieniach ani w zapisach. Gra nieuruchamiana, nic nie wypchniete. Jedyny zapisany plik to ten raport.
Rachunki [R] (rozdz. 3.5 i 5) policzylem na kopii programu kiesy ludu wczytanej do pamieci (`python -I -` ze standardowego wejscia), bez zapisu plikow programu (rozdz. 8).

**Wersja 2 (po krytyce, rozdz. 9):** 14 uwag sprawdzonych w kodzie, logu i rachunku - wszystkie przyjete (2 z poprawka). Najwazniejsza zmiana: wersja 1 nie znala
Twojej decyzji z 09.10 ok. 03:45 (pkt 15: "bierze 30-50%, a nie 2/3") i zostawiala panu 65% utargu "bez pytania". Teraz caly rachunek stoi na 35% + sept 5%, z dziesiecina
(wersja 1 liczyla ja bez), jedna miara historyczna (gotowka wsi), kasa zamku liczona netto, Z15-3 bez karawan, a Z15-5 nazwane wprost jako przesuniecie przez korone.

Na czym stoi:
- `scratchpad\zamki\lancuch.md` (lancuch wartosci okregu zamku, T9 doby 31-120);
- `scratchpad\zamki\historia.md` (podzial produkcji majatku, rachunki dworow, zbrojni, XIII-XV w.);
- raport 12 (`12-DOCHODY-RODOW.md`: B1-B18, D-1..D-13, W-1..W-11, rachunek E2/S3);
- `PROJEKT-KIESA-LUDU-2026-10-09.md` (paczka 173);
- `PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (budzet 166, korona 165);
- raporty 01, 02, 09, ESENCJA; `scratchpad\dochody\pomiar.md`, `kod.md`, `historia.md`;
- decyzje Jeffa w `STAN-PRAC.md`: 05.10 "wojsko zostaje, gospodarke dociagamy", wpis 100 "chlop sam do miasta"; 08.10 zalogi w pokoju o polowe; 09.10 B (dziesiecina),
  C (podatek wojenny zamiast mniej wojska), **09.10 ok. 03:45 pkt 15 (obciazenie wsi: cel 30-50% pan + sept, 2/3 tylko przejsciowo do 166), pkt 18 (korona 1/9 lupu wasali)**.

Oznaczenia:
- **[K]** kod (plik:linia);
- **[P]** pomiar (test T9, 120 dob, srednie dob 31-120; wojna u 94-97% rodo-dob);
- **[H]** historia ze zrodlem (adresy w `zamki/historia.md` rozdz. 9 i `dochody/historia.md`), **[H?]** historia z drugiej reki;
- **[S]** szacunek (rzad wielkosci, +-30%);
- **[R]** rachunek (symulacja).

Przelicznik: 1 zl gry = 1 pens ok. 1300; 1 L rocznie = 0.66 zl na dobe; rok = 364 doby.
"Okreg zamku" = zamek i 2 wsie: tyle ma 113 ze 114 panow samych zamkow [P].

---

## 1. Lancuch wartosci okregu zamku w grze wobec sredniowiecza

### 1.1 Okreg zamku w liczbach (srednio na okreg) [P][S]

| Co | Gra | Zrodlo |
|---|---|---|
| ludnosc Banner Kings (BK) | zamek ok. 2 760 + 2 wsie po ok. 1 880 = **ok. 6 500** (zamek 42%, wsie ok. 3 760) | [K] BK `PopulationManager.cs:471-481`; [S] KIESA 1.2 |
| ludnosc ksiegi Armoury (`PopulationLaw`) | 113-147 tys. (zamek 0) | [P] `ludzie-regiony.csv`; [K] `PopulationLaw.cs:121-123` (ludzie tylko wsi i miast) |
| wartosc towaru wytworzonego w 2 wsiach | **1 191 zl/dobe** (zboze, nabial, ryby, welna, drewno) | [P x P] lancuch 4.1 |
| utarg (miasto placi taborom) | **1 007 zl/dobe**; do wsi dochodzi 935 (70 znika z rozbitymi taborami) | [P] |
| wojsko pana | 525 ludzi (druzyny 312 + zaloga 213), zold **2 009 zl/dobe** (3.8 zl na czlowieka) | [P] `budzet-rodow.csv` |
| ludzie pod bronia / ludnosc | 8% ludnosci BK okregu, 0.4% ksiegi | [S] |
| udzial wszystkich okregow zamkow w swiecie | **28% ludzi BK** (0.85 z 3.09 mln), 36% ksiegi, 47% hearth wsi, **46% wartosci towaru wsi** | [S] z KIESA 1.2; [P] lancuch 2 |
| ... w dochodzie panow | **14.5%** dochodu z ziemi wszystkich panow (95 z 653 tys./dobe); 24% stalego dochodu D rodow | [P] lancuch 7 w. 5; `pomiar.md` 3 |

### 1.2 Kazda moneta towaru okregu: gra wobec proporcji sredniowiecznych

Okreg zamku, zl na dobe [P/S] (lancuch 4.1). Historie porownuje **jedna miara: udzial w gotowce wsi** (to, co chlopi maja ze sprzedazy plonu i z innych wplywow
w monecie). Pan razem z Kosciolem bral **30-50%** tej gotowki [S z H]: czynsz, prawa panskie, dziesiecina i podatek to ok. 23-25 d na glowe rocznie z ok. 200 d dochodu,
czyli ok. 12% calego dochodu, ale 30-50% gotowki rodziny chlopskiej (raport 01 2.2; ESENCJA; decyzja Jeffa 15).

Dwie miary pomocnicze, tylko obok [S]:
- villein na wirgacie oddawal panu ok. 55-60% swojej gotowki (`zamki/historia.md` 2.2) - to gorny przypadek chlopa niewolnego, a liczby wirgaty sa niepotwierdzone;
- "pan swiecki 35-60%, z Kosciolem 45-77%" z `zamki/historia.md` 2.5 to udzial w sprzedazy **calego majatku z folwarkiem**, liczony jako 18-23% dochodu z ziemi
  (Campbell) podzielone przez 30-40% sprzedawanego plonu (Britnell). Zaklada to, ze caly dochod panski byl gotowka ze sprzedazy, a nie byl: dwor zjadal czesc plonu
  folwarku, a dziesiecina szla w naturze. To wiec **gorna granica [S], nie [H]**, i nie sluzy jako cel.

| Ogniwo | Gra zl/dobe | % wartosci | % utargu | Sredniowiecze | Ocena |
|---|---:|---:|---:|---|---|
| **towar wytworzony we wsiach** | **1 191** | 100 | - | towar na targ ok. 1/3 produkcji chlopa (30-40% zboza - Britnell); na glowe ok. 67 d rocznie [S z H] | w skali BK **zgodne**: 66 zl na glowe rocznie (lancuch 7 w. 6) |
| **utarg** (miasto placi taborom) | **1 007** | 85 | 100 | - | - |
| **pan** (licznik podatku wsi BK 554 + renta 20% kiesy wsi z utargu 126) | **680** | 57 | **68** | **pan + Kosciol 30-50% gotowki wsi** | **za duzo**: 68% utargu i do tego prawie cala reszta gotowki chlopow (renta) |
| majatki BK (glownie notable) | 100 | 8 | 10 | wolni dzierzawcy i drobna szlachta | prawdziwy udzial (sila robocza majatku) [K] BK `EstateData.cs:89-99` |
| **chlopi** (kiesa wsi na koniec doby) | **0** | 0 | 0 | **50-70% swojej gotowki** | **zle** |
| Kosciol | 0 | 0 | 0 | w 30-50% razem z panem (dziesiecina glownie w naturze) | po 173: 5% utargu (decyzja Jeffa B) |
| korona (danina z kiesy wsi) i BK rynek wsi | 17 | 1.4 | 1.7 | 2-7% gotowki chlopa w roku poboru | zgodne co do rzedu |
| kupiec miasta (marza na odsprzedazy, ponad 1 191) | ok. 120 | (ok. 12% ceny) | - | woznice i kupcy 5-15% ceny (Galloway: 3.3-9 d za kwarter) | **zgodne** [K] gra `DefaultTradeItemPriceFactorModel.cs:32, 159-168` |
| brama i targ miasta | 0 | 0 | 0 | ponizej 1% | zgodne |
| **w nicosc:** 15% utargu BK 140, zloto rozbitych taborow 70, towar rozbitych taborow po lupie 55, zaplata lorda za jedzenie we wsi 19 | **284** | **24** | - | 0 - rabunek byl lupem rabusia | **zle** (Z1: kazda moneta ma odbiorce) |
| nie dojechalo i niewyjasnione (zapasy, rabunek, zatkane magazyny) | 110 | 9 | - | straty przewozu male | do sprawdzenia (lancuch 9) |

Kod ujsc [K]:
- BK `EconomyPatches.cs:1096-1099` (`PartyTradeGold = 0` po oddaniu 70% i 15%); stawka 70% to BK `BKTaxModel.cs:313-325` (polityka podatkowa wsi: niska 50%, zwykla 70%, wysoka 90%);
- gra `DefaultBattleRewardModel.cs:196-208`: pokonana partia bez bohatera oddaje zwyciezcy 10% zlota (banda 50%), reszta znika z partia;
- towar taboru: gra najpierw oddaje towar handlowy zwyciezcy (`MapEvent.cs:1595-1630`) i pomija ten krok tylko, gdy wygrywaja same zalogi lub milicja (`:1635`);
  nasza ksiega liczy to, co zostalo w partii **po lupie** albo przy rozwiazaniu taboru bez bitwy (`GoodsLedger.cs:755-758`) - 55 zl to prawdziwa strata towaru;
- gra `SellItemsAction.cs:80-86` (wies oddaje 100% prowizji od zakupu lorda).

### 1.3 Struktura dochodu pana: gra wobec historii

| Skladnik | Pan zamku w grze [P] | Historia [H] (Campbell 2005, tab. 3; rachunki dworow) |
|---|---|---|
| czynsze i folwark | **438-554 zl/dobe** z utargu wsi (stawka BK 70% obejmuje dzis folwark, czynsze w naturze i prawa panskie - te same pieniadze chlopow, KIESA 1.2) | magnaci: czynsze 49%, folwark 27%; gentry 47% / 45%; Kingham 1279: folwark 43%, czynsze 46% |
| reszta gotowki chlopow (renta 20% kiesy wsi dziennie) | 167-210 | brak odpowiednika - to zabieranie chlopu ostatniego grosza |
| prawa panskie (mlyn, sad, targ, wpisowe) | 0 jako osobna linia (dzis w 70% BK) | magnaci 24%, gentry 9%; ok. 4 d na glowe rocznie; Kingham: mlyn 3%; Chepstow 1433: sady 10 z 15 L |
| sam zamek (mury) | **0**; kasa zamku kasuje brutto ok. 711 zl/dobe, netto ok. 275-300 prawdziwych pieniedzy (3.2) | **0** - "the castle is worth nothing yearly" (IPM Chepstow 1433, Usk 1425/1432, Dunster, Carisbrooke) |
| miasto | 0 (pan zamku nie ma miasta) | farma miasta 1-5% dochodu miasta; pan swiecki zyl w ok. 3/4-9/10 z ziemi |
| **razem z ziemi** | **640-680** (mediana 320-410) = ok. 970 L rocznie | baron 263 L (173 zl/dobe), par 1436 865 L (570), hrabia 1 610 L (1 061) |

Po 173 jedna nazwa dla kazdej czesci (Z15-4): **udzial pana w utargu = czynsze i folwark**, **oplaty stale z kiesy wsi = prawa panskie** (mlyn, sad, wpisowe).

Wniosek: **sklad dochodu pana zamku jest historycznie prawdziwy** - zyje z ziemi, a zamek nic nie daje. Ale **z tej ziemi bierze za duzo** (68% utargu i reszte gotowki),
chlopom nie zostaje nic, a miasta (kasa kupcow) daja panom swiata 68% dochodu z ziemi, choc historycznie dawaly kilka procent.

---

## 2. Czy bilans i proporcje maja sens - werdykt z liczbami

### 2.1 Ile pan powinien miec, a ile ma (pan zamku, zl na dobe)

Historia przeliczona na okreg zamku [S z H]: utarg 1 007 zl/dobe, do wsi dochodzi 935; ludzie wsi BK ok. 3 760, ludzie zamku ok. 2 760 (panu nic nie placa).

| Miara | Ma (gra) [P] | Powinien (historia) | Werdykt |
|---|---|---|---|
| udzial w gotowce wsi | **68% utargu** + renta z reszty: ok. 85-100% gotowki, ktora zostaje chlopom | pan + Kosciol **30-50% gotowki wsi**: pan ok. **230-420 zl** z oplatami (decyzja Jeffa 15) | **za duzo o 1/3-1/2**; obnizyc razem z kiesa ludu (Z15-0: ok. 340) i wyrownac przez korone (Z15-5) |
| dochod z ziemi w monecie | **640-680** (mediana 320-410) | ranga barona-para: **173-570**; bogaty baron do 660 | **wlasciwy dla rangi**; po Z15-0 ok. 340 - dalej baron-par |
| dochod z ziemi na glowe ludzi wsi (BK) | **ok. 64 zl rocznie** (660 x 364 / 3 760) | panowie swieccy **14.3 d** (Kosciol i korona maja po 173 wlasne linie) | **ok. 4.4 x** [S]; po Z15-0 ok. 33 zl = 2.3 x. Miara pomocnicza - zalezy od skali BK (2.3, "dwie skale"); glowna jest udzial w przeplywie (`zamki/historia.md` 1) |
| prawa panskie (mlyn, sad, targ) | 0 osobno | 9-24% dochodu, ok. 4 d na glowe rocznie; placa ci sami chlopi | po 173: oplaty stale **ok. 40 zl** na okreg (4 zl x ok. 3 650 wolnych glow wsi / 364) jako osobna linia (Z15-4) |
| sam zamek | 0; kasa kasuje brutto 711, **netto ok. 275-300** | **0** | budynek OK; kasowanie to blad (B1, paczka 110) |
| wojsko wojenne trzymane caly rok (T9) | 525 ludzi = **3.1 x ziemi** (mediana 5 x) | wojsko w polu 40-80 dni w roku | T9 mial wojne u 94-97% rodo-dob - **pomiaru pokoju nie ma**; rzetelne liczby w dwoch nastepnych wierszach |
| wojsko w roku wojny, z wlasnej kieszeni | ok. **1.65 x ziemi** (zold minus zwrot korony) | **0.25-0.55 x**; reszte placil krol | **3-7 x za duzo** |
| wojsko w pokoju po 166 [R/S] | **110-135 ludzi** (rozdz. 5) | **20-50 ludzi** za 10-25% dochodu (straz 4-30 + swita) | **2-7 x** - cena decyzji Jeffa (pokoj 35-40% wojny, zalogi polowa) |
| ludzie pod bronia / ludnosc pana | **8%** przez caly rok | swita 1-3%, tylko na sezon | 3-8 x |
| chlopom zostaje | **0** | 50-70% swojej gotowki | zle |
| znika po drodze | **24%** wartosci | 0 | zle |
| udzial okregow zamkow w dochodzie z ziemi panow | **14.5%** (miasta 68%) | wies 3/4-9/10 dochodu pana swieckiego | **odwrocone** na poziomie swiata |

Zrodla historyczne [H]:
- Campbell 2005 (18-23% netto z rolnictwa dla wszystkich panow; struktura tab. 3; IPM: baron 263 L, hrabia 1 610 L; panowie swieccy 14.3 d na glowe);
- raport 01 2.2 (30-50% gotowki rodziny chlopskiej na wszystkie obciazenia);
- Gray 1934 (par 1436, 865 L);
- swity: Jan z Gandawy 173 retainerow, ok. 10% dochodu; Lancaster 1313-14 ok. 20%;
- zalogi w pokoju (zamki krolewskie, tylko jako rzad wielkosci zalogi): Bristol (odzwierny i 2 straznikow), Conwy (35 ludzi za 190 L);
- 1415: baronowie 60-90 ludzi, hrabiowie 210-400; zold od krola po 40 dniach sluzby (`historia.md` 5.1-5.3).

### 2.2 Bilans jednej doby pana zamku dzis [P] (raport 12, 3.1)

| Wplywy | zl/dobe | Wydatki | zl/dobe |
|---|---:|---|---:|
| ziemia (podatek wsi 438 + renta 203; zamek 0) | **641** | zold partii + zalog (517 ludzi) | **1 976** |
| zwrot polowy zoldu od korony (z kurczacego sie zapasu - B5) | 917 | karawany, rada, inne linie, powinnosci | 394 |
| zdarzenia (lup, statki; ok. 86% z niczego) | 1 669 | jedzenie, sprzet, werbunek, handel, raty | 1 351 |
| kasy partii, trzecia, inne | 279 | | |
| **razem** | **3 506** | **razem** | **3 721** |

Saldo wynosi -215 zl/dobe (mediana -586). 83 ze 118 panow traci kiese.
**Z prawdziwego dochodu (ziemia) pan placi 32% zoldu.** Reszte lata zloto z niczego (ok. 1 450/dobe) i zapas korony (917).

### 2.3 Werdykt

- **Ma sens:**
  - dochod w monecie: ranga barona-para;
  - sklad: pan zyje z ziemi;
  - "zamek 0": tak samo w spisach majatku.
- **Nie ma sensu:**
  - (0) **pan bierze ze wsi za duzo**: 68% utargu i prawie cala reszte gotowki chlopow, historycznie z Kosciolem 30-50% (decyzja Jeffa 15). **Pan powinien dostawac mniejsza czesc tego, co wytwarza jego wies, a nie wieksza;**
  - (1) proporcja "dochod -> wojsko": wojsko wojenne przez caly rok, 3 x dochod z ziemi. To glowna przyczyna bankructw, tak samo jak w raporcie 12;
  - (2) chlopom zostaje 0;
  - (3) 24% wartosci znika;
  - (4) kasa zamku to dziura, nie skarbiec (netto ok. 275-300 zl/dobe);
  - (5) pieniadze jego zolnierzy i marze na jego towarze zbieraja miasta (rozdz. 3).
- **Dwie skale** (raport 00 2.2): ludzie i pieniadze okregu sa w skali baronii (6 500 ludzi), a wojsko w skali ksiegi (130 tys. ludzi, "wojsko 1:1" - decyzja Jeffa 05.10).
  Tej luki nie zamknie zadna poprawka dochodu ziemi. Zamyka ja to, co historycznie: **korona placi wojne z podatkow od wszystkich** (165 + podatek wojenny 173, decyzja C) oraz **pulap wojska wedlug dochodu** (166).
- **Odpowiedz na Twoje pytanie:** panowie zamkow nie bankrutuja dlatego, ze dostaja za malo ze swojej produkcji - biora jej nawet za duzo. Bankrutuja przez wojsko wojenne przez caly rok
  i przez ucieczki wartosci. Obnizka do 30-50% jest sluszna, ale w grze przenosi pieniadze do miast, wiec trzeba ja wyrownac (3.5, Z15-5).

---

## 3. Gdzie wartosc ucieka od pana i czy pan zamku jest naprawde baza produkcyjna

### 3.1 Dokad idzie wartosc towaru okregu (zl na dobe na okreg) [P/S]

| Dokad | zl/dobe | Co to jest | Ucieczka? | Naprawa |
|---|---:|---|---|---|
| nicosc: 15% utargu (BK) | 140 | `EconomyPatches.cs:1099` | tak | 112 (K7, gotowa) |
| nicosc: zloto rozbitych taborow (90%) | 70 | `DefaultBattleRewardModel.cs:204-207` | tak | **Z15-3** |
| nicosc: towar rozbitych taborow | 55 | to, co zostalo w partii po lupie albo przy rozwiazaniu taboru bez bitwy (`GoodsLedger.cs:755-758`) | **tak - prawdziwa strata towaru**; Z15-3 jej nie zamyka | 164 (pomiar: czemu towar zostaje - zwyciezca-zaloga/milicja `MapEvent.cs:1635` czy rozwiazanie bez bitwy) |
| nicosc: prowizja wsi od zakupu lorda | 19 | `SellItemsAction.cs:80-86` | tak | 112 |
| majatki BK (notable) | 100 | udzial sily roboczej majatku | nie (prawdziwy wlasciciel) | - |
| kupiec miasta: marza na odsprzedazy | ok. 120 | kara handlowa 6% w kazda strone | nie (historycznie 5-15%) | - |
| **miasta: wydatki zolnierzy pana** | **ok. 900** (60% zoldu druzyn 1 517) | sakiewki ludzi -> kasy miast [P "Obieg"] | **przeplyw, nie ucieczka** - ale przez zawor trafia do **pana miasta** | 163 (markietani: 15% we wsi), 173 |
| **miasta: zakupy pana** (jedzenie ok. 250, handel, odziez, czesc sprzetu) | **ok. 500-800** [S] | do kas miast pewnie idzie ok. 40% "reszty" wydatkow (handel 92, odziez 18, warsztaty zbrojne 14 tys./dobe swiata i czesc sprzetu partii); werbunek idzie do notabli, raty do Banku, sprzet zalog do kasy zamku, a 27-36% "reszty" jest niewyjasnione (`pomiar.md` 5) | jw. | 166 (pulap sprzetu i werbunku) |
| korona | 7 | danina z kiesy wsi | nie | 165 |

### 3.2 Kasa zamku (srednio na zamek i dobe, 130 zamkow) [P] (lancuch 5)

| Kasa zamku | zl/dobe | Platnik -> odbiorca | Kod |
|---|---:|---|---|
| + zold zalogi | 464 | pan zamku (jego wlasne pieniadze) -> kasa | [K] `SoldierPay.cs:374-385` |
| + "zakupy" ludzi zamku (konsumpcja BK) | 436 | **z niczego** -> kasa | [K] BK `BKSettlementBehavior.cs:686-691`, `EconomyPatches.cs:651` |
| + sprzet AI kupiony w zamku | 141 | lordowie (w tym pan za swoja zaloge) -> kasa | [K] `AiGear.cs:170-196` |
| + budowy, BK rynek wsi, dosypka regulatora | 48 | pan / wsie / z niczego | [P] |
| - **regulator kasuje** | **-711 brutto** | kasa -> nicosc; **netto ubywa ok. 275-300 prawdziwych pieniedzy** (711 minus 436 dosypanych z niczego), glownie zold wlasnej zalogi | [K] -> gra `DefaultSettlementEconomyModel.cs:75-79` |
| - do kas miast (inne moduly ticku), odziez, tabory, karawany | -326 | kasa zamku -> miasta, wsie | [P] |
| do pana zamku | **0** (cla 0.1) | - | rozdz. 6 lancucha |

Paczka 110 cofa "zakupy" z niczego w zamku i zeruje regulator tylko w dol (raport 12 D-1). Pan odzyskuje wtedy **ok. 270-310 zl/dobe** - tyle, ile regulator netto kasowal,
w ok. 90% to jego wlasny zold zalogi i dwor. Dlatego ta kwota nie moze podnosic jego budzetu (W-1).

### 3.3 Bilans okregu zamku z miastami [S z P]

- **Okreg sprzedaje miastom** plon za ok. **1 000 zl/dobe**, z czego pan dostaje 680.
- **Same zycie zolnierzy pana zostawia w miastach ok. 900 zl/dobe** [P "Obieg"] - prawie tyle, ile miasta placa za caly plon okregu. Z zakupami pana (ok. 500-800 [S]) razem **ok. 1 400-1 700 zl/dobe** [S].
  Wszyscy panowie zamkow razem: ok. 106 tys. zl/dobe od zolnierzy, wiecej niz cala ich ziemia (75 tys.) [P lancuch 4.2].
- **Roznica ok. 400-700 zl/dobe** [S] wraca dzis do pana tylko przez zwrot korony z zapasu i przez zloto z niczego. W miescie z kasy kupcow 7% nadwyzki dziennie idzie do **pana miasta**.
  Dlatego panowie miast (20% ludzi) dostaja 68% dochodu z ziemi wszystkich panow.

Historycznie wojsko pana tez wydawalo w miastach. Ale **zold placil krol** z podatku od ruchomosci calej ludnosci (subsydium 1334: miasta 1/10, wies 1/15) i z cla od welny [H] (raport 09 2.6, `dochody/historia.md` 2.4).
Mieszczanie zarabiali na wojsku, a pan miasta dostawal z tego farme (1-5% dochodu miasta) [H] (fundament H6).

### 3.4 Czy pan zamku jest w grze baza produkcyjna?

**Fizycznie tak, gospodarczo nie.** Okregi zamkow wytwarzaja 46% wartosci towaru wsi (ok. 156 tys. zl/dobe) [P/S]. Ale:
1. **Zamek nie ma rynku, warsztatow ani ludzi z pieniedzmi.** Wozy wsi zamkowych jada do miasta (wpis 100, `MarketRoad.cs:13-29`), wiec zamek nie ma cel ani targu (0.1 zl/dobe).
   Ludzie zamku BK (2 760) "kupuja" za zloto z niczego (436/dobe), a regulator to kasuje [K] (lancuch 5).
2. **Popyt na plon okregu jest w polowie fikcyjny.** 56% utargu wsi zjadaja mieszczanie BK, ktorzy placa miastu zlotem z niczego [K] BK `EconomyPatches.cs:622, 651`.
   Po zamknieciu tego zrodla (K6) bez kiesy ludu miasta moga kupic mniej plonu - wtedy **pierwsi straca panowie zamkow** (lancuch L-6; liczba w Z15-8).
3. **Zywnosc zamku jest juz "z domeny", bez zlota.** Spichlerz zamku zasilaja przypisane wsie: (poziom hearth + 1) x 6 zywnosci dziennie z kazdej [K] gra `DefaultSettlementFoodModel.cs:66-74`.
   Dochodzi do tego 15% plonu ziemi zamku [K] BK `BKFoodModel.cs:66-71`. Zaloga je ze spichlerza za darmo (1 na 20 ludzi, `DefaultSettlementFoodModel.cs:48`).
   To odpowiednik sredniowiecznej daniny w naturze. Druzyna pana kupuje jednak jedzenie w miastach.
4. Okreg zachowuje sie jak **dostawca surowca**. Sprzedaje plon tanio (miasto bierze marze ok. 12%), a "kupuje wojne" (zold wydany w miastach, sprzet).
   Baza produkcyjna jest prawdziwa, ale korzysc z niej - marze kupcow, zawory miast i pieniadze zolnierzy - zbieraja miasta.

### 3.5 Kiesa ludu (173) i Twoje 30-50% przesuwaja pieniadze od panow zamkow do miast [K][R]

Co zmienia 173 razem z decyzja Jeffa 15 (Z15-0):
- pan bierze z utargu **35%** zamiast 70% (z tego majatki BK swoj udzial), sept 5%, wies zatrzymuje **60%**;
- renta 20% kiesy wsi dziennie ustepuje **oplatom stalym 4 zl na wolna glowe rocznie** (KIESA tab. 3 w. 19) - na okreg zamku ok. 40 zl/dobe;
- wies wydaje swoja gotowke w miescie (sol, zelazo, sukno, piwo) - czyli w kasie kupcow, z ktorej 7% nadwyzki idzie zaworem do pana miasta i korony.

Pan zamku na dobe w skali T9 [S]:
- dzis z ziemi ok. 680; po 112 i 163 (przed 173) ok. 870 w pokoju i ok. 1 070 w wojnie (BK 554 + renta z 30% utargu, w wojnie takze z markietanow);
- po 173 + Z15-0: **ok. 340** (35% z 935 bez udzialu majatkow ok. 277 + oplaty stale ok. 40 + 3% od podzamcza ok. 25);
- traci wiec ok. 340 wobec dzis i ok. 530 / 730 (pokoj / wojna) wobec stanu tuz przed 173. Historycznie to sluszne - chlop zatrzymuje swoja gotowke. Ale chlop wydaje ja w miescie.

Rachunek kiesy ludu (`kiesa\sim2\sim_kiesa2.py`, model PROJEKT, 324 rody, 119 panow samych zamkow, rok 1; **z dziesiecina** `temples=True`; podzial wedlug rodzajow rodow w pamieci) [R]:

| Wariant | Wojsko swiata W / F / P (tys.) | Panowie zamkow: ludzie W / F / P (tys., druzyny + zalogi) | Zmiana W / P | D mediana W / P | Kiesa mediana W: d364 / d728 (tys.) | Pan + sept w gotowce wsi | Panowie miast: druzyny W / P (tys.), D mediana W | Krolowie: druzyny W / P |
|---|---|---|---|---|---|---|---|---|
| bez kiesy ludu (PROJEKT) | 95.6 / 79.1 / 36.1 | 47.9 / 40.0 / 21.2 | - | 4 237 / 3 425 | 169 / 150 | ok. 100% (renta 20%) | 39.5 / 14.8, 10 728 | 19.8 / 7.0 |
| kiesa ludu jak w projekcie (pan 65% + sept 5%) | 90.6 / 74.9 / 31.5 | 43.3 / 35.9 / 17.2 | -10% / -19% | 3 571 / 2 611 | 92 / 78 | 62-69% | 39.5 / 14.2, 10 677 | 19.4 / 6.6 |
| **kiesa + Z15-0 (pan 35% + sept 5%), bez wyrownania** | 88.7 / 72.8 / 29.3 | **41.4 / 33.8 / 14.4** | **-14% / -32%** | 3 282 / 2 354 | **76 / 64** | **36-41%** | 39.6 / 14.7, 10 867 | 19.3 / 6.5 |
| ... + zawor miast 1/2, renty miasto 2 / zamek 1 / wies 0.25 | 92.8 / 76.9 / 31.7 | 46.0 / 38.4 / 18.6 | -4% / -12% | 4 134 / 3 107 | 137 / 117 | 36-41% | 39.4 / 13.5, 10 133 | 19.0 / 6.3 |
| **... + Z15-5: zawor 1/2, renty miasto 2 / zamek 1.5 / wies 0.25** | **93.5 / 77.8 / 32.3** | **47.4 / 39.6 / 20.3** | **-1% / -4%** | **4 417 / 3 503** | **164 / 147** | 36-41% | 39.1 / 12.7, 9 743 | 18.7 / 6.2 |
| ... + renty zamek 2 (dla porownania) | 93.8 / 78.1 / 32.5 | 48.2 / 40.4 / 21.3 | +1% / 0% | 4 604 / 3 749 | 189 / 175 | 36-41% | 38.7 / 12.0, 9 306 | 18.5 / 6.1 |

W = wojna caly rok, F = fale (180 dob wojny / 120 pokoju), P = pokoj caly rok. Udzial "pan + sept w gotowce wsi" = (podatek wsi + oplaty stale + dziesiecina) / (utarg + markietani) w dobie 364:
pan 32-36% + sept 4-5%. We wszystkich wariantach nie ma zaleglego zoldu, a glow rodow ponizej 5 000 jest 2 (W, P) albo 4 (F) - tyle samo co bez kiesy ludu.

Wnioski z tabeli:
- **Kiesa ludu z Twoimi 30-50% bez wyrownania kosztuje prawie tylko panow zamkow:** -14% ludzi w wojnie, -32% w pokoju, kiesy topnieja z 169 do 76 tys. w rok;
  ich udzial w stalym dochodzie D swiata spada z 32% do 26% (W). Panowie miast nie traca nic (druzyny i D bez zmian).
- **Z15-5 przywraca panow zamkow do -1% / -4%** i ich udzial w D do 33% (W) / 36% (P). Placa (wobec wariantu bez wyrownania) panowie miast: suma D -9% W, -15% P, druzyny w pokoju -14%; i krolowie: D -7% / -10%.
- Wojsko swiata z Z15-5: W -2%, P -10.5% wobec stanu bez kiesy ludu - pokoj to 35% wojny (32.3 / 93.5), na dolnej granicy Twojej decyzji 35-40%. Pieniadze, ktorych brakuje rodom, sa w kiesach ludu (rozdz. 4.3 KIESA).

**Sprostowanie do `zamki/lancuch.md` (L-5):** "kiesa ludu sama da ok. +30% zakupu plonu (374 wobec 283 tys./dobe)" jest nieprawda.
374 tys. to **staly plon zalozony w rachunku** PROJEKT ("plon wsi staly 374 tys. dziennie (bez K13)", `PROJEKT-EKONOMIA-OBIEG` 11.1), wziety z autotestu TOWARY 3 (doba 40).
Kiesa ludu zapewnia platnika za caly plon, ale sama go nie zwieksza.

---

## 4. Propozycje (liczby, platnik -> odbiorca, paczka)

Zgodne z raportem 12 (B1-B18, D-1..D-13, W-1..W-11), z kiesa ludu (173) i budzetem 166, **z dwiema roznicami powiedzianymi wprost**: Z15-5 zastepuje D-12 (wsie po decyzji 15
placa panom mniej, nie wiecej), a Z15-8 zmienia kolejnosc z 12 rozdz. 4.4 (K6 dopiero ze 173). Liczby "na okreg" i "na pana zamku" to [S] w skali T9.

| # | Zmiana | Ile | Uzasadnienie | Platnik -> odbiorca | Paczka / zgodnosc |
|---|---|---|---|---|---|
| **Z15-0** | **NOWE - decyzja Jeffa 15: pan 35% utargu wsi, sept 5%, wies 60%** (polityka podatkowa BK: niska 25%, zwykla 35%, wysoka 45%; z septem 30 / 40 / 50%) | pan + sept **36-41%** gotowki wsi [R] (przy niskiej polityce ok. 28-31%, przy wysokiej 45-50%); pan zamku z ziemi ok. **340/dobe** zamiast 680 | pan z Kosciolem 30-50% gotowki wsi [S z H] (01 2.2); decyzja Jeffa 15 | kasa miasta (zakup plonu) -> tabor -> pan 35% (z tego majatki BK swoj udzial) / sept 5% / kiesa wsi 60% (-> towar w miescie, 173) | **173**: postfiks na BK `BKTaxModel.CalculateVillageTaxFromIncome(Village, int)` x 0.5 (`BKTaxModel.cs:313-325`) + 112 (reszta do wsi, bez 15% w nicosc). **Do 173 zostaje 2/3** (przejscie, decyzja 15): bez kiesy ludu renta 20% i tak oddaje panu reszte gotowki wsi, wiec sama obnizka nic by nie dala. Gracz - ta sama regula (polityke wybiera sam). AI BK zmienia polityke wsi co tydzien wedlug hearth, gdy miasto ma zarzadce (`BKSettlementBehavior.cs:401-428`) - w autotescie liczyc udzial z kazda polityka |
| **Z15-1** | **Zawor zamku zamiast kasowania** (= D-1): regulator zamku zerowany tylko w dol, "zakupy" z niczego cofniete, 7% nadwyzki ponad zapas; podzial 2/3 pan, 1/3 skarbiec (klucz z 114); po 173 najpierw 15% dla podzamcza | regulator kasuje brutto 711/dobe, **netto ok. 275-300 prawdziwych**; pan **+270-310/dobe**, w ok. 90% wlasny zold zalogi - zaloga w zamku kosztuje go ok. **63%** zoldu | zamek byl kasa i magazynem pana; zold zalogi wydany na miejscu nie znika | kasa zamku (zold zalogi, sprzet, dwor) -> pan 2/3, skarbiec 1/3 (podzamcze 15% po 173) | **110** + klucz 114; D bez wlasnych pieniedzy (W-1); poprawic opis `PopulationLaw.cs:26-28` ("Zamki bez zmian" - nieprawda: `:157-171` zeruje tez zamek) |
| **Z15-2** | **15% utargu zostaje we wsi, zakup lorda we wsi bez 100% prowizji** (= D-2) | +160/dobe do kiesy wsi okregu; do 173 wraca panu renta (ok. **+130-160**); **po 173 zostaje chlopom** (kupuja w miescie) | chlop sprzedaje plon i z tego placi czynsz | kasa miasta -> tabor -> kiesa wsi (-> pan do 173) | **112** (gotowa); zysk pana z 112 i 163 jest przejsciowy - znika przy 173 (3.5) |
| **Z15-3** | **NOWE: zloto pokonanego taboru wsi i bandy do zwyciezcy w calosci** (tabor 10% -> 100%, banda 50% -> 100%); **karawany osobno** (Q3b) | nie znika ok. **70/dobe na okreg**; swiat [P, doby 31-120]: tabory 19.9 tys./dobe, bandy 2.6 tys. (karawany 15.1 tys. - osobno); tabor wiezie przy rozbiciu srednio ok. 1 600 zl | napadniety woz tracil wszystko na rzecz rabusia; w zamknietym obiegu zloto ma odbiorce | kiesa taboru (utarg wsi) -> zwyciezca: lord 8/9, **korona 1/9** (decyzja Jeffa 18); banda -> paser -> kasa miasta | **164**; postfiks na `DefaultBattleRewardModel.CalculatePlunderedGoldAmountFromDefeatedParty` (gra `:196-208`) z wyjatkiem karawan. **Ryzyko:** 77% taborow rozbijaja bandy - ich zysk z taborow rosnie ok. 10 x (ok. 17 tys./dobe zamiast 1.7), a bandy kupuja za zloto zbroje i konie u pasera, zeby awansowac (`OutlawLaw.cs:1161-1177`, `OutlawGearUpgrades = true`, `Settings.cs:592`). Silniejsze bandy rozbija wiecej taborow, wiec "neutralne dla pana zamku" nie jest wykazane - autotest (rozdz. 5). Dotyczy gracza - Q3 |
| **Z15-4** | **Prawa panskie = oplaty stale z 173** jako osobna linia w logu: wies 4 zl na wolna glowe rocznie (mlyn, sad, wpisowe); **35% utargu = czynsze i folwark**; podzamcze 3% zarobku | ok. **40** (wies) + ok. **25** (podzamcze) zl/dobe na okreg | prawa panskie 9% (gentry) - 24% (magnaci) dochodu; ok. 4 d na glowe rocznie; Kingham: mlyn 3% dworu [H] | kiesa wsi -> pan; kiesa podzamcza -> pan | **173** bez zmian stawek. **Czesc od podzamcza poza D (W-1):** ok. 2/3 dochodu podzamcza to wlasne pieniadze pana (zaloga 15, dwor 23, zawor 27 z 105 tys./dobe w wojnie - KIESA 2.2). Nic ponad to: mlyn i piec w miescie sa juz w cenie chleba (KIESA L5) |
| **Z15-5** | **Wyrownanie dla panow zamkow - przesuniecie od panow miast przez korone** (mechanizm gry: kiesa ludu i Z15-0 przenosza pieniadze chlopow do kas miast). Przy 173: (a) **zawor kupcow miast: pan 1/2, korona 1/2** (projekt 2/3 / 1/3); (b) **renty korony ze stalymi wagami wedlug rodzaju lenna: miasto 2, zamek 1.5, wies 0.25** (projekt 3 / 1 / 0.25) - **nigdy wedlug faktycznej zalogi**; warunek sluzby z D-4 zostaje | panowie zamkow W -1%, P -4% zamiast -14% / -32%; ich D +4% / +2%; kiesy stabilne (164 / 147 tys.); wojsko swiata W -2%; panowie miast D -9% / -15%, krolowie -7% / -10% (wobec kiesy bez wyrownania) [R] (3.5). Renta na udzial (srednia swiata, rok 1) ok. 940 zl/dobe zamiast ok. 630-690 | **gra, nie historia** (D-4: renty to mechanizm gry). Waga zamku 1.5 dobrana tak, zeby D panow zamkow po 173 bylo jak bez kiesy ludu (+4% / +2%); waga 1 zostawia -4% / -12%, waga 2 daje wiecej niz bez kiesy. Pan miasta przy 1/2 dalej ma wiele razy wiecej niz historyczna farma (1-5%) | wydatki chlopow w miescie (173) -> kasa kupcow -> zawor (korona 1/2) -> skarbiec -> renty wedlug stalych wag -> pan zamku | **173 + 165** (ta sama nowa kampania). Renta od faktycznej zalogi to maszyna do pieniedzy (K2 w PROJEKT l. 119 i 360: gracz odzyskuje 1.43-2.88 z monety) - stale wagi tego nie robia. Zawor 1/2 byl rozwazany i uznany za zbedny **dla bankructw** (PROJEKT l. 248, 377); tu chodzi o podzial miedzy panow, a Z8 trzyma sie: gracz odzyskuje 0.40-0.49 z monety (`gracz_kiesa2.py` w pamieci) - zawsze < 1. **Nie jest to D-12:** D-12 zakladalo, ze wsie zaplaca panom wiecej; po decyzji 15 placa mniej. Dotyczy gracza - Q1 |
| Z15-6 | (opcja, Q2) **targ podzamcza**: woz wsi zamkowej najpierw sprzedaje swojemu zamkowi tyle jedzenia, ile podzamcze zje w 10 dni (po cenie wsi), reszte i surowce wiezie do miasta | ok. 370 zl/dobe jedzenia na zamek nie robi drogi wies -> miasto -> zamek; pan ok. **+25-35/dobe** [S] (marza ok. 44 zl zostaje w kasie zamku, pan dostaje ja zaworem: 85% x 2/3); mniej rozbitych wozow; podzamcze mniej czule na drozyzne (KIESA 4.4: przy cenach 1.3 podzamcze ma 57-73% koszyka) | zamki mialy targi tygodniowe i jarmarki z przywileju; okreg jadl swoj chleb [H] | kiesa podzamcza -> kasa zamku -> tabor -> pan 35% / sept 5% / wies 60% | 173 (zakupy podzamcza); "cena wsi" wymaga osobnego mechanizmu ceny; **przed wdrozeniem sprawdzic Z8 gracza** (wlasna zaloga, podzamcze, jedzenie z wlasnych wsi); zmienia decyzje z 05.10 (wpis 100) tylko dla jedzenia podzamcza |
| Z15-7 | **Hamulec wojska i korona** (bez zmian wobec 12): 165 + 166 + 168 (E'), W-1..W-9; podatek wojenny korony (decyzja C) ok. 100 tys. zl/dobe w wojnie | pan zamku: zold / D w wojnie 0.60, w pokoju 0.28 (W-2) | pan placil swite 40-80 dni, potem krol z podatku [H] | lud (173) -> skarbiec -> zwrot 50% zoldu i renty -> pan | **165, 166, 168, 173**; z Z15-5 potrzeba podatku wojennego "zeby bylo jak dzis" spada [S] |
| Z15-8 | **Kolejnosc - zmiana wobec raportu 12 rozdz. 4.4** | K6 przed 173: [R] model PROJEKT (stan po K6, bez kiesy ludu) kupuje 100% plonu, wiec panowie zamkow nie traca; [S] w grze kazde 10% mniej kupionego plonu to ok. -65 zl/dobe u pana zamku (ok. -4% D S3, ok. -15-20 ludzi w wojnie) | lancuch L-6: 56% utargu wsi placa dzis mieszczanie BK zlotem z niczego | - | (1) E' z raportu 12 (165+166+168+110+112+162 minimalny) - konczy fale bankructw; (2) 164a bez statkow + **Z15-3**, 108/109, 164b, 162 pelny, 163, reszta 114, 164c; (3) **K6 (111') przeniesione z kroku 2 raportu 12 do kroku z 173** - albo zostaje w kroku 2 tylko z autotestem "plon kupiony >= 95%"; (4) **173 + Z15-0 + Z15-5** i podatek wojenny; (5) statki (D-11) po 166 |

Z raportu 12 bez zmian, takze dla panow zamkow:
- D-8 - pensje rady od tego, co pan naprawde dostaje (-44 zl/dobe wydatkow);
- D-9 - karawana pana bez miasta oddaje zysk w najblizszym miescie krolestwa (+60-120);
- D-13 - regale gornicze (do pomiaru).

Z raportu 12 zmienione:
- **D-4** - wagi rent 3 / 1 / 0.25 -> 2 / 1.5 / 0.25 (od 173, razem z kiesa ludu; do 173 zostaja jak w 12);
- **D-12** ("zawor miast w dol o tyle, ile wsie zaplaca wiecej") traci warunek po decyzji 15 - zastepuje go Z15-5.

### 4.1 Rozwazone i odrzucone

| Pomysl | Liczba | Dlaczego nie |
|---|---|---|
| Przywrocic podatek BK od ludnosci zamku | 650-850 zl/zamek/dobe [S lancuch 5] | w calosci z niczego. Po 173 caly zarobek podzamcza to ok. 810 zl/zamek/dobe w wojnie (105 tys. / 130 [R KIESA 2.2]), wiec podatek BK zjadlby 80-100% zarobku. Zastepstwo: 3% (Z15-4) |
| Zostawic po 173 pan 65% + sept 5% (2/3 gotowki wsi) | panowie zamkow -10% / -19% bez wyrownania [R] | wbrew decyzji Jeffa 15 i historii (30-50%); 2/3 tylko przejsciowo do 173 |
| Podniesc udzial pana w utargu albo rente z kiesy wsi | - | pan juz bierze 68% utargu i ok. 85-100% gotowki chlopow; historycznie z Kosciolem 30-50% |
| Zostawic przy 173 rente z kiesy wsi (wariant v1 KIESA: 7%/dobe nadwyzki do pana) | +1.7 tys. wojska [R KIESA 4.4] | pan bralby 79-81% gotowki wsi - przeciw decyzji 15 i sensowi kiesy ludu |
| Same wagi rent (zamek 1.5 albo 2) bez zmiany zaworu | zamek 2: panowie zamkow 44.7 / 18.8 tys. (-7% / -11%); zamek 1.5: 43.9 / 17.7 (-8% / -17%) [R] | korona ma za malo do rozdania (renty ok. 360-390 tys./dobe zamiast ok. 500) - zawor 1/2 jest potrzebny |
| Waga wsi w rentach zamiast wagi zamku ("renta wedlug bazy produkcyjnej") | wies 0.5 / 1.0: panowie zamkow 46.2 / 46.4 tys. W (jak przy 0.25) [R] | panowie miast tez maja wsie (ok. 3.2 na miasto), wiec renta wedlug wsi nie trafia w panow zamkow |
| Zawor pana miasta 0.4 przy wagach 2 / 1 / 0.25 | panowie zamkow 47.7 / 20.0 tys. (-0.4% / -6%) [R], panowie miast jak przy Z15-5 | rownie dobre w wojnie, troche slabsze w pokoju; Z15-5 ma okragly podzial "po polowie" - do wyboru, jesli wolisz nie ruszac wag z 12 |
| Renty wedlug faktycznej zalogi | gracz odzyskuje 1.43-2.88 z monety (PROJEKT K2) | maszyna do pieniedzy (Z8) - tylko stale wagi wedlug rodzaju lenna |
| Myto i targowe od cudzych taborow i karawan przy zamku | myto 0.1-0.6% ladunku [H raport 09 2.7]; ladunki na drogach ok. 0.5-0.7 mln zl/dobe [S], wiec ok. 15-30 zl na zamek | historycznie tez male (targowe Bridgwater: 7 s 9 d rocznie [H]). Nie warto kodu teraz (12, 4.3). Wyjatek z lore (przeprawa Blizniakow, Freyowie) - po 164 |
| Osobna "domena" (folwark) pana | - | juz jest: udzial pana w utargu obejmuje folwark (KIESA 1.2), a spichlerz zamku z przypisanych wsi i ziemi zamku karmi zaloge za darmo (3.4, pkt 3). Druga domena liczylaby to dwa razy. Kandydat na pozniej: druzyna pana bierze jedzenie z nadwyzki spichlerza wlasnego zamku (ok. 250 zl/dobe mniej do miast) - dopiero po pomiarze nadwyzek spichlerzy (169) |
| Cel z `lancuch.md` L-5: "pan zamku 1 300-1 600 zl/dobe z samej ziemi" | wymagalby 2.2-2.7 x dzisiejszego utargu okregu przy tym samym udziale pana | brak platnika i wbrew decyzji 15. Poziom D 1 600 osiaga **staly dochod D (ziemia + korona)**: S3 + Z15-0 + Z15-5 ok. 1 620 / 1 390 (rozdz. 5) |
| Udzial okregow zamkow 30-35% "dochodu z ziemi" przez wyzsza cene plonu | - | jw. Udzial panow zamkow w D swiata (suma, [R]): bez kiesy ludu 32% / 34%, kiesa + Z15-0 bez wyrownania 26% / 25%, z Z15-5 33% / 36% (T9: 24% [P]) - przez korone, nie przez cene |

---

## 5. Skutek: ilu ludzi moze utrzymac pan zamku i ile bankructw zostaje

Liczby na jednego pana zamku w skali T9 [S]. Wiersze 1-4 to raport 12 (`sim_dochody2.py`, nowa kampania, rok). Wiersze 5-8 to S3 pomnozone przez zmiany wzgledne z rachunku kiesy ludu [R] (3.5). Zalogi i druzyny sa liczone razem.

| Etap | D pana zamku W / P (zl/dobe) | Ludzi na pana W / P | Na minusie w rok (nowa kampania) |
|---|---|---|---|
| dzis (T9) [P] | D ksiegi 2 095 (ziemia 641) | **517-525** / - | 13 bankrutow ze 118 w 120 dob; 83 traci kiese |
| same poprawki dochodow, bez 166 (S6, bez BEE) [S] | - | 517 | **47 rodow, w tym 13 panow zamkow** - dochody bez hamulca wojska nie wystarcza |
| E' (165+166+168+110+112+162) = E2 [S] | 999 / 716 | **435 / 108** | **0** |
| caly projekt bez kiesy ludu (S3) [S] | 1 550 / 1 359 | 420 / 135 | 0 |
| S3 + kiesa ludu jak w projekcie (65% + 5%) [S z R] | ok. 1 310 / 1 040 | ok. 380 / 110 | 0 (kiesy panow zamkow topnieja: 169 -> 92 tys. w rok) |
| S3 + kiesa + Z15-0 (35% + 5%), bez wyrownania [S z R] | ok. 1 200 / 930 | ok. 365 / 90 | 0 (kiesy 169 -> 76 tys.) |
| **S3 + kiesa + Z15-0 + Z15-5** [S z R] | **ok. 1 620 / 1 390** | **ok. 415 / 130** | **0** |
| ... + podatek wojenny 100 tys. zl/dobe (decyzja C; w 12: panowie zamkow +9.5%) [S] | wyzej w wojnie | **ok. 455 / 130** | 0 |

Ile bankructw zostaje:
- **Nowa kampania:** zadnego rodu na minusie we wszystkich wariantach z budzetem 166. W rachunku kiesy ludu nie ma zaleglego zoldu. Glow ponizej 5 000 zostaje tyle samo co bez kiesy ludu (2; w falach wojen 4) [R].
  Po dwoch latach E2 zostaje 1 rod na minusie (Volentin) [S, 12 5.2].
- **Kampania trwajaca (od doby 120 T9):** na minusie zostaje 6-7 rodow, prawie wszyscy to dzisiejsi bankruci (Harlaw, Royce, Volentin, Marbrand, Banu Ayan, Garner, Banu Nir).
  Po 168 to **zajecie dochodu wsi przez wierzyciela, nie bankructwo** (decyzja Jeffa 05.10: lord nigdy nie traci lenna).

Porownanie z historia (w koszcie, nie w liczbie ludzi - zold w grze 3.8 zl na czlowieka, w 1415 7.9-8.7 d [H]):
- 415-455 ludzi przez caly rok kosztuje ok. 1 580-1 730 zl/dobe, czyli tyle, co ok. **180-220 zbrojnych 1415**: 2-3 x swita lorda-barona (60-90), na poziomie najmniejszej swity hrabiego (Cambridge 210) - i trzymane caly rok, nie 40-80 dni.
- Pan placi z D ok. 0.6 zoldu (W-2), historycznie 0.25-0.55 z wlasnej kieszeni w roku wojny.
- W pokoju ok. 130 ludzi (polowa zalogi i mala druzyna) wobec historycznych 20-50.
- Gra zostaje 2-4 razy ciezsza niz historia. To swiadomy wybor (decyzje Jeffa 05.10 i 08.10: wojsko 1:1, pokoj 35-40% wojny, zalogi polowa). W rachunku z Z15-5 pokoj to 35% wojny (32.3 / 93.5 tys.) [R].

Co sprawdzic autotestem po wgraniu 173 + Z15-0 + Z15-5:
- **udzial pana i septu w gotowce wsi**: 30-50% (osobno dla kazdej polityki BK);
- nowa linia **"prawa panskie"** (oplaty stale wsi, ok. 40 zl na okreg); czesc od podzamcza osobno i poza D (W-1);
- panowie zamkow: zold / D w wojnie ok. 0.6, w pokoju <= 0.3;
- **udzial panow zamkow w D swiata >= 30%** (rachunek: bez wyrownania 26%, z Z15-5 33%; T9 24%);
- **mediana kiesy panow zamkow: zmiana na ostatnie 28 dob >= -2% w wojnie** (rachunek, doby 336-364: bez wyrownania -2.6%, z Z15-5 -0.4%) - prog 10% z wersji 1 niczego nie odroznial;
- po Z15-3: w "Pieniadz swiata (bilans - przyczyny)" "kiesy partii bez wodza, ktore zniknely: tabory" i "bandy" ok. 0; **bandy: sredni tier, zloto, awanse u pasera; rozbite tabory na dobe** (dzis 13.7) - nie wiecej niz +20% wobec przebiegu bez Z15-3;
- podzamcze "stac" >= 95% takze przy drozyznie; zatkane magazyny wsi zamkowych (dzis 5.6% wobec 2.5% wsi miejskich);
- po K6: "plon kupiony" >= 95% oferowanego.

---

## 6. Pytania do Jeffa (tylko zmieniajace rozgrywke)

**Q1. Przy kiesie ludu i Twoich 30-50%: pan miasta dzieli nadwyzke kupcow z korona po polowie (zamiast 2/3 dla siebie), a korona oddaje to rentami stalymi wedlug rodzaju lenna (miasto 2, zamek 1.5, wies 0.25) - takze u Ciebie?**
Bez tego panowie zamkow traca 14% ludzi w wojnie i 32% w pokoju, a ich kiesy topnieja o ponad polowe w rok. Z tym traca 1% i 4%.
Jako pan miasta dostajesz z nadwyzki swojego miasta ok. 1/4 mniej (ok. 1 000-1 400 zl dziennie z jednego miasta: T9 i rachunek [S][R]). Renty korony rosna (na udzial ok. 940 zamiast ok. 630-690 zl dziennie),
wiec przy lennie 1 miasto, 1 zamek, 4 wsie i sluzbie (Twoja decyzja 10) dostajesz ok. 800-1 100 zl dziennie wiecej rent - netto ok. -350..-550 zl dziennie [R, srednia swiata; w malym krolestwie inaczej]. Z monety Twojego zoldu wraca 0.40-0.49, zawsze mniej niz 1.
- (a) **tak, jedna regula dla AI i dla Ciebie** (Twoja zasada z 08.10: wasal-gracz placi koronie jak AI);
- (b) tylko dla AI;
- (c) nie - panowie zamkow beda slabsi po kiesie ludu.

Rekomendacja: **(a)**.

**Q2. Targ przy zamku dla podzamcza: woz wsi zamkowej najpierw sprzedaje swojemu zamkowi jedzenie dla ludzi zamku, a reszte i surowce wiezie do miasta?**
Dzis (wpis 100, Twoja decyzja 05.10) caly plon jedzie do miasta. Po kiesie ludu ludzie zamku kupowaliby swoj chleb przez kase zamku w miescie, czyli wozili go tam i z powrotem.
- (a) jak dzis: wszystko do miasta;
- (b) **zamek kupuje od swoich wsi tylko jedzenie na 10 dni dla podzamcza; rudy i reszty nie kupuje.** Na mapie wozy wsi zamkowych beda czasem jechac do zamku. Panu daje to ok. +25-35 zl dziennie, mniej rozbitych wozow, a podzamcze mniej cierpi przy drozyznie.

Rekomendacja: **(b)**, razem z kiesa ludu (po sprawdzeniu, ze gracz nie zarabia na wlasnym zamku).

**Q3. Lup z rozbitych partii bez wodza (dzis znika ok. 37.6 tys. zl dziennie: tabory 19.9, karawany 15.1, bandy 2.6 [P])?**
- (a) **tabor wsi i banda: zwyciezca bierze cale ich zloto** (dzis 10% i 50%). Dla Ciebie: rozbity tabor da srednio ok. 1 600 zl zamiast ok. 160. Wies traci tyle samo co dzis.
  Bandy dostana z taborow ok. 10 razy wiecej (rozbijaja 77% taborow) i kupia za to zbroje u pasera - autotest pilnuje, czy nie rozbijaja wiecej taborow. Korona bierze 1/9 lupu wasali (Twoja decyzja 18).
- (b) **karawana: zwyciezca 10% jak dzis, a 90% wraca do wlasciciela karawany** (kupcy wloscy od konca XIII w. rozliczali sie wekslami i nie wozili calego kapitalu w gotowce [H?]) zamiast znikac. Karawana startuje z 10 000 albo 17 500 zl ([K] `DefaultCaravanModel.cs:55-59`),
  wiec "cale zloto dla zwyciezcy" dawaloby Ci ok. 10 000 z jednej karawany zamiast ok. 1 000.
- (c) nic nie zmieniac.

Rekomendacja: **(a) i (b)** - zaden pieniadz nie znika, a napad na karawane nie staje sie kopalnia zlota.

Bez pytania (decyzje projektu z uzasadnieniem):
- udzial pana w utargu wedlug Twojej decyzji 15: 35% + sept 5% (polityka BK 25 / 35 / 45%), od 173; do 173 2/3;
- prawa panskie 4 zl na glowe wsi rocznie + 3% podzamcza (czesc od podzamcza poza D);
- brak podatku BK od ludnosci zamku i brak myta (poza wyjatkiem z lore po 164);
- kolejnosc z Z15-8 (K6 razem ze 173 - zmiana wobec 12).

---

## 7. Luki i niepewnosci

- **Rachunek [R]** to model kiesy ludu: podstawa autotest TOWARY 3 doba 40, 324 rody, plon staly 374 tys. zl/dobe, zold na czlowieka 5.0 zl. Ten model rozni sie od T9.
  Z niego biore tylko zmiany wzgledne. Przeniesienie na liczby T9 w rozdz. 5 to [S]. Z15-5 nie byl liczony w `sim_dochody2.py` (raport 12), bo ten model nie ma kiesy ludu.
- Udzial "pan + sept w gotowce wsi" w rachunku to migawka doby 364; w modelu wies ma tylko utarg i markietanow. Polityke BK w rachunku zastepuje jedna stawka (35%).
- **Gracz (Z8)** przy Z15-0 i Z15-5: 0.40-0.49 z monety (`gracz_kiesa2.py` w pamieci: zawor 1/2, pan 35% + sept 5%). Renty i zawor dla gracza w Q1 to srednie swiata z rachunku [R] - w malym krolestwie inne.
- **Strata pana zamku przy 173 + Z15-0 (ok. 340 wobec dzis, ok. 530 / 730 wobec stanu tuz przed 173)** to szacunek z podzialu utargu (lancuch 4.1); w rachunku [R] D panow zamkow spada o 22% (W) i 31% (P).
- **K6 przed 173:** model PROJEKT kupuje 100% plonu po K6, ale ten model nie ma kar handlu gry; w grze zakup plonu zalezy od kasy miasta w chwili wjazdu wozu - stad prog w autotescie.
- **Z15-3 a bandy:** skutek wiekszego lupu dla band (awanse u pasera, wiecej rozbitych taborow) nie jest policzony - tylko autotest.
- **Targ podzamcza (Z15-6)** i jego +25-35 zl/dobe nie byly liczone. Nie wiadomo jeszcze, skad zamek bierze towar dla podzamcza w kodzie 173 (KIESA 5.2) - do projektu 173.
- **Spichlerz zamku:** nie mierzylem, ile zywnosci z przypisania wsi zostaje niezjedzone (pomiar 169).
- **Towar rozbitych taborow** (55 zl/dobe na okreg): wiadomo, ze to strata po lupie; nie wiadomo, ile z tego to rozwiazanie taboru bez bitwy (164).
- Z lancucha: konie z hodowli sa poza ksiega towarow; podatek wsi w CSV ekonomii moze byc zanizony (tylko 3 najwieksze linie); ok. 13 tys. zl/dobe wplat lordow do kies wsi jest nierozbite.
- Z historii: budzet wirgaty to rzad wielkosci. Titow, Dyer, Searle i pelna teza Cornella nieodczytane. Dochody z IPM sa zanizone, wiec rangi moga byc o 20-50% wyzej.

---

## 8. Pliki i powtarzalnosc

Ten raport: `docs/audyt-2026-10-09/15-BILANS-PANOW-ZAMKOW.md` (jedyny zapisany plik; nic nie zacommitowane).

**Badania:** katalog `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad\`:
- `zamki\lancuch.md` (+ `zamki\skrypty\`);
- `zamki\historia.md` (+ `zamki\pobrane\`);
- `dochody\pomiar.md`, `kod.md`, `historia.md`, `sim\wynik-v2.txt`.

**Dane:** `...\scratchpad\kopiaT9-120\` (log Armoury 2026-10-08_22-23-58, `budzet-rodow.csv`, `economy-2026-10-08_22-25-23.csv`).
Z logu: linie "Pieniadz swiata (bilans - przyczyny)" dob 108866-108955, pole "kiesy partii bez wodza, ktore zniknely z mapy" - srednia: karawany 15 081, tabory 19 918, bandy 2 583 zl/dobe.

**Rachunek [R] (3.5, 5):** program `...\scratchpad\kiesa\sim2\sim_kiesa2.py` (dane `sim2\dane\`), wczytany do pamieci (`python -I -`, `exec` kopii zrodla), bez zapisu plikow, z trzema zmianami:
1. po linii `army = ...` zapis grup rodow (druzyny `Wp`/zold na czlowieka, zalogi `Wg`/5.0, mediana D i kiesy, glowy < 5 000, zalegly zold) w dobach 336, 364 i 728;
2. renty korony z wagami z parametrow: `3 * d['towns'] + d['cast'] + 0.25 * d['vill']` -> `wT`, `wC`, `wV` (`sim_kiesa2.py:632`);
3. podzial zaworu zamkow osobno: `s_cast` = 2/3 (`:524-525`), a `s_lord` tylko dla miast (`:415-418`).

Warianty (zawsze `temples=True` przy kiesie ludu - dziesiecina 5% z udzialu pana, `:547`; scenariusze `wojna/fale/pokoj`, `H=728`):
- `lud=False` (PROJEKT); `lud=True` (projekt kiesy: `v_tax=0.70`); `v_tax` 0.50 / 0.45 / 0.40 / 0.30;
- przy `v_tax=0.40`: `s_lord=0.5, wT=2` z `wC` 1 / 1.5 / 2 i `wV` 0.5 / 1.0; `s_lord=2/3, wT=2, wC=2` i `wC=1.5`; `s_lord=0.4, wT=2, wC=1`;
- kontrola wersji 1 (`temples=False`): 43.7 / 17.7 i 47.1 / 21.0 tys. - powtorzone co do 0.1 tys.
Renta na udzial: `renty_korony` doby 364 / suma udzialow (97 miast, 130 zamkow, 569 wsi). Gracz (Z8): `sim2\gracz_kiesa2.py` w pamieci z zaworem miasta 1/2 i `tax = 0.35 * buy`, wies 0.60.

**Kod sprawdzony osobiscie [K]:**
- Armoury (wersja w grze, `scratchpad\sklad5\Armoury\src`):
  - `PopulationLaw.cs:24-28` (opis "Zamki bez zmian"), `:115-124` (ludzie tylko wsi i miast), `:155-172` (zerowanie podatku kazdego `Town`, takze zamku), `:294-316` (renta pomija zamki, 20% kiesy wsi);
  - `SoldierPay.cs:378-386` (zold zalogi do kasy osady, tarcza tylko w miescie), `:444-447`, `:497-500`;
  - `MarketRoad.cs:10-30`; `GoodsLedger.cs:755-758` (towar w partii po lupie); `OutlawLaw.cs:1161-1177` i `Settings.cs:592` (awanse band u pasera).
- Gra 1.4.8 (`3cf3e0ac...\ore-supply\cs`):
  - `DefaultSettlementFoodModel.cs:28, 34, 47-48, 66-74`; `DefaultBattleRewardModel.cs:196-208` (tabor 10%, banda 50%);
  - `MapEvent.cs:1595-1650` (lup towaru; pominiety, gdy wygrywaja same zalogi lub milicja); `DefaultCaravanModel.cs:55-59` (kiesa karawany 10 000 / 17 500, +5 000 gracza).
- BK (`...\ore-supply\bk`):
  - `BannerKings.Models.Vanilla\BKTaxModel.cs:313-325` (podatek wsi 70%, polityka niska 50%, wysoka 90%); `BKFoodModel.cs:36-71`;
  - `BannerKings.Patches\EconomyPatches.cs:1085-1100` (podzial utargu, 15% w nicosc);
  - `BannerKings.Behaviours\BKSettlementBehavior.cs:401-452` (AI zmienia polityke podatkowa co tydzien).

Pozostale [K] za `zamki/lancuch.md` rozdz. 10 i raportem 12, rozdz. 7.

**Zrodla historyczne:** `zamki/historia.md` rozdz. 9 (Campbell 2005, Britnell, Galloway 2000, East Meon 1301-02, Kingham 1279, IPM Chepstow/Usk, Bristol, medievalsoldier.org 1415, Lancaster 1313-14, Jan z Gandawy). Do tego `dochody/historia.md` 2.1-2.6, raport 01 2.2 i raport 09 2.6-2.7.

---

## 9. Krytyka i odpowiedzi (09.10)

Werdykt: **P** - uwaga prawdziwa, wprowadzona; **P/popr.** - prawdziwa, wprowadzona z poprawka; **F** - falszywa, odrzucona. Kazda sprawdzona w kodzie, logu albo rachunku.

| # | Uwaga (skrot) | Waga | Werdykt | Co sprawdzilem i co zmienione |
|---|---|---|---|---|
| 1 | Raport sprzeczny z decyzja Jeffa 15 ("bierze 30-50%, a nie 2/3"); udzial pana "bez pytania" 70%; rachunek liczyl 70% | krytyczne | **P** | `STAN-PRAC.md` l. 810 potwierdza decyzje (03:45, raport mial ostatni zapis 03:25). Przeliczone z `temples=True` i `v_tax` 0.70 / 0.50 / 0.45 / 0.40 / 0.30: moje liczby zgodne z krytykiem co do 0.1 tys. (0.50: 46.4 / 19.4; 0.40: 46.0 / 18.6). Nowe Z15-0 (pan 35% + sept 5% przez polityke BK), rozdz. 0, 2.1, 2.3, 3.5, 5, 6 przepisane; wieksze wyrownanie (waga zamku 1.5) zamiast powrotu do 2/3 |
| 2 | Dwa rozne punkty odniesienia (35-60% / 45-77% wobec 30-50%); 45-77% to gorna granica [S], nie [H]; 57% wartosci zestawione z % gotowki | wazne | **P** | `zamki/historia.md` 2.5: 45-77% = 18-23% przez 30-40% - zaklada caly dochod w gotowce. Jedna miara (gotowka wsi, 30-50%) w 1.2 i 2.1; 45-77% i 55-60% (wirgata) tylko jako [S] obok; w 1.2 porownanie 68% utargu, nie 57% wartosci |
| 3 | Z15-3 obejmuje karawany (10 000 / 17 500 zl); Q3 podawalo 20 tys., a jest 37.6; bandy awansuja za zloto u pasera; brak 1/9 korony | wazne | **P** | `DefaultCaravanModel.cs:55-59`, `OutlawLaw.cs:1161-1177`, `Settings.cs:592` potwierdzone; z logu srednia dob 31-120: karawany 15.1, tabory 19.9, bandy 2.6 tys. Z15-3 tylko tabory i bandy, karawany osobno (90% do wlasciciela), ryzyko band i miary w autotescie, 1/9 dla korony |
| 4 | Z15-5: (a) uzasadnienie z placenia strazy przez korone wycofane w 12 D-4, a renta S3 wieksza w pokoju (878) niz w wojnie (400); (b) to nie D-12; (c) "wedlug zalog" grozi K2; (d) zawor 1/2 juz uznany za zbedny | wazne | **P** | 12 D-4 i 5.3, PROJEKT l. 119, 248, 360, 377 potwierdzone. Z15-5 nazwane "przesuniecie od panow miast przez korone, mechanizm gry"; "stale wagi wedlug rodzaju lenna, nigdy wedlug faktycznej zalogi"; roznica wobec D-12 i przywolanie PROJEKT; Z8 przeliczone: 0.40-0.49 z monety |
| 5 | "Kasa zamku kasuje 710" zawyzone jako strata pana: 436 z wplywow to zloto z niczego; "z czego zold zalogi 464" bledne | wazne | **P** | lancuch 5 i 12 D-1: 110 cofa "zakupy" z niczego. Teraz: brutto 711, netto ok. 275-300 prawdziwych; osobna tabela kasy zamku (3.2), wiersz usuniety z tabeli wartosci towaru |
| 6 | "Zakupy pana 800-950 = 60-70% reszty" nie jest pomiarem; do miast pewnie ok. 40% reszty | wazne | **P** | `pomiar.md` 5: handel 92, odziez 18, warsztaty 14 tys. i czesc sprzetu partii; niewyjasnione 160-220 z 585-614 tys. Teraz ok. 500-800 [S], razem 1 400-1 700 [S]; wniosek oparty na zmierzonych 900 od zolnierzy wobec plonu 1 000 |
| 7 | Z15-8 zmienia kolejnosc z 12 rozdz. 4.4 i pisze, ze jest zgodny; brak liczby straty przy K6 przed 173 | wazne | **P** | 12 4.4: 111' w kroku 2, 173 w kroku 4. Teraz "zmiana kolejnosci wobec 12" wprost; [R] model PROJEKT (stan po K6) kupuje 100% plonu; [S] kazde 10% mniej kupionego plonu = ok. -65 zl/dobe u pana zamku; alternatywa z progiem w autotescie |
| 8 | Rachunek bez dziesieciny (`temples=False`) | drobne | **P** | `sim_kiesa2.py:78` i `:547` potwierdzone; z `temples=True`: 43.3 / 17.2 i 46.9 / 20.6 (krytyk 43.4 / 17.2 i 46.9 / 20.7). Caly rachunek teraz z dziesiecina |
| 9 | "Na glowe 1.2-2.6 x" - mianownik z ludzmi zamku, dolna granica wobec wszystkich panow z Kosciolem i korona | drobne | **P** | na glowe ludzi wsi (3 760) ok. 64 zl = ok. 4.4 x panow swieckich (14.3 d); po Z15-0 ok. 2.3 x; miara pomocnicza (2.1) |
| 10 | "Stale wojsko w pokoju 525 ... 12-30 x" - to wojsko wojenne (T9: wojna u 94-97% rodo-dob) | drobne | **P** | wiersz opisany jako "wojsko wojenne trzymane caly rok", bez porownania z norma pokojowa; dodany wiersz pokoju po 166 (110-135 wobec 20-50 = 2-7 x) |
| 11 | Prawa panskie raz "ukryte w podatku wsi", raz oplaty stale - podwojna nazwa; 3% od podzamcza to w 2/3 wlasne pieniadze pana | drobne | **P** | KIESA 2.2: zaloga 15 + dwor 23 + zawor 27 z 105 tys. Jedna nazwa (udzial w utargu = czynsze i folwark, oplaty stale = prawa panskie); czesc od podzamcza poza D (W-1) |
| 12 | "Towar rozbitych taborow - do sprawdzenia, czy to lup": odpowiedz jest w kodzie | drobne | **P/popr.** | `GoodsLedger.cs:755-758` liczy towar po lupie, `MapEvent.cs:1595-1635` potwierdzone. Poprawka: ten sam licznik obejmuje tez rozwiazanie partii bez bitwy, wiec pomiar 164 rozdziela oba przypadki |
| 13 | Prog autotestu "kiesa nie spada o wiecej niz 10% na 28 dob" niczego nie odroznia | drobne | **P** | rachunek, doby 336-364 (wojna): bez wyrownania -2.6%, z Z15-5 -0.4%. Nowy prog -2% na 28 dob i drugi, mocniejszy: udzial panow zamkow w D swiata >= 30% (26% wobec 33%) |
| 14 | Z15-6: pan dostaje marze zaworem (ok. 25-30), nie 40-60; brak sprawdzenia Z8; "410-450 ludzi = swita hrabiego" - porownywac w koszcie | drobne | **P/popr.** | +25-35 [S] i wymog Z8 wpisane. W koszcie 415-455 ludzi = ok. 180-220 zbrojnych 1415. Poprawka do krytyka: to nie "swita barona albo para" - to 2-3 x swita lorda-barona i poziom najmniejszej swity hrabiego (Cambridge 210) |

Zadnej uwagi nie odrzucilem w calosci.
