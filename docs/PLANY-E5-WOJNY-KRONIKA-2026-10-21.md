# Plany 21.10: E5 (sceny zmieniajace swiat), wojny wedlug ksiazki + smoki, Kronika Westeros (wf_d4f0c951-09f, tylko odczyt)

## PLAN e5

**Podsumowanie:**

Moja ocena: E5 da się zrobić w całości w zamkniętej ekonomii. Główne twierdzenia badaczy A i B potwierdziłem w kodzie i w dekompilacji, ale oba plany trzeba poprawić w 8 miejscach. Niczego nie zmieniałem w repo, drzewach ani w grze.

SPRAWDZIŁEM SAM:
1) Flaga "bez nowych decyzji" (MobilePartyAi.DoNotMakeNewDecisions, [SaveableProperty(11)]) idzie do zapisu gry. Wzór na zdejmowanie jej przed zapisem i przywracanie po nim już działa (FreeFolkRaids.cs:307-372, podpięcie w ArmouryBehavior.cs:1626). Wyłączenie AI przez ucztę BK zapisuje się na stałe (DisableAi ustawia termin "Never" w polu zapisu SaveableProperty(3)).
2) Niewola. EndCaptivityAction.ApplyInternal(Hero prisoner, EndCaptivityDetail detail, Hero facilitatior=null, bool showNotification=true) to prywatna metoda statyczna; przez nią idą wszystkie zwolnienia. Okup jest płacony PRZED zwolnieniem: w grze RansomOffer.cs:178-179, w naszym Ransom178.cs:425. Okup gry rusza tylko z RansomOfferCampaignBehavior.DailyTickHero (RansomOffer.cs:64-69). Dlatego straż musi stać przed przelewem złota, jak chce B.
3) BK. BKFeastBehavior.HourlyTickPartyImpl robi DisableAi() i SetMoveGoToSettlement. Do tego BKGentryBehavior.OnPartyDailyTick robi DisableForHours(72) i wysyła partię do majątku. A tego nie wymienił.
4) Beric. Bractwo bez Sztandarów to w ROT banici (is_minor_faction, is_outlaw, kultura vlandia). Królestwo korony też ma kulturę vlandia, więc według IsAtConstantWar (DefaultDiplomacyModel:1091) Bractwo jest w stałej wojnie z koroną.
5) GrandTourney. Wpis turnieju ma 4 pola. Odczyt pozwala na piąte (p.Length > 3), ale TourneyBehavior.cs:519 przepisuje wpis bez piątego pola. NoblesFightPatch bierze tylko wodzów partii i bohaterów bez partii, najwyżej 15 lordów w drabince.
6) Postacie kanonu. CanonCompanions.Captor przyjmuje id osady. Gdy postać prowadzi własną partię lorda, LeaveParty ją rozwiązuje (StartDisband).

POPRAWKI DO PLANÓW A i B:
1) Baza to w-toku/n31-calosc, a nie m103-sceny: n31 zawiera już m103-sceny i lektora. Stare gałęzie w-toku/e5-* to inna sprawa (ekonomia), więc nowe nazywam m104-*.
2) A i B robią każdy własne zdejmowanie flagi przed zapisem. Zamiast dwóch kopii: jeden rejestr partii prowadzonych skryptem, Prologue.Hold.
3) IslandRoads.cs:534 już łata tę samą metodę uczty BK. Jego postfiks nic nie robi, gdy BK w tym wywołaniu nic nie rozkazał, więc nasz prefiks jest bezpieczny, ale trzeba to wpisać do CHANGELOG.
4) Gentry BK obsłuży ogólna reguła na cudze blokady AI, z licznikiem w logu.
5) Scena 15 ma nagranie prolog_15.wav (z md5 w arm_lektor.tsv, n31). Zmiana tekstu zrobi rozjazd z nagraniem, więc tekstu nie ruszamy.
6) Jeśli CanonCompanions wykona wiersz dla postaci, której partię trzyma orszak (Tyrion, wiersz p01), najpierw trzeba tę partię puścić. Inaczej zamrożona partia zostanie rozwiązana.
7) Gra wypuszcza jeńca także przy pokojowej zmianie właściciela osady (ReleasedAfterBattle). B to przepuszcza: przyjmujemy to, z linią w logu.
8) TransferPrisonerAction zmienia tylko listę jeńców. Po przeniesieniu sprawdzić w logu, gdzie jest postać.

KOLEJNOŚĆ: P1 jeńcy fabularni (tor M) -> P2 orszak (L) -> P3 Turniej Ręki (M) -> P4 klany, Ludzie Góry, Beric (L) -> E6 autotest 230 dób z wczytaniem zapisu -> n32-calosc -> wgranie po teście. Razem około 2 nocy z testami.

Kronika (117 wydarzeń, lektor, kilka obrazków na zmianę) to osobny tor. Wojny Essos, Reach-Dorne i smoki też są osobno. E5 nie dodaje nowych scen; zmienia tylko porę sceny 15, a to czeka na decyzję Jeffa.

**Plan:**

=== 0. ZASADY, GAŁĘZIE, KOLEJNOŚĆ ===
- Baza: w-toku/n31-calosc (48833d54; zawiera m103-sceny, m103-lektor, m103-drobne). Drzewo robocze zb/m104-*.
- Łańcuch gałęzi (wszystkie ruszają Prologue.cs, PrologueBehavior, Settings.cs i wygenerowane McmSettings, więc osobne gałęzie od n31 dałyby konflikty):
  w-toku/m104-jency (P1) -> w-toku/m104-orszak (P2 + P3, osobne commity i osobne autotesty) -> w-toku/m104-swiat (P4) -> scalenie do w-toku/n32-calosc (tools/scalanie/scal.sh).
- Każdy krok:
  - commit i wpis w CHANGELOG (Problem / Przyczyna / Zmiana / Ryzyko / Status);
  - python tools/gen_mcm.py, potem ./build.sh z kodem 0;
  - testy syntetyczne, potem autotest z flagą armoury-prolog.flag;
  - przed PIERWSZYM autotestem skopiować wszystkie logi Jeffa z dnia;
  - autotest tylko wtedy, gdy Jeff nie gra.
- Wgranie: dopiero po E6, na stałej zgodzie (kopia, gra zamknięta).
- Kolejność z punktem 5 Jeffa ("testuj gospodarkę"): gra jest jedna. Test gospodarki na zbudowanym n31 (kandydat WGRANIA 102) puścić przed autotestami E5; kod E5 pisać w tym czasie.
- Kronika, wojny Essos i Reach-Dorne oraz smoki to inne tory, nie mieszać.

=== P0 (pierwszy commit P2): wspólny rejestr partii prowadzonych skryptem ===
Nowy plik Armoury/src/Prologue.Hold.cs (partial Prologue).
- Metody:
  - Hold(MobileParty mp, string owner, string why) - SetDoNotMakeNewDecisions(true) i wpis w słowniku mp -> owner;
  - Release(mp, why) - flaga false, Ai.RethinkAtNextHourlyTick = true, linia "zwolnione";
  - ReleaseOwner(owner, why), IsHeld(mp), ReleaseHero(Hero h) - puszcza partię, której h jest wodzem.
- Zapis:
  - BeforeSave() / SaveOver() / Refreeze() jak FreeFolkRaids.cs:307-372 (w bitwie albo oblężeniu tylko flaga);
  - podpięcie w PrologueBehavior na OnBeforeSaveEvent i OnSaveOverEvent;
  - po wczytaniu flaga wraca w pierwszej godzinie.
- Korzystają z niego: orszak (P2), oczekujący na turniej (P3), Ludzie Góry i Beric (P4). Żadnych osobnych kopii BeforeSave.
- Linia doby: "trzymane partie N (orszak a, turniej b, Ludzie Gory c, Beric d)".

=== P1 w-toku/m104-jency (plan B krok 1, tor M) ===
Pliki:
- Armoury/src/CanonCompanions.cs (static -> static partial);
- nowy Armoury/src/CanonCompanions.Captives.cs;
- Armoury/ModuleData/arm_canon_moves.tsv;
- Settings.cs.

1) TSV:
   - nowa akcja "captive:<cel>[,<cel2>]", dopisana w nagłówku i do KnownActions/ArgActions (CanonCompanions.cs:76-79);
   - p01 captive:town_EN1;
   - m01 captive:town_V2,clan_battania_1 (Riverrun, decyzja Jeffa).
2) Execute "captive" obok "prisoner" (ok. linii 1234):
   - pierwszy ważny cel z listy: nie osada gracza, bez oblężenia, MapFaction różne od h.MapFaction;
   - h już jeńcem nie gracza -> TransferPrisonerAction.Apply(h.CharacterObject, h.PartyBelongedToAsPrisoner, cap). Sprawdzone: zmienia tylko listę jeńców, więc linia logu Where(h) po przeniesieniu;
   - w przeciwnym razie ścieżka jak "prisoner", ale PRZED LeaveParty: Prologue.ReleaseHero(h) (pkt 6 z poprawek);
   - na koniec _held[klucz] = wiersz | osada | dzień.
3) Koniec trzymania (_held.Remove i linia logu):
   - przed Execute każdego późniejszego wiersza tej postaci (p02 free, m02 free) - wtedy nasze ApplyByReleasedByChoice przechodzi;
   - śmierć, PlayerCaptor(h) albo !Captive(h);
   - ReleasedAfterBattle przepuszczone: odbicie, zdobycie osady, także pokojowa zmiana właściciela (PrisonerReleaseCampaignBehavior.OnSettlementOwnerChanged). Powód w linii.
4) Straże, Harmony raz na proces w CanonCompanions.ApplyAll (SubModuleMain), klucz czytany na żywo:
   a) prefiks EndCaptivityAction.ApplyInternal (parametry prisoner, detail):
      - dla trzymanego blokuje ReleasedAfterEscape, Ransom, ReleasedAfterPeace, ReleasedByChoice i ReleasedByCompensation, z licznikiem dla każdego rodzaju;
      - przepuszcza Death i ReleasedAfterBattle;
   b) prefiks RansomOfferCampaignBehavior.DailyTickHero(Hero) - jedyne miejsce, skąd rusza ConsiderRansomPrisoner, więc złoto nie popłynie ani w okupie gry, ani w Ransom178;
   c) postfiks CampaignEventDispatcher.OnBarterablesRequested(BarterData):
      - usuwa SetPrisonerFreeBarterable i TransferPrisonerBarterable dla trzymanego;
      - pole _prisonerCharacter czytać jak Ransom178._fPrisoner (Ransom178.cs:359).
5) Zapis: wiersze "held\t<klucz>\t<wiersz>\t<osada>\t<dzień>" w Export/Import arm_canon_companions; Import czyści _held.
6) MCM: CanonCompanionsCaptivesHeld = true (grupa Canon Companions), hint EN z planu B.
7) Linie logu (bez polskich znaków):
   - raz na dobę, gdy coś jest trzymane: "Jency fabularni (E5): Tyrion Lannister [ROTwanderer8] w lochu The Eyrie (town_EN1) od dnia 110 | zablokowane dzis: ucieczka a, okup b, pokoj c, barter d, inne e (razem ...) | klucz TAK.";
   - przy zdjęciu: "... koniec trzymania (#p02 free / smierc / odbity / zmiana wlasciciela osady / gracz).".
8) Testy syntetyczne m104jency:
   - parsowanie captive z listą celów;
   - Export/Import held;
   - straż: Escape i Peace zablokowane, Death i AfterBattle przechodzą, własne free przechodzi.
9) Autotest T1: prolog 140 dób, wczytanie zapisu w dniu ok. 120.
   Progi:
   - Tyrion jeńcem w town_EN1 w każdej linii doby 110-131;
   - 0 uwolnień poza #p02; #p02 done w dniu 132;
   - po wczytaniu held przetrwał;
   - 0 bloków ERROR IN A MOD z Captives; potknięcia 0.
   Jaime w Riverrun (m01) sprawdzimy dopiero w E6, bo wojna Północy rusza po starcie ROT.

=== P2 w-toku/m104-orszak, część A1 (plan A, tor L) ===
Pliki:
- nowy Armoury/src/Prologue.Road.cs;
- Prologue.cs: Daily() woła RoadDaily(); PrologueBehavior podpina HourlyTickEvent -> RoadHourly() oraz zapis i wczytanie;
- Prologue.End.cs: TryKingFalls;
- CanonCompanions.cs: internal static Hero CanonHero(string tpl), tylko żywi;
- Settings.cs.

1) Silnik tras według planu A pkt 1-6 (nogi z osadą i najwcześniejszym dniem odjazdu, tryb Go albo Escort), na Prologue.Hold.
   Rozkazy:
   - tylko przez wejście metody: MethodInfo SetMoveGoToSettlement / SetMoveEscortParty, jak GT TourneyBehavior.SetMoveGo:274;
   - potem Hold(mp, "orszak");
   - partia w armii: wódz -> DisbandArmyAction.ApplyByUnknownReason, członek -> Army = null;
   - cudza blokada AI (Ai.IsDisabled, np. uczta BK DisableAi albo gentry BK DisableForHours(72)) -> EnableAi() i licznik "cofniete cudze rozkazy" z podziałem na BK uczta / BK gentry / inne.
2) Straż godzinowa (tylko własna lista, bez pętli po partiach świata):
   - pomija partie w bitwie, w oblężeniu i śpiące (NightRest.SleepKind != SkNone);
   - inny rozkaz niż nasz -> wydać ponownie i policzyć;
   - mp.IgnoreForHours(1.5f);
   - przyjazd = CurrentSettlement == cel albo najwyżej 2 jedn. od bramy;
   - 24 h bez ruchu -> "utknal" i rozkaz od nowa; po 3 dobach trasa zwolniona;
   - bohater nie jest już wodzem tej partii -> Release.
3) Harmonogram według planu A pkt 6, stałe kalibruje Claude z T2:
   - postoje tylko w miastach i wsiach (jedzenie);
   - Tyrion kończy trasę w Crossroads Inn (village_EN3_1) w dniu 108 i zostaje zwolniony przed p01 (dzień 110), tam, gdzie w książce pojmuje go Catelyn;
   - król bez partii w dniu 0 -> czeka do dnia 3 z linią "krol bez partii - czeka", potem "pominiety".
4) Zwolnienia:
   - ReleaseOwner("orszak") w TryKingFalls zaraz po sprawdzeniu Busy(robert), przed ChangeClanLeaderAction i ArrestNed;
   - w Daily przy dniu 188 i przy wyłączonym kluczu;
   - partia króla zniszczona albo król w niewoli -> "przerwany: <powod>".
5) Łatka BK w InstallRest (ApplyRoad):
   - prefiks BannerKings.Behaviours.Feasts.BKFeastBehavior.HourlyTickPartyImpl(MobileParty): return !(Active && IsHeld(party));
   - IslandRoads.cs:534 łata ten sam cel. Jego FeastPostfix nic nie robi przy liczniku 0 - sprawdzone, wpisać w Ryzyko;
   - linia "latki prologu: ... | uczta BK dla orszaku TAK/BRAK".
6) Zapis: klucze ro= (nogi), rm= (przeniesieni) w arm_prologue (Pack/Unpack); ResetRoad() w ResetCampaign.
7) MCM: PrologueRoyalColumn = true, hint EN z planu A.
8) Linie logu, tag "Prolog (E5 orszak): " - start / przyjazd / odjazd / zwolnione / przerwany według planu A pkt 10, plus fragment w linii doby: "orszak: faza, noga, cofniete cudze rozkazy dzis N (BK uczta / BK gentry / inne), bitwy orszaku 0, utknal 0".
9) Testy syntetyczne m104orszak: Pack/Unpack ro= i rm=, kolejność nóg.
10) Autotest T2: prolog 100 dób, zapis i wczytanie w dniu ok. 40 (postój w Winterfell) i ok. 70 (w drodze).
    Progi:
    - "start" w dobie 0-1, eskorta >= 1;
    - Winterfell: przyjazd w dniach 28-38, odjazd 55-56; KL: przyjazd w dniach 85-100;
    - bitwy orszaku bez gracza 0; utknął 0;
    - cofnięte cudze rozkazy na partię na dobę <= 6;
    - po wczytaniu partie lordów z flagą bez wpisu w Hold = 0;
    - wydarzenia ROT w prologu 0; straż godzinowa < 1 ms.

=== P3 ta sama gałąź, osobny commit: TURNIEJ RĘKI (plan A2, tor M) ===
GrandTourney/src/TourneyBehavior.cs:
- public static string ProclaimRoyal(string townId, string hostHeroId, string entrantIdsCsv) - według planu A.
  - Wpis "id|dzien|0|0|R:<host>:<e1>,..." dodać PRZED CreateTournament i AddTournament.
  - Linia 519 i każde inne przepisanie wpisu mają ZACHOWAĆ piąte pole.
  - Dla wpisu R bez LocalWhenAtWar i bez MinLordsToHold.
  - Komunikat dla gracza EN: "By order of {HOST}, a great tourney is proclaimed at {TOWN}. The lists open in {N} days.".
- internal List<Hero> RoyalEntrants(Town).
- SummonLords pomija mp.Ai.DoNotMakeNewDecisions.
GrandTourney/src/Patches.cs, NoblesFightPatch.Postfix przy turnieju królewskim:
- najpierw uczestnicy kanonu obecni w mieście, także jako członkowie cudzej partii;
- warunki: zdolni, dorośli, nie gracz;
- miejsce: najpierw wypełniacz, potem ostatni lord spoza kanonu i spoza rodu gracza;
- linia "Szranki X: kanon w drabince k/m (brak: <kto> - <powod>)".
Uczestnicy kanonu (stała lista): Gregor lord_4_21, Loras lord_1_44, Ogar ROTwanderer3, Jaime ROTwanderer1.
Armoury, nowy plik Prologue.Tourney.cs (tag "Prolog (E5 turniej): "):
- d112: Gregor i Loras jadą trasą Go do KL i czekają (Hold);
- d121: przeniesienie uczestników bez partii, wywołanie odbiciem ProclaimRoyal, stan ht=1 dopiero po powrocie z wywołania;
- brak GT albo stary DLL -> "turniej Reki pominiety (brak GrandTourney.ProclaimRoyal)";
- TournamentFinished / TournamentCancelled dla town_EW4 -> ht=2, zwolnienie oczekujących, linia "zakonczony dzien X | zwyciezca Y | kanon w drabince k/m | gracz startowal TAK/NIE";
- zapas: zwolnienie w dniu 140.
MCM: PrologueHandTourney = true.
Scena 15 (indeks 14), jeśli Jeff zgodzi się na wariant zalecany:
- tekst i nagranie bez zmian;
- scena czeka na koniec turnieju jak SceneArrest, najdłużej do dnia 137 (inaczej blokuje sceny 16 i 17);
- prawdziwego zwycięzcę ogłasza GT swoim komunikatem.
CHANGELOG GT. DLL GT i Armoury wgrywać razem.
Autotest T3: prolog 140 dób.
Progi:
- ogłoszony w dniu 121 +-1, otwarty w dniu 125 +-1 jako królewski (nie LOKALNY), koniec najpóźniej w dniu 137;
- kanon w drabince >= 3/4 (poza martwymi i jeńcami);
- trzymane partie z puli "turniej" = 0 po dniu 140.

=== P4 w-toku/m104-swiat (plan B kroki 2-5, tor L) ===
Nowy Armoury/src/Prologue.World.cs. Stan w osobnym kluczu SaveText "arm_prl_world" (limit napisu 32 KB). ResetWorld() w ResetCampaign. Doba i godzina tylko po własnej liście (najwyżej 5 obiektów).
1) Klany górskie Doliny od dnia 118 (S.PrologueValeClans) według planu B krok 2:
   - klan mountain_bandits; ludzie z OutlawLaw.RaidDraw, region castle_EN2, zapas town_EN1; CreateLooterParty;
   - nazwy "{=ARM_PRL_CL1}Burned Men", "{=ARM_PRL_CL2}Milk Snakes", "{=ARM_PRL_CL3}Black Ears";
   - najwyżej 3 bandy w polu, 1 nowa na dobę, nowe do dnia 181;
   - Prologue.OwnsBand obok FreeFolkRaids.Owns: BandFood.cs:149 i :308, OutlawLaw.cs:1334;
   - koniec: FreeFolkRaids.Home -> internal static i wywołanie zamiast kopii;
   - linia "Klany gorskie Doliny (E5): ..." z kontrolą wyruszylo = wrocilo + straty + w polu.
2) Ludzie Góry od dnia 155 (S.PrologueMountainRaiders) według planu B krok 3:
   - z załogi Harrenhal (ROT_town32), w załodze zostaje >= 30%, minimum 20 ludzi; PeopleBook.Transfer z przyczyną FFromGarrison;
   - klan looters, nazwa "{=ARM_PRL_MTN}The Mountain's Men", Hold(mp, "gora");
   - SetMoveRaidSettlement przez wejście metody;
   - cele: Sherrer, Mummer's Ford, Wendish Town, potem najbliższa wieś Dorzecza; wsie rodu gracza pomijane;
   - powrót w dniu 188 do załogi (FToGarrison);
   - linia "Ludzie Gory (E5): ..." z kontrolą m0 = wrocilo + straty + w polu.
3) Oddział Berica od dnia 160 (S.PrologueBericHunt) według planu B krok 4:
   - bohater z decyzji Jeffa (zalecany: wzór ROTuniqueleader_spc_brotherhood_0, zapas ROTclan_58.Leader);
   - Hold(p, "beric"); SetMoveEngageParty na najbliższą partię Ludzi Góry, a bez celu patrol wokół ROT_castle39_village1;
   - bez tworzenia ludzi;
   - linia "Oddzial Berica (E5): ...".
4) Jeśli Jeff się zgodzi: jeden postfiks Prologue.KinPost na DefaultMobilePartyAIModel.ShouldConsiderAttacking (priority Last, po FreeFolkRaids.KinPost).
   - Do dnia 188 partie królestwa vlandia nie atakują Ludzi Góry ani oddziału Berica i odwrotnie.
   - Ustawia tylko __result = false dla tych par; działa pod kluczami 2) i 3).
5) MCM (grupa Prologue 298): PrologueValeClans, PrologueMountainRaiders, PrologueBericHunt, hinty EN z planu B.
6) Testy syntetyczne m104swiat: Pack/Unpack arm_prl_world, rachunki ludzi.
7) Autotest T4: prolog 200 dób, wczytanie zapisu w dniu ok. 165.
   Progi:
   - E5B-VC: w dniach 118-120 >= 1 banda; w polu <= 3; w dniach 188-190 w polu 0; rachunek ludzi się zgadza;
   - E5B-MR: partia >= 20 ludzi z załogi; >= 1 rabunek do dnia 187 (linia DistrictRaid dla wsi Dorzecza, 0 ERROR IN A MOD); w dniach 188-218 m0 = wrocilo + straty;
   - E5B-BE: Beric znaleziony albo linia powodu; puszczony w dniu 188;
   - po OnBeforeSave 0 partii z flagą.

=== E6: TEST CAŁOŚCI ===
- Prolog 230 dób, wszystkie klucze TAK, zapis i wczytanie w dniach ok. 40, 120 i 165.
- Krótki bieg klasyczny 20 dób (nic z prologu nie rusza; straże P1 tylko w kampaniach z flagą CanonCompanions).
- Krótki bieg prologu 20 dób z kluczami E5 na NIE (zero ruchu orszaku).
- Progi wszystkich pakietów oraz:
  - w dniu 188 trzymane partie 0;
  - KONIEC PROLOGU w dniu 186 nieopóźniony;
  - po #m01 (ok. dnia 205+) Jaime jeńcem w town_V2 albo powód;
  - wojny Westeros z udziałem korony w prologu 0;
  - potknięcia prologu 0.
- Potem n32-calosc: scal.sh, build z kodem 0, md5, wgranie na stałej zgodzie (kopia, gra zamknięta, Jeff nie gra), CHANGELOG Armoury i GT, STAN-PRAC.

=== CZEGO NIE ROBIĆ ===
Bez Army, bez własnego PartyComponent, bez wojska i złota z niczego, bez puli złota korony, bez zmiany tekstu sceny 15 (nagranie), bez pętli po wszystkich partiach świata.

**Ryzyka:**

- Zapis gry: flaga DoNotMakeNewDecisions ([SaveableProperty(11)]) i blokada uczty BK (_enableAgainAtHour, [SaveableProperty(3)], DisableAi = Never) trafiają do zapisu. Bez wspólnego Prologue.Hold.BeforeSave cofnięcie DLL zostawi króla, Ludzi Góry i Berica zamrożonych na zawsze. Próg po wczytaniu: 0 partii z flagą bez wpisu.
- Okup i barter: złoto płynie PRZED zwolnieniem (RansomOffer.cs:178-179, Ransom178.cs:425). Gdyby straż stała tylko na ApplyInternal, okup byłby zapłacony, a jeniec zostałby w lochu, czyli złamana zamknięta ekonomia. Dlatego potrzebne są prefiks DailyTickHero i postfiks OnBarterablesRequested.
- Po zablokowanym ApplyByPeace gra i tak wysyła OnPrisonersChangeInSettlement(takenFromDungeon: true) (PrisonerReleaseCampaignBehavior:118). Naszych słuchaczy brak (grep Armoury, RC, CrashScribe); słucha RansomOfferCampaignBehavior:206 i może BK. Przy każdym 'pokoj' w liczniku sprawdzić, czy nic nie liczy wyjęcia z lochu.
- Gra wypuszcza jeńca także przy pokojowej zmianie właściciela osady (OnSettlementOwnerChanged -> ApplyByReleasedAfterBattle). Straż to przepuszcza: Tyrion albo Jaime wyjdą, gdy BK lub Diplomacy przekażą Orle Gniazdo albo Riverrun. Rzadkie, ale musi być linia powodu.
- Ten sam cel w BK łatają dwie łatki: IslandRoads.FeastPrefix/FeastPostfix (IslandRoads.cs:534) i nowy prefiks orszaku. Dziś bezpieczne, bo postfiks nic nie robi przy liczniku 0. Każda zmiana IslandRoads musi to uwzględniać.
- Przepychanka rozkazów: uczta BK co godzinę, gentry BK co dobę (DisableForHours(72)), SummonLords GT, alarm NightMarch. Bez licznika z podziałem na źródła nie zobaczymy nowego przypadku; próg <= 6 na partię na dobę.
- Tyrion: jeśli trasa orszaku nie puści jego partii przed wierszem p01, CanonCompanions.LeaveParty rozwiąże zamrożoną partię (StartDisband). Stąd wywołanie Prologue.ReleaseHero(h) przed każdym Execute.
- Prędkość kolumny (obóz nocny, trakty AiRoadRoutes, śnieg) jest nieznana. Dni przyjazdu mogą odbiegać od 35 i 95, więc stałe harmonogramu kalibruje T2. Scena 15 czeka na koniec turnieju najdłużej do dnia 137; inaczej opóźni sceny 16 i 17.
- Lektor: prolog_15.wav ma md5 w arm_lektor.tsv (n31). Zmiana tekstu sceny 15 daje rozjazd głosu z tekstem albo nowe płatne nagranie z rezerwy. Dlatego zostawiamy tekst, a prawdziwego zwycięzcę ogłasza GT.
- Beric z ROT to banita w stałej wojnie z koroną (DefaultDiplomacyModel.IsAtConstantWar; brotherhood ma kulturę vlandia jak korona). Bez reguły KinPost partie korony będą go bić w drodze, a Lannisterowie będą rozbijać Ludzi Góry (klan looters), sprzecznie z książką.
- Ludzie Góry (banda bez bohatera) rabują ścieżką gry (EncounterManager:221). DistrictRaid nie był testowany z wodzem bez bohatera, a NavalKingdomPolicyCampaignBehaviour.OnRaidCompleted jest nieprzeczytany. Pierwszy rabunek obejrzeć w logu. Milicje i lordowie Dorzecza mogą rozbić bandę, zanim zdąży cokolwiek zrabować.
- Turniej: gra może sama wpuścić Roberta i Neda do drabinki jako wodzów partii w KL. Wtedy przegrany płaci wykup (zasada GT), a unikatowe zbroje kanonu mogą zmienić właściciela. Stary DLL GT zgubi piąte pole wpisu (turniej stanie się zwykły), więc GT i Armoury wgrywać razem.
- Wojny ROT w prologu (Dolina - Żelazne Wyspy, Reach - Dorne, decyzja Jeffa) i ich pokoje odpalają ReleasePrisonersInternal. To oczekiwane 'pokoj' w liczniku straży, nie błąd. Wojny z udziałem korony w prologu muszą wynosić 0, bo inaczej GT LocalWhenAtWar i pokój orszaku nie działają.
- Zmiany P1 działają tylko w kampaniach z flagą CanonCompanions (nowe kampanie od WGRANIA 101). Obecna kampania Jeffa, jeśli powstała wcześniej, nie dostanie straży ani Riverrun.
- Jeden autotest naraz i tylko wtedy, gdy Jeff nie gra. Kolejka testów (gospodarka z punktu 5 plus T1-T4 i E6) to kilka długich biegów. Przed pierwszym trzeba skopiować logi Jeffa, bo rotują.

**Pytania:**

- Beric: wziąć bohatera ROT z Bractwa bez Sztandarów (banita, w stałej wojnie z koroną) czy głowę rodu Dondarrion (ROTlord_243 'Eric Dondarrion', postać wymyślona przez ROT, lojalna wobec korony)? Zalecam Bractwo plus regułę z pytania 2. Bez decyzji robię wariant zalecany pod kluczem PrologueBericHunt.
- Reguła 'swoi nie biją swoich' do dnia 188: partie korony i Lannisterów nie atakują Ludzi Góry ani oddziału Berica i odwrotnie (jeden postfiks ShouldConsiderAttacking, wzór FreeFolkRaids.KinPost). Zalecam TAK; bez niej Lannisterowie rozbiją 'swoich' bandytów.
- Scena 15 (Turniej Ręki): zostawić tekst i nagranie lektora (prolog_15.wav), pokazać ją po zakończeniu turnieju (najpóźniej w dniu 137), a prawdziwego zwycięzcę podać komunikatem GT? Zalecam TAK. Zmiana tekstu oznacza nowe nagranie albo rozjazd głosu z tekstem.
- Turniej królewski: wykluczyć Roberta (gospodarz) i Neda (Ręka, sędzia) z drabinki, jak w książce? Zalecam TAK, bo inaczej król płaci wykup za przegraną walkę na arenie.
- Jaime w Riverrun: trzymać go tak jak Tyriona (bez ucieczki, okupu, barteru i wypuszczenia przez pokój aż do #m02 po Czarnym Nurcie)? Zalecam TAK, pod tym samym kluczem; przy 4% ucieczki na dobę uciekłby po ok. 25 dniach.
- Kolejność z punktem 5 ('testuj gospodarke'): najpierw test gospodarki na zbudowanym n31 (kandydat WGRANIA 102), a w tym czasie kod E5, potem autotesty E5? Zalecam tak, bo gra jest jedna.

## PLAN wojny

**Podsumowanie:**

TOR WOJNY: krytyka i plan. Tylko odczyt: repo, drzewa i gra nietkniete, nic nie budowalem.

NAJWAZNIEJSZE: kod juz jest. Galaz w-toku/m104-wojny (drzewo zb/m104-wojny, baza 756e685f) ma trzy commity z 21.10 12:14-12:21:
- K1 cda60892 - Essos na kalendarzu;
- K2 8e0d7fec - Reach i Dorne z korona;
- K3 8220104e - mlode smoki.
Brakuje K4 (grupa sprawdz_logi m104wojny) i K5 (build 5 modow, raport). Plan ponizej to wiec: przeglad tego kodu, dokonczenie, testy i scalenie, a nie pisanie od zera.

SPRAWDZILEM SAM (dekompilacja i pliki):
1. Wojny fabularne ROT:
   - ROTStorylineWar.Enforced czyta CampaignStartTime 2 razy, EnforceWars ma bramke "ElapsedDaysUntilNow < 1f". Transpiler Prologue.Story podmienia wszystkie odczyty na start opowiesci, wiec w prologu ROT nie wymusza zadnej wojny.
   - W OnNewGameCreated jest 8 wojen czysto essoskich 5/100.
   - Pokoj krola (Prologue.Peace, Realms = 11 krolestw Westeros) przepuszcza pary essoskie.
2. Sojusze w grze 1.4.8:
   - AllianceCampaignBehavior.StartAlliance nie sprawdza CanMakeAlliance ani wojen. Od razu sklada ProposeCallToWarAgreementDecision dla kazdej wojny jednej strony z krolestwem, z ktorym druga strona nie walczy. Dlatego pokoj Reach-Dorne musi byc PRZED sojuszem.
   - CrashScribe CanonAlliances.Allowed: Korona + Reach dozwolone po smierci Renly'ego.
3. Smoki:
   - Zestaw ghi_bat_template_dany ma w slocie konia dragon_black.
   - ROT stawia smoka w bitwie tylko wtedy, gdy przedmiot w slocie konia ma "dragon" w id (AIDragon.SpawnMissingDragonMounts), wiec slot to jedyna dzwignia.
   - Smoka z powrotem daje tylko ROTGankBehavior przy wyjsciu z niewoli z pustym slotem.
   - U Jeffa w MCM ROT: DragonHealth 10000, DragonDamageScaling 10.
   - Nowych kluczy nie ma jeszcze w Armoury.json, wiec obowiazuja wartosci domyslne.

OCENA KODU:
- K1 jest lepszy od projektu badacza A. Nie ma wyjatku w bramce EnforceWars ani postfiksu "false". W prologu wojny essoskie wypowiada doba Armoury, a wojny Westeros 15/16 same zostaja niewymuszone.
- Swiadome odejscie od listy Jeffa z 10:16: wojny Qarth-Daenerys i Dothrakowie-Daenerys czekaja na opowiesc.
  - Za tym przemawia kanon: Daenerys jest w prologu khaleesi Drogo (sceny dni 12, 145, 172), a Kronika nr 5 wiaze rozpad khalasaru ze startem tej wojny w dniu opowiesci 22.
  - Polecam to zostawic i powiedziec Jeffowi.
- K2: kolejnosc krokow jest dobra, Dorne ma tylko pakt (nie sojusz). Trzy luki:
  - brak wylacznika dla Kroniki nr 95 (Sept Baelora). Olenna zostaje glowa tego samego rodu, wiec sojusz bylby odnawiany az do najazdu Daenerys (ok. 1949);
  - Kronika nr 19 i nr 27 maja inne daty i skutki niz kod;
  - brak diagnostyki wypowiedzen wojny mimo paktu.
- K3: bezpieczny, bez latek bitwy. Mlody smok jest jednak tylko mniejszy i wolniejszy. HP ROT ustawia kazdemu smokowi na DragonHealth, czyli 10000. Przy DragonDamageScaling 10 u Jeffa to HP decyduje o sile, nie ogien. Proponuje K3b: samo HP w MissionLogic, po tescie bitwy, bez latki na RegisterBlow.

Tor M, ok. pol dnia: 1-1,5 h kodu i narzedzia, reszta to autotesty.

**Plan:**

=== 0. GALAZ I KOLEJNOSC ===
- Dalej w-toku/m104-wojny (drzewo .../scratchpad/zb/m104-wojny, baza 756e685f, HEAD 8220104e). Nowej galezi nie zakladac.
- Kolejnosc: K4 -> K5 -> build -> T1-T5 -> CHANGELOG -> scalenie z m104-e5 do n32-calosc (od n31-calosc 48833d54) -> test koncowy razem z E5 i gospodarka czesc 3 (decyzja Jeffa nr 1 "zrobisz na koncu") -> wgranie.
- Kazda czesc pod swoim kluczem, wiec przy bledzie wylaczamy klucz, nie cofamy DLL.

=== 1. K4 - JEDNA POPRAWKA KODU (diagnostyka K2, tylko log) ===
Plik Armoury/src/Prologue.Wars.cs (CanonWarsBehavior.RegisterEvents) i Prologue.Crown.cs.
- Dopisac CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared). Typ zdarzenia (IFaction, IFaction, DeclareWarAction.DeclareWarDetail) sprawdzic w dekompilacji 1.4.8.
- Prologue.CanonWarDeclared(f1, f2, detail) liczy tylko 3 pary:
  - vlandia-empire_w, gdy _rcHeld;
  - empire_w-aserai, gdy _rcHeld albo _rnOurs;
  - vlandia-aserai, gdy _cdOurs.
- Linia: "Wojny kanonu (m104): WYPOWIEDZENIE mimo kanonu: <f1>-<f2> (<DeclareWarDetail>), dzien opowiesci N - pokoj od nowa nastepnej doby".
- Licznik _wDeclDay w linii doby: "| wypowiedzenia mimo kanonu dzis k".
- Cel: zobaczyc, kto omija pakt Diplomacy (wezwania do wojny, ROT EnforceWars, AIInfluence, gracz). Twarda blokade dopisac dopiero, gdy log pokaze naruszenia.
- Nic wiecej. Wylacznik Kroniki nr 95 i gettery dla Kroniki dochodza dopiero w paczce Kroniki (pkt 6).

=== 2. K5 - tools/sprawdz_logi.py --grupa m104wojny (stala M104W_TAG = "Wojny kanonu (m104): ") ===
Wzor m103sceny_main. Nazwa grupy i stale musza byc unikalne wobec galezi m104-e5 (pulapka scalania: ta sama nazwa grupy w dwoch galeziach).

Progi:
- W-0: linia "start sesji" = 1 na sesje.
  - Prolog: "(1) Essos na kalendarzu: klucz TAK, latka Enforced TAK, na kalendarzu 6".
  - Klasyczny: "kampania klasyczna - nie dotyczy".
- W-1: ERROR "CanonWars." = 0, "potkniecia dzis 0 (razem 0)".
- W-2 (prolog): "latka wojen essoskich (...): ROTStorylineWar.Enforced TAK" = 1 na proces.
- W-3 (prolog): w dobach kalendarza 23-188 linia doby ma "w wojnie 6/6" w >= 95% dob. Mniej dopuszczalne tylko przy zniszczonym krolestwie, bo pokoj blokuje IsWarForced.
- W-4: "NIE weszla (DeclareWarAction bez skutku)" = 0; "ESSOS - wojna ... WYPOWIEDZIANA" najwyzej 1 na pare na 30 dob.
- W-5 (prolog, regresja): PR-5 m101prolog "wojny Westeros-Westeros 0" bez zmian; w "zablokowane wypowiedzenia" zadnej pary essoskiej.
- W-6 (klasyczny): 0 linii "ESSOS - wojna".
- W-7 (smoki): linia "smoki Daenerys - <why>: etap" najwyzej 1 przy starcie sesji i najwyzej 1 na zmiane etapu. Zmiana w 2 kolejnych dobach bez zmiany etapu = PING-PONG z innym modem, wynik NIE.
  - Prolog: start z "etap jaja", siodlo bez "dragon_".
  - Klasyczny przed dniem 779: "etap smoczeta".
- W-8 (proba Reach/Dorne, T5):
  - "DORNE Z KORONA" = 1;
  - "REACH Z KORONA" = 1, a w tej samej linii "pokoj Reach-Dorne" przed "SOJUSZ korona-Reach ZALOZONY", gdy byla wojna Reach-Dorne;
  - "SOJUSZ NIE wszedl" = 0;
  - "ODNOWIONY" = 0 w 30 dobach;
  - "WYPOWIEDZENIE mimo kanonu" = 0 (przy > 0 tylko ostrzezenie z DeclareWarDetail);
  - CrashScribe SOJUSZE: vlandia + empire_w bez "[poza kanonem]" i bez zerwania przy wczytaniu (CanonAlliancesBreakOnLoad).

=== 3. TRYB PROBY DO T5 (Armoury, flaga armoury-m104-proba.flag) ===
Wzor: armoury-proba-zimy.flag i armoury-zima-teraz.flag. Ta sama DLL, ktora pojdzie do gry.
- Flaga czytana raz na sesje, linia: "Wojny kanonu (m104): TRYB PROBY (flaga) - progi skrocone".
- Skrocone progi:
  - MyrcellaDay = dzien opowiesci 5;
  - D smokow = 30, czyli etapy w dniach 12, 19, 26;
  - dzien 8: ROT SubModule.EventBehavior.TriggerEvent(typ RenlyDeathEvent) odbiciem (prawdziwa droga: OnRenlysDeath ROT i CanonReachNorthWar CrashScribe). Gdy zwroci false: KillCharacterAction na lord_1_9 i linia, ktora droga poszla.
- Skrypt testu usuwa flage po biegu. Flaga u Jeffa = linia TRYB PROBY w kazdej sesji, wiec latwa do wylapania.

=== 4. TESTY (autotest tylko, gdy Jeff nie gra; przed 1. biegiem kopia WSZYSTKICH logow Jeffa z dnia) ===
- T1 prolog 230 dob (armoury-prolog.flag, ok. 45 min):
  - W-0..W-5, W-7;
  - dzien kalendarza 188 = dzien opowiesci 0: etap smoczeta i komunikat "three dragons have hatched";
  - regresja: m101prolog i m103sceny jak w biegu 101.
- T2 prolog z zapisu (103-prolog-wczyt): arm_m104 przezywa wczytanie (dm, ds, rc, cd), linia startu po wczytaniu, brak podwojnych wypowiedzen.
- T3 klasyczny 160 dob z kopii zapisu Jeffa (103-160):
  - W-6;
  - Drogon zdjety przy 1. sesji: kon z taboru albo "pieszo", z liczba przypadkow "brak konia w taborze";
  - Reach "czeka (Renly zyje)", Dorne "czeka (zareczyny Myrcelli w dniu 303)".
- T4 bitwa (bieg-bitwa.ps1 -Battles), tylko jesli da sie postawic bitwe z partia Daenerys:
  - etap smoczeta: w bitwie brak smoka;
  - etap mlode z flagi proby: dragon_green jako wierzchowiec bez bledow siodla i kamery;
  - 0 wyjatkow ROTDragon.
  Jesli sie nie da: zapisac, ze K3 w bitwie nie byl sprawdzony, i nie robic K3b.
- T5 proba 40 dob klasyczny z flaga: W-8; zapis i wczytanie w dobie 20, potem SK bez zerwania; "do dnia" sojuszu przesuwa sie przy < 182 dniach (w probie wymusic pierwsze przedluzenie albo sprawdzic sama linie startu "koniec sojuszu (UpdateAllianceEndTime) TAK").
- Progi wspolne: 0 ERROR m104; czas doby (DayProfile) bez wzrostu > 2%; wgranie dopiero po udanym tescie koncowym, z kopia.

=== 5. K3b (WARUNKOWO, po T4; decyduje Claude, bez pytania Jeffa o liczby) ===
- Gdy T4 przeszedl: nowy plik Armoury/src/Prologue.DragonsField.cs, MissionLogic dodawana w OnMissionBehaviorInitialize tylko przy kluczu DaenerysDragonsGrow i etapie mlode albo podrosle.
- OnMissionTick co 0.5 s: smok (MonsterUsage dragon/dragonfly) z jezdzcem lord_1_14, ktorego Index jeszcze nie ruszalismy. HealthLimit = BaseHealthLimit = ROTSettings.DragonHealth (odbiciem) x 0.30 (mlode) albo x 0.65 (podrosle), Health = min(Health, nowe). Lapie tez nowego agenta po TransformDragonForm.
- Linie: "Wojny kanonu (m104): smok Daenerys w bitwie HP X -> Y (etap)" i "koniec bitwy: smokow skalowanych n".
- BEZ latki Agent.RegisterBlow i BEZ latki ROTCombatSimulationModel.SimulateHit (przy DragonDamageScaling 10 nic nie daja, a to goraca sciezka).
- Osobny CHANGELOG i osobny test bitwy.

=== 6. ZALEZNOSCI DLA TORU KRONIKI (zapisac w PROJEKT-KRONIKA, nie kodowac teraz) ===
- Nr 19 (Myrcella): okno w dniu Prologue.MyrcellaDay() (70 x skala, ok. 303), nie w osobnym oknie 215-390. Tresc "pakt korona-Dorne" zamiast "relacja rosnie".
- Nr 27 (Roze): okno zaraz po linii "REACH Z KORONA" (zdarzenie, nie dni 540-700). Usunac "bez wymuszania pokoju" i wersje "Tyrellowie odmawiaja", zostawic tylko przypadek "warunki nie przeszly".
- Nr 95 (Sept Baelora): razem z ta paczka dopisac Prologue.ReachLeavesCrown(string why). Ustawia stan "zakonczone (Kronika nr 95)", przestaje pilnowac, zapisuje w arm_m104 (rc=3). Bez tego sojusz korona-Reach bylby odnawiany az do najazdu Daenerys. Wojna Reach z korona czy same relacje - decyzja Jeffa przy nr 95.
- Nr 5 (Stos Drogo): gdy powstanie, usunac komunikat etapu smoczeta z PlayerDragonNews (jedno zjawisko = jedna informacja).
- Nr 34 i nr 78 sa zgodne z etapami: mlode 779, dorosle 1656. Nr 78 (Daenerys znika z mapy): DragonsApply czeka, gdy ona nie prowadzi partii (sprawdzone w kodzie).

=== 7. SCALENIE (tools/scalanie/scal.sh, potem sprawdz_narzedzie.py) ===
- Konflikty pewne:
  - Prologue.cs ResetCampaign (obie galezie dopisuja linie po ResetScenes);
  - Settings.cs (blok Prologue 298);
  - McmSettings.cs - NIE scalac recznie, tylko python tools/gen_mcm.py po scaleniu;
  - sprawdz_logi.py (dispatch grup; pulapka: wstawka w cudzy if).
- Kolizji nazw miedzy Prologue.World.cs (E5) a Prologue.Wars/Crown/Dragons.cs nie ma (porownalem skladowe).
- Po scaleniu build 5 modow (kod wyjscia 0) i recenzja styku.

=== 8. CHANGELOG (trzy osobne wpisy, CLAUDE.md pkt 9) ===
- (1) Essos na kalendarzu;
- (2) Reach i Dorne z korona;
- (3) mlode smoki.
Kazdy z sekcja "Ryzyko / co sprawdzic" z listy risks. Status DO SPRAWDZENIA do testu koncowego. Potem STAN-PRAC.

=== 9. DO POWIEDZENIA JEFFOWI (prostym jezykiem) ===
1. Wojny Daenerys z Qarthem i Dothrakami ruszaja po prologu, nie w nim, bo w prologu ona jest jeszcze zona khala Drogo (tak jest w scenach). Reszta Essos (Braavos-Lorath, Tyrosh-Lys, Pentos-Myr, Norvos-Qohor, Volantis-Aegon, Sarnor-Ibben) walczy od dnia 22 kalendarza.
2. W obecnej kampanii klasycznej przy nastepnym wczytaniu Daenerys zsiada z Drogona: jedzie konno, a smoki wracaja ok. 780 dnia opowiesci. Dorne podpisze pakt z korona ok. 303 dnia, a Reach przejdzie do korony po smierci Renly'ego. Kazda z tych zmian ma wlasny wylacznik w MCM.
3. W bitwie mlody smok to dzis mniejszy smok ROT o pelnym zdrowiu. Oslabienie (30% / 65% zdrowia) dojdzie po tescie bitwy.

**Ryzyka:**

- Na ROTStorylineWar.Enforced siedza trzy latki naraz: transpiler Armoury (Prologue.Story), prefiks/finalizer CrashScribe Fabula i prefiks/finalizer Armoury (K1). Finalizer K1 musi zawsze zdjac _calendarDepth ([ThreadStatic]), inaczej kazdy odczyt startu w ROT przejdzie na kalendarz. Kod tak robi; T1 sprawdza linie 'wydarzenia ROT ruszone 0' i PR-4 m101prolog.
- IsWarForced dla 6 par essoskich jest prawdziwe w kampanii z prologiem od dnia KALENDARZA 22 do 433 (dzien opowiesci od -166 do 245). Zmienia to PovertyPeace, StorylineReparations, PeaceHold i blokade pokoju ROT dla tych par. Koniec wojen Essos wypada 188 dni wczesniej niz w klasycznym starcie (zamierzone: kalendarz). Wpisac do CHANGELOG.
- Zbior EssosIds jest staly (id ROT 8.1.8). Inna wersja ROT moze zmienic id: linia startu musi pokazywac 'na kalendarzu 6'.
- StartAlliance gry od razu sklada wnioski wezwan dla wszystkich wojen obu stron (sprawdzone w 1.4.8). Pokoj Reach-Dorne jest przed sojuszem, ale korona moze wezwac Reach do wojen spoza kanonu (np. z Essos). Pieniadze wezwan rozlicza latka 168 CrownCallToWar.
- Pakt Diplomacy blokuje tylko decyzje AI (DeclareWarConditions), nie DeclareWarAction. Wezwania, ROT EnforceWars, AIInfluence i gracz moga go ominac. K2 odnawia pokoj co dobe, a K4 tylko liczy i opisuje wypowiedzenia. Gdy bedzie ich duzo, moze sie zrobic petla wojna/pokoj; wtedy twarda blokada, osobny wpis.
- Bez wylacznika z Kroniki nr 95 sojusz korona-Reach jest odnawiany co dobe az do najazdu Daenerys (ok. 1949). W kanonie Tyrellowie zrywaja z korona po Sepcie Baelora (ok. 1650-1800). Dopisac ReachLeavesCrown razem z paczka Kroniki nr 95.
- Kronika nr 19 (dni 215-390, 'relacja rosnie') i nr 27 (dni 540-700, 'bez wymuszania pokoju', wersja 'odmawiaja') mowia co innego niz K2 (dzien 303; sojusz od razu po smierci Renly'ego). Bez poprawki dokumentu tor Kroniki zrobi drugie, sprzeczne wyzwalacze.
- Sojusz gry wygasa po 84 dniach. Przedluzenie idzie przez prywatna AllianceCampaignBehavior.UpdateAllianceEndTime (odbicie). Gdy zmieni sie jej sygnatura, linia startu pokaze 'koniec sojuszu BRAK', a sojusz bedzie odnawiany co 84 dni (linia ODNOWIONY).
- Rozpoznanie postaci po id (lord_1_9 Renly, lord_4_10 Myrcella, clan_empire_west_1 Tyrell, clan_aserai_1 Martell, lord_1_14 Daenerys). Zmiany prologu (CanonCompanions, przenosiny rodow) moga zmienic wladcow; wtedy warunki cicho nie przejda, a linia doby pokaze powod 'czeka (...)'.
- K3: mlody smok (dragon_green, Viserion) dostaje w bitwie pelne HP z ROTSettings.DragonHealth (u Jeffa 10000), bo ROT ustawia je kazdemu smokowi. 'Mlody' jest tylko mniejszy i wolniejszy, dopoki nie ma K3b (HP w MissionLogic, po tescie bitwy).
- K3: smoki pojawiaja sie i znikaja ze slotu bez platnika. To nie towar (is_merchandise=false, DragonPurge zostawia smoki tylko Daenerys i graczowi), ale trzeba to wpisac do CHANGELOG ('smok nie jest towarem'). Kon idzie z jej taboru i do niego wraca, wiec tu nic z niczego sie nie dzieje.
- K3: Stables.IsPlainMount pomija wielblady. Partia Daenerys w Essos bez koni oznacza Daenerys pieszo. Liczyc w T3 linie 'brak konia w taborze'; gdy czeste, dopuscic wielblady (uprzaz i tak idzie do taboru przez HarnessToTrain).
- K3: inny mod (DTE, BK) moze przepinac wierzchowce lordow, co da ping-pong slotu co dobe. Prog W-7 to wylapuje.
- K3 dziala w kazdej kampanii: w biezacej kampanii klasycznej Jeffa Drogon zniknie przy nastepnym wczytaniu (dzien opowiesci < 779). To zgodne z zyczeniem Jeffa, ale widoczne - powiedziec przed wgraniem.
- Komunikat etapu smoczeta (dzien opowiesci 0) dubluje przyszle okno Kroniki nr 5 (Stos Drogo, dni 22-60). Po powstaniu nr 5 usunac komunikat.
- Tryb proby (flaga armoury-m104-proba.flag) jest w DLL, ktora idzie do gry. Pozostawiona flaga skroci progi u Jeffa. Skrypt testu musi ja usuwac, a linia 'TRYB PROBY' musi byc w kazdej sesji.
- Trzy zjawiska w jednym DLL (CLAUDE.md pkt 8.2): rozdzielone kluczami MCM. Przy bledzie najpierw wylaczyc klucz danej czesci, nie cofac calej galezi.
- Nie testowac ani nie wgrywac, gdy Jeff gra (monitoring od 15.10). Przed pierwszym autotestem kopia wszystkich logow Jeffa z dnia (Armoury, CrashScribe i RC rotuja logi).

**Pytania:**

- Daenerys: jej wojny z Qarthem i Dothrakami zostaja na osi opowiesci (kod K1), choc lista z 10:16 je wymieniala. Wystarczy powiedziec Jeffowi czy pytac? Polecam powiedziec, a zmieniac tylko na jego slowo.
- Czy biezaca kampania Jeffa jest klasyczna (start 20.10), czy po WGRANIU 101 zaczal nowa kampanie z prologiem? Najnowsze logi Armoury to autotesty (10:08), wiec nie wiem. Od tego zalezy, czy K2 i K3 zmienia jego obecna gre.
- Kronika nr 95 (Sept Baelora): po smierci Mace'a wojna Reach z korona czy tylko spadek relacji? Kronika zostawia to Jeffowi. Do tej decyzji sojusz trwa do najazdu Daenerys.
- T4: czy bieg-bitwa.ps1 umie postawic bitwe z partia Daenerys? Jesli nie, K3 w bitwie (brak smoka u smoczat, dragon_green jako wierzchowiec) sprawdzi dopiero gra Jeffa, a K3b czeka.

## PLAN kronika

**Podsumowanie:**

Krytyka i plan toru Kronika (117 wydarzen, 204 okna). Repo, drzew i gry nie zmienialem. Pliki robocze (dekompilacje) leza w scratchpad 7aee5f12.../kron/ (gqm.cs, msvm.cs).

WERDYKT
- Architekture bierzemy od A: dane w TSV, wybor przez liste po oknie, zapis, linie logu, progi.
- Od B bierzemy tabele identyfikatorow, poprawki faktow i staranniej przebrane tematy obrazkow.
- Wprowadzam 10 poprawek. Najwazniejsze:
  (1) okno Kroniki zwykle IDZIE ZA zmiana strony postaci kanonu (`po: mv:mNN`), zamiast ja wykonywac. Kronika steruje zmiana tylko termem `kr:` w wierszu arm_canon_moves. Odpada RunMoveNow.
  (2) w K0 nie ma trzymania wydarzen ROT (klucz i latka Fabula.GatePrefix odpadaja). Zamiast tego okno liczy swoj termin wstecz od planowanego dnia wydarzenia ROT na osi Fabuli.
  (3) dane leza w plikach arm_kronika_kNN.tsv w Armoury/ModuleData. Obecny skrypt wgrania kopiuje tylko arm_*.tsv z tego katalogu (wgraj-v5.ps1 wiersz 41), wiec podkatalog Kronika nie trafilby do gry.
  (4) kod K0 ruszy dopiero na drzewie po scaleniu E5 i wojen. m104-e5 K1 (ff9d85e7) juz zmienia CanonCompanions.cs (HeldRow/Freeing) i lata EndCaptivityAction.
  (5) Kronika nie dotyka smokow ani sojuszy. Te maja juz wlasciciela w m104-wojny: Prologue.Dragons oraz Prologue.Crown, ktory pilnuje sojuszu Reach-korona co dobe do najazdu Daenerys.

SPRAWDZILEM W KODZIE I DEKOMPILACJI (6 twierdzen)
- Okno ROT: Esc zawsze wola ExecuteCancel, nawet przy jednym przycisku, a Enter wola ExecuteDone (ROT EventScreen.OnFrameTick).
- Lista MultiSelection przy max=1 dziala jak radio. Przy isExitShown=false Esc nic nie robi (MultiSelectionQueryPopUpVM i GauntletQueryManager 1.4.8). Autotest (cs-at AutotestUi.HandleMulti) wybiera od konca ostatnia czynna pozycje.
- Akcja 'remove' to dzis smierc (KillCharacterAction.ApplyByRemove).
- DisableHeroAction rozwiazuje partie dowodcy, jencowi zalicza ucieczke, a bohatera przenosi na liste martwych i wylaczonych (CampaignObjectManager).
- Lektor w n31 szuka nagran tylko po tytule. 58 nagran WAV zajmuje 81 MB, a glosy gry sa w .ogg.
- JoffreyEvent robi glowa rodu Cersei.

**Plan:**

A. SPRAWDZONE (dowody; na nich stoi plan)
1. ROT EventScreen.OnFrameTick (zb/pytania-tmp/dec/ROT.Screens.EventScreen.cs):
   - IsKeyReleased(Escape) wola _dataSource.ExecuteCancel() BEZ sprawdzania IsCancelEnabled;
   - IsKeyReleased(Enter) wola ExecuteDone();
   - EventPanel najpierw robi PopState, potem Handle*.
   WNIOSEK: zaden wybor nie moze lezec na przyciskach okna ROT. Kazde "2 przyciski" w tabeli B (wydarzenia 2, 3, 12, 15, 18, 24, 32, 33, 46, 54, 60, 73, 104, 105, 115 ...) zamieniamy na liste po oknie.
2. MultiSelectionQueryPopUpVM (TaleWorlds.MountAndBlade.ViewModelCollection 1.4.8):
   - OnInquiryElementSelected przy MaxSelectableOptionCount==1 odznacza reszte (radio);
   - IsButtonCancelShown = _data.IsExitShown;
   - GauntletQueryManager (Modules/Native .../TaleWorlds.MountAndBlade.GauntletUI.dll): "Exit" tylko przy IsButtonCancelShown, "Confirm" tylko przy IsButtonOkEnabled (przy min=1 wymaga zaznaczenia);
   - autotest zb/cs-at/CrashScribe/src/AutotestUi.cs HandleMulti: petla od konca po czynnych, linia "OKNO wybor z listy".
   POTWIERDZONE.
3. CanonCompanions.cs (m103-sceny):
   - 1283-1285 'remove' to RemoveQuiet, czyli KillCharacterAction.ApplyByRemove (w. 428), wiec powrotu nie ma;
   - DisableHeroAction (TaleWorlds.CampaignSystem 1.4.8): DestroyPartyAction dla dowodcy, EndCaptivityAction.ApplyByEscape dla jenca, stan Disabled;
   - CampaignObjectManager (w. 392/486/500) przenosi Disabled do listy martwych i wylaczonych, wiec wyszukiwania po zywych go nie widza.
4. Lektor.cs (n31-calosc):
   - indeks tylko _byTitle (w. 52, duplikat tytulu pomijany w. 133), wiec warianty okien o tym samym tytule czytalyby zly tekst;
   - n31 ma 58 nagran = 81 MB WAV, a glosy gry SandBox/VoicedLines to 1154 x .ogg (ta sama droga SoundEvent);
   - wgraj-v5.ps1 kopiuje z Lektor tylko *.wav.
5. JoffreyEvent: ChangeClanLeaderAction.ApplyWithSelectedNewLeader(BaratheonJoffrey, CerseiLannister). B ma racje, wydarzenie 51 to Tommen tylko w tekscie.
6. Kolizje z galeziami, ktore juz sa:
   - m104-wojny Prologue.Crown.cs pilnuje co dobe sojuszu korona-Reach i paktow Dorne az do najazdu Daenerys;
   - m104-wojny Prologue.Dragons.cs trzyma slot konia lord_1_14 (smoki wedlug dnia osi);
   - m104-e5 ff9d85e7 zmienia CanonCompanions (HeldRow/Freeing/TemplateHero), wiersze #p01/#p02/#p10/#m01 i lata EndCaptivityAction.ApplyInternal + RansomOffer.
7. Zegar jest jeden: CanonCompanions.StoryDay() = start + 188 (Prologue.PrologueDays), a CrashScribe StoryClock czyta Calendar.StoryStartTime.
   - Fabula.Timeline jest private static w CrashScribe; linia startu to "FABULA: wydarzenie X WYSTARTOWALO (dzien ...".
   - Grupa progow m104wojny juz istnieje, wiec Kronika dostaje m105kronika, prefiks KW- (wolny).
   - Prefiks logu "Kronika ..." koliduje z "Kronika unikatow" w klasyfikatorze logu, wiec bierzemy "Kronika Westeros (KW): ".

B. POPRAWKI DO A I B (decyzje planisty)
P1. Zmiany stron postaci kanonu - jeden wykonawca (CanonCompanions, dobowa droga, jego kolejnosc wierszy):
   (a) DOMYSLNIE okno idzie za ruchem: kolumna po = mv:mNN (CanonCompanions.MoveState(id) zwraca done/skip/brak). Wariant okna zalezy od wyniku: wykonane = pelny tekst, pominiete = wersja bez postaci.
   (b) Gdy okno ma poprzedzic ruch albo wybor gracza ma go zmienic, wiersz arm_canon_moves dostaje term kr:NNN.w[=opcja] obok starego wyzwalacza (stary zostaje zapasem przy wylaczonej Kronice). Term kr: jest prawdziwy, gdy okno rozstrzygniete albo Kronika wylaczona.
   Przyklad: m22 Yoren 'ev:HarrenhalSiegeNotificationEvent|rotday:120' -> '...|kr:008.1+1', bo okno 8 jest w dniach 35-65.
   RunMoveNow z planu A odpada. Walidator ma tabele par okno<->wiersz.
P2. Trzymanie wydarzen ROT odpada z K0 (bez klucza ChronicleHoldsRotEvents, bez zmian w CrashScribe):
   - term przed:ev:X liczy termin okna z PLANOWANEGO dnia X: odbicie CrashScribe.Fabula.Timeline + Scaled; brak = sama kolumna od;
   - ROT nigdy nie startuje przed planem (bramka daty), wiec okno "N dni przed" wypada przed X;
   - gdy X ruszy wczesniej, okno zamienia sie w wiesc albo przepada z powodem;
   - trzymanie wraca jako osobna galaz tylko wtedy, gdy KW-3 pokaze straty.
P3. Ukrycie i powrot postaci (hide/unhide) to nowa akcja CanonCompanions (ApplyAction), uzywana przez Kronike i przez wiersze #move:
   - wiersze m11 (Ogar), m20 (Osha), m21 (Sam) 'remove' -> 'hide'; w kampanii Jeffa jeszcze nie wykonane, wiec bezpieczne;
   - wydarzenia 82/89/88/98 robia unhide;
   - zakaz dla: glowy rodu, wladcy, gubernatora, jenca, postaci gracza oraz listy chronionej;
   - przed DisableHeroAction zawsze LeaveParty (StartDisband);
   - FindHeroId musi szukac tez na liscie wylaczonych.
P4. Smoki: Kronika nie rusza przedmiotow dragon_*. Wydarzenia 106/113/116 to tekst i flaga; jedynym wlascicielem wierzchowca Daenerys jest Prologue.Dragons (m104).
P5. Sojusze:
   - Kronika nie wola StartAlliance;
   - wydarzenia 19/27 to okna za stanem Prologue.Crown (termy cw:reach / cw:dorne przez internal accessor);
   - pelne wersje 95/97/107 wymagaja wylacznika straznika Prologue.Crown i wpisu w CanonAlliances, dlatego tylko za kluczem ChronicleFullVersions i dopiero w K9/K10.
P6. Bandy:
   - pomocniki z E5 (banda z puli, turniej, jeniec fabularny) - po scaleniu sprawdzic, czy sa wspolne; jesli prywatne w Prologue.*, przeniesc 1:1 do Armoury/src/StoryActions.cs (bez zmiany logiki);
   - wspolny sufit band fabularnych na region (FreeFolkRaids + BrokenMen + E5 + Kronika) i Kronika <= 12;
   - wydarzenie 17 bierze bande Berica z E5, gdy zyje.
P7. Wydarzenia, ktore wymagaja postaci nieobecnych w ROT (Hoster, Ellaria, Olenna, Slynt, Dontos, Rodrik Cassel, Rorge, Edric Storm, Gilly), maja sam tekst.
   - Nowe postacie (NP) tylko w wersji pelnej.
   - Poprawki faktow B wchodza do danych: Piesc castle_S6 = Tormund clan_sturgia_7, Arianne w rodzie Aegona, Asha i Yara osobno, RamsayEvent sam przenosi Karstarkow, BastardsEvent niszczy rod Snow (83 do clan_battania_1), Edmure bez niewoli, Yunkai = Drudzy Synowie.
P8. Obrazki:
   - mechanizm od A (staly wybor dla okna, bez powtorki z rzedu), tematy od B (18 tematow, 36 sprite'ow);
   - poza rotacja: event-bg, BlackwaterWarning, Davos, Stannis, Ship1/2, testevent, duplikaty;
   - stannis-slain tylko w nr 81, bez jon_death i ramsay_death;
   - do puli dochodza istniejace pliki Images/Prolog/prolog_NN (dzis 01-03) i opcjonalnie Images/Kronika/kronika_<temat>_<n>;
   - zero promptow, zero pracy Jeffa.
P9. Lektor:
   - lookup najpierw po kluczu (ArmStoryEvent.Key = kr_NNN_w, kolumna 1 arm_lektor.tsv), potem po tytule;
   - w K0 test OGG (ffmpeg C:/Users/GAME/tools/ffmpeg/ffmpeg-9.0.2-essentials_build/bin); gdy gra, nagrania Kroniki ida w .ogg, kontrola pliku: OggS + md5, wgraj-v5.ps1 kopiuje tez *.ogg;
   - nagrywanie paczkami po zamrozeniu tekstow (md5 tekstu w tsv).
P10. Galezie:
   - dane i narzedzia (bez C#) ruszaja od razu na osobnej galezi;
   - kod K0 dopiero na drzewie n32 = n31-calosc + m103-gospodarka cz.2 + m104-e5 + m104-wojny (scal.sh + sprawdz_narzedzie.py);
   - jedna galaz kodu naraz.

C. GALEZIE I KOLEJNOSC
- KD w-toku/kronika-dane (z n31, same nowe pliki; tor M; mozna od razu):
  - tools/kronika/gen_kronika.py, tools/kronika/kronika_check.py;
  - Armoury/ModuleData/arm_kronika_tematy.tsv, arm_kronika_k00.tsv (test), arm_kronika_k01.tsv;
  - teksty EN okien K1 (linie.py: gora do 4 linii, dol do 3).
- n32 scalenie (glowny watek, po zakonczeniu E5/wojen/gospodarki cz.2).
- K0 w-toku/kronika-00-silnik (z n32 + KD; tor L - pelny lancuch, 2 recenzje + recenzja styku).
- K1 ... K10 po kolei (tor M; kazda: dane + nowe akcje + teksty + obrazki z tematow + nagrania okien 1 + autotest + progi + CHANGELOG "Ryzyko / co sprawdzic").
- TERMINY:
  - K0 + K1 musza byc wgrane, zanim kampania Jeffa dojdzie do doby ok. 198 (dzien opowiesci 10);
  - pilnowac "dzien opowiesci" z linii doby CanonCompanions w logach Jeffa;
  - paczka wgrana po swoich dniach nie robi lawiny, ale jej okna przepadaja ("zapis sprzed paczki").

D. DANE (KD)
- Jeden plik na paczke: Armoury/ModuleData/arm_kronika_kNN.tsv (UTF-8, TAB, '#' = komentarz), sekcje jak w arm_canon_moves.tsv.
  #okno: id(NNN.w, trwaly; usuniety = wiersz '#usuniete') | war | od | do | po | przed | warunki | gracz | forma(okno|wiesc|log) | inni(okno|wiesc|nic) | wersja(lekka|pelna|obie) | temat | lektor(1/0) | tytul | gora | dol | zmienne | wybor(pytanie) | skutki(' ; ')
  #wybor: okno | opcja a-d | etykieta <= 60 zn. | podpowiedz | dostepna(termy) | skutki | bierna(1). Zawsze 2-4 opcje, ostatnia bierna (wersja z ksiazki).
  #banda (od K1): id | klan | osada | min | max | herszt | zachowanie | dni | list gonczy | po_rozbiciu
  #para: okno | wiersz mNN | kierunek (po|kr) - dla walidatora P1.
- Teksty TextObject {=ARM_KR_NNN_w[war]_T|U|L}English text - wlasne slowa, bez cytatow; wiersze z lektor=1 bez {ZMIENNYCH}.
- Zrodla generatora:
  - scratchpad 7aee5f12.../kronika_v2.json (okna, poprawki);
  - kronB/tabela.py i kronB/linie.txt (id bohaterow, rodow, osad, klasy, tematy);
  - kronB/heroes.json.
- kronika_check.py (python -I, przed kazdym buildem; wynik w CHANGELOG):
  - unikalne id;
  - znane termy i akcje;
  - id istnieja w XML ROT (ROT_heroes.xml, spclans.xml, ROT-Map settlements.xml);
  - pojemnosc tekstu;
  - 2-4 opcje, ostatnia bierna;
  - gold/ware/garrison/band z platnikiem albo zrodlem;
  - own/ruler/leader/alliance/succeed tylko w wersji pelnej (poza lista dopuszczonych);
  - postac z wierszem #move nie dostaje clan/die/prisoner z Kroniki, tylko para P1;
  - hide/die zakazane na liscie chronionej;
  - md5 tekstu okien lektor=1 zgodne z nagraniem.
- Lista chronionych: postac -> wydarzenie ROT, po ktorym wolno (Ramsay->done:BastardsEvent, Mance->done:WallSiegeEvent, Jon->done:JonEvent, Daenerys->ev:DanyInvasionEvent, Stannis->done:StannisDeathEvent|WallEvent, Roose->done:RooseDeathEvent, Robb->done:RedWeddingEvent, Bran i Sansa (dziedzice przy Krwawych Godach) -> done:RedWeddingEvent, Joffrey->done:JoffreyEvent, Aegon->ev:AegonInvasionEvent). Uzupelnic z dekompilacji rot-events-all.cs w KD.

E. K0 SILNIK - PLIKI I METODY
Nowe (Armoury/src):
- Chronicle.cs - ChronicleBehavior:
  - DailyTick: lista naleznych, POMINIETE z powodem, linia doby;
  - SyncData;
  - Chronicle.AppTick() z SubModuleMain.OnApplicationTick obok Prologue.ScenesAppTick;
  - bramki Prologue.SceneGate (private -> internal, bez zmiany logiki) + StoryWindow.IsOpen + 2 s spokoju + 2 okna na dobe + 6 h od poprzedniego okna Kroniki albo od zamkniecia okna ROT (StoryPictures.EventWindowOnStack true->false);
  - okna osi nie ida w prologu (Prologue.BeforeStory()), poza powracajacymi z flaga 'prolog'.
- Chronicle.Data.cs:
  - wczytanie arm_kronika_*.tsv przy OnSessionLaunched z kontrola daty pliku;
  - zly plik (tez wyzerowany, bajt \0) wylacza tylko swoja paczke;
  - zly wiersz -> linia z powodem.
- Chronicle.Terms.cs:
  - wola CanonCompanions.TermBase/BaseTrue (private -> internal): ev, done, dead, war, own, rotday, prolday;
  - nowe termy: alive, clanof, leads, head, prisoner, peace, kexists, p:region, p:in, p:near, p:vassal, p:army, p:rel, kr, flag, mv (CanonCompanions.MoveState), cw:reach|dorne (Prologue.Crown accessor), season (WesterosClimate), plan:X-N (P2: Fabula.Timeline odbiciem, zapas brak);
  - pamiec pierwszego spelnienia jak CanonCompanions._first.
- Chronicle.Choice.cs:
  - OnClosed okna z wyborem ustawia stan 'a' (w zapisie);
  - AppTick po 0,5 s przy bramkach i !InformationManager.IsAnyInquiryActive() wola:
    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(tytul, opis+" (NNN.w)", elementy, false, 1, 1, "Confirm", "", Affirm, null), true);
  - element: new InquiryElement(litera, etykieta, null, dostepna, podpowiedz);
  - 0 dostepnych albo gracz poza kolumna 'gracz' albo ChroniclePlayerChoices=NIE -> opcja bierna bez pytania;
  - wczytanie ze stanem 'a' pokazuje liste ponownie;
  - flaga CrashScribe/armoury-kronika-wybor.flag (a|b|c|d) wymusza opcje w autotescie.
- Chronicle.Actions.cs:
  - wynik Done/Skip/Wait (Wait co dobe do 30 dni, potem POMINIETA; akcje czekajace w zapisie);
  - klasy: T (wiesc, plotka -> Rumours.AddStoryNews z noLive dla Lektor.Live, flag, kod:<nazwa>), R (rel, relp, relk, renown, influence), S (gold z platnikiem, security, loyalty, hide/unhide przez CanonCompanions.ApplyAction);
  - S wylaczone kluczem ChronicleWorldChanges=NIE z linia.
- Chronicle.Pictures.cs:
  - pula tematu = pliki kronika_<temat>_<n> + istniejace prolog_NN z arm_kronika_tematy.tsv + sprite'y ROT;
  - indeks = staly skrot klucza okna mod n, przy powtorce poprzedniego - nastepny;
  - plik przez StoryPictures.Acquire/Drop jak Prologue.Scenes.
Zmiany w istniejacych:
- CanonCompanions.cs:
  - TermBase/BaseTrue internal;
  - MoveState(id);
  - wydzielone internal Res ApplyAction(Hero, action, arg, zrodlo) (Execute wola to samo);
  - akcje hide/unhide z rejestrem 'hid' w zapisie;
  - term kr: (odbicie bez typu - Chronicle.Resolved(id), brak Kroniki = true);
  - FindHeroId takze po liscie wylaczonych.
- arm_canon_moves.tsv: m11/m20/m21 remove -> hide (od K6/K8 zgodnie z paczka; w K0 tylko mechanika); m22 += |kr:008.1+1 (w K1).
- StoryWindow.cs: ArmStoryEvent.Key.
- Lektor.cs: _byKey przed _byTitle; .ogg (OggS) obok .wav.
- Rumours.cs: AddStoryNews(textId, osada|krolestwo, dni, noLive).
- SubModuleMain: AppTick, OnGameEnd -> Chronicle.Reset, OnGameStart -> reset stanu.
- Settings.cs + python tools/gen_mcm.py.
- tools/sprawdz_logi.py: grupa m105kronika.
- Repo glowne tools/autotest-2026-10-17/wgraj-v5.ps1 i bieg-bitwa.ps1: *.ogg z ModuleData/Lektor (osobny commit jak 697c4885); pliki arm_kronika_*.tsv bierze juz petla arm_*.tsv.

F. KLUCZE MCM (Settings.cs, grupa "// --- Chronicle of Westeros ---", opisy EN, czytane na zywo)
- ChronicleOfWesteros = true - "117 events from the books and the show in story windows when their day comes and the world allows; Off = no Chronicle windows, nothing undone, saves load".
- ChronicleWorldChanges = true - "deaths, side changes, hidden characters, bands, tasks, gold; Off = windows, relations and renown only".
- ChronicleFullVersions = false - "16 big events in full: castle owners, rulers, alliances".
- ChroniclePlayerChoices = true - "Off = the book outcome without asking".
Stale w kodzie (bez pytania Jeffa): MaxLate 30, PerDay 2, GapHours 6, StableSec 2.0, ChoiceDelay 0.5 s, WaitActionDays 30, MaxBands 12, MaxRecurringPerDay 1, RecurringKeys 400.

G. LINIE LOGU (Armoury.log, prefiks "Kronika Westeros (KW): ")
- Start sesji: klucze TAK/NIE | paczki kNN: okien/wyborow/band | zle wiersze n (pierwszy z powodem) | tematy n, sprite'ow n, plikow n | os x<skala>, plan Fabuli TAK/NIE | lektor okien z nagraniem a/b | zapis: pokazane, wiesci, pominiete, czeka na wybor, akcje czekaja, ukryci | dzien opowiesci | nastepne okno NNN.w (od) | potkniecia.
- Doba: dzien | nalezne n [NNN.w: powod czekania] | dzis okna/wiesci/pominiete | akcje ok/pom/czeka | bandy n/12 | ukryci n | zapis n KB | potkniecia.
- "okno NNN.w 'tytul' pokazane | wariant x | dzien d (od a, do b, plan X-N) | obrazek temat->nazwa (ROT|plik) | lektor kr_NNN_w TAK/NIE | okno ROT|zapas | gracz: osada/menu | ms"
- "okno NNN.w zamkniete po s (przycisk|przycisk (Esc)|bez przycisku)"
- "wybor NNN.w = x 'etykieta' (z n; dostepne abc; wymuszone flaga TAK/NIE; bierna TAK/NIE)"
- "akcja NNN.w #i <akcja> -> OK (przed -> po) | POMINIETA (powod) | CZEKA (powod, od dnia d)"
- "zloto NNN.w: platnik -> odbiorca kwota (zaplacone k)"
- "POMINIETE okno NNN.w - warunki swiata: term | spoznione o n dni | X juz ruszylo | zapis sprzed paczki kNN"
- "wiesc NNN.w (gracz poza ...)"
- "ukryty <postac> (NNN.w) do <term>" / "powrot <postac> -> <osada>"
- "banda NNN 'nazwa' (armoury_kr_..) | ludzi n (pula a, domy b) | region r: band fabularnych m/sufit"
- "ZLY PLIK arm_kronika_kNN.tsv (powod) - paczka wylaczona" / "ZLY WIERSZ ..."
- "zapis arm_kronika n KB"
- CanonCompanions: "Postacie kanonu (m101): zmiana strony #mNN ... wyzwalacz kr:NNN.w".
Bledy: Log.Error tylko przy potknieciach 1-5 (licznik), bez globalnego wylacznika.

H. PROGI m105kronika (tools/sprawdz_logi.py, prefiks KW-)
- KW-0 linia startu, klucz TAK, kazda paczka ma okna, zle wiersze 0 (log bez Kroniki = INFO).
- KW-1 ERROR i potkniecia Kroniki 0.
- KW-2 kazde nalezne okno rozstrzygniete (okno/wiesc/POMINIETE z powodem) w <= 3 doby, poza CZEKA z powodem.
- KW-3 zadne okno przed 'od'. Okno z przed:ev:X pokazane przed linia "FABULA: wydarzenie X WYSTARTOWALO" albo POMINIETE z powodem "X juz ruszylo" (licznik INFO; > 10% = sygnal do galezi trzymania).
- KW-4 <= 2 okna na dobe i odstep >= 6 h.
- KW-5 kazda akcja ma linie wyniku; akcji nieznanych 0.
- KW-6 kazda linia zlota z platnikiem (0 "z niczego").
- KW-7 po wczytaniu zadne okno ani wybor dwa razy.
- KW-8 okna "zapas" 0.
- KW-9 kazde okno z wyborem ma linie wyboru i pare z AUTOTEST "OKNO wybor z listy"; Esc przy liscie 0.
- KW-10 "ZWOLNIENIE PRZY OKNIE" 0.
- KW-11 lektor: okna lektor=1 z nagraniem / razem (INFO).
- KW-12 zapis arm_kronika < 24 KB.
- KW-13 kazdy wiersz #mNN wykonany najwyzej raz; pary P1 w kolejnosci (okno po ruchu albo ruch po oknie).
- KW-14 bandy Kroniki <= 12, ludzie z puli, sufit regionu.
- KW-15 lista chronionych: 0 ukryc i smierci przed ich wydarzeniem ROT; kazdy ukryty ma powrot albo powod.

I. TESTY (zasada Jeffa: wgranie tylko po udanym autotescie z kopia; nic, gdy Jeff gra; logi Jeffa kopiowac przed 1. autotestem)
- K0a: kampania klasyczna 40 dob + armoury-kronika-test.flag (plik arm_kronika_k00.tsv wczytywany tylko z flaga):
  - okno zwykle;
  - wariant zalezny od miejsca gracza;
  - wybor 3 opcji z jedna wyszarzona;
  - wiesc;
  - okno plan:NedEvent-3 / przed:ev:X;
  - hide/unhide na postaci testowej;
  - zapis przy stanie 'a', wczytanie, lista wraca.
- K0b: to samo z armoury-kronika-wybor.flag=a i =b.
- K0c: jedno nagranie prologu w .ogg - lektor gra (linia Lektor), md5 OK.
- Paczki: klasyczna do konca dni paczki + 20 (K1: 145 dob). Raz na paczke bieg z prologiem przez przejscie 188 -> dni paczki (K1: ok. 330 dob, ok. 1 h przy 11 s na dobe). Zapis w polowie i wczytanie (KW-7). Biegi z flaga wyboru a/b na paczki z wyborami.
- Bitwa (bieg-bitwa.ps1 -Battles) tylko gdy paczka dotyka misji (nie przewiduje).
- Test koncowy (Jeff: "na koncu"): prolog 230 + wczytanie + 160 z zapisu; K0+K1 dolaczone, jesli gotowe.

J. PACZKI (nowe klocki dochodza w paczce, ktora pierwsza ich potrzebuje)
- K1 (dni 10-125): 1-12 + scena "Tyrion Reka".
  - Nowe: band (8 Lorch; pomocnik E5), tourney (3 opcja; pomocnik E5 i GrandTourney), security/loyalty (9), gold (12), listy wyborow 2/3/12, plan:RiverlandsDeclareWarEvent (2), hide Arya (8), m22 += kr:008.1.
  - Warunek 5/10: done:NedEvent (Essos zyje od kalendarza - m104).
- K2 (100-500): 13-22 + Theon na Pyke.
  - Nowe: Chronicle.Tasks.cs (14, 20; wlasny rejestr bez QuestBase, NoticeBoard Kind.Task), Chronicle.Recurring.cs (013 season, 015 siegehunger), relk, muster (14, 22; jednorazowo, miekko).
  - 16 za mv:p10/m01 (Jaime w Riverrun, E5), 17 banda Berica z E5, 19 za cw:dorne.
- K3 (433-815): 23-32. 23/26 lekkie; 024 powracajace; wound; 31 za m26; 32 za mv:m02 albo kr: dla opcji alarmu.
- K4 (770-967): 33-44 + nowy lad.
  - Nowe: skill (Jaime, 37), 039 oldage, succeed (smierc wladcy z dziedzicem - wzor Prologue.End; 42 tylko przy zgodzie listy chronionych).
- K5 (967-1300): 45-54 + miecz Brienne. 045 brokenmen; 49 hide Sansa (po JoffreyEvent; Sansa juz nie dziedziczka?), 52 za m03.
- K6 (1215-1460): 55-66 + Sam do Starego Miasta. 061 others; ware; 58 przez dlugi m96/IronBank; 63 lekka; m21 -> hide.
- K7 (1300-1510): 67-74, 81-83. 81 lekka (StannisDeathEvent ROT tylko przy zywym Renlym); 82 unhide Sam.
- K8 (1420-1650): 75-77, 84-93 + Tyrion w niewoli. kod:zaraza (76); 86 lekka; m11 -> hide, 89 unhide Ogar.
- K9 (1650-2100): 78-80, 94-103. 94/95/100/103 lekkie; pelna 95 dopiero z wylacznikiem straznika Prologue.Crown.
- K10 (2000-koniec): 104-117. 107/108/115/116/117 lekkie; kod:dzwony, kod:wielka_rada (lekka = tekst i relacje).
- Wydarzenie wielookienne caloscia w paczce pierwszego okna. Pelne wersje tylko za ChronicleFullVersions.

K. LEKTOR I OBRAZKI
- Budzet ElevenLabs:
  - przed kazda paczka odczyt limitu planu (subskrypcja konta Jeffa) - Jeff "zwieksze do 100k";
  - plotki na zywo zjadaja do 1000 zn./dobe (~30 tys./mies.);
  - kolejnosc nagran: okno 1 kazdego wydarzenia paczki (~6-12 tys. zn./paczke; calosc okien 1 ok. 61 tys. zn.), reszta okien po zamknieciu wszystkich paczek, jesli starczy;
  - przelicznik zmierzony 0,72-0,73 kredytu na znak;
  - glos David v2 (BNgbHR0DNeZixGQVzloa, eleven_multilingual_v2, 0.5/0.78/0.15/0.95), tools/lektor/scenariusz_kronika.py + nagraj_david.py;
  - pliki ModuleData/Lektor/EN/kr_NNN_w.ogg (albo .wav, gdy K0c nie przejdzie) + wiersze arm_lektor.tsv z kluczem i md5;
  - warianty z {ZMIENNYMI} bez nagrania (cisza).
- Obrazki: pula wedlug P8; Jeff nic nie robi; nowy plik prologu sam wchodzi do puli.

L. WGRANIE
- Skrypt wgrania (wgraj-v5.ps1) z kopia; pliki arm_kronika_*.tsv ida petla arm_*.tsv; .ogg po zmianie skryptu.
- Kontrola hash-compare po wgraniu (dysk C zeruje pliki).
- Wpis STAN-PRAC po kazdym etapie.

**Ryzyka:**

- Wybor na przyciskach okna ROT dalby przypadkowe decyzje, bo Esc zawsze wola Cancel, a Enter Done (sprawdzone). Wszystkie '2 przyciski' z tabeli B ida na liste po oknie. Przy wczytaniu w stanie 'czeka na wybor' lista musi wrocic (KW-7, KW-9).
- Podwojne wykonanie zmian stron (Kronika i #move). Jeden wykonawca to CanonCompanions, a pary okno<->wiersz sa w sekcji #para i w walidatorze. Prog KW-13.
- m104-e5 (ff9d85e7) i K0 zmieniaja te same pliki (CanonCompanions.cs, arm_canon_moves.tsv, SubModuleMain, Settings.cs, sprawdz_logi.py). Kod K0 tylko na drzewie po scaleniu n32. scal.sh ma pulapki: fuzz wstawia dispatch w cudzy if, a rownolegle galezie daja te same nazwy grup progow.
- Ukrycie: DisableHeroAction niszczy partie dowodcy, jencowi zalicza ucieczke (lata E5 na EndCaptivityAction moga zareagowac), a bohatera przenosi na liste martwych i wylaczonych. Do sprawdzenia przed K1: FindHeroId, dziedziczenie rodu (vanilla i ROT), BK i ROTLords wobec bohatera Disabled. Zakaz dla glow rodu, wladcow, gubernatorow, jencow i listy chronionej.
- Postacie potrzebne pozniejszym wydarzeniom ROT (Ramsay, Mance, Jon, Daenerys, Stannis, Roose, Robb, Bran i Sansa przy Krwawych Godach, Aegon) nie moga zginac ani zniknac przed nimi. Lista chronionych jest w walidatorze i w kodzie, prog KW-15.
- Kolizja ze straznikiem Prologue.Crown (m104-wojny): co dobe odnawia sojusz korona-Reach i pakty Dorne do najazdu Daenerys. Pelne 95/97/107 bez wylacznika straznika zostalyby cofniete nastepnej doby, a sojusz spoza listy CanonAlliances zerwany przy wczytaniu.
- Smoki: tylko Prologue.Dragons (m104) ustawia wierzchowca Daenerys. Kronika ruszajaca dragon_* walczylaby z jego doba (DragonFeed, HeroPrisonerReleased). Dlatego w Kronice smoki to tylko tekst i flaga.
- Bez trzymania wydarzen ROT okno 'przed X' moze przepasc, gdy X ruszy wczesniej niz plan albo gracz siedzi w niewoli. KW-3 to liczy. Galaz trzymania dopiero, gdy straty przekrocza 10%.
- Lawina okien po dlugiej niewoli albo starym zapisie: tempo 2 na dobe z odstepem 6 h, pomijanie spoznionych ponad 30 dni (albo wiesc) i pomijanie przy zapisie sprzed paczki ('pk' w zapisie).
- Kampania Jeffa dojdzie do dnia opowiesci 10 ok. doby 198. Paczka wgrana po swoich dniach traci okna (bez lawiny). Nie wolno wgrywac, gdy Jeff gra, a Jeff chce test i wgranie 'na koncu'.
- Lektor szuka dzis po tytule, wiec warianty czytalyby zly tekst. WAV to ok. 1,4 MB na okno (ponad 200 okien = ok. 300 MB). OGG niesprawdzone w Lektor (kontrola naglowka WAV) i w skrypcie wgrania (kopiuje tylko *.wav). Budzet 100k kredytow dzieli sie z plotkami na zywo (do 1000 zn./dobe), a zmiana tekstu po nagraniu oznacza ponowne nagranie.
- Bandy z niebandyckim klanem (Bractwo, Stone Crows, Synowie Harpii) przez BanditPartyComponent.CreateLooterParty - sprawdzic w dekompilacji przed K1/K2. Do tego wspolny sufit band fabularnych na region (FreeFolkRaids, BrokenMen, E5, Kronika), bo setki partii to CTD.
- Zamknieta ekonomia: gold/ware/garrison/band/pop zawsze z platnikiem albo zrodlem. Bezposrednia zmiana Town.Prosperity moze sie gryzc z DailyLife i ksiega ludzi - tylko przez PeopleBook/Devastation. Prog KW-6.
- Dysk C zeruje pliki: wyzerowany arm_kronika_kNN.tsv wylacza tylko swoja paczke (ZLY PLIK, KW-0). Nagrania sprawdzane md5.
- Id okien sa trwale w zapisie. Przenumerowanie albo usuniecie wiersza w pozniejszej galezi psuje stan, dlatego nagrobki '#usuniete'.
- ShowMultiSelectionInquiry z identyczna trescia daje FailedAssert duplikatu, dlatego opis listy zawiera id okna. Lista pokazywana tylko przy !IsAnyInquiryActive i bez okna ROT na stosie.

**Pytania:**

- Wersje pelne 16 duzych wydarzen (23, 26, 56, 59, 63, 81, 86, 94, 95, 100, 103, 107, 108, 115, 116, 117): domyslnie wszystkie lekkie (ChronicleFullVersions = NIE). Pelne 95/97/107 wymagaja zmiany straznika sojuszy z m104. Czy Jeff chce ktores pelne od razu? Proponuje nie.
- Czy ElevenLabs jest juz na planie Creator (100k)? To do sprawdzenia odczytem limitu przed nagraniem K1, nie pytanie do Jeffa. Domyslnie nagrywamy okno 1 kazdego wydarzenia paczki, reszte okien na koniec.
- Punkty 'do decyzji' z Kroniki (np. 46/101 Jorah, 47 Kamienne Serce bez nowej postaci, 86 Obara zamiast Ellarii) rozstrzygam domyslnie wedlug ksiazki i wersji lekkiej, z linia logu. Jeffa pytamy tylko, jesli sam zechce zmienic.

## e5:A (badacz)

**Podsumowanie:**

E5 czesc A da sie zrobic bez armii, bez nowych partii i bez wojska z niczego. Robert jedzie swoja zwykla partia lorda, Jaime i Tyrion (a w drodze powrotnej Ned) eskortuja go swoimi partiami, a Ogar, Cersei i inni dorosli bez wlasnej partii jada w druzynie krola. Dzieci (Myrcella 16, Tommen 12, Sansa 17, Arya 15 wedlug XML ROT; prog dorosly/dziecko liczyc w grze z AgeModel.HeroComesOfAge, domyslnie 18) przenosza sie razem z przyjazdem orszaku do celu. Ruch to ta sama technika co najazdy Wolnych Ludzi w Armoury (FreeFolkRaids.cs): rozkaz SetMoveGoToSettlement albo SetMoveEscortParty, flaga DoNotMakeNewDecisions (myslenie gry i ROT ja pomija, a gra trzyma ja w zapisie, wiec zdejmujemy ja przed zapisem) oraz IgnoreForHours(1.5) odnawiane co godzine.

Nietykalnosc dziala tylko wobec AI. Gra sama wyklucza ukryte partie z celow i ze spotkan (GetBestInitiativeBehavior, PartyBase.CanPartyInteract), ale gracz moze zaatakowac orszak jak kazda partie. Wtedy trasa sie przerywa z linia w logu, a Robert i tak nie zginie (CanHeroDie).

Sprawdzone w dekompilacji, czego nie psuje:
- myslenie gry i ROT pomija partie z ta flaga (AiPartyThinkBehavior.PartyHourlyAiTick i prefiks ROT AIThinkPatch);
- wezwania do armii tez ja pomijaja (DefaultArmyManagementCalculationModel:130);
- StrategicCampaignAI145 rusza tylko armie;
- przeprawy morskie BK nie dotycza tej trasy (Winterfell nie ma portu, do KL jest droga ladowa).

Kolizje, ktore trzeba zalatac:
- **uczta BK** (BKFeastBehavior.HourlyTickPartyImpl): co godzine wylacza AI goscia i wysyla go na uczte;
- **wezwania na turnieje w GT** (SummonLords): nie patrza na te flage;
- **koniec prologu**: blokady trzeba zdjac przed smiercia krola i aresztem Neda.

Postoje tylko w miastach i wsiach, bo gra kupuje jedzenie tylko tam (PartiesBuyFoodCampaignBehavior.TryBuyingFood).

Turniej Reki: GT dostaje publiczne wejscie ProclaimRoyal, ktore Armoury wola odbiciem w dniu 121. Zjazd trwa 4 dni, wiec listy otwieraja sie w dniu 125, bez reguly osmiu i bez spadku do turnieju lokalnego. Gra nie konczy turnieju przed uplywem 10 dni od utworzenia (dzien 131), najpozniej konczy go w dniu 136, wiec gracz ma kilka dni na start. Gregor (lord_4_21) i Loras (lord_1_44) jada do KL od dnia 112 i tam czekaja. Jaime czeka w KL od powrotu orszaku. Ogar bez partii jest przenoszony do KL w dniu ogloszenia, a potem gra sama go nie ruszy, bo nie przenosi lordow z miasta z turniejem (CanHeroMoveToAnotherSettlement). Bez zmiany w GT uczestnicy kanonu nie wejda do drabinki, bo w KL bywa 18-29 lordow (log GT: "lordow w miescie 28, w drabince bylo 15, dolozono 0"). Dlatego NoblesFightPatch ma wstawiac uczestnikow kanonu przed innych lordow.

Wszystko jest zamknieta ekonomia: zadnego nowego zlota ani wojska, a nagroda zostaje zwykla nagroda turnieju.

**Plan:**

TOR L (dwa mody plus latka na BK). Dwie paczki po kolei, kazda z autotestem: A1 orszak (tylko Armoury), A2 turniej Reki (GrandTourney + Armoury). Galaz: w-toku/m104-orszak od m103-sceny.

=== DANE (sprawdzone w XML ROT) ===
Bohaterowie:
- Robert ROTlord_506 (rod clan_nord_1), Cersei lord_4_2 (clan_nord_1), Joffrey lord_7_1 (18 lat).
- Myrcella lord_4_10 (16), Tommen lord_4_7 (12).
- Gregor lord_4_21 (clan_vlandia_5), Loras lord_1_44 (clan_empire_west_1).
- Ned ROTlord_512, Sansa lord_5_13_1 (17), Arya lord_5_1_1 (15).
- Jaime = wzor ROTwanderer1, Tyrion = ROTwanderer8, Ogar = ROTwanderer3 (dworzanin bez partii, clan_nord_1).

Osady (ROT-Map/ModuleData/settlements.xml):
- KL town_EW4, Darry ROT_castle16 (zamek), Crossroads Inn village_EN3_1 (wies).
- The Twins ROT_town3 (miasto), Moat Cailin castle_B1 (zamek), Fever castle_village_K8_1 (wies miedzy Moat Cailin a Winterfell).
- Winterfell town_B2, Castle Black town_B5 (miasto).

=== A1: ORSZAK - nowy plik Armoury/src/Prologue.Road.cs (partial class Prologue) ===
1. Silnik tras. Klasa Route:
   - pola: klucz, id bohatera albo wzor, tryb Go/Escort, lista nog (osada, najwczesniejszy dzien odjazdu), noga, dzien przyjazdu, stan;
   - HashSet<MobileParty> _held do szybkiego IsHeld(mp) (czyta go latka BK co godzine);
   - partia zawsze z bohatera (hero.PartyBelongedTo, gdy hero.IsPartyLeader); nowa partia tego samego bohatera dostaje rozkaz od nowa.
2. Rozkazy:
   - SetMoveGoToSettlement(osada, NavigationType.Default, false) albo SetMoveEscortParty(partia krola, Default, false), oba przez wejscie metody (wzor F7: TourneyBehavior.SetMoveGo, delegat FastCall), zeby biegly bramka snu SleepGate i straznik BK;
   - potem mp.Ai.SetDoNotMakeNewDecisions(true);
   - partia w armii: wodz -> DisbandArmyAction.ApplyByUnknownReason, czlonek -> party.Army = null (wzor ArrestNed);
   - cudza dluga blokada AI (IsDisabled z terminem > 1.5 h, np. uczta BK) -> EnableAi() i linia logu.
3. Straznik godzinowy RoadHourly() na CampaignEvents.HourlyTickEvent w PrologueBehavior, tylko przy Active i kluczu:
   - pominac partie w bitwie albo oblezeniu (MapEvent/SiegeEvent) i spiaca (NightRest.SleepKind(mp) != SkNone, bo swit odda zapamietany rozkaz);
   - rozkaz rozny od naszego (DefaultBehavior, TargetSettlement, TargetParty) -> wydac ponownie i policzyc "cofniete cudze rozkazy" (pierwsze 3 w calosci, potem liczniki);
   - mp.IgnoreForHours(1.5f), jak FreeFolkRaids ClimbHide;
   - przyjazd = CurrentSettlement == cel albo <= 2 jedn. od bramy -> zapisac dzien;
   - gdy dzien prologu >= dzien odjazdu nogi -> nastepna noga;
   - 24 h bez ruchu poza osada, obozem i bitwa -> linia "utknal" i rozkaz od nowa; po 3 dobach trasa zwolniona.
4. Zapis gry:
   - OnBeforeSaveEvent -> SetDoNotMakeNewDecisions(false) dla _held, OnSaveOverEvent -> true (wzor FreeFolkRaids.BeforeSave/SaveOver, rejestracja jak ArmouryBehavior.cs:1626);
   - po wczytaniu flaga wraca w pierwszej godzinie;
   - stan w arm_prologue: PackRoad/UnpackRoad, klucze ro= (nogi tras), rm= (przeniesieni bohaterowie), ht=/hd= (turniej);
   - ResetRoad() w ResetCampaign.
5. Zwolnienie Release(mp, powod): flaga false, RethinkAtNextHourlyTick = true, usuniecie z _held, linia logu.
   - ReleaseAll(powod): wylaczony klucz w trakcie, dzien >= 188 (zapas w Daily);
   - w TryKingFalls (Prologue.End.cs) zaraz po sprawdzeniu Busy(robert) - przed ChangeClanLeaderAction i ArrestNed;
   - trasa przerwana: partia krola zniszczona albo krol w niewoli -> zwolnic wszystkich.
6. Harmonogram w RoadDaily() (wolany z Daily()). Stale do kalibracji z autotestu; Claude decyduje, nie pytac Jeffa.
   - Dzien 0, krol: Darry (przejazd) -> Crossroads Inn (postoj do d6) -> The Twins (do d18) -> Moat Cailin (przejazd) -> Fever (do d29) -> Winterfell (do d55).
   - Dzien 0, eskorta: Jaime, Tyrion, Joffrey, Cersei (gdy prowadza wlasne partie).
   - Dzien 0, w druzynie krola: dorosli bez partii (Ogar, Cersei, Jaime albo Tyrion bez partii). Najpierw LeaveSettlementAction.ApplyForCharacterOnly, gdy bohater stoi w osadzie, potem TeleportHeroAction.ApplyImmediateTeleportToParty.
   - Pominieci z linia logu: gubernator (AddHeroToPartyAction zdejmuje gubernatora), martwy, jeniec, rod gracza.
   - Kto dojdzie pozniej (CanonCompanions przenosi postacie do rodow w pierwszej dobie) - dolaczany co godzine do przyjazdu do Winterfell.
   - Dzieci (Age < HeroComesOfAge): TeleportHeroAction.ApplyImmediateTeleportToSettlement do Winterfell przy przyjezdzie orszaku.
   - Dzien 30: Ned z partia jedzie do Winterfell i czeka.
   - Dzien 55, krol + eskorta Ned i Jaime: Fever (do d61) -> Moat Cailin -> The Twins (do d70) -> Crossroads Inn (do d88) -> Darry -> KL (cel ok. d90-95).
   - Dzien 55: Sansa i Arya, jesli niepelnoletnie, przenoszone do KL przy przyjezdzie orszaku.
   - Dzien 55, Tyrion (wlasna partia): Castle Black (do d85) -> Crossroads Inn (do d108) -> zwolnienie, zanim wiersz p01 (dzien 110) wezmie go do niewoli. Bez partii: ApplyDelayedTeleportToSettlement do Castle Black, w d85 do Darry.
   - W KL: dorosli z druzyny krola przeniesieni do KL (ApplyImmediateTeleportToSettlement). Robert i Ned czekaja w KL do konca prologu (zwolnienie w TryKingFalls), Jaime do d138.
   - Dzien 138: Jaime jedzie do clan_vlandia_1.HomeSettlement, zwolniony przy przyjezdzie albo w d160.
7. Latka BK w InstallRest (ApplyRoad): prefiks na BannerKings.Behaviours.Feasts.BKFeastBehavior.HourlyTickPartyImpl(MobileParty) przez AccessTools.TypeByName i DeclaredMethod; zwraca !(Active && IsHeld(party)). Bez BK: "BRAK". Linia: "latki prologu: ... | uczta BK dla orszaku TAK/BRAK".
8. CanonCompanions.cs: internal static Hero CanonHero(string tpl), opakowanie prywatnej FindOriginal, tylko zywi.
9. Klucze MCM w Settings.cs, potem python tools/gen_mcm.py; czytane na zywo:
   - PrologueRoyalColumn=true, opis EN: "In a Prologue 298 campaign King Robert rides the Kingsroad on the map: with the queen, Ser Jaime, Tyrion and the Hound to Winterfell (days 0-35) and back with Lord Eddard and his daughters (days 55-95); no outlaw dares touch the royal column. Off = the King stays where the game puts him";
   - PrologueHandTourney=true (A2).
10. Linie logu, tag "Prolog (E5 orszak): " (bez polskich znakow):
   - "start | krol <partia> w <osada> | eskorta: Jaime TAK (druzyna), Tyrion ... | w druzynie krola: ... | dzieci przy przyjezdzie: ... | pominieci: Cersei (gubernator town_EW4) | trasa: ..."
   - "przyjazd <osada> dzien X (odjazd od dnia Y) | eskorta przy krolu k/n (najdalej Z jedn.)"
   - "odjazd <osada> dzien X"
   - w linii doby: "orszak: faza, noga, cofniete cudze rozkazy dzis N (BK uczta / inne), bitwy orszaku 0, utknal 0, trzymane partie N"
   - "zwolnione: <kto> (<powod>)"
   - "przerwany: <powod>"
   Zadnych komunikatow dla gracza o ruchu (sceny opowiadaja).

=== A2: TURNIEJ REKI ===
GrandTourney/src/TourneyBehavior.cs:
- public static string ProclaimRoyal(string townId, string hostHeroId, string entrantIdsCsv) - same napisy, wolane odbiciem, zwraca opis do logu.
  - Odmowa: !Settings.Enabled, brak miasta, WarAtTheGates.
  - Istniejacy turniej w miescie: krolewski -> "juz ogloszony"; kazdy inny -> TourneyRansom.CallOff(game, town) (rozlicza go GT OnTournamentFinished).
  - Potem _events.Add("id|dzien|0|0|R:<host>:<e1>,<e2>") PRZED Campaign.Current.Models.TournamentModel.CreateTournament(town) + TournamentManager.AddTournament(g). OnTournamentStarted widzi wpis (Find != null) i nic nie robi.
  - Potem SummonLords(town, 0), Log.Info i Log.Player zawsze: "By order of {HOST}, a great tourney is proclaimed at {TOWN}. The lists open in {N} days."
- OnDailyTick dla wpisu R: bez galezi LocalWhenAtWar i bez reguly osmiu. Po GatherDays -> UpdateTournamentPrize(false, true), stan 1 z zachowaniem 5. pola, linia "Turniej krolewski w X otwarty, rycerzy N, kanon obecny k/m". Oblezenie odwoluje jak dotad.
- internal List<Hero> RoyalEntrants(Town) dla latki.
- SummonLords: pominac mp.Ai.DoNotMakeNewDecisions (partie prowadzone skryptem: orszak, oczekujacy).
GrandTourney/src/Patches.cs, NoblesFightPatch.Postfix: przy turnieju krolewskim najpierw wstawic uczestnikow kanonu.
- Uczestnik kanonu musi byc w miescie: CurrentSettlement == miasto albo PartyBelongedTo.CurrentSettlement == miasto (takze jako czlonek cudzej partii).
- Musi byc zdolny: zywy, nie ranny, nie jeniec, dorosly, nie IsNoncombatant, nie gracz.
- Miejsce: ostatni wypelniacz (nie bohater), potem ostatni lord spoza kanonu, gracza i rodu gracza.
- Linia "Szranki X: kanon w drabince k/m (brak: <kto> - <powod>)".
Armoury, nowy plik Prologue.Tourney.cs (albo sekcja w Road.cs):
- d112: Gregor i Loras -> trasa Go do KL i czekanie (silnik z A1); bez partii -> ApplyImmediateTeleportToSettlement(KL) w d121.
- d121: Ogar i inni uczestnicy bez partii -> ApplyImmediateTeleportToSettlement(KL); ProclaimRoyal odbiciem (AccessTools.TypeByName("GrandTourney.TourneyBehavior")), gospodarz Robert, wynik do logu; stan ht=1 dopiero po powrocie z wywolania.
- Brak GT -> "turniej Reki pominiety (brak GrandTourney)".
- CampaignEvents.TournamentFinished i TournamentCancelled dla town_EW4 przy ht=1 -> ht=2, zwolnic Gregora i Lorasa, linia "zakonczony dzien X | zwyciezca Y | kanon w drabince k/m | gracz startowal TAK/NIE". Zapas: zwolnienie w d140.
- Tag "Prolog (E5 turniej): ".
Opcjonalnie (tresc): scena 15 (indeks 14) czeka na koniec turnieju jak SceneArrest i dostaje zdanie o prawdziwym zwyciezcy w miejsce "Thoros wins the melee". Zmiana w JSON i generatorze PrologueScenes.Data razem.

=== CZEGO NIE ROBIC ===
Bez Army (cohesion, SCAI145, ArmyRestDay), bez wlasnego PartyComponent, bez dokladania wojska, bez puli zlota korony (nikt o nia nie prosil), bez zmiany nazw partii.

=== PROGI (autotest z armoury-prolog.flag, klucze TAK; A1 100 dob, A2 140 dob) ===
- "start" w dobie 0-1 z partia krola, eskorta >= 1.
- Winterfell: przyjazd d28-38, odjazd d55-56; KL: przyjazd d85-100.
- Bitwy orszaku bez gracza = 0; "utknal" = 0.
- Cofniete cudze rozkazy na partie na dobe <= 6 (wiecej = przepychanka).
- Po zapisie i wczytaniu: partie lordow z DoNotMakeNewDecisions bez wpisu orszaku = 0.
- Turniej: ogloszony d121 +-1, otwarty d125 +-1 jako krolewski (nie LOKALNY), koniec <= d137, kanon w drabince >= 3/4 (poza martwymi i jencami).
- W dniu 188 trzymane partie = 0; KONIEC PROLOGU w d186 nieopozniony przez orszak.
- Wydarzenia ROT w prologu = 0, potkniecia prologu = 0, straznik godzinowy < 1 ms.

Na koniec: CHANGELOG obu modow (Ryzyko / co sprawdzic) i STAN-PRAC.

**Ryzyka:**

- DoNotMakeNewDecisions idzie do zapisu gry ([SaveableProperty(11)]). Bez BeforeSave/SaveOver zapis wczytany starszym DLL zostawi krola i lordow zamrozonych na zawsze. Wzor FreeFolkRaids jest obowiazkowy.
- Przepychanka rozkazow: uczta BK co godzine robi DisableAi i SetMoveGoToSettlement (bez prefiksu orszak jedzie na uczte), a SummonLords w GT wzywa codziennie przez zjazd. Do tego alarm NightMarch daje Hold, a straznik godzinowy przywraca rozkaz. Licznik cofnietych rozkazow pokaze kazdy nowy przypadek.
- Jedzenie: gra kupuje je tylko w miastach i wsiach. Dluzszy postoj w zamku (Darry, Moat Cailin) grozi glodem i dezercja, wiec postoje sa tylko w miastach i wsiach.
- Rzeczywista predkosc kolumny (oboz nocny, premia traktu AiRoadRoutes, snieg na polnocy) jest nieznana. Dni przyjazdu moga odbiegac od 35 i 95; stale harmonogramu trzeba skalibrowac z pierwszego autotestu.
- Robert i Ned stoja w KL przez ok. 90 dni z wylaczonym mysleniem. Nie reaguja na zagrozenia ani wezwania; w pokoju prologu to akceptowalne, ale zwolnienie w TryKingFalls musi przyjsc przed areszt Neda, inaczej zostanie partia bez wodza z zamrozonym AI.
- Ukrycie (IgnoreForHours) chroni tylko przed AI. Gracz moze zaatakowac i pojmac krola (zmiana stron przez PlayerHostility przechodzi przez WarPrefix); wtedy trasa sie przerywa, a smierc krola w dniu 186 i tak wchodzi wymuszona.
- Gubernator (np. Cersei rzadzaca KL) nie zostanie przeniesiony do druzyny, bo AddHeroToPartyAction zdejmuje gubernatora. Zostaje w miescie z linia w logu; to odstepstwo od listy Jeffa.
- GT i wykup w szrankach: przegrany Robert, Jaime albo Loras placi wartoscia konia i zbroi (zasada GT). Unikatowe zbroje kanonu moga zmienic wlasciciela, a gra sama wpusci Roberta i Neda do turnieju jako wodzow partii w KL.
- Wpis GT dostaje 5. pole (R:...). Stary DLL GT przy otwarciu turnieju zgubi znacznik i turniej stanie sie zwyklym; nowy DLL czyta stare wpisy bez zmian.
- Scena 15 (dzien 125) opisuje wyniki, a gracz moze wygrac walki na arenie. Bez zmiany tresci (scena po koncu turnieju z prawdziwym zwyciezca) wyjdzie sprzecznosc.
- Wiersz p01 (Tyrion jeniec, dzien 110) musi dostac Tyriona juz zwolnionego. Obsluga niewoli wedlug decyzji Jeffa (bez ucieczek i okupow, Catelyn) to czesc B; tutaj tylko zwolnienie w dniu 108.
- Wywolania SetMove* wprost z DLL objetego siecia CrashScribe moga ominac latki (lekcja F7). Rozkazy tylko przez wejscie metody albo delegat.

**Pytania:**

- Scena 15 (Turniej Reki): przesunac ja na koniec turnieju i wstawic zdanie z prawdziwym zwyciezca w miejsce 'Thoros of Myr wins the melee'? Zalecam tak; zmiana w JSON i PrologueScenes.Data razem.
- Robert po powrocie: trzymac go w KL do smierci w dniu 186 (zalecane, jak w ksiazce i dla turnieju), czy zwolnic po turnieju?
- Cersei jako gubernator KL (jesli gra ja tak ustawi): zostawic ja w miescie (zalecane, bez skutkow ubocznych) czy zdjac z urzedu, zeby jechala z orszakiem?
- Pula zlota korony dla zwyciezcy (w ksiedze 40 tys. smokow) nie byla w prosbie i jej nie robie. Dopisac pozniej jako osobna rzecz?

## e5:B (badacz)

**Podsumowanie:**

E5 CZESC B - rozpoznanie (tylko odczyt, sprawdzone w dekompilacji 1.4.8 / ROT 8.1.8 / BK / Diplomacy / BKROT / NavalDLC; drzewo zb/m103-sceny).

1) TYRION - UCIECZKI I OKUPY. RealisticCaptivity obsluguje TYLKO niewole gracza (wszystkie latki RC na EndCaptivityAction maja warunek character == Hero.MainHero), wiec straz musi siedziec w Armoury (CanonCompanions), nie w RC. Tyrion trafia do lochu Orlego Gniazda (town_EN1, rod Arryn clan_vlandia_3, krolestwo "empire" = Dolina). Dolina i korona (vlandia, Lannisterowie sa w niej) sa w pokoju z paktem. Sciezki wyjscia z niewoli dla jenca AI w lochu:
(a) ucieczka 4%/dobe (PrisonerReleaseCampaignBehavior.DailyHeroTick) - przez 22 dni ok. 59% szans;
(b) "wypuszczenie przez pokoj" (ReleasePrisonersInternal) - zwalnia KAZDEGO bohatera, ktorego krolestwo nie walczy z wlascicielem lochu. Odpala sie przy dowolnym MakePeace z udzialem Doliny albo korony, przy kazdej zmianie krolestwa rodu (takze przy najemniku dolaczajacym do Doliny, np. Kamienne Wrony) i przy zmianie wlasciciela osady. Dla Tyriona w pokoju to bardzo prawdopodobne;
(c) okup AI-AI 10%/dobe (RansomOfferCampaignBehavior.DailyTickHero -> ConsiderRansomPrisoner, zastapiony prefiksem Armoury Ransom178): zloto rodziny jest przelewane PRZED ApplyByRansom;
(d) barter gracza (SetPrisonerFreeBarterable / TransferPrisonerBarterable) - zloto tez idzie przed zwolnieniem.
ROT zwalnia tylko wlasnych bohaterow (Stannis, Jon, Roose, Mance, Robb). BK ma tylko ApplyByReleasedAfterBattle, Diplomacy nie zwalnia nikogo. Wniosek: potrzebne sa trzy straze:
- prefiks na EndCaptivityAction.ApplyInternal - lapie wszystkie drogi bez zlota;
- prefiks na RansomOfferCampaignBehavior.DailyTickHero - zatrzymuje okup ZANIM poplynie zloto;
- postfiks na CampaignEventDispatcher.OnBarterablesRequested - usuwa barter za trzymanego jenca.
Samo blokowanie w ApplyInternal zlamaloby zamknieta ekonomie: okup bylby zaplacony, a jeniec zostalby w lochu.

2) JAIME W RIVERRUN. CanonCompanions.Captor juz obsluguje "prisoner:<osada>" (Settlement.Find -> st.Party). Riverrun to town_V2 (Tullyowie, krolestwo riverlands). Wystarczy zmienic #m01 w arm_canon_moves.tsv, ale Jaime w Riverrun ma te same artefakty "pokoju" i 4% ucieczki co Tyrion. Proponuje te sama akcje trzymania ("captive") z zapasem na clan_battania_1. Gdy Jaime jest juz jencem innej strony, przeniesienie robi TransferPrisonerAction.Apply (bez zdarzenia zwolnienia).

3) KLANY GORSKIE DOLINY. W ROT nie ma Burned Men, Milk Snakes ani Black Ears. Sa: Kamienne Wrony (stone_crows - pomniejsza frakcja najemna, kultura vale, przywodca Dolf, oddzialy skolderbrotva_tier_1-3 "Stone Crow Tribesman/Hunter/Savage"), Moon Brothers (moon_brothers - frakcja wyjeta spod prawa, kultura vale, przywodca Ulf, oddzialy wolfskins_tier_1-3; wedlug DefaultDiplomacyModel.IsAtConstantWar sa w stalej wojnie z Dolina) oraz klany bandyckie mountain_bandits "Highland Brigands" (Hillman/Brigand/Highwayman; kryjowki hideout_mountain_32 i _10 przy Dolinie) i hilltribesmen. Wzor spawnu z zamknieta ksiega ludzi: FreeFolkRaids.cs:
- ludzie z OutlawLaw.RaidDraw;
- partia z BanditPartyComponent.CreateLooterParty z domem przy osadzie (AI looterow patroluje i napada tabory);
- powrot przez Home + OutlawLaw.RaidDisband;
- wyjatki Owns w BandFood i OutlawLaw.
Nazwe nadaje Party.SetCustomName (MobileParty.Name czyta najpierw CustomName).

4) LUDZIE GORY. Gra zaczyna rabunek wsi przez partie AI w EncounterManager.StartSettlementEncounter, gdy ShortTermBehavior == RaidSettlement i FactionManager.IsAtWarAgainstFaction(partia, wies). Klan bandycki jest w stalej wojnie z kazdym krolestwem (DefaultDiplomacyModel.GetShallowDiplomaticStance), wiec banda "bez sztandarow" moze legalnie zrabowac wies Dorzecza bez wojny krolestw - dokladnie jak w ksiazce i w tekscie sceny 19. Precedens: BK BanditHeroComponent robi SetMoveRaidSettlement + DisableAi. Rabunek prowadzi i ksieguje DistrictRaid (E7/R1): ludzie, spichlerz, towar, "spalone w rabunku". Zlota nie ma - SmallGold.PayRaidLoot wymaga wodza-bohatera. Sluchacze RaidCompleted sa odporni na brak bohatera (ROT, Diplomacy, BKROT, BK RaidCapture; NavalDLC niesprawdzony). Zrodlo ludzi:
- Harrenhal (ROT_town32) nalezy w XML ROT do rodu Clegane (clan_vlandia_5, glowa Gregor lord_4_21) i lezy w Dorzeczu;
- przeniesienie zaloga -> partia i z powrotem przez PeopleBook.Transfer z przyczynami FFromGarrison / FToGarrison.
Ksiazkowe wsie istnieja: Sherrer (village_V2_4), Mummer's Ford (ROT_castle39_village1), Wendish Town (castle_village_EN7_1).

5) BERIC. W ROT Beric to bohater z wzoru ROTuniqueleader_spc_brotherhood_0: przywodca pomniejszej frakcji "brotherhood" (Brotherhood without Banners, wyjeta spod prawa, kultura vlandia, dom Riverrun), tworzony przez HeroCreator.CreateSpecialHero. Wedlug IsAtConstantWar (ta sama kultura co krolestwo) jest w stalej wojnie z krolestwem korony (vlandia) - niezgodne z rola "wyslannika Reki". Thoros i Anguy tez sa w tym rodzie. Rod Dondarrion (ROTclan_58, Blackhaven) ma glowe "Eric Dondarrion" (ROTlord_243). Proponuje: istniejaca partia Berica, a zapasowo glowa rodu Dondarrion; zamrozona AI, cel = najblizsza partia Ludzi Gory; bez tworzenia ludzi z niczego.

Calosc pod 4 kluczami MCM. Wszystko konczy sie w dniu 188: bandy wracaja i rozplywaja sie w puli, Ludzie Gory wracaja do zalogi Harrenhal, Beric wraca do zwyklego AI. Tyrion jest wolny w dniu 132 (wiersz #p02). Pliki robocze dekompilacji: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7aee5f12-7939-45a5-8ff5-01d4910ef5ca\scratchpad\e5b\ (PrisonerReleaseCampaignBehavior.cs, EndCaptivityAction.cs, RansomOffer.cs, EncounterManager.cs, RaidEventComponent.cs, bk_bhc.cs, bk_bandit.cs, rot_HeroSpawnPatches.cs, DefaultDiplomacyModel.cs).

**Plan:**

TOR: L (4 zmiany swiata + latki na sciezki niewoli). Galaz w-toku/m104-e5b z n31-calosc (po scaleniu wf_700623ba). Drzewo zb/m104-e5b. Kazdy krok: osobny commit + wpis w CHANGELOG. Repo: C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\m103-sceny (sciezki nizej wzgledem niego).

=== KROK 1 - JENCY FABULARNI (Tyrion 110-132, Jaime w Riverrun) ===
Pliki: Armoury/src/CanonCompanions.cs (klasa static -> partial) + nowy Armoury/src/CanonCompanions.Captives.cs + Armoury/ModuleData/arm_canon_moves.tsv.

1a. TSV:
- nowa akcja "captive:<cel>[,<cel2>]" = prisoner + trzymanie. Dopisac ja do naglowka #move.
- p01: prisoner:town_EN1 -> captive:town_EN1.
- m01: prisoner:clan_battania_1 -> captive:town_V2,clan_battania_1 (decyzja Jeffa 10:20 "ma byc w Riverrun").
- KnownActions i ArgActions += "captive".

1b. Execute "captive" (obok case "prisoner", ok. linia 1234):
- Captor() rozbija argument po przecinku; pierwszy wazny cel wygrywa.
- Osada jest wazna, gdy: OwnerClan != Clan.PlayerClan, SiegeEvent == null, MapFaction != h.MapFaction.
- Rod - jak dzis (siedziba albo partia glowy rodu).
- h juz jest jencem (nie gracza) i jego lochem nie jest cel: TransferPrisonerAction.Apply(h.CharacterObject, h.PartyBelongedToAsPrisoner, cap) - gra 1.4.8: PrisonRoster -1 / AddPrisoner, bez zdarzenia zwolnienia.
- Inaczej sciezka jak "prisoner" (LeaveParty, gubernator, LeaveSettlementAction, TakePrisonerAction.Apply).
- Na koniec: _held[klucz postaci] = rowId|stringId lochu|dzien.

1c. Koniec trzymania (_held.Remove + linia logu):
- przed Execute kazdego pozniejszego wiersza tej samej postaci (np. p02 free, m02 free) - zdjac PRZED EndCaptivityAction, wiec wlasne uwolnienie przechodzi;
- smierc;
- PlayerCaptor(h);
- !Captive(h) - odbity w bitwie albo osada zdobyta (ReleasedAfterBattle przepuszczone).

1d. Straze - Harmony, wpinane raz z instalacja latek CanonCompanions, klucz czytany na zywo:
- prefiks EndCaptivityAction.ApplyInternal(Hero prisoner, EndCaptivityDetail detail) - prywatna statyczna, duza metoda, bez ryzyka inline:
  - gdy klucz TAK, prisoner w _held i detail jest jednym z: ReleasedAfterEscape, Ransom, ReleasedAfterPeace, ReleasedByChoice, ReleasedByCompensation -> return false i licznik dla tego rodzaju;
  - przepuszcza Death i ReleasedAfterBattle;
  - lapie ucieczke 4%/dobe (DailyHeroTick) i pokoj (ReleasePrisonersInternal z OnMakePeace / ClanChangedKingdom / OnSettlementOwnerChanged).
- prefiks RansomOfferCampaignBehavior.DailyTickHero(Hero hero) (prywatna, wolana delegatem) -> false dla trzymanego. Jedyny wolajacy ConsiderRansomPrisoner, wiec ani okup gry (ExecuteAiBarter), ani prefiks Ransom178 (Execute przelewa zloto przed ApplyByRansom, Ransom178.cs:425) nie ruszy.
- postfiks CampaignEventDispatcher.OnBarterablesRequested(BarterData args): args.GetBarterables().RemoveAll(b => b to SetPrisonerFreeBarterable albo TransferPrisonerBarterable, a pole _prisonerCharacter wskazuje trzymanego). Odbicie pola jak Ransom178._fPrisoner.

1e. Zapis: "held\t<klucz>\t<row>\t<osada>\t<dzien>" w Export/Import arm_canon_compan.

1f. Log raz na dobe, gdy cos jest trzymane:
"Jency fabularni (E5): Tyrion Lannister [ROTwanderer8] w lochu The Eyrie (town_EN1) od dnia 110 | zablokowane dzis: ucieczka a, okup b, pokoj c, barter d, inne e (razem ...) | klucz Canon Captives Held TAK."
Przy zdjeciu: "... koniec trzymania (#p02 free / smierc / odbity / gracz)."

1g. Settings.cs: CanonCaptivesHeld = true. Hint EN: "Story captives stay captive: Tyrion Lannister in the sky cells of the Eyrie (Prologue 298, days 110-132) and Jaime Lannister in Riverrun as Robb Stark's prisoner cannot escape, be ransomed, bartered for or set free by a peace - they leave captivity when the story frees them, when the castle holding them falls, or if they die. Off = ordinary captives".

=== KROK 2 - KLANY GORSKIE DOLINY (od dnia 118) ===
Nowy plik Armoury/src/Prologue.World.cs (partial Prologue).

Warunek: Prologue.Active && BeforeStory() && S.PrologueValeClans && OutlawLaw.On && 118 <= PrologueDay() < 188.

Klan i ludzie:
- klan: Clan.BanditFactions, StringId "mountain_bandits" (Highland Brigands).
- ludzie: OutlawLaw.RaidDraw(region, 30, 12, clan, org, out roster, out fp, out fh); region = castle_EN2 (Bloody Gate), a gdy n <= 0 - town_EN1.
- Wynik: Hillman (CommonerFor), najpierw pula wyrzutkow regionu, potem domy ("bieda").

Utworzenie partii:
- BanditPartyComponent.CreateLooterParty("armoury_prl_clan", clan, Settlement.Find("castle_EN2"), false, null, castle_EN2.GatePosition), potem mp.MemberRoster.Add(roster). Przy bledzie - OutlawLaw.RaidUndraw (wzor FreeFolkRaids.cs:573-588).
- ActualClan = clan; Aggressiveness 0.9-1.0; InitializePartyTrade(0).
- Party.SetCustomName(new TextObject("{=ARM_PRL_CL1}Burned Men" | "{=ARM_PRL_CL2}Milk Snakes" | "{=ARM_PRL_CL3}Black Ears")).
- PeopleBook.Give(mp.MemberRoster, org, PeopleBook.FPoolBand); PeopleLedger.FixCreatedMen(mp).
- AI gry looterow, bez zamrazania: patrol wokol Bloody Gate, napady na tabory i karawany.

Limity:
- najwyzej 3 bandy w polu (twardy sufit);
- najwyzej 1 nowa na dobe;
- nowe tylko do dnia 181.

Prologue.OwnsBand(p) - tablica partii wlasnych (zbior Prologue). Dopisac "|| Prologue.OwnsBand(p)" obok FreeFolkRaids.Owns:
- BandFood.cs:149 i :308;
- OutlawLaw.cs:1334 (nie werbuje - twardy sufit ludzi).

Koniec (PrologueDay >= 188 albo klucz NIE):
- banda w bitwie - nastepna doba;
- banda trzyma gracza w niewoli - czeka;
- inaczej Home jak FreeFolkRaids.Home (FreeFolkRaids.cs:869-907): jency-bohaterowie ApplyByEscape(h, null, false), kiesa bandy do kiesy wsi regionu (MoneyLedger.Note169), towar do magazynu wsi, OutlawLaw.RaidDisband(region, mp). Najlepiej FreeFolkRaids.Home -> internal static i wolac ja zamiast kopii (jedna regula).
- Bandy zniszczone - MobilePartyDestroyed: straty i kto (jak FreeFolkRaids.OnPartyDestroyed).

Linia doby:
"Klany gorskie Doliny (E5): dzien N prologu | w polu b band, m ludzi [nazwa:ludzie, ...] | od poczatku: wyruszylo X w B bandach (z puli p, z domow d), wrocilo r, straty s | klucz TAK."
Kontrola: wyruszylo = wrocilo + straty + w polu.

=== KROK 3 - LUDZIE GORY (od dnia 155) ===
Warunek: Active && BeforeStory && S.PrologueMountainRaiders && 155 <= dzien < 188.

Zrodlo ludzi:
- zaloga Harrenhal (ROT_town32), gdy OwnerClan.StringId == "clan_vlandia_5" i brak oblezenia. Inaczej zaloga Clegane's Keep (castle_V4). Brak - linia "brak zrodla" i koniec.
- do 60 ludzi; najpierw mountains_man i clegane_*, potem reszta; z rannymi; w zalodze zostaje co najmniej 30% stanu; mniej niz 20 - brak.

Partia (jedna):
- BanditPartyComponent.CreateLooterParty("armoury_prl_mountain", Clan.BanditFactions "looters" (Broken Men), ROT_town32, false, null, ROT_town32.GatePosition).
- Przeniesienie: gB = g.MemberRoster ludzie, mB = 0; AddToCounts na partii, RemoveTroop / AddToCounts(-) na zalodze; PeopleBook.Transfer(g.MemberRoster, gB, mp.MemberRoster, mB, PeopleBook.FFromGarrison); PeopleLedger.FixCreatedMen(mp).
- InitializePartyTrade(0); SetCustomName("{=ARM_PRL_MTN}The Mountain's Men"); OwnsBand.

Cele:
- wies z MapFaction.StringId == "riverlands", VillageState == Normal, OwnerClan != PlayerClan, Devastation.CoversQuiet(v);
- kolejnosc: village_V2_4 (Sherrer), ROT_castle39_village1 (Mummer's Ford), castle_village_EN7_1 (Wendish Town), potem najblizsza wedlug MapDistanceModel.GetDistance, 0 < d < 50000 (jak BK IsRaidReachable).

Rozkaz: mp.Ai.SetDoNotMakeNewDecisions(true); mp.SetMoveRaidSettlement(v, MobileParty.NavigationType.Default, false). Rabunek startuje gra: EncounterManager:221-229 -> StartBattleAction.ApplyStartRaid. Lupienie i ksiega strat - DistrictRaid; zlota brak (PayRaidLoot wymaga wodza).

Hourly (tylko gdy partia zyje; bitwa = nic):
- wies celu Looted / BeingRaided przez kogos innego / DefaultBehavior != RaidSettlement bez MapEvent -> nastepny cel;
- licznik rabunkow i lista wsi (z MapEventStarted / RaidCompleted dla wlasnej partii).

Flaga DoNotMakeNewDecisions to [SaveableProperty(11)] - jak FreeFolkRaids.BeforeSave/SaveOver/Refreeze (FreeFolkRaids.cs:309-379): zdjac w OnBeforeSave, przywrocic w OnSaveOver, po wczytaniu w pierwszej godzinie.

Koniec (dzien >= 188 albo klucz NIE):
- zamrozony GoToPoint do bramy Harrenhal (najwyzej 30 dob);
- w domu (<= 4 jednostki): ludzie do zalogi (PeopleBook.Transfer z przyczyna FToGarrison); jency zwykli do Settlement.Party.PrisonRoster Harrenhal; jency-bohaterowie ApplyByEscape; towar do Harrenhal.ItemRoster; pusta partia DestroyPartyAction.Apply(null, mp);
- Harrenhal nie jest Clegane'a - najblizsza warownia rodu clan_vlandia_5, potem krolestwa vlandia.

Opcja do decyzji (pytanie 4): postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking jak FreeFolkRaids.KinPost (FreeFolkRaids.cs:681) - partie krolestwa vlandia nie atakuja Ludzi Gory i odwrotnie.

Linia doby:
"Ludzie Gory (E5): dzien N | partia <id> ludzi m (z zalogi Harrenhal m0, zaloga przed/po a/b) | cel <wies> (<faza>) | rabunki k: [wsie] | straty s, pojmani p | klucz TAK."
Przy powrocie: "wrocilo r do zalogi, towar t szt."
Kontrola: m0 = wrocilo + straty + w polu.

=== KROK 4 - ODDZIAL BERICA (od dnia 160) ===
Warunek: Active && BeforeStory && S.PrologueBericHunt && 160 <= dzien < 188.

Bohater:
- Hero.AllAliveHeroes z Template != null && Template.StringId == "ROTuniqueleader_spc_brotherhood_0" (warunek jak CanonCompanions.FindOriginal);
- zapas: Clan "ROTclan_58" .Leader (ROTlord_243);
- pominac, gdy: jeniec, gracz, w rodzie gracza, w druzynie gracza.

Partia:
- p = h.PartyBelongedTo, gdy p.LeaderHero == h && p.IsActive;
- p w armii AI - p.Army = null (jak CanonCompanions.LeaveParty); w armii gracza - pominac;
- brak partii - czekac do dnia 170, potem linia "brak partii" (bez tworzenia ludzi).

Hourly:
- SetDoNotMakeNewDecisions(true);
- cel = najblizsza zywa partia Ludzi Gory -> p.SetMoveEngageParty(cel, MobileParty.NavigationType.Default);
- brak celu -> p.SetMovePatrolAroundSettlement(ROT_castle39_village1, Default, false).

Zdejmowanie flagi przed zapisem - jak w kroku 3.

Koniec (dzien 188, klucz NIE, Beric martwy albo jencem): SetDoNotMakeNewDecisions(false); Ai.RethinkAtNextHourlyTick = true.

Linia:
"Oddzial Berica (E5): dzien N | Beric Dondarrion [brotherhood] partia m ludzi | cel: <partia>/patrol Mummer's Ford | bitwy z Ludzmi Gory k (wygrane w) | klucz TAK."

=== KROK 5 - PODPIECIE ===
Armoury/src/Prologue.cs, PrologueBehavior:
- DailyTick: Prologue.DailyWorld() po DailyEnd().
- HourlyTickEvent: Prologue.HourlyWorld() - szybki powrot bez partii.
- OnBeforeSaveEvent / OnSaveOverEvent.
- MobilePartyDestroyed / MapEventEnded dla wlasnych partii.
- SyncData: osobny klucz SaveText "arm_prl_world" (format jak arm_mur_najazdy, pamietac limit napisu 32 KB).
- ResetCampaign -> ResetWorld().

Klasyczny start: nic nie biegnie, latki zycia swiata nie sa potrzebne. Straze kroku 1 biegna w kazdej kampanii, w ktorej CanonCompanions jest aktywne (Jaime #m01 ma pusty tryb).

=== KROK 6 - USTAWIENIA, BUILD, TEST ===
Settings.cs, grupa "Prologue 298", klucze z hintami EN:
- CanonCaptivesHeld;
- PrologueValeClans ("from day 118 the hill clans of the Mountains of the Moon - Burned Men, Milk Snakes, Black Ears - come down on the high road around the Bloody Gate: up to three bands of hillmen drawn from the outlaws and the poor of the Vale; on day 188 they go back into the mountains");
- PrologueMountainRaiders ("from day 155 Ser Gregor Clegane's men ride out of Harrenhal without banners and raid the villages of the Riverlands - a real raid, with people, food and goods lost as in any raid; the men come from the Harrenhal garrison and return to it on day 188 with their plunder");
- PrologueBericHunt ("from day 160 Lord Beric Dondarrion leads his company into the Riverlands to hunt the Mountain's men, as Lord Eddard ordered; on day 188 he is his own master again").

Potem: python tools/gen_mcm.py; ./build.sh (kod 0); CHANGELOG (Problem / Przyczyna / Zmiana / Ryzyko / Status).

Grupa testow syntetycznych m104e5b:
- parsowanie captive z lista celow;
- Export/Import held i arm_prl_world;
- straz ApplyInternal: Escape i Peace blokowane, Death i AfterBattle przechodza, wlasne free przechodzi.

E6 - autotest prolog 230 dob (flaga armoury-prolog.flag) + wczytanie zapisu ok. dnia 165 (zamrozenie i odczyt). Progi:
- E5B-TY: Tyrion jeniec town_EN1 w kazdej linii doby 110-131; 0 uwolnien poza #p02; #p02 done w dniu 132; zablokowane >= 0 logowane.
- E5B-JA: po #m01 (war:battania:vlandia+5, ok. dnia 205+) Jaime jeniec town_V2 albo powod.
- E5B-VC: w dniach 118-120 >= 1 banda; w polu <= 3 kazdego dnia; w dniu 188-190 w polu 0; wyruszylo = wrocilo + straty.
- E5B-MR: w dniu 155 partia >= 20 ludzi z zalogi (roznica zalogi = m0); >= 1 rabunek do dnia 187 (linia DistrictRaid dla wsi Dorzecza z wodzem bez bohatera); w dniu 188-218 m0 = wrocilo + straty.
- E5B-BE: w dniu 160 Beric znaleziony i rozkaz, albo linia powodu; w dniu 188 puszczony.
- E5B-0: 0 blokow ERROR IN A MOD z Prologue.World / CanonCompanions.Captives; po OnBeforeSave 0 partii z DoNotMakeNewDecisions; potkniecia 0.

Wgranie tylko po tescie (zasady: kopia, gra zamknieta, Jeff nie gra).

**Ryzyka:**

- Straz na EndCaptivityAction.ApplyInternal pomija zwolnienie, ale ReleasePrisonersInternal i tak wysyla potem OnPrisonersChangeInSettlement(..., takenFromDungeon: true) dla trzymanego bohatera - falszywe zdarzenie dla sluchaczy (ksiegi Armoury, BK). W logu sprawdzic, czy nic nie liczy go jako wyjetego z lochu.
- Okup i barter MUSZA byc zatrzymane przed przelewem: Ransom178.Execute przelewa zloto przed ApplyByRansom (Ransom178.cs:425), a barter gry przenosi zloto przed Apply. Dlatego prefiks idzie na DailyTickHero i postfiks na OnBarterablesRequested, nie tylko na ApplyInternal. Inaczej zloto zostanie zaplacone, a jeniec nie wyjdzie (zlamana zamknieta ekonomia).
- Najemnik (np. Kamienne Wrony) dolaczajacy do Doliny albo do korony uruchamia ClanChangedKingdom -> ReleasePrisonersInternal. Bez strazy Tyrion wyszedlby w pierwszych dniach. Ze straza przy kazdej takiej zmianie przybywa linia 'pokoj' w liczniku - to oczekiwane.
- Flaga DoNotMakeNewDecisions to [SaveableProperty(11)] i trafia do zapisu gry. Bez zdejmowania w OnBeforeSave (wzor FreeFolkRaids) cofniecie DLL zostawi Ludzi Gory i partie Berica zamrozone na zawsze.
- Rabunek przez partie klanu bandyckiego bez bohatera: sciezka gry jest ta sama (EncounterManager:221). BK BanditHeroComponent robi to samo, wiec jest precedens. DistrictRaid/Devastation sa odporne na brak bohatera, ale nie byly testowane z banda bez wodza. NavalKingdomPolicyCampaignBehaviour.OnRaidCompleted jest nieprzeczytany. Pierwszy rabunek trzeba obejrzec w logu (ERROR IN A MOD, linia DistrictRaid).
- Lordowie Dorzecza i milicja wsi sa w wojnie z bandytami, wiec szybko rozbija Ludzi Gory (zamrozona AI nie ucieka). Rabunkow moze byc 0-1 przed Bericem. Partie Lannisterow (vlandia) tez beda atakowac Ludzi Gory - niezgodne z ksiazka, jesli nie dodamy postfiksu ShouldConsiderAttacking (jak FreeFolkRaids.KinPost).
- Bractwo bez Sztandarow (rod Berica) jest wedlug DefaultDiplomacyModel.IsAtConstantWar (ta sama kultura vlandia) w stalej wojnie z krolestwem korony. ROT, BK i BKROT tego nie nadpisuja (Diplomacy tylko czyta). Partie korony moga atakowac Berica w drodze. Stance trzeba potwierdzic w logu.
- Zaloga Harrenhal moze byc mala (KS, ekonomia zalog). Przy < 20 ludziach Ludzie Gory nie wyrusza, a zabranie 60 oslabia Harrenhal na 33 dni. Liczby w linii.
- Bandy klanow gorskich biora ludzi z puli i domow Doliny (TakeCommoners, przyczyna 'bieda'), czyli ubytek ludzi we wsiach Bloody Gate/Eyrie. Wracaja do puli dopiero w dniu 188.
- Banda trzymajaca gracza w niewoli w dniu 188: Home czeka, bo ApplyByEscape dla MainHero idzie sciezka PlayerCaptivity + RC. Inne bandy rozwiazywane normalnie.
- E5 czesc A (orszak Roberta, Tyrion na Mur) moze trzymac Tyriona w specjalnej partii w dniu 110. CanonCompanions LeaveParty rozwiaze partie lorda; partii niestandardowej trzeba to uzgodnic przy scaleniu.
- Zmiany kroku 1 wymagaja kampanii z flaga CanonCompanions (tylko nowe kampanie z paczka m101). Obecna kampania Jeffa sprzed WGRANIA 101 nie dostanie strazy ani Riverrun.
- Dzialania na partiach - najwyzej 5 obiektow (3 bandy + Ludzie Gory + Beric). Hourly biegnie tylko po wlasnej liscie, bez petli po wszystkich partiach (CLAUDE.md: setki obiektow = CTD).

**Pytania:**

- Jaime w Riverrun: czy trzymac go tak jak Tyriona (bez ucieczki, okupu, pokoju i barteru az do #m02 - BlackwaterEvent)? Rekomendacja TAK, pod tym samym kluczem: inaczej przy 4%/dobe ucieknie zwykle w ok. 25 dni, a 'pokoj' w Dorzeczu wypusci go od razu. Jeff powiedzial tylko 'ma byc w Riverrun'.
- Beric: uzyc bohatera ROT z Bractwa bez Sztandarow (wzor ROTuniqueleader_spc_brotherhood_0; jego rod jest wedlug gry w stalej wojnie z korona) czy partii glowy rodu Dondarrion (ROTlord_243 'Eric Dondarrion', Krainy Burzy - lojalny wobec korony, ale to postac wymyslona przez ROT)? Rekomendacja: Beric z Bractwa, Dondarrion jako zapas.
- Czy Ludzie Gory moga rabowac wsie nalezace do rodu gracza w Dorzeczu? Rekomendacja NIE (pomijamy wsie gracza).
- Czy dodac postfiks 'swoi nie bija swoich' (partie korony i Lannisterow nie atakuja Ludzi Gory i odwrotnie, wzor FreeFolkRaids.KinPost)? Bez niego Lannisterowie beda rozbijac 'swoich' bandytow.

## wojny:A (badacz)

**Podsumowanie:**

WOJNY A, tylko rozpoznanie (repo, drzewo i gra nietkniete). Wszystko sprawdzone w dekompilacji ROT 8.1.8, gry 1.4.8 i Diplomacy 1.4.7 oraz w drzewie m103-sceny.

(1) ROTStorylineWars.OnNewGameCreated zaklada 27 wojen fabularnych. Czas czytaja tylko 3 miejsca: ROTStorylineWar.Enforced (2 odczyty CampaignStartTime: StartDay i EndDay) i ROTStorylineWars.EnforceWars (1 odczyt, bramka "ElapsedDaysUntilNow < 1f"). Latka Armoury Prologue.Story.cs przestawia wszystkie 3 na start opowiesci. Skutek w prologu: przez 188 dni EnforceWars konczy prace na tej bramce (dzien opowiesci jest ujemny), wiec ROT nie wymusza zadnej wojny, takze essoskiej. CrashScribe Fabula.WarPrefix mnozy w Enforced StartDay i EndDay x4.33. Wojny essoskie (5,100) wypadaja wiec dzis w dniach opowiesci 22-433.

(2) Wojen czysto essoskich jest 8: Braavos-Lorath, Tyrosh-Lys, Pentos-Myr, Norvos-Qohor, Volantis-Aegon, Qarth-Daenerys, Dothrakowie-Daenerys, Sarnor-Ibben. Projekt: bramka dnia w EnforceWars zostaje na kalendarzu (wyjatek w ScanPatch). Na Enforced dochodzi prefiks, postfiks i finalizer (Armoury, zakladane leniwie razem z latkami opowiesci):
- wojna essoska czyta kalendarz (flaga ThreadStatic w StoryStartOf);
- kazda inna wojna w prologu, w kontekscie EnforceWars, dostaje false, czyli zachowuje sie jak dzis.
Wynik: Essos walczy od dnia kalendarza 22 do 433, Westeros dalej czeka na opowiesc. Klasyczny start bez zmian, bo oba zegary sa rowne.

(3) Pokoj krola (Prologue.Peace.cs) NIE obejmuje Essos. Zbior Realms ma 11 id z Westeros bez Wolnych Ludzi, WarPrefix i CallToWarPrefix przepuszczaja kazda pare z krolestwem z Essos, pakty sa tylko z Westeros. Jedyny niuans: w prologu korona nie wzywa do zadnej wojny swoich trzech sojusznikow (Polnoc, Krainy Burzy, Dorzecze).

(4) Reach-Dorne: wojna ROT 5..100 po rozciagnieciu trwa w dniach 22-433, a Renly ginie ok. 520-534 (Kronika). W chwili sojuszu Reach z korona ROT juz tej wojny nie wymusza. Pokoj trzeba jednak zawrzec samemu, a potem go utrzymac. Game AllianceCampaignBehavior.StartAlliance nie sprawdza CanMakeAlliance (CanonAlliances go nie zablokuje), ale od razu sklada wnioski wezwan do wojny dla wojen obu stron. Kolejnosc: MakePeaceAction.Apply(Reach, Dorne) -> ewentualny pokoj korona-Reach -> StartAlliance(korona, Reach) przez interfejs -> przedluzanie konca sojuszu (gra daje 84 dni; prywatna UpdateAllianceEndTime, jak w Prologue.Peace) -> pakt o nieagresji Diplomacy Reach-Dorne. Diplomacy 1.4.7 nie ma wlasnych sojuszy, korzysta z sojuszy gry.

(5) Dorne: rekomenduje PAKT O NIEAGRESJI z korona, nie sojusz. Sojusz z Dorne:
- nie jest na liscie CanonAlliances, wiec zostalby zerwany przy wczytaniu i blokowany przy odnowieniu;
- StartAlliance wciagnalby Dorne wnioskami wezwan do wojen korony.
Pakt startuje przy Kronice nr 19 "Myrcella odplywa" (dni 215-390), awaryjnie w dniu 390. Trwa do najazdu Daenerys z ROT (DANY_INVASION_DORNE_WESTERLANDS), zgodnie z Kronika ("Dorne zostaje w pokoju az do najazdu Daenerys").
Sojusz Reach z korona startuje przy Kronice nr 27 "Roze przechodza na strone lwa" (dni 540-700), awaryjnie w dniu 700.
Nowy kod Armoury CanonPacts.cs dziala w obu startach, bez latek Harmony.

Tor M (Essos S/M, Reach/Dorne M), ok. 0,5-1 dnia z testami.

**Plan:**

=== 0. FAKTY (dekompilacja, do cytowania w CHANGELOG) ===
Zrodla: C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/pytania-tmp/dec/ROT.CampaignBehaviors.ROTStorylineWars.cs, ...ROTStorylineWar.cs; gra: TaleWorlds.CampaignSystem AllianceCampaignBehavior, MakePeaceAction, DefaultAllianceModel; Diplomacy 1.4.7: DiplomaticAgreementBehavior, NoNonAggressionPactCondition.

- Odczyty CampaignStartTime w wojnach fabularnych sa tylko 3 (rot-grep-start.txt:36-38): Enforced x2 i EnforceWars x1 (bramka "< 1f").
- IsWarForced -> Enforced czytaja:
  - ROTKingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms (blokada pokoju);
  - nasze PovertyPeace, StorylineReparations i PeaceHold.
- EnforceWars:
  - co dobe usuwa z listy wojny krolestw zniszczonych;
  - dla Enforced == true i stron nie w wojnie: CallingDeclareWar = true, potem DeclareWarAction.ApplyByDefault.
- Fabula.WarPrefix/WarFinalizer (CrashScribe) na Enforced: StartDay/EndDay x FabulaTimeScale (4.33), Math.Round; po wywolaniu przywraca.
- Game StartAlliance:
  - bez CanMakeAlliance i bez sprawdzenia wojny;
  - koniec sojuszu = Now + MaxDurationOfAlliance (84 d);
  - dla kazdej wojny strony z krolestwem, z ktorym druga strona nie walczy, sklada ProposeCallToWarAgreementDecision.
  - DailyTickClan: po terminie EndAlliance i AddAllianceDecision (glos).
- MakePeaceAction.Apply(f1, f2) = ApplyInternal(..., 0, 0, Default). Diplomacy go nie latuje; ROT patch27 blokuje tylko Bialych Wedrowcow.
- Diplomacy NAP:
  - blokuje tylko DeclareWarConditions (AI ConsiderWar i UI), NIE sam DeclareWarAction;
  - ROT EnforceWars i wezwania do wojny przechodza mimo paktu;
  - przy OnAllianceStarted Diplomacy gasi NAP tej pary.
- Id ROTKingdoms:
  - Westeros: battania (Polnoc), vlandia (korona), riverlands, dragonstone, stormlands, empire_w (Reach), empire (Dolina), sturgia (Zelazne Wyspy), aserai (Dorne), nightswatch, freefolk, skagosi;
  - Essos: bravos, nord (Lorath), pentos, norvos, qohor, myr, lys, tyrosh, volantis, sarnor, ibb, qarth, khuzait (Dothrakowie), empire_s (Daenerys), valyrian (Aegon), yiti;
  - summer (Wyspy Letnie) poza wojnami.
- Postacie i rody: Tyrell clan_empire_west_1, Mace lord_1_7, Martell clan_aserai_1, Doran lord_3_1, Myrcella lord_4_10, Renly lord_1_9.

=== 1. LISTA 27 WOJEN ===
Format: kolejnosc ROT | strony | StartDay/EndDay ROT -> dni opowiesci x4.33 | wyzwalacz.
1 Smocza Skala-korona | 135/- -> 585 | brak
2 korona-Dorzecze | 30 -> 130 | brak
3 Reach-Dorzecze | 0/0 | FIVE_KINGS_REACH_RIVERLANDS: wlacza OnRenlysDeath
4 Reach-Polnoc | 0/0 | FIVE_KINGS_REACH_NORTH: ROT tylko wylacza (Krwawe Gody), wlacza CrashScribe CanonReachNorthWar przy smierci Renly'ego
5 Reach-Smocza Skala | 135 -> 585 | brak
6 Krainy Burzy-korona | 135 -> 585 | brak
7 korona-Reach | 135 -> 585 | FIVE_KINGS_WESTERLANDS_REACH: na starcie wlaczona, OnRenlysDeath wylacza
8 Zelazne Wyspy-Polnoc | 100/235 -> 433/1018 | brak
9 Dolina-Zelazne Wyspy | 5/100 -> 22/433 | brak
10 Reach-Dorne | 5/100 -> 22/433 | brak
11 Dolina-korona | 0/0 | DANY_INVASION_VALE_WESTERLANDS
12 Daenerys-korona | 0/0 | DANY_INVASION_TARGARYEN_WESTERLANDS
13 Dorne-korona | 0/0 | DANY_INVASION_DORNE_WESTERLANDS
14 Daenerys-rod gracza | 0/0 | DANYS_REVENGE (kradziez smoka)
15 Smocza Skala-Krainy Burzy | 0/0 | ZAWSZE
16 Nocna Straz-Wolni Ludzie | 0/0 | ZAWSZE (i tak w wojnie z XML: isAtWar w ROT_spkingdoms.xml)
17 Polnoc-korona | 0/0 | FIVE_KINGS_NORTH_WESTERLANDS: wlacza OnNedDeath, wylacza Krwawe Gody (robbAlive = false)
18-25 ESSOS | 5/100 -> 22/433 | brak: bravos-nord, tyrosh-lys, pentos-myr, norvos-qohor, volantis-valyrian, qarth-empire_s, khuzait-empire_s, sarnor-ibb
26 Aegon-korona | 0/0 | AEGON_INVASION_WESTERLANDS
27 Polnoc-korona | 350/- -> ~1516 | brak

=== 2. ESSOS NA KALENDARZU (Armoury, Prologue.Story.cs) ===
Plik: C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/m103-sceny/Armoury/src/Prologue.Story.cs

a) Bramka EnforceWars na kalendarzu. W ScanPatch, obok IsCampaignAgeType, dodac:
   IsCalendarMethod(t, m): t.FullName == "ROT.CampaignBehaviors.ROTStorylineWars" && m.Name == "EnforceWars" -> cal += n; continue (bez transpilera).
   - Bezwarunkowo: InstallStory idzie raz na proces, a klucz obsluguje postfiks z pkt c.
   - Do linii "latki opowiesci ROT" dopisac: ", bramka EnforceWars na kalendarzu " + cal (ma byc 1).
   - Poprawic komentarz w summary: podmienionych bedzie 34, nie 35.

b) Zbior EssosIds: { bravos, nord, pentos, norvos, qohor, myr, lys, tyrosh, volantis, sarnor, ibb, qarth, khuzait, empire_s, valyrian, yiti }.
   - Komentarz: Daenerys i Aegon na starcie w Essos; ich wojny z korona (11-13, 26) maja korone, wiec nie sa essoskie.
   - IsEssosWar(object war): Faction1 i Faction2 (wlasciwosci ROTStorylineWar, odczyt odbiciem, pamiec w ConditionalWeakTable) to Kingdom i oba StringId w EssosIds.
   - Rod gracza (wojna 14) -> nie.

c) Latki zakladane w InstallStory po ScanPatch (ten sam Harmony "com.jeff.armoury.prologue", leniwie, tylko w procesie z kampania z prologiem):
   - ROTStorylineWars.EnforceWars:
     - prefiks: _inEnforce++;
     - finalizer: _inEnforce-- (zwraca __exception).
   - ROTStorylineWar.Enforced:
     - prefiks (object __instance, out bool __state): __state = Active && S.PrologueEssosWarsCalendar && IsEssosWar(__instance); jesli __state -> _calendarDepth++;
     - postfiks (ref bool __result, bool __state): jesli _inEnforce > 0 && __result && BeforeStory() && !__state -> __result = false; _westHeldDay++. Jesli __result && __state && _inEnforce > 0 -> _essosForcedDay++;
     - finalizer (bool __state, Exception __exception): jesli __state -> _calendarDepth--; return __exception.
   - Pola [ThreadStatic] static int _calendarDepth, _inEnforce.
   - StoryStartOf(model): na poczatku "if (_calendarDepth > 0) return model.CampaignStartTime;".
   - Nie uzywac prefiksu return false. Postfiks nie ma problemu kolejnosci z Fabula.WarPrefix (CrashScribe); jej finalizer i tak przywraca daty.
   - Skutek:
     - prolog, klucz TAK: 8 wojen essoskich od dnia kalendarza 22 do 433. Peace decision (IsWarForced) dla tych par blokowana tak samo jak w klasycznym starcie;
     - wojny westeroskie, takze 15 i 16 (0/0), w prologu NIE sa wymuszane, jak dzis;
     - klucz NIE: wszystkie wojny czekaja na opowiesc, jak dzis;
     - kampania klasyczna: zero zmian.

d) Linia InstallStory: "wojny essoskie: Enforced TAK/BRAK, EnforceWars TAK/BRAK".

e) StartLine (Prologue.cs, gdy Active): "| wojny fabularne ROT: na liscie N, essoskich K (bravos-nord, ...), kalendarz TAK/NIE (klucz)".
   Odczyt listy: AccessTools.Field(ROT.SubModule, "StorylineWars") -> pole "_storyLineWars".

f) Linia doby prologu (Prologue.Daily, tylko w prologu i 20 dni po nim): "| wojny fabularne ROT: essoskie wymuszone dzis E (par essoskich w wojnie W/8), westeroskie wstrzymane do opowiesci H". Liczniki dobowe zerowane.

g) Komentarze do poprawienia:
   - Prologue.Peace.cs summary pkt (1): "Wojny fabularne ROT i tak nie ruszaja przed opowiescia" -> "... poza 8 wojnami czysto essoskimi (kalendarz, Prologue.Story)";
   - docs/PROJEKT-PROLOG-298 E1.

h) Klucz MCM Armoury (Settings.cs, blok "Prologue 298", potem python tools/gen_mcm.py):
   public bool PrologueEssosWarsCalendar = true;  // in a Prologue 298 campaign, Realm of Thrones storyline wars fought between two Essos realms (Free Cities, Dothraki, Qarth, Daenerys, Aegon) count their days from the campaign start, so Essos is at war during the prologue; Westeros storyline wars still wait for the story. Off = every storyline war waits for the story
   Czytany na zywo w prefiksie.

i) Pokoj krola (Prologue.Peace.cs) BEZ ZMIAN: Realms to 11 id Westeros bez freefolk. WarPrefix, CallToWarPrefix (realmWar wymaga obu stron w Realms) i pakty Essos nie dotycza. Sprawdzic tylko w logu: "zablokowane wypowiedzenia" bez par essoskich.

=== 3. REACH I DORNE Z KORONA (Armoury, nowy plik CanonPacts.cs + CanonPactsBehavior) ===
Gdzie i dlaczego:
- Armoury, bo ma juz Diplomacy NAP i UpdateAllianceEndTime (Prologue.Peace) i tu powstanie Kronika (nr 19 i 27).
- Dziala w kazdej kampanii (klasyczna i prolog), bez Harmony: tylko DailyTick i SyncData.
- Rejestracja w SubModuleMain.cs obok PrologueBehavior (ok. linii 637).
- Zapis: SaveText.Sync(dataStore, "arm_canonpacts", ref v), format "v=1;rc=0|1|2|3;rcd=;rcs=;dc=0|1|3;dcd=;dcs=;". Znaczenie rc: 0 czeka, 1 trwa, 2 Tyrellowie odmowili, 3 zakonczone.
- Pomocnicze z Prologue.Peace zmienic na internal, bez zmiany logiki: DipReady, NapOf, _mNapApply, _mExpire. Dodac lazy UpdateEndMethod(): w klasycznej kampanii ApplyPeace sie nie wpina, wiec _mUpdateEnd jest null.
- Tag logu: "Kanon korony (KK): ".

API dla Kroniki:
- CanonPacts.ReachJoinsCrown(string source) - nr 27, wersja "Tyrellowie przechodza";
- CanonPacts.ReachRefuses(source) - nr 27, wersja "odmawiaja": rc = 2, bez terminu awaryjnego;
- CanonPacts.DorneJoinsCrown(source) - nr 19.
Terminy awaryjne w dobie, liczone w dniach opowiesci (Prologue.StoryStart albo StoryStartTime modelu):
- Reach: dzien 700 (koniec okna nr 27);
- Dorne: dzien 390 (koniec okna nr 19).
Do czasu napisania Kroniki terminy awaryjne sa jedynym wyzwalaczem.

ReachJoinsCrown - warunki:
- klucz CanonReachCrown;
- Renly nie zyje (Hero lord_1_9 !IsAlive albo RenlyDeathEvent zakonczone);
- empire_w i vlandia istnieja;
- empire_w.RulingClan == clan_empire_west_1, Mace zyje;
- empire_w nie w sojuszu ze Smocza Skala;
- zadne z dwoch krolestw bez wladzy gracza (RulingClan != PlayerClan).
Kroki (KOLEJNOSC WAZNA: StartAlliance sklada wnioski wezwan dla wojen obu stron):
1. Jesli Reach w wojnie z aserai:
   - jesli ROT IsWarForced(Reach, Dorne) (przypadek brzegowy) -> wpis wojny 10 (szukac po stronach w ROTStorylineWars.Wars, Id NONE): IsEnabled = false;
   - potem MakePeaceAction.Apply(Reach, Dorne).
2. Jesli korona w wojnie z Reach i !IsWarForced -> MakePeaceAction.Apply(vlandia, empire_w).
3. beh = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>(); beh.StartAlliance(vlandia, empire_w). Jesli juz sa sojusznikami, gra sama wraca.
4. UpdateEndMethod().Invoke(beh, {vlandia, empire_w, Now + 364 d}). Metoda tylko przedluza.
5. NAP Reach-Dorne: _mNapApply(empire_w, aserai, false, true, (float?)max(364, 2200 - dzienOpowiesci), false). Potem sprawdzic NapOf != null.
6. rc = 1, rcd, rcs.
Doba przy rc == 1:
- brak sojuszu, a strony nie w wojnie -> StartAlliance ponownie, linia;
- w wojnie -> nie odnawiac, linia "zerwany wojna (DeclareWarDetail z WarDeclared, jesli zlapany)";
- koniec za mniej niz 182 d -> przedluzyc do Now + 364;
- brak NAP Reach-Dorne, a pokoj -> zalozyc;
- koniec utrzymania (rc = 3), bez zrywania, sojusz wygasa sam: krolestwo zniszczone, Reach bez Tyrellow, gracz wladca ktorejs strony, przyszle CanonPacts.ReachLeavesCrown z Kroniki nr 95.

DorneJoinsCrown - warunki:
- klucz CanonDorneCrown;
- aserai i vlandia istnieja;
- aserai.RulingClan == clan_aserai_1;
- Myrcella lord_4_10 zyje (dla terminu awaryjnego);
- najazd Daenerys nie ruszyl (wpis DANY_INVASION_DORNE_WESTERLANDS: IsTriggered false);
- bez gracza wladcy.
Kroki:
1. Jesli korona w wojnie z Dorne i !IsWarForced -> MakePeaceAction.Apply.
2. NAP korona-Dorne, czas jak wyzej (do ok. dnia 2200, z zapasem po najezdzie ok. 1949).
3. dc = 1.
Doba: brak NAP i pokoj -> zalozyc ponownie.
Koniec przy najezdzie Daenerys: DANY_INVASION_DORNE_WESTERLANDS IsTriggered albo korona w wojnie z Dorne -> _mExpire na NAP korona-Dorne i Reach-Dorne, dc = 3, linia. Wykrywane w dobie, wiec nadrabia tez po wczytaniu.

Dlaczego dla Dorne NAP, nie sojusz:
- CanonAlliances (CrashScribe) nie ma Korona+Dorne na liscie: (2) blokuje odnowienie, (3) zrywa przy wczytaniu;
- StartAlliance wciagnalby Dorne wnioskami wezwan do wojen korony (Polnoc, Dorzecze, Stannis), czego w kanonie nie ma.
CrashScribe CanonAlliances BEZ ZMIAN: Korona+Reach jest dozwolone po smierci Renly'ego (Allowed, why null), wiec (3) go nie zerwie.

Linie logu:
- Start sesji: "Kanon korony (KK): start sesji | klucze Reach TAK/NIE, Dorne TAK/NIE | Diplomacy TAK/BRAK | koniec sojuszu (UpdateAllianceEndTime) TAK/BRAK | Reach z korona: czeka (termin 700) / TAK od dnia N (zrodlo) / odmowa / zakonczone (powod) | Dorne z korona: ... | dzien opowiesci D".
- Zdarzenie: "Kanon korony (KK): dzien opowiesci N - REACH Z KORONA (Kronika nr 27 / termin 700): pokoj Reach-Dorne TAK (wojna ROT wymuszona: NIE), pokoj korona-Reach -, SOJUSZ zalozony (koniec dnia Y), wnioski wezwan zlozone przez gre: k, pakt Reach-Dorne TAK (do dnia Z)".
- Analogicznie "DORNE Z KORONA" i "NAJAZD DAENERYS - pakty Dorne wygaszone 2".
- Doba tylko przy zmianie: odnowienia, przedluzenia, zerwania, wypowiedzenia wojny w parach korona-Reach, Reach-Dorne, korona-Dorne mimo paktu (nasluch CampaignEvents.WarDeclared, z DeclareWarDetail).

Klucze MCM Armoury (osobno, jedno zjawisko na klucz; potem gen_mcm.py):
- public bool CanonReachCrown = true;  // after Renly's death the Reach allies with the crown and makes peace with Dorne, with a non-aggression pact between the Reach and Dorne (Diplomacy). Off = the Reach makes its own alliances
- public bool CanonDorneCrown = true;  // after Myrcella's betrothal Dorne keeps a non-aggression pact with the crown (Diplomacy) until Daenerys invades. Off = no pact
Progi (stale, decyduje Claude): termin Reach 700, termin Dorne 390 (dni opowiesci; koniec okien Kroniki nr 27 i 19); sojusz przedluzany do Now + 364 przy mniej niz 182 d; NAP do dnia opowiesci 2200, nie krocej niz 364 d.

=== 4. TEST (autotest; nie gdy Jeff gra) ===
- T1 prolog (plik armoury-prolog.flag), 40 dni:
  - "bramka EnforceWars na kalendarzu 1";
  - "wojny essoskie: Enforced TAK";
  - od dnia kalendarza 22: "essoskie wymuszone > 0", pary essoskie w wojnie (KRONIKA WOJEN);
  - "wojny Westeros-Westeros 0", "zablokowane wypowiedzenia" bez dragonstone-stormlands co dobe;
  - "wydarzenia ROT ruszone 0".
- T2 klasyczny, 40 dni: wojny essoskie od dnia 22 jak przed zmiana (porownac z biegiem bez DLL).
- T3 klasyczny 400 d (bieg roczny): "DORNE Z KORONA (termin 390)", NAP widoczny w SOJUSZE/Diplomacy.
- T4 Reach: dzien 700 wymaga ok. 2 lat gry. Probna DLL tylko do autotestu ze stalymi terminami obnizonymi i Renly zabitym konsola (zgoda na autotest z 07.10), potem przywrocic zatwierdzona.
  Sprawdzic:
  - pokoj Reach-Dorne PRZED "SOJUSZ zalozony";
  - brak wnioskow wezwan przeciw Dorne;
  - po 90 d sojusz trwa ("do dnia" sie przesuwa);
  - SK bez "[poza kanonem]" i bez zerwania przy wczytaniu.
- CHANGELOG: dwa osobne wpisy (Essos; Reach/Dorne) z sekcja "Ryzyko / co sprawdzic" z pkt risks.

**Ryzyka:**

- Na ROTStorylineWar.Enforced beda trzy latki naraz: transpiler Armoury, prefiks/finalizer CrashScribe Fabula i nowy prefiks/postfiks/finalizer Armoury. Finalizer musi zawsze zdjac _calendarDepth, inaczej wszystkie odczyty ROT przeszlyby na kalendarz. Nie uzywac prefiksu 'return false' (kolejnosc prefiksow Harmony).
- Wyjatek bramki EnforceWars w ScanPatch rozpoznaje metode po nazwie. Po aktualizacji ROT moze cicho przestac dzialac: linia 'bramka EnforceWars na kalendarzu' musi pokazywac 1, przy 0 Essos dalej czeka na opowiesc.
- IsWarForced dla 8 par essoskich jest w prologu prawdziwe w dniach kalendarza 22-433. Zmienia to wyniki PovertyPeace (nie proponuje pokoju), StorylineReparations i PeaceHold, ROTKingdomDecisionPermissionModel blokuje pokoj. To spojne z klasycznym startem, ale trzeba to wpisac do CHANGELOG.
- Kronika nr 5 (Stos khala Drogo, 'dni 22-60, gdy rusza wojna Hordy Dothrakow') w prologu wypada teraz w dniach KALENDARZA 22-60 (opowiesc -166..-128). Okno trzeba wiazac ze startem wojny albo z kalendarzem, nie z dniem opowiesci.
- StartAlliance gry od razu sklada ProposeCallToWarAgreementDecision dla wszystkich wojen obu stron, a pieniadze wezwan rozlicza latka 168 CrownCallToWar. Pokoj Reach-Dorne MUSI byc przed StartAlliance, inaczej Reach wezwie korone przeciw Dorne mimo paktu korona-Dorne. Korona moze tez wezwac Reach do wojen spoza kanonu (np. wojna AI z Essos).
- Pakt o nieagresji Diplomacy blokuje tylko decyzje AI o wojnie (DeclareWarConditions), nie sam DeclareWarAction. ROT EnforceWars, wezwania do wojny, mody wypowiadajace wojne bezposrednio (np. AIInfluence) i gracz moga go ominac. Linia doby liczy takie wypowiedzenia. Twarda blokade dopisac tylko, jesli log pokaze naruszenia.
- Sojusz gry wygasa po 84 dniach. Przedluzenie idzie przez prywatna AllianceCampaignBehavior.UpdateAllianceEndTime (odbicie). Gdy zmieni sie sygnatura, sojusz wygasnie, a AI zaglosuje o odnowienie. Wtedy CanonPacts zalozy go ponownie nastepnej doby (linia 'odnowiony'), ale warto patrzec na 'koniec sojuszu TAK/BRAK' w linii startu.
- Sojusz z Dorne zamiast paktu zostalby zerwany przez CanonAlliances (3) przy wczytaniu i zablokowany przy odnowieniu (2). Dlatego Dorne ma TYLKO pakt; nie zmieniac tego bez zmiany listy CanonAlliances w CrashScribe.
- Stary zapis bez klucza arm_canonpacts, ktory jest juz po dniu 390 albo 700, odpali termin awaryjny przy pierwszej dobie po wczytaniu. Kampania Jeffa jest w prologu, wiec dzis nie dotyczy, ale trzeba to wpisac do CHANGELOG.
- Zbior EssosIds jest staly (id ROT 8.1.8). Inna wersja ROT moze zmienic id: linia startu ma pokazywac 'essoskich 8'.
- Przy rozpoznaniu Renly'ego, Myrcelli i Tyrellow po id postaci (lord_1_9, lord_4_10, clan_empire_west_1) zmiany prologu (CanonCompanions, przenosiny rodow) moga zmienic wladce Reach i Dorne. Warunki wtedy cicho nie przejda; linia startu ma pokazywac powod 'czeka'.
- Test sojuszu Reach (dzien 700) wymaga probnej DLL z obnizonymi progami tylko na czas autotestu. Nie testowac, gdy Jeff gra (monitoring od 15.10).

**Pytania:**

- Czy Daenerys (empire_s) i Aegon (valyrian) zaliczac do Essos? Przyjalem, ze TAK, bo na starcie ROT trzymaja ziemie w Essos, a ich wojny z korona (11-13, 26) maja korone i nie sa essoskie. Bez nich wojny Qarth-Daenerys, Dothrakowie-Daenerys i Volantis-Aegon czekalyby na opowiesc.
- Opisy w Kronice nr 27 ('bez wymuszania pokoju') i nr 19 ('Relacja Dorne z korona rosnie') sa sprzed decyzji Jeffa z 21.10. Trzeba je poprawic na: sojusz z korona plus pokoj i pakt Reach-Dorne (nr 27) oraz pakt korona-Dorne (nr 19), zeby Kronika i CanonPacts mowily to samo.
- Czy w prologu wymuszac tez wojne 16 (Nocna Straz - Wolni Ludzie, 0/0)? Dzis w prologu nie jest wymuszana, choc w XML strony zaczynaja w wojnie. Plan zostawia stan obecny (bez zmian 'przy okazji').

## wojny:B smoki (badacz)

**Podsumowanie:**

ROZPOZNANIE: SMOKI DAENERYS W ROT (tylko odczyt, nic nie zmienialem)

1. Jak sa zrobione
- ROT-Dragon nie ma DLL. To same zasoby: ModuleData/monsters.xml (Monster "dragon" chodzacy i "dragonfly" latajacy: hit_points 5000, weight 8000, kapsula radius 4.5, family_type 3), monster_usage_sets.xml, action_sets.xml, dzwieki i NavMesh. SubModule laduje tylko Monsters.
- Smoki to przedmioty Type="Horse" (wierzchowce) w ROT-Content/ModuleData/ROTassets.xml, linie 6029-6146. Jest 6 smokow chodzacych (Monster.dragon) i 6 latajacych o tym samym id z koncowka "2" (Monster.dragonfly, speed 100). Wszystkie sa is_merchandise=false i skeleton_scale="dragon_green" (wszystkie kosci 1.0):
  - dragon_black "Drogon": speed 45, charge 200, body_length 105, extra_health 1000, difficulty 200
  - dragon_brown "Rhaegal": body_length 85
  - dragon_green "Viserion": body_length 75
  - dragon_gold: 95
  - dragon_smoke: 105
  - dragon_red "Draeghar": 105, nagroda z questu Valyrian Thief
- Cala logika jest w ROT-Core ROT.dll:
  - ROT.MissionBehaviors.ROTDragonMissionBehavior. OnDeploymentFinished wola po kolei: AIDragon.SpawnMissingDragonMounts, AIDragon.Initialize (zamiana w smoka latajacego: TransformDragonForm, id+"2"), a na koniec daje kazdemu smokowi HP = ROTSettings.DragonHealth. Ogien to FireTickInternal i FireTickBody: Blow(jezdziec.Index) z pustym WeaponRecord i InflictedDamage = DragonDamageScaling. Szarza to ChargeTick, zasieg ognia 50 m.
  - ROT.Dragon.* (AIDragon, PlayerDragon, DragonMountComponent, AgentPatch = prefiks na Agent.RegisterBlow).
  - ROT.Models.ROTCombatSimulationModel.SimulateHit: w autobitwie, gdy bijacy ma smoka w slocie 10, wynik zamienia sie na plaskie DragonDamageScaling.
  - ROTDragonBehavior: na mapie tylko perk Beast Whisperer (smoki wylaczone).
  - ROTGankBehavior: odbieranie smoka jencowi. Do tego OnHeroPrisonerReleased: gdy Daenerys wychodzi z niewoli z PUSTYM slotem konia, ROT daje jej pierwszego nieskradzionego smoka (black, brown, green).
- Ustawienia MCM ROT w grupie "Dragons". Domyslnie: DragonHealth 5000, DragonDamageScaling 2000, LethalDragonDamage 0, FriendlyFire off. U Jeffa (RealmOfThrones_v1.json z 24.08): DragonHealth 10000, DragonDamageScaling 10, FriendlyFire true.

2. Czy smoki sa od startu
- TAK. Daenerys (lord_1_14) dostaje w lords.xslt:1007 zestaw ghi_bat_template_dany (ROT_sandboxcore_equipment_sets.xml:6361-6377, slot Horse = Item.dragon_black).
- Jezdzi wiec doroslym Drogonem od 1. dnia ROT. W prologu 298 tez, od dnia -188, bo prolog startuje z tym samym swiatem.
- W bitwie jest JEDEN smok: jej wierzchowiec. Rhaegal i Viserion istnieja tylko jako przedmioty zapasowe po kradziezy.
- Gra liczy autobitwe na zywym zestawie bojowym bohatera (CharacterObject.Equipment => HeroObject.BattleEquipment). Ten sam slot decyduje wiec o polu bitwy i o autobitwie.

3. Wiek i wzrost
- ROT nie ma zadnego wieku, rozmiaru ani wzrostu smokow.
- DanyInvasionEvent smokow nie dotyka. Bramka to CampaignStartTime+450, a nasza Fabula przesuwa to na 450 x 4.33 = ok. 1949 dzien opowiesci. Wydarzenie daje Smocza Skale, 1 mln zlota, wplyw, miasta Essos klanom, przejscie rodow Korony i wojne z Zachodem.
- Jedyna dzwignia wielkosci w XML: Mission.BuildAgent ustawia agentowi skale 0.01 x body_length. Kapsula i punkty jezdzca w monsters.xml sie nie skaluja.

4. Co juz robia nasze mody
- CrashScribe Mends.DragonPurge: smoki ma tylko Daenerys i klan gracza. NoDragonGank blokuje odbieranie smoka. OthersSteel: ogien smoka rani Innych.
- Armoury: DragonFeed (smok zjada 2 owce na dobe), BattlefieldLaw.CleanseDragons, CleanseDragonStables.
- Wszystkie filtry poznaja smoka po id "dragon_*" albo po MonsterUsage "dragon"/"dragonfly".

5. Co da sie zrobic (plan ponizej)
Pod jednym kluczem MCM Armoury smok Daenerys "rosnie" wedlug dnia opowiesci:
- jaja (prolog): brak smoka
- smoczeta (0 do 0.40 D): brak smoka w bitwie i autobitwie, Daenerys jedzie na koniu z wlasnego taboru
- mlode (0.40 do 0.85 D): Drogon wraca, ale ma 30% HP i 30% sily ognia, szarzy i autobitwy
- podrosle (0.85 D do D): 65%
- dorosle (od D albo po DanyInvasionEvent): bez zmian, jak w ROT

D = 450 x FabulaTimeScale (u Jeffa ok. 1949). Progi przy 4.33: ok. 779 dzien (Astapor, kronika nr 34) i ok. 1656 (arena Daznaka, nr 78).

Smoka wylaczamy zdejmujac go ze slotu do "gniazda" (stan w zapisie). Kon pochodzi z taboru Daenerys i do niego wraca, wiec ekonomia zostaje zamknieta. Sile skalujemy trzema latkami w polu i w autobitwie. Mniejszy model smoka (body_length) to opcja na koniec, tylko po tescie bitwy.

**Plan:**

TOR M: 1 workflow z 1 recenzja; ok. 3-4 h kodu i 1 autotest z bitwa. Wszystko w Armoury, CrashScribe bez zmian. Galaz od w-toku/m103-sceny, np. w-toku/m104-smoki. Kolejnosc krokow 1 -> 2 -> 3 -> 4; krok 5 opcjonalny. Po kazdym kroku build + autotest, nie wszystko naraz.

KROK 0 - przygotowanie
- Armoury/src/CanonCompanions.cs:840: zmienic `private static int EventState(string)` na `internal`. Nic poza tym.
- StoryDay() (linia 830) jest juz internal.
- Sprawdzic grepem, czy cokolwiek poza ROT wpisuje smoka do BattleEquipment[10]. Znane miejsca:
  - ROTGankBehavior.OnHeroPrisonerReleased (ROT.dll; ROT daje smoka przy pustym slocie)
  - ROT OthersPatches.patch7 (Daenerys przemieniona w Innego dostaje konia Innych)
  - CrashScribe Army175Gear:334 (pomija smoki)
  - DTE/BK: sprawdzic w dekompilacji, czy przepinaja konie lordow ze slotu albo z taboru

KROK 1 - klucz MCM
- Armoury/src/Settings.cs, obok DragonsEatHerds (linia ok. 739): `public bool DaenerysDragonsGrow = true;`
- Komentarz ma byc tylko po angielsku, bo gen_mcm robi z niego podpowiedz. Tresc: "Daenerys's dragons grow up with the story. In the prologue they are still eggs; once hatched they are too small to fight, and Daenerys rides a horse from her own train. From about story day 780 (Astapor) Drogon carries her into battle, young and weak (30% of a dragon's health and fire, in auto-resolved battles too); from about day 1650 (Daznak's Pit) half-grown (65%); full-grown by her invasion of Westeros, as Realm of Thrones has it. Off: Drogon is full-grown from the first day, as in Realm of Thrones."
- Potem `python tools/gen_mcm.py`.
- Sprawdzic, czy klucza nie ma juz w Armoury.json Jeffa.

KROK 2 - kampania: nowy plik Armoury/src/DragonGrowth.cs (internal static class DragonGrowth)

Stale w kodzie, nie w MCM (Jeff nie chce pytan o parametry):
- FracYoung = 0.40f, FracGrown = 0.85f
- GYoung = 0.30f, GGrown = 0.65f
- DanyId = "lord_1_14"
- NestDefault = "dragon_black"

Metody:
- `float InvasionDay()` = 450f x skala. Skala to CrashScribe.Config.FabulaTimeScale odbiciem: AccessTools.Field(AccessTools.TypeByName("CrashScribe.Config"), "FabulaTimeScale"). Gdy jej brak, CampaignTime.DaysInYear/84f. Przycinac 0.25-6 jak Fabula.Scaled.
- `Stage Current()`:
  - Eggs, gdy CanonCompanions.StoryDay() < 0
  - Adult, gdy CanonCompanions.EventState("DanyInvasionEvent") >= 1 albo day >= D
  - Grown, gdy day >= 0.85 D
  - Young, gdy day >= 0.40 D
  - w pozostalych przypadkach Hatchling
- `float Factor()`: Young 0.30, Grown 0.65, inaczej 1 (Eggs i Hatchling nie maja smoka w slocie, wiec skala ich nie dotyczy).
- `Hero Dany()`: Hero.FindFirst po StringId. Ma byc zywa i niebedaca Innym (kultura/klan WW ROT, tak jak sprawdza Mends.WalkerBlood).
- `void Apply(string why)`: jedyne miejsce zmiany slotu. Wolana:
  - przy starcie sesji (po DragonPurge, OnSessionLaunched Armoury)
  - co dobe w ArmouryBehavior, PRZED DragonFeed.Daily (linia ok. 2692), zeby karmienie widzialo biezacy slot
  - z HeroPrisonerReleased dla Daenerys (listener w RegisterEvents ArmouryBehavior; ROT rejestruje sie wczesniej, wiec jego wydanie smoka juz zaszlo)
- Apply, gdy klucz wlaczony i etap Eggs albo Hatchling, a w BattleEquipment[10] jest smok (DragonFeed.IsDragon):
  - zapamietac _nest = id smoka i zdjac go
  - z dany.PartyBelongedTo?.ItemRoster (tylko gdy jest przywodca partii) wziac najlepszego wierzchowca: IsMountable, nie smok, nie juczny; najwyzszy tier, potem wartosc
  - roster -1, kon do slotu, _horse = id konia
  - bez konia slot zostaje pusty (Daenerys pieszo, nic z niczego)
- Apply, gdy klucz wylaczony albo etap >= Young, a _nest niepuste (albo slot bez smoka i nest==null przy etapie >= Young, zas Daenerys w klanie Targaryen):
  - kon ze slotu (jesli to _horse) wraca do rostera partii Daenerys
  - gdy partii brak, zamiana czeka do nastepnej doby
  - smok (_nest albo dragon_black) do slotu, _nest = null
- Daenerys martwa albo Inna: wyczyscic _nest i nic nie robic.
- Stan zapisu: `_nest`, `_horse`, `_lastStage` w SyncData ArmouryBehavior przez SaveText.Sync (wzor paczki 161). Reset w konstruktorze ArmouryBehavior (linia 734), jak inne Reset().
- Przy zmianie etapu (`_lastStage != stage`, poza Eggs i Hatchling) jeden komunikat dla gracza po angielsku przez Log.Player:
  - Young: "Word from Slaver's Bay: Drogon has grown big enough to carry Daenerys into battle - but he is still young."
  - Grown: "Word from Slaver's Bay: Daenerys's dragons are half-grown, bigger than any warhorse."
  - Adult: "Daenerys's dragons are full-grown."
- Potkniecia liczyc (_stumbles), bez globalnego wylacznika.

Linie logu (Armoury.log; kontrola przez grep "Smoki Daenerys (DG)"):
- przy sesji, przy zmianie i co 7 dob: `Smoki Daenerys (DG): etap jaja|smoczeta|mlode|podrosle|dorosle | dzien opowiesci N (prolog T/N) | sila xG | siodlo: <id|pusto> | gniazdo: <id|-> | kon z taboru: <id|-> | nastepny etap od dnia M (D=1948) | autobitwy od ostatniej linii: ciosy smoka n | potkniecia k`
- `Smoki Daenerys (DG): smok dragon_black do gniazda (etap smoczeta, powod: sesja|doba|po niewoli) - w siodle <kon> z taboru (zostalo X) | w siodle pusto - brak konia w taborze`
- `Smoki Daenerys (DG): smok dragon_black wraca pod siodlo (etap mlode, x0.30) - kon <id> do taboru`
- `Smoki Daenerys (DG): klucz wylaczony - smok wraca pod siodlo jak w ROT`

KROK 3 - pole bitwy i autobitwa: nowy plik Armoury/src/DragonGrowth.Field.cs

a) MissionLogic DragonGrowthField
- Dodawac w Armoury SubModule.OnMissionBehaviorInitialize (linia ok. 516) przy kluczu wlaczonym i Campaign.Current != null. Ustawia static `FieldFactor` (Factor() przy starcie misji; 1 = nic nie robi) i `FieldActive = FieldFactor < 1`.
- OnAgentBuild: zapisac agentow-wierzchowcow, ktorych Monster.MonsterUsage to "dragon" albo "dragonfly".
- OnMissionTick co 0.5 s: dla kazdego zywego smoka z RiderAgent bedacym Daenerys (Character jako CharacterObject, HeroObject == Dany()), ktorego Index nie ma w _scaled:
  - target = ROT.ROTSettings.DragonHealth odbiciem (AccessTools.Property("ROT.ROTSettings:DragonHealth")) x FieldFactor
  - HealthLimit = BaseHealthLimit = target; Health = min(Health, target)
  - dodac do _scaled
- Tick pokrywa smoki z zamian TransformDragonForm (nowy agent z HP monstera; ROT tez go nie przestawia) i z pozniejszych spawnow (SpawnMissingLateAIDragonMounts).
- Linia przy pierwszym ustawieniu: `Smoki Daenerys (DG) bitwa: smok <idx> <item id> HP <stare> -> <nowe> (x0.30, etap mlode)`.
- OnEndMissionInternal: `Smoki Daenerys (DG) koniec bitwy: ciosy smoka skalowane N, obrazenia przed X po Y`, potem FieldActive=false.

b) Harmony prefiks na TaleWorlds.MountAndBlade.Agent.RegisterBlow
- Sygnatura `(Agent __instance, ref Blow blow)`, Priority.VeryHigh. Mends.ValyrianWard ma High, a prefiks ROT AgentPatch zmienia tylko flagi i upadek.
- Pierwsza linia: `if (!FieldActive) return;` (goraca sciezka: zero LINQ, zero alokacji).
- Bijacego szukac przez Mission.Current.FindAgentWithIndex(blow.OwnerId). Cios smoka to:
  - bijacy jest smokiem (MonsterUsage dragon/dragonfly), a jego RiderAgent to Daenerys, albo
  - bijacy to Daenerys z MountAgent-smokiem i pustym WeaponRecord (ROT robi FillAsMeleeBlow(null,null,-1,-1))
- Ta sama definicja co CrashScribe OthersSteel.IsDragonFire; w komentarzu "AKTUALIZUJ OBIE RAZEM".
- Skalowanie: blow.InflictedDamage = max(1, round(InflictedDamage x FieldFactor)), BaseMagnitude x FieldFactor. Plus liczniki.
- Ciosow mieczem Daenerys nie ruszamy. Smoka gracza (dragon_red z questu) tez nie.

c) Harmony postfiks na ROT.Models.ROTCombatSimulationModel.SimulateHit
- Przeciazenie z pierwszym parametrem CharacterObject; typ przez AccessTools.TypeByName.
- Gdy klucz wlaczony, __0.HeroObject == Dany(), etap Young albo Grown, a slot 10 to smok: `__result = new ExplainedNumber(__result.ResultNumber x Factor())`, licznik do linii doby.
- Mends.ValyrianWardSim siedzi na DefaultCombatSimulationModel, wiec nie ma podwojnego liczenia.
- Gdy metody brak: linia "DG: SimulateHit ROT nieznaleziony - autobitwa bez skali".

KROK 4 - spojnosc karmienia
- Armoury/src/DragonFeed.cs: dla smoka Daenerys DragonMeat x DragonGrowth.Factor() (mlody 0.6 owcy na dobe, podrosly 1.3).
- Smoczeta nie maja smoka w slocie, wiec samo wychodzi 0.
- W linii "Smoki (24c)" dopisac "x<G>".
- Wylacznik wspolny z DaenerysDragonsGrow.

KROK 5 (opcjonalny, osobny klucz DaenerysDragonsSmallModel=false, dopiero po tescie z bitwa)
- Armoury/ModuleData/arm_dragons_young.xml i XmlNode Items w Armoury/SubModule.xml (wzor arm_items_rare, IncludedGameTypes Campaign).
- Przedmioty:
  - dragon_young_black: Monster.dragon, mesh dragon_body2, body_length 50
  - dragon_young_black2: Monster.dragonfly, body_length 50, speed 70
  - dragon_grown_black i dragon_grown_black2: body_length 80
- Id MUSZA zaczynac sie od "dragon_" (filtry CS i Armoury) i konczyc na "2" w wersji latajacej (ROT TransformDragonForm i SpawnFlyingDragonMountFor dopisuja albo ucinaja "2").
- Apply podmienia przedmiot wedlug etapu.
- Sprawdzic w bitwie: siodlo i kamere jezdzca (adders monstera sie nie skaluja) oraz start ognia (ROT liczy go z AgentScale).

ZAPIS I TEST (autotest wolno, gdy Jeff nie gra; kopia logow Jeffa przed 1. autotestem)
- T1 103-prolog nowa kampania. W logu linia DG "etap jaja", siodlo != smok, gniazdo dragon_black, potkniecia 0.
- T2 przeskok do dnia opowiesci >= 780 (TestMode albo zapis testowy). Linia zmiany na "mlode", smok pod siodlem, kon w taborze (+1).
- T3 bieg-bitwa.ps1 -Battles z Daenerys. Linia "DG bitwa ... HP 10000 -> 3000", "koniec bitwy: ciosy smoka skalowane N>0", brak bledow "DragonGrowth".
- T4 kopia zapisu klasycznego Jeffa: dzien < 779 daje smoka do gniazda. Klucz off daje "smok wraca pod siodlo jak w ROT".
- T5 autobitwa z Daenerys: licznik ciosow smoka w linii DG > 0 przy etapie mlode/podrosle.

Progi:
- linia DG przy sesji = 1
- dokladnie 1 zamiana na zmiane etapu
- 0 wyjatkow DragonGrowth
- czas doby bez zmian (DayProfile)
- wgranie dopiero po udanym tescie, z kopia

Na koniec CHANGELOG z sekcja "Ryzyko / co sprawdzic" i STAN-PRAC.

**Ryzyka:**

- Biezaca kampania Jeffa (klasyczny start z 20.10, dzien opowiesci zapewne < 779). Klucz domyslnie wlaczony zabierze Daenerys Drogona przy pierwszej sesji, a smok wroci dopiero ok. 779 dnia. To zgodne z zamyslem, ale widoczne; patrz pytania.
- Autobitwa przy MCM ROT Jeffa (DragonDamageScaling 10). Przelacznik ROT zamienia KAZDY cios Daenerys na plaskie 10, wiec dorosly smok w autobitwie prawdopodobnie juz dzis oslabia Daenerys wzgledem bitwy bez smoka. Skala x0.30 da 3, czyli mlody smok wypadnie slabiej niz smoczeta bez smoka (wynik zwykly). To nie nasz blad, ale skala nie bedzie rosla rowno.
- ROT oddaje smoka po niewoli (ROTGankBehavior.OnHeroPrisonerReleased przy pustym slocie). Pokrywa to listener HeroPrisonerReleased i Apply co dobe. Jesli kolejnosc listenerow jest inna niz zakladana, przez 1 dobe Daenerys moze miec doroslego smoka.
- TransformDragonForm w ROT tworzy nowego agenta-smoka z HP monstera (5000 + extra_health) i nie kopiuje zdrowia. Tick DragonGrowthField musi lapac nowe indeksy (_scaled po Index), inaczej mlody smok po wyladowaniu albo starcie mialby pelne HP.
- Agent.RegisterBlow to goraca sciezka, prefiks dostaja tam juz ROT i Mends.ValyrianWard. Prefiks musi wychodzic po jednym bool, gdy FieldActive=false, bez LINQ i alokacji. Blad w nim uderzy w kazdy cios kazdej bitwy.
- Definicja ciosu smoka jest skopiowana z CrashScribe OthersSteel.IsDragonFire (pusty WeaponRecord przy jezdzcu-smoku). Gdy ROT zmieni sposob tworzenia ciosu ognia, skala przestanie dzialac po cichu; w logu zostanie licznik 0.
- Kon z taboru: gdy Daenerys nie ma konia w taborze albo nie prowadzi partii, jedzie pieszo (swiadomie, nic z niczego). Zwrot konia czeka na partie, wiec stan _horse musi przetrwac zapis i wczytanie.
- Klucz wylaczony w trakcie gry musi natychmiast oddac smoka z gniazda. Inaczej Daenerys zostaje bez smoka na zawsze. Tak samo przy Daenerys przemienionej w Innego: wyczyscic gniazdo i nie oddawac.
- D zalezy od CrashScribe.Config.FabulaTimeScale czytanego odbiciem. Bez CrashScribe zostaje zapas DaysInYear/84. Zmiana skali Fabuli przesuwa progi (zamierzone, ta sama os co DanyInvasionEvent).
- Krok 5 (maly model przez body_length): kapsula, siedzisko i kamera jezdzca sa w monsters.xml i nie skaluja sie z agentem. Mozliwy jezdziec wiszacy nad smokiem albo zla kolizja. Tylko po tescie z bitwa, osobny klucz domyslnie wylaczony.
- DragonFeed po kroku 4 zmienia zuzycie owiec w ksiedze towarow (rama FDragon). To drobne, ale wchodzi w test gospodarki 120 dob; porownac linie 'Smoki (24c)' przed i po.

**Pytania:**

- Biezaca kampania Jeffa: wlaczyc od razu (Drogon znika do ok. 779 dnia opowiesci), czy tylko w nowych kampaniach? To da sie zrobic znacznikiem w zapisie przy OnNewGameCreated.
- Etap 'mlode' od ok. 780 dnia (Astapor) to kompromis: ROT umie pokazac smoka w bitwie tylko jako wierzchowca, a w kanonie Daenerys pierwszy raz dosiada Drogona dopiero na arenie Daznaka (ok. 1650). Druga mozliwosc: brak smoka az do 0.85 D, potem od razu 65% i doroslosc przy inwazji.
- Czy DragonDamageScaling = 10 i DragonHealth = 10000 w MCM ROT Jeffa (RealmOfThrones_v1.json z 24.08) to swiadomy wybor? Przy 10 przelacznik autobitwy ROT robi z Daenerys na smoku slabsza bohaterke niz bez smoka (patrz ryzyko).
- Smok gracza z questu Valyrian Thief (dragon_red) jest od razu dorosly. Zostawic jak jest (tak zaklada plan), czy tez ma rosnac od chwili zdobycia?
- Krok 5 (mniejszy model mlodego smoka przez body_length 50/80): robic po tescie bitwy, czy zostac przy samej sile i HP?

## kronika:A (badacz)

**Podsumowanie:**

KRONIKA A to architektura silnika dla 117 wydarzen (204 okna wedlug KRONIKA-WESTEROS-V2) zbudowana na StoryWindow/ArmStoryEvent z m103-sceny. Najwazniejsze decyzje:
(1) Wydarzenia sa DANYMI. Pliki TSV per paczka w Armoury/ModuleData/Kronika (kNN_okna.tsv, kNN_wybory.tsv, opcjonalnie kNN_bandy.tsv) plus wspolny tematy.tsv. Kod jest tylko ogolny: harmonogram, warunki, okno i wybor, biblioteka akcji, wyzwalacze powracajace, zapis. Wyjatkowe skutki ida przez nazwany uchwyt "kod:<nazwa>", najwyzej ok. 10 sztuk na cala Kronike.
(2) Harmonogram liczy dzien opowiesci z CanonCompanions.StoryDay(). Dni z dokumentu sa juz w skali x4.33; silnik przelicza je przez Config.FabulaTimeScale/4.33 z CrashScribe, odczytane odbiciem. Do tego kotwice "po" (z +N dni) i "przed" (wydarzenie ROT albo okno Kroniki). Warunki swiata uzywaja jezyka wyzwalaczy CanonCompanions (ev/done/dead/war/own/rotday/prolday), rozszerzonego o: zyje, rod, wladca, jeniec, pokoj, gdzie jest gracz, czyim jest wasalem, relacja gracza, znaczniki Kroniki, wybory i wykonane zmiany stron (mv:).
Kolejnosc: najnizsze "od", potem id. Okna zalegle ida po kolei, najwyzej 2 na dobe, z odstepem 6 h. Okno spoznione ponad 30 dni albo po swojej kotwicy "przed" odpada z linia logu albo zamienia sie w krotka wiesc. Gdy okno musi wyprzedzic wydarzenie ROT, CrashScribe moze to wydarzenie przytrzymac, najwyzej 5 dni. Zapis sprzed paczki: okna, ktorych termin minal, sa pomijane bez lawiny.
(3) Wybory: okno ROT ma tylko Done i Cancel. Dodatkowo Esc w EventScreen.OnFrameTick zawsze wola ExecuteCancel, a Enter ExecuteDone - sprawdzone w dekompilacji. Dlatego prawdziwy wybor nigdy nie lezy na przyciskach okna ROT. Okno opowiesci ma jeden przycisk, a po zamknieciu, juz nad mapa, pokazuje sie MBInformationManager.ShowMultiSelectionInquiry: 2-4 pozycje, min=max=1, isExitShown=false, pauza gry. Sprawdzone w 1.4.8:
- przy max=1 lista dziala jak radio (MultiSelectionQueryPopUpVM.OnInquiryElementSelected),
- Esc nie dziala, bo IsButtonCancelShown = IsExitShown (GauntletQueryManager.OnEarlyTick),
- pozycja moze byc wyszarzona z podpowiedzia (InquiryElement isEnabled + hint),
- autotest (AutotestUi.HandleMulti) wybiera ostatnia czynna pozycje, wiec ostatnia pozycja jest zawsze "bierna" (wersja z ksiazki).
(4) Skutki to biblioteka akcji w 3 klasach: T (tekst, wiesc, plotka, znacznik), R (relacje, slawa, wplywy), S (swiat: postacie, bandy, zadania, zloto, ludzie, osady). Zmiany stron postaci kanonu wykonuje TYLKO CanonCompanions: akcja move:<id> uruchamia wiersz #move teraz, dzieki czemu ta sama zmiana nie wykona sie dwa razy. Kazde zloto, kazda sztuka i kazdy czlowiek maja platnika albo zrodlo (zamknieta ekonomia). Bandy biora ludzi z puli wyrzutkow przez OutlawLaw.RaidDraw, tak jak FreeFolkRaids.
(5) Zapis to jeden napis "arm_kronika" przez SaveText.Sync, ok. 6-20 KB. Klucze MCM (piec, grupa "Chronicle of Westeros"):
- ChronicleOfWesteros - wlacza Kronike,
- ChronicleWorldChanges - wylacza skutki klasy S,
- ChronicleFullVersions - pelne wersje 16 duzych wydarzen, domyslnie NIE (wersje lekkie),
- ChroniclePlayerChoices - wybory gracza,
- ChronicleHoldsRotEvents - przytrzymywanie wydarzen ROT.
Linie logu zaczynaja sie od "Kronika (KW): ". Grupa progow to m104kronika z prefiksem KW-0..KW-15 (KR- zajal juz m100kryjowki).
(6) Obrazki bez promptow i bez grafik Jeffa: kazde okno ma kolumne "temat", a tematy.tsv podaje pule 40 sprite'ow ROT z ui_event_category (48 sprawdzonych, bez czesci prefabow Blackwater). Do puli wchodza tez pliki, jesli sa: Images/Prolog/prolog_NN oraz Images/Kronika/kronika_<temat>_<n> ("jedna albo kilka" od Jeffa). Wybor jest staly dla danego okna, bez powtorki tego samego obrazka dwa razy z rzedu.
(7) Lektor dziala z gotowych nagran. W galezi m103-lektor plik arm_lektor.tsv ma juz kolumne klucza; Lektor ma szukac najpierw po kluczu okna Kroniki (ArmStoryEvent.Key = kr_NNN_w), potem po tytule. Okno bez nagrania gra w ciszy. Nagrywamy najpierw okno 1 kazdego wydarzenia (ok. 117 x 650 = 76 tys. znakow, calosc ok. 130 tys.). Warianty z podstawianymi imionami zostaja bez nagrania.
Podzial na galezie: K0 silnik (z testowa paczka k00 tylko w autotescie), potem 10 paczek wzdluz osi, po 10-14 wydarzen: K1 1-12, K2 13-22, K3 23-32, K4 33-44, K5 45-54, K6 55-66, K7 67-74+81-83, K8 75-77+84-93, K9 78-80+94-103, K10 104-117. Szesc scen "juz w grze" z dopiskiem "(zrobie z E4)" wchodzi jako okna Kroniki podpiete pod zmiany stron: Tyrion Reka w K1, Theon na Pyke w K2, nowy lad w K4, miecz Brienne w K5, Sam do Starego Miasta w K6, Tyrion w niewoli w K8.

**Plan:**

A. ZALOZENIA I KOLEJNOSC
- Galaz K0 (w-toku/kronika-00-silnik) wychodzi z drzewa, w ktorym jest juz scalone m103-sceny (StoryWindow, StoryPictures, Prologue.Scenes) i m103-lektor (Lektor.cs, arm_lektor.tsv). Scalanie przez tools/scalanie/scal.sh, potem kontrola sprawdz_narzedzie.py.
- E5 (Jeff: "robic teraz") buduje pomocnicze akcje jako WSPOLNE, nie prologowe. Plik Armoury/src/StoryActions.cs, w nim:
  - banda z puli,
  - turniej w miescie,
  - jeniec fabularny bez ucieczek i okupow (rejestr StoryCaptives, czytany przez RealisticCaptivity odbiciem i przez prefiks ucieczek gry - sprawdzic w dekompilacji PrisonerEscapeCampaignBehavior),
  - przejazd orszaku.
  Kronika uzywa tych samych metod (jedna zasada na zjawisko).
- W arm_canon_moves.tsv wiersz m01 (Jaime) ma dostac arg town_V2 (Riverrun) zamiast clan_battania_1. Decyzja Jeffa; Captor(arg) juz obsluguje id osady. Kronika nr 16 wola move:m01.

B. DANE (ModuleData/Kronika, UTF-8, TAB, linie '#' to komentarz; linia z bajtem \0 jest pomijana i liczona, jak w Lektor.LoadIndex)

1) kNN_okna.tsv - jeden wiersz na okno albo wariant okna. Kolumny:
- id: NNN.w, staly na zawsze; nigdy nie przenumerowywac, usuniety wiersz zostaje jako nagrobek "#usuniete".
- war: litera wariantu, puste = podstawowy; pierwszy pasujacy wariant wygrywa.
- nr, od, do: dni opowiesci w skali x4.33, jak w dokumencie Kroniki.
- po: term+N, np. kr:002.1+10 albo ev:RenlyDeathEvent+5.
- przed: kotwica, np. ev:RiverlandsDeclareWarEvent albo kr:032.1.
- warunki: termy rozdzielone ','; '|' to LUB w termie; '!' to NIE.
- gracz: termy, ktore daja pelne okno; reszta graczy dostaje forme z kolumny "inni".
- forma: okno|wiesc|log.
- inni: okno|wiesc|nic.
- wersja: lekka|pelna|obie, filtrowane kluczem ChronicleFullVersions.
- temat, lektor (1/0).
- tytul, gora, dol: TextObject z id {=ARM_KR_NNN_w[war]_T/U/L}. Teksty EN, wlasnymi slowami; pojemnosc z linie.py: gora do 4 linii, dol do 3.
- zmienne: np. RULER=@wladca:battania.
- przycisk: puste = {=ROTasSnYXW5}Continue..., przy wyborze "What will you do?".
- wybor: tekst pytania listy; puste = bez wyboru.
- skutki: akcje rozdzielone " ; ".
- wyzwalacz: tylko dla wydarzen powracajacych, patrz F.

2) kNN_wybory.tsv. Kolumny: id okna, opcja (a-d), etykieta (do 60 znakow), podpowiedz (skutki slowami gracza), dostepna (termy; niespelnione = pozycja wyszarzona z podpowiedzia, dlaczego), skutki, bierna (1 = wersja z ksiazki).
- Zawsze 2-4 opcje. Ostatnia opcja jest bierna.
- Gdy gracz nie spelnia kolumny "gracz" albo klucz ChroniclePlayerChoices = NIE, wykonuje sie opcja bierna i nie ma pytania.

3) kNN_bandy.tsv, od K1. Kolumny: id, klan (bandycki albo mniejszy ROT), osada okolicy, ludzi min/max, przezwisko herszta (TextObject), zachowanie (patrol/rabunek), dni zycia, list gonczy 1/0, po_rozbiciu (akcje po rozbiciu przez gracza).

4) tematy.tsv: temat, sprite'y ROT po przecinku. Propozycja:
- polnoc: robb, robb_stark, RobbMarching(1-3), rickard, ned_stark
- mur: wallfall, fist, jeor, jon, jonsnow, jon_stand, eastwatch, freefolk1-3, WildlingsFail
- inni: nightking, fist, wallfall
- korona: kingslanding, joffrey, blackwater
- dorzecze: riverlands, harrenhal, roose_bolton
- bolton: ramsay, ramsay_death, roose_bolton
- burza: Stannis, Davos, melisandre, stannis-slain, renly-slain
- essos: dany_invasion, traveling_merchant, Ship1, Ship2, messenger-1
- wyspy: Ship1, Ship2
- aegon: aegon
- jon: jon_death, jon_rez, jonsnowres
- ogolny: event-bg, messenger-1, messenger-2, messenger-img
Bez: BlackwaterWarning, StannisIronThrone, StannisSetsSail, testevent.

5) Wczytanie danych raz przy OnSessionLaunched, z kontrola daty pliku. Zly plik albo zly wiersz = linia logu z powodem; zly plik wylacza tylko swoja paczke.

6) tools/kronika_check.py (python -I; uruchamiac przed buildem i w CHANGELOG). Sprawdza:
- unikalne id, znane termy i akcje,
- czy id bohaterow, rodow, krolestw i osad istnieja w XML ROT (ROT_heroes.xml, spclans.xml, ROT-Map settlements.xml),
- pojemnosc tekstu (reguly linie.py), 2-4 opcje i ostatnia bierna,
- czy kazda akcja gold/ware/garrison ma platnika albo zrodlo,
- czy akcje own/ruler/alliance wystepuja tylko w wersji pelnej,
- czy okna z lektor=1 nie maja {ZMIENNYCH},
- czy akcje postaci z wierszem #move w arm_canon_moves.tsv ida przez move:,
- czy akcje die/hide nie dotycza postaci "chronionych" (lista w kronika_check: postac -> wydarzenie ROT, ktore jej potrzebuje, np. Ramsay do done:BastardsEvent, Mance do done:WallSiegeEvent, Daenerys do ev:DanyInvasionEvent).

C. HARMONOGRAM (Chronicle.cs)
- Dzien: CanonCompanions.StoryDay() (w prologu ujemny), razy skala k = Config.FabulaTimeScale/4.33. Odczyt odbiciem z CrashScribe.Config, jak StoryClock; brak = 1.0.
- DailyTick (ChronicleBehavior):
  - dla kazdego nierozstrzygnietego okna liczy termy; pierwszy dzien spelnienia trafia do pamieci, jak CanonCompanions._first;
  - lista naleznych: dzien >= od, "po" spelnione, warunki spelnione;
  - POMINIETE z powodem: term "nigdy" (martwy, a wymagany zywy; brak rodu), "przed" juz zaszlo (ev:X >= 1), spoznienie dzien > do + 30 (jesli wiersz ma tekst wiesci - zamiast okna wiesc);
  - linia doby.
- AppTick (SubModuleMain.OnApplicationTick, 4/s, obok Prologue.ScenesAppTick):
  - nic, gdy StoryWindow.IsOpen albo w prologu czeka scena (Prologue.BeforeStory() blokuje okna osi; wydarzenia powracajace z flaga "prolog" przechodza);
  - bramki: Prologue.SceneGate(c) zmienione z private na internal, bez zmiany logiki - ta sama lista (mapa gotowa, bez rozmowy, bitwy, zapytania, okna BK, okna ROT);
  - 2 s spokoju z rzedu; najwyzej 2 okna Kroniki na dobe i 6 h gry od poprzedniego okna Kroniki albo od zamkniecia okna ROT (przejscie StoryPictures.EventWindowOnStack true->false);
  - otwiera najnizsze "od", potem id.
- Trzymanie ROT (klucz ChronicleHoldsRotEvents):
  - Armoury: internal static bool Chronicle.HoldsRotEvent(string type) - true, gdy okno z przed:ev:type jest nalezne i nierozstrzygniete, najwyzej 5 dni od pierwszego trzymania (stan w zapisie);
  - CrashScribe Fabula.GatePrefix: po bramce daty pytanie odbiciem (wzor StoryClock.PrologueAlliance) i __result=false + linia "FABULA: <typ> trzymane przez Kronike (okno NNN.w)".
- Zapis sprzed Kroniki (brak klucza arm_kronika) albo nowa paczka w trwajacej kampanii: lista "pk=" w zapisie; okna tej paczki z do < dzien sa pomijane z powodem "zapis sprzed paczki kNN", bez serii zaleglych okien (wzor SkipPassedLegacy). Kampania Jeffa w prologu: nic nie odpada.
- Okno, ktore 3 razy sie nie otworzylo, odpada (wzor OpenScene).

D. OKNO I WYBOR (Chronicle.Choice.cs)
- Okno: StoryWindow.Spec {Key="kr_NNN_w", Title, Upper, Lower, Header z obrazka, Closed=OnClosed}; StoryWindow.Show bez zmian (zapas ShowInquiry, gdy brak ROT).
- ArmStoryEvent: dodac "internal string Key { get { return _spec.Key; } }" (dla Lektora).
- Przy oknie z wyborem OnClosed nie pcha okien z wnetrza Handle* (zasada StoryWindow). Ustawia stan "a" (czeka na wybor; jest w zapisie), a nastepny AppTick po 0,5 s, przy bramkach bez IsAnyInquiryActive, woluje:
  MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(tytul, pytanie, elementy, false, 1, 1, "Confirm", "", AffirmativeAction, null), true)
- Element: new InquiryElement(litera, etykieta, null, dostepna, podpowiedz). Zero dostepnych = opcja bierna bez pytania.
- Po wyborze: linia wyboru, stan "ch=x", skutki.
- Wczytanie zapisu ze stanem "a" pokazuje liste ponownie.
- Autotest: wybiera ostatnia czynna pozycje (bierna). Plik CrashScribe/armoury-kronika-wybor.flag (a|b|c|d|rot) wymusza opcje, do testu galezi wyborow.
- Nigdy nie wolno wyboru na Done/Cancel okna ROT (Esc = Cancel, Enter = Done).

E. BIBLIOTEKA AKCJI (Chronicle.Actions.cs, skladnia "nazwa:arg:arg")
- Wynik akcji: Done/Skip/Wait jak w CanonCompanions; Wait jest ponawiane co dobe do 30 dni, potem POMINIETA; akcje czekajace sa w zapisie.
- Klucz ChronicleWorldChanges = NIE: klasa S pominieta z linia; T i R dzialaja.
- Tokeny celu: id bohatera/rodu/krolestwa/osady, @gracz, @wladca:<krolestwo>, @glowa:<rod>, @pan:<osada>.

T (tekst, wiesc, znacznik):
- wiesc:<TextId> - Log.Player / AddQuickInformation.
- plotka:<TextId>:<osada|krolestwo>:<dni> - nowy wpis ksiegi wiesci Rumours: Rumours.AddStoryNews, mala metoda w Rumours.cs. Bez nagrywania na zywo: flaga "noLive" dla Lektor.Live, zeby nie palic znakow ElevenLabs.
- flag:<nazwa>=<n> i flag+:<nazwa>:<n> - ukryte znaczniki, np. prawda_o_jonie, gniew_krolowej.
- kod:<nazwa> - nazwany uchwyt C# dla nietypowych skutkow, np. 076 zaraza, 115 Przystan, 117 Wielka Rada.

R (relacje, slawa, wplywy):
- rel:<a>:<b>:<d> - ChangeRelationAction.ApplyRelationChangeBetweenHeroes, bez powiadomienia.
- relp:<a>:<d> - relacja gracza, ApplyPlayerRelation.
- relk:<krolestwo>:<krolestwo>:<d> - wladcy i glowy rodow.
- renown:<rod>:<n> - GainRenownAction.Apply.
- influence:<rod>:<n> - ChangeClanInfluenceAction.
- honor:@gracz:<n> - sprawdzic w dekompilacji droge cechy, np. TraitLevelingHelper.

S (swiat):
- move:<idWiersza> - nowe internal CanonCompanions.RunMoveNow(id, zrodlo): Execute dla wiersza #move teraz; stan done w CanonCompanions; wiersz juz zrobiony = "juz zrobione". Wiersz zostaje z wlasnym wyzwalaczem jako zapas, gdy Kronika wylaczona.
- clan/court/wander/prisoner/free/die/remove/wed:<postac>:<arg> - Execute z CanonCompanions wydzielone do internal Res ApplyAction(Hero h, string action, string arg, string zrodlo). Te same straze: postac gracza, glowa rodu, bitwa, niewola. MoveRow.Execute wola to samo.
- hide:<postac>:<term konca> / unhide:<postac>:
  - nie dla glowy rodu, wladcy, gubernatora ani postaci gracza (wtedy POMINIETA albo wersja "na dworze" - SetCourt + teleport do siedziby);
  - najpierw CanonCompanions.LeaveParty (StartDisband - ludzie nie przepadaja; DisableHeroAction.Apply sam by wolal DestroyPartyAction), potem DisableHeroAction.Apply;
  - powrot: ChangeState(Active) + TeleportHeroAction do siedziby; rejestr w zapisie.
- wound:<postac>:<dni>, skill:<postac>:<skill>:<-polowa> - do sprawdzenia w dekompilacji (HeroDeveloper).
- item:<postac>:<itemId>:<skad> - przedmiot unikatowy ROT, zabrany posiadaczowi albo ze skarbca; nigdy z niczego.
- gold:<platnik>:<odbiorca>:<kwota|wzor>:
  - GiveGoldAction.ApplyBetweenCharacters / ApplyForSettlementToCharacter / skarbiec korony (KingdomTreasury);
  - brak srodkow = niedoplata z linia; platnik null zakazany (walidator i KW-6).
- ware:<osada>:<osada>:<item>:<ilosc> - towar z rynku do rynku.
- security / loyalty:<osada>:<d>.
- prosperity / pop:<osada>:<d> - tylko przez mechanizmy Armoury: ksiega ludzi PeopleBook z przyczyna "kronika", Devastation. Sprawdzic, czy DailyLife.Prosperity nie nadpisuje Town.Prosperity.
- garrison:<osada>:<oddzial>:<n>:<skad> - z istniejacej partii albo z puli ochotnikow regionu.
- band:<idSzablonu> - StoryActions/FreeFolkRaids:
  - OutlawLaw.RaidDraw(region, ...) + BanditPartyComponent.CreateLooterParty("armoury_kr_<id>", klan, ...);
  - sufit 12 band Kroniki;
  - list gonczy z NoticeBoard.Watch obejmuje je sam;
  - po_rozbiciu w MapEventEnded;
  - sprawdzic, czy CreateLooterParty przyjmuje klan niebandycki (Bractwo, Stone Crows, Synowie Harpii).
- tourney:<miasto> - TournamentModel.CreateTournament + TournamentManager.AddTournament, gdy nie ma turnieju (jak GrandTourney.HostTournament, bez oplat gracza). Dalej GrandTourney.
- muster:<krolestwo>:<osada>:<dni> - miekki cel: jednorazowo SetMoveGoToSettlement partiom lordow bez armii, bitwy i zadania; log. Moze zostac zignorowane przez AI BK.
- task:<id> - od K2, Chronicle.Tasks.cs:
  - wlasny rejestr bez QuestBase w zapisie (jak decyzja D6 NoticeBoard);
  - rodzaje: deliver (opcja w menu miasta), defeat (banda Kroniki), prisoners (jency do Czarnego Zamku);
  - termin, nagroda z platnikiem, wpis na NoticeBoard (Kind.Task) i flaga VisualTrackerManager.
- own / ruler / leader / alliance / truce - tylko wersja pelna:
  - ChangeOwnerOfSettlementAction, ChangeRulingClanAction, ChangeClanLeaderAction;
  - sojusz droga Prologue.Peace; para musi byc dopisana do listy CanonAlliances, inaczej CanonAlliancesBreakOnLoad ja zerwie.

F. WYDARZENIA POWRACAJACE I REAKTYWNE (kolumna wyzwalacz; instancja "NNN#klucz" w zapisie)
- 013 season: zmiana WesterosClimate.Now().
- 015 siegehunger:3: oblezona twierdza 3 doby bez jedzenia (Famine/BesiegedRelief); klucz osada+start oblezenia; takze w prologu.
- 024 ironraid: VillageLooted przez Wyspy; klucz port + 30 dni.
- 039 oldage: HeroKilled DiedOfOldAge glowy waznego rodu; klucz bohater.
- 045 brokenmen: ponad 30% ludzi regionu poza domem (PeopleBook) i rabunek w 30 dniach; klucz region + 90 dni.
- 061 others:1/5/15/25: liczba osad kultury whitewalker.
- 066/088 wizje: okresowe okna z prawdziwego stanu mapy.
- 109/110/112/115 captured/besieged:<osada>:<strona>: OnSettlementOwnerChanged / SiegeStarted.
- Teksty z {ZMIENNYMI}, bez lektora. Najwyzej 1 instancja na dobe. Zbior obsluzonych kluczy ma sufit 400 linii, najstarsze sa przycinane.

G. ZAPIS: ChronicleBehavior.SyncData -> SaveText.Sync(ds, "arm_kronika", ref v), format linii z TAB:
- "v 1"
- "pk k01,k02..."
- "w <id> <s|k|a|p> <dzien> [opcja] [kod powodu]"
- "f <term> <dzien>"
- "g <flaga> <n>"
- "a <id> <nr akcji> <od dnia>"
- "b <partia> <szablon> <dzien>"
- "t <zadanie> ..."
- "h <postac> <do> <osada>"
- "r <rodzaj> <klucz> <dzien>"
- "lp <ostatni obrazek>"
- "hold <okno> <od dnia>"
Oszacowanie 6-20 KB (SaveText i tak tnie powyzej 8000 znakow). Linia rozmiaru przy zapisie. Reset przy OnGameStart, jak ResetCampaign.

H. OBRAZKI (Chronicle.Pictures.cs)
- Pula tematu = pliki Images/Kronika/kronika_<temat>_<n>.png|jpg (StoryPictures.CountFiles/Acquire, katalog DirChronicle juz jest) + Images/Prolog/prolog_NN wskazane w tematy.tsv + sprite'y ROT.
- Wybor: pula[(nr*7+okno) % n]; gdy wypada ten sam co ostatni, bierzemy nastepny.
- Plik przez StoryPictures.Acquire (walidacja bajtow); przy zamknieciu StoryPictures.Drop, jak OnSceneClosed.
- Brak pliku = sprite ROT, bez promptow i bez pracy Jeffa.

I. LEKTOR
- W Lektor.cs (postfiks OnActivate): gdy _eventPopupState.Event is ArmStoryEvent e, szukac _byKey[e.Key] (indeks po kolumnie 1 arm_lektor.tsv), potem po tytule. Brak nagrania = cisza.
- Klucze nagran: kr_NNN_w; wariant ma osobny klucz i domyslnie brak nagrania.
- Nagrania offline po zamknieciu tekstow paczki: okno 1 kazdego wydarzenia (lektor=1), w granicach znakow planu ponad LektorGossipReserveChars. Narzedzie raportuje "czeka na nagranie".
- Tekst nagrania musi byc rowny tekstowi okna (walidator: md5 tekstu w tsv).

J. KLUCZE MCM (Settings.cs, nowa grupa "// --- Chronicle of Westeros ---"; potem python tools/gen_mcm.py; czytane na zywo; opisy EN):
- ChronicleOfWesteros = true (117 events from the books and the show in story windows when their day comes and the world allows; Off = no Chronicle windows, nothing undone, saves load)
- ChronicleWorldChanges = true (deaths, side changes, bands, tasks, gold; Off = windows, relations and renown only)
- ChronicleFullVersions = false (16 big events in full: castle owners, rulers, alliances)
- ChroniclePlayerChoices = true (Off = the book outcome without asking)
- ChronicleHoldsRotEvents = true (a Realm of Thrones story event waits up to 5 days for a Chronicle window that must come before it)
Stale w kodzie, bez pytania Jeffa: MaxLate 30, PerDay 2, GapHours 6, HoldDays 5, WaitActionDays 30, StableSec 2.0, MaxBands 12, ChoiceDelay 0.5 s.

K. LINIE LOGU (Armoury.log, prefiks "Kronika (KW): ")
- start sesji: klucze TAK/NIE, paczki z liczba okien i wyborow, zle wiersze (pierwszy z powodem), tematy i liczba sprite'ow/plikow, skala osi, lektor okien z nagraniem/razem, stan zapisu (pokazane, pominiete, czeka na wybor, akcje czekaja), dzien opowiesci, nastepne okno, potkniecia.
- doba: dzien opowiesci, nalezne z powodem czekania, dzis pokazane/wiesci/pominiete, akcje ok/pominiete/czeka, bandy Kroniki, trzymane wydarzenia ROT, rozmiar zapisu, potkniecia.
- "okno NNN.w 'tytul' pokazane | wariant x | dzien d (od, do) | obrazek temat->sprite (ROT|plik) | lektor klucz TAK/NIE | okno ROT|zapas | gracz: osada, menu | ms"
- "okno NNN.w zamkniete po s (przycisk|Esc|bez przycisku)"
- "wybor NNN.w = x 'etykieta' (z n; dostepne ...; autotest TAK/NIE)"
- "akcja NNN.w #i <akcja> -> OK (przed -> po) | POMINIETA (powod) | CZEKA (powod, od dnia)"
- "zloto NNN.w: platnik -> odbiorca kwota (zaplacone k)"
- "POMINIETE okno NNN.w - warunki swiata: term | spoznione o n dni | kotwica minela: X | zapis sprzed paczki kNN"
- "wiesc NNN.w (gracz poza ...)"
- "banda NNN 'nazwa' (armoury_kr_..) | ludzi n (pula a, domy b) | list gonczy"
- "wydarzenie ROT X trzymane dla NNN.w (d/5 dni)"
- "ZLY PLIK / ZLY WIERSZ ..."
- "zapis arm_kronika n KB"
Bledy: Log.Error tylko przy potknieciach 1-5 (licznik), bez globalnego wylacznika.

L. GRUPA PROGOW m104kronika (tools/sprawdz_logi.py, prefiks KW-)
- KW-0 start: klucz TAK, paczki maja okna, zle wiersze 0.
- KW-1 ERROR i potkniecia Kroniki 0.
- KW-2 kazde nalezne okno rozstrzygniete (okno/wiesc/POMINIETE z powodem) w ciagu 3 dni od nalezenia, poza czekajacymi z powodem.
- KW-3 zadne okno przed swoim "od" ani po swoim "przed" (porownanie z liniami CrashScribe "FABULA: ... WYSTARTOWALO").
- KW-4 najwyzej 2 okna na dobe i odstep >= 6 h.
- KW-5 kazda akcja ma linie wyniku; akcji nieznanych 0.
- KW-6 kazda linia zlota z platnikiem (0 "z niczego").
- KW-7 po wczytaniu zadne okno dwa razy.
- KW-8 okna ROT, zapas 0.
- KW-9 kazde okno z wyborem ma linie wyboru; para z AUTOTEST "OKNO wybor z listy".
- KW-10 "ZWOLNIENIE PRZY OKNIE" 0.
- KW-11 lektor INFO.
- KW-12 zapis < 24 KB.
- KW-13 move: kazdy wiersz #mNN wykonany najwyzej raz (linie "Postacie kanonu (m101): zmiana strony #").
- KW-14 trzymanie ROT <= 5 dni, zadne wydarzenie ROT nie pominiete przez Kronike.
- KW-15 bandy Kroniki <= 12 i ludzie z puli.
Log bez Kroniki: KW-0 INFO.

M. AUTOTEST
- Kampania klasyczna (dzien opowiesci 0 od razu - oszczedza 188 dob) do dnia ok. 130 dla K1 i odpowiednio dalej dla kolejnych paczek.
- Jeden bieg z armoury-prolog.flag przez dzien 188+ (przejscie prolog -> Kronika).
- Biegi z armoury-kronika-wybor.flag = a, b i rot (galezie wyborow).
- K0 testuje silnik na danych k00_test.tsv, wczytywanych tylko przy armoury-kronika-test.flag: okno zwykle, wariant zalezny od miejsca gracza, wybor 3 opcji z wyszarzona, wiesc, okno z przed: na wydarzenie ROT.
- Wgranie tylko po udanym autotescie z kopia (zasada Jeffa); skrypt wgrania ma kopiowac katalog ModuleData/Kronika.

N. PLIKI KODU
Nowe:
- Armoury/src/Chronicle.cs (stan, harmonogram, AppTick, ChronicleBehavior)
- Chronicle.Data.cs
- Chronicle.Terms.cs (wola wydzielone internal CanonCompanions.TermBase/BaseTrue + nowe termy: alive, clanof, leads, head, prisoner, peace, kexists, p:region, p:in, p:near, p:vassal, p:army, p:rel, kr, krc, flag, mv, season, others)
- Chronicle.Choice.cs
- Chronicle.Actions.cs
- Chronicle.Pictures.cs
- od K1: Chronicle.Bands.cs
- od K2: Chronicle.Tasks.cs i Chronicle.Recurring.cs
Zmiany w istniejacych:
- CanonCompanions: ApplyAction, RunMoveNow, TermBase internal.
- Prologue.Scenes: SceneGate internal.
- StoryWindow/ArmStoryEvent: Key.
- Lektor.cs: lookup po kluczu.
- Rumours.cs: AddStoryNews.
- CrashScribe Fabula.GatePrefix: HoldsRotEvent odbiciem.
- SubModuleMain: AppTick, OnGameEnd, Reset.
- Settings.cs + gen_mcm.
- sprawdz_logi.py.
- CHANGELOG (Ryzyko / co sprawdzic) po kazdej galezi.

O. GALEZIE (po kolei; jedna naraz; kazda: dane + akcje, ktorych pierwszy raz potrzebuje + teksty + obrazki z tematow + nagrania okien 1 + autotest + progi)
- K0 kronika-00-silnik: A-N; akcje T i R oraz move/gold/flag/wiesc/plotka.
- K1 (dni 10-125): 1-12 + scena Tyrion Reka. Nowe akcje: hide, item, band (Lorch), security/loyalty, muster, tourney (opcja nr 3). PILNE: kampania Jeffa dojdzie do dnia 10 ok. 198 doby prologu.
- K2 (100-500): 13-22 + Theon na Pyke. Nowe: task, wydarzenia powracajace (013, 015), relk.
- K3 (433-815): 23-32. Duze: 23, 26 w wersji lekkiej; 024 powracajace; wound.
- K4 (770-967): 33-44 + nowy lad. Nowe: skill (Jaime), 039 powracajace.
- K5 (967-1300): 45-54 + miecz Brienne. 045 powracajace.
- K6 (1215-1460): 55-66 + Sam do Starego Miasta. Duze: 56, 59, 63; 061 powracajace; ware.
- K7 (1300-1510): 67-74, 81, 82, 83. Duze: 81.
- K8 (1420-1650): 75, 76, 77, 84-93 + Tyrion w niewoli. Duze: 86; kod:zaraza.
- K9 (1650-2100): 78, 79, 80, 94-103. Duze: 94, 95, 100, 103.
- K10 (2000-koniec): 104-117. Duze: 107, 108, 115, 116, 117; kod:dzwony, kod:wielka_rada.
Wydarzenie wielookienne robimy w calosci w paczce swojego pierwszego okna. Wersje pelne dopisywane tylko za kluczem ChronicleFullVersions.

**Ryzyka:**

- Esc w ROT EventScreen zawsze wola ExecuteCancel, a Enter ExecuteDone (dekompilacja EventScreen.OnFrameTick). Wybor na przyciskach okna ROT dalby przypadkowe decyzje, dlatego wybory ida tylko przez liste MultiSelection bez wyjscia. Lista przy wczytaniu w stanie 'czeka na wybor' musi sie pokazac ponownie.
- Podwojne wykonanie zmian stron: Kronika i CanonCompanions (#move) mowia o tych samych postaciach (Jaime m01, Yoren m22, Brienne m16/m17, Theon m18/m19, Ogar m10/m11...). Jedyny wykonawca to CanonCompanions przez move:<id>. Walidator odrzuca akcje clan/die/prisoner na postaciach majacych wiersz #move. Prog KW-13.
- DisableHeroAction.Apply niszczy partie dowodcy (DestroyPartyAction - ludzie w nicosc, ksiega ludzi). Przed nim zawsze LeaveParty/StartDisband. Nie ukrywac glow rodow, wladcow (np. Daenerys w nr 78 - tylko wersja bez znikania, gdy rzadzi) ani gubernatorow. Powrot z teleportem do siedziby.
- Postacie potrzebne pozniejszym wydarzeniom ROT (Ramsay, Mance, Jon, Daenerys, Stannis, Roose, Robb) nie moga zginac ani zniknac przed swoim wydarzeniem ROT. Potrzebna lista chronionych w walidatorze i odmowa akcji w kodzie z linia logu.
- Trzymanie wydarzen ROT przez CrashScribe moze przesunac lancuch Fabuli (tempo 1 na 4 dni). Limit 5 dni na okno, linia po obu stronach, prog KW-14. Klucz ChronicleHoldsRotEvents to wylacznik.
- Lawina okien po dlugiej niewoli albo wczytaniu starego zapisu. Tempo 2 na dobe z odstepem 6 h, pomijanie spoznionych ponad 30 dni (albo zamiana w wiesc) oraz pomijanie przy zapisie sprzed paczki ('pk=' w zapisie).
- Zamknieta ekonomia: akcje gold/ware/garrison/band/pop musza miec platnika albo zrodlo. Bezposrednia zmiana Town.Prosperity moze sie gryzc z DailyLife.Prosperity i ksiega ludzi (sprawdzic przed K1). Nagrody zadan i niedoplaty musza miec linie z platnikiem (KW-6).
- Bandy Kroniki przez BanditPartyComponent.CreateLooterParty z klanem niebandyckim (Bractwo bez Choragwi, Stone Crows, Synowie Harpii) moga sie wywrocic albo zle zachowywac - sprawdzic w dekompilacji przed K1/K2. Sufit 12 band (setki partii = CTD).
- Id okien sa trwale w zapisie. Przenumerowanie albo usuniecie wiersza w pozniejszej galezi zepsuloby stan, dlatego nagrobki zamiast usuwania. Rownolegle galezie na tych samych plikach (Settings.cs, sprawdz_logi.py, Chronicle.Actions.cs) to pulapka scal.sh (fuzz, te same nazwy grup progow). Galezie robic po kolei; dane per paczka w osobnych plikach.
- Dysk C Jeffa zeruje pliki: wyzerowany TSV paczki wylacza tylko te paczke (linia ZLY PLIK, KW-0 nie przejdzie). Skrypt wgrania musi kopiowac ModuleData/Kronika - bez tego Kronika jest pusta.
- Lektor szuka dzis po tytule. Warianty z tym samym tytulem czytalyby zly tekst, dlatego potrzebny lookup po kluczu ArmStoryEvent.Key - zalezy od scalenia m103-lektor z m103-sceny. Budzet znakow ElevenLabs jest maly (rezerwa 14000 na prolog). Calosc ok. 130 tys. znakow, wiec nagrania tylko okien 1, po paczce. Plotki Kroniki nie moga isc do nagrywania na zywo (Lektor.Live), bo palilyby znaki.
- CanonAlliancesBreakOnLoad zrywa kazdy sojusz Westeros spoza listy. Pelne wersje nr 97 i 107 (sojusz/rozejm) wymagaja dopisania par do listy, inaczej sojusz zniknie po wczytaniu.
- Skala osi: dni Kroniki sa w x4.33. Zmiana Config.FabulaTimeScale przesunelaby okna wzgledem ROT, dlatego przeliczanie przez k = skala/4.33 i kotwice 'przed/po' dla wszystkiego, co musi zachowac kolejnosc.
- Akcje 'muster' (cele zbiorki AI) moga byc ignorowane albo kolidowac z AI BK/StrategicCampaignAI - tylko jednorazowe, miekkie, z logiem; w razie problemow sam tekst.
- Kampania Jeffa (prolog od WGRANIA 101) dojdzie do dnia osi 10 ok. 198 doby od startu prologu. K0 i K1 musza byc wgrane wczesniej, a nie wolno wgrywac, gdy Jeff gra.
- ShowMultiSelectionInquiry z identyczna trescia daje FailedAssert duplikatu (GauntletQueryManager) - opis listy unikalny (id okna). UIExtender ROT InquiryElementVMMixin dotyka kazdego InquiryElementVM, ale dziala tylko dla ROTStorylineWar - sprawdzone, nieszkodliwe.

**Pytania:**

- Wersje pelne 16 duzych wydarzen (23, 26, 56, 59, 63, 81, 86, 94, 95, 100, 103, 107, 108, 115, 116, 117): domyslnie lekkie (klucz ChronicleFullVersions = NIE). Czy Jeff chce ktores od razu pelne?
- Punkty 'do decyzji' z Kroniki - proponuje domyslne, chyba ze Jeff zdecyduje inaczej: 46 Jorah zostaje w Astaporze (bez wedrowca; wtedy 101 odpada); 47 bez nowej postaci Pani Kamienne Serce (Beric zyje jak w serialu); 71 tylko relacje Karstarkow; 77 bez ser Roberta Stronga; 87 Yara i Theon jako wedrowcy na dworze Daenerys; 95 sama relacja Reach z korona (bez wojny); 100 bez sojuszu Wysp z korona; 106 martwy smok tylko w tekscie.
- Czy 6 scen 'juz w grze' (Tyrion Reka, Theon na Pyke, nowy lad, miecz Brienne, Sam do Starego Miasta, Tyrion w niewoli) robimy w Kronice zamiast w E4 - proponuje tak, bo wypadaja po dniu 188 i sa podpiete pod zmiany stron.

## kronika:B (badacz)

**Podsumowanie:**

KRONIKA B: przejrzałem 117 wydarzeń Kroniki v2 (204 okna z dokumentu, z wariantami 210). Porównałem je z kodem m103-sceny (StoryWindow, arm_canon_moves.tsv, Prologue.End i Prologue.Peace, CanonAlliances), z danymi ROT (bohaterowie, rody, osady, 48 sprite'ów ui_event_category) i z dekompilacją wydarzeń ROT. Niczego nie zmieniałem w repo ani w grze. Pliki robocze leżą w scratchpadzie kronB/: tabela.py, stat.py, linie.txt, rot_sheet1.jpg i rot_sheet2.jpg.

WYNIK:
- 40 wydarzeń to samo okno, relacje albo plotka (w wersji lekkiej).
- 25 zmienia świat klockami, które już są albo są proste.
- 52 potrzebuje co najmniej jednego nowego klocka.

Cztery nazwane nowe mechaniki:
- śmierć lub zmiana władcy królestwa: 10 wydarzeń (42, 49, 57, 81, 86, 95, 115, 116, warunkowo 100 i 112), w wersji pełnej jeszcze 108 i 117;
- zmiana właściciela zamku: 1 (63), w wersji pełnej jeszcze 23, 56, 59, 94, 103, 117, a w 70 ród przechodzi razem z zamkiem;
- upadek rodu lub frakcji: 63, 81, 95 (Wiara Wojująca), w wersji pełnej 94; do tego 101, jeśli Jorah zostanie wygnany jako wędrowiec;
- nowa frakcja: żadne wydarzenie jej nie wymaga. Wszystkie frakcje są już w ROT (brotherhood, faith_militant, brave_companions, sons_of_the_harpy, stone_crows, looters 'Broken Men'). Istniejące frakcje mniejsze ruszają wydarzenia 17, 47, 62, 75, 89 i 95.

Najczęstszy brakujący klocek to ukrycie i powrót postaci (14 wydarzeń + 4 powroty). Dziś go nie ma: akcja 'remove' w arm_canon_moves to KillCharacterAction.ApplyByRemove, czyli śmierć. Dalej:
- ekonomia (ludzie, jedzenie, wojsko, Bank): 12;
- władca: 10;
- zadania: 8;
- wybór z 3 lub więcej opcji: 7 (okno ROT ma tylko 2 przyciski);
- cel dla armii AI: 6;
- smoki: 3 (to przedmioty-wierzchowce dragon_*);
- tabor: 2.

Ryzyko techniczne: niskie 41, średnie 49, wysokie 19, pośrednie 8 (5 S/W, 3 N/S).

LEKTOR: 209 nagrań (okno 2 nr 31 odpada, bo Hostera Tully'ego nie ma w ROT), około 102 tys. znaków. To około 75 tys. kredytów ElevenLabs według przelicznika z dwóch nagranych partii (ROT 10 651 znaków dało 7 706 kredytów, prolog 11 926 znaków dało 8 760, czyli 0,72-0,73). Paczka to 6-12 tys. znaków.

OBRAZKI: 36 z 48 sprite'ów ROT nadaje się do zmiany obrazków w 18 tematach. Z 23 obrazków prologu są na razie 3 (prolog_01-03). Kolejne same dojdą do puli.

BŁĘDY FAKTÓW W KRONICE v2, które trzeba poprawić przed kodem:
- Pięść (castle_S6) należy do Tormunda, nie do Nocnej Straży;
- w ROT nie ma Hostera Tully'ego, Ellarii, Olenny Tyrell (Olenna Cuy to inna osoba), Slynta, Dontosa, Rodrika Cassela, Rorge'a, Edrica Storma ani Gilly; Pycelle jest tylko notablem;
- Arianne jest w rodzie Aegona;
- Asha i Yara to w ROT dwie osobne postacie;
- po JoffreyEvent ROT robi władczynią Cersei, nie Tommena;
- ROT RamsayEvent sam przenosi Karstarków;
- BastardsEvent niszczy ród Snow;
- ROT nie bierze Edmure'a do niewoli;
- Yunkai trzymają Drudzy Synowie.

**Plan:**

A. BIBLIOTEKA KLOCKOW (kody z tabeli; status: JEST = jest w kodzie, PROSTY = nowy, ale prosty, NOWA = nowa mechanika)
WARUNKI:
- Z(h) zyje / SM(h) zmarl - JEST (CanonCompanions 'dead:').
- R wladca / glowa rodu - PROSTY.
- O(osada, wlasciciel) - JEST dla krolestwa ('own:'), dla rodu PROSTY.
- WO(a,b) wojna - JEST ('war:').
- E(wydarzenie ROT start/done +N) - JEST ('ev:/done:').
- EP(wydarzenie ROT, -N), czyli N dni przed wydarzeniem ROT - NOWY: dzien z osi Fabuli (CrashScribe Fabula) odbiciem, jak CanonAlliances.IsPrologueAlliance.
- D(a-b) dni osi opowiesci - JEST ('rotday:', StoryDay), losowy dzien w oknie PROSTY.
- K(nr[.okno]) znacznik Kroniki - NOWY (zapis arm_kronika).
- J(h) jeniec - PROSTY.
- G(gracz: region, wasal, armia, odleglosc, relacja, Straz, sluzba Daenerys) - PROSTY.
- P(pora roku) - JEST (r2-klimat).
- OG (glod w oblezeniu 3 doby) - NOWY.
- RB (rabunek w regionie) - JEST (ksiega Rumours, RaidRefugees).
- ST (smierc ze starosci) - NOWY: OnHeroKilled z detalem DiedOfOldAge.
- IN (Inni: zwyciestwo, osady, oblezenie) - czesciowo JEST (linia UMARLI).
- GO (glod druzyny) - NOWY.
SKUTKI:
- OK okno 1-2 przyciski - JEST (StoryWindow / ArmStoryEvent).
- W3 wybor z 3 i wiecej opcji - NOWA (EventScreen ma tylko Done/Cancel).
- WIAD - JEST (Log.Player).
- REL - PROSTY (ChangeRelationAction).
- SL slawa i honor - PROSTY.
- PL plotka - PROSTY (nowy wpis w ksiedze Rumours.cs).
- CM ruch postaci kanonu (clan/court/lead/prisoner/free/wander/remove/die/marry/wed) - JEST (arm_canon_moves.tsv); potrzebny nowy wyzwalacz kr:.
- SM smierc - JEST (die / KillCharacterAction).
- SU wskazany dziedzic, ChangeClanLeaderAction przed smiercia - JEST jako wzor w Prologue.End (Robert->Joffrey, Ned->Robb).
- JE jeniec fabularny - JEST (prisoner + loch jak Ned); bez ucieczek i okupow - z E5 (Tyrion).
- UW uwolnienie - JEST.
- UK/PW ukrycie i powrot - NOWA; 'remove' zabija, wiec nie nadaje sie do powrotu.
- RN ranny - PROSTY.
- SK umiejetnosci - PROSTY.
- PR przedmiot - PROSTY. W ROT sa: longclaw_sword, oathkeeper_sword, heartsbane, widows_wail, vargo_*, dragonglass_axe; nie ma valyrianskiego sztyletu. Przedmiot przenosimy, nie tworzymy.
- BA banda i OD oddzial frakcji mniejszej - klocek z E5 (klany gorskie, ludzie Gory, Beric).
- CEL cel AI / zbiorka - NOWA, ryzyko AI.
- OS osada (dobrobyt, lojalnosc, bezpieczenstwo, jedzenie, milicja) - PROSTY, z zasadami ekonomii.
- LU ludnosc - NOWA/EKON: przez ksiege ludzi Armoury (RaidRefugees, DailyLife).
- ZL zloto z platnikiem - PROSTY.
- ZA zadanie - NOWA: stan w arm_kronika, bez QuestBase (decyzja D6).
- TU turniej - z E5.
- WJ wojsko przenoszone, nie z niczego - NOWA/EKON.
- DY sojusz, pakt, rozejm - JEST jako wzor (Prologue.Peace + lista CanonAlliances).
- WK koniec wojny ROT - NOWA: wyzwalacz ROTStorylineWars, jak CanonReachNorthWar.
- ZM zamek: ChangeOwnerOfSettlementAction - NOWA.
- UP upadek rodu lub frakcji - NOWA.
- RK rod zmienia krolestwo - NOWA.
- NP nowa postac - NOWA.
- SMK smok (dodanie albo zabranie przedmiotu dragon_*) - NOWA, razem z m104 'mlode smoki'.
- TB tabor - NOWA.
- PJ pojedynek - NOWA.
- ZN znacznik albo licznik - baza Kroniki.
- WZ wizja (tekst z mapy) - PROSTY, tekst dynamiczny.

B. LICZBY
- 117 wydarzen: klasa O 40, Z 25, N 52.
- O (samo okno lub relacja, lekka): 1-7, 9-13, 18, 21, 24, 25, 27, 30, 40, 41, 43, 46, 53, 55, 56, 60, 62, 64, 66, 68, 69, 70, 74, 79, 85, 93, 97, 107, 111, 117.
- Z: 16, 19, 31, 32, 33, 35, 44, 51, 52, 54, 59, 71, 72, 73, 80, 83, 87, 90, 94, 99, 101, 105, 108, 109, 114.
- N: pozostale.
- Ryzyko W: 26, 29, 38, 57, 63, 76, 77, 78, 81, 83, 86, 95, 96, 100, 103 (pelna), 104, 107 (pelna), 115, 116.
- Ryzyko S/W: 47, 49, 58, 84, 112.

C. TABELA 117 (tytul [okna/nagrania ~znaki lektora] klasa tagi | W: warunki | S: skutki | R: ryzyko | Obr: temat obrazka). Skroty: o2 = okno 2; '*'/'pelna' = tylko w wersji pelnej.
1 Bialy plaszcz pod tronem [2/2 ~1040] O | W: Z(Barristan lord_S9_l) E(NedEvent done) D10-25; o2 WO(qarth,empire_s) D150-400 | S: OK REL(Barristan-Joffrey/Cersei -, -Daenerys +) | R: N: Barristan w ROT od startu we wlasnym rodzie clan_sturgia_9 u Daenerys - samo wspomnienie | Obr: T1;T12
2 Wilk schodzi z Polnocy [2/2 ~1040] O (CEL-AI opcja) | W: Z(Robb lord_5_1) wladca battania, WO(battania,vlandia) D12-30 przed E(RiverlandsDeclareWarEvent) | S: OK REL(Greatjon lord_5_16, Smalljon lord_5_20 +Robb) SL(Robb); wasal 2 przyciski; opcja CEL(zbiorka castle_B1) | R: N (S z CEL) | Obr: T3;T4
3 Sansa na dworze Joffreya [2/2 ~1040] O | W: Z(Sansa lord_5_13_1, Joffrey lord_7_1) Sansa w town_EW4 D12-40; o2 K(22) D390-520 | S: OK REL(Sansa-Joffrey --, -Tyrion +); dwor: 2 przyciski; opcja TU(town_EW4) | R: S: sprawdzic, gdzie ROT trzyma Sanse po NedEvent; Dontosa brak - tekst | Obr: T1
4 Trupy wstaja w Czarnym Zamku [1/1 ~520] O | W: O(town_B5, nightswatch) Z(Jeor lord_5_3, Jon) D15-60 | S: OK REL(Jon-Jeor ++) PR(longclaw_sword tylko, gdy Jon go nie ma) | R: N | Obr: T7;T9
5 Stos khala Drogo [1/1 ~330] O | W: E(NedEvent done) + WO(khuzait,empire_s) Z(Daenerys lord_1_14) | S: OK REL(Pono lord_6_1 - Daenerys -) PL(Essos 30 d) | R: S: m104 rusza wojny Essos od kalendarza, wiec wojna Dothrakow moze wybuchnac w PROLOGU (Drogo zyje w scenach 12/18/21) -> warunek E(NedEvent done) obowiazkowy | Obr: T12
6 Czerwona kometa [6/6 ~1980] O | W: D25-70 raz; G(region) | S: OK w 6 wersjach (Polnoc, Przystan, Smocza Skala, Mur, Wyspy, Essos) PL(caly swiat 30 d) | R: N | Obr: wedlug regionu
7 Plonacy bogowie i list Stannisa [2/2 ~1040] O | W: O(town_EW1, clan_empire_west_3) Z(Melisandre lord_1_12, Stannis lord_1_11) D30-80; o2 +10-30, przed WO(dragonstone,vlandia) | S: OK REL(Stannis-Melisandre +, Joffrey/Cersei-Stannis --) PL(bekarty) | R: N | Obr: T10
8 Arya na wozie Yorena [2/2 ~1040] N UKRYCIE | W: Z(Arya lord_5_1_1) D35-65; o2 E(HarrenhalSiegeEvent done) | S: CM(Yoren die: m22 -> kr:8) BA(grabiezcy Lorcha lord_4_25 przy Oku Boga) REL(Tully i wsie po rozbiciu bandy) UK(Arya) | R: S: ukryta Arya jest czlonkiem rodu Starkow (dziedziczenie); Yoren nie moze zginac dwa razy | Obr: T5
9 Reka sprzata stolice [2/2 ~1040] O | W: Z(Tyrion ROTwanderer8) O(town_EW4, vlandia) D60-200 | S: OS(town_EW4 bezp.+, lojal.+) REL(Tyrion-Cersei -, -Varys +); Slynta brak, Pycelle to notabl - tekst | R: N | Obr: T1
10 Czerwone Pustkowie [2/2 ~850] O | W: Z(Daenerys) po E(NedEvent done) D60-150; o2 WO(qarth,empire_s) | S: OK REL(Qarth ROTlord_619 - Daenerys -) | R: N (warunek jak w nr 5) | Obr: T12
11 Zielone Widly [1/1 ~520] O | W: Z(Roose lord_1_17, Tywin lord_4_1) WO(battania,vlandia) E(HarrenhalSiegeNotificationEvent) D80-110 | S: SL(Tywin, Tyrion) REL(Tyrion-Shagga +) | R: N | Obr: T4
12 Blizniaki [1/1 ~520] O | W: O(ROT_town3, Frey) Z(Walder lord_1_5, Robb) D85-125 | S: REL(Walder-Robb +); w armii Robba 2 przyciski ZL(gracz->Walder) | R: N | Obr: T6
13 Bialy kruk [3/3 ~990] O | W: P(koniec lata, zima, wiosna) | S: OK; zapas spichlerzy tylko na ekranie, lektor czyta staly akapit | R: N | Obr: T15;T9
14 Wielki Zwiad [2/2 ~1040] N CEL-AI;ZADANIE | W: K(4) Z(Jeor) wladca nightswatch D100-200; o2 +30-60 | S: CEL(druzyna Jeora przy castle_S6) ZA(dowoz jedzenia) Straz: 2 przyciski REL | R: S: Piesc castle_S6 nalezy do clan_sturgia_7 (Tormund), NIE do Strazy | Obr: T8
15 Szczury w spizarni [2/2 ~660] N EKON | W: OG(3 doby, raz na oblezenie) G(rola gracza) | S: 2 przyciski: OS(jedzenie+, konie-) albo LU(chlopi za mury); oblegajacy: honor i tempo oblezenia; AI tylko WIAD | R: S: przez model oblezen i ksiege ludzi | Obr: T5
16 Szepczacy Las [2/2 ~850] Z | W: WO(vlandia,riverlands) start; o2 D130-195 Z(Jaime ROTwanderer1, Robb) | S: CM(Jaime prisoner:town_V2 Riverrun; m01 -> kr:16) SL(Robb) REL(Lannister-Stark -) | R: S: tylko jeden wyzwalacz niewoli | Obr: T4;T5
17 Bractwo bez Choragwi [2/2 ~850] N WYBOR3;TABOR;FRAKCJA | W: G(Dorzecze) D130-300; o2 D800-1000, przywodca brotherhood zyje | S: W3 ZL REL SL TB FR(brotherhood wrogie graczowi) | R: S: Beric to szablon ROTuniqueleader; oddzial z E5 | Obr: T5
18 Dolina zamyka bramy [1/1 ~520] O | W: bez WO(empire,vlandia) Z(Lysa lord_4_6) D150-300 | S: REL(Dolina-Robb/Catelyn -, Dolina-korona +); wasal 2 przyciski | R: N | Obr: T17
19 Myrcella odplywa [1/1 ~520] Z | W: Z(Myrcella lord_4_10) w town_EW4 D215-390 | S: CM(Myrcella -> town_A1, zostaje w rodzie) REL(Dorne-korona +) OS(town_EW4 lojal.-, bezp.-, jedzenie -) | R: S: sojusz Dorne-korona robi m104, okno tylko go oglasza | Obr: T1
20 Lady Hornwood [2/2 ~1040] N ZADANIE | W: Z(Ramsay lord_1_29) O(castle_K9) D250-420, przed K(26) | S: BA przy castle_K9, ZA(zloto ze skarbca Starkow) REL SM(Donella lord_7_5_2) | R: S: Halys lord_7_6 i Daryn lord_7_5_1 w ROT zyja (w ksiazce polegli) | Obr: T11;T3
21 Renly i turniej [2/2 ~1040] O (TU z E5) | W: Z(Renly lord_1_9) wladca stormlands D250-400; o2 D400-500 przed E(RenlyDeathEvent) | S: REL(Renly-Mace lord_1_7 +) TU(castle_EW1 Bitterbridge) | R: N | Obr: T1
22 Wolowy Brod [1/1 ~520] N CEL-AI | W: Z(Robb) WO(battania,vlandia) D380-500 | S: OS(wsie Zachodu -) CEL(ROT_castle40, castle_EN6) SL | R: S | Obr: T4
23 Zelazna flota [1/1 ~520] N CEL-AI;ZAMEK* | W: WO(sturgia,battania) start | S: CEL(castle_B1, ROT_town2) BA(northern_pirates) WIAD; pelna ZM | R: S, w pelnej W: castle_B1 potrzebna Krwawym Godom ROT | Obr: T13
24 Zelazni nad brzegiem [1/1 ~330] O | W: RB(wybrzeze) raz na 30 d | S: 2 przyciski + najezdzca widoczny na mapie | R: N | Obr: T13
25 Pod Koncem Burzy, drugi cien [2/2 ~1040] O | W: EP(RenlyDeathEvent,-20..-10); o2 E(RenlyDeathEvent done) | S: OK REL(Davos lord_WE9_l - Melisandre -) | R: N | Obr: T10;T16
26 Theon zdobywa Winterfell [2/2 ~1040] N UKRYCIE | W: po E(RenlyDeathEvent) WO(sturgia,battania) D540-650; o2 +40-90 przed E(BlackwaterEvent) | S: OS(town_B2 zaloga --, dobrobyt --) UK(Rickon ROTlord_514) CM(Theon: m19 -> kr:26.2) | R: W: oslabiony Winterfell moze zdobyc AI, a od niego zaleza okna ROT (Krwawe Gody, Bekarty) | Obr: T3
27 Roze przy lwie [1/1 ~520] O | W: Z(Mace) O(town_EW4) po E(RenlyDeathEvent) | S: REL; sojusz robi m104 | R: N | Obr: T1
28 Lancuch i dziki ogien [2/2 ~850] N ZADANIE;EKON | W: przed E(BlackwaterSiegeNotificationEvent); o2 E(BlackwaterEvent done) | S: ZA(zelazo i wegiel, placi korona) REL SL OS(milicja z ludnosci) | R: S | Obr: T2
29 Jon i Polreki [2/2 ~1040] N UKRYCIE | W: K(14) Z(Jon, Mance lord_2_3) | S: UK(Jon) SM(Qhorin lord_7_13_2) REL; lekka: same okna | R: W: Jon to glowa rodu Snow; clan_nord_3 to ROTClans.Halfhand, ktorego JonEvent robi rzadzacym Straza | Obr: T8
30 Edmure nad brodami [1/1 ~520] O | W: Z(Edmure lord_1_3, Tywin) WO(riverlands,vlandia) | S: SL REL | R: N | Obr: T5
31 Zlamana przysiega [3/2 ~1040] Z | W: K(22); o2 Hostera BRAK (nie nagrywac); o3 przed E(RedWeddingQuest) | S: CM(slub Jeyne m26 - juz jest) REL(Walder-Robb --) | R: N | Obr: T6;T5
32 Catelyn uwalnia Krolobojce [1/1 ~520] Z | W: J(Jaime w town_V2) Z(Catelyn ROTlord_513) przed E(RickardKarstarkExecutionEvent) | S: 2 przyciski: UW + CM(m02 -> kr:32) albo alarm ZN | R: S: zapas - Jaime wolny przy Krwawych Godach | Obr: T5
33 Po Czarnym Nurcie [2/2 ~1040] Z (UKRYCIE opcja) | W: E(BlackwaterEvent done, Stannis przegral)+5..30 | S: REL CM(Ogar m10 - juz jest); 2 przyciski UK(Sansa) | R: S | Obr: T2;T1
34 Astapor [1/1 ~520] N EKON | W: O(town_A2) D780-840 | S: WJ(Nieskalani z ludnosci do zalogi) OS | R: S | Obr: T12
35 Sansa i Krasnolud [1/1 ~520] Z | W: Sansa w town_EW4 D800-930 | S: CM(Sansa wed Tyrion -> clan_vlandia_1) REL | R: S: Sansa wychodzi z rodu Starkow | Obr: T1
36 Sad w jaskini [1/1 ~520] N WYBOR3 | W: Ogar wedruje D830-890 | S: W3 REL ZL(Ogar->Bractwo) | R: N | Obr: T5
37 Krwawi Komedianci [2/2 ~1040] N ZADANIE | W: K(32)+40..110 | S: SK(Jaime: walka -50%) REL OD(brave_companions) ZA(nagrody: zloto, vargo_armor) | R: S | Obr: T5
38 Ucieczka z Piesci [2/2 ~1040] N UKRYCIE | W: EP(JeorMutinyEvent,-25..-5) | S: SL(Sam) PR(dragonglass_axe) UK(Bran lord_5_7) | R: W: przy Krwawych Godach ROT robi Brana glowa Starkow, czyli wladca Polnocy -> lekka bez ukrycia | Obr: T9
39 Smierc starego lorda [3/3 ~990] N WYBOR3 | W: ST | S: W3 + termin 7 dni, REL(+5/+1) | R: N | Obr: wedlug krainy
40 Yunkai [1/1 ~520] O | W: O(town_A3 - Druga Synowie clan_khuzait_2) | S: REL | R: N | Obr: T12
41 Pijawki [2/2 ~850] O | W: Stannis przegral nad Czarnym Nurtem, przed E(RedWeddingQuest) | S: OK | R: N | Obr: T10
42 Smierc Balona [2/2 ~1040] N WLADCA;WYBOR3 | W: Z(Balon lord_2_1) D910-1040 | S: SM SU(Euron lord_2_13 = wladca Wysp) W3(Euron, Victarion lord_2_7, Asha lord_2_10) | R: S: Asha i Yara lord_2_7_1 sa obie w ROT | Obr: T13
43 Testament Robba [1/1 ~520] O | S: REL | R: N | Obr: T3
44 Rzez w gospodzie [3/3 ~1560] Z | W: E(RedWeddingQuest) i K(33); o2-o3 D1020-1125 | S: SM(Polliver lord_4_25_1) UK(Arya) OS(town_EN3 --) BA | R: S | Obr: T6;T5
45 Zlamani ludzie [1/1 ~330] N EKON | W: RB(>30% ludzi poza domem i rabunek w 30 d) | S: BA(looters z puli RaidRefugees; herszt z Rumours) PL; listy goncze dochodza same | R: S | Obr: T5
46 Meereen: krzyze i Jorah [2/2 ~1040] O | S: 2 przyciski OS/REL; o2 REL(Jorah lord_7_1_3 --) | R: N; DECYZJA o wygnaniu | Obr: T12
47 Pani Kamienne Serce [2/2 ~1040] N FRAKCJA;POSTAC* | W: Catelyn zginela; o2 K(64), K(70) | S: SM(Beric) FR(brotherhood wrogie rodom, jency gina) UK(Brienne 20-40 d); opcja NP | R: S/W | Obr: T5
48 Thennowie [2/2 ~1040] N ZADANIE | W: EP(WallSiegeNotificationEvent) | S: BA(clan_sturgia_5 Styr lord_2_17) OS ZA RN(Jon) SM(Ygritte lord_2_15) | R: S | Obr: T8;T7
49 Ucieczka Sansy, Ksiezycowe Drzwi [2/2 ~1040] N WLADCA;UKRYCIE | W: noc po E(JoffreyEvent); o2 D1190-1300 | S: UK(Sansa); o2 SM(Lysa) SU(Robin lord_4_121 = wladca Doliny) | R: S/W | Obr: T17
50 Czerwona Zmija [2/2 ~1040] N POJEDYNEK* | W: Z(Oberyn lord_3_13); o2 E(JoffreyEvent)+10..45 | S: SM(Oberyn) SM z opoznieniem (Gregor lord_4_21) REL; opcja PJ | R: S: Gregor to glowa rodu Clegane i pan Harrenhal | Obr: T1
51 Krol Tommen [2/2 ~1040] Z | S: CM(Margaery wed Tommen lord_4_7) REL; Tommen krolem TYLKO w tekscie | R: S: ROT robi wladczynia CERSEI | Obr: T1
52 Kusza w wychodku [2/2 ~1040] Z | W: K(50) albo E(JoffreyEvent)+40 | S: SM(Tywin) SU(Kevan lord_4_13) CM(m03 -> kr:52, Pentos) | R: S | Obr: T1
53 Dom Czerni i Bieli [3/3 ~990] O | W: K(44) | S: OK | R: N | Obr: T13
54 Piaskowe Zmije [1/1 ~520] Z | W: smierc Oberyna +65..175 | S: 2 przyciski: Zmije UK 60 d albo BA(Dornijczycy napadaja kupcow z Reach) | R: S: jeniec we wlasnym rodzie niemozliwy | Obr: T18
55 Kruk wybiera Jona [2/2 ~1040] O | W: E(EastwatchEvent done) | S: REL(Jon +, Alliser ROTlord_568 -) | R: N | Obr: T7
56 Fosa Cailin [2/2 ~1040] O, ZAMEK* | W: O(castle_B1, Zelazni) | S: lekka REL; pelna ZM | R: N, pelna W; w lekkiej warunek nigdy sie nie spelni | Obr: T11
57 Stos Krola za Murem [1/1 ~520] N UKRYCIE albo WLADCA | W: E(WallEvent 'Wildlings Routed') i E(EastwatchEvent) | S: SM(Lord of Bones lord_2_22) REL; Mance to WLADCA freefolk | R: W | Obr: T10;T8
58 Zelazny Bank [3/3 ~1560] N EKON | W: po smierci Tywina; Stannis na Polnocy | S: ZL korona->Bank (gorsze warunki kredytu, system dlugow m96); ZL Bank->Straz/Stannis | R: S/W: bez sztywnych mnoznikow cen | Obr: T15
59 Stannis i klany gorskie [2/2 ~1040] Z, ROD-KROL* ZAMEK* | S: lekka REL(Norrey ROTclan_88, Flint ROTclan_30 ++) JE(Asha); pelna RK ZM(ROT_town2) | R: S | Obr: T15;T4
60 Lordowie Deklaranci [2/2 ~1040] O | W: K(49) | S: 2 przyciski REL(Royce lord_1_1 / Petyr) ZL | R: N | Obr: T17
61 Martwi ida [3/3 ~990] N ZADANIE | W: IN(1, 5, 15, 25 osad) | S: OK ZA(jency do town_B5) REL SL | R: N/S | Obr: T9
62 Wiara uzbrojona [1/1 ~520] O (FRAKCJA, jesli uspiona) | W: smierc Tywina +130..220 | S: FR(faith_militant - jest w ROT od startu) OS | R: S | Obr: T1
63 Oblezenie Riverrun [1/1 ~520] N ZAMEK;UPADEK | W: O(town_V2, Blackfish lord_1_50) J(Edmure) | S: pelna ZM(-> Emmon lord_1_1_9), Blackfish wedruje albo SM; lekka samo okno | R: W: Blackfish to glowa wlasnego rodu; ROT nie bierze Edmure'a do niewoli | Obr: T5
64 Cicha Wyspa [3/3 ~1560] O | S: OK RN(Brienne) | R: N | Obr: T5
65 Davos w Bialym Porcie [2/2 ~1040] N UKRYCIE | W: O(town_B3) | S: REL UK(Davos 60-120 d) | R: S: Davos to glowa rodu Seaworth | Obr: T13;T15
66 Bran i trojoka wrona [2/2 ~660] O | W: K(38) | S: WZ | R: N | Obr: T9
67 Euron nad Manderem [2/2 ~1040] N CEL-AI | W: K(42) | S: BA(sea_raiders) OS; o2 CEL(Victarion -> town_K1) | R: S | Obr: T13
68 Tyrion i Mlody Gryf [2/2 ~1040] O | W: K(52), Tyrion w town_EN4 | S: REL(Aegon ROTlord_369, Connington ROTlord_373) | R: N | Obr: T14
69 Pakt z Tormundem [2/2 ~1040] O (ZADANIE opcja) | W: EP(JonEvent,-90..-12) | S: REL(clan_sturgia_7 - Straz ++); opcja ZA(town_S7) | R: N | Obr: T8
70 Raventree [2/2 ~1040] O, ROD-KROL* | W: K(63) | S: REL; pelna RK(clan_empire_north_8 z castle_EN7 -> korona) | R: S | Obr: T5;T15
71 Alys Karstark [2/2 ~1040] Z | S: REL CM(Alys lord_2_18_1 -> clan_sturgia_5); przenosin Karstarkow nie trzeba (robi je RamsayEvent ROT) | R: N | Obr: T7
72 Slub w Winterfell [3/3 ~1560] Z | W: O(town_B2, Bolton) J(Theon) | S: OS(zaloga -) REL CM(Theon) | R: S: zaloga musi wytrzymac do Bekartow ROT | Obr: T11
73 Korona dla Myrcelli [2/2 ~1040] Z | W: K(19) Z(Doran lord_3_1) | S: 2 przyciski REL (Arianne ROTlord_372 jest w rodzie Aegona) SM(Arys ROTlord_278) BA(Gerold Dayne lord_2_21) | R: S | Obr: T18
74 Rozowy list [2/2 ~1040] O | W: EP(JonEvent,-17..-2) | S: OK w 2 wersjach | R: N | Obr: T7;T15
75 Meereen: Harpia, Hizdahr, Quentyn [3/3 ~1560] N ZADANIE;FRAKCJA | S: OD(sons_of_the_harpy) ZA; CM(m24 -> kr:75.2); Harpia znika; SM(Quentyn: m23 -> kr:75.3) | R: S | Obr: T12
76 Blada klacz [2/2 ~1040] N EKON | S: LU(town_A2); zaraza LU(town_K1) i ranni w oddzialach obok | R: W | Obr: T12
77 Areszt krolowych [2/2 ~1040] N UKRYCIE;POSTAC* | S: UK(Cersei = wladczyni, Margaery) REL; opcja NP(Robert Strong) | R: W -> lekka bez ukrycia | Obr: T1
78 Smok nad arena [2/2 ~1040] N UKRYCIE | S: UK(Daenerys = wladczyni empire_s) | R: W -> lekka: Daenerys nie znika | Obr: T12
79 Zapowiedzi najazdow [2/2 ~660] O | W: EP(AegonInvasionEvent,-40), EP(DanyInvasionEvent,-40) | S: OK PL | R: N | Obr: T14;T12
80 Varys i regent [1/1 ~520] Z | W: E(AegonInvasionEvent)+15..60 | S: SM(Kevan) SU(Lannisterowie: wskazac dziedzica) REL OS | R: S | Obr: T1;T14
81 Koniec Stannisa [2/2 ~1040] N WLADCA;UPADEK;WYBOR3 | W: przed E(JonEvent), zima albo GO | S: SM(Shireen lord_1_36, Selyse ROTlord_505) WJ(polowa ludzi -> pula band) W3; o2 CM(Melisandre -> Straz, PRZED smiercia Stannisa) SM(Stannis) UP(clan_empire_west_3) | R: W: StannisDeathEvent ROT dziala tylko, gdy Renly zyje; bez przeniesienia Melisandre to ona odziedziczylaby Smocza Skale | Obr: T16 (stannis-slain);T10
82 Prawda o Jonie [3/3 ~1560] N POWROT | S: ZN PW(Sam) REL | R: S: m21 robi Samowi 'remove' | Obr: T3;T7
83 Brienne odnajduje Sanse [2/2 ~1040] Z | S: CM(Sansa, Brienne, Podrick -> clan_battania_1, NIE do Snow) BA(poscig) REL | R: W: BastardsEvent niszczy rod Snow | Obr: T3
84 Rickon u Ramsaya [2/2 ~1040] N ROD-KROL | S: RK(Umber clan_battania_5) PW+JE(Rickon) SM | R: S/W | Obr: T11;T4
85 Lyanna Mormont [1/1 ~520] O | S: REL(Lyanna lord_B8_c, Glover); opcja CEL; wasal WJ | R: N | Obr: T4
86 Zamach w Slonecznej Wloczni [2/2 ~1040] N WLADCA;POSTAC;WYBOR3 | W: Ellarii BRAK w ROT | S: SM(Doran, Trystane lord_3_13_2, Areo lord_3_7) SU(Ellaria jako NP albo Obara lord_3_10) BA W3 | R: W | Obr: T16;T18
87 Yara i Theon odplywaja [1/1 ~520] Z | S: CM(-> clan_empire_south_1 albo wander) REL | R: S | Obr: T13
88 Drzwi Hodora [2/2 ~1040] N POWROT | S: PW(Bran -> town_B2 albo town_B5) | R: S | Obr: T9
89 Ogar wraca [1/1 ~520] N POWROT;FRAKCJA | S: PW(Ogar -> brotherhood) BA | R: S: m11 robi 'remove' | Obr: T5
90 Sam i Heartsbane [1/1 ~520] Z | S: PR(heartsbane od Randylla lord_4_3) REL | R: S: zalezy od m21 | Obr: T15
91 Rycerze Doliny [1/1 ~520] N EKON | S: WJ(z zalog Doliny -> druzyna Jona) REL | R: S | Obr: T4
92 Bialy Wilk [2/2 ~1040] N UKRYCIE | S: UK(Melisandre do nr 112) REL | R: N/S | Obr: T10;T7
93 Ostatnia lekcja w Braavos [1/1 ~520] O | R: N | Obr: T13
94 Zemsta za Krwawe Gody [2/2 ~1040] Z, UPADEK* ZAMEK* | S: SM(Walder, Black Walder lord_1_43, Lothar lord_1_6) SU(robi gra) REL; pelna UP ZM(ROT_town3) | R: S, pelna W | Obr: T6
95 Sept Baelora [2/2 ~1040] N WLADCA;UPADEK;FRAKCJA;EKON | S: SM(High Sparrow, Margaery, Loras lord_1_44, Mace = wladca Reach, Kevan, Tommen) UP(faith_militant) SU(Willas lord_1_24 albo Garlan lord_1_75) DY(koniec sojuszu Reach-korona) LU/OS | R: W | Obr: T2;T1
96 Ogien w Vaes Dothrak [1/1 ~520] N WOJNA;ROD-KROL | S: lekka WJ(Dothrakowie -> Daenerys) PW; pelna WK RK | R: W: EnforceWars ROT wypowie wojne znowu | Obr: T12
97 Sojusz Zmij i Roz [1/1 ~520] O, DY* | S: lekka REL; pelna sojusz + wpis do listy CanonAlliances | R: S | Obr: T12
98 Rodzenstwo [2/2 ~1040] N POWROT | S: PW(Bran, Arya) PR(dobry sztylet; valyrianskiego nie ma w ROT) REL | R: S | Obr: T3
99 Reka Krolowej [2/2 ~1040] Z | S: CM(m04 -> kr:99) REL(Yezzan ROTlord_486) | R: N | Obr: T12;T13
100 Flota Eurona [1/1 ~520] N WLADCA (warunkowo) | S: JE(Yara) SM(2 Zmije) REL; z Ellaria: SU(Dorne -> Arianne z rodu Aegona) | R: W | Obr: T13
101 Szara luszczyca [1/1 ~520] Z | W: Jorah wygnany jako wedrowiec (K(46)) | S: CM REL SL | R: S: wygnanie jako wedrowiec = upadek rodu clan_nord_2 | Obr: T12
102 Jon na Smoczej Skale [2/2 ~1040] N ZADANIE;EKON | S: REL ZA(obsydian) PR(dragonglass_axe z zapasu) | R: S | Obr: T10;T12
103 Skala i Wysogrod [2/2 ~1040] N, ZAMEK* | S: lekka ZL(skarbiec Tyrellow -> korona); pelna ZM(castle_V5, town_V5) | R: W w pelnej (town_V5 to stolica Reach) | Obr: T4
104 Zloto na Trakcie Rozanym [2/2 ~1040] N TABOR;CEL-AI | S: TB CEL 2 przyciski; o2 SM(Randyll, Dickon lord_4_11 - jesli sa jencami) | R: W | Obr: T4;T12
105 Proces w Winterfell [1/1 ~520] Z | S: SM(Petyr) REL 2 przyciski | R: N | Obr: T16 (rickard)
106 Za Murem po upiora [2/2 ~1040] N SMOK | S: SM(Thoros) REL SMK | R: S | Obr: T9
107 Smocza Jama [1/1 ~520] O, WOJNA* | S: lekka REL; pelna WK(rozejm) CM(Jaime wander) | R: W w pelnej | Obr: T1
108 Theon ratuje Yare [1/1 ~520] Z, WLADCA* | S: UW CM(Theon); pelna SU(Yara) | R: S, pelna W | Obr: T13
109 Twardy Dom [1/1 ~520] Z EKON | W: IN(town_S7) | S: przy K(69) OD(uchodzcy) LU | R: S | Obr: T9
110 Mur pada [2/2 ~850] N CEL-AI;EKON | S: CEL(town_B2) LU | R: N/S | Obr: T9 (wallfall)
111 Rycerz Siedmiu Krolestw [1/1 ~520] O | S: SL REL PR(heartsbane -> Jorah) | R: N | Obr: T3;T15
112 Dluga Noc [3/3 ~1560] N WLADCA (warunkowo) | S: SM uczestnikow bitwy wedlug MapEvent (Jorah, Theon, Lyanna, Beric, Edd lord_5_21) SM(Melisandre) | R: S/W: Edd to wladca Strazy | Obr: T9 (nightking)
113 Missandei [1/1 ~520] N SMOK | S: SMK SM(lord_1_47_1) REL ZN(gniew krolowej) | R: S | Obr: T12
114 Zdrada Varysa [1/1 ~520] Z | S: SM(Varys) REL PL | R: N: Varys zwykle jest u Aegona (m12) - wtedy wydarzenia brak | Obr: T10;T12
115 Dzwony [2/2 ~1040] N WLADCA;EKON | S: LU/OS(town_EW4 ---) SM(Cersei, Jaime, Euron = wladca Wysp, Qyburn, Gregor, Sandor) 2 przyciski ZL(lup z kasy miasta) | R: W | Obr: T2;T1
116 Ostatnia krolowa [2/2 ~1040] N WLADCA;SMOK;WYBOR3 | S: SM(Daenerys) SMK SU JE(Jon) W3 | R: W | Obr: T1 (StannisIronThrone)
117 Wielka Rada [2/2 ~850] O; WLADCA*, ZAMEK*, WYBOR3* | S: lekka OK REL; pelna ZM(town_EW4 i korona -> Starkowie) CM(Jon -> Straz) W3+; o2 epilog dynamiczny | R: N, pelna W | Obr: T1;T7
(Pelne linie z identyfikatorami: scratchpad kronB/linie.txt; dane: kronB/tabela.py; liczenie: kronB/stat.py)

D. OBRAZKI "NA ZMIANE" (36 sprite'ow ROT + obrazki prologu, gdy plik istnieje)
Poza rotacja:
- event-bg (to tlo okna);
- BlackwaterWarning (list 512x768);
- Davos i Stannis (portrety 1240x1024 na czarnym tle);
- Ship1 i Ship2 (wycinki z przezroczystoscia);
- testevent (1024x1024);
- duplikaty jonsnowres (= jon_rez) i RobbMarching (= robb).
StoryPictures rysuje StretchToFit, wiec obrazek inny niz 16:9 sie rozciaga. harrenhal (986x669) i jon_stand (1024x511) sa rozciagniete lekko - do przyjecia.
Tylko do pasujacych okien: stannis-slain (tylko nr 81). jon_death i ramsay_death w ogole pomijamy, bo pokazuja wyniki Bekartow ROT.
Tematy (sprite'y ROT | prolog_NN):
- T1 Przystan/dwor/tron: kingslanding, StannisIronThrone, joffrey | 01, 06, 11, 12, 15 (turniej), 17, 20, 23.
- T2 dziki ogien/pozar: blackwater, kingslanding.
- T3 Polnoc/Starkowie: robb, RobbMarching2, ned_stark, messenger-img | 02, 04, 05, 06, 07, 08.
- T4 wojna w polu: RobbMarching3, RobbMarching2, jon_stand, robb | 09.
- T5 Dorzecze/Bractwo/Harrenhal: riverlands, harrenhal, messenger-img | 13, 19, 20.
- T6 Blizniaki/uczta: traveling_merchant (most Blizniakow), robb_stark (sala uczty) | 04.
- T7 Mur/Straz: jon, jonsnow, eastwatch, jon_rez | 07, 10.
- T8 za Murem: freefolk1, freefolk2, fist | 10.
- T9 Inni/zima: nightking, freefolk3, WildlingsFail, wallfall, fist.
- T10 Smocza Skala/Melisandre: melisandre, StannisSetsSail.
- T11 Boltonowie: ramsay, roose_bolton.
- T12 Essos/Daenerys: dany_invasion | 03 (jest), 18, 21. SLABO: 18 wydarzen na 1 sprite.
- T13 morze/Wyspy/Braavos: StannisSetsSail, aegon.
- T14 Aegon: aegon, StannisSetsSail.
- T15 wiesci/listy: messenger-1, messenger-2, messenger-img.
- T16 smierc/egzekucja: renly-slain, rickard, jeor, ramsay | 08, 22.
- T17 Dolina (w ROT brak): harrenhal, messenger-img | 14, 16.
- T18 Dorne (w ROT brak): messenger-1, messenger-2, kingslanding.
- 'wedlug regionu' (nr 6, 39): Polnoc T3, Przystan T1, Smocza Skala T10, Mur T7, Wyspy T13, Essos T12, Dorzecze T5.
Regula wyboru: lista tematu + pliki prologu, ktore istnieja (StoryPictures.FindFile(DirPrologue, stem)). Indeks = staly skrot klucza okna mod n (bez zapisu). Gdy wypada ten sam obrazek co w poprzednim oknie sesji - bierzemy nastepny. Plik prologu przez StoryPictures.Acquire i Drop, jak w Prologue.Scenes.

E. LEKTOR
- 209 nagran, okolo 102 220 znakow. Okno = tytul + 2 akapity, do 7 linii (linie.py) - okolo 520 znakow; okna krotkie i dynamiczne okolo 330.
- Po rozdzialach: I 13,7 tys., II 13,8 tys., III 18,2 tys., IV 26,2 tys., V 30,3 tys. znakow.
- Paczki Kroniki 1-12: 9,9 / 7,5 / 7,4 / 7,8 / 9,5 / 9,8 / 9,5 / 12,0 / 7,3 / 7,4 / 6,2 / 7,9 tys. znakow.
- Kredyty: okolo 74,6 tys. przy zmierzonych 0,73 na znak (w najgorszym razie 102 tys. przy 1:1).
- Okna z liczbami i imionami (13, 15, 24, 39, 45, 61, 66, 117.2): nagrywamy staly akapit, reszta jest tylko na ekranie. Warianty "z obecnym wladca" zostaja bez glosu (cisza) albo ida na zywo przez Lektor.Live (rozszerzenie).
- Rozmiar: prolog to 44 MB WAV na 23 okna (okolo 1,9 MB na okno), czyli Kronika dalaby okolo 400 MB WAV. Trzeba OGG (ffmpeg jest w C:/Users/GAME/tools/ffmpeg) - sprawdzic SoundEvent.CreateEventFromExternalFile z .ogg; jesli nie przejdzie, zostaje WAV.
- Narzedzia: tools/lektor/scenariusz_kronika.py (z kronika.json po zatwierdzeniu tekstow EN) + nagraj_david.py (David v2, BNgbHR0DNeZixGQVzloa, eleven_multilingual_v2, 0.5/0.78/0.15/0.95). Pliki: Armoury/ModuleData/Lektor/EN/kronika_NNNx.* + wiersze w arm_lektor.tsv.
- Nagrywac paczkami, po teksty zatwierdzone; przed kazda paczka sprawdzic character_limit konta.

F. KROKI DLA PROGRAMISTY (galaz od n31-calosc, osobno od m104-e5 i m104-wojny)
1. Armoury/src/StoryGate.cs: wydzielic bramki z Prologue.Scenes (MapState, MapScreen.IsReady, LoadingWindow, inquiry, rozmowa, bitwa, okno ROT na stosie - StoryPictures.EventWindowOnStack) do jednej klasy. Jedno okno naraz (StoryWindow.IsOpen). Kolejnosc: okna ROT, potem prolog, potem Kronika.
2. Armoury/src/Kronika.cs (partial): Install/Reset/Daily/AppTick (straznik 4 razy na sekunde jak Prologue.Scenes). Zapis 'arm_kronika' przez SaveText: d= pokazane okna, f= znaczniki, c= liczniki, p= terminy (zadania, smierci z opoznieniem, powroty), h= ukryci, q= zadania. Okna pominiete, gdy sa ponad KronikaSkipLateDays po terminie. Najwyzej KronikaMaxPerDay okien na dobe.
3. Armoury/src/Kronika.Data.cs: generowany przez tools/kronika/gen_kronika_data.py z tools/kronika/kronika.json. Klucz kronika_NNNx; dni od-do; warunki i skutki w DSL; przyciski; wersje; temat obrazka; TextObject {=ARM_KR_NNNx_T|U|L|B1|B2}. Wzor: PrologueScenes.Data.cs + linie.py (pojemnosc 7 linii).
4. Armoury/src/Kronika.Conditions.cs: wspolny parser z CanonCompanions (ev:/done:/dead:/war:/own:/rotday:/prolday:). Nowe warunki: alive:, head:<clan>:<hero>, ruler:<kingdom>:<hero>, owner:<settlement>:<clan|kingdom>, prisoner:<hero>[:<where>], hidden:, kr:<NNNx>, flag:, cnt:, player:region|vassal|army|near|rel|nw, season:, siegehunger:3, raid:<region>:30, oldage, undead:<n|settlement>, partyhunger:<hero>:3, rotbefore:<Event>:<N> (dzien z osi Fabuli z CrashScribe odbiciem), outcome:<Event>:<tytul okna wyniku>.
5. Armoury/src/Kronika.Effects.cs: rel:, renown:, honor:, rumour:<region>:<dni> (wpis do ksiegi Rumours), move: (delegacja do CanonCompanions), kill:<hero>[:execution|murder|delay N], heir:<clan>:<hero> (wzor Prologue.End), prison:<hero>:<where>:noescape (wspolny z E5), free:, hide:<hero>[:dni], return:<hero>:<settlement> (Hero.ChangeState Disabled/Active + TeleportHeroAction; nigdy dla glowy rodu ani wladcy, chyba ze KronikaHideRulers), wound:, skill:, item:<hero>:<item>:from<hero|stock>, band: i party: (z E5), aitarget: (pod KronikaAiTargets), settle:<settlement>:<pole>:+-N, people:<settlement>:+-N:<przyczyna> (przez RaidRefugees.Track / DailyLife), gold:<from>><to>:N, quest:<typ>, tourney:<town> (E5), troops:<from>><to>:N, alliance:/pact:/truce:/peace: (Prologue.Peace + lista CanonAlliances), fief: i destroyclan: (tylko wersja pelna), newhero: (pod KronikaNewHeroes), dragon:<hero>:-<item>, flag:, cnt:.
6. StoryWindow.cs: Spec.Choices dla wiecej niz 2 opcji. Lancuch: pierwsze okno (A / "Other choices..."), drugie z reszta; zapas: MultiSelectionInquiry. AutotestUi zamyka wybor pierwsza opcja po 1,5 s i pisze linie "OKNO wybor". Dodac StoryWindow.Open.Key dla lektora.
7. Lektor.cs: dla ArmStoryEvent najpierw dopasowanie po kluczu StoryWindow.Open.Key (kolumna klucz w arm_lektor.tsv), dopiero potem po tytule. Warianty maja ten sam tytul.
8. CanonCompanions.cs + arm_canon_moves.tsv:
- nowy termin kr:NNN z zapasem przy wylaczonej Kronice, np. 'kr:032;done:BlackwaterEvent' (';' = stary wyzwalacz tylko, gdy KronikaEnabled false);
- przepiecia: m01 -> prisoner:town_V2 (decyzja Jeffa) i kr:16, m02 -> kr:32, m03 -> kr:52 (Pentos, potem Druga Synowie ok. 1480), m04 -> kr:99, m19 -> kr:26.2, m22 -> kr:8, m23 -> kr:75.3, m24 -> kr:75.2;
- nowa akcja hide/return zamiast remove dla m11 (Ogar) i m21 (Sam);
- #rescue clan_battania_6 -> clan_battania_1 (Snow do Starkow przy BastardsEvent).
9. CrashScribe/src/CanonAlliances.cs: razem z m104-wojny jeden wlasciciel sojuszy. Koniec Reach-korona przy kr:95; Dorne i Reach z Targaryenami przy kr:97 (pelna); Wyspy z korona przy kr:100 (opcja).
10. KronikaPictures.Pick(temat, klucz) wedlug punktu D.
MCM (Settings.cs, potem python tools/gen_mcm.py): KronikaEnabled=true, KronikaPackages="1-12", KronikaFullList="" (numery w wersji pelnej), KronikaHideHeroes=true (nie dotyczy glow rodow), KronikaHideRulers=false, KronikaQuests=true, KronikaBands=true, KronikaAiTargets=false, KronikaNewHeroes=false, KronikaPictureRotation=true, KronikaMaxPerDay=1, KronikaSkipLateDays=30.
LINIE LOGU (prefiks 'Kronika (m105): '):
- 'START | wlaczona TAK, paczki 1-12, pelne [], ukrywanie wladcow NIE | zapis: okien N, znacznikow M | potkniecia 0.'
- 'okno kronika_NNNx '<tytul>' pokazane | dzien osi X.XX (nalezne A-B) | warunki: ... | obrazek <sprite> (temat Tn, i/n) | okno ROT|zapas | lektor <plik>|cisza | T ms.'
- 'kronika_NNNx pominiete | powod: <warunek NIE> | wersja: <inna|brak>.'
- 'skutek kronika_NNNx <klocek> <cel> TAK|NIE (<powod>).'
- 'wybor kronika_NNNx -> <opcja> (gracz|autotest).'
- 'zadanie <id> START|WYKONANE|PRZEPADLO | nagroda <zl> od <platnik>.'
- 'STAT doba D | pokazane N, pominiete M, skutki TAK K / NIE L, ukryci H, potkniecia P.'
PROGI (grupa m105kronika w tools/sprawdz_logi.py) + test syntetyczny tools/kronika/test_kronika.py (wzor test_m103sceny.py):
- KR-1: START raz na sesje.
- KR-2: 0 ERROR i 0 potkniec Kroniki.
- KR-3: w biegu prolog 230 (dzien osi ok. 42) co najmniej 4 okna z (1.1, 2.1, 3.1, 4, 6) i 0 okien przed E(NedEvent done).
- KR-4: kazde okno ma linie obrazka i lektora.
- KR-5: zadne okno w bitwie ani przy ladowaniu.
- KR-6: kazdy skutek ma TAK albo NIE z powodem, 0 'wyjatek'.
- KR-7: Yoren ginie raz, Jaime trafia do niewoli raz (linie CanonCompanions m22/m01 kontra kronika_008/016).
- KR-8: arm_kronika <= 4 KB.
- KR-9: test syntetyczny: 117 wydarzen w swiecie kanonicznym i w swiecie innym (oczekiwane: pokazane, pominiete, wersja).
- KR-10: flaga armoury-kronika-test.flag (przyspieszone okna paczki) - wszystkie okna paczki pokazane i zamkniete.
KOLEJNOSC:
- K0 podstawa (punkty 1-7) + klocek UKRYCIE (nie dla wladcow) + paczka 1.
- Przed paczka 2: WYBOR3 i ZADANIE (nr 14, 17, 20).
- Wladcy, zamki, upadki: domyslnie wersja lekka; pelna tylko z numerem w KronikaFullList.
- Po kazdej paczce: test, nagrania lektora tej paczki, wgranie w osobnym kroku.

**Ryzyka:**

- Okno ROT (EventScreen/ArmStoryEvent) ma tylko 2 przyciski. 7 wydarzen potrzebuje 3 i wiecej opcji (17, 36, 39, 42, 81, 86, 116; w pelnej wersji 117). Trzeba lancucha okien albo MultiSelectionInquiry, a AutotestUi dzis tego nie obsluguje.
- Klocka UKRYCIE/POWROT dzis nie ma: 'remove' w arm_canon_moves to KillCharacterAction.ApplyByRemove (smierc). Sam (m21), Ogar (m11) i Osha (m20) sa martwi, wiec nr 82, 88-90, 98 i 101 nie maja kogo przywrocic, dopoki nie powstanie akcja hide/return.
- Ukrywanie glow rodow i wladcow psuje AI i okna ROT. Bran zostaje wladca Polnocy przy Krwawych Godach (38). Mance rzadzi Wolnymi Ludzmi (57). Cersei jest wladczynia korony (77). Daenerys dostaje Dragonstone w DanyInvasionEvent (78). Jon (29) i Davos (65) sa glowami rodow. Domyslnie wersje lekkie bez ukrywania.
- Kolizje z arm_canon_moves: m01, m02, m03, m04, m19, m22, m23 i m24 odpalaja wlasnymi wyzwalaczami. Bez przepiecia na kr: ta sama zmiana nastapi dwa razy albo w zlej kolejnosci (np. Tyrion od razu w Drugich Synach, bez Pentos).
- BastardsEvent ROT niszczy rod Snow (DestroyClan), a tsv nie ma #rescue dla Snow. Postacie dodane do rodu Jona (Sansa, Brienne, Podrick z nr 83) zgina razem z rodem.
- Smierc Stannisa (81): ROT StannisDeathEvent odpala tylko, gdy Renly zyje. Po smierci Stannisa, Shireen i Selyse w rodzie clan_empire_west_3 zostaje Melisandre (lord_1_12) i zostalaby wladczynia Smoczej Skaly. Trzeba ja przeniesc przed smiercia Stannisa, potem upadek rodu i wybor nowego wladcy.
- Kolizja z m104-wojny: wojny Essos od kalendarza moga uruchomic nr 5 i 10 jeszcze w prologu (Drogo zyje), wiec potrzebny warunek E(NedEvent done). Sojusze Reach i Dorne (19, 27, 95, 97, 100) musza miec jednego wlasciciela (CanonAlliances), inaczej powstana podwojne albo sprzeczne sojusze.
- Bledy faktow w Kronice v2 wobec danych ROT. Pies (castle_S6) nalezy do Tormunda (14). Brakuje Hostera Tully'ego (31.2), Ellarii (86, 97, 100), Olenny Tyrell (95, 97, 103; 'Olenna Cuy' to inna osoba), Slynta, Dontosa, Rodrika Cassela, Rorge'a, Edrica Storma, Gilly i Sigorna; Gendry i Hodor sa w grupie B. Arianne jest w rodzie Aegona. Asha i Yara to dwie postacie. ROT nie bierze Edmure'a do niewoli (63). Halys i Daryn Hornwood zyja (20). Warunki pisane wprost z Kroniki nigdy sie nie spelnia albo zadzialaja na zlych osobach.
- Smierci wladcow (42, 49, 81, 86, 95, 115, 116; warunkowo 100 i 112) uruchamiaja sukcesje gry i BK (glosowanie o tron, premia ROT x999). Pewny wzor jest jeden: Prologue.End, czyli ChangeClanLeaderAction przed smiercia. Bez niego tron moze dostac przypadkowy rod.
- Zamknieta ekonomia. Nagrody w zlocie, oddzialy (34, 81, 91, 96), ludzie (15, 45, 76, 109, 110, 115), jedzenie (19, 58) i przedmioty musza miec platnika albo zrodlo w ksiegach Armoury. Nr 58 nie moze uzywac sztywnych mnoznikow cen (zasada Jeffa).
- CEL-AI (14, 22, 23, 67, 104, 110) walczy ze StrategicCampaignAI i BK. Druzyna Jeora przy wrogiej Piesci wda sie w bitwy z Wolnymi Ludzmi. Domyslnie KronikaAiTargets=false.
- Zmiana wlasciciela stolic lub kluczowych zamkow w wersjach pelnych (23 castle_B1, 63 town_V2, 103 town_V5 = stolica Reach, 117 town_EW4) moze wyeliminowac krolestwo albo zlamac pozniejsze okna ROT (Krwawe Gody daja Fose Cailin Roose'owi).
- Lektor: okolo 102 tys. znakow, czyli okolo 75 tys. kredytow. Plotki na zywo zuzywaja do 1000 znakow dziennie (okolo 30 tys. miesiecznie), wiec razem moze przekroczyc 100 tys. w planie Creator. Lektor dopasowuje okno po tytule, a warianty maja ten sam tytul - potrzebny klucz. 209 plikow WAV to okolo 400 MB, a dysk C Jeffa zeruje pliki - trzeba OGG albo kontroli bloków.
- Obrazki: dla Essos jest tylko 1 sprite ROT (dany_invasion) na 18 wydarzen. Dla Doliny, Dorne, Reach i Zelaznych Wysp nie ma zadnego. 12 z 48 sprite'ow nie nadaje sie do rotacji (proporcje, wycinki, tlo, duplikaty, wyniki Bekartow). Z 23 obrazkow prologu sa na razie 3.
- Autotest prologu 230 dob dochodzi tylko do okolo 42. dnia osi opowiesci. Wiekszosci Kroniki nie da sie sprawdzic w zwyklym biegu - potrzebne testy syntetyczne i flaga przyspieszonego kalendarza okien.
- Klocek EP (N dni przed wydarzeniem ROT: 25, 38, 48, 69, 74, 79) wymaga prognozy dnia wydarzenia ROT z osi Fabuli (CrashScribe, x4.33). To odczyt miedzy modami odbiciem; bez niego okna przyjda po wydarzeniu, ktore maja zapowiadac.

**Pytania:**

- Nr 46/101: czy wygnanie Joraha to tylko spadek relacji (wtedy nr 101 nigdy nie przyjdzie), czy Jorah zostaje wedrowcem? W ROT jest glowa wlasnego rodu clan_nord_2 i panem Astaporu, wiec jako wedrowiec oznacza upadek rodu i utrate miasta. Propozycja: tylko relacja.
- Nr 86 (oraz 97 i 100): Ellarii nie ma w ROT. Czy zamach prowadzi Obara Sand (lord_3_10, jest w rodzie Martell), czy tworzymy nowa postac Ellarie? Propozycja: Obara.
- Nr 95, 97, 103: Olenny Tyrell nie ma w ROT. Czy role Olenny przejmuje nowa glowa Tyrellow (Willas lord_1_24 albo Garlan lord_1_75), a Olenna zostaje tylko w tekscie? Propozycja: tak.
- Asha (lord_2_10) i Yara (lord_2_7_1) to w ROT dwie osoby. Ktora gra role z ksiazek i serialu w nr 23, 42, 59, 87, 100 i 108? Propozycja: jedna postac w calej Kronice - Yara, bo przechodzi do serialu.
- Czy domyslnie zostajemy przy wersjach lekkich bez ukrywania wladcow i glow rodow? Dotyczy 29 (Jon), 38 (Bran), 57 (Mance), 65 (Davos), 77 (Cersei) i 78 (Daenerys). Jeff powiedzial 'wszystkie bierzemy', ale dokument Kroniki zaklada domyslnie wersje lekka.
- Nr 14: Piesc (castle_S6) w ROT nalezy do Wolnych Ludzi (Tormund). Czy druzyna Jeora ma obozowac przy wrogim zamku (bitwy), czy zwiad zostaje tylko w tekscie z zadaniem dowozu do Czarnego Zamku?
- Nr 82, 88-90, 98, 101: czy zmieniamy 'remove' na ukrycie dla Sama (m21) i Ogara (m11)? Wtedy postacie zyja ukryte i moga wrocic, a CrashScribe UC przestanie widziec ich jako usunietych.
- Nr 51: czy Tommen ma zostac krolem tylko w tekscie? ROT JoffreyEvent robi glowa rodu i wladczynia Cersei, a od tego zaleza nr 77, 95 i 115.
- Lektor: czy plan Creator (100 tys. kredytow) jest juz aktywny? Czy na miesiac nagrywania obnizyc dzienny limit plotek na zywo (LektorGossipDailyChars), zeby Kronika (okolo 75 tys.) zmiescila sie w jednym miesiacu?
- Czy Jeff dorobi kilka obrazkow do tematow bez sprite'a ROT (Essos/Meereen, Dolina, Dorne, Zelazne Wyspy)? Czy wystarczy rotacja 36 sprite'ow i obrazkow prologu, ktore przyjda?

