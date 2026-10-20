## p5-turniej (20.10) - R3-04a: wykup konia i zbroi pokonanego w turnieju (GrandTourney)

**Dla Jeffa (prosto).** W kazdym turnieju rycerz, ktory przegra walke, oddaje zwyciezcy swojego konia i zbroje (glowa, tulow, nogi, rece, plaszcz, kon, ladry)
albo wykupuje je za ich wartosc w obecnym stanie: zuzyta zbroja kosztuje mniej. Gdy przegrasz Ty albo Twoj towarzysz (tylko w turnieju, w ktorym sam walczysz),
po wyjsciu z areny pokaze sie okno "Ransom of Arms": "Pay N" zatrzymuje rzeczy za zloto, a "Yield..." pyta jeszcze raz, zanim cokolwiek oddasz (Esc niczego nie oddaje).
Lordowie AI placa tylko z tego, co ich rod ma ponad swoj skarbiec wojenny. Zwyciezca zaklada zdobyta rzecz, jesli jest lepsza od jego wlasnej, a pokonany kupuje
sobie na targu zwykla zbroje i konia, wiec nikt nie zostaje nagi. Damy, mieszczanie, zwykli zolnierze, krewni i turnieje odwolane przez wojne niczego nie rozliczaja.

**Problem / zrodlo pomyslu.** Jeff 20.10, lista 35 pomyslow (docs/POMYSLY-35-2026-10-20.md, pkt 6, R3-04a): "28 - nie, 29 - nie. POzostale tak". Zrodlo:
docs/audyt-2026-10-09/04-wojna-armie-lupy-okupy.md rozdz. 2 pkt 8 (Lore GoT: Dunk i ser Uthor Underleaf - przegrany oddaje konia i zbroje albo je wykupuje) i N6.
Dotad turniej nie mial zadnej stawki poza nagroda i zakladem (GrandTourney/src - brak wykupu; Armoury TourneyPay/TourneyFolkPurse - tylko nagroda, zaklad, sakiewka).

**Zmiana.** Nowy plik `GrandTourney/src/TourneyRansom.cs`. Wpiecia: pole `TourneyRansom` w `Settings.cs` (i z gen_mcm w `McmSettings.cs`), 3 linie i `OnApplicationTick`
w `SubModuleMain.cs`, referencja `TaleWorlds.DotNet` w `GrandTourney.csproj` (wymagaja jej Agent i Mission), 3 wywolania odwolania w `TourneyBehavior.cs`
(`ResolveTournament` -> `TourneyRansom.CallOff`).
- **Przegrana walka** = kazda walka, z ktorej uczestnik nie przeszedl dalej (kazdy oprocz zwyciezcy turnieju placi raz).
  - Szranki w misji (gracz walczy albo oglada; walki pominiete gra konczy tym samym `TournamentMatch.End`): postfiks na `TournamentMatch.End`.
    Zwyciezca nad przegranym: kto go powalil (postfiks na `TournamentFightMissionController.OnAgentRemoved`, tylko stan Unconscious/Killed, jezdziec, gdy uderzyl kon),
    jesli przeszedl dalej i jest z innej druzyny; inaczej najlepszy wynikiem zwyciezca walki z innej druzyny; inaczej najlepszy zwyciezca walki. Rozliczenie dopiero
    po misji (`OnApplicationTick`: brak misji, stan mapy, brak rozmowy), wiec ekwipunek i okna nie zmieniaja sie w arenie.
  - **Rezygnacja gracza** (Leave albo poddanie walki - `TournamentBehavior.SkipMatch`, gdy gracz stoi na nogach): gra zdejmuje cala druzyne gracza bez walki,
    wiec gracz i jego druzyna (kto nie zostal powalony) nic nie oddaja. Reszta tej walki i dalsze walki symulowane - jak zwykle.
    Latka: prefiks + finalizer na `TournamentBehavior.SkipMatch` (flaga na czas pominiecia); `TournamentMatch.Start` wolany z `SkipMatch` nie czysci juz powalen.
  - Turniej gry bez gracza (`TournamentManager.ResolveTournament` zna tylko zwyciezce): drabinka pojedynkow 16 -> 8 -> 4 -> 2 -> 1 z listy uczestnikow gry,
    zwyciezca gry wygrywa swoje pojedynki, reszte rozstrzyga wynik symulacji gry (`GetTournamentSimulationScore x (0.75 + 0.25 x los)` - wzor `SimulateTournament`).
  - **Turniej odwolany przez GrandTourney** ("wrog u bram", 3 miejsca w `TourneyBehavior`): idzie przez `TourneyRansom.CallOff`. Gra i tak go symuluje
    i wola `TournamentFinished`, ale w trakcie `CallOff` wykupu nie ma (licznik "odwolane bez wykupu" i linia `turniej w X odwolany przez GrandTourney`).
- **Kto stawia:** tylko rycerze: lord, wedrowiec albo bohater rodu gracza, walczacy (`!IsNoncombatant` - damy, ktore `NoblesFightPatch` wpisuje do drabinki,
  nic nie stawiaja) i nie notabl (RBMTournament dopisuje notabli z tierem >= 5). Towarzysze i krewni gracza tylko wtedy, gdy gracz sam stal w tych szrankach
  (gra wpisuje ich tez, gdy gracz tylko oglada, albo w turnieju gry, gdy jego druzyna stoi w miescie). Walka z zolnierzem - nic. Ten sam rod - nic. Zmarly - nic.
- **Stawka:** kon i zbroja z `Hero.BattleEquipment` (Body, Horse, Head, Leg, Gloves, Cape, HorseHarness), kazda sztuka za `EquipmentElement.ItemValue`
  (cena x mnoznik stanu: zuzycie Armoury, lup Spoils of War; ceny historyczne Armoury, gdy sa). Smok (test jak Armoury `DragonFeed.IsDragon`) i jego ladry - nie.
  Wspolny zestaw zmarlych (`Campaign.DeadBattleEquipment`) - nigdy nie ruszany.
- **Wykup AI** sztuka po sztuce od najdrozszej. Placi przegrany, potem glowa jego rodu, lacznie najwyzej **nadwyzka rodu**: `G - R` z budzetu rodu Armoury
  (`ClanBudget.ChestOf` przez refleksje - ta sama rezerwa R, ktora tnie pulap zoldu) minus to, co rod juz dzis wydal na wykupy i zamienniki.
  Bez budzetu Armoury (rod poza krolestwem, dluznik, zajecie, kompania najemna, brak Armoury): ten sam ksztalt - G = kiesy doroslych rodu,
  R = max(20 000; 20 dni zoldu wszystkich partii i zalog rodu) + 5 000 na doroslego. Wedrowiec bez rodu - cala wlasna kiesa. Na co nie stac - oddane.
- **Gracz i rod gracza:** okno "Ransom of Arms" (bez progu - decyduje gracz; kiesa towarzysza, potem gracza): "Pay N" (najdrozsze sztuki, na ktore starcza zlota,
  reszta oddana) albo "Yield..." -> drugie okno "Yield" / "Back". Esc w pierwszym oknie otwiera drugie, Esc w drugim wraca do pierwszego - Esc nigdy nie oddaje.
  Kazde okno otwiera sie w kolejnej klatce mapy, nie z wnetrza zamykanego okna.
- **Zloto:** `GiveGoldAction.ApplyBetweenCharacters(platnik, odbiorca)`, odbiorca = glowa rodu zwyciezcy (jak nagroda w grze `GivePrizeToWinner`), bez rodu - sam zwyciezca.
- **Oddane sztuki** (z tym samym stanem) schodza z ekwipunku przegranego. Zwyciezca AI **zaklada** sztuke, gdy moze ja nosic (slot, plec, wymog umiejetnosci
  `CharacterHelper.CanUseItem`, kon wierzchowy zgodny z ladrami) i jest wiecej warta od jego (smoka nie zamienia) - wtedy dalej idzie jego stara sztuka.
  Dalej: rod gracza - juki gracza (albo jego partii); AI - partia zwyciezcy, inaczej partia glowy rodu (lord sprzeda w miescie jak lup); zwyciezca bez partii -
  targ miasta turnieju, miasto placi mu wartosc z nadwyzki ponad rezerwe (`TownWageLink.Spare`). Sztuka bez miejsca docelowego zostaje u przegranego.
- **Zamiennik dla przegranego AI** (tulow, glowa, nogi, rece, kon; plaszcz i ladry nie): najpierw najlepsza pasujaca sztuka z taboru partii, w ktorej jedzie
  (partia jego rodu; za darmo - to jego), potem targ miasta: najlepsza sztuka za nadwyzke rodu, a gdy nadwyzki brak - najtansza, na ktora ma zloto (kiesa + glowa rodu;
  bez konia i zbroi rycerz nie pojdzie na wojne). Zloto -> kasa miasta (`GiveGoldAction.ApplyForCharacterToSettlement`), sztuka z polki. Kogo nie stac albo gdzie
  na targu nic nie ma - trafia na liste (w zapisie `gt_ransomBare`) i co dobe probuje w miescie, w ktorym stoi (jeniec czeka).
- **Stan:** pamiec (walki misji do rozliczenia, kolejka okien gracza, liczniki doby) + w zapisie lista bohaterow bez zamiennika (`List<string>` "id|maska slotow"
  przez `dataStore.SyncData`, jak `gt_events` w `TourneyBehavior`; stary zapis bez klucza = pusta lista; lista jest krotka, wiec bez `SaveText`).
  Wczytanie przed odpowiedzia w oknie gracza = brak rozliczenia (sprzet zostaje). Wyjatek - licznik potkniec per obiekt, nic nie gasimy.
- **Klucz MCM (grupa "The lists themselves"):** `Tourney Ransom` (tak). Wylaczony = turnieje jak przed zmiana (lista bez zamiennika czeka, nic nie kupuje).
  Klucz `Ransom Ai Keep Wage Days` z pierwszej wersji usuniety (nie byl wgrany; zastapila go nadwyzka rodu z budzetu Armoury).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje: latki tylko na `TournamentMatch.End/Start` (postfiksy nic nie zmieniaja w walce), `OnAgentRemoved` (zapis do slownika) i `TournamentBehavior.SkipMatch`
  (flaga; finalizer bez `__exception` przepuszcza wyjatek gry). RBMTournament lata `Simulate`/`PrepareForMatch`/`GivePrizeToWinner` i prefiks
  `GetParticipantCharacters` (sklad drabinki turnieju gracza: bohaterowie z tierem >= 5, takze notable - u nas nie stawiaja); TXP nie lata ani `End`,
  ani `ResolveTournament` - bez kolizji. `CallOff` zmienia tylko droge wywolania - `ResolveTournament` robi to samo co dotad (nagroda, renoma, sluchacze).
- Kolizje: inni sluchacze `TournamentFinished` (GrandTourney pula/utarg, Armoury sakiewka ludu f8, BK renoma) maja innych platnikow - nic nie liczy sie dwa razy.
  Wydatki rodu AI: jedna zasada z budzetem rodu Armoury (tylko ponad R) - wykup nie zbija rodu pod R; jedynie zakup najtanszej zbroi/konia przy braku nadwyzki
  bierze z kiesy ponizej R (male kwoty; licznik "najtansze ... za N zlota" pokaze skale).
- Zamknieta ekonomia: zloto tylko przelewem bohater -> bohater, miasto -> bohater (z nadwyzki) i bohater -> miasto (zakup z polki); sztuki tylko przenoszone
  (ekwipunek -> ekwipunek / tabor / polka i polka / tabor -> ekwipunek). Ksiega koni Armoury (`ArmsBook`, HorseBook) liczy konie z ekwipunku bohaterow, taborow
  i polek, wiec przeniesienia sa neutralne - nie bedzie koni "bez wyjasnienia".
- Rozgrywka: lordowie AI zmieniaja sprzet w turniejach (zwyciezcy lepszy, pokonani zwyklejszy); unikat zwyciezcy zostaje, bo zamiana tylko na drozsza sztuke.
  Wielkie turnieje (np. "Szranki Harroway: lordow w miescie 35, w drabince 16") = do 15 rozliczen jednego dnia - linia doby pokazuje najwiekszy wydatek jednego rodu.
- Wydajnosc: zakupy przegladaja polke miasta tylko przy przegranej (kilka razy na dobe); raz na dobe przeglad wszystkich zywych bohaterow (tlo "swiat AI").
  Wszystko w watku glownym (zdarzenia kampanii, tick aplikacji) - bez watkow AI.
- Okno gracza: tylko w stanie mapy, bez misji i rozmowy; `ShowInquiry` nie przelacza menu (zasada z CLAUDE.md 7).

**Status.** DO SPRAWDZENIA (build GrandTourney: kod 0, 0 bledow, 0 ostrzezen; bez testu w grze - zakaz uruchamiania gry w tym zadaniu).

**Decyzje (Claude, Jeff: "ekonomie projektuje Claude").**
1. Liczy sie kazda przegrana walka (propozycja z listy), nie tylko final.
2. Stawka to prawdziwy kon i zbroja z ekwipunku bojowego (nie sprzet turnieju), wartosc w obecnym stanie z `ItemValue`.
3. Zwyciezca nad przegranym: kto go powalil, inaczej najlepszy wynikiem zwyciezca walki; w turnieju gry - drabinka pojedynkow z wynikiem symulacji gry.
4. Tylko rycerze (lord, wedrowiec, rod gracza; walczacy, nie notabl); zolnierz i krewni - nic; smok nie przechodzi.
5. Rod gracza (poza graczem) tylko w szrankach, w ktorych gracz sam stoi - nikt nie traci sprzetu w turnieju, do ktorego gracz go nie zglosil.
6. Rezygnacja gracza (Leave / poddanie) to nie przegrana - nikt go nie pokonal; jego druzyna nic nie oddaje.
7. Turniej odwolany przez wojne nie rozlicza wykupu.
8. AI: wykup tylko z nadwyzki rodu ponad rezerwe budzetu rodu Armoury (jedna zasada wydatkow rodu), bez dlugow i rat; reszta oddana.
9. Zwyciezca AI zaklada lepsza sztuke; pokonany AI uzupelnia tulow, glowe, nogi, rece i konia (tabor, potem targ: najlepsza za nadwyzke, inaczej najtansza).
10. Zloto do glowy rodu zwyciezcy (jak nagroda w grze).
11. Wszystkie turnieje swiata (wszystkie i tak przechodza przez GrandTourney), nie tylko "Grand".
12. W zapisie tylko lista bohaterow bez zamiennika.

**Test w grze.** Linie sa w `Modules/GrandTourney/GrandTourney.log` (nie w Armoury.log):
- start: `Wykup (R3-04a): latki 4/4 (...)` i `Wykup (R3-04a): nadwyzka rodu z budzetu rodu Armoury (ClanBudget.ChestOf) - PODPIETA`;
- co dobe: `Wykup (R3-04a): dzien N | ... | turnieje T, walki W (...), odwolane bez wykupu O | rozliczone R: wykupione sztuki A za G zlota, oddane B (...):
  zalozone przez zwyciezce Z, ... | najwiecej jeden rod X (...) | zamiennik: z taboru ..., kupione najlepsze ..., najtansze ... , bez zamiennika ..., czeka bohaterow K |
  swiat AI: bez zbroi tulowia ..., bez konia ... | pominiete: ..., nie rycerz (dama/notabl) ..., rod gracza bez gracza w szrankach ..., rezygnacja gracza ... | potkniecia 0`;
- przy kazdym turnieju: `Wykup (R3-04a): turniej w X (...)`; przy odwolaniu: `Wykup (R3-04a): turniej w X odwolany przez GrandTourney`.
Regula dziala, gdy przy T > 0 jest R > 0 i A + B > 0, potkniecia 0, "czeka bohaterow" nie rosnie z dnia na dzien, a "swiat AI: bez zbroi tulowia / bez konia"
nie rosnie w biegu 160 dob ponad poziom z pierwszej doby. Gracz: wejdz do turnieju i przegraj - po wyjsciu z areny okno "Ransom of Arms", Esc otwiera potwierdzenie,
w logu `okno gracza:`. Wejdz i daj "Leave" przed swoja walka - zadnego okna, w linii doby "rezygnacja gracza".

**Recenzja 20.10 (PELNA) - co zrobiono.**
1. (wazne) Odwolane turnieje rozliczaly wykup - PRAWDA (vanilla `ResolveTournament` symuluje turniej, gdy miasto nie jest oblegane). Poprawione: `CallOff`.
2. (wazne) Esc = "Yield" - PRAWDA (`GauntletQueryManager` wola akcje negatywna klawiszem Exit). Poprawione: dwa okna, Esc tylko otwiera potwierdzenie / cofa.
3. (wazne) Stawialy damy, notable i towarzysze bez zgody gracza - PRAWDA. Poprawione: `Knight` (lord/wedrowiec/rod gracza, walczacy, nie notabl) i rod gracza tylko,
   gdy gracz sam stal w szrankach (zapamietane z uczestnikow walk tej misji). `NoblesFightPatch` bez zmian (sklad drabinki - poza zadaniem).
4. (wazne) AI bez drogi powrotu sprzetu - PRAWDA. Poprawione: zwyciezca zaklada lepsza sztuke, pokonany dostaje zamiennik (tabor, targ), lista czekajacych w zapisie,
   licznik "swiat AI: bez zbroi tulowia / bez konia" w linii doby.
5. (wazne) Rezerwa 7 dni zoldu wlasnej partii obok ClanBudget - PRAWDA. Poprawione: nadwyzka rodu z `ClanBudget.ChestOf` (G - R) minus wydane dzis, zapas bez Armoury
   w tym samym ksztalcie; klucz `Ransom Ai Keep Wage Days` usuniety; w linii doby najwiekszy wydatek jednego rodu.
6. (drobne) Rezygnacja liczona jak przegrana - PRAWDA. Poprawione: latka `TournamentBehavior.SkipMatch`; przy okazji `Start` z wnetrza `SkipMatch` czyscil powalenia
   z trwajacej walki (gracz powalony, potem "skip") - juz nie czysci; powalenie liczy sie tylko w stanie Unconscious/Killed (nie sprzatanie areny).
7. (drobne) Wpis: obawa o konie "bez wyjasnienia" byla bezpodstawna - PRAWDA (`ArmsBook` liczy konie z ekwipunku bohaterow); poprawione w "Ryzyko". Dopisana latka
   RBMTournament `GetParticipantCharacters` (sklad drabinki turnieju gracza; notable z niej nie stawiaja).
