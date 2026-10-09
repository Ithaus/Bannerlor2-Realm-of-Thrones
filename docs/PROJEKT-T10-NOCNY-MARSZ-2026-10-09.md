# PACZKA T10 - NOCNY MARSZ AI (projekt dla wykonawcy, 09.10.2026; poprawiony po krytyce i wykonany)

Status: PROJEKT POPRAWIONY PO KRYTYCE (rozdz. 10 "Krytyka i odpowiedzi") i WYKONANY w drzewie `SCR\noc2\t10` - commit lokalny cc0af2f (galaz `w-toku/t10-nocny-marsz`), wpis
w CHANGELOG.md drzewa (T10, NIEWGRANE). Release zbudowany (kod 0), takze wariant na sucho (`-p:T10Dry=true`). Gra i autotest nieuruchamiane,
folder gry nietkniety, nic nie wgrane, nic nie wypchniete. Dopisek paczki MUSZTRA - na koncu (rozdz. 11) z odpowiedzia.
DRUGA RECENZJA (24 uwagi, 1 krytyczna) - sprawdzona i wykonana: rozdz. 10b, commit lokalny bcae98d w tym samym drzewie (wpis CHANGELOG T10-p, NIEWGRANE);
DLL probny `SCR\test\Armoury-t10.dll`.

Decyzja Jeffa (STAN-PRAC, "DECYZJE JEFFA 09.10 ok. 04:00", pkt 1): samotne kolumny lordow **"maszeruja noca, gdy trzeba - spiesza sie, by przerwac
rabunek, uciekaja przed armia - ale maja miec takie same kary jak gracz"**. Stale zasady: jedna regula dla gracza i AI, nic z niczego, regula obejmuje
100% (nie ulamek), parametry dobiera projekt.

Baza kodu: drzewo `SCR\noc2\t10` = galaz `noc/sklad6` 0c41eee (= Armoury w grze 17a700d7 + 171/172/172b/174), w nim T1 (oboz swiata 0-6, wodz armii
zawsze obozuje). SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`,
dekompilacje: gra `SCR3\ore-supply\cs`, BK `SCR3\ore-supply\bk` (SCR3 = `...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`). Sciezki `[K]` bez
przedrostka = `t10\Armoury\src`. Numery linii "przed T10" = stan 0c41eee; nowy kod opisany nazwami metod (NightMarch.cs, NightRest.cs).
Podstawa: audyt `docs/audyt-2026-10-09/07-mapa-czas-obozy.md` (dalej A07: 1.5, 1.6, 3.8, 3.9, 3.10, 3.11, O-4, O-7), ESENCJA pyt. 18.
Oznaczenia: [K] kod, [P] pomiar z logu autotestu, [S] szacunek z rachunku, [Sym] symulacja arytmetyki ksiegi (Python, ta sama regula co kod).

---

## 0. Dla Jeffa - prostym jezykiem

1. Dzis noca idzie ok. 15% samotnych lordow, ale nie dlatego, ze musza - to czysty los: kazdy lord co 100 dni idzie **15 nocy z rzedu** bez snu
   i nic za to nie placi, a Ty po jednej takiej nocy masz -25% predkosci i morale, a po trzech Twoje wojsko pada.
2. Od teraz samotny lord (i wodz armii) idzie noca tylko z trzech powodow: **ucieka** (gra sama uznala, ze trzeba uciekac - np. przed armia),
   **goni wroga** - lorda, armie albo bande, ktora wlasnie kogos napada - ktorego dogoni jeszcze tej nocy (karawany i chlopow noca nie goni: to lup,
   nie potrzeba), albo **spieszy na odsiecz** swojej wsi, ktora **wlasnie** lupia (i dojdzie przed poludniem).
   W kazdym innym wypadku spi 0-6, a lord w miescie, zamku albo wsi zostaje tam do rana (we wsi budzi go silniejszy wrog, ktory idzie na niego).
3. Gdy do spiacego obozu podchodzi **silniejszy** wrog, ktory nie spi i idzie na niego - lord sie budzi: jesli trzeba, ucieka, jesli nie - kladzie
   sie z powrotem. Slabsza banda obok juz nikogo nie budzi (dzis budzila nawet armie).
4. Kto nie przespal 6 godzin, placi **dokladnie jak Ty**, ta sama ksiega snu: -25% predkosci i morale po jednej nocy, -40% po dwoch, zapasc
   (-90% predkosci, -95% morale) po trzech - kazda partia lorda AI, takze armia, ktora noca goni albo ucieka. Dezercja przy niskim morale: Twoje
   partie i partie Twoich towarzyszy uciekaja wedlug progow wedlug poziomu (rekruci juz ponizej 25 - czyli moga ich tracic przy 1-2 nocach dlugu),
   lordowie AI na razie wedlug gry (ponizej 10, czyli przy zapasci); Twoja decyzja z 04:25 ("dezercja wedlug poziomu takze u AI - TAK", paczka K1b)
   zrowna to w jedna regule.
5. I splaca tak, jak Ty bys to zrobil recznie: lord z jedna zarwana noca rozbija oboz juz o **20:00** (10 godzin snu, splata wymaga 9), a kolumna
   "slaniajaca sie" (2-3 noce) spi tam, gdzie stoi, jak Ty w menu snu: przy 2 nocach ok. doby, przy 3 ok. 29 godzin. **Lord z dlugiem nie goni juz
   noca i nie idzie na odsiecz, bo traci wiecej, niz zyska** - uciekac moze zawsze (wartosc 3 w menu "Ai Nights Awake In Chase" to wylacza).
6. Twoja kara: te same liczby co dotad, ale poprawilem blad w liczeniu - Twoje "-25%" predkosci bylo w praktyce troche wieksze (np. -28%, bo kara
   trafiala pod premie jazdy i terenu); teraz to rowno -25/-40/-90%, dla Ciebie i dla AI.
7. Dla Ciebie: noca spotkasz w drodze duzo mniej lordow; ten, ktory noca przed Toba uciekal, nazajutrz idzie wolniej (w opisie jego predkosci
   i morale zobaczysz "Sleepless nights" - to samo, co widzisz u siebie).
8. Wszystko ma wylaczniki w menu Armoury (wylaczone = stare zasady, a log tylko liczy, co by bylo). Autotest najpierw przebiegnie "na sucho"
   (tylko liczy), potem naprawde - progi testu wezmiemy z liczb, nie ze zgadywania.
9. Trzy pytania na koniec (rozdz. 9): karawany; odsiecz oblezonego zamku i odsiecz armia; czy noc rabunku liczy sie jako sen.

---

## 1. Dlaczego te kolumny ida dzis noca [K] (stan przed T10)

Oboz swiata: `NightRest.AiNightCamp` (przed T10 :601-704), wolany co godzine z `NightRest.OnHourly` (:160, z ArmouryBehavior.cs:634) - **po** wyjsciach
gracza (A07 3.10). W godzinach obozu (T1: ticki 0..5, `InCamp` :199-204) kazda partia lorda i karawana staje (Hold + AI wylaczone na godzine,
:681-684), CHYBA ZE:

| # | Wyjatek | Linia | Kogo przepuszcza | Czy to "gdy trzeba"? |
|---|---|---|---|---|
| W1 | **losowanie**: `(Id + dzien) % 100 < AiCampSkipPercent` (15, takze w Armoury.json Jeffa :290) | :653-656 | samotnych lordow i karawany (wodz armii zwolniony od T1, :653) | **NIE - los.** Reszta z dzielenia rosnie o 1 na dobe, wiec kazda partia trafia w pas 0-14 przez **15 kolejnych nocy co 100 dni** |
| W2 | na morzu | :657 | wszystkich | tak (straze na pokladzie; gracz tez - `SleepAtSeaFree`, Settings.cs:717) |
| W3 | czlonek armii, nie wodz | :658 | doczepionych (ida z wodzem - OK) **i niedoczepionych w drodze na zbiorke** (samotne kolumny - A07 3.9) | doczepieni tak; w drodze na zbiorke - nie |
| W4 | Nieumarli | :659 | Innych | tak (lore) |
| W5 | krotki cel AI `Flee*` albo `Engage*` | :660-661 | uciekajacych i scigajacych - **bez limitu odleglosci** | ucieczka tak; poscig tylko bliski |
| W6 | "wrog blisko": wrogi lord lub banda <= `AiCampDangerRadius` 6 jedn. (ok. 29 km) | :633-638, :663-679 | budzi KAZDEGO, takze przy slabszym albo **spiacym** wrogu (banda 20 ludzi budzi armie); obudzony dostaje rozkaz sprzed snu (:677) i idzie dalej do celu | tylko przy silniejszym, czuwajacym i idacym na lorda (ucieczka) |
| W7 | w osadzie, w bitwie, w obozie obleznikow | :647 | partii w osadzie w ogole nie rusza - AI moze wyjechac o 2:30 i polozyc sie o 3:00 (traci godzine snu) | w osadzie: nie powinien wyjezdzac bez powodu |
| - | **cele AI** (marsz na rabunek, do miasta, na zbiorke, patrol) | - | nie sa wyjatkiem - spia; ale cel nie jest tez nigdzie POWODEM: odsiecz nie ma wyjatku | brakuje "odsieczy" |

Liczby [P] (noc.log, linie `AiNightCamp: h:00 - spi`): srednio na godzine obozu spi lordow 330 (autotest 40 dob 03:48), 336 (02:39), 316 (03:29),
291 (zapis doby 362, 02:52); wodzow armii 4.6-8.5. [S] losowanie W1 przepuszcza ok. 0.15/0.85 x ok. 325 = **ok. 57 samotnych kolumn na godzine nocy**
(ok. 50 na zapisie 362). Ilu idzie z W3/W5/W6 i ilu wyjezdza z osad - dzis nie liczone; T10 liczy to w kazdym trybie (rozdz. 7, na sucho - rozdz. 8 P0).
`AiNightsAwakeInChase` (Settings.cs:727, Armoury.json :292 = 1.0) - martwy klucz przed T10 (A07 3.8); T10 go ozywia (rozdz. 4).

## 2. Jakie kary ponosi gracz za nocny marsz (wszystkie, plik:linia)

| Kara | Kto ja ma dzis | Gdzie | Liczby |
|---|---|---|---|
| **Dlug snu** - doba (swit-swit) bez 6 h odpoczynku = dlug +1 | **tylko gracz** (przed T10) | ksiega: NightRest.cs:106-119 (noc 21:00-swit liczy sie x1, dzien x0.6 - `DayRestFactor`, Settings.cs:715; odpoczynek = osada, postoj (< 0.35 jedn./h), morze), rozliczenie o swicie :159, :938-992, splata od reki :135, :168-177 | baza 6 h (Settings.cs:714); splata 6 + 3x(2xdlug-1) h = 9 / 15 / 21 h (:75-80) |
| -> predkosc | tylko gracz (`mobileParty != MainParty` -> wyjscie, :1006) | `SpeedPostfix` :1000-1015 (od WYNIKU, po suficie MarchPace) | -25 / -40 / -90% (:59) - **w praktyce x (1 + suma wspolczynnikow)**: `Add(-cut)` dokladal do bazy, ktora gra mnozy przez (1 + suma) (ExplainedNumber.cs:156, 228-243), np. -28.5% przy +14% (A07 1.2); MarchPace (:131-135) i Rations licza to dobrze |
| -> morale | tylko gracz (:1023) | `MoralePostfix` :1017-1031 | -25 / -40 / -95% (:60); `AddFactor` - przy sumie wspolczynnikow 0 (morale gra liczy dodawaniem) dokladnie |
| -> zapasc | tylko gracz | :984-990 (kolumna staje raz) | przy dlugu 3 |
| Kara nocna predkosci | **wszyscy** (gracz i AI) | TerrainEase.cs:102-103 (`Campaign.IsNight`, ROT noc 22-02), Settings.cs:324 | -0.5 plasko |
| Krotszy wzrok noca | **wszyscy** | SightRange.cs:34-37 (21-4, x0.65, Settings.cs:754); gra DefaultMapVisibilityModel.cs:21-28 (noc 6 zamiast 12) | jak obok |
| Zasadzka / napad na kolumne noca | **nikt** | brak w kodzie (A07 N-3 - napad na SPIACY oboz, nie zrobiony; CampScene.cs:39 to tylko wyglad bitwy w obozie gracza) | - |
| Niewyspanie w musztrze / zmeczenie | **nikt** (w tym drzewie) | ArmsDrill.cs (171) nie zna marszu ani snu; musztra (paczka MUSZTRA, osobne drzewo) czyta dlug snu - hak w rozdz. 11 | - |
| BannerKings | nikt (kary) | BK ma tylko PREMIE noca (perk OutlawNightPredator, VanillaModelTweakPatches.cs:1078) | - |
| **RBM, DTE, Spoils, ROT, inne** (sprawdzone 09.10: grep metadanych DLL z `Modules` na `get_IsNight`, `get_GetHourOfDay`, `IsDayTime`; trafienia zdekompilowane ilspycmd) | nikt (marszu) | RBM: tylko `RBMCombat/HorseChanges.cs:46` - kon noca -10% w BITWIE (misja, kazdy); Spoils (RealisticLoot): bitwa noca - lup x0.85 i tabor (`RealisticLootModel.cs:541`, `BaggageTrainModel.cs:112, 133`), tylko bitwy gracza; ROT: `MissionPatch.cs:15` (pora dnia sceny bitwy), `ROTDuelsBehavior` (IsNightKingParty - nazwa, nie noc); DTE, StrategicCampaignAI, BetterEconomy, BKROTPatch, ROT_AIInfluence_Compat, RealisticBannerlord: zero odwolan; AIInfluence: odwolanie do `GetHourOfDay`/`IsDayTime` w kodzie zaciemnionym - ilspycmd go nie zdekompilowal, **nie sprawdzone dokladnie** | - |

Wniosek: **gracz ma jedna kare, ktorej AI nie ma - dlug snu (predkosc, morale, zapasc).** Reszta (kara nocna predkosci, wzrok) juz dotyczy wszystkich.
Nie proponuje nowej kary dla obu - wdrazamy dla AI dokladnie ksiege snu gracza (zgodnie z zadaniem: "wdroz tylko to, co juz dotyczy gracza"),
a wzor kary predkosci poprawiamy dla obu (zeby "-25%" znaczylo -25%).

## 3. Regula (wykonana)

**R1 - powod nocnego marszu** (samotna partia lorda AI ORAZ wodz armii). "Samotna" = `IsLordParty`, nie gracz, nie Nieumarli, `AttachedTo == null`
(czyli takze lord w drodze na zbiorke - W3). W godzinie obozu idzie dalej TYLKO z powodem, sprawdzanym co godzine (ticki 0..5, `Classify`):

| Powod | Warunek | Kiedy wolno |
|---|---|---|
| UCIECZKA | `MobileParty.IsFleeing()` - gra sama uznala, ze trzeba uciekac (DefaultMobilePartyAIModel.GetBestInitiativeBehavior) | zawsze |
| POSCIG / PRZECHWYCENIE | krotki albo staly cel `EngageParty`, cel aktywny i wrogi (wojna albo banda) i **wart nocy** (`ChaseWorthy`, T10-p): gracz, partia lorda albo armia, banda w walce (`MapEvent`); karawana, wiesniacy i banda w marszu - nie (lup, nie potrzeba); w zasiegu **L h marszu** (L = dlugosc okna obozu = 6): linia prosta x 1.1 (A07 1.4) / (Speed / 1.2) | dlug < `AiNightsAwakeInChase` |
| ODSIECZ | staly cel `DefendSettlement`, osada tej samej frakcji, **lupiona w chwili ticku** (`Settlement.IsUnderRaid` = `Party.MapEvent.IsRaid`), w zasiegu **2L = 12 h marszu** po siatce osad gry (`MapDistanceModel.GetDistance(partia, osada)` - woda, zatoki, przesmyki; zapasowo 1.1 x linia prosta). Oblezenie (`IsUnderSiege`) i wodz armii - tylko przy `AiNightReliefWider` (pytanie 2, domyslnie wylaczone) | dlug < `AiNightsAwakeInChase` |
| brak | wszystko inne (marsz na rabunek, do miasta, na zbiorke, patrol, odsiecz oblezenia, slabszy albo spiacy wrog obok) | spi 0-6 |

Wodz armii: ta sama regula (bez odsieczy do pytania 2). Spiacy, ktorego **zapamietany przed snem** rozkaz wlasnie stal sie powodem (np. szedl bronic wsi,
a rabunek zaczal sie po polnocy), wstaje z tym rozkazem. Spiacego bez takiego rozkazu nowy rabunek nie budzi (AI wylaczone) - wstanie najpozniej o 6:00;
to najwyzej 6 h z rabunku trwajacego 36-90 h (rozdz. 4). Losowanie W1 dotyczy juz tylko karawan (pytanie 1); przy wylaczonym `AiNightMarchByReason` - stare
reguly T1 w calosci (W1-W7).

**R1a - ALARM** (nie jest powodem marszu, `AlarmThreat`, `GuardAlarmed`): wrog (wrogi lord, banda albo gracz) w `AiCampDangerRadius` (6 jedn.), ktory
- **nie spi**: poza `_camping`, poza snem dluznikow, z wlaczonym AI (`!Ai.IsDisabled`); gracz - nie w obozie i nie w menu snu;
- jest **silniejszy**: sila armii albo partii (`Army.EstimatedStrength` / `Party.EstimatedStrength` - miara gry przy ucieczce, DefaultMobilePartyAIModel.cs:213, 232);
- **idzie na lorda**: jego cel (`ShortTermTargetParty` albo `TargetParty`) to lord, albo odleglosc zmalala od poprzedniej godziny o > 0.2 jedn.
  (pozycje z konca poprzedniego ticku, `SnapshotPositions`); szukanie lokatorem mapy (`StartFindingLocatablesAroundPosition`, jak gra).

Wtedy lord wstaje **bez starego rozkazu** (Hold, AI wlaczone; rozkaz sprzed snu zostaje zapamietany): straznik co 0.1 h gry - ucieczka (`IsFleeing`)
= powod; nowy cel strategiczny AI cofany (Hold); po 0.5 h (2-3 przeglady inicjatywy AI, MobilePartyAi.cs:321: AiCheckInterval 0.25 x 0.6-0.7) bez ucieczki -
z powrotem spac. Wynik w logu: alarm -> ucieczka, alarm -> spi dalej, alarm -> inny marsz (ma byc 0).

**R1b - osada** (`TownNight`): lord w osadzie w godzinie obozu bez powodu zostaje do rana - samo `Ai.DisableForHours(1)`: gra nie wypuszcza z osady partii
z wylaczonym AI (MobileParty.cs:4086-4091, `CheckExitingSettlementParallel`), rozkaz lorda zostaje nietkniety (o swicie jedzie dalej). Z powodem - wyjezdza.
Lorda z AI wylaczonym przez kogo innego (uczta / gentry BK - `DisableAi`) nie ruszamy; osada pod oblezeniem i doczepieni - nie dotyczy.
T10-p: wstrzymany lord, ktoremu w nocy inny mod przejal AI (termin wlaczenia > 1.5 h - `ForeignHold`), zostaje temu modowi. Wies nie jest obronna: lord
we wsi bez powodu dalej zostaje, ale ALARM (R1a: silniejszy, czuwajacy, idzie na niego) daje mu wolne AI - gra ocenia ucieczke (licznik "alarm we wsi").

**R2 - ta sama ksiega snu co gracz, dla 100% partii lordow AI** (samotni, wodzowie, doczepieni; bez Nieumarlych; `AiSleepLedger`). Co godzine, dokladnie
wzorem :106-119: +1 h (noc 21:00-swit) albo +`DayRestFactor` (dzien), gdy partia odpoczywa = w osadzie, albo przesunela sie < 0.35 jedn. od poprzedniej
godziny, albo jest na morzu przy `SleepAtSeaFree`, albo lezy w obozie swiata / snie dlugu (obudzony cudza reka i polozony z powrotem nie traci godziny).
Bitwa, rabunek i oblezenie = partia stoi = odpoczynek - ta sama regula co u gracza (pytanie 3). Splata od reki calej sumy -> dlug 0. O swicie (`PlayerDawn`)
jak `SettleNight`: < baza i bez snu ciaglego -> dlug +1 (maks. 3). Partia bez pelnej doby ksiegi (nowa, < 18 h) nie dostaje dlugu przy pierwszym swicie;
po wczytaniu - kazda ma pelna dobe (zapis trzyma kazdy niezerowy wpis, rozdz. 5). Kary: te same tablice :59-60 we wspolnym `SpeedPostfix`/`MoralePostfix`,
z poprawionym wzorem (rozdz. 2).
T10-p: (a) godzina bez poprzedniego odczytu (pierwszy tick po wczytaniu, nowa partia) = postoj, dokladnie jak gracz (`moved = _hadPos && ...`;
`_hadPos` zerowane w `ResetWorld`, wiec takze drugie wczytanie w sesji) - bez tego wczytanie miedzy 0:00 a 6:00 dawalo setkom lordow dlug 1;
(b) partia chwilowo nieaktywna (rejs statkiem BK: `IsActive=false`) zostaje w ksiedze i liczy sie jak gracz w tym samym rejsie (stoi = odpoczynek),
z ksiegi wypada tylko partia zniszczona; (c) SWIADOME WYJATKI (recenzja 21.1, 21.3): godzina w obozie swiata albo snie dlugu liczy sie jako sen takze
wtedy, gdy cudzy rozkaz (StrategicCampaignAI, AIInfluence) przesunal partie > 0.35 jedn. przed straznikiem - partia spi wedlug naszej reguly, ruch robi
cudza reka; przy wylaczonym `AiCampsAtNight` ksiega AI jest czyszczona, a gracz dalej placi - bez obozu swiata AI nie ma kiedy splacac.

**R3 - splata jak u gracza (AI wybiera to, co gracz robi recznie)** (`AiDebtCamp`). Oboz 0-6 daje dokladnie 6.0 h [Sym] (zapas 0), wiec dlug 1 (9 h)
splacilby sie w obozie swiata tylko z pomoca dziennego postoju (w osadzie x0.6: 5 h w miescie = 3 h) - rzadko; stad:
- dlug 1: samotny lord / wodz rozbija oboz o **20:00** do konca obozu swiata - ticki 21..6 = **10 h** po pelnej stawce przy 9 h potrzebnych (godzina zapasu;
  [Sym]: bez straty splata o 5:00, z 1 zgubiona godzina - o 6:00; wariant 21:00 z 1 zgubiona godzina zostawia dlug);
- dlug 2-3: "spi tam, gdzie stoi" jak gracz w menu snu - sen ciagly z licznikiem jak pasek snu (`Acc`, swit go nie zeruje i nie dolicza dlugu spiacemu,
  jak `_sleeping` :946; wyjscie ze snu: odpoczynek doby = max(doba, licznik) jak `LeaveSleep` :1309). [Sym] dlug 2 od switu: splata o 3:00 (ok. 21 h),
  dlug 3: o 11:00 dnia nastepnego (ok. 29 h; bezpiecznik gracza :1399 = do 39.8 h);
- osobny slownik `_debtSleep` z wlasnym rozkazem sprzed snu - galaz switu obozu swiata (biegnaca o KAZDEJ godzinie 6..23) go nie rusza; zwalnia tylko
  splata, koniec okna (dlug 1 - o koncu obozu swiata), powod albo alarm; wlasny straznik co 0.1 h o kazdej godzinie (`HoldDebtSleepers`);
- w osadzie samo wstrzymanie AI (jak R1b); doczepieni odpoczywaja z armia; ucieczka i alarm przerywaja kazdy sen.
- T10-p: doczepiony do wodza, ktory spi snem ciaglym (dlug 2-3), dostaje ten sam licznik snu (`SleepsWithLeader`) - inaczej przy dlugu 3 potrzebowalby
  21 h w jednej dobie, a stojac zbiera najwyzej 18.4 h (10 h nocy + 14 x 0.6) i zostawalby na -95% morale do rozwiazania armii; licznik konczy sie, gdy
  wodz wstaje. Sen ciagly bez snu dlugu (wczytany zapis, splata w trakcie) konczy sie jak `LeaveSleep` gracza.
- T10-p: partii z AI wylaczonym przez inny mod (uczta / gentry / statek BK) sen dlugu nie kladzie, a spiacemu, ktoremu inny mod przejal AI, oddaje go bez
  `EnableAi` (`ForeignHold`); dlug takiej partii splaca sie zwyklym odpoczynkiem (osada = odpoczynek).
- T10-p, SWIADOMIE (recenzja 24): sen dlugu i alarmy nie ida do zapisu (jak `_orders` T1) - po wczytaniu w trakcie snu dlugu lord kladzie sie dalej
  (dlug i licznik sa w zapisie), ale po splacie mysli od nowa zamiast wracac do rozkazu sprzed snu. Rozkaz sprzed snu wraca tylko, gdy jest wazny
  (`ApplyOrder`: oblezenie i rabunek - wojna z wlascicielem osady, obrona - wlasna frakcja, atak - wroga partia).

## 4. Liczby i uzasadnienie (dobrane przeze mnie)

Rachunek [S] dla lorda ze 150 ludzmi, 1/3 konno (speed 2.72, A07 tab. 1.3; partia robi speed/1.2 jedn./h, A07 1.1; kara nocna 0-2 -0.5):
- nocny marsz 0-6 daje (2 x 2.22 + 4 x 2.72) / 1.2 = **12.8 jedn. (ok. 61 km)**;
- nastepny dzien z dlugiem 1: marsz 6-20 (oboz o 20 na splate) x 0.75 = 23.8 jedn. zamiast 40.0 - **strata 16.2 jedn.**
- **Wniosek: noc bez snu oplaca sie tylko wtedy, gdy cel zostaje osiagniety tej nocy albo rano** - potem lord stoi (bitwa, odsiecz), a kara predkosci nic
  go nie kosztuje. Stad zasiegi:

| Liczba | Wartosc | Uzasadnienie |
|---|---|---|
| zasieg poscigu | **L = 6 h marszu** (ok. 13 jedn. / 61 km u lorda) | dogoni tej nocy; dalej strata dnia zjada zysk nocy. W GODZINACH, nie w jednostkach - po zmianie tempa swiata (A07 T-7) regula dziala tak samo. Odleglosc do partii: linia prosta x 1.1 (gra liczy partia-partia linia prosta, DefaultMapDistanceModel.cs:92-113; trasa ok. 1.1 x - A07 1.4) |
| zasieg odsieczy | **2L = 12 h marszu** (ok. 26 jedn. / 125 km) | dochodzi do poludnia; rabunek wsi trwa 36-90 h ((pierw(ludzi)+5)/900 na godzine: DefaultRaidModel.cs:48), wiec 6 h zysku = 7-17% wsi uratowane. Odleglosc z siatki osad gry (DefaultMapDistanceModel.cs:60-84) |
| "silniejszy" | sila wroga > wlasna (stosunek > 1.0) | ta sama miara sily, ktora gra bierze do decyzji o ucieczce |
| promien alarmu | 6 jedn. (bez zmian, `AiCampDangerRadius`) | gra patrzy na 9 jedn. (EncounterJoiningRadius 3 x 3) - wrog z alarmu jest w zasiegu decyzji AI o ucieczce |
| "idzie na lorda" | cel = lord albo odleglosc -0.2 jedn./h | wrog przechodzacy bokiem nie budzi |
| czas na ucieczke po alarmie | 0.5 h | AI sprawdza inicjatywe co 0.15-0.18 h (MobilePartyAi.cs:321) - 2-3 szanse |
| `AiNightsAwakeInChase` | **1** (wartosc juz zapisana u Jeffa) - klucz ozywa: poscig/odsiecz noca tylko przy dlugu < 1; 0 = noca tylko ucieczka; 3 i wiecej = tylko zapasc zatrzymuje | lord z dlugiem 1 juz traci 25% - druga noc dalaby -40% i strate wieksza niz zysk; uciekac wolno zawsze |
| oboz splaty dlugu 1 | **20:00** | 10 h pelnej stawki (ticki 21..6) przy 9 h potrzebnych - godzina zapasu [Sym] |
| kary | -25/-40/-90 predkosci, -25/-40/-95 morale | liczby gracza, bez zmian (wzor poprawiony dla obu) |

Skutek [S]: zamiast ok. 57 kolumn z losowania - tylko kolumny z powodem. Ile ich jest i ile dlugow powstaje - **nie szacuje**: progi P3/P5 bierze sie
z przebiegu na sucho (rozdz. 8, P0). Samotni lordowie ida srednio ok. 0.15 x 6 h = ok. 0.9 h mniej na dobe (ok. -5% drogi); spiacych lordow +15-20%.

## 5. Gdzie w kodzie (wykonane, drzewo t10)

1. **Settings.cs** (grupa "A night's rest"): NOWE `AiNightMarchByReason = true`, `AiSleepDebt = true`, `AiNightReliefWider = false` (pytanie 2);
   nowe opisy `AiCampSkipPercent` (karawany; lordowie tylko przy wylaczonym AiNightMarchByReason), `ArmyLeadersAlwaysCamp` (dziala przy wylaczonym
   AiNightMarchByReason), `AiNightsAwakeInChase` ("sleepless nights owed after which a lord no longer chases or rides to relief at night - the loss would
   outweigh the gain; fleeing is always allowed. 0 = only flight by night, 3 or more = only the collapse stops them"), `AiCampDangerRadius` (promien alarmu).
   `python tools/gen_mcm.py` (Armoury: 762 ustawienia).
2. **NightMarch.cs (NOWY, `partial class NightRest`)**: `Classify` (R1), `AlarmThreat` + `GuardAlarmed` + `SleepAgain` (R1a), `TownNight` (R1b),
   `LordNight` (sciezka lorda w obozie swiata), `AiSleepLedger` + `SettleAi` + `RebuildPenalties` (R2), `AiDebtCamp` + `EnterDebt` + `ReleaseDebt` +
   `HoldDebtSleepers` (R3), `ExportAi` / `ImportAi` / `ResolveImport` (zapis), `DryClassify` / `DryCount` (na sucho), logi (`LogNightTally`, `LogMove`,
   `LogDawn`, `LogPenaltySample`, `LogPlayerDawn`), `AiHourly` (kolejnosc godziny: ksiega -> splata -> oboz swiata -> bandy -> pozycje), `AiDebtOf`
   (hak dla musztry, rozdz. 11).
3. **NightRest.cs**: `OnHourly` - `AiHourly` PRZED wyjsciami gracza (martwy / Nieumarly), linia ksiegi gracza o swicie (SettleNight bez zmian);
   `AiNightCamp` - petla: lord w osadzie -> `TownNight`; w osadzie/bitwie/oblezeniu -> pomin; w snie dlugu -> pomin; lord przy AiNightMarchByReason ->
   `LordNight`; reszta (karawany, bandy, lordowie przy wylaczonym przelaczniku) -> stara sciezka T1 bez zmian w dzialaniu + `DryClassify`;
   `GiveOrderBack` -> wspolne `ApplyOrder`; `SpeedPostfix`/`MoralePostfix` - `DebtFor` (gracz: `Debt`, AI: `_aiPenalty`) i poprawiony wzor; `OnTick` -
   straznicy snu dlugu i alarmow (co 0.1 h gry, o kazdej godzinie); `LogCampConfig` z nowymi przelacznikami; `ResetWorld` -> `ResetAi`.
4. **Watki (uwaga 12):** `_aiPenalty` czytany w rownoleglym liczeniu predkosci (CampaignTickCacheDataStore.cs:320-327, ValidateSpeed :260) - nigdy nie
   zmieniany w miejscu: `RebuildPenalties` buduje nowy obiekt w ticku godzinowym (watek glowny) i podmienia referencje (`volatile`). Przy zmianie dlugu
   `mp.UpdateVersionNo()` (MobileParty.cs:3051 - uniewaznia pamiec predkosci), tylko partiom ze zmieniona kara.
5. **Zapis**: `ExportAi()/ImportAi()` - "v1|StringId:dlug:odpoczynek:splata:sen;..." - **kazdy niezerowy wpis** (dlug, odpoczynek doby, sen ciagly,
   splata - takze partia bez dlugu, ktora tej nocy szla); limit 1500 (lordow ROT ok. 500-700), odpada najmniejszy dlug i odpoczynek (w logu ile);
   ArmouryBehavior.cs obok `arm_nightrest`: `SaveText.Sync(dataStore, "arm_nightrest_ai", ref ...)`. Wczytanie rozwiazywane przy pierwszym ticku (partie
   juz istnieja); partie spoza zapisu = stan zerowy z pelna doba. `ResetWorld` czysci ksiege AI. Stary zapis bez klucza = wszyscy bez dlugu.
6. **WorldMeasure.cs**: w linii "Miara: marsz" godziny ruchu samotnych lordow (kazda wielkosc, nie wodzowie) w oknie obozu.
7. **Armoury.csproj**: `-p:T10Dry=true` -> stala `T10_DRY` -> DLL na sucho (rozdz. 8 P0).
8. **CHANGELOG.md** drzewa: wpis T10 (NIEWGRANE), commit lokalny.

## 6. Wylaczniki, skutki, zamkniety obieg

**Wylaczniki (MCM Armoury, "A night's rest"):** `AiNightMarchByReason` (wyl. = T1: los 15%, W1-W7; log liczy obok, co powiedzialby T10), `AiSleepDebt`
(wyl. = ksiega AI tylko w logu: bez kar i bez obozu splaty), `AiNightReliefWider` (pytanie 2), `AiNightsAwakeInChase` (0 = noca tylko ucieczka; 3+ =
poscig i odsiecz bez wzgledu na dlug), stare: `AiCampsAtNight` (wyl. = AI nie obozuje, ksiega AI wyczyszczona - bez obozu AI nie ma kiedy spac),
`NightRestEnabled`, `ArmyLeadersAlwaysCamp`, `AiCampSkipPercent`, `AiCampDangerRadius`. Zmiana przelacznika ksiegi = dlugi AI od zera (linia w logu).
Pulapka MCM (CLAUDE.md 7): nowych kluczy nie ma w Armoury.json Jeffa - wezma domyslne z kodu; `AiCampSkipPercent` 15 w jego pliku dalej dziala - dla karawan.

**Skutki dla gracza:** Twoje kary i rachunek bez zmian (kod gracza nietkniety), poza wzorem kary predkosci (rowno -25/-40/-90% zamiast x (1 + suma)).
Noca mniej lordow w drodze; kazdy, kogo spotkasz, ma powod. Lord uciekajacy noca jest nazajutrz o 25% wolniejszy; lord, ktory Cie noca gonil, tez.
W opisie predkosci/morale partii AI pojawia sie "Sleepless nights". Gracz w armii AI (A07 3.11/O-5) - bez zmian (osobna sprawa).

**Skutki dla AI:** samotni lordowie ok. 5% mniej drogi na dobe; lordowie w drodze na zbiorke spia - armia zbiera sie do ok. 6 h dluzej na kazda noc ich
drogi (armia czeka do 72 h: Army.cs:730-747, DefaultArmyManagementCalculationModel.cs:46); lordowie w osadach nie wyjezdzaja noca; nikt nie idzie noca
na rabunek (trwajacy rabunek trwa dalej - partia w rabunku stoi, wiec odpoczywa, jak gracz). **Armie: ta sama regula co samotny lord** - noca tylko
ucieczka, alarm od silniejszego i poscig dokonczony tej nocy; dzis budzil je kazdy wrog w 6 jedn. (W6), wiec przestaja chodzic noca przy slabych bandach;
armia idaca na odsiecz oblezenia nie idzie noca takze w ostatnich 6 jedn. (dawniej budzil ja W6) - do odpowiedzi na pytanie 2. Za noc poscigu albo ucieczki
armia placi tak jak samotny lord (dlug wodza = predkosc armii). Dezercja (POPRAWIONE po recenzji 9 i 13): ta sama tablica kary morale daje dzis rozne
skutki. Gracz, jego rod i partie jego towarzyszy (`IsLordParty`, sa w ksiedze AI) dezerteruja wedlug `DesertionLaw` (DesertionLaw.cs:84-90: prog t1 25,
co tier -3, podloga 10, do 25%/dobe) - dlug 1 przy morale ok. 30 albo dlug 2 przy ok. 40 juz uruchamia ucieczki rekrutow t1-t3. Lordowie AI innych rodow
- wedlug gry (`DesertionLawForAi` = false, Settings.cs:236: ponizej 10, DefaultPartyDesertionModel.cs:12, 28-31), czyli praktycznie dopiero przy zapasci.
Jeff zdecydowal 09.10 ok. 04:25 (C): "dezercja wedlug poziomu takze u AI - TAK" - wlacza to paczka K1b (jedna regula); T10 klucza nie rusza, zeby nie
kolidowac. Dezerterzy ida do puli wyrzutkow ze sprzetem (OutlawLaw.cs:330) - P8 mierzy dezercje AI i doplyw do puli.

**Zamkniety obieg (nic z niczego):** zadnego zlota ani przedmiotow; godziny nocnego marszu kosztuja predkosc i morale nastepnego dnia i dluzszy sen - dla
100% partii lordow AI i dla gracza ta sama ksiega; dlug przezywa zapis. Bez ceny ida noca tylko: Nieumarli (lore), zeglujacy (straze - tak samo gracz),
bandyci (wlasny rytm: lezenie za dnia, `AiBanditRest`) i karawany (pytanie 1). Znika darmowe losowanie 15% lordow.

**Ryzyko / co sprawdzic (zasada 0):** (a) wspolne `SpeedPostfix`/`MoralePostfix` - galaz gracza tylko z nowym wzorem; koszt: pusty slownik = natychmiastowe
wyjscie; (b) R3 a galaz switu - osobny slownik `_debtSleep`, galaz go nie rusza; (c) StrategicCampaignAI / AIInfluence budza spiacych (A07 1.6) - straznik
T1 w godzinach obozu, nowy straznik snu dlugu o kazdej godzinie; (d) ALARM tylko od silniejszego, czuwajacego, idacego - samotny lord i armia przestaja sie
budzic na slabsza albo spiaca bande obok (zamierzone); (e) dlugosc napisu zapisu (linia "NocnyMarsz: zapis"); (f) karawany i ich losowanie nietkniete;
(g) T1 (oboz 0-6, straznik 0.1 h, namioty) - bez regresji w liczbach noc.log (spiacy w snie dlugu poza `_camping` - bez namiotow, celowo);
(h) BK uczta/gentry: w osadzie nie ruszamy partii z AI wylaczonym przez BK; w polu jak w T1 (znane, bez zmian); (i) zero zapasu w obozie 0-6 - progi P5 z P0;
(j) T10-p: lordowie trzymani noca w osadach i spiacy dlug w miastach czesciej maja dobowy tick w osadzie - AiGear czesciej kupuje i naprawia (za zloto,
nic z niczego, ale zmienia popyt na sprzet - 174, K1): P8 porownuje linie zakupow i napraw AI i pokrycie zbrojowni AI (171) z baza;
(k) T10-p: przelacznik glowny `NightRestEnabled` wylaczony w trakcie gry = `MasterSwitch` jednorazowo budzi swiat (T1 i T10), zdejmuje kary AI i czysci ksiege.

## 7. Log: ile kolumn szlo noca i dlaczego (wykonane)

- co godzine obozu (noc.log): `AiNightCamp: marsz 2:00 - samotnych lordow w polu N: spi S, ida noca M (ucieczka a, poscig b, odsiecz c), alarm - wstali
  ocenic zagrozenie x; wodzowie armii w polu W: spi, ida (ucieczka, poscig, odsiecz), alarm; w osadach: zostaja na noc T, wyjechali z powodem U, AI trzyma
  inny mod (np. uczta BK) V; na morzu Z; dluznicy w snie dlugu D (...); karawany z losowania K. BEZ POWODU 0 z konstrukcji - kto naprawde szedl, mowi linia
  'AiNightCamp: ruch'. Alarmy od poprzedniej godziny: ucieczka f, spi dalej g, inny marsz o (cel AI cofniety r, bitwa/osada q); dluznicy obudzeni cudza reka d.`
  Na sucho: `AiNightCamp: marsz 2:00 [NA SUCHO - dzialaja stare reguly T1] - samotnych lordow w polu N: idzie po staremu M, spi S; wodzowie ...
  | wedlug T10 szloby z powodem P (ucieczka, poscig, odsiecz), alarm A; BEZ POWODU wedlug T10 samotnych Q (los, w drodze na zbiorke, poscig daleko / cel
  nie wrog / dlug, wrog blisko slabszy albo spiacy), wodzow Q2; ...`
- **niezalezny pomiar z pozycji** (noc.log, ticki 1..6): `AiNightCamp: ruch 1:00-2:00 (pomiar z pozycji, > 0.35 jedn. w godzinie obozu, poza osada
  i bitwa) - samotni lordowie w ruchu R: z powodem a, alarm -> ucieczka b | BEZ WPISU POWODU n: alarm bez ucieczki, obudzeni cudza reka, dluznicy obudzeni,
  wyjazd z osady / po bitwie, los (stare reguly), stare wyjatki T1, morze/Inni/doczepieni, bez wpisu; wodzowie armii w ruchu W (z powodem w).`
- przyklady (pierwsze 3 i co 50.): `AiNightCamp: marsz - <lord> (wodz armii): idzie noca: odsiecz <wies> (rabunek, 7.2 h marszu, trasa 15.3 jedn.,
  prosto 12.1), dlug 0 [przyklad n w sesji]` / `... ALARM - <wrog> (sila 1240 > 310, 4.1 jedn., idzie na niego) - AI ocenia ucieczke`;
- o swicie (glowny log): `NocnyMarsz: swit dnia N - partii lordow AI w ksiedze 712 (bez pelnej doby 3); nowy dlug 1/2/3: 14/2/0; splacone od reki 11;
  z dlugiem teraz 1/2/3: 15/2/0; szly noca z powodem partii 22 (z nich odpoczely mimo to >= baza 7); partio-godziny nocnego marszu: ucieczka, poscig,
  odsiecz; alarmy (ucieczka, spi dalej, inny marsz); w osadach zostalo na noc / wyjechalo z powodem; oboz splaty od 20:00 partii, sen ciagly, zwolnione,
  dluznik szedl z powodem, alarm dluznika, dluznicy obudzeni cudza reka; zapasci | ruch w oknie obozu (pomiar z pozycji, 6 h): samotni lordowie N
  partio-godzin, BEZ WPISU POWODU M (sr. x na godzine; ...); wodzowie | stoper ksiegi: sr. X ms, maks. Y ms; straznik ...; potkniecia T10 w sesji 0.`
- kara (glowny log, o swicie): `NocnyMarsz: kara - probka 3 z 17 dluznikow AI: <lord> (wodz armii) dlug 1: predkosc 3.12 -> 2.34 (-25.0%, tablica -25%),
  morale 52.0 -> 39.0 (-25.0%, tablica -25%), wpis 'Sleepless nights' tak/tak; ... | wodzow armii w probce 1; zgodnosc z tablica +-1 pp: 3/3.`
- gracz (glowny log, o swicie): `NocnyMarsz: gracz o swicie - odpoczynek doby 6.0 h (baza 6.0, sen w menu nie, splata od reki w tej dobie nie), dlug przed 0,
  po 0 - wzor (dlug +1, gdy < baza i nie spi; maks. 3): zgodny.`
- zapis/wczytanie: `NocnyMarsz: zapis ksiegi snu AI - wpisow n (z dlugiem a, w snie ciaglym b), napis k zn.`, `NocnyMarsz: wczytano ksiege snu AI - ...`
  albo `... zapis bez ksiegi snu AI (stary zapis ...)`; `Miara: marsz ... | okno obozu 0:00-6:00 (samotni lordowie, kazda wielkosc): N h ruchu, partii M.`

## 8. Plan testu

**P0 - NAJPIERW na sucho (uwaga 6):** DLL zbudowany z `-p:T10Dry=true` (najlepiej `-p:OutDir=...`, potem zwykly build z `-t:Rebuild`, zeby obj nie zostal
z wersja na sucho); autotest 40 dob nowej kampanii. Zachowanie i kary jak w T1, logi T10 licza: ilu szloby z powodem (P3), ile dlugow by powstalo przy
zachowaniu T1 i z jakich zgubionych godzin (P5), ruch bez wpisu powodu (P2). **Progi P3 i P5 wpisuje sie z tego przebiegu** (np. srednia + 50%) przed
przebiegiem wlasciwym; ponizsze liczby P3/P5 sa wstepne.
T10-p (recenzja 10 i 18): na sucho dlug AI rosnie bez splaty (brak obozu od 20:00 i snu ciaglego), wiec klasyfikacja powodow liczy z dlugiem 0, a **z P0
bierze sie tylko "nowy dlug 1" i powody (P3)**; progi dlugu 2/3 i zapasci (P5) ustala pierwszy przebieg wlasciwy. DLL na sucho ma stary wzor kary gracza
(jak T1); inna jest tylko kolejnosc w `OnHourly` (swiat AI przed wyjsciami martwego / Nieumarlego gracza) - w autotescie gracz zyje, bez wplywu.

**Przebieg wlasciwy:** autotest 40 dob nowej kampanii + zapis doby 362 przez 8 dob; porownanie z ostatnim przebiegiem tej samej bazy (autotest 03:48: lordow
spiacych sr. 330/h, `Miara: marsz` grupa 1 sr. 18.8 h ruchu, mediana 145 km, 13.1 s/dobe).

| # | Linia logu | Prog przejscia |
|---|---|---|
| P1 | `NightRest: oboz swiata 0:00-6:00 (...). T10: AiNightMarchByReason=True, AiSleepDebt=True, AiNightReliefWider=False, ...` | jest przy starcie, bez "NA SUCHO" |
| P2 | `AiNightCamp: ruch ... BEZ WPISU POWODU n` (niezalezny pomiar z pozycji) i `NocnyMarsz: swit ... BEZ WPISU POWODU M (sr. x na godzine ...)` | **sr. <= 2 na godzine obozu** (zeglujacy osobno - "na morzu (W2, poza P2)", T10-p); "alarm bez ucieczki" i "bez wpisu" blisko 0; w linii `AiNightCamp: marsz` "inny marsz" po alarmie = 0 (liczniki tej godziny, T10-p) |
| P3 | `AiNightCamp: marsz ... ida noca n` | wstepnie: srednia <= 20 na godzine (prog ostateczny z P0); w calym tescie kazdy z powodow (poscig, ucieczka, odsiecz) >= 1 |
| P4 | T10-p: `AiNightCamp: marsz ... P4: lordow spiacych w polu N (oboz swiata + sen dlugu w polu), w osadach na noc M, udzial spiacych samotnych w polu x%` (ta sama populacja co 330 w T1 - `_camping` lordow + dluznicy w polu; T10 wyjmuje dluznikow z `_camping`) | srednia N >= 360 (+10% wzgledem 330) w 40 dobach; udzial spiacych samotnych w polu sr. >= 95% |
| P5 | `NocnyMarsz: swit` (T10-p: nowy dlug i zapasc tylko przy wzroscie, "zapasc trwa" osobno; "splacone od reki ... wedlug poziomu 1/2/3"; "dlug 1 z poprzedniego switu: splacony a, wzrosl do 2 b, dalej 1 c (x%)") | wstepnie: nowy dlug 1 sr. <= 25 na swit (prog ostateczny z P0); dlug 2+ sr. <= 5 i zapasci (nowy dlug 3) <= 10 w calym tescie (progi ostateczne z pierwszego przebiegu wlasciwego); dlug 1 z poprzedniego switu splacony >= 80% (suma a / (a+b+c) w 40 dobach) |
| P6 | `ERROR in` z NightRest / AiSleepLedger / AiDebtCamp / HoldDebtSleepers / GuardAlarmed / Tent | 0; `Potkniecia straznikow w sesji: 0`, `potkniecia T10 w sesji 0` |
| P7 | stopery w linii switu (ksiega; T10-p: petla obozu swiata, oboz splaty, straznicy T10); czas doby | ksiega sr. < 1 ms/h, maks. < 10 ms; oboz splaty sr. < 1 ms; straznicy sr. < 0.5 ms; petla obozu swiata - bez wzrostu > 20% wzgledem P0; doba <= 13.8 s (bazowe 13.1 + 5%) |
| P8 | `Miara: marsz` (grupa 1 i NOWE "okno obozu ... samotni lordowie, kazda wielkosc"), bitwy.log, bankruci; T10-p: dezercja AI na dobe i doplyw dezerterow do puli wyrzutkow (OutlawLaw), linie zakupow i napraw AiGear, "Pokrycie zbrojowni AI (171)" | godziny ruchu samotnych w oknie obozu: wyrazny spadek wzgledem P0; grupa 1 sr. h ruchu <= 18.8; mediana km/dobe >= 130 (-10%); liczba bitew +-15%; bankruci +-3 wzgledem bazy; dezercja AI i doplyw do puli +-25%; zakupy / naprawy AiGear i pokrycie 171 +-15% |
| P9 | zapis 362: `NocnyMarsz: wczytano zapis bez ksiegi snu AI` / `zapis ksiegi` | wczytanie starego zapisu bez bledu; napis zapisu < 40 KB (SaveText dzieli na kawalki po 8000 zn.); po zapisie i wczytaniu w tej samej sesji `wczytano ksiege ... wpisow n` = n z linii zapisu |
| P9b | T10-p (recenzja 11): zapis zrobiony z T10 o ok. 2:00, wczytanie, linia `NocnyMarsz: swit` po wczytaniu | "nowy dlug 1" po wczytaniu ok. jak przy poprzednim swicie tej samej kampanii (roznica <= 5 albo <= 25%), bez fali setek dlugow |
| P10 | `NocnyMarsz: gracz o swicie` | "wzor ... zgodny" w 100% linii; diff: `SettleNight` i `CreditRest` nietkniete (poza linia logu wokol `SettleNight`) |
| P11 | `NocnyMarsz: kara - probka` | w kazdej linii z dluznikami "zgodnosc z tablica +-1 pp: k/k" (100% probek; T10-p: morale z rozpiski liczone punkty / `BaseNumber`, nie / 100) i wpis 'Sleepless nights' tak/tak; co najmniej raz w tescie probka z wodzem armii |

Recznie w grze (Jeff, nie autotest): najechac kursorem na lorda, ktory noca uciekal - w predkosci i morale "Sleepless nights".

## 9. Pytania do Jeffa (tylko zmiany rozgrywki spoza decyzji z 04:00)

1. **Karawany:** dzis 15% karawan idzie noca za darmo (to samo losowanie co lordowie). Czy maja spac jak lordowie (noca tylko ucieczka) i placic tym
   samym dlugiem snu? Skutek: karawany ok. 5% wolniejsze = troche mniej handlu. Rekomendacja: TAK, ale razem z paczka tempa karawan (A07 T-5), zeby
   ekonomie przeliczyc raz; do tego czasu karawany bez zmian.
2. **Odsiecz oblezonej osady i odsiecz armia (jedna sprawa, przelacznik `AiNightReliefWider`, dzis WYLACZONY):** Twoje slowa mowia o rabunku ("spiesza sie,
   by przerwac rabunek"). Czy noca maja isc takze (a) samotny lord na odsiecz OBLEZONEGO zamku albo miasta swojej frakcji (do 12 h marszu) i (b) armia na
   odsiecz lupionej wsi albo oblezonego zamku - z ta sama kara za noc? Dzis armia idaca na odsiecz staje na noc nawet 6 jedn. przed oblegajacymi. W ksiazkach
   odsiecz Riverrun (Szepczacy Las, potem Bitwa Obozow) idzie forsownym marszem i bije w ciemnosci. Rekomendacja: TAK (jedna regula dla lorda i armii) -
   wystarczy wlaczyc przelacznik.
3. **Noc rabunku jako sen:** w ksiedze snu (Twojej i AI) partia, ktora stoi - takze w bitwie, przy rabunku albo pod murami - odpoczywa. Lord, ktory cala noc
   lupi wies, nie placi za noc, a lord, ktory idzie ja ratowac - placi. To ta sama regula co u Ciebie (gdy Ty lupisz noca, tez nie placisz). Czy noc rabunku
   (i oblezenia) ma sie liczyc jako sen dla Ciebie i dla AI tak samo? Rekomendacja: zostawic (rabunek to praca w miejscu z warta, a oblegajacy spia na zmiany),
   ale decyzja Twoja.

## 10. Krytyka i odpowiedzi

Kazda uwaga sprawdzona w kodzie (gra 1.4.8, nasz kod 0c41eee) przed poprawka. "Potwierdzona" = opis zgodny z kodem.

**1 (krytyczne) - ALARM bez filtra snu i kierunku.** POTWIERDZONA: lista zagrozen to kazda aktywna partia lorda i banda (przed T10 :633-638), bez
sprawdzenia `_camping`, AI ani kierunku; przy alarmie `GiveOrderBack` (:677) - obudzony idzie do starego celu, nie ucieka; `DefaultPartyDesertionModel.cs:12`
- prog dezercji 10. Petla dlugu z uwagi jest realna przy starym alarmie. ZMIANA (R1a): alarm tylko od wroga czuwajacego (poza `_camping`, poza snem
dluznikow, `!Ai.IsDisabled`; gracz - nie w obozie i nie w menu snu), silniejszego i idacego na lorda (cel = lord albo odleglosc -0.2 jedn. od poprzedniej
godziny); obudzony BEZ starego rozkazu (Hold, AI wlaczone, rozkaz sprzed snu zapamietany), straznik co 0.1 h: ucieczka = powod, nowy cel AI cofany, po 0.5 h
bez ucieczki - spac. Alarm, ktory konczy sie "spi dalej", nie kosztuje godziny snu (partia stoi = odpoczynek). Log: alarm -> ucieczka / spi dalej / inny
marsz (ma byc 0), a niezalezny pomiar (uwaga 4) lapie "alarm bez ucieczki" w ruchu.

**2 (krytyczne) - wodz armii na starych wyjatkach W5/W6.** POTWIERDZONA (:660-679: W5 bez limitu, W6 kazdy wrog w 6 jedn., takze banda). ZMIANA: wodz
armii ma te sama regule co samotny lord - ucieczka, alarm poprawiony (uwaga 1), poscig do L = 6 h; odsiecz armia - tylko przy `AiNightReliefWider` (pytanie 2).
Rozdz. 6 poprawiony ("armie: ta sama regula", nie "bez zmian"); dopisany skutek: armia idaca na odsiecz oblezenia nie idzie noca w ostatnich 6 jedn.

**3 (wazne) - ODSIECZ po rabunku.** POTWIERDZONA: `SettlementVariablesBehavior.cs:7, 20-23` kasuje `LastAttackerParty` dopiero dobe po zagrozeniu.
ZMIANA: odsiecz tylko gdy rabunek trwa w chwili ticku (`IsUnderRaid` = `Party.MapEvent.IsRaid`, Settlement.cs:446-456); warunek "napastnik zyje" usuniety.
Oblezenie: dolaczone do pytania 2 (jedna sprawa dla lorda i armii, przelacznik `AiNightReliefWider`, wylaczony) - nie jest moja interpretacja "gdy trzeba".

**4 (wazne) - P2 sprawdza sam siebie; P8 nie mierzy malych kolumn.** POTWIERDZONA (P2 liczony tym samym kodem, ktory kladzie spac; WorldMeasure grupa 1
= > 300 ludzi albo wodzowie). ZMIANA: niezalezny pomiar z pozycji ksiegi R2 (`LogMove`, linia `AiNightCamp: ruch`): co godzine obozu samotni lordowie poza
osada i bitwa przesunieci > 0.35 jedn., zestawieni z tym, co T10 o nich zapisal w poprzednim ticku (z powodem / alarm -> ucieczka / alarm bez ucieczki /
obudzeni cudza reka / dluznicy obudzeni / wyjazd z osady lub po bitwie / los / stare wyjatki / bez wpisu); prog P2 = "BEZ WPISU POWODU" sr. <= 2 na godzine.
Do `Miara: marsz` dopisane godziny ruchu samotnych lordow kazdej wielkosci w oknie obozu (P8). Przy tej uwadze znalazlem przyczyne, ktorej projekt nie
mial: partie w osadzie w ogole nie byly ruszane (W7) - AI wyjezdzalo noca bez powodu i tracilo godzine snu; dodane R1b (lord w osadzie zostaje do rana -
samo wstrzymanie AI, bo gra nie wypuszcza z osady partii z wylaczonym AI: MobileParty.cs:4086-4091; rozkaz nietkniety, BK-owe `DisableAi` nieruszane).

**5 (wazne) - brak dowodu kary AI; P10 niemierzalny.** POTWIERDZONA. Do tego znalazlem blad wzoru: `SpeedPostfix` robil `Add(-cut)`, a gra mnozy baze przez
(1 + suma wspolczynnikow) (ExplainedNumber.cs: `_unclampedResultNumber = Base + Base x SumOfFactors`) - kara -25% wychodzila np. -28.5%; probka +-1 pp
nie przeszlaby takze u gracza. ZMIANA: wzor poprawiony dla obu (predkosc `/ (1 + suma)` jak MarchPace; morale `x (1 + suma)` jak Rations); linia dzienna
`NocnyMarsz: kara - probka` (do 3 dluznikow, w tym wodz armii: predkosc i morale z rozpiski gry `SpeedExplained` / `GetEffectivePartyMorale(.., true)`,
bez kary i z kara, procent vs tablica, wpis "Sleepless nights", zgodnosc +-1 pp; podloga `LimitMin` oznaczona) - P11. P10 zastapiony linia
`NocnyMarsz: gracz o swicie` (odpoczynek, dlug przed i po, sprawdzenie wzoru) i diffem (SettleNight i CreditRest nietkniete).

**6 (wazne) - progi bez rachunku; zero zapasu.** POTWIERDZONA [Sym]: oboz 0-6 = 6.0 h przy bazie 6 (zapas 0), oboz od 21:00 = 9.0 h przy 9 h potrzebnych.
ZMIANA: oboz splaty dlugu 1 od **20:00** (10 h; [Sym] z 1 zgubiona godzina dalej splaca); przebieg NA SUCHO jako P0 (DLL `-p:T10Dry=true` - stala
`T10_DRY`, bez ruszania Armoury.json Jeffa; ksiega i klasyfikacja w logu, zachowanie i kary jak T1), progi P3/P5 z tego przebiegu; szacunek "5-20 na
godzine" usuniety. Zero zapasu w obozie 0-6 zostaje (to regula gracza: 6 h bazy); glowna przyczyna zgubionych godzin (wyjazd z osady w nocy) zamknieta R1b.

**7 (drobne) - zmienione znaczenie AiNightsAwakeInChase.** POTWIERDZONA. Zostaje "poscig i odsiecz tylko przy dlugu < wartosci, ucieczka zawsze" - dopisane
w rozdz. 0 pkt 5 jednym zdaniem dla Jeffa (lord z dlugiem traci wiecej, niz zyska; 3 w menu to wylacza); nowy opis klucza w MCM.

**8 (drobne) - linia prosta.** POTWIERDZONA (gra liczy partia-partia linia prosta, partia-osada po siatce osad: DefaultMapDistanceModel.cs:60-113).
ZMIANA: odsiecz - `MapDistanceModel.GetDistance(partia, osada)` (zapasowo 1.1 x linia prosta); poscig - 1.1 x linia prosta (jak A07 1.4); w przykladach
logu obie odleglosci.

**9 (drobne) - ksiega AI za wyjsciami gracza.** POTWIERDZONA (:88-93; A07 3.10). ZMIANA: `AiHourly` (ksiega AI, oboz splaty, oboz swiata, bandy, pozycje)
biegnie w `OnHourly` zaraz po `NightRestEnabled`, przed wyjsciami martwego / Nieumarlego gracza; te wyjscia dotycza juz tylko ksiegi gracza.

**10 (drobne) - zapis tylko dluznikow.** POTWIERDZONA. ZMIANA: zapis trzyma kazdy niezerowy wpis (dlug, odpoczynek doby, sen ciagly, splata - takze partia
bez dlugu, ktora szla noca); po wczytaniu partie spoza zapisu maja stan zerowy z pelna doba (nic nie ginie). Limit 1500, odpada najmniejszy dlug
i odpoczynek, w logu ile. (Uwaga do uwagi: w dzien odpoczynek doby > 0 ma kazda partia, ktora stala - zapis to kilkaset wpisow, nie "kilkadziesiat".)

**11 (drobne) - galaz switu budzi o kazdej godzinie.** POTWIERDZONA (:607-630 biegnie przy `!night`, czyli 6..23, czysci `_camping`, `_bedPos`, oddaje
`_orders`). ZMIANA: osobny slownik `_debtSleep` z wlasnym rozkazem (przejmowanym z `_orders`, gdy partia spala w obozie swiata) - galaz switu go nie
rusza o zadnej godzinie, takze o 6:00; zwalniaja tylko splata, koniec okna, powod albo alarm; wlasny straznik o kazdej godzinie.

**12 (drobne) - watki.** POTWIERDZONA (CampaignTickCacheDataStore.cs:320-327 `TWParallel.For`, `ValidateSpeed` :260). ZMIANA: zasada w kodzie i rozdz. 5.4 -
slownik kar budowany od nowa w ticku godzinowym (watek glowny) i podmieniany w calosci (`volatile`), nigdy zmieniany w miejscu.

**13 (drobne) - RBM/DTE/Spoils/ROT niesprawdzone.** POTWIERDZONA. ZMIANA: sprawdzone (grep metadanych DLL + ilspycmd) - wiersz w tabeli rozdz. 2:
zaden z tych modow nie karze marszu noca (RBM i Spoils - tylko bitwa noca; ROT - wyglad sceny); AIInfluence (zaciemniony) - nie sprawdzony dokladnie.

**14 (drobne) - przesadzone zdania.** POTWIERDZONA (dzienny postoj x0.6: NightRest.cs:113-119; dezercja < 10: DefaultPartyDesertionModel.cs:12). ZMIANA:
"rzadko splaciloby" (R3), "dezercja praktycznie dopiero przy zapasci" (rozdz. 6); pytanie 3 (noc rabunku jako sen, dla gracza i AI tak samo) - nie wdrazane.

## 10b. Druga recenzja (24 uwagi) i odpowiedzi - T10-p

Kazda uwaga sprawdzona w kodzie (drzewo t10 po cc0af2f, gra 1.4.8, BK). Kod: commit bcae98d, wpis CHANGELOG T10-p.

| # | Uwaga (skrot) | Werdykt | Co zrobione |
|---|---|---|---|
| 1, 11 | po wczytaniu Stamp -1 -> pierwsza godzina bez odpoczynku; oboz 0-6 = 6.0 h bez zapasu -> fala dlugu 1 (krytyczne) | POTWIERDZONA | ksiega jak gracz: brak odczytu = postoj; `_hadPos` zerowane w `ResetWorld`; P9b |
| 2, 12 | sen dlugu nadpisuje `DisableAi` uczty BK (`BKFeastBehavior.cs:247`), `ReleaseDebt` -> `EnableAi`; gentry 72 h | POTWIERDZONA | `ForeignHold` + pominiecie przy wejsciu (AI wylaczone poza naszymi listami); zwolnienie bez `EnableAi`; takze `TownNight` i `HoldDebtSleepers` |
| 3 | `NightRestEnabled` wylaczony w trakcie gry - dluznicy na Hold do konca sesji, kary i hak zostaja (T1 tez) | POTWIERDZONA | `MasterSwitch` (jednorazowe sprzatanie T1 i T10), straznicy tylko przy wlaczonym, `AiDebtOf` 0 |
| 4 | probka morale: linia Multiply z `GetLines` to punkty (`BaseNumber x wsp.`), nie procent | POTWIERDZONA (ExplainedNumber.cs `GetLines`) | wsp. = punkty / `BaseNumber` |
| 5 | rozkaz sprzed snu bez sprawdzenia waznosci; `_orders` po alarmie z ucieczka | POTWIERDZONA (EncounterManager.cs:102-112) | `ApplyOrder` sprawdza wojne / frakcje / wrogosc; ucieczka usuwa rozkaz |
| 6 | doczepieni bez snu ciaglego (maks. 18.4 h/dobe < 21 h) | POTWIERDZONA | `SleepsWithLeader` - licznik wodza; przy okazji koniec "wiecznego" licznika bez snu dlugu |
| 7, 21.2 | partia nieaktywna (rejs BK, `BKShippingBehavior.cs:535`) wypada z ksiegi o swicie z dlugiem | POTWIERDZONA | zostaje w ksiedze (jak gracz w rejsie); wypada tylko zniszczona; zapis/wczytanie bez filtra aktywnosci |
| 8 | liczniki alarmow zamykane tylko w godzinach obozu | POTWIERDZONA | `FlushHourCounters` co godzine |
| 9 | opis dezercji niescisly (towarzysze wg `DesertionLaw`) | POTWIERDZONA | rozdz. 0 pkt 4 i rozdz. 6 |
| 10 | DLL na sucho ma nowy wzor kary gracza i inna kolejnosc | POTWIERDZONA | na sucho stary wzor; kolejnosc opisana w P0 |
| 13 | ta sama kara morale, rozne skutki dezercji gracz/AI; pytanie o `DesertionLawForAi` | POTWIERDZONA | bez pytania - Jeff juz zdecydowal 04:25 (C), wlacza K1b; opis i P8 |
| 14 | poscig obejmuje karawany i wiesniakow | POTWIERDZONA | `ChaseWorthy` (waski wariant, slowa Jeffa) |
| 15 | zeglujacy w "BEZ WPISU POWODU" | POTWIERDZONA | osobny licznik morza |
| 16 | P4 porownuje rozne populacje | POTWIERDZONA | linia P4 (oboz swiata + sen dlugu w polu) i udzial |
| 17 | P5 niepoliczalne; dlug 3 liczony co swit jako nowa zapasc | POTWIERDZONA | wzrost tylko przy wzroscie, splaty wg poziomu, losy dlugu 1 |
| 18 | na sucho klasyfikacja z dlugiem bez splaty | POTWIERDZONA | `DryClassify` z dlugiem 0; P0 tylko dlug 1 i powody |
| 19 | alarm 5:30-6:00 cofa rozkaz oddany o swicie | POTWIERDZONA | po koncu obozu bez cofania, rozstrzygniecie od reki |
| 20 | R1b trzyma lordow we wsiach bez alarmu | POTWIERDZONA | alarm we wsi daje wolne AI; opisy |
| 21.1, 21.3 | drobne odpoczynki AI, ktorych gracz nie ma | POTWIERDZONA | swiadome wyjatki, opis w R2 (bez zmiany kodu) |
| 22 | napisy MCM; "Sleepless nights -30%" w podpowiedzi morale | napisy POTWIERDZONE; "-30%" ODRZUCONE - podpowiedz (CampaignUIHelper.TooltipAddExplanation -> GetLines) pokazuje punkty, rowne dokladnie % z tablicy | opisy 5 ustawien, suwak `AiNightsAwakeInChase` "0" |
| 23 | `catch {}` polyka bledy; stoper tylko ksiegi | POTWIERDZONA | potkniecia T10 w `AlarmThreat`, `DryClassify`, `ApplyOrder`; stopery obozu swiata, splaty, straznikow |
| 24 | sen dlugu i alarmy poza zapisem | POTWIERDZONA | swiadomie (jak T1), opis w R3 |
| 25 | AiGear czesciej w osadach | POTWIERDZONA (posrednio) | P8 i ryzyko (j) |

---

## 11. Dopisek z paczki MUSZTRA (09.10, krytyka musztry pkt 5 i 12) - do scalenia (zachowany) i odpowiedz T10

- Musztra (drzewo `SCR\noc2\musztra`, `Armoury\src\Drill.cs`) czyta dlug snu AI przez hak `Drill.SleepDebtOf` (`Func<MobileParty,int>`, domyslnie 0)
  - to inna rzecz niz ustawienie T10 `AiSleepDebt` (bool). **Wpina go ten, kto scala drugi:** przy starcie kampanii `Drill.SleepDebtOf = <dlug AI z ksiegi R2>`
  (przy wylaczonym `AiSleepDebt` zwraca 0). Partie doczepione do armii gracza musztra liczy juz z dlugiem gracza (`NightRest.Debt`).
- Godzina odpoczynku: musztra ma jedna funkcje `Drill.RestHour(mp, krok)` = osada, oboz obleznikow albo ruch <= 0.35 jedn./h (ten sam prog i ta sama
  granica co `NightRest.OnHourly`: "ruszyl sie" = krok > 0.35) - to czesc wspolna; `NightRest.OnHourly` i R2 przy scaleniu wolaja ja dla tej czesci, a morze
  przy `SleepAtSeaFree`, sluzba ROT i lista `_camping` / `_bedPos` T10 dochodza po stronie ksiegi snu, nie musztry (dla musztry partia na morzu plynie).
- Prog wlaczenia Z14b (musztra AI) mierzy sie dopiero z T10 w drzewie i `AiSleepDebt` wlaczonym.
- **Dopisek po recenzji musztry (09.10, "MUSZTRA-p"):** kara snu AI w musztrze NIE czeka na Z14b - nowy wylacznik musztry `DrillPenaltyAi` (domyslnie wlaczony,
  Jeff: "takie same kary jak gracz") zeruje trening kazdej partii lorda AI przy glodzie albo dlugu snu >= 1 z haka. Po wpieciu
  `Drill.SleepDebtOf = NightRest.AiDebtOf;` niewyspani lordowie od razu nie cwicza (cale XP, takze perki, bez zuzycia zapasu), tak jak gracz.
  Wyjatek w haku liczy sie do potkniec musztry (`Drill.SleepDebt`) - nie znika po cichu.

**Odpowiedz T10 (wykonawca):** hak gotowy - `NightRest.AiDebtOf(MobileParty)` (NightMarch.cs) zwraca dlug, ktory naprawde dziala (0 przy wylaczonym
`AiSleepDebt`, przy wylaczonym obozie swiata i w DLL na sucho), czyta podmieniany w calosci slownik kar - bezpieczny z kazdego watku. Przy scaleniu wystarczy
`Drill.SleepDebtOf = NightRest.AiDebtOf;` (T10-p: zwraca 0 takze przy wylaczonym `NightRestEnabled`). Godzina odpoczynku R2 (`AiSleepLedger`): osada, ruch < 0.35 jedn./h (oboz obleznikow stoi, wiec sie w tym miesci),
morze przy `SleepAtSeaFree`, `_bedPos` obozu swiata albo `_debtSleep` - przy scaleniu R2 moze wolac `Drill.RestHour` dla czesci wspolnej (osada, postoj),
a morze i obozy dokladac po swojej stronie; liczby sie nie zmienia.

**Dopisek grupa11 (scalenie MUSZTRA + T10 + I1b, galaz `noc/grupa11`) - ZROBIONE INACZEJ NIZ WYZEJ, nie wracac do haka:** `Drill.SleepDebtOf` i `NightRest.AiDebtOf`
usuniete; musztra wola wprost jedna funkcje `NightRest.DebtOf(mp)` (NightRest.cs): gracz - `Debt`, kazda inna partia lorda - ksiega snu AI R2 (ten sam slownik
kar co predkosc i morale), TAKZE doczepieni do armii gracza - licza wlasny sen (krytyka 5 musztry zmieniona: jedna prawda z kara morale). 0 przy wylaczonym
`NightRestEnabled`, `AiSleepDebt`, obozie swiata (takze rowne godziny obozu) i na sucho. `NightRest.OnHourly` i R2 (`AiSleepLedger`) wolaja `Drill.RestHour`;
`RestMoveLimit = Drill.RestStep`. **grupa11-p (recenzja):** (1) `SleepsWithLeader` - wodzem moze byc gracz spiacy w menu (`_sleeping`): doczepieni spia z nim
przez swit (licznik `Acc`, `SettleAi` bez dlugu jak `SettleNight` gracza), a po jego pobudce `AiSleepLedger` konczy ich sen od reki, przed switem tej godziny
(jak `LeaveSleep`); (2) `AiImportNow` - ksiega AI z zapisu rozwiazana na pierwszej klatce po wczytaniu (`NightRest.OnTick`), nie w pierwszym ticku godzinowym.
Opis w CHANGELOG (grupa11, grupa11-p).
