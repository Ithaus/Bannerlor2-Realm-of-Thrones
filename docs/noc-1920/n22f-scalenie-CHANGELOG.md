## 2026-10-20 (n22f: recenzja scalenia p5-naprawy, p5-jednostki, p5-kruki z reszta n22)

**Mody:** Armoury (Rumours.cs, Ravens.cs) | **Galaz:** `w-toku/n22f-scalenie` (baza `4885ae75` = n22-calosc: WGRANIE 92 + 18 galezi fali 1) | **Tor:** M (bez gry, bez autotestu; build 5 modow kod 0, gen_mcm bez zmian, sprawdz_narzedzie OK; poprawki po 2 recenzjach - opis, komentarz i 1 linia w `Rumours.Import`) | **Status:** DO SPRAWDZENIA (w biegu przed WGRANIEM 93)

**Dla Jeffa (prosto):**
Kruki i plotki w karczmach gryzly sie ze soba (blad samej galezi krukow, nie scalenia): karczma pamietala tylko ostatnie 60 wiesci, czyli 5-6 dni, a kruk z daleka leci do 12 dni, wiec wiesc o dalekiej wojnie wypadala z pamieci karczmy, zanim kruk z nia dolecial. Teraz karczma pamieta wiesc 13 dni (najwyzej 300 wiesci), wiec po kruku daleka wiesc jest do uslyszenia. Nie znaczy to, ze uslyszysz ja zawsze: karczma podaje tylko 2 wiesci wojenne, najblizsze i najswiezsze, wiec w Essos wiesc z Westeros wejdzie do plotek tylko wtedy, gdy w okolicy nie dzieje sie nic wiekszego. Przy okazji plotki wszedzie pamietaja dluzej - czasem uslyszysz o bliskiej bitwie sprzed tygodnia lub dwoch zamiast o swiezszej, ale dalszej. Pozostale dwie galezie (naprawy wedlug cechow, platnerz w taborze, jazda Doliny i Freyowie) z reszta fali 1 nie koliduja - sprawdzone w kodzie ponizej.

### Znaleziska i wynik

| # | interakcja / waga | znalezisko | wynik |
|---|---|---|---|
| 1 | Rumours x Ravens (blad galezi p5-kruki, widoczny w n22) / wazne | ksiega wiesci 60 (FIFO, z sw-zajazdy) gubi daleka wiesc, zanim doleci kruk (Ravens.NotYet) - plotka o niej nie mogla padnac | **poprawione** (klucz RavenNews) |
| 2 | WorkshopLaw: p5-tarcze x p5-naprawy | rece podwojnie / ujemne / pomiar tarcz | bez kolizji - bez zmian |
| 3 | WorkshopLaw x p5-warsztaty (WorkshopStartup/WorkshopTrade) | wspolny stan | brak wspolnego stanu - bez zmian |
| 4 | CrashScribe Army175 (p5-jednostki x 175.2, DTE, p5-tarcze) | kolejnosc, idempotencja, latki ROT | bez kolizji - bez zmian |
| 5 | ArmouryBehavior / SubModuleMain / Harmony / SyncData | Reset, klucze zapisu, latki | bez kolizji - bez zmian |
| 6 | gen_mcm / McmSettings / sprawdz_logi | grupy, def, dispatch, B4 | bez kolizji - bez zmian |

### 1. Plotki o dalekich wiesciach ginely przy krukach (poprawione)

**Problem.** p5-kruki (R3-05c) wstrzymuje plotke w karczmie (Rumours.Wars i trwajace oblezenia - `Ravens.NotYet`), dopoki nie doleci kruk: koniec doby zdarzenia + 3 h + odleglosc / 6.25 jedn. na godzine, najwyzej 12 dob. Ksiega wiesci Rumours (sw-zajazdy, potem n20-plotki) trzyma najwyzej `NewsMax = 60` wiesci, najstarsza odpada (`AddNews`: `while (_news.Count > NewsMax) _news.RemoveAt(0)`). Opis MCM RavenNews obiecuje: "Tavern gossip about such news waits for the same raven" - a dla dalekiej karczmy wiesc odpadala z ksiegi wczesniej, niz kruk mogl doleciec.

**Dowod.** Linia doby "Plotki ... | wiesci N (nowe dzis M)" w 8 biegach (r2nowa, r3nowa, n21nowa, n22anowa, r2160, r3160, n21160, n22a160; skrypt `n22f-scalenie-tmp/okno_wiesci.py`, srednie policzone jeszcze raz awk na kopiach w `D:\Backup-Bannerlord\logi-testow\2026-10-20`):
- nowe wiesci: srednio na dobe r2nowa 11.1, r3nowa 9.7, n21nowa 11.2, n22anowa 10.9, r2160 10.3, r3160 12.1, n21160 11.4, n22a160 11.3 (zakres 9.7-12.1; mediana 11-12, najwiecej 23-27); ksiega pelna (60) w 97-156 z 119-161 dob (82-97%);
- zycie wiesci w ksiedze: mediana 5-6 dob (q10 4-5, q90 7-13) - wiec plotka mogla dojsc tylko do karczmy blizej niz ok. 580-730 jedn. mapy (q10: 431-581). Pentos - Winterfell 643 jedn. (kruk 4.4 doby), Qarth - Krolewska Przystan 1062 (7.2 doby) - w Essos daleka wojna w Westeros prawie nigdy nie mogla trafic do plotek (zostawal tylko ogolnik "Folk say ... is at war with ...").
- Przed p5-kruki ta sama wiesc byla w karczmie od razu (te same 5-6 dob). Blad wnosi sama galaz p5-kruki: jej baza `5189c0fe` (WGRANIE 92) ma juz ksiege 60 FIFO (`git show 5189c0fe:Armoury/src/Rumours.cs` linie 54 i 120), koncowka galezi `b40c4cc8` ma jednoczesnie te sama ksiege i `Ravens.NotYet` (linie 54, 120, 1050, 1063), a zadna z 15 galezi fali 1 nie zmienia Rumours.cs (`git diff --stat 5189c0fe..161e099c`). Wlasna recenzja p5-kruki tego nie wylapala.
- Trwajacy bieg z DLL n22 (Armoury-2026-10-20_07-23-31.log, tylko odczyt): "wiesci 60" w kazdej dobie, kruki "sr. 83 h, najdluzej 113 h", "plotki w karczmie wstrzymane do kruka" 39-55 na dobe (jedna proba przy graczu na dobe) - sredni kruk (3.5 doby + koniec doby zdarzenia) zabiera juz ok. 70% zycia wiesci w ksiedze.

**Przyczyna.** Blad galezi p5-kruki (nie scalenia): `Ravens.NotYet` dopisany do istniejacej ksiegi 60 wiesci bez wspolnej miary - okno ksiegi (60 wpisow, ok. 5-6 dob) jest krotsze niz najdluzszy lot kruka (12 dob + doba zdarzenia). Naprawione w drzewie scalenia; p5-kruki nie moze isc nigdzie sama bez tej poprawki (uwaga dopisana w `p5-kruki-CHANGELOG.md`).

**Zmiana (minimalna, pod istniejacym kluczem RavenNews).**
- `Rumours.AddNews`: ponad 60 najstarsza wiesc odpada jak dotad, ale przy wlaczonym RavenNews dopiero, gdy jest starsza niz `Ravens.HoldDays` (= 12 dob lotu + doba zdarzenia = 13, wiesc w wieku 0-13; NotYet liczy zdarzenie na koniec jego dnia, najdluzszy lot konczy sie na poczatku 13. doby), albo gdy ksiega przekroczy `NewsMaxRavens = 300`. Wylaczony RavenNews - 60 jak dotad, co do wpisu.
- `Rumours.Import`: czyta do 300 wiesci (zapis z krukami); przy wylaczonym kluczu od razu zostawia 60 najnowszych (`RemoveRange`, poprawka po recenzji - wczesniej przycinal dopiero pierwszy nowy AddNews, wiec do pierwszej nowej wiesci po wczytaniu N moglo byc ponad 60).
- `Ravens.HoldDays` - nowa wlasciwosc (z tej samej stalej MaxHours co NotYet i kolejka krukow - jedna miara).
- Sufit 300: symulacja nowej reguly na dobowych liczbach (autor: 4 biegi n21nowa, n22anowa, n21160, n22a160 - mediana 155-178; recenzja `n22f-scalenie-tmp/rec/sym.py`: wszystkie 8 biegow) - ksiega po 14. dobie mediana 145-178 wiesci, najwiecej 238 (suma "nowe dzis" z 14 kolejnych dob najwyzej 180-238); wiesci mlodszych niz 13 dob wyrzuconych 0. Nowe wiesci i ich zrodla (bitwy, napady band z imieniem herszta, oblezenia) bez zmian - nic sie nie dubluje, zmienia sie tylko, jak dlugo wiesc zostaje w pamieci.
- Wybor plotek (Rumours.Wars, Build) bez zmian - patrz Ryzyko: poprawka sprawia, ze daleka wiesc po kruku jest DOSTEPNA, a nie, ze zawsze padnie.

**Ryzyko / co sprawdzic.**
- Wybor 2 wiesci wojennych (n20-plotki, bez zmian): `Build` daje kategorii wojen 2 miejsca (`cap = { 2, 1, 1, 1, 1, 2 }`), `Wars` bierze 2 najwyzsze wyniki: 1.2 + (1 - wiek/60) x 1/(1 + d/250) (x1.3 wojna/pokoj); trwajace oblezenie do 300 jedn. 2.2 + (1 - d/300). Swiezosc wazy malo (wiek/60), rozstrzyga bliskosc. Rachunek: bitwa pod Winterfell w Pentos (d 643, od wieku 5) 1.457 (wypowiedzenie wojny x1.3: 1.534); oblezenie KP w Qarthu (d 1062, od wieku 8) 1.365; bliska wiesc d 400 sprzed 13 dob 1.501, d 300 sprzed 12 dob 1.564, d 100 sprzed 13 dob 1.759 - kazda z nich wygrywa z daleka. Daleka wiesc wejdzie do plotek tylko w karczmie, ktora ma w okolicy mniej niz 2 mocniejsze wiesci (i zadnego trwajacego oblezenia do 300 jedn.) - w Essos przy jego wlasnych wojnach rzadko. To cecha wyboru z n20-plotki (karczma mowi najpierw o tym, co blisko), nie nowy blad.
- Dluzsza pamiec takze blisko: okno bliskich wiesci rosnie z ok. 5.5 do 13 dob (ok. 2.4 x wiecej kandydatow) we WSZYSTKICH karczmach - czesciej padnie bliska wiesc sprzed 7-13 dob ("Some weeks ago") zamiast swiezszej dalszej, a te same 2 bliskie wiesci moga sie trzymac do 2 tygodni. Zgodne z zamiarem sw-zajazdy (NewsDays = 60, napisy "Some weeks ago / A month or more ago"; 60 wpisow opisano tam jako straznik zapisu, nie pamiec karczmy), ale to zmiana widoczna takze przy graczu. Z RavenNews wylaczonym - jak WGRANIE 92.
- Okno daleko jest krotsze niz blisko: wiesc widac w karczmie od przylotu kruka do konca 13. doby, gdy ksiega ma ponad 60 wpisow (Qarth - KP od 8. doby, czyli ok. 6 dob; najdluzszy lot 12 dob - tylko 13. doba). Ksiega ponizej 60 - nic nie odpada az do NewsDays 60, jak dotad.
- Napis wieku: plotka z Qarthu o KP przychodzi w wieku 8 dob lub wiecej, wiec zawsze "Some weeks ago" (do 6 dob "A few days ago", od 7 "Some weeks ago" - Rumours.cs `when`); bez zmian w tej poprawce.
- Licznik "plotki w karczmie wstrzymane do kruka" (Ravens.NotYet liczy kazda wstrzymana wiesc przy kazdej budowie plotek, w autotescie raz na dobe w probie przy graczu) wzrosnie, bo w ksiedze jest wiecej wiesci (dzis 39-55 przy 60) - to nie zmiana znaczenia.
- Wieksza ksiega: Rumours.Wars przeglada do ok. 240 wpisow przy kazdym sluchaniu plotek (nie co klatke; dotad 60) i SiegeNotYet dla oblezen do 300 jedn. - koszt pomijalny wobec reszty budowy plotek ("budowa srednio X ms" w linii doby). Zapis arm_rumours: ok. 240 x ok. 100 znakow = ok. 25 KB wiecej, SaveText.Sync (paczka 161) bez limitu 32 KB; straznik SaveMaxChars (Import odrzuca ponad 1.2 mln znakow) bez zmian.
- Powrot do DLL WGRANIA 92 z zapisem z wieksza ksiega: stara DLL czyta 60 PIERWSZYCH (najstarszych) wiesci - najnowsze z zapisu by przepadly (tylko plotki, raz).
- Kolejnosc ksiegi: AddNews dopisuje na koniec, eksport/import zachowuja kolejnosc - _news[0] to zawsze najstarsza (RemoveRange(0, ...) w Import zdejmuje najstarsze).
- Watki: tylko watek glowny (zdarzenia kampanii, rozmowa w karczmie, SyncData), bez nowych alokacji w petli.

**Test w grze.**
- Autotest 120 dob z RavenNews: linia "Plotki (PL): dzien N ... | wiesci N" - N powyzej 60 (ok. 130-240 po 14. dobie), nigdy ponad 300. To potwierdza TYLKO mechanizm (ksiega trzyma wiesc) - autotest nie slucha plotek ("sluchane 0 (zajazdy 0, karczmy miast 0)" we wszystkich biegach), a proba przy graczu ("opowiesci dzien N ... proba przy graczu") podaje przyklad tylko o bandzie lub kryjowce, wiec samej plotki o dalekiej wiesci w logu nie widac. "Kruki (R3-05c)": "plotki w karczmie wstrzymane do kruka" wyzej niz dotad (patrz Ryzyko).
- Recznie (Jeff, na mapie): karczma daleko od wojny, w spokojnej okolicy (np. Pentos, gdy w Essos cicho), kilka dni po oblezeniu lub bitwie w Westeros - plotka o nim pada dopiero po kruku z ta wiescia (czat "A raven from ..."), nigdy przed. Napis wedlug wieku wiesci: do 6 dob "A few days ago ...", od 7 "Some weeks ago ..." (z Qarthu o KP zawsze "Some weeks ago"). Gdy w okolicy sa 2 mocniejsze wiesci (blizsze albo trwajace oblezenie do 300 jedn.), dalekiej nie bedzie - to wybor 2 wiesci wojennych z n20-plotki, nie blad poprawki.
- Z RavenNews wylaczonym: N najwyzej 60, takze zaraz po wczytaniu zapisu z krukami - jak w WGRANIU 92.

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

- Wynik scalen 3 galezi = dokladnie zmiany galezi (porownanie linii dodanych/usunietych baza 5189c0fe -> galaz wobec rodzic n22 -> scalenie: 1308/1308, 626/626, 976/976); `tools/scalanie/metody.py` (z repo glownego - w drzewie n22 katalogu tools/scalanie nie ma) - kazda dodana linia w tej samej metodzie (jeden falszywy alarm: pole `_p5RavensReset` miedzy metodami).
- Reset: Ravens - osobne pole (jak p5-urodzaj), GuildMend + FieldArmourer - przez SmithHours.Reset w konstruktorze; SmithHours.Reset wolany tylko tam.
- SyncData: nowy klucz `arm_ravens` unikalny (grep wszystkich kluczy 4 modow - jedyne "duplikaty" to pary zapis/odczyt tego samego klucza). GuildMend bez zapisu (jak SmithHours).
- Harmony: Ravens wlasne id `com.jeff.armoury.ravens`, latki raz na proces w starcie sesji; CampaignInformationManager.NewLogEntryAdded - zadna inna DLL w Modules (grep metadanych) ani nasz kod jej nie latuje; BK SiegeConsequences - nasz transpiler (Ravens) obok prefiksu/postfiksu CitySack L6 (Harmony laczy); Diplomacy ApplyInternal/NotifyExpired i ROT patch7 - inne metody niz BkDipGold (AssessCosts) i RealisticCaptivity CleanBreak (TakePrisonerAction.ApplyInternal).
- p5-naprawy bez latek Harmony; GuildMend/FieldArmourer tylko na watku glownym (jedyne kody na puli watkow - F5Koszt, MapRoads, MapSideRoads - nie dotykaja napraw).

### 5. gen_mcm / McmSettings / sprawdz_logi - bez kolizji

- `python tools/gen_mcm.py` - McmSettings bez zmian (zgodne z Settings.cs), brak powtorzonych nazw w MCM; nowe klucze: RavenNews (grupa plotek "A night's rest", jak InnGossip*), CampArmourer/CampQuartermasterSteward ("The men's gear"), MendHoursByGuild/AutoMendHalfGuild/MendBulkEveryOrder ("Time at the forge"), Army175ValeReachKnights/Army175FreyHorseArchers ("Armies of the realms (175)").
- `tools/scalanie/sprawdz_narzedzie.py tools/sprawdz_logi.py` (skrypt z repo glownego, uruchomiony na sprawdz_logi.py drzewa; powtorzone po poprawkach recenzji): dispatch poza main 0, zle wstawki 0, zdublowane def 0, zdublowane grupy 0, kompilacja OK. Jedyna zmiana narzedzia z 3 galezi (R2_B4 z dopiskiem R3-08g) - sprawdzona na n22anowa: 120/120 linii B4 dopasowanych. Linia "wiesci N" w sprawdz_logi tylko jako INFO (bez progu 60).

### Otwarte (bez zmian, do decyzji/biegu)
- GuildMend `_carry` (dlug cechu za zlecenie gracza ponad wolne rece) i lawa platnerza w taborze nie sa w zapisie - po wczytaniu zaleglosc cechu znika (godziny raz "za darmo"); opis galezi mowi "stan tylko dzienny". Do rozwazenia przy nastepnym przegladzie napraw.
- Brak biegu z trzema galeziami (n22a = bez naprawy/jednostki/kruki): linie "Naprawy wg cechow (R3-02)", "Platnerz w taborze (R3-04c)", "Kruki (R3-05c)", "Mends: konni lucznicy Freyow (R3-08f)" do obejrzenia w pierwszym biegu n22.
- Plotka o dalekiej wiesci po kruku nie ma sladu w logu autotestu (nikt nie slucha plotek, proba przy graczu loguje przyklad tylko o bandzie/kryjowce). Jesli ma byc mierzalna: licznik w linii "opowiesci dzien N ... proba przy graczu", np. "wojny w probie N, po kruku M, najdalsza d" - osobna zmiana do uzgodnienia (CLAUDE.md 8.3), nie dodana.

### Recenzja 20.10 (2 recenzje: diagnoza, technika) - co zrobione
Obie recenzje: poprawka ksiegi poprawna i usuwa przyczyne (regula AddNews zgodna z NotYet co do godziny, sufit 300 z zapasem, scalenia wierne galeziom, build 5 modow 0, gen_mcm bez zmian, sprawdz_narzedzie 0/0/0/0, WorkshopLaw i CrashScribe JazdaGear/JazdaSkills bez kolizji). Uwagi:
- [wazne, diagnoza] Obiecany efekt dla gracza ("kiedy kruk doleci, plotka o tym jest", "takze w Essos") niewiarygodny i niesprawdzalny w autotescie - PRAWDA. Sprawdzone w kodzie: `Build` cap wojen 2 (Rumours.cs `var cap = new[] { 2, 1, 1, 1, 1, 2 }`), `Wars` 2 najwyzsze wyniki z bliskoscia 1/(1+d/250), trwajace oblezenie 2.2+; w kazdym biegu "sluchane 0"; napis od wieku 7 "Some weeks ago". Poprawione opisy: "Dla Jeffa", Ryzyko (wybor 2 wiesci, dluzsza pamiec takze blisko, krotsze okno daleko, napis wieku, licznik wstrzymanych rosnie), "Test w grze" (autotest potwierdza tylko mechanizm; test reczny w spokojnej karczmie). Kodu wyboru plotek nie zmieniam: karczma mowi najpierw o tym, co blisko (n20-plotki) - poprawka ma tylko przestac gubic wiesc. Licznik w probie - w Otwartych (osobna zmiana).
- [drobne, obie] Zle przypisana przyczyna (nie scalenie, tylko galaz p5-kruki) - PRAWDA (`git show 5189c0fe` / `b40c4cc8` Rumours.cs linie 54 i 120, merge-base `b40c4cc8 5189c0fe` = `5189c0fe`, `git diff --stat 5189c0fe..161e099c` bez Rumours.cs). Poprawione w Przyczynie i Dowodzie; uwaga w `p5-kruki-CHANGELOG.md` (galaz nie idzie sama bez n22f).
- [drobne, diagnoza] Liczby: 9.7-12.1 nowych wiesci na dobe w 8 biegach (bylo 10.9-11.4 - pasowalo do 4) - PRAWDA, przeliczone awk (lista w Dowodzie), ksiega pelna 82-97%. Komentarz "HoldDays, 14 dob" - PRAWDA, poprawiony na "HoldDays = 13 dob, wiesc w wieku 0-13" (Ravens.cs: Ceiling(288/24) + 1 = 13).
- [drobne, diagnoza] "Z RavenNews wylaczonym najwyzej 60" nieprawda zaraz po wczytaniu zapisu z krukami (Import czytal do 300, przycinal dopiero AddNews) - PRAWDA. Poprawione kodem, nie opisem (1 linia w `Rumours.Import`: przy wylaczonym kluczu `RemoveRange` do 60 najnowszych), bo zdanie "wylaczony = jak WGRANIE 92" ma byc prawdziwe zawsze; build 5 modow kod 0, dekompilacja Armoury.dll: `_news.RemoveRange(0, _news.Count - 60)` w Import.
- [drobne, technika] Oczekiwanie "plotka pojawia sie po kruku, takze w Essos" nie wynika z poprawki - PRAWDA (to samo co pierwsza uwaga), poprawione w "Test w grze" i Ryzyku z rachunkiem wynikow. Komentarz HoldDays - jw.
- Uwaga techniki: `sprawdz_narzedzie.py` i `metody.py` sa w repo glownym, nie w drzewie - dopisane w pkt 4 i 5.
