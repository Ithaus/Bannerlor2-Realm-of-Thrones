# m103-sceny - E4: sceny prologu 298 jako okna ROT, obrazki z plikow (prolog, okna ROT, pozniej Kronika)

Galaz `w-toku/m103-sceny` (baza n30-calosc ed9273fa = WGRANIE 101). Projekt: PROJEKT-PROLOG-298-2026-10-21.md pkt 2 (os dni 0-188, 23 sceny),
PROMPTY-OBRAZKI-PROLOG-2026-10-21.md (obrazki prolog_01..23). Rozpoznanie: 5 agentow + krytyk (journal wf_6b8877cd-0c3), architektura krytyka kroki 1-12.
Jeff 21.10: "I zrob wydarzenia wedlug ksiazki"; o Kronice: "Ale Pamietaj ze ja che wydarzenia z calej ksiazki i potem z serialu jak sie konczy ksiazka aby to
obejmowalo cala fabule" - dlatego okno (ArmStoryEvent/StoryWindow) i obrazki (StoryPictures) sa OGOLNE: prolog teraz (A Game of Thrones do aresztu Neda),
Kronika Westeros (cala ksiazka, potem serial po koncu ksiazek) pozniej, na tych samych klasach (katalog Images\Kronika, pliki kronika_<id>). Tej galezi Kronika
NIE obejmuje - to osobny etap.

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| K1 | Armoury.csproj: referencje ROT.dll i TaleWorlds.TwoDimension.dll (RotLibs / TwoDimLibs: $(GameLibs), zapas katalogi gry; Private=false), libs/README.md | ZROBIONE (72e91be4, build 0) |
| K2 | StoryPictures (obrazki z plikow jako sprite'y Gauntlet) + StoryWindow / ArmStoryEvent (ogolne okno w wygladzie ROT, zapas ShowInquiry) | ZROBIONE (e12778aa, build 0) |
| K3 | PrologueScenes.Data.cs - 23 sceny (TextObject ARM_PRL_NN_T/U/L), dlugosci linie.py (najwyzej 7 linii), zlagodzone 118/125/155 | ZROBIONE (9e5bf4cf, build 0) |
| K4 | Prologue.Scenes.cs - straznik w OnApplicationTick, maska sc=/sk= w zapisie, pomijanie, klucz PrologueScenes, OnGameEnd | ZROBIONE (8ade7536, build 0, gen_mcm) |
| K5 | RotWindowPictures - obrazki Jeffa w oknach wydarzen ROT (rot_<Klasa>_<n>.png), klucz StoryImagesForRotWindows | ZROBIONE (34ea2dd4, build 0, gen_mcm) |
| K6 | tools/obrazki_okien.py + skrypty wgrania/testu (katalog Armoury\Images) | ZROBIONE (5f109708; skrypty poza drzewem - nizej) |
| K7 | grupa tools/sprawdz_logi.py m103sceny (SC-0..SC-14, --rgl) + test na 13 logach syntetycznych | ZROBIONE (b5b8a709, WYNIK OK) |
| P1 | przeglad: okno ROT na wierzchu = nasze, gdy EventPopupState.Event nieczytelne; tekstura bez wskaznika = zly plik | ZROBIONE (c7517ee5, build 5 modow 0) |
| P2 | obecnosc ROT (AccessTools.TypeByName - przeszukuje wszystkie zestawy) sprawdzana raz na proces, nie przy kazdej scenie | ZROBIONE (0535b5c5, build 5 modow 0, Armoury md5 7a002807) |
| R1 | recenzja: bramka scen - bez ekranu wczytywania (LoadingWindow) i mapa gotowa (MapScreen.IsReady) | ZROBIONE (6c842729, build 0) |
| R2 | recenzja: okna ROT z prefabem Blackwater bez obrazka z pliku (33 okna z obrazkiem, 2 bez); obrazki_okien.py, README | ZROBIONE (8c120835, build 0) |
| R3 | recenzja: kontrola przed zwolnieniem tekstury (linia ZWOLNIENIE PRZY OKNIE), SC-13 na tej linii, SC-2 ze scenami z zapisu, SC-1/SC-13 w kampanii klasycznej | ZROBIONE (9e24e937, test 14 przypadkow OK) |
| R4 | recenzja: teksty scen 03, 07, 09, 20 (bez obietnic mapy i gracza, dalszy ciag z ksiazki) | ZROBIONE (e4aa6174, linie.py najwyzej 7) |
| R5 | recenzja: wgraj-v5.ps1 przenosi stary format obrazka (.png/.jpg/.jpeg) do .bak; bieg-bitwa.ps1 (repo) - UWAGA | ZROBIONE (poza drzewem, parser 0, test na drzewie probnym) |
| AT | autotest: 103-prolog 230 dob + 103-prolog-zapis + 103-bitwa + 103-160 z zapisu (wzor testy-101; Jeff 21.10 - bez osobnej "nowej") | DO ZROBIENIA (gra zajeta autotestem w tle - nie uruchamialem) |

## Problem

Prolog 298 (WGRANIE 101) zmienia swiat (Robert i Ned zyja, pakty, koniec prologu w dniu 186), ale gracz nie dostaje opowiesci: 188 dni bez okien, podczas gdy
klasyczny ROT opowiada okna wydarzen. Jeff chce wydarzen "wedlug ksiazki" w wygladzie okien ROT, z wlasnymi obrazkami (generuje je sam - prolog_01..23, a takze
obrazki do glownych wydarzen ROT).

## Rozpoznanie (dekompilacja ROT 8.1.8 i gry 1.4.8 - szczegoly w journalu rozpoznania)

- `ROT.Events.EventBase` jest public abstract; Title/UpperText/LowerText/HeaderImage/BackgroundImage/przyciski - public virtual z setterem protected;
  `protected OpenPopup()` = `PushState(CreateState<EventPopupState>(this))`; Initialize/ConditionsMet/ConditionsHold - abstract; HandleDone/CancelButton - virtual.
  Zapisywane sa tylko IsActive/TotalEvents/EventStartTime/Quest i tylko obiekty w `ROTEventBehavior._events` (definer zna wylacznie EventBase).
- `EventScreen` ([GameStateScreen(EventPopupState)]): konstruktor laduje kategorie `ui_event_category` (brak = KeyNotFound), OnActivate tworzy EventPanel i LoadMovie
  "EventXml" (dzialajacy prefab z ROT-Content: obrazek 1024x576, Galahad 25, srodki akapitow 120 px od siebie), tytuly Blackwater przelaczaja prefab;
  OnFrameTick: Esc ZAWSZE ExecuteCancel (bez IsCancelEnabled), Enter ExecuteDone. EventPanel: najpierw PopState, potem Handle*.
- Obrazek: wiazanie `Sprite="@HeaderImage"` zamienia napis na sprite przez `SpriteData.GetSprite` (slownik `UIResourceManager.SpriteData.Sprites`, publiczny);
  `Texture.CreateFromMemory(byte[])` (gra: AvatarThumbnailCache, dekoder stb_image), `EngineTexture`, `TwoDimension.Texture`, podklasa `Sprite` (UV 0..1).
  Zwolnienie: `ITexture.Release` -> ReleaseImmediately i Texture = null (DrawImage pomija); zwolnienie w trakcie rysowania = FailedAssert "Trying to render a
  released texture".
- Autotest (n22-at AutotestUi): okno = stan `ROT.CampaignBehaviors.EventPopupState` + pole EventPanel na TopScreen, po 1.5 s ExecuteDone, linia
  `AUTOTEST: OKNO wydarzenie ROT "<tytul>"` - podklasa EventBase jest obslugiwana bez zmian CrashScribe.
- Bramka PlayerEncounter: w menu miasta i przy czekaniu w miescie `PlayerEncounter.Current != null` (EncounterManager startuje go przy wjezdzie) - bramka
  "PlayerEncounter == null" dalaby zero scen w autotescie (town_wait_menus); wlasciwa: brak spotkania ALBO spotkanie w osadzie bez bitwy.
- Okno startowe BK = MultiSelectionInquiry (IsAnyInquiryActive); okno mapy BK = warstwa "BannerKingsMapView" (FindLayer, publiczne API).

## Zmiana

1. **Armoury.csproj, libs/README.md** - `RotLibs` (ROT.dll z $(GameLibs), zapas Modules/ROT-Core/bin/Win64_Shipping_Client) i `TwoDimLibs`
   (TaleWorlds.TwoDimension.dll z $(GameLibs), zapas bin/Win64_Shipping_Client), Private=false. W bin/Release tylko Armoury.dll.
2. **StoryWindow.cs** (nowy) - ogolne okno opowiesci: `Spec` (tytul, 2 akapity, obrazek, tlo, 1-2 przyciski, akcja `Closed(spec, jak)`), `Show` -> okno ROT
   (`ArmStoryEvent : ROT.Events.EventBase` tworzone tylko w metodach NoInlining; nigdy w `_events`, nigdy StartEvent; tytul Blackwater dostaje kropke) albo
   zapas `InformationManager.ShowInquiry` z tym samym tekstem i pauza (brak ROT, brak `ui_event_category`, wyjatek - po wyjatku przy pchnieciu okno zdejmowane
   PopState, zeby gra nie stanela). HandleDone == HandleCancel przy jednym przycisku (Esc), zamkniecie idempotentne (`Spec.Done`). `Watch` - okno zamkniete bez
   przycisku (cudzy PopState) albo schowany zapas. Jedno okno naraz. Licznik potkniec.
3. **StoryPictures.cs** (nowy) - obrazki z plikow `Modules\Armoury\Images\<Prolog|ROT|Kronika>\<nazwa>.png|jpg|jpeg`, sprawdzane przy kazdym pokazaniu
   (data, rozmiar) - nowy plik bez przebudowy DLL. WALIDACJA BAJTOW przed dekoderem natywnym: PNG - sygnatura, IHDR pierwszy, kazdy blok z typem z liter i
   zgodnym CRC, IDAT, IEND; JPG - SOI, EOI na koncu, SOF z wymiarami; wymiary 1..4096; same zera = "plik wyzerowany na dysku"; rozmiar 1 B..48 MB. Zly plik =
   null (okno bierze sprite ROT) + linia `ZLY PLIK` raz na zmiane pliku. Sprite `arm_<nazwa>` (ArmFileSprite) wpisywany do SpriteData.Sprites tuz przed oknem;
   unikalna nazwa tekstury silnika; najwyzej jeden obrazek w uzyciu, poprzednie do kolejki zwolnien - zwalniane w AppTick, gdy na stosie stanow nie ma
   EventPopupState i minela 1 s; ten sam plik z kolejki wraca bez dekodowania. ReleaseAll przy OnGameEnd (od razu), DropDir(Prolog) przy koncu scen.
4. **PrologueScenes.Data.cs** (nowy, generowany: tools/prolog/gen_sceny_data.py z tools/prolog/prolog298_sceny.json) - 23 sceny: dzien, plik obrazka
   prolog_NN, sprite zapasowy ROT, TextObject `{=ARM_PRL_NN_T|U|L}`, przycisk `{=ROTasSnYXW5}Continue...`. Teksty wedlug A Game of Thrones (wlasne slowa).
   Najwiecej 7 linii (linie.py: gorny do 4, dolny do 3). Zlagodzone (bez obietnic, E5 jeszcze nie ma): 14 (dzien 118) - zamiast "warbands now haunt the passes /
   ride well armed" ksiazkowy dalszy ciag (straty w ludziach Catelyn, Blackfish przy Krwawej Bramie); 15 (dzien 125) - zamiast "a skilled fighter could win gold"
   Ser Hugh of the Vale (swiezo pasowany giermek Jona Arryna); 19 (dzien 155) - zamiast "travellers should expect armed bands" lordowie dorzecza sla ocalalych
   po sprawiedliwosc do Krolewskiej Przystani.
5. **Prologue.Scenes.cs** (nowy) + Prologue.cs (Reset/Pack/Unpack/OnSessionLaunched) + SubModuleMain (straznik, OnGameEnd) - straznik 4 x/s w
   OnApplicationTick (wlasny licznik 3 bledow ladowania typu, PRZED returnem wiosek). Bramki (2 s z rzedu): MapState i GameStarted, bez rozmowy, bez menu Esc,
   bez zapytania i sceny powiadomienia, bez pauzy silnika i TimeControlModeLock, bez bitwy, PlayerEncounter brak albo w osadzie bez bitwy, bez warstwy
   BannerKingsMapView, bez EventPopupState na stosie; (R1) bez ekranu wczytywania (`LoadingWindow.IsLoadingWindowActive`) i mapa gotowa (`MapScreen.IsReady`). Jedna scena naraz, najnizsza niepokazana z dniem <= dzis. Pomijanie (POMINIETA): spoznienie > 30 dni,
   sceny 1-22 po dniu 188, scena 23 tylko po `_kingFallen` (bez limitu spoznienia) i nie po egzekucji Neda. Zapis: `sc=`/`sk=` w arm_prologue; zapis prologu z
   WGRANIA 101 (bez `sc=`) - sceny z dni < floor(dzis) POMINIETE (powod "zapis prologu sprzed scen (WGRANIE 101)"), bez serii zaleglych. 3 nieudane otwarcia
   tej samej sceny - pominieta.
6. **RotWindowPictures.cs** (nowy) - prefiks na jawna implementacje `IGameStateListener.OnActivate` ekranu `ROT.Screens.EventScreen` (mapa interfejsu,
   odbicie - bez typow ROT): okno rozpoznane po klasie wydarzenia i tytule (tabela 35 okien ROT 8.1.8 w kodzie, z tools/lektor/teksty.json; klasa z jednym
   oknem - po klasie, z kilkoma - po tytule EN albo przetlumaczonym TextObject z tym samym id); plik `Images\ROT\rot_<Klasa>_<n>.png|jpg` -> HeaderImage
   wydarzenia = sprite z pliku (setter protected odbiciem). Postfiks OnDeactivate: oryginalny HeaderImage wraca, obrazek do kolejki zwolnien. Bez pliku -
   bez zmian (raz na sesje linia z nazwa pliku, ktorego okno szuka). Nasze okna (ArmStoryEvent) pomijane. (R2) Okna o tytule "Stannis Sails for King's Landing"
   (BlackwaterSiegeNotificationEvent_0) i "Stannis Claims the Iron Throne" (BlackwaterEvent_1) maja w ROT prefaby z obrazkiem na sztywno (bez @HeaderImage) -
   bez podmiany, linia "prefab Blackwater"; obrazek z pliku dziala w 33 oknach z 35.
7. **Settings.cs + McmSettings (gen_mcm)** - `PrologueScenes` (TAK, grupa "Prologue 298 (new campaign)"), `StoryImagesForRotWindows` (TAK, nowa grupa
   "Story windows"); oba czytane na zywo, nie w SESSION_KEYS. Armoury 2245 ustawien.
8. **tools/obrazki_okien.py** (nowy) - assets/prolog|rot|kronika -> Armoury/Images/Prolog|ROT|Kronika: PIL verify, kadrowanie srodka do 16:9, 1024x576 LANCZOS,
   PNG (albo --jpg q=92), ta sama walidacja bajtow co w grze na wyniku, zapis przez plik tymczasowy, odczyt kontrolny, md5; `--sprawdz` - tylko walidacja
   katalogu wyjscia. Nazwy spoza wzorow pominiete; rot_* spoza tabeli 35 okien - UWAGA; (R2) rot_BlackwaterEvent_1 i rot_BlackwaterSiegeNotificationEvent_0 -
   UWAGA i pominiete (gra ich nie pokaze). Armoury/Images/README.txt - nazwy plikow dla Jeffa.
9. **Skrypty (POZA drzewem, niezacommitowane w repo glownym)** - dopisane petle Armoury\Images (rekursywnie *.png|*.jpg|*.jpeg):
   - `test/bieg-bitwa.ps1` (ta kopia wola autotest; identyczna z repo) i repo `tools/autotest-2026-10-17/bieg-bitwa.ps1` - pliki jako ExtraFile (nowe po tescie znikaja);
   - repo `tools/autotest-2026-10-17/wgraj-v5.ps1` (TEJ wersji uzywa sie do wgran - ma petle arm_*.xml; `test/wgraj-v5.ps1` z 18.10 jej nie ma i nie byl
     zmieniany) - pliki jak inne (kopia .bak i lustro na D:, md5), obrazek bez sygnatury PNG/pelnego JPG = STOP przed podmiana.
   Kontrola: parser PowerShell 0 bledow, fragmenty uruchomione na drzewie testowym (6 obrazkow, plik .txt pominiety, plik z zer - STOP kod 2).
   Kopie sprzed zmiany: scratchpad sesji 7aee5f12 `m103/bak-skrypty/`. Nowych plikow danych galaz nie ma (tabela okien w kodzie).
10. **tools/sprawdz_logi.py** - grupa `m103sceny` (opis w pomocy, dispatch, nowa opcja `--rgl <rgl_log>`); **tools/prolog/test_m103sceny.py** - 13 logow
    syntetycznych (ok, spozniona, nalozenie, powtorka, zapas, brak pary CS, bez przycisku, ERROR, assert tekstury, zapis sprzed scen, wczytanie ok/zle, klasyczna)
    - WYNIK OK; m101prolog na tym samym logu bez wyjatku. (R3) 14 przypadkow: "assert tekstury" z wymyslona linia rgl_log zastapiony linia Armoury
    "ZWOLNIENIE PRZY OKNIE" (SC-13 NIE), reload-ok sprawdza tez SC-2 TAK, nowy "klas-blad" (kampania klasyczna z bledem RotWindowPictures i StoryPictures -
    SC-1 NIE) - WYNIK OK.
11. (R3) **StoryPictures.OnScreen** - przed kazdym zwolnieniem tekstury z kolejki (AppTick): jesli na wierzchu ekranu jest okno wydarzenia ROT
    (ekran ROT.Screens.EventScreen albo warstwa "EventScreen") z tym obrazkiem (Event.HeaderImage == sprite i w slowniku nie ma nowszego obrazka o tej nazwie)
    albo z obrazkiem nieczytelnym - zwolnienie wstrzymane, linia `ZWOLNIENIE PRZY OKNIE` (raz na obrazek), licznik w linii "zwolnione obrazki". Sprawdzenie
    niezalezne od bramki EventWindowOnStack (nazwa typu ekranu, nie stanu); ReleaseAll przy koncu gry bez kontroli.

## Linie logu (Armoury)

- `Prolog (m103): start sesji | klucz Prologue Scenes TAK | sceny pokazane N/23, pominiete M[ (zapis sprzed scen ...)] | nastepna NN (dzien D) | katalog obrazkow <sciezka>[ (brak)] | plikow: Prolog a, ROT b, Kronika c | potkniecia P.`
- `Prolog (m103): scena NN '<tytul>' pokazana | dzien X.XX (nalezna od D) | obrazek arm_prolog_NN (plik prolog_NN.png 1024x576, dekodowanie T ms)` albo `obrazek <sprite ROT> (ROT - brak pliku | zly plik: powod)` `| okno ROT` albo `okno zapas ShowInquiry (powod) | gracz: <osada>, menu <menu>[, w niewoli] | pokazane N/23, pominiete M | T ms.`
- `Prolog (m103): scena NN zamknieta po S s (przycisk | przycisk (Esc) | bez przycisku - ...).`
- `Prolog (m103): POMINIETA scena NN '<tytul>' - <powod> (dzien prologu X.XX).`
- `Prolog (m103): scena NN '<tytul>' czeka: <powod> (dzien prologu X.XX, nalezna od D).` (raz na dzien, przy spoznieniu >= 0.25 dnia)
- `Prolog (m103): scena NN '<tytul>' - okno nie otworzylo sie (proba k/3).`, `Prolog (m103): zapis prologu sprzed scen (bez sc=) - minionych scen pominietych N ...`,
  `Prolog (m103): sceny prologu zakonczone | pokazane N/23, pominiete M ... | okna ROT a, zapas b, potkniecia okna c | potkniecia P.`
- `Obrazki (m103): latka okien ROT: OnActivate TAK, OnDeactivate TAK | okien w tabeli 35 | klucz ... ` (menu glowne), `Obrazki (m103): start sesji | latka okien ROT: ... | katalog obrazkow ... | podmienionych okien ROT w procesie N | wczytanych obrazkow a, odrzuconych b | potkniecia ...`
- `Obrazki (m103): okno ROT <Klasa>_<n> '<tytul>' - obrazek arm_rot_<Klasa>_<n> (plik ...) zamiast <sprite ROT>.` / `... - brak pliku Images\ROT\rot_<Klasa>_<n>.png|jpg, obrazek ROT <sprite>.` / `ZLY PLIK <sciezka> - <powod> ...` / `zwolnione obrazki: N (koniec gry) ...`
- `Okno opowiesci (m103): okno <klucz> zdjete po wyjatku przy otwarciu - pokazuje zapas.`
- (R1) `Prolog (m103): scena NN '<tytul>' czeka: ekran wczytywania` / `czeka: mapa jeszcze nie gotowa (MapScreen.IsReady)` (jak inne "czeka" - raz na dzien
  prologu przy spoznieniu >= 0.25 dnia; przy wczytaniu czas stoi, wiec zwykle bez linii).
- (R2) `Obrazki (m103): okno ROT BlackwaterEvent_1 'Stannis Claims the Iron Throne' - prefab Blackwater ROT (obrazek wpisany w prefab, bez @HeaderImage) - obrazek z
  pliku niemozliwy, zostaje obrazek ROT.` (raz na proces); w linii latki: `okien w tabeli 35 (z obrazkiem z pliku 33, prefab Blackwater bez obrazka 2)`.
- (R3) `Obrazki (m103): ZWOLNIENIE PRZY OKNIE arm_<nazwa> - okno wydarzenia ROT na wierzchu ekranu z tym obrazkiem - zwolnienie wstrzymane (...) | wstrzymanych w
  sesji N.` (blad kolejki obrazkow - prog SC-13); `zwolnione obrazki: N (koniec gry) | ... zwolnionych razem a, wstrzymanych przy oknie b | potkniecia c.`
- Bledy: `ERROR in Prologue.Scenes.*`, `ERROR in StoryWindow.*`, `ERROR in StoryPictures.*`, `ERROR in RotWindowPictures.*` (pierwsze 5 kazdego licznika), `ERROR in Prologue.Scenes: straznik nie laduje sie (proba k/3)`.
- CrashScribe (autotest): `AUTOTEST: OKNO wydarzenie ROT "<tytul sceny>" (...) -> [Continue...]` - para dla kazdej sceny w oknie ROT.

## Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje**: straznik dziala w kazdej kampanii tylko jako Watch (nic, gdy okna nie ma) i kolejka obrazkow (pusta = nic); sceny tylko przy Prologue.Active.
  Latka okien ROT bez pliku nic nie zmienia; HeaderImage nie jest zapisywany i wraca przy zamknieciu. Nowe pole zapisu `sc=/sk=` w istniejacym arm_prologue.
  Klasyczna kampania i bieg z zapisu: zero linii `Prolog (m103)` (poza `Obrazki (m103)`).
- **Kolizje**: galaz m103-lektor latka te same metody EventScreen (postfiks) - Harmony laczy latki; nasze okna to zwykle okna ROT, lektor je widzi (brak nagrania
  = cisza). Scalenie: konflikty tekstowe mozliwe w SubModuleMain (OnApplicationTick, OnBeforeInitialModuleScreenSetAsRoot), Settings.cs, McmSettings (gen_mcm),
  sprawdz_logi (dispatch, pomoc, opcja --rgl) i skryptach (lektor moze dopisywac kopiowanie nagran do tych samych petli) - dopisac oba, McmSettings z gen_mcm.
- **Cudzy kod**: twarda referencja do ROT.dll (EventBase) - po aktualizacji ROT mozliwy TypeLoad/MissingMethod: zostaje w try (NoInlining), okno idzie zapasem
  ShowInquiry; bez ROT skanery GetTypes() na Armoury (Harmony PatchAll lapie ReflectionTypeLoadException, MCM - niesprawdzone) - u Jeffa ROT jest zawsze.
- **Niesprawdzone w grze** (gra zajeta autotestem): dekodowanie PNG przez CreateFromMemory, zwolnienie tekstury z pamieci (RenderTargetComponent moze byc null -
  wyjatek w try, teksture zwolni finalizer), wyglad tekstu (7 linii, zrzuty 03 i 09), powrot menu miasta po zamknieciu okna (MapState.OnActivate), GC przy
  kazdym pchnieciu stanu (Common.MemoryCleanupGC) - obserwowac w biegu 23 okien.
- **Decyzje Claude'a** (do wpisu w PRZEKAZANIE): pomijanie > 30 dni i scen 1-22 po dniu 188; scena 23 bez limitu spoznienia, odpada po egzekucji Neda; przy
  PrologueKings = NIE (Robert martwy od startu) sceny i tak sa pokazywane (teksty o zywym Robercie - niespojnosc ze swiatem); wybor z tabeli projektu (dzien
  75 "T + wybor") nie jest zrobiony - (R4) scena 09 juz go nie obiecuje.
- **Ekran wczytywania (R1)**: bramka czyta `LoadingWindow.IsLoadingWindowActive` (publiczne, silnik) i `MapScreen.Instance.IsReady` (publiczne; to samo czyta
  MapVillagesView). MapScreen.Instance == null nie blokuje (GallowsView/MapFields dowodza, ze w ROT jest) - gdyby ROT kiedys podmienil ekran mapy, zostaje sama
  bramka ekranu wczytywania. Ryzyko odwrotne: IsReady falszywe przez dluzej (np. scena mapy nie gotowa) = scena czeka (linia "czeka"), po 30 dniach POMINIETA.
- **Esc**: autotest klika tylko Done - sciezke Esc (HandleCancelButton == Done) sprawdzic recznie.

## Status

DO SPRAWDZENIA - kod gotowy (z poprawkami po recenzji R1-R5), build 5 modow 0, test progow na logach syntetycznych 14/14 OK. NIE wgrane, NIE w autotescie
(w tle trwa inny autotest).

## Test w grze krok po kroku (autotest, wzor testy-101)

1. DLL test/n103 z tej galezi (CrashScribe bez zmian - CrashScribe-at-n101.dll). Drzewo z jednym prawdziwym PNG: `python -I tools/obrazki_okien.py --src <repo>/assets`
   (albo plik testowy) -> Armoury/Images/Prolog/prolog_01.png; skrypty kopiuja katalog Images same.
2. 103-prolog: 230 dob z flaga prologu (armoury-prolog.flag + armoury-start-zloto.flag), zdjecia okien scen 01 (wlasny PNG; zrzut ma pokazac okno, nie ekran
   wczytywania), 03 i 09 (najdluzsze teksty: 09 = 7 linii).
3. 103-prolog-zapis: `-LoadSave autotest-dlugi -Days 20` (zapis kontrolny doby 180) - sceny 22-23 bez powtorek (SC-12). Zapis z WGRANIA 101 (bez sc=) - sceny
   01-21 jako POMINIETE "zapis prologu sprzed scen" (SC-7 TAK, INFO); zapis z maska sc= - zalatwione z linii startu (SC-2 liczy je od R3).
4. (R1) 103-prolog-wczyt: wczytanie zapisu, w ktorym scena jest nalezna OD RAZU (zapis prologu zrobiony w dniu sceny przed jej pokazaniem, np. autozapis z
   biegu 103-prolog z doby 48.x przed scena 06, albo zapis 101 z doby 182.x - scena 22) - linia `scena NN ... pokazana` dopiero po zniknieciu ekranu
   wczytywania (czas linii po `start sesji` + ok. 2 s od gotowej mapy), zrzut tej sceny pokazuje okno, nie ekran ladowania.
5. 103-bitwa i (R9, Jeff 21.10: "prolog to nowa kampania, po co rozbijac") 103-160: 160 dob z zapisu kampanii KLASYCZNEJ (bez flagi prologu) - zero linii
   `Prolog (m103)`, latka obrazkow okien ROT (linie `Obrazki (m103): okno ROT ...`), `--grupa m103sceny` z SC-1 i SC-13 (licza takze w kampanii klasycznej od R3).
6. Progi: `python -I tools/sprawdz_logi.py --grupa m103sceny <Armoury-*.log> --crashscribe <session-*.log>` (`--rgl` tylko dodatkowo - assert gry nie trafia
   do logow w grze wydaniowej):
   - 103-prolog: SC-0..SC-8, SC-10, SC-11, SC-13 TAK; SC-12 INFO; SC-9 INFO "z pliku 1 z 23";
   - 103-prolog-zapis: SC-0..SC-8, SC-10..SC-13 TAK (SC-2 z zalatwionymi z zapisu albo z POMINIETYMI "zapis sprzed scen", SC-7 - te ostatnie INFO); SC-9 INFO
     "z pliku 0 z 2" (prolog_01 nie nalezy do tych scen);
   - 103-prolog-wczyt: jak zapis + kolejnosc linii z punktu 4;
   - 103-bitwa, 103-160: SC-0 INFO, SC-1 TAK, SC-13 TAK, SC-14 INFO.
   `--grupa m101prolog` bez zmian (11/11), log Armoury: linia `obrazek arm_prolog_01 (plik prolog_01.png 1024x576, dekodowanie ...)`, brak `ZLY PLIK`,
   `ZWOLNIENIE PRZY OKNIE` i `ERROR in Story*`.
7. W grze Jeffa: scena dnia 0 po oknie Banner Kings (nie pod nim), okno nad menu czekania w miescie i powrot menu, Esc zamyka scene i nastepna przychodzi.

## Poprawki po recenzji (21.10, R1-R5; build 5 modow 0, gen_mcm bez zmian, test syntetyczny 14/14 OK)

Wszystkie 9 uwag sprawdzone w kodzie i dekompilacji - prawdziwe, poprawione (zadnej nie odrzucilem):

1. **Scena pod ekranem wczytywania (wazne) - PRAWDZIWA, R1.** Dekompilacja SandBox.View MapScreen: `HandleIfSceneIsReady` wlacza widok sceny tylko przy
   `ScreenManager.TopScreen == this`, a `HandleIfBlockerStatesDisabled` zdejmuje ekran wczytywania (`LoadingWindow.DisableGlobalLoadingWindow`) tylko przy
   wlaczonym widoku; ROT EventScreen.OnActivate ekranu wczytywania nie zdejmuje. Bramka: `LoadingWindow.IsLoadingWindowActive` -> "ekran wczytywania",
   `MapScreen.Instance != null && !IsReady` -> "mapa jeszcze nie gotowa"; 2 s spokoju licza sie od gotowej mapy. Plan testu: punkt 4 (103-prolog-wczyt).
2. **Okna Blackwater (wazne) - PRAWDZIWA, R2.** ROT EventScreen.OnActivate: tytul "Stannis Sails for King's Landing" -> prefab BlackwaterEventWarning
   (Sprite="StannisSetsSail", linia 14), "Stannis Claims the Iron Throne" -> BlackwaterEventStannisWin (Sprite="StannisIronThrone", linia 11) - bez
   @HeaderImage (EventXml ma go w linii 18). RotWindowPictures przy tych tytulach (to samo porownanie co ROT: EventPanel.EventTitle == Event.Title) nic nie
   dekoduje, pisze "prefab Blackwater"; obrazki_okien.py pomija oba pliki z UWAGA; README i opis: 33 okna z obrazkiem, 2 bez.
3. **SC-13 niewykrywalny (wazne) - PRAWDZIWA, R3.** Napis jest tylko w TaleWorlds.Engine.GauntletUI (TwoDimensionEnginePlatform.DrawImage -> Debug.FailedAssert ->
   DebugManager.Assert; MBDebugManager.Assert ma puste cialo; ButterLib DebugManagerWrapper pisze LogDebug). Do tego po naszym `Release` EngineTexture.Texture =
   null i DrawImage wychodzi PRZED assertem (pusta ramka, bez napisu). Zrobione: StoryPictures.OnScreen przed kazdym zwolnieniem (punkt 11 Zmiany), SC-13 liczy
   linie `ZWOLNIENIE PRZY OKNIE` (assert z --rgl/CrashScribe tylko dodatkowo); test z wymyslona linia rgl_log usuniety; plan testu bez "rgl_log: brak ...".
4. **Stary format obrazka wygrywa po wgraniu (drobne) - PRAWDZIWA, R5.** `tools/autotest-2026-10-17/wgraj-v5.ps1` (repo glowne, niezacommitowany): dla kazdego
   obrazka z drzewa plik tego okna w innym formacie, ktory lezy w grze, a nie ma go w drzewie, idzie do `.bak-<Tag>-przed` + lustro na D:, wiersz "stary format
   usuniety" (przy -DryRun "NA SUCHO"); kontrola: brak pliku po przeniesieniu i md5 kopii. `bieg-bitwa.ps1` (repo) - linia UWAGA. Parser PowerShell 0 bledow;
   blok uruchomiony na drzewie probnym (prolog_05.jpg w drzewie + prolog_05.png w "grze" -> .bak i lustro; prolog_06.png bez zmian). Kopie sprzed zmiany:
   scratchpad sesji 7aee5f12 `rv2/bak-skrypty/`. Kopii `test/bieg-bitwa.ps1` (scratchpad 6ba92574) NIE ruszalem - ma juz dopisany blok lektora (galaz
   m103-lektor); blok UWAGA przeniesc przy scaleniu (to tylko komunikat).
5. **SC-2 na biegu z zapisu (wazne) - PRAWDZIWA, R3.** Sceny z numerem <= pokazane + pominiete z linii startu sesji (maska sc=/sk= jest zawsze ciagla od 01 -
   NextScene zalatwia sceny po kolei) licza sie jako zalatwione. Test reload-ok: SC-2 TAK. Progi rozpisane osobno dla biegow (punkt 6 planu).
6. **Kampania klasyczna bez progu bledow (wazne) - PRAWDZIWA, R3.** Przy logu bez `Prolog (m103)` grupa daje teraz SC-1 (ERROR = 0) i SC-13; przypadek
   "klas-blad" (ERROR in RotWindowPictures.ActivatePrefix + StoryPictures.AppTick) -> SC-1 NIE.
7. **Sceny 07, 09, 20 obiecuja E5 i wybor (wazne) - PRAWDZIWA, R4.** 07: zamiast "the Kingsroad will be crowded with the king's men" - Ned obiecuje Jonowi
   przy rozstaniu rozmowe o jego matce; 09: zamiast "If you ride with the king's column..." - Sansa wini siostre za smierc Lady, Arya nie wybaczy Ogarowi
   chlopca rzeznika; 20: zamiast "Beric's riders now hunt the raiders" - "Lord Tywin does not come." JSON + gen_sceny_data.py, linie.py: 09 = 7 linii (U4 L3),
   07 = 6, 20 = 5.
8. **Scena 03 - Robert "In King's Landing" w dniu 12 (drobne) - PRAWDZIWA, R4.** Teraz "Robert Baratheon never forgets a Targaryen." (03 = 6 linii).
9. **Plan testu z biegiem 103-nowa (drobne) - PRAWDZIWA (pamiec jeff-kronika-cala-fabula-lektor-david: "prolog to nowa kampania, po co rozbijac").** Zamiast
   103-nowa - 103-160 z zapisu kampanii klasycznej (punkt 5 planu), z SC-1/SC-13.

Lektor (galaz m103-lektor): tytuly scen bez zmian (klucz nagran = EventTitle); jesli lektor nagra sceny prologu, brac teksty z `tools/prolog/prolog298_sceny.json`
po R4 (zmienione dolne akapity scen 03, 07, 09, 20). Kronika Westeros (Jeff: cala ksiazka, potem serial po koncu ksiazek) - dalej osobny etap na tych samych
klasach (StoryWindow, StoryPictures, katalog Images\Kronika).

## Dla Jeffa

Prolog 298 opowiada teraz 23 sceny z pierwszego tomu (od smierci Jona Arryna do aresztu Neda) w takich samych oknach jak wydarzenia ROT - kazda w swoim dniu,
gdy jestes na mapie albo w miescie (nie w bitwie ani w rozmowie). Twoje obrazki: wrzuc prolog_01.png ... prolog_23.png do assets/prolog, uruchom
`python -I tools/obrazki_okien.py` - narzedzie sprawdzi je, przytnie do 1024x576 i wlozy do Armoury/Images/Prolog; po wgraniu gra bierze je sama, bez
przebudowy. Tak samo obrazki do okien ROT: assets/rot/rot_<Wydarzenie>_<n>.png (np. rot_NedEvent_0.png) - log Armoury podaje nazwe pliku dla kazdego okna.
Dwa okna ROT ("Stannis Sails for King's Landing", "Stannis Claims the Iron Throne") maja obrazek wbudowany na stale - do nich obrazkow nie rob (33 okna z 35).
Kronika Westeros (cala ksiazka, potem serial) pojdzie na tych samych oknach i obrazkach - osobny etap.
