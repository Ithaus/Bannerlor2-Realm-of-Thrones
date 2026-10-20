## 2026-10-20 (zadanie "rody", pomysly R3-01c i R3-01d z POMYSLY-35) - POSAG PRZY SLUBACH AI I POMOC WASALI NA OKUP PANA

**Mod:** Armoury (+ 2 linie w CrashScribe) | **Galaz:** `w-toku/p5-rody` (od 5189c0fe = kandydat WGRANIA 92) | **Tor:** M (bez gry - zakaz uruchamiania;
build Armoury i CrashScribe: kod 0, 0 Error(s)) |
**Klucze (nowe, domyslnie TAK; w `Armoury.json` Jeffa ich nie ma - wejda domyslne z kodu):** grupa MCM "Ransoms by wealth (stage 2)":
`VassalRansomAid` (+ `VassalRansomAidShare` 0.05), `AiDowry` (+ `AiDowryWealthShare` 0.25, `AiDowryIncomeDays` 90), `AiDowryMatchTilt`.
Warunki u Jeffa spelnione: `LordRansomByIncome`, `DebtLadderEnabled`, `ClanBudgetEnabled` = true. | **Zapis:** brak (same liczniki doby, nic do `SyncData`).

**Dla Jeffa (prosto):**
- Gdy dwa rody AI biora slub i panna przechodzi do rodu meza, jej rod placi teraz posag w zlocie glowie rodu meza. Posag to najmniejsza z trzech kwot: wycena
  Banner Kings dla tej panny, cwierc zlota jej rodziny (bez jej wlasnej kiesy, bo ta odchodzi z nia) albo 90 dni dochodu jej rodu - i tylko z tego, co rod ma
  ponad swoja rezerwe. Biedny rod wydaje corke bez posagu. Sluby Twojego rodu zostaja jak byly. Uwaga: Banner Kings w rozmowie liczy posag odwrotnie (placi
  rod, ktory bierze nowego czlonka), ale w tej wersji BK przy zwyklym slubie zadne zloto nie przechodzi. U AI zostaje zwyczaj sredniowieczny z opisu, na ktory
  powiedziales "tak": posag daje rod panny.
- Swaty AI troche patrza na pieniadze: biedny kawaler i bogata panna z tego samego krolestwa maja 1,5 raza wieksza szanse na slub, bogaty kawaler z biedna
  panna 0,8 raza. Slubow w roku jest tyle samo (dalej 48 - pilnuje tego regulator CrashScribe).
- Gdy glowa rodu AI albo krol AI jest wykupywany, a rodzinie (krolowi - skarbcowi) brakuje gotowki, skladaja sie wasale: inne rody tego krolestwa z lennami,
  wedlug lenn (miasto liczy sie za dwa zamki), kazdy najwyzej 5% tego, co ma ponad rezerwe. Ich zloto idzie do porywacza razem z reszta gotowki (do Ciebie,
  gdy sprzedajesz takiego jenca posrednikowi), a dlug okupu jest o tyle mniejszy. Ciebie nikt o nic nie prosi (prosba Banner Kings o doplate do okupu pana
  w tej wersji BK nigdy sie nie pojawia). Rod w dlugu albo ponizej rezerwy nie daje nic.
- Wprost zobaczysz malo (to sprawy AI z AI); w logu kazdy posag i kazda zbiorka ma swoja linie.

**Problem / zrodlo pomyslu:** POMYSLY-35 nr 1 (R3-01c) i nr 2 (R3-01d), Jeff 20.10: "28 - nie, 29 - nie. POzostale tak". Audyt `docs/audyt-2026-10-09/05-ai-lordowie-bankructwa.md`
L9 / D2 (brak posagu w slubach AI: BK ma wycene `BKMarriageModel.GetDowryValue`, ale placi ja tylko w kontrakcie gracza `BKMarriageBehavior.ApplyMarriageContract` - recenzja 20.10: w tej wersji BK i tam zloto nie przechodzi;
ROT laczy pary AI bez zlota) i D4 wariant 1 + `04-wojna-armie-lupy-okupy.md` N1 (pomoc wasali na okup pana, Magna Carta kl. 12). Podatek nadzwyczajny (wariant 2) -
NIE robiony (Jeff odlozyl kasy miast). Dowod potrzeby z logu testu 160 dob na WGRANIU 91 (`Modules/Armoury/Armoury-2026-10-20_00-44-29.log`):
- "Sluby AI (169c)": 26 slubow AI-AI w 160 dobach, w tym 11 "biedny z bogatym", posag hipotetyczny razem 265 734 zl - zlota nikt nie placil.
- "Okupy (178): AI-AI": 865 okupow, z tego 368 glow rodow; 65 glow rodow zostalo z dlugiem okupu (razem 2 330 584 zl), np. "Doro Kasgos (glowa rodu, Kasgos)
  wolny za 91714 zl (D 3058): gotowka 38407, dlug 53307"; krol Tyrosh (Belicho Staegone) - skarbiec 122 888 gotowki, dlug korony 46 171.

**Zmiana:**
- `Armoury/src/AiDowry.cs` (NOWY): sluchacz `CampaignEvents.BeforeHeroesMarried` (MarriageAction.ApplyInternal - po ustaleniu rodu po slubie, przed zmiana rodu;
  pierwszy bohater jest w rodzie po slubie, drugi odchodzi - sprawdzane drugi raz `MarriageModel.GetClanAfterMarriage`, czysta funkcja w grze/BK/ROT). Posag:
  panna odchodzi do rodu meza; rod panny z budzetem 166 (`ClanBudget.ChestOf` - nie dluznik, nie w zajeciu, nie Straz bez zoldu, nie najemnik);
  posag = min(BK `GetDowryValue(panna,false,false)` przez `BannerKingsConfig.Instance.MarriageModel`, 0.25 x g, 90 x D) i najwyzej g - R; g = min(zloto z budzetu,
  kiesy rodziny dzis) minus kiesa panny (poprawka po recenzji), R bez zmian, D = `DebtLadder.DebtD`. Zloto z kies rodziny panny (glowa, potem dorosli lordowie, kazdy do podlogi 5 000; panna nie placi sama) ->
  kiesa glowy rodu meza (`ClanIncomeBook.NoteInflow` KRansom - jednorazowe od prawdziwego platnika, poza D stalym; `CirculationWindows.NoteHeroGold` po obu stronach).
  Pominiete: slub z rodem gracza, slub wewnatrz `ApplyMarriageContract` (prefiks + finalizer na `BKMarriageBehavior.ApplyMarriageContract` - flaga), pan mlody
  przechodzacy do rodu zony, ten sam rod, panna - glowa wlasnego rodu.
  Przechyl swatow: postfiks na `NpcCoupleMarriageChance` modelu, ktorego gra uzywa (u Jeffa `ROT.Models.ROTMarriageModel`, `AccessTools.DeclaredMethod` na klasie
  deklarujacej, wpinany w `OnSessionLaunched`): ten sam krolestwo, kawaler G < R (miara `NeedChestAnyOf`) i panna G > 60 D -> x1.5; panna G < R i kawaler G > 60 D
  -> x0.8; szansa 0 zostaje 0.
- `Armoury/src/VassalRansomAid.cs` (NOWY): `Help(jeniec, rod porywacza, brak)` wolane z `Ransom178.Execute` (AI-AI i kurier) po pulapie dlugu okupow, gdy zostaje
  dlug: tylko glowa rodu (takze krol - wtedy brak skarbca), rod jenca nie gracza i nie najemny. Wasale = rody krolestwa jenca z lennami, bez rodu jenca, rodu krola,
  porywacza, gracza i najemnikow, z budzetem `ChestOf` i zlotem ponad R; dar_i = min(brak x lenna_i / suma lenn, 0.05 x (G_i - R_i)), jedno przejscie.
  Zloto z kies rodziny wasala -> ta sama droga co gotowka okupu (`Ransom178.Pay` - glowa rodu porywacza albo skarbiec korony zdobywcy; 1/9 korony porywacza
  liczy sie od calej gotowki). Cena okupu bez zmian (cena = gotowka + dlug zachowane).
- `Armoury/src/Ransom178.cs`: 2 x 2 linie w `Execute` (galaz rodu i galaz krola - po zdjeciu ze skarbca, wiec dar wasali nie rusza skarbca); po recenzji takze
  `SalePostfix` (gracz sprzedaje jenca posrednikowi albo na ekranie druzyny): `Help(jeniec, rod gracza, dlug)` w obu galeziach, dar wasali do kiesy gracza
  (`ChangeHeroGold` + `CirculationWindows.NoteHeroGold`, 1/9 korony od gotowki + daru jak dotad przez `NoteRansomIn`), dlug wobec gracza mniejszy o tyle samo;
  gotowka "z gry" (posrednik) bez zmian. Linia "Okupy (178): posrednik" z dopiskiem "od wasali (R3-01d) N", napis dla gracza "its vassals add N".
- `Armoury/src/ArmouryBehavior.cs`: 3 linie (sluchacz zdarzen, `AiDowry.OnSessionLaunched` po `RaceLaw`, raporty po "Okupy (178)").
- `Armoury/src/Settings.cs` + `McmSettings.cs` (gen_mcm): 6 kluczy wyzej.
- `CrashScribe/src/MarriageFast.cs` `Foreign`: przepuszcza postfiks `Armoury.AiDowry.ChancePostfix` na lancuchu szansy - bez tego CrashScribe uznalby go za obca
  latke i wylaczyl skrot dla zonatych (wolniejsze swaty). Postfiks tylko mnozy szanse > 0, wiec skrot (szansa zonatego = 0) zostaje prawdziwy.

**Linie logu (do autotestu):**
- `Posagi AI (R3-01c): start sesji | latki: kontrakt BK (ApplyMarriageContract), przechyl swatow na ROT.Models.ROTMarriageModel.` (raz na sesje)
- `Posagi AI (R3-01c): dzien N | slubow AI-AI X: panna przechodzi do rodu meza Y, ... | posagow P razem S zl (najwiekszy: ...) | bez posagu: ... | swaty - ocen pary z przechylem: x1.5 a, x0.8 b | latki: ...` (co dobe)
- `Posag (R3-01c): <panna> (<rod>) wychodzi za <maz> (<rod>) - posag N zl do <glowa> (BK .., 25% zlota rodu .., 90 dni D .., ponad rezerwe ..).` (kazdy posag)
- `Pomoc wasali (R3-01d): dzien N | okupow glow rodow i krolow AI z brakiem gotowki X (brak razem ..) | z pomoca wasali Y: od wasali razem Z zl ...` (co dobe)
- `Pomoc wasali (R3-01d): <jeniec> (glowa rodu|krol, <rod>, <krolestwo>) - brak gotowki na okup N zl; wasale dali M zl[ graczowi (posrednik)] (K rodow: ...), zostaje dlug ...`
  (kazda zbiorka; wzorzec autotestu `Pomoc wasali \(R3-01d\): .* wasale dali` lapie TYLKO zbiorki - linia doby mowi "od wasali razem")
- Kontrola sumy: w linii "Okupy (178)" "gotowka" rosnie o dar wasali, "dlug okupu" maleje o tyle samo; "Swaty ROT (Z.3)" CrashScribe bez nowej linii "obca latka".

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: `Ransom178.Execute` i `SalePostfix` - wpiecie tylko przy dlugu > 0 i glowie rodu; cena, pulap dlugu, kolejka, 1/9 korony bez zmian. Gracz: nie placi
  (wasal) i nie dostaje posagu; jako porywacz w kurierze dostaje wiecej gotowki od razu (napis "part in coin now, the rest owed to you" dalej prawdziwy), przy
  sprzedazy posrednikowi dostaje dar wasali od razu do kiesy (prawdziwe zloto z kies wasali), a dlug rodu jenca wobec niego jest o tyle mniejszy. Gracz jako
  JENIEC - bez pomocy wasali AI (pomysl mowi o krolach i lordach AI).
- Kolizje: kontrakt BK (`ApplyMarriageContract`) w tej wersji BK zlota przy zwyklym slubie nie przenosi (`MarriageAction.Apply` przenosi oboje do `FinalClan`
  przed sprawdzeniem `Clan != finalClan`) - podwojnego posagu nie ma; kontrakt jest zawsze z rodem gracza, ktory i tak jest pominiety (flaga zostaje jako bezpiecznik). `Measure169c` (posag hipotetyczny) liczy dalej po swojemu
  (zdarzenie po slubie) - linia zostaje jako porownanie. RaceLaw (postfiks IsCoupleSuitableForMarriage) - inna metoda. Regulator CrashScribe sumuje szanse
  po przechyle, wiec 48 slubow na rok zostaje; przy MarriageTargetPerYear = 0 (staly hamulec) przechyl zmienia liczbe slubow nieznacznie.
- Zamknieta ekonomia: posag kiesy rodziny panny -> kiesa glowy rodu meza; dary wasali kiesy wasali -> odbiorca okupu. Nic z niczego, nic w nicosc.
- Bieda: dary i posag tylko ponad rezerwe R budzetu 166 i z kies do podlogi 5 000 - nie pchaja rodu do Banku; dluznik nie placi. 5% nadwyzki na jedna zbiorke;
  przy ok. 0.4 zbiorki na dobe w calym swiecie (65 glow z dlugiem w 160 dobach + krolowie) ten sam wasal placi rzadko.
- Watki: posag w zdarzeniu slubu, pomoc w okupie (dzienne zachowania gry), przechyl w swatach ROT (DailyTickClan) - glowny watek; przechyl bez alokacji
  (2 odczyty slownika budzetu). Potkniecia liczone (bez gaszenia funkcji).
- Otwarte: ile realnie da pomoc wasali i posag - dopiero autotest (linie wyzej). Rody krolestwa skladaja sie takze na glowe rodu, ktora nie jest krolem
  (tak opisuje wariant 1 audytu - "rody krolestwa z lennami"), a nie tylko na pana z wlasnymi lennikami BK.

**Status:** DO SPRAWDZENIA (zbudowane, nie wgrane, nie testowane w grze; poprawki po recenzji 20.10 - sekcja Recenzja na koncu).

**Decyzje (podjete sam, wedlug "ekonomie projektuje Claude"):**
1. Posag placi rod PANNY tylko, gdy to ona odchodzi do rodu meza; pan mlody przechodzacy do rodu zony i panna - glowa rodu: bez posagu (posag corki, nie "posag syna").
2. Wariant 1 pomocy: wasale = rody krolestwa jenca z lennami (miasto = 2, zamek = 1), BEZ rodu krola (pan nie jest wasalem swojego wasala) i bez rodu gracza.
3. Pomoc wasali placi do dlugu PO pulapie dlugu okupow (wasale nie doplacaja do czesci, ktora i tak bylaby umorzona).
4. Zwykly lord (nie glowa rodu) - bez pomocy wasali (Magna Carta: okup ciala pana).
5. "Ta sama pomoc na posag corki pana" z opisu (zdanie "mogla by") - NIE robione (nic ponad zadanie).
6. (po recenzji) Kierunek posagu zostaje sredniowieczny - daje rod panny, bierze rod meza - bo tak brzmi opis R3-01c, na ktory Jeff powiedzial "tak" (przyklad
   Tyrellow: bogaty rod panny ratuje zadluzony rod pana mlodego). BK w rozmowie gracza liczy odwrotnie (placi rod, ktory bierze nowego czlonka), ale zlota nie
   przenosi - wiec u gracza posagu w zlocie nie ma wcale, a u AI jest jeden kierunek. Wycena BK `GetDowryValue(panna)` zostaje tylko jako gorny pulap WIELKOSCI
   posagu (tier rodu, poziom, dziedziczka +50% - dziedziczka wnosi wiecej; wdowa, bezplodna, z gminu - mniej); zwykle i tak wiaze 25% zlota albo 90 dni D.
   Odwrocenie na sposob BK = zamiana platnika z odbiorca w `AiDowry.OnBeforeMarried` (payer/recv) - tylko na slowo Jeffa.
7. (po recenzji) Kiesa panny nie liczy sie do zlota rodu panny (g), rezerwa R bez zmian - rod po posagu zostaje co najmniej na rezerwie sprzed slubu.
8. (po recenzji) Pomoc wasali takze przy sprzedazy jenca posrednikowi przez gracza (100% okupow glow rodow i krolow AI) - dar do gracza, dlug mniejszy.
Wylacznik kazdej czesci: MCM Armoury -> "Ransoms by wealth (stage 2)" -> "Vassal Ransom Aid" / "Ai Dowry" / "Ai Dowry Match Tilt" off.

**Test w grze (autotest):** wczytac kampanie (najlepiej wojna), 60-120 dob; w logu Armoury: linia startu "Posagi AI (R3-01c): start sesji" z obiema latkami;
"Posagi AI (R3-01c): dzien" co dobe, przy slubie AI "Posag (R3-01c):" (oczekiwane ok. 1 slub na tydzien, wiekszosc z posagiem kilku-kilkudziesieciu tys.);
"Pomoc wasali (R3-01d):" przy okupach glow rodow z dlugiem (oczekiwane kilka-kilkanascie na 60 dob); w CrashScribe "Swaty ROT (Z.3)" bez "obca latka" i skrot dla
zonatych czynny; "potkniecia" nigdzie. Porownac sume "dlug okupu" w "Okupy (178)" z biegiem 160 dob (2.33 mln glow z dlugiem) - powinna spasc.

**Recenzja (PELNA, 20.10) - co zrobione:**
1. (wazne) "Posag oparty na blednym zalozeniu o BK" - PRAWDA co do BK (sprawdzone w dekompilacji BK Redux: dymek `{=2QM2cpdw}` - placi rod, ktory bierze nowego
   czlonka; `ApplyMarriageContract` - `MarriageAction.Apply` przed `Clan != finalClan`, wiec `GiveGoldAction` nigdy sie nie wykonuje). Kierunku NIE zmienialem:
   Jeff zatwierdzil opis z kierunkiem sredniowiecznym (decyzja 6), a zasada Jeffa to "ekonomie projektuje Claude, nie pytac". Poprawione: opisy w kodzie, dymek
   MCM ("Weddings of your own house are left alone." bez zdania o kontrakcie BK), tekst dla Jeffa i "Kolizje" w Ryzyku (bez "drugiego bezpiecznika przed
   podwojnym posagiem"). Wycena BK zostaje jako pulap wielkosci, opisana uczciwie.
2. (drobne) Dymek "Banner Kings asks you itself" - PRAWDA (`RansomDuty` nigdzie nie tworzone, tylko zapis i pole). Dymek: "Your house is never asked, and ...";
   tekst dla Jeffa i komentarz w `VassalRansomAid.cs` poprawione.
3. (drobne) Kiesa panny w zlocie rodu - PRAWDA, poprawione (g bez kiesy panny, decyzja 7).
4. (drobne) Brak pomocy przy sprzedazy posrednikowi - PRAWDA, poprawione (`SalePostfix`, decyzja 8).
5. (drobne) Wzorzec "wasale dali" lapal linie doby - PRAWDA, poprawione (linia doby: "od wasali razem").
Build po poprawkach: Armoury i CrashScribe kod 0, 0 Error(s); gen_mcm przebudowal McmSettings (2 dymki).
