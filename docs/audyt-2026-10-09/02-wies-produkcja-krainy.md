# AUDYT 02 (09.10, noc): produkcja wsi z ludnosci, wioski poboczne, rozklad produkcji wedlug krain

Stan: tylko odczyt. Kod = wersja w grze (Armoury c01a54ba = commit 2e235ea, kopia w `SCR7\audyt\repo`), nic nie zmienione,
nic nie uruchomione. Jedyny nowy plik to ten raport. Skrypty i dane posrednie: `SCR7\wies-prod\` (`parse.py` -> `wsie.tsv`
= 571 wsi ROT z typem, hearth, kultura, polozeniem; `krainy.py` = klasyfikacja klimatu).

Oznaczenia: [K] kod (plik:linia), [P] pomiar z logu, [H] historia / lore ze zrodlem, [S] szacunek, [PROJ] decyzja projektu.
Skroty sciezek: `SCR7` = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`,
`OS` = `...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\ore-supply` (dekompilacje), `AP` = `...\3cf3e0ac-...\scratchpad\audyt-pieniadz`,
`LOG` = `Modules\Armoury\Armoury-2026-10-08_08-18-38.log` (autotest roczny, doby 108836-109199), `CSV` = `Modules\Armoury\Logs\2026-10-08_08-18-38\ludzie-regiony.csv`.

Co juz zbadano i czego NIE powtarzam (tylko odsylam i poprawiam): `docs/przekazanie-lawa-2026-10-07/AUDYT-PRODUKCJI-I-WARSZTATOW.md`
(krainy, 12 "bzdur" wsi, warsztaty, poprawki A1-A6, B1-B5), `docs/DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md` rozdz. 4.6 (wzor "plon od rak"),
`docs/paczki/113-ludzie-spustoszenie.md` (spustoszenie jako ulamek okregu, czynnik rak), `docs/HISTORIA-RABUNKU-I-BITEW-2026-10-07.md`
rozdz. 2.5-2.6 (odbudowa po najezdzie, elastycznosc 1.0), `docs/PROJEKT-GLOD-2026-10-07.md` (spichlerz okregu), `docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md`
(W0-W4), `docs/PROJEKT-WIOSKI-LORE-2026-10-08.md` (zajecia wiosek tekstem).

---

## 0. Dla Jeffa

1. Dzis wioska w grze NIE produkuje "tyle, ile jej ludzie". Gra patrzy tylko na trzy progi wielkosci wsi: mala wies daje pol normy,
   srednia cala norme, duza poltorej (u nas obciete do ok. 1.3). Wies, z ktorej ucieknie co trzeci czlowiek, czesto produkuje dokladnie tyle samo.
2. Odwrotnie tez: wsie same rosna szybko (w rocznym tescie o ok. 60%), wiec po roku produkuja ok. 10-20% wiecej "z niczego",
   bez nowych ludzi z ksiegi.
3. Wioski poboczne, ktore stoja na mapie, to dzis tylko obraz: kazda ma na sztywno 4 000 albo 6 750 dusz, nikt w nich nie pracuje
   na rachunek gry, a ogien na nich to dekoracja. Spalenie wioski pobocznej nie zmienia ani ludzi, ani plonu.
   Wejsc do nich tez sie jeszcze nie da (napis "Look over the district" jest przygotowany, ale opcji w menu nie ma).
4. Gdy ktos spladruje glowna wies, gra wylacza produkcje CALEGO okregu na 8-17 dni (wszystko albo nic), a potem wies jest "jak nowa".
5. Proponuje prosta zasade: plon okregu = praca ludzi, ktorzy sa w domu, we wszystkich jego wioskach (glownej i pobocznych).
   Spalona wioska poboczna = jej ludzie uciekaja albo gina = okreg traci jej czesc plonu (wioska 4 000 dusz w okregu 60 000 to ok. -7%),
   a plon wraca dopiero, gdy ludzie wroca i odbuduja (zwykle 2-3 lata w pokoju, w wojnie dluzej). Tak bylo w sredniowieczu: po najazdach
   szkockich na polnocna Anglie welna wrocila po 7-8 latach, mlyny po 13 latach dawaly 1/3.
6. Region tez ma znaczenie: dzis pszenica w piaskach Dorne rodzi tyle samo, co w Reach. Proponuje wspolczynnik zyznosci krainy
   (Reach i Dorzecze wiecej, Polnoc i Zelazne Wyspy mniej, za Murem rolnictwa nie ma), tak dobrany, zeby swiat razem mial tyle samo zboza.
7. Oliwek na Polnocy nie ma - to sie zgadza. Ale jest 16 wsi z rzecza, ktora tam rosnac nie moze: bawelna przy Murze, w Braavos,
   w gorach Doliny i w Sarnorze (10 wsi), winnica w Lorath, daktyle na Tarth, pszenica za Murem (4). Do tego papirus (roslina z bagien
   Nilu) rodzi sie dzis na KAZDEJ farmie zbozowej, takze na Polnocy i za Murem, oraz "las wsi" daje drewno na pustyniach Dorne i Qarthu.
   Wszystko to da sie poprawic tabelka w kodzie (bez ruszania plikow ROT).
8. Na dzis w nocy proponuje tylko male rzeczy: wpis do logu, ktory model naprawde liczy plon; poprawke 16 wsi + 5 wsi wyrownujacych
   bawelne; papirus tylko na goracym poludniu; drewno z lasu wedlug klimatu. Duza zmiana "plon od ludzi" czeka na ksiege ludzi (108-113),
   ktorej w grze jeszcze nie ma.

---

## 1. Stan dzis

### 1.1 Ktory model liczy plon - POPRAWKA wczesniejszych audytow

- [K] Produkcje towaru we wsi robi prefiks BK `EconomyPatches.TickGoodProductionPatch.TickGoodProduction` (`OS\EconomyPatches.cs:1130-1166`):
  lista z `PopulationManager.GetProductions`, a ilosc z **`Campaign.Current.Models.VillageProductionCalculatorModel`** (`:1148`).
- [K] Czynny model to `NavalDLCVillageProductionCalculatorModel` (log startu: "produkcja wsi NavalDLC.GameComponents.NavalDLCVillageProductionCalculatorModel",
  `LOG` 08:20:12). On tylko owija `BaseModel` (`OS\NavalVillageProd.cs`: `BaseModel.CalculateDailyProductionAmount` + polityki morskie +25% / -5%).
- [K] `BaseModel` = model zarejestrowany przed NavalDLC (`OS\cs\TaleWorlds.CampaignSystem\CampaignGameStarter.cs:76-80`, `GetModel<T>` :59-68).
  **BK nie rejestruje swojego modelu, gdy jest BetterEconomy** (`AP\bk_Main.cs:201-204`, `if (!ModCompat.BetterEconomy)`, `ModCompat.cs:114`),
  **a BetterEconomy nie rejestruje swojego, gdy jest BK** (`AP\be_SubModule.cs:104-107`, `if (!isActive)`). ROT modelu produkcji nie ma.
  Zostaje wiec **`DefaultVillageProductionCalculatorModel` gry** (`OS\DefaultVillageProductionCalculatorModel.cs:17-74`) - tak samo ustalil
  autor paczki 113 (`docs/paczki/113-ludzie-spustoszenie.md:14`).
- [K] Na tym modelu siedza latki: BK `BKEconomyLayerInstaller` - delta BK (prawo niewolnikow do kopaln, kon bojowy, perk Ritter) i
  **sufit: nadwyzka ponad `baza x 1.3` liczy sie w 20%** (`OS\BKEconomyLayerInstaller.cs:103-131, 167-214`; `VillageProductionMultiplier` 1.3 w
  `Configs\...\BannerKings\BannerKings.json`); nasze `MaterialLaw.ProdPostfix` (ruda i drewno x3, przelicznik ladunku 100 kg, `MineralOnce.Times`,
  `Armoury\src\MaterialLaw.cs:163-193`), `WinterBite.VillageProdPostfix` (`WinterBite.cs:104-114`).
- **Wzor, ktory naprawde dziala** [K] `DefaultVillageProductionCalculatorModel.cs:31, 36`: `baza_typu x (poziom_hearth + 1) x 0.5`, gdzie
  poziom = 0 ponizej 200 hearth, 1 od 200, 2 od 600 (`OS\Village.cs:320-330`). Czyli **x0.5 / x1.0 / x1.5 (po sufice BK ok. x1.34)**,
  potem budynki miasta, perki, polityki NavalDLC, zima. Stan wsi inny niz Normal = **0** (`:20`).
- **Dowod z pomiaru** [P] (`LOG`, linie "Towary: dzien 109150"; baza = liczba wsi typu x baza typu z `OS\DefaultVillageTypes.cs:259-270`,
  BK `OS\BKVillageTypes.cs:18-23`):

  | towar | wsi typu | baza na dobe | wsie dzis (doba 109150) | iloraz |
  |---|---|---|---|---|
  | papirus (farmy zboza, BK +0.5) | 128 | 64 | 80 | 1.25 |
  | len | 19 | 342 | 435 | 1.27 |
  | welna | 23 | 230 | 304 | 1.32 |
  | winogrona | 24 (po NorthernFare) | 264 | 337 | 1.28 |
  | bawelna | 13 | 104 | 136 | 1.31 |
  | daktyle | 15 | 120 | 153 | 1.28 |
  | ryby (rybacy) | 62 | 1736 | 2157 | 1.24 |
  | futra | 23 | 32 | 44 | 1.37 |
  | zboze farm pszenicy | 128 | 6400 | 8341 | 1.30 |

  Wynik 1.24-1.37 x baza to dokladnie wzor gry z progami i sufitem BK. Model BK "od robotnikow" (`OS\BKVillageProductionModel.cs:47-145`:
  0.0045 szt. na robotnika x udzial towaru) dalby papirusu kilka setnych sztuki na wies - **nie dziala**.
- **Co z tego wynika dla starszych dokumentow:** `docs/AUDYT-SUROWCE.md:45` ("aktywny model BKROTVillageProductionModel") i
  `AUDYT-PRODUKCJI-I-WARSZTATOW.md` rozdz. 1 ("BK daje ok. 1/4 bazy - kalibracja na rudzie") sa NIEAKTUALNE: kalibracja na rudzie pominela
  przelicznik ladunku (`MaterialLaw.cs:175`, `HistoricalPrices.BulkScale`). Tak samo martwe sa: model BK od pracy i ziemi (`LandData` -
  akry, zyznosc), blogoslawienstwa BKROT (+25% Kowal i Bogowie Lata, +50% drwale i rybacy, `OS\BKROTVillageProductionModel.cs`),
  model BEE (chlopi, pory roku, `ProductionProfiles` krain) oraz dodatki BK "per wies" spoza listy typu: jablka, marchew, pomarancze, miod,
  kury, gesi, chleb (`PopulationManager.GetProductions` je dokleja, ale model gry daje 0 dla towaru spoza `VillageType.Productions`).
  [P] Potwierdzenie: jablka, pomarancze i miod w ksiedze maja produkcje tylko z "inne ticki dobowe (osady)", zero z "wsie" (`LOG` doba 109150).
  Papirus (BK dopisal go do LISTY typu farmy zboza) i przyprawy (do listy daktyli) - dzialaja: 80 i 9 na dobe.

### 1.2 Od czego zalezy plon wsi dzis

| Czynnik | Dzis | Dowod |
|---|---|---|
| ludzie wsi | tylko przez progi hearth 200 / 600; ksiega ludzi (PopulationLaw) nie wchodzi | [K] `DefaultVillageProductionCalculatorModel.cs:31` |
| start kampanii | 12 wsi < 200, 522 w pasmie 200-599, 37 >= 600 (mediana 350) | [K] `ROT-Map\ModuleData\settlements.xml`, `SCR7\wies-prod\wsie.tsv` |
| wzrost wsi | srednio 378 -> 615 hearth w 364 doby (+63%), bo ksiegi przyrostu (109) w grze nie ma | [P] `CSV`: suma hearth 215 331 -> 350 184 przy 569 wsiach |
| skutek wzrostu | plon wsi +6..+21% w rok (len 382 -> 462, welna 267 -> 306, ryby 2107 -> 2238, papirus 75 -> 80) - "z niczego" | [P] `LOG` linie "Towary" dob 108845 / 109198 |
| rabunek | gra: -39.7% hearth za pelny rabunek; 488 z 571 wsi nie traci przez to ani sztuki plonu (prog sie nie zmienia), wies tuz nad progiem -33..-50% | [K]/[P] `docs/paczki/113-ludzie-spustoszenie.md:8`, `DEMOGRAFIA-SILA-ROBOCZA` :491 |
| stan Looted | caly okreg 0 plonu przez 8.3-16.7 doby, potem pelny | [K] `113-ludzie-spustoszenie.md:13` (VillageHealCampaignBehavior), `DefaultVillageProductionCalculatorModel.cs:20` |
| marsz wrogiej armii | `ScorchedEarth` zdejmuje hearth (0.8 na 500 ludzi dziennie, podloga 25) i bierze zboze | [K] `Armoury\src\ScorchedEarth.cs:14-30` |
| kraina / teren / zyznosc | brak. Zyznosc BK (`LandData.cs` Init, pustynia 0.5, rownina 1.0) uzywana tylko w zywnosci MIASTA (`BKFoodModel.cs:178`) | [K] |
| pora roku | tylko zima: -50% x gradient polnocy 0.75-1.25 (`WinterBite.cs:50-57, 104-114`; Jeff: Cut 50, Gradient 25 w `Armoury.json`); BEE pory neutralne (`WinterSource.cs`) | [K] |
| zima w tropikach | Wyspy Letnie, Qarth, Dorne, Zatoka Niewolnicza: -38..-43% w zimie (gradient nie schodzi ponizej 0.75) | [K] + rachunek na `wsie.tsv` |
| zywnosc dla warowni | `CalculateDailyFoodProductionAmount` = poziom + 1 (1-3 na dobe), zima tnie | [K] `:76-88`, `WinterBite.cs:116-127` |

W rocznym autotescie zimy nie bylo (lato 143 doby, potem jesien 344 doby, `LOG` "Klimat"), wiec pomiar roczny nie pokazuje ciecia zimowego.

### 1.3 Wioski poboczne na mapie (W2)

- [K] `MapVillagesView.cs` (4 448 linii) to SAM WIDOK: plik `Modules\Armoury\ModuleData\arm_map_villages.tsv` (v4, 2 446 wiosek w 425 okregach,
  mediana 5 na okreg, najwiecej 18). Naglowek: "NIC nie idzie do zapisu gry; spalona / mniejsza / odbudowa to ksiega wiosek (W3)" (`:7`).
- [K] Ludzie w dymku = `settlements x 250` na sztywno z pliku (`MapVillagesView.cs:4426`). [P] W pliku: 16 osad (4 000 dusz) w 1 981 wioskach,
  27 osad (6 750) w 280; razem 42 092 osady = **10.52 mln ludzi = 25.1% ludzi wsi ksiegi** (41.98 mln, `CSV` doba 108836); udzial w regionie
  od 0 do 90% (New Ibbish 90%, Gelona / Ghoyan Drohe 87%), mediana 30%.
- [K] Ogien tylko na pierwszej wiosce lancucha, gdy wies gry `IsUnderRaid`; dym, gdy `IsRaided` (`MapVillagesView.cs:1865`). Brak stanu,
  brak zwiazku z hearth, ksiega ludzi, BK, plonem.
- [K] Wejscia do wioski nie ma: tekst `arm_dist_option` "Look over the district" istnieje (`VillageTexts.cs:27`), ale zadna opcja menu go nie
  uzywa (brak `AddGameMenuOption` w W2). Menu okregu to projekt (PROJEKT-WIOSKI-LORE rozdz. 7, krok L3).
- [P] Obraz a gospodarka: 335 z 400 wiosek "fishing" stoi w okregach, ktore ryb nie produkuja (typ wsi gry inny niz rybacy); 232 z 358 mlynow
  wodnych w okregach bez farmy zboza (`arm_map_villages.tsv` x `settlements.xml`). Nie blad - okregi byly mieszane - ale gra tej mieszanki nie liczy.

### 1.4 Cztery rozne "ludnosci" tej samej wsi

| Liczba | Skad | Typowo (wies-mediana) | Czy zmienia plon |
|---|---|---|---|
| hearth gry | `settlements.xml`, rosnie vanilla | 350 -> ok. 600 w rok | TAK, tylko progami |
| ludzie ksiegi (PopulationLaw) | hearth x k krainy (Reach ok. 220 / hearth, Zelazne Wyspy ok. 50) | ok. 60 tys. na okreg | nie (tylko renta) |
| ludnosc BK | hearth x 4-6 na start (`PopulationManager.cs:471-480`), wlasny wzrost | ok. 1.5-2 tys. | nie (model BK nieczynny) |
| wioski na mapie | plik, 250 na osade, stale | 22 750 w wioskach okregu (mediana) | nie |

Jednostki czlowieka rozjechane opisuje juz `DEMOGRAFIA-SILA-ROBOCZA` L7; tu dochodzi czwarta (plik wiosek).

### 1.5 Co zaprojektowano, a czego nie ma w grze

- 108 (jednostka czlowieka), 109 (przyrost z ludzi zamiast +4/+1.2/+0.2 hearth), 113 (spustoszenie jako ulamek okregu + **"plon od rak":
  plon x (ludzie w domu / ludzie sprzed spustoszenia)^0.5** w `MaterialLaw.ProdPostfix`, progi hearth liczone sprzed spustoszenia) - na
  galeziach (`paczki-na-120/*`), NIE w grze ([K]: w repo gry brak `Devastation.cs`, brak klucza `arm_people`).
- W3 "ksiega wiosek" (b_w = puste osady kazdej wioski, spalona przy polowie, odbudowa z powrotow 113) - projekt.
- Spichlerz okregu (PROJEKT-GLOD) - projekt, czyta `q` = mnoznik gry.
- Poprawki krain A1-A6 z audytu lawy - NIE wdrozone; w grze jest tylko `NorthernFare` CrashScribe (4 winnice Polnocy, `CrashScribe\src\Mends.cs:2112-2160`).

---

## 2. Jak bylo w sredniowieczu i w lore [H]

### 2.1 Pole, plon, gospodarstwo

- Plon z ziarna siewnego: Sussex 1350-1399 pszenica 4.34, jeczmien 4.01, owies 2.87 ziarna z jednego wysianego; z akra 7-15 buszli
  (470-1 000 kg/ha), w zly rok ponizej 4 buszli. Rosja 1850 ok. 600 kg/ha. [H] https://en.wikipedia.org/wiki/Agriculture_in_the_Middle_Ages
- Plon netto (po odjeciu ziarna siewnego i ugoru) poludniowej Anglii przed Czarna Smiercia ok. 4 buszli pszenicy z akra uprawnego (Clark 1992,
  wg streszczenia) [H] https://www.medievalists.net/2010/08/the-economics-of-exhaustion-the-postan-thesis-and-the-agricultural-revolution ;
  baza plonow z rachunkow dworskich 1211-1491: https://www.bahs.org.uk/crop-yields-database/ ; szereg 1270-1870 (Broadberry, Campbell i in.):
  https://warwick.ac.uk/fac/soc/economics/research/centres/cage/data/visualisation/04-04-22-crop_yields_england_1270_1870
- Plon zalezal przede wszystkim od pogody roku (panel 49 dworow biskupa Winchester 1349-70) [H] https://www.nuff.ox.ac.uk/economics/history/Paper90/schneider90.pdf
- Trojpolowka: co roku 1/3 ziemi odlogiem; dwupolowka (poludnie) - 1/2. [H] Wikipedia jw.
- Gospodarstwo: lan (virgate) nominalnie ok. 30 akrow (12 ha), "tyle, ile zaorze para wolow w sezonie"; w spisie 1279 (7 hrabstw) 46% rolnikow
  mialo ponizej 10 akrow (za malo na rodzine), 33% pol lanu (12-16 akrow), 20% caly lan; pan mial 32% ziemi ornej. [H]
  https://en.wikipedia.org/wiki/Virgate , https://en.wikipedia.org/wiki/Agriculture_in_the_Middle_Ages , przyklad 1279: https://sourcebooks.web.fordham.edu/source/alwalton.asp
- Jedzenie: zboze dawalo do 80% kalorii robotnika zniwnego na poczatku XIV w. (chleb, piwo, polewka); z upraw ok. 1 669 kcal na glowe dziennie
  ok. 1380 r. (Overton i Campbell wg Harris i in.) [H] https://strathprints.strath.ac.uk/50435/ ; w repo przyjeta racja 0.6 kg zboza / 2 000 kcal
  i nadwyzka wsi 25% (PROJEKT-GLOD 2.1-2.2).
- Rachunek osady gry (250 ludzi = ok. 50 rodzin) [S]: ok. 0.9 ha ornej na osobe, 2/3 obsiane -> 150 ha x 0.7 t = 105 t brutto, minus ziarno
  siewne (1:4.3) ok. 80 t netto = ok. 220 kg/dobe, a zjada 250 x 0.6 = 150 kg -> nadwyzka ok. 30% na rynek, pana i dziesiecine. Zgodne z 1.25 z PROJEKT-GLOD.
  Okreg 60 tys. ludzi = ok. 53 t zboza netto dziennie; gra daje farmie zboza 50-65 sztuk po 10 kg = 0.5-0.65 t, czyli skala rynku ok. 1:80-1:100 -
  zgodnie z decyzja Jeffa 05.10 ("nie mnozymy do pelnych wielkosci na glowe tam, gdzie gra nie ma odbiorcy"). Model ma byc wiec WZGLEDNY:
  plon okregu rosnie i maleje proporcjonalnie do pracujacych ludzi, a jego poziom ustawiaja kroki K13.

### 2.2 Hodowla, rybolowstwo, rzemioslo wiejskie

- Ciezka gleba wymagala zaprzegu, najlepiej 8 wolow; niewielu chlopow mialo caly zaprzeg - orano wspolnie [H] Wikipedia jw. Strata wolow
  przy najezdzie = strata orki na sezon (Bolton: zabrano 43 woly) [H] `HISTORIA-RABUNKU` 2.6.
- Stada byly lupem nr 1 najezdzcow i wracaly najwolniej: welna Northumberland wrocila do poziomu 1312 r. 7-8 lat po rozejmie 1323,
  Jarrow -96% owiec 1313-1326 (z pomorem) [H] McNamee 1997 wg `HISTORIA-RABUNKU` 2.5-2.6.
- Rzemioslo wiejskie i dorabianie (przedzenie, tkanie, piwowarstwo, kowal, mlynarz) bylo zajeciem "obok pola" - zalezy od rak tak samo jak pole.
  Rybolowstwo morskie zalezalo od lodzi (kapital): spalone lodzie = brak polowu do zbudowania nowych.

### 2.3 Co bylo z produkcja po spaleniu wsi [H] (zrodla w `HISTORIA-RABUNKU-I-BITEW-2026-10-07.md` rozdz. 2.5-2.7, 7)

| Przypadek | Co z plonem i ludzmi | Ile trwala odbudowa |
|---|---|---|
| pojedynczy najazd, potem pokoj (Thierache 1339-40) | wszystkie 174 parafie odbudowane; ulgi podatkowe 5-7 lat | kilka lat |
| najazdy co roku (Northumberland 1311-1322) | Wark 1323: 936 akrow dworu i 416 akrow lanow odlogiem "ze strachu"; Penrith wydzierzawione 15 ze 120 akrow; Stenton pusta 4 lata | welna 7-8 lat po rozejmie, mlyny po 13 latach 1/3, dziesieciny Holy Island -87% i "nigdy w pelni" |
| zima 1069-70 (Harrying of the North) | spalone zbiory, stada i ziarno; glod; Domesday 1086: 1/3 ziemi Yorkshire nadal "waste" (kategoria sporna - Lewis 2023) | dziesieciolecia; wsie przebudowane w XII w. |
| dluga wojna (Ile-de-France, Normandia 1415-1450) | dekanat Montmorency 1470 < 1/3 stanu z 1328; Gorna Normandia: produkt rolny +45% w ok. 30 lat po dnie 1463-64 | 50-100 lat, przez przybyszow |

Wniosek dla modelu: (1) plon spalonej czesci spada **o tyle, o ile ubylo rak, albo bardziej** (elastycznosc 1.0, nie 0.5 - kapital: woly,
ziarno, mlyny, lodzie); (2) wraca **wolniej niz ludzie** (najpierw ludzie, potem stada i mlyny); (3) nietknieta reszta okregu pracuje dalej.

### 2.4 Klimat roslin [H] i lore krain [H]

- Oliwka: zabija ja mroz ok. -8 C i wiosenne przymrozki; potrzebuje lagodnej, deszczowej zimy i goracego, suchego lata (klimat srodziemnomorski)
  https://spj.areeo.ac.ir/article_111279.html?lang=en , https://pmc.ncbi.nlm.nih.gov/articles/PMC9144808/
- Palma daktylowa: rejony suche, dlugie gorace lato, > 2 000 jednostek ciepla od kwitnienia do owocu; srednia najzimniejszego miesiaca > 2 C
  https://www.fao.org/3/y4360e/y4360e08.htm
- Bawelna w sredniowieczu: przywieziona do Sycylii i Hiszpanii w IX w., uprawiana na nawadnianych polach al-Andalus; na Sycylii towar od XIII w.
  https://www.islamicspain.tv/the-science-and-culture-of-islamic-spain/25-subjects-of-science-and-culture/agriculture , https://www.medieval.eu/?p=2644
- Winorosl: Domesday ok. 42-46 winnic, wszystkie na poludnie od linii Ely - Gloucestershire; dalej na polnoc pojedyncze, niepewne wzmianki -
  winorosl "na samej granicy zasiegu" https://www.realclimate.org/index.php/archives/2006/07/medieval-warmth-and-english-wine/ ,
  https://www.awri.com.au/wp-content/uploads/2018/07/Kent.pdf . Westeros: najlepsze wino z Arbor (Redwyne), potem Dorne
  https://awoiaf.westeros.org/index.php/The_Arbor ; Dorzecze odpowiada pasowi Burgundii-Nadrenii - winnice mozliwe, ale nie slynne.
- Papirus (Cyperus papyrus): bagna Nilu, w sredniowieczu takze Sycylia - roslina goraca i bagienna; Westeros pisze na pergaminie [H] wiedza ogolna,
  `AUDYT-PRODUKCJI-I-WARSZTATOW` 3.2.
- Za Murem rolnictwo jest "niepraktyczne", wolni ludzie to glownie mysliwi https://awoiaf.westeros.org/index.php/Beyond_the_Wall ,
  https://awoiaf.westeros.org/index.php/Free_folk . Polnoc w realnym odpowiedniku (Szkocja, Orkady): jeczmien dojrzewa pewniej niz owies,
  owies siega najdalej na polnoc https://pure.uhi.ac.uk/pt/publications/the-agronomic-performance-and-nutritional-content-of-oat-and-barl/ .
- Pory roku Westeros wieloletnie; zima uderza najmocniej na polnocy https://awoiaf.westeros.org/index.php/Seasons ,
  https://awoiaf.westeros.org/index.php/Climate . Wyspy Letnie lezace przy rowniku nie maja zimy w sensie Westeros [S z nazwy i polozenia].

---

## 3. Luki i bledy logiki

| Nr | Luka | Dowod |
|---|---|---|
| L1 | Plon nie zalezy od ludzi, tylko od 3 progow hearth. Wies traci 1/3 ludzi i produkuje tyle samo (488/571 wsi po rabunku) albo od razu -33..-50% (wies tuz nad progiem). Sprzeczne z decyzja Jeffa 05.10 "Plon zalezy od liczby ludzi". | [K] 1.1, [K/P] 113 :8 |
| L2 | Plon rosnie z niczego: hearth +63% w rok -> +6..21% towaru wsi. Sprzeczne z "nie ma rzeczy darmowych"; projekt obiegu liczy plon staly (`PROJEKT-EKONOMIA-OBIEG` :472 "plon wsi staly 374 tys. dziennie"). | [P] 1.2 |
| L3 | Spladrowanie = caly okreg 0 przez 8-17 dob, potem 100%. Sprzeczne z decyzja Jeffa 07.10 "spladrowana wies produkuje wedle tego, co zostalo (symbol), nie zero na 8-17 dob". | [K] 1.2 |
| L4 | Wioski poboczne nie maja ludzi w rachunku gry: stale 4 000 / 6 750 z pliku, spalenie to dekoracja. Ich ludnosc (25% ludzi wsi) nie jest czescia zadnej ksiegi. | [K] `MapVillagesView.cs:4426, 1865` |
| L5 | Brak zyznosci krainy: farma zboza w piaskach Dorne = farma w Reach; za Murem i na Skagos farmy zboza; kazdy typ wsi (takze rybacy, kopalnie za Murem) ma "zboze 3". | [K] `DefaultVillageProductionCalculatorModel.cs:22-36`, `LandData` nieczynne |
| L6 | Zima bije tropiki: Wyspy Letnie, Qarth, Dorne -38%; Essos poludniowe ma zime Westeros 1:1. | [K] `WinterBite.cs:50-57` |
| L7 | 16 wsi z towarem niemozliwym w klimacie (tabela 4.D), 36 watpliwych. | [K] `settlements.xml` + `krainy.py` |
| L8 | Papirus na 128 farmach zboza, w tym ok. 100 w Westeros i na polnocy Essos: ok. 62 z 80 sztuk dziennie (po 100 d) w miejscach, gdzie nie rosnie. | [K] `BKVillageTypes.cs:22`, [P] `LOG` 109150 |
| L9 | "Las wsi" 2.5 ladunku na dobe dla kazdej wsi bez drwali, takze na pustyniach (Dorne, Qarth, Zatoka Niewolnicza, Czerwone Pustkowie); w sumie 1 267 ladunkow dziennie. | [K] `VillageWoodlot.cs:13-30, 96-102`, [P] `LOG` 109150 "las wsi (126) 1267" |
| L10 | Wczesniejsze audyty i plan K13 licza produkcje z zalozenia "model BK, 1/4 bazy" - liczby welny / lnu / skor w PLAN-K13 tab. :51-55 sa oparte na pomiarze (dobrze), ale opis modelu i "kalibracja na rudzie" (AUDYT-SUROWCE :45, audyt lawy rozdz. 1) - zle. | [K]/[P] 1.1 |
| L11 | Martwe mechaniki, ktore ktos moze uznac za dzialajace: blogoslawienstwa BKROT na plon, zyznosc i akry BK, dodatki BK per wies (jablka, pomarancze, miod, kury), `ProductionProfiles` BEE. Poprawianie ich klimatu NIC nie zmieni. | [K]/[P] 1.1 |
| L12 | Okregi bez wiosek (134-146 z 571: Zelazne Wyspy, Qarth, step, Skagos, Straz) - rachunek "glowna + poboczne" musi dzialac takze tam (glowna = 100%). | [K] PROJEKT-WIOSKI 1.7, [P] 425 okregow z wioskami |

---

## 4. Propozycje

Zasady wspolne: poziom swiata nie zmienia sie po cichu (decyzja Jeffa 06.10 pkt 3) - kazdy nowy mnoznik jest wyrownany tak, zeby w dniu
startu nowej kampanii swiat produkowal tyle samo co dzis; poziom podnosza tylko jawne kroki K13 (x1.3 / 1.6 / 1.9 z popytem).
Kazda zmiana z wylacznikiem MCM, logiem i autotestem; wgranie tylko na "wgraj".

### A. Plon okregu = praca ludzi w jego wioskach (pytanie Jeffa)

**A1. Zamrozic progi hearth dla plonu (most do czasu 108-113).** [P1, mala-srednia]
- Co: w `MaterialLaw.ProdPostfix` (jedno miejsce, ktore juz mnozy wynik modelu, `MaterialLaw.cs:163-193`) dopisac czynnik
  `(poziom_startowy + 1) / (poziom_dzis + 1)` liczony z FAKTYCZNEGO ilorazu wzoru gry z sufitem BK (KRYT 11 z DEMOGRAFIA 4.6: poziom 2 = x1.34,
  nie x1.5). Poziom startowy = z hearth w `ROT-Map\ModuleData\settlements.xml` (czytany raz przy starcie, bez zapisu w sejwie).
- Liczby: swiat w dniu wlaczenia bez zmian; po roku plon nie rosnie o 6-21% (L2). Uzasadnienie: przyrost ludzi wsi to 0.3-0.5% rocznie (Broadberry
  1086-1290: 0.50%), a nie +63%.
- Rozgrywka: tak (dochody panow z wsi przestaja rosnac same). Nowa kampania: nie (dziala od dnia wlaczenia, swiat zostaje na dzisiejszym poziomie -
  w starej kampanii "zamraza" juz podniesiony poziom). Ryzyko: male (mnozenie wyniku; JIT `GetHearthLevel` nie dotyczy, bo liczymy z hearth).
- Zaleznosci: znika, gdy wejdzie 109 (przyrost z ksiegi) + 113 (progi sprzed spustoszenia); do projektu obiegu (162-168) pasuje lepiej niz dzis
  ("plon staly"). Kolizja: 169 mierzy obieg - wlaczac PO zebraniu pomiaru 169.

**A2. Plon od rak - 113 z dwoma poprawkami.** [P0 po 108-113, duza]
- Co: wzor 113 (`plon x (L_dom / L_start)^e`) zostaje, ale:
  (a) **elastycznosc 1.0 dla ludzi wygnanych przez spustoszenie** (rabunek, marsz), **0.5 dla ubytku bez zniszczen** (polegli, zaraza, pobor) -
  zgodnie z `HISTORIA-RABUNKU` 2.6 (kapital ginie razem z ludzmi; Broadberry 0.47-0.71 opisuje zaraze, nie spalenie);
  (b) **odbudowa kapitalu wolniej niz powrot ludzi**: osoba wracajaca do spalonej czesci pracuje pierwsze 364 doby na 50% (brak wolow, ziarna,
  mlyna), potem 100% [S, z "welna 7-8 lat, mlyny 1/3 po 13 latach" w wojnie i "ulgi 5-7 lat" w pokoju; 1 rok to dolna granica dla pokoju].
- Liczby (okreg-mediana 60 tys.): jeden rabunek pocztu 112 ludzi = 0.2% okregu = plon -0.2% (dzis 0 albo -33%); spalona wioska poboczna 4 000 = -6.7%;
  spalona polowa okregu = -50%, wraca: polowa ludzi po ok. 2.4 roku (113), plon do 90% po ok. 3.5 roku [S].
- Rozgrywka: tak (wojna zostawia slad w dochodach i cenach zboza na lata). Nowa kampania: tak (lancuch 104/105 i tak wymaga). Ryzyko: srednie
  (male krainy - Straz, Wolni Ludzie - przy ciaglych najazdach x0.3-0.5; to zgodne z historia Cumberlandu, ale pokretlo `Devastation Max Percent`).
- Zaleznosci: 108, 109, 113 (wszystko na galeziach); W3.

**A3. Wioski poboczne jako czesc okregu (W3 + A2).** [P1 po A2, srednia]
- Co: ludzie okregu L (ksiega) dzielimy miedzy wioski wedlug osad z pliku: wioska w ma `s_w = n_w / N`, gdzie `N = round(L0 / 250)` (PROJEKT-RABUNEK pkt 1),
  glowna (wies gry) = reszta, co najmniej tyle co najwieksza wioska (W rozdz. 3). Dymek pokazuje ludzi z ksiegi (`L x s_w x (1 - b_w / n_w)`),
  nie `settlements x 250` (`MapVillagesView.cs:4426`). Spalenie osady w wiosce w (b_w +1) zdejmuje z plonu okregu `1/N` z elastycznoscia 1.0 (A2a).
  Stan Looted gry przestaje zerowac caly okreg: zeruje tylko spalona czesc (decyzja Jeffa 07.10). Technicznie: postfiks `Village.VillageState` w
  modelu nie wchodzi w gre - zamiast tego w `TickGoodProduction` (prefiks BK) dla Looted liczymy wynik jak dla Normal i mnozymy przez czesc niespalona
  (osobna, mala latka na wywolaniu modelu; do sprawdzenia, czy BK prefiks nie konczy sie wczesniej dla Looted - `EconomyPatches.cs:1130-1135` nieczytelne w dekompilacji).
- Liczby: jak A2; "glowna" bez wiosek (okregi bez obrazkow, L12) = 100% w jednej wsi, ta sama regula.
- Rozgrywka: tak (gracz widzi, ktora wioska splonela i ile to kosztuje okreg). Nowa kampania: tak. Ryzyko: srednie - zmiana zachowania stanu Looted
  dotyka wszystkich modow czytajacych produkcje; AI dalej rabuje tak samo (CLAUDE.md 8.3).
- Zaleznosci: W3 (b_w), 113, PROJEKT-RABUNEK (osada po osadzie).

**A4. Mieszanka okregu z wiosek (opcjonalnie).** [P2, srednia]
- Co: wioska "fishing" daje ryby, "mill / windmill / granary / farm" zboze - w miejsce "zboza 3" typu wsi gry, w proporcji ludzi tej wioski, wartosc
  okregu bez zmian (wymiana, nie dodatek). 335 wiosek rybackich w okregach bez ryb dostaje sens gospodarczy.
- Liczby: ok. +3 sztuki ryb dziennie na okreg z wioska rybacka, -3 zboza [S]. Rozgrywka: drobna. Nowa kampania: nie. Ryzyko: male. Zaleznosc: A3.

**A5. Zywnosc wsi dla warowni od tych samych rak.** [P1 razem z A2]
- `CalculateDailyFoodProductionAmount` (1-3 na dobe) dostaje ten sam czynnik co towar - tak juz planuje 113 (`FoodPostfix`). Bez zmian w projekcie, tylko
  przypomnienie, ze A3 (czesc niespalona) ma objac tez zywnosc.

### B. Wydajnosc krainy i pory roku

**B1. Zyznosc krainy dla upraw (R).** [P2, srednia]
- Co: mnoznik R tylko dla upraw (zboze, len, winogrona, oliwki, daktyle, bawelna, papirus) w `MaterialLaw.ProdPostfix`, wedlug kultury osady
  (ten sam klucz co `PopulationLaw.Table`):
  Reach 1.20, Dorzecze 1.10, Volantis / Rhoyne 1.15, Ziemie Zachodu, Dolina, Korona, Ziemie Burzy, Pentos, Myr, Lys, Tyrosh, Norvos, Qohor 1.00,
  Dorne 0.85 (piaski z oazami i Greenblood), Zatoka Niewolnicza 0.90, Smocza Skala 0.85, Braavos 0.85, Polnoc 0.80, Lorath, Ibben 0.70,
  Zelazne Wyspy 0.65, Qarth 0.60, Skagos 0.50, Dothrakowie 0.70, Nocna Straz / Dar 0.70, za Murem 0 (bez rolnictwa - patrz D),
  potem jedna stala wyrownujaca swiat do 1.00 (wazone baza zboza).
- Uzasadnienie: realne plony z ziarna wahaly sie 1:3 (owies, ziemie marginalne) do 1:4-1:5 (pszenica dobrej ziemi) - stosunek ok. 0.6-1.4 wokol
  sredniej; lore: Reach "najzyzniejsza", Zelazne Wyspy "cienka, kamienista gleba" (PROJEKT-WIOSKI-LORE 1.3), za Murem rolnictwo niepraktyczne.
- Rozgrywka: tak (zboze tanieje w Reach, drozeje na Polnocy i Wyspach - wiecej handlu zbozem, jak w lore "Reach karmi Krolewska Przystan").
  Nowa kampania: nie. Ryzyko: srednie - Polnoc i Wyspy maja juz deficyt zywnosci w zimie; sprawdzic linie "zywnosc" i glod warowni (PROJEKT-GLOD).

**B2. Zima wedlug klimatu, nie tylko wedlug Y.** [P2, mala]
- Co: w `WinterBite.Northness` (`WinterBite.cs:50-57`) dla kultur cieplych mnoznik ciecia: Wyspy Letnie 0, Qarth, Zatoka Niewolnicza, Dorne,
  Lys, Tyrosh, Myr, Volantis, Valyria 0.4 (zima = chlodniejsza pora deszczowa), reszta bez zmian.
- Liczby: Dorne zima -15% zamiast -38%; Wyspy Letnie 0 zamiast -38%. Uzasadnienie: zima Westeros jest zjawiskiem polnocy (AWOIAF Seasons/Climate),
  a w klimacie srodziemnomorskim zima to pora siewu, nie glodu. Rozgrywka: tak (poludnie staje sie spichlerzem w zimie - impuls do handlu).
  Nowa kampania: nie. Ryzyko: male. Zaleznosc: brak; NIE da sie sprawdzic autotestem 40 dob bez zimy (na pozniej).

**B3. Rytm zniw (opcjonalnie, z PROJEKT-GLOD).** [P2, srednia] Plon zboza nie co dzien rowno, tylko w oknie zniw (np. 8 tygodni lata / jesieni,
STAN-PRAC "okno zniw dni 196-252"), reszta roku z zapasu spichlerza okregu. Bez spichlerza (PROJEKT-GLOD) nie ma sensu - zostawic do kroku B.

### C. Rozklad produkcji wedlug krain (pytanie Jeffa "czy oliwek nie produkuje sie na polnocy")

Odpowiedz: **oliwek na polnocy nie ma** (19 gajow: Reach 5, Dorne 6, Ziemie Burzy 2, Volantis 2, Lys, Tyrosh, Myr po 1, Dothrakowie 1 - wszystkie
w strefie srodziemnomorskiej albo goracej; dwa na granicy - Rooster's Top i Rhaes Azor). Bledy sa gdzie indziej - tabela D.

**C1. Tabela klimatu wsi (A1 + A2 audytu lawy, rozszerzona).** [P1, mala, dane w kodzie]
- Co: zamienic slownik `NorthernFare` (`CrashScribe\src\Mends.cs:2117-2123`) na tabele `id -> (typ_stary, typ_nowy)` (warunek "typ_stary"
  zamiast stalego "vineyard" w `:2133`), dopisac 16 zmian z D.1 i 5 zmian wyrownujacych bawelne (D.2); zbierac miasta po `Village.TradeBound`
  (blad A2 audytu lawy: dzis tylko `Bound`, `:2134, :2140`) i przestawiac w nich KAZDY warsztat, ktoremu zniknal wsad (tlocznia -> browar,
  aksamit -> tkalnia lnu). Wywolac takze przed rozdaniem warsztatow nowej gry (A1 audytu lawy: prefiks `WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart`).
- Liczby: bawelna 104 -> 64 bazy (8 wsi, wszystkie w cieplych strefach) [rachunek z tabeli D: 13 - 10 + 5]; welna +30, len +54, bydlo +4,
  ryby +112, konie stepowe +1 wies, drwale +1 / -3, daktyle -3, zboze farm -4 x 50 bazy (za Murem).
  [P] bawelna ma dzis lekki deficyt (zapas 10 093, -19/dobe) - po zmianie aksamit podrozeje; to luksus, akceptowalne, inaczej dodac 1-2 wsie bawelny
  na poludniu Essos.
- Rozgrywka: tak (inne towary w 21 wsiach; aksamit drozszy). Nowa kampania: zalecana (warsztaty), stare zapisy dzialaja (zmiana typu w `OnSessionLaunched`).
  Ryzyko: male-srednie (warsztaty bez wsadu - dlatego razem z A3 audytu lawy "przerobka tylko przy surowcu").

**C2. Papirus tylko tam, gdzie rosnie.** [P1, mala]
- Co: w `MineralOnce.Postfix` (juz latka na `GetProductions`, `MineralOnce.cs:79`) - jedna linia filtra: usunac `Papyrus` z listy wsi, ktorej kultura
  nie jest w {aserai, ghiscari, qartheen, volantine, lyseni, myrish, tyroshi, valyrian, summer}. Model gry liczy towar z listy BK, wiec usuniecie
  z listy = brak produkcji (prefiks BK iteruje te liste, `EconomyPatches.cs:1143-1148`).
- Liczby [P/S]: papirus 80 -> ok. 18-20 sztuk dziennie (25 z 128 farm). Cena papirusu (100 d) wzrosnie, jesli mieszczanie go kupuja (zuzycie 441 / dobe
  na starcie, `LOG` 08:20:12) - to dobrze: towar z daleka. Udzial pracy farmy wraca do zboza tylko w modelu BK (nieczynny), wiec zboza nie przybywa.
- Rozgrywka: drobna. Nowa kampania: nie. Ryzyko: male. Zaleznosc: kolejnosc z `MineralOnce` (ten sam postfiks - jedno miejsce, zgodnie z uwaga audytu lawy).

**C3. Las wsi wedlug klimatu.** [P1, mala]
- Co: `VillageWoodlot.PiecesPerDay` (`VillageWoodlot.cs:97-102`) mnozy stawke przez wspolczynnik kultury: pustynie (aserai, ghiscari, qartheen) 0.3,
  step Dothrakow 0.5, srodziemnomorskie (Lys, Tyrosh, Myr, Volantis, Valyria) 0.8, lesne (battania, river, vale, qohorik, ibbenese, nord, freefolk) 1.2,
  reszta 1.0 - i stala wyrownujaca tak, by swiat mial te same ok. 1 267 ladunkow dziennie.
- Uzasadnienie: kazda parafia miala las (opal, budulec) - w pasie umiarkowanym; pustynia nie. [P] drewno ma juz lekki deficyt (zapas 48 518, -150 / dobe
  pod koniec roku), wiec wyrownanie zamiast ciecia.
- Rozgrywka: drobna (drewno drozsze w Dorne i Qarth, tansze na Polnocy - wiecej handlu). Nowa kampania: nie. Ryzyko: male.

**C4. Za Murem bez rolnictwa.** [P1 razem z C1, mala]
- Co: 4 farmy zboza za Murem -> traperzy / rybacy (D.1), a "zboze 3" znika z listy wszystkich 18 wsi kultury freefolk (ten sam filtr co C2).
- Ryzyko: zywnosc warowni Wolnych Ludzi liczy sie z poziomu hearth (`CalculateDailyFoodProductionAmount`), nie z towaru - glodu warowni to nie zmieni;
  zmieni tylko towar zboza na targach za Murem (sa tam miasta? - Hardhome, Frostfang's Camp, Thenn: sprawdzic w logu "Rynek"). Lore: wolni ludzie
  zdobywaja zboze rabunkiem i wymiana - zgodne.

**C5. Watpliwe (36) - decyzja projektu, bez pytania Jeffa, ale osobnym krokiem.** [P2]
- Winnice w Dorzeczu (6): zostawic 4, zamienic Hag's Mire (bagno pod Przesmykiem) -> rybacy i Nunn's Deep (hearth 100) -> swinie; brak winnic w Reach
  poza Arbor jest luka lore, ale zamiana farmy zboza Reach na winnice obniza zboze "spichlerza swiata" - zostawic.
- Wielblady (rancza pustynne) w Lys, Tyrosh, Myr, Volantis (10 wsi): wielblad to zwierze Czerwonego Pustkowia i Qarthu; w Wolnych Miastach rancza
  "europejskie" (konie, muly) albo zostawic. Zmiana konia wsi rusza hodowle koni (paczki konne P1) - dopiero po pomiarze koni.
- Daktyle w Lys / Tyrosh (6) i Volantis (1): 2 ida pod bawelne (D.2), reszta zostaje (klimat Lys - Elche w Hiszpanii daje daktyle).
- Drwale na pustyni: Qarth 3 (2 pod bawelne), Zatoka Niewolnicza 1 (Ghozai -> gaj oliwny: lore ADWD o gajach pod Meereen - do sprawdzenia u zrodla,
  AWOIAF blokuje pobieranie), Dorne 1 (Red Cliffs pod Yronwood - Czerwone Gory maja lasy, zostawic).

### D. Tabela wsi do zmiany

**D.1 Zle (16)** - wspolrzedne x/y z `settlements.xml`, y rosnie na polnoc (Winterfell 845, Krolewska Przystan 397, Sunspear 104).

| Wies (id) | Kraina, strefa | Bound | Typ dzis | Dlaczego zle | Czym zastapic | Dlaczego to |
|---|---|---|---|---|---|---|
| Ornstead (`castle_village_B7_1`) | Nocna Straz, arktyka, y 1066 | Shadow Tower | bawelna | bawelna nie zniesie mrozu | owce | czarna welna plaszczy Strazy |
| Durlston (`castle_village_EN2_1`) | Dolina, gory, y 532 | Bloody Gate | bawelna | gory, chlodno | owce | gorskie pastwiska |
| Rushing Falls (`castle_village_EN5_1`) | Dorzecze | Acorn Hall | bawelna | klimat umiarkowany | bydlo | laki rzeczne |
| Samatha (`village_EN5_4`) | Braavos, y 629 | Braavos | bawelna | polnoc Essos, chlodno | len | plotno zaglowe Arsenalu |
| Metachia (`castle_village_ES2_2`) | Norvos, wzgorza | Ny Sar | bawelna | umiarkowany, wzgorza | len | jak audyt lawy |
| Ispantar (`village_K5_3`) | Sarnor, step | Kyth | bawelna | step umiarkowany | konie stepowe | rydwany Sarnoru |
| Karahan (`village_K6_1`) | Sarnor | Saath | bawelna | jw. | owce | step |
| Danara (`village_K6_3`) | Sarnor | Saath | bawelna | jw. | bydlo | step |
| King's Mountain (`ROT_castle17_village2`) | Kingswood | Fellwood | bawelna | las umiarkowany | drwale | Krolewski Las |
| Old Stonebridge (`village_EW5_3`) | Ziemie Korony | Duskendale | bawelna | umiarkowany | len | jak audyt lawy |
| Gelina (`ROT_castle49_village1`) | Lorath, zimno, y 651 | Anogar | winnica | winorosl nie dojrzeje | rybacy | wyspy, morze |
| Tarth (`ROT_town8_village1`) | Tarth, wyspa umiark. | Evenfall Hall | daktyle | palma chce > 2 000 jedn. ciepla | rybacy | wyspa |
| Crowgrave (`village_S6_2`) | za Murem, y 1219 | Thenn | zboze | rolnictwo niepraktyczne (AWOIAF) | traperzy | mysliwi |
| Storrold (`village_S7_2`) | za Murem, wybrzeze | Hardhome | zboze | jw. | rybacy | Hardhome nad morzem |
| Ghostcreek (`village_S7_3`) | za Murem, wybrzeze | Hardhome | zboze | jw. | rybacy | jw. |
| Frostbank (`village_S4_2`) | za Murem | Frostfang's Camp | zboze | jw. | traperzy | Mrozne Kly |

**D.2 Wyrownanie bawelny (5, z audytu lawy)** - Shirosi (`ROT_town35_village2`, Qarth, drwale w Czerwonym Pustkowiu -> bawelna),
Port Yhos (`ROT_town37_village2`, Qarth, drwale -> bawelna), Sagora (`village_ES4_1`, Volantis, delta Rhoyne, drwale -> bawelna),
Abar (`ROT_town11_village1`, Lys, daktyle -> bawelna), Tyrono (`ROT_castle47_village2`, Tyrosh, daktyle -> bawelna). Uwaga: Sagora nie wyszla
w mojej klasyfikacji jako watpliwa (drwale w strefie srodziemnomorskiej dopuszczalni) - zostaje, bo nawadniana delta to najlepsze miejsce bawelny.

**D.3 Watpliwe (36)** - pelna lista z id, bound, hearth, x/y: wynik `SCR7\wies-prod\krainy.py` (sekcja "WATPLIWE"): daktyle 8 (Lys 3, Tyrosh 3,
Volantis 1, Greenview w Ziemiach Burzy 1), rancza pustynne 9 (Lys 2, Myr 2, Tyrosh 2, Volantis 3), drwale na pustyni 5 (Qarth 3, Dorne 1, Ghis 1),
oliwki na granicy 2 (Rhaes Azor, Rooster's Top), winnice umiarkowane 11 (Dorzecze 6, Dolina 1, Norvos 1, Qohor 2, Smocza Skala 1), zboze Skagos 1
(Ravikayr -> owce / kozy, gdy Jeff nie chce rolnictwa na Skagos).

**D.4 Rozklad wedlug krain po C1 (liczba wsi typu)** - kontrola sensu (wybrane):
Polnoc 57: zboze 15, traperzy 7, drwale 7, garrony 6, bydlo 4, swinie 3, len 3, rybacy 3, owce 2, srebro 2, wieloryby 2, ruda 1, glina 1 - zgodne z
Szkocja/Skandynawia; Zelazne Wyspy 28: zboze 6, rybacy 6, ruda 4 - zgodne z lore (cienka gleba, olow, cyna, zelazo); Reach 40: zboze 12, oliwki 5 -
brak winnic poza Arbor (C5); Dorne 38: zboze 8, rybacy 7, oliwki 6, daktyle 4, konie pust. 4 - zgodne; Za Murem po C1/C4: traperzy 5, drwale 3,
rybacy 4, len, srebro, swinie, ruda, bydlo, konie - bez zboza.

### E. Czego nie mamy, a mogloby sie przydac (w temacie wsi; Jeff: "cos, na co nie wpadlismy")

1. **Odbudowa kosztuje towar.** Wracajacy do spalonej osady potrzebuja drewna (domy), ziarna siewnego i wolow - kupowanych na targu miasta przez
   pana albo wies (prawdziwy platnik, prawdziwy towar). Domyka "nic z niczego" w odbudowie i daje miastom popyt na drewno i zboze po wojnie. [P2, srednia, po A2]
2. **Stada jako lup.** Rabunek zabiera najpierw bydlo i owce (prawdziwe sztuki z magazynu i "stada" okregu), a hodowla okregu wraca w tempie przyrostu
   stada (owce ok. 20-30% rocznie, bydlo 10-15% [S]) - welna 7-8 lat po wojnie [H]. [P2]
3. **Mlyn na mapie jako kapital.** Wioska-mlyn (358 na mapie) spalona = okreg miele mniej: chleb BK i maka drozeja, zboze tanieje lokalnie
   (Norhamshire: mlyny 1/3 po 13 latach [H]). [P2, po A3]
4. **Dziesiecina dla septu / swiatyni.** 1/10 plonu okregu do kaplicy (wiara z PROJEKT-WIOSKI-LORE) - prawdziwe ujscie zboza z odbiorca (kaplani jedza,
   sprzedaja nadwyzke). [P2, do projektu obiegu]
5. **Ugor i "zmeczona ziemia".** Okreg przeludniony wzgledem ziemi (ludzie > ziemia) daje malejacy plon na glowe (Postan; prawo malejacych przychodow) -
   naturalny hamulec przyrostu bez sztucznych sufitow. [P2, po 109]
6. **Zniwa i przednowek** (B3) - ceny zboza rosna przed zniwami, spadaja po nich; daje sens handlowi spekulacyjnemu karawan. [P2, po PROJEKT-GLOD]

---

## 5. Do zrobienia tej nocy vs na pozniej

### Tej nocy (male, bezpieczne, do sprawdzenia autotestem 40 dob na nowej kampanii)

| Nr | Co | Gdzie dokladnie | Jak sprawdzic w autotescie 40 dob |
|---|---|---|---|
| N1 | Log: jaki model jest POD NavalDLC (`BaseModel`) i iloraz wynik / baza dla 10 typow wsi + rozklad poziomow hearth (0/1/2) | `Armoury\src\MoneyLedger.cs:850-855` (linia "Przeplywy osad: modele czynne") - dopisac nazwe `BaseModel` przez refleksje (`MBGameModel<T>.BaseModel`, protected getter); nowa linia dzienna w `GoodsLedger` albo raz przy starcie | w logu "produkcja wsi NavalDLC... (pod spodem DefaultVillageProductionCalculatorModel)"; ilorazy 1.0-1.35 |
| N2 | Tabela klimatu wsi C1 + C4 (16 zmian + 5 bawelny) z poprawka miast po `TradeBound` | `CrashScribe\src\Mends.cs:2117-2152` (slownik i warunek `:2133`, miasta `:2134, :2140`); wylacznik `NorthernFareEnabled` zostaje (opis w `Settings.cs:775` do poprawy) | linia "Mends: strawa Polnocy - ..." z 21 zmianami; "Towary" bawelna wsie ok. 70-85 zamiast 127-136; tlocznie/aksamit bez wsadu - linia "Warsztaty" |
| N3 | Papirus tylko na goracym poludniu (C2) i bez "zboza 3" za Murem (C4) | `Armoury\src\MineralOnce.cs:79-` (postfiks `GetProductions`, filtr na wejsciu, przed petla powtorzen); nowe ustawienie `CropClimateFilter` w `Settings.cs` + `python tools/gen_mcm.py` | "Towary: Papyrus ... wsie" ok. 18-20 zamiast 75-80; zboze z wsi freefolk 0 |
| N4 | Las wsi wedlug klimatu (C3), wyrownany do sumy | `Armoury\src\VillageWoodlot.cs:97-102` (`PiecesPerDay` dostaje wies albo kulture), wspolczynniki jako tabela w kodzie | "las wsi (126)" nadal ok. 1 250-1 300 / dobe; linia startowa wypisuje wspolczynniki |

Kazdy z N1-N4 osobno do recenzji (autor + recenzent), build kod 0, wpis CHANGELOG, wgranie tylko na "wgraj". N2 i N3 zmieniaja towar wsi -
wlaczyc je PO tym, jak paczka 169 (log obiegu) zbierze pomiar bazowy, inaczej pomiar 169 miesza dwie zmiany. N1 jest czystym logiem i moze isc razem z 169
(ten sam plik `MoneyLedger.cs` - uzgodnic, kto pisze linie 850).

### Na pozniej

| Nr | Co | Priorytet | Wielkosc | Czeka na |
|---|---|---|---|---|
| A1 | zamrozone progi hearth dla plonu | P1 | mala-srednia | pomiar 169; decyzja o kolejnosci z 109 |
| A2 | plon od rak z elastycznoscia 1.0 dla spustoszenia i opoznieniem kapitalu | P0 | duza | 108, 109, 113 w grze |
| A3 | wioski poboczne w rachunku (ludzie z ksiegi w dymku, spalona czesc zamiast Looted calego okregu) | P1 | srednia | A2, W3 |
| A4 | mieszanka okregu z wiosek (ryby z wiosek rybackich) | P2 | srednia | A3 |
| A5 | zywnosc warowni od rak | P1 | mala | razem z A2 |
| B1 | zyznosc krainy dla upraw (wyrownana) | P2 | srednia | PROJEKT-GLOD (bilans zywnosci krain) |
| B2 | zima wedlug klimatu (tropiki 0, poludnie 0.4) | P2 | mala | test z zima (rok albo wymuszona zima) |
| B3 | rytm zniw | P2 | srednia | spichlerz (PROJEKT-GLOD krok A) |
| C5 | watpliwe 36 wsi (winnice Dorzecza, wielblady, daktyle) | P2 | mala | pomiar koni (paczki konne), decyzja Jeffa o kanonie tylko dla Skagos/Ghozai |
| E1-E6 | odbudowa za towar, stada jako lup, mlyn, dziesiecina, ugor, przednowek | P2 | rozne | A2/A3, projekt obiegu 162-168 |
| doc | poprawic opis modelu w `AUDYT-SUROWCE.md:45` i audycie lawy rozdz. 1 (czynny jest model gry z progami, nie BKROT) | P1 | mala | - |

### Kolizje i zaleznosci z pracami rownoleglymi

- **169 (log obiegu):** N1 dotyka tego samego miejsca (`MoneyLedger.cs:850`) - uzgodnic; N2-N4 i A1 zmieniaja towar wsi, wiec pomiar 169 najpierw.
- **170 (domkniecie BetterEconomy):** zamyka druga produkcje wsi BEE (`TickSecondaryProduction`, 4 szt./dobe w `LOG`) - zgodne z tym raportem
  (to tez towar wsi "z niczego"); nic wspolnego w kodzie.
- **171 (zbrojenie zalog):** brak kolizji.
- **Projekt obiegu 162-168:** zaklada plon wsi staly 374 tys. dziennie (`PROJEKT-EKONOMIA-OBIEG-2026-10-08.md:472`) i podatek wsi 70% utargu (`:221`).
  Dzis plon rosnie 6-21% rocznie (L2) - A1 przywraca to zalozenie. A2/A3 sprawiaja, ze dochod panow z wsi spada po wojnie (realnie: "nie jest tak,
  ze wszyscy bankrutuja" - spadek dotyczy tylko spalonych czesci, reszta okregu placi dalej, a skarbiec krolestwa w wojnie zwraca polowe zoldu).
- **K13 (skala produkcji):** czynniki A1/A2/B1 mnoza wynik modelu PRZED krokiem K13 i PRZED czynnikiem rak 113 (kolejnosc z `113-ludzie-spustoszenie.md:106`:
  czynnik rak po mnozniku K13, przed `OreLedger.NoteModel`).
- **W2 / L0-L4 (wioski - wyglad i lore):** A3 zmienia tylko liczbe ludzi w dymku (`MapVillagesView.cs:4426`) - scalic z praca nad wygladem.
