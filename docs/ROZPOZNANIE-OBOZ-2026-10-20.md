# Rozpoznanie: obóz armii (20.10.2026)

Dla Jeffa. Odpowiedź na listę z `docs/ZADANIA-OBOZ-NOC.md` (7 punktów Jeffa i 6 pomysłów Claude).
To jest tylko rozpoznanie: w kodzie, w repo i w folderze gry nic nie zmieniłem, gry nie uruchamiałem
(grałeś, log Armoury z 13:28), żadnego moda nie pobierałem.

## Streszczenie

1. W grze (i w ROT) noc trwa od 22:00 do 2:00. Tylko wtedy działa nocna kara prędkości (u nas -0,5) i nocny wzrok.
   Dziś świat obozuje od 0:00 do 6:00, więc każda kolumna jedzie co noc 2 godziny po ciemku z karą.
2. Przestawienie obozu jest tanie i pewne: to dwa ustawienia i trzy drobne poprawki. Proponuję te same 6 godzin snu, tylko
   przesunięte: kolumny stają przed 22:00 i ruszają po 2:00. Wtedy droga na dobę się nie zmienia, a ekonomia, karawany
   i wozy wsi zostają przy dzisiejszej kalibracji.
3. Na wejście do obozu i chodzenie po nim jest świeży mod na 1.4.8, Practice Drills (Nexus 14005). Robi prawie wszystko z punktów 2, 4 i 7,
   a autor pozwala brać jego kod i zasoby bez pytania. Nie radzę go instalować obok naszego obozu (dwa obozy naraz, podwójne morale).
   Lepiej wziąć go jako wzór.
4. Gra ma wszystkie klocki. ROT ma gotowy namiot żołnierski z posłaniami, namiot oficerski i namiot wodza, Native ma posłania, ogniska,
   kotły i wozy, a SandBox ma nieużywany typ misji „Camp”. Własna scena obozu jest więc realna, ale to duża praca: pierwsza własna misja w naszych modach.
5. Masz 30 kg udźwigu, bo to 10 kg bazy + 20 kg za Ciebie. Koń pod siodłem daje 0, koń juczny daje 100 kg. Juki na wierzchowcu to mała zmiana.
6. Trening z wyboru gracza da się zrobić szybko, jako opcję w naszym menu obozu, na bazie naszej Musztry.
7. Namiotów, wozów i juków jako towaru nie ma ani w grze, ani w żadnym modzie. Wozy 3D są tylko do scen i jako nieruchoma dekoracja mapy.
   Żadna drużyna na mapie nie jeździ wozem.
8. Nierealne w rozsądnym czasie są dwie rzeczy: tabor wozów jako osobna drużyna na mapie, napadana przez bandy, i spacer po obozie
   zrobiony „jak we wsi” (gra wymaga do tego osady).

## Skąd to wiem

Trzy niezależne rozpoznania:
- **Mody**: Nexus (publiczne API, strony uprawnień w przeglądarce wbudowanej, bez logowania) i Steam (wyszukiwarka i publiczne API).
- **Gra**: dekompilacja DLL gry, ROT, BK, NavalDLC, RB oraz przegląd 7097 prefabów ze wszystkich modułów.
- **Nasze mody**: drzewo kodu obecnej wersji (WGRANIE 95), Twoje ustawienia i Twoje logi z 20.10.

Skróty w dowodach:
- **G** = `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
- **N24** = `...\scratchpad\zb\n24-calosc` (kod WGRANIA 95)
- **TMP** = `...\scratchpad\zb\oboz-rozpoznanie-tmp` (dekompilacje, lista prefabów `prefabs_all.tsv`, surowe dane modów w `dane\`)
- **Ustawienia** = `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json` (tylko czytane)

U Ciebie są: ROT v8.1.8 (`G\Modules\ROT-Core\SubModule.xml`) i BK v1.9.34.0 (`G\Modules\BannerKings.Redux\SubModule.xml`).

## Ocena w skrócie

| Punkt | Co to da | Praca | Ryzyko | Ocena |
|---|---|---|---|---|
| 1. Obóz od 22:00 | nikt nie jedzie w nocy z karą | mała | małe | realne |
| 2. Wejście do obozu, chodzenie | spacer po własnym obozie | duża | średnie/duże | realne etapami |
| 3. Trening z wyboru | „Ćwicz ludzi” w obozie | mała/średnia | małe | realne |
| 4. Wygląd według drużyny | posłanie, namioty, konie | średnia (mapa), duża (scena) | średnie | częściowo |
| 5. Udźwig: juki na koniu | więcej kg bez kary | mała | małe | realne |
| 6. Przedmioty obozowe, wozy 3D | namiot, wóz, juki do kupienia | średnia (towary), duża (wóz na mapie) | średnie | częściowo |
| 7. Miasteczko obozowe | sklep markietanów, większy obóz | średnia (sklep), duża (całość) | średnie | realne etapami |
| Pomysł: czas zwijania | duża armia rusza później | mała | małe | realne |
| Pomysł: ogniska, jedzenie, morale | premia za noc w obozie | mała/średnia | małe | realne |
| Pomysł: furażerzy | jedzenie z okolicy kosztem wsi | średnia | średnie (ekonomia) | częściowo |
| Pomysł: warsztat obozowy | cyrulik, płatnerz | mała/średnia | małe | realne |
| Pomysł: wozy taborowe | więcej udźwigu, wolniej, cel napadów | duża | duże | częściowo (bez napadów) |
| Pomysł: namiot wodza z herbem | z daleka widać, kto obozuje | mała/średnia | średnie (mapa) | realne, połowa już jest |

---

## 1. Godzina obozu 22:00

**Co to da w grze.** Twoja kolumna i kolumny całego świata nie jadą po ciemku z nocną karą. Obóz pokrywa się z nocą gry.

**Jak jest dziś.**
- Noc w grze trwa od 22:00 do 1:59. ROT ma te same godziny, a nasz kalendarz ich nie zmienia.
- Nocą gra zabiera 25% prędkości. U nas zamiast tego jest stała kara -0,5 (masz ustawione 0,5). Wzrok w grze spada nocą z 12 do 6.
- Nasz moduł wzroku ma własną noc, od 21:00 do 4:59: nocą wzrok x0,65, w dzień x1,15.
- Świat obozuje od 0:00 do 6:00. Każda kolumna jedzie więc co noc 2 godziny (22-24) z karą prędkości i 3 godziny (21-24) z nocnym wzrokiem.
- Zmrok na mapie zaczyna się od 20:00, a jasno robi się między 2:00 a 4:00. BetterTime (masz go włączonego) to tylko przyciski czasu i godzin nie zmienia.

**Ważny szczegół: gra sprawdza godzinę raz na godzinę.** Robi to w minucie losowanej przy każdym wczytaniu (dziś u Ciebie o :42).
Ustawienie „start o 22” oznacza więc, że kolumny staną dopiero przy pierwszym sprawdzeniu po 22:00, czyli między 22:00 a 23:00.
To do godziny jazdy po ciemku z karą. Żeby nikt nie jechał w nocy, obóz musi się zaczynać przy sprawdzeniu w godzinie 21.

**Gotowe mody.** Żaden mod nie przesuwa nocy ani nocnej kary. Practice Drills (niżej) ma ten sam pomysł co Ty: „pełna noc w obozie”
liczy się, gdy obóz stoi przed 22:00. Daje za to +10 morale.

**Moja propozycja (decyduję sam, zgodnie z Twoją zasadą).** Obóz świata w ustawieniach: **start 21, koniec 3**.
- W praktyce kolumny stają między 21:00 a 22:00, a ruszają między 3:00 a 4:00, po końcu nocy gry (2:00), kiedy już się rozjaśnia.
- Sen trwa 6 godzin jak dziś, więc rachunek snu i długu snu się nie zmienia.
- Marsz trwa 18 godzin na dobę jak dziś, a nawet ok. 1% dalej, bo znika jazda po ciemku. Kalibracja ekonomii, karawany, wozy wsi i budżety zostają bez zmian.
- Świt (pobudka, karczma, doba armii) przesuwa się z 6:00 na 3:00-4:00.

Odrzucam wariant 22-6, czyli 8 godzin obozu. Świat maszerowałby wtedy 16 godzin zamiast 18 (11% krótsza droga na dobę),
a to przestawia całą ekonomię i trzeba by ją kalibrować od nowa.
Da się też zrobić obóz co do minuty o 22:00, ale tylko dla Twojej drużyny. Wtedy Ty jedziesz do 22:00, a świat stoi od 21:xx,
czyli byłyby dwie zasady na jedną noc. Nie polecam.

**Co trzeba poprawić oprócz dwóch liczb.**
- Obóz „spłaty długu snu” u lordów AI zaczyna się na sztywno o 20:00 i ma dawać 10 godzin. Przy pobudce o 3:00 dałby 7 godzin,
  a potrzeba 9. Trzeba go liczyć od końca obozu, a nie od stałej godziny.
- Noc wzroku (21:00-4:59) trzeba zrównać z nocą gry (22:00-2:00). Jedna noc dla wszystkiego.
- Opisy w ustawieniach MCM mówią „0 and 6 = midnight to six”.
- Drobiazg: noc snu w menu jest liczona od 21:00 do świtu. Przy świcie o 3:00 to się zgadza samo.

**Co ta zmiana ruszy.** Wszystkie moduły pytają o ten sam przełącznik: sen gracza i pytanie „Night falls”, księga snu AI,
marsz nocny i dług snu, dzień odpoczynku armii, karczmy, wyznaczanie drogi, pomiary świata.
Noc przez północ jest już obsłużona (opis w kodzie: „noc 22-6 przez polnoc = jeden numer”).
W modułach wozów wsi i handlu nie znalazłem zależności od godzin obozu. Marsz forsowny działa na tych samych godzinach i dalej pomija pytanie o nocleg.

**Pułapka.** Twoje godziny są zapisane w ustawieniach (CampStartHour 0, CampEndHour 6), więc zmiana w kodzie sama nic nie da.
Trzeba poprawić plik przy zamkniętej grze (z kopią) albo przestawić w MCM (bez restartu). Zmieniać w dzień (między 4:00 a 20:00),
nie w środku nocy obozu, bo wtedy może się pomylić rachunek tej jednej nocy.

**Praca:** mała (tor S). **Ryzyko:** małe, ale zmiana dotyczy całego świata, więc potrzebny jest autotest na 2-3 doby
(świt, pytanie o nocleg, dzień odpoczynku armii, karczma, dług snu AI).

**Ocena: realne.** To tylko przesunięcie istniejącego przełącznika plus trzy poprawki, a długość snu i marszu się nie zmienia.

Dowody:
- Noc gry: dekompilacja CampaignTime (TMP\CampaignTime.cs l.80-102, IsDayTime/IsNightTime); DefaultCampaignTimeModel `SunRise => 2`, `SunSet => 22`;
  ROT `G\Modules\ROT-Core\bin\Win64_Shipping_Client\ROT.dll` ROTCampaignTimeModel (te same 2 i 22); N24\Armoury\src\Calendar.cs:42-43.
- Kara prędkości: TMP\DefaultPartySpeed.cs l.65 („MovingAtNightEffect = -0.25f”) i l.319-327; N24\Armoury\src\TerrainEase.cs:147-150, Settings.cs:521.
- Wzrok: TMP\DefaultMapVis.cs l.21-28 (12 / 6); N24\Armoury\src\SightRange.cs:35 (`h >= 21 || h <= 4`), Settings.cs:1754-1756.
- Światło: TMP\DefaultMapWeather.cs l.358-380, 493-500.
- Twoje ustawienia: Armoury.json — CampStartHour 0, CampEndHour 6, SleepHoursNeeded 6.0, NightSpeedPenalty 0.5, NightSightFactor 0.65, DaySightFactor 1.15.
- Przełącznik: N24\Armoury\src\NightRest.cs:365-382 (CampStart, CampEnd, PlayerDawn, InCamp przez północ); McmSettings.cs:4755-4761 (zakres 0-23, bez restartu, opis „0 and 6 = midnight to six”).
- Spłata długu: N24\Armoury\src\NightMarch.cs:61 (`DebtCampHour = 20`, „10 h pelnej stawki (1 h zapasu nad 9 h)”) i l.219-224.
- Droga na dobę: N24\Armoury\src\Wayfinder.cs:92-108 (godziny obozu się pomija, a ciemność 22-02 kosztuje ułamek godziny).
- Sprawdzanie raz na godzinę: N24\Armoury\src\NightClock.cs:9-20 (faza ustalana przy wczytaniu); log `G\Modules\Armoury\Armoury-2026-10-20_13-28-19.log` l.550 („termin 10:42, zegar 10:43”).
- Noc przez północ: NightClock.cs:125-129. Noc snu w menu: NightRest.cs:180.
- Practice Drills: opis na Nexusie „pitched by ten in the evening, nobody moving” (TMP\dane\mod_14005.json).

---

## 2. Wejście do obozu i chodzenie po nim

**Co to da w grze.** W menu obozu wybierasz „Wejdź do obozu” i chodzisz po scenie w miejscu postoju. Są tam Twoje namioty, ogniska,
konie i wozy. Ludzie siedzą, jedzą, rozmawiają, nocą śpią. Im większa armia, tym większy obóz.

**Gotowe mody.**
- **Practice Drills Visiting Camp and Riding with Troop Column** — https://www.nexusmods.com/mountandblade2bannerlord/mods/14005
  (autor nilliks, wersja 1.3 z 18.10.2026, wymaga 1.4.8 i Harmony).
  - Po kliknięciu we własną drużynę na mapie: przemarsz kolumny, rozbicie obozu (1 h, na mapie pojawia się kilka namiotów), spacer po obozie,
    bitwa ćwiczebna, czekanie do rana, zwinięcie obozu (0,5 h).
  - Scena obozu: namioty Twojej kultury (ok. 1 na 6 ludzi), namiot dowódcy, ogniska z siedzącymi ludźmi, wozy z prawdziwymi zapasami,
    konie na palikach, zagroda dla bydła, kuchnia i stół płatnika (od 30 ludzi), plac ćwiczeń, warty, ranni przy cyruliku, jeńcy przy palach,
    sztandar klanu, noc ze śpiącymi, deszcz i śnieg, dźwięki. Obóz rośnie z armią.
  - Uprawnienia: wolno zmieniać i używać zasobów bez pytania i bez podawania autora.
  - Zgodność: jeden gracz pisze, że działa mu z ROT 8.1 na 1.4.8 (Ty masz ROT 8.1.8). Kłóci się z modem Talk on the Map, którego nie masz.
  - Ryzyka: mod ma 5 dni, 3 wersje w tydzień i 182 pobrania. Ma własny obóz na mapie, własne +10 morale i blokuje marsz przy rozbitym obozie,
    czyli dublowałby nasz obóz (sen 0-6, namioty, bitwa w obozie). Nie wiem, jak stawia namioty na mapie (nasza zasada: nie dotykać wizerunków mapy co klatkę),
    i nie wiem, czy jego wozy i namioty to modele z gry. Archiwum nie jest jeszcze opisane na Nexusie, a moda nie pobierałem.
  - Uwaga: jego bitwa ćwiczebna niczego nie zapisuje (zero doświadczenia).
- **Less Menus More Immersion 1.7.0 beta** — https://www.nexusmods.com/mountandblade2bannerlord/mods/6717: obóz pod Ctrl+T,
  po którym się chodzi, a czas stoi. Można postawić wino, sparing, musztra. Uprawnienia zamknięte, więc tylko jako pomysł.
- **Camp Menu** (Steam 3627110143), oznaczony na 1.3.15: wejście na scenę bitwy bez bitwy, czekanie w namiocie.
  Autor pisze, że nie ma własnych zasobów, tylko kod, czyli wszystko składa z modeli gry. Brak licencji, więc tylko jako pomysł.
- Imprisoned (11814, obóz porywacza), Bandit Plus (12030, obozy band), Council of War (11677, scena przed szturmem). Wszystkie mają zamknięte uprawnienia.

**Co ma gra.**
- Spaceru „jak we wsi” nie da się użyć. Misja wsi, miasta i zamku wymaga spotkania z osadą i jej lokacji, a w polu ich nie ma.
  Nasz „Take a walk” działa tylko po wjeździe przez menu wsi. Z mapy gra by się wywróciła, co mamy zapisane w kodzie.
- SandBox ma za to gotowy, nigdzie nieużywany typ misji **„Camp”** z kompletem widoków: kamera, menu Esc, pasek zdrowia, wyjście.
  Sama gotowa metoda wymaga bitwy, więc trzeba ją otworzyć z własnym zestawem zachowań, a widoki „Camp” gra dołoży sama.
- Scena: ta, którą gra wzięłaby do bitwy w tym miejscu mapy. Pora dnia i pogoda idą z mapy, więc o 23:00 jest ciemno i ogniska świecą.
- Stawianie namiotów na scenie mamy sprawdzone. Nasza bitwa w obozie (CampScene) 20.10 postawiła 10 elementów obozu.
  Ludzi stawia się jak w bitwie, a konie jak we wsiach (tak gra robi z końmi w wioskach).

**Jak to zrobimy we własnym modzie.** Opcja „Walk through the camp” w naszym menu obozu. To zwykłe menu, nie menu oczekiwania,
więc pułapka z przełączaniem menu go nie dotyczy. Etapy:
1. Scena z miejsca postoju, namioty i ogniska według liczby ludzi, wejście i wyjście. Najpierw trzeba sprawdzić powrót z misji do menu bez spotkania.
2. Ludzie siedzą, jedzą, śpią (gra ma gotowe punkty: siedzenie na ziemi, jedzenie, rozmowa, sen, drzemka warty, ćwiczenia) i konie na postoju.
3. Życie obozu: kuchnia, płatnik, ranni przy cyruliku, kramy markietanów (punkt 7).

Musi być sufit postaci dla płynności, np. 1 postać na 5-10 ludzi i najwyżej ok. 60-100.

Practice Drills mógłby oszczędzić noc prób. Jeśli go pobierzesz (Nexus wymaga Twojego logowania, ja tego nie zrobię), rozbiorę jego kod
i zobaczę, jak otwiera scenę i wraca na mapę. Uprawnienia na to pozwalają. Nie instalujemy go jako drugiego moda.

**Praca:** duża (tor L), raczej 2-3 noce. **Ryzyko:** średnie do dużego. To pierwsza własna misja w naszych modach:
wywrotki przy wejściu i wyjściu, płynność przy wielu postaciach, sceny ROT_* z wymuszoną porą 22:00 albo 12:00 (ROT tak robi).

**Ocena: realne etapami.** Wszystkie klocki są w grze, a Practice Drills pokazuje, że da się to zrobić na 1.4.8 z ROT.
Nie da się tego zrobić drogą „jak we wsi”, tylko własną misją.

Dowody:
- Practice Drills: TMP\dane\mod_14005.json (opis, wymagania „Bannerlord 1.4.8”, „Bannerlord.Harmony”, pliki 1.3 / 1.2 / 1.1.0); uprawnienia i komentarze odczytane na stronie moda (sekcja Permissions, zakładka posts).
- Twoje moduły: lista `G\Modules` (brak Talk on the Map, LMMI i Practice Drills).
- Misja „Camp”: TMP\SandBoxMissions.cs l.1487-1532 (OpenCampMission), l.144 (wymaga bitwy drużyny); TMP\SandBoxMissionViews.cs l.689-709 (`[ViewMethod("Camp")]`);
  grep OpenCampMission w SandBox.dll, SandBox.View.dll, TaleWorlds.CampaignSystem.dll, StoryMode.dll: tylko definicje, brak wywołań.
- Misja wsi: TMP\SandBoxMissions.cs l.564-622 (27 zachowań); SandBox.dll MissionAgentHandler l.139 (wymaga spotkania i lokacji osady); N24\Armoury\src\SideVillageHome.cs:42-46, 448-477; N24\docs\noc-1920\r4-dom-CHANGELOG.md:66, 97-100.
- Scena z mapy: CampaignSystem.dll DefaultSceneModel:26 (GetBattleSceneForMapPatch), Helpers.MenuHelper:223.
- Stawianie elementów: N24\Armoury\src\CampScene.cs:181-200; log `G\Modules\Armoury\Armoury-2026-10-20_10-24-16.log` [11:18:21] „CampScene: postawiono 10 elementow obozu na polu bitwy”.
- Ludzie i konie: TMP\Mission.cs l.4083 (SpawnAgent), l.4403 (SpawnMonster); SandBox.dll SandBoxHelpers.SpawnHorses.
- Menu obozu: N24\Armoury\src\NightRest.cs:2175-2199 (arm_camp_wait).
- Pora w scenach ROT: ROT.dll ROT.HarmonyPatches.TimeLock.MissionPatch.

---

## 3. Trening wojska z wyboru gracza

**Co to da w grze.** W obozie wybierasz „Ćwicz ludzi”. Zamiast części odpoczynku ludzie mają musztrę: dostają więcej doświadczenia,
płacą za to snem (dług snu) i jedzeniem, a z małą szansą ktoś się zrani.

**Gotowe mody.**
- **Training Battles** — https://www.nexusmods.com/mountandblade2bannerlord/mods/12277 (kod: https://github.com/TraxData313/TrainingBattlesMod).
  Klawisz G na mapie dzieli armię na dwie połowy i robi bitwę ćwiczebną, po której ludzie zachowują część doświadczenia.
  Licencja public domain (Unlicense), więc wolno wziąć wszystko. Wersja 1.4.0, na Steamie z oznaczeniem 1.4.7.
- FieldCam Train-Forage-Ambush (11748): bierny trening w obozie na mapie. Uprawnienia zamknięte.
- Garrison Drills (11449), Troop Training Expanded (9148): trening w mieście lub na arenie. Uprawnienia zamknięte.
- Practice Drills: bitwa ćwiczebna bez doświadczenia, więc to nie trening.
- Zgodności żadnego z nich z BK i ROT nie sprawdzałem.

**Co ma gra i co mamy my.**
- Gra daje samoczynny trening tylko lordom AI spoza Twojego rodu. Twoja drużyna dostaje doświadczenie wyłącznie z perków.
  U Ciebie działa model treningu BK, z tym samym warunkiem.
- Nasza Musztra daje doświadczenie wszystkim, także Tobie, według jednego wzoru: postój x1,5, marsz x0,9,
  a dzień z długiem snu o świcie to dzień bez ćwiczeń.
- Ćwiczenia na arenie gry wymagają sceny areny i osady, więc do obozu się nie przeniosą.

**Jak to zrobimy we własnym modzie.** Opcja „Drill the men” w naszym menu obozu, z paskiem czasu jak przy śnie.
Godziny musztry podnoszą mnożnik Musztry tego dnia, kosztują sen i trochę jedzenia. Nie mogą się liczyć drugi raz razem z premią za postój.
Żeby była jedna zasada dla wszystkich, lordowie AI też mogą ćwiczyć w obozie według prostego warunku (np. stoją w miejscu i nie mają długu snu).
Ustalę to przy kodzie. Później, po scenie obozu, można dodać bitwę ćwiczebną z doświadczeniem na wzór Training Battles.

**Praca:** mała do średniej (tor S/M). **Ryzyko:** małe (dotyka Musztry i rachunku snu).

**Ocena: realne.** Mamy gotowy wzór doświadczenia i gotowe menu obozu.

Dowody:
- Model gry: TMP\DefaultPartyTraining.cs l.21-29; BannerKings.dll BKPartyTrainningModel l.41-45; log `Armoury-2026-10-20_13-28-19.log` („czynny BannerKings.Models.Vanilla.BKPartyTrainningModel”).
- Musztra: N24\Armoury\src\Drill.cs:3-30, ArmsDrill.cs:300-337; log `Armoury-2026-10-20_10-24-16.log` [10:25:28] „Musztra: start ... (Drill Law) TAK”.
- Arena: SandBox.dll ArenaPracticeFightMissionController l.88-131.
- BetterEconomy ma osobną łatkę treningu (BetterEconomy.Patches.PartyTrainingPatch). Jej nie czytałem.
- Mody: TMP\dane\mod_12277.json, mod_11748.json; licencja Training Battles z GitHuba (spdx Unlicense).

---

## 4. Wygląd obozu według drużyny

**Co to da w grze.** Sam jesteś? Posłanie przy ognisku i Twój koń. Co kilku ludzi namiot z posłaniami, konie na postoju, przy wodzu namiot dowódcy.
Na mapie: mała drużyna bez namiotu, większa z namiotem, armia jako duży obóz.

**Gotowe mody.** Practice Drills ma prawie dokładnie to: ok. 1 namiot na 6 ludzi, namiot dowódcy, posłania w namiotach,
konie na palikach, kuchnia i płatnik od 30 ludzi oraz suwaki ilości. Wariantu „sam gracz = tylko posłanie, bez namiotu” nie ma nigdzie.

**Co ma gra.**
- **ROT ma gotowe moduły** w sam raz do „co X ludzi namiot”:
  - Tent_Small: namiot i 2 posłania;
  - Tent_Small_Campfire: to samo plus ognisko z dymem, ławy, kłody;
  - Tent_Officer i Tent_Officer_Hangout: łóżka, stoły, jedzenie, stojaki na zbroje;
  - Tent_Leader: namiot wodza ze stołem, księgami, skrzynią i stojakiem;
  - małe obozy camp_prefab_a-d;
  - cały obóz oblężniczy Siege_Camp (225 namiotów, 450 posłań, 39 ognisk), za ciężki na zwykły obóz.
- **Native:** posłania na ziemi bed_ground_a..j, słomiane prycze, namioty kultur (vlandia, aserai, khuzait, battania, sturgia), ogniska, kotły, koryto.
- **Koni przywiązanych do palika nie ma jako gotowego elementu.** Jest nieruchomy koń i animowane konie, muły i wielbłądy ROT.
  Najlepiej postawić prawdziwe konie Twojej drużyny, tak jak gra stawia je we wsiach.
- **Na mapie:** dziś jest jeden namiot z chorągwią. Są też gotowe siatki dużego obozu w 5 rozmiarach (map_icon_siege_camp_1..5).
  Siatki posłania w skali mapy nie ma.

**Jak to zrobimy we własnym modzie.**
- **W scenie** (po punkcie 2): do kilku ludzi posłania przy ognisku i Twój koń; potem 1 namiot żołnierski na ok. 6 ludzi; namiot oficerski na większy oddział;
  namiot wodza dla dowódcy; konie w proporcji (np. 1 postawiony koń na 5-10 koni drużyny), z sufitem.
- **Na mapie:** mała drużyna bez namiotu (zostaje figurka), większa z namiotem, armia z siatką dużego obozu.
  Zmiana tylko przy rozbiciu i zwinięciu, nigdy co klatkę.

**Praca:** na mapie mała do średniej; w scenie duża (zależy od punktu 2). **Ryzyko:** na mapie średnie (zasada „nie co klatkę”), w scenie płynność.

**Ocena: częściowo realne.** W scenie całość jest realna. Na mapie „samo posłanie” się nie da, bo nie ma takiej siatki, więc zostaje figurka bez namiotu.

Dowody:
- ROT: `G\Modules\ROT-Map\Prefabs\Swan_Siege.xml` l.706-2919 (moduły Tent_*), l.2920-28073 (Siege_Camp); locations.xml l.2-464, 465-1909, 1910-2665 (camp_prefab_a, c, d), l.4480-5444 (tent_prefab_a z krzesłami do siedzenia); scena ROT_lannisport używa Siege_Camp. Zliczenia skryptem TMP\children.py.
- Native: `G\Modules\Native\Prefabs\props_furniture.xml` l.296-359 (bed_ground_a..j), l.198-240 (bed_straw); archhitecture_vlandia.xml l.1486/1522/1558 (tent_vlandia_a-c) i inne kultury.
- Konie: Native\Prefabs\props_other.xml l.885 (horse_critter); ROT-Map\Prefabs\animals.xml l.44/180/278/722; SandBox.dll SandBoxHelpers.SpawnHorses.
- Namioty, które już stawiamy w bitwie: log `Armoury-2026-10-20_12-25-19.log` [12:27:10] „namioty 5/5 ... uzyte: tent_vlandia_c”.
- Duży obóz na mapie: `G\Modules\Native\Prefabs\props_siege_mapicons.xml` l.76-126.
- Practice Drills: TMP\dane\mod_14005.json („about one for every six men”, „Camp kitchen (30+ men)”).
- Wyglądu tych prefabów i siatek w grze nie oglądałem (skala, kolizje).

---

## 5. Udźwig: koń z jukami

**Co to da w grze.** Więcej rzeczy bez kary za przeciążenie, a koń nie jest już „pusty”.

**Skąd 30 kg.** Gra liczy: 10 kg bazy + 20 kg za każdego zdrowego człowieka + 20 kg za każdego luźnego konia w taborze + 100 kg za każde zwierzę juczne.
Koń, na którym siedzisz, jest w Twoim ekwipunku, nie w taborze, więc daje 0. Sam z koniem masz 10 + 20 = 30 kg.
- U nas luźny koń, który nie wiezie piechura ani jeńca, niesie 60 kg zamiast 20.
- Już dziś najprostszy sposób to kupić konia jucznego (sumpter horse, ok. 110 denarów): daje +100 kg, czyli razem 130 kg.
- BK dodaje tylko +20 kg na zwierzę juczne za jeden perk. NavalDLC na lądzie nic nie zmienia.

**Gotowe mody.**
- Herds and Logistics (12105): dopilnowane zwierzęta juczne dają dodatkowe miejsce. Dobry pomysł, ale wersje 1.3.15-1.4.7,
  pod model The Old Realm i z zamkniętymi uprawnieniami.
- Campaign Tweaks (13708): mnożnik udźwigu na 1.4.8. To sztywny mnożnik, więc nie pasuje do naszej zasady.
- Pack Camel (13051) jest na 1.5.3. Stare mody na nieograniczony udźwig odpadają.
- Nic gotowego na 1.4.8 z BK i ROT.

**Jak to zrobimy we własnym modzie.** Wierzchowiec niesie sakwy przy siodle: **+20 kg** za każdego konia pod jeźdźcem
(tyle, ile gra daje za człowieka; więcej spowalniałoby konia w walce). Sam z koniem będziesz miał 50 kg.
Jedna zasada dla wszystkich, więc jazda AI i lordowie też dostaną więcej. Sprawdzę, czy to nie zmienia ich ekonomii.
Zmiana idzie w tym samym miejscu co nasz luźny koń 60 kg, żeby konia nie liczyć dwa razy.
Większe juki jako przedmiot opisuję w punkcie 6.

**Praca:** mała (tor S). **Ryzyko:** małe. Do sprawdzenia: udźwig jazdy AI i karawan oraz nasze tempo marszu.

**Ocena: realne.** To jedna dopiska w modelu udźwigu, który i tak już zmieniamy.

Dowody:
- Wzór gry: TMP\DefaultInvCap.cs l.74-79; koń pod jeźdźcem nie w taborze: TMP\PartyBase.cs l.385-387.
- NavalDLC: TMP\NavalInvCap.cs. BK: TMP\BKVanillaTweaks.cs l.625-641.
- Nasze: N24\Armoury\src\PackMarket.cs:26-33, 326-344, 369-371; Settings.cs:2521; MarketRoad.cs:248-252.
- Zwierzęta juczne: `G\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml` (is_pack_animal="true" od l.421), skrypt TMP\pack.py.
- Mody: TMP\dane\mod_12105.json, mod_13708.json, mod_13051.json.
- Do sprawdzenia przy kodzie: konie z wadą (np. kulawe) gra przy przeliczeniu taboru może pomijać jako juczne (dekompilacja ItemRoster ok. l.441-447). Nie sprawdzałem tego dokładnie.

---

## 6. Przedmioty obozowe do kupienia i wozy 3D

**Co to da w grze.** Kupujesz namiot (lepszy sen i morale w złą pogodę), wóz (dużo więcej udźwigu, ale wolniej), juki (więcej udźwigu na koniu).
Wozy widać w obozie, a może i na mapie.

**Gotowe mody.**
- Wozów ani namiotów jako przedmiotu do kupienia nie ma w żadnym modzie.
- Practice Drills ma wozy ciągnięte przez juczne zwierzęta w kolumnie i w obozie, ale tylko w scenie.
- Empires of Europe 1700 (6733) ma wozy karawan na mapie, ale to całkowita przeróbka gry, a jej zasobów nie wolno brać bez zgody.
- Persistent Bannerlord (Steam 2971078193) to tryb sieciowy.

**Co ma gra.**
- **Przedmioty:** w danych gry i wszystkich Twoich modów nie ma przedmiotu namiot, wóz, posłanie ani juki. Jedyny „tent” to ulepszenie statku z NavalDLC.
  Są za to uprzęże **„Mule Harness with Pack / with Baskets / with Bags”**, czyli dokładnie te juki, które nosi muł karawany na mapie.
  Dziś nie są towarem na targu.
- **Wozy do scen:** wozy wiejskie (bd_cart_a-c), wozy z sianem, zepsute wozy, taczka, wóz z beczkami.
  ROT ma też wozy z animowaną krową. Wozu z koniem w zaprzęgu nie ma.
- **Wozy na mapę:** są siatki w skali mapy (mi_cart_a, mi_cart_b, mi_cart_b_full), ale tylko jako nieruchoma dekoracja na mapie świata.
  Żadna drużyna na mapie nie jeździ wozem: karawana to postać i muł z jukami. Wozy wsi gry i nasze to sama logika (udźwig x2), bez obrazka wozu.

**Jak to zrobimy we własnym modzie.**
- **Juki:** wpuszczamy uprzęże z jukami na targ (ktoś je robi i sprzedaje, zgodnie z zamkniętą ekonomią).
  Założone koniowi zamiast zbroi końskiej dają większy udźwig, ale koń nie ma zbroi. Do sprawdzenia: czy uprząż muła dobrze wygląda na koniu.
- **Namiot:** nowy towar z płótna i skóry, robiony w miastach. W taborze daje lepszy sen i morale, a bez niego deszcz i zima bolą.
- **Wóz:** nowy towar. Wóz z zaprzęgniętym zwierzęciem jucznym niesie dużo więcej, ale drużyna jedzie wolniej. Widać go w scenie obozu.
- **Wóz na mapie przy drużynie:** trudne. Siatka jest nieruchoma (koła się nie kręcą), da się ją doczepić tylko przy zmianie stanu,
  a gra sama przebudowuje wizerunek drużyny.

**Praca:** średnia dla towarów bez obrazka na mapie; duża dla wozu na mapie. **Ryzyko:** średnie, bo nowe towary to nowy handel:
kto robi, kto kupuje, czy AI też kupuje.

**Ocena: częściowo realne.** Towary i wozy w scenie są realne. Wóz jadący po mapie przy drużynie trudny, a ładny (z kręcącymi się kołami) nierealny.

Dowody:
- Brak przedmiotów: grep po id w `G\Modules\*\ModuleData` (saddlebag, pannier, tent, bedroll, wagon, cart). Jedyne trafienie to deck_tent_lvl1-3 z NavalDLC.
- Juki: `G\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml` l.3439-3493 (mule_load_a/b/c, HorseHarness, is_merchandise="false", family_type="1"); RBM\ModuleData\RBMCombat_horse_armors.xml l.640-716.
- Muł karawany: CampaignSystem.dll CaravanPartyComponent.GetMountAndHarnessVisualIdsForPartyIcon („mule_load_a/b/c”, „camel”).
- Wozy do scen: `G\Modules\Native\Prefabs\building_details.xml` l.2148/4149/4156/2501/2508/3975-3996/8312/2176; ROT-Map\Prefabs\Mystaf_Outside.xml l.19115/19538/22295/20296/22151/2703, Others.xml l.821-866.
- Wozy na mapę: `G\Modules\Native\Prefabs\map_icon_parts.xml` l.1247-1259; występują tylko w scenach Main_map (SandBox, ROT-Map, NavalDLC).
- Nasze wozy wsi: log `Armoury-2026-10-20_13-28-19.log` [13:28:54] „MarketRoad: woz - udzwig taborow wsi ... x2.0”.
- Mody: TMP\dane\mod_14005.json, mod_6733.json, steamd_2971078193.json.

---

## 7. Mocna rozbudowa obozowania: miasteczko obozowe z markietankami

**Co to da w grze.** Duża armia ma duży obóz, a za nią ciągną markietani. Kupujesz jedzenie, wino, odzież i drobny sprzęt,
masz usługi (cyrulik, płatnerz), a w scenie widać kramy i ludzi.

**Gotowe mody.**
- Practice Drills: obóz rośnie z armią (kuchnia i płatnik od 30 ludzi, liczba namiotów od liczby ludzi).
- Camp Followers (3317): handlarz i usługi w menu armii, ale skompilowany na 1.3.2. Uprawnień nie przeczytałem (strona za blokadą treści dla dorosłych).
- Enlistment (9109): markietanki dla zaciężnego.
- Living Calradia (13933, 1.4.8): przygotowanie obozu zależy od czasu, umiejętności i terenu. Uprawnienia zamknięte.
- ROT Settlement Quartering (13120, wymaga ROT 8.1): kwatery, pasza, dostawy z wsi. Uprawnienia zamknięte, a mod dublowałby naszą ekonomię.
- Dwa mody z obozem są usunięte z Nexusa (11383 Set up camp, 11153 TAOM Camps).

**Co mamy dziś.** Nasi markietani (Sutlers) to tylko obieg złota. Co dzień ludzie drużyn w polu wydają 10% wolnej sakiewki:
85% trafia do kasy najbliższego niewrogiego miasta, reszta do wsi. Dochodzi do tego zakup odzieży wojska.
Markietani nie mają obrazka, menu, sklepu ani postaci. Płatnerz w taborze już jest.

**Jak to zrobimy we własnym modzie.**
1. **Sklep markietanów w menu obozu** (średnia praca): towary z półek tego miasta, do którego i tak płynie ich złoto, z narzutem za dowóz.
   Nic nie powstaje z niczego.
2. **Kramy i postacie markietanów w scenie obozu**, rosnące z armią (po punkcie 2). ROT ma prefaby kramów, np. market_stall_snorri_cart_a.
3. **Usługi:** cyrulik i płatnerz (pomysł „warsztat” niżej).

**Praca:** średnia dla sklepu, duża dla całości. **Ryzyko:** średnie, bo sklep to nowy handel w zamkniętej ekonomii.

**Ocena: realne etapami.** Sklep jest realny od razu. Pełne „miasteczko” w scenie wymaga sceny obozu, więc nie w jedną noc.

Dowody:
- Nasze: N24\Armoury\src\Sutlers.cs:3-27, Settings.cs:216-224; Armoury.json „SutlersEnabled”: true.
- Mody: TMP\dane\mod_3317.json, mod_9109.json, mod_13933.json, mod_13120.json; usunięte: mod_11383.json, TMP\n11383.html (status „removed”).

---

## Pomysł: czas rozbijania i zwijania według wielkości armii

**Co to da w grze.** Mała drużyna zwija się szybko, a wielka armia rusza później. Pośpiech nie jest za darmo.

**Gotowe mody.** Practice Drills: rozbijanie 1 h, zwijanie 0,5 h, ale niezależnie od wielkości armii.
Living Calradia: przygotowanie obozu zależy od drużyny i terenu (uprawnienia zamknięte).

**Co mamy.** Zwinięcie obozu („Break camp and move on”) daje Ci dziś równo 1 godzinę nieładu.

**Jak to zrobimy.** Godzina nieładu + dodatkowy czas za każdą kolejną setkę ludzi. Ta sama zasada dla AI, ale przedtem muszę przejrzeć,
jak AI rusza o świcie (tego nie sprawdzałem).

**Praca:** mała. **Ryzyko:** małe. **Ocena: realne.**

Dowody: N24\Armoury\src\NightRest.cs:2182-2199 („dezorganizacja rowno GODZINE”); TMP\dane\mod_14005.json („Setting up takes an hour and breaking camp half an hour”); mod_13933.json.

---

## Pomysł: ogniska i jedzenie a morale

**Co to da w grze.** Pełna noc w rozbitym obozie (przy ogniu, z ciepłą strawą) podnosi morale. Noc bez obozu, zwłaszcza zimą, je obniża.

**Gotowe mody.** Practice Drills: +10 morale za pełną noc w obozie rozbitym przed 22:00, raz na dobę.
LMMI 1.7: to, co masz w wozach, widać w morale. I'm Tired (365): morale za odpoczynek (uprawnienia zamknięte).

**Co ma gra.** Wygląd jest gotowy: ogniska, kotły, kocioł z gulaszem, garnek na trójnogu (ROT). Zasadę morale trzeba dopisać samemu.

**Jak to zrobimy.** Premia morale rano za pełną noc w obozie (większa przy urozmaiconym jedzeniu) i kara za noc w marszu (marsz forsowny)
albo zimą bez obozu. Jedna zasada dla wszystkich drużyn.

**Praca:** mała do średniej. **Ryzyko:** małe (trzeba sprawdzić, czy nie dubluje premii, które już są). **Ocena: realne.**

Dowody: TMP\dane\mod_14005.json; mod_6717.json; mod_365.json; prefaby: `G\Modules\Native\Prefabs\lighting.xml` l.86/1269/3630, props_foods_&_kitchen.xml l.87/94/430/829, ROT-Map\Prefabs\MyItems_Interior.xml l.5289.

---

## Pomysł: furażerzy z obozu

**Co to da w grze.** Z obozu wysyłasz furażerów. Armia oszczędza zapasy, a okoliczne wsie tracą jedzenie: własne się złoszczą, wrogie płaczą.

**Gotowe mody.** FieldCam (11748): furażowanie zależne od terenu, liczby ludzi i zwiadu; ogołocony teren jest potem pusty.
ROT Quartering (13120): wsie w okolicy oddają jedzenie kosztem gospodarstw. Oba mają zamknięte uprawnienia, więc tylko jako pomysł.
TAOM Hosts in the Field usunięty.

**Co mamy.** Furaż wrogich armii przy cudzych wsiach (ScorchedEarth).

**Jak to zrobimy.** Opcja w menu obozu: jedzenie z wsi w okolicy, a wieś traci zapas (zamknięta ekonomia: nic z niczego).
Własne wsie dają mniej i z utratą relacji. Trzeba sprawdzić, czy to nie kłóci się z naszymi daninami i dostawami.

**Praca:** średnia. **Ryzyko:** średnie (ekonomia wsi). **Ocena: częściowo realne.** Sam mechanizm jest prosty, ale dopasowanie do naszej ekonomii wymaga kontroli całości.

Dowody: TMP\dane\mod_11748.json („Forage for supplies”), mod_13120.json, mod_10449.json (status „removed”); N24\Armoury\src\ScorchedEarth.cs.

---

## Pomysł: warsztat obozowy (płatnerz, kowal, cyrulik)

**Co to da w grze.** W obozie naprawiasz sprzęt i szybciej leczysz rannych.

**Co mamy i co jest.**
- **Płatnerz w taborze** już jest (FieldArmourer).
- **Cyrulik:** Practice Drills ma rannych przy namiocie cyrulika, ale tylko jako widok.
  Enlisted (9193, licencja MIT) ma leczenie ran, ale to mod zaciężnego, nie wodza.
- **Pułapka do naprawy.** RealisticBannerlord (masz go włączonego) dodaje do menu obozu gry dwie opcje:
  - „Set up field hospital” zabiera 2 narzędzia i 2 len lub wełnę, pokazuje komunikat i **nic nie daje**;
  - „Maintain equipment” zabiera 1 narzędzie i leczy rannych bohaterów o 5 HP, a sprzętu nie dotyka.
- **Kowal i podkowy:** w grze nie ma podków. Moda, który robi z tego mechanikę, nie znalazłem.
  Czy da się leczyć kulawe konie u kowala, nie sprawdzałem.

**Jak to zrobimy.** „Field hospital” dostaje skutek (szybsze leczenie rannych w obozie, zależne od Medycyny cyrulika) albo znika z menu.
Wybiorę przy kodzie, co prostsze i bezpieczniejsze.

**Praca:** mała do średniej. **Ryzyko:** małe. **Ocena: realne** (cyrulik i płatnerz). Kowal od podków: nierealne, bo gra nie ma podków.

Dowody: `G\Modules\RealisticBannerlord\bin\Win64_Shipping_Client\RealisticBannerlord.dll` CampExpansionBehavior (TMP\RB_Camp.cs l.24-25, 60-82, 109-116; „hospital” występuje tylko w tej klasie); N24\Armoury\src\FieldArmourer.cs; TMP\dane\mod_9193.json.

---

## Pomysł: wozy taborowe (więcej udźwigu kosztem prędkości, cel napadów)

**Co to da w grze.** Tabor wozów wiezie zapasy armii. Jedzie wolniej, a bandy mogą go napaść.

**Gotowe mody.** Herds and Logistics: juczne zwierzęta zmniejszają karę za ładunek. Pack Camel: osobny tabor wielbłądów jadący za graczem
na mapie (na 1.5.3). Moda z wozem taborowym napadanym przez bandy nie ma.

**Jak to zrobimy.** Wersja prosta: wóz jako przedmiot z punktu 6, czyli więcej udźwigu i wolniej, bez osobnej drużyny na mapie.
Wersja pełna, czyli tabor jako osobna drużyna jadąca za armią i napady band na nią, to osobna drużyna, jej zachowanie i rozliczanie strat:
duża praca i ryzyko dla płynności mapy. Nie polecam teraz.

**Praca:** średnia (wersja prosta), duża (pełna). **Ryzyko:** duże w wersji pełnej. **Ocena: częściowo realne.** Wersja prosta tak, napady na tabor nie w rozsądnym czasie.

Dowody: TMP\dane\mod_12105.json, mod_13051.json („Their caravan follows your party on the campaign map”).

---

## Pomysł: namiot wodza z herbem na mapie

**Co to da w grze.** Z daleka widać, kto obozuje i jak duża jest jego siła.

**Co już jest.** Połowa tego działa: nasz obóz stawia na mapie namiot z chorągwią herbu rodu, a gra sama powiększa chorągiew
według siły drużyny (wódz armii pełna wielkość, reszta 80%). Na duże armie są gotowe siatki obozu w 5 rozmiarach.
BK ma własny obóz, który zmienia obrazek co klatkę, ale w Twojej wersji BK jest wyłączony i działa nasz.

**Jak to zrobimy.** Armia powyżej pewnej liczby ludzi dostaje siatkę dużego obozu zamiast jednego namiotu, a wódz zostaje z chorągwią.
Tylko przy rozbiciu i zwinięciu.

**Praca:** mała do średniej. **Ryzyko:** średnie (mapa, zasada „nie co klatkę”). **Ocena: realne.** Wyglądu siatek w grze nie oglądałem.

Dowody: N24\Armoury\src\NightRest.cs:388-396, 1398-1460, 274-276, 2141-2160; TMP\MobilePartyVisual.cs l.1005-1008, 1338-1366 (l.1362 siła / 500); `G\Modules\Native\Prefabs\props_siege_mapicons.xml` l.76-126; TMP\BKCamping.cs l.59, 130.

---

## Propozycja kolejności na noc

Dopóki grasz, tylko piszę kod, buduję i przeglądam. Autotest, wgranie i poprawka pliku ustawień dopiero po zamknięciu gry.
Jedna rzecz naraz, każda z własnym autotestem i wpisem w STAN-PRAC.

1. **Obóz przed nocą gry** (S): start 21, koniec 3. Do tego obóz spłaty długu AI liczony od końca obozu, noc wzroku zrównana z nocą gry (22-2)
   i nowe opisy w MCM. Po zamknięciu gry: kopia ustawień i wpisanie 21 i 3, autotest 2-3 doby (pytanie o nocleg, świt, karczma,
   dzień odpoczynku armii, dług snu AI), wgranie.
2. **Juki na wierzchowcu +20 kg** (S), z kontrolą udźwigu jazdy AI i karawan.
3. **Trening z wyboru: „Drill the men” w menu obozu** (S/M), na bazie Musztry, z kosztem snu.
4. **Cyrulik:** naprawa pustego „Set up field hospital” z RealisticBannerlord (S).
5. **Czas zwijania według wielkości armii** (S/M), po przejrzeniu, jak AI rusza o świcie.
6. **Większy obóz armii na mapie** (siatka dużego obozu, tylko przy zmianie stanu) (S/M).
7. **Sklep markietanów w menu obozu** (M), jeśli zostanie czas.
8. Na kolejne noce: scena obozu (tor L, etapy z punktu 2), potem namiot, wóz i juki jako towary, furażerzy, życie obozu w scenie.
   Jeśli pobierzesz Practice Drills, przed sceną rozbiorę go jako wzór.
9. Optymalizacja na koniec, po zamrożeniu nowych funkcji (zgodnie z `docs/ZADANIA-OBOZ-NOC.md`).

## Czego nie da się zrobić i dlaczego

- **Spaceru po obozie „jak we wsi”:** misja wsi i miasta wymaga osady, a w polu jej nie ma. Da się tylko własną misją (punkt 2).
- **Wozu jadącego po mapie z kręcącymi się kołami:** siatki wozów na mapę są nieruchome. Można je co najwyżej doczepić do drużyny przy zmianie stanu.
- **Taboru jako osobnej drużyny napadanej przez bandy:** za duże na teraz (zachowanie AI, płynność mapy).
- **Samego posłania na mapie dla samotnego gracza:** nie ma takiej siatki w skali mapy. Zostaje figurka bez namiotu.
- **Kowala od podków:** gra nie ma podków.
- **Gotowego „konia przy paliku”:** nie ma takiego elementu, ale żywe konie da się postawić w scenie.
- **Practice Drills jako drugiego moda obok naszego obozu:** dałoby się zainstalować, ale dwa obozy, podwójne morale i blokada marszu
  kłóciłyby się z naszym snem i namiotami. Nie polecam.

## Pytania do Jeffa (tylko decyzje gracza)

1. Czy pobierzesz z Nexusa plik Practice Drills 1.3 (https://www.nexusmods.com/mountandblade2bannerlord/mods/14005, wymaga Twojego logowania)?
   Nie będziemy go instalować. Rozbiorę go tylko jako wzór sceny obozu.
2. Czy chodzenie po obozie ma być na początku kolejki, choć to 2-3 noce pracy i najwięcej ryzyka wywrotek? Czy najpierw rzeczy tanie (punkty 1-7 kolejności wyżej)?
3. Czy namiot, wóz i juki mają być towarem do kupienia (zmienia to handel w miastach), czy wystarczy, że obóz wygląda według liczby ludzi?
4. Markietani: tylko sklep (jedzenie, wino, odzież, drobny sprzęt), czy także usługi i rozrywka w obozie?

## Czego nie sprawdziłem

- Żadnego moda w grze. Nic nie pobierałem: wymagałoby to Twojej zgody, a na Nexusie zwykle logowania.
- Kodu Practice Drills (jak stawia namioty na mapie, czy wozy to modele z gry) i jego zgodności z BK i naszymi modami.
- Wyglądu w grze prefabów ROT i siatek mapy (skala, kolizje, szczegółowość z daleka).
- Powrotu z własnej misji „Camp” do menu bez spotkania, płynności przy wielu postaciach i przypinania naszych postaci do punktów siedzenia i snu.
- Jak AI zwija obóz o świcie. Łatki treningu BetterEconomy. Nocnych zmian RBM w bitwie. AIInfluence.dll się nie dekompiluje.
- Czy wozy wsi i budżety mają jeszcze inne, ukryte zależności od godziny (grep ich nie znalazł).
- Pełnych list komentarzy pod modami (tylko pierwsze strony), uprawnień Camp Followers, forów TaleWorlds, Reddita i ModDB.
- Czy uprząż muła z jukami dobrze wygląda na koniu.
- Czy minuta sprawdzania godziny (dziś :42) zmienia się przy każdym wczytaniu u Ciebie. Kod gry mówi, że jest ustalana przy wczytaniu,
  ale w Twoich logach tego nie porównywałem.

Pliki robocze (dekompilacje, lista prefabów, surowe dane modów i skrypty) są w TMP. W repo dodałem tylko ten plik.
