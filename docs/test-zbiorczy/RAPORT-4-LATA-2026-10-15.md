# Test 4 lata (wersja 54) - raport dla Jeffa, 15.10.2026

Bieg: nowa kampania na WGRANIU 54 (fin34 = `w-toku/zbior-e8` b9cb462e), autotest. Cz. 1 doby 0-767 z religia BK (wywrotka w d767 - patrz rozdz. 5),
cz. 3 od zapisu d720 bez religii do d1458 (zatrzymane na prosbe Jeffa "koncz raport na 4 latach"). Logi: `D:\Backup-Bannerlord\logs\2026-10-15-6lat`
(Armoury i CrashScribe obu czesci, tabele cz. 3 w `tabele-cz3`, economy CSV). Armoury: 0 ERROR w obu czesciach. Tempo ok. 13,5 s na dobe.
Tabela krolestw: skrypt `raport4/krolestwa.py` (balans-krolestw.csv + economy CSV, doba 1456). "Start" twierdz = stan z d720 (poczatek cz. 3).

## 1. Krolestwa na koniec 4. roku (doba 1456)

| Krolestwo | Twierdze d720 -> d1456 | Ludzie w druzynach | Zalogi | % wojska swiata | Rody | Zloto rodow | Skarbiec korony | Faktyczna zmiana zlota rodow / dobe |
|---|---|---|---|---|---|---|---|---|
| Baratheonowie z Krolewskiej Przystani (z Lannisterami) | 25 -> 47 | 19 741 | 19 750 | 46,9% | 69 | 36,1 mln | 0,50 mln | +16 046 |
| Reach | 18 -> 24 | 4 570 | 9 087 | 10,9% | 43 | 2,2 mln | 0,50 mln | -1 326 |
| Volantis | 7 -> 7 | 2 962 | 2 468 | 7,0% | 16 | 1,9 mln | 0,50 mln | -6 330 |
| Wolni Ludzie | 7 -> 6 | 2 443 | 1 819 | 5,8% | 16 | 1,4 mln | 1,10 mln | +2 229 |
| Targaryen (Aegon) | 3 -> 5 | 2 418 | 1 955 | 5,8% | 16 | 7,3 mln | 0,51 mln | +3 039 |
| Zelazne Wyspy | 10 -> 10 | 2 139 | 3 347 | 5,1% | 27 | 3,4 mln | 0,50 mln | -1 192 |
| Dorzecze | 11 -> 6 | 1 784 | 1 628 | 4,2% | 36 | 7,5 mln | 0,50 mln | -25 965 |
| Qarth | 4 -> 4 | 844 | 813 | 2,0% | 7 | 1,4 mln | 0,54 mln | -3 893 |
| Nocna Straz | 5 -> 2 | 685 | 948 | 1,6% | 3 | 7,0 mln | 0 | -225 |
| Smocza Skala (Stannis) | 20 -> 2 | 669 | 825 | 1,6% | 43 | 2,3 mln | 0,50 mln | +1 648 |
| Pentos | 5 -> 4 | 661 | 659 | 1,6% | 13 | 3,0 mln | 0,50 mln | -9 088 |
| Dorne | 15 -> 20 | 524 | 1 573 | 1,2% | 40 | 4,4 mln | 0,51 mln | -5 118 |
| Dothrakowie | 6 -> 6 | 305 | 360 | 0,7% | 22 | 4,4 mln | 0,62 mln | -3 756 |
| Dolina | 13 -> 17 | 286 | 1 296 | 0,7% | 37 | 3,3 mln | 0,52 mln | -12 511 |
| Polnoc | 23 -> 12 | 106 | 112 | 0,3% | 55 | 3,6 mln | 0,53 mln | -146 |
| Targaryen (Daenerys) | 8 -> 4 | 41 | 562 | 0,1% | 15 | 1,7 mln | 0,52 mln | -2 339 |
| pozostale 14 (Wolne Miasta, Ibben, Lorath, Sarnor, Wyspy Letnie, Skagos, Yi Ti...) | | ponizej 300 kazde | | | | | | |

Swiat: ludzie w druzynach 42 048, zalogi 49 952, zloto rodow 101,7 mln, skarbce koron 14,4 mln.

## 2. Wnioski - prostym jezykiem

1. **Krolewska Przystan urosla do 47 twierdz i 47% wojska swiata - glownie przez FABULE ROT** (Krwawe Gody: Boltonowie, Freyowie i sojusznicy
   przechodza do Lannisterow z Winterfell, Dreadfort, Barrowton, Last Hearth, The Twins...; po Czarnym Nurcie rody Stannisa ida do Reach, Dorne, Doliny).
   Szturmem AI nie zdobylo w 4 lata ANI JEDNEGO MIASTA (d0-767: 61 oblezen miast, 0 zdobytych; d720-1229: 16 + 7 stolic, 0 zdobytych);
   szturmem padaly tylko zamki (16 w 2 lata, potem 3).
2. **Polnoc prawie znikla jako sila** (106 ludzi w druzynach) - fabula (Krwawe Gody) i brak odbijania twierdz.
3. **Wojsko swiata spada:** druzyny lordow w wojnie (W5-7) 64,1 tys. po 2 latach -> **37,7 tys. po 4 latach** (cel 85 tys.).
4. **Pieniadz:** obieg jest zamkniety (zloto nie bierze sie z niczego - ksiega rodow liczy kazde rozliczenie), ale **plynie w jedna strone**:
   rody placa zold (dziennie ok. 270 tys. druzyny + 150 tys. zalogi), a zloto osiada w **kiesie ludu**: 37,4 mln w d720 -> **51,0 mln** w d1456;
   kasy miast 11,2 -> 7,8 mln; zloto rodow ok. 110 -> 102 mln. Rody biednieja, wiec trzymaja mniej wojska (pkt 3). To jest glowna sprawa do strojenia ekonomii.
5. **Daenerys** stracila tron: BK zmienil Targaryenom ustroj na republike i w corocznych wyborach krolem zostal Barristan Selmy (d366), potem jej rod odszedl
   w buncie (d1243). **NAPRAWIONE** (galaz `w-toku/daenerys` 3a980d6a, klucz StoryThroneGuard) - do testu.
6. **Joffrey zyje po Purpurowych Godach:** wydarzenie wyskoczylo (d1083), ale skutek (smierc Joffreya, Cersei na czele rodu) sie nie wykonal - do zbadania.
7. **Inni:** Nocny Krol zyje, 4 bandy, ok. 1280 trupow, 6 osad za Murem; do inwazji brakuje 19 osad (trzymaja je Wolni Ludzie) - inwazja pozno (po 4. roku).
8. **Bandy:** start 5,8 tys. w 320 bandach, po 2 latach 23,6 tys. w ok. 1130 (jak w poprzednim tescie).
9. **Armie (zmiana AC):** spojnosc spada 4,3 raza wolniej (zgodnie z zasada), ale armie i tak rozpadaja sie srednio po ok. 28 dniach - bo odchodza z nich
   druzyny (NotEnoughParty ok. 4450 w 2 lata), nie przez spojnosc.
10. **Fabula w tempie:** Czarny Nurt 301 d40, Krwawe Gody 301 d240, Purpurowe Gody 301 d356, atak na Mur 302 - Jeff: "jest dobrze".

## 3. W toku / do zrobienia (stan 15.10 rano)

- **Oblezenia** (Jeff "napraw oblezenia"): wf_295da547-718, galaz `w-toku/oblezenia-2` - trebusze AI pod miastem i stolica, glod i kapitulacja,
  drabiny AI przy przewadze >= 6:1 po >= 30 dobach, odsiecz bez zmian.
- **Daenerys:** zrobione (3a980d6a), do scalenia i testu.
- **Wywrotka Blackwater:** zrobione (b1cb26a8, RotStoryArmies) - test: wczytanie d720 z religia ON (55 dob), potem religia z powrotem ON.
- **Joffrey / skutki wydarzen fabularnych:** do zbadania (czy inne wydarzenia tez gubia skutki).
- **Ekonomia:** zloto osiada w kiesie ludu, wojsko spada do 38 tys. - strojenie (najwiekszy temat po oblezeniach).
- Drobne: AUD-P1-5 (2 wsie bez tytulu), prog AB-10, D1-D10 (wydajnosc, MCM, instrukcja).

## 5. Wywrotka w d767 (dla porzadku)

Pierwotny wyjatek: NullReferenceException w `GatherArmyLogEntry` <- `GatherArmyAction.Apply` <- ROT `BlackwaterSiegeEvent.SetupSiegeAttackers`
(ROT zaklada, ze `Kingdom.CreateArmy` utworzy armie Stannisa; bramka BK albo gra moze odmowic). Okno raportu bledu BLSE potem samo wywraca gre.
Religia BK tylko zmienila los (bez niej armia akurat powstala). Naprawa b1cb26a8.
