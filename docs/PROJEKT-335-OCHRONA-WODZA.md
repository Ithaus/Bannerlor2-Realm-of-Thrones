# Paczka 335 - ochrona wodza (mapa i bitwa)

Galaz `noc/335-ochrona-wodza` od `noc/333-ai-bitwy` (a7e623fb). Skarga Jeffa 06.11 (gra od 11:51, log `Armoury-2026-11-06_11-51-21.log`):

1. MAPA: "zblizam sie do Tywina, to inni wokol niego uciekaja zamiast bronic glowy rodu, choc maja przewage liczebna".
2. BITWA: "Tywin jezdzil sam wokol wojsk jak idiota, dopadlem go i zabilem; nie mial kawalerii - jak nie ma obstawy, dowodzi z tylu blisko swoich,
   zaatakowany wycofuje sie do swoich, a jego wojska biegna go chronic".

## 1. Diagnoza (skrot - pelna w zadaniu workflow 335)

### Mapa
- Gra nie zna "obrony sojuszniczego lorda": `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` liczy przewage lokalna kazdej partii osobno
  (wlasna sila + sojusznicy w 2R od wroga z waga b), a partia wolniejsza od wroga dostaje wynik ataku 0 (num5 = 0), chyba ze wrog jest w 2.5 jedn.
- Sojusznik liczy sie do przewagi przy celu = gracz tylko, gdy `ShouldConsiderAttacking(sojusznik, gracz)`; 223 (`LordEscort.AttackPostfix`) i p9
  (`ArmsFirst.AttackPostfix`) daja "nie zaczepiaj" slabym R1-R3 - Tywin (0.29 wypelnienia), Melwyn, Lancel znikali z przewagi pozostalych.
- `GetNearbyPartyToFlee` - ucieczka tylko do JEDNEGO silniejszego sojusznika w 11.25 jedn.; Lancel (77) slabszy, Melwyn za daleko - Tywin uciekal sam.
- Grupa Lannisterow (ok. 550-710 sily) byla silniejsza od Jeffa (ok. 460), zaden osobno.

### Bitwa
- RBM `CanTeamHaveGeneralsFormationPatch` = zawsze NIE: brak szyku wodza, `BehaviorGeneral` i obstawy (`BehaviorProtectGeneral`).
- RBM `ManageFormationCounts`: kazdy konny bez broni dystansowej (takze wodz) do szyku 2 (Cavalry) - Tywin w 1-3 osobowym szyku jazdy z rozkazami calej
  jazdy (skrzydlo, szarza, FrontalCavalryCharge), z dala od piechoty.
- 149 (`HeroCaution`) nie zna wodza: "samotny" dopiero przy 2 wrogach w 10 m (Jeff byl jeden) - "samotni 0"; ranny ponizej 45% jechal sam przez pole
  do glownej sily i padl w drodze (318c: nieprzytomny 100 s po rozstawieniu).

## 2. Liczby - wszystkie z istniejacych modeli

| Liczba | Skad |
|---|---|
| obstawa = kandydaci / 10, najwyzej 20 | `GeneralsAndCaptainsAssignmentLogic` (gra) - eskorta konna od 10 jezdzcow, straz 3..20 |
| stanowisko: glebokosc glownego szyku + 3 m od mediany, w strone przeciwna do mediany celu | `BehaviorGeneral.CalculateCurrentOrder` (gra) |
| 40 m za szykiem tylko przy konnym szyku wodza i >= 33.3% jazdy | `BehaviorGeneral` - dotyczy wodza z obstawa; bez eskorty Jeff: "blisko swoich" = 3 m |
| wrog w 10 m, swoj w 5 m, postoj 6 s, droga 25 s, odswiezanie 1.5 s, zasieg 80 m | 149 (`FoeRadius`, `AllyRadius`, `AloneHold`, `TravelMax`, `RefreshAlone`, `BannerRange`) |
| szyk pieszy od 8 ludzi | 149 `MainMin` |
| napastnik w 6R (18 jedn.) | gra: `GetBestInitiativeBehavior` pomija wroga dalej niz R x 2 x 3 |
| sojusznicy napastnika w 2R | gra: przewaga lokalna liczy partie w 2R od wroga |
| margines sily 1.0 | gra: atak, gdy 0.5 x (1 + a) > 1 / a, czyli przewaga a > 1 |
| obroncy w `FleeToNearbySettlementRadius` (22.5) / `FleeToNearbyPartyRadius` (11.25) | model AI gry - promien wiekszy dla wladcy / glowy rodu / znanego lorda / wodza armii |
| obroncy w `DrMaxMarchHours` marszu (2 h) | m97 `DrMarchArrival` - ten sam limit przyprowadza posilki do bitwy (trasa 1.1 x, predkosc / 1.2) |
| migawka co `AiCheckInterval` (0.25 h) | gra - tak czesto partie licza decyzje chwili |

Nowe stale bez modelu: `ThreatRider` 20 m (2 x `FoeRadius`: konny pokonuje 10 m w ok. 1 s - jeden pass i reakcja), `LoneGap` 30 m (tylko pomiar),
`ThreatCool` 1 s (dowodca reaguje znowu od razu - 149 czeka 10 s, bo samotny wraca do walki; dowodca do walki nie wraca), straz min. 3 (gra daje 0 przy
< 10 kandydatach - "kilku ludzi" Jeffa).

## 3. Macierz zachowan

### 3.1 Bitwa (klucz `CommandersKeepCommandPost`, z `HeroesKeepBehindTheirMen`; tylko bitwa polowa)

Role (co pass 0.5 s): DOWODCA = wodz druzyny AI (`Team.GeneralAgent`) albo kapitan szyku AI (`Formation.Captain`); GLOWA RODU = glowa rodu /
wladca / nastepca bez dowodzenia; reszta bohaterow - 149 bez zmian. Gracz i szyki pod jego rozkazami (`HorsePursuit.PlayerForm`, cala druzyna
gracza-wodza) - nigdy.

| Stan bitwy \ rola | Dowodca bez eskorty konnej | Dowodca z eskorta konna (>= 10 jezdzcow w szyku) | Dowodca pieszy w swoim szyku (>= 8) | Glowa rodu | Zwykly bohater |
|---|---|---|---|---|---|
| spokoj, glowny szyk jest | stanowisko: mediana glownego szyku - kierunek do wroga x (glebokosc + 3 m) | z eskorta (dalej niz polowa szyku + 10 m - za szyk eskorty) | w szyku, ostatnie szeregi (149) | 149 | 149 |
| spokoj, glownego szyku brak (zostal sam) | walczy (jak `BehaviorGeneral`: szarza) | z eskorta | w szyku | 149 | 149 |
| zagrozenie: wrog w 10 m albo jezdziec w 20 m, swoich w 5 m mniej | do najblizszego wlasnego szyku (srodek, przy wrogu - tyl) + straz | to samo | swoi obok - zwykle brak zagrozenia | to samo co dowodca | 149: 2 wrogow w 10 m |
| ranny < 45% | do najblizszego wlasnego szyku, straz dalej w drodze | to samo | to samo | to samo | 149: tyl szyku / glowna sila |
| panika, odwrot rannego 143, odwrot na rozkaz, gracz przejal | jak 149 - pozycja zdjeta, straz zwolniona | | | | |

Straz: ludzie druzyny w 80 m, szeregowi AI (nie chorazowie, nie jezdzcy 148/204, nie w odwrocie / panice / 143, nie w szyku gracza, bez cudzej pozycji
skryptowej), najblizsi (jezdziec z polowa odleglosci), liczba = kandydaci / 10 (3..20); pozycja skryptowa na dowodcy co 0.25 s (przy przesunieciu
o 2 m); koniec przy wyjsciu dowodcy z zagrozenia, po 25 s, przy panice / odwrocie, koncu bitwy.

### 3.2 Mapa (klucz `LordsDefendThreatenedLord`)

| Sytuacja | Chroniony P | Obroncy (ta sama strona, w zasiegu i 2 h marszu) |
|---|---|---|
| napastnik X slabszy lub rowny P | gra | gra |
| X silniejszy od P, brak obroncow | gra (ucieczka, 223 - twierdza) | - |
| X silniejszy od P, grupa P + obroncy < X z sojusznikami w 2R | gra (ucieczka) | gra (ucieczka) |
| X silniejszy od P, grupa >= X z sojusznikami | do najblizszego obroncy (EscortParty), w R od niego - atak na X razem | atak na X (EngageParty, wynik >= 1.01) |
| obronca ucieka przed KIMS INNYM (wynik > 1) | - | gra (inne zagrozenie) |
| obronca przy dwoch zagrozonych | - | przy wyzszej randze, potem blizszym |

Ranga P: wladca / nastepca / glowa rodu / znany lord (223 R1-R3) i wodz armii - obroncy z 22.5 jedn.; reszta lordow - z 11.25 jedn.
Napastnik: gracz (jedzie w strone P - 45 st. - albo kliknal P, albo jest w 2R) albo wrogi lord / wodz armii, ktory goni P (cel krotki lub dlugi) albo
przed ktorym P ucieka. Zakaz zaczepiania 223 / p9 nie obowiazuje obroncy i chronionego wobec tego napastnika (to przywraca tez liczenie ich w
przewadze lokalnej gry).

## 4. Wpiecia

- `HeroCaution.Guard335.cs` (nowy, partial) + `HeroCaution.cs`: tryby `ModePost` / `ModeThreat`, rola, `NearestOwn` w `Pick`, osobne liczniki;
  BattleSense / glowny watek / po wczytaniu akcji (bez latek na Agent).
- `LordEscort.Guard335.cs` (nowy, partial): migawka na `CampaignEvents.TickEvent`, postfiks `GetBestInitiativeBehavior` czynnego modelu
  (`PatchTarget.OfActive` - NavalDLC wola model bazowy), wyjatek w `LordEscort.AttackPostfix` i `ArmsFirst.AttackPostfix`, linie doby i pojman.
- `HorsePursuit.TestRunAnyThread`: takze lowca testu 335 (bez capu marszu i oddechu szyku).
- `Settings.cs` / `McmSettings.cs`: `CommandersKeepCommandPost`, `LordsDefendThreatenedLord`, `CommanderHuntInAutotest` (tylko autotest).

## 5. Logi i sprawdzanie

- `Ochrona wodza (335): bitwa misja ...` raz na bitwe (po `bohaterowie (149)`), `Ochrona wodza (335) TEST start/koniec` (test lowcy),
  `Ochrona wodza (335): mapa start sesji`, `Ochrona wodza (335): mapa - ...` (przyklady, 10 na dobe), `Ochrona wodza (335): mapa dzien N`.
- `python -I tools/sprawdz_logi.py --grupa wodz335 <Armoury-*.log>` - W335-S test syntetyczny, W335-0..W335-8 (opis w naglowku grupy).

## 6. Test

- Autotest bitew: plan "slaba" (armia gracza stoi 150 s, 333t) - LOWCA: czlowiek AI druzyny gracza (jezdziec najpierw, bron wrecz) 8 s po rozstawieniu
  jedzie prosto na wodza wroga (jak Jeff na Tywina), HP i morale jak gracz do kontaktu; koniec: wodz padl / uszedl, lowca padl, 150 s, koniec bitwy.
- Mapa: bez scenariusza gracza - mechanizm dziala takze dla napastnikow AI (wrogi lord goni lorda), wiec doby autotestu daja pary i decyzje (W335-2).
- Komenda: patrz CHANGELOG 335 (bieg-bitwa-204.ps1 z katalogiem `test\m335`, tylko gdy Jeff nie gra).

## 7. Ryzyka

- Pozycja skryptowa dowodcy (stanowisko) walczy z rozkazami szyku RBM - przywracana co 0.25 s (licznik "przywrocen"); RBM StanceLogic moze ja zdejmowac.
- Straz odrywa kilku ludzi z szykow na <= 25 s; szyki w zwarciu traca ich na ten czas.
- Mapa: obroncy atakuja gracza, ktory jedzie w strone zagrozonego lorda - gracz szybszy moze ich ciagnac; migawka znika, gdy gracz odjedzie (poza 6R
  albo w inna strone).
- Obroncy dochodza w roznym czasie - pierwszy moze zaczac bitwe sam; m97 przyprowadza reszte (ten sam limit 2 h).
