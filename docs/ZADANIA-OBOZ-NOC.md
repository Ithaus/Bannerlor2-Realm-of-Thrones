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

## DECYZJE JEFFA PO RAPORCIE (20.10 ok. 14:20)
Doslownie: "1. pobrane 2. tak 3. woz do kupienia + kon juczny do powozenia, daje tyle ile w wozie kg mozna przewiezc, juki nie, juki to siodlo, mamy juz siodla, tylko dajmy
siodla bojowe i siodla transportowe, ale to chyba juz jest 4. uslugi, rozrywka i sklep, ale to w duzych obozach 5. jak pokonuje bande czy ogolnie armie, to oni powinni miec
tez zloto i jedzenie, bandyci chodza bez jedzenia czy zlota?"
1. Practice Drills 1.3 pobrany przez Jeffa (Downloads/PracticeDrills 1.3 14005 ...zip; licencja public domain - LICENSE "free and unencumbered software released into
   the public domain"); rozpakowany TYLKO do odczytu: scratchpad zb/practicedrills-1.3 (PracticeDrills.dll, 7 dzwiekow .wav, tarp_canvas.jpg, SubModule.xml) - wzor sceny obozu.
2. CHODZENIE PO OBOZIE NA POCZATKU kolejki (tor L, 2-3 noce): etap 1 scena z miejsca postoju, namioty ROT wedlug liczby ludzi, ogniska, wejscie/wyjscie; etap 2 ludzie i konie;
   etap 3 zycie obozu.
3. WOZ do kupienia + KON JUCZNY do ciagniecia: udzwig = ladownosc wozu (kg); bez osobnych jukow (juki = siodlo); siodla bojowe i siodla transportowe (juczne) -
   sprawdzic, co juz jest (PackMarket, uprzaz, siodla w grze/ROT), dolozyc brakujace.
4. MARKIETANI: uslugi, rozrywka i sklep - tylko w duzych obozach (armia).
5. BANDY I ARMIE: pokonani maja miec zloto i jedzenie. Dzis (kod): bandy nie jedza (gra: DoesPartyConsumeFood false dla IsBandit), zywnosc z niczego zamknieta
   (OutlawNoFreeFood) - jedza tylko w lupie; kiesa bandy w polu to drobne, bo co dobe odnosza czesc do kryjowki (HideoutDailyStash), zwyciezca bierze cala kiese
   (BeatenPurseToVictor). Do zrobienia: bandy jedza (zapas na kilka dni z rabunku/zakupu u pasera, glod -> rozpad do puli wyrzutkow), noszą zapas zywnosci i kiese
   na kilka dni zycia - po pokonaniu do zwyciezcy; sprawdzic to samo dla armii lordow (zywnosc w jukach, zloto lorda i kiesy ludzi).
6. Oboz 21-3 (noc gry 22-2) - zgodnie z raportem.

