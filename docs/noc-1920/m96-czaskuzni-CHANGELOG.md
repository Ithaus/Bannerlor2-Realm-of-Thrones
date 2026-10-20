## m96-czaskuzni (20.10) - ekran kuzni = jedna robota, po wyjsciu zawsze jeden pasek na caly czas (Armoury)

**Dla Jeffa (prosto).** Ekran kuzni to teraz jedna robota: wszystko, co na nim zrobisz (wytop, przekucie metalu, miecze, zbroje, luki, zlecenia),
dopisuje swoje godziny do jednego licznika, a napis "Hours spent for all actions" pokazuje ich sume. Po wyjsciu z ekranu (Done albo Esc) zawsze
otwiera sie jeden pasek na caly ten czas - najpierw leca godziny wytopu, potem kolejne sztuki schodza z kowadla, kazda w swoim momencie. Jesli odejdziesz
wczesniej ("Step away"), masz to, co juz gotowe; reszta czeka w tej kuzni, a po powrocie wystarczy znow wejsc na ekran kuzni (albo "Stay until the work
is done"), zeby robota szla dalej od miejsca przerwania. Metal z wytopu dostajesz od razu (jest potrzebny do kucia), a w nocy (23-5) kuznia stoi
i pasek czeka do rana - tak jak dotad przy sztukach. Wylacznik w MCM: "Forge One Job" (grupa "Time at the anvil"), wylaczony = jak dotad.

**Problem.** Jeff 20.10 (gra na 95a): "jak kuje bron, to jestem w menu z paskiem, a teraz czasami jestem, czasami nie jestem; pokazuje mi, ze to bedzie
trwalo 17.8 godzin; ma liczyc calkowity czas: jak wytapiam, potem kuje miecz i wychodzi mi 23 godziny, to odpala sie pasek i leci 23 godziny; jak wyjde
i nie skoncze kucia, to biore tylko to, co wykulem, a reszta czeka, az wroce."

**Dowod (logi Jeffa `D:\Backup-Bannerlord\logi-jeffa\2026-10-20`, linia `Jeden zegar kuzni:` przy kazdym zamknieciu ekranu przyciskiem Done).**
- Sam wytop - pasek BK = ekran: `[14:38:29] ekran BK 17.5 h, z tego w kolejce 0.0 h - odsiadka BK za przetop / rafinacje 17.5 h` (The Eyrie;
  tak samo 16.0 / 13.3 / 18.3 / 17.7 / 16.7 h rano) - to jest "17.8 godzin" z ekranu, ktore Jeff widzi i ktore pasek odlicza.
- Wytop + miecze - pasek krotszy niz ekran, miecze w tle: `[11:09:07] ekran BK 14.8 h, z tego w kolejce 6.0 h - odsiadka 8.8 h` (Seagard),
  `[11:42:04] ekran BK 11.7 h, z tego w kolejce 10.0 h - odsiadka 1.7 h` (Harrenhal, 4 miecze - pasek 1.7 h przy 11.7 h na ekranie),
  `[14:43:40] ekran BK 12.8 h, z tego w kolejce 7.3 h - odsiadka 5.5 h` (The Eyrie) i zaraz `Dostawa broni: crafted_item_10/11` (14:43:43, 14:43:48) -
  sztuki kuly sie ROWNOLEGLE z paskiem BK, a po jego koncu reszta kolejki szla bez paska (tylko gdy gracz stoi w miescie).
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

**Zmiana.** Nowy plik `Armoury/src/ForgeJob.cs` + male wpiecia; klucz MCM `ForgeOneJob` (domyslnie TAK; dziala razem z `ForgeOneClock`).
- JEDNA ROBOTA = zwykla kolejka kowala tej osady (`_projects`, jeden zegar po kolei, zapis `arm_projects` jak dotad). Otwarcie ekranu (postfiks
  konstruktora `CraftingVM`): stamina wszystkich kowali z listy ekranu (`AvailableCharactersForSmithing` = bohaterowie druzyny). Kazda sztuka z ekranu
  (`ForgeClock.NoteQueued` - jedno miejsce dla broni, zbroi, lukow, amunicji) dopisuje swoje godziny z kolejki i z miary BK. Wyjscie z ekranu (postfiks
  `CraftingVM.ExecuteCancel` - przycisk Done przez BK i klawisz Esc; tylko gdy `CraftingState` naprawde zszedl ze stosu): praca bez sztuki =
  zuzyta stamina wszystkich / 6 minus miara BK sztuk - wpis "praca bez sztuki" (`~prep|dni|1|osada|prep||1`) w kolejce tej kuzni PRZED sztukami z tej
  wizyty (za robota sprzed wizyty), potem `SwitchToMenu("arm_project_wait")` na laczny czas tej kuzni. Odsiadka BK pominieta (`SkipBkWait` -> false,
  `ForgeJob.TookOverBkWait`). Napis ekranu "Hours spent for all actions" = godziny tej wizyty, ktore odliczy pasek (postfiks BK `GetSpentHours`).
- PASEK `arm_project_wait` (ten sam co "Stay until the work is done"): napis po angielsku z lacznym czasem, ile z tego sprzed wizyty, kolejnosc, noc,
  odejscie. Koniec paska i "Step away" wracaja do menu, z ktorego otwarto ekran (np. podmenu kuzni m95), inaczej do "Work the forge".
- WYNIK: sztuka wychodzi, gdy pasek ja przejdzie (`Forge.Deliver` jak dotad). Material z wytopu i rafinacji - od razu w sakwach, jak dotad (ekran
  potrzebuje go do nastepnego kucia; wybrane jako bezpieczniejsze niz trzymanie metalu w kuzni pod otwartym ekranem); w kolejce stoja tylko jego godziny,
  bez wydania i bez XP (XP za wytop dal ekran).
- ODEJSCIE przed koncem ("Step away", napad, inne menu, wyjazd): gotowe sztuki juz w sakwach, reszta (sztuki w toku i godziny pracy bez sztuki) czeka
  w tej kuzni i idzie dalej po powrocie - od miejsca przerwania, przed nowa robota. Praca bez sztuki zawsze tylko na miejscu (twoje rece), takze przy
  `ForgeWorksWithoutYou`.
- NOC: jedna zasada dla calej roboty, ta co dotad dla sztuk (`WorkshopNightRest`, Jeff 05.10 "noc - tak"): 23-5 kuznia stoi, pasek czeka do switu,
  czyli kowal pracuje 5-23 (18 h); rytm snu przy kowadle (AnvilShift) bez zmian. Dotad pasek BK za wytop lecial tez noca.
- LOG (`Armoury.log`, prefiks `Robota w kuzni (m96)`): przy starcie gry - wpiete latki i zasady; przy wyjsciu z ekranu - godziny wizyty (praca bez sztuki
  i sztuki, miara BK, stamina kowali), robota sprzed wizyty, razem w kuzni, pasek TAK/NIE; przy koncu paska / odejsciu - ile zostalo; w kazdej linii:
  roboty gracza w sesji gry (godziny lacznie, czynnosci, paskow do konca, odejsc, przerwanych inaczej) i czekajace w kuzniach (godziny, sztuki w toku,
  gotowe, wpisy bez sztuki).
- Wpiecia: `Project.cs` (`IsPrep`, `Valid`, `Name`, zapis id `~prep`), `ArmouryBehavior.cs` (spis, AdvanceProjects, CollectReadyProjects, HasProjectsHere,
  ProjectHoursLeftHere, HandOver, `ProjectLines`, Reset), `ForgeClock.cs` (1 linia w NoteQueued, 1 w SkipBkWait, `ResetBkQueued`), `SmithMenu.cs`
  (napis paska, powrot, linia logu), `SubModuleMain.cs` (ApplyAll), `Settings.cs` + `McmSettings.cs` (gen_mcm).

**Ryzyko / co sprawdzic (kontrola calosci).**
- `SwitchToMenu` z postfiksu `ExecuteCancel` - ta sama chwila, w ktorej BK otwiera swoj pasek (`CloseWithWait`: `ExecuteCancel`, potem
  `StartCraftingMenu` -> `SwitchToMenu`), sprawdzona u Jeffa; NIE z opcji menu oczekiwania (pulapka CLAUDE.md). `PopState` gry jest natychmiastowy
  (`GameStateManager.DoGameStateJobs`), `CurrentMenuContext` bierze MapState. Bez menu gry - pasek nie startuje (robota czeka, log "brak menu gry").
- Wszystkie odczyty `_projects` przejrzane: spis ("Look in on the work in hand" - "Bench work (smelting, refining, orders) at X"), sufit broni "van"
  (pomija wpis), AdvanceProjects (wpis bez sztuki: tylko na miejscu, noc, doba kuzni jak sztuki, bez `Recipes.For(null)` i bez XP), CollectReadyProjects
  i HandOver (nic do wydania), HasProjectsHere / ProjectHoursLeftHere (licza wpis). Innych czytelnikow brak (grep).
- Cofniecie DLL: starszy DLL nie znajdzie przedmiotu `~prep` (`MBObjectManager.GetObject` = null) i wyrzuci wpis po cichu - przepadaja tylko godziny pracy
  bez sztuki w toku; sztuki zostaja. Nowy DLL czyta stary zapis bez zmian (brak wpisow `~prep`).
- Kolejnosc: praca bez sztuki z wizyty idzie przed WSZYSTKIMI sztukami tej wizyty (nie przeplatana czynnosc po czynnosci). Przy kolejnosci kucie - wytop -
  kucie pierwsza sztuka wyjdzie o godziny wytopu pozniej; laczny czas ten sam.
- Odejscie zaraz po samym wytopie: metal masz, godziny wytopu zostaja w tej kuzni i ida przed nowa robota przy nastepnej wizycie (nic z niczego - czas
  nie przepada; dotad z paska BK nie dalo sie wyjsc, Esc pomijal go wcale).
- Towarzysz przy kowadle: jego stamina tez liczy godziny (BK liczyl tylko gracza) - napis na ekranie moze byc wiekszy niz dotad.
- Cena: doba kuzni jak dotad (`Forge.PayDayRent` / `DayPass` - jeden rejestr); przy Done karnet dalej kupuje `DayPassPrefix` na `StartCraftingMenu`
  (prefiksy biegna przed `SkipBkWait`), przy Esc doba placi sie przy pierwszej godzinie paska. Godzinowka BK nie leci (nie ma paska BK). Materialy i XP
  bez zmian. Zlecenia gry (CraftingOrder): sztuka do zlecenia od razu jak dotad, jej godziny w pracy bez sztuki (dotad w pasku BK). Cech (GuildMend,
  rece kowali miasta) i platnerz w taborze (poza osada - ForgeJob nie startuje) bez zmian. `ForgeLikeTownSmith` (C5): sztuki z godzinami C5, napis
  ekranu tez (jedna miara).
- Stamina w pasku: nie regeneruje (`InForgeMenu` zawiera `arm_project_wait`) - jak w pasku BK.
- m96-wzory / m96-jakosc: wpiecia w `ForgeClock` tylko po 1 linii (NoteQueued, SkipBkWait); `CraftPost`, `OnNewItemCrafted`, jakosc i wzory nietkniete.
- Klucz `ForgeOneJob` nowy - w `Armoury.json` Jeffa go nie ma, wiec dziala domyslne TAK (bez edycji pliku).

**Status: DO SPRAWDZENIA** (build Armoury kod 0; gra nie uruchamiana).

**Test w grze krok po kroku.**
1. Miasto z kuznia, rano (np. 6:00-10:00). "Go to the smithy" -> "Enter smithy". Przetop 3-4 sztuki, potem wykuj 1-2 miecze. Zapamietaj napis
   "Hours spent for all actions" (np. 12.8 h).
2. Kliknij Done: od razu pasek "Your work at the forge: about 13 hours in all ...". Pasek leci; po godzinach wytopu miecze schodza po kolei
   ("Off the anvil: ..."), na koncu powrot do podmenu kuzni. W `Armoury.log`: `Robota w kuzni (m96): <miasto> - wyjscie z ekranu | ta wizyta: godzin 12.8
   ... pasek TAK`, potem `pasek doszedl do konca`. Linii `Jeden zegar kuzni:` ani paska BK juz nie ma.
3. To samo, ale wyjdz klawiszem Esc - pasek tez sie otwiera.
4. Odejscie: wytop + 2 miecze, Done, w polowie paska "Step away - the work can wait": komunikat "... N hours of work wait here for you", w sakwach tylko
   gotowe sztuki. Wyjedz z miasta i wroc; "Work the forge" -> "Look in on the work in hand" pokazuje reszte, "Stay until the work is done" (albo ekran
   kuzni i Done) - pasek na reszte, z dopiskiem "(N of them left from before)", gdy wejdziesz przez ekran.
5. Noc: zacznij ok. 20:00 - od 23:00 pasek stoi do 5:00 (czas plynie), potem leci dalej.
6. Towarzysz przy kowadle (wybor bohatera na ekranie): jego kucie tez liczy godziny i otwiera pasek.
7. Kontrola: `grep "Robota w kuzni (m96)" Armoury.log` - linia startu z "otwarcie ekranu wpiete, wyjscie z ekranu ... wpiete, napis godzin BK wpiety",
   linie wizyt, koncow i odejsc z licznikami i spisem "czekaja w kuzniach". Bez `ERROR in ForgeJob`.
