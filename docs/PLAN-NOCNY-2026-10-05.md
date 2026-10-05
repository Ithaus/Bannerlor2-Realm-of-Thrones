# Plan pracy nocnej 2026-10-05 / 06 (Jeff spi; rano raport)

Jeff 05.10 wieczorem: "Ja ide spac, zaplanuj prace nocna i rano dasz mi raport".

## Zasady nocy

- Gra jest zamknieta i Jeff nie testuje. Do gry wolno wgrac TYLKO paczke samych logow (bez zmiany zasad gry), po niezaleznym
  przegladzie. Kazda zmiane zasad gry przygotowac jako gotowa paczke (kopia robocza w scratchpadzie sesji, build z kodem 0,
  niezalezny recenzent, gotowy wpis CHANGELOG) - wgranie dopiero po testach Jeffa, po jednej.
- Limit sesji: przed kazdym etapem `get_usage`; przy 85% i wiecej nie startowac - przelozyc etap o 20 minut (CronCreate) i zakonczyc
  ture; pauza przy 90%.
- Wszystko, co ustalone, zapisywac w repo (commit + push) - kontekst rozmowy moze zostac skrocony.
- Nie pytac Jeffa o nic w nocy: decyzje sa w `docs/STAN-PRAC.md`, sekcja "Decyzje Jeffa 05.10 - komplet".
- Stan wyjsciowy: w grze DLL wpisu 101 (md5 165b8744d2b122733099877ece396ec1), nieprzetestowany. Scratchpad sesji:
  `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`.

## Etap 0 (trwa, przed resetem limitu ok. 17:20)

Zadania w tle uruchomione wieczorem:
- karawany (task wdz31lb12): latka w kopii `scratchpad\wpis-102\repo` + przeglad - NIE wgrywac w nocy;
- aneks demografii (task wsjtbrfqz): `docs/DEMOGRAFIA-ANEKS-2026-10-05.md` (spustoszenie jako ulamek okregu, male krainy, widok ksiegi, bandyci);
- nieumarli (task w781xei9u): skad Innym przybywa ludzi.

## Etap 1 (po 17:25)

1. Przyjac wyniki etapu 0: aneks - commit + push (+ PDF do `Dokumenty\Bannerlord-dokumenty`); nieumarli - wynik dopisac do
   `docs/STAN-PRAC.md`; karawany - zostaja w kopii, opis do raportu.
2. Workflow "noc-1" (kazda paczka: autor w kopii roboczej repo, build kod 0, niezalezny recenzent, wpis CHANGELOG bez statusu):
   - **LOGI** (tylko log, zero zmian zasad): ksiega przeplywow osad i pieniadza swiata (K1 fundamentu: utarg wsi, skup miast
     i zamkow, renty, zold, kasy, zloto tworzone i znikajace); log "Ludzie:" (krok 1 demografii: w domu / w sluzbie / wyrzutki /
     polegli na region + plik CSV); poprawki ksiegi "Ruda:" / "Drewno:" (zakupy naszych budow, podzial taborow na wiesniakow /
     karawany / lordow); "brak surowca" w linii "Warsztaty:" z nazwa surowca.
   - **ZAPAS STARTOWY** (K3): ruda i drewno bez dziesieciokrotnej masy na starcie nowej kampanii.
   - **MINERAL BK**: usuniecie podwojnego wpisu mineralu wsi gorniczej (decyzja Jeffa) + `MineOutputMultiplier` 3 -> 6, zeby
     wydobycie rudy zostalo na dzisiejszym poziomie (do potwierdzenia w raporcie).

## Etap 2 (po 22:25)

1. Paczke LOGI wprowadzic do repo, zbudowac (kod 0), wgrac (gra zamknieta, kopia `.bak-...-przed-NN`, md5), CHANGELOG, commit + push.
   To jedyne wgranie tej nocy. Pozostale paczki zostaja w kopiach.
2. Workflow "noc-2":
   - **PASER**: bandy sprzedaja zrabowany ladunek w miescie (OutlawLaw);
   - **BETTERECONOMY**: gotowy skrypt zmiany 13 kluczy w `better_economy_settings.xml` z kopia pliku (nie uruchamiac);
   - **ZOLD I SKARBIEC**: zold partii do sakiewek ludzi, zold garnizonu do kasy osady (takze gracz); skarbiec krolestwa w wojnie
     zwraca lordom polowe dziennego zoldu - kod w kopii + przeglad;
   - **PROJEKT** (bez kodu): Nocna Straz, niewolni Zelaznych Wysp, Wolni Ludzie (300 tys., walcza wszyscy dorosli), nieumarli -
     reguly na ksiedze ludzi; dlug bez utraty lenna (wierzyciel pobiera dochod wsi, lord wyprzedaje wszystko).

## Etap 3 (po 03:25)

1. Przyjac wyniki "noc-2" (zapisac w repo, co gotowe).
2. Workflow "noc-3": drugi, niezalezny przeglad wszystkich paczek pod katem kolizji miedzy nimi i kolejnosci wgrywania; paczka
   PRZYROST NATURALNY (kroki 2-3 demografii: jednostka czlowieka 1/k hearth, przyrost naturalny w miejsce stalego +0.3% dziennie);
   plan testow na rano.

## Raport poranny (06:40)

`docs/RAPORT-NOCNY-2026-10-06.md` + PDF w `Dokumenty\Bannerlord-dokumenty` + krotkie streszczenie w rozmowie:
co wgrane (tylko logi), jakie paczki czekaja i w jakiej kolejnosci je testowac, co wyszlo z badan (nieumarli, male krainy,
spustoszenie), czego nie zdazono i dlaczego, pytania do Jeffa.

## Proponowana kolejnosc testow rano

1. Woz (wpis 101) + nowe logi. 2. Karawany. 3. Zapas startowy. 4. Mineral BK. 5. BetterEconomy. 6. Paser. 7. Zold i skarbiec.
Potem demografia (przyrost naturalny, pobor) i reguly krain.
