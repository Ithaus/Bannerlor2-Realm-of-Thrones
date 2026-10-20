## m96-receptury (20.10) - przepisy kuzni z masy przedmiotu, otwarte ladunki surowcow, przetop bez zysku (Armoury)

**Dla Jeffa (prosto).** Kuznia liczy teraz kazdy luk, kusze, strzaly i pancerz z tego, ile surowca naprawde w nich siedzi, plus rozsadny odpad:
cieciwa to 30 gramow lnu, a nie cala 10-kilogramowa bela za 100 denarow, luk nie bierze ani grama metalu, seria trzech kolczanow bierze 1-6 sztab zelaza na groty (pociski balist do 18),
a tier zmienia gatunek metalu, nie ilosc. Tak jak z drewnem: napoczeta bela lnu, skora czy sztaba zostaje otwarta przy kowadle i starcza na nastepne
sztuki - nowa schodzi z sakw dopiero, gdy otwarta sie skonczy. Przetop oddaje metal, ktory jest w sztuce, minus straty (zgar, zuzel) i nigdy wiecej niz
polowe tego, co poszlo na wykucie - na kuciu i przetapianiu nie da sie zarobic. Wylacznik: "Recipes By Mass" w MCM (grupa Time at the anvil).

**Problem.** Jeff 20.10 (gra na 95a): "przejrzyj kucie strzal i lukow i pancerzy, bo niektore strzaly wymagaja 14 np. najlepszej stali albo luki 8 lnu
etc., to bez sensu"; doprecyzowanie ok. 16:45: "len jest drogi, moze zastosujmy ogolna zasade wagi surowca, tak jak jest w drewnie ... popatrz, ile
1 jednostka danego potrzebnego surowca wazy i na ile starcza w danym przedmiocie"; o przetopie: "odda najwyzej tyle, ile przepis bierze, zeby nie dalo
sie na nim zarabiac - przetopi sie mniej, straty etc.".

**Dowod.**
- Jednostki w grze (items-dump.csv z sesji Jeffa 14:55): kazda sztaba (ironIngot1-6) 0.5 kg (1, 1, 2, 3, 4, 6 d), len 10 kg (100 d), skora 10 kg (40 d),
  drewno (hardwood) - ladunek 100 kg (4 d), wegiel 5 kg. Luki waza 0.1-0.8 kg, strzala 0.05-0.15 kg, kusza 1-5 kg.
- Kod starego przepisu (`Recipes.BuildRecipe` + `Legendize`): luk/kusza = 75% wagi w drewnie + 25% wagi w sztabach swojego tieru (min. 1) + 1 len na cieciwe,
  x2 przy t5-6 (`RangedHighTierCostFactor`), legenda x4 (`LegendaryMaterialFactor`) + Iron6 x tier. Stad dokladnie liczby Jeffa: `dragonbone_bow` i
  `goldenheart_longbow` (0.1 kg) = **14 x Iron6 + 8 x len** (87 kg surowca, 884 d na luk 0.1 kg). Pancerze skorzane i tkane - z punktow ochrony w CALYCH
  sztukach skory i lnu (po 10 kg), reszta "w zelazo" (okucia w sukience). Strzaly w naszej kuzni stali nie biora (log 14-53-56, 14:55:42: `Kuznia: amunicja
  (S1) ... ze stali Iron4-Iron6 0`) - "14 najlepszej stali" to kafelek luku-legendy (wyzej); amunicja miala za to 1 kg drewna na 3 kolczany (90 drzewc).
- AUDYT wszystkich 2546 przepisow (luki, kusze, strzaly, belty, pancerze, peleryny, rzad konski) - `m96-receptury-tmp/audyt_kg.py` (model C# offline z
  items-dump + XML modulow), wynik `m96-receptury-tmp/przepisy-kg-przed-po.csv` (id, typ, tier, waga, material, stary przepis, masa i koszt, nowy przepis
  w kg, masa i koszt, przetop przed/po, flagi). Wsrod 2142 realnych sztuk (waga > 0.05 kg, bez dp_) stary przepis mial: masa surowca > 5 x masa wyrobu
  **675**, miekkie (len/skora) > 3 x masa wyrobu **230**, cieciwa z calej beli lnu **42** (28 lukow, 14 kusz), metal w luku **28**, zelazo w tkaninie **39**,
  legendy x4 **6**. Nowy przepis: **0** flag (najwiecej x4.3 - luk t6 z klepki: drewno x2 odpad x2 dobra klepka).

Mediana (masa surowca / masa wyrobu) i (koszt materialu / wartosc sztuki):

| Grupa | n | masa: bylo | masa: jest | koszt/wartosc: bylo | jest |
|---|---|---|---|---|---|
| Luki | 28 | x36.3 (max x874) | x2.08 (max x4.3) | 5.37 | 0.02 |
| Kusze | 14 | x8.0 | x1.40 | 3.70 | 0.07 |
| Strzaly / belty (seria 3 kolczanow) | 34 / 20 | x0.31 | x1.46 | 0.28 / 0.36 | 0.29 / 0.39 |
| Korpus tkany | 197 | x20.0 (max x51) | x1.10 | 11.1 | 0.61 |
| Korpus skorzany | 233 | x3.8 (max x50) | x1.23 | 0.94 | 0.28 |
| Nogi skorzane | 119 | x13.3 | x1.23 | 2.86 | 0.23 |
| Korpus kolczuga | 103 | x1.70 (max x13.8) | x1.36 | 0.15 | 0.14 |
| Korpus plyta | 157 | x1.33 | x1.36 | 0.15 | 0.18 |
| Helm plytowy | 445 | x1.25 | x1.36 | 0.28 | 0.32 |

Przyklady (przed = build 95a, po = ten build; koszt po cenach z items-dump; przetop = metal z zakladki Smelt przy Smithing 25+):

| Przedmiot | Waga | BYLO | masa / koszt | JEST | masa / koszt | Przetop bylo -> jest |
|---|---|---|---|---|---|---|
| dragonbone_bow t6 (legenda, blad Jeffa) | 0.1 kg | 14x Iron6 + 8x len + 0.4 kg drewna | 87 kg / 884 d | 0.03 kg lnu (cieciwa) + 0.4 kg drewna | 0.43 kg / 0.3 d | - |
| goldenheart_longbow t6 (legenda) | 0.1 kg | 14x Iron6 + 8x len + 0.4 kg drewna | 87 kg / 884 d | 0.03 kg lnu + 0.4 kg drewna | 0.43 kg / 0.3 d | - |
| stealth_bow t4 (legenda) | 0.45 kg | 4x Iron4 + 4x Iron6 + 4x len + 0.4 kg | 44 kg / 436 d | 0.03 kg lnu + 0.9 kg drewna | 0.93 kg / 0.3 d | - |
| noble_long_bow t6 | 0.8 kg | 2x Iron6 + 2x len + 0.6 kg | 21.6 kg / 212 d | 0.03 kg lnu + 3.2 kg drewna (klepka x2, dobra x2) | 3.2 kg / 0.4 d | - |
| woodland_longbow t6 | 0.5 kg | 2x Iron6 + 2x len + 0.4 kg | 21.4 kg / 212 d | 0.03 kg lnu + 2.0 kg drewna | 2.0 kg / 0.4 d | - |
| hunting_bow t1 (refleksyjny) | 0.3 kg | 1x Iron2 + 1x len + 0.4 kg | 10.9 kg / 101 d | 0.15 kg skory (rog i sciegno) + 0.03 kg lnu + 0.36 kg drewna | 0.54 kg / 0.9 d | - |
| crossbow_g (300 funtow) | 2.2 kg | 2x Iron2 + 1x len + 1.7 kg | 12.7 kg / 102 d | 0.31 kg Iron2 (zamek) + 0.62 kg Iron6 (lek stalowy) + 0.075 kg lnu + 2.0 kg drewna | 3.0 kg / 8.8 d | - |
| crossbow_a | 1.0 kg | 1x Iron2 + 1x len + 0.8 kg | 11.3 kg / 101 d | 0.14 kg Iron2 + 0.075 kg lnu + 1.2 kg drewna (lek drewniany) | 1.4 kg / 1.1 d | - |
| default_arrows t1 (3 x 30) | 4.5 kg | 2x Iron2 + 1 kg drewna | 2.0 kg / 2 d | 0.81 kg Iron2 (groty) + 5.7 kg drewna (drzewca) | 6.6 kg / 1.8 d | - |
| hardened_125_gr_arrows t6 | 11.7 kg | 5x Iron3 + 1 kg | 3.5 kg / 10 d | 2.1 kg Iron3 + 14.9 kg drewna | 17.0 kg / 9.0 d | - |
| bolt_a t6 | 13.5 kg | 5x Iron3 + 1 kg | 3.5 kg / 10 d | 2.4 kg Iron3 + 17.2 kg drewna | 19.6 kg / 10.4 d | - |
| royce_armor t5 (skora) | 17 kg | 6x Iron5 + 4x skora + 1x len | 53 kg / 284 d | 3.6 kg Iron5 (okucia) + 17.3 kg skory | 20.9 kg / 98 d | - |
| steppe_leather_boots t2 | 0.8 kg | 2x skora | 20 kg / 80 d | 0.17 kg Iron2 + 0.82 kg skory | 0.98 kg / 3.6 d | - |
| fm_noble_dress1 t2 (tkanina) | 0.4 kg | 1x Iron2 + 2x len | 20.5 kg / 201 d | 0.44 kg lnu | 0.44 kg / 4.4 d | - |
| aserai_civil_d_hscarf t1 (chusta) | 0.1 kg | 1x len | 10 kg / 100 d | 0.11 kg lnu | 0.11 kg / 1.1 d | - |
| knight_cap t4 (plyta) | 1.2 kg | 1x Iron3 + 2x Iron4 | 1.5 kg / 8 d | 0.30 kg Iron3 + 1.21 kg Iron4 + 0.12 kg lnu | 1.6 kg / 9.7 d | 0.75 -> 0.76 kg |
| northern_lamellar_armor t2 (plyta) | 1.9 kg | 5x Iron2 + 1x skora | 12.5 kg / 45 d | 2.39 kg Iron2 + 0.19 kg lnu | 2.6 kg / 6.7 d | 1.25 -> 1.20 kg |
| rot_horse_saddle2 t2 (plyta, waga do kosztu) | 15.2 kg | 32x Iron2 + 1x skora | 26 kg / 72 d | 19.2 kg Iron2 + 1.5 kg lnu | 20.7 kg / 53 d | 8.0 -> 9.6 kg (najwyzej polowa z 19.2) |

**Zasada.**
- Przepis = kilogramy, ktore wchodza w sztuke, plus odpad kucia; wspolczynniki te same, z ktorych gra liczy wartosc sztuki i ktore zuzywa warsztat AI
  (`ArmsPricing.CostOf`, regula R7 "jedna wycena"): metal x1.4 (zgorzelina, obcinki), groty x1.2, drewno x1.5 (klepka, drzewca), skora x1.2, len x1.1.
- Budowa: **luk** - klepka (drewno 2 x masa luku; t5-6 jeszcze x2 - dobra klepka, mniej sztuk z klody), bez metalu; refleksyjny (composite/steppe/nomad/
  recurve/desert... w id albo nazwie) - rdzen z drewna (60%) i rog ze sciegnem jako skora (40% masy x1.25). **Cieciwa** - 20 g lnu (kusza 50 g), x1.5 odpad;
  bez lnu w sakwach i w otwartym ladunku - rzemien ze skory (jak dotad). **Kusza** - lozysko z drewna (60% masy), zamek i strzemie z zelaza Iron2 (10%),
  lek (20%): stalowy przy kuszy mocy tieru 4+ (`XbowPrice.ForgeTier`, gatunek wedlug mocy: 300 funtow = Iron6), slabszej drewniany. **Strzaly/belty** -
  groty: 15% masy strzal serii x1.2, gatunek grotu jak w wartosci (S2: Iron2 do t4, Iron3 t5-6), drzewca 85% x1.5, lotki pominiete. **Pancerz** wedlug
  MATERIALU: plyta/kolczuga - metal 90% (80% swojego tieru + 20% tier nizej - regula Jeffa 01.09) + len podszycia 10%; skora - skora 85% + okucia 15%;
  tkanina - len; peleryna tkana t5+ - futro (skora 50%). Tier zmienia gatunek metalu, nie ilosc. **Legenda** - caly metal ze stali zamkowej (Iron6), metal x2
  (mistrz odrzuca kazdy gorszy kawalek), stamina x3 i prog legendy jak dotad - bez x4 na wszystko.
- **Otwarte ladunki** (jak drewno `SmithWood`): niepelna jednostka zostaje otwarta i starcza na kolejne sztuki, nowa z sakw dopiero, gdy otwarta sie skonczy.
  Metal - skrzynka resztek kuzni (`SmeltScraps`, tysiaczne sztaby, zapis `arm_smeltscraps` bez zmiany formatu): napoczeta sztaba i resztki przetopu to ten sam
  metal; len i skora - nowy `SmithStock` (kg, zapis `arm_smith_stock`, `SaveText.Sync`); drewno - `SmithWood` bez zmian. Ekran CRAFT i nasze menu pokazuja
  sztuki do otwarcia (0, gdy otwarty ladunek starcza) - tak jak dzis drewno; po kuciu wiadomosc "Materials at the anvil: ... Left open: Linen 9.97 kg ...".
- **Przetop**: masa metalu w SZTUCE (bez odpadu kucia) minus straty (zgar i zuzel): plyta 25%, kolczuga 35% (tysiace kolek i nitow), okucia skory/tkaniny 40%,
  metal luku/kuszy 30%, groty 40%; stal (Iron4+) +5% (odweglenie liczone jako strata - gatunek bez zmian, tak jak przetop broni z czesci SmeltWeaponByParts;
  obnizanie gatunku przy jednej regule dla pancerza i nie dla broni byloby dwiema regulami jednego zjawiska). Sufit: udzial przetopu (SmeltingReturnShare +
  Smithing x bonus, najwyzej 0.5 - Jeff 30.08) x kg PRZEPISU tej sztuki. Plyta: 0.9 x 0.75 = 0.675 masy sztuki, sufit 0.5 x 1.26 = 0.63 - kucie + przetop
  traci ok. 50% metalu (audyt: przetop / metal przepisu najwyzej 0.5000 we wszystkich 2142 sztukach, Smithing 25+). Drewno, skora, len i rog przepadaja.
  `SmeltWeaponByParts`, `SmeltArmourOnce`, `ValyrianNoSmelt`, `SmeltKeepsScraps` zostaja; `SmeltCapToCraftCost` nie jest potrzebny przy przepisie z masy (sufit w regule).

**Zmiana** (klucz MCM `RecipesByMass`, domyslnie TAK; NIE = przepisy w sztukach jak dotad):
- `Armoury/src/Recipes.Mass.cs` (nowy): regula (`MassKg`), przepis (`MassFor` - tier, stamina, prog i trudnosc z `BuildRecipe` jak dotad, czesci = sztuki do
  otwarcia, `Recipe.Kg` = kg), pobranie (`MassTake` - liczy naprawde zdjete sztuki), czesc (`MassTakePartial`, `MassMissing` - pekniecie, naprawa), CRAFT BK
  (`MassCommitBk`, `BillMatches`), przetop (`MassSmeltBars`, `MassSmeltYield`, `MassSmeltMilli`), dokladne sztuki do naprawy (`ExactUnits`), linia startu.
- `Armoury/src/SmithStock.cs` (nowy): otwarte ladunki lnu i skory w kg, eksport/import.
- Wpiecia: `Recipes.cs` (partial, 2 pola `Recipe`, 1 linia w For / TakeMaterials / MissingParts / TakePartial / SmeltYield / Describe), `SmeltScraps.cs`
  (BoxMilli/BarsFor/ForgeUse; ArmourMilli - 2 linie), `SmeltTab.cs` (sufit tylko dla starego przepisu, lista Smelt widzi dokladny odzysk), `MendMaterial.cs`
  i `SmithMenu.cs` (naprawa z dokladnych sztuk), `PriceList8.cs` (linia logu kwitu), `FletchForge.cs` (postfiks SpendMaterials CRAFT BK - kg z otwartych
  ladunkow, tylko gdy kwit BK = przepis z masy), `Patches.cs` (TrueArmourCost: kazdy pancerz bierze kwit z przepisu z masy), `ArmouryBehavior.cs` (zapis
  `arm_smith_stock`, reset nowej kampanii, nasluch OnAfterSessionLaunched), `Settings.cs` + `McmSettings.cs` (gen_mcm), `tools/sprawdz_logi.py` (grupa `m96receptury`).
- Gracz, CRAFT BK, nasze menu i naprawa wlasnymi rekami ida tym samym przepisem. AI: warsztaty uzbrojenia nie uzywaja `Recipes` - licza z `ArmsPricing.CostOf`
  (te same kg i wspolczynniki, bez zmian). Zlecenia kowalskie (CraftOrders) to bron z projektu gry - koszt z `GetSmithingCostsForWeaponDesign` + SmithWood/SmithCoal,
  przepisy z tej paczki ich nie dotycza.
- Linie logu: `Przepisy z masy (m96-receptury): start sesji | klucz RecipesByMass TAK | policzone N (pancerze A, luki B, kusze C, strzaly i belty D; legendy L),
  stary przepis bez receptury wartosci 0, metal w lukach 0 (ma byc 0) | jednostki: sztaba 0.5 kg, len 10 kg, skora 10 kg, drewno ladunek 100 kg | masa surowca /
  masa wyrobu: mediana xM, najmniej: ..., najwiecej: ... | otwarte ladunki: ... | przyklady: dragonbone_bow t6 0.1 kg: BYLO 14x ironIngot6 + 8x linen + 0.4 kg wood
  (87 kg) -> JEST 0.03 kg linen (string) + 0.4 kg hardwood (stave); ... | potkniecia 0.` (OnAfterSessionLaunched, po ROT-RBM). Przy kuciu (pierwsze 40, potem co 25.):
  `Surowce w kuzni (kg) (m96-receptury): <id> | zuzyte: ... | z sakw: ... | otwarte: ... | od startu sesji: kuc N, sztuk z sakw M, potkniecia 0.`

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje / ten sam stan: kazdy czytelnik `Recipe.Parts` przejrzany (grep `.Parts`): kwit CRAFT BK (FletchForge.BkArmorBillPostfix, TrueArmourCost) i
  HasMaterials czytaja sztuki do otwarcia - zamierzone (jak drewno); przetop (SmeltTab, SmeltScraps, Forge.Smelt, SmithMenu) i naprawa (MendMaterial, SmithMenu,
  PriceList8) czytaja kg / dokladne sztuki (inaczej otwarty ladunek zanizalby odzysk). Tarcze (FletchForge.ShieldBillCheck, aksamit, SmeltTruth) - stary przepis.
- Nic z niczego: otwarty ladunek dostaje tylko sztuki naprawde zdjete z sakw (`MassTake` liczy roznice; CRAFT BK - tylko gdy kwit BK = przepis; brak = 0, nie
  ujemny metal). Pekniecie w CRAFT BK (ArmourBotchHalfBack) zwraca czesc zdjetych sztuk, a otwarty ladunek ma zapisane cale kg - gracz traci najwyzej tyle co
  dotad (zwrot B sztuk <= kg przepisu, dowod: Units <= (kg - otwarte)/jednostka + 1, B <= Units - 1). Kucie + przetop < 100% (audyt: najwyzej 0.50).
- Kolizje: skrzynka resztek przetopu (r4-przetop, `SmeltKeepsScraps`) jest teraz takze otwarta sztaba kuzni - resztki przetopu ida w kucie, napoczete sztaby
  w przetop (cala sztaba wychodzi do sakw, gdy skrzynka sie zapelni). Okno przetopu (Begin/End) biegnie wewnatrz DoSmelting - kucie go nie przecina. Bez
  `SmeltKeepsScraps` skrzynka dalej sluzy kuciu.
- Ekonomia: luki i kusze kosztuja teraz grosze w materiale (praca, stamina i czas zostaja) - dotad material byl 3-5x drozszy od sztuki i nikt by ich nie kul;
  pancerze tkane i skorzane wielokrotnie tansze (tkanina 11x wartosci -> 0.6), metalowe podobnie jak dotad (helm i plyta troche drozsze - odpad x1.4,
  kolczuga tansza). Legendy: bez x4 - luk-legenda (stealth_bow) prawie bez materialu, nadal jedna sztuka, prog legendy i stamina x3; legendy z rzadkiego
  materialu (dragonbone, goldenheart, weirwood, giant) i tak nie do wykucia (`RareBowsNotForged`).
- Mocne kusze: lek stalowy wedlug mocy (m96-wzory zostawilo metal kuszy przy tierze gry) - swiadomie: sila 300-funtowej kuszy to stalowy lek.
- Peleryny: `CostOf` liczy kazda peleryne jak tkanine (193 naramienniki plytowe ROT wycenione "z lnu") - przepis kuzni idzie wedlug materialu (plyta = metal);
  wycena sztuki w HistoricalPrices tego nie zmienia (osobny temat, nie ruszalem cen).
- Ekran CRAFT: sztuki do otwarcia, bez podpowiedzi kg otwartego lnu/skory/metalu na ekranie (drewno i wegiel ja maja) - widac w wiadomosci po kuciu i w opisie
  naszego menu (`Describe`). Do dorobienia, jesli Jeff zechce.
- Zapis: nowy klucz `arm_smith_stock` (build 95a go nie czyta - otwarty len/skora przepada przy powrocie na 95a, ok. 10 kg na surowiec); format skrzynki bez zmian.
  Wylaczenie klucza w trakcie kampanii: otwarty len i skora czekaja w zapisie, skrzynka metalu dalej sluzy przetopowi.
- Wydajnosc: `For()` przy kluczu liczy `BuildRecipe` + `MassKg` (CostOf w pamieci) - BK pyta o kwit przy wyborze i akcjach, nie co klatke; petla po wszystkich
  przedmiotach raz na sesje (linia startu). Watek glowny.
- Nie zmienione: tarcze, bron z ekranu kuzni gry, wegiel (pancerze i luki go w naszych przepisach nie braly - jak dotad), warsztaty AI, zlecenia kowalskie.
- Konfiguracja Jeffa: klucz nowy, nie ma go w Armoury.json - obowiazuje domyslne TAK, nic do zmiany.

**Status.** DO SPRAWDZENIA (build Armoury kod 0; ForgeView nieruszany; w grze nie testowane; liczby z modelu offline `audyt_kg.py` - w grze potwierdzi je linia startu).

**Test w grze.** (1) Wczytac zapis: linia `Przepisy z masy (m96-receptury): start sesji ... metal w lukach 0 ... mediana x1.3-1.5 ... potkniecia 0` z przykladami
jak w tabeli. (2) CRAFT -> Bows: zwykly luk pokazuje 1 len (gdy nic otwartego) i 0 metalu; po wykuciu wiadomosc "Materials at the anvil: 0.03 kg Linen ... Left open:
Linen 9.97 kg", drugi luk - 0 lnu. (3) Strzaly: seria bierze 1-6 sztab zelaza (balisty do 18), drewno w kg. (4) Pancerz skorzany w CRAFT: skora w calych sztukach tylko do otwarcia,
linia `Surowce w kuzni (kg) ... (CRAFT BK)`. (5) Przetop kolczugi w zakladce Smelt: nie wiecej niz polowa metalu jej przepisu (`Przetop resztki (r4-przetop)`).
(6) Zapisac, wczytac - otwarte ladunki te same (linia startu: "otwarte ladunki"). (7) `python -I tools/sprawdz_logi.py --grupa m96receptury <Armoury-*.log>`:
R96-0..5 bez NIE.
