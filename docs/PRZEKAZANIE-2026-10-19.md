# PRZEKAZANIE 19.10 (wieczor) - dla drugiego konta / nastepnej sesji

Limit tygodniowy pierwszego konta: 98% (reset 23.10 ok. 20:00 PDT). Ten plik + `docs/STAN-PRAC.md` (### 91-93) + gora `CHANGELOG.md` wystarcza do kontynuacji.

## Co jest w grze Jeffa

- **WGRANIE 87** (19.10 ok. 10:45 PDT) = `w-toku/noc-wgranie-87` (noc-ks4 fe9704aa + CHANGELOG), Armoury **3468643a**, CrashScribe f98aac55, RealisticCaptivity 8dbedff3, GrandTourney 87465d27.
- **WGRANIE 88 - WGRANE** 19.10 ok. 13:00 PDT (test 5 dob + bitwa 0 ERROR, latki 11/11, 3/3, arakh 5 -> 0; galaz `w-toku/noc-wgranie-88`): `w-toku/noc-ks4` **855a9b61** (Armoury **6d0fd3e4**, 2012 ustawien) = 87 + f28-wyceny (ilosci w zadaniach gry i XP Lotrostwa od wartosci sprzed przeliczenia cen; klucze Hist Quest Qty Old Coin, Hist Roguery Xp Old Value) + f29-arakh (pary krewne GearFit na poziomie przedmiotu; klucz Gear Fit Kin Item Level). Test 5 dob + bitwa x20 (fin101) w toku. Jesli w STAN-PRAC nie ma wpisu "WGRANIE 88" - sprawdzic md5 Armoury.dll w grze (6d0fd3e4 = wgrane, 3468643a = nie).
- Kopie: `*.bak-<data>-wgranieNN-przed` obok plikow + `D:\Backup-Bannerlord\wgrane\<data>-wgranieNN-przed`; zapisy Jeffa `D:\Backup-Bannerlord\zapisy-jeffa\2026-10-19-po-sesji-86` i `-po-sesji-87`.

## Dzisiejsze wgrania (84-88) w skrocie

- 85: f24 - skarbiec doplaca braki dnia rat Banku 186 / kontraktow 185 / wezwan 168 (Ds War Grant Treasury Pays Dues); checker NAKUM-18 bez ujscia.
- 86: f25 4b (Ds War Grant Treasury Covers Dues - skarbiec niesie tez stale rachunki w sesji DS), f26 5C (Ks Land Gate Exact Loss), checker 2a/3c, NKS-30b Wolni Ludzie prog 0.60 (Jeff "zostawnmy a"; f26-wolni NIE scalone).
- 87: f27 - punkty badan czesci kuzni od wartosci sprzed przeliczenia (Hist Research From Old Value). Potwierdzone w grze Jeffa: 9 nowych czesci w 15 min.
- 88: patrz wyzej.

## Sprawdzone w grze Jeffa (log 19.10 09:52)

Przywrocenie wlosci z ksiazki 7/7 (KL -> Joffrey, armia Robba 9455 pod Harrenhal, zwrot 1 125 434), sojusze Dragonstone+Riverlands i Straz+Skagos zerwane, 0 ERROR. Do dnia 273 KL nieoblegana, zero zmian wlascicieli.

## Otwarte - kolejnosc po resecie limitu

1. **Crash 19.10 10:55** (Jeff: "zostawmy to na razie"): gra stanela 1-2 s po zamknieciu 3. okna CraftPopup w 9 s (kuznia BK wydaje miecze), bez raportu gry i CrashScribe, proces zyl 2 min. Podejrzany CraftPopup (Gauntlet layer, ItemTableau). Obejscie dla Jeffa: MCM Armoury "Craft Result Popup" off. Nie zmieniac kodu bez dowodu.
2. **Towarzysze kupuja sprzet za wlasne zloto** (Jeff 19.10: "placilem towarzyszowi 2500 - czemu nie kupi sobie lepszego sprzetu, tak jak armia?"). Nowa funkcja pod nowym kluczem. Do ustalenia z Jeffem: czy podmieniac sprzet, ktory dal gracz (raczej nie - tylko puste/gorsze sloty), komunikat, limit wydatkow. Sprawdzic w kodzie gry, gdzie trafia zloto towarzysza przy dolaczeniu (Hero.Gold).
2b. **Zold towarzyszy wedlug poziomu** (Jeff 19.10, doslownie: "czemu bohaterowie biora 100 zlota miesiecznie np. w savie Cassel ... to przeciez jakis absurd" -> "Powinni brac per poziom, i dopiero im wyzszy tym duzo drozej"). Dzis: zold bohatera poza wodzem liczy BK (losowany; nasz straznik losowania tylko go trzyma - linia "bohater poza wodzem (zold BK losowany)"); Cassel (wedrowiec ROT) ok. 100/mies. Do zrobienia pod nowym kluczem: zold towarzysza rosnacy z poziomem, wyzej wyraznie stromiej (np. wykladniczo), w skali historycznej (1 d = 1 pens; zolnierz kilka d/dobe); zloto do sakiewki towarzysza (zamkniety obieg) - razem z p. 2 (towarzysz kupuje sprzet za wlasne zloto). Najpierw sprawdzic w kodzie BK/gry, skad dzis 100 (BKPartyWageModel / DefaultPartyWageModel) i gdzie Jeff to widzi.
2c. **Zold towarzyszy - dane** (log 13:46 "Towarzysze (C)"): zold/d mediana 98.2 (5.0-227.6), poziom mediana 19 (1-30) - to zold DZIENNY w skali gry, nie miesieczny; cena najmu = 20 dni zoldu (nasza regula); zolnierz sredni ok. 4.6 d/dobe (Zold 575131 / ok. 124 tys. ludzi). Jeff: "mam expenses".
2e. **ZOLD TOWARZYSZY - DECYZJA JEFFA (19.10, "tak wgraj to do przekazania") - PIERWSZE ZADANIE PO RESECIE LIMITU.** Jeff ma "Main party wages 109" przy jednym towarzyszu (Cassel, poz. ok. 19, ok. 100 d/dobe) - zold bohaterow liczy BK w skali gry, zolnierze sa w skali historycznej (ok. 4.6 d/dobe). Zatwierdzona tabela dziennego zoldu towarzysza wedlug poziomu (interpolowac miedzy punktami, stromo w gore): poz. 5 = 3 d, 10 = 8, 15 = 15, 20 = 25, 25 = 40, 30 = 60 (poz. 1 ok. 1). Cena najmu zostaje 20 dni zoldu (regula "Towarzysze (C)" - Cassel ok. 450 zamiast 2500). Nowy klucz MCM, zloto z kiesy gracza do sakiewki towarzysza (zamkniety obieg, razem z p. 2 - towarzysz kupuje sprzet za wlasne zloto). Najpierw w dekompilacji: skad BK bierze zold bohatera (BKPartyWageModel / DefaultPartyWageModel.GetCharacterWage, "zold BK losowany" - Armoury PerfD2 tylko pamieta wynik), czy ten sam model liczy towarzyszy w miastach i w rodzie gracza, i jak MenPurse.WageRaw to uzywa. Test 5 dob + bitwa, linia logu z zoldem towarzyszy gracza przed/po.
2d. **Niewola gracza** (Jeff 19.10: "wypuscili mnie ze zlotem i pancerzem ... i bronia i koniem; zabrali wszystko inne"): to regula C4 RealisticCaptivity (PlayerLosesLikeAiLord): rynsztunek zostaje na graczu, zloto tylko lup bitwy 5% (max 10 000), tabor caly do zdobywcy (RC log Modules/RealisticCaptivity/RealisticCaptivity-2026-10-19_13-00-24.log 13:31:55). Jeff chce, by zdobywca zabieral tez rynsztunek/kon/zloto - do ustalenia (wylaczenie C4 w MCM -> StripEquipment; sprawdzic zwrot przy odbiciu i zloto).
3. **Arakhy - decyzje Jeffa** (f29 CHANGELOG): f29 zamyka droge przez pary krewne (Dorne, Zelazne Wyspy, najemnicy neutral, bandy), ale arakh dalej wolno nosic wojskom Essos (regula Jeffa 16.09 "ta sama strona morza") i bandy w Westeros trzymaja arakhy z lupu w zbrojowni. Zapytac: arakh tylko dla Dothrakow (khuzait)? lup arakhow u band? Zapytac tez, gdzie Jeff je widzial (wrog/swoi/sojusznicy, jaka kultura).
4. **Reszta audytu wycen** (`docs/AUDYT-WYCENY-2026-10-19.md` na noc-ks4): Spoils prog 5000, XP Handlu z magazynu warsztatu, AI sprzedaz lupu, kryjowka nocna, BK Acquire Book x30, drobne.
5. **NAKUM-7 (glowy rodow +45 tys./d) i NKA-1 (zaleglosci DS)** - tylko w biegach x20 (fin98, fin99); bieg porownawczy x8.
6. Okolica tkalni aksamitu - linia logu z indeksem aksamitu wedlug miasta.
7. A7 pelna zmiana zajec wiosek - osobny test 160 dob (decyzja 6b).
8. Wydajnosc - rozlozenie obliczen dnia na godziny (Jeff: pozniej).

## Jak testowac / wgrywac (sprawdzone 19.10)

- Gra musi byc zamknieta (tasklist: Bannerlord/BLSE). Zapisow Jeffa nie wczytywac (tylko autotest-F-d240).
- `T=...\6ba92574-15a3-4209-a065-ed325b84b1b4\scratchpad\test`: `bieg-bitwa.ps1 -DllDir <dir> -CsAt $T\CrashScribe-at-83.dll -Tree <worktree> -Days 160|5 [-Battles 1 -BattleSec 90] -LoadSave autotest-F-d240 -SwitchWaitSec 720 -Speed 20` (+ plik armoury-ks-auto.flag dla 160 dob, usuwac po biegu).
- **-Speed nigdy 16** (Better Time Ctrl+Spacja = 16 cofa mnoznik do 0 - czas stoi). x20: 160 dob ok. 33 min.
- Wgranie: `wgraj-v5.ps1 -DllDir <dir> -Tree <worktree> -Tag '<data>-wgranieNN'` (robi .bak + D: + md5).
- Worktree glowny: `...\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\n-ks4` (galaz w-toku/noc-ks4); repo lancuch `...\3cf3e0ac-...\scratchpad\lancuch` (origin = to repo, potem push na GitHub).
- Checker: `python -I tools/sprawdz_logi.py --grupa <g> <log>`; wyniki biegow w scratchpadzie sesji (fin97/fin98/fin99).
