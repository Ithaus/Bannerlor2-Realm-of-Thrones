# CLAUDE.md — instrukcja dla Claude pracujacego nad tymi modami

Ten plik czyta Claude Code automatycznie po otwarciu repozytorium. Przeczytaj go
w calosci ZANIM cokolwiek zmienisz.

## 1. Kim jest uzytkownik i jak z nim rozmawiac

- **Jeff**, gra po polsku, **rozmawiaj po polsku**.
- **Wszystkie napisy w grze musza byc po ANGIELSKU** — nazwy opcji menu, komunikaty
  dla gracza, opisy ustawien MCM. Bez wyjatkow.
- Komentarze w kodzie: po polsku, **bez polskich znakow diakrytycznych**
  (`zuzycie`, nie `zużycie`) — tak jest w calym repo, trzymaj sie tego.
- Jeff testuje kazda zmiane w grze od razu. Jesli powiesz "naprawione", a nie jest —
  zauwazy w ciagu minuty. **Nie mow, ze cos dziala, dopoki tego nie zobaczysz w logu
  albo on nie potwierdzi.**

## 2. Srodowisko gry

- Bannerlord **1.4.8**, uruchamiany przez **BLSE**.
- Total conversion **Realm of Thrones 8.1.8** (ROT-Core / ROT-Content / ROT-Map / ROT-Dragon).
- Okolo 40 modow. Najwazniejsze dla nas: **BannerKings.Redux**, **BKROTPatch**, **RBM**,
  **RealisticBannerlord**, **BetterEconomy**, **DynamicTroopEquipmentReupload**,
  **NavalDLC**, **Spoils of War**, **UIExtenderEx**, **MCMv5**, **Harmony**.
- **Uwaga na kultury ROT**: ROT nadpisuje vanillowe id kultur.
  `battania` = **Polnoc (The North)**, `vlandia` = Westerlands/Reach itd.
  Wlasne id ROT to m.in. `wildlings`, `freefolk`, `nightswatch`, `skagosi`, `ghiscari`.
  Nigdy nie zakladaj, ze `battania` to Battania.

## 3. Nasze piec modow

| Mod | Co robi |
|---|---|
| **Armoury** | Kuznia (kucie pancerzy, lukow, kusz, amunicji), receptury i materialy, zuzycie sprzetu, obozowanie i sen, marsz kolumny, stajnie AI, dlugi rok, lupy, parowanie mistrza, lamanie sie rannych |
| **RealisticCaptivity** | Niewola, ucieczki, okup, praca jenca, domy klanowe, ucieczka konno ze spotkania |
| **GrandTourney** | Turnieje |
| **ForgeView** | UI zakladki CRAFT BannerKings (filtry, sortowanie wzorow) |
| **CrashScribe** | Diagnostyka: log bledow, latki na cudze mody (Mends), rozrusznik fabuly ROT, kronika wojen, spis notabli |

Kazdy ma wlasne ustawienia w MCM (Armoury ~200 pozycji). Ustawienia generuje
`tools/gen_mcm.py` z pol w `src/Settings.cs` — **po dodaniu ustawienia uruchom ten skrypt**,
inaczej nie pojawi sie w MCM.

## 4. Budowanie i wgrywanie

```bash
./build.sh                                  # wszystkie piec
dotnet build Armoury/Armoury.csproj -c Release -v q --nologo   # jeden
python3 tools/gen_mcm.py                    # po zmianie Settings.cs
```

Biblioteki gry — patrz `libs/README.md`. Wynik: `<Mod>/bin/Release/<Mod>.dll`.

Wgranie: podmien plik w
`...\Mount & Blade II Bannerlord\Modules\<Mod>\bin\Win64_Shipping_Client\<Mod>.dll`.
**Gra musi byc zamknieta** — inaczej plik jest zablokowany.

## 5. Gdzie sa logi (to jest twoje glowne narzedzie)

| Log | Sciezka |
|---|---|
| CrashScribe (bledy, kronika, fabula, notable) | `Documents\Mount and Blade II Bannerlord\CrashScribe\session-*.log` |
| Armoury (kuznia, stajnie, stamina) | `Modules\Armoury\Armoury.log` |
| RealisticCaptivity | `Modules\Armoury\RealisticCaptivity.log` |

**Zawsze czytaj log zamiast zgadywac.** CrashScribe wypisuje m.in.:
- `KRONIKA WOJEN` — kto z kim wojuje
- `FABULA: dzien N | czolo kolejki: X` — stan fabuly ROT
- `UMARLI: Nocny Krol ... N trupow w polu, M osad` — sily Innych
- `OSADA <nazwa> | kultura osady: X | notable: [imie / kultura / profesja]`
- `Mends: ...` — co zalatalismy w cudzych modach

## 6. Dekompilator (bez tego nie ruszaj cudzego kodu)

W repo go nie ma (to narzedzie, nie kod moda). Zbuduj sobie maly projekt na
**ICSharpCode.Decompiler** albo uzyj ILSpy/dnSpy. Wzorzec uzycia, ktory sie sprawdzil:

```
dec <plik.dll> list <fragment-nazwy>        # znajdz typy
dec <plik.dll> members <PelnaNazwaTypu>     # wypisz skladowe
dec <plik.dll> d <PelnaNazwaTypu>           # zdekompiluj
```

**Zawsze sprawdz w kodzie gry/moda, zanim napiszesz latke.** Polowa bledow w tym
projekcie wzieła sie z zalozenia zamiast sprawdzenia.

## 7. PUŁAPKI, ktore juz nas kosztowaly (czytaj uwaznie)

**`ItemObject.ItemTiers.Tier1 == 0`.** Wyswietlany tier to `(int)item.Tier + 1`.
Uzywaj `Recipes.Grade(item)`, nie rzutowania. Ten blad przez tydzien powodowal,
ze luki tieru 6 kuly sie z materialow tieru 5.

**Rok w Bannerlordzie ma 84 dni** (7 × 3 × 4 w `DefaultCampaignTimeModel`), a w trybie
`GameAccelerationMode.Fast` — **24 dni**. Armoury wydluza rok do 168 dni
(`Calendar.cs`, `WeeksPerSeason = 6`). Kazde przeliczenie "dni na lata" musi
mowic, o ktorym kalendarzu jest mowa.

**NIE WOLNO wolac `GameMenu.SwitchToMenu()` z wnetrza opcji menu oczekiwania.**
VM menu jest w polowie klatki i konczy sie to `NullReferenceException`
w `GameMenuVM.OnFrameTick` — czyli CTD. Uzywaj `GameMenu.ExitToLast()`.
Kosztowalo to caly wieczor crashy przy obozie.

**Globalne wylaczniki zabijaja wszystko.** `NightRest.Tent()` mial
`catch { _tentBroken = true; }` — jedna wywrotka na jednej partii gasila namioty
calemu swiatu na cala sesje. Jesli lapiesz wyjatek per-obiekt, **licz potkniecia,
nie gas funkcji**.

**Nie dotykaj wizerunkow na mapie co klatke.** `RemoveAllChildren()` +
`AddTentEntityForParty()` na partii gracza przy kazdym ticku = CTD.
Rob to raz, przy zmianie stanu.

**Ochotnicy ida wedle kultury NOTABLA, nie osady** (`BKVolunteerModel.GetPopTypeRecruit`
wola `GetPossibleSpawns(sellerHero.Culture, ...)`). Dlatego kaplan z obcej kultury
wystawia obce wojsko — patrz `docs/ERRORS.md`.

**Zawsze rob `cd` do katalogu projektu przed `dotnet build`** — inaczej MSB1003.

## 8. Zasady pracy (wyciagniete z bolesnych doswiadczen)

1. **Najpierw log, potem hipoteza, dopiero potem kod.** Jesli nie masz dowodu, powiedz
   ze nie masz, zamiast twierdzic.
2. **Jedna zmiana naraz.** Jeff testuje po kazdej. Trzy zmiany w jednym DLL = nie wiadomo,
   ktora zepsula.
3. **Zanim cos dodasz "przy okazji" — nie dodawaj.** Dwa najgorsze crashe w tym projekcie
   wzieły sie z kodu dopisanego "na wszelki wypadek", o ktory nikt nie prosil.
4. **Zanim zmienisz kod, ktory dotad byl martwy (bo wyjatek go wylaczal), sprawdz,
   ile pracy nagle zacznie wykonywac.** Naprawienie `_tentBroken` przy wlaczonym
   obozowaniu band = kilkaset namiotow na tick = CTD.
5. **Cofanie jest w porzadku.** Jak cos psuje gre, wroc do dzialajacej wersji i dopiero
   potem szukaj przyczyny. Jeff woli grac niz czekac.
6. **Nie ma tu gita z historia** (repo zaklada sie od zera) — rob commity po kazdej
   dzialajacej zmianie, zeby bylo do czego wracac.

## 9. OBOWIAZEK: dziennik zmian

Po **kazdej** zmianie w kodzie — nawet jednolinijkowej — **dopisz wpis na gorze
`CHANGELOG.md` i zrob commit + push**. To jest jedyny sposob, zeby drugie konto
Claude na drugim komputerze wiedzialo, co sie stalo, bez czytania calej rozmowy.

Wpis ma miec: **Problem** (objaw u gracza + dowod z logu), **Przyczyna** (co konkretnie
w kodzie), **Zmiana**, **Ryzyko / co sprawdzic**, **Status** (WGRANE / COFNIETE /
DO SPRAWDZENIA). Wzor jest na gorze `CHANGELOG.md`.

Jesli cos **cofasz** — tez wpis, ze statusem COFNIETE i powodem. Cofniecia sa rownie
wazne jak naprawy; bez nich drugie konto odbuduje ten sam blad.

Zanim zaczniesz prace na nowym komputerze: `git pull`, przeczytaj **pierwsze trzy wpisy**
w `CHANGELOG.md` i `docs/ERRORS.md`. To wystarczy, zeby wiedziec, gdzie jestesmy.

## 10. Otwarte sprawy

Patrz `docs/ERRORS.md` (bledy cudzych modow) i `docs/HISTORY.md` (co i dlaczego zrobilismy,
oraz lista rzeczy niedokonczonych).
