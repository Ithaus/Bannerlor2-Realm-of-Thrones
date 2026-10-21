## m97-wioski (20.10) - do kazdej wioski pobocznej mozna wjechac jak do wsi glownej (Armoury)

**Dla Jeffa (prosto).** Kazda wioska poboczna na mapie (nie tylko Twoja) ma teraz raczke kursora: klikasz ja, druzyna jedzie i wjezdza, znika z mapy
jak w zajezdzie, slychac wies. W srodku nie ma nikogo waznego, ale jest opis (czyja, z jakiej wsi glownej, co robi, czy spalona), nocleg (za oplata jak
wspolna izba zajazdu, na Twojej ziemi za darmo), "Make camp here", handel tym, co ta wioska robi, i inwestycje w jej prace. W wojnie masz "Take a hostile
action": rabunek TYLKO tej wioski (pasek jak przy rabunku, lup z jej czesci okregu, wies glowna zostaje nietknieta) albo okup od tej wioski (ochrona 30 dob).
Wylacznik w MCM: "Side Villages Enterable" (domyslnie TAK).

**Problem.** Jeff 20.10 ok. 17:30: "Czy mozemy zrobic tak, ze do wioski pobocznej tez mozna wjechac jak do wioski glownej? ... bez bohaterow w niej, ale
zainwestowac, zakupic towar (jesli to nie zepsuje ekonomii i predkosci gry), przespac sie, oboz rozbic, pladrowanie kazdej osobno, okup". Dowod z kodu
(kandydat 96, 25f24c17): menu mial tylko obrazek wioski gracza (`MapVillagesView.OnVisualIntersected` ustawial `selectedVisual` tylko przy
`SideVillageFief.Clickable`), reszta 1957 wiosek ukladu v7 byla tylko obrazkiem z dymkiem; rabunek gracza szedl zawsze przez wies gry (zdarzenie rabunku
gry + `DistrictRaid`, ksiega pali wioski lancuchem od strony wroga), okup (`RaidTribute`) tylko dla wsi gry.

**Zmiana.** Nowe pliki `Armoury/src/SideVillageVisit.cs` i `SideVillageVisit.Raid.cs` + male wpiecia.
1. **Wjazd** (klucz `SideVillagesEnterable`): klik na wioske = `MapState.ProcessTravel` (jak zajazd i wioska gracza), menu `arm_sidevil` po dojechaniu do
   1.6 jedn. Bez pytania przy postoju (wiosek sa setki - pytanie przy kazdym postoju meczyloby). Wioska gracza z wlasnym menu (`SideVillageFief`) - jak dotad.
   Druzyna znika z mapy ta sama droga co w zajezdzie m95 (`RoadsideInnStay`: menu `arm_sidevil` i `arm_sidevil_hostile`, takze menu wioski gracza
   `arm_side_fief` / `arm_side_home` otwarte z mapy, i sen w wiosce pobocznej). Dzwiek i obrazek jak menu wsi gry (`settlement_village`, `2d/village`,
   `wait_mesh` wsi okregu - sprawdzone w `PlayerTownVisitCampaignBehavior` 1.4.8), przy rabunku `wait_raiding_village` + `2d/village_raided`; sen w wiosce -
   tlo wsi zamiast karczmy.
2. **Opis** (liczony raz w init menu): wioska, wies gry okregu, pan (`MapVillagesView.HouseName`), krolestwo, "no lord or headman lives here", ludzie
   i osady z ksiegi wiosek, stan (spalona / opuszczona / odbudowa / czesciowo pusta), linie `DistrictWorks.PartLines` (co robi, % produkcji okregu, prace),
   rabunek okregu, ochrona po okupie, wojna albo "your own lands".
3. **Inwestycje**: prace okregu SA juz per czesc (wies gry albo jedna wioska poboczna) - `DistrictWorks.OpenPartFor(okreg, uid)`, warunek jak w menu wsi
   gry (`CanTrade`: wojna, rabunek) i wycena czesci (`QuotesOf`: Allowed, rodzaj prac) raz na godzine gry i po oknie prac. Nic nie trzeba dzielic.
4. **Handel** - koszyk liczony LENIWIE przy kliknieciu "Trade", bez nowych magazynow: dla kazdego towaru magazynu wsi okregu udzial tej wioski w jego
   produkcji dnia (`VillageCrafts.PartsOf`: `Pcs` czesci / suma `Pcs` czesci okregu), zaokraglony; czego wioska nie robi - nie ma w koszyku. Ekran handlu jak
   handel wioski gracza z mapy (ceny targu wsi okregu, kiesa wsi jako kupiec, wlasny sluchacz). Po "Done" magazyn wsi okregu zmienia sie DOKLADNIE o roznice
   koszyka (kupione schodzi, sprzedane wchodzi - dowoz do wsi okregu), kiesa wsi o to, co gra przelala graczowi bez osady (`min(-TotalAmount, zloto kupca)`
   - ta sama poprawka co `SideVillageHome.MapTradeDone`). Anulowanie - gra cofa koszyk (`InventoryLogic.Reset`), magazyn bez zmian.
5. **Nocleg**: wioska na ziemi rodu gracza (`st.OwnerClan == Clan.PlayerClan`) - za darmo pod dachem (`TownInns.HostSleep`; `TownInns.HostNear` przyjmuje
   te wioske przez `SideVillageVisit.HostNear`); obca - za oplata jak wspolna izba zajazdu (`TownInns.RoadQCondition` / `RoadCommit` z "zajazdem" wioski:
   `RoadsideInns.Find` ma zapas `SideVillageVisit.InnFor`), ten sam rachunek, kontrola obiegu i podatek pana co zajazd, pieniadze do kiesy wsi okregu.
   Wojna, rabunek okregu, wioska spalona - opcja szara z powodem. **Oboz**: "Make camp here" zamyka menu (`ExitToLast`), oboz rozbija tick
   (`NightRest.MakeCampNow` - droga klawisza O, takze oboz BK).
6. **Rabunek tej wioski** ("Take a hostile action" -> "Raid the village", tylko w wojnie): obroncy = czesc milicji wsi okregu wedlug ludzi wioski w domu;
   mniej zdrowych ludzi (gracz + armia) niz obroncow - nie (bez bitwy). Praca = ludzie osad wioski do pulapu (`DevastationMaxPercent` x osady - puste) i nie
   wiecej niz Room okregu; tempo `DevastationPerRaiderDay` x ludzie na dobe (regula E7/R1, mnoznik 1). Menu oczekiwania `arm_sidevil_raid` z paskiem; co
   godzine gry `Devastation.StrikePeople` (ten sam rdzen: ludzie, zabici, las, uchodzcy, spichlerz, prace okregu, pamiec wojny, wiadomosc o spaleniu) z
   `VillageBook.ForceUid` = ta wioska - ksiega pali TYLKO jej osady (nowa `VillageBook.Forced` w `Burn`: bez lancucha). Wies gry zostaje Normal, bez
   zdarzenia rabunku gry. Lup raz na koniec (pelny pasek, "Stop raiding", bitwa / spotkanie - za zrobiona czesc): udzial s = trafieni / ludzie okregu w domu
   na starcie, zloto s x kiesa wsi (`SmallGold.PayRaidLoot`), stado (`VillageHerds.OnRaid`), towar s z magazynu wsi do taboru (zwierzeta zawsze, reszta wedlug
   udzwigu, nadmiar spalony; ramka `GoodsLedger.FRaid`, `ArmsBook.KRaid`) - arytmetyka `DistrictRaid.Loot`. Relacja: pan okregu -3, notable -1 (gra przy
   rabunku wsi: -6 / -3); wlasna ziemia - bez zmiany. "Stop raiding" - flaga, tick wraca do menu (zadnego `SwitchToMenu` z opcji menu oczekiwania).
7. **Okup tej wioski** ("Demand tribute"): regula R3-03a na czesci wioski - q = ludzie / obroncy wioski, q < 1 - opor; stawka `RaidTributeShareLow` ..
   `RaidTributeShareHigh` (1:1 - 5:1), kwota = stawka x kiesa wsi okregu x udzial wioski w ludziach okregu; przelew `GiveGoldAction` kiesa wsi -> gracz;
   ochrona (wioska, krolestwo gracza) `RaidTributeSpareDays` dob (bez rabunku i drugiego okupu); relacja pan -1. Okup wsi gry od krolestwa gracza (R3-03a,
   `RaidTribute.Spared`) chroni tez jej wioski. Po rabunku wioski 10 dob bez okupu (jak gra po wrogim czynie).
8. **AI bez zmian** (rabuje wies gry; `DistrictRaid` pali wioski lancuchem jak dotad - `ForceUid` jest ustawiane tylko na czas uderzenia gracza).
9. **Zapis** `arm_sidevil` (`SaveText.Sync`): `sv1|t=uid,frakcja,do,doba,zloto;...|l=uid,doba;...|r=uid,okreg,praca,zrobione,godziny,minelo,trafieni,dom0,
   ludzie,doba`. Brak klucza (stary zapis, kampania Jeffa) = brak ochron i rabunku. Reset w osobnej linii `_m97WioskiReset` (ArmouryBehavior, wzorzec p6).
10. **Linie logu** (prefiks `Wioski poboczne (WP): `): `start sesji | klucz ... | wiosek w ukladzie N | ukrycie partii ... | nocleg za oplata ... | okup ...`;
   `wjazd do X (uid, okreg, pan, wojna z graczem) - klik na wioske`; `handel w X - koszyk z magazynu wsi okregu ...: N rodzajow, M sztuk`; `handel ... zamkniety:
   gracz +/-, wies okregu +/-, magazyn: wydane / przyjete`; `nocleg w X - pod dachem / za oplata`; `oboz przy wiosce`; `okno prac`; `rabunek X - start: ...`,
   `rabunek X - koniec: powod | godziny, trafieni, lup (udzial %, zloto, stado, sztuki, spalone) | wioska: spalona / puste osady`; `okup X: N zl (stawka,
   udzial, kiesa wsi przed -> po, kontrola)`; `dzien N | wjazdy | handel | noclegi, obozy, okna prac | rabunki wiosek ... | okupy wiosek ... | tick: sprawdzen,
   srednio ms | potkniecia` (tylko z ruchem). Bledy jako `ERROR in SideVillageVisit.*`.
11. **Wpiecia** (minimalne): `MapVillagesView.cs` (3 miejsca: klik, raczka, `OnMapClick`), `RoadsideInns.Find` (zapas po uid), `RoadsideInnStay.InnNow`
   (menu wioski i sen pod dachem) i `InnSleepSound` (tlo wsi), `TownInns.Host.cs` `HostNear` (wioska na ziemi rodu gracza), `VillageBook.Burn` (+`ForceUid`,
   `Forced`), `ArmouryBehavior.cs` (reset, zapis, tick, start sesji, menu, doba - po 1 linii), `Settings.cs` + `McmSettings.cs` (gen_mcm).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Wydajnosc: nic co klatke poza tym, co juz bylo - `Clickable` przy najechaniu (slownik uid + `Settlement.Find`), tick 4 razy na sekunde tylko gdy czeka klik,
  oboz albo trwa rabunek; opis, koszyk i plan rabunku liczone przy wejsciu / kliknieciu; warunki opcji gra liczy przy odswiezeniu menu (`GameMenuManager
  .GetVirtualMenuOptionIsEnabled` czyta zapamietane IsEnabled - sprawdzone w dekompilacji). Slownik wiosek (1957) raz na sesje.
- Klik w teren: teraz klik w obrys KAZDEJ wioski = jazda + menu (dotad klik szedl w teren). Kto klika wioske tylko po to, zeby przez nia przejechac, dostanie
  menu po dojechaniu - "Leave" i dalej. Wylacznik `SideVillagesEnterable`.
- Ukrycie i nocleg za oplata ida przez klucze zajazdow (`RoadsideInns`, `RoadsideInnHideParty`, `TownInns`) - przy wylaczonych zajazdach nocleg w obcej wiosce
  szary z napisem zajazdu ("Roadside inns are switched off"), a druzyna nie znika. Teksty rachunku sa z zajazdu ("common room", "the keeper") - do oceny.
- Zapas `RoadsideInns.Find`: dla uid prawdziwych zajazdow bez zmian (slownik zajazdow najpierw). Sen za oplata w wiosce zapisany i wczytany: `RoadNear`
  szuka "zajazdu" wioski w ukladzie ksiegi; gdyby ksiega jeszcze nie byla wczytana przy pierwszym sprawdzeniu snu, stan snu zejdzie bez premii (jak przy
  odjechaniu) - ZOBACZYC w grze.
- Rabunek: uderzenia ida przez galaz "zerowanie" `VillageBook.OnStrike` (bez klucza sesji rabunku gry) - w liczbach ksiegi wiosek licza sie jako zerowanie
  (`_dForW`), w `Devastation` jako rabunek osada po osadzie (KDist). Jency E4/9 - nie (bez zdarzenia rabunku gry nikt nie bierze jencow; trafieni zostaja
  uchodzcami). Liczniki wojny gry (SuccessfulRaids) - nie. Rabunek innej wsi przez AI w tym samym czasie - `ForceUid` tylko na czas naszego wywolania.
- Okup wioski chroni tylko przed rabunkiem i drugim okupem TEJ wioski przez krolestwo gracza; AI krolestwa gracza rabujac wies gry moze dalej spalic te wioske
  lancuchem (AI bez zmian - polecenie). Okup wsi gry (R3-03a) chroni cala okolice, takze przed rabunkiem wioski.
- Przerwanie rabunku (bitwa, spotkanie): straznik po 2 s (8 sprawdzen) bez menu rabunku zamyka rabunek za zrobiona czesc z lupem (jak `DistrictRaid`: lup
  dla napastnika, ktory nie przegral - tu lup do taboru od razu; przegrana bitwa i tak zabierze tabor). Wczytanie w srodku rabunku - stan z zapisu, menu
  oczekiwania wraca (`RaidInit` -> `StartWait`).
- Handel: towary, ktorych wioska nie robi, nie sa w koszyku - wioska rybacka sprzeda ryby, a nie zboze wsi glownej (zboze kupisz we wsi glownej).
- Kolizje: inne galezie w-toku nie sa tu scalane - przed scaleniem `git merge-tree` z galeziami 97 (pliki wspolne: `ArmouryBehavior.cs`, `Settings.cs`,
  `McmSettings.cs` - po scaleniu od nowa z gen_mcm, `MapVillagesView.cs`, `RoadsideInnStay.cs`, `VillageBook.cs`).
- Zbudowane (kod 0). Bez gry i autotestu (zakaz w tej pracy).

**Status.** DO SPRAWDZENIA (zbudowane, kod 0; bez autotestu).

**Test w grze (krok po kroku dla Jeffa).**
1. Najedz kursorem na wioske poboczna (nie Twoja) - kursor raczki. Kliknij: druzyna jedzie, po dojechaniu otwiera sie menu wioski; druzyny nie widac na mapie,
   slychac wies. W logu Armoury: `Wioski poboczne (WP): wjazd do ...`.
2. Przeczytaj opis: czyja, z jakiej wsi glownej, ilu ludzi, co robi. "Leave" - druzyna wraca na mape.
3. "Trade": po lewej towar tej wioski (np. ryby w wiosce rybackiej), kup cos i sprzedaj cos, "Done". W logu `handel w ... zamkniety` z kiesa wsi okregu i
   magazynem (wydane / przyjete). Otworz i zamknij przez anuluj - w logu nic sie nie zmienia w magazynie.
4. "Invest in its works" - okno prac tej jednej wioski (jak w Twojej wiosce).
5. "Rest the night here - N d": placisz jak za wspolna izbe zajazdu, sen do switu, druzyny nie widac. Na swojej ziemi (wioska wsi Twojego rodu) - za darmo.
6. "Make camp here": menu znika, druzyna rozbija oboz obok wioski.
7. W wojnie: "Take a hostile action" -> "Demand tribute" (kwota w dymku) - zloto do Ciebie, w opisie wioski "spared for 30 more days"; drugi raz i rabunek
   tej wioski - zablokowane z powodem.
8. W wojnie, w innej wiosce: "Raid the village" (dymek: ilu ludzi, ile godzin, ile zlota) - pasek; po koncu komunikat "You raided ...", wioska na mapie
   spalona, wies glowna NIE plonie i nie jest spladrowana. Sprobuj tez "Stop raiding" w polowie - lup za zrobiona czesc.
9. Zapisz w menu wioski i w trakcie rabunku, wczytaj - menu dziala, rabunek idzie dalej.
10. MCM "Side Villages Enterable" = NIE - wioski znowu tylko obrazki (klik w teren).
11. W logu szukaj `Wioski poboczne (WP): dzien N` (wjazdy, handel, noclegi, rabunki, okupy) i brak `ERROR in SideVillageVisit`.

**Decyzje (projektowal Claude; wylacznik `SideVillagesEnterable`).**
- Bez magazynow wiosek: koszyk z magazynu wsi glownej wedlug udzialu wioski w produkcji towaru, liczony tylko przy kliknieciu - zero kosztu dla swiata i ta
  sama ekonomia (jeden magazyn, jedna kiesa na okreg).
- Bez bitwy z milicja wioski: wioska bez bohaterow nie wystawia oddzialu - przewaga albo nic (jak okup R3-03a). Bitwa przy wsi glownej zostaje jak w grze.
- Bez pytania przy postoju przy wioskach (setki wiosek) - wejscie tylko klikiem.
- Nocleg w obcej wiosce = cena wspolnej izby (jedna regula z zajazdem), na wlasnej ziemi za darmo (jak Twoja wioska).
