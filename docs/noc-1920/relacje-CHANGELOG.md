## 2026-10-19 (n20-relacje: relacje po wygranej z bandytami - jedno zbiorcze powiadomienie)

**Mod:** Armoury | **Galaz:** `w-toku/n20-relacje` (od `934cc039` = WGRANIE 88) | **Tor:** M (bez gry - gra zajeta testem; build Armoury 0 bledow; gen_mcm uruchomiony, 2013 ustawien) | **Klucz (nowy, domyslnie TAK, grupa MCM "Grateful villages" zaraz pod Bandit Cheer Relation):** `BanditReliefRelationSummary` ("Bandit Relief Relation Summary")

**Dla Jeffa (prosto):**
- Po wygranej z bandytami dalej dostajesz ten sam plus do relacji z ludzmi z okolicznych wiosek (nic nie zmniejszone, nic nie dodane).
- Zamiast 15-36 osobnych okienek "Your relation with ... increased" wyskakuje JEDNO, np.:
  "Villages around Oldstones are grateful you beat the bandits: relation +2 with 14 notables in 5 villages (Oldstones, Fairmarket and 3 more). Better relations let you recruit more of their men."
- Po co to: im lepsza relacja z notablem (soltys, starszy wioski), tym wiecej jego ochotnikow mozesz zwerbowac - to dopisane w komunikacie, zeby bylo wiadomo, co z tego masz.
- Kto chce stare okienka (po jednym na notabla) - wylacza "Bandit Relief Relation Summary" w MCM Armoury.

**Problem:** Jeff 19.10 (doslownie): "jak zabije bandytow to pobliskie wioski daja mi + do relacji, czy mozmey zrobic to tak, zeby nie wyskakiwlao mi 30 powiadimien ze kazdy lord i ile, czy nie da sie to jakos zbiorczo i tak mi to nic nie mowi to po co to." Dowod z logu Jeffa `D:\Backup-Bannerlord\zawieszenie-2026-10-19-1609\logi\Armoury-2026-10-19_14-41-48.log`: 6 wygranych z bandami, kazda "BanditCheer: bandyci pokonani - 3..9 wiosek w promieniu 50 ucieszonych, +2 relacji z 15 / 36 / 16 / 32 / 11 / 11 notablami" (15:18:44 - 16:09:01); wczesniejsza sesja `Modules/Armoury/Armoury-2026-10-19_13-00-24.log`: 18 / 26 / 35 / 31 notabli. Kazdy notabl = jedno okienko.

**Przyczyna (sprawdzone w dekompilacji, nie zgadywane):** zrodlem jest NASZ `Armoury/src/BanditCheer.cs` (Jeff 01.09 "wdzieczne wioski"), nie gra, nie BK i nie ROT:
- `BanditCheer.AfterVictory` (z `ArmouryBehavior.OnMapEventEnded` po wygranej) wola `ChangeRelationAction.ApplyPlayerRelation(notable, 2, false, showQuickNotification: true)` dla KAZDEGO notabla kazdej wioski w promieniu 50.
- `ChangeRelationAction.ApplyInternal` (TaleWorlds.CampaignSystem): mnoznik `DiplomacyModel.GetRelationIncreaseFactor` (Charm, perki) + `MBRandom.RoundRandomized`, zapis miedzy bohaterami z `GetHeroesForEffectiveRelation` (notabl z rodem -> glowa rodu), potem `CampaignEventDispatcher.OnHeroRelationChanged(..., showQuickNotification, ...)`.
- `SandBox.CampaignBehaviors.DefaultNotificationsCampaignBehavior.OnRelationChanged`: przy `showNotification` -> `MBInformationManager.AddQuickInformation` (okienko z portretem notabla, "str_your_relation_increased_with_notable" albo "..._with_clan" gdy notabl ma rod - stad "kazdy lord i ile").
- Gra sama nie daje relacji za bandytow pobitych w polu kolo wiosek (CharacterRelationCampaignBehavior.MapEventEnded daje tylko za obrone wiesniakow/karawan i rabunek; HideoutCampaignBehavior po kryjowce - jeden losowy notabl, bez okienka); BannerKings (12 wywolan ApplyPlayerRelation - zadne po bitwie) i ROT.dll (RedWedding, Enlistment, SendToWall) tez nie. Wzorzec "bez okienek + jedno zbiorcze" jest w samej grze: `CharacterRelationCampaignBehavior.OnHeroKilled` (egzekucja: ApplyPlayerRelation(..., showQuickNotification: false) + jedno AddQuickInformation "The execution has hurt your relations with {COUNT} clans").

**Zmiana:** klucz `BanditReliefRelationSummary` (domyslnie TAK; wylaczony = dokladnie stara sciezka, okienko gry przy kazdym notablu). Przy wlaczonym:
- ta sama petla, ten sam promien i ta sama relacja (`BanditCheerRelation`), to samo `ApplyPlayerRelation` - tylko `showQuickNotification = false`;
- zysk kazdego notabla mierzony jak w grze: relacja bazowa (`CharacterRelationManager.GetHeroRelation`) miedzy bohaterami z `DiplomacyModel.GetHeroesForEffectiveRelation` (tymi samymi, ktorym ApplyInternal ja zapisuje) przed i po wywolaniu - czyli z mnoznikiem Charm i zaokragleniem; notabl juz na +100 liczony jako "bez zmiany";
- JEDNO `MBInformationManager.AddQuickInformation` (dzwiek "event:/ui/notification/relation", jak okienko gry, bez portretu): najblizsza wioska, zysk (+2 albo "+2 to +3", gdy mnoznik dal rozne), liczba notabli z zyskiem, liczba wiosek, do 3 nazw (wiecej = "X, Y and N more"), zdanie "Better relations let you recruit more of their men." (sprawdzone: `BKVolunteerModel.CalculateMaximumRecruitmentIndex` dodaje `GetRelationImpact(relacja z notablem)`);
- potkniecie jednego notabla (wyjatek) liczone i logowane raz na zdarzenie - reszta wiosek dostaje relacje (dotad jeden wyjatek przerywal cala petle);
- linia logu przy kazdej wygranej z banda (patrz Test w grze). Stara linia "BanditCheer: bandyci pokonani - ..." zostaje bez zmian.

**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md pkt 8.0):**
- Regresje: jedyne wywolanie `BanditCheer.AfterVictory` (ArmouryBehavior.OnMapEventEnded, watek glowny, raz na bitwe gracza) - bez zmian miejsca i warunkow; nikt inny nie czyta stanu BanditCheer (brak stanu, brak zapisu). Wylaczony klucz = kod jak w WGRANIU 88.
- Zysk relacji i XP Charm bez zmian: `CharacterRelationCampaignBehavior.OnHeroRelationChanged` -> `SkillLevelingManager.OnGainRelation` nie patrzy na `showNotification`. Inni sluchacze `HeroRelationChanged`: BK - brak (grep dekompilacji), ROT - brak, AIInfluence (zaciemniony, nie do dekompilacji) nie ma w metadanych nazwy `HeroRelationChanged` - nie slucha tego zdarzenia; nasz CrashScribe UniqueCompanions laduje tylko CompanionRolesCampaignBehavior (klotnie towarzyszy), nie notabli.
- Kolizje: inne zrodla relacji (HideoutPurge, DistrictWorks - juz bez okienek; zadania, rozmowy, obrona wiesniakow w grze) bez zmian - Jeff moze dalej zobaczyc pojedyncze okienka, jesli w tej samej bitwie obronil wiesniakow (CharacterRelationCampaignBehavior gry, osobne zrodlo, swiadomie nieruszane).
- Zamknieta ekonomia: bez zlota i towarow - nie dotyczy. Zapis: nic nie zapisywane. Watki: tylko watek glowny (MapEventEnded), dwie male listy na bitwe.
- Okienko: dluzszy tekst (ok. 190 znakow) - widget GameNotificationUI ma HeightSizePolicy="CoverChildren" (zawija, rosnie w dol). Sprawdzic w grze, czy czyta sie dobrze.
- Notabl z rodem: relacja trafia (jak w grze) do glowy rodu; dwoch notabli jednego rodu = dwa razy +2 dla glowy - komunikat liczy ich jako 2 notabli (tak jak gra pokazalaby 2 okienka).

**Status:** DO SPRAWDZENIA (zbudowane, niewgrane, nietestowane w grze).

### Decyzje (podjete samodzielnie)
- Tylko zrodlo BanditCheer (nasze) - inne zmiany relacji zostaja z okienkami gry. Wylacznik: `BanditReliefRelationSummary` = false ("Bandit Relief Relation Summary" w MCM Armoury, grupa "Grateful villages").
- Jedno okienko (AddQuickInformation), bez dodatkowej linii w dzienniku wiadomosci - Jeff prosil o jedno powiadomienie. Szczegoly (kazda wioska i ilu notabli) tylko w logu Armoury.
- W tekscie zdanie "po co to" (wiecej ochotnikow) - odpowiedz na "tak mi to nic nie mowi to po co to"; sprawdzone w BKVolunteerModel.
- Wielkosc relacji (+2), promien (50) i warunek "banda" bez zmian - istniejace klucze `BanditCheerRelation`, `BanditCheerRadius`, `BanditCheerEnabled`. Zadnych zmian w Armoury.json Jeffa.
- Potkniecia per notabl przy wlaczonym kluczu (zasada "licz potkniecia, nie gas"); przy wylaczonym zachowanie 1:1 jak dotad.

### Test w grze
- Wystarczy zwykly autotest **5 dob + bitwa** (`bieg-bitwa.ps1 ... -Days 5 -Battles 1`), bez specjalnych plikow i flag: bitwa autotestu to banda (BanditPartyComponent.CreateBanditParty), a wszystkie biegi 19.10 (04:54, 08:31, 10:36, 12:51) mialy po mocnej bitwie "BanditCheer: bandyci pokonani - 5 wiosek w promieniu 50 ucieszonych, +2 relacji z 20 notablami."
- Oczekiwane linie w `Armoury-*.log` zaraz po bitwie:
  - `BanditCheer: bandyci pokonani - 5 wiosek w promieniu 50 ucieszonych, +2 relacji z 20 notablami.` (jak dotad)
  - `BanditCheer (zbiorczo): 1 komunikat zamiast 20 | wiosek z zyskiem 5/5, notabli z zyskiem 20 (bez zmiany 0, potkniecia 0), relacja +2 na osobe, suma +40 | najblizsza ... (.. j.) | wioski: ... | "Villages around ... are grateful you beat the bandits: ..."` (liczby moga sie roznic: mnoznik Charm moze dac "+2..+3", notable na +100 ida do "bez zmiany").
  - Zle: `BanditCheer (zbiorczo): bez komunikatu` przy notablach ponizej +100, `potkniecia` > 0, albo ERROR `BanditCheer.AfterVictory`.
- Przy wylaczonym kluczu: `BanditCheer (zbiorczo): wylaczone (Bandit Relief Relation Summary) - gra pokazala N osobnych okienek relacji.`
- Okienka na ekranie autotest nie pokaze w logu - w grze Jeffa: po wygranej z banda kolo wiosek jedno okienko zamiast kilkudziesieciu.
