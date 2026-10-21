## m97-szkolenie - marsz nie uczy, szkolenie wojska tylko z obozu (Armoury)

**Dla Jeffa (prosto):** w marszu Twoi ludzie juz sie nie ucza - ucza sie tylko wtedy, gdy w obozie (klawisz O) wybierzesz **Train the men**. Cwiczenia
ida za dnia (przy obozie 21-3: od 3:00 do 18:00, w nocy ludzie spia), najwyzej 8 godzin na dobe, wyjsc mozesz w kazdej chwili ("Stop the drill").
Pelny dzien cwiczen (8 h) uczy o 50% wiecej niz dawny dzien postoju, krotsze cwiczenie - swoja czesc; tempo dalej zalezy od Twojego Przywodztwa, a do tego
pomaga **mistrz musztry** - najlepszy towarzysz w druzynie (polowa jego Przywodztwa). Perki treningu (Combat Tips, Raise the Meek i 10 innych) dzialaja
teraz na dzien cwiczen (wartosc gry x1.5 za pelny dzien) i ich opisy w grze to mowia. Lordowie AI zostaja na razie przy dawnej regule (klucz
**DrillCampOnlyAi = NIE**): wedlug biegu 96 przy jednej zasadzie ich ludzie uczyliby sie o ok. 1/3 (od 14% do 53%) mniej - linia w logu mierzy to co dobe, decyzja Twoja.

**Problem:** Jeff 20.10 ok. 18:27: "zmien szkolenie wojska - szkolenie wojska nie odbywa sie w marszu, w marszu jest marsz; na ekranie obozu mam opcje
szkolenie wojska i szybkosc szkolenia zalezy od moich umiejetnosci leadership i co jeszcze mozna; [...] szkolenie z ekranu obozu daj, ze jest o 50% wydajniejsze
niz bylo przedtem, ale nadal zalezne od umiejetnosci leadership etc.; i zobacz, czy mozna przerobic perki leadership, aby pasowaly".
Stan przed zmiana (kod, `Drill.cs` naglowek): XP = (B x L x D x S + P) x A raz na dobe w ticku treningu; D = 1.5 przy postoju (ruch < 4 h z 24), **0.9 w marszu** -
marsz uczyl 60% tego, co postoj; perki P z modelu treningu gry/BK co dobe niezaleznie od cwiczen (Walk It Off, Forced March, Unburdened - **tylko w marszu**).

**Zasada (decyduje Claude, liczby):**
- **Marsz nie uczy:** D liczy tylko godziny cwiczen; godzina ruchu = 0. Jedna zasada: gracz, rod gracza i lordowie w armii gracza (Z14a) - klucz **DrillCampOnly**
  (TAK); lordowie AI - klucz **DrillCampOnlyAi** (NIE, uzasadnienie nizej).
- **Gracz cwiczy tylko z obozu:** `arm_camp_wait` -> **Train the men** -> menu oczekiwania z paskiem `arm_camp_drill_wait` (wzor `arm_sleep_wait`/SmithMenu
  "Step away": opcja **Stop the drill** tylko podnosi flage, przelacza tick menu - bez SwitchToMenu z opcji menu oczekiwania). Godziny cwiczen = poza noca snu
  `NightRest.MenuNight` (swit 3 -> cwiczenia 3:00-17:59, noc 18:00-2:59; swit 6 -> 6:00-20:59). Najwyzej **8 h na dobe od switu do switu**; o zmierzchu,
  po 8 h, przy glodzie - samo konczy (komunikat) i wraca do menu obozu. Opcja wylaczona (z podpowiedzia): noc, glod, dlug snu o swicie, 8 h juz dzis, brak ludzi,
  osada/morze/armia cudzego wodza, bitwa. W osadzie gracz NIE cwiczy (Jeff: opcja w obozie; obozu nie rozbija sie w miescie) - decyzja, do zmiany jednym zdaniem.
- **Tempo:** pelny dzien cwiczen = **8 godzin** (dzien cwiczen zolnierza: rano i po poludniu po 4 h, reszta to oporzadzenie, posilki, warty). Pelny dzien
  **D = 1.5 x 1.5 = 2.25** (dawny dzien postoju x "o 50% wydajniejsze"), godzina = 2.25/8 = **0.28**. Dalej x L, x S (zapas), x A (uzbrojeni), + perki, kara glodu
  i dlugu snu jak dotad (dzien kary = 0, godziny czekajace przepadaja). Godziny gracza czekaja na najblizszy tick treningu jego druzyny (wzor liniowy w godzinach -
  chwila ticku nie zmienia sumy); menu pokazuje "Today: X of 8 hours", XP przychodzi z codzienna linia "Drill today".
- **"Co jeszcze mozna" - mistrz musztry (klucz DrillMaster, TAK):** najlepszy INNY bohater w druzynie (towarzysz, rodzina) wedlug Przywodztwa; polowa jego
  Przywodztwa dochodzi do dowodcy: L = (Przywodztwo dowodcy + 0.5 x mistrza) / 170 w 0.5-1.5 (sufit bez zmian). Tylko Przywodztwo (umiejetnosc uczenia ludzi);
  bronie ucza samego bohatera - drugi skladnik pominiety swiadomie (prosto). Strona gracza i AI pod zasada obozu - tak samo.
- **Przyklad (60 ludzi tieru 2, B = 10 + 2 x 2 = 14, S = 1, A = 100%; XP na czlowieka / na 60 ludzi):**

  | Przywodztwo | L | dawny postoj (x1.5) | dawny marsz (x0.9) | **8 h cwiczen (x2.25)** | 4 h cwiczen | marsz teraz | z mistrzem 100 |
  |---|---|---|---|---|---|---|---|
  | 50 | 0.50 | 10.5 / 630 | 6.3 / 378 | **15.8 / 945** | 7.9 / 473 | 0 | L 0.59: 18.5 / 1112 |
  | 150 | 0.88 | 18.5 / 1112 | 11.1 / 667 | **27.8 / 1668** | 13.9 / 834 | 0 | L 1.18: 37.1 / 2224 |
  | 250 | 1.47 | 30.9 / 1853 | 18.5 / 1112 | **46.3 / 2779** | 23.2 / 1390 | 0 | L 1.50 (sufit): 47.3 / 2835 |

  Tydzien przy Przywodztwie 150: dawniej 4 dni marszu + 3 dni postoju = 4 x 11.1 + 3 x 18.5 = 99.9 XP na czlowieka; teraz 3 dni po 8 h = 83.4, a marsz rano
  (3:00-10:00) i 8 h cwiczen po poludniu (10:00-18:00) co dzien = 194.6.
- **Perki (wszystkie, ktore daja dzienne XP w `DefaultPartyTrainingModel.GetEffectiveDailyExperience` i `BKPartyTrainningModel` - zdekompilowane):** pod zasada
  obozu liczone wlasna tabela (`CampDrill.Rules`), wartosc gry (zaokraglona jak BK) **x 1.5 za pelny dzien cwiczen**, czesc dnia - czesc; warunek oddzialu i stanu
  partii jak w grze, **warunek "w marszu" / "czekajac w osadzie" zastapiony cwiczeniem**. Opis w grze podmieniony (PrimaryDescription albo SecondaryDescription
  i STR1/STR2, id bez zmian; pierwotny opis do logu):

  | Perk (czesc) | gra: na dobe | teraz: za pelny dzien cwiczen |
  |---|---|---|
  | Leadership Combat Tips (1) | +2 wszyscy, takze w marszu | +3 wszyscy |
  | Leadership Raise The Meek (1) | +4 tier 1-2 | +6 tier 1-2 |
  | One Handed Military Tradition (1) | +2 piechota | +3 piechota |
  | Athletics Walk It Off (2) | +3 piesi, TYLKO w marszu | +4.5 piesi (marsz sam nie uczy) |
  | Athletics A Good Days Rest (2) | +10 piesi, tylko czekajac w osadzie | +15 piesi |
  | Throwing Saddlebags (2) | +1 piechota | +1.5 piechota |
  | Bow Trainer (2) | +3 strzelcy | +4.5 strzelcy |
  | Crossbow Renowned Marksmen (1) | +2 strzelcy | +3 strzelcy |
  | Scouting Forced March (2) | +2 wszyscy w marszu przy morale > 75 | +3 wszyscy przy morale > 75 |
  | Scouting Unburdened (2) | +2 wszyscy w marszu z przeciazeniem | +3 wszyscy przy przeciazeniu |
  | Steward Seven Veterans (1) | +4 tier 4+ (BK; gra 0) | +6 tier 4+ |
  | Steward Drill Sergeant (1) | +2 wszyscy | +3 wszyscy |
  | Polearm Drills (2) | bonus 0.1 -> gra i BK zaokraglaja do 0 | bez zmian (0, opis bez zmian) |
  | Roguery No Rest for the Wicked | +20% dla oddzialow kultury bandytow | +20% wyniku cwiczen (opis prawdziwy - bez zmian) |

  Bow Bulls Eye (zalogi z namiestnikiem) - nie dotyczy partii lordow. Liczby wedlug wartosci gry odczytanych w chwili startu sesji (linia startu podaje faktyczne).
- **AI - pomiar z biegu 96 (`Armoury-2026-10-20_17-49-10.log`, 180 dob, linie "Musztra AI"):** partio-doby wedlug godzin ruchu: 0 h 13.9%, 1-3 h 3.0%, 4-11 h 9.6%,
  **12+ h 73.5%**; waga XP (baza gry) z ruchem < 4 h 15%, < 8 h 19%, < 12 h 25%. Dawne D srednio 0.99; perki P = 25% XP przed bronia; XP po broni 16.8 na glowe na dobe.
  Zasada obozu (godziny postoju za dnia, noc snu sie nie liczy, oboz AI 21-3 lezy w nocy): doby < 8 h ruchu -> 2.25, 8-11 h -> 1.4-2.25, 12+ h - nieznany
  rozklad wewnatrz kubla (pelny dzien marszu 3:00-21:00 = 0 h postoju za dnia -> 0). Wynik: **XP AI x0.47 (12+ h = 0), x0.68 (12+ h srednio D 0.25), x0.86
  (D 0.5)** - srodek ok. **-32%**, perki spadaja bardziej (x0.34-0.62). Spadek ponad 30% -> osobny klucz **DrillCampOnlyAi, domyslnie NIE**: promocje AI i popyt
  na lepszy sprzet (kalibracja KS 95-105%, ekonomia od rekrutow i awansow) sa ustawione na dzisiejszym XP; zmiany o -14..-53% nie da sie sprawdzic tej nocy.
  Nowa linia `Szkolenie w obozie (m97): AI dzien N` liczy CO DOBE obie reguly dla kazdej partii AI pod wzorem (takze przy NIE): "dawna regula X -> zasada obozu Y
  (xR)" i rozklad godzin postoju za dnia - po jednym biegu Jeff ma dokladna liczbe. TAK = jedna zasada dla wszystkich (Jeff 09.10), AI uczy sie mniej.

**Zmiana (Armoury):**
- NOWY `CampDrill.cs`: zasada (`RuleFor`), godziny dnia (`DayHour` = poza `NightRest.MenuNight`), stan gracza (dzis od switu, czekajace na tick) z zapisem
  `arm_campdrill` "1|dzis|doba switu|do policzenia" (SaveText.Sync, osobny try; brak klucza = 0), opcja i menu cwiczen, mistrz musztry (`Lead`), tabela perkow
  (`PartyPerks` raz na partie, `TroopPerks` na oddzial), opisy perkow przy starcie sesji (gdy DrillCampOnly i DrillLaw), linie logu, liczniki z potknieciami.
- `Drill.cs` (wpiecia): Ctx - pola zasady obozu; `CtxOf` - H (gracz: godziny z menu; reszta: `DayRestHours` = godziny postoju za dnia z maski 24 h, najwyzej 8),
  D i L zasady obozu (czynne albo tylko pomiar AI); `Shape` - pod zasada obozu XP = (B x L x D x S + Pc) x czynnik bandytow, Pc z tabeli; pomiar dawna/obozu;
  `Done` - liczniki pomiaru; `TickPostfix` - zuzycie godzin gracza i linia doby; `DayMessage` - "N h of drill in camp", "No drill today ..."; `Reset`, `SessionStart`,
  `Daily` - po jednej linii.
- `ArmouryBehavior.cs`: jedna linia RegisterEvents (po CampWalk) + blok SyncData `arm_campdrill`. `NightRest.cs`: `SetCampBackground` private -> internal (tlo menu cwiczen).
- `Settings.cs` + `McmSettings.cs` (gen_mcm): **DrillCampOnly** (TAK), **DrillCampOnlyAi** (NIE), **DrillMaster** (TAK), grupa MCM "Drill".
- `tools/sprawdz_logi.py --grupa m97szkolenie` (SZ-0..SZ-6).
- Linie logu: `Szkolenie w obozie (m97): start sesji | zasada ... | tempo: pelny dzien 8 h = x2.25 ... | godziny cwiczen 3:00-17:59 ... | mistrz musztry ... | perki
  przerobione N/13: [lista z wartosciami]`, `... perk X - opis gry: '...' -> nowy: '...'` (12 linii), `... opcja "Train the men" ...`, `... koniec cwiczen | h | dzis X z 8
  | powod`, `... gracz dzien N | godzin cwiczen H (z menu obozu ...) -> dzien xD ... | XP przyjete ... | Przywodztwo xL (dowodca, mistrz) | zapas | uzbrojeni | kara`,
  `... AI dzien N | zasada AI TAK/NIE | partii | godzin postoju za dnia ... | XP po broni: dawna regula -> zasada obozu (xR) ... | XP z marszu ... oddzialow 0 | strona gracza ...`.

**Ryzyko / co sprawdzic (kontrola calosci):**
- **Menu cwiczen nieprzetestowane w grze** (zakaz uruchamiania tej nocy): wzor wyjscia jak `arm_project_wait` (flaga + SwitchToMenu z ticku, zegar ruszany z opcji -
  ratunek po wczytaniu w srodku menu); powrot do `arm_camp_wait`. Bitwa/spotkanie - tick nic nie robi, przelacza gra.
- **Gracz w osadzie nie cwiczy** (decyzja wyzej); rod gracza i AI w osadzie cwicza (godziny postoju za dnia) - zgodnie z zadaniem.
- **Rod gracza i lordowie w armii gracza** (pod DrillCampOnly) maszeruja jak AI - ich XP spada podobnie jak AI (-14..-53%); lordowie doczepieni do Twojej armii
  stoja, gdy Ty cwiczysz w obozie, wiec cwicza razem z Toba (godziny postoju za dnia).
- **Ksiega snu gracza** liczy stanie w miejscu jako odpoczynek za dnia (stawka dzienna) - takze w godzinach cwiczen (regula NightRest bez zmian); stamina w menu
  cwiczen jak w marszu (menu cwiczen nie jest w `InCampMenu` - cwiczenia to nie odpoczynek).
- **Perki pod zasada obozu z wlasnej tabeli** - wynik perkow modelu gry/BK nie jest uzywany (dla partii pod zasada); inny mod dopisujacy XP do modelu treningu
  stracilby to dla strony gracza (znany tylko BK - jego perki sa w tabeli). Opisy perkow podmieniane przy starcie sesji (wylaczenie klucza w trakcie gry - stare
  opisy wracaja dopiero po wczytaniu, dzialanie od razu). Opisy mowia z perspektywy druzyny gracza; AI przy DrillCampOnlyAi = NIE dostaje perki jak w grze.
- **Zuzycie zapasu** (Wear) jak dotad proporcjonalne do D: pelny dzien cwiczen 2.25 = 1.5 x dawnego dnia postoju, dzien bez cwiczen - bez zuzycia.
- Pomiar AI liczy dla kazdej partii AI pod wzorem co dobe 13 HasPerk + petla po bohaterach rosteru + 24 bity maski - koszt w linii "Musztra AI ... koszt" (dotad
  ok. 160 ms na dobe). Linie "Musztra AI" (stare czynniki L x D) przy DrillCampOnlyAi = TAK licza L zasady obozu (z mistrzem).
- Zapis: nowy klucz `arm_campdrill`; stara DLL go ignoruje; stary zapis bez klucza = 0 godzin. Bitwy, XP za bitwe, awanse - bez zmian (tylko trening dzienny).
- Klucze nowe w `Armoury.json` Jeffa nie istnieja - MCM wezmie domyslne (TAK / NIE / TAK). Pliku nie ruszalem.

**Status:** DO SPRAWDZENIA (build Armoury kod 0; gra nieuruchomiona; nic nie wgrane; `sprawdz_logi --grupa m97szkolenie` sprawdzone na sztucznym logu).

**Test w grze krok po kroku:**
1. Start sesji - w `Armoury.log` linia `Szkolenie w obozie (m97): start sesji | ... TAK ... | perki przerobione 12/13` i 12 linii `perk ... -> nowy`; ekran postaci,
   drzewo Leadership: Combat Tips mowi "+3 experience to every man for a full day of drill in camp ... Marching teaches nothing."
2. Mapa, marsz caly dzien bez obozu: nastepnego dnia komunikat "No drill today: your men drill only in camp ..."; w logu `gracz dzien N | godzin cwiczen 0.00 ... XP przyjete 0`.
3. O 9:00 klawisz O -> menu obozu -> **Train the men** (podpowiedz o tempie) -> pasek; po ok. 4 h "Stop the drill" -> wraca menu obozu; ponownie Train the men ->
   po kolejnych 4 h samo konczy: "A full day of drill (8 hours) ..."; opcja wyszarzona "The men have drilled a full day ...". O 19:00 opcja wyszarzona "The men are
   asleep ...". Wejscie o 16:00 -> o 18:00 "Dusk - the drill is over for today ...". W logu `koniec cwiczen | ... | powod: gracz / pelny dzien / noc`.
4. Nastepny dzien treningu: "Drill today: N XP ... Leadership x.. (drill master Imie), 8.0 h of drill in camp x2.25 (full day 8 h x2.25) ..."; log `gracz dzien N | godzin
   cwiczen 8.00 (z menu obozu ... 8.00 ...) -> dzien x2.25`. Z towarzyszem z Przywodztwem - "mistrz musztry Imie N" i L wyzsze niz "bez mistrza".
5. Glod albo noc bez snu: opcja wyszarzona z powodem; zapis i wczytanie w trakcie dnia - "Today: X of 8" zostaje.
6. Po kilku dobach: `python -I tools/sprawdz_logi.py --grupa m97szkolenie <Armoury-*.log>` - SZ-1 (zero XP z marszu), SZ-2, SZ-3, SZ-4, SZ-6 TAK; **SZ-5 - liczba
   dla Jeffa**: "XP po broni razem dawna X -> obozu Y (xR)" - przy xR powyzej ok. 0.7 mozna wlaczyc DrillCampOnlyAi bez obawy o ekonomie.
Linie do grep: `Szkolenie w obozie (m97)`, `CampDrill.` (Log.Error), `arm_campdrill`, `Drill today`, `No drill today`.
