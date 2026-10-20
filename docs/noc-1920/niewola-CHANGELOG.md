## 2026-10-19 (noc 19/20.10, zadanie 9 "niewola") - NIEWOLA GRACZA: ZDOBYWCA ZABIERA RYNSZTUNEK, KONIA I ZLOTO

**Mod:** RealisticCaptivity | **Galaz:** `w-toku/n20-niewola` (od 934cc039 = WGRANIE 88) | **Tor:** M (bez gry - gra zajeta testem; build 0 bledow) |
**Klucz (nowy, domyslnie TAK):** `CaptorTakesArmsAndPurse` (MCM Realistic Captivity -> "Gear and plunder" -> "Captor Takes Arms And Purse"); dziala tylko
przy wlaczonym `PlayerLosesLikeAiLord` (C4, u Jeffa TAK - domyslne). Pliku MCM RealisticCaptivity Jeffa nie ma (`Configs/ModSettings/Global` bez folderu
RealisticCaptivity) - dzialaja domyslne z kodu, wiec klucz wejdzie wlaczony bez zmian w plikach Jeffa. Zadna istniejaca domyslna nie zmieniona (opis C4
dostal dopisek o nowym kluczu). | **Zapis:** nowy klucz `rc_n20_warGear` (napis "1#dokad#wpis;wpis", najwyzej 12 wpisow, ponizej 2 KB, przez
`SaveText.Sync` w RC); stary zapis bez klucza = pusta ksiega. Stara DLL nowego klucza nie czyta (nic sie nie psuje).

**Dla Jeffa (prosto):**
- Gdy wezma Cie do niewoli, zdobywca zabiera teraz caly rynsztunek bojowy: bron, choragiew, zbroje, konia i uprzaz - do SWOICH jukow (lord i banda), a gdy
  pojmala Cie milicja, zaloga albo loch - do zapasu tej osady. Zabiera tez cale zloto, ktore masz przy sobie (do kiesy lorda, kiesy bandy albo kasy osady).
  Nic nie znika: to jest jego lup, jak kazdy inny. Moze go sprzedac w miescie, moze go stracic, gdy ktos rozbije jego druzyne.
- Zostaje Ci: stroj cywilny (nie chodzisz nago po miescie; jesli w stroju cywilnym masz noz albo miecz - tez zostaje), zestaw do skradania, a takze to,
  czego nie nosisz: skrzynia i skarbiec Twoich domow (dom: "what lies within is beyond any captor's reach"), warsztaty, lenna, skarbiec krolestwa (gdy jestes krolem - to nie Twoja kiesa). Kto chce chronic zloto -
  trzyma je w skarbcu domu. Dochod rodu dalej wplywa w niewoli (log: "NIEWOLA zloto"), wiec po wyjsciu masz to, co przyszlo w tym czasie.
- Rynsztunek wraca tylko, gdy odbija Cie sila: gdy ktos rozbije druzyne, ktora Cie trzymala, albo odsiecz Twojego rodu odbije Cie z rak bandy - zabierasz z
  ich jukow to, co tam jeszcze lezy. Gdy druzyna zdobywcy przegrywa bitwe, gra rozdaje jego juki zwyciezcom ZANIM uwolni jencow - wtedy Twoj sprzet
  jest lupem zwyciezcow (wraca zwykle tylko choragiew i rzeczy, ktorych gra nie lupi). Okup, ucieczka, pokoj, wyrzucenie przez bandytow - sprzet zostaje u zdobywcy.
- W bitwie po wyjsciu walczysz w koszuli, dopoki nie kupisz albo nie zalozysz czegos z taboru, skrzyni domu czy zbrojowni. Bez zlota masz prace najemna
  RC (dniowka, warta), skarbiec domu i dochod rodu.
- Unikaty (nazwane bronie i zbroje ROT, stal valyrianska) ida dalej tylko "zwyczajem wojny" Armoury: bierze je zdobywca-bohater, a banda / osada ich nie bierze.
- Wylaczenie: MCM Realistic Captivity -> "Captor Takes Arms And Purse" off (wtedy jak dotad: rynsztunek i kiesa zostaja, zloto tylko lup bitwy 5%).

**Problem (Jeff 19.10, doslownie):** "wypuscili mnie ze zlotem i pancerzem ... i bronia i koniem; zabrali wszystko inne". Dowod: RC log
`Modules/RealisticCaptivity/RealisticCaptivity-2026-10-19_13-00-24.log` 13:31:55 - "Pojmanie gracza (C4, jak lord AI): rynsztunek zostaje na graczu ...,
zloto - tylko lup bitwy (5%, najwyzej 10 000), tabor caly do zdobywcy" i "Przetrzasnieto juki: 154 przedmiotow, 0 zlota. C4 (PlayerLosesLikeAiLord): zloto -> nikt
... -> juki partii Quenten Banefort, Lord of Banefort's Party"; potem "NIEWOLA zloto: 2294 -> 2394" (zloto przy graczu cale niewole).

**Przyczyna:** regula C4 (Jeff 14.10 "tak samo jak pokonany lord AI", klucz `PlayerLosesLikeAiLord`) w `CaptivityBehavior.OnHeroPrisonerTaken` celowo
pomijala `StripGear`, a `LootBaggage` w galezi C4 nie bralo zlota (tylko lup bitwy A6). Stary `StripGear` (droga E5, C4 wylaczone) trzymal zdjety
rynsztunek w "limbo" (`_storedGear`, poza swiatem) z odkupem / paserem / zwrotem przy odbiciu - nie do zlozenia z C4 i nie zamknieta ekonomia
(rynsztunek nie byl u nikogo, a `GiveRags` tworzyl lachmany z niczego).

**Zmiana (RealisticCaptivity):**
- `Settings.cs` + gen_mcm: `CaptorTakesArmsAndPurse` (grupa "Gear and plunder", przy C4); opis C4 z dopiskiem o nowym kluczu.
- `CaptivityBehavior.TakeWarGear` (pojmanie gracza, C4 + klucz): 12 slotow zestawu bojowego (bron 0-3, choragiew 4, glowa, korpus, nogi, rece, plaszcz,
  kon, uprzaz) -> `PlunderDest` = ten sam odbiorca co tabor w `LootBaggage` (`CaptorHome`: osada bez bohatera - `Settlement.ItemRoster`; partia w polu -
  jej `ItemRoster`). Najpierw `AddToCounts` u odbiorcy, dopiero potem pusty slot (wyjatek = sztuka zostaje na graczu, licznik). Unikaty pomijane przez
  `ArmouryBridge.IsUnique` (refleksja na `Armoury.UniqueSpoils.Is`, `AccessTools.DeclaredMethod`; bez Armoury nic nie jest unikatem). Stroj cywilny i zestaw
  ukrycia nietkniete. Ksiega `_warGear` ("slot|item|mod") + `_warGearAt` ("p:partia" / "s:osada") - tylko do odebrania i logu.
- `LootBaggage` (C4 + klucz): cale `Hero.MainHero.Gold` -> `GoldToCaptor` (ta sama droga co E5: kiesa lorda / kasa osady milicji, zalogi, lochu / kiesa partii);
  komunikat "They emptied your purse ..."; linia "Przetrzasnieto juki" mowi "(n20 CaptorTakesArmsAndPurse: cale zloto przy sobie; A6 - zaliczka lupu bitwy)".
- `WarGearOnRelease` (kazde wyjscie gracza z niewoli, `OnHeroPrisonerReleased`): przy odbiciu sila (`ReleasedAfterBattle` albo `Rescue.TryFightRescue` -
  flaga `_rescuedByForce`) i tylko gdy ksiega wskazuje te sama partie / osade, ktora trzymala gracza w chwili wyjscia (`HolderIfSame`) - sztuka po sztuce
  (ten sam przedmiot i stan, `FindIndexOfElement`) z jukow / zapasu do swojego slotu, a przy zajetym slocie do taboru gracza. Czego juz nie ma - nie ma.
  Ksiega czyszczona zawsze (`finally`). Komunikat po angielsku (odebrane / lup zwyciezcow / zostaje u zdobywcy).
- `SyncWarGear` (wlasny try) + nowy `SaveText.cs` w RC (kopia drogi zapisu Armoury 161: napis > 8000 znakow w kawalkach; RC nie zalezy od Armoury).
- `ArmouryBridge.IsUnique` / `UniqueKnown`.

**Kontrola calosci (CLAUDE.md 8.0, w kodzie i dekompilacji 1.4.8):**
- Gra: `TakePrisonerAction.ApplyInternal` (gracz: zdjety z partii, `PlayerCaptivity.StartCaptivity`, potem zdarzenie `OnHeroPrisonerTaken`) - sprzetu nie rusza;
  `EndCaptivityAction.ApplyInternal` dla gracza wola `OnHeroPrisonerReleased(prisoner, partyBelongedToAsPrisoner, ...)` przy KAZDEJ drodze wyjscia (okup,
  ucieczka, pokoj, bitwa, smierc). `MapEvent.CalculateAndCommitMapEventResults`: `LootDefeatedPartyItems` (juki pokonanych do zwyciezcow, bez NotMerchandise,
  przedmiotow zadan i choragwi) PRZED `LootDefeatedPartyPrisoners` (tu `ApplyByReleasedAfterBattle`) - stad przy odbiciu w bitwie z jukow zdobywcy wraca
  zwykle tylko choragiew / NotMerchandise; reszta jest lupem zwyciezcow (zamkniete: przedmiot ma jednego wlasciciela).
- Zloto i A6 (Armoury `BattlePlunder`): pojmanie idzie w `CaptureDefeatedPartyMembers`, przed rozliczeniem A6; `LeaderCap` liczy od migawki: zloto gracza 0 ->
  czesc gracza w puli 0, udzialy zwyciezcow z puli maleja o to samo (bez drugiego pobrania, bez zlota z niczego). `Hero.Gold` nie spada ponizej 0 (setter).
- Inni sluchacze `HeroPrisonerTaken` gracza: Armoury `UniqueSpoils` (unikaty do zdobywcy-bohatera, zamiennik), `CaptiveArmory` (zbrojownia DTE),
  `AutotestBattleKit`, `Ransom178`, `Measure169c`; DTE `ApplyCapturedArmoryLoss` (zbrojownia DTE); BK `BKClanBehavior` (role w druzynie),
  `BKCriminalityBehavior` (przestepstwo) - zaden nie rusza zestawu bojowego gracza ani `Hero.Gold`. Kolejnosc: Armoury wczytany po RC (LauncherData Jeffa),
  sluchacz dopisany pozniej biegnie wczesniej (MbEvent, opis AutotestBattleKit) - unikaty schodza w Armoury PRZED TakeWarGear; gdyby kolejnosc byla inna,
  RC i tak unikaty pomija (IsUnique). BannerlordPrisonIsNotFun / RealisticPrisoner w liscie aktywnych modow Jeffa nie ma.
- Stare drogi bez zmian: C4 wylaczone -> E5 (`StripGear`, odkup, paser, zwrot); `_storedGear` / odkup (`CanBuyBackFrom` wymaga starej ksiegi) - przy nowej
  drodze pusta, wiec dialogu odkupu nie ma (sprzet jest w jukach zdobywcy, nie w limbo). Towarzysze - bez zmian (C4: zostaja w swoim rynsztunku).
- Ksiega stanu sztuk gracza Armoury (`_condition`, slot + id + stan): pusty slot / inna sztuka = nowy rachunek (`GetCondition`); odebrana sztuka ma ten sam
  id i ItemModifier. Ksiegi uzbrojenia / koni (E6/0) to tylko pomiar.
- Watki: wszystko w zdarzeniach kampanii na watku glownym (pojmanie, wyjscie, zapis); zadnej sciezki z `PartiesThink`.

**Ryzyko / co sprawdzic:**
- Duza kiesa gracza (u gracza `Hero.Gold` to tez skarbiec rodu) idzie cala do zdobywcy - zamierzone (E5 decyzja 9 Jeffa z 12.10 i dzisiejsza prosba); bez zlota
  rod moze nie oplacic zoldu do czasu wplywow (gra: dezercje jak przy kazdym braku). Ochrona: skarbiec domu RC.
- Okup (RansomAmountPatch / Armoury 178) liczy majatek ze zlota przy sobie + skarbce domow - po zabraniu kiesy okup gotowka jest mniejszy (178: reszta na raty).
- Zamiennik unikatu z niczego (Armoury `UniqueLaw.StandInFor`, istniejaca regula, licznik w linii "Unikaty (177)") trafia teraz do jukow zdobywcy razem z
  reszta rynsztunku (wczesniej zostawal na graczu) - ta sama liczba sztuk z niczego co dotad, inny wlasciciel.
- Gracz bez konia idzie pieszo (predkosc druzyny); w bitwie bez zbroi i broni do czasu zakupu - zamierzone.
- Przedmioty kute przez gracza (crafted) - odbior szuka po StringId (`MBObjectManager`); gdyby sztuka kuta nie byla zarejestrowana, liczy sie jako "juz nie ma".

**Decyzje (podjete samodzielnie; wylacznik `CaptorTakesArmsAndPurse`):**
1. Zabierane: caly zestaw bojowy (12 slotow, z choragwia), nie zestaw cywilny ani zestaw ukrycia (Jeff: "zabrali wszystko inne" - bron, zbroja, kon, zloto
   maja isc tez; stroj cywilny = gracz nie jest nagi). Bez lachmanow z niczego (`GiveRags` nie uzywany - zamknieta ekonomia).
2. Zloto: tylko to "przy sobie" = `Hero.Gold`. Skarbiec i skrzynia domow RC, warsztaty, karawany, lenna - zostaja (to nie jest przy sobie). Banku z wkladami
   dla gracza w Armoury nie ma (Bank tylko pozycza lordom) - nic tam do ochrony.
3. Dokad: do zdobywcy, nigdy w nicosc - ten sam odbiorca co tabor (juki lorda / bandy, zapas osady milicji / zalogi / lochu); zloto ta sama droga co E5.
4. Zwrot: tylko odbicie sila i tylko z jukow tych, ktorzy trzymali gracza w chwili wyjscia; okup, ucieczka, pokoj, wyrzucenie - sprzet zostaje u zdobywcy
   (jego lup). Bez odkupu w dialogu (sprzet nie lezy w limbo; mozna go odkupic na targu, gdy zdobywca sprzeda).
5. Unikaty: tylko zwyczaj wojny Armoury (bez zmian) - RC ich nie bierze (stal valyrianska w taborze AI znikala - Armoury 177-fix).
6. Towarzysze: bez zmian (C4 - zostaja w rynsztunku); Jeff mowil o sobie.
7. Szanse po wyjsciu: bez broni z niczego; zostaje bron w stroju cywilnym (jesli byla), zestaw ukrycia, skarbiec domu, praca najemna RC, dochod rodu.

**Test w grze:** autotest 5 dob + bitwy: `bieg-bitwa.ps1 ... -Days 5 -Battles 3 -BattleSec 90 -LoadSave autotest-F-d240 -SwitchWaitSec 720 -Speed 20` - bitwa 3
autotestu to plan "slaba druzyna -> przegrana / niewola -> ucieczka" (CrashScribe AutotestBattle, Plan Strong/Retreat/Weak), wiec 3 bitwy pokaza pojmanie
przez bande i wyjscie ucieczka. Bez specjalnego pliku / flagi. W `Modules/RealisticCaptivity/RealisticCaptivity-*.log` biegu:
- `Gracz pojmany przez:` -> `Rynsztunek gracza (n20, CaptorTakesArmsAndPurse): N szt. (wartosc ...) -> juki partii <banda>` (N > 0, gdy gracz testu mial sprzet),
- `Przetrzasnieto juki: ... C4 (PlayerLosesLikeAiLord): zloto -> kiesa partii <banda> (n20 CaptorTakesArmsAndPurse: cale zloto przy sobie; ...)` albo `kiesa pusta (n20`,
- `Rynsztunek gracza (n20) przy wyjsciu z niewoli (ReleasedAfterEscape): zabrane N szt. -> p:<id>; zostaje u zdobywcy N | w tej sesji pojman 1 ...`,
- `NIEWOLA zloto:` (dochod w niewoli) i brak `ERROR in TakeWarGear` / `ERROR in WarGearOnRelease` / `ERROR in SyncWarGear` / `ERROR in ArmouryBridge`.
Armoury log tej bitwy: linia "Lup w bitwie (A6)" bez ujemnych reszt (czesc gracza w puli 0). Odbicia sila autotest nie robi - ta droga tylko w kodzie
(ew. test 160 dob, jesli gracz trafi do niewoli). Wczytanie zapisu po pojmaniu i zapis w niewoli: ksiega `rc_n20_warGear` (bez bledu wczytania).

**Status:** DO SPRAWDZENIA (zbudowane, nie testowane w grze - gra zajeta testem).
