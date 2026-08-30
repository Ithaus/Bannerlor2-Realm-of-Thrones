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

## Stan swiata (dla orientacji, 30.08.2026)

- kampania: **dzien ~597**, Winter 1087; **13 rownoczesnych wojen** (KRONIKA
  WOJEN w logu) — przez to targi bywaja puste i armie AI gloduja (glod rani
  25% szeregowych dziennie; bohaterowie tylko traca HP, stad lordowie
  "zdrowi" wsrod samych rannych)
- fabula ROT: odpalone m.in. WallSiege i Wall (pelna lista w logu "FABULA
  ROT odpalone")
- **Nocny Krol zyje i odbudowuje sie po kazdej przegranej** (pila w logach:
  836 -> 212 -> 796; 30.08 rano 759 -> 834). Klan niewybijalny przed inwazja
  (CanHeroDie=false), nekromancja wskrzesza po kazdej bitwie
- **0/25 osad za Murem od 594 dni** — prog oblezenia (>=500 zdrowych w JEDNEJ
  bandzie, realnie ~1000-1500 przez warunki przewagi i limit rosnacy
  +3/dzien) jest poza zasiegiem band po ~200 trupow. Inwazja mechanicznie
  mozliwa, praktycznie zamrozona balansem ROT. Dzwignia:
  `OthersNecromancyMultiplier` w MCM ROT (sekcja Others, domyslnie 1.0)

## Rzeczy niedokonczone / do decyzji

- **Namiot obozu, obozowanie bandytow, dlug snu** — ODBUDOWANE 26-27.08
  (wpisy w CHANGELOG); dzialaja, pilnowac tylko zasad z CLAUDE.md przy
  zmianach (zadnych globalnych wylacznikow, zadnych wizerunkow co klatke).
- **BK relacje (dawne 37 601 NRE)** — zalatane bramami RelationsUpdateGate
  + EssosTitleGate; 30.08 zero wyjatkow. Szczegoly: ERRORS.md A4.
- **DTE AmbiguousMatchException na pasku mapy** — najwiekszy zywy generator
  wyjatkow (604/sesje), udokumentowane jako ERRORS.md A6, mend do decyzji.
- **Panel wyniku kucia (CraftPopup)** — WGRANE, wciaz NIEZWERYFIKOWANE przez
  Jeffa (dzwiek + Done bez crasha).
- **Pokretla, ktorych Jeff nie ustawil**: `SmithingSkillPerTier` 45→35,
  `DurabilityPerArmorPoint` 20→6-8, `MinSellPercentOfValue` 5→2-3, tempa
  marszu; po sledztwie glodu takze `BkSupplyDaysCap` 1→3-4 (nasz temper
  tnie zywnosc z sakw BK i przyspiesza glod AI o 2-4 dni).
- **PLAN-kuznia-1do1 krok 5** (popup wyboru klas pancerza) — bez decyzji.
