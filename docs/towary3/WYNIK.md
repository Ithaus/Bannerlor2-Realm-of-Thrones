# WYNIK: TOWARY 3 po audycie (08.10.2026) - galaz t3-sklad, HEAD c1e8bea

> RECENZJA DOKONCZENIA 08.10 (sekcja "Recenzja dokonczenia 08.10" na samym koncu): 3 bledy znalezione i poprawione (157, 158, 159 - osobne commity "poprawka po recenzji"); HEAD galezi **297320e**, DLL do wgrania `towary3\dll-final-2` (Armoury **e9c47794**, GT 1337433c, RC 3e04b89b); wersja sprzed recenzji (Armoury a68d4a74) - `towary3\dll-final-2-przed-recenzja`, NIE do wgrania.
>
> DOKONCZENIE 08.10 (sekcja "Dokonczenie 08.10" na koncu): (A)-(F) = 154-159 + W2 v4 (wioski z gry 70b29477); HEAD galezi wtedy d618cf4 (DLL Armoury a68d4a74 - zastapiony, patrz wyzej). Ponizsze sekcje 1-10 opisuja stan c1e8bea; zatwierdzony Armoury w grze to od 04:55 **70b29477** (nie 62e37795).

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

## Dokonczenie 08.10 - (A)-(F) + W2 v4: galaz t3-sklad = d618cf4

- Klon `scratchpad\lancuch` (sesja 3cf3e0ac), galaz **t3-sklad = d618cf46** (worktree `towary3\repo`, commity robione w nim - galaz przesunieta sama, `rev-parse t3-sklad` = HEAD).
  NIC nie wgrane do gry, NIC nie wypchniete, repo Jeffa i `Armoury.json` tylko czytane (kopia WYNIK/SKLAD w repo Jeffa `docs/towary3` - nieaktualna, do podmiany przy przekazaniu).
- Robocze pliki, kopie prob i wyniki: `towary3\dokonczenie` (katalogi autorow prob nietkniete).
- **UWAGA - gra zmienila sie w trakcie pracy:** 08.10 ok. 04:55 wgrane wioski v4 - zatwierdzony Armoury w grze to teraz **70b29477** (n120 + W2 85215de, plik danych b742f307),
  nie 62e37795 (kopia `Armoury.dll.bak-2026-10-08-wioski-v1`). STAN-PRAC: "TOWARY 3 musi przy skladaniu dostac W2 85215de" - dolozone jako ostatni commit (W2 v4, nizej);
  gdyby mialo go nie byc: w worktree `git reset --hard ab1f1a6` (159) i ponowny build.

### 1. Commity (kazdy: wpis `docs/paczki/NNN-*.md` + wpis na gorze `CHANGELOG.md` bez Status, gen_mcm, build kod 0, proby)

| Commit | Nr | Co | Zrodlo / konflikty |
|---|---|---|---|
| 60a5b71 | 154 | WYSPY - karawany i lordowie nie stoja (`IslandRoadsFix`) | cherry-pick 90234dc; konflikt `SubModuleMain.cs` (etykiety 131/130) - wersja lancucha + linia `IslandRoads.ApplyAll` po `CartTownExit.ApplyAll`; "paczka NN" -> 154 |
| 87a5a33 | 155 | CENA SPRZEDAZY SPRZETU - kara BK raz, cena od stanu, sufit, ksiega skupu (`BkTradePenaltyOnce`, `SellPriceByCondition`, `SellCapPercentOfNewAsk`) | cherry-pick a87ad4a; bez konfliktow; styk 128/133/135/143/145 sprawdzony w kodzie (opis 155) |
| 2250787 | 156 | poprawka 144: popyt na konie jak przed 144 (ges i kura w kategorii "horse" poza przelicznikiem) | nowy kod (`HistoricalPrices.Apply` krok 3) |
| d15a3ba | 157 | poprawka 143: kon najemnika z karczmy - zaplata tylko za konia zdjetego z polki, inaczej wlasny kon bez doplaty (`MercHorseFromShelf`); linia dnia "Konie rekrutow (157)" | nowy kod (`RecruitCost`, `LevyGold`; gracz - latki `buy_mercenaries_on_consequence`, `BuyMercenaries`) |
| 32eb231 | 158 | WRAKI NA ZLOM - Mangled i stany <= 0.10 na zadnej drodze kowali miasta (`WrecksToScrap`) | nowy kod (`LootPrices`, `SmithMenu`, `ArmouryBehavior`, `MendMaterial`, `SpoilsSeal`, `QuartermasterLaw`) |
| ab1f1a6 | 159 | CleanseAmmo nie leczy koni (kon i zwierze zachowuja stan) | nowy kod (`ArmouryBehavior.CleanseAmmo`) |
| d618cf4 | W2 v4 | wioski v4 - wersja z gry 70b29477 | cherry-pick 10239d9 + 85215de; konflikt tylko pliku danych - wziety v4 (b742f307 = plik w grze); `MapVillagesView.cs` i tsv co do bajtu = 85215de |

Ustawienia: Armoury 671 -> **677** (+6: `IslandRoadsFix`, `BkTradePenaltyOnce`, `SellPriceByCondition`, `SellCapPercentOfNewAsk` = 10, `MercHorseFromShelf`, `WrecksToScrap`; wszystkie wlaczone),
GrandTourney 40, RealisticCaptivity 104 (bez zmian). Zmienione tylko opisy MCM: `MinSellPercentOfValue` (155), `TroopMendWreckShare`, `SmithMendFromMarket`, `SpoilsQuartermasterRepair` (158).

### 2. Najwazniejsze decyzje projektu (slowami gracza i w skrocie)

- **156:** w ROT ges, kura, kot i pies leza w kategorii koni; przelicznik popytu tej kategorii liczyl sie z samego drobiu (/25) i mieszczanie przestali kupowac konie. Regula: przeliczone
  zwierze liczy sie do przelicznika kategorii tylko w kategorii bez przedmiotow na handel z cena nieprzeliczona - w "horse" (konie) popyt jak przed 144, drob kosztuje 4 / 1 d, na polce wazy dawne 50.
  Wol, krowa, swinia, owca - bez zmian. (Odrzucone: "srednia ze wszystkich sztuk" - dawala /1.11, czyli -10% popytu na konie, nie "jak przed 144".)
- **157:** kon najemnika: jest na polce kon tej rasy (ten sam przedmiot, potem ta sama kategoria + tier + rodzaj zwierzecia) - schodzi z polki, miasto dostaje jego cene polki; nie ma - najemnik
  na wlasnym koniu, placi sie tylko dni zoldu x2; wiecej najemnikow niz koni - nadplata wraca (lord, karawana, gracz). "Wlasny kon" (i caly komplet najemnika) dalej z niczego - do kroku
  "weterani". Gracz objety (menu zaulka i rozmowa w karczmie): konie z polki placi kasie miasta, dni zoldu gracza dalej w nicosc jak w grze.
- **158:** wrak = Mangled albo kazdy stan <= 0.10 (najgorsze stany RBM `*_damage_*` = 0.1, dotad naprawiane, bo regula byla "< 0.1"), na grzbiecie - takze stan w ksiedze <= 10%.
  Zaden kowal miasta za monete (z regula kowali i bez niej - polki wojska bez reguly braly wrak za 10% wartosci, decyzja 26.08 zmieniona); ludzie i AI tak samo; wrak to zlom (kowale biora
  wraki z polki jako metal - teraz takze stan 0.10) albo wlasne kowadlo. Kwatermistrz przy wylaczonym 134 ("Spoils jak dotad") - bez zmian.
- **159:** `CleanseAmmo` pomija `IsBeast` - kulawy kon zostaje kulawy; strzaly i towar jak dotad.

### 3. Proby poza gra (kopie w `towary3\dokonczenie`; PRZED = DLL poprzedniego ogniwa, PO = DLL ogniwa; na koncu wszystko na DLL z archiwum d618cf4)

| Proba | Przed | Po | Na DLL finalnym (a68d4a74) |
|---|---|---|---|
| wyspy (154) | 78/79 (dll-final c1e8bea - pulapka odtworzona) | 111/111 | 111/111 |
| cena sprzedazy (155): `paczka nowa` / `wylaczniki`, `porownaj.py`, `kontekst` | - | 11420/11420 linii = paczka (0 roznic), 59/59, 5/5 | to samo (0 roznic, 59/59, 5/5) |
| 127 (DOPASOWANIE: nowa sygnatura `PricePostfix`, `PROBA_B`) | - | B0 22/22 (dane = jak przed), B1 22/22 (25 cen pod sufitem) | 22 / 22 / 22 |
| konie stosu lawy + NOWE 17b (156) i 16b (157) | 136/136 -> 139/142 (17b: popyt 900 -> 36 d, polka koni /25) -> 142/142 (16b brak) | 142/142 -> **149/149** | 149/149 |
| lawa stosu + NOWE 18 (158) i 19 (159) | 133/133 -> 134/144 (10 FAIL 18) -> 146/147 (19: kulawe konie zdrowieja) | 145/145 -> **147/147** | 147/147 |
| zamowienia (145) | 130/130 | 130/130 (kazde ogniwo) | 130/130 |
| kwatermistrz r0 / r1 (134) | 52/52, 47/52 | 52/52, 47/52 (te same 5 FAIL co w sekcji 5: 4 z robocizna 25% na sztywno sprzed 135 i napis 'a quarter of the worth') | 52/52, 47/52 |
| 121, 122 on/off, 123 on/off, 124, 125, 128, 129, 130, 132 / wylacznik, 133, 134 r0, 146, 147, 148 (2), 150 | jak w sekcji 5 | bez zmian (132 wylacznik 54/55 - W8 zastane, jak przed) | bez zmian |

Dopasowania kopii prob (nie sprawdzianow): lista nazw ustawien +6; sygnatura `SupplyDemand.PricePostfix` (155) w kopiach 127 i 132; napis o wraku stary albo nowy (158); WZORZEC sekcji 2
stosu lawy (wszystko jak dotad) z `WrecksToScrap` wylaczonym; kroki 1-5 sekcji 16 (143) z `MercHorseFromShelf` wylaczonym. Skrypty: `nazwy.py`, `dop158.py`, `wstaw16b.py`, `sekcja16b/17b/18/19.cs.txt`.

### 4. Build i DLL (`towary3\dll-final-2`, z czystego `git archive` d618cf4 w `dokonczenie\archiwum`; gen_mcm w archiwum - McmSettings bez zmian; logi `dokonczenie\logi-final`)

Build `-c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs"` - **kod 0 x 5**: Armoury (stare CS0169), RealisticCaptivity (stare CS0618), GrandTourney, ForgeView (stare CS0105/CS0618), CrashScribe.

| Plik (`dll-final-2`, md5.txt) | md5 | Wgrac? |
|---|---|---|
| Armoury.dll | **a68d4a740329a5a3d8e29c480394f068** | TAK (w grze 70b29477) |
| GrandTourney.dll | 1337433c1f87410b1319f50efeb63a71 | TAK (= dll-final; w grze 0aa5d0ef) |
| RealisticCaptivity.dll | 3e04b89b8839b95843dc178e459b983f | TAK (= dll-final; w grze c393f4fb) |
| CrashScribe.dll | 3588f9b998f6f8c4e00bed1afeca7f79 | NIE (bez zmian w lancuchu; w grze zostaje 11fa0214) |
| ForgeView.dll | 382f257376941d8da95f4256d46beb12 | NIE (bez zmian w lancuchu; w grze zostaje b058c7d2) |
| arm_map_villages.tsv | b742f307bb26b37134c686a9693a3c11 | juz w grze (ten sam plik) |

Kontrola przed wgraniem: `git diff t3-sklad <galaz wgrania> -- Armoury GrandTourney RealisticCaptivity` pusty + build kod 0; autotest 40 dob (NOWA kampania) przed "wgraj".

### 5. Armoury.json przy wgraniu (gra zamknieta, najpierw `Armoury.json.bak`) - bez zmian wobec sekcji 6

DOKLADNIE trzy klucze (127): `"SmithingSkillPerTier": 45 -> 35`, `"DurabilityPerArmorPoint": 20.0 -> 13.33`, `"MinSellPercentOfValue": 5 -> 2`; kontrola grep -> 35 / 13.33 / 2 (stan pliku Jeffa
sprawdzony 08.10: 45 / 20.0 / 5, plik z 05.10). Paczki 154-159 i W2 v4 NIE dodaja edycji: nowych kluczy nie dopisywac (brak = domyslne z kodu, wszystkie wlaczone; `SellCapPercentOfNewAsk` = 10);
`TroopMendWreckShare` (0.1 w pliku) zostaje - przy `WrecksToScrap` dotyczy juz tylko sztuk niebedacych wrakami. GrandTourney.json, RealisticCaptivity - nic.
**Cofniecie:** DLL do zatwierdzonych z gry (Armoury **70b29477**, GT 0aa5d0ef, RC c393f4fb) i Armoury.json z `.bak` (albo 45 / 20.0 / 5).

### 6. Klucze zapisu gry

154-159 i W2 v4 - **zadnego nowego klucza** (`SyncData` bez zmian; 157 - tylko liczniki doby w pamieci, czyszczone `RecruitCost.Reset` w konstruktorze ArmouryBehavior; 154 - stan w pamieci,
czyszczony w `CartTownExit.Reset`). Caly lancuch t3 jak w sekcji 7: NOWE `arm_mendstock`, `arm_towncrafts`, `arm_armyclothing`; zmieniona tresc `arm_condition`, `arm_daypass`, `gt_events`.

### 7. Otwarte / do wiadomosci

1. **Narzedzie logow** (`naprawa\narzedzia\sprawdz_logi.py`, poza galezia) nie zna nowych linii: "Wyspy i drogi", "Kara handlowa BK", "Skup sprzetu (doba)", "Konie rekrutow (157)";
   linie startowe, ktore czyta (143 "RecruitCost: kon rekruta po cenie targu", 144 "HistoricalPrices: zywy inwentarz"), maja ten sam poczatek (dopisany tylko koniec). Probka 8 linii
   "RecruitCost: kon rekruta X ..." (143) usunieta - zastapiona linia dnia.
2. **157 - poza zakresem, znalezione przy kontroli calosci:** ochotnik konny od notabla, ktory konia nie kupil (tier 1 z wlasnym dobytkiem, albo kon odziedziczony z nizszego szczebla
   puli), placi notablowi cene konia z targu (od 143; przed 143 stala 150 / 500) - zloto lorda -> notabl za konia, ktorego nikt nie kupil. Kon rekruta garnizonu: koszt klanu bez odbiorcy
   (jak w grze). Najemnik gracza: dni zoldu w nicosc (jak w grze). Do decyzji / kroku "weterani".
3. **158 - skala:** stan 0.10 dostaje kazda sztuka zuzyta ponizej 20% i najgorszy lup - mniej napraw AI (mniej zlota do kas miast za naprawy), wiecej wrakow jako zlom; zmierzyc w autotescie
   ("Zuzycie AI", "TroopSelfMend", "Material napraw z targu"). Uboczne: `lame_horse` (0.1) jest "wrakiem" - naprawa ludzi bez materialu nie leczy juz kulawych koni (dobrze, zgodnie z 159).
4. **156:** ges i kura dziela budzet kategorii z konmi - przy cenie 4 / 1 d miasto moze zjesc wiecej sztuk drobiu niz przed 144 (do pomiaru w ksiedze towarow, linie "Towary: goose / chicken").
5. **155:** kupno sprzetu poza polka miasta (wies, karawana, `Town.GetItemPrice` bez kupca - m.in. wraki-zlom w `MendMaterial`) tanieje o ok. 18% (kara BK raz) - opisane w 155.
6. Autotestu w grze NIE bylo (zadanie: nic nie wgrywac). Nastepny krok: autotest 40 dob na dll-final-2 (NOWA kampania), odczyt linii z sekcji 9 + nowych: "IslandRoads: wpiete",
   "Wyspy i drogi", "Kara handlowa BK: x5.0", "Skup sprzetu", "HistoricalPrices: ... (bez horse /25)", "zywy inwentarz ... poza przelicznikiem ...: goose [horse], chicken [horse]",
   "Konie rekrutow (157 ... CZYNNY)", "Werbunek: ... za najemnikow do miast" (na najemnika ok. 290 przy pustych polkach koni), podpowiedzi kowala z "wrecks (Mangled, or worn to a tenth ...)".

## Recenzja dokonczenia 08.10 - niezalezny przeglad (A)-(F) + W2 v4: galaz t3-sklad = 297320e

- Klon `scratchpad\lancuch`, galaz **t3-sklad = 297320e** (worktree `towary3\repo`; commity zrobione w nim, `rev-parse t3-sklad` = HEAD). NIC nie wgrane do gry, NIC nie wypchniete,
  repo Jeffa i `Armoury.json` tylko czytane. Kopie prob recenzenta i wyniki: `towary3\recenzja-dokonczenia` (katalogi prob autora nietkniete; WYNIK sprzed recenzji: `recenzja-dokonczenia\WYNIK-przed-recenzja.md`).

### 1. Werdykt dla kazdego punktu

| Punkt | Werdykt | Co sprawdzone |
|---|---|---|
| (A) 154 wyspy | OK | cherry-pick co do znaku jak 90234dc (rozni sie tylko etykieta NN -> 154); CartTownExit / RoadMemoryFix w lancuchu = baza paczki c92f7e7 (roznia sie tylko etykiety 130 / 131); proba wysp na DLL koncowym 111 z 111 |
| (B) 155 cena sprzedazy | OK | proba paczki na DLL koncowym: `nowa` i `wylaczniki` 11420 / 11420 linii identycznych z autorem, `porownaj.py` 59 z 59, `kontekst` 5 z 5; p127 (B0 / B1) 22 / 22 / 22; styk z 157 (cena konia najemnika = cena KUPNA z polki - 155 jej nie rusza) |
| (C) 156 popyt koni | OK | regula sprawdzona w kodzie (`HistoricalPrices.Apply` krok 3, `MixedShelf`: kategoria "horse" bez przelicznika, drob z waga 50); dane ROT z logu 14:18 (ges, kura, kot, pies w "horse"; w SandBoxCore - "animal"); proba koni 17b OK |
| (D) 157 kon najemnika | **BLAD - poprawiony** | patrz 2.1 |
| (E) 158 wraki na zlom | **luka - poprawiona** | patrz 2.2; reszta drog (lawa, polki wojska z regula i bez, uprzaz, ludzie, AI, kwatermistrz 134, zlom z polki) - zgodna z decyzja 07.10 |
| (F) 159 CleanseAmmo | OK + **luka obok - poprawiona** | patrz 2.3 |
| W2 v4 | OK | `MapVillagesView.cs` i plik danych co do bajtu = 85215de (git diff pusty); 10239d9 + 85215de dotykaja tylko tych 2 plikow; STAN-PRAC 08.10: "TOWARY 3 musi przy skladaniu dostac W2 85215de" |

### 2. Znalezione i poprawione (kazde osobny commit, wpis na gorze CHANGELOG bez Status, dopisek w `docs/paczki/NNN-*.md`)

| Commit | Co |
|---|---|
| 7020ea0 | **157: poprawka po recenzji** - kon najemnika: zaplata i zwrot w kwocie, ktora kupujacy naprawde zaplacil za konia |
| 0f43e2c | **158: poprawka po recenzji** - "Pick a piece" na grzbiecie liczy wrak jak "Mend everything you wear" |
| 297320e | **159: poprawka po recenzji** - kulawy kon nie zdrowieje takze u kowali miasta (TroopSelfMend) |

1. **157 (zamknieta ekonomia - zloto z niczego / kasa miasta na minusie).** Gra liczy koszt najemnika (dni zoldu + kon) x (1 + suma mnoznikow kupujacego): perki gry (Slick Negotiator, Sword for Barter, Frugal), BK - kultura -5%, ranga klanu -5..+15%, obca kultura w krolestwie +25%, prawa Drafting +50 / +100% (dekompilacja `DefaultPartyWageModel`, `BKPartyWageModel` :347-410). 157 placil miastu za konia z polki i oddawal za brakujace konie SAMA cene polki. Proba (sekcja 16c): mnoznik 0.75 - miasto oddaje lordowi 360 zl za konie, ktorych nie bylo; 0.5 i 10 najemnikow - kasa miasta **-2080**; 1.35 - miasto zatrzymuje 504 zl doplaty za nieistniejace konie; gracz z rabatem 0.75 - **180 zl z niczego** przy 3 najemnikach (rosnie z liczba najemnikow i cena konia). Do tego werbunek bez latki miejsca (stala gry 150 / 500 w koszcie) dalej placil miastu za konia z niczego. Poprawka: kon w koszcie = koszt ze sprzetem - koszt bez sprzetu (`RecruitCost.HorseShareOfCost`, ten sam model i kupujacy) - ta kwota do kasy za konia zdjetego z polki i ta sama z powrotem za konia, ktorego nie bylo; stala gry bez targu - cala wraca. Przy mnozniku 1 co do grosza jak 157.
2. **158 (dwie reguly jednego zjawiska).** Na grzbiecie wrakiem jest takze sztuka ze stanem w ksiedze <= 10% (`HarnessWreck` w "Mend everything you wear"), ale "Pick a piece" dla [EQUIPPED] patrzyl tylko na modyfikator - a bron i tarcze maja najgorszy modyfikator zuzycia 0.3 (nie wrak). Proba (sekcja 18j): miecz 8% w ksiedze - kowal odnawial go do 100% za 6 / 23 zl. Poprawka: `ArmouryBehavior.SlotWreck` = ta sama regula co uprzaz, w oknie, wykonaniu i podpowiedzi "Pick a piece".
3. **159 (kon zachowuje stan na kazdej drodze).** W grze RBM nadpisuje `lame_horse`: price_factor **0.5** (`Modules\RBM\ModuleData\RBMCombat_item_modifiers.xml`; Native 0.1) - opis 159 / WYNIK ("lame_horse 0.1 jest wrakiem, ludzie juz go nie lecza") byl bledny. `TroopSelfMend` nie mial filtra konia: przy wylaczonych naprawach z materialem (`MendMaterialMenAndLords` / `SmithMendFromMarket` = off) kowale "leczyli" kulawego konia w zbrojowni wojska za 1/4 utraconej wartosci (proba 19b: kon 900, zdrowy za 112 zl). Poprawka: `Mendable` i `Run` pomijaja `IsBeast`. Przy domyslnych ustawieniach (naprawy z materialem - kon bez receptury) w grze bez zmian.

### 3. Proby (PRZED = DLL autora d618cf4 / DLL poprzedniego kroku, PO = DLL koncowy z archiwum 297320e; kopie prob z nowymi sekcjami w `recenzja-dokonczenia\proby`)

| Proba | Przed | Po (DLL koncowy e9c47794) |
|---|---|---|
| konie stosu lawy + NOWA 16c (S8-S12, mnoznik kupujacego) | 149 z 154 (5 FAIL - S8-S12) | **154 z 154** |
| lawa stosu + NOWA 18j (Pick a piece na grzbiecie) | 147 z 149 (2 FAIL) | **150 z 150** (z 19b) |
| lawa stosu + NOWA 19b (TroopSelfMend, kulawy kon) | 149 z 150 (1 FAIL) | **150 z 150** |
| zamowienia (145) | 130 z 130 | 130 z 130 |
| kwatermistrz r0 / r1 | 52 z 52, 47 z 52 | 52 z 52, 47 z 52 (te same 5 starych FAIL) |
| wyspy (154) | 111 z 111 | 111 z 111 |
| cena sprzedazy (155): nowa / wylaczniki / porownaj / kontekst | 11420 / 11420, 59, 5 | identycznie (0 roznic linii z autorem), 59 z 59, 5 z 5 |
| p121-p134, p127 B0/B1, k1-k4 (caly zestaw p1 autora, DLL podmieniony) | jak w sekcji 3 dokonczenia | identycznie (132 wylacznik 54 z 55 - W8 zastane) |

### 4. Build i DLL (`towary3\dll-final-2`, md5.txt; z czystego `git archive 297320e`; gen_mcm w archiwum - McmSettings bez zmian x 3; build kod 0 x 5, tylko stare CS0105 / CS0169 / CS0618)

| Plik | md5 | Wgrac? |
|---|---|---|
| Armoury.dll | **e9c47794b2afc624d711e141ea451cbc** | TAK (w grze 70b29477) |
| GrandTourney.dll | 1337433c1f87410b1319f50efeb63a71 | TAK (w grze 0aa5d0ef) |
| RealisticCaptivity.dll | 3e04b89b8839b95843dc178e459b983f | TAK (w grze c393f4fb) |
| CrashScribe.dll | 3588f9b998f6f8c4e00bed1afeca7f79 | NIE (bez zmian, w grze 11fa0214) |
| ForgeView.dll | 382f257376941d8da95f4256d46beb12 | NIE (bez zmian, w grze b058c7d2) |
| arm_map_villages.tsv | b742f307bb26b37134c686a9693a3c11 | juz w grze |

Armoury a68d4a74 (przed recenzja) lezy w `towary3\dll-final-2-przed-recenzja` - NIE do wgrania.

### 5. Armoury.json przy wgraniu - bez zmian wobec dokonczenia

Gra zamknieta, najpierw `Armoury.json.bak`; DOKLADNIE trzy klucze z 127: `"SmithingSkillPerTier": 45 -> 35`, `"DurabilityPerArmorPoint": 20.0 -> 13.33`, `"MinSellPercentOfValue": 5 -> 2` (stan pliku 08.10: 45 / 20.0 / 5). Poprawki recenzji nie dodaja ustawien (Armoury 677, GT 40, RC 104); nowych kluczy 154-159 nie dopisywac (brak = domyslne, wszystkie wlaczone). GrandTourney.json, RealisticCaptivity - nic. Cofniecie: DLL 70b29477 / 0aa5d0ef / c393f4fb + Armoury.json z `.bak`.

### 6. Klucze zapisu gry

Poprawki recenzji - zadnego nowego klucza (`SyncData` bez zmian). Caly lancuch t3 jak dotad: NOWE `arm_mendstock`, `arm_towncrafts`, `arm_armyclothing`; zmieniona tresc `arm_condition`, `arm_daypass`, `gt_events`.

### 7. Do wiadomosci / decyzji (nie poprawiane)

1. **158 - skala:** `ArmouryBehavior.PickWornModifier` (lup i zuzycie AI) daje przy stanie losowanym <= 30 (LootWearBase 45 +- 25 - ok. 1 sztuka na 5) najgorszy modyfikator grupy; dla zbroi grup RBM (plate / chain / leather / cloth) to `*_damage_*` = 0.1, czyli od 158 wrak. RBM nazywa czesc z nich lekko ("chain_damage_95 = Scratched", pancerz -5), ale mod liczy stan z ceny (0.1 = 10%, kara pancerza ok. -80% juz dzis). Wynik: ok. 1 na 5 zuzytych zbroi z lupu i u AI idzie na zlom zamiast do kowala. Zgodne z litera decyzji ("stany <= 0.10"); do obejrzenia w autotescie.
2. **157 - cena konia przy wielu najemnikach naraz:** koszt jednego werbunku liczy sie raz, z najtanszego pasujacego konia; gdy schodzi kilka koni, kolejny moze byc inny (ta sama kategoria / tier) i miec inna cene polki - miasto dostaje cene pierwszego (roznica kilkudziesieciu zl w obie strony, bez zlota z niczego).
3. **157 - perk = rabat u miasta:** przy mnozniku kupujacego miasto dostaje za konia cene polki x mnoznik (perk gracza obniza zaplate miastu, prawo BK ja podnosi) - jak kazda doplata w koszcie werbunku gry.
4. Z dokonczenia (bez zmian): narzedzie logow nie zna nowych linii; ochotnik konny z koniem "odziedziczonym" placi notablowi; kon i komplet najemnika "z wlasnym koniem" z niczego - krok "weterani"; drob w budzecie kategorii koni (156); kopia WYNIK/SKLAD w repo Jeffa `docs/towary3` nieaktualna; CHANGELOG 154-159 + W2 v4 + 3 poprawki bez Status.

### 8. Autotest 40 dob (NOWA kampania, DLL z `dll-final-2`) - co sprawdzic w logu

- Start: "IslandRoads: wpiete - rozkazy BK bez drogi ladowej ...", "Kara handlowa BK: x5.0 (raz; zdjety dubel z BKROTPatch.Models.BKROTPriceModel)", "Skup sprzetu: ... wpiety", "HistoricalPrices: popyt miast przeliczony ..." BEZ "horse /25" i "zywy inwentarz ... poza przelicznikiem ...: goose [horse], chicken [horse]", "RecruitCost: ... zakup gracza wpiety w 2/2 metodach"; ERROR "BkPenaltyOnce" / "SlotWreck" / "RecruitCost.PlayerBuyPostfix" = 0.
- Co dobe: "Werbunek: ... za najemnikow do miast X" - na najemnika blisko ok. 290 (nie 479), nigdy ujemne; "Konie rekrutow (157, ... CZYNNY)": "kon z polki K (miastom za konie G ...)", "z wlasnym koniem bez doplaty M", "nadplata ... zwrocona R", "bez targu (stala gry) F" (oczekiwane 0); "Wyspy i drogi (IslandRoadsFix CZYNNE)" z "wpiete: ocena miast BK TAK, wybor celu BK TAK, uczta TAK, gentry TAK, droga w strazniku TAK" - karawany w Pyke / Lord Hewett's Town nie stoja dluzej niz kilka dob, lordowie bez "stoja ... najdluzej" > 2 doby; "Skup sprzetu (doba)" - lordowie AI i sakiewki ludzi z podloga / sufitem; "Zuzycie AI: ... naprawione w miastach N" - spadek wobec 14:18 (1395 / dobe) o czesc wrakow, "Material napraw z targu" - wraki-zlom zdejmowane z polek.
- Po 40 dobach: konie na polkach miast blisko 9 000 (nie 12 953), cena konia jak bez 144; "Przeplywy osad" - zakupy mieszkancow do kas miast i zamkow jak w autotestach bez 144; "Towary: goose" / "Towary: chicken" - zuzycie miast (drob w budzecie koni); Zelazny Bank - dlug i bankructwa ponizej 14:18 (198 tys. / 1).
- Gracz (jesli autotest gra postacia): "Pick a piece" przy zalozonym wraku - "The smiths will not restore a wreck for coin"; kulawy kon zostaje kulawy po bitwie i po wczytaniu; najemnicy konni w karczmie: z koniem na targu - kon znika z targu, kasa miasta + tyle, ile zaplacono za konia.
