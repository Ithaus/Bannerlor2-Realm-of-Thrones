## m101-postacie - postacie kanonu: bohaterowie ROT do rodow, usuniecie fabularnych, zmiany stron wedlug ksiazki (Armoury + CrashScribe UC)

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

### Status
- Krok 1 (rozpoznanie + mapa rodow): GOTOWE.
- Krok 2 (A do rodow + dworzanie bez druzyny): ZROBIONE, build Armoury kod 0, DO SPRAWDZENIA w grze.
- Krok 3 (B nie powstaja, A nie odradzaja sie, UC liczy 'usuniete z kanonu'): ZROBIONE, build Armoury i CrashScribe kod 0, DO SPRAWDZENIA. Zostalo: kroki 4-5,
  grupa `sprawdz_logi.py --grupa m101postacie`.
