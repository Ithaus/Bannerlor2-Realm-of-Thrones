<!-- PROJEKT paczki H3 (noc 08/09.10, n9), galaz w-toku/n9-przegrani-uchodza od 2e235ea (= DLL w grze, 160 + 161).
     Status: SPECYFIKACJA PO KRYTYCE (15 uwag: 14 przyjetych, 1 czesciowo, 0 odrzuconych - rozdz. 13); KOD w drzewie n9-h3
     (Armoury/src/LosersFlee.cs + OutlawLaw.cs, build kod 0, NIEWGRANE - DO SPRAWDZENIA; roznice wobec projektu - rozdz. 14;
     poprawki po przegladzie kodu - 8 uwag, 8 przyjetych - rozdz. 15).
     Decyzja Jeffa jest (07.10, STAN-PRAC "Decyzje Jeffa 07.10", pyt. 5, hamulec H3).
     Wiazace: docs/audyt-2026-10-09/00-AUDYT-SWIATA-2026-10-09.md (2.8 "Straty w bitwie", E10, S16, R10), raport 04 W1,
     docs/HISTORIA-RABUNKU-I-BITEW-2026-10-07.md rozdz. 4 (13 bitew, wzor). Oznaczenia: [K] kod sprawdzony, [P] pomiar z logu,
     [H] historia / lore, [S] szacunek projektu. -->

# H3 - PRZEGRANI UCHODZA ZAMIAST GINAC (projekt)

## 0. Dla Jeffa (prosto)

1. **Dzis** w bitwie, ktora AI rozstrzyga bez Ciebie, gra bije az do ostatniego czlowieka przegranych. Kogo trafi, ten pada:
   zginal albo ranny. Rannych przegranych zwyciezca bierze do niewoli WSZYSTKICH, a tych kilku, ktorzy jeszcze stali, w 3/4
   po prostu kasuje. Nikt nie ucieka do domu. Wychodzi: przegrani traca ok. 46% zabitych, reszta to jency; zwyciezca w
   prawdziwej bitwie traci 11-14% zabitych. Chlopi z wozami napadnieci na drodze gina w 87%. W historii przegrani tracili 15-40% (przy poscigu konnicy
   albo w pulapce rzeki do ok. 60%), jencow bylo 1-20%, a reszta uciekala; zwyciezca tracil 1-5%.
2. **Po zmianie:** gra dalej sama decyduje, KTO wygral (nic w walce sie nie zmienia). Zmienia sie tylko to, co dzieje sie
   z pokonanymi po bitwie: ilu ginie, zalezy od sytuacji (czy zwyciezca ma konnice do poscigu, czy za plecami byla rzeka
   albo bagno, jaka przewaga, czy zawodowcy bili pospolite ruszenie) - od 5% (konni na otwartym polu) do 65%, typowo 15-30%. Do niewoli idzie
   kilka procent (glownie ciezkozbrojni - oni byli warci okupu). Reszta ucieka i kazdy wraca TAM, SKAD GO WZIETO:
   - zolnierz lorda: polowa w las (pula wyrzutkow - "zlamani ludzie" z ksiazek), polowa do domu - do najblizszej osady
     swojego krolestwa i swojej kultury (tam lordowie zwykle werbuja), jako ludnosc i ochotnicy u notabli;
   - bandyta: polowa wraca do lasu, polowa do wsi (bandyci to w wiekszosci chlopi, ktorych bieda wypchnela z wsi);
   - chlop z wozem albo rybak: ginie ok. 5%, reszta wraca do SWOJEJ wsi (wies odzyskuje dokladnie tyle, ile stracila, gdy go wyslala);
   - straznicy karawan walcza, wiec licza sie jak wojsko (wzor). Ocalali nie dopisuja sie nigdzie, bo gra daje karawanie ludzi
     dzis z niczego - to zamknie paczka 108 (wtedy pojda do swojego miasta).
   Zwyciezca traci zabitymi najwyzej 5% - reszta jego poleglych to ranni, ktorzy sie wylecza.
3. **Twoje bitwy w polu (misja) - bez zmian.** Tam decyduje walka. Jedyne, co sie zmienia: rozbici, ktorzy uciekli z pola
   (Twoi i wroga), nie znikaja juz w polowie - wracaja do domu albo w las. Kazda Twoja bitwa dostaje w logu jedna linie
   "pominieta (gracz)", zeby bylo widac, ze H3 jej nie ruszyl.
4. **Skutek dla swiata** (szacunek z trzech autotestow 40 dob): zabitych w bitwach AI w polu ok. 50% mniej, jencow ok. 88% mniej;
   ok. 385 ludzi dziennie zyje dalej: ok. 130 w las, ok. 120 zolnierzy do domow, ok. 100 chlopow i bandytow do wsi, ok. 30 ludzi
   karawan. Werbunek lordow u notabli zmieni sie od -3% do +11% (zwyciezcy maja mniej jencow do wcielenia, ale mniej poleglych) -
   test to zmierzy, osobny licznik wcielonych jencow.
   Do pilnowania: wiecej ludzi w lesie = moze byc wiecej band. Sa na to dwa suwaki awaryjne (ile zolnierzy i ile bandytow idzie w las)
   i test na caly rok, bo bandy rosna powoli (dzis po roku jest ich ok. 22.7 tys. ludzi, po 40 dobach 6.1-6.5 tys.).
5. **Pytanie do Ciebie jest jedno** (rozdz. 10): czy ta sama regula ma dzialac, gdy Ty klikasz "wyslij wojsko" / autobitwe
   (dzis: nie - zostawiam tak, jak jest, do Twojego slowa).

## 1. Jak gra dzis rozstrzyga bitwe bez gracza [K] (gra 1.4.8, dekompilacja `ore-supply/cs`)

### 1.1 Petla symulacji
- `MapEvent.Update` co 30 min gry (szturm 60 min; `DefaultCombatSimulationModel.GetSimulationTickInterval`) wola
  `SimulateBattleSessionForMapEvent` -> `SimulateBattleRound` (`MapEvent.cs:1107-1147`): `GetSimulationTicksForBattleRound`
  daje liczbe ciosow obu stron (`DefaultCombatSimulationModel.cs:241-280`, ok. `min(2 x wrog, N^0.6)`), kazdy cios to
  `SimulateSingleTroopHit` (`MapEvent.cs:958-986`): losowy bijacy i losowy trafiany, obrazenia
  `(0.5..1) x 40 x (sila bijacego / sila trafianego)^0.7 x przewaga` (`DefaultCombatSimulationModel.SimulateHit`, `:18-30`;
  BK postfiks +20% za strzemiona i +15% SiegePlanner - `VanillaModelTweakPatches.cs:313-346`; ROT podmienia tylko cios smoka -
  `ROTCombatSimulationModel`; CrashScribe tnie ciosy w Innych bronia ponizej T6 - `Mends.cs:1755`).
- Po kazdym ciosie `CalculateWinner` (`MapEvent.cs:1212-1268`): strona przegrywa, gdy nie ma juz zadnego stojacego
  (`NumRemainingSimulationTroops == 0`) albo gdy jej morale spadnie do 0 - wtedy `MapEventSide.Route()` (`MapEventSide.cs:1069-1081`)
  oglasza wszystkich jeszcze stojacych za rozbitych (`OnTroopRouted`: zdjeci z partii, wpisani do `RoutedInBattle`,
  `MapEventParty.cs:316-328`).
- Ustawienie `BattleState` na zwyciestwo wola od razu `OnBattleWon` (`MapEvent.cs:290-303, 1048-1056`), a dla bitwy bez gracza
  `CalculateAndCommitMapEventResults` (`:1411-1432`): kolejno `LootDefeatedPartyCasualties` (zloto "z cial" dla AI z
  `DiedInBattle` + `WoundedInBattle`), `LootDefeatedPartyItems`, `LootDefeatedPartyPrisoners`, `LootDefeatedPartyShips`,
  **`CaptureDefeatedPartyMembers`**, potem `CommitCalculatedMapEventResults`. Dla bitwy z graczem to samo wola
  `PlayerEncounter.DoApplyMapEventResults` (`PlayerEncounter.cs:1520-1523`) - po ekranie wyniku.
- Potem `FinalizeEventAux` -> zdarzenie `MapEventEnded` (tu slucha Armoury, DTE, Spoils) -> `MapEventSide.HandleMapEventEnd`:
  partia przegrana bez ludzi (albo bez zdrowych) ginie (`DestroyPartyAction`), wodz zostaje zbiegiem (`MapEventSide.cs:429-452`).

### 1.2 Kto ginie, kto jest ranny
- Cios trafia szeregowego, gdy `RandomInt(MaxHitPoints) < obrazenia` (`MapEventSide.cs:993`); wtedy rzut na przezycie
  `PartyHealingModel.GetSurvivalChance`: ranny (`OnTroopWounded` - zostaje w partii jako ranny, wpis `WoundedInBattle`)
  albo zabity (`OnTroopKilled` - zdjety z partii, wpis `DiedInBattle`). Bohaterowie osobno (rany, smierc z `DeathMark`).
- Czynny lancuch szansy przezycia: `NavalDLCPartyHealingModel` (owija model wczesniejszy, `BaseModel`) i `BKROTPartyHealingModel`
  (dziedziczy po grze, +0.03 za kazde z trzech blogoslawienstw; kolejnosc tych dwoch zalezy od ladowania, wynik ten sam) ->
  `DefaultPartyHealingModel.GetSurvivalChance`, ktory **RBM podmienia prefiksem** (`RBMCombat.CampaignChanges`,
  `OverrideDefaultPartyHealingModel`): szansa = `1 - 1 / (1 + chirurg + 0.02 x poziom)`, **RBM zgubil warunek "cios obuchem
  nie zabija"** (w grze tepy cios = zawsze ranny). Przyklad [S]: piechur poziomu 11 (tier 2), chirurg partii AI z medycyna 50
  (RBM bez mnoznika 0.25 gry dla AI): 1 - 1/1.72 = 42% przezywa, 58% ginie; bez chirurga przezywa 18%.
- Wniosek: o odsetku zabitych decyduje medycyna chirurga i poziom jednostki, a nie sytuacja na polu. Ginie ok. 40% trafionych
  w wojskach lordow z chirurgiem (prawdziwe bitwy: 36.5% zabitych na 54.5% rannych) i 65-85% u band i chlopow (niski poziom,
  bez chirurga), a przegranych trafia sie prawie wszystkich.

### 1.3 Pogrom pokonanej strony
`CaptureDefeatedPartyMembers` (`MapEvent.cs:1955-2045`), szanse z `GetCaptureMemberChancesForWinnerParties`
(`DefaultBattleRewardModel.cs:231-255`; NavalDLC tylko doklada morze):
- **ranni** przegranych - suma szans zwyciezcow = 1, czyli **kazdy ranny idzie do niewoli** (o ile po stronie zwyciezcy jest partia,
  ktora bierze jencow - nie tabor wsi, nie karawana, nie patrol);
- **zdrowi**, ktorzy jeszcze stali - suma szans 0.25: kazdy z szansa 25% do niewoli;
- potem `party.MemberRoster.AddToCountsAtIndex(-Number, -WoundedNumber)` - **wszyscy szeregowi znikaja z partii**, takze ci
  zdrowi, ktorych nikt nie wzial (75%) i ranni, gdy zwyciezca nie bierze jencow. Ci ludzie ida w nicosc.
- Wyjatki: `RetreatingSide != None` (strona sie wycofala - po 4 rundach poscigu, `GetPursuitRoundCount`) - funkcja wychodzi od razu,
  partia zachowuje ocalalych (to jest w grze "odwrot w porzadku"); `IsSurrendered` (poddanie bez walki) - wszyscy do niewoli.
- Dwie male dziury w samym pojmaniu (po krytyce): (a) `CanTroopBeTakenPrisoner(troop) == false` - jednostka nie idzie do niewoli,
  a i tak znika z partii (`MapEvent.cs:2013-2030`; dzis model gry zwraca zawsze `true`, NavalDLC tylko przekazuje dalej, BK i ROT go nie maja - sprawdzone);
  (b) losowanie zwyciezcy `FindWinnerPartyToGetCurrentLootObjectBasedOnChances` (`:1834-1847`) zwraca `null`, gdy los wypadnie ponad
  sume szans (suma po dzieleniu bywa odrobine mniejsza od 1) - wtedy `?.AddToCounts` gubi jenca (`:2019`). Rzadkie; H3 to mierzy (3.1 krok 6).
- **Jency pokonanych** (`LootDefeatedPartyPrisoners`, `:1529-1592`): szeregowi sa zdejmowani z lochu pokonanej partii zawsze (`:1544-1547`);
  gdy ktorys zwyciezca moze ich wziac, dolaczaja do niego JAKO LUDZIE (`RosterToReceiveLootMembers` - uwolnieni), a gdy zaden nie moze
  (`GetLootPrisonerChances`, `DefaultBattleRewardModel.cs:256-280`: tabory, karawany, milicje i patrole nie biora nikogo, bandy tylko
  bandytow), **znikaja**. Ta sama metoda co H3, ta sama zasada "w nicosc" - H3 to zamyka (3.1 krok 3a).
- `RoutedInBattle` (rozbici przy morale 0) gra niczego z nimi nie robi - ich juz nie ma w partii. Jedyny czytelnik w grze to
  `DesertersCampaignBehavior` (bandy dezerterow z rozbitych i ZABITYCH partii rodow pokonanej strony, `DesertersCampaignBehavior.cs:101-121`) -
  u nas wylaczony prefiksem `OutlawLaw.SkipVanillaDeserters` (`OutlawLaw.cs:733, 1634`), ale **tylko przy wlaczonym prawie wyrzutkow**
  (`Skip` zwraca `!On`); przy `OutlawLawEnabled = false` gra sklada dezerterow sama (gdy w swiecie jest klan dezerterow) - 3.2.

### 1.4 Kto jeszcze lata te miejsca (wszystkie mody z listy; nazwy metod szukane w UTF-8 i UTF-16) [K]
| Mod | Co | Wplyw na H3 |
|---|---|---|
| RBM (`RBMCombat.dll`) | prefiks `DefaultPartyHealingModel.GetSurvivalChance` (wyzej) | rzadzi dzis zgonem; po H3 tylko w bitwach z graczem i u zwyciezcy do limitu 5% |
| BKROTPatch | `BKROTPartyHealingModel.GetSurvivalChance` +0.03 x blogoslawienstwa | j.w. |
| BannerKings | postfiks `SimulateHit` (strzemiona, SiegePlanner), postfiks `GetLootedItemFromTroop` (LootScale), straznik nulla `OnSurgeryApplied` | nic do zmiany |
| ROT | `ROTCombatSimulationModel` (smoki), `EnlistmentPatches` wola `CalculateAndCommitMapEventResults` refleksja - tylko bitwy gracza na sluzbie | nasza latka przepuszcza bitwy gracza |
| ROT (dopisane po krytyce) | `ROT.HarmonyPatches.Dragon.LootCollectorPatch` - prefiks na TEJ SAMEJ `MapEvent.CalculateAndCommitMapEventResults`: usuwa z `ItemRoster` pokonanych smoki, wilki i ich miniatury, zawsze `return true` | zadnej kolizji (towar, nie ludzie); kolejnosc prefiksow obojetna |
| ROT `ROTTroopRecruiter` (dopisane po krytyce) | po kazdym werbunku lorda AI wymienia rekruta na jednostke z szablonu rodu i wola drugi raz `OnTroopRecruited(wodz, null, null, ...)` (`ROTTroopRecruiter.cs:105-122, 216`) | H3 nie dotyczy; ale kategoria `PeopleLedger` "bez osady" to w wiekszosci to echo (rozdz. 8, 9.6) |
| NavalDLC | modele `NavalDLC*` owijaja bazowe (`BaseModel`), zmiany tylko na morzu | morze wylaczone z H3 |
| DTE | `MapEventEnded`: cala zbrojownia pokonanych do zwyciezcow (`dte_ecb.cs:529-561, 751-827`); nie czyta `DiedInBattle` | bez zmian (rozdz. 8) |
| Spoils of War (`RealisticLoot`) | czyta `DiedInBattle / WoundedInBattle / RoutedInBattle` tylko w bitwach gracza (`LootCollectionBehavior.cs:3018`, `:464`) | bez wplywu |
| Diplomacy 1.4.7 | zmeczenie wojna z `MapEventSide.TroopCasualties` (licznik trafien w czasie walki, `WarExhaustionManager.cs:826-827`) | H3 go nie zmienia - zmeczenie jak dzis |
| StrategicCampaignAI145 | zadnej z tych metod ani modeli | kto wygrywa - bez zmian |
| AIInfluence | ma `CombatSimulationModel` / `EncounterModel` w nazwach, DLL zaciemniony (ilspycmd: blad); w `#Strings` brak siedmiu nazw metod H3 (skan krytyka, UTF-8 i UTF-16), wiec nie wola ich wprost; latki przez zaszyfrowany napis wykluczyc sie nie da | H3 nie zmienia modeli, tylko wynik po bitwie; **pomiar w grze:** `LosersFlee.ApplyAll` wypisuje wlascicieli latek (3.1) |
| RealisticBannerlord | `RealisticHealingModel`, `CriticalWoundBehavior` (rany po bitwie) | dotyczy rannych w partiach, nie podzialu |

### 1.5 Co dzis robi Armoury (drzewo n9-h3 = 2e235ea) [K]
- `OutlawLaw.OnMapEventEnded` (`OutlawLaw.cs:340-359`): z `RoutedInBattle` KAZDEJ bitwy (takze gracza) `OutlawRoutedShare` (0.5,
  `Settings.cs:565`; w `Armoury.json` Jeffa klucza nie ma - dziala domyslna) idzie do puli wyrzutkow regionu bitwy w swoim typie
  zolnierza; **druga polowa znika** (opis ustawienia mowi "instead of going home", ale kodu "do domu" nie ma).
- `OutlawLaw.Daily` (`:507-521`): z puli co dobe 0.5% (+2% x dobrobyt w pokoju) wraca "do wsi" - `ReturnHome` dodaje
  `OutlawHearthPerMan` = 0.5 hearth na czlowieka najbiedniejszej wsi regionu (`:242-250`). **Uwaga [P]:** w ksiedze ludzi
  (`PopulationLaw`) 1 hearth = ok. 195-200 ludzi (CSV `ludzie-regiony` 17-59-09: hearth 215 105 -> 235 734, ludzie wsi
  41.98 -> 47.06 mln), wiec jeden zolnierz wracajacy z puli dopisuje wsi ok. **100 ludzi ksiegi**. Dzis to male (powroty
  3-7 ludzi dziennie po 40 dobach; po roku 55-63 dziennie - log 18-38-27), po H3 nie (rozdz. 3.3).
- **Co jest w puli (po krytyce) [K][P]:** klucz `"~"` = prosci zdjeci z hearth (`TakeCommoners`, `:222-240`, takze pula startowa
  `Seed`); klucze typow = dezerterzy (`OnTroopsDeserted`), rozbici z bitew (`:354`) i **bandy rozwiazane razem z ich jencami**
  (`OnPartyDestroyed`, `:394-395`). Banda werbuje z puli najpierw typy, potem prostych, ktorych zamienia na najnizszego bandyte
  klanu albo lupiezce (`CommonerFor`, `Draw` / `DrawOnly`, `:271-292, 316-321, 638`) - wiec bandyta wracajacy do puli to
  w wiekszosci prosty z hearth pod kluczem bandyty. Linia "Wyrzutki:" liczy jako "zolnierzy" kazdy klucz inny niz `"~"`:
  w dobie 40 (18-13-16) "zolnierzy 1217" z 1343, przy naplywie "rozwiazane bandy" 50-122 dziennie i "rozbitkowie" 0.
- `PeopleLedger` (`PeopleLedger.cs:105-131`) i `BattleChronicle` (`BattleChronicle.cs:52-93`) - tylko log: zabici / ranni / rozbici
  z tych samych trzech list; "Bitwy:" liczy w "prawdziwych bitwach" (obie strony >= `BattleRealMinSide` 50, nie gorzej niz 1:4)
  srednia na bitwe `zabici / zdrowi na starcie`.
- `BattlefieldLaw` - tylko bitwy gracza (lup, loteria za poleglych wycieta, `BattlefieldLaw.cs:233-237, 559-569`).
- `DesertionLaw` / `WarLedger` - dezercja z zoldu i zaleglosci (nie bitwa); `SlowHealing` - tempo gojenia (AI 100%);
  `BrokenMen` - zachowanie rannych w misji (tylko pole gracza); `AiWear` - obicie lupu; `UniqueSpoils` - bohaterowie.
- Nikt w Armoury nie dotyka zgonu, ran ani pojmania w symulacji.

### 1.6 Liczby dzis
- Rok autotestu (00-AUDYT 2.2, raport 04 1.2) [P]: zabici we wszystkich starciach 389 675, w partiach rodow 185 533; w 1 313
  prawdziwych bitwach zwyciezcy 13.6%, przegrani 46.2% zabitych (srednia na bitwe).
- Trzy autotesty 40 dob z 08.10 (17:44, 17:59, 18:13; DLL po 161; `bitwy.log` 2 179 bitew, 3 x 40 dob) [P], przeliczone skryptem
  (scratchpad `h3scan/bitwy.py`, `proj.py`):

| Starcia (wagi = ludzie) | Bitew | Zwyciezcy: zabici / ranni | Przegrani: zabici / ranni (-> jency) / rozbici / stali |
|---|---|---|---|
| wszystkie | 2 114 | 3.7% / 4.9% | 49.1% / 45.9% / 2.5% / 2.6% |
| prawdziwe (obie >= 50, do 1:4) | 318 | 10.2% / 17.2% | 36.5% / 54.5% / 4.6% / 4.4% |
| nierowne | 1 796 | 2.0% / 1.7% | 63.3% / 36.1% / 0.1% / 0.5% |
| przegrany = tabor wsi | 378 | 2.3% | **85.4%** / 14.6% / 0 / 0 |
| przegrany = banda | 619 | 1.5% | 71.3% / 28.7% / 0 / 0 |

- Srednia na bitwe jak w linii "Bitwy:" (3 przebiegi): zwyciezcy 11.1 / 12.0 / 11.3%, przegrani 44.2 / 44.4 / 41.8%.
- Doba 40: partie rodow 102-105 tys., jency w partiach 3 129-4 528, pula wyrzutkow 553-1 343, bandy 6 114-6 546 ludzi;
  zabici 467-568 dziennie (partie rodow 222-306); rozbici ok. 20 dziennie.

## 2. Regula

### 2.1 Kogo dotyczy (faza 1)
| Bitwa | H3 | Dlaczego |
|---|---|---|
| AI z AI w polu (`IsFieldBattle`, ladowa), jest zwyciezca | **TAK** | ok. 87% starc (`bitwy.log` wszystkich sesji 08.10: FieldBattle 2 574 z 2 947; Raid 336, reszta 37) |
| j.w., partie rodu gracza prowadzone przez towarzyszy (bez gracza w bitwie) | **TAK** | jedna regula swiata |
| strona sie wycofala (`RetreatingSide`) | nie - gra (poscig 4 rundy, ocalali zostaja w partii) | to jest "odwrot w porzadku" (5-25% [H]) |
| poddanie bez walki (`IsSurrendered`) | nie | nie bylo bitwy |
| szturm (`Siege`), wypad, bitwa pod murami, blokada, rabunek wsi (`Raid`) | nie (faza 1) | po szturmie inne prawo (rzez albo kapitulacja); rabunek ma paczke 113 |
| morze (`IsNavalMapEvent`) | nie | statki, tonacy - osobna mechanika NavalDLC |
| z udzialem Innych (`Undead.Party`, `Undead.cs:50`) | nie | Inni nie biora jencow, umarli wstaja - watek R2/R4/T2. Rozbici z takiej bitwy (podzial 3.2 biegnie przy KAZDEJ bitwie): zywi - jak wszedzie; **wighty - ani w las, ani do domu** (z niczego, do niczego; po przegladzie kodu, rozdz. 15 uwaga 6) |
| z graczem w polu (misja) | nie - **walka decyduje**; tylko rozbici nie znikaja (2.4) | decyzja Jeffa |
| symulacja z graczem (autobitwa, "wyslij wojsko") | TAK - F1, decyzja Jeffa 09.10 F, `LosersFleePlayerAuto` (bitwa z walka w polu, potem "wyslij wojsko" - jak pole) | pytanie 10.1 - rozstrzygniete |

### 2.2 Ilu przegranych ginie
Dla strony przegranej (szeregowi, bez bohaterow; sklad = to, co partia miala przed bitwa: w partii + polegli + rozbici):

**p = 0.15 + 0.35 x C + 0.15 x T + 0.15 x O + 0.20 x Q - 0.10 x F x (1 - T)**, w granicach **5-65%**

- **C - poscig konnicy** = max(0, udzial konnych u zwyciezcy - udzial konnych u przegranego). Konny: `IsMounted` jednostki.
- **F - konni przegrani uciekaja** = max(0, udzial konnych u przegranego - udzial konnych u zwyciezcy). C i F wykluczaja sie.
  To jest "czlon ujemny" z audytu (2.8, E10, R10): bez niego minimum wzoru to 15%; rycerstwo konne pokonane przez pieszych
  uchodzilo albo szlo do niewoli za okup [H] (Bremule 1119: 3 zabitych na ok. 900, Lincoln 1217: 3 zabitych, Bouvines 1214: 5-13% rycerzy).
  **F liczy sie tylko poza pulapka terenu (T = 0)** (po krytyce): kon nie pomaga w rowach, bagnie i nad rzeka - pod Courtrai
  zginelo 40-60% francuskiej konnicy [H] (HISTORIA 4.1).
- **T - pulapka terenu** = 1, gdy w miejscu bitwy albo tuz obok jest teren, ktory zamyka ucieczke: Fording, Lake, River, Swamp,
  Bridge, Beach, NonNavigableRiver, UnderBridge, Canyon, Cliff (`TerrainType` z `TaleWorlds.Core`); inaczej 0 [H] (Towton - rzeka
  Cock za plecami, Stoke - wawoz, Courtrai - rowy i strumien, Azincourt - bloto). **Sprawdzane w 9 punktach** (po krytyce): srodek
  (`MapEvent.EventTerrainType` = sciana pod bitwa, `MapEvent.cs:1162`) i po 4 punkty na kole o promieniu 1 i 2 jednostek mapy
  (`Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition`, `IMapScene.cs:18`). Sam srodek nie wystarcza: z NavalDLC bitwa
  stojaca NA rzece albo jeziorze jest morska i wypada z H3, wiec T = 1 wychodziloby tylko na scianie brodu, mostu, plazy, bagna,
  wawozu albo urwiska dokladnie pod bitwa. Promien to jedna stala w kodzie; udzial bitew z T = 1 jest w linii dnia (rozdz. 5, 7).
- **O - przewaga** = clamp((ludzie zwyciezcy / ludzie przegranego - 1.5) / 1.5, 0, 1); ludzie = suma `HealthyManCountAtStart`.
  Od 3:1 O = 1 (okrazenie). Gdy przegrany nie ma szeregowych - nic do liczenia (partia pominieta); gdy zwyciezca nie ma - O = 0.
- **Q - jakosc** = clamp((sredni tier zwyciezcy - sredni tier przegranego) / 3, 0, 1); tier = `CharacterObject.Tier`
  (poziom 1 -> 0, 11 -> 2, 21 -> 4, 31 -> 6). Zawodowcy na pospolite ruszenie [H] (Visby, Formigny). Pusta strona (sami bohaterowie)
  -> Q = 0. Kazdy mianownik zero -> skladnik 0 (zadnego NaN w p).
- **Tabor wsi i rybacy** (`IsVillager`; `FishingPartyComponent` dziedziczy po `VillagerPartyComponent`, wiec `IsVillager` tez,
  `MobileParty.cs:4175`): zamiast wzoru stale **5%** (`NonCombatantDeathPercent`) [H]: strona niewalczaca, ta sama skala co przy
  rabunku 1-5% (HISTORIA 4.3, 2.2).
- **Karawana** (`IsCaravan`) - **wzor jak wojsko** (po krytyce): zaloga to uzbrojeni straznicy (`occupation="CaravanGuard"` we wszystkich
  `caravan_guard_*` i `caravan_master_*` ROT), ktorzy walcza; ginie tyle, ile wynika z poscigu, przewagi i jakosci (typowo 30-45%).
- (Bez pardonu N = +0.25 - tylko Inni; w fazie 1 bitwy z Innymi sa poza H3.)

Dlaczego te wagi: HISTORIA 4.2-4.3 - wiekszosc ginela po zlamaniu szyku, w poscigu i przy dobijaniu; do zlamania szyku straty
ok. 5% (Sabin), przegrani ok. 14%, zwyciezcy ok. 5% (Krentz 1985); masowo przy poscigu konnicy, w pulapce terenu, przy okrazeniu
i gdy zawodowcy bili chlopow. Wagi sa z HISTORIA 4.3 bez zmian, F dodany wedlug audytu.

### 2.3 Ilu do niewoli
Sposrod tych, co przezyli: tier 4 i wyzej **30%** (`LoserCaptiveVeteranPercent`), reszta **5%** (`LoserCaptiveCommonPercent`);
karawana tak samo (straznicy to wojsko). **Tabor wsi / rybacy: 5% tylko wtedy, gdy kultura frakcji zwyciezcy jest na liscie kultur
z niewolnictwem, inaczej 0** (po krytyce; HISTORIA 4.3 "jency 0-5% (tylko kultury z niewolnictwem)", decyzja Jeffa 07.10 pyt. 6a).
To ta sama lista, ktora ma dostac rabunek osada po osadzie (PROJEKT-RABUNEK rozdz. 3: "kultura frakcji napastnika na liscie
niewolnictwa", bez Pentos i Lorath - decyzja Jeffa 07.10 pkt 5) - jedna lista w kodzie, `LosersFlee.SlaveCulture(culture)`, dopoki
rabunek jej nie przejmie. Sklad [H] wedlug Martina: Zatoka Niewolnicza (`ghiscari`), Volantis, Lys, Myr, Tyrosh, Qohor, Norvos
(`volantine`, `lyseni`, `myrish`, `tyroshi`, `qohorik`, `norvos`), Valyria (`valyrian`), Dothrakowie i Zelazni Ludzie (thralls) -
id tych dwoch kultur ROT ustalic przy kodzie po nazwie kultury. Nie prawo BK `SlaveryAserai` / `SlaveryStandard`: BK nadaje
prawa niewolnictwa krolestwom wedlug wlasnych regul, a decyzja Jeffa mowi o kulturach swiata Martina.
Bohaterowie - jak dzis (gra). Gdy po stronie zwyciezcy nie ma partii, ktora bierze jencow - 0. Gdy
`BattleRewardModel.CanTroopBeTakenPrisoner(troop) == false` - 0 dla tego typu (zamiast znikac, uchodzi).
[H] jency przegranych 1-20%, prawie tylko szlachta: Poitiers 13-21%, Azincourt 5-18%, Otterburn 13%, Verneuil 1%, Neville's Cross
ponizej 1%; prosty zolnierz do ok. 1400 rzadko wart okupu (Ambuhl 2013). Wychodzi zwykle 3-15% przegranych.

### 2.4 Reszta uchodzi - dokad (jeden podzial, bez ludzi z niczego i w nicosc)
Kazdy szeregowy przegranej partii trafia dokladnie do jednej z trzech list gry: **zabici** (`DiedInBattle`), **jency**
(`WoundedInBattle`, zostaja w partii jako ranni, wiec `CaptureDefeatedPartyMembers` bierze ich na pewno), **rozbici**
(`RoutedInBattle`). Suma trzech = ilu partia miala (S16; to kontrola kodu - prawdziwe sprawdzenie bez podwojnego dopisania robi
3.1 krok 6 i 3.4, po krytyce). Rozbici (te same, ktore gra tworzy przy morale 0 -
takze w bitwach gracza) rozdziela jedno miejsce: `OutlawLaw.OnMapEventEnded`:

| Kto uciekl | W las (pula wyrzutkow regionu bitwy, w swoim typie) | Reszta - "do domu" wedlug pochodzenia (`LosersFlee.SendHome`) |
|---|---|---|
| banda (`IsBandit` / komponent "Bandit") | **`OutlawBandRoutedShare` = 0.5** (NOWY suwak, po krytyce) | wedlug zawodu jednostki (nizej): bandyta -> wies regionu bitwy |
| tabor wsi, rybacy (`IsVillager`) | 0 | 100% do **swojej wsi**: `Village.Hearth += 0.5 x n` |
| karawana (`IsCaravan`) | 0 | 100% **"z szablonu"** - nigdzie sie nie dopisuja, licznik (do E7 / 108) |
| zaloga, milicja, patrol (`IsGarrison`, `IsMilitia`, `IsPatrolParty`) | `OutlawRoutedShare` 0.5 (jak dzis) | **"z szablonu"** - licznik (do E7 / 108) |
| partie rodow i inne partie z wodzem | `OutlawRoutedShare` = **0.5** (bez zmian - S16, jeden podzial) | wedlug zawodu jednostki (nizej) |

**Pochodzenie = dom (po krytyce, "zolnierz wraca tam, skad go wzieto").** Kto z czego powstal, do tego wraca - tylko tam, gdzie gra
naprawde zabrala czlowieka:
1. **Chlopi i rybacy -> hearth wlasnej wsi.** Gra zdejmuje ich z hearth przy wysylaniu taboru: `Hearth -= (n + 1) / 2`
   (`VillagerCampaignBehavior.cs:179, 187`; NavalDLC rybacy tak samo, `FishingPartyCampaignBehavior.cs:186, 227`). Powrot to
   dokladna odwrotnosc: `HomeSettlement.Village.Hearth += 0.5 x n` (stala 0.5 jak w grze, nie suwak). Ludnosci BK NIE dopisujemy
   (BK ich nigdy nie stracila); gdy wies nieznana - najbiedniejsza wies regionu bitwy (`OutlawLaw.ReturnHome`). Do puli nie idzie nikt.
2. **Zalogi karawan, patroli, garnizonow i milicji -> nigdzie ("z szablonu").** Gra tworzy je z szablonu bez ubytku ludnosci
   (00-AUDYT 2.11: "Zalogi nowych karawan i taborow z szablonu", "Przyrost zalog z niczego", "Milicja z niczego"; patrole -
   `PatrolPartiesCampaignBehavior.cs:649-651`). Dopisanie ich do ludnosci BK tworzyloby ludzi z niczego; ich odejscie zamyka te sama
   dziure w druga strone. Licznik "z szablonu" w linii dnia. Po E7 / 108 (zaloga z ludzi miasta / regionu) zmienia sie tylko ten wiersz.
   Polowa rozbitych zalog, milicji i patroli idzie do puli jak dzis (S16) - bez zmian wobec stanu obecnego. Garnizon jest mieszany
   (glownie przyrost z niczego, +32.8 tys. w roku, ale tez zolnierze zostawieni przez lordow - "wymiana z garnizonem"); w polu nie
   walczy, rozbitych ma tylko w szturmach i wypadach (poza wzorem H3). Liczony osobno ("z szablonu: garnizony"), zeby bylo widac skale;
   gdyby byla duza - garnizon wedlug zawodu jak partie rodow.
3. **Pozostali (partie rodow, bandy, inne) - wedlug zawodu jednostki** (`CharacterObject.Occupation`; w ROT 840 z 858 jednostek
   `ROT-Troops.xml` to `Soldier`, 22 `Bandit`, 4 `Mercenary`; chlopi `Villager`, straznicy karawan `CaravanGuard`):
   - `Soldier`, `Mercenary` -> **ludnosc BK** przez `PopulationData.UpdatePopFromSoldiers(troop, n)` - ta sama metoda, ktora BK oddaje
     zolnierzy przy rozwiazaniu partii (`BKClanBehavior.cs:1205-1236`, tam tez tylko `Occupation == Soldier`) i lustro tego, co BK
     zabiera przy werbunku (`MilitaryData.DeduceManpower`, `RecruitmentApplyInternalPatch.cs:20`): klasa (`GetCharacterManpowerType`) + manpower;
   - `Bandit`, `Villager` i reszta -> **hearth** najbiedniejszej wsi regionu bitwy (`OutlawLaw.ReturnHome`, 0.5 na czlowieka -
     symetrycznie do `TakeCommoners`, skad bandyta w wiekszosci pochodzi, 1.5);
   - `CaravanGuard` poza karawana (np. wcielony jeniec) -> "z szablonu", jak w pkt 2.
4. **Ktora osada BK (zolnierze, po krytyce).** BK zdejmuje rekruta z osady NOTABLA, u ktorego lord werbowal (`RecruitmentApplyInternalPatch.cs:20`),
   a nie z siedziby rodu - oddanie do siedziby wodza przesuwaloby ok. 118 ludzi dziennie (ok. 43 tys. rocznie) z regionow werbunku
   i frontu do kilkudziesieciu siedzib (a z nimi produkcje BEE - te same klasy, `BetterEconomyBridge.UpdateClassCount`,
   `PopulationData.cs:399`). Bez pamieci pochodzenia (gra jej nie ma) bierzemy najblizsze werbunkowi: **najblizsza miejscu bitwy osada
   z danymi BK nalezaca do frakcji partii (`MapFaction`), o kulturze jednostki** (uciekinier idzie do swoich, do najblizszej osady
   swojego kraju i swojej mowy); brak -> najblizsza osada frakcji; frakcja bez osad -> najblizsza osada kultury jednostki; na koniec
   warownia regionu bitwy. Wynik w pamieci na (bitwa, frakcja, kultura). HISTORIA 4.3 "ksiega ludzi swojego regionu", raport 04 W1
   "region / karczma swojej kultury" - to samo w przyblizeniu. Przesuniecie widac w nowym pliku `h3-domy.csv` (3.4) obok kolumny
   `zwerbowani_dzis` z `ludzie-regiony.csv` (= to, co BK zdjal: `DeduceManpower` biegnie tylko przy werbunku z osada, a te ksiega
   przypisuje regionowi osady).
5. Gdy BK nie przyjmie zolnierza (wyjatek, brak danych) - zastepczo klasa chlopow (3.4), a gdy i to zawiedzie - pula regionu jako
   "bez domu". **Nikt, kto skads przyszedl, nie znika; kto przyszedl z niczego, wraca do niczego jawnie, z licznikiem.**

Dlaczego tak, a nie "ksiega" (108) ani karczma (167), jak w audycie (E10: "108 albo tymczasowo karczma 167"; raport 04 W1): obu nie
ma w grze, a H3 ma isc przed grupa C (E10). Decyzja projektu (rozdz. 9.5): do czasu 108 dom zolnierza = ludnosc BK, bo stamtad BK go
zabral; chlopi = hearth, bo stamtad zabrala ich gra; zalogi z szablonu - nigdzie. Gdy wejdzie 108 / 167, zmienia sie tylko
`LosersFlee.SendHome` i powrot z puli (3.3) - nic wiecej.
Historia [H]: uciekinierzy wracali do domu albo do pana (HISTORIA 4.3); bandy z rozbitych to w zrodlach raczej zwolnieni najemnicy
(brak stopy); w lore Westeros "zlamani ludzie" (broken men) to wlasnie prosci zolnierze rozbici wojna, ktorzy schodza na zly
tor (AFFC, mowa septona Meribalda, rozdz. Brienne V) - stad udzial w las jest, ale nie calosc.

### 2.5 Zwyciezca
Zabici zwycieskiej partii najwyzej **5%** jej ludzi (`WinnerDeathCapPercent`); poleglych ponad limit gra liczy jako rannych
(wracaja do partii ranni, goja sie). [H] zwyciezca 1-5%: Crecy ok. 2%, Courtrai 1-4%, Roosebeke 1%, Dupplin 2%; antyk ok. 5%
(Krentz). Dzis w nierownych starciach zwyciezca traci 2% (bez zmian), w prawdziwych bitwach 10-14% (spadnie do <= 5%).
**Decyzja (po krytyce): 5% stale, takze przy rownych silach (O = 0, Q = 0).** Wyjatki z HISTORIA 4.1 (Sempach 13-60%, Visby 12-15%)
to bitwy, w ktorych przegrany nie mial dokad uciec albo nie bylo pardonu (Gotlandczycy pod zamknietymi murami, rycerstwo austriackie
spieszone w zwarciu) - zwyciezca placil za walke do konca. W H3 przegrany ucieka, a "bez pardonu" w grze maja tylko Inni (poza H3).
Limit liczony na partie i tylko w dol (gdy gra zabila mniej, nic nie zmieniamy).

### 2.6 Sprawdzenie wzoru na bitwach (HISTORIA 4.3, z F) [S/H]
T w grze to 0 albo 1 (Visby: zamkniete mury to nie teren -> T = 0). F oszacowany [S]; przy T = 1 F nie dziala (po krytyce).

| Bitwa | C | T | O | Q | F | p | Historia |
|---|---|---|---|---|---|---|---|
| Neville's Cross 1346 | 0.2 | 0 | 0 | 0 | 0 | 22% | 8-25% |
| Poitiers 1356 | 0.1 | 0 | 0 | 0 | 0 | 18.5% | 16-18% |
| Crecy 1346, dzien 1 | 0 | 0 | 0 | 0 | 0.4 | 11% | ok. 19% zbrojnych (cala armia mniej) |
| Halidon Hill 1333 | 0.35 | 0 | 0 | 0 | 0 | 27% | ok. 19% |
| Courtrai 1302 | 0 | 1 | 0 | 0 | 0.35 (nie liczy sie, T = 1) | 30% | 12-19% armii, 40-60% konnicy |
| Towton 1461 | 0.1 | 1 | 0 | 0 | 0 | 33.5% | ok. 30% |
| Patay 1429 | 0.8 | 0 | 0 | 0 | 0 | 43% | 40%+ |
| Stoke 1487 | 0.2 | 1 | 0 | 0 | 0 | 37% | ok. 50% |
| Formigny 1450 | 0.5 | 0 | 0.3 | 0 | 0 | 37% | 58-63% |
| Visby 1361 | 0 | 0 | 0 | 1 | 0 | 35% | 59-90% |
| Verneuil 1424 (bez pardonu - w grze nie ma) | 0.1 | 0 | 0 | 0 | 0 | 18.5% | 38-57% |
| St Albans 1455 | 0 | 0 | 0 | 0 | 0 | 15% | 5% |
| Azincourt 1415 | 0 | 0 | 0 | 0 | 0.1 | 14% | 25-50% |

W widelkach albo do 4 pkt od nich: Neville's Cross, Poitiers, Towton, Patay; 7-10 pkt za wysoko: Halidon, Courtrai, St Albans;
za nisko: Crecy (8 pkt), Stoke, Formigny, Visby, Verneuil, Azincourt - skrajnosci (scisk w blocie, podwojne okrazenie, zamkniete
mury, "bez pardonu"), ktorych gra nie zna. Srednio wzor daje 15-30% dla typowych bitew, jak w HISTORIA 4.3. F lekko obniza Crecy
i Azincourt (tam T = 0 - wzor nie zna "scisku w blocie"), Courtrai zostaje bez F (30%, blizej strat konnicy); w grze F dziala glownie
przy lordach z jazda pobitych przez pieszych na otwartym polu - tam, gdzie zrodla pokazuja ucieczke albo niewole rycerzy (Bremule, Lincoln, Bouvines).

### 2.7 Typowe starcia w grze [S]
| Starcie | C | T | O | Q | F | p | jency | rozbici |
|---|---|---|---|---|---|---|---|---|
| lord (30% konnych, tier 2.5) bije bande pieszych (tier 1) 1:5 | 0.25 | 0 | 1 | 0.5 | 0 | ok. 50% | ok. 2.5% | ok. 48% -> pol las, pol wies |
| lord na lorda, sily i sklad podobne | 0 | 0 | 0 | 0 | 0 | 15% | ok. 8% (20% tier 4+) | ok. 77% (pol las, pol dom - osada BK) |
| lord na lorda 2:1, zwyciezca z jazda 40% wobec 10% | 0.3 | 0 | 0.33 | 0.1 | 0 | ok. 32% | ok. 7% | ok. 61% |
| to samo nad rzeka | 0.3 | 1 | 0.33 | 0.1 | 0 | ok. 47% | ok. 5% | ok. 48% |
| lord / banda napada tabor wsi | - | - | - | - | - | 5% | 0 (5% - zwyciezca z kultury z niewolnictwem) | ok. 95% -> swoja wies (hearth) |
| lord (30% konnych) napada karawane 3:1, straznicy tier 2-3 | 0.2 | 0 | 1 | 0 | 0 | ok. 37% | ok. 6% | ok. 57% -> "z szablonu" |
| konny poczet (60% konnych, polowa tier 4+) pobity przez tylu samo pieszych | 0 | 0 | 0 | 0 | 0.6 | 9% | ok. 16% (rycerze) | ok. 75% |
| to samo w rowach / nad rzeka (Courtrai) | 0 | 1 | 0 | 0 | 0.6 (bez skutku) | 30% | ok. 13% | ok. 57% |

## 3. Gdzie zmienic (kod)

### 3.1 NOWY `Armoury/src/LosersFlee.cs` - latka na wynik bitwy
**Latka:** Harmony **prefiks** na `MapEvent.CalculateAndCommitMapEventResults` (metoda `internal`, instancyjna, bez argumentow;
`AccessTools.Method(typeof(MapEvent), "CalculateAndCommitMapEventResults")`), `Priority.First`, zawsze `return true`
(oryginal biegnie zawsze), oraz **postfiks na tej samej metodzie - tylko pomiar** (krok 6, po krytyce). Rejestracja
`LosersFlee.ApplyAll(_harmony)` w `SubModuleMain.cs` zaraz po `OutlawLaw.ApplyAll` (`:83`), z linia startowa "LosersFlee (H3): latka
na wynik bitwy wpieta" albo "... nie znaleziono metody - H3 spi". **W tej samej linii wlasciciele latek** (po krytyce, zamiast
"nie sprawdzone" przy AIInfluence): `Harmony.GetPatchInfo` dla `MapEvent.CalculateAndCommitMapEventResults`,
`MapEvent.CaptureDefeatedPartyMembers`, `MapEvent.LootDefeatedPartyPrisoners` i `DefaultPartyHealingModel.GetSurvivalChance` -
lista `Owners` (oczekiwane: nasze, ROT `LootCollectorPatch`, RBM; kazdy inny wlasciciel = sprawdzic przed testem).
Trzecia, mala latka (tylko licznik, po krytyce): postfiks na prywatnej `RecruitPrisonersCampaignBehavior.RecruitPrisonersAi(MobileParty,
CharacterObject, int, int)` (`RecruitPrisonersCampaignBehavior.cs:111-118`) - ilu jencow wcielaja partie rodow AI na dobe (rozdz. 7, 8);
gdy metody nie ma - licznik "brak" i nic wiecej.
Dlaczego tu: to jedyne miejsce, przez ktore przechodzi kazda bitwa z wynikiem, PRZED lupem z cial i pojmaniem i PRZED
`MapEventEnded` - wszyscy pozniejsi czytelnicy (gra, DTE, OutlawLaw, BattleChronicle, PeopleLedger, AiWear) widza juz jeden,
poprawiony podzial. Nie ruszamy walki (`SimulateHit`, `GetSurvivalChance`) - te sa wspolne z misja gracza.

**Warunki wejscia** (kazdy niespelniony = licznik pominietych z powodem, nic nie zmieniamy):
`Settings.LosersFleeEnabled`; `!IsPlayerMapEvent`; `BattleState` = `AttackerVictory` albo `DefenderVictory`; `IsFieldBattle` i
`!IsNavalMapEvent` (z NavalDLC `FieldBattle` bywa na morzu - `IsNavalMapEvent => !Position.IsOnLand`); `RetreatingSide ==
BattleSideEnum.None`; strona pokonana nie poddala sie (`MapEventSide.IsSurrendered` to pole `internal` -
`AccessTools.FieldRefAccess<MapEventSide, bool>("IsSurrendered")`); pole prywatne `MapEvent._mapEventResultsApplied == false`
(`FieldRefAccess`, raz na bitwe); zadna partia obu stron nie jest Inna.
**Bitwa gracza** (`IsPlayerMapEvent`, takze symulacja) - po krytyce: nic nie zmieniamy, ale piszemy jedna linie
"Bitwa: H3 pominieta (gracz) ..." z liczbami gry i podzialem rozbitych z 3.2 (rozdz. 5) - zeby reczna proba (rozdz. 7) miala co czytac.

**Algorytm (dwie fazy: najpierw policz i sprawdz wszystko, potem zastosuj - po krytyce):**
1. Opis stron (szeregowi, bez bohaterow): ludzie = suma `HealthyManCountAtStart`; sklad typow = `MemberRoster` (z rannymi) +
   `DiedInBattle` + `RoutedInBattle`; udzial konnych, sredni tier. Teren: 9 punktow (2.2). -> C, T, O, Q, F, p (2.2), z zabezpieczeniami
   na zero (pusta strona, zero ludzi - skladnik 0, partia bez szeregowych pominieta).
2. `canCapture`: raz na bitwe `Campaign.Current.Models.BattleRewardModel.GetCaptureMemberChancesForWinnerParties(...)` -
   lista rannych niepusta; `slaveWinner` = kultura frakcji zwyciezcy na liscie (2.3); dla typu `CanTroopBeTakenPrisoner(troop)`.
3. **Plan** dla kazdej partii przegranej (`Party.MobileParty != null`, `IsActive`), dla kazdego typu szeregowego i:
   `d` = polegli, `r` = rozbici, `m` = w partii (`w` z tego rannych), `n = d + r + m` (gdy 0 - pomin);
   `k = RoundRandomized(p_partii x n)`; `s = n - k`; `j = (canCapture && CanTroopBeTakenPrisoner) ? min(s, RoundRandomized(s x c_i)) : 0`
   (`c_i` z 2.3; tabor wsi bez `slaveWinner` -> 0); `f = s - j`.
   **Sprawdzenie planu przed czymkolwiek:** zadna liczba ujemna, `k + j + f == n`, `j <= s`, `w <= m`. Blad w liczeniu albo
   w sprawdzeniu = **cala partia po staremu** (nic jeszcze nie zmieniono), licznik potkniec, pierwszy blad do logu.
3a. **Uwolnieni jency bez odbiorcy** (po krytyce, ta sama metoda gry, 1.3): dla kazdej partii przegranej i kazdego typu szeregowego
   w jej `PrisonRoster`: gdy `BattleRewardModel.GetLootPrisonerChances(zwyciezcy, element)` jest pusta, gra ich zdejmie i nikt ich nie
   wezmie - plan: zdjac ich z lochu sami i oddac wedlug pochodzenia (`SendHome` bez partii - zawod jednostki, 2.4 pkt 3; miejsce -
   region bitwy), licznik "uwolnieni jency". Gdy lista niepusta - nie ruszamy (gra wcieli ich do zwyciezcy jako ludzi).
4. **Zastosowanie** (tylko policzone liczby, same `AddToCounts`, **zmiany zerowe pomijane** - `AddToCounts` z 0 / 0 na brakujacym typie
   daje `FailedAssert`, `TroopRoster.cs:441`): `DiedInBattle += k - d`; `WoundedInBattle` ustawione na `j` (licznik i ranni);
   `RoutedInBattle += f - r`; `MemberRoster += (j - m)` z rannymi `+= (j - w)` - w partii zostaje dokladnie `j` rannych.
   Zostawiamy licznik `k + j + f - n` w linii dnia, ale to TYLKO kontrola kodu (z definicji 0); prawdziwe sprawdzenie robi krok 6.
5. Dla kazdej partii zwycieskiej (`IsActive`): `N = suma (m + d + r)`, limit `L = RoundRandomized(WinnerDeathCapPercent% x N)`;
   gdy polegli `D > L`: `x = D - L` rozlozone na typy proporcjonalnie do poleglych (losowo, reszta do najwiekszego):
   `DiedInBattle -= x_i`, `WoundedInBattle += x_i` (ranni), `MemberRoster += x_i` (ranni). Ta sama zasada dwoch faz.
6. **Niezalezne sprawdzenie (postfiks, po krytyce):** w prefiksie zapamietujemy liczbe szeregowych w `RosterToReceiveLootPrisoners`
   kazdego zwyciezcy (bez dubli - zaloga i milicja tej samej osady maja wspolny loch), w postfiksie odczyt po: **przyrost = suma j**
   (uwolnieni jency pokonanych ida do zwyciezcow jako LUDZIE, `RosterToReceiveLootMembers`, wiec nie mieszaja sie z jencami).
   Roznica = "jency zgubieni przez gre" (null w losowaniu, 1.3) - licznik dnia, oczekiwane ok. 0. **Po przegladzie kodu (rozdz. 15
   uwaga 2):** liczone tylko w bitwach, w ktorych plan objal kazda partie przegrana z szeregowymi w partii i zastosowanie przeszlo bez
   bledu (inaczej gra bierze jencow z partii spoza planu po staremu i roznica wychodzi ujemna, maskujac prawdziwe zgubienia); reszta -
   licznik "bez sprawdzenia". Dawne "w kazdej partii przegranej zero szeregowych po postfiksie" nie moglo nic wykryc (gra zeruje kazda
   partie przegrana, `MapEvent.cs:2034`) - zastapione **prawdziwym sprawdzeniem list**: na koncu zastosowania odczyt list gry typ po typie
   (zabici = k, ranni-jency = j, rozbici = f, w partii j, z tego j rannych) i w postfiksie jeszcze raz zabici i rozbici (gra ich w tej
   metodzie nie zmienia - zmiana to cudza latka); licznik "listy gry inne niz plan: typow (ludzi)".
7. Liczniki dnia (przed / po: zabici, ranni, rozbici, jency, ludzie; srednie C, T, O, Q, F; udzial bitew z T = 1; "prawdziwe bitwy")
   i jedna linia "Bitwa: H3 ..." (trafia do `bitwy.log` - `Log.cs:100` kieruje tam kazdy napis od "Bitwa:"), rozdz. 5.
- **try/catch na partie** w fazie liczenia (wyzej); faza zastosowania to same `AddToCounts` na sprawdzonych liczbach - wyjatek tam
  oznacza blad kodu (licznik "potkniecia w zastosowaniu", pierwszy blad do logu, test wymaga 0). Zadnego globalnego wylacznika po
  bledzie (CLAUDE.md 7).
- Bohaterow nie dotykamy (smierc, rany, niewola, zbiegostwo - gra; okupy W2 i pomiar 169 N8 bez zmian).
- Wodz i partia: przegrana partia dalej traci wszystkich szeregowych (jak dzis), wiec `HandleMapEventEnd` niszczy ja jak dzis -
  StrategicCampaignAI i BK widza te same zwyciestwa, porazki i zniszczone partie.

### 3.2 `OutlawLaw.OnMapEventEnded` (`OutlawLaw.cs:340-359`) - jeden podzial rozbitych
- Przy `LosersFleeEnabled`: dla kazdej partii obu stron, kazdy typ szeregowy z `RoutedInBattle` (n, liczby calkowite), wedlug tabeli 2.4:
  banda -> `RoundRandomized(n x OutlawBandRoutedShare)` do puli regionu bitwy, reszta `LosersFlee.SendHome` (wedlug zawodu);
  tabor wsi / rybacy -> 100% `SendHome` (hearth swojej wsi); karawana -> 100% "z szablonu"; zaloga / milicja / patrol ->
  `RoundRandomized(n x OutlawRoutedShare)` do puli, reszta "z szablonu"; partie rodow i inne -> `RoundRandomized(n x OutlawRoutedShare)`
  do puli, reszta `SendHome`; czego `SendHome` nie przyjal (tylko zolnierze do BK) -> do puli ("bez domu").
- **Inni (po przegladzie kodu, rozdz. 15 uwaga 6):** partia Innych (`Undead.Party`) i kazdy wight w innej partii (`Undead.Character`) -
  ani do puli, ani `SendHome`: z niczego, do niczego, licznik "Inni (wighty - do niczego)". Wight ma w ROT `occupation="Soldier"`
  (`ROT-Troops.xml`, 12 jednostek `Culture.whitewalker`), wiec bez filtra szedlby do ludnosci BK osady zywych.
- **Prawo wyrzutkow wylaczone (`!On`) przy wlaczonym H3** (po krytyce): nikt do puli - kazdy rozbity idzie `SendHome` wedlug pochodzenia,
  "bez domu" -> licznik "znikneli (prawo wyrzutkow wylaczone)". **Bandyta wtedy "z szablonu", nie do hearth** (po przegladzie kodu, uwaga 5):
  przy wylaczonym prawie bramki band przepuszczaja gre, bandy rodza sie z szablonu, nie z hearth - "bandyta do hearth jako prosty" jest
  prawdziwe tylko przy wlaczonym prawie wyrzutkow. I **vanilla dezerterzy zablokowani**:
  `OutlawLaw.SkipVanillaDeserters` zwraca `!On && !LosersFlee.On` (dzis `!On`) - inaczej gra sklada partie dezerterow z
  `RoutedInBattle + DiedInBattle` pokonanych partii rodow (`DesertersCampaignBehavior.cs:101-121`), czyli z tych samych ludzi,
  ktorych H3 odsyla do domu (podwojnie). Gdy oba wylaczone - gra jak dzis.
- Przy wylaczonym H3 - kod jak dzis co do grosza (polowa do puli, reszta znika).
- Dotyczy KAZDEJ bitwy z rozbitymi (takze misji gracza i rabunkow) - to jest domkniecie "w nicosc", nie zmiana walki.
- Potrzebne: `IsOutlawParty` (`:418-428`) z `private` na `internal` (uzywa go `LosersFlee` do rodzaju partii); `ReturnHome`
  (`:242-250`) z `private` na `internal` (hearth dla bandytow i prostych).

### 3.3 `OutlawLaw.Daily` - powrot z puli (`OutlawLaw.cs:507-521`)
Przy `LosersFleeEnabled` powrot rozdzielony **wedlug zawodu klucza** (`CharacterObject.Occupation`, po krytyce - w puli sa tez
bandyci, ktorzy pochodza z hearth, 1.5):
- klucz `"~"` (prosci) oraz typy `Bandit`, `Villager` i inne -> **hearth** jak dzis (`ReturnHome`, 0.5 na czlowieka - symetrycznie
  do `TakeCommoners`);
- typy `Soldier`, `Mercenary` (dezerterzy, rozbici, jency rozwiazanych band) -> **ludnosc BK** (`SendHome` bez partii: osada BK
  regionu, a gdy kultura sie nie zgadza - najblizsza osada kultury jednostki, 2.4 pkt 4); liczba calkowita `RoundRandomized`, z puli
  zdejmujemy dokladnie tyle, ile BK przyjal; reszta zostaje w puli do jutra;
- typy `CaravanGuard` -> "z szablonu" (licznik);
- typy Innych (`Undead.Character`, klucz `KeyUndead` - po przegladzie kodu) -> wygasaja w tym samym tempie do niczego (licznik "wighty do
  niczego"); nigdy do ludnosci BK. Stare klucze wightow sa w puli z czasow przed H3 (polowa rozbitych szla do puli bez filtra) - bandy
  moga je dalej werbowac przez `Draw` / `DrawOnly` (stara dziura, nie H3 - do STAN-PRAC przy wdrozeniu, rozdz. 15);
- **reszta klucza zolnierza ponizej 1 czlowieka** (po przegladzie kodu, uwaga 8; stare klucze z ulamkami - dawny podzial rozbitych x 0.5
  i dawny powrot d x rate): z szansa rate na dobe klucz sie zamyka - z szansa d jeden caly czlowiek do ludnosci BK, inaczej nikt
  (oczekiwanie = d, bez ludzi z niczego i w nicosc); dom nie przyjal - klucz czeka. Bez tego reszta < 1 zostawalaby w puli na zawsze.
Bez tego podzialu rozwiazana banda (prosci z hearth pod kluczem bandyty) wracalaby do BK (+1 czlowiek), a hearth by ich nie odzyskal;
powroty z puli to dzis 3-7 dziennie po 40 dobach, ale 55-63 dziennie po roku (log 18-38-27), a po H3 pula rosnie. Z drugiej strony
zolnierz do hearth to ok. 100 ludzi ksiegi (1.5). Zawod jest w danych jednostki - nic nowego w zapisie. Przy wylaczonym H3 - jak dzis.

### 3.4 `LosersFlee.SendHome(MobileParty partia /* moze byc null */, CharacterObject troop, int n, Vec2 miejsce, IFaction frakcja)` -> ilu przyjeto
Jedna funkcja pochodzenia dla bitwy (3.2), powrotu z puli (3.3) i uwolnionych jencow (3.1 krok 3a), wedlug 2.4:
1. `partia.IsVillager` -> `partia.HomeSettlement.Village.Hearth += 0.5 x n` (stala jak w grze; brak wsi -> `OutlawLaw.ReturnHome`
   regionu miejsca); przyjeto n, licznik "do wsi (tabory)".
2. `partia` to karawana, zaloga, milicja albo patrol, albo `troop.Occupation == CaravanGuard` -> nic; przyjeto n, licznik "z szablonu".
3. `troop.Occupation` to `Soldier` albo `Mercenary` -> **ludnosc BK** osady z 2.4 pkt 4 (najblizsza `miejsce`, frakcja `frakcja`,
   kultura jednostki; pamiec na bitwe). BK przez refleksje (wzor: `Levy.Resolve`, `Levy.cs:42-55`; `PopulationLaw.cs:178-185`):
   `BannerKingsConfig.Instance.PopulationManager` -> `GetPopData(Settlement)` -> `PopulationData.UpdatePopFromSoldiers(CharacterObject, int)`;
   `MethodInfo` raz na sesje. **Sprawdzenie kazdego wywolania (po krytyce):** `TotalPop` przed i po - przyrost musi wynosic n
   (inaczej licznik "BK przyjal inaczej" i roznica do linii dnia; test wymaga 0). Wyjatek w BK -> `PopulationData.UpdatePopType(PopType.Serfs, n)`
   (enum BK przez `Enum.Parse`; klasa bez manpower); gdy i to zawiedzie -> 0 (wolajacy oddaje do puli, "bez domu").
   Znane kaprysy BK (sprawdzone, `MilitaryData.cs:103-156`): `KeyNotFound`, gdy klasa jednostki nie ma klucza w `Manpowers`
   (np. `Slaves` po zmianie prawa, `None` dla `null`); **`Nobles` dla zwyklej jednostki**, gdy lista wag klas wojskowych jest pusta
   (militaryzm 0): `MBRandom.ChooseWeighted` na pustej liscie daje `default(PopType)` = `Nobles` (`MBRandom.cs:99-120`, enum BK
   `Nobles = 0`). Rzadkie; liczymy je odczytem `GetTypeCount(Nobles)` przed i po, gdy jednostka nie jest z drzewa elitarnego
   (`BannerKings.Utils.Helpers.IsRetinueTroop`) - licznik "jako szlachta (kaprys BK)", bez poprawiania.
4. Pozostale zawody (`Bandit`, `Villager`, ...) -> `OutlawLaw.ReturnHome(region miejsca, n)` (0.5 hearth na czlowieka); licznik "do wsi (prosci)".
Po przegladzie kodu (rozdz. 15) przed pkt 1 i miedzy 2 a 3: **0.** wight Innych (`Undead.Character`) -> nic, przyjeto n, licznik "Inni"
(zabezpieczenie dla uwolnionych jencow i powrotu z puli; rozbitych pomija juz 3.2); **2a.** `Bandit` przy wylaczonym prawie wyrzutkow ->
"z szablonu" (licznik "bandyci przy wylaczonym prawie"). Liczniki domow sa dodatkowo rozbite wedlug zrodla (rozbici z bitew / uwolnieni
jency / powrot z puli), bo powrot z puli biegnie w `OutlawLaw.Daily` PO linii "Przegrani (H3)" i trafia do linii nastepnej doby.
Liczniki dnia: do wsi (tabory), do wsi (prosci), do domu (ludnosc BK), zastepczo jako chlopi, jako szlachta, z szablonu, bez domu,
BK przyjal inaczej. **Plik `h3-domy.csv`** (`Log.Csv`, wiersz na region na dobe - region jak w `ludzie-regiony.csv`, `OutlawLaw.RegionFor`):
`dzien;region_id;region;do_domu_bk;do_wsi_ludzi;do_wsi_hearth;w_las;z_szablonu;uwolnieni` - porownanie z kolumna `zwerbowani_dzis`
tego samego regionu w `ludzie-regiony.csv` pokazuje, czy ludzie wracaja tam, skad ich zabrano (po krytyce; osobny plik, zeby nie
ruszac `PeopleLedger`, ktory zmieniaja rownolegle paczki).

### 3.5 `Settings.cs` + `McmSettings.cs` (generator `python tools/gen_mcm.py`; zwarty blok zaraz po `OutlawRoutedShare`, `:565`)
```
public bool LosersFleeEnabled = true;            // battles fought without you: the beaten side mostly flees instead of dying - the slain follow the situation (horse to pursue, river or marsh behind, odds, seasoned men against levies), 5-65%; a few are taken; the rest go home or to the woods. The winner loses at most a few slain - the rest of his fallen are wounded. Your own battles are not touched
public float WinnerDeathCapPercent = 5f;         // the winner of a battle fought without you loses at most this share of his men slain; the rest of his fallen live, wounded (history: 1-5%)
public float LoserCaptiveVeteranPercent = 30f;   // of the beaten men who live, this share of seasoned troops (tier 4 and up) is taken - they were worth a ransom
public float LoserCaptiveCommonPercent = 5f;     // of the beaten men who live, this share of the rest is taken
public float NonCombatantDeathPercent = 5f;      // villagers and fishermen beaten on the road: share slain; the rest run back to their own village (a few are taken only by peoples who keep slaves). Caravan guards fight and are judged like soldiers
public float OutlawBandRoutedShare = 0.5f;       // share of outlaws fleeing a lost fight who go back to the woods; the rest - mostly villagers driven out by want - go home to the villages
```
Wagi wzoru (0.15 / 0.35 / 0.15 / 0.15 / 0.20 / -0.10 x (1 - T), granice 5-65%), promien terenu i stala hearth 0.5 tabor - stale w kodzie
z komentarzem (HISTORIA 4.3, `VillagerCampaignBehavior.cs:179`), bez suwakow. Lista kultur z niewolnictwem - stala w kodzie (2.3).
Opis `OutlawRoutedShare` poprawic: "share of soldiers routed or fleeing a lost battle who take to the woods instead of going home
(outlaws follow their own share, villagers always go home)". 680 -> 686 ustawien.
`Armoury.json` Jeffa: zadnego z tych kluczy nie ma (sprawdzone 08.10) - dzialaja domyslne.
`OutlawBandRoutedShare` 0.5 (po krytyce): ta sama polowa co u zolnierzy - jedna regula "polowa zbrojnych uciekinierow w las, polowa do
domu"; bandyta to w wiekszosci prosty z hearth (1.5), wiec jego "dom" to wies. Bez tego (100% w las, jak w pierwszej wersji)
pokonana banda tracilaby tylko polegle i pojmanych (ok. 52%) i odradzala sie z puli (`OutlawDailyRecruit` 2 na bande dziennie,
`OutlawLaw.cs:551-563`), a suwak awaryjny `OutlawRoutedShare` bandy by nie dotyczyl.

### 3.6 `ArmouryBehavior.cs`
- konstruktor (`:389`): `LosersFlee.Reset();` (liczniki dnia, pamiec refleksji BK, pamiec osad domowych);
- doba: `try { LosersFlee.Daily(); } catch (...)` zaraz po `BattleChronicle.Daily()` (`:1229`), przed `OutlawLaw.Daily()` (`:1258`);
  `LosersFlee.Daily` liczy tez raz na dobe ludnosc BK swiata (suma `TotalPop` po `Settlement.All` z danymi BK, ta sama refleksja -
  do testu A/B, rozdz. 7; gdy 169 wprowadzi te sama miare, uzyc jej zamiast tej);
- sluchaczy nie dochodzi (`OutlawLaw.OnMapEventEnded` jest juz wpiety, `:531`).

### 3.7 Czego NIE ruszamy
Walki i modeli (`SimulateHit`, `GetSurvivalChance`, `CalculateWinner`, odwrot, morale), bitew gracza (poza 3.2), bohaterow,
DTE (zbrojownia pokonanych do zwyciezcow jak dzis), lupu z cial (`LootDefeatedPartyCasualties` - sam zobaczy mniej cial, rozdz. 8),
`BattleChronicle` i `PeopleLedger` (czytaja te same listy - po H3 "ranni" przegranego w bitwie H3 = jency, jak dzis w praktyce;
H3 pisze wlasny plik `h3-domy.csv` zamiast dokladac kolumn do `ludzie-regiony.csv`), `MenPurse` (decyzja w rozdz. 8).

## 4. Bitwy z graczem
- **Pole bitwy (misja):** wynik walki bez zmian - kto padl, ten padl; jency wedlug gry. Faza 1 zmienia tylko 3.2: rozbici z pola
  (Twoi, ktorzy uciekli, i wroga) ida pol w las, pol do domu zamiast w polowie znikac. Zostaje stara dziura: zdrowi przegrani,
  ktorzy nie weszli do walki (fale posilkow), w 75% znikaja w `CaptureDefeatedPartyMembers`. Propozycja na faze 2 (bez zmiany
  walki): w prefiksie dla bitwy gracza, gdy gracz wygral, z tych zdrowych `RoundRandomized(0.25 x h)` zostaje jako ranni (gra ich
  wezmie - tyle jencow co dzis, tylko oznaczeni jako ranni), reszta do `RoutedInBattle`. Widac to w ekranie jencow (ranni) - na slowo Jeffa.
- **Symulacja z graczem** (autobitwa, "wyslij wojsko"; `IsPlayerMapEvent` i `IsPlayerSimulation`): wynik stosuje
  `PlayerEncounter.DoApplyMapEventResults` przez te sama metode, wiec latka moze objac ja jednym warunkiem. Ale tablica wyniku
  symulacji (`BattleSimulation`) pokazuje zabitych i rannych na zywo, zanim latka zadziala - gracz zobaczylby "zabici 120",
  a na ekranie jencow mniej niz rannych; trzeba by dopisac komunikat ("X of the enemy's fallen fled the field"). Gdy gracz
  przegrywa symulacje, jego ludzie uciekliby (dom / las) zamiast trafic do niewoli. Faza 1: NIE (jak dzis). Pytanie 10.1.
- Partie Twojego rodu prowadzone przez towarzyszy, w bitwie bez Ciebie - H3 TAK (jedna regula swiata): ich zwyciestwa daja mniej
  jencow, ich porazki - wiecej ludzi wraca do domow zamiast do wrogich lochow.
- **Sprawdzenie (po krytyce):** autotest nie ma bitew gracza w polu (licznik "z graczem" wyniesie 0), wiec ani pominiecie bitew
  gracza, ani podzial rozbitych z 3.2 w misji nie zostana w nim sprawdzone. Dlatego: (1) kazda bitwa gracza pisze linie
  "Bitwa: H3 pominieta (gracz)" (rozdz. 5); (2) w CHANGELOG reczna proba Jeffa - jedna bitwa w polu i jedna autobitwa: w linii
  "pominieta (gracz)" liczby gry bez zmian, rozbici rozpisani na las / dom, a sklad partii gracza po bitwie taki sam jak bez H3
  (ci sami zabici, ranni, jency na ekranie).

## 5. Logi
**Raz na bitwe H3** (do `bitwy.log`, przed linia "Bitwa: dzien ..." tej samej bitwy; bitwy ponizej `BattleChronicleMinMen` tylko liczone):
```
Bitwa: H3 dzien 108860 - pod X: przegrany <wodz (frakcja)> 412 ludzi (konni 12%, tier 2.1) vs zwyciezca <wodz> 655 ludzi (konni 34%, tier 2.6) |
  p = 0.15 + 0.35xC 0.22 + 0.15xT 0 (teren: srodek Plain, woda 0/8) + 0.15xO 0.06 + 0.20xQ 0.17 - 0.10xF 0 = 27% |
  gra: zabici 168 (41%), ranni 221, rozbici 0, stali 23 | H3: zabici 111 (27%), jency 21 (5%), rozbici 280 (68%) |
  zwyciezca: zabici gry 71 (10.8%) -> 33 (5.0%), +38 rannych | jency wzieci 21 (zgubieni przez gre 0), uwolnieni bez odbiorcy 0.
```
**Bitwa gracza** (po krytyce; raz na bitwe, takze ponizej `BattleChronicleMinMen`):
```
Bitwa: H3 pominieta (gracz) dzien D - pod X: <pole / symulacja> | gra: atakujacy zabici a, ranni b, rozbici c; obronca ... |
  rozbici: do puli P, do domu H (BK h1, wies h2), z szablonu S, bez domu B.
```
**Raz na dobe** w glownym logu, zaraz po "Bitwy:":
```
Przegrani (H3): dzien D - bitew objetych N (pominiete: z graczem a, nie w polu b [oblezenie, rabunek, wypad, morze], odwrot c,
  poddanie d, z Innymi e) | przegrani L ludzi: zabici K (k%; gra dalaby K0, k0%), jency J (j%; gra J0), rozbici F (f%) -
  wojsko ..., bandy ..., tabory wsi ..., karawany ... | zwyciezcy W ludzi: zabici (w%; gra w0%), z poleglych ranni +R |
  prawdziwe bitwy (obie >= 50, do 1:4; srednia na bitwe jak w "Bitwy:"): przegrani zabici X% (gra Y%), zwyciezcy Z% (gra V%) -
  historia 15-40% / 1-5% | srednie p 0.xx: C .., T .., O .., Q .., F .. ; bitew z T = 1: t% |
  sprawdzenia: jency wzieci - j = 0 (zgubieni przez gre G), ludnosc BK przyjeta - przyrost TotalPop = 0 (roznica R),
  w partiach przegranych po bitwie 0 szeregowych; kontrola kodu k + j + f - n = 0 |
  uwolnieni jency bez odbiorcy U (w bitwach poza H3 tylko liczeni: U2) | domy: do domu BK h1, jako chlopi h1s, jako szlachta h1n,
  do wsi (tabory) h2 ludzi = h2h hearth, do wsi (prosci) h3, z szablonu z, bez domu b | ludnosc BK swiata P (zmiana dP) |
  wcieleni jency AI (partie rodow) wj | potkniecia: liczenie S1, zastosowanie S2 | czas T ms.
```
**Po przegladzie kodu (rozdz. 15) linia dnia w kodzie wyglada tak** (fragmenty zmienione; reszta jak wyzej):
```
... | sprawdzenia: jency wzieci T - j J = T-J (zgubieni przez gre G, bitew sprawdzonych C, bez sprawdzenia - partia przegrana poza
  planem U), listy gry po zmianie inne niz plan: a typow (b ludzi), po wyniku gry c typow (d ludzi), BK przyjal inaczej niz przyrost
  TotalPop: e wywolan (roznica f); kontrola kodu k + j + f - n = 0 | uwolnieni ... | domy: do domu BK h1 (...), ..., z szablonu z
  (garnizony zg, bandyci przy wylaczonym prawie wyrzutkow zb, z puli straz karawan zs), bez domu b (do puli) |
  domy wedlug zrodla: rozbici z bitew - BK r1, wies r2, z szablonu r3; uwolnieni jency - BK u1, wies u2, z szablonu u3;
  z puli (linia "Wyrzutki:" doby D-1) - BK p1 | Inni (wighty - z niczego, do niczego, nigdy do BK ani wsi): rozbici w1, inna droga w2 |
  dziura werbunku (partie lordow AI, ludzie bez werbunku u notabli): nowe partie z szablonu klanu s1 ludzi (s2 partii), z zalog do lordow
  g1, od lordow do zalog g2 (netto z zalog g1-g2) | ludnosc BK swiata ... | wcieleni jency AI ... | potkniecia ... |
  czas T ms (wynik bitwy i doba t1, rozbici przy koncu bitwy t2, powrot z puli - doba wczesniej t3).
```
"rozbici z bitew" (r1, r2, r3) tej doby = "ludnosc BK", "do wsi", "z szablonu" z dopisku H3 linii "Wyrzutki:" TEJ SAMEJ doby; "z puli" (p1)
= "do ludnosci BK" z linii "Wyrzutki:" doby D-1 (powrot z puli biegnie w `OutlawLaw.Daily` po linii "Przegrani (H3)"). Przy wylaczonym H3
linia "WYLACZONE" ma tez "dziura werbunku" i pelny "czas". W linii "Bitwa: H3" czlon F jest wypisany jako wartosc czynna `F x (1 - T)`,
a przy T = 1 i F > 0 z dopiskiem "(F x nie liczy sie przy T = 1)"; "jency wzieci" bitwy spoza pelnego planu - "(bez sprawdzenia - partia
przegrana poza planem)".
Linie "gra dalaby" licza sie z list PRZED zmiana w tej samej bitwie - porownanie z dzisiejszymi 46% / 14% w jednym przebiegu.
Linia bitwy zaczyna sie od "Bitwa: H3" (a nie "Bitwa: dzien"), wiec parsery liczace bitwy po "Bitwa: dzien" (`sprawdz_logi.py`,
skrypty audytu) nie licza jej drugi raz - przy wdrozeniu sprawdzic, czy zaden parser nie liczy po samym "Bitwa:".
**"Wyrzutki:"** (`OutlawLaw.cs:611-618`) dostaje: sklad puli wedlug zawodu (po krytyce: "pula N (zolnierzy Soldier/Mercenary Z,
bandytow B, prostych P)" zamiast dzisiejszego "zolnierzy" = kazdy klucz inny niz `"~"`); "naplyw: ... rozbitkowie N (wojsko F1,
bandy F2, zalogi F3) ... | rozbici do domu: ludnosc BK H, do wsi W, z szablonu S, bez domu (do puli) B | powrot z puli: do hearth X
(prosci X1, bandyci X2), do ludnosci BK Y, z szablonu Y2" i sprawdzenie "rozbici z bitew dzis R = do puli P + do domu (H + W + S) + bez domu B".
Po przegladzie kodu: w skladzie puli "Inni (wighty, stare)", w sumie rozbitych "+ Inni (wighty - do niczego) U", a "roznica" nazywa sie
"kontrola kodu - roznica" (liczy sie z tych samych licznikow, ktore dzieli - wychwyci tylko wyjatek w srodku elementu, to NIE jest
niezalezne sprawdzenie); w powrocie z puli "(w tym z reszt ponizej 1 czlowieka s: zamkniete klucze k, ulamki razem u)" i "wighty do niczego".

## 6. Zapis, koszt
- **Zapis: nic nowego.** H3 nie ma stanu miedzy dobami (liczniki dnia jak `BattleChronicle`); pula wyrzutkow zapisuje sie juz
  (`arm_outlaws`, `SaveText.Sync`, `ArmouryBehavior.cs:432`), ludnosc BK zapisuje BK. Wylaczenie H3 w MCM = gra jak dzis od nastepnej bitwy.
- **Koszt:** raz na bitwe z wynikiem (ok. 15-20 bitew AI w polu dziennie): kilka partii x kilkadziesiat typow, same liczby
  calkowite i `AddToCounts`; teren 9 odczytow; osada domowa - jedno przejscie po `Settlement.All` na (bitwa, frakcja, kultura),
  z pamiecia na bitwe; refleksja BK raz na (partia, typ) rozbitych do domu + odczyt `TotalPop` przed i po. Postfiks: odczyt lochow
  zwyciezcow. Raz na dobe: suma `TotalPop` po osadach z danymi BK (ok. tysiac odczytow). Szacunek < 1 ms na bitwe, < 30 ms na dobe -
  mierzone `Stopwatch`, wypisywane w linii dnia. Powrot z puli: ta sama petla co dzis + odczyt zawodu klucza (pamiec typow na sesje).
- **Po przegladzie kodu (uwaga 7):** "czas" w linii dnia liczy teraz WSZYSTKIE czesci H3 - takze rozbitych przy `MapEventEnded`
  (`RoutedH3`: `SendHome`, refleksja BK, `FindHome` po ok. 800 osadach z danymi BK na kazda pare frakcja / kultura, pamiec na bitwe)
  i galaz H3 powrotu z puli (doba wczesniej). Szacunek przegladu ok. 0.1-0.3 ms na bitwe z rozbitymi zolnierzami. Pamieci `FindHome`
  na dobe (klucz: region bitwy + frakcja + kultura) NIE dodaje - decyzja Jeffa 08.10 "optymalizacja na koniec"; gdy pomiar pokaze
  ponad 50 ms na dobe, to pierwszy kandydat.

## 7. Test - A/B na tym samym DLL i tym samym zapisie (40 dob), potem rok
**Uklad (po krytyce, obowiazkowy):** jeden DLL (z H3), jeden zapis startowy (ten sam, z ktorego szly przebiegi 08.10, doba ok. 108 836),
dwa przebiegi po 40 dob: **A** `LosersFleeEnabled = false`, **B** `true`. Rownolegle paczki (169-171, T1-T8) siedza w obu przebiegach
tak samo, wiec roznica B - A to H3. Trzy przebiegi 08.10 (17:44 / 17:59 / 18:13, inny DLL) - tylko orientacyjnie (kolumna "Dzis").
Potem **B na rok** (pula i bandy ustalaja sie powoli: powrot z puli 0.5% dziennie w wojnie, `Settings.cs:563-564`, czyli stala czasu
rzedu 200 dob) i porownanie z rokiem bez H3 (A na rok albo log 18-38-27: zapis po roku, pula 10.5-12 tys., bandy 1 123-1 128, ludzi
22.6-22.8 tys.) - zgodnie z regula Jeffa "40 dob na test zmiany, rok na skutki dlugie".

Projekcja [S] z `bitwy.log` 08.10 (skrypt scratchpad `h3scan/proj2.py`; tylko AI w polu; zalozone p: prawdziwe 20%, nierowne 40%,
bandy 50%, karawany 35%, tabory 5%; jency: tabory 1%, karawany 8%): przegranych 552 dziennie - zabici 278 -> ok. 138, jency ok. 253 -> ok. 30,
rozbici ok. 20 -> ok. 384: do puli ok. 131 (wojsko 118, bandy 14), do ludnosci BK ok. 118, do wsi ok. 103 ludzi (tabory 89, bandyci 14 -
ok. 52 hearth dziennie), "z szablonu" (karawany) ok. 32; zwyciezcy zabici 97 -> ok. 47 dziennie.

| Co (log) | Dzis (A) | Musi byc w B |
|---|---|---|
| "Przegrani (H3)": prawdziwe bitwy, przegrani zabici (srednia na bitwe) | gra 42-46% | **12-32%** |
| j.w., zwyciezcy zabici | 11-14% | **<= 5.0%** (zadna partia >= 20 ludzi ponad 6%) |
| przegrani razem (wagi ludzie): zabici / jency | 49% / ok. 46% | **15-35% / 3-15%** |
| tabory wsi: zabici / jency | 85% / 15% | **<= 7% / <= 1%** (jency tylko od kultur z niewolnictwem) |
| karawany: zabici | 54% | **20-50%** (wzor jak wojsko) |
| **sprawdzenia niezalezne** (po krytyce; po przegladzie kodu rozdz. 15 uwaga 2): jency wzieci - j (tylko bitwy z pelnym planem); przyrost `TotalPop` - przyjeci; listy gry po zmianie i po wyniku gry wobec planu (typow / ludzi) | - | **0 / 0 / 0** kazdej doby; "zgubieni przez gre" w przedziale **0 .. 0.1% j** (ujemne = blad pomiaru); "bez sprawdzenia" <= 1% bitew objetych; potkniecia liczenia i zastosowania 0 |
| kontrola kodu (NIE niezalezna): k + j + f - n; "kontrola kodu - roznica" w dopisku H3 linii "Wyrzutki:" | - | 0 (wychwyci tylko wyjatek w srodku elementu) |
| "Bitwy:" zabitych dziennie (wszystkie starcia, srednia 40 dob) | 457-556 | B wobec A: spadek **30-50%** |
| "Ludzie:" zabici w partiach rodow dziennie (srednia 40 dob) | 216-298 | B wobec A: spadek **>= 25%** |
| jency w partiach (doba 40) | 3 129-4 528 | **<= 50%** A |
| "Wyrzutki:" rozbitkowie dziennie | ok. 0-20 | **60-250**; "bez domu" <= 2% zolnierzy do domu |
| rozbici z bitew = do puli + do domu (BK + wies + z szablonu) + bez domu + Inni (kontrola kodu) | - | **roznica 0** |
| "domy wedlug zrodla" (linia "Przegrani (H3)") wobec dopisku H3 "Wyrzutki:" | - | rozbici z bitew BK / wies / z szablonu doby D = "ludnosc BK" / "do wsi" / "z szablonu" w "Wyrzutki:" doby D; "z puli - BK" doby D = "do ludnosci BK" w "Wyrzutki:" doby D-1 (kazdej doby, roznica 0) |
| Inni (wighty) do ludnosci BK albo wsi (po przegladzie kodu, uwaga 6) | dzis 50-500 w 40 dob (polowa rozbitych Innych przez `SendHome`) | **0**: "Inni ... rozbici" > 0 w dobach bitew z Innymi (np. wypady pod Hornfoot), a w `h3-domy.csv` i "do domu BK" ich nie ma; w skladzie puli "Inni (wighty, stare)" tylko maleje |
| pula wyrzutkow | doba 40: 553-1 343 | doba 40 **<= 8 000** i nachylenie dob 30-40 **<= +100 dziennie**; po roku **<= 2 x A** |
| bandy - ludzi | doba 40: 6 114-6 546 | doba 40 **<= 1.3 x A**; po roku **<= 1.25 x A** (A po roku ok. 22.7 tys.) |
| hearth swiata (CSV, doba 1 -> 40) | +9.6% | **<= A + 1.5 pkt** (tabory wracaja do swoich wsi: ok. +52 hearth dziennie = ok. +0.9 pkt w 40 dobach); roznica B - A = suma "do wsi (hearth)" z linii dnia +-20% |
| ludnosc BK swiata (`LosersFlee.Daily`, doba 1 -> 40) | - | **B - A <= suma "do domu BK"**; B - A >= ta suma minus dodatkowy werbunek u notabli B - A. To NIE dowodzi "nikt z niczego" (po przegladzie kodu, uwaga 1): ludzie, ktorzy weszli do partii lordow bez werbunku BK (szablon nowej partii, zalogi), siedza wewnatrz tej sumy |
| **dziura werbunku** (NOWE, po przegladzie kodu, uwaga 1): "nowe partie z szablonu klanu" + "netto z zalog" (linia "Przegrani (H3)", suma 40 dob) | mierzy dopiero ten DLL (tez w A) | jawna gorna granica: **"do domu BK" <= werbunek BK u notabli ("Ludzie:" od notabli do partii rodow) + szablon + netto z zalog**. Gdy szablon + netto z zalog > **20%** "do domu BK" - nastepny krok (osobna decyzja, nie ta paczka): zolnierzy partii, ktore nie werbowaly u notabli (klany bez osad, najemnicy), traktowac w `SendHome` jak "z szablonu" |
| osady domowe (`h3-domy.csv` obok `zwerbowani_dzis` z `ludzie-regiony.csv`, 40 dob) | - | **>= 70%** "do domu BK" w regionach, w ktorych w tych 40 dobach werbowano; zaden region **> 5%** calosci |
| partie rodow - ludzi (doba 40) | 102-105 tys. | **>= A** (zwyciezcy maja wiecej rannych, ktorzy wracaja) |
| "Ludzie:" od notabli do partii rodow (srednia 40 dob) | 1 620-1 630/d | B wobec A **-10% .. +20%** (rozdz. 8: oczekiwane -3..+11%) |
| wcieleni jency AI, partie rodow (NOWY licznik, postfiks `RecruitPrisonersAi`) | - (mierzy dopiero ten DLL) | B **<= 0.5 x A** (jencow ok. 88% mniej) |
| kategoria "bez osady" w "Ludzie:" | 1 836-1 847/d | **nie uzywac** - to w wiekszosci echo ROT (rozdz. 8) |
| udzial bitew H3 z T = 1 | - | **10-30%**; ponizej 5% albo ponad 50% - promien terenu do poprawy (jedna stala) |
| linia startowa: wlasciciele latek 4 metod | - | tylko oczekiwani (Armoury, ROT `LootCollectorPatch`, RBM); inny - sprawdzic przed dalszym testem |
| czas H3 (linia dnia; po przegladzie kodu WSZYSTKIE czesci: wynik bitwy i doba + rozbici przy koncu bitwy + powrot z puli) | - | **<= 50 ms** na dobe razem; doba gry nie wolniejsza o wiecej niz 2% |
| reszty kluczy zolnierzy ponizej 1 czlowieka (dopisek H3 "Wyrzutki:", po przegladzie kodu, uwaga 8) | - | "zamkniete klucze" > 0 w pierwszych dobach na starym zapisie i maleje; suma 40 dob "z reszt ... do ludnosci BK" = suma "ulamki razem" +- 30% (gdy zamknietych kluczy razem >= 50; losowanie - przy malej liczbie kluczy rozrzut wiekszy) |
| bledy (CrashScribe, "Armoury ERROR") | 0 | **0** nowych |
| reczna proba gracza (CHANGELOG): bitwa w polu + autobitwa | - | linia "H3 pominieta (gracz)" przy kazdej; sklad partii gracza jak bez H3 |

**Wyjscia awaryjne (jedno naraz, kolejny test po kazdym):** bandy ponad prog -> `OutlawBandRoutedShare` 0.5 -> **0.25**; pula ponad prog
(bandy w normie) -> `OutlawRoutedShare` 0.5 -> **0.25**; oba - najpierw bandy. Kazda zmiana domyslnej: wpis w CHANGELOG + sprawdzic
`Armoury.json`. Gdy prawdziwe bitwy ponad 32%: sprawdzic w liniach "Bitwa: H3", ktory skladnik (zwykle O albo Q przy liczeniu
z rannymi) - nie stroic wag bez dowodu.

## 8. Ryzyka i co sprawdzic (zasada 0)
- **Wiecej ludzi zyje -> wyrzutki i bandy:** naplyw rozbitych do puli z ok. 0-20 do ok. 130 dziennie (wojsko 118, bandy 14). Bandy
  werbuja z puli do 2 dziennie na bande (`OutlawDailyRecruit`; dzis bierze sie 12-24 dziennie, bo pula jest mala i rozproszona),
  nowe bandy rodza sie tylko tam, gdzie sa ludzie (bramki OutlawLaw) - wiekszej puli bandy wezma wiecej, mozliwy wzrost band.
  Stan ustalony puli przy samym naplywie H3 i powrocie 0.5-2.5% dziennie to ok. 5-26 tys. [S], ustala sie przez setki dob - stad test
  roczny. Bez H3 po roku jest juz pula 10.5-12 tys. i bandy 22.7 tys. ludzi (log 18-38-27). Progi i dwa wyjscia awaryjne w rozdz. 7.
  W historii stopy "rozbici -> bandy" nie ma; w lore broken men sa.
- **Werbunek i budzet (grupa C, E10) - poprawione po krytyce:** audyt zakladal "werbunek -30-40%" - **to sie NIE sprawdzi przy tym
  projekcie** (rozdz. 9): pokonana partia dalej traci wszystkich ludzi z partii (uchodza do domu, nie do pana). Zwyciezca dostaje
  ok. 220 jencow dziennie mniej (253 -> ok. 30) i traci ok. 50 poleglych dziennie mniej. Przedzial z tych liczb: **dolna granica**
  (wcielenie jencow nic nie znaczy, mniej poleglych = mniej uzupelnien) **-50 dziennie**; **gorna** (kazdy niewziety jeniec bylby
  wcielony, a lord uzupelnia go u notabli) **+170 dziennie**. Wobec sredniej 40 dob werbunku od notabli do partii rodow
  (1 620-1 630 dziennie) to **-3% .. +11%**; wobec stanu z doby 40 (942-997 dziennie) -5% .. +18%. **Oczekiwane blizej dolnej
  granicy** [K][P]: kategoria "bez osady" (1 836-1 847 dziennie srednio), w ktorej `PeopleLedger` liczy wcielenia jencow
  (`RecruitPrisonersCampaignBehavior.cs:117` wola `OnTroopRecruited` z osada `null`), to w wiekszosci echo ROT: `ROTTroopRecruiter`
  po kazdym werbunku lorda AI wymienia rekruta i wola zdarzenie drugi raz z osada `null` (`ROTTroopRecruiter.cs:105-122, 216`,
  dzialaja dla kazdego wodza AI spoza rodu gracza). "Bez osady" jest nawet o 70-86 dziennie MNIEJSZE niz notable + karczma
  (1 620-1 630 + 288-297), wiec wcielen jencow jest malo - prawdziwa liczbe da nowy licznik (postfiks `RecruitPrisonersAi`, 3.1).
  Budzet rodow (166): koszt werbunku moze wzrosnac najwyzej o ok. 11% (srednia), oczekiwanie kilka procent - grupa C liczy z pomiaru A/B.
  Zysk H3 to ludzie swiata (mniej trupow, mniej jencow), nie zloto lordow.
- **Ludnosc BK (poprawione po krytyce):** do ludnosci BK wraca tylko wojsko (`Soldier` / `Mercenary`): ok. 118 dziennie z bitew + czesc
  powrotow z puli (zolnierze, 0.5% puli dziennie w wojnie). Chlopi i bandyci wracaja do hearth (ok. 103 ludzi = ok. 52 hearth dziennie),
  zalogi karawan nigdzie. Werbunek od notabli zdejmuje ok. 1 600 dziennie, wiec bilans BK z wojny dalej mocno ujemny. Ci, co weszli
  do partii rodu "z niczego" (nowa partia z szablonu, wcielony jeniec z zalogi karawany), przy powrocie dopisuja sie do BK - to dziura
  po stronie werbunku (zamyka 108 / E7), nie H3; H3 nie dopisuje tylko tych rodzajow partii, ktore sa W CALOSCI z szablonu (2.4 pkt 2).
  **Po przegladzie kodu (uwaga 1) - sprostowanie:** przed H3 tacy ludzie gineli albo znikali, wiec ludnosc BK sie nie zmieniala; po H3
  dopisuja sie do BK - czyli H3 otwiera NOWY strumien ludzi z niczego do BK, a test "B - A <= do domu BK" go nie widzi (ci ludzie sa
  wewnatrz sumy). Skala jest teraz mierzona (pomiar, bez zmiany zachowania): postfiks na
  `LordPartyComponent.InitializationArgs.InitializeLordPartyProperties` (nowa partia lorda AI spoza rodu gracza: szeregowi z szablonu
  klanu - `LordPartyComponent.cs:38-39`; szablony lordow ROT maja 53-56 ludzi; `Priority.Last`, wiec po zdjeciu szablonu klanu Spoils
  przez `SpoilsCompany`) i na `GarrisonTroopsCampaignBehavior.TakeTroopsFromGarrison` / `LeaveTroopsToGarrison` (`:542-610`; przekazania
  z zalog, ktore rosna z niczego, +32.8 tys. na rok). Linia dnia "dziura werbunku" i prog w rozdz. 7 (20% "do domu BK" - wtedy osobna
  decyzja: zolnierze partii, ktore nie werbowaly u notabli, "z szablonu"). Inne znane drogi z niczego do partii lordow (nie mierzone tu):
  "ochotnicy z mapy" mniejszych frakcji (`RecruitmentCampaignBehavior.HourlyTickParty`, liczy `SpoilsCompany` przy Spoils) i wcieleni
  jency z zalog karawan.
  Osada domowa = najblizsza bitwie osada frakcji i kultury jednostki (2.4 pkt 4), a nie siedziba wodza - ludzie nie splywaja do kilkudziesieciu stolic.
- **Sakiewka ludzi rozbitej partii (decyzja po krytyce):** `MenPurse.OnPartyDestroyed` (`MenPurse.cs:63-79`) oddaje cala sakiewke ludzi
  pokonanej partii zwyciezcy (trzecia wodzowi, reszta jego ludziom), chociaz po H3 ok. 70% ludzi przezywa i ucieka. **Zostaje tak,
  swiadomie:** sakiewka ludzi to oboz i tabor partii (lup sprzedany w miescie, zold) - po rozbiciu oboz przepadal na rzecz zwyciezcy [H]
  (lup obozu dzielony "trzeciami", Hay 1954; po Poitiers i Azincourt zdobywano tabory), a uciekinier niosl przy sobie tyle, co na sobie.
  Pieniadz nie powstaje ani nie znika. Sprzet uciekajacego zostaje z nim dopiero z W1 / 171 (DTE) - tam tez wrocic do sakiewki.
- **Mniej jencow (-88%):** mniej sprzedazy jencow (zloto z niczego L5 / L13 - dobrze dla Z1, mniejszy dochod lordow), mniej
  okupow szeregowych, mniej pracy jencow i wcielen (RealisticCaptivity, Prison Is Not Fun - dotyczy szeregowych), bandy mniej
  werbuja jencow (`OutlawPrisonerJoinPercent`). Bohaterowie bez zmian.
- **Mniej lupu z cial dla AI:** `LootDefeatedPartyCasualties` liczy `DiedInBattle` + `WoundedInBattle`; po H3 tylko zabici + jency
  (ok. -70% cial) - zloto z niczego dla wodzow AI z ok. 10-25 do ok. 3-7 tys. dziennie [S] (PROJEKT wiersz 14 / 164a; kierunek zgodny).
- **DTE i sprzet uchodzacych:** DTE oddaje zwyciezcy cala zbrojownie (zapas) pokonanej partii jak dzis; uchodzacy "zabieraja swoje"
  tylko w tym sensie, ze w puli zostaja w swoim typie (wzorcu sprzetu). Pelne "sprzet uchodzacego zostaje z nim" (W1) - razem
  z 171 / 164, nie tutaj (zmiana w DTE).
- **StrategicCampaignAI, Diplomacy:** kto wygrywa i czyja partia ginie - bez zmian (walka nietknieta). Zwyciezcy maja po bitwie
  wiecej rannych (szybciej odzyskuja sile, SlowHealing AI 100%) - mozliwe nieco wiecej armii (SCAI: sila >= 260). Zmeczenie wojna
  Diplomacy liczy trafienia w walce (`TroopCasualties`), nie zabitych - bez zmian, czyli H3 nie wydluza ani nie skraca wojen tym kanalem.
- **Zgodnosc list:** `WoundedInBattle` przegranego = jency (dzis ranni przegranego tez wszyscy szli do niewoli - znaczenie to samo).
  `BattleChronicle` "Bitwa:" pokaze dla bitwy H3: zabici K, ranni J (= jency), rozbici F. `PeopleLedger` "bilans partii rodow":
  uchodzacy z pokonanych partii sa w "reszcie" (jak dzis jency).
- **Kolizje rownoleglych paczek:** 169 (pomiar pojman bohaterow, MIARA SWIATA z ludnoscia BK) - bohaterowie nietknieci; gdy 169 da
  sume ludnosci BK, H3 bierze ja zamiast swojej (3.6); 170 (BEE) - BEE czyta te same klasy BK (`BetterEconomyBridge.UpdateClassCount`),
  wiec 118 zolnierzy dziennie do BK to tez +118 w klasach BEE (zamierzone - to ci sami ludzie, ktorych BK zdjal przy werbunku);
  171 (zbrojenie zalog) - DTE bez zmian; T4 (dezerterzy z niezaplaconych armii do puli) - ich powrot z puli wedlug zawodu (3.3: zolnierz
  do ludnosci BK) - spojne ("zolnierz wraca tam, skad go wzieto"); 113 / 108 / 109 (ksiega) - po 108 `SendHome` i 3.3 przechodza na
  ksiege w jednym miejscu; T2 / R2 / R4 (Inni) - bitwy z Innymi poza H3; rabunek osada po osadzie - ta sama lista kultur
  z niewolnictwem (2.3). Wszystkie zmiany w `OutlawLaw` sa za wylacznikiem H3. H3 nie dotyka `PeopleLedger` (wlasny plik CSV).
- **Brzegi:** armia (kazda partia osobno - limit 5% na partie); zwyciezca bez partii biorace jencow (tabor, karawana, patrol) -> jency 0,
  wszyscy uchodza; partia przegrana nieaktywna - pomijamy; strona bez szeregowych - O, Q, C, F = 0, bez dzielenia przez zero; bitwa
  raz (`_mapEventResultsApplied`); plan sprawdzony przed zastosowaniem, zmiany zerowe pomijane (3.1); RBM zgubiony "cios obuchem
  nie zabija" dziala dalej w misji gracza i u zwyciezcy do limitu (poza zakresem).

## 9. Sprostowania do audytu (wpisac do 00-AUDYT przy nastepnym przegladzie)
1. **E10 / W1 "werbunek -30-40%"** - nieprawda dla regul "do domu / w las" (rozdz. 8): werbunek u notabli zmienia sie od -3% do +11%
   (srednia 40 dob; po krytyce - liczone z ubytku jencow i poleglych), oczekiwanie blizej dolnej granicy; zysk H3 to ludzie (zabici
   ok. -50% w bitwach AI w polu, -30..-50% we wszystkich starciach; jency ok. -88%). Prawdziwe "-30%" daloby tylko "uciekinierzy wracaja
   do pana" (nizej, na pozniej).
2. **W1 "nigdy do puli wyrzutkow"** zastapione przez S16 (0.5, jeden podzial) - zostaje S16; dla band osobny suwak 0.5 (2.4, 3.5);
   progi i wyjscia 0.25 w rozdz. 7.
3. **Powrot z puli do hearth** (`OutlawHearthPerMan` 0.5) to ok. 100 ludzi ksiegi na czlowieka - dopisac do 2.11 (dziury ludzi);
   H3 zamyka to dla zolnierzy (3.3, wedlug zawodu); prosci i bandyci zostaja przy hearth (stamtad ich wzieto), do 108.
4. **Wzor:** zakres 5-65% (z F), nie 15-65%; F tylko przy T = 0; T binarne, z 9 punktow (Visby 35%, nie 42%); "bez pardonu" w grze tylko Inni.
5. **Dom uciekiniera przed 108 / 167 (decyzja projektu po krytyce; dopisac w 00-AUDYT E10 i w STAN-PRAC przy H3 przy wdrozeniu -
   ta paczka zmienia tylko swoj dokument):** E10 i raport 04 W1 wskazuja "108 (albo tymczasowo karczma 167)". Obu nie ma w grze, a H3 ma
   isc przed grupa C. Dlatego H3 idzie przed 108 / 167 z tymczasowym domem **wedlug pochodzenia**: zolnierze (`Soldier` / `Mercenary`)
   -> ludnosc BK najblizszej osady frakcji i kultury (stamtad zdejmuje ich werbunek BK); chlopi, rybacy i bandyci -> hearth (stamtad
   zdejmuje ich gra i prawo wyrzutkow); zalogi karawan, patroli, garnizonow i milicji -> nigdzie, z licznikiem (z szablonu, E7).
   Po 108 / 167 zmienia sie tylko `LosersFlee.SendHome` i powrot z puli (3.3).
6. **Kategoria "bez osady" w "Ludzie:" (`PeopleLedger`)** to w wiekszosci echo ROT (`ROTTroopRecruiter` wola `OnTroopRecruited`
   drugi raz z osada `null` po kazdej wymianie rekruta): srednio 1 836-1 847 dziennie wobec 1 620-1 630 od notabli + 288-297 z karczmy.
   Wplywa na "zwerbowani przez wodzow" i "reszte" bilansu partii rodow (np. 18-13-16 doba 40: +2 278 zwerbowanych, reszta -2 012).
   Audyt (2.11 "ochotnicy z mapy", E2) i grupa C nie powinny tej kategorii czytac jako werbunku jencow ani ochotnikow.

## 10. Pytania do Jeffa
10.1 **Twoja autobitwa / "wyslij wojsko"** - czy ma dzialac ta sama regula (przegrani wroga uciekaja, jencow mniej; gdy Ty
przegrasz symulacje - Twoi ludzie uciekaja do domu i w las zamiast do lochow)? Rekomendacja: TAK, ale w fazie 2, po autotescie
AI-AI, z komunikatem w grze o uciekinierach (rozdz. 4).
(Liczby - wzor, 5%, 30% / 5%, 0.5 i 0.5 dla band, lista kultur z niewolnictwem wedlug Martina - dobrane w projekcie, bez pytania.)

## 11. Na pozniej (nie w tej paczce)
- "Zbiorka po klesce": czesc uchodzacych wraca do pana (najblizsza partia rodu w ciagu kilku dni) - dopiero to daje mniej werbunku;
  po 108 i 166.
- Bitwy pod murami, wypady, szturmy (szturm: kapitulacja albo rzez wedlug kultury) i morze (tonacy, ratowani z wody).
- "Bez pardonu" (N) poza Innymi: bunty chlopskie, Zelazni Ludzie na wybrzezu - tylko z kanonu.
- Faza 2 bitew gracza (rozdz. 4).
- Uwolnieni jency w bitwach poza H3 (oblezenia, rabunki) - w fazie 1 tylko liczeni (licznik U2, rozdz. 5); zamkniecie po pomiarze.
- Zaloga karawany z ludzi miasta (E7 / 108) - wtedy wiersz "z szablonu" w `SendHome` zamienia sie na "do miasta karawany".

## 12. Szkic wpisu CHANGELOG (dla implementacji)
`## 2026-10-0X (H3 / n9) - PRZEGRANI UCHODZA ZAMIAST GINAC: w bitwach AI w polu bez gracza ginie 5-65% pokonanych wedlug sytuacji
(poscig konnicy, rzeka / bagno / wawoz obok pola, przewaga, jakosc, konni przegrani uciekaja na otwartym polu), do niewoli 30% weteranow
i 5% reszty (chlopi tylko u ludow z niewolnictwem), reszta uchodzi i wraca tam, skad ja wzieto - zolnierze pol w las, pol do ludnosci BK
najblizszej osady swojego kraju i kultury; bandyci pol w las, pol do wsi; chlopi i rybacy do swojej wsi (hearth); zalogi karawan
z szablonu nigdzie (licznik, do 108); zwyciezca traci zabitymi najwyzej 5%, reszta jego poleglych to ranni; rozbici z kazdej bitwy
(takze gracza) juz nie znikaja; uwolnieni jency, ktorych nikt nie wzial, wracaja do domu; powrot z puli wyrzutkow wedlug zawodu;
przy wylaczonym prawie wyrzutkow vanilla dezerterzy nie dubluja rozbitych`
**Mod:** Armoury | **Pliki:** NOWY `LosersFlee.cs`; `OutlawLaw.cs` (`OnMapEventEnded`, `Daily` powrot z puli, linia "Wyrzutki:",
`SkipVanillaDeserters`, `IsOutlawParty` i `ReturnHome` internal); `Settings.cs` + `McmSettings.cs` (6 NOWYCH, opis `OutlawRoutedShare`);
`ArmouryBehavior.cs` (Reset, Daily); `SubModuleMain.cs` (ApplyAll: prefiks + postfiks wyniku bitwy, postfiks `RecruitPrisonersAi`).
Bez zapisu w grze. Nowy plik logu `h3-domy.csv`. Problem / Przyczyna / Zmiana / Ryzyko - rozdz. 1, 2, 3, 8 tego projektu;
**Status: NIEWGRANE - DO SPRAWDZENIA** (A/B 40 dob na tym samym DLL i zapisie, potem rok; progi rozdz. 7). **Reczna proba Jeffa:**
jedna bitwa w polu i jedna autobitwa - w `bitwy.log` linia "Bitwa: H3 pominieta (gracz)" przy kazdej, liczby gry bez zmian, rozbici
rozpisani na las / dom; sklad Twojej partii po bitwie taki sam jak bez H3.

## 13. Krytyka i odpowiedzi (przeglad 08/09.10, 15 uwag)
Kazda uwaga sprawdzona w dekompilacji (gra 1.4.8, BK, ROT, NavalDLC) i w logach 08.10 (17-44, 17-59, 18-13, 18-38) oraz skryptem
`h3scan/proj.py` (liczby krytyka powtorzone: tabory i rybacy 85, karawany 49 z 252 "do domu"; wojsko do domu 94 + 24 = 118 dziennie).
Falszywych uwag nie bylo (zadnej nie odrzucam); jedna przyjeta czesciowo (7 - inna liczba i inna podstawa szacunku). Tam, gdzie krytyk dal do wyboru kilka poprawek, wybor i jego powod sa w kolumnie "Co zmienione".

| # | Waga | Uwaga (skrot) | Werdykt i dowod | Co zmienione |
|---|---|---|---|---|
| 1 | krytyczne | chlopi, rybacy i zalogi karawan wracaja do ludnosci BK, ktorej nie stracili | **Przyjeta.** [K] `VillagerCampaignBehavior.cs:179, 187` (`Hearth -= (n+1)/2`), NavalDLC `FishingPartyCampaignBehavior.cs:186, 227`, `FishingPartyComponent : VillagerPartyComponent` -> `IsVillager` (`MobileParty.cs:4175`); `UpdatePopFromSoldiers` -> `AddManpowerFromSoldiers` (+manpower, `MilitaryData.cs:150-156`) -> `UpdatePopType` -> BEE (`PopulationData.cs:399`); karawany i patrole z szablonu (00-AUDYT 2.11; `PatrolPartiesCampaignBehavior.cs:649-651`) | 2.4 (pochodzenie: tabory -> `Hearth += 0.5 x n` swojej wsi; karawany, patrole, zalogi, milicje -> "z szablonu" z licznikiem), 3.2, 3.4, rozdz. 0, 7 (wiersz hearth wedlug zrodla), 8 |
| 2 | krytyczne | powrot z puli: bandyci (z hearth) szliby do BK | **Przyjeta.** [K] `TakeCommoners` `:222-240`, `CommonerFor` / `Draw` / `DrawOnly` `:271-292, 316-321, 638`, rozwiazane bandy `:394-395`; [P] 18-13-16 doba 40: "zolnierzy 1217" z 1343, rozwiazane bandy 50-122/d; log roczny 18-38-27: powroty 55-63/d | 3.3 wedlug `Occupation` (`Bandit` / `Villager` / `"~"` -> hearth, `Soldier` / `Mercenary` -> BK, `CaravanGuard` -> z szablonu); 1.5; linia "Wyrzutki:" ze skladem wedlug zawodu (5) |
| 3 | wazne | dom = siedziba wodza przesuwa ok. 118/d (43 tys./rok) z regionow werbunku | **Przyjeta.** [K] `RecruitmentApplyInternalPatch.cs:20` (zdjecie z osady notabla); BK sam przy rozwiazaniu partii oddaje tylko `Soldier` tej samej kultury (`BKClanBehavior.cs:1205-1236`) | 2.4 pkt 4: najblizsza bitwie osada BK frakcji i kultury jednostki (lancuch zastepczy); `h3-domy.csv` obok `zwerbowani_dzis` (= to, co zdjal `DeduceManpower`, wedlug regionu) zamiast kolumn w `ludzie-regiony.csv` (nie ruszamy `PeopleLedger`); prog w 7 |
| 4 | wazne | test: kontrola tautologiczna, 40 dob za malo, inny DLL, brak progow BK | **Przyjeta.** [K] `k + j + f = n` z definicji; `?.AddToCounts` przy `null` (`MapEvent.cs:1834-1847, 2019`); `ChooseWeighted` pusta lista -> `Nobles` (`MBRandom.cs:99-120`, enum BK `Nobles = 0`); `KeyNotFound` w `Manpowers[...] +=` (`MilitaryData.cs:154`); [P] rok bez H3: pula 10.5-12 tys., bandy 22.7 tys. | 3.1 krok 6 (postfiks: jency wzieci = j; uwolnieni ida do zwyciezcy jako ludzie, wiec sie nie mieszaja), 3.4 (przyrost `TotalPop` = przyjeci; licznik "jako szlachta"), 7: A/B na tym samym DLL i zapisie obowiazkowe, nachylenie dob 30-40, test roczny, wiersze ludnosc BK swiata i wcieleni jency (nowy licznik zamiast "bez osady" - patrz 7) |
| 5 | wazne | bandy 100% w las - samoodnawialne, suwak awaryjny ich nie dotyczy | **Przyjeta** (0.5 miesci sie w propozycji "1.0 albo nizej"). [P] proj.py: dzis banda 69% zabitych / 31% jencow, po H3 ok. 27/d do puli | NOWY suwak `OutlawBandRoutedShare`, domyslnie **0.5** (nie 1.0): ta sama polowa co u zolnierzy, reszta do hearth (bandyta pochodzi z hearth); 7: ktory suwak przy jakim progu (najpierw bandy) |
| 6 | wazne | przy `OutlawLawEnabled = false` vanilla dezerterzy z rozbitych + odeslanie do domu = podwojnie | **Przyjeta.** [K] `SkipVanillaDeserters => !On` (`OutlawLaw.cs:733`), `DesertersCampaignBehavior.cs:101-121` (RoutedInBattle + DiedInBattle) | 3.2: `Skip` = `!On && !LosersFlee.On`; przy `!On` wszystko wedlug pochodzenia, nic do puli; 1.3 |
| 7 | wazne | "netto werbunek -2..+5%" nie wynika z liczb spec | **Przyjeta czesciowo.** Zarzut slusznie: liczby nie byly policzone. Ale +17% krytyka to gorna granica wobec doby 40 (942-997/d); wobec sredniej 40 dob (1 620-1 630/d) gorna granica to +11%. I [K][P] kategoria "bez osady" (1 836-1 847/d) to w wiekszosci echo `ROTTroopRecruiter` (`ROTTroopRecruiter.cs:105-122, 216`), nie wcielenia jencow - jest nawet mniejsza od notable + karczma | 8: przedzial -50..+170/d = -3..+11% (srednia) / -5..+18% (doba 40), oczekiwanie blizej dolnej granicy; postfiks `RecruitPrisonersAi` (licznik wcielonych jencow); 7: wiersze notable (-10..+20%), wcieleni jency, "bez osady - nie uzywac"; 0.4; 9.1, 9.6 |
| 8 | wazne | dom = ludnosc BK to trzecia droga, nieopisana wobec "108 albo karczma 167" | **Przyjeta.** 00-AUDYT E10 (wiersz 547), raport 04 W1 | 9.5 (jawna decyzja: H3 przed 108/167, dom wedlug pochodzenia, po 108 zmienia sie tylko `SendHome` i 3.3); 2.4 koniec. STAN-PRAC i 00-AUDYT - do dopisania przy wdrozeniu (ta paczka zmienia tylko swoj dokument) |
| 9 | drobne | T w jednym punkcie prawie nie dziala; F przy T = 1 przeczy Courtrai | **Przyjeta.** [K] `_eventTerrainType = Position.Face.FaceGroupIndex` (`MapEvent.cs:1162`), `IsNavalMapEvent => !Position.IsOnLand` (`:247`); `IMapScene.GetTerrainTypeAtPosition` (`:18`) | 2.2: `- 0.10 x F x (1 - T)`; T z 9 punktow (promien 1 i 2); 2.6 Courtrai 30%; 2.7 nowy wiersz; udzial T = 1 w linii dnia i prog 10-30% (7). Uwaga: F dalej lekko zaniza Crecy i Azincourt (T = 0) - zapisane w 2.6 |
| 10 | drobne | jency z taborow wszedzie 5%; straznicy karawan jak niewalczacy | **Przyjeta** (inny mechanizm niz przyklad krytyka: lista kultur, nie prawo BK). HISTORIA 4.3 "jency 0-5% (tylko kultury z niewolnictwem)"; [K] straznicy `occupation="CaravanGuard"` w ROT | 2.2 (karawana wzorem), 2.3 (tabory: 5% tylko gdy kultura zwyciezcy na liscie; lista wspolna z rabunkiem, wedlug Martina - nie prawo BK `SlaveryAserai`, bo decyzja Jeffa mowi o kulturach, a BK nadaje prawa krolestwom po swojemu), 2.7, 3.5, 7 |
| 11 | drobne | brak `LootCollectorPatch` w tabeli, zly numer linii, AIInfluence "nie sprawdzone" | **Przyjeta.** [K] `ROT.HarmonyPatches.Dragon.LootCollectorPatch` (prefiks na tej samej metodzie, tylko smoki i wilki z `ItemRoster`); warunek trafienia na `MapEventSide.cs:993` | 1.4 (dwa nowe wiersze ROT, AIInfluence z wynikiem skanu), 1.2 (`:993`), 3.1 (wlasciciele latek 4 metod w linii startowej), 7 (prog) |
| 12 | drobne | `LootDefeatedPartyPrisoners` gubi jencow pokonanych, gdy nikt ich nie moze wziac | **Przyjeta.** [K] `MapEvent.cs:1544-1547` (zdjecie zawsze), `GetLootPrisonerChances` (`DefaultBattleRewardModel.cs:256-280`); gdy lista niepusta - do zwyciezcy jako ludzie (`RosterToReceiveLootMembers`) | 1.3; 3.1 krok 3a (w bitwach H3: zdjac i oddac wedlug pochodzenia, licznik "uwolnieni"); poza H3 tylko licznik (11) |
| 13 | drobne | sakiewka ludzi rozbitej partii w calosci do zwyciezcy | **Przyjeta** (druga z propozycji: jawna decyzja). [K] `MenPurse.cs:63-79` | 8: swiadomie zostaje - sakiewka to oboz i tabor, przepadaly na rzecz zwyciezcy [H]; powrot do tematu z W1 / 171 |
| 14 | drobne | dzielenie przez zero, niespojne listy po wyjatku w polowie, limit 5% | **Przyjeta.** [K] `AddToCounts` 0/0 na brakujacym typie -> `FailedAssert` (`TroopRoster.cs:441`) | 3.1: dwie fazy (plan, sprawdzenie, dopiero potem `AddToCounts`; zmiany zerowe pomijane), zabezpieczenia O / Q / C / F; 2.5: limit **5% zostaje** - opisana decyzja (Sempach, Visby to walka do konca bez ucieczki przegranego) |
| 15 | drobne | autotest nie sprawdzi bitew gracza | **Przyjeta.** | 3.1 i 5: linia "Bitwa: H3 pominieta (gracz)"; 4 i 12: reczna proba w CHANGELOG (bitwa w polu + autobitwa, sklad partii gracza bez zmian) |

**Dodatkowo znalezione przy sprawdzaniu (nie bylo w uwagach):** (a) `CanTroopBeTakenPrisoner == false` tez wyrzucalby ludzi w nicosc -
H3 daje wtedy j = 0 (2.3; dzis zawsze `true`); (b) echo `ROTTroopRecruiter` w kategorii "bez osady" (9.6) - dotyczy tez innych paczek
liczacych werbunek; (c) log 18-38-27 (zapis po roku, bez H3): pula 10.5-12 tys., bandy 1 123-1 128 (22.6-22.8 tys. ludzi) - punkt
odniesienia dla testu rocznego.

## 14. Wykonanie (kod, noc 08/09.10) - co w kodzie inaczej niz w projekcie
Kod: `Armoury/src/LosersFlee.cs` (NOWY), `OutlawLaw.cs` (`RoutedH3`, powrot z puli, dopisek "Wyrzutki:", `SkipVanillaDeserters`),
`Settings.cs` + `McmSettings.cs` (686 ustawien), `ArmouryBehavior.cs`, `SubModuleMain.cs`; wpis w CHANGELOG. Build kod 0; gra nie uruchomiona.
Sygnatury sprawdzone w DLL gry 1.4.8 (libs = gra, md5 zgodne) i w RBM / ROT (CHANGELOG). Roznice (kazda drobna, z powodem):
1. **Linia "Wyrzutki:"** - sklad puli wedlug zawodu, rozbici rozpisani na las / dom / bez domu z "roznica" i powrot z puli wedlug zawodu
   sa DOPISANE na koncu linii ("H3 (przegrani uchodza): ..."), a nie zamiast "(zolnierzy X, prostych Y)" - `tools/sprawdz_logi.py`
   czyta dotychczasowe pola po dokladnych napisach (" ludzi (zolnierzy ", ", prostych ", ", rozbitkowie "), wiec ich nie ruszam.
2. **BK przyjal mniej niz n bez wyjatku** (przyrost `TotalPop` < n): przyjeci = faktyczny przyrost, reszta wraca do wolajacego jako
   "bez domu" (pula) - zamiast liczyc n jako przyjetych; licznik "BK przyjal inaczej" (wywolania i roznica) zostaje sprawdzeniem.
   Zastepcza klasa chlopow tylko wtedy, gdy po wyjatku ludnosc osady sie nie ruszyla (wyjatek w srodku dopisywania BK nie moze dac
   drugiego dopisania tych samych ludzi).
3. **Bandyci i prosci do hearth** z bitwy, ktorej najblizsza warownia nie ma wsi - do najblizszej warowni, ktora je ma
   (`OutlawLaw.HearthRegionAt`); `ReturnHome` nic by nie dopisal i ludzie by znikneli. Powrot z puli do hearth (3.3) bez zmian (jak dzis).
4. **Wlasciciele latek** wypisani dwa razy: w linii startowej i przy pierwszej dobie kampanii (czesc modow lata dopiero przy starcie gry).
5. **H3 wylaczony:** linia "Przegrani (H3): dzien D - WYLACZONE" z ludnoscia BK swiata i licznikiem wcielonych jencow AI - przebieg A
   testu A/B musi te liczby zmierzyc (rozdz. 7); postfiks `RecruitPrisonersAi` liczy zawsze.
6. **Linia "Bitwa: H3 pominieta (gracz)"** pisze `OutlawLaw.RoutedH3` przy `MapEventEnded`, a nie prefiks - podzial rozbitych na las /
   dom znany jest dopiero tam; jest przy kazdej bitwie gracza (takze bez wyniku i ponizej `BattleChronicleMinMen`).
7. **"gra dalaby" jency** w linii dnia to wartosc oczekiwana (ranni + 0.25 x stojacy, gdy zwyciezca bierze jencow), nie losowanie.
8. **Powrot zolnierza z puli do BK:** n = min(`RoundRandomized(udzial)`, cale osoby w kluczu) - klucz z mniej niz jednym czlowiekiem
   czeka w puli (bez ulamkow ludzi w ludnosci BK). **Po przegladzie kodu (uwaga 8):** "czeka" znaczylo "na zawsze" (bandy tez nie biora
   ulamkow) - reszta < 1 zamyka sie teraz losowo (3.3): z szansa rate na dobe; wtedy z szansa d jeden czlowiek do BK, inaczej nikt.
9. **Uwolnieni jency bez odbiorcy:** dom bez frakcji (pochodzenie nieznane) - najblizsza osada BK kultury jednostki, dalej warownia
   regionu bitwy; tabor wsi jency (gdy zwyciezca z kultury z niewolnictwem) - ta sama regula tieru co reszta (30% / 5%; chlopi to tier < 4).

## 15. Przeglad kodu (noc 08/09.10, 8 uwag) - werdykty i poprawki
Kazda uwaga sprawdzona w kodzie drzewa n9-h3, w dekompilacji gry 1.4.8 (`ore-supply/cs`), w `ROT-Troops.xml` i w `bitwy.log` 08.10.
Falszywych nie bylo - wszystkie 8 przyjete. Build kod 0, gra nie uruchomiona.

| # | Waga | Uwaga (skrot) | Werdykt i dowod | Co zmienione |
|---|---|---|---|---|
| 1 | wazne | zolnierze lordow z szablonu / z zalog ida przez `SendHome` do BK - nowy strumien ludzi z niczego, test BK go nie widzi | **Przyjeta.** [K] `LordPartyComponent.cs:29-40` (`InitializeMobilePartyAroundPosition(DefaultPartyTemplate)` dla lordow spoza rodu gracza); `GarrisonTroopsCampaignBehavior.cs:542-610`; BK / ROT / BEE / BKROTPatch tych metod nie lataja (szukane) | Pomiar (bez zmiany zachowania): 3 postfiksy z prefiksem (`InitializeLordPartyProperties` `Priority.Last`, `TakeTroopsFromGarrison`, `LeaveTroopsToGarrison`), linia "dziura werbunku" (takze przy WYLACZONE); 7 - wiersz BK poprawiony, nowy wiersz z progiem 20%; 8 - sprostowanie |
| 2 | drobne | "partie przegranych z szeregowymi po bitwie" zawsze 0; "roznica" tautologiczna; "zgubieni" wlicza partie spoza planu | **Przyjeta.** [K] `MapEvent.cs:2034` (`AddToCountsAtIndex(-Number, -WoundedNumber)` dla kazdego szeregowego kazdej partii przegranej); `OutlawLaw.H3Segment` liczy roznice z tych samych licznikow | `_losersLeft` usuniety; nowe sprawdzenie list typ po typie na koncu zastosowania (5 list) i w postfiksie (zabici, rozbici); "zgubieni" tylko z bitew z pelnym planem (`BP.Covered`), licznik "bez sprawdzenia"; "roznica" nazwana "kontrola kodu" (3.1 krok 6, 5, 7) |
| 3 | drobne | liczniki domow mieszaja trzy zrodla; powrot z puli doby D w linii D+1 | **Przyjeta.** [K] `ArmouryBehavior.cs:1230` (`LosersFlee.Daily`) przed `:1259` (`OutlawLaw.Daily`) | Liczniki wedlug zrodla (`_bkBy` / `_hearthBy` / `_tplBy`), segment "domy wedlug zrodla" z jawna doba linii "Wyrzutki:" dla puli; kolejnosci Daily NIE zmieniam (linia "Przegrani (H3)" ma byc zaraz po "Bitwy:") |
| 4 | drobne | "- 0.10xF" wypisywane przy T = 1, choc nie dziala | **Przyjeta.** [K] `LosersFlee.cs` wzor `- WF x F x (1 - T)` | Wypisana wartosc czynna `F x (1 - T)`, przy T = 1 i F > 0 dopisek "(F x nie liczy sie przy T = 1)" |
| 5 | drobne | przy wylaczonym prawie wyrzutkow bandyta do hearth, choc bandy sa z szablonu | **Przyjeta.** [K] `OutlawLaw.GateClan` / `GateGlobal` zwracaja true przy `!On`, `RosterPostfix` wychodzi przy `!On` | `SendHome` 2a: `Bandit` przy `!OutlawLaw.On` -> "z szablonu" (licznik); takze uwolnieni jency-bandyci; 3.2, 3.4 |
| 6 | wazne | wighty Innych z rozbitych do ludnosci BK (occupation Soldier) | **Przyjeta.** [K] `ROT-Troops.xml:24682-25088` (`occupation="Soldier"`, `Culture.whitewalker`); `RoutedH3` bez filtra Innych; [P] `bitwy.log` 17-44-34: "SallyOut pod Hornfoot ... Quort's Party (Others) ... rozbici 341" | `RoutedH3`: partia Innych i wighty - ani do puli, ani do domu (licznik "Inni - do niczego"; decyzja: polowa do puli NIE - wight to nie czlowiek, a bandy werbowalyby go z puli); `SendHome` 0: wight -> nic (zabezpieczenie); `KeyKind`: NOWY `KeyUndead` (nie `KeySoldier`) - stare klucze wygasaja do niczego. **Do STAN-PRAC przy wdrozeniu:** stare wighty w puli moga byc dalej werbowane przez bandy (`Draw` / `DrawOnly`), a przy wylaczonym H3 polowa rozbitych wightow dalej idzie do puli - stara dziura sprzed H3 |
| 7 | drobne | "czas" bez `RoutedH3` i powrotu z puli; `FindHome` skanuje ok. 800 osad na bitwe | **Przyjeta.** [K] `Stopwatch` tylko w prefiksie, postfiksie i `Daily` | `LosersFlee.AddTicks`: czas `RoutedH3` i galezi H3 powrotu z puli; "czas" w linii dnia = suma z rozbiciem na trzy czesci. Pamieci `FindHome` na dobe nie dodaje (decyzja Jeffa 08.10: optymalizacja na koniec) - rozdz. 6 |
| 8 | drobne | klucz zolnierza z d < 1 nigdy nie wraca i nie znika | **Przyjeta** (z inna poprawka niz pierwsza propozycja przegladu: "z szansa m wyslij 1 i usun klucz" daje w calym zyciu klucza oczekiwanie 1 czlowieka na d < 1 - ludzie z niczego). [K] `OutlawLaw.Daily` galaz `KeySoldier`: `Floor(d) = 0` -> n = 0, `continue` omija `d -= m`; `DrawOnly` bierze tylko `Floor` | Reszta < 1: z szansa rate na dobe klucz sie zamyka - z szansa d jeden czlowiek do BK (`SendHome`), inaczej nikt; oczekiwanie = d. Licznik w dopisku "Wyrzutki:" (3.3, 14 pkt 8) |
