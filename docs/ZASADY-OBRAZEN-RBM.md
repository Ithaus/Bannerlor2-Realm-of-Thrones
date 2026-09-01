# Jak licza sie obrazenia (RBM) - i co naprawde daje pancerz

Zrodlo: dekompilacja RBMCombat.dll (Utilities.RBMComputeDamage,
WeaponTypeDamage) + RBMConfig.dll. Stan na 01.09.2026, wartosci domyslne
configu RBM (ArmorMultiplier=2, ArmorThresholdModifier=1).

## Trzy skladniki kazdego trafienia

Kazdy cios/pocisk niesie MAGNITUDE (sile z rozpedu, predkosci pocisku,
wagi broni). RBM dzieli ja na:

1. **PENETRACJA PELNA** - to, co przebija pancerz na wylot:
   `pen = max(0, magnitude - Armor x FactorTypuBroni)`
   Armor dziala tu jak PROG: dopoki magnitude nie przekroczy progu,
   pelnej penetracji NIE MA wcale.

2. **PENETRACJA CZESCIOWA (tylko PIERCE)** - grot, ktory wchodzi plytko:
   `czesc = min(magnitude - Armor x ProgCzesciowy, 15)`
   Dla STRZAL i BELTOW ProgCzesciowy = 0, wiec strzala, ktora w ogole
   dosiegla ciala, wbija sie zawsze - ale NAJWYZEJ za 15 obrazen.

3. **TEPY URAZ (blunt trauma)** - energia zatrzymana przez pancerz
   i przekazana w cialo:
   `blunt = magnitude x WspolczynnikTepy x udzial_zatrzymany x 100/(100 + 2 x Armor)`
   Tu pancerz dziala PROCENTOWO: Armor 50 przepuszcza polowe tepego,
   Armor 100 - jedna trzecia.

Laczne obrazenia = pen + czesc + blunt.

## Najwazniejsze reguly specjalne

- **Strzaly vs material pancerza**: gdy cel ma pancerz INNY niz plyta
  (skora, kolczuga, tkanina), strzaly i belty licza jego Armor x0.5 -
  grot szyje miekkie oslony. Vs PLYTA armor liczy sie w pelni.
  (pierwsza linia RBMComputeDamage)
- **Maczugi/obuchy (Blunt)**: prog penetracji = Armor x5 (przebic sie
  trudno), ale tepy uraz przechodzi wyjatkowo dobrze - dlatego obuch
  to bron na puszki.
- **Ciecie (Cut)**: prog penetracji wysoki (FactorCut per bron) - miecz
  slabo tnie przez metal, za to golego rozbiera z HP natychmiast.

## Co z tego wynika w praktyce (przyklad: strzala ~35 magnitude)

- **Goly cel**: pen ~35 + blunt = smierc w 2-3 strzaly, w glowe czesto 1.
- **Skorznia 20 pkt** (liczona x0.5 = 10): pen = 35-10 = 25 + blunt -> boli.
- **Helm plytowy 60 pkt**: pen pelna = max(0, 35-60) = 0;
  czesciowa = 15 (cap); blunt = male procenty przez 100/(100+120)=~45%.
  RAZEM ~18-20 na strzale -> **4-6 strzal w helm to ZAMYSL RBM**, nie blad.
  Chcesz szybciej zabijac pancernych lucznikiem? Celuj w twarz/konczyny
  bez oslony albo bierz belty (wyzsze magnitude) badz obuch.

## Gdzie sie to stroi

- RBM MCM: ArmorMultiplier (dom. 2 - sila procentowej czesci pancerza),
  ArmorThresholdModifier (dom. 1 - sila progow penetracji),
  wspolczynniki per typ broni (FactorCut/Pierce, ExtraBlunt).
- Nasze prawa (Armoury/CrashScribe): Prawo Rozsadku Pancerza rowna tylko
  WYSTAJACE sztuki do normy (percentyl 75 x1.3 w grupie typ+tier), wiec
  progi RBM dzialaja na uczciwych liczbach; HitScribe loguje kazde
  trafienie pociskiem (dmg, wchloniete, czesc ciala) do Armoury.log.

## Uwaga o wartosciach pancerza

RBM przelicza pancerze takze na ekranie (to dlatego Dothraki Chaps
pokazywaly 55, gdy XML ROT mowi 25) - wszystkie progi wyzej licza sie
na wartosciach PO tym przeliczeniu, czyli na tych, ktore widzisz
w tooltipie.
