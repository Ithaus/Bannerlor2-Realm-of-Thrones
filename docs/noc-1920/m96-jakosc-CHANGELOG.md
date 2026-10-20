# m96-jakosc - legendarne i mistrzowskie rzadkie, limit swiata (20.10)

**Problem (Jeff 20.10, gra na WGRANIU 95):** "za duzo jest legendary przedmiotow, legendarne przedmioty to kilka sztuk na swiecie, masterwork juz wiecej itp.,
a w sklepach za latwo mozna znalezc legendary i masterwork". Chodzi o jakosc (modyfikatory gry: Legendary, Masterwork / Lordly, Fine / Balanced ...),
nie o legendy z imienia (te pilnuje LegendaryLaw).

**Dowod:**
- Zaden log nie liczyl jakosci (grep "legendary_|masterwork_|lordly_" w biegu 160 dob 12-25-19: jedno trafienie, nazwa przedmiotu). Liczby z innych linii:
- Jeff 20.10 (Armoury-2026-10-20_13-28-19.log, 14:14:07, dzien 108861): "Zakupy i nadwyzki AI (E8/Z): dodatnie stany: odrzucenia kandydatow 3505 w 166 wizytach,
  kupione dodatnie stany 0 szt. ... brak zostal przy polce ze sztuka nie dla AI: dodatni stan 2070 szt." - na polkach ok. 21 sztuk z dodatnim stanem na jedna wizyte
  zakupow, AI nie kupuje zadnej (AiSkipFineGoods - zbrojownia DTE to Dictionary<ItemObject, int>, jakosc by przepadla), wiec leza na zawsze.
- Ta sama doba: "Uzbrojenie wedlug zrodla (E6/0): zrodla: warsztaty 2030" sztuk na dobe; bieg 160 dob (12-25-19): warsztaty 1005/d, polki osad 209 638 szt.
- Wyliczenie z wag (ponizej): u Jeffa ok. 88 legendarnych i 177 mistrzowskich NA DOBE z samych warsztatow AI; w biegu 160 dob ok. 44 i 87 na dobe - tysiace
  legendarnych na polkach po kilku miesiacach. Dokladna liczba - pierwsza linia nowej paczki po wczytaniu ("spis przed wyrownaniem").

**Przyczyna (dekompilacja + XML):**
- `ItemModifierGroup.GetRandomItemModifierProductionScoreBased` = losowanie wedlug `production_drop_score`. Native item_modifiers_groups.xml: brak modyfikatora 75;
  RBM (RBMCombat_item_modifiers.xml, scala sie z Native atrybut po atrybucie): legendary 5, masterwork/lordly 10, fine 15, inferior 5, poor 5 -> **legendarna 4.3%,
  mistrzowska 8.7%, dobra 13% kazdej sztuki**, bez wzgledu na tier i na to, kto kuje.
- Kto tak losuje: Armoury `WorkshopLaw.cs:334` (kazda gotowa sztuka warsztatu AI - glowne zrodlo), gra `WorkshopsCampaignBehavior.GetRandomItemAux` (warsztaty gracza,
  linie gry; `CraftRealm.FilterRandom` losuje tak samo), `HeroGear.cs:315` i ROT `ROTGankBehavior.GetRandomItem` (zamiennik broni jenca).
  Wagi lupu (`loot_drop_score`: brak 45, legendary 2, masterwork 7, fine 10): `DefaultBattleRewardModel.GetRandomItem` (lup gracza z poleglych - AI dostaje tylko
  zloto, MapEvent:1862) i ROT `ROTEnlistmentBehavior.GetRandomItem` (nagroda zaciagu) - legendarna 2.2%, mistrzowska 7.6%.
- Bez jakosci: TownFletchers, ColdStart, warsztaty BK (`EconomyPatches.ProduceOutputPrefix` kladzie sztuke bez modyfikatora), zbrojownie DTE (bez stanu).
  Kuznia gracza (Forge.RollQuality, ArmourQuality, BK CraftingMixin) i ulepszenie u kupca ROT (gracz placi) - sprawa gracza, bez zmian.
- Miasto placi warsztatowi cene ZWYKLEJ sztuki (`WorkshopLaw.Revenue(town, ItemObject)`) - jakosc byla wartoscia z niczego.

**Zmiana (nowy plik `Armoury/src/QualityScarcity.cs`, klucz Quality Scarcity, domyslnie TAK):**
1. Powstanie sztuki (warsztaty AI, droga gry, lup gracza, jeniec ROT, nagroda zaciagu ROT): rzut gry Legendary/Masterwork przepada (sztuka zwykla). Wlasny rzut:
   legendarna tylko z tieru 5-6 (Quality Legendary Chance 0.001 = 1 na 1000 takich sztuk), mistrzowska z tieru 4-6 (Quality Masterwork Chance 0.004 = 1 na 250),
   i tylko gdy swiat ma mniej niz limit: **legendarne 6, mistrzowskie 60** (Quality Legendary/Masterwork World Cap). Fine, slabe, zepsute - jak dzis; fuszerka
   nie staje sie arcydzielem; stany bez etykiety (konie, zuzycie, lup Spoils) bez zmian.
2. Spis raz na dobe (i przy wczytaniu): polki wszystkich osad, juki partii, rynsztunek bohaterow, gracz (sakwy, schowek, partie i bohaterowie rodu). Limit przy
   powstaniu = spis + nowe od spisu. **Bez stanu w zapisie** - zapis Jeffa wczytuje sie bez zmian, cofniecie DLL tez.
3. Nadmiar (stary swiat Jeffa): przy rozliczeniu doby, TYLKO na polkach osad: sztuki ponad limit (limit minus to, co trzymaja juki, bohaterowie i gracz) schodza o
   stopien - legendarna -> mistrzowska (gdy jest miejsce w limicie mistrzowskich), inaczej -> dobra; mistrzowska -> dobra (grupa bez Fine -> zwykla). Zostaja
   najdrozsze. Ta sama liczba sztuk na tej samej polce, zloto nie rusza (spada tylko wartosc towaru - linia podaje ile). Nigdy: sakwy, schowek i bohaterowie gracza,
   bron wykuta przez gracza (IsCraftedByPlayer).
- Wpiecia: 1 linia w WorkshopLaw.cs, 2 w HeroGear.cs, 2 w SubModuleMain.cs (ApplyAll po ArmourQuality, AddBehavior po HeroGearBehavior); latki Postfix
  Priority.Last przez AccessTools.DeclaredMethod: WorkshopsCampaignBehavior.GetRandomItemAux (po WorkshopLaw.RandomItemPostfix i CraftRealm),
  DefaultBattleRewardModel.GetLootedItemFromTroop (po BK LootScale), ROTEnlistmentBehavior.GetRandomItem(int, Settlement). Settings.cs + gen_mcm (RANGES).
- Linie: "Jakosc przedmiotow (m96jakosc): wpiete ..." (menu glowne), "... start sesji | ... spis przy wczytaniu", "... dzien N | regula, limit, spis przed
  wyrownaniem, swiat po (polki/juki/bohaterowie/gracz), dobre/slabe/zepsute, zwykle uzbrojenie na polkach, nowe od wczoraj wedlug zrodla, rzut gry odebrany,
  zatrzymane limitem, obnizki na polkach, wartosc, koszt, potkniecia". Grupa `python -I tools/sprawdz_logi.py --grupa m96jakosc <log>` (JQ-0..JQ-9).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: kto czyta modyfikatory sztuk z polek - AiBuyRules (dodatnie stany - teraz rzadsze, wiec AI kupi WIECEJ z polek, mniej "zamowien, bo na polce lezala
  tylko sztuka nie dla AI"; to poprawa, ale przesuwa przeplyw sprzetu i zlota miedzy polkami a warsztatami - patrzec na E8/Z i E6/0 w wielkim tescie),
  SellByCondition, Wyceny20, ShelfIndex (koszyk typ x tier - obnizka nie zmienia koszyka, tylko wersje polki), TradeCarts (wozy wioza stan dalej; sztuki w
  wozie nie sa w spisie, wyrownaja sie na polce celu).
- Kolizje: GetRandomItemAux ma juz postfiks WorkshopLaw (Priority normal) - nasz Last idzie po nim, jedna ocena na sztuke; GetLootedItemFromTroop ma postfiks BK
  (moze wyzerowac lup) - nasz po nim; ROTGankBehavior.GetRandomItem wola sam siebie - drugi przebieg tej samej sztuki nie odbiera jakosci danej przez regule.
- Zamknieta ekonomia: przy powstaniu nic sie nie zmienia w zlocie (miasto i tak placi za zwykla). Obnizka na polce zmniejsza wartosc towaru bez przeplywu zlota -
  przy sztukach kupionych przez miasto od gracza po cenie legendarnej to strata miasta (rzadkie; po wyrownaniu nadmiaru zdarza sie tylko, gdy gracz sam przepcha
  swiat ponad limit). Sztuki sprzedane przez gracza z kuzni lukow/zbroi (nie IsCraftedByPlayer) moga zejsc o stopien na polce, gdy swiat jest pelny.
- Koszt: spis raz na dobe (osady ok. 2400, partie ok. 3300, bohaterowie x 2 komplety) - linia podaje ms; prog JQ-8 250 ms. Goraca sciezka (Roll) bez alokacji.
- Stare sztuki leza dalej, dopoki ktos ich nie kupi (AI ich nie kupuje) - limit zajety, wiec nowe legendarne powstaja dopiero, gdy gracz albo bohater kupi
  ktoras. Lup gracza: legendarna/mistrzowska z lupu tez rzadko (ta sama regula) - Jeff to zauwazy.

**Status:** DO SPRAWDZENIA (build 0, kod wyjscia 0; gry ani autotestu nie uruchamialem).

**Test w grze / autotest:** zapis Jeffa (save040) wczytac -> w logu "Jakosc przedmiotow (m96jakosc): start sesji | ... spis przy wczytaniu: legendarne N" (stan
przed paczka), po pierwszej polnocy linia "dzien N" ze "spis przed wyrownaniem" -> "swiat po: legendarne <= 6, mistrzowskie <= 60" (ponad limit tylko poza polkami)
i obnizkami; kolejne doby: nowe legendarne 0-1 na kilka tygodni, mistrzowskie ok. 1-2 na dobe do limitu. `--grupa m96jakosc`: JQ-1..JQ-5, JQ-7, JQ-8 TAK.
W grze: sklepy kilku miast - legendarnych prawie brak, mistrzowskie pojedyncze; liczba sztuk na polce ta sama.

**Dla Jeffa:** Warsztaty w miastach robily co 23. sztuke legendarna i co 12. mistrzowska (tak losuje gra z RBM), a lordowie takich nie kupuja - wiec lezaly
w sklepach tysiacami. Teraz legendarna moze wyjsc tylko z najlepszych wyrobow (tier 5-6) i na calym swiecie jest ich najwyzej 6, mistrzowskich najwyzej 60. Nadmiar,
ktory juz lezy w sklepach, po pierwszej polnocy spada o stopien (legendarna na dobra albo mistrzowska) - nic nie znika, przedmioty zostaja na polkach. Twoje
rzeczy, Twoich towarzyszy i to, co wykujesz sam, nie sa ruszane.
