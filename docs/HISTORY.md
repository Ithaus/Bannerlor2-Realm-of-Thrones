# Co te mody robia i dlaczego tak

Kontekst dla kogos, kto siada do tego pierwszy raz. Szczegoly zmian: `CHANGELOG.md`.
Bledy i pulapki: `docs/ERRORS.md`.

## Zalozenie calosci

Jeff gra w **Realm of Thrones** i chce, zeby swiat zachowywal sie **realistycznie**,
a nie "growo". Wiekszosc decyzji projektowych wynika wprost z tego jednego zdania:

- kolumna maszeruje **w tempie najwolniejszego czlowieka**, chyba ze piechota jedzie na
  luzakach (wtedy polowa premii kawalerii — "nie jezdza tak dobrze jak jezdzcy")
- **wszyscy musza spac** poza umarlymi; oboz to twarda pauza, nie przewijanie czasu
- **jedzenie nie ma zuzycia**, pancerz ma
- **ucieczka z pola = brak lupu** ("uciekłem, to niby jak?")
- **konny moze uciec** przed piechota, nawet samotny
- **ranny schodzi z pola** zamiast stac i wyc
- rzemioslo jest **nauka**: zaczynasz od tieru 1 i dochodzisz do reszty, mozesz **spartaczyc**
  albo trafic legende, a zdobyta sztuke mozesz **rozlozyc na wzor** kosztem samej sztuki
- kalendarz: **rok ma 168 dni**, zeby fabula ROT zgadzala sie z chronologia serialu

## Skad sie wzial CrashScribe

Z koniecznosci. Stos okolo 40 modow, w tym dwa (BannerKings + BKROTPatch) w kiepskim
stanie, powodowal CTD i ciche psucie danych, ktorego nie dalo sie zdiagnozowac z okna
crasha. CrashScribe:

1. **loguje kazdy wyjatek** z winowajca, stanem gry i zywym stosem watku
2. **lapie bledy w modach, zeby gra nie padala** (finalizery Harmony)
3. **latami cudze mody** (`Mends.cs`) — patrz `docs/ERRORS.md`
4. **raportuje stan swiata**: wojny, fabula ROT, sily Innych, notable w osadzie

Ten log jest **glownym narzedziem diagnostycznym** calego projektu. Bez niego kazda
naprawa jest zgadywaniem — a zgadywanie w tym projekcie kosztowalo juz kilka wieczorow.

## Fabula ROT

ROT odpala wydarzenia serialowe po dacie kampanii. Problemy, ktore rozwiazuje `Fabula.cs`:

- dwie daty stały w zlym miejscu wzgledem serialu (egzekucja Karstarka i bunt u Crasterow
  odpalaly PRZED Czarnym Nurtem) — przesuniete
- po wlaczeniu fabuly w trwajacej kampanii **wszystko chcialo odpalic naraz** — rozrusznik
  przepuszcza jedno wydarzenie na `FabulaPaceDays` dni, w kolejnosci osi
- wydarzenie, ktoremu swiat odjechal, jest omijane po 6 dniach na czele kolejki
- os czasu jest mnozona przez `FabulaTimeScale` (2.0), bo rok wydluzylismy do 168 dni

## Stan swiata (dla orientacji, 25.08.2026)

- kampania: **dzien ~195**, Summer 1085
- odpalone: Ned, RiverlandsDeclareWar, HarrenhalSiegeNotification, HarrenhalSiege, Harrenhal
- **Nocny Krol zyje**, ale w jeden dzien gry stracil armie z **828 do 72** trupow
- **0 osad za Murem** — do inwazji brakuje mu wszystkich 25; Mur jest bezpieczny
- trwa wojna **Free Folk vs Nights Watch**, Polnoc bije sie z Krolewska Przystania i Zelaznymi Wyspami

## Rzeczy niedokonczone / do decyzji

- **Namiot obozu** — mechanizm cofniety po CTD; do zrobienia od nowa, ostroznie
  (bez globalnego wylacznika, bez dotykania wizerunkow co klatke).
- **Obozowanie bandytow** — Jeff tego chcial, zostalo cofniete razem z reszta. Wrocic
  osobno, z limitem ikon i testem po kazdym kroku.
- **Dlug snu w pogoni** — jw.
- **BK `GetHeroesToUpdate`** — 37 601 wyjatkow na sesje, latka zaproponowana, czeka na zgode.
- **Pokretla, ktorych Jeff nie ustawil**: `SmithingSkillPerTier` 45→35,
  `DurabilityPerArmorPoint` 20→6-8, `MinSellPercentOfValue` 5→2-3, tempa marszu.
