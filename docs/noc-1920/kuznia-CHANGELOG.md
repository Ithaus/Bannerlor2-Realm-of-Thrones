## 2026-10-19/20 (n20-kuznia: przetop wedlug materialu - bron, pancerz, tarcza; caly lancuch kuzni przeliczony)

**Mod:** Armoury (ForgeView, CrashScribe, RC, GT bez zmian) | **Galaz:** `w-toku/n20-kuznia` (od `934cc039` = WGRANIE 88) | **Tor:** M (bez gry - gra zajeta testem; build Armoury 0 bledow, gen_mcm 2012 -> 2016 ustawien) | **Klucze (nowe, domyslnie TAK):** `SmeltWeaponByParts` ("Smelt Weapon By Parts", grupa "Time at the anvil", zaraz pod Smelt No Wood), `SmeltArmourOnce` ("Smelt Armour Once", tamze), `SmeltCapFromForgeBill` ("Smelt Cap From Forge Bill", grupa "Smithing materials", pod Smelt Cap To Craft Cost), `BkShieldBillByRecipe` ("Bk Shield Bill By Recipe", tamze) | **Pliki:** `SmeltTruth.cs` (nowy), `SmeltTab.cs`, `FletchForge.cs`, `ArmouryBehavior.cs` (linia startu sesji), `Settings.cs`, `McmSettings.cs` (gen_mcm) | **Status:** DO SPRAWDZENIA

**Dla Jeffa (prosto):**
- Przetop broni oddaje teraz metal z TEGO, z czego bron jest zrobiona (gatunki czesci broni i ich proporcja), mniej wiecej polowe jej metalu - tak samo jak przetop pancerza. Nie ma juz sztaby surowki (crude iron) doklejanej do kazdej broni i stal nie wychodzi z broni, w ktorej stali prawie nie ma.
- Drewniany miecz, widly czy drewniana palka nie daja juz surowki (BK dawal 1 sztabe zelaza z drewna).
- Pancerz i tarcza w zakladce Smelt liczyly sie PODWOJNIE (blad w laczeniu naszej latki z BK). Teraz raz - ale ciezki pancerz oddaje teraz uczciwie do polowy metalu, ktory kosztuje jego wykucie (np. lekka zbroja Lannisterow: bylo 10 sztab, jest ok. 30 przy kwicie kucia ok. 60).
- Tarcza wykuta na ekranie zbroi BK kosztowala 2 drewna i zero zelaza, a po przetopie dawala kilka sztab - metal z niczego. Teraz kosztuje to samo co w Twojej kuzni (drewno, zelazo na okucia, wegiel, skora).
- Rafinacja (ruda -> surowka -> zelazo kute -> zelazo -> stal -> stal szlachetna -> stal zamkowa) jest dobrze: kazdy stopien tylko z poprzedniego, 5 sztab -> 4 wyzsze + wegiel; nigdzie nie robi sie stal z surowki jednym krokiem. Surowka jest potrzebna tylko na zelazo kute - nie do przetopu.

**Problem (Jeff 19.10, doslownie):** "Przejrzyj smithing, czy na pewno wszystko jest dobrze, policozne bo crude nie powinno byc potrzbne do wytapiania i robie a zadnej broni z crue robi sie stal." Dowody z gry Jeffa (log `D:\Backup-Bannerlord\zawieszenie-2026-10-19-1609\logi\Armoury-2026-10-19_14-41-48.log`, sesja 19.10 14:41-16:09; trail.txt: GauntletCraftingScreen 15:59): 15:22:07-15:22:19 przetop `bracketed_heater_shield`, `southern_broad_2hsword_t4` (Broad Kaskara), 2 x `arakh`, `simple_pike_t2`, 3 x `nord_throwing_axe_2_t4`; 15:56:14-15:57:44 ok. 20 przetopow buzdyganow `vlandia_mace_1_t2` / `battania_mace_2_t2`; 13:48-13:49 (log `Armoury-2026-10-19_13-00-24.log`) i 14:47-14:49 seria przetopow pancerzy z liniami "SmeltTab: przetop X przyciety do kosztu wykucia: 30 -> 5 szt. metalu (wykucie 11)". W kodzie (dekompilacja BannerKings.dll i TaleWorlds.CampaignSystem.dll 1.4.8) i w danych czesci (crafting_pieces Native + RBM + RBM_WS + ROT, kolejnosc modulow z LauncherData.xml Jeffa) - symulacja na 432 broniach z projektem (skrypt w `n20-kuznia-tmp/py/sim.py`):

| Bron (z logu Jeffa) | Czesci (metal) | Przetop dotad (BK) |
|---|---|---|
| Spiked Mace / Highland Spiked Club (t2) | 2 zelaza kutego | 1 surowka + 1 zelazo kute |
| Simple Pike (t2) | 1 zelazo kute | 1 surowka |
| Arakh | 1 zelazo kute + 3 stal szlachetna | 1 surowka + 2 stal szlachetna |
| Broad Kaskara (t4) | 1 zelazo, 4 stal, 5 stal szl. | 1 surowka + 2 stal + 1 stal szl. |

Na 432 bronie: surowka z niczego w **352**, wiecej metalu niz w czesciach w **78** (np. lanca `empire_lance_1_t3_blunt`: 2 zelaza -> 1 surowka + 1 zelazo kute + 1 zelazo; drzewce `khuzait_polearm_1_t4_blunt`: 1 stal -> 1 surowka + 1 zelazo), surowka z broni BEZ metalu w **6** (`wooden_sword_t1/_t2`, `wooden_2hsword_t1`, `peasant_maul_t1`, `peasant_pitchfork_2_t1`), najwyzszy gatunek z mniejszosci czesci w **23** (np. `reaper_falx`: 2 zel. kute, 3 stal, 3 stal szl. -> 1 surowka, 2 stal, 1 stal szl.). Wsadu "crude" przy przetopie w kodzie NIE MA (SmeltingItemVM: wsad = ujemne pozycje wyniku, a tam ujemny jest tylko wegiel) - surowka jest zawsze w WYNIKU, przy kazdej broni; tak to odczytuje.

**Przyczyna:**
1. `BKSmithingModel.GetSmeltingOutputForItem` (BK nadpisuje gre): bierze wynik gry (materialy czesci, najwyzszy gatunek traci stopien, -1 wegiel), przycina metal do `GetMetalMax(klasa)` = 1-3 sztaby odejmujac po jednej sztabie od NAJNIZSZEGO gatunku w petli (j = 2..6), a na koniec `if (out[1] == 0 && metalMax > 0) out[1]++` - sztaba surowki z niczego. Stad surowka przy kazdej broni i gatunki "z gory" (tansze czesci odpadaja pierwsze).
2. `SmeltTab.OutputPostfix` jest wpiety we WSZYSTKIE modele z wlasna metoda (log: "wyjscie w 2 modelach" = DefaultSmithingModel i BKSmithingModel), a BK wola metode bazy - postfiks bazy dokladal odzysk pancerza/tarczy, postfiks BK dokladal go drugi raz: kazdy przetop pancerza x2; tarcza (BK widzi ja jako bron z klasa LargeShield/SmallShield) dostawala dodatkowo surowke z niczego.
3. Sufit `SmeltCapToCraftCost` (audyt 04.10) liczyl "koszt wykucia" z punktow ochrony (`Recipes.ArmourUnits`), bo wtedy tak liczyl CRAFT BK. Tego samego dnia (audyt K1, `FletchForge.BkArmorBillPostfix`, log "kwit pancerzy BK podmieniony na Prawo Wagi") kwit CRAFT BK przeszedl na wage (`Recipes.For`) - sufit zostal przy punktach ochrony i cial przetop do ok. 1/10 metalu kucia (lannister_light_armor: odzysk 30 -> 5, kwit kucia ok. 60).
4. Tarcza w CRAFT BK: `BKSmithingModel.GetCraftingInputForArmor` - galaz broni: `array[7] = 2` (drewno), zelazo `Iron4` tylko gdy `PhysicsMaterial == "shield_metal"`; nasze latki (FletchForge, TrueArmourCost) pomijaly przedmioty bez ArmorComponent. Drewniana tarcza (np. bracketed_heater_shield, `wood_shield`, 4.7 kg) kosztowala 2 drewna, a przetop (Recipes.For z wagi x ClassCostShield 0.55) oddawal sztaby zelaza - petla metalu z niczego.

**Zmiana:**
1. `SmeltWeaponByParts` (`SmeltTruth.ApplyWeapon`, z `SmeltTab.OutputPostfix` tylko w wywolaniu zewnetrznym - po BK): metal wyniku (indeksy 1-6) liczony od nowa z czesci broni: n = floor(metal czesci x udzial), udzial = `SmeltingReturnShare` + Smithing x `SmeltingSkillBonus`, najwyzej 0.5 (ta sama regula co pancerz, Jeff 30.08 "najwyzej polowa"), co najmniej 1 sztaba, gdy bron ma metal, nie wiecej niz sufit BK klasy (`GetMetalMax` czynnego modelu przez refleksje; bez BK - bez sufitu). Rozdzial na gatunki proporcjonalnie (najwieksze reszty; remis -> nizszy gatunek). Bez obnizania gatunku i bez surowki z niczego. Drewno (`SmeltNoWood`) i wegiel (`SmithCoal`) bez zmian.
2. `SmeltArmourOnce`: `SmeltTruth.IsOuter(__originalMethod)` - zewnetrzne = metoda najbardziej pochodnego modelu z wpietych (`SmeltTab.PatchedTypes`); wewnetrzne (baza wolana przez BK) nic nie dodaje, zewnetrzne zeruje metal 1-6 (surowka BK przy tarczy) i PRZYPISUJE odzysk z receptury.
3. `SmeltCapFromForgeBill`: sufit = metal receptury `Recipes.For` (= kwit CRAFT BK i naszej kuzni) x min(udzial, 0.5); tarcza z sufitem tylko przy `BkShieldBillByRecipe` (inaczej jak dotad - bez sufitu).
4. `BkShieldBillByRecipe`: `FletchForge.BkArmorBillPostfix` liczy tez tarcze z `Recipes.For` (metal okuc z wagi, drewno, wegiel, skora/len); `BkSpendMaterialsPrefix` zdejmuje drewno tarczy w kg z otwartego ladunku (`SmithWood.Use`) jak nasza kuznia.
5. Log: linia startu sesji "Kuznia przetop (n20-kuznia): start sesji" (klucze, samosprawdzenie na wszystkich broniach z projektem, pancerz i drewniana tarcza: odzysk z modelu wobec receptury, kwit tarczy BK); po prawdziwym przetopie broni "Przetop broni (n20-kuznia): <id> | czesci: ... -> wynik: ..." (raz na wzor w sesji); linia "Przetop (zakladka Smelt): <id>" dostala " | metal: ...".

**Tabela dla Jeffa - co wkladasz -> co wychodzi (Smithing 143, czyli udzial 50%; bez drewna i wegla):**

Przetop broni:

| Bron | Z czego zrobiona | Dotad | Teraz |
|---|---|---|---|
| Spiked Mace, Highland Spiked Club (t2) | 2 zelaza kutego | 1 surowka + 1 zelazo kute | 1 zelazo kute |
| Simple Pike (t2) | 1 zelazo kute | 1 surowka | 1 zelazo kute |
| Arakh | 1 zelazo kute + 3 stal szlachetna | 1 surowka + 2 stal szlachetna | 1 zelazo kute + 1 stal szlachetna |
| Broad Kaskara (t4) | 1 zelazo, 4 stal, 5 stal szl. | 1 surowka, 2 stal, 1 stal szl. | 1 stal + 2 stal szl. (sufit BK: 3 sztaby) |
| Reaper Falx | 2 zel. kute, 3 stal, 3 stal szl. | 1 surowka, 2 stal, 1 stal szl. | 1 zel. kute, 1 stal, 1 stal szl. |
| Execution Axe | 3 zel. kute, 1 zelazo, 1 stal | 1 surowka, 2 zel. kute, 1 zelazo | 1 zel. kute + 1 zelazo |
| Drewniany miecz, widly, palka chlopska | samo drewno | 1 surowka | nic (drewno sie spala) |
| Lanca empire_lance_1_t3_blunt | 2 zelaza | 1 surowka, 1 zel. kute, 1 zelazo (3 z 2!) | 1 zelazo |

Razem na 432 broniach: metal czesci 2038 sztab, BK oddawal 1273 (w tym 352 surowki z niczego), teraz 732 (ok. 36% - polowa metalu, przycieta do sufitu BK na duzych broniach).

Przetop pancerza i tarczy (zakladka Smelt):

| Sztuka | Kwit kucia (metal) | Dotad | Teraz |
|---|---|---|---|
| lannister_light_armor | ok. 60 sztab | 10 (5 x 2 - sufit z punktow ochrony, liczone dwa razy) | ok. 30 (polowa kwitu) |
| ranger_mail | ok. 24 | 4 (2 x 2) | ok. 12 |
| mail_shirt | ok. 14 | 6 (3 x 2) | ok. 7 |
| chainmail_shoulder_armor | ok. 4 | 2 (1 x 2) | 2 |
| tarcza drewniana (bracketed_heater_shield) | CRAFT BK: 2 drewna, 0 zelaza | odzysk z receptury x2 + 1 surowka | CRAFT BK: kwit z receptury (zelazo okuc, drewno, wegiel, skora); odzysk raz, najwyzej polowa |

Rafinacja (bez zmian, sprawdzona - `MaterialLaw.RefinePostfix`, ceny historyczne TAK u Jeffa, perki jak w grze):

| Wkladasz | Wychodzi |
|---|---|
| 1 ladunek drewna (100 kg) | 4 bryly wegla (6 z perkiem Charcoal Maker; log Jeffa "ladunek drewna 100 kg -> wegli 6") |
| 1 lupa z dymarki + 1 wegiel | 30 sztab surowki (15 kg) |
| 5 surowki + 1 wegiel | 4 zelaza kutego |
| 5 zelaza kutego + 1 wegiel | 4 zelaza |
| 5 zelaza + 1 wegiel (perk Steel Maker) | 4 stali |
| 5 stali + 1 wegiel (Steel Maker 2) | 4 stali szlachetnej |
| 5 stali szlachetnej + 2 wegla (Steel Maker 3) | 4 stali zamkowej |

Kucie broni (bez zmian, sprawdzone): metal = materialy czesci (gra, `GetSmithingCostsForWeaponDesign`), drewno w kg (`SmithWood`), wegiel 0.5 kg na bron z otwartej bryly (`SmithCoal`). Kucie pancerza w CRAFT BK i w naszej kuzni: `Recipes.For` z wagi (80% metalu swojego tieru + 20% tier nizej, crude nigdy - `IronForTier` t1/t2 = zelazo kute).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: `GetSmeltingOutputForItem` wolaja tylko zakladka Smelt (SmeltingItemVM, CraftingVM, DoSmelting), `SmeltTab.DoSmeltingPrefix` (pancerz) i samosprawdzenia (SmithCoal.SelfCheck, SmeltTruth.SessionStart) - AI nie przetapia, goracej sciezki brak. Wegiel (`SmithCoal.SmeltPostfix`, Priority.Last, indeks 8) i drewno (`SmeltNoWood`, indeks 7) - inne indeksy, kolejnosc bez znaczenia. Nauka czesci za przetop (f27, `ResearchSmeltPostfix`) liczy od wartosci przedmiotu - bez zmian. Legendy (`DoSmeltingPostfix`) i stal valyrianska (`ValyrianBlades` blokuje przetop) - bez zmian.
- Mniej metalu z broni niz dotad (36% zamiast 62% metalu czesci, z czego 17 pkt to surowka z niczego) - Jeff moze to odczuc przy przetapianiu lupu. Wylacznik: `SmeltWeaponByParts` = false (sposob BK).
- Wiecej metalu z ciezkich pancerzy (do polowy kwitu kucia zamiast ok. 1/10 x 2). Petla kucie -> przetop zostaje na minusie (polowa metalu ginie, wegiel, stamina, czas); sprzedaz lupu po cenach historycznych dalej lepsza niz przetop (cena sztuki z metalu + robocizny). Wylacznik: `SmeltCapFromForgeBill` = false.
- Bron z 1 sztaba metalu oddaje te 1 sztabe (minimum 1, jak pancerz) - petla kucie/przetop takiej broni kosztuje tylko wegiel, drewno, stamine i czas (dotad BK oddawal 2 sztaby z 1 - gorzej).
- Tarcza w CRAFT BK drozsza (metal okuc + wegiel + skora zamiast 2 drewna). Wylacznik: `BkShieldBillByRecipe` = false. Drewno tarczy schodzi z otwartego ladunku (SmithWood.Use) - jak w naszej kuzni; tarcze metalowe (`shield_metal`) maja teraz tez kwit z receptury (dotad BK: Iron4 x waga).
- `IsOuter`: gdy czynny model nie dziedziczy po zadnym wpietym typie (inny mod z wlasnym modelem w zestawie bez BK) - zachowanie jak dotad (kazde wywolanie zewnetrzne). Harmony wstrzykuje `__originalMethod` (Harmony 2.x).
- Zgodnosc zapisu: brak nowych danych zapisu.
- Kod BK uzyty przez refleksje: `BKSmithingModel.GetMetalMax(WeaponClass)` (publiczna); brak = bez sufitu (jak gra).

**Decyzje (podjete sam, kazda z wylacznikiem):**
1. Ilosc z przetopu broni = ta sama regula co pancerz (najwyzej polowa metalu, rosnie ze Smithing), z sufitem klasy BK - "kuznia 1:1" (Jeff 26.08) i "przetop zwraca najwyzej polowe receptury" (Jeff 30.08). Gatunki - proporcja czesci, remis do nizszego gatunku. Bez obnizania gatunku (gra robila ze stali zelazo, z zelaza kutego surowke - to wlasnie dawalo "crude" w wyniku). Wylacznik `SmeltWeaponByParts`.
2. Odzysk pancerza raz (to blad, nie projekt). Wylacznik `SmeltArmourOnce`.
3. Sufit przetopu pancerza od prawdziwego kwitu kucia. Wylacznik `SmeltCapFromForgeBill` (wtedy sufit z punktow ochrony, jak dotad).
4. Tarcza w CRAFT BK z naszej receptury (jeden kwit na tarcze w obu kuzniach, koniec metalu z niczego). Wylacznik `BkShieldBillByRecipe`.
5. Rafinacji nie ruszalem - lancuch jest spojny. Martwy kod "tygiel w menu miasta" (`SmithMenu.SmeltConsequence` - opcja nie jest rejestrowana) zostawiony bez zmian.

**Test w grze:**
- Autotest 5 dob + bitwa wystarczy (zadnych plikow/flag). W logu Armoury przy starcie sesji linia `Kuznia przetop (n20-kuznia): start sesji` - ma byc: klucze TAK x4, "model BKSmithingModel", "wyjscie wpiete w 2 modelach", "gatunek wyzszy niz w czesciach 0", "surowka bez surowki w czesciach 0" (wyjatek: ok. 22 czesci gry naprawde maja surowke - wtedy nie liczy sie), "ponad polowe metalu czesci 0", "bez metalu w czesciach N" (N ok. 6+), "pancerz: <id> model X / receptura X (...) = raz", "tarcza: ... = raz", "kwit tarczy w CRAFT BK: <id> metal M (receptura M) ... = z receptury". Bez klucza: "= DWA RAZY" i "= kwit BK".
- W grze Jeffa (zakladka Smelt): po przetopie broni linia `Przetop broni (n20-kuznia): <id> | czesci: ... -> wynik: ...`; po przetopie pancerza `Przetop (zakladka Smelt): <id> | metal: ...` - liczba sztab ma sie zgadzac z panelem i przyrostem w sakwach (dotad x2).
- Brak ERROR `SmeltTruth.` i `SmeltTab.` w logu.
