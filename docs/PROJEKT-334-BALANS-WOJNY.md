# Projekt 334: równowaga wojny korona–Północ według kanonu (z Żelaznymi Ludźmi)

Gałąź bazowa: noc/333-ai-bitwy (a7e623fb), czyli WGRANIE 167. Skargi Jeffa z 06.11:
- 13:45: „znowu Polnoc leje poludnie, to jest w ogole nie zbalansowana gra”,
- 13:50: „Zelazni ludzie w ogole atakuja Polnoc, pladruja ja, czy siedza na wyspie?”.

Źródła: diagnoza z logów 04–06.11 (243 doby, 109279–109522) i mapa mechanizmów 264/274/326/330. Kod gry: AiMilitaryBehavior 1.4.8 i AiHelper (dekompilacja e307).

Przy pisaniu projektu Jeff gra. Projekt powstał wyłącznie z czytania kodu i logów. Testy (rozdz. F) wolno uruchomić dopiero po zamknięciu gry, na kopii `autotest-przekierowany-*`, a po serii trzeba posprzątać.

Odpowiedź na pytanie Jeffa w jednym zdaniu: **nie, dziś Żelaźni nie atakują Północy wcale.** Przez 243 doby nie zebrali żadnej armii, nie oblegali niczego i nie zrobili żadnego najazdu z łupem. Siedzą na wyspach i biją bandy, a to Północ oblegała ich Ten Towers.

---

## A. Przyczyny (korzenie, nie objawy)

Wszystkie poniższe przyczyny mają jedno wspólne źródło: **kilka miar w modzie liczy siłę „całego królestwa” albo „całej koalicji”, a nie siłę, która rzeczywiście stoi na danym froncie.** Z tego biorą się P1–P3 i część P4.

| # | Przyczyna | Dowód (log / kod) | Skutek |
|---|---|---|---|
| P1 | **SCA liczy jako zagrożenie całą siłę wroga, bez względu na odległość** (`CalculateFactionStatus`: EnemyStrength = CurrentTotalStrength wszystkich wrogów, razem z załogami). Wrogowie Północy dostają IsExhausted, a potem ForcePeace. | korona: wróg 68277 / swoi 15175; Reach: 68181 / 23676; Wyspy: 46255 / 11452. Północ: 50303 / 46255, więc nie jest wyczerpana. | Cele oblężeń ×0.55 (wyczerpanie) i ×0.35 (ForcePeace), w sumie ok. ×0.19; obrona ×1.7. Korona oblegała 0 z 243 dób, Wyspy miały 0 armii, Reach stoczył 1 bitwę w 55 dób. |
| P2 | **320e zdejmuje ForcePeace tylko wtedy, gdy WSZYSTKIE wojny z królestwami mają pokój zablokowany przez 264.** Korona walczy też ze Smoczą Skałą, a Reach i Wyspy walczą z Północą, i tych par 264 nie obejmuje. | `zablokowane 0 z 1`, `0 z 2` | ForcePeace zostaje u wszystkich trzech wrogów Północy. |
| P3 | **274 i 330 liczą koalicję, a nie front.** B = korona + Reach, przy czym Reach jest bezczynny od 684 dób. | B/A 0.882, `dzwignie NIE`; sama korona: 6726 ludzi wobec 18181. 330: korona 22405 > 6.7 tys. w partiach. | Dźwignie 274, zbiórka i powściągliwość silniejszego się nie włączają. 330 pokazuje 1:1.13, a w polu jest ok. 1:2.7. |
| P4 | **Weto siedzib w BK zwraca null zamiast następnego celu.** | „BK cel zdjety 3” na dobę; korona miała armię przez 69 z 243 dób. | Gdy najbliższą wrogą twierdzą jest siedziba (Riverrun, Bliźniaki, Winterfell), korona tego dnia nie zakłada armii. |
| P5 | **Rozejm 330 rozbraja tylko koronę.** W rozejmie korona przechodzi na budżet pokoju 166 (pułap 0.28 D zamiast 0.60 D), a Północ dalej jest w wojnie (Reach, Wyspy). | 109375–109415: korona spada z 61 partii / 9.7 tys. do 29 / 2.4 tys., Północ ma 111 / 21 tys.; po wznowieniu korona ma 6–7 tys. | Każdy rozejm trwale przesuwa siły na korzyść Północy. |
| P6 | **Brak teatru kanonu.** Północ może oblegać i zdobywać wszystko, co jest w zasięgu AI. Weto 326b obejmuje tylko jedną zwróconą twierdzę na 60 dób. | Duskendale (Crownlands): szturm 109519; oblężenia Rosby i Tumbleton; zwrot z załogą 0. | Cykl „zdobycie → łup i ludzie dla Północy → pusty zamek dla korony”. |
| P7 | **Żelaźni nie są kanonicznym hamulcem**, bo opcje ich najazdu nie powstają: <br>(a) ForcePeace z P1; <br>(b) jedzenie poniżej progu gry 8 dni (`GetFoodScoreForActionType` zwraca 0 i przerywa myślenie); <br>(c) zasięg morski: próg 5 × średnia odległość dla nawigacji All = 411, Pyke–Flint's Finger to 331 drogi, a część morska jest mnożona przez `CalculateShipDistanceAmplifier`. Ten mnożnik daje 0.35–1.25 przy zdrowych statkach, ×1.5–3.5 przy załodze ponad pojemność i pół przekątnej mapy przy safe-sail krótszym od rejsu. Zasięgu nie zmierzono. <br>(d) Weto 274 na Winterfell. | Opcje rabunku pojawiają się po każdym wczytaniu (5 z 5) i gasną w ciągu doby. Od 109507 jedzenie jest, a opcji dalej 0. | Północ trzyma 60% lordów w armiach na południu i nie musi nikogo zostawiać na zachodnim wybrzeżu. |
| P8 | **Strukturalna przewaga pojemności** (ROT + unia 272): B ma 13.1 tys. miejsc, A 29.3 tys. (0.45). Korona ma pieniądze (skarbiec 3.7 mln), ale dźwignia W4 (najemnicy) jest tylko raportem. | Rody korony na minusie: 16–22; skarbiec ponad zapas wojny. | Bogactwo Lannisterów (kanon: Brave Companions, „Lannister zawsze płaci”) nie zamienia się w ludzi w polu. |

**Odrzucone jako przyczyny** (z uzasadnieniem):
- **Jakość wojska.** Konni 7.5% / 7.5%, tier 3.1 / 2.6, straty prawie równe. Silniejsza strona wygrała 61 z 65 bitew, więc decyduje liczebność, nie jakość.
- **NorthHomeEdge.** Dotyczył 4 bitew. Reguła zgadza się ze słowami Jeffa („twardsi, zwłaszcza w zimie i u siebie”), więc **zostaje bez zmian**.
- **326x FrontGarrison.** Wyłączony decyzją Jeffa („załogi, na jakie stać lorda”) i **zostaje wyłączony**.
- **175.** To drobny wpływ.

---

## B. Zmiany (paczka 334)

Zasady wspólne dla wszystkich zmian:
- tylko liczby z istniejących modeli (gra, SCA, 166, 274, 326a);
- złoto i ludzie płyną wyłącznie istniejącymi kanałami: skarbiec, kiesa, kontrakt najemnika, werbunek, rynek, stocznia;
- każdy klucz działa na żywo, a wyłączony daje stan 167 co do monety;
- potknięcia są liczone, bez globalnego wyłącznika.

### B1. Miara frontu F334 (wspólny pomocnik, plik `WarFront334.cs`)
`FrontStrength(X, Y)` to suma `PartyBase.EstimatedStrength` (z p9) drużyn lordów i armii królestwa X (z drużyną gracza, jeśli należy do X), które **mogą dosięgnąć Y w horyzoncie obrony gry**:
- czas drogi drużyny do najbliższej twierdzy Y albo do najbliższej twierdzy X zagrożonej przez Y wynosi najwyżej **8.02 doby**;
- czas liczy się tym samym pomocnikiem gry co myślenie AI (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` z prędkością drużyny), więc morze ze statkami liczy się tak, jak liczy je gra;
- 8.02 to stała z modelu (`AiMilitaryBehavior.GetDistanceScoreForDefending`, w 274 jako `DefendRangeDays`);
- załogi nie liczą się do F334, bo nie wychodzą w pole; w linii logu są osobno.

Pomiar jest raz na dobę, z pamięcią podręczną par. SCA pyta co 4 h i dostaje wartość z doby.

Stosunek frontu: `rF(Północ, korona) = F(korona→Północ) / F(Północ→korona)`. Sojusznik (Reach) wchodzi do strony B tylko tymi drużynami, które spełniają warunek zasięgu, czyli dopiero wtedy, gdy rzeczywiście stoi na froncie.

### B2. SCA: zagrożenie z frontu, nie z całego królestwa (postfiks 320b, klucz `ScaFrontThreat334`)
- Postfiks `CalculateFactionStatus`, który już przelicza EnemyStrength bez band, liczy teraz EnemyStrength = Σ F(wróg→my) po wrogich królestwach i klanach. Pasmo wyczerpania SCA zostaje bez zmian (wróg > swoi × 1.35).
- Swoich SCA liczy jak dotąd, razem z załogami: broni się także murami.
- Skutek dla Wysp: wrogiem jest tylko ta część Północy, która może dopłynąć albo dojść do ich twierdz w 8.02 doby. Dziś to prawie nic, bo armie Północy są w Dorzeczu i na Zachodzie, a Północ ma 36 statków.
- Skutek dla korony i Reach: liczy się tylko siła Północy na ich froncie.

### B3. 320e: ForcePeace zdjęty, gdy GŁÓWNA wojna ma zablokowany pokój (klucz `ScaFightsOnMainWarLocked334`)
- Dotychczasowy warunek „wszystkie wojny zablokowane przez 264” zmienia się na: **wojny z pokojem zablokowanym przez 264, 330 (para Północ–korona do kapitulacji) albo 312b Feuds (Wyspy–Północ 433–1018) dają co najmniej połowę zagrożenia F334**.
- Mechanizm jest ten sam co w 320e: ForcePeace zastąpiony następnym celem SCA, a WantsPeace = false.
- IsExhausted zostaje (cele ×0.55), bo to prawdziwa przewaga wroga na froncie. Jej źródło usuwają B4 i B6, a nie ta zmiana.

### B4. 274 i 330 liczą front, nie koalicję (klucz `CanonWarFront334`)
- **274:** S_A = F(Północ→korona), S_B = F(korona→Północ) + F(sojusznik→Północ) w zasięgu. Pasmo (1/1.5..1.5), progi 3 dób i dźwignie zostają bez zmian. Zmienia się tylko miara.
- **330:** w linii i w warunkach K1/K3 siła = ta sama miara F334 (symetrycznie dla obu stron). Koalicja zostaje w linii jako „koalicja”, tylko informacyjnie.
- **homeGuard 274:** rody z twierdzą w zasięgu **realnego** zagrożenia trzeciego wroga zostają w domu. Zagrożeniem są drużyny trzeciego wroga z F(trzeci→my) > 0 w 8.02 doby od twierdzy rodu. Dotąd liczyło się 60 jednostek lądem do twierdzy wroga, przez co morze było niewidoczne. Gdy Żelaźni wylądują na Kamiennym Brzegu, Glover, Tallhart i Flint zostają u siebie, jak w kanonie (ser Rodrik).

### B5. W4: najemnicy za złoto korony (dźwignia 274, plik `CanonWarBalance.Levers.cs`, klucz `CanonWarBalanceHireCompanies`)
- **Kiedy:** dźwignie słabszej strony włączone (B4) i skarbiec królestwa ponad zapas wojny (ten sam zapas co W5b).
- **Co:** władca najmuje wolną kompanię najemną (bez kontraktu, nie w wojnie ze stroną) mechaniką gry i kontraktem 185 (K = 1.3 × dzienny żołd). Płaci ze skarbca, bez kredytu.
- **Limity:** najwyżej 1 kompania na 7 dób (`SpawnEveryDays`), do `rF ≥ 1/1.2` (`CalmBand`), skarbiec po wypłacie nie schodzi poniżej zapasu wojny.
- **Symetria:** ta sama reguła działa dla Północy, gdy to ona jest słabsza (po Krwawych Godach, po stratach od Wysp).
- **Kanon:** Tywin najmuje Dzielnych Kompanów; złoto Casterly Rock to atut korony.
- **Bez cheatu:** ludzie istnieją już w kompaniach, złoto wychodzi ze skarbca, a księga 185 to rejestruje.

### B6. Rozejm uzbrojony (330, klucz `WarTruceArmed334`)
W rozejmie 330 obie strony pary liczą się w 166 jako „w wojnie”. Dotyczy to `ClanBudget.AtWar`, W5 i W5b.

Uzasadnienie: rozejm jest czasowy (42 dni), a wojna wraca sama, bo korona nie kapituluje. Pułap wojenny 0.60 D liczy się z prawdziwego dochodu, nie powstaje ani złoto, ani ludzie. Usuwa P5.

### B7. Teatr kanonu Północy do Krwawych Godów (klucz `NorthCanonTheatre334`, plik `CanonWarBalance.Theatre.cs`)
- **Reguła:** AI Północy (drużyny, armie, BK) nie zaczyna oblężenia ani najazdu na osadę, której `Settlement.Culture` (z ROT-Map, nie zmienia się po zdobyciu) nie należy do teatru. Mechanizm jest ten sam co weto siedzib 274: wynik myśli -1 i weto BK z B8.
- **Teatr dozwolony:**
  - `battania` (Północ),
  - `river` (Dorzecze),
  - `vlandia` (Westerlands, gdzie Robb wygrywa bitwy i bierze zamki; zwrot zostaje w 326b),
  - osada, którą Północ posiadała w tej kampanii i straciła (odbicie).
- **Poza teatrem:** `crownlands`, `reach`, `stormlands`, `dragonstone`, `vale`, `aserai` i rdzeń Wysp `sturgia`. Robb nie płynie na Pyke, a zachodnie wybrzeże Północy ma bronić, nie zdobywać.
- **Faza:** od startu kanonu do Krwawych Godów (`CanonWarClock`, ta sama reguła co 264/274). Po Krwawych Godach teatr jest wyłączony.
- **Gracz:** weto nie dotyczy decyzji gracza. Działa na wodza AI armii, w której jest Jeff, więc Jeffa nikt nie zaciągnie pod Duskendale. Gdy Jeff sam prowadzi armię, może oblegać wszystko. Jego zdobycz zostaje u niego (326b pomija gracza). Zdobycz przyznana rodowi AI wraca przez 326b.
- **Zastępuje przyczynę P6.** 326b zostaje jako siatka bezpieczeństwa na przypadki wyjątkowe (zdobycz gracza przyznana AI, zdobycie przed wczytaniem 334).

### B8. Weto BK: następny cel zamiast null (klucz `BkVetoNextTarget334`)
- Postfiks `BKArmyBehavior.FindArmyObjective`: gdy cel BK jest zawetowany (siedziba 274, teatr B7, 326b), wybiera **następną** wrogą twierdzę według tej samej reguły BK (najbliższa w 280 jednostkach, bez wetowanych). Null zostaje tylko wtedy, gdy nie ma żadnej.
- Działa dla obu stron, bo usuwa P4 u korony, a u Północy przenosi cel w obręb teatru.

### B9. Żelaźni: kanoniczna wojna z zachodu (klucz `IronbornCanonReave334`, plik `IronbornReave334.cs`, sterowany pomiarem Z0)

**Z0. Pomiar (bezwarunkowy, rozszerzenie WW0, linia „Balans wojny (334): Zelazni ...”).** Na partię lordów Wysp raz na dobę, a w pierwszej dobie po wczytaniu co 4 h:
- położenie: wyspa / ląd Północy / morze;
- czy jest flota, ile statków, pojemność załogi zdrowych statków (kadłub > 20%) wobec ludzi;
- kadłub min / średnio;
- `GetEstimatedSafeSailDuration` wobec czasu rejsu do najbliższej wsi Północy;
- odległość skorygowana gry do najbliższej wsi Północy wobec progu 5 × `GetAverageDistanceBetweenClosestTwoTownsWithNavigationType`;
- dni jedzenia wobec `NeededFoodsInDaysThresholdForRaid` / `ForSiege`;
- liczba opcji RaidSettlement i BesiegeSettlement z `PartyThinkParams`;
- powód postoju (DefaultBehavior, cel, obiekt, w którym stoi).

Zbiorczo: ile partii odpada na każdym sicie (jedzenie → armia → stan wsi → zasięg), w tej kolejności co gra. **Z0 rozstrzyga, które z Z1–Z3 jest potrzebne**, a pozostałe zostają, bo na nie zawsze trafiają jakieś przypadki (pełna macierz).

**Z1. Jedzenie z rynku za złoto rodu.** Partia Wysp poniżej progu najazdu, ze złotem rodu powyżej podłogi 166, idzie do najbliższego własnego portu z jedzeniem na rynku. Jedzie celem ruchu gry (jak 274 PatrolAroundPoint), a jedzenie kupuje sama gra przy wizycie. Gdy rynki Wysp są puste (zima, rynek ma na 5 dób), celem jest najbliższy **neutralny** port z jedzeniem, w którym Wyspy mogą handlować (Lannisport, Wyspy Tarczowe przed wojną, Braavos), do zasięgu morskiego gry. Ceny gry, złoto rodu. Kanon: Wyspy nie mają chleba i kupują albo biorą „żelazną ceną”.

**Z2. Flota zdolna do rejsu (model statków gry i 326a).**
- **(a) Kadłub.** Statek z kadłubem ≤ 20% albo safe-sail krótszy od rejsu do celu: partia w porcie własnym naprawia statek naprawą NavalDLC po cenie gry ze złota rodu, zanim wypłynie.
- **(b) Załoga.** Ludzie ponad pojemność zdrowych statków: 326a przydziela partii statek z puli floty królestwa (255 statków przy idealne 426). Statki istnieją, więc nie powstają z niczego. Gdy statków brak, 326a kupuje je za złoto w stoczni, do ideału (jak dotąd).
- **(c) Kontrola.** Linia Z0 po naprawie ma pokazywać mnożnik ≤ 1.25 i Flint's Finger w progu 411.

**Z3. Przyczółek kanonu.** Gdy po Z1 i Z2 cel dalej jest poza progiem 5× (Deepwood Motte 444, Moat Cailin), Wyspy prowadzą wojnę etapami, jak w kanonie: Kamienny Brzeg, potem Deepwood i Moat Cailin.
- Pierwszy cel osiągalny z morza to wsie Kamiennego Brzegu i Flint's Finger, w progu gry.
- Lądowanie i pierwsza twierdza na lądzie Północy dają nową bazę. Od niej progi gry liczą się już lądem, więc Torrhen's Square, Deepwood i Moat Cailin wchodzą w zasięg same, bez nowej stałej.

**Z4. Winterfell dla Wysp.** Weto siedzib 274 (Winterfell) nie dotyczy królestwa sturgia w fazie przed Krwawymi Godami (kanon: Theon). Dla stron A i B weto zostaje.

**Z5. SCA Wysp.** Wystarczy B2: zagrożenie z frontu. Do tego B3 zdejmuje ForcePeace, bo pokój Wysp z Północą blokuje Feuds 312b.

**Teatr Wysp:**

| faza | cele |
|---|---|
| do Krwawych Godów | osady kultury `battania` na zachodzie Północy i wybrzeże `river` (Seagard to kanoniczny cel najazdów); Westerlands i korona tylko wtedy, gdy toczy się wojna z nimi (dziś nie) |
| po Krwawych Godach | to samo, plus `reach` (wody Tarczowe, Mander, Arbor), gdy istnieje wojna z koroną albo Reach. Samo wypowiedzenie tej wojny (Euron) należy do osobnej paczki, 334 go nie tworzy. |

### B10. Linie i diagnostyka
- Wszystkie nowe linie mają prefiks **„Balans wojny (334): ”** (rozdz. D).
- Pomiar korony (bezwarunkowy): na ród korony podaje limit, ludzi, pułap 166, żołd, W5, ochotników w zasięgu i niegotowych p9. Na armię korony podaje dni jedzenia i przyczynę głodu (rynek najbliższego miasta, złoto wodza).
- Te dane służą do decyzji po teście, czy potrzebny jest następny krok (Tywin głodował pod Ashemark 109468–109479). Same nic nie zmieniają.

---

## C. Pełna macierz przypadków

Fazy:
- **F0:** start kanonu do Czarnego Nurtu (~767). Reach jest sojusznikiem korony od śmierci Renly'ego (m104).
- **F1:** od Czarnego Nurtu do Krwawych Godów (953). Reach wchodzi do korony (332).
- **F2:** po Krwawych Godach.

| # | faza | front | stan sił (rF = korona/Północ) | gracz | co działa w 334 | oczekiwany wynik |
|---|---|---|---|---|---|---|
| 1 | F0 | Dorzecze / Westerlands | rF < 1/1.5 | poza armią | B4: dźwignie korony (W1, W2, W5 najemnicy, zbiórka); powściągliwość Północy (cele frontu 55); B7 teatr; B8 cele korony; B2/B3 bez ForcePeace | korona odbudowuje siły z własnego złota, Tywin w polu |
| 2 | F0 | jw. | w paśmie | poza armią | B2/B3/B7/B8, bez dźwigni | wyrównane bitwy, oblężenia obu stron |
| 3 | F0 | jw. | rF > 1.5 | poza armią | dźwignie Północy (symetrycznie), powściągliwość korony; weta siedzib Riverrun i Winterfell zostają | korona nie podbija Północy |
| 4 | F0 | Crownlands / Reach / Stormlands | dowolny | poza armią | B7: Północ nie zaczyna oblężeń ani najazdów; odsiecz na własne oblężone twierdze w teatrze zostaje | 0 twierdz Północy poza teatrem |
| 5 | F0 | zachód Północy (Wyspy) | dowolny | dowolny | B9 Z1–Z5, B2, B3, homeGuard B4 | Wyspy najeżdżają Kamienny Brzeg, oblegają Flint's Finger, Torrhen's Square, Deepwood i Moat Cailin; rody zachodu Północy zostają w domu |
| 6 | F0 | Wyspy rdzeń (Pyke, Ten Towers) | dowolny | poza armią | B7: Północ nie oblega rdzenia Wysp | brak powtórki oblężenia Ten Towers |
| 7 | F0–F1 | dowolny | dowolny | **w armii AI Północy** | weta B7 i 274 działają na wodza; F334 liczy drużynę Jeffa w armii | Jeff walczy w Dorzeczu i na Zachodzie, nie pod Duskendale |
| 8 | F0–F1 | dowolny | dowolny | **prowadzi własną armię** | brak wet dla gracza; zdobycz gracza zostaje u niego (326b pomija gracza); przydział AI wraca przez 326b; F334 liczy drużynę gracza | wolność gracza, bez obchodzenia teatru przez AI |
| 9 | F0–F1 | dowolny | dowolny | poza Północą / bez królestwa | 334 nie dotyczy gracza; miary bez jego drużyny | bez zmian |
| 10 | dowolna | para Północ–korona w rozejmie 330 | dowolny | dowolny | B6: obie strony z budżetem wojny | po rozejmie korona ma tyle partii co przed |
| 11 | F1 | Reach w koronie (332) | dowolny | dowolny | Reach to już korona: F334 liczy jej drużyny tylko w zasięgu frontu; B7 bez zmian | Tarly i Tyrellowie w Dorzeczu tylko wtedy, gdy realnie tam stoją |
| 12 | F1 | Smocza Skała (druga wojna korony) | dowolny | dowolny | B2: zagrożenie Smoczej Skały tylko z jej drużyn w zasięgu; B3: główna wojna (Północ, 264/330) zablokowana, więc bez ForcePeace | korona walczy na dwóch frontach bez paraliżu |
| 13 | F2 | wszystkie | dowolny | dowolny | B7 wyłączone; Z4 wyłączone; B2/B3/B4/B5/B8 dalej działają (330 F3: rozejm, kapitulacja); teatr Wysp + Reach | Północ upada albo walczy dalej według 330, Wyspy przechodzą na Reach, jeśli trwa wojna |
| 14 | dowolna | Wyspy: brak wojny z Północą (po 1018 albo pokój) | — | — | B9 nie ma celów | bez zmian |
| 15 | dowolna | kanon wyłączony / inna kampania | — | — | wszystkie reguły 334 nieczynne (`Prologue.CanonHere`), pomiar Z0 tylko w linii | bez zmian |
| 16 | dowolna | pierwsze doby po wczytaniu | — | — | F334 liczony przy pierwszej dobie; SCA do tego czasu dostaje wartość z zapisu albo surową (licznik „bez F334” w linii) | brak skoku po wczytaniu (dziś 5 z 5 wczytań) |

---

## D. Klucze Settings / MCM

Wszystkie klucze to `bool`, domyślnie `true` i działają na żywo. W `Settings.cs` stoją po sekcji 326, z komentarzem po angielsku i po polsku bez ogonków. Po `tools/gen_mcm.py` trzeba przywrócić ręczne wpisy McmSettings 326d i 326x.

| klucz | MCM (EN) | wyłączony |
|---|---|---|
| `CanonWarFront334` | Canon War Front Measure: the balance of the war and the truce rules count only the troops that can reach the front, not whole alliances | 274/330 liczą koalicję jak w 167 |
| `ScaFrontThreat334` | Strategic AI Front Threat: a realm fears only the enemy troops that can reach its lands | SCA liczy cały wrogi świat (320b bez zmian) |
| `ScaFightsOnMainWarLocked334` | Strategic AI Fights On When Main War Locked: no "force peace" goal when peace with the main enemy is impossible | warunek 320e „wszystkie wojny” |
| `CanonWarBalanceHireCompanies` | Weaker Side Hires Sellswords: the weaker side of the canon war hires free companies from its treasury above the war reserve | W4 tylko raport |
| `WarTruceArmed334` | Armed Truce: during a truce between the North and the crown both keep their war budgets | rozejm = budżet pokoju |
| `NorthCanonTheatre334` | North Canon Theatre: until the Red Wedding the North wages war only in the North, the Riverlands and the Westerlands | Północ oblega wszystko w zasięgu |
| `BkVetoNextTarget334` | Army Objective Skips Vetoed Seats: a vetoed siege target gives the next enemy stronghold instead of no army | null jak w 167 |
| `IronbornCanonReave334` | Ironborn Reave The North: the Iron Islands buy food, mend and man their ships and strike the western shore of the North as in the books | Wyspy jak w 167 (Z0 dalej mierzy) |

Nowych liczb nie ma. Wszystkie progi pochodzą z istniejących modeli:

| liczba | źródło |
|---|---|
| 8.02 doby | gra, horyzont obrony |
| 1.35 | SCA |
| 1.5 / 1.2 / 3 doby / 7 dób | 274 |
| 280 | BK |
| 5 × średnia odległość | gra |
| kadłub 20%, safe-sail | NavalDLC |
| ideał floty | 326a |
| zapas wojny | W5b |
| K = 1.3 × żołd | 185 |

---

## E. Linie logu „Balans wojny (334): ”

1. `start sesji | klucze ... | latki: SCA status ok, SCA cel ok, BK ok, mysl AI ok | teatr Polnocy: osad w teatrze N / poza N | Wyspy: prog najazdu (All/Naval/Default) X/Y/Z, Pyke->Flint's Finger droga D`
2. `dzien N | faza F0/F1/F2 | front Polnoc->korona Fa (partii, ludzi) / korona->Polnoc Fb (w tym sojusznik ...) | rF x.xx (koalicja x.xx) | 274 dzwignie TAK/NIE od d | 330 sila F334 a/b`
3. `dzien N | SCA: krolestwo wrog surowy->F334, swoi, wyczerpane, cel przed->po, chce pokoju` (korona, Reach, Wyspy, Polnoc, Smocza Skala)
4. `dzien N | teatr: weta mysli k, weta BK k, cele poza teatrem zdobyte przez AI Polnocy 0` i zdarzenie `weto teatru | partia -> osada (kultura)` (raz na partię, osadę i dobę)
5. `BK nastepny cel | armia -> zawetowany X -> nowy Y (odl.)` oraz w linii doby `BK null 0`
6. `najem kompanii | krolestwo -> kompania (ludzi), kontrakt K, skarbiec przed/po, zapas wojny` oraz w linii doby `kompanii najetych dzis / razem, zloto razem`
7. `rozejm uzbrojony | dzien N | rody korony w budzecie wojny k, ludzie korony dzis vs przed rozejmem`
8. `Zelazni | dzien N | partii P: wyspa/lad/morze, sita: jedzenie a, armia b, stan wsi c, zasieg d, z opcja rabunku e (opcji, wsi), z opcja oblezenia f; mnoznik statkow med/max; kadlub min; safe-sail<rejs g; zaloga>pojemnosc h | Z1 do portu k, Z2 naprawy m (zloto), statki z puli n | armie, oblezenia (cele), rabunki z lupem`
9. `Zelazni partia | ...` (szczegół Z0, pierwsza doba po wczytaniu i co 7 dób; najwyżej 60 linii)
10. `korona pomiar | rody: limit/ludzie/pulap/zold/W5/ochotnicy/p9 | armie: jedzenie, przyczyna`
11. `kontrola | zloto utworzone 0, ludzie utworzeni 0 (przeplywy: skarbiec->kompanie, kiesa->naprawy/jedzenie) | potkniecia s`

---

## F. Grupa `sprawdz_logi --grupa balans334` (progi)

Dokładne liczby tej tabeli są wzorem do testu na kopii zapisu Jeffa: bieg 60–120 dób od dnia ok. 109522, flaga `armoury-balans334.flag`. Przy wyniku PASS trzeba potwierdzić ten sam stan 2 razy.

| ID | co | próg |
|---|---|---|
| B334-1 | linia startu, wszystkie łatki wpięte | jest, `ok` ×4 |
| B334-2 | potknięcia 334 (max z linii doby) | 0 |
| B334-3 | rF (front korona/Północ), mediana ostatnich 30 dób | 0.8–1.2 (sama miara; koalicja jako INFO) |
| B334-4 | trend rF: mediana ostatniej tercji minus pierwszej tercji | \|Δ\| ≤ 0.15 |
| B334-5 | twierdze kultury crownlands/reach/stormlands zdobyte przez AI Północy przed Krwawymi Godami | 0 (zdobycze gracza jako INFO) |
| B334-6 | korona: doby z ≥ 1 armią | ≥ 50% |
| B334-7 | korona: doby z oblężeniem celu Północy | ≥ 1 w każdym oknie 28 dób |
| B334-8 | Tywin (lord_4_1) w polu (drużyna nie w osadzie i nie w lochu) | ≥ 70% dób |
| B334-9 | bitwy polowe Północ–korona (bitwy.log): udział wygranych korony | 35–65% przy ≥ 10 bitwach, inaczej INFO |
| B334-10 | SCA korony: doby z celem ForcePeace | ≤ 10% |
| B334-11 | Wyspy: doby z ≥ 1 armią | ≥ 30% |
| B334-12 | Wyspy: oblężenia osad Północy (kultura battania) | ≥ 1 w 60 dobach |
| B334-13 | Wyspy: rabunki z łupem (WW1 / linia 8) | ≥ 3 w 60 dobach |
| B334-14 | Wyspy: doby z opcją rabunku > 0 po 3. dobie | ≥ 50% |
| B334-15 | Wyspy: partie poniżej progu jedzenia (mediana) | ≤ 25% |
| B334-16 | Wyspy: mnożnik statków (mediana partii z flotą) | ≤ 1.25 |
| B334-17 | rozejm 330, gdy wystąpi: partie korony w rozejmie | ≥ 80% stanu z dnia przed rozejmem (bez rozejmu INFO) |
| B334-18 | BK null przy wecie, gdy istnieje inny cel w 280 | 0 |
| B334-19 | kontrola: złoto i ludzie utworzeni przez 334 | 0 / 0 |
| B334-20 | Winterfell oblegany przez stronę A lub B przed Krwawymi Godami | 0 (przez Wyspy INFO) |
| B334-21 | najemnicy: skarbiec po najmie ≥ zapas wojny | 100% najmów |
| B334-22 | regresja: P326-6, W330-x, R274-x bez nowych FAIL | jak w 167 |

---

## G. Kolejność prac i ryzyka

**Kolejność:**
1. B1 F334 + linie 2–3 (sam pomiar).
2. Z0.
3. Krótki bieg pomiarowy 20 dób, żeby potwierdzić przyczynę P7.
4. B2 + B3.
5. B4.
6. B7 + B8.
7. B6.
8. B5.
9. B9 Z1–Z4, w kolejności, jaką wskaże Z0.
10. Bieg 120 dób: build Release `-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs`, kod wyjścia 0.

**Ryzyka:**
- **Koszt F334.** To ok. 300 drużyn × najbliższe twierdze raz na dobę. Trzeba liczyć z indeksu twierdz, nie wszystkie pary, i mierzyć w DayProfile.
- **Łatka SCA.** Działa na tym samym postfiksie co 320b (DeclaredMethod), bez nowych latek na małych metodach (lekcja z GC).
- **Pętla „Wyspy podbijają za dużo”.** Ogranicza ją sama Północ: homeGuard B4 i obrona gry w 8.02 doby. Kanon dopuszcza utratę Moat Cailin, Deepwood i Torrhen's Square do F2.
- **Przy włączonej grze Jeffa** nic nie testować i nic nie wgrywać. Kopie zapisów sprzątać po serii.

## H. Paczka 334c (po teście 334b: 81 dób z kopii zapisu Jeffa, wywrotka na dobie 78)

### H1. Wywrotka pogody przy Blackwater (klucz `PartyPositionGuard334c`, plik `WarFront334.Guard.cs`, każda kampania)
**Dowód:** `IndexOutOfRangeException` w `DefaultMapWeatherModel.GetWeatherEventInPosition` ← `NavalDLCMapWeatherModel.GetWeatherEffectOnTerrainForPosition` ← `MobileParty.IsBaseSpeedCacheInvalid` (wątek `ParallelTickMovingParties`), 26 s po `FABULA: BlackwaterSiegeEvent WYSTARTOWALO (dzien 767)`. Indeks siatki pogody to `(int)(x / komórka)` bez obcięcia, a osady leżą w x 12–1577, y 2–1245, więc wyjątek daje tylko `(int)NaN`. Pozycja partii to NaN. W 2495 archiwalnych logach ten błąd jest tylko w tej sesji. To pierwszy bieg, który doszedł do Blackwater. „BLAME NavalDLC” jest mylące: NavalDLC tylko przekazuje wywołanie.

**Przyczyna:** `MobileParty.FinishNavigationTransitionInternal` ustawia pozycję końca przejścia ląd/morze. Gdy przejście zaczęło się w osadzie (wyjście z portu), gra nie zapisuje jego końca i zostaje `CampaignVec2.Invalid`. ROT `BlackwaterSiegeEvent.SetupSiegeAttackers` i `DefendSiegeTarget` wyprowadzają Stannisa i lordów Smoczej Skały z osady (`LeaveSettlementAction`), przestawiają ich setterem `Position` i nie wołają `CancelNavigationTransition`. Smocza Skała to wyspa, więc lordowie często są wtedy w trakcie wypływania. Po zakończeniu przejścia mają pozycję NaN, a przy następnym liczeniu prędkości gra się wywraca.

**Zmiana (trzy warstwy):**
1. **Źródło:** prefiks na `SetupSiegeAttackers` anuluje przejścia Stannisa i partii wojennych rodów Smoczej Skały przed teleportem (ten sam wzór co `BookHoldings.Teleport`). Postfiks robi to samo dla członków `SiegeArmy` i naprawia pozycję NaN. Prefiks na `DefendSiegeTarget` obejmuje Joffreya i Lancela.
2. **Strażnik ogólny:** postfiks na `FinishNavigationTransitionInternal` (wątek główny). Gdy pozycja to NaN, partia dostaje kolejno: pozycję wodza (przypięty albo armia), bramę osady, w której stoi, bramę osady domowej, bramę pierwszej twierdzy frakcji. Flagę morza bierze od wodza, a na bramie zawsze ląd. Przypięci przechodzą razem z wodzem.
3. **Siatka bezpieczeństwa:** prefiks na `GetWeatherEventInPosition`. Dla pozycji poza siatką zwraca `WeatherEvent.Clear` zamiast wyjątku. Na wątku roboczym tylko zwiększa licznik `Interlocked` i dodaje wpis do kolejki (partia z `MapRoads.CurrentWriter`). Naprawa i linia `potkniecie 334c pogoda` (partia, pozycja, naprawa) idą w następnej klatce na wątku głównym.
4. Raz na dobę przegląd wszystkich partii i linia `Pozycje partii (334c): dzien N`.

### H2. Żelaźni nie najeżdżają, bo BK ustawia postawę obronną (klucz `CanonWarStance334c`, plik `WarFront334.Stance.cs`)
**Dowód:** linia „Zelazni” od 2. doby pokazuje „z opcja rabunku 0 (opcji 0)” przez 81 dób. Na dobie 109522, przed pierwszym tickiem BK, były 152 opcje. Pomiar Z0 liczy każdą opcję `RaidSettlement`, nawet z oceną 0, więc gra tej opcji w ogóle nie podała. Zasięg nie jest przyczyną: droga skorygowana wynosi 35–60 przy progu 411.

**Przyczyna:** `BKDiplomacyBehavior.TickKingdoms` działa co dobę i po wczytaniu. Gdy krolestwo ma 1 twierdzę albo najsilniejszy wróg ma co najmniej 1,5 × jego `CurrentTotalStrength`, wszystkie jego partie dostają postawę Defensive. Gdy krolestwo ma co najmniej 1,5 × siłę wroga, dostają Aggressive. `AiMilitaryBehavior.FindBestTargetAndItsValueForFaction` przy Defensive przerywa liczenie dla oblężeń i rabunków, także z zebraniem armii. Wyniki z testu: Wyspy 9,3 tys. wobec Północy 35 tys. – Defensive; korona 13 tys. – Defensive; Północ – Aggressive (×1,2 do oblężeń, bez opcji obrony).

Diagnoza wskazała też lądową bramkę `BKArmyManagementModel.CanLordCreateArmy`. Czynnym modelem armii jest jednak `StrategicCampaignAI145.StrategicArmyManagementModel` (wanilia: członkowie liczeni po swojej `NavigationCapability`, potem cooldown i siła ≥ 260; log „Mury oblezen (OB start)”). BK `CanLordCreateArmy` i `IsLandReachableForGather` dotyczą tylko celu BK `CallBannersGoal`, który prowadzi armię lądem (`NavigationType 1`). Tych metod nie zmieniamy, bo armia wysłana lądem przez morze by utknęła. Waniliowa ścieżka armii Wysp była zablokowana samą postawą.

**Zmiana:** postfiks (priorytet Last) na `TickKingdoms` oraz jednorazowo przy starcie sesji, tylko w kampanii kanonu. Królestwo gracza zostaje bez zmian, tak jak w BK.
- **Strony 274:** ta sama reguła BK (próg 1,5, wyjątek 1 twierdzy). Siła własna to suma `CurrentTotalStrength` królestw tej strony. Wróg to max(suma królestw drugiej strony w wojnie z tym królestwem, najsilniejszy inny wróg). Przy stosunku 0,9–0,95 obie strony wychodzą Neutral.
- **Żelazne Wyspy w wojnie z Północą:** Neutral, czyli postawa gry bez BK. Najeźdźca jest słabszy, ale w kanonie rabuje i oblega wybrzeże. Neutral nie odcina żadnej opcji, także obrony.
- **Pozostałe królestwa** (Dorne, Dolina, Essos, Wolni Ludzie) zostają przy wyniku BK. Wolni Ludzie mają ten sam objaw, ale to osobna decyzja.
- **Linia** `Balans wojny (334): postawa (334c) | dzien N`: BK → 334c, siła i wróg, twierdze, partie obronne i ofensywne dla Północy, korony, Reach, Wysp i Smoczej Skały.
- **Autotest:** T6, reguła na 7 przypadkach.

### H3. Korona „wyczerpana” w SCA (klucz `ScaCoalitionOwn334c`, wymaga `ScaFrontThreat334`)
**Dowód:** linia SCA 334 korony: „wrog 69850->35590, swoi 16007, wyczerpane tak”. Reach: „wrog 69127->34834, swoi 24098, wyczerpane tak”. 152 ze 159 dób-lordów korony ma wpis „SCA: wyczerpane - cele oblezen x0.55”.

**Przyczyna:** 334 B2 zamienił wroga SCA na front F334, który obejmuje też sojuszników Północy. „Swoi” zostali jednak samym królestwem, bez Reach. Warunek SCA to wróg > swoi × 1,35.

**Zmiana:** w postfiksie 320b `CalculateFactionStatus` `OwnedStrength` królestwa strony 274 to suma `CurrentTotalStrength` królestw tej strony, czyli ta sama miara SCA. Wróg zostaje F334 do tego królestwa, liczony raz. Według logu korona ma 16 + 24 ≈ 40 tys. wobec 35–39 tys. Cel `DefendCapitalRegion` przy wrogu > 500 pod stolicą zostaje (Stannis pod King's Landing). `OwnedStrength` czyta w SCA tylko `IsExhausted`. Linia SCA 334 pokazuje „swoi krolestwa X->koalicja Y”.

### H4. Błędy raportu
- **B334-1:** linia startu 334 była wypisywana przed `ScaNoBandits320.EnsureHooks`, bo zależało to od kolejności słuchaczy `OnSessionLaunched`. Teraz `EnsureHooks` = `Wire()` + linia, a `WarFront334.SessionStart` woła `Wire()` przed swoją linią. Linia startu 334 pokazuje też łatki 334c.
- **R274-9:** wzór dnia 274 obejmuje teraz nawias 326c po „lordow do frontu N”, co dodano też do self-testu. Na logu 334b wzór rozpoznaje 40/40 linii zamiast 0/40.
- **B334-6/-8:** liczone tylko w dobach wojny (z linią frontu). B334-8 liczy Tywina „na wojnie”, czyli w armii albo w zasięgu frontu Północy (`Reaches`, 8,02 doby drogi). Stara linia bez tego pola liczy „w polu”.
- **B334-7:** okna 28 dób tylko w ciągłych odcinkach wojny. Odcinek krótszy niż 28 dób daje INFO.
- **B334-17:** liczona mediana 3 kolejnych dób, więc jedna doba po przegranej bitwie nie rozstrzyga.

### H5. Nowe progi `--grupa balans334 <Armoury-*.log> [<session-*.log>]`
| Próg | Co | Warunek |
|---|---|---|
| B334-23 | wywrotki pogody: „pogoda poza siatka” z linii 334c, opcjonalnie `IndexOutOfRangeException` / `GetWeatherEventInPosition` w session | 0 / 0 |
| B334-24 | NaN po przejściu (i przegląd doby) poza dobą Blackwater ±1 | 0 dób |
| B334-25 | linia `Pozycje partii (334c)` co dobę, łatki ok (Blackwater ok albo „bez ROT”) | tak |
| B334-26 | postawa w dobach wojny: Wyspy bez partii obronnych, korona obronna tylko z reguły koalicji | 0 / 0 |

Zelaznych sprawdzają dalej B334-11 (armie ≥ 30% dób), B334-12 (oblężenia osad Północy ≥ 1 na 60 dób), B334-13 (rabunki z łupem ≥ 3 na 60 dób) i B334-14 (opcja rabunku ≥ 50% dób). Synteza obejmuje dodatkowo 7 biegów: pogoda, NaN poza Blackwater, NaN w dobie Blackwater, postawa, jedna doba spadku w rozejmie, luka rozejmu i stara linia startu.

### H6. Ryzyka i co dalej
- **Gorąca ścieżka pogody:** prefiks robi tylko porównania. Siatka jest liczona raz na sesję, a bez niej działa oryginał.
- **Postawa zmienia przebieg wojny:** Północ traci premię ×1,2 do oblężeń i dostaje opcje obrony. Wyspy dostają rabunki i oblężenia, które dalej przechodzą sito gry: jedzenie, zasięg i `CalculateDistanceScoreForBesieging`. Samotne partie najpierw rabują wybrzeże, a twierdze w głębi biorą dopiero od przyczółka (Z3).
- **Więcej rejsów:** po zmianie postawy na morzu będzie więcej ruchu. Strażnik pozycji obejmuje każdy koniec przejścia.
- **Kampania Jeffa:** poprawkę Blackwater trzeba wgrać przed startem Blackwater w kampanii Jeffa, niezależnie od losów balansu.
- **Test:** ta sama kopia zapisu, do doby ≥ 79. Szukać linii `Blackwater (334c): anulowane przejscia przed teleportem N`; N > 0 potwierdza hipotezę wprost.
