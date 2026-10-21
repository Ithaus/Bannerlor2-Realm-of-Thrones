# m104-e5 - E5: sceny zmieniajace swiat w prologu 298 (orszak, Turniej Reki, Tyrion i Jaime jencami, klany gorskie, ludzie Gory, Beric)

Galaz `w-toku/m104-e5` (baza 756e685f = m103-sceny: prolog 298 + okna scen + obrazki; WGRANIE 101 + m103-sceny). Projekt: PROJEKT-PROLOG-298-2026-10-21.md
pkt 2 (rodzaj "S") i pkt 4 (E5). Jeff 21.10: "Kod scen zmieniajacych swiat i wojen wedlug ksiazki - rob to teraz". Rozpoznanie: wlasne (dekompilacja 1.4.8, ROT 8.1.8,
BK, GrandTourney/RC/Armoury) + rownolegle e5:A / e5:B (journal wf_d4f0c951-09f).
Tylko kampania z prologiem, kazda scena pod swoim kluczem MCM, koniec najpozniej przy koncu prologu (dzien 186-188); kampania klasyczna bez zmian.

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| K1 | straz niewoli (pkt 3 i 4): CanonCompanions.HeldRow / Freeing, tabela #m01 classic + #p10 Riverrun, latki EndCaptivityAction.ApplyInternal i RansomOfferCampaignBehavior.DailyTickHero, klucze PrologueTyrionHeld / PrologueJaimeAtRiverrun + 5 kluczy scen | ZROBIONE (ff9d85e7, build 0, gen_mcm) |
| K2 | orszak krola (pkt 1): rejestr partii sceny (flaga bez nowych decyzji poza zapisem, rozkazy przez wejscie metody, ukrycie przed AI), trasa KP-gospoda-Blizniaki-Winterfell-...-KP, eskorta Jaime/Tyrion/Ned, dosiadajacy w druzynie krola, dzieci przy przyjezdzie, Tyrion na Mur i do gospody; uczta BK pomija partie sceny; GT CanonApi.SkipSummon | ZROBIONE (9e4b8151, build Armoury + GT 0) |
| K3 | Turniej Reki (pkt 2): uczestnicy kanonu do KP od d117, obwieszczenie GT d121 (CanonApi.ProclaimRoyal: Grand bez reguly osmiu i bez spadku do lokalnego, kanon w drabince najpierw), rozstrzygniecie gry d127, linie | ZROBIONE (b0ee6e05, build Armoury + GT 0) |
| K4 | klany gorskie Doliny (pkt 5): od d118 do 4 band klanu hilltribesmen (Stone Crows, Burned Men, Milk Snakes, Black Ears) przy wsiach Gorskiego Traktu, ludzie z puli okregu (RaidDraw), uzupelnienie co 3 dni do d186, po prologu zwykle bandy | ZROBIONE (f3025c82, build 0) |
| K5 | ludzie Gory (pkt 6): od d155 dwie bandy robber_knights 'The Mountain's Men' pod sztandarem Lannisterow z ludzi okregu wsi Clegane najblizszej Riverrun (Harrenhal), rabunek wsi Dorzecza po kolei (mechanika gry), na koniec prologu do domow (FreeFolkRaids.Home) | ZROBIONE (build 0) |

## K1 - straz niewoli: Tyrion w Orlim Gniezdzie (110-132), Jaime w Riverrun

**Problem.** Tabela kanonu (m101-postacie) wsadza Tyriona do lochu Orlego Gniazda w dniu 110 (#p01) i uwalnia w 132 (#p02), a Jaimego (#m01) do Winterfell po wybuchu
wojny Polnocy. Jeff: Jaime ma siedziec w Riverrun (jak w ksiazce) i obaj nie moga uciec ani byc wykupieni do uwolnienia z tabeli. Bez strazy gra wypuszcza ich sama.

**Przyczyna (dekompilacja 1.4.8).** RealisticCaptivity dotyka tylko niewoli gracza (wszystkie latki RC na EndCaptivityAction maja warunek MainHero). Jeniec AI w lochu
wychodzi przez: (a) ucieczke 4%/dobe (PrisonerReleaseCampaignBehavior.DailyHeroTick; 22 dni = ok. 59% szans), (b) zwolnienie przy pokoju i zmianie krolestwa rodu
(ReleasePrisonersInternal - zwalnia kazdego bohatera, ktorego krolestwo NIE walczy z wlascicielem lochu: Tyrion i Dolina sa w pakcie, Jaime i Dorzecze moga nie byc
w wojnie), (c) okup AI-AI (RansomOfferCampaignBehavior.DailyTickHero -> ConsiderRansomPrisoner, u nas prefiks Ransom178 - zloto plynie PRZED ApplyByRansom),
(d) wypuszczenie przez porywacza i wykup (SellPrisonersAction).

**Zmiana.**
- `CanonCompanions`: mapa jencow trzymanych (`HeldRow`, `HeldDay`) - ostatni wykonany wiersz postaci to "prisoner", a pozniejszy "free" jeszcze nie zapadl; liczona
  w dobie po Moves i przy starcie sesji; `Freeing` - na czas naszej akcji "free" (straz przepuszcza); `TemplateHero` - postac z wzoru dla scen.
- `arm_canon_moves.tsv`: #m01 (Winterfell) tylko `classic`; nowy #p10 (ten sam wyzwalacz `war:battania:vlandia+5`) `prisoner:town_V2` (Riverrun) tylko `prolog`;
  dalej #m02 (uwolnienie po Czarnym Nurcie) bez zmian. Kampania klasyczna - bez zmian (decyzja do potwierdzenia: czy Riverrun takze w klasycznej).
- `Prologue.World.cs` (nowy, partial Prologue): prefiks `EndCaptivityAction.ApplyInternal` - jeniec trzymany przez scene (kampania z prologiem, klucz, czas,
  loch OSADY) nie wychodzi przez ucieczke, pokoj, okup, wypuszczenie, wykup; smierc i "po bitwie" (upadek osady, zmiana pana) oraz gracz jako sprawca (rozbicie lochu,
  okup gracza - zaplacil, wiec wychodzi) przechodza; prefiks `RansomOfferCampaignBehavior.DailyTickHero` - okup AI-AI nie zaczyna sie wcale (zloto nie plynie).
  Czas: Tyrion najpozniej do dnia prologu 140 (zapas po #p02), Jaime najwyzej 364 dni od #p10. Bez klucza CanonCompanionsFollowStory straz nie dziala (wiersz "free"
  nie przyjdzie).
- Latki wpinane w `InstallRest` (pierwsza kampania z prologiem w procesie) - klasyczny start: brak latek.
- Klucze MCM (grupa "Prologue 298 (new campaign)"): PrologueRoyalProgress, PrologueHandTourney, PrologueTyrionHeld, PrologueJaimeAtRiverrun, PrologueHillClans,
  PrologueMountainRaiders, PrologueBericHunt (wszystkie TAK; MCM Jeffa ich nie ma - domyslne).
- Linie: `Prolog E5 (m104): start sesji | ...`, `... dzien N | Tyrion: ... | Jaime: ... | straz niewoli: zablokowane dzis ...`, `... straz niewoli: ZABLOKOWANE - <kto>
  (#p01, jeniec town_EN1) - ucieczka ...` (raz na dobe na rodzaj), `... PRZEPUSZCZONE - ...`.

**Ryzyko / co sprawdzic.** Prefiks na prywatnej, duzej metodzie gry (bez wklejania JIT); nikt inny jej nie latka (RC lata publiczne ApplyByEscape/ApplyByRansom tylko
dla gracza - postfiksy). Pokoj: ReleasePrisonersInternal zostawia jenca w lochu (bez rozjazdu stanu). Okup gracza (barter) przechodzi - celowo (inaczej gracz zaplacilby
za nic); decyzja Jeffa, czy zablokowac takze barter. #p10 wymaga wolnego Jaimego w chwili wyzwalacza (jak #m01).

**Status.** DO SPRAWDZENIA (autotest z prologiem: dni 110-132 bez ucieczki Tyriona; Jaime w town_V2 po wojnie Polnocy).

## K2 - orszak krola Roberta (pkt 1), Ned, Tyrion na Mur

**Problem.** Projekt pkt 2: dzien 0 krol rusza Krolewskim Traktem do Winterfell z dworem (Cersei z dziecmi, Jaime, Tyrion, Ogar), dzien 35 Winterfell, 55 powrot z
Nedem, Sansa i Arya, ok. 95 Krolewska Przystan; dzien 55 Tyrion na Mur, ok. 100 przez rozstaje. Dotad nic sie nie ruszalo - tylko okna scen.

**Zmiana (Prologue.World.cs - wspolne, Prologue.World.Progress.cs - scena).**
- Rejestr partii sceny (`Control` / `Uncontrol`, bez zapisu - sceny odbudowuja go co godzine): flaga gry `DoNotMakeNewDecisions` (mysl gry, ROT AIThinkPatch i wezwania
  do armii ja pomijaja - rozpoznanie e5:A), rozkaz przez wejscie metody (`MethodInfo.Invoke` - bramka snu Armoury SleepGate i straznik drog BK dzialaja jak przy kazdym
  rozkazie), ukrycie przed AI `IgnoreForHours(2)` odnawiane co godzine (AI nie wybiera partii za cel i nie zaczyna spotkania; gracz moze). Partia spiaca (NightRest) -
  rozkaz nie ruszany do switu. Partia w armii - wychodzi (wodz - armia rozwiazana). Zapis gry: flaga zdejmowana w OnBeforeSave i wracajaca w OnSaveOver (wzor
  FreeFolkRaids - po cofnieciu DLL nikt nie stoi zamrozony). Partia nieodswiezona w przebiegu scen (klucz wylaczony, koniec sceny) - od razu wraca do zwyklego AI.
- Orszak: krol swoja druzyna lorda; trasa z postojami tylko w miastach i wsiach (gra kupuje tam jedzenie co godzine - PartiesBuyFoodCampaignBehavior.HourlyTickParty):
  KP (d0) -> gospoda na rozstajach (od d1) -> Blizniaki (od d10) -> Winterfell (od d28) ... d55 -> Blizniaki -> gospoda (od d70) -> KP (od d88) = koniec. Odcinek bez
  dojazdu 15 dni po terminie nastepnego - dalej (linia SPOZNIONY). Eskorta swoimi druzynami: Jaime, Tyrion (do d55), Ned (od wyjazdu z Winterfell), krolowa i Joffrey,
  gdy maja druzyny. W druzynie krola (TeleportHeroAction do partii; ten sam rod): dorosli z rodu krola bez druzyny - Cersei, Joffrey, Ogar (namiestnik osady zostaje).
  Dzieci (Myrcella, Tommen) do Winterfell przy przyjezdzie i do KP na koniec; Sansa i Arya do KP na koniec (tylko czynni, wolni, bez druzyny i bez urzedu).
  Na koniec: dosiadajacy wysiadaja w KP, wszyscy wracaja do swojego AI. Krol jencem / bez druzyny - orszak przerwany (linia).
- Ned (d50): druzyna do Winterfell, od wyjazdu orszaku eskorta do KP; bez druzyny - na koniec orszaku do KP (TeleportHeroAction, gdy wolny i nie namiestnik).
- Tyrion (d55): na Mur (Czarny Zamek town_B5), postoj do d96, gospoda na rozstajach do d109 (potem wolne AI; #p01 w dniu 110 bierze go stamtad).
- Uczta BK (prefiks `BKFeastBehavior.HourlyTickPartyImpl`): partia sceny nie jedzie na uczte (rozpoznanie e5:A: co godzine DisableAi + rozkaz do miasta uczty).
- GrandTourney `CanonApi.SkipSummon` (Armoury podaje odbiciem `Prologue.Scripted`): herold nie wzywa partii sceny; bez Armoury / kampania klasyczna - null, jak dotad.
- Zapis sprzed E5 wczytany w polowie prologu: orszak, Ned i Tyrion, ktorych czas minal, pominiete (bez nadrabiania).
- Linie: `orszak RUSZA`, `orszak: odcinek k/7 -> <osada> (...)`, `orszak w WINTERFELL (dzien ...)`, `orszak w KROLEWSKIEJ PRZYSTANI - KONIEC (dzien ...)`,
  `Ned: ...`, `Tyrion: ...`; w linii doby `orszak: odcinek ... krol <pozycja>, eskorta: ..., w druzynie krola: ...`.

**Ryzyko / co sprawdzic.** (1) Czy druzyna krola istnieje w dniu 0 (gra daje ja glowie rodu przy nowej grze - wynik +1000 w GetHeroPartyCommandScore); bez druzyny do d3 -
orszak pominiety. (2) Eskorta SetMoveEscortParty przy zamrozonej AI - czy jedzie za krolem i wchodzi z nim do osad (sprawdzic w autotescie odleglosci w linii doby).
(3) BK gentry (BKGentryBehavior.OnPartyDailyTick: DisableForHours(72) + rozkaz do majatku) - nie latane (rody orszaku nie sa szlachta BK; rozpoznanie: GentryNoParties);
jesli w logu eskorta "stoi" - dopisac te sama latke. (4) Postoj 22 dni w Winterfell - jedzenie kupowane co godzine przez gre (miasto); w zamkach postojow nie ma.
(5) Gracz zaatakuje orszak - wojna z korona przez wrogosc gracza jak zawsze; trasa idzie dalej.

**Status.** DO SPRAWDZENIA (autotest z prologiem 230 dob: linie orszaku, Winterfell przed d40, KP przed d100).

## K3 - Turniej Reki (pkt 2)

**Problem.** Projekt pkt 2: dzien 125 - turniej w Krolewskiej Przystani (Gora, Rycerz Kwiatow, Ogar), gracz moze startowac. GrandTourney sam turnieju nie zaklada
w zadanym miescie i dniu; przy wojnie krolestwa (gdziekolwiek) robi z niego turniej LOKALNY bez wezwan, przy mniej niz 8 lordach - lokalny; w KP bywa 18-29 lordow,
a drabinka ma 15 miejsc dla lordow (e5:A: "lordow w miescie 28, w drabince bylo 15, dolozono 0") - uczestnicy kanonu mogli nie wejsc.

**Zmiana.**
- GrandTourney `CanonApi.ProclaimRoyal(miasto, uczestnicy)` (publiczne, wolane odbiciem z Armoury): turniej nowy albo przejety, wpis `|c` w gt_events (5. pole - stary
  DLL GT czyta 4 pierwsze, bez zmian), wezwania jak zawsze (SummonLords); w OnDailyTick wpis `|c` nie schodzi do lokalnego przy wojnie krolestwa i otwiera sie jako Grand
  bez reguly osmiu; zjazd = GatherDays (4) z zamknietymi listami i bez konca turnieju (NoEarlyEnd - bez zmian). `CanonApi.SetHeroes` - uczestnicy kanonu (po wczytaniu
  Armoury podaje ich ponownie); NoblesFightPatch: uczestnicy kanonu najpierw (OrderBy stabilny), przy braku miejsca zastepuja ostatniego lorda spoza kanonu (nie gracza);
  linia "Szranki ... turniej kanonu: uczestnikow kanonu N, zastapionych lordow M". `CanonApi.Done` przy koncu / odwolaniu turnieju w tym miescie. Bez turnieju kanonu
  wszystko jak dotad (petla drabinki ta sama, gdy kanonu brak).
- Armoury `Prologue.World.Tourney.cs`: d117 Gregor (lord_4_21), Loras (lord_1_44), Jaime druzynami do KP (partia sceny, bez ukrycia; partia w armii - nie odciagamy, jak
  herold GT), Ogar bez druzyny - przeniesiony do KP (raz; gra nie wyprowadza lordow z miasta z turniejem); d121 obwieszczenie (bez GT - turniej gry) i wiesc EN dla gracza;
  d125 listy otwarte (GT); d127 - jesli nikt nie rozegral - `TournamentManager.ResolveTournament` (nagroda jak w kazdym turnieju gry); d128 albo koniec - uczestnicy do
  swojego AI. TournamentFinished (KP, faza obwieszczenia) - linia `Turniej Reki ROZSTRZYGNIETY w town_EW4 (dzien ..., z graczem / AI gry / rozstrzygniecie gry w dniu 127)
  | zwyciezca ... | uczestnicy kanonu w drabince: ... | gracz TAK/NIE` + wiesc EN (gdy gracz nie startowal). TournamentCancelled - `ODWOLANY`.

**Ryzyko / co sprawdzic.** (1) GT otwiera listy, gdy minely 4 doby od obwieszczenia (dzien zapisany z 2 miejscami po przecinku - otwarcie d125 albo d126). (2) Gregor
z Zachodu (ok. 310 jedn.) i Loras z Highgarden (ok. 300) jada ok. 4-5 dob z noclegami - od d117 zdazaja na d121-125. (3) Gra rozstrzyga turniej losowo po otwarciu (szansa
konca) - w oknie 125-127 to w porzadku. (4) Gracz w arenie w dniu 127 - niemozliwe w ticku doby (misja wstrzymuje kampanie).

**Status.** DO SPRAWDZENIA (autotest: linia ROZSTRZYGNIETY w d123-127, uczestnicy kanonu w drabince).

## K4 - klany gorskie Doliny (pkt 5)

**Problem.** Projekt pkt 2, dzien 118: klany gorskie napadaja na Gorskim Trakcie. W swiecie ROT nie ma klanow gorskich Doliny jako band (Burned Men, Milk Snakes,
Black Ears - brak; Stone Crows i Moon Brothers to pomniejsze frakcje, nie bandy - rozpoznanie e5:B).

**Zmiana (Prologue.World.Clans.cs).** Od dnia 118 do 186: najwyzej 4 bandy klanu bandyckiego `hilltribesmen` (Hill Tribesmen, kultura i oddzialy ROT) z nazwami
klanow z ksiazki (Party.SetCustomName: Stone Crows, Burned Men, Milk Snakes, Black Ears), dom bandy = wies na Gorskim Trakcie z najmniejsza liczba band sceny
(Stoneyhead, Durlston, Shady Glen, Hollow's Creek, Monton - wsie Orlego Gniazda i Krwawej Bramy). Wzor FreeFolkRaids: `OutlawLaw.RaidDraw` z okregu wsi (pula wyrzutkow,
potem domy wsi okregu - nikt z niczego), `BanditPartyComponent.CreateLooterParty` (AI band gry - patrol wokol domu, napady na podroznych), kiesa 0, `PeopleBook.Give`
(pochodzenie FPoolBand), `PeopleLedger.FixCreatedMen`; nieudane utworzenie - `OutlawLaw.RaidUndraw` (ludzie wracaja do puli). W dniu 118 do 4 band, potem uzupelnienie
po jednej co 3 dni. Bandy NIE sa zamrazane - to zwykle bandy (werbunek i jedzenie jak kazda banda). Po prologu (188) zostaja zwyklymi bandami (scena ich nie sledzi).
Prawo wyrzutkow wylaczone - bez ludzi, bez band (linia). Linie: `klany gorskie: nowa banda '<nazwa>' (<id>, klan hilltribesmen) przy <wies> (okreg ...) - N ludzi
(z puli ..., zabrani z domow ...) | w polu k/4`, w linii doby `klany gorskie: w polu k/4 (ludzi ...), od poczatku ...`.

**Ryzyko / co sprawdzic.** (1) Okreg Doliny moze miec mala pule - wtedy bandy biora z domow wsi (jak glod Wolnych Ludzi) albo nie powstaja (linia "za malo ludzi").
(2) Liczba band swiata rosnie o najwyzej 4 (twardy sufit sceny). (3) CommonerFor = lekki bandyta klanu (hilltribesmen_grunt), jak w RaidDraw.

**Status.** DO SPRAWDZENIA (autotest: linie "nowa banda" w d118-119, w polu >= 1 w oknie 118-186).

## K5 - ludzie Gory w Dorzeczu (pkt 6)

**Problem.** Projekt pkt 2, dzien 155: grabiezcy Lannisterow pala wsie Dorzecza - bez wypowiadania wojny krolestw (pokoj krola w prologu).

**Zmiana (Prologue.World.Raiders.cs).** Od dnia 155 dwie bandy klanu bandyckiego `robber_knights` (Robber Knights) z nazwa "The Mountain's Men" i sztandarem Lannisterow
(Party.SetCustomName / SetCustomBanner - rod clan_vlandia_1). Klan bandycki jest w stalej wojnie z kazdym krolestwem (DefaultDiplomacyModel.GetShallowDiplomaticStance),
wiec rabunek wsi Dorzecza idzie zwykla mechanika gry (EncounterManager: ShortTermBehavior RaidSettlement + IsAtWarAgainstFaction; precedens gry - bandy dezerterow
w zadaniu ExtortionByDeserters) i nie potrzeba wojny krolestw. Ludzie: `OutlawLaw.RaidDraw` z okregu wsi zrodla = wies twierdzy rodu Clegane (clan_vlandia_5) najblizszej
Riverrun (w ROT Harrenhal) - jak K4 (wzor FreeFolkRaids). Kazda banda: partia sceny (bez nowych decyzji, rozkaz `SetMoveRaidSettlement`) na najblizsza wies Dorzecza w stanie
Normal (nie cel drugiej bandy, nie spalona przez nas w ostatnich 20 dniach); po spaleniu (wies Looted po naszym rabunku) - nastepna. W marszu do pierwszej wsi ukryta przed AI
(wyjscie z ziem Clegane), potem widoczna - lordowie Dorzecza, Beric i gracz moga ja rozbic. Straty rabunku ksieguje gra i Armoury jak kazdy rabunek (DistrictRaid;
zlota bandy nie ma - SmallGold.PayRaidLoot wymaga wodza-bohatera). Dzien 188: `FreeFolkRaids.Home` (teraz internal): bohaterowie-jency uciekaja, lup do wsi zrodla (zloto
do kiesy, towar do magazynu), ludzie do puli okregu zrodla, banda rozwiazana. Klucz wylaczony w trakcie - bandy zostaja zwyklymi bandami (bez rozwiazania).
CampaignEvents.MobilePartyDestroyed - kto rozbil bande (linia; Beric liczy swoje zwyciestwa).
Linie: `ludzie Gory: nowa banda 'The Mountain's Men' (<id>, klan robber_knights, sztandar Lannister) przy <wies> ... | pierwszy cel <wies>`, `... nowy cel ...`, `... SPALONA wies
<nazwa> (<id>, riverlands) przez <id> ... | spalonych od poczatku N`, `... banda <id> ROZBITA albo rozwiazana (rozbili: ...)`, `... koniec prologu - <id> wraca do domow ...`.

**Ryzyko / co sprawdzic.** (1) Okreg Harrenhal - pula / domy; bez ludzi banda nie powstaje (linia), proba do d160. (2) Rabunek wymaga HP wsi > 0 i braku innej bitwy przy wsi
(PickRaidTarget to sprawdza). (3) Bandy na zywnosci BandFood i z werbunkiem OutlawLaw jak kazda banda (nie przesuwaja band - sprawdzone). (4) Sztandar Lannisterow vs tekst
sceny 19 "raiders flying no banners" - decyzja Jeffa (jedna stala w kodzie).

**Status.** DO SPRAWDZENIA (autotest: linie "nowa banda" d155-156, co najmniej jedna SPALONA wies do d186, powrot do domow w d188).
