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
