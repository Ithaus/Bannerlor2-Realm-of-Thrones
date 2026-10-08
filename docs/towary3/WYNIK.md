# WYNIK: TOWARY 3 po audycie (08.10.2026) - galaz t3-sklad, HEAD c1e8bea

- Klon `scratchpad\lancuch` (sesja 3cf3e0ac), galaz **t3-sklad = c1e8bea** (worktree `scratchpad\dzien-6\towary3\repo`; `git branch -f t3-sklad <HEAD>`
  odmawia, bo galaz jest wybrana w worktree - wskazuje juz na c1e8bea, sprawdzone `rev-parse`).
- NIC nie wgrane do gry, NIC nie wypchniete. Repo Jeffa i `Armoury.json` tylko czytane. Gra w trakcie pracy nie dzialala (proces sprawdzony).
- Zatwierdzone DLL w grze (md5 sprawdzone 08.10): Armoury **62e37795** (= build `git archive 39b60a0`, n120 + W2 - sprawdzone budowaniem: ten sam md5),
  GrandTourney 0aa5d0ef, RealisticCaptivity c393f4fb, CrashScribe 11fa0214; dane wiosek `ModuleData\arm_map_villages.tsv` 08ab1f16 (wariant 4000).

## 1. Co zlozone

Lancuch 8ee9c94 (127-150, opis SKLAD.md) + 12 commitow:

| Commit | Co |
|---|---|
| 1a2a6a3 | W2: wioski na mapie - widok (cherry-pick 3b59c75, bez konfliktow) |
| 80dbb34 | W2: poprawka po autotescie (cherry-pick 39b60a0) - to jest W2 z gry (62e37795) |
| 3a9a41e | W2: dane wiosek jak w grze - wariant 4000 (repo mialo plik wariantu 6000; DLL pliku nie zawiera) |
| b655be6 | 151: sufit XP rafinacji dziala (Priority.High przeliczenia od dawnej wartosci) - sprzed lancucha |
| f594ebc | 128, 129, 132, 133, 148: linie startowe bez falszywego BRAK / WYLACZONE; przyklady 129 pod jednym prefiksem |
| 2ab41e5 | 135: naprawy ludzi gracza i lordow AI z materialem z targu (krok 139 planu K13, NOWY wlacznik `MendMaterialMenAndLords`); ramka napraw w ksiedze towarow (146) |
| eac4563 | 152: budowy nie placa kasom wiecej, niz lord ma - sprzed lancucha |
| f6c8e19 | 135: naprawa uprzezy przy wylaczonej regule kowali co do grosza jak w n120 |
| 10e69ae | 134, 135, 137, 139, 140, 142, 145: opisy MCM i napisy w grze zgodne z regulami (Armoury, GT, RC) |
| 16288ac | 153: wolne gojenie nie lagodzi kary BK za glod w oblezonej osadzie (Priority.Low) - sprzed lancucha |
| 12f5f18 | 140: pula turnieju wraca, gdy ogloszenie padnie wyjatkiem (GT) |
| c1e8bea | docs: poprawki po audycie w opisach paczek 127-148 |

Kazdy commit kodu: wpis na gorze `CHANGELOG.md` (Problem / Przyczyna / Zmiana / Ryzyko), `python tools/gen_mcm.py`, build Armoury (i GT / RC, gdy
dotyczy) kod 0. Ustawienia: Armoury 667 -> **671** (+3 W2: MapVillagesEnabled, MapVillagesHideAboveCameraHeight, MapVillageNamesOnHover; +1
MendMaterialMenAndLords), GrandTourney 40, RealisticCaptivity 104.

## 2. DLL do wgrania (`towary3\dll-final`, z czystego `git archive` c1e8bea; gen_mcm w archiwum = commit; build kod 0 x 4)

| DLL | md5 |
|---|---|
| Armoury.dll | **a08ac43a4440f84b2e8864e788cf06b8** |
| GrandTourney.dll | **1337433c1f87410b1319f50efeb63a71** |
| RealisticCaptivity.dll | **3e04b89b8839b95843dc178e459b983f** |

CrashScribe w lancuchu bez zmian (build kod 0, 3588f9b9) - NIE dolaczony, w grze zostaje 11fa0214. Stare `towary3\dll` (8ee9c94) - NIE do wgrania
(bez W2: zabraloby Jeffowi wioski). Kontrola przed wgraniem: `git diff t3-sklad <galaz wgrania> -- Armoury GrandTourney RealisticCaptivity` pusty + build 0.

## 3. Zgloszenia WAZNE - wszystkie sprawdzone, wszystkie prawdziwe, poprawione

1. **W2 brak w lancuchu (audyt zapisu + audyt MCM).** Prawda: `merge-base --is-ancestor` 3b59c75 / 39b60a0 -> nie; DLL gry 62e37795 = moj build
   archiwum 39b60a0 co do bajtu. Poprawka: cherry-pick obu (bez konfliktow, gen_mcm bez zmian, build 0) + plik danych 4000 jak w grze. Spis latek
   Harmony (census): W2 nie dodaje zadnej latki; zapis gry - W2 nic nie zapisuje.
2. **Sufit XP rafinacji obchodzony (audyt latek).** Prawda: xpprobe na DLL 8ee9c94 - stal 133 / 200 / 156 XP przy suficie 60. Poprawka: przeliczenie
   (`MaterialLaw.RefineXpPostfix`, `HistoricalPrices.XpPostfix`) z Priority.High. Po: 60 / 60 / 60, "sufit dziala"; census nowy vs stary - jedyne
   roznice to te priorytety (i 153). Nie poprawiane: Iron2 30 zamiast 36 XP (przeliczenie zaokraglonego wyniku gry - drobne).
3. **Ludzie i AI naprawiaja bez materialu (audyt zamknietej ekonomii, Z1).** Prawda (kod TroopSelfMend :94-109, :161-174; AiWear :261-270; sam
   naglowek MendMaterial zapowiadal "potem naprawy ludzi i AI"). Poprawka: ta sama regula co lawa (`MendMaterial.Order`), wlacznik
   `MendMaterialMenAndLords` (domyslnie wl., przy `SmithMendFromMarket`); rezerwa sakiewki z szacunkiem materialu; linia "Zuzycie AI" z
   materialem; ramka `FMend` w ksiedze towarow. Proba: NOWA sekcja 17 proby lawy (A-G, 15 kontroli) - patrz sekcja 6.
4. **Brak instrukcji cofniecia 127 w Armoury.json (audyt zapisu).** Prawda (grep "cofn|przywroc" w SKLAD / 127.md - nic). Poprawka: dopisane w
   `docs/paczki/127-pokretla-jeffa.md`, w errata SKLAD.md i tu (sekcja 7).
5. **"BRAK: nic" w liniach startowych 128 / 132 / 133 (audyt logow).** Prawda (sprawdz_logi: `\bBRAK\b` -> ALARM; sprawdzone na logu
   syntetycznym). Poprawka: napis "wszystkie sciezki wpiete" / "wszystkie drogi wpiete" / "wszystko wpiete"; BRAK tylko przy prawdziwym braku.
   Przy okazji tej samej klasy bledu (niskie): 148 "TanOrWeave WYLACZONE" -> "zastapione rzemioslem", 129 przyklady pod stalym prefiksem.
6. **Narzedzie logow nie zna linii 127-150 (audyt logow).** Prawda (OGNIWA koncza sie na 119/113, brak tematow). Narzedzie NIE lezy w t3-sklad
   (jest tylko w repo Jeffa, galaz robocza, md5 59962faf), wiec poprawka jest obok galezi: `towary3\naprawa\narzedzia\sprawdz_logi.py`
   (md5 e97a6670) i latka `sprawdz_logi-towary3.patch` (naklada sie `git apply -p1` na 59962faf i daje dokladnie e97a6670). Zawiera: latke ksiegi
   towarow z k13 (127 -> 146), 10 nowych tematow (Spoils of War (128), Spoils - naprawa u kwatermistrza, Spoils - klan najemnikow, Zatkane wsie
   (diagnoza) / (wozy w drodze) / (przyklad), Wozy w miastach, Rzemieslnicy BK (147), Rzemioslo miasta (148), Odziez wojska (150)), nowe kolumny
   (na odziez wojska w Sakiewka ludzi, sredni poziom plac w Budowy oplacone, material napraw w Zuzycie AI), PRZESUNIECIE_DNIA, 18 ogniw z pewna linia
   (121-126, 128-134, 143, 144, 146-148, 150; bez wlasnej linii: 120, 124, 127, 135-142, 145, 149), grupa `--grupa 3t` (TOWARY 3) i ogolne
   kontrole ogniw. Sprawdzone: logi 12-37, 14-18 i log gry Jeffa 08.10 02:01 - bez wyjatku, na logu Jeffa "OGNIWO W GRZE 119" jak dotad; log
   syntetyczny TOWARY 3 (14-18 + prawdziwe linie startowe dll-final i linie dnia z prob) - zaden temat w trybie surowym, zero falszywych BRAK,
   `--grupa 3t` OK 34 / UWAGA 3 (uwagi z danych bazy) / BRAK DANYCH 0. Do wgrania razem z lancuchem (na slowo Jeffa).

## 4. Zgloszenia NISKIE

Poprawione (oczywiste i bezpieczne):
- Z7 BuildFunding: robocizna najwyzej z reszty kiesy lorda (152) - koniec zlota z niczego przy drogim materiale.
- Z6 GT: pula zdjeta przed `AddTournament` wraca z nicosci, gdy wpis obwieszczenia nie powstal (tylko sciezka wyjatku; przy wylaczonym bez zmian).
- HarnessLabor (135): stara kwota starym wyrazeniem - wylaczona regula naprawde "co do znaku" jak n120.
- Teksty: menu i opis MCM kwatermistrza (robota wedle reguly kowali), opisy suwakow nieuzywanych przy nowych wlacznikach (ForgeFeePerTier,
  BkForgeHourlyMultiplier, ForgeDayHours - nowy opis, RepairCostFactor, TroopOrderMarkup, GT HostBaseFee / HostFeeProsperityFactor /
  HostTakingsProsperityFactor / CancelledFeeRefund, RC HomePrice*, WorkPay*, GuardPay*, GuardBrawlBonus) - kazde "not used" sprawdzone w kodzie;
  RC podpowiedz dniowki = polowa w miejscu nasyconym (jak zaplata); BkSupplyDaysCap bez polskiego cytatu.
- SlowHealing (153): po BK (Priority.Low) - kara BK za glod w oblezonej osadzie nie jest juz polowiona dla gracza. Census potwierdza kolejnosc.
- Dokumentacja: 141 (karnet BK bez pieniedzy nie jest wydawany takze przy wylaczonym ForgeHireHistorical - kod zostaje, zamyka darmowa dobe;
  opis poprawiony), 140 (gt_events: znaczenie 4. pola bez zmian - pula gracza juz w n120), 128 (15 latek = 10 + 5 z 134), liczby ustawien w
  SKLAD (631 -> 667 wzgledem gry; 5 kluczy 121-126 dopisane w errata), XML 127 (tylko SmithingSkillPerTier + SmithingDifficultyPerTier).

NIE poprawione (wymagaja decyzji projektu albo osobnej paczki z proba - wpisane jako "Otwarte" w opisach paczek):
- Z2 (147): dodatkowe cykle linii wieloproduktowej gubia wyroby uboczne (6 owiec -> +2 welny; skory, gdy count skor < count miesa) - do osobnej
  poprawki (dopisac brakujace sztuki z zaplata albo ograniczyc cykle).
- Z3 (132): pekniecie zbroi przy kowadle BK niszczy caly material, broni - 50% (dwie reguly jednego zjawiska) - zwrot polowy wsadu do sakw wymaga
  proby z BK (`GetCraftingInputForArmor`).
- Z4 (128): zdarzenie Spoils "bandyci ukradli" zdejmuje sztuke do nikad - decyzja: nie odgrywac czy oddac bandzie.
- Z5 (134/135): `Commit` materialu przed petla w `SmithMenu.ApplyRoster` / `SpoilsSeal.Apply` - material przepada tylko przy wyjatku w polowie
  (w nowym kodzie 139 kolejnosc: plan na kopii -> Commit -> zbrojownia -> zaplata, bez petli z wyjatkiem).
- 143 bez linii dnia (koszt HorseCost nieznany), brak pomiaru kosztu w liniach 147 / 148 / 150 / 130, ksiega towarow (146) w glownym logu
  (20-40 KB / dobe) - dopiski logu, nie bledy; po autotescie, gdy pomiar czasu doby pokaze potrzebe.

Odrzucone: zadne zgloszenie nie okazalo sie nieprawdziwe.

## 5. Proby poza gra - przed / po (DLL dll-final; kopie prob w `towary3\naprawa\p1f`, `p2f`, `pl`; katalogi autorow i proby-1/proby-2 nietkniete)

Dopasowania srodowiska (nie sprawdzianow): obok testowanego DLL kopie SandBox.dll, SandBox.View.dll i (125/126/129/130) TaleWorlds.MountAndBlade.View.dll
z gry - W2 dziedziczy po SandBox.View, a proby robia `GetTypes()`; napis "BRAK: nic" -> nowy w kopiach 128 / 132 / 133 / 134 / kwatermistrz;
lista nazw ustawien w probach lawy (+3 W2, +1 MendMaterialMenAndLords - jak kopia D1 z proby-2); wejscie mapa.txt / bramy.tsv dla 130.

| Proba | Przed (proby-1 / proby-2, dopasowane) | Po (dll-final) |
|---|---|---|
| 121 on / rev | 12/12, 8/8 | 12/12, 8/8 (x2) |
| 122 on / off122 | 90/90, 75/75 | 90/90, 75/75 (x2) |
| 123 on / off | 51/51, 48/48 | 51/51, 48/48 (x2) |
| 124 / 125 / 126 | 28/28, 36/36, 58/58 | 28/28, 36/36, 58/58 (x2) |
| 127 nowa / wylacznik | 22/22, 22/22 | 22/22, 22/22 (x2) |
| 128 / 129 / 130 | 76/76, 69/69, 52/52 | 76/76, 69/69, 52/52 (x2) |
| 132 nowa / wylacznik | 73/73, 54/55 | 73/73, 54/55 (x2; W8 zastane - jak przed) |
| 133 / 134 (r0) | 74/74, 52/52 | 74/74, 52/52 (x2) |
| 146 k1 | 66/66 | 66/66 (x2) |
| 147 k2 off / on / on2 | 35/35, 40/40, 40/40 | 35/35, 40/40, 40/40 |
| 148+149 harness off / on / on2 | 40/40, 83/83, 83/83 | 40/40, 83/83, 83/83 |
| 148 k3 off / on / on2 | 39/39, 74/74, 74/74 | 39/39, 74/74, 74/74 |
| 150 k4 off / on | 32/32, 47/47 | 32/32, 47/47 |
| lawa (D2) + NOWA sekcja 17 (139) | 118/118 | **133/133** (118 + 15 nowych; x2 na DLL przed archiwum, x1 na dll-final) |
| konie (D1) / zamowienia (D1) | 136/136, 130/130 | 136/136, 130/130 |
| kwatermistrz REGULA_KOWALI=0 / =1 | 52/52, 48/52 | 52/52, **47/52** - nowy FAIL = zamierzony: sprawdzian wymaga napisu "a quarter of the worth" przy wlaczonej regule kowali, a menu mowi teraz prawde ("their share of the days a master spent making each piece"; robota 145 zl); 4 stare FAIL bez zmian (robocizna 25% na sztywno, sprzed 135) |
| xpprobe (151) | Iron4-6: 133 / 200 / 156 XP | 60 / 60 / 60, "sufit dziala" |
| census latek (Armoury + GT + RC) | 578 latek | 578, roznice tylko priorytety: MaterialLaw / HistoricalPrices XP p600, SlowHealing p200 |

Sekcja 17 (naprawy z materialem): A town_a - 5 sztuk w godzine, sakiewka -32 = kasa +32 = robota 28 + material 4, z polki za 46.00 = material +
42.57 w zapasie kowali, godziny kowali tylko za 5 sztuk; B town_b (bez metalu i skory) - nic, "czeka na material 5 (brak: metal, skora)" i komunikat
po angielsku; C wylacznik - jak dotad 28 zl, polka nietknieta; D sakiewka 25 - 4 sztuki za 23, nie ponizej zera; E lord AI - jak gracz, ta sama
robota; rezerwa 28 / 33; F lord w town_b - nic, linia dnia z materialem i czekaniem; G wylacznik AI - jak dotad.

Autotestu w grze NIE bylo (zgoda Jeffa obejmuje autotest, ale zadanie mowilo "nic nie wgrywaj").

## 6. Edycje Armoury.json przy wgraniu (gra zamknieta, najpierw kopia `Armoury.json.bak`)

`Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json` - DOKLADNIE trzy klucze (paczka 127):
`"SmithingSkillPerTier": 45 -> 35`, `"DurabilityPerArmorPoint": 20.0 -> 13.33`, `"MinSellPercentOfValue": 5 -> 2`; kontrola grep -> 35 / 13.33 / 2.
Nowych kluczy NIE dopisywac (brak = domyslne z kodu, takze `MendMaterialMenAndLords` = true i klucze W2). GrandTourney.json i RealisticCaptivity - nic.
**PRZY COFNIECIU** (do 62e37795 / 0aa5d0ef / c393f4fb): przywrocic Armoury.json z `.bak` albo 45 / 20.0 / 5 - inaczej n120 liczy kosci kuzni z progu
35 (wiecej legend, 0% pekniec), zbroja zuzywa sie 1.5 x szybciej, podloga ceny 2% bez nowej logiki. Plik wiosek w ModuleData (08ab1f16) zostaje.

## 7. Klucze zapisu gry

Poprawki po audycie NIE dodaja kluczy. Lancuch (bez zmian wobec SKLAD / audytu zapisu): NOWE `arm_armyclothing` (150), `arm_mendstock` (134/135 -
od poprawki 139 uzywany takze przez naprawy ludzi i AI: reszty calych sztuk materialu kowali wedle miasta), `arm_towncrafts` (148); zmieniona tresc
`arm_condition` (piate pole, 132), `arm_daypass` (doby projektow, 141/142), `gt_events` (ten sam format; pula idzie do zwyciezcy / wraca cala).
W2 nic nie zapisuje. Stary zapis (save039 Jeffa na 62e37795) wczyta sie: brak klucza = pusty stan. Cofniecie do n120 po zapisie t3: stary DLL sie nie
wywroci (osierocone klucze czyszczone przy zapisie), przepada tylko stan t3 (pula turnieju, zapas kowali, potrzeba odziezy).

## 8. Ryzyka

1. **Naprawy AI z materialem (139):** setki napraw dziennie zjadaja teraz rude / sztaby / zlom, drewno, skore, plotno z polek (plan K13: ok. 825
   napraw -> 3-5 skor, 3-5 plotna, 8-16 lup albo zlomu dziennie). W miastach bez metalu naprawy AI czekaja; "obite" AI to tylko nasz zapis (DTE nie
   zna modyfikatorow) - sila wojsk AI bez zmian, zmienia sie cena sprzedazy nadwyzek AI i sakiewki ludzi. Sztuki bez receptury kowala w zbrojowni
   gracza nie sa juz naprawiane przez kowali (jak na lawie).
2. **Koszt czasu (nie zmierzony):** `MendMaterial.Bench` przechodzi polke miasta raz na zlecenie (AI: raz na dobe na partie w miescie; gracz: co godzine
   w miescie) i raz przy rezerwie sakiewki przy wyjezdzie; szacunek dziesiatki ms na dobe przy 12.6 s - zmierzyc w autotescie.
3. **Tempo kowalstwa (151):** rafinacja stali uczy najwyzej 60 XP za partie (dotad 133-200) - tak, jak mowi suwak RefineXpCap (60 w Armoury.json).
4. Jeff sam przy wioskach (STAN-PRAC 08.10): zalecone MCM Map Villages Enabled = off do nowej wersji wiosek (AccessViolation w diagnostyce drzewa
   W2 - `MapVillagesView.TreeLine`); W2 w t3 to dokladnie wersja z gry - ten sam blad zostaje, poprawka idzie workflow wioski-wyglad-i-zdjecia.
5. Otwarte sprzed tego kroku (STAN-PRAC "DO POPRAWY PRZED WGRANIEM", analiza autotestu lawy 14:18; nie bylo w zgloszeniach tego audytu, nie ruszane):
   (1) 144 - dzielnik popytu kategorii "horse" z przecenionych sztuk (ges, kura, kot, pies w kategorii koni: popyt na konie /25); (2) 143 - kon
   najemnika z karczmy z niczego, a miasto dostaje zaplate. STAN-PRAC: "osobny krok poprawek (1) i (2) na t3-sklad" - nastepny krok.
6. Paczki wysp (w toku) i ceny sprzedazy (a87ad4a; Jeff: wgrac razem z 127) - poza ta grupa, dojda na koncu.

## 9. Co sprawdzic w autotescie (NOWA kampania, 40 dob; `python towary3\naprawa\narzedzia\sprawdz_logi.py <log> --grupa 3t`)

Linie startowe: "SpoilsSeal: ... | wszystkie sciezki wpiete", "SpoilsCompany: ... | wszystkie drogi wpiete", "Zbroja z kuzni: ... CZYNNE ... | wszystko
wpiete", "GoodsLedger: ksiega towarow (146) CZYNNA", "Rzemieslnicy BK (147): kazda sztuka z wlasnego wsadu WLACZONA", "Rzemioslo miasta (148): WLACZONE
... x poziom plac miasta ... zastapione rzemioslem", "Odziez wojska (150): WLACZONA", "RecruitCost: kon rekruta po cenie targu", "Wioski: plik ... 4000
people per village" (W2), "SmithAudit: ... XP z sufitem", "SlowHealing: gojenie na mapie - gracz 50%, AI 100% (2/2 metod)"; zadnej linii startowej z
BRAK / NIECZYNNE / WYLACZONE.
Co dobe: "Zuzycie AI: dzien D - ... naprawione w miastach N ... Material napraw z targu: X zl w N zleceniach, zuzyto kg: metal / drewno / skora /
plotno; czeka na material N szt. (metal ..., skora ...)" - czekajace powinny byc ulamkiem napraw (gdy duzo - polki metalu); "Towary: ... naprawy kowali
miasta (135) ..." w ujsciach rudy, drewna, skory, lnu, welny i "Towary (bilans): ... ZGODNA / ZGODNA"; "TroopSelfMend: <miasto> - naprawiono N szt. za
G z sakiewki ludzi (w tym material M zl; ...)" (gdy gracz stoi w miescie z obitym sprzetem); "Sakiewka ludzi: ... na odziez wojska (150) ..."; "Spoils of
War (128)" z "wyplacono z niczego 0 zl"; "Rzemioslo miasta (148)", "Odziez wojska (150)", "Rzemieslnicy BK (147)", "Zatkane wsie (diagnoza)", "Wozy w
miastach" jak w opisach paczek; czas doby (s/dobe) wobec 12.6 s; ERROR 0.

## 10. Pliki

- Galaz: klon `scratchpad\lancuch`, t3-sklad c1e8bea; DLL `towary3\dll-final` (md5.txt); archiwum budowania `towary3\naprawa\archiwum`, logi
  `towary3\naprawa\logi-final`.
- Proby: `towary3\naprawa\pl` (proba lawy + sekcja 17; wyniki `p2f\wyniki\run-lawa-1.txt`), `naprawa\p1f\powt-naprawa*-wynik.txt`, `naprawa\p2f\wyniki`,
  `naprawa\xpprobe-przed.txt` / `xpprobe-po.txt` / `xpprobe-final.txt`, `naprawa\census\{stare,nowe}\census-ours.tsv`.
- Narzedzie logow: `towary3\naprawa\narzedzia\sprawdz_logi.py` (e97a6670), `sprawdz_logi-towary3.patch`, generator `zrob_sprawdz_logi.py`, log
  syntetyczny `log_syntetyczny_t3.py` + `test\` (wyjscia na logach 12-37, 14-18, 02-01 i syntetycznym).
- SKLAD.md - dopisana ERRATA 08.10 (kopia sprzed: `naprawa\SKLAD-przed-errata.md`).
