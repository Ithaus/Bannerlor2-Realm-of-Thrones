# Audyt: koszt wojny, praca gracza, lupy, naprawy, amunicja, wozy (2026-10-04)

Tylko odczyt - nic nie zmienione. Klucz: 1 denar ~ 1 pens (docs/AUDYT-CEN.md). Rok gry = 364 dni
(CHANGELOG wpis 11; CLAUDE.md mowi 168 - nieaktualne). Liczby historyczne to SZACUNKI z literatury
(Prestwich, Ayton, Dyer), do sprawdzenia, nie cytaty.

UWAGA o kompletnosci: pytania 2 (praca gracza) i 6 (wozy, pojemnosc) oraz szczegoly DTE w 3 i 5
byly pierwotnie oznaczone [NIESPRAWDZONE]. UZUPELNIONE tego samego dnia - sekcje 7-10 na koncu
(udzwig i predkosc, praca gracza, DTE i amunicja, jedzenie) oraz "Propozycje (uzupelnienie)".
Dawne znaczniki zamienione na [SPRAWDZONE -> sekcja X]. Sciezki `tw/`, `sb/`, `sbv/`, `bk/`, `dte/`, `rot/`,
`rbl/`, `be/`, `nv/`, `sow/` = dekompilacje w scratchpadzie (vanilla TaleWorlds.CampaignSystem, SandBox,
SandBox.View, BannerKings, DTE, ROT, RealisticBannerlord, BetterEconomy, NavalDLC, Spoils of War).

Zrodla: `Documents/.../CrashScribe/economy-2026-10-04_09-02-34.csv` (nowa gra, 315 rodow, dni 108836-108840),
`economy-2026-10-04_05-47-36.csv` (stary save, Autumn 1091, dojrzala gra), `Modules/Armoury/Armoury-*.log`,
dekompilacje w scratchpad (tw, bk, dip).

---

## 1. Dochody a koszt wojska

### 1.1 Liczby z gry (nowa gra, 5 dni)

| Kategoria | Zloto rodziny (mediana) | Dochod model/dzien | Zold/dzien | Wojsko | Faktyczna zmiana/dzien |
|---|---|---|---|---|---|
| Krol (29) | 370 000 -> 313 663 | 9 070 -> 5 156 | 0 -> 1 032 | 0 -> 265 | -385 (dzien 5) |
| Rod krolestwa (273) | 210 000 -> 194 233 | 4 221 -> 1 370 | 0 -> 334 | 0 -> 75 | -218 |
| Rod mniejszy (12) | 270 000 -> 262 124 | 320 | 418 | 163 | -1 755 |

Sumy wszystkich rodow (ten sam CSV): zloto 62.7 mln -> 57.8 mln w 4 dni (**-1.2 mln/dzien**); zold
wszystkich 73 tys. -> 184 tys./dzien; dochod modelu 1.83 mln -> 0.75 mln/dzien (glownie "Village Demesnes"
1.44 mln -> 0.25 mln - spadek do wyjasnienia, prawdopodobnie zgromadzony podatek wsi na starcie BK).

Zakupy sprzetu AI (Armoury-2026-10-04_09-01-00.log, linie "ZakupyAI: dzien"):
1.28 / 2.10 / 1.70 / 1.38 / 0.99 mln zl dziennie (915-1586 szt., ~1 300 zl/szt.).
**Sprzet kosztuje AI 7-12x wiecej niz zold** i wiecej niz caly dochod rodow (7.45 mln vs 5.86 mln w 5 dni).
Przyczyna: AiGear.cs:135 - budzet = 25% zlota ponad rezerwe 2 000 na kazda wizyte (Settings.cs:368-371),
raz dziennie na partie; ceny broni/zbroi 10-200x za wysokie (AUDYT-CEN.md).

Przyklady: Stannis 661 ludzi, 60 szt. za 75 484 (zloto 353 987 -> 278 503); Karstark 59 ludzi, 11 szt.
za 43 654 (jedna kolczuga 29 839 = 10 lat zoldu zolnierza t3).

### 1.2 Czy zlota jest za duzo / czy skarbiec rosnie bez konca

Stary save (dojrzala gra, economy-2026-10-04_05-47-36.csv):
- mediana krola 171 tys., rodu 57 tys. (p10 6.9 tys., p90 540 tys.) - typowy rod NIE puchnie;
- ale ogony: Joffrey 43.4 mln + skarbiec krolestwa 130 mln; najemnicy bez lenn Wild Hares 36.8 mln,
  Bright Banners 27.2 mln, Moon Brothers 7.4 mln, Brotherhood w/o Banners 3.7 mln - przy dochodzie
  modelu 0.8-6 tys./dzien. To potwierdza hipoteze z AUDYT-CEN: doplata DTE `MoveRosterToArmory` (pelna Value
  za lupy) i sprzedaz lupow drukowaly pieniadze. Od 04.10 `MoveRosterToArmory` jest wylaczone (AiGear.cs:84-89),
  efekt w nowej grze jeszcze nie do zmierzenia (5 dni).
- skarbce krolestw razem 245 mln.

Kurki "z niczego" w skarbcu krolestwa (vanilla, nie nasze):
- start: kazde krolestwo 2 000 000 (tw ClanVariablesCampaignBehavior.cs:199);
- krol dostaje do skarbca +1 000/dzien, gdy < 2 mln, oraz losowo 100-400 tys. gdy < 1 mln (tamze :417-423)
  - z niczego;
- podatek od bogactwa rodu > 100 tys.: vanilla 1% (DefaultClanFinanceModel.cs:174-182), BK zmienia na 0.1%
  (bk EconomyPatches.cs:213-217) - skarbiec rosnie;
- wydatek ze skarbca: zapomoga 500-2 000/dzien (x2 przy skarbcu > 1 mln, x2 dla krola) dla rodow z < 30 000
  zlota (DefaultClanFinanceModel.cs:155-158, 517-527); poza tym prawie nic (BK dyplomacja, Diplomacy
  GiveGoldToKingdom). **Skarbiec jest siatka asekuracyjna bez dna: zaden lord nie zbankrutuje, a
  pieniadz w skarbcu lezy martwy.**

### 1.3 Jak dlugo lord utrzyma wojsko

Z CSV (dzien 5): rod krolestwa 75 ludzi, zold 334/dzien, zloto 194 tys. -> **~580 dni samego zoldu bez
zadnego dochodu**; krol 265 ludzi, 1 032/dzien, 313 tys. + 2 mln skarbca -> ~300 dni z wlasnej kiesy,
~2 000 dni ze skarbcem. Jedzenie: ziarno 10 zl/10 kg (items-dump.csv), 300 ludzi zjada rzedu 10 jednostek
dziennie = ~100 zl/dzien [SPRAWDZONE -> sekcja 10: 300 ludzi zjada 300/20 x 0.6 = 9 jednostek/dzien
= ~90-110 zl/dzien] - pomijalne.
Najemnicy x1.5 zoldu (tw DefaultPartyWageModel.cs:19).
**Wniosek: zold i jedzenie nie ograniczaja wojny. Ogranicza ja wylacznie AiGear (zakupy sprzetu).**

### 1.4 Historia (szacunki)

| Rzecz | Wartosc | W denarach (1 = 1 d) |
|---|---|---|
| Zwyczajny dochod korony angielskiej ok. 1300-1340 | ~30 000 L/rok (z clami i subsydiami w latach wojny 60-100 tys. L) | 7.2-24 mln/rok |
| Armia 10 000 (1 000 zbrojnych x12 d, 3 000 lucznikow konnych x6 d, 6 000 pieszych x2-3 d) | ~45 000 d/dzien ~ 190 L/dzien ~ 68 000 L/rok | 45 tys./dzien |
| Kampania Crecy-Calais 1346-47 | rzedu 130-150 tys. L (oblezenie Calais ~11 mies.) | ~35 mln |
| Edward III 1339-41 | bankructwo korony, dlugi u Bardi/Peruzzi | - |
| Baron 200-500 L/rok | utrzyma caly rok ~10-25 zbrojnych po 12 d | 50-120 tys./rok |
| Sluzba lenna | 40 dni na wlasny koszt, potem zold krola | - |

Gra: krol ma dochod ~5-9 tys./dzien = 1.9-3.3 mln/rok -> moze trzymac caly rok ~1 000-1 800 ludzi
(przy sredniej ~5 zl na glowe) z dochodu. Historycznie korona z dochodu zwyczajnego trzymala armie
10 000 ludzi ~4-5 miesiecy, potem subsydia i pozyczki. **Skala dochodu krolow jest realna; za duze sa
zapasy gotowki (2 mln skarbca + 300 tys. na reku = ~10 000 L gotowki na start, kazdy krol) i brak
kosztu wojny poza sprzetem.** Historycznie wojna = dlug (Edward III, Bank Zelazny pasuje), w grze
IronBank: "dluznikow 0, kapital 5 mln" przez 5 dni (log 09:03-09:04) - nikt nie potrzebuje pozyczki.

---

## 2. Praca dla gracza [SPRAWDZONE -> sekcja 8]

Pewne z kodu/logu:
- Okupy: RealisticCaptivity `LordPrice` lord 8 000, glowa rodu 25 000, krol 100 000 + tier i lenna
  (CHANGELOG 2026-10-04 "OKUP"); dziura B4 w AUDYT-DZIURY (posrednik w karczmie placi z niczego).
- Kryjowki: 150 + 120 zl za bande i 3 przedmioty z niczego (AUDYT-DZIURY C6).
- Lupy: dzialka gracza 30% z magazynu DTE (z druzyna) albo 100% (sam) - BattlefieldLaw.cs:428-442.
- Kucie: BK CRAFT plyta t6 ~2.7 tys. surowca vs ~23 tys. rynek (AUDYT-DZIURY B3) - najwiekszy zarobek.
- Rada BK: "Councillor role" w CSV do 6 697 zl/dzien sumarycznie u wszystkich rodow (pojedyncze 500-570/dzien
  w starym save, np. Lannister, bar Emmon).

[SPRAWDZONE -> sekcja 8]: wzory nagrod questow vanilla, arena, turnieje, zaciag ROT, karawany,
warsztaty, rada BK, sprzedaz jencow - liczby i pliki w sekcji 8.

Proporcje (ocena): dzien pracy zolnierza t1 = 2 zl, rzemieslnika historycznie 3-4 d, mistrza 4-6 d.
Wszystko, co daje gracz-wojownik (okup 8 000 = 4 000 dni zoldu t1; jeden helm t2 7 700), jest o rzedy
wielkosci ponad dniowke, bo ceny uzbrojenia sa 10-200x zawyzone. Najpierw ceny (AUDYT-CEN pkt 1),
dopiero potem strojenie nagrod - inaczej strojenie bedzie do powtorki.

---

## 3. Lupy po bitwie

### 3.1 Jak to dziala u nas (log + kod)

- Bitwa osobista: lup = realny sprzet zabitych z magazynu DTE; dzialka gracza 30% (z druzyna) lub 100%
  (sam) - BattlefieldLaw.cs:381-426, 428-442. Log: "BattlefieldLaw: 249 szt. (30%) z magazynu wojska czeka
  na ekran lupow" (Armoury-2026-10-01_12-41-54.log:1958).
- Czesc trafiona smiertelnym ciosem (DTE "rozbita") wraca jako wrak, tylko jesli zabil czlowiek gracza,
  limit worka 400 (BattlefieldLaw.cs:561-571).
- Wraki <= 3% wartosci niszczone (Settings.cs:197 `LootMinConditionPercent`, BattlefieldLaw.cs:336-348, 668).
- Lupy z ekranu dostaja stan bojowy (Settings.cs:185-193: srednio 45% +/- 25%).
- Przegrana/ucieczka = nic z pola (ArmouryBehavior.cs:1250-1260).
- Bitwa automatyczna: Spoils of War, bez mnoznikow tieru; BK `LootScale` 0.5 (AUDYT-CEN).
- AI: DTE `DistributeLootRandomly` miedzy magazyny zwyciezcow; lup kopiuje zbrojownie przegranych
  bez czyszczenia (AUDYT-DZIURY C5).

Ile przepada (log):

| Log | Do ekranu (30%) | Wrakow zostalo na polu | Pozycji na ekranie |
|---|---|---|---|
| 2026-09-20_06-53:1571-1577 | 77 | 41 | 18 |
| 2026-09-20_09-03:1451-1458 | 100 | 74 | 39 |
| 2026-10-02_14-55:811-820 | 363 | 208 | 550 |
| 2026-10-01_12-41:1958-1967 | 249 | 400 (sufit) | 925 |

Wrakow jest 50-75% liczby sztuk z dzialki - czyli zniszczenia sa duze. Dokladny procent DTE (trafiona
sztuka + "czasem druga") [SPRAWDZONE -> sekcja 9.1; UWAGA: wraki w ogole nie trafiaja na ekran -
kazdy przypadek w logach to "N wrakow ponizej progu zniszczenia", patrz 9.2].

### 3.2 Historia (szacunki)

- Pole bitwy obdzierano do naga (zwyciezcy, potem ludzie z taboru i okoliczni chlopi).
- Kontrakty angielskie XIV-XV w.: "trzecie" - zolnierz oddaje 1/3 kapitanowi, kapitan 1/3 swoich krolowi.
  Nasze 30% dla gracza-kapitana z lupu druzyny jest bliskie (realnie kapitan mial 1/3 + 1/9 od ludzi).
- Zbroja zabitego zwykle naprawialna: kolczuge laczono ogniwami, plyte klepano; zniszczone byly glownie
  przebite helmy, rozciete przeszywanice, polamane drzewca. Szacunek: 20-40% sztuk z trupa do wyrzucenia,
  reszta do naprawy za 5-20% wartosci. **U nas 50-75% wrakow + prog 3% - za ostro dla zbroi metalowej,
  w sam raz dla drzewc, strzal i tekstyliow.**
- Najwiekszy lup to jency (okup), konie i tabor przeciwnika, nie zbroje szeregowcow.

---

## 4. Naprawy i zuzycie

### 4.1 Stan

- Zuzycie wojska gracza: po kazdej WYGRANEJ bitwie 12% sztuk w uzyciu schodzi o stopien
  (Settings.cs:103-104, ArmouryBehavior.cs:1296, 1352-1415). Tylko gracz (ArmouryBehavior.cs:1241
  `IsPlayerMapEvent`). **Zbrojownie AI nie zuzywaja sie wcale** - poza zniszczeniem DTE.
- Zuzycie bohatera: celowane per trafienie (WearDamageFactor 0.15, pociski 10%, bron 0.6/cios,
  tarcza 0.3) - Settings.cs:110-116; stan obniza statystyki (90% kary przy 1%, wykladnik 1.15 - :107-109).
- Samonaprawa wojska: codziennie w MIESCIE 10% zuzytych sztuk (min. 3) wraca do 100% ZA DARMO
  - TroopSelfMend.cs:51-61, Settings.cs:158-159. Napis "z wlasnego zoldu", ale zadne zloto nie plynie
  (AUDYT-DZIURY C8). Log: 102, 92, 83, 74 ... szt. dzien po dniu w Riverrun (Armoury-2026-10-01).
- Kowal: sprzet bohatera 25% wartosci x uszkodzenie (SmithMenu.cs:397, 410, RepairCostFactor 0.5 / 2);
  polki wojska: wrak 10% wartosci, rabat hurtowy do 30% (SmithMenu.cs:919-940, Settings.cs:523-527);
  samemu: material do 20% receptury (Settings.cs:535). Zloto kowala idzie w nicosc (AUDYT-DZIURY C2).

### 4.2 Ocena

- Ceny napraw liczone od zawyzonej `Value`: kolczuga 29 839 w stanie 50% = ~1 500 u kowala za polki,
  ~3 700 za sprzet bohatera = 250-600 dni zoldu t3. Historycznie: platnerz 4-6 d/dzien, naprawa kolczugi
  kilka-kilkanascie pensow, czyszczenie (scouring) w beczce z piaskiem - grosze.
- Brak zuzycia AI = AI ma zawsze swiezy sprzet, gracz nie.
- Darmowa samonaprawa w miescie = brak kosztu dla gracza; rozwiazanie dziala, ale bez ekonomii.

### 4.3 Model rozsadniejszy

1. Koszt naprawy = robocizna + material, nie procent `Value`: godziny platnerza x stawka (np. 5 zl/dzien
   pomocnik, 10 zl mistrz) + sztabki/skora/len wedle udzialu uszkodzenia w recepturze (juz mamy
   `SelfMendParts`, SmithMenu.cs:539-557). Uniezaleznia naprawe od zawyzonych cen.
2. TroopSelfMend placi naprawde: z kiesy gracza (albo potracone z zoldu = mniej morale), zloto do miasta.
3. Platnerze w obozie (specjalizacja jak lekarz): kompan/zolnierz z Smithing naprawia X sztuk dziennie
   poza miastem, zuzywa material z taboru.
4. AI: po bitwie te same 12% sztuk stopien w dol; w miescie AI placi miastu za naprawe z tego samego
   wzoru (kupuje wtedy mniej nowego sprzetu - realny popyt na prace kowali).

---

## 5. Amunicja i logistyka

- Vanilla: amunicja odnawia sie co bitwe, kampania jej nie zuzywa (pewne dla vanilla). DTE [SPRAWDZONE ->
  sekcja 9.3]: kolczan wystrzelany do zera przepadal, ale CrashScribe Mends.QuiversComeBack to wylacza -
  dzis strzaly i belty NIE zuzywaja sie wcale (gracz i AI), przepadaja tylko wyrzucone oszczepy/toporki.
- Pekanie kolczanow po bitwie (AmmoAttrition) usuniete na zadanie Jeffa 17.09 (ArmouryBehavior.cs:1243-1244):
  "amunicja ubywa tylko w polu".
- Kwatermistrz wydaje tylko nadwyzki ponad potrzeby (QuartermasterLaw.cs:14-22), liczy stosy per strzelec
  (CHANGELOG 2026-10-03). Log: "Arrows 285/294 - brak ilosciowy (polka 9923)" (Armoury-2026-10-04_03-29-05).
- AI: amunicja na liscie zakupow AiGear (AiGear.cs:100-107), wiec AI ja kupuje w miescie.
- Ceny: strzaly t1 55/stos, t4 606, t6 2 939-7 202 (items-dump.csv). Historycznie snop 24 strzal ~14-18 d,
  Korona kupowala centralnie setki tysiecy snopow (Privy Wardrobe w Tower). t6 to 400x historii.
- Jedzenie, udzwig, kara za stado i przeciazenie, MarchPace - [SPRAWDZONE -> sekcje 7 i 10].

---

## 6. Wozy [SPRAWDZONE -> sekcja 7 i "Propozycje (uzupelnienie)"]

Przeglad XML, prefabow i modeli zrobiony (sekcja 7): w ZADNYM module nie ma przedmiotu-wozu; sa siatki
wozow (takze w skali mapy: `mi_cart_a/b/b_full`). Kon juczny (sumpter) 99 zl w grze, historycznie 60-120 d.

Historia (szacunki): woz dwukolowy (carecta) 3-10 s (36-120 d) bez koni, wielki woz czterokolowy (wain,
plaustrum) 15-40 s; kon pociagowy (affer) 3-10 s; wol 10-13 s; wozak 2 d/dzien; wynajem wozu z 3-4 konmi
i wozakiem 12-24 d/dzien (kampanie Edwarda I). Udzwig: kon juczny ~100-150 kg, woz dwukolowy 2-3 konie
~500-700 kg, wain 4-8 zwierzat ~1-1.5 t. Tempo: woly 15-20 km/dzien, konie z wozem 25-30 km po drodze.
W grze przy 1 zl = 1 d: woz 50-150 zl, wain 200-500 zl, kon pociagowy 60-120 zl - tanie wzgledem broni,
drogie wzgledem zoldu (miesiac pracy wozaka).

Co by trzeba: przedmiot "woz" (Goods/Animal z kategoria wlasna) dodajacy pojemnosc tylko z przypisanym
koniem pociagowym; latka na model pojemnosci (+X kg za woz z koniem) i na model predkosci (kara jak za
stado, ale mniejsza na drogach, wieksza w lesie/gorach - TerrainEase.cs ma juz teren). Wizual na mapie -
osobna praca (siatki), najpierw sama mechanika.

---

## Propozycje (wplyw / ryzyko)

1. **Budzet AiGear od zoldu, nie od zlota** (wplyw b. duzy, ryzyko male): dziennie najwyzej N dni zoldu
   partii (np. 3) zamiast 25% zlota na wizyte (AiGear.cs:135). Dzis AI wydaje 7-12x zoldu na sprzet.
2. **Ceny uzbrojenia do skali zoldu** (AUDYT-CEN pkt 1; wplyw najwiekszy, ryzyko srednie - cofniete raz,
   CHANGELOG 4). Bez tego naprawy, lupy, nagrody i okupy sa nie do zestrojenia.
3. **Skarbiec krolestwa bez kurkow z niczego** (wplyw duzy, ryzyko male): zero +1 000/dzien i losowych
   100-400 tys. (ClanVariablesCampaignBehavior.cs:417-423); zapomoga ze skarbca tylko na zold w czasie wojny.
4. **Zuzycie AI** (wplyw sredni, ryzyko srednie - wydajnosc): po bitwie AI ta sama regula 12% stopien w dol,
   naprawa platna w miescie.
5. **Naprawa = robocizna + material** zamiast % Value; TroopSelfMend placi miastu (C8, C2) (sredni, male).
6. **Lupy: mniej wrakow dla metalu** - prog zniszczenia osobno dla zbroi metalowej (np. 0) i drewna/tekstyliow
   (sredni, male).
7. ~~Dokonczyc audyt pytan 2, 5 i 6~~ - ZROBIONE, sekcje 7-10.
8. Wozy jako mechanika - projekt w "Propozycje (uzupelnienie)" ponizej.
9. Zmierzyc 30+ dni CSV nowej gry: spadek "Village Demesnes" 1.44 -> 0.25 mln/dzien i czy po wylaczeniu
   doplaty DTE najemnicy dalej puchna.

---

# UZUPELNIENIE (2026-10-04, druga tura - odczyt kodu i XML, nic nie zmieniane w grze)

## 7. Udzwig, zwierzeta juczne, predkosc, wozy w danych gry

### 7.1 Zwierzeta juczne w XML (wszystkie moduly)

`is_pack_animal="true"` maja tylko 3 pliki: `SandBoxCore/ModuleData/items/horses_and_others.xml`,
`RBM/ModuleData/RBMCombat_horses.xml` (nadpisuje te same id) i `BannerKings.Redux/ModuleData/items.xml` (wol).
ROT, NavalDLC, Spoils, DTE - zadnych wlasnych jucznych. Kategoria wszystkich koni jucznych: `sumpter_horse`;
wol BK: `Oxen`.

| id | nazwa | wierzchowy | cena w grze (items-dump.csv) | uwagi |
|---|---|---|---|---|
| sumpter_horse | Sumpter Horse | tak | 99 | RBM: weight 450, speed 34 |
| old_horse | Work Horse | tak | 99 | RBM speed 37 |
| saddle_horse | Saddle Horse | tak | 140 | TEZ juczny (is_pack_animal=true) - liczy sie jako 100 kg, nie 20 |
| mule / mule_unmountable | Mule | tak / nie | 99 | |
| pack_camel(_unmountable) | Pack Camel | tak / nie | 99 | |
| ox (BK) | Ox | nie (brak is_mountable) | 300 (XML) | juczny, NIE bydlo rzezne |

Klasyfikacja (dekompilacja `HorseComponent` z TaleWorlds.Core.dll, scratchpad `cap/HorseComponent.cs:47-67,154-155`):
`IsMount = rideable && !pack`, `IsLiveStock = !rideable && !pack`, `IsPackAnimal` = flaga XML.
`ItemRoster.OnRosterUpdated` (tw ItemRoster.cs:439-452) liczy jucznego i luzaka TYLKO bez modyfikatora -
kon "kulawy"/"stary" z lupu Spoils nie daje ani udzwigu, ani nie wchodzi do stada (a pelny przelicznik
:566-576 modyfikatory ignoruje - niespojnosc vanilli, drobna).

Waga zwierzat: `GetItemEffectiveWeight` zwraca 0 dla kazdego przedmiotu z HorseComponent
(tw DefaultInventoryCapacityModel.cs:39-48) - waga 150-520 kg z XML nic nie znaczy.

### 7.2 Pojemnosc (udzwig) - wzor i liczby

tw DefaultInventoryCapacityModel.cs:50-99 (stale :12-18):
- baza 10 kg;
- **zdrowy czlowiek: 20 kg** (TroopsFactor 2 x srednia waga 10); ranni nic; perk ForcedLabor dolicza jencow;
- **luzak (kon wierzchowy w taborze, bez modyfikatora): 20 kg**;
- **zwierze juczne: 100 kg** (10 x 10); perki BeastWhisperer, DeeperSacks, ArenicosMules jako procent;
  BK perk CaravaneerStrider +20 kg/jucznego (bk VanillaModelTweakPatches.cs:625-641);
- konie pod jezdzcami (sprzet oddzialu) NIE daja nic - nie sa w taborze;
- na morzu: tylko ludzie + ladownosc statkow (nv NavalDLCInventoryCapacityModel.cs:37-53).
- Zaden inny mod nie rusza pojemnosci (skan DLL w Modules: tylko BK i NavalDLC).

Przyklad: 100 ludzi bez zwierzat = 2 010 kg; +25 mulow (tyle, ile MarchPace puszcza "za darmo",
Settings.cs:330 `MarchPackAllowance 0.25`) = 4 510 kg. 300 ludzi + 75 mulow = 13 510 kg.

**Wazne: zbrojownia DTE nic nie wazy.** Magazyn sprzetu wojska (`ArmyArmory.Armory`, dte ArmyArmory.cs:24)
to osobny statyczny ItemRoster; `CalculateTotalWeightCarried` sumuje tylko `mobileParty.ItemRoster`
(tw DefaultInventoryCapacityModel.cs:102-113). Setki zbroi, lukow i kolczanow zapasu jada bez wagi.

Historia (szacunki): zolnierz poza bronia niesie 15-25 kg (20 kg w grze - trafne); kon juczny 100-150 kg
(100 kg - trafne); kon w wozie ciagnie 250-350 kg (w grze brak wozow).

### 7.3 Predkosc: stado i przeciazenie

Lancuch modeli: ROTPartySpeedModel (rot ROT.Models/ROTPartySpeedModel.cs:21-43, tylko +20% dla partii,
do ktorej gracz jest zaciagniety) -> BEE_PartySpeedModel (tylko karawany) -> BKROTPartySpeedModel
(blogoslawienstwa bogow +15%) -> RealisticPartySpeedModel (pory roku i pogoda, rbl
RealisticPartySpeedModel.cs:58-101, podloga 0.2) -> NavalDLC -> vanilla. **Zaden mod nie zmienia wzoru
stada ani przeciazenia** - liczy vanilla:

tw DefaultPartySpeedCalculatingModel.cs:
- baza `4 x (200/(200+ludzie))^0.4` (:240-243): 100 ludzi 3.40, 300 ludzi 2.77; potem nasz WorldPace
  x0.5 (WorldPace.cs:54-56, Settings.cs:282);
- ladunek w granicach udzwigu: najwyzej -2% (:361-364);
- **przeciazenie: -0.4 x (nadwyzka / udzwig)** (:154-158, :245-252), bez sufitu poza perkami
  Energetic/Unburdened: 10% ponad = -4%, 50% = -20%, 100% = -40%, 200% = -80%;
- **stado**: stado = juczne + bydlo + luzaki ponad liczbe piechurow (:175, :230-238); kara
  `-0.3 x (stado - ludzie)/ludzie`, max -80% (:378-390). Stado do liczby ludzi jest DARMOWE; kazde
  zwierze ponad to -0.3/ludzie (100 ludzi: -0.3% za sztuke; 150 zwierzat = -15%, 300 = -60%);
  wiesniacy zwolnieni; perk Shepherd lagodzi;
- jazda +30% x udzial jazdy, piechota na luzakach +15% x udzial (:411-426);
- ranni -5% x udzial ponad 1/4 (:392-409).

Nasze nakladki (Armoury): MarchPace.cs:35-52, 75-125 - sufit kolumny w jednostkach mapy, x WorldPace 50%:
ktos idzie pieszo 4.0 -> 2.0; sama jazda z taborem (juczne+bydlo > 0.25/czlowieka) 4.2 -> 2.1; piechota
na luzakach 5.0 -> 2.5; czysta jazda 6.5 -> 3.25 (Settings.cs:326-330). TerrainEase: las -0.10 i noc
-0.5 PLASKO zamiast procentow vanilli (Settings.cs:284, 289). W praktyce kara stada vanilli prawie nigdy
nie dziala (armie maja mniej zwierzat niz ludzi), a kara taboru zyje tylko w MarchPace i tylko dla jazdy.
AIInfluence.dll tez odwoluje sie do PartySpeedModel - nie dekompilowane [NIESPRAWDZONE, raczej poboczne].

### 7.4 Wozy - czy cos istnieje

- **Przedmiot "woz"**: brak w KAZDYM module (grep `cart|wagon|wain|carriage` po ModuleData poza Languages:
  tylko teksty w Native module_strings.xml i SandBox wanderer_strings.xml). CrashScribe items-dump.csv:
  0 trafien.
- **Siatki 3D (sceny)**: Native/SandBox maja `bd_cart_a/b/c`, `bd_hay_cart_a/b`, `bd_cartbroken_a-d`,
  `bd_cart_wheel_a/b`, `cart_village*` (meta-mesh `bd_cart_a` uzyty 23x w prefabach).
- **Siatki w skali mapy**: `Native/Prefabs/map_icon_parts.xml:1247-1260` - prefaby `map_icons_props_cart_a`,
  `map_icons_props_cart_b`, `map_icons_props_cart_b_full` (meta-mesh `mi_cart_a`, `mi_cart_b`,
  `mi_cart_b_full`) - rekwizyty mapy (ikony produkcji), statyczne, bez koni.
- ROT-Map: `cart_village_animated_a` (ROT-Map/Prefabs/Mystaf_Outside.xml:19115) - animowana krowa przy wozie,
  dekoracja mapy; `horse_wagon` (ROT-Map/Prefabs/Others.xml:821) - kon + narzedzia tortur, rekwizyt sceny.
- **Wizual partii na mapie**: lider + JEDNO dodatkowe zwierze z uprzeza (sbv MobilePartyVisual.cs:1021-1029,
  1632-1644). Karawany dostaja `mule` + `mule_load_a/b/c` albo wielblada (tw CaravanPartyComponent.cs:265-292);
  partie lordow - nic (bazowe PartyComponent.cs:102-106 zwraca puste). Wozu w ikonie partii nie ma nigdzie.

## 8. Praca dla gracza - wyplaty

Skala odniesienia - zold dzienny zolnierza w tej grze: vanilla 1/2/3/5/8/12/17/23 (tier 0-7,
tw DefaultPartyWageModel.cs:23-41) x BK `BaseWage` 1.2546 z zapisanego MCM (BannerKings.json; bk
BKPartyWageModel.cs:85) = **t1 2, t2 3, t3 6, t4 10, t5 15, t6 21 zl/dzien**. Rok 364 dni: t1 = 730 zl/rok.

| Zrodlo | Wyplata | Plik | W dniach zoldu t1 / t6 |
|---|---|---|---|
| Zaciag ROT (gracz jako zolnierz) | 2 + 2 x poziom bohatera / dzien (poziom 20 = 42) | tw CharacterObject.cs:349-357; rot ROTClanFinanceModel.cs:45-59 | 21 / 2 dziennie |
| ^ blad ROT | ta sama stawka dodawana RAZ NA KAZDEGO bohatera rodu w partii gracza (petla :55-59) - 3 kompanow = x4 | j.w. | - |
| ^ Free Folk, Biali Wedrowcy | 0 | j.w. :47-54 | - |
| Arena (trening) | 0 / 5 / 10 / 25 / 60 zl wg liczby pokonanych (<3, <6, <10, <20, 20+), 250 za wszystkich 30 | sb ArenaMasterCampaignBehavior.cs:400-443 | 30 / 3 (60 zl) |
| Turniej vanilla | przedmiot o Value 1 600-5 000; zaklad max 150 na runde | tw FightTournamentGame.cs:343,359; sb TournamentBehavior.cs:33,71 | 800-2 500 / 76-238 |
| GrandTourney | lokalny (wojna) przedmiot do 2 000; sakiewki 3 000 / 8 000 / 15 000; organizator placi 2 000 + prosperity x0.5, dostaje 200/lorda | GrandTourney/src/Settings.cs:26,33-37,47 | do 7 500 / 714 |
| Questy vanilla (m = PlayerProgress 0.1-1, tw DefaultIssueModel.cs:23-26) | MerchantNeedsHelpWithOutlaws 400+1500m; CaravanAmbush 1000+3000m; ExtortionByDeserters 800+4200m; LandlordTrainingForRetainers 2000+4000m; Smugglers 750+3000m; NearbyBanditBase 3 000; CapturedByBountyHunters 3 000; LordWantsRivalCaptured 5 000; TheConquestOfSettlement 20 000; EscortMerchantCaravan min(8 000, (250+1000m) x k); LandLordNeedsManualLaborers 50/jenca; GangLeaderNeedsRecruits 2000+100/rekruta | tw TaleWorlds.CampaignSystem.Issues/*.cs (RewardGold), sb SandBox.Issues/*.cs | 3 000 zl = 1 500 / 143 |
| Karawana | koszt 15 000 / 22 500 (duza); dochod gracza w starym save 485/dzien | tw DefaultCaravanModel.cs:43-51; economy-...05-47-36.csv | 242 / 23 dziennie |
| Warsztat | koszt = EquipmentCost + 4 x prosperity + InitialCapital/5; dochod mediana 475/dzien (34 rody), gracz 371 | tw DefaultWorkshopModel.cs:60-63; CSV | 185-237 / 18-23 dziennie |
| Rada BK (Councillor role) | mediana 150-166/dzien, p90 209-512, max 1 000 | bk BKClanFinanceModel.cs:218-225; CSV oba save | 75-83 / 7-8 dziennie |
| Kontrakt najemny | 1 080/dzien (jedyny przypadek w CSV) | CSV | 540 / 51 dziennie |
| Sprzedaz jenca (zwykly) | 25% kosztu rekrutacji: koszt 10/20/50/100/200/400/600/1000 wg poziomu (+150 kon) -> looter 2, t3 ~12-15 (BK podloga 10 x zold) | tw DefaultRansomValueCalculationModel.cs:9-30; tw DefaultPartyWageModel.cs:215-231; bk BKPartyWageModel.cs:383 | 1-8 / 0.1-0.7 |
| Okup lorda (RealisticCaptivity) | 8 000 / 25 000 / 100 000 | sekcja 2 | 4 000 / 381 (lord) |
| Glowy bandytow | BRAK systemu nagrod w zainstalowanych modulach (grep "Bounty": tylko BK DefaultContractAspects) | - | - |
| Kucie BK CRAFT | plyta t6 ~23 tys. rynek przy ~2.7 tys. surowca | AUDYT-DZIURY B3 | 10 000 / 1 000 |

Mody "z praca" (BannerlordExpanded.SettlementInteractions, BasicOverhaul, Arena Overhaul, WealthyWorkshops,
MinimalWorkshopIncome...) - maja tylko stare pliki w `Configs/ModSettings/Global`, **nie sa w Modules** -
nie dzialaja. Zainstalowane dodatki z zarobkiem: GrandTourney, TournamentsXPanded (nie dekompilowany
[NIESPRAWDZONE]), RealisticCaptivity (okupy, praca jenca), ROT (zaciag, pojedynki z zakladem
rot ROTDuelsBehavior.cs:655-690).

Ocena: gracz-wojownik zarabia w skali RYCERZA/BARONA od pierwszego tygodnia. Najtansza droga (arena 60 zl za
godzine walki) = miesiac zoldu t1; pojedynczy quest 3 000 = 4 lata zoldu t1; karawana 485/dzien = dochod
barona (200-500 L/rok ~ 130-330 d/dzien, sekcja 1.4). Historycznie najemnik 2-6 d/dzien, rycerz 24 d/dzien.
Najblizej realiow jest zaciag ROT (rycerz-poziom 11 = 24 zl/dzien) - poza bledem mnozenia przez kompanow.

## 9. DTE: zniszczenie sprzetu i amunicja (dekompilacja)

Ustawienia gracza: obecny plik DTE to `Configs/ModSettings/Global/DynamicTroop/DynamicTroopSettings.json`
(dte ModSettings.cs:12,16 - Id "DynamicTroopSettings", folder "DynamicTroop"): **DropRate 1.0**,
ScrapCapPerCategory 600, Underequipped true. Plik `bannerlord.dynamictroop.json` (DropRate 0.5, 21.08)
to stara wersja - nieuzywany.

### 9.1 Bitwa osobista (dte DynamicTroopMissionLogic.cs)

Dla KAZDEGO zwyklego zolnierza (nie bohatera), ktory padl ZABITY albo NIEPRZYTOMNY (`agentState 3/4`, :241;
AgentState: Unconscious=3, Killed=4 - zweryfikowane w TaleWorlds.Core.dll), po obu stronach:
1. **czesc trafiona ciosem** - losowana sposrod noszonych zbroi pokrywajacych trafiona czesc ciala,
   z waga = wartosc pancerza tej czesci (:330, dte ArmorSelector.cs) - **zawsze niszczona**;
2. **dodatkowa czesc** (losowa inna zbroja, bez uprzezy konskiej) - tylko gdy ofiara jest po stronie
   PRZECIWNEJ graczowi; szansa wg tieru ofiary: t3 25%, t4 35%, t5 45%, t6+ 60%, t1-2 0% (:336-368);
3. reszta (bron, tarcze cale, pozostale zbroje, kon) idzie do `ItemsToRecover` wlasnej partii, a z
   szansa DropRate (u nas 1.0 = zawsze) TAKZE do lupu partii zabojcy (:376-394).
Rozliczenie (:703-733): zwyciezca odzyskuje swoje + bierze lup; przegrany - nic (ani swoich, ani lupu);
bitwa nierozstrzygnieta - kazdy odzyskuje swoje. Tarcza rozbita (HP 0) przepada (Global.cs:268).
Uwaga: **ranni (nieprzytomni) tez traca czesc trafiona** - takze ludzie gracza.
Skala: zolnierz nosi zwykle 3-5 zbroi + 2-4 bronie/tarcze/kolczany; strata 1 (swoi) albo 1-1.6 (wrog t3-t6)
sztuki = ok. 12-25% sztuk z kazdej ofiary.

Bitwa automatyczna z graczem (dte EveryoneCampaignBehavior.cs:571-725): dla zabitego/rannego przegranego
tylko "dodatkowa czesc" wg tieru (:676-701, te same 25/35/45/60%), reszta z szansa DropRate do losowo
wybranego zwyciezcy wg szans lupu (:712-724). Bitwy AI-AI: cala zbrojownia przegranych rozdana
zwyciezcom (`DistributeLootRandomly`, :561) - AUDYT-DZIURY C5.
Magazyn gracza: zlomowanie ponad 600 sztuk na kategorie (dte ArmyArmoryBehavior.cs:283).

### 9.2 Nasze "wraki" - martwa funkcja

BattlefieldLaw.cs:187-199 podpina sie pod `GetRandomArmorByBodyPart` i `OnAgentRemoved`, zapamietuje
czesc z pkt 1 (tylko pkt 1 - "dodatkowa czesc" z pkt 2 ginie bez sladu) i dla zabojstw ludzi gracza
wklada ja do worka (limit 400, :562-573). Przy oddaniu lupu `AppendWrecks` (:659-674) nadaje modyfikator
Spoils `rl_looted_heavy_max` (WreckModifier, :576-586) - a ten ma `price_factor="0.03"`
(Spoils of War/ModuleData/item_modifiers.xml:4-12). Prog zniszczenia `LootMinConditionPercent = 3`
(Settings.cs:197), warunek `<=` (:668-669) -> **kazdy wrak jest zawsze kasowany**. Logi: 8 na 8 przypadkow
"N wrakow ponizej progu zniszczenia - zostaly na polu" (41, 43, 74, 208, 400 x4) - ani jeden wrak nie
dotarl do gracza. Funkcja WreckSalvage (Settings.cs:196) nic nie robi.

### 9.3 Amunicja (strzaly, belty, oszczepy)

- Vanilla: agent dostaje pelny kolczan co bitwe, kampania nie odejmuje nic.
- DTE: przy pojawieniu sie zolnierza sprzet (z kolczanami) jest ZDEJMOWANY z magazynu - gracz
  ArmyArmory.cs:236-261 (`AddToCounts -1`), AI przez distributor (dte Patches/SpawnAgentPatch.cs:127-134);
  po bitwie wraca tylko to, co przeszlo filtr `Global.ProcessAgentEquipment` (Global.cs:257-273):
  kolczan/belty/rzucane wracaja tylko, jesli NIE sa puste (`IsAmmoAndEmpty`, :276). Kolczan z 1 strzala
  wraca pelny (magazyn nie pamieta liczby strzal).
- CrashScribe Mends.QuiversComeBack (CrashScribe/src/Mends.cs:2510-2517, 4259-4269) - aktywne w sesji
  09:00:57 (session log :162) - zmusza `IsAmmoAndEmpty` do false dla strzal i beltow. **Skutek: strzaly
  i belty nie zuzywaja sie WCALE - ani u gracza, ani u AI w bitwach z graczem.** Przepadaja tylko
  wyrzucone do zera oszczepy/toporki (galaz IsThrowing celowo nietknieta) i kolczany zabitych po
  przegranej. Komunikat Mends "jedyna trwala strata to AmmoAttrition" jest nieaktualny - AmmoAttrition
  usuniety 17.09 (ArmouryBehavior.cs:1243).
- Uzupelnianie: gracz - recznie, wkladajac do zbrojowni; QuartermasterLaw.cs:14-22 broni zejscia ponizej
  progow DTE (liczy kolczany per strzelec, :63-75, 350-360). AI - zakupy AiGear w miastach (AiGear.cs:100-107).
  DTE doklada "extra arrows" lucznikom z nadwyzki magazynu (dte PartyEquipmentDistributor.cs:1041-1069).
- AmmoTracer (log 03-04.10): wahania -96/-44/+44/+32 itp. - pobranie przy wejsciu w bitwe i zwrot po niej,
  plus ruchy gracza; trwalego ubytku nie widac.

## 10. Jedzenie

- Zuzycie vanilla: `(ludzie + jency/2) / 20` jednostek dziennie (tw DefaultMobilePartyFoodConsumptionModel.cs,
  `NumberOfMenOnMapToEatOneFood => 20`); BK: 20 + SlowerParties x 20, u Jeffa SlowerParties 0.0
  (BannerKings.json) -> 20. ROT/BEE/NavalDLC/RBL przekazuja dalej; RBL dodaje lato (premia) i zime/zamiec/upal
  (kara) - rbl RealisticFoodConsumptionModel.cs.
- **Nasze Rations -40% raz** (Rations.cs:51-71, licznik zagniezdzenia :47-49; log 09:01:12: zalatane 5 modeli):
  **0.03 jednostki na czlowieka dziennie**. Jednostka = 1 sztuka jedzenia = 10 kg (grain/fish/cheese/butter/
  beer: weight 10, items-dump.csv) -> **0.3 kg/czlowieka/dzien** (vanilla 0.5 kg).
- Konie: BK `CalculateAnimalFoodNeed` (kon 0.25, rumak 0.5, mul 0.15, krowa 0.175, swinia 0.1, owca 0.05
  jednostki/dzien) liczone TYLKO na pustyni i (polowa) w sniegu (bk BKPartyConsumptionModel.cs) - poza tym
  konie nie jedza.
- Furaz: vanilla brak (tylko perk Foragers w lesie/stepie). Nasze ScorchedEarth: armia >= 100 ludzi przy
  wrogiej wiosce (promien 3) zdejmuje paleniska i bierze `1 + ludzie/250` ziarna dziennie
  (ScorchedEarth.cs:76, Settings.cs:311-314) - dla 500 ludzi 3 z 15 zjadanych jednostek.
- W praktyce: 100 ludzi zjada 3 jednostki = 30 kg dziennie; sam ich udzwig (2 010 kg) miesci 200 jednostek
  = **~66 dni jedzenia bez jednego zwierzecia**. 300 ludzi: 9 jednostek/dzien, udzwig 6 010 kg = 66 dni.
- Historia (szacunki): racja 1.5-2 kg/dzien (chleb ~1 kg, piwo 3-4 l, mieso/ryba, groch) + obrok konia
  5-10 kg (wypas latem). 100 ludzi = 150-200 kg/dzien = 5-7x wiecej niz w grze; zolnierz niosl 3-5 dni
  zapasu, reszta jechala wozami albo z furazu. **W grze jedzenie jest 5-7x za lekkie, wiec udzwig nigdy nie
  ogranicza - dlatego dzis wozy nie mialyby po co istniec.**

---

## Propozycje (uzupelnienie) - ranking wplyw / ryzyko

1. **Wraki naprawde wracaja** (wplyw sredni, ryzyko b. male, jedna linia): wrak dostaje `rl_looted_heavy`
   (8%) zamiast `rl_looted_heavy_max` (3%) w BattlefieldLaw.WreckModifier, albo prog porownuje `<` zamiast `<=`.
   Dzis WreckSalvage jest martwe (9.2). Sprawdzic w logu: "BattlefieldLaw: N szt. ... (w tym M wrakow...)".
2. **Blad zoldu zaciagu ROT x liczba bohaterow** (wplyw maly-sredni, ryzyko male): Postfix na
   ROTClanFinanceModel.CalculateClanGoldChange zostawiajacy jeden wpis "Wages from". Najpierw zobaczyc
   w grze dymek dochodu przy 2+ kompanach (dowod), dopiero potem latka.
3. **Waga zbrojowni DTE** (wplyw duzy na logistyke, ryzyko srednie): doliczac do ciezaru partii gracza
   np. 50% wagi magazynu DTE (Postfix CalculateTotalWeightCarried, osobny wpis "Army stores"). Bez tego ani
   juczne, ani wozy nie maja sensu. Najpierw zmierzyc: ile kg ma dzis magazyn (log).
4. **Amunicja zbierana przez zwyciezce** (wplyw sredni, ryzyko male): w QuiversComeBack przepuszczac pusty
   kolczan tylko stronie wygranej; przegrany traci puste. To jest "ubywa tylko w polu" z 17.09.
5. **Wozy** (wplyw duzy na realizm, ryzyko srednie; dopiero po pkt 3) - projekt ponizej.
6. **Racje realniejsze** (wplyw sredni, ryzyko srednie - glod AI): jednostka jedzenia na 3-4 dni czlowieka
   zamiast 33 - dopiero gdy wozy i waga magazynu beda; dzis podniesienie zuzycia = glodujace armie AI.
7. Nagrody questow/areny do skali zoldu - dopiero po cenach uzbrojenia (sekcja 2, AUDYT-CEN).

### Projekt wozow (szkic, liczby do strojenia)

**Przedmioty** (nowe XML w Armoury/ModuleData, typ Goods, nowa kategoria `arm_cart`, sprzedawane w miastach;
NIE jako HorseComponent - bez flag bylyby "bydlem", liczylyby sie do stada i do jedzenia z miesa):

| id | nazwa (EN) | udzwig | zaprzeg | waga wlasna | cena gry | historia (SZACUNEK) |
|---|---|---|---|---|---|---|
| arm_cart | Two-wheeled Cart | +600 kg | 2 zwierzeta pociagowe | 150 kg | 80 | carecta 5-8 s = 60-96 d (zakres 3-10 s) |
| arm_wain | Four-wheeled Wain | +1 400 kg | 4 zwierzeta (woly lub konie) | 400 kg | 300 | wain/plaustrum 15-40 s = 180-480 d |

- **Zwierzeta pociagowe** = istniejace juczne: sumpter_horse, old_horse (Work Horse), mule, ox (BK).
  Ceny gry: kon roboczy 99 (historycznie affer 3-10 s = 36-120 d - OK); wol BK 300 (historycznie 10-13 s =
  120-156 d - za drogi x2, propozycja 150). Wozak historycznie 2 d/dzien; wynajem wozu z 3-4 konmi
  i wozakiem 12-24 d/dzien (szacunek) - w grze bez zoldu wozaka (woz prowadzi dowolny zolnierz).
- Zwierze w zaprzegu NIE daje swoich 100 kg i NIE liczy sie do stada (ciagnie, nie niesie): netto woz
  +400 kg za 2 konie (vs 200 kg jako juczne), wain +1 000 kg za 4 zwierzeta. To oddaje historie (kon w wozie
  ciagnie 2-3x tyle, ile uniesie na grzbiecie).
- Woz bez zaprzegu = martwy ladunek (wazy 150/400 kg). Waga wozu z zaprzegiem = 0 (Postfix
  GetItemEffectiveWeight dla kategorii arm_cart, jak vanilla robi z konmi).
- Realizacja: Postfix na NAJBARDZIEJ ZEWNETRZNYM CalculateInventoryCapacity (z licznikiem zagniezdzenia jak
  Rations/SpeedDepth - NavalDLC owija vanille, BK patchuje vanille) dodajacy wpis "Carts". Bez nowego modelu.
- **Predkosc**: woz zawsze = tabor dla MarchPace (sufit MarchTrainPace 4.2 -> 2.1, niezaleznie od
  MarchPackAllowance); wain z wolami: sufit 0.85 x tempo piechura (woly 15-20 km/dzien vs ludzie 25-30).
  Teren (plasko, styl TerrainEase, tylko gdy wozy > 0): las -0.15, gory/wzgorza -0.3, snieg -0.2, brod -0.3;
  drogi/rownina 0. Bloto jesienne juz daje RBL wszystkim.
- Utrata: zwykly przedmiot w ItemRoster -> po przegranej trafia do zwyciezcy jak lup vanilla.
- AI: na start TYLKO gracz. AI dopiero gdy pkt 3 (waga magazynu) obejmie AI; wtedy AiGear kupuje 1 woz na
  60 ludzi w miescie.

**Wizual na mapie**:
- A (najlepsze, do sprawdzenia w grze): raz przy zmianie stanu (wozy 0 <-> >0, NIGDY co klatke - pulapka
  z CLAUDE.md) doczepic prefab `map_icons_props_cart_b_full` (Native/Prefabs/map_icon_parts.xml:1257-1260,
  meta-mesh `mi_cart_b_full`) jako dziecko encji partii. Siatka jest w skali mapy, ale statyczna (bez kol
  i koni); skala i obrot wzgledem ikony partii NIESPRAWDZONE.
- B (bezpieczny zapas): drugie zwierze ikony jak u karawan - Postfix na
  `PartyComponent.GetMountAndHarnessVisualIdsForPartyIcon` (tw PartyComponent.cs:102-106; lordowie nie
  nadpisuja) zwracajacy `mule` + `mule_load_c` (wzor: CaravanPartyComponent.cs:281-292). Wolane tylko przy
  przebudowie ikony - bez ryzyka "co klatke". Nie woz, ale widac tabor.
- C: ROT `cart_village_animated_a` (animowany woz z krowa, ROT-Map/Prefabs/Mystaf_Outside.xml:19115) -
  dekoracja mapy, doczepienie do partii niesprawdzone.
- Ikona w ekwipunku: meta-mesh `bd_cart_a` (sceny Native) - czy renderuje sie jako ikona przedmiotu,
  NIESPRAWDZONE.
