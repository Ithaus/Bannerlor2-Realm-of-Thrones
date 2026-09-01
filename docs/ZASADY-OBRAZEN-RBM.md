# PELNE ZASADY OBRAZEN (RBM) - co naprawde daje pancerz

Zrodlo: dekompilacja RBMCombat.dll (RBMComputeDamage, WeaponTypeDamage)
i RBMConfig.dll - wartosci DOMYSLNE configu (ArmorMultiplier=2,
ArmorThresholdModifier=1, BluntTraumaBonus=0). Stan na 01.09.2026.
Wszystkie liczby ponizej policzone DOKLADNYM wzorem z kodu.

---

## 1. Slowniczek

- **MAGNITUDE** - sila ciosu/pocisku zanim spotka pancerz. Rosnie
  z predkoscia pocisku, rozpedem zamachu, damage broni i skillem.
- **ARMOR** - suma punktow oslony trafionej czesci ciala, taka jak
  W TOOLTIPIE (RBM juz przeliczyl wartosci z XML w gore).
- **HP** - zolnierz ma ~100 zycia. Trafienie w glowe mnozy obrazenia
  (vanilla ~1.2-2x zaleznie od broni), w nogi oslabia.

## 2. Trzy skladniki kazdego trafienia

### A. PENETRACJA PELNA (prog twardy)
`pen = max(0, MAGNITUDE - ARMOR x ProgBroni)`
Pancerz to PROG, nie procent: dopoki magnitude nie przekroczy
ARMOR x ProgBroni, pelnej penetracji NIE MA WCALE.

### B. PENETRACJA CZESCIOWA (tylko klucia/groty, cap 15)
`czesc = min(max(0, MAGNITUDE - ARMOR x ProgCzesciowy), 15)`
Grot wbija sie plytko nawet w oslonietego. STRZALY i BELTY maja
ProgCzesciowy = 0, wiec ich czesciowa penetracja to zawsze
min(MAGNITUDE, 15). To jest zrodlo "wiecznych ~15-17 dmg" strzal.

### C. TEPY URAZ (procentowy)
`blunt = MAGNITUDE x WspBlunt x udzial_zatrzymany x 100/(100 + 2 x ARMOR)`
Energia zatrzymana przez pancerz i tak trzesie cialem. Tu pancerz
dziala PROCENTOWO: ARMOR 50 przepuszcza polowe, ARMOR 100 - 1/3.

**Laczne obrazenia = A + B + C.**

## 3. Reguly specjalne

1. **Strzaly/belty vs material**: cel w pancerzu INNYM niz plyta
   (skora, kolczuga, tkanina) liczy ARMOR x0.5 przeciw strzalom
   i beltom. Vs PLYTA armor liczy sie w PELNI. (pierwsza linia
   RBMComputeDamage)
2. **Obuchy (Blunt)**: prog penetracji staly ARMOR x5 (przebic sie
   niemal nie da), ale wspolczynnik tepego to az 0.7 - dlatego obuch
   ignoruje wiekszosc procentowej redukcji i "gniecie przez blache".
3. **Klucie broni recznej** (miecze/wlocznie sztychem): ProgCzesciowy=2,
   wiec plytkie klucie dziala tylko na slabo oslonietych.
4. **Tarcza/blok**: zablokowane trafienie w ogole nie wchodzi w te wzory.

## 4. Progi broni (z configu RBM, domyslne)

| Bron           | ProgPierce | ProgCut | WspBlunt(P) | WspBlunt(C) | ProgCzesc |
|----------------|-----------:|--------:|------------:|------------:|----------:|
| Strzala        |        2.0 |     2.6 |        0.15 |        0.15 |         0 |
| Belt           |        2.0 |     2.6 |        0.15 |        0.15 |         0 |
| Oszczep        |        3.0 |     3.0 |        0.20 |        0.05 |         2 |
| Sztylet        |        3.0 |     5.0 |        0.35 |        0.25 |         2 |
| Miecz 1h/2h    |        3.5 |     5.0 |        0.35 |        0.25 |         2 |
| Topor 1h/2h    |        2.5 |     5.0 |     0.25-0.3|         0.3 |         2 |
| Wlocznia 1h/2h |        3.0 |     5.0 |        0.35 |        0.30 |         2 |
| Obuch 1h/2h    | (blunt: 5.0)|    4.0 |        0.25 |        0.10 |         0 |
| Proca          |        6.0 |    10.0 |        0.35 |        0.30 |         0 |

Czytanie: strzala PRZEBIJA NA WYLOT dopiero, gdy magnitude > ARMOR x2.
Miecz przetnie zbroje dopiero, gdy magnitude > ARMOR x5 - praktycznie
nigdy przy 40+ pancerza.

## 5. TABELA OBRAZEN (dokladny wzor, domyslny config)

Obrazenia LACZNE (pen + czesciowa + blunt) wg pancerza trafionej czesci:

| Atak \ Pancerz         |  0 | 15 | 30 | 50 | 70 | 90 |
|------------------------|---:|---:|---:|---:|---:|---:|
| Strzala 35 vs skora/kolczuga | 35 | 22 | 17 | 17 | 17 | 17 |
| Strzala 35 vs PLYTA    | 35 | 17 | 17 | 16 | 16 | 16 |
| Belt 55 vs skora/kolczuga | 55 | 42 | 28 | 19 | 19 | 18 |
| Belt 55 vs PLYTA       | 55 | 28 | 19 | 18 | 18 | 17 |
| Oszczep 60             | 60 | 22 |  8 |  6 |  5 |  4 |
| Miecz 1h ciecie 40     | 40 |  8 |  6 |  5 |  4 |  4 |
| Topor 2h ciecie 55     | 55 | 13 | 10 |  8 |  7 |  6 |
| Obuch 35               | 35 | 19 | 15 | 12 | 10 |  9 |

Wnioski liczbowe:
- **Strzala w helm 50+ = ~16-17 dmg** -> ~6 strzal na 100 HP (w glowe
  z mnoznikiem ~4-5). TAK MA BYC w RBM - to nie usterka.
- **Belt** trzyma sile do ~30 pancerza i bije strzale wszedzie.
- **Ciecie kona przy 15+ pancerza** - miecz na golasow i do sztychu.
- **Obuch** jako jedyny rosnie WZGLEDEM innych wraz z pancerzem celu:
  przy 70-90 pancerza bije miecz 2x.
- **Goly cel** umiera od wszystkiego w 2-3 trafienia - dlatego wczoraj
  nadzy bandyci padali od jednej strzaly, a dzis, w helmach, potrzeba
  kilku. Roznice zrobil ich UBIOR, nie nasze obrazenia.

## 6. Praktyka: czym bic kogo

| Cel                     | Najlepiej                  | Unikaj            |
|-------------------------|----------------------------|-------------------|
| Golas / tkanina         | wszystko; ciecia najszybsze| -                 |
| Skora / kolczuga        | BELT, strzala, oszczep     | ciec przez oslone |
| Plyta (helm, kirys)     | OBUCH, belt, sztych w luki oslony | strzaly w kirys, ciecia |
| Kon (nieopancerzony)    | wlocznia/oszczep, strzaly  | -                 |

Lucznik na pancernych: celuj w NIEOSLONIETE czesci (twarz bez pelnego
helmu, rece, nogi) - tam ARMOR = 0 i strzala wraca do pelnej sily.

## 7. Gdzie sie to stroi

- **RBM MCM -> Combat -> Global**: ArmorMultiplier (2 - sila czesci
  procentowej), ArmorThresholdModifier (1 - sila WSZYSTKICH progow;
  obnizysz = pancerz slabszy globalnie), BluntTraumaBonus (0),
  wspolczynniki per bron (WeaponTypes).
- **Nasze prawa**: Prawo Rozsadku Pancerza (percentyl 75 x1.3 w grupie
  typ+tier - rowna tylko wystajace absurdy, patrz CHANGELOG 01.09);
  HitScribe pisze kazde trafienie pociskiem do Armoury.log
  ("HIT klasa -> ofiara [czesc ciala] dmg= wchloniete= HPpo=").

## 8. Zastrzezenia

- RBM przelicza wartosci pancerzy z XML przy starcie gry - wszystkie
  progi licza sie na liczbach Z TOOLTIPA, nie z plikow ROT.
- Tabele policzone dla domyslnego configu RBM; jesli suwaki w MCM RBM
  byly ruszane, liczby sie przesuna (kierunki i mechanika - nie).
