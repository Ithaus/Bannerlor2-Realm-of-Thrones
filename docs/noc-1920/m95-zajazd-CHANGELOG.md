## m95-zajazd (20.10) - w zajezdzie druzyna znika z mapy, w menu slychac karczme (Armoury)

**Dla Jeffa (prosto).** Gdy wchodzisz do zajazdu przy drodze, Twoja druzyna znika z mapy tak jak przy wejsciu do wioski: nie ma jezdzca, konia ani
napisu z Twoim imieniem nad zajazdem (partie armii, ktora prowadzisz, tez znikaja). W menu zajazdu slychac gwar karczmy - ten sam dzwiek, ktory gra
daje w dzielnicy karczmy w miescie - i skrzypniecie drzwi przy wejsciu; gwar trwa tez, gdy spisz w zajezdzie. Gdy klikniesz "Ride on", obudzisz sie
albo wyciagnie Cie bitwa, druzyna od razu wraca na mape. Dwa wylaczniki w MCM (grupa "A night's rest"): "Roadside Inn Hide Party" (znikanie) i
"Roadside Inn Tavern Sound" (dzwiek) - wylaczone = zajazd jak dotad.

**Problem.** Jeff 20.10: "Jak wchodze do zajazdu to powinienem znikac z mapy tak jak sie wchodzi do wioski i powinny byc odglosy karczmy, ze jestem
w karczmie." Dowod z kodu (WGRANIE 94, `RoadsideInns.cs`): zajazd nie jest osada gry. Wejscie (klik na budynek albo pytanie "Go in" przy postoju) robi
tylko postoj partii i `GameMenu.ActivateGameMenu("arm_roadinn")` - `MobileParty.MainParty.CurrentSettlement` zostaje null, wiec gra rysuje druzyne
dalej obok budynku (a przy snie - namiot NightRest). `MenuInit` ustawia tylko obrazek wsi okregu (`wait_mesh` wsi), zadnego dzwieku tla.

**Przyczyna (jak gra chowa druzyne w wiosce - dekompilacja 1.4.8).** `EnterSettlementAction` ustawia `CurrentSettlement`; wizerunek
(`SandBox.View.Map.Visuals.MobilePartyVisual.RefreshPartyIcon`) przy `CurrentSettlement != null` stawia tylko choragiew przy osadzie (bez jezdzca i konia),
a napis gracza zdejmuje `PartyNameplatesVM.UpdateMobilePartyVisibility` (galaz `CurrentSettlement != null`: `PlayerNameplate.Clear(); PlayerNameplate = null`).
Dzwiek: menu osad wolaja `MenuContext.SetAmbientSound(...)` w init (`PlayerTownVisitCampaignBehavior`): wioska `.../2d/village`, miasto `.../2d/city`,
dzielnica karczmy (`town_backstreet`) `event:/map/ambient/node/settlements/2d/tavern` + `SetPanelSound("event:/ui/panels/panel_settlement_enter_tavern")`.
`MenuViewContext` gra tlo, dopoki menu jest otwarte, i wylacza je przy zamknieciu.

**Zmiana.** Nowy plik `Armoury/src/RoadsideInnStay.cs` + male wpiecia:
- UKRYCIE (klucz `RoadsideInnHideParty`): wejscie do zajazdu do osady gry odpada (menu wioski, pozycja przy wsi, AI). Uzyta droga mgly wojny gry:
  `MobileParty.MainParty.IsVisible = false` RAZ przy wejsciu (`MenuInit` zajazdu) i przy snie w zajezdzie (`TownInns.RoadSleepInit`). Wizerunek gasnie sam
  (`MobilePartyVisual.TickFadingState`, jak partie poza zasiegiem wzroku; namiot snu to dziecko wizerunku), kroki koni milkna (MapScreen gra je tylko
  widocznym partiom), partie armii gracza ida za widocznoscia wodza (`PartyBase.CalculateVisibilityAndInspected`, przeliczone raz przy zmianie stanu).
  Gra liczy widocznosc druzyny gracza od nowa co tick, gdy czas biegnie (`CampaignTickCacheDataStore.UpdateVisibilitiesAroundMainParty` ->
  `PartyBase.UpdateVisibilityAndInspected`) - **prefiks Harmony** na tej metodzie (`AccessTools.DeclaredMethod` na klasie deklarujacej `PartyBase`,
  sprawdzony `Harmony.GetPatchInfo`) przytrzymuje `false` tylko dla druzyny gracza i tylko w stanie "w zajezdzie"; inne partie i druzyna poza zajazdem -
  metoda gry bez zmian (sam test jednego pola bool). Napis z imieniem: **prefiks** na `SandBox.ViewModelCollection.Nameplate.PartyNameplatesVM
  .UpdateMobilePartyVisibility` (typ przez `AccessTools.TypeByName`, bez nowej referencji) robi to, co gra przy wejsciu do osady; po wyjsciu gra stawia
  napis sama (ta sama metoda, druzyna znow widoczna - `CreateNameplateFor`).
- WYJSCIE: "Ride on" - od razu (`RoadsideInnStay.Leave` przed `ExitToLast`); reszta - **straznik** 4 razy na sekunde z `SubModuleMain.OnApplicationTick`
  (dziala takze przy zatrzymanym czasie, tylko gdy aktywny jest stan mapy). Stan "w zajezdzie" = menu `arm_roadinn` przy zajezdzie (`RoadsideInns.AtInn`)
  albo sen oplacony w tym zajezdzie (`arm_sleep_wait` + `TownInns.RoadSleepUid`), druzyna aktywna, poza osada, bitwa, spotkaniem, oblezeniem, morzem,
  niewola i sluzba ROT. Inaczej (pobudka, "Rouse the men early", bitwa/napad, spotkanie, niewola, klucz wylaczony w trakcie, menu zamkniete) - druzyna
  wraca: widocznosc liczy gra (`UpdateVisibilityAndInspected` dla druzyny i jej armii). Wczytanie zapisu w zajezdzie: stan tylko w pamieci, straznik albo
  init menu chowa druzyne od nowa. Nic nie wola `SwitchToMenu` (pulapka menu oczekiwania), nic nie dotyka wizerunkow co klatke - tylko pole `IsVisible`
  przy zmianie stanu, reszte robi gra.
- DZWIEK (klucz `RoadsideInnTavernSound`): w `MenuInit` zajazdu `SetAmbientSound("event:/map/ambient/node/settlements/2d/tavern")` i
  `SetPanelSound("event:/ui/panels/panel_settlement_enter_tavern")` - dokladnie to, co gra daje w dzielnicy karczmy miasta (oba zdarzenia sa w
  `Modules/Native/ModuleData/sound_event_data.gen.xml`, linie 786 i 12676). Sen przechodzi do `arm_sleep_wait` w tym samym menu - gwar gra dalej; po
  wczytaniu w trakcie snu tlo ustawia `RoadSleepInit` (bez drzwi). Nie ustawiamy drugi raz, gdy juz gra (petla nie startuje od nowa).
- TLO OBRAZKA: gra nie ma obrazka karczmy (`SetBackgroundMeshName` w grze i SandBox: tylko `encounter_*`, `wait_*`, `town_blockade`; dzielnica karczmy
  w miescie zostaje przy `wait_mesh` miasta). Zajazd zostaje przy `wait_mesh` wsi okregu, jak dotad - bez zmiany.
- LOG (prefiks `Zajazdy (ZJ): pobyt - `, nie myli sie z liniami grupy nzajazdy w `tools/sprawdz_logi.py`): `start sesji | klucze ... | latka widocznosci:
  wpieta TaleWorlds.CampaignSystem.Party.PartyBase.UpdateVisibilityAndInspected (prefiks) | latka napisu gracza: wpieta ... (prefiks) | ...`;
  `ukrycie partii gracza w <zajazd> (<uid>) - wejscie (menu zajazdu) / sen w zajezdzie / straznik (...)`; `partia gracza znow na mapie (<zajazd>) -
  powod: Ride on / menu zamkniete / bitwa / spotkanie / klucz wylaczony ...`; `dzwiek karczmy w <zajazd>: tlo ..., wejscie ...`; `dzien N | ukrycia,
  powroty, przytrzymania (tick gry), ponowne ukrycia, napis gracza zdjety, dzwiek karczmy | teraz schowana | latki | potkniecia`. Bledy jako
  `ERROR in RoadsideInns.Stay.*` - licza sie w progu ZJ-8 grupy nzajazdy.
- Wpiecia: `RoadsideInns.cs` (Reset, MenuInit, "Ride on", SessionStart, Daily), `TownInns.Road.cs` (`RoadSleepUid`, wywolanie w `RoadSleepInit`),
  `SubModuleMain.cs` (straznik w `OnApplicationTick`, licznik bledu ladowania typu po 3 - gra bez zmian), `Settings.cs` + `McmSettings.cs` (gen_mcm).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Kto czyta `IsVisible` druzyny gracza (grep po dekompilacji): AI gry go nie czyta (`TaleWorlds.CampaignSystem` - tylko wlasny cel gracza
  `MobilePartyAi:433`, odswiezenia wizerunkow przy wojnie/pokoju), SandBox (kroki koni, licznik partii w debugu), BK (odswiezenie wizerunkow, statki),
  ROT (sam chowa druzyne tak w sluzbie - przy sluzbie my nie chowamy). Wrogowie dalej moga napasc druzyne spiaca w zajezdzie (spotkania nie zaleza
  od widocznosci) - przy bitwie prefiks puszcza metode gry, straznik zdejmuje stan.
- Wizerunek: zmiana `IsVisible` = jedno `SetVisualAsDirty` przy wejsciu i wyjsciu (jak wyjscie partii z mgly). Namiot snu (`NightRest.Tent`) stoi pod
  ukrytym wizerunkiem - schowany z nim (tak gra chowa namioty oblegajacych we mgle); ZOBACZYC w grze, czy namiot przy snie w zajezdzie nie jest widoczny.
  Pobudka: `Tent(false)` pokazuje figurki (alfa 0 z gasniecia), straznik po <= 0.25 s przywraca widocznosc - mozliwe krotkie mrugniecie, do oceny na oko.
- Zapis: `MobileParty._isVisible` jest w zapisie gry. Zapis w zajezdzie = `false` w pliku; gra liczy widocznosc od nowa przy wczytaniu i przy kazdym ticku
  czasu (takze stary DLL i klucz wylaczony - druzyna wraca). Nowych danych w zapisie brak.
- Koszt: prefiks widocznosci - jedno pole bool dla kazdej partii w zasiegu (gdy nic nie schowane), w zajezdzie test jednej partii; straznik - przy
  wylaczonym kluczu i niczym schowanym nic, inaczej 4 razy na sekunde odczyt id menu (w zajezdzie plus sprawdzenie sluzby ROT przez odbicie).
  Male metody pod Harmony (zawieszenie GC 19.10): latamy dwie duze metody gry, nie male.
- Kolizje: `w-toku/m95-nocleg` (943cc411) zmienia `Settings.cs`, `McmSettings.cs`, `RoadsideInns.cs`, `TownInns.Road.cs` - sprawdzone `git merge-tree`
  c4ddeba9 x 943cc411: bez konfliktow (drzewo 3eb0f53c; McmSettings.cs po scaleniu i tak od nowa z gen_mcm). `w-toku/m95-menu` bez commitow
  (= baza e35e6f99). Uruchomione tylko budowanie (kod 0), bez gry i autotestu (zakaz w tej pracy).

**Status.** DO SPRAWDZENIA (zbudowane, kod 0; bez autotestu - Jeff gra).

**Test w grze (krok po kroku dla Jeffa).**
1. Podjedz do zajazdu przy drodze i kliknij go (albo stan obok i wybierz "Go in"). Gdy otworzy sie menu zajazdu: slychac skrzypniecie drzwi i gwar karczmy;
   na mapie wokol menu Twojej druzyny (jezdziec, kon, napis z imieniem) przy zajezdzie juz nie ma.
2. Kliknij "Ride on" - druzyna od razu wraca na mape obok zajazdu, gwar cichnie.
3. Wejdz jeszcze raz i wybierz nocleg (np. "Common room"). Podczas snu: gwar dalej gra, druzyny (ani namiotu) na mapie nie widac.
4. Po pobudce (albo "Rouse the men early") druzyna wraca na mape.
5. Zapisz gre w menu zajazdu i wczytaj - druzyna dalej schowana, menu zajazdu dziala; "Ride on" przywraca druzyne.
6. Wylacz w MCM "Roadside Inn Hide Party" i "Roadside Inn Tavern Sound" - zajazd jak dotad (druzyna widoczna obok, cisza).
7. W logu Armoury szukaj `Zajazdy (ZJ): pobyt - ` : linia startu ma "latka widocznosci: wpieta" i "latka napisu gracza: wpieta"; przy kazdym wejsciu
   "ukrycie partii gracza", przy wyjsciu "partia gracza znow na mapie - powod: ...", "dzwiek karczmy w ..."; brak `ERROR in RoadsideInns.Stay`.

**Decyzje (projektowal Claude; wylaczniki: `RoadsideInnHideParty`, `RoadsideInnTavernSound`).**
- Dwa osobne klucze: ukrycie dotyka widocznosci druzyny (latka Harmony), dzwiek tylko menu - Jeff moze wylaczyc jedno bez drugiego.
- Dzwiek karczmy z gry (tawerna miasta), nie wlasny plik: istnieje, gra go zna, dziala tak jak w miescie.
- Obrazek menu bez zmian (gra nie ma obrazka karczmy, karczma w miescie tez pokazuje obrazek osady).
- Gdy latka widocznosci by nie weszla - druzyny nie chowamy wcale (bez latki gra pokazalaby ja przy pierwszym ticku czasu i wizerunek by mrugal);
  linia startu mowi, dlaczego. Gdy latka napisu nie wejdzie albo potknie sie 3 razy z rzedu - napis zostaje jak dotad, ukrycie dziala dalej.
