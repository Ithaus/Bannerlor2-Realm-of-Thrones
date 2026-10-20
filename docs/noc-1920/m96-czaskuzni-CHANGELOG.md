## m96-czaskuzni (20.10) - ekran kuzni = jedna robota, po wyjsciu zawsze jeden pasek na caly czas (Armoury)

**Dla Jeffa (prosto).** Ekran kuzni to teraz jedna robota: wszystko, co na nim zrobisz (wytop, przekucie metalu, miecze, zbroje, luki, zlecenia),
dopisuje swoje godziny do jednego licznika, a napis "Hours spent for all actions" pokazuje ich sume. Po wyjsciu z ekranu (Done albo Esc) zawsze
otwiera sie jeden pasek na caly ten czas i leci dokladnie tyle godzin, ile pokazal napis (23 h na ekranie = 23 h paska): najpierw wytop i przekucie
(metal masz od razu, wiec od tych godzin nie mozna odejsc - jak dawniej z paska BK), potem sztuki schodza z kowadla po kolei. Od sztuk mozesz odejsc
w kazdej chwili ("Step away"): masz to, co gotowe, a reszta naprawde czeka w tej kuzni - takze gdy zostajesz w miescie (karczma, targ) - i rusza
dopiero, gdy znow staniesz przy kowadle (ekran kuzni i Done albo "Stay until the work is done"). Noca (23-5) kuznia stoi, pasek czeka do rana;
oblezenie albo bitwa pod murami przerywa pasek. Wylacznik w MCM: "Forge One Job" (grupa "Time at the anvil"), wylaczony = jak dotad.

**Problem.** Jeff 20.10 (gra na 95a): "jak kuje bron, to jestem w menu z paskiem, a teraz czasami jestem, czasami nie jestem; pokazuje mi, ze to bedzie
trwalo 17.8 godzin; ma liczyc calkowity czas: jak wytapiam, potem kuje miecz i wychodzi mi 23 godziny, to odpala sie pasek i leci 23 godziny; jak wyjde
i nie skoncze kucia, to biore tylko to, co wykulem, a reszta czeka, az wroce."

**Dowod (logi Jeffa `D:\Backup-Bannerlord\logi-jeffa\2026-10-20`, linia `Jeden zegar kuzni:` przy kazdym zamknieciu ekranu przyciskiem Done).**
- Sam wytop - pasek BK = ekran: `[14:38:29] ekran BK 17.5 h, z tego w kolejce 0.0 h - odsiadka BK za przetop / rafinacje 17.5 h` (The Eyrie;
  tak samo 16.0 / 13.3 / 18.3 / 17.7 / 16.7 h rano) - to jest "17.8 godzin" z ekranu, ktore Jeff widzi i ktore pasek odlicza.
- Wytop + miecze - pasek krotszy niz ekran, miecze w tle: `[11:09:07] ekran BK 14.8 h, z tego w kolejce 6.0 h - odsiadka 8.8 h` (Seagard),
  `[11:42:04] ekran BK 11.7 h, z tego w kolejce 10.0 h - odsiadka 1.7 h` (Harrenhal, 4 miecze - pasek 1.7 h przy 11.7 h na ekranie),
  `[14:43:40] ekran BK 12.8 h, z tego w kolejce 7.3 h - odsiadka 5.5 h` (The Eyrie): `[14:43:29] Projekt dodany: crafted_item_10|0.15278|...|van`
  i zaraz `[14:43:43] Dostawa broni: crafted_item_10`, `[14:43:48] ... crafted_item_11` - sztuki kuly sie ROWNOLEGLE z paskiem BK, a po jego
  koncu reszta kolejki szla bez paska (gdy gracz stoi w miescie).
- Wyjscie klawiszem Esc: zadnej linii i zadnego paska (BK `CloseWithWait` siedzi tylko na przycisku).

**Przyczyna (kod, dekompilacja BK i gry 1.4.8).**
- Dwa zegary (wpis 83 + poprawka 15.10 w `ForgeClock`): kute sztuki (bron vanilla `OnNewItemCrafted` "van", zbroja CRAFT `ForgeClock.CraftPost` "bk",
  luki/amunicja `Forge.Smith`) ida do kolejki kowala (`ArmouryBehavior._projects`), a `ForgeClock.SkipBkWait` (prefiks BK `StartCraftingMenu`) zostawia BK
  odsiadke tylko za RESZTE (wytop, rafinacja, zlecenia, spartaczone). Pasek BK (`bannerkings_wait_crafting`) pokazuje wiec reszte, kolejka sztuk tyka w tym
  czasie obok (AdvanceProjects co godzine, gracz w osadzie), a nasz pasek `arm_project_wait` otwieral sie tylko, gdy reszty nie bylo (<= 0.05 h).
- `BannerKings.UI.Extensions.CraftingMixin.CloseWithWait` jest podpiety tylko pod przycisk (`CraftingCancelButtonPatch`); klawisz Esc
  (`GauntletCraftingScreen.OnFrameTick` -> `CraftingVM.ExecuteCancel`) zamyka ekran bez `StartCraftingMenu` - bez paska, a licznik `_bkQueued` zostawal
  na nastepna wizyte i zjadal godziny wytopu (krotszy albo zaden pasek nastepnym razem).
- BK liczy godziny ze staminy SAMEGO gracza (`spentStamina = startingStamina - stamina gracza`, `GetSpentHours = spentStamina / 6`); praca towarzysza przy
  kowadle = 0 h, a przy 0 h `CloseWithWait` nie wola paska wcale.
- Kolejka kowala tyka w kazdym menu osady (`AdvanceProjects`: warunek tylko "gracz w tej osadzie"), godzina po godzinie tylko czolo kolejki, a ulamek
  godziny po skonczonej sztuce przepadal - pasek trwal dluzej niz suma (Eyrie: ekran 12.8 h, takty 6 + 4 + 4 = 14).

**Zmiana.** Nowy plik `Armoury/src/ForgeJob.cs` + male wpiecia; klucz MCM `ForgeOneJob` (domyslnie TAK; dziala razem z `ForgeOneClock`).
- JEDNA ROBOTA = zwykla kolejka kowala tej osady (`_projects`, jeden zegar po kolei, zapis `arm_projects` jak dotad). Otwarcie ekranu (postfiks
  konstruktora `CraftingVM`): stamina wszystkich kowali z listy ekranu (`AvailableCharactersForSmithing` = bohaterowie druzyny). Kazda sztuka z ekranu
  (`ForgeClock.NoteQueued` - jedno miejsce dla broni, zbroi, lukow, amunicji) dopisuje swoje godziny z kolejki i z miary BK. Wyjscie z ekranu (postfiks
  `CraftingVM.ExecuteCancel` - przycisk Done przez BK i klawisz Esc; tylko gdy `CraftingState` naprawde zszedl ze stosu): praca bez sztuki =
  zuzyta stamina wszystkich / 6 minus miara BK sztuk - wpis "praca bez sztuki" (`~prep|dni|1|osada|prep||1`) NA CZELE otwartej roboty tej kuzni
  (za wczesniejsza praca bez sztuki, przed wszystkimi sztukami, takze tymi sprzed wizyty), potem `SwitchToMenu("arm_project_wait")` na laczny czas tej
  kuzni. Odsiadka BK pominieta (`SkipBkWait` -> false, `ForgeJob.TookOverBkWait`). Napis ekranu "Hours spent for all actions" = godziny tej wizyty,
  ktore odliczy pasek (postfiks BK `GetSpentHours`).
- PASEK `arm_project_wait` (ten sam co "Stay until the work is done"): napis po angielsku z lacznym czasem, ile z tego sprzed wizyty, ile wytopu na
  poczatku, noc, odejscie, brak pieniedzy. Koniec paska i "Step away" wracaja do menu, z ktorego otwarto ekran (np. podmenu kuzni m95), inaczej do
  "Work the forge". Doba kuzni placona przy otwarciu paska (`Forge.PayDayRent` / `DayPass.EnsureBought` - jeden rejestr; przy Done dalej
  `DayPassPrefix`, drugi raz nie pobiera), takze po Esc.
- ROBOTA GRACZA TYLKO NA PASKU (`ForgeJob.HandsAt`, wpiecie w `AdvanceProjects`): przy kluczu i `ForgeOnlyWhileThere` (u Jeffa) kazdy wpis tej kuzni
  (praca bez sztuki, sztuki z ekranu, wlasne projekty z "Forge armour, shields and tack") idzie naprzod tylko, gdy biezace menu to `arm_project_wait`
  w tej osadzie. Po "Step away", w karczmie, na targu, w "Wait here" - stoi. Przy kowalu pracujacym bez ciebie (`ForgeWorksWithoutYou` bez
  `ForgeOnlyWhileThere`, nie u Jeffa) sztuki ida jak dotad, praca bez sztuki czeka na gracza i nie blokuje sztuk za soba.
- GODZINA TAKTU NA NASTEPNY WPIS (`AdvanceProjects`, tylko przy kluczu): gdy wpis konczy sie w trakcie godziny, reszta tej godziny idzie od razu na
  nastepny wpis tej kuzni - pasek trwa tyle, ile laczny czas (zaokraglony raz, w gore do pelnej godziny), nie suma zaokraglen kazdej sztuki.
  XP za godzine pracy bez zmian (XP za przepracowana czesc godziny).
- WYNIK: sztuka wychodzi, gdy pasek ja przejdzie (`Forge.Deliver` jak dotad). Material z wytopu i rafinacji - od razu w sakwach, jak dotad (ekran
  potrzebuje go do nastepnego kucia; bezpieczniejsze niz trzymanie metalu w kuzni pod otwartym ekranem); w kolejce stoja tylko jego godziny,
  bez wydania i bez XP (XP za wytop dal ekran). Dlatego od pracy bez sztuki odejsc nie mozna (`ForgeJob.HoldAtAnvil` w opcji "Step away":
  komunikat "The smelting and refining come first ... About N more hours at the anvil"), jak z dawnego paska BK, ktory nie mial zadnego wyjscia.
  Bezpieczniki, zeby nikt nie utknal: odejscie wolne, gdy gracza nie stac na dobe kuzni (robota i tak stoi) albo gdy pasek stoi 2.5 h dnia bez
  postepu (noc 23-5 sie nie liczy). Od sztuk mozna odejsc zawsze.
- ODEJSCIE przed koncem ("Step away" od sztuk, oblezenie, bitwa, inne menu, wyjazd): gotowe sztuki juz w sakwach, reszta (sztuki w toku i ewentualna
  praca bez sztuki) czeka w tej kuzni i idzie dalej po powrocie - od miejsca przerwania, tylko na pasku.
- PRZERWANIA (`ForgeJob.Interrupt` w `ProjWaitTick`, wzor `NightRest.SettlementInterrupt`): oblezenie osady albo bitwa (druzyny lub osady) - menu ze
  stanu gry (`GetGenericStateMenu`, przy oblezeniu najwyzej `menu_siege_strategies`), `SwitchToMenu` z ticku (wzor vanilli), komunikat "Trouble at the
  walls - you leave the forge ...". Armii nie ruszamy (gra sama prowadzi gracza w cudzej armii).
- NOC: jedna zasada dla calej roboty, ta co dotad dla sztuk (`WorkshopNightRest`, Jeff 05.10 "noc - tak"): 23-5 kuznia stoi, pasek czeka do switu,
  czyli kowal pracuje 5-23 (18 h); rytm snu przy kowadle (AnvilShift) bez zmian. Dotad pasek BK za wytop lecial tez noca.
- NAPISY: spis "Look in on the work in hand" (praca bez sztuki "Bench work (smelting, refining, orders) at X", dopisek "goes on only while you wait
  at the anvil", bez tempa dla pracy bez sztuki), komunikat po odlozeniu sztuki na lawe (ForgeClock.Queue i bron "van"), okno "How will you work?"
  i "At the Forge" - mowia "only while you wait at the anvil" zamiast "while you stay in this settlement" (tylko przy kluczu).
- LOG (`Armoury.log`, prefiks `Robota w kuzni (m96)`): przy starcie gry - wpiete latki i zasady; przy wyjsciu z ekranu - godziny wizyty (praca bez sztuki
  i sztuki, miara BK, stamina kowali), robota sprzed wizyty, razem w kuzni, pasek TAK/NIE, doba kuzni; przy koncu paska / odejsciu / przerwaniu - ile
  zostalo, odmowy odejscia; w kazdej linii: roboty gracza w sesji gry (godziny lacznie, czynnosci, paskow do konca, odejsc, przerwanych) i czekajace
  w kuzniach (godziny, sztuki w toku, gotowe, wpisy bez sztuki).
- Wpiecia: `Project.cs` (`IsPrep`, `Valid`, `Name`, zapis id `~prep`), `ArmouryBehavior.cs` (spis, AdvanceProjects: bramka paska, reszta godziny,
  XP x czesc godziny; CollectReadyProjects, HasProjectsHere, ProjectHoursLeftHere, HandOver, `ProjectLines`, Reset, napis broni "van"),
  `ForgeClock.cs` (1 linia w NoteQueued, 1 w SkipBkWait, `ResetBkQueued`, napis w Queue), `SmithMenu.cs` (napis paska, powrot, linia logu, odmowa
  "Step away", przerwanie, zastoj, 3 napisy), `SubModuleMain.cs` (ApplyAll), `Settings.cs` + `McmSettings.cs` (gen_mcm).

**Poprawki po recenzjach (gracz + technika, 20.10).**
- [wazne, obie] "Po Step away robota nie czeka, tyka w miescie bez paska" - PRAWDA (`AdvanceProjects`: jedyny warunek `here == p.SettlementId`;
  log 14:43:29-14:43:48 pokazuje sztuki schodzace obok paska). Poprawione: `ForgeJob.HandsAt` - robota gracza tylko na pasku (patrz Zmiana).
- [wazne gracz, drobne technika] "Pasek dluzszy niz napis - ulamek godziny przepada" - PRAWDA (`busyForge` jeden wpis na kuznie na godzine, ujemna
  reszta wyrzucana; Eyrie 12.8 h -> 14 taktow). Poprawione: reszta godziny przechodzi na nastepny wpis tej kuzni w tym samym takcie.
- [wazne, obie] "Metal od razu + Step away = godziny wytopu do ominiecia" - PRAWDA (opcja zawsze czynna, dlug zostawal tylko w tej kuzni).
  Poprawione: praca bez sztuki na czele roboty tej kuzni i bez odejscia (`HoldAtAnvil`), jak dawny pasek BK; bezpieczniki pieniadze / zastoj.
- [drobne] "Praca bez sztuki jednym wpisem przed sztukami, nie czynnosc po czynnosci" - PRAWDA, ale ZOSTAWIONE celowo: metal z wytopu jest w sakwach
  od pierwszej chwili, wiec jego godziny musza isc pierwsze i bez odejscia; przy przeplataniu (wytop po mieczu) od mieczy dalo by sie odejsc przed
  wytopem albo trzeba by blokowac odejscie az do ostatniego wytopu. Laczny czas ten sam; wpis idzie teraz na czolo calej roboty tej kuzni
  (takze przed sztukami sprzed wizyty), zeby z wytopu nie dalo sie wymknac za starymi sztukami.
- [drobne] "Pasek nie reaguje na oblezenie / bitwe" - PRAWDA (stan sprzed zmiany). Poprawione: `ForgeJob.Interrupt` (bez armii - opis wyzej).
- [drobne] "Pusta sakiewka - pasek stoi bez konca, metal bez placenia" - PRAWDA. Poprawione: doba kuzni placona przy otwarciu paska (takze Esc),
  napis paska mowi "You cannot pay the forge hire - the work waits", odejscie wtedy wolne. Zostaje: gracz z mniej niz doba kuzni (ok. 3 d) dostaje
  metal z wytopu bez zaplaty (dotad pasek BK bral godzinowke, ile bylo w sakiewce) - przypadek brzegowy, opisany w Ryzyku.
- [drobne] "Spis: dopisek zegara i tempo przy pracy bez sztuki; prep blokuje sztuki przy kowalu bez ciebie" - PRAWDA. Poprawione: `ForgeJob.ClockNote`
  (bez tempa przy pracy bez sztuki) i wpis prep pomijany przed `busyForge`, gdy rece gracza nie pracuja (tylko tryb bez `ForgeOnlyWhileThere`).

**Ryzyko / co sprawdzic (kontrola calosci).**
- `SwitchToMenu` z postfiksu `ExecuteCancel` - ta sama chwila, w ktorej BK otwiera swoj pasek (`CloseWithWait`: `ExecuteCancel`, potem
  `StartCraftingMenu` -> `SwitchToMenu`); NIE z opcji menu oczekiwania (pulapka CLAUDE.md). `PopState` gry jest natychmiastowy
  (`GameStateManager.DoGameStateJobs`), `CurrentMenuContext` bierze MapState. Bez menu gry - pasek nie startuje (robota czeka, log "brak menu gry").
  Sciezki Esc (wolanie z `OnFrameTick` ekranu) nie widzialem jeszcze w grze. Opcja "Step away" przy odmowie tylko pisze komunikat i wznawia czekanie
  (bez `SwitchToMenu`); przerwanie przelacza z ticku menu (wzor vanilli i NightRest).
- Robota gracza tylko na pasku: sztuki i wlasne projekty z zapisu 95a (np. kolejka w Harrenhal) po wgraniu nie ida juz w menu miasta - trzeba stanac
  przy kowadle ("Work the forge" -> "Stay until the work is done"). Gotowe dalej odbierane przy wejsciu do osady. Zlecenia lordow (terminy) - czas plynie,
  wiec robota pod zlecenie musi byc odsiedziana na pasku.
- Wytop zawsze odsiadywany: wytop wieczorem = noc przy kowadle (23-5 stoi, AnvilShift klada spac), dopiero potem sztuki i odejscie. Logi 20.10:
  16-20 h wytopu na wizyte, czyli ok. doba przy kowadle - tyle samo co dawny pasek BK (on lecial tez noca, wiec byl krotszy w kalendarzu).
- Reszta godziny: przy kluczu wpisy koncza sie do 1 h szybciej niz dotad (bez zaokraglania kazdego w gore); XP za czesc godziny - suma XP sztuki
  najwyzej o ulamek godziny mniejsza niz dotad (dotad ostatnia czesciowa godzina dawala XP za pelna). Bez klucza krok zawsze 1 h jak dotad.
- Wszystkie odczyty `_projects` przejrzane: spis, sufit broni "van" (pomija wpis), AdvanceProjects (wpis bez sztuki: tylko na pasku, noc, doba kuzni
  jak sztuki, bez `Recipes.For(null)` i bez XP), CollectReadyProjects i HandOver (nic do wydania), HasProjectsHere / ProjectHoursLeftHere (licza wpis),
  `ForgeClock.Queue` (suma kolejki w komunikacie), `SkipBkWait` (stara galaz tylko bez klucza). Innych czytelnikow brak (grep).
- Cofniecie DLL: starszy DLL nie znajdzie przedmiotu `~prep` (`MBObjectManager.GetObject` = null) i wyrzuci wpis po cichu - przepadaja tylko godziny pracy
  bez sztuki w toku; sztuki zostaja. Nowy DLL czyta stary zapis bez zmian (brak wpisow `~prep`).
- Towarzysz przy kowadle: jego stamina tez liczy godziny (BK liczyl tylko gracza) - napis na ekranie moze byc wiekszy niz dotad.
- Cena: doba kuzni jak dotad (`Forge.PayDayRent` / `DayPass` - jeden rejestr), teraz pobierana przy otwarciu paska. Godzinowka BK nie leci (nie ma
  paska BK). Materialy i XP bez zmian. Zlecenia gry (CraftingOrder): sztuka do zlecenia od razu jak dotad, jej godziny w pracy bez sztuki (dotad w pasku
  BK). Cech (GuildMend, rece kowali miasta - wlasny zegar, nie `_projects`) i platnerz w taborze (poza osada - ForgeJob nie startuje) bez zmian.
  `ForgeLikeTownSmith` (C5): sztuki z godzinami C5, napis ekranu tez (jedna miara). Pusta sakiewka: metal z wytopu bez zaplaty (brzeg, < doba kuzni).
- Stamina w pasku: nie regeneruje (`InForgeMenu` zawiera `arm_project_wait`); poza paskiem regeneruje, ale robota wtedy stoi (spojne).
- Zastoj 2.5 h: postep przychodzi co pelna godzine taktu; jesli gra pominie takt godzinowy dluzej (np. bardzo wolny czas), "Step away" odblokuje sie
  wczesniej - tylko luzuje odmowe, nic nie psuje.
- m96-wzory / m96-jakosc: wpiecia w `ForgeClock` tylko po 1 linii (NoteQueued, SkipBkWait, napis Queue); `CraftPost`, `OnNewItemCrafted` (poza napisem),
  jakosc i wzory nietkniete; galezie m96 nie ruszaja `AdvanceProjects` ani `ProjWaitTick` (git diff 848b2e80..w-toku/m96-*).
- Klucz `ForgeOneJob` nowy - w `Armoury.json` Jeffa go nie ma, wiec dziala domyslne TAK (bez edycji pliku).

**Status: DO SPRAWDZENIA** (build Armoury kod 0; gra nie uruchamiana).

**Test w grze krok po kroku.**
1. Miasto z kuznia, rano (np. 6:00-10:00). "Go to the smithy" -> "Enter smithy". Przetop 3-4 sztuki, potem wykuj 1-2 miecze. Zapamietaj napis
   "Hours spent for all actions" (np. 12.8 h).
2. Kliknij Done: od razu pasek "Your work at the forge: about 13 hours in all. First N hours of smelting and refining ...". Pasek leci 13 godzin gry
   (zegar na gorze); po godzinach wytopu miecze schodza po kolei ("Off the anvil: ..."), na koncu powrot do podmenu kuzni. W `Armoury.log`:
   `Robota w kuzni (m96): <miasto> - wyjscie z ekranu | ta wizyta: godzin 12.8 ... pasek TAK, doba kuzni oplacona`, potem `pasek doszedl do konca`.
   Linii `Jeden zegar kuzni:` ani paska BK juz nie ma.
3. To samo, ale wyjdz klawiszem Esc - pasek tez sie otwiera.
4. W czasie wytopu kliknij "Step away": komunikat "The smelting and refining come first ... About N more hours", pasek leci dalej.
5. Gdy miecze sie kuja (po wytopie), "Step away": komunikat "You step away from the forge ... N hours of work wait here for you", w sakwach tylko
   gotowe sztuki. Postoj w miescie kilka godzin (karczma, "Wait here") - "Look in on the work in hand" pokazuje TYLE SAMO godzin (robota stoi).
   Wyjedz i wroc; "Stay until the work is done" (albo ekran kuzni i Done - pasek z dopiskiem "(N of them left from before)") - robota rusza od miejsca
   przerwania.
6. Noc: zacznij wytop ok. 20:00 - od 23:00 pasek stoi do 5:00 (czas plynie, "Step away" dalej odmawia), potem leci dalej.
7. Towarzysz przy kowadle (wybor bohatera na ekranie): jego kucie tez liczy godziny i otwiera pasek.
8. Kontrola: `grep "Robota w kuzni (m96)" Armoury.log` - linia startu z "otwarcie ekranu wpiete, wyjscie z ekranu ... wpiete, napis godzin BK wpiety"
   i "robota gracza tylko na pasku przy kowadle", linie wizyt, koncow, odejsc i przerwan z licznikami ("odmow odejscia") i spisem "czekaja w
   kuzniach". Bez `ERROR in ForgeJob`.
