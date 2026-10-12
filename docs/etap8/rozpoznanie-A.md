# Etap 8 - rozpoznanie A: 8.1, 8.2, 8.4, 8.6, 8.7, 8.8, 8.10 (12.10.2026)

Zakres: pozycje PLAN-DO-KONCA-MODA-2026-10-09.md rozdz. "Etap 8" 8.1, 8.2, 8.4, 8.6, 8.7, 8.8, 8.10. Tylko odczyt - kod, gra,
ustawienia i zapisy Jeffa nietkniete, gry nie uruchamialem. Zrodla pozycji (PLAN rozdz. Zrodla): a13 = audyt-2026-10-09/13-UMIEJETNOSCI.md
(Z1-Z18), A05 = AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md, a04 = audyt-2026-10-09/04-wojna-armie-lupy-okupy.md,
SP = STAN-PRAC.md "Otwarte sprawy" pkt 3, AUDYT-PELNY-EKONOMIA.md (N4, S3). Kazde twierdzenie audytow sprawdzone na dzisiejszym kodzie -
czesc juz zrobiona w etapach 1-7 (zaznaczone).

Oznaczenia dowodow: [K] kod `SCRATCH\zb\58` (galaz w-toku/zbior-5-8, b461cc5; sciezki wzgledem `Armoury\src`), [G] gra 1.4.8
(`SCRATCH\dec32\cs_full`), [RBM] `SCRATCH\e8A\rbmcombat.cs` (ilspycmd RBMCombat.dll, 12.10), [SW] Spoils of War (`SCRATCH\e8A\donate.cs`,
`SCRATCH\e8A\lootcol.cs`, ilspycmd RealisticLoot.dll), [DTE] `SCRATCH\audyt-zloto\DTE\DynamicTroopEquipmentReupload\...`,
[L] `Modules\Armoury\Armoury-2026-10-12_02-44-09.log` (120 dob, WGRANIE 33; doba 1 = dzien 108836, doba 120 = 108955),
[L728] `SCRATCH\kopia-728\Armoury-2026-10-10_20-58-02.log` (dlugi bieg, starszy DLL), [ID] `Documents\...\CrashScribe\items-dump.csv`
(12.10 03:22, ceny PO HistoricalPrices), [D] dokument. SCRATCH = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`.

Propozycja: **TAK** (do etapu 8), **POMIAR** (najpierw linia logu, kod po liczbach), **ZROBIONE** (zamknac dowodem z logu),
**JEFF** (prawdziwy wybor). NK = dziala tylko w nowej kampanii.

---

## 0. Streszczenie

| Poz. | Co | Stan na dzis (dowod) | Propozycja | Latki (cel) | Koszt pracy | NK |
|---|---|---|---|---|---|---|
| 8.1a | Ksiega umiejetnosci (Z4) | brak - 0 linii "Umiejetnosci:" w [L] | TAK, pierwsza (pomiar "przed" dla 8.1b-d) | `HeroDeveloper.AddSkillXp`, `TroopRoster.AddXpToTroop` (prefiksy-liczniki) | S, uwaga na goracy punkt (8.17) | nie |
| 8.1b | XP za strzal tylko w bitwie (Z7) | RBM 30/60/50 XP za KAZDY strzal bohatera, takze arena [RBM:3628-3677] | TAK | RBM `CampaignChanges+OverrideOnAgentShootMissile.Postfix` (prefiks false) | S | nie |
| 8.1c | Zarzadca z jedzenia (Z8) | XP liniowo od zjedzonej ilosci, zima +50% [G DefaultSkillLevelingManager.cs:457-464] | TAK po 8.1a, przed proba zimy | `DefaultSkillLevelingManager.OnFoodConsumed` | S | nie |
| 8.1d | Zwiad z marszu (Z10) | XP od predkosci x ludzie^0.66, prog 5, premia tylko AI [G :418-440] | TAK po 8.1a | `OnTraverseTerrain`, `OnAIPartiesTravel` | S | nie |
| 8.2a | Dar Spoils dla miasta -> zbrojownia zalogi (Z9) | sztuki znikaja [SW donate.cs:142-171] | TAK | `DonateEquipmentBehavior.OnDonateScreenClosed` (prefiks+postfiks) | S | nie |
| 8.2b | Resztki trofeow nie znikaja (Z9) | zostaja w "pozostalosci pola" Spoils, ktora wygasa; mala bitwa - od razu nic [SW lootcol.cs:1957, 3372-3390] | TAK (po broni i zbroi do zapasu musztry) | `LootCollectionBehavior.TryCreateBattlefieldRemnant` (postfiks) | S-M | nie |
| 8.2c | Drobne XP (Z17) | paser Roguery = 5% zaplaty [K SpoilsSeal.cs:531]; samonaprawa XP bez wzgledu na przywrocony stan [K SmithMenu.cs:1096, 1132] | TAK (paser, samonaprawa); RBL i GT - POMIAR | nasze pliki + RBL | S | nie |
| 8.4a | AI zuzywa amunicje w swoich bitwach | 0 - AiWear pomija amunicje [K AiWear.cs:275, ArmouryBehavior.cs:2301-2304] | TAK (najpierw sam log) | brak nowych - `AiWear.OnMapEventEnded` | S | nie |
| 8.4b | Odzysk w misji wedlug strony agenta | `PlayerWon()` dla obu stron [K AmmoRecovery.cs:40-41, 51-61] | TAK | DTE `Global.ProcessAgentEquipment` (prefiks: biezacy agent) | S | nie |
| 8.4c | Oszczepy jak strzaly | dwie definicje "amunicji": cena x8 dla Thrown [K HistoricalPrices.cs:292-303], zuzycie bez Thrown [K ArmouryBehavior.cs:2266-2270] | TAK | brak nowych | S | nie (ceny przy starcie sesji) |
| 8.6a | Wraki i zdobyta zbrojownia 100% dla gracza | bez trzeciej [K BattlefieldLaw.cs:530-542, 633-646] | TAK (wraki wg zabojcy; zbrojownia pokonanych - JEFF) | nasze | S | nie |
| 8.6b | Dubel lupu: kopia zbrojowni ocalalego pokonanego | konie ZROBIONE (E6/K1); reszta sprzetu nadal kopia [DTE EveryoneCampaignBehavior.cs:684-708, 747-781] | TAK (to samo okno co HorseFate) | DTE `EveryoneCampaignBehavior.OnMapEventEnded` (juz latany) | S | nie |
| 8.6c | Dubel lupu w "Send troops" | DTE 100% do zbrojowni + loteria Spoils [K BattlefieldLaw.cs:283, 583] | TAK (niski priorytet - Jeff nie uzywa) | `AfterGenerateLoot`, `CutCasualtyLootPrefix` | S | nie |
| 8.7a | Zalogi zuzywaja sprzet | ZROBIONE w K1 (noc/sklad7) [K AiWear.cs:264, AiGear.cs:276-291; CHANGELOG zb58:5592] | ZROBIONE - dopisac "w tym zalogi" do linii "Zuzycie AI" i zamknac | - | S (log) | nie |
| 8.7b | Monopole korony | 5% od STANU zysku co dobe [K KingdomTreasury.cs:444-452, G Workshop.cs:36]; w tescie 0 przez 120 dob [L492..35950] | POMIAR, potem TAK | brak nowych | S | nie |
| 8.8a | Zakupy AI: jakosc ginie | dodatni stan placony i gubiony [K AiGear.cs:608, 800; AiWear.cs:150-156] | TAK (AI nie kupuje sztuk z PriceMultiplier > 1) | nasze | S | nie |
| 8.8b | Zakupy AI bez sufitu ceny | jedyny warunek: budzet [K AiGear.cs:610] | POMIAR (cena/wartosc), potem TAK | nasze | S | nie |
| 8.8c | Naprawy AI staja | `break` na pierwszej sztuce bez pieniedzy [K AiWear.cs:511, 544]; zaleglosc 90 -> 71 432 szt. [L336, L35835] | TAK | nasze | S | nie |
| 8.8d | Zuzycie AI: stale 4% i "nowe sztuki" | stale share [K AiWear.cs:255], obija tylko sprawne [K :280-281] | TAK | nasze | S | nie |
| 8.8e | Twoj sprzet na arenie | ciosy z areny/turnieju/kleski do ksiegi zuzycia [K FieldCraft.cs:333, ArmouryBehavior.cs:1827, 1878] | TAK | nasze | S | nie |
| 8.10a | Budzet platnerzy na starcie | `break` przy losie tieru bez sztuk [K ColdStart.cs:209-211] | TAK | nasze | S | TAK |
| 8.10b | Kon bez uprzezy | StartKit zmienia konia bez sprawdzenia rodziny uprzezy [K StartKit.cs:57-70] | TAK (interpretacja - patrz 7.2) | nasze | S | TAK |
| 8.10c | Komplety rekrutow z niczego (start) | AI zamkniete (ZROBIONE, [L35819]); ok. 240 partii lordow z doby 1 bez dorobku [L231, L341, L337/653] | TAK | nasze (`ColdStart.FillToTemplate` juz jest) | S | TAK |
| (1.12) | Rekruci gracza t2+ bez darmowego kompletu | decyzja Jeffa "jak u AI" (STAN-PRAC:354, 448), NIEZROBIONE [DTE RecruitmentPatch.cs:36-70] | poza etapem 8 (PLAN 1.12); jesli etap 1 tego nie zrobi - dolaczyc do 8.10 | DTE `RecruitmentPatch.AddStartingEquipmentToPlayerArmory` | S | nie |

---

## 1. 8.1 Ksiega umiejetnosci; XP za strzal tylko w bitwie; zarzadca i zwiad

Zrodlo: a13 rozdz. 5 (Z4, Z7, Z8, Z10). Z1, Z2, Z3, Z5, Z5b, Z14a/b sa juz w kodzie i w logu (DonationXpLaw [L16], SpoilsSeal [L157],
musztra [L215]). Z4, Z6, Z7, Z8, Z9, Z10, Z17 - nie ma (grep `ShotXp|StewardFood|ScoutXp|SkillLedger|SpoilsGoodsHome|BookPriceFix` w zb58 = 0).

### 1.1 Ksiega umiejetnosci (Z4)
- **Dowod:** w [L] 0 linii "Umiejetnosci:" / "XP wojska:"; jedyne linie XP to startowe [L14] "FairXp: nauka z ciosu wyrownana w 3 modelach"
  i [L83] "XP kowalstwa od dawnych wartosci 3/3". Brak latki na `HeroDeveloper.AddSkillXp` w Armoury, CrashScribe, GT, RC (grep).
- **Przyczyna:** nigdy nie zbudowana; wszystkie "ile dziennie" w a13 to szacunki [S].
- **Co zmienic:** nowy `SkillLedger.cs` (sam log): prefiks-licznik na `HeroDeveloper.AddSkillXp` (umiejetnosc x zrodlo x grupa bohatera:
  gracz / towarzysze / lordowie AI) i na `TroopRoster.AddXpToTroop`; zrodlo z [ThreadStatic] znacznika ustawianego w krotkich prefiksach znanych
  wolajacych (strzal RBM, OnFoodConsumed, OnTraverseTerrain, musztra, kuznia, Spoils); raz na dobe "Umiejetnosci:" i "XP wojska:", raz na
  tydzien mediana/p90 poziomow lordow. Wylacznik `SkillLedgerLog`.
- **Koszt:** S. **Uwaga 8.17:** `AddSkillXp` to goracy punkt (kazdy cios, strzal, godzina marszu kazdej partii). Licznik musi byc tablica
  [umiejetnosc, zrodlo] bez alokacji i bez napisow w prefiksie (napisy tylko w linii doby); cel < 0.1 s na dobe (a13). Po pomiarze mozna go wylaczyc.
- **NK:** nie. **Kolejnosc:** pierwszy - daje stan "przed" dla 1.2-1.4.

### 1.2 XP za strzal tylko w bitwie (Z7)
- **Dowod:** [RBM:3628-3677] `CampaignChanges.OverrideOnAgentShootMissile.Postfix` (patch na `Mission.OnAgentShootMissile`): jesli
  `shooterAgent.IsHero && Campaign.Current != null` - `AddSkillXp` za kazdy wypuszczony pocisk: klasa 16 (luk) 30, 17 (kusza) 60, 21-23
  (bron miotana) 50, 18 - 30. Jedyny warunek to kampania - arena, turniej i cwiczenia tez placa, chybienie tez. Nasz `FairXp` [K FairXp.cs:27-100]
  prostuje tylko `GetXpFromHit` (trafienia). Druga klasa o tej samej nazwie (RangedRework, [RBM:6879-6882]) robi predkosc pocisku, nie XP.
- **Przyczyna:** RBM placi za wypuszczenie, nie za trafienie; FairXp tego nie objal.
- **Co zmienic:** prefiks `return false` na `RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix` (prywatna klasa zagniezdzona -
  `AccessTools.TypeByName`, nie ruszac RangedRework); wlasny postfiks w FairXp: tylko `Mission.CombatType == Combat` i `MapEvent.PlayerMapEvent != null`,
  stawki 1/3: luk 10, kusza 20, miotana 17, proca 10; arena/turniej 0. Wylacznik `ShotXpFair`. Log startu "FairXp: strzal RBM wpiety / nie znaleziono".
- **Koszt:** S. **NK:** nie. **Ryzyko:** nowa wersja RBM zmieni nazwe klasy - latka loguje "nie znaleziono" i nic nie psuje.

### 1.3 Zarzadca z jedzenia (Z8)
- **Dowod:** [G DefaultSkillLevelingManager.cs:457-464] `OnFoodConsumed`: XP = round(-BaseFoodChange x 100) x (rodzaje - 2) / 3 przy >3 rodzajach -
  liniowo od zjedzonej ilosci (lord 350 ludzi, 5 rodzajow ok. 1 750 surowego dziennie, a13 tab. Steward). Zima podnosi zuzycie
  (`WinterBite.FoodPostfix` na `CalculateDailyBaseFoodConsumptionf` [K FoodChain.cs:50, WinterBite.cs:204]) - wiec zima = +50% nauki.
- **Co zmienic:** prefiks na `OnFoodConsumed`: 50 x sqrt(ludzie) x (rodzaje - 2) / 3, rodzaj liczony od pol dnia zapasu, zima x1.25 od pory roku
  (WinterBite tylko czytany). NavalDLC przekazuje do tej metody (a13). Wylacznik `StewardFoodXpFair`.
- **Koszt:** S. **NK:** nie. **Skutek:** Steward lordow AI z jedzenia ok. -47% (350 ludzi), mala druzyna wiecej - dlatego po 1.1 (pomiar przed/po).

### 1.4 Zwiad z marszu (Z10)
- **Dowod:** [G :424-440] `OnTraverseTerrain`: XP = predkosc x (1 + ludzie^0.66) x 0.15 (las/pustynia/snieg 0.25), placone od 5 (partia
  do ok. 16 ludzi na rowninie - nic); [G :418-422] `OnAIPartiesTravel`: AI dodatkowo 3-5 na godzine. Nasze `TerrainEase`, `NightRest`, `SightRange`
  Scoutingu nie czytaja (a13).
- **Co zmienic:** prefiks na obu: stale 15/h (rownina), 25/h (trudny teren), karawany polowa, bez progu, premia tylko-AI = 0. Wylacznik `ScoutXpFair`.
  Z13 (zwiadowca noca) to nowa mechanika - NIE w 8.1 (a13 pkt 2 rozdz. 4 - tylko na slowo Jeffa).
- **Koszt:** S. **NK:** nie. **Skutek:** lord 350-450 ludzi ok. -50%, gracz ze 100 ludzmi bez zmian.

---

## 2. 8.2 Dary Spoils do zbrojowni zalogi; drobne doswiadczenie

### 2.1 Dar dla miasta (Z9, czesc 1)
- **Dowod:** [SW donate.cs:142-237] `DonateEquipmentBehavior.OnDonateScreenClosed`: liczy sztuki pozostawione po lewej, `_donateScreenRoster = null` (:171) -
  sztuki znikaja; w zamian `Town.Security += 0.7 x n` (:191), `Militia += 0.4 x n` (:192), relacje z notablami 1 na 5 sztuk, Trade/Charm XP (:229-230 -
  zdjete nasza latka `GiftXpTranspiler` [K SpoilsSeal.cs:1672], log [L157] "dar dla miasta bez XP"). Zasada "nic nie znika" (ksiega obiegu 169) lamana.
- **Co zmienic:** prefiks zapamietuje kopie `_donateScreenRoster` (Amount > 0), postfiks wklada sztuki do zbrojowni DTE zalogi tego miasta
  (`AiGear.AddToArmory(garrison, item, n)` + `AiWear.NoteBought(garrison, el, n)` - stan Plundered zostaje zapisany, jak dostawy GarrisonCarts).
  Brak zalogi albo DTE odrzuca - na polke miasta bez zaplaty (porzucony dobytek, regula HorseFate (2)). Wylacznik `SpoilsGoodsHome`.
- **JEFF (tylko pytanie, nie w paczce):** `Militia += 0.4 za sztuke` tworzy milicjantow z niczego. Rada: osobna decyzja (ludzie z niczego to temat etapu 4),
  nie dokladac "przy okazji".
- **Koszt:** S. **NK:** nie.

### 2.2 Resztki trofeow (Z9, czesc 2)
- **Dowod:** po "Leave" bron i zbroje ida najpierw do zapasu musztry ([K SpoilsSeal.cs:1205-1220], [L215] "Spoils 1/1 (trofea przy Leave)"),
  XP za resztki wylaczone ([K SpoilsSeal.cs:1306-1317]); reszta trafia do `TryCreateBattlefieldRemnant` [SW lootcol.cs:1957, 3372-3390] - pozostalosc pola
  powstaje tylko przy bitwie >= `RemnantMinEnemies` i wygasa po `RemnantLifetimeHours`; mniejsza bitwa albo wygasniecie = sztuki znikaja.
- **Co zmienic:** gdy pozostalosc nie powstala (mala bitwa) - resztki od razu na polke najblizszej przyjaznej warowni bez zaplaty (ta sama regula co
  "dobytek rozbitych" HorseFate (2)); wygasniecie pozostalosci - POMIAR, czy Spoils ma zdarzenie wygasniecia (jesli nie - zostawic, bo to rzadkie).
- **Koszt:** S (bez wygasania), M (z wygasaniem). **NK:** nie.

### 2.3 Drobne XP (Z17)
- **Dowod:** paser: `Hero.MainHero.AddSkillXp(Roguery, paid x 0.05)` [K SpoilsSeal.cs:531] - XP od ceny; samonaprawa: 30 + 20 x tier niezaleznie od
  tego, ile stanu przywrocono [K SmithMenu.cs:1096, 1132]; RBL szpieg/sabotaz/glos za zloto (a13: rbl.cs:1320, 2123-2132 - niesprawdzone przeze mnie);
  gospodarz turnieju GT (a13: TourneyBehavior.cs:600 - niesprawdzone).
- **Co zmienic:** paser 25 Roguery za udana sprzedaz; samonaprawa XP x udzial przywroconego stanu (min. 25%); RBL i GT - najpierw sprawdzic w dekompilacji
  (POMIAR), osobny wylacznik na kazda sprawe (a13 tab. Z17). "Na Mur bez Roguery" - razem z I1 (zrobione w etapie 4? - sprawdzic przy paczce).
- **Koszt:** S. **NK:** nie.

---

## 3. 8.4 Amunicja: odzysk AI jak u Ciebie; oszczepy jak strzaly

Zrodlo: A05 wojna/W10 i produkcja/W10a, SP pkt 3 ("AmmoRecovery liczy wynik bitwy z perspektywy gracza takze dla AI").

### 3.1 AI nie zuzywa amunicji w swoich bitwach
- **Dowod:** `AiWear.OnMapEventEnded` pomija sztuki `ArmouryBehavior.NoWear` [K AiWear.cs:275] = `IsAmmo || IsBeast || IsGoods` [K ArmouryBehavior.cs:2301-2304];
  `AmmoRecovery` dziala tylko w misji gracza (hak na DTE `Global.IsAmmoAndEmpty`) [K AmmoRecovery.cs:70-79]. Log strzelarzy (172): AI kupuje strzaly tylko
  do kompletow nowych ludzi - doba 1: 10 snopow strzal / 2 belty [L351], doba 30: 367/180 [L8911], doba 120: 280/25 [L35828] przy produkcji 290-320 snopow strzal
  i 130-480 beltow dziennie. Zadna linia nie pokazuje kolczanow zuzytych przez AI.
- **Co zmienic:** w `AiWear.OnMapEventEnded` (ta sama petla po partiach lordow i zalog): zdjac ze zbrojowni kolczany/snopy = strzelcy partii x natezenie
  bitwy (ten sam wzor co 3.3 w 8.8d) x 0.35 dla zwyciezcy i 1.0 dla przegranego (zwyciezca zbiera pole - regula AmmoRecovery). AiGear dokupi (BuyOrder ma
  Arrows/Bolts/Thrown). **Najpierw sam log** "Amunicja AI: zeszloby N kolczanow" (A05 ryzyko: popyt AI na strzaly wzrosnie; dzis zapas strzelarzy jest).
- **Koszt:** S. **NK:** nie.

### 3.2 Odzysk w misji liczony z wyniku gracza dla obu stron
- **Dowod:** [K AmmoRecovery.cs:40-41] `won = PlayerWon(); if (won.HasValue && !won.Value) recover = 0`; `PlayerWon` = `Mission.MissionResult.PlayerVictory`
  [K :51-61]. DTE wola `IsAmmoAndEmpty` w `ProcessAgentEquipment` dla agentow WSZYSTKICH partii bitwy gracza [DTE Global.cs:262-279; DynamicTroopMissionLogic.cs:230
  (uciekli/usunieci), :290 (polegli), :587 (zwrot na koniec)]. Skutek: wygrana gracza - pokonany wrog odzyskuje 65%; kleska gracza - zwycieski wrog traci wszystko;
  agenci zdjeci w trakcie (`MissionResult == null`) - 65% po obu stronach (poprawka weryfikatora A05).
- **Co zmienic:** prefiks na DTE `Global.ProcessAgentEquipment(Agent, Action<EquipmentIndex,ItemObject>, Func<...>)` ustawia [ThreadStatic] biezacego agenta
  (finalizer czysci); w postfiksie `IsAmmoAndEmpty` strona agenta (`agent.Team.Side`) porownana ze strona zwycieska (z `MissionResult` i `PlayerTeam.Side`);
  w trakcie misji (wynik nieznany) - jak dzis. Licznik w linii "AmmoRecovery: po bitwie" wedlug stron.
- **Koszt:** S. **NK:** nie.

### 3.3 Oszczepy jak strzaly
- **Dowod:** dwie definicje tego samego zjawiska: `HistoricalPrices.IsAmmo` = Arrows/Bolts/**Thrown** [K HistoricalPrices.cs:292-295] - praca x
  `HistAmmoLaborMultiplier` 8 [K :303, Settings.cs:637]; `ArmouryBehavior.IsAmmo` = Arrows/Bolts, **bez Thrown** [K ArmouryBehavior.cs:2266-2270] - wiec oszczep
  sie obija jak bron, a nie zuzywa jak pocisk; `AmmoRecovery` znowu liczy Thrown jako pocisk [K AmmoRecovery.cs:35]. Cena: wiazka oszczepow srednio 107 zl
  (65 sztuk Thrown, np. western_javelin_3_t4 122, generic_javelin_1_t3 120), strzaly 20 zl, a bron drzewcowa tieru 4-5 33-78 zl [ID].
- **Co zmienic:** jeden predykat "pocisk" (Arrows, Bolts, Thrown) dla zuzycia i odzysku (AiWear 3.1, NoWear, AmmoRecovery); mnoznik pracy x8 tylko dla
  Arrows/Bolts (snop 24 strzal), Thrown zostaje przy `DaysThrown` 0.5-3 z ArmsPricing (A05 W10a). Ceny przeliczy `HistoricalPrices.Apply` przy starcie sesji.
- **Kolizja:** 8.5 (cennik) - ta sama metoda `HistDays`; robic w jednej paczce z 8.5 albo 8.4 przed 8.5 (nie obie naraz). Wartosc lupu, zakupy AI i
  `TownFletchers` (linia ranged_weapons) zmienia sie 4-6x dla broni miotanej.
- **Koszt:** S. **NK:** nie.

---

## 4. 8.6 Lup: wraki 100% dla Ciebie; podejrzenie dubla lupu i koni

### 4.1 Wraki i zdobyta zbrojownia bez trzeciej
- **Dowod:** `QueueWreck` [K BattlefieldLaw.cs:633-646] dodaje rozbita czesc do worka, gdy zabojca LUB ofiara jest z partii gracza (limit 400), bez dzialki;
  `AppendWrecks` wsypuje cale do ekranu gracza. `AfterCollectLoot` [K :522-542] przenosi cale `gainedLoots` (DTE `ItemRosterForPlayerLootSharePatch`: zbrojownie
  pokonanych x udzial bitewny x DropRate) do kolejki gracza; trzecia kapitana (33%) obejmuje tylko sprzet z cial powalonych przez zolnierzy [K :467-514].
  A05 wojna/W3 (potwierdzone 05.10) - ta sciezka bez zmian. W autotescie brak bitew gracza (linia E6/K1 "misja nie wpieta" [L36063]) - dowod tylko z kodu.
- **Co zmienic:** wraki wedlug zabojcy: bohater 100%, zolnierz `PlayerLootSharePercent` (reszta do `ArmyArmory` ze stanem Mangled), wlasny polegly - do
  `ArmyArmory` (to sprzet Twoich ludzi). Zbrojownia i tabor pokonanych (gainedLoots) - **JEFF:** A05 pytanie "czy trzecia obejmuje tez zbrojownie i tabor
  pokonanych" nie ma odpowiedzi w STAN-PRAC; rada: tak, ta sama trzecia (zolnierz oddaje kapitanowi 1/3 kazdej zdobyczy - Hay 1954), unikaty dalej UniqueSpoils.
- **Koszt:** S. **NK:** nie.

### 4.2 Dubel: kopia zbrojowni ocalalego pokonanego
- **Dowod:** DTE `GetAllLootItems` [DTE EveryoneCampaignBehavior.cs:684-708] bierze slownik `SanitizePartyArmory` [:747-781], ktory zwraca TEN SAM slownik
  pokonanego bez czyszczenia, i rozdaje go zwyciezcom [:617-682]. Dla rozbitej partii to przeniesienie (gra niszczy ja po MapEventEnded - A05 "co dziala").
  Kopia zostaje u pokonanego, ktory przezyl bitwe - glownie zaloga po oblezeniu/wypadzie (a04 L14). Konie: ZROBIONE w E6/K1 - "lup DTE: dopisane zwyciezcom 18,
  kopie zdjete u pokonanych 18" [L36063]; HorseFate zdejmuje tylko konie [K HorseFate.cs:34-38].
- **Co zmienic:** w tym samym oknie (prefiks/postfiks DTE `OnMapEventEnded`, migawka zbrojowni zwyciezcow - HorseFate) rozszerzyc "lup = przeniesienie" na
  wszystkie sztuki: to, co DTE dopisalo zwyciezcom, schodzi ze zbrojowni pokonanych, ktorzy dalej istnieja; licznik "kopie zdjete" wedlug rodzaju.
  Najpierw POMIAR w tej samej linii: ile sztuk innych niz konie zostaje u ocalalych (rzad wielkosci - 51 oblezen rocznie, a04).
- **Koszt:** S. **NK:** nie. **Kolizja:** AiWear (`_battleSince` - przybytek po bitwie = lup) i GarrisonArmory (zbrojownia zalogi po zdobyciu przechodzi na nowa
  zaloge - E5/A7 kiesa tak samo).

### 4.3 Dubel w "Send troops" (autorozstrzygniecie gracza)
- **Dowod:** DTE `DistributePlayerSimulationLoot` [DTE EveryoneCampaignBehavior.cs:471-598] wklada sprzet poleglych i rannych pokonanych do `ArmyArmory` (100%,
  bez trzeciej i bez ekranu), a `BattlefieldLaw.AfterGenerateLoot` [K :283] i `CutCasualtyLootPrefix` [K :583] w symulacji zostawiaja loterie Spoils
  (1-2 sztuki na trupa) - ten sam polegly placi dwa razy. GarrisonKit sklad7b [K GarrisonKit.cs:1315-1345] pilnuje tylko, zeby lup byl z tego, co pokonany mial.
- **Co zmienic:** w symulacji gracza jedna droga: lup DTE z poleglych, dzialka gracza (trzecia) na ekran, reszta do `ArmyArmory`; loteria Spoils wycieta jak w bitwie
  z misja (A05 W10 pkt 3). Niski priorytet - Jeff nie uzywa "Send troops" (CHANGELOG, A05).
- **Koszt:** S. **NK:** nie.

---

## 5. 8.7 Zalogi nie zuzywaja sprzetu; monopole korony

### 5.1 Zalogi - ZROBIONE w K1
- **Dowod:** `AiWear.OnMapEventEnded` obejmuje `mp.IsGarrison` [K AiWear.cs:264]; zaloga naprawia u kowali (zamek - najblizsze miasto handlowe) [K AiGear.cs:276-291];
  zakupy, dostawy wozem i autowerbunek notuja stan (`NoteBought`) [K GarrisonArmory.cs:349, 820; GarrisonCarts.cs:229; RecruitKit.cs:291]; wpis CHANGELOG zb58:5592
  "(drobne) Zuzycie zalog" (noc/sklad7). Pozycja SP pkt 3 jest przestarzala (opisywala AiWear.cs:165 sprzed K1). Odziez zalog zuzywa ArmyClothing
  (`ArmyClothingGarrisonClothKg` 2, `LinenKg` 1 [K Settings.cs:1088-1089]).
- **Czego brak:** linia "Zuzycie AI" [L35835] nie rozdziela zalog - z logu nie da sie tego potwierdzic.
- **Co zmienic:** tylko log - "w tym zalogi: zuzyte N, naprawione M". Zuzycie zalog w pokoju (rdza, cwiczenia) - nowa mechanika, NIE dodawac bez slowa Jeffa.
- **Koszt:** S. **NK:** nie.

### 5.2 Monopole korony
- **Dowod:** [K KingdomTreasury.cs:444-452] przy polityce StateMonopolies krol bierze co dobe `min(5% x w.ProfitMade, Capital)` z kazdego warsztatu w miastach rodu
  krola. [G Workshop.cs:36] `ProfitMade = max(Capital - InitialCapital, 0)` - to STAN (narosly kapital), nie zysk dnia; gra sama wyplaca wlascicielowi
  `ProfitMade / RevenueSmoothenFraction` (5) dziennie [G DefaultClanFinanceModel.cs:871], a vanilla StateMonopolies liczy 5% sumy ProfitMade [G :299] (to zdejmujemy
  jako "z niczego" [K KingdomTreasury.cs:464-472]). Nasza wersja zabiera wiec 5% stanu dziennie (ok. 5x za duzo wobec "5% zysku"). W tescie: "monopole 0" we wszystkich
  120 liniach "Korona: ... danina wojenna" [L492 .. L35950] - kod martwy w tym tescie (zaden krol z polityka albo warsztaty bez ProfitMade - nie wiem ktore).
- **Co zmienic:** najpierw POMIAR w tej samej linii (krolow z StateMonopolies, warsztatow z ProfitMade > 0, suma ProfitMade) - zasada 4 CLAUDE.md: poprawka zrobi
  z martwego kodu zywy. Potem: 5% dziennej wyplaty wlascicielowi (`ProfitMade / RevenueSmoothenFraction x 0.05`), z kapitalu warsztatu jak dzis.
- **Koszt:** S. **NK:** nie.

---

## 6. 8.8 Zakupy, naprawy i zuzycie AI; Twoj sprzet na arenie

### 6.1 Jakosc ginie
- **Dowod:** AI placi cene sztuki z jej stanem (`GetPrice(el.EquipmentElement, ...)` [K AiGear.cs:608]), a do zbrojowni DTE idzie sam przedmiot
  (`AddToArmory(mp, el.Item, n)` [K AiGear.cs:800]); `AiWear.NoteBought` zapisuje tylko stany `PriceMultiplier < 1` [K AiWear.cs:150-156]. Sztuka "Fine"/"Masterwork"
  jest oplacona z doplata i zamienia sie w zwykla; przy sprzedazy nadwyzek wraca na polke jako zwykla - wartosc znika. Wzorzec ten sam w zakupach zalog i notabli.
  Skali log nie podaje.
- **Co zmienic:** AI nie bierze kandydatow z `PriceMultiplier > 1` (dodatnie stany zostaja dla gracza i bohaterow) + licznik "pominiete dodatnie". Ksiega dodatnich
  stanow w AiWear - wieksza zmiana, nie teraz.
- **Koszt:** S. **NK:** nie.

### 6.2 Brak sufitu ceny
- **Dowod:** w `BuyLoop` jedyny warunek ceny to `price > budget - spent` [K AiGear.cs:610] (tak samo przy pierwszym wyborze [K :146]); brak porownania z wartoscia
  sztuki i filtra `IsCivilian` (A05 werbunek/W7: zaloga 290 zl za fartuch wart 12 - stary log). Dzis [L36029] i suma 120 dob linii "ZakupyAI wedlug kupujacego":
  zalogi miast placa za zbroje korpusu srednio 211 zl/szt. (9 412 szt.), zamowienia zamkow 223, notable 219, lordowie 109 (33 712 szt.); helmy: zalogi 106, lordowie 66.
  Roznica moze byc tez z tierow (zalogi kupuja wyzej) - to nie jest jeszcze dowod.
- **Co zmienic:** POMIAR: w linii 174b mediana i p90 stosunku cena / wartosc sztuki (ze stanem) wedlug kupujacego. Potem sufit `AiGearMaxPriceFactor` (np. 2.0 x wartosc)
  w obu miejscach i w `ShelfBuy.Take` (cena kolejnych sztuk), brak idzie jako zamowienie; pomijac `IsCivilian`.
- **Koszt:** S. **NK:** nie.

### 6.3 Naprawy staja
- **Dowod:** roboty sortowane od najgorszego stanu [K AiWear.cs:503], a petla konczy sie na pierwszej sztuce, na ktora sakiewki nie stac (`break` [K :511]; sciezka z
  materialem, dzis czynna: `if (o.Poor > 0) break;` [K :544]) - tansze czekaja. Zaleglosc obitych sztuk AI: doba 1 - 90 [L336], 30 - 7 961 [L8898], 60 - 26 447 [L17818],
  90 - 52 431 [L26804], 120 - 71 432 w 651 partiach [L35835]; dlugi bieg doba 728 - 122 054 w 674 [L728]. Naprawy 850-1 100 szt./dobe wobec doplywu lup 1 600-2 600 +
  bitwy 200-650. Czesc zaleglosci to lup, ktorego ludzie nie nosza (E6/N1 `AiMendsWornOnly` celowo go pomija), wiec nie cala liczba to "stoi".
  Dodatkowo (A05, weryfikator): zakup placi z sakiewki cala cene [K AiGear.cs:789-791 `MenPurse.Take(mp, cost)`], choc budzet odlicza rezerwe na naprawy - rezerwa
  znika tego samego dnia.
- **Co zmienic:** `continue` zamiast `break` (sztuka bez pieniedzy czeka, nastepna probuje), kolejnosc od najtanszej naprawy; zaplata zakupu z sakiewki najwyzej do
  "sakiewka - zalegle naprawy", reszta z kiesy lorda (zloto sie nie tworzy - zmienia sie platnik). Licznik: partie stojace na sakiewce / materiale / godzinach.
- **Koszt:** S. **NK:** nie.

### 6.4 Zuzycie AI: stale 4% i "zabiera nowe sztuki"
- **Dowod:** `share = TroopWearPercent / 100` stale (4, json Jeffa 4.0) [K AiWear.cs:255] - tyle samo za dwoch maruderow co za bitwe; gracz w autobitwie ma natezenie
  0.1-3 x od rannych [K ArmouryBehavior.cs:1950] - dwie reguly na jedno zjawisko. Obijane sa tylko sztuki sprawne: `sound = ile - obite`, `hits = min(sound, ...)`
  [K :280-281] - kazda bitwa zamienia sprawne (takze swiezo kupione, lezace w zapasie) w obite, a obite nigdy nie schodza nizej. "W uzyciu" liczone po koszykach
  `NeedBuckets` w kolejnosci zbrojowni, a naprawy i nadwyzki po `FitBest` - dwie definicje (poza zakresem e6/p3 CHANGELOG:72, zb58:562 pkt 2).
- **Co zmienic:** share x natezenie tej partii (ten sam wzor co gracz: (DiedInBattle + WoundedInBattle) / zdrowi na starcie / `TroopWearBaseCasualtyShare`, 0.1-3);
  obijane sztuki noszone przez ludzi (jedna definicja - dopasowanie N1 `UsedByMen`), obita moze zejsc o stopien (do Mangled). 3.1 (amunicja AI) korzysta z tego natezenia.
- **Koszt:** S. **NK:** nie.

### 6.5 Twoj sprzet na arenie
- **Dowod:** `FieldCraft` jest w kazdej misji kampanii [K SubModuleMain.cs:326-327]; galaz zuzycia bohatera w `OnAgentHit` (`affectedAgent.IsMainAgent && raw > 0`
  [K FieldCraft.cs:333], do :380) nie sprawdza `Mission.CombatType` (smierc konia sprawdza - [K :194]). Ksiega `WearLedger` jest statyczna [K :224], zerowana tylko w
  `TakeLedger` po WYGRANEJ bitwie na mapie [K ArmouryBehavior.cs:1878]; przegrana wraca wczesniej [K :1827-1834], a `OnMapEventStarted` jej nie czysci [K :1799-1809].
  Ciosy z areny i turnieju (w pozyczonym sprzecie) oraz z przegranych bitew obijaja Twoj rynsztunek po najblizszym zwyciestwie (A05 weryfikator; kod bez zmian).
- **Co zmienic:** w `OnAgentHit` i `OnAgentShootMissile` pomijac ksiege bohatera i `TroopWearLedger`, gdy `CombatType != Combat`; zerowac ksiege w `OnMapEventStarted`
  gracza. Kleska - zgodnie z decyzja z A05 W6 (nie zmieniac przy okazji: dzis kleska = bez zuzycia).
- **Koszt:** S. **NK:** nie.

---

## 7. 8.10 Start kampanii (NK)

### 7.1 Budzet platnerzy
- **Dowod:** `ColdStart.Markets` losuje tier (35% tier 1) i szuka sztuki tylko w dol (`tt--` [K ColdStart.cs:209]); brak sztuki = `break` [K :211] konczy petle cechu
  i reszta jego budzetu przepada. Cechy bez sztuk tieru 1 (platnerz, tarczownik - A05 produkcja/W3, symulacja 05.10: platnerze 170 szt. zamiast 1 066) traca
  wiekszosc budzetu przy pierwszym takim losie. [L232] "97 miast dostalo 50105 szt. (60 dni pracy)" - bez podzialu na cechy, wiec skali dzis nie widac.
- **Co zmienic:** po tierach w dol szukac w gore (t+1..5), dopiero gdy cech nie ma zadnej sztuki - koniec; licznik budzetu cechu: dany / zuzyty / przepadly.
- **Koszt:** S. **NK:** TAK (ColdStart tylko przy kampanii <= 3 dni [K ColdStart.cs:38-40]).

### 7.2 Kon bez uprzezy
- **Interpretacja:** jedyne miejsce w zrodlach to AUDYT-PELNY-EKONOMIA.md N4 ("StartKit - zamiana konia bez sprawdzenia rodziny uprzezy", stare S7). Do potwierdzenia
  przez Jeffa, jesli mial na mysli co innego.
- **Dowod:** `StartKit.Substitute` [K StartKit.cs:57-70] zamienia konia, ktorego bohater nie moze uzyc, na najlepszego tanszego tego samego `ItemType` (pomija tylko
  juczne), bez porownania rodziny wierzchowca z uprzeza w slocie 11; uprzaz tez moze zostac zamieniona osobno. Uprzaz innej rodziny zdejmuje `MountMeshGuard` przy
  budowie siatki ([L142]) - kon jedzie bez uprzezy, uprzaz bezuzyteczna. W tym starcie zamiany konia nie bylo ([L310]: 4 zamiany broni i zbroi) - dowod z kodu.
- **Co zmienic:** zamiana konia tylko na wierzchowca tej samej rodziny co obecna uprzaz (albo zamiana pary kon + uprzaz razem); log "kon/uprzaz".
- **Koszt:** S. **NK:** TAK (raz po kreatorze postaci).

### 7.3 Komplety rekrutow z niczego (start)
- **ZROBIONE dla AI w trakcie gry:** [L35819] "werbunek AI: z zapisu notabla 196, ... notabl bez zapisu 0 ... duplikat ROT pominiety 642 (bez drugiego kompletu)";
  echo ROT (A05 werbunek/W1) zamkniete w `RecruitSources` [K RecruitSources.cs:14, 140]. "Tier 1 z wlasnym dobytkiem" (379 dziennie) to rzeczy z domu rekruta (PLAN 1.4/1.5).
- **Otwarte (A05 werbunek/W3):** ColdStart wyposaza zbrojownie raz, przy starcie sesji: [L231] "261 partii (w tym garnizonow 227)" - czyli 34 partie lordow. Pozostale
  partie lordow powstaja w pierwszej dobie z ludzmi z szablonu, a `LevyGold.PartyCreatedPrefix` [K LevyGold.cs:171-184] od `GameStarted` blokuje im komplet DTE:
  "nowe partie AI bez darmowego kompletu DTE: 541" [L341], 1 291 [L665], 604 [L958]; zakupy AI 2 451 szt. [L337] -> 6 123 [L653] -> 5 037 [L942]. Ludzie z szablonu
  istnieja (z niczego - vanilla), a ich "dorobek stuleci" nie, wiec pierwsze dni to fala zakupow i cofnietych awansow.
- **Co zmienic:** nowa partia lorda AI w pierwszych 3 dniach kampanii dostaje dorobek tak jak 34 pierwsze - jest gotowa metoda `ColdStart.FillToTemplate(mp)`
  (uzywana przez GarrisonArmory [K GarrisonArmory.cs:1041]); wywolanie w `PartyCreatedPrefix` (albo w sluchaczu `MobilePartyCreated` po DTE), licznik w linii ColdStart.
  Kontrola: "ColdStart: zbrojownie" ma objac ok. 280-300 partii lordow.
- **Rekruci gracza t2+ (PLAN 1.12, decyzja Jeffa "jak u AI", STAN-PRAC:354, 448):** NIEZROBIONE - DTE `RecruitmentPatch.Postfix` -> `AddStartingEquipmentToPlayerArmory`
  [DTE RecruitmentPatch.cs:36-70] (i karczma/menu najemnikow) dalej daje pelny komplet; w zb58 brak latki. To pozycja etapu 1 - jesli tam nie wejdzie, dolaczyc
  do 8.10 (to samo zjawisko, ta sama zasada).
- **Koszt:** S. **NK:** TAK (dorobek tylko w pierwszych dniach nowej kampanii).

---

## 8. Proponowane paczki etapu 8 (z tego rozpoznania) i kolejnosc

Jedna zmiana naraz w sensie CLAUDE.md 8.2, ale testy zbiorcze (pamiec "testy-zbiorcze"): kod kilku paczek, potem jeden test 120 dob.

1. **A1 - pomiary (sam log):** 8.1a ksiega umiejetnosci; 8.4a "zeszloby kolczanow"; 8.6b kopie u ocalalych (wszystkie rodzaje); 8.7a "w tym zalogi"; 8.7b licznik
   monopoli; 8.8b cena/wartosc zakupow AI; 8.8a dodatnie stany kupione; 8.8c powody stania napraw. Zero zmian w grze - pomiar "przed".
2. **A2 - XP (8.1b-d, 8.2c):** Z7, Z8, Z10, Z17 (paser, samonaprawa). Po A1.
3. **A3 - zuzycie i naprawy AI (8.8c, 8.8d, 8.4a, 8.4b, 8.8e):** natezenie, jedna definicja "w uzyciu", continue w naprawach, amunicja AI, odzysk wedlug strony,
   arena. Wspolny plik AiWear - jedna paczka, zeby nie zderzyc trzech zmian w tej samej petli.
4. **A4 - zakupy AI (8.8a, 8.8b):** po liczbach z A1.
5. **A5 - lup (8.6a-c, 8.2a-b):** wraki wedlug zabojcy, przeniesienie zamiast kopii, Send troops, dary Spoils do zalogi, resztki na polke. 8.6a zbrojownia pokonanych - po JEFF.
6. **A6 - ceny i monopole (8.4c, 8.7b):** razem z 8.5 (ta sama `HistDays`), monopole po pomiarze.
7. **A7 - start kampanii (8.10a-c):** NK - przed nowa kampania Jeffa; jeden test nowej kampanii 40 dob.

**Decyzje dla Jeffa (JEFF):** (1) trzecia z wrakow, zbrojowni i taboru pokonanych (8.6a) - rada: tak; (2) milicja +0.4 za oddana sztuke w darze Spoils (8.2a) - rada:
osobno, nie w tej paczce; (3) "kon bez uprzezy" - czy chodzi o StartKit (7.2).

## 9. Czego nie sprawdzilem

- RBL (rbl.cs) i GrandTourney (TourneyBehavior.cs:600) z Z17 - liczby z a13, bez wlasnej dekompilacji.
- Czy Spoils ma zdarzenie wygasniecia pozostalosci pola (8.2b) - nie szukalem w `RemnantManager`.
- Dlaczego monopole = 0 (brak polityki czy ProfitMade = 0) - wymaga linii z A1.
- Skala kopii u ocalalych pokonanych (8.6b) i realny udzial zalog w zuzyciu (8.7a) - brak liczb w logu.
- `MissionResult` z TaleWorlds.MountAndBlade nie jest w `dec32\cs_full` - sposob odczytu strony zwycieskiej w 8.4b do sprawdzenia przy paczce.
