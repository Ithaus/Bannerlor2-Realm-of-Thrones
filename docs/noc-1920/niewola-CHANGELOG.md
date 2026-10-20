## 2026-10-19 (noc 19/20.10, zadanie 9 "niewola") - NIEWOLA GRACZA: ZDOBYWCA ZABIERA RYNSZTUNEK, KONIA I ZLOTO; ODBICIE SILA ODDAJE CALY RYNSZTUNEK

**Mod:** RealisticCaptivity | **Galaz:** `w-toku/n20-niewola` (od 934cc039 = WGRANIE 88) | **Tor:** M (bez gry - gra zajeta testem; build 0 bledow; po recenzji) |
**Klucze (nowe, domyslnie TAK):** `CaptorTakesArmsAndPurse` (MCM Realistic Captivity -> "Gear and plunder" -> "Captor Takes Arms And Purse") i
`GearBackFromVictors` ("Gear Back From Victors", ta sama grupa, po recenzji). Oba dzialaja tylko przy wlaczonym `PlayerLosesLikeAiLord` (C4, u Jeffa TAK -
domyslne). Pliku MCM RealisticCaptivity Jeffa nie ma (`Configs/ModSettings/Global` bez folderu RealisticCaptivity), a w jego `RealisticCaptivity.settings.xml`
nie ma tych kluczy - dzialaja domyslne z kodu, wiec oba wejda wlaczone bez zmian w plikach Jeffa. Zadna istniejaca domyslna nie zmieniona (opisy C4 i
CaptorTakesArmsAndPurse dostaly dopiski). | **Zapis:** nowy klucz `rc_n20_warGear` (napis "1#dokad#wpis;wpis", najwyzej 12 wpisow, ponizej 2 KB, przez
`SaveText.Sync` w RC); stary zapis bez klucza = pusta ksiega. Slad lupu (`GearBackFromVictors`) nie idzie do zapisu (zyje jedno rozliczenie bitwy). Stara DLL
nowego klucza nie czyta (nic sie nie psuje).

**Dla Jeffa (prosto):**
- Gdy wezma Cie do niewoli, zdobywca zabiera teraz caly rynsztunek bojowy: bron, choragiew, zbroje, konia i uprzaz - do SWOICH jukow (lord i banda), a gdy
  pojmala Cie milicja, zaloga albo loch - do zapasu tej osady. Zabiera tez cale zloto, ktore masz przy sobie (do kiesy lorda, kiesy bandy albo kasy osady).
  Nic nie znika: to jest jego lup, jak kazdy inny. Moze go sprzedac w miescie albo zalozyc.
- Zostaje Ci stroj cywilny - ale gra pokazuje go TYLKO w miescie, zamku i wsi. Na mapie, w ekranie postaci i w bitwie gra pokazuje zestaw bojowy, a ten jest
  pusty: tam jestes w bieliznie i pieszo, dopoki nie kupisz albo nie zalozysz czegos nowego. (Lachmanow z niczego nie dajemy - kazda rzecz musi miec
  zrodlo.) Bron, ktora masz w stroju cywilnym, do bitwy nie idzie.
- Zostaje tez wszystko, czego nie nosisz przy sobie: skrzynia i skarbiec Twoich domow ("what lies within is beyond any captor's reach"), warsztaty, lenna,
  skarbiec krolestwa (gdy jestes krolem - to nie Twoja kiesa). Kto chce chronic zloto - trzyma je w skarbcu domu. Dochod rodu dalej wplywa w niewoli
  (log: "NIEWOLA zloto"), wiec po wyjsciu masz to, co przyszlo w tym czasie.
- Rynsztunek wraca, gdy ktos uwolni Cie SILA: rozbije druzyne, ktora Cie trzymala (obcy lord, Twoja druzyna rodu, milicja - wszystko jedno), albo odsiecz
  Twojego rodu odbije Cie z rak bandy. Wraca WSZYSTKO, co zdobywca jeszcze mial - takze to, co zwyciezcy wlasnie wyjeli z jego jukow jako lup (oddaja Ci to).
  Nie wraca tylko to, co zdobywca juz sprzedal albo zalozyl przed bitwa. Zloto nie wraca (monety zdobywcy sa jego - zwyciezcy biora jego kiese jak lup).
- Okup, ucieczka, pokoj, wyrzucenie przez bandytow - sprzet zostaje u zdobywcy (jego lup). Odkupu w rozmowie nie ma (sprzet nie lezy "w powietrzu", tylko
  u zdobywcy); mozna go kupic na targu, jesli zdobywca go sprzeda.
- Bez zlota po wyjsciu masz prace najemna RC (dniowka, warta), skarbiec domu i dochod rodu.
- Unikaty (nazwane bronie i zbroje ROT, stal valyrianska) ida dalej tylko "zwyczajem wojny" Armoury: bierze je zdobywca-bohater, a banda / osada ich nie bierze
  (wtedy zostaja na Tobie).
- Wylaczenie: MCM Realistic Captivity -> "Captor Takes Arms And Purse" off (wtedy jak dotad: rynsztunek i kiesa zostaja, zloto tylko lup bitwy 5%).
  "Gear Back From Victors" off: przy odbiciu w bitwie wraca tylko to, co zwyciezcy zostawili w jukach zdobywcy (losowo okolo polowa).

**Problem (Jeff 19.10, doslownie):** "wypuscili mnie ze zlotem i pancerzem ... i bronia i koniem; zabrali wszystko inne". Dowod: RC log
`Modules/RealisticCaptivity/RealisticCaptivity-2026-10-19_13-00-24.log` 13:31:55 - "Pojmanie gracza (C4, jak lord AI): rynsztunek zostaje na graczu ...,
zloto - tylko lup bitwy (5%, najwyzej 10 000), tabor caly do zdobywcy" i "Przetrzasnieto juki: 154 przedmiotow, 0 zlota. C4 (PlayerLosesLikeAiLord): zloto -> nikt
... -> juki partii Quenten Banefort, Lord of Banefort's Party"; potem "NIEWOLA zloto: 2294 -> 2394" (zloto przy graczu cale niewole).
Po recenzji: przy odbiciu w bitwie gra rozdaje juki pokonanego zdobywcy zwyciezcom losowo (ok. polowa sztuk) PRZED uwolnieniem gracza - bez poprawki
wracala losowo ok. polowa rynsztunku, a ta sama odsiecz rodu dawala 100% (odbicie wirtualne) albo ok. 50% (prawdziwa bitwa, reszta w jukach wlasnej druzyny
rodu, do ktorych gracz nie ma dostepu) - wbrew regule Jeffa z 13.09 ("skoro pokonali bandytow i mnie uwolnili, moj sprzet powinien byc odzyskany" - wtedy
uwolnila go "armia jakiegos lorda").

**Przyczyna:** regula C4 (Jeff 14.10 "tak samo jak pokonany lord AI", klucz `PlayerLosesLikeAiLord`) w `CaptivityBehavior.OnHeroPrisonerTaken` celowo
pomijala `StripGear`, a `LootBaggage` w galezi C4 nie bralo zlota (tylko lup bitwy A6). Stary `StripGear` (droga E5, C4 wylaczone) trzymal zdjety
rynsztunek w "limbo" (`_storedGear`, poza swiatem) z odkupem / paserem / zwrotem przy odbiciu - nie do zlozenia z C4 i nie zamknieta ekonomia
(rynsztunek nie byl u nikogo, a `GiveRags` tworzyl lachmany z niczego). Zwrot przy odbiciu w bitwie (pierwsza wersja tej galezi) czytal tylko juki
zdobywcy, a gra lupi je wczesniej: `MapEvent.LootDefeatedPartyItems` -> `DefaultBattleRewardModel.GetLootItemChancesForWinnerParties` (udzial w bitwie x premia
Roguery x **0.5**; milicja i zaloga pomijane), `FindWinnerPartyToGetCurrentLootObjectBasedOnChances` losuje kazda sztuke osobno (suma szans ok. 0.5 - reszta
zostaje u pokonanego).

**Zmiana (RealisticCaptivity):**
- `Settings.cs` + gen_mcm: `CaptorTakesArmsAndPurse` i `GearBackFromVictors` (grupa "Gear and plunder", przy C4); opisy C4 i CaptorTakesArmsAndPurse z dopiskami.
- `CaptivityBehavior.TakeWarGear` (pojmanie gracza, C4 + klucz): 12 slotow zestawu bojowego (bron 0-3, choragiew 4, glowa, korpus, nogi, rece, plaszcz,
  kon, uprzaz) -> `PlunderDest` = ten sam odbiorca co tabor w `LootBaggage` (`CaptorHome`: osada bez bohatera - `Settlement.ItemRoster`; partia w polu -
  jej `ItemRoster`). Najpierw `AddToCounts` u odbiorcy, dopiero potem pusty slot (wyjatek = sztuka zostaje na graczu, licznik). Unikaty pomijane przez
  `ArmouryBridge.IsUnique` (refleksja na `Armoury.UniqueSpoils.Is`, `AccessTools.DeclaredMethod`; bez Armoury nic nie jest unikatem). Stroj cywilny i zestaw
  ukrycia nietkniete. Ksiega `_warGear` ("slot|item|mod") + `_warGearAt` ("p:partia" / "s:osada") - tylko do odebrania i logu. Komunikat mowi prawde:
  "You keep your town clothes, but on the road and in battle you have only your smallclothes (and go on foot) until you get new gear" (gdy cos zostalo w zestawie
  bojowym - unikat, ktorego banda nie wziela - "You keep your town clothes and the N pieces they left you").
- `LootBaggage` (C4 + klucz): cale `Hero.MainHero.Gold` -> `GoldToCaptor` (ta sama droga co E5: kiesa lorda / kasa osady milicji, zalogi, lochu / kiesa partii);
  komunikat "They emptied your purse ..."; linia "Przetrzasnieto juki" mowi "(n20 CaptorTakesArmsAndPurse: cale zloto przy sobie; A6 - zaliczka lupu bitwy)".
- (po recenzji) `OnLootDistributed` - sluchacz `CampaignEvents.OnLootDistributedToPartyEvent`: gdy gra daje zwyciezcy lup z jukow partii, ktora trzyma gracza
  i do ktorej poszedl jego rynsztunek (`_warGearAt` "p:" + `PartyBelongedToAsPrisoner` / `PlayerCaptivity.CaptorParty`), zapisuje slad: zwyciezca, sztuka
  (przedmiot + stan), ile (najwyzej tyle, ile jest w ksiedze) i bitwa (`MapEvent`). Slad tylko w pamieci, czyszczony przy pojmaniu i kazdym wyjsciu.
  Pierwszy warunek (pusta ksiega) bez alokacji - zdarzenie pada przy kazdej bitwie swiata.
- `WarGearOnRelease` (kazde wyjscie gracza z niewoli, `OnHeroPrisonerReleased`): przy odbiciu sila (`ReleasedAfterBattle` albo `Rescue.TryFightRescue` -
  flaga `_rescuedByForce`) i tylko gdy ksiega wskazuje te sama partie / osade, ktora trzymala gracza w chwili wyjscia (`HolderIfSame`) - sztuka po sztuce
  (ten sam przedmiot i stan, `FindIndexOfElement`): najpierw z jukow / zapasu zdobywcy, a (po recenzji) przy `ReleasedAfterBattle` i `GearBackFromVictors` -
  brakujaca sztuka z jukow zwyciezcy ze sladu (ta sama bitwa: `heldBy.MapEvent == _lootTrailEvent`). Do swojego slotu, przy zajetym slocie do taboru gracza.
  Czego juz nie ma - nie ma. Ksiega i slad czyszczone zawsze (`finally`). Linia logu mowi, ile z jukow zdobywcy, ile od zwyciezcow (ktorych), ktore sloty
  wrocily, stan klucza i czy slad byl uzyty. Komunikat po angielsku (wszystko wrocilo / czesc - reszta sprzedana albo zalozona przed bitwa / zostaje u zdobywcy).
- `ParseGear` (wspolny odczyt wpisu ksiegi), linia startu sesji `Niewola gracza (n20): ...` (stan obu kluczy, czy Armoury UniqueSpoils widac, ksiega z zapisu).
- `SyncWarGear` (wlasny try) + nowy `SaveText.cs` w RC (kopia drogi zapisu Armoury 161: napis > 8000 znakow w kawalkach; RC nie zalezy od Armoury).
- `ArmouryBridge.IsUnique` / `UniqueKnown`.

**Kontrola calosci (CLAUDE.md 8.0, w kodzie i dekompilacji 1.4.8):**
- Gra: `TakePrisonerAction.ApplyInternal` (gracz: zdjety z partii, `PlayerCaptivity.StartCaptivity`, potem zdarzenie `OnHeroPrisonerTaken`) - sprzetu nie rusza;
  `EndCaptivityAction.ApplyInternal` dla gracza wola `OnHeroPrisonerReleased(prisoner, partyBelongedToAsPrisoner, ...)` przy KAZDEJ drodze wyjscia (okup,
  ucieczka, pokoj, bitwa, smierc); `PlayerCaptivity.EndCaptivityInternal` nie wyjmuje partii zdobywcy z bitwy.
- (poprawione po recenzji) `MapEvent.CalculateAndCommitMapEventResults`: `LootDefeatedPartyCasualties`, `LootDefeatedPartyItems`, `LootDefeatedPartyPrisoners`
  (tu `ApplyByReleasedAfterBattle` gracza - dla gracza zawsze, kazda galaz), `CaptureDefeatedPartyMembers`, `CommitCalculatedMapEventResults` - jedno
  wywolanie, partie jeszcze w bitwie (`FinalizeEventAux` pozniej). `LootDefeatedPartyItems`: juki pokonanego bez NotMerchandise, przedmiotow zadan i choragwi,
  kazda sztuka losowana osobno: szansa zwyciezcy = udzial / suma udzialow x premia Roguery x 0.5 (`DefaultBattleRewardModel`; NavalDLC tylko podbija ja przy
  karawanach i wiesniakach; BK `SiegePatch` tylko zamki), wiec zwykle ok. polowa sztuk idzie do zwyciezcow (do `Party.ItemRoster` - `RosterToReceiveLootItems`
  partii NPC), reszta zostaje; milicja i zaloga nie lupia nic (wtedy 100% zostaje). Zdarzenie `OnLootDistributedToParty(zwyciezca, pokonany, lup)` po
  `RosterToReceiveLootItems.Add` - sztuki leza juz u zwyciezcy. Drugie wywolanie tego zdarzenia (`LootDefeatedPartyCasualties`) jest tylko ze zwyciezca =
  partia gracza, ktorej przy niewoli w bitwie nie ma.
- Inni sluchacze lupu: BK `BKCaravansBehavior.OnLootCaravanParties` (tylko umiejetnosci przy karawanach), ROT `ROTTroopRecruiter.OnDistributeLootToParty`
  (tylko wojsko), DTE `ItemRosterForPlayerLootSharePatch` (prefiks `OnCollectLootItems`, tylko zwyciezca = partia gracza) - zaden nie wyjmuje sztuk z jukow
  zwyciezcy miedzy rozdaniem a uwolnieniem. Armoury R14 (`SpoilsOfBrokenParties`) dziala w `OnPartyDestroyed` - po uwolnieniu, wiec odebrane sztuki nie ida
  drugi raz. Armoury nie slucha `OnLootDistributedToParty`.
- Zamknieta ekonomia zwrotu: kazda odebrana sztuka schodzi z jednego miejsca (juki zdobywcy albo juki zwyciezcy, ktory dostal ja z tych jukow w tej bitwie)
  i trafia do gracza (slot albo tabor) - bez kopii, bez sztuk z niczego. Zwyciezca traci tylko to, co przed chwila wzial z rynsztunku gracza.
- Zloto i A6 (Armoury `BattlePlunder`): pojmanie idzie w `CaptureDefeatedPartyMembers`, przed rozliczeniem A6; `LeaderCap` liczy od migawki: zloto gracza 0 ->
  czesc gracza w puli 0, udzialy zwyciezcow z puli maleja o to samo (bez drugiego pobrania, bez zlota z niczego). `Hero.Gold` nie spada ponizej 0 (setter).
- Inni sluchacze `HeroPrisonerTaken` gracza: Armoury `UniqueSpoils` (unikaty do zdobywcy-bohatera, zamiennik), `CaptiveArmory` (zbrojownia DTE),
  `AutotestBattleKit`, `Ransom178`, `Measure169c`; DTE `ApplyCapturedArmoryLoss` (zbrojownia DTE); BK `BKClanBehavior` (role w druzynie),
  `BKCriminalityBehavior` (przestepstwo) - zaden nie rusza zestawu bojowego gracza ani `Hero.Gold`. Sluchacze `HeroPrisonerReleased` w Armoury (`RansomRepeat`,
  `Measure169c`, `Ransom178`) nie ruszaja sprzetu. Kolejnosc: Armoury wczytany po RC (LauncherData Jeffa), sluchacz dopisany pozniej biegnie wczesniej (MbEvent) -
  unikaty schodza w Armoury PRZED TakeWarGear; gdyby kolejnosc byla inna, RC i tak unikaty pomija (IsUnique). BannerlordPrisonIsNotFun / RealisticPrisoner
  w liscie aktywnych modow Jeffa nie ma.
- Co gra pokazuje (po recenzji): `CharacterObject.Equipment` bohatera = `HeroObject.BattleEquipment`; ikona druzyny na mapie (`MobilePartyVisual` -
  `GetVisualPartyLeader(...).Equipment`, z dekompilacji recenzenta), ekran postaci i bitwa uzywaja zestawu bojowego; stroj cywilny widac tylko w scenach osad. Pusty korpus = bielizna.
- Stare drogi bez zmian: C4 wylaczone -> E5 (`StripGear`, odkup, paser, zwrot); `_storedGear` / odkup (`CanBuyBackFrom` wymaga starej ksiegi) - przy nowej
  drodze pusta, wiec dialogu odkupu nie ma. Towarzysze - bez zmian (C4: zostaja w swoim rynsztunku).
- Ksiega stanu sztuk gracza Armoury (`_condition`, slot + id + stan): pusty slot / inna sztuka = nowy rachunek (`GetCondition`); odebrana sztuka ma ten sam
  id i ItemModifier. Ksiegi uzbrojenia / koni (E6/0) to tylko pomiar.
- Watki: wszystko w zdarzeniach kampanii na watku glownym (pojmanie, rozliczenie bitwy, wyjscie, zapis); zadnej sciezki z `PartiesThink`.

**Ryzyko / co sprawdzic:**
- Duza kiesa gracza (u gracza `Hero.Gold` to tez skarbiec rodu) idzie cala do zdobywcy - zamierzone (E5 decyzja 9 Jeffa z 12.10 i dzisiejsza prosba); bez zlota
  rod moze nie oplacic zoldu do czasu wplywow (gra: dezercje jak przy kazdym braku). Ochrona: skarbiec domu RC.
- Okup (RansomAmountPatch / Armoury 178) liczy majatek ze zlota przy sobie + skarbce domow - po zabraniu kiesy okup gotowka jest mniejszy (178: reszta na raty).
- Zamiennik unikatu z niczego (Armoury `UniqueLaw.StandInFor`, istniejaca regula, licznik w linii "Unikaty (177)") trafia teraz do jukow zdobywcy razem z
  reszta rynsztunku (wczesniej zostawal na graczu) - ta sama liczba sztuk z niczego co dotad, inny wlasciciel.
- **Bitwa z pustym zestawem bojowym** - nowy stan w zestawie modow Jeffa (dawna droga E5 przy `LeaveInRags=true` dawala lachmany na korpus). Nie sprawdzalem w
  dekompilacji, jak RBM / DTE / ROT zachowuja sie przy bohaterze bez broni, zbroi i konia w bitwie - autotest z 4. bitwa (nizej) to sprawdza.
- Slad lupu dopasowuje sztuki po przedmiocie i stanie (jak cala gra - sztuki tego samego przedmiotu i stanu sa nie do odroznienia). Gdy zdobywca mial
  wlasna taka sama sztuke, gracz moze dostac "jej" egzemplarz - liczba sztuk sie zgadza, nic nie powstaje.
- Zwrot od zwyciezcy, ktory jest wrogiem gracza (obcy lord rozbija bande trzymajaca gracza): gra i tak go uwalnia (`LootDefeatedPartyPrisoners` - gracz zawsze
  `ApplyByReleasedAfterBattle`), wiec tez oddaje sprzet - jedna regula "uwolniony sila = rynsztunek wraca" (Jeff 13.09).
- Gracz bez konia idzie pieszo (predkosc druzyny); w bitwie bez zbroi i broni do czasu zakupu - zamierzone.
- Przedmioty kute przez gracza (crafted) - odbior szuka po StringId (`MBObjectManager`); gdyby sztuka kuta nie byla zarejestrowana, liczy sie jako "juz nie ma".

**Decyzje (podjete samodzielnie):**
1. Zabierane: caly zestaw bojowy (12 slotow, z choragwia), nie zestaw cywilny ani zestaw ukrycia (Jeff: "zabrali wszystko inne" - bron, zbroja, kon, zloto
   maja isc tez; stroj cywilny zostaje, ale widac go tylko w osadach - na mapie i w bitwie gracz jest w bieliznie, co mowi komunikat). Bez lachmanow z niczego
   (`GiveRags` nie uzywany - zamknieta ekonomia). Wylacznik: `CaptorTakesArmsAndPurse`.
2. Zloto: tylko to "przy sobie" = `Hero.Gold`. Skarbiec i skrzynia domow RC, warsztaty, karawany, lenna - zostaja (to nie jest przy sobie). Banku z wkladami
   dla gracza w Armoury nie ma (Bank tylko pozycza lordom) - nic tam do ochrony. Wylacznik: `CaptorTakesArmsAndPurse`.
3. Dokad: do zdobywcy, nigdy w nicosc - ten sam odbiorca co tabor (juki lorda / bandy, zapas osady milicji / zalogi / lochu); zloto ta sama droga co E5.
4. Zwrot (po recenzji, swiadomy wybor "wszystko, co zdobywca jeszcze mial"): tylko przy uwolnieniu SILA (rozbicie druzyny zdobywcy - kazdy zwyciezca:
   obcy lord, druzyna rodu, milicja; albo odsiecz rodu `TryFightRescue`); wraca to, co lezy w jukach zdobywcy, i to, co zwyciezcy wyjeli z nich w tej bitwie.
   Tak obie drogi odsieczy rodu i kazdy zwyciezca daja ten sam wynik (jedna zasada na jedno zjawisko). Nie wraca: sprzedane / zalozone przed bitwa.
   Wylacznik czesci "od zwyciezcow": `GearBackFromVictors` (off = tylko to, co zostalo w jukach zdobywcy, losowo ok. polowa).
5. Okup, ucieczka, pokoj, wyrzucenie - sprzet zostaje u zdobywcy (jego lup); bez odkupu w dialogu (sprzet nie lezy w limbo; mozna go kupic na targu).
6. Zloto przy odbiciu nie wraca (monety nie maja "sladu"; kiesa pokonanego idzie do zwyciezcow - gra / Armoury BeatenPurse). Tabor tez nie wraca (C4 bez zmian).
7. Unikaty: tylko zwyczaj wojny Armoury (bez zmian) - RC ich nie bierze (stal valyrianska w taborze AI znikala - Armoury 177-fix).
8. Towarzysze: bez zmian (C4 - zostaja w rynsztunku); Jeff mowil o sobie.
9. Szanse po wyjsciu: bez broni z niczego; zestaw ukrycia, skarbiec domu, praca najemna RC, dochod rodu.

**Recenzja (19.10, po pierwszej wersji 96e1169c / e17b1dd6):**
- (wazne) "Przy odbiciu w bitwie wraca losowo ok. polowa, nie tylko choragiew" - PRAWDA. Sprawdzone w dekompilacji: `DefaultBattleRewardModel.GetLootItemChancesForWinnerParties`
  (x0.5, milicja i zaloga pomijane), `MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances` (null = sztuka zostaje), `LootDefeatedPartyItems` (pusta lista
  szans - nic). NavalDLC i BK nie zmieniaja tego dla band i lordow. Poprawione: regula wybrana swiadomie (decyzja 4, klucz `GearBackFromVictors`), opis dla Jeffa
  poprawiony, linia wyjscia mowi, ktore sloty wrocily i skad.
- (wazne) "Odsiecz rodu daje dwa wyniki (wirtualna 100%, prawdziwa bitwa ok. 50% - reszta w jukach druzyny rodu), komunikat o lupie zwyciezcow" - PRAWDA
  (`Rescue.DriveTowardCaptor` -> `SetMoveEngageParty`, `MapEventParty.RosterToReceiveLootItems` NPC = `Party.ItemRoster`). Poprawione szerzej niz podpowiedz
  (nie tylko druzyna rodu): slad lupu od kazdego zwyciezcy tej bitwy - Jeff 13.09 mowil o "armii jakiegos lorda", nie o swoich ludziach. Komunikat "plunder of
  the victors" tylko przy wylaczonym kluczu.
- (drobne) "Stroj cywilny widac tylko w osadach; komunikat 'left in your own clothes' i 'w koszuli' wprowadzaja w blad" - PRAWDA (`CharacterObject.Equipment`
  = `HeroObject.BattleEquipment`). Poprawione: komunikat w grze i opis dla Jeffa mowia o bieliznie na mapie i w bitwie; bez lachmanow z niczego.
- (drobne) "Autotest nie sprawdzi bitwy bez rynsztunku, odbicia ani zapisu w niewoli" - PRAWDA (`AutotestBattle.Plan` = Strong, Retreat, Weak, cyklicznie).
  Poprawione w "Test w grze": `-Battles 4` (4. bitwa = Strong po ucieczce, bez rynsztunku). Odbicia sila i zapisu w niewoli autotest nie zrobi - zapisane jako
  otwarte.

**Test w grze:** autotest 5 dob + bitwy: `bieg-bitwa.ps1 ... -Days 5 -Battles 4 -BattleSec 90 -LoadSave autotest-F-d240 -SwitchWaitSec 720 -Speed 20` -
plan bitew CrashScribe `AutotestBattle` jest cykliczny (Strong, Retreat, Weak, Strong): bitwa 3 = slaba druzyna -> przegrana -> niewola -> ucieczka
(`ApplyByEscape`), bitwa 4 = zwykla bitwa gracza z PUSTYM zestawem bojowym (nowy stan - sprawdza, ze RBM / DTE / ROT nie wywracaja gry przy bohaterze bez
broni, zbroi i konia). Bez specjalnego pliku / flagi. W `Modules/RealisticCaptivity/RealisticCaptivity-*.log` biegu:
- na starcie sesji (kazdy bieg): `Niewola gracza (n20): CaptorTakesArmsAndPurse=True (C4 PlayerLosesLikeAiLord=True; czynne: True), GearBackFromVictors=True, unikaty przez Armoury UniqueSpoils: True, ksiega rynsztunku u zdobywcy z zapisu: 0 szt.`,
- `Gracz pojmany przez:` -> `Rynsztunek gracza (n20, CaptorTakesArmsAndPurse): N szt. (wartosc ...) -> juki partii <banda>` (N > 0, gdy gracz testu mial sprzet;
  w tej linii "zostaje w nim 0 szt." albo liczba unikatow, ktorych banda nie wziela),
- `Przetrzasnieto juki: ... C4 (PlayerLosesLikeAiLord): zloto -> kiesa partii <banda> (n20 CaptorTakesArmsAndPurse: cale zloto przy sobie; ...)` albo `kiesa pusta (n20`,
- `Rynsztunek gracza (n20) przy wyjsciu z niewoli (ReleasedAfterEscape): zabrane N szt. -> p:<id>; zostaje u zdobywcy N | w tej sesji pojman 1 ...`,
- `NIEWOLA zloto:` (dochod w niewoli) i brak `ERROR in TakeWarGear` / `ERROR in WarGearOnRelease` / `ERROR in OnLootDistributed` / `ERROR in SyncWarGear` /
  `ERROR in ArmouryBridge`.
W CrashScribe `session-*.log` bitwy 4: bitwa idzie do konca bez wywrotki (bohater bez rynsztunku). Armoury log bitwy 3: linia "Lup w bitwie (A6)" bez ujemnych reszt
(czesc gracza w puli 0). Linii `Slad lupu (n20 GearBackFromVictors):` autotest nie pokaze (ucieczka, nie odbicie) - odbicie sila (ReleasedAfterBattle i
TryFightRescue) sprawdzone tylko w kodzie i dekompilacji; przy odbiciu w grze Jeffa (albo w tescie 160 dob, jesli gracz trafi do niewoli i ktos go odbije)
powinno byc: `Slad lupu (n20 GearBackFromVictors): <zwyciezca> wzial z jukow <zdobywca> K szt. ...`, potem `Rynsztunek gracza (n20) przy wyjsciu z niewoli
(ReleasedAfterBattle, odbicie sila): zabrane N szt. -> p:<id>; odebrane N (z juki partii <zdobywca> N-K, od zwyciezcow z tej bitwy K [<zwyciezca>]), sloty ..., juz nie ma 0
| GearBackFromVictors=True, slad lupu uzyty`. Zapis i wczytanie w niewoli (ksiega `rc_n20_warGear`) - autotest tego nie robi; po wczytaniu zapisu z niewoli
linia startu sesji pokazuje "ksiega rynsztunku u zdobywcy z zapisu: N szt. (p:...)".

**Status:** DO SPRAWDZENIA (zbudowane, nie testowane w grze - gra zajeta testem).
