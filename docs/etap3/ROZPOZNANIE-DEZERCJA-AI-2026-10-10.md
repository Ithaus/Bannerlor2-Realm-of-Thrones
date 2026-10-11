# Rozpoznanie 1 - dezercja AI w 2. polowie biegu (morale, zaopatrzenie BK, zalogi, Inni)

Tylko rozpoznanie. Zadnych zmian w drzewie e2c, nic nie budowane, gra nie uruchamiana.
Dekompilacje (ilspycmd 9.1) w tym katalogu: `dec/` (pojedyncze typy), `bkfull/` (caly BannerKings.dll), `rpfull/` (caly BKROTPatch.dll), `game/` (typy gry).
Skrypty pomiaru: `parse.py`, `table.py`, `split.py`; dane: `d457.json`, `d218.json`, `*.inni`, `pool*.txt`, `bitwy457.txt`, `zalogi457.txt`.
E2C = `...\7016f733-...\scratchpad\noc2\e2c`. Logi: L457 = `kopia-okup1-457\Armoury-2026-10-10_10-38-24.log`, L218 = `kopia-bk-218\Armoury-2026-10-10_13-54-14.log`, L15 = `Modules\Armoury\Armoury-2026-10-10_15-27-53.log`.

Uwaga o dobach: linia 169c "dzien X" i linia 183 "dzien X+1" opisuja te sama dobe (183 liczy "od wczoraj"); w tabelach przesunalem 183 o 1.

---------------------------------------------------------------------------------------------------

## 0. Najwazniejsze wnioski (skrot)

1. **To nie jest pora roku.** W obu dlugich biegach przez wszystkie doby: `zima: nie` (458/458 linii Drewno w L457). Klimat Westeros: lato do doby ~131, potem jesien 412-443 dni (`Klimat: dzien 108967 - nowa pora Autumn, potrwa 412 dni`). Wzrost to czas trwania kampanii, nie sezon.
2. **Liczba "lordowie morale w glodzie 1->177/d" to glownie Inni (wighty), nie ludzie.** 169c wlicza partie Innych do "partie lordow AI" (Measure169c.cs:448 grupa wedlug IsLordParty), a nasze prawo dezercji ich nie obejmuje (DesertionLaw.cs:95 `!Undead.Party(mp)` -> vanilla). Roznica 169c - 183 = 10 197 ludzi w L457 (korelacja dzienna 0.56 ze "stratami" Innych z linii "Wzrost Innych (169c)"). W oknie 420-447: 169c lordowie 270/d, z tego ludzie (183) 112/d, Inni ~158/d (Inni "straty" -161/d).
3. **Rozklad calego biegu L457 (459 dob, 43 738 dezerterow AI):** ludzie lordow z morale 16 666 (38%), Inni ~10 197 (23%), przepelnienie zalog 8 898 (20%), przepelnienie partii lordow 6 668 (15%), zalogi z morale 930, zalegly zold 379.
   Druga polowa (doby 182-458) wobec pierwszej: razem 134/d (36/d); ludzie z morale 52/d (13; w glodzie 12), Inni 34/d (4), zalogi limit 28/d (7), lordowie limit 17/d (11).
4. **Ludzka dezercja z morale jest epizodyczna:** mediana 8/d, 48% calosci w 22 dobach (>=150/d): doby 193-198, 254-255, 306 (554), 357-367 (807, 636, 525...), 375, 425, 445-447 (750, 531, 413). Epizody wypadaja przy dniach wielkich bitew (358: 7 417 zabitych+rannych, 446: 9 718), korelacja z sumą 3 dob 0.22. W dlugich logach NIE MA linii 183d (skladniki morale), wiec sklad epizodow jest hipoteza (patrz 3.4).
5. **Zaopatrzenie BK (alkohol, drewno) - mechanizm ma trzy wzmacniacze kary i jeden blad zakupu, zadna partia AI praktycznie nic nie kupuje ani nie zuzywa:** alkohol "zaopatrzenie BK" zuzyte 1-6 szt./dobe na caly swiat, kupione 0-12 szt./dobe; drewno 0-4 / 0-12 (L457, linie "Towary:"). Liczby z linii bilansu "zaopatrzenie BK (zuzycie) ~5967 / (zakupy) ~1377" to **liczba wywolan metod (ramek), nie sztuki** (GoodsLedger.cs:999-1001 `_framesN`): ~9 wywolan ConsumeItems na partie na dobe.
6. **Przepelnienie zalog idzie do lasu, nie do domu:** vanilla zdejmuje 25% nadwyzki ponad PartySizeLimit dziennie (+5 przy zaleglym zoldzie zalogi), OutlawLaw.OnTroopsDeserted wrzuca KAZDEGO dezertera do puli wyrzutkow (OutlawLaw.cs:330-339) - takze wighty Innych. 166 wylacza tylko limit zoldu (ClanBudget.cs:904-918), nie przepelnienie.

---------------------------------------------------------------------------------------------------

## 1. Mechanizm zaopatrzenia BK (dekompilacja)

### 1.1 Gdzie i kiedy
- `BKPartyNeedsBehavior` (dec/BannerKings.Behaviours.PartyNeeds.BKPartyNeedsBehavior.cs): `DailyTickPartyEvent` -> AddPartyNeeds + `PartySupplies.Tick()` (:31, :172-203); tylko `IsLordParty` (:225-231); wejscie partii lorda do MIASTA/zamku -> `BuyItems()` (:164-170); po wczytaniu `PostInitialize()` dla wszystkich (:213-223).
- `PartySupplies` (dec/...PartySupplies.cs): potrzeby zapisywane w save (SaveableProperty 3-11: AlcoholNeed, WoodNeed, ToolsNeed, ClothNeed, ArrowsNeed, WeaponsNeed, HorsesNeed, AnimalProductsNeed, ShieldsNeed). `DaysOfProvision = 10` w PostInitialize (:98), nie zapisywane. `AutoBuying = party != MainParty` (:92).
- BKROTPatch: brak typow potrzeb/zaopatrzenia. Typy morale/zoldu: `BKROTPartyMoraleModel`, `BKROTBattleMoraleModel`, `BKROTPartyWageModel`, `GetCharacterWagePatch`. `BKROTPartyMoraleModel` (dec/BKROTPartyMoraleModel.cs) dziedziczy po `DefaultPartyMoraleModel`, dodaje tylko blogoslawienstwa bostw (+8..+30, :61-141) i +2 do dziennej kary glodu za Saagaela (:149-181). Kary zaopatrzenia sa z latki BK na `DefaultPartyMoraleModel` (dziedziczona).

### 1.2 Kategorie (PartySupplies.cs:99-134)
alkohol: Wine, Beer, Mead | produkty zwierzece: Meat, Cheese, Butter, Fish, Honey, Eggs, Garum | narzedzia: Tools | konie: Horse | amunicja: Arrows | bron: MeleeWeapons2/3 | tarcze: Shield2/3 | tekstylia: Wool, Linen, Flax | drewno: Wood (hardwood).

### 1.3 Przyrost potrzeby (BKPartyNeedsModel.cs:20-36, PartySupplies.cs:233-305)
- Dzienna potrzeba = suma po stosach `ludzie x stawka x PartySuppliesFactor` (BK MCM Jeffa: `"PartySuppliesFactor": 0.5`; domyslna w kodzie BK tez 0.5 - bkfull/BannerKings.Settings/BannerKingsSettings.cs:223).
  Stawki: alkohol 0.025, drewno 0.02, zwierzece 0.01, tekstylia 0.01, narzedzia 0.01 (tylko oblezenie), bron 0.006 (nie-strzelcy), strzaly 0.003 (formacje 1/3), tarcze 0.003, konie 0.001.
  Przy factor 0.5: alkohol 0.0125/czl./dobe, drewno 0.01, zwierzece 0.005.
- `AddFactor(-1) "In a town or castle"` gdy partia w miescie/zamku (BKPartyNeedsModel.cs:64-67 i analogicznie) -> wynik 0. Kwatermistrz: SupplyEfficiency i cecha CalculatingSupplies (mnozniki, :68-73).
- **Brama przyrostu: `Party.MemberRoster.Count > MinimumSoldiersThreshold`** (PartySupplies.cs:233). `TroopRoster.Count` to liczba STOSOW (rodzajow jednostek), nie ludzi (game/TroopRoster.cs:46 `Count => _count`; TotalManCount :61). Prog = 5 + Przywodztwo/10 (BKPartyNeedsModel.cs:38-51); mediana Przywodztwa lordow AI 195 (linia "Musztra AI") -> prog ~24 stosow. Potrzeba rosnie tylko w partiach z >~24 rodzajami jednostek.
- Mnozniki przyrostu: lifestyle Commander -0.25, prawo ArmyLegion -0.15 (:242-259).
- Klamry (od DZISIEJSZEJ liczby ludzi, tylko gdy brama otwarta): alkohol +-0.25 x ludzi (:262), drewno +-0.3 (:265), narzedzia +-0.1, tekstylia +-0.3, zwierzece +-0.25 (:301), bron +-ludzi, strzaly/konie/tarcze z dzieleniem calkowitym `num7 / TotalManCount` = 0 (bledy BK, nieistotne tu).
- Gdy brama zamknieta, potrzeba ZAMARZA (bez przyrostu i bez klamry), a kara liczy sie dalej.

### 1.4 Kara morale (bkfull/BannerKings.Patches/VanillaModelTweakPatches.cs:965-1035, wzor :1019-1033)
Postfix BK na `DefaultPartyMoraleModel.GetEffectivePartyMorale`; brama: `TotalManCount > MinimumSoldiersThreshold` (LUDZIE, :1020 - inna niz brama przyrostu).
Dla alkoholu, zwierzecych, tekstyliow, drewna:
`kara = -min( Need / max(dzienna_potrzeba_teraz, 1), Need )`
gdzie dzienna_potrzeba_teraz = `Get*CurrentNeed()` -> ten sam model potrzeb (z czynnikiem "w miescie" i z nasza czapka). Ujemna Need (nadwyzka) daje premie (`+|Need|`, np. "Animal products supplies +2.7").
Skutki wzoru (factor 0.5, bez kwatermistrza):
- poza miastem, partia 80-240 ludzi: kara = dni braku, najwyzej 0.25/0.0125 = **20** (alkohol), 0.3/0.01 = **30** (drewno), 20 (zwierzece). Przy factor 1.0 byloby 10/15/10 - **factor 0.5 polowi zuzycie, ale podwaja maksymalna kare** (wlasciwosc BK).
- **W miescie/zamku dzienna potrzeba = 0 -> dzielnik 1 -> kara = surowa Need**, do 0.25 x ludzi alkoholu + 0.3 x ludzi drewna (160 ludzi: -40 -48 = -88). Morale w grze jest liczone na zywo (game/MobileParty.cs:923-930), wiec partia z narosla potrzeba traci morale w chwili wejscia do twierdzy (takze oblezonej).
- **Partia mala (<80 ludzi alkohol, <100 drewno)**: dzienna < 1 -> dzielnik 1 -> kara = surowa Need. Z zamrozona potrzeba (brama stosow zamknieta) kurczaca sie partia ma kare coraz WIEKSZA wzgledem rozmiaru -> spirala.
- Nasza czapka (BkSupplyTemper.NeedCapPostfix, 2.1): dzienna <= 12/4 = 3 -> dla partii >240 ludzi (alkohol) / >300 (drewno) kara = Need/3 do **ludzi/12** (alkohol) i **ludzi/10** (drewno): 370 ludzi -> -31 / -37; 424 -> -35 / -42.
- Probka z L15 (183d, doby 109017-109019, ta sama partia): `Alcohol supplies -35.3; Wood supplies -33.6; ... Personal +20.1; Base +50.0`, dezercja 10 ludzi/dobe przy morale ~7. -35 nie da sie uzyskac w zwyklym zakresie (max 20) - to jedna z trzech sciezek wzmocnienia: (a) partia >=424 ludzi poza miastem z nasza czapka 3/d, (b) partia w twierdzy (surowa Need), (c) skurczona partia z zamrozona Need (dzielnik 1). 10 dezerterow przy morale 7 (t1 traci wtedy 18%/d) wskazuje raczej na mala lub elitarna partie, czyli (b) albo (c). Nie rozstrzygniete.

### 1.5 Zakupy AI (PartySupplies.cs:318-422)
- `BuyItems()` (:318-332): gdy `EffectiveQuartermaster != null && AutoBuying && CurrentSettlement != null`. EffectiveQuartermaster = lider, gdy brak kwatermistrza (game/MobileParty.cs:810-820), wiec AI zawsze przechodzi. Wolane w ticku dobowym (takze we wsi) i przy wejsciu partii lorda do miasta/zamku.
- Dla kategorii: `BuyItems(Need x DaysOfProvision, kategorie)` (:322-330) - od NAROSLEJ potrzeby razy dni, nie od dziennej.
- **Blad BK (:353-371): od chcianej liczby odejmowany jest zapas OSADY (`CurrentSettlement.ItemRoster`), nie partii.** Gdy miasto ma >= Need x dni sztuk kategorii -> `return`, nic nie kupione. Gdy ma mniej -> kupuje (Need x dni - zapas miasta), najtansze najpierw, ograniczone zapasem i `LeaderHero.Gold / ItemValue` (:393-421). To samo zauwazono juz w ArmyClothing.cs:19-20 (dla tekstyliow).
- Cena z `Town.GetItemPrice` (:403), placi `LeaderHero.ChangeHeroGold(-cena x n)` (:415) - **osada nic nie dostaje: zloto w nicosc, towar z polki znika do jukow** (niezamknieta ekonomia).
- Brak towaru albo zlota: nic sie nie dzieje, potrzeba zostaje (rosnie do klamry), kara trwa.
- Zuzycie: `ConsumeItems(Need x 2, kategorie)` codziennie (:307-315, :424-485), z jukow partii, najtansze pierwsze; zwraca sztuki x PriceMultiplier modyfikatora.
- Drewno: miasta trzymaja 55-72 tys. hardwood (posiadacze "miasta" w liniach Towary), wiec dla drewna blad praktycznie zawsze blokuje zakup (Need x 4 to ~100-500). Alkohol: miasta trzymaja ~3.0-3.6 tys. szt. lacznie (~35 na miasto), wiec zakup jest mozliwy, ale polki sa cienkie; piwo jest zywnoscia (w liniach "beer" pozycja "zywnosc partii" 48-185/d) i lordowie trzymaja go 2.9-6.5 tys. jako jedzenie.

---------------------------------------------------------------------------------------------------

## 2. Nasze latki dotykajace zaopatrzenia BK i odplywu ludzi

### 2.1 BkSupplyTemper.cs (E2C/Armoury/src)
- `PostInitPostfix` (:26-44): DaysOfProvision AI = `WinterBite.SupplyDaysCapNow()` = BkSupplyDaysCap 4, jesienia x AutumnStockMultiplier 2 = 8 (WinterBite.cs:78-90; Settings.cs:144, :364; Armoury.json Jeffa: BkSupplyDaysCap 4, BkSupplyMaxPieces 12). Dziala tylko w PostInitialize (konstruktor albo wczytanie gry), wiec w ciaglym biegu partie z lata zostaja przy 4 przez cala jesien, a po wczytaniu wszystkie maja 8 (L15: `zapasy AI sciete do 8 dni`). Niespojnosc, bez wplywu na morale.
- `NeedCapPostfix` (:54-67): LimitMax(BkSupplyMaxPieces / BkSupplyDaysCap = 3) na KAZDY Calculate*Need (takze ten, ktory BK uzywa jako dzielnika kary morale). Skutek: kara duzych partii rosnie z ich rozmiarem (1.4). Obietnica z komentarza ("CALY zapas nigdy nie przekroczy BkSupplyMaxPieces") NIE jest prawdziwa: BuyItems liczy Need (narosla, do 0.25 x ludzi) x dni, a nie dzienna x dni.
- `ClothZeroPostfix` (:78-92, paczka 150): tekstylia = 0 i ClothNeed zerowana przy kazdym wywolaniu -> kara tekstyliow 0. Bez problemu.
- `ArrowsZeroPostfix` (:102-127), `WeaponsZeroPostfix/ShieldsZeroPostfix` (:137-165), `NeedsBuyPrefix` (:171-197): strzaly, bron, tarcze = 0 (nie wchodza do morale). **Nic nie dotyka alkoholu, drewna ani zwierzecych** poza czapka 3/d.
- Prefiks BuyItems() (partie ponizej progu ludzi) zeruje tylko strzaly/bron/tarcze - nie blokuje zakupu alkoholu ani drewna.

### 2.2 ArmyClothing.cs (paczka 150)
Wlasny zakup odziezy z sakiewek ludzi do kas miast (zamknieta ekonomia), BK tekstylia = 0. Nie rusza alkoholu ani drewna. Jest wzorem dla wariantu W3 (5.).

### 2.3 Budzet rodow 166 (ClanBudget.cs)
- `DesertPrefix/DesertFinalizer` (:904-925): dla rodow z budzetem limit zoldu podniesiony do maksimum na czas `GetTroopsToDesertDueToWageAndPartySize` -> brak dezercji z limitu zoldu; **przepelnienie (NumberOfAllMembers ponad PartySizeLimit -> 25%/d) i zalegly zold zalogi (+min(zdrowi,5)) zostaja** (game: dec/vDesertModel.cs GetTroopsToDesertDueToWageAndPartySize).
- `Releases/ReleaseFrom` (:605-700): nadwyzka zoldu ponad pulap -> zwolnieni do wsi (ludnosc BK). L457: 143-545 ludzi/d ("zwolnieni: do wsi"), to NIE jest dezercja.
- Kiesy: kara BK nie zalezy od zlota (ceny: beer 3 d, wine 14, mead 10, hardwood 4); `biednych (<1000) 1` na krolestwo -> zloto nie jest przeszkoda zakupu.
- "Unpaid Wages" (WarLedger): zalegly zold zabral 379 ludzi przez 459 dob L457 (0.6-1.0/d); linia Zold: z kies zeszlo ~98% naliczonego -> skladnik maly.

### 2.4 Inne nasze moduly przy alkoholu/drewnie
- Drewno: zuzycie w warsztatach zbrojnych (841/d), budowach (480/d), strzelarzach (56/d), naprawach (22/d), gnicie RawNoRot 0.2% (85/d); doplyw: las wsi VillageWoodlot (126) ~2000/d + drwale 308/d (L457 doba 109100). Zapas drewna w miastach rosnie 3.5 tys. -> 71.5 tys. - **nasze moduly nie oprozniaja rynku drewna**; brak drewna w taborach AI to wylacznie blad BuyItems.
- Alkohol: zadna nasza latka nie zmienia produkcji wina/piwa/miodu (grep "wine|beer|mead" w src: tylko HistoricalPrices.cs:44 ceny historyczne, RawPrice.cs:300). Produkcja: piwo 422-1147/d (glownie rzemieslnicy BK grain>beer, z "mnoznikiem BK rzemieslnikow"), miod 116-127/d (wsie lumberjack), wino 24-29/d (wine_press) wobec zuzycia wina przez mieszczan 45-51/d -> wino w miastach spada 417 -> 277 (powoli). Piwo i miod stabilne/rosna.
- NocnyMarsz/NightRest T10 - kara "Sleepless nights" (NightRest.cs:62-63 tablica -25/-40/-95%, MoralePostfix :1244-1262) - patrz 3.3.

---------------------------------------------------------------------------------------------------

## 3. Dowody z logow

### 3.1 Rozklad dezercji (169c + 183 + "Wzrost Innych"), okna 28 dob, L457
| okno | 169c lordowie m+g | ludzie (183) | Inni ~ | Inni straty/d | zalogi limit | lordowie limit | pula: dezercja/d | pula: wighty |
|---|---|---|---|---|---|---|---|---|
| 0-27 | 10 | 1 | 9 | -14 | 0 | 30 | 43 | 19 |
| 84-111 | 28 | 10 | 18 | -22 | 5 | 6 | 51 | 106 |
| 140-167 | 42 | 42 | 0 | -10 | 14 | 11 | 76 | 15 |
| 196-223 | 49 | 49 | 0 | -7 | 16 | 6 | 83 | 12 |
| 252-279 | 86 | 45 | 41 | -36 | 26 | 57 | 178 | 790 |
| 280-307 | 87 | 37 | 50 | -65 | 35 | 16 | 153 | 2362 |
| 336-363 | 121 | 121 | 0 | -12 | 31 | 11 | 168 | 2097 |
| 364-391 | 77 | 71 | 6 | -20 | 47 | 19 | 150 | 1728 |
| 392-419 | 100 | 29 | 71 | -79 | 31 | 14 | 142 | 2039 |
| 420-447 | 270 | 112 | 158 | -161 | 25 | 12 | 312 | 4888 |
| 448-458 | 52 | 38 | 14 | -20 | 32 | 11 | 152 | 5947 |
(L218: 196-218 - 169c 161, ludzie 150, Inni 11; tu wzrost jest ludzki, 41% w partiach glodujacych.)

### 3.2 Zaopatrzenie BK w towarach (L457, srednie z okien; "miasta"/"lordowie" = posiadacze)
| okno | wino miasta/lordowie | piwo m/l | miod m/l | drewno m/l | BK alkohol zuzyl/kupil | lordowie kupili alkoholu (jedzenie) | BK drewno zuzyl/kupil |
|---|---|---|---|---|---|---|---|
| 0-27 | 417/6 | 1578/2253 | 344/33 | 3555/120 | 2/0 | 220 | 0/0 |
| 112-139 | 359/52 | 1364/5606 | 1513/304 | 29987/1093 | 1/0 | 201 | 0/0 |
| 224-251 | 343/62 | 1401/4404 | 1625/694 | 49246/3201 | 1/1 | 191 | 0/0 |
| 336-363 | 309/12 | 1250/3712 | 1616/386 | 64737/1082 | 3/6 | 193 | 1/3 |
| 420-447 | 292/24 | 1219/2910 | 1467/100 | 71511/1232 | 6/11 | 187 | 4/12 |
Wniosek: rynek nie wysycha w 2. polowie (poza powolnym spadkiem wina); BK prawie nic nie kupuje ani nie zuzywa przez caly bieg. Pozycja "zaopatrzenie BK" pojawia sie w liniach Towary tylko 642 razy na 459 dob x 92 towary (beer 181, fish 111, hardwood 105, meat 63, ...), zawsze kilka sztuk.
Partie z narosla potrzeba to mniejszosc (brama stosow), bo inaczej srednie morale nie byloby 71 (L15 183d: `wszystkie partie lordow AI teraz 697: morale srednio 71.3, ponizej 25: 1`). Ale Need jest w zapisie i nie maleje bez towaru, wiec liczba "zarazonych" partii moze tylko rosnac z czasem (nowe partie startuja od 0) - hipoteza zgodna z trendem, nie zmierzona (brak linii o liczbie partii z Need > 0).

### 3.3 Inne skladniki morale
- **Sleepless nights (nasz T10):** linie `NocnyMarsz: swit dnia ... z dlugiem teraz 1/2/3` (L457, srednie okien): dlug1 3-16 partii, dlug2 0.1-2.5, dlug3 0-0.7 z ~650. Najwiecej 280-391 (13-16 / 1.4-2.5). Dlug1 (-25%) zbija typowe 71 do ~53, dlug2 (-40%) do ~43, dopiero dlug3 (-95%) daje dezercje sam. Wzmacniacz przy innych karach, nie glowna przyczyna. W probce 183d: `Sleepless nights -23.4 (u 1)` = dlug 2.
- **Unpaid Wages:** vanilla `HasUnpaidWages x -20` (game/DefaultPartyMoraleModel.cs:43-46, :238-241). Zalegly zold WarLedger 0.6-1.0 ludzi/d; probka 183d `Unpaid Wages -6.7 (u 1)`. Maly.
- **No Food:** vanilla -30 stale (DefaultPartyMoraleModel.cs:38-41) + dzienna -5 do Recent Events (:28-31; BKROT +2 za Saagaela). Ludzie w glodujacych partiach: 24% dezercji z morale w L457 (4 226 z 17 596), 41% w L218 (2 973 z 7 300); epizody glodu L218 doby 145-156 i 191-201 (183 "w glodzie" ~ cala dezercja dnia).
- **Recent Events:** vanilla -3 za przegrana runde symulacji (-1 przy odwrocie) dla kazdej partii strony (game/DefaultBattleRewardModel.cs:401-409, MapEventParty.cs:363-371), spada 10%/dobe (MobileParty.cs:2446), klamra +-100. Probka 183d doba 109022: `No Food -30.0; Recent Events -26.0`. Wzorzec epizodow (np. 807 -> 636 -> 525 -> 394 -> 270 -> 154 w dobach 357-362) pasuje do szoku po bitwie i zaniku, ALE bez 183d w dlugich logach to hipoteza.
- Jak to sie sklada z progiem: nasze prawo (DesertionLaw.cs:73-112) - t1 ponizej 25 (vanilla ponizej 10), 1%/pkt, sufit 25%/d. Partia 50 + Personal ~20 + Valor 5 = ~75; -50 z alkoholu i drewna (poza miastem, 80-240 ludzi) daje ~25 = prog t1; kazda inna kara (sen, glod, przegrana) spycha pod prog.

### 3.4 Czemu skoki ~250-300 i ~420-450
- 252-307: Inni zaczynaja glodowac (straty -36 i -65/d, wighty w puli 790 -> 2362), przepelnienie partii lordow (dzien 254: L_lim 57 w oknie), zalogi dochodza do sufitu (zalogi 86-88 tys. od doby ~250, linia "Zalogi: przyrost bez werbunku (169c)"), epizod ludzki dnia 306 (554).
- 336-391: dwa ludzkie epizody (357-367, 364-367) przy bitwach 7.4 tys. i 3.6 tys. ofiar; prawie bez glodu (183 "w glodzie" 0) i bez Innych.
- 392-447: Inni (straty -79 i -161/d; 169c lordowie w glodzie 89 i 177/d) + epizod ludzki 425 i 445-447 (bitwa 9.7 tys. ofiar w dniu 446).
- Natezenie wojny rosnie przez caly bieg: zabici+ranni 462/d (0-27) -> 1.9-2.8 tys./d (od 112), prawdziwe bitwy 2.0 -> 6.4/d (linie "Bitwy:").
- Zima: brak (patrz 0.1). Jesien od doby ~131 nic nie zmienia w karach (DaysOfProvision tylko przy PostInitialize).

---------------------------------------------------------------------------------------------------

## 4. Zalogi AI - "limit zoldu i wielkosci partii (gra)" 25-47/d

- Zrodlo: vanilla `GetTroopsToDesertDueToWageAndPartySize` (dec/vDesertModel.cs): `nadwyzka = NumberOfAllMembers - PartySizeLimit -> max(1, 25% nadwyzki)`; limit zoldu (wylaczony przez 166 dla rodow z budzetem, ClanBudget.cs:904-918); zaloga z zaleglym zoldem `+min(zdrowi, 5)`. Linia Zold pokazuje garnizony oplacone prawie w calosci (221 419 z 221 649), wiec w praktyce **to przepelnienie**.
- Skad przepelnienie: BK `BKPartyBehavior.AddPatrolBehavior` przy powrocie patrolu do macierzystej twierdzy przenosi CALY sklad patrolu do zalogi bez sprawdzenia limitu (bkfull/BannerKings.Behaviours/BKPartyBehavior.cs:798-818); patrole wracaja 560-685 ludzi/d ("patrole BK wracajace do zalogi"), a w tym czasie zaloga dobiera werbunek notabli (+105), przyrost z modelu (+115), ludzi od lordow (+300-600/d). Od doby ~250 zalogi stoja przy suficie (~88 tys.), wiec kazdy powrot patrolu przepelnia. Zgodne z danymi (G_lim 0-7/d do 140, 26-47/d od 252), ale **nie zmierzylem przepelnienia per twierdza** (brak linii).
- Dokad ida: `CampaignEvents.OnTroopsDeserted` -> `OutlawLaw.OnTroopsDeserted` -> `AddRoster` do puli regionu, bez rozroznienia przyczyny i rodzaju partii (OutlawLaw.cs:330-339, :209-220; subskrypcja ArmouryBehavior.cs:661; takze zdjeci za zalegly zold - WarLedger.cs:132). Czyli **do lasu, nie do domu** - w przeciwienstwie do 166 (`zwolnieni: do wsi`, ClanBudget.cs:605-700) i H3 (rozbici do domu albo w las). To samo zjawisko ("za duzo ludzi na miejsce") ma dwa wyjscia - lamie "jedna zasada na jedno zjawisko".
- Dodatkowo: dezerterzy Innych (wighty) tez ida do puli przez ten sam OnTroopsDeserted (brak filtra Undead), a `OutlawLaw.Draw` (:296-327) bierze z puli kazdy klucz poza "prostymi" - takze wighty (brak filtra, w odroznieniu od RoutedH3 :443-456, gdzie Inni ida "do niczego"). Skladnik puli "Inni (wighty, stare)" rosnie do 5 947 (L457 koniec). Czy bandy faktycznie werbuja wighty - nie sprawdzone w logu.

---------------------------------------------------------------------------------------------------

## 5. Warianty naprawy (zasady: zamknieta ekonomia, jedna regula gracz/AI, zachowanie z biezacego zysku, 100% przypadkow, "naprawiamy morale, nie dezercje")

### W1. Naprawa wzoru kary zaopatrzenia BK (dzielnik i zamrozona potrzeba) - tylko morale
- Co: postfix na `PartySupplies.GetAlcoholCurrentNeed / GetWoodCurrentNeed / GetAnimalProductsCurrentNeed / GetTextileCurrentNeed(bool)` - zwraca "polowa" dzienna potrzebe (ludzie x stawka x PartySuppliesFactor, bez czynnika "w miescie" i bez naszej czapki 3/d, minimum 1). Te metody czyta tylko kara morale BK (VanillaModelTweakPatches.cs:1022-1032) i dialog kwatermistrza gracza (BKPartyNeedsBehavior.cs:144-157); Tick liczy przyrost wprost z modelu (PartySupplies.cs:260-303), wiec przyrost i zakupy bez zmian. Plus prefiks na `PartySupplies.Tick`: klamra zapisanych AlcoholNeed/WoodNeed/AnimalProductsNeed do +-(0.25/0.3/0.25) x dzisiejszych ludzi dla kazdej partii (takze przy zamknietej bramie stosow). Opcjonalnie suwak MCM "najwieksza kara za kategorie" (decyzja Jeffa: BK przy factor 0.5 daje 20/30/20).
- Pliki: Armoury/src/BkSupplyTemper.cs (+ApplyAll, linia logu), ewentualnie Settings.cs + tools/gen_mcm.py.
- Skutek: kara = dni braku, najwyzej 20/30/20 wszedzie - koniec skokow w twierdzach (surowa Need), koniec kary rosnacej z rozmiarem partii (nasza czapka), koniec spirali kurczacej sie partii. Gracz tak samo (jedna regula).
- Ryzyko: niskie; zero zlota i towaru. Sprawdzic: 183d ma pokazywac `Alcohol supplies` <= 20 i `Wood supplies` <= 30; dialog kwatermistrza w miescie pokaze potrzebe polowa zamiast 0; NeedCapPostfix dalej ogranicza przyrost (3/d) - kolizji nie ma, bo nasz postfix na Get* liczy od zera, nie od wyniku modelu. Nie naprawia braku drewna w taborach (kara 30 za drewno nadal mozliwa u partii z narosla potrzeba).

### W2. Zakupy BK wedlug zapasu partii, zaplata do kasy osady
- Co: prefiks zastepujacy `PartySupplies.BuyItems(float, List<ItemCategory>)`: chciane = floor(Need x dni) - zapas PARTII w kategorii, z sufitem BkSupplyMaxPieces na kategorie i wizyte (spelnia obietnice z komentarza BkSupplyTemper); kupno z polki osady od najtanszej, po cenie targu; placi lider (albo sakiewka ludzi jak w 150) -> `Town.ChangeGold` (wies: kasa wsi); ksiegowanie w MoneyLedger jako przeplyw, nie ujscie.
- Pliki: Armoury/src/BkSupplyTemper.cs; sprawdzic GoodsLedger.cs:1112-1119 (ramka "zaopatrzenie BK (zakupy)" na tej samej metodzie - kolejnosc prefiksow: ramka GoodsLedger ma Priority.First - GoodsLedger.cs:1088 - wiec otworzy sie przed naszym prefiksem; to trzeba utrzymac), MoneyLedger/CirculationWindows (nowa pozycja).
- Skutek: drewno dostepne od reki (miasta 55-72 tys.), alkohol tam, gdzie jest; zloto przestaje znikac (dzis "zaopatrzenie BK" placi w nicosc). Kara spada u partii, ktore odwiedzaja miasta.
- Ryzyko: srednie. Polki alkoholu sa cienkie (~35 szt./miasto, wino ~3) - jesli partii z potrzeba jest duzo, AI wykupi alkohol mieszczanom (budzet konsumpcji BK, ceny). Gracz z AutoBuying dostaje ta sama regule. Potrzebny pomiar "Towary" wina/piwa/miodu w miastach po zmianie.

### W3. Napitek i opal wojska wedlug wzoru 150 (zastepuje alkohol i drewno BK)
- Co: BK alkohol i drewno = 0 dla wszystkich partii (jak tekstylia 150, strzaly 172, bron 174) z wyzerowaniem zapisanych potrzeb; wlasna ksiega potrzeby na zolnierza (partie rodow, garnizony, gracz), zakup przy wyjezdzie z miasta z sakiewki ludzi do kasy miasta (MenPurse.OnLeft, jak ArmyClothing), garnizon miasta z wlasnej polki; kara morale z wlasnego licznika braku, ograniczona.
- Pliki: nowy modul (wzor ArmyClothing.cs), BkSupplyTemper.cs (zerowanie), Settings.cs + gen_mcm.py, MoneyLedger/GoodsLedger.
- Skutek: zamknieta ekonomia, 100% ludzi, kara z prawdziwej dostepnosci.
- Ryzyko: najwieksza praca. Stawka musi wynikac z podazy: przy stawkach BK (0.0125 alkoholu/czl./d) ~110 tys. ludzi partii = ~1 375/d (z garnizonami ~2 500/d) wobec produkcji ~790/d (piwo ~635, miod ~127, wino ~29) i zuzycia mieszczan ~450/d - brak dla wszystkich = kara dla wszystkich = wiecej dezercji. Najpierw pomiar i decyzja o stawce.

### W4. Wlasciwe ujscie ludzi + czysty pomiar (zalogi, Inni)
- Co: (a) Measure169c.OnTroopsDeserted (:439-454) - Inni (Undead.Party) osobna grupa, nie "partie lordow AI"; prog 183 liczony bez Innych (sam log). (b) OutlawLaw.OnTroopsDeserted - partia/postac Innych do niczego (jak RoutedH3 :443-456); OutlawLaw.Draw - pomijac klucze Innych. (c) Przepelnienie zalog (i partii) rodow z budzetem: prefiks na `GetTroopsToDesertDueToWageAndPartySize` - nadwyzka ponad PartySizeLimit do domu przez ClanBudget.ReleaseFrom (ta sama regula co "zwolnieni: do wsi"), wiec gra nie ma juz czego zdejmowac; alternatywnie ograniczyc powrot patrolu BK (BKPartyBehavior.AddPatrolBehavior) do wolnego miejsca, reszta do domu.
- Pliki: Armoury/src/Measure169c.cs, OutlawLaw.cs, ClanBudget.cs (lub nowa latka na BK AddPatrolBehavior).
- Skutek: odplyw do puli w 2. polowie mniejszy o ~25-47 (zalogi) + ~30-160 (Inni) ludzi/d; pomiar pokazuje prawdziwa dezercje ludzi.
- Ryzyko: nisko-srednie. (a) sam log. (b) zmienia sklad puli (wighty juz w puli wygasaja "do niczego"). (c) ksiegowanie ludnosci BK i sakiewek przy zwolnieniu, zgodnosc z H3 i z 166 (zalogi w wojnie "pelne" - GarFull).

### Rekomendacja
1. **W1 jako pierwsza, pojedyncza zmiana** - jedyne skladniki morale, ktore sa bledami (BK + nasza czapka), a nie zamierzonymi karami; niskie ryzyko, jedna regula, wprost "naprawiamy morale". Razem z nia (bez skutku w grze) W4(a) - rozdzielenie Innych w 169c/183, inaczej prog 183 mierzy wighty.
2. Potem nowy dlugi bieg (>=300 dob) z obecnym 183d, zeby ustalic sklad epizodow (Recent Events po bitwach vs glod vs zaopatrzenie) - dlugie logi go nie maja.
3. W2 (zamyka zloto BK i daje drewno), potem W4(b)(c).
4. W3 dopiero po pomiarze podazy alkoholu i decyzji Jeffa o stawce.
Do decyzji Jeffa: czy maksymalna kara 20/30/20 (BK factor 0.5) jest zamierzona; czy epizody po wielkich bitwach (vanilla -3/runde przy naszym progu t1 25 zamiast 10) to cecha, czy do zmiany.

---------------------------------------------------------------------------------------------------

## 6. Czego NIE sprawdzilem
- Ile partii AI ma Need > 0 (brak linii w logach; mozna dopisac licznik do 183d).
- Sklad morale w epizodach dlugich biegow (brak 183d w L457/L218).
- Przepelnienie per twierdza i udzial zaleglego zoldu zalog w G_lim.
- Czy bandy faktycznie werbuja wighty z puli.
- Dlaczego partie Innych glodują (ROT).
- Liczba rund w autobitwach i realna wielkosc Recent Events po bitwach.
