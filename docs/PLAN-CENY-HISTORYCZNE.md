# Plan: caly swiat w wartosciach historycznych (2026-10-04)

Jeff 04.10: "no kurwa tak, przeliczamy caly swiat do wartosci historycznych".
Klucz: 1 denar = 1 pens angielski (12 d = 1 s, 240 d = 1 L). Zrodla: docs/CENY-HISTORYCZNE.md
(baza Clarka/Rogersa 1300-1349), spis gry: docs/SPIS-CEN-GRY.md.

## 1. Co juz jest historyczne (zostaje)

| Rzecz | Gra | Historia |
|---|---|---|
| Zold t1-t6 | 2-21 d/dzien | 1.5-24 d/dzien |
| Dochody rodow, krol | mediana ~1.4 tys./dzien, krol ~300 tys. w skarbcu | baron 200-500 L/rok, korona ~30 tys. L/rok |
| Konie, bydlo | juczny ~100, rumak ~10 tys. | 60-160 d, destrier 40-100 L |
| Wiekszosc towarow handlowych | 1-10x historii | - |

## 2. Co jest przesadzone (do przeliczenia)

| Rzecz | Przyczyna | Przebicie |
|---|---|---|
| Bron, zbroje, tarcze, amunicja | wzor gry 100-120 x 2.75^tier (t1 -> t6: x157) | 10-200x |
| Sztabki, wegiel (nasz MaterialLaw) | lancuch od drewna i rudy w skali gry | 60-100x |
| Ruda, drewno | ceny gry 5 i 2.5 d/kg | 50-100x |
| Piwo, wino, deski, ceramika | ceny gry | 8-30x |
| Welna, jedwab, aksamit, przyprawy | ceny gry | za TANIE |
| Wegiel drzewny: waga 5 kg w grze, nasz lancuch liczyl 0.5 kg | blad zalozenia | 1 drewno (10 kg) -> 4 wegle = 20 kg |

## 3. Sposob

Wartosc (Value) przedmiotu ustawiana przy starcie gry (setter, jak MaterialLaw) - wtedy cena na targu,
lup, naprawa, XP kowalstwa, nagrody turniejowe i zamowienia ida za nia same.

- **Bron i zbroje: koszt historyczny** z naszego modelu ArmsPricing: metal (kg z wagi, strata 1.4) x cena
  historyczna gatunku + skora, len, drewno x ceny historyczne + dni pracy x dniowka mistrza wedle tieru
  (3 d na t1 do ~10 d na t6) x jakosc sztuki (0.6-1.8), zysk 25%. Unikaty: koszt x prestiz.
- **Surowce kuzni historycznie**: ruda ~0.05-0.1 d/kg, drewno ~0.035, wegiel ~0.07, zelazo kute 2.5,
  zelazo 3-4, stal ~6, stal szlachetna ~8; wegiel liczony ze swoja prawdziwa waga (5 kg), uzysk z drewna 20%.
  Male sztuki (sztabka 0.5 kg) dostaja wartosci 1-4 d - gra zna tylko liczby calkowite.
- **Popyt rynku liczony w zlocie** (kategorie zelaza, drewna) skalowany razem z wartoscia - inaczej
  wspolczynnik ceny gry (0.1-10) wybije pod sufit i ceny nie spadna (ostrzezenie ze spisu).
- **Stale kwoty** (nagrody questow, koszt karawany/warsztatu, cel kasy miasta, nasze ustawienia):
  osobny etap, po spisie stalych (docs/SPIS-CEN-GRY.md 2.1-2.3 do dopisania).

## 4. Ceny kontrolne (wyliczenie modelem, do strojenia z historia)

| Sztuka | Historia | Model (szacunek) | Dzis w grze |
|---|---|---|---|
| Tani miecz t1 (1 kg) | ~6 d | ~8 d | 550-800 |
| Luk t1 | 12-18 d | ~12 d | ~1 800 |
| Snop strzal (24) | ~15 d | do strojenia | 55 za stos |
| Bascinet (helm ~2.5 kg) | ~48 d | ~80 d | ~7 700 (t2) |
| Kolczuga (haubergeon ~9 kg) | 1-7 L (240-1600 d) | ~400 d | 1 500-2 300 |
| Pelna plyta t6 (25 kg) | 8-16 L (1920-3840 d) | ~1 600 d | ~40 000 |
| Komplet rycerza (1374) | 3920 d | ~2 500-4 000 d | ~71 000 (t5) |

## 5. Etapy (kazdy osobno: build, wgranie, CHANGELOG, commit; nowa gra zalecana)

1. Surowce kuzni historycznie + poprawka wagi wegla + skalowanie popytu kategorii zelaza i drewna.
2. Bron i zbroje: Value z kosztu historycznego (model ArmsPricing w trybie historycznym); XP kowalstwa
   liczone od starych wartosci (jak dzis przy sztabkach).
3. Towary przesadzone/za tanie (piwo, wino, deski, ceramika / welna, jedwab, przyprawy).
4. Stale kwoty gry i modow (po dokonczeniu spisu).
5. Nasze ustawienia w zlocie (progi legendy, kowal, praca jenca itd.).
