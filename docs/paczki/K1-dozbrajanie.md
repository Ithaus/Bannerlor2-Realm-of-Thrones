# K1 - DOZBRAJANIE: zolnierze za swoje + wymiana z graczem (specyfikacja)

**Status:** WYKONANE W KODZIE 09.10 (K1-A, K1-B, K1-C; wpis CHANGELOG "K1"), NIEWGRANE - DO SPRAWDZENIA (build kod 0, proba poza gra 22/22, po poprawkach z przegladu kodu 25/25 - 16 uwag, opis w CHANGELOG "K1", "Poprawki po przegladzie kodu"; autotest i proby reczne R1-R12 przed wgraniem).
**K1c (poprawki po przegladzie K1b, 17 uwag - w kodzie, ta sama galaz):** zaloga w trybie "tylko to, co ma" naprawde bez sprzetu z niczego (DressCode jej nie ubiera, pusty wpis DTE dla zalogi bez zbrojowni, straz "nic z niczego" na przydzialach zalog), zbrojownie zalog w zapisie gry (DTE ich nie zapisuje), komunikat wymiany tylko o wkladach tej sesji, sufit P1 takze dla brakow; zakres P2 (tylko bitwy toczone osobiscie) i skutki opisane nizej (A10, P2, C6, R12); build kod 0, proba poza gra 49/49 (`noc2/k1fix/proba`, proba4.log). Wpis CHANGELOG "K1", blok "K1c".
**K1b (decyzje Jeffa 09.10 A, B, C - w kodzie, ta sama galaz):** P1 i P2 rozstrzygniete (rozdz. 6), B1/B2 poprawione wedlug zasady WYMIANY z 04:40, A7 (sufit +1), A10 (zaloga walczy tylko tym, co ma); build kod 0, proba poza gra 42/42 (`noc2/k1fix/proba`, proba3.log). Wpis CHANGELOG "K1", blok "K1b". Swiadome odstepstwa - w CHANGELOG (K1, "Swiadome odstepstwa"). Drzewo `noc2/k1`, galaz `w-toku/k1-dozbrajanie`, baza `noc/sklad5` d486813
(= Armoury w grze 63640cb3). `src/...` oznacza `Armoury/src/...` w tym drzewie, numery linii z d486813.
**Decyzja Jeffa 09.10 (K):** "chce oba mechanizmy: ze za swoje sami sie zbroja z lupow i zoldu, i ja rowniez moge ich dozbroic na zasadzie
wrzuc im lepsza zbroje, a oni wydaja mi swoja gorsza jako wymiane". Dotyczy jego druzyny i jego zalog. AI dziala wedlug tej samej reguly.
Ekonomia jest zamknieta: kazda sztuka pochodzi z prawdziwej polki, kazdy denar z prawdziwej kiesy i trafia do prawdziwej kasy.

## 0. Dla Jeffa - w pieciu zdaniach

1. Twoi ludzie, tak jak ludzie kazdego lorda, odkladaja czesc zoldu i lupu. W miescie kupuja za to lepsza sztuke w miejsce swojej najslabszej, a stara sprzedaja kupcowi.
2. Kupuja najwyzej sprzet o jeden stopien wyzszy niz ich wlasny (Jeff 09.10, P1: "tak, jesli go na to stac i jest dostepna"; rekrut nie kupi plyty) i tylko taki, ktory udzwigna. Kupuja tylko wtedy, gdy nowa sztuka jest wyraznie lepsza (o co najmniej 10%).
3. Twoje zalogi dostaja wlasna sakiewke z polowy swojego zoldu i zbroja sie tak samo, na targu swojej osady. Nie placa z Twojej kiesy.
4. Wymiana w zbrojowni: ludzie biora Twoja sztuke tylko w puste rece albo wtedy, gdy jest LEPSZA od tej, ktora nosza, i ja udzwigna. Za kazda Twoja sztuke, ktora wyparla ich sztuke, dostajesz za darmo jedna ich najgorsza sztuke tego rodzaju. To, co wypelnilo puste rece, oddajesz bez zwrotu. Sztuka gorsza, rowna albo za trudna zostaje w oknie jako Twoja (Jeff 09.10 04:40) - komunikat mowi, ile zostalo, bo nikt jej nie chcial albo nie udzwignal. Reszta ich zapasu nalezy do nich: mozesz ja od nich kupic po cenie kupca.
5. Zalogom dajesz sprzet przez nowa opcje w menu osady, "Hand kit to the garrison" (ta sama regula). Gorsze sztuki wracaja od razu do Twoich sakw, a zaloga walczy TYLKO tym, co ma w zbrojowni - kto nie ma sztuki, walczy bez niej (Jeff 09.10, P2).

## 1. Co jest dzis, czego brakuje (skrot rozpoznania)

- **Sakiewka ludzi** (`src/MenPurse.cs`):
  - Wplywy: zold partii (`src/SoldierPay.cs:390-404`) i lup. U gracza ludzie maja 2/3 lupu (wpis 82); u lorda AI 67% ze sprzedazy nadwyzek (`MenPurse.cs:362-365`).
  - Wydatki: naprawy, potem BRAKI, potem reszta "na zycie" przy kazdym wyjezdzie (`:199-205`).
  - Braki to tylko liczba sztuk, bez sprawdzania umiejetnosci (`:283-325`); AI kupuje tier t albo t-1 (`AiGear.cs:204-268`).
  - Nikt nie kupuje lepszej sztuki i nikt nie oszczedza.
- **Zalogi:**
  - Nie maja sakiewki: zold idzie do kasy osady (`SoldierPay.cs:374-389`).
  - Braki zalog AI oplaca pan osady. Zalogi gracza nic nie kupuja (`GarrisonBuysGearPlayer=false`, `AiGear.cs:180`).
  - DTE wydaje w bitwie sprzet tylko partiom z `LeaderHero` (DTE `DynamicTroopMissionLogic.cs:110`). Zaloga walczy wiec we wzorcu, za darmo.
- **Wymiana w druzynie** (`QuartermasterLaw.PurgeUnusable :489-623` -> `FitFor :337-476`):
  - Kazda nienoszona sztuka trafia do gracza (`targetOwn = Total - Used`, `:469`). Zapas ludzi i lup, ktorego miasto nie kupilo, staja sie Twoje za darmo.
  - Przez to zakup "od ludzi" z wpisu 84 (`BookPostfix :889-891`) nie dziala.
  - Ksiega i widocznosc sa liczone per id (`HoldReserve :1092-1107`), wiec gracz moze dostac inny egzemplarz niz ten, ktory mu przypadl.
- **Zalogi gracza:** nie ma zadnej drogi wymiany. Spoils "Equip garrison" kasuje sztuki za Security - to sprawa Z1, poza K1.
- **Slowa Jeffa, ktore K1 wykonuje** (`R/CHANGELOG.md`):
  - 29.08: "wrzucam luki t6 ... MNIE wydac gorsze luki" (:6558, :6543).
  - 30.08: "za sztuke lepsza wojsko oddaje swoja NAJGORSZA tego samego typu" (:6257-6262).
  - 14.09: "najpierw BRAKI, a jak wszyscy maja, dopiero wymieniac" (:2313).
  - 19.09: "nowy luk znika w stash, a stary luk lucznika pokazuje sie w stash" (:1479).
  - 05.10: "wojsko sklada lepszy sprzet ... wydaje na ... polepszenie sprzetu" oraz "z DTE nadwyzke moge przeciagnac do siebie i sprzedac, kase dostaje ja, nie armia" (:765).
  - 09.10: oba mechanizmy (STAN-PRAC :780-781).

## 2. Pojecia wspolne (jedna regula dla gracza, lordow i zalog)

- **Koszyk** = typ x tier wzorca (`AiGear.NeedBuckets`, `Bucket`): ilu ludzi wedlug wzorca nosi sztuke danego typu w danym tierze.
  `NeedBuckets` dostaje dodatkowo liste oddzialow w koszyku, potrzebna do sprawdzenia umiejetnosci. Konie i rzedy sa poza K1-A, bo nimi zajmuje sie Stajnia.
- **Sila sztuki** = ta sama miara co u kwatermistrza: `RangedRank.Key` dla lukow i kusz (naciag RBM), `Effectiveness` dla reszty.
  *Dlaczego:* ta sama miara obowiazuje przy zakupie, w dopasowaniu i w strazy bitewnej `SkillLawWard`.
- **Gorsza / najgorsza** to porzadek calkowity: tier rosnaco, potem sila, potem jakosc modyfikatora (`PriceMultiplier`), na koniec id. Wrak (`LootPrices.IsWreck`) jest zawsze najgorszy w swoim id.
  *Dlaczego:* tak mowi regula Jeffa z 30.08 ("nizszy tier, przy rownym nizsza wartosc"), a porzadek calkowity z 19.09 sprawia, ze wynik nie zalezy od kolejnosci rostera.
- **Uzyteczna** = `ItemReq.Meets(oddzial, sztuka)` jest spelnione dla co najmniej jednego czlowieka, ktory nosi ten typ. Sztuki wykluczone przez straz bitewna (`QuartermasterLaw.BarredInBattle`: unikaty, klingi lore, sprzet umarlych) nigdy nie sa "do noszenia" i przypadaja graczowi jak dzis.
- **Sztuka ludzi / sztuka gracza** (druzyna): decyduje ksiega wkladow (`ArmouryBehavior.StockOf`, per id). Przy jednym id ksiega gracza obejmuje NAJGORSZE egzemplarze.
  *Dlaczego:* ksiega nie zna stanu sztuki, wiec watpliwosc rozstrzygamy na korzysc ludzi (nic z niczego).
- **`SwapMath`** (nowy plik) to czyste funkcje bez typow gry: czlowiek = skill, sztuka = klucz, tier, sila, wymog, jakosc, ile jej jest, ile z tego gracza. Licza dopasowanie, wymiane 1:1 i przydzial do koszykow.
  *Dlaczego:* jedna implementacja obsluguje druzyne, zalogi i prog K1-C, a da sie ja sprawdzic poza gra (C5).

## 3. (A) Dozbrajanie za swoje

**A1. Skad pieniadze.**
- Partie: bez zmian - sakiewka = zold (SoldierPay) + udzial w lupie.
- Zalogi: NOWA sakiewka w tym samym slowniku `MenPurse._purse`, kluczem jest StringId partii zalogi, zapis jak dotad w "arm_menpurse".
- *Dlaczego:* jeden mechanizm i zadnego nowego zapisu.

**A2. Zold zalogi.**
- W `SoldierPay.Route` (galaz zalogi, `:374-389`) `MenGearSavePercent` (50%) faktycznie wyplaconego zoldu trafia do sakiewki zalogi, a reszta jak dzis do kasy osady.
- Gdy sakiewka osiagnie limit z A3, caly zold idzie do kasy.
- `ArmyClothing.OnGarrisonPaid` dostaje tylko czesc, ktora poszla do kasy.
- *Dlaczego:* pieniadze i tak koncza w tej samej kasie, tylko pozniej i jako zakup sprzetu z jej targu. Decyzja Jeffa z 05.10 ("zold garnizonu do kasy jego osady") zostaje wiec w mocy co do miejsca.

**A3. Oszczedzanie (partie).**
- `MenPurse.OnLeft` (`:185-210`) najpierw odklada rezerwe na zalegle naprawy i placi za odziez (150).
- Z tego, co zostaje, `MenGearSavePercent` (50%) zostaje w sakiewce na sprzet, a reszta idzie "na zycie" do kasy miasta.
- Kwota ponad `MenGearSaveDays` (30) x dzienny zold partii (`mp.TotalWage`) tez idzie na zycie.
- *Dlaczego:* Jeff 05.10 wymienil "polepszenie sprzetu" wsrod wydatkow ludzi, a bez odkladania na drozsza zbroje nigdy nie starczy. Limit jednego miesiaca zoldu nie pozwala trwale wyjac pieniedzy z obiegu.

**A4. Kiedy.**
- Druzyna gracza i lord AI: w MIESCIE, przy wjezdzie i raz na dobe postoju. U gracza to nowy `DailyTickPartyEvent`, u AI `AiGear.OnDailyTickParty`.
- Zaloga: raz na dobe, na targu swojej osady.
- Zaloga zamku: najpierw polka zamku. Gdy nie ma na niej kandydata, kupuje w najblizszym miescie swojego krolestwa, ktore nie jest w wojnie - zloto idzie do kasy tego miasta, a sztuke przywozi woz pana bez osobnego kosztu. Dotyczy to brakow i lepszych.
- *Dlaczego:* sakiewka zyje w miastach (MenPurse liczy tylko miasta), a polki zamkow sa puste - bez tej reguly zalogi zamkow nie dozbroilyby sie nigdy.

**A5. Kolejnosc w jednym zdarzeniu:** sprzedaz nadwyzek (A9) -> naprawy -> braki (A6) -> lepsze (A7).
*Dlaczego:* Jeff 14.09: "najpierw braki, potem wymiana". Naprawy maja pierwszenstwo od wpisu 84.

**A6. Braki - poprawka.**
- Gracz:
  - Brak = `FitFor(...).UnfitMen`, czyli ludzie bez UZYTECZNEJ sztuki.
  - Kupiona sztuka musi miec wymog <= `UnfitMinSkill` danego typu. To ta sama liczba, ktora kwatermistrz pokazuje jako "bring <= N".
  - Dzis `NeedForType - HaveFor` (`MenPurse.cs:295`) liczy jako "ma" takze sztuki ponad umiejetnosc.
- AI: bez zmian (koszyki t/t-1, `AiGear.TryBuy`).
- **K1c (przeglad K1b, P1 "ten sam sufit dla ludzi gracza i AI"):** braki tez najwyzej do sufitu jednostki (`SwapMath.CeilingTier`). AI: koszyk wzorca ponad sufit liczy sie jako koszyk sufitu (t4 z tarcza t6 we wzorcu kupuje na brak t5 albo t4; ten sam tier w potrzebie i w pokryciu zapasem - bez petli "tier nizej" z wpisu 89). Gracz: k-ty zakup typu najwyzej do k-tego sufitu ludzi bez sztuki (od najwyzszego) - rekrut bez zbroi nie kupi plyty.
- Zaloga:
  - Placi najpierw z sakiewki zalogi, potem z kiesy pana osady.
  - U gracza z jego kiesy tylko wtedy, gdy wlaczone jest `GarrisonBuysGearPlayer`. Zamiast `return` w `AiGear.cs:180` budzet kiesy ustawiamy na 0, a sakiewka dziala dalej.
- *Dlaczego:* "za swoje" - Twoja zaloga kupuje z wlasnego zoldu, a Ty doplacasz tylko wtedy, gdy chcesz.

**A7. Lepsze - co kupuja.**
1. *Przydzial.* Sztuki ludzi danego typu idziemy do koszykow od najwyzszego tieru, najlepsze najpierw. Sztuka trafia do koszyka tylko wtedy, gdy ktos z tego koszyka ja udzwignie. Czego zaden koszyk nie przyjmie, to zapas (A9).
2. *Kandydat do wymiany* to najslabsza sztuka koszyka. Bierzemy go pod uwage tylko wtedy, gdy koszyk jest pelny (najpierw braki).
3. *Zakup z polki miasta.* Wymagania:
   - ten sam typ i ta sama klasa broni (`PrimaryWeapon.WeaponClass`: miecz za miecz, tarcza za tarcze tej samej klasy);
   - tier nie wyzszy niz sufit: tier JEDNOSTKI, a przy `MenUpgradeOneTierUp` (domyslnie) o jeden wyzej - P1 rozstrzygniete 09.10 (`SwapMath.CeilingTier`; najmniej 1; ten sam sufit dla ludzi gracza, lordow AI i zalog; paczka 175 daje wzorcom sprzet ich tieru);
   - sila >= sila starej x (1 + `MenUpgradeMinGainPercent`/100) albo wyzszy tier;
   - `ItemReq` spelnione dla oddzialu z tego koszyka;
   - jesli w koszyku sa jezdzcy - bron, ktorej da sie uzyc z siodla (jak `Mends.MountOk`);
   - bez unikatow i tylko sztuki sprawne (bez modyfikatora < 1).
4. *Wybor:* najwiecej sily za denara netto, czyli (sila nowej - sila starej) / (cena nowej - cena skupu starej).
5. *Kolejnosc:* najpierw sloty z najwieksza roznica "sufit jednostki - tier najslabszej sztuki", potem typy w kolejnosci `AiGear.Order` (korpus, bron, tarcza, helm, ...).
6. *Limit:* `MenUpgradeMaxPerVisit` (20) sztuk na zdarzenie. Polke przegladamy raz na typ.

*Dlaczego:*
- Regula "najslabsza sztuka koszyka na lepsza, najwyzej o stopien ponad tier jednostki" stale podnosi sprzet, ale nie pozwala przeskakiwac wiecej niz o jeden stopien - dalsze przychodza z awansem.
- Prog 10% nie pozwala kupowac w kolko.
- Ta sama klasa broni chroni przed sprzetem, ktorego bitwa nie wyda - DTE i straz wydaja bron wedlug klasy.

**A8. Za ile, kto placi, co ze stara sztuka.**
- *Cena* = cena polki tego miasta (`MarketData.GetPrice(..., false, ...)`). Placi sakiewka, pieniadze ida do kasy miasta (`Town.ChangeGold`; liczniki `MoneyLedger NGear` i `N169Kit`).
- *Budzet* = sakiewka minus rezerwa na zalegle naprawy (`TroopSelfMend.OutstandingCost` / `AiWear.OutstandingCost`). Kupujemy tylko wtedy, gdy cena <= budzet - najpierw zaplata, potem skup starej.
- *Kto nie placi:* lord AI i pan zalogi NIE doplacaja do lepszych sztuk, tylko do brakow.
- *Stara sztuka* od razu idzie do kupca:
  - cena skupu tej sztuki w tym stanie (`MenPurse.SellPrice`), nie wiecej niz ma kasa miasta;
  - pieniadze trafiaja do sakiewki, a sztuka na polke;
  - gdy kasa miasta jest pusta, sztuka zostaje w zbrojowni jako zapas i odejdzie przy nastepnej sprzedazy nadwyzek.
- *AI:* kupiona sztuka przechodzi przez `AiWear.NoteBought`, a sprzedana przez `AiWear.TakeCondition`.
- *Druzyna gracza:* sprzedawana sztuka nigdy nie pochodzi z ksiegi gracza.
- *Dlaczego:* lepszy sprzet to prywatna inicjatywa ludzi (Jeff 05.10), a obowiazkiem pana jest tylko komplet. Ekonomia jest zamknieta - towar pochodzi z prawdziwej polki, a zloto trafia do prawdziwej kasy.

**A9. Nadwyzki - poprawka.**
- Dotyczy gracza (`SellPlayerSurplus :213-280`) i lorda AI (`SellAiSurplus :328-374`).
- NOWE dla zalogi: raz na dobe, ta sama regula. Trzecia czesc (`LordLootThirdPercent`) dostaje pan osady, reszta idzie do sakiewki zalogi.
- Sprzedaz liczymy po dopasowaniu (K1b: `SwapMath.FitBest` - ten sam porzadek "najlepsze najpierw" co wymiana; u gracza przy wylaczonej wymianie 1:1 dawny `Fit`), a nie po liczbie sztuk:
  1. najpierw sztuki, ktorych nikt nie udzwignie;
  2. potem najgorsze uzyteczne sztuki ponad komplet + `SurplusKeepPercent`.
- W zapasie zostaja najlepsze wolne sztuki uzyteczne.
- U gracza pomijamy tylko jego czesc danego id (najgorsze egzemplarze). Dzis pomijane jest cale id, gdy `StockOf > 0`.
- *Dlaczego:* dzis liczenie po typie zatrzymuje T6, ktorej nikt nie naciagnie, a sprzedaje uzyteczne T3.

**A10. Zaloga w bitwie (K1-C).**
- Latka na DTE `DynamicTroopMissionLogic.TryInitializeDistributors` (postfix przez refleksje; bez DTE nic sie nie wpina).
- Dla zalogi bioracej udzial w bitwie gracza tworzymy `PartyEquipmentDistributor` z jej zbrojowni (`SanitizePartyArmory`), tak jak dla partii lorda (`:108-119`).
- ~~Warunek: zbrojownia pokrywa >= `GarrisonArmoryMinFillPercent` (75%) slotow wzorca, liczone dopasowaniem `SwapMath`. Ponizej progu zaloga walczy we wzorcu jak dzis i zostaje linia w logu.~~
- **P2 rozstrzygniete 09.10 (Jeff: "reszta walczy bez uzbrojenia po prostu") - `GarrisonFightsWithArmoryOnly` (domyslnie):** rozdzielacz dla kazdej zalogi, bez progu (takze przy pustej zbrojowni). Zaloga walczy TYLKO tym, co ma: kto nie ma sztuki, walczy bez niej. DTE nie dopelnia pustych slotow (`Assignment.FillEmptySlots` pominiete dla przydzialow zalog) i nie daje zestawu awaryjnego (`PartyEquipmentDistributor.ApplyEmergencyLoadout` pominiete dla rozdzielacza zalogi). Wylaczone - jak wyzej (prog 75%, ponizej wzorzec).
- Straz `SkillLawWard` z CrashScribe obejmie ja sama, bo jest postfiksem na kazdym rozdzielaczu.
- **K1c (przeglad K1b):**
  - *Naprawde bez sprzetu z niczego.* `DressCode` (prefiks `Mission.SpawnAgent`, po DTE) nie ubiera przydzialow zalog w trybie "tylko to, co ma" (`GarrisonKit.IsBareEquipment` - ta sama referencja `Assignment.Equipment`, ktora DTE daje agentowi). Dotad kazdy pusty slot pancerza dostawal sztuke wzorca z niczego, a po bitwie DTE (`ReturnEquipmentFromAgents`, `ItemsToRecover`) oddawal ja do zbrojowni zalogi.
  - *Zaloga bez wpisu w zbrojowniach DTE* dostaje pusty wpis (jak w `PartyEquipmentDistributor.Spawn`). Bez niego `SpawnAgentPatch` odrzuca partie (`MobileParty.IsValid`: bez wpisu i bez `LeaderHero` = false), wiec ludzie szli w pelnym wzorcu za darmo, a po bitwie caly ich sprzet wracal do zbrojowni przez nasz rozdzielacz (mennica). Bezpiecznik: rozdzielacz i strona bitwy tylko przy wpisie.
  - *Straz "nic z niczego"* (`GarrisonKit.Guard`, postfiks `FillEmptySlots` z `Priority.Last`): kazda nietymczasowa sztuka przydzialu zalogi (sloty 0-3, 5-9) musi miec pokrycie w zbrojowni zalogi tuz przed spawnem. Straze CrashScribe (`RealmWard` - obca zza Waskiego Morza, `ArmourWard` - unikat, sprzet umarlych, pancerz ponad Atletyke) podmieniaja sztuke na sztuke wzorca z niczego; teraz w trybie "tylko to, co ma" slot zostaje pusty, a w trybie z progiem slot jest tymczasowy (nie wraca do zbrojowni). Lordowie AI - bez zmian (mennica DTE sprzed K1).
  - *Zapis:* DTE zapisuje zbrojownie tylko partii z `LeaderHero` (`ShouldPersistParty`) - zbrojownie zalog ginely przy kazdym wczytaniu. Armoury zapisuje je samo (`arm_garrisonarmory` przez `SaveText.Sync`, osada -> przedmiot:ile) i odtwarza po wczytaniu (`OnSessionLaunched`, po `OnGameLoaded` DTE, przed ColdStart; zaloga, ktora DTE juz ma - pominieta). Stary zapis nie ma klucza - po pierwszym wczytaniu zalogi zaczynaja z pusta zbrojownia (jak po kazdym wczytaniu dotad) i odkupuja braki.
  - *Zakres P2:* zbrojownie DTE dzialaja tylko w misji, czyli w bitwie, ktora gracz toczy osobiscie. Autobitwy AI-AI i "wyslij wojsko" licza model symulacji BK/ROT z tieru i wzorca - zbrojowni nikt tam nie czyta (przeglad: brak odwolan do `PartyArmories` w BannerKings.dll i ROT.dll). Biedne zamki AI nie padaja wiec masowo w wojnach AI-AI; slabsze sa tylko wtedy, gdy szturmujesz je osobiscie. Odwrotnie przy obronie: Twoja zaloga broni sie pelna sila bez Ciebie (symulacja), a tylko swoim sprzetem, gdy bronisz osobiscie. Jedna regula dla calego swiata wymagalaby latki na model symulacji (sila zalogi x pokrycie slotow) - dopiero po pomiarze w autotescie i na slowo Jeffa.
  - *Odstepstwo od "jednej reguly" w tej samej bitwie:* Twoi ludzie z pustymi slotami dostaja od DTE zestaw awaryjny (sprzet podstawowego zolnierza kultury, tymczasowy), ludzie lordow AI - zestaw awaryjny i `FillEmptySlots` (wzorzec z niczego, wraca do ich zbrojowni - mennica DTE sprzed K1), zaloga - nic. Zdjecie zestawu awaryjnego wszystkim to osobna decyzja Jeffa (zmienia bitwy jego druzyny).
  - *Konie:* do zbrojowni zalog nie trafiaja konie ani rzedy (zakupy brakow ich nie kupuja, "Hand kit" je odrzuca), wiec w wypadzie zalogi (bitwa w polu) jazda zalogi walczy pieszo. W szturmie konie i tak sa wylaczone. Konie dla zalog - osobna decyzja.
  - *Nowy stan w misji:* obroncy bez zadnej broni (pusta albo dziurawa zbrojownia). Zachowanie AI RBM i oblezen ROT wobec takich agentow nie jest sprawdzone - proba R12 rozszerzona.
- *Dlaczego:* bez tego sprzet dany zalodze (A6-A8, B6) nic nie zmienia w walce. Prog chroni przed nagle bezbronnymi zalogami w starych zapisach, w ktorych zbrojownie zalog byly kasowane az do wpisu 89.

**A11. Zaloga traci ludzi albo osade.**
- Sakiewka zalogi, ktorej partia zostala zniszczona, idzie istniejaca droga `MenPurse.OnPartyDestroyed` (do zwyciezcy albo do najblizszego miasta).
- Gdy przy dziennym ticku zaloga ma 0 ludzi, jej sakiewka idzie do kasy osady.
- *Dlaczego:* nic nie znika i zadne pieniadze nie zostaja bez wlasciciela.

**A12. Poza K1:**
- bitwy AI z AI bez misji i "wyslij wojsko" gracza (symulacja nie patrzy na zbrojownie - takze zalogi; K1c: zakres P2 w A10);
- oplata za awans zolnierza (dzis tylko pomiar, `CirculationWindows.cs:547-558`);
- karawany, bandy i milicja;
- zakup lepszych koni (Stajnia).

## 4. (B) Wymiana z graczem

**B1. Regula (druzyna i zaloga).** Liczona po zamknieciu ekranu, osobno dla kazdego typu.
**Poprawione 09.10 wedlug decyzji Jeffa (04:40, WYMIANA):** "jesli dasz cos taniego, ale gorszego od tego, co maja - oni tego tez nie biora, zostaje po prostu w okienku DTE, co oznacza, ze maja wszyscy lepszy sprzet albo wymagania byly za duze i nie spelniaja np. atletyki".
- Twoja sztuka moze wyprzec sztuke ludzi TYLKO wtedy, gdy jest od niej LEPSZA (porzadek "gorsza/najgorsza" z rozdz. 2: tier, sila, wrak, jakosc - bez rozstrzygania po id, wiec rowna nie jest lepsza) i czlowiek spelnia jej wymog (`ItemReq`).
- Twoja sztuka, ktora ktos nosi wedlug dopasowania, przechodzi na ludzi.
- Za kazda taka sztuke, ktora WYPARLA sztuke ludzi (ktos ja nosil przed Twoim wkladem, a teraz nikt), dostajesz JEDNA sztuke ludzi tego typu. Jest to najgorsza sposrod ich wolnych UZYTECZNYCH sztuk (wypartych i zapasu). Ludzie zatrzymuja lepszy zapas, zgodnie z regula z 30.08.
- Twoja sztuka, ktora wypelnila puste rece, przechodzi bez zwrotu (najpierw braki - Jeff 14.09). Puste rece wypelnia kazda uzyteczna sztuka, takze gorsza.
- Twoja sztuka gorsza, rowna albo za trudna NIE jest noszona - zostaje w oknie DTE jako Twoja (do zabrania), a ludzie zatrzymuja swoj lepszy sprzet. Dotychczasowy przypadek "wklad wyparl lepsza sztuke ludzi, bez zwrotu" ("pushed out better kit of theirs") nie moze sie zdarzyc.
- Komunikat po zamknieciu mowi, ile Twoich sztuk zostalo w oknie, bo nikt ich nie chcial (ludzie maja rowne albo lepsze) albo nie udzwignal (ten, komu bylyby lepsze, nie spelnia wymogu). **K1c (przeglad K1b):** liczy TYLKO sztuki wlozone w tej sesji ekranu (rejestr egzemplarzy id|stan, `SwapMath.TallyDeposits`), z nazwami - stary schowek, dawne zwroty i konie sa tylko w logu; zdanie nie dolacza sie do porzadku przy wjezdzie do miasta. Wklad lepszego egzemplarza (np. "Fine X") obok gorszego egzemplarza ludzi tego samego id przechodzi na ludzi za ich gorszy egzemplarz (ksiega per id na najgorsze, C6.6) - komunikat mowi to wprost ("went to the men for their own copy of the same item in worse condition"), a nie "nobody wanted".
- Twoja sztuka ponad potrzebe (wszyscy maja rowne albo lepsze) zostaje Twoja.
- Przy identycznym egzemplarzu (to samo id i stan) najpierw nosza ludzie swoje sztuki, a Twoja zostaje Twoja.
- Reszta wolnych sztuk ludzi nalezy do LUDZI: sprzedadza je w miescie albo Ty je od nich kupisz (B3).

*Dlaczego:* to doslownie zasady Jeffa z 29.08, 30.08, 14.09 i 19.09 (1:1, najgorsza tego samego typu, najpierw braki, stary luk wraca). Darmowy zapas ludzi lamal regule "nic z niczego" i decyzje z 05.10.

**B2. Liczenie (`SwapMath`).**
- Dwa dopasowania tym samym algorytmem. ~~Jak dzis w `FitFor`: ludzie od najwyzszego skilla, sztuki wedlug wymogu malejaco, potem RangedRank, jakosc, id.~~ **Od K1b:** "najlepsze najpierw" (`SwapMath.Greedy`): sztuki (osobno czesc ludzi i czesc gracza egzemplarza) od najlepszej (tier, sila, wrak, jakosc), przy rownych najpierw sztuki ludzi; sztuka wchodzi, jesli da sie ja dolozyc do przydzialu bez zdejmowania wczesniejszych (sciezka powiekszajaca). To zachlanna baza matroidu przydzialow: tylu ludzi ze sztuka, ilu w FitCore (najwiecej), a do tego najlepsze sztuki, jakie udzwigna. Z kolejnosci wynika B1: kazda wyparta sztuka ludzi ma noszony wklad od niej lepszy, a gorszy albo rowny wklad wchodzi tylko w puste rece.
  - PRZED = tylko sztuki ludzi (Total - Own).
  - PO = cala polka.
- Na egzemplarz e:
  - `ownWorn_e = max(0, Used_e - (Total_e - Own_e))`
  - `wyparte = suma max(0, UsedPrzed_e - (Used_e - ownWorn_e))`
  - `X = min(suma ownWorn_e, wyparte)`
- Graczowi przypada: `Own_e - ownWorn_e` oraz X najgorszych wolnych uzytecznych sztuk ludzi. Sztuki wykluczone (BarredInBattle) przypadaja graczowi jak dzis.
- Nowy stan ksiegi per id = suma po egzemplarzach. Korekty robimy jak dzis (`StockDeposit/StockWithdraw`).
- Koszt: dwa dopasowania na typ przy otwarciu i zamknieciu ekranu, czyli dwa razy wiecej niz dzis.
- Migracja nie jest potrzebna: co dzis jest w Twojej ksiedze, zostaje Twoje (wchodzi jako `Own`).

**B3. Druzyna - UI (bez nowego ekranu).** Miasto -> menu DTE "Army armory", jak dzis.
1. *Otwarcie.* Noszone sztuki sa schowane jak dzis, ale liczone per egzemplarz (`Used_e`), a nie per id. Widac Twoje sztuki (za darmo) i wolny zapas ludzi.
   Komunikat: `QM: free to take - N pcs that are yours (M of them handed back in exchange); the men's spare (K pcs) is theirs - take it and you pay them the merchant's price on closing.`
2. *Wkladasz lepsze i zamykasz.* Komunikat: `QM: the men took N pcs of yours (...): B filled empty hands, X replaced worse kit - those X worse pcs are yours in the stash (...); C pcs of yours stay in the stash: W nobody wanted (the men have as good or better, or no use for them), H nobody who would gain from them can use them (skill requirement).`
   Gdy nie wzieli nic (same gorsze albo za trudne): `QM: the men took nothing - C pcs of yours stay in the stash: ...`.
   Gdy nikt nie udzwignie sztuki, zostaje dzisiejszy komunikat "no man can use ... stays yours".
3. *Otwierasz ponownie.* Gorsze sztuki leza na liscie jako Twoje (Jeff 29.08: "otwieram i zamiast moich lukow leza wymienione"). Zabierasz je i sprzedajesz.
4. *Bierzesz cos z zapasu ludzi.* Przy zamknieciu placisz im cene kupca (istniejace `MenPurse.NoteBuy/SettleBuys`, wpis 84 pkt 5). Czego nie mozesz oplacic, wraca na polke.

Zmiany w kodzie:
- `FitFor` i `PurgeUnusable` zwracaja mape: egzemplarz -> (Used, czesc gracza).
- `HoldReserve` chowa `Used_e` zamiast "wszystkiego poza ksiega".
- `BookPostfix` liczy darmowe wyjecie per egzemplarz z tej mapy (dzis per id). Nadwyzka idzie do `NoteBuy`.
- `OnScreenReset` cofa takze mape.
- Wylacznik `QuartermasterSwapOneForOne = false` przywraca dzisiejsza regule "wszystko nienoszone dla gracza".

*Dlaczego:*
- Jeff zna ten ekran - zmienia sie tylko to, co jest darmowe.
- Zakup od ludzi z wpisu 84 wreszcie zaczyna dzialac.
- Liczymy per egzemplarz, bo przy liczeniu per id gracz mogl zabrac sprawny egzemplarz ludzi zamiast swojego obitego.

**B4. Zamowienie u kowala to Twoj wklad.**
- Dotyczy `SmithMenu.DoOrderKit` (`:1655-1680`) i zamowienia z polki (paczka 145).
- Dostarczone sztuki wpisujemy do ksiegi gracza i do rejestru wymian (`StockDeposit` + `QuartermasterEscrow.NoteDeposit`), a nie prosto na stan wojska.
- Rozliczenie nastepuje przy najblizszym otwarciu zbrojowni.
- *Dlaczego:* zaplaciles. Jesli lepsza sztuka wyparla gorsza, gorsza jest Twoja; jesli wypelnila brak, przechodzi na ludzi jak dzis.

**B5. Ksiega musztry (przydzial).**
- Bez zmian. Rozkaz wybiera, co oddzial ma nosic, i chroni sprzet przed przycinaniem magazynu, ale o wlasnosci decyduje dopasowanie (zasada z 19.09).
- Przydzielona Twoja sztuka liczy sie jak wklad (B1).
- *Dlaczego:* jedna regula wlasnosci dla wszystkiego.

**B6. Zaloga - UI (nowe).**
- *Gdzie:* menu miasta i menu zamku, gdy osada nalezy do Twojego rodu i ma zaloge. Opcja `Hand kit to the garrison`, z podpowiedzia w rodzaju `Garrison kit: 82% of slots filled; short: 12 helmets, 4 shields`.
- *Ekran:* `InventoryScreenHelper.OpenScreenAsReceiveItems(new ItemRoster(), "Garrison", done)`. Lewa strona jest pusta, przeciagasz na nia sztuki z sakw.
- *Po zamknieciu* (`done`; Cancel cofa rostery, wiec lewa strona jest pusta i nic sie nie dzieje):
  1. Wracaja od razu do sakw, z komunikatem:
     - konie i rzedy (Stajnia);
     - unikaty, klingi lore i sprzet umarlych;
     - nie-sprzet;
     - sztuki z modyfikatorem na plus: `The garrison books only plain kit: X stays with you.` Zbrojownia AI nie zna modyfikatorow na plus, wiec sztuka stracilaby wartosc.
  2. Reszta przechodzi przez B1/B2 na rosterze zalogi i jej zbrojowni DTE (`AiGear.Armories()[garrison.Id]`).
  3. Ruch fizyczny:
     - noszone wklady -> `AiGear.AddToArmory` (+ `AiWear.NoteBought` ze stanem);
     - X gorszych sztuk -> ze zbrojowni zalogi (z najgorszym stanem przez `AiWear.TakeCondition`) do `MainParty.ItemRoster`;
     - nienoszone wklady -> z powrotem do sakw.
  2a. **K1c (przeglad K1b):** sztuki zalogi tych id, ktore wkladasz, ida do porownania ze swoim stanem z `AiWear` (obite osobno) - nowy egzemplarz jest lepszy od obitej sztuki zalogi, a nie "rowny". Nienoszone sztuki typu, ktorego DTE nie przyjal w calosci, maja swoj powod (nikt nie chcial / wymog), a "the stores would not take" to tylko odrzucone przez DTE.
  4. Komunikat: `The garrison of X took N pcs (...): B filled empty hands, X replaced worse kit - you got the worse ones back (...). C pcs went back to your bags: W nobody wanted (...), H nobody who would gain from them can use them (skill requirement).` (K1b: ta sama regula B1 - gorsza, rowna albo za trudna wraca do sakw.)
- *Zapas:* gdyby ekran nie przyjmowal przeciagania w lewo, uzywamy `OpenScreenAsStash(roster tymczasowy)` i rozliczamy w `ReleasePostfix`, jak w zbrojowni DTE.
- *Dlaczego:* zaloga nie ma ekranu DTE. Natychmiastowy zwrot do sakw nie wymaga drugiej ksiegi (zadnej trwalej "polki gracza" w zalodze), a dzieki K1-C ten sprzet liczy sie w obronie.

**B7. Niezmienniki** (sprawdzane w logu i w probie):
1. Liczba sztuk w zbrojowni i w sakwach jest taka sama przed i po - wymiana nie tworzy i nie kasuje sztuk.
2. Zwroty X <= liczba noszonych wkladow.
3. Czesc gracza danego egzemplarza nigdy nie przekracza liczby sztuk tego egzemplarza i nigdy nie jest ujemna.
4. Drugi przebieg bez zmian na polce daje zero korekt (stabilnosc z 19.09).
5. Wymiana odbywa sie bez pieniedzy. Zakupy od ludzi i zakupy ludzi zawsze ida przez kasy (sakiewka, kiesa, kasa miasta).

**B8. AI.** Lordowie AI nie wymieniaja sie sprzetem z wlasnymi ludzmi - pan daje tylko braki. Spojnosc AI zapewnia ta sama regula z punktow A1-A11.

## 5. (C) Log, wylaczniki, testy

**C1. Linie logu** (Armoury.log, po polsku bez polskich znakow):
- *Zdarzenie* (gracz zawsze, AI do `AiGearLogPerDay`):
  `Dozbrajanie: <kto> w <miasto> - lepsze N szt. za X (BodyArmor t2->t3 padded_coat->mail_hauberk 2400; ...), stare sprzedane M za Y, stare do zbrojowni K (kasa miasta pusta); sakiewka A -> B.`
- *Doba:*
  `Dozbrajanie: dzien D - gracz n/x, lordowie n/x, zalogi n/x (szt./zloto); stare sprzedane M za Y (do zbrojowni K); pominiete koszyki: brak lepszej na polce P, za malo w sakiewce Q, nikt nie udzwignie R; odlozone przy wyjazdach S, ponad limit na zycie T; zold zalog do sakiewek G (n zalog), w sakiewkach zalog Z; zalogi w bitwie ze zbrojowni a / we wzorcu b.`
- *Dopiski do istniejacych linii:* `Sakiewka ludzi:` + "odlozone na sprzet S"; `Zold:` + "zalogi do sakiewek G".
- *Wymiana w druzynie* (na typ, gdy cos sie zmienilo):
  `Wymiana: <Typ>: wklady noszone A (braki B, wymiana X), graczowi: id(mod) xN, ...; wklady nienoszone C zostaja gracza; zapas ludzi D.`
  Podsumowanie: `Wymiana: razem ludzie wzieli A, oddali X gorszych, braki B; widoczne: gracza F, zapas ludzi G.`
- *Wymiana z zaloga:*
  `Wymiana zalogi <osada>: przyjete A (ids), braki B, oddane graczowi X (ids), zwrocone C, odrzucone R (kon/unikat/plus/nie sprzet).`
- *K1-C:*
  `Zaloga w bitwie: <osada> zbrojownia F% (prog 75) - walczy tym, co ma | we wzorcu (za malo).` K1b (P2): `Zaloga w bitwie: <osada> zbrojownia F% - walczy tylko tym, co ma (kto nie ma sztuki, walczy bez niej).`; w linii doby `sloty wzorca bez sztuki - walcza bez N, bez zestawu awaryjnego DTE M`.
  Przy starcie: `GarrisonKit: latka DTE (zaloga w bitwie) wpieta|BRAK; menu zalogi wpiete.`
- *Komunikaty gracza (EN):* te z B3 i B6, a ponadto:
  - `Your men bought N better pieces with their own coin for X denars (Mail Hauberk for Padded Coat, ...); the old ones fetched Y.`
  - przy wyjezdzie: `... They put by S for better kit (purse P).`

**C2. Wylaczniki MCM** (`Settings.cs`, potem `python tools/gen_mcm.py`). Domyslnie wszystko WLACZONE, opisy po angielsku slowami gracza (wzor: `MenPurseEnabled`).

| Pole | Domyslnie | Co obejmuje |
|---|---|---|
| `MenUpgradeGear` | true | A7-A8 dla wszystkich |
| `MenGearSavePercent` | 50 | A2, A3 |
| `MenGearSaveDays` | 30 | A3 - limit oszczednosci |
| `MenUpgradeMinGainPercent` | 10 | A7 - prog lepszej sztuki |
| `MenUpgradeMaxPerVisit` | 20 | A7 - limit sztuk |
| `GarrisonPurseEnabled` | true | A1, A2, A6/A9 dla zalog, A11 |
| `QuartermasterSwapOneForOne` | true | B1-B4 (false = regula z dzis) |
| `GarrisonKitMenu` | true | B6 |
| `GarrisonArmoryInBattle` | true | A10 |
| `GarrisonArmoryMinFillPercent` | 75 | A10 - prog (tylko przy wylaczonym `GarrisonFightsWithArmoryOnly`) |
| `MenUpgradeOneTierUp` | true | K1b, P1: sufit zakupu o jeden stopien ponad tier jednostki |
| `GarrisonFightsWithArmoryOnly` | true | K1b, P2: zaloga walczy tylko tym, co ma (bez progu, bez wzorca i zestawu awaryjnego) |

- `GarrisonBuysGearPlayer` zostaje. Od teraz oznacza tylko doplate z Twojej kiesy do brakow Twoich zalog.
- Czesc A dziala tylko przy wlaczonym `MenPurseEnabled`, czesc B przy `ArmouryProtectUsed`.

**C3. Zapis.**
- Nic nowego: sakiewki zalog trafiaja do "arm_menpurse" (`SaveText.Sync`, okolo 300 kluczy, kilka KB).
- Mapa egzemplarzy z B3 istnieje tylko na czas otwartego ekranu, liczniki doby tylko w pamieci.

**C4. Pliki.**
- NOWE:
  - `src/SwapMath.cs` - czyste funkcje: dopasowanie, wymiana, koszyki;
  - `src/MenUpgrade.cs` - A4-A8 i linia doby;
  - `src/GarrisonKit.cs` - B6, latka DTE z A10, A9/A11 dla zalog.
- ZMIENIANE:
  - `QuartermasterLaw.cs` - FitFor przez SwapMath, B2 w PurgeUnusable, HoldReserve/BookPostfix/OnScreenReset per egzemplarz, komunikaty;
  - `MenPurse.cs` - kolejnosc A5 i wywolanie MenUpgrade w OnEntered, A3 w OnLeft, A9 w obu sprzedazach nadwyzek, A6 w BuyPlayerGaps;
  - `AiGear.cs` - A6 dla zalog (sakiewka), MenUpgrade po brakach, zamek -> miasto;
  - `SoldierPay.cs` - A2;
  - `SmithMenu.cs` - B4 i opcja menu B6 (albo w GarrisonKit);
  - `ArmouryBehavior.cs` - dzienny tick gracza, Reset;
  - `SubModuleMain.cs` - `GarrisonKit.ApplyAll`;
  - `Settings.cs` i `McmSettings.cs`.
- Bez latek na Spoils.

**C5. Testy.**
- **Build:** kod 0, polecenie z zadania. `gen_mcm`: +10 ustawien, a drugi przebieg nic nie zmienia.
- **Proba poza gra** (`SwapMath` przez refleksje na Armoury.dll, jak proba 128):
  - W1 (29.08): 4 lucznikow z lukami t3, wklad 4 x t6 -> X=4, graczowi 4 x t3.
  - W2 (14.09): 5 lucznikow, 4 luki, wklad 2 x t6 -> 1 brak wypelniony, X=1.
  - W3 (19.09): 1 lucznik z lukiem, wklad 1 luku -> wraca jego stary luk.
  - W4: zapas t2 przy 4 x t3, wklad 1 x t6 -> graczowi t2, ludzie trzymaja t3.
  - W5: wklad z wymogiem 175 przy skillach <= 140 -> zostaje gracza, X=0.
  - W6: wklad identyczny z noszonym egzemplarzem -> zostaje gracza.
  - W7: nieuzyteczne t6 z lupu ludzi + wklad wypelniajacy brak -> X=0 (zadnego prezentu dla gracza).
  - W8: A(150) z E5, B(10) bez sztuki, zapas E4(100), wklad E1 -> X=0.
  - W9: wklad gorszy od wszystkich sztuk ludzi -> zostaje gracza.
  - W10: drugi przebieg -> 0 korekt, a niezmienniki B7 sa spelnione.
  - U1: koszyk t3 z sztuka t2 -> kupuja t3.
  - U2: sztuka t3 o sile 100, na polce t3 o sile 105 -> nic nie kupuja (prog 10%).
  - U3: na polce tylko t4 -> nic nie kupuja (sufit tieru).
  - U4: wymog sztuki powyzej skilla koszyka -> nic nie kupuja.
  - U5: cena wyzsza niz budzet -> nic nie kupuja.
  - U6: kasa miasta 0 -> stara sztuka zostaje w zbrojowni jako zapas.
  - K1b (proba3.log, 42/42): W11 zmienione (tani ciezki luk gorszy od t3 ludzi - zostaje gracza); C1 tanie gorsze - nie biora (nikt nie chcial); C2 lepsza, ale wymog 175 - zostaje (nie udzwigneli); C3 rowna - nie wypiera; C4 braki + gorsze + lepsze; C5 gorsza w puste rece; C6 slaby czlowiek tylko t1; C7 lancuch (wraca najgorsza); C8 za trudna dla potrzebujacego; C9 drugi przebieg 0; CA i U7-U9 sufit +1 (t3 kupi t4, nie t5, tylko jesli go stac); A9b nadwyzki po FitBest; FZ 3000 losowych przypadkow (niezmienniki B7, kazda wyparta ma lepszy wklad, liczba jak FitCore, zachlanna baza sprawdzona niezaleznym doborem Kuhna, drugi przebieg 0).
- **Autotest** (DLL probny; gra zamknieta przed i po; zasady z "zgody na autotest"): nowa kampania 40 dob + 8 dob z zapisu `autotest-161-kawalki`. Progi:
  - 0 bledow Armoury i CrashScribe;
  - czas doby <= 13.8 s (baza 13.1 s + 5%);
  - linia `Dozbrajanie: dzien` codziennie, u lordow > 0 sztuk;
  - stare sprzedane + stare do zbrojowni = liczba kupionych lepszych;
  - w `Sakiewka ludzi` wplynelo - wyszlo = zmiana stanu;
  - suma sakiewek < 30 dni zoldu wszystkich partii i zalog;
  - ksiega pieniadza (169) bez nowej reszty;
  - "zold zalog do sakiewek" > 0, a zalogi robia zakupy z sakiewek;
  - rynek: odsetek miast bez zbroi korpusu t3+ najwyzej o 10 punktow wyzszy niz w bazie;
  - linia startowa `GarrisonKit` mowi "wpieta".
- **Recznie w grze:**
  - R1: wloz luki lepsze od lukow lucznikow -> komunikat B3 "X replaced worse kit"; po ponownym otwarciu gorsze luki sa Twoje; w logu `Wymiana: Bow`.
  - R2: brak (np. "Bow 4/6"), wloz 3 luki -> "2 filled empty hands", 1 zwrot.
  - R3: luk ponad skill wszystkich ludzi -> "no man can use ... stays yours".
  - R4: wez 1 sztuke z zapasu ludzi -> przy zamknieciu "You paid your men ...".
  - R5: wjazd do miasta z sakiewka > 3000 -> "Your men bought N better pieces ..."; przy wyjezdzie "They put by ...".
  - R6: zaloga - 5 lepszych helmow + 1 Masterwork -> gorsze helmy w sakwach, Masterwork wraca; w logu `Wymiana zalogi`.
  - R7: bitwa z udzialem zalogi (obrona wlasnej osady albo szturm zamku AI) -> w logu `Zaloga w bitwie: ... F%`.
  - R8: Reset i Cancel na obu ekranach -> nic sie nie zmienia.
  - R9 (K1b): wloz tanie sztuki gorsze od sprzetu ludzi -> "QM: the men took nothing - N pcs of yours stay in the stash: N nobody wanted ..."; sztuki zostaja w oknie.
  - R10 (K1b): zbroja lepsza, ale z wymogiem Atletyki ponad ludzi -> "... nobody who would gain from them can use them (skill requirement)".
  - R11 (K1b): to samo przez "Hand kit to the garrison" - gorsze i za trudne wracaja do sakw z powodem.
  - R12 (K1b): bitwa z zaloga o zbrojowni < 75% -> "Zaloga w bitwie: ... walczy tylko tym, co ma"; ludzie bez sztuki walcza bez niej (bez sprzetu wzorca). **K1c - rozszerzona:** (a) szturm na zamek z zaloga z wpisem DTE i pusta zbrojownia (0%) oraz z zaloga bez wpisu ("pusty wpis zalozony"); (b) wypad zalogi (sally-out); (c) obrona wlasnego zamku. Za kazdym razem: brak bledow RBM, ROT i CrashScribe, bitwa sie konczy, linia doby "sloty wzorca bez sztuki - walcza bez N, bez zestawu awaryjnego DTE M, bez pokrycia w zbrojowni zatrzymane K" ma sensowne liczby; helm albo korpus wzorca u czlowieka bez sztuki = BLAD (DressCode); liczba sztuk w zbrojowni zalogi po bitwie nie wieksza niz przed + lup (mennica).
  - R13 (K1c): zapis -> wczytanie -> podpowiedz menu "Garrison kit: N% of slots filled" taka sama jak przed zapisem; w logu "Zbrojownie zalog (K1c): zapisane ..." i "odtworzone z zapisu ...".
  - R14 (K1c): wloz "Fine X", gdy ludzie maja zwykly X -> "... went to the men for their own copy of the same item in worse condition ..."; wloz 1 tani helm przy duzym starym schowku -> zdanie mowi o 1 sztuce z nazwa, nie o calym schowku.

**C6. Ryzyka.**
1. *Polki:* ludzie AI kupujacy lepsze sztuki moga wymiesc t3-t5 z polek; ceny rosna (SupplyDemand), a dla gracza zostaje mniej. Chronia limit 20 sztuk i prog 10%; mierzy to prog rynku w autotescie.
2. *Kasy miast:* skup starych sztuk zabiera zloto, ale tylko do wysokosci kasy, a zakupy oddaja wiecej - miasto netto zyskuje. Sprawdzic w linii kas 169.
3. *DTE "Loyal Equipments"* (domyslnie wlaczone) daje w bitwie sztuke ze wzorca albo najblizsza mu sila, z sufitem tieru +2 (`PartyEquipmentDistributor.cs:1210-1390, 1769-1779`). Dopiero straz `SkillLawWard` z CrashScribe rozdziela wedlug wymogu. Bez CrashScribe kupione sztuki moga lezec nieuzywane; sufit "tier koszyka" utrzymuje zakupy w granicach tego, co DTE i tak wyda.
4. *Widoczna zmiana:* zapas ludzi przestaje byc darmowy w zbrojowni i jest teraz na sprzedaz. Jeff to zauwazy; tak wynika z jego decyzji z 05.10 (wpis 84).
5. *K1-C:* zaloga AI z dziurawa zbrojownia (powyzej progu 75%) bedzie przy obleganiu walczyc slabiej niz dzis. K1b (P2): takze ponizej progu i przy pustej zbrojowni - ludzie bez sztuki walcza bez niej (Jeff swiadomie: "reszta walczy bez uzbrojenia po prostu"). K1c: tylko w bitwach toczonych osobiscie (A10, zakres P2); po wgraniu w obecnej kampanii zalogi zaczynaja z pusta zbrojownia (DTE ich nie zapisywal) - przy szturmie walcza prawie nago, dopoki nie odkupia brakow (MCM `GarrisonFightsWithArmoryOnly` = off przywraca prog). Zalogi gracza w nowej grze ColdStart pomija - od startu bez sprzetu, dopoki nie dokupia z sakiewki.
6. *Ksiega per id:* przy tym samym id gracz zawsze ma najgorsze egzemplarze. Wlozony Masterwork X obok zwyklego X ludzi liczy sie wiec jak zwykly. K1c: komunikat mowi to wprost (wymiana na ich gorszy egzemplarz). Sztuki odlozone w oknie DTE "na pozniej" ludzie traktuja jak dane im: lepsze od swoich wezma i oddadza gorsze 1:1 (najpozniej przy wjezdzie do miasta) - co chcesz zachowac, trzymaj w sakwach. Pelne rozwiazanie to ksiega per id i stan, ale to poza K1, bo `StockOf` jest uzywany w wielu miejscach.
7. *Wydajnosc:* dwa dopasowania przy ekranie, MenUpgrade raz na dobe na partie w miescie, polka przegladana raz na typ. Mierzymy czas doby.
8. *Kolizja z Z1* (`noc2/z1` 290d3d2: SpoilsSeal.cs +278 linii, DonationXpLaw.cs): K1 nie dotyka Spoils. Konflikty moga byc tylko w Settings.cs, McmSettings.cs, ArmouryBehavior.cs i SubModuleMain.cs - przy scaleniu zachowac zmiany z obu stron. Ekran zalogi (B6) nie daje XP, bo dziala w trybie Default, a nie Loot (`InventoryLogic.InitializeXpGainFromDonations`).
9. *Zamki:* zalogi zamkow kupuja w najblizszym miescie (A4), wiec na targach miast przygranicznych wzrosnie popyt.

## 6. Pytania do Jeffa (tylko takie, ktore zmieniaja rozgrywke)

- **P1 - stopien. ROZSTRZYGNIETE 09.10 (Jeff: "tak, jesli go na to stac i jest dostepna"):** zolnierz moze kupic sprzet o JEDEN stopien wyzej niz tier jednostki, jesli ma pieniadze w sakiewce i sztuka jest na rynku; wymog umiejetnosci przedmiotu (`ItemReq.Meets`) nadal obowiazuje; ten sam sufit dla ludzi gracza, lordow AI i zalog (`MenUpgradeOneTierUp`, domyslnie wlaczone; A7.3).
  ~~Twoi zolnierze kupuja najwyzej sprzet swojego stopnia: zbrojny T3 kupi najlepsza zbroje T3, ale nie T4. Czy bogaty zolnierz moze kupic sprzet o jeden stopien wyzej? Domyslnie: NIE.~~
- **P2 - zaloga w bitwie. ROZSTRZYGNIETE 09.10 (Jeff: "reszta walczy bez uzbrojenia po prostu"):** zaloga walczy TYLKO tym, co ma w zbrojowni, takze ponizej 75% - kto nie ma sztuki, walczy bez niej (bez pelnego wzorca za darmo i bez zestawu awaryjnego DTE; `GarrisonFightsWithArmoryOnly`, domyslnie wlaczone; A10). **Zakres (K1c, do powiedzenia Jeffowi):** zaloga walczy tylko tym, co ma, w bitwach, ktore toczysz osobiscie; w bitwach rozstrzyganych bez Ciebie (AI-AI, "wyslij wojsko") liczy sie jej stopien jak dotad. Jedna regula dla calego swiata (latka na symulacje) - dopiero po pomiarze i na slowo Jeffa.
  ~~Ponizej progu 75% zaloga walczy jak dzis, w pelnym wzorcu za darmo. Czy ponizej 75% tez ma walczyc tylko tym, co ma? Domyslnie: prog 75%.~~
- **WYMIANA (doprecyzowanie Jeffa 09.10 04:40):** sztuka gracza gorsza, rowna albo za trudna nie jest noszona i zostaje w oknie DTE - B1 i B2 poprawione.

## 7. Etapy i wpis

- **K1-A:** A1-A9 i A11 (`SwapMath` - koszyki, `MenUpgrade`, MenPurse, AiGear, SoldierPay).
- **K1-B:** B1-B5 (`SwapMath` - wymiana, QuartermasterLaw, SmithMenu).
- **K1-C:** A10 i B6 (`GarrisonKit`). Te dwa punkty ida razem, bo sprzet dany zalodze bez A10 nie zmienia walki.
- Kolejnosc A -> B -> C. Kazdy etap musi miec build z kodem 0 i proba poza gra musi przejsc. Jeden wpis w CHANGELOG:
  `## 2026-10-09 (K1) - DOZBRAJANIE: zolnierze kupuja lepszy sprzet za swoje (zold + lup, takze zalogi), wymiana z graczem 1:1 (lepsza za ich najgorsza) w zbrojowni i w zalodze`
  z **Status:** NIEWGRANE - DO SPRAWDZENIA, a w "Ryzyko / co sprawdzic" punkty z C5 i C6.
- Po etapach: autotest (C5) -> scalenie z Z1 na `noc/sklad5` -> wgranie dopiero na slowo Jeffa albo zgodnie z zasada nocnej zgody.
