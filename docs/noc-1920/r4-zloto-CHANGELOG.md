## r4-zloto (20.10) - zloto startu obciete: kazdy start nowej kampanii tym samym przelicznikiem (Armoury, PLAN 3.10)

**Problem.** PLAN 3.10: start BK "zeglarz" (Mariner) daje 10 000 zl. Jeff 20.10: "obcinamy kazde zloto w starcie". Dowod (kod, StartKit przed zmiana):
regula zlota startowego dotyczyla tylko awanturnika BK i startu bez wyboru BK; kazda inna opcja BK - "zloto bez zmian". Logi testow na WGRANIU 91:
nowa kampania `Armoury-2026-10-20_00-17-30.log` linia 530 "StartKit: start BK 'brak' - zloto bez zmian (1000 d)" i 557 "StartKit: zloto startowe 1000 -> 100 d
(start bez wyboru BK)"; zapis 160 dob `Armoury-2026-10-20_00-44-29.log` linia 127 - tylko latka, bez ciecia (wczytany zapis). Zeglarz dostalby 10 000 d, najemnik 250, banita 50.

**Przyczyna.** Wszystkie zrodla zlota gracza przy starcie nowej kampanii (dekompilacja 20.10):
- gra: `Campaign.InitializeGamePlayReferences` +1000 (GiveGoldAction z niczego) i `Campaign.OnAfterNewGameCreatedInternal` `Hero.MainHero.Gold = 1000` - gracz ma 1000;
- tworzenie postaci gry, ROT (`ROTCharacterCreationCampaignBehavior`) i NavalDLC: jedyna droga zlota to `CharacterCreationContent.ApplySkillAndAttributeEffects(goldToAdd)`
  z `NarrativeMenuOptionArgs.GoldToAdd` - prywatny setter, nikt go nie ustawia: 0 (grep "gold" w dekompilacji ROT i NavalDLC - brak);
- BK `BKCampaignStartBehavior.RunStartOption`: `ChangeHeroGold(option.Gold - gold)` = zloto gracza ROWNE Gold opcji. `DefaultStartOptions`: start_adventurer 1000,
  start_lord (Indebted Lord) 0, start_mercenary 250, start_outlaw 50, start_caravaneer 0, start_mariner 10000, start_gladiator 0. Action opcji: ludzie, konie, towar,
  statek - zlota nie. `GiveClansResources` pomija rod gracza. BKROTPatch i ROT (grep MainHero: tylko kruki, Krwawe Gody, zaciag, kupcy, pojedynki - zadne przy starcie)
  zlota startu nie ruszaja. StoryMode: tworzenie postaci bez zlota. Nasza wioska poboczna (SideVillageFief, "Indebted Lord") - zlota nie daje (dochod z licznika pana).
- PRZELICZNIK: ceny historyczne Armoury NIE maja jednego mnoznika - kazda sztuka ma wlasny (log 00-17-30 linia 324: uzbrojenie suma 34 337 341 -> 1 040 701 = x0.03;
  jedzenie ok. x0.3 (zboze 10->3, mieso 30->9); zwykly kon x0.4; zold i werbunek bez zmian - "1 denar = 1 pens", SPIS-CEN-GRY: zold OK). Jedyny stosunek, ktorego
  mod uzywa dla zlota GRACZA, to start awanturnika (wpis 46, Jeff 04.10): 1000 gry -> StartGoldAdventurer 100 = x0.1 (BK Adventurer = "Vanilla start").

**Zmiana.** `Armoury/src/StartKit.cs`, nowy klucz `StartGoldCut` (MCM "Start Gold Cut", grupa "Iron Bank" obok "Start Gold Adventurer", domyslnie TAK):
- przelicznik r = StartGoldAdventurer / 1000 (u Jeffa 100 -> x0.100), najwyzej 1 - tylko w dol, nigdy zlota z niczego; reszta znika (nikt jej nie mial);
- start BK: postfiks `RunStartOption` (cel `AccessTools.DeclaredMethod`; ten sam co dotad) - zloto po BK x r: zeglarz 10 000 -> 1 000, awanturnik 1 000 -> 100 (jak dotad),
  najemnik 250 -> 25, banita 50 -> 5, Indebted Lord / kupiec / gladiator 0 -> 0;
- start bez wyboru BK (i kazdy start, ktorego nie przecial BK): pierwsza godzina - zloto gry x r (1 000 -> 100 jak dotad; cokolwiek dolozylby inny mod - tez x r);
- tylko nowa kampania: znacznik z `OnCharacterCreationIsOver` w tej sesji (zdarzenie biegnie tylko w nowej kampanii, przed oknem startu BK), czyszczony w Reset.
  Wczytany zapis - dawne zachowanie bez zmian. Klucz wylaczony albo StartGoldAdventurer < 0 - dawne zachowanie (tylko awanturnik / BK bez zmian);
- linie logu: przy starcie gry stan reguly; w nowej kampanii raz tabela wszystkich opcji BK (ile daje -> ile zostanie) i linia ciecia: zrodlo, ile dala gra/BK, ile zostaje;
- furtka autotestu: DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-start-zloto.flag` (tresc: id opcji
  BK, pusty = start_mariner) - prefiks RunStartOption ustawia te opcje, gdy autotest konczy okno BK bez wyboru. Bez pliku albo w zwyklej grze - nic.
- `StartGoldAdventurer` - opis MCM dopisany (ustala tez przelicznik reszty startow). gen_mcm: 2057 ustawien.

**Ryzyko / co sprawdzic.** Kontrola calosci:
- regresje: awanturnik i start bez wyboru BK - te same 100 d co dotad (1000 x 0.1); wczytany zapis - ta sama sciezka kodu co przed zmiana (znacznik nowej kampanii falszywy);
  latka RunStartOption ta sama metoda (DeclaredMethod zamiast Method - BKCampaignStartBehavior sam ja deklaruje), dopisany prefiks dziala tylko z plikiem w autotescie;
- kolizje: zadna inna regula nie rusza zlota gracza przy starcie (grep ChangeHeroGold / GiveGoldAction na MainHero w naszych modach - tylko przeplywy w grze);
  ksiegi zlota widza te same ChangeHeroGold co dawne ciecie awanturnika (tylko wieksza kwote przy zeglarzu: -9 000 raz na kampanie, zloto z niczego cofniete);
- skutek dla gracza: zold i werbunek sa bez zmian (juz w pensach) - start z ludzmi ma malo zoldu: najemnik 22 ludzi za 25 d (u BK 250 d tez tylko na kilka dob),
  zeglarz 8 marynarzy za 1 000 d (ok. 3 tygodnie). To jest zamiar Jeffa ("kazde zloto"), nie blad;
- rzadki przypadek: gdyby pierwsza godzina minela miedzy wyborem opcji w oknie BK a "Finish", linia godziny opisze zrodlo jako start BK bez latki, a "Finish" i tak
  przetnie zloto BK jeszcze raz (BK ustawia Gold opcji od nowa) - wynik dobry, tylko opis linii mylacy;
- watki / goraca sciezka: nic - raz na kampanie, watek gry; bez zapisu (regula dziala tylko w pierwszej sesji nowej kampanii; BK nie pokaze okna startu po wczytaniu,
  bo hasSeenInquiry = true).

**Status.** DO SPRAWDZENIA (build Armoury kod 0, 0 Error(s); bez autotestu - zakaz uruchamiania gry w tym zadaniu).

**Decyzje (Claude, Jeff: "ekonomie projektuje Claude").**
- Przelicznik = StartGoldAdventurer / 1000, nie suma cen swiata: suma cen (x0.03) dalaby startowi gry 30 d - mniej niz zatwierdzone przez Jeffa 100 - i przecielaby zold,
  ktory w tym swiecie jest juz w pensach; jeden przelicznik dla wszystkich startow trzyma ich proporcje z BK (zeglarz = 10 startow gry, najemnik 1/4, banita 1/20).
- Jedno pokretlo: suwak "Start Gold Adventurer" ustala tez przelicznik reszty (jedna zasada na jedno zjawisko); nowy klucz tylko wlacza/wylacza regule.
- Tylko zloto - towar, ludzie, statek i konie z opcji BK zostaja (zadanie: "kazde zloto").

**Test w grze.** Autotest nowej kampanii (bieg BEZ -LoadSave): w Armoury.log
`StartKit: zloto startowe (Start Gold Cut) - nowa kampania, przelicznik x0.100` (tabela: start_mariner 10000 -> 1000, start_mercenary 250 -> 25, start_outlaw 50 -> 5,
start_adventurer 1000 -> 100) i `StartKit: zloto startowe (Start Gold Cut) - zrodlo: gra (start bez wyboru BK...) | dala gra/BK 1000 d, zostaje 100 d`. Droga BK:
ten sam bieg z plikiem `armoury-start-zloto.flag` (pusty) - linia "test (autotest, plik armoury-start-zloto.flag): opcja BK 'start_mariner' (Gold 10000)" i
"zrodlo: start BK 'start_mariner' (Gold opcji BK 10000) | dala gra/BK 10000 d, zostaje 1000 d"; plik usunac po biegu. Wczytany zapis (-LoadSave): zadnej linii
"Start Gold Cut - zrodlo", zloto gracza po wczytaniu = zloto z zapisu. Reczny test Jeffa (nowa kampania, start Mariner): w sakiewce 1 000 zamiast 10 000.

**Dla Jeffa.** Kazdy start nowej gry daje teraz mniej zlota - tyle razy mniej, ile juz wczesniej ucielismy awanturnikowi (z 1000 na 100). Zeglarz zaczyna z 1 000
zamiast 10 000, najemnik z 25 zamiast 250, banita z 5 zamiast 50; awanturnik dalej 100. Ludzie, statek i towar ze startu zostaja, zabrane jest tylko zloto, ktore
bralo sie z niczego. Twoja trwajaca kampania sie nie zmienia - regula dziala tylko przy zakladaniu nowej gry.
