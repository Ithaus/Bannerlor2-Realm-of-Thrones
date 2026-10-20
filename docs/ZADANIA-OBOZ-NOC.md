# ZADANIA NA NOC: OBOZ ARMII (zapisane 20.10.2026 ok. 08:50 PDT)

Jeff 20.10 (doslownie, skrot): "dodaj jeszcze obozowanie: teraz oboz zaklada chyba za pozno, powinnismy przeniesc na 22:00, tak aby oboz zaczynal sie,
jak w grze jest noc, aby nie jechac z modyfikatorem noc [...] To praca na noc, aby to w nocy przejrzec, czy mozemy to zrobic, czy sa pod to mody, a jak nie,
to zrobic swoj mod i co jest realne, a co nie. I plus Twoje dodatkowe pomysly, dopisz to do zadan i zrob rozpoznanie, co mozemy zrobic."
Potwierdzenia Jeffa z gry (20.10): start "Indebted Lord" DZIALA; premia na drodze bocznej DZIALA.

## Lista Jeffa
1. **Godzina obozu 22:00** zamiast dzisiejszej (swiat spi 0-6, NightRest.cs) - zgrac z godzina, od ktorej gra liczy noc (kara nocna predkosci/wzroku), zeby nie jechac
   z modyfikatorem nocy. Rozpoznac: od ktorej godziny gra/ROT/nasze mody licza noc (CampaignTime IsNightTime, MapWeather, kara nocy predkosci) i jak zmiana
   godziny ruszy sen, dlug snu, karczmy, marsz forsowny (p5-armia), dzien odpoczynku armii, rozwoz wozow wsi, budzety.
2. **Wejscie do obozu i chodzenie po nim**: im wieksza armia, tym wiekszy oboz - namioty, ludzie, wojsko siedzi, rozmawia, zycie obozowe. Czy sa takie mody?
   (mamy CampScene.cs - bitwa wsrod namiotow przy napadzie na oboz gracza).
3. **Trening wojska w obozie** - oprocz samoczynnego treningu (model gry) dodatkowy trening z wyboru gracza.
4. **Wyglad obozu wedlug druzyny**: sam gracz = poslanie i kon (nawet nie namiot); co X zolnierzy namiot albo poslanie, konie na postoju.
5. **Udzwig**: sam z koniem 30 - za malo; kon (z jukami) powinien dawac X.
6. **Przedmioty obozowe do kupienia**: namioty, rzeczy obozowe, wozy; czy jest model 3D wozu w grze (do obozu i na mape).
7. **Ogolnie mocno rozbudowac obozowanie armii**: im wieksza armia, tym wiekszy oboz, wrecz miasteczko obozowe z markietankami itd.

## Moje dodatkowe pomysly (do rozpoznania, Jeff decyduje po raporcie)
- Czas rozbijania i zwijania obozu wedlug wielkosci armii (dzis godzina nieladu przy zwijaniu) - duza armia rusza pozniej.
- Ogniska i jedzenie w obozie: morale z jakosci jedzenia (Sutlers/markietanki juz sa) i z ognisk zima.
- Podjazdy furazerow z obozu: jedzenie z okolicy (wsie wokol obozu traca, armia oszczedza zapasy) - zamknieta ekonomia.
- Warsztat obozowy: platnerz (R3-04c juz jest), kowal podkuwa konie, cyrulik leczy rannych szybciej w obozie.
- Wozy taborowe: wiecej udzwigu kosztem predkosci (i cel napadow band na tabor).
- Namiot wodza z herbem rodu na mapie (wiekszy przy wiekszej armii) - widac z daleka, kto obozuje.

## Rozpoznanie (w nocy, przed kodem)
- Mody: Nexus/Steam (oboz, camp life, army camp, carts/wagons, carry capacity, training) - zgodnosc z 1.4.8, BK, ROT; licencja/uzycie; co mozna wziac, co zrobic samemu.
- Gra: prefaby/sceny namiotow, poslan, wozow, koni na postoju, ognisk (SandBoxCore, Native, ROT, NavalDLC); mozliwosc wlasnej sceny obozu (misja chodzenia jak w
  wiosce/zamku - jak "Take a walk" we wsi gracza), model udzwigu (DefaultInventoryCapacityModel, juki/zwierzeta juczne), model treningu (DefaultPartyTrainingModel).
- Wynik: docs/ROZPOZNANIE-OBOZ-<data>.md - co realne (S/M/L), co nie, ryzyka (wydajnosc mapy - nic co klatke), propozycja kolejnosci; prostym jezykiem dla Jeffa.

## Kolejnosc nocy
Najpierw oboz (rozpoznanie -> 22:00 i najtansze realne czesci -> raport), optymalizacja (docs/PLAN-OPTYMALIZACJA-NOC.md) NA KONIEC - po zamrozeniu nowych funkcji.
