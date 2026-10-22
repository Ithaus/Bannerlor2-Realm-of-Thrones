## 2026-10-22 (m105-miasta: gospodarka miast, skarbiec korony na glod, drogi Valyrii, wozy wsi - projekt m105-miasta, plan docs/PLAN-GOSPODARKA-2026-10-21.md)

**Mod:** Armoury (+ `tools/sprawdz_logi.py`, `tools/m105-miasta/test_m5m_syntetyczny.py`) | **Galaz:** `w-toku/m105-miasta` (od `77c17f3c` = n32 = WGRANIE 102) | **Status:** DO SPRAWDZENIA (nic nie wgrane, gry nie uruchamiano; autotest - plan na koncu)

Cztery zmiany w grze, kazda z wlasnym kluczem MCM (domyslnie TAK; NIE = kod jak w WGRANIU 102), jedenascie poprawek narzedzia progow i nowa grupa `m105miasta`.
Granica z galezia `w-toku/m105-gospodarka4` (wsie: koszyk ZC4, kiesy wsi, danina wsi, narzedzia wsi, zaleglosci zamkow) zachowana - zadna z tych metod nie ruszona.

| Commit | Co |
|---|---|
| `7e9cf1e4` | G1 `DsFarmCutBelowReserve` - szybka obnizka firmy miasta ponizej zapasu kupcow |
| `2affdfe9` | G2 `CrownHoardFeedsHungry` - skarbiec korony ponad zapas wojny karmi glodne miasta (chleb M3 i rachunki KS-H) |
| `660907ce` | G3 `MapRoadBonusAboveFloor` - premia drogi od predkosci po podlodze gry |
| `1a7a6b9e` | G4 `VillageCartGoldAware` - wozy wsi rezerwuja zloto kasy miasta celu (P8) |
| `6ce383e7` | T1-T11 - poprawki narzedzia progow |
| `08624233` | grupa progow `m105miasta` (M5M-S, M5M-0..M5M-9) |
| `aa33e728` | G2 recenzja wlasna: migawka skarbca przesunieta o splate rachunkow ze skarbca (CrownIncome.ShiftSnapshot) |
| `f40e632b` | CHANGELOG galezi + test syntetyczny grupy m105miasta |
| `8b47a3c4` | poprawki po recenzjach (ekonomia R1-R8, kod R1-R6) - sekcja "Poprawki po recenzjach" nizej |

Odstepstwo od kolejnosci projektu (T przed G): commity G poszly pierwsze - na tym komputerze dysk C zeruje pliki w miejscu, wiec kazda zmiana gry szla do repo od razu. Pliki rozlaczne, kazdy commit buduje sie sam (build po kazdym, kod 0).

gen_mcm: 2276 -> 2279 ustawien (4 nowe klucze). Build Armoury: kod 0, 0 bledow (7 ostrzezen jak dotad). `sprawdz_narzedzie.py tools/sprawdz_logi.py`: dispatch, wstawki, def, grupy, kompilacja - OK.
Nowe klucze nie istnieja w `Armoury.json` Jeffa - dzialaja domyslne z kodu (TAK).

---

### G1. Szybka obnizka firmy miasta ponizej zapasu kupcow (klucz `DsFarmCutBelowReserve`, wymaga `KsFarmSurplusCapNet`)

**Problem (log 102 `Armoury-2026-10-21_14-29-20.log`):** NAKUM-27 "12 -> 18 miast" z KS ponizej zapasu kupcow (prog <= 14), NKA-3 "na koniec 16" (prog <= max(12, 14)); szacunek projektu (`m105/g1_szybka.py` na kasy-miast.csv): 102 d229 17 -> 11 miast ponizej zapasu, n31 24 -> 17, bieg 160 z zapisu 13 -> 6. Narzedzie m105miasta na kasy-miast.csv 102: M5M-1 (>= 28 dob z rzedu ponizej zapasu z poborem ponad doplyw) na koniec 4, M5M-2 (pobor ponad doplyw w dobach ponizej zapasu, 56 dob) 2.10% poboru (n31 7 / 2.38%, 160 1 / 1.21%).

**Przyczyna:** `DsTowns.SurplusReview` (galaz `gq <= OverReserve x Reserve || remit || arrears`) - przy kasie ponizej 1.5 x zapasu jedyna droga w dol to obnizka z dochodu (e) `IncomeCut`: raz na Ks Farm Surplus Review Days (14) o Ks Farm Surplus Review Max (25%). Miasto, ktoremu kupcom skonczyl sie zapas obrotowy, placi przez te tygodnie firme ponad doplyw i popada w zaleglosci.

**Zmiana:** `DsTowns.cs` - nowa metoda `FastCut` przed `IncomeCut`, tylko przy `capNet`: gdy `remit == 0` i (kasa po naleznosciach `gq` < `TownPurse.Reserve(t)` albo zaleglosci firmy / dlug rady) - firma pana i korony z kasy (FL, FC) ponad `max(0, NetAvg) x 364` schodzi do niej od razu, ten sam stosunek pan / korona, czesc z oszczednosci ludu (FLf) bez zmiany; `Share` najwyzej 1; `SLow = today` (cykl obnizki (e) od tej doby); nowe pole sesyjne `Farm.FastDay` - najwyzej raz na 7 dob (`FastGapDays`). Bez PELNEGO pomiaru doplywu (NaN, < 28 dob - `NetDays`; po recenzji R1, dotad 7 dob) - nic: pamiec doplywu (`Net`, `NF`, `FastDay`) jest tylko w sesji, wiec po wczytaniu zapisu szybka obnizka rusza od 28. doby (wczesniej obnizka (e) jak dotad); w nowej kampanii pamiec jest pelna przed koncem rampy DS (d85). Gdy szybka obnizka nic nie zmienila (firma juz przy doplywie, odstep 7 dob) - obnizka (e) jak dotad. Miedzy 1.0 a 1.5 zapasu zostaje (e). Zloto sie nie rusza - zmienia sie nalezna rata.

**Linia logu:** w "Dochod ludu wedlug koszyka (f5-lud): dzien N" za "| P3 sufit firm ..." segment `| m105 szybka obnizka (Ds Farm Cut Below Reserve TAK): dzis N (-X zl/rok), od startu sesji N (-X zl/rok), miast z KS ponizej zapasu M, z firma ponad doplyw K` (spis `DsTowns.FastCensus` - miasta z KS z firma, kasa po naleznosciach < zapas; z nich firma > doplyw x 364). Regexy NLU_RX i M3P "seg" bez kotwicy na koncu - bez zmian.

**Slowami gracza:** "Miasto, ktoremu kupcom skonczyl sie zapas obrotowy, od razu placi panu tyle, ile naprawde zarabia - nie popada w zaleglosci, czekajac dwa tygodnie na obnizke."

---

### G2. Skarbiec korony ponad zapas wojny karmi glodne miasta (klucz `CrownHoardFeedsHungry`)

**Problem (log 102, ostatnie 28 dob):** chleb biedoty M3 "nie stac nikogo" 22.3 tys./d; Qarth: rachunki zywnosci KS-H starsze niz 30 dni (NKA-4: 11 dob, najstarszy 35 dni, blokada dowozu od d219) przy skarbcu 3.97 mln (nadwyzka ponad zapas wojny 3.47 mln). Szacunek projektu (`m105/g2_korona_glod.py`): pokryte 67% chleba (reszta - Pentos, nadwyzka 70 tys.), Qarth splacony, NKA-4 -> 0; bieg 160: chleb 100%.

**Przyczyna:** chleb M3 placila korona tylko z reszty wplywow dnia, potem pan (tempo skrzyni), rachunki KS-H tylko pan (`KsLordFood`); zapas skarbca ponad zapas wojny ("zostaje w skarbcu - zapas na wojny i trudne czasy", Jeff przy f7) nie byl uzywany na glod nigdy.

**Zmiana:**
- `KingdomTreasury.CrownSurplus.cs`: `CsDays` internal; `HoardRoom(k)` - jeden limit dzienny na krolestwo = floor(`TreasurySurplusFor(k)` / CsDays) z pierwszego uzycia doby (ta sama miara nadwyzki co W5b: skarbiec - reszta wplywow dnia - zapas wojny - dlug w Banku - dlug u miast), kazda wyplata najwyzej biezaca nadwyzka; krolestwo gracza (RulingClan = gracz) - 0. `HoardUse`, `HoardBelow` (kontrola: skarbiec po wyplacie ponizej zapasu wojny + reszty wplywow + dlugow). Reset w `CrownSurplusReset`.
- (a) `TownFolk.Bread.cs` `BreadRest`: po koronie z wplywow dnia i po panie reszta luki osady - ze skarbca krolestwa wlasciciela (`HoardRoom`), skarbiec -> kiesy ludu (`BreadCredit`, kontrola zamknieta kies `_ctlIn`). Kwota w `_brCrown` (platnik to skarbiec; "Obieg": "chleb biedoty (M3) skarbce -> kiesy ludu" = `LastBreadCrown`), bez `CrownIncome.Spent` i bez `KDay.Bread` (to nie wplywy dnia - jak W5b `CrownAid`).
- (b) `KsLordFood.cs` `HoardBills` - w `BeforeEarly` (prog wczesnej splaty 20 dni) i w `Before` tryb RACHUNKI (prog = zwloka 30 dni) po kroku pana, takze gdy pan nie mogl (bez pana, bez budzetu, bez miejsca); nie dla oblezonego; miasto gracza-wasala TAK (po recenzji R8c - jedna regula z chlebem: placi korona krolestwa AI, gracz nigdy z kiesy; krolestwo gracza-wladcy - HoardRoom 0). Warunek: kasa bez niczego ponad podloge kredytu (`KsFoodCredit.FreeOf <= 0` - PayDaily splaca z tej samej granicy) i rachunki starsze niz prog > 0. Skarbiec -> kasy zrodel (`PayByLordAge`, najstarsze najpierw, z pomiarem), nieprzyjete wraca do skarbca, `TownPurse.NoteLordFood` jak przy panu. Po recenzji wlasnej (`aa33e728`): `CrownIncome.ShiftSnapshot(k, -took)` - KsLordFood biegnie po `CrownIncome.End`, bez tego jutrzejsze "inne wplywy" korony liczylyby wyplate z zapasu jako wydatek spoza nas i renty dnia malalyby drugi raz.
- Kolejnosc platnikow: wplywy dnia -> pan -> skarbiec. Zloto: skarbiec -> kiesy ludu albo kasy miast zrodel; nic z niczego.

**Linie logu:**
- "Chleb biedoty (M3)": w nawiasie korony dopisek `; w tym ze skarbca ponad zapas wojny X` (regex R2_M3 `[^)]*`), na koncu segment `| m105 skarbiec korony (Crown Hoard Feeds Hungry TAK): ze skarbca X zl (krolestw N, osad M), nie stac nikogo po skarbcu Y zl (w tym przy wolnym limicie skarbca Z zl - oczekiwane 0), kontrola skarbiec -T + kiesy ludu X = 0, ponizej zapasu wojny po wyplacie 0, limit doby 1/30 ...`. Pole "nie stac nikogo" w starej czesci linii to teraz luka PO skarbcu.
- "Pan miasta placi za dowoz (KS)": przed "| czas" segment `| m105 skarbiec korony: rachunki splacone ze skarbca X zl (miast N, krolestw K, kandydatow po panie bez kasy ponad podloge C), kontrola skarbiec -T + wierzyciele X = 0, ponizej zapasu wojny po wyplacie 0`. Linia KS-H liczy te splaty w "splacone przez panow miast" (PayByLordAge; kontrola zlota KS-H dalej 0 - GLordPaid i GSrcGot rosna razem); linia pana ma je tylko w segmencie m105 (nie w `_billsPaid`) - narzedzie NGL-6f (grupa nglod) dolicza segment do linii pana (po recenzji R2; bez tego kazda doba ze splata ze skarbca dalaby falszywe NIE).

**Slowami gracza:** "Krol, ktory ma pelny skarbiec, nie patrzy, jak jego miasto gloduje. Oplaca chleb biedoty i zalegle rachunki za zboze, ale nigdy nie siega do pieniedzy odlozonych na wojne."

---

### G3. Premia drogi od predkosci po podlodze gry (klucz `MapRoadBonusAboveFloor`, wymaga `MapRoadSpeedBonus`)

**Problem:** VR-3 ze wszystkimi probkami 24.98% (102), 24.26% (160 z zapisu); probki "na podlodze predkosci gry" (wynik bez premii na LimitMin) dostawaly premie, ktora podloga zjadala (VR-3b: 15.33% przy 1.000 / 1.153).

**Przyczyna:** `MapRoads.SpeedRoad`: dodatek `res x pct` do wyniku BEZ podlogi (podstawa x czynniki); gdy ten wynik lezy ponizej `LimitMin`, wynik z premia = max(u + dodatek, LimitMin) - premia ginie w calosci albo w czesci.

**Zmiana:** `MapRoads.cs` `SpeedRoad`: `u = BaseNumber x (1 + SumOfFactors)`; dodatek = max(`res x pct`, `max(res, LimitMinValue) x (1 + pct) - u`). Partia ponad podloga - bez zmiany (u = res); sufit kolumny (LimitMax) - bez zmiany (dla u > LimitMax drugi czlon jest mniejszy). Kara snu (NightRest) dalej po premii. Licznik `MapRoads._floorBonus` (Interlocked - watki robocze).

**Linia logu:** "Drogi Valyrii (VR): dzien N" - za kazdym audytem `, podloga z premia (m105) N` (probki z podlogi z pelna premia), za audytami `, liczenia z premia od podlogi gry (m105, wszystkie drogi) ok. N`. Regexy M96VALYRIA_DAY / M96VALYRIA_AUD / M103_VALYRIA_LOW bez zmian (sprawdzone self-testem grupy m105miasta).

**Slowami gracza:** "Nawet najwolniejszy patrol jedzie droga Valyrii szybciej niz obok niej."

---

### G4. Wozy wsi rezerwuja zloto kasy miasta celu (klucz `VillageCartGoldAware`, wymaga `VillageCartRoadNews`; P8)

**Problem:** A1-T19 towar wsi niesprzedany w miastach 0.148% (prog < 0.1%); skok z rampy KS (d31-60 0.526%, d91-230 0.013%), Highgarden -81.7 tys. w jedna dobe (`sur/unsold.py`).

**Przyczyna:** wiesc z drogi doliczala do polki celu towar innych wozow, ale nie ZLOTO kasy: wiele wozow wycenialo ten sam pelny skarbczyk miasta i wracalo z towarem, za ktory kupcy nie mieli czym zaplacic.

**Zmiana:** `MarketCarts.cs`: `Haul.Gold` = wycena kursu przy wyborze (bestVal), `_gold[Town]` = suma rezerwacji wozow w drodze; `Note` dodaje, `Forget` odejmuje (wjazd do miasta / zamku - SellPrefix, CastleMarket; nowy wybor - Forget na poczatku Choose zdejmuje wlasna rezerwacje; porzadki dobowe - woz nieczynny, skrecil, 30 dob). `Value` - przeciazenie z `reserved` (kasa = Gold - rezerwacje innych wozow) i znacznikiem `capped`; stare przeciazenie bez zmian. Choose: wycena kandydatow, miasta targowego wlokna (FibreToMarket) i porownanie z wlasnym miastem - z rezerwacjami. Gorna granica (`Upper`) bez limitu kasy - odciecie jak dotad. Licznik "wybor zmieniony": wybor bez rezerwacji (druga wycena tylko, gdy kasa po rezerwacjach ograniczyla sprzedaz).

**Linia logu:** "Dowoz (wozy): dzien N" przed "| woz x" - `| m105 zloto zarezerwowane (Village Cart Gold Aware TAK): wozow z rezerwa N, zarezerwowane X zl u M miast, wybor zmieniony K, wygasle J`. Zadne wyrazenie narzedzia nie czytalo tej linii po polach.

**Wplyw na A1-T19: NIEZNANY (recenzja R6).** Prog zalezy od pojedynczych zdarzen: w 102 doby 108708-108709 (rampa KS, doby 60-61) to 3734 z 5163 szt. (Highgarden: kasa 112 835 -> 27 288, doplyw -77 719), w n31 jedna wizyta w d108858 to 5111 z 7064 szt. `Value` widzi kase minus rezerwacje w chwili wyboru (MarketCarts.cs:238), nie widzi spadku kasy po wyborze (pobor firmy, wydatki kupcow) - G4 nie musi tych zdarzen zlapac. W Biegu A czytac razem: A1-T19, "wybor zmieniony" z segmentu G4, M5M-8 i GL2-6.

**Slowami gracza:** "Woznice wiedza, ze do miasta jada juz inne wozy, i nie wioza towaru tam, gdzie kupcom zabraknie pieniedzy."

---

### T1-T11. Narzedzie progow (`tools/sprawdz_logi.py`, commit `6ce383e7`) - gra bez zmian, stara miara zostaje jako INFO

| Prog | Zmiana | 102 (14-29-20) przed -> po | n31 (12-48-45) | 160 z zapisu (13-37-13) |
|---|---|---|---|---|
| T1 NAKUM-7 / NAKUM-7p | sakiewki ludzi poza bramka +30 tys./d (pomiar w wierszu); NAKUM-7p z "Markietani (E5/A3)": <= 15 dni zoldu kazdej doby okna i koniec <= poczatek + 1 dzien; NAKUM-7s bez zmian (bramka tylko skarbce) | NIE -> TAK; 7p TAK 8.0 -> 7.9 | NIE -> TAK; 7p TAK 5.9 -> 6.9 | NIE -> NIE (glowy rodow +65.5 tys./d - poza zakresem); 7p TAK 8.4 -> 9.0 |
| T2 G1-5a / G1-5a2 | G1-5a (Z33 sprzed KS/f4/f5/f7) -> INFO; G1-5a2: kasy / (1.5 x zapas + 28 dob poboru) <= 1.10 w dobie 120 pliku i na koncu (kasy-miast.csv z katalogu sesji); po recenzji R7 G1-5a2b - dolna strona i trend jako INFO (102 0.94 -> 0.70, -0.216 na 100 dob; n31 0.91 -> 0.65) | NIE -> INFO; 5a2 TAK 0.94 / 0.70 | NIE -> INFO; TAK 0.91 / 0.65 | brak wierszy G1-5 (etap8 z zapisu) |
| T3 G1-5c | -> INFO | NIE -> INFO | NIE -> INFO | - |
| T4 A16-5b / A16-5b2 | A16-5b -> INFO; A16-5b2: ostatnie 56 dob >= -15 tys./d (wiazace, gdy okno zaczyna sie po d37) | NIE -> INFO; 5b2 TAK -13.7 | NIE -> INFO; TAK -10.4 | TAK -> INFO; TAK -11.5 |
| T5 C-4c / C-4c3 / C-4c2 | C-4c (jedna doba) -> INFO; C-4c3 (budzet-rodow.csv, od doby 120 pliku): rody AI (po recenzji R4 bez wiersza `rodzaj=gracz`) z zapasem G < 28 dni przy tempie 28 dob <= 2% i bankrutow 0; C-4c2 INFO | NIE -> INFO; 4c3 TAK 1.6% max (9 z 569, d202), 0 | NIE -> INFO; TAK 1.2% (7 z 595) | TAK 1.6% (9 z 577; C-4c2 10.2% INFO) |
| T6 R1-10b | prog 177 -> 60/d | TAK 17/d | TAK 19/d | - |
| T7 KR2-1 | regex `krolestw.*?\|` (bez spacji po "krolestw;") | NIE -> TAK (230/230), KR2-3/4 licza sie: TAK | NIE -> TAK | NIE -> TAK (161/161) |
| T8 NAKUM-2c | w dobie przejscia na F3 skok liczony od 'pobrano' linii kiesa2 doby przed (zmiana pola, nie poboru) | NIE +82542 -> TAK +13901 | NIE +82125 -> TAK +12263 | TAK -> TAK +4849 |
| T9 A3-1 | regex z wstawka `| E3-G2 danina pana ...` | NIE -> TAK (A3-2..A3-7 licza sie: TAK / INFO) | NIE -> TAK | INFO (brak dob okna) |
| T10 NWAR-15 | poczatek = pierwsza doba linii + 14 | NIE -> TAK (0.90 / 0.88 -> 1.28 / 0.82) | NIE -> NIE (0.96 / 0.88 -> 1.31 / 0.61) | **TAK -> NIE** (0.99 / 1.00 -> 1.27 / 0.79; dawny start d1 0.46 / 0.52 byl artefaktem po wczytaniu - prawdziwy spadek klina plotno/len, poza zakresem m105-miasta) |
| T11 P1-4 (kiesa1), R2-T2-2 (rok2) | INFO, gdy linia DS pokazuje "P1 ... x 0.000" (P1 pod DS wylaczona z zalozenia); po recenzji (kod R4): pierwsza taka doba i wyciszenie tylko, gdy lezy najpozniej w pierwszej dobie okna (P1-4: <= min(okno), R2-T2-2: doba biegu <= 91) - dzis doba biegu 84 (102, n31) / 85 (160), werdykty bez zmian | NIE -> INFO / NIE -> INFO | NIE -> INFO / NIE -> INFO | - / INFO |

Kontrola regresji narzedzia: wszystkie 156 grup na logu 102 przed i po (run_all.py) - zmienione werdykty tylko w wierszach z tabeli i w wierszach, ktore dzieki T7 / T9 zaczely sie liczyc (wszystkie TAK albo INFO). Pierwsza wersja T1 wlaczala bramke NAKUM-7s przez sakiewki (INFO -> TAK) - poprawione przed commitem (bramka NAKUM-7s tylko od skarbcow).

### Grupa `m105miasta` (commit `08624233`)

`python -I tools/sprawdz_logi.py --grupa m105miasta <Armoury-*.log> [--tabele <katalog z kasy-miast.csv>]`. Stale `M5M_*`, prefiks `M5M-` (wolne nazwy).

| Wiersz | Co | Prog |
|---|---|---|
| M5M-S | self-test wyrazen segmentow na liniach kontrolnych zlozonych jak w kodzie C# | OK |
| M5M-0 | segmenty m105 w kazdej dobie z linia bazowa (f5-lud, Chleb M3, Pan miasta placi, Drogi Valyrii, Dowoz), klucze TAK | brakow 0; log bez segmentow - INFO |
| M5M-1 | miasta >= 28 dob z rzedu ponizej zapasu kupcow z poborem 28 dob ponad doplyw (kasy-miast.csv, najwiecej w ostatnich 56 dobach) | <= 2 (102 bez G1: koniec 4) |
| M5M-2 | pobor ponad doplyw w dobach ponizej zapasu (56 dob) / pobor | <= 1% (102: 2.10%) |
| M5M-3 | (straznik G1) srednia d202-229 pliku zawor_do_pana + do_korony miast | >= 0.9 x 102 = 0.9 x 315 136 zl/d; bieg krotszy - INFO |
| M5M-3b | (po recenzji R3) ta sama miara w d86-120 pliku - pierwsze okno po rampie DS, w ktorym G1 tnie | INFO wobec 102 106 788 zl/d (n31 113 717) |
| M5M-4 | rachunki KS-H starsze niz zwloka kazdej doby / starsze niz prog wczesnej splaty na koniec | 0 dob / <= 10 tys. (102: 11 dob, 56 tys.) |
| M5M-5 | SAMOKONTROLA kodu G2 (po recenzji R7 / kod R2): luka chleba przy wolnym limicie skarbca - z konstrukcji 0 (x = min(luka, limit), potem luka 0 albo HoardRoom 0); NIE tylko przy bledzie HoardRoom / HoardUse, to nie jest miara pokrycia chleba | 0 zl |
| M5M-5b | (po recenzji) chleb ze skarbca / "nie stac nikogo" po skarbcu od startu i % pokrycia luki po panie | INFO (szacunek projektu 67% w 102 - reszta Pentos) |
| M5M-6 | skarbiec ponizej zapasu wojny po wyplacie / kontrola skarbiec - odbiorcy / koszt skarbcow (sr. 28 dob) | 0 / 0 / <= 40 tys./d |
| M5M-7 | VR-3 ze wszystkimi probkami audytu Valyrii (wazona liczba probek) | >= 24.9% |
| M5M-8 | rezerwacje wozow wygasle przy porzadkach / zalozone | <= 1% (dzis wiesci wygasaja w 0.04% wyborow) |
| M5M-9 / 9b | ERROR nowego kodu (po recenzji kod R3: cala klasa "ERROR in MarketCarts." - blad Note / Forget / Value loguje sie nazwa wolajacego: Daily, SellPrefix; wyjatek w MapRoads.SpeedRoad to licznik "w predkosci" - pilnuje NDR-8, grupa ndrogi) / czas linii KsLordFood i f5 | 0 / INFO |

Odstepstwa od projektu: M5M-3 - baza projektu 267.9 tys./d nie dala sie odtworzyc z logu ani CSV; prog liczony ta sama miara co wiersz (kasy-miast.csv 102, d202-229: 315 136 zl/d). M5M-9 czas - INFO (nowy kod nie ma osobnego stopera; HoardBills jest w czasie linii KsLordFood). Na starych logach (102, n31, 160): M5M-S TAK, M5M-0 INFO "brak linii m105", M5M-9 TAK - bez wyjatku.

Test syntetyczny: `python -I tools/m105-miasta/test_m5m_syntetyczny.py` - bieg "dobry" (wszystkie M5M TAK, 3b / 5b / 9b INFO) i "zly" (wszystkie NIE: brak segmentu w dobie, 3 miasta ponizej zapasu z poborem ponad doplyw, M5M-3 66.6%, rachunek starszy niz zwloka, luka chleba przy wolnym limicie, kontrola != 0, VR 24.0%, 10% wygaslych, ERROR in MarketCarts.Daily) - zgodne; po recenzji czesc 2 - NGL-6f (grupa nglod): pan + skarbiec = KS-H TAK, log bez segmentu m105 TAK, KS-H ze splata skarbca przy linii pana bez segmentu NIE - zgodne.

---

### Ryzyko / co sprawdzic (zasada 0 - kontrola calosci)

**Regresje (kto czyta / pisze ten sam stan):**
- G1: `Farm.FL / FC / Share / SLow` pisza tez Review/Renew (przeglad zwykly - cel Share x doplyw x 364, Share <= 1 w miastach z KS - nie podniesie firmy nad doplyw), SurplusReview (podwyzka tylko przy kasie > 1.5 x zapas), FromZero (firma 0 - wroci od zera przy kasie > 1.5 x zapas), IncomeCut (cykl od `SLow`). Szybka obnizka nie dziala w dzien przegladu zwyklego (`_renewN > 0` - jak podwyzki). Firma 0 przy doplywie <= 0 - z projektu; FLf (z oszczednosci ludu) nietkniete. Dochod panow z firm spada wczesniej. Szacunek poprawiony po recenzji R3 (dawne liczby "d92-120 ok. -14%, d229 -14.2 tys./d = 3.5%" byly z `m105/g1_szybka.py` z oknem d65-120, ktore liczylo tez rampe DS - kod rampe pomija, `inRamp -> continue`); wedlug kodu (`rec_g1b.py`, od d86): 102 d120 -9.5 tys./d (4.9%), d140 -11.8 tys./d, d229 -19.9 tys./d; n31 d229 -28.1 tys./d; 160 z zapisu d160 -50.9 tys./d; miast z firma scieta do 0: 19 / 23 / 28 (102 d120 / d140 / d229), 29 (n31), 12 (160). Straznicy: M5M-3 (d202-229, wiazacy), M5M-3b (d86-120, INFO), C-4c3; mniej pozyczek m96 (M96D-3 ma zostac TAK). Po wczytaniu zapisu (recenzja R1): dotad szybka obnizka ruszala przy 7 dobach pamieci doplywu i schodzila do najnizszej z krotkich probek (102 d100/160/190/229: 5/6/3/4 miasta, 2.6-5.6 tys./d glebiej; 160: 8/7 miast, 11.0-16.2 tys./d) - firma wracala tylko powoli (SurplusReview co 14 dob o 25%, do pelna <= 56 dob; przeglad kwartalny +15%); teraz wymaga 28 dob (`NetDays`).
- G2: `TreasurySurplusFor` czytaja tez W5b (CrownSurplusPlan), W5c (MidAid), DS (najpierw skarbiec), L5b-1 - kazdy ma wlasny limit 1/30 nadwyzki; w wojnie skarbiec moze dac w dobie W5b + W5c + glod (kazde <= 1/30 biezacej nadwyzki) - nigdy ponizej zapasu wojny (HoardRoom <= nadwyzka; kontrola HoardBelow w liniach). Chleb ze skarbca nie jest w KDay.Bread / Spent (nie wplywy dnia) - linia 165 bez zmian. Rachunki: po End - migawka przesunieta (ShiftSnapshot), wiec jutrzejsze wplywy korony nie maleja drugi raz. PayByLordAge liczy splaty skarbca w linii KS-H jako "panowie miast" (napis; kontrola zlota KS-H 0 - GLordPaid i GSrcGot rosna o tyle samo); NGL-6f porownuje linie pana z KS-H - po recenzji R2 narzedzie dolicza segment m105 (sprawdzone: kopia logu 102 z dopisana splata 1000 zl ze skarbca w d108800 - przed poprawka NGL-6f NIE "pan 2270, KS-H 3270", po - TAK; log 102 / n31 / 160 bez zmian, NGL-0 bez nierozpoznanych). Napis KS-H w C# zostaje (osobny GCrownPaid wymagalby zmiany kontroli zlota KsFoodCarts.cs:1272 - nie teraz). Gracz (recenzja R8c): jedna regula - korona krolestwa AI placi chleb i rachunki za lenno gracza-wasala (gracz nigdy z kiesy, licznik "gracz (nigdy)" bez zmian); krolestwo gracza-wladcy - HoardRoom 0.
- G3: zmienia wynik tylko partii z wynikiem bez podlogi ponizej LimitMin (0.1-0.4% probek traktow, 0.2-3.1% drog Valyrii); podpowiedz "Road" pokaze wtedy wiecej niz 25% liczby bez podlogi. Audyt traktow DR (NDR-5) liczy probki z podlogi poza srednia - bez zmian.
- G4: `Value` ma nowe przeciazenie; stare (bez rezerwacji) zostaje dla innych wolajacych (dzis tylko Choose). Wozy pojada dalej (czas dochodu wsi inny, suma bez zmiany); rezerwacja niezdjeta omijalaby zdrowe miasto - kontrola "wygasle" (M5M-8).

**Kolizje z innymi galeziami:** `w-toku/m105-gospodarka4` - po recenzji R5 sprawdzone na `5055d957` (origin): `git merge-tree --write-tree HEAD 5055d957` czysty (drzewo 80a39ef1); wspolne pliki DsTowns.cs, Settings.cs, McmSettings.cs, sprawdz_logi.py. Scalenie probne w osobnym drzewie (usuniete po tescie): gen_mcm 2285 ustawien i McmSettings z automatu = wynik gen_mcm (git status czysty), build Armoury kod 0 (0 bledow; `IncomeCut(..., bool castle = false)` z gospodarka4 przyjmuje wywolanie m105), sprawdz_narzedzie OK, test syntetyczny OK. G5 `DsRentAfterBread` i G6 `DsCastleIncomeCut` z gospodarka4 zmieniaja renty po chlebie i firmy zamkow - C-4c3 (rody) liczyc na buildzie po scaleniu. **Do przekazania gospodarka4:** G4 `VillageCartGoldAware` zmienia wybor miasta przez wozy wsi, wiec takze to, gdzie i kiedy wies zarabia (kiesy wsi) - zloto sie nie rusza, ale czas dochodu wsi inny. `MarketCarts.cs` i `KsLordFood.cs` zmienia tylko m105-miasta.

**Spojnosc (zamknieta ekonomia):** G1, G3 - zloto sie nie rusza. G2 - skarbiec -> kiesy ludu / kasy zrodel, kontrole w liniach (oczekiwane 0), nigdy z kredytu (nadwyzka bez dlugu w Banku i u miast). G4 - zloto sie nie rusza (tylko wybor celu). Jedna regula na zjawisko: obnizka firmy ponizej zapasu (G1) i (e) miedzy 1.0 a 1.5 zapasu; jeden limit dzienny skarbca na chleb i rachunki.

**Cudzy kod:** `ExplainedNumber` (BaseNumber, SumOfFactors, LimitMinValue, ResultNumber = clamp(Base x (1 + factors), min, max)) - pola uzywane juz w repo (CampCart, BkrotChains, ArmsFromPeople). `Kingdom.KingdomBudgetWallet` (int) - jak W5b i chleb korony.

**Wylaczniki:** kazda zmiana ma wlasny klucz; gdy cos nie przejdzie - wylaczyc klucz tej jednej zmiany (G1 `DsFarmCutBelowReserve`, G2 `CrownHoardFeedsHungry`, G3 `MapRoadBonusAboveFloor`, G4 `VillageCartGoldAware`), nie cofac calosci.

### Czego nie zmieniono (z projektu, sekcje C i D)

- QuestPay8 DivideByZero (GangLeaderNeedsWeaponsIssue; 7 NIE: etap3, T1, E8 T0, D1-K, GL2-1c, NE3-0, T-4) - do **m105-drobne** (prefiks na getter, przy cenie 0 zwraca 0).
- R1-7 w biegu z zapisu (kopalnie Sunmine, Bir Seif, Tylemos - "zloto wsi 0"), NE3-16 / NE3-16c2, narzedzia wsi i budowy bez materialow (T2-K1-6, NE3-18), 2.58 mln szt. niesprzedanych przy zamkach - **gospodarka4**.
- Firmy zamkow -18% (W3) - nie (zaleglosci zamkow M3P-7 to obszar gospodarka4; rodow zamkowych z zapasem < 56 dni w d229 7 wobec 8 w P2).
- Zapas kupcow od dobrobytu (A16-1a), B3-3 / A1-T8 (udzial korony 0.410) - na pozniej / obserwacja.
- **Zostaja NIE (prawdziwy brak w grze, progi bez luzowania):** GL2-6 (jedzenie na polkach Qarth / Braavos - zyja z dowozu KS-H do T* = 7 x cena^(-1/0.6), dla zboza 3.6 dnia; naprawa rusza rdzen KS-H i ceny zboza w zrodlach), GL2-8 i MK-4..7 (oszczednosci i koszyk wielkich miast Essos - niedobor calego dochodu miasta pod koszykiem KS; kandydat po premierze: podzial dochodu klas wedlug udzialu w potrzebie w `TownFolk.Live` SplitOf / Credit). G1 i G2 poprawiaja tu tylko czesc.
- NWAR-15 w biegu 160 z zapisu po T10 - NIE (spadek klina plotno/len 1.00 -> 0.79 od d+14) - poza zakresem (warsztaty tekstylne).

### Poprawki po recenzjach (commit `8b47a3c4`; weryfikacje "ekonomia" R1-R8 i "kod" R1-R6 - wszystkie PRAWDA, czesc co do faktu z mniejszym skutkiem)

| Uwaga | Werdykt weryfikacji | Zmiana |
|---|---|---|
| ekonomia R1 (G1: glebsze ciecie po kazdym wczytaniu) | PRAWDA, skala przesadzona (firma wraca, tylko powoli) | `DsTowns.FastCut` i `FastCensus`: `NetAvg(f, NetDays)` - szybka obnizka dopiero przy pelnych 28 dobach pamieci doplywu (po wczytaniu od 28. doby; w nowej kampanii bez zmian) |
| ekonomia R2 = kod R1 (G2: splaty ze skarbca jako splaty panow, NGL-6f) | PRAWDA | narzedzie `_nglod_scan`: wyrazenie `rx_hfb` poza slownikiem `pl` (stare logi bez "nierozpoznanych"), NGL-6f porownuje pan + skarbiec z KS-H; test syntetyczny czesc 2 |
| ekonomia R3 (szacunek kosztu G1) | PRAWDA (dokumentacja) | liczby z kodu w "Ryzyko" (d120 -9.5 tys./d ... 160 d160 -50.9 tys./d); M5M-3b INFO d86-120 (baza 102 106 788 zl/d) |
| ekonomia R4 (C-4c3: tekst progu, gracz) | PRAWDA (drobne) | tekst "102 1.6% (d202), n31 1.2%; zapas do 2% ok. 2 rody"; `_m105_c4c3` pomija `rodzaj=gracz` (102: 9 z 569, 1.6% - werdykt bez zmian) |
| ekonomia R5 (kolizja z gospodarka4) | PRAWDA (ryzyko, nie blad) | merge-tree czysty na 5055d957, scalenie probne buduje sie (kod 0, gen_mcm 2285); Bieg A na buildzie scalonym; przekazanie: G4 dotyka kies wsi (czas dochodu) |
| ekonomia R6 (G4 a A1-T19) | PRAWDA co do tresci (liczby prognozy w repo nie bylo) | G4: "wplyw na A1-T19 nieznany"; Bieg A czyta A1-T19 z "wybor zmieniony", M5M-8 i GL2-6 |
| ekonomia R7 = kod R2 (M5M-5 tautologia; G1-5a2 tylko gora) | PRAWDA; "co projekt chcial" - NIEPEWNE (dokumentu z takim progiem nie ma) | M5M-5 = samokontrola kodu (0 z konstrukcji), M5M-5b INFO pokrycie chleba ze skarbca; G1-5a2b INFO dolna strona i trend; pokrycie chleba - do planu autotestu |
| ekonomia R8a (naglowek TownFolk.Bread.cs "nigdy z zapasu skarbca") | PRAWDA | naglowek: krok 3 - skarbiec ponad zapas wojny (G2), bez KDay.Bread |
| ekonomia R8b = kod R6 (R2-4e lapie segment m105) | PRAWDA (wyglad wydruku) | `out["brbyk"]` odcina segment " \| m105" przed obcieciem do 300 znakow |
| ekonomia R8c (gracz niespojnie: chleb tak, rachunki nie) | PRAWDA | jedna regula: `HoardBills` takze w galezi gracza (Before tryb RACHUNKI i BeforeEarly) - korona AI placi za lenno gracza-wasala, gracz nigdy z kiesy |
| kod R3 (M5M_ERR bez MarketCarts.Daily / SellPrefix) | PRAWDA, skutek maly (nekon3 i NDR-8 lapaly) | `M5M_ERR`: "ERROR in MarketCarts." (cala klasa); test syntetyczny z bledem MarketCarts.Daily |
| kod R4 (`m105_ds_p1_zero` - ostatnia doba) | PRAWDA, dzis bez skutku | pierwsza doba; P1-4 wyciszony tylko przy `p1z <= min(okno)`, R2-T2-2 tylko przy dobie biegu <= 91 |
| kod R5 (NAKUM-7 / NWAR-15 w planie) | CZESCIOWO PRAWDA (tylko NAKUM-7 w pkt 1) | pkt 1 planu: "NAKUM-7 (102, n31; 160 NIE - glowy rodow, poza zakresem)" |

Sprawdzenie poprawek: build Armoury kod 0 (0 bledow, 7 ostrzezen jak dotad); gen_mcm 2279 (bez zmian - Settings.cs nietkniety); `sprawdz_narzedzie.py tools/sprawdz_logi.py` OK; test syntetyczny OK (M5M dobry / zly + 3 przypadki NGL-6f); grupy etap8, kiesa1, rok2, m105miasta, nglod na logach 102 / n31 / 160 przed i po (`popr/cmp_rows.py`): werdykty bez zmian, nowy wiersz INFO G1-5a2b (102 0.94 -> 0.70, n31 0.91 -> 0.65), tekst progu C-4c3, doba "P1 x 0.000" w P1-4 / R2-T2-2 teraz pierwsza (108731, nadal INFO); NGL-6f na kopii logu 102 z dopisana splata ze skarbca TAK. Klucze MCM bez zmian (4 klucze galezi).

### Plan autotestu (nie uruchamiany - zakaz uruchamiania gry w tej pracy)

1. Narzedzie na starych logach - zrobione: oczekiwane TAK (NAKUM-7 (102, n31; 160 NIE - glowy rodow +65.5 tys./d, poza zakresem), G1-5a2, A16-5b2, C-4c3, R1-10b, KR2-1, NAKUM-2c; NWAR-15 TAK w 102) - zgodne; M5M-0 INFO "brak linii" bez wyjatku - zgodne; po poprawkach `8b47a3c4` werdykty bez zmian (wyzej).
2. **Bieg A: prolog 230 z KS** (`armoury-ks-auto.flag`) **na buildzie po scaleniu z gospodarka4** (recenzja R5; inaczej C-4c3 i kiesy wsi licza sie bez G5 / G6 gospodarki4). Grupy: m105miasta, nglod, nakum, etap8, a16, nkasy, m96dlugi_dlugi, m103p3, m96koszyk, gl2, etap5, m96valyria, korona2, nwar, nekon3, ndrogi, m103wsie, m105gosp4 (straznik "nie gorzej niz 102"), potem wszystkie grupy przez `cmp3.py` wobec 102. Cele: NAKUM-27 <= 14, NKA-3 i NKA-4 TAK, A1-T19 < 0.1% (czytac z "wybor zmieniony" segmentu G4, M5M-8 i GL2-6 - wplyw G4 nieznany), M5M-7 (VR-3 ze wszystkimi) >= 24.9%, C-4c3 <= 2%, M5M-3 >= 0.9 x baza, M5M-0..9 TAK (M5M-5 = samokontrola), NGL-6f TAK, NDR-8 TAK, M3P-7 <= 200 tys., zero nowych NIE poza GL2-6, GL2-8, MK.
   **NIEPEWNE - do sprawdzenia w Biegu A (bez progu):** (a) pokrycie chleba ze skarbca w krolestwach z nadwyzka (weryfikacja kodu R2: "co projekt chcial" nieustalone) - M5M-5b (szacunek 67% w 102, reszta Pentos); gdy duza luka "nie stac nikogo" zostaje w krolestwie z duza nadwyzka skarbca (linia Chleb M3 "na krolestwo"), limit doby 1/30 jest za ciasny - decyzja po biegu; (b) M5M-3b d86-120 - szacunek z kodu ok. 91% bazy 102 (miary szacunku i wiersza sie roznia); (c) C-4c3 na buildzie scalonym - zapas do 2% to ok. 2 rody; (d) G1-5a2b - trend kas wobec progu gersumy (102 -0.216 na 100 dob).
3. **Bieg B: 160 z zapisu z KS** (autotest-F-d240), te same grupy. Cele: VR-3 25.00, NKA-4 TAK, M96D-3 <= 50 tys., M3P-7 nie gorzej niz 449 tys. +-10%, C-4c3 <= 2% (M5M-3 - INFO, bieg krotszy niz 229 dob). G1 po wczytaniu (recenzja R1): segment "m105 szybka obnizka" - "dzis 0" i "z firma ponad doplyw" 0 do 28. doby po wczytaniu, potem obnizki; **NIEPEWNE:** czy w tych 28 dobach sama obnizka (e) utrzyma miasta przy zapasie - NKA-3 / M5M-1 w d1-28 wobec d29-56. R1-7 NIE - sprawdzic udzial R1-7b ("pusta kiesa wsi") i przekazac do gospodarka4. NAKUM-7 i NWAR-15 NIE oczekiwane (poza zakresem).
4. Gdy cos nie przejdzie - wylaczyc klucz tej jednej zmiany i powtorzyc bieg. Wgranie tylko po udanym tescie i nie wtedy, gdy Jeff gra.

### Przewidywane wyniki progow (Bieg A, prolog 230 z KS; z projektu i szacunkow - nie z biegu)

| Prog | 102 bez zmian | Przewidywane | Zrodlo |
|---|---|---|---|
| NAKUM-27 / NKA-3 (miasta z KS ponizej zapasu) | 12 -> 18 / 16 | <= 14 / TAK (szacunek 102 d229 17 -> 11) | G1, `m105/g1_szybka.py` |
| M5M-1 / M5M-2 | 4 / 2.10% | <= 2 / <= 1% | G1 |
| M5M-3 (d202-229) / M5M-3b (d86-120) | 315 136 / 106 788 zl/d | >= 0.9 x (koszt wg kodu d229 -19.9 tys./d = ok. 94%) / INFO ok. 91% | recenzja R3 (`rec_g1b.py`) |
| C-4c3 | 1.6% | <= 2% (zapas ok. 2 rody; dochod panow nizszy) | T5, R4 |
| NKA-4 / M5M-4 (rachunki KS-H, Qarth) | NIE (11 dob, 56 tys.) | TAK / 0 dob, <= 10 tys. | G2, `m105/g2_korona_glod.py` |
| M5M-5b (chleb ze skarbca) | - | ok. 67% luki po panie (Pentos bez nadwyzki) | G2 |
| M5M-6 (koszt skarbcow) | - | <= 40 tys./d, kontrole 0 | G2 |
| NGL-6f | TAK | TAK (z segmentem m105) | R2 |
| M5M-7 (VR-3 ze wszystkimi) | 24.98% | >= 24.9% (ok. 25.0) | G3 |
| A1-T19 | 0.148% | NIEPEWNE (< 0.1% tylko gdy G4 lapie zdarzenia rampy KS) | G4, R6 |
| M5M-8 | - | <= 1% (wiesci wygasaja w 0.04% wyborow) | G4 |
| GL2-6, GL2-8, MK-4..7 | NIE | NIE (prawdziwy brak, poza zakresem - kandydat po premierze) | sekcja "Zostaja NIE" |
