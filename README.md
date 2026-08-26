# Mody do Bannerlorda — Realm of Thrones

Piec modow pisanych pod jedna kampanie: **Bannerlord 1.4.8 + Realm of Thrones 8.1.8**,
okolo 40 modow, BLSE.

| Mod | Krotko |
|---|---|
| **Armoury** | kuznia, receptury, zuzycie sprzetu, oboz i sen, marsz, stajnie AI, dlugi rok, lupy, walka |
| **RealisticCaptivity** | niewola, ucieczki, okup, praca jenca, domy klanowe |
| **GrandTourney** | turnieje |
| **ForgeView** | UI zakladki CRAFT BannerKings |
| **CrashScribe** | log bledow, latki na cudze mody, rozrusznik fabuly ROT, raporty o swiecie |

## Zanim cokolwiek zrobisz

- **[CLAUDE.md](CLAUDE.md)** — instrukcja dla Claude: srodowisko, zasady, **pulapki**
- **[CHANGELOG.md](CHANGELOG.md)** — dziennik zmian, **uzupelniany po KAZDEJ zmianie**
- **[docs/ERRORS.md](docs/ERRORS.md)** — bledy nasze i cudze, jak czytac logi
- **[docs/HISTORY.md](docs/HISTORY.md)** — po co to wszystko, stan swiata, co niedokonczone

## Budowanie

```bash
# 1. wrzuc biblioteki gry do libs/  (patrz libs/README.md)
# 2.
./build.sh
```

Wynik: `<Mod>/bin/Release/<Mod>.dll` → podmien w
`Modules\<Mod>\bin\Win64_Shipping_Client\`. **Gra musi byc zamknieta.**

Po dodaniu ustawienia do `src/Settings.cs` uruchom `python3 tools/gen_mcm.py`,
inaczej nie pojawi sie w MCM.

## Logi

```
Documents\Mount and Blade II Bannerlord\CrashScribe\session-*.log
Modules\Armoury\Armoury.log
```
