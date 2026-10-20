## p5-jednostki (20.10) - lepsza jazda Doliny i Reach (R3-08e) i konni lucznicy Freyow (R3-08f) (CrashScribe, klucze w Armoury)

Galaz `w-toku/p5-jednostki` na 5189c0fe (kandydat WGRANIA 92). Jeff 20.10 do listy POMYSLY-35: "28 - nie, 29 - nie. Pozostale tak" -
czyli nr 24 (R3-08e) i nr 25 (R3-08f) TAK.

### Dla Jeffa (prostymi slowami)
Rycerze Doliny i Reach sa teraz lepsi w siodle: Vale Rider, Vale Lancer i Reach Rider maja po 20 punktow wiecej jazdy konnej i swojej
glownej broni. Obaj jezdzcy Doliny nosza ciezsza zbroje z rzeczy Doliny i nadal nie ponad swoj poziom: dostaja rekawice i naramienniki,
bo Dolina nie ma na poziomach 3-4 ciezszego napiersnika - ten zostaje. Rod Frey dostal konnych lucznikow: Frey Rider (poziom 4) jezdzi
z krotkim lukiem do strzelania z siodla, dwoma kolczanami i mieczem, bez tarczy, i stoi w szyku konnych lucznikow; Frey Horseman
(poziom 5) zostaje kopijnikiem. Lordowie Freyow beda kupowac dla nich luki i strzaly zamiast kopii, a ROT dalej werbuje i awansuje ich
jezdzcow tak jak dzis. W toczacej sie kampanii luk pojawi sie w reku Frey Ridera dopiero, gdy lord go kupi. W MCM sa
dwa nowe wylaczniki (oba wlaczone): "Army175 Vale Reach Knights" i "Army175 Frey Horse Archers" (grupa "Armies of the realms (175)"); zmiana wchodzi po wczytaniu gry.

### Problem / zrodlo pomyslu
- R3-08e (POMYSLY-35 nr 24; docs/audyt-2026-10-09/14-ARMIE-KROLESTW.md 4.5, tabele Vale :255-258, Reach :563): wedlug ksiazek rycerstwo
  Doliny i Reach jest najlepsze w Westeros, a w grze ich jazda t3-t4 jest przecietna (Vale Rider Jaz 70 / 1H 70, Vale Lancer i Reach Rider
  Jaz 100 / Dr 100; sila jazdy Doliny 91, Reach 94 wobec przecietnej ok. 98-100). Audyt: +20 Jazdy i +20 broni ponad dzisiejsze wartosci
  w grze, w Dolinie korpus o stopien ciezszy.
- R3-08f (nr 25; audyt 4.12, tabela Frey :354-366; PROJEKT-175-ARMIE :392-396 "Frey - konni lucznicy: ODLOZONE"): w kulturze `river` nie ma
  zadnego konnego lucznika; `frey_rider` t4 i `frey_horseman` t5 to kopijnicy (Luk 5 w XML ROT). Zeby byl konny lucznik, trzeba zmienic
  role istniejacej jednostki: szyk HorseArcher, luk do jazdy (nie dlugi) i Luk najwyzsza umiejetnoscia (Armoury SkillsDecide wybiera bron
  glowna po najwyzszej umiejetnosci - inaczej kopia).

### Zmiana
Nowy plik `CrashScribe/src/Army175Jazda.cs` (partial `Army175`, ta sama maszyna co NorthHardy / DothrakiRiders / Qohor / Zlota Kompania).
Danych ROT (XML) nie ruszamy - wszystko w pamieci, od nowa przy kazdym wczytaniu (nic nie idzie do zapisu).
1. **Frey Rider - zestaw i szyk (R3-08f)**, przy wczytaniu (AfterRegisterSubModuleObjects, zaraz po 175.2, lore i Zlotej Kompanii - Armoury
   TroopFit/ColdStart widza juz luk), zapasowo w sesji (TierGearCheck / DailyCatchUp, gdy 175.2 weszlo dopiero tam):
   - w kazdym zestawie bojowym sloty 0-3: krotki luk uzywalny z siodla (`Mends.MountOk`, uzycie bez "long") i 2x te same strzaly - oba
     z kaskady 175.2 w tierze jednostki (`BestOfType`, jak Zlota Kompania), slot 3 - bron reczna z wlasnego zestawu uzywalna z siodla
     (najpierw klasy najwyzszej umiejetnosci recznej); u frey_rider to zawsze miecz `narrow_sword_t3` - kopia nie przechodzi `MountOk`
     (uzycie `polearm_bracing` ma requires_no_mount), wiec wzorzec i zakupy AI (AiGear.Deficit z 1. zestawu) to miecz, a w bitwie z graczem
     Armoury SkillsDecide prosi o zapas wedlug drugiej umiejetnosci - Drzewce 110 > 1H 100, czyli drzewce uzywalne z siodla, jesli
     zbrojownia je ma (opis poprawiony po recenzji, zachowanie bez zmian); tarcza i reszta broni zdjete (lista w logu). Nowy roster (`Rewrite`/`InstallRoster`) -
     stary roster i inne postacie nietkniete, jak przy 175.2;
   - szyk jak z XML `default_group="HorseArcher"`: `DefaultFormationClass`, `DefaultFormationGroup`, pole `_isRanged` (sprawdzone
     w dekompilacji 1.4.8: BasicCharacterObject.Deserialize ustawia dokladnie te trzy; CharacterObject nadpisuje IsRanged/GetFormationClass
     tylko dla bohaterow);
   - Luk po SkillSinew (OnSessionLaunched, po DothrakiRiders; zapasowo DailyTick): max(obecny, wymog wlasnego luku i strzal, najwyzsza inna
     bron + 10), z sufitem "bez przeskoku tieru" (TierCap jak NorthHardy), zawsze co najmniej najwyzsza inna bron + 1. Z XML: Drzewce 110 ->
     Luk ok. 120 (sufit t4 przy 35/tier: 139).
   - awans `frey_cutthroat` -> `frey_rider` -> `frey_horseman` bez zmian (Frey Horseman zostaje kopijnikiem).
2. **Dolina i Reach - umiejetnosci (R3-08e)**, po SkillSinew: `vale_rider` t3, `vale_lancer` t4, `reach_rider` t4 - Jazda +20 i bron glowna
   (MainMelee jak SkillsDecide) +20 ponad max(obecna, wymog wlasnego sprzetu); bron z sufitem bez przeskoku tieru (t3: 104, t4: 139), Jazda
   bez sufitu (jak DothrakiRiders). Znacznik raz na obiekcie umiejetnosci (`ConditionalWeakTable`, jak NorthHardy). Z XML: Vale Rider
   Jaz 70->90, 1H 70->90; Vale Lancer Jaz 100->120, Dr 100->120; Reach Rider Jaz 100->120, Dr 100->120.
3. **Dolina - zbroja o stopien ciezsza w tierze (R3-08e; po recenzji)**, w sesji po ArmorSanity (jak QohorHeavier, przed prawami tieru
   i SkillSinew - Atletyke pod nowa wage podnosi SkillSinew): `vale_rider` i `vale_lancer` - w kazdym slocie pancerza 5-9 (glowa, korpus,
   nogi, rece, ramiona) najciezsza czesc TYLKO z rzeczy Doliny (kultura `vale` albo noszone przez zolnierzy Doliny - `SameTroopCult`),
   w tierze jednostki (decyzja 8b), ciezsza niz dzis (wiecej ochrony po rozsadku i nie lzejsza w kg); pusty slot (rece, ramiona) dostaje
   czesc Doliny; najpierw rzeczy z targu (lord je kupi), bez cywilnych, bandyckich i zakazanych plcia. Tylko razem z 175.2. Wedlug zrzutu
   przedmiotow z 20.10 (sesja 05-34-48), liczone skryptem tym samym porzadkiem: Vale Rider t3 - rece `lord_gloves` t2 (27), ramiona
   `grafton_shoulders` t1 (16); Vale Lancer t4 - rece `vale_bracers_gloves` t4 (54), ramiona `mail_shoulders` t4 (30); korpus obu bez
   zmian (ciezszego korpusu Doliny w tierze nie ma: t3 najwyzej 104, t4 tylko `grafton_armor` 123 - noszony juz przez Vale Lancera;
   nastepny `royce_armor` t5) - linia logu podaje najblizszy ciezszy ponad tier. Glowa i nogi tylko, gdy w tierze jest ciezsza czesc
   Doliny (dokladny wynik w linii logu).
4. Wpiecia (minimalne; po recenzji dodatkowo `InstallJazda(harmony)` na poczatku `Army175.Install` w Army175Sklad.cs): `Army175Gear.cs` - `ResetJazda()` w Reset, `JazdaGear(false)` w OnObjectsRegistered, `JazdaGear(true)` po
   QohorHeavier w trzech miejscach (TierGearCheck x2, DailyCatchUp); `Mends.cs` - `Army175.JazdaSkills()` po DothrakiRiders w OnSessionLaunched
   i w DailyTick. Klucze MCM w `Armoury/src/Settings.cs` (sekcja "Armies of the realms (175)", po Army175MinorLore), McmSettings.cs z `python tools/gen_mcm.py`:
   `Army175ValeReachKnights` (dom. TAK), `Army175FreyHorseArchers` (dom. TAK); CrashScribe czyta je `Mends.ArmouryFloat` (bez Armoury -
   domyslnie wlaczone). Armoury.json Jeffa tych kluczy nie ma, wiec biora domyslne.
5. Poprawka po przejeciu galezi (poprzedni agent przerwal sie bez commita): umiejetnosci Freyow maja wlasny znacznik `_freySkillsDone` -
   gdy zestaw Freyow wejdzie dopiero w przebiegu zapasowym (DailyCatchUp, doby 1-3), Luk podnosi sie tej samej doby (dotad jeden znacznik
   na Doline i Freyow - Luk zostalby na wymogu luku, a bron glowna w bitwie bylaby kopia); opis broni zapasowej poprawiony (MountOk).
6. **Cel skladu ROT dla Freyow (R3-08f; po recenzji)** - postfiks `ROTTroopRecruiter.GetTemplateComposition` (`AccessTools.DeclaredMethod`
   na klasie deklarujacej, typ ROT po nazwie): stos `frey_rider` (t3+, wagi MaxValue) liczony w klasie najwyzszego awansu (`frey_horseman`
   = Cavalry), tak jak ROT liczy sklad partii (`GetPartyComposition`, `ShouldRecruit`, szansa awansu `ROTTroopUpgradeModel`, zamiany
   `ExchangeClanTroops`). Cel `clan_frey_party_template` zostaje jak przed zmiana: Cav 6/29 = 21%, KL 0 (bez poprawki: Cav 7%, KL 14%
   nigdy do wypelnienia). Dziala tylko, gdy frey_rider zostal przerobiony (`_freyConverted`). Wynik ROT jest pamietany per bohater/osada
   (Settings), wiec to nie goraca sciezka; liczniki Interlocked; linia przy pierwszej poprawce (cel przed/po) i linia dobowa z licznikami
   (szablonow z frey_rider, poprawionych, potkniec), gdy cos przybylo.

### Ryzyko / co sprawdzic (kontrola calosci)
- **Regresje**: zmienione sa tylko 4 jednostki (wzorce, umiejetnosci, szyk Freya) i cel skladu ROT jednego szablonu
  (`clan_frey_party_template`, przywrocony do stanu sprzed zmiany). Pozostale funkcje 175 (TierGear, lore, Qohor, NorthHardy,
  DothrakiRiders) bez zmian - nowe kroki biegna PO nich i tylko na tych id. `CountOver` (sloty ponad tier) zostaje 0: luk, strzaly
  i zbroja w tierze jednostki. Linia kontroli 175 policzy o 1-3 rostery wiecej (nowe rostery Freya i Doliny).
- **Kolizje**: SkillSinew i TroopFit tylko podnosza umiejetnosci, wiec nie zjedza dodatkow; drugi przebieg TierGear po rozsadku nie rusza
  luku (tier <= jednostki). Armoury SkillsDecide w bitwie z graczem wybierze Luk jako bron glowna (najwyzsza umiejetnosc) i jako zapas
  Drzewce (110 > 1H 100) - DTE da jezdzcowi tylko bron uzywalna z siodla (`PartyEquipmentDistributor.IsWeaponSuitable`:
  `!IsMounted | IsSuitableForMount`, sprawdzone w dekompilacji DTE 1.4.7), wiec dlugiego luku z siodla nie dostanie.
- **ROT (recenzja 20.10, poprawione)**: ROT czyta `DefaultFormationClass` w celu skladu (szablon) i w skladzie partii (klasa najwyzszego
  awansu) - bez postfiksu cel jazdy Freyow spadlby z 21% do 7%, a cel KL 14% nigdy by sie nie wypelnil (zadna jednostka Freyow nie ma KL
  na szczycie drzewa). Postfiks przywraca cel; sklad partii ROT liczy frey_rider jako Cavalry tak jak dzis, wiec werbunek, awanse i zamiany
  ROT dzialaja jak przed zmiana. Inne odczyty szyku: Armoury MusterBook/MusterOut (widok/log), ROT ROTEnlistmentMenuBehavior (sluzba
  gracza u lorda), linia `Shape` skladu 175 (tylko log, liczy po IsMounted/IsRanged - pokaze Freyom KL 14%). Armoury CavalryTemplate
  liczy po `IsMounted` - bez zmian. Gdy postfiks sie nie wepnie (inna wersja ROT): "NIE wpiety - OSTRZEZENIE" w linii zestawu Freyow.
- **Dolina - zakupy i waga**: rece i ramiona to nowe sloty we wzorcu, wiec lordowie Doliny kupuja dla Vale Riderow i Vale Lancerow
  rekawice i naramienniki za swoje zloto z polek targu (koszyk typ+tier, AiGear.Deficit) - zamknieta ekonomia, wiecej popytu na rekawice
  t2/t4 i naramienniki t1/t4. Dodatkowe ok. 4-6 kg - Atletyke pod nowa wage podnosi SkillSinew (w sesji). Gdy 175.2 weszlo dopiero
  w przebiegu zapasowym (DailyCatchUp, doby 1-3) po SkillSinew, Atletyka pod nowa wage nie zostanie podniesiona do konca sesji - tak samo
  jak dzis u Qohoru.
- **Freyowie w toczacej sie kampanii (save040)**: zbrojownie Freyow maja dzis kopie i tarcze, lukow nie. Dopoki lordowie nie dokupia lukow
  (koszyk Bow t3, wzorzec `nordic_shortbow` t3), DTE nie znajdzie Frey Riderowi luku ani strzal - przy wlaczonym EmergencyLoadout DTE
  wstawi w sloty 0-1 bron rekruta kultury (BasicTroop), inaczej sloty zostana puste i Frey Rider stoi w szyku konnych lucznikow bez luku.
- **Zakupy AI (zamknieta ekonomia)**: nic nie powstaje z niczego - zmieniamy tylko wzorce. Lordowie Freyow kupuja za swoje zloto luki
  i strzaly z polek (AiGear.Deficit liczy potrzebe z 1. zestawu bojowego, koszyk typ+tier) zamiast kopii i tarcz. Koszyk "Bow t4" nie
  rozroznia luku do jazdy od dlugiego - jesli lord kupi dlugi luk, DTE go Frey Riderowi nie da (w bitwie z graczem bez luku); tak samo
  jest dzis u Dothrakow i Mormont Mounted Huntress. Do obejrzenia w tescie, nie naprawiane tutaj.
- **Autobitwa**: moc liczy sie z tieru, wiec +20 umiejetnosci Doliny/Reach jej nie zmienia (wojen AI nie przechyla). Frey Rider liczy sie
  jak konny lucznik (IsMounted + IsRanged: +30% w polu, -30% w lesie, -20% przy oblezeniu) - mala zmiana dla 4 ludzi w szablonie Freyow
  i ich awansow. Zold bez zmian (tier + kon).
- **Cudzy kod**: pola `_isRanged`, setter `DefaultFormationClass` (protected), `DefaultFormationGroup`, `_equipmentRoster` sprawdzone
  w dekompilacji TaleWorlds.Core 1.4.8; brak pol refleksji = linia OSTRZEZENIE i jednostka bez zmian (bez wywrotki). Wyjatek w kroku =
  `Scribe.Report` i ten krok konczy sie na te sesje; Dolina liczy potkniecia per jednostka.
- **Watki**: wszystko na watku glownym (AfterRegister, OnSessionLaunched, DailyTick), raz na sesje; zadnej goracej sciezki ani latki Harmony.
- **Stan miedzy kampaniami**: `ResetJazda()` z Army175.Reset (OnGameStart); znaczniki umiejetnosci na obiektach, ktore sa nowe w kazdej grze.
- Przy wylaczonym `Army175TierGear` (albo zapisie bez K1) Freyowie i korpus Doliny sa pomijani z linia "pominiete"; umiejetnosci Doliny
  i Reach dzialaja dalej (bron i tak z sufitem bez przeskoku tieru).

### Status
**DO SPRAWDZENIA** - zbudowane (CrashScribe i Armoury: kod 0, 0 Error(s)), nie wgrane, bez autotestu (zakaz w tym zadaniu).
Po recenzji 20.10 (drugi commit): zbroja Doliny tylko z rzeczy Doliny, postfiks celu skladu ROT dla Freyow, poprawione opisy.

### Recenzja 20.10 - co bylo i co zrobione
- (wazne, PRAWDA) Korpus Doliny z obcej kultury i lzejszy w kg - sprawdzone na items-dump.csv (sesja 05-34-48) i XML ROT: korpusy `vale`
  t3 najwyzej 104, t4 tylko `grafton_armor` 123, nastepny `royce_armor` t5. Poprawione: tylko rzeczy Doliny, "ciezsza" = wiecej ochrony
  i nie mniej kg, cala zbroja (glowa-ramiona) w tierze; korpus zostaje z linia o najblizszym ponad tier (punkt 3 Zmiany, Decyzje).
- (wazne, PRAWDA) Kolizja z ROT `ROTTroopRecruiter` - sprawdzone w dekompilacji ROT.dll: `GetTemplateComposition` liczy z
  `DefaultFormationClass` stosow t3+, `GetPartyComposition`/`ShouldRecruit` z klasy najwyzszego awansu (`UpgradeTargets(maxTierOnly)`);
  `clan_frey_party_template` t3+ = 29 ludzi, frey_rider 4, frey_horseman 2 - Cav 21% -> 7%, KL 14%. frey_rider jest tylko w tym jednym
  szablonie. Poprawione postfiksem (punkt 6 Zmiany).
- (drobne, PRAWDA) Opis broni zapasowej ("kopia przy Drzewcach 110, gdy MountOk") - ta galaz nigdy nie zachodzi. Poprawiony komentarz
  w kodzie i opis tutaj (miecz we wzorcu i w zakupach, drzewce w bitwie z SkillsDecide); zachowanie bez zmian.
- (drobne, PRAWDA) Zbrojownie Freyow w save040 bez lukow - dopisane w Ryzyku i w Tescie w grze.

### Decyzje (projektuje Claude - Jeff: "ekonomie projektuje Claude")
- Konnym lucznikiem zostaje `frey_rider` t4, nie `frey_horseman` t5 - t4 jest w szablonie rodu Frey (4 ludzi) i w awansie z Cutthroata,
  a t5 zostaje kopijnikiem, wiec Freyowie maja obie role (jak w ksiazkach: rycerze i konni lucznicy obok siebie).
- "Korpus o stopien ciezszy" = najciezszy korpus w granicy tieru jednostki (jak Qohor), ale tylko ciezszy niz dzis i najwyzej do tieru
  jednostki - zasada 8b "nic ponad tier" wygrywa z "stopien ciezej".
- +20 jako stala z audytu (bez suwaka) - wylacznik bool wystarcza; Jazda bez sufitu (kon wedlug Jazdy, konie poza decyzja 8b).
- Bron zapasowa Freya z jego wlasnego zestawu (bez dokupowania nowej sztuki do wzorca), tarcza zdjeta (konny lucznik ma rece na luku).
  Po recenzji zostaje miecz we wzorcu (a drzewce w bitwie z SkillsDecide) - obie bronie sa uzywalne z siodla, poprawiony tylko opis.
- "Zbroja o stopien ciezsza" (po recenzji): decyzja 8b Jeffa ("najwyzej swojego tieru") jest doslowna, a Dolina nie ma ciezszego korpusu
  na t3-t4, wiec zamiast obcych barw (kolczuga Polnocy, zbroja Dorne - lzejsza w kg) albo wyjatku od 8b (`royce_armor` t5 dla t4)
  ciezsza jest RESZTA zbroi: rzeczy Doliny w tierze na glowie, nogach, rekach i ramionach. "Ciezsza" = wiecej ochrony i nie mniej kg.
  Wyjatek od 8b (Vale Lancer w `royce_armor` t5, Vale Rider w `grafton_armor` t4) to jedna zmiana warunku tieru, gdyby Jeff wolal napiersnik.
- Cel skladu ROT przywrocony postfiksem zamiast zostawienia ryzyka: Frey Rider ma byc konnym lucznikiem, a nie powodem, by ROT
  przestal werbowac jazde Freyow (rozwiazanie ma objac 100%).

### Test w grze (linie w `Documents\Mount and Blade II Bannerlord\CrashScribe\session-*.log`)
- `Mends: konni lucznicy Freyow (R3-08f) - frey_rider t4` ... `szyk HorseArcher (IsMounted True, IsRanged True), zestawow bojowych
  zmienionych 1 z 1; luk <id> t<=4 (z siodla), strzaly <id> x2, bron zapasowa <id>` - przy wczytaniu, przed liniami sesji.
- `Mends: konni lucznicy Freyow (R3-08f) - frey_rider t4 Luk <stary>-><nowy>` ... `luk bronia glowna TAK.` - po SkillSinew (nowy Luk >
  najwyzsza inna bron).
- `Mends: jazda Doliny i Reach (R3-08e) - zbroja o stopien ciezsza w granicy tieru (tylko rzeczy Doliny` ... `czesci w 2 z 2 zestawow`
  (oczekiwane ok. 4 czesci: rece i ramiona obu), `korpus bez zmian - brak ciezszego korpusu Doliny w tierze`, `potkniecia 0.` - po linii Qohor.
- `Mends: konni lucznicy Freyow (R3-08f) - postfiks celu skladu ROT (ROTTroopRecruiter.GetTemplateComposition) wpiety` - przy starcie gry.
- W linii zestawu Freyow: `cel skladu ROT: stos frey_rider liczony jako Cavalry (najwyzszy awans frey_horseman), postfiks wpiety.`
- `Mends: konni lucznicy Freyow (R3-08f) - cel skladu ROT clan_frey_party_template: stos frey_rider (4 z 29 ludzi t3+) przeniesiony
  z HorseArcher do Cavalry - Cavalry 7% -> 21%, HorseArcher 14% -> 0%.` - przy pierwszym pytaniu ROT o cel Freyow; potem
  `... cel skladu ROT od startu sesji: szablonow z frey_rider N, poprawionych N, potkniecia 0.` (raz na dobe, gdy cos przybylo).
- Udzial jazdy Freyow nie spada: K3 "Rycerze i jazda (219)" / udzial konnych w partiach i zalogach Dorzecza jak w tescie 160 dob na WGRANIU 91.
- `Mends: jazda Doliny i Reach (R3-08e) - 3 z 3 jednostek +20 Jazdy i +20 broni glownej` ... `potkniecia 0.` - po DothrakiRiders.
- `Mends: ustawienia Armoury: PODPIETY (Armoury.Settings.Army175FreyHorseArchers)` i `... Army175ValeReachKnights` - klucze podpiete.
- W bitwie z druzyna Freyow: Frey Riderzy w szyku konnych lucznikow, strzelaja z siodla; w okienku jednostki Frey Rider - luk i Luk ok. 120.
  Pierwszy sprawdzian dopiero po kilku dobach zakupow lordow Freyow (albo w nowej kampanii) - w save040 zbrojownie Freyow nie maja jeszcze
  lukow, wiec bitwa zaraz po wczytaniu moze pokazac Frey Riderow bez luku (bron rekruta z EmergencyLoadout albo puste sloty), choc kod
  jest dobry. Armoury nie pisze zakupow per jednostka - sprawdzic w bitwie (albo w zbrojowni lorda Freyow), czy pojawily sie luki konne t2-t3.
- Brak linii "OSTRZEZENIE" z R3-08e/f i brak `Army175.FreyGear` / `Army175.ValeArmor` / `Army175.KnightsSkills` / `Army175.FreySkills`
  w bledach CrashScribe.
