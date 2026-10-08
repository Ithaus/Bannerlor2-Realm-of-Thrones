# WIOSKI NA MAPIE - LORE I IMMERSJA: BADANIE C - TECHNIKA (pkt 1 herb w dymku, 2 proporczyk, 8 menu okregu, 7 pamiec wojny)

**Decyzja Jeffa (08.10):** "bierzemy wszystkie 8 do wiosek". To badanie mowi, JAK zrobic w kodzie pkt 1, 2, 7 i 8 (dane lore
pkt 3-6 to badania A/B). **Stan:** tylko projekt i szkice. Gra, repo i worktree W2 tylko czytane; nic nie zbudowane w Armoury,
nic nie wgrane, nic nie zacommitowane. Szkice skompilowane osobno na DLL gry 1.4.8 (rozdz. 9).

Oznaczenia: SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`;
dec = `SCR\dzien-6\autotest\dec` (dekompilacja 1.4.8); TCS = `SCR\ore-supply\cs` (TaleWorlds.CampaignSystem);
W2 = `SCR\dzien-6\wioski-2200\repo\Armoury\src\MapVillagesView.cs` (galaz w2-wioski-2200); szkice = `SCR\dzien-6\wioski-lore\szkice`.
[KOD] = sprawdzone w dekompilacji; [ZMIERZONE] = policzone na pliku danych / proba poza gra; [AUTOTEST] = do sprawdzenia w grze.

---

## 0. W skrocie

| Pkt | Jak | Nowe w modzie | Zapis | Wymaga |
|---|---|---|---|---|
| 1 dymek z panem i herbem | wlasny typ dymka gry (`InformationManager.RegisterTooltip<MapVillageTip, MapVillageTooltipVM>`) + wlasny prefab = kopia `PropertyBasedTooltip.xml` ROT z herbem (`ImageIdentifierWidget` + `BannerImageIdentifierVM`); Alt = wiecej (czym zyje, wiara, historia, pamiec wojny); tlo dymka jak przy osadach gry (sojusznik / wrog) | `MapVillageTooltip.cs`, `VillageLord.cs`, **NOWY katalog** `Modules\Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml` | nic | W2 |
| 2 proporczyk | ta sama choragiew co partie w zamku i namioty: `MobilePartyVisual.GetBannerOfCharacter(banner, "campaign_flag")` (public static, SandBox.View - juz w referencjach W2); osobna encja przy przednim narozniku obrysu; tylko blisko kamery (z <= 90, najblizsze 80); herb sprawdzany raz na sekunde po KODZIE herbu | `MapVillagePennants.cs` | nic | W2 |
| 8 menu okregu | opcja "Look over the district" w `village` i `village_looted` (zwykle menu, nie oczekiwania) -> okienko wyboru gry (`ShowMultiSelectionInquiry`) z herbem w kazdym wierszu i dymkiem wiersza -> "Hear more" = okienko z historia; **zero SwitchToMenu** | `DistrictMenu.cs` | nic | W2 (stan z gry), W3 (stan z ksiegi) |
| 7 pamiec wojny | `CampaignEvents.VillageStateChanged` (sprawca, dzien) + wpiecie w koniec rabunku 113 (uciekli, zabici); ostatnie zdarzenie okregu + licznik spalen | `WarMemory.cs` + 2 pola i 2 linie w `Devastation.cs` (113) | sekcje `m:` w kluczu `arm_district` (typowo 2-5 KB) | 113 dla liczb; bez 113 tylko "kto i kiedy" |

Najwazniejsze ustalenia [KOD]:
1. Zwykly dymek gry NIE umie obrazka: `TooltipProperty` ma tylko tekst, kolor, wysokosc i flagi (TooltipProperty.cs:28-40), prefab
   `PropertyBasedTooltip.xml` (Native i ROT) ma same `RichTextWidget`. `typeof(Clan)` / `typeof(Settlement)` tez ida przez ten prefab -
   herb w dymku = tylko wlasny prefab.
2. Pulapka delegata: dymek wlasnego VM pochodnego od `PropertyBasedTooltipVM` dziala TYLKO z delegatem typu
   `Action<PropertyBasedTooltipVM, object[]>` - inaczej pusty dymek bez bledu (rozdz. 1.2).
3. `Banner` nie ma `Equals` - pamiec materialow herbu na mapie (`MapScreen.CharacterBannerMaterialCache`, MapScreen.cs:125) liczy po
   REFERENCJI, a kolory herbu zmieniaja sie w miejscu (Clan.cs:1377-1403). Do proporczykow: jeden obiekt `Banner` na KOD herbu.
4. `MapScreen.ClearGPUMemory` (przy kazdym wejsciu w misje, MissionCampaignView.cs:28) czysci tekstury herbow - po powrocie z bitwy
   proporczyki trzeba zbudowac od nowa z nowym obiektem `Banner` (gra robi tak samo z partiami).
5. Gra pamieta sprawce rabunku (`VillageStateChangedLogEntry.RaidLeader`), ale wpis ginie po 7 dniach (LogEntry.cs:18) - pamiec
   wojny musi miec wlasny zapis. Najmniejszy: ostatnie zdarzenie na okreg, sekcja `m:` wspolnego klucza `arm_district`.

---

## 1. Pkt 1 - dymek z panem okregu i herbem

### 1.1 Co gra ma [KOD]
- `InformationManager.ShowTooltip(Type, params object[])` tylko rozsyla zdarzenie (TaleWorlds.Library InformationManager.cs:81-84);
  rejestr typow `RegisterTooltip<TRegistered, TTooltip>(Action<TTooltip, object[]>, movieName)` (:126-131) - slownik po `typeof(TRegistered)`.
- Pokazuje `GauntletInformationView.OnShowTooltip` (TaleWorlds.MountAndBlade.GauntletUI, :103-121): `Activator.CreateInstance(TooltipType,
  type, args)` + `LoadMovie(MovieName, vm)` w try/catch - blad = `Debug.FailedAssert` i brak dymka, nie wywrotka.
- Gra rejestruje swoje typy w `SandBoxViewSubModule.OnSubModuleLoad` -> `RegisterTooltipTypes` (dec SandBox.View :72-88, :360-379):
  Settlement, Clan, Hero, MobileParty, Army... - wszystkie z prefabem "PropertyBasedTooltip" (tylko ExplainedNumber ma "RundownTooltip"); `List<TooltipProperty>` (ten, ktorego uzywa
  dzis W2) rejestruje GauntletUISubModule.cs:193.
- `PropertyBasedTooltipVM` (TaleWorlds.Core.ViewModelCollection): lista `TooltipProperty`, `Mode` (tlo: 1 obcy, 2 swoi, 3 wrog -
  RefreshSettlementTooltip TooltipRefresherCollection.cs:892+), odswiezanie co 2 s wartosci z `Func<string>` (:63-64, :88-96),
  `IsExtended` (Alt) przebudowuje liste (:98-106). Ogolny `RefreshGenericPropertyBasedTooltip` ustawia zawsze `Mode = 0` (:117-125).
- Herb jako obrazek w UI gry: `BannerImageIdentifierVM(banner, nineGrid)` (ImageIdentifiers, Id = kod herbu, TextureProviderName
  "BannerImageTextureProvider") + w prefabie `ImageIdentifierWidget ... AdditionalArgs="@AdditionalArgs" ImageId="@Id"
  TextureProviderName="@TextureProviderName"` (np. ROT MultiSelectionQueryPopup.xml:63). nineGrid = true daje ksztalt choragwi
  (encyklopedia, Banner_9), false = kwadrat herbu.
- `RichTextWidget` w dymku zna znacznik `<img src="...">`, ale tylko dla SPRITE'OW z zaladowanych kategorii - herb rodu to tekstura
  renderowana z kodu (ThumbnailCache), nie sprite. Herbu tak nie wstawimy.
- Prefaby z modulow: `UIResourceManager` dodaje `\GUI\` kazdego modulu do ResourceDepot (TaleWorlds.Engine.GauntletUI, dekompilacja
  ilspycmd :115-122). **Armoury dzis nie ma katalogu GUI** (Modules\Armoury: ModuleData, bin, SubModule.xml) - paczka musi go dolozyc.

### 1.2 Droga zalecana: wlasny typ dymka (szkic `MapVillageTooltip.cs` + `GUI\Prefabs\ArmMapVillageTooltip.xml`)
- Klucz typu i dane = `MapVillageTip` (nazwa, okreg, osady, ludzie, miejsce, stan, rycerz, herb rodu z lore, czym zyje, wiara, historia,
  pamiec wojny). Wypelnia go widok w `Visual.OnHover` (W2 :1582-1607) raz na najechanie - jak dzis lista `TooltipProperty`.
- VM = `MapVillageTooltipVM : PropertyBasedTooltipVM` + `[DataSourceProperty] BannerImageIdentifierVM Banner` i `bool HasBanner`.
  Dziedziczymy cale zachowanie dymka gry (tlo sojusznik / wrog, Alt, odswiezanie, ukladanie kolumn `PropertyBasedTooltipWidget`).
- **PULAPKA (sprawdzona w kodzie):** `PropertyBasedTooltipVM.Refresh` wola `InvokeRefreshData(this)` z T = `PropertyBasedTooltipVM`,
  a `TooltipBaseVM.InvokeRefreshData<T>` sprawdza `OnRefreshData is Action<T, object[]>` (TooltipBaseVM.cs:89-95). Delegat
  `Action<MapVillageTooltipVM, object[]>` NIE przejdzie tego testu (pusty dymek, `IsActive = false`, bez bledu w logu). Trzeba podac
  `Action<PropertyBasedTooltipVM, object[]> cb = Refresh;` - kontrawariancja `Action<in T1, in T2>` pozwala go wlozyc tam, gdzie
  `RegisterTooltip<MapVillageTip, MapVillageTooltipVM>` chce `Action<MapVillageTooltipVM, object[]>` (szkic sie kompiluje, rozdz. 9).
- Konstruktor bazy wola `Refresh` od razu - nasz callback ustawia `Banner` jeszcze w konstruktorze; pola podklasy bez inicjalizatorow
  zaleznych od kolejnosci (szkic tak jest napisany).
- Rejestracja raz na sesje gry: `SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot` (Armoury juz tam ustawia latki); wlasny klucz
  typu, kolejnosc wobec gry bez znaczenia. `UnregisterTooltip<MapVillageTip>()` w `OnSubModuleUnloaded`.
- Prefab `ArmMapVillageTooltip.xml` = kopia ROT `PropertyBasedTooltip.xml` (ROT nadpisuje Native; te same pedzle `Tooltip.*`) z jedna
  zmiana: w `PropertyListBackground` pionowy `ListPanel Id="Stack"` = [herb 52x52, `IsVisible="@HasBanner"`] + `PropertyList`; sciezki
  `PropertyList="Body\PropertyListBackground\Stack\PropertyList"` i `WidgetToCopyHeightFrom="..\PropertyListBackground\Stack"`
  poprawione. `PropertyBasedTooltipWidget` szuka tylko `PropertyList` i `PropertyListBackground` (dekompilacja widgetu :32-34, :269-281).
  XML sprawdzony parserem (34 elementy, poprawny).
- Tresc (kolejnosc linii, teksty EN w `VillageLoreTexts.cs`):
  ```
  [herb]
  STONY HOLT
  A village of the Tumbledown lands, held by House Mormont of Bear Island
  Held in fief by Ser X            (tylko BK: lordship wsi u kogos innego niz glowa rodu pana)
  Sworn to them: Ser Rolph Wode    (dane lore pkt 3)
  4,000 souls in 16 settlements
  Burning / Burned ...             (stan)
  Hold Alt for more info           (wiersz gry str_map_tooltip_info, jak dymki partii - TooltipRefresherCollection :836-841)
  -- po Alt (albo przy ustawieniu gry "dlugie dymki" IsMapTooltipLongForm) --
  czym zyje / wiara i swiete miejsce / zdanie historii / pamiec wojny   (MultiLine - zawija sie do szerokosci dymka, TooltipPropertyWidget :507-514)
  ```
- Skad dane [KOD]: pan okregu = `Settlement.OwnerClan` wsi = `Village.Bound.OwnerClan` (Settlement.cs:490-497); "X lands" = `Village.Bound`
  (zamek / miasto okregu) albo sama wies - wybor tekstu nalezy do danych lore; herb = `Clan.Banner` (dla rodu panujacego zwraca
  `Kingdom.Banner`, Clan.cs:282-295 - tak samo pokazuje gra przy partiach); siedziba "of Z" = `Clan.HomeSettlement` (Clan.cs:378-387),
  zapas `Bound`. Nazwy rodow ROT bywaja z przecinkiem ("Tully,Blackfish", "Baratheon,Renly" - spclans.xml ROT-Content) - `HouseName`
  bierze czlon przed przecinkiem; slowo "House" tylko dla krain, gdzie pasuje (kolumna `house_style` danych lore; Essos: "the Maegyr
  family" albo bez slowa). Tlo dymka: wrog / swoi / obcy jak dymek osady gry.
- BannerKings: wsie maja tytul lordship (`BannerKingsConfig.Instance.TitleManager.GetTitle(settlement)`, `FeudalTitle.deJure`;
  dekompilacja BK TitleManager :160-183, FeudalTitle :31) - pan zamku moze dac wies rycerzowi (BK `GrantKnighthood(FeudalTitle, ...)` :206).
  To jest PRAWDZIWY "rycerz z ziemia" w grze - szkic `VillageLord.BkHolder` czyta go refleksja (jak reszta Armoury przy BK), tylko przy
  dymku i w menu, licznik potkniec zamiast wylacznika.
- Zapas (MCM "Map Village Tooltip Herb" = off albo rejestracja nieudana): dzisiejszy dymek `List<TooltipProperty>` + linia pana w
  KOLORZE rodu (`TooltipProperty` z `TextColor = Color.FromUint(Clan.Color)`) - bez nowych plikow, bez ryzyka prefabu.

### 1.3 Drogi odrzucone
- `ShowTooltip(typeof(Clan), clan)` - pokazuje dymek RODU (gra: RefreshClanTooltip), nie wioski, i dalej bez obrazka.
- `<img>` w RichText - tylko sprite'y, herb nie jest sprite'em.
- UIExtenderEx na prefab gry `PropertyBasedTooltip` - wstawilby herb do WSZYSTKICH dymkow gry; wlasny prefab jest izolowany.

---

## 2. Pkt 2 - proporczyk w barwach pana nad wioska

### 2.1 Jak gra stawia herb na mapie [KOD]
- Osady same NIE pokazuja herbu pana. Choragwie przy zamkach to choragwie PARTII w srodku: `MobilePartyVisual` (dec SandBox.View
  :884-921) stawia siatke `"campaign_flag"` w miejscu z `SettlementVisual.GetBannerPositionForParty` (encje z tagiem
  `map_banner_placeholder`, SettlementVisual.cs:59, :300-364, :514-528). Namiot oblezenia: ta sama siatka (`AddTentEntityForParty` :1327-1374,
  skala 0.15-0.5 wedlug sily partii).
- Herb na siatce: `public static MetaMesh MobilePartyVisual.GetBannerOfCharacter(Banner banner, string bannerMeshName)` (:1289-1325):
  `MetaMesh.GetCopy`, dla kazdej podsiatki bez tagu `dont_use_tableau` kopia materialu, tekstura herbu z
  `BannerVisualExtensions.GetTableauTextureLarge` (asynchronicznie; flaga shadera `use_tableau_blending`), kopie materialow w
  `MapScreen.CharacterBannerMaterialCache` z kluczem `Tuple<Material, Banner>` (MapScreen.cs:125).
- Tekstura herbu: `ThumbnailCache` po kodzie herbu (`"Mesh_BannerTableauLarge:" + BannerCode`, BannerThumbnailCreationBaseData.cs:28-36;
  BannerThumbnailCache.cs:25-54) - jedna tekstura na kod.
- `Banner` nie nadpisuje `Equals` (Banner.cs) - klucz pamieci materialow liczy po referencji. Gra i tak robi `new Banner(kod)` przy
  kazdej przebudowie ikony partii, wiec u niej pamiec prawie nie trafia (trzyma wlasna `_cachedBannerComponent` po kodzie, :904-920).
- Kolory herbu zmieniaja sie W MIEJSCU: `Clan.UpdateBannerColorsAccordingToKingdom` (Clan.cs:1377-1403) -> `Banner.ChangePrimaryColor /
  ChangeIconColors` zeruja `_bannerCode` (Banner.cs:100-158); `BannerCode` liczony leniwie (:45). Referencja `Clan.Banner` zostaje ta sama.
- `MapScreen.ClearGPUMemory` (:507-517) = `ThumbnailCacheManager.ForceClearAllCache` + `SandBoxViewVisualManager.ClearVisualMemory` -
  wolane przy wejsciu w kazda misje (MissionCampaignView.cs:28). Partie przebudowuja herby po powrocie (`MobilePartyVisual.ClearVisualMemory`).
- `MapScreen.OnFinalize` konczy komponenty widoku (nasz tez) PRZED `MapScene.ClearAll` i potem czysci `CharacterBannerMaterialCache`
  (:726-743).
- "campaign_flag" to najpewniej plotno z symulacja: `ApplyWindEffect` rzutuje komponent herbu na `ClothSimulatorComponent` (:393),
  `GameEntity.ComponentType` 3 = ClothSimulator (GameEntity.cs:13-23). [AUTOTEST]
- Inne siatki: `"map_banner"` (animacja rozmowy, GauntletMapParleyAnimationView.cs:113) - bez herbu, bez plotna; mozna jej dac ten sam
  material herbu - zapas, gdyby plotno kosztowalo za duzo. [AUTOTEST]

### 2.2 Projekt (szkic `MapVillagePennants.cs`)
- **Osobna encja w scenie** (nie dziecko obrazka wioski): rama w swiecie, bez przeliczania skali obrazka (matka x poziom, niejednorodna),
  wlasny cykl zycia; `CreateEmpty(scene, false, false, false)` bez fizyki i skryptow, `AddMultiMesh(GetBannerOfCharacter(b, "campaign_flag"))`,
  `SetReadyToRender`, `CheckResources` - te same wywolania co namiot partii i obrazek W2 (zadnych natywnych odczytow encji sceny typu
  `GetOldPrefabName`, ktore daly AccessViolation 08.10).
- Miejsce: przedni naroznik obrysu (0.85 polowy dlugosci wzdluz ulicy, 0.25 za frontem - strona drogi / wody), wysokosc z
  `MapSceneWrapper.GetTerrainHeightAndNormal` (IMapScene.cs:56), obrot wedlug `front_deg`, skala 0.32 (namiot 0.15-0.5).
- **Jeden obiekt `Banner` na kod herbu** (`_bannerByCode`): 50 wiosek jednego pana = jedna kopia materialu i jedna tekstura. Klucz po
  kodzie, bo kolory zmieniaja sie w miejscu.
- **Zmiana pana / barw:** raz na sekunde dla pokazanych proporczykow `VillageLord.CodeOf(okreg)` (jeden odczyt na okreg w przebiegu);
  inny kod = przebudowa tej jednej encji. Obejmuje wszystko naraz bez nasluchow: zmiana wlasciciela zamku (OnSettlementOwnerChanged),
  zmiana krolestwa (kolory), zmiana rodu panujacego (herb krolestwa). Nasluchy byly by szybsze o <= 1 s, ale kazdy to osobny przypadek.
- **Po misji:** `ClearVisualMemory` (widok W2 ma juz te metode, :687-691) -> `_bannerByCode.Clear()` i `Code = null` - proporczyki
  przebudowane z nowymi obiektami `Banner` przy nastepnym pokazaniu (inaczej trafienie w stara kopie materialu z wyczyszczona tekstura =
  pusta flaga).
- **Tylko blisko kamery:** kamera z <= 90 (MCM), promien z + 40, najblizsze 80 (MCM), budowa najwyzej 12 na cwierc sekundy; poza tym
  schowane (nie usuwane); usuwane z obrazkiem przy koncu mapy / wylaczeniu w MCM. Widocznosc razem z wioska (`VillageShown` ustawia widok
  - takze mgla ScoutingFog). Nic co klatke (CLAUDE.md 7).

### 2.3 Koszt [ZMIERZONE na `Modules\Armoury\ModuleData\arm_map_villages.tsv`, 2 449 wiosek w 422 okregach]
| Promien od kamery (jedn.) | wiosek: mediana / 90% / max | okregow (= najwyzej tyle herbow): mediana / 90% / max |
|---|---|---|
| 60 (kamera ok. 20) | 41 / 66 / 87 | 8 / 12 / 18 |
| 100 (kamera ok. 60) | 101 / 152 / 188 | 17 / 28 / 34 |
| 150 (kamera 110-160, bez proporczykow) | 201 / 301 / 351 | 33 / 54 / 63 |

- Proporczykow naraz najwyzej 80 (sufit), herbow (tekstur) najwyzej ok. 30 - zwykle mniej, bo pan trzyma kilka okregow. Tekstura herbu
  renderowana raz na kod (herby ROT maja do 100+ warstw - pierwszy raz drozej, potem pamiec).
- Plotno: 80 symulacji naraz to wiecej niz gra ma zwykle partii z choragwiami w kadrze - stad sufit i niski pulap kamery. [AUTOTEST:
  klatki przy z 30 / 60 / 90 z 0 / 40 / 80 proporczykami; czy plotno sie rusza (encja bez DoNotTick) i ile kosztuje; "map_banner" jako zapas.]
- Pamiec materialow `CharacterBannerMaterialCache`: wpis na (material x obiekt Banner); po kazdej misji nowe obiekty = nowe wpisy (gra
  robi to samo z partiami); czyszczona przy koncu mapy (MapScreen.cs:743).

---

## 3. Pkt 8 - menu okregu z lista wiosek

### 3.1 Menu wsi gry [KOD]
- `"village"` = `AddGameMenu` (PlayerTownVisitCampaignBehavior.cs:153; opcje :154-162), `"village_looted"` = `AddGameMenu`
  (VillageHostileActionCampaignBehavior.cs:118-119; opcje dokladane takze w PlayerTownVisit :163-164). **Oba to zwykle menu, nie menu
  oczekiwania** (`AddWaitGameMenu` jest tylko `village_wait_menus` :165). Pulapka CLAUDE.md 7 (SwitchToMenu z opcji menu oczekiwania =
  CTD w GameMenuVM.OnFrameTick) tu nie dotyczy - a szkic i tak NIE przelacza menu wcale.
- Armoury dodaje opcje z `OnSessionLaunched` przez `starter.AddGameMenuOption(menuId, id, tekst, warunek, skutek, isLeave, index)`
  (CampaignGameStarter.cs:93; IronBank.cs:295-311 - opcja w "town" + wlasne menu; BattleMuster.cs:38-40 - opcja w menu gry;
  NightRest.cs:1049-1059 - menu oczekiwania, tam `ExitToLast`). Warunek wolany przy kazdym odswiezeniu menu - tani i bez wyjatkow.

### 3.2 Projekt (szkic `DistrictMenu.cs`)
- Opcja "Look over the district" (tekst jest juz w W2 VillageTexts `arm_dist_option`) w `village` (index 4, za "Take a walk through the
  lands") i `village_looted` (index 0), `LeaveType.Submenu`; widoczna, gdy MCM on i okreg ma wioski w pliku.
- Skutek = okienko wyboru gry `MBInformationManager.ShowMultiSelectionInquiry` [KOD]:
  - wiersz = `InquiryElement(identifier, title, ImageIdentifier, isEnabled, hint)` (InquiryElement.cs:26-33);
    obrazek = `new BannerImageIdentifier(herb, false)` (BannerImageIdentifier.cs:5-10) - prefab ROT MultiSelectionQueryPopup.xml ma w wierszu
    ramke 86 x 63 z `ImageIdentifierWidget` wiazanym na TextureProviderName (:61-63) i `HintWidget` (:70, :74) - **herb w kazdym wierszu
    i dymek wiersza bez wlasnego prefabu**. Gra nie uzywa tam herbow (tylko postaci - PrisonBreakCampaignBehavior :473), wiec wyglad
    herbu w ramce 86 x 63 (rozciagniety kwadrat) [AUTOTEST];
  - tytul wiersza: "Hag's Bridge - by the bridge, 6,000 souls - burning" / wies gry "(the district village)" na koncu; dymek wiersza:
    rycerz, czym zyje, pamiec wojny; herb = rod zaprzysiezony z lore albo pan okregu;
  - min 0 / max 1 wybor: "Hear more" bez wyboru po prostu zamyka (MultiSelectionQueryPopUpVM.cs:219-221); wyszukiwarka przy > 10 wierszach.
- "Hear more" -> `InformationManager.ShowInquiry` z pelnym tekstem (rycerz, czym zyje, wiara, historia, pamiec wojny); "Back to the villages"
  otwiera liste od nowa. Bezpieczne: akcja okienka jest wolana PRZED `CloseQuery` (MultiSelectionQueryPopUpVM.cs:187-201), nowe okienko
  trafia do kolejki `GauntletQueryManager` (:216-226) i wyskakuje po zamknieciu biezacego.
- Wierszy na okreg [ZMIERZONE]: mediana 5, 90% 11, najwiecej 19 wiosek + wies gry - lista przewijana (ScrollablePanel prefabu).
- Zrodlo wierszy - interfejs `IDistrictRows` (szkic): W2 = plik wiosek + "tryb gry" (plonie pierwsza wioska lancucha przy `IsUnderRaid`,
  wies gry "looted" przy `IsRaided`); W3 = ksiega wiosek (`StateOf`). Plik danych musi byc dostepny bez widoku mapy - dzis parsuje go
  `MapVillagesView.Load` przy kazdym `OnInitialize`; menu potrzebuje wspolnej statycznej kopii (jedno wczytanie na sesje).
- Zero stanu i zero zapisu; nic nie zalezy od tego, czy gracz zamknie okienko.

---

## 4. Pkt 7 - pamiec wojny (kto spalil, kiedy, ilu ucieklo)

### 4.1 Co pamieta gra [KOD]
- `VillageStateChangedLogEntry` (TCS LogEntries :14-35): wies, stan stary / nowy, `RaiderPartyMapFaction`, `RaidLeader` (:347),
  `GameTime` - ale bez nadpisania `KeepInHistoryTime` = `CampaignTime.Days(7f)` (LogEntry.cs:18), a `LogEntryHistory.DeleteOutdatedLogs`
  (:61-79) usuwa starsze. Po tygodniu gra nie wie, kto spalil.
- Ksiega 113 (`Devastation.cs`, galaz paczki 113): konto okregu `Scar { Away, Dead }` (:64-66) - same sumy; `RaidState` rabunku w toku
  (:71-77) - bez zapisu, bez sprawcy. "Kto i kiedy" z niej nie wynika.
- Wniosek: wlasny maly zapis jest POTRZEBNY (CLAUDE.md "nic do sejwu bez potrzeby" - spelnione).

### 4.2 Skad dane [KOD]
- `CampaignEvents.VillageStateChanged(village, old, new, MobileParty raiderParty)` (CampaignEvents.cs:659; wolane z
  `ChangeVillageStateAction.ApplyInternal` :8-16): `BeingRaided` z napastnikiem na starcie rabunku (RaidEventComponent.cs:117),
  `Looted` z napastnikiem przy spaleniu (`OnBeforeFinalize` :132), `Normal` (bez napastnika) po przerwanym rabunku (:136).
- Liczby ludzi z 113: w `Devastation.EndFinalizer` (:392-430) po `Settle` - rabunek skonczony, trafieni policzeni. Wpiecie: `RaidState`
  dostaje `Dead` i `Away` (dwa pola), `Strike` dodaje do nich zabitych i uchodzcow tego kroku (dwa `+=` obok `sc.Away += refugees;
  sc.Dead += dead;` :248, przez parametr `rs` albo zwrot przez `out`), a `EndFinalizer` wola `WarMemory.NoteRaidPeople(v, rs.Dead, rs.Away)`.
  Bez 113 - pamiec bez liczb ("Burned by Ser Gregor's men in the autumn of 299 AC.").
- Kolejnosc w jednym rabunku [KOD]: [113 EndPrefix] -> `OnBeforeFinalize`: Looted -> `VillageStateChanged` (nasz zapis sprawcy) ->
  `RaidCompleted` -> [113 EndFinalizer: Settle -> `NoteRaidPeople` dopisuje liczby do rekordu z tej samej doby]. Przerwany rabunek
  (Normal) z uciekinierami = rekord "raided" (nie nadpisuje spalenia mlodszego niz rok).
- Ktora wioska na mapie: W2 - pierwsza wioska lancucha, na ktorej stoi ogien (widok `District.FxSlot`) - `WarMemory.BurningUid`;
  W3 - wioska z planu kroku ksiegi wiosek (`MapVillages.Plan`). Linia pamieci pokazuje sie przy TEJ wiosce (albo przy wsi gry).
- Data: `CampaignTime.Now.ToDays` (double - float gubil ulamek przy zapisie, proba rozdz. 9); tekst "in the autumn of 299 AC" z
  `CampaignTime.Days(d).GetSeasonOfYear / GetYear` (CampaignTime.cs:158-160; dlugi rok Armoury zachowuje numeracje lat ROT - Calendar.cs).

### 4.3 Zapis (szkic `WarMemory.cs`)
- Wspolny klucz `arm_district` (PROJEKT-WIOSKI rozdz. 7.2: jeden klucz, sekcje z prefiksem, kazda paczka czyta i pisze swoje). Pamiec:
  `|m:<idWsi>=<doba>;<rodzaj 1 rabunek / 2 spalona>;<ile razy spalona>;<id bohatera>;<nazwa sprawcy>;<gracz 0/1>;<uciekli>;<zabici>;<uid8>`.
  Tylko OSTATNIE zdarzenie okregu + licznik spalen. Jesli pamiec wejdzie przed W3, klucz `arm_district` zaklada ona (same sekcje `m:`);
  W3 dopisze `L:` / `n:` / `w:` / `a:` bez migracji. Wzor w `ArmouryBehavior.SyncData`: `string s = Export(); dataStore.SyncData(key, ref s);
  if (IsLoading) Import(s);` - we WLASNYM try (jak `arm_startstock`, ArmouryBehavior.cs:393-402), zeby wyjatek innego klucza go nie zgubil.
- Rozmiar: ok. 70 B na okreg - wojna w kilkudziesieciu okregach 2-5 KB, wszystkie 571 okregow ok. 50 KB.
- Brak klucza / stary zapis / smieci (NaN, zly rodzaj, za malo pol) = wiersz pominiety, reszta zostaje; cudze sekcje ignorowane. Reset w
  konstruktorze `ArmouryBehavior` (jak reszta stanu, :388). Nazwa sprawcy zapisana tekstem (bohater mogl zginac; bandy i Inni nie maja
  bohatera) - z niej znaki `| ; = \n` usuwane.
- Watki: zapis z ticku kampanii, odczyt w dymku / menu - pod blokada (jak `_scars` w 113).

### 4.4 Pamiec z ksiazek (statyczna, bez zapisu)
- Kolumna `war_lore` danych lore: zdanie pokazywane, dopoki gra nie zapisze wlasnej pamieci tej wioski. Przyklad z lore: przed wojna
  Pieciu Kroli ludzie Gregora Clegane'a bez choragwi napadli Mummer's Ford, Sherrer i Wendish Town w Dorzeczu (A Wiki of Ice and Fire:
  [Raid on Sherrer](https://awoiaf.westeros.org/index.php/Raid_on_Sherrer), [Mummer's Ford](https://awoiaf.westeros.org/index.php/Mummer%27s_Ford),
  [Battle at the Mummer's Ford](https://awoiaf.westeros.org/index.php/Battle_at_the_Mummer%27s_Ford) - koniec 298 / poczatek 299 AC).
  Stad tez tekst zastepczy sprawcy bez imienia: "men without banners". Kampania ROT zaczyna sie w 299 AC (Calendar.cs) - takie zdania
  pasuja do wiosek Dorzecza nad Red Fork. Dobor wiosek i zdan = badanie lore (A/B), nie ten dokument.

---

## 5. Kontrakt danych lore (propozycja dla badan A/B; szkic `MapVillageLore.cs`)
- Plik `Armoury\ModuleData\arm_map_village_lore.tsv`, jeden wiersz na wioske, klucz `uid` z `arm_map_villages.tsv`; kolumny wedlug naglowka
  `#uid\t...` (kolejnosc dowolna, brak kolumny = puste): `house`, `house_style` (house / family / none), `house_clan` (StringId klanu gry ROT
  - herb na zywo, `Clan.Banner`), `house_banner` (kod herbu Bannerlorda dla rodow spoza ROT, sprawdzany `Banner.IsValidBannerCode`, Banner.cs:566),
  `knight`, `livelihood`, `faith`, `history`, `war_lore` - gotowe zdania EN.
- Wiersz `# layout_crc32: <crc32 arm_map_villages.tsv>` - inny niz plik ukladu w grze = lore wylaczone (uid = id wsi + skrot POLOZENIA:
  nowy przebieg generatora v4 zmienia uid, np. `ROT_castle15_village2#f1926faa` w pliku 4000 -> `#c7902781` w proba4).
- Badanie B klucze juz po `uid` (`b-skrypty\wioski_b.tsv`) - zgodne.
- Kolumna miejsca: dzis `kind` = miejsce (road / river / coast / crossroad / bridge / field / lake - w pliku 1 160 / 545 / 430 / 166 / 74 / 69 / 5);
  v4 dodaje `kind` = rodzaj (mill / farm / granary / fishing / village). Parser W2 czyta `kind` po nazwie kolumny - v4 musi nazwac miejsce
  inaczej (np. `place`) albo rodzaj inaczej (np. `type`), inaczej dymek i menu pomyla "by the river" z "mill".

---

## 6. Teksty w grze (EN; `szkice\VillageLoreTexts.cs`, w modzie dopisac do `VillageTexts.xml` i przegenerowac)
- Dymek: "A village of the {LANDS} lands, held by {HOUSE} of {SEAT}", "Held in fief by {HERO}", "Sworn to them: {KNIGHT}"; wiersz Alt z gry
  (`str_map_tooltip_info`).
- Pamiec: "Burned by {RAIDER} in the {SEASON} of {YEAR} AC.", "...; about {FLED} souls fled and {DEAD} were killed.", "Raided by {RAIDER}
  in the {SEASON} of {YEAR} AC; about {FLED} souls fled.", "It has burned {TIMES} times since the war began.", "{NAME}'s men", "your men",
  "men without banners". Liczby zaokraglone ("about 600 souls").
- Menu: "Look over the district" / "Villages of the {DISTRICT} district" / "Leave" sa juz w W2 (`arm_dist_*` - w modzie uzyc tych samych id,
  nie dublowac); nowe: "{HELD_BY}. Choose a village to hear more of it.", "{VILLAGE} - {PLACE}, {PEOPLE} souls - {STATE}",
  "{VILLAGE} (the district village) - ...", "Hear more", "Back to the villages", stany "standing / burning / burned / looted, smoke still rising".

## 7. MCM (po angielsku; `python tools/gen_mcm.py` po dodaniu do Settings.cs)
| Ustawienie | Domyslnie | Pkt |
|---|---|---|
| Map Village Tooltip Herb (herb pana w dymku; off = zwykly dymek z linia pana w kolorze rodu) | on | 1 |
| Map Village Pennants | on | 2 |
| Map Village Pennant Max Camera Height | 90 | 2 |
| Map Village Pennant Max (naraz, najblizsze kamerze) | 80 | 2 |
| District Menu ("Look over the district") | on | 8 |
| War Memory (zapis: sekcje m: w arm_district) | on | 7 |

---

## 8. Kolejnosc i wpiecia (bez ruszania kodu teraz - inny agent zmienia wyglad w MapVillagesView.cs)
1. **L1 - dymek z herbem + proporczyk (pkt 1, 2)** - sam widok, bez zapisu, nie wymaga 108-113. Wpiecia w W2: `Visual.OnHover` -> wypelnic
   `MapVillageTip` i `MapVillageTooltip.Show` (zamiast listy `TooltipProperty`); w `Load` lista `PennantSlot` z rekordow (X, Y, front_deg, obrys,
   okreg); w `OnVisualTick` po `UpdateVisibility` -> `pennants.Tick(pokazane, kamera, dt)`; przy pokazaniu / schowaniu wioski
   `VillageShown`; `ClearVisualMemory` -> `pennants.ClearVisualMemory()`; `OnFinalize` / wylaczenie w MCM -> `RemoveAll` / `DropAll`;
   `SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot` -> `MapVillageTooltip.Register()`. Instalacja: NOWY katalog `Modules\Armoury\GUI\Prefabs`.
   Referencje: nic nowego (SandBox.View i TaleWorlds.Core.ViewModelCollection juz sa).
2. **L2 - menu okregu (pkt 8)** - z L1 albo osobno; potrzebuje statycznej kopii pliku wiosek (`IDistrictRows`) i `DistrictMenu.AddMenus` w
   `OnSessionLaunched`. Bez zapisu.
3. **L3 - pamiec wojny (pkt 7)** - po grupie 5 (113): `WarMemory.RegisterEvents`, wpiecie w `Devastation.EndFinalizer`, sekcja `m:` klucza
   `arm_district` w `ArmouryBehavior.SyncData` (wlasny try), `Reset` w konstruktorze. Bez 113 mozna wgrac sama "kto i kiedy".
4. Dane lore (pkt 3-6) - sam plik `arm_map_village_lore.tsv` + `MapVillageLore.Load` przy wczytaniu pliku wiosek; dymek i menu czytaja
   `LoreRow` po uid.
Kazda paczka: autor + niezalezny recenzent, build kod 0, kontrola calosci (CLAUDE.md 8.0), wpis CHANGELOG, wylacznik w MCM, wgranie tylko na "wgraj".

---

## 9. Co sprawdzone poza gra
- **Kompilacja szkicow na DLL gry 1.4.8** (bin\Win64_Shipping_Client + Modules\SandBox\bin): `szkice\*.cs` + zaslepki `Log` / `Settings`
  (`szkice\_proba_kompilacji\Stubs.cs`) + kopia W2 `VillageTexts.cs`, projekt `szkice\_proba_kompilacji\Szkice.csproj` (budowany w kopii poza
  katalogiem szkicow): **Build succeeded, 0 bledow** (2 ostrzezenia: pola ustawiane przez widok). Potwierdza sygnatury: `RegisterTooltip` z
  delegatem kontrawariantnym, `PropertyBasedTooltipVM(Type, object[])`, `BannerImageIdentifierVM`, `MobilePartyVisual.GetBannerOfCharacter`,
  `MultiSelectionInquiryData` (12 argumentow), `InquiryElement` z obrazkiem, `CampaignEvents.VillageStateChanged`, `IMapScene.GetTerrainHeightAndNormal`.
- **Proba zapisu pamieci** (`szkice\_proba_kompilacji\ProbaZapisuPamieci.cs.txt`, konsola na tych samych DLL): eksport -> wczytanie -> eksport
  identyczny; cudze sekcje (`n:`, `w:`) i smieci (NaN, zly rodzaj, za malo pol, null, tekst bez sekcji) pominiete - **6 z 6 prob OK**. Pierwsza
  wersja zgubila ulamek doby (float przy ok. 109 tys. dob) - poprawione na double.
- Prefab XML poprawny skladniowo (parser). Gauntlet go jeszcze nie widzial. [AUTOTEST]

## 10. Ryzyka / co sprawdzic w autotescie (zgoda Jeffa na autotest 07.10)
1. **Dymek z herbem:** czy prefab sie laduje (brak "Failed to display tooltip of type" w rgl_log), czy herb sie rysuje w warstwie dymka
   (BannerImageTextureProvider), Alt, tlo wrog / swoi, odswiezanie; wyglad przy dlugich nazwach rodow. Zapas: MCM off = zwykly dymek.
2. **Proporczyk:** czy "campaign_flag" na encji bez skryptow i bez DoNotTick sie rysuje i rusza (plotno); klatki przy z 30 / 60 / 90 i 0 / 40 / 80
   proporczykach (cel: spadek < 5%); powrot z bitwy (herby nie puste); zmiana pana (zdobycie zamku) i zmiana krolestwa - barwy w <= 1 s;
   skala i miejsce (nie na drodze, nie w rzece - obrys sprawdzony przez generator, ale proporczyk stoi 0.25 za frontem). Zapas: "map_banner".
3. **Menu:** opcja w `village` i `village_looted`, herby w wierszach (rozciagniecie w ramce 86 x 63), dymek wiersza, "Hear more" -> "Back" ->
   lista, wyjscie z wioski przy otwartym okienku; inne mody (BK, ROT) moga dokladac opcje do "village" - sprawdzic kolejnosc i czy index 4 nie przesuwa ich opcji (niesprawdzone).
4. **Pamiec:** rabunek AI -> rekord z nazwa; zapis i wczytanie -> linia zostaje; rozmiar `arm_district` w zapisie; rabunek przerwany.
5. Nazwy rodow ROT z przecinkiem i rody Essos ("House" nie wszedzie) - tekst z danych lore (`house_style`).
6. BK przez refleksje: zmiana API BK = brak linii "Held in fief by" (licznik potkniec, bez wywrotki).
7. Aktualizacja gry: `MobilePartyVisual.GetBannerOfCharacter` i `PropertyBasedTooltipWidget` to kod gry - przy nowej wersji sprawdzic dekompilacje.
8. Mod `Bannerlord.CCsBanners` (wlasne ikony herbow) jest w zestawie - kody herbow z jego ikonami tez przejda przez `IsValidBannerCode` /
   tablice ikon; do potwierdzenia w autotescie na rodzie z ikona CCs.

## 11. Pliki
- `SCR\dzien-6\wioski-lore\TECHNIKA.md` - ten dokument.
- `szkice\MapVillageTooltip.cs` (typ dymka, VM z herbem, rejestracja, zapas), `szkice\GUI\Prefabs\ArmMapVillageTooltip.xml` (prefab),
  `szkice\VillageLord.cs` (pan, herb, siedziba, BK lordship), `szkice\MapVillagePennants.cs` (proporczyki), `szkice\DistrictMenu.cs` (menu),
  `szkice\WarMemory.cs` (pamiec + zapis), `szkice\MapVillageLore.cs` (kontrakt danych lore), `szkice\VillageLoreTexts.cs` (teksty EN),
  `szkice\_proba_kompilacji\Szkice.csproj`, `Stubs.cs` (zaslepki + nowe pola MCM), `ProbaZapisuPamieci.cs.txt`.
- Dekompilacje dolozone do tego badania (ilspycmd, tylko odczyt): `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8\scratchpad\dec\`
  (UIResourceManager, PropertyBasedTooltipWidget, TooltipPropertyWidget, BK TitleManager / FeudalTitle / Estate).
