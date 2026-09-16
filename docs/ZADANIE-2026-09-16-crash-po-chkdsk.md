# ZADANIE 2026-09-16 - crash przy starcie po chkdsk (do wykonania NA KOMPUTERZE Z GRA)

Ten plik to tresc karty zadania z sesji zdalnej (chmurowej) z 16.09, ktora nie miala dostepu
do dysku C. Sesja lokalna (Claude Code na Windowsie, w folderze repo) ma go przeczytac i wykonac.

Przeczytaj najpierw `CLAUDE.md` (rozmawiaj po polsku, napisy w grze po angielsku, komentarze
w kodzie bez polskich znakow, nie mow "naprawione" bez dowodu z logu, wpis w `CHANGELOG.md`
+ commit + push po kazdej zmianie).

## Sytuacja (relacja Jeffa)

Sciezki:
- gra: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
- configi: `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\Configs`
- logi gry: `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\logs`
- logi CrashScribe: `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\session-*.log`
- BLSE, .NET Framework 4.8, Windows 11 build 26200. Launcher BLSE jest ZAMKNIETY.

Co sie stalo:
1. Dysk C mial uszkodzone indeksy NTFS, `chkdsk /spotfix` je naprawil (0 bad sectors).
   `LauncherData.xml` zostal przez to zresetowany (0 zaznaczonych modow).
2. Jeff przywrocil `LauncherData.xml` z `LauncherData.xml.bak-2026-09-03-ai` (36 zaznaczonych)
   i dopisal NA KONCU jako zaznaczone: AIInfluence, ROT_AIInfluence_Compat, VoiceActingPatch.
   Razem 45 wpisow, 39 zaznaczonych. Kopia stanu sprzed przywrocenia:
   `LauncherData.xml.bak-2026-09-16-po-chkdsk`.
3. Gra crashuje przy starcie/wczytaniu (okno BUTR "Bannerlord has encountered a problem").
4. Jeff twierdzi, ze poprzednia sesja zbudowala latke w CrashScribe (zold najemnikow przy
   ujemnym wplywie), zrobila commit, ale NIE wgrala DLL do Modules. To sie NIE zgadza
   z GitHubem - patrz Ustalenia A.

## Ustalenia sesji zdalnej (z GitHuba) - sprawdz lokalnie, nie zakladaj

A. Latka na ujemny zold najemnika (`CrashScribe/src/Mends.cs`, `MercenaryWageFloor`, patch na
   `DefaultClanFinanceModel.AddMercenaryIncome`) dodana 13.09 (8644407), 14.09 WYWROCILA
   wczytywanie zapisu (`TypeInitializationException` w `DefaultClanFinanceModel` - cctor siega
   do `Game.Current`, ktore na sciezce ROT `RealmOfThronesGameModeManagerPatch.Prefix ->
   MapScreen..ctor` jeszcze nie stoi) i tego dnia COFNIETA (520396d, wpis CHANGELOG
   "COFNIETE: latka na ujemny zold najemnika"). Ostatni wypchniety commit na
   `claude/bannerlord-rot-setup-o75kvo` to a416562 (15.09); kazdy build CrashScribe z 15.09 ma
   w CHANGELOG md5 zgodne z gra. Na GitHubie NIE MA commitu z 16.09. Sprawdz: `git status`,
   `git log -5`, lokalne niewypchniete commity, czy `Mends.cs` ma `harmony.Patch(mMerc, ...)`
   wlaczony.

B. Kolejnosc launchera z 03.09 (wpis CHANGELOG "AI Influence 6.0.2 + Voice Acting Patch"):
   AIInfluence i VoiceActingPatch TUZ ZA ROT-Dragon, ROT_AIInfluence_Compat na samym koncu.
   Kopia `.bak-2026-09-03-ai` byla robiona PRZED dopisaniem tych trzech. Id modulow bierz
   z `SubModule.xml` (folder `VoiceActingPatchRemake` moze miec inne Id niz nazwa folderu).

C. Dysk C (PNY CS3140, 5% zycia, wpis CHANGELOG 01.09 "DIAGNOZA OSTATECZNA") gubi ogony
   zapisow: plik w calosci z zer albo zera od granicy wielokrotnosci 4096 B. 26.08 i 01.09
   dalo to "Cannot load Armoury.dll" i dziure w `Configs\RBM\config.xml`. NAJPIERW szukaj
   dziur z zer w DLL zaznaczonych modow, `SubModule.xml` i `Configs\*`, potem kolejnosci.
   Mirror z dobrymi kopiami: `D:\Backup-Bannerlord\Modules` i `...\Documents`
   (`tools/backup-bannerlord.ps1`, stare wersje w `graveyard`).

## Skrypt diagnostyczny

`tools/diag-start-crash.ps1` (galaz `claude/brave-meitner-rhef41` = a416562 + commit ze
skryptem) - TYLKO CZYTA: raporty BUTR html/zip (wyjatek, Involved Modules, Enhanced
Stacktrace), ogon rgl_log, ModLogs od dzis, session-*.log CrashScribe, Armoury.log,
LauncherData (kolejnosc, zaznaczenia, kopie, zera), SubModule.xml wszystkich modulow z kontrola
zaleznosci (DependedModules / DependedModuleMetadatas, LoadBefore/After, DLL z <SubModules>),
skan zer w DLL/SubModule.xml/Configs/swiezych plikach Modules z informacja o kopii na D:,
md5 naszych 5 DLL gra vs repo + git log/status, dziennik Windows (NTFS 55/98/130/140, bledy
aplikacji). Wynik: `Documents\...\CrashScribe\diag-start-<data>.txt`.

Pobranie:
```
git fetch origin claude/brave-meitner-rhef41
git merge --ff-only origin/claude/brave-meitner-rhef41
```
Jesli ff-only nie przejdzie (lokalne commity): `git checkout origin/claude/brave-meitner-rhef41
-- tools/diag-start-crash.ps1 CHANGELOG.md docs/ZADANIE-2026-09-16-crash-po-chkdsk.md`
i rozwiaz CHANGELOG recznie (wpis z 16.09 na gorze zostaw).
Uruchom: `powershell -ExecutionPolicy Bypass -File tools\diag-start-crash.ps1`. Skrypt NIE byl
nigdy uruchomiony (pisany bez PowerShella) - blad skladni popraw, uruchom ponownie, poprawke
zacommituj.

## Zadania, po kolei

A. Z raportu skryptu (albo recznie): najnowszy raport BUTR (Pulpit/Documents/Downloads,
   *.html/*.zip; jesli okno BUTR jeszcze wisi, popros Jeffa o "Save Report" na Pulpit),
   najnowszy `rgl_log_*.txt`, dzisiejsze pliki w `Configs\ModLogs`, najnowszy `session-*.log`
   CrashScribe. Wyciagnij wyjatek, stacktrace, "Involved Modules". Powiedz Jeffowi, ktory mod
   i dlaczego. `TypeInitializationException` + `DefaultClanFinanceModel` = sygnatura z 14.09,
   czyli w Modules lezy build z WLACZONA latka zoldu. "Cannot load ...dll" /
   `BadImageFormatException` / `XmlException ... 0x00` = dysk (wzorzec C).

B. Zweryfikuj `LauncherData.xml`: Harmony/ButterLib/UIExtenderEx/MCM na gorze; moduly ROT
   (ROT-Core, ROT-Content, ROT_Map, ROT-Dragon) przed swoimi latkami (ROTFinishNullFix,
   RoyalArmouryFix, VoiceActingPatch, ROT_AIInfluence_Compat); ROT_AIInfluence_Compat po
   AIInfluence. Kazdy zaznaczony mod ma folder w Modules o tym Id? Jego `SubModule.xml` nie
   wymaga (DependedModule bez Optional / DependedModuleMetadata bez optional="true") czegos
   wylaczonego? Sekcja 7 raportu to liczy - zweryfikuj pozycje BLAD/KOLEJ.

C. Zaproponuj poprawke Jeffowi w dwoch zdaniach i wgraj ja. ZASADY: kazda zmiana w Configs
   z kopia `<plik>.bak-2026-09-16-<opis>` PRZED zmiana; jedna zmiana naraz; plik uszkodzony
   przez dysk podmieniaj z `D:\Backup-Bannerlord` po sprawdzeniu, ze kopia jest zdrowa
   (naglowek MZ, ogon nie z zer, `[System.Reflection.AssemblyName]::GetAssemblyName()`
   przechodzi dla DLL .NET; XML parsuje sie), stara odloz jako `<plik>.corrupt-2026-09-16`;
   nasze 5 DLL (Armoury, CrashScribe, RealisticCaptivity, GrandTourney, ForgeView) mozna wziac
   z `<Mod>\bin\Release` w repo albo zbudowac (`dotnet build <Mod>/<Mod>.csproj -c Release`,
   `cd` do katalogu projektu). Kolejnosc modow zmieniaj tylko, jesli dowod na to wskazuje;
   wtedy wzorzec z 03.09 (Ustalenia B). Po zmianie popros Jeffa o uruchomienie gry i sprawdz
   w nowym `session-*.log` CrashScribe i `rgl_log`, ze wstala - dopiero wtedy mow, ze dziala.

D. DOPIERO gdy gra wstaje: rozstrzygnij CrashScribe.dll. Porownaj md5
   `Modules\CrashScribe\bin\Win64_Shipping_Client\CrashScribe.dll` z
   `CrashScribe\bin\Release\CrashScribe.dll` i z md5 z ostatnich wpisow CHANGELOG (15.09).
   Sprawdz lokalny `git log`/`git status`. Jesli lokalny build ma PONOWNIE WLACZONY
   `harmony.Patch` na `AddMercenaryIncome` w postaci z 13.09 - NIE WGRYWAJ (crash z 14.09
   wroci); zaproponuj bezpieczna droge z wpisu COFNIETE (inne miejsce zaczepienia albo
   `RuntimeHelpers.RunClassConstructor(typeof(DefaultClanFinanceModel).TypeHandle)`
   w `OnSessionLaunched`, gdy `Game.Current` istnieje, i dopiero potem patch). Jesli lokalny
   build = a416562 (latka wylaczona) i md5 w grze sie zgadza - nie ma czego wgrywac, napisz
   to wprost. Niewypchniete lokalne commity wypchnij (`git push -u origin <galaz>`).

Na koniec: wpis na gorze `CHANGELOG.md` (Problem z dowodem z logu, Przyczyna, Zmiana, Ryzyko,
Status WGRANE/COFNIETE/DO SPRAWDZENIA), commit, push. Jesli czegos nie ma w logach - napisz,
jakiego pliku lub komendy brakuje, zamiast zgadywac.
