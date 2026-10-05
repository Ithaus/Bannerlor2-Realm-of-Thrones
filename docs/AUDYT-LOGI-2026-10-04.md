# AUDYT LOGOW 2026-10-04 (5 sesji: 13:00, 14:05, 15:31, 15:54, 16:53)

Zrodla: `Modules/Armoury/Armoury-2026-10-04_*.log`, `Modules/Armoury/Logs/2026-10-04_16-53-30/*.log`,
`Modules/RealisticCaptivity/RealisticCaptivity-2026-10-04_*.log`,
`Documents/.../CrashScribe/session-2026-10-04_*.log`, `economy-2026-10-04_*.csv`.
Kod NIE byl zmieniany. Tam, gdzie przyczyna jest zgadywana - oznaczone **PRZYPUSZCZENIE**.

## 0. Wersje DLL w sesjach i wazna poprawka

| Sesja | Dni gry | DLL (wpisy CHANGELOG) | Co bylo testem dla |
|---|---|---|---|
| 13:00 | 108836-108840 (5) | do 43 | wpis 44 |
| 14:05 | 108836-108842 (7) | 44-45 | wpisy 46-47 |
| 15:31 | 108836-108844 (9) | 46-57 | wpisy 58-59 |
| 15:54 | 108836-108848 (13) | 58-61 | wpisy 62-64 |
| 16:53 | 108836-108848 (13) | 62-65 | wpisy 66-67 |

**Poprawka do wpisu 66 / zalozenia audytu:** sesja 16:53 to **NOWA GRA**, nie wczytany save. Dowody:
CrashScribe 16:55:32 wyjatek w `BKCampaignStartBehavior.OnCharacterCreationOver` (tworzenie postaci "Borys"),
`EKONOMIA (start, Day 3641 of Summer, 299 AC ...)`, Armoury `[16:55:41] StartKit: zloto startowe 1000 -> 100 d (start start_adventurer)`,
`UniqueSpoils: ... kopie startowe zdjete raz na kampanie: z targow 47`, dzien kampanii 108836 = start ROT.
Wszystkie piec sesji to nowe gry od dnia 108836 - dobrze, bo porownania miedzy sesjami sa uczciwe.

## 1. Bledy i wyjatki

- Armoury-*.log (wszystkie 5): **zero linii bledu** (szukane: error, wyjatek, exception, fail, null).
- RealisticCaptivity-*.log: 11 linii na sesje, start bez bledow, potem cisza (gracz nie byl w niewoli).
- CrashScribe: tylko "ERROR CAUGHT FURTHER UP" (FirstChance, zlapane). Z naszym kodem na stosie:
  - `Armoury.NightRest.PatchModels` (kazda sesja): `Parameter "mobileParty" not found in method NavalDLC.GameComponents.NavalDLCPartyMoraleModel::GetEffectivePartyMorale`
    -> postfix morale snu NIE wpiety w model NavalDLC (log: `Nocleg: kary snu wpiete (predkosc w 6, morale w 3 modelach)`). Zlapane, bez skutkow dla stabilnosci.
    Jesli NavalDLC jest aktywnym modelem morale i nie wola bazy - kara snu do morale nie dziala (**PRZYPUSZCZENIE**). Plik: `Armoury/src/NightRest.cs:1277`.
  - `Armoury.WesterosClimate.ApplyAll` (kazda sesja): `ArgumentException: Cannot bind to the target method` w MonoMod podczas detouru - zlapane; log klimatu meldowal "niebo metod 6, gospodarka metod 19" - nieszkodliwe.
  - `Armoury.StartKit.ApplyAll` - `AmbiguousMatchException` tylko w 14:05 (wersja 44, naprawione wpisem 46; w 15:31+ brak).
- Reszta wyjatkow to cudze mody: BKROTPatch (`DynamicPartySizePerformancePatch`, `RebellionsGameEntityInstantiatePatch` - 25x HarmonyException), AIInfluence (siec 127.0.0.1:4315, HTTP 402, JSON, `Collection was modified` w `SaveQueueManager`, IOException na pliku json), BK `CorrectPlayerEducation` (ArgumentNullException), UIExtenderEx (AmbiguousMatch).
- Zawieszenie `hang-2026-10-04_14-11-27.log`: 61 s w kodzie natywnym, "outside a campaign" (po wyjsciu z sesji 14:05). Bez naszego kodu.
- Crash 16:01:45 (natywny, wg wpisu 66) - w logach nic naszego.

## 2. Systemy - status

Legenda: OK / PROBLEM / NIE TESTOWANE. Liczby z sesji 16:53, o ile nie napisano inaczej.

### 2.1 Ceny historyczne (HistoricalPrices) - OK
- Wpiete: `popyt miast w nowej monecie wpiety w 3 modelach`, `zakupy mieszczan (BK CalculateBudget) ... wpiete`, `uzbrojenie 3264 szt. przeliczone (suma wartosci 34338604 -> 1504011)`, ruda/drewno w ladunkach `iron 10 -> 100 kg = 8 d, hardwood 10 -> 100 kg = 4 d`.
- Kontrola (wpis 58): tylko 1. dnia: `kontrola - 7 przedmiotow mialo zmieniona wartosc ... weirwood_bow 90000->61, ... giant_bow 200000->30 ...` (15:54 i 16:53 identycznie); kolejne dni brak linii = nikt juz nie zmienia. Ktos zmienia te 7 wartosci RAZ przy starcie (zrodlo dalej nieznane, kontrola dziala).

### 2.2 Rynek broni / PodazPopyt - PROBLEM
- Wpiete: `PodazPopyt: prawo podazy i popytu dla uzbrojenia CZYNNE (3 modeli cen)` (handel.log).
- Polki swiata 16:53, dzien 108837 -> 108848: razem 6961 -> 9057, ale wzrost to konie 2390 -> 3966, plaszcze 1178 -> 1829, helmy 108 -> 334, strzaly 634 -> 914, belty 466 -> 943.
  Ubywa sprzetu bojowego: BodyArmor 35 -> 29 (min 17), OneHanded 159 -> 81, Polearm 538 -> 214, Shield 535 -> 186, HorseHarness 341 -> 164.
- Kupcy (PodazPopyt): 731-927 szt. dziennie miedzy osadami za 162-348 tys.; "bez odbiorcy" rosnie 56 -> 564 szt. (nadwyzka sie gromadzi).
  Dla porownania 15:31 (przed wpisem 59): 1.77-4.35 mln dziennie - wtedy luki po ~7000 d.
- Zamowienia (wpis 67): w zadnej sesji brak linii `PodazPopyt: zamowienia` - **NIE TESTOWANE** (DLL wgrane po 16:53).
- handel.log ma tylko 73 linie i tylko Storm's End (2 zrzuty: 16:56:23, 16:58:50) - to probka, nie pelny zapis.
- Indeks surowcow na rynku broni: ruda med **1.50 (sufit) przez cala sesje**, drewno 1.50 -> 0.96, skora med 1.50, len 0.99 -> 1.34.

### 2.3 Warsztaty - PROBLEM (dzialaja, ale zly asortyment)
| Sesja | szt./dzien | Glowne wyroby | BodyArmor/dzien | Koszt/sprzedaz dzienna |
|---|---|---|---|---|
| 13:00 | 33-88 | tylko Bow | 0 | 173-468 / 237-595 tys. |
| 14:05 | 27-95 | tylko Bow | 0 | 140-474 / 194-690 tys. |
| 15:31 | 177-251 | tylko Bow | 0 | 744-1112 / 1.28-1.67 mln (!) |
| 15:54 | 102-334 | Bow, Cape, Head | 0-1 | 0.7-5.1 tys. / 0.9-10.3 tys. |
| 16:53 | 591-1105 | Cape 606-833 (dni 1-4) potem HeadArmor 209-321 | 1-39 (zwykle <16) | 7.7-14.6 tys. / 12.3-19.5 tys. |
- 16:53 "odpuszczone": brak surowca 521 -> 1302 cykli/dzien (ROSNIE), bez zysku 31-122, w robocie 1475-2356, brak zlota 0.
- Diagnoza (warsztaty.log): najwiecej utargu `desert_fabric_shoulderpad x376-435 (Value 26)`, `merchants_hat x249 (Value 3)`, `empire_cape_a x81-148 (Value 9)`.
  Pusty ranking: tarcze t1-t3 (`worn_horsemans_kite_shield Value 9, cena 8, koszt 12`; `tyrell_shield Value 31, cena 32, koszt 36`), uprzaz (`light_harness Value 44, cena 36, koszt 43`), garment.
  Ceny surowca w diagnozie: **skora 59-405 d za sztuke (historycznie 40), len 75-232 (historycznie 100)** - przy pustej polce surowiec (towar handlowy) drozeje do x10 i zabija oplacalnosc zbroi.
- Rachunek z kodu (`WorkshopLaw.cs:234` + `Revenue` linia 404): sztuka startuje gdy `0.8 x cena >= 1.10 x (surowce + dni x placa)`, a wartosc = `1.25 x (surowce + praca)` (`HistoricalPrices.cs:125`, `HistProfitPercent 25`).
  0.8 x 1.25 = 1.00 < 1.10 -> przy cenie rownej wartosci warsztat NIGDY nie startuje; potrzebna cena >= ~1.1-1.4 x wartosc (pusta polka, wojna). **PRZYPUSZCZENIE**: to glowny powod, czemu robia tylko to, czego brakuje najbardziej, a tarcze/uprzaz stoja przy pelnych polkach skor i lnu po cenach rynkowych.
- Garbarze/tkacze (wpis 64): `wygarbowali skor 0-182, utkali plotna 18-730` dziennie (730 tylko 1. dnia). Skory surowe i tak ROSNA 482 -> 1815 (+110/dzien), skora 192 -> 398 - za mala przepustowosc garbowania (**PROBLEM**, `WorkshopLaw.cs` `TanOrWeave`, `ArtisanTanWeavePerCycle 5`, szybkosc linii BK 0.2).

### 2.4 Surowce (Rynek surowcow) - PROBLEM
16:53, dzien 108837 -> 108848: iron 1527 -> 608 (-60%), hardwood 4372 -> 7591 (+74%), leather 192 -> 398, hides 482 -> 1815 (+277%), linen 615 -> 183 (-70%), flax 980 -> 210 (-79%), wool 405 -> 380.
Ruda i len sie koncza (stad "brak surowca" rosnie), drewno i skory surowe sie gromadza. Ten sam obraz w 15:54 (iron 1380 -> 712, hides 656 -> 2980, flax 1785 -> 685).

### 2.5 Konsumpcja miast - OK (posrednio)
Wpiete (`zakupy mieszczan ... wpiete`). Brak dziennej linii konsumpcji - ocena tylko przez zapasy: drewno i skory rosna, czyli mieszczanie ich nie zjadaja (zgodnie z wpisem 53). Len i ruda spadaja - konsumuja warsztaty, nie mieszczanie (`TownUseIron 0`).

### 2.6 Renty (Ludnosc) - OK, ale **populacja rosnie za szybko** (PROBLEM)
- Renty zaplacone 16:53: 737 358 -> 608 272 -> ... -> 334 318 zl/dzien (spadek coraz wolniejszy: ostatnie 4 dni 363k, 347k, 340k, 334k). Zgodne z szacunkiem rownowagi z wpisu 49 (~0.36 mln). 15:54: 732k -> 335k (13 dni). Nalezne ~5.8-5.9 mln (placone ~6%).
- Sesje przed wpisem 49 (13:00, 14:05): 1.44 mln -> 0.20-0.51 mln w 4-7 dni (wtedy wsie placily 50%).
- Ludnosc: 52.6 -> 53.8 mln w 12 dni (+2.3%, ~0.19%/dzien); 15:54: 52.6 -> 54.3 mln w 13 dni. Przy roku 364 dni to ~x2 na rok - historycznie ~0.5%/rok.
  Ludnosc = ogniska wsi x k + dobrobyt miasta x k (`PopulationLaw.cs:106 PeopleOf`), wiec rosnie razem z ogniskami/dobrobytem gry. **PRZYPUSZCZENIE**: wzrost ognisk/dobrobytu (vanilla/BK `BKGrowthModel`) nie przeskalowany do roku 364 dni (patrz docs/AUDYT-KALENDARZ.md:162). Renty nalezne rosna razem z tym (5.78 -> 5.91 mln).

### 2.7 Korona - OK
16:53: powinnosci 26 076 -> 8 908 zl/dzien (spadaja razem z dochodami rodow), danina wojenna 6 635-17 785, clo 22 315 -> 14 690, mennica 0, monopole 0 (kazdego dnia).
Suma 12 dni: 476 272 zl. Skarbce krolestw w CSV: kazde z 29 startuje od 2 000 000, po 12 dniach 2 001 394 - 2 067 961, razem **+746 121** = Korona 476 tys. + "Kingdom Budget Expense" rodow (~20.7 tys./dzien w top wydatkow). Bilans sie zgadza - nic z niczego.
Uwaga: skarbce tylko rosna (zaden nie zmalal) - **PRZYPUSZCZENIE**: skarbiec nie jest niczym wydawany, wiec to "czarna dziura" ~60 tys./dzien zabierana z obiegu.
Mennica i monopole 0 we wszystkich dniach - nie wiadomo, czy zaden krol nie ma tych polityk, czy liczenie nie dziala (**NIE TESTOWANE**).

### 2.8 Ekonomia krolestw (CrashScribe EKONOMIA + CSV) - PROBLEM (trend)
CSV 16:53 (310-313 rodow), dzien 108836 -> 108848:
- zloto rodzin razem 62.23 mln -> szczyt 64.48 mln (d. 108838) -> 61.09 mln; krolowie razem 10.69 -> 10.08 mln.
- wojsko 14 488 -> 59 873 ludzi (x4.1), zold/dzien 72 660 -> 266 019 (x3.7), wciaz rosnie 2-4%/dzien na koniec.
- dochod_model razem 1.50 mln -> 0.167 mln/dzien (po wpisie 49 renta idzie poza modelem); "Walled Demesnes" w top3 tylko 26 tys., "Village Demesnes" 111 tys.
- faktyczna zmiana razem: +1.55 mln (d.1) -> od d.5 ujemna, ostatnio -485 tys./dzien (~0.8% majatku/dzien).
- mediany EKONOMIA ostatni dzien: KROL zloto 366 005, faktycznie -842/d; ROD KROLESTWA 192 777 (start 220 000, -12%), -418/d; ROD MNIEJSZY 245 752, -1589/d.
- rody z zerowym zlotem: 3 od dnia 108844 (rodow 310 -> 313 - nowe rody bez zlota). Ten sam obraz w 15:31 i 15:54.
Wniosek: nikt nie bankrutuje w 12 dni, ale deficyt rosnie razem z armia; przy -400 do -1600 d/dzien rody krolestwa maja zapas na ~120-450 dni. Do obserwacji po wpisie 67.

### 2.9 Bank Zelazny - NIE TESTOWANE
Wszystkie 5 sesji: `nowe pozyczki 0 (0) ... dlug razem 0, kapital Banku 5000000`. Wedlug kodu (`IronBank.cs:200-208`) lord pozycza, gdy zloto < 10 dni zoldu (x2 w wojnie, `IronBankWageDays 10`); mediana zlota ~190 tys. wobec zoldu ~650/dzien - warunek nie zachodzi. Nie blad, ale system martwy na poczatku gry.

### 2.10 Wyrzutki - OK
16:53: start `pula poczatkowa 6562 ludzi w 227 regionach`, 1. dnia `bandy nowe 373 (6178 ludzi)`, potem pula 400 -> 201, band 354 -> 343, ludzi w bandach 5987 -> 5450 (-9% w 12 dni), nowe bandy 10-17/dzien, odmowione ~50/dzien. "awanse ... bez sprzetu" 0 -> 23/dzien (rosnie). Stabilnie.

### 2.11 Pobor / Ochotnicy / Werbunek - PROBLEM (Ochotnicy)
- Pobor: chec do sluzby x0.69 (d.1) -> x0.28-0.32; losowan 20 597 -> ~5 400. OK.
- HouseLevies: 1008 -> 150-206/dzien. OK.
- Werbunek: zloto AI do notabli 23-58 tys./dzien, do miast za najemnikow 94-128 tys./dzien. OK (zloto ma odbiorce).
- **Ochotnicy: awanse z kupionym sprzetem 1-9/dzien (1. dzien 33), cofniete 339 -> 806-814/dzien i ROSNIE.** Ta sama liczba we wszystkich sesjach od 13:00 (325-688, 280-769, 346-781, 344-794). Wpis 67 ma to rozwiazac (zamowienia) - NIE TESTOWANE.
  Przyczyna widoczna w danych: BodyArmor na WSZYSTKICH polkach swiata 17-35 sztuk, a warsztaty robia 1-39 dziennie.

### 2.12 ZakupyAI i garnizony - PROBLEM (asortyment)
- 16:53: d.1 `127 wizyt, 5272 szt. za 320586; w tym garnizony 89 zakupow za 261327`, potem 111-150 wizyt, 924-1228 szt. za 41-59 tys.; garnizony 74-101 zakupow za 31-45 tys. (~75% wydatkow).
- Suma z zakupy.log: budzety 10.99 mln, wydane 134 tys. - budzet nie jest ograniczeniem, ogranicza towar.
- **Garnizony i lordowie kupuja stroje cywilne i bezuzyteczne rzeczy**: w zakupy.log 45/224 linii ma `andal_dress*`, `andal_noble_clothes*`, `cloth_apron`, `half_apron`, `merchants_hat`, `tournament_arrows` (np. `garnizon The Eyrie ... andal_noble_clothes3 22, andal_dress6 21, cloth_apron 43`; `Barristan Selmy ... GRE_tournament_arrows 2`). Najczesciej kupowane: `desert_fabric_shoulderpad` 39 razy, strzaly wielu rodzajow, `empire_cape_a` 20, `merchants_hat` 18.
  **PRZYPUSZCZENIE**: wzorce zolnierzy garnizonu biora tez zestawy cywilne albo dopasowanie jest po samym slocie bez minimalnego pancerza; strzaly turniejowe nie sa wykluczone. Plik: `Armoury/src/AiGear.cs` (wzorce/braki, `TryBuy`).
- zakupy.log ma tylko 224 linie wobec ~1600 wizyt w 12 dni - zapis szczegolow jest ograniczony.

### 2.13 Unikaty (UniqueSpoils, Kronika) - PROBLEM (czesciowo naprawione wpisem 66)
- Start: `unikaty ROT poza zaopatrzeniem sklepow - oznaczone 89; kopie startowe zdjete raz na kampanie: z targow 47, z bagazy AI 0`.
- Mimo to pierwszy stan swiata (dzien 108837) pokazuje na targach: `brienne_armor: targ Storm's End`, `rhaegar_pauldrons`, `cersei_red_dress: targ King's Landing`, `ramsay_shoulders: targ Barrowton` - **PRZYPUSZCZENIE**: zdejmowanie (16:54:59) bylo przed pierwszym zaopatrzeniem targow nowej gry albo te sztuki nie sa NotMerchandise. Plik `UniqueSpoils.cs`.
- Strzaly "unikatowe" w wielu kopiach: `ravens_teeth_arrows: targ The Twins x4`, `giant_arrows` na 3-4 targach; lordowie je kupuja za 7-37 zl - to zwykla amunicja oddzialow, nie unikat (wpis 66 `UniqueMaxWearers` dotyczy noszacych, nie kopii na targach).
- `noble_default` i `andal_civ_boots` jako unikaty; `noble_default` wypisywany jako "zmiany" codziennie z ta sama lista nazwisk (d. 108839, 108840, 108843 ...) - wykrywanie zmian daje falszywe zmiany (**PRZYPUSZCZENIE**: porownanie listy noszacych w innej kolejnosci). 12 "zdobyczy" przy pojmaniu - wszystkie to "Noble Default". Naprawione wpisem 66 tylko dla przedmiotow noszonych przez >3 postacie.
- Lord kupuje unikat z polki (wpis 62): `Meryn Trant kupil rhaegar_gauntlets w King's Landing za 2678` - dziala.
- Znikanie: `LegendaryLaw: magazyny AI (dzien) - 1 legend przepadlo z 1 partii` (16:53; 1-2 w kazdej sesji od 14:05); `LegendaryLaw: targi (dzien) - 14-25 egzotycznych wierzchowcow zdjetych z polek` (130 w sesji). To celowe zdejmowanie, ale sprzeczne z zasada wpisu 62 "nic nie znika" - jesli miasto za nie zaplacilo, traci (**PRZYPUSZCZENIE**).

### 2.14 Bitwy (wpis 65) - PROBLEM
- Dziennie 5-29 bitew, zabitych 98-461. Glowny log: zwyciezcy 0.9-16.3%, przegrani 69.2-79.4% (wszystkie bitwy).
- Przeliczenie bitwy.log (170 bitew polowych + 7 najazdow): 89 bitew (52%) to lordowie przeciw "Broken Men" (6-15 ludzi). Wszystkie: zwyciezcy srednio 8.0% / mediana 1.6%, przegrani 73.3% / 83.0%.
  **Prawdziwe bitwy (obie strony >= 50, nie gorzej niz 4:1): 12 - zwyciezcy srednio 15.2% / mediana 11.4%, przegrani 33.5% / 32.2%.**
  Przegrani zgodnie z historia (15-40%), **zwyciezcy ~3x za duzo** (historia 1-5%). Przyklad: `Davos Seaworth 311 vs Raymund Connington 310; wygrywa atakujacy | atakujacy: zabici 62 (19.9%)`. Zwyciezca czasem traci wiecej niz przegrany (`Dim Dalba 125 vs Night King's Party 119: atakujacy zabici 34 (27.2%), obronca 17 (14.3%)`).
  **PRZYPUSZCZENIE**: symulacja bitew automatycznych (vanilla/RBM + `BattlefieldLaw: bitwy automatyczne bez mnoznikow tieru`) zabija zbyt rowno; brak fazy poscigu/rozbicia - `rozbici` = 0 w 352 z 354 wpisow stron.
- **Zloto bitwy nie jest logowane**: wpis 65 obiecuje "zdobyl/stracil zl", a w 177 liniach bitwy.log nie ma ani jednej. Kod pokazuje je tylko gdy > 0 (`BattleChronicle.cs:42,68`) - `PlunderedGold`/`GoldLost` sa zawsze 0 w chwili odczytu (**PRZYPUSZCZENIE**: odczyt przed rozliczeniem lupu albo po wyzerowaniu).
- Gracz nie walczyl w zadnej sesji - lupy, zuzycie, amunicja gracza: NIE TESTOWANE.

### 2.15 Zloto startowe (StartKit) - OK
15:54 i 16:53: `StartKit: zloto startowe 1000 -> 100 d (start start_adventurer)`; CSV gracz 100. (13:00-15:31: gracz 1000 - przed wpisem 58.)

### 2.16 Kalendarz / klimat - OK
`Kalendarz: rok ma 364 dni (13 tygodni w sezonie, sezon 91 dni)` w kazdej sesji; `Klimat: start - lato trwa juz 3640 dni, skonczy sie za 155-258 dni`.
Uwaga: CLAUDE.md pisze o roku 168 dni (`WeeksPerSeason = 6`) - kod i log maja 13 tygodni / 364 dni (`McmSettings.cs:901`). CLAUDE.md jest nieaktualny.

### 2.17 Fabula (CrashScribe) - OK
16:53: `FABULA: dzien 3-5 ... czolo kolejki: NedEvent (na dzien 8)`, potem `FABULA ROT odpalone: Ned`, `dzien 12 | zaleglych na osi: 20 | czolo kolejki: HarrenhalSiegeNotificationEvent (na dzien 77) | ostatni start: 9`. To samo w 15:54. Umarli: Nocny Krol zyje, 4 bandy, 570-650 trupow, 0 osad, inwazja jeszcze nie. Kronika wojen: Free Folk vs Nights Watch, Dragonstone vs Stormlands, potem North vs Baratheon KL, Targaryen vs Aegon.

### 2.18 Niewola (RealisticCaptivity) - NIE TESTOWANE
Start OK w kazdej sesji (`Harmony: patche zaaplikowane`, `Sesja wystartowala, dialogi, domy i praca dodane`), potem brak zdarzen.

### 2.19 Wydajnosc - OK
Odstep miedzy dziennymi podsumowaniami (czas rzeczywisty): 16:53 ~10.7 s/dzien (16:56:52 -> 16:58:39 = 107 s na 10 dni), 15:54 ~10.8 s, 15:31 ~10.3 s, 14:05 ~10 s. Pierwszy dzien 30-40 s. Brak spowolnienia z kolejnymi dniami mimo ~1700 sztuk w toku warsztatow. noc.log: do 534 spiacych partii, `obudzony cudza reka ... Razem w sesji: 360` - dziala.

## 3. Sprzecznosci znalezione

1. Wpis 66 "wczytany save" - to byla nowa gra (punkt 0).
2. Wpis 65 "zloto zdobyte/stracone" - nigdy nie pojawia sie w bitwy.log.
3. Unikaty zdjete z targow przy starcie (47), a dzien pozniej na targach sa `brienne_armor`, `rhaegar_pauldrons`, `cersei_red_dress`.
4. "Nic nie znika" (wpis 62) vs codzienne zdejmowanie legend i egzotycznych koni (LegendaryLaw).
5. Skarbce krolestw tylko rosna (+746 tys. w 12 dni), nic ich nie wydaje.
6. CLAUDE.md: rok 168 dni; log/kod: 364 dni.
7. Wartosc sztuki = koszt x 1.25, a warsztat zada 0.8 x cena >= 1.1 x koszt - przy cenie = wartosc nigdy nie zarabia.
Nie znaleziono: podwojnego liczenia renty/korony (bilans skarbcow zgadza sie z suma Korony + vanilla), zlota z niczego w handlu (15:54/16:53 PodazPopyt rzedu 0.15-0.35 mln, przelew miedzy miastami).

## 4. Problemy wedlug waznosci

| # | Problem | Dowod | Przyczyna | Plik |
|---|---|---|---|---|
| 1 | Brak zbroi korpusu, ochotnicy ~800 cofnietych/dzien | BodyArmor na polkach swiata 17-35; warsztaty 1-39/dzien; `cofniete 806-814` | popyt staly (naprawa wpisem 67 - NIE TESTOWANE) + punkt 2 + 3 | `SupplyDemand.cs`, `VolunteerKit.cs`, `WorkshopLaw.cs` |
| 2 | Warsztat nie zarabia przy cenie = wartosc | 0.8 x 1.25 = 1.0 < 1.10 (`WorkshopMinProfitPercent 10`) | konflikt ustawien `WorkshopSellShare`, `HistProfitPercent`, `WorkshopMinProfitPercent` (wyliczone z kodu; skutek PRZYPUSZCZENIE) | `WorkshopLaw.cs:205,234,404`, `HistoricalPrices.cs:125` |
| 3 | Skora i len w warsztacie po cenie targu x5-x10 | diagnoza: `skora 0.0x405`, `len 0.0x232` (historycznie 40 / 100) | towar handlowy przy pustej polce do x10 (wpis 67 wspomina) | `WorkshopLaw.cs` (`MatPrice`), `SupplyDemand.cs` |
| 4 | Ruda sie konczy, skory surowe sie gromadza | iron 1527 -> 608; hides 482 -> 1815; brak surowca 521 -> 1302 | za malo rudy (indeks 1.50 caly czas); garbowanie ~70 skor/dzien przy doplywie ~+110 | `MaterialLaw.cs` (wydobycie), `WorkshopLaw.cs` (`TanOrWeave`, `ArtisanTanWeavePerCycle`) |
| 5 | Garnizony/lordowie kupuja suknie, fartuchy, kapelusze, strzaly turniejowe | 45/224 linii zakupy.log | PRZYPUSZCZENIE: wzorce z zestawow cywilnych, brak wykluczenia tournament | `AiGear.cs` |
| 6 | Zwyciezcy traca ~15% zabitych (historia 1-5%) | 12 prawdziwych bitew: 15.2% / 11.4%; `rozbici` prawie zawsze 0 | PRZYPUSZCZENIE: symulacja auto-bitwy, brak poscigu | `BattlefieldLaw.cs`, symulacja RBM/vanilla |
| 7 | Ludnosc +2.3% w 12 dni (~x2 na rok) | `Ludnosc: 52.6 mln -> 53.8 mln` | PRZYPUSZCZENIE: wzrost ognisk/dobrobytu nie przeskalowany do roku 364 | `PopulationLaw.cs:106`, wzrost BK/vanilla |
| 8 | Unikaty na targach mimo zdjecia; strzaly "unikatowe" w kopiach | `brienne_armor: targ Storm's End`, `ravens_teeth_arrows: targ The Twins x4` | PRZYPUSZCZENIE: zdjecie przed zaopatrzeniem targow; amunicja w spisie unikatow | `UniqueSpoils.cs`, `RotUniques.cs` |
| 9 | Falszywe "zmiany" w kronice unikatow | `noble_default` codziennie z ta sama lista | PRZYPUSZCZENIE: porownanie zalezne od kolejnosci | `UniqueSpoils.cs` |
| 10 | Zloto bitew nie logowane | 0 wystapien "zdobyl"/"stracil" w 177 bitwach | PRZYPUSZCZENIE: odczyt w zlym momencie | `BattleChronicle.cs:42` |
| 11 | Skarbce krolestw tylko rosna | +746 121 w 12 dni | PRZYPUSZCZENIE: brak wydatkow ze skarbca | `KingdomTreasury.cs` |
| 12 | Kara snu do morale nie wpieta w NavalDLC | CrashScribe: `Parameter "mobileParty" not found ... NavalDLCPartyMoraleModel` | inna nazwa parametru w modelu NavalDLC | `NightRest.cs:1277` |
| 13 | Deficyt rodow rosnie z armia | faktycznie -485 tys./dzien razem; wojsko x4.1, zold x3.7 | wojna + renty ~0.33 mln wobec zoldu 0.27 mln + wydatkow garnizonu | obserwowac (CSV) |

## 5. Nie testowane w tych logach
Zamowienia i sufit x4 (wpis 67), filtr unikatow i srednie z prawdziwych bitew (wpis 66), Bank Zelazny (brak pozyczek), mennica/monopole (zawsze 0), niewola, lupy i zuzycie sprzetu gracza (gracz nie walczyl), kuznia gracza.
