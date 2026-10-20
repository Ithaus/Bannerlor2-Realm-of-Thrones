## p5-jednostki (20.10) - lepsza jazda Doliny i Reach (R3-08e) i konni lucznicy Freyow (R3-08f) (CrashScribe, klucze w Armoury)

Galaz `w-toku/p5-jednostki` na 5189c0fe (kandydat WGRANIA 92). Jeff 20.10 do listy POMYSLY-35: "28 - nie, 29 - nie. Pozostale tak" -
czyli nr 24 (R3-08e) i nr 25 (R3-08f) TAK.

### Dla Jeffa (prostymi slowami)
Rycerze Doliny i Reach sa teraz lepsi w siodle: Vale Rider, Vale Lancer i Reach Rider maja po 20 punktow wiecej jazdy konnej i swojej
glownej broni, a obaj jezdzcy Doliny nosza ciezsza zbroje (nadal nie ponad swoj poziom). Rod Frey dostal konnych lucznikow: Frey Rider
(poziom 4) jezdzi z krotkim lukiem do strzelania z siodla, dwoma kolczanami i bronia do walki wrecz, bez tarczy, i stoi w szyku konnych
lucznikow; Frey Horseman (poziom 5) zostaje kopijnikiem. Lordowie Freyow beda kupowac dla nich luki i strzaly zamiast kopii. W MCM sa
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
     (najpierw klasy najwyzszej umiejetnosci recznej); tarcza i reszta broni zdjete (lista w logu). Nowy roster (`Rewrite`/`InstallRoster`) -
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
3. **Dolina - korpus o stopien ciezszy w tierze (R3-08e)**, w sesji po ArmorSanity (jak QohorHeavier, przed prawami tieru i SkillSinew -
   Atletyke pod nowa wage podnosi SkillSinew): `vale_rider` i `vale_lancer` - najciezszy korpus (suma ochrony po rozsadku) sposrod ciezszych
   niz dzisiejszy, tier od dzisiejszego do tieru jednostki (decyzja 8b - nic ponad tier), najpierw kultura jednostki, potem Westeros / bez
   kultury (bez zbroi z Essos), bez cywilnych, bandyckich i zakazanych plcia. Tylko razem z 175.2.
4. Wpiecia (minimalne): `Army175Gear.cs` - `ResetJazda()` w Reset, `JazdaGear(false)` w OnObjectsRegistered, `JazdaGear(true)` po
   QohorHeavier w trzech miejscach (TierGearCheck x2, DailyCatchUp); `Mends.cs` - `Army175.JazdaSkills()` po DothrakiRiders w OnSessionLaunched
   i w DailyTick. Klucze MCM w `Armoury/src/Settings.cs` (sekcja "Armies of the realms (175)", po Army175MinorLore), McmSettings.cs z `python tools/gen_mcm.py`:
   `Army175ValeReachKnights` (dom. TAK), `Army175FreyHorseArchers` (dom. TAK); CrashScribe czyta je `Mends.ArmouryFloat` (bez Armoury -
   domyslnie wlaczone). Armoury.json Jeffa tych kluczy nie ma, wiec biora domyslne.
5. Poprawka po przejeciu galezi (poprzedni agent przerwal sie bez commita): umiejetnosci Freyow maja wlasny znacznik `_freySkillsDone` -
   gdy zestaw Freyow wejdzie dopiero w przebiegu zapasowym (DailyCatchUp, doby 1-3), Luk podnosi sie tej samej doby (dotad jeden znacznik
   na Doline i Freyow - Luk zostalby na wymogu luku, a bron glowna w bitwie bylaby kopia); opis broni zapasowej poprawiony (MountOk).

### Ryzyko / co sprawdzic (kontrola calosci)
- **Regresje**: zmienione sa tylko 4 jednostki (wzorce, umiejetnosci, szyk Freya). Pozostale funkcje 175 (TierGear, lore, Qohor, NorthHardy,
  DothrakiRiders) bez zmian - nowe kroki biegna PO nich i tylko na tych id. `CountOver` (sloty ponad tier) zostaje 0: luk, strzaly i korpus
  w tierze jednostki. Linia kontroli 175 policzy o 1-3 rostery wiecej (nowe rostery Freya i Doliny).
- **Kolizje**: SkillSinew i TroopFit tylko podnosza umiejetnosci, wiec nie zjedza dodatkow; drugi przebieg TierGear po rozsadku nie rusza
  luku (tier <= jednostki). Armoury SkillsDecide w bitwie z graczem wybierze Luk jako bron glowna (najwyzsza umiejetnosc) i jako zapas
  Drzewce (110 > 1H 100) - DTE da jezdzcowi tylko bron uzywalna z siodla (`PartyEquipmentDistributor.IsWeaponSuitable`:
  `!IsMounted | IsSuitableForMount`, sprawdzone w dekompilacji DTE 1.4.7), wiec dlugiego luku z siodla nie dostanie.
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

### Decyzje (projektuje Claude - Jeff: "ekonomie projektuje Claude")
- Konnym lucznikiem zostaje `frey_rider` t4, nie `frey_horseman` t5 - t4 jest w szablonie rodu Frey (4 ludzi) i w awansie z Cutthroata,
  a t5 zostaje kopijnikiem, wiec Freyowie maja obie role (jak w ksiazkach: rycerze i konni lucznicy obok siebie).
- "Korpus o stopien ciezszy" = najciezszy korpus w granicy tieru jednostki (jak Qohor), ale tylko ciezszy niz dzis i najwyzej do tieru
  jednostki - zasada 8b "nic ponad tier" wygrywa z "stopien ciezej".
- +20 jako stala z audytu (bez suwaka) - wylacznik bool wystarcza; Jazda bez sufitu (kon wedlug Jazdy, konie poza decyzja 8b).
- Bron zapasowa Freya z jego wlasnego zestawu (bez dokupowania nowej sztuki do wzorca), tarcza zdjeta (konny lucznik ma rece na luku).

### Test w grze (linie w `Documents\Mount and Blade II Bannerlord\CrashScribe\session-*.log`)
- `Mends: konni lucznicy Freyow (R3-08f) - frey_rider t4` ... `szyk HorseArcher (IsMounted True, IsRanged True), zestawow bojowych
  zmienionych 1 z 1; luk <id> t<=4 (z siodla), strzaly <id> x2, bron zapasowa <id>` - przy wczytaniu, przed liniami sesji.
- `Mends: konni lucznicy Freyow (R3-08f) - frey_rider t4 Luk <stary>-><nowy>` ... `luk bronia glowna TAK.` - po SkillSinew (nowy Luk >
  najwyzsza inna bron).
- `Mends: jazda Doliny i Reach (R3-08e) - korpus o stopien ciezszy w granicy tieru` ... `2 zestawow z 2` - po linii Qohor.
- `Mends: jazda Doliny i Reach (R3-08e) - 3 z 3 jednostek +20 Jazdy i +20 broni glownej` ... `potkniecia 0.` - po DothrakiRiders.
- `Mends: ustawienia Armoury: PODPIETY (Armoury.Settings.Army175FreyHorseArchers)` i `... Army175ValeReachKnights` - klucze podpiete.
- W bitwie z druzyna Freyow: Frey Riderzy w szyku konnych lucznikow, strzelaja z siodla; w okienku jednostki Frey Rider - luk i Luk ok. 120.
- Brak linii "OSTRZEZENIE" z R3-08e/f i brak `Army175.FreyGear` / `Army175.ValeArmor` / `Army175.KnightsSkills` / `Army175.FreySkills`
  w bledach CrashScribe.
