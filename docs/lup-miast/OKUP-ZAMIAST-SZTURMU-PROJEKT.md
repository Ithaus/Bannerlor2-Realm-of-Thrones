# OKUP ZAMIAST SZTURMU - projekt (13.10.2026, bez kodu)

Status: TYLKO PROJEKT. Decyzja Jeffa 13.10 (pyt. 5b "tak, i tak"): skarbiec korony w stolicy, okup zamiast szturmu, karawany wroga w murach - TAK, jako ostatni
etap. Dwie pierwsze rzeczy sa w kodzie (galaz `w-toku/lup-5b`: L5b-1 `CapitalTreasury.cs`, L5b-2 `EnemyCaravans.cs`); okup wymaga lupu z monety (L2,
galaz `w-toku/lup-miast`) - kod dopiero po scaleniu obu galezi. Oznaczenia jak w `LUP-ZDOBYTEGO-MIASTA-2026-10-13.md`: [K] kod (G = gra 1.4.8, dekompilacja
`dec32\cs_full`), [P] pomiar (bieg roczny kodu 43, `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_15-02-59.log`), [S] szacunek.

## 0. Dla Jeffa - prostym jezykiem

1. Dzis miasto ma dwa wyjscia: wytrzymac albo pasc. Nie moze sie wykupic. Wojsko pod murami tez nie ma wyboru "wezcie zloto i odejdzcie".
2. Propozycja: gdy szturm jest juz blisko, miasto moze zaplacic okup - czesc pieniedzy kupcow i mieszczan. Wojsko bierze zloto, zwija oboz i przez 60 dob
   nie wraca pod to miasto. Miasto zostaje u swojego pana, bez spalonych budowli, bez zabitych i bez grabiezy - ale biedniejsze.
3. Ile: jedna czwarta monety miasta (kasa kupcow + kiesy mieszczan). To mniej niz grabiez (trzecia czesc), bo wojsko nie traci ludzi w szturmie, a miasto
   placi tylko pieniedzmi. Przyklad z biegu: Saltpans przed szturmem mial 176 tys. zl w monecie - okup 44 tys. (grabiez wzielaby 59 tys. i spalila reszte).
4. Kto dostaje: tak samo jak lup - zolnierze 2/3 do sakiewek, wodz 1/3, z niej korona trzecia czesc. Jedna zasada dla lupu i okupu.
5. Kiedy AI przyjmuje: gdy jego wojsko jest w potrzebie (zalegly zold albo kiesa wodza ponizej dwoch tygodni zoldu) albo gdy szturm bylby krwawy (sila
   oblegajacych mniej niz poltora raza ponad obrone murow). Nigdy pod stolica krolestwa (tam ida po koronny skarbiec) i nigdy, gdy okup to mniej niz doba
   zoldu armii. AI-obronca proponuje okup, gdy mury nie wytrzymaja (oblegajacych wiecej niz obroncow liczac przewage murow), a miasto ma z czego zaplacic.
6. Co widzisz: oblegasz - w menu oblezenia nowa opcja "Demand a ransom" i okno z kwota (przyjmij / odrzuc). Bronisz albo jestes panem miasta - okno
   "The besiegers of X demand N denars to lift the siege" (zaplac z kasy miasta / odmow). Nieobecny pan - wiadomosc, ile miasto zaplacilo.
7. Ile to w skali swiata: w roku kodu 43 bylo 34 szturmy i 18 zdobyc; gdyby polowa szturmow skonczyla sie okupem, to ok. 0.4-0.8 mln zl rocznie [S] -
   tyle co lup, nie wiecej. Okup nie jest zrodlem pieniedzy na wojne (jak lup - rozdz. 5.2 projektu lupu); to wybor "zloto teraz albo miasto".
8. Najwieksze ryzyko: menu oblezenia to "menu oczekiwania" - wolanie `SwitchToMenu` z niego konczy sie wywrotka gry (CLAUDE.md rozdz. 7). Dlatego tylko okno
   z pytaniem (popup) i dalej zwykla droga gry.

## 1. Jak dziala oblezenie dzis [K]

- **Poczatek**: armia albo lord z celem `BesiegeSettlement` staje pod murami - `SiegeEvent`, oboz `BesiegerCamp`, przygotowania `SiegePreparations` i machiny
  (G `TaleWorlds.CampaignSystem.Siege\BesiegerCamp.cs:45-47`).
- **Decyzja AI o szturmie**: co mysl AI `MobilePartyAi.GetBesiegeBehavior` (G `...Party\MobilePartyAi.cs:709-718`) - wodz obozu przechodzi na `AssaultSettlement`,
  gdy `BesiegerCamp.IsReadyToBesiege` = przygotowania 100% i `StartingAssaultOnBesiegedSettlementIsLogical` (G `BesiegerCamp.cs:375-430`): stosunek sily
  oblegajacych do sily obroncow x przewaga osady^w (w = 0.8, po 4 dobach maleje o 0.02 na godzine, x1.25 bez taranu, x1.25 bez wiezy) musi byc > 1; potem
  kara za mury i machiny obroncy i los: szansa = 10% stosunku na kazda mysl. Pusta warownia - szturm od razu.
- **Szturm**: `EncounterManager` (G `...CampaignSystem\EncounterManager.cs:246-270`) -> `StartBattleAction.ApplyStartAssaultAgainstWalls`; wygrana ->
  `KingdomManager.SiegeCompleted` -> zmiana pana (G `KingdomManager.cs:239-278`) -> nastepstwa (lup, L0-L7 w galezi `w-toku/lup-miast`).
- **Gracz-oblegajacy**: menu oczekiwania `menu_siege_strategies` (G `...CampaignBehaviors\SiegeEventCampaignBehavior.cs:243-248`): "Lead an assault",
  "Send troops", "Leave". **Gracz-obronca**: w tym samym menu "Request a parley" -> `request_meeting_with_besiegers` (rozmowa - dzis tylko bezpieczne przejscie).
- **Liczby roku** [P]: 829 dob oblezen twierdz (miasta 205, zamki 624; srednio 2.3 naraz - linia "Oblezone (E5/A4)"), 34 szturmy (`bitwy.log` B175 "Siege"),
  18 zdobyc, 0 odpartych, 16 szturmow bez rozstrzygniecia; oblezona osada nie placi panu ani koronie (BesiegedPay) - pieniadz lezy w kasie.

## 2. Regula

| Co | Ustalenie (wybor autora) | Dlaczego |
|---|---|---|
| Z czego | kasa kupcow (`Town.Gold`) i kiesy ludu W, R, P, gmina (zamek: kasa + podzamcze) - te same zbiorniki co lup L2, przez te sama ksiege kies (`TownFolk.SackTake`) | jedna zasada "lup bierze to, co lezy"; kontrola kies r = 0 |
| Ile | okup = floor(1/4 x moneta osady); nie mniej niz doba zoldu armii (inaczej AI odmawia), nie wiecej niz 1/3 | mniej niz grabiez (1/3), bo bez strat w szturmie i bez ognia |
| Komu | jak lup L2: druzyny armii wedlug ludzi, w druzynie 2/3 sakiewki, 1/3 wodz, korona 1/9 (prawo trzecich) | jedna zasada z lupem |
| Kiedy proponuje AI-obronca | od 2 doby oblezenia, raz na dobe, gdy stosunek sily z `StartingAssaultOnBesiegedSettlementIsLogical` > 1 (mury nie wytrzymaja) i moneta >= 4 x doba zoldu armii | miasto placi, gdy i tak padnie |
| Kiedy przyjmuje AI-oblegajacy | armia w potrzebie (ta sama co L4: zalegly zold, kiesa wodza < 14 dob zoldu) ALBO stosunek sily < 1.5 (szturm krwawy); NIE pod stolica przegranego (L5b-1), NIE gdy Inni, NIE w oblezeniach fabuly ROT | zachowanie z biezacej potrzeby (zasada Jeffa) |
| Potem | oboz zwiniety droga gry (`SiegeEvent` konczony jak przy odejsciu oblegajacych), rozejm: frakcja oblegajacych nie oblega tej osady 60 dob (pole zapisu: osada -> frakcja -> doba) | bez rozejmu oblezenie wracaloby nazajutrz |
| Zamek | ta sama regula (kasa zamku + podzamcze) | 624 z 829 dob oblezen to zamki |
| Laska po okupie | brak blizny, brak zabitych BK, budowle stoja, pan ten sam | to cena okupu |

Dowod obiegu: kasa i kiesy osady -> sakiewki, kiesy wodzow -> (nastepnego ranka) korona 1/9. Zdjete = dopisane w linii; "Pieniadz swiata" bez zmian; okno O10
nie dotyczy (bez nastepstw). Nic z niczego, nic w nicosc.

## 3. Co widzi gracz (napisy po angielsku)

- Oblegajacy (wodz): opcja "Demand a ransom" w `menu_siege_strategies` (tylko gdy AI-obronca zgodzilby sie zaplacic): okno `InformationManager.ShowInquiry`
  "The people of {TOWN} offer {N} denars to see your army gone. Your men would get {M}, you {K}. Take the ransom and lift the siege for 60 days?" - Accept / Refuse.
- Obronca (gracz w miescie albo pan osady): okno "The besiegers of {TOWN} demand {N} denars from its merchants and townsfolk to lift the siege." - Pay / Refuse.
  Gracz nieobecny: wiadomosc "{TOWN} paid a ransom of {N} denars; the enemy has lifted the siege."
- Gracz w cudzej armii: wiadomosc z czescia jego druzyny (jak L2).

## 4. Pulapki i ryzyka

1. **GameMenu**: `menu_siege_strategies` to menu oczekiwania - zadnego `SwitchToMenu` z opcji ani z okna (CLAUDE.md rozdz. 7: CTD w `GameMenuVM.OnFrameTick`).
   Okno popup, a po "Accept" tylko akcje gry (koniec oblezenia); menu zmienia sama gra, jak przy "Leave". Test reczny obowiazkowy (autotest nie widzi gracza).
2. **Wojna bez zdobyc**: AI placi i placi - mniej zdobyc. Prog O-4. Rozejm 60 dob i kurczaca sie moneta miasta to hamulec.
3. **Petla gracza**: oblegaj - wez okup - wroc po 60 dobach. Moneta miasta po okupie odrasta tygodniami (rozdz. 3.6 projektu lupu), wiec kolejny okup mniejszy.
4. **Kolizje**: L2 (te same zbiorniki - okup i lup nigdy w tej samej dobie: po okupie nie ma szturmu), L4 (ta sama "potrzeba armii" - jedna funkcja), BesiegedPay
   (pieniadz wstrzymany w oblezeniu jest czescia okupu - slusznie), Diplomacy (rozejm to nie pokoj - nie wolac jego akcji), BK (stabilnosc - bez zmian), ROT
   (oblezenia skryptowe, np. Harrenhal - wykluczone).
5. **Prog 85 tys.**: okup to zold dla zolnierzy oblegajacych (sakiewki) - wojska nie ubywa; obronca traci monete miasta, nie skarbiec. Sprawdzic progiem O-5.

## 5. Paczki i progi

| # | Paczka | Tor | Tresc |
|---|---|---|---|
| O0 | pomiar na sucho | S | linia przy kazdej dobie oblezenia: moneta osady, okup wedlug reguly, stosunek sily, "AI-obronca zaproponowalby / AI-oblegajacy przyjalby", wynik oblezenia |
| O1 | okup AI - AI | M | regula 2 bez gracza; rozejm 60 dob (jedno pole zapisu przez SaveText.Sync) |
| O2 | gracz-obronca | M | okno popup, wiadomosc nieobecnego pana |
| O3 | gracz-oblegajacy | M | opcja "Demand a ransom" + okno; test reczny |

Kolejnosc: po scaleniu `w-toku/lup-miast` (L2, L4) i `w-toku/lup-5b`. Klucze `SiegeRansom*` domyslnie wlaczone, wylaczone = stan sprzed paczki.

| # | Prog | Wartosc |
|---|---|---|
| O-1 | linii pomiaru = dob oblezen z linii "Oblezone (E5/A4)" | rowne |
| O-2 | kazdy okup: zdjete z kasy i kies = dopisane sakiewkom, wodzom, kiesom druzyn; kiesy ludu r = 0 | 0 zl |
| O-3 | oblezenie tej samej osady przez te sama frakcje w 60 dob po okupie | 0 |
| O-4 | zdobycia twierdz w roku wobec biegu bez paczki | >= 50% (ponizej - DECYZJA) |
| O-5 | druzyny lordow krolestw w wojnie (miara W5-7), bieg roczny | >= 85 tys. |
| O-6 | okup pod stolica, przez Innych, w oblezeniu fabuly ROT | 0 |
| O-7 | ERROR i potkniecia; wywrotki w menu (test reczny: zamek, oblegajacy i obronca) | 0 |

Czego nie wiem: jak czesto AI-obronca bylby gotow placic (mierzy O0); ile dni trwa dzis oblezenie zakonczone szturmem (log ma doby oblezen, nie
poczatek i koniec kazdego); czy Diplomacy albo BK reaguja na koniec oblezenia bez bitwy (do sprawdzenia w dekompilacji przed O1).

## 6. Sprawdzenie 14.10 (bez kodu)

Drzewo `w-toku/okup` (od `w-toku/zbior-e8` 449d453: lup L0-L7, LB, L5b-1, L5b-2, Z.1, plotno, drobne14). Logi: dwuletni `Armoury-2026-10-13_21-14-13.log`
(728 dob, 108837-109564) i W52 `Armoury-2026-10-14_02-50-28.log` (120 dob), oba z tego samego zapisu, z `Logs\<data>\bitwy.log` (B175). Gra `dec32\cs_full`,
BK `e4tools\bkfull`. Z.1 (zold w pokoju) i plotno nie dotykaja okupu: oblezenie jest tylko w wojnie, a monete osady czytamy w chwili decyzji.

### 6.1 Miejsca kodu z projektu [K]

- `CitySack` - jest, klasa czesciowa w 9 plikach (`CitySack.cs` L0 + Read/RuleTake/Recipients/NeedOf/Rule34, `.Capture` L1, `.Coin` L2, `.Window` L3, `.Ai` L4,
  `.Menu` L5, `.People` L6, `.Goods` L7, `.Burn` LB). Do uzycia: `Read(st)` (Kasa, Folk[4], Castle), `NeedOf(a)` (ArmyWage = doba zoldu armii, InNeed, LeadDays;
  prog 14 dob - `NeedDays`, private const), `Recipients(attacker, contrib)` (wymaga slownika druzyn - bez niego tylko `attacker`), `CrownPart`, `PartText`, `N`.
  Podzial puli (2/3 sakiewki, 1/3 wodz, korona) jest dzis WPISANY w prywatna `Sack()` (`CitySack.Coin.cs:84-138`) - do wyjecia jako wspolna funkcja.
- Moneta osady = `Town.Gold` (cala, z zapasem kupcow) + kiesy ludu przez `TownFolk.SackRead / SackTake / SackBack` (`TownFolk.Sack.cs`; zamek - podzamcze przy
  CastleLive). `TownPurse` (E5/A1) jest prawdziwy, ale lup z niego nie korzysta (bierze cala kase) - okup tak samo. Komentarz w `BesiegedPay.cs` o zaslepce
  `TownPurseA4Stub` jest nieaktualny (zaslepki nie ma) - bez wplywu.
- `Ransom178` - jest: `NoteThird(h, x)`, `WouldTax(h)`, `NoteBeatenGold`; `CrownThirdsShare` 0.111 (domyslna; w Armoury.json Jeffa klucza nie ma).
- `CapitalTreasury` - jest, ale `CapitalOf(Kingdom, out src)` jest PRIVATE - okup potrzebuje `internal static bool IsCapital(Settlement)`.
- `EnemyCaravans` - jest (L5b-2 przy zdobyciu + diagnoza drobne14: nasluch `OnSiegeEventStarted/Ended`); koniec oblezenia okupem tylko zdejmuje jej spis - bez kolizji.
- `BesiegedPay` - jest (R22: oblezona osada nie placi renty, polityk, zaulkow, podatkow ludu) - wstrzymany pieniadz lezy w monecie i wchodzi w okup (jak rozdz. 4 pkt 4).
- Gra: `menu_siege_strategies` (`SiegeEventCampaignBehavior.cs:243-248`), "Request a parley" -> `request_meeting_with_besiegers` (:246, :533), `IsReadyToBesiege`
  (`BesiegerCamp.cs:49-58`), `StartingAssaultOnBesiegedSettlementIsLogical` (:375-430), `GetBesiegeBehavior` (`MobilePartyAi.cs:709-718`) - nazwy i linie zgodne.
  `IsReadyToBesiege` ma JEDNEGO wolajacego (MobilePartyAi :714, wodz obozu, mysl AI); BK go nie wola.
- Nowe klucze `SiegeRansom*` - bez kolizji (w Settings sa `Ransom*` paczki 178 - nazwy w MCM musza mowic "Siege Ransom").

### 6.2 Liczby na logach [P]

| Co | Dwuletni (728 dob) | W52 (120 dob) |
|---|---|---|
| doby oblezen (linia "Oblezone (E5/A4)") | 1 781: miasta 451, zamki 1 330; srednio 2.45 naraz, max 9 | 267: miasta 53, zamki 214; 2.23, max 6 |
| szturmy (B175 "Siege") | 67 (pod miastami 21), zdobyte 38, odparte 0, bez rozstrzygniecia 29 (9 z nich: oblegajacy "partii 0 ludzi 0") | 13, zdobyte 8, bez rozstrzygniecia 5 |
| straty oblegajacych w szturmach | 37 811 ludzi, mediana 490 na szturm (20% armii) | 7 616, mediana 520 (29%) |
| wypady obroncow | 78 - w 77 obroncy rozbili oboz (oblezenie bez zdobycia), 1 zdobycie wypadem | 9 - wszystkie wygrane przez obroncow |
| bitwy pod murami | 52 (strona atakujaca wygrala 10) | 1 |
| zdobycia z linia L0 | 39 (miasta 12, zamki 27); wybor gry: laska 18 (ta sama kultura 8), grabiez 14, spustoszenie 7 | 8 (1 miasto, 7 zamkow) |
| moneta osady przed lupem, mediana | miasta 292 tys. (155-707; kupcy 96, lud 185), zamki 62 tys. (31-466) | Starfall 328 tys.; zamki 50 tys. (42-69) |
| okup 1/4, mediana (doby zoldu armii) | miasta 73 tys. (6.7 doby; zold 10.7 tys./d, 3.6 tys. ludzi), zamki 15.6 tys. (2.2 doby; zold 8.2 tys./d) | miasto 82 tys. (9.5), zamki 12.5 tys. (2.0) |
| okup ponizej doby zoldu | 5 z 39 (1 miasto, 4 zamki) | 0 z 8 |
| "armia w potrzebie" (NeedOf, L4) | 1 z 39; kiesa rodu wodza 30-672 dob zoldu druzyn rodu (prog 14) | 0 z 8 (45-185 dob) |
| upadki stolic (L5b-1) | 3 z 39 zdobyc: Dragonstone town_EW1 (nadwyzka skarbca ujemna - 0 zl), Mantarys town_K2 (257 tys.), Oros castle_K1 (164 tys. - stolica BK bywa zamkiem) | 0 |

- Skala: 1/4 monety przy wszystkich 39 zdobyciach = 1.75 mln zl w 2 lata; lup L2 faktycznie wziety 1.92 mln (grabiez 0.88, spustoszenie 1.05, laska 0). Srednio
  okup 45 tys. wobec lupu 49 tys. na zdobycie - okup nie jest nowym zrodlem pieniedzy; pkt 7 rozdz. 0 ma gorna granice ok. 0.9 mln/rok (gdyby kazde zdobycie bylo okupem).
- Powroty tej samej frakcji pod te sama osade: 12-256 dob (castle_ES2 Qohor 3 szturmy w 41 dob, town_K2 Volantis 3 w 69 dob, castle_K7 i castle_EN9 po 12 dobach) -
  bez rozejmu armia po okupie wracalaby; 60 dob zostaje.
- Odrost monety po lupie (linia "L0 po nastepstwach", 30 dob): zamki prawie nie odrastaja (ROT_castle33 35 -> 35 tys., ROT_castle69 32 -> 32 tys., ROT_castle16
  34 -> 41 tys.), miasta roznie (ROT_town9 87 -> 510 tys., town_S5 104 -> 253 tys., ROT_town21 123 -> 105 tys.) - drugi okup zamku po 60 dobach mniejszy (hamulec
  petli gracza z rozdz. 4 pkt 3), miasta - nie zawsze.
- Czego log nie ma: stosunku gry `StartingAssault...` (B175 "stosunek sil" to moc bez przewagi murow - mediana 5.2, nie nadaje sie do progu 1.5) ani poczatku
  i konca kazdego oblezenia - mierzy O0.

### 6.3 Okno gracza bez SwitchToMenu [K]

- Gra sama wola `SwitchToMenu` z opcji tego menu ("Lead an assault" :295, "Leave" :493, "Request a parley" :533) i z jego ticku (:259-278), ale nasz kod trzyma
  sie CLAUDE.md 7: ani opcja, ani okno, ani ich wywolania nie zmieniaja menu i nie koncza oblezenia.
- Wzor w repo: E7/R2 `DistrictRaid.MenuTickPostfix` (i NightRest): flaga -> okienko `InformationManager.ShowInquiry` z pauza z postfiksu ticku menu oczekiwania
  (`IsAnyInquiryActive` przed ponownym otwarciem, licznik nieudanych prob) -> wywolanie okna ustawia TYLKO wybor -> wykonanie w nastepnym ticku kampanii.
- Gracz-oblegajacy (O3): opcja "Demand a ransom" w `menu_siege_strategies` (AddGameMenuOption przy OnSessionLaunched, indeks przed `menu_siege_strategies_leave` -
  wzor `BeeGifts.AddGiftOption`). Widoczna dla strony Attacker; czynna, gdy gracz dowodzi oblezeniem i przygotowania sa gotowe (ten sam warunek co "Lead an
  assault", :430-457) i gdy obronca by zaplacil; inaczej `IsEnabled = false` z dymkiem (np. "Their walls still hold - they will not pay."). Konsekwencja = flaga.
  Okno z postfiksu `game_menu_siege_strategies_on_tick`. Po "Accept" wykonanie w ticku kampanii; menu zmienia gra: `GetGenericStateMenu`
  (`DefaultEncounterGameMenuModel.cs:226`) przestaje dawac `menu_siege_strategies`, gdy `MainParty.BesiegerCamp == null`, a on_tick gry robi `ExitToLast` (:270) -
  ta sama droga, co gdy wodz AI zwija oboz z graczem w armii.
- Gracz-obronca (O2): zadanie przychodzi w chwili decyzji AI o szturmie (6.4 P1) - kolejka; postfiks `IsReadyToBesiege` daje false, dopoki gracz nie zdecyduje.
  Okno: gracz w osadzie - ten sam postfiks on_tick (gra daje mu `menu_siege_strategies`); gracz na mapie - nasluch `CampaignEvents.TickEvent` przy czynnym MapState bez
  spotkania. "Pay" -> wykonanie w ticku; gracza w miescie przelacza gra (`SiegeEvent.FinalizeSiegeEvent` :887-893 -> `PlayerSiege.OnSiegeEventFinalized` :83-95,
  menu "siege_attacker_left") - jak przy zwyklym odejsciu oblegajacych. "Refuse" -> szturm przy nastepnym losowaniu gry; jedno zadanie na oblezenie.
- "Request a parley" (rozmowa gry) - bez zmian; wiersz okupu w rozmowie mozliwy pozniej, poza O2.

### 6.4 Poprawki projektu (wiazace dla O0-O3)

- **P1 Chwila.** Zamiast "obronca proponuje od 2 doby raz na dobe": jedna chwila dla AI i gracza-obroncy - decyzja gry o szturmie (postfiks `IsReadyToBesiege` =
  true po losowaniu gry). W tej chwili stosunek gry jest > 1 z definicji (mury nie wytrzymaja), wiec warunek obroncy sprowadza sie do "jest z czego zaplacic".
  Gracz-oblegajacy - gdy moglby poprowadzic szturm i ta sama formula gry bez losowania daje > 1.
- **P2 Kiedy przyjmuje AI.** "Armia w potrzebie" wypada 1 raz na 39 - sama nie da okupow. Zostaje (ta sama `NeedOf` co L4) i dochodzi powod z biezacej sytuacji:
  ta armia (frakcja) miala juz w tym oblezeniu szturm bez rozstrzygniecia (20 prawdziwych w 2 lata, mediana strat ok. 500 ludzi) - druga proba to okup.
  "Stosunek < 1.5" zostaje, ale liczony formula gry bez losowania (`BesiegerCamp` :375-430) i najpierw zmierzony w O0; wartosc progu - decyzja autora po O0.
- **P3 Ile.** floor(1/4 monety) zostaje. "Nie wiecej niz 1/3" - skreslic (martwe). "Moneta >= 4 x doba zoldu" (obronca) i "okup >= doba zoldu" (oblegajacy) to
  ten sam warunek - jeden zapis: okup < doba zoldu armii (`NeedOf.ArmyWage`) -> okupu nie ma (5 z 39 zdobyc).
- **P4 Komu.** Nie "druzyny armii", tylko wszystkie druzyny obozu (`BesiegerCamp`, takze lordowie spoza armii) wedlug ludzi - inaczej po odejsciu armii oboz trwa
  z nowym wodzem (`BesiegerCamp.cs:224-251`). Podzial z `CitySack.Sack` wyjac do wspolnej funkcji (jeden kod lupu i okupu: MenPurse 2/3, wodz 1/3 z
  `Ransom178.NoteThird` / `ClanIncomeBook.KThird`, reszta zaokraglen do najwiekszej, zwrot do zbiornikow przy wyjatku). Inni - bez udzialu, same Inni - bez okupu.
- **P5 Koniec oblezenia.** `BesiegerCamp.RemoveAllSiegeParties()` (gra :254 - kazda druzyna `BesiegerCamp = null`, przy zerze `FinalizeSiegeEvent` ->
  `OnSiegeEventEnded`) tylko z ticku kampanii, tylko gdy `st.Party.MapEvent == null` i zaden oblegajacy nie jest w bitwie (inaczej FailedAssert gry :259).
  Nigdy z mysli AI, z opcji menu ani z wywolania okna.
- **P6 Rozejm 60 dob.** AI: postfiks `TargetScoreCalculatingModel.GetTargetScoreForFaction` (wola go `AiMilitaryBehavior.cs:459`) = 0 dla (osada, frakcja) przy
  `ArmyTypes.Besieger`. Gracz: ta sama regula - opcja oblezenia tej osady nieczynna z dymkiem. Kolizja BK: `ArmySiegePersistencePatches` (postfiks
  `AiPartyThinkBehavior.PartyHourlyAiTick`, `ArmySiegePersistence` = true w BannerKings.json Jeffa) trzyma armie, ktorej oblezenie "zniknelo z tablicy", w miejscu
  do 24 h (SetMoveModeHold co 2 h) - po okupie wyczyscic jej wpis albo przyjac postoj; liczyc w O1.
- **P7 Stolica.** "Nigdy pod stolica" zostaje (3 z 39 zdobyc); wymaga `CapitalTreasury.IsCapital` (6.1); stolica BK bywa zamkiem.
- **P8 Fabula ROT i Inni.** Armoury nie rozpoznaje oblezen fabuly (umie to tylko CrashScribe `Mends.cs` - `ROTSettlements.Harrenhal`, `HarrenhalSiegeEvent`) -
  O1 potrzebuje wlasnej listy osad fabuly (Harrenhal, Mur i zamki Strazy - id do sprawdzenia w XML ROT) i `Undead.Party` dla kazdej druzyny obozu.
- **P9 Zapis.** Rozejm we wlasnym kluczu `SaveText.Sync` (np. `arm_siege_ransom`), nie w `arm_sack`: Import `arm_sack` odrzuca nieznana wersje w calosci
  (`CitySack.Window.cs:159`) - cofniecie DLL zgubiloby caly stan lupu.
- **P10 Ksiega.** Nowa nazwana pozycja `MoneyLedger` obok `NSack` (10) - okup osobno od lupu w "Przeplywy osad"; kiesy ludu przez `SackTake` (r = 0 bez zmian).
- **P11 Gracz-obronca.** Okno, gdy gracz jest w osadzie albo rod gracza jest jej panem (decyduje pan); osada pana AI - wiadomosc dla gracza z tego krolestwa;
  okno niemozliwe przez 24 h (misja, bitwa, rozmowa) - regula AI-obroncy i wiadomosc. Gracz w cudzej armii oblegajacej - wiadomosc z czescia jego druzyny (jak L2).
- **P12 Liczby rozdz. 0-1.** Rok kodu 43 zastapic tabela 6.2; "0 odpartych" dotyczy tylko szturmow - 77 oblezen w 2 lata skonczyl wypad obroncow. Przyklad
  Saltpans -> mediana miasta 292 tys., okup 73 tys.
- **P13 Prog O-4.** 38 zdobyc w 2 lata (8 w 120 dob) - porownanie dwoch biegow jest za glosne; O-4 liczyc na tym samym biegu z O0/O1 ("zdobycia zamienione
  w okup / decyzje o szturmie"), potwierdzic biegiem dwuletnim. O-1 zmienic: linie O0 = decyzje gry o szturmie (nie doby oblezen).

### 6.5 Paczki po sprawdzeniu

| # | Tor | Klucz | Tresc i progi |
|---|---|---|---|
| O0 | S | `SiegeRansomMeasureLog` | postfiks `IsReadyToBesiege` tylko liczy (wynik bez zmian): linia "Okup (O0)" przy kazdym true - osada, moneta (`Read`), okup 1/4, doba zoldu, stosunek gry bez losowania, potrzeba, wczesniejszy szturm bez rozstrzygniecia (migawka z `OnMapEventEnded`), stolica / fabula / Inni / gracz, "obronca zaplacilby / AI przyjalby"; linia doby. Prog: kazdy szturm B175 ma wczesniej linie O0 |
| O1 | M | `SiegeRansomAi` | P1-P10 bez gracza; progi O-2, O-3, O-6, O-7 + "oboz osady pusty po okupie" (0 druzyn) + licznik BK SIEGE-STICK po okupie; bieg 120 dob, potem dwuletni (O-4, O-5) |
| O2 | M | `SiegeRansomPlayerDefender` | P11 i 6.3 (okno z ticku, wykonanie w ticku); test reczny: gracz w miescie, gracz na mapie, odmowa |
| O3 | M | `SiegeRansomPlayerBesieger` | opcja + okno (6.3), rozejm gracza (P6); test reczny: zamek i miasto, gracz sam i jako wodz armii |

Kolejnosc O0 -> bieg 120 dob (decyzja progu "stosunek" z P2) -> O1 -> O2 -> O3. Wszystkie klucze domyslnie wlaczone (decyzja Jeffa), wylaczone = 449d453.

Czego dalej nie wiem: rozklad stosunku gry w chwili szturmu (O0); czy Diplomacy reaguje na koniec oblezenia bez bitwy (dekompilacji Diplomacy nie ma w
scratchpadach - sprawdzic przed O1); czy w menu gracza-oblegajacego `PlayerEncounter` jest czynny (`LeaveSiege` gry wola wtedy `PlayerEncounter.Finish`, :459-470 -
sprawdzic przed O3); czy on_tick menu oczekiwania biegnie na pauzie (gdy nie - okno pojawi sie po wznowieniu czasu; test reczny).
