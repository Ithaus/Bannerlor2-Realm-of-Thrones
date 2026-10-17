# PROJEKT: DANINY SREDNIOWIECZNE (DS) - 17.10.2026

Projekt bez kodu. Kod czytany z drzewa `zb/t-r3/Armoury/src` (galaz w-toku/noc-ks3, 909aba37 = WGRANIE 74 + KS runda 3).
Liczby przeliczone od nowa z logow wlasnym parserem (`daniny/final/rc.py`, `final/model.py`, `final/ramp.py`, wynik `final/model_out.txt`):
- KS = test KS runda 3, 120 dob (Armoury-2026-10-17_06-07-51.log + CSV), doby 109075-109195;
- BAZA = WGRANIE 73 bez KS, 120 dob (Armoury-2026-10-16_19-02-22.log), doby 109075-109196;
- JEFF = jego Ironman 15/16.10 (4 sesje, doby 108946-109028, starszy build <= WGRANIE 67). Log 17.10 (02-43-14) nie ma ani jednej doby
  (wczytanie doby 109020, potem ekran kuzni 02:45-06:05), wiec liczby "Jeff" sa z 16.10.
Srednie "120 dob" = caly bieg, "28 dob" = ostatnie 28 dob. zl/d = zlota na dobe, caly swiat (571 wsi, 97 miast, 130 zamkow, 29 krolestw).

Decyzje Jeffa, ktore wiaza (17.10): daniny i oplaty jak w sredniowieczu (bez "sztucznych" zasad), system dynamiczny; zamknieta ekonomia;
podaz i popyt; produkcja tylko z rak i ziemi; start 95-105% kazdego towaru; zwrot zoldu nietkniety; stary zapis (Ironman) wczytuje sie bezpiecznie;
krol nigdy nie moze nagle nie miec na armie - wolno "nieco zawyzyc" realne podatki i oprzec sie o pozyczki od mieszczan, kupcow i bankow; dlugi trzeba splacac,
Bank z Braavos nie daruje.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 0. W skrocie

1. Petla KS (alfa wsi 0.47 -> 0) bierze sie z poborow "resztowych": korona, podatek od oszczednosci, rynek BK, drabina zbytkow biora co dzien czesc kiesy ponad prog.
   Im wiecej wies wyda na koszyk, tym mniej ma, tym mniejsza alfa. DS usuwa WSZYSTKIE pobory resztowe z kiesy wsi. Zostaja tylko daniny od sprzedazy
   (brane przy wozie, zanim zloto dojdzie do kiesy) i maly czynsz staly placony pierwszy. Budzet KS liczy sie z tych praw, nie z kiesy - petli nie ma.
2. Wybrany projekt to POLACZENIE: wies z D2 (daniny od sprzedazy, korona bezposrednio przy wozie zamiast przez panow), ksiega zaleglosci czynszu z DZ,
   kupcy jedzenia na kredycie kupieckim (DZ, uproszczone), miasta i korona z DZ ("stare sumy, nowe formy": stale firmy, nadania wojenne na kwartal,
   pomoc od sprzedazy, podatek od zarobku, dziesiata od ruchomosci), kredyt z obu (zaliczki mieszczan, Bank z zastawem cla, skarbiec przed Bankiem).
3. Wdrozenie w trzech krokach: F1 wsie + KS (runda 4 razem z KS), F2 kupcy KS-H (tez runda 4), F3 miasta, zamki, korona i kredyt (runda 5, najpierw symulator).
4. Wojna: F1 kosztuje korone 5-10% wplywow dnia przez pierwsze 4 tygodnie, potem 2.5-3% (pieniadz wraca przez miasta); panowie zyskuja tyle samo.
   F3 przy pelnym nadaniu (g = 2) daje koronie 91-93% dzisiejszych wplywow statycznie; luki 12.4 tys. zl/d w 7 malych krolestwach pokrywa skarbiec ponad
   zapas wojny, zaliczki mieszczan i Bank. Zwrot zoldu zawsze pierwszy i w calosci.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 1. Ocena projektow i wybor

| kryterium | DZ (zwyczaj, czynsz pieniezny) | D2 (prawo dworu, daniny w naturze, korona przez panow) | DS (wybrany) |
|---|---|---|---|
| realizm sredniowieczny | wysoki: czynsz, dziesiecina w naturze, champart, mlyn, subsydium, firmy, zaleglosci | wysoki dla Westeros: robocizna, danina z plonu, korona przez lennikow | wysoki: champart/robocizna jako udzial przy wozie, czynsz z zaleglosciami, dziesiecina, "dziewiata" korony, firmy, nadania, kredyt |
| zamknieta ekonomia | tak; zajecie towaru za zaleglosci wymaga wyceny i wywozu | tak; "lot pana" w magazynie 571 wsi - znakowanie towaru | tak; wszystko w istniejacych miejscach przeplywu zlota |
| dynamika (zly tydzien) | slaba: duzy czynsz staly (48% utargu) = koszt staly, alfa spada w zle tygodnie, zaleglosci w zimie | dobra: daniny od sprzedazy | dobra: daniny od sprzedazy, czynsz staly maly (1.6% plonu) |
| KS bez petli | tak (alfa 0.57, ale potrzebny "pulap pracy domowej" z bisekcja) | tak (alfa 0.43) | tak (alfa mediana 0.45-0.46 przy g = 1, 0.41 przy g = 2, policzona wies po wsi) |
| pieniadze na wojne | dobre: firmy = dzisiejsze sumy, g do 2, kredyt | slabe statycznie: panowie -35%, korona -66 tys./d, liczy na nieudowodniony obieg | jak DZ dla miast i korony; wies jak D2; F1 neutralne po 2-3 mies. |
| ryzyko wdrozenia | wysokie (pomiar zwyczaju z zapisow KS z oslabiona sprzedaza, champart, mlyn, dziesiecina w naturze, zajecia) | wysokie (loty w magazynach, nowa pomoc lennikow 1/3 dochodu, renty jako annuitety) | F1 male (8 haczykow + 1 plik + wzor KS), F2 srednie, F3 duze, ale po symulatorze |
| stary zapis | okno 28 dob, nowe klucze | okno 28 dob, nowe klucze | F1/F2 tylko tam, gdzie KS zastosowany; F3 okno 28 dob; nowe klucze zapisu, stare nietkniete |

Czego NIE biore i dlaczego:
- czynsz pieniezny DZ zastepujacy udzial pana w utargu: koszt staly 48% utargu zabija alfe w zle tygodnie i w zimie Westeros (2-6 lat), a do ustalenia
  wymaga "normalnego" okna, ktorego w zapisach KS nie ma (sprzedaz -33%). Udzial od sprzedazy (champart, metayage) jest tak samo sredniowieczny;
- dziesiecina w naturze od calego plonu (DZ): zabiera jedzenie zjadane w domu i zmienia rece w Planie - ryzyko dla 95-105%;
- korona tylko przez panow (D2): pomoc lennikow 1/3 dochodu z ziemi jest wysoka i nie z historii; korona zalezalaby od gotowki panow na pulapie
  (zaleglosci potracane z W5 = petla do armii);
- annuitety zamiast rent 180 (D2): renty 180 sa buforem, ktory chroni zwrot zoldu - zostaja jako "laska krola z tego, co zostalo". To wydatek, nie pobor.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 2. Zasady ogolne DS

Z1. Kazda danina jest jednym z trzech rodzajow:
- (a) udzial w sprzedazy brany w chwili sprzedazy (udzial pana przy wozie, dziesiecina, udzial korony przy wozie, myto, pomoc od sprzedazy, clo);
- (b) stala suma zwyczajowa na terminy z ksiega zaleglosci (czynsz wsi, firma miasta, firma zamku);
- (c) nadanie wojenne: stawka na stala podstawe, ogloszona na kwartal (91 dob) na poziomie g (0, 0.5, 1, 1.5, 2).
Zadna danina nie jest "x% dziennie tego, co ponad prog".

Z2. Kolejnosc wydatkow jak w sredniowieczu: daniny u zrodla (przy wozie) -> czynsz -> jedzenie i rzeczy konieczne (koszyk) -> skrzynia na czarna godzine
-> zbytki tylko z prawdziwej nadwyzki ponad skrzynie. Pozyczki dla panow tylko z kiesy ponad skrzynie.

Z3. Pieniadze na wojne: zwykle wplywy (firmy, clo, powinnosci) + nadanie wojenne na kwartal dobrane do prognozowanego braku + kredyt.
Zwrot zoldu nietkniety i zawsze przed rentami. Brak na zwrot: skarbiec ponad zapas wojny -> zaliczka mieszczan pod nadanie -> Bank z Braavos.

Z4. Zamknieta ekonomia: kazdy przeplyw ma platnika i odbiorce; zaleglosc i weksel to roszczenie, nie pieniadz; nic z niczego, nic w nicosc.

Z5. Stare zapisy: nowe klucze zapisu, domyslnie 0; okno pomiaru 28 dob tam, gdzie trzeba ustalic stara sume; wlaczanie stopniowe (rampa 28 dob).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 3. WIES (krok F1)

Wies DS = wies, na ktorej KS jest zastosowany (`StartCalibration.AppliedFor(vil)`) i klucz `DuesMedieval` wlaczony. Pozostale wsie - stare zasady bez zmian
(to wazne dla gry Jeffa na WGRANIE 74: bez koszyka KS wies nie ma na co wydac uwolnionych pieniedzy - patrz rozdz. 15 ryzyko 3).

### 3.1 Daniny wsi

| nr | danina | podstawa i stawka | kiedy i jak | platnik -> odbiorca | zly rok / wojna | dzis -> DS |
|---|---|---|---|---|---|---|
| W1 | udzial pana z utargu (champart + robocizna zamieniona na pieniadz) | utarg oddany przez woz: BK podatek x 0.5 + 10% = 45% (dekret Low 35%, High 55%); z tego majatki BK (ok. 9 pkt) | przy powrocie wozu (VillageTakings 112) | woz -> licznik pana, majatki BK | sam spada ze sprzedaza | bez zmian (kod bez zmian, tylko nazwa) |
| W2 | dziesiecina | 5% oddanego utargu (= 1/11 czesci wsi) | PRZY WOZIE (bylo: z kiesy po oknie, najwyzej stan kiesy) | czesc wsi -> kaplan: 1/2 jalmuzna biednym wsi (zostaje w kiesie), 1/2 budowa septow (3/4 place ludu miasta targowego, 1/4 material do kasy) | spada ze sprzedaza; bez kaplana nie ma | ta sama stawka, inne miejsce poboru |
| W3 | czynsz staly (sad, mlyn, targ) | 4 zl rocznie na wolna glowe x dekret (0.7 / 1 / 1.3, zwolnienie 0) | narasta co dzien 1/364; placony z kiesy PIERWSZY w dobie (PopulationLaw przed TownFolk); brak -> ksiega zaleglosci, splacana pierwsza | kiesa -> glowa rodu pana (RentToday, ta sama sciezka) | rabunek, spladrowanie, oblezenie miasta, glod: rata dnia darowana od razu; zaleglosc starsza niz 364 dni - pan darowuje ("decasus", licznik) | dzis: najwyzej kiesa, reszta przepada; DS: zaleglosc |
| W4 | udzial korony przy wozie ("dziewiata" w wojnie) | g x 1/10 czesci wsi po dziesiecinie (g = 1 w wojnie, 1.5 z polityka War Tax, 0 w pokoju; w F3 g z nadania) | przy wozie, ta sama chwila co W2; takze od czesci wsi z markietanow | czesc wsi -> skarbiec korony (wplyw dnia 165, LeviesToday) | wies nie Normal albo miasto targowe oblezone albo glod (Famine) - 0 | zastepuje danine 2.5%/d kiesy ponad 15 dni |
| W5 | myto targowe | 4% sprzedazy wozu w miescie/zamku, 1/2 pan miasta, 1/2 zapas korony | bez zmian | woz -> pan miasta, korona | - | bez zmian |

Wylaczone dla wsi DS:
- danina wojenna korony od kiesy (KingdomTreasury.cs:395-412, galaz wsi);
- podatek od nadwyzki wsi (TownFolk.Guard.cs:341 PayHoardVillage);
- stawka wojenna pana W7 ze wsi (KingdomTreasury.LordLevy.cs:251-262, 306);
- rynek wsi BK jako osobny pobor 5% kiesy (BkSeal34.cs:113) - zostaje jako "sklep" kroku 5;
- wydatek z nadwyzki 15%/d na stara liste i drabina P5 poza krokiem 5 (TownFolk.Live.cs:1179-1210, TownFolk.Luxury.cs:404).

Markietani (czesc wsi 15% wydatkow zolnierzy): zostaje jako zaplata za kwatery i uslugi (pranie, gotowanie, pasza, wozenie) - pieniadz za prace,
bez towaru. Podzial jak dzis (pan wsi z cudzego rodu bierze udzial BK), plus W4. Zakup prawdziwego jedzenia wsi przez zolnierzy - krok F4 (z popytem w Planie).

### 3.2 Kolejnosc wydatkow wsi (co dobe)

0. Przy wozie (nigdy nie dochodzi do kiesy): udzial pana i majatki (W1), dziesiecina (W2), udzial korony (W4), myto (W5).
1. Czynsz (W3) i zaleglosc czynszu - pierwsze z kiesy (PopulationLaw.Daily idzie przed TownFolk.Daily, ArmouryBehavior.cs:1859 i 1872).
2. Koszyk KS (ZC4: zboze i sol -> napoj -> bialko -> reszta) za budzet z Planu; dla wsi bez ZC4 - stara lista dnia (buy1). Wlasne jedzenie najpierw (KsOwnFoodFirst).
3. E3-W1: zaleglosc koszyka kupowana wozem w innym miescie (bez zmian).
4. Skrzynia na czarna godzine: Sk_v = 21 x (koszyk zaplacony na dobe z Planu albo buy1) + 21 x czynsz na dobe. Ponizej skrzyni nic wiecej nie idzie.
5. Zbytki tylko z nadwyzki: N_v = kiesa - Sk_v; wydatek dnia = rampa x N_v / 15 (rampa 0 -> 1 przez 28 dob od wlaczenia), polowa na liste P5
   (TownFolk.Luxury), polowa na rynek BK (BkSeal34, towar z nadwyzka podazy). Pozyczki od ludu dla panow (TownFolk.Loans) - tylko z N_v.

Zadna danina nie czyta kiesy: kiesa decyduje tylko o tym, czy wies MA czym zaplacic (czynsz) i ile wydaje sama (krok 5).

### 3.3 Zly rok i wojna dla wsi

- Zly rok (zima, nieurodzaj, mniej rak): sprzedaz spada -> W1, W2, W4 spadaja same tego samego dnia. Czynsz (1.6% plonu) zostaje; gdy brak - zaleglosc,
  nie glod (czynsz nigdy nie zabiera pieniedzy z kroku 2 nastepnych dni ponad rate dnia + zaleglosc; skrzynia 3 tygodni trzyma koszyk).
- Rabunek, spladrowanie, oblezenie miasta targowego, glod: W4 = 0 i rata czynszu darowana tego dnia (jak 1433: ulga dla zniszczonych osad).
- Wojna: W4 = 1/10 czesci wsi (g = 1), w ciezkiej wojnie 1/5 (g = 2). W pokoju 0.

### 3.4 Sprawdzenie historyczne (wies srednia, KS, 120 dob)

Plon wsi brutto ok. 630 tys. zl/d (KS doba 109194: na targ 264 tys. + w domu 364 tys.).

| pozycja | dzis zl/d | % plonu | DS g=1 zl/d | % plonu | DS g=2 | % plonu |
|---|---|---|---|---|---|---|
| pan + majatki (W1) | 114.7 tys. | 18.3% | 114.7 tys. | 18.3% | 114.7 tys. | 18.3% |
| czynsz staly | 10.0 tys. | 1.6% | 10.0 tys. (+ zaleglosci) | 1.6% | 10.0 tys. | 1.6% |
| dziesiecina netto (po jalmuznie) | 6.6 tys. | 1.0% | 6.6 tys. | 1.0% | 6.6 tys. | 1.0% |
| korona: danina od kiesy | 61.6 tys. | 9.8% | 0 | 0 | 0 | 0 |
| podatek od oszczednosci (korona+pan+kasy) | 45.8 tys. | 7.3% | 0 | 0 | 0 | 0 |
| korona przy wozie (W4) | 0 | 0 | 16.7 tys. | 2.7% | 33.4 tys. | 5.3% |
| RAZEM daniny | 238.7 tys. | 38% | 148 tys. | 23.6% | 164.7 tys. | 26.2% |

Historia: panowie ok. 1300 brali 18-23% dochodu z ziemi, a wszystkie renty, dziesieciny, banalia i kary razem najwyzej 1/8 dochodu wsi (Campbell 2005);
typowe obciazenie srednie 20-30% plonu, najgorsze lata wojen 40-50%. DS miesci sie w 20-30%; dzisiejsze 38% (+5% plonu na wymuszony rynek BK) - nie.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 4. KS krok 8 bez petli (czesc F1)

### 4.1 Wzor

Dzis (StartCalibration.Money.cs:127-142, klucz KsAlphaNet): alfa = 0.95 x (wplyw - wydatki spoza koszyka) / wartosc bazowa - wydatki spoza koszyka to wlasnie
pobory resztowe, wiec alfa zalezy od kiesy i od samej siebie.

DS (nowy klucz `KsAlphaRules`): budzet koszyka wsi v w Planie (StartCalibration.Core.cs:1218):

    budzet_v = max(0, AlfaR_v x sprzedazB_v + Stala_v)
    AlfaR_v  = 0.95 x k_v x Phi
    k_v      = (u_v - 0.05) x (1 - g_k/10) + 0.025
    Stala_v  = 0.95 x (O_v x (1 - g_k/10 x m_v) - R_v)

- sprzedazB_v - zmienna Planu (sprzedaz okregu na targ w wartosci bazowej, z konmi stadnin) - bez zmian;
- u_v - udzial wsi w utargu z dekretu pana (pomiar 28 dob a[1]/a[0] z VillageTakings: 0.55 / 0.65 / 0.45; to prawo, nie stan kiesy);
- 0.05 - dziesiecina, 0.025 - polowa wraca jako jalmuzna; g_k - nadanie krolestwa na ten kwartal (znane z gory);
- Phi - utarg swiata / wartosc bazowa produkcji wsi (istniejace PhiNow, 28 dob; KS 0.69-0.77) - poziom cen, nie kiesa;
- O_v - srednia 28 dob wplywow wsi, ktore nie zaleza od jej kiesy ani wydatkow: markietani (czesc do kiesy), sakiewki zwolnionych, zakupy panow we wsi,
  zloto notabla bez dziedzica, fracht KS-H; m_v - czesc markietanow w O_v (W4 dotyczy tez markietanow);
- R_v - czynsz na dobe (VillageFeeToday x dekret).

Zaden skladnik nie czyta kiesy ani zadnego odplywu. Jedyna petla to ta w Planie: sprzedaz <-> praca domowa; nachylenie 0.95 x k x Phi = 0.95 x 0.476 x 0.72
= 0.33 < 1 - istniejace rundy punktu stalego (tlumienie 0.5) zbiegaja. Alfa widoczna w logu = budzet / sprzedazB ("alfa z praw").

### 4.2 Alfa z praw - wies po wsi (KS CSV ks-budzety, Phi 0.72, O przydzielone wedlug utargu, R wedlug ludzi)

| doba Planu | g | p10 | p25 | mediana | p75 | p90 | wsi < 0.20 |
|---|---|---|---|---|---|---|---|
| 109123 (553 wsi) | 0 (pokoj) | 0.42 | 0.48 | 0.50 | 0.52 | 0.57 | 10 |
| 109123 | 1 | 0.38 | 0.43 | 0.45 | 0.47 | 0.52 | 12 |
| 109123 | 2 | 0.33 | 0.39 | 0.41 | 0.43 | 0.46 | 17 |
| 109179 (566 wsi) | 1 | 0.37 | 0.44 | 0.46 | 0.47 | 0.49 | 17 |
| 109179 | 2 | 0.32 | 0.39 | 0.41 | 0.43 | 0.44 | 25 |
| dzis zmierzona (netto) 109123 | - | 0.00 | - | 0.00 | - | 0.16 | 305 z 548 na 0 |
| dzis zmierzona (netto) 109179 | - | 0.00 | - | 0.07 | - | 0.25 | 222 z 552 na 0 |

Wsie < 0.20: male wsie z wieloma ludzmi (czynsz od glow wiekszy niz utarg) - budza zaleglosci czynszu, nie zera.

### 4.3 Budzet koszyka wsi swiata (zl/d)

| | KS 120 dob | KS 28 dob | BAZA 120 dob | JEFF 16.10 |
|---|---|---|---|---|
| dzis: budzet Planu | 105 tys. (start, alfa 0.47 bez pomiaru) -> 16-36 tys. po zapadnieciu | - | - | - |
| dzis: kupione przez wsie (koszyk + stara lista) | 54.4 tys. (81 tys. po dobie 109135) | 80.9 tys. | 13.4 tys. | 6.8 tys. |
| DS g=0 (pokoj) | 180.0 tys. | 171.4 tys. | 193.2 tys. | 189.6 tys. |
| DS g=1 | 164.1 tys. | 156.2 tys. | 176.3 tys. | 173.1 tys. |
| DS g=2 | 148.2 tys. | 141.0 tys. | 159.4 tys. | 156.6 tys. |
| do skrzyni (5%) g=1 | 8.6 tys. | 8.2 tys. | 9.3 tys. | 9.1 tys. |

Skad pieniadze: wies zatrzymuje to, co dzis biora pobory resztowe (KS: korona 61.6 + podatek od oszczednosci 45.8 + rynek BK 32.0 tys. = 139 tys./d).
Koszyk wsi (KS, ceny startu) 400-441 tys./d; zaplacona czesc rosnie z 5-14% do ok. 35-40%, praca domowa spada, sprzedaz na targ rosnie.

### 4.4 W Planie i zapisie
- KsDistrict.Alpha (Core.cs:85) = AlfaR_v; nowe pole KsDistrict.Fix = Stala_v; MDist (Core.cs:1001) dostaje Fix; Core.cs:1080-1084 przepisuje Fix jak Alpha;
  Core.cs:1218 `budget = Math.Max(0.0, v.Alpha * salesB + v.Fix)`.
- Plan.cs:173 `kd.Alpha = DsVillage.KsOn(v) ? VilAlphaRules(v, out kd.Fix) : VilAlphaFor(v);`.
- VilOutDaily / KsVilOut zostaja tylko do logu ("alfa zmierzona" obok "alfa z praw").
- Liczniki O_v (nowe `KsVilIn(v, x)`, ta sama waga 28 dob co utarg): Sutlers.cs:381-393 (czesc do kiesy po W4), ClanBudget.cs:1236-1248 (zwolnieni),
  VillageTakings.cs:383-407 (zakupy panow), Seal31B.cs:1057-1068 (notabl), KsFoodCarts.cs:637-665 (fracht, juz jest KsVilFreight).
- Srednie O_v zapisywane w nowym kluczu `arm_daniny` (nie w "@$V", zeby starsza DLL nie czytala zle).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 5. KUPCY JEDZENIA KS-H NA KREDYCIE KUPIECKIM (krok F2)

Dzis (KsFoodCarts.cs:47 CofferShare 0.15, :451 budzet, :595-633 Ship, :341-380 Arrivals, :383-404 Back): kasa CELU placi z gory przy wyjezdzie (najwyzej
15% kasy ponad zapas na dobe) towar i fracht. Wynik: 42% zamowionych sztuk zablokowane "kasa celu za mala" (KS 120 dob: zamowione 15.4 tys. szt./d,
kupione 10.4 tys., zablokowane 6.4 tys.; ostatnie 28 dob 19.5 / 12.8 / 8.6 tys.), Pentos ponizej zapasu 120 dob z rzedu.
W sredniowieczu kupiec zbozowy kupowal za wlasny kapital albo kredyt, wiozl i dostawal pieniadze ze sprzedazy w miejscu docelowym; miasto nie placilo z gory
(londynscy kupcy zbozowi w Henley, statut kupiecki 1283/1285 - egzekucja dlugu kupca).

Nowa zasada (klucz `KsFoodCartsConsign`):
1. Wyjazd: towar schodzi z polki zrodla (jak dzis), CEL NIE PLACI. Fracht placi kasa ZRODLA (kupiec placi furmanom: wsie zrodla wedlug ognisk,
   morzem kiesa wyrobnikow zrodla). Cart.Paid = 0.
2. Warunki zamowienia (zamiast budzetu 15% kasy celu): (a) cel nie ma rachunkow starszych niz 30 dni; (b) otwarte rachunki zrodla + ten lot <= 50% kasy zrodla;
   (c) kasa zrodla ma na fracht. Reszta regul oferty i ceny bez zmian (polowa nadwyzki ponad 1.25 x T* x Dz + 5, indeks celu >= 1.03 x koszt).
3. Dostawa: towar na polke celu (jak dzis). Rachunek: cel winien zrodlu n x cena skupu celu (ta sama, co dla taboru wsi w tym miescie).
   Cel placi od razu, ile moze: min(rachunek, kasa celu - 25% zapasu kupcow celu); reszta zostaje jako rachunek.
4. Splata: co dobe, przed zaworem/firma, kasa celu splaca rachunki od najstarszego z kasy ponad 25% zapasu.
5. Powrot (cel utracony, wojna): towar wraca na polke zrodla, pieniadze nie plyna (nic nie zaplacono). Rachunki miedzy krolestwami w wojnie przepadaja
   ("kupcy stracili", licznik); w jednym krolestwie przetrwaja zdobycie.
6. Stare wozy w drodze z Paid > 0 (zaplacone po staremu) dojezdzaja po staremu (zwrot przy powrocie) - rozpoznanie po Paid, bez nowych pol w zapisie wozu.
7. Zapis: nowy klucz `arm_daniny_kupcy` (rachunek: zrodlo~cel~zostalo~doba).

Oczekiwane (KS): zablokowane przez cel 42% -> 0; kupione z 10.4-12.8 tys. do 15.4-19.5 tys. szt./d; zloto towaru z 31.8-41.8 tys. do ok. 50-63 tys. zl/d;
otwarte rachunki - glownie biedne miasta (Pentos, Lys) - ok. 0.1-0.4 mln (kasy miast 13.4 mln). Kontrola doby: w drodze wczoraj + wyslane - dowiezione -
zawrocone = w drodze dzis; zloto: fracht + zaplaty przy dostawie + splaty = ruch miedzy kasami, suma 0.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 6. MIASTA, ZAMKI, KORONA (krok F3) - "stare sumy, nowe formy"

Historycznie firma miasta (firma burgi) byla ustalana na sume, ktora szeryf krola zbieral wczesniej, i potem byla stala; korona w wojnie dostawala
nadania (dziesiata i pietnasta, w Anglii 1294-97 co rok), pomoc od sprzedazy (alcabala 10%, aides), clo wojenne (maltolt), pozyczki pod clo
(Ricciardi, Bardi, Peruzzi) i przymusowe pozyczki miast (Florencja, Wenecja, 5%).

### 6.1 Daniny miast i zamkow

| nr | danina | wzor | kiedy | platnik -> odbiorca | zastepuje |
|---|---|---|---|---|---|
| M1 | czynsz gruntowy | 1/3 czynszu W i R do pana | jak dzis | lud -> pan | bez zmian |
| M2 | firma miejska panu | F_L = 364 x srednia 28 dob (zawor do pana + udzial pana w podatku od skarbca ludu tego miasta + W7 z ludu tego miasta) | 1/52 co 7 dob z kasy (dzien wedlug Fnv miasta); zwloka -> ksiega; po 2 terminach (91 dob) pan bierze 2/3 akcyzy gminy i swoja polowe myta do splaty; odnowa co 364 dni w strone (doplyw netto kupcow 28 dob x udzial z dnia ustalenia), najwyzej +-15%; oblezenie/lup - rata darowana | kasa -> glowa rodu | zawor 7%/d (TownPurse.cs:420-492, PopulationLaw.cs:1329), podatek od skarbca ludu (czesc pana) |
| M3 | firma krolewska | F_C = 364 x srednia 28 dob (zawor do korony tego miasta) | jak M2, w pokoju i w wojnie; zwloka: korona bierze w swoje rece licznik cel miasta | kasa -> skarbiec (wplyw dnia 165) | czesc korony z zaworu |
| M4 | firmy zamku | zamek: 364 x srednia 28 dob zaworu zamku (pan 67%, korona 33%) | jak M2 | kasa zamku -> pan, skarbiec | CastlePurse.cs:209 Dues, wywolanie :613 |
| M5 | akcyza 2% | gmina 2/3, pan 1/3 | jak dzis | lud -> gmina, pan | bez zmian |
| M6 | wyplata kupcom | 1/28 dziennie kasy ponad 1.5 x zapas kupcow -> rodziny P (70%) i R (30%) jako zarobek (placa 184 i podatek pana) | co dobe, po firmach | kasa -> kiesy P, R | "5% zaworu dla ludu" |
| M7 | pomoc wojenna od sprzedazy (aides, alcabala) | min(10%, g x 5%) sprzedazy kasy z dnia (lud, markietani, wsie, partie) | co dobe z licznika sprzedazy kasy; brak -> ksiega przed firma | kasa -> skarbiec | danina wojenna miast 1%/d (KingdomTreasury.cs:395-412) |
| M8 | podatek wojenny od zarobku (184) | s = min(10%, g x 5%) (z polityka War Tax do 15%); wagi W 0.5, R 1, P 1.5, podzamcze 1 | u zrodla, jak dzis; stawka na kwartal zamiast kroku +0.5 pkt/tydzien | lud -> skarbiec | KingdomTreasury.WarTax.cs:130-136, 243-244 |
| M9 | dziesiata od ruchomosci (subsydium) | w wojnie, w kazdy dzien kwartalu: g x 1/10 (oszczednosci ludu ponad zapas dni W10/R21/P30/podzamcze 10 + kasa ponad 1.5 x zapas) | 13 rat co 7 dob; oblezenie/lup - rata darowana; zwloka -> ksiega korony, doliczana do nastepnego nadania | kiesy ludu i kasa -> skarbiec | P1 (KingdomTreasury.WarAid.cs), podatek od skarbca ludu (TownFolk.Hoard.cs, Guard.cs:220-300) |
| M10 | clo | CrownCustomsShare x 2 w wojnie (maltolt), x 4 z Crown Duty | jak dzis | licznik cel -> skarbiec | mnoznik |

Lud miast: zbytki z prawdziwej nadwyzki 1/15 dziennie oszczednosci ponad zapas dni (zamiast 15%/d, TownFolk.Live.cs:994-996), rampa 28 dob.

### 6.2 Korona

- K1 pomoc lennikow: w wojnie 10% czesci "ziemia" D stalego (bylo 3%, KingdomTreasury.cs:97), w pokoju 2%.
- K2 nadanie wojenne g: w dni kwartalu (91/182/273/364) dla kazdego krolestwa w wojnie:
  brak = (zwrot + potrzeba W5 + dary + kontrakty + raty dlugow + 1/2 zwyklych rent 180 + 1/91 luki do zapasu wojny) - (firmy + clo + pomoc lennikow + inne + wydanie zapasu)
  - plon nadania przy g = 1; g = najmniejsze z {0.5, 1, 1.5, 2} pokrywajace brak, zmiana najwyzej 0.5 na kwartal; War Tax +0.5 (najwyzej 2); pokoj -> 0
  w najblizszy dzien kwartalu (albo po 14 dobach pokoju). g stale przez kwartal - nie ma petli z kiesami (Parlament nadal tyle "dziesiatych").
- K3 garnek W5 = wplywy wojenne dnia (M7 + M8 + raty M9 + W4), nie P1.
- K4 kolejnosc wydatkow bez zmian: rata 186 -> dary -> reparacje -> kontrakty -> wezwania -> ZWROT ZOLDU (nietkniety) -> W5 -> W5b -> M1 -> M3 -> dwor -> renty 180.
- K5 brak na zwrot: najpierw skarbiec ponad zapas wojny (skarb krola), potem zaliczka mieszczan (L1), na koncu Bank 186.
- Zostaja: 1/450 zapasu (krol wydaje skarb w wojnie), W5b z zapasu ponad zapas wojny, renty 180 jako ostatni wydatek ("laska krola"), podatek gry od bogatych
  rodow (41 tys./d - F4: zamiana na dziesiata od ruchomosci panow).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 7. KREDYT: mieszczanie, kupcy, Bank (F3; F1 uzywa istniejacego)

| nr | pozyczka | kiedy | stawka | limit | splata | zwloka |
|---|---|---|---|---|---|---|
| L1 | zaliczka mieszczan pod nadanie (rozszerzenie korona2, TownFolk.CrownLoans.cs, nowy rodzaj 'a') | po K5, gdy skarbiec ponad zapas wojny nie starcza na zwrot i W5 | 10% rocznie | niezebrane raty biezacego nadania (M9 + W4 prognoza) | pierwsza z rat nadania, gdy wplywaja | reszta po kwartale -> zwykly dlug korona2 |
| L2 | przymusowa pozyczka (prestanza) | korona2 jak dzis (skarbiec ponizej zapasu wojny) | 10% rocznie | 90 dni wplywow + 50 tys. na miasto (bez zmian); pozyczajacy wedlug listy M9 | w pokoju z akcyzy i cla | jak dzis (umorzenie po 90 dobach w pokoju, lojalnosc -10) |
| L3 | Bank z Braavos dla korony (CrownBorrow 186) | brak na zwrot po L1 | 20-30% + 2% oplaty (bez zmian) | 180 dni sredniego podatku (podstawa = nowe wplywy: firmy + nadanie + clo + pomoc); 365 dni, gdy krolestwo zastawi clo | rata 1/182 najwiekszego dlugu, najwyzej 30% podatku | zwloka: Bank sam bierze licznik cel krolestwa, az bedzie 28 dob pelnych rat; Bank NIGDY nie daruje |
| L4 | pozyczki od ludu dla panow (TownFolk.Loans.cs) | bez zmian | 12% | bez zmian | bez zmian | bez zmian; ze wsi DS tylko ponad skrzynie |
| L5 | kredyt wojenny KW (DebtLadder) | bez zmian | - | bez zmian | - | - |

Pozyczki nie uniosa wiecznej wojny same: mozliwy do pozyczenia zapas mieszczan i kas ok. 17.7 mln (KS: oszczednosci ponad zapas 12.2 mln + kasy ponad zapas 5.5 mln)
to ok. 36 dni potrzeby wojennej korony (483 tys./d z W5b). Dlatego kredyt jest tylko na luki i wahania kwartalu, a glowny ciezar niosa podatki (rozdz. 8).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 8. Co podnosimy ponad historie, o ile i dlaczego (Jeff: "nieco zawyzyc")

Powod wspolny: w sredniowieczu wojny trwaly kilka lat i konczyly sie pokojem i splata dlugow; u nas 26 z 29 krolestw walczy bez przerwy, a zold
(637 tys. pola + 214 tys. zalog na dobe) to wiekszosc pieniadza w obiegu. Formy sa historyczne, czestotliwosc i czesc stawek wyzsza.

| podatek | historycznie | DS g=1 | DS g=2 | podniesione o | dlaczego |
|---|---|---|---|---|---|
| udzial korony przy wozie (W4) | 1340 "dziewiata" snopow, runa, jagniat na 2 lata (ok. 11% plonu); dziesiata/pietnasta co rok w 1294-97 | 1/10 czesci wsi = 2.7% plonu | 1/5 = 5.3% plonu | czestotliwosc: kazdy kwartal wojny | wieczna wojna |
| pomoc lennikow (K1) | scutage, pomoce; lordowie placili subsydium od ruchomosci | 10% dochodu z ziemi | 10% | z 3% do 10% | "dziesiata" panow |
| podatek od zarobku 184 (M8) | dziesiata Saladyna 1188 (1/10 dochodow), miasta 1/10 ruchomosci | W 2.5 / R 5 / P 7.5% | W 5 / R 10 / P 15% | pulap z 3% (6%) do 10% (15%) | zastepuje P1 (190 tys./d); P 15% to 1.5 x historyczna dziesiata |
| pomoc od sprzedazy (M7) | alcabala kastylijska 10%, aides francuskie ok. 5% (stawka 1360 niepewna) | 5% | 10% | nowe | najwiekszy podatek wojenny w wojnie stuletniej byl posredni |
| dziesiata od ruchomosci (M9) | 1/10 ruchomosci miast na nadanie | 1/10 na kwartal | 2/10 na kwartal | do 8 nadan rocznie (1294-97: 1 rocznie) | wieczna wojna |
| clo wojenne (M10) | maltolt 1294: 40 szylingow za worek welny = 6 x dawne clo | x2 | x2 | x2 (historia x6) | umiarkowanie |
| firmy miast i zamkow (M2-M4) | firma = stara suma szeryfa, potem stala; zwykle ok. 10-15% dochodu miasta | dzisiejsza suma zaworu (ok. 96% doplywu netto kupcow) | to samo | 4-5 x udzial historyczny | u nas doplyw kas miast to glownie zold wydany u markietanow (322-344 tys./d) - firma oddaje te pieniadze panom i koronie, jak zaopatrzenie wojska (purveyance, prises) |

NIE podnosimy: udzialu pana przy wozie (18% plonu - w normie), czynszu (4 zl/glowe/rok), dziesieciny (5%), myta (4%), czynszu gruntowego, podatkow pana od zarobku, akcyzy.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 9. LICZBY (przeliczone z logow)

### 9.1 Kiesy wsi - przeplywy swiata dzis (zl/d, 120 dob: KS / BAZA / JEFF)

| pozycja | KS | BAZA | JEFF |
|---|---|---|---|
| utarg oddany przez wozy H | 255.9 tys. | 275.7 tys. | 271.3 tys. |
| - pan (licznik) | 92.0 tys. (36.0%) | 99.7 tys. (36.2%) | 103.5 tys. (38.2%) |
| - majatki BK | 22.7 tys. (8.9%) | 24.0 tys. (8.7%) | 18.1 tys. (6.7%) |
| - do kiesy z wozow | 142.4 tys. (55.6%) | 153.3 tys. (55.6%) | 152.7 tys. (56.3%) |
| inne wplywy: przelewy gry / markietani / zwolnieni / KS-H | 17.0 / 37.5 / 8.2 / 1.0 tys. | 22.3 / 38.4 / 6.7 / 0 | 21.8 / 34.7 / 6.9 / 0 |
| danina korony od kiesy | 61.6 tys. | 75.4 tys. | 88.1 tys. |
| podatek od oszczednosci wsi (korona / pan / kasy) | 45.8 (19.8 / 17.5 / 8.6) tys. | 63.1 (24.9 / 25.6 / 12.6) | 77.2 (27.0 / 33.6 / 16.6) |
| rynek BK wsi | 32.0 tys. | 39.1 tys. | 35.4 tys. |
| czynsz / dziesiecina netto / pozyczki dla panow | 10.0 / 6.6 / 3.1 tys. | 10.3 / 7.1 / 5.1 | 9.7 / 6.9 / 3.9 |
| zakupy wsi (koszyk + stara lista) | 54.4 tys. | 13.4 tys. | 6.8 tys. |
| stan kies: start -> koniec | 3.66 mln -> 2.61 mln (szczyt 4.19 mln w dobie 109125) | 3.67 -> 4.26 mln | 4.61 -> 4.52 mln (wahania 4.41-5.05 mln) |
| mediana kiesy / wsi < 1000 zl (ostatnie 28 dob) | 2 910 / 170 | 5 476 / 68 | 5 704 / 56 |

Korona bierze dzis ze wsi (danina + czesc podatku od oszczednosci): KS 81.4 tys. = 13.3% jej wplywow dnia, BAZA 100.3 tys. = 17.4%, JEFF 115.1 tys. = 20.8%.

### 9.2 Trzy wsie rocznie (KS, Plan doby 109179, g = 1; "dzis" = struktura swiata KS skalowana utargiem tej wsi)

| wies | utarg/rok | dzis: do kiesy | dzis: korona + podatek od oszcz. + rynek BK | dzis: czynsz + dziesiecina netto | dzis: koszyk | dzis: zostaje | DS: dziesiecina (wraca) | DS: korona | DS: czynsz | DS: na koszyk | DS: do skrzyni | alfa DS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| biedna p10 village_N1_4 | 16.8 tys. | 13.2 tys. | 4.0 + 3.0 + 2.1 tys. | 1.1 + 0.4 tys. | 3.6 tys. | -1.0 tys. | 0.84 (0.42) tys. | 1.08 tys. | 1.10 tys. | 10.1 tys. | 0.53 tys. | 0.43 |
| mediana ROT_town32_village2 | 113.2 tys. | 89.1 tys. | 27.2 + 20.3 + 14.2 tys. | 6.0 + 2.8 tys. | 24.1 tys. | -5.5 tys. | 5.7 (2.8) tys. | 7.2 tys. | 6.0 tys. | 69.4 tys. | 3.7 tys. | 0.44 |
| bogata p90 ROT_castle53_village1 | 369.6 tys. | 301.8 tys. | 89.0 + 66.2 + 46.3 tys. | 7.6 + 9.2 tys. | 78.6 tys. | +5.0 tys. | 18.5 (9.2) tys. | 24.7 tys. | 7.6 tys. | 247.2 tys. | 13.0 tys. | 0.48 |

Pan bierze przy wozie (bez zmian): biedna 7.6 tys., mediana 50.9 tys., bogata 166.3 tys. rocznie (45% utargu, w tym majatki).
Przy g = 2 korona dwa razy wiecej (2.2 / 14.5 / 49.5 tys.), na koszyk 9.1 / 62.5 / 223.7 tys., alfa 0.39 / 0.40 / 0.44.
Zly rok (sprzedaz -30%): mediana ma na koszyk ok. 54 tys. zamiast 69.4 tys. (-22%, bo wplywy spoza wozow zostaja), alfa z praw ok. 0.49; czynsz 6.0 tys. placony pierwszy.

### 9.3 Dryf kies wsi
- Dzis: KS -8.9 tys./d (3.66 -> 2.61 mln; od szczytu 4.19 mln -22.9 tys./d), BAZA +5.0 tys./d (3.67 -> 4.26 mln), JEFF -1.1 tys./d (4.61 -> 4.52 mln, wahania 4.41-5.05 mln).
- DS: +5% wplywu do skrzyni (KS 8.6 tys./d, BAZA 9.3, JEFF 9.1) do celu skrzyni 21 dni (KS ok. 3.65 mln = 21 x 164 tys. + 21 x 10 tys.; BAZA ok. 3.9 mln).
  KS z 2.61 mln dochodzi w ok. 120 dob; BAZA i JEFF sa juz przy celu - krok 5 wydaje nadwyzke (1/15 dziennie). Potem kiesy plaskie.
- Kiesa nie moze spadac w sposob ciagly: jedyny odplyw, ktorego wies nie wybiera, to czynsz (10 tys./d swiata). Oczekiwane wsie < 1000 zl: <= 70 (poziom BAZY).

### 9.4 Korona i panowie po F1 (wsie), g = 1, zl/d

Statycznie = przed obiegiem; po obiegu = pieniadz wsi wydany w miastach wraca przez dzisiejsze pobory miast (zawor, P1, podatek od skarbca) do panow i korony
w udziale korony 0.57-0.61 (KS 516.7 tys. korona / 326.7 tys. panowie z poborow miast, 28 dob).

| | KS 120 dob | KS 28 dob | BAZA 28 dob | JEFF 28 dob |
|---|---|---|---|---|
| wplywy dnia korony + 1/450 zapasu | 680.0 tys. | 659.1 tys. | 645.2 tys. | 668.5 tys. |
| korona statycznie (danina - podatek od oszcz. + W4) | -64.6 tys. (-9.5%) | -48.0 tys. (-7.3%) | -86.7 tys. (-13.4%) | -96.6 tys. (-14.5%) |
| panowie statycznie (czesc podatku od oszcz. + W7) | -18.1 tys. | -12.5 tys. | -26.9 tys. | -33.4 tys. |
| wsie zatrzymuja / z tego do miast | 91.3 / 82.6 tys. | 66.3 / 57.9 | 126.2 / 116.7 | 145.8 / 136.6 |
| korona po obiegu | -15.7 tys. (-2.3%) | -13.2 tys. (-2.0%) | -20.3 tys. (-3.2%) | -20.9 tys. (-3.1%) |
| panowie po obiegu | +13.9 tys. | +9.5 tys. | +21.0 tys. | +24.7 tys. |
| razem panowie + korona po obiegu | -1.8 tys. | -3.7 tys. | +0.7 tys. | +3.8 tys. |

Rampa 28 dob (stare pobory wsi slabna liniowo, nowe rosna), obieg jako opoznienie 1 rzedu 14-28 dob (final/ramp.py):
- najgorszy dzien korony = doba 28: KS -36 do -46 tys. (-5.3 do -6.8%), BAZA -45 do -58 tys. (-6.9 do -9.0%), JEFF -51 do -68 tys. (-7.9 do -10.4%);
- doba 56: KS -19 do -27 tys., BAZA -22 do -33, JEFF -24 do -37; doba 90-180: -16 do -25 tys. (-2.5 do -3%);
- panowie: doba 28 od -12 do +1 tys., od doby 56 +7 do +21 tys., od doby 90 +12 do +24 tys.
Kto to niesie: zwrot zoldu idzie przed rentami, wiec spadek korony zjadaja renty 180 (dzis 222-283 tys./d) - armie nie traca. W krolestwach bez rent
(KS 23% krolestwo-dob) dziala K5 dopiero w F3; w F1 - jak dzis - Bank 186 (luka w tych krolestwach to ich udzial w -36..-68 tys., gros w duzych krolestwach z rentami).

### 9.5 Korona po F3 (swiat, statycznie, bez obiegu i bez odnowy firm), zl/d, 28 dob

Znika z wplywow dnia: P1, danina miast, danina wsi, stary 184 i powinnosci 3% (zastapione); znika z doplywu do zapasu: podatek od skarbca ludu (czesc korony).
Firmy = dzisiejszy zawor (pan i korona bez zmian w chwili przelaczenia). Potrzeba przed rentami = zwrot + W5 + M1 + M3 + dary + kontrakty + rata 186 + reparacje + dwor.

| | KS | BAZA | JEFF |
|---|---|---|---|
| dzis: wplyw dnia + zapas / renty 180 | 659.1 / 222.0 tys. | 645.2 / 230.9 tys. | 668.5 / 283.3 tys. |
| potrzeba przed rentami (w tym zwrot) | 445.8 (328.4) tys. | 437.8 (332.7) | 396.0 (316.2) |
| znika: P1 / danina miast / danina wsi / 184 / powinnosci | 190.2 / 43.2 / 47.7 / 13.5 / 4.6 tys. | 149.1 / 37.8 / 77.8 / 11.8 / 4.6 | 146.8 / 35.6 / 88.2 / 10.3 / 7.4 |
| g=1: 184 / pomoc od sprzedazy / dziesiata od ruchomosci / W4 / clo x2 / pomoc lennikow | 34.1 / 43.5 / 14.0 / 16.0 / 21.0 / 15.3 tys. | 32.2 / 35.4 / 12.2 / 17.9 / 22.3 / 15.5 | 27.8 / 32.4 / 12.7 / 17.3 / 20.5 / 24.5 |
| g=1: wplyw / renty 180 | 503.9 tys. (76%) / 58.1 tys. | 499.6 (77%) / 61.8 | 515.3 (77%) / 119.3 |
| g=1.5: wplyw / renty 180 | 557.7 tys. (85%) / 111.9 tys. | 548.4 (85%) / 110.6 | 560.4 (84%) / 164.4 |
| g=2: wplyw / renty 180 | 611.5 tys. (93%) / 165.7 tys. | 597.3 (93%) / 159.5 | 605.4 (91%) / 209.4 |

Bazy (KS 28 dob): zarobek W 617 / R 127 / P 112 / podzamcza 78 tys. zl/d (z podatkow pana 1/2/3%); sprzedaz kas 871 tys. zl/d (lud 416 + markietani 344 +
wsie 81 + rynek BK 29; bez partii i karawan - ostroznie); oszczednosci ludu ponad zapas 12.15 mln (+ kasy ponad 1.5 x zapas 0.55 mln).

Panowie przy g = 2 (statycznie): renty 180 -56.3 tys. (KS), -71.4 tys. (BAZA), -73.9 tys. (JEFF); pomoc lennikow -10.7 / -10.8 / -17.2 tys.;
razem -67 / -82 / -91 tys. = -8 / -10 / -10% D stalego (816 / 819 / 923 tys.) = -4 / -5 / -6% D169. Z F1 (+9.5 / +21 / +25 tys.) i obiegiem w F3
(pieniadz ludu wydany na targu placi pomoc od sprzedazy i 184 od wyplaty kupcow - DZ szacowal +37 tys./d) -> ok. -2 do -4% D stalego. Pulap wojenny
(0.6 D + 0.8 (G - R)/45, dzis 1.57 mln, zold 54% pulapu) spada o 1.5-3%; garnek W5 (wplywy wojenne dnia: KS 108 tys. przy g = 1, 215 tys. przy g = 2) jest wiekszy
niz potrzeba W5 (KS 103 tys., BAZA 116 tys., JEFF 64 tys.; dzis garnek = P1 190 tys.), wiec domy z potrzeba sa pokryte przed rentami.

### 9.6 Korona po F3 - krolestwa (KS, 28 dob, statycznie; tys. zl/d)

Przydzial do krolestw: danina miast wedlug nadwyzki kas, danina wsi i W4 wedlug kies wsi, sprzedaz kas i 184 wedlug zarobku ludu, ruchomosci wedlug nadwyzki ludu,
clo i pomoc lennikow wedlug D stalego (szacunek; dokladnie policzy symulator).

| krolestwo | dzis | g=1 | g=1.5 | g=2 | potrzeba | renty dzis | luka przy g=2 | kredyt 186 dzis |
|---|---|---|---|---|---|---|---|---|
| King's Landing | 76.7 | 53.4 | 58.0 | 62.7 | 44.9 | 36.5 | 0 | 0 |
| Riverlands | 69.2 | 45.2 | 48.8 | 52.4 | 37.1 | 38.6 | 0 | 0 |
| The Reach | 66.2 | 48.1 | 53.4 | 58.8 | 37.0 | 32.1 | 0 | 0 |
| Dorne | 58.9 | 34.4 | 38.0 | 41.5 | 37.3 | 28.3 | 0 | 0 |
| The North | 43.6 | 34.7 | 38.3 | 42.0 | 35.3 | 11.5 | 0 | 0 |
| The Vale | 34.4 | 35.2 | 38.5 | 41.8 | 29.1 | 10.7 | 0 | 0 |
| Targaryen | 32.0 | 32.6 | 35.8 | 39.1 | 19.9 | 11.4 | 0 | 0 |
| Qohor | 24.9 | 16.8 | 18.6 | 20.5 | 16.0 | 7.0 | 0 | 0 |
| Myr | 23.3 | 14.9 | 16.7 | 18.4 | 17.1 | 5.6 | 0 | 0 |
| Stormlands | 22.5 | 22.3 | 24.6 | 26.9 | 16.8 | 7.7 | 0 | 0.3 |
| Volantis | 21.9 | 11.8 | 14.2 | 16.5 | 20.4 | 0.0 | 3.8 | 0 |
| Dothraki | 21.4 | 22.6 | 24.0 | 25.5 | 17.1 | 6.5 | 0 | 0 |
| Iron Islands | 19.3 | 8.4 | 9.5 | 10.7 | 12.8 | 1.0 | 2.1 | 0 |
| Ibben | 17.1 | 10.3 | 11.1 | 12.0 | 10.9 | 5.3 | 0 | 0 |
| Qarth | 16.7 | 18.1 | 20.1 | 22.2 | 10.3 | 0.7 | 0 | 0 |
| Braavos | 15.6 | 13.1 | 14.8 | 16.5 | 13.2 | 0.5 | 0 | 0.9 |
| Dragonstone | 15.6 | 8.5 | 9.5 | 10.4 | 12.4 | 3.2 | 1.9 | 0.3 |
| Tyrosh | 15.3 | 10.0 | 11.1 | 12.2 | 9.2 | 3.2 | 0 | 0.4 |
| Targaryen, Aegon | 14.2 | 8.3 | 9.4 | 10.4 | 10.6 | 2.9 | 0.2 | 0.1 |
| Norvos | 10.5 | 7.4 | 8.6 | 9.8 | 6.4 | 0.3 | 0 | 0 |
| Sarnor | 9.0 | 5.0 | 5.5 | 6.1 | 8.3 | 1.3 | 2.2 | 0 |
| Pentos | 7.6 | 6.6 | 7.7 | 8.7 | 7.1 | 0 | 0 | 1.1 |
| Lorath | 5.5 | 3.2 | 3.8 | 4.5 | 6.3 | 0 | 1.8 | 3.1 |
| Lys | 4.9 | 3.5 | 4.0 | 4.6 | 4.8 | 0 | 0.2 | 2.9 |
| reszta (Nights Watch, Free Folk, Yi Ti, Summer Isles, Skagos) | 12.8 | 13.4 | 14.3 | 15.1 | 5.5 | 7.8 | 0 | 0.4 |
| RAZEM | 659.1 | 487.6 | 538.4 | 589.3 | 445.8 | 222.0 | 12.4 | 9.6 |

Luki: g = 1 - 33.9 tys./d w 13 krolestwach; g = 2 - 12.4 tys./d w 7 (Volantis 3.8, Sarnor 2.2, Iron Islands 2.1, Dragonstone 1.9, Lorath 1.8, Aegon 0.2, Lys 0.2).
Dzis Bank pozycza koronom na zwrot 9.6 tys./d (12.3% krolestwo-dob), wiec luka przy g = 2 jest tej samej wielkosci co dzisiejszy kredyt.

### 9.7 Kto pokrywa luki przy g = 2 (K5 -> L1 -> L3)
- Skarbiec ponad zapas wojny (60 dni zwrotu, najmniej 500 tys.; skarbce z konca KS): Volantis 2.33 mln (zapas 0.69 mln) -> luka 3.8 tys./d na ok. 430 dob; Sarnor 0.71 (0.5) -> ok. 95 dob;
  Lys 0.58 (0.5) -> ok. 400 dob; Aegon 1.30 (0.5) -> lata; Dragonstone 0.61 (0.53) -> ok. 40 dob; Lorath 0.55 (0.5) -> ok. 26 dob; Iron Islands 0.55 (0.5) -> ok. 24 dob.
- Potem zaliczka mieszczan pod nadanie (L1) i Bank (L3). Pula Banku dla koron (kapital 3.90 mln - 2 mln) / 60 = ok. 32 tys./d - luki sie mieszcza.
  Kapital Banku i tak spada 3.5-7.3 tys./d (stare dlugi rodow) - K5 zmniejsza pozyczki koron i spowalnia ten spadek; zastaw cla (L3) przyspiesza splaty.
- Iron Islands: polowa ich zoldu to lup (dzis tez sa najslabsze, 14% stalego dochodu/zold) - w F4 "dziesiecina zelaznych" 1/10 lupu dla korony.

### 9.8 Kupcy KS-H (F2)
KS 120 dob / 28 dob: zamowione 15.4 / 19.5 tys. szt./d, kupione 10.4 / 12.8, zablokowane "kasa celu za mala" 6.4 / 8.6 (42% / 44%); zloto towaru 31.8 / 41.8 tys. zl/d,
fracht 1.2 / 1.65 tys. Po F2: zablokowane 0, kupione do 15.4-19.5 tys. szt., zloto ok. 50-63 tys. zl/d, rachunki otwarte ok. 0.1-0.4 mln.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 10. KOD (specyfikacja; nic nie zmieniono)

### 10.1 F1 - wsie (wymaga KS zastosowanego dla wsi)

Nowy plik `DsVillage.cs` (static partial class DsVillage):
- `On(Settlement st)` = DuesMedieval && st.IsVillage && StartCalibration.AppliedFor(st.Village); `KsOn(Village v)` = On && KsAlphaRules.
- `G(Kingdom k)` = F1: wojna ? (WarTax ? 1.5 : 1) : 0; F3: z DsSeason.
- `DuesAtCart(Village home, long villagePart, long handed)`: dziesiecina 5% handed (najwyzej villagePart) -> istniejaca sciezka C3 TownFolk.Tithe (nowa metoda
  TitheVillageAtCart: polowa jalmuzna wraca do kiesy, polowa septy); korona g/10 x (villagePart - dziesiecina) -> k.KingdomBudgetWallet, KingdomTreasury.LeviesToday,
  DailyLife.NoteVillageLevy; remisja: VillageState != Normal, Bound oblezony, Famine.IsHungry.
- `WarShareSutlers(Village v, int purse)`: to samo g/10 od czesci markietanow w kiesie.
- `RentPay(Settlement st, float due, int gold)`: rata + zaleglosc, najwyzej kiesa; brak -> _arrears[st]; `RentRemit(st)` dni rabunku/oblezenia/glodu;
  zaleglosc > 364 dni czynszu -> darowane (licznik "decasus").
- `Chest(Village v)` = 21 x (koszyk zaplacony/dobe z Planu albo buy1) + 21 x czynsz/dobe; `SurplusBudget(Village v)` = rampa x max(0, gold - Chest)/15 (raz na dobe,
  dzielony: 1/2 P5, 1/2 rynek BK); `NoteSurplusSpent`.
- `Ramp` = min(1, (doba - DayOn)/28) - stare pobory wsi x (1 - Ramp), nowe x Ramp (W4, krok 5) przez pierwsze 28 dob.
- Liczniki i linia logu "Daniny wsi (DS)"; Export/Import `arm_daniny` (zaleglosci, DayOn, srednie O_v, liczniki rampy).

Haczyki w istniejacym kodzie:
| plik:linia | zmiana |
|---|---|
| VillageTakings.cs:227-233 (EnterPostfix) i :237-299 (Whole112) | Whole112 oddaje przez out czesc wsi z okna (kept + purse) i handed; po CoalFirst: `if (DsVillage.On(home.Settlement)) DsVillage.DuesAtCart(home, part, handed)` |
| TownFolk.Tithe.cs:141-185 (petla wsi) | `if (DsVillage.On(f.St)) continue;` - dziesiecina wsi juz przy wozie |
| Sutlers.cs:381-393 | po Split / ChangeGold czesci wsi: `DsVillage.WarShareSutlers(vv, vpurse)`; `StartCalibration.KsVilIn(vv, reszta)` |
| KingdomTreasury.cs:395-412 (Levies) | przed `int gold`: `if (!town && DsVillage.On(st)) { danina x (1 - Ramp) ... ; }` - po rampie wies DS pomijana |
| TownFolk.Guard.cs:341 (PayHoardVillage) | na poczatku: wies DS - podatek x (1 - Ramp), po rampie return |
| KingdomTreasury.LordLevy.cs:251-262, 306 | wies DS nie jest zrodlem W7 |
| PopulationLaw.cs:1286-1303 | wies DS: `pay = DsVillage.RentPay(st, rentNow * decree, gold)`; galaz "rabunek" -> DsVillage.RentRemit(st); dalej ta sama sciezka GiveGoldAction / RentToday |
| TownFolk.Live.cs:1172-1210 | wies DS: extra = 0, bez VillageLadder po koszyku; po zakupach `DsVillage.SurplusSpend(f, vil, mk, d)` (P5 przez VillageLadder z budzetem kroku 5) |
| BkSeal34.cs:113 | `int budget = DsVillage.On(st) ? DsVillage.SurplusBudgetBk(village) : (int)(village.Gold * 0.05f);` + NoteSurplusSpent |
| TownFolk.Loans.cs:203-207 | zrodlo wies DS: nadwyzka = gold - DsVillage.Chest(v) |
| StartCalibration.Core.cs:85, 1001, 1080-1084, 1218 | pole Fix, przeniesienie do MDist, `budget = Math.Max(0.0, v.Alpha * salesB + v.Fix)` |
| StartCalibration.Plan.cs:173 | `kd.Alpha = DsVillage.KsOn(v) ? VilAlphaRules(v, out kd.Fix) : VilAlphaFor(v);` |
| StartCalibration.Money.cs (nowe VilAlphaRules obok :127) | wzor rozdz. 4.1; u_v z _vilTake (a[1]/a[0]); O_v z nowego _vilIn; R_v z TownFolk.VillageFeeToday x dekret |
| StartCalibration.R3.cs:499 (obok KsVilOut) | nowe KsVilIn(v, x) i srednia 28 dob w VilOutDaily |
| ClanBudget.cs:1236-1248, VillageTakings.cs:383-407, Seal31B.cs:1057-1068 | KsVilIn |

### 10.2 F2 - kupcy KS-H (plik KsFoodCarts.cs + nowy KsFoodCarts.Credit.cs)
| plik:linia | zmiana |
|---|---|
| KsFoodCarts.cs:451 (budget[i]) | przy Consign: budzet celu bez limitu kasy; nowe `SrcRoom[s]` = 0.5 x kasa zrodla - otwarte rachunki zrodla; warunek celu bez rachunkow > 30 dni |
| KsFoodCarts.cs:595-633 (Ship) | przy Consign: brak zaplaty celu; fracht z kasy ZRODLA (ladem do pendFreight jak dzis, morzem TownFolk.KsCrewPay(srcT, ...)); Paid = 0 |
| KsFoodCarts.cs:341-380 (Arrivals) | po polozeniu towaru: jesli c.Paid == 0 i Consign -> `Credit.Open(src, dst, wartosc skupu celu)`; zaplata od razu z kasy celu ponad 25% zapasu |
| KsFoodCarts.cs:383-404 (Back) | Paid == 0 - bez zwrotu (nic nie zaplacono) |
| nowe Credit.PayDaily() (wolane w SupplyDemand.DailyTrade przed TradeCarts / przed zaworem) | splaty od najstarszego; wojna -> przepadek; linia "kredyt kupiecki" w "Kupcy zywnosci (KS-H)" |
| ArmouryBehavior.cs (SyncData, obok :605) | `arm_daniny_kupcy` przez SaveText.Sync |

### 10.3 F3 - miasta, zamki, korona (nowe pliki DsTowns.cs, DsSeason.cs)
| plik:linia | zmiana |
|---|---|
| TownPurse.cs:420-492 (Valve), :494 (ValveDay); PopulationLaw.cs:1329 (ValveTowns) | przy DsTowns: zawor nie idzie; zamiast niego raty firm M2/M3 (DsTowns.FarmDay) i wyplata kupcom M6 |
| CastlePurse.cs:171 (DuesOf), :209 (Dues), :613 | przy DsTowns: firma zamku M4 |
| KingdomTreasury.WarAid.cs (P1 caly) | przy DsTowns: wylaczone (FolkWarAid ignorowane) |
| TownFolk.Hoard.cs, TownFolk.Guard.cs:220-300 | miasta i zamki: podatek od skarbca ludu wylaczony; straznik obiegu (FolkCirculationGuard) wylaczony |
| KingdomTreasury.cs:395-412 (galaz miast) | danina 1%/d wylaczona; M7 pomoc od sprzedazy z licznika sprzedazy kasy (TownPurse / TownFolk okno) |
| KingdomTreasury.WarTax.cs:130-136, 243-244 | stawka s = min(pulap, g x 5%) na kwartal z DsSeason; pulap z nowych kluczy |
| KingdomTreasury.cs:97 | stawka wojny = DsVassalAidWarPercent (10) przy DsTowns |
| KingdomTreasury.NeedAid.cs:181 (garnek) | row.Pot = wplywy wojenne dnia DS (M7 + M8 + M9 + W4), najwyzej LeftFor |
| KingdomTreasury.LordLevy.cs (zrodla ludu) | W7 wylaczone przy DsTowns (wliczone w firme) |
| TownFolk.Live.cs:994-996 | lud: 1/15 dziennie oszczednosci ponad zapas (rampa) zamiast 15%/d |
| CrownIncome.cs (krok zwrotu, przed CrownBorrow.Lend) | K5: brak na zwrot najpierw ze skarbca ponad zapas wojny (KingdomTreasury zapas wojny 60 dni), potem L1, potem 186 |
| TownFolk.CrownLoans.cs:66-67 i wyzwalacz | rodzaj 'a' (zaliczka pod nadanie): limit = niezebrane raty nadania; splata pierwsza z rat |
| CrownBorrow.cs:337 | limit = (DsCrownLoanLimitDaysPledged gdy zastaw cla, inaczej CrownLoanLimitDays) x sredni podatek; zwloka -> licznik cel krolestwa do Banku |
| ArmouryBehavior.cs SyncData | `arm_daniny_miasta`: firmy (F_L, F_C, doba ustalenia, zaleglosci), oceny M9, raty, g krolestw |

### 10.4 Klucze MCM (wszystkie NOWE; zadna istniejaca wartosc nie zmienia domyslnej - pulapka MCM z CLAUDE.md)
F1: DuesMedieval true (dziala tylko przy KS zastosowanym), DsVillageCartDues true, DsWarShareVillage 0.10 (na jednostke g), DsRentArrears true, DsRentForgiveDays 364,
DsRentRemitRaided true, DsVillageChestDays 21, DsSurplusSpendDays 15, DsSurplusBkShare 0.5, DsRampDays 28, KsAlphaRules true.
F2: KsFoodCartsConsign true, KsConsignFloorShare 0.25, KsConsignSourceExposure 0.5, KsConsignOverdueDays 30.
F3 (domyslnie false do testu): DsTowns false, DsFarmMeasureDays 28, DsFarmRenewDays 364, DsFarmRenewMax 0.15, DsFarmDistraintTerms 2, DsMerchantDrawDays 28,
DsMerchantDrawPShare 0.7, DsAidesRate 0.05, DsAidesMax 0.10, DsTallageRate 0.05, DsTallageMax 0.10, DsTallageMaxPolicy 0.15, DsMovablesRate 0.10,
DsCustomsWarMultiplier 2, DsVassalAidWarPercent 10, DsGrantMax 2.0, DsGrantStep 0.5, DsGrantRentsKeep 0.5, DsRefundTreasuryFirst true,
DsAnticipationLoans true, DsCrownLoanLimitDaysPledged 365, DsFolkSurplusSpendDays 15.
Stare klucze (LaySubsidy*, FolkHoard*, FolkWarAid*, TownRentShare, CastleDues*, WarTaxMax, CrownDuesWarPercent, KsAlphaNet...) zostaja w pliku Jeffa
z wartosciami - DS je omija, a wylaczenie DS je przywraca.

### 10.5 Zapis (SaveText.Sync, kazdy < 32 KB)
- `arm_daniny` (F1): "v~id~zaleglosc~doba" na wies z zaleglosci (najwyzej 571 x ok. 30 znakow = 17 KB), "o~id~O~m" srednie O_v, "r~DayOn", liczniki.
- `arm_daniny_kupcy` (F2): rachunki "zrodlo~cel~zostalo~doba".
- `arm_daniny_miasta` (F3): firmy na miasto/zamek, oceny M9 i raty, g krolestw, zaliczki 'a' (albo w arm_korona2 z nowym rodzajem).
Brak klucza = zera. Starsza DLL nie zna kluczy i je pomija.

### 10.6 Linie logu
- "Daniny wsi (DS): dzien N | wozy: oddane H, pan i majatki (bez zmian) L, dziesiecina T (jalmuzna A, septy B), korona przy wozie C (g krolestw, wsi zwolnionych: rabunek/oblezenie/glod)
  | markietani: korona C2 | czynsz: nalezny, zaplacony, zaleglosci Z u n wsi, darowane po roku, wstrzymane | skrzynia: kiesy, cel skrzyni, ponad skrzynia, zbytki z nadwyzki (P5 / BK), rampa
  | stare pobory wsi DS (oczekiwane 0 po rampie): danina, podatek od nadwyzki, W7 | kontrola: platnicy - odbiorcy = 0".
- "KS: pieniadze planu" + "alfa z praw (DS) min/med/max, czesc stala (O - R) swiata, g" obok "alfa zmierzona".
- "Kupcy zywnosci (KS-H)" + "kredyt kupiecki: otwarte X zl (n), zaplacone przy dostawie, splacone, starsze niz 30 dni, przepadle (wojna)".
- F3: "Firmy miast i zamkow (DS)", "Korona: nadanie wojenne (DS) - krolestwo g, prognoza, zwykle, wojenne, potrzeba, renty, skarbiec ponad zapas, zaliczki, Bank", "Pozyczki zaliczkowe (DS)".

---------------------------------------------------------------------------------------------------------------------------------------------------

## 11. Stare zapisy (Ironman Jeffa)

- Gra Jeffa dzis (WGRANIE 74 bez KS): F1 i F2 nie dzialaja (wymagaja KS zastosowanego). Nic sie nie zmienia, dopoki KS nie zostanie zastosowany na jego zapisie
  (dzis Apply zablokowane, wymaga 28 dob sesji).
- Pierwsze wczytanie z nowa DLL: brak `arm_daniny*` = zera; F1 startuje z rampa 28 dob od doby zastosowania KS (stare pobory wsi slabna, nowe rosna).
- F3 (DsTowns): wlaczenie zaczyna okno pomiaru 28 dob - stare zasady dzialaja dokladnie jak dzis, log "Daniny: pomiar firm dzien N/28"; w dobie 28 firmy
  ustalone ("Daniny: firmy ustalone: 97 miast, 130 zamkow, sumy"), pierwsze nadanie w najblizszy dzien kwartalu, lud: rampa zbytkow 28 dob.
  Okno F3 ma byc PO co najmniej 28 dobach F1 - wtedy firmy zawieraja pieniadz wsi, ktory wrocil przez miasta.
- Wylaczenie DS: stare zasady wracaja nastepnego dnia; zaleglosci i rachunki zostaja w zapisie bez skutku (rachunki KS-H splacane dalej, zeby nic nie zniknelo).
- Nowa kampania: KS kalibruje start z regulami DS (alfa z praw od pierwszego Planu), F3 ustala firmy z pierwszych 28 dob.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 12. Plan wdrozenia (tor L - pelny lancuch)

| runda | zakres | kroki | czas (szac.) |
|---|---|---|---|
| R4 (razem z KS runda 4) | F1 + KS krok 8 z praw + F2 | 1) galaz od w-toku/noc-ks3 (z c1f61987); 2) symulator ksim: nowy budzet, kolejnosc wydatkow wsi, kredyt KS-H - bramki S1-S4; 3) kod DsVillage.cs + haczyki 10.1 + KsFoodCarts 10.2; 4) build rc 0; 5) 2 recenzje (ekonomia + kod/zapis); 6) autotest 120 dob z flaga KS (armoury-ks-wlacz.flag, potem USUNAC) + 1 bitwa; 7) sprawdz_logi: nks, nzc0-2, nbitwa, nowe nds; 8) wgranie po udanym tescie (kopia DLL; nigdy, gdy Jeff gra) | 1 noc |
| R5 | F3 miasta, zamki, korona, kredyt | 1) symulator dsim (rozdz. 13), walidacja na starych zasadach, potem DS; 2) kod DsTowns.cs, DsSeason.cs + haczyki 10.3; 3) 2 recenzje; 4) autotest 120 dob z kopii zapisu Jeffa i z zapisu KS (F3 wlaczone od doby 1 = okno 28 dob + 92 doby DS); 5) bramki nd3; 6) wgranie | 1-2 noce |
| R6 | F4 | markietani kupuja jedzenie wsi (z popytem w Planie), zajecie towaru za zaleglosci, podatek od bogatych -> dziesiata od ruchomosci panow, dziesiecina zelaznych z lupu | pozniej |

---------------------------------------------------------------------------------------------------------------------------------------------------

## 13. Symulator (przed kodem) i jego bramki

SIM-A (F1 + F2): rozszerzenie ksim (scratchpad KS, 97 miast, runda 3): budzet wsi = AlfaR x sprzedazB + Stala; kolejnosc wydatkow wsi (czynsz, koszyk, skrzynia 21 dni,
zbytki 1/15 z rampa); W4 przy g = 1 w krolestwach w wojnie; KS-H na kredycie (rachunki, splaty ponad 25% zapasu, limit zrodla 50%). Start: ten sam zrzut KS co runda 3. 120 dob.
- S1: Plan zbiezny (Westeros i Essos TAK) w <= 14 rundach; mediana alfy z praw >= 0.40 w obu rynkach, p10 >= 0.30; budzet swiata 140-180 tys./d.
- S2: start 95-105% kazdego towaru (bilans na targ) po Apply i po 28 dobach.
- S3: kiesy wsi: dryf 60 dob >= -0.2%/d stanu; mediana >= 0.9 x start; wsi < 1000 zl <= 15%.
- S4: KS-H: zablokowane przez cel = 0; rachunki otwarte <= 10 dni dostaw; starsze niz 30 dni <= 10%; Pentos dostaje zboze.

SIM-B (F3): nowy dsim - model zasobow i przeplywow na 29 krolestw: wsie (zbiorczo na miasto targowe), kasy miast i zamkow, lud W/R/P/podzamcza, rody (zbiorczo
na krolestwo + rody z potrzeba W5), skarbce, Bank. Przeplywy z pomiaru 28 dob (KS, BAZA, JEFF): zold -> sakiewki -> markietani (85/15) i zycie w miastach -> kasy ->
robocizna, obrot P, doplata 35% -> lud -> koszyk i zbytki -> kasy; utarg wsi (udzial pana, majatki, dziesiecina, W4); D rodow; kolejnosc wydatkow korony K4/K5;
kredyt L1-L3; nadanie g co 91 dob. 360 dob wiecznej wojny + scenariusze: pokoj od doby 180; nieurodzaj -30% przez 91 dob; 10% miast oblezonych 28 dob;
kapital Banku przy podlodze 2 mln.
- S0 (walidacja): na STARYCH zasadach dsim odtwarza okno 28 dob z dokladnoscia +-5%: wplyw dnia korony, renty 180, P1, zawor, dryf kies wsi i ludu.
- S5: zwrot zoldu 100% kazdego dnia w kazdym krolestwie; renty 180 swiata >= 50% okna; krolestwo-dob na kredycie Banku <= 15%; kapital Banku >= 2 mln w dobie 360;
  skarbiec ponizej zapasu wojny > 30 dob z rzedu tylko Iron Islands i Dragonstone (lista jawna).
- S6: D stale rodow >= 0.92 x okna w dobie 90 i >= 0.95 w dobie 360; domy z potrzeba W5 <= 76.
- S7: lud <= 12% zlota swiata; kasy ponad zapas kupcow >= 90% miast; zaleglosci firm <= 10% miast; g srednie <= 2 (nikt nie siedzi na 2 i luce > 1 rok poza lista).
- S8: zamkniecie: suma wszystkich zasobow stala co dobe (reszta 0 zl).
- S9: pokoj od doby 180 -> g = 0 w 1 kwartal, dlugi koron spadaja; nieurodzaj -> zaleglosci czynszu <= 20% wsi, alfa >= 0.25; oblezenia -> zaleglosci firm splacone w 91 dob.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 14. Bramki sprawdz_logi

Grupa nds (F1 + F2, test R4):
- NDS-1: wsie DS po rampie: "Podatek od nadwyzki wsi" 0, danina wsi w "Przeplywy osad" = 0 (korona tylko przez "Daniny wsi (DS)"), W7 ze wsi 0, rynek BK wsi tylko z kroku 5.
- NDS-2: "Daniny wsi (DS)" kontrola 0 kazdej doby; MoneyLedger "bez odbiorcy" 0.
- NDS-3: kiesy wsi ostatnie 60 dob: dryf >= -0.2%/d; mediana >= 0.9 x start; wsi < 1000 zl <= 15%.
- NDS-4: "alfa z praw" mediana >= 0.40 W i E w kazdym Planie po pierwszym; brak spadku mediany > 0.05 z Planu na Plan bez zmiany g; alfa 0 tylko wsie w rabunku/oblezeniu, <= 3%.
- NDS-5: nks bez zmian (95-105%), Plan zbiezny.
- NDS-6: wsi z zaleglosci > 91 dni czynszu <= 10%; darowane po roku - w logu.
- NDS-7: wplyw dnia korony + zapas: w rampie (doby 1-56) >= 0.85 x okna przed wlaczeniem, od doby 57 >= 0.95; zwrot 100%; krolestwo-dob na kredycie Banku <= 15%.
- NDS-8: KS-H "kasa celu za mala" 0; rachunki otwarte <= 0.5 mln; starsze niz 30 dni <= 10%; Pentos dostaje zboze; kontrola towaru i zlota 0.
- NDS-9: D stale rodow >= 0.95 x okna; nieoplacone partie <= 1/dobe.

Grupa nd3 (F3, test R5):
- ND3-1: zawor miast i zamkow, P1, podatek od skarbca ludu, danina miast = 0 w logu; firmy zaplacone >= 90% naleznych; miasta z zaleglosci firm <= 10%.
- ND3-2: g kazdego krolestwa w logu co kwartal; zwrot 100% w >= 97% krolestwo-dob; renty 180 >= 50% okna; kredyt Banku <= 15% krolestwo-dob.
- ND3-3: lud <= 12% zlota swiata, korony 19-22%; kasy ponad zapas >= 90% miast.
- ND3-4: D stale >= 0.92 x okna; domy z potrzeba W5 <= 76; glowy rodow < 5000 zl <= 15; nieoplacone partie <= 1/dobe.
- ND3-5: zaliczki L1 splacone w kwartale >= 90%; dlug koron u mieszczan i w Banku w limitach; 0 niesplat w 120 dobach.
- ND3-6: MoneyLedger "bez odbiorcy" 0; "Firmy miast i zamkow (DS)" kontrola 0.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 15. Ryzyka i sprawy otwarte

1. Dziura korony w rampie F1 (doba 28: -5 do -10% wplywow) - niosa ja renty 180; w krolestwach bez rent (Lorath, Lys, Volantis, Pentos...) Bank. Gdyby NDS-7 nie przeszlo:
   rampa 56 dob albo W4 przy g = 1.5 do konca rampy.
2. Obieg przez miasta w F1 opiera sie na DZISIEJSZYCH poborach miast (zawor, P1) - ktore F3 usuwa. Dlatego okno firm F3 musi byc po >= 28 dobach F1 (firmy "wchlaniaja"
   wtedy pieniadz wsi), a nadanie g pokrywa reszte.
3. Wies bez KS nie ma na co wydac uwolnionych pieniedzy (stara lista 0.07 zl/glowe/dobe, kupuje 6.8-13.4 tys./d z braku towaru) - dlatego F1 tylko z KS.
4. Zbytki i towar: wiecej zakupow wsi (koszyk 140-180 tys./d zamiast 54-81) i ludu (bez P1) podnosi ceny - zamierzone, ale w pasmie KS (nks) i bramka ludu 12%.
5. Wieczna wojna: g = 2 bedzie norma w duzej czesci krolestw. To jest "podniesienie" opisane w rozdz. 8; male krolestwa (Volantis, Sarnor, Iron Islands, Dragonstone,
   Lorath) i tak pozycza - jak dzis.
6. Firmy na dzisiejszym poziomie (96% doplywu kupcow): miasto oblezone albo spladrowane szybko wpada w zaleglosci; remisja rat za oblezenie i lup, zajecie akcyzy dopiero po 91 dobach.
7. Kapital Banku spada 3.5-7.3 tys./d niezaleznie od DS (stare kredyty rodow) - osobna sprawa (zysk Banku do kapitalu, zastaw cla).
8. Przydzial liczb do krolestw w 9.6 to szacunek (bazy z CSV, proporcje) - liczby dokladne da dsim (S0 waliduje).
9. Liczby JEFF z 16.10 (build <= WGRANIE 67, wlasne zyski rodow 38 tys. wobec 172-185 tys. w testach) - przed R5 przydalaby sie sesja Jeffa na WGRANIE 74 z dobami.
10. Niepotwierdzone zrodla historyczne: stawka aides 1360, mlynarskie 1/16, "kilka %" w latach szczytowych - uzyte tylko jako tlo, liczby gry z logow.
11. Markietani bez towaru (15% wydatkow zolnierzy, 38-47 tys./d) - w DS nazwane zaplata za kwatery i uslugi; zakup prawdziwego jedzenia - F4 po KS.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 16. Zrodla historyczne (skrot z audytu historii, daniny/hist)

- Campbell, Past & Present 188 (2005): panowie ok. 1300 - 18-23% dochodu netto z ziemi; renty, dziesieciny, banalia i kary razem najwyzej 1/8 dochodu wsi.
- Dziesiecina 1/10 plonu (w naturze, w polu); champart ok. 1/8 (1/6-1/12); mlynarskie 1/30-1/10.
- Subsydium (dziesiata i pietnasta): 1/15 wies, 1/10 miasta; 1334 stale kwoty wspolnot (38 tys. funtow); ulgi dla zubozalych miast od 1433.
- Edward I 1294-97: nadania co rok (1/10 i 1/6, 1/11 i 1/7, 1/12 i 1/8, 1/9); 1340 "dziewiata" snopow, runa i jagniat na 2 lata.
- Clo: 1275 pol marki za worek welny, 1294 i 1336 maltolt 40 szylingow (x6).
- Kredyt: Ricciardi (clo 1275), Bardi i Peruzzi (pozyczki pod clo, upadek 1343-46, ugoda 30% / 20%), syndykat de la Pole 1337, Beaufort; Florencja Monte 5% (1343-45),
  Wenecja prestiti 5%; Fuggerowie 1519; bankructwo Hiszpanii 1557.
- Kupcy zbozowi: wlasny kapital i kredyt, zaplata ze sprzedazy w miejscu docelowym (Londyn - Henley; Exeter 1319); statut kupiecki 1283/1285.
- Westeros: korona winna ponad 6 mln smokow (Lannisterowie, Tyrell, Bank, kartele Tyrosh, Wiara); Bank z Braavos nie daruje - wspiera rywala dluznika.
