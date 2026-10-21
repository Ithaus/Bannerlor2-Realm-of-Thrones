## m102-tablica - tablica ogloszen w miescie: zadania gry, rozmowa z tablicy, listy gonczne kapitana strazy (Armoury)

**Dla Jeffa (prosto):** w kazdym miescie, zaraz pod "Go to the tavern district", jest nowa opcja "Read the notice board" (z wykrzyknikiem, gdy ktos ma dla Ciebie
robote). Na tablicy widzisz wszystkich, ktorzy daja zadania: kupcow, rzemieslnikow, hersztow, lordow w miescie i lordow rodu pana miasta, ktorzy sa w polu, oraz
soltysow i bogaczy z wsi tego miasta - z kwota (ta sama, ktora powie Ci w rozmowie), terminem i miejscem, gdzie ta osoba jest. Klik w wpis otwiera karte: co trzeba
zrobic, kto placi i czy go stac, ile zostalo czasu; z karty mozesz pojsc do tej osoby ("Go and see" - scena miasta przy niej), porozmawiac od razu ("Talk now")
albo postawic flage na mapie. Do wsi trzeba pojechac - tablica stawia flage na wsi.

**Problem:** Jeff 20.10 (doslownie): "Dopisz do nocnej pracy tablice ogloszen jak w grach RPG z misjami, aby misje byly przedstawione, i zobacz, czy sa misje,
aby wybic bandytow od kapitana strazy w danym miescie, bo tego brakuje, i jaka nagroda byla realnie, inne misje, jakie daje gra itp. Czy jest to mozliwe, aby
w miescie byla tablica i jak wchodze, mam liste lordow, kapitana strazy etc., ktorzy daja misje, i jak klikne, mam visit i rozmawiam z nim i opisuje mi misje?";
21.10 "i rob 5 i 7" (7 = tablica), "zapisuj przy kazdej zmianie". Projekt: PROJEKT-TABLICA-OGLOSZEN-2026-10-21.md (etapy E1 -> E3 -> E2 -> E2b).

---

### E1 - tablica z zadaniami gry (commit E1)

**Zasada (z liczbami):**
- Tablica tylko w miescie, nie w przebraniu (`Campaign.IsMainHeroDisguised`). Opcja `arm_board_enter` w menu "town" wstawiana w `OnAfterSessionLaunched`
  (po wszystkich `OnSessionLaunched` - opcje gry juz stoja) zaraz za `town_backstreet`; poza planem przenosin TownMenus (zostaje w menu miasta).
- Wpisy (na swiezo przy kazdym otwarciu, bez pamieci podrecznej - BK sam zamyka zadania): `IssueManager.GetIssuesInSettlement(miasto, false)` (notable, bohaterowie
  bez partii, lordowie w partiach w miescie), notable wsi miasta (`BoundVillages`, klucz NoticeBoardVillages), lordowie rodu pana miasta poza miastem (tylko flaga).
  Tylko sprawy wolne (`IsOngoingWithoutQuest`), jeden wpis na osobe, bez gracza i jego rodu.
- Kolejnosc: w miescie (grupa 1), wsie (2), nieobecni (3); w grupie od najlepiej platnych.
- Kwota: `QuestPay8.ShownReward` - getter `RewardGold` z postfiksem `RewardShown` (ta sama liczba co w rozmowie: robocizna N x D x zold x 1.25 przy QuestRewardInWageDays).
  Platnik: przy QuestRewardFromGiver kiesa zleceniodawcy - karta pisze "can afford it" albo "has only about N" (zaokraglone do 10 / 50).
- Rozwiazanie zastepcze gry: `GetTotalAlternativeSolutionNeededMenCount` i `GetTotalAlternativeSolutionDurationInDays`. Termin: `IssueDueTime` (brak - "no set time",
  po terminie - "may be taken down any day", gra zdejmuje z szansa 20% na dobe).
- Gdzie osoba: lokacja z `LocationComplex.GetLocationOfCharacter` (karczma, zamek, ulice, zaulki, arena, port), wies z odlegloscia i kierunkiem (km = jedn. x 4.75),
  partia w polu przy najblizszej osadzie.
- Przyciski karty (kazdy sprawdza sprawe na swiezo - inaczej "This notice has been taken down."):
  - "Go and see": tylko osoba w miescie, lokacja znana, wstep (`SettlementAccessModel.CanMainHeroAccessLocation` - brak wstepu = wyszarzone z powodem gry); kod jak
    pasek portretow gry (`GameMenuOverlay.ExecuteTroopAction` Conversation): alley -> center, `OnPlayerStartTalkFromMenu`, `CreateAndOpenMissionController(loc, null, postac)`;
    linia logu "odwiedziny" PRZED wejsciem do sceny.
  - "Talk now": osoba w miescie; `CampaignMapConversation.OpenConversation` jak QuickConversation gry.
  - "Mark on the map" / "Ride to X": `VisualTrackerManager.RegisterObject` (bohater albo wies); nasza flaga stoi 10 dob (zapis `arm_board`), potem zdjeta (nasz udzial
    w liczniku gry - zadanie gry trzyma swoj). Drugi klik zdejmuje flage.
- Hak autotestu (autotest nie klika menu): co 30 dob (doba % 30 == 5) tablice 10 miast (w tym `town_EW4`) budowane bez UI - liczby wpisow, nagrody, terminy.

**Zmiana:**
- nowy `Armoury/src/NoticeBoard.cs` (E1) i `NoticeBoard.Later.cs` (puste miejsca dla E3 i E2 - wypelniane w kolejnych commitach);
- `QuestPay8.ShownReward(IssueBase)` i `QuestPay8.PromisedReward(QuestBase)` (odczyt, bez zmiany wyplat);
- `ArmouryBehavior`: Reset (osobna linia `_m102TablicaReset`), zapis `arm_board` (SaveText.Sync, pisany tylko z danymi), `OnAfterSessionLaunched` -> `NoticeBoard.AddMenus`,
  doba -> `NoticeBoard.Daily` (po TownDebt.Daily, z `DayProfile.Lap`);
- `Settings.cs` grupa "--- Notice board ---": `NoticeBoard` (TAK), `NoticeBoardVillages` (TAK); `gen_mcm.py` przebiegl (2230 ustawien).

**Linie logu:**
- `Tablica ogloszen (m102): wpieta - menu town 1/1 (za town_backstreet: index N), podmenu 2/2, linie rozmowy L; klucze: ...`
- `Tablica ogloszen (m102): dzien N, klucz TAK, otwarcia A, wpisow sr. B (zadania, miasto, lordowie, wsie, nieobecni, straz, soltysi), odwiedziny, rozmowy teraz, flagi (czynne),
  z tablicy do zadania, odmowy dostepu, zdjete przed kliknieciem, nagroda nieczytelna, tekst z tokenami (z N tekstow), koszt otwarcia sr. ms, doba ms | proba: ... | potkniecia`
- `Tablica ogloszen (m102): odwiedziny - <hero> w <lokacja> (<miasto>)`, `rozmowa teraz - <hero> (<miasto>)`, `flaga - h:<id> (<miasto>)`

**Ryzyko / co sprawdzic (kontrola calosci):**
- Wejscie do sceny z menu: dokladnie kod paska portretow gry, tylko z opcji zwyklego menu (nie z menu oczekiwania - CLAUDE.md 7), linia logu przed scena. Niesprawdzone
  w grze: czy `CreateAndOpenMissionController(loc, null, postac)` sam zaczyna rozmowe po wejsciu, i do ktorego menu wraca gra po wyjsciu ze sceny (karta sprawdza sprawe na swiezo).
- Menu: `SwitchToMenu` tylko ze zwyklych menu (tablica i karta to nie menu oczekiwania). Menu rejestrowane zawsze - zapis zrobiony na tablicy wczyta sie po wylaczeniu klucza.
- Zadania gry, nagrody i AI bez zmian (sam odczyt); getter nagrody przez delegat QuestPay8 (bez `_raw`) - ta sama liczba co w rozmowie.
- Flagi: licznik referencji gry (`RegisterObject` / `RemoveTrackedObject`) - zdejmujemy tylko swoja czesc; po usunieciu modu flaga zostalaby w zapisie gry (do 10 dob nie zniknie sama).
- Wydajnosc: lista budowana tylko przy otwarciu tablicy; wykrzyknik w menu miasta to przeglad spraw miasta i wsi (bez nagrod i tekstow).
- TownMenus: opcja `arm_board_enter` nie jest w planie przenosin; wstawiona przed `TownMenus.Apply` (ten liczy kotwice `town_smithy` na zywej liscie - bez wplywu).

**Status:** DO SPRAWDZENIA (build kod 0; w grze nie testowane - zakaz uruchamiania gry w tej pracy).

**Test w grze:** otworzyc tablice w Krolewskiej Przystani i w dwoch innych miastach (lista, wykrzyknik, kwoty jak w rozmowie); "Go and see" do kupca w karczmie
i do lorda w sali (brak wstepu - wyszarzone z powodem); "Talk now" z notablem; flaga na wies i drugi klik zdejmuje; zapis i wczytanie na karcie (karta mowi "taken down",
"Back" dziala); `python -I tools/sprawdz_logi.py --grupa m102tablica <Armoury-*.log>`.

**Stan etapow po E1:** E1 zrobiony. Zostaly: E3, E2, E2b.

---

### E3 - rozmowa z tablicy, slowa zleceniodawcy, "Your tasks" (commit E3)

**Zasada (z liczbami):**
- "Go and see" i "Talk now" zapamietuja osobe i jej sprawe. W glownych opcjach rozmowy z ta osoba gracz ma linie "I read your notice on the board in X."
  (`hero_main_options` -> `issue_offer`, priorytet 111 - ta sama para stanow co linia gry `hero_give_issue` 110, bez skutku; dalej kwestie zadania z gry bez zmian).
  Warunki jak w grze: rozmowca nie jeniec, gracz i rozmowca nie na tratwie, sprawa wolna. Klucz `NoticeBoardOpening` (TAK).
- Osoba zapomniana przy koncu rozmowy z nia (`CampaignEvents.ConversationEnded`) i przy wyjezdzie gracza z miasta (`OnSettlementLeftEvent`). Gdy sprawa po
  rozmowie nie jest juz wolna (zadanie w toku albo wyslani ludzie) - licznik i linia "z tablicy do zadania".
- Karta: slowa zleceniodawcy (`IssueBriefByIssueGiver`, oczyszczone; z niewypelnionymi tokenami - opis gry `Description`, licznik "tekst z tokenami").
- "Your tasks": na gorze tablicy najwyzej 8 zadan gracza w toku (`QuestManager.Quests`, `IsOngoing`): tytul, zleceniodawca, kwota obiecana
  (`QuestPay8.PromisedReward` - zamrozona przy przyjeciu, najwyzej kwota gry), dni do `QuestDueTime` (ukryty czas - bez dni), gdzie jest zleceniodawca; karta z flaga na nim.
- Karta sprawy przyjetej przez gracza mowi "You have taken up this work" zamiast "taken down".

**Zmiana:** nowy `Armoury/src/NoticeBoard.Talk.cs`; puste miejsca E3 zdjete z `NoticeBoard.Later.cs`; `ArmouryBehavior`: nasluchy `ConversationEnded` i `OnSettlementLeftEvent`;
`Settings.cs`: `NoticeBoardOpening` (TAK); `gen_mcm.py` (2231 ustawien).

**Linie logu:** `Tablica ogloszen (m102): z tablicy do zadania - <hero> (<miasto>)`; w linii "wpieta" klucz Opening i liczba linii rozmowy (1 w E3, z kapitanem wiecej).

**Ryzyko / co sprawdzic:**
- Linia 111 stoi obok linii gry 110 ("I heard you may need some help...") - obie prowadza do tego samego `issue_offer`; nasza tylko, gdy gracz przyszedl z tablicy.
- Inne mody rozmow (AIInfluence, Diplomacy) moga dopisywac linie w `hero_main_options` - nie sprawdzone (projekt 6.8).
- "Your tasks" to sam odczyt zadan gry (bez zmian w zadaniach); kwota obiecana - ten sam slownik QuestPay8, co wyplata.

**Status:** DO SPRAWDZENIA (build kod 0).

**Test w grze:** z tablicy "Talk now" z kupcem - na liscie opcji "I read your notice on the board in ..."; po przyjeciu zadania karta tej osoby mowi "taken up",
a zadanie stoi na gorze tablicy z dniami i flaga na zleceniodawcy.

**Stan etapow po E3:** E1 i E3 zrobione. Zostaly: E2 (listy gonczne kapitana), E2b (soltysi).
