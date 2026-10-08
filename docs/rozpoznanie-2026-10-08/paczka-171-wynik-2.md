# Paczka 171 - wynik agenta 2 (workflow wf_e42eab09-42b)

## summary

Rozpoznanie 2 (tylko odczyt, nic nie zmienione). Wiekszosc tego, co log nazywa "bez zapisu (wzorzec)", to nie ochotnicy bez zapisu. Glowne zrodlo to podwojne zdarzenie werbunku z ROT. ROT.CampaignBehaviors.ROTTroopRecruiter.ExchangeClanTroops zamienia zwerbowanego X na oddzial rodu Y tego samego tieru i wysyla drugie OnTroopRecruited(lord, settlement=null, source=null, Y). DTE i RecruitKit dostaja oba zdarzenia, wiec prawie kazdy rekrut AI wnosi dwa komplety: swoj (X) i wzorca Y z niczego. Potwierdzenie z logu: liczba "bez osady" jest co dobe prawie rowna sumie "od notabli + z karczmy" (3010 wobec 2975, 2809 wobec 2859, 2549 wobec 2543). Faktyczny werbunek AI to ok. 2800 ludzi na dobe na starcie i ok. 600 po roku, nie 5-6 tys. Prawdziwi ochotnicy tieru 2+ od notabla bez zapisu to na starcie tylko ok. 270 na dobe, po roku ok. 40. Proponowana regula "rekrut przychodzi z tym, co ma" ma piec przypadkow: (1) duplikat ROT to ten sam czlowiek, bez drugiego kompletu; (2) jeniec przychodzi z niczym, bo jego rzeczy wzial zwyciezca jako lup; (3) ochotnik tieru 2+ bez zapisu dostaje dobytek tieru 1 swojej linii awansu, a jak linii tieru 1 nie ma - bez broni; (4) tier 1 i komplety z zapisu bez zmian; (5) najemnik z karczmy wedlug tej samej reguly co (3), lord dokupuje braki od razu na polce tego miasta. Skutek wedlug logow: na starcie ok. 2700-3400 kompletow mniej z niczego na dobe (ok. 12-15 tys. sztuk), po roku ok. 700-740 kompletow (ok. 3,2 tys. sztuk). Dla porownania: warsztaty robia 554-847 sztuk na dobe, zakupy AI to 3,5-4,3 tys. sztuk. Liczba sztuk na komplet to szacunek ok. 4,5 (z ColdStart). Log nie rozbija werbunku na tiery 2-6, tylko tier 1 i tier 2+.

## findings

## A. Jak dzis sprzet trafia do zbrojowni partii AI

Zbrojownia DTE to slownik EveryoneCampaignBehavior.PartyArmories: MBGUID partii -> {ItemObject: ile}. Nie ma w nim stanu sztuk; stan prowadzi osobno AiWear (_worn).

**1. Werbunek: zdarzenie OnTroopRecruited**
- AiGear.RecruitKitPrefix jest prefiksem na DTE.OnTroopRecruited.
- Gdy werbuje gracz, RecruitKit tylko zdejmuje komplety notabla, a DTE daje pelny wzorzec.
- Gdy AiRecruitsBringKit=false, rekrut nie dostaje nic.
- W pozostalych przypadkach decyduje RecruitKit.OnRecruited:
  - tier <=1: `_dayTier1++` i return true, czyli DTE wklada pelny wzorzec (GetRecruitEquipments = RandomBattleEquipment, mozliwie zrandomizowane);
  - tier 2+ z zapisem u notabla (Pop(source, troop)): Materialize, czyli to, co notabl kupil, plus dobytek; `_dayKits++`;
  - tier 2+ bez zapisu: TemplateItems(troop), czyli FirstBattleEquipment z niczego, przez AiGear.AddToArmory; `_dayLegacy++`.
- Pop szuka po parze (notabl, oddzial). Gdy source == null (karczma, jency, ROT), zawsze trafia do galezi "wzorzec".

**2. Zapisy kompletow (VolunteerKit + RecruitKit)**
- Postfiks na RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement widzi awanse X->Y. Notabl kupuje na targu MarketOf brakujace czesci kluczowe; brak towaru albo zlota cofa awans.
- RecruitKit.OnUpgrade przenosi komplet. Gdy X nie ma zapisu, liczy sie czesc wspolna wzorcow X i Y.
- Reconcile raz na dobe usuwa komplety bez ochotnika w puli: kupione rzeczy idzie na targ, notabl dostaje zloto.
- Zmian w pulach tej latki nie widzi:
  - BK BKNotableBehavior.UpdateVolunteers, czyli awanse u notabli zamkow, za darmo;
  - Garrison auto-recruit (GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange:90). Bierze ochotnika z puli bez zdarzenia, wiec zaloga dostaje go nagiego, a jego komplet zostaje sierota i jest sprzedawany;
  - BEE ApplyRecruitQualityDrift przy BK sie nie uruchamia (`ModCompatibility.HasBannerKings`), wiec nie jest dziura.

**3. ColdStart (raz, nowa gra)**
- Zbrojownie: brakujace sztuki kompletu ludzi z niczego ("dorobek stuleci").
- Targi: zapas na 14 dni pracy rzemieslnikow.
- RecruitKit.SeedCampaignStart daje komplet Template=true kazdemu ochotnikowi tieru 2+ w pulach.

**4. Nowe partie**
- LevyGold.PartyCreatedPrefix blokuje OnMobilePartyCreated DTE po starcie gry. Gracz bez zmian.

**5. StartKit**
- Dotyczy tylko gracza: podmiana sprzetu startowego, ktorego nie moze nosic, i zloto startowe BK. Nie dotyczy AI.

**6. QuartermasterLaw.KitTypes / NeedForType / HaveFor**
- Tylko partia gracza. MenPurse.SellPlayerSurplus i BuyPlayerGaps liczy dopasowanie po umiejetnosci (CountNeeds/FitFor). AI z tego nie korzysta.

**7. AiWear**
- Komplety rekrutow i zakupy wchodza jako sprawne, chyba ze kupiona sztuka ma modyfikator <1 (NoteBought).
- Po bitwie zuzywa sie TroopWearPercent sztuk w uzyciu.
- Naprawy w miescie raz na dobe z wspolnej puli godzin kowali (SmithHours) z materialem (MendMaterial). Placi sakiewka ludzi.

**8. Zaloga zamku/miasta**
- Do zbrojowni zalogi nie wchodzi zaden komplet z werbunku. Auto-recruit, przyrost podstawowy (TickGarrisonChangeForTown:106), wymiana ROT DailyTickSettlement.ExchangeRoster i BK BKSettlementBehavior:723 dzialaja bez OnTroopRecruited.
- Zaloga ma tylko to, co kupi przez AiGear.TryBuy.

## B. Jak partia lub zaloga uzupelnia braki (AiGear.TryBuy)

**Kiedy**
- Przy wejsciu do osady (SettlementEntered) i w dziennym ticku postoju (OnDailyTickParty; najpierw AiWear.MendInTown).
- Raz na partie na dobe (_lastDay).

**Kto kupuje i gdzie**
- Partia lorda AI (IsLordParty, nie gracz) w kazdym miescie albo zamku, ktory nie jest wrogi.
- Zaloga (GarrisonBuysGear) tylko we WLASNEJ osadzie (`mp.CurrentSettlement == st`). W zamku oznacza to polke zamku.
- Zalogi gracza tylko przy GarrisonBuysGearPlayer (domyslnie wylaczone).

**Z czyjej kiesy**
- Partia: budzet = (zloto lorda - AiGearGoldReserve 2000) x AiGearBudgetPercent 25% + sakiewka ludzi (MenPurse.Get minus zalegle naprawy, AiWear.OutstandingCost). Najpierw placi sakiewka (MenPurse.Take), reszta z lorda.
- Zaloga: zloto `st.OwnerClan.Leader`, ta sama formula, bez sakiewki.
- Zloto idzie do st.Town.ChangeGold i do MoneyLedger NGear.

**Jak liczy potrzeby**
- Dla kazdego oddzialu (bez bohaterow) sloty 0-9 z ch.Equipment, czyli wzorca (bez koni), razy liczba ludzi.
- Koszyk = typ*10 + tier (TierOf = Tier+1, przyciete do 1-6).
- Odejmuje stan zbrojowni. Zapas w innym tierze tego samego typu pokrywa brak: najpierw wyzsze tiery, potem t-1.

**Co kupuje**
- Kolejnosc typow: korpus, 1H, 2H, drzewce, luk, kusza, tarcza, helm, strzaly, belty, rzucane, nogi, rece, plaszcz. Tiery od 6 do 1.
- Kandydat: ten sam typ, tier t albo t-1, bez unikatow, najlepsza Effectiveness/cena (MarketData.GetPrice).
- Limit AiGearMaxPiecesPerVisit=60 sztuk na wizyte.
- Sztuka kupowana jest z modyfikatorem z polki.
- Gdy na polce nie ma nic danego typu i tieru, powstaje zamowienie SupplyDemand.NoteUnmetOnce (najwyzej 10).

**Nadwyzki**
- MenPurse.SellAiSurplus sprzedaje nadwyzki po typie. Lord dostaje trzecia czesc.

## C. Liczby z logow

### Nowa kampania (Armoury-2026-10-08_11-40-05.log, doby 108836-108839)

**ColdStart**
- Zbrojownie: 344 099 sztuk dla 262 partii (w tym 227 zalog). To ok. 4,45 sztuki na czlowieka przy 77 tys. ludzi.
- Targi: 10 691 sztuk w 97 miastach, wartosc 370 281 d.
- Komplety ochotnikow: 1520 zasianych. Juz pierwsze uzgodnienie z pulami: 1565 -> 308, 1257 sierot (ochotnik zniknal z puli bez zdarzenia; najpewniej auto-recruit zalog).

**Ludzie: zwerbowani na dobe**

| Doba | Razem | Od notabli | Z karczmy | Bez osady |
|---|---|---|---|---|
| 108836 | 4456 | 1804 | 608 | 2044 |
| 108837 | 5985 | 2557 | 418 | 3010 |
| 108838 | 5668 | 2493 | 366 | 2809 |
| 108839 | 5092 | 2205 | 338 | 2549 |

Jencow w partiach jest tylko 46-297, wiec "bez osady" to niemal wylacznie duplikaty ROT.

**Komplet rekruta na dobe**

| Doba | Z zapisem | Wzorzec | Tier 1 |
|---|---|---|---|
| 108836 | 170 | 646 | 2406 |
| 108837 | 68 | 1522 | 4396 |
| 108838 | 35 | 1415 | 4002 |
| 108839 | 29 | 1075 | 3706 |

- Rozklad na prawdziwych ludzi (srednia 108837-108839, duplikaty ROT maja ten sam tier):
  - tier 1: ok. 2020 prawdziwych i ok. 2020 duplikatow;
  - tier 2+: ok. 690 prawdziwych, w tym ok. 375 najemnikow, ok. 44 z zapisem i ok. 270 od notabla bez zapisu; do tego ok. 690 duplikatow.
- Rozbicia na tiery 2-6 log nie ma.

**ZakupyAI na dobe**

| Doba | Wizyty | Sztuki | Zloto | W tym zalogi |
|---|---|---|---|---|
| 108836 | 71 | 2633 | 75 378 | 20 zakupow, 16 680 |
| 108837 | 155 | 4280 | 122 693 | 89 zakupow, 46 465 |
| 108838 | 128 | 3555 | 129 082 | 78 zakupow, 65 636 |
| 108839 | 148 | 3634 | 127 994 | 80 zakupow, 57 928 |

Pojedynczych linii zakupow w logu brak.

**Ochotnicy: awanse z kupionym sprzetem**
- 181 (590 sztuk, 48 920 zl), 172 (402 sztuki, 38 386 zl), 128 (275 sztuk, 16 038 zl), 97 (219 sztuk, 29 788 zl).
- Cofniete: 195, 518, 509, 534.

**Sakiewka: nadwyzki sprzedane**
- 1126, 4957, 3596, 3986 sztuk na dobe, za 1,6-9,2 tys.; trzecia czesc lordow 249-2090.
- To w duzej mierze "stare" komplety X po zamianie ROT.

**LevyGold**
- Nowe partie AI bez darmowego kompletu: 552, 1303, 591, 277.
- Zloto za ochotnikow do notabli 32-61 tys. na dobe, za najemnikow do miast 72-111 tys.

**Warsztaty: wykonano na dobe**
- 463, 744, 554, 847 sztuk, glownie LegArmor, Cape, HeadArmor.
- Bron: 1H 1-22, 2H 0-18, drzewce 15-46, luk 0-9, kusza 2-14, tarcza 1-5.
- Odpuszczone z braku surowca: 406-1119 (glownie ruda).
- Rzemioslo miasta (148) to tylko len, welna i skory (plotno, filc, skora), nie bron.

**PodazPopyt**
- Kupcy przewiezli 2,2-2,4 tys. sztuk na dobe miedzy miastami; 275-424 sztuk bez odbiorcy.

### Zapis z doby ok. 360 (Armoury-2026-10-08_11-32-39.log, doby 109196-109197)

**Komplety po wczytaniu**
- 12 624 z 12 927; uzgodnienie 12 624 -> 11 554 (nadmiar 1070, sprzedane 1549 sztuk). Potem 11-20 sierot na dobe.

**Ludzie, doba 109196**
- Zwerbowani 1279: od notabli 540, z karczmy 65, bez osady 674. To ok. 605 duplikatow ROT i ok. 70 jencow (w partiach 6172-7009 jencow).

**Komplet rekruta**
- 109196: z zapisem 184, wzorzec 473, tier 1 409.
- 109197: z zapisem 198, wzorzec 603, tier 1 519.
- Prawdziwych ochotnikow tieru 2+ od notabla bez zapisu jest juz tylko ok. 40 na dobe.

**ZakupyAI**
- 215 wizyt, 9659 sztuk, 890 491 zl; zalogi 148 zakupow za 820 362.
- 227 wizyt, 10 525 sztuk, 896 495 zl; zalogi 168 zakupow za 834 839.
- Zalogi wydaja ok. 92% zlota.

**Ochotnicy**
- 229 awansow (893 sztuki, 51 464 zl), 280 awansow (1083 sztuki, 44 860 zl); cofniete 155 i 195.
- Powody cofniec: BodyArmor t6 i t5, OneHandedWeapon t5, HorseHarness.

**Sakiewka**
- Nadwyzki sprzedane 8509 i 9739 sztuk na dobe.

**Warsztaty**
- Tylko 115 i 204 sztuk na dobe (bez zysku 214-324).

**PodazPopyt**
- Wywieziono 6,2-13,7 tys. sztuk, ale 147-155 tys. sztuk uzbrojenia lezy bez odbiorcy. Targi sa przepelnione, wiec zakupy zamiast kompletow z niczego maja z czego brac.

## D. Proponowana regula "rekrut przychodzi z tym, co ma" (tylko AI; gracz bez zmian)

**1. Zamiana ROT = ten sam czlowiek, bez drugiego kompletu**
- Wykrycie: Harmony prefix/postfix na ROTTroopRecruiter.ExchangeClanTroops (AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter")) ustawia licznik glebokosci.
- W RecruitKitPrefix: gdy licznik >0 i source == null, to return false (DTE nic nie daje).
- Komplet X zostaje w zbrojowni i pokrywa koszyki Y (ten sam typ i tier). Braki dokupi AiGear, a nadwyzke sprzeda MenPurse.
- Ten sam licznik niech pominie duplikat w PeopleLedger (tylko log).
- Skutek: start ok. 2700 kompletow na dobe mniej (ok. 12 tys. sztuk), po roku ok. 605 (ok. 2,7 tys.).
- Spadna tez sprzedaze "nadwyzek z niczego" (3,6-5 tys. sztuk na dobe na starcie) i trzecia czesc dla lorda.

**2. Jeniec zwerbowany przez AI: nic**
- Uzasadnienie: obszukany jeniec ma lachmany (CaptiveRags/RealisticCaptivity), a jego sprzet zwyciezca juz wzial w lupie DTE.
- Wykrycie: flaga na RecruitPrisonersCampaignBehavior.RecruitPrisonersAi.
- Skutek: start ok. 0-100 na dobe, po roku ok. 70 kompletow.

**3. Ochotnik tieru 2+ od notabla bez zapisu: dobytek tieru 1 swojej linii awansu**
- Daje TemplateItems przodka tieru 1 (mapa rodzicow z UpgradeTargets liczona raz na sesje, przodek tej samej kultury). Gdy linii tieru 1 nie ma, przychodzi bez broni i zbroi.
- Uzasadnienie: zaczynal jako tier 1 z wlasnym dobytkiem (regula przyjeta 05.10); kupna nikt nie zapisal, wiec zakladamy, ze go nie bylo. Reszte kupuje pan (AiGear).
- Nie bierzemy sztuk z kompletu poprzedniego szczebla, bo dla szczebla 2+ to nadal wzorzec z niczego.
- Skutek: start ok. 270, po roku ok. 40 kompletow tieru 2+ zamienionych na tanie rzeczy tieru 1.
- Uboczne: sztuki tieru 1 nie trafia w koszyk t/t-1 tieru 3+ i pojda jako nadwyzka do MenPurse (realne, tanie).

**4. Bez zmian**
- Tier 1 dostaje wlasny dobytek (komplet DTE). Komplet z zapisem notabla bez zmian.

**5. Najemnik z karczmy (start 340-610, po roku 65 na dobe)**
- Rekomenduje te sama regule co w pkt 3. Lord stoi wtedy w tym miescie, wiec niech AiGear.TryBuy moze ruszyc jeszcze raz tego dnia zaraz po werbunku (wyjatek od _lastDay raz na dobe na "po werbunku"). Dozbraja go z polki tego miasta. To dokladnie decyzja Jeffa (2): "dozbraja go pan, kupujac na targu".
- Doc RecruitCost mowi, ze komplet i kon najemnika sa z niczego "do kroku weterani". Ta regula ten krok zamyka.
- Bez tego zostaje jedyna sciezka z niczego (ok. 1,7 tys. sztuk na dobe na starcie).

**Lacznie**
- Start: ok. 2700-3400 kompletow mniej z niczego na dobe (ok. 12-15 tys. sztuk).
- Po roku: ok. 700-740 kompletow (ok. 3,2 tys. sztuk).
- Liczby sztuk to szacunek przy 4,5 sztuki na komplet.

**Do zrobienia przy okazji (tanie)**
- Wylacznik MCM (np. RecruitBringsOwnOnly, osobno RotSwapSameMan), domyslnie wlaczony.
- Linia "Komplet rekruta" rozbita na sciezki (notabl z zapisem / notabl bez zapisu / karczma / jeniec / duplikat ROT) i na tiery t1-t6. Dzis log nie pozwala podac werbunku wedlug tieru 2-6.
- Diagnoza sierot ColdStart (1257 pierwszego dnia): licznik ochotnikow zabranych przez zaloge (TickAutoRecruitmentGarrisonChange) i awansow BK w zamkach.
- Pod paczke 171 (zaloga): przy auto-recruit zalogi komplet ochotnika z zapisu powinien isc do zbrojowni zalogi, a nie na sprzedaz w Reconcile.

**Gracz, osobno**
- Jego werbunek: DTE RecruitmentPatch (RecruitmentVM.OnDone) daje pelny wzorzec do ArmyArmory. RecruitKit tylko zdejmuje zapisy u notabla.
- ROT nie zamienia gracza ani bohaterow jego klanu (IsHeroManageable = false).
- Partie towarzyszy klanu gracza ida sciezka AI RecruitKit, ale duplikatu ROT nie maja.
- Zalogi gracza nie kupuja przy domyslnych ustawieniach (GarrisonBuysGearPlayer=false).

## Pliki

**Kod (drzewo robocze)**
- C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/dzien-6/zaloga171/repo/Armoury/src/RecruitKit.cs (OnRecruited 163-185)
- tamze AiGear.cs (RecruitKitPrefix 79-89, TryBuy 170-320)
- tamze VolunteerKit.cs
- tamze ColdStart.cs
- tamze LevyGold.cs (PartyCreatedPrefix)
- tamze PeopleLedger.cs (134-142)
- tamze MenPurse.cs (SellAiSurplus 314+)

**Dekompilacje w moim scratchpadzie (tylko do odczytu)**
- ROT: C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8/scratchpad/rot/ROT.CampaignBehaviors/ROTTroopRecruiter.cs (ExchangeClanTroops; drugie OnTroopRecruited ok. linia 216)
- DTE: .../fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8/scratchpad/dte/DynamicTroopEquipmentReupload/EveryoneCampaignBehavior.cs (OnTroopRecruited 870, OnMobilePartyCreated 468)

**Gra**
- ore-supply/cs/TaleWorlds.CampaignSystem.CampaignBehaviors/RecruitPrisonersCampaignBehavior.cs:117 (jency, source null)
- ore-supply/cs/TaleWorlds.CampaignSystem.CampaignBehaviors/GarrisonRecruitmentCampaignBehavior.cs:76-107 (zaloga bez zdarzenia)
- ore-supply/bk/BannerKings.Behaviours/BKNotableBehavior.cs:194+ (awanse w zamkach poza VolunteerKit)

**Logi**
- C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/Armoury-2026-10-08_11-40-05.log
- C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/Armoury-2026-10-08_11-32-39.log

