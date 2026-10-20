## 2026-10-20 (zadanie "rody", pomysly R3-01c i R3-01d z POMYSLY-35) - POSAG PRZY SLUBACH AI I POMOC WASALI NA OKUP PANA

**Mod:** Armoury (+ 2 linie w CrashScribe) | **Galaz:** `w-toku/p5-rody` (od 5189c0fe = kandydat WGRANIA 92) | **Tor:** M (bez gry - zakaz uruchamiania;
build Armoury i CrashScribe: kod 0, 0 Error(s)) |
**Klucze (nowe, domyslnie TAK; w `Armoury.json` Jeffa ich nie ma - wejda domyslne z kodu):** grupa MCM "Ransoms by wealth (stage 2)":
`VassalRansomAid` (+ `VassalRansomAidShare` 0.05), `AiDowry` (+ `AiDowryWealthShare` 0.25, `AiDowryIncomeDays` 90), `AiDowryMatchTilt`.
Warunki u Jeffa spelnione: `LordRansomByIncome`, `DebtLadderEnabled`, `ClanBudgetEnabled` = true. | **Zapis:** brak (same liczniki doby, nic do `SyncData`).

**Dla Jeffa (prosto):**
- Gdy dwa rody AI biora slub i panna przechodzi do rodu meza, jej rod placi teraz posag w zlocie glowie rodu meza. Posag to najmniejsza z trzech kwot: wycena
  Banner Kings dla tej panny, cwierc zlota jej rodziny albo 90 dni dochodu jej rodu - i tylko z tego, co rod ma ponad swoja rezerwe. Biedny rod wydaje corke bez
  posagu. Sluby Twojego rodu zostaja jak byly (tam posag rozlicza kontrakt Banner Kings).
- Swaty AI troche patrza na pieniadze: biedny kawaler i bogata panna z tego samego krolestwa maja 1,5 raza wieksza szanse na slub, bogaty kawaler z biedna
  panna 0,8 raza. Slubow w roku jest tyle samo (dalej 48 - pilnuje tego regulator CrashScribe).
- Gdy glowa rodu AI albo krol AI jest wykupywany, a rodzinie (krolowi - skarbcowi) brakuje gotowki, skladaja sie wasale: inne rody tego krolestwa z lennami,
  wedlug lenn (miasto liczy sie za dwa zamki), kazdy najwyzej 5% tego, co ma ponad rezerwe. Ich zloto idzie do porywacza razem z reszta gotowki, a dlug okupu
  jest o tyle mniejszy. Ciebie nikt o nic automatycznie nie prosi (Banner Kings pyta Cie sam). Rod w dlugu albo ponizej rezerwy nie daje nic.
- Wprost zobaczysz malo (to sprawy AI z AI); w logu kazdy posag i kazda zbiorka ma swoja linie.

**Problem / zrodlo pomyslu:** POMYSLY-35 nr 1 (R3-01c) i nr 2 (R3-01d), Jeff 20.10: "28 - nie, 29 - nie. POzostale tak". Audyt `docs/audyt-2026-10-09/05-ai-lordowie-bankructwa.md`
L9 / D2 (brak posagu w slubach AI: BK ma wycene `BKMarriageModel.GetDowryValue`, ale placi ja tylko w kontrakcie gracza `BKMarriageBehavior.ApplyMarriageContract`;
ROT laczy pary AI bez zlota) i D4 wariant 1 + `04-wojna-armie-lupy-okupy.md` N1 (pomoc wasali na okup pana, Magna Carta kl. 12). Podatek nadzwyczajny (wariant 2) -
NIE robiony (Jeff odlozyl kasy miast). Dowod potrzeby z logu testu 160 dob na WGRANIU 91 (`Modules/Armoury/Armoury-2026-10-20_00-44-29.log`):
- "Sluby AI (169c)": 26 slubow AI-AI w 160 dobach, w tym 11 "biedny z bogatym", posag hipotetyczny razem 265 734 zl - zlota nikt nie placil.
- "Okupy (178): AI-AI": 865 okupow, z tego 368 glow rodow; 65 glow rodow zostalo z dlugiem okupu (razem 2 330 584 zl), np. "Doro Kasgos (glowa rodu, Kasgos)
  wolny za 91714 zl (D 3058): gotowka 38407, dlug 53307"; krol Tyrosh (Belicho Staegone) - skarbiec 122 888 gotowki, dlug korony 46 171.

**Zmiana:**
- `Armoury/src/AiDowry.cs` (NOWY): sluchacz `CampaignEvents.BeforeHeroesMarried` (MarriageAction.ApplyInternal - po ustaleniu rodu po slubie, przed zmiana rodu;
  pierwszy bohater jest w rodzie po slubie, drugi odchodzi - sprawdzane drugi raz `MarriageModel.GetClanAfterMarriage`, czysta funkcja w grze/BK/ROT). Posag:
  panna odchodzi do rodu meza; rod panny z budzetem 166 (`ClanBudget.ChestOf` - nie dluznik, nie w zajeciu, nie Straz bez zoldu, nie najemnik);
  posag = min(BK `GetDowryValue(panna,false,false)` przez `BannerKingsConfig.Instance.MarriageModel`, 0.25 x G, 90 x D) i najwyzej G - R; G = min(zloto z budzetu,
  kiesy rodziny dzis), D = `DebtLadder.DebtD`. Zloto z kies rodziny panny (glowa, potem dorosli lordowie, kazdy do podlogi 5 000; panna nie placi sama) ->
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
- `Armoury/src/Ransom178.cs`: 2 x 2 linie w `Execute` (galaz rodu i galaz krola - po zdjeciu ze skarbca, wiec dar wasali nie rusza skarbca).
- `Armoury/src/ArmouryBehavior.cs`: 3 linie (sluchacz zdarzen, `AiDowry.OnSessionLaunched` po `RaceLaw`, raporty po "Okupy (178)").
- `Armoury/src/Settings.cs` + `McmSettings.cs` (gen_mcm): 6 kluczy wyzej.
- `CrashScribe/src/MarriageFast.cs` `Foreign`: przepuszcza postfiks `Armoury.AiDowry.ChancePostfix` na lancuchu szansy - bez tego CrashScribe uznalby go za obca
  latke i wylaczyl skrot dla zonatych (wolniejsze swaty). Postfiks tylko mnozy szanse > 0, wiec skrot (szansa zonatego = 0) zostaje prawdziwy.

**Linie logu (do autotestu):**
- `Posagi AI (R3-01c): start sesji | latki: kontrakt BK (ApplyMarriageContract), przechyl swatow na ROT.Models.ROTMarriageModel.` (raz na sesje)
- `Posagi AI (R3-01c): dzien N | slubow AI-AI X: panna przechodzi do rodu meza Y, ... | posagow P razem S zl (najwiekszy: ...) | bez posagu: ... | swaty - ocen pary z przechylem: x1.5 a, x0.8 b | latki: ...` (co dobe)
- `Posag (R3-01c): <panna> (<rod>) wychodzi za <maz> (<rod>) - posag N zl do <glowa> (BK .., 25% zlota rodu .., 90 dni D .., ponad rezerwe ..).` (kazdy posag)
- `Pomoc wasali (R3-01d): dzien N | okupow glow rodow i krolow AI z brakiem gotowki X (brak razem ..) | z pomoca wasali Y: wasale dali razem Z zl ...` (co dobe)
- `Pomoc wasali (R3-01d): <jeniec> (glowa rodu|krol, <rod>, <krolestwo>) - brak gotowki na okup N zl; wasale dali M zl (K rodow: ...), zostaje dlug ...` (kazda zbiorka)
- Kontrola sumy: w linii "Okupy (178)" "gotowka" rosnie o dar wasali, "dlug okupu" maleje o tyle samo; "Swaty ROT (Z.3)" CrashScribe bez nowej linii "obca latka".

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: `Ransom178.Execute` - wpiecie tylko przy dlugu > 0 i glowie rodu; cena, pulap dlugu, kolejka, 1/9 korony bez zmian. Gracz: nie placi (wasal) i nie dostaje
  posagu; jako porywacz w kurierze dostaje wiecej gotowki od razu (napis "part in coin now, the rest owed to you" dalej prawdziwy).
- Kolizje: posag BK w kontrakcie - podwojny bezpiecznik (rod gracza + flaga ApplyMarriageContract). `Measure169c` (posag hipotetyczny) liczy dalej po swojemu
  (zdarzenie po slubie) - linia zostaje jako porownanie. RaceLaw (postfiks IsCoupleSuitableForMarriage) - inna metoda. Regulator CrashScribe sumuje szanse
  po przechyle, wiec 48 slubow na rok zostaje; przy MarriageTargetPerYear = 0 (staly hamulec) przechyl zmienia liczbe slubow nieznacznie.
- Zamknieta ekonomia: posag kiesy rodziny panny -> kiesa glowy rodu meza; dary wasali kiesy wasali -> odbiorca okupu. Nic z niczego, nic w nicosc.
- Bieda: dary i posag tylko ponad rezerwe R budzetu 166 i z kies do podlogi 5 000 - nie pchaja rodu do Banku; dluznik nie placi. 5% nadwyzki na jedna zbiorke;
  przy ok. 0.4 zbiorki na dobe w calym swiecie (65 glow z dlugiem w 160 dobach + krolowie) ten sam wasal placi rzadko.
- Watki: posag w zdarzeniu slubu, pomoc w okupie (dzienne zachowania gry), przechyl w swatach ROT (DailyTickClan) - glowny watek; przechyl bez alokacji
  (2 odczyty slownika budzetu). Potkniecia liczone (bez gaszenia funkcji).
- Otwarte: ile realnie da pomoc wasali i posag - dopiero autotest (linie wyzej). Rody krolestwa skladaja sie takze na glowe rodu, ktora nie jest krolem
  (tak opisuje wariant 1 audytu - "rody krolestwa z lennami"), a nie tylko na pana z wlasnymi lennikami BK.

**Status:** DO SPRAWDZENIA (zbudowane, nie wgrane, nie testowane w grze).

**Decyzje (podjete sam, wedlug "ekonomie projektuje Claude"):**
1. Posag placi rod PANNY tylko, gdy to ona odchodzi do rodu meza; pan mlody przechodzacy do rodu zony i panna - glowa rodu: bez posagu (posag corki, nie "posag syna").
2. Wariant 1 pomocy: wasale = rody krolestwa jenca z lennami (miasto = 2, zamek = 1), BEZ rodu krola (pan nie jest wasalem swojego wasala) i bez rodu gracza.
3. Pomoc wasali placi do dlugu PO pulapie dlugu okupow (wasale nie doplacaja do czesci, ktora i tak bylaby umorzona).
4. Zwykly lord (nie glowa rodu) - bez pomocy wasali (Magna Carta: okup ciala pana).
5. "Ta sama pomoc na posag corki pana" z opisu (zdanie "mogla by") - NIE robione (nic ponad zadanie).
Wylacznik kazdej czesci: MCM Armoury -> "Ransoms by wealth (stage 2)" -> "Vassal Ransom Aid" / "Ai Dowry" / "Ai Dowry Match Tilt" off.

**Test w grze (autotest):** wczytac kampanie (najlepiej wojna), 60-120 dob; w logu Armoury: linia startu "Posagi AI (R3-01c): start sesji" z obiema latkami;
"Posagi AI (R3-01c): dzien" co dobe, przy slubie AI "Posag (R3-01c):" (oczekiwane ok. 1 slub na tydzien, wiekszosc z posagiem kilku-kilkudziesieciu tys.);
"Pomoc wasali (R3-01d):" przy okupach glow rodow z dlugiem (oczekiwane kilka-kilkanascie na 60 dob); w CrashScribe "Swaty ROT (Z.3)" bez "obca latka" i skrot dla
zonatych czynny; "potkniecia" nigdzie. Porownac sume "dlug okupu" w "Okupy (178)" z biegiem 160 dob (2.33 mln glow z dlugiem) - powinna spasc.
