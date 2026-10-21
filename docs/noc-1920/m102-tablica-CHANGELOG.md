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

---

### E2 - listy gonczne kapitana strazy (commit E2)

**Dla Jeffa:** kapitana strazy nie ma w grze, w ROT ani w Banner Kings - teraz jest na tablicy. W kazdym miescie wisza najwyzej 2 listy gonczne na konkretne bandy
z okolicy ("WANTED: Jon Rivers and his bandits (20) - 375"). Kapitan (w Krolewskiej Przystani kapitan Zlotych Plaszczy) opowie o bandzie i oznaczy ja na mapie.
Kto rozbije bande - Ty albo lord - dostaje zaplate od razu, z kasy miasta (a gdy ta za mala, doplaca pan miasta); lup bandy i tak bierzesz osobno. Biedne
albo zadluzone miasto listow nie wywiesza - nic z niczego.

**Zasada (z liczbami):**
- Doba (`NoticeBoard.WatchDaily`, z `NoticeBoard.Daily`): jeden przebieg po `MobileParty.AllBanditParties` - bez Innych (`Undead.Party`), bez band w osadach
  (kryjowki - zadania soltysa z gry), bez band na morzu (`IsCurrentlyAtSea`), bez band w bitwie, od 5 zdrowych ludzi; miasto = najblizsze (wszystkie miasta, takze
  oblezone) w promieniu 45 jedn. (ok. 214 km).
- W miescie najwyzej `WatchBountiesPerTown` (2) listow; kolejnosc: najwieksza doplata za szkody, potem najwiecej ludzi. Termin `WatchBountyDays` (10 dni).
- Kwota = N x D x S x 1.25 x (1 + doplata): N = zdrowi ludzie bandy 10..60, D = 3 (4 przy wiekszosci konnych), S = pelny zold zalogi miasta na glowe
  (`MountedWage.FullTotalWage`; bez zalogi 5); doplata +25% za kazda wies miasta zlupiona przez bande w 60 dniach (`RaidCompletedEvent`), +15% za kazda rozbita
  karawane / woz wiejski okregu (`MapEventEnded`: bandyci wygrali), +50% za herszta-bohatera, razem najwyzej +100%. Przyklad: 20 ludzi, S = 5 -> 20 x 3 x 5 x 1.25 = 375.
- Wolna kasa = kasa - max(`TownPurse.Reserve`, `DsTowns.TdKeep`). List z kasy, gdy kwota <= 1/4 (wolnej kasy - kwot czynnych listow miasta); inaczej
  (`WatchBountyLordTopUp`) z doplata pana (AI, nie gracz), gdy ma ponad `AiGearGoldReserve` co najmniej kwote; inaczej brak listu ("brak kasy"). Rada z dlugiem
  (`TownDebt.ArrearsLike > 0`) i oblezone miasto nie wywieszaja nowych.
- Zdjecie: banda znikla albo nieaktywna, termin, zmiana pana miasta (rod przy wywieszeniu).
- Dowod (`MapEventEnded`, przed zniszczeniem partii w `HandleMapEventEnd`): banda po stronie przegranej z mniej niz 25% ludzi z chwili listu (albo nieaktywna).
  Odbiorca: gracz po stronie zwyciezcy; lord AI prowadzacy strone zwyciezcy (`WatchBountyAiLords`); nie wrog miasta (`IsAtWarWith`). Straz, milicja, patrol,
  karawana, wiesniacy, bandyci, nieznany - list zdjety bez wyplaty. Banda zniszczona bez bitwy (`MobilePartyDestroyed`) - list zdjety.
- Wyplata od razu: kasa miasta `min(kwota, wolna kasa)` (`GiveGoldAction.ApplyForSettlementToCharacter`), reszta od pana miasta z jego zlota ponad rezerwe
  (`ApplyBetweenCharacters`; nie gdy pan to gracz albo sam odbiorca), brak - niedoplata z komunikatem "The council could pay only N of the promised M.".
  Bezpieczenstwo miasta +2, slawa +1. Komunikat gracza z powodem (bez "You received N" gry).
- Kapitan: twarz `goldcloak_captain` w `town_EW4`, inaczej piechur z najwyzszym poziomem z zalogi (na start `EliteBasicTroop` kultury pana); rozmowa
  `CampaignMapConversation.OpenConversation` z partia zalogi (wzor gry "Talk to the garrison commander"); bez zalogi opcja wyszarzona. Dialog `start` priorytet 310
  z flaga naszej rozmowy (zdejmowana w `ConversationEnded`): "I'll hunt them down" (przyjety, flaga na bandzie do konca listu), "Who pays, exactly?", "Not now".
  Straz: "the watch" (Westeros), "the city guard" (Essos), "the Gold Cloaks" (`town_EW4`). Nazwa bandy z plotek (`Rumours.BandName`, nowy odczyt) - nie w zapisie.

**Zmiana:** nowy `Armoury/src/NoticeBoard.Watch.cs`; `NoticeBoard.Later.cs` - zostaly tylko miejsca E2b; `Rumours.BandName` (odczyt); `ArmouryBehavior`: nasluchy
`MapEventEnded`, `RaidCompletedEvent`, `MobilePartyDestroyed` (po CirculationWindows - tamten zostaje pierwszy); `Settings.cs`: `WatchBounties` (TAK),
`WatchBountiesPerTown` (2), `WatchBountyDays` (10), `WatchBountyAiLords` (TAK), `WatchBountyLordTopUp` (TAK); `gen_mcm.py`: zakresy 0..5 i 1..60 (2236 ustawien);
`tools/sprawdz_logi.py --grupa m102tablica` (T-1..T-7 i L-1..L-10, L-K; sprawdzone na sztucznym logu).

**Zapis:** `arm_board` = `nb1|F=flagi|W=miasto~banda~~ludzie~dni~doplata~kwota~od~do~flagi~S*100~rodzaj~soltys~wies~rod pana;...|H=banda~doba,rodzaj,miasto/...`
(ok. 90 B na list, 2 x 97 miast - ok. 17 KB w najgorszym razie; prog L-8 16 KB pokaze, czy trzeba skrocic).

**Linie logu:** `Listy goncze (m102): wpiete - zdarzenia 3/3 ..., dialog kapitana 7/7, opcja karty 1/1, zapis arm_board nb1`; `list - <miasto> <banda> (N ludzi, D d, S s,
szkody +p%) = kwota, platnik kasa|pan, wolna kasa F - nazwa`; `wyplata - <miasto> <banda> obiecane M, kasa a + pan b, niedoplata c, dla gracz|lord id; kasa przed g0 po g1 (Keep k)`;
`zdjety bez wyplaty - ...`; `przyjety przez gracza - ...`; `rozmowa z kapitanem - ...`; linia doby z miastami, powodami, wyplatami, zdjeciami, zapisem, kosztem i potknieciami.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Nic z niczego: kazda wyplata to przelew kasa miasta -> odbiorca albo pan -> odbiorca (zdarzenie gry `HeroOrPartyTradedGold` - ksiega Obieg je widzi); kasa
  nigdy ponizej Keep (wyplata najwyzej wolna kasa). Ta sama banda w zadaniu kupca i w liscie - dwie zaplaty od dwoch platnikow (projekt 4.5).
- Lup bandy (BeatenPurse, skarbiec kryjowki) bez zmian - nagroda jest osobno. AI lordow nie zmieniamy (bez celow AI) - tylko czasem dostaja zaplate (L-7).
- Kolejnosc nasluchow: `MapEventEnded` - nasz tylko czyta bitwe i placi z kasy miasta (bez wplywu na inne nasluchy); `MobilePartyDestroyed` - tylko zdjecie listu
  (bez przelewu), CirculationWindows zostaje pierwszym dopisanym (biegnie ostatni).
- Rozmowa z kapitanem: szablon zolnierza (nie bohater) - flaga naszej rozmowy i ta sama postac, priorytet 310 nad 300 z gry; flaga zdejmowana na koncu kazdej rozmowy.
- Nie sprawdzone w grze: ile band jest poza osadami w ROT (ROT_Hideouts, ROTBanditLeaveHideouts), typowe S zalog ROT, czy promien 45 jedn. pasuje do mapy ROT
  (linia doby pokaze "band poza osadami" i "miasta z banda").

**Status:** DO SPRAWDZENIA (build kod 0; gra nie uruchamiana).

**Test w grze:** tablica w Krolewskiej Przystani - wpis kapitana Zlotych Plaszczy, rozmowa, "I'll hunt them down" stawia flage na bandzie; rozbic bande - komunikat
zaplaty, linia "wyplata" (obiecane = kasa + pan + niedoplata); zapis i wczytanie z czynnym listem (list stoi, nazwa bandy wraca); wczytanie z `WatchBounties` NIE - bez bledu,
listy znikaja z tablicy; `python -I tools/sprawdz_logi.py --grupa m102tablica <Armoury-*.log>` po tescie 120 dob (L-7: gdy nie przejdzie - domyslne WatchBountyAiLords = false).

**Stan etapow po E2:** E1, E3, E2 zrobione. Zostal E2b (soltysi).
