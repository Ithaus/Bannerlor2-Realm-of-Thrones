# Paczka 171 - wynik agenta 3 (workflow wf_e42eab09-42b)

## summary

Rozpoznanie 3, tylko odczyt, w drzewie nic nie zmienialem. (a) Garnizon zamku kupuje dzis z polki SWOJEGO zamku (AiGear.TryBuy), a pan placi kasie zamku. Polke zamku wypelnia glownie SupplyDemand.DailyTrade, ktory przenosi nadwyzke natychmiast, bez drogi, a placi kasa zamku po cenie hurtowej. W dobie 360 zamki sprzedaja sprzet za ok. 342 tys. na dobe (miasta 640 tys.), a garnizony wydaja razem 820-835 tys. na dobe; na starcie kampanii bylo to 46-65 tys. (b) Wozy MarketCarts, CartTownExit i MarketRoad to wozy wsi z samej gry, Armoury nie tworzy zadnych partii. Uzycie ich do dowozu do zamku jest kruche, bo w zamku BK sprzedalby ladunek na polke. Proponuje zamowienie w drodze bez partii: zakup na targu miasta, zloto pana do kasy miasta, towar w zapisie, a dostawa do zbrojowni zalogi po czasie wynikajacym z odleglosci drogowej i predkosci wozu wsi. (c) Gotowa funkcja to ArmyClothing.MarketTown (ArmyClothing.cs:250-268): miasto handlowe wsi zamku, ktore gra sama przelicza przy wojnie, pokoju i zmianie wlasciciela. VolunteerKit.MarketOf sie nie nadaje (liczy po linii prostej, nie patrzy na wojne, trzyma wynik na stale). (d) Doswiadczenie zalog: budynki Training Fields i codzienny projekt Drills z gry, mnoznik polityki BK, oboz BEE (to zamyka paczka 170). Cwiczenia moga zostac, ale uzaleznione od tego, czy ludzie maja wlasna bron w zbrojowni. Przy okazji: oplata za awans zolnierza w grze po prostu znika.

## findings

Sciezki: repo = C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/zaloga171/repo/Armoury/src, gra = .../scratchpad/ore-supply/cs, BK = .../ore-supply/bk, BEE = .../ore-supply/be.

=== (a) CO DZIS KUPUJE ZALOGA ZAMKU ===
1. Wywolania: AiGear.OnDailyTickParty (AiGear.cs:120-123) dla kazdej partii stojacej w osadzie, wpiete w ArmouryBehavior.cs:513, oraz OnSettlementEntered (AiGear.cs:118, ArmouryBehavior.cs:509).
2. TryBuy (AiGear.cs:170-320):
 - garrison = mp.IsGarrison && mp.CurrentSettlement == st && GarrisonBuysGear (:178);
 - placi st.OwnerClan.Leader (:179);
 - gracz pomijany, gdy GarrisonBuysGearPlayer = false (:180; domyslnie false, Settings.cs:402);
 - dozwolone miasto ALBO ZAMEK (:182), bez osad wrogich (:183), raz na dobe na partie (:184-187);
 - budzet = (zloto pana - AiGearGoldReserve 2000) x AiGearBudgetPercent 25%, bez sakiewki ludzi (:192-197);
 - potrzeba = wzorce ludzi w koszykach typ x tier minus zbrojownia DTE z zastepstwem tierow (:203-245);
 - zakup Z POLKI st.ItemRoster, czyli z polki zamku (:250-291);
 - cena st.Town.MarketData.GetPrice(..., st.Party) (:270). Dane rynku zamku: RawPrice pisze w logu, ze "gra nie prowadzi ich danych rynku", wiec cena w zamku jest nieaktualna;
 - towar do zbrojowni DTE (:283), zloto pana -> st.Town.ChangeGold, czyli KASA ZAMKU (:286-288);
 - brak na polce -> zamowienie SupplyDemand.NoteUnmetOnce(mp, st = ZAMEK, ...) (:298-308).
3. Kto wklada sprzet na polke zamku:
 - SupplyDemand.DailyTrade (SupplyDemand.cs:378-483, wolane w ArmouryBehavior.cs:1260). Wsrod osad sa takze zamki (:395), jako zrodlo i jako cel. Przeniesienie jest natychmiastowe, bez drogi (:466-469), placi kasa osady docelowej po cenie hurtowej 50% x mnoznik zrodla (:461-470, SupplyDemandTradePricePercent, Settings.cs:675). Popyt zamku = 0.5 x zamoznosc plus zamowienia (SupplyDemand.cs:144-163, :152), a zamowienia stawia sam garnizon (AiGear.cs:307).
 - Gra nie: lordowie sprzedaja lup tylko w miastach (gra, PartiesSellLootCampaignBehavior.cs:21, warunek !settlement.IsTown). MenPurse tez tylko w miastach (MenPurse.cs:136,165,177). ColdStart wypelnia polki tylko miast (ColdStart.cs:146).
 - Wynik (log 11-32-39, doba 360): kasy zamkow, pozycja "zakupy sprzetu AI" +341997 na dobe (miasta +640072). Garnizony: 148-168 zakupow za 820-835 tys. na dobe na 890-896 tys. calosci (ZakupyAI). Na starcie (log 11-40-05): garnizony 46-65 tys., zamki +5.4-5.8 tys. DailyTrade w dobie 360 przenosi 6-14 tys. sztuk na dobe, a 147-155 tys. sztuk utknelo bez odbiorcy.
 - Do sprawdzenia w logu (paczka 169), na razie tylko hipoteza: skok wydatkow garnizonow 15x mozna tlumaczyc awansami z doswiadczenia "z niczego" (pkt d: kazdy awans wymaga kompletu wyzszego tieru) i nieaktualna cena polki zamku.
4. Garnizon miasta kupuje z polki swojego miasta, a zloto pana idzie do kasy miasta. To juz zgadza sie z decyzja Jeffa (1) i zostaje bez zmian.
5. Rekruci zalog juz dzis przychodza bez kompletu, co pasuje do decyzji (2):
 - gra GarrisonRecruitmentCampaignBehavior.cs:97-108 dodaje BasicTroop zwyklym AddToCounts;
 - auto-werbunek (:76-95) zabiera ochotnika z puli notabla bez zdarzenia TroopRecruited, wiec DTE i RecruitKit go nie widza. Komplet z zapisu staje sie wtedy sierota i notabl sprzedaje go na targu (RecruitKit.cs:133-151).
 - Propozycja poboczna: prefiks na TickAutoRecruitmentGarrisonChange, ktory przenosi komplet ochotnika z RecruitKit (Pop/Materialize, RecruitKit.cs:44,53) do zbrojowni garnizonu zamiast sprzedazy.
6. Gracz: jego garnizony nie kupuja (GarrisonBuysGearPlayer = false). Jego wlasny werbunek dostaje komplet DTE bez zmian (AiGear.cs:84, RecruitKit.cs:167). Jego partia jest wylaczona z TryBuy (:181). Proponuje nie zmieniac; nowa regula dla jego zamku tylko przy GarrisonBuysGearPlayer = true.

=== (b) WOZY ===
1. MarketRoad (MarketRoad.cs:49-86) to prefiks na grze VillagerCampaignBehavior.SendVillagerPartyToTradeBoundTown. Najpierw pyta MarketCarts.Choose (:59-68), potem wies zamkowa jedzie do TradeBound (:70-79). Udzwig wozu x MarketCartFactor i dolna granica wagi ladunku (:99-114).
2. MarketCarts.Choose (MarketCarts.cs:382-478):
 - woz wsi (VillagerPartyComponent z gry) wybiera miasto o najwyzszym utargu na dobe kursu;
 - odleglosc: MapDistanceModel.GetDistance(osada, osada, false, false, Default) (:362-368);
 - wrogie miasta pomijane (:370-375), oblezone tez (:426);
 - predkosc PerDay = EstimatedAverageVillagerPartySpeed x 24 x WorldPace (:338-346; przy WorldPacePercent 50 ok. 41 jednostek na dobe);
 - zasieg MarketMaxDistance 250 (Settings.cs:465);
 - "wiesc z drogi" (:513-571) to zapis wozow w drodze, ktory wygasa po 30 dobach (:659);
 - zaplata przy sprzedazy SellGoodsForTradeAction, ze zwrotem nadplaty sztuka po sztuce (:576-646).
3. CartTownExit (CartTownExit.cs:116-191, 245-373) to tylko latka straznika BK GuardSettlementMove (prog 50000, :45) i bezpiecznik wozu stojacego w miescie. Nie przewozi towaru.
4. Wniosek: Armoury NIE ma wlasnych partii (zadnego CreateParty ani PartyComponent w src).
 - Wlasna partia wozu wymaga zapisywalnego typu komponentu (zepsuje zapis po zdjeciu moda), sterowania AI, przejscia przez straznika BK i IslandRoads, a do tego do 130 partii na mapie. Odradzam.
 - Podczepienie pod woz wsi zamkowej: woz wraca z miasta do WSI, nie do zamku. Przy wjezdzie do zamku BK i gra sprzedalyby ladunek na polke zamku (SellGoodsForTradeAction). Kruche, odradzam.
5. PROPOZYCJA, prostsza droga: nowy plik GarrisonCarts.cs, "zamowienie w drodze" bez partii.
 - Zamek kupuje na targu miasta tym samym algorytmem co TryBuy. Najpierw wydzielic petle AiGear.cs:247-294 do BuyLoop(shelf, market, mp, payer, need, budget, deliver).
 - Towar schodzi z polki miasta, zloto pana -> market.Town.ChangeGold, MoneyLedger.Note(NGear, market).
 - Rekord: {zamek, miasto, klan/pan, doba przyjazdu, lista EquipmentElement x n}. Doba przyjazdu = teraz + GetDistance(miasto, zamek, false, false, Default) / MarketCarts.PerDay (zmienic na internal).
 - Raz na dobe (ArmouryBehavior, obok :1249-1260) dostawa AiGear.AddToArmory(castle.Town.GarrisonParty, it, n) (AiGear.cs:137).
 - Zamek oblezony: woz czeka. Po 30 dobach (ten sam prog co Haul w MarketCarts) albo gdy zamek przeszedl do frakcji wrogiej placacemu: woz zawraca, towar na polke miasta, pan dostaje cene skupu (MenPurse.SellPrice), nie wiecej niz kasa miasta, jak RecruitKit.SellOff (RecruitKit.cs:79-96).
 - Zamek w tym samym krolestwie, ale u innego pana: oddac zalodze.
 - Towar w drodze odejmowac od potrzeby w TryBuy (slownik zamek -> koszyk -> sztuki), zeby zamek nie zamawial co dzien tego samego.
 - Zapis: SaveText.Sync(dataStore, "arm_garrisoncarts", ...) obok ArmouryBehavior.cs:449-456, z rozwiazaniem po starcie sesji jak RecruitKit (161: ArmouryBehavior.cs:981-982, bo w SyncData obiektow jeszcze nie ma). Reset() w konstruktorze (ArmouryBehavior.cs:389).
 - Parametry bez nowych liczb, uzasadnienie: predkosc = woz wsi, bo to te same wozy; zasieg = MarketMaxDistance, jak wozy wsi; partia do AiGearMaxPiecesPerVisit 60 i budzet jak u lorda; zamowienie raz na dobe (dzisiejsza przepustowosc), towar w drodze sie liczy. Bandyci nie rozbijaja dostawy (swiadome uproszczenie).
 - Wylacznik MCM GarrisonGearFromTown, domyslnie wlaczony.
6. Zmiany w AiGear.TryBuy dla zalogi zamku (st.IsCastle):
 - najpierw dotychczasowy zakup z wlasnej polki zamku (to, co tam lezy, oplacila juz kasa zamku), reszta zamowieniem w miescie;
 - zamowienia na brak (AiGear.cs:307) stawiac w mie

ście targowym, nie w zamku;
 - partie lordow kupuja tylko w miastach (AiGear.cs:182, dla !garrison wymagac st.IsTown);
 - SupplyDemand.DailyTrade: przy wlaczonym GarrisonGearFromTown zamki NIE sa celem (SupplyDemand.cs:395, petla celow :441-452), zostaja tylko zrodlem. Wtedy zapas zamkow splywa do miast, a nowy nie przybywa natychmiast.
 - Log dobowy "Zaopatrzenie zamkow: dzien N - zamowien, szt., zloto pan -> kasy K miast, w drodze, dojechalo, zawrocone, czeka (oblezenie), bez miasta, za daleko".

=== (c) NAJBLIZSZE PRZYJAZNE MIASTO ===
1. VolunteerKit.MarketOf (VolunteerKit.cs:93-114): liczy po linii prostej (DistanceSquared), nie patrzy na wojne ani oblezenie, wynik trzyma w pamieci na stale (_market). NIE nadaje sie do zakupow zamku w czasie wojny.
2. Wlasciwe juz istnieje: ArmyClothing.MarketTown(castle) (ArmyClothing.cs:250-268): TradeBound pierwszej wsi zamku nie w wojnie, inaczej najblizsze niewrogie miasto (po linii prostej). Uzywa go odziez zalog zamkow (ArmyClothing.cs:227-244), gdzie zamek albo miasto oblezone = czekaj.
 - Propozycja: przeniesc do wspolnego miejsca (np. internal static w Helper.cs) i uzyc w GarrisonCarts.
 - Do zapasowej drogi dodac sprawdzenie odleglosci drogowej MapDistanceModel.GetDistance(castle, t, false, false, Default) < MarketMaxDistance oraz < CartTownExit.BkLimit (droga ladowa istnieje), plus !IsUnderSiege.
3. TradeBound liczy gra: DefaultVillageTradeModel.GetTradeBoundToAssignForVillage, czyli najblizsze po drodze miasto WLASNEJ frakcji w zasiegu 3 x sredni rozstaw miast, inaczej najblizsze obce niewrogie. Przelicza je przy nowej grze, wczytaniu, wypowiedzeniu wojny, pokoju, zmianie wlasciciela, zmianie krolestwa i zniknieciu klanu (VillageTradeBoundCampaignBehavior.cs:13-19, 62-71). Czyli koszt zero co dobe, a warunek "nie kupuje u wroga" dziala sam. Zostaje tylko sprawdzenie oblezenia i wojny w chwili zakupu: FactionManager.IsAtWarAgainstFaction(castle.MapFaction, town.MapFaction), jak w AiGear.cs:183.
4. Inne narzedzia gry: SettlementHelper.FindNearestSettlementToSettlement(from, Default, cond) (SettlementHelper.cs:31) przeglada wszystkie osady, wiec tylko na zdarzenia. MapDistanceModel.GetNeighborsOfFortification(town, Default) (MapDistanceModel.cs) daje sasiadow z pamieci drog i jest tani. Zamki bez wsi rzadko wypadaja na zapasowa droge.

=== (d) SKAD ZALOGI MAJA DOSWIADCZENIE ===
1. Gra, GarrisonRecruitmentCampaignBehavior.OnDailySettlementTick -> HandleGarrisonXpChange (GarrisonRecruitmentCampaignBehavior.cs:55-73, 110-122): co dobe kazdy czlowiek zalogi dostaje CalculateDailyTroopXpBonus(town) x CalculateGarrisonXpBonusMultiplier(town).
 - DefaultDailyTroopXpBonusModel.cs:16-23: budynki ExperiencePerDay; perki gubernatora RaiseTheMeek i ProjectileDeflection.
 - Budynki: Training Fields miasta 1/2/3 (DefaultBuildingTypes.cs:197), Training Fields zamku 3/4/5 (:256), codzienny projekt zamku "Drills" 8 na dobe (:325).
 - To jest "cwiczenie": zbudowany plac i musztra. ZOSTAJE.
2. BK: tylko mnoznik polityki "garrison" x0.7 przy Dischargement i x1.3 przy Enlistment (BK VanillaModelTweakPatches.cs:603-622). Wlasnego doswiadczenia zalogom nie daje.
3. Gra MobilePartyTrainingBehavior.OnDailyTickParty (MobilePartyTrainingBehavior.cs:43-51) -> model treningu: BKPartyTrainningModel (BK BKPartyTrainningModel.cs:30-122), NavalDLC tylko przekazuje dalej.
 - Dla garnizonu: brak dowodcy i rol, wiec perki dzialaja praktycznie tylko przez gubernatora z BullsEye (:60-67). To umiejetnosc, zostaje.
 - UWAGA, poza zalogami: partie lordow AI dostaja codziennie 15 + 3 x tier (glowa rodu) albo 10 + 2 x tier na czlowieka z niczego (BKPartyTrainningModel.cs:41-50; gra DefaultPartyTrainingModel.cs:21-31). Do osobnej decyzji, nie ruszam.
4. BEE, z niczego, robi paczka 170: ApplyAiTrainingCampPassive (BEE CastleEconomyCampaignBehavior.cs:459, 486-540; GetAiGarrisonXp :2044-2053) daje codziennie CastleTrainingAiGarrisonXpLevel1-3 na zdrowego czlowieka ponizej tieru elity. Sesje treningowe gracza (:880-930) sa platne i z ranami. Wspomniana latka BEE PartyTrainingPatch tylko zeruje dla blednych oddzialow.
5. Bitwy i oblezenia (MapEventParty.cs:453, SiegeEventCampaignBehavior.cs:161): prawdziwe doswiadczenie, zostaje.
6. PROPOZYCJA "cwiczenia wlasna bronia":
 - postfiks Priority.Last na DefaultDailyTroopXpBonusModel.CalculateGarrisonXpBonusMultiplier(Town) mnozy wynik przez udzial uzbrojonych w zalodze: dla glownej broni kazdego oddzialu (luk/kusza u strzelca, inaczej pierwsza bron biala, jak VolunteerKit.IsKey, VolunteerKit.cs:140-162) pokrycie ze zbrojowni DTE = min(1, sztuki koszyka / ludzie), z AiGear.NeedBuckets (AiGear.cs:143) i AiGear.Armories() (AiGear.cs:135);
 - metode wola tylko GarrisonRecruitmentCampaignBehavior.cs:113, raz na twierdze na dobe, wiec koszt to jeden przeglad listy zalogi (kilkanascie pozycji) na twierdze dziennie. Mnoznik BK dziala dalej (mnozenie jest przemienne);
 - bez broni nie ma cwiczen, a doswiadczenie z budynkow zostaje tylko dla uzbrojonych;
 - wylacznik MCM GarrisonDrillNeedsArms, domyslnie wlaczony.
7. Znika zloto, wazne dla decyzji "jak znika to zamykamy": gra PartyUpgraderCampaignBehavior awansuje codziennie takze garnizony (:54-60, 168-186). Oplate za awans placi party.Owner albo dowodca przez GiveGoldAction.ApplyBetweenCharacters(owner, null, ...) (:139-150), czyli zloto znika. W src nikt tego nie przechwytuje (grep UpgradeGold / ApplyEffects pusty). Do osobnej paczki.

