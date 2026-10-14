# Kto zawiera pokoj? (bieg dwuletni 13/14.10, doby 449-513)

Oznaczenia: [P] pomiar z logu/kodu, [S] szacunek. Doba = "dzien N" CrashScribe = "dzien" Armoury - 108836 [P: T9 d33 Dragonstone-Stormlands = Armoury dzien 108869].
Nic nie zmieniano. Gry nie uruchamiano.

## 0. Odpowiedz dla Jeffa (prosto)

1. Pokoj zawieraja dwie rzeczy: **mod Diplomacy** i **glosowanie rodow**. Nasz CrashScribe i ROT zadnego z tych pokojow nie zawarly.
2. Z 13 pokojow fali: **8 wymusilo "zmeczenie wojna" Diplomacy** (wskaznik dobil do 100 - krolestwo przegrywajace musi podpisac, bez glosowania): Lys-Tyrosh, Lorath-Braavos, Dorne-Reach, Qarth-Targaryen, Aegon-Volantis, Norvos-Qohor, Ibben-Sarnor, Myr-Pentos [P].
3. **5 przeglosowaly rody** (zwykla propozycja gry, glosowanie): Braavos-Ibben, Dolina-Zelazne Wyspy, Dorzecze-Dolina, Dothrakowie-Targaryen, Smocza Skala-Myr - kazdy zaraz po 91 dobach wojny, bo tyle Diplomacy kaze czekac, zanim wolno w ogole proponowac pokoj [P].
4. Wczesniej te same pokoje tez sie zdarzaly co 1-3 miesiace, ale ROT tego samego dnia wypowiadal wojne od nowa. ROT ma dla tych 10 par (feudy Essos, Reach-Dorne, Dolina-Wyspy) wpisane "wojna od dnia 5 do dnia 100". Nasza fabula rozciaga daty x4.33 (rok 364), wiec wyszlo **od doby 22 do doby 433**. Po dobie 433 ROT juz nie wznawia - pierwszy pokoj po tej dacie zostaje na zawsze [P]. To nie blad, tylko koniec scenariusza ROT.
5. Nowych wojen nie ma, bo **AI prawie w ogole nie wypowiada wojen glosowaniem**: przez 2 lata 177 wojen, z tego 128 wznowil ROT, 37 przyszlo z sojuszy (wezwanie do wojny), 11 to bunty, a glosowaniem rodow - **1** (doba 62) [P]. Nasz rozejm 87 dni nie zatrzymal ani jednej propozycji, bo zadnej nie bylo [P].
6. Dlaczego AI nie proponuje wojny - **nie wiem na pewno** (log nie podaje powodu odrzucenia). Najbardziej prawdopodobne: krolestwo w pokoju rozpuszcza wojsko do kilku procent (nasz pulap zoldu w pokoju), a BK ocenia wojne bardzo nisko, gdy sasiad jest kilka razy silniejszy, i doklada kare "nie stac go na wojne" biednym glowom rodow [S].
7. Dzwignie: (a) przedluzyc wymuszanie tych wojen ROT przez nasza fabule, (b) zmeczenie wojna w Diplomacy (wylaczyc albo spowolnic), (c) wyzszy pulap zoldu w pokoju, (d) najpierw licznik "czemu AI nie wypowiada wojny", potem ewentualnie nasz "rozrusznik wojen". Szczegoly i ryzyko - rozdz. 4.

## 1. Kto zainicjowal kazdy pokoj fali

Sciezki do pokoju miedzy krolestwami AI (kod):
- Glosowanie: `KingdomDecisionProposalBehavior.DailyTickClan -> GetRandomPeaceDecision -> ConsiderPeace` (gra, `dec32/cs_full/TaleWorlds.CampaignSystem.CampaignBehaviors/KingdomDecisionProposalBehavior.cs:63-150, 257-287`) albo BK `ForceProposePeaceFromLosingSide`; Diplomacy blokuje wniosek przez `MakePeaceConditions` (min. czas wojny `MinimumWarDurationInDays` = **91** w pliku Jeffa `Configs/ModSettings/Global/Diplomacy/DiplomacySettings_v1.2.json`); wynik glosu -> `Diplomacy.Patches/MakePeaceKingdomDecisionPatch.cs:19-33` -> `KingdomPeaceAction.ApplyPeace`.
- Zmeczenie wojna: `Diplomacy.CampaignBehaviors/WarExhaustionBehavior.cs:261-265` (DailyTick) -> `WarExhaustionManager.ProcessWarExhaustion/ConsiderPeaceActions` (`Diplomacy.WarExhaustion/WarExhaustionManager.cs:1663-1690`): gdy `GetWarResult` = Loss/Tie, a to wymaga max(zmeczenie stron) >= 100 (`:575-582`) -> `KingdomPeaceAction.ApplyPeace(kingdom, enemy)` **bez glosowania i bez progu 91 dni**.
- Obie sciezki daja to samo zdarzenie gry (MakePeace `ByKingdomDecision`) i ta sama linie T9 (`StorylineReparations.cs:15-18, 37-38`). Rozroznia je linia E1 "decyzja o pokoju" - pisana na `KingdomDecisionConcluded` dla kazdej decyzji o pokoju w wojnie niefabularnej (`PovertyPeace.cs:688-724`). Brak tej linii przy pokoju niefabularnym = zmeczenie Diplomacy (inne mody nie wolaja pokoju - audyt CHANGELOG `zb/int/CHANGELOG.md:13384`; CivilWarBehavior Diplomacy dotyczy buntow).

| doba | para | T9 (CS session) | E1 "decyzja o pokoju" | dni wojny od ostatniego wypowiedzenia (Armoury E7/0) | kto |
|---|---|---|---|---|---|
| 449 | Tyrosh-Lys | L11333, reparacje 67 800 | brak | 46 | Diplomacy zmeczenie [P] |
| 451 | Lorath-Braavos | L11377, 139 500 | brak | 33 | Diplomacy zmeczenie [P] |
| 453 | Dorne-Reach | L11418, 343 300 | brak | 31 | Diplomacy zmeczenie [P] |
| 453 | Qarth-Targaryen | L11419, 149 500 | brak | 82 | Diplomacy zmeczenie [P] |
| 458 | Aegon-Volantis | L11531, 0 (Aegon zniszczony tego dnia) | brak | 210 | Diplomacy zmeczenie [P] |
| 461 | Norvos-Qohor | L11598, 66 200 | brak | 54 | Diplomacy zmeczenie [P] |
| 475 | Ibben-Sarnor | L11895, 67 000 | brak | 61 | Diplomacy zmeczenie [P] |
| 493 | Braavos-Ibben | L12299, 0 | L12300 POKOJ (5 popierajacych) | 93 | glos rodow, wniosek gry [P] |
| 505 | Dolina-Zel. Wyspy | L12564, 0 | L12565 POKOJ (12) | 93 | glos rodow [P] |
| 506 | Dorzecze-Dolina | L12591, 191 000 | L12592 POKOJ (14) | 94 | glos rodow [P] |
| 506 | Dothrakowie-Targaryen | L12594, 131 300 | L12595 POKOJ (14) | 92 | glos rodow [P] |
| 511 | Myr-Pentos | L12679, 58 600 | brak | 98 | Diplomacy zmeczenie [P] |
| 511 | Smocza Skala-Myr | L12702, 0 | L12703 POKOJ (11) | 98 | glos rodow [P] |

Kontrola licznikow E1 "od wczytania: decyzji o pokoju": d449 = 10 (L11337), d453 = 10 (L11423) - mimo 4 pokojow d449-453 licznik stoi [P]. E1b d449-455 (L11338-11466): "do glosowania: gra 0, BK 0, z biedy 0" [P]. Wnioskodawca glosowan: dobowe E1b (linia nastepnej doby) "do glosowania: gra 1" przy d494, d506, d507 (gra 4), d512 - wniosek gry, nie BK, nie nasz [P].
Linie Armoury (`Armoury-2026-10-13_21-14-13.log`, "Wojna i pokoj (E7/0)"): L172450, L173229, L173982, L173985, L175890, L177033, L182353, L189281, L193897, L194287, L194292, L196193, L196205 - wszystkie "POKOJ (przyczyna ByKingdomDecision)" [P].

Nasz udzial: **zaden** [P]. E1/E1b: "WNIOSKOW Z BIEDY 0" przez caly bieg (ostatnia E1b, "od wczytania"), w kazdej linii decyzji "rodow odwroconych na pokoj przez biede 0"; 16 krolestw fali nie bylo w biedzie skarbca (lista "krolestwa w biedzie" d440-720 - tylko bunty, Valyria, KL d480, Reach d600, Myr d720 0.64). T9 tylko zeruje reparacje w wojnach wymuszanych (od d449 te pary sa "niefabularne" - reparacje placone normalnie). Rozrusznik fabuly - nic nie zawiera; jego skala x4.33 tylko przesunela koniec wymuszania z d100 na d433.
Dlaczego akurat 91-98 dob: progu 91 dni Diplomacy nie da sie przejsc glosem wczesniej (E1b caly bieg: "This war hasn't gone on long enough to consider peace" 9162 odrzucen [P]); zmeczenie dochodzi do 100 szybciej (31-61 dob) w wojnach z bitwami/oblezeniami [S] (0.25/dobe samo z czasu = 400 dob; reszta z bitew 0.02/zabitego, oblezen 10, najazdow 3, smierci 5 - plik Jeffa [P]).

## 2. Dlaczego ROT przestal wznawiac (d422 -> d449)

- `dec37/rot/ROT.CampaignBehaviors/ROTStorylineWars.cs:64-80`: pary z `StartDay 5, EndDay 100`: Dolina-Zel. Wyspy, Reach-Dorne, Braavos-Lorath, Tyrosh-Lys, Pentos-Myr, Norvos-Qohor, Volantis-Aegon, Qarth-Targaryen, Dothrakowie-Targaryen, Sarnor-Ibben (10 par, 19 krolestw) [P].
- `ROTStorylineWar.cs:62-89` `Enforced`: false, gdy `ElapsedDaysUntilNow >= EndDay` ("War's end day has passed"); `EnforceWars` (DailyTick, `ROTStorylineWars.cs:197-219`) wypowiada tylko wojny `Enforced`; `IsWarForced` (`:179-190`) - ten sam warunek [P].
- CrashScribe `Fabula.cs:245-283` (`WarPrefix`): na czas `Enforced` StartDay/EndDay x `FabulaTimeScale` = 4.33 (`Config.cs:31`) -> start round(21.65) = **22**, koniec **433** [P]. Log CS L285: "Fabula: wojny fabularne x4.33 (StartDay/EndDay)". KRONIKA: wszystkie 10 par zaczyna sie d22; d433 startuje Zel. Wyspy-Polnoc (ROT 100-235 -> 433-1018) - ta sama skala [P, `kronika.out.txt`].
- Ostatnie wznowienia ROT tych par: d412 Dolina-Wyspy, d413 Pentos-Myr, d414 Dothrakowie i Ibben-Sarnor, d418 Lorath-Braavos, d422 Dorne-Reach (Armoury L158261, L158657, L159055-6, L160584, L162137) [P]. Pierwszy pokoj po d433 (d449) T9 klasyfikuje jako "wojna niefabularna" - czyli `IsWarForced` = false [P].
- Pary bez daty konca (Smocza Skala-Krainy Burzy, Wolni Ludzie-Straz, Dorzecze-KL) ROT wznawial dalej do konca biegu: T9 "fabularne 111, wznowione 111, NIE wznowione 0" [P].

## 3. Dlaczego krolestwa w pokoju nie wypowiadaja nowych wojen

Pomiary [P]:
- Wszystkie wypowiedzenia wojny biegu (Armoury E7/0, 343 zdarzen, `e70_zdarzenia.txt`): Default/ROT EnforceWars **128**, CausedByCallToWarAgreement (sojusze) **37**, CausedByKingdomCreation (bunty) **11**, CausedByKingdomDecision **1** (d62 KL na Smocza Skale, Armoury L23768; CS E1 "decyzji o wojnie 1"). AI glosowaniem nie wypowiada wojen w ogole - takze w roku 1, takze silne krolestwa.
- Nasz postfiks rozejmu 87 dni (E7/K, wpiety, Armoury L302): "propozycje wojny gry wstrzymane rozejmem: 0 (sesja 0)" w ostatniej linii (L279593) - zadna propozycja wojny nie doszla do konca `GetRandomWarDecision`.
- Wojsko w pokoju znika (budzet-rodow.csv, partie+zalogi): Lorath d440 3 036 -> d520 271; Tyrosh 3 695 -> 191; Lys 2 207 -> 207; Ibben 4 255 -> 361; Dolina 12 801 -> 5 365 -> d650 1 257; mediana krolestw w pokoju d520 1 046 vs w wojnie 4 368, d650 205 vs 5 280 (`sila.out.txt`). Pulap zoldu Lorath 21 708 -> 1 217 (`kiesy.out.txt`). Regula `ClanBudget.cs:25-27`: pokoj 0.28 D, wojna 0.60 D + ...
- Glowy rodow z kiesa < 50 tys. (BK "cannot afford a war" -0.8 x 600): Dorne 27-29 z 40, Dolina 11-12 z 21, Tyrosh 4 z 6 (d480-700), Norvos 5 z 6 (d480) i 5 z 5 (d520-600) [P liczby glow; skutek S].
- Nasza kara E1 (bieda skarbca) - nie dotyczy (zadne z 16 nie w biedzie, rozdz. 1).

Bramki propozycji wojny (kod), kolejno: wplyw rodu >= 100 i > koszt BK (200 + 0.2 x limit wplywu, `BKDiplomacyModel.cs:172-189`); los min(0.33, 1/(N+2)) na rod i dobe; losowe krolestwo z calego swiata (`KingdomDecisionProposalBehavior.cs:234-254`); `DeclareWarBarterable` >= 0 (prog BK = 0, `BKDiplomacyModel.cs:765-768`) = wynik BK `GetScoreOfDeclaringWar` (`:811-1000`); Diplomacy `DeclareWarConditions` (wplyw, pakt o nieagresji 364 dni, sojusz, rozejm 91 dni od ostatniej wojny tej pary, relacja wladcow >= 30 / przyjaciele / malzenstwa rodow - `Diplomacy.DiplomaticAction.WarPeace/DeclareWarConditions.cs`, `GenericConditions/BadRelationCondition.cs`); `CalculateSupport > 50`; los < 1.4 x prawdop. - 0.55; nasz rozejm 87 dni.
Ktora bramka zabija - **nie wiem** (brak licznika). Szacunek [S] wyniku BK dla krolestwa po demobilizacji (num3 = 600): sasiad 3x silniejszy -> "Difference in strength" ok. -250, "Enemy significantly stronger" -240, "Defensive stance" -720, bez casus belli -200, "cannot afford" -240/-480, odleglosc do -1200 -> razem ok. -1 400 do -3 000 przy dodatnich tylko "threat" i casus belli. Krolestwa w wojnie maja dodatkowo "Existing war with X". Wplyw rodow - nie wiem (log nie zapisuje stanu wplywu AI).

## 4. Dzwignie (nic nie zmieniono)

| dzwignia | liczby | skutek | ryzyko |
|---|---|---|---|
| A. CrashScribe: osobna skala konca wojen fabularnych ROT (np. EndDay x 8.66 -> 866 albo "bez konca" dla 10 par) | 10 par / 19 krolestw [P]; dzis koniec d433 | ROT wznawia jak do d433; pokoj Diplomacy trwa dobe; T9 dalej zeruje reparacje | wieczne te same feudy, 111+ "pokojow na dobe" [P]; wojsko i zold wojenny w Essos caly czas; inna os niz reszta fabuly (dzis jedna skala 4.33) |
| B. Diplomacy: zmeczenie wojna (plik Jeffa) | wylaczenie zabiera 8 z 13 pokojow fali [P]; zostaja glosy po 91 dobach (5 z 13) [P]; polowa stawek -> ok. 2x dluzej do 100 [S]; `MinimumWarDurationInDays` 91 -> 182 opoznia glosy [S] | wojny trwaja dluzej, nie wiecznie | dotyczy tez wojen gracza; zmeczenie to jedyny hamulec wojen bez decyzji; plik w folderze uzytkownika - wedlug pamieci "probowac raz, z kopia" |
| C. Armoury `PeaceWageShare` 0.28 D (np. 0.40-0.45) | dzis partie rodow z wojny do pokoju 38 125 -> 1 486 ludzi [P, raport wojsko] | wiecej wojska w pokoju, mniejsza kara sily w ocenie BK [S] | wiecej zoldu z kies (kiesy w pokoju rosly 26.4 -> 29.0 mln [P]); czy AI wtedy wypowie wojne - nie wiem |
| D. Najpierw licznik "propozycja wojny" (jak E1b dla pokoju): wywolania `GetRandomWarDecision`, pierwsza niespelniona bramka, srednie czlony BK | tor S, tylko odczyt | odpowiedz na pyt. 3 z [P] | koszt: kilkadziesiat wywolan na dobe |
| E. Po D: nasz "rozrusznik wojen" (wniosek DeclareWarDecision dla krolestwa w pokoju > 182 dob, cel - sasiad, glosuja rody) | 16 krolestw x 220-277 dob pokoju [P] | nowe wojny z woli rodow | tor L; spirala wojen, kolizje z Diplomacy/BK/ROT; obchodzi bramki BK |

Pliki: `kronika.py/.out.txt` (KRONIKA WOJEN -> poczatki/konce), `e70_zdarzenia.txt` (Armoury E7/0), `kiesy.py/.out.txt`, `sila.py/.out.txt` (budzet-rodow.csv). Logi: `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-13_21-14-11.log`, `Armoury-2026-10-13_21-14-13.log`, `Logs\2026-10-13_21-14-13\budzet-rodow.csv`.
