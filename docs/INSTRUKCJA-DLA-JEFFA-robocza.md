# Instrukcja dla Jeffa - WERSJA ROBOCZA (uzupelnic po D5: menu MCM)

Stan: noc 15/16.10.2026, gra na WGRANIU 61. Plan: `PLAN-DO-KONCA-MODA-2026-10-09.md` Z.9 = `CO-ZOSTALO-DO-KONCA-MODA-2026-10-14.md` D10.
Tu jest tylko to, co trzeba zrobic recznie poza gra albo pilnowac po aktualizacjach. Lista najwazniejszych suwakow MCM
dojdzie po D5 (porzadki menu), bo do konca zmian w kodzie nazwy i grupy jeszcze sie przesuwaja.

Zasada ogolna: kazda zmiana plikow ustawien - **przy zamknietej grze**. Claude niczego nie wgrywa i nie testuje, gdy gra dziala.

## 1. Po aktualizacji BetterEconomy (BEE)

Aktualizacja BEE nadpisuje jego plik ustawien `Modules\BetterEconomy\ModuleData\better_economy_settings.xml`. Wtedy wracaja
"ujscia" BEE: zloto i towar z niczego albo w nicosc u AI oraz losowe zdarzenia (jarmark, susza, zaraza bydla).

1. Zamknij gre.
2. W PowerShell, w katalogu repo (`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`):
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File tools\bee\zamknij-ujscia-bee.ps1 -ListaZFundamentu
   ```
   Skrypt zmienia 13 wartosci "ujsc" (wariant z 08.10: zbrojownie BEE zamkniete takze dla Ciebie) i klucz 14
   `EconomicEventDailySpawnChance` = 0. Przed zapisem robi kopie `better_economy_settings.xml.bak-<data>-przed-BEE-ujscia`
   i na koncu wypisuje tabele "przed / po".
3. Wynik skryptu:
   - kod 0 - zrobione albo plik juz byl zamkniety (nic nie zapisal);
   - kod 1 - STOP bez zapisu: nowa wersja BEE zmienila plik (inny uklad albo inne wartosci). **Nic nie poprawiaj recznie - daj znac Claude.**
   - kod 2 - gra jest uruchomiona (bez zapisu);
   - kod 3 - blad kopii albo zapisu (komunikat mowi, w jakim stanie jest plik).
4. Sprawdzenie bez zapisu: ten sam wiersz z `-NaSucho` na koncu. Cofniecie: `tools\bee\cofnij-ujscia-bee.ps1`.

Dzis (16.10) plik jest zamkniety (np. `ArmoryRequiredArtisans` 1000000000, `EconomicEventDailySpawnChance` 0) - nic nie rob,
dopoki BEE sie nie zaktualizuje. Pelny opis: `tools/bee/OPIS.md` (rozdz. 11 - klucz 14).

## 2. Diplomacy

Plik `Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json` (te same
wartosci widac w MCM Diplomacy). Po aktualizacji Diplomacy, ktora zresetuje ustawienia, ustaw je ponownie:

- **Scaling War Reparations Gold Cost Multiplier = 10** (bylo 50; klucz `ScalingWarReparationsGoldCostMultiplier`). Dzis 10.
- **Zmeczenie wojna podzielone przez 4.33** (rok 364 dni zamiast 84 - wojna trwa w latach tyle samo co w grze bez modow):
  `WarExhaustionPerDay` 0.058, `WarExhaustionPerCasualty` 0.0046, `WarExhaustionPerCaravanRaid` 0.46, `WarExhaustionPerImprisonment` 0.23,
  `WarExhaustionPerDeath` 1.15, `WarExhaustionPerRaid` 0.69, `WarExhaustionPerSiege` 2.31, `WarExhaustionWhenOccupied` 3.46. Dzis wszystkie 8 tak (ustawione 14.10).
  Czasy Diplomacy (najkrotsza wojna, przerwy, sojusze, pakty) przeliczone 13.10 - tez nie ruszac.

## 3. BannerKings

- `Configs\ModSettings\Global\BannerKings\BannerKings.json`: **"EnableReligion": true** - religia BK wlaczona (Twoja decyzja
  14.10 "napraw religie"). Dzis true. Po aktualizacji BK sprawdz, czy nie wrocilo false.

## 4. MCM - ustawienia naszych modow (Armoury, RealisticCaptivity, GrandTourney)

- MCM pamieta kazda wartosc w pliku `Configs\ModSettings\Global\<Mod>\<Mod>.json` i ta wartosc wygrywa z domyslna z kodu.
  Dlatego, gdy Claude zmienia wartosc domyslna, sam sprawdza i poprawia ten plik (gra zamknieta, kopia `.bak`).
  Ty nic nie musisz robic - tylko nie przywracaj starych plikow `.json` z kopii bez pytania.
- **Weeks Per Season = 13** (Armoury, klucz `WeeksPerSeason`) = rok 364 dni. Dzis 13. Nie zmieniac w trakcie kampanii - wszystkie
  przeliczenia "na rok" w modach i w Diplomacy stoja na 364 dniach.
- Kazda nasza nowa mechanika ma wlasny wylacznik (domyslnie wlaczony); "off" w opisie = stan sprzed tej zmiany.
  Przyklad z tej nocy: grupa "Siege camps without fighting men" (Siege Phantom Lift).
- **DO UZUPELNIENIA PO D5:** lista najwazniejszych suwakow (ekonomia, oblezenia, kuznia, sen) z prostym opisem.

## 5. CrashScribe - ustawienia w pliku, nie w MCM

- Plik: `Modules\CrashScribe\ModuleData\CrashScribe.settings.xml`. Kazdy klucz ma nad soba angielski opis; wartosci true / false
  albo liczba. CrashScribe czyta plik przy starcie gry - zmiana dziala od nastepnego uruchomienia.
- Brak klucza w pliku = wartosc domyslna z kodu (dlatego stary plik tez dziala). Claude podmienia ten plik przy wgraniu,
  z kopia `CrashScribe.settings.xml.bak-<data>-...-przed` obok.
- Przyklad z tej nocy: `BkTitlesVillageFix` (wsie bez tytulu BannerKings dostaja go w biegnacej kampanii).

## 6. Gdzie sa logi (to wysylaj Claude po problemie)

| Co | Gdzie |
|---|---|
| Armoury (wiekszosc mechanik) | `Modules\Armoury\Armoury-<data>_<czas>.log` (jeden na sesje gry) i `Modules\Armoury\Logs\<sesja>\` (tabele csv) |
| CrashScribe (bledy, crash, fabula, tytuly) | `Documents\Mount and Blade II Bannerlord\CrashScribe\session-<data>_<czas>.log` |
| RealisticCaptivity | `Modules\Armoury\RealisticCaptivity.log` |

Po crashu albo dziwnym zachowaniu wystarczy powiedziec, o ktorej godzinie zegara komputera to bylo - Claude znajdzie wlasciwa sesje.

## 7. Jak wrocic do poprzedniej wersji moda

Przy kazdym wgraniu Claude zostawia kopie starej DLL obok nowej:
`Modules\<Mod>\bin\Win64_Shipping_Client\<Mod>.dll.bak-<data>-wgranieNN-przed`
(np. `Armoury.dll.bak-2026-10-15-wgranie61-przed` = wersja sprzed wgrania 61) i druga kopie na
`D:\Backup-Bannerlord\wgrane\<data>-wgranieNN-przed`.

1. Zamknij gre.
2. Zmien nazwe obecnej `<Mod>.dll` (np. na `<Mod>.dll.zla`).
3. Skopiuj kopie `.bak-...-przed` jako `<Mod>.dll`. Dla CrashScribe tak samo `CrashScribe.settings.xml` (kopie w `ModuleData`).
4. **Przed wczytaniem kampanii Ironman na starszej wersji zapytaj Claude** - nowsza wersja moze miec w zapisie dane, ktorych
   starsza nie zna. Kopie zapisow kampanii sa w `D:\Backup-Bannerlord\zapisy-jeffa`.
