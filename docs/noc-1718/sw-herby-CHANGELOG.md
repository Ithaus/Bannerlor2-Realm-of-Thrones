## 2026-10-18 (noc 17/18.10, paczka sw-herby: proporczyki wiosek na malych teksturach 512x512 z wlasnym kluczem i zwalnianie tekstur dalekich herbow - koniec limitu 160 i herbu pana zamiast gospodarza)

**Mod:** Armoury + `tools/sprawdz_logi.py` (grupa `nherby`) + `tools/gen_mcm.py` (McmSettings) + `Armoury.csproj` / `libs/README.md` | **Galaz:** `w-toku/sw-herby` (od `efbbf0dc` = noc-ks4 KS r4 cz. 5) | **Tor:** M (bez gry; dekompilacja 1.4.8, build kod 0, checker na starych i syntetycznych logach; test w grze robi prowadzacy noc) | **Klucze:** nowy `VillagePennantSmallTexture` (domyslnie tak; NIE = stara droga dokladnie jak dotad). Nic do zapisu gry.

**Dla Jeffa (prosto):**
- Proporczyki nad wioskami uzywaja teraz czterokrotnie mniejszych obrazkow herbow (512 x 512 zamiast 1024 x 1024). Na mapie wygladaja tak samo - proporczyk jest maly.
- Herby wiosek daleko od kamery sa zwalniane z pamieci karty graficznej, gdy jest ich w pamieci ponad 128. Kiedy wrocisz kamera w to miejsce, proporczyki stana od nowa (kilkanascie na cwierc sekundy, jak dotad).
- Dzieki temu po dlugiej podrozy po mapie bez bitwy kazda wioska pokazuje herb swojego rodu. Dotad po ok. 160 roznych herbach nowe wioski dostawaly herb pana (w tescie 125 wiosek).
- Pamiec na herby proporczykow: najwyzej ok. 150 MB zamiast do 640 MB.
- Wylacznik w MCM: "Village Pennant Small Texture" (wylaczone = stare duze obrazki i stary limit).

**Problem:** STAN-PRAC ### 60 (WGRANIE 73): "limit tekstur herbow 160 (1024x1024, ok. 4 MiB) do najblizszej misji - po dlugim zwiedzaniu bez misji czesc wiosek pokazuje herb pana (w tescie 125)". Dowod: test Armoury-2026-10-16_14-49-31.log + zdjecia CrashScribe/zdjecia/at-20261016-144916 (autotest zdjec jezdzi kamera po wielu celach bez misji); w nowszych logach licznik "wiosek z herbem pana zamiast gospodarza" w linii "Proporczyki (E7/W5): w kadrze" = 0 tylko dlatego, ze kamera nie zwiedzala daleko (herbow w pamieci najwiecej 95 / 50 / 42).

**Przyczyna (sprawdzone w dekompilacji 1.4.8):**
- `MobilePartyVisual.GetBannerOfCharacter` (SandBox.View) -> `BannerVisualExtensions.GetTableauTextureLarge` -> `BannerTextureCreationData(isLarge: true)` -> `ThumbnailCacheManager.CreateTexture` -> `BannerPersistentTextureCache` (TaleWorlds.MountAndBlade.View): klucz "Mesh_BannerTableauLarge:<kod>", licznik odwolan 1 na zawsze, `OnReleaseTexture` zwraca true i nic nie robi. Tekstura zyje do `ForceClearAllCache(true)` w `MapScreen.ClearGPUMemory` (przed misja) albo `FlushCache` (koniec mapy / menu glowne).
- `BannerTextureCreator.CreateTexture`: 1024x1024 (tableau duza) albo 512x512 (mala); innych rozmiarow gra nie daje.
- Limit 100/160 byl NASZ (`CapCode` w MapVillagePennants.cs, recenzja D2 16.10) - ochrona pamieci karty (160 x 4 MiB = 640 MiB). Ponad 160 kodow w pamieci nowy herb gospodarza dostawal herb pana.

**Zmiana:**
- `Settings.cs` / `McmSettings.cs` (gen_mcm): `VillagePennantSmallTexture = true` (opis po angielsku w MCM).
- `MapVillagePennants.cs` (tylko przy kluczu):
  1. Wlasna kopia materialu choragwi, jak `GetBannerOfCharacter`: kopia siatki `campaign_flag`, `material.CreateCopy()`, tekstura herbu w slot 1 (DiffuseMap2) + flaga `use_tableau_blending`.
  2. Tekstura MALA 512x512 przez `PenTexData : BannerTextureCreationData(isLarge: false)`. To ta sama droga gry co tarcze z herbem w misji (`GetTableauTextureSmall`): pamiec trwala, tekstura always_valid, setAction od razu. Klucz jest WLASNY: `RenderId = "ArmPen_Mesh_BannerTableauSmall:<kod>"`. Nikt inny go nie zamawia, wiec zwolnienie nie zabierze tekstury choragwi partii, zaglom NavalDLC, tarczom w ekwipunku ani dymkowi.
  3. Ksiega herbow `_penTex`: kod -> kopie materialow, tekstura, takt ostatniego uzycia. Herb uzyty w ticku = kod pierwszego proporczyka kazdej z 80 wybranych wiosek i kod POKAZANEGO drugiego. W kadrze najwyzej 100 proporczykow, wiec w uzyciu < 128 herbow.
  4. Ponad `PenTexKeep` (128) herbow w pamieci zwalniane sa najdawniej uzyte: nieuzyte w tym ticku, zamowione ponad 1 s temu, najwyzej 32 na tick (nowych najwyzej 12 - budzet budowy).
     - Jeden przebieg po wioskach: encje proporczykow z tymi herbami zdjete RAZ. Sa daleko i schowane; nigdy wioski wybranej w tym ticku. `Why = PenWhyFar`, przebudowa przy nastepnym wyborze w budzecie budowy.
     - Potem wezel pamieci gry usuniety: `ThumbnailCache<BannerTextureCreationData>.RemoveThumbnailCacheNode(node, true)` przez refleksje na `_map` i metode (protected). To ta sama sciezka, ktora `ThumbnailCache.Tick` zwalnia nieuzywane miniatury: anuluje zamowienie, `Texture.Release`, Count--.
     - Na koniec nasze kopie materialow `ManualInvalidate`.
  5. Herb pana zamiast gospodarza tylko jako siatka bezpieczenstwa: `PenTexHard` 224, a w uzyciu jest najwyzej 100 + mlode 48. Przy dzialajacym zwalnianiu nigdy.
  6. Brak refleksji (inna wersja gry): male tekstury bez zwalniania (do misji), limit 400/640 - ta sama pamiec co stare 100/160 duzych. W linii startu: "zwalnianie dalekich BRAK (powod)".
  7. Po misji (`ClearVisualMemory`, zaraz potem `ForceClearAllCache` gry zwalnia nasze tekstury natychmiast): ksiega zapomniana bez ruszania pamieci gry, proporczyki przebudowane przy pokazaniu jak dotad (`PenWhyMission`).
  8. Koniec mapy / wylacznik / zmiana klucza w MCM (`RemovePennants`): encje zdjete, wszystkie herby zwolnione. Cudza scena (`DropPennants`): same odwolania.
  9. Klucz NIE: `GetBannerOfCharacter` + `_penBanners` + `CapCode` 100/160 jak dotad. Linia "Proporczyki (E7/W5): w kadrze" ma ten sam format; przy kluczu "herbow w pamieci" = ksiega i "limit 128/224".
- Log: "Proporczyki - tekstury (E7/W5): start | ..." (raz na mape), "... stan | ..." (co 2 minuty przy zmianie), "... przy zdjeciu | ..." (koniec mapy, wylacznik, zmiana klucza). Pola linii:
  - herby nowe, tekstury zamowione (wspolne, nieprzyjete), zwolnione (bez klucza w pamieci gry), zapomniane po misji, w pamieci (najwiecej, cel, siatka);
  - TOZSAMOSC ksiegi: nowe - zwolnione - zapomniane - w pamieci = 0;
  - pamiec gry: klucze ArmPen_ w BannerPersistentTextureCache i roznica z ksiega (0 = zwalnianie naprawde oddaje wezly), herbow gry razem (pojemnosc 1000), MiB (najwiecej);
  - rozmiar tekstury odczytany z gry (Width x Height, KiB);
  - encje (zywe, zdjete przy zwalnianiu, przebudowane, zablokowane);
  - herb zastepczy (wiosek, teraz w kadrze);
  - zwalnianie (przebiegi, sr. ms), potkniecia.
- `Armoury.csproj`: referencja `TaleWorlds.MountAndBlade.View.dll` (`NativeViewLibs`: z GameLibs, a gdy jej tam nie ma - z `Modules/Native/bin/Win64_Shipping_Client` gry, jak SandBoxLibs); `libs/README.md` dopisane.
- `tools/sprawdz_logi.py --grupa nherby <log>` (opis w naglowku):
  - NHB-0 linie paczki;
  - NHB-1 tryb (klucz tak + zwalnianie CZYNNE);
  - NHB-2 rozmiar z gry 512x512;
  - NHB-3 tozsamosc 0 w kazdej linii;
  - NHB-4 klucze ArmPen_ = ksiega;
  - NHB-5 herb zastepczy 0 / w kadrze 0;
  - NHB-6 w pamieci <= 224;
  - NHB-7 zwalnianie przy dlugiej podrozy (nowe > 128 -> zwolnione > 0);
  - NHB-8 <= 256 MiB;
  - NHB-9 przebieg zwalniania <= 2 ms;
  - NHB-10 herbow gry <= 1000;
  - NHB-11 potkniecia i ERROR 0;
  - NHB-12 nieprzyjete 0;
  - NHB-13 liczniki (INFO).
  - Na logu 15-37-31 (sprzed paczki): INFO, bez wyjatku; nks na tym logu bez zmian (24/53).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje:
  - Klucz NIE - kod starej drogi bez zmian, tylko nowe linie logu. Klucz tak - zmienione tylko materialy i tekstury proporczykow wiosek.
  - Wybor wiosek, limit encji 100, budzet budowy 12, drugi proporczyk D2, przebudowa po misji i zmiana herbu - bez zmian.
  - `_penBanners`, `PenState` i ksiegi uzywa tylko MapVillagePennants.cs (grep).
  - Format linii "Proporczyki (E7/W5): w kadrze" bez zmian (etap7_w5 czyta: najwiecej, budowa sr., klatek z budowa, potkniecia, wiosek bez proporczyka, przebudowy po misji, zmiany herbu).
  - Czas zwalniania poza pomiarem budowy W5-2.
- Kolizje:
  - Wlasny klucz ArmPen_ - zadna inna tekstura gry (choragwie partii i zagle NavalDLC "Mesh_BannerTableauLarge", tarcze i ekwipunek "Mesh_BannerTableauSmall", UI BannerThumbnailCache) nie jest zwalniana ani wspoldzielona.
  - `ForceClearAllCache` wolaja tylko MapScreen.ClearGPUMemory i MapScreen.OnInitialize (grep wszystkich modow w Modules); `ClearVisualMemory` tylko z ClearGPUMemory.
  - AIInfluence (zaciemniony) i NavalDLC.View odwoluja sie do pamieci miniatur, ale nie do naszych kluczy.
  - MapScreen.CharacterBannerMaterialCache nie rosnie juz od proporczykow (wlasne materialy) - drobna poprawa.
- Cudzy kod: refleksja na protected `_map` / `RemoveThumbnailCacheNode` sprawdzana raz. Brak = BRAK w linii startu i stara pamiec (do misji) z limitem 400/640, bez wyjatku.
- W grze sprawdzic (autotest zdjec po wielu celach, ktory dawal 125):
  - nherby: NHB-5 = 0, NHB-4 roznica 0, NHB-7 zwolnione > 0;
  - zdjecia: herby na proporczykach ostre jak dotad (512 na malym plotnie), bez bialych / pustych flag;
  - po bitwie proporczyki wracaja (przebudowy po misji > 0);
  - etap7_w5 W5-2 < 8 ms, W5-3 potkniecia 0;
  - linia startu "zwalnianie dalekich CZYNNE".
- Ryzyko silnika (nie do sprawdzenia bez gry): `RemoveThumbnailCacheNode` na wezle pamieci trwalej (gra robi to dla miniatur UI, dla pamieci trwalej nigdy, bo licznik odwolan 1). Zwalniamy tylko wlasne klucze, po zdjeciu encji i po wyrenderowaniu (herb starszy niz 1 s).
  - Gdyby bylo zle (puste flagi, wywrotka przy zwalnianiu): klucz VillagePennantSmallTexture = NIE wraca do starej drogi bez restartu (zmiana w MCM = proporczyki zdjete i od nowa).
- 256x256 NIE zrobione: gra daje tylko 512 albo 1024 (`BannerTextureCreator`). 256 wymagaloby wlasnego widoku miniatur i wewnetrznej sceny gry przez refleksje - za duze ryzyko wywrotki bez testu w grze. 512 + zwalnianie konczy limit.

**Status:** DO SPRAWDZENIA (nie wgrane; build kod 0; gra nie uruchamiana; test w grze - autotest zdjec wiosek + `--grupa nherby` i etap7_w5).
