# Model materialow: ruda, wegiel, sztabki, wykucie (2026-10-04, PROPOZYCJA - nic nie zmienione)

Zamowienie Jeffa: "waga jednej sztabki to 0.5 kg surowca, wiec trzeba najpierw ustalic, ile to jest
sztaba rudy zelaza itp., ile z tego mozna wykuc i ile czasu to zajmuje, aby ustalic, ile trzeba
podniesc wydobycie kopaln itp. i ceny do tego."

## 1. Jednostki w grze (items-dump.csv, ROT 8.1.8)

| Towar | Waga jednostki | Cena | Cena za kg |
|---|---|---|---|
| Ruda zelaza (iron) | 10 kg | 50 | 5 |
| Drewno (hardwood) | 10 kg | 25 | 2.5 |
| Wegiel drzewny (charcoal) | 0.5 kg | 50 | **100** |
| Surowka (ironIngot1, Crude Iron) | 0.5 kg | 20 | 40 |
| Zelazo kute (ironIngot2) | 0.5 kg | 30 | 60 |
| Zelazo (ironIngot3) | 0.5 kg | 60 | 120 |
| Stal (ironIngot4) | 0.5 kg | 100 | 200 |
| Stal szlachetna (ironIngot5) | 0.5 kg | 160 | 320 |
| Stal valyrianska (ironIngot6) | 0.5 kg | 260 | 520 |

Przetopy vanilla (`DefaultSmithingModel.GetRefiningFormulas`, 1.4.8):

| Przepis vanilla | Masa wejscia -> wyjscia |
|---|---|
| 2 drewna -> 1 wegiel | 20 kg -> 0.5 kg (1:40) |
| 1 ruda + 1 wegiel -> 2 surowki (3 z perkiem) | 10.5 kg -> 1 kg |
| 1 surowka + 1 wegiel -> 1 zelazo kute | 1 kg -> 0.5 kg |
| 2 zelaza kutego + 1 wegiel -> 1 zelazo + 1 surowka | ... kolejne stopnie tak samo, do stali valyrianskiej |

Wydobycie vanilla: kopalnia zelaza 10 rudy/dzien (100 kg), drwale 18 drewna/dzien (180 kg).

## 2. Historia (Europa XIII-XIV w., przyblizenia z literatury o dymarkach i kowalstwie)

| Etap | Proporcja historyczna | W jednostkach gry |
|---|---|---|
| Wegiel z drewna | 1 kg wegla z 4-7 kg drewna | 1 drewno (10 kg) -> ok. 4 wegla (2 kg) |
| Dymarka: ruda -> lupa (surowka) | 1 kg zelaza z 5-10 kg dobrej rudy, przy 1-2 kg wegla na kg rudy | 1 ruda + ok. 20 wegla -> ok. 3 surowki (1.5 kg) |
| Lupa -> zelazo kute (zageszczanie, kucie) | strata 30-50% | 3 surowki + 2 wegla -> 2 zelaza kutego |
| Kolejne stopnie stali (nawegl., zgrzewanie) | strata 20-30% na stopien, wiecej wegla | 3 sztabki nizsze + 2-4 wegla -> 2 wyzsze |
| Wykucie przedmiotu | metal w sztabie = 1.3-1.5 x waga gotowego przedmiotu | np. miecz 1.2 kg -> ok. 3.5 sztabki |

## 3. Ile metalu na wyrob (mediana wagi z gry, x1.4 strat kucia)

| Wyrob | Waga (mediana) | Sztabek 0.5 kg |
|---|---|---|
| Miecz 1h t3 | 1.14 kg | ok. 3 |
| Helm t3 / t6 | 1.5 / 3 kg | ok. 4 / 8 |
| Korpus t3 (kolczuga) | 10 kg | ok. 28 |
| Korpus t5 / t6 (plyta) | 18 / 33 kg | ok. 50 / 92 |
| Grot strzaly | 0.01-0.02 kg | 1 sztabka = ok. 25-50 grotow |
| Tarcza t3 | 4.7 kg, glownie drewno | 1-2 sztabki okuc + drewno |

## 4. Czas pracy kowala (warsztat: mistrz + pomocnik, przyblizenia)

| Wyrob | Czas |
|---|---|
| Groty strzal | 50-100 sztuk dziennie |
| Grot wloczni, siekiera | 0.5-1 dnia |
| Prosty miecz | 2-3 dni |
| Dobry miecz | 1-2 tygodnie |
| Prosty helm / bascinet | 2-3 dni / ok. tygodnia |
| Kolczuga (haubergeon / hauberk) | 1-2 / 2-3 miesiace |
| Brygantyna / plaszcz plyt | ok. 2 tygodni |
| Pelna zbroja plytowa (helm, korpus, rece, nogi) | kilka tygodni pracy warsztatu kilku ludzi |
| Wytop w dymarce | 1 dzien: 50-100 kg rudy -> 5-15 kg lupy |

## 5. Co z tego wynika dla wydobycia

Kopalnia 10 rudy/dzien = 100 kg rudy = ok. 15 kg zelaza = ok. 1.5 kompletu zolnierza t3 (kolczuga,
helm, miecz ~ 13 kg wyrobu, ~18 kg sztab, ~120 kg rudy) dziennie. 26 kopaln x 10 = ok. 40 kompletow t3
dziennie na caly swiat - przy armiach po kilkaset ludzi i stratach w kazdej bitwie to za malo,
o ile AI ma kupowac sprzet. Do tego wegiel: na 100 kg rudy ok. 100 kg wegla = ok. 50 drewna
(500 kg) - dzis drwal daje 18 drewna dziennie, wiec jedna kopalnia potrzebuje ok. 3 osad drwali.

## 6. Problem do decyzji: ceny wyrobow w grze nie trzymaja sie wagi

Koszt metalu na wyrob przy dzisiejszych cenach sztabek wobec ceny wyrobu z gry:

| Wyrob | Metal (dzisiejsze ceny sztab) | Cena wyrobu w grze | Metal / cena |
|---|---|---|---|
| Miecz 1h t3 (zelazo) | ok. 3 x 60 = 180 | ok. 1 900 | 10% |
| Helm t2 (zelazo) | ok. 3 x 60 = 180 | ok. 7 700 | 2% |
| Korpus t3, kolczuga (zelazo) | ok. 28 x 60 = 1 700 | ok. 3 000 | 56% |
| Korpus t5, plyta (stal szl.) | ok. 50 x 160 = 8 000 | ok. 22 200 | 36% |

Gdy wegiel i sztabki dostana koszt zgodny z historia (wegla duzo, zelazo z dymarki drozsze), kolczuga
t3 kosztowalaby wiecej metalu niz jest warta, a helm t2 dalej bylby zlota zyla. Ceny wyrobow w grze
(ROT) sa ustawione "pod gre", nie pod wage metalu.
