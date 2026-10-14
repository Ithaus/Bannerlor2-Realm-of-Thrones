# Audyt zbioru e8 (4d33042..43af413) - soczewka GRACZ I GRA NA ZYWO

Drzewo: `...\ae222cb7-...\scratchpad\zb\int` (galaz w-toku/zbior-e8, HEAD 43af413). Diff 92 pliki, +17 280 linii.
Logi: `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_20-41-38.log` (120 dob, wszystko poza L5b),
`...19-21-12.log` (rok gospodarki), dla porownania `...15-02-59.log` (rok, wersja sprzed tej nocy).
Dekompilacja gry: `...\3cf3e0ac-...\scratchpad\audyt-pieniadz\twall` (SiegeAftermathCampaignBehavior, KingdomManager, MapEvent,
DefaultPartySpeedCalculatingModel, PrisonerReleaseCampaignBehavior, Building).
Nic nie zmienialem w kodzie, nic nie uruchamialem w grze.

Szukane tylko INTERAKCJE miedzy paczkami widoczne dla gracza. Wynik w skrocie: zadnej sciezki wywrotki (CTD) ani
pulapki `GameMenu.SwitchToMenu` nie znalazlem; jest jedna realna kolizja dwoch paczek lupu (L5b-2 x L7) uderzajaca w
predkosc druzyny gracza i armii AI, jedna dziura procesowa (zadna sciezka gracza z tej nocy nie przeszla testu, a
wersja koncowa 43af413 nie ma zadnego biegu) i kilkanascie drobnych niespojnosci napisow, MCM i komunikatow.

---------------------------------------------------------------------------------------------------------------------

## 1. Znaleziska (od najpowazniejszego)

### [WAZNE] 1. L5b-2 (karawany wroga) wrzuca caly ladunek do druzyny zdobywcy bez udzwigu i bez limitu stada - L7 (towar z polek) pilnuje obu
- **Strona A:** `EnemyCaravans.cs:210-239` - kazda sztuka ladunku karawany (towar, konie handlowe, juczne) idzie do
  `cap.ItemRoster.AddToCounts(ee, off)` bez sprawdzenia udzwigu i stada; wszystko do JEDNEJ druzyny (wodz armii).
- **Strona B:** `CitySack.Goods.cs:44-57, 124-151, 172-191` - L7 daje towar z polek tylko w wolnym udzwigu (`FreeKg`) i konie
  tylko do granicy kary "Herding" (`HerdRoom`), rozdzielajac wedlug ludzi na wszystkie druzyny armii.
- **Kolejnosc w grze** (`MapEvent.cs:2079` OnMapEventEnded, potem `:2093` SiegeCompleted):
  - zdobycie przez AI: najpierw L7 (z limitami), potem L5b-2 dosypuje na wierzch bez limitow;
  - zdobycie przez gracza-wodza: L5b-2 przy koncu bitwy, L7 dopiero przy wyborze w menu - L7 widzi juz zapchana
    druzyne gracza (FreeKg ~ 0, HerdRoom ~ 0) i daje jego druzynie malo albo nic.
- **Scenariusz:** gracz (60-100 ludzi) szturmuje miasto, w ktorym stoja 2-3 karawany wroga. Karawana startuje z ok. 15
  jucznymi (log: "startowe 324 z 4830 jucznymi gry") plus ladunek, czasem konie jako towar. Po bitwie druzyna gracza
  dostaje 30-50 zwierzat i setki sztuk towaru.
- **Skutek w grze:** kara stada gry to -0.3 x (nadmiar / ludzie), najwyzej -80% (`DefaultPartySpeedCalculatingModel.cs:378-390`),
  do tego kara ladunku i przeciazenia (`:148-157`). Gracz wychodzi ze zdobytego miasta w slimaczym tempie i musi sprzedac
  lup w miescie, ktoremu L2 wlasnie zabral 1/3-2/3 kasy kupcow (malo kto kupi). U AI zwalnia druzyna wodza, a z nia cala
  armia, az do wejscia do nastepnego miasta (PartiesSellLoot dziala dopiero przy nastepnym wejsciu). Komunikat 5b
  ("... items of cargo, horses and pack animals to your baggage") nie mowi, ze to przeciazy druzyne.
- **Naprawa:** w `EnemyCaravans.Seize` uzyc tej samej dystrybucji co L7: odbiorcy z `CitySack.Recipients` (druzyny armii
  wedlug ludzi), zwierzeta do `HerdRoom`, reszta do `FreeKg`. Czego nikt nie uniesie, idzie na polke zdobytej osady
  (`st.ItemRoster`) w ramce `GoodsLedger.Begin(FSack, st)`, zeby ksiega towarow nadal sie zamykala. W komunikacie gracza
  dopisac "N left on the market of X". Mozna tez prosciej: wspolna metode `CitySack.Distribute(...)` wolac z obu paczek.

### [WAZNE] 2. Zadna sciezka gracza z tej nocy nie przeszla testu, a wersja koncowa 43af413 nie ma ani jednego biegu
- **Dowody w logu 20-41-38:**
  - "Lup miast (L0)": 4 nastepstwa, wszystkie "gracz -"; linii "Lup miast (L5)" (okno gracza) 0;
  - L1 "kiesy zalog ... (gracz 0)" 82/82;
  - "Tarczowe (W6)": "gracz: ... ziemia nie, wasal krolestwa nie, krolestwo w wojnie nie -> warunki nie spelnione" przez
    120 dob; W7 "gracz bez klucza";
  - N7: "rod gracza pod choragiew 0" 120/120;
  - L5b nie bylo w tym biegu wcale (wg zadania i CHANGELOG: tylko narzedzie na "logu kodu 43").
- **Sciezki nietestowane w grze:**
  - `CitySack.Menu.cs` (TextPost, LeaderMenuPre, Summary, MenuLine);
  - `CitySack.Coin.cs:185-210` (Messages), `CitySack.Capture.cs:78`, `CitySack.Goods.cs:249-255`, `CitySack.Window.cs:46`;
  - `KingdomTreasury.ScutagePlayer.cs` (cala), `KingdomTreasury.LordLevy.cs:430-454`;
  - `StragglersRally.cs:372, 422, 484, 561-577, 667` (odbior do MainParty);
  - `EnemyCaravans.cs:168, 247, 271-277`, `CapitalTreasury.cs:229-256, 280-285` (galaz "glowa rodu bez krolestwa" = gracz).
- **Skutek:** Jeff zaczyna kampanie na wersji, w ktorej pierwszy kontakt z menu lupu, tarczowym, rozbitymi i karawanami bedzie
  jego wlasnym testem. Wywrotek nie przewiduje: kazde wejscie gracza jest w try/catch z licznikiem potkniec. Jesli cos
  pojdzie zle, gracz zobaczy raczej zle liczby albo brak komunikatu niz CTD.
- **Naprawa:** przed oddaniem jeden autotest wersji 43af413 (zgoda na autotest z 07.10) z gracza-wodza, na tej samej konsoli
  co dotad. Kolejno:
  1. szturm miasta wroga z karawanami: grabiez, potem laska na innym (linie L5, L2, L7, L5b-2, "gracz wodz");
  2. gracz jako wasal z zamkiem w wojnie przez 8 dob (W6b: wyjasnienie + suma tygodnia);
  3. przegrana bitwa w polu i odbior rozbitych (N7: "fled toward" / "rejoined you").
  Progi: brak potkniec w liniach L5/L5b/N7/W6, kontrola L7 "bilans sztuk = 0", L5b "zdjete - dopisane = 0".

### [DROBNE] 3. Klucze lupu rozbite po dwoch grupach MCM, kilka kluczy w grupach bez zwiazku (artefakt scalenia galezi)
- **Lup w dwoch miejscach:**
  - `McmSettings.cs:1515-1525` - Capital Treasury Seized, Capital Treasury Share i Enemy Caravans Seized sa w "The marching
    column" (obok starych Sack Scar / Sack Loyalty Hit). Paczka 5b powstala w innej galezi.
  - 15 kluczy lupu L0-L7 jest w nowej grupie "The sack of towns and castles" (`:5515` i dalej).
- **Klucze w grupach bez zwiazku:**
  - Stragglers Rally (+Wait/Reach Days) i Food Sources In Ledger - w "Iron Bank" (`:2559`);
  - Scutage Player Clan, Lord War Levy i Player War Levy - w "The townsfolk's purse" (`:5167`);
  - Peace Release After Day - w "Ransoms by wealth (stage 2)" (`:3847`);
  - Folk Builds Town - w "Army purchases";
  - Idle Caravan Disband - w "The town's purse".
- **Skutek:** gracz szukajacy "co sie dzieje po zdobyciu miasta" znajdzie polowe ustawien; "Stragglers" pod Bankiem Zelaznym jest nie do znalezienia.
- **Naprawa:** w `tools/gen_mcm.py` przypisac grupy: 5b do "The sack of towns and castles"; N7 do grupy bitew lub wojska;
  W6b/W7 do grupy korony ("The crown's income" lub W5/W6); Peace Release do dyplomacji. Potem gen_mcm i build. Zmiana grupy
  nie rusza zapisanych wartosci (MCM trzyma je po Id).

### [DROBNE] 4. "Stragglers: X has fallen to the enemy" takze wtedy, gdy nic nie padlo, tylko rod gracza zmienil strone
- **Kod:** `StragglersRally.cs:417` (`Hostile(g.Rally, g.Clan)` -> `Move`) i komunikat `:484`. `Hostile` jest prawdziwe takze
  wtedy, gdy rod gracza zmienil krolestwo, skonczyl kontrakt najemnika albo wypowiedzial wojne.
- **Scenariusz:** gracz-najemnik przegrywa bitwe; jego rozbici czekaja w miescie pracodawcy. Gracz konczy kontrakt i
  przechodzi do wroga. Nastepnego ranka widzi: "Stragglers: <miasto> has fallen to the enemy - 23 of your men are making for <inne>".
- **Skutek:** falszywa wiadomosc, ze miasto padlo.
- **Naprawa:** zapamietac `Rally.MapFaction` przy tworzeniu grupy; "has fallen" tylko gdy zmienila sie frakcja osady. W
  pozostalych przypadkach napis "<miasto> is no longer safe for your men - they are making for <inne>".

### [DROBNE] 5. "Your men are still gorged on the sack of X" takze graczowi, ktory w lupie nie bral udzialu
- **Kod:** `CitySack.Window.cs:41-46`. `Sacker` liczy druzyny KROLESTWA zdobywcy (decyzja 3), wiec warunek jest prawdziwy
  dla kazdej druzyny tego krolestwa.
- **Scenariusz:** lord AI z krolestwa gracza (albo pracodawcy najemnika) zlupil miasto. Gracz przyjezdza po dwoch dobach i przy wyjezdzie widzi ten napis.
- **Skutek:** napis sugeruje, ze to jego ludzie lupili.
- **Naprawa:** komunikat tylko gdy `MainParty` byla w `contrib` tego lupu (zapamietac w `Track` flage `PlayerIn`). Inaczej
  napis "Your realm's soldiers took what they wanted in X - your men pay nothing here for N days".

### [DROBNE] 6. Ostrzezenie "X is yours - but the sack has left it bleeding" nie trafia juz tam, gdzie trafialo
- **Kod:** `CitySack.Coin.cs:255-266` (`OwnerChangedScarSkipped`), wolane z `WarLedger.cs:309`. Gra przy zdobyciu oddaje osade
  KROLOWI zdobywcy (`KingdomManager.cs:244` `newOwner = kingdom.Leader`).
- **Przed ta noca** (`WarLedger.cs:316-317`) dostawal go gracz-krol przy kazdym zdobyciu przez jego krolestwo i gracz bez
  krolestwa przy wlasnym zdobyciu, od razu przy zmianie pana.
- **Przypadek 1 - gracz-wodz zdobywa sam** (jako krol albo bez krolestwa): zmiana pana nastepuje PRZED wyborem w menu, wpisu
  lupu jeszcze nie ma. Komunikat nie pojawia sie juz nigdy.
- **Przypadek 2 - gracz-krol:** komunikat dostaje tylko po grabiezy lorda AI swojego krolestwa, ze slowami "Rebuild what you broke".
- **Skutek:** brak ostrzezenia tam, gdzie gracz sam zniszczyl, i oskarzenie tam, gdzie zniszczyl ktos inny.
- **Naprawa:** komunikat przeniesc do `AftermathPost` (w chwili nalozenia blizny), gdy `__1.OwnerClan == Clan.PlayerClan`.
  Tresc neutralna: "X has been sacked: prosperity and loyalty are down."

### [DROBNE] 7. Jedno zdobycie, dwa slowa na pieniadz i dwa formaty liczb
- **"coins":** L2/L5/W7 (`CitySack.Coin.cs:196-207`, `CitySack.Menu.cs:122-160`, `KingdomTreasury.LordLevy.cs:450`).
- **"denars":** L5b, L1 i W6b (`EnemyCaravans.cs:272-277`, `CapitalTreasury.cs:281-285`, `CitySack.Capture.cs:78`,
  `KingdomTreasury.ScutagePlayer.cs:205-206`).
- **Bez separatora tysiecy:** `CitySack.Capture.cs:78` i `ScutagePlayer.cs:205-206`; reszta uzywa "N0".
- **Skutek:** przy jednym szturmie gracz czyta "Your army took 12,000 coins" i "4,350 denars go to your purse" - dwa slowa na to samo.
- **Naprawa:** wszedzie "denars" (slowo gry) i `ToString("N0", Inv)`.

### [DROBNE] 8. Zamek opisany jak miasto ("merchants", "townsfolk")
- **Kod:** `CitySack.Menu.cs:122-139, 154, 159-160, 182` i `CitySack.Coin.cs:196, 207`. Te napisy nie patrza na `k.Castle`,
  choc `Read()` je ustawia (`CitySack.cs:79`).
- **Scenariusz:** gracz szturmuje zamek. Podpowiedz mowi "Merchants' coin: 3,200 - the army takes 1,066 ... Townsfolk's coin: ...".
- **Naprawa:** przy `k.Castle` (albo `!st.IsTown`) napisy "the castle's purse" i "the castle folk".

### [DROBNE] 9. Podpowiedzi L5 nie mowia o towarze z polek (L7), choc gracz wybiera grabiez/laske takze dla lupu
- **Kod:** `CitySack.Menu.cs:87-145` (Preview) i `:147-161` (Summary) licza tylko monete. L7 daje osobny komunikat dopiero
  PO wyborze (`CitySack.Goods.cs:252`). LB pokazuje poziomy budowli w tej samej podpowiedzi (`ProjectsLossPre`), wiec brak
  towaru wyglada na przeoczenie przy dokladaniu L7 po L5.
- **Naprawa:** w Preview dopisac linie "Goods: a third of what lies on the shelves (N pieces, worth ~V); horses and food
  only as much as your army can lead and eat". Liczby z tego samego planu co `SackGoods`, tylko odczyt.

### [DROBNE] 10. Wyjasnienia W6b i W7 wracaja po kazdym wczytaniu
- **Kod:** `KingdomTreasury.ScutagePlayer.cs:187-199` (`_plToldScut`, `_plToldAid` - "bez pola zapisu - po wczytaniu jeszcze
  raz") i `KingdomTreasury.LordLevy.cs:436-445`.
- **Skutek:** Jeff czesto wczytuje. Po kazdym wczytaniu, w pierwszej dobie z poborem, dostaje dwa komunikaty po 300-400
  znakow. Przy grze z czestymi wczytaniami to spam.
- **Naprawa:** flagi "juz wyjasnione" w zapisie (np. dopisek do istniejacego klucza `arm_m6a` albo osobny maly klucz).

### [DROBNE] 11. Opisy MCM nie mowia, co sie stanie z biezacym stanem po wylaczeniu w trakcie kampanii
- **Stragglers Rally:** po wylaczeniu WSZYSCY czekajacy rozchodza sie nastepnego ranka (`StragglersRally.cs:414`, z
  komunikatem "gave up waiting"). Opis mowi tylko "Off = as before".
- **Sack Burns Buildings:** po wylaczeniu zgliszcza zamieraja (bez postepu i kolejki, `CitySack.Burn.cs:196`), a po
  ponownym wlaczeniu wracaja. Opis milczy.
- Reszta nowych kluczy ze stanem ma ostrzezenie w opisie albo stan, ktory sie nie psuje: Start Gold By People, Goods Rates
  By Pattern i Sarnor People Raised ("new campaign only"); Townsfolk Buy Only Work Horses ("turn on together with ...").
- **Naprawa:** jedno zdanie w HintText (Settings.cs + gen_mcm).

### [DROBNE] 12. Komunikaty lupu bez korony i bez sakiewek
- **Korona:** L5 mowi "(the crown will claim N of your part)". Kiesa karawan (`EnemyCaravans.cs:192-194` ->
  `Ransom178.NoteBeatenGold`) i kiesa zalogi (`CitySack.Capture.cs:74-78`) tez placa koronie 1/9 nastepnego ranka, ale
  komunikat tego nie mowi. Gracz-wasal rano widzi ubytek bez wyjasnienia.
- **Sakiewki:** przy wylaczonym MenPurse komunikat i podsumowanie mowia "Your men's purses grew by 0" (`CitySack.Coin.cs:197`, `CitySack.Menu.cs:160`).
- **Naprawa:** dopisek o koronie przez ten sam rachunek co `CitySack.CrownPart` (1/9 przy `Ransom178.WouldTax`); zdanie o sakiewkach tylko przy `MenPurse.On`.

---------------------------------------------------------------------------------------------------------------------

## 2. Wszystkie nowe napisy widoczne dla gracza (sprawdzone: wszystkie po angielsku, zadnego polskiego slowa)

### 2a. Komunikaty w dzienniku (Log.Player; kolor zloty, "bad" = czerwony)

| Gdzie | Tresc (zmienne w <>) | Kiedy / jak czesto |
|---|---|---|
| StragglersRally.cs:372 | Stragglers: <N> of your men fled toward <osada>. / Stragglers: <N> men of <druzyna> fled toward <osada>. | raz na pobita druzyne rodu gracza w bitwie |
| :422 | Stragglers: <N> of your men have gathered at <osada>. | raz na grupe (gotowosc) |
| :484 | Stragglers: <osada> has fallen to the enemy - <N> of your men are making for <osada2>. | raz na przeniesienie (pkt 4) |
| :575 | Stragglers: <N> of your men rejoined you from <osada>. / ... rejoined <druzyna> from <osada>. | przy kazdym odbiorze (moze co dobe, gdy miejsce zwalnia sie stopniowo) |
| :667 | Stragglers: <N> of your men at <osada> gave up waiting - <a> went home, <b> took to the woods. | raz na rozejscie grupy |
| ScutagePlayer.cs:190 | Scutage: in war a vassal whose lands could pay for more soldiers ... Field more men and you pay less. | raz na sesje (pkt 10) |
| :197 | War aid: in war the crown helps pay the wages of vassals ... (garrisons and service). | raz na sesje |
| :205 / :206 | Scutage: you paid <N> denars to the crown of <K> this week - ... / War aid: the crown of <K> paid you <N> denars this week ... | co 7 dob |
| LordLevy.cs:441 / :450 | War levy: while your lands cannot pay ... You can turn it off (Player War Levy). / War levy: your fiefs gave you <N> coins this week ... | tylko z Player War Levy (domyslnie wylaczone); raz na sesje + co 7 dob |
| FolkAidLoyalty.cs:271 | The tax gatherers have left <osady>: the place is / they are too close to revolt to pay the war aid. (czerwony) | raz na osade, do powrotu lojalnosci |
| BuildFunding.Folk.cs:351 | The townsfolk / castle folk of <osada> have finished the <budowla> (level <n>) at their own cost. | przy kazdym ukonczonym poziomie w lennie gracza |
| CitySack.Coin.cs:196 | Your army took <N> coins from <osada>: <a> from its merchants, <b> from its townsfolk. Your men's purses grew by <c>; your own part is <d>. | gracz-wodz bez menu L5 (Right of Pillage albo L5 wylaczone) |
| :202 / :204 | <wodz> spared <osada>. There is no plunder. / <wodz> let the army sack <osada>. Your party's part: <N> coins (your men <a>, you <b>). | gracz w cudzej armii |
| :207 | <osada> has been sacked: its merchants lost <N> coins, its townsfolk <M>. (czerwony) | lenno gracza zlupione |
| :265 | <osada> is yours - but the sack has left it bleeding: ... Rebuild what you broke. (czerwony) | patrz pkt 6 |
| CitySack.Capture.cs:78 | The broken garrison's chest is yours: <N> denars. | gracz rozbil zaloge wroga |
| CitySack.Goods.cs:252 | From the shelves of <osada> your men carried off what they could: the army took <N> goods, horses and arms[, and the fire took <M> more]. | druzyna gracza wsrod odbiorcow lupu |
| CitySack.Window.cs:46 | Your men are still gorged on the sack of <osada> - they spend nothing here. | przy kazdym wyjezdzie z miasta w oknie 3 dob (pkt 5) |
| CapitalTreasury.cs:281 / 283 / 285 | <stolica> has fallen to <F>: the enemy has carried off <N> denars from the royal treasury of <K>. / With <stolica> taken, <N> denars ... pass to the treasury of <K2>. / ... are now in your purse. | upadek stolicy (rzadko) |
| EnemyCaravans.cs:272 / 276 | <osada> has fallen to <F>: your caravan there was seized - <N> denars and <M> items ... are lost; <imie> is now held prisoner. / In <osada> your men seized <k> enemy caravans: <N> denars go to your purse and <M> items of cargo, horses and pack animals to your baggage; <k> caravan masters are your prisoners. | przy zdobyciu |

### 2b. Menu lupu (L5), tekst w podpowiedziach "Devastate / Pillage / Show Mercy" i w podsumowaniu (CitySack.Menu.cs)
Podpowiedz: "Little is left here: <X> was sacked <today/yesterday/N days ago>." | "No sack: the merchants keep <N> and the
townsfolk <M>. Your men get nothing from the town - they will spend their pay here as in any town." | "The sack: <a third/
two thirds/...> of all coin in <X>." | "Merchants' coin: <N> - the army takes <M>." | "Townsfolk's coin: ..." | "Your party (<n> of
<m> men): <N> - your men <a>, you <b> (the crown will claim <c> of your part)." | "Afterwards: the merchants keep <N>, the
townsfolk <M>. For <three days> your men will take what they want here and pay for nothing."
Podsumowanie: "Your army took <N> coins from <X>: ... Your men's purses grew by <a>; your own part is <b> (the crown will
claim <c> of it)." albo "No sack: ... Your men will spend their pay here as in any town."

### 2c. Okno "People of this land" (PeopleMenu.cs, ShowInquiry z "Close")
Nowe linie: "Stragglers waiting to rejoin their lords: <N>" (N7) | wies: "Goods made here: x<f> of what the hearths alone would
give - this village stands for <N> people, <u> times an average village of the world, and goods follow the people [(no
village yields more than <max> times the average)]" / "Goods made here: as before - the Night's Watch lives on ..." (GL-1) |
"Works: the townsfolk put <N> coins into the <budowla> today - ..." i 6 wariantow "Works: ..." (P2) | "Better living: ..."
(P3/P5, 3 warianty) | "Sacked <N days ago>: the merchants lost <N> coins, the townsfolk <M>." (L5, 30 dob).

### 2d. Dymki i wyjasnienia liczb gry
- dymek wsi: wiersz "People of this land" = "x<u>" (`VillageByPeople.cs:680`). Uwaga: w dymku "x" to ludzie wobec sredniej,
  w oknie "Goods made here: x" to plon. Ten sam znak dla dwoch roznych liczb, do rozwazenia napis "x0.85 of an average village";
- milicja (przyrost i limit): "People of this land";
- zywnosc warowni: "Townsfolk purchases: counted for the people of the market";
- lojalnosc: "War aid from the people's savings";
- produkcja wsi: "Armoury: goods follow the people of this land", "Armoury: salt rate of the realm (x2)";
- budowa: "Town works (paid by the townsfolk)";
- lore wsi (DistrictMenu / dymek W2): "Flax growers and linen weavers." / "Flax growers and net-makers: linen thread for nets and sails."

---------------------------------------------------------------------------------------------------------------------

## 3. Czestotliwosc komunikatow - spamu nie ma
- Codziennych komunikatow brak. W6b/W7 to sumy co 7 dob. Wyjasnienia W6b/W7 pokazuja sie raz na sesje, ale po kazdym
  wczytaniu od nowa (pkt 10). P1b - raz na osade.
- Zdarzenia (zdobycie, bitwa) daja paczke 3-5 linii naraz. Przyklad przy szturmie gracza: garrison chest, caravans seized,
  royal treasury (gdy stolica), from the shelves. To tresc zdarzenia, nie spam.
- N7 moze dawac "rejoined" codziennie, dopoki druzyna ma miejsce tylko na czesc grupy: jedna linia na grupe i druzyne.
- "Your men are still gorged" - jedna linia przy kazdym wyjezdzie z lupionego miasta przez 3 doby.

## 4. Menu lupu L5 i pulapka GameMenu.SwitchToMenu - czysto
- W calym diffie nie ma `GameMenu.SwitchToMenu`, `ExitToLast`, `ActivateGameMenu`, `PlayerEncounter.*`, `EnterSettlementAction`
  ani `LeaveSettlementAction` (grep diffu).
- L5 to postfiks `GetSiegeAftermathConsequencesText`: podmienia tylko zmienne ARMY_GOLD_TEXT i PARTY_GOLD_TEXT zwroconego
  TextObject. Prefiks `menu_settlement_taken_player_leader_on_init` (menu zwykle, nie oczekiwania) ustawia tylko pole float
  `_settlementProsperityCache`. Menu przelacza wylacznie gra (`SiegeAftermathCampaignBehavior.cs:298-318, 446, 476, 518`).
- Jedyne zmiany stanu w czasie zdarzenia gracza to `TakePrisonerAction` i `DestroyPartyAction(null, karawana)` z L5b-2. Biegna w
  postfiksie `KingdomManager.SiegeCompleted`, czyli w finalizacji MapEvent (`MapEvent.cs:2093`), a nie w opcji menu.
- PeaceHold wola co godzine metode gry `ReleasePrisonersInternal`. To ten sam kontekst co dobowa decyzja pokoju w grze, a
  gracza gra tam nie uwalnia (`PrisonerReleaseCampaignBehavior.cs:115`: `HeroObject != Hero.MainHero`).

## 5. Okno "People of this land" - czysto
Kazda nowa linia ma wlasny try/catch (`PeopleMenu.cs` diff), jest tylko odczytem i dziala bez SwitchToMenu. `TownFolk.LuxuryLine`
przy kazdym otwarciu okna dopisuje linie do logu - to tylko log, nie widac go w grze.

## 6. MCM - 68 nowych kluczy (gen_mcm 1406 pozycji)
- Wszystkie nazwy i opisy sa po angielsku; polskich znakow i slow nie znalazlem (skrypt po HintText). Najdluzsze opisy
  (Lord War Levy 1173 znakow, Stragglers Rally 1086) nie przekraczaja starych (rekord 1390).
- 44 wylaczniki (42 wlaczone; wylaczone Player War Levy - decyzja Jeffa - i Levy By People - GL-4, decyzja) i 24 liczby.
  Wartosci rozsadne: Sack 0.333/0.667, Stragglers 30 dob / 5 dob zasiegu, Merc 28, Ransom 56 dob / 0.25.
  Trzem starym kluczom zmieniono tylko opis: Scutage, Crown War Aid By Need, Horse Breeding By Demand.
- Wartosci domyslne Settings.cs == McmSettings.cs dla wszystkich 68 (porownanie skryptem). Pulapka z CLAUDE.md 7 (MCM
  nadpisuje domyslne) nie dotyczy nowych kluczy: zadnego z nich nie ma w `Documents\...\Configs\ModSettings\Global\Armoury\Armoury.json`
  Jeffa (plik z 13.10 02:10, sprawdzone grep), wiec obowiaza wartosci z kodu.
- Grupy - pkt 3. Opisy przy wylaczaniu w trakcie kampanii - pkt 11.
- Klucz "Scutage Player Clan" jest wlaczony domyslnie: gracz-wasal z zamkiem zaplaci tarczowe od pierwszej doby wojny
  (decyzja Jeffa "rod gracza tez"). Warto mu to powiedziec przy starcie kampanii.

## 7. Wyjatki przy akcjach gracza - macierz (kod przejrzany, bez biegu)

| Sytuacja | Paczki | Wynik |
|---|---|---|
| Gracz-wodz zdobywa miasto (szturm) | L0-L7, LB, L5b-1, L5b-2, L1-D3 | bez wyjatku; kolizja ladunku - pkt 1; brak "is yours" - pkt 6 |
| Gracz zdobywa zamek | jw. | dziala; napisy "merchants/townsfolk" - pkt 8; stolica to zawsze miasto |
| Gracz w armii AI przy zdobyciu | L2 Messages, L4, L7 | dziala; gra nie wola dla niego GetSiegeAftermathConsequencesText - tylko komunikat |
| Gracz-wodz nieprzytomny (HandlePlayerDeathDuringSiegeAftermath) | L2, L5 | aftermath z gry, podsumowanie z L2 - spojne |
| Gracz-najemnik | W6b (wylaczony warunkiem), MercTenure (gracz pominiety: `MercTenure.cs:85,129,260,289,307`), L2 korona (bez najemnikow), L5b-1 (komunikat "pass to the treasury") | bez wyjatku |
| Gracz-krol | W6b/W5/W7 (wladca pominiety), L5b-1 (skarbiec jego krolestwa), MercTenure (AI-najemnicy w jego krolestwie dostaja pole ShouldStayInKingdomUntil, jego zwolnien gra tym nie blokuje) | bez wyjatku; pkt 6 (komunikat po cudzej grabiezy) |
| Gracz w niewoli | N7 (MainParty nieczynna -> nie odbiera; grupy czekaja do 30 dob), W6b (MainParty pominieta w NaParties), PeaceHold (gracza nie uwalnia - jak gra), RansomRepeat (PlayerPrice bez zmian) | bez wyjatku |
| Gracz bez krolestwa | W6b (warunki nie), L5b-1 (skarbiec do kiesy gracza, `CapitalTreasury.cs:229,249-256`), N7 (zbiorka: najblizsze niewrogie miasto) | bez wyjatku |
| Rozwiazanie druzyny rodu gracza / karawana gracza | N7 (`IsDisbanding` -> nieuzywalna), CaravanFlow idle (gracz pominiety `CaravanFlow.cs:365`), L5b-2 (karawana gracza w zdobytym miescie zajeta + komunikat) | bez wyjatku |
| Gracz otwiera "People of this land" | GL-1, P2, P3/P5, N7, L5 | bez wyjatku |

## 8. Wydajnosc (linie "Profil doby (E8/0)", mediana pierwszych 119 dob kazdego biegu)

| | przed noca (15-02-59) | czesc paczek (19-21-12) | wszystko poza 5b (20-41-38) |
|---|---|---|---|
| takt doby (nasze w OnDailyTick) | 339 ms | 346 ms | **376 ms (+37 ms, +11%)** |
| suma godzin w dobie | 266 ms | 264 ms | 266 ms (bez zmian) |
| "nasze razem" (dolna granica) | 1213 ms | 1222 ms | 1240 ms (+27 ms) |
| SupplyDemand.DailyTrade | 32.2 ms | 35.9 ms | 52.7 ms |
| BuildFunding.Daily | 22.1 ms | 23.5 ms | 25.3 ms |

- Najwiekszy przyrost, +20 ms w SupplyDemand.DailyTrade, to ZACHOWANIE, nie koszt latek. Wywoz kupcow urosl 2.2 razy
  (d108900: "nowa droga" 4134 -> 9057, "popyt z pamieci" 82k -> 190k) przez rynki wedlug ludzi (GL-2) i zbytki.
- Wlasne pomiary nowych paczek (mediana dobowa): Rynek od ludzi GL2 5.5 ms; Roboty ludu P2 1.8 ms (maks. 4.3); Zbytki P3-P5
  1.4 ms doby + 1.5 ms szacunku popytu (19 068 wywolan latki na dobe = ok. 80 ns na wywolanie); Kiesa ludu "doba" 1.4 -> 3.0 ms;
  N7 0.44 ms; P1b 0.04 ms; Konie robocze 0.1 ms; PeaceHold.Hourly ok. 1 ms na dobe (w profilu godzin); lup L0-L7 i L5b
  tylko przy zdobyciu.
- Nic nie biegnie co klatke. Jedyny nowy tik godzinowy to PeaceHold.Hourly (pusta lista prawie zawsze). Latki na metody
  wolane czesto (`CalculateTownFoodStocksChange` przez `Town.FoodChange` w AI, `GetMilitiaLimit` BK,
  `GetEstimatedDemandForCategory`, `RefreshSettlementTooltip`) to kilka porownan i ewentualnie `ExplainedNumber.Add`.
  Postfiks L5 przy kazdym odswiezeniu menu lupu buduje string klucza i liczy podglad 3 razy, co jest pomijalne.
- Wyjatkow w logu 20-41-38: 0 (poza 3 kontrolnymi wpisami drogi bledu).

## 9. Co sprawdzic w pierwszej godzinie kampanii Jeffa (gdyby autotestu z pkt 2 nie bylo)
1. Szturm miasta z karawana wroga: predkosc druzyny po wyjsciu (pkt 1), linie L5 w logu, brak potkniec w "Lup miast" i "Karawany w zdobytym miescie (L5b-2)".
2. Zamek z lennem + wojna: po 1 i 8 dobach komunikaty "Scutage" / "War aid"; w linii W6 "gracz: ... warunki tak".
3. Przegrana potyczka: "Stragglers: ... fled toward" i po kilku dobach "rejoined you".
