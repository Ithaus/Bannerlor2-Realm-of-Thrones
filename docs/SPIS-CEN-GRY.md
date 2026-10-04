# Spis cen gry - co trzeba przeskalowac (2026-10-04, AUDYT - nic nie zmienione)

Zamowienie: przeskalowac CALA ekonomie swiata do wartosci historycznych, **1 denar = 1 pens angielski**
(Anglia ok. 1300-1350, patrz `docs/AUDYT-CEN.md`). Ten plik to pelny spis: ceny przedmiotow (1),
stale i wzory zlota w vanilla i modach (2), mechanizmy i ryzyka (3).

Zrodla:
- `Documents/.../CrashScribe/items-dump.csv` (sesja 2026-10-04 09:01, 3569 przedmiotow, wartosc = `ItemObject.Value`
  PO naszych prawach - widac juz ceny MaterialLaw dla wegla i sztabek). Kategorie towarow dobrane z XML
  (`Modules/*/ModuleData`) i z kodu (`DefaultItems`, `BKItems`).
- `economy-2026-10-04_09-02-34.csv` (stan swiata w dniu 1 nowej kampanii).
- Kod zdekompilowany (scratchpad: tw, sb, sbv, core, bk, bkrot, be, dte, rot, nv, rbl, sow) i nasze `src`.
- Sciezki kodu gry podaje wzgledem katalogu dekompilacji (np. `tw/TaleWorlds.CampaignSystem.GameComponents/X.cs:12`).

Skrot klas skalowania (uzyty w czesci 2):
- **OK** - juz mniej wiecej historyczne przy 1 d = 1 pens (zold, dochody rodow, konie, bydlo, okupy lordow RC).
- **S10** - do podzielenia ok. 5-20x (wiekszosc zbroi/broni srednich tierow, cen od `Value` sprzetu).
- **S50** - do podzielenia ok. 30-100x (luki, helmy, tania bron, nagrody liczone od cen sprzetu).
- **WZOR** - wartosc liczona od `Value` przedmiotu albo od zoldu; zmieni sie sama po przeskalowaniu zrodla
  (trzeba tylko sprawdzic, czy nie ma stalej obok).
- **PROG** - prog/decyzja AI na absolutnej kwocie; po zmianie skali trzeba przeliczyc razem z reszta.
- **?** - do decyzji Jeffa (nie ma oczywistej skali historycznej, np. smoki, miecze valyrianskie).

---

## 1. Ceny przedmiotow

### 1.0 Jak gra ustala `ItemObject.Value`

| Droga | Gdzie | Kogo dotyczy |
|---|---|---|
| Atrybut XML `value="..."` | `core/ItemObject.cs:479` (Item) i `:674` (CraftedItem): `Value = int.Parse(node.Attributes["value"])` | towary SandBoxCore/NavalDLC (26 szt.), luki i kusze ROT (26+13), konie szlachetne ROT (10000/20000/30000), rydwany 22000, 37 broni kutych z `value`, zwierzeta BK (wol 300) |
| `DetermineValue()` -> `ItemValueModel.CalculateValue` | `core/ItemObject.cs:483/678/950-952`; model `TaleWorlds.Core.DefaultItemValueModel` (zdekompilowany ilspycmd) | **cala reszta**: 930 zbroi tulowia, 556 helmow, 274 nogi, 229 rak, 327 peleryn, 190 tarcz, 113 rzedow konskich, cala amunicja, bron kuta bez `value`, konie bez `value` |
| `ItemObject.InitializeTradeGood(item, ..., value, weight, ...)` | `core/ItemObject.cs:334-340`; wolane z `tw/TaleWorlds.CampaignSystem/DefaultItems.cs:119-134` (zboze 10, mieso 30, deski 180, filc 230, skory 50, narzedzia 250, ruda 50, drewno 25, wegiel 50, sztabki 20/30/60/100/160/260) i `bk/BannerKings.Managers.Items/BKItems.cs:314-462` | towary vanilla z kodu i wszystkie towary BK |
| **BKROTPatch dzieli ceny towarow BK przez 100** | `bkrot/BKROTPatch.Patches/BKItemsInitializePatch.cs:87` `value /= 100;` (prefiks na `BKItems.InitializeTradeGood`) | WSZYSTKIE towary BK: chleb 20->0, jajka 5->0, miod 28->0, wapien 50->0, ciasto 30->0, garum 35->0, papirus 60->0, atrament 200->2, miod pitny 120->1, marmur 150->1, ruda zlota 400->4, sztabka zlota 1000->10, klejnoty 50000->500, ksiegi 1000->10 (ksiegi wiary ROT 1000 bez podzialu), **futro 125->1** (`BKItems.cs:442`, bezwarunkowo nadpisuje XML 1). Wiele towarow ma dzis `Value = 0`. |
| Nadpisanie w locie przez refleksje | ROT `rot/ROT.CampaignBehaviors/ROTRBMCompatibility.cs:155` (`<Value>k__BackingField`): dragonbone_bow 100000, weirwood_bow 90000, goldenheart_longbow 130000, giant_bow 200000, ravens_teeth_longbow 150000, giant_arrows 12000, ravens_teeth_arrows 11000 (tylko z RBM) | 7 przedmiotow ROT |
| Nadpisanie w locie przez setter | nasze `Armoury/src/MaterialLaw.cs:52-71` (`AccessTools.PropertySetter(typeof(ItemObject),"Value")`) | wegiel 9, surowka 87, zelazo kute 118, zelazo 157, stal 210, stal szl. 281, stal valyrianska 1000 |

Wzor vanilla (`DefaultItemValueModel.CalculateValue`):
`Value = mnoznik x 2.75^Tierf x (1 + 0.2 x (Appearance - 1)) + 100 x max(0, Appearance - 1)`,
mnoznik 120 dla tulowia/rak/nog, 100 dla reszty (helmy, peleryny, bron, konie, rzad, towar, sztandar);
`Tierf` z pancerza (`0.1 x (1.2 head + body + leg + arm) x mnoznik typu - 0.4`), z obrazen broni, z konia
(`0.12 speed + 0.07 maneuver + 0.01 hp + 0.15 charge - 11.5`). **Cena rosnie wykladniczo z tierem (x2.75 na tier)**,
dlatego t6 kosztuje 2.75^5 = 157 x t1 - stad roznice 10-200x wobec historii (historycznie plyta ~25x kaftana).

`ItemObject.Value` ma prywatny setter (`core/ItemObject.cs:110` `public int Value { get; private set; }`) -
mozna go zmienic w locie (AccessTools.PropertySetter jak MaterialLaw albo pole `<Value>k__BackingField` jak ROT).
Wartosc NIE jest zapisywana w save (przedmioty to obiekty MBObjectManager wczytywane z XML przy kazdym starcie) -
zmiana w locie dziala na kazdym zapisie, ale trzeba ja powtarzac przy kazdym starcie gry.

**Uwaga**: nasza `ArmsPricing` (Armoury) NIE zmienia `Value` - zmienia tylko cene na targu (postfiks
`GetPrice`, `ArmsPricing.cs:338` `m *= BaseOf(it) / it.Value`). Wszystko, co liczy od `Value` (lupy, doplata DTE,
okupy od ekwipunku, rekrutacja z kosztem sprzetu, nagrody turniejowe, AI oceniajace sprzet, kowalstwo, naprawy),
dalej widzi stare ceny.

### 1.1 Towary handlowe (Goods, ItemCategory, wartosc za jednostke)

Kolumny: kategoria | id | wartosc teraz | waga jednostki (kg) | wartosc/kg | skad cena | uwagi historyczne
(pensy, Anglia ok. 1300-1340, przyblizenia z literatury jak w AUDYT-CEN, do sprawdzenia).

**Zywnosc** (IsFood; liczy sie do zapasow partii):

| Kategoria | id | Wartosc | kg | d/kg | Skad | Historycznie (10 kg) |
|---|---|---|---|---|---|---|
| grain | grain | 10 | 10 | 1.0 | kod DefaultItems:119 | pszenica ~5-6 s/kwarter (~220 kg) -> ok. 3 d/10 kg. **Gra ~3x** |
| fish | fish | 12 | 10 | 1.2 | XML SandBoxCore | sledz solony ~ 4-6 d/10 kg. ok. 2x |
| meat | meat | 30 | 10 | 3.0 | kod :120 | ok. 10-15 d/10 kg. ok. 2-3x |
| cheese | cheese | 40 | 10 | 4.0 | XML | ~ 0.5 d/funt -> ok. 10 d. ok. 4x |
| butter | butter | 25 | 10 | 2.5 | XML | ~ 1 d/funt -> ok. 20 d. ok. 1x |
| grape | grape | 20 | 10 | 2.0 | XML | (owoce) ok. 1-2x |
| olives | olives | 30 | 10 | 3.0 | XML | import, ok. 1-3x |
| date_fruit | date_fruit | 50 | 10 | 5.0 | XML | import |
| beer | beer | 50 | 10 | 5.0 | XML | ale 1-1.5 d/galon -> ok. 3-4 d/10 l. **ok. 12x** |
| wine | wine | 90 | 10 | 9.0 | XML | wino gaskonskie 3-4 d/galon -> ok. 8-10 d/10 l. **ok. 10x** |
| (BK) bread | bread | 0 | 10 | 0 | BKItems:330 /100 | chleb farthing/bochenek -> 10 kg ok. 10 d. **zepsute (0)** |
| (BK) pie, apple, orange, carrot, honey, garum, Egg | ... | 0 | 10 | 0 | BKItems /100 | **zepsute (0)** |
| (BK) mead | mead | 1 | 10 | 0.1 | BKItems:348 /100 | |
| (BK) WhaleMeat | WhaleMeat | 50 | 10 | 5.0 | BKItems:318 (ItemObject.InitializeTradeGood bez /100) | |

**Surowce i polprodukty**:

| Kategoria | id | Wartosc | kg | d/kg | Skad | Uwagi |
|---|---|---|---|---|---|---|
| hardwood | hardwood | 25 | 10 | 2.5 | kod :126 | drewno opalowe/budowlane ~ 1-2 d za 10 kg. ok. 1-2x |
| iron | iron (ruda) | 50 | 10 | 5.0 | kod :125 | ruda ~ 1-2 d za 10 kg; zelazo kute 1-2 d/funt. ruda ok. 3x |
| iron | ironIngot1-6 | 87/118/157/210/281/1000 | 0.5 | 174-2000 | MaterialLaw (vanilla 20/30/60/100/160/260) | zelazo kute ~ 2-4 d/kg, stal ~ 6-10 d/kg. **sztabki 20-50x za drogie** (MaterialLaw liczyl lancuch od drogiego wegla/rudy gry) |
| hardwood | charcoal | 9 | 0.5 | 18 | MaterialLaw (vanilla 50) | wegiel ~ 1 d za kilka kg. ok. 10-20x |
| hides | hides | 50 | 10 | 5.0 | kod :123 | skora wolowa surowa 1-2 s szt. (~20 kg) -> ok. 6-12 d/10 kg. ok. 1x |
| leather | leather | 230 | 10 | 23 | XML | garbowana ok. 2-3x skory -> ok. 30 d. **ok. 8x** |
| flax | flax | 15 | 10 | 1.5 | XML | ok. 1-2x |
| linen | linen | 245 | 10 | 24.5 | XML | plotno 2-4 d/lokiec (~0.3 kg) -> 10 kg ok. 70-130 d. ok. 2-3x |
| wool | wool | 22 | 10 | 2.2 | XML | welna 4-6 L/worek (165 kg) -> ok. 70 d/10 kg. **gra ok. 3x ZA TANIO** |
| cloth | (brak przedmiotu) | - | | | | |
| cotton | cotton ("Raw Silk") | 80 | 10 | 8.0 | XML | jedwab surowy: bardzo drogi (kilka s/funt). za tanio |
| velvet | velvet | 575 | 10 | 57.5 | XML | aksamit/sukno luksusowe 3-10 s/lokiec -> tysiace d/10 kg. **za tanio** |
| fur | fur | **1** | 10 | 0.1 | BKItems:442 (125)/100 | futro popielic: setki d. **zepsute** |
| felt | felt | 230 | 10 | 23 | kod :122 | |
| planks | planks | 180 | 10 | 18 | kod :121 | deski: kilka d/10 kg. **ok. 30x** |
| tools | tools | 250 | 10 | 25 | kod :124 | lopaty/siekiery 3-12 d szt. -> 10 kg ok. 30-60 d. **ok. 5x** |
| clay | clay | 18 | 10 | 1.8 | XML | |
| pottery | pottery | 210 | 10 | 21 | XML | garnek 0.5-1 d -> 10 kg ok. 10 d. **ok. 20x** |
| salt | salt | 40 | 10 | 4.0 | XML | sol 1-2 d/buszel (~25 kg). **ok. 5-10x** |
| silver | silver (ruda) | 100 | 10 | 10 | XML | |
| oil | oil | 290 | 10 | 29 | XML | oliwa importowana: ok. 1-2x |
| jewelry | jewelry | 675 | 10 | 67.5 | XML | (kategoria luksusu, brak skali) |
| spice | spice | 300 | 10 | 30 | XML SandBoxCore (BK 300 /100 nie dotyczy - XML wygrywa) | pieprz 1-2 s/funt -> 10 kg ok. 500-1000 d. **za tanio** |
| medicine | poppy | 900 | 1 | 900 | XML ROT, nie na handel | |
| whale_oil / walrus_tusk | whale_oil 200 / walrus_tusk 400 | | 10 | 20/40 | XML NavalDLC | |
| (BK) Ink 2, PurpleDye 5, Papyrus 0, Limestone 0, Marble 1, gold_ore 4, goldingot 10, pouchofgems 500 | | | 10 (zloto 0.5, klejnoty 1) | | BKItems /100 | **zepsute** (zloto 10 za sztabke 0.5 kg - ok. 2000x za tanio) |
| (BK) ksiegi | book_* | 10 (BK) / 1000 (ROT wiary) / 1500 (slowniki ROT) | 1-2.5 | | BKItems /100; BKROTItems.cs:273-343 | ksiega ~ 1-10 L (240-2400 d) |
| trash, stolen_goods, dragon_thumbnail | | 1 | | | | techniczne |

Wniosek dla towarow: **wiekszosc jest w skali 1-10x** (zboze, ryby, mieso, skory, drewno, oliwa dobrze;
piwo, wino, deski, ceramika, skora garbowana 8-30x za drogie; welna, jedwab, aksamit, przyprawy, futro, zloto ZA TANIE).
Towary BK sa zepsute przez `/100` BKROTPatch (wiele ma 0 - nie da sie ich sprzedac ani kupic sensownie).

### 1.2 Zwierzeta i konie

| Typ | Kategoria | id | Tier | Wartosc | Waga | Handel | value w XML | Historycznie |
|---|---|---|---|---|---|---|---|---|
| Animal | Oxen | ox | 1 | 300 | 200 | T | 300 (BK items.xml) | wol ok. 160 d (13 s). 2x |
| Animal | cow | cow | 2 | 200 | 200 | T | 200 | krowa 120 d (10 s). 1.7x |
| Animal | hog | hog | 1 | 60 | 120 | T | 60 | swinia 30-40 d. 1.5-2x |
| Animal | sheep | sheep | 2 | 80 | 40 | T | 80 | owca 12-18 d. **5x** |
| Animal | animal | goose / chicken | 2 | 50 | 5 | T | 50 | ges 3-4 d, kura 1 d. **15-50x** |
| Animal | animal | cat / dog | 3 | 20 | | N | 20 | |
| Horse | sumpter_horse | sumpter_horse, mule, pack_camel | 1 | 99 | 150-450 | T | SandBoxCore 110-220, ale RBM `RBMCombat_horses.xml` nadpisuje bez `value` -> wzor 99 | kon juczny 60-120 d. OK |
| Horse | sumpter_horse | saddle_horse | 1 | 140 | 420 | T | 140 | kon wierzchowy 120-240. OK |
| Horse | horse | palfreys/rounseys kultur (6) | 3-4 | 929-1258 | 320-440 | T | brak (wzor) | rouncey 240-1200 (1-5 L). OK-2x |
| Horse | horse | camel 1177, hunter 5980, wight_horse 2240 | 3-5 | | | T | wzor | |
| Horse | war_horse | t2_*_horse (6) | 4 | 1221-2380 | 300-475 | T | wzor | courser 5-20 L = 1200-4800. OK |
| Horse | war_horse | charger 10973, war_camel 9719 | 5-6 | | | T | wzor | destrier 40-80 L = 9600-19200. OK |
| Horse | noble_horse | t3_*_horse (6) | 4-5 | 10000 | 430-550 | T | 10000 | destrier. OK |
| Horse | noble_horse | noble_horse_* (6) | 5-6 | 20000 | | N | 20000 | wielki destrier krola 80-100 L. OK |
| Horse | horse/noble | unicorn1-3, zorse | 5-6 | 30000 | | T | 30000 | fantastyka - ? |
| Horse | horse | chariot1-6 | 2 | 22000 | | T | 22000 | **?** (rydwan dla tier 2 drozszy niz destrier) |
| Horse | horse | elephant 121, mammoth 748, smoki 552-643 (N) | 1-3 | | | | wzor | **? - absurdalnie tanie** (smok tanszy od konia wierzchowego) |

Uwaga: kolumna `value w XML` - wartosc z ktoregokolwiek pliku (SandBoxCore, ROT-Content `items.xml`, RBM). RBM `RBMCombat_horses.xml` (50 koni) i `RBMCombat_*` (zbroje, tarcze, strzaly) laduja sie po SandBoxCore i w wiekszosci nie maja `value` - cena idzie ze wzoru.

Podsumowanie koni na handel: juczne mediana 99, wierzchowe/ROT "horse" mediana 4110 (rozrzut przez rydwany i jednorozce),
wojenne mediana 2206 (1221-10973), szlachetne 10000-30000. **Konie i bydlo trzymaja skale (1-2x)**, wyjatki: owca 5x,
drob 15-50x, oraz fantastyka (rydwany, smoki, jednorozce, slon, mamut) bez sensownej skali.

Uprzaz konska (HorseHarness, wzor; 113 szt.): t0 med 1098 (siodlo), t2 3905, t3 4634, t5 7168, t6 8255 (med. waga 23-140 kg).
Historycznie siodlo 2-5 s (24-60 d), kropierz/barding 2-10 L. **ok. 20-50x za drogie (siodla), 3-10x (bardy)**.

### 1.3 Bron, zbroje, tarcze, amunicja (merchandise=1, typ x tier wyswietlany)

Tier = `(int)item.Tier + 1` (CrashScribe tak zapisuje, jak `Recipes.Grade`); "0" = ponizej t1 (Tierf < 0).
d/kg = mediana wartosc/waga dla sztuk. Historyczne odniesienia z `AUDYT-CEN.md`.

| Typ | Tier | Szt. | Mediana | Min | Max | Waga med. (kg) | d/kg med. |
|---|---|---|---|---|---|---|---|
| BodyArmor | 0 | 199 | 370 | 305 | 4400 | 0.50 | 785 |
| BodyArmor | 1 | 89 | 1469 | 501 | 12992 | 1.60 | 667 |
| BodyArmor | 2 | 40 | 2255 | 699 | 17480 | 6.00 | 616 |
| BodyArmor | 3 | 61 | 3054 | 2270 | 23240 | 10.00 | 311 |
| BodyArmor | 4 | 83 | 3906 | 3284 | 26360 | 14.00 | 866 |
| BodyArmor | 5 | 64 | 22200 | 3930 | 35804 | 18.00 | 1098 |
| BodyArmor | 6 | 123 | 40316 | 4535 | 61772 | 33.00 | 1237 |
| HeadArmor | 0 | 80 | 180 | 136 | 802 | 0.50 | 504 |
| HeadArmor | 1 | 24 | 806 | 256 | 6004 | 1.30 | 774 |
| HeadArmor | 2 | 35 | 7732 | 1063 | 9532 | 1.20 | 6065 |
| HeadArmor | 3 | 41 | 10036 | 1279 | 13060 | 1.50 | 6883 |
| HeadArmor | 4 | 112 | 14860 | 11188 | 19540 | 1.50 | 9675 |
| HeadArmor | 5 | 124 | 17164 | 13348 | 24004 | 2.50 | 7073 |
| HeadArmor | 6 | 92 | 22744 | 10000 | 28612 | 3.00 | 7666 |
| LegArmor | 0 | 10 | 222 | 100 | 300 | 0.70 | 288 |
| LegArmor | 1 | 20 | 352 | 155 | 435 | 0.90 | 383 |
| LegArmor | 2 | 89 | 525 | 205 | 600 | 1.30 | 380 |
| LegArmor | 3 | 11 | 3595 | 615 | 5355 | 1.80 | 2031 |
| LegArmor | 4 | 12 | 6375 | 5595 | 6555 | 2.10 | 3039 |
| LegArmor | 5 | 36 | 7395 | 975 | 7635 | 3.00 | 2288 |
| HandArmor | 1 | 11 | 302 | 134 | 1458 | 0.60 | 663 |
| HandArmor | 2 | 51 | 374 | 150 | 2226 | 1.80 | 241 |
| HandArmor | 3 | 13 | 3890 | 198 | 4274 | 1.40 | 2514 |
| HandArmor | 4 | 42 | 4946 | 710 | 5330 | 2.00 | 2379 |
| HandArmor | 5 | 39 | 5618 | 3762 | 6098 | 2.50 | 2341 |
| Cape | 0 | 23 | 82 | 74 | 1010 | 0.50 | 156 |
| Cape | 1 | 54 | 106 | 94 | 1586 | 1.55 | 98 |
| Cape | 2 | 23 | 314 | 138 | 2162 | 2.00 | 209 |
| Cape | 3 | 24 | 380 | 338 | 2642 | 2.30 | 190 |
| Cape | 4 | 27 | 458 | 410 | 3506 | 2.50 | 223 |
| Cape | 5 | 10 | 3842 | 554 | 4178 | 2.75 | 993 |
| Cape | 6 | 148 | 8978 | 3122 | 13394 | 4.00 | 2199 |
| OneHandedWeapon | 1 | 13 | 548 | 467 | 664 | 0.61 | 849 |
| OneHandedWeapon | 2 | 35 | 858 | 676 | 1185 | 0.95 | 924 |
| OneHandedWeapon | 3 | 36 | 1950 | 1273 | 2729 | 1.08 | 1773 |
| OneHandedWeapon | 4 | 34 | 4823 | 2923 | 6659 | 1.15 | 4070 |
| OneHandedWeapon | 5 | 42 | 11384 | 7178 | 17859 | 1.23 | 8504 |
| OneHandedWeapon | 6 | 18 | 20832 | 19206 | 33326 | 1.40 | 17192 |
| TwoHandedWeapon | 2 | 6 | 1344 | 1292 | 1810 | 1.10 | 1300 |
| TwoHandedWeapon | 3 | 3 | 2779 | 2038 | 2841 | 1.30 | 2059 |
| TwoHandedWeapon | 4 | 17 | 7057 | 4431 | 10148 | 1.29 | 5503 |
| TwoHandedWeapon | 5 | 24 | 20723 | 11912 | 26207 | 1.57 | 12927 |
| TwoHandedWeapon | 6 | 4 | 39286 | 31062 | 42162 | 1.25 | 31404 |
| Polearm | 0-2 | 6 | 134-271 | 127 | 282 | 0.6-1.8 | ~200 |
| Polearm | 3 | 5 | 717 | 518 | 757 | 1.03 | 696 |
| Polearm | 4 | 18 | 986 | 862 | 1293 | 1.06 | 953 |
| Polearm | 5 | 51 | 3860 | 2348 | 5561 | 1.70 | 2101 |
| Polearm | 6 | 22 | 6729 | 5589 | 8692 | 1.94 | 3364 |
| Thrown | 1-2 | 12 | 146-169 | 120 | 208 | 0.8-0.9 | ~200 |
| Thrown | 4-5 | 20 | 1482-2698 | 1413 | 4432 | 0.6-0.9 | ~3000 |
| Shield | 1 | 7 | 171 | 166 | 199 | 4.00 | 46 |
| Shield | 2 | 29 | 274 | 223 | 358 | 4.70 | 65 |
| Shield | 3 | 45 | 568 | 394 | 811 | 4.70 | 123 |
| Shield | 4 | 35 | 1374 | 835 | 1982 | 4.70 | 252 |
| Shield | 5 | 22 | 3852 | 2346 | 5192 | 4.70 | 822 |
| Shield | 6 | 45 | 15166 | 6063 | 15166 | 4.00 | 3475 |
| Bow | 0 | 2 | 650 | 600 | 700 | 0.30 | 2167 |
| Bow | 1 | 3 | 1800 | 616 | 2700 | 0.30 | 6160 |
| Bow | 2 | 3 | 3000 | 2200 | 3300 | 0.30 | 10000 |
| Bow | 3 | 4 | 3225 | 2400 | 3750 | 0.40 | 8750 |
| Bow | 4 | 3 | 4500 | 3900 | 6075 | 0.50 | 9000 |
| Bow | 5 | 4 | 14975 | 4350 | 90000 | 0.50 | 20575 |
| Bow | 6 | 6 | 115000 | 4800 | 200000 | 0.30 | 520000 |
| Crossbow | 0 | 10 | 4500 | 1000 | 10000 | 1.60 | 2000 |
| Crossbow | 1-2 | 2 | 10000-20000 | | | 2.0-2.2 | |
| Sling | 6 | 3 | 600 | 300 | 1000 | 0.10 | |
| Arrows (stos) | 0-2 | 17 | 40-100 | 33 | 144 | 0.04-0.06 (szt.) | |
| Arrows | 3-4 | 7 | 230-709 | 175 | 894 | 0.08 | |
| Arrows | 5-6 | 16 | 2056-7202 | 1325 | 12000 | 0.10-0.11 | |
| Bolts | 0-3 | 9 | 33-229 | 33 | 257 | 0.03-0.08 | |
| Bolts | 4-6 | 14 | 549-7202 | 378 | 7202 | 0.08-0.14 | |
| SlingStones | 1-4 | 15 | 57-601 | | | | |
| HorseHarness | patrz 1.2 | 105 | | | | | |

Poza handlem (merchandise=0): 272 zbroi tulowia (mediana 200 - stroje cywilne/nagie ciala), 86 broni 1h (mediana 26 646),
27 broni 2h (mediana 42 085). **41 przedmiotow >= 100 000**: miecze valyrianskie i lore (longclaw, ice, oathkeeper,
dawn, blackfyre, darksister, heartsbane... 150 000-350 000), luki lore (dragonbone 100 000, goldenheart 130 000,
giant 200 000). To "prestiz" - **?** do decyzji (1 000 L za miecz valyrianski moze zostac jako cena legendy).

Skala wzgledem historii (z AUDYT-CEN): helmy 30-200x, luki 100-2000x, bron 20-90x, zbroje tulowia 10-25x,
tarcze 15-30x, strzaly 3-4x, peleryny t0-t4 ok. 1-5x (cywilne ubrania mniej wiecej OK), t6 duzo za drogie.
Historyczny cel (AUDYT-CEN): komplet t1 ~150-300 d, t3 ~600-1 500, t6 ~4 000-8 000.

Przyczyna: wzor `2.75^Tierf` (x157 miedzy t1 a t6) zamiast historycznego x20-40; luki ROT maja wpisane w XML
`value` ok. 600-6000 dla t1-t4 (historycznie 12-60 d).

---

## 2. Stale i wzory zlota

**STAN: CZESCIOWY.** Pelne przeszukanie vanilla (zadania, turnieje, karawany, warsztaty, lapowki, barter, budowy)
i modow BK/BKROT/BEE/DTE/SoW/RBL/ROT/NavalDLC zlecilem dwom agentom pomocniczym; ich wyniki NIE dotarly przed
oddaniem raportu. Ponizej tylko to, co sprawdzilem sam w kodzie lub w logu. Sekcje 2.2-2.3 i reszte 2.1 trzeba dopisac.

### 2.1 Vanilla - sprawdzone

| Plik:linia | Stala / wzor | Za co | Klasa |
|---|---|---|---|
| `tw/TaleWorlds.CampaignSystem.GameComponents/DefaultSettlementEconomyModel.cs:77-78` | `num = 10000 + Prosperity * 12 - Gold; return Round(0.25 * num)` | dzienna zmiana zlota miasta (cel 10000 + 12 x prosperity) | OK w skali zoldu; PROG dla skupu lupow |
| `DefaultSettlementEconomyModel.cs:61-72` | popyt = `BaseDemand x prosperity + LuxuryDemand x max(0, prosperity - 3000)` | popyt miasta w ZLOCIE | patrz 3.2 - skalowac razem z Value |
| `DefaultSettlementEconomyModel.cs:35` | `ProsperityLuxuryTreshold = 3000` | prog luksusu | OK |
| `core2/DefaultItemCategories.cs:349-413` | `BaseDemand`/`LuxuryDemand` kategorii (np. zboze 140/0, piwo 46/20, bron 9..4 / 7..10, zbroje 9..3 / 15..17) | popyt kategorii | skalowac razem z Value kategorii |
| `tw/.../DefaultTradeItemPriceFactorModel.cs:15,17,176-179` | factor 0.1-10, `(demand/(0.1 supply + 0.04 inStore + 2))^0.6` | cena na targu | patrz 3.2 |
| `DefaultTradeItemPriceFactorModel.cs:200` | `GetTheoreticalMaxItemMarketValue = Value x 10` | | WZOR |
| `tw/.../DefaultPartyTroopUpgradeModel.cs:76-81` | awans = (rekrutacja celu - rekrutacja zrodla, bez sprzetu) / 2 (najemnicy / 3) | koszt awansu | WZOR od kosztu rekrutacji |
| `tw/.../DefaultSmithingModel.cs:122,127,132,137` | XP: przetop 0.3 x Value, wytop 0.02 x Value, kucie 0.02 x Value, zamowienie 0.1 x Value | XP kowalstwa | WZOR - XP spadnie po skali Value |
| `ClanVariablesCampaignBehavior.DailyTickClan` (stale wg `Armoury/src/KingdomTreasury.cs:9-14,32`) | skarbiec < 2 mln: +1000/dzien; < 1 mln: losowo +100/200/400 tys. | zloto z niczego dla skarbca | juz wylaczone przez Armoury |
| (wg `KingdomTreasury.cs` komentarz) `DefaultClanFinanceModel` | rody AI > 100 tys. wplacaja 1% nadwyzki dziennie | zasilanie skarbca | PROG 100 000 - OK w skali dochodow |
| Start kampanii (log `economy-2026-10-04_09-02-34.csv`, dzien 1) | skarbiec kazdego krolestwa 2 000 000; krol mediana 310 000 (max 355 491); rod krolestwa mediana 155 000 (max 296 867); rod mniejszy 240 000; gracz 1 000 | zloto startowe | **OK** (rod 155 000 d = 650 L = ok. 2 lata dochodu barona; skarbiec 2 mln d = 8 300 L) |
| Dochody (ten sam log) | krol mediana 9 070/dzien, rod 4 221/dzien (model) | dochody | OK (AUDYT-CEN) |
| Zold `DefaultPartyWageModel` x BK BaseWage 1.2546 | t1 2 ... t6 21 (AUDYT-CEN) | zold | **OK** |
| `Armoury/src/Settings.cs:135` (komentarz) | vanilla i BK nie kupuja jedzenia drozszego niz 120 | AI jedzenie | PROG OK |

Do dopisania z wynikow agentow (nie zweryfikowane): wzory `RewardGold` wszystkich zadan, nagrody i zaklady turniejow,
koszt karawany, ceny i wydatki warsztatow, pompa zlota notabli, koszt najmu towarzyszy, koszt rekrutacji i najemnikow,
`DefaultRansomValueCalculationModel`, sprzedaz jencow, lapowki, barter i trybuty, koszty budow, ceny statkow NavalDLC.

### 2.2-2.3 Mody cudze (BK, BKROT, BEE, DTE, SoW, RBL, ROT, NavalDLC)

Sprawdzone osobiscie:
- BKROTPatch `bkrot/BKROTPatch.Patches/BKItemsInitializePatch.cs:87` `value /= 100` - wszystkie towary BK (patrz 1.0).
- ROT `rot/ROT.CampaignBehaviors/ROTRBMCompatibility.cs:46-118,155` - ceny lukow/strzal lore 11 000-200 000.
- BK MCM Jeffa: `BaseWage` 1.2546, `LootScale` 0.5 (wg AUDYT-CEN).
- BK `bk/BannerKings.Managers.Items/BKItems.cs:444` - `ExperimentalPrices` (wylaczone): podmienia ceny ceramiki, lnu, skory, welny, aksamitu, oliwy, piwa, wina, owocow, masla, sera, narzedzi, rudy, skor (`:445-462`).
- BK `bk/BannerKings.Behaviours/BKCaravansBehavior.cs:1580` - wlasny slownik cen karawan (`_priceDictionary`) - cache do sprawdzenia przy zmianie Value.

Reszta (modele BK wage/recruit/ransom/warsztaty/majatki/tytuly/budowy, ustawienia BEE, doplata DTE, SoW, RBL, NavalDLC) - DO DOPISANIA.

### 2.4 Nasze mody (domyslne z `Settings.cs`; MCM Jeffa `Configs/ModSettings/Global/Armoury/Armoury.json` ma te same wartosci tam, gdzie je zapisal - nowsze pola ida z domyslnych)

**Armoury** (`Armoury/src/Settings.cs`)

| Linia | Ustawienie | Wartosc | Za co | Klasa |
|---|---|---|---|---|
| 56 | OrderPayMultiplier | 1.35 | zamowienie lorda = Value x 1.35 | WZOR (od Value) |
| 62 | OrderMaxItemValue | 12000 | sufit wartosci zamawianej sztuki | PROG, S10-S50 (po skali sprzetu ~300-1000) |
| 64-65 | ForgeFeeBase / ForgeFeePerTier | 75 / 60 | oplata za kuznie kowala | OK (dzien kowala ~10-20 d; t6 = 435 d za drogo - S2-S5 dla PerTier) |
| 68 | ForgeDayHours | 8 | "~200 zlota" za dzien najmu kuzni | S10 (historycznie mistrz z pomocnikiem 6-10 d/dzien) |
| 96 | LegendaryValueFloor | 25000 | od tej Value sztuka jest LEGENDA | PROG - przeliczyc razem z cenami |
| 125 | RepairCostFactor | 0.5 | naprawa = 0.5 x Value | WZOR |
| 135 | AiStarvingBuysAnyPrice | (komentarz: vanilla/BK odmawiaja jedzenia > 120 denarow) | | PROG vanilla 120 (czesc 2.1) |
| 171-172 | HideoutGoldBase / PerBand | 150 / 120 | zloto w kryjowce bandytow | OK (kilka szylingow na bande) |
| 198 | LegendaryLootValueFloor | 100000 | bron tej wartosci nie lezy w lupach | PROG |
| 214 | MinSellPercentOfValue | 5 | podloga ceny skupu (% Value) | WZOR |
| 268 | AiMountPurseShare | 0.15 | udzial sakiewki na konie | WZOR |
| 272 | AiMountBreederMarkup | 1.3 | narzut hodowcy | WZOR |
| 336-342 | CharcoalValue 9, CrudeIronValue 87, WroughtIronValue 118, IronValue 157, SteelValue 210, FineSteelValue 281, ValyrianSteelValue 1000 | | ceny surowcow kuzni (MaterialLaw) | **S20-S50** (zelazo kute hist. 2-4 d/kg -> sztabka 0.5 kg ok. 1-2 d; stal 3-5 d; wegiel < 1 d) - liczone lancuchem od rudy 50 i drewna 25 gry, ktore same sa 2-3x za drogie |
| 349 | ArmsPriceBand | 2 | cena = koszt wykucia x [1/B, B] | WZOR |
| 350 | SmithDayWage | 10 | dzien mistrza i pomocnika | OK (historycznie 4-6 d mistrz + 1-2 d pomocnik) |
| 351 | SmithProfitPercent | 25 | zysk warsztatu | OK |
| 363 | TradeTransportPercentPer100 | 5 | koszt transportu % wartosci | WZOR |
| 368-369 | AiGearBudgetPercent 25 / **AiGearGoldReserve 2000** | | lord kupuje sprzet; rezerwa | PROG 2000 - OK w skali zoldu (2000 d = 8 L) |
| 377 | WorkshopWagePerDay | 10 | dzien pracy w warsztacie | OK |
| 393 | PopulationRentPerHead | 40 | renta od mieszkanca na rok | OK (z zalozenia historyczne, 40 d/glowe) |
| 419 | OutlawFenceMarkup | 1.5 | paser: Value x 1.5 | WZOR |
| 421 | OutlawCoinsPerMan | 2 | zloto nowej bandy na glowe | OK |
| 440-441 | CrownDuesPeacePercent 2 / War 10 | | powinnosci wasali (% dochodu) | WZOR |
| 447 | **IronBankCapital** | 5 000 000 | kapital Zelaznego Banku | OK/? (5 mln d = 20 800 L; bank Bardich/Peruzzich mial setki tys. L - mozna podniesc, nie dzielic) |
| 448 | IronBankIncomeDays | 60 | limit pozyczki = 60 dni dochodu | WZOR |
| 451 | IronBankWageDays | 10 | AI pozycza, gdy zloto < 10 dni zoldu | WZOR |
| 469 | SupplyDemandTradePricePercent | 50 | hurt miedzy miastami % Value | WZOR |
| 473-476 | MarketGlut* | % Value | skup lupow | WZOR |
| 530-535 | TroopMendWreckShare 0.10, TroopOrderMarkup 1.15 | | naprawy/zamowienia dla oddzialu (% Value) | WZOR |

Stale w kodzie Armoury (nie w MCM):

| Plik:linia | Stala | Za co | Klasa |
|---|---|---|---|
| `Armoury/src/KingdomTreasury.cs:32` | `1000 / 100000 / 200000 / 400000` -> 0 (transpiler na `ClanVariablesCampaignBehavior.DailyTickClan`) | zloto z niczego dla skarbca | juz wylaczone; start 2 mln zostaje |
| `Armoury/src/PopulationLaw.cs:168` | `gold - (int)(10000f + Prosperity * 12f)` | renta bierze tylko nadwyzke ponad cel zlota miasta (kopia vanilla) | PROG (zmienic razem z celem vanilla) |
| `Armoury/src/Recipes.cs:130` | `it.Value >= MathF.Max(1000f, s.LegendaryValueFloor)` | rozpoznanie legendy | PROG |
| `Armoury/src/ArmsPricing.cs:61-68` | dni pracy per typ x tier (DaysWeapon 1-12, DaysBow 2-12, ...) | koszt wykucia | OK (dni, nie zloto) |
| `Armoury/src/ArmsPricing.cs:338` | `m *= BaseOf(it) / it.Value` | cena targowa z kosztu, NIE zmienia `Value` | patrz 1.0 |
| `CrashScribe/src/Mends.cs:4624` | `if (Hero.MainHero.Gold < 250000) return false;` | szpieg/przekupstwo lorda (RealSpyChance) wymaga 250 tys. w sakiewce | PROG (250 000 d = 1 040 L - za duzo? do decyzji) |

**RealisticCaptivity** (`RealisticCaptivity/src/Settings.cs`)

| Linia | Ustawienie | Wartosc | Za co | Klasa |
|---|---|---|---|---|
| 17 | LootGoldPercent | 60 | % zlota zabrany jencowi | WZOR |
| 19 | BuybackPriceMultiplier | 1.6 | wykup wlasnego sprzetu (Value x 1.6) | WZOR - po skali Value tanieje sam |
| 34 | ParoleRansomDiscount | 0.75 | | WZOR |
| 37 | RansomMultiplier | 5.0 | okup GRACZA = 5 x vanilla | WZOR od vanilla (sprawdzic, czy po skali vanilla ma sens) |
| 38 | RansomRenownFactor | 30 | +30 zlota za punkt renomy | OK (renoma 500 -> 15 000 d = 62 L, rycerz/baron) |
| 49-54 | LordRansomKing 100000, ClanLeader 25000, Lord 8000, PerClanTier 0.25, PerTown 10000, PerCastle 5000 | | okupy lordow wg pozycji | **OK** (lord 8000 d = 33 L = okup rycerza; krol 100 000 d = 417 L - historycznie krol 10-100x wiecej, np. Jan II 3 mln ecu; mozna podniesc krola) |
| 72-75 | HomePriceTown 4000 + 0.5 x prosperity; HomePriceVillage 1500 + 1.5 x hearth | | dom klanowy | OK (dom w miescie 5-30 L = 1200-7200 d) |
| 80 | BanditDumpWorthlessGold | 800 | ponizej tego bandyci nie trzymaja jenca | PROG OK |
| 94-95 | DebtInterest 1.4 / DebtDailyInstalment 250 | | dlug okupu | OK |
| 101 | EscapeBribeGold | 800 | lapowka dla straznika | OK-2x (historycznie kilka szylingow - kilkaset d) |
| 124 | WorkOnlyBelowGold | 2500 | praca tylko przy pustej sakiewce | PROG OK |
| 125-128 | WorkPayVillageBase 15 + hearth/40; WorkPayTownBase 20 + prosperity/300 | | dniowka jenca/robotnika | **ok. 10x za duzo** (robotnik 1-2 d, rzemieslnik 3-4 d dziennie; wies 15 d = 3 tygodnie pracy robotnika) - S5-S10 |
| 133-136 | GuardOnlyBelowGold 10000; GuardPayBase 45 + prosperity/150 | | straz nocna | **ok. 10-20x za duzo** (straznik 1-3 d) |
| `CaptivityBehavior.cs:392` | `if (amount < 1) amount = 1000;` | zapasowy okup | PROG |
| `FairRansom.cs:131` | kurier tylko, gdy placacy ma okup + 1000 | | PROG OK |
| `Rescue.cs:107` | `renown / 2000f` | udzial renomy w cenie odbicia | WZOR |

**GrandTourney** (`GrandTourney/src/Settings.cs`)

| Linia | Ustawienie | Wartosc | Za co | Klasa |
|---|---|---|---|---|
| 26 | LocalPrizeMaxValue | 2000 | lokalna nagroda (przedmiot do tej Value) | PROG od Value - po skali sprzetu da bron t5-t6 (S10) |
| 33-34 | HostBaseFee 2000 + 0.5 x prosperity | | koszt wystawienia turnieju | OK-? (turniej krolewski kosztowal setki L; maly 10-50 L = 2400-12000 d) |
| 35-37 | PrizeModest 3000 / PrizeWorthy 8000 / PrizePrincely 15000 | | sakiewki nagrod | OK (12-60 L - zgodne z nagrodami XIV w.) |
| 49 | CancelledFeeRefund | 0.5 | | WZOR |

**CrashScribe** - brak ustawien zlota (tylko `Mends.cs:4624` wyzej). **ForgeView** - UI, brak zlota.

---

## 3. Mechanizmy przeskalowania i ryzyka

### 3.1 Narzedzia, ktore mamy

| Mechanizm | Jak | Plusy | Minusy / pulapki |
|---|---|---|---|
| **Nadpisanie `ItemObject.Value` w locie** | `AccessTools.PropertySetter(typeof(ItemObject),"Value")` (wzor: `Armoury/src/MaterialLaw.cs:52-71`) albo `<Value>k__BackingField` (ROT `ROTRBMCompatibility.cs:155`); raz po zaladowaniu przedmiotow, z zapamietaniem oryginalu (`MaterialLaw._orig`) | dziala na kazdym zapisie (Value nie idzie do save), jedna zmiana trafia WSZEDZIE (handel, lupy, DTE, rekrutacja z kosztem sprzetu, nagrody turniejowe, naprawy, okup od sprzetu) | musi przejsc PRZED wszystkim, co cache'uje ceny (nasze `ArmsPricing._cost`, `MarketGlut`, BK `BKCaravansBehavior._priceDictionary`, BEE profile); kolejnosc wzgledem ROT (`ROTRBMCompatibility` ustawia luki lore w `OnSessionLaunched`) i BKROT (`/100` przy `InitializeTradeGood`); kowalstwo: XP z `Value` (MaterialLaw ma juz `RefineXpPostfix`, trzeba tak samo dla XP z wykucia - `WeaponXpFromValueCapped`) |
| **Postfiks na `ItemValueModel.CalculateValue`** | Harmony na `TaleWorlds.Core.DefaultItemValueModel.CalculateValue` (np. zamiast `2.75^Tierf` krzywa historyczna) | jedna regula dla ~2800 przedmiotow liczonych wzorem; nowe przedmioty modow tez | nie obejmuje przedmiotow z `value` w XML (luki, kusze, konie ROT, towary) - te i tak trzeba przejechac recznie; `CalculateValue` wolany przy wczytaniu XML (przed naszym `OnSubModuleLoad`? - trzeba sprawdzic, czy Harmony zdazy; bezpieczniej: przeliczyc po zaladowaniu i ustawic setterem) |
| **XSLT na XML** (`<xsl:template match="Item[@id='...']/@value">`) | jak ROT `items.xslt`, `bandits.xslt` | trwale, widoczne w XML, zero kodu | tylko dla przedmiotow z atrybutem `value` (26 towarow, ~40 lukow/kusz, konie ROT); kolejnosc modulow (XSLT naszego modulu musi byc po ROT/RBM); nie dziala na towary z kodu (`DefaultItems`, `BKItems`) |
| **Postfiks na modelach gry** (`ExplainedNumber`/`int` z wynikiem w zlocie) | `AddFactor(k - 1)` albo `__result = (int)(__result * k)` na: `IssueBase.RewardGold`, `TournamentGame` nagrody, `DefaultClanFinanceModel`, `DefaultRansomValueCalculationModel`, `DefaultBribeCalculationModel`, `DefaultWorkshopModel`, `DefaultSettlementEconomyModel.GetTownGoldChange` itd. | jedna latka na model, mozna wlaczac MCM; latki na KAZDA implementacje (BK/BEE podmieniaja modele - wzor `SupplyDemand.cs:385` szuka wszystkich podklas) | uwaga na zagniezdzenie (BEE/BK wolaja model bazowy -> licznik glebokosci jak `MaterialLaw.ProdPrefix`); stale w zachowaniach (behaviors) nie przechodza przez model -> transpiler (wzor `KingdomTreasury.cs:20-35` podmienia `ldc.i4`) |
| **Transpiler stalych** | `KingdomTreasury.Transpiler` (`ldc.i4 1000/100000/...` -> 0) | celnie w jednej metodzie | kruche miedzy wersjami gry; tylko tam, gdzie stala jest w IL |
| **Ustawienia MCM** | nasze `Settings.cs` + `tools/gen_mcm.py`; MCM BK/BEE/DTE/SoW | bez kodu | tylko to, co mod wystawia; nasze ustawienia trzeba przeliczyc recznie (czesc 2.4) |
| **Jednorazowy przelicznik zlota w zapisie** | przy pierwszym wczytaniu z nowa skala: `Hero.Gold`, `Clan` (przez glowe), `Town.Gold`, `Village.Gold`, `Kingdom.KingdomBudgetWallet`, warsztaty `Workshop.Capital`, karawany, BK estates, nasz `IronBank._capital` i dlugi, RC dlugi - razy k; znacznik w SaveDefiner | stare zapisy przechodza | Jeff i tak zaczyna nowa gre (AUDYT-CEN: "nowa gra, wszystko do przerobienia") - **mozna pominac**, jesli skalujemy tylko na nowa kampanie |

### 3.2 Najwazniejsze ryzyko: rynek liczy popyt w ZLOCIE

`tw/.../DefaultTradeItemPriceFactorModel.cs:176`:
`factor = (demand / (0.1 x supply + 0.04 x inStoreValue + 2)) ^ (0.6, zwierzeta 0.3)`, przyciete 0.1-10 (`:15,:17,:179`).
`supply`, `demand`, `inStoreValue` w `Town.MarketData` sa w **zlocie** (wartosc towaru), a popyt miasta to
`category.BaseDemand x prosperity + LuxuryDemand x max(0, prosperity - 3000)` (`DefaultSettlementEconomyModel.cs:61-72`,
`BaseDemand` z `core2/DefaultItemCategories.cs:349-413`). Jesli podzielimy `Value` kategorii przez k, a popytu nie,
to podaz w zlocie spadnie k razy -> wspolczynnik ceny skoczy ok. k^0.6 (przy k = 20: x6, uderzy w sufit 10) -
**ceny przeskalowanych rzeczy nie spadna tak, jak chcemy**. Kazde skalowanie `Value` kategorii wymaga takiego
samego skalowania `BaseDemand`/`LuxuryDemand` tej kategorii (pola `ItemCategory`, prywatne settery - ta sama
technika co `Value`) albo postfiksu na `GetDailyDemandForCategory`. To samo w BK (popyt populacji BK, `BKItemCategories`)
i BEE (`BEE_SettlementEconomyModel`, profile konsumpcji klas w `BetterEconomy/ModuleData/*.xml`).
Nasze `SupplyDemand` liczy podaz/popyt w SZTUKACH (nie w zlocie) - odporne.

Drugie: **zloto miast i wiosek**. Cel zlota miasta `10000 + 12 x prosperity` (`DefaultSettlementEconomyModel.cs:77`,
powtorzone w naszym `PopulationLaw.cs:168`) - miasto, ktore ma 40 000-70 000, kupi od gracza 20x wiecej zbroi po
przeskalowaniu. Jesli skalujemy tylko sprzet, zloto miast zostaje (jest w skali zoldu/dochodow); kupcy beda mieli
"za duzo" pieniedzy wzgledem sprzetu - to raczej dobrze (dzis po bitwie miasto nie ma czym zaplacic).

### 3.3 Ryzyka - lista kontrolna

1. **Progi AI na absolutnych kwotach** (czesc 2.5): decyzje "stac mnie / nie stac mnie" (rekrutacja, kupno koni,
   karawany, warsztaty, wydatki na bunty, ucieczka z armii, ofiary BK). Jesli skalujemy tylko ceny przedmiotow,
   progi zoldu/dochodow zostaja OK; jesli ruszamy zloto rodow albo nagrody - progi trzeba przeliczyc razem.
2. **Okupy i ceny jencow** liczone od poziomu/tieru postaci, nie od `Value` - nie zmienia sie same (RC `LordPrice` juz historyczne).
3. **Rekrutacja**: `GetTroopRecruitmentCost(..., withoutItemCost)` - koszt werbunku moze zawierac cene konia/sprzetu
   (`DefaultPartyTroopUpgradeModel.cs:76-81` liczy awans z roznicy kosztow rekrutacji BEZ sprzetu) - sprawdzic BK/BEE, czy doliczaja `Value`.
4. **DTE**: doplata za bron z taboru po pelnym `Value` (AUDYT-CEN) - po zmianie `Value` zmniejszy sie sama (dobrze),
   ale jej istnienie to i tak drukarka pieniedzy.
5. **Turnieje**: nagroda wybierana z przedzialu wartosci przedmiotu (czesc 2.2) - po zmianie `Value` turniej
   zacznie dawac sprzet wyzszego tieru (przedzial w zlocie stoi) - trzeba przeskalowac przedzial razem.
6. **Kowalstwo**: XP z ceny wyrobu i przetopu (`tw/TaleWorlds.CampaignSystem.GameComponents/DefaultSmithingModel.cs:122` przetop 0.3 x Value, `:127` wytop 0.02 x Value, `:132` wolne kucie 0.02 x Value, `:137` zamowienie 0.1 x Value);
   przy `/20` XP spadnie 20x - trzeba liczyc od ceny SPRZED (jak `MaterialLaw.RefineXpPostfix`). Zamowienia kowala
   (vanilla `CraftingCampaignBehavior` + nasze `Orders`) placa od `Value`.
7. **Handel gracza**: perki Trade (`Steward`/`Trade`) daja XP od zysku w zlocie (`DefaultTradeXpModel`?) - przy
   tanszym sprzecie XP handlu z lupow spadnie (to raczej zamierzone).
8. **UI**: ceny 0-1 (towary BK po `/100`) - `Value = 0` daje dzielenie przez 0 w niektorych modelach (np. `ArmsPricing`
   pilnuje `it.Value <= 0`, BEE/BK nie zawsze). Przy skalowaniu w dol zaokraglac do min. 1.
9. **Zapis**: `ItemObject.Value` nie jest zapisywany; zapisane jest zloto bohaterow/miast/warsztatow/karawan,
   `ItemRoster` (ilosci, nie ceny), `MarketData` (podaz/popyt w zlocie - przeliczy sie samo w kilka dni, wspolczynnik 0.15/dzien).
   Nowa kampania - brak ryzyka; stary zapis - przez kilka dni ceny "plywaja".
10. **MCM BK `LootScale` 0.5, `BaseWage` 1.25** i inne mnozniki BK/BEE - nie dublowac skalowania (czesc 2.3).
11. **Kolejnosc nadpisan**: BKROT `/100` (przy inicjalizacji BK), ROT luki lore (sesja), nasze MaterialLaw (sesja) -
    nowe prawo cen musi isc PO nich wszystkich i miec jedna tabele docelowa (inaczej dwa mody nadpisuja sie nawzajem).
12. **Unikaty**: `Recipes.cs:130` i `LegendaryValueFloor 25000`, `LegendaryLootValueFloor 100000`,
    `ArmsPricing.IsUnique` - progi "legendy" w zlocie; po przeskalowaniu legend (albo zostawieniu ich drogich) progi
    trzeba ustawic na nowo, inaczej zwykly sprzet albo wszystkie legendy zmienia klase.
