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
| AT | autotest prologu 230 dob + zapis + bitwa + nowa (wzor testy-101) | DO ZROBIENIA (gra zajeta autotestem w tle - nie uruchamialem) |

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
   BannerKingsMapView, bez EventPopupState na stosie. Jedna scena naraz, najnizsza niepokazana z dniem <= dzis. Pomijanie (POMINIETA): spoznienie > 30 dni,
   sceny 1-22 po dniu 188, scena 23 tylko po `_kingFallen` (bez limitu spoznienia) i nie po egzekucji Neda. Zapis: `sc=`/`sk=` w arm_prologue; zapis prologu z
   WGRANIA 101 (bez `sc=`) - sceny z dni < floor(dzis) POMINIETE (powod "zapis prologu sprzed scen (WGRANIE 101)"), bez serii zaleglych. 3 nieudane otwarcia
   tej samej sceny - pominieta.
6. **RotWindowPictures.cs** (nowy) - prefiks na jawna implementacje `IGameStateListener.OnActivate` ekranu `ROT.Screens.EventScreen` (mapa interfejsu,
   odbicie - bez typow ROT): okno rozpoznane po klasie wydarzenia i tytule (tabela 35 okien ROT 8.1.8 w kodzie, z tools/lektor/teksty.json; klasa z jednym
   oknem - po klasie, z kilkoma - po tytule EN albo przetlumaczonym TextObject z tym samym id); plik `Images\ROT\rot_<Klasa>_<n>.png|jpg` -> HeaderImage
   wydarzenia = sprite z pliku (setter protected odbiciem). Postfiks OnDeactivate: oryginalny HeaderImage wraca, obrazek do kolejki zwolnien. Bez pliku -
   bez zmian (raz na sesje linia z nazwa pliku, ktorego okno szuka). Nasze okna (ArmStoryEvent) pomijane.
7. **Settings.cs + McmSettings (gen_mcm)** - `PrologueScenes` (TAK, grupa "Prologue 298 (new campaign)"), `StoryImagesForRotWindows` (TAK, nowa grupa
   "Story windows"); oba czytane na zywo, nie w SESSION_KEYS. Armoury 2245 ustawien.
8. **tools/obrazki_okien.py** (nowy) - assets/prolog|rot|kronika -> Armoury/Images/Prolog|ROT|Kronika: PIL verify, kadrowanie srodka do 16:9, 1024x576 LANCZOS,
   PNG (albo --jpg q=92), ta sama walidacja bajtow co w grze na wyniku, zapis przez plik tymczasowy, odczyt kontrolny, md5; `--sprawdz` - tylko walidacja
   katalogu wyjscia. Nazwy spoza wzorow pominiete; rot_* spoza tabeli 35 okien - UWAGA. Armoury/Images/README.txt - nazwy plikow dla Jeffa.
9. **Skrypty (POZA drzewem, niezacommitowane w repo glownym)** - dopisane petle Armoury\Images (rekursywnie *.png|*.jpg|*.jpeg):
   - `test/bieg-bitwa.ps1` (ta kopia wola autotest; identyczna z repo) i repo `tools/autotest-2026-10-17/bieg-bitwa.ps1` - pliki jako ExtraFile (nowe po tescie znikaja);
   - repo `tools/autotest-2026-10-17/wgraj-v5.ps1` (TEJ wersji uzywa sie do wgran - ma petle arm_*.xml; `test/wgraj-v5.ps1` z 18.10 jej nie ma i nie byl
     zmieniany) - pliki jak inne (kopia .bak i lustro na D:, md5), obrazek bez sygnatury PNG/pelnego JPG = STOP przed podmiana.
   Kontrola: parser PowerShell 0 bledow, fragmenty uruchomione na drzewie testowym (6 obrazkow, plik .txt pominiety, plik z zer - STOP kod 2).
   Kopie sprzed zmiany: scratchpad sesji 7aee5f12 `m103/bak-skrypty/`. Nowych plikow danych galaz nie ma (tabela okien w kodzie).
10. **tools/sprawdz_logi.py** - grupa `m103sceny` (opis w pomocy, dispatch, nowa opcja `--rgl <rgl_log>`); **tools/prolog/test_m103sceny.py** - 13 logow
    syntetycznych (ok, spozniona, nalozenie, powtorka, zapas, brak pary CS, bez przycisku, ERROR, assert tekstury, zapis sprzed scen, wczytanie ok/zle, klasyczna)
    - WYNIK OK; m101prolog na tym samym logu bez wyjatku.

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
  PrologueKings = NIE (Robert martwy od startu) sceny i tak sa pokazywane (teksty o zywym Robercie - niespojnosc ze swiatem); scena 09 dalej mowi graczowi
  "If you ride with the king's column, expect to be asked whose side you are on" - wybor z tabeli projektu (dzien 75 "T + wybor") nie jest zrobiony.
- **Esc**: autotest klika tylko Done - sciezke Esc (HandleCancelButton == Done) sprawdzic recznie.

## Status

DO SPRAWDZENIA - kod gotowy, build 5 modow 0, test progow na logach syntetycznych OK. NIE wgrane, NIE w autotescie (w tle trwa inny autotest).

## Test w grze krok po kroku (autotest, wzor testy-101)

1. DLL test/n103 z tej galezi (CrashScribe bez zmian - CrashScribe-at-n101.dll). Drzewo z jednym prawdziwym PNG: `python -I tools/obrazki_okien.py --src <repo>/assets`
   (albo plik testowy) -> Armoury/Images/Prolog/prolog_01.png; skrypty kopiuja katalog Images same.
2. 103-prolog: 230 dob z flaga prologu (armoury-prolog.flag + armoury-start-zloto.flag), zdjecia okien scen 01 (wlasny PNG), 03 i 09 (najdluzsze teksty).
3. 103-prolog-zapis: `-LoadSave autotest-dlugi -Days 20` (zapis kontrolny doby 180) - sceny 22-23 bez powtorek (SC-12).
4. 103-bitwa i 103-nowa (klasyczny start - zero linii scen).
5. Progi: `python -I tools/sprawdz_logi.py --grupa m103sceny <Armoury-*.log> --crashscribe <session-*.log> --rgl <rgl_log_*.txt>` (oczekiwane: SC-0..SC-8,
   SC-10, SC-11, SC-13 TAK; SC-9 INFO "z pliku 1 z 23"), `--grupa m101prolog` bez zmian (11/11), log Armoury: linia `obrazek arm_prolog_01 (plik prolog_01.png
   1024x576, dekodowanie ...)`, brak `ZLY PLIK` i `ERROR in Story*`; rgl_log: brak "Trying to render a released texture".
6. W grze Jeffa: scena dnia 0 po oknie Banner Kings (nie pod nim), okno nad menu czekania w miescie i powrot menu, Esc zamyka scene i nastepna przychodzi.

## Dla Jeffa

Prolog 298 opowiada teraz 23 sceny z pierwszego tomu (od smierci Jona Arryna do aresztu Neda) w takich samych oknach jak wydarzenia ROT - kazda w swoim dniu,
gdy jestes na mapie albo w miescie (nie w bitwie ani w rozmowie). Twoje obrazki: wrzuc prolog_01.png ... prolog_23.png do assets/prolog, uruchom
`python -I tools/obrazki_okien.py` - narzedzie sprawdzi je, przytnie do 1024x576 i wlozy do Armoury/Images/Prolog; po wgraniu gra bierze je sama, bez
przebudowy. Tak samo obrazki do okien ROT: assets/rot/rot_<Wydarzenie>_<n>.png (np. rot_NedEvent_0.png) - log Armoury podaje nazwe pliku dla kazdego okna.
Kronika Westeros (cala ksiazka, potem serial) pojdzie na tych samych oknach i obrazkach - osobny etap.
