# Projekt 330 „Wojna do kapitulacji”: końcowa wersja po porównaniu trzech projektów

Kod sprawdziłem w obu dekompilatach, gry i Diplomacy, oraz w trzech miejscach: worktree n326x, ROT rot313 i dip147. Zapisu Jeffa nie otwierałem, więc to, czy ma w nim wpis Revenge, sprawdzi dopiero test T1.

## A. Co się potwierdziło, a co jest nieprawdą

1. **Potwierdzone: wpis ROT Revenge istnieje.** `ROTStorylineWars.cs:81`: Polnoc–Westerlands, StartDay 350, EndDay 0, bez wyzwalacza.
   - Fabula w `CrashScribe/src/Fabula.cs:401-414` (`WarPrefix`/`WarFinalizer`) na czas każdego wywołania `Enforced` mnoży StartDay i EndDay przez `FabulaTimeScale`. Wychodzi ok. 1516. dzień opowieści.
   - Kolejność kanonu: Krwawe Gody ok. 953, Jon ok. 1407, **Revenge ok. 1516**, oblężenie Bękartów ok. 1546. P0 ma tu rację.
2. **Raport „nasz-kod” mylił się w jednym ważnym miejscu: po Bękartach pokój stały wcale nie jest otwarty.**
   - Revenge jest wymuszony przed Bękartami. Dzięki temu po końcu fazy 2 dalej działa faza 1 `CanonPeaceLock` (`rotForced`, :145) i codzienne `EnforceWars`.
   - **Prawdziwe dziury, przez które przechodzi pokój stały:**
     - (a) wczesne wyjście k320, gdy Winterfell 120 dób nie jest u Boltonów. Daje okno od ok. 1073. do 1516. dnia;
     - (b) zapis bez wpisu Revenge (lista wojen powstaje tylko w `OnNewGameCreated`);
     - (c) wyłączony klucz `NorthFightsOn`: okno 953–1516;
     - (d) Kronika `truce:` w fazie 2;
     - (e) gracz-władca.
3. **Potwierdzone: `IsWarForced` wewnątrz wywołuje `Enforced`** (`ROTStorylineWars.cs:183-194`).
   - Postfiks na `ROTStorylineWar.Enforced` naraz wycisza trzy rzeczy: `EnforceWars`, model uprawnień ROT („sovereign will not entertain peace”) oraz nasze `Prologue.RotWarForced`, z którego korzystają Kronika, faza 1 264 i 168.
   - Wzór już mamy: `Prologue.Feuds.FeudEnforcedPostfix` (Priority.Last).
4. **Nieprawda: „trzeba zerować daninę przy wznowieniu” (P0/P1/P2) to zbędny krok.** Gra robi to sama:
   - `DeclareWar` ustawia StanceType=War, a to woła `StanceLink.ResetStats` (:492-504), które zeruje `_dailyTributeFrom1To2` i `DailyTributeInstallments`;
   - w czasie wojny danina i tak nie jest płacona (`DefaultClanFinanceModel.AddIncomeFromTribute` :553 pomija strony w wojnie).
5. **Potwierdzone: Diplomacy przy zmęczeniu woła `ApplyPeace` bez pytania o pozwolenie** (`WarExhaustionManager.cs:1672-1689`).
   - `AcceptPeace` (:182-195) robi kolejno: `ApplyCost`, `DoReturnFiefs`, daninę na `DaysInYear*5`, a na końcu może `SoftlyDestroy`.
   - Sygnatura: `DoReturnFiefs(Kingdom, Kingdom, List<Town>)`, private static.
6. **Odrzucam pomysł P1 „rozejm przez ustawianie StartDay/EndDay”.** Fabula na czas wywołania podmienia te pola i potem je przywraca, a pola są w zapisie, więc łatwo o pomyłkę. Zostaje postfiks.
7. **Odrzucam z P1 „koronę liczoną co dobę po King's Landing”.** Tabela `Rules` i wojny ROT są na sztywno przypisane do id królestw, więc zmiana strony korony rozjechałaby fazy i wydarzenia.
8. **Odrzucam z P0 domyślne „Krwawe Gody = kapitulacja” (F2-K).** Jeff wcześniej wybrał 320a-KG: Północ walczy dalej. Zostawiam to bez zmian.
9. **Odrzucam z P2 rozejm także z decyzji AI (H2/H3).** Rozejm ma jedno kontrolowane wejście, czyli H1. Każdy inny pokój łapie siatka H6.
10. **Faza 0 (dziś u Jeffa) dostaje rozejm, tak jak w P2.** Jeff napisał wprost: „pokój może być tymczasowy”.
    - Bez postfiksu `Enforced` taki rozejm trwałby jedną dobę, bo ROT l.71 (wymuszona od śmierci Neda) wypowiedziałby wojnę następnego dnia.
    - Krwawe Gody nie wymagają wojny: `MainCheck` sprawdza tylko życie bohaterów, Bliźniaki i to, czy Północ istnieje. `ExecuteRobb` robi pokój tylko wtedy, gdy strony są w wojnie.

## B. Reguły

**Para:** Północ–korona, wiersz `Rules` z nowym polem `UntilSurrender=true`. Pozostałe pary bez zmian.

**Fazy:**

| Faza | Kiedy |
|---|---|
| F0 | jak dziś, przed Krwawymi Godami |
| F2 | 320a-KG, od Krwawych Godów do Bękartów |
| F3 | wszystko po F2, także po wyjściu k320 |
| Z | po kapitulacji jednej ze stron |

Gdy gracz rządzi jedną ze stron (`PlayerChoice`), nic nie wznawiamy, tylko piszemy linię w logu.

### 1. Rozejm, faza F0 i F3 (w F2 nie ma rozejmu, bo oblężenie Winterfell ma iść jak w ROT)

**Jedyne wejście:** H1, czyli `KingdomPeaceAction.ApplyPeace` przy zmęczeniu Diplomacy. Warunki, wszystkie naraz:
- zmęczenie którejś strony >= 100, albo zima i obie strony >= 75;
- wojna trwa co najmniej **84 doby** od `StanceLink.WarStartDate` (rok gry; chroni przed rozejmem co bitwę, bo zmęczenie po wznowieniu startuje od zera);
- minęła **karencja 7 dób** od pierwszego wczytania wersji 330;
- żadna strona nie spełnia progu kapitulacji. Jeśli spełnia, zamiast rozejmu jest kapitulacja.

**Wykonanie:**
- H1 zwraca false: oryginał Diplomacy nie biegnie, więc nie ma zwrotu twierdz, 5 lat daniny ani `SoftlyDestroy`.
- Wołamy `MakePeaceAction.ApplyByKingdomDecision(payer, recv, daily, dni)`.
  - `daily` liczy `Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(payer.RulingClan, recv.RulingClan, out _)`, czyli aktywny model gry/BK. Płaci strona, która gorzej wypada w postępie wojny. Jeśli wyjdzie 0, nikt nie płaci.
  - Danina trwa tyle, ile rozejm.
- Twierdze zostają tam, gdzie stoją wojska. Jeńcy wracają według PeaceHold, bez zmian.

**Długość:** 42 doby. Jeśli koniec wypada zimą, rozejm przedłuża się do 1. dnia wiosny, najwyżej o 21 dób (razem maksymalnie 63).

**W czasie rozejmu:**
- **H4**, postfiks `ROTStorylineWar.Enforced` (Priority.Last, obok Feuds): dla tej pary zwraca false z powodem „rozejm do d.X”.
- **H5:**
  - prefiks `Kingdom.AddDecision(DeclareWarDecision)` dla tej pary zwraca false;
  - w `CanonThroneWars.WarScorePostfix` (BK `GetScoreOfDeclaringWar`) para dostaje -1e6.

**Koniec rozejmu (doba Armoury):**
1. Najpierw sprawdzamy kapitulację.
2. Jeśli jej nie ma, `DeclareWarAction.ApplyByDefault(odbiorca daniny albo korona, druga strona)`. Jeśli ROT wymusza tę wojnę, wypowie ją sam tej samej doby; my jesteśmy zapasem.
3. Gra sama zeruje daninę, a Diplomacy liczy zmęczenie od 0.
4. Jeśli `DeclareWar` się nie uda, ponawiamy co dobę i liczymy potknięcie.

Jeśli ROT albo inny mod wypowie wojnę wcześniej, rozejm kończy się od razu i trafia do logu.

**H6, siatka bezpieczeństwa:** nasłuch `CampaignEvents.MakePeace` dopięty do listy w `Prologue.Wars:373`.
- Każdy pokój tej pary bez naszej flagi (barter gracza, kody, inne mody, Kronika, wymuszony pokój 312c) zamienia się w rozejm 42 doby.
- Danina przycięta do 42 rat.
- Pokój w F2 (ROT ze starych ścieżek, `RwPeace` przepuszcza) dostaje rozejm 1 doby, więc wojna wraca jak dziś przez `RotDeclareWar272`.
- Wyjątki: nasza kapitulacja, `PlayerChoice` i pokój ROT z Krwawych Godów przy wyłączonym kluczu `NorthFightsOn`.

**Kronika** (`Chronicle.Canon.cs`, `Truce` przed `MakePeaceAction.Apply`): `if (CanonPeaceLock.Stops(k1,k2)) Skip`. W F0/F3 wywołuje `WarTruce.Start("kronika")`, ale tylko przy warunkach z pkt 1.

**H2/H3 bez zmian:** decyzje AI, BK, PovertyPeace i wniosek wasala zostają zablokowane we wszystkich fazach. H1 w F2 nadal blokuje.

### 2. Kapitulacja, symetrycznie (L = przegrywający, W = zwycięzca)

Liczniki są w zapisie. Rosną co dobę wojny, a gdy warunek pęka, wracają do 0. Wystarczy jeden próg.

| Próg | Warunek | Ile dób z rzędu |
|---|---|---|
| K1 | L nie trzyma swojej siedziby (Winterfell / King's Landing), ma ≤ max(3, 1/3 twierdz, które miał w dniu startu liczników), a siła L/W wg 274 < 0.67 | 42 |
| K2 | władca L w niewoli W i siła L/W < 0.67 | 21 |
| K3 | siła L/W < 0.33 | 30 |
| K4 | L ma 0 miast i zamków | brak licznika: grę niszczy królestwo, to koniec wojny, a nie pokój; tylko log |

- W fazie F2 kapitulacja jest tylko odłożona i zapisana w logu.
- Gdy obie strony przekroczą próg tej samej doby, kapituluje ta z niższym stosunkiem sił.
- Śmierć władcy zeruje licznik K2.

**Kapitulacja Północy (cesja z P2, bez wchłaniania królestwa):**
- `MakePeaceAction.ApplyByKingdomDecision`, Północ płaci daninę z modelu gry przez 84 doby.
- Twierdze Północy o kulturze innej niż battania przechodzą do korony (`ChangeOwnerOfSettlementAction.ApplyByBarter`, właściciel = ród władcy korony albo dawny pan z korony).
- Jeńcy wracają.
- Przejście rodów: klucz `WarSurrenderClansToCrown`, domyślnie WYŁ. Gdy ktoś go włączy, gracz dostaje okno wyboru.

**Kapitulacja korony („pokój Króla Północy”):**
- Każdy zatrzymuje to, co trzyma.
- Korona płaci daninę przez 84 doby, jeńcy wracają.
- Korony nie niszczymy, bo ROT jej potrzebuje.

**Po kapitulacji (faza Z):**
- H4 tłumi `Enforced` dla tej pary.
- Pokój trwa do następnego wydarzenia kanonu tej pary: Krwawe Gody (stan 2), JonEvent albo doba Revenge × skala. Każde z nich kasuje zatrzask i wojna wraca w odpowiedniej fazie (F2 albo F3).
- Po zakończeniu Bękartów (`BastardsSiegeEvent`=2) kapitulacja jest ostateczna i para wypada z 330. To jedyny pokój stały.

### 3. Pełna macierz przypadków

| Sytuacja | F0 | F2 | F3 |
|---|---|---|---|
| Zmęczenie 100, wojna ≥ 84 doby | rozejm 42 | blok (jak dziś) | rozejm 42 |
| Zmęczenie 100, wojna < 84 doby | blok + linia „następny od d.X” | blok | blok + linia |
| Zima, oba zmęczenia ≥ 75 | rozejm do wiosny (≤ 63) | blok | rozejm |
| Próg Północy | cesja, faza Z do Krwawych Godów | odłożona | cesja, Z do Jona/Revenge, a po Bękartach na stałe |
| Próg korony | pokój Króla Północy, faza Z do Krwawych Godów | odłożona | jak w F0, a po Bękartach na stałe |
| Krwawe Gody w czasie rozejmu | ROT: przejścia rodów; jego pokój nie biegnie, bo nie ma wojny; koniec rozejmu wznawia wojnę już w F2 | – | – |
| Pokój spoza H1 | rozejm 42 | rozejm 1 doba | rozejm 42 |
| Gracz rządzi stroną | linia, bez akcji | bez akcji | bez akcji |
| Wpisu Revenge brak w zapisie | bez wpływu: wznawiamy sami | bez wpływu | bez wpływu, wznawiamy sami |

## C. Pliki i miejsca w kodzie (worktree n326x)

- **Nowy `Armoury/src/WarTruce.cs`:**
  - stan i zapis `arm_k330` przez `SaveText.Sync`: rozejm od/do, powód, liczba rozejmów, `firstSeenDay`, liczniki K1–K3 dla obu stron, zatrzask Z i wydarzenie, które go kasuje;
  - `Start`, `DailyTick`, `Resume`;
  - H4 `EnforcedPostfix`;
  - H5 prefiks `AddDecision` dla wojny;
  - H6 `OnPeace`.
- **Nowy `Armoury/src/WarTruce.Surrender.cs`:** progi, cesja, pokój Króla Północy.
- **Nowy `Armoury/src/WarTruce.Test.cs`.**
- **`Armoury/src/CanonPeaceLock.cs`:**
  - pole `UntilSurrender` w `Rule` (:61-74);
  - w `Locked` (:126-148) nazwanie fazy F3/Z;
  - w H1 `ApplyPeacePrefix` (:355-376) wywołanie `WarTruce.TryFromExhaustion`, a potem false.
- **`Armoury/src/CanonThroneWars.cs`:** w `WarScorePostfix` -1e6 dla tej pary w czasie rozejmu i w fazie Z.
- **`Armoury/src/Chronicle.Canon.cs`:** w `Truce`, przed `MakePeaceAction.Apply`, wywołanie `Stops`.
- **`Armoury/src/Prologue.Wars.cs:373`:** dopięcie nasłuchu H6.
- **`Armoury/src/Settings.cs`** (grupa „Pokoj kanonu (264)”), klucze:
  - `CanonWarUntilSurrender`=tak (wyłączony daje zachowanie dzisiejsze);
  - `WarTruceDays`=42, `WarTruceWinterMaxExtra`=21, `WarTruceMinWarDays`=84, `WarTruceWinterExhaustion`=75, `WarTruceLoadGraceDays`=7;
  - `WarSurrenderLoseRatio`=0.67, `WarSurrenderRatio`=0.33, `WarSurrenderRatioDays`=30, `WarSurrenderHoldDays`=42, `WarSurrenderCaptiveDays`=21, `WarSurrenderStrongholds`=3, `WarSurrenderTributeDays`=84;
  - `WarSurrenderClansToCrown`=nie.

## D. Logi (prefiks „Wojna do kapitulacji (330)”)

- **Linia dobowa:**
  `faza F | wojna od d.X (n dob) | zmeczenie N/K | rozejm NIE (nastepny od d.Y) / do d.Y nr k | Polnoc K1 a/42 K2 b/21 K3 c/30, tw. n | korona ... | ROT Revenge: jest/BRAK, wymusza tak/nie | karencja do d.Z`
- **Zdarzenia:**
  - `ROZEJM start (zmeczenie|zima|kronika|z zewnatrz:zrodlo) d.X-d.Y, danina P->R d/doba x n`
  - `ROZEJM koniec: wojna wraca (ROT|nasza DeclareWar)`
  - `ROZEJM przerwany (zrodlo)`
  - `blok: AI wojna w rozejmie (zrodlo)`
  - `blok: rozejm odrzucony (faza 2|wojna < 84|karencja)`
  - `KAPITULACJA Polnocy/korony (Kn, licznik)`: cesja n [lista], danina, jeńcy m
  - `kapitulacja odlozona (F2)`
  - `zatrzask Z skasowany (RedWedding|Jon|Revenge) -> wojna`
  - `KONIEC 330 (Bekarci + kapitulacja)`
- **Przy wczytaniu, jednorazowo:** obecność i stan wpisu Revenge oraz l.71 w `_storyLineWars`.

## E. Testy (autotest na kopii `autotest-przekierowany-*`, nie przy włączonej grze Jeffa, sprzątanie po serii)

**Jak dojść do Krwawych Godów:** test syntetyczny pod flagą autotestu (wzór `CanonPeaceLock.Test`). Wymusza fazę przez podstawienie stanu w `Locked` i warunków ROT, zamiast przewijać około 250 dób. Do tego jeden bieg z kodem fazy F2 przez test 320a-KG, który już istnieje.

| Test | Sprawdza |
|---|---|
| T1 | kopia Jeffa, 9 dób: przez 7 dób karencja i blokada jak dziś; doba 8 to rozejm F0 (wojna od dnia 9 > 84): lenna bez zmian, ROT l.71 nie wypowiada wojny; log wpisu Revenge |
| T2 | doba 43 (lub wiosna): wojna wraca, `StanceLink` danina 0, zmęczenie 0 |
| T3 | zmęczenie 100 po 30 dobach od wznowienia: blok |
| T4 | zima 75/75: rozejm do wiosny, nie dłużej niż 63 |
| T5 | F2: blok H1; Kronika pominięta (`Stops`); obcy `MakePeaceAction.Apply` daje rozejm 1 doby i wojnę następnej doby |
| T6 | F3: obcy `MakePeaceAction.Apply` zamienia się w rozejm 42 |
| T7 | AI `DeclareWarDecision` i ocena BK w czasie rozejmu: blok |
| T8 | K1 Północy (podstawione twierdze i stosunek sił): cesja, danina 84, faza Z, 120 dób bez wojny ROT |
| T9 | K3 korony (stosunek 0.30, 30 dób): pokój Króla Północy |
| T10 | faza Z, potem syntetyczne Krwawe Gody i JonEvent: zatrzask skasowany, wojna |
| T11 | zapis i wczytanie w środku rozejmu oraz w fazie Z: daty zachowane |
| T12 | klucz WYŁ.: linie 264 identyczne jak w wersji 326x |
| T13 | gracz-władca: bez akcji |

## F. Ryzyka

1. **Jeff zobaczy pierwszy rozejm ok. 8 dób po wgraniu**, bo dziś ma zmęczenie 100/100. To zgodne z jego decyzją, ale trzeba mu to powiedzieć wprost.
2. **Korona słabnie: stosunek 0.567, a wcześniej było 21.6k, teraz 23.5k u Północy.**
   - Przy < 0.33 przez 30 dób skapituluje korona, co w F0 jest zgodne z regułą „albo korona przegra”.
   - Krwawe Gody i tak przywrócą wojnę. Linia dobowa pokazuje, ile brakuje do progu.
3. **Rozejm w F0 zatrzymuje dźwignie 274 i weta frontu na czas pokoju**, bo działają tylko w wojnie (`CanonWarBalance` sprawdza `IsAtWarWith`). Po wznowieniu wracają same.
4. **Postfiks `Enforced` wpływa też na `RotWarForced`.** Dotyczy to fazy 1 264, Kroniki i 168. W czasie rozejmu jest to spójne: para nie jest w wojnie.
5. **Cesja zmienia właścicieli twierdz.** Test T8 musi sprawdzić zamki z załogą `FrontGarrison` i zgodność 272/320 (Winterfell w F2).
6. **Nie wiemy, czy zapis Jeffa ma wpis Revenge.** Odpowie T1; projekt działa w obu przypadkach.

## Streszczenie dla Jeffa (prosty język)

1. Wojna Północy z koroną nie może się już skończyć zwykłym, wiecznym pokojem.
2. Gdy obie strony są wycieńczone, wolno zawrzeć rozejm na 42 dni (zimą do wiosny), ale nie częściej niż raz na rok walki.
3. Po rozejmie wojna wraca sama, nikt nie musi jej wypowiadać.
4. W czasie rozejmu nikt nie oddaje zamków za darmo. Kto przegrywał, płaci daninę tylko na czas rozejmu.
5. Na stałe wojna kończy się dopiero, gdy któraś strona się podda: straci stolicę i większość zamków, jej król długo siedzi w niewoli albo ma trzy razy mniej wojska przez miesiąc.
6. Poddana Północ oddaje koronie zdobyte ziemie spoza Północy i płaci daninę. Poddana korona uznaje Króla Północy i płaci jemu.
7. Wydarzenia z książki (Krwawe Gody, Jon, Bitwa Bękartów) i tak rozpalą wojnę na nowo. Ostateczny koniec jest dopiero po Bękartach.
8. Między Krwawymi Godami a Bękartami rozejmu nie ma, Północ walczy dalej, tak jak już ustaliłeś.
9. W Twojej kampanii obie strony są wycieńczone do końca, więc około tydzień po wgraniu zobaczysz pierwszy rozejm, a 42 dni później wojna wróci.
10. Korona słabnie. Jeśli spadnie do jednej trzeciej siły Północy na miesiąc, sama się podda. Codziennie w logu widać, ile do tego brakuje.
11. Odpoczynek armii i załogi zostają bez zmian. Całość wyłącza jeden przełącznik.
## G. Poprawka Claude'a po syntezie (05.11, wg słów Jeffa „korona będzie prowadziła wojnę aż do zwycięstwa”)
- Korona NIE kapituluje z samego stosunku sił (K3 tylko dla Północy). Kapitulacja korony wyłącznie z K1 (utrata King's Landing + progi twierdz) albo K2 (król w niewoli 21 dób).
- Jeff 05.11: „pokój może być tymczasowy, ale nie stały”.

## H. Decyzja Jeffa 05.11 wieczorem (ZASTEPUJE sprzeczne punkty B-G)
Jeff: „pokój może być tylko tak, jak było w fabule: jeśli Bran Stark lub Jon Snow zostaną królem, to zawierają pokój z Północą, Sansa, która jest królową Północy”.
- STALY POKOJ korona-Polnoc tylko gdy wladca krolestwa korony (ROT Westerlands = Baratheon KL) to Bran Stark albo Jon Snow (ROTLords.BranStark / ROTLords.JonSnow, zywy). Wtedy: pokoj bez daniny i bez cesji, para wypada z 330 na stale. Jesli Sansa zyje i jest w Polnocy - linia logu "Krolowa Polnocy Sansa"; zmiany wladcy Polnocy NIE robimy (ROT nie ma tego wydarzenia).
  ROT nie ma wydarzenia „Bran/Jon krolem Szesciu Krolestw” - regula czeka na taki stan swiata (sukcesja BK/gra/gracz).
- KORONA NIGDY NIE KAPITULUJE (usunieta „kapitulacja korony” / „pokoj Krola Polnocy”, K1-K3 tylko dla Polnocy). Gdy korona przegrywa - dalej wojna z rozejmami czasowymi.
- Kapitulacja Polnocy (K1-K3) zostaje: to nie pokoj, tylko poddanie (cesja + danina + faza Z do nastepnego wydarzenia kanonu). Po Bekartach kapitulacja Polnocy jest ostateczna (Polnoc pod korona) - to nadal nie „pokoj rowny”.
- Rozejmy czasowe (42 doby, zima do wiosny <= 63, min. 84 doby wojny) bez zmian.
- Wszystkie inne drogi do stalego pokoju (Diplomacy, gra, BK, ROT RedWedding MakePeace, Kronika, barter gracza) -> rozejm czasowy (siatka H6).
