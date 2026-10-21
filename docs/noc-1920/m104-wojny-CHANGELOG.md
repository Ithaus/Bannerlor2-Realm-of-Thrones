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
| R | poprawki po recenzji: 8 uwag, wszystkie prawdziwe (R1-R8, sekcja nizej); build 5 modow kod 0, gen_mcm, progi na 12 logach syntetycznych | ZROBIONE (commit "m104-wojny R: poprawki po recenzji") |
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
   jaja (prolog), smoczeta (0 - 0.40 D, ok. 779 = Astapor), mlode (0.40 D) i podrosle (0.625 D, ok. 1218) - bez smoka pod siodlem, kon z jej taboru; dorosle
   (0.85 D, ok. 1656 = arena Daznaka, albo najazd) - Drogon jak w ROT. **Po recenzji (R5)**: pierwotnie mlode = dragon_green, podrosle = dragon_brown (przedmioty
   ROT o nazwach "Viserion" i "Rhaegal") - sprzeczne z ksiazka (Daenerys dosiada tylko Drogona, pierwszy raz na arenie Daznaka); teraz wersja z ksiazki, do
   potwierdzenia przez Jeffa (pytanie 5). **Zakres (R1)**: kampania z prologiem albo nowa kampania z ta DLL - biezaca kampania klasyczna Jeffa bez zmian.
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
- Po recenzji: w linii startu `| zakres (2) i (3): kampania z prologiem | nowa kampania (znacznik) | nie dotyczy (zapis sprzed m104 bez prologu - decyzja Jeffa nr 5) |`
  (stary zapis: stany doby Reach/Dorne i smoki `nie dotyczy (...)`); w opisie Essos `..., w wojnie W/N, zniszczone Z (pary)` (wojny ze strona zniszczona poza N i W);
  `Reach z korona: wojna ROT korona-Reach (FIVE_KINGS_WESTERLANDS_REACH) - wyzwalacz zdjety jak w ROT OnRenlysDeath (Renly nie zyje poza RenlyDeathEvent: ...)`;
  `krolestwo gracza (<id>) - nasz pakt Reach-Dorne|korona-Dorne wygaszony N (gracz nie jest zwiazany naszym paktem).`; stan Reach przy Dorne gracza
  `... pakt Reach-Dorne Dorne gracza - bez paktu, Reach-Dorne bez zmian (Dorne gracza)`.

## Progi (python -I tools/sprawdz_logi.py --grupa m104wojny <Armoury-*.log>)

WK-0 linia startu; WK-1 ERROR CanonWars 0 i potkniecia 0; WK-2 (prolog) latka Enforced TAK i 6 wojen essoskich bez empire_s; WK-3 wypowiedzenia Armoury tylko pary
essoskie, dzien kalendarza >= 21, przed dniem opowiesci 1, wszystkie WYPOWIEDZIANA, co najmniej 1 gdy log siega dnia 23 (chyba ze pary juz byly w wojnie);
WK-4 w prologu od dnia kalendarza 23 pary essoskie w wojnie W = N w >= 90% dob; WK-5 REACH Z KORONA najwyzej raz na sesje, po pierwszym SOJUSZ TAK kazda doba
SOJUSZ TAK (do najazdu), Reach-Dorne bez WOJNY; WK-6 od dnia zareczyn + 1 stan PAKT, bez wojny korona-Dorne; WK-7 etap smokow wedlug dnia opowiesci (D z linii,
tolerancja 1 dzien), jaja/smoczeta/mlode/podrosle bez smoka pod siodlem, dorosle na smoku (R5); WK-8 zdarzenia (INFO). Log klasyczny: WK-2..4 INFO; Renly zywy: WK-5
INFO; przed dniem zareczyn: WK-6 INFO. Po recenzji: WK-0 zakres (INFO); klucz Essos NIE - WK-2 tylko latka, WK-3/WK-4 INFO (R6); WK-3 "co najmniej 1" tylko gdy log
ma doby prologu od dnia kalendarza 23 i nie wszystkie pary w wojnie; WK-2 w prologu N + Z = 6, po starcie opowiesci N + Z <= 6 (R7); "nie dotyczy" i "krolestwo
gracza" nie uruchamiaja WK-5. Test: 12 logow syntetycznych (scratchpad sesji .../r104/logi, generator r104/gen_test2.py) - wynik w sekcji "Poprawki po recenzji".

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
  smok bez wymogu Jazdy. DragonFeed: do etapu dorosle smoki nie jedza (brak smoka w slocie - R5). Pod siodlem tylko Drogon (dragon_black), ktorego ROT uzywa od zawsze - mniejsze
  modele (75/85) pod jezdzcem juz nie wchodza (R5). **Biezaca kampania Jeffa (klasyczna z 20.10)**: po R1 bez zmian (zapis bez znacznika - "nie dotyczy");
  (2) i (3) tylko w kampanii z prologiem i w nowych kampaniach. **Cofniecie DLL ponizej m104**: najpierw klucze NIE i jedna doba (procedura w R4).
- Wspolne: osobny licznik potkniec i ERROR "CanonWars" - linie m101 (PR-1) bez zmian. Bez globalnego wylacznika.

## Autotest (do zrobienia, gdy Jeff nie gra; kopia logow Jeffa przed pierwszym biegiem)

1. 104-prolog (plik armoury-prolog.flag), >= 60 dob: m101prolog i m103sceny jak dotad (PR-5: 0 wojen Westeros-Westeros), m104wojny: WK-2 latka TAK / 6 wojen,
   od dnia kalendarza 22 linie "ESSOS - wojna ... WYPOWIEDZIANA", WK-4 W = 6/6, WK-7 etap jaja i siodlo bez smoka; KRONIKA WOJEN (CrashScribe) - pary essoskie.
2. 104-prolog do dnia >= 200 (przejscie w opowiesc): etap smoczeta + komunikat "three dragons have hatched", od dnia opowiesci 1 ROT EnforceWars dalej trzyma Essos
   (W = N), "wypowiedziane dzis przez Armoury 0".
3. 104-160 z zapisu klasycznego (kopia zapisu Jeffa): linia startu "zakres (2) i (3): nie dotyczy", siodlo Daenerys bez zmian (dragon_black), zero linii
   "smoki Daenerys -", "REACH Z KORONA", "DORNE Z KORONA" (R1); WK-5..7 INFO.
3a. 104-nowa klasyczna (nowa kampania, >= 40 dob, zapis i wczytanie w dobie 20): "zakres (2) i (3): nowa kampania (znacznik)" takze po wczytaniu (nw=1), etap
   smoczeta - Drogon zdjety, kon z taboru.
4. Probna DLL tylko do autotestu (zgoda 07.10): Renly zabity konsola PRZED RenlyDeathEvent - linia "wyzwalacz zdjety jak w ROT OnRenlysDeath" (R3), REACH Z KORONA:
   pokoj Reach-Dorne przed SOJUSZ, SK bez "[poza kanonem]", po 90 dobach sojusz trwa ("do dnia" sie przesuwa), po dniu ok. 585 bez wojny korona-Reach; progi dni
   smokow obnizone - mlode/podrosle bez smoka (kon), dorosle Drogon, kon wraca do taboru (R5).
5. bieg-bitwa.ps1 -Battles: Daenerys w etapie bez smoka (kon) i dorosla na Drogonie - bez bledow siodla (mniejsze modele po R5 nie wchodza).
6. Klucze wylaczone (MCM): Essos czeka na opowiesc (WK-2 tylko latka, WK-3/WK-4 INFO - R6); nasze pakty wygaszone; smok wraca pod siodlo
   ("klucz NIE - powrot smoka ROT") - to tez krok 2 procedury cofniecia (R4).

## Decyzje do potwierdzenia przez Jeffa

1. Daenerys poza "Essos na kalendarzu" (jej wojny z Qarthem i Dothrakami od startu opowiesci), Aegon w Essos - czy tak? (TAK dla Daenerys = jedna linia).
2. Pakt o nieagresji Reach-Dorne (trzyma pokoj z zadania) - zostawic? Twarda blokada wojny Reach-Dorne do najazdu - tylko jesli log pokaze ponowne wojny.
   (Gdy gracz rzadzi Reach albo Dorne, nasz pakt gasnie, a przy Dorne gracza nie konczymy jego wojny z Reach - R2, R8.)
3. Sojusz Reach-korona od smierci Renly'ego (a nie od okna Kroniki nr 27, 540-700) i do najazdu Daenerys - czy tak?
4. Dzien zareczyn Myrcelli = dzien opowiesci ok. 303 (os 70 x 4.33) - czy tak?
5. Smoki: po recenzji wersja z ksiazki - do areny Daznaka (ok. dnia 1656) Daenerys bez smoka pod siodlem (kon), potem Drogon; komunikaty o rosnacych smokach przy
   dniach 0 / 779 / 1218 / 1656. Czy tak, czy mniejszy smok wczesniej (wtedy jako Drogon - latka bitwy albo nowy przedmiot, nie "Viserion"/"Rhaegal")?
   Zakres: (2) i (3) dzialaja w kampaniach z prologiem i w nowych kampaniach; biezaca kampania klasyczna - bez zmian. Wlaczyc tez w niej (Drogon znika do ok. 1656,
   Reach/Dorne z korona od dni 303/534)? To jedna linia (Prologue.CanonHere). HP/ogien mlodego smoka w bitwie i autobitwie - osobny etap z testem bitwy?

## Poprawki po recenzji (8 uwag - wszystkie prawdziwe, zadna odrzucona)

Kazda sprawdzona w kodzie galezi i w dekompilacji (ROT 8.1.8 - ROTStorylineWars/ROTStorylineWar/RenlyDeathEvent w pytania-tmp/dec; Diplomacy 1.4.7 dectool).

- **R1 (wazne) - zakres (2) i (3).** Problem: obie czesci dzialaly w kazdej kampanii przy kluczach domyslnie TAK - po wgraniu biezaca kampania klasyczna Jeffa
  (z 20.10) przy pierwszym wczytaniu: Drogon zdjety do ok. dnia 779, po dniu 303 wymuszony pokoj i pakt korona-Dorne. Kod rozstrzygal pytanie 5 na TAK, a stala
  zgoda na wgranie po autotescie wpuscilaby to przed odpowiedzia Jeffa. Zmiana: `Prologue.CanonHere` = kampania z prologiem (Active) albo nowa kampania z ta DLL
  (znacznik `nw=1` w arm_m104; CanonWarsBehavior slucha `CampaignEvents.OnNewGameCreatedEvent`, ResetWars w OnGameStart zeruje go wczesniej; wczytanie - z zapisu).
  Stary zapis bez znacznika: CrownDaily i DragonsApply zachowuja sie jak przy wylaczonych kluczach (nic nie zakladaja i nic nie zdejmuja - flagi i tak puste),
  linia startu "zakres (2) i (3): nie dotyczy (...)", stany doby "nie dotyczy (...)". Kampanie z prologiem objete: prolog to kampania "wedlug ksiazki", a prosba Jeffa
  z 10:18 dotyczyla wlasnie smoka od pierwszego dnia (w prologu jaja). Opisy MCM: "Only in Prologue 298 campaigns and in campaigns started after this update".
  Wlaczenie w starych zapisach - tylko na slowo Jeffa (CanonHere => true).
- **R2 (wazne) - Dorne gracza w HoldReach.** Problem: HoldReach sprawdzal tylko korone i Reach; krok 1 co dobe konczyl wojne Reach z Dorne gracza (MakePeaceAction),
  krok 5 zakladal pakt Reach-Dorne w imieniu jego krolestwa (MakeNap bez pytania i kosztow). Zmiana: `playerDorne = PlayerRules(dorne)` - kroki 1 i 5 pominiete, stan
  "pakt Reach-Dorne Dorne gracza - bez paktu, Reach-Dorne bez zmian (Dorne gracza)"; sojusz korona-Reach jak dotad.
- **R3 (drobne) - Renly martwy poza wydarzeniem ROT.** Sprawdzone: RenlyDeathEvent.ConditionsHold/MainCheck wymaga zywych Renly'ego i Stannisa oraz Krain Burzy
  i Smoczej Skali; tylko ExecuteRenly wola ROTEvents.OnRenlysDeath -> ROTStorylineWars.OnRenlysDeath -> GetWar(FIVE_KINGS_WESTERLANDS_REACH).Trigger(false). Wpis
  Westerlands-TheReach ma StartDay 135, EndDay 0, hasTrigger i isTriggered TRUE od startu - po rozciagnieciu ok. 585 dnia ROT EnforceWars wypowiada wojne korona-Reach
  bez konca, a AllianceCampaignBehavior.OnWarDeclared zrywa sojusz. Zmiana: krok 2a w HoldReach (przed pokojem korona-Reach i sojuszem) - `UntriggerWestReach`:
  wpis o Id FIVE_KINGS_WESTERLANDS_REACH z IsTriggered = true -> Trigger(false) + FlavorText = ROTStorylineWars._reachAlliedRenly (dokladnie to, co robi ROT;
  odbiciem, opcjonalne skladowe w RotWarsReady) i linia "wyzwalacz zdjety jak w ROT OnRenlysDeath". Zwykla droga (ROT zdjal sam) - nic. Obejmuje tez smierc Renly'ego
  po dniu 585 (wczesniej doba stala na zawsze w "czeka (wojna ROT korona-Reach wymuszona)"). Wojny Reach-Dorzecze (Trigger(true) w OnRenlysDeath) NIE ruszamy -
  nowa wojna nie nalezy do naszej reguly.
- **R4 (drobne) - cofniecie DLL.** Zdjety smok nigdzie nie lezy; wraca tylko sciezka klucza NIE w tej DLL; ROT oddaje smoka tylko przy wyjsciu z niewoli z pustym
  slotem. Zmiana: procedura ponizej (tez komentarz w Prologue.Dragons.cs i Settings.cs). Po R1 dotyczy tylko kampanii z prologiem i nowych.
  **PROCEDURA COFNIECIA DLL PONIZEJ m104 (wpisac do STAN-PRAC przy wgraniu):** 1) MCM Armoury, grupa "Wars of the books": "Daenerys Dragons Grow", "Canon Reach Joins
  Crown", "Canon Dorne Joins Crown" na NIE (nasze pakty Diplomacy trwaja do ~1900 dni - po cofnieciu nie mialby ich kto wygasic); 2) jedna doba w grze - w Armoury.log
  "smoki Daenerys - doba: etap ... (klucz NIE - powrot smoka ROT) ... | siodlo X -> dragon_black" (albo "klucz NIE - smok jak w ROT", gdy nic nie bylo zdjete)
  i "klucz Canon ... NIE - nasz pakt ... wygaszony N"; 3) zapis; 4) dopiero teraz podmiana DLL. DLL cofnieta bez tej procedury: wgrac DLL m104 na jedna dobe
  z kluczami NIE (kroki 1-3), potem cofnac.
- **R5 (wazne) - kanon smokow.** Problem: etapy mlode/podrosle sadzaly Daenerys na dragon_green i dragon_brown (ROTassets.xml: "Viserion", "Rhaegal"); w ksiazkach
  dosiada tylko Drogona, pierwszy raz na arenie Daznaka (nasz komentarz sam to mowil); tekst gracza i opis MCM to utrwalaly. Zmiana (wersja z ksiazki, do potwierdzenia
  przez Jeffa - pytanie 5): `StageDragons[DYoung]` i `[DGrown]` = null - kon z taboru do 0.85 D, Drogon od 0.85 D albo najazdu; komunikaty bez smoka pod siodlem
  ("... big enough to breathe fire, but none of them is yet big enough to carry her" / "... bigger than any warhorse, but none of them has let her ride yet" /
  "Drogon is full-grown and has let Daenerys ride him. Now she rides him to war."); opis klucza w Settings.cs + gen_mcm; WK-7: mlode/podrosle bez smoka. Znika tez
  ryzyko nieprzetestowanych mniejszych modeli (75/85) pod jezdzcem - pod siodlem tylko Drogon, ktorego ROT uzywa od zawsze.
- **R6 (drobne) - WK-3 przy kluczu Essos NIE.** Zmiana: `ess_off` (linia startu "Essos na kalendarzu: klucz NIE" albo doba "(1) Essos: klucz NIE") - WK-2 tylko
  latka, WK-3 i WK-4 INFO. Ten sam prog dawal tez NIE dla sesji od dnia opowiesci 10 (cal_max >= 23 bral dni kalendarza z dob po starcie opowiesci) - "co najmniej 1"
  liczone teraz tylko, gdy log ma doby prologu od dnia kalendarza 23 i nie wszystkie pary byly w wojnie.
- **R7 (drobne) - wojny ze strona zniszczona.** Sprawdzone: ROT EnforceWars usuwa z listy wojny ze strona zniszczona (za bramka dnia opowiesci 1), w prologu zostaja.
  Zmiana: EssosText pomija f1/f2 IsEliminated (jak EssosDaily) i wypisuje je osobno ", zniszczone Z (pary)"; WK-2: linia startu w prologu N + Z = 6, po starcie
  opowiesci N + Z <= 6 (tylko pary essoskie, bez empire_s).
- **R8 (drobne) - nasz pakt, gdy gracz zostanie wladca strony.** Sprawdzone w Diplomacy 1.4.7: DeclareWarConditions zawiera NoNonAggressionPactCondition (okno
  dyplomacji gracza KingdomTruceItemVMMixin:364, DiplomacyKingdomDecisionPermissionModel:35). Zmiana: `PlayerPactsOff` w CrownDaily (po kluczach i najezdzie, przed
  Reach/Dorne): nasz pakt Reach-Dorne, gdy gracz rzadzi Reach albo Dorne, i nasz pakt korona-Dorne, gdy gracz rzadzi korona albo Dorne - ExpireNap, flaga false,
  linia "krolestwo gracza (<id>) - nasz pakt ... wygaszony N". Pakty AI i sojusz bez zmian; gdy gracz przestanie rzadzic, regula zalozy pakt od nowa.

Test progow (python -I tools/sprawdz_logi.py --grupa m104wojny): 12 logow syntetycznych - dobre: 1 prolog, 3 nowa klasyczna do najazdu (smoki wedlug R5),
5 bez linii m104, 6 prolog z kluczem Essos NIE (R6), 7 prolog z Lorathem zniszczonym w dniu 120 (R7: WK-4 165/165), 8 sesja od dnia opowiesci 10 z 5 wojnami (R7),
9 stary zapis - nie dotyczy (R1), 10 Dorne gracza (R2) - 0 NIE; zle: 2 (5 NIE), 4 (3 NIE), 11 mlode na "Viserionie" (WK-7 NIE), 12 prolog bez wypowiedzen
(WK-3 i WK-4 NIE). Build 5 modow kod 0, gen_mcm kod 0 (Armoury 2249 ustawien).

## Status

DO SPRAWDZENIA - nie wgrane, gra nie uruchamiana. Build 5 modow kod 0 (md5 bin/Release po K5: Armoury 96fffb80, CrashScribe 82395109, GrandTourney 8d97634e,
RealisticCaptivity 7cd126aa, ForgeView 48f537af; CrashScribe/RC/GT/ForgeView bez zmian w kodzie); gen_mcm kod 0 (Armoury 2249 ustawien).
Po poprawkach recenzji (R1-R8): build 5 modow kod 0 (md5 bin/Release: Armoury 3760e36c, CrashScribe bc9c2658, GrandTourney 58bbfcc2, RealisticCaptivity 2c269ce0,
ForgeView efda0504; poza Armoury bez zmian w kodzie - inne md5 to tylko nowy build); gen_mcm kod 0. Przed wgraniem: odpowiedz Jeffa na pytanie 5 (smoki z ksiazki,
zakres) i autotest wedlug planu (kroki 3 i 3a sprawdzaja R1).
