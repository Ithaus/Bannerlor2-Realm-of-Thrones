# Reguly krain i dlugu - projekt gotowy do zakodowania (noc 05/06.10.2026, noc-2, paczka "reguly", wersja 2.1 po niezaleznej recenzji)

Sam projekt. Niczego nie zbudowano, nie wgrano i nie uruchomiono w grze; repo, gra i ustawienia modow nietkniete.
Podstawa: decyzje Jeffa z `docs\STAN-PRAC.md` (sekcja "Decyzje Jeffa 05.10 - komplet"), badania (aneks demografii, audyt lordow,
sprawdzenie nieumarlych) i odczyt kodu tej nocy.

**Wersja 2 (06.10, drugi przebieg autora).** Pierwsza wersja (05.10, 23:05) zostala w calosci sprawdzona jeszcze raz w kodzie i dekompilacjach:
wszystkie odwolania plik:linia do Armoury i CrashScribe, metody ROT, gry i BK, na ktorych stoja reguly, oraz liczby z logu (skrypt recenzji
`_recenzja\spr_bitwy.py` - zgodne co do sztuki). Odwolania sie zgadzaja; **siedem rzeczy poprawiono** (lista w rozdz. 7, "Poprawione w wersji 2"),
z czego trzy zmieniaja reguly: (1) BK daje jencow z rabunku kazdemu rodowi AI, ktorego siedziba jest miasto - takze wiekszosci rodow Zelaznych Wysp
(wersja 1 twierdzila, ze wyspy ich nie dostaja); (2) zajecie kiesy dluznika do 10 dni zoldu wpychaloby go pod prog limitu zoldu BK i poczet
rozpadalby sie od razu - doszla podloga kiesy; (3) trzy latki staly na metodach, ktore moga nie zadzialac (mala metoda wklejana przez JIT,
model dezercji czynny tylko przy wlaczonym prawie dezercji, wynik modelu rajdu nadpisywany przez inny mod) - wskazane pewniejsze miejsca.

**Wersja 2.1 (06.10, niezalezna recenzja).** Recenzent sprawdzil ponownie w zrodle odwolania plik:linia (Armoury, CrashScribe, gra, ROT, BK), przeliczyl `rachunek2.py` i `bitwy.log`
na kopiach i powtorzyl probe poza gra - wyniki zgodne z dokumentem. **Poprawil siedem rzeczy** (lista z dowodami: rozdz. 7, "Poprawki recenzenta"): (1) limit zoldu BK to funkcja
ciagla kiesy (600-1 200 zl na partie juz ponizej 112 917 zl, 200-600 ponizej 37 639) - podloga 38 000 nie znosi ciecia, tylko zostawia partii ok. 600 zl zoldu dziennie;
(2) spis Strazy ksiegowal zabitych w dniu konca bitwy, a gra zdejmuje ich ze skladu w jej trakcie - bitwa trwajaca przez tick dobowy dawalaby falszywy nabor z Westeros
(to samo w rownaniu "w polu" Innych); (3) R2 wgrane bez R4 w nowej kampanii zostawia Innym 216 trupow zamiast 616 - fabula ROT nie ruszy; (4) sprzedaz warsztatu akcja
`ApplyByBankruptcy` zeruje jego kapital do 10 000 (zloto z niczego albo w nicosc) - wskazana akcja, ktora kapital zachowuje; (5) rozbici Strazy wracaja do garnizonu tylko
do wolnego miejsca; (6) `docs\STAN-PRAC.md` zapisuje watek nieumarlych "po ksiedze ludzi" - odstepstwo kolejnosci opisane jawnie, do decyzji Jeffa; (7) niewolni bez
doplywu jencow: prog spada o ok. 1 punkt rocznie, nie "na 1.5 roku".

**Oznaczenia.**
- [KOD] = sprawdzone tej nocy w kodzie albo dekompilacji (plik:linia); [LOG] = policzone tej nocy z logu sesji 15:22;
  [PROBA] = sprawdzone tej nocy proba poza gra na prawdziwych bibliotekach (`SCR\noc-2\reguly\proba`, wynik `run.log`);
  [DOK] = przyjete z dokumentu bez ponownego sprawdzania; SZACUNEK = liczba z rachunku, nie z pomiaru.
- SRC = `C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src`; CS = `...\Bannerlor2-Realm-of-Thrones\CrashScribe\src`.
- SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`.
- TW = `SCR\audyt-pieniadz\twall` (gra), ROT = `SCR\audyt-pieniadz\rotall` (ROT.dll z 16.08), BK = `SCR\audyt-pieniadz\bkall` i `SCR\ore-supply\bk`.
- LOG = `...\Modules\Armoury\Armoury-2026-10-05_15-22-29.log`; BIT = `...\Modules\Armoury\Logs\2026-10-05_15-22-29\bitwy.log` (28 dob poczatku kampanii - nie stan ustalony).
- ANEKS = `docs\DEMOGRAFIA-ANEKS-2026-10-05.md`; DOK = `docs\DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md`; AUDYT = `docs\AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md`.
- Rachunki tej paczki: `SCR\noc-2\reguly\rachunek2.py` i `rachunek2_out.txt` (pliki `rachunek-v1-nieaktualny*` to pierwsza proba z bledami - nie uzywac).
- Rok gry = 364 dni. Numery linii SRC wedlug stanu repo po wpisie 102.

---

## 0. Skrot

### 0.1 Co nowego wyszlo z kodu tej nocy (ma wplyw na reguly)

1. **ROT co dobe podmienia zolnierzy na typy z szablonu rodu** [KOD: ROT `ROT.CampaignBehaviors\ROTTroopRecruiter.cs:33-49, 51-91, 105-123, 128-215, 523-557`]: kazdy
   nie-szablonowy oddzial w partii lorda AI i w garnizonie kazdej warowni AI zamienia sie na drzewo `Clan.DefaultPartyTemplate`.
   Skutek: (a) pochodzenia zolnierza nie da sie czytac z typu oddzialu - trzeba je trzymac w ksiedze; (b) "przywdzial czern"
   dzieje sie sam: kazdy przyjety do Strazy staje sie bratem w czarnym.
2. **Nocna Straz jest w tej grze rabusiem, a nie Wolni Ludzie** [LOG, BIT]: z 53 starc "Raid" w 28 dobach 10 to Straz palaca wsie
   Wolnych Ludzi (m.in. Skirling i Whitetree; 7 wygranych, 3 nierozstrzygniete; 186 zabitych obroncow). Wolni Ludzie i Zelazne Wyspy:
   0 rabunkow wsi. Zelazne Wyspy: 20 starc - 16 z bandami i 4 z karawanami (3 napady wysp na karawany Doliny, 1 napad Doliny na karawane wysp).
3. **BK daje jencow z rabunku kazdemu rodowi AI, ktorego siedziba jest miasto - z niczego** [KOD: BK `BannerKings.Behaviours.Raids\RaidCapturePolicyManager.cs:38-74`,
   `BannerKings.Managers\PolicyManager.cs:159-181, 200-211`, `BKRaidCaptureBehavior.cs:193-237, 324-399`]. Warunek `ClanRealmAllowsSlavery`: rod bez krolestwa ALBO prawo
   krolestwa "Slavery" kultury `nord` (Lorath) lub `aserai` (Dorne; `DefaultDemesneLaws.cs:138-176`) ALBO `Clan.HomeSettlement` jest MIASTEM z polityka karna "Enslavement".
   Ta polityka jest domyslna dla kazdej osady (`GeneratePolicy`, galaz domyslna `:209`), a zmienic ja moze tylko gracz w oknie BK (zadnego kodu AI, ktory ja ustawia,
   w BK ani w BKROTPatch nie ma). Siedzibe rodu wyznacza gra punktami (TW `GameComponents\DefaultSettlementValueModel.cs:56-104, 161-201`; BK tego nie zmienia): wlasna
   osada +5120, osada krolestwa +2560, miasto +1280, zamek +640. Miasto jest wiec siedziba (a) rodu, ktory ma wlasne miasto, i (b) rodu BEZ ziemi w krolestwie majacym
   miasta; zamek - tylko rodu, ktory ma sam zamek. Skutek: jencow z rabunku dostaje wiekszosc rodow AI w kazdym krolestwie. Na Zelaznych Wyspach to 6 rodow z miastem
   (Lordsport, Pyke, Blacktyde, Ten Towers, Lonely Light, Pebbleton) i rody bez ziemi (19 wedlug ANEKS 2.2); nic nie dostaja tylko 4 rody zamkowe (Hammerhorn, Orkwood,
   Old Wyk, Saltcliffe). W Nocnej Strazy: rod Castle Black (pozostale 4 rody maja zamki); u Wolnych Ludzi 3 rody z miastem i 3 bez ziemi [KOD: `ROT-Map\ModuleData\settlements.xml`;
   liczby rodow bez ziemi - DOK ANEKS 2.2]. Jency to `villager_<kultura>` dopisani do `PrisonRoster` napastnika (`:368-381`); z napadnietej wsi nie ubywa nikt.
   To drugie zrodlo ludzi z niczego obok szablonow partii. (Wersja 1 tego dokumentu twierdzila, ze wyspy jencow nie dostaja - blad.)
4. **Jeniec sprzedany w warowni znika, a zloto za niego powstaje z niczego** [KOD: TW `TaleWorlds.CampaignSystem.Actions\SellPrisonersAction.cs:10-100`
   (zloto: :83 i galaz osady :88), `...CampaignBehaviors\PartiesSellPrisonerCampaignBehavior.cs:24-39` (lord AI przy wjezdzie do warowni), `:41-87` (loch
   osady AI: 10% dziennie)]. Jedno zdarzenie dla wszystkich: `CampaignEvents.OnPrisonerSoldEvent(sprzedajacy, kupujacy, roster)` (TW `CampaignEvents.cs:957`),
   ale BEZ kwoty. Pulapka: gdy w partii jest choc jeden pojmany bohater oddawany do lochu wlasnej frakcji, akcja nie wyplaca NIC za cala partie jencow
   (znacznik `flag`, `:43-47, 60, 66`) - kwoty nie wolno wiec liczyc na nowo, trzeba ja zmierzyc (regula 2, punkt 4). Jency, ktorych nikt nie sprzedal,
   sa tez wcielani do partii lordow AI (`RecruitPrisonersCampaignBehavior.cs:49-96, 111-119`), a ROT nazajutrz zamienia ich na typ z szablonu rodu (pkt 1).
5. **Kalibracja ludnosci jest w zapisie gry** [KOD: SRC `PopulationLaw.cs:333-360`, `ArmouryBehavior.cs:417-419`]: zmiana tabeli
   (Wolni Ludzie 150 -> 300 tys.) dziala sama tylko w nowej kampanii; stary sejw wymaga jednorazowej migracji.
6. **Dochod wsi da sie zajac u zrodla w dwoch miejscach, bez zaleznosci od kolejnosci tickow** [KOD]: renta - SRC `PopulationLaw.cs:315`;
   utarg (licznik podatku pana `Village.TradeTaxAccumulated`, pole z publicznym `set` - `SCR\ore-supply\Village.cs:132`) - okno powrotu taboru do wsi, to samo,
   ktore mierzy `MoneyLedger` (SRC `MoneyLedger.cs:146-189`). Okno jest pewne z kodu gry, nie z zalozenia: `EnterSettlementAction.ApplyInternal` wola po kolei
   `OnBeforeSettlementEntered`, `OnSettlementEntered`, `OnAfterSettlementEntered` (TW `Actions\EnterSettlementAction.cs:26-28`), a caly podzial utargu robi latka BK
   na nasluchu `OnSettlementEntered` wiesniakow: podatek = utarg x 0.7 (dekret Standard; 0.5 / 0.9), polowa reszty do kiesy wsi, wlasciciele majatkow BK dostaja swoja czesc,
   reszta podatku trafia do licznika (`SCR\ore-supply\EconomyPatches.cs:1083-1118`; BK `BannerKings.Managers.Populations.Estates\EstateData.cs:80-104`;
   `BannerKings.Models.Vanilla\BKTaxModel.cs:313-326`). Pan dostaje co dobe 80% licznika pomniejszone o koszty administracji (`BKTaxModel.cs:238-311`, ustawienie
   `VillageTaxReserves` = true) w rozliczeniu rodu (`EconomyPatches.cs:402-486`), a tick rodu gra rozklada na cala dobe (`PeriodicTicker<Clan>`,
   TW `TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:120, 171`; wyplata: `ClanVariablesCampaignBehavior.cs:413-414`), wiec nie ma stalej kolejnosci
   wobec naszego ticku dobowego - dlatego zajecie "po fakcie" z kiesy byloby nieszczelne.
7. **Czesc "wyprzedazy" juz dziala**: gra sprzedaje caly nie-zywnosciowy ladunek lorda AI przy kazdym wjezdzie do miasta
   [KOD: TW `PartiesSellLootCampaignBehavior.cs:19-42`], a nadwyzki zbrojowni DTE sprzedaje nasz `MenPurse.SellAiSurplus`
   [KOD: SRC `MenPurse.cs:269-312`]. Przejscie warsztatu miedzy AI nie przenosi zlota [KOD: TW `ChangeOwnerOfWorkshopAction.cs:7-20`].
8. **Metody ROT od nieumarlych sa prywatnymi nasluchami zdarzen** [KOD: ROT `ROTOthersCampaignBehavior.cs:382-418, 857-896, 1299-1381`] - da sie je
   wylaczyc prefiksem Harmony. ROT ma suwak MCM "Necromancy Multiplier", ale zakres to 0.25-3 (`ROT\ROTSettingsMCM.cs:211-213`) - zera nie ustawi,
   a latka na maly statyczny getter nie jest pewna (wklejanie przez JIT). Nie opierac sie na tym suwaku.
9. **Straz i Wolni Ludzie sa niewyplacalni z zalozenia** [DOK ANEKS 2.2; LOG l.1776, l.1795]: Straz ma 698 zl rent dziennie przy zoldzie ok. 14.4 tys.
   (kiesa wodza 157 127 = 13 dni), Wolni Ludzie 1 620 przy ok. 11.4 tys. Drabina dlugu (regula 5) uruchomi sie u nich pierwsza - patrz pytanie 4.
10. **Generator MCM** [KOD: `tools\gen_mcm.py`]: do MCM trafiaja tylko pola `bool|int|float`; pole `string` zostaje w samym `Settings.cs`/XML.
    Suwak liczby ma zakres 0..max(1 albo 10; 4 x wartosc domyslna) - pole z domyslnym 0 nie da sie w MCM podniesc ponad 1 (float) / 10 (int).
    W projekcie zadne ustawienie nie ma domyslnego 0; procenty kod przycina do 0-100 (suwak pozwala na 4 x domyslna).
11. **Limit zoldu BK dziala wczesniej niz jakikolwiek prog Banku** [KOD: `SCR\audyt-pieniadz\bkall\BannerKings.Patches\EconomyPatches.cs:117-156`; `BannerKings.json`: `BaseWage` 1.2546]:
    BK ustawia kazdej partii rodu limit wyplat wedlug kiesy glowy rodu - funkcja ciagla, bez skoku: ponad 112 917 zl (90 000 x BaseWage) pelny limit; od 37 639
    (30 000 x BaseWage) do 112 917 liniowo 600-1 200 zl dziennie; ponizej 37 639 `200 + 400 x (kiesa / 37 639)^2`, czyli 200-600 zl (kiesa 0: 200; 10 000: 228;
    20 000: 313; 30 000: 454; 38 000: 603; 75 000: 898); partia glowy rodu x1.5; rody mniejsze: progi 12 546 / 37 639. Partii, ktorej zold przekracza limit, gra zdejmuje
    dezercja `min(20; max(1; (zold - limit) / sredni zold x 0.25))` ludzi dziennie, az zold zmiesci sie w limicie (TW `DefaultPartyDesertionModel.cs:50-76`,
    `DesertionCampaignBehavior.cs:27-49`; dla AI nasz `TierDesertionModel` wola model gry, SRC `DesertionLaw.cs:117`). Przy ok. 4.4 zl zoldu na czlowieka (Straz: 10 909 zl
    na 2 471 ludzi, ANEKS 2.2) limit 200 zl oplaca ok. 45 ludzi, 600 zl ok. 135, 1 200 zl ok. 270 (SZACUNEK). AI pozycza dopiero ponizej 10 dni zoldu partii (w wojnie 20):
    rod z zoldem partii do 3 764 zl dziennie (w wojnie do 1 882) ma wtedy kiese ponizej 37 639 i limit ponizej 600 zl na partie, wiec jego poczet topnieje sam,
    zanim cokolwiek zrobi drabina dlugu. Ile rodow siedzi w ktorym przedziale kiesy i ile partii ma zold ponad limit - pokaze linia R1 "Dlugi:".
    Dezerterzy tej drogi ida do puli wyrzutkow (zdarzenie `OnTroopsDeserted`), nie do domu.
12. **ROT ma juz gotowy wzor "Straz czegos nie robi"** [KOD: ROT `ROT.HarmonyPatches.Core\AIThinkPatch.cs:21-144`]: postfiks na `CampaignEventDispatcher.AiHourlyTick` wycina partiom
    Nocnej Strazy oblezenia (poza Fist of the First Men i Craster's Keep, i dopiero po 1.5 roku, `:96-108`). Rabunku (`AiBehavior.RaidSettlement` = 4) nie wycina.
    Wlasny model celu rejestruja dwa mody (`StrategicCampaignAI145`: `StrategicTargetScoreModel`; `NavalDLC`: `NavalTargetScoreCalculatingModel`), a BK dokleja postfiks do modelu gry
    (`VanillaModelTweakPatches.cs:1947-1948`) - dlatego hamulec "Straz nie rabuje" lepiej oprzec na tym samym miejscu co ROT niz na wyniku modelu (regula 3, H1).
13. **Partia AI nie ma osobnej listy lupu jencow** [KOD: TW `MapEvents\MapEventParty.cs:75-101`]: `RosterToReceiveLootPrisoners` partii AI to jej wlasny `PrisonRoster`,
    a `RosterToReceiveLootMembers` to jej `MemberRoster`. ROT po kazdej bitwie czysci wiec bandzie Innych CALY loch (`ROTOthersCampaignBehavior.cs:1372-1378`), a odbici
    jency pokonanego wchodza od razu do skladu bandy w swoim zywym typie (TW `MapEvent.cs:1553, 1577`). Kolejnosc rozdania lupu: polegli, rzeczy, odbici jency, okrety,
    pojmani (`MapEvent.cs:1423-1427`), a zdarzenie `MapEventEnded` pada dopiero po calym rozdaniu (`:1048-1054, 2057-2079`). W chwili `MapEventEnded` w skladzie bandy
    stoja wiec zywi ludzie (odbici jency), a w jej lochu swiezo pojmani; ROT zamieni zywych na wighty dopiero w nastepnym ticku dobowym partii (pkt 1) - jest okno,
    w ktorym nasz nasluch moze ich policzyc i zdjac bez latki na model lupu.
14. **Trzy rzeczy, na ktorych latka moze nie zadzialac** [KOD]: (a) `RecruitmentCampaignBehavior.GetRecruitVolunteerFromMap` to jednolinijkowa metoda prywatna
    (TW `RecruitmentCampaignBehavior.cs:646-649`) - JIT moze ja wkleic w `HourlyTickParty` i latka Harmony nie zadziala; latac `ApplyInternal` (`:606-639`), gdzie siedzi juz
    nasz postfiks (SRC `LevyGold.cs:26-56, 89-90`); (b) nasz model dezercji `TierDesertionModel` jest dodawany tylko przy `DesertionLawEnabled` (SRC `DesertionLaw.cs:47-58`) -
    blokade dezercji wightow trzeba wpiac w `DesertionCampaignBehavior.CheckDesertionForParty`, jedyne miejsce, ktore zdejmuje dezerterow i wola zdarzenie
    (TW `DesertionCampaignBehavior.cs:27-49`); (c) `DefaultBattleRewardModel.CanTroopBeTakenPrisoner` tez jest jednolinijkowa (`:426-429`), ale wolana wirtualnie przez
    model abstrakcyjny (`MapEvent.cs:2013`) - tu latka jest bezpieczna; za to czynnym modelem lupu moze byc `NavalDLCBattleRewardModel` z wlasna wersja tej metody [PROBA],
    wiec latac trzeba kazda implementacje, nie tylko model gry. Wszystkie metody z tej listy i z list regul istnieja w jednej sygnaturze, a prefiks zwracajacy `false`
    pomija prywatny nasluch ROT wolany przez delegata [PROBA] (szczegoly: rozdz. 4.3).

### 0.2 Co mozna zrobic przed ksiega ludzi, a co musi czekac

Wpis 102 (`PeopleLedger`) to tylko log: nie ma stanu w zapisie, nie zna pochodzenia zolnierza, a 1 czlowiek puli wyrzutkow to nadal 0.5 hearth
(ok. 96 ludzi w skali 52.6 mln). "Krok N" = krok demografii z DOK rozdz. 5.2 w kolejnosci z ANEKS 6.2.

| Regula | Mozna juz teraz (wlasny stan, bez ksiegi) | Czeka na krok 2-3 (jednostka 1/k, przyrost) | Czeka na krok 4 (spustoszenie) | Czeka na krok 5 (pochodzenie, polegli) | Czeka na krok 7 (pobor z ksiegi) |
|---|---|---|---|---|---|
| 1. Nocna Straz | spis i log "Straz:"; nabor z pul wyrzutkow; dezerter do puli Polnocy; rozbici wracaja do zamku Strazy; Dar poza progiem (log) | zdejmowanie ludzi z wsi Westeros (czesc "z ludnosci") | - | polegly = ubytek regionu pochodzenia w ksiedze (do tego czasu liczone we wlasnym stanie Strazy) | wyjatek Daru w checi do sluzby |
| 2. Zelazne Wyspy | stan niewolnych na region, bilans otwarcia, ubytek, sprzedany jeniec = niewolny (takze jency z rabunku, ktorych BK juz dzis daje wiekszosci rodow wysp - z niczego), zaplata z kasy osady, prog w logu, licznik "jency z rabunku BK" | - | przejecie jencow z rabunku od BK (jedna regula: liczba z reguly spustoszenia, odjecie z napadnietej wsi), czynnik rak w plonie (razem z 6a) | pochodzenie jenca z bitwy | prog 20-40% w checi do sluzby |
| 3. Wolni Ludzie | tabela 300 tys. (nowa kampania albo migracja), prog 100% w logu, hamulec "Straz nie pali wsi" | - | - | - | pula = wszyscy dorosli w checi do sluzby i hamulcu naboru |
| 4. Nieumarli | wszystko poza przypisaniem cial do regionow pochodzenia: zamkniecie zrodel z niczego, pula cial, narodziny bandy z puli, wight nie dezerteruje i nie idzie do niewoli, Zew | - | rajd Innych w ludziach wsi | cialo = polegly konkretnego regionu | - |
| 5. Dlug | wszystko pieniezne: zajecie renty i utargu wsi, podloga kiesy, wierzyciele, wyprzedaz majatku; odchodzacy z braku zoldu do puli wyrzutkow zamiast w nicosc | - | - | rozpuszczenie pocztu z powrotem do wsi pochodzenia (i straze rozwiazanych karawan) - do tego czasu poczet dluznika topnieje dezercja do pul wyrzutkow (limit zoldu BK, pkt 0.1.11) | - |

### 0.3 Kolejnosc wdrazania (kazda pozycja = osobny wpis, osobny DLL, osobny test Jeffa)

Po kolejce z `docs\STAN-PRAC.md` (woz i logi, karawany, zapas startowy, mineral BK, 13 kluczy BetterEconomy, paser, zold i skarbiec).

| Nr | Wpis | Mod | Zalezy od | Regula |
|---|---|---|---|---|
| R1 | KRAINY (tylko log): progi wolnych rak wedlug krainy w "Ludzie:", linie "Straz:", "Niewolni:" (z licznikiem jencow-chlopow BK w partiach i lochach), "Umarli (ksiega):" i "Dlugi:" na sucho | Armoury | wpis 102 przetestowany | 1-5 |
| R2 | NIEUMARLI 1: koniec trupow z niczego (+100, +2, ochotnicy z mapy); wight nie dezerteruje, nie idzie do niewoli ani do puli wyrzutkow. Szablon 53 trupow przy narodzinach bandy zostaje do R4; +100 ROT przepuszczone w pierwszych dwoch dobach kampanii (bilans otwarcia - 4.2 punkt 1). Proba latek poza gra juz wykonana (4.3) | Armoury | R1 | 4 |
| R3 | ZEW: limit hordy liczony z modelu i przed transferem | CrashScribe | - (moze isc razem z R2 jako drugi DLL, ale osobny wpis) | 4 |
| R4 | NIEUMARLI 2: pula cial za Murem i przy Murze, narodziny bandy i podnoszenie tylko z puli | Armoury | R2 | 4 |
| R5 | BANK 1 (poprawki): zold garnizonow w progu, renty w limicie, blad wiarygodnosci, straz przed dlugiem wobec korony | Armoury | - | 5 |
| R5b | ODCHODZACY Z BRAKU ZOLDU: ludzie zdejmowani przez `WarLedger` ida do puli wyrzutkow regionu zamiast znikac | Armoury | - (osobny wpis: to ludzie, nie pieniadz) | 5, ksiega ludzi |
| R6 | DLUG 1: zajecie dochodu wsi zamiast bankructwa (renta + utarg u zrodla, kiesa tylko ponad podloge, odsetki zamrozone, koniec 25% kiesy dziennie) | Armoury | R5, R5b; najlepiej po 13 kluczach BEE i paczce "zold" | 5 |
| R7 | DLUG 2: wierzyciele - skarbiec krolestwa i bogaty rod | Armoury | R6, paczka "zold i skarbiec" | 5 |
| R8 | DLUG 3: wyprzedaz majatku (zbrojownia, konie, karawany, warsztaty) | Armoury | R6 | 5 |
| R9 | STRAZ 1: nabor z pul wyrzutkow Westeros, dezerter do puli Polnocy, rozbici wracaja do zamku | Armoury | R1, paser | 1 |
| R10 | STRAZ NIE RABUJE: partie Strazy nie pala wsi (hamulec H1; to samo miejsce, w ktorym ROT wycina Strazy oblezenia) | Armoury | R1 | 1, 3 |
| R11 | WOLNI LUDZIE: tabela 300 tys. z migracja zapisu | Armoury | R1 | 3 |
| R12 | NIEWOLNI 1: stan niewolnych, sprzedany jeniec = niewolny, zaplata z kasy osady (kwota zmierzona, nie liczona na nowo) | Armoury | R1 | 2 |
| R13 | (po krokach 2-5 demografii, kazda czesc osobnym wpisem) STRAZ 2: czesc "z ludnosci" i polegli w ksiedze; NIEWOLNI 2: jency z rabunku przejeci od BK i czynnik rak; DLUG 4: rozpuszczenie pocztu; NIEUMARLI 3: cialo = polegly regionu | Armoury | kroki 2, 3, 4 + 6a, 5 | 1, 2, 4, 5 |

Uzasadnienie kolejnosci: R1 daje pomiar bez zmiany gry (mozna wgrac jak wpis 102). Nieumarli (R2-R4) i dlug (R5-R8) nie zaleza od ksiegi
ludzi i zamykaja najwieksze "z niczego" tych dwoch tematow. R9-R12 ruszaja pule wyrzutkow, AI Strazy i tabele - po pierwszym odczycie nowych logow.

**Odstepstwo do zatwierdzenia przez Jeffa.** `docs\STAN-PRAC.md` (l.112) zapisuje zmiany u nieumarlych jako "osobny watek, po ksiedze ludzi". R2 i R3 (zamkniecie zrodel
z niczego, Zew) ksiegi nie potrzebuja; R4 (pula cial) prowadzi wlasny stan i do kroku 5 demografii nie odejmuje cial zadnemu regionowi. Jesli zapis STAN-PRAC ma byc trzymany
doslownie - R4 przesunac do R13 (za krok 5), R2 i R3 zostawic. R2 i R4 testowac razem albo R2 w wersji z bilansem otwarcia (4.2 punkt 1): samo R2 w nowej kampanii
dawaloby Innym 4 x 54 = 216 trupow zamiast 616 i Zew (cel 520) oraz oblezenia ROT (prog 500) nie mialyby z czego ruszyc.

---

## 1. NOCNA STRAZ

**Decyzja Jeffa:** rekrut zawsze z krolestw Westeros - najpierw z pul wyrzutkow ("przywdzial czern"), potem z ludnosci proporcjonalnie do
liczby ludzi regionu; sluzba dozywotnia; polegly = ubytek regionu pochodzenia; dezerter do puli wyrzutkow Polnocy; ludzie Daru (20 tys.)
licza sie tylko do plonu.

### 1.1 Skad Straz bierze ludzi dzis [KOD]

| Kanal | Gdzie w kodzie | Skad ludzie | Zdarzenie gry |
|---|---|---|---|
| Nowa partia rodu | TW `Party\MobileParty.cs:2639-2651` -> `GameComponents\DefaultPartySizeLimitModel.cs:427-464`; szablon `kingdom_hero_party_nightswatch_template` = 54 ludzi (`ROT-Content\ModuleData\partyTemplatesROT.xml:4-17`) | z niczego | brak; nasz `OutlawLaw.RosterPostfix` (SRC `OutlawLaw.cs:546-567`) widzi kazda partie z szablonu, dzis wychodzi na `!IsOutlawParty` |
| Ochotnicy u notabli (11 wsi Daru, Castle Black) | TW `CampaignBehaviors\RecruitmentCampaignBehavior.cs:504-561` -> `ApplyInternal :606-639` (VolunteerFromIndividual) | z niczego (BK zdejmuje wlasna klase w innej skali) | `OnTroopRecruited` |
| Ochotnicy we wsiach innych krolestw, gdy partia tam wjedzie | tamze `:563-604` (wies kazdej frakcji; miasto tylko niewrogie) | jw. | jw. |
| Najemnicy z karczmy Castle Black | tamze `:414-502` (MercenaryFromTavern); paczka co 2 dni z niczego `:341-371` | z niczego | `OnTroopRecruited` bez notabla |
| Garnizon: przyrost bazowy | TW `GarrisonRecruitmentCampaignBehavior.cs:97-108` (`MapFaction.BasicTroop`) | z niczego | BRAK zdarzenia |
| Garnizon: auto-werbunek od notabli | tamze `:76-95` | ochotnik notabla | BRAK zdarzenia |
| Jency wcieleni do partii | TW `RecruitPrisonersCampaignBehavior.cs:49-96` (codziennie kazda partia lorda AI, gdy model pozwala), `:111-119` | jeniec z lochu partii (np. dziki pojmany w bitwie) | `OnTroopRecruited(wodz, null, null, typ, n)` - ta sama postac co ochotnik "z mapy" |
| Jency-chlopi z rabunku (BK) | BK `BKRaidCaptureBehavior.cs:324-399`; tylko gdy rabunkiem dowodzi rod Castle Black (jedyny rod Strazy z miastem jako siedziba, pkt 0.1.3) | z niczego: `villager_freefolk` w lochu partii po wygranym rabunku wsi Wolnych Ludzi; potem sprzedaz w warowni (zloto z niczego) albo wcielenie (wiersz wyzej) | `RaidCompletedEvent` (nasluch BK) |
| "Zeslanie na Mur" ROT | ROT `ROTSendToWallBehavior.cs:45-88, 104-113` [DOK ANEKS 2.2] | tylko gracz-krol i pojmany lord | - |

- Wspolny punkt werbunku partii AI: `RecruitmentCampaignBehavior.ApplyInternal(partia, osada, notabl, oddzial, liczba, bitCode, detail)` - tam siedzi juz nasz
  `LevyGold.ApplyInternalPostfix` (SRC `LevyGold.cs:26-56`). Garnizon i szablon ida obok niego.
- ROT zamienia kazdego przyjetego na typ Strazy najpozniej w nastepnym ticku dobowym (pkt 0.1.1).
- Wojna Straz - Wolni Ludzie jest wymuszana co dobe, bez daty konca [KOD: ROT `ROTStorylineWars.cs:94` (start 0, koniec 0), `:234-257`; `ROTStorylineWar.cs:63-91`].
- Ile daje ktory kanal - nieznane; zmierzy to wpis 102 (kolumna `zwerbowani_dzis` pliku `ludzie-regiony.csv` dla 5 regionow Strazy i pozycja "reszta" w bilansie partii rodow).

**Wniosek: przechwytywac stanem, nie hakami na kanaly.** Dwa kanaly garnizonu nie maja zdarzenia, a ROT miesza typy. Jeden spis dobowy
Strazy (ludzie w partiach i garnizonach rodow krolestwa `nightswatch` + bracia w cudzej niewoli) wylapie kazde zrodlo, takze przyszle.

### 1.2 Liczby

| Pozycja | Wartosc | Skad |
|---|---|---|
| Wojsko Strazy | 3 376 (partie 2 471 + garnizony 905) | ANEKS tab. 2.1 |
| Zabici zolnierze | 24.9 dziennie (srednia 28 dob) .. 44.2 (tygodnie 3-4) = 9 074 .. 16 089 rocznie = 269-477% stanu | ANEKS 2.4 |
| Pula wyrzutkow swiata, doba 28 | 568 (zolnierzy 434, prostych 133) | LOG l.1814 |
| Doplyw do pul | 72.4 dziennie nowych (dezercja 44.7, rozbitkowie 11.5, bieda 15.4, rabunki 0.8) + 28.1 z rozwiazanych band | ANEKS 4.1, 4.4 |
| Odplyw z pul | 88.0 dziennie do band + 3.0 powrot do wsi | ANEKS 4.4 |
| Udzial Westeros | ok. 60%: 135 z 227 regionow, 60.2% hearth, 59.7% wojska rodow -> pula ok. 340, doplyw ok. 60 dziennie | [LOG] z `SCR\aneks-male-krainy\m1.json`; SZACUNEK - log nie dzieli puli na regiony |
| Westeros (10 kultur tabeli) | 27.45 mln ludzi, 7.41 mln mezczyzn 16-60; przyrost 0.5% = ok. 37 tys. mezczyzn rocznie | SRC `PopulationLaw.cs:39-48` |

**Potrzeba wobec pul (stan ustalony, SZACUNEK; X = jaka czesc puli regionu moze jednego dnia przywdziac czern):**

| X dziennie | Straz dostaje z pul | Pokrycie potrzeby niskiej / wysokiej | Skutek uboczny: bandom i powrotom w Westeros zostaje |
|---|---|---|---|
| 2% | 7.9 dziennie = 2 862 rocznie | 32% / 18% | 52.4 dziennie (-13%) |
| **5% (domyslnie)** | **16.8 dziennie = 6 120 rocznie** | **67% / 38%** | **43.5 dziennie (-28%)** |
| 10% | 27.1 dziennie = 9 864 rocznie | 100% / 61% | 33.2 dziennie (-45%) |
| 25% | 42.8 dziennie = 15 585 rocznie | 100% / 97% | 17.5 dziennie (-71%) |
| 100% | 60.3 dziennie = 21 949 rocznie | 100% / 100% | 0 (bandy w Westeros gasna) |

**Reszta z ludnosci Westeros (przy X = 5%):** potrzeba niska - 2 954 rocznie = 0.040% mezczyzn = 8% rocznego przyrostu mezczyzn;
potrzeba wysoka - 9 969 rocznie = 0.135% mezczyzn = 27% przyrostu. Bez pul (calosc z ludnosci): 0.12-0.22% mezczyzn = 24-43% przyrostu
(zgodne z ANEKS 2.4 [KRYT]). Rozklad na 1000 rekrutow z ludnosci: Reach 291, Westerlands 146, Dorzecze 128, Polnoc 109, Dolina 109,
Krainy Burzy 91, Dorne 55, Smocza Skala 31, Ziemie Korony 22, Zelazne Wyspy 18. W hearth to pomijalne: przy 10 000 ludzi rocznie
kraina traci 3.4-8.0 punktu hearth na rok (np. Polnoc 8.0 z 20 833).
Dla porownania kanon: wojsko 1 000 = potrzeba 2 688-4 766 rocznie przy tym samym tempie strat.

**Wniosek liczbowy:** pule same nie utrzymaja Strazy 3 376 przy dzisiejszych stratach (potrzeba 24.9-44.2 dziennie to 41-73% calego doplywu pul Westeros, ok. 60 dziennie);
ludnosc Westeros utrzyma ja bez trudu. O skali decyduje tempo strat (wojna stala z Wolnymi Ludzmi), nie zrodlo.

### 1.3 Regula

**Kto jest Straza:** partie lordow i garnizony rodow, ktorych krolestwo ma `StringId` z listy `WatchKingdoms` ("nightswatch" - `ROT_spkingdoms.xml`; dzis 5 rodow:
Castle Black, Shadow Tower, Eastwatch, Nightfort, The Wall), bez rodu gracza i bez rodow w sluzbie najemnej (`Clan.IsUnderMercenaryService`).
**Zrodlo:** regiony (miasto albo zamek + wsie, geografia `OutlawLaw`), ktorych warownia ma kulture z listy `WatchSourceCultures`
(battania, river, reach, aserai, vlandia, vale, stormlands, sturgia, crownlands, dragonstone).

Raz na dobe (`WatchLaw.Daily`):

1. **Spis.** `S = suma ludzi (bez bohaterow) w MemberRoster partii i garnizonow Strazy + bracia w niewoli`.
   Bracia w niewoli = oddzialy kultury `nightswatch` w `PrisonRoster` partii i w lochach (`Settlement.Party`) tylko tych stron, ktore sa w wojnie z krolestwem Strazy.
   Filtr wojny jest potrzebny: dezerter Strazy siedzi w puli Polnocy w swoim typie, a zlapany pozniej jako bandyta trafia do lochu lorda Polnocy -
   bez filtra wygladalby na nowego brata i sciagal ludzi z Westeros za nic.
   **Bitwa w toku.** Gra zdejmuje zabitych i rozbitych ze skladu partii juz w trakcie bitwy (TW `MapEvents\MapEventParty.cs:298-307, 316-323`), a `MapEventEnded` pada dopiero
   na jej koncu; bitwy AI licza sie rundami przez godziny, oblezenia i rabunki przez doby. Bez poprawki bitwa trwajaca przez tick dobowy dalaby jednego dnia "ubytek bez zdarzenia",
   a nazajutrz falszywy nabor `N > 0` - Westeros zaplaciloby za tych samych ludzi drugi raz. Dlatego partiom i garnizonom Strazy, ktore w chwili spisu sa w starciu
   (`Party.MapEventSide != null`), DOLICZA sie do `S` niebohaterow z `DiedInBattle + RoutedInBattle` ich `MapEventParty` (wlasciwosci publiczne, `:65-69`): straty niedokonczonej
   bitwy stoja w spisie do jej konca, a po `MapEventEnded` wchodza do `K` i `U` i w tym samym spisie schodza z `S`.
2. **Ubytki dnia ze zdarzen:** `K` zabici (`MapEventEnded`: `DiedInBattle` partii Strazy), `D` dezerterzy (`OnTroopsDeserted`),
   `Z` zmarli w zarazie obozowej (SRC `CampFever.cs:184`) i odeszli z braku zoldu (SRC `WarLedger.cs:113-139`), `J` bracia sprzedani z niewoli (`OnPrisonerSold`),
   `U` rozbici, ktorzy nie wrocili: do R9 wszyscy z `RoutedInBattle` (dzis polowa idzie do puli regionu bitwy, polowa znika); od R9 tylko czesc
   `OutlawRoutedShare`, bo druga polowa wraca do garnizonu (punkt 9) i stanu nie zmienia.
3. **Nabor dnia:** `N = S_dzis - S_wczoraj + K + D + Z + J + U`. Gdy `N > 0` - tylu ludzi trzeba pokryc. Gdy `N < 0` - ubytek bez zdarzenia
   (glod, wyniszczenie oblezenia, kasowanie rozbitych): ksiegowany jak polegli, wypisany osobno. Znane kanaly bez zdarzenia: po przegranej bitwie gra zdejmuje caly
   pozostaly sklad pokonanej partii, a do niewoli trafia 100% rannych i tylko 25% zdrowych (TW `MapEvent.cs:2013-2030`, `DefaultBattleRewardModel.cs:231-255`) - reszta znika;
   jency Strazy w lochu bandy Innych kasuje ROT po kazdej bitwie (pkt 0.1.13); jency u band przechodza do bandy (SRC `OutlawLaw.cs:470-485`).
4. **Najpierw pule ("przywdzial czern").** Budzet regionu r: `B_r = Pula_r x WatchPoolSharePercent / 100` (bez kluczy oddzialow kultury Strazy - dezerter
   nie wraca na Mur). Gdy `suma B >= N`, kazdy region oddaje `B_r x N / suma B`; inaczej caly budzet. W regionie najpierw zolnierze (klucze oddzialow),
   potem prosci ("~"). Ludzie znikaja z puli - sa juz w czarnym.
5. **Reszta z ludnosci.** `R = N - z_pul`; region r daje `R x Ludzie_r / suma Ludzi` (`PopulationLaw.PeopleOf` warowni i jej wsi).
   Do krokow 2-3 demografii to tylko zapis "nalezne z ludnosci" (dlug ksiegi); po nich: hearth wsi regionu maleje o `ludzie / k` kultury,
   rozlozone na wsie proporcjonalnie do ich ludzi, sumy w `double`, zapis do hearth raz na dobe.
6. **Pochodzenie.** Slownik `Pochodzenie[region] += wziete z regionu` (jeden na cala Straz - bracia mieszaja sie miedzy zamkami, a ROT miesza typy).
7. **Polegly.** `K + Z + ubytek bez zdarzenia` schodzi z `Pochodzenie` proporcjonalnie (ulamki w `double`) i trafia do `Polegli[region]` - trwale.
8. **Dezerter (`D` i `U`).** Schodzi z `Pochodzenie` proporcjonalnie; czlowiek idzie do puli wyrzutkow najblizszej warowni kultury z `WatchDeserterCultures`
   ("battania"), w swoim typie i sprzecie. To przeniesienie miedzy regionami, nie strata swiata.
9. **Rozbici.** Dzis polowa rozbitych idzie do puli regionu bitwy, a druga znika (SRC `OutlawLaw.cs:283-302`, `OutlawRoutedShare` 0.5). Dla Strazy:
   pierwsza polowa do puli Polnocy (jak dezerterzy), druga wraca do garnizonu najblizszej warowni Strazy (sluzba dozywotnia - nie ma "do domu").
   Tylko do wolnego miejsca garnizonu (`Party.PartySizeLimit`): ponad limit gra zdejmuje dezercja 25% nadwyzki dziennie (TW `DefaultPartyDesertionModel.cs:54, 62-65`;
   garnizony tez - `DesertionCampaignBehavior.cs:21`), wiec nadwyzka idzie do kolejnej najblizszej warowni Strazy, a gdy miejsca nie ma nigdzie - do puli Polnocy jak pierwsza polowa.
10. **Sluzba dozywotnia.** Straz jest wylaczona z przyszlego rozpuszczania pocztu w pokoju i z konta "weterani" (ANEKS 5.2).
11. **Dar tylko do plonu.** Regiony kultury `nightswatch`: bez progu wolnych rak (nie licza sie do "regionow ponad prog"), werbunek u ich notabli nie
    zdejmuje ludzi Daru (zrodlem jest Westeros), plon i renta Daru licza sie normalnie od jego 20 tys. ludzi. Naplyw biedy do puli z wsi Daru zostaje
    (to nie nabor Strazy).

**Bilans otwarcia:** stan pierwszego spisu rozlozony na regiony Westeros wedlug ludnosci, bez zdejmowania ludzi ("dorobek stuleci").

**Etapy:** W0 = punkty 1-3 i 6-7 liczone na sucho, tylko log (R1). W1 = punkty 4, 8, 9 (R9). W2 = punkt 5 naprawde + polegli w ksiedze regionow (R13).

### 1.4 Miejsca w kodzie

- NOWY `SRC\WatchLaw.cs` (statyczna klasa: `Reset`, `Export`/`Import`, `Daily`, `OnMapEventEnded`, `OnDeserted`, `NoteLoss`, `IsWatch(MobileParty)`, `IsWatchCulture(string)`).
- SRC `ArmouryBehavior.cs:389` - `WatchLaw.Reset()` w konstruktorze; `:417-423` - wzor zapisu, nowy klucz `arm_watch`; `:512` - obok `PeopleLedger.OnMapEventEnded` nasluch
  `WatchLaw.OnMapEventEnded`; `:1185` - `WatchLaw.Daily()` w osobnym try TUZ PRZED `OutlawLaw.Daily()` (Straz bierze z pul przed bandami).
- SRC `OutlawLaw.cs`: nowy akcesor `internal static float TakeForWatch(Settlement region, float men, Func<string, bool> skipKey, out float soldiers)` obok `:139-142`;
  `OnTroopsDeserted :272-281` - galaz: partia Strazy -> region = najblizsza warownia kultury z listy (nowa `NearestNodeOf(pos, kultury)`), potem `WatchLaw.OnDeserted`;
  `OnMapEventEnded :283-302` - galaz dla partii Strazy (pkt 9; druga polowa: `garnizon.MemberRoster.AddToCounts`).
- SRC `PeopleLedger.cs:32` i `:251-252` - prog przez funkcje (rozdz. 6.2), regiony Strazy poza licznikiem "ponad prog".
- SRC `CampFever.cs:184` i `WarLedger.cs:113-139` - po jednym wywolaniu-liczniku `WatchLaw.NoteLoss(mp, n)` (za istniejaca zmiana rostera, w try). Gdy wczesniej wejdzie
  poprawka R5b (odchodzacy z braku zoldu ida do puli przez `OutlawLaw.OnTroopsDeserted`), odejscia z `WarLedger` staja sie dezercja `D` i licznik `NoteLoss` zostaje tylko w `CampFever`.
- W2: `PopulationLaw` dostaje z kroku 2 demografii `internal static float HearthPerMan(Village)` (tabela `_k`, SRC `PopulationLaw.cs:72`).

### 1.5 Stan do zapisu (`arm_watch`, napis jak pozostale klucze)

`v1|last=<S wczoraj>|seeded=1|orig=<idRegionu>:<ludzie>,...|fallen=<idRegionu>:<ludzie>,...|owed=<idRegionu>:<ludzie>,...|sum=<zPul>;<zLudnosci>;<polegli>;<dezerterzy>`
(liczby `double`, kultura niezmienna; pusty napis = pierwsza doba: bilans otwarcia). Dopisac tez liczniki dnia `day=<K>;<D>;<Z>;<J>;<U>` - inaczej zapis i wczytanie
w srodku doby zamienia zdarzenia tej doby w "bez zdarzenia" (dezerter zostalby zaksiegowany jak polegly).

### 1.6 Log i test

```
Straz: dzien N | stan 3 376 (partie 2 471, garnizony 905), w niewoli 12 | dzis przybylo 31: z pul wyrzutkow 17 (9 regionow; Winterfell 4, ...), z ludnosci 14 (reach 4, vlandia 2, ...)
 | ubylo: polegli 25, dezercja 3 (do puli: Last Hearth), zaraza i zold 0, sprzedani z niewoli 0, bez zdarzenia 0 | od poczatku: przybylo 812 (z pul 430, z ludnosci 382), polegli 698
 | pochodzenie: reach 29%, vlandia 15%, ... | pula Westeros po naborze 310 (limit 5% puli regionu dziennie) | w starciach w toku: 3 partie, 14 poleglych i rozbitych doliczonych do stanu
 | Dar: 20 000 ludzi - tylko plon.
```
Do W2 w linii stoi "z ludnosci (nalezne, jeszcze nie zdjete)". Prefiks "Straz:" zostaje w glownym logu (nie ma go w `Log.TopicOf`, SRC `Log.cs:96-111`).

Po czym poznac, ze dziala:
1. `stan dzis = stan wczoraj + przybylo - ubylo` co do sztuki; "bez zdarzenia" male i tylko w dobach z przegrana bitwa albo po starciu z Innymi (znane kanaly - punkt 3 reguly).
   Duze albo na przemian ujemne i dodatnie w kolejnych dobach = spis nie dolicza strat bitwy w toku (punkt 1 reguly) albo nieznany kanal ubytku.
2. "przybylo" srednio rowne "polegli + dezercja" (25-44 dziennie), gdy stan stoi.
3. Suma `pochodzenie` = stan + w niewoli (rownanie stanu Strazy).
4. W "Wyrzutki:" po W1: pula Westeros nizsza, "bandy nowe" w Westeros o ok. 28% mniej przy X = 5% (porownac z sesja bazowa).
5. Dezercja Strazy pojawia sie w puli regionu Polnocy (plik `ludzie-regiony.csv`, kolumna `wyrzutki`), nie regionu Wolnych Ludzi.

### 1.7 Ryzyko i kolizje

- Mniej wyrzutkow w Westeros = mniej band (zamierzone przez "przywdzial czern", ale zmienia balans drog; paczka "paser" tez dotyka `OutlawLaw` - scalac recznie).
- Pula trzyma dzis ludzi bez pochodzenia; po kroku 5 (ANEKS 4.5) `TakeForWatch` ma zwracac sklad pochodzenia zamiast regionu puli.
- Spis liczy caly roster partii Strazy - takze swiezych rekrutow obcej kultury sprzed podmiany ROT (dobrze: sa juz w Strazy). Braci w niewoli rozpoznaje po kulturze oddzialu;
  gdyby ROT przestal podmieniac typy, liczba "w niewoli" bylaby zanizona (widac w "bez zdarzenia").
- Rody najemne w sluzbie Strazy sa poza spisem (`IsUnderMercenaryService`) - inaczej najemnik stalby sie bratem, a jego odejscie "poleglymi".
- Jeniec wcielony do partii Strazy (dziki z bitwy albo chlop z rabunku BK - tabela 1.1) wyjdzie w spisie jako nabor i zostanie pokryty z Westeros, choc to czlowiek zza Muru.
  Skale pokaze wpis 102 (pozycja "bez osady") i licznik R1; po R10 (Straz nie rabuje) znika zrodlo chlopow BK, zostaja jency z bitew. Gdy liczba bedzie istotna -
  wcielanie jencow przez Straz wylaczyc albo liczyc osobno jako "zza Muru" (decyzja po pomiarze).
- Dezerter Strazy zostaje w puli w swoim typie (sprzet). Filtr wojny w spisie (punkt 1) zdejmuje glowny przypadek falszywego naboru (lochy Polnocy); zostaje rzadki:
  dezerter zlapany przez strone bedaca w wojnie ze Straza. Jesli "bez zdarzenia" pokaze wiecej - liczyc niewole zdarzeniami bitwy zamiast spisem lochow.
- Nie liczyc drugi raz: `PeopleLedger` dalej liczy swoje (log); `WatchLaw` nie wola `OutlawLaw.MiseryOf` ani kalibracji `PopulationLaw` (czyta `PeopleOf` tylko przy `Calibrated`).

---

## 2. ZELAZNE WYSPY

**Decyzja Jeffa:** prog wolnych rak ok. 40% (pracuja niewolni); niewolni to jency z rajdow, przenoszeni z ksiegi napadnietego regionu do ksiegi wysp
jako rece do pracy (plon i kopalnie).

### 2.1 Skad w grze sa jency [KOD]

| Zrodlo | Kod | Ile | Co sie z nimi dzieje dzis |
|---|---|---|---|
| Bitwa: ranni i zdrowi pokonanej partii | TW `MapEvents\MapEvent.cs:1955-2046` (`CanTroopBeTakenPrisoner :2013`, szanse z `BattleRewardModel.GetCaptureMemberChancesForWinnerParties`) | Zelazne Wyspy w 28 dobach: 20 starc, w 18 wygranych jako napastnik 73 rannych wrogow = 2.6 dziennie = ok. 950 rocznie [LOG, BIT] - gorna granica jencow | do `PrisonRoster` zwyciezcy |
| Jency pokonanej partii | TW `MapEvent.cs:1529-1593` | - | wchodza wprost do skladu zwyciezcy |
| Rabunek wsi (BK) | BK `BKRaidCaptureBehavior.cs:193-237, 324-399`; `BKRaidCaptureModel.cs:24-57`: min(10% chlopow BK x 0.4; max(5; 0.5 x (N - 5))), sufit 150; `EnableRaidCaptureSystem` = true, `RaidCaptureFraction` = 0.4 (`BannerKings.json`). Tylko rabunek wygrany przez napastnika, wodz-bohater, rod z "prawem" (pkt 0.1.3) | na wyspach prawo ma 6 rodow z miastem i rody bez ziemi, 4 rody zamkowe nie; w 28 dobach 0 rabunkow wysp [LOG], wiec 0 jencow - ale tylko dlatego, ze nie rabowaly | `villager_<kultura>` w `PrisonRoster` napastnika (`:368-381`; kultura losowana z kultur ludnosci wsi wedlug BK z pominieciem kultury wodza, `BKRaidCaptureModel.cs:97-134`; brak takiego oddzialu - kultura wsi, potem `looter`); z wsi nic nie ubywa (ludzie z niczego); wodz kultury innej niz kultura krolestwa dostaje 20% lupu w zlocie z niczego (`:384-393`, `ForeignMercSkim` 0.2) |
| Sprzedaz w warowni | TW `PartiesSellPrisonerCampaignBehavior.cs:24-39`; `SellPrisonersAction.cs:10-100` | kazdy wjazd lorda AI do niewrogiej warowni | jeniec znika; zloto z niczego dla sprzedajacego (`:83`), chyba ze w partii byl bohater oddany do lochu wlasnej frakcji - wtedy nic (pkt 0.1.4); BK dopisuje sprzedanych do wlasnej klasy "Slaves" osady przy polityce Enslavement, czyli domyslnie zawsze (BK `BannerKings.Patches\SettlementPatches.cs:24-41, 79-93`; prefiksy przepuszczaja akcje gry) |
| Wcielenie do partii | TW `RecruitPrisonersCampaignBehavior.cs:49-96, 111-119` | nieznane (wpis 102: pozycja "bez osady") | jeniec staje sie zolnierzem zwyciezcy; ROT nazajutrz zamienia go na typ z szablonu rodu |
| Loch osady AI | TW `PartiesSellPrisonerCampaignBehavior.cs:41-87` | 10% jencow lochu dziennie | znikaja; zloto z niczego do kasy osady |

`RealisticCaptivity` to niewola GRACZA i okup za lordow (`FairRansom.cs`, `Work.cs`) - nie tworzy jencow szeregowych. Jego latka na
`SellPrisonersAction.ApplyInternal` (prefiks, postfiks i finalizator, `RealisticCaptivity\src\FairRansom.cs:152-186, 218-223`) i podloga ceny jenca (`:21-55`) zostaja bez zmian.
Uwaga dla kodu: wycena jenca-bohatera w trakcie sprzedazy ma skutek uboczny (zdejmuje nadwyzke z kiesy rodu jenca, `:106-118`) - nasz kod nie moze wolac
`PrisonerRansomValue` dla bohaterow; dla szeregowych wycena jest czysta, ale u BK zalezy od stanu niewolnikow osady, ktory sama sprzedaz zmienia.

### 2.2 Liczby (SZACUNEK, `rachunek2_out.txt` rozdz. 2)

- Wyspy: 500 000 ludzi, 135 000 mezczyzn 16-60; prog 20% = 27 000, prog 40% = 54 000. Pod bronia dzis 8 078 = 6.0% mezczyzn (ANEKS tab. 2.1).
- Zeby prog wynosil 40%, potrzeba 27 000 niewolnych (jeden niewolny zwalnia jednego wolnego) = 5.4% ludnosci wysp.
- Utrzymanie tego stanu przy ubytku 5% rocznie: 1 350 jencow rocznie = 3.7 dziennie (przy 3%: 810; przy 10%: 2 700).
- Jeden rabunek (regula spustoszenia ANEKS 1.4, R = 0.5): napastnik 62 -> 31 jencow, 112 (mediana z logu) -> 56, 200 -> 98, 339 -> 136.
  Utrzymanie stanu = ok. 24 rabunki napastnika-mediany rocznie albo sami jency z bitew (ok. 950 rocznie) + 7 rabunkow.

**Co, gdy rajdow brak (start 27 000, ubytek 5% rocznie):**

| Doplyw jencow rocznie | Po 1 roku | Po 3 latach | Po 5 latach | Po 10 latach |
|---|---|---|---|---|
| 0 | 25 650 (prog 39.0%, plon -0.3%) | 23 149 (37.1%, -0.8%) | 20 892 (35.5%, -1.3%) | 16 166 (32.0%, -2.3%) |
| 949 (same bitwy, pomiar 28 dob) | 26 599 (39.7%) | 25 856 (39.2%) | 25 186 (38.7%) | 23 782 (37.6%) |
| 1 350 | 27 000 (40.0%) | 27 000 | 27 000 | 27 000 |
| 2 650 (ok. 50 rabunkow) | 28 300 (40.0%, plon +0.3%) | 30 708 (+0.8%) | 32 882 (+1.2%) | 37 433 (+2.1%) |

Start od zera przy doplywie 949 rocznie: prog 20.7% po roku, 23.2% po 5 latach, 25.6% po 10.

**Uczciwie:** przy 6% mezczyzn pod bronia prog 40% (ani 20%) nie wiaze - czynnik progu w plonie zostaje 1.0. Niewolni dzialaja dwiema drogami:
(1) jako rece: plon i kopalnie wysp -5.7% bez niewolnych, -2.8% przy polowie stanu, +5.4% przy podwojnym; (2) jako prog w checi do sluzby
(krok 7: nadwyzka rak 0.34 zamiast 0.14 - wiecej ochotnikow na wyspach). Skutek jest powolny: bez zadnego doplywu jencow prog spada o ok. 1 punkt rocznie
(40.0 -> 39.0 -> 37.1 -> 35.5 -> 32.0% po 1 / 3 / 5 / 10 latach - tabela wyzej); z samymi jencami z bitew (ok. 950 rocznie, bez rabunkow) o ok. 1 punkt na 4 lata.

### 2.3 Regula

**Kto:** regiony niewolnych = regiony, ktorych warownia ma kulture z `ThrallCultures` ("sturgia": 6 miast i 4 zamki, 28 wsi). Niewolnym zostaje jeniec sprzedany w takim regionie -
bez wzgledu na to, kto go sprzedaje. Jencow z rabunku (punkt 5) biora rody krolestw z `RaidCaptiveKingdoms` ("sturgia" - id krolestwa Zelaznych Wysp, `ROT-Content\ModuleData\spkingdoms.xslt:51`).

1. **Stan:** `T_R` = niewolni regionu R (liczba `double`). **Bilans otwarcia** (nowa kampania albo pierwsze uruchomienie w starej):
   `T_R = Ludzie_R x ThrallSeedPerThousand / 1000` (54 -> razem 27 000). To bilans otwarcia, nikomu nie odejmowany.
2. **Ubytek:** co dobe `T_R -= T_R x ThrallYearlyLossPercent / 100 / 364` (zgon; wyzwolen osobno nie liczymy).
3. **Sprzedany jeniec = niewolny.** Nasluch `OnPrisonerSoldEvent`: osada = `sprzedajacy.Settlement ?? kupujacy.Settlement` (tak samo liczy ja gra, `SellPrisonersAction.cs:12`;
   gdy obie puste - `CurrentSettlement` partii sprzedajacego); gdy jej region jest regionem niewolnych: `T_R += liczba jencow-niebohaterow`. Dotyczy lordow AI, lochu osady
   i gracza. Wighty pomijane (`Undead.Character`). Do R13 to takze jency-chlopi, ktorych BK daje rodom z miastem przy rabunku (pkt 0.1.3) - licznik osobno ("w tym chlopi z rabunku BK").
4. **Zaplata bez zlota z niczego** (`ThrallTownPaysForCaptives`). Kwote MIERZYMY, nie liczymy na nowo (pkt 0.1.4: znacznik `flag`, okup bohaterow w tej samej partii,
   cena BK zalezna od stanu po sprzedazy). Prefiks i finalizator na `SellPrisonersAction.ApplyInternal` otwieraja i zamykaja okno (`[ThreadStatic]` licznik glebokosci -
   obok istniejacej latki `FairRansom`, bez kolizji: nasze haki niczego nie zmieniaja); w oknie nasz nasluch `HeroOrPartyTradedGold` sumuje przelewy bez dawcy
   (kwota i odbiorca: bohater albo osada - `GiveGoldAction` zglasza kazdy przelew, `SCR\audyt-pieniadz\van_GiveGoldAction.cs:9-41`). Sumowac tylko przelewy, ktorych odbiorca jest
   ten, komu placi akcja (`SellPrisonersAction.cs:70-83`: wodz, wlasciciel albo glowa rodu partii sprzedajacej; `:88`: kasa osady sprzedajacej); inne przelewy bez dawcy w oknie
   (cudzy nasluch `OnPrisonerSold`) zostawic i policzyc osobno ("obce przelewy w oknie"); gdy akcja nie znalazla odbiorcy (`:83` z pustym bohaterem) - zloto nie powstalo, korekty nie ma.
   Zdarzenie `OnPrisonerSold` pada jeszcze
   w tym oknie (`SellPrisonersAction.cs:83-95`). Korekta TYLKO gdy w sprzedawanej partii jencow nie ma zadnego bohatera - wtedy jedyny przelew bez dawcy w oknie to
   zaplata za szeregowych i `cena` = zmierzona suma (zadnego ponownego liczenia). Partia z bohaterem: bez korekty, tylko licznik w logu ("sprzedaze z bohaterem: n, zloto z niczego X").
   `z_kasy = min(cena; kasa osady ponad prog)` (prog = `TownRentFloorGold` dla miasta, 0 dla zamku); `osada.ChangeGold(-z_kasy)`; reszta `cena - z_kasy` wraca od odbiorcy
   zmierzonego przelewu (`ChangeHeroGold(-reszta)`) - osada placi tyle, ile ma, sprzedajacy dostaje tyle, ile osada zaplacila. Przy sprzedazy z lochu osady
   (odbiorca = kasa tej osady, `:88`) - odjac cala `cena` z tej kasy (osada nie placi sama sobie). Gracz: sprzedaz przez ekran partii nie wyplaca nic w akcji
   (`:112-115`), wiec suma = 0 i korekty nie ma - liczymy tylko niewolnych; sprzedaz gracza u posrednika - w pierwszym wpisie tez bez korekty (znane zloto z niczego, jak poza wyspami).
5. **Jency z rabunku - jedna regula zamiast BK** (R13, po krokach 2 i 4 demografii). Prefiks na BK `BannerKings.Behaviours.Raids.BKRaidCaptureBehavior.ExecuteCapture(MobileParty, Hero, Clan,
   Village, int, bool)` (`:324`): dla rodu AI `return false` (BK nie dopisuje jencow z niczego), a nasza regula decyduje. Rod krolestwa z listy `RaidCaptiveKingdoms`
   ("sturgia"; o Dorne i Lorath - pytanie 6): `J = min(ThrallsPerRaider x N; ThrallShareOfHarried x A; ThrallCapPerRaid)`, N = ludzie strony atakujacej (BK podaje ja w argumencie),
   A = ludzie dotknieci rabunkiem z reguly spustoszenia. Pozostale rody AI: 0 jencow. Gracz: bez zmian (BK, przelacznik w menu wsi), dopoki Jeff nie zdecyduje inaczej.
   J schodzi z czesci "uchodzcy" tej wsi, trafia jako `villager_<kultura wsi>` do `PrisonRoster` partii wodza (prawdziwi jency na mapie: trzeba ich dowiezc,
   rozbicie partii ich uwalnia) i do slownika `wDrodze[partia][region pochodzenia]`. W ksiedze regionu pochodzenia: konto "jency", a po sprzedazy na wyspach - nowa
   kategoria ubytku "uprowadzeni" (nie "polegli"). Wylaczyc tez wtedy dzialke 20% w zlocie z niczego (ta sama metoda BK - `return false` obejmuje i ja).
6. **Prog wolnych rak regionu wysp:** `f_R = FreeHandsShare + min(ThrallFreeHandsExtra; T_R / M_R)`, `M_R = 0.27 x Ludzie_R` (0.20 + do 0.20 = 40% przy pelnym stanie).
7. **Rece do pracy (plon i kopalnie; razem z krokiem 6a):** `rece_R = 0.43 x Dom_R + T_R`; czynnik `(rece_R / rece_R,start)^e`, e = 0.5, wspolny mnoznik w `MaterialLaw.ProdPostfix`
   (SRC `MaterialLaw.cs:158-182`) - nie drugi czynnik obok czynnika ludzi z kroku 6a (0.43 = udzial pracujacych, DOK H4).

### 2.4 Miejsca w kodzie

- NOWY `SRC\ThrallLaw.cs` (`Reset`, `Export`/`Import`, `ApplyAll(Harmony)`, `Daily`, `OnPrisonerSold`, `OnGoldTraded`, `SalePrefix`/`SaleFinalizer`, `CapturePrefix`,
  `FreeHands(Settlement)`, `Hands(Settlement)`, `IsThrallRegion`).
- SRC `SubModuleMain.cs` - `ThrallLaw.ApplyAll(_harmony)` we wlasnym try (wzor `:63`): R12 - prefiks + finalizator na `SellPrisonersAction.ApplyInternal` (typ gry, metoda prywatna
  statyczna - tak samo wpina sie `FairRansom`); R13 - prefiks na `BKRaidCaptureBehavior.ExecuteCapture` (typ po nazwie, brak = wpis "BRAK", bez wyjatku; wzor SRC `OutlawLaw.cs:818-829`).
- SRC `ArmouryBehavior.cs:389` (Reset), `:417-423` (klucz `arm_thralls`), `RegisterEvents :472+`: `CampaignEvents.OnPrisonerSoldEvent` i `HeroOrPartyTradedGold`
  (drugi nasluch obok `MoneyLedger.OnGoldTraded`, `:511` - ksiega dalej liczy swoje; nasza korekta idzie przez `ChangeGold`/`ChangeHeroGold`, wiec w ksiedze wyjdzie
  jako zmiana stanu kasy osady, nie jako przelew gry); `OnDailyTick`: `ThrallLaw.Daily()` w osobnym try po `KingdomTreasury` (`:1183`), przed `OutlawLaw.Daily()`.
- SRC `MoneyLedger.cs:53-56` - nowy rodzaj wywolania-licznika `Note` ("jency", obok zakupow sprzetu, najemnikow i warsztatow): bez niego zaplata z kasy osady za jencow
  wyszlaby w "Przeplywy osad (kasy miast)" jako reszta [R]. Sam licznik - ksiega dalej niczego nie zmienia.
- SRC `PeopleLedger.cs:251-252` - prog z funkcji (6.2). Etap 2: `MaterialLaw.ProdPostfix` (wspolny czynnik z 6a) i `Levy.ExcessShare` (SRC `Levy.cs:58-76`, krok 7).
- Cudzy kod: w R12 bez zmian (BK dalej daje jencow z rabunku rodom z miastem - z niczego, liczone); w R13 jedna latka na BK (`ExecuteCapture`). `SellPrisonersAction`
  i `RealisticCaptivity\FairRansom` - bez zmian w dzialaniu.
- Kolejnosc: w ciagu doby zdarzenia (sprzedaz jencow: stan + zaplata; rabunek: jency w drodze) -> raz na dobe `ThrallLaw.Daily`: ubytek, przeglad `wDrodze` (partie, ktore znikly), linia logu.

### 2.5 Stan do zapisu (`arm_thralls`)

`v1|seeded=1|t=<idRegionu>:<niewolni>,...|way=<idPartii>:<idRegionu>:<n>,...|sum=<zeSprzedazy>;<zRabunkow>;<ubytek>;<zaplaconoZKas>`

### 2.6 Log i test

```
Niewolni: dzien N | stan 26 950 w 10 regionach (start 27 000) | dzis: ze sprzedazy jencow +3 (Pyke 3, w tym chlopi z rabunku BK 0; z kasy osady 240 zl, odebrane sprzedajacemu 0;
 sprzedaze z bohaterem 0, zloto z niczego 0), ubytek -3.7 | jency w partiach i lochach wysp 12 (w tym chlopi z rabunku 0) | rabunki wysp dzis 0 (od poczatku 0)
 | prog wolnych rak wysp 39.9% (pod bronia 6.0% mezczyzn) | rece: wolni 215 000 + niewolni 26 950 -> czynnik plonu 1.000 (czynny od kroku 6a).
```
R1 (na sucho): ta sama linia bez stanu - tylko "jency w partiach i lochach wysp", "rabunki wysp" i liczba `villager_*` w lochach wszystkich partii swiata (ile ludzi z niczego daje dzis BK).
Start (raz): `ThrallLaw: sprzedaz jencow mierzona (SellPrisonersAction wpiete) | rabunki: BK bez zmian / przejete (ExecuteCapture wpiete) | BRAK: ...`.
Test: (1) pierwsza doba nowej kampanii: "stan 27 000" (500 000 x 54 / 1000); (2) `stan dzis = wczoraj + sprzedaz - ubytek`; (3) po sprzedazy jencow w Pyke
kasa Pyke spada o "z kasy osady", a suma "Pieniadz swiata" nie rosnie o cene jencow (zloto z niczego za te sprzedaz zniesione co do sztuki: wyplata gry - korekta = z kasy osady);
(4) w "Ludzie (regiony)" regiony wysp maja prog ok. 40%, nie 20%; (5) bez sprzedazy stan maleje o 0.0137% dziennie; (6) gdy Pyke ma kase pod progiem - "odebrane sprzedajacemu" = cala cena
(sprzedajacy nie dostaje nic, jeniec i tak zostaje niewolnym).

### 2.7 Ryzyko i kolizje

- BK rownolegle dopisuje tych samych jencow do swojej klasy "Slaves" osady (polityka Enslavement jest domyslna, wiec przy kazdej sprzedazy; inna ksiega, inna skala:
  1 jeniec = 1 niewolnik BK w osadzie liczacej u BK kilka tysiecy ludzi) - swiadoma niespojnosc, jak w ANEKS 1.8 pkt 5. Wazne przy kroku 6a: klasa "Slaves" wchodzi
  do modelu plonu wsi BK (`SCR\ore-supply\BKVillageProductionModel.cs:69-87, 210-264`; `LandData.cs:142-166`), a BK dopisuje ja warowni, w ktorej sprzedano jencow - czy i ilu
  z nich trafia do wsi wysp, nie sprawdzono. Nasz czynnik rak moglby wiec liczyc tych samych ludzi drugi raz: przed 6a zmierzyc klase "Slaves" wsi wysp i albo
  nie dawac wyspom naszego czynnika od niewolnych, albo wylaczyc dopis BK (decyzja po pomiarze, nie teraz).
- `OnPrisonerSold` pada tez przy sprzedazy przez ekran partii gracza (`ApplyByPartyScreen`, `SellPrisonersAction.cs:112-115`: bez przelewu zlota w akcji) - dla gracza
  punkt 4 pomijac, liczyc tylko niewolnych.
- Punkt 5 przed krokami 2 i 4 tworzylby ludzi z niczego - tak robi dzis BK dla kazdego rodu z miastem (pkt 0.1.3). Do R13 tych ludzi tylko liczymy; przejecie wczesniej
  zamieniloby jedno "z niczego" na drugie.
- Prefiks na `ExecuteCapture` wylacza jencow z rabunku wszystkim rodom AI spoza listy - takze Dorne i Lorath, ktorym BK daje ich prawem krolestwa (pytanie 6).
  Metoda jest prywatna w cudzym modzie: po aktualizacji BK sprawdzic sygnature; brak = "BRAK" w logu i BK dziala po staremu (ludzie z niczego wracaja - widac w liczniku).
- Jeniec z bitwy nie ma pochodzenia do kroku 5 - do tego czasu `T_R` rosnie, a region pochodzenia nie jest znany (pozycja "bez pochodzenia" w logu).
- Korekta zaplaty zmienia kiese sprzedajacego po fakcie: lord, ktory wjechal do biednego zamku wysp, dostaje mniej, niz policzyla gra. Dla AI bez znaczenia (decyzji
  o sprzedazy nie podejmuje od ceny); dla gracza korekty nie ma.

---

## 3. WOLNI LUDZIE

**Decyzja Jeffa:** walcza wszyscy dorosli (pula ok. polowy doroslych) ORAZ ludnosc w tabeli 150 -> 300 tys.

### 3.1 Zmiana tabeli i jej skutki [KOD + rachunek]

- Zmiana: SRC `PopulationLaw.cs:49` `{ "freefolk", new Region(150000f, 0f) }` -> liczba z nowego ustawienia `FreeFolkPopulation` (300 000), czytana w `Calibrate` (`:82-110`);
  ustawienie jest wlacznikiem (150 000 = stan dzisiejszy) i dziala przy kalibracji, czyli w nowej kampanii albo przez migracje nizej.
- **Kalibracja jest w zapisie** (`Export` pisze naglowek "v1", `:333-341`): nowa kampania dostaje 300 tys. sama; stary sejw trzyma k = 21. Migracja: `Export` pisze "v2",
  a `Import` po odczytaniu naglowka "v1" mnozy `_k["freefolk"][0]` przez `FreeFolkPopulation / 150000` (2; raz - nastepny zapis ma juz "v2") i pisze linie do logu.
- Ludzi na punkt hearth wsi: 21 -> 42 (hearth 18 wsi = 7 192 bez zmian; miasta Wolnych Ludzi maja udzial 0, wiec nadal 0 ludzi i 0 renty - fundament P6).
- Renta nalezna: 16 978 -> 33 956 zl dziennie; zaplacona 1 620 bez zmian (wiaze pulap 20% kiesy wsi, SRC `PopulationLaw.cs:305-313`). Dochod rodow bez zmian.
- "Ludnosc:" swiata +150 tys. (52.57 -> 52.72 mln na starcie).
- Odsetek pod bronia: 3 115 / 81 000 mezczyzn = 3.85% (dzis 7.69%).
- Po kroku 2 demografii czlowiek Wolnych Ludzi = 1/42 hearth (0.024) zamiast 1/21; po kroku 6a ten sam polegly wazy w plonie o polowe mniej.
- Krolestwo Skagos ma kulture `freefolk`, ale jego osady maja kulture `skagosi` (osobny wiersz tabeli) - zmiana go nie dotyka [KOD: `ROT_spkingdoms.xml`, LOG l.159].

### 3.2 "Walcza wszyscy dorosli"

- Pula = 27% ludnosci (wszyscy mezczyzni 16-60 albo, co daje te sama liczbe, polowa wszystkich doroslych - mezczyzni i wlocznice) = 81 000 przy 300 tys.
- W kodzie: prog wolnych rak regionow kultury `freefolk` = `FreeFolkFreeHands` (1.0) zamiast 0.20 (rozdz. 6.2). W logu od R1; w checi do sluzby i hamulcu naboru od kroku 7:
  nadwyzka rak `max(0; f - a) x ludzie / start` = ok. 0.96 (reszta swiata 0.19) -> chec do sluzby ok. 1.0-1.3 przy suficie `RecruitWillingMax` 1.5 (reszta swiata ok. 0.3-0.4, DOK krok 7):
  puste miejsce u notabla zapelnia sie ok. 3 razy szybciej (SZACUNEK).
- Czynnik progu w plonie (6b) dla Wolnych Ludzi jest zawsze 1.0 - mobilizacja nie obniza plonu (lowiectwo i stada prowadza pozostali).

### 3.3 Czy kraina przezyje 1 / 3 / 5 lat (SZACUNEK, `rachunek2_out.txt` rozdz. 3)

Model: polegli to walczacy dorosli (zolnierze partii, obroncy wsi, tabory wiesniakow, straze karawan) - schodza z PULI, nie z calej ludnosci.
Co roku 1/44 puli dorasta (1 841 ludzi przy 300 tys.) i 1/44 sie starzeje; przyrost naturalny 0.5%; wojsko uzupelniane do 3 115, dopoki pula starcza.
Tempo niskie = srednia 28 dob (26.0 zolnierzy + 15.9 pozostalych dziennie = 15 249 rocznie); wysokie = tygodnie 3-4 (39.1 + 24.0 = 22 984 rocznie).

| Wariant | Pula po 1 roku | Po 3 latach | Po 5 latach | Nabor staje (niskie / wysokie tempo) |
|---|---|---|---|---|
| Dzis: 150 tys., prog 20% mezczyzn | -37% / -56% | -94% / -100% | -100% | 1.7 / 1.1 roku |
| **DECYZJA: 300 tys., walcza wszyscy dorosli (pula 81 000)** | **-19% / -28%** | **-55% / -82%** | **-89% / -100%** | **5.4 / 3.5 roku** |
| + H1: Straz nie rabuje wsi za Murem | -16% / -24% | -46% / -69% | -75% / -100% | 6.5 / 4.2 |
| + H2: Inni tylko z poleglych | -15% / -23% | -44% / -66% | -71% / -100% | 6.9 / 4.4 |
| + H1 i H2 | -13% / -19% | -37% / -56% | -60% / -91% | 8.3 / 5.3 |
| + H1, H2 i H3 (przegrani uchodza) | -8% / -12% | -23% / -35% | -38% / -57% | 14.1 / 8.8 |
| + H4 sama (wojna falami 91 dni w roku) | -6% / -9% | -18% / -27% | -29% / -43% | 19.2 / 12.0 |
| + H1, H2, H3 i H4 | -3% / -4% | -7% / -11% | -12% / -18% | ponad 30 lat |

Odpowiedz: po decyzji kraina przezyje rok, po 3 latach zostaje jej 18-45% puli walczacych, a miedzy 3.5 a 5.4 roku nabor staje (dzis: 1.1-1.7 roku).
Cala ludnosc spada wolniej (-5..-7% rocznie), bo gina tylko walczacy. Straty to 8-12 razy wiecej, niz dorasta.

**Przyczyna strat** [DOK ANEKS 2.2; LOG]: 1 173 zabitych w 28 dob: partie 727 (62%), obrona wsi 186 (16%), tabory wiesniakow 138 (12%), karawany 122 (10%).
Zabijaja: Nocna Straz 777 (66%), Inni 385 (33%). Tempo rosnie tygodniami: 93 / 196 / 400 / 484. Wojna ze Straza jest wymuszana kazdej doby (ROT `ROTStorylineWars.cs:94`),
a Straz pali wsie Wolnych Ludzi (10 starc "Raid", pkt 0.1.2).

### 3.4 Hamulce zgodne z kanonem

| Hamulec | Kanon | Jak w kodzie | Skutek (SZACUNEK) | Zalecenie |
|---|---|---|---|---|
| **H1. Straz nie rabuje wsi** | Straz to tarcza - trzyma Mur, zwiadowcy walcza i wracaja; nie pustoszy osad dzikich (ani Siedmiu Krolestw) | postfiks na `CampaignEventDispatcher.AiHourlyTick(MobileParty, PartyThinkParams)` - to samo miejsce, w ktorym ROT wycina Strazy oblezenia (pkt 0.1.12): dla partii Strazy kazda pozycja `AiBehavior.RaidSettlement` dostaje wynik -1 przez `PartyThinkParams.SetBehaviorScore` (TW `PartyThinkParams.cs:98-109`). Wybor celu startuje od -1 i bierze tylko wynik scisle wiekszy (TW `AiPartyThinkBehavior.cs:76-86`; kopia ROT `AIThinkPatch.cs:358-367`), wiec rabunek nie zostanie wybrany nigdy, takze przez wodza armii. Lista zostaje nietknieta (bez `Reset`, ktory kasuje tez znaczniki armii - tak robi ROT). Wlacznik `WatchDoesNotRaid` | -186 zabitych obroncow wsi (16%); wsie Wolnych Ludzi przestaja byc palone; znika tez zrodlo jencow-chlopow BK dla rodu Castle Black (pkt 0.1.3) | TAK (R10) |
| **H2. Inni rosna tylko z poleglych** | regula 4 | regula 4 | Inni zabili 385 (33%); przy doplywie Innych mniejszym o 75-88% przyjeto ich zabojstwa x0.4 | TAK (R2-R4) |
| H3. Przegrani uchodza, zamiast ginac | - (sprawa calego swiata: w bitwach AI ginie 51% przegranych wobec historycznych 15-40%, AUDYT wojna/W8) | osobny temat | zolnierze x0.5 | osobna decyzja Jeffa, nie tu |
| H4. Wojna falami | wielkie najazdy dzikich to wydarzenia (Mance), na co dzien male wypady | postfiks na `ROTStorylineWar.Enforced` (CrashScribe juz ja latka: CS `Fabula.cs:245-275`) + pokoj na czas ciszy | straty x0.33 | NIE na razie: rusza fabule ROT ("walcza od tysiecy lat") i wymaga pokoju, ktorego w kanonie nie bylo |
| H5. Mniej ludzi = mniejsze wojsko (hamulec naboru, pytanie 10 DOK) | zastep topnieje razem z ludem | po kroku 7: cel wojska krainy x (pula / pula startowa) | spowalnia, nie zatrzymuje | po kroku 7 |

Zalecenie: H1 + H2 (oba wynikaja z kanonu i z innych regul), potem pomiar w sesji 60-90 dob. Dopiero gdy pula dalej spada szybciej niz ok. 10% rocznie - H3 jako decyzja o calym swiecie.

### 3.5 Miejsca w kodzie, zapis, log, test

- SRC `PopulationLaw.cs:49` (tabela), `:333-360` (naglowek "v2" i migracja); SRC `PeopleLedger.cs:32, 251-252` (prog z funkcji).
- H1: NOWA metoda w `WatchLaw.cs` (`AiTickPostfix(MobileParty __0, PartyThinkParams __1)`) + `WatchLaw.ApplyAll(Harmony)`: jedna latka na `CampaignEventDispatcher.AiHourlyTick`
  (TW `CampaignEventDispatcher.cs:1224`; metoda publiczna wirtualna, 32 bajty IL [PROBA], ale wolana przez typ bazowy `CampaignEventReceiver` z kopii ROT, ktora zastepuje
  petle myslenia gry - ROT `AIThinkPatch.cs:146-148, 355` - wiec nie do wklejenia; latana juz przez ROT - dwa postfiksy sie nie gryza: gdy ROT pojdzie po nas i odbuduje liste,
  przeniesie nasz wynik -1, gdy przed nami - poprawimy to, co zostawil), wpiecie w SRC `SubModuleMain.cs` obok `:73-80` we wlasnym try; log "wpiete / BRAK". Przebieg: kopia `__1.AIBehaviorScores` do tablicy,
  dla pozycji z `AiBehavior == RaidSettlement` `SetBehaviorScore(in dane, -1f)`. `SetBehaviorScore` zmienia tylko pierwsza rowna pozycje listy (TW `PartyThinkParams.cs:100-106`) -
  po petli sprawdzic, czy nie zostal rabunek z wynikiem ponad -1 (ten sam cel dodany dwa razy; w tym zestawie modow rabunki dodaje tylko `AiMilitaryBehavior.cs:482-483`,
  wiec to bezpiecznik); gdyby zostal - wpisac -1 wprost do prywatnej listy `_aiBehaviorScores` (`AccessTools.Field`), nadal bez `Reset`. Wersja 1 dokumentu latala wynik modelu celu (`GetTargetScoreForFaction`) - odrzucone: w grze sa trzy
  modele (gry, `NavalDLC` i `StrategicCampaignAI145`) i postfiks BK, a wynik 0 moze zostac wybrany, gdy wszystkie inne tez sa zerowe i partia stoi (`AiPartyThinkBehavior`: wynik podbijany wtedy do 1).
- Zapis: bez nowego klucza (naglowek `arm_population` "v2").
- Log: `PopulationLaw: kalibracja ludnosci - ... freefolk 0.30M (wies 42/hearth, miasto 0/dobrobyt)` (liczba obcieta do calosci: 41-43 zaleznie od hearth w chwili kalibracji;
  hearth z `settlements.xml` 7 192 daje 20.9 i 41.7, w sesji 15:22 log pokazal 21); po wczytaniu starego sejwu raz: `PopulationLaw: tabela Wolnych Ludzi 150 -> 300 tys. - przeliczono zapis (wies 21 -> 42 ludzi na hearth).`
  W "Ludzie (regiony)": regiony `freefolk` z progiem 100%. H1: w BIT brak linii "Raid pod ...: ... (Nights Watch)".
- Test: "Ludnosc: ... freefolk 0.30M 1620/33956"; liczba starc "Raid" Strazy = 0 w 28 dobach (bylo 10); zabici Wolnych Ludzi "obrona wsi" bliscy 0.

### 3.6 Ryzyko

- H1 nie dotyczy gracza (tick AI nie obejmuje partii gracza) ani obrony. Rabunek, ktory juz trwa w chwili wgrania, AI przerwie przy najblizszym przemysleniu
  (gra konczy starcie, gdy najlepszy cel nie jest ta wsia - ROT `AIThinkPatch.cs:393-406`) - jednorazowo.
- Bandy BK z hersztem rabuja wlasnym rozkazem (`BanditHeroComponent.cs:120, 220`) - to nie Straz, H1 ich nie dotyka.
- Po H1 partie Strazy moga czesciej wybierac patrol albo oblezenie (ROT pozwala Strazy oblegac tylko Fist of the First Men i Craster's Keep, i dopiero po 1.5 roku) -
  skutek dla liczby bitew nieznany; patrzec w BIT na starcia Strazy wedlug rodzaju.
- Podwojenie ludnosci nie podwaja wojska ani rent - to celowe (wojsko idzie za rodami i warowniami). Bez H1/H2 sama tabela tylko odsuwa koniec z 1.1-1.7 do 3.5-5.4 roku.

---

## 4. NIEUMARLI

**Zasada Jeffa:** rosna tylko z poleglych za Murem i przy Murze. Wynik sprawdzenia (`docs\STAN-PRAC.md`, punkt "Nieumarli"; pelny wynik: `...\tasks\w781xei9u.output`):
dzis NIE - z pokonanych pochodzi najwyzej co czwarty (z zabitych w boju ok. 12%); doplyw Innych ok. 126 dziennie, z czego najwyzej ok. 31 z pokonanych.

### 4.1 Stan w kodzie [KOD tej nocy, liczby DOK]

| Zrodlo / przeciek | Kod | Ile (5 sesji, ok. 76 dob) |
|---|---|---|
| +100 trupow przy narodzinach bandy | ROT `ROTOthersCampaignBehavior.cs:1299-1305` (nasluch `MobilePartyCreated`, `:416`); razem z szablonem 53 (`partyTemplatesROT.xml:1261-1275`) i wodzem = 154 | min. 38 narodzin = ok. 5 850 |
| +2 trupy dziennie na bande | ROT `:1307-1313` (nasluch `DailyTickPartyEvent`, `:415`) | do 488 |
| Ochotnicy "z mapy" (gra daje ich partiom rodow mniejszych) | TW `RecruitmentCampaignBehavior.cs:291-339` (warunek `IsMinorFaction :312`, 3-7 ludzi, szansa 5% na godzine) -> `GetRecruitVolunteerFromMap :646-649` | 700-1 000 (SZACUNEK) |
| Nekromancja po bitwie | ROT `:1315-1381`: zdrowi na starcie strony przegranej x udzial bandy x mnoznik, do wolnego miejsca; potem `RosterToReceiveLootPrisoners.Clear()` (`:1372-1378`) - dla partii AI to jej caly loch (pkt 0.1.13). Metoda nie sprawdza, czy banda byla po stronie zwyciezcow - liczy sie kazda banda Innych w starciu z niepustym skladem (`:1334-1358`) | najwyzej 2 358 = 1 115 zabitych + 1 243 rannych jencow |
| Odbici jency pokonanej partii wchodza do bandy | TW `MapEvent.cs:1529-1593`: prosto do `MemberRoster` zwyciezcy, w zywym typie; ROT zamienia ich na wighty w nastepnym ticku dobowym partii (pkt 0.1.1, 0.1.13) | nieznane |
| Wcielanie jencow przez partie AI | TW `RecruitPrisonersCampaignBehavior.cs:49-96` (banda Innych to partia lorda) | 0 w praktyce: ROT czysci loch bandy po kazdej bitwie |
| Rajd Innych: hearth / 2 trupow | ROT `:857-896` | 0 (rajd AI nieosiagalny dla rodu bez krolestwa) |
| Dezerterzy-wighty i polowa rozbitych do puli wyrzutkow | SRC `OutlawLaw.cs:272-281, 283-302` przez `AddRoster :151-162` | ok. 1 000 + 538-700 |
| Wight w niewoli | TW `DefaultBattleRewardModel.cs:426` (`CanTroopBeTakenPrisoner` = true) | nieznane |
| Dezercje wywoluje nasz Zew | CS `NightKingCall.cs:383-420`: transfer (`:383-396`), dopiero potem premia limitu (`:398-420`), liczona z `PartySizeLimit` z pamieci podrecznej (TW `Party\PartyBase.cs:343-355`) i bez mnoznika BK (BK `VanillaModelTweakPatches.cs:891-916`: x2 wodz rodu, x1.5 pozostali przy `PartySizes` = 2.0) | np. 413 -> 344 w dobie 1 |
| Przed inwazja ROT trzyma bandy za Murem | ROT `:667-677` (`ROTUtilities.IsBeyondTheWall`, `ROT.Misc\ROTUtilities.cs:65-76`) | wszystkie 76 bitew za Murem |

Juz zrobione po naszej stronie: `WarLedger.OnDaily` pomija partie nieumarlych (SRC `WarLedger.cs:40`); rozpoznawanie: SRC `Undead.cs:35-66`.

### 4.2 Regula

**Teren:** bitwa liczy sie, gdy `ROTUtilities.IsBeyondTheWall(pozycja)` (metoda publiczna statyczna [PROBA]; refleksja, wzor CS `NightKingCall.cs:77-81, 137-142`) ALBO region bitwy
(`OutlawLaw.RegionAt`) ma kulture `nightswatch` ("przy Murze"). Gdy metody ROT brak: sam region kultury `freefolk` lub `nightswatch`.

**A. Koniec trupow z niczego (R2).**
1. Prefiks na ROT `OnMobilePartyCreated(MobileParty)` - `return false` (metoda robi tylko +100). Wyjatek do czasu R4: w pierwszych dwoch dobach kampanii
   (`CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow < 2` - ten sam zegar, ktorym ROT odlicza start kampanii, `ROTStorylineWars.cs:238-239`) prefiks przepuszcza metode
   i liczy "+100 bilans otwarcia xN": pierwsze 4 bandy rodza sie miedzy startem a pierwszym tickiem dobowym (sesja 15:22, log CrashScribe `session-2026-10-05_15-22-28.log`
   l.1491: 0 band, l.1582: 4 bandy) po 154, razem 616 - dzisiejszy stan startu. Bez tego samo R2 w nowej kampanii zostawia Innym 4 x 54 = 216 i fabula ROT nie ma z czego ruszyc.
   Od R4 wyjatek znika - te same 616 daje pula cial (punkt 8). W kampanii starszej niz dwie doby wyjatek nie dziala (bandy juz sa).
2. Prefiks na ROT `OnDailyTickParty(MobileParty)` - `return false` (metoda robi tylko +2).
3. Prefiks na TW `RecruitmentCampaignBehavior.ApplyInternal(MobileParty, Settlement, Hero, CharacterObject, int, int, RecruitingDetail)` (`:606-639`): `return false`, gdy
   szczegol = "VolunteerFromMap" (odczyt jak w naszym postfiksie: `object __6`, `ToString()`) i `Undead.Party(__0)`. NIE latac jednolinijkowej `GetRecruitVolunteerFromMap`
   (pkt 0.1.14a). Pominiecie metody zostawia tez zloto wodza (gra kasowala je przelewem do nikogo, `:630`). Nasz postfiks `LevyGold.ApplyInternalPostfix` biegnie mimo
   pominiecia, ale dla tego szczegolu nic nie robi (SRC `LevyGold.cs:37-53`).
4. `OutlawLaw.AddRoster` (SRC `OutlawLaw.cs:151-162`): pomijac `Undead.Character` - wight nie wchodzi do puli wyrzutkow zadna z trzech drog (dezercja `:272-281`,
   rozbici `:283-302`, rozwiazana banda `:321-333`); petla "jency przechodza do bandy" (`:470-485`) - tez pomijac.
5. Prefiks na TW `DesertionCampaignBehavior.CheckDesertionForParty(MobileParty)` (`:27-49`, prywatna statyczna): `return false` dla `Undead.Party`. To jedyne miejsce,
   ktore zdejmuje dezerterow ze skladu i wola `OnTroopsDeserted` - dziala niezaleznie od tego, czyj model dezercji jest czynny (pkt 0.1.14b). Samej zmiany w naszym
   `TierDesertionModel` (SRC `DesertionLaw.cs:109, 115`) byloby za malo: przy wylaczonym `DesertionLawEnabled` rzadzi model gry.
6. Postfiks na kazdej implementacji `BattleRewardModel.CanTroopBeTakenPrisoner(CharacterObject)`: `false` dla `Undead.Character` - pokonany wight znika (TW `MapEvent.cs:2013-2030` zdejmuje go z rostera).
7. (Dopiero w R4, razem z pula cial - inaczej ci ludzie znikaliby bez ksiegowania.) **W bandzie Innych nie ma zywych.** W naszym nasluchu `MapEventEnded`, dla kazdej bandy Innych
   w starciu: kazdy niebohater w `MemberRoster`, ktory nie jest `Undead.Character` (odbici jency pokonanego - pkt 0.1.13), jest zdejmowany ze skladu i liczony jako cialo (punkt 9).
   Bez latki na model lupu. To samo sprzatanie raz na dobe w `UndeadLaw.Daily` jako bezpiecznik (liczone osobno: "zywi w bandzie poza bitwa" - powinno byc 0).

**B. Pula cial (R4).**
8. **Stan:** `Ciala[region]` (liczba) dla regionow terenu. **Bilans otwarcia:** `UndeadStartBodies` (616 = dzisiejsze 4 x 154, "dawni umarli") w regionie siedziby rodu Innych,
   zakladany leniwie przy pierwszym uzyciu (wzor `OutlawLaw.Seed`, SRC `OutlawLaw.cs:195-212`) - pierwsze bandy rodza sie przed pierwszym tickiem dobowym.
9. **Doplyw** - nasz nasluch `MapEventEnded` na terenie:
   - bitwa wygrana przez Innych: ciala = zabici strony zywych (`DiedInBattle`, bez bohaterow) + (gdy `UndeadCaptivesRise`) jency-niebohaterowie w lochach band
     (w chwili zdarzenia sa juz w `PrisonRoster` bandy - pkt 0.1.13; loch byl pusty przed bitwa, bo czyscimy go po kazdej) + zywi zdjeci ze skladu w punkcie 7.
     Wstaja od razu: banda i dostaje `min(wolne miejsce; ciala x udzial_i)`, udzial = `ContributionToBattle` bandy / suma band Innych;
     nadwyzka zostaje w `Ciala[region]` (dzis przepada). Gdy `UndeadCaptivesRise` = false: jency i odbici sa wypuszczani do puli wyrzutkow regionu bitwy w swoim typie
     (nowy wewnetrzny akcesor w `OutlawLaw` obok `:139-142`, bo `AddRoster` jest prywatna);
   - bitwa zywych z zywymi: `Ciala[region] += zabici obu stron x UndeadLivingBattleBodiesPercent / 100` (50%: reszte spalono);
   - bitwa przegrana przez Innych: zabici zywych nie daja cial (zwyciezca pali poleglych); zniszczone wighty nigdy. (ROT dzis podnosi trupy takze bandzie, ktora
     przegrala, jesli zostal jej sklad - pkt 4.1; tej furtki nie powtarzamy.)
10. **Zamiast nekromancji ROT:** prefiks na ROT `OnMapEventEnded(MapEvent)` - samo `return false`; cala obsluge robi nasz nasluch `MapEventEnded` (kolejnosc nasluchow
    przestaje miec znaczenie, bo metoda ROT niczego juz nie rusza). Nasz nasluch powtarza jedyna inna czynnosc tej metody: dla kazdej partii Innych
    w starciu (pomijajac partie gracza-wighta i partie z pustym rosterem, jak ROT `:1356`) na koncu `RosterToReceiveLootPrisoners.Clear()` (ROT `:1372-1378`;
    wlasciwosc publiczna `MapEventParty`, TW `MapEventParty.cs:87-101`) - po policzeniu jencow do punktu 9.
11. **Podnoszenie dobowe (zamiast +2):** banda Innych bierze do `UndeadRaisePerDay` (2) cial z regionu, w ktorym stoi (potem z sasiednich, `OutlawLaw.Near`), do wolnego miejsca.
12. **Narodziny bandy:** galaz `Undead.Party` w `OutlawLaw.RosterPostfix` (SRC `OutlawLaw.cs:546-567`), PRZED warunkiem `IsOutlawParty` i niezalezna od `OutlawLaw.On`:
    `n = min(UndeadBandBirthSize (153); wszystkie ciala terenu, od najblizszych)`; sklad = szablon przyciety proporcjonalnie, a ponad 53 - `angry_wight` (ten sam oddzial, co ROT `:515`).
    Pusta pula = sam wodz.
13. **Rajd Innych:** prefiks na ROT `OnRaidCompleted` - liczba ROT (hearth / 2 x mnoznik) nie wchodzi wprost do bandy, tylko do `Ciala[region wsi]`; po kroku 4 demografii
    zastapiona liczba ludzi wsi z reguly spustoszenia i zapisana jako polegli tej wsi. Do kroku 4 to nadal ciala bez odjecia ludzi z wsi (jak dzis u ROT) - jedyne
    "z niczego", ktore zostaje w regule; w 5 sesjach 0 przypadkow, licznik w logu ("ciala z rajdow Innych").
14. **Zniszczony wight:** bez ciala, bez powrotu. `zniszczeni` = `DiedInBattle` + `RoutedInBattle` bandy (rozbity wight nie idzie do puli - punkt 4) + przy klesce ci,
    ktorzy zostali w skladzie (gra zdejmuje ich z rostera, a po punkcie 6 nie ida do niewoli). Liczone jako roznica stanu: nasluch `MapEventStarted` (jest juz w
    SRC `ArmouryBehavior.cs:518`) zapamietuje liczbe niebohaterow kazdej bandy Innych w starciu, `MapEventEnded` odejmuje stan po bitwie (przed podniesieniem nowych).
    Banda, ktora dolaczyla w trakcie i nie ma zapamietanego stanu: sama suma `DiedInBattle` + `RoutedInBattle`.

**C. Naprawa Zewu (R3, CrashScribe - osobny DLL).** W CS `NightKingCall.cs:383-420`:
- limit liczyc z zywego modelu, nie z pamieci: `L(b) = Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(partia.Party).ResultNumber` przy `_partySizeGrowth = b`
  (dwa pomiary: b = 0 i b = 100 -> nachylenie k; obejmuje mnoznik BK i +250 Nocnego Krola bez stalych w kodzie);
- najpierw policzyc planowany transfer (bez przenoszenia), potem ustawic premie `b = (cel - L(0)) / k`, gdzie `cel = max po wszystkich bandach(ludzie_po_transferze / NightKingCallFullness)`
  (premia jest wspolna dla wszystkich band - zadna nie moze znalezc sie ponad limitem), dopiero potem przeniesc;
- nie obnizac premii, gdy ktorakolwiek banda jest zajeta (bitwa, oblezenie) - horda oblegajaca z ok. 503 ludzmi traci dzis limit;
- zrownac progi: `NightKingMarchMin` (500, CS `Config.cs:40`) = `NightKingCallTarget` (520, `:33`).
Po punkcie 5 przekroczenie limitu i tak nie zabierze juz wightow - poprawka Zewu usuwa przyczyne, punkt 5 skutek.

### 4.3 Miejsca w kodzie

- NOWY `SRC\UndeadLaw.cs` (`Reset`, `Export`/`Import`, `ApplyAll(Harmony)`, `OnMapEventStarted`, `OnMapEventEnded`, `Daily`, `BirthRoster`, `Ground(Vec2, Settlement)`); wpiecie latek
  wzorem `OutlawLaw.ApplyAll` (SRC `OutlawLaw.cs:818-829`: typ po nazwie, metoda, hak, etykieta, lista "BRAK"): typ `ROT.CampaignBehaviors.ROTOthersCampaignBehavior` (klasa `internal`),
  metody `OnMobilePartyCreated`, `OnDailyTickParty` (R2), `OnMapEventEnded`, `OnRaidCompleted` (R4) - wszystkie cztery sa nasluchami zdarzen wolanymi przez delegata (ROT `:384, 415-418`),
  wiec JIT ich nie wklei; `RecruitmentCampaignBehavior.ApplyInternal` (prefiks obok istniejacego postfiksu, SRC `LevyGold.cs:89-90`); `DesertionCampaignBehavior.CheckDesertionForParty`;
  petla po typach `BattleRewardModel` dla `CanTroopBeTakenPrisoner` (wzor petli SRC `OutlawLaw.cs:852-873`).
- SRC `SubModuleMain.cs` - `UndeadLaw.ApplyAll(_harmony)` we wlasnym try (wzor `:63`); SRC `ArmouryBehavior.cs:389` (Reset), `:417-423` (klucz `arm_undead`), nasluchy `MapEventEnded`
  obok `:512` i `MapEventStarted` obok `:518`, `UndeadLaw.Daily()` po `OutlawLaw.Daily()` (`:1185`).
- SRC `OutlawLaw.cs:151-162, 470-485, 546-567`; `:102-112` (`Near` jest prywatna - wewnetrzny akcesor dla punktow 11 i 12, obok `:139-142`); CS `NightKingCall.cs:383-420`, `Config.cs:33, 40`.
  SRC `DesertionLaw.cs` bez zmian (blokada siedzi w latce na zachowanie gry).
- **Proba poza gra - WYKONANA tej nocy** [PROBA: `SCR\noc-2\reguly\proba\Program.cs`, wynik `run.log`; wzor `SCR\przeglad100-latka\htest`; Harmony 2.4.2 z `libs`, biblioteki gry,
  ROT, BK i NavalDLC tylko czytane]:
  - kazda metoda z list R2-R4, R10, R12, R13 istnieje pod ta nazwa i w jednej sygnaturze (bez `AmbiguousMatch`, ktory wywrocil kiedys `PopulationLaw`, SRC `PopulationLaw.cs:133-134`):
    ROT `OnMobilePartyCreated` (IL 46 bajtow), `OnDailyTickParty` (96), `OnMapEventEnded` (531), `OnRaidCompleted` (206); gra `RecruitmentCampaignBehavior.ApplyInternal` (267),
    `DesertionCampaignBehavior.CheckDesertionForParty` (264, prywatna statyczna), `SellPrisonersAction.ApplyInternal` (662), `CampaignEventDispatcher.AiHourlyTick`; BK `ExecuteCapture` (1016);
  - prefiks zwracajacy `false` pomija cialo prywatnego nasluchu ROT wolanego przez delegata - i dla delegata utworzonego przed latka, i po niej (bez latki: wyjatek z ciala metody;
    z latka: brak wyjatku, 4 trafienia prefiksu na 4 wywolania; po przelaczeniu prefiksu na `true` cialo znow biegnie);
  - to samo dla `RecruitmentCampaignBehavior.ApplyInternal` ze szczegolem `VolunteerFromMap`;
  - `GetRecruitVolunteerFromMap` ma 14 bajtow IL (potwierdza pkt 0.1.14a), `DefaultBattleRewardModel.CanTroopBeTakenPrisoner` 2 bajty, a `NavalDLC` ma wlasny model lupu
    `NavalDLC.GameComponents.NavalDLCBattleRewardModel` z wlasna `CanTroopBeTakenPrisoner` - petla po wszystkich typach modelu jest konieczna, sama latka na model gry nie wystarczy;
    BK wlasnego modelu lupu nie ma;
  - `Village.TradeTaxAccumulated` i `Kingdom.KingdomBudgetWallet` maja publiczny `set`, `MapEventParty.RosterToReceiveLootPrisoners` jest publiczna.
  Proba NIE sprawdza zachowania w kampanii (kolejnosc zdarzen, liczby) - to pokaze dopiero log.
- Przy okazji logu (R1): SRC `BattleChronicle.cs:59` pomija starcia bez zabitych i rannych - rozbicie hordy bez strat (162 rozbitkow w sesji 02:22) nie ma linii "Bitwa:".

### 4.4 Stan do zapisu (`arm_undead`)

`v1|seeded=1|b=<idRegionu>:<ciala>,...|last=<trupy w polu wczoraj>|sum=<podniesieniZBitew>;<zPuli>;<narodziny>;<zniszczeni>;<cialaZWygranych>;<cialaZBitewZywych>`

### 4.5 Log i test

```
Umarli (ksiega): dzien N | w polu 612 w 4 bandach | dzis: podniesieni po bitwach 41 (zabici 25 + jency 14 + odbici jency 2), z puli 8, narodziny 0 | zniszczeni 13 (zabici 9, rozbici 4, po klesce 0)
 | bez zdarzenia 0 | pula cial 655 w 7 regionach (doszlo: z wygranych Innych 0 ponad miejsce, z bitew zywych 26) | zywi w bandzie poza bitwa 0
 | zablokowane: +100 x0, +2 x4, ochotnicy z mapy 1, dezercja 0 partii, niewola 0.
```
Start (raz): `UndeadLaw: zrodla z niczego zamkniete - wpiete: ROT +100, ROT +2, ROT nekromancja, ROT rajd, ochotnicy z mapy (ApplyInternal), dezercja (CheckDesertionForParty), niewola w N modelach; BRAK: ...`
(w tym zestawie modow N = 2: model gry i `NavalDLCBattleRewardModel`).
Kontrola: `w polu dzis = wczoraj + podniesieni + z puli + narodziny - zniszczeni + bez zdarzenia`; kazde "bez zdarzenia" rozne od 0 to przeciek
(znany: wyniszczenie oblezenia `RealisticBannerlord` zabija wighty bez bitwy - ujemne). Bandom, ktore w chwili spisu sa w starciu, doliczyc do "w polu" niebohaterow
z `DiedInBattle + RoutedInBattle` ich `MapEventParty` (gra zdejmuje ich ze skladu w trakcie bitwy, a "zniszczonych" ksiegujemy na jej koncu - jak w spisie Strazy, 1.3 punkt 1);
bez tego bitwa trwajaca przez tick dobowy daje pare falszywych "bez zdarzenia" (-n, nazajutrz +n).
Test: (1) nowa kampania, doba 1: 4 bandy po 153 trupy + wodz (616 razem z wodzami, jak dzis), "narodziny 612", pula cial 4; (2) doba bez bitew: stan Innych bez zmian albo +2 na bande tylko, gdy pula > 0;
(3) w "Wyrzutki:" brak skokow "dezercja" po ruchu Zewu (bylo 64-198); (4) w CS: "Zew ... premia limitu hordy" nie spada, gdy horda oblega, a nazajutrz stan hordy = stan po transferze;
(5) CS "UMARLI: ... N trupow w polu" zgodne z "w polu" naszej linii.

### 4.6 Ryzyko i kolizje

- **Fabula ROT.** Oblezenie wymaga 500 zdrowych w bandzie, rajd ponad 400 (CS `NightKingCall.cs:17-21`). Po zamknieciu zrodel Inni rosna tylko tak szybko, jak gina ludzie za Murem:
  ciala z bitew zywych przy 50% to ok. 26 dziennie (ok. 9 600 rocznie), przy 0% - zero poza wlasnymi zwyciestwami (do ok. 15-31 dziennie). Bilans otwarcia 616 wystarcza Zewowi na
  jedna horde (jak dzis w dobie 1). Jesli H1 i pokoj za Murem zmniejsza liczbe poleglych, Inni slabna razem z nimi - to jest wlasnie zasada Jeffa, ale moze opoznic inwazje (i tak nie ruszyla w zadnej z 5 sesji).
- Prefiksy `return false` na metodach ROT zakladaja, ze metody robia tylko to, co w dekompilacji z 16.08 - po aktualizacji ROT sprawdzic cztery metody ponownie; brak metody = wpis "BRAK", bez wyjatku.
  Ze prefiks na tych metodach dziala, sprawdzono proba poza gra (4.3) - nie zakladac tego dla nowych metod bez powtorzenia proby.
- Narodziny bandy: wight-niebohater pojawia sie w skladzie dopiero przez `FindAppropriateInitialRosterForMobileParty`, a wodz jest dopisany do skladu wczesniej
  (TW `LordPartyComponent.cs:29-40`), wiec `Undead.Party` rozpozna bande juz w chwili losowania skladu (po wodzu albo po kulturze rodu). Gdyby nie rozpoznala - banda dostanie
  53 trupy z szablonu z niczego; widac to w tescie (1) jako "narodziny" mniejsze od stanu bandy.
- Dzisiejsze tempo narodzin (min. 38 band w ok. 76 dobach, STAN-PRAC) to popyt ok. 76 cial dziennie przy doplywie ok. 26 dziennie z bitew zywych - nowe bandy beda
  sie rodzic male albo z samym wodzem. To skutek zasady, nie blad; jesli Wedrowcy bez bandy okaza sie latwym lupem i ROT zacznie ich tracic, wrocic z pytaniem do Jeffa.
- Banda ponad limitem partii nie traci juz wightow dezercja (punkt 5), wiec do czasu poprawki Zewu (R3) horda po transferze moze stac ponad limitem - bez skutku
  dla gry poza tym, ze ROT nie doliczy jej nowych trupow ("wolne miejsce" ujemne).
- `OnMapEventEnded` ROT czysci caly roster jencow bandy razem z bohaterami - powtarzamy to wiernie (nie zmieniamy losu pojmanych lordow; przemiane lorda robi inna latka ROT).
- Gracz grajacy wightem: ROT wylacza dla niego nekromancje (`:1356`) - zostawiamy wyjatek.
- Kolizja z regula 1: Straz nie bierze z pul wightow (i tak ich tam nie bedzie po punkcie 4). Kolizja z regula 2: wight nie moze zostac niewolnym (punkt 6 + filtr w `ThrallLaw`).
- Osady Innych (ochotnicy, milicja, garnizon, patrole) - w 5 sesjach 0 osad; do zrobienia, gdy Inni zdobeda pierwsza (lista w wyniku badania, pkt A7).
- Rajd Innych a BK: rod Innych nie ma krolestwa, wiec BK uznaje, ze ma "prawo" do jencow z rabunku (pkt 0.1.3; `RaidCapturePolicyManager.cs:44-47`) - po wygranym rabunku
  dopisalby bandzie `villager_*` do lochu z niczego, a nasz punkt 9 policzylby ich przy nastepnej bitwie jako ciala. W 5 sesjach 0 rabunkow Innych; do R13 (prefiks na
  `ExecuteCapture`) liczyc osobno: jency w lochu bandy Innych sprzed bitwy = "ciala z jencow BK".

---

## 5. DLUG BEZ UTRATY LENNA

**Decyzja Jeffa:** lord NIGDY nie traci lenna. Wierzyciel pobiera dochod z jego wsi az do splaty; lord splaca z tego, co ma - w ostatecznosci wyprzedaje wszystko.

### 5.1 Stan w kodzie [KOD: SRC `IronBank.cs`]

- Dlug: klasa `Debt` (`:41-51`): kwota, stopa, termin, spoznienia, wiarygodnosc, bankrut, liczba pozyczek. Jeden wierzyciel - Bank (`_capital`, `:54`), jeden dlug na rod.
- AI pozycza, gdy kiesa glowy < 10 dni zoldu PARTII (wojna: 20) (`:203-213`); `Wages` nie liczy garnizonow (`:82-87`); `Limit` nie liczy naszych rent (`:109-110`).
- Rata dzienna = dlug / dni do terminu (`:234-235`); brak pelnej raty = spoznienie: wiarygodnosc x0.8, stopa +2 pp (`:236-240`) - lord nie placi wtedy NIC.
- 3 spoznienia = bankructwo (`:241-253`): 50% kiesy od razu, potem 25% kiesy dziennie (`:228-231`), odsetki rosna dalej (`:223-224`), wyjscie tylko po pelnej splacie (`:268-273`).
  Lenna, wsie, majatek, wojsko - nietkniete. Kiesa 40 000 schodzi ponizej 2 000 w 9 dni - limit zoldu BK spada wtedy z ok. 620 do ok. 200 zl na partie (pkt 0.1.11),
  wiec poczet kurczy sie dezercja do ok. 45 ludzi na partie (SZACUNEK przy 4.4 zl zoldu na czlowieka).
- **Poczet dluznika topnieje juz dzis, i to zanim Bank cokolwiek zrobi** [KOD, pkt 0.1.11]: AI pozycza dopiero ponizej 10 dni zoldu partii, a BK tnie limit wyplat kazdej partii
  juz ponizej 112 917 zl kiesy (600-1 200 zl dziennie), a ponizej 37 639 do 200-600 zl (x1.5 partii glowy rodu). Partia z zoldem ponad limit traci do 20 ludzi dziennie (do puli wyrzutkow), a gdy zold jest
  nieoplacony - takze przez nasz `WarLedger` (SRC `WarLedger.cs:113-139`: ludzie znikaja bez zdarzenia). "Rozpuszczenie pocztu" nie jest wiec ostatnim szczeblem, ktory
  dopiero trzeba zbudowac - jest pierwszym skutkiem biedy; drabina moze tylko (a) nie pogarszac go zajeciem kiesy i (b) zadbac, zeby odchodzacy nie znikali.
- Blad: `:239` `Math.Max(0.5f, Trust * 0.8f)` PODNOSI wiarygodnosc bylego bankruta z 0.25 do 0.5 po spoznieniu (AUDYT lordowie-kod/W2, weryfikator).
- W 12 sesjach: 0 pozyczek, 0 spoznien, 0 bankructw (AUDYT) - cala sciezka jest nieprzetestowana w grze; ponizsze to przebudowa "na zapas".

Dochod pana z jednej wsi [DOK fundament P4; dwie sesje]: renta `PopulationLaw` 140-170 zl dziennie + utarg BK ("Village Demesnes") 160-283 zl = **300-453 zl na wies dziennie**.
Renty miast to 72-77% wszystkich rent (245-262 z 342 tys.) - doslowne "dochod z jego wsi" ich nie obejmuje.

### 5.2 Drabina po decyzji Jeffa (przerobione S1-S7 audytu)

Jedna miara dla wszystkich szczebli: `Z = kiesa glowy rodu / dzienny zold partii i garnizonow rodu` (dni zoldu).

| Szczebel | Wejscie | Co sie dzieje | Wyjscie | Z audytu |
|---|---|---|---|---|
| D0 czysty | brak dlugu | - | - | - |
| D1 kredyt | AI: `Z < IronBankWageDays` (10; wojna x2) - jak dzis, ale zold z garnizonami | pozyczka do 1.5 x braku, w limicie; wierzyciel w kolejnosci: skarbiec krolestwa (10%), bogaty rod tego samego krolestwa (20%), Bank (20/30/45% jak dzis) | splata | S2 |
| D2 zaleglosc | rata niezaplacona w calosci | lord placi tyle, ile ma ponad `DebtArrearsKeepDays` (3) dni zoldu (dzis: nic); spoznienie jak dzis (x0.8, +2 pp); flaga "sprzedaj nadwyzki" (5.4 krok 0) | pelna rata -> D1 | S5 w czesci |
| **D3 zajecie dochodu wsi** | 3 spoznienia pod rzad (ZAMIAST bankructwa) | wierzyciel bierze u zrodla rente i utarg WSZYSTKICH wsi rodu (`DebtSeizeVillagePercent` 100) az do splaty; z kiesy codziennie tylko to, co lezy ponad rezerwe = wieksza z: `DebtKeepWageDays` (10) dni zoldu i `DebtPurseFloorGold` (38 000 - przy tej kiesie BK zostawia partii ok. 600 zl zoldu dziennie, partii glowy rodu 900; nizej limit spada do 200 - pkt 0.1.11); odsetki zamrozone w dniu zajecia; kredyt odciety (znacznik `Defaulted`, jak dzis); "Bank pozycza wrogom" jak dzis; gracz -100 renomy jak dzis | dlug 0 -> D0, wiarygodnosc 0 + 0.25 = 0.25 i narzut +15 pp - oba jak dzis (SRC `IronBank.cs:270-271, 136`) | S3 + S6 |
| D4 wyprzedaz | w D3 prognoza splaty (`dlug / srednia 7 dni zajec`) > `DebtHorizonDays` (364) albo zajecia = 0 przez 14 dni | co `DebtSaleEveryDays` (7) jeden krok: 0 nadwyzki zbrojowni i luzne konie, 1 karawany, 2 warsztaty; kazdy przychod wprost do wierzyciela | prognoza <= 364 dni | S5 |
| D5 poczet | NIE jest osobnym krokiem w czasie: poczet kurczy sie sam od chwili, gdy kiesa spada pod prog BK (5.1) - zwykle jeszcze w D1-D2 | do kroku 5 demografii: nic nowego poza poprawka, zeby odchodzacy z braku zoldu szli do puli wyrzutkow (R5b); po kroku 5: zamiast dezercji uporzadkowane zwolnienie do stanu, na ktory starcza dochodu po zajeciu (cel: zold <= 80% dochodu, 10% dziennie, najemnicy i najnizsze tiery pierwsi), zwolnieni wracaja do regionu pochodzenia, nie do lasu | zold <= dochod | S4 |
| koniec bez wsi | rod bez wsi w D3-D5 po `DebtWriteOffDays` (364) | reszta dlugu = strata wierzyciela (linia logu), wiarygodnosc 0 na kolejne 364 dni | - | zamiast S7 |
| ~~S7 sprzedaz lenna~~ | - | SKRESLONE: lenno zostaje zawsze | - | S7 |
| oszczednosc (S1) | dlug > 0 | osobny, pozniejszy wpis: budowy cywilne stop, zakupy sprzetu tylko braki, bez nowych karawan / warsztatow / okretow / rycerzy BK (AUDYT lordowie-kod/W4) | - | S1 |

Ile trwa zajecie (odsetki zamrozone; `rachunek2_out.txt` rozdz. 5):

| Rod | Dlug | Sama renta wsi | Renta + utarg |
|---|---|---|---|
| z zamkiem, 2 wsie, pelny dzisiejszy limit | 29 300 | 280-340 zl dziennie: 87-105 dni | 600-906 zl: 33-49 dni |
| z zamkiem, 3 wsie | 29 300 | 58-70 dni | 22-33 dni |
| z miastem, 4 wsie | 67 000 | 99-120 dni | 37-56 dni |
| krol, 6 wsi | 67 000 | 66-80 dni | 25-38 dni |
| 2 wsie, dlug 100 000 (po poprawce limitu o renty) | 100 000 | 295-357 dni | 111-167 dni |

Przy dzisiejszych limitach zajecie konczy sie w 1-4 miesiace; wyprzedaz (D4) to rzeczywiscie ostatecznosc - dla rodow bez wsi i dla Strazy / Wolnych Ludzi (pkt 0.1.9).
Poczet topnieje wczesniej i z innej przyczyny (limit zoldu BK, 5.1) - tego drabina nie odwraca, tylko nie pogarsza (podloga kiesy) i porzadkuje, dokad ida ludzie.

### 5.3 Zajecie: renta i utarg u zrodla, kiesa ponad podloge

**(a) Renta.** SRC `PopulationLaw.cs:315-316` (dzis: `GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay, true)` i dopis do `RentToday`):
```csharp
int seized = st.IsVillage ? IronBank.SeizeRent(st.OwnerClan, st, pay) : 0;   // zajecie: kiesa wsi -> wierzyciel; zwraca przejeta kwote
int toLord = pay - seized;
if (toLord > 0)
{
    GiveGoldAction.ApplyForSettlementToCharacter(st, lord, toLord, true);
    int r0; RentToday.TryGetValue(st.OwnerClan, out r0); RentToday[st.OwnerClan] = r0 + toLord;   // powinnosci i budowy licza tylko to, co pan dostal
}
```
`IronBank.SeizeRent`: gdy rod jest w D3+: `x = min(dlug; pay x DebtSeizeVillagePercent / 100)`; `st.SettlementComponent.ChangeGold(-x)`; `PayCreditor(d, x)`; `d.Principal -= x`; licznik dnia. Linia "Ludnosc:" dalej liczy calosc jako "renty zaplacone", z dopiskiem "w tym zajete przez wierzycieli N".

**(b) Utarg.** Licznik podatku pana `Village.TradeTaxAccumulated` rosnie tylko w oknie powrotu taboru do wlasnej wsi (BK dzieli wtedy utarg, pkt 0.1.6). Dwa nasluchy w `IronBank`
(wlasna migawka - nie zalezec od wylacznika ksiegi `MoneyLedger.Live`): `BeforeSettlementEnteredEvent` (tabor wsi wjezdza do swojej wsi: zapamietac licznik) i `AfterSettlementEntered`
(`delta = licznik - zapamietany`; gdy `delta > 0` i wlasciciel wsi w D3+: `x = min(dlug; delta x procent)`; `wies.TradeTaxAccumulated -= x`; `PayCreditor`). Rejestracja w SRC `ArmouryBehavior.cs`
PO nasluchach ksiegi (`:509-510`), zeby "Przeplywy osad" mierzyly podzial sprzed zajecia. Rachunek sie zamyka: pan dostawalby z licznika 80% dziennie minus koszty administracji BK,
a reszta zostaje w liczniku na nastepne doby (`EconomyPatches.cs:477-481`), wiec zloto zdjete z licznika to zloto, ktore i tak szlo do pana - nic nie powstaje ani nie znika.
Majatki BK: czesc utargu idzie w tym samym oknie wprost do wlascicieli majatkow (`EstateData.cs:95-99`, przelew bez dawcy). Gdy odbiorca nalezy do rodu dluznika,
ta kwota tez jest dochodem z jego wsi: nasluch `HeroOrPartyTradedGold` w otwartym oknie (ten sam warunek co w ksiedze, SRC `MoneyLedger.cs:231`) zdejmuje ja odbiorcy
(`ChangeHeroGold(-x)`) i oddaje wierzycielowi; majatki cudzych bohaterow (notabli, rycerzy innych rodow) zostaja nietkniete. Poza zajeciem zostaje podatek klas ludnosci
wsi z urzedem podatkowym BK (`BKTaxModel.cs:272-279` - powstaje z niczego, nie z licznika; temat 13 kluczy / ksiegi pieniadza, nie tej reguly).

Wylacznik `DebtSeizureEnabled` = false przywraca dzisiejsze bankructwo (50% + 25% kiesy dziennie) - do proby A/B z jednego sejwu.

**(c) Kiesa.** W `IronBank.Daily` zamiast `:228-231`: `rezerwa = max(zold x DebtKeepWageDays; DebtPurseFloorGold)`; `pay = min(dlug; max(0; kiesa - rezerwa) x DebtPursePercent / 100)` (100),
gdzie `zold` = partie + garnizony (po R5). `DebtPurseFloorGold` = 38 000 to kiesa, przy ktorej BK zostawia kazdej partii ok. 600 zl zoldu dziennie (partii glowy rodu 900;
30 000 x `BaseWage` 1.2546 = 37 639 - pkt 0.1.11). Podloga NIE znosi ciecia BK (pelny limit jest dopiero ponad 112 917 zl, a funkcja nie ma skoku przy 37 639) - wyznacza tylko,
jak duzy poczet dluznik moze utrzymac w czasie zajecia: 38 000 -> ok. 135 ludzi na partie, 75 000 -> ok. 200, 112 917 -> bez ciecia; ustawienie 0-10 000 przywraca "golego"
dluznika (limit 200-230 zl, ok. 45-50 ludzi na partie; SZACUNEK przy 4.4 zl zoldu na czlowieka). Wierzyciel bierze wiec z kiesy tylko to, co dluznik zgromadzi ponad podloge
(lup, okup, sprzedany majatek, renty miast); dluznik, ktory wpadl w zajecie z pusta kiesa, najpierw odbudowuje ja do podlogi. Glownym strumieniem splaty sa wsie - zgodnie
z decyzja. Gracz: jak dzis 25% kiesy (rezerwa gracza = 0, `:226`).

**`PayCreditor(d, x)`:** Bank -> `_capital += x`; skarbiec -> `Kingdom.KingdomBudgetWallet += x` (pole zapisywalne - tak pisze `KingdomTreasury`, SRC `KingdomTreasury.cs:70`);
rod -> `glowa.ChangeHeroGold(x)`. Wierzyciel zniknal (krolestwo albo rod wymarly) -> reszta dlugu umorzona z linia logu (jak dzis strata Banku, `:275-280`).

### 5.4 Wyprzedaz: co i komu (wszystko za prawdziwe zloto kupujacego)

| Krok | Co | Komu | Jak | Stan dzis |
|---|---|---|---|---|
| - | towary i lup w jukach partii | targ miasta (kasa miasta) | nic nie robimy: gra sprzedaje caly nie-zywnosciowy ladunek przy kazdym wjezdzie do miasta (TW `PartiesSellLootCampaignBehavior.cs:19-42`); przychod trafia do kiesy i schodzi regula (c) | dziala |
| 0 | nadwyzki zbrojowni DTE | targ miasta | `MenPurse.SellAiSurplus` (SRC `MenPurse.cs:269-312`): dla dluznika w D2+ zapas `SurplusKeepPercent` = 0, a "trzecia lorda" idzie wprost do wierzyciela; dwie trzecie zostaja ludziom (ich lup - wpis 84) | mechanizm jest |
| 0 | luzne konie wierzchowe w jukach (gra ich nie sprzedaje) | targ miasta | przy wjezdzie do miasta: zwykle wierzchowce (`Stables.IsPlainMount`, SRC `Stables.cs:539`) ponad liczbe jezdnych w skladzie partii, przez `SellItemsAction.Apply` (ta sama akcja co gra) | brak |
| 1 | karawany rodu (`Hero.OwnedCaravans`) | targ miasta | gdy karawana stoi w miescie: ladunek przez `SellItemsAction`, `PartyTradeGold` do wierzyciela, partia rozwiazana (`DestroyPartyAction.Apply(null, karawana)`); straze - do ksiegi ludzi po kroku 5 (do tego czasu znikaja jak przy kazdym rozwiazaniu partii - licznik "straze rozwiazanych karawan" w linii "Dlugi:") | brak |
| 2 | warsztaty (`Hero.OwnedWorkshops`) | najbogatszy notabl tego miasta | `cena = min(WorkshopModel.GetCostForNotable(w); zloto notabla)`; gdy `cena >= DebtWorkshopMinPrice` (1 000): `notabl.ChangeHeroGold(-cena)`, `PayCreditor`, `ChangeOwnerOfWorkshopAction.ApplyByDeath(w, notabl)` - zachowuje rodzaj i kapital warsztatu, zlota nie przenosi (TW `ChangeOwnerOfWorkshopAction.cs:39-42`). NIE `ApplyByBankruptcy`: ta ustawia kapital warsztatu na `InitialCapital` (10 000 w modelu gry, `DefaultWorkshopModel.cs:23`; `BKWorkshopModel` tego nie zmienia; `:22-25`, `Workshop.cs:135-145`) - roznica wobec starego kapitalu powstawalaby z niczego albo znikala, a cena `GetCostForNotable` liczy juz polowe kapitalu (`DefaultWorkshopModel.cs:65-68`) | brak |
| (D5) | poczet | - | po kroku 5: wspolna funkcja z rozpuszczaniem pocztu w pokoju (decyzja Jeffa: do 35-40% stanu); do tego czasu dziala tylko limit zoldu BK (dezercja gry - ludzie ida do puli wyrzutkow) i `WarLedger` - z poprawka R5b: po zdjeciu ludzi w `DesertElitesFirst` (SRC `WarLedger.cs:133`) zebrac ich w roster i wywolac wprost nasze dwa nasluchy dezercji: `OutlawLaw.OnTroopsDeserted(mp, roster)` (SRC `OutlawLaw.cs:272-281`) i `PeopleLedger.OnTroopsDeserted` (SRC `PeopleLedger.cs:163-172`; pozniej takze `WatchLaw.OnDeserted` zamiast licznika `NoteLoss`), zeby szli do puli zamiast znikac (AUDYT lordowie-kod/W8). Zdarzenia gry nie wolac - slucha go tez zadanie gry o szkoleniu druzyny | czesciowo |
| poza v1 | okrety (sprzedaz w grze daje zloto z niczego), posiadlosci BK, wirtualne skarbce zamkow BEE | - | po osobnym sprawdzeniu | - |

Gracz-dluznik: zajecie renty i utargu jego wsi + 25% kiesy dziennie; jego majatku nie sprzedajemy za niego.

### 5.5 Poprawki Banku (R5, male, przed drabina)

1. `Wages` (`:82-87`): dodac zold garnizonow rodu (`foreach (var f in c.Fiefs) if (f != null && f.GarrisonParty != null) w += f.GarrisonParty.TotalWage;` - warownia bez garnizonu ma `GarrisonParty` puste).
2. `Limit` (`:109-110`): `income += PopulationLaw.RentToday[c]` (tak licza juz `KingdomTreasury.cs:61-62` i `BuildFunding.cs:155`).
3. `:239`: `d.Trust = Math.Min(d.Trust, Math.Max(0.5f, d.Trust * 0.8f));`.
4. Petla pozyczek AI (`:203-213`): nie pozyczac rodowi z `Clan.DebtToKingdom > 0` (reparacje Bannerlord.Diplomacy zabieraja nazajutrz cala kiese razem z pozyczka - AUDYT, dopisek weryfikatora).

Osobny wpis R5b (ludzie, nie pieniadz): `WarLedger.DesertElitesFirst` (SRC `WarLedger.cs:113-139`) - ludzie odchodzacy z braku zoldu ida do puli wyrzutkow regionu
(tabela 5.4, wiersz "poczet") zamiast znikac. Zamyka ubytek ludzi w nicosc na sciezce, ktora dluznik przechodzi najczesciej; dotyczy takze partii gracza.
Partie nieumarlych `WarLedger` juz pomija (`:40`). Skutek uboczny: wiecej zolnierzy w pulach, wiec wiecej band - widac w "Wyrzutki: ... dezercja".

### 5.6 Wierzyciele (R7)

- **Skarbiec krolestwa:** rod wasalny (nie rod krola, nie najemnik), gdy `KingdomBudgetWallet - kwota >= CrownLoanReserve` (1 000 000); stopa `CrownLoanRate` (10%); wyplata `wallet -= kwota`.
  Dlug w NASZYM slowniku, nie w `Clan.DebtToKingdom` (gra sciaga go nazajutrz i zloto znika - AUDYT lordowie-kod/W1, weryfikator). Paczka "zold i skarbiec" ma pierwszenstwo do skarbca w wojnie.
- **Bogaty rod tego samego krolestwa:** bez wlasnego dlugu, `kiesa glowy >= zold x DebtLenderWageDays (180) + kwota`; stopa `HouseLoanRate` (20%); wyplata z kiesy glowy. Gracz jako wierzyciel - nie w v1.
- **Bank:** jak dzis. Kolejnosc: skarbiec -> rod -> Bank. Wierzyciel staly do splaty dlugu (jedno pole `Creditor`); dobieranie tylko u tego samego.

### 5.7 Stan do zapisu (rozszerzenie `arm_ironbank`, SRC `IronBank.cs:391-431`)

Do wiersza rodu (dzis 9 pol) dopisac na koncu: `;Creditor;Stage;StageDay;Frozen;SeizedAvg;SaleStep;LastSaleDay;SeizedSum` (`Creditor`: `B`, `K:<idKrolestwa>`, `C:<idRodu>`).
`Import`: stary wiersz (9 pol) -> `Creditor = B`, `Stage = Defaulted ? 3 : (Principal >= 1 ? 1 : 0)`, reszta 0. `Reset()` (`:40`) czysci tez migawke okna taboru i liczniki dnia.

### 5.8 Kolejnosc w dobie

1. `PopulationLaw.Daily` (SRC `ArmouryBehavior.cs:1175`): renty; zajecie renty wsi dluznikow w srodku.
2. W ciagu doby: powroty taborow - zajecie utargu (nasluchy).
3. `KingdomTreasury.Daily` (`:1183`): powinnosci od dochodu, ktory pan naprawde dostal.
4. `IronBank.Daily` (`:1186`): pozyczki (wybor wierzyciela) -> odsetki (nie w D3+) -> rata / zaleglosc / wejscie w D3 -> kiesa w D3 -> prognoza -> krok wyprzedazy -> zamkniecie splaconych -> linie logu.
5. `MoneyLedger.Daily` (`:1188`) widzi wynik: kasy wsi nizsze o rente jak dotad, odbiorca inny (Bank jest posiadaczem w "Pieniadz swiata" - `IronBank.CapitalNow`, `:62`).

### 5.9 Log, napisy w grze i test

```
IronBank: dzien N - nowe pozyczki 2 (41 200: skarbce 1, rody 0, Bank 1), splaty 14 (9 320), spoznienia 1, zajecia nowe 1; dluznikow 23 (w zajeciu 3), dlug razem 512 400, kapital Banku 4 871 000.
Dlugi: dzien N | szczeble: kredyt 19, zaleglosc 1, zajecie 3, wyprzedaz 0, poczet 0 | zajeto dzis 2 940 (renty wsi 1 310 z 9 wsi, utarg 1 120, kiesy 510) | wyprzedaz dzis: zbrojownia 0, konie 0, karawany 0, warsztaty 0
 | prognoza splaty w zajeciu: mediana 41 dni, najdluzej 118 (Errol) | wierzyciele: Bank 402 100, skarbce 88 000, rody 22 300 | umorzone dzis 0.
IronBank: ZAJECIE Errol - dlug 28 400 u Banku, 3 raty niezaplacone; wierzyciel bierze rente i utarg 2 wsi (Haystack, ...) do splaty; odsetki zamrozone.
```
R1 (na sucho, bez zmiany gry): "Dlugi: dzien N | zapas w dniach zoldu (partie + garnizony): ponizej 10: a, 10-20: b, 20-45: c, 45-90: d, ponad 90: e | limit zoldu BK: rody z kiesa ponizej 37 639: f, 37 639-112 917: g, partie z zoldem ponad limit: h | dochod wsi rodow: renta X, utarg Y".

Napisy dla gracza (po angielsku, `InformationManager.DisplayMessage`; do pliku osobno `Log.Info`):
- "Your creditors have taken the rents and dues of your villages until your debt of {N} is repaid."
- "Your debt is repaid. Your villages pay you again."
- menu Banku (`BankText`, `:317-333`): "The Bank holds the rents of your villages. {N} is still owed."

Test (bez czekania tygodniami): z konsoli albo tymczasowo `IronBankWageDays` 200 -> rody pozyczaja pierwszego dnia; potem zabrac rodowi zloto -> 3 doby spoznien -> linia "ZAJECIE";
nastepnej doby "Ludnosc: ... w tym zajete N" > 0 i "Przeplywy osad" bez zmiany po stronie wsi (kiesa wsi spada o te sama rente co zawsze, inny jest tylko odbiorca);
kiesa pana, ktora urosla ponad podloge (np. po dosypaniu z konsoli), wraca do `max(10 dni zoldu; 38 000)` w jedna dobe, a kiesa ponizej podlogi nie jest ruszana;
po splacie linia "splacil dlug w calosci" i renty wracaja.
Czego NIE oczekiwac: ze wojsko dluznika stoi w miejscu. Rod z kiesa pod 112 917 zl ma u BK obciety limit zoldu (pod 37 639 - ponizej 600 zl na partie) i traci ludzi dezercja
niezaleznie od Banku (5.1) - porownywac z sesja bazowa (linia R1 "limit zoldu BK"), nie z zerem. Sprawdzic za to, ze po R5b dezercje z braku zoldu widac w "Wyrzutki: ... dezercja", a nie tylko w "WarLedger:".
Rownanie: `dlug wczoraj + odsetki - (raty + zajecia + wyprzedaz) = dlug dzis` w kazdej dobie; "Pieniadz swiata" bez nowej pozycji "z niczego"
(suma zajec dnia = przyrost kapitalu Banku + skarbcow + kies wierzycieli z tego tytulu).

### 5.10 Ryzyko i kolizje

- Wies z tytulem BK u innego bohatera (`title.deJure != glowa rodu`, `SCR\ore-supply\EconomyPatches.cs:454-458`): BK placi utarg jemu - zajecie utargu pomijac, gdy BK `Village.GetActualOwner()`
  (refleksja) nie jest glowa rodu dluznika; renta `PopulationLaw` idzie zawsze do `st.OwnerClan` (bez zmian). Pelniej (nie w pierwszym wpisie): dla utargu dluznikiem jest rod
  bohatera, ktoremu BK naprawde wyplaca - wtedy zajecie obejmuje tez wsie, ktorych tytul trzyma rycerz rodu dluznika.
- Spalona wies nie placi renty (SRC `PopulationLaw.cs:297`) - ryzyko wierzyciela, jak w historii; prognoza splaty wydluza sie i moze uruchomic D4.
- Zajecie do Banku odklada zloto w kapitale, ktory niczego nie wydaje (znany odplyw: AUDYT lordowie-kod/W10) - widac w "Pieniadz swiata"; wydatki Banku to osobny temat.
- Odsetki zamrozone czynia zajecie tanim dla dluznika - kara to odciety kredyt, +15 pp na przyszlosc i wrogowie z limitem x1.5; alternatywa w pytaniu 8.
- Nie dublowac zdejmowania ludzi: D5 ma zastapic `WarLedger` dla dluznikow, nie dzialac obok (dwa prawa zdejmowalyby ludzi tego samego dnia w przeciwnej kolejnosci - AUDYT lordowie-historia, dopisek).
- 13 kluczy BetterEconomy przed R6: inaczej glowne ujscie kies (wplaty 12 000 / 42 000) wpycha rody w dlug, ktorego przyczyna nie jest zold.
- Progi BK (limit zoldu od kiesy 112 917 / 37 639) dzialaja wczesniej niz prog Banku - Bank moze dalej byc martwy; linia R1 pokaze rozklad i wtedy decyzja o progach w jednej skali (AUDYT, dopisek weryfikatora).
  Podloga kiesy 38 000 (5.3c) robi tylko jedno: zajecie nie schodzi z kiesa dluznika ponizej poziomu, przy ktorym BK placi jeszcze ok. 600 zl na partie. Nie naprawia tego, ze rod pozycza dopiero, gdy jest juz gleboko pod tym progiem
  (10 dni zoldu to dla rodu Strazy ok. 22 tys. - ANEKS 2.2: zold partii 10 909 zl dziennie na 5 rodow; dla innych rodow nie zmierzono) - prog pozyczki AI nalezaloby podniesc do `max(10 dni zoldu; 38 000)`, ale to zmienia liczbe pozyczek w calym swiecie:
  decyzja po pierwszym odczycie linii R1 "Dlugi:" (ile rodow siedzi pod progiem BK), nie teraz.
- Podloga 38 000 jest liczba z ustawien BK Jeffa (`BaseWage` 1.2546). Zmiana `BaseWage` przesuwa prog - zamiast stalej mozna czytac ustawienie BK refleksja
  (`GlobalSettings<BannerKingsSettings>.Instance.BaseWage`); w pierwszym wpisie stala z opisem w MCM.
- `RentToday` po zajeciu zawiera tylko czesc pana, wiec `IronBank.Limit` (po R5) i powinnosci wobec korony licza sie od mniejszego dochodu - dluznik placi koronie mniej;
  korona nie jest wierzycielem uprzywilejowanym. Jesli ma byc - osobna decyzja.

---

## 6. Wspolne

### 6.1 Ustawienia (`Settings.cs`; komentarz = opis w MCM, po angielsku; po zmianie `python tools/gen_mcm.py`)

```csharp
// --- Lands with their own law ---
public int FreeFolkPopulation = 300000;               // people of the free folk beyond the Wall (150 000 before); read when a campaign's lands are first counted
public float FreeHandsShare = 0.20f;                  // share of grown men a land can spare for war before its harvest suffers (historical estimate)
public float FreeFolkFreeHands = 1.0f;                // beyond the Wall every grown man and spearwife fights: the share of grown men the free folk can field
public string FreeFolkCultures = "freefolk";
// --- The Night's Watch ---
public bool WatchLawEnabled = true;                   // the Watch takes its men from the Seven Kingdoms: first outlaws who take the black, then common folk of every land by its numbers; the Gift only feeds it
public float WatchPoolSharePercent = 5f;              // percent of a region's outlaws who may take the black in a single day
public bool WatchDesertersGoNorth = true;             // a deserter from the Watch hides among the outlaws of the nearest region of the North, not beyond the Wall
public bool WatchRoutedWalkHome = true;               // men of the Watch routed in battle who do not turn outlaw walk back to the nearest castle of the Watch
public bool WatchDoesNotRaid = true;                  // the Watch holds the Wall: its rangers fight beyond it but do not burn villages
public string WatchKingdoms = "nightswatch";
public string WatchSourceCultures = "battania,river,reach,aserai,vlandia,vale,stormlands,sturgia,crownlands,dragonstone";
public string WatchDeserterCultures = "battania";
// --- Thralls of the Iron Islands ---
public bool ThrallLawEnabled = true;                  // the ironborn keep thralls: captives sold in their harbours work the fields and mines, so more free men can be spared for the longships
public float ThrallSeedPerThousand = 54f;             // thralls already on the islands when a campaign begins, per 1000 islanders (54 lets four free men in ten be spared)
public float ThrallYearlyLossPercent = 5f;            // thralls lost each year to death and flight
public float ThrallFreeHandsExtra = 0.20f;            // extra share of grown men the islands can spare when thralls do all the heavy work (on top of the usual share)
public bool ThrallTownPaysForCaptives = true;         // a captive sold on the islands is paid for from that settlement's purse, not out of thin air
public bool RaidCaptivesFromLedger = true;            // captives carried off in a raid are real villagers taken from the plundered village, and only reavers take them (replaces the Banner Kings raid captives of AI houses)
public float ThrallsPerRaider = 0.5f;                 // captives one raider can drive to the ships from a plundered village
public int ThrallCapPerRaid = 150;                    // most captives taken in a single raid
public float ThrallShareOfHarried = 0.5f;             // at most this share of the villagers a raid falls upon can be carried off
public string RaidCaptiveKingdoms = "sturgia";
public string ThrallCultures = "sturgia";
// --- The dead beyond the Wall ---
public bool UndeadLawEnabled = true;                  // the Others raise only the dead: men fallen beyond the Wall and at the Wall - no wights out of thin air
public bool UndeadNoFreeWights = true;                // ends the 100 wights Realm of Thrones gives every new band of the Others and the 2 it adds each day
public bool UndeadNoMapVolunteers = true;             // the Others get no volunteers on the march
public bool UndeadNeverDesertOrYield = true;          // wights do not desert, are not taken prisoner and never join the outlaws: a wight that falls is gone
public int UndeadStartBodies = 616;                   // the old dead lying beyond the Wall when a campaign begins - what the first bands of the Others rise from
public int UndeadBandBirthSize = 153;                 // most wights a new band of the Others can raise at its birth, if the dead are there
public int UndeadRaisePerDay = 2;                     // wights a band raises each day from the dead lying in the region it stands in
public float UndeadLivingBattleBodiesPercent = 50f;   // percent of the men killed when the living fight each other beyond the Wall whose bodies are left unburnt
public bool UndeadCaptivesRise = true;                // men the Others take alive after a victory are killed and rise as wights
// --- Debts and creditors ---
public bool DebtSeizureEnabled = true;                // a house that misses three instalments keeps its fiefs: its creditor takes the rents and dues of its villages until the debt is repaid
public float DebtSeizeVillagePercent = 100f;          // percent of a debtor's village rents and dues his creditor takes
public int DebtKeepWageDays = 10;                     // days of wages a debtor under seizure keeps in his purse; the creditor takes the rest
public int DebtPurseFloorGold = 38000;                // the creditor never takes a debtor's purse below this (at about 38 000 Banner Kings still lets each party draw some 600 a day in wages; a poorer house may pay less and its men desert)
public float DebtPursePercent = 100f;                 // percent of the purse above that reserve taken each day
public int DebtArrearsKeepDays = 3;                   // a debtor short of an instalment still pays whatever he has above this many days of wages
public bool DebtFreezeInterest = true;                // interest stops growing on the day the creditor takes the villages
public int DebtHorizonDays = 364;                     // if the seized income would not clear the debt within this many days, the debtor starts selling what he owns
public int DebtSaleEveryDays = 7;                     // days between one forced sale and the next
public int DebtWorkshopMinPrice = 1000;               // a workshop is not sold for less than this
public int DebtWriteOffDays = 364;                    // a house with no villages left to seize has the rest of its debt written off after this many days - the creditor's loss
public bool CrownLoansEnabled = true;                 // a realm's treasury lends to its vassals before they turn to the Iron Bank
public int CrownLoanReserve = 1000000;                // the treasury never lends below this
public float CrownLoanRate = 10f;                     // yearly interest on a loan from the crown (percent)
public bool HouseLoansEnabled = true;                 // a rich house of the same realm lends to a house in need
public int DebtLenderWageDays = 180;                  // a house lends only what it holds above this many days of its own wages
public float HouseLoanRate = 20f;                     // yearly interest on a loan from another house (percent)
```
Pola `string` nie pojawia sie w MCM (generator bierze tylko bool, int, float) - to celowe. Zadna wartosc domyslna nie jest zerem (pkt 0.1.10).
Linie `// --- ... ---` staja sie nazwami grup w MCM (generator, `tools\gen_mcm.py:8-11`). W opisach nie uzywac cudzyslowu `"` (generator zamienia go na apostrof).
Suwaki procentow siegaja 4 x wartosci domyslnej (np. `DebtSeizeVillagePercent` do 400) - kod przycina procenty do 0-100, a udzialy do 0-1.
Suwak `WatchPoolSharePercent` siega tylko 20 (4 x 5; `tools\gen_mcm.py:46`) - wiersze 25% i 100% tabeli 1.2 sa poza zasiegiem MCM (tylko plik ustawien).
Kazdy mechanizm ma wlacznik: `WatchLawEnabled` (+ trzy czastkowe), `ThrallLawEnabled` (+ `ThrallTownPaysForCaptives`, `RaidCaptivesFromLedger`), `UndeadLawEnabled` (+ trzy czastkowe),
`DebtSeizureEnabled`, `CrownLoansEnabled`, `HouseLoansEnabled`; tabela Wolnych Ludzi - liczba `FreeFolkPopulation`, prog - `FreeFolkFreeHands` (0.20 = jak reszta swiata).
Wlaczniki czastkowe a punkty regul: `WatchDesertersGoNorth` = regula 1 punkt 8; `WatchRoutedWalkHome` = regula 1 punkt 9; `WatchDoesNotRaid` = hamulec H1 (3.4);
`UndeadNoFreeWights` = regula 4 punkty 1-2; `UndeadNoMapVolunteers` = punkt 3; `UndeadNeverDesertOrYield` = punkty 4-6; `UndeadCaptivesRise` = punkty 7 i 9;
`DebtFreezeInterest` = odsetki w D3 (5.2). Wylaczenie `UndeadLawEnabled` przywraca calosc ROT (wszystkie prefiksy przepuszczaja, nasz nasluch bitew nic nie robi) -
pula cial zostaje w zapisie nietknieta.
Wlaczniki latek Harmony sa czytane w samej latce (nie przy wpinaniu), zeby zmiana w MCM dzialala bez restartu - jak w `OutlawLaw` (bramki sprawdzaja `On` przy kazdym wywolaniu).
Wartosci domyslne w `Settings.cs` nie zmienia tych, ktore Jeff ma juz w `Armoury.json` - nowych kluczy tam nie ma, wiec zadzialaja domyslne.

### 6.2 Jeden prog wolnych rak (zamiast stalej `FreeHands`, SRC `PeopleLedger.cs:32`)

```csharp
/// <summary>Prog wolnych rak regionu: jaka czesc mezczyzn 16-60 kraina moze oddac bez straty plonu; -1 = region poza progiem.</summary>
internal static float FreeHandsOf(Settlement region)
{
    string c = region != null && region.Culture != null ? region.Culture.StringId : "";
    if (WatchLaw.IsWatchCulture(c)) return -1f;                                   // Dar: ludzie tylko do plonu
    if (Helper.InList(Settings.Current.FreeFolkCultures, c)) return Settings.Current.FreeFolkFreeHands;   // walcza wszyscy dorosli (InList: nowa pomocnicza - lista id po przecinkach)
    if (ThrallLaw.IsThrallRegion(region)) return ThrallLaw.FreeHands(region);     // 20% + do 20% od niewolnych
    return Settings.Current.FreeHandsShare;
}
```
Uzycie: `PeopleLedger.cs:251-252` (licznik "ponad prog"), nowa kolumna `prog_proc` w `ludzie-regiony.csv` (na koncu - nie przesuwac istniejacych 28 kolumn), pozniej kroki 6b i 7.

### 6.3 Nowy stan i sprzatanie

| Klucz zapisu | Modul | `Reset()` w konstruktorze `ArmouryBehavior` (`:389`) |
|---|---|---|
| `arm_watch` | `WatchLaw` | tak |
| `arm_thralls` | `ThrallLaw` | tak |
| `arm_undead` | `UndeadLaw` | tak |
| `arm_ironbank` (rozszerzony) | `IronBank` | jest (`:40`) - dopisac nowe pola statyczne |
| `arm_population` (naglowek v2) | `PopulationLaw` | jest |

Wszystkie jako napisy `Export`/`Import` (wzor `ArmouryBehavior.cs:417-423`) - `SaveDefiner.cs` bez zmian (zadnego nowego typu kontenera).
Stan przejsciowy bez zapisu, czyszczony w `Reset()`: okno sprzedazy jencow i suma przelewow w oknie (`ThrallLaw`, `[ThreadStatic]`), migawka licznika podatku w oknie taboru (`IronBank`),
liczba wightow na poczatku bitwy (`UndeadLaw`, slownik po partii - wpis zdejmowany w `MapEventEnded`, reszta sprzatana raz na dobe), liczniki dnia wszystkich modulow.
Zapis w srodku okna nie jest mozliwy (okna zamykaja sie w tym samym wywolaniu gry), a utrata migawki bitwy po wczytaniu daje tylko jedna linie "bez zdarzenia".
Wyjatki: per obiekt, licznik potkniec w linii logu, jednorazowe `Log.Error` na modul; zadnego globalnego wylacznika.

### 6.4 Kolejnosc w ticku dobowym po wszystkich wpisach (SRC `ArmouryBehavior.cs:1154-1189`)

`... -> PopulationLaw.Daily (renty, zajecie) -> BuildFunding -> ... -> KingdomTreasury -> ThrallLaw.Daily -> WatchLaw.Daily -> OutlawLaw.Daily -> UndeadLaw.Daily -> IronBank.Daily -> SupplyDemand -> MoneyLedger.Daily -> PeopleLedger.Daily`
- kazde nowe wywolanie we wlasnym `try/catch` z `Log.Error` (wzor istniejacych linii).

---

## 7. Kontrola wedlug zasady 0 (wynik wlasnego sprawdzenia)

**Sprawdzone w kodzie tej nocy (nie przepisane z dokumentow):** caly `IronBank.cs`, `PopulationLaw.cs`, `OutlawLaw.cs`, `PeopleLedger.cs`, `Undead.cs`, `Levy.cs`, `LevyGold.cs`;
fragmenty `WarLedger.cs:26-140`, `DesertionLaw.cs:60-197`, `MenPurse.cs:268-312`, `AiGear.cs`, `KingdomTreasury.cs:44-135`, `MoneyLedger.cs:140-220`, `ArmouryBehavior.cs` (konstruktor, zapis, nasluchy, tick dobowy);
`CrashScribe\src\NightKingCall.cs` (calosc), `Fabula.cs:225-275`; ROT: `ROTOthersCampaignBehavior.cs` (rejestracja, tick, rajd, +100, +2, nekromancja), `ROTTroopRecruiter.cs`, `ROTStorylineWars.cs`,
`ROTStorylineWar.cs`, `ROTSettingsMCM.cs`, `ROTUtilities.cs`, `ROTSettlementGarrisonModel.cs`; gra: `RecruitmentCampaignBehavior.cs:215-675`, `GarrisonRecruitmentCampaignBehavior.cs`, `DefaultPartySizeLimitModel.cs:427-464`,
`MapEvent.cs:1529-1593, 1955-2046`, `SellPrisonersAction.cs`, `PartiesSellPrisonerCampaignBehavior.cs`, `PartiesSellLootCampaignBehavior.cs`, `ChangeOwnerOfWorkshopAction.cs`, `AiMilitaryBehavior.cs:405-485`, `PartyBase.cs:343-355`,
`BattleRewardModel.cs`; BK: `BKRaidCaptureModel.cs`, `BKRaidCaptureBehavior.cs`, `RaidCapturePolicyManager.cs`, `DefaultDemesneLaws.cs:128-178`, `SettlementPatches.cs:21-90`, `EconomyPatches.cs:400-485`,
`VanillaModelTweakPatches.cs:885-916, 1738-1762`; pliki gry: `ROT_spkingdoms.xml`, `partyTemplatesROT.xml`, `BannerKings.json`; `tools\gen_mcm.py`.
Z logu policzone na nowo: 490 linii "Bitwa:", 53 starcia "Raid" wedlug napastnika i ofiary, 20 starc Zelaznych Wysp, linie l.159, l.1776, l.1795, l.1814.

**Sprawdzone ponownie w wersji 2 (06.10):** kazde odwolanie plik:linia tego dokumentu do `IronBank.cs`, `OutlawLaw.cs`, `PopulationLaw.cs`, `ArmouryBehavior.cs`, `MoneyLedger.cs`, `PeopleLedger.cs`,
`KingdomTreasury.cs`, `BuildFunding.cs`, `WarLedger.cs`, `CampFever.cs`, `DesertionLaw.cs`, `Levy.cs`, `LevyGold.cs`, `MaterialLaw.cs`, `MenPurse.cs`, `Log.cs`, `BattleChronicle.cs`, `SubModuleMain.cs`,
`Stables.cs`, `Undead.cs`, `Settings.cs`, `tools\gen_mcm.py`, CS `NightKingCall.cs`, `Config.cs`, `Fabula.cs` - zgodne; ROT, gra i BK w miejscach, na ktorych stoja reguly - zgodne poza punktami z listy
"Poprawione w wersji 2". Doczytane: TW `EnterSettlementAction.cs`, `RecruitPrisonersCampaignBehavior.cs`, `DesertionCampaignBehavior.cs`, `DefaultPartyDesertionModel.cs`, `MapEventParty.cs`, `PartyThinkParams.cs`,
`AiPartyThinkBehavior.cs`, `AiMilitaryBehavior.cs:455-485`, `GiveGoldAction.cs`, `DefaultSettlementValueModel.cs:161-201`, `LordPartyComponent.cs`, `ClanVariablesCampaignBehavior.cs:389-414`; ROT `AIThinkPatch.cs`;
BK `PolicyManager.cs`, `BKCriminalPolicy.cs`, `EstateData.cs`, `BKTaxModel.cs:238-340`, `EconomyPatches.cs:100-160, 1056-1119`, `BKVillageProductionModel.cs`, `LandData.cs`; BKROTPatch `BKROTBehavior.cs`;
`RealisticCaptivity\src\FairRansom.cs` (calosc); `ROT-Map\ModuleData\settlements.xml` (osady i wlasciciele wysp, Strazy, Wolnych Ludzi), `ROT-Content\ModuleData\spkingdoms.xslt`, `BannerKings.json`.
Rachunek `rachunek2.py` uruchomiony ponownie - wynik identyczny z `rachunek2_out.txt`; liczby z BIT przeliczone niezaleznym skryptem (`_recenzja\spr_bitwy.py`) - zgodne co do sztuki
(490 linii, 53 rabunki, 10 rabunkow Strazy: 7 wygranych, 3 nierozstrzygniete, 186 zabitych obroncow; wyspy 20 starc, 73 rannych wrogow w 18 wygranych; Wolni Ludzie 1 173 zabitych: Straz 777, Inni 385).

**Regresje (kto czyta i pisze ten sam stan):**
- `RentToday` czytaja `KingdomTreasury.Daily` (`:61`), `BuildFunding` (`:155`) i (po R5) `IronBank.Limit` - po zajeciu zawiera tylko czesc pana: powinnosci i budowy dluznika maleja (zamierzone), limit kredytu tez (zamierzone: i tak kredyt odciety).
- Pule wyrzutkow czytaja bramki band (`GateClan`, `GateGlobal`, `GateHideout`) - nabor Strazy zmniejsza pule w Westeros, wiec czesciej "odmowione" (zamierzone, skala w 1.2).
- `OutlawLaw.RosterPostfix` dostaje druga galaz (nieumarli) - warunek `_depth > 1` zostaje pierwszy, zeby podmiana byla raz na lancuch modeli.
- Migracja `arm_population` v1 -> v2 mnozy tylko `freefolk` i tylko raz; stare wersje DLL czytajace "v2" pomina naglowek tak samo jak "v1" (`Import` nie sprawdza jego tresci, `:349`).

**Kolizje miedzy regulami i z innymi paczkami:** wight nie trafia do pul (4) -> nie moze przywdziac czerni (1) ani zostac niewolnym (2); brat Strazy sprzedany na wyspach: `WatchLaw` zdejmuje go z pochodzenia
jako "sprzedany z niewoli", `ThrallLaw` dopisuje niewolnego - jeden czlowiek, dwie pozycje po przeciwnych stronach (bez podwojenia); H1 (Straz nie rabuje) zmniejsza liczbe cial dla Innych (4) i strat Strazy (1);
"paser", "zold i skarbiec" i "karawany" dotykaja `OutlawLaw.cs`, `IronBank.cs` / `KingdomTreasury.cs`, `ArmouryBehavior.cs`, `Settings.cs` - same wstawki, scalac recznie; regula 5 zaklada, ze paczka "zold" nie zmienia `MobileParty.TotalWage`.
Cele latane przez wiecej niz jedna latke (kolejnosc bez znaczenia - sprawdzone w kodzie kazdej z nich): `SellPrisonersAction.ApplyInternal` (BK: dwa prefiksy przepuszczajace na metodach publicznych,
`RealisticCaptivity`: prefiks, postfiks, finalizator, nasz: prefiks i finalizator - same znaczniki okna); `RecruitmentCampaignBehavior.ApplyInternal` (nasz postfiks `LevyGold` + nowy prefiks dla Innych -
postfiks biegnie takze po pominieciu metody i dla "VolunteerFromMap" nic nie robi); `CampaignEventDispatcher.AiHourlyTick` (postfiks ROT + nasz); `FindAppropriateInitialRosterForMobileParty`
(jedna nasza latka `OutlawLaw` z dwiema galeziami: bandy i Inni). Zdarzenie `HeroOrPartyTradedGold` ma po wersji 2 trzech naszych sluchaczy (`MoneyLedger`, `ThrallLaw`, `IronBank`) - kazdy
dziala tylko we wlasnym oknie; okna zajecia i sprzedazy nie nachodza na siebie (jency sa sprzedawani przy wjezdzie do warowni albo z lochu osady, okno zajecia utargu otwiera sie
tylko przy wjezdzie wiesniakow do wlasnej wsi).

**Zamknieta ekonomia:** kazdy przeplyw ma platnika i odbiorce: zajecie (kiesa wsi / licznik podatku -> wierzyciel), pozyczki (skarbiec / kiesa rodu / kapital Banku -> glowa rodu), wyprzedaz (kasa miasta albo zloto notabla -> wierzyciel),
niewolni (kasa osady -> sprzedajacy zamiast zlota z niczego), rekrut Strazy (pula albo wies Westeros -> Straz), cialo (polegly -> pula cial -> wight -> zniszczony). Bilanse otwarcia sa trzy i jawne: pochodzenie Strazy,
27 000 niewolnych, 616 cial. Znane dziury, ktorych projekt NIE zamyka albo zamyka dopiero w R13: kapital Banku (rosnie i nic nie wydaje); zloto za jencow poza wyspami, za jencow sprzedanych
razem z bohaterem i dla gracza (z niczego - temat ksiegi pieniadza); jency-chlopi BK dla rodow z miastem w calym swiecie (ludzie z niczego - do R13 tylko liczeni, potem przejeci);
jency wcielani do partii lordow (czlowiek zmienia strone bez sladu w ksiedze - krok 5 demografii); ochotnicy "z mapy" rodow mniejszych innych niz Inni (z niczego, zloto wodza znika -
STAN-PRAC, drobne); podatek klas ludnosci wsi BK z urzedem podatkowym (z niczego); zold Strazy (pytanie 4).

**Cudzy kod, na ktorym projekt sie opiera - stan sprawdzenia po wersji 2:**
- sprawdzone w kodzie gry (bylo "przyjete za wpisem 102"): okno `Before/AfterSettlementEntered` obejmuje caly podzial utargu (`EnterSettlementAction.cs:26-28` + latka BK na nasluchu `OnSettlementEntered`);
- sprawdzone proba poza gra [PROBA]: prefiks na prywatnych nasluchach ROT i na `ApplyInternal` werbunku dziala; wszystkie latane metody istnieja w jednej sygnaturze; `NavalDLC` ma wlasny model lupu;
- sprawdzone przeszukaniem bibliotek modow (nazwy typow w DLL): model lupu maja gra i `NavalDLC` (BK, BKROTPatch i DTE tylko sie do niego odwoluja); model celu maja gra, `NavalDLC`
  i `StrategicCampaignAI145`, BK go latka; modelu dezercji nie ma zaden mod poza Armoury; `SellPrisonersAction` dotykaja BK i `RealisticCaptivity`; zdarzenia `RaidCompleted` sluchaja
  BK (jency), BKROTPatch (tylko renoma i poboznosc, `SCR\ore-supply\bkrot\BKROTPatch.Behaviors\BKROTBehavior.cs:78-113`), ROT (Inni), `BetterEconomy`, `NavalDLC` i `Bannerlord.Diplomacy` (trzech ostatnich nie czytano);
- przeczytane w wersji 2 (bylo "nie czytane"): `RecruitPrisonersCampaignBehavior` - partie lordow AI wcielaja jencow;
- NADAL bez proby: zachowanie w kampanii (kolejnosc nasluchow tego samego zdarzenia miedzy modami, liczby), API rozwiazania karawany i `WorkshopModel.GetCostForNotable` dla AI (z sygnatur),
  nasluchy `RaidCompleted` w `BetterEconomy`, `NavalDLC` i `Bannerlord.Diplomacy`, czy BK przenosi klase "Slaves" z warowni do wsi.

**Poprawione w wersji 2 (wzgledem wersji 1 tego dokumentu):**
1. BK a jency z rabunku (pkt 0.1.3, regula 2): wersja 1 - "Zelazne Wyspy nie biora jencow"; kod - polityka karna "Enslavement" jest domyslna dla kazdej osady, wiec jencow z niczego dostaje
   kazdy rod AI z miastem jako siedziba - wlasnym albo, dla rodow bez ziemi, miastem krolestwa (na wyspach wszyscy poza 4 rodami zamkowymi). Regula 2 punkt 5 przepisana:
   przejecie metody BK jedna latka zamiast drugiego zrodla jencow obok BK.
2. Zaplata za sprzedanego jenca (regula 2 punkt 4): wersja 1 liczyla cene na nowo w nasluchu; kod - akcja gry przy bohaterze oddawanym do lochu nie wyplaca nic, a cena BK zalezy
   od stanu po sprzedazy. Teraz kwota jest mierzona w oknie akcji, a korekta tylko dla partii jencow bez bohaterow.
3. Hamulec "Straz nie rabuje" (regula 3, H1): wersja 1 zerowala wynik modelu celu; w grze sa trzy modele celu i latka BK, a zero moze zostac wybrane. Teraz wynik -1 w miejscu,
   w ktorym ROT wycina Strazy oblezenia.
4. Ochotnicy z mapy dla Innych (regula 4 punkt 3): latka przeniesiona z 14-bajtowej metody na `ApplyInternal`.
5. Dezercja wightow (regula 4 punkt 5): latka na zachowanie gry zamiast zmiany w naszym modelu, ktory bywa nieczynny.
6. Odbici jency Innych (regula 4 punkt 7): bez latki na model lupu - w chwili `MapEventEnded` stoja w skladzie bandy w zywym typie i mozna ich zdjac wprost.
7. Dlug (regula 5): doszla podloga kiesy 38 000 (zajecie do 10 dni zoldu spychalo dluznika pod prog limitu zoldu BK), szczebel "rozpuszczenie pocztu" opisany zgodnie z kodem
   (poczet topnieje sam od progu BK, drabina tylko porzadkuje, dokad ida ludzie), wiarygodnosc po splacie 0.25 jak w kodzie (wersja 1: 0.5), zajecie obejmuje tez wyplaty majatkow BK
   dla rodu dluznika, a test nie obiecuje juz "wojska bez skoku w dol".
Drobne: spis braci w niewoli tylko u stron bedacych w wojnie ze Straza; Straz = rody krolestwa bez najemnych; definicja "zniszczonych" wightow obejmuje rozbitych i pozostalych po klesce.

**Poprawki recenzenta (wersja 2.1, 06.10; niezalezne sprawdzenie wersji 2 w zrodle):**
Sprawdzone ponownie i zgodne: wszystkie odwolania plik:linia do `IronBank.cs`, `PopulationLaw.cs`, `OutlawLaw.cs`, `ArmouryBehavior.cs`, `MoneyLedger.cs`, `PeopleLedger.cs`, `WarLedger.cs`,
`CampFever.cs`, `LevyGold.cs`, `DesertionLaw.cs`, `Undead.cs`, `KingdomTreasury.cs`, `BuildFunding.cs`, `MenPurse.cs`, `Stables.cs`, `MaterialLaw.cs`, `Levy.cs`, `Log.cs`, `BattleChronicle.cs`,
`SubModuleMain.cs`, CS `NightKingCall.cs`, `Config.cs`, `Fabula.cs`, `RealisticCaptivity\src\FairRansom.cs`, `tools\gen_mcm.py`; w dekompilacjach: BK (rabunek, polityka karna, prawa niewolnictwa,
limit zoldu, utarg, majatki, podatek wsi, sprzedaz jencow), ROT (`ROTOthersCampaignBehavior`, `ROTTroopRecruiter`, `AIThinkPatch`, `ROTStorylineWars`, ustawienia), gra (`SellPrisonersAction`,
`PartiesSellPrisonerCampaignBehavior`, `RecruitPrisonersCampaignBehavior`, `RecruitmentCampaignBehavior`, `GarrisonRecruitmentCampaignBehavior`, `MapEvent`, `MapEventParty`, `EnterSettlementAction`,
`GiveGoldAction`, `DefaultSettlementValueModel`, `Clan` (siedziba rodu liczona na nowo przy zmianach), `PartyThinkParams`, `AiPartyThinkBehavior`, `AiMilitaryBehavior`, dezercja, model lupu, warsztaty);
`settlements.xml` (wyspy 6 miast, 4 zamki, 28 wsi; Straz 1 / 4 / 11; Wolni Ludzie 3 / 4 / 18; Westeros 135 z 227 warowni, 60.2% hearth), `ROT_spkingdoms.xml`, `spkingdoms.xslt`,
`partyTemplatesROT.xml` (54 i 53), `BannerKings.json`, log l.159, l.1776, l.1795, l.1814; 49 nowych ustawien bez kolizji nazw z `Settings.cs` repo i paczek nocy (karawany, zapas, mineral, paser, zold).
Na kopiach (`_recenzja2\`): `rachunek2.py` - wynik identyczny; `spr_bitwy.py` - 490 linii, 53 rabunki, 10 Strazy (7 wygranych, 3 bez rozstrzygniecia, 186 zabitych obroncow), wyspy 20 starc i 73 rannych;
proba poza gra uruchomiona ponownie - wynik identyczny z `proba\run.log`. Przeszukanie bibliotek modow: model lupu tylko gra i `NavalDLC`, modelu dezercji nie ma zaden mod poza Armoury.
1. Limit zoldu BK (pkt 0.1.11, 5.1, 5.2, 5.3c, 5.9, 5.10, opis `DebtPurseFloorGold`, pytanie 7): wersja 2 opisywala 37 639 zl jako prog, pod ktorym BK "tnie limit", i podloge 38 000 jako
   ochrone przed cieciem. Kod (`EconomyPatches.cs:123-155`): limit jest ciagla funkcja kiesy - 600-1 200 zl miedzy 37 639 a 112 917, 200-600 ponizej, bez skoku. Podloga 38 000 zostawia partii
   ok. 600 zl zoldu dziennie (ok. 135 ludzi) zamiast ok. 200-270 zl; nie znosi dezercji. Opisy, test, log R1 i pytanie 7 przepisane.
2. Spis Strazy w trakcie bitwy (1.3 punkty 1 i 3, 1.5, 1.6; to samo w 4.5): zabici i rozbici schodza ze skladu w trakcie bitwy (`MapEventParty.cs:298-323`), a wersja 2 ksiegowala ich
   w `MapEventEnded`. Bitwa trwajaca przez tick dobowy dalaby "bez zdarzenia", a nazajutrz falszywy nabor pokrywany z Westeros drugi raz. Spis dolicza teraz straty bitew w toku.
3. R2 bez R4 (0.3, 4.2 punkt 1): wylaczenie +100 ROT w nowej kampanii przed pula cial zostawialoby Innym 216 trupow zamiast 616. Dodany bilans otwarcia pierwszych dwoch dob kampanii do czasu R4.
4. Sprzedaz warsztatu (5.4): `ApplyByBankruptcy` ustawia kapital warsztatu na 10 000 - zloto z niczego albo w nicosc. Zamieniona na `ApplyByDeath` (kapital i rodzaj zostaja). Nadal bez proby w grze.
5. Rozbici Strazy (1.3 punkt 9): do garnizonu tylko do wolnego miejsca - ponad limit gra i tak zdejmuje ich dezercja.
6. Kolejnosc (0.3): STAN-PRAC zapisuje nieumarlych "po ksiedze ludzi" - odstepstwo opisane jawnie, z wariantem doslownym.
7. Liczba (2.2): bez doplywu jencow prog wysp spada o ok. 1 punkt rocznie (bylo: "na 1.5 roku"); z samymi jencami z bitew o 1 punkt na 4 lata.
Drobne: zaplata za jenca sumuje tylko przelewy do odbiorcy akcji (2.3 punkt 4); bezpiecznik na dwie rowne pozycje rabunku w H1 (3.5); kultura jenca-chlopa BK to kultura ludnosci wsi wedlug BK,
nie zawsze kultura wsi (2.1); log kalibracji pokaze 41-43, nie zawsze 42 (3.5); `GarrisonParty` bywa puste (5.5); suwak `WatchPoolSharePercent` siega 20 (6.1); liczniki dnia Strazy w zapisie (1.5);
BK daje "prawo" do jencow z rabunku takze rodowi Innych (4.6); dla utargu wsi z tytulem BK u rycerza rodu - wariant pelniejszy (5.10).
Czego recenzent NIE sprawdzil: niczego w grze (jak autor); BetterEconomy, NavalDLC i Bannerlord.Diplomacy na zdarzeniu `RaidCompleted`; API karawan BK; liczb z `m1.json` poza udzialem regionow i hearth.

**Poprawione w toku tej kontroli (wzgledem pierwszej wersji rachunku tej paczki):** (1) Wolni Ludzie - pierwsza wersja zdejmowala poleglych z calej ludnosci i wychodzilo "nabor nie staje w 5 lat";
polegli to walczacy dorosli, wiec schodza z puli: nabor staje po 3.5-5.4 roku (ta sama metoda dla stanu dzisiejszego daje 1.1-1.7 roku = dni 411-622 z ANEKS 2.4). (2) Dlug - pierwsza wersja brala 45% rent swiata jako renty wsi;
wedlug fundamentu P4 wsie to 80-97 tys. z 342 tys. (23-28%), a "45%" to udzial wsi w calym dochodzie z ziemi; dochod wsi podany teraz widelkami 300-453 zl. (3) Odbici jency Innych przeniesieni do etapu z pula cial.
(4) Numery linii `KingdomTreasury`, `EconomyPatches` i BK sprawdzone ponownie.

**Czego nie wiadomo (liczby):** podzial puli wyrzutkow na regiony i udzial Westeros (60% to szacunek); udzial kanalow naboru Strazy; ile jencow naprawde dowoza partie Zelaznych Wysp; czy rody AI maja warsztaty i karawany;
wszystkie przeliczenia roczne pochodza z 28 dob poczatku kampanii, w ktorych wojna powszechna trwala 6 dob. Hamulec H2 ("zabojstwa Innych x0.4") to zalozenie bez pomiaru.

---

## 8. Zrodla

- Decyzje i badania: `docs\STAN-PRAC.md` (linie 66-115); `docs\DEMOGRAFIA-ANEKS-2026-10-05.md` (rozdz. 1.4, 2, 4, 5.2, 6); `docs\DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md` (rozdz. 4, 5);
  `docs\AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md` (odcinki lordowie-kod W1-W10, lordowie-historia L0, S1-S7, K1, G1); `docs\EKONOMIA-FUNDAMENT-2026-10-05.md` (P4-P6); `CHANGELOG.md` (wpisy 98-102);
  wynik sprawdzenia nieumarlych: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\tasks\w781xei9u.output`.
- Kod Armoury (SRC): `IronBank.cs`, `PopulationLaw.cs`, `OutlawLaw.cs`, `PeopleLedger.cs`, `MoneyLedger.cs`, `Undead.cs`, `DesertionLaw.cs`, `WarLedger.cs`, `MenPurse.cs`, `AiGear.cs`, `Stables.cs`, `KingdomTreasury.cs`,
  `Levy.cs`, `LevyGold.cs`, `CampFever.cs`, `MaterialLaw.cs`, `BattleChronicle.cs`, `ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs`, `SaveDefiner.cs`, `Log.cs`; CrashScribe: `NightKingCall.cs`, `Config.cs`, `Fabula.cs`; `tools\gen_mcm.py`.
- Dekompilacje: TW, ROT, BK jak w oznaczeniach; `SCR\ore-supply\EconomyPatches.cs`.
- Logi i dane: LOG, BIT, `SCR\aneks-male-krainy\m1.json`, `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\BannerKings\BannerKings.json`,
  `...\Modules\ROT-Content\ModuleData\ROT_spkingdoms.xml`, `partyTemplatesROT.xml`.
- Rachunki: `SCR\noc-2\reguly\rachunek2.py`, `rachunek2_out.txt`.
- Wersja 2: proba poza gra `SCR\noc-2\reguly\proba\Program.cs`, `proba.csproj`, `run.log`, `build.log`; niezalezne przeliczenie bitew `SCR\noc-2\reguly\_recenzja\spr_bitwy.py`;
  `...\Modules\ROT-Map\ModuleData\settlements.xml`, `...\Modules\ROT-Content\ModuleData\spkingdoms.xslt`; dekompilacje `SCR\ore-supply\bkrot` (BKROTPatch), `SCR\nieumarli-zrodla`
  (StrategicCampaignAI145), `SCR\audyt-lordowie-kod` (dezercja gry); lista dodatkowo przeczytanych plikow - rozdz. 7.
- Wersja 2.1 (recenzja): kopie i wyniki w `SCR\noc-2\reguly\_recenzja2\` (dokument i wpis sprzed poprawek, kopie logow, wynik `rachunek2.py`, ponowna proba `proba_run2.log`,
  lista nowych ustawien); skrypty poprawek `poprawki-recenzenta-v21.py`, `-v21b.py`, `-v21c.py`, `-v21d.py`, `poprawki-recenzenta-changelog.py` (jednorazowe - nie uruchamiac ponownie);
  log CrashScribe `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\session-2026-10-05_15-22-28.log` (narodziny band Innych); doczytane dekompilacje:
  `SCR\audyt-lordowie-kod\van_DefaultPartyDesertionModel.cs`, TW `MapEvents\MapEventParty.cs`, `Workshop.cs`, `DefaultWorkshopModel.cs`, `AIBehaviorData.cs`, BK `BKWorkshopModel.cs`.

---

## 9. Pytania do Jeffa (8) z rekomendacja

1. **Nieumarli - czy jency Innych wstaja jako trupy?** Dzis ROT po kazdej bitwie kasuje bandzie caly loch (w 5 sesjach do 1 243 rannych jencow) i podnosi trupy wedlug wlasnego wzoru,
   a odbici jency pokonanego wchodza do bandy zywi i nazajutrz sa wightami. **Rekomendacja: TAK** - Inni nie biora jencow: pojmany i odbity jeniec to polegly swojego regionu i cialo
   dla Innych (tylko za Murem i przy Murze). Odpowiedz "nie" oznacza, ze tych ludzi trzeba wypuscic (uciekaja do puli wyrzutkow regionu bitwy) - Inni rosna wtedy tylko z zabitych
   (w 5 sesjach ok. 15 dziennie zamiast ok. 31).
2. **Nieumarli - ciala z bitew zywych z zywymi za Murem (Straz - Wolni Ludzie, ok. 53 zabitych dziennie) i 616 trupow startowych.** **Rekomendacja:** 50% cial zostaje niespalonych (pokretlo;
   100% = ok. 19 tys. cial rocznie, wiecej niz Inni udzwigna; 0% = Inni maja tylko to, co sami zabija) oraz 616 jako "dawni umarli" w bilansie otwarcia (bez nich fabula ROT nie ma z czego ruszyc).
   Do tego kolejnosc: w STAN-PRAC zmiany u nieumarlych stoja jako "osobny watek, po ksiedze ludzi" - **rekomendacja:** nie czekac; zamkniecie zrodel z niczego i Zew (R2, R3)
   ksiegi nie potrzebuja, a pula cial (R4) ma wlasny stan i dopiero po kroku 5 zacznie przypisywac ciala regionom (szczegoly i wariant doslowny: rozdz. 0.3).
3. **Nocna Straz - ilu wyrzutkow dziennie moze przywdziac czern?** Kazdy wziety to o jednego bandyte mniej w Westeros. **Rekomendacja: 5% puli regionu dziennie** (ok. 6 100 rocznie = 2/3 potrzeby
   przy spokojniejszej wojnie; doplyw band w Westeros -28%); reszta z ludnosci bez limitu (do 0.14% mezczyzn Westeros rocznie).
4. **Nocna Straz - zold.** Straz ma 698 zl rent dziennie przy zoldzie ok. 14.4 tys.: kiesa wodza pustoszeje po ok. 13 dniach, a po regule 5 renty Daru beda stale zajete (nowy dlug zaraz po splacie starego).
   **Rekomendacja:** bracia sluza bez zoldu (zold oddzialow Strazy = 0, zostaje wyzywienie i sprzet) - zgodnie z kanonem; osobny maly wpis przed R6. To samo pytanie w mniejszej skali dotyczy Wolnych Ludzi (1 620 wobec 11.4 tys.).
5. **Wolni Ludzie - ktory hamulec?** Sama decyzja (300 tys., wszyscy dorosli) odsuwa koniec naboru z 1.1-1.7 do 3.5-5.4 roku. **Rekomendacja: H1 (Straz nie rabuje wsi za Murem) + H2 (Inni tylko z poleglych)**
   -> 5.3-8.3 roku, potem pomiar; "przegrani uchodza, zamiast ginac" (H3, caly swiat) jako osobna decyzja; wojny falami (H4) nie ruszac.
6. **Jency z rabunku - kto ich bierze?** Dzis BannerKings daje ich z niczego (do 150 chlopow na rabunek, wies nikogo nie traci) kazdemu rodowi AI, ktorego siedziba jest miasto
   (rody z wlasnym miastem i rody bez ziemi) - w calym swiecie, takze rodowi Castle Black z Nocnej Strazy; na Zelaznych Wyspach wszystkim poza 4 rodami zamkowymi. **Rekomendacja:** po ksiedze ludzi (kroki 2 i 4) jedna regula: jencow biora tylko Zelazne Wyspy, sa to prawdziwi
   ludzie zdjeci z napadnietej wsi, reszta swiata nie bierze nikogo; do tego czasu BK bez zmian, tylko liczymy. Do decyzji: czy na liscie maja byc tez Dorne i Lorath (BK daje im jencow
   prawem krolestwa) - rekomendacja: Dorne nie (Siedem Krolestw niewolnictwa nie zna), Lorath wedlug Twojego uznania. Osobno bilans otwarcia: 27 000 niewolnych na starcie (prog 40% od pierwszego dnia;
   bez tego prog rosnie z 20% o ok. 0.6 punktu rocznie) i ubytek 5% rocznie - rekomendacja: tak.
7. **Dlug - co zajmuje wierzyciel i ile zostawia?** **Rekomendacja:** 100% renty i utargu WSI; z kiesy tylko to, co lezy ponad 38 000 zl (albo ponad 10 dni zoldu, gdy to wiecej);
   renty miast zostaja panu na zold. Powod podlogi: BannerKings ogranicza zold, ktory rod moze wyplacic partii, wedlug kiesy glowy rodu (pelny dopiero ponad ok. 113 000 zl;
   przy 38 000 ok. 600 zl dziennie na partie = ok. 135 ludzi, przy pustej kiesie 200 zl = ok. 45 ludzi; ponad limit ludzie dezerteruja). Zajecie kiesy az do 10 dni zoldu
   zostawialoby dluznikowi poczty po ok. 45-60 ludzi, choc wierzyciel i tak odzyskuje dlug z wsi w 1-4 miesiace. Podloga nie chroni wojska calkiem - wybiera jego wielkosc:
   38 000 -> ok. 135 ludzi na partie, 75 000 -> ok. 200, 113 000 -> bez strat (SZACUNEK). Alternatywy: (a) bez podlogi - dluznik zostaje z garstka ludzi ("splaca z tego, co ma"
   doslownie); (b) takze renty miast - splata 2-4 razy szybsza (SZACUNEK: miasta to 72-77% rent), ale rod z miastem nie ma z czego placic zoldu.
8. **Dlug - odsetki i koniec dla rodu bez wsi.** **Rekomendacja:** odsetki zamrozone w dniu zajecia (dlug na pewno sie konczy) i umorzenie reszty po 364 dniach dla rodu, ktory nie ma juz wsi
   ani majatku (strata wierzyciela, wiarygodnosc 0 przez kolejny rok). Alternatywa: odsetki rosna dalej - wtedy maly rod z jedna spalona wsia moze nie wyjsc z dlugu nigdy.
