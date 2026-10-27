# PRZEKAZANIE 24.10.2026 (aktualne 22:30 CEST) - kontynuacja na drugim koncie (limit tygodnia tego konta 100%)

## DOPISEK 27.10 ok. 22:20 CET - 280+281+282 SCALONE (n161 6f30e9ac, Armoury 591d0ecc), AUTOTEST 131a w toku; paczka 284 karawany i wszystkie surowce
- Scalenie: git merge 280 (bez konfliktow), 281 i 282 (CHANGELOG/sprawdz_logi zlozone scal_sklad z bazy 9d807118), gen_mcm, build Release 0 bledow.
  Self-testy K280, E223, RU282, R277 TAK. Zapis testowy odswiezony z zapisu Jeffa 22:09 (zdjecie bazy usuniete - wrapper robi nowe).
- AUTOTEST 131a (zadanie btw7kzz1c, m113\seria-131a.out.txt): kopia,bitwaN,probyN 30 dob, flagi jak 130a. PO NIM (dla 280 B2): drugi bieg wczytujacy zapis
  zostawiony przez probyN, 4 doby (autotest-bitwa-204.ps1 -Days 4 -LoadSave autotest-przekierowany-IronmanKkUpHftX7ZIq) -> grupa ksiega280 K280-3/8/9.
  Grupy: ksiega280 e223 ruda282 nruda rody277 + 15 z 129a. Potem WGRANIE 131 (280+281+282).
- Jeff 22:10: karawana nic nie zarabia - PRAWDA: 'Caravan of Isa Storm' 22 doby: sprzedaz 22857, zakupy 17991, zysk ze sprzedazy -5071, zold 10890,
  netto -11890, wartosc 17744 -> 14703 (< kapitalu 15000, wyplat 0). Swiat: 88 karawan rodow, wyplacilo 13, 63 z kiesa na podlodze. Warsztat Silversmith
  Seagard ok. 208/dobe (4588 w 22 doby, podatek 1310). Jeff: 'rob te plotna skory wszystkie surowce aby ta gospodarka zyla'.
- 284 (zb
284, noc/284-karawany-surowce-przeplyw od 6f30e9ac; workflow wf_627247df-0c5): diagnoza strat karawan, zysk z prawdziwych roznic cen,
  bramka przeplywu 282 dla wszystkich towarow, kolejnosc zamowien i rowne pokrycie dla wszystkich surowcow warsztatow, marze warsztatow po 241; grupa handel284.
- 283 nadal w toku (wf_4a1d4aa3-b15). Log Jeffa calej sesji 20:56-22:09: D:\Backup-Bannerlord\logi-jeffa6-10-27-2210-po-grze\.
- Limit tygodnia 94% o 22:15.

## DOPISEK 27.10 ok. 22:40 CET - 282 GOTOWE (zbudowane, po recenzji, NIE scalone, NIE testowane); czeka 283
- 282 (zb
282): 90699a3e + 0564e798 + recenzja 04810741 (Armoury 0fac1b16). Przyczyna: karawany trzymaja ok. 1/4 zelaza swiata w jukach (1666-2463),
  prog zrodla 174 sumuje sie ponad polki, zamowienia do najtanszego zrodla nawet 8 dob drogi w dowolnej kolejnosci. F1 CaravanBulk.cs (bramka przeplywu rudy
  karawan, zapis arm_cb282), F2 najbardziej potrzebujace miasto pierwsze + zrodlo wedlug zysku na dobe drogi (7 surowcow), F3 rowne pokrycie rudy, F4 rece
  przy kopalniach licza towar w drodze, F5 linia Drewno bez 'bez wyjasnienia' (to nie wyciek). Grupa ruda282 (RU282-0..9), nruda NRU-2 zmienione.
  Do zrobienia potem (decyzja Claude): ta sama bramka dla plotna (ok. 2500 w jukach przy braku 430) i skory - teraz tylko 'na sucho' w linii 282.

## DOPISEK 27.10 ok. 22:10 CET - 280 i 281 GOTOWE (zbudowane, po recenzji, NIE scalone, NIE testowane)
- 280 (zb
280): 6e964978 + 673fb061 (Armoury 2a9f1216); recenzje 2 soczewki - 0 uwag. DayGate.cs (zapis arm_daygate, klucz DailyTickOncePerDay; P1 przestawia
  nastepny takt doby gry na kanoniczna pore po wczytaniu, prefiks Campaign.DailyTick pomija pierwszy takt z tym samym numerem), narodziny w ticku wsi
  (PopulationLaw.FloorPrefix/Finalizer, klucz BirthsAtVillageTick), udzialy band w zapisie (pd:), sw: v2 (polegli bez pochodzenia, reszta przy zapisie),
  reszta -5.4k ZAMROZONA i pokazywana osobno ('od 280'), B3 tekst, B4 'Bitwy: dzien N (doba ksiegi'. Grupa ksiega280. Test: bieg A 4 doby z zapisem,
  bieg B wczytanie zapisu z A 4 doby (K280-3 skok ~0), bitwy (K280-7). Zostaje 5-8 ludzi/dobe 'w nicosc ... inne' (E4/7) - poza 280.
- 281 (zb
281): bcb49094 + 52416361 + recenzja 7a2b03c3 (Armoury 8654c20e). Release() na kazdej drodze, ReleaseLate przed wczesnymi return, przeglad godzinny
  (mysl co 6 h), Restore wpisow z zapisu przed wczesnymi return; grupa e223 + E223-8, E223-9.
- Czekaja: 282 (wf_7236362f-1af), 283 (wf_4a1d4aa3-b15). Potem scalenie 4 galezi, autotest 131a, WGRANIE 131.

## DOPISEK 27.10 ok. 21:50 CET - paczka 283 reakcja na nekanie z tylu (workflow wf_13ad8807-a09); TYDZIEN 93%
- Jeff gra na 130 (sesja od 20:56, log skopiowany: D:\Backup-Bannerlord\logi-jeffa\2026-10-27-2145-bitwa\). Uwagi do bitwy (21:40): strzelal z 3 m w plecy lucznikow
  i piechoty (konno, potem pieszo) - lucznicy szli dalej, konnica ruszyla tylko czesciowo, piechota nie reagowala. Log 204 (linia 7304): wykryc 52, epizody 2,
  jezdzcy 3, lucznicy na cel 32, ale 'cel zdjety - swoi przy strzelcu 19' i powroty 'swoi przy strzelcu 204' (straznik FF po bliskosci zamiast linii strzalu);
  204 liczy tylko KONNYCH strzelcow - pieszy gracz bez reakcji.
- 283 (zb\n283, noc/283-reakcja-na-nekanie-z-tylu od 9d807118): zatrzymanie/obrot szyku pod ostrzalem z tylu/boku, lucznicy strzelaja (FF = linia strzalu),
  cala konnica na konnego, oddzial piechoty na pieszego, wodz wstrzymuje marsz; autotest -Harasser rozszerzony o pieszego i pozycje tuz za lucznikami.
- 21:57 Jeff doprecyzowal: konnica w zwiazanej walce walczy dalej, chyba ze ostrzelany szyk wazniejszy albo jest w nim wodz party (wodz chroniony); atak gracza
  mieczem na lucznikow = lucznicy strzelaja, piechota biegnie, konnica (niezwiazana) szarzuje. Run wf_13ad8807-a09 ZATRZYMANY, nowy: wf_20af758b-7a4.
- 22:00 Jeff: "ja mam ci pisac kazde zachowanie ... nie umiesz sam wydedukowac" -> 283 od nowa jako PELNA MACIERZ reakcji (zagrozenie x typ oddzialu x stan),
  przyklady Jeffa = przypadki testowe; grupa nekanie283. Run wf_20af758b-7a4 ZATRZYMANY, nowy: wf_4a1d4aa3-b15.
- Wznowienie: scriptPath paczki-280-281-wf_d8c1bd65-fa1.js, resumeFromRunId wf_4a1d4aa3-b15 (args w journal.jsonl).
- W toku rownolegle: 280+281 (wf_d8c1bd65-fa1), 282 (wf_7236362f-1af), 283. Potem: scalenie wszystkich czterech do n161 (git merge + scal_sklad), autotest 131a
  (bitwaN z -Harasser + probyN 30 dob + wczytanie zapisu dla 280 B2), grupy ksiega280 e223 ruda282 m204jazda/nekanie283 + reszta, WGRANIE 131 gdy Jeff nie gra.
- LIMIT TYGODNIA 93% o 21:45 - przy 99% zatrzymac sie i przekazac drugiemu kontu (wszystkie runy wznawialne z journal.jsonl).

## DOPISEK 27.10 ok. 21:15 CET - paczka 282 ruda (workflow wf_7236362f-1af) obok 280/281
- Jeff pytal o inwestycje (odpowiedz z logu 12:57: kuznia ~123 d/dobe, tkalnia welny ~98, len ~68; tartak ~0, rzeznik -5, platnerz zbroi konskich -8 - zmiana branzy
  E3-D3 po 60 dobach strat), potem: "tak zob paczke, ale ta rude to my chyba juz ktoreys raz naprawiamy i mowiles ze naprawione".
- 282 (zb\n282, noc/282-ruda-przeplyw od 9d807118): DIAGNOZA czemu f5-ruda / 174 / 174b / 214 nie dzialaja w kampanii Jeffa (28 z 97 miast bez rudy, kontrakty rudy
  ~0, zapas w Norvos/Oldtown/Fairmarket/Lannisport), potem naprawa przyczyny; tez 'Drewno ... bez wyjasnienia -1562/dobe'. Grupa ruda282.
- Wznowienie: ten sam scriptPath co 280/281 (paczki-280-281-wf_d8c1bd65-fa1.js), resumeFromRunId wf_7236362f-1af, args z journal.jsonl runu.
- Potem: scalenie 280+281+282 do n161, autotest 131a, WGRANIE 131.

## DOPISEK 27.10 ok. 20:55 CET - Jeff "tak napraw wszystko": paczki 280 i 281 w toku (workflow wf_d8c1bd65-fa1)
- 280 (zb\n280, noc/280-ksiega-doba-po-wczytaniu od n161 9d807118 = WGRANE 130): B1 narodziny wsi ksiegowane w ticku wsi (nie prognoza doby moda; niezaleznie od
  Measure40.On), reszta niewyjasnionych skokow, decyzja o -5.4k w zapisie Jeffa; B2 centralny straznik doby moda (raz na numer doby, nadrobienie pominietej, zapis);
  B3 tekst linii 'Ludzie (ksiega)'; B4 dzien w linii 'Bitwy'; B5 licznik poleglych bez pochodzenia; grupa sprawdz_logi ksiega280. Projekt + wykonanie + 2 recenzje.
- 281 (zb\n281, noc/281-eskorta-postoj-12-dob od 9d807118): LordEscort.ThinkPostfix - zwolnienie po 12 dobach na kazdej drodze (wczesne return 410-414), bez
  zostawionych NoAtk/kolejek przy glodzie; E223-5 ma przejsc.
- Wznowienie: Workflow scriptPath ~\.claude\projects\C--Program-Files--x86--...\1e1c276a-...\workflows\scripts\paczki-280-281-wf_d8c1bd65-fa1.js,
  resumeFromRunId wf_d8c1bd65-fa1, te same args (w journal.jsonl runu). Potem: scalenie obu do n161 (git merge + scal_sklad dla CHANGELOG i sprawdz_logi z bazy
  9d807118), build, autotest 131a (bitwaN + probyN 30 dob + bieg z wczytaniem zapisu - B2), grupy ksiega280 + e223 + reszta, WGRANIE 131 gdy Jeff nie gra.
- Limit: tydzien 90% o 20:50 (do 99% wolno, potem przekazanie na drugie konto).

## DOPISEK 27.10 ok. 20:45 CET - WGRANIE 130 (277 rody kanonu Zachodu); nic w toku, czeka odpowiedz Jeffa o 280
- Autotest 130a (20:10-20:40): bitwaN + probyN 30 dob (109002 -> 109031) kod 0, zapisy Jeffa nietkniete, bledy CrashScribe 27/25 jak 128a. Grupy (m113\wyniki\grupy-130a):
  rody277 13 progow NIE 0 w obu biegach; przeniesienie na kopii zapisu Jeffa: panow 11 (Spicer rycerski do Krwawych Godow), rycerzy z nazwa kanonu 4 (Greenfield,
  Hetherspoon, Lorch, Ruttiger), nowe druzyny (limit 190). NIE tylko: K259-7 T2 w bitwaN (jak 129a) i E223-5 "najdluzej w stanie" 27.1 dob (Ookpik Wolfcaller,
  Wolni Ludzie) - starsze 223: LordEscort.ThinkPostfix wychodzi (Unready/CapTrip, DoNotChangeBehavior, IsStarving - linie 410-414) PRZED zwolnieniem po 12 dobach
  (:469-477), stan wisi; 277 LordEscort.cs nie rusza. Do poprawki osobno (razem z 280?).
- WGRANIE 130 o 20:42 (scratchpad\wgraj130.ps1): Armoury.dll 88d10a9a (n161 12eb9216) + ModuleData\arm_houses_canon.tsv 5e1d8c20; kopie .bak-2026-10-27-przed-wgranie130
  (DLL i tsv) i D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie130-przed. Zapis testowy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-27-130a.
  CHANGELOG 1c0059d2 + 9d807118 (wypchniete). Przy pierwszym wczytaniu kampanii Jeffa: przeniesienie raz (nazwy i herby NIEODWRACALNE w Ironmanie).
- Do sprawdzenia w logu Jeffa po jego grze: linie "Rody kanonu (277)" (przeniesienie raz, panowie 11, druzyny), stosunek B/A w linii 274.
- Czeka na Jeffa: paczka 280 (B1-B4 z dopisku 19:55) + E223-5.

## DOPISEK 27.10 ok. 20:15 CET - 277 gotowe i scalone (n161 12eb9216, build 88d10a9a), AUTOTEST 130a w toku
- 277 (zb\n277): 6ff77201 + fbd0f287 + recenzja 653bdca6 (Armoury 711c6712). CanonHouses277.cs/.Pay.cs/.Test.cs; 12 rodow Zachodu i ziem koronnych z rycerzy BK korony
  panami (nazwa i herb z arm_houses_canon.tsv, pelne parostwo BK, tier 3, druzyna glowy SpawnLordParty); zold od pana lennego regula W5 (Lannister / Joffrey);
  raz na zapis (arm_rody277); Spicer rycerski do Krwawych Godow; klucze "Canon Houses of the West" / "- Book Names" / "- Liege Pays the Host"; grupa rody277.
  Szacunek B/A 0.50 -> 0.54-0.58 (reszte trzyma 274). NIEODWRACALNE w Ironmanie: nazwy i herby rodow zostaja w zapisie (klucz wylacza tylko zold i nowe druzyny).
  Dane: arm_houses_canon.tsv (status lordly 6 rodow ziem koronnych) - przy wgraniu 130 RAZEM z DLL.
- Scalenie: git merge bez konfliktow (12eb9216), build Release kod 0, Armoury 88d10a9a.
- AUTOTEST 130a (od ok. 20:10, zadanie bn33kv8y4, m113\seria-130a.out.txt): kopia,bitwaN,probyN -ProbyDays 30, flagi jak 129a + armoury-rody277.flag.
  tsv 277 wgrany do gry NA CZAS TESTU, kopia 129: D:\Backup-Bannerlord\wgrane\2026-10-27-test130a-tsv (komenda sama przywraca po tescie - SPRAWDZIC md5).
  Potem grupy (rody277 + 15 z 129a), zapis testowy na D:, WGRANIE 130 = DLL + arm_houses_canon.tsv, gdy Jeff nie gra.
- Jeff pytany 19:55 o paczke 280 (B1 narodziny przy wczytaniu, B2 podwojna/brakujaca doba moda po wczytaniu, B3 tekst) - czeka na odpowiedz.

## DOPISEK 27.10 ok. 19:55 CET - straty ludnosci: wynik sprawdzenia (wf_ac2622a4-2b8, 2 sledztwa + 2 recenzje)
- Straty SA odejmowane trwale: rekrut schodzi z DOM przy werbunku (FromVillage hearth -1/k, FromTown stan E4/4; PeopleBook.Origin.cs:1187-1228), polegly nie wraca
  (DieShares Fallen, Origin.cs:811-827), wracaja tylko zywi z pochodzeniem. Narodziny = wlasny model PopulationLaw.GrowthOf (0.5%/rok + dobrobyt - wojna - glod),
  NIE zalezy od poleglych (zbieznosc +86.6k ~ +86.9k w oknie 108768-109002 to zbieg; wojna obniza g z 0.60% do 0.41%/rok). Od siewu 108648 zywych +30k.
- Skutki w grze: mniej ludzi = mniejszy plon wsi, renta/oplaty wsi, rynek miasta, milicja. NIE: dobrobyt miast (ksiega 10.43 mln vs z dobrobytu 14.77 mln,
  ZC6 wylaczone - decyzja projektu), miejsca u notabli (LevyByPeople nie). Hamulec wolnych rak dziala w 1 regionie z 227 (ibbenese).
- BLEDY (do paczki - Jeff pytany o zgode 19:55):
  B1 narodziny wsi ksiegowane raz na dobe moda jako prognoza pelnej doby (PopulationLaw.cs:970-988), a hearth rosnie w ciaglym ticku wsi (zapisywanym); doba moda
     dostaje nowa pore po wczytaniu (Campaign.CreateCampaignEvents initialWait) -> kazde wczytanie zmienia 'swiat' o (W-1 doby) x narodziny (-707..+112).
     Poprawka: NoteBorn w ticku wsi (wlasna latka, NIE zalezna od Measure40.On = LogEnabled && PeopleMeasureEnabled), w GrowthDaily tylko log. Czesc skokow
     (-150..-270 przy 108984 i 108987, dryf w sesjach wojennych -6..-15/dobe) niewyjasniona.
  B2 po wczytaniu cala doba moda (OnDailyTick) moze isc 2x tego samego dnia gry (W<1) albo przepasc (W>1); brak straznika (ArmouryBehavior.cs:2733, Campaign.cs:1002).
     W logach: podwojne 108829, 108836, brak 108986. Czesc modulow ma wlasny straznik numeru doby (ArmsScrap, KsHunger, PlagueWatch, MapFields, InfluenceWatch, BkrotChains).
  B3 tekst linii 'Ludzie (ksiega)': 'werbunek jeszcze nie zdejmuje z DOM' (PeopleBook.cs:1177, komentarze :127, ClanBudget.cs:1634) - nieaktualny.
  B4 kosmetyka: 'Bitwy: dzien N' = dzien ksiegi + 1.
- Dane: scratchpad\ksiega_ludzi.py/.tsv, krainy.py; pelny wynik: tasks\w0tuyln4t.output (sesja 1e1c276a).

## DOPISEK 27.10 ok. 19:25 CET - WGRANIE 129 (241+259+264+268+272+274); w toku 277 i sprawdzenie strat ludnosci
- Autotest 129a (18:50-19:15): bitwaN + probyN kod 0, zapisy Jeffa nietkniete, bledy CrashScribe 25 = jak 128a (cudze latki Harmony). 15 grup sprawdz_logi x 2 biegi
  (war241 karawany259 pokoj264 z268 krol272 rownowaga274 p202 p203 m204jazda odbicie222 e223 m109muz m165mapa m6a nwar) - NIE 0, jedyny NIE: K259-7 T2 w bitwaN (bieg
  bitew za krotki; w probyN T2 TAK). Wyniki: m113\wyniki\grupy-129a\. NWAR-10 tym razem TAK.
- WGRANIE 129 o 19:19: Armoury.dll a25f28c0 (n161 2a96d15b), skrypt scratchpad\wgraj129.ps1; kopie Armoury.dll.bak-2026-10-27-przed-wgranie129 i
  D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie129-przed. Pliki danych bez zmian od 128. Zapis testowy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-27-129a.
  CHANGELOG statusy 6 paczek = WGRANE 129 (086fc0c9, wypchniete).
- Jeff 19:05: "sprawdz w logu ile osob pochlonela wojna, ile ludnosci stracily wioski i miasta i czy straty sa odejmowane od liczby ludnosci".
  Dane (scratchpad\ksiega_ludzi.py -> ksiega_ludzi.tsv, krainy.py): dzien 108768 -> 109002: polegli +86 876 (od siewu 108648: 108 478), cywile 1 176, wsie -102 tys.,
  miasta -42 tys. (razem -0.28%), SLUZBA +104 tys., uchodzcy 3.6 -> 28 tys., narodziny netto +86.6 tys.; reszta rownania krain -1582 -> -5365 (skoki przy wczytaniu).
  Weryfikacja w kodzie: workflow wf_ac2622a4-2b8 (rachunek strat + dryf rownania, z recenzja).
- Nastepne: 277 (wf_f21d9616-2f0) -> scalenie do n161 -> autotest 130 -> WGRANIE 130.

## DOPISEK 27.10 ok. 19:20 CET - paczka 277 rody kanonu Zachodu/Ziem Koronnych "po ksiazce"; autotest 129a w toku
- Jeff 19:10: "Czemu lannisterowie maja tak malo rodow, w ksiazce tez tak malo mieli?" -> odpowiedz: w ksiazce Tywin ok. 20k + Jaime ok. 15k, dziesiatki rodow
  Zachodu; w ROT strona korony 50 rodow, 17 z ziemia, 19 twierdz (strona Robba 95 / 34 / 38). Pieniadze NIE sa przyczyna (KL 6.2 mln skarbiec). Kredyt W3 niepotrzebny.
  Jeff: "ksiazce" -> paczka 277 (zb\n277, noc/277-rody-kanonu-zachod od n161 2a96d15b, workflow wf_f21d9616-2f0): rozpoznanie rodow kanonu, potem siedziby/tier/drużyny
  wedlug ksiazek, bez zabierania stronie Robba, migracja zapisu Jeffa raz (flaga), klucz MCM, linia 'Rody kanonu (277)'.
- Autotest 129a (od 18:50, m113\seria-129a.out.txt; 6 paczek, build a25f28c0). Po nim: grupy (war241 karawany259 pokoj264 z268 krol272 rownowaga274 + stare),
  zapis testowy na D:, WGRANIE 129 (bez 277) gdy Jeff nie gra; 277 osobno potem (130).

## DOPISEK 27.10 ok. 18:50 CET - 274 gotowe; WSZYSTKIE 6 PACZEK SCALONE w n161 (2a96d15b, Armoury a25f28c0, NIE wgrane); nastepny krok AUTOTEST 129a
- 274 (zb\n274): 3b37dd98 + 22a7ca04 + recenzja 48710016 (cc91a05c). CanonWarClock.cs, CanonWarBalance(.Levers/.Restraint/.Test).cs; klucze Canon War Balance /
  Restraint / Muster; zapis arm_cwb274; koniec przy RedWeddingEvent >= 2 albo smierci Robba (jak 264). Przyczyna slabosci B: ROT - B ma 17 rodow z lennami i 19
  twierdz, A 34 i 38 (pelne B to ok. 0.78 A). Siedziby z "strona domowa" (odbicie wlasnej siedziby bez weta). Decyzja dla Jeffa: W3 (wiekszy kredyt 168 dla slabszego).
- Scalenie 274: konflikt Settings.cs (bloki 272 i 274 - oba), McmSettings z gen_mcm, CHANGELOG i sprawdz_logi scal_sklad z bazy 056ae5f4 i 6 galezi.
- AUTOTEST 129a do zrobienia (gra wylaczona o 18:49): wrapper m113\testy-noc-204.ps1 -Tree n161 -Dll mfinal129a -Runs kopia,bitwaN,probyN -Harasser 3
  -CsAt test\CrashScribe-at-n204.dll -ExtraFlags armoury-dzwiek-bitwy.flag,armoury-pokoj264.flag,armoury-zbroja268.flag,armoury-rownowaga274.flag,armoury-karawany.flag
  (+ odswiezyc zapis testowy z zapisu Jeffa, przeniesc stare zdjecie bazy m113\zapisy na D:; logi Jeffa z dnia na D: przed testem). Potem grupy war241 karawany259
  pokoj264 z268 krol272 rownowaga274 + stare; dlugi bieg swiata (241/259/274) osobno.

## DOPISEK 27.10 ok. 20:00 CET - 268, 272 gotowe; 241+259+264+268+272 SCALONE w n161 (cf193290, build 2a226448, NIE wgrane); 274 w toku
- 268 (zb\n268): 9fc4889c + c087422c + recenzja 03d5313c. p9 liczy B (udzial ludzi ze zbroja korpusu), ArmourOrders.cs (zamowienia zbroi/helmow pelne, cap 60),
  E3-Z1 bez limitu 1/dzien dla korpusu i helmu, klucze AiArmsCountsBodyArmour, AiArmourOrderFull; flaga armoury-zbroja268.flag, grupa z268.
- 272 (zb\n272): 25436c96 + 05eee9cc + recenzja 45048904. KingInTheNorth.cs (zapis arm_k272, klucz CanonNorthRiverlandsUnion): unia po ROT RiverlandsDeclareWarEvent
  stan 2 albo 2 dni od daty z planu Fabuli, Robb zyje, przed Krwawymi Godami; rody przez ApplyByJoinToKingdom / najemnicy ApplyByJoinFactionAsMercenary (kariera BK
  przepisana); skarbiec: splata Banku, reszta do Polnocy; DestroyKingdomAction Dorzecza; okno "The King in the North and the Trident"; ExecuteRobb postfiks
  (Freyowie, Mootonowie, Coxowie, Brackenowie do korony). Grupa krol272.
- SCALANIE n161: 241 (ff), 259 ab0f28b2, 264 2deb7796 (konflikt konstruktora ArmouryBehavior - suma Reset()), 268 a00425ea, 272 96c073f3; CHANGELOG i sprawdz_logi
  zlozone scal_sklad z bazy 056ae5f4 (cf193290). Wszystkie grupy (war241 karawany259 pokoj264 z268 krol272 + stare) daja tabele; NWAR-10 NIE starsze (tez na 056ae5f4).
- Po 274: scalic 274 (git merge + scal_sklad z 6 galeziami), build, autotest: bitwaN (flagi pokoj264, zbroja268) + dlugi bieg probyN (120 dob dla 241/259/274, flagi
  krol272/odbicie) -> grupy -> wgranie 129 po zamknieciu gry.
- Na pozniej (z audytu): daniny z miast od biezacego zysku, pamiec cen karawan w zapisie, karawany z zysku, sprzedaz na przyszlosc, licznik podatku 2 wsi BK (NaN),
  ubytek przy budowach, log trafien, NWAR-10 (pola KS w F4 6.74x).

## DOPISEK 27.10 ok. 18:40 CET - 241 i 259 GOTOWE (zbudowane po recenzji, nie wgrane)
- 241 (zb\n241): 6cf804cd + 56485d08 + recenzja 237e6c60 (Armoury 0a0b45e8). MarketSales.cs (sprzedaz miast 28 dni, zapis arm_mktsales); kupiec placi cene z
  historii sprzedazy albo pulap ludu; rzemieslnicy 35% (TownFolkCraftWageShare); WorkshopTaxOnPayout (podatek BK od wyplaty, 1x stawka); ClampBack (BK placi ponad
  kase miasta -> cofniete z kapitalu warsztatu). sprawdz_logi --grupa war241. Test: 120 dob swiata (liczby warsztatow, ceny bizuterii/aksamitu/narzedzi, kasy miast).
- 259 (zb\n259): 7da9cfd1 + 361f8451 + recenzja 6366da19 (Armoury f0381e70). Rozliczenie karawan rodow raz na dobe w prefiksie AddIncomeFromParty; kapital =
  min(model, dane przez E3/7) (arm_259_kapital); BK wplata i E3/9 dla nich wylaczone; linia "Majatek gracza (karawany i warsztaty)"; sprawdz_logi --grupa karawany259.
- W toku: 268+272 (wf_755f2f7e-043), 274 (wf_1f03e327-b32). Potem scalanie (scal_sklad dla sprawdz_logi i CHANGELOG), autotest dlugi.

## DOPISEK 27.10 ok. 17:35 CET - 264 GOTOWE (zbudowane po recenzji, nie wgrane)
- 264 (zb\n264, noc/264-pokoj-wojny-kanonu): 114ba627 + 34feb90a, recenzja 351fac2a (Armoury be852bc6). CanonPeaceLock.cs (klucz CanonWarNoPeaceUntilRedWedding):
  pary battania-vlandia i riverlands-vlandia; blokada do RedWeddingEvent ROT stan 2 (zakonczone) albo smierci Robba (lord_5_1) - ZNACZNIK DLA 274; Diplomacy
  ApplyPeace / Kingdom.AddDecision / MakePeaceKingdomDecision.IsAllowed; gracz-wladca: wybor tylko gdy Diplomacy daje Decline. PeaceHold (M6a-3) czeka do 72 h na ROT.
  Data wojny z rekordu gry. CanonWarWatch.cs: 4 linie wladcow (Robb, Joffrey, Tully, rod Tywina) + 3 linie krolestw dziennie.
- Autotest: flaga CrashScribe\armoury-pokoj264.flag, sprawdz_logi --grupa pokoj264 (P264-7 T1-T7 TAK, P264-0 4 haki, P264-5 linie co dobe); potem m6a i m104wojny.
- W toku: 241+259 (wf_d5f20a7a-22b), 268+272 (wf_755f2f7e-043), 274 (wf_1f03e327-b32).

## DOPISEK 27.10 ok. 17:00 CET - paczka 274 rownowaga wojny kanonu do Krwawych Godow (Jeff)
- Jeff 17:00: "lannisterowie i krolewska przystan byli minimum porownywalnie tak samo silni jak polnoc, chodzi o to zeby zadna ze stron nie uzyskala wyraznej
  przewagi az do krwawych godow aby walczyli i aby sily byly porownywalne dorzecze + polnoc i lannisterowie i krolewska przystan".
- Stan z audytu: strona Robba ok. 17.7 tys. w polu (Polnoc 11.0k + Dorzecze 6.7k) wobec KL 7.9k.
- 274 (zb\n274, noc/274-rownowaga-wojny-kanonu, workflow wf_1f03e327-b32): najpierw przyczyny slabosci KL, potem dzwignie tylko z zamknietej ekonomii
  (kredyt Banku/Iron Bank, rekruci z prawdziwej ludnosci, najemnicy za kredyt, pelne druzyny, armie), powsciagliwosc silniejszego (bez oblezen gniazd rodow przed
  Krwawymi Godami), obie strony walcza; wylacznik po znaczniku Krwawych Godow (wspolny z 264); zgodnosc z 272 (Krol Polnocy). Test 120 dob.
- W toku: 264 (wf_09849f4a-635), 241+259 (wf_d5f20a7a-22b), 268+272 (wf_755f2f7e-043), 274 (wf_1f03e327-b32). Limit 5h 24%, tydzien 80% (17:00).

## DOPISEK 27.10 ok. 16:45 CET - po odnowieniu limitu: 264 wznowione, 241+259 i 268+272 w toku
- Workflowy (skrypt workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js): 264 wf_09849f4a-635 (wznowiony, args scratchpad\args-264.json),
  241+259 wf_d5f20a7a-22b, 268+272 wf_755f2f7e-043. Przy przerwaniu: wznowic resumeFromRunId z TYMI SAMYMI args (z pliku runu w workflows\wf_*.json, pole args).
- Uwaga: skrypt tnie liste potwierdzonych uwag dla poprawiajacego do 20000 znakow - po wyniku sprawdzic w journal.jsonl, czy wszystkie potwierdzone uwagi poprawione.
- Limit 5h 0% o 16:42, tydzien 74%.

## DOPISEK 27.10 ok. 15:35 CET - PAUZA do odnowienia limitu (Jeff: "zrob pauze i kontynuuj po resecie limitu")
- 264 zatrzymane (run wf_09849f4a-635: projekt gotowy w pamieci podrecznej, wykonanie przerwane - drzewo zb\n264 czyste). Wznowienie po 16:40:
  Workflow(scriptPath=workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js, resumeFromRunId=wf_09849f4a-635, args = scratchpad 1e1c276a...\scratchpad\args-264.json).
- Potem uruchomic 241, 259, 268, 272 (opis w dopisku 15:30). Budzik w tle na 16:42.

## DOPISEK 27.10 ok. 15:30 CET - AUDYT GOTOWY (docs/AUDYT-2026-10-27.md); Jeff: "tak zacznij od tych pieciu" + Krol Polnocy
- Jeff 27.10 ok. 15:25: "tak zacznij od tych pieciu, a tak potem jak dolaczaja do roberta to staja sie jednym krolestwem King in the North".
- Paczki (galezie od noc/161-integracja 056ae5f4 = WGRANE 128, drzewa scratchpad fffc991e...\zb\nNNN):
  241 warsztaty-cena-rynkowa-podatek: "cena sprawiedliwa" -> kupiec miejski placi tyle, za ile odsprzeda (srednia sprzedazy 28 dni albo cena graniczna ludu 1.5x
      wartosci), producent dostaje cene rynkowa; podatek BK warsztatow liczony od dziennej wyplaty (raz), cena warsztatu od 1x stawki (audyt pkt 1.1 i 1.2). Test 120 dob.
  259 karawany-gracza-rozliczenie: karawany gracza (i lordow), ktorym zold placi rod: codziennie nadwyzka ponad kapital z modelu gry (10 000 / 17 500 elitarna /
      30 000 morska, bez rezerwy 30 dni zoldu) do wlasciciela przelewem, bez podwojnej wyplaty z gry; linia logu + komunikat "Twoje karawany i warsztaty" (audyt pkt 4).
  264 pokoj-wojny-kanonu: pokoj z decyzji krolestwa zablokowany w wojnie kanonu do Krwawych Godow, sprawdzenie jencow M6a-3 po ROT, data wojny w logu, linia planow
      wladcow (Robb) - URUCHOMIONE 15:26 (workflow wf_09849f4a-635).
  268 gotowosc-zbroja: ocena gotowosci p9 liczy zbroje na tulow (prog 60% jak bron), zamowienia zbroi bez limitu 1/dzien, braki liczone w miescie (audyt pkt 2).
  272 krol-polnocy: Polnoc i Dorzecze jednym krolestwem, gdy Dorzecze dolacza do Robba (kanon: Robb ogloszony Krolem Polnocy i Tridentu w Riverrun) - wydarzenie
      (Kronika/prolog), przeniesienie rodow Dorzecza do krolestwa Polnocy, dyplomacja, przynaleznosc gracza (Jeff jest w Dorzeczu?) - najpierw projekt.
- Limit 5h 82% o 15:25 (odnowienie 16:40) -> 241, 259, 268, 272 uruchomic po 16:42 (budzik w tle). Skrypt workflow paczek: workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js
  (args: lista paczek {id, wt, br, design, auth, task, lens1, lens2}). Po paczkach: scalanie TYLKO scal_sklad.py/scal_wstawki.py, self-testy i pelne grupy sprawdz_logi,
  autotest (bitwaN + probyN, przy 241 i 259 dluzszy bieg), wgranie po zamknieciu gry.

## DOPISEK 27.10 ok. 15:10 CET - AUDYT GRY JEFFA W TOKU (po awarii pradu); w grze 128
- Jeff 27.10 ok. 13:45: "zaproponuj naprawy ... czy ekonomia dziala poprawnie, czemu wojsko czesto nie ma pancerza albo ma tylko ja zle trafiam i mam pecha, i czemu
  dorzecze i polnoc jest slabsza od lannisterow i krolewskiej przystani to nie ma sensu, zrob audyt ... i te karawany". Wczesniej (13:40) odpowiedz o karawanie
  (kapital obrotowy max(10000, 30 dni zoldu) w kiesie, BK placi tylko w osadzie wlasciciela - Jeff bez twierdzy) i warsztacie (cena sprawiedliwa: koszt + 25%,
  nadmiar bizuterii 6/dobe vs popyt 1, podatek BK 57.8% w Lannisport; wyplata ok. 22 zl/dobe).
- Kopie logow do audytu: scratchpad 1e1c276a...\scratchpad\audyt (Armoury-2026-10-27_12-57-35.log, Armoury-2026-10-26_21-30-37.log, session-2026-10-27_12-57-33.log).
- Workflow audyt-gry-jeffa-27-10 (run wf_02052cf7-373; skrypt workflows\scripts\audyt-gry-jeffa-27-10-wf_02052cf7-373.js): 4 sledztwa (ekonomia, pancerz, krolestwa,
  karawany) + sceptycy + synteza po polsku. Po awarii pradu wznowiony z resumeFromRunId (15:08). Wynik = propozycje dla Jeffa, NIC nie wdrazac bez jego zgody.
- Po awarii sprawdzone: Armoury 35f2df7e / CrashScribe bef9db58 w grze, zapis Jeffa (13:49) bez zer, repo i n161 bez wyzerowanych plikow.

## DOPISEK 27.10 ok. 02:00 CET - WGRANE 128 (222d); nic w toku
- WGRANE 128 (01:58): Armoury 35f2df7e (n161 6eda5012, status 056ae5f4, wypchniete) = 127 + 222d. Kopie: Armoury.dll.bak-2026-10-27-przed-wgranie128 i
  D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie128-przed (a06033ef = 127).
- 222d (20719bda + recenzja 6eda5012): zimny trop -> lordowie w zasiegu (droga gry, ReachOwnDays/ReachHelpDays) szukaja wokol osady najblizszej miejsca pojmania
  albo twierdzy rodu porywacza przez 72 h od swojej wiesci; nikt w zasiegu -> "czeka (poza zasiegiem)", budzi sie przy sladzie / wiesci w zasiegu / lochu;
  komunikat wedlug wyniku (ride to free / searching / too far away - for now / not strong enough - for now). Okno wiesci (Heard) w zapisie (pole 24).
- Autotest 128a (probyN 8 dob, zapis Jeffa z Tywinem): kod 0, ERROR 0, odbicie222 12 progow 0 NIE; Tywin: wiesc po 140.4 h, czeka (poza zasiegiem), T9 TAK.
- Do sprawdzenia u Jeffa w grze: ekran druzyny (202), jazda AI na konnego strzelca (204 - autotest bez wrogiej jazdy), komunikat o Tywinie po ok. 6 dobach gry,
  muzyka bitwy -40%, bieg w zbroi i kon (203), eskorty lordow (223).
- Zapis testowy na D:\Backup-Bannerlord\zapisy-testowe-archiwum (..._po-128a.sav). W Game Saves brak autotest-*.

## DOPISEK 27.10 ok. 01:15 CET - WGRANE 127 (202, 203, 204, 222/222c, 223/223b); w toku 222d
- WGRANE 127 (01:10): Armoury a06033ef (n161 6e5a9186, statusy a485ec5a, wypchniete n161 i galezie 202/203/204/222/223) + 40 ogg Bitwa\lufs21.4/24.4/27.4/30.4.
  Kopie: Armoury.dll.bak-2026-10-27-przed-wgranie127 i D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie127-przed (f67326e2 = 126, lista pliki-127.txt).
- Autotesty: 127a (bitwaN + 3 konni lucznicy CrashScribe-at-n204, probyN 6 dob; build c6b74f23) i 127b (probyN 8 dob; a06033ef): kod 0, ERROR 0, 0 NIE.
  203: muzyka bitwy z lufs24.4 przy suwaku 3, bieg w zbroi gracza 39 -> 70 s, kon 47 -> 94 s. 223: slabe partie 358/2138, 16 eskort (935 ludzi), wladcy/glowy slabe 42 -> 19.
  204: wykrycia 158 (pudla 28), koszt 0.02 ms - reakcji jazdy autotest NIE sprawdzil (wrog bez jazdy) - sprawdzi gra Jeffa.
  222c: wiesc o Tywinie po 139 h (bylo 13 dob); ale potem "trop zimny" (wszyscy lowcy 850 jedn. dalej) a komunikat "Their lords ride to free him" i tak sie pokazuje -> 222d.
- NAPRAWY NARZEDZI: tools/sprawdz_logi.py zlozony od nowa (fb66a074) - scalenia "zostaw oba" w 202/203/204/222 zgubily konce p202_main/p203_main.
  Odtad scalanie: scratchpad 1e1c276a...\scratchpad\scal_sklad.py <plik> <BAZA> <galezie...> albo scal_wstawki.py; po scaleniu uruchomic KAZDA grupe w calosci.
- W toku: workflow 222d wf_2d1e8111-b57 (zimny trop -> szukanie w okolicy pojmania i lennach porywacza; nikt w zasiegu -> czeka; uczciwy komunikat). Potem autotest + WGRANIE 128.
- Zapis testowy przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum (autotest-...-2026-10-27_po-127b.sav); w m113\zapisy jest swieze zdjecie bazy z zapisu Jeffa 26.10 22:19.

## DOPISEK 27.10 ok. 00:20 CET - n161 = 126 + 202 + 203 + 204 + 222 + 223 (584f9f96, Armoury c6b74f23); AUTOTEST 127a W TOKU
- Scalenia: 202+203 f3413043, 204 e4817be0, 222 7a51c053 (ZEPSUTE sprawdz_logi - naprawa 99956c53), 223 8d7becef, polaczenie 222+223 584f9f96
  (RescueHunt.Hunters pomija LordEscort.HeldBack). UWAGA: rozwiaz_oba.py ("zostaw oba") psuje sprawdz_logi, gdy git sparuje wspolne konce funkcji
  dwoch galezi - uzywac scratchpad 1e1c276a...\scratchpad\scal_wstawki.py <plik> [OURS THEIRS [BAZA]] (wstawia bloki dopisane wzgledem bazy; odmawia,
  gdy galaz cos zmienia) i ZAWSZE sprawdzic self-testy po scaleniu (ladowanie pliku ok. 60 s).
- 204 dokonczone: 65fc23dc + 5a63aa89 (koszt passu, lamiaca sie strona, FF lucznikow co pass). Testowy CrashScribe-at-n204.dll (test\, md5 6160e8ef = n103 +
  konni lucznicy "harasser"); wrapper m113\testy-noc-204.ps1 -Harasser 3.
- 222 recheck 038ffb78 (odsiecz OB5 wladcy), 223 recheck 5dd6d0d2 (223b znani lordowie: wielkie rody kanonu, glowy bez lenna, postacie ksiazek) + 5f050614.
- Zapis testowy odswiezony z zapisu Jeffa 26.10 22:19 (Tywin w niewoli); stare zdjecie bazy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum. Logi Jeffa z 26.10
  recznie na D:\Backup-Bannerlord\logi-jeffa\2026-10-26-wieczor-dodatkowo (bieg "kopia" po polnocy ich nie bral).
- 00:14 AUTOTEST 127a (m113\seria-127a.out.txt): kopia, bitwaN (harasser 3), probyN 6 dob, flagi dzwiek-bitwy + odbicie. Potem sprawdz_logi grupy p202 p203
  m204jazda odbicie222 e223 m109muz m165mapa; zapis testowy na D:; WGRANIE 127 = DLL + Bitwa\lufs21.4/24.4/27.4/30.4 (40 ogg).

## DOPISEK 27.10 ok. 00:35 CET - 222 i 223 zbudowane po recenzji; recheck w toku; 204 dokonczenie w toku
- 222 (zb\n222): 96a90b9b + d245fde1 + recenzja 47b9f8b8 (md5 8f06d41c). Tywin (juz jeniec) przejety przy starcie sesji - komunikat w 1. godzinie po wgraniu.
  Probe: flaga CrashScribe\armoury-odbicie.flag, sprawdz_logi --grupa odbicie222.
- 223 (zb\n223): e0e600a8 + 6ddfb3f5 + recenzja 11b6dffc (md5 2663767f). Tywin: 95 ludzi, 90% sprzetu, sam zaatakowal (sila gry z tieru), nocny marsz -25%.
  sprawdz_logi --grupa e223. Jeff "znani lordowie a juz glowy rodu" -> 223b: znani lordowie bez ziemi (Jaime itd.) w workflow wf_b6fa88ea-c4c.
- Uwagi recenzji w plikach: scratchpad 1e1c276a...\scratchpad\recenzje\uwagi-222.json, uwagi-223.json; 204: scratchpad\n204\uwagi-204-recenzja.json.
- Plan: po wf_0055b61c-6fc (204) i wf_b6fa88ea-c4c (222/223) scalic 204, 222, 223 do n161 (rozwiaz_oba.py), build, autotest 127a (bitwaN z CrashScribe-at-n204
  i "harasser": 3, probyN 6 dob, flaga armoury-odbicie.flag), sprawdz_logi p202 p203 m204jazda odbicie222 e223 m109muz m165mapa -> po zamknieciu gry WGRANIE 127.

## DOPISEK 26.10 ok. 23:40 CET - 202, 203 gotowe i scalone do n161 (f3413043, NIE wgrane); 204 dokonczenie; 222/223 w toku
- n161 HEAD f3413043 = 126 + 202 (426fe0ba) + 203 (8f48dbcb); build kod 0, Armoury 06c8ab82 (do testu; NIE wgrane - w grze dalej 126).
  203 usunal z repo stare Bitwa\lufs17-26\bit_*.ogg, nowe zestawy Bitwa\lufs21.4/24.4/27.4/30.4 (40 ogg) - przy wgraniu dodac te katalogi (stare moga zostac).
- 204 (zb\n204): 6c561288 + 6f04a755 + recenzja bafb4574 (7 uwag potwierdzonych; poprawiajacy dostal ucieta liste). Dokonczenie: wf_0055b61c-6fc
  (pozostale uwagi: koszt przebiegu, lamiaca sie strona; recheck; testowy CrashScribe-at-n204.dll z konnymi lucznikami "harasser": 3 - tylko do testu).
- 222/223: wf_63b46d86-20c w toku (UWAGA: ten sam skrypt ucina liste uwag do 20000 znakow - sprawdzic po wyniku).
- Konflikty przy scalaniu typu "obie paczki dopisaly w tym samym miejscu" (CHANGELOG, sprawdz_logi): scratchpad 1e1c276a...\scratchpad\rozwiaz_oba.py (zostawia oba).

## DOPISEK 26.10 ok. 22:30 CET - uwagi Jeffa z bitwy na 126; paczki 202-223 w toku (Jeff gra)
- Rycerz Lannisterow z jencow z "full kit" (sledztwo wf_ba8fb83f-df5): NIE blad - kon, uprzaz i zbroja z JEGO zbrojowni (wolny kon bojowy + uprzaz, potem jego
  Chain Mail Horse Armor); ekran druzyny pokazuje wzorzec. Jeff wybral "B" = paczka 202 (prawdziwy sprzet na ekranie druzyny + linie logu werbunku jencow).
- Uwagi Jeffa 22:15 (dokladne slowa w promptach workflow): muzyka bitwy -40%; zbroja meczy o polowe mniej na kg; kon w galopie x2 dluzej; jazda AI ma przeganiac
  konnego strzelca (gracza); po pojmaniu Tywina Lannisterowie po wiesci (kruki) maja go scigac; glowy rodow nie jezdza z garstka biedoty.
- Galezie od eac436b3 (126), drzewa scratchpad fffc991e...\zb\n202, n203, n204, n222, n223:
  202 ekran-druzyny-sprzet, 203 bitwa-glosnosc-stamina (muzyka, zbroja, kon), 204 jazda-na-strzelca, 222 odbicie-lorda, 223 eskorta-lordow.
- Workflowy: wf_0d787158-815 (202+203), wf_a555d7f1-449 (204), wf_63b46d86-20c (222+223); skrypt workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js
  (args = lista paczek). Pierwsze uruchomienie 202+203 (wf_03ae36fd-43a) padlo na bledzie skryptu (null z etapu) - poprawione.
- Po nich: scalic do n161, build, autotest (bitwaN + probyN), dopiero po zamknieciu gry przez Jeffa -> WGRANIE 127.

## DOPISEK 26.10 ok. 21:30 CET - WGRANE 126 (muzyka bitwy 164 + muzyka mapy 165/165b); Jeff sprawdza uchem
- WGRANE 126 (21:25): Armoury f67326e2 (build scalenia 8574a3ad; kod C# = 11d6fcfa) + 80 ogg z drzewa n161 (Lektor\Muzyka\Bitwa\lufsNN\bit_*, Lektor\Muzyka\Mapa\lufsNN\map_*).
  Kopie: Armoury.dll.bak-2026-10-26-przed-wgranie126 i D:\Backup-Bannerlord\wgrane\2026-10-26-wgranie126-przed (8e488bd6 = 125, lista nowych plikow nowe-pliki-126.txt).
  Wycofanie: przywrocic DLL 125 i usunac pliki z listy (stara wersja ich nie czyta, wiec mozna je zostawic).
- Autotest 126b: kod 0, ERROR 0; sprawdz_logi m109muz + m165mapa 0 NIE (MAP-7: psai 0 co klatke, motyw mapy gry zatrzymywany, 1 nasze zdarzenie). Recheck bezpieczenstwa
  (wf_96b60ce9-683): StopFree/ReleaseById bez zastrzezen; poprawiony tylko MAP-7 w sprawdz_logi (resztki odcinkow bitwy, 20788b14).
- Statusy 164/165 w CHANGELOG n161 eac436b3; push n161/n164/n165 (ok. 100 MB muzyki i glosow). Zapis testowy przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.
- DO SPRAWDZENIA u Jeffa: czy na mapie gra jedna muzyka (126a: "leca dwie naraz"), glosnosc mapy -23 LUFS, muzyka bitwy. Wylacznik MCM "Our Map Music" / "BattleMusicOurs".
- ZNANE DROBNE: 159c - po wczytaniu zapisu partia z zegarem po terminie dostaje raz wyjazd po bron zamiast odsylki; leftover glos psai po bitwie (fade 1.00) slyszalny
  dopiero, gdy oddamy muzyke grze (jak w czystej grze). Kredyty ElevenLabs ok. 9 tys. (200 nowych kwestii glosu czeka).

## DOPISEK 26.10 ok. 21:05 CET - 165b (dwie muzyki) zbudowane; AUTOTEST 126b + recheck bezpieczenstwa w toku
- Diagnoza (wf_9cd7649b-016): nasz MapHold pisal psai 0 tylko raz (odczyt przed zapisem); psai co 30-120 s startuje nowy odcinek mapy na innym glosie
  i natywny kanal najpewniej nie zachowuje 0 -> muzyka gry pod naszym utworem; odczyt "psai 0.00" to tylko pole managed (nie mikser). Inne mody wykluczone.
- 165b (zb\n165): 981f17c5 + a86cc5d1, recenzja 5ad02d06 + 40c923ac: psai 0 co klatke, ForceStopThemeWithFadeOut motywu mapy gry co >= 5 s, trzymanie 0 przy
  ladowaniu misji (20 s), linia "kontrola" co 30 s, sprawdz_logi MAP-7 (jedno zrodlo), StopFree (Stop + zwolnienie po id - TEZ dla dzwiekow bitwy!).
- Scalone do n161: 8574a3ad, build kod 0, Armoury f67326e2. 21:03 AUTOTEST 126b (m113\seria-126b.out.txt) + workflow wf_96b60ce9-683 (recheck: bezpieczenstwo
  natywne StopFree/ReleaseById, koszt, ForceStop). Po obu: sprawdz_logi --grupa m165mapa i m109muz na logach 126b, potem odsluch Jeffa -> WGRANIE 126.

## DOPISEK 26.10 ok. 20:25 CET - 126 WSTRZYMANE: Jeff slyszal dwie muzyki naraz na mapie (okno autotestu 126a)
- Autotest 126a: bitwaN + probyN kod 0, ERROR 0, gra przywrocona 20:22 (w grze 125: Armoury 8e488bd6, CS bef9db58; po tescie zostaly puste foldery Lektor\Muzyka\Mapa\lufsNN - bez znaczenia).
- Jeff ok. 20:10 patrzyl na okno autotestu (myslal, ze to jego gra): "muzyki na mapie kampanii na siebie nachodza, leci nasza muzyka a w tle muzyka z gry", "i leca dwie naraz".
  Log twierdzi, ze psai = 0.00 i gra jeden nasz utwor (20:07-20:12 map_07, map_08, map_03) - druga muzyka spoza naszej kontroli albo psai podnoszone przez gre/mod.
- Workflow wf_9cd7649b-016 (naprawa-165-dwie-muzyki): 3 sledztwa (silnik/psai, inne mody, nasz kod) -> diagnoza -> poprawka 165b w zb\n165 + linia kontrolna co 30 s
  + test sprawdz_logi (jedno zrodlo) -> recenzja -> fix. Potem: scalic n165 do n161, autotest, odsluch Jeffa (jego ucho rozstrzyga), dopiero wtedy WGRANIE 126.

## DOPISEK 26.10 ok. 20:05 CET - 164 + 165 scalone do n161; AUTOTEST 126a W TOKU (muzyka bitwy i mapy)
- 10 utworow mapy nagrane (zostalo ok. 9 tys. kredytow realnie; licznik z opoznieniem 13.5 tys.), wyslane Jeffowi. Przerobione bitwa_v2.py --prefiks map -> 40 ogg OK.
- 164 recheck 7fe8c6d0 (motyw ROT stop 50 ms), 165 e96eb615..bc0010c1 + pliki 16f37999. Scalone do n161: 604cb213 (CHANGELOG: wpisy 165, 164 nad 159).
  Build n161 kod 0, Armoury md5 cda4a309 (build z 604cb213). gen_mcm bez zmian, selftesty m109muz i m165mapa OK.
- WAZNE: n161 mial STARE glosy bitwy (WGRANE 123 bylo ze scratchpad, nie z repo) - 315 plikow Lektor\Bitwa innych niz w grze. Naprawione: 4c63bc38 = glosy 123
  z gry do repo (md5 zgodne z manifestem 395/395). Teraz drzewo n161 = gra (832 takie same, 0 roznych) + 80 nowych (bit_*, map_*). NIE wgrywac wgraj-v5 z drzew
  sprzed 4c63bc38 (cofnelyby glosy 123).
- 20:01 AUTOTEST 126a (m113\seria-126a.out.txt; -Dll mfinal126a -Tree n161, bitwaN + probyN 6 dob). Po TAK -> WGRANIE 126 = DLL cda4a309 + 80 ogg
  (Lektor\Muzyka\Bitwa|Mapa\lufs17-26) z drzewa n161; potem statusy 164/165 w CHANGELOG, push n161/n164/n165; zapis autotest-przekierowany-* na D:.

## DOPISEK 26.10 ok. 19:35 CET - WGRANE 125 (159 + 159b + 159c); muzyka mapy nagrywana; 165 w toku
- WGRANE 125 (19:27): Armoury 8e488bd6 = build scalenia f5048698 noc/161-integracja (status 6674fb98, wypchniete; noc/159-nadzy-lordowie tez wypchniete).
  Kopie: Armoury.dll.bak-2026-10-26-przed-wgranie125 i D:\Backup-Bannerlord\wgrane\2026-10-26-wgranie125-przed (05503a8c = 124). CrashScribe bef9db58, flag brak.
  Autotest 163c: bitwaN + probyN 6 dob kod 0, ERROR 0, potkniecia 0; (2b) 32 -> 18-20 gotowych ponad % po wyjazdach, do domu 161 ludzi w 5 partiach (163b: 120).
- Muzyka mapy: Jeff 19:15 "to zrob pozostale 9" - generuj.py --wszystkie (muzyka_mapy\probka, log generuj.log). Kredyty przed: 26 302.
- W toku: wf_d370822d-ca4 (recheck 164 -> paczka 165 w zb\n165). Potem: bitwa_v2/mapa na 10 mp3 -> scalic 164+165 do n161 -> autotest -> WGRANIE 126
  (DLL + Lektor\Muzyka\Bitwa\lufsNN\bit_*.ogg + Lektor\Muzyka\Mapa\lufsNN\map_*.ogg). Zapis testowy autotest-przekierowany-* w Game Saves przeniesc na D: po serii 126.

## DOPISEK 26.10 ok. 19:07 CET - 159c ZBUDOWANE i scalone; AUTOTEST 163c W TOKU
- 159c (zb\n159): c7c00ae2 + 20aacfb7 (recenzja 2), ed31a2c2 + 8c65f8bb (recenzja 3: 4 potwierdzone + drobne poprawione). Recheck: tylko 1 drobna (po wczytaniu
  zapisu partia z zegarem po terminie dostaje raz wyjazd po bron zamiast odsylki, takze gdy przed zapisem nie byla w drodze) - zostawiona, do pozniejszej poprawki.
- Scalone do n161: f5048698 (konflikt CHANGELOG rozwiazany skryptem). Build n161 kod 0, Armoury md5 8e488bd6.
- 19:05 AUTOTEST 163c (m113\seria-163c.out.txt; -Dll mfinal163c, bitwaN + probyN 6 dob). Jesli urwany: gra wedlug pamieci przerwany-autotest-sprzatanie
  (w grze ma byc Armoury 05503a8c), potem powtorzyc tym samym poleceniem z -Prefix 163d. Po TAK -> WGRANIE 125 = DLL 8e488bd6 (kopia .bak + D:, md5, statusy, push).

## DOPISEK 26.10 ok. 18:55 CET - 164 ZBUDOWANE po recenzji; 165 muzyka mapy (Jeff 18:30); 159c w toku
- 164 (zb\n164, noc/164-muzyka-bitwy): 3d1d6b18 + cc9d4cda + recenzja 5171df53 (Armoury md5 8ebc3f2f). Recenzja: "Stop Event Themes On Map" nie dzialal (ROT gra motywy
  402-415 przez PlaySegment na glosach 7/8 warstwy psai) - teraz refleksja psai m_logik/m_playbackChannels + startFade. Pliki bit_* (40 ogg + 10 mp3) w repo galezi.
- Jeff 18:30: "zrob mi jeszcze 10 utworow na mape kampanii w stylu gry o tron lub sredniowiecza minimum 2 minuty ... nie zbyt intensywna, nie rozprasza, dobry podklad".
  Zadania: scratchpad 1e1c276a...\scratchpad\muzyka_mapy (zrob_zadania.py, zadania\map_01..10_*.json po 125 s, generuj.py - pomija gotowe, STOP < 2500 kredytow).
  Probka map_01_krolewski_trakt.mp3 (2:05) wyslana Jeffowi do odsluchu; reszta 9: po jego OK albo (bez odpowiedzi) po WGRANIU 125. Kredyty przed: 26 302.
- Workflow wf_d370822d-ca4: recheck poprawki 164 -> paczka 165 muzyka mapy w zb\n165 (galaz noc/165-muzyka-mapy od 5171df53): nasze utwory map_* tylko na mapie
  kampanii (psai wyciszone), losowo bez powtorek, przerwa 20-40 s, okna wydarzen / misje przejmuja, klucz MCM "Our Map Music".
- Kolejnosc wgran: 125 (159+159b+159c, po autotescie 163c) -> 126 (164 + 165, pliki bit_* i map_* do Lektor\Muzyka\Bitwa|Mapa\lufsNN, po autotescie).

## DOPISEK 26.10 ok. 18:20 CET - NOWE KONTO (poprzednie: limit tygodnia o 15:17); 159c i 164 w toku
- Test 163b (159 + 159b-fix, build 9650e63d = merge 9263c755 w n161): bitwaN + probyN kod 0, ERROR 0, gra przywrocona 15:22 (Armoury w grze 05503a8c = 124).
- Druga recenzja 159b (wf_e0014686-1ec): 2 prawdziwe usterki -> paczka 159c. Wynik recenzji: scratchpad sesji 1e1c276a...\scratchpad\recenzja-159b-fix2-wynik.json.
- Workflowy z 15:17 (159c wf_0e9e7939-ff8, 164 wf_57fa2a26-aee) padly na limicie - 159c nie ruszyl; 164 zostawil NIEZACOMMITOWANE zmiany w zb\n164
  (EventMusic.Bitwa.cs, tools/muzyka/bitwa_v2.py); ogg 10 utworow x 4 lufs gotowe w scratchpad fffc991e...\muzyka_bitwy\out.
- 18:15 nowe konto: workflow 159c wf_f37a28aa-e17 (zb\n159: poprawka, 3 recenzje + obalanie, fix2, recheck) i 164 wf_3f58b257-583 (zb\n164: dokonczenie, recenzja, fix).
  Skrypty: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\1e1c276a-7a4b-41a8-acfd-5b40353b59ab\workflows\scripts\
  Po 159c: merge noc/159-nadzy-lordowie -> noc/161-integracja (zb\n161), build, autotest 163c (bitwaN,probyN 6 dob), dopiero po TAK -> WGRANIE 125 (159+159b+159c).
  Po 164: merge do n161 po 125, autotest, WGRANIE 126 (DLL + bit_*.ogg do Modules\Armoury\ModuleData\Lektor\Muzyka\Bitwa\lufsNN).
- Wrapper testow: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (163b: -Dll test\mfinal163b -NowyArmoury ... -Prefix 163b -Runs bitwaN,probyN).

## DOPISEK 26.10 ok. 13:45 CET - STAN NA WYPADEK URWANIA (Jeff: "zapisz stan, zaraz moze urwac sie tokeny")
**W GRZE (zatwierdzone):** WGRANE 124 = Armoury 05503a8c (commit 87aaa764 galezi noc/161-integracja), CrashScribe bef9db58, glosy 123 (dane).
Gra posprzatana po URWANYM tescie 163a (13:04): przywrocone z C:\Users\GAME\autotest-kopie\2026-10-26_13-04-00 (Armoury, CrashScribe, arm_canon_moves.tsv,
BannerlordConfig.txt LatestSaveGameName, engine_config.txt safely_exited), flaga usunieta. Procedura: pamiec przerwany-autotest-sprzatanie.md.
**UWAGA:** HEAD noc/161-integracja (worktree zb\n161) ma JUZ scalone 159 (merge, build 9650e63d) - NIE wgrane. Wgrane 124 = 87aaa764.
**W TOKU (jesli urwie sie - co zrobic):**
1. Autotest 163b (159 na 124; bitwaN + probyN 6 dob): m113\seria-163b.out.txt. Jesli urwany: SPRAWDZ GRE (md5 Armoury 05503a8c, CS bef9db58, flagi, Configs)
   wedlug pamieci przerwany-autotest-sprzatanie.md, potem powtorz: testy-noc.ps1 -Dll mfinal163c -NowyArmoury zb\n161\Armoury\bin\Release\Armoury.dll
   -Prefix 163c -Runs bitwaN,probyN -ProbyDays 6 -CsAt test\CrashScribe-at-n103.dll -ExtraFlags armoury-dzwiek-bitwy.flag (run_in_background).
   Kryteria: kod 0, ERROR 0 (bledy 27 first-chance = latki BKROTPatch/BK, tak samo w kazdym tescie); w logu linia dobowa "Nowe druzyny i sprzet (159)".
2. Recenzja 159b fix2 (workflow wf_e0014686-1ec): wznowic Workflow(scriptPath=...\workflows\scripts\recenzja-159b-fix2-wf_e0014686-1ec.js, resumeFromRunId).
   Gdy recenzja i test OK -> WGRANIE 125 (159+159b): kopia .bak + D:, md5, statusy w CHANGELOG n161, push.
3. Muzyka: GOTOWE wszystkie 10 utworow (13:50) w scratchpad\muzyka_bitwy\probka\bit_01..10_*.mp3 (01 = 120 s, reszta 150 s). Jeff o probce bit_04:
   "tak swietny klimat". Kredyty ElevenLabs po muzyce: ok. 26.3k (licznik aktualizuje sie z opoznieniem).
4. Paczka 164 (muzyka tylko w bitwie, motywy wojenne off z mapy) - workflow wf_57fa2a26-aee w zb\n164 (galaz noc/164-muzyka-bitwy od 87aaa764).
   Po nim: tools/muzyka/bitwa_v2.py na 10 mp3 -> ogg w 4 zestawach lufs -> wgrac pliki + DLL 164 (scalony z 159 jesli 125 wgrane) po autotescie.
**DECYZJE JEFFA 26.10:** nadzy lordowie "Pozyczka + mniejsze" + max 10% bez broni (brak pancerza OK); ubrania chlopow barwy ziemi bez szlacheckich;
glosy dziko (v4, bloki, RRRAAAH rzadko); muzyka 10 utworow tylko w bitwie, rozdzielic od mapy; wybor glosow zostawil Claude.
**OTWARTE / POZNIEJ:** pytanie do Jeffa "spotkate koszule" = kolory (zrobione) czy dlugosc?; 200 nowych kwestii glosu (ranni hp/pn/dy, sztandar bl/bg/bt/eb, chor ou -
scratchpad\glosy_v4\klipy_v4_nowe.csv, blok_v4.py) nagrac razem z paczka runtime (ranni krzycza z miejsca 50%, max 4 naraz); sztandar B (przejecie, odbijanie)
i C (piesn zwyciezcow); muzykant w szyku (rog/bodhran, raport w transkrypcie: cornu + bodhran z grajkow karczmy, 2 linie action_sets); drobne: sprawdz_logi ND-7
(13 probek zamiast 6), 146 falszywa UWAGA JIT, Fall Back za mape = PANIKA, phantom wpis DTE w SpawnPost (sprzed 159).

## DOPISEK 26.10 ok. 12:35 CET - WGRANE 121-124 (ubrania, jezdziec, glosy v4, sztandar); w toku 159b, muzyka
- LINIA GLOWNA: noc/161-integracja (worktree scratchpad\zb\n161) = 120 + 160 + 157 + 158 (+ wpis 123). Kolejne paczki scalac tu.
- WGRANE 121 (11:45): Armoury 72b07d42 = 160 ubrania chlopow w barwach ziemi (bez barw rodu - UseTeamColor, bez farbowanych ROT; tekstury sprawdzone).
- WGRANE 122 (12:14): Armoury 2d20df52 = 157 jezdziec: tarcza pod ostrzalem (bez zmiany w szarzy szyku), kontra 1-2 jezdzcow na strzelca (20 s), pomiar konnicy AI.
- WGRANE 123 (12:15, DANE): 314 glosow bitwy na nowo (eleven_v4, bloki, 14 glosow z konta, Karda fonetycznie "Kar-da"); kopie starych: Modules\Armoury\kopie\Bitwa-przed-wgranie123 i D:.
  Narzedzia: scratchpad\glosy_v4 (blok_v4.py, nagraj_v4.py, roles_v4.json, klipy_v4.csv, klipy_v4_nowe.csv - 200 nowych NIE nagranych).
- WGRANE 124 (12:29): Armoury 05503a8c = 158 biegacz po upuszczony sztandar (w autotescie niesprawdzony - szyki puste).
- W TOKU: 159b (nadzy lordowie, regula Jeffa: tylko bez broni, max 10%) w zb\n159; muzyka: probka bit_04_szarza czeka na odsluch Jeffa
  (scratchpad\muzyka_bitwy, zadania/*.json, probka_muzyki.py); pytanie do Jeffa: "spotkate" koszule = kolory czy dlugosc.

## DOPISEK 26.10 ok. 11:05 CET - WGRANE 119 (pasek konia) i 120 (dzwiek 3D); w toku 157-160, glosy v4, muzyka
- LINIA GLOWNA: noc/156-dzwiek-3d-na-119 (worktree scratchpad\zb\n156) = 118 + 155 + 154. Nowe galezie 157-160 odgalezione od niej.
- WGRANE 119 (10:49): Armoury 4372c1cb = 155 pasek staminy konia pod paskiem zdrowia konia (UIExtenderEx, klucz HorseStaminaBar). Autotest 155a TAK.
- WGRANE 120 (11:03): Armoury 7b6dc24c = 154 dzwiek bitwy 3D: 395 klipow przez event:/mod/mission/voice_shout / voice / ambient_3d_big
  (Extra/voiceover bylo 2D - stad "werble przy uchu"), odleglosci przy zrodle, pozycja co klatke, blad 'Save the banner!' przy smierci dowodcy.
  Autotest 156a: kod 0, ERROR 0, zdarzenia 3D odrzucone 0. Slyszalnosc 3D - tylko ucho Jeffa. tools/sprawdz_logi.py ND-7 do poprawy (13 probek, nie 6).
- Uwagi Jeffa z gry 25.10 22:45 (przez sesje SKYRIM) i sledztwo: scratchpad\sledztwo-2245.txt. Zold: nie blad (druzyna 331 + karawana 313, regula BK).
- Decyzje Jeffa 26.10: nadzy lordowie = "Pozyczka + mniejsze" + najwyzej 10% nieuzbrojonych; ubrania chlopow - kolory ziemi, dluzsze, bez barw szlacheckich;
  glosy - dziko jak runda 2 (eleven_v4, bloki, wybor glosow zostawil Claude); muzyka - 10 utworow tylko w bitwie, rozdzielic od mapy; muzykant w szyku - pozniej.
- W TOKU: workflow paczek 157 (jezdziec: tarcza, kontra na strzelca, pomiar konnicy), 158 (biegacz po sztandar), 159 (nadzy lordowie), 160 (ubrania chlopow);
  glosy v4: scratchpad\glosy_v4 (klipy_v4.csv, roles_v4.json, blok_v4.py - nagrywanie blokami z with-timestamps, probka bloki_probka czeka na odsluch Jeffa);
  muzyka: scratchpad\muzyka_bitwy\RAPORT.md (10 utworow, ElevenLabs Music, ok. 22-28k kredytow; najpierw 1 probka).

## DOPISEK 25.10 ok. 22:20 CET - WGRANE 118 (fala "AI walczy o zycie" 139-149 + 153 + 117)
- WGRANE 118 (22:13): Armoury 86195e02 z galezi noc/139-ai-zycie (worktree scratchpad\zb\n139, build 8995f2ab, wpis 86fc8054). Galaz zawiera scalone noc/138-calosc (117 + 153),
  wiec to jest TERAZ linia glowna - nowe prace odgalezniac od noc/139-ai-zycie. CrashScribe bez zmian (bef9db58). Kopie: Armoury.dll.bak-2026-10-25-przed-wgranie118, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie118-przed.
- Autotest 118a: kod 0, ERROR 0, 4/4 bitwy; kontrola fali: python -I tools/sprawdz_logi.py --grupa ai139 <Armoury-*.log> (galaz n139) - 9/10 TAK.
  NIE: tarcza pod ostrzalem po 1.5 s 24/36 (67%, prog 90%) - nastepna paczka: pomiar po 3 s i powod braku tarczy.
- Po pierwszej grze Jeffa: --grupa ai139 na jego logu; 141/146/148/149 i oblezenia (145, 147) autotest nie sprawdza. Odlozone drobne: 146 falszywa UWAGA JIT przy bandytach,
  Fall Back za mape = PANIKA. Decyzje Jeffa 25.10 ok. 22:25: odwrot rannego z bronia w oblezeniach/kryjowkach - NIE (zostaje tylko bitwa polowa, bez zmian kodu);
  poscig za armia AI w odwrocie na rozkaz wodza - "zobacze jak pogram" (czekamy na jego uwagi po grze, na razie NIE).

## DOPISEK 25.10 ok. 18:30 CET - WGRANE 117 (151 + 152 + 150a + 150b + 117r); fala AI 139-149 nadal w toku
- WGRANE 117 (18:23): Armoury 2c95388a (galaz noc/138-calosc, commit c55b2c58; statusy 746e87a2/7cc6ba66), CrashScribe bez zmian (bef9db58). Kopie: Armoury.dll.bak-2026-10-25-przed-wgranie117
  obok DLL i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie117-przed.
  - 151: odnowa oddechu wykladniczo od Atletyki (wolny 2x szybciej co 100 pkt od 100; 300 - po wyczerpaniu ok. 3 min, 10 - ponad 20 min).
  - 152: ponad 300 pula RBM 2800 x 2^((A-300)/31) (310 ok. 3500, 330 ok. 5500); oddech i bieg razem z nia; sufit sprawnosci biegu 2.5.
  - 150a: proba albumu obrazkow okna lupu (tylko z flaga armoury-zdjecia-obrazkow.flag). 150b: obrazek okna lupu wedlug miejsca (region kultury osady, Inni, dzicy,
    morze, turniej - sala tronowa tylko w King's Landing, bez Joffreya). 117r: 6 poprawek po niezaleznej recenzji (Skagos to nie dzicy - kultura rodu; bez Ship1/Ship2;
    osada obrazka od miejsca bitwy; osady Innych; krzywa 1.0 + mistrzostwo; opis suwaka).
  - Autotesty 152a, 117a, 117b: kod 0, ERROR 0, 4/4 bitwy, zdjecia okien 22/22. Zdjecia okien lupu: docs/zdjecia-okien-2026-10-25/okna-132-[5-7]-*-117.jpg.
- Znane, niepoprawione (nastepna paczka): tekst okna "Near X" bierze Hero.LastKnownClosestSettlement (rzadko odswiezane) - w autotescie bitwa pod Riverrun, tekst "Near Pendric";
  obrazek juz liczy osade od miejsca bitwy (117r), tekst jeszcze nie.
- W TOKU: workflow fali AI 139-149 (galaz noc/139-ai-zycie, worktree zb\n139) - po nim scalic noc/138-calosc (151/152/150/117r) do 139, test bitwaN, wgranie 118.

## DOPISEK 25.10 ok. 17:20 CET - WGRANE 116; w toku fala AI 139-149 i obrazki lupu 150
- WGRANE 116 (17:15): Armoury 25f69127 + CrashScribe bef9db58 z galezi noc/138-calosc (worktree scratchpad\zb\n138): 132 proba zdjec okien, 133a-g okno Review the army
  (Kit for the next battle z podgladu DTE, Last battle, konie, Speed, Herd, Pay x3, Recent messages; 133a CrashScribe - cisza podgladu), 136 stamina koni (HorseWind), 137 oddech dwufazowy.
  Autotest 138a kod 0, ERROR 0; zdjecia okien w docs/zdjecia-okien-2026-10-25. 133a NIE sprawdzony autotestem (testowy CrashScribe-at) - w logu CS Jeffa linia
  'Mends: podglad gotowosci DTE (133a) - pole _isReadinessPreview JEST'. T1 konia gracza (czy MountSpeed spowalnia) - tylko gra Jeffa: linia 'Konie (136)' v/(Vbase x m) ok. 1.
- Drobne uwagi z przegladu 138 (do poprawy w nastepnej paczce, wpis 138 w CHANGELOG): liczniki NB2 przy koncu odpoczynku konnicy, linia 'BattleWind (130)' z martwa regeneracja,
  opisy MCM po 136/137, licznik E oddechu, Atletyka biegu 127 surowa.
- W TOKU: workflow fali "AI walczy o zycie" (galaz noc/139-ai-zycie od 138, worktree zb\n139; kroki 139 pomiar i przelacznik paniki, 140 tarcza pod ostrzalem, 141 obronca naprzod,
  142 panika bez powrotu, 143 ranny <20% odwrot z bronia, 144 AI oszczedza sily + animacje/glos zadyszki, 145 ranny ostrozny, 146 szyk pod ostrzalem, 147 panika do wyczerpania,
  148 poscig konnicy AI, 149 bohaterowie ostrozni). Decyzje Jeffa 25.10: ranny <20% odwrot z bronia; panika bez powrotu (zbiorka pozniej); lordowie ostrozni; poscig z limitem.
- W TOKU: 150 obrazki okien lupu wedlug miejsca - krok 1 album 48 sprite'ow ROT (galaz noc/150-obrazki, flaga armoury-zdjecia-obrazkow.flag), potem dobor (region, przeciwnik, rodzaj).
- Wrapper testow: m113\testy-noc.ps1 rozbija -ExtraFlags po przecinku (od 13:00).

## DOPISEK 25.10 ok. 13:15 CET - WGRANE 114 i 115; w toku: konie, regeneracja, zdjecia okien, raport armii
- WGRANE 114 (12:31): Armoury 3cab5bea z noc/final (125 okno lupu po turnieju, 126 przypinanie typow wojska). Crash 126a w bitwie 4/4 = AccessViolation w natywnym Agent.MakeVoice
  z Immersive_Battlefields.PlayOrderGesturesPatch.Prefix (IB i gra wolaja Owner.MakeVoice bez IsActive; autotest ponawia rozkaz szarzy co 15 s) - powtorka 126b czysta.
- WGRANE 115 (13:10): Armoury 70246086 z galezi noc/127-bitwa-cialo (worktree scratchpad\zb\n127; CHANGELOG 127-131) + 16 klipow mo_karda w Lektor/Bitwa/motto i manifest.
  127 bieg wedlug fizjologii (jedna regula, w biegu bez oddechu), 128 szarza z daleka marszem (bieg od 60 m), 129 okrzyki krolestwa gracza "Karda" (gdy Jeff wlada krolestwem),
  130 stamina tylko z Atletyki (bez Endurance bohaterow) + krzywa 1.12, 131 udzwig +5 kg za punkt Endurance kazdego bohatera w partii.
- BLAD WRAPPERA (naprawiony 13:00): testy-noc.ps1 '-ExtraFlags a,b' przez powershell -File = JEDNA flaga 'a,b' - dlatego w nocy proby P4/T14/T15 nie ruszaly. Teraz rozbicie po przecinku.
  Proby P4 (przeglad armii), T14/T15 (przesluchanie), test okna turnieju - DO PUSZCZENIA jeszcze raz (biegi probyN / turniejN).
- W TOKU (workflowy tej sesji): rozpoznanie staminy koni (Jeff: "konie tez powinny miec stamine ... zalezna od cech konia"), regeneracja wedlug fizjologii (Jeff: "wyjscie z zadyszki
  jak w prawdziwej fizjologii"), paczka 132 proba zdjec nowych okien (worktree zb\n132, galaz noc/132-zdjecia - flaga armoury-zdjecia-okien.flag), rozpoznanie 133
  raportu w przegladzie armii (Jeff: braki sprzetu i dlaczego nie zaklada, zold, konie, predkosc, herding).
- Jeff wie: stamina koni, regeneracja i 133 beda osobnymi paczkami; zdjecia okien wysle po biegu 132.

## DOPISEK 25.10 ok. 06:30 CET - WGRANE 113 (nocny pakiet gotowy przed 12:00)
- W grze: Armoury 6b4f188b = galaz noc/final 1e7593e5 (CHANGELOG 203ac63a): 114 marsz i doktryny kultur (+S1 ostrzal, S3 flanka, S4 lucznicy za piechota), 115 choragwie, 116 przeglad armii w obozie, 117 okno lupu (bitwa, oblezenie),
  118 friendly fire (strzaly 100% / oblezenie 50%, wrecz 50%, szarza 50%), 119 dzwiek bitwy, 120 przesluchanie (juz w 112), 121 podatek warsztatow (juz w 112), 122 gotowosc AI (sila z uzbrojeniem, dozbrajanie), 123 szturm murow i bebny oblezenia, 124 domyslne + nagrania.
- Pliki poza DLL w grze: Modules\Armoury\ModuleData\Lektor\Bitwa (379 ogg + manifest.tsv, 15 MB) i Modules\Armoury\GUI\Prefabs\ArmSpoilsWindow.xml. Kopia przed: Armoury.dll.bak-2026-10-25-przed-wgranie113, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie113-przed.
- Nagrania ElevenLabs: 298 glosow + 81 SFX, 16 glosow z konta Jeffa (bez dodawania z biblioteki - konto 33 glosy przy limicie 30), kredyty 88 120 -> 76 203. Narzedzia tools/lektor/nagraj_bitwa.py, sfx_bitwa.py na galezi noc/p2a (19a871a5); kopia nagran D:\Backup-Bannerlord\nagrania-bitwa-2026-10-25.
- Autotesty 124b/124d: ERROR 0, 4/4 bitwy. NIE pokryte: oblezenie (szturm, ogien, tlum, bebny), okno lupu po oblezeniu, proba P4 i T14/T15 (nie odpalily w 124c - przyczyna nieustalona), sluch i wyglad.
- Do sprawdzenia w logu Jeffa po pierwszej sesji: ERROR 0; "Dzwiek bitwy (noc-p2): misja" (wyciek 0, odrzucenia wedlug grup), "FF (pole|oblezenie)" (udzial FF), "Choragwie (CH) ekonomia" (odlozone == oddane), "Okno lupu (117)", "Gotowosc do bitwy (p9)" linia doby, "Przesluchanie (m109): pochodzenie - banda", D11-3 "w tym gracz zaplacil".
- Decyzje Jeffa otwarte: dubel SiegeAssaultCrowd z ROTBA Contextual Combat Shouts; glosy z biblioteki zamiast narratorow konta (wymaga zwolnienia miejsc); splity RBM a doktryny (Polnoc, Zelazne, Dorne, Lorath); przeglad armii v2 (przypinanie typow, pelne okno); okno lupu po turnieju (v1.1); wyzwiska (v1.1).
- Zapis testowy autotest-przekierowany przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.

## DOPISEK 25.10 ok. 04:05 CET - WGRANE 112
- W grze: Armoury 68f74d0f = galaz noc/wgranie112 (a14c76d3 + noc/p7-przesluchanie 443b13f5 + noc/p8-podatek 8be4aeca; CHANGELOG 120, 121). Kopia przed: ...\Armoury.dll.bak-2026-10-25-przed-wgranie112 i D:\Backup-Bannerlord\wgrane6-10-25-wgranie112-przed.
- Autotest 112a: ERROR 0, 4/4 bitwy. Do sprawdzenia w grze Jeffa: "Przesluchanie (m109): pochodzenie - banda", "bez migawki 0", opcja szara; D11-3 "w tym gracz zaplacil X".
- KOLEJNA INTEGRACJA (noc/calosc z workflow noc-bitwa-kod) MUSI scalic noc/wgranie112 i noc/p9-gotowosc (gotowosc AI) + dzwiek szturmu murow i bebny oblezenia (do dopisania).
- Logi sesji Jeffa 02:11-03:46: D:\Backup-Bannerlord\logi-jeffa6-10-25_03-50-po-grze. Zapis testowy = kopia jego zapisu z 03:46 (md5 0ea55e89).

## DOPISEK 25.10 ok. 02:35 CET - NOC W TOKU (workflow noc-bitwa-kod, run wf_d049918d-367)
- Projekty z rozpoznania: scratchpad fffc991e...\scratchpad\noc\projekty\ (ai-marsz-taktyki.json, dzwiek-glosy.json, choragwie.json, przeglad-armii.json, okno-lupu.json, friendly-fire.txt, krytyk.txt - podzial P0-P5, kolizje K1-K17, zakres v1).
- Galezie: noc/p0 (baza: klucze, wpiecia, kontrakty) -> noc/p1-marsz, noc/p2a (nagrania ElevenLabs, budzet 25k znakow + 120 SFX), noc/p2b-dzwiek, noc/p3-choragwie, noc/p4-przeglad, noc/p5-lup, noc/p6-ff -> noc/calosc (scalenie, CHANGELOG 114-119) -> audyt.
  Drzewa: scratchpad\zb\noc-<nazwa>. Baza a14c76d3 (= wgrane 111).
- Potem sesja glowna: autotest bitew (UWAGA zmiana czasu), wgranie DLL + plikow poza DLL (Armoury/ModuleData/Lektor/Bitwa/**, Armoury/GUI/Prefabs/ArmSpoilsWindow.xml) przed 12:00.
- Osobno: diagnoza warsztatu Jeffa ("czemu moj warsztat nie daje kasy") - agent tylko czyta logi z D:\Backup-Bannerlord\logi-jeffa\2026-10-25_01-00-po-grze.

## DOPISEK 25.10 ok. 02:15 CET (po zmianie czasu) - WGRANE 111
- W grze: Armoury 9e301ed6 = galaz w-toku/m117-ubranie b74049c9 (111 ubranie cywilne v3, 112 szyk armii na postoju, 113 kij z domu; CHANGELOG na galezi, a14c76d3). Reszta modow bez zmian
  (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e). Kopia przed: Modules\Armoury\bin\...\Armoury.dll.bak-2026-10-25-przed-wgranie111 i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie111-przed.
- Autotest 117a: 4 bitwy, KONIEC OK, ERROR 0; kij 58/58 potwierdzone tymczasowe; szyk 4.05 -> 0.42-0.50 jedn.; ubrania 0 przypadkow (sprawdzic w grze Jeffa). PULAPKA: zmiana czasu 03:00 -> 02:00 myli wrapper (szuka logu po czasie startu).
- Zapis testowy Game Saves\autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (kopia zapisu Jeffa z 00:59, md5 9a0b4ab9) zostaje na nocne testy - po serii przeniesc na D:.
- NOC (Jeff "start" 02:55): pakiet bitwy sredniowiecznej + przeglad armii + okno lupu + friendly fire + dzwiek (przemowy rodow, motta przy szarzy, jeki rannych po bitwie, konie, szczek broni, deszcz/wiatr,
  ptaki i kruki po bitwie, ogien przy oblezeniu) - gotowe na 25.10 12:00. Rozpoznanie: workflow noc-bitwa-rozpoznanie (5 czesci + krytyk) i osobne rozpoznanie friendly fire.

## DOPISEK 25.10 00:20 (drugie konto)
- Kontrola 110 w sesji Jeffa (start 22:24, Armoury-2026-10-24_22-24-27.log): OK - wojna korona-Dorzecze raz (108869, E7/0 + kruk), sojusz Polnoc+Dorzecze
  (108870, do dnia 586), zwrot Neda 292902 raz (nr=done), Cape Wrath jedno oblezenie (bez petli), Stoneknoll 31 przy powrocie wozu, zloto przy wczytaniu +616,
  ERROR Armoury 0. Jeszcze nie bylo: ekran trofeow (bitwa gracza 22:58 jest - sprawdzic linie m114), chleb (po zapisie i wczytaniu), okno ROT o Dorzeczu (dzien 108).
- m117-ubranie (Jeff 23:05: goli zolnierze maja dostac najtansza tunike i buty swojej kultury): galaz w-toku/m117-ubranie (bb367a94, od 6cd80130, wypchnieta),
  drzewo scratchpad fffc991e...\scratchpad\zb\m117-ubranie, Armoury.dll md5 14e193c7 (tylko Armoury; CrashScribe bez zmian). CHANGELOG 111 na galezi.
  Status: ZBUDOWANE, recenzja 2x (2 uwagi minor poprawione), CZEKA na zamkniecie gry -> autotest bitwy na kopii zapisu Jeffa -> wgranie.
- Pytanie Jeffa o rozjechana armie (Gregor Clegane 1577 przy Mousedown Mill, dzien odpoczynku R3-08a): nasz kod nie rusza pozycji doczepionych; szyk gry
  (Army.GetRelativePositionForParty) stawia druzyny za wodzem do ok. 1.5-2 jedn. i nie dociaga ich na postoju. Czeka na Jeffa: najechac na grupe i ewentualnie
  zgoda na sciaganie figurek na poczatku postoju (tylko obraz, raz przy zmianie stanu).

## NAJWAZNIEJSZE - STAN NA 22:30
- W grze: WGRANIE 110 (Armoury ae412d24; galaz w-toku/m116-fabula 6cd80130, wypchnieta; drzewo scratchpad 6570af7b...\scratchpad\zb\m116-fabula). Reszta modow: CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e. Lancuch galezi: 107 (n36 d3cada05) -> 108 (n37 9300d259) -> 109 (n38 34f0db38) -> 109.1 (m115-okno e2417cd3) -> 110 (m116-fabula 6cd80130).
- Zapis Jeffa: Game Saves\IronmanKkUpHftX7ZIq.sav (21:03, md5 35d84a9c; kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze). W Game Saves brak zapisow autotest-* (archiwum D:\Backup-Bannerlord\zapisy-testowe-archiwum, 426 plikow).
- 110 przy pierwszym wczytaniu w kampanii Jeffa: Dorzecze w wojnie z korona, sojusz Polnoc+Dorzecze, zwrot 292 902 Polnocy (raz) - SPRAWDZIC w jego logu (linie 'Wojny kanonu (m104): (4) Dorzecze', 'nr=done:292902', ERROR 0).

## DO ZROBIENIA (kolejnosc)
1. Kontrola 110 w logu Jeffa po jego pierwszej sesji (wyzej) + ekran trofeow ('Ekran trofeow (m114): bitwa ... poza ekranem 0') + wyplaty Stoneknoll przy powrocie wozu.
2. Raport swiata 24.10 (szczegoly nizej): dryf skarbcow koron (36% pieniedzy, cel 22%, bezpiecznik oszczednosci na suficie 3.7), zloto znikajace przy wczytaniu (-43..-84 tys.) i 3 doby z RESZTA ok. -55 tys., martwy front Joffrey-Robb (19 dni bez bitwy; 1 zamek zdobyty w 215 dni), biedne wsie Polnocy i Reach (wsie < 1000 zl 10 -> 51).
3. Mniejsze: ROT wojna #2 EndDay 0 (Dorzecze nie poddaje sie po Krwawych Godach - sprawdzic przy Godach), Beric w Essos, obrazki scen prologu z ROT, nagrody turniejow lokalnych 7-85 zl, Nocna Straz za silna / Dothrakowie za slabi, wywrotka silnika przy wyjsciu (zrzut).
4. Pytanie bez odpowiedzi Jeffa: limit taboru w kolumnie marszowej (MarchPackAllowance 0.25 = 1 zwierze juczne/hodowlane na 4 ludzi; Jeff pytal o -0.62 'Marching column: baggage train') - zaproponowano 1 na 2, czeka na decyzje.

## PULAPKI TESTOW (24.10)
- Wrapper scratchpad\m113\testy-m114.ps1 trzyma zdjecie bazy w m113\zapisy\baza-autotest-przekierowany-*.sav i PRZYWRACA je na starcie, jesli plik w Game Saves jest inny - przed seria na nowszym zapisie Jeffa usunac stare zdjecie (bieg 116a poszedl przez to na zapisie z 02:26).
- Gra testowa wisi przy wyjsciu po 'KONIEC OK' - zamknac PID gry testowej (Bannerlord.BLSE.Standalone), wrapper sam przywraca pliki.
- Katalog DLL z wrappera nie ma CrashScribe.dll - do wgrania dolozyc produkcyjny CrashScribe (84205371) z poprzedniego katalogu.
- Po serii przeniesc autotest-*.sav na D: (NIGDY przy wlaczonej grze).

---
## (dalej: tresc z 21:10)

## Stan gry Jeffa
- W grze: WGRANIE 109.1 (Armoury feb5abdb; galaz w-toku/m115-okno e2417cd3 = w-toku/n38-calosc 34f0db38 + okno inwestycji). Reszta modow jak 106.1 (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e).
- Kampania Jeffa: Ironman Documents\Mount and Blade II Bannerlord\Game Saves\IronmanKkUpHftX7ZIq.sav (ostatni zapis 21:03, kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze).
- 425 zapisow testowych przeniesione na D:\Backup-Bannerlord\zapisy-testowe-archiwum (Game Saves: 105 zapisow, Ithauser tylko jeden). Kopie Ithauser z testow: D:\Backup-Bannerlord\zapisy-testowe-ithauser.
- Jeff skonczyl grac o 21:03. Gra zamknieta.

## Co dzis wgrano (CHANGELOG 107, 108, 109, 109.1)
- 107: walka w bitwach (m113-walka2), pobor we wsiach pobocznych (pula 10%, odrost z przyrostu naturalnego E4/4), kupno ziemi (cena 14-20 lat wedlug relacji), GrowthMaxPercent 1.3 (tez w Armoury.json Jeffa).
- 108: caly lup z bronia na ekranie trofeow (SpoilsSoldierSplitOff - wylaczony ukryty podzial Spoils EnablePrioritySystem), bron ze stanem z ciala (LootWeaponsKeepCondition), dochod prac na zielono (WorksIncomeShown). Potwierdzone w grze: ekran 70/70 i 79/79 sztuk.
- 109: wyplata prac z udzialu dodatku w targu (WorksPayMarketShare; Stoneknoll 1.7 -> 4.5/dobe), komunikat musztry wedlug przyczyny, Lordship i zamek wlaczone po tescie 114a-ziemiaD (T11 Sealbay, T12 Ramsgate TAK).
- 109.1: okno 'Invest in its works' - 5 linii opisu, lista kwot widoczna.

## m116-fabula - WGRANE 22:24 jako 110 (patrz CHANGELOG); ponizej opis zakresu
Jeff 21:00 (wsciekly): "Dorzecze jest w pokoju - co to za bzdura, to bylo pierwsze, co zostalo zaatakowane". Kanon: po pojmaniu Tyriona Gora pali Dorzecze, Jaime bije lordow Dorzecza i oblega Riverrun PRZED egzekucja Neda; potem Polnoc i Dorzecze razem przeciw koronie. U nas plan wpuszczal Dorzecze do wojny ok. 108 dnia opowieci - blad.
Workflow wf_4fdd61ac-eb2 (task waydblgky) - skrypt: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\6570af7b-c8f5-4f3e-827c-790a5efcedd4\workflows\scripts\m116-dorzecze-w-wojnie-i-fabula-wf_4fdd61ac-eb2.js (kopia: docs/przekazanie-m116-workflow.js). Worktree (jesli powstal): scratchpad 6570af7b...\scratchpad\zb\m116-fabula, galaz w-toku/m116-fabula od e2417cd3 (w klonie scratchpad\zb\n35-calosc, origin = to repo).
Zakres (jedna paczka):
1. Dorzecze w wojnie z korona od konca prologu (nowe kampanie) + JEDNORAZOWY catch-up w zapisie Jeffa przy wczytaniu: Dorzecze wypowiada wojne Joffreyowi, Polnoc i Dorzecze razem (Diplomacy jesli sie da), kruk; stary termin ~108 ma byc no-op. Sprawdzic cala mape wojen 298-300 z kanonem (Renly/Stannis vs Joffrey itd.); wojny ROT wybrane przez Jeffa (Dolina-Zelazne, Reach-Dorne) zostaja.
2. Okup Neda: blokada wykupu od konca prologu do egzekucji (Prologue.World.cs RansomDailyPrefix / EndCaptivity tylko p01/p10); jednorazowy zwrot 292 902 ze skarbca korony do Polnocy (log [20:06:26] 'Okupy (178): AI-AI - Eddard Stark ... wolny za 292902').
3. Petla oblezenia Cape Wrath (armia Renly'ego zaczyna i zwija oblezenie 60+ razy - linie 'Koniec oblezenia (KO) ... ROT_castle18 ... odejscie z obozu (zmiana celu ...)'): minimum trwania / histereza celu + kruk 'oblezone' raz na (armia, osada).
4. Chleb biedoty (TownFolk.Bread.cs:17 'pamiec sesji') - zapisac w SaveText.Sync.
5. Kruki: smierc Roberta z pustym tekstem ([18:49:27] Kruki (R3-05c)) - tekst; pojmanie Jaimego (arm_canon_moves.tsv p10 bez wiadomosci, przypisane Dorzeczu) - wiadomosc i Robb/Polnoc.
Potem: build, autotest na kopii zapisu Jeffa 2-3 doby (wojna jest, zwrot raz, ERROR 0), wgranie, CHANGELOG 110.

## Raport swiata (24.10 21:00, pelny w odpowiedzi czatu) - do zrobienia pozniej
- Dryf skarbcow koron (36% pieniedzy, cel 22%), bezpiecznik oszczednosci na suficie 3.7 od 108732; wsie < 1000 zl 10 -> 51; Polnoc i Reach biednieja.
- Pieniadze przy wczytaniu: -43..-84 tys. przy 3 z 4 wczytan; 3 dni z RESZTA ok. -55 tys. (108781, 108817, 108844).
- Wojna Joffrey-Robb bez bitew 19 dni; 1 zamek zdobyty w 215 dni (martwy swiat).
- Beric jeniec, potem w Essos; obrazki scen prologu z ROT; nagrody turniejow lokalnych 7-85 zl; Nocna Straz za silna (7900), Dothrakowie za slabi (173).
- Wywrotka silnika przy wyjsciu 0xC0000005 (znana, ERRORS.md) - zrzut pamieci.

## Jak testowac i wgrywac (sprawdzone dzis)
- Kopia zapisu Jeffa do testu: Copy IronmanKkUpHftX7ZIq.sav -> autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (Ironman zapisuje pod ta nazwa); PO SERII przeniesc autotest-*.sav na D: (NIGDY gdy gra dziala - 17:19 crash przy wczytaniu).
- Wrapper: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (biegi: kopia, bitwa, bitwaB, bitwaOFF, ziemia, ziemiaD, pobor, poborF, wczyt; -Dll <nowy katalog w test\>, -NowyArmoury <dll>, -Tree <drzewo>, -CsAt test\CrashScribe-at-n103.dll, -Prefix, -BezKopii, -PoborDays). Plan: m113\PLAN-TESTOW.md.
- Wgranie: test\wgraj-v5.ps1 -DllDir test\<katalog z 5 DLL> -Tree <drzewo> -Tag 2026-10-xx-wgranieNNN (katalog test = scratchpad 6ba92574...\scratchpad\test). Gry nie moze byc wlaczonej.
- Monitoring gry Jeffa: Monitor na najnowszym Modules\Armoury\Armoury-*.log (ERROR, 'Ekran trofeow (m114): bitwa', 'Ziemia (m113): KUPNO').

## Zasady Jeffa (pamiec)
- Wersje jak najszybciej ("ciagle cos naprawiamy zamiast pograc"): jedna seria testow, potem wgranie; male zmiany wgrywac po zamknieciu gry bez testu ("wgraj jak wylacze gre, na moim logu sprawdzisz").
- Luzne pytania - krotka odpowiedz bez agentow. Liczby z istniejacych modeli moda (nie nowe stale). Jedna regula gracz/AI. Nic nie wgrywac/testowac, gdy gra dziala.
