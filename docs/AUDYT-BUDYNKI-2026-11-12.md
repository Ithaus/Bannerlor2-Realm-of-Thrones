# Audyt budynkow: koszt i czas wobec efektu w naszej ekonomii

To byl tylko odczyt: w repo i w grze nic nie zmienilem. Raport laczy trzy podaudyty (miasta, zamki, wsie). Kluczowe twierdzenia sprawdzilem drugi raz w kodzie. Mury (Fortifications) robi zespol 472, wiec tutaj sa tylko odnotowane.

**Twierdzenia sprawdzone w kodzie (plik:linia):**
- **Kopalnie, "+0.5 szybkosci budowy":** BK `Mines` i `Castle Mines` maja `ConstructionPerDay 0.5f, 1f, 1.5f` (n472dec/bk_all.cs:77369-77378).
- **Ten efekt nie dziala.** `BuildFunding.PowerPostfix` zastepuje cala moc budowy oplacona praca (n367c/Armoury/src/BuildFunding.cs:151-168). Do tej pracy dopisuje tylko Masona, a `Buildings338Mason.MasonPoints` liczy wylacznie typy `SettlementMason` i `CastleMason` (Buildings338Mason.cs:26-33).
- **AI wybiera budowe losowo:** `GetNextBuilding` = `...GetRandomElementInefficiently()` (DefaultBuildingScoreCalculationModel.cs:16-18). Nasz kod tego nie nadpisuje (grep = 0).
- **Craftmans Quarters jest martwy.**
  - Jego dochod liczy tylko `CalculateTownIncomeFromProjects` (DefaultClanFinanceModel.cs:459-467).
  - Latka BK `VillageIncomePrefix` (bk_all.cs:30465) tej metody nie wola, dopisuje tylko perk ArchitecturalCommisions.
  - Podatek zamku zastepuje `PopulationLaw.TownTaxPostfix` (PopulationLaw.cs:1242-1258).
  - Wniosek: nie ma ani zlota z niczego, ani zadnego efektu. Wczesniejszy AUDYT-338 (pkt 5, "DZIALA") tu sie mylil.
- **Retinue Barracks i Warhorse Studs sa liczone jako budowle cywilne** (`isMilitaryProject: false`, bk_all.cs:77380 i 77394). W wojnie cywilne stoja (BuildFunding.cs:245-249).
- **Kopalnie BK pracuja tylko w miastach:** `RunMines` jest wolane tylko w `OnTownDailyTick` (bkbb.cs:242-246).

---

## 1. Ile budynkow i ile bez sensu

Lacznie jest **ok. 50 typow budynkow z poziomami** (miasto 18, zamek 15, wies 17) oraz kilkanascie projektow dziennych.
- **Sensowne: ok. 20.** Koszary i biuro kasztelana w zamku, spichlerze, magazyn, akwedukt, drogi, urzad podatkowy miasta, Siege Workshop, Farmlands, stocznia, infrastruktura wsi.
- **Slabe: ok. 15.** Placa sporo za maly efekt.
- **Bez sensu albo martwe: ok. 12.** Zbudowane daja prawie nic albo zupelnie nic.

Najgorsze, z tym co dzis naprawde daja:

| Budynek | Koszt | Co obiecuje | Co naprawde daje |
|---|---|---|---|
| **Kopalnie BK w miescie** | 60/100/159 tys. zl, 0,8-2,2 roku na poziom | Rude oraz w dymku "+0.5/1/1.5 budowy" | Punkty budowy: **0**. Ruda: wszystkie kopalnie miast razem ok. 50-90 zl/d. Nie zwroci sie nigdy. |
| **Castle Mines** | 47/78/125 tys. zl, razem ok. **7 lat** | Jw. | Ok. **4 zl/d na zamek** (18 zamkow razem 8-9 rudy/d). |
| **Kopalnie "miejskie" w zamku** | Jw. | Jw. | **Zero**: urobek liczy sie tylko w ticku miast. AI wydaje na nie 3,4% punktow zamkow. |
| **Kopalnia wsi BK** | 9000 pkt pracy wsi, ok. 6 lat | Wiecej rud | Netto **0 zl**: ruda +163 zl, ale inne plony -163 zl. |
| **Craftmans Quarters** (zamek) | 11-20 tys. zl | Zloto od ognisk wsi | **0** (martwy, sprawdzone wyzej). |
| **Warhorse Studs** | 31/56/75 tys. zl | Konie bojowe | Ok. **1 kon na zamek na rok**, lezy w Stash bez odbiorcy. W miastach 0. |
| **Armory BK** (zamek) | 47-78 tys. zl | Trening zalogi | **+1 XP na kazdym poziomie** (tablica 1,1,1). Training Fields daje +3 XP za 1/3 ceny. Zjada 11% punktow zamkow. |
| **Court House BK** | 63-100 tys. zl, ok. 7 lat | Stabilnosc | **+0.02 stabilnosci na poziom**. Zjada najwiecej punktow ze wszystkich budow zamkow (15%). |
| **Port i Harbor w zamku** | 47-188 tys. zl za poziom, 8-12 lat | Handel i ryby | Handel w zamku martwy. Zaden zamek nie ma Portu. |
| **Tax Office wsi** | ok. 5 lat pracy wsi | Podatek | **0**: sami go wylaczylismy (PolicyIncomeConserved), a AI dalej go buduje. |
| **Fur Tannery** (wies) | 1-2 lata | Skora | **0 skory**: 8 wsi bez surowca. |
| **Projekty akrow wsi** (Farmland, Pasture, Woodland) | 0 | Wiecej ziemi | **Szkodza**: akrow BK nikt nie czyta, a wybranie projektu wylacza +15% Production. |

**Kaplice, swiatynie, septy** jako budynki **nie istnieja** w zadnym module: gra, BK, BKROTPatch, ROT, NavalDLC, Armoury. Najblizej sa BK Theater (lojalnosc i waga kultury pana) oraz Court House. Nasze "septy" (dziesiecina) i "roboty gminy" to sam obieg pieniedzy, bez budynku i bez efektu w grze. Do tego zjadaja material dwa razy szybciej niz budowy panow.

## 2. Dlaczego koszty nie maja sensu w naszej ekonomii (liczby z logu 12.11, rok = 364 dni)

- **Budzet panow jest maly.** Wszyscy panowie razem wydaja na budowy ok. **21 tys. zl/d na caly swiat**. To ok. **190 zl/d na osade** (10% dochodu rodu, jedna budowa na rod) i ok. 526 pkt/d.
- **Punkt kosztuje ok. 40 zl.** Cywilny ok. 31 zl, wojskowy ok. 63 zl.
- **Tempo jest wolne.** Miasto robi ok. 5-7 pkt/d, zamek ok. 3 pkt/d. W 55% dob zamek nie buduje wcale (brak pieniedzy, wojna albo brak materialu).
- **Budynek za 100 tys. zl** to ok. 530 dni calego budzetu budowy tej osady.
- **Koszty w punktach to wanilia i BK bez zmian.** Byly robione pod darmowa moc budowy, a nie pod oplacona prace.
  - Budynki BK w zamku kosztuja jak w miescie: 1000-6000 pkt.
  - Wanilowe budynki zamku kosztuja 280-1260 pkt, czyli 3-5 razy mniej.
  - Przy 3 pkt/d Castle Mines od 0 do 3 to 7,3 roku, a Harbor w zamku 11,9 roku.
- **AI wybiera losowo.** Przez to **48,7% punktow zamkow** (ok. 430 tys. zl w 59 dobach) poszlo na budynki slabe albo martwe. W tym czasie koszary i spichlerze czekaja w kolejce.
- **Wsie buduja za darmo:** 0 zl, 0 materialu, 1-12 lat (Manor 12 lat za ok. 0,3 wplywu na dobe).
- **Mason daje darmowe punkty.** To ok. 38% oplaconej mocy swiata, bez plac i materialu. Lamie to zasade "nic z niczego".
- **Klasa budowli jest pomylona.** Retinue Barracks, Studs i miejski Siege Workshop sa "cywilne", wiec w wojnie stoja i blokuja kolejke. 9 zamkow z Retinue stoi do 59 dob.

## 3. Plan naprawy: paczki, jedna naraz

Ramy czasu wzgledem 472: mury dostaja od zespolu 472 poziomy 1-2 / 3-4 / 6-8 lat. Pozostale budowle maja byc **wyraznie krotsze od murow**. Proponowane czasy, przy typowym tempie osady:
- tanie budynki zamku: 0,3-1 roku na poziom;
- budynki gospodarcze miasta i kopalnie: 1-2 lata na poziom;
- duze budowle miejskie (Harbor, Courthouse): 2-3 lata na poziom, nigdy dluzej niz mury tego samego poziomu.

**Paczka 1: sprzatanie martwych (male ryzyko)**
- **Budynki:** Mines miejski w zamkach, Tax Office wsi, Tannery w wsiach bez skor, Craftmans Quarters (do czasu paczki 6), projekty akrow wsi.
- **Zmiana:** usunac je z list budowy AI i zdjac `ConstructionPerDay` z obu typow kopalni, zeby dymek nie oszukiwal.
- **Ryzyko:**
  - Budynki juz zbudowane w zapisach musza zostac i nie moga sie wywrocic.
  - Typow z gry nie kasowac, tylko wylaczyc je z wyboru.
  - Sprawdzic, skad zamki maja typy miejskie BK. To nadal niepotwierdzone.

**Paczka 2: klasa wojskowa (male ryzyko)**
- **Budynki:** Retinue Barracks, Warhorse Studs, Siege Workshop w miescie.
- **Zmiana:** oznaczyc je jako budowle wojskowe. Punkt bedzie 2x drozszy, ale budowa nie stanie w wojnie.
- **Koszt w punktach:** Retinue x0.5 (500/1000/1500), zeby cena w zlocie zostala podobna.
- **Ryzyko:** wiecej budow w wojnie zabiera pieniadze z zoldu. Sprawdzic, czy WarFront i pulap zoldu to wytrzymaja.

**Paczka 3: koszty BK do skali zamku i wsi (srednie ryzyko)**
- **Budynki zamku:** BK x0.3, czyli ok. 300-1200 pkt. Castle Mines 0->3 skraca sie z 7 do ok. 2 lat.
- **Budynki wsi:** Manor x0.4, Palisade x0.5, Training Grounds i Arms Warehouse x0.5.
- **Usunac z zamkow:** Harbor, Court House BK. Court House scalic z wanilowym Courthouse.
- **Ryzyko:** kolejki przyspiesza, a przez to rosnie popyt na material, ktorego juz brakuje (38-42 budowy stoja). Paczke 3 wdrazac **po** paczce 5 albo razem z kontrola materialow.

**Paczka 4: Mason bez darmowych punktow (srednie ryzyko)**
- **Nowy efekt:** -10/-20/-30% zl za punkt (cech murarzy), czyli szybciej za te same pieniadze. Naprawa murow zostaje bez zmian.
- **Ryzyko:** budowy na swiecie zwolnia o ok. 30-40% oplaconej mocy, bo darmowy dopisek zniknie. To kolizja z 472: mury tez zwolnia. Trzeba to policzyc razem z zespolem 472, zeby ich lata sie zgadzaly.

**Paczka 5: AI wybiera budowe wedlug wartosci (najwieksza dzwignia, wieksze ryzyko)**
- **Zmiana:** ocena kazdego budynku wedlug zwrotu w zl/d i potrzeb osady:
  - glod, wtedy Granary, Farmlands, Waterworks;
  - drogi zold, wtedy Barracks i Castellan's Office;
  - front, wtedy budowle wojskowe i mury wedlug 472;
  - brak narzedzi, wtedy Smith we wsi.
- **Projekt dzienny,** gdy zadna budowa sie nie oplaca.
- **Ryzyko:**
  - Latka na model gry: sprawdzic w dekompilacji BK i SCAI.
  - Wydajnosc: ocena tylko przy wyborze, nie co tick.
  - Po 472: nie wpychac murow 2->3 bez zagrozenia.

**Paczka 6: nowe efekty z rak ludnosci (duze, po pomiarach)**
- **Kopalnie:** gornicy z ludnosci okregu z realna placa, urobek wedlug zloza. Gdzie zloza brak, budynku nie ma.
- **Stadnina:** zrebieta z pastwisk, konie na rynek albo do zalogi.
- **Port:** rybacy, ryby do zapasu i na rynek.
- **Craftmans Quarters:** rzemieslnicy przerabiaja surowiec wsi na towar na targ.
- **"Sept/Swiatynia":** przerobiony Theater, czyli waga wiary pana w `CalculateReligionWeight` i mniej napiecia religijnego. Placony z dziesieciny zamiast niewidzialnych septow. Roboty gminy finansuja budynki BK wsi.
- **Ryzyko:** zamknieta ekonomia (kazde zloto i kazda sztuka ma platnika) i kolizja z lancuchem podazy 435-L*.
- **Przed ta paczka potrzebne pomiary:** linia logu budow wsi, urobek kopalni miast i Smith (narzedzia).

## 4. Pytania do Jeffa

1. Martwe budynki: usuwamy je z gry (AI ich nie buduje, zbudowane zostaja) czy dajemy im nowe efekty z paczki 6? Ja proponuje najpierw usunac, a nowe efekty dodawac pozniej.
2. Mason: zamieniamy darmowe punkty na tanszy punkt? Wszystkie budowy, takze mury z 472, zwolnia wtedy o ok. 1/3.
3. Farmlands i premia BK +50% to jedzenie z niczego. Zostaje, bo bez niego bylby glod, czy przechodzi na prace chlopow (KS)?
4. Czy chcesz budynek wiary (Sept/Swiatynie) z prawdziwym efektem na wiare BK, finansowany z dziesieciny?
5. Barracks i Castellan's Office razem obnizaja zold zalogi do -60%. Dajemy sufit, np. -40%?

**Rekomendacja:** najpierw paczka 1 i 2 (male ryzyko, od razu przestaje sie marnowac zloto na martwe budynki), potem paczka 5 (AI wedlug wartosci). Koszty z paczek 3-4 dopiero po uzgodnieniu tempa z zespolem 472.

**Pliki:**
- Kod: C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n367c/Armoury/src/BuildFunding.cs, C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n367c/Armoury/src/Buildings338Mason.cs, C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n367c/Armoury/src/PopulationLaw.cs
- Dekompilacje: C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n472dec/bk_all.cs, C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n472dec/bkbb.cs, C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/decout/v/all_cs/TaleWorlds.CampaignSystem.GameComponents/DefaultBuildingScoreCalculationModel.cs, C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/decout/v/all_cs/TaleWorlds.CampaignSystem.GameComponents/DefaultClanFinanceModel.cs
- Log: C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/Armoury-2026-11-12_22-47-01.log oraz C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/Logs/2026-11-12_22-47-01/budowy.log