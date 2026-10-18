# Raport nocny 17/18.10 - dla Jeffa (prosto)

## Najwazniejsze w trzech zdaniach

1. **Gra jest gotowa do gry: WGRANIE 79 (wgrane 18.10 ok. 13:10 Twojego czasu).** W srodku sa wszystkie poprawki nocy i poranka (4 fale + poprawki z koncowego przegladu ekonomii). Test Twojej sciezki 160 dob przeszedl bez bledow i bez zgonow z glodu, test z bitwa tez.
2. **Nowa ekonomia (kalibracja startu, "KS") wlaczy sie w Twoim zapisie sama.** Przy pierwszym wczytaniu zaczyna liczyc Twoj swiat (ok. 33 dni gry), potem w 14 dni lagodnie przestawia rynek. W tescie Twojej sciezki (160 dob) wlaczyla sie sama w 33. dniu, bez glodu i bez wstrzasu. Po drodze znalazlem i zamknalem ostatnie "nic z niczego", ktore ja blokowalo: konwoje zaopatrzenia moda DTE dostawaly ok. 28 koni jucznych dziennie znikad.
3. Twoj zapis jest bezpieczny. Kopia: `D:\Backup-Bannerlord\zapisy-jeffa\IronmanjCg6MOpv28iN-2026-10-17-przed-wgraniem77.sav`, poprzednie pliki modow leza obok jako `*.bak-2026-10-17-wgranie77-przed`.

**Instrukcja z wylacznikami MCM i tym, co zobaczysz w grze:** `docs/INSTRUKCJA-NOC-2026-10-18.md`.

## Co zobaczysz w grze

**Swiat**
- **Zajazdy przy szlakach** (196 na mapie, rzadziej tam, gdzie mniej ludzi). Podjedziesz, mozesz przenocowac (placisz, rano jestes wypoczety: szybciej idziesz, lepsze morale) i posluchac plotek.
- **Plotki** z prawdziwego stanu swiata: bandy w okolicy (jak silne, ile juz rabuja, ile zlota nagrabily), kryjowki i ich skarby, wojska lordow, karawany, ceny w pobliskich miastach, wiesci z wojen. Im dalej i starsza wiesc, tym mniej dokladna. Plotki sa tez w karczmach w miastach ("Listen to the gossip").
- **Drogi ROT: +15% szybciej** dla wszystkich na lodzie, gdy ida droga (w podpowiedzi predkosci linia "Road").
- **Przysiolki na pustkowiach**: mniej wiosek stloczonych w kupie, za to male osady pasterzy, drwali, rybakow i weglarzy w pustych miejscach. Ludzi w okregu tyle samo. Twoj zapis przechodzi na nowy uklad sam.
- **Herby wiosek**: mniejsze tekstury, wiec po dlugiej jezdzie nie zamieniaja sie na herb pana.
- **Zbrojownia**: zbroja z modyfikatorem nie zamienia sie juz na gorszy egzemplarz po sortowaniu; 5 drobnych poprawek.
- **Wydarzenia ROT** (np. Purpurowe Gody): w logu widac, czy skutek sie wykonal, a nasze blokady juz ich nie gubia.

**Ludzie i wojna**
- **Uchodzcy z najazdu ida do najblizszego miasta** (dzialaja po wlaczeniu nowej ekonomii): dolaczaja do biedoty, jedza z targu, pracuja za dniowke, wracaja do wsi po 30 dniach spokoju. Miasta przy froncie puchna od biedoty.
- **Glod w miescie -> niepokoj -> bunt.** Gdy na targu brakuje jedzenia, lojalnosc i bezpieczenstwo spadaja (pozycja "Hunger"). Pan miasta doplaca kupcom za dowoz ze swojego zlota ponad rezerwe wojenna.
- **Wolni Ludzie**: glod pcha ich do najazdow na poludnie; **Nocna Straz** dostaje od lordow Polnocy danine w zbozu.
- **Awans spoleczny, wykup z poddanstwa, blizny wojny** - wlaczone (Twoj "punkt 14").

**Ekonomia (to, o co prosiles: "zeby nikt nie zarabial, nie mogac wydac, i nie wchlonal calej kasy")**
- Znalazlem taki akumulator **w Twojej obecnej grze**: robotnicy w miastach zbierali pieniadze o 186 tys. dziennie (180 dni zapasu), bo przy nowych daninach zniknely podatki od oszczednosci, a "roboty gminy" wsi placily im za material, ktorego nikt nie zdejmowal z polki.
- Naprawione trzema regulami: **danina od majatku** (kto ma wiecej niz 3 x swoj zapas, placi panu - jak sredniowieczny tallage; w miastach i we wsiach, ta sama zasada, z lagodnym startem); **roboty gminy tylko z prawdziwego materialu**; **mieszczanie z oszczednosciami kupuja lepszy stol** (mieso, ser, wino).
- Wynik w tescie: kiesy ludu miast rosna juz tylko ok. 19-35 tys. dziennie zamiast 186 tys.
- **Kupcy zbozowi na kredyt**: wioza teraz, a miasto placi przy dostawie, ile ma; reszta jest dlugiem. Kto nie splaca 30 dni, nie dostaje nowych dostaw. Blokada z braku pieniedzy spadla z 38% do 0,4% zamowien. Statek, gdy szybszy; glodne miasta i Polnoc najpierw.
- **Biedni kupuja tansze jedzenie**, gdy brakuje im pieniedzy (bez zadnego prawa dla jednej krainy), **place rosna tam, gdzie brakuje rak**.
- **Rynek wsi wedlug potrzeb** (sol, zelazo, sukno, garnki; zbytki tylko bogate wsie). Naprawiona **sol** (wsie kupowaly cala polke bez potrzeby, stad cena na suficie).
- Receptury warsztatow wedlug wagi (realne uzyski).

## Twoje decyzje z tej nocy (wpisane)
- Kuznia jak u kowali AI - **NIE** (zostaje szybka).
- Uchodzcy do miast - **TAK** (zrobione).
- Place za brak rak - **TAK** (zrobione).
- Essos bez sztucznych praw - **zrobione** (tanszy koszyk jest zwyklym zachowaniem kazdego biedaka na swiecie).
- Mod CK2/CK3 na UI - skreslony.

## Co zdecydowalem sam (mozesz zmienic w MCM)
- **Danina od majatku** (Ds Hoard Due, Ds Hoard Due Villages): 3% dziennie od tego, co ponad 3 x zapas. To powrot "podatku od skarbca", ktory wczesniej zdjelismy przy daninach sredniowiecznych - ale bez niego pieniadze zbieraly sie u ludzi, ktorzy nie mieli ich na co wydac. Jesli wolisz inaczej, napisz.
- **"Najpierw wyzywic siebie"** (okreg najpierw stawia ludzi przy jedzeniu na swojej ziemi): w WGRANIU 77 dziala tylko w zimnych krainach (Polnoc, za Murem, Straz, Skagos, Zelazne Wyspy, Ib). To regula dla jednej czesci swiata, czego nie chciales - dla calego swiata rozchwiewala Essos. Robie poprawke, zeby dzialala wszedzie (ponizej, czy zdazylem).

## Co jeszcze nie jest idealne (uczciwie)
- Ceny jedzenia w nowej ekonomii sa jeszcze 1,4-2 razy wyzsze od startowych w czesci miast (byly 2-3 razy). Kupcy rozwoza coraz lepiej, ale nie wszedzie.
- Doba nowej ekonomii liczy sie za dlugo (5,5 ms, cel 3 ms) - poprawka w toku.
- Korona ma ok. 86% dawnych wplywow, panowie ok. 90%, ale zold wraca w 100% kazdego dnia, a Bank pozycza malo.
- Bandy Wolnych Ludzi nie dochodza do celow na poludniu (moze brak przejscia przez Mur na mapie) - poprawka w toku.
- W tescie bitwy "mocna" druzyna dalej przegrywa z bandytami (panika) - stara sprawa, do zbadania.


## Koncowy przeglad ekonomii (o co prosiles) - wynik prosto
Pelny opis: `docs/PRZEGLAD-EKONOMII-2026-10-18.md`.
- **Ekonomia jest szczelna**: w 160 dniach ubylo 0,23% zlota swiata, ksiega towarow zgadza sie co do sztuki. Nic nie powstaje z niczego i nie znika.
- **Problem to kierunek, w ktorym plyna pieniadze:**
  1. **Robotnicy w miastach** po wlaczeniu nowej ekonomii maja tanszy koszyk przy tych samych zarobkach, wiec odkladaja (ok. 100 dni zapasu zamiast 10). Dzis trzyma ich w miejscu danina od majatku. Robie poprawke u zrodla: zarobek dopasowany do koszyka i place, ktore spadaja tam, gdzie jest duzo wolnych rak (druga strona Twojego "place rosna przy braku rak"). Potem danina od majatku moze byc rzadka i mala, jak w sredniowieczu.
  2. **Korona zwraca 70% zoldu takze najbogatszym rodom** (ok. 65 rodow siedzi na 37 mln), a skarbce krolow chudna o 65-80 tys. dziennie i krolowie zaczynaja pozyczac w Banku. **TO TWOJA DECYZJA** (15.10 powiedziales "zwrot bez zmian"). Moja rada: zwrot wedlug potrzeby - bogaty rod placi za swoja wojne sam, sredni dostaje czesc, biedny calosc. Napisz "zwrot wedlug potrzeby: tak/nie".
- **Pod nowa ekonomia wioski nie zmieniaja zajec** (ta regula jest wylaczona od 16.10), wiec produkcja nie reaguje na ceny. Samo wlaczenie dzis by nie pomoglo (92% wiosek chcialoby uciec do towarow z cenami na suficie) - najpierw tydzien pomiaru, potem jedna regula z Twoim hamulcem zmian.
- **Opal i ruda**: pod nowa ekonomia mieszczanie pala duzo wiecej drewna (zgubione letnie zmniejszenie), a ruda znika z polowy miast - to odczujesz w kuzni. Poprawka w toku.
- Do tego w toku: limit uchodzcow, ktory naprawde chroni male miasta; przezbrajanie warsztatow liczone uczciwie; kasy miast i kupcy zywnosci; skory, sol i drewno.

## Co czeka na Twoje slowo (nic z tego nie robilem)
- Kasy miast z dlugami i podatkiem nadzwyczajnym (F1), pozyczka miejska dla pana i Ciebie (F2), karta miejska (F3), sakiewka turniejowa od mieszczan (F4).
- Duchowni jako osobny stan, pory roku w koszykach, kolor mapy wedlug zamoznosci, niewola za dlugi (ZC8).
- Limit band ROT (zajete kryjowki x 12) - zostawic czy zmienic?

## Gdzie patrzec w logu po pierwszej sesji
`Modules\Armoury\Armoury-*.log`: linie "KS: samo uzbrojenie" (dzien, w ktorym nowa ekonomia zaczela liczyc), "Akumulator kies (ds-akumulator)", "Zajazdy", "Przysiolki", "Uchodzcy do miast (RT)", "Glod miast (KS)". Bledow ERROR ma byc 0.

## Stan nocy
- 08:05 WGRANIE 77 (wszystko z nocy), 09:17 WGRANIE 78 (+ konwoje DTE bez koni z niczego - nowa ekonomia moze sie sama wlaczyc).
- 13:10 WGRANIE 79: fale 3 i 4 - "najpierw wyzywic siebie" dla calego swiata, szybsza doba nowej ekonomii, inwestycje w okreg, poprawki uchodzcow (limit dziala), Muru, ksiegi towarow, skor, opalu (pora roku), warsztatow, firm miast, plac (spadaja tam, gdzie duzo wolnych rak), oraz pomiary na kolejna runde. Specjalizacja wiosek pobocznych jest w kodzie, ale WYLACZONA (MCM 'Side Village Crafts'), bo przy wlaczeniu jeszcze o kilka procent zmienia produkcje - wlacze, gdy start bedzie neutralny.
