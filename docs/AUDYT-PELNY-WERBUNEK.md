# AUDYT PELNY: WERBUNEK, LUDNOSC, WYRZUTKI i systemy z 04.10 (tylko odczyt)

Data: 2026-10-04. Zakres: Levy, VolunteerKit, LevyGold, BK manpower (DeduceManpower), garnizon, demobilizacja,
werbunek w miastach, OutlawLaw, PopulationLaw, Calendar, WesterosClimate, Fabula x4.33, StartKit,
KingdomTreasury, IronBank. Zrodla: nasz kod (`Armoury/src`, `CrashScribe/src`) i dekompilacja gry/modow
(tw = TaleWorlds 1.4.8, bk = BannerKings.Redux, be = BetterEconomy, dte = DynamicTroopEquipmentReupload,
rot = ROT). **Nic z dzisiejszych zmian nie bylo jeszcze uruchomione w grze** - wszystkie oceny dzialania
liczbowego sa do potwierdzenia w logu.

Skroty: W/S/N = numery z `docs/AUDYT-PONOWNY.md`.

---

## 0. TL;DR

1. **Krytycznych crashy nie znalazlem.** Wszystkie latki wpinaja sie w istniejace metody z pasujacymi
   parametrami (sprawdzone w dekompilacji), refleksja BK ma bezpieczne fallbacki, transpilery klimatu i skarbca
   sa typowo poprawne. Najwieksze ryzyko crasha to nadal **puste bandy** (S1, otwarte).
2. **W2, W3, W4 z poprzedniego audytu sa NADAL OTWARTE** - kod `VolunteerKit.cs` i `LevyGold.cs` sie nie zmienil.
   W1, W5, W6 zamkniete (wpisy 35, 36), ale W6 ma nowa dziure (H-M4: dobieranie po terminie).
3. **Trzy ludnosci, nie dwie.** Nasza symboliczna (PopulationLaw, hearth x k), BK (pobor) i hearth vanilli
   (wyrzutki). BEE liczy produkcje wsi jako `chlopi BK / (hearth x stala)` - wiec gdy wyrzutki zabieraja hearth,
   **produkcja wsi ROSNIE** (odwrotny efekt).
4. **Podwojny hamulec ochotnikow:** SlowMuster (25%) x Levy (0.05-1.5) x BK. W spokojnej krainie to ~1 ochotnik
   na notabla na miesiac, a ta sama szansa bramkuje awanse - VolunteerKit prawie nie bedzie mial czego kupowac.
   Wojsko AI przejdzie na zrodla z niczego: najemnicy z karczmy, przyrost garnizonu, startowe sklady partii.
5. Garnizon (auto-werbunek i "base change") dalej omija pule BK i nie placi notablowi.

---

## 1. Status ustalen poprzedniego audytu

| Nr | Temat | Status | Dowod |
|---|---|---|---|
| W1 | Okup przez ekran druzyny | ZAMKNIETE (wpis 35) | - |
| W2 | VolunteerKit: kupione sztuki znikaja, DTE daje komplet z niczego | **OTWARTE** | `VolunteerKit.cs:160` (`AddToCounts(e,-1)` bez odlozenia), `AiGear.cs:69-75` + `Settings.cs:367` (`AiRecruitsBringKit = true`) - DTE `OnTroopRecruited` (dte `EveryoneCampaignBehavior.cs:870-895`) dalej daje komplet |
| W3 | VolunteerKit nie widzi czesci awansow | **OTWARTE** | `VolunteerKit.cs:66-82` (roznica multizbiorow bez zmian), `:178` (tylko vanilla) - BK `BKNotableBehavior.HandleCastles/UpdateVolunteers` (bk :157-283) nielatane |
| W4 | Bez kompletu nowej partii = przeniesienie darmowego sprzetu na czas bitwy | **OTWARTE** | `LevyGold.cs:54-66` - przepuszcza tylko `MainParty` |
| W5 | Kryjowka | ZAMKNIETE czesciowo (wpis 36); zrodlo 25% przy wejsciu bandy dalej z niczego | - |
| W6 | Bank przed terminem | ZAMKNIETE, ale nowa dziura po terminie (M4) | `IronBank.cs:163,168` |
| S1 | Puste bandy | **OTWARTE** | `OutlawLaw.cs:550-557`, `:568-589` |
| S2 | Rozbitkowie 50% w nicosc, hearth zamiast BK, asymetria per | **OTWARTE** | `OutlawLaw.cs:184` (surowe `OutlawHearthPerMan`), `:282-291` |
| S3 | Cache wojny `_war` | **OTWARTE** | `OutlawLaw.cs:66-69` (Reset bez `_war`), `:360-370`, `:409` |
| S5 | ROT podmiana rekruta, zloto z/w nicosc | **OTWARTE** | rot `ROTTroopRecruiter.cs:205-216` |
| S7 | StartKit kon/uprzaz | ZLAGODZONE przez `MountMeshGuard` (prefix na `AddMountMesh` zdejmuje uprzaz z obcej rodziny) | `StartKit.cs:99`, `MountMeshGuard.cs` |
| S8 | Rampa rajdow Innych `0.01 x dni` | **OTWARTE** | rot `ROTOthersCampaignBehavior.cs:1120` |
| N3 | Reset stanow | CZESCIOWO (OutlawLaw `_war`, liczniki dnia, HideoutPurge itd. dalej poza Reset) | `ArmouryBehavior.cs:389` |
| N6 | `ToString()` enuma w LevyGold | **OTWARTE** | `LevyGold.cs:33,37,43` |
| N7 | Cofniety awans na pozycji Y | **OTWARTE** | `VolunteerKit.cs:81` |
| N8 | Renty gracza niewidoczne | **OTWARTE** + to samo dla powinnosci korony | `PopulationLaw.cs:171`, `KingdomTreasury.cs:67` |

---

## 2. WYSOKIE

### H1. (W2, potwierdzone) Sprzet ochotnika: kupiony przepada, a komplet i tak przychodzi z niczego
**Pliki:** `Armoury/src/VolunteerKit.cs:130-164`; `Armoury/src/AiGear.cs:69-75`; `Armoury/src/Settings.cs:367`;
dte `EveryoneCampaignBehavior.cs:870-895`; rot `ROTTroopRecruiter.cs:202-216`.

**Co sie dzieje:**
- Notabl zdejmuje sztuki z polki i placi miastu (`:160-161`), ale sztuk nikt nie dostaje - znikaja.
- Przy werbunku DTE `OnTroopRecruited` dodaje werbujacemu pelny komplet szablonu: gracz zawsze
  (`RecruitKitPrefix` przepuszcza `MainHero`), AI dopoki `AiRecruitsBringKit = true` (domyslnie).
- ROT `ExchangeClanTroops` po werbunku podmienia rekruta na typ rodu i **ponownie odpala `OnTroopRecruited`**
  (`:216`) - DTE daje drugi komplet dla podmienionego, a pierwszy zostaje w zbrojowni. Kazdy rekrut lorda
  z szablonem ROT = dwa komplety z niczego.

**Poprawka (kolejno, osobne kroki):**
1. `Dictionary<Hero, List<EquipmentElement>> _stash` (zapas notabla; zapis w save jak `OutlawLaw.Export`).
   W `Buy` zamiast niszczyc: `_stash[notable].AddRange(picks)`.
2. W `LevyGold.ApplyInternalPostfix` dla `VolunteerFromIndividual` (gracz i AI): przelac `_stash[__2]` za
   tego rekruta do zbrojowni DTE werbujacego (`AiGear._add` = `AddItemToPartyArmory(party.Id, item, 1)`),
   gracz - do `MobileParty.MainParty.ItemRoster` albo zbrojowni DTE gracza.
3. Prefix na DTE `OnTroopRecruited`: `return false`, gdy `recruitmentSource != null && recruitmentSource.IsNotable`
   i VolunteerKit wlaczony (rekrut przychodzi tylko z tym, co kupil notabl + tier 1: odzienie).
4. Prefix na DTE `OnTroopRecruited`, gdy wola go ROT (flaga ROT `_firingEvent` przez refleksje albo
   `recruitmentSettlement == null && recruitmentSource == null`): pominac - podmiana to ten sam czlowiek.

### H2. (W3, potwierdzone) Darmowe awanse ochotnikow - trzy drogi
**Pliki:** `Armoury/src/VolunteerKit.cs:66-82`, `:178`; bk `BKNotableBehavior.cs:157-283`;
`Armoury/src/HouseLevies.cs:66-77`, `:136-147`.

1. **Roznica pul.** Tego samego dnia slot 0: X->Y, slot 1: pusty->X (podstawowy). Przed {X}, po {Y, X}: X
   "zostal", Y "przybyl" bez zrodla -> traktowany jak nowy ochotnik tieru 1, awans za darmo. Przy wysokiej
   szansie BK zdarza sie stale.
2. **Zamki.** Vanilla `UpdateVolunteersOfNotablesInSettlement` (tw :215-219) wychodzi dla zamkow; zamki
   odswieza BK `HandleCastles -> UpdateVolunteers` (awans do tieru 4 za darmo). VolunteerKit tam nie siedzi.
   (Levy dziala - BK wola ten sam `GetDailyVolunteerProductionProbability`.)
3. **HouseLevies.** `Pick(tree, tier, ...)` dopuszcza `Math.Abs(c.Tier - tier) == 1` - szlachecki ochotnik
   tieru 2 moze stac sie czlowiekiem rodu tieru 3 bez zakupu.

**Poprawka:**
- Postfix: kazdy przybyly Y, ktory nie jest `VolunteerModel.GetBasicVolunteer(n)` i ma tier > 1, to awans;
  zrodla X szukac najpierw w zniknietych, potem w pozostalych (X, ktorego `UpgradeTargets` zawiera Y).
  Gdy zakup sie nie uda - cofnac do X (nie do null).
- Ten sam prefix/postfix na `BannerKings.Behaviours.BKNotableBehavior.UpdateVolunteers(Settlement)` (private,
  przez `AccessTools.Method`). Uwaga: BK bierze tylko `Notables[0]` - prefix moze zapisac wszystkich, nic nie szkodzi.
- `HouseLevies.Pick`: najpierw `spread 0`, potem tylko `c.Tier < tier` (nizszy), nigdy wyzszy.

### H3. (W4, potwierdzone) "Bez darmowego kompletu" obejmuje za duzo, a bitwa i lup i tak daja sprzet
**Plik:** `Armoury/src/LevyGold.cs:54-66`; dte `EveryoneCampaignBehavior.cs:468-508`, `SpawnAgentPatch.cs:42-98`.

- Prefix pomija cale `OnMobilePartyCreated` dla kazdej partii poza `MainParty`: partie towarzyszy gracza,
  karawany gracza i AI, nowe garnizony (`AddGarrisonParty`), lordowie odrodzeni po niewoli. Bezpieczne
  technicznie (DTE `AddItemToPartyArmory` tworzy slownik przez `TryGetValue`, `:940-944`), ale gracz zobaczy
  pusta zbrojownie towarzysza.
- W bitwie DTE `FillEmptySlots` i tak ubiera AI w szablon; vanilla tworzy lup z szablonu poleglych.

**Poprawka:** przepuszczac `__0.ActualClan == Clan.PlayerClan`; decyzja Jeffa w sprawie FillEmptySlots dla AI
i lupu ze zbrojowni (C5) - bez niej ten przelacznik daje tylko pozor.

### H4. Podwojny hamulec ochotnikow: SlowMuster x Levy (x BK)
**Pliki:** `Armoury/src/SlowMuster.cs:39-46` (x `VolunteerRegenPercent` 25%, `Settings.cs:144`);
`Armoury/src/Levy.cs:86-99` (x 0.05..1.5); tw `RecruitmentCampaignBehavior.cs:231-249`.

- Obie latki to postfixy na tej samej metodzie BK - mnoza sie. Spokojna wies bez nadwyzki rak:
  `0.5..0.8 (BK) x 0.25 x 0.05` = ~0.6-1% na slot dziennie, ~1 ochotnik na notabla na 3-4 tygodnie.
- Ta sama szansa bramkuje AWANSE (vanilla: `if (rand < p) { pusty -> nowy; inaczej proba awansu log2(moc/tier) x 1% }`),
  wiec awanse beda rzadsze o dwa rzedy wielkosci - VolunteerKit (krok 2) prawie nie zadziala.
- AI z braku ochotnikow siegnie po zrodla, ktore NIE sa zwiazane z ludnoscia: najemnicy z karczmy (odrastaja
  z niczego), przyrost garnizonu (H6), startowe sklady nowych partii (Czego brakuje 4).
- SlowMuster powstal (28.08), zanim byla zasada "kto sie zglasza"; jego uzasadnienie (rok 168) juz nie obowiazuje.

**Poprawka:** gdy `LevyEnabled`, SlowMuster nie mnozy (warunek w `SlowPostfix`), a skale ustawia
`RecruitBaseWilling`. Alternatywnie Levy mnozy tylko wypelnianie pustych slotow, a nie awans: w prefixie
`UpdateVolunteersOfNotablesInSettlement` nie da sie tego rozdzielic - trzeba by transpilera; prosciej zostawic
jedna latke. Przed decyzja: log `Pobor: dzien N - chec srednio xW` z 3-5 dni gry.

### H5. Trzy ludnosci; wyrzutki zabieraja hearth, wiec produkcja wsi ROSNIE
**Pliki:** `Armoury/src/OutlawLaw.cs:159-187`, `:298-313`; `Armoury/src/PopulationLaw.cs:106-115`;
be `BEE_VillageProductionCalculatorModel.cs:99-115`, `BannerKingsAdapter.TryGetPopulationSnapshot`;
bk `PopulationManager.cs:480` (hearth uzyty tylko przy starcie), bk `VillageDailyTicktPatch` (hearth dalej wlasnym torem).

- **Pobor BK** (`DeduceManpower` -> `UpdatePopType(-n)`) zmniejsza chlopow BK. **Wyrzutki** zmniejszaja tylko
  `Village.Hearth`. **Renty** (PopulationLaw) licza `hearth x k` (k ~ setki ludzi na punkt).
- BEE: czynnik produkcji wsi = `(serfs + tenants + slaves BK) / (Hearth x VillagePeasantsFromHearth)`, 0.2-1.5.
  Gdy wyrzutki zabieraja hearth, mianownik maleje -> **czynnik rosnie**: bandy podnosza produkcje wsi.
  Pobor dziala poprawnie (licznik maleje).
- Rekrut nie zmniejsza rent (hearth nie rusza), banda zmniejsza renty, ale nie zmniejsza rak do pracy.
- Powrot z puli (`ReturnHome`) oddaje hearth takze dezerterom-zolnierzom, ktorzy nigdy z hearth nie wyszli
  (ich ubytek byl w BK) - hearth z niczego.

**Poprawka:**
1. OutlawLaw: zamiast (albo obok) `v.Hearth -= ...` - `PopulationData.UpdatePopType(Serfs/Tenants, -n)` BK
   (refleksja jak w `Levy.Resolve`); `ReturnHome` - `UpdatePopType(+n)`. Hearth zostawic w spokoju (to
   wskaznik vanilli/BEE, nie ludzie).
2. PopulationLaw: `PeopleOf = TotalPop BK x wspolczynnik krainy` (kalibracja raz na save, jak teraz) -
   wtedy pobor, zaraza, glod i wyrzutki zmieniaja renty jedna miara.
3. Dezerterzy i rozbitkowie wracajacy "do domu" -> `MilitaryData.AddManpowerFromSoldiers` / `UpdatePopType`
   osady regionu, nie hearth.

### H6. Garnizon: auto-werbunek i przyrost "base" omijaja ludnosc i notabli
**Plik (gra):** tw `GarrisonRecruitmentCampaignBehavior.cs:76-108`, `:206-243`; tw `DefaultClanFinanceModel.cs:802-805`.

- `TickAutoRecruitmentGarrisonChange` bierze ochotnikow notabli przez `AddToCounts` - NIE przez `ApplyInternal`,
  wiec BK nie odejmuje manpower (BK latka jest tylko na `ApplyInternal`), notabl nie dostaje zlota, a
  `AutoRecruitmentExpenses` placone przez rod (1/5 dziennie) ida w nicosc.
- `TickGarrisonChangeForTown` dodaje `town.MapFaction.BasicTroop` z niczego (koszary/budynki), co dzien.

**Poprawka:** postfix na `TickAutoRecruitmentGarrisonChange` z prefixem zapamietujacym `VolunteerTypes` notabli
osady: dla kazdego zabranego slotu `DeduceManpower(popData, 1, troop, notable)` + koszt do notabla (i zerowanie
odpowiedniej czesci `AutoRecruitmentExpenses`, zeby rod nie placil dwa razy - albo prosciej: zostawic wydatek
rodu, a notablowi dac kwote zdjeta z `AutoRecruitmentExpenses` w `AddExpensesForAutoRecruitment`). Przyrost
"base": postfix na `CalculateBaseGarrisonChange` ograniczajacy do manpower BK osady i `DeduceManpower` w postfixie
`TickGarrisonChangeForTown`.

---

## 3. SREDNIE

### M1. (S1) Puste bandy nadal - glowne ryzyko crasha w tym zakresie
`Armoury/src/OutlawLaw.cs:550-557` (region ze `HomeSettlement`), `:568-589` (`GateClan` sprawdza DOWOLNA
kryjowke klanu), `:591-602` (`GateGlobal` - suma swiata), `:329-345` (`DestroyPartyAction` co godzine).
Partia z 0 ludzi zyje do godziny na mapie (AI, spotkania). **Poprawka:** w `RosterPostfix`, gdy
`Avail(region) < want`, wziac najblizszy region z `Avail >= MinBand`; gdy nic - 1 czlowiek z dowolnej puli
(lub, gdy pula swiata pusta, 1 looter "z niczego" i log) zamiast pustego rosteru.

### M2. (S2) Wyrzutki: rozbitkowie w nicosc i asymetria hearth
`OutlawLaw.cs:282-291`: druga polowa `RoutedInBattle` (vanilla zdejmuje ich z partii: tw `MapEventParty.cs:316-322`)
znika. `:184`: `ReturnHome` bierze surowe `OutlawHearthPerMan`, `:162` - `max(0.01, ...)`. **Poprawka:** jak S2
+ H5 (zwrot do BK).

### M3. (S3) Levy czyta nedze z cache wojny, ktory czysci tylko OutlawLaw.Daily
`OutlawLaw.cs:360-370`, `:409`; `Reset()` `:66-69` bez `_war.Clear()`. Przy wylaczonych wyrzutkach Levy liczy
wojne po stanie z pierwszego zapytania; po wczytaniu innej gry w tej samej sesji slownik trzyma frakcje
poprzedniej kampanii (obiekty z innego swiata - tylko klucze, bez crasha, ale zle wyniki). **Poprawka:**
datownik dnia w `AtWar` + `_war.Clear()` w `Reset()`.

### M4. Bank: dobieranie po terminie resetuje termin calego dlugu (rolowanie wrocilo)
`Armoury/src/IronBank.cs:163` blokuje tylko `DueDay > today && DueDay - today < 30`; `:168` ustawia nowy termin,
gdy `DueDay <= today`. Po terminie (rata = caly dlug, spoznienie) AI z brakiem zlota na zold (`:206-207`)
pozycza dowolnie mala kwote i dostaje nowe pol roku na CALY dlug; spoznienia sie zeruja przy pierwszej
splaconej (malej) racie. Gracz tak samo. **Poprawka:** gdy `running && DueDay <= today` - odmowa (albo transza
z osobnym terminem: lista transz w `Debt`).

### M5. (S5) ROT: podmiana rekruta - zloto z/w nicosc i drugi komplet DTE
rot `ROTTroopRecruiter.cs:205-216`: `GiveGoldAction(null, owner, roznica)` (dodatnia = z niczego, ujemna = w nicosc,
bo `ChangeHeroGold` bez odbiorcy) + ponowne `OnTroopRecruited` (H1 pkt 4). **Poprawka:** prefix na
`GiveGoldAction.ApplyBetweenCharacters` z flaga ustawiana prefixem/finalizerem `ExchangeClanTroops`: przy
roznicy dodatniej -> zero; ujemnej -> do notabla/miasta. Log liczby podmian.

### M6. Levy nie widzi puli poboru BK ani klasy; miasta liczone ziemia
`Armoury/src/Levy.cs:58-91`. Chec zalezy od `WorkforceExcess/AvailableWorkForce` (ziemia) i nedzy, a nie od
`MilitaryData.Manpower` (pula po wczesniejszym poborze). BK `CanHaveRecruits` sprawdza tylko `Manpower > 0`
(suma wszystkich klas, bk `BKVolunteerModel.cs:249-269`) - wies wydrenowana prawie do zera dalej wystawia
ochotnikow w normalnym tempie. W miastach `LandData` (pola wokol miasta) nie mowi nic o rzemieslnikach (W12).
**Poprawka:** `w *= clamp(Manpower / (suma pulapow klas), 0.1, 1)` (pulap = `GetTypeCount x militarnosc`,
bk `MilitaryData.cs:179-195`); dla miast zamiast `ExcessShare`: udzial warsztatow stojacych (WorkshopLaw zna
powody) + bieda miejska (1 - dobrobyt/`OutlawProsperityGood`).

### M7. Najemnicy z karczmy - zrodlo wojska z niczego, a przy Levy stana sie glownym
tw `RecruitmentCampaignBehavior.cs:341-413` (`UpdateCurrentMercenaryTroopAndCount` odnawia liczbe i typ),
`:704-714`, `:788-794` (gracz placi `GiveGoldAction(gracz, null)` - w nicosc). LevyGold kieruje do miasta tylko
zloto AI. **Poprawka:** postfix na `UpdateCurrentMercenaryTroopAndCount`: liczba = min(vanilla, zolnierze w puli
wyrzutkow regionu miasta) i zdjecie ich z puli (`OutlawLaw.DrawOnly` dla kluczy nie-"~"); zloto gracza za
najemnikow - prefix na `BuyMercenaries`/`buy_mercenaries_on_consequence` -> `GiveGoldAction.ApplyForCharacterToSettlement`.

### M8. Powinnosci korony: od dochodu brutto, niewidoczne dla gracza
`Armoury/src/KingdomTreasury.cs:57-68`. `CalculateClanIncome` to dochod BRUTTO (bez zoldu), wiec rod na minusie
placi 10% w wojnie - przyspieszy bankructwa (zamierzone, ale razem z Bankiem M4 i rentami z kasy osad moze
dac kaskade). Wliczona jest tez zapomoga ze skarbca (tw `DefaultClanFinanceModel.cs:517-528`) - rod oddaje
koronie 10% jej wlasnej zapomogi (drobne kolko). Graczowi zloto znika bez komunikatu. **Poprawka:** podstawa
= max(0, `CalculateClanGoldChange` netto + renta) albo brutto bez linii "Kingdom support"; dla gracza
`InformationMessage` raz dziennie ("Crown dues: X") albo linia opisowa w `ClanFinanceModel`.
Sprawdzone OK: transpiler `DailyTickClan` trafia dokladnie 4 stale (1000/100000/200000/400000; inne liczby
tej metody: 10000, 2000000, 1000000f - nie na liscie) - log ma pokazac "podmienionych stalych 4".

### M9. Demobilizacja: ludzie znikaja albo ida do cudzego garnizonu
tw `DisbandPartyCampaignBehavior.cs:290-361`: rozwiazana partia przy wlasnej frakcji -> garnizon (ok), przy
obcej -> `MemberRoster.Clear()` (znikaja), przy wsi -> rozjazd. BK `UpdatePopFromSoldiers` tylko przy pokoju
krolestwa i tej samej kulturze (bk `BKClanBehavior.cs:1200-1238`). Zwolnieni przez gracza (ekran druzyny) i
partie po smierci lorda - w nicosc. **Poprawka:** postfix na `MergeDisbandPartyToFortification`/`ToVillage`
oraz `OnPartyDisbanded`: przed `Clear()` ludzie -> `AddManpowerFromSoldiers` najblizszej osady ich kultury
albo (gdy obca) do puli wyrzutkow regionu.

### M10. Renty: wzor celu kasy miasta wpisany na sztywno; wies traci polowe kasy dziennie
`Armoury/src/PopulationLaw.cs:168-169`. `10000 + 12 x dobrobyt` to wzor vanilli; dziala tylko, jesli aktywny
`SettlementEconomyModel` to BEE (ktory wola `new DefaultSettlementEconomyModel()`, be
`BEE_SettlementEconomyModel.cs:15,43-46`). Gdy kolejnosc modow da BK (`BKEconomyModel.GetTownGoldChange` =
dochod kupcow bez celu, bk :642-647), surplus jest liczony od zlego progu. Wies: do 50% kasy dziennie - kasa
wsi zasila m.in. inwestycje BEE (be `VillageInvestmentCampaignBehavior.cs:400` -> hearth). **Poprawka:** cel z
aktywnego modelu (`gold - 4 x max(0, -GetTownGoldChange(town))` gdy zmiana ujemna, inaczej 0) i log typu modelu
przy starcie; wies 10-20% zamiast 50%.

### M11. (S8) Fabula: rampa sily rajdow Innych bez skali
rot `ROTOthersCampaignBehavior.cs:1120` `min(0.01 x dni, 50)` - przy osi x4.33 Inni osiagaja pelna sile
4.33x wczesniej wzgledem fabuly. `Fabula.cs:290-307` skaluje tylko 300/400/500. **Poprawka:** w tym samym
transpilerze `ldc.r4 0.01` stojace przed `ElapsedDaysUntilNow` -> `0.01/k`.

---

## 4. NISKIE

- **L1 (N6)** `LevyGold.cs:33,37,43` - rozpoznanie przez `ToString()` enuma; porownac `(int)` z
  `RecruitingDetail.VolunteerFromIndividual/MercenaryFromTavern`. Typ `object __6` dla enuma jest poprawny
  (Harmony pakuje).
- **L2 (N7)** `VolunteerKit.cs:81` - cofniety awans zostaje na pozycji Y (kosmetyka puli).
- **L3 Przejrzystosc.** Levy jest niewidoczny w UI BK ("Draft efficiency" w `MilitaryData.DraftEfficiency`,
  bk `MilitaryData.cs:55-71`). Latka na `GetDraftEfficiency` (zamiast `GetDailyVolunteerProductionProbability`)
  z `AddFactor(w - 1, new TextObject("Hands to spare and hardship"))` pokazalaby gracza przyczyne - UWAGA: wtedy
  zdjac obecny postfix, inaczej podwojne mnozenie. Renty i powinnosci korony bez linii w finansach (N8, M8).
- **L4 StartKit** `StartKit.cs:67-77` - zamiennik bez filtra `it.IsReady`/`IsCraftedByPlayer`/`NotMerchandise`
  dla broni craftowanych szablonow; kon (S7) zabezpieczony przez MountMeshGuard. `_pending` nie w save
  (zapis w pierwszej godzinie = brak zamiany) - bez znaczenia.
- **L5 Bank** `IronBank.cs:62-66` - dlugosc roku z modelu (czyta MCM na zywo) zamiast `CampaignTime.DaysInYear`
  (statyczne po Initialize); fallback 84. `:116`, `:217` - `Clan.FindFirst` w petli (N4).
- **L6 Fabula** `CrashScribe/src/Config.cs` - `FabulaTimeScale` 4.33 na sztywno; przy wylaczonym dlugim roku
  (84 dni) fabula idzie 4.33x za wolno. Lepiej `CampaignTime.DaysInYear / 84f`.
- **L7 Zloto notabli.** BK daje kazdemu nowemu notablowi 10 000 z niczego (bk `NotablePatches.cs:85-89`);
  zloto zmarlego notabla przepada. Notable maja teraz przychod (LevyGold) i wydatek (VolunteerKit), brak
  zwyklego dochodu z osady.
- **L8 Reset (N3)** - `OutlawLaw` (`_war`, liczniki dnia `_inDesert`...), HideoutPurge, WinterBite,
  SupplyDemand, MapClock, Stables poza konstruktorem `ArmouryBehavior.cs:389`.
- **L9 Data** `WesterosClimate.cs:246-265` - format "Day N of Summer, 299 AC" po angielsku zamiast
  `str_date_format` (zgodnie z zasada "napisy po angielsku" - OK); daty sprzed historii -> sam rok.

---

## 5. Sprawdzone i poprawne (bez uwag)

- **Levy**: refleksja BK (`BannerKingsConfig.Instance` statyczna wlasciwosc, `PopulationManager` wlasciwosc -
  kod probuje pola, potem wlasciwosci; `GetPopData(Settlement)`, `LandData`, `WorkforceExcess` (moze byc ujemne -
  clamp 0), `AvailableWorkForce`) - wszystko istnieje; wyjatek BK (`BannerKingsException`) lapany -> 0.2.
  Zaden inny mod (ROT, BKROTPatch, RBL, BEE, Naval) nie nadpisuje `GetDailyVolunteerProductionProbability`.
- **VolunteerKit**: parametr `settlement` zgodny; tablica BK `VolunteersLimit` (moze byc > 6) obslugiwana
  (`Clone()`); `GiveGoldAction.ApplyForCharacterToSettlement` przycina do zlota notabla, a `Buy` sprawdza je wczesniej.
- **LevyGold**: pozycje `__0..__6` zgodne z `ApplyInternal(side1Party, settlement, individual, troop, number,
  bitCode, detail)`; lord AI werbuje tylko, gdy `PartyTradeGold > koszt` (tw :550), wiec notabl nie dostaje
  wiecej, niz lord zaplacil. `GameStarted` = true dopiero przy pierwszym `RealTick` nowej gry (tw `Campaign.cs:894`),
  wiec startowe partie dostaja komplet.
- **BK `DeduceManpower`** dziala dla gracza i AI przez `ApplyInternal` (bk `RecruitmentApplyInternalPatch`);
  pula moze zejsc ponizej 0 do nastepnego `Update` (clamp 0..pulap, +1% pulapu dziennie).
- **OutlawLaw**: nazwy parametrow bramek zgodne (`SpawnBanditParty(Clan selectedFaction)`,
  `SpawnLooterParty(Clan, bool)`, `FillANewHideoutWithBandits(Clan faction)`, Naval `SpawnPirateParty(Clan clan, ...)`,
  BK `InfestHieout(Hideout, Clan)`, `CreateBanditHero(Clan clan)`, `UpgradeReadyTroops(PartyBase party)`).
  Licznik zagniezdzenia rosteru poprawny (prefix/postfix/finalizer). BEE "recruit quality drift" wylaczony przy BK.
- **Calendar**: `ROTCampaignTimeModel : CampaignTimeModel` (nie Default...), wiec `prev` jest brany - start
  299 AC, wschod 2, zachod 22 z ROT.
- **WesterosClimate**: typy podmian zgodne (`GetSeasonOfYear` enum, `GetDayOfSeason` int, `ToSeasons` double,
  `DaysInSeason` static int); `ref CampaignTime` przyjmuje adres struktury; `str_season_<Enum>` jak w vanilli.
- **Fabula**: `ROTStorylineWar.Enforced(out string)` + `StartDay/EndDay {get;set;}`; progi 300/400/500 stoja
  po `ElapsedDaysUntilNow` (float) - transpiler trafi.
- **KingdomTreasury**: patrz M8 (stale zweryfikowane).

---

## 6. CZEGO BRAKUJE do realistycznego werbunku zwiazanego z ludnoscia i gospodarka

1. **Jedna ludnosc.** BK jako jedyne zrodlo ludzi: renty (PopulationLaw), wyrzutki (OutlawLaw), powroty
   z wojska i z lasu. Hearth vanilli tylko jako wskaznik, nie jako ludzie (H5).
2. **Pula poboru w checi.** Levy x stan `Manpower` BK; rozdzielenie klas (chlopi -> piechota/lucznicy,
   szlachta -> jazda; BK `GetCharacterManpowerType` juz to wie) - pusta pula szlachty = brak awansow na konnych.
3. **Garnizon i milicja** z ludnosci: auto-werbunek przez `DeduceManpower` + zloto notablowi; przyrost
   "base" i `Militia` (tw `Village.DailyTick`, BK) ograniczone pula BK (H6).
4. **Startowe sklady partii** (lord po niewoli, karawana, wiesniacy) z `FindAppropriateInitialRosterForMobileParty`
   (tw `DefaultPartySizeLimitModel.cs:427-464`) sa z niczego - dla partii lordow pobrac z manpower BK osady
   macierzystej (postfix jak `OutlawLaw.RosterPostfix`, tylko dla `IsLordParty`).
5. **Najemnicy z puli wyrzutkow/dezerterow** regionu miasta; zloto gracza za najemnikow do kasy miasta (M7).
6. **Demobilizacja i jency do ludnosci:** rozwiazane partie, zwolnieni, wypuszczeni jency -> BK; sprzedani
   jency w miescie -> `PopulationData.AbsorbCaptives` (BK ma gotowa metode, bk `PopulationData.cs:349-370`)
   zamiast znikania (M9).
7. **Sprzet rekruta:** zapas notabla do zbrojowni werbujacego, koniec kompletu DTE dla rekrutow od notabli,
   tier 1 = odzienie i narzedzie z towarow wsi (len, narzedzia - zdjete z targu wsi) (H1).
8. **Zasady w miastach:** chec = bezrobocie rzemieslnikow (warsztaty stojace z braku surowca/zlota -
   WorkshopLaw) + bieda miejska; w zamkach - tylko ludzie pana (szlachta BK) (M6).
9. **Rytm roku:** przy wieloletnich porach (WesterosClimate) - zima i przednowek = wiecej rak bez pracy,
   zniwa = mniej ochotnikow; dzis Levy nie czyta pory roku.
10. **Feudalny pobor (sluzba lenna):** dzis lord werbuje jak kupiec. Historycznie wasal przyprowadza swoich
    ludzi z lenna; "tarczowe" (KingdomTreasury 10% w wojnie) to zamiennik sluzby - mozna by zwolnic z niego
    rody, ktore wystawiaja partie w armii krola.
11. **Zold do gospodarki** (wydawany w osadach, gdzie partia stoi) - dzis znika; bez tego notable/miasta nie
    maja z czego finansowac awansow (L7) poza zlotem za werbunek.
12. **Przejrzystosc dla gracza:** czynnik Levy w UI BK, renty i powinnosci w finansach rodu (L3).
13. **Kalibracja po pierwszych dniach gry:** linie `Pobor:`, `Ochotnicy:`, `Werbunek:`, `Wyrzutki:`, `Ludnosc:`,
    `Korona:`, `IronBank:` z 3-5 dni - przed jakakolwiek zmiana liczb (H4 jest decyzja Jeffa, nie bledem kodu).

---

## 7. Proponowana kolejnosc (jedna zmiana naraz)

1. H4 - wylaczyc SlowMuster przy Levy (jedna linia, ustawienie) - zanim zacznie sie ocene VolunteerKit.
2. H2 - poprawka roznicy pul + latka na BK `UpdateVolunteers` + HouseLevies bez awansu w gore.
3. H1 - zapas notabla -> zbrojownia werbujacego; prefix DTE `OnTroopRecruited` dla rekrutow od notabli i podmian ROT.
4. M1 - bandy bez pustych skladow (ryzyko crasha).
5. M4 - bank bez dobierania po terminie.
6. H6 - garnizon przez pule BK.
7. H5 - wyrzutki i renty na ludnosci BK (najwieksza zmiana; osobny plan).
8. M3, M2, M5, M7, M9, M10, M11, L-ki.
