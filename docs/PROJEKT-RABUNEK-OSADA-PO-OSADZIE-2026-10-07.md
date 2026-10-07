# PROJEKT: rabunek okregu osada po osadzie (07.10, projektant + krytyk; NIC NIE ZAKODOWANE)

Pomysl Jeffa 07.10: okreg = wiele osad, rabunek osada po osadzie, po kazdej okienko i pytanie "rabowac kolejna? zajmie N dni" (raczej dni - trzeba dojechac).
Projekt autora: SCRATCH 3cf3e0ac\dzien-6abunek-osady\PROJEKT.md (+ calc.py, calc2.py); krytyk: kryt.py, kryt2.py w scratchpadzie sesji fa2fd7a6.
KOLEJNOSC: dopiero PO paczce spichlerza (bez spichlerza napastnik nie ma z czego jesc) i po ksiedze ludzi 108-113.
OTWARTE: zgranie z przysiolkami-obrazkami (ok. 10 obrazkow na okreg, a osad po 250 ludzi srednio 243 -> obrazek = gromada ok. 24 osad).

## Dla Jeffa (tekst krytyka)

Wieś na mapie to okręg: zwykle ok. 60 tys. ludzi, czyli ok. 240 osad po ok. 250 osób (jak prawdziwa wieś). W menu wsi, także spalonej, jest „Look over the district”: ile osad stoi, ile spalono, ilu ludzi jest w domu, a ilu w ucieczce, na ile dni starczy spichlerz.

Rabujesz osada po osadzie. Dojazd do następnej to kilka godzin, resztę zajmuje przeszukanie, spędzanie ludzi i bydła, palenie. 50 ludzi potrzebuje ok. 10 dni na osadę, 112 ok. 4,5 dnia, 300 ok. 2 dni. Po każdej osadzie okienko: ilu zabito, ilu uciekło, ilu porwano (tylko ludy z niewolnictwem), co zabrałeś, ile stracił spichlerz, ile osad stoi. Na koniec pytanie: „Rabować następną? Zajmie ok. N dni”. Przerwać możesz zawsze.

Cały okręg w kilka tygodni spali tylko duża armia (2000 ludzi pali połowę w ok. 7 tygodni). Poczet 112 ludzi pali dziesiątą część w ok. 4 miesiące. Tak było naprawdę: wielkie najazdy paliły setki wsi, małe po kilka.

AI robi to samo, ale po jednej turze odjeżdża: lord pali jedną osadę w ok. 4,5 dnia, armia ok. 10 osad w 2,5 dnia. Na odsiecz masz kilka dni.

Po rabunku okręg jest „w strachu” (ikona spalonej wsi, bez wozów i podatku): 2 dni po jednej osadzie, do 17 po wielkiej armii. Produkcja nie spada do zera, tylko o tyle, ilu ludzi brakuje.

Łup to prawdziwy dobytek spalonych osad: mała część magazynu i kiesy wsi, a ludzie jedzą z jej spichlerza. Złota z niczego już nie ma, więc rabunek służy wojnie, nie zarobkowi. Osada stanie znowu, gdy wrócą jej ludzie (ok. 2 lata).

## Przyklad liczbowy

Okreg-mediana: 60 652 ludzi, 243 osady po 250. W zasiegu 218 osad (Room 90%). R = 0.5, oddzial D = 200, osady co 2.6 km, 20 km na dobe, f = 1. Rachunek: kryt.py i kryt2.py.

GRACZ, 50 ludzi
- Krok: 1 osada. Dojazd 3.1 h + przeszukanie i spalenie 240 h = 243 h = 10.1 doby.
- 5 osad pod rzad: 51 dob. 10% okregu: 268 dob; 25%: 2 lata; 50%: 4.9 roku.
- Zjedzone ze spichlerza w kroku: 25 sztuk (506 racji = 0.28% spichlerza osady).
- Gra dzis: 74.6 h i -24 tys. ludzi (-39.7% hearth). 113: 78 ludzi.

POCZET 112
- Krok: 1 osada. 3.1 h + 107 h = 110 h = 4.6 doby.
- 5 osad: 23 doby. 10%: 121 dob; 25%: 0.9 roku; 50%: 2.2 roku.
- Zjedzone: 26 sztuk.
- Gra dzis: 57.8 h. 113: 135 ludzi.

POCZET 300
- Krok: 1 osada (przy D=100 bylyby 3 osady w 5.1 doby). 3.1 h + 40 h = 43 h = 1.8 doby.
- 5 osad: 9.1 doby. 10%: 47 dob; 25%: 127 dob; 50%: 315 dob.
- Gra dzis: 40.3 h. 113: 251 ludzi.

DLA POROWNANIA
- Armia 2000: 10 osad w 2.6 doby; 50% okregu w 50 dob (ok. 7 tygodni).
- Inni 1000: 5 osad w 2.6 doby.

PO KAZDEJ OSADZIE (250 ludzi, parametry 113: 5% / 5% / 90%)
- Napastnik bez niewolnictwa: zabici 12.5, w las 12.5, uchodzcy 225.
- Kultura z niewolnictwem: zabici 12.5, jency 25 (do lochu; nadmiar to uchodzcy), w las 5-12.5, uchodzcy 200-207.
- Hearth -1.4. Plon okregu -0.37% (elastycznosc 1.0) albo -0.19% (0.5).
- Spichlerz traci udzial osady: pelny to 181 700 racji. Napastnik zjada z tego tylko swoje zuzycie (np. 26 sztuk zboza przy 112 ludziach), reszta splonie.
- Lup: 0.41% kazdej pozycji magazynu wsi, z tego polowa (x mnoznik) do napastnika, reszta splonie; 0.41% kiesy wsi do przywodcy.
- Gracz: okienko z liczbami i pytaniem "Raid the next settlement (about N days)" / "End the raid"; pasek stoi na 99%.
- AI: koniec sesji, Looted, nowy cel.

CZUJNOSC (Looted) PO SESJI
- 1 osada: 2 doby. 5 osad: 4 doby. Poczet 300 (1 krok): 2 doby.
- Inni 1000: 4 doby. Armia 2000: 6 dob. 5000 ludzi: 12 dob. Najwyzej 17.

MALY OKREG (Nocna Straz)
- 1 291 / 1 726 / 2 983 ludzi = 5 / 7 / 12 osad.
- Poczet 112 pali 1 osade w ok. 4.6 doby; to 8-20% okregu; czujnosc 17 / 16 / 10 dob.

ODBUDOWA: osada stoi znowu po ok. 2-2.4 roku (polowa uchodzcow wraca po 693 / 866 dobach).

## Projekt koncowy

PROJEKT KONCOWY PO KRYTYCE: rabunek okregu osada po osadzie (07.10). Autor: PROJEKT.md (SCR\dzien-6\rabunek-osady). Rachunki krytyka: kryt.py i kryt2.py w scratchpadzie sesji fa2fd7a6. SCR = C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad.

=== 0. WERDYKT KRYTYKA ===
Szkielet autora zostaje: okreg z osad po 250 ludzi, R x N x f x u, jedna ksiega 113, AI po jednym kroku, czujnosc 1+100d, bez "Looted po wszystkich".
Sprawdzilem twierdzenia w kodzie. Znalazlem 3 bledy, przez ktore gracz utknalby albo rabunek skonczylby sie zle, 1 regresje, 1 blad skali lupu i kilka nieprawdziwych twierdzen. Ponizej poprawki K1-K12; tekst projektu nizej juz je zawiera.

- K1 (BLAD: koniec po pierwszej osadzie albo zakleszczenie)
  - Pasek menu = 1 - punkty osady (VillageHostileActionCampaignBehavior.cs:636).
  - GameMenu.RunOnTick (GameMenu.cs:224-238) przy Progress >= 1 sam wola EndWait i konsekwencje menu oczekiwania.
  - Ta konsekwencja to wait_menu_end_raiding_on_consequence - TA SAMA metoda co opcja "End Raiding" (:99-100, :381-385), czyli PlayerEncounter.Finish.
  - Autor: punkty 1 -> 0 w kazdym kroku. Wtedy gra konczy rabunek gracza po pierwszej osadzie.
  - Prefiks autora "przy >=1 osadzie tylko flaga" blokowalby tez wlasciwy koniec (punkty 0 + wygrana). Gracz zostaje w menu z zatrzymanym zegarem, a "End Raiding" znow ustawia tylko flage.
  - POPRAWKA: w kroku punkty schodza 1 -> 0.01 (pasek do 99%). Na czas decyzji stoja na 0.01. Zero tylko przy koncu sesji. Prefiks konsekwencji przepuszcza oryginal, gdy MapEvent ma juz zwyciezce.
- K2 (BLAD: zakleszczenie po "End Raiding")
  - Opcja ma isLeave, wiec RunMenuOptionConsequence (GameMenu.cs:274-276) robi EndWait PRZED konsekwencja: zegar Stop, tick menu nie biegnie.
  - Sama flaga nigdy sie nie wykona.
  - POPRAWKA: prefiks przy >=1 spalonej osadzie robi EndSession (rozliczenie czesci kroku, punkty 0f, SetOverrideWinner(Attacker)), potem StoppablePlay + StartWait (wzorzec HideoutPurge.cs:287-294) i return false.
  - Nastepny tick menu: pasek 1 -> gra sama wola konsekwencje -> prefiks przepuszcza -> PlayerEncounter.Finish. Dalej dokladnie droga gry przy pelnym rabunku (PlayerEncounter.cs:1903-1914, OnMapEventEnded :64-83).
- K3 (REGRESJA: brak kary relacji)
  - RaidDamage rosnie tylko w pomijanym Update gry (RaidEventComponent.cs:193-194).
  - CharacterRelationCampaignBehavior.OnRaidCompleted (:465-489) liczy z niego kare: -6 x RaidDamage u pana wsi, -3 x RaidDamage u notabli.
  - Projekt autora = rabunek bez kary relacji.
  - POPRAWKA: w prefiksie OnBeforeFinalize, tuz przed RaidCompleted, RaidDamage = q (setter prywatny, AccessTools): q = 1 po >=1 spalonej osadzie, inaczej czesc kroku.
  - W trakcie sesji RaidDamage zostaje 0, wiec obroncy AI licza 4.01 doby dojazdu (AiMilitaryBehavior.cs:380-387). To pasuje do krokow 2-5 dob.
- K4 (BLAD SKALI: lup ze spichlerza)
  - Autor: napastnik bierze 50% spalonego spichlerza do udzwigu, 20 racji = 1 zboze, czyli do 9 085 sztuk z osady.
  - Spichlerz to ksiega 1:1 (racje ludzi). Towar wsi to symbol: PROJEKT-GLOD 1.6 - cala zywnosc w towarze wsi wykarmilaby 0.1-0.2 mln z 52.5 mln.
  - Nawet po obcieciu udzwigiem (20 wagi na czlowieka, DefaultInventoryCapacityModel.cs:75) to setki sztuk z niczego na krok, ktore trafia na targi.
  - Dla porownania ScorchedEarth daje armii 1 + N/250 zboza na dobe (ScorchedEarth.cs:98-99).
  - POPRAWKA: napastnicy JEDZA ze spichlerza okregu tyle, ile zjadaja. Przy kazdym 5% kroku kazda partia strony napastnika dostaje zboze rowne swemu zuzyciu za ten czas (-FoodChange x doby, RoundRandomized), zdjete ze spichlerza po 20 racji za sztuke (skala zolnierza, PROJEKT-GLOD 1.6), w granicach tego, co Famine.Burn w tym kroku spalil.
  - To ok. 0.3% spalonego spichlerza osady. Nic na sprzedaz ponad zuzycie.
- K5 (spojnosc ze 113: zasieg osad)
  - "Osady w zasiegu" i u licze z Room 113 (Devastation.cs:184-200: pulap 90% ORAZ dno 11 hearth), nie z floor(0.9n).
  - Inaczej w malym okregu (Nocna Straz, 5-12 osad) krok "pali" osade, a Strike zwraca 0 przez dno hearth.
- K6 (stan wsi ustawia gra, przed RaidCompleted)
  - Przy AttackerVictory i >=1 spalonej osadzie prefiks OnBeforeFinalize ustawia punkty 0f, wiec gra sama daje Looted (RaidEventComponent.cs:130-133) PRZED RaidCompleted.
  - Dzieki temu licza sie: SuccessfulRaids (CampaignWarManagerBehavior.cs:28 wymaga Looted w chwili zdarzenia), zadanie "Raid an enemy territory" (RaidAnEnemyTerritoryIssueBehavior.cs:389 wymaga punktow == 0f), i AI nie wraca na ponowny rabunek (AiMilitaryBehavior.cs:98-105).
  - Looted ustawiane w finalizerze (po RaidCompleted) gubiloby te liczniki.
  - Przy wygranej obroncy (odsiecz) czujnosci nie ma - KingdomManager.cs:225-233 i tak ustawia Normal. Punkty wracaja do 1.
- K7 (wyciete: woz w czasie sesji)
  - Prefiks TickVillageThink to dodatek "przy okazji" (CLAUDE.md 8.3). Partia wiesniakow rodzaca sie przy wsi z trwajacym MapEvent to nowe ryzyko.
  - Towar czeka w magazynie i jedzie po sesji (122: woz zabiera caly magazyn).
- K8 (bramki produkcji: czyje i ile)
  - Bramki naleza do paczki SPICHLERZA, bo tam jest poprawka Jeffa 07.10 "spladrowana wies produkuje wedle tego, co zostalo". Ta paczka ich wymaga, ale ich nie robi.
  - Sa 4, nie 3. Czwarta: BKVillageProductionModel.cs:59 (VillageState == 0). Bez niej BK w BeingRaided/Looted spada na wzor gry od hearth zamiast swojej sily roboczej.
  - Po zdjeciu zera produkcji upada uzasadnienie elastycznosci 0.5 w 113 ("utrate kapitalu niesie postoj Looted", 113-ludzie-spustoszenie.md:38). W tej samej paczce elastycznosc 1.0 (HISTORIA 2.6).
- K9 (menu okregu takze w spalonej wsi)
  - Gracz wjezdzajacy do wsi w czujnosci dostaje menu "village_looted" (PlayerTownVisitCampaignBehavior.cs:1113, :1124), nie "village".
  - Opcja okregu musi byc w obu.
- K10 (precedens okienka)
  - Okienko z ticku = wzorzec NightRest.cs:877-881 ("NIE otwieramy menu prosto z ticku... pytajka... bezpieczna sciezka UI").
  - HideoutPurge robi co innego: flaga + StartWait w opcji, SwitchToMenu/ExitToLast w ticku.
  - Okienko, a nie wlasne menu, bo zapis gry stoi wtedy w menu gry "raiding_village". Po cofnieciu DLL zapis nie wskazuje nieznanego menu.
- K11 (straznik w latkach 113, nie wyrejestrowanie)
  - Devastation.On wymaga _raidPatched && _endPatched (Devastation.cs:54-60).
  - Wyrejestrowanie RaidPrefix gasi CALE 113 (Strike, powroty, plon).
  - Pozostawienie bez straznika liczy ludzi drugi raz: RaidPrefix zbiera osobodni (:316-323), a EndFinalizer robi Settle -> Strike (:406).
  - POPRAWKA: pierwsza linia RaidPrefix i EndPrefix 113: if (DistrictRaid.Owns(__instance)) return;
- K12 (nieprawdziwe twierdzenia autora)
  - Gra zdejmuje 5% BIEZACEGO magazynu na krok 0.05 (:212), czyli ok. 64% po 20 krokach, nie 100%.
  - Przy milicji rabunek gry to 2 zdarzenia (walka, potem lupienie po SetMoveRaidSettlement, :146-158). Sa wiec 2 RaidCompleted i 2 VillageBeingRaided - tak samo dzis, ale nie "jedno na sesje".
  - ROT AIThinkPatch.cs:102 nie ma warunku IsCurrentlyAtSea. Rabunek z morza tez mysli co godzine i bywa przerywany.
  - Nekromancja ROT liczy upiory przy kazdym RaidCompleted, bez wzgledu na zwyciezce (ROTOthers.cs:857-897).
  - Cytaty linii RaidEventComponent: Update :160-320, OnInitialize :113-126, OnBeforeFinalize :128-144, OnFinalize :146-158 (u autora przesuniete o 1-2).

=== 1. LICZBA OSAD ===
- Wzor: n = max(1, round(L0 / S)), S = SettlementPeople 250 (ok. 55 dymow).
  - L0 = ludzie okregu (w domu + uchodzcy 113) przy pierwszym odczycie po kalibracji ludnosci; n zapamietane w arm_district.
  - Wielkosc osady S_v = L / n plywa z ludnoscia.
- Skad 250 (aneks 1.2/1.4, HISTORIA 2.1): Yorkshire 1319 - 190 ludzi na wies; Langwedocja 1355 - 200-450; parafia Francji 1328 - ok. 370; vill Domesday - ok. 130. 250 to wies zwarta z polami.
- Wyniki (calc-wynik.txt):
  - okreg-mediana 60 652 ludzi = 243 osady; 10%/90% okregow 63/622;
  - Nocna Straz 5-12, Wolni Ludzie 29, Polnoc 185, Reach 622 (najwiekszy 1 208); swiat 168 tys. osad.
- Grywalnosc: liczba osad to tylko liczba w menu. S zmienia czas jednej osady, nie tempo pustoszenia (zawsze R x N na dobe). Przy "10x wiecej wsi na mapie" okreg-mediana mialby ok. 24 osady.
- Stan bez nowego zapisu kazdej osady:
  - puste (spalone albo opuszczone) = round(n x uchodzcy / L), z konta u: 113; uchodzcy z zerowania i glodu tez oprozniaja osady, wiec menu mowi "burned or abandoned";
  - stojace w zasiegu Z = floor(Room / S_v) (Room 113);
  - u = Room / (0.9 x L), czyli ten sam iloraz co wykladnik 113 (Devastation.cs:237).
  - Z = 0: okreg wypalony (pkt 6).

=== 2. CZAS ROBOTY (krok) ===
- Krok = m osad naraz: m = min(Z, max(1, floor(N / D))), D = RaidDetachmentMen.
- ZMIANA KRYTYKA: D = 200 zamiast 100.
  - Czas kroku duzej sily = S / (R x D) i nie zalezy od N: przy D = 100 kazdy rabunek AI od 200 ludzi trwa 5.1 doby; przy D = 200 - 2.6 doby.
  - Zakres sesji AI od 100 ludzi wychodzi 1.3-5 dob, czyli "najazdy przechodzily przez okreg w 2-5 dni" (HISTORIA). Ryzyko tempa wojny dla armii spada o polowe.
  - D nie zmienia R ani tempa pustoszenia; zmienia tylko dlugosc kroku i wielkosc paczki w okienku.
- Dojazd = 2.6 km / sqrt(u) / 20 km na dobe = 3.1 h na swiezym okregu.
  - 2.6 km = rozstaw osad 250 ludzi przy 37 na km2; 20 km na dobe = tempo najazdu z lupem.
- Praca = ludzie m osad / (R x N x f x u), gdzie:
  - R = DevastationPerRaiderDay 0.5 (113, HISTORIA 2.1);
  - N = AttackerSide.TroopCount na biezaco;
  - f = ResultNumber / BaseNumber z CalculateHitDamage czynnego modelu, gdy BaseNumber > 0, inaczej poprzednie f. Obejmuje: perk gry NoRestForTheWicked (DefaultRaidModel.cs:46-58), BK 5 perkow +15% i palisade -12% za poziom (VanillaModelTweakPatches.cs:1167-1208), BKROT GodsFire +15% (BKROTRaidModel.cs:16-49), NavalDLC i BK DrakkarRaidMaster (NavalPerkPatches.cs:50-110). Wszystko to AddFactor, wiec iloraz jest czystym mnoznikiem.
- Postep liczony w ludziach przeszukanych: += R x N x f x u x dt.
  - Punkty osady = 1 - 0.99 x (postep kroku); pasek menu = postep.
- Wyniki:
  - 50 ludzi: 10.1 doby na osade;
  - 112: 4.6 doby;
  - 300: 1 osada w 1.8 doby;
  - 1000 (Inni): 5 osad w 2.6 doby;
  - 2000: 10 osad w 2.6 doby.
- Gra dzis (900/(sqrt N+5) h, -39.7% hearth = 24 tys. ludzi okregu-mediany): 74.6 / 57.8 / 40.3 / 18.1 h.
- 113 w tym samym czasie gry: 78 / 135 / 251 / 749 ludzi.

=== 3. CO DAJE SPALENIE JEDNEJ OSADY ===
Co 5% kroku i na koniec kroku: Devastation.StrikePeople(v, ludzie, osobodni, napastnik). To nowe wejscie do tego samego Strike (Devastation.cs:221-261); bez wzoru wykladniczego, jego role gra u.
- Trafieni: ludzie przeszukanej czesci, w granicach Room.
- Zabici: DevastationKilledPercent (113: 5%), najwyzej 1 na zbrojnego na dobe (:240).
- W las: DevastationOutlawPercent, do puli wyrzutkow (OutlawLaw.AddFled).
- Jency (NOWE): tylko gdy kultura frakcji napastnika jest na liscie niewolnictwa (decyzja Jeffa 07.10 pkt 6a i 5: bez Pentos i Lorath; lista z watku NIEWOLNI).
  - RaidCaptivePercent 10% trafionych jako villager_<kultura wsi> do lochu partii (jak BK, BKRaidCaptureBehavior.cs:363-378);
  - ponad PrisonerSizeLimit zostaja uchodzcami;
  - w ksiedze nowa pozycja "uprowadzeni" z regionem pochodzenia.
- Reszta to uchodzcy (konto u: 113).
- Hearth -trafieni/k (PeopleUnit.Shift). Plon okregu przez YieldFactor 113.
- Spichlerz: Famine.Burn(v, trafieni / L) raz, w Strike (PROJEKT-GLOD 2.2 pkt 6).
- Jedzenie napastnika (K4): zuzycie partii za ten czas, ze spalonego, po 20 racji za sztuke.
- Na koniec kroku lup:
  - s = ludzie kroku / ludzie w domu;
  - magazyn: kazda pozycja ItemRoster RoundRandomized(ilosc x s) zdjeta; kazda sztuka do partii napastnika z szansa 0.5 x GetRaidLootMultiplier, losowana wedle ludzi jak w grze (:218-240); reszta splonie;
  - kiesa: s x SettlementComponent.Gold do przywodcy przez GiveGoldAction.ApplyForSettlementToCharacter (GiveGoldAction.cs:52);
  - OnItemsLooted z prawdziwym lupem, wiec XP Roguery wedle wartosci.
- Bez 4 zl za hearth, bez towaru z produkcji i bez "zwyklego lupu" z niczego (:176-188, :272-301).
- Przerwany krok rozlicza sie w czesci: ludzie juz w ksiedze, lup za zrobiona czesc.

=== 4. GRACZ: MENU PO KAZDEJ OSADZIE I DECYZJA ===
Sesja na wies trzyma: przywodce, spalone w sesji, m, ludzi kroku, postep, "czeka na decyzje" i sumy do okienka.
1. Przed rabunkiem: menu okregu (pkt 8) mowi "With your N men, burning one settlement takes about D days".
2. Start jak dzis ("Raid the village", walka z milicja jak w grze). Komunikat: "Your men ride out to the first settlement of {VILLAGE} ({PEOPLE} souls). Burning it will take about {DAYS} days."
3. W trakcie: menu oczekiwania gry raiding_village.
   - Postfiks ticku (:504) uzupelnia VILLAGE_NAME: "{VILLAGE} - settlement {K}, about {X} days left".
4. Koniec kroku liczy prefiks Update w ticku kampanii: "czeka na decyzje", punkty stoja na 0.01, pasek 99% - gra NIE wola konsekwencji (K1).
   - Postfiks ticku menu (flaga _askOpen jak NightRest) pokazuje InformationManager.ShowInquiry z pauza:
   > {VILLAGE}: a settlement burns
   > Your men have searched and burned a settlement of {PEOPLE} souls. Killed: {K}. Taken captive: {C}. Fled to the castle and the neighbours: {R}. Gone to the woods: {O}. Loot: {ITEMS}; {GOLD} denars from the villagers' purse. The granary lost {DAYS_FOOD} days of food; your men ate {FOOD} of it. {STANDING} of {N} settlements still stand; the harvest will be {Y}% smaller until the people come back. The next settlement lies about {KM} km away; with {MEN} men burning it will take about {DAYS} days. If you end the raid now, the district stays on alert for about {ALERT} days.
   > [Raid the next settlement] [End the raid]
   - Armia: "Your detachments have burned {M} settlements...". Escape = "Raid the next settlement" - to odwracalne, "End Raiding" jest zawsze w menu.
5. "Raid the next settlement": nowy krok (m, u, dojazd wedle stanu okregu), punkty 1, StoppablePlay + StartWait.
6. "End the raid": EndSession (punkty 0f, SetOverrideWinner(Attacker)), StoppablePlay + StartWait.
   - Tick menu: pasek 1 -> RunOnTick -> konsekwencja gry -> prefiks przepuszcza (HasWinner) -> PlayerEncounter.Finish -> FinalizeBattle (HasWinner) -> OnBeforeFinalize: Looted, RaidCompleted(Attacker) -> village_player_raid_ended "You have successfully raided the village."
   - Postfiks village_player_raid_ended_on_init (:649) dopisuje: "{X} settlements burned in {D} days: ...". To dokladnie droga gry przy pelnym rabunku.
7. Opcja gry "End Raiding" w kroku:
   - po >=1 spalonej osadzie - jak pkt 6 (K2);
   - przy 0 osadach - jak w grze (odwolany, Normal, czesc kroku w ksiedze, punkty 1 w finalizerze).
8. Po wczytaniu w stanie "czeka": postfiks village_raid_game_menu_init (:344) robi StartWait (jak SearchInit HideoutPurge), tick pokazuje okienko znowu.
9. Gracz w armii AI: decyduje przywodca (regula AI); konczy sie tez przez konsekwencje (punkty 0) - prefiks przepuszcza.
10. Zakaz CLAUDE.md 7: zadnego SwitchToMenu z opcji menu oczekiwania; okienko nie przelacza menu; koniec zawsze droga gry z ticku.

=== 5. AI: ILE OSAD PALI, KIEDY PRZERYWA ===
- Jeden krok na rabunek, potem EndSession w ticku kampanii i finish (jak gra :307-313):
  - lord 112: 1 osada w 4.6 doby;
  - 300: 1 w 1.8;
  - Inni 1000: 5 w 2.6;
  - armia 2000: 10 w 2.6.
- Dalej gra: Looted (czujnosc), KingdomManager SetMoveModeHold (:213-223), AiMilitaryBehavior bez ponownego rabunku (punkty 0), nowy cel jak w grze.
- Przerywa wczesniej z powodow gry:
  - myslenie co godzine w rabunku (AiPartyThinkBehavior.cs:55-58, :111-124; ROT AIThinkPatch.cs:94-156, takze na morzu) -> FinalizeEvent;
  - odsiecz (walka w tym samym zdarzeniu, MapEvent.cs:1012-1022);
  - rozpad armii, zniszczenie.
  - Czesc kroku w ksiedze, bez czujnosci, punkty 1.
- Bez petli:
  - po pelnym kroku wies jest Looted 2-17 dob (AI rabuje tylko Normal, AiMilitaryBehavior.cs:424-431; obcy nie wchodza, EncounterManager.cs:146);
  - okreg wypalony (Z = 0) zostaje Looted, wiec AI go nie wybiera;
  - AI zaczyna tylko przy punktach > 0.001 (:222-229).
- Inni i Straz: ta sama regula (H1/H2 osobno). Rabunek z morza: ten sam komponent.

=== 6. STAN WSI GRY ===
- W sesji: BeingRaided (gra, OnInitialize).
  - Produkcja wedle rak - bramki z paczki spichlerza (K8).
  - Bez wozu (gra), podatek czeka w TradeTaxAccumulated (DefaultClanFinanceModel.cs:242, :472 nie pobiera; dopisuje VillagerCampaignBehavior.cs:336).
- Po sesji z >=1 spalona osada i AttackerVictory: Looted = czujnosc.
  - Czas: ceil(1 + 100 x d) dob, d = trafieni sesji / L, najwyzej 17 (aneks 1.4; tick dobowy).
  - Wartosci: 1 osada 2 doby; poczet 300 2; Inni 1000 4; armia 2000 6; 5000 12.
  - Produkcja wedle rak; bez wozu, podatku, renty (PopulationLaw), przyrostu (GrowthOf) i powrotow (ReturnRate); AI tu nie rabuje.
- Wygrana obroncy, pokoj, AI przerywa, gracz odwoluje przy 0 osadach: Normal, punkty 1, bez czujnosci.
- Koniec czujnosci: nasz Daily po Devastation.Daily -> ChangeVillageStateAction.ApplyBySettingToNormal + punkty 1.
  - Bez +20 milicji z niczego (IncreaseSettlementHealthAction.cs:7-16), bo prefiks VillageHealCampaignBehavior.DailyTickSettlement (:16-31) pomija wsie w czujnosci i wypalone.
- Okreg wypalony (Z = 0): Looted, az Z >= 1; ReturnRate przyjmuje tam powroty po czujnosci.
- NIE "Looted dopiero po wszystkich", bo:
  - 243 osady = stan nie zdarzylby sie nigdy;
  - AI rabuje tylko Normal i stalby w jednym okregu miesiacami;
  - SuccessfulRaids, zadanie rabunku, Diplomacy i SCA145 licza rabunek po Looted / punktach 0.

=== 7. ODBUDOWA ===
- Puste osady maleja z powrotem uchodzcow 113: 0.1% dziennie x (1 - niebezpieczenstwo); x0.25 przy glodnej warowni; 0 w BeingRaided, Looted i przy oblezeniu.
- Osada stoi znowu po ok. 2-2.4 roku: polowa po 693 / 866 dobach.
- Zabici, jency i wyrzutki nie wracaja na konto uchodzcow - odrabia to przyrost.

=== 8. MENU OKREGU ===
- Opcja "Look over the district" w "village" ORAZ "village_looted" (K9). Prowadzi do zwyklego menu arm_district; przelaczenie z opcji zwyklego menu jest bezpieczne (wzorzec IronBank). Powrot do menu, z ktorego weszlo.
- Tresc (po angielsku):
  - osady: stoi / puste / poza zasiegiem;
  - ludzie w domu i w ucieczce, zabici, uprowadzeni;
  - dni spichlerza (% pelnego) i plon %;
  - ostatni rabunek (kto, ile osad), czujnosc (ile dob);
  - "A settlement holds about S people. With your N men, burning one would take about T days" - tylko wies wroga.

=== 9. ZGODNOSC (jedna regula, bez podwojnego liczenia) ===
- 113:
  - Strike to jedyny ruch ludzi; zerowanie armii bez zmian. ScorchedEarth pomija partie w MapEvent (ScorchedEarth.cs:70), wiec napastnik nie zeruje podwojnie.
  - Straznik w RaidPrefix/EndPrefix (K11) zostawia Devastation.On i cala droge 113 dla wylaczonej reguly i dla wsi bez przelicznika.
  - Nasz prefiks Update przejmuje faze lupienia (DefenderSide.TroopCount == 0) i nie puszcza gry: brak -39.7%, lupu gry i zlota z niczego.
  - Faza walki, brak napastnikow i LeaderParty null idzie do gry / MapEvent.
- Nasluchy RaidCompleted:
  - prefiks OnBeforeFinalize podmienia hearth na hearth x 0.975^(20q) na czas metody (jak 113, :373-420) i ustawia RaidDamage = q;
  - widza to: ROT nekromancja hearth/2, BKROT piety = hearth i +10 renomy (BKROTBehavior.cs:78-108), relacje gry;
  - kazdy widzi poziom rabunku gry raz na sesje (H2 przepisze nekromancje na zabitych).
- OutlawLaw "3% przy spaleniu": juz wylaczone przy 113 (OutlawLaw.cs:409).
- Spichlerz: Famine.Burn raz, w Strike; jedzenie napastnika wewnatrz spalonego. Bramki produkcji (4) i elastycznosc 1.0 w paczce spichlerza (K8).
- BK:
  - postfiks BKRaidCaptureBehavior.FeatureEnabled (:135-138, prywatna) = false przy czynnej regule; znikaja jency i zloto skim z niczego (:324-399) oraz opcje Take/Leave;
  - modyfikatory tempa BK dzialaja przez f;
  - ksiega chlopow BK i -8% BetterEconomy to inne ksiegi (aneks 1.8 pkt 5).
- ROT: AI Innych omija Looted/BeingRaided (ROTOthersCampaignBehavior.cs:415); _raidLocks bez zmian.
- NavalDLC: perki i polityki przez f; rabunek z morza ten sam.
- Gra: WasEverInLootingPhase = true jak :164; SetLevelMaskIsDirty na koncu; RaidCompleted/VillageBeingRaided jak dzis (z milicja po 2).

=== 10. ZAPIS, LOG, MCM ===
- Zapis: klucz tekstowy arm_district w ArmouryBehavior.SyncData - n wsi, czujnosc do (doba), uprowadzeni wg regionu, sesje (id wsi -> pola z pkt 4).
  - Brak klucza = sesja od nowa (postep z punktow). Reset w konstruktorze.
- Log co dobe "Rabunki (osady):":
  - sesje w toku (gracz / AI), kroki, osady, trafieni = zabici + w las + jency + uchodzcy;
  - lup (sztuki, zl), spichlerze (spalone, zjedzone);
  - konce sesji wedle przyczyny (krok AI, myslenie AI, odsiecz, gracz koniec / odwolany, wypalony);
  - czujnosc (wsi, srednio dob), srednio dob na krok i napastnikow, sesje AI dluzsze niz 10 dob.
  - tools/sprawdz_logi.py - nowe wzorce.
- MCM (napisy po angielsku, python tools/gen_mcm.py), 8 ustawien: RaidBySettlementEnabled (wl.), SettlementPeople 250, RaidDetachmentMen 200, SettlementSpacingKm 2.6, RaidMarchKmPerDay 20, RaidAlertDaysPer1Percent 1.0, RaidAlertMaxDays 17, RaidCaptivePercent 10.
  - R = istniejace DevastationPerRaiderDay.
  - Wylaczone = rabunek jak w 113.

=== 11. KOLEJNOSC ===
1. 113 wgrane i sprawdzone.
2. Spichlerz (krok A) z 4 bramkami i elastycznoscia 1.0.
3. Ta paczka: autor + niezalezny recenzent; proba na prawdziwym ciele Update / OnBeforeFinalize; zamkniecie rachunku ludzi i lupu; autotest 40 dob.
4. Gra Jeffa.
Bez spichlerza nie wlaczac: napastnik nie mialby z czego jesc.

## Gdzie w kodzie

GRA (SCR\ore-supply\cs):
- RaidEventComponent.cs:
  - Update :160-320 (lupienie :162-315; hearth :172-175; zloto :176-190; punkty i RaidDamage :192-194; magazyn 5% biezacego :204-271; towar z niczego :272-301; koniec :307-313; brak napastnikow :316-319);
  - OnInitialize :113-126; OnBeforeFinalize :128-144 (Looted gdy punkty <= 1e-5, RaidCompleted); OnFinalize :146-158.
- GameMenus\GameMenu.cs:
  - RunOnTick :224-238 (Progress >= 1 -> EndWait + konsekwencja) - K1;
  - RunMenuOptionConsequence :266-283 (isLeave -> EndWait przed konsekwencja) - K2;
  - StartWait :285-290.
- VillageHostileActionCampaignBehavior.cs:
  - menu :99-100 (konsekwencja oczekiwania = konsekwencja "End Raiding");
  - init :344-355; :381-385; tick :504-514; UpdateWaitMenuProgress :636-639; village_player_raid_ended_on_init :649; OnMapEventEnded :64-83.
- PlayerEncounter.cs: start :1801-1810; FinalizeBattle :1903-1914; wyniki nie dla rabunku :1362; dolaczenie odsieczy :779-800.
- MapEvent.cs: Update :995-1046; BattleState :284-304; SetOverrideWinner :1058-1066; WasEverInLootingPhase :249-264.
- Settlement.cs: SettlementHitPoints :256-257 (internal set); IsRaided :422; IsUnderRaid :446.
- CharacterRelationCampaignBehavior.cs:465-489 (kara relacji z RaidDamage) - K3.
- KingdomManager.cs:209-235 (Attacker -> Hold, Defender -> Normal).
- CampaignWarManagerBehavior.cs:28; RaidAnEnemyTerritoryIssueBehavior.cs:389; NotablePowerManagementBehavior.cs:39-45.
- AiMilitaryBehavior.cs: :91-104 (ponowny rabunek gdy punkty != 0); :376-387 (obroncy: 4.01 doby przy RaidDamage 0); :424-431 (cel tylko Normal).
- AiPartyThinkBehavior.cs:47-124 (w rabunku co godzine, FinalizeEvent :111-124).
- EncounterManager.cs:146, :222-229; DefaultTargetScoreCalculatingModel.cs:290-298.
- VillageHealCampaignBehavior.cs:16-31; IncreaseSettlementHealthAction.cs:7-16; ChangeVillageStateAction.cs; Village.cs:57-77 (zdarzenia stanu).
- DefaultRaidModel.cs:46-58; GiveGoldAction.cs:52; DefaultInventoryCapacityModel.cs:75.
- Bramki produkcji (do paczki spichlerza): DefaultVillageProductionCalculatorModel.cs:20, :78; VillageGoodProductionCampaignBehavior.cs:141; BKVillageProductionModel.cs:59.
- Podatek: DefaultClanFinanceModel.cs:242, :472; VillagerCampaignBehavior.cs:336.

MODY:
- BK: BKRaidCaptureBehavior.cs:135-138 (FeatureEnabled), :193-237, :324-399; VanillaModelTweakPatches.cs:1167-1208; NavalPerkPatches.cs:50-110.
- BKROT: BKROTRaidModel.cs:16-53; BKROTBehavior.cs:78-108.
- ROT:
  - SCR\ore-supply\ROTOthers.cs:857-897 (upiory przy kazdym RaidCompleted);
  - SCR\dzien-6\glod\rot\ROT.CampaignBehaviors\ROTOthersCampaignBehavior.cs:286, :415;
  - ROT.HarmonyPatches.Core\AIThinkPatch.cs:94-156 (bez IsCurrentlyAtSea).
- BetterEconomy: VillageSupplyCampaignBehavior.cs:94-115.
- SCA145: StrategicCampaignAI145Behavior.cs:22, :101, :110.

ARMOURY (baza paczki-na-120/113-ludzie-spustoszenie + spichlerz; zrodla SCR\dzien-6\rabunek-osady\src113):
- NOWY Armoury\src\DistrictRaid.cs:
  - stan, Reset, Export/Import (arm_district), Owns(raid);
  - prefiks Update (return false w lupieniu, finish po EndSession);
  - prefiks + finalizer OnBeforeFinalize (czesc kroku, punkty 0f przy AttackerVictory i >=1 osadzie, RaidDamage = q przez AccessTools.PropertySetter, podmiana hearth dla nasluchow, czujnosc, punkty 1);
  - postfiks wait_menu_raiding_village_on_tick (okienko, VILLAGE_NAME);
  - postfiks village_raid_game_menu_init (StartWait po wczytaniu);
  - prefiks wait_menu_end_raiding_on_consequence (przepuszcza przy HasWinner; przy >=1 osadzie EndSession + StartWait + return false);
  - postfiks village_player_raid_ended_on_init;
  - prefiks VillageHealCampaignBehavior.DailyTickSettlement;
  - postfiks BKRaidCaptureBehavior.FeatureEnabled (AccessTools.TypeByName);
  - menu arm_district + opcje w village i village_looted;
  - Daily, log.
- Devastation.cs:
  - StrikePeople (Room, jency, Famine.Burn, jedzenie);
  - straznik Owns w RaidPrefix (:305) i EndPrefix (:373);
  - Room do Z i u;
  - ReturnRate (:473-486) dla wypalonych po czujnosci.
- PeopleLedger.cs: "uprowadzeni", CSV osady / puste / czujnosc / uprowadzeni.
- ArmouryBehavior.cs: Reset, Daily po Devastation.Daily, SyncData, menu w OnSessionLaunched.
- SubModuleMain.cs: ApplyAll po Devastation.ApplyAll (wlasny try).
- Settings.cs + tools/gen_mcm.py; tools/sprawdz_logi.py.
- Famine.cs (spichlerz): Burn zwraca spalone racje; Take(v, racje) dla jedzenia napastnika.
- Precedensy:
  - NightRest.cs:877-881 - okienko z ticku;
  - HideoutPurge.cs:287-294, :460-470, SearchInit StartWait - zegar;
  - IronBank - menu.

## Ryzyka

1. Tempo wojny.
   - Rabunek lorda 4.6 doby zamiast 2.4; armii 2.6 zamiast 0.75 (przy D=100 byloby 5.1).
   - Pomiar w autoteście 40 dob: doby w rabunku na lorda, oblezenia.
   - Pokretlo: RaidDetachmentMen - skraca tylko krok duzych sil, R zostaje.
2. Przerywanie AI. AI mysli co godzine w rabunku (AiPartyThinkBehavior.cs:55-58; ROT AIThinkPatch.cs:94-102, takze na morzu). Przy krokach 2-5 dob czesc sesji skonczy sie przed spaleniem osady (czesc w ksiedze, bez czujnosci). Log wedle przyczyny konca.
3. Spustoszenie swiata ok. 2x szacunku 113 (wiecej dob lupienia AI), dalej setki razy mniej niz gra. Pomiar.
4. Cykl AI:
   - lord 4.6 doby + 2 doby czujnosci = sesja co ok. 7 dob na wies (gra: 9-19), wiec pogranicze dostaje gesciej;
   - partie < 100 ludzi: krok 5-25 dob - pomiar; jesli sa, osobna zmiana "AI nie zaczyna, gdy krok > 10 dob" (nie dopisywac teraz).
5. Lup i przychody:
   - prawie zero zlota i towaru (0.4% magazynu i kiesy na osade);
   - rody AI traca przychod z rabunkow (kiesy rodow w wojnie);
   - XP Roguery z wartosci lupu prawie znika;
   - rabunek gracza nieoplacalny (zold 2-10 dob na osade) - zmienia gre Jeffa, mowi o tym for_jeff.
6. Kolejnosc. Bez spichlerza napastnik nie ma z czego jesc, a w zwyklym lupie jedzenia jest ulamek. Nie wlaczac przed paczka spichlerza.
7. CTD i menu. Do sprawdzenia na zywo:
   - pasek stoi na 99% przy okienku (gra nie wola konsekwencji);
   - "End the raid" -> village_player_raid_ended;
   - "End Raiding" po 1 osadzie i przy 0 osadach;
   - wczytanie w stanie decyzji (StartWait w init, okienko znowu);
   - armia gracza, gracz w armii AI;
   - odsiecz w trakcie kroku i w trakcie okienka (pauza);
   - czy napis VILLAGE_NAME odswieza sie co klatke;
   - Escape w okienku.
8. Bramki produkcji (paczka spichlerza): 4 miejsca (z BKVillageProductionModel.cs:59), IL jednego odczytu stanu, owijka NavalDLC; w dymku tylko linia rak.
9. Nasluchy RaidCompleted dostaja poziom rabunku gry raz na sesje, ale sesji (i przerwanych sesji) moze byc wiecej:
   - ROT: upiory hearth/2 przy kazdym RaidCompleted, tez przegranym - z niczego do watku H2 (decyzja Jeffa 07.10);
   - BKROT: piety = hearth i +10 renomy.
   - Opcja: H2 dla rabunku w tej samej latce (upiory = zabici sesji) - tylko za zgoda na laczenie zmian.
10. Relacje: kara -6/-3 za sesje (RaidDamage = q); przy gestszych sesjach relacje spadaja szybciej niz dzis.
11. BK:
    - znika przelacznik Take/Leave; gracz kultury z niewolnictwem bierze jencow zawsze;
    - lista kultur z watku NIEWOLNI (ThrallCultures + Essos, bez Pentos i Lorath);
    - villager_<kultura> moze nie istniec, wtedy fallback jak BK.
12. Jency w lochu:
    - sprzedaz u posrednika okupu to zloto z niczego (gra);
    - werbunek jencow zrobi z "uprowadzonych" zolnierzy bez zapisu w ksiedze;
    - do watku NIEWOLNI (R12/R13).
13. Czujnosc 1+100d to wartosc projektu, nie liczba ze zrodla.
14. Binarny stan wsi czytaja tez nasze prawa:
    - PopulationLaw.Danger (udzial Looted/BeingRaided regionu), GrowthOf (przyrost 0 calego okregu), renta (ok. :1011);
    - OutlawLaw (nedza :511, :545);
    - BetterEconomy (-8% chlopow BK i przerwa dowozu 10-28 dni na kazde VillageBeingRaided, z milicja 2 razy).
    - Kazda sesja (BeingRaided 2-5 dob + czujnosc) wstrzymuje przyrost i rente CALEGO okregu. Rzad podobny jak dzis (gra 9-19 dob), ale niespojne z "symbolem".
    - Osobna zmiana: zastapic stan udzialem pustych osad.
15. Zapis i cofniecie DLL:
    - stan sesji w arm_district; brak klucza = sesja od nowa;
    - po cofnieciu DLL gra dokonczy rabunek z punktow (pila), a wsie w czujnosci wyleczy w 8-17 dob (+20 milicji);
    - zapis stoi w menu gry, nie w naszym.
16. Odsiecz wygrana przez napastnika po >=1 osadzie daje czujnosc - gracz nie moze od razu dalej rabowac tego okregu (swiadomie, prosto).
17. Punkty osady jako pasek kroku czyta cel AI (x(1+(1-punkty))) i GetValue; skutki drobne.
18. Male krainy. Okreg Nocnej Strazy to 5-12 osad: poczet 112 pali 8-20% okregu w 4.6 doby, czujnosc 10-17 dob (skala 1:1 jak w 113).
19. Wydajnosc: jedno wolanie modelu rabunku na tykniecie, 20 Strike na krok, petla partii napastnika przy jedzeniu i lupie.
20. Elastycznosc plonu 1.0 (w paczce spichlerza) podwaja ubytek plonu od rak wobec dzis - zmierzyc w linii "Ludzie (spustoszenie)".
