

# TEMAT

## cause
Przyczyna glowna: wies DS w kroku 5 kupuje sol i narzedzia, ktorych nie potrzebuje, a ksiega nie przypisuje tego zakupu do zadnego rynku.

DsSurplus (TownFolk.Live.cs:1249) to krok 5 wsi DS: wydatek z nadwyzki ponad skrzynie. Wolany jest w bloku finally petli Villages (TownFolk.Live.cs:1237). W czesci 2 przy VillWorkOnly()==WardsShopOn (domyslnie TAK) caly reszta-budzet idzie wylacznie na sol i narzedzia: BuyGoods(vil, mk, list, 0, d, rest) (Live.cs:1275-1276, VillWorkCat w TownFolk.Luxury.cs:396). BuyGoods nie ma limitu potrzeby ani ceny: n = min(polka-5, budzet/cena) (Live.cs:1407, 1432). Wies kupuje cala polke do ShelfKeep=5 po kazdej cenie. Towar znika, bo nic nie trafia do magazynu wsi.

Ten zakup to drugi raz to samo zjawisko. Sol i narzedzia sa juz w koszyku ZC4 (KsBasket.Ids: salt, tools). Drabina 1 kupuje sol zaplacona z Planu, drabina 2 kupuje reszte z limitem ceny i progu polki. G4 swiadomie wylacza stary wydatek z nadwyzki na sol i narzedzia przy ZC4 (leakCut, Live.cs:1196-1197), a DsSurplus przywraca go tylnymi drzwiami.

Druga strona tego samego bledu to ksiega. To jedyny zakup wsi BEZ wlasnej ramki GoodsLedger.Begin(FFolkVill, mk); porownaj Live.cs:1210 i 1361, TownFolk.Ks.cs:207 i 298, Luxury.cs:455. Ruch soli laduje wiec w zewnetrznej ramce bez osady GoodsLedger.Begin(FFolkVill) (ArmouryBehavior.cs:1894). MkOfFrame daje wtedy 2, a Book (GoodsLedger.cs:651) zapisuje to "bez rynku". KsTake odpada na st==null (GoodsLedger.cs:595), wiec KS-H nie widzi tego w odbiorze 7 dob. Sterowanie rynku czyta tylko kolumny Westeros i Essos: kv.Value[3+m] w StartCalibration.R3.cs:214.

Skutki:
- linia "KS: rynki" gubi okolo 60% zuzycia soli, stad S/C 2.5-4.5 i "bez rynku" 160%;
- polki prawie wszystkich miast stoja na 5 sztukach, stad cena przy suficie;
- wsie ida w kolejnosci petli, wiec krok 5 bogatej wsi zdziera polke, zanim nastepna wies kupi swoj zaplacony koszyk ZC4 (wypieranie);
- KS-H widzi wieczny prawdziwy brak (kx 2.5) i wiezie sol, ktora krok 5 od razu kupuje;
- sterowanie widzi pozorna nadwyzke i tnie popyt soli w Planie (Essos 0.927 -> 0.862).

Czyli nadwyzki soli NIE ma. Swiat jest zbilansowany (S/C 1.04), a anomalia to blad pomiaru plus realny sztuczny ujsciowiec. Wzor ceny to wzor gry (Supply z InStoreValue wobec Demand kategorii gry, StartCalibration.Ramp.cs:213-228). Nie czyta linii rynku: sufit to efekt pustej polki, a nie innego popytu.

## evidence
Dowody z logow (fin80, D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_16-26-10.log):

1. Suma zgadza sie co do sztuki. Doba 109194: rynek Westeros zuzycie 448 + Essos 290 + bez rynku 1533 = 2271. To jest dokladnie "Towary: dzien 109194 - salt ... zuzycie 2271 [mieszczanie 106; rzemioslo 2; wsie (zakupy w miescie, E5/B1) 2163]". Okolo 71% zakupow wsi idzie "bez rynku".

2. "Bez rynku" w ostatnich dobach (109190-109195) to TYLKO sol (544-1909/d) i narzedzia (36-106/d). To sa dokladnie dwie kategorie VillWorkCat. Zboze i reszta "bez rynku" sa male.

3. Okno D+22..D+49 (doby 109140-109167, 28 dob):
- produkcja: Westeros 834/d, Essos 796/d;
- zuzycie rynku: Westeros 328/d, Essos 275/d, stad S/C rynku 2.54 / 2.90 (= NKS-5);
- zuzycie bez rynku: 968/d;
- S/(C + bez rynku) na swiat = 1.04, czyli zbilansowane.

4. Linia "Towary 109195 salt": posiadacze miasta 1537 (1.0 dnia), wsie 6233, wozy 4752, karawany 2716, kupcy w drodze 6817. Zapas swiata 22169 i rosnie (+139/d). Sol lezy w drodze, a nie na polkach.

5. CSV ks-miasta-towary.csv, sol, doby >= 109160:
- polka <= 5 szt.: Westeros 87% wierszy, Essos 70% (mediana 3-4 szt.);
- kx = 2.5 (maksimum, tryb prawdziwego braku): mediana; >= 2 w 97% (Westeros) i 88% (Essos);
- popyt zaplacony Planu: Westeros 820/d, Essos 722/d (razem 1542 = cale realne zuzycie 1537);
- odbior 7 dob: tylko 352 i 270, bo krok 5 wypiera koszyki ZC4;
- w drodze: Westeros 1183, Essos 1060.

6. NKS-27: sterowanie tnie sol w Essos (0.927 -> 0.862) mimo pustych polek.

7. Linia DS "krok 5 wydany: towar" 12-15 tys. zl/d. To pasuje do okolo 1000-1500 soli po okolo 5 zl plus 40-100 narzedzi.

8. fin78 (a6c56999) ma ten sam obraz: bez rynku 450-2800/d, czyli przyczyna nie lezy w KsRecipes, skrzyni ani spichlerzu.

9. Uwaga ZC1 "sol woz/miasto 0.17": w fin80 w oknie woz dostaje 2.85 zl wobec ceny przy wjezdzie 5.20, czyli stosunek 0.55, przy 1423 szt./d sprzedanych wozami. Sol z 10 miast targowych wsi solnych jest wysypywana w tych miastach. Absorb (MarketCarts.cs ok. 605-618) daje pelna cene tylko za wczorajsze zakupy wsi tam, reszta schodzi po spadajacej cenie gry. To skutek geografii (10 z 97 miast), a nie zrodlo anomalii.

10. Kasy 10 miast solnych: 854k -> 907k w 56 dob (okolo +950/d) - bez istotnego akumulatora.

## fix
Dwa kroki, kazdy osobno z testem (zasada 2).

A) POMIAR (bez zmiany ekonomii): w TownFolk.Live.cs DsSurplus otoczyc wywolanie BuyGoods (linia 1276) ramka `var gf = GoodsLedger.Begin(GoodsLedger.FFolkVill, mk); try { g2 = BuyGoods(...); } finally { GoodsLedger.End(gf); }`, tak jak w Live.cs:1210.
- Sol i narzedzia trafia na rynek miasta targowego w linii "KS: rynki".
- Zobacza je NKS-5 i 5u, sterowanie (R3.cs:214) i odbior KS-H (KsTakeNote kod 4).
- Sam krok A podniosby jednak w Planie produkcje soli do sztucznego ujscia, dlatego potrzebny jest krok B.

B) PRZYCZYNA (realizm, jedna zasada na jedno zjawisko): przy wsi na koszyku ZC4 z Ks Village Paid First (ten sam warunek co leakCut w Live.cs:1196, czyli zc4 && StartCalibration.SwPaidFirst) krok 5 NIE kupuje kategorii koszyka ZC4 przez BuyGoods. Sol, narzedzia, len, garnki i piwo kupuje juz drabina 1 i 2 ZC4: z limitem potrzeby, z cena ZPass2Price i progiem polki ZPass2Floor.
- Reszta budzetu kroku 5 idzie dalej istniejaca droga do DsWorks (roboty gminy: place ludu miasta targowego plus material do kasy miasta, obieg zamkniety) albo zostaje w kiesie wsi. Pomaga to tez NDS-3a (kiesy wsi).
- Technicznie: przekazac do DsSurplus flage leakCut (albo policzyc ja w srodku) i przy niej pominac blok `if (rest >= 1) { ... BuyGoods ... }` (Live.cs ok. 1271-1278).
- Klucz MCM, np. `KsSurplusSkipsBasket` (domyslnie TAK, w Settings.cs plus tools/gen_mcm.py), opis po angielsku: "with the start calibration, villages spend their surplus on comforts and communal works, not on more salt and tools than their basket needs".
- Jesli Jeff chce sol jako zapas wsi (realne solenie miesa i ryb na zime), to zgodnie z zasadami nie jako znikanie towaru. Zrobic to jako zakup do magazynu wsi do N dni potrzeby q[ZSalt], z ramka FFolkVill(mk), zjadany pozniej przez G3 (OwnFirst).

Oczekiwany efekt po B:
- polki soli > 5 szt.;
- odbior 7 dob zbliza sie do popytu zaplaconego (Westeros 352 -> ok. 800);
- kx schodzi z 2.5;
- w drodze maleje;
- indeks schodzi z sufitu;
- sterowanie przestaje ciac sol;
- produkcja 1630/d wobec popytu okolo 1650/d, czyli SaltRateRaised x3.5 bez zmian.

## files
Kod (odczyt przez git show efbbf0dc:<sciezka>):
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\TownFolk.Live.cs: linie 1196-1197 (leakCut), 1210 (wzor ramki), 1237 (wywolanie DsSurplus w finally), 1249-1285 (DsSurplus, BuyGoods bez ramki 1275-1276), 1407 i 1432 (BuyGoods bez limitu potrzeby i ceny)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\TownFolk.Luxury.cs: 393-400 (VillWorkOnly, VillWorkCat), 455 (BuyLadder ma ramke)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\TownFolk.Ks.cs: 207, 298, 443 (ZC4 z ramkami), 382-387 (drabina 1: sol)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\ArmouryBehavior.cs: 1894 (zewnetrzna ramka FFolkVill bez osady)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\GoodsLedger.cs: 568-600 (SettleMk, KsTake), 651 (Book -> rynek 2), 1276 (_ksMk)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\StartCalibration.R3.cs: 198-222 (ControlDaily czyta tylko Westeros i Essos)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\StartCalibration.Deliver.cs: 250-257 (KsBasket zawiera sol i narzedzia)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\MarketCarts.cs: 600-618 (Absorb, uwaga ZC1)
- C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\KsFoodCarts.cs: 1-40 i 514 (kx, sol jako towar podstawowy)

Logi:
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_16-26-10.log
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_15-37-31.log
- C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury\Logs\2026-10-17_16-26-10\ks-miasta-towary.csv
- C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\fin80-nks.txt

## confidence
Pewnosc:
- WYSOKA, ze anomalia S/C i 160% "bez rynku" to krok 5 DsSurplus poza ramka. Suma rynek + bez rynku zgadza sie z linia "Towary" co do sztuki (2271), "bez rynku" to tylko sol i narzedzia (dokladnie VillWorkCat), a to jedyne BuyGoods bez ramki z osada.
- SREDNIO-WYSOKA, ze ten sam zakup zdziera polki do 5 szt., wypiera koszyki ZC4 i trzyma cene przy suficie. Swiadcza o tym CSV (polka <= 5 w 87%/70% miast, kx 2.5, odbior 352 wobec popytu zaplaconego 820), ale trzeba to potwierdzic testem.
- Liczby sufitu NKS-7b (28-34%) roznia sie od CSV (indeks >= 9.5 w 2-13% wierszy co 7 dob). Sprawdzajacy liczy prawdopodobnie inna miare albo okno, wiec tej liczby nie uzywam jako dowodu.

Co to udowodni:
1. Po samym kroku A test 120 dob: "bez rynku" soli i narzedzi okolo 0, zuzycie rynku Westeros+Essos wzrasta o okolo 970/d, NKS-5 dla soli okolo 1.0-1.1, NKS-5u bez soli. Nic w ekonomii sie nie zmienia.
2. Po kroku B:
- linia "Towary salt": zuzycie "wsie" spada z okolo 1500-2200 do okolo 600-800/d, posiadacz "miasta" rosnie z 1537 do kilku tys.;
- CSV ks-miasta-towary: polka soli > 5 w wiekszosci miast, kx mediana < 2, odbior 7 dob okolo popytu zaplaconego;
- NKS-7b dla soli < 20%;
- NKS-27 bez ciecia soli;
- DS "krok 5 wydany: towar" spada o okolo 7-10 tys. zl/d na rzecz robot gminy albo kies wsi (NDS-3a nie gorsze).

## proof



# TEMAT

## cause
CENY (NKS-7) - trzy przyczyny naraz, zadna nie jest bledem wzoru ZC4. Ceny sa w wiekszosci PRAWDZIWE. Kolejnosc wedlug wagi:
(1) ROZDZIAL MIEDZY MIASTA (najwieksza czesc). Wzor ceny gry to funkcja DNI ZAPASU na polce miasta: indeks = (popyt/(0.1*podaz+0.04*wartosc polki+2))^0.6, czyli ok. (7/dni polki)^0.6. Nadwyzka (NKS-5 1.09-1.27) laduje w kilku miastach. Mediana miast ma 1-2 dni zapasu. Linia cen to mediana miast, wiec pokazuje te puste. Rozdzial dusi KS-H: wspolny budzet miasta celu (KsFoodCore.cs:70 `budget[i] = CofferShare*(Gold-Res)`, CofferShare 0.15, :20) jest wydawany towar po towarze w stalej kolejnosci k (petla :84, `avail = budget - spent[t]` :148). Zboze i ryby ida pierwsze i zjadaja kase. Ser, maslo, winogrona, oliwki i sol dostaja "kasa celu za mala": NKS-22 37.8% zamowionych sztuk, w dobie 109158 11737 z 28319.
(2) STRUKTURALNA LUKA ZAPASU (glownie nabial). Cena startu wymaga zapasu ShelfDays(p)=7*p^(-1/0.6) dni popytu: dla ceny 0.70 to 12.7 dnia, dla 1.0 to 7 dni. Rampa nie napelnila polek, w D (109117) bylo w Westeros: ser 651/4339 (15%), maslo 323/1176, winogrona 4030/10383, oliwki 2506/6179, sol 607/5861. F2 nie podnosi podazy, gdy polka ma mniej niz polowe celu (3090 pozycji bez ustawienia). Regula NKS-5 (podaz/zuzycie = 1) trzyma zapas w miejscu, czyli zamraza cene z D, a ta jest blisko cen sprzed KS: ser W 2.11, winogrona 3.32, pomarancze 7.49 w dobie 109081. Ceny schodza tylko tyle, ile wynosi realna nadwyzka (NKS-7c ujemne nachylenia).
(3) ARTEFAKT P7 (mniejsza czesc, 0.3-0.5 indeksu). Srednia cen jest zerowana w D (Live.cs:583 `RulePxReset()` przy StartShelves), a nie w D+14, jak chce plan P7. Probka co 7 dob i waga 1/4 (Rule.cs:62-95) daja ok. 28 dob pamieci, wiec okno D+22..D+49 niesie ceny z rampy.
WZOR ZC4 / CZLON POPYTU: dla sera i masla niewinny. Dni wynikajace z indeksu dziela sie przez dni polki do popytu zaplaconego w stosunku 0.95-1.27. Dla Westeros miesa (0.58), winogron (0.59) i oliwek (0.61) wzor widzi ok. 1.7x wiecej popytu niz zaplacony (albo opoznienie wygladzonej podazy). To podbija cene o ok. 1.35x. Tylko przy miesie jest to glowna przyczyna (linia 1.54, bez opoznienia). Tego nie dowiodlem.
WELNA/LEN (NKS-5, NKS-5e): to NIE KsRecipes. Plan nie liczy glownych odbiorcow plotna i filcu. Popyt Planu: lnu (plotno ludu + o("linen"))*1.25, welny (sukno + o("felt"))*1.45 (Core.cs:685-686). o() = Army+Cast+Court, a Ws (warsztaty) tylko dla iron/hardwood/silver (Core.cs:648). Army z ksiegi to tylko "zywnosc partii" (GoodsLedger.cs:1271 `v[KsArmy] = Group(a.Sink, GFood)`). "odziez wojska (150)" (GArmyCloth) nie wchodzi do zadnej kolumny KS (KsShop :1273 jej nie zawiera). W oknie na swiecie: plotno zuzycie 238/d, z tego odziez wojska 102, warsztaty zbrojne 74 (poza Planem), mieszczanie 45. Filc 184/d, z tego odziez wojska 130. Welna dodatkowo linie warsztatow (wool_weavery) 83/d. Plan sieje len 67/d na swiecie (W 30, E 37 = produkcja na targ 30/37), a rzemioslo miasta (148) zjada 236 lnu/d. Zapasy W len -51%/d, welna -23%/d.
NKS-26: linia liczy STAN podlogi, nie podloge dana. R3.cs:537 `if (fl[0] == 1) { on++; kcalFloor += max(0, need - (own+home+bk+own)) }` to licznik stanu z FloorDaily. Stan nie schodzi, bo zejscie wymaga 14 dob cov>=0.95 i bp>=0.80 (R3.cs:529), a wsie kupuja 79% chcianych sztuk. "kcal podlogi 5.68%" to niepokryta potrzeba (brak), a nie kcal dane. Podloga dana = _granFloorUp, w linii produkcji "podloga (wzor dzis) w 0" we WSZYSTKICH 93 dobach. _granNoFloor (Live.cs:229, ++ w :243) jest martwy: nigdzie nie jest wypisywany ani zerowany (zerowanie tylko granUp/Above/Rel w Live.cs:476-477).

## evidence
Dane miast z ks-miasta-towary.csv (fin80), kolumna indeks = chwilowy GetItemCategoryPriceIndex:
- Linia NKS-7 (28-dobowa) wobec chwilowej mediany miast w tych samych dobach 109144-109172, Westeros: ser 2.26 / 1.97, maslo 2.20 / 1.76, winogrona 2.38 / 2.11, oliwki 2.30 / 1.75, mieso 1.54 / 1.63. P7 dodaje 0.3-0.55 (bez miesa).
- Rozdzial, Westeros winogrona 109158: zapas razem 5.1 dnia popytu zaplaconego (indeks z sumy 1.22). Mediana miasta 2.0 dnia (indeks 2.25). 49% miast ma ponizej 2 dni, 5 miast trzyma 82% zapasu. Oliwki W 109172: razem 8.9 dnia (indeks 0.87), mediana 1.5 dnia (1.24), 51% miast ponizej 2 dni, 39% ponad 10 dni, 5 miast trzyma 76%. Sol W: 83% miast ponizej 2 dni, 5 miast trzyma 83-85%. Ryby (k=1) sa rozlozone dobrze (12-31% miast ponizej 2 dni). Kasa miast nie odroznia pustych od pelnych: mediana kasa/zapas_kupcow 1.12-1.49 w obu grupach, wiec winny jest podzial budzetu, nie bieda miast. NKS-13b: wsie W "bez towaru" 18.6%.
- Luka zapasu: ser W razem 3.0 -> 5.8 dnia w oknie (indeks z sumy 1.65 -> 1.12) przy celu 12.7 dnia. Maslo W 2.4 -> 4.4 dnia przy celu 7.
- Czlon popytu, mediana (7*ix^-1.667)/(polka/popyt zaplacony) w oknie: ser W 0.95, maslo 1.27, zboze 1.47, mieso W 0.58, winogrona W 0.59, oliwki W 0.61, ryby 0.80.
- Pomarancze, daty, welna i len nie maja kolumn w CSV. Pomarancze W: 7.49 przed KS, 5.41 w D, w oknie 4.23 -> 2.88 (nachylenie -1.36%/d), zapas w D 202/305. Daty W: 5.34 w D. Welna i len to prawdziwy niedobor (podaz 0.21-0.66).
- Welna/len, fin78 (a6c56999, PRZED KsRecipes) wobec fin80, D+22..D+49: len W 0.20 / 0.21, E 0.33 / 0.33; welna W 0.58 / 0.66, E 0.72 / 0.69. KsRecipes nie jest przyczyna, troche nawet pomoglo. Ksiega na swiecie: produkcja lnu przed KS 513 W + 426 E /d, przy KS 30 + 37 /d. Plan ks-plan.csv 109179: len na targ W 30, E 50 szt./d (jednostka szt. = kg/370).
- NKS-26: 93 z 93 linii produkcji "podloga (wzor dzis) w 0", "podloga zdjeta (KS-R3) w 15-16". Linia stanu "wsi z podloga 561 z 569, kcal podlogi 5.68%" to stan i brak (zgadza sie z NKS-8c: bez pokrycia W 13.5%, E 5.4%). Klucza KsGranaryNoFloor nie ma w logu.

## fix
1) ROZDZIAL KS-H, glowny ruch (KsFoodCore.cs:70/84/148). Zamiast jednej kasy wydawanej w kolejnosci k: podzielic budzet miasta celu na towary wedlug udzialu wartosci popytu zaplaconego (budget[t] * Dp_k*cena_k / suma), z przeniesieniem niewydanej reszty na drugi przebieg. Albo przechodzic towary od najkrotszego pokrycia (H/(T*d) rosnaco), a nie po indeksie k. Zamknieta ekonomia bez zmian: kasa miasta placi, zrodlo dostaje, furman bierze fracht. Nowy klucz MCM, np. "Ks Carts Budget By Need" (dopisac w Settings.cs i uruchomic tools/gen_mcm.py). Dowod: liczniki _dPoor na towar w linii KS-H, NKS-22 "kasa celu za mala" ponizej 10%, mediana dni polki winogron, oliwek i soli W powyzej 3, NKS-13b "bez towaru" ponizej 5%.
2) LUKA ZAPASU. Bez sztucznej podazy (zasada Jeffa). Na teraz plan P4: dodatnia marza zapasu Planu per rynek x towar (Plan.cs ShelfMargin 1.03 -> marza z luki: cel ShelfDays(p)*popyt minus zapas, rozlozona np. na 28 dob, wygasa przy zapasie celu). Realne rece produkuja nadwyzke, miasta za nia placa. Dla sera W luka ok. 3700 szt., czyli ok. +130/d (+36% zuzycia). Jesli Jeff tego nie chce, zmienic cel NKS-7 na pasmo wynikajace z faktycznych dni zapasu, a nie z ceny cienia.
3) P7, pomiar (bez wplywu na gre). Live.cs:583: RulePxReset w D+14 (koniec popytu rampy), a nie w D. Albo NKS-7 w sprawdz_logi.py:35116-35145 liczony z chwilowej mediany (nowa linia INFO z _pxNow albo z ks-miasta-towary.csv).
4) WELNA/LEN (Core.cs:648 i :685-686, GoodsLedger.cs:1271-1273). Nowa kolumna ksiegi KS, np. KsCloth = Group(a.Sink, GArmyCloth), wpieta w KsItem (Plan.cs:111-112) i w OneToOne dla linen/felt/leather. Dla linen/felt/leather dodac do pcs takze Group(GArms) (warsztaty zbrojne; dzis Ws tylko dla iron/hardwood/silver). Do D[GWool] dodac bezposrednie GShopIn welny (wool_weavery). Uwaga na podwojne liczenie: GCraft (rzemioslo miasta 148, przerob lnu na plotno) NIE dodawac do lnu, bo jest juz w plotnie *1.25. Tak samo sprawdzic skory (leather 0.89): ten sam wzorzec, odziez wojska poza Planem.
5) NKS-26, linia i sprawdzacz. W FloorDaily (R3.cs:543) wypisywac: stan klucza Ks Granary No Floor; "podloga dana (wzor gry)" = granUp; "bez podlogi (brak z zapasu spichlerza)" = _granNoFloor, zerowany co dobe razem z innymi w Live.cs:476-477. Przy wlaczonym kluczu "kcal podlogi" nazwac "kcal braku (z zapasu spichlerza)". W sprawdz_logi.py:34499-34511 przy kluczu TAK bramka = podloga dana 0 we wszystkich dobach okna, a brak kcal jako INFO powiazane z NKS-8c i NKS-15. Udzial wsi w stanie nie jest wtedy bramka. Wtedy fin80 NKS-26 = TAK (0 z 93 dob).

## files
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury\Logs\2026-10-17_16-26-10\ks-miasta-towary.csv; ...\Logs\2026-10-17_16-26-10\ks-plan.csv; ...\Logs\2026-10-17_16-26-10\kasy-miast.csv; D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_16-26-10.log; D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_15-37-31.log; efbbf0dc:Armoury/src/KsFoodCore.cs (:20, :70, :84, :148); efbbf0dc:Armoury/src/StartCalibration.Core.cs (:648, :685-686); efbbf0dc:Armoury/src/GoodsLedger.cs (:1271-1273); efbbf0dc:Armoury/src/StartCalibration.Plan.cs (:111-112); efbbf0dc:Armoury/src/StartCalibration.Live.cs (:229, :239-245, :476-477, :583, :601); efbbf0dc:Armoury/src/StartCalibration.R3.cs (:529, :537, :543); efbbf0dc:Armoury/src/StartCalibration.Rule.cs (:62-95, :314); efbbf0dc:Armoury/src/StartCalibration.Ramp.cs (StartShelves, ShelfDays); efbbf0dc:tools/sprawdz_logi.py (:34499-34511, :35116-35145); skrypt pomocniczy C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\rynki.py

## confidence
Wysoka: ze rozdzial i luka zapasu to glowne przyczyny (liczby z CSV miast); ze P7 to tylko 0.3-0.5 indeksu; ze welna/len to nie KsRecipes (fin78 te same stosunki); ze Plan pomija odziez wojska i warsztaty zbrojne (kod GoodsLedger.cs:1271-1273, Core.cs:648/685-686 plus ksiega); ze NKS-26 liczy stan, a nie podloge dana (kod i 93 x "podloga (wzor dzis) w 0").
Srednia: ze stala kolejnosc towarow w KS-H jest powodem odciecia sera, winogron, oliwek i soli. Za tym przemawia wzorzec: sol ostatnia i najgorsza, ryby wczesnie i dobrze. Brakuje licznika _dPoor na towar.
Niska do sredniej: ze czlon popytu wzoru podbija cene miesa, winogron i oliwek W ok. 1.35x. To tylko wniosek posredni, nie mam ItemData.Demand.

## proof
a) Do ks-miasta-towary.csv dopisac ItemData.Demand, Supply i InStoreValue kategorii. Stosunek Demand/(popyt_zaplacony*wartosc) dla miesa W okolo 1.7 potwierdzi czlon popytu, okolo 1.0 go wykluczy.
b) Linia KS-H z _dPoor na towar: oczekuje, ze zboze i ryby maja ok. 0, a ser, maslo, winogrona, oliwki i sol niosa wiekszosc z 37.8%. Potem bieg z budzetem wedlug potrzeby: mediana dni polki tych towarow powinna dojsc do sumy (np. winogrona W ok. 5 dni), a chwilowa mediana indeksu spasc do ok. 1.2-1.3.
c) P7: chwilowa mediana z CSV (ser W 1.97, oliwki 1.75) wobec linii (2.26, 2.30). Po przeniesieniu zerowania na D+14 linia powinna zblizyc sie do chwilowej.
d) Welna/len (P8 krok 1): srednie ksiegi z 28 dob per ujscie wobec popytu Planu per towar. Oczekuje popytu lnu w Planie ok. 70/d wobec ok. 300/d potrzeby (238 plotna*1.25). Po dodaniu KsCloth i GArms NKS-5 len i welna powinny wejsc w 0.95-1.05.
e) NKS-26: przy wypisanych granUp i _granNoFloor (zerowanym co dobe) fin80 pokazalby podloge dana 0 i bez podlogi ok. 554-561 wsi na dobe.
Pliki: kod efbbf0dc: Armoury/src/KsFoodCore.cs, StartCalibration.Core.cs, GoodsLedger.cs, StartCalibration.Live.cs, StartCalibration.R3.cs, StartCalibration.Rule.cs, StartCalibration.Ramp.cs, tools/sprawdz_logi.py.


# TEMAT

## cause
Kiesy wsi nie maja juz trwalego wycieku. Spadek w NDS-3a i NDS-3c sklada sie z trzech rzeczy i zadna nie jest dziura w zamknietej ekonomii.

(1) Jednorazowy uskok poziomu, a nie dryf. Koszyk ZC4 wsi ruszyl w dobach 109123-133 (zakupy wsi 0 -> ~90 tys./d; rampa KS skonczona 109132). Ten koszyk nalozyl sie na krok 5 DS. Krok 5 wydaje codziennie 1/15 zapasu ponad skrzynie: DsVillage.cs:519 `budget = ramp * over / sd`, DsSurplusSpendDays=15, skrzynia 21 dob (Settings.cs:2733-2734; w Armoury.json Jeffa brak kluczy Ds, czyli dzialaja wartosci domyslne). Krok 5 reaguje na nowy odplyw tylko przez spadek zapasu, ze stala czasowa 15 dob. Dlatego suma kies spadla o ~0.9 mln w dobach 109126-109151 i potem stanela. Okno 60 dob w NDS-3a zaczyna sie w dobie 109135, czyli w srodku tego uskoku. Po uskoku suma jest plaska albo rosnie: ostatnie 30 dob +0.11%/d, ostatnie 20 dob +0.21%/d. W fin78 te same okna daja -0.16%/d, wiec efbbf0dc (drabina 2 tylko ponad skrzynie) zadzialal.

(2) Mediana spada, bo tak zaprojektowano krok 5. Kiesa kazdej wsi dazy do skrzynia_i + 15 x nadwyzka_i. Dla przecietnej wsi, po koszyku, czynszu i daninach przy wozie, nadwyzka jest mala, wiec kiesa dazy do jej skrzyni. Liczby: skrzynie razem 1.74 mln, srednio 3044 zl na wies. Mediana kies 4408 -> 3466 i dalej opada w strone skrzyni. Bramka NDS-3c (>= 0.9 startu) porownuje do startu ustawionego starymi normami sprzed DS, a nie do skrzyni z nowego projektu.

(3) Prawdziwy problem lezy poza wsiami: kiesy ludu miast (klasa W) sa akumulatorem i karmia sie nadwyzka wsi. Krok 5 nie znajduje towaru, wiec 75% jego budzetu idzie na roboty gminy: DsWorks w TownFolk.Live.cs:1295-1314. Linia 1309 robi `Credit(mf, wage, KBuildLabour)`, czyli 3/4 trafia do kies ludu miasta targowego. Linia 1310 robi `mk.Town.ChangeGold(mat); mf.Sale[MixBuild] += mat`: 1/4 idzie do kasy miasta za "material", ale zaden towar nie schodzi z polki. Ten fikcyjny "material" lamie zasade "nic z niczego". Lud W ma juz wszystkie dobra kupowane z nadwyzki zablokowane: 60% oferty P3 niekupione, 199 z 240 par miasto-kategoria ponad cena graniczna. Ma tez zablokowana droge powrotu pieniadza do wsi przez jedzenie: StartCalibration.Zc4.cs:775-783 `Zc4OfferFactor = 1 - lambda`, przy lambda 1.00 grupa "lepszy stol" ma 0 zl oferty, a w bazie 17 tys./d. Dlatego lud tylko zbiera i nie oddaje.

Odplyw do miast jest zrownowazony tylko dlatego, ze kasy miast placa wozom wsi. Petla wyglada tak: kasy miast -> utarg wozow -> kiesa wsi -> roboty gminy -> kiesa W (tam pieniadz staje).

## evidence
RACHUNEK KIES WSI, fin80, srednio z ostatnich 60 dob, zl/d. Zrodla: linie 'Przeplywy osad (kiesy wsi)', '(przyczyny)', 'Daniny wsi (DS)' i 'Koszyk stanow (ZC4)'.

WPLYWY, razem +214.3 tys.:
- czesc wsi z utargu wozow (brutto): +144.6
- markietani (czesc wsi): +32.8
- sakiewki zwolnionych (166): +19.5
- przelewy gry (towar kupiony we wsiach, lordowie): +13.2
- fracht KS-H: +4.2

ODPLYWY, razem -223.8 tys.:
- koszyk ZC4, drabina 1 i 2: -86.3 (drabina 1 ok. 72: W 37 + E 35; drabina 2 ok. 14)
- krok 5, czesc P5: -86.7, z tego towar tylko 13.3, a roboty gminy 73.4 (place ludu miast 55.1 + "material" do kas 18.4)
- krok 5, rynek BK: -10.2 (to jest caly odczyt NKS-29; NDS-1c: stary pulap 0, wszystko z nowego budzetu DS, polowa wedlug DsSurplusBkShare 0.5)
- przy wozie -26.6: korona 20.1 + dziesiecina na septy 6.5 (jalmuzna zostaje we wsi)
- czynsz: -10.6
- E3-W1: -0.3

Wynik: -9.5 tys./d. Zmierzone: -9.0 tys./d (reszta [R] okolo 0).

Pierwsze 30 dob, przed koszykiem: krok 5 ok. 150 tys./d, kiesy rosly +4.9 tys./d.

Szereg sum kies (doba: tys. zl): 109121: 3778, 109131: 3513, 109141: 3162, 109151: 2953, 109166: 2838, 109181: 2876, 109195: 2896.

Ponad skrzynie: 2.28 mln (109115) -> 1.35 mln (109175) -> 1.41 mln (109195).

Mediana kies: 4365 -> 3681 (109166) -> 3466. Wsi ponizej 1000 zl: 35 -> 85.

Wedlug ks-miasta.csv (109123 -> 109193) traca bogate rynki ponad skrzynia: reach -165 tys., vale -130, ghiscari -129, stormlands -125. Zyskuja biedne: aserai +73, sturgia +45, dragonstone +34.

AKUMULATOR - kiesy ludu miast ('Kiesa ludu'):
- razem 12.08 -> 27.19 mln w 120 dobach; W 10.03 -> 23.59 mln
- zapas dni W 55 -> 180; lokata 0 -> 19.6 mln
- ostatnie 60 dob: +204 tys./d (wplywy 959 tys., wydatki 755 tys.)
- baza 16.10 (bez DS i KS): 12.08 -> 12.85 mln, czyli plasko
- fin78 tak samo jak fin80 (26.95 mln)

Ze wsi do miast w calym biegu (suma linii DS):
- place robot gminy 7.57 mln
- "material" bez towaru 2.52 mln
- septy: place 0.60 + material 0.20 mln
- towar z kroku 5 tylko 1.71 mln

Ponad polowa przyrostu kies ludu (15.1 mln) to place robot gminy ze wsi.

Inne zrodla akumulatora:
- robocizna z kas miast 280 tys./d
- pola mieszczan KS 75 tys./d
- doplata do 35%: 88 tys./d (baza 64)
- 5% zaworu: 61 tys./d (baza 15)

Ujscie, ktore oslablo: danina z oszczednosci P1 86 tys./d (baza 163).

Oferta P3 dla ludu W: 243 tys./d, kupione 97.6 tys. (40%), "lepszy stol" 0 (baza 17 tys.).

Pieniadz swiata: 219.4 -> 219.2 mln, plasko. Wzrost kies ludu (+15.3 mln) i podzamczy (+1.8 mln) pokryly:
- skarbce krolestw -9.56 mln
- glowy rodow -5.69 mln
- kasy zamkow -0.94 mln
- notable -0.88 mln
- kiesy wsi -0.79 mln

Notable sa w pasmie 4500-10500 i nie sa akumulatorem.

NKS-29 (rynek BK 5.6% / 8.2%) to wprost DsSurplusBkShare 0.5 - polowa kroku 5. To niezgodnosc bramki z projektem, a nie odplyw: 10 tys./d, i za to jest towar.

## fix
Zgodnie z zasada "jedna zmiana naraz" - w tej kolejnosci:

KROK 0 (narzedzie nds/nks, bez DLL):
- NDS-3a liczyc od konca rampy KS + 2 x DsSurplusSpendDays (doba >= 109162) albo z ostatnich 30 dob. fin80 wtedy przechodzi: +0.11%/d.
- NDS-3c porownywac mediane kiesy z mediana skrzyni, a nie ze startem (start to normy sprzed DS).
- NKS-29 liczyc tylko rynek BK ponad budzet kroku 5 DS. Alternatywa: przy KS obnizyc DsSurplusBkShare do ok. 0.15.

KROK 1 (gra; glowna poprawka na odplyw wsi i karmienie akumulatora):
Zmienic DsWorks w TownFolk.Live.cs:1295-1314. Nowa regula: "roboty gminy robi gmina wlasnymi rekami".
- Pansczyzna i wspolna praca nie kosztuja monety. Kwota `wage` nie schodzi z kiesy wsi, czyli usuwamy Credit z linii 1309.
- Z kiesy schodzi tylko material, ktory naprawde jest na polce miasta targowego: drewno, wapien, glina, narzedzia, zelazo. Kupowany po cenie polki przez BuyGoods albo ZcTake, zdejmowany z rostera, zapisany w ramce GoodsLedger. To zamiast `mk.Town.ChangeGold(mat); Sale[MixBuild] += mat` bez towaru w linii 1310.
- Placa dla wynajetych rzemieslnikow miasta: najwyzej DsWorksWagePerMaterial x material kupiony, np. 0.5.
- Brak materialu: brak robot, pieniadz zostaje we wsi i jest dostepny do pozyczek dla panow (TownFolk.Loans juz pozycza tylko ponad skrzynie).
- Nowe klucze: DsWorksRealMaterial (bool, domyslnie true) i DsWorksWagePerMaterial (float 0.5). Po dodaniu uruchomic gen_mcm.
- Ta sama regula dla "materialu" septow: TownFolk.Tithe.cs:130, 178, 231.

Oczekiwany skutek: odplyw ze wsi do kies W spada z ok. 73 tys./d do kwoty materialu, ktory faktycznie istnieje. Kiesy wsi wroca do wyzszej rownowagi: skrzynia + nadwyzka x 15 / udzial wydatkow z towarem. Akumulator W traci ok. 55 tys./d doplywu.

Ryzyko: NDS-3b (gorny limit +0.2%/d) w czasie przejscia. Wies moze chomikowac, gdy nie ma towaru. Trzeba sprawdzic, czy pozyczki i rynek BK wchlaniaja nadwyzke.

KROK 2 (osobny test; akumulator W):
- W StartCalibration.Zc4.cs:775-783 Zc4OfferFactor przestaje zerowac kategorie koszyka dla oferty nadwyzki P3 (TownFolk.Luxury.cs:233 i TownFolk.Live.cs:472). Lud W z lokata ponad zapas moze kupowac "lepszy stol" (mieso, ser, maslo, wino, miod) ponad ilosci zaplacone w koszyku ZC4, najwyzej do ceny granicznej P4.
- Pieniadz wraca przez kasy miast do wozow wsi - za prawdziwy towar, z popytu i podazy, bez doplat.
- Do rozwazenia w tym samym kroku: przywrocic sile P1 (danina z oszczednosci), ktora przy DS spadla z 163 do 86 tys./d.

Ryzyko: ceny jedzenia rosna, a NKS-13 juz pokazuje, ze tylko 73.5% miast stac na jedzenie. Sprawdzic stac jedzenie R i P.

NIE robic: kolejnych ciec po stronie wsi (koszyk, dziesiecina, korona) - suma juz stoi.

## files
Kod (efbbf0dc):
- Armoury/src/TownFolk.Live.cs:1249-1286 (DsSurplus), 1295-1314 (DsWorks; 1309 place do kies ludu, 1310 material bez towaru), 472
- Armoury/src/DsVillage.cs:238-245 (ChestOf), 506-532 (SurplusDay, 519 budzet = ramp x ponad / 15)
- Armoury/src/Settings.cs:2733-2737 (DsVillageChestDays=21, DsSurplusSpendDays=15, DsSurplusBkShare=0.5, DsCommunalWorks)
- Armoury/src/TownFolk.Ks.cs:180-300 (drabiny ZC4 wsi; 281 drabina 2 ponad skrzynie)
- Armoury/src/TownFolk.Tithe.cs:193-240 (septy przy wozie)
- Armoury/src/StartCalibration.Zc4.cs:775-783 (Zc4OfferFactor)
- Armoury/src/TownFolk.Luxury.cs:233

Logi:
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_16-26-10.log
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_15-37-31.log
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-16_18-23-34.log
- C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury\Logs\2026-10-17_16-26-10\ks-miasta.csv i ks-budzety.csv

Skrypty parsujace: C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\kiesy\p.py, q.py, r.py

## confidence
Wysoka (ok. 90%) co do rachunku: zgadza sie z pomiarem do ~0.5 tys./d, reszta [R] okolo 0. Wysoka co do tego, ze suma kies jest stabilna od ok. doby 109160 i ze NDS-3a lapie jednorazowy uskok po starcie koszyka ZC4. Wysoka co do akumulatora W: +15.1 mln w fin80 i fin78 przy plaskiej bazie, a 7.57 mln z tego to place robot gminy ze wsi.

Srednia (ok. 60-65%) co do skutku kroku 1. Nie mam kiesy i skrzyni dla pojedynczej wsi w CSV (ks-budzety.csv nie ma kolumn kiesa i skrzynia). Dlatego nie wiem, ile nadwyzki po kroku 1 wchlona pozyczki i rynek BK, a ile zostanie w kiesach (ryzyko NDS-3b). Nie przeliczalem w kodzie dokladnej sciezki od Sale[MixBuild] do podstawy robocizny. Wiem tylko z komentarzy i logu, ze material robot gminy jest liczony jako sprzedaz bez zdejmowania towaru.

## proof
1. Bez DLL: puscic nds na fin80 z oknem NDS-3a >= doba 109162 albo z ostatnich 30 dob. Oczekiwane ok. +0.1%/d - przechodzi. Na fin78 te same okna daja -0.16%/d, co potwierdza, ze naprawil to efbbf0dc.

2. Dopisac do ks-budzety.csv kolumny kiesa_zl i skrzynia_zl dla kazdej wsi (sam pomiar). Jesli mediana kiesa/skrzynia stoi w okolicy 1.0-1.2 i nie maleje, mediana spada przez zbieganie do skrzyni, a nie przez wyciek.

3. Po kroku 1 w kolejnym fin:
- linia DS 'roboty gminy' bez 'place ludu miast targowych' i z materialem zgodnym z towarem zdjetym z polki (ksiega towarow),
- przyrost kies ludu W w 'Kiesa ludu' nizszy o ok. 55 tys./d (z +204 do ok. +150 tys./d),
- kiesy wsi rosna do nowej rownowagi; NDS-3c >= 0.9; obserwowac NDS-3b.

4. Po kroku 2: w linii 'Zbytki ludu (P3-P5)' grupa "lepszy stol" > 0 kupione; utarg wozow wsi rosnie; zapas dni W przestaje rosnac; stac jedzenie R i P >= 90%.


# TEMAT

## cause
Spadek od fin78 do fin80 to w wiekszosci szum. Nie jest to skutek KsRecipes ani zadnego bledu.

(1) Zwykle wplywy korony sa w obu testach takie same. Po rampie (doby DS 57+): fin78 594.3k/d, fin80 593.3k/d. Fin76 (DS F3 bez KS) mial 587.2k/d.
- Rozne sa tylko jednorazowe wplywy do skarbca. W fin78 Dorne dostalo 1 218 874 zl w dobie 109180 i jeszcze raz duzo w 109184. W fin80 takich wplywow jest malo: 4.2k/d wobec 28.9k/d w fin78 i 18.6k/d w fin76.
- CrownIncome.cs:252-254 liczy kazde dodatnie "inne" jako wplyw dnia. Renty 180 wyplacaja to tego samego dnia (Dorne: renty 1 201 594).
- StableD.cs:450 wlicza TodayRent do czesci "korona", a StableD.cs:529 usrednia ja w pierscieniu 28 dob. Jednorazowy wplyw siedzi wiec w "D stalym" przez 28 dob. Po odjeciu tych wplywow D z ostatnich 28 dob wynosi: baza 786.7k, fin78 732.1k, fin80 689.8k.
- Reszta roznicy fin78-fin80 (ok. -42k) to:
  - zwrot zoldu -21.6k: zalezy od tego, ile wojska jest w polu; to zwrot kosztow, nie dochod. Fin80 314.5k jest blisko bazy 322.9k.
  - przesuniecie w "wlasne" ok. -18k: zawor miast wlasne +24.6k, zawor zamkow wlasne +5.4k. Gotowka panow z ziemi jest WIEKSZA: 494.2k wobec 481.9k w fin78.

(2) Prawdziwa, trwala luka wobec bazy wystepuje we wszystkich biegach DS F3 (fin76, fin78, fin80). Jej zrodlem jest AKUMULATOR PIENIADZA w kiesach wyrobnikow (klasa W) w miastach.
- Po rampie F3 wylaczone sa trzy odplywy z oszczednosci ludu:
  - danina P1: Savings.cs:92 przez WaRate;
  - podatek od skarbca ludu (kiesa2): TownFolk.Hoard.cs:93-94, `if (!(dsOld > 0)) return 0`;
  - straznik obiegu: TownFolk.Guard.cs:40, `!DsTowns.FarmsLive`.
- W bazie te odplywy zabieraly ok. 170k/d (P1, dzien 109190) i 71.5k/d (kiesa2).
- Zamienniki nie rosna razem z oszczednosciami:
  - M9: g x 0.10 x nadwyzka na kwartal, w 13 ratach, tylko w wojnie (DsSeason.cs:242-284);
  - stala firma "panom z oszczednosci ludu" 22.9M/rok, ok. 63k/d, ustalona w oknie 109104-109131;
  - jej odnowa (DsTowns.cs:367-389) co 364 dni o najwyzej +-15%, wedlug doplywu kupcow (`f.FLf *= r`), a nie wedlug oszczednosci ludu.
- Skutek: kiesy ludu miast rosna +186k/d. Pieniadz, ktory w bazie szedl lud -> korona -> renty 180 -> D, zostaje u wyrobnikow. Wyrobnicy tylko zarabiaja, nie wydaja.
- Korona traci ok. -49k/d: P1 157k/d zastapily M9 54k i dodatkowe 184 +52k, do tego mniej z 1/450 (-8k).
- D traci ok. -107k/d: renty 180 -45k, zwrot -8k, podatek wsi BK -14k, cla -7k, zamki/zawor -10k.

KsRecipes nie ruszyly dochodu:
- firmy miast i zamkow sa nawet wyzsze: 158.5M/rok wobec 156.0M w fin78;
- podatek wsi BK 98.8k i renta wsi ok. 10.5k sa takie same w fin78 i fin80; podatek wsi BK byl nizszy juz w fin78 (KS r4 cz. 1-3), wiec to nie skutek KsRecipes;
- cla 12.2k wobec 12.4k;
- warsztaty dodaja do D rodow 1-3 zl/d;
- utarg wsi: NDS-14a 1.000;
- nic nie znika: kontrola platnicy - odbiorcy = 0, zamkniecie D 0.00%.

## evidence
Parsowalem kazda dobe linii "D staly (169c)", "Korona: wplywy dnia (165)", "Kiesa ludu", "Podatek od nadwyzki ludu (kiesa2)", "Danina z oszczednosci (P1)", "Firmy miast i zamkow (DS)" i "Kasy miast (169c)" w czterech logach: baza 18-23-34, fin76 14-24-13, fin78 15-37-31, fin80 16-26-10.

KORONA, doby DS 57+ (wplywy z naszych licznikow + inne + 1/450):
| bieg | razem | P1 | M9 | 184 | rdzen | inne | 1/450 | jednorazowe |
|---|---|---|---|---|---|---|---|---|
| baza | 646.5k | 163.7k | 0 | 12.3k | 353.1k | 52.4k | 65.0k | 12.3k/d |
| fin76 | 587.2k | | | | | | | 18.6k/d |
| fin78 | 594.3k | | | | | | | 28.9k/d |
| fin80 | 593.3k | 65.7k (rampa) | 32.5k | 52.8k | 333.8k | | 59.9k | 4.2k/d |

- Ostatnie 28 dob: P1 = 0 w biegach DS, a w bazie 157k.
- Jednorazowe w fin78: doba 109180 +1 107 087 (Dorne: wplyw 1 218 874, renty 1 201 594), doba 109184 +509 305.

D STALY, ostatnie 28 dob:
| bieg | D | korona | renty 180 dzis | zwrot | ziemia | zawor miast (wlasne) |
|---|---|---|---|---|---|---|
| baza | 800.9k | 580.2k | 215.3k | 322.9k | 201.1k | 324.0k (106.7k) |
| fin78 | 763.6k | 554.2k | 224.0k (w tym jednorazowe) | 336.1k | 190.3k | 304.0k (94.4k) |
| fin80 | 694.0k | 504.4k | 170.2k | 314.5k | 172.3k | 318.2k (119.0k) |

- Bez jednorazowych: baza 786.7k, fin78 732.1k, fin80 689.8k.
- Gotowka panow z ziemi (zawory miast i zamkow + renta wsi + podatek wsi BK + cla): baza 531.2k, fin78 481.9k, fin80 494.2k.

AKUMULATOR, kiesy ludu miast:
| bieg | 109076 | 109132 | 109195 | ostatnie 28 dob |
|---|---|---|---|---|
| baza | 11.99M | 13.33M | 12.85M | +11k/d |
| fin76 | 12.00M | | 27.18M | +195k/d |
| fin78 | | | 26.95M | +188k/d |
| fin80 | 11.98M | 15.93M | 27.19M | +186k/d |

- Zapas dni W: baza 46.3 -> 36.2, fin80 46.3 -> 179.6.
- Lokata W (ponad zapas dni) na dobe 109190: baza 8.1M, fin80 19.0M.
- Doba 109190 w bazie: P1 pobrano 170 199 zl, kiesa2 pobrano 71 551 (stawka straznika 3.93%/d; panom 47 942).
- Ta sama doba w fin80: P1 0, kiesa2 0, straznik obiegu "nie". Kiesa W / poziom normalny 4.89 (w bazie 1.28).
- R i P moga sobie pozwolic na pelny koszyk tylko w 84.6% / 77.4%, a W ma 180 dni zapasu.

Kod przy efbbf0dc:
- TownFolk.Hoard.cs:93-94: kiesa2 zerowana przez DsTowns.OldShare;
- TownFolk.Hoard.cs:164: czesc pana idzie do RentToday, czyli do ziemi D;
- TownFolk.Savings.cs:90-92: M9 zamiast P1;
- TownFolk.Guard.cs:40: straznik obiegu wylaczony przy firmach;
- TownFolk.Live.cs:1003-1004 i 1014: tempo nadwyzki 1/15 i kolejnosc PayAid -> PayHoard;
- DsSeason.cs:242-284 (M9) i Settings.cs:2761 (DsMovablesRate 0.10);
- DsTowns.cs:16-26 (lista wylaczonych poborow) i DsTowns.cs:367-389 (odnowa FLf wedlug doplywu kupcow; Settings DsFarmRenewDays 364, DsFarmRenewMax 0.15);
- CrownIncome.cs:250-254 (Measured = Own + Other);
- ClanIncomeBook.StableD.cs:450 i :529 (renty 180 w czesci "korona", pierscien 28 dob).

## fix
GLOWNA POPRAWKA (jedna zmiana, zgodna z zasadami Jeffa): przywrocic przy DS proporcjonalna danine pana od skarbca ludu ponad poziom normalny. Sredniowieczny odpowiednik to tallage, czyli danina od ruchomosci wedlug zamoznosci, placona w pokoju i w wojnie. To podatek, nie doplata. Pieniadz ma platnika i odbiorce.

Gdzie i jak:
- TownFolk.Hoard.cs PayHoard, linie 93-94 i 125. Nowy klucz MCM "DsHoardDue" (Settings.cs + `python tools/gen_mcm.py`), domyslnie TAK.
- Przy DsTowns.FarmsLive nie zwracac 0 i nie mnozyc przez dsOld. Brac tylko `room` (kiesa ponad FolkHoardNormalTimes = 3 x zapas dni) stawka bazowa FolkHoardTaxRate (1%/d; straznik jest wylaczony, wiec HoardRateNow daje stawke bazowa).
- Jako `pre` (tak jak dzis `aid`) wliczyc rate M9 tej klasy z tej doby i dzienna czesc firmy "panom z oszczednosci ludu" (FLf/364). Tej samej warstwy oszczednosci nie wolno brac drugi raz.
- Podzial bez zmian: pan 67% do RentToday (ziemia D), kasa osady 33%. Korona nic, bo jej wojenna czesc idzie przez M9.

Przewidywany skutek (szacunek, niesprawdzony):
- kiesa2 przy obecnym room ok. 18.5M bralaby ok. 185k/d, czyli tyle, ile wynosi przyrost kies. Akumulator powinien sie zatrzymac;
- D powinno wzrosnac o ok. +100-125k/d (z 694k do ok. 800k+, odniesienie 803k);
- NDS-7b korony zostanie ok. 0.92. Uwazam to za dopuszczalne: zwrot zoldu 100%, krolestwo-doby z Bankiem 3.2% wobec 16.1% w bazie, dlug koron w Banku maleje (-367/d). Prog NDS-7b 0.95 to prog projektu, nie potrzeba korony;
- jesli Jeff zechce, zeby kiesa W tez malala, a nie tylko stala: stawka 2%/d. Najpierw sprawdzic 1%.

ODRZUCONE:
- Szybsza odnowa FLf w DsTowns.Renew: odnowa co 364 dni o najwyzej +-15% i wedlug doplywu kupcow, za wolno na przyrost +186k/d.
- Podniesienie DsMovablesRate: M9 dziala tylko w wojnie, a przy g=1 juz zabiera ok. 40% nadwyzki rocznie.
- Zmiany w KsRecipes: nie sa przyczyna.

DRUGA POPRAWKA, pomiarowa, osobnym krokiem:
- Wplyw jednorazowy (inne krolestwa > 3 x mediana jego inne z 28 dob) odkladac w CrownIncome.Begin (linie 252-254) do zapasu skarbca. Dalej rozchodzi sie istniejaca regula 1/450, zamiast trafiac tego samego dnia do rent 180 i na 28 dob do D stalego. To ten sam wzor co poprawka 0564a5a5 ("inne" jako mediana).
- W sprawdz_logi grupa nds (NDS-7b, NDS-9) liczyc odniesienie i wynik bez jednorazowych albo z mediany. Dzis prog lapie szczescie wojny: fin78 przeszedl tylko dzieki wplywowi 1.6M do Dorne.

Kontrola zasad:
- regresja: PayHoard bez DS nie zmienia sie (warunek FarmsLive);
- kolizja: kiesa2 zamiast czesci firmy, z pre = FLf + M9, bez podwojnego poboru;
- zamknieta ekonomia: kiesa W -> pan / kasa osady, kontrola linii kiesa2 = 0.

## files
Logi:
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_16-26-10.log (fin80)
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_15-37-31.log (fin78)
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_14-24-13.log (fin76, DS F3 bez KS)
- D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-16_18-23-34.log (baza)

Kod przy efbbf0dc (repo lancuch):
- Armoury/src/TownFolk.Hoard.cs (93-94, 125, 164)
- Armoury/src/TownFolk.Savings.cs (90-92)
- Armoury/src/TownFolk.Guard.cs (40)
- Armoury/src/TownFolk.Live.cs (1003-1004, 1014)
- Armoury/src/DsSeason.cs (22-25, 242-284)
- Armoury/src/DsTowns.cs (16-26, 367-389)
- Armoury/src/CrownIncome.cs (250-254)
- Armoury/src/ClanIncomeBook.StableD.cs (450, 529)
- Armoury/src/Settings.cs (2749-2772)
- tools/sprawdz_logi.py (NDS-7b/NDS-9 ok. 34090-34127, _nds_ref 33914)

Skrypty parsera:
- C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\dk\dparse.py
- C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\dk\kparse.py
- C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\dk\fparse.py

## confidence
- Ok. 90%: spadek fin78 -> fin80 (0.948 -> 0.865 i 0.945 -> 0.914) to jednorazowe wplywy do skarbca plus zmienny zwrot zoldu, a nie KsRecipes. Zwykle wplywy korony sa takie same z dokladnoscia do 0.2%, firmy, podatek wsi BK, cla i renta wsi tez.
- Ok. 85%: trwala luka wobec bazy to akumulator w kiesach W po wylaczeniu P1, kiesa2 i straznika przy DS F3. Ten sam przyrost +186-195k/d jest w fin76 bez KS, a w bazie +11k/d.
- Ok. 60%: wielkosc efektu poprawki (stawka 1% wystarczy do zatrzymania, D ok. +100k). Zalezy od tego, jak szybko wroci tempo zarobkow W.

Czego nie sprawdzilem: CSV z folderow Logs. Wystarczyly linie logu i kod.

## proof
1. Test 120 dob bez bitwy: efbbf0dc + tylko klucz DsHoardDue=TAK (flaga KS jak fin80). Dla porownania ten sam bieg z kluczem NIE, ktory powinien powtorzyc +186k/d.
Ocena:
- (a) "Kiesa ludu": kiesy ludu miast po rampie F3 (od ok. 109160) plaskie, przyrost < +30k/d (baza +11k), zapas dni W przestaje rosnac (dzis 179.6);
- (b) "Podatek od nadwyzki ludu (kiesa2)": pobrano > 0 przy DS, rzedu 150-190k/d na poczatku, kontrola = 0;
- (c) "D staly (169c)": ziemia i "renta wsi i zawor miast bez wlasnych" rosna o ok. +100k; NDS-9 >= 0.95 wobec bazy 18-23-34;
- (d) bez nowych NIE w NDS-2 (kontrola platnicy - odbiorcy), NDS-3, NDS-7a/7c; zwrot zoldu 100%.

2. Ze starych logow: nds dla fin78 i fin80 z odniesieniem z sumy nasze+inne (bez Max(0) i bez jednorazowych). Moj przeliczony wynik:
- korona po rampie 593.3k fin80 / 594.3k fin78 / 646.5k baza;
- D bez jednorazowych 689.8k / 732.1k / 786.7k.
Fin78 i fin80 roznia sie wtedy tylko zwrotem zoldu i klasyfikacja "wlasne".

3. Dowod przeciw KsRecipes: w fin80 wobec fin78 firmy miast i zamkow 158.5M wobec 156.0M/rok; podatek wsi BK 98.8k w obu; cla 12.2k wobec 12.4k; gotowka panow z ziemi 494.2k wobec 481.9k.

Skrypty parsera (moj scratchpad, nie pliki repo): C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\7752dcdc-cf14-4782-9c49-18cb18a2204d\scratchpad\dk\dparse.py, kparse.py, k3.py, k4.py, fparse.py.
