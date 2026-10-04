# Audyt cen, zoldu i lupow (2026-10-04)

Zamowienie Jeffa: "czy ceny sa realne do sredniowiecza, zrob audyt cen, od tego zaczniemy;
potem luki i zold dla wojska, ile dostawal zolnierz, plus jak bylo z lupami. Jak jest u nas."
Kontekst: nowa gra, wszystko do przerobienia. Nic jeszcze nie zmienione.

## Zrodla

- Gra: `Documents/.../CrashScribe/items-dump.csv` (wartosc `ItemObject.Value`, tylko `merchandise=1`,
  mediana na typ i tier), `DefaultPartyWageModel.GetCharacterWage` (1.4.8) razy `BaseWage` BannerKings
  z MCM Jeffa (1.2546), audyt ekonomii `economy-2026-10-04_05-47-36.csv` (371 rodow, 3 dni).
- Historia: Anglia ok. 1300-1350 (Edward I-III), wartosci przyblizone z literatury
  (Prestwich *Armies and Warfare*, Ayton *Knights and Warhorses*, Dyer *Standards of Living*).
  1 funt (L) = 20 szylingow (s) = 240 pensow (d). Do sprawdzenia, nie cytat.

## Klucz przeliczenia: 1 denar gry = ok. 1 pens

Zold w grze (dziennie, po mnozniku BK) prawie dokladnie pokrywa sie z angielskim zoldem w pensach:

| Tier w grze | Zold gry | Odpowiednik historyczny | Pensy dziennie |
|---|---|---|---|
| 1 | 2 | piechur z poboru | 2 |
| 2 | 3 | lucznik pieszy | 3 |
| 3 | 6 | lucznik konny | 6 |
| 4 | 10 | zbrojny (man-at-arms) | 12 |
| 5 | 15 | zbrojny weteran / sierzant konny | 12-18 |
| 6 | 21 | rycerz | 24 |
| (najemnik x1.5) | | banneret | 48 |

Tak samo pasuja dochody: mediana rodu krolestwa 421/dzien ~ 70 tys./rok (rok Armoury 168 dni)
~ 300 L rocznie = baron. Mediana krola 9.8 tys./dzien ~ 6900 L/rok = maly krol
(korona angielska ok. 30 000 L zwyczajnego dochodu). **Zold, dochody, konie i zwierzeta sa realne.**

## Ceny: co sie zgadza, a co nie

| Rzecz | Gra (denary) | Historia (pensy) | Gra / historia |
|---|---|---|---|
| Kon juczny (sumpter) | 99 | 60-120 (5-10 s) | ok. 1x |
| Kon wierzchowy | 140 | 120-240 | ok. 1x |
| Kon bojowy t3 (rouncey) | ok. 1 000 | 240-1 200 (1-5 L) | 1-4x |
| Rumak t5-t6 (destrier) | 10 000-11 000 | 9 600-19 200 (40-80 L) | ok. 1x |
| Wol | 300 | ok. 160 (13 s) | 2x |
| Krowa | 200 | ok. 120 (10 s) | 1.7x |
| Owca | 80 | ok. 15 | 5x |
| Strzaly t1 (stos) | 55 | 14-18 (snop 24 strzal) | 3-4x |
| Luk t1 | 1 800 | 12-18 (1-1.5 s) | **ok. 100x** |
| Luk t6 | 130 000 | luk najwyzszej jakosci moze 36-60 | **2 000x+** |
| Helm t2 | 7 700 | 36-120 (kapalin/bascinet 3-10 s) | **60-200x** |
| Helm t6 | 23 000 | 240-720 (helm turniejowy 1-3 L) | 30-100x |
| Zbroja tulowia t1-t2 (przeszywanica, kolczuga lekka) | 1 500-2 300 | 60-240 (5-20 s) | 10-25x |
| Zbroja t6 (pelna plyta) | 40 000 | 1 900-3 800 (pelny harness 8-16 L) | 10-20x |
| Miecz t1-t2 | 550-800 | 12-36 (1-3 s) | 20-60x |
| Miecz t6 | 21 000 | 240-1 200 (1-5 L, bron pana) | 20-90x |
| Tarcza t1 | 170 | 6-12 | 15-30x |
| Komplet zolnierza t1 | ok. 3 800 | 100-150 | **25-40x** |
| Komplet t3 | ok. 23 000 | 300-600 | 40-75x |
| Komplet t5 | ok. 71 000 | 2 500-5 000 (rycerz w plycie) | 15-30x |

Wniosek: **bron i zbroje sa 10-200 razy za drogie** wzgledem zoldu i dochodow, najbardziej luki,
helmy i tania bron. Przy 1 denar = 1 pens zolnierz t1 musialby pracowac ok. 5 lat na swoj komplet
(historycznie: 2-3 miesiace). Pozostala ekonomia trzyma skale.

Skutki, ktore juz widzielismy:
- AI nie moglaby kupowac sprzetu (rod mediana 421/dzien, komplet t3 = 23 000).
- Cotygodniowa doplata DTE za bron z taboru (`MoveRosterToArmory`, pelna `Value`) drukuje pieniadze -
  najemnicy bez fiefow maja 7-37 mln (hipoteza, niezweryfikowana logiem).
- Lupy sa warte majatek: sprzedaz zdobycznej zbroi bije dochody z fiefow.

## Lupy - historia

- Zolnierz zatrzymuje zdobycz, ale oddaje kapitanowi **jedna trzecia** ("thirds"), a kapitan
  trzecia czesc swoich "trzecich" krolowi (kontrakty angielskie XIV-XV w.).
- Jeniec nalezy do tego, kto go wzial; okup za jenca znacznego (krol, wodz) przejmuje korona
  za odszkodowaniem. Okup rycerza ~ jego roczny dochod (20-40 L = 5-10 tys. pensow);
  krol: Ryszard I 150 000 marek, Jan II francuski 3 mln ecu.
- Lupy ze zdobytego miasta szturmem - wolne dla zdobywcow; z miasta, ktore sie poddalo - nie.

## Lupy - u nas (stan na 04.10)

- Bitwa stoczona osobiscie: lupem rzadzi DTE (zabici zostawiaja ekwipunek, czesc trafiona
  ciosem smiertelnym przepada), dzialka gracza wg wkladu na ekran lupow (Armoury `BattlefieldLaw`).
- Bitwa automatyczna: Spoils of War (RealisticLoot), bez mnoznikow tieru; `LootScale` BK = 0.5.
- AI: lupy dzielone miedzy magazyny zwyciezcow (DTE `DistributeLootRandomly`).
- Okupy lordow: od 04.10 wedle pozycji (RealisticCaptivity `LordPrice`: lord 8 000, glowa rodu
  25 000, krol 100 000 + tier i fiefy) - lord 8 000 przy 1 denar = 1 pens ~ 33 L, czyli
  dokladnie skala okupu rycerza.

## Propozycja kolejnosci

1. **Ceny broni i zbroi**: przeskalowac `Value` tylko dla typow uzbrojenia (Head/Body/Leg/Hand/Cape,
   bronie, tarcze, luki, kusze, amunicja), tier po tierze, do mnoznikow z tabeli (docelowo
   komplet t1 ~150-300, t3 ~600-1 500, t6 ~4 000-8 000). Zostaja bez zmian: konie, zwierzeta,
   towary, zold. Do sprawdzenia przed zmiana: nasza kuznia (receptury i materialy), MarketGlut,
   Spoils of War, warsztaty BK, tabela okupow.
2. Potem dopiero: AI placi za sprzet (koniec darmowego przydzialu DTE) i zatkanie doplaty DTE.
3. **Werbunek od liczby mieszkancow** (Jeff 04.10, "po drugie"): policzyc populacje kazdego miasta,
   zamku i wioski w Westeros i Essos (BannerKings ma `PopulationData.TotalPop` per osada) i od niej
   liczyc, ilu ludzi mozna zwerbowac. Najpierw audyt populacji w grze (jak audyt ekonomii), potem regula.
