## 2026-10-20 (n22f: recenzja scalenia p5-naprawy, p5-jednostki, p5-kruki z reszta n22)

**Mody:** Armoury (Rumours.cs, Ravens.cs) | **Galaz:** `w-toku/n22f-scalenie` (baza `4885ae75` = n22-calosc: WGRANIE 92 + 18 galezi fali 1) | **Tor:** M (bez gry, bez autotestu; build 5 modow kod 0, gen_mcm bez zmian, sprawdz_narzedzie OK) | **Status:** DO SPRAWDZENIA (w biegu przed WGRANIEM 93)

**Dla Jeffa (prosto):**
Kruki i plotki w karczmach gryzly sie ze soba: karczma pamieta tylko ostatnie 60 wiesci, a przy ok. 11 nowych wiesciach dziennie najstarsza wypadala po 5-6 dniach. Kruk z daleka leci dluzej (z Westeros do Qarth ok. tygodnia), wiec karczma w Essos czekala na kruka, a wiesc w tym czasie juz z pamieci wypadla - o dalekich wojnach i oblezeniach nie uslyszalbys tam nigdy. Teraz karczma trzyma wiesc tak dlugo, jak moze jeszcze leciec z nia kruk (13 dni, najwyzej 300 wiesci); kiedy kruk doleci, plotka o tym jest. Reszta trzech galezi (naprawy wedlug cechow, platnerz w taborze, jazda Doliny i Freyowie) z reszta fali 1 nie koliduje - sprawdzone w kodzie ponizej.

### Znaleziska i wynik

| # | interakcja / waga | znalezisko | wynik |
|---|---|---|---|
| 1 | Rumours x Ravens (p5-kruki x n20-plotki) / wazne | ksiega wiesci 60 (FIFO) gubi daleka wiesc, zanim doleci kruk (Ravens.NotYet) - plotka nigdy | **poprawione** (klucz RavenNews) |
| 2 | WorkshopLaw: p5-tarcze x p5-naprawy | rece podwojnie / ujemne / pomiar tarcz | bez kolizji - bez zmian |
| 3 | WorkshopLaw x p5-warsztaty (WorkshopStartup/WorkshopTrade) | wspolny stan | brak wspolnego stanu - bez zmian |
| 4 | CrashScribe Army175 (p5-jednostki x 175.2, DTE, p5-tarcze) | kolejnosc, idempotencja, latki ROT | bez kolizji - bez zmian |
| 5 | ArmouryBehavior / SubModuleMain / Harmony / SyncData | Reset, klucze zapisu, latki | bez kolizji - bez zmian |
| 6 | gen_mcm / McmSettings / sprawdz_logi | grupy, def, dispatch, B4 | bez kolizji - bez zmian |

### 1. Plotki o dalekich wiesciach ginely przy krukach (poprawione)

**Problem.** p5-kruki (R3-05c) wstrzymuje plotke w karczmie (Rumours.Wars i trwajace oblezenia - `Ravens.NotYet`), dopoki nie doleci kruk: koniec doby zdarzenia + 3 h + odleglosc / 6.25 jedn. na godzine, najwyzej 12 dob. Ksiega wiesci Rumours (n20-plotki i starsze) trzyma najwyzej `NewsMax = 60` wiesci, najstarsza odpada (`AddNews`: `while (_news.Count > NewsMax) _news.RemoveAt(0)`). Opis MCM RavenNews obiecuje: "Tavern gossip about such news waits for the same raven" - a dla dalekiej karczmy wiesc odpadala z ksiegi wczesniej, niz kruk mogl doleciec.

**Dowod.** Linia doby "Plotki ... | wiesci N (nowe dzis M)" w 8 biegach (r2nowa, r3nowa, n21nowa, n22anowa, r2160, r3160, n21160, n22a160; skrypt `n22f-scalenie-tmp/okno_wiesci.py`):
- nowe wiesci: srednio 10.9-11.4 na dobe (mediana 11-12, najwiecej 23-27); ksiega pelna (60) w 97-156 z 119-161 dob (80-96%);
- zycie wiesci w ksiedze: mediana 5-6 dob (q10 4-5, q90 7-13) - wiec plotka moze dojsc tylko do karczmy blizej niz ok. 580-730 jedn. mapy (q10: 431-581). Pentos - Winterfell 643 jedn. (kruk 4.4 doby), Qarth - Krolewska Przystan 1062 (7.2 doby) - w Essos daleka wojna w Westeros nie trafilaby do plotek prawie nigdy (zostaje tylko ogolnik "Folk say ... is at war with ...").
- Przed p5-kruki ta sama wiesc byla w karczmie od razu (te same 5-6 dob), czyli to regresja ze scalenia, nie zmiennosc.
- Trwajacy bieg z DLL n22 (Armoury-2026-10-20_07-23-31.log, tylko odczyt, do d108866): "wiesci 60" w kazdej dobie, kruki "sr. 83 h, najdluzej 113 h", "plotki w karczmie wstrzymane do kruka 39" na dobe - sredni kruk (3.5 doby + koniec doby zdarzenia) zabiera juz ok. 70% zycia wiesci w ksiedze.

**Przyczyna.** Dwie reguly bez wspolnej miary: okno ksiegi (60 wpisow) jest krotsze niz najdluzszy lot kruka (12 dob + doba zdarzenia). Kod obu galezi osobno poprawny.

**Zmiana (minimalna, pod istniejacym kluczem RavenNews).**
- `Rumours.AddNews`: ponad 60 najstarsza wiesc odpada jak dotad, ale przy wlaczonym RavenNews dopiero, gdy jest starsza niz `Ravens.HoldDays` (= 12 dob lotu + doba zdarzenia = 13; NotYet liczy zdarzenie na koniec jego dnia), albo gdy ksiega przekroczy `NewsMaxRavens = 300`. Wylaczony RavenNews - 60 jak dotad, co do wpisu.
- `Rumours.Import`: czyta do 300 wiesci (zapis z krukami); przy wylaczonym kluczu pierwsza nowa wiesc przycina ksiege do 60.
- `Ravens.HoldDays` - nowa wlasciwosc (z tej samej stalej MaxHours co NotYet i kolejka krukow - jedna miara).
- Sufit 300: symulacja nowej reguly na dobowych liczbach 4 biegow (n21nowa, n22anowa, n21160, n22a160): ksiega po 14. dobie mediana 155-178 wiesci, najwiecej 238; wiesci mlodszych niz 13 dob wyrzuconych 0. Nowe wiesci i ich zrodla (bitwy, napady band z imieniem herszta, oblezenia) bez zmian - nic sie nie dubluje, zmienia sie tylko, jak dlugo wiesc zostaje w pamieci.

**Ryzyko / co sprawdzic.**
- Wieksza ksiega: Rumours.Wars przeglada do ok. 240 wpisow przy kazdym sluchaniu plotek (nie co klatke; dotad 60) i SiegeNotYet dla oblezen do 300 jedn. - koszt pomijalny wobec reszty budowy plotek ("budowa srednio X ms" w linii doby). Zapis arm_rumours: ok. 240 x ok. 100 znakow = ok. 25 KB wiecej, SaveText.Sync (paczka 161) bez limitu 32 KB; straznik SaveMaxChars bez zmian.
- Blisko gracza moze teraz wygrac starsza (6-13 dob) bliska wiesc z nowsza daleka - zgodnie z NewsDays = 60 (plotka do 60 dni z napisem "A few days ago / Some weeks ago"); wynik punktowy (swiezosc x bliskosc) bez zmian.
- Powrot do DLL WGRANIA 92 z zapisem z wieksza ksiega: stara DLL czyta 60 PIERWSZYCH (najstarszych) wiesci - najnowsze z zapisu by przepadly (tylko plotki, raz).
- Kolejnosc ksiegi: AddNews dopisuje na koniec, eksport/import zachowuja kolejnosc - _news[0] to zawsze najstarsza.
- Watki: tylko watek glowny (zdarzenia kampanii i rozmowa w karczmie), bez nowych alokacji w petli.

**Test w grze.** Bieg 120 dob z RavenNews: linia "Plotki ... | wiesci N" - N powyzej 60 (oczekiwanie: ok. 130-240 po 14. dobie), nigdy ponad 300; "Kruki (R3-05c)" - liczba plotek wstrzymanych (doba) bez zmian znaczenia. Na mapie: postoj w karczmie w Essos (Pentos/Qarth) kilka dni po bitwie lub oblezeniu w Westeros - plotka "A few days ago ... laid siege to ..." pojawia sie po kruku (czat "A raven from ..."), nie przed nim. Z RavenNews wylaczonym: N najwyzej 60, jak w WGRANIU 92.

### 2. WorkshopLaw: tarczownicy w wojnie (p5-tarcze) i godziny napraw cechow (p5-naprawy) - bez kolizji

Sprawdzone w `WorkshopLaw.BuildPlan` (linie 1009-1114):
- ciecie napraw cechu (GuildMend.ManDaysYesterday / liczba ukrytych warsztatow) dla czynnego cechu `cut = min(h, gr)` - h nigdy ujemne; dla nieczynnego `min(freed, own, gr)` - freed nigdy ujemne; wszystko liczone od nowa w kazdej z 3 rund (repairs, carp zerowane na poczatku rundy) - nic nie narasta;
- godziny cechu w GuildMend (Capacity = TownHands x waga / suma 6 wag) <= own w warsztacie (H x waga / suma wag czynnych w planie), wiec wczorajsze naprawy zawsze mieszcza sie w rekach cechu - nie ma podwojnego liczenia ani rak z niczego;
- rece ciesli (ShieldWarHelp.ExtraHands) dopisane w osobnej petli PO cieciach napraw, od `carpOwn = own` - naprawy robia tylko tarczownicy (GuildMend nie liczy ciesli w Capacity), ciesle nikomu nie zabieraja rak; `_hands += H + carp`, `_handsRepair` obejmuje teraz tez naprawy pozostalych cechow (linia "rece (174.1)" spojna: bezczynne = rece - linie - naprawy);
- pomiar tarcz (ShieldWarHelp.Measure: AiGear.Deficit w partiach AI) - ani p5-naprawy (AiWear.MendInTown - tylko naprawy), ani p5-jednostki go nie zmieniaja; frey_rider bez tarczy (R3-08f) zmniejsza potrzebe tarcz Freyow w tej samej mierze, co linia 171 - porownanie "swiat vs 171" dalej trafne.
- p5-warsztaty (WorkshopStartup, WorkshopTrade): tylko pamiec i cena wyniku warsztatu - zadnego wspolnego stanu z rekami, SmithHours ani GuildMend (grep: brak odwolan).

### 3. CrashScribe Army175 (p5-jednostki) - bez kolizji

- Kolejnosc: zestaw Freyow przy wczytaniu po GoldenBows (JazdaGear(false)), zbroja Doliny w sesji po QohorHeavier (TierGearCheck, DailyCatchUp, "start sesji (zapas)"), umiejetnosci (JazdaSkills) po SkillSinew i DothrakiRiders - jak w opisie galezi; flagi `_freyGearDone/_valeArmorDone/_knightsSkillsDone/_freySkillsDone` czyszczone w Army175.Reset (ResetJazda), `_rotTplPatched` na proces (latka raz).
- Latka ROT `ROTTroopRecruiter.GetTemplateComposition` (postfiks) - jedyna na ten cel (inne nasze latki ROT: Settings, ExchangeClanTroops - inne metody); CavalryTemplate liczy IsMounted - frey_rider dalej konny, bez zmian.
- p5-tarcze: patrz pkt 2 (miara tarcz ta sama co 171).

### 4. ArmouryBehavior / SubModuleMain / Harmony / SyncData - bez kolizji

- Wynik scalen 3 galezi = dokladnie zmiany galezi (porownanie linii dodanych/usunietych baza 5189c0fe -> galaz wobec rodzic n22 -> scalenie: 1308/1308, 626/626, 976/976); `tools/scalanie/metody.py` - kazda dodana linia w tej samej metodzie (jeden falszywy alarm: pole `_p5RavensReset` miedzy metodami).
- Reset: Ravens - osobne pole (jak p5-urodzaj), GuildMend + FieldArmourer - przez SmithHours.Reset w konstruktorze; SmithHours.Reset wolany tylko tam.
- SyncData: nowy klucz `arm_ravens` unikalny (grep wszystkich kluczy 4 modow - jedyne "duplikaty" to pary zapis/odczyt tego samego klucza). GuildMend bez zapisu (jak SmithHours).
- Harmony: Ravens wlasne id `com.jeff.armoury.ravens`, latki raz na proces w starcie sesji; CampaignInformationManager.NewLogEntryAdded - zadna inna DLL w Modules (grep metadanych) ani nasz kod jej nie latuje; BK SiegeConsequences - nasz transpiler (Ravens) obok prefiksu/postfiksu CitySack L6 (Harmony laczy); Diplomacy ApplyInternal/NotifyExpired i ROT patch7 - inne metody niz BkDipGold (AssessCosts) i RealisticCaptivity CleanBreak (TakePrisonerAction.ApplyInternal).
- p5-naprawy bez latek Harmony; GuildMend/FieldArmourer tylko na watku glownym (jedyne kody na puli watkow - F5Koszt, MapRoads, MapSideRoads - nie dotykaja napraw).

### 5. gen_mcm / McmSettings / sprawdz_logi - bez kolizji

- `python tools/gen_mcm.py` - McmSettings bez zmian (zgodne z Settings.cs), brak powtorzonych nazw w MCM; nowe klucze: RavenNews (grupa plotek "A night's rest", jak InnGossip*), CampArmourer/CampQuartermasterSteward ("The men's gear"), MendHoursByGuild/AutoMendHalfGuild/MendBulkEveryOrder ("Time at the forge"), Army175ValeReachKnights/Army175FreyHorseArchers ("Armies of the realms (175)").
- `tools/scalanie/sprawdz_narzedzie.py tools/sprawdz_logi.py`: dispatch poza main 0, zle wstawki 0, zdublowane def 0, zdublowane grupy 0, kompilacja OK. Jedyna zmiana narzedzia z 3 galezi (R2_B4 z dopiskiem R3-08g) - sprawdzona na n22anowa: 120/120 linii B4 dopasowanych. Linia "wiesci N" w sprawdz_logi tylko jako INFO (bez progu 60).

### Otwarte (bez zmian, do decyzji/biegu)
- GuildMend `_carry` (dlug cechu za zlecenie gracza ponad wolne rece) i lawa platnerza w taborze nie sa w zapisie - po wczytaniu zaleglosc cechu znika (godziny raz "za darmo"); opis galezi mowi "stan tylko dzienny". Do rozwazenia przy nastepnym przegladzie napraw.
- Brak biegu z trzema galeziami (n22a = bez naprawy/jednostki/kruki): linie "Naprawy wg cechow (R3-02)", "Platnerz w taborze (R3-04c)", "Kruki (R3-05c)", "Mends: konni lucznicy Freyow (R3-08f)" do obejrzenia w pierwszym biegu n22.
