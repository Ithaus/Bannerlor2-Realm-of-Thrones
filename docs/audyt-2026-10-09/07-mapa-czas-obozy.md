# AUDYT 07 (09.10): realne odleglosci, tempo marszu i obozy (noc 24-6)

Zlecenie Jeffa (09.10, w nocy): "realne dystanse, aby wojsko pokonywalo realne dystanse na mapie, sprawdz, plus zaudytuj obozy -
teraz jak armia idzie, to trzeba rozbic oboz - daj godziny od 24 do 6 rano" + "rzeczy, ktorych nie mamy, a moglyby sie przydac".
Tylko odczyt. Kod = Armoury c01a54ba (commit 2e235ea). Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [H] historia/lore ze zrodlem,
[S] szacunek. Baza, ktorej NIE powtarzam: `docs/AUDYT-CZAS-MAPA.md` (04.10: skala mapy, lancuch modeli predkosci, oblezenia, spojnosc
armii) i `docs/AUDYT-KALENDARZ.md`. Styk z `08-drogi-wioski-widok.md` (drogi M-1/M-2, ognisko obozu N-5) zaznaczony w tekscie.

---

## 0. Dla Jeffa

1. **Dzis wojsko idzie 5-6 razy za szybko.** Armia 800 ludzi robi ok. 115-120 km na dobe, lord ze 150 ludzmi 160-190 km, kilku jezdzcow
   ok. 280 km. Z Winterfell do Krolewskiej Przystani: armia ok. 21 dni, lord 13, goniec 9. W sredniowieczu armia z taborem szla 15-25 km
   dziennie, a w ksiazkach Stannis planowal 300 mil (ok. 480 km) w 15 dni, czyli ok. 32 km dziennie.
2. **Godziny obozu same tego nie zmienia.** Dzis lordowie i karawany spia od 22 do 5 (7 godzin). Twoje "od 24 do 6" to 6 godzin, czyli
   AI pojdzie o godzine DLUZEJ dziennie (ok. +3% drogi). Odleglosc ustawia predkosc, nie godziny snu.
3. **Obozy dzialaja, ale przeciekaja.** W rocznym tescie co noc spi ok. 730 oddzialow (ok. 310 lordow, ok. 420 karawan, 7 armii).
   Co noc ok. 100 z nich budzi rozkaz innego moda (glownie StrategicCampaignAI o polnocy i o 4), oddzial jedzie srednio ok. 6 km
   z namiotem, zanim go polozymy z powrotem - to jest Twoj "namiot, ktory sie porusza". Przy szybkim czasie (x8) straznik zaglada
   co sekunde prawdziwa, a to 2-3 godziny gry.
4. **Tej nocy (male, bezpieczne):** oboz 0-6 dla wszystkich (lordowie, armie, karawany, pytanie o oboz u Ciebie o polnocy, bez kary snu,
   jesli rozbijesz oboz o 0:00), straznik spiacych liczony w czasie gry (namioty przestaja jezdzic), poprawka meldunku "ile dni drogi"
   (pokazuje dzis o 20% za krotko).
5. **Realne odleglosci wymagaja przebudowy "tempa swiata"** - dzis suwak dziala tylko na czesc predkosci i ma podloge, przez ktora duze
   armie przestaja zwalniac. Po przebudowie proponuje wariant "jak w ksiazkach" (20% dzisiejszej bazy): armia 800 ok. 32 km/dobe,
   Winterfell-KP: goniec 33 dni, lord 49, armia 77. Wariant "twarda historia" (12%): 56 / 82 / 129 dni. **To Twoja decyzja** -
   we wrzesniu prosiles o przyspieszenie swiata o 50%, wiec pytam, czy na pewno chcesz wolniej.
6. Wiesniacy i karawany musza dostac wlasne, szybsze tempo - wioska na mapie lezy ok. 150 km od miasta (mapa jest scisnieta), wiec
   przy pelnym zwolnieniu miasta by zglodnialy.
7. Nowe rzeczy, ktorych nie mamy: zimowa noc dluzsza (oboz zima 22-8), dzien odpoczynku armii co 7 dni, napad na spiacy oboz
   (obroncy w nieladzie), zmeczenie AI po nocnym marszu (dzis AI maszeruje noca za darmo), marsz forsowny dla Ciebie.

---

## 1. Stan dzis

### 1.1 Jednostki i skala [K][P]

- Ruch: `MobileParty.ComputeNextMoveDistance` - droga = `Speed x dt` (`MobileParty.cs:2795-2800`); czas mapy `MapTimeTracker.Tick(4320 x dt)`
  (`Campaign.cs:874`, `0.25 x realDt`, `:844`) => **1 dt = 1.2 h gry, partia robi `Speed / 1.2` jedn. na godzine** [K].
- Skala: **4.77 km / jedn.** (Mur-Sunspear 1012 jedn. = ok. 3000 mil; `AUDYT-CZAS-MAPA.md` rozdz. 1). `Wayfinder.cs:16` ma 4.75 - roznica 0.4%,
  bez znaczenia. Skala 2.84 z innych notatek to "Mur" (ROT ma Mur 1.5x za dlugi) - styk `08-...md` M-1 [K][P].
- Szybki czas: `SpeedUpMultiplier` vanilla 4 (`Campaign.cs:370`), u Jeffa `FastForwardMultiplier` 8 (`Settings.cs:688`) => 1 s realna =
  ok. 2.4 h gry (doba w ok. 10 s) [K][S]. Roczny autotest: 364 doby w 7260 s = ok. 20 s na dobe [P] (`Armoury-2026-10-08_08-18-38.log`).

### 1.2 Lancuch predkosci (stan w grze) [K]

| Warstwa | Wartosc u Jeffa | Gdzie |
|---|---|---|
| vanilla baza `4 x (200/(200+ludzi))^0.4`, jazda +0.30 x udzial konnych, noc -0.25, las -0.3, brod/most -0.3, **`MinimumSpeed` 1.0** | - | `DefaultPartySpeedCalculatingModel.cs:93-95, 242, 264-323, 357` |
| **WorldPace** = `AddFactor(p/100 - 1)` na bazie | **75%** (`Armoury.json:118`; domyslna w kodzie 50, `Settings.cs:313`) | `WorldPace.cs:56` |
| TerrainEase: cofa procent vanilli, doklada PLASKIE kary (las 0.1, pustynia 0.2, snieg 0.2, bagno 0.3, brod/most 0.5, noc 0.5) | jak w kodzie (`Armoury.json:89,110`) | `TerrainEase.cs:79-103`, `Settings.cs:314-320` |
| MarchPace: sufity 4.0 pieszo / 4.2 tabor / 5.0 piechota na luzakach / 6.5 jazda, **x WorldPace** | `Armoury.json:82-88` | `MarchPace.cs:116-118`, `Settings.cs:356-359` |
| RB pory roku: zima -20%, jesien -10%, wiosna +5% | `RealisticBannerlord` json | (AUDYT-CZAS-MAPA 2.2) |
| BK `SlowerParties` | 0.0 (wylaczone 15.09) | BannerKings.json |
| NightRest: kara dlugu snu -25/-40/-90% | tylko gracz | `NightRest.cs:59, 785-800` |

**Wazne (nowe wzgledem 04.10): WorldPace NIE mnozy predkosci, tylko dodaje -0.25 do sumy wspolczynnikow** (`ExplainedNumber`:
`wynik = baza x (1 + suma czynnikow)`, `ExplainedNumber.cs:156, 245-255`). Dowod [P]: `Audyt predkosci ... Base +3.99 | Cavalry +1.20 |
Cargo -0.04 | Game Difficulty +0.40 | World pace -1.00 => 4.55` (`Armoury-2026-10-08_11-40-05.log`) - 3.99 x (1 + 0.30 - 0.01 + 0.10 - 0.25) = 4.55.
Jazda traci wiec tylko 19% zamiast 25%, a przy suwaku 30% konnica stracilaby 54%, piechota 70% - suwak nie jest proporcjonalny.

### 1.3 Predkosc i droga na dobe - dzis [K][S]

Godziny ruchu AI: oboz 22:00-05:00 = 7 h snu, **17 h marszu**; 15% kolumn idzie przez noc (24 h, z kara nocna 22-02). Srednio ok. 18 h.

| Typ | Speed dzis | km/dobe (17 h) | po zmianie na 0-6 (18 h, 22-24 z kara nocna -0.5) |
|---|---|---|---|
| Goniec (5 jezdzcow) | 4.16 | 281 | 294 |
| Lord 150, 1/3 konno | 2.72 | 184 | 191 |
| Lord 150 pieszo | 2.40 | 162 | 168 |
| Armia 800, 1/4 konno | 1.73 | 117 | 120 |
| Armia 2000, 1/5 konno | 1.24 | 84 | 87 |
| Gracz (pomiar, glownie konno) [P] | 4.15-4.55 | ok. 300 (przy 18 h) | - |

### 1.4 Ile dni drogi - dzis (trasa = 1.1 x linia prosta, linie proste z `ROT-Map/ModuleData/settlements.xml`) [K][S]

| Trasa (jedn. linii prostej, km drogi) | Goniec | Lord 150 | Armia 800 | Armia 2000 |
|---|---|---|---|---|
| Winterfell - Krolewska Przystan (465, ok. 2440 km) | 8.7 | 13.3 | 20.8 | 29.1 |
| Winterfell - Castle Black (256, 1340) | 4.8 | 7.3 | 11.5 | 16.0 |
| Winterfell - Riverrun, marsz Robba (346, 1815) | 6.5 | 9.9 | 15.5 | 21.7 |
| KP - Riverrun (215, 1130) | 4.0 | 6.1 | 9.6 | 13.5 |
| KP - Casterly Rock (351, 1840) | 6.6 | 10.0 | 15.7 | 22.0 |
| KP - Highgarden (294, 1540) | 5.5 | 8.4 | 13.2 | 18.4 |
| KP - Storm's End (149, 780) | 2.8 | 4.3 | 6.7 | 9.3 |
| KP - Sunspear (337, 1770) | 6.3 | 9.6 | 15.1 | 21.1 |
| Riverrun - Casterly Rock (187, 980) | 3.5 | 5.3 | 8.4 | 11.7 |

Zmiana godzin obozu na 0-6 skraca te liczby o ok. 3-4% (tabela 1.3) - **nie wydluza**.

### 1.5 Obozy - kod [K]

Wszystko w `Armoury/src/NightRest.cs`, wolane z godzinowego ticku (`ArmouryBehavior.cs:603`) i z klatki mapy (`ArmouryBehavior.cs:576`,
`NightRest.cs:1267-1285`).

| Kto | Okno | Jak | Linie |
|---|---|---|---|
| Lordowie i karawany (AI) | ticki 22,23,0..4 -> spia **22:00-05:00** | `RememberOrder` + `Ai.DisableForHours(1)` + `SetMoveModeHold()`; o swicie oddanie rozkazu sprzed snu | `:402-494` (okno `:406`) |
| Armie | tylko wodz (`Army.LeaderParty`); doczepieni stoja z nim; **czlonkowie w drodze na zbiorke nie spia** | `:448` | |
| Pomijani | 15% kolumn na noc (Id + dzien) % 100 < 15; wrog lub banda <= 6 jedn. (ok. 29 km); ucieczka/poscig; morze; osada; bitwa; oblezenie; Nieumarli | `:441-469`; `Settings.cs:697, 700` | |
| Bandyci | NIE obozuja (`AiBanditsCampToo` false - `Armoury.json:288`); 3/4 "nocnych" lezy 10-16, 1/4 "dziennych" 23-5 | `:506-538` (okna `:511-512`) | |
| Wiesniacy, patrole | nie obozuja (filtr `IsLordParty / IsCaravan`) | `:440` | |
| Gracz | pytanie o oboz o **21:00** (`:138`); sen liczony 21:00-05:59 pelna stawka, w dzien x0.6 (`:107, 114`); auto-namiot na postoju w nocy (`:118-124`); rozliczenie nocy o **6:00** (`:154`); menu snu noc 21-5 (`:1188, 1206`) | | |
| Nieumarli | nie spia (gracz `:92`, AI `:449`, namioty `:286`) | | |
| Straznik snu (`HoldSleepers`) | co **~1 s REALNA** w oknie 22-4 kladzie z powrotem kazdego z cudzym rozkazem lub zjazdem > 0.3 jedn. | `:200-232`, wywolanie `:856-862` | |
| Namioty AI wokol gracza | co **~2 s REALNE**, limit 60, promien 100 jedn.; noca 21-5 namiot tez dla kazdej STOJACEJ kolumny lorda/karawany | `:242-294`, wywolanie `:847-853` | |
| Namiot gracza | zdejmowany co klatke, gdy gracz odjedzie > 0.25 jedn. od miejsca obozu | `:833-840` | |

Klucze MCM obozu w `Armoury.json` Jeffa (`:288-326`): `AiCampsAtNight` true, `AiCampSkipPercent` 15, `AiCampDangerRadius` 6.0,
`AiTentCap` 60, `AiTentRadius` 100, `BanditsRestByDay` true, `AiBanditsCampToo` false, `CampTentIcon` true, `NightfallPromptEnabled` true,
`SleepHoursNeeded` 6.0, `DayRestFactor` 0.6, `NightRestEnabled` true. **Godzin obozu NIE ma w zadnym kluczu** - sa zaszyte w kodzie
(`:107, 138, 154, 250, 406, 511-512, 852, 861, 1188, 1206`). W `Armoury.json` nie ma czego zmieniac; trzeba dodac klucze w kodzie.

### 1.6 Obozy - pomiar [P]

`Modules/Armoury/Logs/2026-10-08_08-18-38/noc.log` (autotest roczny, 364 noce, 4511 linii):

| Miara | Wartosc |
|---|---|
| spiacych na godzine nocy (srednia) | ok. 730-770 (lordow ok. 310, karawan ok. 420, band 0, **wodzow armii ok. 7.1**) |
| pierwsza noc -> ostatnia | 197 (160 lordow, 37 karawan) -> 775 (287 lordow, 488 karawan, 9 wodzow armii) |
| obudzonych cudza reka | **39 218** w roku = ok. 108 na noc (raport godzinowy 2548 linii, srednio 15.4/h) |
| zapisane przypadki (pierwsze 3 + co 20.) | 1963: rozkaz po przebudzeniu Hold 1142, GoToSettlement 564, GoToPoint 231, Patrol 16, Escort 7, Raid 2, Besiege 1; **wodz armii 199** (ok. 4 000 w roku) |
| zjazd obudzonego od legowiska | **srednio 1.29 jedn. (ok. 6 km), maks. 5.69 (27 km), > 1 jedn. w 59%, > 3 jedn. w 2%** |
| gra Jeffa (zapis z doby 360, `2026-10-08_11-32-39/noc.log`) | 729-735 spiacych, 6-7 wodzow armii, 21-25 obudzonych/h, zjazd sr. 0.79 |

Wniosek: armie i lordowie naprawde staja na noc, ale ok. 15% spiacych na noc jest budzonych rozkazem StrategicCampaignAI / AIInfluence / BK
(SCA co 4 h wola `SetMove*` na wodzach: `StrategicCampaignAI145Behavior.cs:154-335`) i przejezdza srednio 6 km, zanim straznik ich polozy.
Zjazd rosnie z szybkoscia zegara (straznik liczy sekundy realne, nie godziny gry) - autotest ok. 1.2 h gry na sekunde: sr. 1.29; gra
Jeffa (wolniej): 0.79.

### 1.7 Gracz [P]

Audyt predkosci (raz na dobe): gracz konno 4.55 (morale normalne), 4.15 (niskie morale) - `Armoury-2026-10-08_11-40-05.log`,
`..._11-32-39.log`. `World pace -1.00` = 25% bazy, nie wyniku.

---

## 2. Jak bylo w sredniowieczu i w lore [H]

### 2.1 Historia

| Kto | Tempo | Zrodlo |
|---|---|---|
| Piechota rzymska (norma szkolenia) | 20 mil rzymskich (ok. 29.6 km) w 5 "letnich godzinach", pelny krok 24 mile | Wegecjusz, *Epitoma* I.9 - https://classicsforall.org.uk/sites/default/files/uploads/Bellaria%20Part%202/CfA%20Bellaria%20104%20Vegetius%202.pdf ; https://thesciencebookstore.com/thesciencebookstore/2013/01/roman-military-technopunk.html |
| Duza armia z taborem | 8-12 mil (13-19 km) dziennie; 12 mil z taborem to "dosc szybko"; ok. 8 h marszu, wymarsz ok. 5:00, sen ok. 21:00 | B. Devereaux - https://acoup.blog/2019/10/06/new-acquisitions-how-fast-do-armies-move/ |
| Henryk V, 1415 (Harfleur - Azincourt) | ok. 260 mil w 17 dni = ok. 15 mil (24 km) dziennie, krotki prowiant | https://www.nationalarchives.gov.uk/agincourt/campaign-and-battle/ ; https://deremilitari.org/RESOURCES/SOURCES/agincourt.htm |
| Armie krzyzowcow w Europie | studium tempa marszu | https://www.cambridge.org/core/journals/traditio/article/abs/rate-of-march-of-crusading-armies-in-europe-a-study-and-computation1/75444AF71A0DD79AE827A0117F71100C |
| Harold II, 1066 (Londyn - York) | ok. 30 mil (48 km) dziennie - wyjatek, konno | acoup (wyzej); wiedza ogolna |
| Mongolowie | do 60 mil (ok. 95 km) dziennie strategicznie | acoup (wyzej) |
| Poslaniec bez rozstawnych koni | ok. 40 mil (64 km) dziennie; sztafeta - etapy 20-30 mil, dalej | https://en.wikipedia.org/wiki/King%27s_Messenger ; https://historyrise.com/article/the-black-princes-role-in-the-medieval-english-postal-system/ (zrodlo slabe) |
| Rytm dnia | wymarsz o switaniu, oboz przed zmrokiem; armia w obozie 12-16 h na dobe | acoup (wyzej) [H]; [S] |

### 2.2 Lore Gry o Tron

| Zdarzenie | Liczby | Zrodlo |
|---|---|---|
| Stannis, Deepwood Motte - Winterfell (ok. 5 000 ludzi) | plan: **100 lig = 300 mil (ok. 480 km) w 15 dni = ok. 32 km/dobe**; w sniezycy po 15 dniach polowa drogi, po 33 dniach 3 dni przed celem | https://awoiaf.westeros.org/index.php/Battle_in_the_ice ; https://awoiaf.westeros.org/index.php/A_Dance_with_Dragons-Chapter_42 |
| Robert jedzie na Polnoc | ok. 300 ludzi, wielki woz krolowej ciagniety przez 40 koni; czas niepodany w ksiazce; Devereaux szacuje 35-40 dni na ok. 1 600 mil | https://awoiaf.westeros.org/index.php/A_Game_of_Thrones-Chapter_4 ; https://awoiaf.westeros.org/index.php/Wheelhouse ; acoup |
| Krolewski Trakt | ok. 2 000 mil Storm's End - Mur; Winterfell - KP fani 1 500-1 750 mil | https://iceandfire.fandom.com/wiki/Kingsroad ; https://mapofwesteros.com/distances/ |
| Mapa ROT | Winterfell - KP 465 jedn. = 2 218 km w linii prostej, ok. 2 440 km drogi [K][S] | settlements.xml |

Wniosek [S]: lore jest szybsze od historii (Stannis 32 km/dobe z 5 000 ludzi, jezdzcy ok. 60-70 km/dobe), historia wolniejsza
(15-25 km armia, 40-60 km jazda). Oba sa 4-8x wolniejsze niz dzisiejsza gra.

---

## 3. Luki i bledy logiki

1. **WorldPace dodaje zamiast mnozyc** [K][P] - `WorldPace.cs:56` (`AddFactor`), dowod w 1.2. Skutek: nie da sie ustawic "x% predkosci"
   rowno dla wszystkich; im nizszy suwak, tym bardziej faworyzuje jazde i partie z premiami (morale, perki, trudnosc gry). Przy 30%:
   jazda x0.46, piechota x0.30 (wzgledem bez suwaka).
2. **Podloga `MinimumSpeed` 1.0** [K] (`DefaultPartySpeedCalculatingModel.cs:95, 357`; `ExplainedNumber.LimitMin` nadpisuje, `:257-259`).
   Dzis lapie armie ok. 2000 noca (1.24 - 0.5 < 1.0); przy kazdym wolniejszym suwaku lapie wszystkie armie - duze wojsko przestaje
   zwalniac (znane z 04.10, nienaprawione).
3. **Plaskie kary terenu nie skaluja sie z tempem** [K] (`TerrainEase.cs:79-103`). Przy armii 0.45 (wariant 20%) noc -0.5 = postoj
   (na podlodze), snieg -0.2 = -44%.
4. **AI szacuje droge na stare tempo** [K] - `Campaign.EstimatedAverageLordPartySpeed` 3.36 i 7 innych (`Campaign.cs:332-346, 1196`) +
   vanilla liczy `Speed x 24` na dobe (bez 1.2 i bez snu). Juz dzis AI mysli, ze idzie ok. 1.6x dalej niz idzie (24/18 x 1.2) [S].
5. **Straznik snu i namioty AI licza sekundy realne** [K][P] (`NightRest.cs:847-862`). Przy x8 to 2.4 h gry miedzy przegladami:
   obudzony oddzial jedzie z namiotem do kolejnego przegladu (pomiar: sr. 6 km, maks. 27 km), a "stojaca" kolumna z namiotem z okna 21-5
   (`:274-290`) traci namiot dopiero przy nastepnym odswiezeniu (2 s realne = ok. 5 h gry). To jest "namiot sie porusza".
6. **Szesc roznych "nocy" w kodzie** [K] - gra `IsNight` 22-02 (`CampaignTime.cs:80-102`, ROT `SunRise 2 / SunSet 22`) dla kary nocnej;
   oboz AI 22-5; sen gracza 21-6; namioty 21-5; widocznosc 21-5 (`SightRange.cs:34-35`); bandyci 23-6 (`NightRest.cs:512`); warsztat
   23-5 (`ArmouryBehavior.cs:1138-1139`, `TroopSelfMend.cs:95`). Narusza zasade "jedna zasada na jedno zjawisko" (CLAUDE.md, zasada 0).
7. **Pulapka przy 0-6 dla gracza** [K]: tick o godzinie h liczy godzine, ktora WLASNIE minela. Gracz, ktory staje o 0:00, ma tick 0:00
   jako "w ruchu" (`:96, 108-111`), ticki 1-5 pelne (5 h), tick 6:00 juz "dzien" x0.6 (`:107, 114`) = 5.6 h < 6 h -> **dlug snu mimo
   obozu 0-6**, jesli tylko przesunac okno na 0-5. Trzeba liczyc tick 6:00 jako noc (patrz 5.1).
8. **Martwy klucz `AiNightsAwakeInChase`** [K] - w `Settings.cs:699`, `McmSettings.cs:2465, 3349`, w `Armoury.json:292`, nigdzie nieczytany.
   AI, ktore maszeruje noca (15% kolumn, scigajacy, uciekajacy, "wrog blisko"), nie placi za to niczym - gracz placi -25..-90% predkosci
   i morale. "Nic za darmo" - zlamane dla AI.
9. **Czlonkowie armii w drodze na zbiorke nie spia** [K] (`NightRest.cs:448` - kazdy z `Army != null`, ktory nie jest wodzem, jest
   pomijany, takze niedoczepiony).
10. **Obozowanie swiata zalezy od stanu gracza** [K] - `AiNightCamp` jest wolane z `OnHourly` gracza (`:155`) PO wczesnych wyjsciach:
    `NightRestEnabled` (`:88`), gracz zywy (`:90`), gracz nie-Nieumarly (`:92-93`). Gracz grajacy Innym albo z wylaczonym snem = swiat
    przestaje obozowac.
11. **Gracz w armii AI dostaje dlug snu, gdy wodz idzie noca** (15% nocy) [K] - zwolniony jest tylko w sluzbie ROT (`:111, 735`),
    choc w armii o postoju decyduje wodz.
12. **Meldunek kursu zaniza czas o 20%** [K] - `Wayfinder.cs:63`: `rideH = dist / speed`, a partia robi `speed / 1.2` jedn. na godzine
    (`MobileParty.cs:2800`, `Campaign.cs:874`). Powinno byc `dist x 1.2 / speed`. Do tego `:64` zaszyte 18 h marszu.
13. **Most liczony jak brod** [K] (`TerrainEase.cs:81-82`, za vanilla `DefaultPartySpeedCalculatingModel.cs:285`). Historycznie most to
    szybka przeprawa; brod wolna. Drobne (kilka scian siatki).
14. **Nieaktualne komentarze** [K] - `WorldPace.cs:14-22` i `Settings.cs:313` mowia o roku 168 dni; `Settings.cs:693` i hint
    `McmSettings.cs:2439` "(22-4)".

---

## 4. Propozycje

Parametry dobrane przeze mnie; Jeffa pytam tylko o zmiany rozgrywki (rozdz. 4.3).

### 4.1 Obozy

| # | Co zmienic | Liczby i uzasadnienie | Prio | Wielkosc | Zmienia gre gracza | Nowa kampania | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|---|
| O-1 | Okno obozu jako klucze `CampStartHour` / `CampEndHour` i jedna funkcja `NightRest.InCamp(h)` (z przejsciem przez polnoc); uzyc w oknie AI, straznika, namiotow, pytaniu o oboz, auto-namiocie, bandytach "dziennych" | **0 i 6** - decyzja Jeffa; 6 h = `SleepHoursNeeded` | P0 | mala | tak (pytanie o oboz o 0:00 zamiast 21:00) | nie | niskie: AI idzie 1 h dluzej (+3% drogi), 2 h marszu 22-24 z kara nocna | konflikt tekstowy w `Settings.cs`/`McmSettings.cs`/CHANGELOG z 169-171 - scalac PO nich |
| O-2 | Straznik snu i odswiezanie namiotow co 0.1 h GRY zamiast 1 s / 2 s realnych; namiot zdejmowany, gdy oddzial zjedzie > 0.3 jedn. od legowiska (zamiast "stoi od 2 s") | 0.1 h = najwyzej 0.1 x 1.73/1.2 = 0.14 jedn. jazdy armii przed powrotem (dzis do 5.7) | P0 | mala | tak (namioty nie jezdza) | nie | koszt: ok. 750 odczytow pozycji co 6 min gry (przy x8 co ok. 0.04 s) - pomijalny; zadnych wizerunkow wiecej niz dzis | O-1 |
| O-3 | Zamiast lapac po fakcie: prefiks na `MobileParty.SetMove*` (GoToSettlement, GoToPoint, Besiege, Defend, Patrol, Engage, Escort, GoAround) - spiacy w oknie i bez zagrozenia nie dostaje rozkazu; rozkaz zapamietany i oddany o 6:00 | zero zjazdu | P1 | srednia | nie | nie | srednie: SCA powtarza rozkaz co 4 h - bez szkody; nasz `GiveOrderBack` musi isc PO zdjeciu z listy (dzis tak jest, `:417-421, 465-467`) | O-1, O-2 |
| O-4 | AI tez placi za noc bez snu: wpiac martwy `AiNightsAwakeInChase` - partia AI, ktora nie spala w oknie (15%, poscig, alarm), dostaje nastepnego dnia -15% predkosci i -15% morale; po 2 nocach -30% | polowa kary gracza (-25%), bo AI nie odsypia w dzien - prostsza ksiegowosc; liczba: dlug 1 noc | P2 | srednia | posrednio (AI wolniejsze po nocnym poscigu) | nie | srednie: kolejny postfix w lancuchu predkosci - licznik `SpeedDepth`; stan w zapisie (`SaveText`) | O-1 |
| O-5 | Gracz w armii AI: noc liczona jak w sluzbie ROT (bez dlugu, gdy wodz maszeruje) | o postoju decyduje wodz | P2 | mala | tak | nie | niskie | - |
| O-6 | `AiNightCamp` wolac osobno z godzinowego ticku, nie zza wyjsc gracza (`:88-93`) | swiat obozuje niezaleznie od gracza | P2 | mala | nie | nie | niskie | O-1 |
| O-7 | Czlonkowie armii w drodze na zbiorke (niedoczepieni) tez spia | jak wszyscy lordowie | P2 | mala | nie | nie | armie zbieraja sie ok. 6 h dluzej | `MaximumWaitTime` zbiorki |

### 4.2 Tempo swiata (realne odleglosci)

Kolejnosc jak w `AUDYT-CZAS-MAPA.md` rozdz. 8, poprawiona o blad 3.1.

| # | Co zmienic | Liczby i uzasadnienie | Prio | Wielkosc | Zmienia gre | Nowa kampania | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|---|
| T-1 | WorldPace jako MNOZNIK wyniku: w najbardziej zewnetrznym `CalculateFinalSpeed` (po TerrainEase, przed sufitem MarchPace) wpis `Add((p-1) x wynik / (1 + suma czynnikow))` jak w `MarchPace.cs:131-135`; wpis na bazie (`WorldPace.cs:56`) usunac | przy 75% jazda 4.16 -> 3.86 (-7%), piechota bez zmian | P1 | mala | malo | nie | podwojne liczenie w lancuchu - `SpeedDepth.OutermostFinal` jak MarchPace | - |
| T-2 | Podloga = `1.0 x p` (`__result.LimitMin(p)` w tym samym postfiksie - LimitMin nadpisuje) | duze armie znow zwalniaja | P1 | mala | nie przy 75% | nie | niskie | T-1 |
| T-3 | Kary TerrainEase x p (las, pustynia, snieg, bagno, brod, noc) | kara ma byc tym samym ULAMKIEM drogi przy kazdym tempie | P1 | mala | nie przy 75% | nie | niskie | T-1 |
| T-4 | `Campaign.Estimated*Speed` (8 pol, settery publiczne) x p x 18/24 po kazdym wczytaniu | AI liczy zasieg celow, opoznienia, patrole na prawdziwe tempo i 18 h marszu | P1 | mala | nie | nie | srednie: zmienia wybor celow AI - zmierzyc liczbe oblezen i dlugosc wojen (KRONIKA WOJEN) | T-1, O-1 |
| T-5 | Osobne tempo: `VillagerPacePercent` **40**, `CaravanPacePercent` **30**, `SeaPacePercent` **28** | wies: kurs do miasta (sr. 32 jedn.) ok. 1 doby - wioska "reprezentuje" okreg, prawdziwy targ byl 10-25 km [S]; karawana: 30-40 km/dobe [H: karawany jucznie, wiedza ogolna]; statek zaglowy 100-180 km/dobe calodobowo -> speed ok. 1.5 z ok. 5.5 | P1 | srednia | tak (handel) | lepiej tak | wysokie dla ekonomii: dostawy jedzenia, rynek uzbrojenia, kursy wozow (`MarketCarts`), BEE | paczki 164-168 (obieg) i 170 (BEE) - robic PO nich i z nowym rocznym autotestem |
| T-6 | Spojnosc armii x p (`ArmyCohesionPacePercent`, tylko ujemna czesc, po SCA) | armia nie moze rozpasc sie w polowie marszu | P1 | srednia | tak | nie | srednie | T-1 |
| T-7 | **Suwak swiata: 75 -> 20** (wariant L "jak w ksiazkach") - albo 12 (wariant H "historia") | tabela nizej; 20% daje armie 800 ok. 32 km/dobe = plan Stannisa (2.2) i Wegecjusza (2.1) | P1 | mala (liczba) | **TAK - pytanie 1** | lepiej tak (ekonomia skalibrowana na 75%) | wysokie bez T-1..T-6 | T-1..T-6; po paczkach 164-168 |
| T-8 | Poprawki tekstow: `WorldPace.cs:14-22`, `Settings.cs:313` (rok 364), `Wayfinder.cs:63-64` | patrz 3.12 | P2 | mala | tylko meldunek | nie | zero | O-1 |

Wariant docelowy po T-1..T-6 (mnoznik wyniku, oboz 0-6 = 18 h marszu, kary skalowane) [S]:

| Typ | km/dobe dzis | 30% (K z 04.10) | **20% (L, propozycja)** | 12% (H) | historia / lore |
|---|---|---|---|---|---|
| Goniec 5 jezdzcow | 281 | 109 | **73** | 44 | 40-60, poslaniec ok. 64 [H] |
| Lord 150, 1/3 konno | 184 | 74 | **50** | 30 | 25-40 [H][S] |
| Lord 150 pieszo | 162 | 67 | **45** | 27 | Wegecjusz 30 [H] |
| Armia 800 | 117 | 47 | **32** | 19 | Stannis 32 (plan), Henryk V 24 [H] |
| Armia 2000 | 84 | 34 | **22** | 13 | 13-19 z taborem [H] |

| Trasa | Goniec dzis / L / H | Lord 150 dzis / L / H | Armia 800 dzis / L / H | Armia 2000 dzis / L / H |
|---|---|---|---|---|
| Winterfell - KP | 9 / **33** / 56 | 13 / **49** / 82 | 21 / **77** / 129 | 29 / **109** / 181 |
| Winterfell - Castle Black | 5 / 18 / 31 | 7 / 27 / 45 | 12 / 43 / 71 | 16 / 60 / 100 |
| Winterfell - Riverrun | 7 / 25 / 42 | 10 / 37 / 61 | 16 / 58 / 96 | 22 / 81 / 135 |
| KP - Riverrun | 4 / 15 / 26 | 6 / 23 / 38 | 10 / 36 / 60 | 14 / 50 / 84 |
| KP - Casterly Rock | 7 / 25 / 42 | 10 / 37 / 62 | 16 / 58 / 97 | 22 / 82 / 137 |
| KP - Highgarden | 6 / 21 / 35 | 8 / 31 / 52 | 13 / 49 / 82 | 18 / 69 / 115 |
| KP - Storm's End | 3 / 11 / 18 | 4 / 16 / 26 | 7 / 25 / 41 | 9 / 35 / 58 |
| KP - Sunspear | 6 / 24 / 40 | 10 / 36 / 59 | 15 / 56 / 94 | 21 / 79 / 131 |

Uzasadnienie L: goniec Winterfell-KP 33 dni = szacunek podrozy Roberta (35-40 dni, 2.2); armia 800 idzie jak plan Stannisa; armia 2000
(13-22 km) jak duze armie z taborem. H daje armie scisle historyczne, ale goniec 56 dni jest wolniejszy niz lore. Przy roku 364 dni
i szybkim czasie x8 (doba ok. 10 s) marsz armii Winterfell-KP w L to ok. 13 minut realnych [S].

Skutki uboczne do zmierzenia (rok autotestu): jedzenie armii na marsz (800 ludzi x 0.6 / 20 = 24 jedn./dobe - 77 dni = 1 850 jedn.,
musza kupowac po drodze, twierdze co ok. 47 jedn. = co ok. 7 dni marszu armii w L); spojnosc (T-6); oblezenia (odsiecz idzie 3.7x dluzej);
dlugosc wojen i "markietani" w polu (projekt obiegu 5.3 - armia w polu dluzej = wiecej wydatkow w polu); ochotnicy (lord odwiedza mniej wsi).

### 4.3 Pytania do Jeffa (tylko to, co zmienia gre)

1. **Tempo swiata:** "jak w ksiazkach" (L: armia z Winterfell do KP ok. 2.5 miesiaca, goniec miesiac) czy "twarda historia"
   (H: armia 4 miesiace, goniec prawie 2)? We wrzesniu (03.09) prosiles o przyspieszenie o 50% ("predkosc za wolna") - dzis
   mamy 75%. Rekomendacja: L.
2. **Zima:** czy noc w obozie ma byc dluzsza zima (np. zima 22-8, wiosna/jesien 23-7, lato 0-6)? W ksiazkach zimowy marsz Stannisa
   stanal w sniegu. Rekomendacja: tak (pomysl N-1).
3. **Pytanie o oboz o 0:00** zamiast o 21:00 - zgodnie z "od 24 do 6"; czy 23:00 ("za godzine polnoc")? Rekomendacja: 0:00, jak powiedziales.

### 4.4 Rzeczy, ktorych nie mamy, a gra na nie pozwala

| # | Pomysl | Jak (co gra daje) | Prio | Wielkosc | Zmienia gre | Ryzyko |
|---|---|---|---|---|---|---|
| N-1 | **Dluga zimowa noc** - okno obozu zalezne od pory roku (lato 0-6, wiosna/jesien 23-7, zima 22-8) | `CampaignTime.Now.GetSeasonOfYear` i nasz `WesterosClimate` (dluga zima lat) - jedna funkcja `InCamp(h)` z O-1 | P2 | mala (po O-1) | tak - pytanie 2 | niskie; zima wolniejsza o 2-4 h marszu na dobe [S] |
| N-2 | **Dzien odpoczynku armii** co 7. dzien marszu (armia AI stoi dobe; spojnosc +2, morale +5) | historycznie armie odpoczywaly co kilka dni (krucjaty - niedziela) [H, wiedza ogolna]; licznik dni marszu w `_camping` | P2 | srednia | tak (AI 1/7 wolniejsze) | srednie (SCA) |
| N-3 | **Napad na spiacy oboz** - kto zaatakuje oddzial w oknie obozu (bez alarmu), ten ma obronce w nieladzie | `MobileParty.SetDisorganized` (uzywamy juz w `NightRest.cs:1039-1041`); spiacy = lista `_camping` | P2 | srednia | tak (nocne napady maja sens; styk N-5 z raportu 08 - ognisko obozu) | srednie (bitwy AI-AI tez) |
| N-4 | **Marsz forsowny dla gracza** (klawisz O, opcja "forced march": +25% predkosci, sen nie liczy sie wcale, dlug rosnie co 12 h) | lancuch predkosci (`NightRest.SpeedPostfix`) | P2 | mala | tak | niskie |
| N-5 | **Most szybszy niz brod** (most bez kary, brod -0.5) | `TerrainEase.cs:81-82` | P2 | mala | malo | zero |
| N-6 | **Goniec ze zmiana koni** - partia do 10 ludzi, wszyscy konno, z zapasowymi konmi (>= 1 na czlowieka w sakwach) dostaje +30% (sztafeta) | `MarchPace.CountColumn` juz liczy konie | P2 | mala | tak | niskie |
| N-7 | **Meldunek "ile dni drogi" dla armii, w ktorej jedziesz** i dla wodza wrogiej armii w zasiegu wzroku (z prawdziwego Speed, 1.2 i okna obozu) | `Wayfinder` | P2 | mala | tylko informacja | zero |

---

## 5. Do zrobienia tej nocy vs na pozniej

### 5.1 Tej nocy (male, bezpieczne, sprawdzalne autotestem 40 dob)

Wszystko w `Armoury/src/NightRest.cs`, `Settings.cs`, `McmSettings.cs` (przez `tools/gen_mcm.py`), `Wayfinder.cs`. **`Armoury.json` Jeffa
nie wymaga zmiany** (nowe klucze biora domyslne z kodu). Scalac PO paczkach 169-171 (te same pliki Settings/McmSettings/CHANGELOG).

**N-A. Oboz 0-6 (O-1).**
- `Settings.cs` (grupa "A night's rest", przy `:693`): `public int CampStartHour = 0;` `public int CampEndHour = 6;` (opis po angielsku:
  "hour the world makes camp / breaks camp; 0 and 6 = midnight to six"); poprawic opis `AiCampsAtNight` "(22-4)" na "(camp hours below)".
- `NightRest.cs`: helper `internal static bool InCamp(int h)` = `s < e ? (h >= s && h < e) : (h >= s || h < e)` (s, e z Settings, clamp 0-23).
- `:406` `bool night = h >= 22 || h <= 4;` -> `bool night = InCamp(h);` (ticki 0..5 -> spia 0:00-6:00; o 6:00 pobudka i oddanie rozkazu).
- `:852` i `:861` (`hh >= 22 || hh <= 4`) -> `InCamp(hh)`.
- `:138` `h == 21` -> `h == CampStartHour` (pytanie "Night falls" o polnocy); `:118` auto-namiot gracza: `night` -> `InCamp(h)`.
- `:107` sen gracza: `bool night = h >= 21 || h <= 5;` -> `bool night = h >= 21 || h <= CampEndHour;` (tick 6:00 liczy godzine 5-6 jako
  noc - inaczej oboz 0-6 daje 5.6 h i dlug; patrz 3.7). `:154` `h == 6` -> `h == CampEndHour` (rozliczenie PO doliczeniu ticku - kolejnosc
  w kodzie juz taka).
- `:512` bandyci "dzienni" `h >= 23 || h <= 5` -> `InCamp(h)` (jedna noc obozu dla wszystkich, ktorzy obozuja).
- Nie ruszac: `:250` (namiot dla stojacych kolumn 21-5 - to wyglad zmroku), `SightRange.cs:35`, `TerrainEase.cs:102` (noc gry 22-02),
  warsztaty 23-5.
- Efekt [S]: AI 17 -> 18 h marszu (+3% drogi, tabela 1.3); gracz z "always camp" idzie 21-24 dodatkowo (do +3 h/dobe).

**N-B. Straznik w czasie gry (O-2).** `NightRest.cs:847-862`: zamiast `(DateTime.Now - _lastTentRefresh).TotalSeconds > 2.0` i `> 1.0` -
pola `CampaignTime _lastHoldSweepT, _lastTentRefreshT` i warunek `(CampaignTime.Now - _lastX).ToHours >= 0.1`. W `RefreshNearbyTents`
(`:254-258`) do warunku zdjecia dodac `_bedPos` - zjazd > 0.3 jedn. = zdjac namiot (obudzony z wylaczonym AI tez).

**N-C. Meldunek kursu (3.12).** `Wayfinder.cs:63` `rideH = dist / speed` -> `dist * 1.2f / speed`; `:64` `24f / 18f` ->
`24f / (24f - godziny obozu)` z `CampStartHour/CampEndHour`.

**Co sprawdzic w autotescie 40 dob** (`Logs/<sesja>/noc.log` i glowny log):
- linie `AiNightCamp: 0:00 ... 5:00` (6 na noc), **zadnej** z 22:00 i 23:00; liczba spiacych podobna do dzis (ok. 400-750);
- `obudzony cudza reka ... zjechal X`: srednia < 0.3 (dzis 1.29), maks. < 1.0 (dzis 5.69);
- `obudzonych cudza reka od poprzedniej godziny` - moze wzrosnac (czestszy przeglad), to dobrze;
- brak `Log.Error` z `NightRest.*`, `HoldSleepers`, `RefreshNearbyTents`, `Tent (potkniecie`;
- `Audyt predkosci` bez zmian (godziny nie zmieniaja predkosci);
- zestawienie gospodarcze (ludzie, rynek) - bez skoku (AI +3% drogi).
- Recznie w grze (nie autotest): pytanie o oboz o 0:00; oboz 0-6 bez komunikatu o zarwanej nocy.

### 5.2 Na pozniej

- O-3 (blokada cudzych rozkazow u spiacych) - po N-B, jesli zjazd nadal > 0.3.
- T-1..T-4 (tempo jako mnoznik, podloga, kary terenu, szacunki AI) - jedna paczka "tempo swiata 2", bez zmiany suwaka (przy 75% prawie
  bez skutku w grze), autotest roczny.
- T-5, T-6, T-7 - po odpowiedzi Jeffa (pytanie 1) i PO paczkach obiegu 164-168 i BEE 170 (zmiana tempa zmienia dostawy wsi, karawany,
  czas wojen i wydatki armii w polu - kalibracja projektu obiegu jest liczona na dzisiejszym tempie).
- O-4..O-7, N-1..N-7 - wedlug priorytetow; N-1 po pytaniu 2.
- Jedna "noc" dla calego kodu (3.6) przy okazji T-3.
- Optymalizacja petli zagrozen w `AiNightCamp` (`:427-461`, ok. 750 x 1 500 odleglosci co godzine nocy) - na koniec (decyzja Jeffa 08.10).

### 5.3 Kolizje z paczkami w toku

- 169 (log obiegu), 170 (BEE), 171 (zbrojenie zalog): logicznie bez kolizji z N-A..N-C; kolizja tylko tekstowa (Settings/McmSettings/CHANGELOG).
- T-5/T-7 koliduja z kalibracja projektu obiegu (paczki 162-168) - nie zmieniac tempa swiata przed ich rocznym autotestem.
- Styk z `08-drogi-wioski-widok.md`: M-1 (jedna skala 4.77 - tu przyjeta), M-2 (premia za droge +15% - liczyc PO T-1, bo przy dzisiejszym
  dodawaniu czynnikow +15% byloby nieproporcjonalne), N-5 tamze (ognisko przy namiocie) - pasuje do N-3 tutaj.
