# Towarzysze - czy Tyrion / Jaime Lannister maja kopie? (pytanie Jeffa 14.10)

Oznaczenia: [P] log / dane z gry, [K] kod plik:linia (dekompilacja), [D] plik danych XML, [S] szacunek / wniosek.
Nic nie zmieniano, gry nie uruchamiano, nic nie zapisano w folderze gry. Rozpoznanie: dane ROT, kod gry / BK / BKROTPatch / ROT / nasz, logi autotestow 13-14.10, logi AIInfluence z Twojej kampanii "Ithauser" (wrzesien), naglowki zapisow.

## 0. Odpowiedz dla Jeffa (prosto) - po weryfikacji (rozdz. 7)

1. To nie jest kopia lorda: w ROT Tyrion i Jaime w ogole NIE sa lordami. Rod Lannister w danych to Tywin, Kevan, Lancel, Willem i Stafford (Cersei nosi nazwisko Lannister, ale startuje w rodzie Baratheonow Joffreya). ROT celowo zrobil z Tyriona, Jaimego i 61 innych postaci (glownie z ksiazek, kilka wymyslonych przez ROT) "unikalnych wedrowcow" do najecia w karczmie - kazdy ma wlasne kwestie ("I'm Tyrion of House Lannister").
2. W danych ROT nie ma ani jednej pary "wedrowiec i lord o tym samym imieniu" - sprawdzone dwa razy, niezaleznie (63 unikaty wobec ok. 2300-2400 lordow; tez 14 unikalnych dowodcow najemnikow ROT - zaden to nie Tyrion ani Jaime).
3. ROT robi unikat tylko wtedy, gdy na swiecie nie ma nikogo z jego "wzorem", wiec dwoch takich samych naraz ta droga nie bedzie, a zaden mod nie robi z nich kopii po cichu. W czterech testach z 14.10 Tyrion i Jaime powstali dokladnie raz, a dzienna lista unikatow (41 plikow) nigdy nie pokazala dwoch Tyrionow jednoczesnie.
4. "Tyrion z wlasna armia" to ten sam Tyrion. W kampanii Ithauser (wrzesien) Tyrion prowadzil Twoj konwoj (85 ludzi), a Jaime sam wygrywal bitwy - zawsze jako Twoj rod Karda (16 bitew, zero innych rodow). Druga mozliwosc: Ty sam mogles zrobic z niego pana wlasnego rodu (Spoils of War "Create from companion", domyslna nazwa "Tyrion Lannister's Company", albo gra, gdy jestes krolem i dajesz mu lenno). To nadal ten sam Tyrion. Sladu tego w logach nie ma.
5. Prawdziwy blad jest inny: unikaty sie odradzaja. Gra moze po cichu usunac wolnego wedrowca (niepoznanego wymazuje od razu), a zmarlych wedrowcow wymazuje po 40 dniach. Wtedy ROT nastepnego dnia robi nowego Tyriona: ta sama twarz, nowa osoba. W testach Tyrion zniknal i wrocil 3 razy, a Jaime w Ithauser to prawie na pewno juz druga "wersja".
6. Czyli martwy Tyrion moze wrocic do karczmy, a zniknely - pojawic sie w innym miescie, ale dwoch naraz z tej drogi nie ma.
7. Jedyna droga w kodzie do DRUGIEGO "Tyriona Lannistera" naraz: syn towarzysza-unikatu dostaje imie, twarz i stroj ojca. Jesli ojciec byl wczesniej zrobiony panem rodu (bez tytulu rycerskiego BK), syn jest LORDEM "Tyrion Lannister" i moze prowadzic armie. BK blokuje to tylko u ojcow z tytulem. W logach tego nie ma.
8. Propozycja na osobny krok (CrashScribe, MCM, domyslnie wlaczone): unikat, ktory naprawde zginal, zostaje martwy (a "przeprowadzki" wolnych zostaja jak dzis, bo inaczej unikaty znikalyby z karczm na zawsze); straznik duplikatow, ktory nie liczy dzieci; zwykle imie dla dziecka unikatu; codzienna linia w logu, gdzie jest kazdy unikat - nastepnym razem dowod po 1 dniu.

## 1. Pary w danych ROT

### 1a. Ta sama postac jako wedrowiec i lord - BRAK (0 par)

Skrypt porownal kazde imie z `spspecialcharacters_ROTcustomwanderers.xml` (63 szablony `ROTwanderer*`) ze wszystkimi postaciami z ROT-Content, ROT-Map, ROT-Dragon, BKROTPatch, BK, NavalDLC, SandBox i SandBoxCore (7703 wpisy NPCCharacter, w tym 2340 lordow: `ROT_lords.xml`, `lords.xslt`, `naval_lords.xslt`). Porownania: imie identyczne, imie bez "Lord/Ser/Maester..." i bez "of the ..." oraz to samo imie i nazwisko. Wynik: **0 / 0 / 0** [D].
Tyrion i Jaime nie wystepuja jako postacie w `ROT_lords.xml`, `lords.xslt` ani `ROT_heroes.xml`. W `ROT_heroes.xml` sa tylko w opisach innych postaci (np. :306 Myrcella, :336 Addam Marbrand) [D].

Szablony [D] `ROT-Content/ModuleData/spspecialcharacters_ROTcustomwanderers.xml`:
- `:3` `ROTwanderer1` "Jaime Lannister", culture vlandia, `is_template="true" occupation="Wanderer"`
- `:407` `ROTwanderer8` "Tyrion Lannister", culture vlandia, `is_template="true" occupation="Wanderer"`

Zamiar ROT [D] `ROT_module_strings.xml`: `:645` "I'm surprised you don't recognized the infamous Kingslayer. I'm Ser Jaime of House Lannister." (backstory_a.ROTwanderer1), `:694-700` "Why the googly eyes? You never saw a dwarve before." / "I'm Tyrion of House Lannister." (prebackstory / backstory_a.ROTwanderer8). To sa kwestie rozmowy przy najmowaniu w karczmie, wiec Tyrion i Jaime jako towarzysze to **CECHA ROT**.

### 1b. Wedrowcy-unikaty, ktorych rod ma w ROT lordow (to nie te same osoby)

| Wedrowiec (szablon, linia [D]) | Kultura | Lordowie tego rodu w danych [D] |
|---|---|---|
| Jaime Lannister (ROTwanderer1, :3) | vlandia | Tywin (lord_4_1, lords.xslt:7011), Cersei (lord_4_2, :7058 - POPRAWKA 7b: rod clan_nord_1, nie Lannister), Kevan (lord_4_13, :7535), Lancel (lord_4_14, :7560), Willem (lord_4_141, :7587), Stafford (lord_4_15, :7613); rod `clan_vlandia_1` (spclans.xml:73) |
| Tyrion Lannister (ROTwanderer8, :407) | vlandia | jw. |
| Sandor Clegane (ROTwanderer3, :116) | vlandia | Gregor Clegane (lord_4_21) |
| Samwell Tarly (ROTwanderer5, :232) | reach | Randyll, Talla, Dickon Tarly |
| Brienne of Tarth (ROTwanderer9, :462) | stormlands | Selwyn, Agnus, Endrew Tarth |
| Theon Greyjoy (ROTwanderer20, :967) | sturgia | Balon, Victarion, Yara, Asha, Euron Greyjoy |
| Quentyn Martell (ROTwanderer26, :1232) | aserai | Doran, Oberyn, Trystane Martell |
| Benjen Stark (ROTwanderer38, :1742) | nightswatch | Robb, Bran, Arya, Sansa, Eddard, Catelyn, Rickon Stark |
| Barry B Beesbury (ROTwanderer36, :1991) | reach | 5 lordow Beesbury (ROTlord_262-266) |
| Arion Sand (ROTwanderer40, :2188) | aserai | 6 "Sand" (Tyene, Obara, Nymeria, Perrin, Morion, Daemon) - to nazwisko bekarta, a nie rod |
| Ser Beric Karstark (ROTwanderer52, :2322) | battania | Rickard, Alys, Harold, Arna Karstark |
| Ser Cristal Hightower (ROTwanderer65, :3424) | reach | Baelor, Garth, Gunthor, Baela Hightower |
| Tae Hill (34, :2650) / Ash Pyke (59, :2999) / Alyssa Storm (63, :3280) / Isa Storm (66, :3495) | rozne | Johanna Hill / Cotter Pyke / Rolland Storm - tylko zbieznosc nazwiska |

Pozostali unikaty bez rodu lordow w ROT: Lord Varys, Bronn, Podrick Payne, Gendry, Jaquin Hagar, Mya Stone, Qyburn, Syrio Forel, Hodor, Rago, Xaro Xhoan Daxos, Kinvara, Osha, Shagga, Myranda, Hizdahr zo Loraq, Locke, Maester Cressen, Jacelyn Bywaters, Lord Petyr Baelish, Yoren, Dick Crabb, Wun Wun, Talisa i postacie wymyslone przez ROT (Ser Kemal, Guts, Bedir Khan...) [D].
Na marginesie: `notablesROT.xml:71354` "Damion Lannister" i `:71380` "Lucion Lannister" maja occupation Wanderer, ale nie sa szablonami ani bohaterami i nic ich nie uzywa (grep po wszystkich XML). Nie da sie ich najac [D].

## 2. Kto tworzy bohaterow - czy cos robi kopie Tyriona? [K]

Dekompilacja (scratchpad sesji c36b73ca): `rotdec/` = ROT.dll (md5 27c8957d...), `bkdec/` = BannerKings.dll (90a45bba...), `tow/` = TaleWorlds.CampaignSystem.dll 1.4.8 (8915d942...), `tow/bkrot/` = BKROTPatch.dll, `tow/sow/` = RealisticLoot.dll.

**Imie i twarz.** Bohater zrobiony z szablonu wedrowca dostaje pelne imie = nazwa szablonu (`tow/NameGenerator.cs:76-79`: `if (hero.IsWanderer) textObject = hero.Template.Name`). Twarz bierze z szablonu, a ROT ma tam BodyProperties = BodyPropertiesMax (spspecialcharacters_ROTcustomwanderers.xml:5-6), wiec kazdy bohater z `ROTwanderer8` to "Tyrion Lannister" z identyczna twarza. `Hero.Template` = `CharacterObject.OriginalCharacter` (`tow/Hero.cs:299`), ustawiane w `CharacterObject.CreateFrom` (`tow/CharacterObject.cs:419`) i zapisywane w zapisie gry (pole `_originCharacter`, `:374, 382`).

| Kod | Co robi | Moze zrobic drugiego Tyriona? |
|---|---|---|
| ROT `ROTSpawnUniqueWanderers` (`rotdec/ROT.CampaignBehaviors/ROTSpawnUniqueWanderers.cs:21, 33-69`, rejestracja `rotdec/ROT/SubModule.cs:287`) | na start gry i **codziennie**: dla kazdego `ROTwanderer*` sprawdza, czy jakis bohater w `AllAliveHeroes + DeadOrDisabledHeroes` ma ten szablon. Jesli nie ma zadnego, tworzy nowego w losowym miescie jego kultury | NIE naraz. TAK po zniknieciu, czyli odrodzenie (nizej) |
| ROT latka `CompanionsCampaignBehavior.GetCompanionTemplateToSpawn` (`rotdec/ROT.HarmonyPatches.CustomCompanions/CompanionsCampaignBehaviorPatches.cs:8-17`) | zwykly spawner towarzyszy gry zwraca null dla `ROTwanderer*` | NIE (zablokowane) |
| Gra `CompanionsCampaignBehavior.TryKillCompanion` (`tow/CompanionsCampaignBehavior.cs:215-235`, DailyTick :145-150) | 10%/dobe losuje szablon z zywych wedrowcow, w tym unikaty ROT (dopisywane przy narodzinach, :207-213). Nienajetego, ktory nie stoi w Twoim miescie, usuwa (`KillCharacterAction.ApplyByRemove`) | posrednio: znika, a ROT nazajutrz tworzy nowego |
| Gra `OnHeroKilled` (:186-193) i `WeeklyTick` (:152-161) | zmarly wedrowiec, ktorego nie poznales, jest od razu wymazany z listy zmarlych (`UnregisterDeadHero`). Kazdy zmarly wedrowiec (takze Twoj towarzysz) jest wymazany po 40 dniach | posrednio: ROT nie widzi juz zmarlego i tworzy nowego, czyli **martwy unikat wraca** |
| Gra, narodziny `HeroCreator.DeliverOffSpring` (`tow/HeroCreator.cs:258-281`, wzorzec = postac rodzica przez `GetCharacterTemplateForOffspring`, `tow/DefaultHeroCreationModel.cs:346-353`) | dziecko dostaje szablon, twarz i stroj rodzica tej samej plci. Imie jest nadawane (:311-314) ZANIM ustawi sie zawod (:320-322), a zawod startowy = zawod z postaci rodzica (`tow/CharacterObject.cs:431`, `tow/Hero.cs:2253`) | **teoretycznie TAK**: syn towarzysza z `ROTwanderer8` ma przy nadawaniu imienia zawod Wanderer, wiec dostaje imie "Tyrion Lannister" i stroj Tyriona. Wymaga slubu towarzysza; w logach brak |
| BK `EvaluateRecruitCompanion` / `GetAdequateTemplate` (`bkdec/BannerKings.Behaviours/BKClanBehavior.cs:1584-1645`) | towarzysze AI z `clan.Culture.NotableTemplates` z zawodem Wanderer | NIE: kultury ROT nie maja wedrowcow w `notable_templates` (spcultures.xml, sprawdzone wszystkie 36 kultur [D]; tag `notable_and_wanderer_templates` z NavalDLC nie trafia, bo ROT go nie ma) |
| BK `EvaluateRecruitKnight` (`BKClanBehavior.cs:1675-1760`) + BKROTPatch (`tow/bkrot/BKROTPatch.Patches/EvaluateRecruitKnightPatch.cs:79-80`) | rycerze AI z tej samej listy; BKROTPatch dodatkowo **wyklucza id z "ROT"** | NIE |
| BK goscie dworu `CouncilData.Update` (`bkdec/BannerKings.Managers.Court/CouncilData.cs:181, 215-221`) | jw., `NotableTemplates` z zawodem Wanderer | NIE |
| BK dworzanie `BKCourtierBehavior.GetTemplate` (`bkdec/BannerKings.Behaviours/BKCourtierBehavior.cs:158-170`) | kolejnosc: gentry BK, potem szablon lorda, potem dowolny lord kultury, a dopiero na koncu **wedrowiec kultury** (w tym unikaty ROT) | NIE w praktyce: kazda kultura z miastem ma lordow obu plci (skrypt `tow/kurt.py` [D]), wiec do wedrowcow nigdy nie dochodzi |
| BK rycerze AI z wlasnego rodu `BKTitleBehavior.ChooseKnightCandidate` (`bkdec/BannerKings.Behaviours/BKTitleBehavior.cs:253-283`) | biora `clan.Companions` / `clan.Heroes` rodu AI | NIE (rody AI nie maja unikatow ROT) |
| BK sluby (`bkdec/BannerKings.Models.Vanilla/BKMarriageModel.cs:254-273`; dolaczenie do rodu `BKMarriageBehavior.cs:699-712`) | wymagaja `Clan != null`: wolny wedrowiec odpada, Twoj towarzysz moze wziac slub | to nie kopia, ta sama osoba w nowym rodzie; furtka do dziecka-imiennika (wyzej) |
| BK bandyci / edukacja / statki / notable, BKROTPatch `CreateHeroAtOccupationPatch`, Spoils of War `SubClanBehavior` (`tow/sow/RealisticLoot.Behaviors/SubClanBehavior.cs:658, 1084, 1890`) | wlasne szablony (`bannerkings_bandithero`, `bannerkings_bookseller_`, `EliteBasicTroop`...) | NIE |
| ROT `SubModule.OnAfterGameInitializationFinished` (`rotdec/ROT/SubModule.cs:452-465`) | atrapa przywodcy bandytow z `lord_*` | NIE |
| Nasze mody (Armoury, CrashScribe, RealisticCaptivity, GrandTourney, ForgeView) | grep w repo i w DLL: brak `CreateSpecialHero`, `CreateChild`, `CompanionOf =`, `SetNewOccupation`, `AddCompanionAction`, `UnregisterDeadHero`, `ApplyByRemove`. W repo tylko komentarze (`CrashScribe/src/Mends.cs:2913, 3719, 4121`, `Armoury/src/UniqueLaw.cs:41`) | NIE |
| AIInfluence (tylko kampania Ithauser; teraz wylaczony) | DLL ma `TryApplyHeroJoinClan`, `ExecuteUnifiedJoinClan`, `CanHeroJoinClanAsCompanion` i `UniqueRoleCharacterManager` (tworzenie postaci przez AI). Dekompilacja sie nie udaje (`BadImageFormatException`, zaciemniony). Akcja `join_clan` ma opis w `AIInfluence/save_data/PEh4X2ByGArW/prompts/actions/KingdomJoinClanSelf.txt` | mogl przenosic postacie miedzy rodami. W `logs/aiActions.log` jest 0 wykonan `join_clan` [P] |

Moduly wlaczone teraz [P]: `rgl_log_33208.txt:88` (Command Args) i naglowki zapisow od `save035` nie maja AIInfluence ani ROT_AIInfluence_Compat. `IronmanPEh4X2ByGArW.sav` (Ithauser) i `save026-034` go maja.

## 3. Logi i zapisy

**Gra (rgl), 4 pelne sesje autotestu 14.10** (`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_33208/37616/39392/40276.txt`). Kazde stworzenie bohatera z szablonu zapisuje linie `creating hero from template with id: X` (`tow/HeroCreator.cs:287`) [P]:
- `ROTwanderer8` (Tyrion) i `ROTwanderer1` (Jaime): **dokladnie 1 raz na sesje**, w paczce startowej nowej gry (np. rgl_log_33208.txt:7985 Jaime, :7992 Tyrion; rgl_log_37616.txt:7987/:7994; rgl_log_39392.txt:7984/:7991; rgl_log_40276.txt:7960/:7967).
- Inne unikaty ROT tworzone ponownie: 26 / 4 / 7 / 2 razy na sesje, np. `ROTwanderer10` (Jaquin Hagar) 4 razy w rgl_log_33208 (:7994 start, :14004, :16821, :16849). **Wszystkie** ponowne utworzenia (39 z 39) wypadaja do 1,5 s po linii `Before Daily Tick`, czyli to dzienny spawner ROT po zniknieciu poprzednika. Nie ma ani jednego z innego kodu (skrypt `tow/rgl.py`).

**Armoury - dzienna kronika unikatow** (`unikaty.log`, kod `Armoury/src/UniqueSpoils.cs:225-272`). Wypisuje KAZDEGO zywego bohatera, ktory nosi dany unikat, a kilku bohaterow laczy przez "; " [K]. 29 plikow z 12-14.10 (`D:\Backup-Bannerlord\logs\...\Logs\*\unikaty.log` + `Modules\Armoury\Logs\*`) [P]:
- `tyrion_clothes` / `tyrion_boots`: zawsze **jeden** posiadacz "nosi Tyrion Lannister" (64 wpisy: 32 clothes + 32 boots), nigdy "nosi X; nosi Y".
- Znikniecie i powrot nastepnego dnia, 3 razy: `2026-10-13-noc34-35/Logs/2026-10-13_13-28-08` dzien 108858 "nigdzie" -> 108859 "nosi Tyrion Lannister"; `2026-10-14-noc/Logs/2026-10-13_21-14-13` 109123 -> 109124 (:251) i 109214 -> 109215 (:339).
- Ta sama kronika pokazuje skutek narodzin: dzieci nosza unikaty rodzica ("Myrielle przebrany przez gre - zostaje przy cersei_crown", "Visenya ... dany_hair, dany_dress", 2026-10-13_21-14-13/unikaty.log poczatek). Twarz i stroj sa po rodzicu, ale imie juz nie.

**Sesja 06:01 (wskazane logi)**: `session-2026-10-14_06-01-14.log` i `Armoury-2026-10-14_06-01-15.log` zawieraja 0 wystapien slow "Tyrion" i "Jaime" (wyniki "Ostyrion" to inny rod). W tym autoteście Tyrion nie prowadzil oddzialu, nie byl w niewoli ani na liscie okupow [P].

**Twoja kampania Ithauser** (`IronmanPEh4X2ByGArW.sav`, dzien 1237, ostatni zapis 04.10, z AIInfluence). Dane AIInfluence w `Modules/AIInfluence/logs` i `save_data/PEh4X2ByGArW` [P]:
- `aiActions.log:1668` `[2026-09-15 11:46:16] Hero Tyrion Lannister joined the player's party`; `:2393` `[2026-09-17 12:54:43] Hero Jaime Lannister joined the player's party`; wiele razy "Keeping companion party for Tyrion Lannister - companions can create caravans".
- `battle.txt` `[2026-09-20 07:29:50] Winner: Jaime Lannister (clan:Karda, kingdom:The North)` i kolejne. Jaime prowadzil **wlasny oddzial** jako Twoj towarzysz (rod Karda).
- `Tyrion_Lannister (CharacterObject_6573).json`: oddzial 85 ludzi ("First Men Armed Convoy Trader" x23, "Convoy Guard"..., "Stark Horseman" x6, "Northern Scout" x5), "traveling to Sunspear, sailing at sea". To Twoj konwoj prowadzony przez Tyriona.
- Na kazdego jest tylko jeden rekord: Tyrion `CharacterObject_6573` (numer z poczatku gry, obok Ser Kemal 6610) i Jaime `CharacterObject_184682`. Wysoki numer Jaimego znaczy, ze powstal duzo pozniej, czyli to **odrodzony** Jaime: pierwszy zniknal albo zginal [S].

**Zapisy** (`Documents\...\Game Saves`) [P]. Naglowek zawiera tylko liste modulow, imie postaci i dzien, a nie liste bohaterow; reszta pliku jest skompresowana i nie byla czytana. Ostatni Twoj zapis to `save039.sav` (08.10, "Harmond", dzien 14.5, bez AIInfluence). Wszystkie nowsze to autotesty.

**Czego nie mam**: logu z Twojej obecnej gry, w ktorym Tyrion albo Jaime bylby lordem rodu Lannister. W zadnym logu nie ma takiego zapisu.

## 4. Werdykt

- **CECHA ROT**: Tyrion i Jaime to zamierzeni unikalni wedrowcy do najecia (szablony + kwestie rozmowy), a nie lordowie Lannisterow [D]. Po najeciu moga prowadzic Twoj oddzial albo karawane pod Twoim rodem, z imieniem "... Lannister" [P: Ithauser].
- **Brak kopii lorda i brak dwoch naraz** w danych, w kodzie wszystkich modow i w logach [D][K][P].
- **BLAD (prawdziwy, inny)**: unikaty ROT sie **odradzaja**. Gra usuwa wolnych wedrowcow (TryKillCompanion) i po 40 dniach wymazuje zmarlych z listy, a strazniczka ROT patrzy tylko na liste. Skutek: martwy Tyrion wraca do karczmy jako nowa osoba z ta sama twarza, a zniknely pojawia sie gdzie indziej [K][P]. Prawdopodobnie to, co Jeff odebral jako "dokladnie ten sam karzel" [S].
- **Furtka teoretyczna**: dziecko towarzysza-unikatu dostaje jego imie, twarz i stroj, bo imie jest nadawane przed zmiana zawodu [K]. Sladu w logach brak.
- Jesli Jeff zobaczy to jeszcze raz w obecnej grze, potrzebny jest dowod: strona encyklopedii Tyriona z polem "Clan" albo linia z pkt 5c.

## 5. Propozycja naprawy (kod osobno; ustawienia MCM po angielsku, CrashScribe / Mends)

a) **`UniqueCompanionsNoRespawn`** (MCM: "ROT unique companions stay unique (no respawn)", domyslnie ON):
   - prefix na `CampaignObjectManager.UnregisterDeadHero(hero)`: pomin, gdy `hero.Template?.StringId` zaczyna sie od `ROTwanderer`. Zmarly unikat zostaje w `DeadOrDisabledHeroes`, wiec spawner ROT go widzi i nie tworzy nowego;
   - `CompanionsCampaignBehavior.TryKillCompanion`: nie usuwac wolnych unikatow ROT. W prefiksie mozna powtorzyc logike gry (:215-235) z wykluczeniem `ROTwanderer*`. Unikat dalej krazy miedzy miastami (SwapCompanions), ale nie "miga".
   - Ryzyko: lista zmarlych rosnie o najwyzej 63 pozycje. Spawner ROT dalej tworzy unikat, gdy go nie bylo nigdy. Do sprawdzenia: czy BK albo nasz kod czyta `DeadOrDisabledHeroes` i liczy tam wedrowcow (grep przed wdrozeniem).
b) **`UniqueCompanionsDuplicateGuard`** (MCM: "ROT unique companions: duplicate guard", domyslnie ON), raz na dobe:
   - grupuj zywych bohaterow po `Template.StringId` z `ROTwanderer*`. Przy wiecej niz 1: zostaw tego w rodzie albo oddziale (Twojego, potem najstarszego), wolnego nowszego usun `ApplyByRemove` (tylko bez rodu, bez oddzialu, nie w miescie gracza). Wpis do logu;
   - nowo narodzone dziecko z szablonem `ROTwanderer*` i imieniem rownym nazwie szablonu dostaje zwykle imie kultury (NameGenerator) z nazwiskiem rodu. Przewidywany wynik: 0 trafien na dobe w autotescie.
c) **Dzienna linia w logu** CrashScribe, bez wylacznika albo `UniqueCompanionsLog`, domyslnie ON: `UNIKATY: Tyrion Lannister [ROTwanderer8] - wolny w Lannisport | towarzysz Karda, oddzial X | lord rodu Y | martwy od dnia N`, liczba zywych na szablon (oczekiwane zawsze <= 1) i licznik odrodzen. Daje dowod w 1 dobe, gdyby Jeff znow cos zobaczyl.
- Tor: M (jedna latka Mends, latwa do wycofania, autotest 2 lata: licznik odrodzen 0, duplikatow 0).
- Nie proponuje robienia z Tyriona i Jaimego lordow Lannisterow ani ukrywania ich: to zmiana tresci ROT, nie naprawa bledu. Jesli Jeff chce, mozna dodac osobny wylacznik.

## 6. Pliki posrednie

Scratchpad sesji `c36b73ca-.../scratchpad/tow/`: `pary.py` (porownanie imion), `nt.py` (notable_templates kultur), `kurt.py` (pokrycie szablonow dworzan BK), `rody.py` (lordowie rodow), `rgl.py` (odrodzenia wzgledem doby), `szuk.py`; dekompilacje `CompanionsCampaignBehavior.cs`, `HeroCreator.cs`, `NameGenerator.cs`, `Hero.cs`, `CharacterObject.cs`, `CultureObject.cs`, `DefaultHeroCreationModel.cs`, `bkrot/`, `sow/`. Wczesniejsze dekompilacje z tej sesji: `rotdec/`, `bkdec/`.

## 7. Weryfikacja (drugi przeglad, sceptyk, 14.10)

Sprawdzone od nowa, wlasnymi skryptami (`tow/wer.py` - porownanie imion, `tow/wer_rgl.py` - odrodzenia wzgledem doby) i na pelnej dekompilacji `TaleWorlds.CampaignSystem.dll` 1.4.8 (`tow/twcs/`, md5 8915d942... jak w rozdz. 2). Hashe ROT.dll (27c8957d...) i BannerKings.dll (90a45bba...) zgodne z tymi, ktore byly dekompilowane. Niczego w grze nie zmieniano, gry nie uruchamiano.

### 7a. Glowna teza "Tyrion / Jaime istnieja dwa razy?" - POTWIERDZONA (z poprawkami w 7b)

- **Dane [D].** Szablony tylko w `spspecialcharacters_ROTcustomwanderers.xml`: 63 sztuki, `:3` Jaime (ROTwanderer1), `:407` Tyrion (ROTwanderer8), oba `is_template="true" occupation="Wanderer"`. Napis "ROTwanderer" jest tylko w 2 plikach XML (ten + `ROT_module_strings.xml`) i w jednym DLL (`ROT.dll`) - nie ma go w zadnej kulturze (`notable_templates`), w BK, BKROTPatch ani w naszych DLL.
- **Niezalezne porownanie imion** (`wer.py`, 15 modulow, 7763 wpisy NPCCharacter, 2371 lord/hero): 0 imion identycznych, 0 "imie + nazwisko", 0 lordow z imieniem Tyrion albo Jaime. "Ostyrion" (`ROT_lords.xml:7403-7494`) to inny rod. Unikalni dowodcy najemnikow ROT (`ROTuniqueleader_*`, `ROT_lords.xml:13411-14927`, 14 szt., tworzeni przez latke `rotdec/ROT.HarmonyPatches.CustomCompanions/HeroSpawnCampaignBehaviorPatches.cs`) - zaden nie jest Tyrionem ani Jaimem [D][K].
- **Skad bierze sie druga postac [K].** Tylko `ROTSpawnUniqueWanderers.cs:52-69`: tworzy unikat, gdy w `AllHeroes` (`:21` = zywi + `DeadOrDisabledHeroes`) nie ma nikogo z `Template == ROTwanderer8`. `Template` = `_originCharacter` (`tow/CharacterObject.cs:419`: `character._originCharacter ?? character`), zapisywany w zapisie gry. Dwoch naraz ta droga nie bedzie. Nowa osoba pojawia sie dopiero po wymazaniu poprzedniej:
  - `CompanionsCampaignBehavior.cs:215-235` (TryKillCompanion) -> `twcs/.../Actions/KillCharacterAction.cs:205-208` (`ApplyByRemove`, detail `Lost`) -> `MakeDead` (:124, :227) i `OnHeroKilled` (:144);
  - `CompanionsCampaignBehavior.cs:186-193`: niepoznany wedrowiec wymazany od razu (`UnregisterDeadHero`, `twcs/.../CampaignObjectManager.cs:477-482`);
  - `:152-161` (WeeklyTick): kazdy zmarly wedrowiec po >= 40 dniach; sprawdzane raz w tygodniu, wiec realnie 40-46 dni.
- **Logi gry [P].** 4 sesje rgl z 14.10: ROTwanderer1 / ROTwanderer8 dokladnie raz (linie zgodne z rozdz. 3). Ponowne utworzenia innych unikatow 26 / 4 / 7 / 2, wszystkie 0,50-0,68 s po "Before Daily Tick" (czyli dzienny spawner ROT). Dwie dalsze sesje (`rgl_log_37324`, `rgl_log_40716`, wczytane zapisy) - 0 Tyrionow i Jaimich.
- **Kronika unikatow [P].** 41 plikow `unikaty.log` (D:\Backup-Bannerlord + Modules\Armoury\Logs, z 12.10 wlacznie - stad 41, a nie 29): `tyrion_clothes` 44 x jeden posiadacz "nosi Tyrion Lannister" i 3 x "nigdzie", `tyrion_boots` tak samo; nigdy dwoch posiadaczy ("; nosi"). Przejscia "nigdzie -> nosi" potwierdzone: `2026-10-13_13-28-08/unikaty.log:60-61`, `2026-10-13_21-14-13/unikaty.log:250-251` i `:335-339`. Ograniczenie: kronika patrzy tylko na `BattleEquipment` zywych (`Armoury/src/UniqueSpoils.cs:231-240`) - Tyrion przebrany przez gracza bylby niewidoczny; przedmiotow Jaimego kronika nie ma (0 wpisow `jaime_*`).
- **Wszystkie inne logi [P].** D:\Backup-Bannerlord\logs (wszystkie pliki) i 282 logi CrashScribe w Documents: "Tyrion Lannister" / "Jaime Lannister" wystepuja WYLACZNIE w `unikaty.log`. Nigdzie jako lord ani rod Lannister.
- **Ithauser [P].** `battle.txt`: 16 wpisow z Jaimem, wszystkie "(clan:Karda, kingdom:The North)", 0 innych rodow; Tyrion 0 bitew. `aiActions.log:1668-1669` i `:2393-2394` dolaczenia do druzyny; "join_clan" 0 wykonan (tylko tekst podpowiedzi w `npc_initiative.txt`). Jaime `CharacterObject_184682` wobec Tyriona `CharacterObject_6573`: Jaime jest tworzony w paczce startowej PRZED Tyrionem (rgl `:7985` przed `:7992`), wiec oryginalny Jaime mialby numer nizszy niz 6573 -> to drugi Jaime [S], wniosek mocny.

### 7b. Poprawki do rozdz. 0-4

1. **Rod Lannister [D].** `clan_vlandia_1` (spclans.xml:73; ROT_heroes.xml:303, 307-310) = Tywin, Kevan, Lancel, Willem, Stafford. Cersei (`lord_4_2`) ma w `ROT_heroes.xml:304` `faction="Faction.clan_nord_1"` = rod "Baratheon, Joffrey". Rozdz. 0 pkt 1 i tabela 1b liczyly ja do rodu Lannister - poprawione.
2. **"61 innych postaci z ksiazek"** - nie wszystkie sa z ksiazek (rozdz. 1b sam wymienia postacie ROT: Ser Kemal, Guts, Bedir Khan...). Poprawione w rozdz. 0.
3. **Furtka "dziecko" - mechanizm inny, wniosek mocniejszy [K].**
   - Zawod dziecka = zawod rodzica tej samej plci (`tow/HeroCreator.cs:267`, `.SetOccupation(... father.Occupation)`). Syn towarzysza (Wanderer) jest wiec wedrowcem NA STALE, nie tylko "w chwili nadania imienia".
   - Imie: w chwili nadania (`:311-314`) zawod = surowe pole `_occupation` skopiowane z postaci ojca (`CharacterObject.cs:431`, `:486-489`, `Hero.cs:2253`) = Wanderer ze wzoru, NAWET gdy ojciec jest juz lordem. Dopiero potem (`:320-322`) zawod zmienia sie na Lord. Syn towarzysza, ktorego gracz zrobil panem rodu, jest wiec **lordem o imieniu "Tyrion Lannister"** w rodzie ojca. To jedyna droga w kodzie do "Tyriona Lannistera, ktory jest lordem i prowadzi armie".
   - BK juz to czesciowo lata: `bkdec/BannerKings.Patches/FixesPatches.cs:48-76` (prefix na `NameGenerator.GenerateHeroFullName`): gdy rodzic tej plci jest "knighted" (`TitleManager.IsHeroKnighted` = IsLord i posiada tytul, `bkdec/BannerKings.Managers/TitleManager.cs:254-261`), dziecko-wedrowiec dostaje samo imie. Dziura zostaje dla ojca-towarzysza (Wanderer) i dla ojca-lorda bez tytulu BK.
   - Dziecko ma ten sam `Template` co ojciec (`CharacterObject.cs:419`), wiec dla ROT "liczy sie" jako Tyrion i blokuje odrodzenie po smierci ojca.
   - W logach brak sladu.
4. **Brakujaca droga "Tyrion prowadzi wlasna armie" (to on, nie kopia) [K].**
   - Spoils of War `dec/SpoilsOfWar/RealisticLoot.Behaviors/SubClanBehavior.cs:151` (menu "Create from companion"), `:326-397` (wybor towarzysza), `:449-457` (domyslna nazwa rodu `"{leader.Name}'s Company"` = "Tyrion Lannister's Company"), `:472-474` (`RemoveCompanionAction.ApplyByByTurningToLord`). Towarzysz zostaje panem wlasnego rodu najemnikow z wlasnym oddzialem. W `Modules/Spoils of War/Logs` debug byl wlaczony tylko 08-25.08 i nie ma tam "Created autonomous clan" - to nie dowod, ze pozniej tego nie bylo [P].
   - Gra: `twcs/.../CompanionRolesCampaignBehavior.cs:170-173` (tylko gdy gracz jest krolem), `:258-266`: towarzysz dostaje lenno i rod o nazwie wpisanej przez gracza; dodatkowi czlonkowie rodu sa ze `culture.LordTemplates` (`:359-363`), nie z Tyriona.
   - AI nie najmuje istniejacych wedrowcow: w grze `AddCompanionAction.Apply` tylko dla `Clan.PlayerClan` (`LordConversationsCampaignBehavior.cs:2031`, `LordsNeedsTutorIssueBehavior.cs:437`); BK tworzy nowych z `culture.NotableTemplates` (`BKClanBehavior.cs:1584-1645`), a unikatow tam nie ma.
5. **BK dworzanie** - kolejnosc potwierdzona (`bkdec/BannerKings.Behaviours/BKCourtierBehavior.cs:168-171`, nie :158-170). Uwaga: krok 3 ("dowolny Lord tej kultury") bierze z `CharacterObject.All` takze postacie istniejacych bohaterow. Gdyby Tyrion byl juz lordem (pkt 3/4), dworzanin z jego postaci dostalby imie "Tyrion Lannister" ta sama droga co dziecko. Teoria, malo prawdopodobne (dziesiatki lordow vlandia do losowania) [S].
6. **"Damion / Lucion Lannister"** - potwierdzone, ze nieuzywane. Dodatkowo gra bierze wzory wedrowcow tylko z `IsTemplate && Wanderer` (`CompanionsCampaignBehavior.cs:344-353`), a te dwa wpisy nie maja `is_template` (`notablesROT.xml:71354`, `:71380`) [D][K].

### 7c. Poprawki do propozycji naprawy (rozdz. 5) [K]

- **a) `UniqueCompanionsNoRespawn` - sam prefix na `UnregisterDeadHero` jest niebezpieczny.** Wolny, niepoznany unikat usuniety przez TryKillCompanion (detail `Lost`) zostalby na liscie zmarlych na zawsze, wiec ROT nigdy by go nie odtworzyl i po 1-2 latach czesci unikatow nie byloby w karczmach. Bezpieczniej: blokowac wymazanie tylko unikatom, ktore naprawde zgineli (wlasny zbior id z `HeroKilledEvent` z detail != `Lost`, zapisany w SyncData), a "przeprowadzki" (`Lost`) zostawic jak dzis. Albo wdrazac a) wylacznie razem z wylaczeniem unikatow z TryKillCompanion. Filtr po `Template` lapie tez dzieci unikatow - dopuszczalne (lista zmarlych rosnie minimalnie).
- **b) `UniqueCompanionsDuplicateGuard`** - liczyc tylko bohaterow BEZ ojca i matki (`Father == null && Mother == null`). Dzieci maja ten sam `Template`, wiec bez tego beda falszywe alarmy. Odrodzony unikat (CreateSpecialHero) rodzicow nie ma, wiec warunek go nie gubi.
- **Imie dziecka** - jako postfix na `HeroCreator.DeliverOffSpring` (jesli `Template` to ROTwanderer* i `Name` == nazwa szablonu: nowe imie z `NameGenerator` dla kultury dziecka). Nie latac drugi raz tego, co robi BK (`FixesPatches.cs:48-76`): sprawdzic, czy imie juz rozne od szablonu.
- **c) `UniqueCompanionsLog`** - bez zmian; dopisac do linii `Father/Mother` i `Occupation`, zeby dziecko-imiennik bylo widac od razu.
- Klucze MCM i tor M bez zmian; autotest musi dodatkowo pokazac, ze liczba zywych unikatow w karczmach nie spada przez 2 lata.

### 7d. Werdykt po weryfikacji

- **CECHA ROT** [D][K][P] - potwierdzona: Tyrion i Jaime to zamierzeni wedrowcy do najecia, a nie lordowie Lannisterow. Kopii lorda nie ma; dwoch naraz nie ma w zadnym logu.
- **BLAD: odradzanie unikatow** [K][P] - potwierdzony (kod i 3 przejscia w kronice); Jaime w Ithauser to najpewniej druga "wersja" [S].
- **Furtka "syn = drugi Tyrion"** [K] - potwierdzona i mocniejsza niz w rozdz. 2: syn towarzysza zrobionego panem rodu (bez tytulu BK) bylby LORDEM "Tyrion Lannister". Poza zwyklym towarzyszem i panem rodu zrobionym przez gracza to jedyna droga w kodzie do tego, co opisal Jeff. Sladu w logach brak.
- **Czego dalej nie mam:** logu z gry Jeffa, w ktorym Tyrion albo Jaime bylby lordem. Do rozstrzygniecia: strona encyklopedii (pola "Clan" i "Father") albo linia z pkt 5c.

Pliki weryfikacji (scratchpad `c36b73ca-.../scratchpad/tow/`): `wer.py`, `wer_rgl.py`, `twcs/` (pelna dekompilacja TaleWorlds.CampaignSystem 1.4.8).
