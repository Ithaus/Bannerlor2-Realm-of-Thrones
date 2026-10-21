## m97-wioski (20.10) - do kazdej wioski pobocznej mozna wjechac jak do wsi glownej (Armoury)

**Dla Jeffa (prosto).** Kazda wioska poboczna na mapie (nie tylko Twoja) ma teraz raczke kursora: klikasz ja, druzyna jedzie i wjezdza, znika z mapy
jak w zajezdzie, slychac wies. W srodku nie ma nikogo waznego, ale jest opis (czyja, z jakiej wsi glownej, co robi, czy spalona), nocleg (za oplata jak
wspolna izba zajazdu, na Twojej ziemi za darmo), "Make camp here", handel tym, co ta wioska robi, i inwestycje w jej prace. W wojnie masz "Take a hostile
action": rabunek TYLKO tej wioski (pasek jak przy rabunku, lup z jej czesci okregu, wies glowna zostaje nietknieta) albo okup od tej wioski (ochrona 30 dob).
Po recenzjach: kazda wioske pilnuje cala milicja wsi glownej (maly oddzial nie spali juz okolicy bez bitwy), a Twoj rabunek wsi glownej pali tylko jej wlasne
osady - kazda wioske poboczna rabujesz osobno. Wioska, ktora zaplacila Ci okup, nie splonie tez przy rabunku wsi glownej przez Twoje krolestwo, a okup od wsi
glownej jest wtedy mniejszy o te wioski. Wylacznik w MCM: "Side Villages Enterable" (domyslnie TAK).

### Poprawki po recenzjach (20.10 wieczorem, commit nad 03dc9953)

Dwie recenzje (gra, technika): bez uwag krytycznych, 3 wazne, 6 drobnych. Wszystkie wazne poprawione, drobne - 5 poprawionych, 1 swiadomie nie (powod nizej).
1. **WAZNE (obie recenzje): rabunek wsi glownej przez gracza dalej palil wioski poboczne lancuchem** - Jeff: "pladrowanie wioski trzeba robic kazda osobno,
   nie tak, ze robi sie to z glownej". PRAWDA (`VillageBook.PickFirst/NextFrom` - wioski najpierw, wies gry na koncu; `DistrictRaid` bez zmian). Poprawka:
   `DistrictRaid.PlayerMainOnly` (klucze `RaidBySettlementEnabled` i `SideVillagesEnterable`) - krok sesji, ktora prowadzi GRACZ (`ss.Player`, takze jako wodz
   armii), liczy plan tylko z czesci wsi gry (`Plan(..., mainOnly)`: miejsce i zasieg z `VillageBook.RoomFor` - osady wsi gry x ludzie na osade l / n, ta sama
   miara co ksiega przy uderzeniu), uderzenie ma `VillageBook.ForceMain` (nowa galaz `Forced`: tylko osady wsi gry, bez lancucha) i jest obciete do miejsca wsi
   gry TERAZ. Ogien na mapie od razu na wsi gry (`SessionFor`: `s.Cur = W`). Okienko "Raid the next settlement" liczy nastepny krok z tej samej reguly; wies gry
   wypalona - tylko "End the raid". Lup kroku jak dotad (trafieni / ludzie okregu w domu - ta sama arytmetyka co rabunek wioski). AI bez zmian. Przy wylaczonym
   `RaidBySettlementEnabled` (rabunek gry E4/5, nie domyslny) lancuch zostaje jak dotad - zapisane w ryzykach.
2. **WAZNE (gra): okup wioski nie chronil jej przed lancuchem rabunku wsi glownej** (gracz i AI krolestwa gracza), choc dymek obiecywal ochrone krolestwa.
   PRAWDA. Poprawka: sesja ksiegi ma frakcje napastnika (`Sess.Fid`), `PickFirst` / `NextFrom` / biezaca wioska w `Burn` omijaja wioski chronione przed ta
   frakcja (`SideVillageVisit.Spared` - migawka ochron `_protSnap`, podmieniana w calosci, bez alokacji przy odczycie), a plan kroku `DistrictRaid` (i szacunek
   `StepEstimate` w mysli okupu AI) liczy miejsce bez nich (`RoomFor` z `fid`; bez zadnej ochrony - wyjscie od razu, `AnyProt`, plan jak dotad). Uderzenie AI
   obciete do miejsca czesci, ktore wolno palic. Dymek okupu mowi teraz wprost: "you will not raid it, and raids on <wies> by your realm pass it by".
3. **WAZNE (technika): milicja dzielona na wioski bez bitwy** - maly oddzial mogl bez bitwy spalic / oplacic okreg wioska po wiosce. PRAWDA (`Defenders` =
   milicja x udzial wioski). Poprawka: obroncy wioski = CALA milicja wsi okregu i partie we wsi w wojnie z graczem - `RaidTribute.DefOf` (teraz internal), ta
   sama regula sily co okup wsi (milicja pilnuje calej okolicy). Rabunek i okup wioski tylko przy przewadze nad nia; milicje rozbija sie bitwa przy wsi glownej
   ("Raid the village" gry). Napisy: "The militia of X - about N men - ...", przy przewadze milicji podpowiedz "Beat the militia at X first, or come with more men."
4. drobne (gra + technika): **okup wsi glownej po okupach wiosek bral ok. 1.4-1.8x dawnej kwoty**. PRAWDA. Poprawka: `RaidTribute.QuoteOf` (gracz i AI) mnozy
   kwote przez (1 - suma udzialow wiosek tego okregu chronionych przed ta frakcja) - `SideVillageVisit.PaidShare`; udzial i wies okregu zapisane przy okupie
   wioski (`Prot.Share`, `Prot.Dist`; zapis `t=` dostal 2 pola na koncu, wpis bez nich czytany jak dotad). Dymek okupu wsi mowi o odjetej czesci.
5. drobne (technika): **okup wioski nie trafia do pamieci wrogich czynow gry ani do licznikow wojny** - PRAWDA, ale bez zmiany (decyzja): okup wioski to
   czesc okregu - liczony jak udany rabunek dalby 8 "rabunkow" za 8 wiosek jednej wsi (`StanceLink.SuccessfulRaids`), a wpis do pamieci gry wsi glownej
   (`_villageLastHostileActionTimeDictionary`) zablokowalby na 10 dob zapasy i rekrutow w INNEJ osadzie niz ta, ktora zaplacila. Podwojny okup zamyka pkt 4.
6. drobne (technika): **po wczytaniu w trakcie rabunku gubila sie niewola napastnika** (`RaidState.Slaver`). PRAWDA. Poprawka: `Raid.SlaverSet`; przy
   pierwszym ticku po wczytaniu `Slaver` liczone od nowa (`LosersFlee.SlaveCulture` kultury krolestwa gracza - ta sama regula co start rabunku).
7. drobne (technika): **wioska pelna przed koncem pracy - ludzie dalej schodzili z domow calego okregu bez palenia**. PRAWDA (`dB = hit x N / lBefore`,
   lBefore maleje). Poprawka: `SideVillageVisit.StrikeChunk` obcina uderzenie do miejsca wioski TERAZ (`VillageBook.UidRoom` x ludzie okregu / osady okregu);
   pelna - 0, rabunek konczy sie "nic wiecej do spalenia". Licznik obciec w linii doby ("obcietych do miejsca wioski N"). To samo obciecie dla kroku gracza
   (pkt 1) i AI z ochronami (pkt 2).
8. drobne (gra): **nocleg w obcej wiosce mial napisy karczmy** i wymagal klucza zajazdow. PRAWDA. Poprawka: "zajazd" wioski (`Kind == "village"`, tylko
   `SideVillageVisit.InnFor`; plik zajazdow ma tylko track / crossroad / road) - sen "The villagers of X take the men in for coin - straw in their barns and lofts",
   komunikat "The villagers of X take you in for the night - N denars...", powody "You are not at the village." / "The villagers bar their doors..."; klucz
   `RoadsideInns` nie jest juz potrzebny do noclegu w wiosce (ukrycie druzyny dalej za kluczami zajazdow). Rachunek bez zmian.
9. drobne (gra): **brak grupy w sprawdz_logi** - poprawione: `--grupa m97wioski` (WP-0 start, WP-1 ERROR SideVillageVisit 0, WP-2 potkniecia 0, WP-3 kontrola
   handlu - kiesa wsi o -zysk gracza i kiesa gracza o zysk, WP-4 kontrola okupu, WP-5 rabunki wiosek INFO, WP-6 ruch doby INFO); sprawdzone na sztucznym logu.
Opis MCM klucza `SideVillagesEnterable` uzupelniony (milicja, rabunek wsi glownej tylko jej osady), `McmSettings.cs` z `tools/gen_mcm.py`. Zbudowane (kod 0).

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
6. **Rabunek tej wioski** ("Take a hostile action" -> "Raid the village", tylko w wojnie): obroncy = cala milicja wsi okregu i partie we wsi (po recenzji,
   `RaidTribute.DefOf`); mniej zdrowych ludzi (gracz + armia) niz obroncow - nie (bez bitwy). Praca = ludzie osad wioski do pulapu (`DevastationMaxPercent` x osady - puste) i nie
   wiecej niz Room okregu; tempo `DevastationPerRaiderDay` x ludzie na dobe (regula E7/R1, mnoznik 1). Menu oczekiwania `arm_sidevil_raid` z paskiem; co
   godzine gry `Devastation.StrikePeople` (ten sam rdzen: ludzie, zabici, las, uchodzcy, spichlerz, prace okregu, pamiec wojny, wiadomosc o spaleniu) z
   `VillageBook.ForceUid` = ta wioska - ksiega pali TYLKO jej osady (nowa `VillageBook.Forced` w `Burn`: bez lancucha). Wies gry zostaje Normal, bez
   zdarzenia rabunku gry. Lup raz na koniec (pelny pasek, "Stop raiding", bitwa / spotkanie - za zrobiona czesc): udzial s = trafieni / ludzie okregu w domu
   na starcie, zloto s x kiesa wsi (`SmallGold.PayRaidLoot`), stado (`VillageHerds.OnRaid`), towar s z magazynu wsi do taboru (zwierzeta zawsze, reszta wedlug
   udzwigu, nadmiar spalony; ramka `GoodsLedger.FRaid`, `ArmsBook.KRaid`) - arytmetyka `DistrictRaid.Loot`. Relacja: pan okregu -3, notable -1 (gra przy
   rabunku wsi: -6 / -3); wlasna ziemia - bez zmiany. "Stop raiding" - flaga, tick wraca do menu (zadnego `SwitchToMenu` z opcji menu oczekiwania).
7. **Okup tej wioski** ("Demand tribute"): regula R3-03a na czesci wioski - q = ludzie / obroncy (cala milicja okregu), q < 1 - opor; stawka `RaidTributeShareLow` ..
   `RaidTributeShareHigh` (1:1 - 5:1), kwota = stawka x kiesa wsi okregu x udzial wioski w ludziach okregu; przelew `GiveGoldAction` kiesa wsi -> gracz;
   ochrona (wioska, krolestwo gracza) `RaidTributeSpareDays` dob (bez rabunku i drugiego okupu); relacja pan -1. Okup wsi gry od krolestwa gracza (R3-03a,
   `RaidTribute.Spared`) chroni tez jej wioski. Po rabunku wioski 10 dob bez okupu (jak gra po wrogim czynie).
8. **AI bez zmian** (rabuje wies gry; `DistrictRaid` pali wioski lancuchem jak dotad - `ForceUid` jest ustawiane tylko na czas uderzenia gracza). Po recenzji
   lancuch omija tylko wioski, ktore zaplacily okup frakcji napastnika (w praktyce krolestwo gracza); rabunek wsi glownej przez GRACZA pali tylko jej osady.
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
- Ukrycie druzyny idzie przez klucze zajazdow (`RoadsideInns`, `RoadsideInnHideParty`) - przy wylaczonych zajazdach druzyna nie znika. Nocleg za oplata po
  recenzji potrzebuje tylko `TownInns` (napisy wsi zamiast karczmy); dymek rachunku (`RoadQCondition`) dalej mowi o cenach "common room" - to ta sama cena.
- Zapas `RoadsideInns.Find`: dla uid prawdziwych zajazdow bez zmian (slownik zajazdow najpierw). Sen za oplata w wiosce zapisany i wczytany: `RoadNear`
  szuka "zajazdu" wioski w ukladzie ksiegi; gdyby ksiega jeszcze nie byla wczytana przy pierwszym sprawdzeniu snu, stan snu zejdzie bez premii (jak przy
  odjechaniu) - ZOBACZYC w grze.
- Rabunek: uderzenia ida przez galaz "zerowanie" `VillageBook.OnStrike` (bez klucza sesji rabunku gry) - w liczbach ksiegi wiosek licza sie jako zerowanie
  (`_dForW`), w `Devastation` jako rabunek osada po osadzie (KDist). Jency E4/9 - nie (bez zdarzenia rabunku gry nikt nie bierze jencow; trafieni zostaja
  uchodzcami). Liczniki wojny gry (SuccessfulRaids) - nie. Rabunek innej wsi przez AI w tym samym czasie - `ForceUid` tylko na czas naszego wywolania.
- Okup wioski chroni ja przed krolestwem gracza: menu wioski (rabunek, drugi okup), lancuch rabunku wsi gry (gracz i AI krolestwa gracza) i plan kroku; okup
  wsi glownej tej frakcji jest mniejszy o oplacone wioski. NIE chroni przed zerowaniem armii (zywienie wojska z okregu) - tak samo jak okup wsi gry (R3-03a).
  Okup wsi gry chroni cala okolice, takze przed rabunkiem wioski.
- Rabunek wsi glownej przez gracza (po recenzji): tylko jej osady, ale przy `RaidBySettlementEnabled` = NIE (rabunek gry E4/5) i przy sesji oddanej grze po
  3 potknieciach ksiega pali dalej lancuchem (plan gry liczy ludzi z calego okregu). Wies glowna wypalona do pulapu - rabunek konczy sie od razu (bitwa z
  milicja i tak jest), wioski trzeba rabowac z ich menu.
- Obrona wioski = cala milicja i partie we wsi glownej: lord stojacy we wsi glownej z druzyna blokuje rabunek i okup wszystkich jej wiosek (jak okup wsi).
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
   spalona, wies glowna NIE plonie i nie jest spladrowana. Sprobuj tez "Stop raiding" w polowie - lup za zrobiona czesc. Z malym oddzialem (mniej ludzi niz
   milicja wsi glownej) opcja jest szara: "The militia of X - about N men - would drive you off".
8a. W wojnie, przy wsi glownej: "Raid the village" gry (bitwa z milicja, potem rabunek). Ogien na mapie na samej wsi glownej, okienko po osadzie liczy tylko jej
   osady; wioski poboczne NIE plona (sprawdz na mapie i w ich menu). Wioska, ktora wczesniej zaplacila Ci okup, nie plonie przy rabunku wsi glownej przez lorda
   Twojego krolestwa. Okup od wsi glownej po okupach wiosek - dymek "less the N% of its lands whose villages already paid you".
9. Zapisz w menu wioski i w trakcie rabunku, wczytaj - menu dziala, rabunek idzie dalej.
10. MCM "Side Villages Enterable" = NIE - wioski znowu tylko obrazki (klik w teren).
11. W logu szukaj `Wioski poboczne (WP): dzien N` (wjazdy, handel, noclegi, rabunki, okupy) i brak `ERROR in SideVillageVisit`; automat:
   `python -I tools/sprawdz_logi.py --grupa m97wioski <Armoury.log>`.

**Decyzje (projektowal Claude; wylacznik `SideVillagesEnterable`).**
- Bez magazynow wiosek: koszyk z magazynu wsi glownej wedlug udzialu wioski w produkcji towaru, liczony tylko przy kliknieciu - zero kosztu dla swiata i ta
  sama ekonomia (jeden magazyn, jedna kiesa na okreg).
- Bez bitwy z milicja wioski: wioska bez bohaterow nie wystawia oddzialu - przewaga nad CALA milicja wsi glownej albo nic (jak okup R3-03a). Bitwa przy wsi
  glownej zostaje jak w grze - to droga, zeby oslabic milicje.
- Rabunek wsi glownej przez gracza pali tylko jej osady (Jeff: "kazda osobno, nie z glownej"); AI pali lancuchem jak dotad.
- Okup wioski: bez wpisu do licznikow wojny i do pamieci wrogich czynow gry (powod w poprawkach, pkt 5).
- Bez pytania przy postoju przy wioskach (setki wiosek) - wejscie tylko klikiem.
- Nocleg w obcej wiosce = cena wspolnej izby (jedna regula z zajazdem), na wlasnej ziemi za darmo (jak Twoja wioska).

## Obroncy wioski (krok 2) - 20.10 w nocy (Armoury, klucz SideVillageDefenders)

**Dla Jeffa (prosto).** Wioske poboczna bronia teraz jej wlasni ludzie, a nie milicja wsi glownej: mniej wiecej co dziesiaty mieszkaniec w domu bierze cep, wlocznie
albo luk (najwyzej 60 ludzi). Gdy chcesz ja spladrowac albo zadac okupu i masz mniej niz 4 razy wiecej zdrowych ludzi, chlopi wychodza przed wioske i jest zwykla
bitwa: wygrasz - rabunek albo okup idzie dalej, przegrasz albo odjedziesz - wioska nietknieta. Z przewaga 4:1 i wieksza poddaja sie bez walki. Polegli i pojmani
obroncy ubywaja z ludzi okregu, a wioska przez dwa miesiace ma mniej ludzi do obrony; lordowie innych krolestw z mniejsza druzyna niz obroncy wioske omijaja.
Wylacznik w MCM: "Side Village Defenders" (domyslnie TAK; NIE = jak dotad, wioske pilnuje cala milicja wsi glownej).

**Problem.** Jeff 20.10: "czy wioski poboczne moga miec tez obroncow? czy to za duzo tak jak wioski glowne?", potem "dopisz do kolejki po obozie" (osobna bitwa
w wiosce pobocznej) i POPRAWKA: "ale ja chce, aby to byli ludzie z tej wioski pobocznej, a nie z wioski glownej". Kod przed krokiem (c578f4e7, poprawka nr 3 po
recenzji): `SideVillageVisit.Defenders` = `RaidTribute.DefOf` - cala milicja wsi okregu i partie we wsi; rabunek i okup wioski tylko przy przewadze, bez bitwy.

**Zasada (z liczbami).**
- Obroncy = wlasni ludzie wioski w domu (`VillageBook.PartView.Home` - ta sama liczba co opis wioski): 1 na 10 mieszkancow (dorosli mezczyzni to ok. 1/4 ludzi,
  do walki staje ok. 2 z 5 z nich), najwyzej 60, minus pamiec poleglych tej wioski (wraca liniowo przez 60 dob - ranni do sil, chlopcy dorastaja, ludzie z okregu).
  Przyklady: wioska 60 mieszkancow - 6 obroncow, 150 - 15, 400 - 40 (600 i wiecej - 60). Po bitwie, w ktorej 400-osobowa wioska stracila 30 ludzi: od razu 10
  obroncow, po 30 dobach 25, po 60 dobach znow 40 (o ile ludzi nie ubylo).
- Sklad: 70% chlopi kultury wsi okregu (`Culture.Villager`, ROT `villager_*`, poziom 1), 30% milicja kultury (2/3 wlocznicy `MeleeMilitiaTroop`, 1/3 lucznicy
  `RangedMilitiaTroop`, ROT `*_militia_spearman` / `*_militia_archer`, poziom 11 = t2), bez bohaterow. Np. 15 obroncow = 10 chlopow, 3 wlocznikow, 2 lucznikow.
- Poddanie: zdrowi ludzie gracza (z partiami armii, ktora prowadzi - `MenNow`) >= 4 x obroncy. 40 ludzi na wioske 60 mieszkancow (6 obroncow) - poddanie; 40 ludzi
  na wioske 150 (15) - bitwa; 160 ludzi na wioske 400 (40) - poddanie, 100 ludzi - bitwa.
- Milicja wsi glownej nie broni juz wiosek pobocznych (broni tylko wsi glownej - bitwa rabunku gry bez zmian).
- AI (lancuch rabunku wsi gry w ksiedze wiosek): napastnik (zdrowi ludzie partii i jego armii na starcie sesji) mniejszy niz obroncy wioski - lancuch ja pomija;
  wiekszy - pali jak dotad, a obroncy tracaja d x min(1, d / napastnicy) do pamieci poleglych (przy 40 obroncach i 400 napastnikach - 4, przy 50 napastnikach - 32):
  przy duzej przewadze wiekszosc ucieka, przy rownych silach gina. Bez symulacji bitwy i bez drugiego ubytku ludzi (trafionych liczy jak dotad `Devastation`).

**Zmiana.** Nowy plik `Armoury/src/SideVillageVisit.Defenders.cs` + male wpiecia.
1. **Liczba obroncow** `DefendersFor(uid, home)` (pulap `DefCapOf` - 1/10, najwyzej 60, minus `Fallen(uid)`); `SideVillageVisit.Defenders` przy kluczu wola ja
   zamiast `RaidTribute.DefOf`. Tekst menu wrogich akcji: "About N men of its P people would take up flails, spears and bows ... They will come out and fight you by
   the village" albo "... they would give in without a fight"; dymki "Raid the village" / "Demand tribute" mowia, czy bedzie bitwa (bez szarej opcji z powodu sily).
2. **Bitwa** (`StartFight`, z opcji ZWYKLEGO menu `arm_sidevil_hostile`): tymczasowa druzyna `CustomPartyComponent.CreateCustomPartyWithTroopRoster` (droga druzyn zadan
   gry, np. `RivalGangMovingInIssue`) przy graczu, nazwa "Villagers of X", rod = pan okregu (wojna z graczem), wioska okregu jako dom, `IgnoreByOtherPartiesTill(Never)`,
   `SetDoNotMakeNewDecisions`, stoi. Spotkanie: `PlayerEncounter.RestartPlayerEncounter(druzyna, gracz)` + `GameMenu.SwitchToMenu("encounter")` - to samo, co gra robi
   przy wrogiej akcji we wsi (`VillageHostileActionCampaignBehavior.StartHostileAction`, dekompilacja 1.4.8); init menu "encounter" sam zaczyna bitwe polowa
   (`PlayerEncounter.StartBattle` -> `FieldBattleEventComponent`). Gracz ma zwykle "Attack" / "Send troops" / "Leave". Zadnego SwitchToMenu z menu oczekiwania.
3. **Ludzie druzyny = ludzie okregu** (zamknieta ksiega ludzi): przy wyjsciu `PeopleBook.TakeLevy(wies okregu, d, MLevyMilitia)` + `GiveNew` (pochodzenie) - ta sama
   droga co przyrost milicji (`ArmsFromPeople.MilitiaDelta`); czego DOM nie dal - mniej obroncow (`Strip`), ulamek - `GiveNone(FArmsFloor)` jak milicja. Po bitwie
   ocaleni (zdrowi i ranni w druzynie) wracaja do domow: `PeopleBook.Take(FMilitiaHome)` + `ArriveAt(..., MBackMilitia, MBackMilitiaNone)` - jak milicja; sklad
   wyczyszczony i `DestroyPartyAction.Apply(null, druzyna)` (nikt nie wraca drugi raz). Polegli i pojmani nie wracaja (ubytek z ludzi okregu) i ida do pamieci poleglych.
   Klucz `LevyFromVillages` / ksiega pochodzenia wylaczone - druzyna bez zdjecia z DOM (jak gra), ocaleni bez powrotu.
4. **Wynik** - nasluch `MapEventEnded` (`SideVillageVisit.OnMapEventEnded`, 1 linia w `ArmouryBehavior`): bitwa z nasza druzyna - zwyciezca gracz (1) albo nie (2: przegrana,
   odwrot, "Leave" bez zwyciezcy). Straznik `FightGuard` w ticku (4 razy na sekunde, tylko przy bitwie w toku): druzyna poza bitwa i brak `PlayerEncounter` (po ekranach
   lupu) - koniec. Wygrana i gracz na mapie przy wiosce (do 4 jedn.): rabunek startuje (`BeginRaid` + `ActivateGameMenu(arm_sidevil_raid)` z ticku - bez menu) albo
   okup (`PayTribute` ze stawka gorna, q = 5 - obroncy rozbici) i menu wioski. Przegrana - komunikat "The villagers of X held their homes - the village is spared."
5. **Gracz w niewoli druzyny obroncow** (przegral bitwe): druzyna zostaje do konca niewoli (ucieczka / wykup gry), dopiero potem rozwiazana. Jency druzyny przy
   rozwiazaniu: bohaterowie uciekaja (`EndCaptivityAction.ApplyByEscape`, jak `FreeFolkRaids.Home`), szeregowi wracaja do druzyny gracza jako ranni (udzialy pochodzenia
   z lochu do skladu, `FTransfer`), komunikat "The villagers let your N captured men go".
6. **Poddanie** (`Surrenders`): komunikat "The villagers of X see your N men and lay down their tools - they will not fight." i rabunek / okup jak dotad.
7. **AI** (`VillageBook`): sesja rabunku ma sile napastnika (`Sess.Men` = `SideVillageVisit.PartyMen` przywodcy strony atakujacej na starcie sesji); `Burn` (teraz
   opakowanie `BurnCore`) ustawia `_chainMen` tylko dla lancucha (bez `ForceUid` / `ForceMain`), `PickFirst` / `NextFrom` pomijaja wioske `Guarded` (obroncy z ludzi
   w domu tej czesci > napastnik); przy pierwszym paleniu wioski w sesji `NoteChainBurn` (ubytek obroncow do pamieci poleglych).
8. **Zapis** `arm_sidevil` (SaveText): nowe sekcje na koncu, tylko gdy sa: `|d=uid,ilu,doba;...` (pamiec poleglych) i `|f=uid,druzyna,rodzaj,obroncy,ludzie,doba,wynik,
   levy,z DOM,okreg` (bitwa w toku). Stary czytnik je pomija; brak sekcji = brak pamieci i bitwy. Wczytanie: pierwszy tick sesji - `SweepOrphans`: druzyna z wpisu
   odnaleziona po StringId (straznik doprowadza bitwe do konca), druzyny "Villagers of ..." (CustomPartyComponent, dom - wies) bez wpisu i poza bitwa - sklad do domow
   i rozwiazanie; wpis bez druzyny - zamkniety bez strat.
9. **Klucz MCM** `SideVillageDefenders` (domyslnie TAK, dziala przy `SideVillagesEnterable`), `McmSettings.cs` z `tools/gen_mcm.py`. NIE = regula poprawki nr 3.
10. **Log** (prefiks `Wioski poboczne (WP): `): start sesji `... | obroncy wiosek: klucz Side Village Defenders TAK (1 na 10 mieszkancow w domu, pulap 60, poddanie
   przy 4:1, sklad 70% chlopi / 30% milicja, pamiec poleglych 60 dob, wpisow N, bitwa w toku ...)`; starcie `obroncy X (uid, okreg Y): ludnosc P, obroncow D (pulap
   ludnosci K, pamiec poleglych F; chlopi, wlocznicy, lucznicy), sila gracza M -> poddanie (...) | wynik: bez walki | straty z ludnosci 0.` albo `-> bitwa (rabunek|okup,
   druzyna ID, z DOM n)`; koniec `obroncy X (...): koniec bitwy - jak | obroncow D, wrocilo do domow L, straty z ludnosci S (polegli i pojmani) | wynik: ... | pamiec
   poleglych F.`; doba (w linii `dzien N`): `obroncy wiosek: starcia N (poddania, bitwy: wygrane gracza, przegrane / odwrot), straty z ludnosci, druzyny sprzatniete
   po wczytaniu | AI: wiosek pominietych N, spalonych z ubytkiem obroncow M (ubytek x) | pamiec poleglych N wiosek`. Bledy - `ERROR in SideVillageVisit.*`.
11. **Sprawdzanie**: `python -I tools/sprawdz_logi.py --grupa m97obroncy <Armoury.log>` - OB-0 start, OB-1 ERROR SideVillageVisit 0, OB-2 kazda bitwa zamknieta
   (druzyna nie przezywa bitwy; ostatnia otwarta na koncu logu - INFO), OB-3 straty <= obroncow, OB-4 obroncow <= pulapu ludnosci, OB-5 sieroty (INFO), OB-6 doba
   (INFO). Sprawdzone na sztucznym logu (TAK; zly pulap - NIE).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Bitwa: droga gry (RestartPlayerEncounter + menu "encounter" ze zwyklego menu) - jak wroga akcja we wsi; NIE sprawdzone w grze (zakaz autotestu w tej pracy).
  ZOBACZYC: menu spotkania z "Villagers of X", bitwa na mapie przy wiosce, ekrany lupu, powrot na mape i start rabunku. Inne partie w poblizu moga dolaczyc do bitwy
  (gra: `CheckNearbyPartiesToJoinPlayerMapEvent`) - jak przy kazdej bitwie.
- Druzyna tymczasowa nalezy do rodu pana okregu na czas bitwy: nasluchy tworzenia partii (DTE, LevyGold, PeopleLedger - tylko pomiar) ja widza. DTE moze dac jej
  komplet przy utworzeniu - to tylko kilkanascie-kilkadziesiat ludzi na chwile; zbrojownia znika z druzyna.
- Gra sama kasuje pokonana druzyne (A17: `HandleMapEventEndForPartyInternal`) i rozdziela jej szeregowych (jency do zwyciezcy, reszta "rozbici" do domow / lasu) - wtedy
  nasz koniec liczy straty = wszyscy obroncy (pamiec poleglych i linia moga zawyzyc o rozbitych, ludzie sie zgadzaja - rozbitych prowadzi A17 / OutlawLaw).
- Przegrana gracza: gracz w lochu druzyny chlopow (do ucieczki / wykupu gry; RealisticCaptivity widzi zwykla partie). ZOBACZYC, czy niewola u "Villagers of X" nie
  zawiesza menu niewoli.
- Pamiec poleglych to liczba obroncow, nie ludzi: ksiega wiosek nie trzyma ludzi osobno dla kazdej wioski (ludzie wioski = jej czesc ludzi okregu wedlug osad), wiec
  polegli schodza z ludzi okregu, a "z tej wioski" pilnuje pamiec poleglych (mniej obroncow tej wioski przez 60 dob).
- AI: plan kroku `DistrictRaid` (Room okregu) nie wie o wioskach pominietych z powodu obroncow - gdy miejsce maja juz tylko pominiete wioski, uderzenie trafia ludzi
  okregu, a osady nie plona (nadmiar w liczniku ksiegi `_dOver`, ta sama droga co okreg na pulapie). Rabunek wsi glownej przez gracza (`ForceMain`) i rabunek jednej
  wioski (`ForceUid`) - bez sprawdzania obroncow (gracz juz walczyl albo sie poddali).
- `Guarded` liczy ludzi w domu wioski (`SnapOf`) przy kazdym sprawdzeniu w lancuchu - kilkanascie odczytow na uderzenie, tylko w trakcie rabunku AI.
- Zapis w srodku bitwy / menu spotkania: druzyna jest zwykla partia gry (w zapisie gry), wpis `f=` w naszym; po wczytaniu straznik dokancza. Straznik nie patrzy
  na klucz - bitwa z wpisu dokonczona takze po przelaczeniu na NIE; przeglad sierot tylko przy `SideVillagesEnterable` = TAK.
- Kolizje: `VillageBook.Burn` przemianowany na `BurnCore` z opakowaniem (jedna sygnatura, te same wywolania); `Sess` ksiegi +2 pola; `SideVillageVisit.Raid.cs` -
  `RaidStart` / `TributeConsequence` rozbite na `BeginRaid` / `PayTribute` (ta sama arytmetyka i linie logu, w linii startu rabunku doszlo "(z menu wioski)").
  Przy scalaniu z galeziami 97: pliki wspolne jak w kroku 1 (`ArmouryBehavior.cs`, `Settings.cs`, `McmSettings.cs` od nowa z gen_mcm, `VillageBook.cs`).
- Zbudowane (kod 0). Bez gry i autotestu (zakaz w tej pracy).

**Status.** DO SPRAWDZENIA (zbudowane, kod 0; bez autotestu i bez gry).

**Test w grze (krok po kroku dla Jeffa).**
1. Wojna z krolestwem jakiejs wsi. Wjedz do jej wioski pobocznej (klik na mapie), "Take a hostile action": w tekscie "About N men of its P people would take up
   flails, spears and bows..." - N to mniej wiecej co dziesiaty mieszkaniec (wioska 150 ludzi - ok. 15).
2. Z druzyna mniejsza niz 4 x N: "Raid the village" - menu spotkania "Villagers of X", "Attack!" - bitwa z chlopami (cepy, wlocznie, kilka lukow). Wygraj: po ekranach
   lupu sam startuje pasek rabunku tej wioski. W logu Armoury `Wioski poboczne (WP): obroncy X ...: koniec bitwy ... wynik: gracz wygral`.
3. Drugi raz to samo w innej wiosce, ale w menu spotkania "Leave" - wioska nietknieta, komunikat "The villagers of X held their homes". W logu `wynik: gracz nie
   wygral`, `wrocilo do domow N`.
4. "Demand tribute" z mala druzyna - bitwa; po wygranej wioska placi (stawka gorna) i jest chroniona 30 dob.
5. Z duza druzyna (4 x N i wiecej) - "Raid the village" albo "Demand tribute": komunikat "...lay down their tools - they will not fight." i od razu pasek / okup.
6. Wroc do tej samej wioski zaraz po bitwie - obroncow mniej ("fewer than before - it has buried and nursed its men of late").
7. Zapisz w menu spotkania (przed "Attack"), wczytaj - spotkanie dalej, po bitwie wszystko jak w pkt 2.
8. MCM "Side Village Defenders" = NIE - znowu cala milicja wsi glownej (szara opcja przy przewadze milicji), bez bitwy w wiosce.
9. Automat: `python -I tools/sprawdz_logi.py --grupa m97obroncy <Armoury.log>` (OB-0..OB-6) i `--grupa m97wioski`.

**Decyzje (projektowal Claude; wylacznik `SideVillageDefenders`).**
- 1 na 10 i pulap 60: dorosli mezczyzni ok. 1/4 ludzi, do walki ok. 2 z 5 z nich; wioska bez pana i bez muru nie wystawi wiecej niz kilkudziesieciu.
- Poddanie przy 4:1 wedlug liczby ludzi (nie sily jednostek) - prosto do policzenia na oko w menu.
- Ranni obroncy, ktorych nikt nie pojmal, wracaja do domow (zyja); z ludzi schodza tylko polegli i pojmani. "Mniej rak" po bitwie liczy pamiec poleglych (60 dob).
- Okup po wygranej bitwie - stawka gorna (q = 5): obroncy rozbici, wioska nie ma czym sie targowac.
- AI bez symulacji bitwy: slabszy pomija wioske, silniejszy pali z ubytkiem obroncow wedlug przewagi.
