# PACZKA 177 - STAL VALYRIANSKA: STAL ZAMKOWA, SKONCZONY ZASOB, PRZEKUWANIE W QOHORZE (projekt, 09.10.2026)

Status: WDROZONE w drzewie `SCR\noc2\sv` (galaz `w-toku/177-stal-valyrianska` od `noc/sklad6` 0c41eee): commity 177-1 (7fde4a6), 177-2 (abd1ac0),
177-3 (a47eef3); build Release kod 0. Gra i autotest nie uruchamiane, folder gry nietkniety, nic nie wgrane, nic nie wypchniete.
**Po krytyce (28 uwag, 4 krytyczne) projekt poprawiony - rozdz. 11 "Krytyka i odpowiedzi"; zmienione miejsca oznaczone [po krytyce].**

SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`.
Sciezki `[K]` bez przedrostka: `SCR\noc2\sv\Armoury\src\...` (stan 0c41eee). [BK] = `...\3cf3e0ac...\scratchpad\ore-supply\bk`, [GRA-DEK] =
`...\3cf3e0ac...\scratchpad\ore-supply\cs`, [DTE] = `SCR\dte\DynamicTroopEquipmentReupload`, [CORE] = `...\3cf3e0ac...\scratchpad\dzien-6\autotest\dec\TaleWorlds.Core\TaleWorlds.Core`,
[GRA] = `...\Mount & Blade II Bannerlord\Modules`. Rozpoznanie: `SCR\a177\dane.md` (dane i lancuch) i `SCR\a177\przekuwanie.md` (kanon, Qohor).

Oznaczenia: **[K]** przeczytane w kodzie, **[D]** dane XML / items-dump, **[P]** pomiar z logu autotestu 120 dob (08.10 22:23, `SCR\kopiaT9-120`), **[S]** moj
szacunek / projekt, **[H]** kanon (AWOIAF, rozdzialy ksiazek - przez rozpoznanie).

Decyzja Jeffa (wiazaca), STAN-PRAC :831-834, 09.10 ok. 04:05, pkt **24a - TAK**: (1) material kuzni 6. poziomu (ironIngot6) = "castle-forged steel" ze zwyklego
lancucha, tier 6 w kuzni i w warsztatach zostaje; (2) prawdziwa stal valyrianska tylko w istniejacych legendach ROT, nikt nie robi nowej; (3) jedyna droga do
nowego miecza valyrianskiego to PRZEKUCIE istniejacej stali u mistrza w Qohorze (oplata do kowala/miasta, kilka dni, wielki miecz moze dac dwa mniejsze);
(4) sztaby z niczego (wedrowcy BK) - zamknac. Zasady: nic z niczego, jedna regula dla gracza i AI, teksty w grze po angielsku.

---

## 0. Dla Jeffa - prostym jezykiem

[po krytyce] Najwyzsza stal w kuzni nazywa sie teraz **Castle-forged Steel** (stal z zamkowych kuzni) i robisz ja jak kazda inna stal - z Fine Steel,
bez dawnej podwojnej ceny; sztaba kosztuje ok. 6 pensow zamiast 100, bo to juz nie skarb. Wedrowni rzemieslnicy z Banner Kings nie przynosza juz sztab
ani wegla znikad (dzis ok. 43 "valyrianskie" sztaby dziennie) - reszta ich ladunku zostaje jak byla; sztaby beda w swiecie tylko z wytopu, a to, co juz
lezy w Twoim zapisie, zostaje jako zwykla stal zamkowa. Prawdziwa stal valyrianska to tylko istniejace miecze ROT (29 wzorow: Lod, Dlugi Pazur, Jasny Ryk,
Mroczna Siostra, Wierny Przysiedze, seryjne "Valyrian Steel Sword" i kilka innych) - tyle sztuk, ile bylo na starcie swiata (Vigilance jedna, u glowy
Hightowerow). Zwykla kuznia ich nie przetopi. Kazda kopia, ktora gdzies sie pojawi (karawana DTE, nagroda turniejowa, lup), staje sie zwyklym dobrym mieczem
tego samego rodzaju - takze u Ciebie. Gdy wlasciciel umrze poza walka, miecz bierze kat (jak Lod - Lannisterowie) albo dziedzic; lord przebrany przez gre
przy zmianie wladcy zostaje z mieczem; rozbita partia oddaje go zwyciezcy. Miecz jest wart ok. 60 tys. za miare (Lod 120 tys.) - kupic go z targu moze tylko
bardzo bogaty lord. Nowy miecz valyrianski dostaniesz tylko w Qohorze: przynosisz ostrze w taborze, placisz cechowi mistrzow (miecz zwykly ok. 4 070 pensow,
wielki miecz - jak Lod - ok. 7 820; zloto idzie do kasy Qohoru) i czekasz 7 dni pracy w miescie albo wracasz pozniej. Z wielkiego miecza dwurecznego
(Lod, Heartsbane, Blackfyre, Jasny Ryk) mistrzowie zrobia dwa miecze (jak Tobho Mott z Lodu), ze zwyklego - jeden w innym ksztalcie; stali nie przybywa
ani nie ubywa, a zbroi z miecza sie nie zrobi. Ta sama regula dla lordow: lord, ktory stoi w Qohorze z wielkim mieczem zdobytym od innego rodu i ma pieniadze,
moze kazac go rozdzielic na dwa (jak Tywin z Lodem). Przy okazji jedna regula tieru 6: warsztaty miast licza bron i zbroje tieru 6 ze stali zamkowej, jak
Twoja kuznia - beda o ok. 4-8% drozsze.

---

## 1. Sprostowania do zadania i rozpoznania (wazne dla wdrozenia)

1. **Nazwy "Valyrian Steel" nie nadaje ROT, tylko nasz Armoury** [K]: `ValyrianSteel.cs:34` (wolane z `ArmouryBehavior.cs:1047`). Gra ma "Thamaskene Steel"
   ([GRA-DEK] `DefaultItems.cs:133`, napis `vLVAfcta`); ROT, BK i inne mody tej nazwy nie ruszaja (rozpoznanie: grep XML i skan UTF-16 DLL). Zmiana nazwy to nasz
   kod, bez zadnej latki na pliki ROT.
2. **Wycena klng valyrianskich NIE idzie z ceny sztaby Iron6** [K] (punkt K3 rozpoznania "przekuwanie" jest nieaktualny): `HistoricalPrices.HistCost`
   (`HistoricalPrices.cs:245-257`) bierze cene metalu z gatunku tieru `ArmsPricing.GradeFor` (`ArmsPricing.cs:96-104`), a ten dla t6 daje **Iron5** (Fine Steel).
   Lod 1 377 d, miecz 1H ok. 770 d (items-dump 09.10) to koszt wykonania x `HistUniquePrestige` 4. Zmiana ceny sztaby ich nie rusza.
3. **Przetop klingi valyrianskiej liczy gra/BK, nie `Recipes.SmeltYield`** [K]: klingi ROT maja `WeaponDesign`, wiec `SmeltTab.DoSmeltingPrefix` (`SmeltTab.cs:119-124`)
   je przepuszcza do wanilii; wynik z czesci, uciety przez BK `GetMetalMax` (Lod 3 sztaby Iron6, wiekszosc 2 [S, rozpoznanie dane 2.3]).
4. **"Miasto placi za ladunek wedrowcow i dostaje kopie" (audyt 03, L6) to martwy kod** [K+P]: jedyne wywolanie `CreateTravellerParty` ([BK]
   `BKPartyBehavior.cs:1043`) nie podaje `trading`, log przez 119 dob: "konwoje ludnosci 0". Ladunek ginie z partia (`BKPartyBehavior.cs:938`).
5. **Odkrycie czesci legendy po przetopie (Jeff 29.08) trwa tylko do ponownego wczytania** [K]: `SmeltTab.DoSmeltingPostfix` (`SmeltTab.cs:175-205`) odkrywa czesci
   w pamieci, a `LegendaryLaw.LockLegendPieces` (`LegendaryLaw.cs:92-126`, wolane przy kazdym starcie sesji `:80`) chowa je znowu. Dla stali valyrianskiej i tak
   zamykamy przetop (rozdz. 4.6); dla pozostalych legend to osobny blad - poza 177 (rozdz. 6, D3).

---

## 2. Material 6: "Castle-forged Steel" (paczka 177-1)

### 2.1 Co zobaczy gracz (napisy po angielsku)

| Miejsce | Dzis | Po 177 |
|---|---|---|
| Sztaba (targ, tabor, kuznia, przetop) | "Valyrian Steel" (nasza nazwa), 100 d | **"Castle-forged Steel"** (l. mn. "bars of castle-forged steel"), **6 d** |
| Podpowiedz sztaby (tooltip) | - | "The finest steel of the castle forges - good steel, but not Valyrian." |
| Perk Steel Maker 3 | "...refine two units of fine steel into one unit of Thamaskene steel..." (wanilia, i tak niezgodne z naszymi przepisami) | "You can refine fine steel into castle-forged steel, the finest steel the castle forges make." |
| Nazwy z "Thamaskene" (12 przedmiotow, np. "Thamaskene Steel Spathion", "Thamaskene Pike", strzaly "Thamaskene Steel Hardened Barbed Arrows"; ok. 6 czesci kuzni, np. "Thamaskene Steel Spatha Blade") [D] | "Thamaskene..." | "Castle-forged Steel ..." / "Castle-forged ..." (slowo "Thamaskene" nic nie znaczy w Westeros) |
| MCM | "Valyrian Steel Value", "Hist Valyrian Per Kg" | "Castle-forged Steel Value", "Hist Castle Steel Per Kg" (opisy w 2.2) |

Wszystko latkami przy wczytaniu (bez plikow danych): nazwa przez setter `ItemObject.Name` (jak dzis `ValyrianSteel.cs:32-34`), opis perku przez prywatny
setter `PerkObject.PrimaryDescription` ([GRA-DEK] `PerkObject.cs:35`; tekst perku `DefaultPerks.cs:2122`), nazwy czesci przez prywatny setter
`CraftingPiece.Name` ([CORE] `CraftingPiece.cs:36`), podpowiedz - postfiks na `TooltipRefresherCollection.RefreshItemTooltip` (ten sam cel co
`TooltipCondition.cs:14`, osobny postfiks). Straznik `if (cur == "Valyrian Steel") return` (`ValyrianSteel.cs:31`) dostaje nowa nazwe. Przedmiot sztaby powstaje przy kazdym wczytaniu z kodu gry
("Thamaskene Steel"), jego nazwa nie siedzi w zapisie - zapisy sprzed 177 dostaja nowa nazwe od razu.

### 2.2 Lancuch: zwykly stopien jak kazdy inny

- **Zdejmujemy `ValyrianSteel.DearRefine`** (`ValyrianSteel.cs:40-67`; dzis podwaja wsad formuly z wyjsciem Iron6 - "valyrianska ma byc trudna"). Zostaje
  przepis z `MaterialLaw.RefinePostfix` (`MaterialLaw.cs:124`, ceny historyczne): **5 Fine Steel + 2 wegla -> 4 Castle-forged Steel** (perk Steel Maker 3),
  czyli ta sama strata 20% metalu co na kazdym stopniu (Jeff 04.10), tylko podwojny wegiel (najdluzsze grzanie). Bez cen historycznych `:133` (5 + 5 -> 4).
- **Przetop pancerzy (`Recipes.SmeltYield`, `Recipes.cs:597-620`)**: zdejmujemy dodatkowe "/2" dla Iron6 (`:603`, `:614`) - stal zamkowa wraca z przetopu
  jak kazda inna (najwyzej polowa receptury).
- Koszt z lancucha [S, z przepisow `MaterialLaw.cs:118-124`]: 1 sztaba (0.5 kg) = 1.25 Fine = ... = 3.05 surowki = ok. **10 kg rudy** i ok. **4.3 wegla =
  ok. 110 kg drewna**; w cenach historycznych ruda ok. 0.8 d + drewno ok. 3.8 d = **ok. 4.6 d surowca** + praca.
- **Cena**: `HistValyrianPerKg` 200 -> **`HistCastleSteelPerKg` = 12 d/kg = 6 d za sztabe** (Fine Steel 8 d/kg = 4 d; +25% metalu i podwojny wegiel daja ok.
  10.4 d/kg materialu, reszta to dluzsze grzanie). Bez cen historycznych: `ValyrianSteelValue` 1000 -> **`CastleSteelValue` = 375** (ten sam krok x1.33 co
  w calym szeregu 87-118-157-210-281, `Settings.cs:373-377`).
- Klucze MCM `ValyrianSteelValue` i `HistValyrianPerKg` NIE sa w `Armoury.json` Jeffa (sprawdzone 09.10: 331 kluczy, zadnego "Valyrian") - zmiana nazwy pola
  nie trafi na pulapke "MCM ma zapisana wartosc" (CLAUDE.md rozdz. 7). Po zmianie `Settings.cs`: `python tools/gen_mcm.py`.
- Opisy MCM: "Castle-forged Steel Value - worth of one bar of castle-forged steel (0.5 kg), the last grade of the ordinary chain"; "Hist Castle Steel Per Kg -
  castle-forged steel, pence per kg (fine steel plus a fifth of the metal lost and double charcoal)".
- XP za rafinacje liczy `MaterialLaw.RefineXpPostfix` od ceny SPRZED prawa (260) - bez zmian.

### 2.3 Jedna regula tieru 6 (gracz = warsztaty miast)

Dzis dwie reguly [K]: kuznia gracza `Recipes.IronForTier` t6 -> **Iron6** (`Recipes.cs:437-443`), warsztaty i wyceny `ArmsPricing.GradeFor` t6 -> **Iron5**
z komentarzem "t6 w ROT to nie stal valyrianska" (`ArmsPricing.cs:96-104`). Po 24a ten powod znika, wiec **`GradeFor` t6 -> Iron6** (wylacznik `Tier6CastleSteel`).
Skutki [S, z `ArmsPricing.Compute` :165-232 i `HistoricalPrices.HistCost` :245-257]:
- cena t6: metal 8 -> 12 d/kg; miecz 1H t6 (1.3 kg, 1.55 kg metalu, 12 dni pracy) ok. **+4%**, zbroja plytowa t6 (25 kg) ok. **+8%**; t1-t5 bez zmian;
- warsztaty zbrojne miast (`WorkshopLaw.Needs`, `WorkshopLaw.cs:157-170`, `StepsOf` :128-139): ruda na sztuke t6 x1.25 (5 stopni zamiast 4);
- naprawy u kowali (`MendMaterial.cs:111`, `:482`): surowka na naprawe t6 x1.25 (ta sama regula gatunku).
Klingi valyrianskie (tier 6) podrozeja razem z innymi t6 o ok. 4% - to wynik reguly, nie osobna wycena.

---

## 3. Skad stal zamkowa w swiecie po zmianie (paczka 177-1)

### 3.1 Dzis [P, autotest 120 dob, linie "Towary: dzien" - `GoodsLedger.cs:927`]

| Towar | Produkcja/d (jedyne zrodlo: wedrowcy BK) | Ujscie/d | Zapas swiata d0 -> d119 (w miastach d119) |
|---|---|---|---|
| ironIngot6 | **43.2** (5 141 w 119 dob) | 40.6 przepada z partiami, 0.34 smithy, 0.12 rzemieslnicy BK | 1 -> 246 (57) |
| ironIngot1-5 | 51.4 / 50.8 / 27.7 / 19.5 / 16.1 | prawie wszystko przepada z partiami | 47-11 -> 216-66 (2-10 w miastach) |
| wegiel | 52.7 | 50.9 przepada | 44 -> 229 (5) |

Mechanizm [K]: `PopulationPartyComponent.CreateTravellerParty` ([BK] `PopulationPartyComponent.cs:214`) wola `GiveItems` (`:288`, cialo `:316-405`). Rzemieslnik
losuje w kazdym obrocie 1 material z `Materials` (`:84-97`: wegiel, Iron1-6, ruda, drewno) z waga **Value x losowa** (`:335-342`) - przy cenie 100 d ok. 74%
losowan to Iron6 [S, symulacja rozpoznania]; kazdy wedrowiec dobiera tez towary z `Items.AllTradeGoods` z waga 1/Value, a sztaby i wegiel to `Goods`
(stad tanie Iron1/Iron2/wegiel). Kopalnie i warsztaty sztab nie daja (`WorkshopLaw.RandomItemPostfix`, `WorkshopLaw.cs:1388-1406`), targi na starcie maja 0
sztab [P, linia "Rynek surowcow", doba 108836].

### 3.2 Zamkniecie: wedrowcy bez sztab i wegla

- **Postfiks na `PopulationPartyComponent.CreateTravellerParty`** (public static, zwraca `MobileParty`; typ przez `AccessTools.TypeByName("BannerKings.Components.
  PopulationPartyComponent")`, jak juz robi `CirculationWindows.cs:1774`): po `GiveItems` zdjac z `__result.ItemRoster` wszystkie sztuki **ironIngot1-6 i wegla**
  (oba losowania: materialow rzemieslnika i towarow kazdego wedrowca). Wylacznik `TravellersNoIngots` (domyslnie wlaczony).
- Dlaczego postfiks, a nie prefiks/transpiler na `GiveItems`: `GiveItems` jest `protected static` z petla wazona; podmiana petli = kopia kodu BK (pulapka przy
  aktualizacji BK). [po krytyce] Budzet ladunku BK to suma WARTOSCI, a losowanie materialu jest wazone wartoscia - sama tansza sztaba (6 d zamiast
  100 d) wydluzylaby petle i wedrowcy nieslibyby wiecej rudy, drewna i towarow z niczego. Dlatego na czas `GiveItems` sztaba 6 ma dawna wartosc (pole
  `<Value>k__BackingField`, bez settera i blokady cen; zdjecie sztab jeszcze przy niej - `ItemRoster.TotalValue` bez przesuniecia), a przelicznik popytu
  kategorii "iron" liczy sztabe 6 po dawnej cenie (`CastleSteel.RatioValue`). Reszta ladunku i popyt miast - dokladnie jak przed 177.
- Linia dnia: `Wedrowcy BK: dzien N - nowych partii X, zdjeto z ladunku sztab Y (w tym Castle-forged Z) i wegla W (sztaby tylko z wytopu)`.
- **Zostaje poza 177** (swiadomie, to etap 3.1 / 164a i audyt 03 P1-C): ruda, drewno i towary handlowe wedrowcow (len, welna, skora, narzedzia...) dalej
  rodza sie z niczego i gina z partia. 177 zamyka 100% sztab i wegla - innego zrodla sztab w swiecie nie ma (3.1).
- Skutki dla innych: nikt nie traci - ladunek wedrowca i tak ginal z partia (`BKPartyBehavior.cs:938`); do miast trafialy tylko resztki przez lup
  rozbitych wedrowcow.

### 3.3 Po zmianie stal zamkowa bierze sie tylko z lancucha

- **Kuznia gracza** (i jego towarzyszy): rafinacja z rudy (2.2) - jedyne zrodlo sztab. AI nie rafinuje (jak dzis).
- **Przetop** zwyklych mieczy t5/t6 z klinga Iron6 (wanilia/RBM "Thamaskene ...", 37-39 klng [D]) - to byla stal zamkowa, wraca jako stal zamkowa.
- **Warsztaty miast (174)** robia t6 prosto z rudy z gatunkiem Iron6 (2.3) - sztaby jako towar nie powstaja (jak dzis).
- **Zuzycie**: wanilijny `smithy` i rzemieslnicy BK biora kategorie "iron" (ruda i sztaby) - beda brac rude, jak w 95% juz dzis; naprawy u kowali braly sztaby
  z polki tylko, gdy lezaly (2-10 sztuk w miastach [P]), dalej biora rude + dymarke (`MendMaterial.Pick`).

### 3.4 Zapas startowy i zapis Jeffa

- **Nowa kampania**: targi startuja z 0 sztab [P] - nic do zrobienia; wedrowcy od pierwszego dnia bez sztab.
- **Zapis Jeffa**: sztaby, ktore juz leza (miasta ok. 57-104 Iron6, zamki ok. 13, partie ok. 150-250 [P, rok: audyt 03 :123]), **zostaja** - to ten sam przedmiot,
  od wczytania nazywa sie Castle-forged Steel i kosztuje 6 d. Kasowanie byloby "w nicosc"; zuzyja je smithy, naprawy i gracz. Wedrowcy juz w drodze w chwili
  wczytania koncza jak dotad (ladunek ginie z partia w ciagu kilku dni). Sztaby gracza zostaja, tanieja na papierze 100 -> 6 d.

---

## 4. Prawdziwa stal valyrianska: skonczony zasob (paczka 177-2)

### 4.1 Spis (jedna lista w kodzie `ValyrianBlades`; [po krytyce] 29 wzorow - bez Euron's Axe)

Kryterium [D]: bron ROT z klinga/glowica z materialu Iron6 i w zbiorze legend (ROT-UNIKATY.md), minus kanoniczne wyjatki (4.2). [po krytyce] **Miara** = ile
stali niesie KLINGA (dlugosc kawalka Blade z `ROT_crafting_pieces.xml`, skala 100): miecz dwureczny z klinga >= 105 cm (`ValyrianGreatswordMinCm`) =
**2 miary** - Lod 140.3, Heartsbane 114.5, Blackfyre 111, Brightroar i Brightroar Silver 108; reszta = **1 miara** - takze Longclaw 103 (w ksiazkach
poltorak), Despair 100, Orphanmaker 96.89, Nightfall 92 i type 8 (89.7 - ta sama klinga `valsteelblade_2` co type 2-4). Dark Sister ma 109 cm, ale jest
jednoreczna. Kanon "z wielkiego miecza dwa mniejsze" zgadza sie z miarami; wagi ROT nie (Lod 1.73 kg -> 1.30 + 1.40 kg), wiec liczymy miarami.

| id | nazwa w grze | miary | kanon | kto ma na starcie [D/P, rozpoznanie dane 4.1] | wartosc dzis [D] |
|---|---|---|---|---|---|
| `ice_sword` | Ice | 2 | tak (Stark) | Robb Stark | 1 377 |
| `longclaw_sword` | Longclaw | 1 | tak (Mormont; poltorak) | Jon Snow | 886 |
| `brightroar` | Brightroar | 2 | ROT (w kanonie zaginiony w Valyrii) | Tywin Lannister | 1 311 |
| `brightroar2` | Brightroar Silver | 2 | wariant ROT | nikt | 1 311 |
| `blackfyre` | Blackfyre | 2 | tak | Aegon Targaryen | 883 |
| `heartsbane` | Heartsbane | 2 | tak (Tarly) | Randyll Tarly | 866 |
| `nightfall` | Nightfall | 1 | tak (Harlaw) | Harras Harlaw | 863 |
| `whyt_sword` | Orphanmaker | 1 | tak (Roxton) | nikt (ROT: merch) | 851 |
| `assist_sword` | Despair | 1 | ROT | notabl "valyrian_thief" (quest) | 888 |
| `val_steel_sword_8` | Valyrian Steel Sword type 8 | 1 | seryjny ROT | Corvan Hale | 1 157 |
| `oathkeeper_sword` | Oathkeeper | 1 | tak (z Lodu) | Brienne of Tarth | 795 |
| `ww2_sword` | Widow's Wail | 1 | tak (z Lodu) | Joffrey | 767 |
| `darksister` | Dark Sister | 1 | tak | Daenerys | 794 |
| `lady_forlorn2` | Lady Forlorn | 1 | tak (Corbray) | Lyn Corbray | 787 |
| `lady_forlorn` | Tempest | 1 | ROT | nikt | 782 |
| `lamentation` | Lamentation | 1 | tak (Royce) | Bronze Yohn Royce | 774 |
| `red_rain` | Red Rain | 1 | tak (Drumm) | Dunstan Drumm | 782 |
| `vigilance_sword` | Vigilance | 1 | tak (Hightower) | 3 osoby (Baelor, Gunthor, Garth Hightower - dane ROT) | 783 |
| `truth` | Truth | 1 | ROT (kanon niepewny) | Tregar Ormollen | 782 |
| `celtigar_axe` | Crab's Pincer | 1 | ROT (kanon niepewny) | Ardrian Celtigar | 611 |
| ~~`euron_axe`~~ | Euron's Axe | - | [po krytyce] POZA spisem: ani ksiazki, ani ROT nie nazywaja go valyrianskim (glowica `euron_head` tier 4); zostaje legenda | Euron Greyjoy | 613 |
| `koa_sword_tier_5`, `val_steel_sword_2`..`_7`, `val_steel_sword_blue`, `val_steel_sword_red` | Valyrian Steel Sword type 1-7, Blue, Red | 1 | seryjne ROT | type 4: Bedir Khan; reszta nikt | 742-774 |

[po krytyce] Spis: 29 wzorow (19 nazwanych + 10 seryjnych). Na starcie swiata [P, kronika testu 120 dob, zestaw bojowy]: 17 wzorow, 19 klng; po nowych
miarach, bez Euron's Axe i z jedna Vigilance ok. 17-18 klng i 21-22 miary [S] - dokladnie linia startowa "Stal valyrianska: pierwsze wczytanie z 177".
[po krytyce] Vigilance u trzech Hightowerow (jeden szablon `rea_bat_template_hightower` dla trzech lordow) to dwie kopie z niczego: nazwana legenda
jest najwyzej JEDNA (Jeff 30.08 "jedna na swiecie") - zostaje u glowy rodu, dwie pozostale staja sie zwykla stala (straznik 4.5).

### 4.2 Iron6, ale NIE stal valyrianska (zostaje jak jest)

- **Liontooth** (`widows_wail` - kanon: pierwszy, zwykly miecz Joffreya; ROT dal mu Iron6 x3) - to teraz zgodnie z kanonem stal zamkowa; zostaje legenda
  (decyzja 29.08 o przetopie legend nadal go dotyczy).
- Dawn (stal z gwiazdy), Needle (Mikken z Winterfell - wlasnie "castle-forged"), Lightbringer - bez Iron6 albo nie valyrianskie.
- Lod Innych: `ice_sword2`, `ice_spear`, `nightking_blade` (Iron6 x10-20 jako "lod") - sprzet umarlych (osobne prawo `IsDeadGear`), nie stal valyrianska.
- Mloty `baratheon_hammer`, `gendry_hammer` (1 Iron6 + 3 Iron5) i ok. 37-39 zwyklych klng wanilii/RBM t5 z Iron6 - stal zamkowa.

### 4.3 Oznaczenie

- **Jedna lista w kodzie**: nowy `ValyrianBlades` (id -> miary). Czytaja ja: zbior legend (`LegendaryLaw.BuildLegendSet` `:294-312` - dochodza `celtigar_axe`
  i ewentualne inne spoza progu 100k, wiec `LockLegendPieces` schowa tez `celtigar_head`), blokada przetopu (4.6), czystki (4.4), dziedziczenie (4.4), spis (4.5),
  filtr DTE (4.4), Qohor (rozdz. 5). Ta sama lista co `RotUniques` (`docs/ROT-UNIKATY.md`), wiec `ArmsPricing.IsUnique` (`ArmsPricing.cs:79-87`) juz je widzi.
- **W grze**: linia w podpowiedzi przedmiotu (ten sam postfiks co 2.1): "Valyrian steel - one measure / two measures. No smith alive can make more; only the
  masters of Qohor can reforge it." Nazw przedmiotow nie zmieniamy (kanon ROT; seryjne i tak maja "Valyrian" w nazwie).

### 4.4 Nic nowego z niczego, nic w nicosc - kazde wejscie i wyjscie

| Droga | Dzis [K/P] | Po 177 |
|---|---|---|
| Sklepy (zaopatrzenie) | `LegendaryLaw.SweepWorld` daje NotMerchandise przy kazdym wczytaniu (`LegendaryLaw.cs:139-143`) | bez zmian |
| Nagrody turniejowe | [po krytyce] wanilia bierze tylko `!NotMerchandise`, ale TournamentsXPanded (wlaczony u Jeffa) ma prefiks na `GetTournamentPrize` bez filtra NotMerchandise i legend (pula elitarna = kazdy przedmiot t5+) | postfiks `FightTournamentGame.GetTournamentPrize` (Priority.Last, po TXP): legenda/unikat/VS -> zwykly odpowiednik |
| Loteria Spoils (symulacja bitwy gracza) | [po krytyce] `RealisticLootModel` losuje z PRAWDZIWEGO zestawu pokonanego bohatera i dodaje KOPIE; `BattlefieldLaw.AfterGenerateLoot` wracal przed czystka | `StripLegendCopies` na poczatku `AfterGenerateLoot` - legendy i VS precz z lupu (klinga zostaje przy bohaterze albo idzie zwyczajem wojennym) |
| Czystka przy kazdym menu | [po krytyce] `CleanseTrashInBags` kasowal KAZDA legende z taboru gracza i magazynu DTE (wolane w `OnGameMenuOpened`) - klinga niesiona do Qohoru, zdobyta albo odziedziczona znikala | unikaty ROT i VS pomijane (K21) |
| Przebranie wladcy przez gre | [po krytyce] `NPCEquipmentsCampaignBehavior.OnRulingClanChanged` -> `EquipmentHelper.AssignHeroEquipmentFromEquipment` nadpisuje 12 slotow nowego i starego wladcy - Truth "nigdzie" u zywego Tregara | prefiks/postfiks: unikat i VS wracaja w swoj slot (K24) |
| Rozbita / rozwiazana partia | [po krytyce] gra lupi z taboru tylko `!NotMerchandise`, reszta ginie z partia | `UniqueSpoils.OnPartyDestroyed`: zwyciezca, wlasciciel albo polka (K25) |
| Szablony zolnierzy, przydzial DTE | `SweepTemplates` (`:355-421`), `Mends.UniqueWard` (CrashScribe `Mends.cs:1007-1028`) | bez zmian |
| **Karawana posilkow DTE ("Reinforcement Caravan")** | `CutTheirSupplyBehavior.GetRandomGearItems` ([DTE] `:1049-1059`) losuje NOWY sprzet t1-6 z puli kultury; [P] 16 kopii legend w 120 dob (Ice, Nightfall x2, Vigilance x2, Orphanmaker x2 - jedna na targu Menetragos) | **NOWE: postfiks na `GetRandomGearItems`**: kazda legenda/unikat (`LegendaryLaw.IsLegend` lub `ArmsPricing.IsUnique`) -> zwykly zamiennik `LegendaryLaw.ReplacementFor` (`:325-350`), brak zamiennika - wypada; karawana wiezie tyle samo sztuk |
| Warsztaty miast | `WorkshopForbiddenIds` ("val_steel,valyrian", `Settings.cs:442`) + `IsUnique` (`WorkshopLaw.cs:441`) | bez zmian |
| Kuznia gracza (nasza) | lista wyrobow pomija NotMerchandise poza strzelecka (`SmithMenu.cs:2591`) - klingi VS niedostepne | bez zmian |
| Projektownik wanilii | czesci legend ukryte (`LockLegendPieces`); po przetopie legendy odkrywane do wczytania (1.5) | czesci VS ukryte zawsze; przetop VS zablokowany (4.6) |
| Przetop VS | 0-3 sztab Iron6 + odkrycie czesci | **zablokowany** (4.6) |
| **Czystka przy wczytaniu** | `SweepWorld` KASUJE legendy z targow i bagazy AI (`:145-184`) - takze klinge sprzedana przez gracza i stara klinge lorda zdjeta przy `UniqueSpoils.Wear` (`UniqueSpoils.cs:159-171`) - sprzeczne z decyzja Jeffa 04.10 ("jak sprzedam, to bedzie gdzies w swiecie", naglowek `UniqueSpoils.cs:14-28`) | **VS nie kasujemy** po pierwszym spisie (4.5): zostaje NotMerchandise, klinga lezy na polce albo w taborze, lord AI moze ja kupic (`UniqueSpoils.OnDailyTickParty` `:190-220`) |
| Czystka magazynow DTE co dobe | `SweepAiArmories` kasuje legendy (`:222-284`) | [po krytyce] VS nie kasujemy: straznik (4.5) - w magazynie DTE AI lezy tylko kopia albo przedmiot przeniesiony, wiec przy nadwyzce zamieniany pierwszy; zachowana -> wodz partii (`UniqueLaw.FindParty` - `GetObject(MBGUID)` nie zna partii), bez wodza polka |
| **Smierc bohatera** | `UniqueSpoils.OnHeroKilled` (`:120-129`) dziala tylko przy smierci w bitwie z zabojca; inaczej klinga zostaje na martwym ([GRA-DEK] `KillCharacterAction.MakeDead` `:225-228` nie rusza ekwipunku). [po krytyce] Pomiary Truth i Longclaw NIE byly smiercia: Truth zniknal przy przebraniu wladcy (zywy Tregar), Longclaw wypchnal z rak luk Ygritte (`SlotFor` -> `Weapon0`) | **NOWE dziedziczenie** (4.4a) + przebranie, `Wear` i rozbita partia (wiersze wyzej) |

**4.4a Dziedziczenie** (Jeff 31.08 "nie zyja - spadkobiercom", 04.10 "aby nie znikaly"; wylacznik `UniqueInheritance`; [po krytyce] TYLKO stal
valyrianska - unikatowa zbroja na dziedzicu zeszlaby przy wczytaniu przez `UniqueLaw.SweepHeroes` / `MayWear`, D5): w `UniqueSpoils.OnHeroKilled`, gdy `Take` nie zabral unikatu:
- `Executed` / `ExecutionAfterMapEvent` z wykonawca -> **wykonawca** (kanon: Lod po egzekucji Neda trafil do Lannisterow);
- inne (starosc, morderstwo, smierc bez zabojcy, zaginiecie) -> **dziedzic**: glowa rodu (gra zmienia ja PRZED zdarzeniem - [GRA-DEK] `KillCharacterAction.cs:61-66`,
  zdarzenie `:144`), potem zywe dzieci, malzonek, rodzenstwo, inny zywy czlonek rodu; dziedzic zaklada (`UniqueSpoils.Wear`), gracz-dziedzic dostaje do taboru;
- nikogo -> **polka targu miasta rodu** (`Clan.HomeSettlement`, gdy zamek - miasto, do ktorego nalezy), na koniec najblizsze miasto. Jedna linia kroniki
  `Kronika unikatow: dziedzictwo <martwy> -> <kto/gdzie>: <lista>`; graczowi komunikat tylko, gdy dotyczy jego rodu.
- [po krytyce] Smierc gracza: gra SAMA przekazuje caly ekwipunek nastepcy (`HeirSelectionCampaignBehavior.OnBeforePlayerCharacterChanged` -> tabor nowej
  partii); nasze dziedziczenie pomija `Hero.MainHero` (inaczej klinga przeszlaby dwa razy), a nasz sluchacz zdejmuje cywilny duplikat klingi.
- [po krytyce] rod gracza jako dziedzic - klinga do taboru gracza (z komunikatem), nie na towarzysza.
- VS zawsze traktujemy jako unikat (pomijamy filtr "noszone przez wiecej niz `UniqueMaxWearers`", `UniqueSpoils.cs:40-46`).

### 4.5 Spis stali valyrianskiej (pomiar i bramka)

[po krytyce] Spis jest STRAZNIKIEM, nie samym logiem (`ValyrianBlades`):
- **Rejestr** (ile sztuk kazdego wzoru jest w swiecie) = stan startowy ROT: szablony postaci (`BasicCharacterObject._equipmentRoster`, zestaw bojowy i
  cywilny) bohaterow zywych, nieaktywnych i zmarlych PO starcie kampanii, czytane PRZED `LegendaryLaw.SweepTemplates`; wspolny szablon liczy sie raz;
  nazwana legenda najwyzej 1; wlasciciel ze stanu startowego zapisany. W zapisie: `arm_vs_registry` + znacznik `arm_vs_mark` (SaveText, kazdy we wlasnym
  try). Zmienia go tylko przekucie w Qohorze (wejscie -> wynik). Znacznik bez rejestru (zgubiony klucz) = bezpieczny domysl: rejestr = wiecej z (stan
  startowy, obecny) - nic nie zamieniane.
- **Spis dobowy** (w dobie LegendaryLaw, zaraz po czystce magazynow AI; jedno przejscie): bohaterowie (bojowy+cywilny - ta sama klinga = 1 sztuka; zywi
  i nieaktywni), tabory, targi, schowki gracza (`Settlement.Stash`), magazyn DTE gracza i partii AI, magazyn wojenny Spoils, u mistrzow Qohoru (wejscie
  do wydania). Nadwyzka -> `LegendaryLaw.ReplacementFor` w tym samym miejscu (liczba sztuk ta sama, stal zwykla), linia `Z NICZEGO`; kolejnosc
  zostawiania: wlasciciel startowy > inni bohaterowie > rzeczy gracza > tabory AI > targi > magazyny DTE AI; magazyn wojenny i Qohor - nie do zmiany.
  Ubytek -> `Log.Error` "Stal valyrianska: UBYTEK -x - id (jest, rejestr), ostatnio: ..." (raz na wzor do zmiany; nie przy starcie sesji). Klinga
  z magazynu DTE AI -> wodz tej partii.
- **Pierwsze wczytanie z 177** (nowa kampania albo zapis Jeffa): przy PIERWSZYM spisie dobowym (magazyn DTE gracza DTE odtwarza dopiero po sesji):
  kopie -> zwykla stal; brakujace -> najpierw z bohaterow zmarlych po starcie (do dziedzica), potem odtworzone u wlasciciela startowego albo jego
  dziedzica (klingi zgubione przez dawne bledy kodu - Truth, Longclaw). Linia "Stal valyrianska: pierwszy spis z 177 - kopii ..., odzysk z poleglych ...,
  odtworzone ...".
- Linia (start i przy zmianie): `Stal valyrianska: dzien N - K klng, M miar (rejestr R klng, baza B miar): na bohaterach a, tabory b, targi c, schowki
  gracza d, magazyny DTE e, magazyn wojenny f, u mistrzow Qohoru g; zmiany: ...`. Kronika unikatow bierze z niej miejsca, ktorych sama nie widzi.
- Bramka testu: liczba klng = rejestr kazdej doby, "UBYTEK" = 0, "Z NICZEGO" tylko z nowych kopii (T7).

### 4.6 Kuznia gracza

- **Projekt broni nie moze dostac materialu "valyrianskiego"** - takiego materialu juz nie ma: Iron6 = stal zamkowa, a czesci klng VS (`valsteelblade_*`,
  `valyrian_short_blade`, `val_red_blade`, `val_blue_blade`, `celtigar_head` i czesci nazwanych legend) zostaja ukryte w projektowniku.
- **Przetop VS zablokowany** (wylacznik `ValyrianNoSmelt`), niezaleznie od `CraftingEnabled`: postfiks `SmeltingVM.RefreshList` zdejmuje VS z listy przetopu
  (jak `SmeltTab.RefreshListPostfix` `:207-248`, tylko odejmuje) + prefiks `CraftingCampaignBehavior.DoSmelting` z `Priority.First` (przed `SmeltTab.DoSmeltingPrefix`):
  VS -> `return false` i komunikat "Only the masters of Qohor can work Valyrian steel." Rozbiorka u kowala (`SmithMenu.TakeApart*` `:2148-2259`) VS i tak nie
  przyjmuje (`RangedLore.CanLearnFrom` -> `Teachable` tylko luki/pancerze) - bez zmian.
- **Sprzecznosc dwoch decyzji Jeffa - rozstrzygnieta na korzysc pozniejszej**: 29.08 "smelt Brightroar i dostac od razu wszystkie czesci" vs 24a (09.10) "nikt
  nie robi nowej stali valyrianskiej, jedyna droga - Qohor". Dla 30 wzorow VS obowiazuje 24a; dla pozostalych legend (Dawn, Needle, Liontooth, luki...) 29.08 zostaje.
- Legendarne receptury pancerzy (`Recipes.Legendize`, `Recipes.cs:136-150`: "najszlachetniejsza stal" x tier) - zostaja: to teraz stal zamkowa.

---

## 5. Przekuwanie w Qohorze (paczka 177-3)

### 5.1 Kto i gdzie

- **Cech mistrzow platnerzy Qohoru** - bez imiennego NPC: w ROT nie ma Tobho Motta ani kowala Qohoru (notable miasta to zwykli rzemieslnicy i kupcy [D]).
  Kanon [H]: wytapiac stal valyrianska nie umie nikt od Zaglady; przekuwac umieja tylko kowale Qohoru (zaklecia); Mott, wyszkolony w Qohorze, przekul Lod
  na dwa miecze. ROT sam pisze w opisie Qohoru, ze jego kowale umieja "reforge Valyrian steel" ([GRA] `ROT-Map\ModuleData\settlements.xml:2357`).
- **Tylko Qohor** = `town_ES6` (ustawienie `QohorTownId`, gdyby ROT zmienil id). Mott w Krolewskiej Przystani - nie (nie ma go w ROT; mozliwe rozszerzenie).
- Wzor menu: Bank Zelazny w Braavos (`IronBank.AddMenus`, `IronBank.cs:433-457`): opcja w "town" z warunkiem na id osady, wlasne menu, "Leave" -> "town".
  Rejestracja obok `IronBank.AddMenus` (`ArmouryBehavior.cs:1049`; [po krytyce] linia poprawiona).

### 5.2 Co wchodzi i co wychodzi

Wejscie: klingi ze spisu 4.1 z **taboru gracza** (noszona trzeba zdjac - mowi o tym podpowiedz). [po krytyce] Zdejmowana jest DOKLADNA sztuka (`EquipmentElement`
z modyfikatorem) - "Take back" oddaje ja w tym samym stanie. Zasada: **wynik ma tyle miar, ile wejscie**.

| Zlecenie (tekst opcji) | Wejscie | Wynik do wyboru | Dni pracy |
|---|---|---|---|
| "Have a blade reforged in a new form" | klinga 1 miary | 1 miecz 1 miary: wzory seryjne ROT `koa_sword_tier_5`, `val_steel_sword_2`..`_8`, `_blue`, `_red` (10 wzorow; type 8 to dwureczny 1 miary) | 7 |
| "Have a greatsword reforged into two blades" (jak Lod) | wielki miecz 2 miar | 2 miecze 1 miary (jeden wzor dla obu albo dwa) | 7 |

- [po krytyce] **"Dwa w jeden" - NIE**: Jeff powiedzial tylko "wielki miecz moze dac dwa mniejsze", a seryjnego wyniku 2 miar nie ma (type 8 = 1 miara).
- **Wynik to gotowe seryjne miecze ROT** (wariant A rozpoznania): juz sa legendami, unikatami, poza handlem, z wycena i w spisie. Nazwanych legend mistrzowie
  nie robia; przekuty Lod znika na zawsze, zostaja dwa nowe ostrza. Nazwy pojedynczej sztuki nadac sie nie da (ItemObject wspolny) - wariant B, pytanie 2.
- **Zbroje: nie** (kanon nie zna przekucia zbroi; w ROT nie ma zbroi z prawdziwej stali valyrianskiej; masa).

### 5.3 Ile dni [S]

[po krytyce] `QohorDays` = **7 dni pracy** dla kazdego zlecenia - skrot gry na zyczenie Jeffa ("kilka dni"); w ksiazkach - miesiace (miedzy egzekucja Neda
a pokazaniem mieczy). Dawne 7/8/10/12 z dzielenia dni mistrza przez czworke mistrzow to bylo dopasowanie, nie pomiar. Kazdy dzien pracy zabiera rece
kowali Qohoru (`SmithHours.Use`, najwyzej `QohorCrew x WorkHoursPerManDay` na dobe - te same rece co naprawy i warsztaty, Jeff 05.10); cech bierze tyle,
ile dzis wolnych, i odklada na zlecenie - dzien pracy schodzi, gdy uzbiera sie pelny (zajete rece = praca wolniej, nie stoi). Wegiel: `QohorCharcoalPerMeasure` (2) na miare z polki Qohoru przy pierwszym dniu; brak - zlecenie czeka.

### 5.4 Ile kosztuje i dokad idzie zloto [S + K]

[po krytyce] Liczone jawnie, bez dopasowania do ceny miecza:
- **Praca** = dni (7) x mistrzowie (`QohorCrew` 4) x dniowka mistrza t6 (`HistoricalPrices.WageFor(6)` = 3 + 5 x 1.5 = 10.5 d) x poziom plac Qohoru
  (`TownWage.Index` = dobrobyt / 4800, 0.5-1.5; na starcie 5 200 -> 1.08) = **ok. 318 d**.
- **Oplata cechu** = `QohorTollPercent` 6.25% (1/16) wartosci stali, ktora przechodzi przez ogien (miary x `HistValyrianPerMeasure` 60 000) = 3 750 d za
  miare. Uzasadnienie [S]: jedyny cech na swiecie, ktory zna sztuke - monopol jak mlyn dworski, ktory bral od ziarna swoja miarke (multure, zwyczajowo
  1/24-1/16); klient nie ma innego mlyna, wiec stawka gorna.
- Razem: klinga 1 miary **ok. 4 070 d**, wielki miecz 2 miar (Lod -> dwa) **ok. 7 820 d**; zmienia sie z dobrobytem Qohoru (tylko czesc za prace).
- Wartosc klingi (4.3, `HistoricalPrices`): **miary x `HistValyrianPerMeasure`** - kotwica [P]: mediana kiesy glowy rodu w 60. dobie testu 120 dob = 58 894
  (budzet-rodow.csv) - typowy lord oddalby cala kiese, a zakup z polki wymaga 2 x ceny w kiesie, wiec kupi tylko najbogatszy (kanon: Tywin trzy razy
  nie kupil). Na polce klinga wazy w danych rynku tylko swoj koszt wykonania - nie zalewa podazy kategorii w miescie. Przelicznik popytu kategorii broni
  liczony PRZED ta wycena - popyt miast jak przed 177.
- Dawne zdanie o Mocie ("podwojna oplata = premia monopolu") bylo bledne: podwojna oplata to cena przyjecia Gendry'ego na terminatora (reszta - za
  milczenie), nie cena przekucia.
- **Dokad**: z gory, do **kasy Qohoru** (`GiveGoldAction.ApplyForCharacterToSettlement`) - placi gracz albo lord, dostaje miasto; nie wraca przy odebraniu
  klingi przed czasem (praca zaczeta). Wegiel z polki Qohoru - z oplaty, zloto zostaje w miescie.

### 5.5 Przebieg w grze (teksty po angielsku)

1. W menu Qohoru: **"Seek out the Valyrian steel masters of Qohor"** (podpowiedz: ile zlecen w toku / gotowych).
2. Menu `arm_qohor_masters`: "Beneath the temple of the Black Goat the smiths of Qohor keep the last secret of Valyria. No one has made Valyrian steel since
   the Doom - but these masters can melt it and forge it anew, and nothing of it is lost." + lista zlecen.
   Opcje: trzy zlecenia z 5.2 | "Wait in Qohor until the masters are done" (gdy w toku) | "Collect your reforged steel" (gdy gotowe) | "Take back your blade"
   (gdy w toku) | "Leave".
3. Wybor klingi i wzoru: `MBInformationManager.ShowMultiSelectionInquiry` z obrazkiem (`SmithMenu.ItemPic`), jak `TakeApartConsequence` (`SmithMenu.cs:2148-2195`);
   potwierdzenie `InformationManager.ShowInquiry`: "{BLADE} goes into the fire. In {DAYS} days the masters will hand you {RESULT}. Their fee of {FEE} is paid
   now and is not returned."
4. [po krytyce] Zaplata, zdjecie DOKLADNEJ sztuki wejscia (`ItemRoster.AddToCounts(element, -1)`), zlecenie z licznikiem dni PRACY (`DaysLeft`, schodzi w dobie, gdy
   sa rece kowali i wegiel - nie zegar bezwzgledny); NIE w `_projects` kuzni gracza (dniowka, przerwy nocne, XP gracza).
5. Odjazd i powrot albo czekanie w miescie: `AddWaitGameMenu("arm_qohor_wait", ...)` wedlug wzoru `arm_project_wait` (`SmithMenu.cs:163-178`, tick :295-330),
   pasek postepu `1 - zostalo/calosc`; gotowe -> tick przelacza na menu mistrzow.
6. Odbior: `ItemRoster.AddToCounts`, okienko `CraftPopup.Show(item, null, n)` (`CraftPopup.cs:101`): "The masters of Qohor lay {RESULT} before you. The ripples of
   the old steel run through the new blade." Wynik bez modyfikatora (nowe ostrze). Gotowe czeka bez konca.
7. Przy wejsciu do Qohoru z gotowym zleceniem - komunikat "Your Valyrian steel is ready at the masters' forge."

### 5.6 Zabezpieczenia

- **Wylacznik** `QohorReworkEnabled`: wylaczony chowa nowe zlecenia, ale odbior i "Take back" dzialaja dalej - nic nie utknie u mistrzow.
- **Zapis w trakcie**: jedna lista, klucz `arm_qohor_orders` przez **`SaveText.Sync`** (`SaveText.cs:31`; wzor `IronBank.Export/Import`, `ArmouryBehavior.cs:465-467`).
  [po krytyce] Rekord: `#wlasciciel;wej;mod;wyn1,wyn2;zostalo;razem;oplata;miasto;wegiel;dzien` (wlasciciel `@player` albo StringId lorda AI). Kilka wierszy - daleko od 32 KB. Wczytanie w srodku menu czekania: init liczy postep
  z `ReadyDay`; ratunek z zastoju zegara jak w `arm_project_wait` (`StoppablePlay` + `StartWait`).
- **CTD**: zadnego `GameMenu.SwitchToMenu()` z opcji menu czekania (CLAUDE.md rozdz. 7) - opcja "Leave the masters to their work" podnosi flage, tick przelacza.
- **Nie dopisywac `arm_qohor_wait` do `TrueArmourCost.InForgeMenu`** (`Patches.cs:328-338`): tam platnosci gracza w menu kucia sa polykane (`SwallowForgeFee`
  `:349-361`). Oplata i tak idzie przed czekaniem, z menu mistrzow.
- Wojna z wlascicielem Qohoru / brak wstepu: zlecenie czeka, nic nie znika. Zmiana wlasciciela Qohoru - bez znaczenia (cech zostaje).
- Brak przedmiotu wyniku po aktualizacji ROT (id zniknelo) -> mistrzowie oddaja wejscie (log). Brak `town_ES6` -> funkcja spi (linia startowa "BRAK").
- Nowa kampania: czyszczenie w liscie `Reset()` konstruktora `ArmouryBehavior` (`ArmouryBehavior.cs:389`).
- Spis 4.5 liczy klingi u mistrzow (wejscie do odbioru = wynik); kronika `UniqueSpoils` pokazuje "u mistrzow Qohoru (gotowe dnia N)" zamiast "nigdzie".
  Wiersz kroniki przy odbiorze: `Kronika unikatow: Qohor - ice_sword przekuty na val_steel_sword_3 x2 (miary 2 -> 2)`.

### 5.7 AI - [po krytyce] ta sama regula (dawniej: tylko gracz)

[po krytyce] Poprzednie uzasadnienie bylo odwrocone: jedyne kanoniczne przekucie zlecil lord (Tywin), z CUDZEJ klingi zdobytej na wrogu (Lod po egzekucji), wlasnie
po to, by zniszczyc dziedzictwo wrogiego rodu i uzbroic swoj. Dlatego **AI ma te sama regule**: lord, ktorego partia stoi w Qohorze, nosi wielki miecz 2 miar,
ktorego wlasciciel ze stanu startowego jest z INNEGO rodu, i ma w kiesie co najmniej 2 x oplata - zleca rozdzielenie na dwa miecze (losowe wzory seryjne).
Ta sama oplata do kasy Qohoru, te same dni i rece kowali; odbior tez tylko w Qohorze (gdy jego partia tam stanie): jeden miecz zaklada, drugi dostaje glowa
rodu / dziedzic. Bez "zdalnego" zlecenia. Wylacznik `QohorAiRework`.

### 5.8 Wariant B (pozniej, tylko na slowo Jeffa)

Ostrze wedlug projektu gracza z nadana nazwa (jak Joffrey nazwal Wdowi Lament): wykonalne przez publiczne `Crafting.GenerateItem` + zdarzenie `OnNewItemCrafted`
(rozpoznanie przekuwanie 3.3), ale 5-6 punktow styku (rejestr "to jest VS" dla id `crafted_item_N` we wszystkich prawach unikatow, straznik na nasz
`ArmouryBehavior.OnNewItemCrafted` :1369-1424, wycena). Osobna paczka - pytanie 2.

---

## 6. Kontrola calosci (zasada 0 CLAUDE.md)

| # | Co | Gdzie | Skutek / co robimy |
|---|---|---|---|
| K1 | przetop legendy odkrywa czesci (29.08) | `SmeltTab.cs:175-205` | dla VS zastapione blokada przetopu (4.6); inne legendy bez zmian |
| K2 | przetop VS daje Iron6 | wanilia/BK przez `SmeltTab.DoSmeltingPrefix` :119-124 | zamkniete ta sama blokada |
| K3 | wycena VS | `HistoricalPrices.HistCost` :245-257 z `GradeFor` | cena sztaby nie wplywa; `GradeFor` t6 -> Iron6 daje ok. +4% (jak kazdej broni t6) |
| K4 | `SweepWorld` kasuje legendy co wczytanie vs `UniqueSpoils` 04.10 | `LegendaryLaw.cs:145-184` | [po krytyce] VS wylaczone zawsze (kopie lapie straznik 4.5, bez jednorazowej czystki); ta sama sprzecznosc dla pozostalych legend i dla unikatowych zbroi (`Mends.UniqueWares` CrashScribe :828-858) - osobna mala paczka (D1), decyzja 04.10 juz jest |
| K5 | `SweepAiArmories` kasuje legendy | `LegendaryLaw.cs:222-284` | [po krytyce] VS pomijane - straznik (4.5): nadwyzka (w magazynie AI to kopia) -> zwykla stal, reszta -> wodz partii przez `UniqueLaw.FindParty`; `MoveRosterToArmory` wylaczony przez `AiGear` |
| K6 | karawana DTE rodzi kopie | [DTE] `CutTheirSupplyBehavior.cs:1049-1083` | postfiks: legendy/unikaty -> zamiennik; dotyczy wszystkich unikatow (regula 30.08 "jedna na swiecie") |
| K7 | wedrowcy BK | [BK] `PopulationPartyComponent.cs:214-292` | zdjete sztaby i wegiel; ruda/drewno/towary - etap 3.1 (164a), audyt 03 P1-C; ksiega towarow pokaze spadek "inne ticki dobowe (osady)" i "przepadlo z rozbitymi partiami" |
| K8 | naprawy, smithy, rzemieslnicy BK bez sztab z polki | `MendMaterial.Pick`, `spworkshops.xml:797-830` | biora rude (jak dzis w ok. 95%) - bez regresji |
| K9 | `GradeFor` t6 -> Iron6 | `ArmsPricing.cs:96-104`, `WorkshopLaw.Needs`, `MendMaterial.cs:111,482`, `HistoricalPrices.cs:250` | ceny t6 +4-8%, ruda t6 x1.25 w warsztatach i naprawach; wylacznik `Tier6CastleSteel`; t1-t5 bez zmian (test: rowne co do pensa) |
| K10 | "valyrianska zasada T6" (Inni) | CrashScribe `Mends.cs:66-125`, `:1746-1760`, `:2616-2650` | **bez zmian** do odpowiedzi Jeffa (pytanie 1); tylko komentarz/log "T6 = stal zamkowa i valyrianska" |
| K11 | klucze MCM | `Settings.cs:378, 520`, `McmSettings.cs:1279, 1799, 3369, 3499` | nie ma ich w Armoury.json Jeffa - domyslne zadzialaja; `gen_mcm.py` |
| K12 | nazwa sztaby w zapisie | - | item z kodu gry, nazwa nie jest zapisywana; przedmioty wykute przez gracza z "Thamaskene" w zapisanej nazwie - nie ruszamy |
| K13 | `UniqueSpoils` filtr "pospolite" (> 3 noszacych) | `UniqueSpoils.cs:40-46` | VS zawsze unikat |
| K14 | bohaterowie nieaktywni vs martwi | `Hero.DeadOrDisabledHeroes` | spis liczy Disabled jako zasob; odzysk tylko z martwych po starcie kampanii |
| K15 | kolejnosc: zapis wczytany przed `OnSessionLaunched` | `ArmouryBehavior.SyncData` | flaga spisu znana, zanim `LegendaryLaw.OnSession` (:72-84) odpali `SweepWorld` |
| K16 | menu Qohoru a zapora oplat kuzni | `Patches.cs:328-361` | menu czekania poza `InForgeMenu` |
| K17 | duplikaty seryjnych mieczy u gracza | `LegendaryLaw.CullPlayerAll` :426-444 (jednorazowe, flaga) | dwa takie same miecze z Lodu nie zostana skasowane (flaga juz ustawiona) - sprawdzic w tescie recznym |
| K18 | AIInfluence: wiedza `valyrian_steel` | `tools\ai-influence-pack\world_data\world_info.json:193` | zgodna z kanonem ("lost fires of Valyria") - bez zmian |
| K19 | wydajnosc | `UniqueSpoils.Daily`, postfiks BK (raz na nowa partie), postfiks DTE (raz na karawane) | pomijalne; spis w tym samym przejsciu co kronika |
| K20 | jedna kolejnosc doby | - | oplata natychmiast przy zleceniu, wydanie przy opcji/wejsciu; zadnych nowych tickow swiata |
| K21 | [po krytyce] czystka przy kazdym menu vs unikaty | `ArmouryBehavior.CleanseTrashInBags` | unikaty ROT i VS pomijane |
| K22 | [po krytyce] TournamentsXPanded - nagroda bez filtra NotMerchandise | TXP `GetTournamentPrizePatch.Prefix` | nasz postfiks `GetTournamentPrize` Priority.Last |
| K23 | [po krytyce] Spoils of War - loteria z prawdziwego zestawu bohatera (symulacja) | `RealisticLootModel.ProcessSingleCasualty`, `BattlefieldLaw.AfterGenerateLoot` | `StripLegendCopies` dla kazdej bitwy |
| K24 | [po krytyce] gra przebiera wladcow (12 slotow) | `NPCEquipmentsCampaignBehavior` -> `EquipmentHelper.AssignHeroEquipmentFromEquipment` | prefiks/postfiks - unikat i VS zostaja w slocie |
| K25 | [po krytyce] lup bez NotMerchandise, tabor ginie z partia | `MapEvent.LootDefeatedPartyItems`, `DestroyPartyAction` | `UniqueSpoils.OnPartyDestroyed` |
| K26 | [po krytyce] nastepca gracza dziedziczy ekwipunek (bojowy i cywilny) | SandBox `HeirSelectionCampaignBehavior` | dziedziczenie VS pomija gracza; cywilny duplikat zdjety |
| K27 | [po krytyce] CrashScribe `DressedOrNot` na tym samym celu co K24 | CrashScribe `Mends.cs` | nasz prefiks Priority.Last, postfiks nic nie robi bez przebrania |

Dalsze, poza 177 (zapisane, nie robione): **D1** czystki legend/unikatowych zbroi z targow i bagazy (K4) - zgodnie z 04.10 przestac kasowac; **D2** ruda,
drewno i towary wedrowcow BK (K7) - etap 3.1; **D3** odkrycie czesci legendy po przetopie gine po wczytaniu (1.5) - dla nie-VS to blad wobec decyzji 29.08.

---

## 7. Kolejnosc wdrozenia (jedna zmiana naraz - trzy commity, kazdy z build kod 0, gen_mcm, wpisem CHANGELOG)

[po krytyce] WYKONANE: 177-1 (7fde4a6), 177-2 (abd1ac0), 177-3 (a47eef3) na galezi `w-toku/177-stal-valyrianska`. Nazwy ustawien: `DteCaravanNoLegends` ->
`NoConjuredLegends` (obejmuje tez nagrody turniejowe), nowe `ValyrianGuard`, `UniqueNeverLost`, `HistValyrianPerMeasure`, `ValyrianGreatswordMinCm`,
`QohorAiRework`, `QohorDays`, `QohorTollPercent`, `QohorCharcoalPerMeasure` (zamiast `QohorMeltDays`, `QohorDaysPerBlade1H/2H`, `QohorJoinDays`,
`QohorSecretPremium`). Ponizej plan pierwotny:

1. **177-1 Stal zamkowa** (Armoury): `ValyrianSteel.cs` -> `CastleSteel.cs` (nazwa + liczba mnoga + podpowiedz, opis perku, "Thamaskene" w nazwach przedmiotow
   i czesci, bez `DearRefine`, postfiks BK na `CreateTravellerParty` z linia dnia); `Settings`: `CastleSteelValue` 375, `HistCastleSteelPerKg` 12,
   `TravellersNoIngots` true, `Tier6CastleSteel` true; `MaterialLaw.cs:67`, `HistoricalPrices.cs:234`, `ArmsPricing.GradeFor`, `Recipes.SmeltYield`.
2. **177-2 Stal valyrianska - skonczony zasob** (Armoury): nowy `ValyrianBlades.cs` (spis 30 wzorow + miary, blokada przetopu, podpowiedz, spis i pierwsze
   wczytanie, `arm_vs_census`); `LegendaryLaw` (zbior legend + VS, `SweepWorld`/`SweepAiArmories` bez kasowania VS po spisie); `UniqueSpoils` (VS zawsze,
   dziedziczenie, kronika); postfiks DTE na `GetRandomGearItems`; `Settings`: `ValyrianNoSmelt`, `UniqueInheritance`, `DteCaravanNoLegends` (true).
3. **177-3 Mistrzowie Qohoru** (Armoury): nowy `QohorMasters.cs` (wzor `IronBank.cs`), rejestracja przy `ArmouryBehavior.cs:1048`, `arm_qohor_orders` w SyncData,
   `Reset()`; `Settings`: `QohorReworkEnabled` true, `QohorTownId` "town_ES6", `QohorMeltDays` 4, `QohorDaysPerBlade1H` 3, `QohorDaysPerBlade2H` 4,
   `QohorJoinDays` 4, `QohorCrew` 4, `QohorSecretPremium` 2.5.
CrashScribe - bez zmian w 177 (poza komentarzem/logiem zasady T6, jesli Jeff odpowie "zostaw").

---

## 8. Plan testu

### 8.1 Autotest: nowa kampania 40 dob (po 177-1 + 177-2; po 177-3 powtorka) - linie logu i progi

| # | Linia (log) | Prog |
|---|---|---|
| T1 | `CastleSteel: 'Thamaskene Steel' przemianowana na 'Castle-forged Steel'; opis Steel Maker 3; 'Thamaskene' w nazwach: 12 przedmiotow, N czesci` (Armoury, start) | jest; items-dump: `ironIngot6;Castle-forged Steel;...;6`; zero "Thamaskene" w kolumnie nazw |
| T2 | `HistoricalPrices` surowce: `ironIngot6 260->6` | 6 (bez cen hist.: MaterialLaw `260->375`) |
| T3 | brak linii `ValyrianSteel: wytop ... 2x drozszy` | brak |
| T4 | `Wedrowcy BK: dzien N - ... zdjeto sztab Y (Castle-forged Z), wegla W` | co dzien Y > 0 (spodziewane ok. 200-210 sztab i ok. 50 wegla/d [S z 3.1]) |
| T5 | `Towary: dzien` dla ironIngot1-6 i wegla (`tools/iron6.py` z `SCR\a177\tools`) | produkcja "inne ticki dobowe (osady)" = **0** kazdego dnia; zapas Iron6 swiata <= startowego (1), w miastach 0. [po krytyce] Ruda, drewno i towary wedrowcow w "inne ticki dobowe (osady)" NIE wiecej niz przed 177 (porownac z autotestem 120 dob, te same doby); linia "popyt miast przeliczony": kategoria iron /23.5 jak przed 177 |
| T6 | `HistoricalPrices` przyklady (`casterly_heavy_helm`, `battania_sword_5_t5`, ...) | t6 +3-10%, t1-t5 rowne co do pensa z logiem sprzed 177 |
| T7 | `Stal valyrianska: dzien ... K klng, M miar (rejestr R, baza B)` | [po krytyce] start: rejestr ze stanu startowego (ok. 17-18 klng / 21-22 miary [S]); po pierwszym spisie co dobe **K = R i M = B** (klingi u mistrzow Qohoru liczone); linii "UBYTEK" (ERROR) **0**; "Z NICZEGO" tylko pierwszego dnia (kopie z dawnych zrodel) albo z nowego, nazwanego zrodla |
| T13 | `ValyrianBlades: latki wpiete - przetop, lista Smelt, karawana DTE, nagroda turniejowa`; `UniqueSpoils: przebranie bohatera ...` | [po krytyce] obie linie bez "ZADNA" / "nieznaleziona" |
| T14 | `Kronika unikatow: ... przebrany przez gre ... zostaje przy ...`; `Unikaty (177): dzien` | [po krytyce] przy zmianie rodu panujacego wladca z VS zostaje z nia; przy rozbitej partii z unikatem - linia "partia ... znika ... -> ..." |
| T15 | `HistoricalPrices: stal valyrianska - 29 wzorow wyceniona jako skonczony zasob` | [po krytyce] jest; przyklady: ice_sword -> 120000, longclaw_sword -> 60000 |
| T8 | `Kronika unikatow` | zero "nigdzie" dla 30 wzorow VS; kazda smierc noszacego -> linia `dziedzictwo` albo zdobycz |
| T9 | linia DTE `karawany posilkow: N legend/unikatow -> zamienniki` | N > 0 przy co najmniej jednej karawanie (dzis ok. 5 kopii / 40 dob); w kronice zero VS w "tabor Reinforcement Caravan" |
| T10 | `LegendaryLaw: magazyny AI ... legend przepadlo` | dla VS 0 |
| T11 | `QohorMasters: menu w town_ES6 (Qohor) wpiete` | jest, bez "BRAK" |
| T12 | bledy | 0 nowych `Log.Error` / CrashScribe; czas doby w granicach +5% (dzis 13.1 s/dobe) |

### 8.2 Zapis (doba 362, kampania sprzed 177) - 8 dob
- [po krytyce] pierwsze wczytanie: linia "pierwsze wczytanie z 177 - rejestr ze stanu startowego"; pierwsza doba: "pierwszy spis z 177 - kopii ..., odzysk
  z poleglych ..., odtworzone ..." (Truth -> Tregar i Longclaw -> Jon to ODTWORZENIE, nie odzysk z martwych - te klingi nie leza na martwych); drugie
  wczytanie: brak tych linii, rejestr z zapisu.
- sztaby Iron6 w swiecie: liczba bez zmian przy wczytaniu (nic nie kasujemy), potem tylko spada (zuzycie); progi T4-T12 jak wyzej.
- zapis i wczytanie w trakcie (dzien 4): spis i lista Qohoru wracaja bez zmian.

### 8.3 Recznie (Jeff albo autotest z klikaniem, gdy bedzie)
1. Targ i kuznia: sztaba "Castle-forged Steel" z podpowiedzia, perk Steel Maker 3, rafinacja 5 Fine + 2 wegla -> 4.
2. Zakladka Smelt: klingi VS nie ma na liscie; zwykly miecz "Castle-forged Steel Spathion" jest.
3. Podpowiedz klingi VS: "Valyrian steel - one/two measure(s)...".
4. Qohor (przyklad: gracz z Lodem i 2 000 d): opcja w menu miasta; bez klingi/zlota opcje szare z podpowiedzia; Lod -> dwa miecze: zloto -1 140 (ok.),
   kasa Qohoru +tyle samo (linia obiegu), Lod znika z taboru; "Wait in Qohor" - pasek, po 10 dniach dwa miecze w taborze i okienko; odjazd i powrot - odbior
   z menu; "Take back" w trakcie - Lod wraca w tym samym stanie, zloto nie; zapis i wczytanie w menu czekania i poza nim; dwa 1H -> jeden 2H.
5. Smierc lorda z klinga VS poza bitwa (konsola/test): klinga u glowy rodu.

---

## 9. Pytania do Jeffa (tylko zmiany rozgrywki spoza decyzji 24a)

[po krytyce] Doszly trzy sprawy spoza 177 (D4-D6, rozdz. 11) - zapisane, nie zrobione: czystka olbrzymow/egzotykow w magazynach AI martwa; unikatowa zbroja
zdobyta lub kupiona zdejmowana przy wczytaniu; zamiennik pojmanego z niczego.

1. **Inni a bron tieru 6.** Dzis kazda bron tieru 6 rani Bialych Wedrowcow i Nocnego Krola w pelni, a slabsza zadaje 15% (Twoja zasada z 30.08 "tier 6 =
   stal valyrianska"). Po 24a tier 6 to stal zamkowa, a w ksiazkach zwykla stal peka na Innych. (a) **zostawic jak jest** (proponuje: stali valyrianskiej
   jest ok. 20 klng na caly swiat, bez tieru 6 Inni byliby prawie nie do pokonania); (b) pelne obrazenia tylko stal valyrianska, smocze szklo i smoczy ogien,
   tier 6 np. 50%, reszta 15% (zgodne z ksiazkami, ale Inni wyraznie silniejsi - trzeba by ponownie zmierzyc ich pochod).
2. **Wlasny miecz z Qohoru (wariant B).** Na start mistrzowie robia gotowe wzory ROT ("Valyrian Steel Sword type 1-8, Blue, Red"). Czy chcesz pozniej, by
   mogli wykuc ostrze wedlug Twojego projektu z kuzni i z Twoja nazwa (jak Joffrey nazwal Wdowi Lament)? To osobna, wieksza paczka.

---

## 10. Zrodla

- Rozpoznanie: `SCR\a177\dane.md` (rozdz. 1-6), `SCR\a177\przekuwanie.md` (kanon 1.1-1.4, gra 3.1-3.7, kontrola K1-K10); skrypty `SCR\a177\tools\iron6.py`,
  `pieces.py`, `uniq.py`, `SCR\a177\vs_items.py`.
- Kanon [H]: AWOIAF Valyrian steel, Tobho Mott, Qohor, Ice, Brightroar, Steel ("castle-forged"), The Forsaken (TWOW); ASOS rozdz. 32 i 60 (jedyny cytat:
  "There was enough metal for two new blades"); SSM "Producing Valyrian Steel" (westeros.org/Citadel/SSM/Entry/1153).
- Kod gry [GRA-DEK]: `DefaultItems.cs:133`, `DefaultPerks.cs:2122`, `PerkObject.cs:35`, `DefaultTournamentModel.cs:95-140`, `KillCharacterAction.cs:18-29, 56-66, 144,
  225-228`, `DefaultSmithingModel.cs:170-177`; [CORE] `CraftingPiece.cs:36, 86`, `CraftingMaterials.cs:3-15`.
- BK [BK]: `PopulationPartyComponent.cs:84-97, 214-292, 316-405`; `BKPartyBehavior.cs:850-859, 938, 1043`. DTE [DTE]: `CutTheirSupplyBehavior.cs:1037-1083`,
  `EveryoneCampaignBehavior.cs:368-421`.
- Dane: `[GRA]\ROT-Map\ModuleData\settlements.xml:2357`, `docs/ROT-UNIKATY.md`, `Documents\...\CrashScribe\items-dump.csv` (09.10 04:04),
  `Documents\...\Configs\ModSettings\Global\Armoury\Armoury.json` (09.10, 331 kluczy).
- Pomiary [P]: `SCR\kopiaT9-120\Armoury-2026-10-08_22-23-58.log` (linie "Towary: dzien", "Rynek surowcow", "Kronika unikatow"), `unikaty.log`; audyt
  `docs/audyt-2026-10-09/03-surowce-naprawy.md` (1.5, 1.6, 2.4, P1-C).

---

## 11. Krytyka i odpowiedzi (09.10, po przegladzie - wdrozone w drzewie `SCR\noc2\sv`)

Kazda uwaga sprawdzona w kodzie (gra 1.4.8 [GRA-DEK], SandBox `...\3cf3e0ac...\scratchpad\dzien-6\autotest\dec\SandBox`, TaleWorlds.ObjectSystem przez
ilspycmd `SCR\a177impl\dec`, DTE [DTE], Spoils `...\3cf3e0ac...\scratchpad\dzien-6\spoils\dekompilacja\rl`, TXP `SCR\a177-krytyka\txp`, ROT XML [GRA]).
Werdykt: **PRAWDA** / **CZESCIOWO** / **NIEPRAWDA**. "Zrobione" = kod w commitach 177-1 (7fde4a6), 177-2 (abd1ac0), 177-3 (a47eef3).
Tam, gdzie odpowiedz zmienia tresc wyzej, rozdzialy 0-9 poprawione (oznaczone "[po krytyce]").

| # | Uwaga (skrot) | Werdykt i dowod | Zrobione |
|---|---|---|---|
| 1 | `CleanseTrashInBags` kasuje legendy (w tym VS) z taboru gracza i magazynu DTE przy kazdym menu | **PRAWDA** - `ArmouryBehavior.CleanseTrashInBags` (galaz `LegendaryLaw.IsLegend`), wolana z `OnGameMenuOpened` i ze startu sesji; `LootMinConditionPercent` domyslnie 3 | pomija unikaty ROT (`UniqueSpoils.Is`) i spis VS; kopie lapie straznik (4.5). Nowe K21 |
| 2 | TXP i loteria Spoils rodza kopie | **PRAWDA** - TXP: prefiks `GetTournamentPrizePatch.Prefix` (priority 800) bierze nagrode z puli bez filtra NotMerchandise; Spoils: `RealisticLootModel.ProcessSingleCasualty` -> `GetRandomBattleEquipment(character)` (dla bohatera - jego prawdziwy zestaw), `BattlefieldLaw.AfterGenerateLoot` wracal dla symulacji przed `CleanseTrash` | postfiks `FightTournamentGame.GetTournamentPrize` (Priority.Last, po kazdym prefiksie): legenda/unikat/VS -> zwykly odpowiednik; `BattlefieldLaw.StripLegendCopies` na poczatku `AfterGenerateLoot` (tez symulacje; "Lucky" siedzi w tym samym rosterze). Wylacznik `NoConjuredLegends` |
| 3 | VS w taborze AI nie jest lupem i ginie z partia; `Wear` bez partii gubi stara sztuke; `SlotFor` nadpisuje unikat | **PRAWDA** - `MapEvent.LootDefeatedPartyItems` filtr `!NotMerchandise`; `DestroyPartyAction.ApplyInternal`: `OnMobilePartyDestroyed` PRZED `RemoveParty` (tabor jeszcze jest; to samo przy rozwiazaniu - `ApplyForDisbanding`) | `UniqueSpoils.OnPartyDestroyed` (`UniqueNeverLost`): gracz-zwyciezca - tabor gracza, lord - VS zaklada, inne unikaty do jego taboru, rozwiazanie - wlasciciel, na koniec polka miasta; `Wear` nie wypycha unikatu, odklada do taboru (nie tego, ktory znika), bez partii - polka miasta rodu (`Park`/`Shelve`/`TownFor`) |
| 4 | `MBObjectManager.GetObject(MBGUID)` nie zna partii | **PRAWDA** - komentarz `UniqueLaw` 16.09 i `FindParty` | VS z magazynu DTE AI -> `UniqueLaw.FindParty` (teraz internal) -> wodz/wlasciciel. Olbrzymy i egzotyczne wierzchowce w tej samej petli `LegendaryLaw.SweepAiArmories` dalej z `GetObject` (martwe od zawsze; naprawa = kasowanie w nicosc - zasada 4 CLAUDE.md) - **D4 do Jeffa** |
| 5 | `DoSmeltingPostfix` biegnie po prefiksie `false` | **PRAWDA** (Harmony 2.4.2 wola postfiksy zawsze) | postfiks z `__runOriginal` i bez VS; blokada przetopu VS w osobnym prefiksie Priority.First + lista Smelt bez VS |
| 6 | wspolny try w SyncData; jednorazowa czystka nie odroznia kopii; kolejnosc sluchaczy | **PRAWDA** | `arm_vs_mark`, `arm_vs_registry`, `arm_qohor_orders` i przeniesiony `arm_uniq_init` - kazdy we wlasnym try; zgubiony rejestr przy obecnym znaczniku = bezpieczny domysl (wiecej z: stanu startowego, obecnego - nic nie zamieniane); zamiast "skasuj wszystko z polek i bagazy" - straznik wedlug stanu startowego z kolejnoscia (wlasciciel startowy > inni bohaterowie > rzeczy gracza > tabory AI > targi > magazyny DTE AI); spis na koncu `LegendaryLaw.OnSession`, a dzialania (zamiana, odzysk) w pierwszej dobie - magazyn DTE gracza DTE odtwarza dopiero po sesji |
| 7 | dziedziczenie wszystkich unikatow koliduje z `UniqueLaw.SweepHeroes` | **PRAWDA** (`SweepHeroes` -> `MayWear` -> zamiennik bez zwrotu) | dziedziczenie tylko VS (bron - `SweepHeroes` dotyczy tylko UniqueGear). Ta sama kolizja dla zdobyczy (`Take`) i zakupu z polki unikatowej ZBROI istnieje od 04.10 - **D5 do Jeffa** (lista "prawowitych noszacych" w zapisie) |
| 8 | cena sztaby 6 zmienia ladunek wedrowcow BK i popyt miast na "iron" | **PRAWDA** - BK `GiveItems`: budzet `num2` w wartosci, materialy wazone `Value x losowa`, towary `1/Value`; `HistoricalPrices` przelicznik = srednia geometryczna (stara/nowa) | na czas `GiveItems` sztaba 6 ma dawna wartosc (pole, nie setter; zdjecie sztab jeszcze przy niej - `ItemRoster.TotalValue` bez przesuniecia), wiec reszta ladunku dokladnie jak przed 177; przelicznik kategorii i srednia kategorii mieszanych licza sztabe 6 po dawnej cenie (`CastleSteel.RatioValue`). Zdanie w 3.2 poprawione |
| 9 | sam `PrimaryDescription` nie zmieni okna wyboru perku | **PRAWDA** - `PerkObject.Initialize` sklada `Description` (pole `PropertyObject._description`) | oba pola; linia startowa mowi "(podpowiedz i okno wyboru perku)" |
| 10 | brak linii w panelu `ItemMenuVM` | **PRAWDA** | `SteelTooltip`: `RefreshItemTooltip` i `ItemMenuVM.SetItem` |
| 11 | zbroje-unikaty z karawany DTE bez zamiennika | **PRAWDA** (`ReplacementFor` tylko bron) | postfiks DTE: bron -> `ReplacementFor`, reszta (i bron bez odpowiednika) -> `UniqueLaw.StandInFor` w kulturze karawany; licznik "bron / reszta / bez zamiennika" |
| 12 | `RemoveOne` w dowolnym stanie; klingi tylko w napisie przy cofnieciu DLL; spis bez `WarStockpile` | **PRAWDA** | Qohor zdejmuje dokladna sztuke (EquipmentElement z modyfikatorem) i oddaje ja przy "Take back"; CHANGELOG 177-3 "Ryzyko": przed cofnieciem DLL odebrac zlecenia; magazyn wojenny Spoils w spisie (miejsce "nie do zmiany") |
| 13 | BSC nieobecny; TXP i Spoils to realne kolizje | **PRAWDA** | odnotowane (tabela K: K22, K23) |
| 14 | odwolanie :1048; `Tier6CastleSteel` po wczytaniu; smierc gracza | **PRAWDA** (a, b), **CZESCIOWO** (c - patrz 24) | opis MCM "takes effect after reloading the game"; smierc gracza - patrz 24 |
| 15 | wydajnosc | **PRAWDA** (uwagi drobne) | spis raz na dobe, jedno przejscie (bohaterowie, tabory, osady, magazyny); zmarli tylko przy pierwszym odzysku; magazyny DTE AI liczone w spisie zaraz po `SweepAiArmories` |
| 16 | VS znika z ZYWYCH: przebranie wladcy, `Wear` wypychal Longclaw | **PRAWDA** - `NPCEquipmentsCampaignBehavior.OnRulingClanChanged` -> `EquipmentHelper.AssignHeroEquipmentFromEquipment` nadpisuje 12 slotow nowego I starego wladcy; `SlotFor` zwracal `Weapon0` | prefiks/postfiks na `AssignHeroEquipmentFromEquipment` (Priority.Last, po CrashScribe `DressedOrNot`): unikat i VS wracaja w swoj slot; `Wear` nigdy nie wypycha unikatu; `OnPartyDestroyed` (pkt 3); diagnoza w 4.4/4.5 poprawiona; odzysk przy pierwszym spisie (pkt 19) |
| 17 | Maegor: cywilna Despair w szablonie | **PRAWDA** - `notablesROT.xml` `valyrian_thief` (occupation Wanderer), `assist_sword` w Item0 obu zestawow | `SweepTemplates` czysci tez zestawy cywilne nie-bohaterow; spis = straznik (nadwyzka -> `ReplacementFor`, linia "Z NICZEGO") |
| 18 | miary z `ItemType` zle | **PRAWDA** - dlugosci klng z `ROT_crafting_pieces.xml` sprawdzone: Lod 140.3, Heartsbane 114.5, Blackfyre 111, Brightroar(2) 108, Dark Sister 109 (ale 1H), Longclaw 103, Despair 100, Orphanmaker 96.89, Nightfall 92, `valsteelblade_2` 89.7 (type 2-4 i 8); skala 100 | miara z klingi: dwureczny i klinga >= `ValyrianGreatswordMinCm` 105 cm = 2, reszta 1; zlecenie "dwa w jeden" usuniete |
| 19 | baza = stan pierwszego wczytania; Vigilance x3; szacunek zamiast pomiaru | **PRAWDA** | rejestr ze stanu startowego ROT: `BasicCharacterObject._equipmentRoster` (bojowy+cywilny) bohaterow zywych, nieaktywnych i zmarlych po starcie, PRZED `SweepTemplates` (ta podmienia wspolne obiekty `Equipment` w pamieci; `_equipmentRoster` nie jest zapisywany - po kazdym wczytaniu swiezy z XML); nazwana najwyzej 1 (Vigilance zostaje u glowy rodu Hightower, dwie pozostale -> zwykla stal); seryjne - tyle, ilu bohaterow (wspolny szablon raz); dla zapisu Jeffa odzysk: z poleglych do dziedzica, potem odtworzenie u wlasciciela startowego albo dziedzica; dokladna liczba - linia startowa |
| 20 | T7 przepuszcza ubytek | **PRAWDA** | T7: liczba klng = rejestr, "UBYTEK" = 0 (to `Log.Error`), "Z NICZEGO" tylko z nowych kopii |
| 21 | AI tylko gracz - uzasadnienie odwrocone (Tywin) | **PRAWDA** | sciezka AI z ta sama regula (pkt 5.7 poprawiony) |
| 22 | oplata i wartosc dopasowane | **PRAWDA** | wartosc VS = miary x `HistValyrianPerMeasure` 60 000 (kotwica [P]: mediana kiesy glowy rodu w 60. dobie testu 120 dob 58 894 - typowy lord oddalby cala kiese; zakup z polki wymaga 2 x cena); na polce klinga wazy koszt wykonania (bez zalewu podazy kategorii); oplata = praca (7 dni x 4 mistrzow x 10.5 d x poziom plac Qohoru) + oplata cechu 1/16 wartosci stali (monopol jak mlyn dworski i jego miarka - [S]); zdanie o Mocie poprawione (podwojna oplata to cena przyjecia Gendry'ego i milczenia, nie premia monopolu) |
| 23 | TXP | **PRAWDA** | pkt 2 |
| 24 | smierc gracza zostawia ekwipunek na martwym | **NIEPRAWDA** - `HeirSelectionCampaignBehavior.OnBeforePlayerCharacterChanged` kopiuje zestaw bojowy I cywilny starego gracza do `_equipmentsThatWillBeInherited`, a `OnPlayerCharacterChanged` dodaje go do taboru nowej partii; `ApplyHeirSelectionAction` wola `KillCharacterAction.ApplyByDeathMarkForced(Hero.MainHero)` przed zmiana postaci | prawdziwe ryzyka: podwojne przekazanie (nasze dziedziczenie + gra) i dwie klingi z bojowego i cywilnego zestawu. Dziedziczenie pomija `Hero.MainHero`; nasz sluchacz `OnBeforePlayerCharacterChanged` zdejmuje cywilny duplikat VS (gdyby szedl po grze - nadwyzke zlapie straznik) |
| 25 | robocizna i wegiel mistrzow z niczego | **PRAWDA** | godziny pracy z puli kowali Qohoru (`SmithHours.Use`, najwyzej `QohorCrew x WorkHoursPerManDay` na dobe); zajete rece = praca wolniej (godziny odkladane na zlecenie), nie stoi; wegiel `QohorCharcoalPerMeasure` na miare z polki Qohoru przy pierwszym dniu, brak = czeka |
| 26 | dni dopasowane | **PRAWDA** | `QohorDays` 7 dla kazdego zlecenia [S] - "skrot gry na zyczenie Jeffa; w ksiazkach miesiace" |
| 27 | etykiety kanonu; Euron's Axe | **PRAWDA** | Brightroar - "ROT (w kanonie zaginiony)"; Longclaw - "poltorak"; `euron_axe` poza spisem VS (zostaje legenda - stara regula); "Castle-forged" - termin AWOIAF, cytatu z ksiazek nie potwierdzono |
| 28 | zamiennik pojmanego z niczego | **CZESCIOWO** - prawda, ale proponowany pusty slot rozbraja lorda AI na stale (zaden system nie dozbraja bohaterow AI) | zamiennik najpierw z taboru partii pojmanego (gdy jest), dopiero potem jak dotad z niczego - liczony w linii "Unikaty (177)"; pelne zamkniecie = zakupy sprzetu bohaterow AI - **D6 / pytanie do Jeffa** |

Rzeczy nowe, znalezione przy wdrozeniu (poza lista):
- **N1**: postac gracza (i jego rod) jako dziedzic - klinga idzie do taboru gracza z komunikatem, nie zaklada sie sama na towarzysza (gracz decyduje).
- **N2**: klinga w magazynie DTE partii AI to zawsze kopia albo przedmiot przeniesiony - w kolejce straznika ostatnia (najpierw zamieniana); zachowana idzie do wodza tej partii.
- **N3**: `UniqueSpoils.OnDailyTickParty` (zakup z polki) i `Take` korzystaja z nowego `Wear` (bez wypychania unikatu) - to samo zachowanie dla wszystkich unikatow.
- **N4**: wycena VS ustawiana PO przeliczniku popytu kategorii i PO wagach polki - przelicznik broni miast liczy klinge jak przed 177 (inaczej 29 wzorow z ln(200 tys./60 tys.) przesunelyby popyt na cala kategorie broni).

Tabela K uzupelniona (rozdz. 6): **K21** czystka menu (`CleanseTrashInBags`) vs unikaty - pkt 1; **K22** TournamentsXPanded (nagroda bez filtra) - pkt 2; **K23** Spoils of War (loteria z zestawu bohatera, symulacja) - pkt 2; **K24** gra: przebranie wladcy (`AssignHeroEquipmentFromEquipment`) - pkt 16; **K25** gra: lup bez NotMerchandise i zniszczenie partii - pkt 3; **K26** gra: nastepca gracza dziedziczy ekwipunek (SandBox) - pkt 24; **K27** CrashScribe `DressedOrNot` (ten sam cel co K24, nasz Priority.Last).

Dalsze (poza 177, zapisane - nie robione): **D4** czystka olbrzymow i egzotycznych wierzchowcow w magazynach DTE AI martwa (wlasciciel `null`) - ozywienie = kasowanie w nicosc, decyzja Jeffa; **D5** unikatowa ZBROJA zdobyta (`Take`), kupiona z polki albo przebrana zostaje zdjeta przy wczytaniu przez `UniqueLaw.SweepHeroes` (`MayWear`) - lista prawowitych noszacych w zapisie; **D6** zamiennik pojmanego z niczego - zakupy sprzetu bohaterow AI.
