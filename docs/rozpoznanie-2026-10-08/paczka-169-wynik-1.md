# Paczka 169 - wynik agenta 1 (workflow wf_6488c056-dc9)

## summary

Mapa naszego kodu pod paczke 169 jest gotowa (tylko czytalem, w drzewie roboczym nic nie zmienilem). Prosba Jeffa o zapisanie optymalizacji na koniec jest juz spelniona: lezy w repo Jeffa w docs/STAN-PRAC.md:709-716, commit 7904ccf. Nie ma jej tylko w MEMORY.md. Przy okazji znalazlem jeszcze jedna rzecz na te liste: MoneyLedger przeglada caly swiat dwa razy przy rozliczeniu kazdego rodu. Stan pomiarow z rozdz. 4.1: w pelni zmierzone sa wiersze 2, 4, 5, 7, 8, 9, 23, czesciowo 1, 3, 6, 10, 17, 22. Zupelnie brakuje 1b (zold karawan notabli) oraz 11-16 i 18-21. Z rozdz. 4.2 nie zmierzono prawie nic, poza naszymi wlasnymi warsztatami i rzemieslnikami BK. D najlepiej liczyc w nowej klasie raz na dobe, w OnDailyTick miedzy IronBank.Daily (:1259) a MoneyLedger.Daily (:1264). Zapis: nowy klucz SaveText "arm_clanincome", identyfikator rodu jako klucz. Wynik: linia "Budzet rodow (na sucho)" i CSV w Logs/<sesja>/. Generator MCM sprawdzilem na kopii: dziala powtarzalnie (Armoury 680, RC 104, GT 40, bez roznic).

## findings

0. PROSBA JEFFA ("optymalizacja na koniec - zapisz do zrobienia")
- Juz zapisana w repo Jeffa: C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/STAN-PRAC.md:709-716, sekcja "DECYZJE JEFFA 08.10", commit 7904ccf. Lista obejmuje MenPurse.OnEntered, dzienny tik Armoury, AiGear, tik godzinny i smieci GC. W galezi roboczej (2e235ea) tego wpisu nie ma. W MEMORY.md tez nie ma osobnej notatki. Niczego nie dopisywalem (zadanie tylko do odczytu).
- Nowy kandydat na te liste:
  - MoneyLedger.ClanTickPrefix i ClanTickPostfix (MoneyLedger.cs:309, :324) wolaja WorldTotal() (:642-648, czyli ReadHolders :596-639 i Snap :422-432).
  - WorldTotal to pelny przeglad swiata: osady dwa razy, wszyscy bohaterowie, MobileParty.All, Village.All, warsztaty, sakiewki.
  - Dzieje sie to dwa razy na kazde rozliczenie rodu, czyli ok. 800 pelnych przegladow na dobe, rozlozonych po dobie.
  - Biegnie takze przy LogEnabled=false, bo MoneyLedger nie ma wylacznika.
- Inne drobne koszty:
  - LINQ w goracej sciezce: MenPurse.SellAiSurplus :334-335.
  - Clan.FindFirst w petli: IronBank.Limit :119 (O(n^2)).

1. KOLEJNOSC DOBY (ArmouryBehavior.cs)
- OnDailyTick :1209.
  - MoneyLedger.BlockOpen :1232 -> PopulationLaw.Daily :1233 -> Mark(MRent) :1234.
  - BuildFunding :1237 -> Mark(MBuild) :1239.
  - KingdomTreasury.Daily, Levies, WageRefund i KingdomLedger.Daily :1254 -> Mark(MCrown) :1255.
  - SoldierPay.Daily :1256, OutlawLaw :1258 (jego migawki: OutlawLaw.cs:572, 574, 606), IronBank :1259, MountedWage :1263.
  - MoneyLedger.Daily :1264, potem PeopleLedger :1265.
- Rozliczenia rodow (DailyTickClan) i doby bohaterow (DailyTickHero) gra rozklada po calej dobie (CampaignPeriodicEventManager.cs:400-405, PeriodicTickSome). Nie biegna razem z naszym DailyTickEvent.
- Konstruktor z Reset(): ArmouryBehavior.cs:389 (jedna dluga linia). Sa tam juz MoneyLedger, PeopleLedger, KingdomLedger, SoldierPay, KingdomTreasury, IronBank, MenPurse, LevyGold i PopulationLaw.
- SyncData :391-497:
  - osobny try dla StartStock :396-402;
  - glowny try :403-487 (klucze SaveText.Sync :421-473);
  - osobny try dla MendStock :490-496.
- OnSessionLaunched :979-1010:
  - McmSettings.Apply :985;
  - latki zakladane dopiero w kampanii: MountedWage.EnsureContextHooks :987 (wzor);
  - RecruitKit.ResolvePending :982 (lekcja 161: w SyncData bohaterow jeszcze nie ma).
- Nasluchy ksiegi: ArmouryBehavior.cs:542-544 (Before/AfterSettlementEntered, HeroOrPartyTradedGold), PeopleLedger :546-549.
- Latki: SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot.
  - MoneyLedger.ApplyAll :72 (we wlasnym try), LevyGold :88, KingdomTreasury :90, SoldierPay :91, MountedWage :97, GoodsLedger :136 (na koncu).
  - Latki zakladane sa zawsze. Wylaczniki sprawdzamy w biegu (Settings.Current).
- ROZJAZD NUMERU DNIA:
  - MoneyLedger (:866) i PeopleLedger (:184) pisza (int)Now.ToDays-1.
  - Korona (KingdomTreasury :74, :202, :283), Zold (SoldierPay :520), Skarbce (KingdomLedger :29), IronBank (:196, :284), Ludnosc (PopulationLaw :327), Budowy (BuildFunding :184) i EconomyAudit CSV (:64, :115) pisza (int)Now.ToDays.
  - Ta sama doba ma wiec w logu dwa numery. Nowe linie musza swiadomie wybrac jeden.
- LENIWE LINIE DNIA: liczniki zeruja sie przy pierwszym zdarzeniu nowej doby, nie w naszym ticku.
  - "Sakiewka ludzi" MenPurse.cs:119-129, "Werbunek" LevyGold.cs:107-116, "ZakupyAI" AiGear.cs:188 i :324-330.
  - Linia "Obieg" nie moze czytac tych licznikow w DailyTick. Potrzebuje wlasnych licznikow, zasilanych wywolaniem Note przy samym zdarzeniu.

2. MoneyLedger.cs (896 linii, tylko log, bez wylacznika MCM, bez zapisu w save)
- Okno taboru (zdarzenia gry, nie Harmony):
  - OnBeforeEntered :183-196 zapisuje stan w polach statycznych: _winParty, _winTime (porownanie z CampaignTime.Now - ten sam tik, WinOpen :180), _winGold, _winHomeGold, _winHomeTax.
  - Niedomkniete okno zwieksza licznik _winStale (:188).
  - OnAfterEntered :199-225:
    - w miescie albo zamku: _vPaid (zmiana zlota taboru), _vVisits, Unsold :228-241;
    - we wsi: handed, kept (kiesa wsi), tax (licznik podatku), estates.
  - Wyplata majatkow BK w oknie powrotu (GiveGoldAction z niczego) idzie do _winEstates (:267).
  - "Zniklo" = handed - kept - tax - estates (:680, :738).
- Przelewy gry: OnGoldTraded :254-294, kazde GiveGoldAction (O(1)).
  - Z niczego albo w nicosc: do kasy osady -> _fromNothing / _toNothing[klasa]; poza rozliczeniem rodu -> _worldFromNothing / _worldToNothing; saldo glowy rodu -> _netUp / _netDown; inne w trakcie rozliczenia -> _clanOth* (:273-287).
  - W naszym bloku dobowym przelewy miedzy posiadaczami sa pomijane (:289) - lapia je migawki.
  - Pozostale idzie do _goldIn / _goldOut[klasa, strona] (:290-291; strony: PName :62-63).
  - Z NICZEGO I W NICOSC NIE MA ROZBICIA na strony ani przyczyny.
- Okno rozliczenia rodu (Harmony): ClanTickPrefix :303-313 i ClanTickPostfix :316-330 na ClanVariablesCampaignBehavior.DailyTickClan.
  - Priorytety First i Low (:583-584). SoldierPay ma First i Last (SoldierPay.cs:553-554), wiec rozdziela zold PO naszym pomiarze. Stad osobne zrodlo "zold oddany do obiegu" (:689).
  - Wynik: _clanUp / _clanDown, _clanUpN, _clanDownN, _clanFlatN; niedomkniete rozliczenia -> _clanStale; wyjatki -> ClanStumble :333-339 (pierwszy do pliku, kolejne tylko liczone).
- Liczniki naszych modulow poza tickiem dobowym:
  - Note(kind, osada, kwota) :343-353; rodzaje NGear..NCloth :66-69 (Notes=7).
  - NoteLevyBack :360, NoteWageRouted :363-367, NoteWorkshopPay :378-389, NoteWorkshopFair :392-401, NoteArtisans :408-419.
- Migawki naszego ticku dobowego: Snap :422-432, BlockOpen :435-439, Mark :442-452 (rodzaje MRent..MLife :72-74). Reszta ticku trafia do MRest w Daily :868-869.
- Postfiksy-liczniki:
  - konsumpcja: ShelfPostfix :456-464 i ConsumePostfix :467-479 -> _cons;
  - regulator kasy: RegulatorPostfix :482-498 -> _regIn / _regOut (wpiety we wszystkich modelach :549-568);
  - zold: WagePostfix :518-528 -> _wage[partie rodow / garnizony / karawany / inne];
  - ApplyAll :530-593; linia "wpiete / BRAK" :591-592.
- Posiadacze: ReadHolders :596-639 (21 posiadaczy :77-79). MenPurse.TotalNow i IronBank.CapitalNow czytane z tych modulow.
- Linie dzienne (Daily :861-894; pierwsza doba po wczytaniu to sam stan :872-873; ClearDay w finally :893):
  - "Pieniadz swiata:" StateLine :652-667;
  - "Pieniadz swiata (bilans):" BalanceLine :669-704: zrodla :690 = cons + regIn + clanUp + routed + from; ujscia :691 = clanDown + regOut + vanished + to - levyBack; reszta [R] :699;
  - "Pieniadz swiata (rody):" ClanLine :707-734;
  - "Przeplywy osad:" VillagerLine :736-765 (utarg taborow, podzial utargu, zold wedlug rodzaju, potkniecia);
  - "Przeplywy osad (kasy miast | kasy zamkow | kiesy wsi):" ClassLine :767-841 (pozycje zmierzone, reszta [R], rozklad osad);
  - "Przeplywy osad: modele czynne" :843-856 (raz).

3. POZOSTALE MODULY: co mierza i gdzie w logu
- KingdomTreasury.cs:
  - Transpiler zeruje dosypke gry do skarbca (:25-38, wpiety :387-388).
  - Daily, powinnosci wasali: dochod = CalculateClanIncome(c,false,false,false) + RentToday (:60-62); glowa -> skarbiec przez ChangeHeroGold, bez zdarzenia (:69-70); linia "Korona: dzien N - powinnosci wasali" :74-75.
  - WageRefund :134-214:
    - SoldierPay.TakePaid :141;
    - zwrot przez h.ChangeHeroGold :183 (bez zdarzenia, bez licznika na rod);
    - pomiar zwrotu dla rodow dluznych koronie :189;
    - linia "Korona: ... zwrot zoldu" :202-209;
    - _refund wedlug krolestwa :88-91, dopisek do "Skarbce" przez RefundNote :127-132.
  - Levies :221-287: danina, clo (do skarbca), mennica i monopole przez ruler.ChangeHeroGold :271, :279 (bez zdarzenia); linia :283-284.
  - BRAK dziennej linii wplywow i wyplat korony (zrodlo zwrotu).
- KingdomLedger.cs:
  - "Skarbce:" :61-65 (skarbiec i zmiana, krol, kiesy rodow, biedni, wojsko rodow);
  - "Finanse:" :74 -> plik tematyczny finanse.log;
  - bilans rodu = zmiana kiesy glowy z jednej doby (:51-53);
  - wylacznik FinanceLedgerEnabled (:27-28).
- SoldierPay.cs:
  - para na DailyTickClan :142-153 i :262-270 (_debtBefore :149);
  - latki modelu dopiero w kampanii: EnsureHooks :160-197 (pulapka konstruktora statycznego :44-49);
  - WagePostfix :200-229, NetPostfix :232-249, Settle :273-311 (brak w saldzie i przyrost DebtToKingdom :295-301);
  - Route :313-348 (garnizon -> kasa osady :325, Note :327-328; partia -> MenPurse :341-345; karawany "bez zmian" _dOther :347);
  - AddPaid :350-355 (tylko przy CrownWageRefundEnabled), TakePaid :358-363;
  - linia "Zold:" :520-534 (naliczony / zeszlo / do sakiewek / do kas, przyciecia, sakiewki, tarcza);
  - BRAK zoldu zaplaconego na rod niezaleznie od zwrotu - potrzebny do "zold faktyczny".
- MenPurse.cs:
  - stan _purse (klucz MobileParty.StringId) :32, TotalNow :83;
  - Add :109-116 (_dayIn / _dayOut);
  - "Sakiewka ludzi: dzien" :124-127 (leniwie);
  - OnLeft, zycie w miescie :187-193 (ChangeGold + Note NLife + tarcza);
  - OnPartyDestroyed :63-81 (trzecia lorda :76, kasa najblizszego miasta :78) - BEZ licznika "z poleglymi";
  - trzecia lorda AI przez ChangeHeroGold :347-348 (bez zdarzenia).
- IronBank.cs:
  - _debts (klucz Clan.StringId) :53, CapitalNow :62;
  - Limit = dochod modelu x 60 dni (:110, :113);
  - Lend: ChangeHeroGold :173, bez zdarzenia (pozyczka sama wypada z D);
  - Daily :189-288; linia "IronBank: dzien" :284-285; bankructwo :247; rod wymarly :278;
  - BRAK dostepu do odczytu dlugow (potrzebny do "Dlugi (na sucho)").
- PeopleLedger.cs:
  - "Ludzie:" :273-315 (zolnierze wedlug rodzaju, zabici, werbunek, dezercja, bilans partii rodow z reszta);
  - "Ludzie (regiony):" :331-332;
  - CSV ludzie-regiony.csv przez Log.Csv :371 (wzor CSV).
- LevyGold.cs:
  - ochotnik -> notabl :40-42 (NoteLevyBack :42);
  - najemnik -> kasa miasta :82, Note NMerc :84, NoteLevyBack :86;
  - "Werbunek: dzien" :112-113 (leniwie).
- PopulationLaw.cs:
  - RentToday (Dictionary<Clan,int>) :144, zerowane :274 i :283, wypelniane :316;
  - renta wsi i zawor miast 7% nadwyzki ponad TownRentFloorGold :306-310;
  - GiveGoldAction osada -> pan w bloku dobowym :315;
  - "Ludnosc: dzien" :327-328 - tylko wedlug kultury, bez podzialu wies / miasto.
- Log.cs:
  - Info :62-72: bramka LogEnabled, File.AppendAllText przy kazdym wywolaniu - tylko linie dzienne, nigdy linia na zdarzenie;
  - plik tematyczny wedlug prefiksu: TopicOf :96-112;
  - Csv :78-93; Error :114-122.
- CrashScribe/src/EconomyAudit.cs (osobny DLL, nie ma odwolania do Armoury):
  - CSV economy-*.csv w Scribe.ReportDir (:61-62), wiersz na rod na dobe (:113-125);
  - FamilyGold = wszyscy zywi bohaterowie rodu (:79);
  - dochod / wydatki / saldo modelu z opisami, bez wyplat (:95-99);
  - faktyczna zmiana (:102-105);
  - kluczem wiersza jest NAZWA rodu (:116), dzien (int)today;
  - podsumowanie "EKONOMIA" :128-150;
  - kolejnosc wzgledem tiku Armoury nieznana (dwa zachowania).
  - Odczyt Armoury przez refleksje ma precedens w CrashScribe: Mends.cs:886.

4. POKRYCIE TABELI 4.1 I 4.2 PROJEKTU
- 1 notable, pasmo: CZESCIOWO. Siedzi w "GiveGoldAction z niczego / w nicosc poza rozliczeniami" (:276, :284 -> :695, :698), bez rozbicia. Zmieszane z dziennym zyskiem notabli (ClanVariablesCampaignBehavior.DailyTickHero: przelew z kapitalu udajacy zloto z niczego, opis :683-685).
- 1b zold karawan notabli: BRAK. Gra robi tylko notable.Gold -= i PartyTradeGold -= (NotablesCampaignBehavior.cs:309-330), bez zdarzenia - siedzi w reszcie.
- 2 "zakupy" mieszczan: JEST [P] (:456-479, :693, :780-781).
- 3 GiveGoldAction w nicosc: CZESCIOWO - jedna suma, bez przyczyn (BEE, awanse, ochotnik z mapy, statki); LevyGold odjety (:698).
- 4 reszta: JEST [R] (:699).
- 5 regulator: JEST [P] (:482-498, :782-784); tarcza SoldierPay :430-453.
- 6 GiveGoldAction z niczego: CZESCIOWO - suma, bez przyczyn (jency, oblezenia, turnieje BK).
- 7 sakiewki: JEST jako stan (:662) i ruch (MenPurse :124-127). BRAK: z poleglymi, markietani, zwolnieni.
- 8 skarbce: JEST (zwrot :202-209, "Skarbce:" :61-65). BRAK dziennego bilansu korony (wplywy i wyplaty).
- 9 "zniklo": JEST (:745-748, :697).
- 10 zold karawan lordow: JEST jako "w tym" (:523-524, :750-757; SoldierPay :347 / :526); w bilansie siedzi w _clanDown.
- 11 trybut, 12 Debts, 18 dochod za tier, 19 podatek BK 0.1%, 20 zapomoga gry: BRAK OSOBNO. Wszystkie biegna w oknie DailyTickClan, wiec sa w _clanUp / _clanDown (ClanLine :716-725). Zapomoga i podatek BK to przelewy, dla swiata 0.
- 13 zakup lorda we wsi: BRAK (reszta kies wsi :830).
- 14 lup z cial: BRAK (reszta swiata).
- 15 HandleMarketGold, 16 porty / kopalnie / konwoje / HandleExcessFood: BRAK.
  - Uwaga: nasza wlasna polowa kopalni wraca do miasta bez Note (BuildFunding.cs:96-99).
- 17 wydatki lordow BK: CZESCIOWO. Te przez GiveGoldAction sa w "to"; te przez ChangeHeroGold - w reszcie.
- 21 rada: BRAK.
- 22 dar startowy: tylko stan.
- 23 Bank: JEST (posiadacz HBank, linia IronBank).
- 4.2:
  - zold karawan notabli: BRAK;
  - kasa <-> warsztaty BK (EconomyPatches 835-839, 873-881): BRAK - mierzymy tylko nasze WorkshopTrade (:378-401, :807-816);
  - rzemieslnicy BK: CZESCIOWO - tylko nasz ArtisanInputs (:408-419, :817-821);
  - prowizja 10%: BRAK;
  - konwoje, porty, kopalnie, HandleMarketGold, HandleExcessFood: BRAK;
  - wydatki lordow BK: BRAK;
  - awanse, ochotnik z mapy, statki, jency, oblezenia, turnieje BK: tylko suma;
  - lup z cial: BRAK;
  - dochod za tier, zapomoga, podatek BK: BRAK;
  - majatki BK: tylko wyplaty w oknie taboru (:267, :221).
- Linie z 10.1:
  - "Obieg", "Obieg: notable i karawany", "Budzet rodow", "Dlugi", "ROWNOWAGA 28 dob" i kolumny CSV: BRAK. MoneyLedger trzyma tylko dobe poprzednia (_lastHold :88).
  - "Wojsko": CZESCIOWO ("Ludzie:", "Skarbce:", "Zold konnych" MountedWage.cs:208).
- NASZE WLASNE PRZEPLYWY DO KAS MIAST BEZ NOTE (poza blokiem dobowym, wiec ida do reszty kas miast; dla swiata 0):
  - AiWear.cs:299, :338 (naprawy, przez MenPurse.OnEntered);
  - TroopSelfMend.cs:117, :179;
  - MenPurse.cs:78, :254, :301, :346;
  - RecruitKit.cs:89, UniqueSpoils.cs:211, CaravanBulk.cs:629, BuildFunding.cs:98, SpoilsSeal.cs:392;
  - OutlawLaw.cs:1147, :1329 - sprawdzic, czy biegna w bloku dobowym.

5. JAK DODAC NOWE OKNO I NOWA LINIE (styl repo)
- a) Wpiecie zalezy od celu:
  - Zachowania i akcje gry (NotablesCampaignBehavior, NotablePowerManagementBehavior, ClanVariablesCampaignBehavior.DailyTickHero, PartyUpgrader, akcje): w ApplyAll wolanym z SubModuleMain obok :72, we wlasnym try.
  - Prywatne metody DefaultClanFinanceModel (AddIncomeFromKingdomBudget :517-528, AddPaymentForDebts :219-230 - czytaja statyczne TextObject z Game.Current, :34-82): TYLKO w kampanii. Przy starcie gry pada konstruktor statyczny klasy, na caly proces (SoldierPay.cs:44-49). Wzor: EnsureHooks z pierwszego ClanTickPrefix (SoldierPay.cs:148, :160-197) albo z OnSessionLaunched (ArmouryBehavior.cs:987).
- b) Stan "przed" w `out long __state` prefiksu, postfiks go czyta (wzory: RecruitCost.cs:322, :347; MountedWage.cs:79-80). Gdy okno ustawia kontekst statyczny "przyczyny" dla OnGoldTraded: prefiks zapisuje poprzedni kontekst w __state, finalizer go przywraca (SellByCondition.cs:431-460). Finalizer biegnie takze przy wyjatku.
- c) Kazde cialo w try/catch z licznikiem potkniec (pierwszy wyjatek do pliku jak :333-339). Nigdy nie gasic okna.
- d) Bramka: `On && Live` (Live :164); On = wylacznik && LogEnabled (wzor GoodsLedger.cs:174-177).
- e) Tylko pola long / int, bez alokacji, LINQ i petli po swiecie. Odczyt samych posiadaczy tego wywolania, np. notabl + jego OwnedCaravans, O(1).
- f) Licznik wywolan okna w linii, jak "(N tickow osad)" :781. Zero wywolan = latka nie wpieta albo metoda wpleciona przez JIT (male prywatne metody bez petli, np. BalanceGoldAndPowerOfNotable - pewniej latac wolajace DailyTickHero).
- g) Koszt okien w linii: Stopwatch (GoodsLedger.cs:231, 244, 970).
- h) Liczniki zerowac w ClearDay (:143-162), stan miedzy dobami w Reset (:135-141). Reset nowej klasy dopisac w konstruktorze :389.
- i) Linia: metoda XLine(day) na StringBuilder (wzor ClanLine :707-734), wolana z MoneyLedger.Daily w galezi else (:874-889) PRZED ClearDay w finally :893. Znaczniki [P] / [R], tekst po polsku bez znakow.
  - Pozycja dotad w reszcie: dopisac do zrodel lub ujsc BalanceLine (:690-691) i usunac z opisu reszty (:699-701).
  - Pozycja dotad w "z niczego / w nicosc" albo w _clanUp / _clanDown: tylko jako "w tym", bez drugiego liczenia.
- j) Szczegoly na rod: prefiks linii + wpis w Log.TopicOf (:96-112). CSV przez Log.Csv (wzor PeopleLedger.cs:335-372).
- k) Tanie dopiski do istniejacego okna rodu (:303-330): przed i po odczyt Kingdom.KingdomBudgetWallet, DebtToKingdom i Leader.Gold, O(1). Daje "korona z rozliczen netto (podatek BK - zapomoga)", "splata dlugu wobec korony (w nicosc)" i "nowy dlug". Dokladny podzial zapomogi - okno na AddIncomeFromKingdomBudget w kampanii.

6. D (dochod staly, srednia 28 dob) - gdzie liczyc i zapisac
- Miejsce: nowa klasa statyczna, np. ClanIncomeBook. Daily() raz na dobe w OnDailyTick po IronBank.Daily (:1259), przed MoneyLedger.Daily (:1264), we wlasnym try. Wtedy znane sa juz dzisiejsze renty i zwroty, a linia wejdzie do bloku ksiegi.
- Wplyw dnia rodu:
  - (a) CalculateClanIncome(c,false,false,false), ta sama definicja co KingdomTreasury.cs:60, BuildFunding.cs:154 i IronBank.cs:110;
  - (b) + PopulationLaw.RentToday (:144);
  - (c) + zwrot korony: nowy licznik przy KingdomTreasury.cs:183;
  - (d) + mennica i monopole krola (:271, :279);
  - (e) + zdarzenia z OnGoldTraded (:254-294): odbiorca z rodu, placacy spoza rodu, poza oknem rodu i poza blokiem dobowym (jency, oblezenia, turnieje, skup lupu, okupy);
  - (f) + trzecia lorda (MenPurse.cs:348, :76 - ChangeHeroGold bez zdarzenia).
- Bez pozyczek: IronBank.Lend :173 nie daje zdarzenia, wiec wypada sam. Bez saldo rodu z okna rozliczenia - dochod modelu juz jest w (a).
- G = kiesa glowy + doroslych zywych czlonkow, w tym samym przebiegu.
- D = suma pierscienia 28 dob / 28. Dopoki pierscien nie jest pelny, brakujace doby licza sie jako G/60 (K14).
- Zold faktyczny na rod: nowy licznik w SoldierPay.Route (:318, :333). _paidToday kasuje TakePaid i dziala tylko przy zwrocie.
- Zapis: SaveText.Sync(dataStore, "arm_clanincome", ref s) we wlasnym try (wzor :396-402).
  - Klucz Clan.StringId; obiekty Clan wyszukac dopiero w Daily, nie w Import (lekcja 161).
  - Rozmiar ok. 400 rodow x 28 liczb = 60-80 KB; SaveText tnie to na kawalki po 8000 znakow.
- Wyjscie:
  - linia "Budzet rodow (na sucho): dzien N" w glownym logu;
  - CSV na rod na dobe w Logs/<sesja>/ z nazwa i StringId rodu oraz numerem dnia jak w economy-*.csv, zeby dalo sie je laczyc.
- Pozniejsi uzytkownicy D (paczki 166 / 168): BuildFunding :154-156, KingdomTreasury :59-62, IronBank :109-113.
- BK "Courtiers of ...": nasz kod ich nie rozpoznaje (grep: 0 trafien) - trzeba ustalic regule.
- "Dlugi (na sucho)" wymaga metody do odczytu _debts w IronBank.

7. MCM
- Dopisac `public bool CirculationLedgerEnabled = true; // ...log only, changes nothing in the game` po Settings.cs:411. Obok sa juz inne ksiegi: FinanceLedgerEnabled :408, GoodsLedgerEnabled :411, grupa "Army purchases" :399.
- Opis po angielsku, w tej samej linii (regex gen_mcm.py).
- Uruchomic `python tools/gen_mcm.py` z korzenia repo (nie python3). Generator przepisuje tez RC i GT. Na kopii sprawdzilem, ze dzis wszystkie trzy wychodza bez roznic; Armoury 680 -> 681.
- Wartosci trafiaja do gry przez McmSettings.Apply: co godzine (:522-523), w OnSessionLaunched (:985) i w OnGameStart (SubModuleMain.cs:205).
- Starych pomiarow MoneyLedger nie podpinac pod nowy wylacznik (regresja).

## files

[
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/MoneyLedger.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/ArmouryBehavior.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/SubModuleMain.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/KingdomTreasury.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/SoldierPay.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/MenPurse.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/IronBank.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/PeopleLedger.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/KingdomLedger.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/LevyGold.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/PopulationLaw.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/Settings.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/McmSettings.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/tools/gen_mcm.py",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/Log.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/SaveText.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/GoodsLedger.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/SellByCondition.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/MountedWage.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/Armoury/src/BuildFunding.cs",
 "C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/obieg169/repo/CrashScribe/src/EconomyAudit.cs",
 "C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/STAN-PRAC.md",
 "C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md"
]

