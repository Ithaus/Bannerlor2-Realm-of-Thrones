## 2026-10-19/20 (n20-kuznia: surowka tylko do rafinacji - kucie i przetop; przetop wedlug materialu - bron, pancerz, tarcza; caly lancuch kuzni przeliczony)

**Mod:** Armoury (ForgeView, CrashScribe, RC, GT bez zmian) | **Galaz:** `w-toku/n20-kuznia` (od `934cc039` = WGRANIE 88) | **Tor:** M (bez gry - gra zajeta testem; build Armoury kod 0, 0 bledow; gen_mcm 2012 -> 2018 ustawien) | **Klucze (nowe, domyslnie TAK, zadnego nie ma w Armoury.json Jeffa):** `SmeltWeaponByParts` ("Smelt Weapon By Parts", grupa "Time at the anvil", zaraz pod Smelt No Wood), `SmeltArmourOnce` ("Smelt Armour Once", tamze), `SmithNoCrudeInput` ("Smith No Crude Input", tamze - po recenzji), `SmeltCapFromForgeBill` ("Smelt Cap From Forge Bill", grupa "Smithing materials", pod Smelt Cap To Craft Cost), `BkShieldBillByRecipe` ("Bk Shield Bill By Recipe", tamze), `ShieldNoVelvet` ("Shield No Velvet", tamze - po recenzji) | **Pliki:** `SmeltTruth.cs` (nowy), `SmeltTab.cs`, `FletchForge.cs`, `Recipes.cs`, `ArmouryBehavior.cs` (linia startu sesji), `Settings.cs`, `McmSettings.cs` (gen_mcm) | **Status:** DO SPRAWDZENIA

**Dla Jeffa (prosto):**
- **Surowka (crude iron) jest teraz potrzebna TYLKO do rafinacji** (5 surowki + wegiel -> 4 zelaza kutego). Dotad 25 czesci broni z gry, RBM i ROT (jelce, rekojesci, glowice toporow, groty - m.in. jelec Starkow `stark_guard_1`, ktory masz od poczatku) zjadalo przy kuciu surowke: 32 bronie, np. miecz Starkow, miecze t2, chlopskie topory i widly. Teraz te czesci kosztuja tyle samo sztab **zelaza kutego**, a przetop takiej broni oddaje zelazo kute, nigdy surowke.
- Przetop broni oddaje metal z TEGO, z czego bron jest zrobiona (gatunki czesci i ich proporcja), mniej wiecej polowe jej metalu - tak samo jak przetop pancerza. Nie ma juz sztaby surowki doklejanej do kazdej broni i stal nie wychodzi z broni, w ktorej stali prawie nie ma.
- Drewniany miecz, widly czy drewniana palka nie daja juz surowki (BK dawal 1 sztabe zelaza z drewna).
- Pancerz i tarcza w zakladce Smelt liczyly sie PODWOJNIE (blad w laczeniu naszej latki z BK). Teraz raz - a ciezki pancerz oddaje uczciwie do polowy metalu, ktory kosztuje jego wykucie (np. lekka zbroja Lannisterow: bylo 10 sztab, czyli ok. 1/6 kwitu kucia ok. 60; jest ok. 30).
- Tarcza wykuta na ekranie zbroi BK kosztowala 2 drewna i zero zelaza, a po przetopie dawala kilka sztab - metal z niczego. Teraz kosztuje to samo co w Twojej kuzni (drewno, zelazo na okucia, wegiel, skora).
- Tarcze t5-t6 sa obite skora zamiast aksamitu (sztuka aksamitu kosztuje ok. 4000 d, tarcza 50-100 d) - w Twojej kuzni, na ekranie BK i przy naprawie.
- Rafinacja (ruda -> surowka -> zelazo kute -> zelazo -> stal -> stal szlachetna -> stal zamkowa) jest dobrze: kazdy stopien tylko z poprzedniego, 5 sztab -> 4 wyzsze + wegiel; nigdzie nie robi sie stali z surowki jednym krokiem.

**Problem (Jeff 19.10, doslownie):** "Przejrzyj smithing, czy na pewno wszystko jest dobrze, policozne bo crude nie powinno byc potrzbne do wytapiania i robie a zadnej broni z crue robi sie stal." Dowody z gry Jeffa (log `D:\Backup-Bannerlord\zawieszenie-2026-10-19-1609\logi\Armoury-2026-10-19_14-41-48.log`, sesja 19.10 14:41-16:09; trail.txt: GauntletCraftingScreen 15:59): 14:47-14:48 ekran kucia broni otwarty (linie "Drewno w kuzni (kg) - koszt: OneHandedSword" - log nie pokazuje gatunkow metalu kosztu); 15:22:07-15:22:19 przetop `bracketed_heater_shield`, `southern_broad_2hsword_t4` (Broad Kaskara), 2 x `arakh`, `simple_pike_t2`, 3 x `nord_throwing_axe_2_t4`; 15:56:14-15:57:44 ok. 20 przetopow buzdyganow `vlandia_mace_1_t2` / `battania_mace_2_t2`; 13:48-13:49 (log `Armoury-2026-10-19_13-00-24.log`) i 14:47-14:49 seria przetopow pancerzy z liniami "SmeltTab: przetop X przyciety do kosztu wykucia: 30 -> 5 szt. metalu (wykucie 11)". Log potwierdza tylko, CO przetopiono (linie f27 i wegla, bez wyniku metalu) - wyniki BK ponizej pochodza z kodu (dekompilacja BannerKings.dll i TaleWorlds.CampaignSystem.dll 1.4.8) i z symulacji na danych czesci (crafting_pieces Native + RBM + RBM_WS + ROT, kolejnosc modulow z LauncherData.xml Jeffa; 432 bronie z projektem; skrypty `n20-kuznia-tmp/py/sim.py` i `sim2.py`):

| Bron (z logu Jeffa) | Czesci (metal) | Przetop dotad (BK, symulacja) |
|---|---|---|
| Spiked Mace / Highland Spiked Club (t2) | 2 zelaza kutego | 1 surowka + 1 zelazo kute |
| Simple Pike (t2) | 1 zelazo kute | 1 surowka |
| Arakh | 1 zelazo kute + 3 stal szlachetna | 1 surowka + 2 stal szlachetna |
| Broad Kaskara (t4) | 1 zelazo, 4 stal, 5 stal szl. | 1 surowka + 2 stal + 1 stal szl. |

Na 432 bronie: surowka z niczego w **352**, wiecej metalu niz w czesciach w **78** (np. lanca `empire_lance_1_t3_blunt`: 2 zelaza -> 1 surowka + 1 zelazo kute + 1 zelazo; drzewce `khuzait_polearm_1_t4_blunt`: 1 stal -> 1 surowka + 1 zelazo), surowka z broni BEZ metalu w **6** (`wooden_sword_t1/_t2`, `wooden_2hsword_t1`, `peasant_maul_t1`, `peasant_pitchfork_2_t1`), najwyzszy gatunek z mniejszosci czesci w **23** (np. `reaper_falx`: 2 zel. kute, 3 stal, 3 stal szl. -> 1 surowka, 2 stal, 1 stal szl.). Wsadu "crude" przy SAMYM przetopie w kodzie nie ma (SmeltingItemVM: wsad = ujemne pozycje wyniku, a tam ujemny jest tylko wegiel). Surowka jest natomiast **wsadem kucia**: 25 czesci ma `Iron1` w MaterialsUsed (Native: 11 jelcow i 5 rekojesci, `sturgian_pommel_5`; RBM: glowice `axe_craft_15_head` (4 surowki), `axe_craft_37_head`, `spear_blade_31/32`, `sickle_blade_1`; RBM_WS: `naval_axe_head_02`; ROT: `stark_guard_1` (is_default="true" - kazdy gracz ma go od startu; `ROT_crafting_pieces.xml:350-355`), `needle_guard`), uzywa ich 32 bronie z projektem.

**Przyczyna:**
1. `BKSmithingModel.GetSmeltingOutputForItem` (BK nadpisuje gre): bierze wynik gry (materialy czesci, najwyzszy gatunek traci stopien, -1 wegiel), przycina metal do `GetMetalMax(klasa)` = 1-3 sztaby odejmujac po jednej sztabie od NAJNIZSZEGO gatunku w petli (j = 2..6), a na koniec `if (out[1] == 0 && metalMax > 0) out[1]++` - sztaba surowki z niczego. Stad surowka przy kazdej broni i gatunki "z gory" (tansze czesci odpadaja pierwsze).
2. `SmeltTab.OutputPostfix` jest wpiety we WSZYSTKIE modele z wlasna metoda (log: "wyjscie w 2 modelach" = DefaultSmithingModel i BKSmithingModel), a BK wola metode bazy - postfiks bazy dokladal odzysk pancerza/tarczy, postfiks BK dokladal go drugi raz: kazdy przetop pancerza x2; tarcza (BK widzi ja jako bron z klasa LargeShield/SmallShield, sufit 1-2) dostawala dodatkowo surowke z niczego.
3. Sufit `SmeltCapToCraftCost` (audyt 04.10) liczyl "koszt wykucia" z punktow ochrony (`Recipes.ArmourUnits`), bo wtedy tak liczyl CRAFT BK. Tego samego dnia (audyt K1, `FletchForge.BkArmorBillPostfix`, log "kwit pancerzy BK podmieniony na Prawo Wagi") kwit CRAFT BK przeszedl na wage (`Recipes.For`) - sufit zostal przy punktach ochrony i cial przetop do 5 sztab (ok. 1/12 kwitu), a liczony dwa razy (pkt 2) dawal 10 - ok. 1/6 metalu kucia (lannister_light_armor: odzysk 30 -> 5, kwit kucia ok. 60).
4. Tarcza w CRAFT BK: `BKSmithingModel.GetCraftingInputForArmor` - galaz broni: `array[7] = 2` (drewno), zelazo `Iron4` tylko gdy `PhysicsMaterial == "shield_metal"`; nasze latki (FletchForge, TrueArmourCost) pomijaly przedmioty bez ArmorComponent. Drewniana tarcza (np. bracketed_heater_shield, `wood_shield`, 4.7 kg) kosztowala 2 drewna, a przetop (Recipes.For z wagi x ClassCostShield 0.55) oddawal sztaby zelaza - petla metalu z niczego.
5. (po recenzji) `DefaultSmithingModel.GetSmithingCostsForWeaponDesign` odejmuje wszystkie MaterialsUsed czesci (`array[(int)item.Item1] -= item.Item2`), BK tej metody nie nadpisuje, a nasze latki (SmithWood, SmithCoal) ruszaja tylko drewno (7) i wegiel (8). Ten sam wynik czyta licznik na ekranie (`CraftingVM.UpdateCurrentMaterialCosts`), "czy stac" (`HaveMaterialsNeeded`) i zdjecie z sakw (`CraftingCampaignBehavior.SpendMaterials`) - wiec kucie broni z 25 czesci wymagalo i zjadalo surowke, wbrew zasadzie Jeffa 01.09 zapisanej w `Recipes.IronForTier` ("surowka do wyrobu nie wchodzi", t1 = zelazo kute). BK, BKROTPatch i ROT.dll tej metody nie wolaja (grep dekompilacji).
6. (po recenzji) `Recipes.For` dla tarczy t5-t6 dodaje aksamit (blok miekkich surowcow, `weight x 0.3`, do `tier x SoftMaterialPerTier`); tablica BK ma tylko 11 miejsc (9 surowcow, skora, len), wiec ekran BK aksamitu nie pokazywal ani nie zdejmowal, nasza kuznia zdejmowala. Aksamit kosztuje 4000 d za sztuke (log 14:43:44 "velvet 575->4000"), tarcza t5-t6 ok. 50-100 d (linie "Dozbrajanie", np. pavise_shield 83-101) - kwit 40-120 razy drozszy niz wyrob.

**Zmiana:**
1. `SmeltWeaponByParts` (`SmeltTruth.ApplyWeapon`, z `SmeltTab.OutputPostfix` tylko w wywolaniu zewnetrznym - po BK): metal wyniku (indeksy 1-6) liczony od nowa z czesci broni: n = floor(metal czesci x udzial), udzial = `SmeltingReturnShare` + Smithing x `SmeltingSkillBonus`, najwyzej 0.5 (ta sama regula co pancerz, Jeff 30.08 "najwyzej polowa"), co najmniej 1 sztaba, gdy bron ma metal, nie wiecej niz sufit BK klasy (`GetMetalMax` czynnego modelu przez refleksje; bez BK - bez sufitu). Rozdzial na gatunki proporcjonalnie (najwieksze reszty; remis -> nizszy gatunek). Bez obnizania gatunku i bez surowki z niczego. Drewno (`SmeltNoWood`) i wegiel (`SmithCoal`) bez zmian.
2. `SmeltArmourOnce`: `SmeltTruth.IsOuter(__originalMethod)` - zewnetrzne = metoda najbardziej pochodnego modelu z wpietych (`SmeltTab.PatchedTypes`); wewnetrzne (baza wolana przez BK) nic nie dodaje, zewnetrzne zeruje metal 1-6 (surowka BK przy tarczy) i PRZYPISUJE odzysk z receptury.
3. `SmeltCapFromForgeBill`: sufit = metal receptury `Recipes.For` (= kwit CRAFT BK i naszej kuzni) x min(udzial, 0.5); tarcza z sufitem tylko przy `BkShieldBillByRecipe` (inaczej jak dotad - bez sufitu).
4. `BkShieldBillByRecipe`: `FletchForge.BkArmorBillPostfix` liczy tez tarcze z `Recipes.For` (metal okuc z wagi, drewno, wegiel, skora/len). Po recenzji: reszta kwitu spoza tablicy BK (kg drewna z otwartego ladunku - `SmithWood.Use`, aksamit przy `ShieldNoVelvet` = NIE) w nowym `BkShieldSpendPostfix` na `CraftingMixin.SpendMaterials` - wpiety razem z kwitem tarczy (sam `CraftingEnabled`), takze bez `AllowRangedCrafting` (dotad drewno w kg schodzilo tylko w prefiksie za ta bramka). `BkHasMaterialsPostfix` nie przepuszcza tarczy bez aksamitu (gdy go wymaga), `BkMainActionPostfix` mowi w podpowiedzi przycisku, ile go brakuje (ekran BK pokazuje tylko skore i len).
5. (po recenzji) `SmithNoCrudeInput`: `SmeltTruth.CrudeCostPostfix` na `GetSmithingCostsForWeaponDesign` metody klasy deklarujacej (`PatchTarget.OfActive`, wpis "kuznia: surowka do kucia" w linii "Latki czynnego modelu (HT)"; pulapka 15.10) - koszt surowki (indeks 1) przechodzi 1:1 na zelazo kute (indeks 2). Licznik ekranu, "czy stac" i zdjecie z sakw widza jedna liczbe. `SmeltTruth.PartsMetal` liczy surowke czesci jako zelazo kute - przetop takiej broni oddaje zelazo kute. Postfiks na `CraftingCampaignBehavior.SpendMaterials` pisze linie po prawdziwym kuciu.
6. (po recenzji) `ShieldNoVelvet`: `Recipes.For` - tarcza t5-t6 dostaje skore zamiast aksamitu (ta sama liczba). Jeden kwit w naszej kuzni, w CRAFT BK i w naprawie wlasnymi rekami (`MendMaterial.SelfParts` czyta `Recipes.For`).
7. Log: linia startu sesji "Kuznia przetop (n20-kuznia): start sesji" (6 kluczy, samosprawdzenie na wszystkich broniach z projektem, pancerz i drewniana tarcza: odzysk z modelu wobec receptury, kwit tarczy BK, obicie tarczy t5+, kucie bez surowki: latki, liczba czesci i broni z surowka, proba kosztu z modelu, nowy licznik "z 1 sztaby oddaja ja cala"); po prawdziwym przetopie broni "Przetop broni (n20-kuznia): <id> | czesci: ... -> wynik: ..." (raz na wzor w sesji); po kuciu broni z surowka w czesciach "Kucie bez surowki (n20-kuznia): ..."; po kuciu tarczy w CRAFT BK "Tarcza w CRAFT BK (n20-kuznia): ..."; linia "Przetop (zakladka Smelt): <id>" dostala " | metal: ...".

**Tabela dla Jeffa - co wkladasz -> co wychodzi (Smithing 143, czyli udzial 50%; bez drewna i wegla):**

Kucie broni z czesciami z surowka (32 bronie; po zmianie te same sztuki, tylko surowka -> zelazo kute):

| Bron | Dotad zjadala | Teraz zjada |
|---|---|---|
| Miecz Starkow (`stark_sword_1`, jelec `stark_guard_1`) | 1 surowka, 2 zel. kute, 3 zelaza | 3 zel. kute, 3 zelaza |
| vlandia_sword_1_t2 | 1 surowka, 2 zel. kute, 3 zelaza | 3 zel. kute, 3 zelaza |
| bastard_sword_t2 | 1 surowka, 3 zel. kute, 3 zelaza | 4 zel. kute, 3 zelaza |
| battania_2hsword_4_t4 | 2 surowki, 1 stal, 3 stal szl. | 2 zel. kute, 1 stal, 3 stal szl. |
| Chlopski topor dwureczny (`peasant_2haxe_1_t1`) | 4 surowki | 4 zel. kute |
| Widly / sierp / kilof chlopski (t1) | 1-2 surowki | 1-2 zel. kute |

Przetop broni:

| Bron | Z czego zrobiona | Dotad (BK) | Teraz |
|---|---|---|---|
| Spiked Mace, Highland Spiked Club (t2) | 2 zelaza kutego | 1 surowka + 1 zelazo kute | 1 zelazo kute |
| Simple Pike (t2) | 1 zelazo kute | 1 surowka | 1 zelazo kute |
| Arakh | 1 zelazo kute + 3 stal szlachetna | 1 surowka + 2 stal szlachetna | 1 zelazo kute + 1 stal szlachetna |
| Broad Kaskara (t4) | 1 zelazo, 4 stal, 5 stal szl. | 1 surowka, 2 stal, 1 stal szl. | 1 stal + 2 stal szl. (sufit BK: 3 sztaby) |
| Reaper Falx | 2 zel. kute, 3 stal, 3 stal szl. | 1 surowka, 2 stal, 1 stal szl. | 1 zel. kute, 1 stal, 1 stal szl. |
| Execution Axe | 3 zel. kute, 1 zelazo, 1 stal | 1 surowka, 2 zel. kute, 1 zelazo | 1 zel. kute + 1 zelazo |
| bastard_sword_t2 | 1 surowka, 3 zel. kute, 3 zelaza | 1 surowka, 2 zel. kute, 1 zelazo | 2 zel. kute + 1 zelazo |
| Chlopski topor dwureczny | 4 surowki (teraz kuty z 4 zel. kutego) | 4 surowki (100%) | 2 zel. kute |
| Drewniany miecz, widly, palka chlopska | samo drewno | 1 surowka | nic (drewno sie spala) |
| Lanca empire_lance_1_t3_blunt | 2 zelaza | 1 surowka, 1 zel. kute, 1 zelazo (3 z 2!) | 1 zelazo |

Razem na 432 broniach: metal czesci 2038 sztab, BK oddawal 1273 (w tym 352 surowki z niczego), teraz 732 (ok. 36% - polowa metalu, przycieta do sufitu BK na duzych broniach), surowki w wyniku 0. 31 broni ma 1 sztabe metalu - oddaja te 1 sztabe (minimum 1, jak przy pancerzu; decyzja 7).

Przetop pancerza i tarczy (zakladka Smelt):

| Sztuka | Kwit kucia (metal) | Dotad | Teraz |
|---|---|---|---|
| lannister_light_armor | ok. 60 sztab | 10 (5 x 2 - sufit z punktow ochrony, liczone dwa razy; ok. 1/6) | ok. 30 (polowa kwitu) |
| ranger_mail | ok. 24 | 4 (2 x 2) | ok. 12 |
| mail_shirt | ok. 14 | 6 (3 x 2) | ok. 7 |
| chainmail_shoulder_armor | ok. 4 | 2 (1 x 2) | 2 |
| tarcza drewniana (bracketed_heater_shield) | CRAFT BK: 2 drewna, 0 zelaza | odzysk z receptury x2 + 1 surowka | CRAFT BK: kwit z receptury (zelazo okuc, drewno, wegiel, skora); odzysk raz, najwyzej polowa |
| tarcza t5-t6 (np. pavise_shield) | nasza kuznia: skora + 1-3 aksamitu (ok. 4000 d sztuka); CRAFT BK: kwit BK | - | skora zamiast aksamitu w obu kuzniach |

Rafinacja (bez zmian, sprawdzona - `MaterialLaw.RefinePostfix`, ceny historyczne TAK u Jeffa, perki jak w grze):

| Wkladasz | Wychodzi |
|---|---|
| 1 ladunek drewna (100 kg) | 4 bryly wegla (6 z perkiem Charcoal Maker; log Jeffa "ladunek drewna 100 kg -> wegli 6") |
| 1 lupa z dymarki + 1 wegiel | 30 sztab surowki (15 kg) |
| 5 surowki + 1 wegiel | 4 zelaza kutego (jedyne miejsce, gdzie surowka jest potrzebna) |
| 5 zelaza kutego + 1 wegiel | 4 zelaza |
| 5 zelaza + 1 wegiel (perk Steel Maker) | 4 stali |
| 5 stali + 1 wegiel (Steel Maker 2) | 4 stali szlachetnej |
| 5 stali szlachetnej + 2 wegla (Steel Maker 3) | 4 stali zamkowej |

Kucie broni: metal = materialy czesci (gra, `GetSmithingCostsForWeaponDesign`), surowka czesci -> zelazo kute (`SmithNoCrudeInput`), drewno w kg (`SmithWood`), wegiel 0.5 kg na bron z otwartej bryly (`SmithCoal`). Kucie pancerza w CRAFT BK i w naszej kuzni: `Recipes.For` z wagi (80% metalu swojego tieru + 20% tier nizej, surowka nigdy - `IronForTier` t1/t2 = zelazo kute). Warsztaty miast (`WorkshopLaw`, `MendMaterial`) licza metal w "kg surowki" jako jednostce rozrachunku (kazdy stopien x1.25) - to ekonomia miast, nie kuznia gracza; bez zmian.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: `GetSmeltingOutputForItem` wolaja tylko zakladka Smelt (SmeltingItemVM, CraftingVM, DoSmelting), `SmeltTab.DoSmeltingPrefix` (pancerz) i samosprawdzenia (SmithCoal.SelfCheck, SmeltTruth.SessionStart) - AI nie przetapia, goracej sciezki brak. Wegiel (`SmithCoal.SmeltPostfix`, Priority.Last, indeks 8) i drewno (`SmeltNoWood`, indeks 7) - inne indeksy, kolejnosc bez znaczenia. Nauka czesci za przetop (f27, `ResearchSmeltPostfix`) liczy od wartosci przedmiotu - bez zmian. Legendy (`DoSmeltingPostfix`) i stal valyrianska (`ValyrianBlades` blokuje przetop) - bez zmian.
- `GetSmithingCostsForWeaponDesign`: wolaja tylko `CraftingVM.UpdateCurrentMaterialCosts` (licznik i "czy stac") i `CraftingCampaignBehavior.SpendMaterials` (zdjecie) - watek glowny, ekran kuzni; BK/BKROTPatch/ROT jej nie wolaja. Trzy nasze postfiksy na tej metodzie (SmithWood - indeks 7, SmithCoal - 8, SmeltTruth - 1 i 2) nie dotykaja tych samych pozycji. Postfiks bez alokacji. Kucie broni z tych 32 wzorow jest troche drozsze (zelazo kute zamiast surowki - 1 do 4 sztab). Wylacznik: `SmithNoCrudeInput` = false. Opis czesci w projektowniku gry dalej moze wymieniac "Crude Iron" (to dane czesci) - licznik na dole ekranu pokazuje juz zelazo kute.
- Mniej metalu z broni niz dotad (36% zamiast 62% metalu czesci, z czego 17 pkt to surowka z niczego) - Jeff moze to odczuc przy przetapianiu lupu. Wylacznik: `SmeltWeaponByParts` = false (sposob BK).
- Wiecej metalu z ciezkich pancerzy (do polowy kwitu kucia zamiast ok. 1/6). Petla kucie -> przetop zostaje na minusie (polowa metalu ginie, wegiel, stamina, czas); sprzedaz lupu po cenach historycznych dalej lepsza niz przetop (cena sztuki z metalu + robocizny). Wylacznik: `SmeltCapFromForgeBill` = false.
- Bron z 1 sztaba metalu (31 wzorow, np. simple_pike_t2, widly) oddaje te 1 sztabe w tym samym gatunku (minimum 1, jak pancerz) - petla kucie/przetop takiej broni kosztuje tylko wegiel, drewno, stamine i czas, a daje XP i punkty badan f27 (log Jeffa 15:22:17 simple_pike_t2 -> 7 pkt). Dotad BK oddawal z niej surowke (gra: stopien nizej). Linia startu liczy te bronie osobno ("z 1 sztaby oddaja ja cala"). Decyzja 7.
- Tarcza w CRAFT BK drozsza (metal okuc + wegiel + skora zamiast 2 drewna). Wylacznik: `BkShieldBillByRecipe` = false. Drewno tarczy schodzi z otwartego ladunku (SmithWood.Use) - jak w naszej kuzni, teraz niezaleznie od `AllowRangedCrafting`; tarcze metalowe (`shield_metal`) maja tez kwit z receptury (dotad BK: Iron4 x waga). Postfiksy na `CraftingMixin.HasMaterials` i `UpdateMainAction` (BK) biegna przy odswiezeniu ekranu, nie co klatke; przy `ShieldNoVelvet` = TAK sprawdzanie aksamitu nic nie robi (tarcza go nie wymaga).
- `ShieldNoVelvet`: tarcza t5-t6 bierze wiecej skory (np. +2-3 skory zamiast 2-3 aksamitu) w naszej kuzni, w CRAFT BK i w naprawie wlasnymi rekami. Ceny tarcz (ArmsPricing) i warsztaty miast liczy osobna receptura - bez zmian. Wylacznik: `ShieldNoVelvet` = false (wtedy aksamit sprawdzany i zdejmowany tez w CRAFT BK).
- `IsOuter`: gdy czynny model nie dziedziczy po zadnym wpietym typie (inny mod z wlasnym modelem w zestawie bez BK) - zachowanie jak dotad (kazde wywolanie zewnetrzne). Harmony wstrzykuje `__originalMethod` (Harmony 2.x). Gdyby oba wywolania wyszly zewnetrzne - zewnetrzne i tak zeruje i przypisuje; gdyby oba wewnetrzne - linia startu pokaze "surowka w wyniku" > 0.
- Zgodnosc zapisu: brak nowych danych zapisu. Klucze w MCM czytane w chwili wywolania (przelaczenie dziala od razu).
- Kod BK uzyty przez refleksje: `BKSmithingModel.GetMetalMax(WeaponClass)` (publiczna), `CraftingMixin.SpendMaterials/HasMaterials/UpdateMainAction` i pola `armorCrafting`, `crafting` (dekompilacja BannerKings.dll); brak = latka nie wchodzi, linia startu pokazuje "BRAK".

**Decyzje (podjete sam, kazda z wylacznikiem):**
1. Ilosc z przetopu broni = ta sama regula co pancerz (najwyzej polowa metalu, rosnie ze Smithing), z sufitem klasy BK - "kuznia 1:1" (Jeff 26.08) i "przetop zwraca najwyzej polowe receptury" (Jeff 30.08). Gatunki - proporcja czesci, remis do nizszego gatunku. Bez obnizania gatunku (gra robila ze stali zelazo, z zelaza kutego surowke - to wlasnie dawalo "crude" w wyniku). Wylacznik `SmeltWeaponByParts`.
2. Odzysk pancerza raz (to blad, nie projekt). Wylacznik `SmeltArmourOnce`.
3. Sufit przetopu pancerza od prawdziwego kwitu kucia. Wylacznik `SmeltCapFromForgeBill` (wtedy sufit z punktow ochrony, jak dotad).
4. Tarcza w CRAFT BK z naszej receptury (jeden kwit na tarcze w obu kuzniach, koniec metalu z niczego). Wylacznik `BkShieldBillByRecipe`.
5. Rafinacji nie ruszalem - lancuch jest spojny. Martwy kod "tygiel w menu miasta" (`SmithMenu.SmeltConsequence` - opcja nie jest rejestrowana) zostawiony bez zmian.
6. (po recenzji) Surowka w czesciach broni = tyle samo sztab zelaza kutego (1:1) - najnizszy metal wyrobu wedlug zasady Jeffa 01.09; ilosc bez zmian, zeby nie zmieniac wagi i statystyk broni. Wylacznik `SmithNoCrudeInput`.
7. (po recenzji) Minimum 1 sztaba z broni, ktora ma 1 sztabe metalu, zostaje - tak samo liczy pancerz (`Recipes.SmeltYield`: kazda czesc najmniej 1), a 0 sztab z noza czy grotu wygladaloby jak blad przetopu. Gatunku nie obnizam (obnizenie zelaza kutego = surowka, czyli to, co Jeff zglosil). Zmiana tej regul wymagalaby slowa Jeffa; linia startu pokazuje, ilu broni dotyczy.
8. (po recenzji) Tarcze t5-t6 bez aksamitu (skora zamiast, ta sama liczba) - aksamit 40-120 razy drozszy niz tarcza, a Jeff 01.09 zdjal aksamit z pancerzy ("to nie suknia"). Wylacznik `ShieldNoVelvet`.

**Recenzja (po pierwszej wersji, 7db4025f / efaa387d) - co z nia zrobilem:**
- WAZNE "surowka dalej jest WSADEM przy kuciu broni z 25 czesci" - PRAWDA (dekompilacja `DefaultSmithingModel.GetSmithingCostsForWeaponDesign`, `CraftingVM.UpdateCurrentMaterialCosts/HaveMaterialsNeeded`, `CraftingCampaignBehavior.SpendMaterials`; `ROT_crafting_pieces.xml:350-355`; symulacja `sim2.py`: 25 czesci, 32 bronie). Naprawione kluczem `SmithNoCrudeInput` (zmiana 5); zdanie "Surowka jest potrzebna tylko na zelazo kute" bylo nieprawda - poprawione i teraz prawdziwe.
- drobne "aksamit tarczy t5+ w CRAFT BK nie schodzi" - PRAWDA (`Recipes.For`: armour konczy sie przed blokiem miekkich surowcow, tarcze nie). Naprawione dwa razy: przy `ShieldNoVelvet` tarcza go nie potrzebuje (decyzja 8), bez klucza CRAFT BK go sprawdza i zdejmuje (zmiana 4).
- drobne "drewno w kg tarczy za darmo przy AllowRangedCrafting = NIE" - PRAWDA (`FletchForge.ApplyAll`: prefiks za bramka). Naprawione: `BkShieldSpendPostfix` wpiety razem z kwitem tarczy.
- drobne "minimum 1 sztaba = 100% przy broni z 1 sztaby" - PRAWDA, zostawione swiadomie (decyzja 7), z osobnym licznikiem w linii startu.
- drobne "niescislosci opisu" - PRAWDA: naglowek `SmeltTruth` mowi teraz, ze wyniki BK sa z symulacji (log potwierdza tylko, co przetopiono); test: przy `SmeltArmourOnce` = NIE pancerz "= DWA RAZY", tarcza "= INNE"; "ok. 1/6" zamiast "ok. 1/10"; pole `_shieldBills` przeniesione przed opis XML metody.

**Test w grze:**
- Autotest 5 dob + bitwa wystarczy (zadnych plikow/flag). W logu Armoury przy starcie sesji linia `Kuznia przetop (n20-kuznia): start sesji` - ma byc: 6 kluczy TAK ("Smelt Weapon By Parts", "Smelt Armour Once", "Smelt Cap From Forge Bill", "Bk Shield Bill By Recipe", "Smith No Crude Input", "Shield No Velvet"), "model BKSmithingModel", "wyjscie wpiete w 2 modelach", "gatunek wyzszy niz w czesciach 0", "surowka bez surowki w czesciach 0", "ponad polowe metalu czesci 0", "bez metalu w czesciach N" (N ok. 6+), "z 1 sztaby oddaja ja cala N" (ok. 31), "surowka w wyniku 0", "pancerz: <id> model X / receptura X (...) = raz", "tarcza: ... = raz", "kwit tarczy w CRAFT BK: <id> metal M (receptura M) ... = z receptury ... latki tarczy zdjecie, czy stac, podpowiedz", "tarcza t5+: <id> t5|t6: aksamit 0, skora N (klucz Shield No Velvet TAK, aksamit ma byc 0)", "kucie bez surowki: latki koszt DefaultSmithingModel, pobranie SpendMaterials, czesci z surowka 25, bronie z surowka w czesciach 32, proba <id>: czesci crude C, wrought W -> koszt modelu crude 0, wrought C+W = zelazo kute zamiast surowki". W linii "Latki czynnego modelu (HT)" wpis "kuznia: surowka do kucia" bez bledu.
- Przy wylaczonych kluczach: `SmeltArmourOnce` = NIE -> pancerz "= DWA RAZY", tarcza zwykle "= INNE" (BK przycina wewnetrzny odzysk tarczy do 1-2 i dokleja surowke); `BkShieldBillByRecipe` = NIE -> "= kwit BK"; `SmithNoCrudeInput` = NIE -> "= surowka (jak w grze)".
- W grze Jeffa (ekran kuzni): kucie broni z jelcem/rekojescia z surowka - licznik na dole pokazuje zelazo kute zamiast surowki, w logu `Kucie bez surowki (n20-kuznia): <nazwa> (<szablon>) | surowka czesci N -> zdjete zelazo kute N`; kucie tarczy w CRAFT BK - `Tarcza w CRAFT BK (n20-kuznia): <id> | kwit z receptury, drewno X kg, aksamit 0`; po przetopie broni `Przetop broni (n20-kuznia): <id> | czesci: ... -> wynik: ...`; po przetopie pancerza `Przetop (zakladka Smelt): <id> | metal: ...` - liczba sztab ma sie zgadzac z panelem i przyrostem w sakwach (dotad x2).
- Brak ERROR `SmeltTruth.`, `SmeltTab.`, `BkShieldSpend`, `BkHasMaterials`, `BkMainAction`, `FletchForge.ApplyAll(tarcza)` w logu.
