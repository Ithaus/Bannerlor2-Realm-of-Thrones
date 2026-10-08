# Paczka 171 - wynik agenta 1 (workflow wf_e42eab09-42b)

## summary

Rozpoznanie (tylko odczyt, w drzewie nic nie zmienialem). "Bez zapisu (wzorzec)" ma trzy glowne przyczyny:
(1) ROT ROTTroopRecruiter przy kazdym werbunku AI podmienia rekruta na czlowieka z szablonu rodu i strzela DRUGIM OnTroopRecruited(lord, null, null, Y, n). DTE i RecruitKit dostaja wiec dwa zdarzenia na jednego czlowieka, a drugie, bez zrodla, zawsze idzie jako "bez zapisu". Do tego zbrojownia dostaje podwojny komplet.
(2) HouseLevies (nasza latka) co dobe zamienia ok. 350-950 szlacheckich ochotnikow na ludzi rodu, a VolunteerKit tego nie widzi.
(3) Swiezy ochotnik szlachecki BK (noble_recruit, level 11 = tier 2) przychodzi od razu z tierem 2, a VolunteerKit traktuje kazdego nowego jak tier 1 i nic mu nie zapisuje.
Mniejsze dziury: pule zamkow BK (BKNotableBehavior.UpdateVolunteers), najemnicy z karczmy i jency (zrodlo null). Same sieroty (komplet bez ochotnika) robia: Mends.LocalLevies, autowerbunek garnizonu, ekran werbunku gracza w BK, straze majatku BK.
Najprostsza naprawa:
a) pominac ponowne zdarzenie ROT (flaga _firingEvent);
b) w HouseLevies przenosic komplet X na Y;
c) w VolunteerKit zapisywac komplet nowym ochotnikom tieru 2+;
d) ta sama latka VolunteerKit na BKNotableBehavior.UpdateVolunteers (ta sama sygnatura);
e) w RecruitKit zamiast wzorca dawac "to, co ma" (decyzja Jeffa nr 2), co zamyka wszystkie pozostale dziury.

## findings

PRZYCZYNA GLOWNA, CZESC 1: ROT strzela drugim zdarzeniem werbunku (podwojny komplet + zawsze "bez zapisu")
- ROT.dll (dekompilacja: C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8/scratchpad/rot/ROT.CampaignBehaviors/ROTTroopRecruiter.cs):
  - :27 nasluch OnTroopRecruited.
  - :105-126 dla kazdego lorda AI (IsHeroManageable :523-542: nie gracz, nie jego klan, nie karawana) wola ExchangeClanTroops(..., fireEvent: true).
  - :201-202 w rosterze zamienia X na Y z szablonu rodu (DetermineReplacement :222, ten sam tier albo sasiedni).
  - :213-217 `OnTroopRecruited(owner, (Settlement)null, (Hero)null, val, num)`.
  - Rejestracja: ROT/SubModule.cs:294-295; instancja w statycznym polu `ROT.SubModule.Recruiter`; prywatne pole `_firingEvent` (:23) jest true tylko na czas tego ponownego zdarzenia.
- DTE (EveryoneCampaignBehavior.cs:140, :870) przyjmuje oba zdarzenia. AiGear.RecruitKitPrefix (Armoury/src/AiGear.cs:79-89) wola za kazdym razem RecruitKit.OnRecruited.
  - Zdarzenie A (zrodlo = notabl, oddzial X): Pop(notabl, X) daje komplet albo wzorzec.
  - Zdarzenie B (zrodlo null, oddzial Y): Pop(null, Y) zawsze daje null, czyli RecruitKit.cs:177 "bez zapisu (wzorzec)", czyli drugi komplet z niczego do zbrojowni (:178-182).
- Dowod z logu (Armoury-2026-10-08_11-40-05.log, linia "Ludzie ... zwerbowani"). "Bez osady" (zrodlo i osada null) prawie rowna sie werbunkowi od notabli plus z karczmy, a w nowej kampanii jencow jeszcze nie ma:

| Dzien | Bez osady | Od notabli | Z karczmy |
|---|---|---|---|
| 108836 | 2044 | 1804 | 608 |
| 108837 | 3010 | 2557 | 418 |
| 108838 | 2809 | 2493 | 366 |
| 108839 | 2549 | 2205 | 338 |
| 109196 (zapis z doby 360) | 674 | 540 | 65 |

  Wniosek: prawie kazdy werbunek AI ma swoj duplikat. W linii "Komplet rekruta" duplikat tieru 2+ zawsze trafia do "bez zapisu", a tieru 1 do "tier 1" (te liczniki tez sa zawyzone mniej wiecej dwukrotnie).

PRZYCZYNA GLOWNA, CZESC 2: pule zmieniane poza VolunteerKit
- VolunteerKit lata TYLKO `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement` (VolunteerKit.cs:268-277, prefix i postfix z roznica multizbiorow :39-89).
- HouseLevies.Convert (Armoury/src/HouseLevies.cs:63-76, wpiety :34-38 w DailyTickSettlement i SettlementEntered gracza). Szlachecki X zamienia na czlowieka rodu Y tego samego tieru (`slots[i] = repl`). Komplet X zostaje sierota, a Y nie ma zapisu i przy werbunku idzie wzorzec.
  - Log: "HouseLevies: dzien 108837 - 952" (etykieta przesunieta o dobe: to zamiany z doby 108836), potem 638, 462, 352.
  - Uzgodnienie po 1. dobie: "kompletow 1565 -> 308, nadmiar wobec puli 1257". Z 1520 kompletow ColdStart po dobie zostalo 308, a najwiekszy kawalek tego spadku to wlasnie HouseLevies.
- Swiezy ochotnik tieru 2. BKVolunteerModel.GetBasicVolunteer (bk/BannerKings.Models.Vanilla/BKVolunteerModel.cs:272-333) daje z puli Nobles (RecruitSpawns) albo kaplanowi druidow (:286-294) EliteBasicTroop kultury. W ROT to river_/reach_/crownlands_/stormlands_noble_recruit, vale_page, imperial_vigla_recruit, battanian_highborn_youth: wszystkie level 11, czyli tier 2.
  - VolunteerKit.cs:79 `if (x == null) continue; // nowy ochotnik (tier 1)` niczego nie zapisuje, wiec przy werbunku RecruitKit.cs:177 daje wzorzec.
  - To samo tlumaczy 1520 ochotnikow tieru 2+ w pulach zaraz po pierwszym napelnieniu (ColdStart): po jednym przebiegu nikt nie zdazy awansowac, wiec to musza byc swiezi ochotnicy tieru 2.
  - Do tego, gdy taki ochotnik bez zapisu awansuje, RecruitKit.OnUpgrade :61-66 daje mu za darmo czesci wzorca X wspolne z Y. To przyblizenie tez jest "z niczego".

KAZDE MIEJSCE, KTORE ZMIENIA Hero.VolunteerTypes (plik:linia / kiedy biegnie / czy VolunteerKit to widzi)

Gra 1.4.8 (scratchpad/ore-supply/cs):
1. RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement (TaleWorlds.CampaignSystem.CampaignBehaviors/RecruitmentCampaignBehavior.cs:215-289): nowy ochotnik :236-239, awans :241-247, sortowanie :255-288. Biegnie w DailyTickSettlement :145-148 i przy nowej grze :166-176. Tylko miasta i wsie (:217), zamki odpadaja. VolunteerKit: TAK, ale nowego ochotnika tieru 2+ traktuje jak tier 1 (patrz wyzej).
2. RecruitmentCampaignBehavior.ApplyInternal :606-639: zeruje slot :626 (VolunteerFromIndividual) i :634 (ToGarrison), potem OnTroopRecruited :638. Wywolania: HourlyTickParty :291, CheckRecruiting :414, RecruitVolunteersFromNotable :504-561, OnBeforeSettlementEntered :563-604. VolunteerKit: nie musi, bo komplet zdejmuje RecruitKit.OnRecruited. Wyjatek ToGarrison: LeaderHero garnizonu = null, wiec AiGear.cs:83 i DTE wychodza, Pop sie nie dzieje i zostaje sierota.
3. GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange (GarrisonRecruitmentCampaignBehavior.cs:76-95, :92 `= null`). Dzienny autowerbunek garnizonu (CanSettlementAutoRecruit :236), BEZ zadnego zdarzenia. VolunteerKit: NIE. Skutek: sierota (Reconcile ja sprzedaje), a ludzie trafiaja do garnizonu bez kompletu. Wazne dla paczki 171 (zalogi).
4. RecruitPrisonersCampaignBehavior.cs:117: OnTroopRecruited(lord, null, null, ...). Pul nie zmienia, ale jeniec tieru 2+ idzie jako "bez zapisu", czyli komplet wzorca z niczego (jeniec nie powinien miec nic).
5. Hero.cs:1502 (new CharacterObject[6]), :1960 (= null przy smierci). Reconcile (RecruitKit.cs:120) sobie z tym radzi.

BannerKings (scratchpad/ore-supply/bk):
6. BKNotableBehavior.UpdateVolunteers (BannerKings.Behaviours/BKNotableBehavior.cs:194-284; nowy :230-234, awans :235-245, sortowanie :250-283) z HandleCastles :157-163 w DailyTickSettlement :47. Tylko zamki i tylko Notables[0]. VolunteerKit: NIE. Awanse w zamkach sa darmowe i bez zapisu.
7. BKNotableBehavior.ExtendVolunteersArray :166-191 (wczytanie :55-58, koniec tworzenia postaci :84-87): nowa tablica dlugosci VolunteersLimit z przepisanymi wpisami. Skladu nie zmienia, wiec nieszkodliwe (gra wypelnia i tak tylko i<6).
8. RecruitmentOnDonePatch (BannerKings.UI.Patches/RecruitmentOnDonePatch.cs:18-36): ekran werbunku GRACZA. Zeruje slot :34 i strzela tylko OnUnitRecruited :36, bez OnTroopRecruited. Galaz gracza w RecruitKit.cs:167 nie rusza, wiec zostaje sierota. Gracz: osobno, bez zmian.
9. BKLandsLaborBehavior.TryGuardTick (BannerKings.Behaviours.Estates/BKLandsLaborBehavior.cs:253-322, :318 `= null`, :322): straze majatku gracza (placi Hero.MainHero), dzienne, bez zdarzenia. Zostaje sierota. Tylko gracz.
10. Odczyt bez zapisu: BKAIVisitSettlementBehavior.cs:381, NotablePatches.cs:114-116, BKVolunteerModel.cs:61. RecruitmentApplyInternalPatch.cs to postfix ApplyInternal (manpower), pul nie zmienia.

ROT (dekompilacja w C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8/scratchpad/rot):
11. ROTOthersCampaignBehavior.ChangeNotableCulture :1614-1625: `VolunteerTypes = new CharacterObject[6]` przy zmianie osady na kulture "Others". Rzadkie, zostaja sieroty.
12. ROTTroopRecruiter: pul NIE zmienia (opis wyzej). ExchangeRoster przez DailyTickParty :81-91 i lup :93-103 podmienia ludzi w partiach bez zdarzenia (fireEvent false). Na pule i komplety nie wplywa.

Inne:
13. BetterEconomy PopulationCampaignBehavior.ApplyRecruitQualityDrift (be/BetterEconomy.Behaviors/PopulationCampaignBehavior.cs:396-470, zapis :462): z BK nieczynne (warunek HasBannerKings :398).
14. DTE, Diplomacy 1.4.7 i BKROTPatch: zero zapisow (przeszukalem dekompilacje). NavalDLC: tylko odczyt w questcie (FreeTheSeaHoundsCaptivesQuest.cs:257). Przeszukanie wszystkich DLL w Modules po "VolunteerTypes" znalazlo tylko Armoury, BK, BE, CrashScribe, NavalDLC, ROT, StoryMode.
15. Nasze:
  - CrashScribe Mends.LocalLevies (CrashScribe/src/Mends.cs:4133-4185, `vt[i] = null` :4174): codziennie dla wszystkich osad (WarReport.cs:22), po wczytaniu (:21) i przy wjezdzie gracza (:69). Zeruje ochotnikow obcej kultury. VolunteerKit: NIE, zostaja sieroty.
  - HouseLevies (wyzej): NIE widzi.
  - Levy.ProbabilityPostfix (Levy.cs:89-101): tylko szansa, nie zapis.
  - LevyGold.ApplyInternalPostfix (LevyGold.cs:26-, wpiety :123-124): tylko zloto.

SCIEZKA AI DO "BEZ ZAPISU" (RecruitKit.cs:163-185): troop.Tier >= 2, lord AI z partia i Pop(recruitmentSource, troop) == null. Dzieje sie to, gdy:
- (a) recruitmentSource == null: duplikat ROT, karczma (ApplyInternal MercenaryFromTavern, individual null), jency;
- (b) notabl bez zapisu dla tego oddzialu: swiezy szlachcic tieru 2, zamiana HouseLevies, pula zamku BK;
- (c) zapis zgubiony przy wczytaniu ("brak notabla 303").

NAJPROSTSZA NAPRAWA (zeby kazdy ochotnik tieru 2+ mial znany komplet)
1. Duplikat ROT. Na poczatku AiGear.RecruitKitPrefix: gdy recruitmentSource == null i ROT.SubModule.Recruiter._firingEvent == true, zwrocic false (DTE nic nie dodaje, bo czlowiek dostal juz komplet w zdarzeniu A, a ROT tylko zmienil mu znaczek). Koszt: jeden odczyt pola z zapamietanym FieldInfo/AccessTools. Zabiera mniej wiecej polowe "bez zapisu" i usuwa podwojny komplet. Opcjonalnie przeniesc zapis: lepiej po prostu nie dawac nic drugi raz.
2. HouseLevies.Convert :73: przed `slots[i] = repl` wolac nowe RecruitKit.OnSwap(notable, troop, repl), czyli Pop(X) i Push(Y, Materialize(X)): ten sam czlowiek, ten sam dobytek. Bez nowych przegladow.
3. VolunteerKit.Postfix :79: dla `x == null` i `y.Tier >= 2` zapisac komplet "to, co ma". Najprosciej Buy(n, market, kultura.BasicTroop, y) jak przy awansie, a przy porazce cofnac slot do BasicTroop kultury. Albo, zgodnie z decyzja Jeffa nr 2, zapis Kit{Troop=y, Items = czesci wzorca y wspolne z BasicTroop kultury}.
4. Zamki BK: ta sama para VolunteerKit.Prefix/Postfix na `BannerKings.Behaviours.BKNotableBehavior:UpdateVolunteers(Settlement)`. Sygnatura identyczna, wystarczy jedno h.Patch w VolunteerKit.ApplyAll.
5. Siatka bezpieczenstwa (decyzja Jeffa nr 2): RecruitKit.cs:177 zamiast TemplateItems(troop) dawac "to, co ma" (czesci wspolne z tier 1 kultury, albo nic u jenca). Wtedy kazda pozostala dziura (karczma, jency, zgubiony zapis, nieznane mody) przestaje dawac sprzet z niczego, a dozbraja pan przez zakupy AI.
   - Najemnik z karczmy: do decyzji, czy ma wlasny sprzet. Proponuje ten sam fallback; jego cena i tak zawiera sprzet (RecruitCost).
6. Sieroty (autowerbunek garnizonu GarrisonRecruitmentCampaignBehavior.cs:76-95, Mends.LocalLevies, gracz BK, straze majatku, ROT Others) juz sprzata codzienny RecruitKit.Reconcile. Autowerbunek garnizonu warto zalatac w 171: prefix/postfix na TickAutoRecruitmentGarrisonChange z roznica pul, a zabrany komplet do zbrojowni garnizonu.
7. Diagnoza do testu: w linii "Komplet rekruta" rozbic "bez zapisu" na: duplikat ROT / zrodlo null (karczma, jency) / notabl bez zapisu. Wtedy po paczce widac, ile zostalo.

Gracz: jego werbunek idzie przez ekran BK (OnUnitRecruited), komplet DTE bez zmian, a jego zapisy zostaja sierotami sprzatanymi przez Reconcile. ROT gracza nie podmienia (IsHeroManageable). Bez potrzeby nie zmieniac.

