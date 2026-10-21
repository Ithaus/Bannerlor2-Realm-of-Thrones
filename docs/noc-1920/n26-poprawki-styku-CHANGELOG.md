## n26-poprawki-styku (20.10 wieczorem) - poprawki styku po recenzji scalenia n26-calosc (HEAD 8e93796e), kandydat 97 (Armoury)

**Dla Jeffa (prosto).** Rabunek wioski pobocznej nie jest juz przerywany przez noc: w trakcie rabunku nie pada pytanie "Night Falls", nie staje sam namiot
i nie ma ostrzezenia o nocnym marszu - pytanie przyjdzie po rabunku. W bitwie z chlopami wioski pobocznej walcza naprawde tylko oni: Dynamic Reinforcements
traktuje ja jak rabunek wsi (u Ciebie Allow Raids wylaczone), wiec zadna zaloga, milicja ani lord nie dochodzi. Gdy przegrasz z chlopami, Twoi jency, ktorych
odbili, wracaja do swoich domow (a nie jako chlopi do wioski), a Twoj lup trafia do magazynu wsi okregu zamiast przepasc; straty wioski licza sie poprawnie.
Okup wioski pobocznej daje jeden komunikat z powodem zamiast dwoch.

Recenzja scalenia: 3 wazne (wszystkie potwierdzone weryfikacja i poprawione) + 1 drobne (poprawione). Zbudowane: Armoury kod 0, 0 bledow, 6 dawnych ostrzezen;
`tools/gen_mcm.py` (1 nowy klucz). Bez gry i bez autotestu (zakaz w tej pracy - trwal autotest 97).

### 1. Noc gracza a rabunek wioski pobocznej (Armoury/src/NightRest.cs)

**Problem.** `NightRest.OnHourly` nie znal rabunku wioski pobocznej (m97-wioski: menu oczekiwania `arm_sidevil_raid` bez MapEvent). Pytanie "Night Falls",
rozkaz "always make camp" (`OwnCamp` -> `ActivateGameMenu` = SwitchToMenu przy otwartym menu - straznik rabunku `RaidGuard` po 2 s zamykal go jako "przerwany"),
namiot automatyczny (postoj w godzinie obozu = odpoczynek) i ostrzezenie marszu nocnego przerywaly albo psuly rabunek. m97-oboz przesunal oboz na 21:00, wiec
trafialo to wieczorne rabunki tuz po dojezdzie. Rabunek gry jest od tego wolny (`mp.MapEvent != null`).

**Zmiana.** Raz na tick `bool raidNow = SideVillageVisit.RaidActive` i `&& !raidNow` w czterech warunkach: namiot automatyczny, pominiecie ZB-6 (noc nie
odhaczona), pytanie / oboz z rozkazu, `marchField` (licznik marszu nocnego i ostrzezenie). Noc NIE jest odhaczona (`_promptNight` bez zmian) - pytanie padnie w
pierwszej godzinie obozu z ruchem po rabunku. Linia logu raz na noc: `Nocleg: gracz - godzina obozu H:00 w trakcie rabunku wioski pobocznej - bez namiotu,
pytania o nocleg i ostrzezenia (noc N nie odhaczona).` Szerszej bramki (kazde obce menu oczekiwania, `CurrentMenuContext.GameMenu.IsWaitMenu`) NIE dodalem:
zmienilaby zachowanie w innych menu oczekiwania (zajazd przy drodze, kuznia, sen, oboz BK), ktorych dzis nikt nie zglaszal - wpiecie minimalne.

**Ryzyko / co sprawdzic.** `RaidActive` = `_raid != null`; `RaidGuard` zamyka rabunek najdalej 2 s po zamknieciu jego menu, wiec bramka nie moze zostac na stale.
Godzina rabunku dalej liczy sie jak postoj do odpoczynku (`resting` bez zmian - `Drill.RestHour` patrzy tylko na osade, oboz obleznikow i krok; tak samo
przy rabunku wsi gry) - nie ruszane, poza zakresem bledu (OTWARTE: czy rabunek ma byc praca jak cwiczenia `CampDrill.DrilledLastHour`). Parser `sprawdz_logi` (NO-9, ZB) nie zna nowej linii - zaczyna sie od `Nocleg: gracz - godzina obozu`, nie koliduje z `oboz automatyczny`
ani `zmierzch`.

**Test w grze.** Wojna, wioska poboczna, "Raid the village" ok. 20:30 tak, by rabunek trwal przez 21:00-22:00: pasek rabunku idzie dalej, bez "Night Falls",
bez namiotu i bez zoltego ostrzezenia; w logu Armoury linia `godzina obozu ... w trakcie rabunku wioski pobocznej`. Po rabunku jedz dalej - w nastepnej godzinie
obozu z ruchem pytanie "Night Falls" (albo oboz z rozkazu "always").

### 2. Posilki Dynamic Reinforcements w bitwie z obroncami wioski pobocznej (Armoury/src/DrMarchArrival.cs, SideVillageVisit.Defenders.cs, klucz MCM)

**Problem.** Bitwa z obroncami wioski pobocznej to w grze bitwa w polu (FieldBattle), nie Raid - ustawienie Jeffa DR `AllowRaids = false` jej nie obejmowalo.
DR dawal posilki (zaloga i milicja twierdzy, lordowie i armie z promienia marszu), choc wedlug Jeffa bronia TYLKO mieszkancy tej wioski; menu, dymek i regula
poddania 4:1 licza tylko ich. Straty zalogi i milicji schodzily z twierdzy.

**Rozpoznanie (dekompilacja DynamicReinforcements.dll z folderu gry).** `DynamicReinforcements.Battle.MissionUtils.IsRealBattleMission(Mission)` (public static bool)
- jedyne wywolanie w `SubModule.OnMissionBehaviorInitialize`: `BattleStateBehavior` (cale DR w misji) doklejany tylko przy true. EventType 1 (FieldBattle) -> true;
2 (Raid) -> `DRConfig.Current.AllowRaids` (pole public bool, domyslnie false). `DRConfig.Current` - wlasciwosc statyczna.

**Zmiana.** Nowa latka (postfiks) na `IsRealBattleMission` w `DrMarchArrival.HookDefenders` (`AccessTools.DeclaredMethod`, raz na proces z `EnsureHooks`,
niezalezna od klucza `DrArrivalByMarch` i od reszty latek DR). Gdy `__result` i `SideVillageVisit.IsDefendersEvent(MapEvent.PlayerMapEvent)` (nowa funkcja:
nasza druzyna obroncow w `InvolvedParties` - jak `OnMapEventEnded`; po wczytaniu przed odnalezieniem druzyny - po StringId): ta sama regula co rabunek wsi gry -
DR `AllowRaids` NIE (u Jeffa) -> `__result = false` (bez posilkow obu stron), TAK -> DR jak przy rabunku. Nowy klucz MCM `SideVillageDefendersNoReinforcements`
(domyslnie TAK, grupa "District works and side-village crafts (18.10)"; NIE = DR jak w kazdej bitwie w polu). Linie logu (prefiks `Posilki marszem (m97-posilki): `):
`posilki DR wylaczone: bitwa z obroncami wioski X (bronia tylko mieszkancy; jak rabunek wsi gry przy DR Allow Raids NIE) | w sesji N.` albo `bitwa z obroncami
wioski X - DR Allow Raids TAK, posilki DR jak przy rabunku wsi gry`; linia startu dostala koncowke `| obroncy wioski pobocznej: klucz ... TAK, latka DR
IsRealBattleMission 1/1, DR Allow Raids NIE (bez posilkow), bitew bez posilkow N, z posilkami (Allow Raids) M`. Wyjatek w latce - `Stumble` (licznik potkniec,
3 pierwsze do logu), wynik DR bez zmian.

**Ryzyko / co sprawdzic.** Latka dotyczy tylko bitwy z NASZA druzyna obroncow (inne bitwy - wynik DR bez zmian). Gra sama moze dolaczyc partie stojace obok
(`CheckNearbyPartiesToJoinPlayerMapEvent`) - to nie DR, bez zmian (jak w kazdej bitwie). Parser `sprawdz_logi --grupa m97posilki`: `M97P_START` szuka od poczatku
linii startu (koncowka dopisana na koncu, `potkniecia N` - pierwsze trafienie to dalej licznik DrMarchArrival); nowe linie nie zaczynaja sie od `start - `,
`bitwa ` ani `przyjety `. Bitwa "Send troops" (bez misji) - DR i tak nie dziala.

**Test w grze.** Bitwa z chlopami wioski pobocznej ("Raid the village" z mniej niz 4 x obroncow, "Attack!"): w bitwie nie pojawia sie zaden komunikat DR o posilkach
ani oddzialy zalogi / lordow; w logu Armoury `posilki DR wylaczone: bitwa z obroncami wioski`. Zwykla bitwa w polu obok - DR jak dotad (linia `bitwa N | kandydatow`).
MCM "Side Village Defenders No Reinforcements" = NIE - DR znow daje posilki w tej bitwie.

### 3. Przegrana gracza z obroncami: jego jency i lup w druzynie obroncow (Armoury/src/SideVillageVisit.Defenders.cs)

**Problem (potwierdzony dekompilacja 1.4.8).** Po przegranej gracza (albo odwrocie z bitwy) gra oddaje druzynie obroncow jego lup: `MapEvent.LootDefeatedPartyPrisoners`
- zwykli jency gracza (np. bandyci, chlopi z taborow) do `RosterToReceiveLootMembers` = skladu druzyny; `LootDefeatedPartyItems` - towary do taboru; `CampCart.OnTaken`
doklada 3 drewna za woz; surowce rzadkie z sakw ida z towarami. `Disband` liczyl `left` jako caly sklad (z ex-jencami) i odsylal ich do DOM okregu jako wracajaca
milicje (`MBackMilitia`); `FinishFight` liczyl straty `Def - left - routed` - przy `left >= Def` straty 0 (wioska bez pamieci poleglych i braku rak). Tabor druzyny
ginal przy `DestroyPartyAction.Apply(null, ...)`: `HorseFate.Spoils` pomija partie bez niszczyciela, ktora nie jest znana (CustomPartyComponent), wiec ksiega towarow
ksiegowala "przepadlo z rozbitymi partiami" - nic w nicosc.

**Zmiana.**
- `Fight.Types` - szablony obroncow (chlop, wlocznik, lucznik) ze `StartFight`; po wczytaniu (bez pola w zapisie) - z kultury wsi okregu, ta sama droga co `StartFight`.
- `Disband(f, why, out freed, out note)`: obroncy = szablony wioski w skladzie, najwyzej do pulapu: przy przegranej / odwrocie z wynikiem bitwy (`Won == 2`, `Dead >= 0`)
  `Def - Dead - Routed`, przy wygranej gracza i bez bitwy (Leave) `Def`, sierota po wczytaniu (`Def` nieznane) - bez pulapu. Szablony wioski ponad pulap (jency gracza
  tego samego szablonu) i wszystkie inne szablony = ex-jency gracza (bohaterowie i umarli pominieci - jak dotad znikaja z druzyna / bohaterow obsluguje `FreePrisoners`).
- Pochodzenie: `PeopleBook.Take(sklad, obroncy, FMilitiaHome)` i `Take(sklad, ex-jency, FFreed)` z jednego slownika skladu, potem `HomeFirst`: obroncy dostaja udzial
  regionu wsi okregu i "" (to ich udzial z poboru `GiveNew` / `GiveNone`), ex-jency w zamian obce udzialy - sumy bez zmian. Obroncy jak dotad `ArriveAt(wies okregu,
  MBackMilitia)`; ex-jency - `LosersFlee.FreedHome` (istniejaca jedna droga "uwolnieni bez odbiorcy": 1/3 uprawnionych do karczmy, reszta do domu wedlug pochodzenia,
  bez domu - pula wyrzutkow regionu przy prawie wyrzutkow `OutlawLaw`, jak `DoomedPartyMen`).
- `FinishFight`: `left` = tylko obroncy, wiec `lost = Def - left - routed` = polegli (przy przegranej gracz nikogo nie pojmal) - pamiec poleglych i brak rak licza sie.
  Przy wygranej `left <= Def`.
- `GoodsToVillage`: przed `DestroyPartyAction` tabor druzyny do magazynu wsi okregu (`Settlement.Find(f.Dist).ItemRoster`) w ramce `GoodsLedger.FRaid` (przewoz - suma
  zero, nie zrodlo ani ujscie): towary, konie, drewno wozow, surowce rzadkie; bez rzeczy nie-handlowych, choragwi, przedmiotow zadan i unikatow (jak `HorseFate.Spoils`).
- Log: koniec linii `koniec bitwy` (i linii sieroty) `..., jency gracza z lupu obroncow uwolnieni N (karczma a, do domow b, bez domu c - pula wyrzutkow), lup obroncow do
  magazynu wsi ID N szt. (K rodzajow)` - tylko gdy cos bylo; doba: `jency gracza z lupu obroncow uwolnieni N, lup obroncow do magazynu wsi M szt.` (tylko > 0).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regex `sprawdz_logi` OB (`end`) sprawdzony na przykladowej linii z nowa koncowka (grupy bez zmian); OB-3 (`wrocilo + rozbici + straty <= obroncow`) - teraz zawsze rowne.
- Zloto gracza zabrane przy przegranej (`PlunderedGold` druzyny bez wodza i bez kiesy handlowej; RealisticCaptivity "CaptorTakesAllFromPlayer") - poza zakresem, bez zmian
  (gra: `MapEventParty.CommitGoldChanges` nie ma odbiorcy). Do sprawdzenia osobno, jesli Jeff zauwazy.
- Gdy gracz trzymal jencow tego samego szablonu co obroncy (np. chlopi kultury okregu) - nadwyzka ponad pulap jest zawsze ex-jencami (najpierw chlopi), ale ktorzy dokladnie
  ludzie to obroncy, a ktorzy jency, nie da sie rozroznic (sklad gry liczy szablony) - sumy sie zgadzaja.
- `HomeFirst` zmienia tylko podzial udzialow miedzy dwie grupy (sumy i udzial Strazy W przyciete); przy wylaczonej ksiedze pochodzenia ex-jency ida do domu bez udzialow (jak
  `DoomedPartyMen`).
- Zwykly przypadek (Leave, wygrana gracza, poddanie) - ex-jencow 0, tabor pusty: zachowanie jak przed poprawka.
- Magazyn wsi dostaje lup gracza (np. zbroje, konie) - towar do kupienia we wsi okregu; wieksza podaz chwilowo.

**Test w grze.** Z malymi silami i kilkoma jencami (np. bandyci) w lochu oraz towarem w taborze zaatakuj chlopow wioski pobocznej i przegraj (albo wycofaj sie z bitwy).
Po niewoli / ucieczce w logu Armoury `koniec bitwy ... wrocilo do domow L` z L najwyzej `obroncow - zabici - rozbici`, `straty z ludnosci S` > 0, na koncu `jency gracza z lupu
obroncow uwolnieni N (...)` i `lup obroncow do magazynu wsi ... szt.`; wjedz do wsi glownej okregu - w handlu wsi widac Twoj stracony towar. Automat: `python -I
tools/sprawdz_logi.py --grupa m97obroncy <Armoury.log>` - OB-3 TAK.

### 4. Okup wioski pobocznej - dwa komunikaty (Armoury/src/SideVillageVisit.Raid.cs)

**Problem.** m97-wioski wyszla z bazy sprzed m96-wies (`PayNotes`, klucz `PlayerPaymentReasons`): okup wioski (`PayTribute`) robil przelew z komunikatem gry, wiec przy
wlaczonym kluczu byly dwa komunikaty - goly "You received N" gry i nasz "X paid you N denars of tribute...". Okup wsi glownej (`RaidTribute`) daje juz jeden.

**Zmiana.** Wzorzec `RaidTribute`: `bool pn = PayNotes.On`, przelew `GiveGoldAction.ApplyForPartyToCharacter(..., pn)` (komunikat gry wyciszony przy kluczu), potem
`PayNotes.Got("tribute", zaplacono, ": tribute from the people of X. Your realm will spare it for 30 days.")`; klucz NIE - komunikat gry i nasz jak dotad. Kwoty i linia
logu bez zmian. Rozbicia kwoty noclegu w wiosce (PlayerSplit jak w karczmie) NIE dodalem - nocleg ma juz jeden komunikat z powodem (`PayNotes.Paid`), rozbicie to nowa
rzecz, nie blad styku.

**Ryzyko / co sprawdzic.** Tylko napis; `PayNotes.Got` przy kwocie 0 nic nie pokazuje (kwota > 0 gwarantowana warunkiem `t > 0`).

**Test w grze.** "Demand tribute" w wiosce pobocznej z przewaga 4:1: jeden komunikat "You received N (moneta): tribute from the people of X. Your realm will spare it for
30 days."; w logu Armoury `Komunikaty platnosci (m96-wies): ...` z rodzajem `tribute`.

**Status.** DO SPRAWDZENIA (zbudowane, kod 0; bez gry i autotestu). Do wgrania razem z kandydatem 97 po autotescie - autotest 97 biegl na DLL sprzed tych poprawek.
