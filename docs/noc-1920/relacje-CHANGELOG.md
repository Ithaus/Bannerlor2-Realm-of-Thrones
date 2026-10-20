## 2026-10-19 (n20-relacje: relacje po wygranej z bandytami - zbiorcze powiadomienia zamiast kilkudziesieciu okienek)

**Mod:** Armoury | **Galaz:** `w-toku/n20-relacje` (od `934cc039` = WGRANIE 88) | **Tor:** M (bez gry - gra zajeta testem; build Armoury 0 bledow; gen_mcm uruchomiony, 2014 ustawien; latka DR sprawdzona poza gra na Harmony gry i prawdziwym DynamicReinforcements.dll) | **Klucze (nowe, domyslnie TAK, grupa MCM "Grateful villages", zaraz pod Bandit Cheer Relation):** `BanditReliefRelationSummary` ("Bandit Relief Relation Summary"), `ReinforcementRelationSummary` ("Reinforcement Relation Summary")

**Dla Jeffa (prosto):**
- Po wygranej z bandytami dostajesz dalej ten sam plus do relacji. Dostajesz go od ludzi z okolicznych wiosek i od lordow, ktorzy przyszli Ci z odsiecza. Nic nie jest zmniejszone ani dodane.
- Okienka "Your relation ... increased" mialy dwa zrodla. Teraz kazde daje JEDNO okienko:
  1. Wioski (nasza regula "wdzieczne wioski"): zamiast 11-36 okienek jest jedno, np. "Villages around Oldstones are grateful you beat the bandits: relation +2 with 14 notables in 5 villages (Oldstones, Fairmarket and 3 more). Better relations let you recruit more of their men."
  2. Lordowie z odsieczy (mod Dynamic Reinforcements): zamiast 1-8 okienek "with Smallwood / Mallister / Piper ..." jest jedno, np. "Lords who came to your aid think better of you: relation +2 with Jason Mallister, Brynden Blackfish Tully and 2 more. Friendlier lords are more willing to join your battles." Okienko lordow pojawia sie zaraz po bitwie, a okienko wiosek kilkanascie sekund pozniej, po wyjsciu z bitwy.
- Po co to:
  - Im lepsza relacja z notablem, tym wiecej jego ochotnikow mozesz zwerbowac.
  - Im lepsza relacja z lordem, tym chetniej przychodzi Ci z odsiecza.
  - Oba zdania sa w okienkach. Sprawdzilem je w kodzie Banner Kings i Dynamic Reinforcements.
- Okienka zbiorcze zostaja na ekranie ok. 4 s dluzej niz zwykle, zeby dalo sie je przeczytac.
- Stare okienka da sie przywrocic: w MCM Armoury wylacz "Bandit Relief Relation Summary" (wioski) albo "Reinforcement Relation Summary" (lordowie).

**Problem:** Jeff 19.10 (doslownie): "jak zabije bandytow to pobliskie wioski daja mi + do relacji, czy mozmey zrobic to tak, zeby nie wyskakiwlao mi 30 powiadimien ze kazdy lord i ile, czy nie da sie to jakos zbiorczo i tak mi to nic nie mowi to po co to."
Dowod z logow Jeffa:
- Armoury `D:\Backup-Bannerlord\zawieszenie-2026-10-19-1609\logi\Armoury-2026-10-19_14-41-48.log`: 6 wygranych z bandami, kazda z linia "BanditCheer: bandyci pokonani - 3..9 wiosek w promieniu 50 ucieszonych, +2 relacji z 15 / 36 / 16 / 32 / 11 / 11 notablami" (15:18:44 - 16:09:01). Wczesniejsza sesja `Modules/Armoury/Armoury-2026-10-19_13-00-24.log`: 18 / 26 / 35 / 31 notabli. Kazdy notabl to jedno okienko.
- rgl gry, czyli `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_26116.txt` (sesja 13:00) i `rgl_log_4376.txt` (sesja 14:41). Zaraz po "Player MapEvent BattleState: ...Victory" sa okienka lordow, a 10-25 s pozniej okienka notabli. Przyklady:
  - 13:21:17: 8 okienek (Smallwood x2, Vance, Mallister, Piper x2, Blackwood x2), a o 13:21:27 26 okienek notabli.
  - 13:45:49: 5 okienek (Paege x2, Blackwood, "Tully,Blackfish" x2).
  - 15:18:20: Mallister x2.
  - 15:36:21: "Tully,Blackfish" x2 i Piper.
  - Tekst gry to "Your relation is increased by 2 to 4 with Smallwood." - nazwa RODU zamiast lorda, czyli "kazdy lord i ile".

**Przyczyna (sprawdzone w dekompilacji, nie zgadywane).** Sa dwa zrodla. Gra, BK i ROT same nie daja relacji za bandytow pobitych w polu.
- **(1) NASZ `Armoury/src/BanditCheer.cs`** (Jeff 01.09, "wdzieczne wioski"):
  - `BanditCheer.AfterVictory` (z `ArmouryBehavior.OnMapEventEnded` po wygranej) wola `ChangeRelationAction.ApplyPlayerRelation(notable, 2, false, showQuickNotification: true)` dla KAZDEGO notabla kazdej wioski w promieniu 50.
- **(2) DynamicReinforcements v2.1.6** (`Modules/DynamicReinforcements`, u Jeffa `RelationEffects=True`):
  - `Battle.BattleStateBehavior.OnMissionResultReady` (wynik misji, watek glowny) wola `Reinforcement.Consequences.RewardHelpers(Candidates, playerVictory)`.
  - Dla kazdego kandydata z `Accepted && TroopsSpawned > 0`, strona gracza, idzie `ChangeRelationAction.ApplyPlayerRelation(candidate.Leader, playerVictory ? 2 : 1, true, true)` - jedno okienko na lorda.
  - Ten sam lord moze dostac nagrode 2x w jednej bitwie (dwie partie). Widac to w rgl 13:45:49: "by 2 to 9" i "by 2 to 11 with Tully,Blackfish".
  - To jedyne wywolanie w DR (IL: jedno `call ChangeRelationAction::ApplyPlayerRelation(Hero, int32, bool, bool)`). `PenalizeRefusal` (-1 za zignorowane wezwanie) jest osobne i pojedyncze.
- **Wspolna droga gry:**
  - `ChangeRelationAction.ApplyInternal` liczy mnoznik `DiplomacyModel.GetRelationIncreaseFactor` (Charm, perki), potem `MBRandom.RoundRandomized`.
  - Zapis idzie miedzy bohaterami z `GetHeroesForEffectiveRelation`. Czynny model to **BKDiplomacyModel**, ktory zwraca samych bohaterow: relacja jest osobista z notablem / lordem. Glowa rodu wchodzi tylko, gdy ktorys bohater to null albo ten sam bohater. Przekierowanie do glowy rodu ma tylko `DefaultDiplomacyModel` gry bez BK.
  - Potem `CampaignEventDispatcher.OnHeroRelationChanged(..., showQuickNotification, ...)`.
  - SandBox `DefaultNotificationsCampaignBehavior.OnRelationChanged` przy `showNotification` wola `MBInformationManager.AddQuickInformation`. Przy notablu bez rodu daje portret i "str_your_relation_increased_with_notable"; przy kims z rodem "..._with_clan" (stad "with Smallwood").
  - `AddQuickInformation` pisze tez `Debug.Print` do rgl, stad dowod w rgl.
- **Gra sama:**
  - `CharacterRelationCampaignBehavior.MapEventEnded` daje relacje tylko za obrone wiesniakow/karawan i za rabunek.
  - `HideoutCampaignBehavior` po kryjowce daje relacje jednemu losowemu notablowi, bez okienka.
  - BK (12 wywolan ApplyPlayerRelation, zadne po bitwie) i ROT.dll (RedWedding, Enlistment, SendToWall) tez tego nie robia.
  - Wzorzec "bez okienek + jedno zbiorcze" jest w samej grze: `CharacterRelationCampaignBehavior.OnHeroKilled` (egzekucja: ApplyPlayerRelation(..., false) i jedno "The execution has hurt your relations with {COUNT} clans").

**Zmiana:**
- **(A) Wioski - klucz `BanditReliefRelationSummary`** (domyslnie TAK; wylaczony = dokladnie stara sciezka, okienko gry przy kazdym notablu):
  - Ta sama petla, ten sam promien i ta sama relacja (`BanditCheerRelation`), to samo `ApplyPlayerRelation` - tylko z `showQuickNotification = false`.
  - Zysk kazdego notabla mierzony jak w grze: relacja bazowa (`CharacterRelationManager.GetHeroRelation`) miedzy bohaterami z `DiplomacyModel.GetHeroesForEffectiveRelation`, przed i po wywolaniu. Uwzglednia to mnoznik Charm i zaokraglenie. Notabl juz na +100 liczony jako "bez zmiany".
  - JEDNO `MBInformationManager.AddQuickInformation` (dzwiek "event:/ui/notification/relation", bez portretu). W tekscie: najblizsza wioska, zysk, liczba notabli i wiosek, do 3 nazw oraz zdanie "Better relations let you recruit more of their men." (sprawdzone: `BKVolunteerModel.CalculateMaximumRecruitmentIndex` dodaje `GetRelationImpact(relacja z notablem)`).
  - Potkniecie jednego notabla liczone i logowane raz na zdarzenie; reszta wiosek dostaje relacje.
  - Linia logu po kazdej wygranej z banda.
- **(B) Lordowie z odsieczy DR - klucz `ReinforcementRelationSummary`** (domyslnie TAK; nowy plik `Armoury/src/ReinforcementThanks.cs`, wpiecie w `SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot` obok `AmmoRecovery.ApplyAll`, jak inne latki cudzych modow misji):
  - Cel przez `AccessTools.TypeByName("DynamicReinforcements.Reinforcement.Consequences")` + `AccessTools.DeclaredMethod(t, "RewardHelpers")`.
  - **Transpiler** podmienia w TEJ JEDNEJ metodzie wywolanie `ChangeRelationAction.ApplyPlayerRelation(Hero, int, bool, bool)` na `ReinforcementThanks.Apply`. Stos i podpis sa te same.
  - Przy wlaczonym kluczu `Apply` robi to samo wywolanie gry z `showQuickNotification = false` i mierzy zysk jak w (A). Przy wylaczonym kluczu (albo poza paczka) przekazuje wywolanie 1:1.
  - Wszystkie warunki DR (kto, ile, `RelationEffects`, try/catch per lord, linia DRLog "Relation +2 avec ...") zostaja nietkniete.
  - **Prefiks** otwiera paczke; **finalizer** (biegnie tez przy wyjatku) pokazuje JEDNO okienko i pisze linie logu.
  - Lord nagrodzony 2x w jednej bitwie wystepuje raz, z suma zysku.
  - Tekst: "Lords who came to your aid think better of you: relation +2 with A, B and N more. Friendlier lords are more willing to join your battles." Przy jednym lordzie: "A came to your aid and thinks better of you: relation +2. ...".
  - Zdanie "po co" sprawdzone: DR `ReinforcementCalculator.ComputeScore` dolicza `c.Leader.GetRelation(dowodca strony) * 0.2` (do +20 pkt), czyli lord, ktory lubi gracza, ma wyzszy wynik przy wyborze odsieczy.
  - Brak DR albo inna wersja bez tego wywolania: latka nic nie zmienia, a linia startowa mowi dlaczego.
  - Okienko lordow dziala w kazdej bitwie z odsiecza DR (to jedno zrodlo), nie tylko z bandami - patrz Decyzje.
- **(C) Czas okienek:** oba zbiorcze okienka maja `extraTimeInMs = 4000` (`BanditCheer.SummaryExtraMs`). `GameNotificationVM.CurrentNotificationOnScreenTime`: 0.6 s + (slowa - 4) / 5 + extra. Przy ok. 30 slowach wychodzi ok. 6 s, a teraz ok. 10 s.

**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md pkt 8.0):**
- Regresje (A):
  - Jedyne wywolanie `BanditCheer.AfterVictory` (ArmouryBehavior.OnMapEventEnded, watek glowny, raz na bitwe gracza) - miejsce i warunki bez zmian.
  - Nikt inny nie czyta stanu BanditCheer (brak stanu, brak zapisu). Wylaczony klucz = kod jak w WGRANIU 88.
- Regresje (B):
  - `RewardHelpers` ma jedno wywolanie w DR (`BattleStateBehavior.OnMissionResultReady`). Nikt inny go nie latuje: grep "RewardHelpers" po wszystkich DLL modow daje tylko DR, a w galeziach n20-* i n-ks4 nikt nie rusza DR ani ChangeRelationAction.
  - Latka nie dotyka metod gry: ChangeRelationAction bez latki z naszej strony, a latka ROT na ApplyInternal (OthersPatches.patch12) dalej dziala, bo idziemy ta sama droga.
  - `RewardHelpers` to duza metoda (236 B IL), wiec problem z malymi metodami i UNWIND_INFO (zawieszenie 16:09, metody < 5 B) jej nie dotyczy.
  - Sprawdzone POZA GRA (konsola net472, `0Harmony.dll` z `Modules/Bannerlord.Harmony`, prawdziwy `DynamicReinforcements.dll` 2.1.6, zbudowany Armoury.dll): `Patch` zwraca `RewardHelpers_Patch1`, `_swapped = 1`, prefixes=1 transpilers=1 finalizers=1. `PatchProcessor.GetCurrentInstructions` pokazuje `call Armoury.ReinforcementThanks::Apply` zamiast ApplyPlayerRelation. Samej metody nie wolalem (DRConfig czyta/pisze konfiguracje w Documents).
- Zysk relacji i XP Charm bez zmian:
  - `CharacterRelationCampaignBehavior.OnHeroRelationChanged` -> `SkillLevelingManager.OnGainRelation` nie patrzy na `showNotification`.
  - Inni sluchacze `HeroRelationChanged` (CompanionRoles, MarriageOffer, VassalAndMercenaryOffer) tez nie patrza na te flage.
  - BK i ROT nie maja sluchaczy. AIInfluence jest zaciemniony, ale w metadanych nie ma nazwy `HeroRelationChanged`.
- Kolizje:
  - Inne zrodla relacji zostaja bez zmian: HideoutPurge i DistrictWorks juz bez okienek; zadania, rozmowy i obrona wiesniakow w grze; kara DR -1 za zignorowane wezwanie.
  - Jeff moze dalej zobaczyc pojedyncze okienko, jesli w tej samej bitwie obronil wiesniakow (CharacterRelationCampaignBehavior gry - osobne zrodlo, swiadomie nieruszane).
- Zamknieta ekonomia: bez zlota i towarow - nie dotyczy.
- Zapis: nic nie zapisywane.
- Watki: tylko watek glowny (MapEventEnded i wynik misji). Na zdarzenie kilka pol statycznych i 1-2 male listy; lista lordow jest czyszczona po kazdej paczce (nie trzyma bohaterow).
- Okienka: dluzszy tekst (do ok. 190 znakow). Widget GameNotificationUI ma `HeightSizePolicy="CoverChildren"` (zawija i rosnie w dol). Sprawdzic w grze, czy czyta sie dobrze. +4 s opoznia kolejne okienka w kolejce (np. awans) o te 4 s.
- Notabl / lord z rodem: przy BK (czynny model) relacja jest osobista, bez podwojnego liczenia glowy rodu. Komunikat liczy tych samych bohaterow, ktorym gra zapisuje relacje.

**Status:** DO SPRAWDZENIA (zbudowane, niewgrane, nietestowane w grze).

### Decyzje (podjete samodzielnie)
- Zbieram oba zrodla okienek po bitwach z bandami, bo slowa Jeffa "kazdy lord i ile" pasuja do okienek DR "with Smallwood". Kazde ma osobny wylacznik:
  - wioski: `BanditReliefRelationSummary` = false;
  - lordowie: `ReinforcementRelationSummary` = false.
  - Inne zmiany relacji zostaja z okienkami gry.
- Okienko lordow DR jest zbiorcze w KAZDEJ bitwie z odsiecza, nie tylko z bandami. To to samo zrodlo i ten sam zalew okienek, a warunek "tylko bandy" dokladalby kod bez pozytku. Wylacznik: `ReinforcementRelationSummary`.
- Dwa okienka (wioski i lordowie), a nie jedno wspolne. Przychodza w innym czasie (wynik misji, potem koniec spotkania ok. 10-25 s pozniej). Laczenie wymagaloby trzymania relacji DR do konca spotkania, a w bitwach z lordami nie byloby okienka wiosek.
- Bez dodatkowej linii w dzienniku wiadomosci, bo Jeff prosil o jedno powiadomienie. Zamiast tego okienko jest dluzej (+4 s), a szczegoly (kazda wioska / kazdy lord, relacja po zmianie) trafiaja do logu Armoury.
- W tekstach jest zdanie "po co to" (ochotnicy u notabli, odsiecz u lordow) - sprawdzone w BKVolunteerModel i DR ReinforcementCalculator.
- Imiona lordow w okienku, nie nazwy rodow: gra pisala "with Tully,Blackfish" / "with Smallwood" (nazwa rodu), co nic nie mowi.
- Wielkosc relacji (+2 / +1 DR, +2 wioski), promien (50) i warunek "banda" bez zmian - istniejace klucze `BanditCheerRelation`, `BanditCheerRadius`, `BanditCheerEnabled` i ustawienia DR. Zadnych zmian w Armoury.json Jeffa.
- Potkniecia liczone per notabl / lord (zasada "licz potkniecia, nie gas"). Przy wylaczonych kluczach zachowanie jest 1:1 jak dotad.

### Test w grze
- Wystarczy zwykly autotest **5 dob + bitwa** (`bieg-bitwa.ps1 ... -Days 5 -Battles 1`), bez specjalnych plikow i flag. Bitwa autotestu to banda (BanditPartyComponent.CreateBanditParty). Wszystkie biegi 19.10 (04:54, 08:31, 10:36, 12:51) mialy po mocnej bitwie "BanditCheer: bandyci pokonani - 5 wiosek w promieniu 50 ucieszonych, +2 relacji z 20 notablami."
- Oczekiwane linie w `Armoury-*.log`:
  - Zawsze, przy starcie gry (menu glowne): `ReinforcementThanks: start - jedno okienko relacji za odsiecz DR [wl], latka wpieta 1/1 (DR Consequences.RewardHelpers, podmienione wywolania ApplyPlayerRelation: 1).` Zle: `latka wpieta 0/1`, `podmienione ... 0` albo ERROR `ReinforcementThanks.ApplyAll`.
  - Po bitwie z banda:
    - `BanditCheer: bandyci pokonani - 5 wiosek w promieniu 50 ucieszonych, +2 relacji z 20 notablami.` (jak dotad)
    - `BanditCheer (zbiorczo): 1 komunikat zamiast 20 | wiosek z zyskiem 5/5, notabli z zyskiem 20 (bez zmiany 0, potkniecia 0), relacja +2 na osobe, suma +40 | najblizsza ... (.. j.) | wioski: ... | "Villages around ... are grateful you beat the bandits: ..."`
    - Liczby moga sie roznic: mnoznik Charm moze dac "+2..+3", a notable na +100 ida do "bez zmiany".
  - Tylko gdy DR przyprowadzil lordow (w `Documents\Mount and Blade II Bannerlord\Logs\DynamicReinforcements.log` sa linie `Relation +2 avec ... (venu en renfort).`): `ReinforcementThanks (zbiorczo): 1 komunikat zamiast N | lordow odsieczy z zyskiem M (bez zmiany 0, potkniecia 0), relacja +2 na osobe, suma +.. , wygrana tak | lordowie: Jason Mallister +2 -> 4, ... | "Lords who came to your aid ..."`. N = liczba linii "venu en renfort" w DR. Bez odsieczy linii nie ma - to normalne.
  - Zle: `bez komunikatu` przy relacjach ponizej +100, `potkniecia` > 0, ERROR `BanditCheer.AfterVictory` / `ReinforcementThanks.Apply` / `ReinforcementThanks.Finalizer`.
- Dodatkowo w rgl gry (`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`; `AddQuickInformation` pisze tam kazde okienko). Po "Player MapEvent BattleState: AttackerVictory":
  - NIE MA juz serii `Your relation is increased by .. with <notabl / rod>`;
  - jest jedna linia `Villages around ...` (i ewentualnie jedna `Lords who came to your aid ...`).
- Przy wylaczonych kluczach (jesli ktos chce porownac):
  - `BanditCheer (zbiorczo): wylaczone (Bandit Relief Relation Summary) - gra pokazala N osobnych okienek relacji.`
  - `ReinforcementThanks (zbiorczo): wylaczone (Reinforcement Relation Summary) - DR pokazal N osobnych okienek relacji.`
- Samo okienko na ekranie (czytelnosc, czas) widac tylko w grze Jeffa.

### Recenzja (19.10, po pierwszej wersji 1d2ebb88)
- **[wazne] "Diagnoza pomija DynamicReinforcements" - PRAWDZIWE, poprawione.**
  - Dekompilacja DR 2.1.6: `Consequences.RewardHelpers` -> `ChangeRelationAction.ApplyPlayerRelation(candidate.Leader, num, true, true)`, wolane z `BattleStateBehavior.OnMissionResultReady`.
  - rgl Jeffa: 1-8 okienek lordow na bitwe w chwili wyniku misji (patrz Problem).
  - Poprawka: zmiana (B) - `ReinforcementThanks`, klucz `ReinforcementRelationSummary`, linia logu. Zamiast prefiksu z kopia petli DR jest transpiler podmieniajacy jedno wywolanie, wiec logika DR zostaje u DR.
  - Sprawdzone poza gra na Harmony gry (podmienione 1/1).
  - Przyczyna i Ryzyko poprawione - DR jest teraz opisany jako drugie zrodlo.
- **[drobne] "Opis glowy rodu jest falszywy przy BK" - PRAWDZIWE, poprawione.**
  - `BannerKings.Models.Vanilla.BKDiplomacyModel.GetHeroesForEffectiveRelation` zwraca hero1/hero2; glowa rodu tylko przy null albo tym samym bohaterze.
  - Komentarz `BanditCheer.BaseRelation` i punkt Ryzyka poprawione. Kod liczyl dobrze, bo pyta ten sam model co ApplyInternal.
- **[drobne] "Okienko ok. 6 s, bez sladu w grze" - PRAWDZIWE, poprawione czesciowo.**
  - Oba okienka zbiorcze maja +4 s (`SummaryExtraMs`).
  - Linii w dzienniku wiadomosci swiadomie nie dodalem, bo bylyby to dwa powiadomienia na zdarzenie (patrz Decyzje). Szczegoly sa w logu Armoury i w rgl.
