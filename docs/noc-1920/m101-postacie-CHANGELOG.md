## m101-postacie - postacie kanonu: bohaterowie ROT do rodow, usuniecie fabularnych, zmiany stron wedlug ksiazki (Armoury + CrashScribe UC)

**Mod:** Armoury + CrashScribe (UC) + narzedzie | **Galaz:** `w-toku/m101-postacie` (na 1d2318e3, `w-toku/n29-calosc`) | **Klucze:** nowe `CanonCompanionsToClans`, `CanonCompanionsFollowStory` (domyslnie tak); `python -I tools/gen_mcm.py` | **Nowy plik do wgrania RAZEM z DLL:** `Armoury/ModuleData/arm_canon_moves.tsv` -> `Modules\Armoury\ModuleData\` (skrypty wgrywania kopiuja arm_*.tsv; bez niego cala paczka po cichu nic nie robi - linia startu `plik brak pliku arm_canon_moves.tsv`, `--grupa m101postacie` MP-0 / MP-0b = NIE).

**Dla Jeffa (prosto):** w NOWEJ kampanii Jaime, Tyrion, Bronn, Theon, Varys, Baelish i reszta z listy A nie siedza juz w karczmach do wynajecia -
od pierwszego dnia sa lordami swoich rodow (Jaime i Tyrion u Lannisterow, Theon u Starkow, Varys i Baelish na dworze krola, Benjen i Sam w Nocnej Strazy itd.).
Ci, ktorzy dowodza wojskiem w ksiazce (Jaime, Tyrion, Theon, Quentyn, Rago, Benjen, pozniej Bronn), moga dostac druzyne; dworzanie (Varys, Baelish, Qyburn,
Jacelyn, Ogar) siedza w zamku rodu. Hodor, Syrio, Jory, Cressen, Jaqen, Kinvara, Xaro, Gendry i Talisa w ogole sie nie pojawiaja (zamiast Talisy jest Jeyne
Westerling, ktora w ksiazce bierze slub z Robbem). Potem postacie zmieniaja strony jak w ksiazce, przy wydarzeniach ROT (Czarny Nurt, Krwawe Gody,
Purpurowe Gody, Aegon, Daenerys...). Twoja obecna kampania sie nie zmienia.

**Problem:** Jeff 21.10 (doslownie): "bohaterowie Jaime Lannister i Tyrion nie sa bohaterami, zrob ich lordami Lannisterow, tak samo innych, Hodor nie moze byc
bohaterem, bo idzie z Branem i potem umiera, wywal bohaterow, ktorzy sa de facto po stronie i w jakiejs fabule"; po liscie: "A - tak, zgadzam sie przeniesc,
ale pamietaj, ze potem Tyrion zmieni barwy, zeby postacie przechodzily zgodnie z fabula do innych stron; B - ok usun; C - ok do wynajecia; D - ok";
Jeyne Westerling zamiast Talisy - "tak"; "zrob wydarzenia wedlug ksiazki"; "rob tez 3 postacie"; "zapisuj przy kazdej zmianie".
Lista: docs (tmp) LISTA-BOHATEROW-ROT-2026-10-21.md (A 22 + Talisa, B 8, C 2, D 30), tabela zmian stron: PROJEKT-PROLOG-298-2026-10-21.md pkt 3.

### Rozpoznanie (dekompilacja 1.4.8, ROT 8.1.8, BK Redux)

- **Skad bohaterowie ROT:** `ROT.CampaignBehaviors.ROTSpawnUniqueWanderers` - `OnNewGameCreated` i KAZDA doba (`OnDailyTick`) wolaja `SpawnHeroes()`:
  dla kazdego `CharacterObject` z `Occupation == Wanderer` i id `ROTwanderer*` (63 wzory w ROT-Content/ModuleData/spspecialcharacters_ROTcustomwanderers.xml)
  `SpawnHero(CharacterObject)` (prywatna) tworzy bohatera `HeroCreator.CreateSpecialHero(wzor, losowe miasto kultury wzoru, bez rodu, wiek wzoru)` - TYLKO gdy
  w `AllAliveHeroes + DeadOrDisabledHeroes` nie ma nikogo z `Template == wzor`. Czyli: zywy albo zapisany zmarly bohater z wzoru = ROT nic nie robi; zniknal -
  ROT robi nowego nastepnego dnia.
- **Gra (CompanionsCampaignBehavior)** tworzy wedrowcow z KAZDEGO wzoru `IsTemplate && Wanderer` (takze ROTwanderer*), ale ROT ma postfiks
  `CompanionsCampaignBehaviorPatches` na `GetCompanionTemplateToSpawn` - wzor `ROTwanderer*` -> null. Gra nigdy nie robi bohatera z wzoru ROT. Gra natomiast:
  `OnHeroKilled` wymazuje niepoznanego zmarlego wedrowca, `WeeklyTick` kazdego zmarlego WEDROWCA po 40 dobach (`IsWanderer` - lord nie), `TryKillCompanion`
  (10%/dobe) usuwa wolnego wedrowca z `_aliveCompanionTemplates` (lord wypada z tej listy przy zmianie zawodu - `OnHeroOccupationChanged`).
- **CrashScribe UC (CrashScribe/src/UniqueCompanions.cs):** prefiks `UnregisterDeadHero` (zmarly unikat zostaje na liscie zmarlych), prefiks ROT `SpawnHero`
  (wzor z listy zmarlych CS nie wraca), przeprowadzka zamiast `TryKillCompanion` (tylko wolny: bez rodu, bez druzyny), straznik duplikatow (usuwa tylko wolnego),
  linia doby (wzory: zywy / martwy / BRAK; "w rodach" = `Clan != null`). UC nic nie przenosi miedzy rodami - zmiany stron sa bezpieczne. Linia doby liczy wzor bez
  bohatera jako "brak" (`tools/sprawdz_logi.py --grupa unikaty` UN-5: 0 ostatniej doby) - usuniete z kanonu musza byc liczone osobno (krok 3).
- **Wedrowiec -> lord w grze:** `CompanionRolesCampaignBehavior.ClanNameSelectionIsDone` (towarzysz gracza zostaje lordem): `RemoveCompanionAction`,
  `Hero.SetNewOccupation(Occupation.Lord)` (zdarzenie `HeroOccupationChanged`), rod. `Hero.Clan` setter: `OnLordRemoved` / `OnLordAdded`, `OnHeroChangedClan`,
  zeruje `HomeSettlement` (liczy sie od nowa). `Hero.Template` = `CharacterObject.OriginalCharacter` - zostaje po zmianie zawodu (ROT i UC dalej widza wzor).
- **Druzyny lordow:** `HeroSpawnCampaignBehavior` - nowa gra: cala petla `OnNewGameCreatedPartialFollowUp` (partie lordow z ludzmi `isNewGame`) biegnie PO
  calym `OnNewGameCreatedEvent` (tak samo pisze ArmouryBehavior przy VillageClimate); potem co dobe `ConsiderSpawningLordParties`. Dowodca:
  `GetBestAvailableCommander` - 1. przejscie z `CanLeadParty()` (zdarzenie `CampaignEvents.CanHeroLeadPartyEvent`, ref bool), 2. przejscie (rody AI) BEZ
  `CanLeadParty`; zawsze `CharacterObject.Occupation == Lord`. BK ma POSTFIKS tej metody (w pokoju zeruje dowodce bez tytulu, nie glowe rodu).
  Dworzanie bez druzyny = nasz nasluch `CanHeroLeadPartyEvent` (false) + postfiks `GetBestAvailableCommander` (dworzanin -> null, jak BK).
- **Zmiana rodu lorda z druzyna:** wzor gry `MarriageAction.HandleClanChangeAfterMarriageForHero` - armia (rozwiazana / opuszczona), `MemberRoster.RemoveTroop`,
  `MakeHeroFugitiveAction`, `DisbandPartyAction.StartDisband` (ludzie odchodza do osady - nic nie znika), `hero.Clan = nowy`. NIE `MakeHeroFugitiveAction` na
  dowodcy (to `DestroyPartyAction` - ludzie przepadaja).
- **Usuniecie:** `KillCharacterAction.ApplyByRemove(hero, showNotification false)` = Lost (bez komunikatu); `ApplyByMurder(hero, null, false)` = smierc bez komunikatu
  (bohater zostaje na liscie zmarlych jako lord - gra go nie wymazuje, UC tez nie). Postac w bitwie dostaje znak smierci i umiera po bitwie (gra).
- **Nowa kampania:** `Campaign.CampaignGameLoadingType == NewCampaign` od `SetLoadingParameters`; `GameStarted` = false az do pierwszego `RealTick` (wczytany zapis:
  true od razu). `McmSettings.Apply()` idzie w `SubModule.OnGameStart` - klucz MCM jest juz znany w `OnNewGameCreated`. Wybor prologu (galaz m101-prolog) jest w
  tworzeniu postaci - PO `OnNewGameCreated`, dlatego roznice prologu ida jako zmiany z wyzwalaczem `prolday:N` liczone w dobie.
- **Wydarzenia ROT:** `ROT.CampaignBehaviors.ROTEventBehavior._events` (List<EventBase>), `EventBase.IsActive` / `TotalEvents` (CompleteEvent: +1). Odczyt
  jak `CrashScribe Fabula.EventState`: trwa = IsActive, bylo = TotalEvents > 0. Nazwy: NedEvent, RenlyDeathEvent (Renly ginie 14 dob po starcie wydarzenia),
  BlackwaterSiegeEvent, BlackwaterEvent (wynik bitwy, StannisWon), RedWeddingEvent (Robb i Catelyn ginu), JoffreyEvent (Purpurowe Gody - Joffrey otruty po
  dobie), RamsayEvent (Roose otruty, Ramsay panem Dreadfort), JonEvent (Jon zabity i wskrzeszony, Krol Polnocy; rod Snow i Giantsbane do Polnocy),
  WallSiegeEvent / WallEvent (Mance pod Murem), HarrenhalSiegeNotificationEvent, RickardKarstarkExecutionEvent, AegonInvasionEvent, DanyInvasionEvent.
  ROT nie uzywa zadnego wzoru ROTwanderer w wydarzeniach (grep ROT.dll: tylko spawn, encyklopedia, kruki) - przeniesienie nic w nich nie psuje.
  Wojna Zelaznych Wysp z Polnoca: `ROTStorylineWars` (sturgia - battania, start 100 dob od startu kampanii) - wyzwalacz `war:sturgia:battania`.
- **Jeyne Westerling** jest lordem ROT: `ROTlord_177`, rod Westerling `ROTclan_45` (Turnia = `castle_EN6`), Robb = `lord_5_1`, Lysa Arryn = `lord_4_6`,
  Daenerys = `lord_1_14`, Renly = `lord_1_9`, Joffrey = `lord_7_1` (ROT.Misc.ROTLords).

### Mapa rodow (nazwa z listy -> id rodu ROT, spclans.xml + ROT.Misc.ROTClans)

| Nazwa w liscie | Id rodu | Krolestwo ROT |
|---|---|---|
| Lannisterowie (Zachod) | `clan_vlandia_1` (Lannister) | `vlandia` (Zachod / Zelazny Tron) |
| dwor korony / Krolewska Przystan (Joffrey, potem Tommen, Cersei) | `clan_nord_1` (Baratheon, Joffrey) | `vlandia` |
| rod Renly'ego (Teczowa Gwardia) | `clan_empire_west_2` (Baratheon, Renly) | `stormlands` |
| Starkowie | `clan_battania_1` (Stark) | `battania` (Polnoc) |
| Greyjoyowie | `clan_sturgia_1` (Greyjoy) | `sturgia` (Zelazne Wyspy) |
| Martellowie | `clan_aserai_1` (Martell) | `aserai` (Dorne) |
| Boltonowie | `clan_empire_south_3` (Bolton) | `battania` |
| Nocna Straz | `clan_battania_2` (Mormont-Nights Watch, Jeor) | `nightswatch` |
| Jon Snow (Nocna Straz, potem Polnoc - JonEvent) | `clan_battania_6` (Snow) | `nightswatch` |
| Wolni Ludzie (Mance) | `clan_sturgia_2` (Rayder) | `freefolk` |
| khalasar / rod Daenerys, szlachta Meereen (Meereen nalezy do niego) | `clan_empire_south_1` (Targaryen) | `empire_s` |
| Druga Synowie | `clan_khuzait_2` (Second Sons) | `empire_s` |
| strona Aegona | `ROTclan_84` (Targaryen, Aegon) | `valyrian` |
| Dolina (Arryn) | `clan_vlandia_3` (Arryn) | `empire` (Dolina) |
| Westerlingowie (Turnia) | `ROTclan_45` (Westerling) | `vlandia` |
| klany gorskie Doliny (Kamienne Wrony) | `stone_crows` (rod pomniejszy) | - |

Braki w ROT: rodu **Stokeworth** nie ma - Bronn po odmowie walki za Tyriona idzie do rodu korony (`clan_nord_1`, w ksiazce to Cersei daje mu Lollys Stokeworth);
**szlachty Meereen** jako osobnego rodu nie ma - Meereen ma rod Targaryen (Daenerys), Hizdahr wchodzi tam jako dworzanin.

### Zasada (kroki - stan na biezaco nizej)

1. Klucz MCM `CanonCompanionsToClans` (domyslnie TAK) i `CanonCompanionsFollowStory` (zmiany stron, domyslnie TAK; dziala tylko z pierwszym). Flaga w zapisie
   przy `OnNewGameCreated` - stary zapis (kampania Jeffa) nic nie dostaje.
2. A - przy nowej kampanii (po utworzeniu wedrowcow przez ROT, przed petla partii lordow): zawod Lord, rod docelowy, wyjscie z karczmy do siedziby rodu;
   druzyna: tak / nie (dworzanin) / pozniej (Bronn po Czarnym Nurcie).
3. B - wzor nie powstaje wcale (prefiks ROT `SpawnHero` w fazie tworzenia nowej gry i przy kazdej dobie z flaga); po starcie zaden wzor A/B nie powstaje drugi raz.
   UC liczy je jako "usuniete z kanonu", nie "brak".
4. Zmiany stron: `Armoury/ModuleData/arm_canon_moves.tsv` (#move), wyzwalacze: wydarzenia ROT, smierc postaci, wojna krolestw, dzien opowiesci, dzien prologu;
   kazda raz (zapis), po kolei dla tej samej postaci.
5. Jeyne/Talisa: Talisa w B, Jeyne - slub z Robbem po zdobyciu Turni przez Polnoc (albo przy egzekucji Karstarka - w ksiazce slub jest wczesniej), po smierci
   Robba wraca do Westerlingow.

### Zmiana na krok

**Krok 2 - A do rodow (Armoury/src/CanonCompanions.cs, ModuleData/arm_canon_moves.tsv #start, Settings.cs + McmSettings.cs, SubModuleMain.cs 2 linie):**
- `arm_canon_moves.tsv` czesc `#start`: 63 wzory - A 22 (rod, druzyna yes/no/later), B 9 (8 z listy + Talisa), C 2, D 30.
- Klucze MCM (grupa "Canon characters (21.10)"): `CanonCompanionsToClans` (domyslnie TAK), `CanonCompanionsFollowStory` (domyslnie TAK, krok 4).
- `CanonCompanionsBehavior` (wlasne zachowanie, zapis `arm_canon_companions` przez SaveText.Sync): `OnNewGameCreated` - flaga nowej kampanii i klucz w tej chwili,
  potem przejscie startu A: `SetNewOccupation(Lord)`, `Hero.Clan = rod`, `IsMinorFactionHero` wedlug rodu, z karczmy do siedziby rodu (HomeSettlement twierdza ->
  lenno -> osada glowy rodu -> stolica krolestwa). Ponowna proba w `OnNewGameCreatedPartialFollowUpEnd`, przy starcie sesji, ostatnia w pierwszej dobie (potem
  start zamkniety; brak bohatera / rodu - linia "przeniesienie POMINIETE").
- Dworzanie (`party` no / later): nasluch `CanHeroLeadPartyEvent` (false) + postfiks `HeroSpawnCampaignBehavior.GetBestAvailableCommander` (wynik-dworzanin ->
  null; licznik w linii doby). Klucz dworzanina = wzor oryginalu (bez rodzicow) - dziecko postaci nie jest dworzaninem.
- Linie: `Postacie kanonu (m101): wpiecie | ...`, `... : start | klucz ..., A 22, B 9, C 2, D 30 | mapa rodow znaleziona N/M | ...`, `... : przeniesienie - X [wzor]
  wedrowiec w <miasto> -> rod <nazwa> (id, krolestwo), lord, druzyna: ... | wyzwalacz start (...)`, linia doby `... : dzien N | A 22: w rodach ... wedrowcy ...`.

**Krok 3 - B nie powstaja + UC (Armoury CanonCompanions.cs, CrashScribe UniqueCompanions.cs, tools/sprawdz_logi.py --grupa unikaty):**
- Prefiks `ROTSpawnUniqueWanderers.SpawnHero(CharacterObject)` (AccessTools.DeclaredMethod): w fazie tworzenia nowej gry (`NewCampaign`, `!GameStarted`, klucz)
  i potem przy fladze kampanii - wzor B nie powstaje nigdy, wzor A nie powstaje po zamknieciu startu (postac kanonu, ktora zginela albo zostala usunieta, nie
  wraca jako nowy wedrowiec). Linia `ROT chcial stworzyc X [wzor] (grupa B ...) - nie powstaje` raz na sesje na wzor; licznik w linii doby.
  Zapas: bohater B, ktory jednak powstal (latka nie wpieta) - `KillCharacterAction.ApplyByRemove` (Lost, bez komunikatu) przy starcie, linia `B usuniety po utworzeniu`.
- CrashScribe UC: `CanonBlocked(wzor)` (refleksja na `Armoury.CanonCompanions.IsCanonBlocked(string)`, raz na proces; brak Armoury = jak dotad). Linia doby UC:
  nowe pole `usuniete z kanonu N` (wzor bez bohatera albo Lost, ktorego ROT nie odtworzy) - nie wlicza sie do `brak` (UN-5); usuniecie Lost postaci kanonu ma
  wlasna linie ("przez Armoury (postacie kanonu m101) ... ROT go nie odtworzy"). Grupa `unikaty`: regex z polem opcjonalnym (stare logi czytaja sie jak dotad),
  UN-6 liczy usunietych z kanonu do wyjasnienia spadku wolnych w karczmach.
- UC sam nie przenosi postaci miedzy rodami: przeprowadzka tylko wolnego wedrowca (bez rodu), straznik duplikatow tylko wolnego - postacie A w rodach i zmiany
  stron (krok 4) sa poza zasiegiem UC. Wedrowiec z ruchu `wander` (np. Ogar w Dorzeczu) moze zostac przeniesiony przez UC do miasta kultury wzoru, gdy gra
  wylosuje go w TryKillCompanion - to zmiana miasta, nie strony.

**Krok 4 - zmiany stron (Armoury CanonCompanions.cs, arm_canon_moves.tsv #move):**
- Tabela `#move` (34 wiersze: 25 dla kazdej kampanii + 9 tylko z prologiem): `id, who, name, trigger, action, mode, message, note`. Wyzwalacze: `ev:<wydarzenie ROT>`
  (wystartowalo), `done:<wydarzenie>` (zakonczone), `dead:<id bohatera>`, `war:<krolestwo>:<krolestwo>`, `own:<osada>:<krolestwo>`, `rotday:N`, `prolday:N`, laczone `|`,
  `+N` = N dob po pierwszym spelnieniu (dzien pierwszego spelnienia w zapisie). Akcje: `clan:` / `court:` (lord z druzyna / dworzanin), `lead`, `prisoner:<rod albo osada>`
  (siedziba rodu, potem druzyna glowy rodu), `free:<rod>`, `wander:<kultura>` (wedrowiec do wynajecia w miescie tej kultury), `remove` (Lost, bez komunikatu),
  `die` (smierc bez komunikatu - lord zostaje na liscie zmarlych), `marry:<id>`, `wed:<id>` (slub i rod malzonka).
- Wykonawca: raz na dobe po zamknietym starcie i przy kluczu `CanonCompanionsFollowStory`. Wiersze jednej postaci po kolei: wczesniejszy, ktorego wyzwalacz nie
  zaszedl, gdy pozniejszy juz tak - POMINIETY ("wyprzedzona"); wczesniejszy w toku (bitwa, oblezenie, niewola AI, brak miejsca niewoli) wstrzymuje pozniejsze, po
  60 dobach czekania - pominiety. Kazda zmiana raz (`done` w zapisie). Pominiete z linia: postac martwa, brak postaci, u gracza (rod, towarzysz, druzyna), w niewoli
  gracza; glowa rodu nie zmienia rodu i nie odchodzi. Wiersz `mode prolog` w klasycznym starcie - pominiety od razu ("tylko kampania z prologiem").
- Zmiana rodu lorda: wzor gry `MarriageAction.HandleClanChangeAfterMarriageForHero` (armia rozwiazana / opuszczona, `RemoveTroop`, `MakeHeroFugitiveAction`,
  `DisbandPartyAction.StartDisband` - ludzie odchodza, nic nie przepada), zdjecie z namiestnika, `Hero.Clan`, `TeleportHeroAction` do siedziby. Jeniec:
  `TakePrisonerAction.Apply` (po wyjsciu z druzyny i osady); uwolnienie: `EndCaptivityAction.ApplyByReleasedByChoice`. Wedrowiec: `Hero.Clan = null`,
  `SetNewOccupation(Wanderer)`, miasto kultury (bez oblezonych, bez miasta gracza, bez Innych). Slub: `Hero.Spouse` (obie strony), bez zmiany rodu drugiej osoby.
- Wiesc (EN, kolumna message) tylko gdy gracz zna postac (`HasMet`) albo jego krolestwo jest jedna ze stron; inaczej tylko log.
- **Dzien opowiesci - jedno miejsce do podpiecia prologu:** `CanonCompanions.PrologDays()` (dzis 0). Galaz m101-prolog zwraca tam dlugosc prologu (dni od startu
  kampanii do startu opowiesci ROT); `StoryDay()` = dni od `CampaignStartTime` - `PrologDays()` (wyzwalacz `rotday`), `IsProlog()` wlacza wiersze `mode prolog` i
  `prolday:N` (dni od startu kampanii). Wybor prologu musi byc znany w pierwszej dobie (tworzenie postaci jest po `OnNewGameCreated`, start A jest przed nim -
  dlatego roznice prologu ida jako wiersze `prolday:0` itd.). Wyzwalacze `ev:` / `done:` ida za zegarem ROT/Fabuli - prolog ich nie dotyczy.
- Linie: `Postacie kanonu (m101): zmiana strony #id - X [wzor] <przed> -> <po> (akcja; powod) | wyzwalacz ... (dzien kampanii, opowiesci) | wiesc: ...`,
  `... #id POMINIETA - ... - powod`, `... #id CZEKA - ... - powod` (raz na sesje), w linii doby: dzis wykonane / pominiete, razem, czeka na wyzwalacz, w toku [lista].

**Tabela zmian (wedlug ksiazki; PROJEKT pkt 3, decyzje Claude'a tam, gdzie ROT nie ma rodu albo wydarzenia):**

| # | Postac | Wyzwalacz | Akcja | Uwaga |
|---|---|---|---|---|
| m01 | Jaime | wojna Polnoc-Zachod +5 dob | jeniec Starkow | Szepczacy Las (w ksiazce przed egzekucja Neda; wojna ROT zaczyna sie z NedEvent) |
| m02 | Jaime | koniec BlackwaterEvent | wolny, Lannisterowie | Catelyn go uwalnia (wiesc) |
| p01/p02 | Tyrion (prolog) | prolog 110 / 132 | jeniec w Orlim Gniezdzie / wolny do Lannisterow | gospoda na rozstajach, proces |
| m03 | Tyrion | JoffreyEvent +40 | Druga Synowie (z druzyna) | wiesc "Tyrion Lannister has fled across the narrow sea." |
| m04 | Tyrion | DanyInvasionEvent | rod Targaryen (Daenerys) | Namiestnik Krolowej (wiesc) |
| p03/p04 | Bronn (prolog) | prolog 0 / 132 | wedrowiec w Dorzeczu / Lannisterowie | najemnik do gospody, potem czempion Tyriona |
| m05 | Bronn | koniec BlackwaterEvent | moze prowadzic druzyne | pasowany |
| m06 | Bronn | JoffreyEvent +30 | rod korony | Stokeworth - brak w ROT |
| m07 | Podrick | JoffreyEvent +40 | wedrowiec w Dorzeczu | z Brienne szuka Sansy |
| p05/p06 | Shagga (prolog) | prolog 0 / 132 | Kamienne Wrony / przy Tyrionie | |
| m08 | Shagga | JoffreyEvent +40 | Kamienne Wrony | wraca w gory |
| m09 | Qyburn | JoffreyEvent +40 | dwor korony (Cersei) | po smierci Tywina |
| m10 | Ogar | koniec BlackwaterEvent | wedrowiec w Dorzeczu | dezercja (wiesc) |
| m11 | Ogar | koniec RedWeddingEvent +20 | usuniety | Cicha Wyspa |
| m12 | Varys | AegonInvasionEvent | dwor Aegona | |
| m13/m14 | Baelish | JoffreyEvent +5 / +15 | dwor Arrynow (wiesc) / slub z Lysa (`lord_4_6`) | ucieczka z Sansa do Doliny |
| m15 | Jacelyn | koniec BlackwaterSiegeEvent albo BlackwaterEvent | smierc | |
| m16 | Brienne | smierc Renly'ego (`lord_1_9`) | dworka Starkow | przy Catelyn |
| m17 | Brienne | koniec RedWeddingEvent albo smierc Robba | wedrowiec w Dorzeczu | szuka Sansy |
| m18 | Theon | wojna Zelazne Wyspy-Polnoc | Greyjoyowie (z druzyna) | wiesc |
| m19 | Theon | koniec BlackwaterEvent +10 | jeniec Boltonow | upadek Winterfell (ACOK) jest w czasie Czarnego Nurtu - PROJEKT mial RamsayEvent (smierc Roose'a), ksiazka: wczesniej |
| p07/p08 | Osha (prolog) | prolog 0 / 62 | Wolni Ludzie / sluzba Starkow | |
| m20 | Osha | koniec BlackwaterEvent +10 | usunieta | ucieka z Rickonem |
| p09 | Benjen (prolog) | prolog 80 | usuniety | znika za Murem (klasyczny start - zostaje w Nocnej Strazy, jak lista A) |
| m21 | Samwell | koniec WallEvent +20 | usuniety | Stare Miasto |
| m22 | Yoren | HarrenhalSiegeNotificationEvent albo dzien opowiesci 120 | smierc | ludzie Lorcha |
| m23 | Quentyn | JonEvent | smierc | smoki w Meereen (ADWD, ten sam czas) |
| m24 | Hizdahr | RamsayEvent | slub z Daenerys (`lord_1_14`) | ADWD |
| m25 | Wun Wun | JonEvent | przy Jonie (rod Snow) | ROT JonEvent przenosi rod Snow do Polnocy |

**Krok 5 - Jeyne Westerling zamiast Talisy (arm_canon_moves.tsv; kod: akcja `wed` z kroku 4):**
- Talisa (`ROTwanderer45`) jest w grupie B - nie powstaje (krok 3).
- m26: Jeyne (`ROTlord_177`, rod Westerling `ROTclan_45`) - wyzwalacz `own:castle_EN6:battania` (Turnia w rekach Polnocy) albo `ev:RickardKarstarkExecutionEvent`
  (w ksiazce slub jest przed egzekucja Karstarka) - `wed:lord_5_1`: slub z Robbem (oboje zywi, bez malzonka, nie u gracza, nie w niewoli) i przejscie do rodu
  Starkow (dworka, bez druzyny); wiesc "Robb Stark has wed Jeyne Westerling of the Crag." (gracz zna Jeyne albo jego krolestwo to Polnoc / Zachod).
- m27: smierc Robba (`dead:lord_5_1`, w ROT RedWeddingEvent) - Jeyne wraca do Westerlingow (`clan:ROTclan_45`). Robb zginal przed slubem - m26 wyprzedzona,
  m27 zastaje Jeyne w rodzie ("juz w tym rodzie").

**Narzedzie:** `tools/sprawdz_logi.py --grupa m101postacie <Armoury-*.log> [--crashscribe <session-*.log>]` - MP-0 start (nowa kampania z kluczem; inaczej INFO),
MP-1 ERROR CanonCompanions i potkniecia 0, MP-2 latki 2/2, MP-3 mapa rodow N/N, MP-3b odwolania tabeli zmian N/N (literowka = zmiana nigdy nie zajdzie), MP-4 przeniesienia A (kazdy wzor raz, BRAK rodu 0), MP-5 zaden wzor A nie jest
wedrowcem bez zmiany `wander` (od drugiej linii doby), MP-6 zaden B nie zyje, MP-7 zmiana strony najwyzej raz, MP-8 lista zmian (INFO), MP-9 A w rodach (INFO),
MP-10 UC: odrodzenia wzorow A/B 0 (log CrashScribe). Sprawdzone na sztucznym logu (wszystkie wiersze licza sie bez wyjatku).

**Poprawka 2 (wazne, PRAWDZIWE) - brak pliku TSV w grze byl niewidoczny dla narzedzia:** dowod - sztuczny log recenzenta (`plik brak pliku arm_canon_moves.tsv | A 0, B 0 ... 0/0`) dawal 12 progow, NIE 0. Zmiana (tools/sprawdz_logi.py): MP-0 = NIE, gdy pole `plik` linii startu nie jest `wczytany`; nowy MP-0b - A/B/C/D z logu = liczby z `Armoury/ModuleData/arm_canon_moves.tsv` w repo (A > 0); MP-3 - N/N, N > 0 i N = liczba rodow tabeli w repo (17: rody A, `#rescue`, cele clan/court/free); MP-3b - N > 0; MP-4 - A z repo (> 0); MP-6 - ostatniej doby `nie powstali + usunieci` = B z repo (9); MP-11 (INFO) - linie ratunku i usuniec przez cudzy kod. Sprawdzone: log recenzenta -> 14 progow, NIE 6 (MP-0, MP-0b, MP-3, MP-3b, MP-4, MP-6); sztuczny log zgodny z tabela (tmp synth-ok.log z synth_log.py) -> NIE 0. W naglowku wpisu linia pliku do wgrania razem z DLL.

### Poprawki po recenzji (HEAD f5492999)

**Poprawka 1 (wazne, PRAWDZIWE) - ROT niszczy rody postaci A (DestroyClanAction), postacie znikaly bez zmian m16/m17/m21:**
- Dowod (dekompilacja ROT 8.1.8 i gry 1.4.8): `RenlyDeathEvent.ExecuteRenly` - `DestroyClanAction.ApplyByClanLeaderDeath(ROTClans.BaratheonRenly)` (clan_empire_west_2)
  PRZED `KillCharacterAction.ApplyByMurder(Renly)`, potem `_clans.Remove(rod)` (rod znika z `Clan.All`); `JeorMutinyEvent.DailyTickEvent` - dzien >= 150, dobe po starcie:
  `ChangeRulingClanAction` (Nocna Straz -> Thorne) i `DestroyClanAction.ApplyByClanLeaderDeath(ROTClans.MormontNightsWatch)` (clan_battania_2); `BastardsEvent` - Bolton
  albo Snow; `StannisDeathEvent` - rod Stannisa. Gra `DestroyClanAction.ApplyInternal`: najpierw `OnClanDestroyed`, `DeactivateClan`, partie rodu (DestroyPartyAction), potem
  `KillCharacterAction.ApplyByRemove` kazdego zywego czlonka poza glowa (ClanLeaderDeath). Brienne znikala przy smierci Renly'ego, Benjen/Samwell/Yoren przy buncie u
  Crastera (dzien >= 150, WallEvent 260+) - m16, m17, m21 nigdy by nie zaszly, a UC pisal "usuniety przez Armoury".
- Zmiana (Armoury CanonCompanions.cs): nasluch `CampaignEvents.OnClanDestroyedEvent` (idzie PRZED usunieciem czlonkow - lista `destroyedClan.Heroes` jest brana po
  zdarzeniu): postac kanonu (oryginal wzoru A/B albo bohater z kolumny `who`) z niszczonego rodu przechodzi do rodu z nowej sekcji `#rescue` w `arm_canon_moves.tsv`
  (ta sama droga co zmiana strony: `MoveToClan` - armia, druzyna, zbieg, teleport do siedziby; jeniec - tylko rod, zostaje w niewoli; w bitwie / glowa rodu / brak rodu -
  bez ratunku, linia). Wiersze: rod Renly'ego `clan_empire_west_2` -> Tarth `clan_empire_west_5` (ROT przenosi Tarth do Dragonstone, rodu nie niszczy; Brienne jest
  tam najwyzej dobe - m16 `dead:lord_1_9` wykonuje sie w najblizszej dobie, Renly ginie w tej samej chwili); Nocna Straz Jeora `clan_battania_2` -> Snow `clan_battania_6`
  (w ksiazce Jon zostaje Lordem Dowodca po Jeorze; ROT daje wladze Thorne'owi - wybralem ksiazke). Bez wiersza (Bolton / Snow w BastardsEvent, Stannis) - postac znika
  z rodem, z linia (Myranda i Locke gina z Boltonami jak w serialu; Wun Wun i Benjen przy wygranej Ramsaya znikaja razem z rodem Snow - decyzja: zostaja z Jonem do konca, bez kolejnego ratunku).
- Zrodlo usuniecia: wlasne usuniecia Armoury (`remove`, zapas B) ida przez `RemoveQuiet` ze znacznikiem; nasluch `HeroKilledEvent` zapisuje zrodlo kazdego Lost postaci
  kanonu (`lost` w zapisie arm_canon_companions), cudze ma linie `postac kanonu usunieta (Lost) NIE przez Armoury - X [wzor] przez zniszczenie rodu ...`.
  CrashScribe UC: `CanonRemovedBy(string)` (refleksja, obok IsCanonBlocked) - linia Lost i linia doby podaja prawdziwe zrodlo (Armoury m101: #id / zniszczenie rodu
  ... DestroyClanAction / inny kod); bez odpowiedzi Armoury UC opisuje zrodlo wedlug rodu (rod wyeliminowany = DestroyClanAction), nigdy "Armoury".
- Kontrola odwolan: rody zniszczone w kampanii (`gone` w zapisie) i `MBObjectManager` licza sie jako znalezione - po RenlyDeath rod Renly'ego nie daje "BRAK" w linii
  startu. `#rescue` wchodzi do mapy rodow: **17/17** (Tarth nowy). Linia startu: `ratunek przy zniszczeniu rodu (#rescue) 2 wierszy, rody zniszczone w kampanii N [...],
  postacie kanonu usuniete (Lost) N`; linia doby: `ratunek przy zniszczeniu rodu dzis N (od wczytania N), usuniete przez cudzy kod dzis N`; linia
  `Postacie kanonu (m101): ratunek - X [wzor] <przed> -> <po> | rod ... niszczony (DestroyClanAction ...), #rescue A -> B | dzien N`.
- Build Armoury i CrashScribe kod 0. Status: DO SPRAWDZENIA (ratunek widac dopiero przy RenlyDeath / JeorMutiny - dlugi bieg).

**Poprawki drobne (recenzja) - kod Armoury CanonCompanions.cs, build kod 0:**
- **Falszywe blokady A w prefiksie ROT SpawnHero (PRAWDZIWE):** ROT wola SpawnHero dla 63 wzorow co dobe, a tworzy tylko, gdy nikt nie ma tego wzoru - prefiks liczyl i
  logowal 22 wzory A, ktorych lordowie zyja (licznik 31 zamiast 9). Teraz najpierw `AnyWithTemplate` (ten sam warunek co ROT: zywy albo zmarly, takze dziecko) - wtedy
  przepuszcza bez linii i licznika; linia `ROT chcial stworzyc ...` i licznik tylko przy prawdziwej blokadzie (B zawsze 9 na dobe, A dopiero po usunieciu i wymazaniu).
- **Dworzanin w rodzie gracza (PRAWDZIWE):** `IsCourtier` = false, gdy postac jest w rodzie gracza albo jego towarzyszem (np. po slubie z Brienne, Osha, Myranda,
  Hizdahrem) - o druzynie decyduje gracz (ClanPartiesVM pyta `CanLeadParty`). Znacznik zostaje w zapisie, nie dziala w rodzie gracza.
- **Slub marry/wed bez sprzatania romansu (PRAWDZIWE):** po `Hero.Spouse` jak gra w `MarriageAction.ApplyInternal`: koniec wszystkich zalotow obojga (petla jak
  `Romance.EndAllCourtships` - internal) i `ChangeRomanticStateAction.Apply(..., Marriage)`; linia zmiany podaje `zakonczone zaloty N`. `OnBeforeHeroesMarried`
  (internal dyspozytora) pominiete - rod ustawia nasz `MoveToClan`.

### Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)

- **Zniszczenie rodu przez ROT (poprawka 1):** zmiana rodu w srodku `DestroyClanAction` (nasluch `OnClanDestroyed`) - postac wychodzi z rodu przed petla partii i
  usuniec; druzyna postaci jest rozwiazywana (`StartDisband`, pusta - `DestroyPartyAction` od razu), potem gra niszczy pozostale partie rodu jak dotad. Inni sluchacze
  `OnClanDestroyed` (BK tytuly, ROT) widza rod juz bez tej postaci - jak gdyby odeszla dzien wczesniej. Do sprawdzenia w dlugim biegu: linia `ratunek` przy RenlyDeath
  (Brienne -> Tarth, potem m16) i JeorMutiny (Benjen, Samwell, Yoren -> Snow, jesli zyja), brak linii `NIE przez Armoury` dla Brienne/Nocnej Strazy.

- **Kolejnosc nasluchow przy nowej grze:** ROT tworzy wedrowcow w swoim `OnNewGameCreated`; jesli nasz nasluch idzie wczesniej (kolejnosc modulow), A przechodza
  przy `FollowUpEnd` (bez partii z petli nowej gry - partie przyjda z doby rodu). B blokuje faza tworzenia gry niezaleznie od kolejnosci. W logu: `wyzwalacz start
  (OnNewGameCreated)` albo `(FollowUpEnd)`.
- **Partie lordow:** postac z `party yes` dostaje druzyne tylko w limicie rodu (ClanTierModel) - nie ma nowych partii ponad limit; BK w pokoju zeruje dowodce bez
  tytulu (postfiks BK), wiec Jaime/Tyrion moga czekac na wojne. Dworzanin: nasz postfiks dziala po postfiksie BK (obie zeruja wynik - bez sprzecznosci).
- **UC (CrashScribe):** zmiany stron poza zasiegiem przeprowadzek i straznika duplikatow (tylko wolni wedrowcy). UN-5 nie liczy usunietych z kanonu jako `brak`.
  Wedrowiec z ruchu `wander` (Ogar, Brienne, Podrick, Bronn w prologu) moze zostac przeniesiony przez UC do miasta kultury wzoru (TryKillCompanion gry, 10%/dobe
  na jeden wzor) - to zmiana miasta, nie strony.
- **Usuniecia:** `remove` = Lost (lord zostaje na liscie zmarlych - gra wymazuje tylko wedrowcow; wedrowiec po 40 dobach zniknie z listy, ale wzor jest zablokowany -
  ROT go nie odtworzy). `die` = smierc bez komunikatu (UC zapisuje zgon, `UNIKAT ZGINAL`). Encyklopedia pokaze zgon.
- **Jency:** `prisoner` daje postac do lochu siedziby rodu (potem druzyna glowy rodu); gra / RealisticCaptivity moga ja uwolnic (ucieczka, okup) - wtedy pozniejsze
  `free` mowi `juz wolny` i tylko dopilnuje rodu.
- **Kampania Jeffa:** brak flagi w zapisie = nic (prefiks ROT tylko przy fladze albo przy tworzeniu nowej gry z kluczem; dworzan nie ma).
- **Kolizje z m101-prolog:** wlasne pliki (CanonCompanions.cs, arm_canon_moves.tsv); wspolne pliki: Settings.cs (nowa grupa po Book holdings), McmSettings.cs
  (generowany - po scaleniu `python tools/gen_mcm.py`), SubModuleMain.cs (2 linie), UniqueCompanions.cs, sprawdz_logi.py. Prolog: tylko `PrologDays()`.
- **Nie sprawdzone w grze:** nic z tego nie bylo uruchomione (zakaz gry w tym zadaniu). Do sprawdzenia: czy `Hero.Clan` + `SetNewOccupation` na wedrowcu w
  `OnNewGameCreated` nie psuje BK (tytuly, edukacja), czy `TeleportHeroAction` po `MakeHeroFugitiveAction` stawia lorda w siedzibie, czy wiesci pokazuja sie raz.

### Test w grze

1. Autotest: NOWA kampania (flaga start-zloto jak dzis), kilka dob: `python -I tools/sprawdz_logi.py --grupa m101postacie <Armoury-*.log> --crashscribe <session-*.log>`
   (MP-0..MP-7 TAK) i `--grupa unikaty --crashscribe <session-*.log>` (UN-4 odrodzenia 0, UN-5 brak 0, pole `usuniete z kanonu 9`).
2. W linii startu: `A 22, B 9, C 2, D 30`, `mapa rodow znaleziona 17/17, odwolania zmian (osady, postacie, krolestwa, wydarzenia ROT) 23/23`, `ratunek przy zniszczeniu rodu (#rescue) 2 wierszy`, 22 linie `przeniesienie - ...`; w linii doby `w rodach 22`, `wedrowcy 0`, `B 9: zywych 0`.
3. Reczny: encyklopedia - Jaime i Tyrion jako lordowie rodu Lannister, w karczmach brak Hodora i Talisy; Varys/Baelish w Krolewskiej Przystani bez druzyny.
4. Dlugi bieg (zmiany stron): linie `zmiana strony #...` przy wydarzeniach ROT (Czarny Nurt ~dzien 766, Purpurowe Gody ~1083 przy osi Fabuli x4.33).

### Status
- Krok 1 (rozpoznanie + mapa rodow): GOTOWE.
- Krok 2 (A do rodow + dworzanie bez druzyny): ZROBIONE, build Armoury kod 0, DO SPRAWDZENIA w grze.
- Krok 3 (B nie powstaja, A nie odradzaja sie, UC liczy 'usuniete z kanonu'): ZROBIONE, build Armoury i CrashScribe kod 0, DO SPRAWDZENIA.
- Krok 4 (tabela zmian stron i wykonawca): ZROBIONE, build Armoury kod 0, DO SPRAWDZENIA (zmiany przy wydarzeniach ROT widac dopiero w dlugim biegu).
- Krok 5 (Jeyne zamiast Talisy) i grupa `--grupa m101postacie`: ZROBIONE, build kod 0. **Status calosci: DO SPRAWDZENIA** (nic nie uruchomione w grze, nic nie wgrane).
- Poprawka 1 po recenzji (ratunek z rodow niszczonych przez ROT, prawdziwe zrodlo usuniec w UC): ZROBIONE, build Armoury i CrashScribe kod 0, DO SPRAWDZENIA.
- Poprawka 2 po recenzji (narzedzie widzi brak / inna wersje pliku TSV; plik do wgrania w naglowku): ZROBIONE (narzedzie, bez zmian w DLL).
- Drobne w kodzie (prefiks SpawnHero bez falszywych blokad, dworzanin w rodzie gracza, slub z koncem zalotow): ZROBIONE, build Armoury kod 0.
  Zostalo: drobne w danych i opisach (Bronn p04, Varys/Baelish/Quentyn, BEL w Settings.cs, opis dworzan).
