## p5-turniej (20.10) - R3-04a: wykup konia i zbroi pokonanego w turnieju (GrandTourney)

**Dla Jeffa (prosto).** W kazdym turnieju kto przegra walke, traci na rzecz zwyciezcy swojego konia i swoja zbroje (glowa, tulow, nogi, rece, plaszcz, kon, ladry)
- albo wykupuje je za ich wartosc w obecnym stanie: zuzyta zbroja kosztuje mniej. Gdy przegrasz Ty albo ktos z Twojego rodu, po turnieju (juz na mapie, nie w arenie)
pokaze sie okno "Ransom of Arms" z lista rzeczy i cena: "Pay N" (zaplac i zatrzymaj; jesli nie starcza zlota - zatrzymujesz najdrozsze rzeczy, na ktore Cie stac)
albo "Yield" (oddaj wszystko). Gdy Ty pokonasz lorda, on Ci placi albo jego rzeczy laduja w Twoich jukach. Lordowie AI robia to samo miedzy soba: placa z kiesy
(swojej, potem glowy rodu), ale nigdy nie ruszaja zoldu swoich ludzi na 7 dni - na co ich nie stac, to oddaja. Zwykly zolnierz w drabince walczy w cudzym sprzecie
i nic nie stawia, krewni sie nie wykupuja, a smok nie jest koniem turniejowym.

**Problem / zrodlo pomyslu.** Jeff 20.10, lista 35 pomyslow (docs/POMYSLY-35-2026-10-20.md, pkt 6, R3-04a): "28 - nie, 29 - nie. POzostale tak". Zrodlo:
docs/audyt-2026-10-09/04-wojna-armie-lupy-okupy.md rozdz. 2 pkt 8 (Lore GoT: Dunk i ser Uthor Underleaf - przegrany oddaje konia i zbroje albo je wykupuje) i N6.
Dotad turniej nie mial zadnej stawki poza nagroda i zakladem (GrandTourney/src - brak wykupu; Armoury TourneyPay/TourneyFolkPurse - tylko nagroda, zaklad, sakiewka).

**Zmiana.** Nowy plik `GrandTourney/src/TourneyRansom.cs` (+ wpiecia: 2 pola w `Settings.cs` i z gen_mcm w `McmSettings.cs`, 3 linie i `OnApplicationTick`
w `SubModuleMain.cs`, referencja `TaleWorlds.DotNet` w `GrandTourney.csproj` - Agent/Mission jej wymagaja).
- **Przegrana walka** = kazda walka, z ktorej uczestnik nie przeszedl dalej (kazdy oprocz zwyciezcy turnieju placi raz).
  - Szranki w misji (gracz walczy albo oglada; takze walki pominiete i "Leave" - gra konczy je tym samym `TournamentMatch.End`): postfiks na `TournamentMatch.End`.
    Zwyciezca nad przegranym: kto go powalil (postfiks na `TournamentFightMissionController.OnAgentRemoved`, jezdziec, gdy uderzyl kon), jesli przeszedl dalej
    i jest z innej druzyny; inaczej najlepszy wynikiem zwyciezca walki z innej druzyny; inaczej najlepszy zwyciezca walki. Rozliczenie dopiero po misji
    (`OnApplicationTick`: brak misji, stan mapy, brak rozmowy) - ekwipunek i okna nie ruszaja sie w arenie.
  - Turniej gry bez gracza (`TournamentManager.ResolveTournament` zna tylko zwyciezce): drabinka pojedynkow 16 -> 8 -> 4 -> 2 -> 1 z listy uczestnikow gry,
    zwyciezca gry wygrywa swoje pojedynki, reszte rozstrzyga wynik symulacji gry (`GetTournamentSimulationScore x (0.75 + 0.25 x los)` - wzor `SimulateTournament`).
    Rozpoznanie drogi: `Mission.Current` przy `TournamentFinished` (misja - walki juz zebrane; brak misji - drabinka).
- **Kto:** tylko bohaterowie (lordowie, towarzysze, wedrowcy, gracz). Walka z zolnierzem (w obie strony) - nic. Ten sam rod - nic. Zmarly - nic.
- **Stawka:** kon i zbroja z `Hero.BattleEquipment` (Body, Horse, Head, Leg, Gloves, Cape, HorseHarness), kazda sztuka za `EquipmentElement.ItemValue`
  (cena x mnoznik stanu: zuzycie Armoury, lup Spoils of War; ceny historyczne Armoury, gdy sa). Smok (test jak Armoury `DragonFeed.IsDragon`) i jego ladry - nie.
  Wspolny zestaw zmarlych (`Campaign.DeadBattleEquipment`) - nigdy nie ruszany.
- **Wykup** sztuka po sztuce od najdrozszej. Placi przegrany, potem glowa jego rodu. AI wydaje tylko ponad `RansomAiKeepWageDays` (7) dni zoldu partii, ktora prowadzi.
  Gracz i rod gracza: okno (bez progu - decyduje gracz; kiesa towarzysza, potem gracza). Zloto: `GiveGoldAction.ApplyBetweenCharacters(platnik, odbiorca)`,
  odbiorca = glowa rodu zwyciezcy (jak cena nagrody w grze `GivePrizeToWinner`), bez rodu - sam zwyciezca.
- **Oddane sztuki** (z tym samym stanem) schodza z ekwipunku przegranego: rod gracza - partia zwyciezcy albo juki gracza; AI - partia zwyciezcy, inaczej partia glowy
  rodu (lord sprzeda je w miescie jak lup); zwyciezca bez partii - targ miasta turnieju, a miasto placi mu wartosc z nadwyzki ponad rezerwe (`TownWageLink.Spare`).
  Sztuka bez miejsca docelowego zostaje u przegranego (nic nie znika).
- **Stan:** tylko pamiec (walki misji do rozliczenia, kolejka okien gracza, liczniki doby). Nic w zapisie - wczytanie czysci kolejke (sprzet zostaje u przegranego).
  Wyjatek - licznik potkniec, nic nie gasimy.
- **Klucze MCM (grupa "The lists themselves"):** `Tourney Ransom` (tak), `Ransom Ai Keep Wage Days` (7). Wylaczony klucz = turnieje jak przed zmiana.

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje: latki tylko na `TournamentMatch.End/Start` (gra konczy walke tak samo; nasz postfiks nic nie zmienia w walce) i `OnAgentRemoved` (tylko zapis do slownika).
  RBMTournament lata `Simulate`/`PrepareForMatch`/`GivePrizeToWinner`, TXP nie lata ani `End`, ani `ResolveTournament` (dekompilacja) - bez kolizji.
- Kolizje: inni sluchacze `TournamentFinished` (GrandTourney pula/utarg, Armoury sakiewka ludu f8, BK renoma) maja innych platnikow - nic nie liczy sie dwa razy.
- Zamknieta ekonomia: zloto tylko przelewem bohater -> bohater (zdarzenie gry z prawdziwym platnikiem) albo miasto -> bohater; sztuki tylko przenoszone.
  ALE: kon przeniesiony z ekwipunku bohatera do taboru/targu prawdopodobnie pojawia sie w liczonym swiecie koni Armoury (GoodsLedger liczy konie z rosterow; czy liczy tez ekwipunek bohaterow - nie sprawdzone do konca) -
  moze dac kilka koni "bez wyjasnienia" w ksiedze na dobe turnieju. Do sprawdzenia w linii ksiegi towarow po tescie.
- Rozgrywka: lord AI bez zlota oddaje zbroje i nic w grze (ani Armoury) nie kupuje bohaterom AI nowej - biedny lord albo wedrowiec moze zostac bez zbroi na stale.
  Licznik "oddane" w linii doby pokaze skale. Do decyzji Jeffa, jesli liczby beda duze.
- Wielkie turnieje (np. "Szranki Harroway: lordow w miescie 35, w drabince 16") = do 15 wykupow jednego dnia; duze kwoty plyna do rodow mistrzow.
- Okno gracza: tylko w stanie mapy, bez misji i rozmowy; `ShowInquiry` nie przelacza menu (zasada z CLAUDE.md 7). Wczytanie przed odpowiedzia = brak rozliczenia.

**Status.** DO SPRAWDZENIA (build GrandTourney 0 bledow, bez testu w grze - zakaz uruchamiania gry w tym zadaniu).

**Decyzje (Claude, Jeff: "ekonomie projektuje Claude").**
1. Liczy sie kazda przegrana walka (propozycja z listy), nie tylko final.
2. Stawka to prawdziwy kon i zbroja z ekwipunku bojowego (nie sprzet turnieju), wartosc w obecnym stanie z `ItemValue`.
3. Zwyciezca nad przegranym: kto go powalil, inaczej najlepszy wynikiem zwyciezca walki; w turnieju gry - drabinka pojedynkow z wynikiem symulacji gry.
4. Tylko bohaterowie; zolnierz i krewni - nic; smok nie przechodzi.
5. AI: najpierw wykup (od najdrozszej sztuki), ponad 7 dni zoldu; reszta oddana. Bez dlugow.
6. Zloto do glowy rodu zwyciezcy (jak nagroda w grze).
7. Wszystkie turnieje swiata (wszystkie i tak przechodza przez GrandTourney), nie tylko "Grand".
8. Nic w zapisie.

**Test w grze.** `GrandTourney.log`: `Wykup (R3-04a): latki 3/3`, potem co dobe `Wykup (R3-04a): dzien N | klucz Tourney Ransom tak ... | turnieje T, walki W
(turnieje gry S, szranki w misji M) | rozliczone R: wykupione sztuki A za G zlota, oddane B ...`, a przy kazdym turnieju `Wykup (R3-04a): turniej w X (...)`.
Regula dziala, gdy przy T > 0 jest R > 0 i A + B > 0. Gracz: wejdz do turnieju, przegraj - po wyjsciu z areny okno "Ransom of Arms"; linia `okno gracza:`.
