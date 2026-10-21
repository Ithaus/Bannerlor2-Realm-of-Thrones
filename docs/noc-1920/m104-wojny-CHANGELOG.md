# m104-wojny - wojny wedlug ksiazki: Essos na kalendarzu, Reach i Dorne z korona, mlode smoki Daenerys

Galaz `w-toku/m104-wojny` (baza 756e685f = m103-sceny: prolog 298 + okna scen + obrazki; WGRANIE 101 + m103-sceny).
Jeff 21.10: "Kod scen zmieniajacych swiat i wojen wedlug ksiazki - rob to teraz" (decyzje "tak, rob to"). Rozpoznanie: wlasne (dekompilacja ROT 8.1.8,
gry 1.4.8) + wyniki rownoleglego toru (journal wf_d4f0c951-09f: "wojny:A", "smoki" - wnioski zgodne, roznice opisane nizej).

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| K1 | (1) Essos na kalendarzu: klucz PrologueEssosWarsCalendar, prefiks/finalizer ROTStorylineWar.Enforced (flaga w StoryStartOf), doba Armoury w prologu, linie startu/doby, CanonWarsBehavior (arm_m104), klucze (2)/(3) w Settings | ZROBIONE (cda60892, build Armoury 0, gen_mcm) |
| K2 | (2) Reach i Dorne z korona: Prologue.Crown.cs - po smierci Renly'ego pokoj Reach-Dorne, pokoj i SOJUSZ korona-Reach (przedluzany), pakt Reach-Dorne; po zareczynach Myrcelli (dzien osi 70 x skala) pokoj i pakt korona-Dorne; pilnowane co dobe do najazdu Daenerys (pakty wygaszone) | ZROBIONE (8e0d7fec, build Armoury 0) |
| K3 | (3) mlode smoki Daenerys: Prologue.Dragons.cs - slot konia Daenerys wedlug etapu (jaja/smoczeta bez smoka - kon z jej taboru; mlode dragon_green 75; podrosle dragon_brown 85; dorosle dragon_black 105); start sesji, doba przed DragonFeed, po niewoli; komunikat gracza przy zmianie etapu | ZROBIONE (8220104e, build Armoury 0) |
| K4 | grupa tools/sprawdz_logi.py m104wojny (WK-0..WK-8) + test na 5 logach syntetycznych (dobre: 0 NIE; zle: wszystkie bledy wykryte; stary log: INFO) | ZROBIONE (21c451e4; WK-3 poprawka w K5) |
| K5 | build 5 modow (kod 0), gen_mcm (Armoury 2249 ustawien), CHANGELOG, raport; poprawki z przegladu: WK-3 bez falszywego NIE, gdy pary juz w wojnie; dzien sojuszu zastanego | ZROBIONE (ten commit) |
| AT | autotest (plan nizej) - NIE uruchamialem gry (gra moze byc w uzyciu); nic nie wgrane | DO ZROBIENIA |

## Problem

- Prolog 298 (WGRANIE 101): latka startu opowiesci (Prologue.Story) przestawia wszystkie odczyty CampaignStartTime w ROT na start opowiesci - takze wojny fabularne.
  Skutek: przez 188 dni prologu ROT nie wymusza ZADNEJ wojny fabularnej, takze 8 wojen miedzy krolestwami Essos (Essos "stoi" i czeka na egzekucje Neda).
- ROT: po smierci Renly'ego Reach (empire_w) nie zawiera sojuszu z korona (vlandia) - ROT ma tylko tekst "The Reach allied with the Westerlands after Renly's
  death" i wlacza wojny Reach-Dorzecze (+ Reach-Polnoc przez CrashScribe CanonAlliances). CanonAlliances tylko DOZWALA pare Korona + Reach. Wojna fabularna
  Reach-Dorne (feud) - ROT nie zawiera pokoju. Dorne nie ma zadnego zwiazku z korona (w ksiazce zareczyny Myrcelli z Trystanem).
- ROT: Daenerys ma doroslego Drogona od pierwszego dnia (takze w prologu, dzien -188) - w ksiazce smoki wykluwaja sie na stosie Drogo (start opowiesci) i rosna latami.

## Rozpoznanie (dekompilacja ROT 8.1.8, gry 1.4.8, Diplomacy 1.4.7)

- ROTStorylineWars.OnNewGameCreated - 27 wojen. Czas czytaja: ROTStorylineWar.Enforced (StartDay, EndDay) i bramka EnforceWars "ElapsedDaysUntilNow < 1".
  CrashScribe Fabula.WarPrefix/WarFinalizer mnozy StartDay/EndDay x FabulaTimeScale (4.33) na czas Enforced. Wojny essoskie (StartDay 5, EndDay 100): bravos-nord,
  tyrosh-lys, pentos-myr, norvos-qohor, volantis-valyrian (Aegon), qarth-empire_s (Daenerys), khuzait-empire_s (Dothrakowie-Daenerys), sarnor-ibb -> dni 22-433.
  Reach-Dorne (5/100) -> 22-433; Renly ginie ok. 520-534, wiec w chwili jego smierci ROT juz tej wojny nie wymusza (stan wojny moze trwac).
  W XML ROT zadna para essoska nie zaczyna w wojnie.
- IsWarForced (ten sam Enforced) czytaja: ROTKingdomDecisionPermissionModel.IsPeaceDecisionAllowed (blokada pokoju), CrashScribe PovertyPeace i StorylineReparations,
  Armoury PeaceHold. ROT DeclareWarActionPatch (ModCompatibility.Diplomacy) przy CallingDeclareWar przepuszcza wszystko (blokuje tylko Nocna Straz / Wolnych Ludzi).
- Pokoj krola (Prologue.Peace): zbior Realms = 11 krolestw Westeros (bez Wolnych Ludzi) - WarPrefix i CallToWarPrefix (realmWar wymaga obu stron w Realms)
  Essos NIE dotycza. Pakty prologu tylko z Westeros. Sprawdzone w kodzie.
- Gra AllianceCampaignBehavior.StartAlliance: bez CanMakeAlliance i bez sprawdzenia wojny, koniec = Now + 84 dni, od razu ProposeCallToWarAgreementDecision dla
  kazdej wojny jednej strony, w ktorej druga nie walczy (stad kolejnosc: najpierw pokoj Reach-Dorne). Prywatne UpdateAllianceEndTime tylko przedluza.
  OnWarDeclared miedzy sojusznikami - kara i EndAlliance. MakePeaceAction.Apply = ApplyInternal(..., Default) - Diplomacy go nie latuje.
- Diplomacy NAP (FormNonAggressionPactAction - ta sama droga co pakty prologu, sprawdzona w WGRANIU 101): blokuje decyzje AI o wojnie (DeclareWarConditions), NIE
  sam DeclareWarAction (ROT EnforceWars, wezwania i inne mody go omijaja).
- ROT RenlyDeathEvent: niszczy Krainy Burzy (rody do Smoczej Skaly), Reach bez zmian. CanonAlliances.RenlyGone: lord_1_9 martwy albo RenlyDeathEvent zakonczone.
- Rody: Tyrell clan_empire_west_1 (Mace lord_1_7), Martell clan_aserai_1 (Doran lord_3_1), Myrcella lord_4_10, Daenerys lord_1_14.
- Smoki (szczegoly w Prologue.Dragons.cs): przedmioty Type="Horse" (ROTassets.xml): dragon_black Drogon (body_length 105, extra_health 1000, speed 45),
  dragon_brown Rhaegal (85, 40), dragon_green Viserion (75, 35) + latajace "...2". Mission.BuildAgent: skala agenta 0.01 x body_length. Daenerys - zestaw
  ghi_bat_template_dany, Horse = dragon_black. Bitwa: ROTDragonMissionBehavior (HP = ROTSettings.DragonHealth raz przy rozstawieniu; TransformDragonForm robi nowego
  agenta z HP monstera), ogien/szarza - globalne DragonDamageScaling; autobitwa ROTCombatSimulationModel.SimulateHit - plaskie DragonDamageScaling dla kazdego
  ciosu bohatera ze smokiem w slocie 10. ROTGankBehavior.OnHeroPrisonerReleased: Daenerys z pustym slotem konia dostaje pierwszego nieskradzionego smoka
  (black, brown, green). Nasze: Mends.DragonPurge (smoki tylko Daenerys i rod gracza, NoDragonGank), DragonUnmount (lord_1_14 zostaje na smoku, smok bez wymogu
  Jazdy), DragonFeed (smok w slocie je 2 owce/dobe). U Jeffa (RealmOfThrones_v1.json 24.08, wg toru "smoki"): DragonHealth 10000, DragonDamageScaling 10.

## Decyzje (wedlug kanonu i mapy; do potwierdzenia przez Jeffa - nizej)

1. **Essos**: kalendarz dostaja wojny, w ktorych OBIE strony sa w Essos: Wolne Miasta, Lorath, Dothrakowie, Qarth, Sarnor, Ibben, Yi Ti, Wyspy Letnie i **Aegon**
   (valyrian, Mantarys - Zlota Kompania i Aegon ukryci w Essos az do inwazji; wojna Volantis-Aegon to zycie Essos). **Daenerys (empire_s, Meereen) - NIE**: w ksiazce
   jej wojny w Essos (Qarth, khalasary po smierci Drogo) zaczynaja sie na starcie opowiesci ROT; w prologu jest khaleesi khalasaru Drogo (sceny prologu: slub dnia 12,
   korona Viserysa dnia 145, zamach dnia 172) - wojna Dothrakow z Daenerys w prologu bylaby sprzeczna z ksiazka. Rownolegly tor proponowal Daenerys TAK (ziemie
   w Essos). Zmiana na TAK = dopisanie "empire_s" do EssosIds (Prologue.Wars.cs) i progu WK-2 (6 -> 8). Na kalendarzu: 6 wojen, dni kalendarza 22-433.
2. **Reach z korona** - wyzwalacz: smierc Renly'ego (jak w zadaniu, jak tekst ROT i CanonAlliances), nie okno Kroniki nr 27 (dni 540-700, Kronika jeszcze
   niezrobiona). Warunki: Reach Tyrellow, bez sojuszu ze Smocza Skala, zadna strona bez wladzy gracza. Kolejnosc: pokoj Reach-Dorne -> pokoj korona-Reach (gdy ROT nie
   wymusza) -> SOJUSZ gry -> koniec +364 dni (gdy < 182) -> pakt o nieagresji Reach-Dorne. Pilnowane co dobe do najazdu Daenerys.
   **Pakt Reach-Dorne** to dodatek do "pokoju" z zadania - jedyny sposob, zeby AI nie wypowiedzialo tej wojny od nowa nastepnego tygodnia (MakePeace co dobe
   zostaje jako zapas). Do potwierdzenia.
3. **Dorne z korona** - zareczyny Myrcelli: w ksiazce Tyrion zareczyl Myrcelle na poczatku "Starcia kroli", ona odplywa przed smiercia Renly'ego. Na osi Fabuli:
   Dorzecze w wojnie dzien 25, Renly 120 -> wybrany dzien osi **70 x skala Fabuli = dzien opowiesci ok. 303** (Kronika nr 19: 215-390). Warunki: Myrcella zywa (bez
   niej zareczyn nie bedzie), Dorne Martellow, bez gracza. Pokoj (gdy ROT nie wymusza) + **pakt o nieagresji** (kanon: Dorne formalnie przy koronie, ale nie walczy) -
   nie sojusz (CanonAlliances zerwalby Korona + Dorne przy wczytaniu; StartAlliance wciagnalby Dorne do wojen korony). Koniec: najazd Daenerys (DanyInvasionEvent) -
   nasze pakty Dorne wygaszone, bo ROT wlacza wtedy wojne Dorne-korona.
4. **Smoki** - tylko bezpieczna czesc (kampania, slot konia Daenerys), etapy wedlug dnia opowiesci, D = 450 x skala Fabuli (ok. 1949, najazd Daenerys):
   jaja (prolog) i smoczeta (0 - 0.40 D, ok. 779 = Astapor) - bez smoka, kon z jej taboru; mlode (0.40 D) - dragon_green, model 75%; podrosle (0.625 D, ok. 1218) -
   dragon_brown 85%; dorosle (0.85 D, ok. 1656 = arena Daznaka, albo najazd) - Drogon jak w ROT. ROT ma rozne przedmioty smokow o roznej wielkosci (body_length) -
   uzyte jako etapy (zadanie: "jesli ROT ma rozne jednostki/przedmioty smokow, uzyj ich jako etapow"); nazwa w grze mlodego smoka to "Viserion", podroslego "Rhaegal".
   **NIE zrobione (ryzyko, bez testu z bitwa)**: HP i sila ognia mlodego smoka w bitwie (wymaga latki Agent.RegisterBlow - goraca sciezka kazdego ciosu - i
   lapania nowych agentow z TransformDragonForm), skala w autobitwie (ROT daje plaskie DragonDamageScaling; u Jeffa 10 - mlody smok i tak nie bylby "slabszy"),
   karmienie DragonFeed wedlug wieku, wlasne modele mlodych smokow (XML, siodlo i kamera jezdzca nie skaluja sie z agentem).

## Zmiana

1. **Prologue.Wars.cs** (nowy, partial Prologue): (1) EssosIds, latka (InstallEssos z OnPrologueCampaign, raz na proces, Harmony "com.jeff.armoury.prologue"):
   prefiks EssosEnforcedPrefix (wojna essoska + Active + klucz -> [ThreadStatic] _calendarDepth++) i finalizer EssosEnforcedFinalizer (zdejmuje licznik zawsze) na
   ROTStorylineWar.Enforced; EssosDaily - dopoki dzien opowiesci < 1 (bramka ROT zamknieta): Enforced (wszystkie latki) == true i strony nie w wojnie ->
   CallingDeclareWar ROT = true, DeclareWarAction.ApplyByDefault, przywrocenie; wspolne: WStumble (wlasny licznik, ERROR "CanonWars.*"), odczyt wojen ROT odbiciem,
   WarForced (ROT IsWarForced), StoryDays, zapis arm_m104 (PackWars/UnpackWars), linia startu i doby; **CanonWarsBehavior** (OnSessionLaunched: smoki + linia startu;
   DailyTick: Essos, Reach/Dorne, linia doby; HeroPrisonerReleased: smoki).
2. **Prologue.Story.cs**: StoryStartOf - `if (_calendarDepth > 0) return model.CampaignStartTime;` (kalendarz tylko na czas Enforced wojny essoskiej).
3. **Prologue.Crown.cs** (nowy): (2) - CrownDaily, HoldReach, HoldDorne, EndCrown (najazd), StoryScale (CrashScribe Config.FabulaTimeScale odbiciem, zapas rok/84),
   RotEventState (bez pamieci doby), RenlyGone, UpdateEnd (leniwie - w klasycznej kampanii ApplyPeace sie nie wpina), MakeNap/ExpireNap (pomocnicze Diplomacy z
   Prologue.Peace: DipReady, NapOf, _mNapApply, _mExpire - bez zmian), DisableRotWar (IsEnabled = false tylko dla wpisu wymuszajacego wojne - przypadek brzegowy).
4. **Prologue.Dragons.cs** (nowy): (3) - etap, StageDragon (pomija skradzione z ROTGankBehavior._stolenDragons), BestMount (Stables.IsPlainMount, tier, wartosc),
   HarnessFits/HarnessToTrain (uprzaz innej rodziny do taboru), DragonsApply (jedyne miejsce zmiany slotu), komunikat dla gracza przy zmianie etapu (po angielsku).
5. **ArmouryBehavior.cs**: jedna linia - `Prologue.DragonsApply("doba")` przed DragonFeed.Daily. **Prologue.cs**: InstallEssos w OnPrologueCampaign, ResetWars w
   ResetCampaign. **SubModuleMain.cs**: `starter.AddBehavior(new CanonWarsBehavior())` po PrologueBehavior.
6. **Settings.cs** (+ gen_mcm, McmSettings.cs): PrologueEssosWarsCalendar (grupa "Prologue 298"); CanonReachJoinsCrown, CanonDorneJoinsCrown, DaenerysDragonsGrow
   (nowa grupa "Wars of the books"). Wszystkie domyslnie true, czytane na zywo. W Armoury.json Jeffa ich nie ma (sprawdzone grep).
7. **tools/sprawdz_logi.py**: grupa m104wojny (osobny blok przed m100kryjowki, opis w pomocy, dyspozycja).
CrashScribe, RealisticCaptivity, GrandTourney, ForgeView - bez zmian w kodzie (build kontrolny).

## Linie logu (Armoury.log, "Wojny kanonu (m104): ")

- `latka wojen essoskich (<why>): ROTStorylineWar.Enforced TAK|BRAK|BLAD.` - raz na proces, przy pierwszej kampanii z prologiem.
- `start sesji | prolog TAK|NIE | dzien opowiesci X | (1) Essos na kalendarzu: klucz, latka Enforced, na kalendarzu 6 (pary), w wojnie W/6, bramka ROT ... |
  (2) Reach i Dorne z korona: klucze, Diplomacy, koniec sojuszu (UpdateAllianceEndTime), Renly, Reach z korona: ..., Dorne z korona: ..., najazd Daenerys na osi dnia N |
  (3) smoki Daenerys: klucz, etap E | siodlo S | nastepny etap od dnia opowiesci N (D=1948) | potkniecia N.`
- `doba | dzien opowiesci X (dzien kalendarza N) | (1) Essos: ..., wypowiedziane dzis przez Armoury D [pary] (razem R) | (2) Reach: <stan> | Dorne: <stan> |
  (3) smoki Daenerys: ... | potkniecia dzis A (razem B).` - w prologu (dzien opowiesci < 1, klucz Essos) co dobe; potem przy zdarzeniu albo co 7 dob opowiesci.
- `ESSOS - wojna a-b WYPOWIEDZIANA|NIE weszla w prologu (dzien kalendarza N, dzien opowiesci X) - wojna fabularna ROT na kalendarzu.`
- `REACH Z KORONA (dzien opowiesci X, po smierci Renly'ego): SOJUSZ korona-Reach ZALOZONY (do dnia opowiesci Y) | pakt Reach-Dorne ... [kroki] | wnioski wezwan ...`
- `Reach z korona: sojusz ODNOWIONY ...` / `Reach z korona: pilnowanie ... [ponowny pokoj Reach-Dorne; koniec sojuszu przesuniety ...]` / `Reach z korona: SOJUSZ NIE wszedl`
- `DORNE Z KORONA (dzien opowiesci X, zareczyny Myrcelli z Trystanem - dzien osi N): PAKT o nieagresji korona-Dorne ZALOZONY (Diplomacy, D dni) ...` / `Dorne z korona: pilnowanie ...`
- `NAJAZD DAENERYS (DanyInvasionEvent ruszyl, dzien opowiesci X) - nasze pakty Dorne wygaszone N, sojusz Reach-korona bez pilnowania ...`
- `klucz Canon Reach Joins Crown NIE - nasz pakt Reach-Dorne wygaszony N ...` / `klucz Canon Dorne Joins Crown NIE - ...`
- `smoki Daenerys - <start sesji|doba|po niewoli>: etap E (dzien opowiesci X) | siodlo A -> B (kon z taboru|kon do taboru|uprzaz ... do taboru)` /
  `... | czeka: kon ... nie ma dokad wrocic (Daenerys bez wlasnej partii)`
- Bledy: `ERROR in CanonWars.<miejsce> (potkniecie N)` (pierwsze 5 na proces), licznik w linii startu i doby.

## Progi (python -I tools/sprawdz_logi.py --grupa m104wojny <Armoury-*.log>)

WK-0 linia startu; WK-1 ERROR CanonWars 0 i potkniecia 0; WK-2 (prolog) latka Enforced TAK i 6 wojen essoskich bez empire_s; WK-3 wypowiedzenia Armoury tylko pary
essoskie, dzien kalendarza >= 21, przed dniem opowiesci 1, wszystkie WYPOWIEDZIANA, co najmniej 1 gdy log siega dnia 23 (chyba ze pary juz byly w wojnie);
WK-4 w prologu od dnia kalendarza 23 pary essoskie w wojnie W = N w >= 90% dob; WK-5 REACH Z KORONA najwyzej raz na sesje, po pierwszym SOJUSZ TAK kazda doba
SOJUSZ TAK (do najazdu), Reach-Dorne bez WOJNY; WK-6 od dnia zareczyn + 1 stan PAKT, bez wojny korona-Dorne; WK-7 etap smokow wedlug dnia opowiesci (D z linii,
tolerancja 1 dzien), jaja/smoczeta bez smoka, mlode/podrosle/dorosle na smoku; WK-8 zdarzenia (INFO). Log klasyczny: WK-2..4 INFO; Renly zywy: WK-5 INFO;
przed dniem zareczyn: WK-6 INFO. Test na 5 logach syntetycznych (scratchpad m104/logi): dobre 0 NIE, zle - kazdy blad wykryty, stary log INFO.

## Ryzyko / co sprawdzic (kontrola calosci - w kodzie)

- **Regresje (1)**: StoryStartOf zmienia wynik tylko przy _calendarDepth > 0, a ustawia go wylacznie prefiks Enforced wojny essoskiej (finalizer zdejmuje zawsze, takze
  po wyjatku; prefiks nie pomija oryginalu - kolejnosc z Fabula.WarPrefix bez znaczenia, jej finalizer przywraca daty niezaleznie). ScanPatch bez zmian - PR-2 m101
  (30/30 metod, 34 miejsca) bez zmian. Kampania klasyczna: latka Essos nie wpieta w procesie bez kampanii z prologiem; w procesie z nia - prefiks wychodzi na Active.
- **Kolizje (1)**: IsWarForced par essoskich w prologu prawdziwe w dniach kalendarza 22-433 -> ROT blokuje pokoj, PovertyPeace ich nie proponuje, StorylineReparations
  zeruje reparacje, PeaceHold - jak w klasycznym starcie (spojne). Wojny wypowiadane z CallingDeclareWar (jak ROT). Pokoj krola i pakty prologu - bez wplywu (Realms).
  Kronika (projekt) nr 5 "Stos khala Drogo, dni 22-60 gdy rusza wojna Hordy Dothrakow" - wojna Dothrakow z Daenerys zostaje na zegarze opowiesci, wiec bez kolizji.
- **(2)**: StartAlliance sklada wnioski wezwan do wojny dla wojen obu stron (korona wezwie Reach np. przeciw Polnocy/Dorzeczu, Reach korone przeciw Dorzeczu/Polnocy/
  Smoczej Skale) - rozlicza je latka 168 CrownCallToWar; korona moze tez wezwac Reach do wojny AI z Essos. Pokoj Reach-Dorne idzie PRZED sojuszem. Pakt Diplomacy nie
  blokuje DeclareWarAction - wezwania i inne mody moga go ominac; wtedy doba robi pokoj Reach-Dorne od nowa (linia "ponowny pokoj") - jesli w logu bedzie tego duzo,
  twarda blokada (prefiks DeclareWarAction) do decyzji. Sojusz przedluzany przez prywatne UpdateAllianceEndTime (odbicie) - przy BRAK w linii startu sojusz wygasnie
  po 84 dniach i doba zalozy go od nowa (linia ODNOWIONY, nowe wnioski wezwan). Klucz wylaczony: nasze pakty wygaszone, sojusz zostaje do swojego terminu (do +364 dni).
  Stary zapis bez arm_m104 po smierci Renly'ego / po dniu 303 - sojusz i pakt zalozone w pierwszej dobie po wczytaniu (zamierzone).
  Warunki po id (lord_1_9, lord_4_10, clan_empire_west_1, clan_aserai_1) - inna wersja ROT albo przenosiny rodow: stan "czeka (...)" w linii doby mowi powod.
  CrashScribe CanonAlliances: Korona + Reach dozwolone po smierci Renly'ego (ta sama regula RenlyGone) - zrywanie przy wczytaniu go nie ruszy; Dorne ma TYLKO pakt.
- **(3)**: zmienia sie tylko slot konia zestawu bojowego Daenerys. Kon bierze sie z taboru partii, ktora prowadzi, i do niego wraca (zamknieta ekonomia; smoki to nie
  towar - DragonPurge i tak je czysci z obiegu); A17Diag "Konie wedlug ramek" zobaczy -1/+1 konia poza ramkami przy zmianie etapu (rzadko). ROT po niewoli daje smoka
  przy PUSTYM slocie - nasz sluchacz HeroPrisonerReleased (rejestrowany po ROT, gdy Armoury laduje sie po ROT) zdejmuje go od razu; przy innej kolejnosci - najwyzej
  do nastepnej doby. Uprzaz innej rodziny niz nowy wierzchowiec idzie do taboru (AccessViolation w AddMountMesh). DragonUnmount trzyma lord_1_14 na kazdym dragon_*,
  smok bez wymogu Jazdy. DragonFeed: smoczeta nie jedza (brak smoka w slocie), mlody smok je jak dorosly (bez zmian w DragonFeed). Mniejsze smoki (75/85) w bitwie:
  siodlo/kamera jezdzca (adders monstera) sie nie skaluja - ROT daje te przedmioty Daenerys po kradziezy, ale w bitwie trzeba to zobaczyc (bieg-bitwa.ps1 -Battles).
  **Biezaca kampania Jeffa (klasyczna z 20.10)**: przy pierwszym wczytaniu Daenerys traci Drogona (smoczeta) do ok. dnia opowiesci 779 - zgodne z zamyslem, widoczne.
- Wspolne: osobny licznik potkniec i ERROR "CanonWars" - linie m101 (PR-1) bez zmian. Bez globalnego wylacznika.

## Autotest (do zrobienia, gdy Jeff nie gra; kopia logow Jeffa przed pierwszym biegiem)

1. 104-prolog (plik armoury-prolog.flag), >= 60 dob: m101prolog i m103sceny jak dotad (PR-5: 0 wojen Westeros-Westeros), m104wojny: WK-2 latka TAK / 6 wojen,
   od dnia kalendarza 22 linie "ESSOS - wojna ... WYPOWIEDZIANA", WK-4 W = 6/6, WK-7 etap jaja i siodlo bez smoka; KRONIKA WOJEN (CrashScribe) - pary essoskie.
2. 104-prolog do dnia >= 200 (przejscie w opowiesc): etap smoczeta + komunikat "three dragons have hatched", od dnia opowiesci 1 ROT EnforceWars dalej trzyma Essos
   (W = N), "wypowiedziane dzis przez Armoury 0".
3. 104-160 z zapisu klasycznego (kopia zapisu Jeffa): WK-7 smoczeta (Drogon zdjety, kon z taboru), WK-5/WK-6 INFO albo DORNE Z KORONA po dniu 303.
4. Probna DLL tylko do autotestu (zgoda 07.10): Renly zabity konsola albo przesuniety dzien - REACH Z KORONA: pokoj Reach-Dorne przed SOJUSZ, SK bez "[poza kanonem]",
   po 90 dobach sojusz trwa ("do dnia" sie przesuwa); progi dni smokow obnizone - zmiana siodla mlode/podrosle/dorosle i kon wraca do taboru.
5. bieg-bitwa.ps1 -Battles z Daenerys na dragon_green / dragon_brown (wyglad, siodlo, lot) - przed wgraniem (3).
6. Klucze wylaczone (MCM): Essos czeka na opowiesc; nasze pakty wygaszone; smok wraca pod siodlo ("klucz NIE - powrot smoka ROT").

## Decyzje do potwierdzenia przez Jeffa

1. Daenerys poza "Essos na kalendarzu" (jej wojny z Qarthem i Dothrakami od startu opowiesci), Aegon w Essos - czy tak? (TAK dla Daenerys = jedna linia).
2. Pakt o nieagresji Reach-Dorne (trzyma pokoj z zadania) - zostawic? Twarda blokada wojny Reach-Dorne do najazdu - tylko jesli log pokaze ponowne wojny.
3. Sojusz Reach-korona od smierci Renly'ego (a nie od okna Kroniki nr 27, 540-700) i do najazdu Daenerys - czy tak?
4. Dzien zareczyn Myrcelli = dzien opowiesci ok. 303 (os 70 x 4.33) - czy tak?
5. Smoki: etapy na przedmiotach ROT (mlody = "Viserion", podrosly = "Rhaegal", dorosly = Drogon) i progi 779 / 1218 / 1656 - czy tak? Czy wlaczyc tez w biezacej
   kampanii (Drogon znika do ok. 779), czy tylko w nowych (znacznik w zapisie)? HP/ogien mlodego smoka w bitwie i autobitwie - osobny etap z testem bitwy?

## Status

DO SPRAWDZENIA - nie wgrane, gra nie uruchamiana. Build 5 modow kod 0 (md5 bin/Release po K5: Armoury 96fffb80, CrashScribe 82395109, GrandTourney 8d97634e,
RealisticCaptivity 7cd126aa, ForgeView 48f537af; CrashScribe/RC/GT/ForgeView bez zmian w kodzie); gen_mcm kod 0 (Armoury 2249 ustawien).
