# PLAN: kucie pancerzy 1:1 z bronia (zatwierdzenie Jeffa: CZEKA)

Jeff (26.08): "dla pancerzy dokladnie taka sama mechanika jak bron - jedna czesc i OK,
ale UI/UX, wybor, odkrywanie, przetop i jakosc (dull itd.) identyczne".

## Ustalenia z dekompilacji (dowody, nie przypuszczenia)

- `Crafting.GenerateCraftedItem` + `CraftedItemGenerationHelper.FillWeapon` buduja przedmiot
  WYLACZNIE jako bron (WeaponComponentData). Ranged zna tylko Javelin/ThrowingAxe/ThrowingKnife;
  `CalculateMissileSpeed` ma assert "Weapon is not a missile". Pancerz: brak sciezki.
  => doslowne wpiecie pancerzy/lukow w zakladke projektowania = kruchy hack. NIE ROBIMY.
- Jakosc wykutej broni: `DefaultSmithingModel.GetCraftedWeaponModifier` ->
  `GetModifierQualityProbabilities(design, hero)`: ExplainedNumber = -trudnosc + bonus skilla
  (limit +-300), szanse: Poor 0.36*(1-S(-70)), Inferior 0.45*(1-S(-55)), Common S(25),
  Fine 0.36*S(40), Masterwork 0.27*S(70), Legendary 0.18*S(115), sigmoid k=0.018;
  perki Experienced/Master/Legendary Smith przesuwaja; potem
  `ItemModifierGroup.GetModifiersBasedOnQuality(quality)` + losowanie.
  GetModifiersBasedOnQuality dziala na KAZDEJ grupie, takze pancerza.
- Przetop: `DefaultSmithingModel.GetSmeltingOutputForItem` liczy TYLKO z `item.WeaponDesign`
  (pancerz = null = zero). Zakladka Smelt nie pokaze pancerza bez patcha wyjscia + filtra listy.

## Kroki (kazdy osobno testowalny, kolejnosc uzgodniona)

1. **Jakosc 1:1**: RollQuality (Forge.cs) zastapic wierna kopia formuly vanilla
   (sigmoidy + perki + GetModifiersBasedOnQuality na grupie przedmiotu).
   Trudnosc pancerza = nasz SkillNeeded z receptury. Ustawienia Shoddy* wygasaja.
2. **Odkrywanie 1:1**: RangedLore dostaje punkty TAKZE za przetop (dzis tylko kucie);
   vanilla popup odblokowania zamiast linijki na czacie.
3. **Przetop w zakladce Smelt**: patch GetSmeltingOutputForItem (nasze Recipes.SmeltYield
   dla pancerzy/lukow) + filtr listy przetopu; nasz tygiel z menu miejskiego moze wtedy zniknac.
4. **Luki/kusze w trybie pancerza BK**: ten sam flow (jedna "czesc" = caly wyrob),
   wspolne odkrycia/jakosc/przetop.
5. **Wybor klas jak przy broni**: popup klas (kirysy/helmy/nogawice/rekawice/plaszcze/
   tarcze/kropierze) przez ForgeView (wstrzykniecie Gauntlet).

Status (26.08, po zgodzie Jeffa "ok rob"):
- Krok 1 (jakosc 1:1) - ZROBIONE (commit "Kuznia 1:1 krok 1").
- Krok 2 (nauka za przetop + vanilla baner) - ZROBIONE (commit "krok 2").
- Krok 3 (zakladka Smelt przyjmuje pancerze i luki) - ZROBIONE (SmeltTab.cs);
  stary tygiel w menu miasta ZOSTAJE do czasu potwierdzenia w grze.
- Krok 4 (luki/kusze w trybie CRAFT) - BYL JUZ zrobiony wczesniej (ForgeView
  InjectRanged + FletchForge); kroki 1-3 ujednolicily im reszte.
- Krok 5 (popup wyboru klas jak przy broni) - NIEZROBIONY, do decyzji po
  testach 1-3 (ForgeView ma juz filtr kategorii, ktory czesciowo to zalatwia).

## Kolor wierszy listy zbroi (locked = czerwony) - ZROBIONE ZE ZRODEL BK, DO TESTU

Jeff chce, zeby na liscie zbroi w CRAFT zamkniete wzory byly wyszarzone/czerwone jak
zablokowane czesci broni, zamiast dopisku "- LOCKED" (Armoury/BkArmourList.NamePostfix).
Mialo czekac na Windows (prefab BK jest w plikach modulu, nie w DLL), ale prefab
znalazl sie w ZRODLACH BannerKings na githubie (R-Vaccari/bannerlord-banner-kings,
GUI/Prefabs/Crafting/ArmorCraftingCategory.xml) - wiersz nazwy to RichTextWidget
z Text="@ItemName" w ItemTemplate listy SmeltableItemList (DataSource {Armors}).

Zrobione (2026-08-26, z kontenera bez gry):
- ForgeView/ArmourListColour: PrefabExtension na prefab "ArmorCraftingCategory",
  XPath descendant::*[@Text='@ItemName'], Replace na dwa warianty wiersza:
  normalny IsVisible="@FvKnown" i czerwony #D65252FF IsVisible="@FvLocked".
- Armoury/BkArmourList.NamePostfix: dopisek "- LOCKED" schodzi TYLKO gdy
  ForgeView.ArmourListColour.Applied == true (flaga ustawiana po realnym wejsciu
  latki do prefabu; czytana przez reflection). Jesli prefab w BK.Redux rozni sie
  i XPath nie trafi, UIExtenderEx wypisze blad na ekranie, a dopisek ZOSTAJE.
Do potwierdzenia w grze: kolory na liscie, brak dopisku, brak bledu UIExtenderEx.
