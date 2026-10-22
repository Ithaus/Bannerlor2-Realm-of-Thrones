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

Odstepstwo od kolejnosci projektu (T przed G): commity G poszly pierwsze - na tym komputerze dysk C zeruje pliki w miejscu, wiec kazda zmiana gry szla do repo od razu. Pliki rozlaczne, kazdy commit buduje sie sam (build po kazdym, kod 0).

gen_mcm: 2276 -> 2279 ustawien (4 nowe klucze). Build Armoury: kod 0, 0 bledow (7 ostrzezen jak dotad). `sprawdz_narzedzie.py tools/sprawdz_logi.py`: dispatch, wstawki, def, grupy, kompilacja - OK.
Nowe klucze nie istnieja w `Armoury.json` Jeffa - dzialaja domyslne z kodu (TAK).

---

### G1. Szybka obnizka firmy miasta ponizej zapasu kupcow (klucz `DsFarmCutBelowReserve`, wymaga `KsFarmSurplusCapNet`)

**Problem (log 102 `Armoury-2026-10-21_14-29-20.log`):** NAKUM-27 "12 -> 18 miast" z KS ponizej zapasu kupcow (prog <= 14), NKA-3 "na koniec 16" (prog <= max(12, 14)); szacunek projektu (`m105/g1_szybka.py` na kasy-miast.csv): 102 d229 17 -> 11 miast ponizej zapasu, n31 24 -> 17, bieg 160 z zapisu 13 -> 6. Narzedzie m105miasta na kasy-miast.csv 102: M5M-1 (>= 28 dob z rzedu ponizej zapasu z poborem ponad doplyw) na koniec 4, M5M-2 (pobor ponad doplyw w dobach ponizej zapasu, 56 dob) 2.10% poboru (n31 7 / 2.38%, 160 1 / 1.21%).

**Przyczyna:** `DsTowns.SurplusReview` (galaz `gq <= OverReserve x Reserve || remit || arrears`) - przy kasie ponizej 1.5 x zapasu jedyna droga w dol to obnizka z dochodu (e) `IncomeCut`: raz na Ks Farm Surplus Review Days (14) o Ks Farm Surplus Review Max (25%). Miasto, ktoremu kupcom skonczyl sie zapas obrotowy, placi przez te tygodnie firme ponad doplyw i popada w zaleglosci.

**Zmiana:** `DsTowns.cs` - nowa metoda `FastCut` przed `IncomeCut`, tylko przy `capNet`: gdy `remit == 0` i (kasa po naleznosciach `gq` < `TownPurse.Reserve(t)` albo zaleglosci firmy / dlug rady) - firma pana i korony z kasy (FL, FC) ponad `max(0, NetAvg) x 364` schodzi do niej od razu, ten sam stosunek pan / korona, czesc z oszczednosci ludu (FLf) bez zmiany; `Share` najwyzej 1; `SLow = today` (cykl obnizki (e) od tej doby); nowe pole sesyjne `Farm.FastDay` - najwyzej raz na 7 dob (`FastGapDays`). Bez pomiaru doplywu (NaN, < 7 dob) - nic. Gdy szybka obnizka nic nie zmienila (firma juz przy doplywie, odstep 7 dob) - obnizka (e) jak dotad. Miedzy 1.0 a 1.5 zapasu zostaje (e). Zloto sie nie rusza - zmienia sie nalezna rata.

**Linia logu:** w "Dochod ludu wedlug koszyka (f5-lud): dzien N" za "| P3 sufit firm ..." segment `| m105 szybka obnizka (Ds Farm Cut Below Reserve TAK): dzis N (-X zl/rok), od startu sesji N (-X zl/rok), miast z KS ponizej zapasu M, z firma ponad doplyw K` (spis `DsTowns.FastCensus` - miasta z KS z firma, kasa po naleznosciach < zapas; z nich firma > doplyw x 364). Regexy NLU_RX i M3P "seg" bez kotwicy na koncu - bez zmian.

**Slowami gracza:** "Miasto, ktoremu kupcom skonczyl sie zapas obrotowy, od razu placi panu tyle, ile naprawde zarabia - nie popada w zaleglosci, czekajac dwa tygodnie na obnizke."

---

### G2. Skarbiec korony ponad zapas wojny karmi glodne miasta (klucz `CrownHoardFeedsHungry`)

**Problem (log 102, ostatnie 28 dob):** chleb biedoty M3 "nie stac nikogo" 22.3 tys./d; Qarth: rachunki zywnosci KS-H starsze niz 30 dni (NKA-4: 11 dob, najstarszy 35 dni, blokada dowozu od d219) przy skarbcu 3.97 mln (nadwyzka ponad zapas wojny 3.47 mln). Szacunek projektu (`m105/g2_korona_glod.py`): pokryte 67% chleba (reszta - Pentos, nadwyzka 70 tys.), Qarth splacony, NKA-4 -> 0; bieg 160: chleb 100%.

**Przyczyna:** chleb M3 placila korona tylko z reszty wplywow dnia, potem pan (tempo skrzyni), rachunki KS-H tylko pan (`KsLordFood`); zapas skarbca ponad zapas wojny ("zostaje w skarbcu - zapas na wojny i trudne czasy", Jeff przy f7) nie byl uzywany na glod nigdy.

**Zmiana:**
- `KingdomTreasury.CrownSurplus.cs`: `CsDays` internal; `HoardRoom(k)` - jeden limit dzienny na krolestwo = floor(`TreasurySurplusFor(k)` / CsDays) z pierwszego uzycia doby (ta sama miara nadwyzki co W5b: skarbiec - reszta wplywow dnia - zapas wojny - dlug w Banku - dlug u miast), kazda wyplata najwyzej biezaca nadwyzka; krolestwo gracza (RulingClan = gracz) - 0. `HoardUse`, `HoardBelow` (kontrola: skarbiec po wyplacie ponizej zapasu wojny + reszty wplywow + dlugow). Reset w `CrownSurplusReset`.
- (a) `TownFolk.Bread.cs` `BreadRest`: po koronie z wplywow dnia i po panie reszta luki osady - ze skarbca krolestwa wlasciciela (`HoardRoom`), skarbiec -> kiesy ludu (`BreadCredit`, kontrola zamknieta kies `_ctlIn`). Kwota w `_brCrown` (platnik to skarbiec; "Obieg": "chleb biedoty (M3) skarbce -> kiesy ludu" = `LastBreadCrown`), bez `CrownIncome.Spent` i bez `KDay.Bread` (to nie wplywy dnia - jak W5b `CrownAid`).
- (b) `KsLordFood.cs` `HoardBills` - w `BeforeEarly` (prog wczesnej splaty 20 dni) i w `Before` tryb RACHUNKI (prog = zwloka 30 dni) po kroku pana, takze gdy pan nie mogl (bez pana, bez budzetu, bez miejsca); nie dla miasta gracza i oblezonego. Warunek: kasa bez niczego ponad podloge kredytu (`KsFoodCredit.FreeOf <= 0` - PayDaily splaca z tej samej granicy) i rachunki starsze niz prog > 0. Skarbiec -> kasy zrodel (`PayByLordAge`, najstarsze najpierw, z pomiarem), nieprzyjete wraca do skarbca, `TownPurse.NoteLordFood` jak przy panu. Po recenzji wlasnej (`aa33e728`): `CrownIncome.ShiftSnapshot(k, -took)` - KsLordFood biegnie po `CrownIncome.End`, bez tego jutrzejsze "inne wplywy" korony liczylyby wyplate z zapasu jako wydatek spoza nas i renty dnia malalyby drugi raz.
- Kolejnosc platnikow: wplywy dnia -> pan -> skarbiec. Zloto: skarbiec -> kiesy ludu albo kasy miast zrodel; nic z niczego.

**Linie logu:**
- "Chleb biedoty (M3)": w nawiasie korony dopisek `; w tym ze skarbca ponad zapas wojny X` (regex R2_M3 `[^)]*`), na koncu segment `| m105 skarbiec korony (Crown Hoard Feeds Hungry TAK): ze skarbca X zl (krolestw N, osad M), nie stac nikogo po skarbcu Y zl (w tym przy wolnym limicie skarbca Z zl - oczekiwane 0), kontrola skarbiec -T + kiesy ludu X = 0, ponizej zapasu wojny po wyplacie 0, limit doby 1/30 ...`. Pole "nie stac nikogo" w starej czesci linii to teraz luka PO skarbcu.
- "Pan miasta placi za dowoz (KS)": przed "| czas" segment `| m105 skarbiec korony: rachunki splacone ze skarbca X zl (miast N, krolestw K, kandydatow po panie bez kasy ponad podloge C), kontrola skarbiec -T + wierzyciele X = 0, ponizej zapasu wojny po wyplacie 0`. Linia KS-H liczy te splaty w "splacone przez panow miast" (PayByLordAge; kontrola zlota KS-H dalej 0 - GLordPaid i GSrcGot rosna razem).

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

**Slowami gracza:** "Woznice wiedza, ze do miasta jada juz inne wozy, i nie wioza towaru tam, gdzie kupcom zabraknie pieniedzy."

---

### T1-T11. Narzedzie progow (`tools/sprawdz_logi.py`, commit `6ce383e7`) - gra bez zmian, stara miara zostaje jako INFO

| Prog | Zmiana | 102 (14-29-20) przed -> po | n31 (12-48-45) | 160 z zapisu (13-37-13) |
|---|---|---|---|---|
| T1 NAKUM-7 / NAKUM-7p | sakiewki ludzi poza bramka +30 tys./d (pomiar w wierszu); NAKUM-7p z "Markietani (E5/A3)": <= 15 dni zoldu kazdej doby okna i koniec <= poczatek + 1 dzien; NAKUM-7s bez zmian (bramka tylko skarbce) | NIE -> TAK; 7p TAK 8.0 -> 7.9 | NIE -> TAK; 7p TAK 5.9 -> 6.9 | NIE -> NIE (glowy rodow +65.5 tys./d - poza zakresem); 7p TAK 8.4 -> 9.0 |
| T2 G1-5a / G1-5a2 | G1-5a (Z33 sprzed KS/f4/f5/f7) -> INFO; G1-5a2: kasy / (1.5 x zapas + 28 dob poboru) <= 1.10 w dobie 120 pliku i na koncu (kasy-miast.csv z katalogu sesji) | NIE -> INFO; 5a2 TAK 0.94 / 0.70 | NIE -> INFO; TAK 0.91 / 0.65 | brak wierszy G1-5 (etap8 z zapisu) |
| T3 G1-5c | -> INFO | NIE -> INFO | NIE -> INFO | - |
| T4 A16-5b / A16-5b2 | A16-5b -> INFO; A16-5b2: ostatnie 56 dob >= -15 tys./d (wiazace, gdy okno zaczyna sie po d37) | NIE -> INFO; 5b2 TAK -13.7 | NIE -> INFO; TAK -10.4 | TAK -> INFO; TAK -11.5 |
| T5 C-4c / C-4c3 / C-4c2 | C-4c (jedna doba) -> INFO; C-4c3 (budzet-rodow.csv, od doby 120 pliku): rody z zapasem G < 28 dni przy tempie 28 dob <= 2% i bankrutow 0; C-4c2 INFO | NIE -> INFO; 4c3 TAK 1.6% max, 0 | NIE -> INFO; TAK 1.2% | TAK 1.6% (C-4c2 10.2% INFO) |
| T6 R1-10b | prog 177 -> 60/d | TAK 17/d | TAK 19/d | - |
| T7 KR2-1 | regex `krolestw.*?\|` (bez spacji po "krolestw;") | NIE -> TAK (230/230), KR2-3/4 licza sie: TAK | NIE -> TAK | NIE -> TAK (161/161) |
| T8 NAKUM-2c | w dobie przejscia na F3 skok liczony od 'pobrano' linii kiesa2 doby przed (zmiana pola, nie poboru) | NIE +82542 -> TAK +13901 | NIE +82125 -> TAK +12263 | TAK -> TAK +4849 |
| T9 A3-1 | regex z wstawka `| E3-G2 danina pana ...` | NIE -> TAK (A3-2..A3-7 licza sie: TAK / INFO) | NIE -> TAK | INFO (brak dob okna) |
| T10 NWAR-15 | poczatek = pierwsza doba linii + 14 | NIE -> TAK (0.90 / 0.88 -> 1.28 / 0.82) | NIE -> NIE (0.96 / 0.88 -> 1.31 / 0.61) | **TAK -> NIE** (0.99 / 1.00 -> 1.27 / 0.79; dawny start d1 0.46 / 0.52 byl artefaktem po wczytaniu - prawdziwy spadek klina plotno/len, poza zakresem m105-miasta) |
| T11 P1-4 (kiesa1), R2-T2-2 (rok2) | INFO, gdy ostatnia linia DS pokazuje "P1 ... x 0.000" (P1 pod DS wylaczona z zalozenia) | NIE -> INFO / NIE -> INFO | NIE -> INFO / NIE -> INFO | - / INFO |

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
| M5M-4 | rachunki KS-H starsze niz zwloka kazdej doby / starsze niz prog wczesnej splaty na koniec | 0 dob / <= 10 tys. (102: 11 dob, 56 tys.) |
| M5M-5 | chleb "nie stac nikogo" przy wolnym limicie skarbca | 0 zl |
| M5M-6 | skarbiec ponizej zapasu wojny po wyplacie / kontrola skarbiec - odbiorcy / koszt skarbcow (sr. 28 dob) | 0 / 0 / <= 40 tys./d |
| M5M-7 | VR-3 ze wszystkimi probkami audytu Valyrii (wazona liczba probek) | >= 24.9% |
| M5M-8 | rezerwacje wozow wygasle przy porzadkach / zalozone | <= 1% (dzis wiesci wygasaja w 0.04% wyborow) |
| M5M-9 / 9b | ERROR nowego kodu / czas linii KsLordFood i f5 | 0 / INFO |

Odstepstwa od projektu: M5M-3 - baza projektu 267.9 tys./d nie dala sie odtworzyc z logu ani CSV; prog liczony ta sama miara co wiersz (kasy-miast.csv 102, d202-229: 315 136 zl/d). M5M-9 czas - INFO (nowy kod nie ma osobnego stopera; HoardBills jest w czasie linii KsLordFood). Na starych logach (102, n31, 160): M5M-S TAK, M5M-0 INFO "brak linii m105", M5M-9 TAK - bez wyjatku.

Test syntetyczny: `python -I tools/m105-miasta/test_m5m_syntetyczny.py` - bieg "dobry" (wszystkie M5M TAK) i "zly" (wszystkie NIE: brak segmentu w dobie, 3 miasta ponizej zapasu z poborem ponad doplyw, M5M-3 66.6%, rachunek starszy niz zwloka, luka chleba przy wolnym limicie, kontrola != 0, VR 24.0%, 10% wygaslych, ERROR) - zgodne.

---

### Ryzyko / co sprawdzic (zasada 0 - kontrola calosci)

**Regresje (kto czyta / pisze ten sam stan):**
- G1: `Farm.FL / FC / Share / SLow` pisza tez Review/Renew (przeglad zwykly - cel Share x doplyw x 364, Share <= 1 w miastach z KS - nie podniesie firmy nad doplyw), SurplusReview (podwyzka tylko przy kasie > 1.5 x zapas), FromZero (firma 0 - wroci od zera przy kasie > 1.5 x zapas), IncomeCut (cykl od `SLow`). Szybka obnizka nie dziala w dzien przegladu zwyklego (`_renewN > 0` - jak podwyzki). Firma 0 przy doplywie <= 0 - z projektu; FLf (z oszczednosci ludu) nietkniete. Dochod panow z firm spada wczesniej (szacunek d92-120 ok. -14%, 102 d229 -14.2 tys./d = 3.5% poboru): straznik M5M-3 i C-4c3; mniej pozyczek m96 (M96D-3 ma zostac TAK).
- G2: `TreasurySurplusFor` czytaja tez W5b (CrownSurplusPlan), W5c (MidAid), DS (najpierw skarbiec), L5b-1 - kazdy ma wlasny limit 1/30 nadwyzki; w wojnie skarbiec moze dac w dobie W5b + W5c + glod (kazde <= 1/30 biezacej nadwyzki) - nigdy ponizej zapasu wojny (HoardRoom <= nadwyzka; kontrola HoardBelow w liniach). Chleb ze skarbca nie jest w KDay.Bread / Spent (nie wplywy dnia) - linia 165 bez zmian. Rachunki: po End - migawka przesunieta (ShiftSnapshot), wiec jutrzejsze wplywy korony nie maleja drugi raz. PayByLordAge liczy splaty skarbca w linii KS-H jako "panowie miast" (napis; kontrola zlota KS-H 0).
- G3: zmienia wynik tylko partii z wynikiem bez podlogi ponizej LimitMin (0.1-0.4% probek traktow, 0.2-3.1% drog Valyrii); podpowiedz "Road" pokaze wtedy wiecej niz 25% liczby bez podlogi. Audyt traktow DR (NDR-5) liczy probki z podlogi poza srednia - bez zmian.
- G4: `Value` ma nowe przeciazenie; stare (bez rezerwacji) zostaje dla innych wolajacych (dzis tylko Choose). Wozy pojada dalej (czas dochodu wsi inny, suma bez zmiany); rezerwacja niezdjeta omijalaby zdrowe miasto - kontrola "wygasle" (M5M-8).

**Kolizje z innymi galeziami:** `w-toku/m105-gospodarka4` (nie wypchnieta przy tej pracy - diffu nie dalo sie sprawdzic): ruszamy `MarketCarts.cs` (Haul, Note, Forget, Value, Choose, Daily) i `KsLordFood.cs` (Before, BeforeEarly) - przed scaleniem obejrzec diff obu plikow (pulapka scal.sh: latka w cudzym if). `Settings.cs`: 4 klucze w sekcjach "Town purses", korona (po CrownSurplusAidDays), drogi (po MapRoadSpeedPercent), wozy (przed VillageCartRoadNews) - gen_mcm po scaleniu.

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

### Plan autotestu (nie uruchamiany - zakaz uruchamiania gry w tej pracy)

1. Narzedzie na starych logach - zrobione: oczekiwane TAK (NAKUM-7, G1-5a2, A16-5b2, C-4c3, R1-10b, KR2-1, NAKUM-2c; NWAR-15 TAK w 102) - zgodne; M5M-0 INFO "brak linii" bez wyjatku - zgodne.
2. **Bieg A: prolog 230 z KS** (`armoury-ks-auto.flag`). Grupy: m105miasta, nakum, etap8, a16, nkasy, m96dlugi_dlugi, m103p3, m96koszyk, gl2, etap5, m96valyria, korona2, nwar, nekon3, m103wsie (straznik "nie gorzej niz 102"), potem wszystkie grupy przez `cmp3.py` wobec 102. Cele: NAKUM-27 <= 14, NKA-3 i NKA-4 TAK, A1-T19 < 0.1%, M5M-7 (VR-3 ze wszystkimi) >= 24.9%, C-4c3 <= 2%, M5M-3 >= 0.9 x baza, M5M-0..9 TAK, M3P-7 <= 200 tys., zero nowych NIE poza GL2-6, GL2-8, MK.
3. **Bieg B: 160 z zapisu z KS** (autotest-F-d240), te same grupy. Cele: VR-3 25.00, NKA-4 TAK, M96D-3 <= 50 tys., M3P-7 nie gorzej niz 449 tys. +-10%, C-4c3 <= 2% (M5M-3 - INFO, bieg krotszy niz 229 dob). R1-7 NIE - sprawdzic udzial R1-7b ("pusta kiesa wsi") i przekazac do gospodarka4.
4. Gdy cos nie przejdzie - wylaczyc klucz tej jednej zmiany i powtorzyc bieg. Wgranie tylko po udanym tescie i nie wtedy, gdy Jeff gra.
