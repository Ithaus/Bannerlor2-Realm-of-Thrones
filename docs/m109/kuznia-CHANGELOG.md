# m109-kuznia - Kuznia: wolniejszy wzrost Smithing (-30%) i jeden czytelny rachunek surowcow na ekranie kucia

Galaz `w-toku/m109-kuznia` (baza 44c03a05 = WGRANIE 103.5). Mody: Armoury, ForgeView. Jeff 23.10 zatwierdzil zadanie; parametry ustalal Claude.

Jeff 23.10 (skrot): "smithing - za duzo teraz i za szybko rosnie, jak sie tworzy luki itd., trzeba to wszystko zmniejszyc o 30%, spowolnic; totalnie zle zrobiony
jest interfejs, czego ile potrzeba, aby wykonac np. luk; musimy ujednolicic caly system i dodac nowe ikonki ... dorobic te liczby na ekranie kucia: mamy 3 drewna,
czyli 300 kg, i 87 kg w pamieci - jako liczba nad ikonkami metali ... teraz przy lukach nie ma w ogole pokazanych wymagan i kuje i kuje i nie wiem, czy to blad,
czy po prostu mam surowce, ktore sa w pamieci".

## Stan pracy

| Krok | Co | Stan |
|---|---|---|
| A | SmithXp.cs - SmithingXpFactor 0.70 (prefiks HeroDeveloper.AddSkillXp), linie, test A, gen_mcm | ZROBIONE (e90acb1e, build Armoury 0) |
| B1 | ForgeBill.cs - jeden rachunek; dostepy (UnitKgOf, OpenKgOf, WeaponKg x2, NeedOf, Forge.TakeFor, FletchForge.SpendBill/MassSpend); przycisk CRAFT, podpowiedz przycisku broni, napisy Forge i SmithMenu; ForgeBillOnScreen, gen_mcm | ZROBIONE (4c82196b, build Armoury 0) |
| B2 | ForgeView ForgeBillRow.cs - rzad kafli (mixin CraftingVM, wstawka Crafting Index 7), BkVmFixes.Sync, TestStep | ZROBIONE (33f21761, build ForgeView 0) |
| B3 | ForgeBillTest.cs - test (b) rachunek = zuzycie, test (c) ekranu; SubModuleMain/ArmouryBehavior | ZROBIONE (6a187644, build Armoury 0) |
| P | grupa progow m109kuz w tools/sprawdz_logi.py + KUZ-S / KUZ-S2 (dobry i zly bieg) | ZROBIONE (4273ab70; sprawdz_narzedzie OK; KUZ-S i KUZ-S2 OK; log Jeffa 22.10 - INFO) |
| F | poprawki wlasnego przegladu: TestStep odswieza BK przy wyborze tego samego kafla; kwit BK porownywany tylko w rachunku zakladki CRAFT (nasza kuznia i menu kowala zdejmuja przepis) | ZROBIONE (6e00720e, 8f3fd11e; build Armoury i ForgeView 0) |
| R | recenzje (zgodnosc K1-K5, kod KUZ-1..KUZ-6) - weryfikacja i poprawki (tabela "Recenzja 23.10" nizej) | ZROBIONE (ec37b917, 95e9a17c, 3701e882, 096d2004, b5df594e; build Armoury i ForgeView 0, gen_mcm, sprawdz_narzedzie OK, KUZ-S i KUZ-S2 OK) |
| AT | autotest (plan nizej) - NIE uruchamialem gry (Jeff gra); nic nie wgrane | DO ZROBIENIA |

## Problem

**A. Kowalstwo rosnie za szybko.** Log Jeffa 22.10 22:28-23:59 (`D:/Backup-Bannerlord/logi-jeffa/2026-10-22/Armoury-2026-10-22_22-28-22-PELNY-2359.log`):
Smithing 63 -> 122 w jednej sesji, surowe XP kuzni 31 744, z tego ok. 58% luki (40 linii "Wykuto" - prawie same luki: nordic_shortbow 11, steppe_war_bow 10,
steppe_bow 7, composite_steppe_bow 7). XP za jeden klik luku (Forge.Smith): t1 60, t2 240, t3 540, t4 960 (pekniecie - 30% tej kwoty); zlecenie kowalskie ok. 343 XP.

**B. Przy lukach ekran nie pokazuje wymagan.** W tym samym logu 39 z 40 linii "Surowce w kuzni (kg) (m96-receptury): <luk>" ma "z sakw: nic (otwarte ladunki
starczyly)" - luk bral gramy lnu i skory z pamieci, ekran nie pokazywal nic, przycisk byl czynny.

## Przyczyna

**A.** Kazda droga XP kuzni (gra, BK, nasze Forge*, SmithMenu, FieldArmourer, Helper, SmeltTab, zlecenia) konczy sie w HeroDeveloper.AddSkillXp (Hero.AddSkillXp
tylko do niej przekazuje). Miejsca w naszym kodzie: Forge.cs (Smelt 253, Finish 474/499, SelfRepair 555, Smith 639/658/672), ArmouryBehavior 2531 (godzinowe XP
roboty), FieldArmourer 165, Helper 65, SmeltTab 202, SmithMenu 1179/1218/2409/2432/2438; w grze CraftingCampaignBehavior (rafinacja 838, przetop 857, wolne kucie
868, zlecenie 877); BK CraftingMixin.ExecuteMainActionBK. Nikt nie mial mnoznika na kowalstwo.

**B.** (sprawdzone w dekompilacji BK CraftingMixin i ROT Crafting.xml) Ekran pokazywal tylko SZTUKI DO OTWARCIA z sakw: pasek gry ResourceChangeAmount = -kwit BK
(TrueArmourCost: Recipes.For -> Parts = KgPart.Units / ladunki drewna), rzad BK skora/len tak samo. Przy otwartych resztkach to 0, a ChangeAmountTextWidget przy 0 sie
chowa (IsVisible = Amount != 0); BK HasMaterials zwraca prawde. Pamiec (resztki kg przy kowadle, w zapisie): drewno SmithWood.OpenKg (arm_smith_wood_kg), wegiel
SmithCoal.OpenKg (arm_smith_coal_kg), len i skora SmithStock (arm_smith_stock), metal - skrzynka resztek SmeltScraps (arm_smeltscraps) - nigdzie na ekranie.
Brak czardrzewa / smoczej kosci wychodzil dopiero po kliknieciu (RareMaterials.Ready w Forge.Smith).

## Zmiana

### A - SmithXp.cs (klucz SmithingXpFactor, float, domyslnie 0.70; 1.00 = jak 44c03a05)

- Prefiks Priority.Low na `HeroDeveloper.AddSkillXp(SkillObject, float, bool, bool)` (AccessTools.DeclaredMethod na klasie deklarujacej; wlasna instancja
  Harmony com.jeff.armoury.smithxp; raz na proces, z OnSessionLaunched obok SkillLedger.EnsureHooks). Do recenzji Priority.First - zmiana po K3 (nizej).
- `rawXp *= klucz` tylko gdy: skill == Crafting, isAffectedByFocusFactor == true, rawXp > 0, klucz poza 0.999-1.001 (klamra 0-4). Wywolania z false (ChangeSkillLevel -
  poziom ustawiany wprost) bez zmian, liczone jako "bez mnoznika".
- Tylko umiejetnosc (recenzja K3): oryginal daje GainRawXp juz uciete rawXp, wiec prefiks oddaje poziomowi postaci roznice od razu -
  `GainRawXp(Round(przed) - Round(po))` przez delegat (AccessTools.MethodDelegate) na prywatna metode gry; gra zaokragla kazde wywolanie, wiec suma =
  Round(przed) - poziom postaci rosnie jak bez ciecia. Bez delegata (inna wersja gry) - poziom tez wolniej, linia startu "poziom postaci bez ciecia NIE".
- Kolejnosc na celu: ROT patch30 (Normal - x GlobalXPBonus tylko na sluzbie u lorda) -> nasz (Low - tnie i oddaje poziomowi juz z premia ROT, wiec poziom
  dokladnie jak bez ciecia takze na sluzbie) -> ksiega E8/P1 (SkillLedger, Last - liczy juz po cieciu).
- Szacunek: ta sama sesja Jeffa przy 0.70 skonczylaby sie na ok. 109 zamiast 122 (z tabeli XP gry; rozpoznanie).
- Slowami gracza: kazdy przyrost kowalstwa (kucie broni, zbroi, lukow, kusz, strzal, przetop, rafinacja, naprawa, zlecenia i kazde inne XP kowalstwa;
  kazdy bohater) jest o 30% mniejszy. Poziom postaci zarobiony kowalstwem rosnie jak dotad (recenzja K3 - Jeff mowil o kowalstwie). Sufity (Xp Cap Per Tier,
  Weapon Xp Cap Per Tier, Refine Xp Cap, dobowe 100 XP kowala polowego, minimum 25% XpFair) licza sie przed cieciem - opis klucza to mowi.

### B - jeden rachunek (klucz ForgeBillOnScreen, bool, domyslnie TAK; NIE = rzad BK, przyciski i napisy jak dotad)

- **Armoury/src/ForgeBill.cs** - jedyna funkcja "co i ile potrzeba / ile jest": `Of(item[, recipe])` dla CRAFT, naszej kuzni i menu kowala (Recipes.For: drewno
  w kg z SmithWood, metal/len/skora w kg z przepisu z masy, reszta w calych sztukach, surowiec luku RareMaterials.NeedOf; pancerz/tarcza - kwit BK
  GetCraftingInputForArmor porownany z przepisem, inny = wiersze z kwitu BK w sztukach i licznik; po recenzji KUZ-1 kazdy przedmiot CRAFT poza lukiem, kusza
  i amunicja strzelecka - takze pociski procy, ktore BK kuje wlasnym kwitem, licznik "kwit wlasny BK"), `OfWeapon(design)` dla broni (koszt modelu kuzni - to samo
  wywolanie co SpendMaterials gry, z latkami; drewno i wegiel w kg z SmithWood/SmithCoal.WeaponKg). Wiersz: potrzeba, sztuki w sakwach (len/skora/aksamit - cala
  kategoria), kg sztuki, pamiec, sztuki do otwarcia, brak (te same warunki co Recipes.HasMaterials, RareMaterials.Ready i BK HasMaterials).
- **Co widac = co zabrane**: te same funkcje licza ekran i zuzycie - Recipes.For (Forge.Smith/Begin przez nowe `Forge.TakeFor`, kwit BK przez wyjete
  `FletchForge.SpendBill` i `MassSpend`), koszt modelu + `SmithWood.WeaponKg` / `SmithCoal.WeaponKg` (uzywane teraz przez ich SpendPrefix). Dostepy bez zmiany
  zachowania: `Recipes.UnitKgOf`, `Recipes.OpenKgOf`, `RareMaterials.NeedOf`, `RareMaterials.WhereOf`.
- **ForgeView/src/ForgeBillRow.cs** - rzad kafli nad paskiem surowcow (ta sama kotwica co pasek gry i rzad BK: Center/Bottom, MarginRight 675; MarginBottom 118 - nad
  "-N" gry), ten sam na ekranie broni gry i w zakladce CRAFT BK. Kafel 120x124: potrzeba ("-0.36 kg", czerwona przy braku), sklad ("3x100 +87" = sztuki x kg
  sztuki + pamiec), ikona surowca (obraz przedmiotu ItemImageIdentifierVM - jak MaterialRowExtension, bez nowych sprite'ow; kazdy surowiec ma ikone), plakietka
  "387 kg", podpowiedz po najechaniu (np. "Hardwood. This piece takes 0.36 kg. You have 3 in your bags (100 kg each) = 300 kg, and 87 kg left of the open one =
  387 kg. The open one covers it - nothing new is taken from your bags."). Mixin UIExtenderEx na CraftingVM (FvBillRows, FvBillShown), most refleksja do
  Armoury.ForgeBill.ScreenRows (bez Armoury / klucz NIE - null, stare zachowanie). Rzad BK (skora, len) przy kluczu TAK pusty w obu trybach (BkMaterialsModeFix.Sync).
- **Przyciski**: CRAFT BK (FletchForge.BkHasMaterialsPostfix) - przy braku w rachunku przycisk szary z napisem, czego brakuje (nowosc: takze czardrzewo i smocza
  kosc - dotad odmowa dopiero po kliknieciu); bron - postfiks CraftingVM.RefreshEnableMainAction (Harmony com.jeff.armoury.forgebill): gdy gra zablokowala
  przycisk brakiem materialow, podpowiedz mowi, czego brakuje (stan przycisku bez zmian). Napisy Forge.Smith/Begin przy braku i opis w menu kowala (SmithMenu) z
  tego samego rachunku.
- **Linie logu** "Kowalstwo wolniej (m109): " (start sesji, 5 pierwszych wywolan, doba, test) i "Kuznia wymagania (m109): " (start sesji z probkami luk/kusza/
  zbroja/bron/strzaly, ekran - pierwsze 10 i co 50., doba, test zuzycia, test ekranu, linia ForgeView "rzad wymagan - wpiety ... Armoury PODPIETY", po
  recenzji "ForgeView blad <gdzie> (blad N): ..." - pierwsze 3 i co 100.; "test - zuzycie probne: poczatek").
- **Autotest** (tylko CrashScribe\armoury-kuznia.flag + DLL autotestu, LogGate 1/2): (a) SmithXp.SelfTest - 100 XP Smithing (po recenzji K2/K3: ciecie w latce = klucz, przyrost - gdy mierzalny, poziom postaci +100), kontrola Athletics
  (1.00), droga ustawiania poziomu (1.00); na sluzbie u lorda ROT pominiety. (b) ForgeBillTest.RunBills - luk, kusza, zbroja, bron, strzaly: rachunek i zuzycie
  jednego kucia ta sama droga co klikniecie, zgodnosc sztuk (== otwarcia) i kg (+- max(0.001 kg, 0.5%)); brakujace sztuki dokladane na probe; stan po kazdym
  przedmiocie odtworzony (sakwy, pamiec drewna i wegla, SmithStock, SmeltScraps). (c) test ekranu - w menu miasta bez roboty w tej kuzni: OpenCrafting, kroki
  ForgeView TestStep (bron, zbroja, luk, strzaly - kafle VM wobec rachunku liczonego od nowa ta sama funkcja), zdjecie silnika
  `CrashScribe\zdjecia\kuznia-m109-<rodzaj>-<czas>.png`, ExecuteCancel.
- **Progi** `python -I tools/sprawdz_logi.py --grupa m109kuz <Armoury-*.log>` (KUZ-S, KUZ-S2, KUZ-0..KUZ-9 - opis w --help).
- Zapis: bez nowych kluczy i typow; stare zapisy Jeffa wczytuja sie bez zmian. Nowe klucze MCM nie sa w Armoury.json Jeffa - dzialaja domyslne z kodu.

## Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)

- A: ten sam cel lataja ROT patch30 (przemienne) i SkillLedger.XpPrefix (licznik po cieciu - linie ksiegi E8/P1 pokaza XP kuzni o 30% mniejsze; to zamierzone).
  Ciecie dotyczy kazdego XP Crafting z czynnikiem nauki, takze drobnego spoza kuzni (edukacja i zdarzenia BK) i wszystkich bohaterow - jedna regula.
  Wolniej rosna progi czytajace Smithing: RangedLore (prog 35 na tier), PatternGate, Forge.FailureChance / RollQuality, Helper (prog 20), stamina kowala - zamierzone.
  Po K3 poziom postaci z kowalstwa bez zmian (LevelPoints - awanse jak dotad). Prefiks wola prywatne GainRawXp (CheckLevel, awans) przed oryginalem - to samo,
  co oryginal robi na swoim poczatku; przy kluczu > 1 roznica ujemna (TotalXp chwilowo mniejsze, poziom sie nie cofa, suma = Round(przed)). Goracy punkt:
  2 porownania na wywolanie (ok. 20 tys./dobe), bez alokacji; przy cieciu jedno wywolanie delegata.
- B: stan pamieci ForgeBill tylko czyta; pisze go wylacznie test (b) i odtwarza. MendMaterial._frac (pamiec samonaprawy) nie bierze udzialu w kuciu - nie pokazywana.
  Latki: BK CraftingMixin.OnRefresh i setter IsInArmorMode (ForgeView, rozszerzone), CraftingMixin.HasMaterials (FletchForge, rozszerzone pod kluczem),
  CraftingVM.RefreshEnableMainAction (nowa; grep DLL modow - nikt inny jej nie lata), GetCraftingInputForArmor (TrueArmourCost, BkArmorBillPostfix) i
  ExecuteMainActionBK (FletchForge, ArmourQuality, ForgeClock) - bez zmian. BK czyta CurrentExtraMaterials (First("leather")) tylko wewnatrz OnRefresh po wlasnym
  Clear + Add - czyscimy dopiero po nim (postfiks); w trybie broni tak bylo juz od dawna.
- UIExtenderEx: drugi mixin na CraftingVM (BK ma swoj); kolejnosc tworzenia mixinow BK / naszego nie jest pewna - nasz konstruktor odswieza rzad z ostatniego
  odswiezenia BK tego samego VM, inaczej robi to pierwsze odswiezenie BK. Nazwy z prefiksem Fv.
- HIPOTEZA (do sprawdzenia na zdjeciu albo u Jeffa): uklad - w trybie pancerza dolne ok. 27 px podgladu 3D moze zachodzic na rzad (stala MarginBottom w jednym
  miejscu: ForgeBillRowExtension); hover kafla (podpowiedz) - struktura skopiowana z paska gry (DoNotPassEventsToChildren + HintWidget).
- W trybie broni rzad powtarza metale z paska gry - zamierzone (jeden format rachunku).
- Istniejaca rozbieznosc (nienaprawiana, poza zakresem): SmithCoal.CostPostfix liczy bryly z liczby modelu, a SpendPrefix z RawForge - test (b) broni ja ujawni.
- Klamiaca linia "Noc-ekonomia (NE): kuznia (N12) ... nie wpiete" zostaje (poza zakresem).
- Wydajnosc: ForgeBill tylko przy odswiezeniu ekranu (zdarzenia), nie co klatke; w trybie pancerza ok. 2 rachunki na odswiezenie (HasMaterials i rzad).
- Test (c) otwiera ekran kucia z taktu aplikacji (PushState) - HIPOTEZA, ze przejdzie; tylko pod plikiem flagi, najwyzej 3 proby.
- Scalanie: wspolne punkty z rownoleglymi galeziami - Settings.cs (2 klucze w 2 blokach: Experience po WeaponXpCapPerTier, blok noc-ekonomia po SmithWoodOnScreen),
  ArmouryBehavior.cs (1 sluchacz OnAfterSessionLaunched, 1 linia OnSessionLaunched, 3 linie doby), SubModuleMain.cs (1 blok taktu + metoda), FletchForge.cs,
  Forge.cs, tools/sprawdz_logi.py (nowa grupa przed __main__, dispatch po m107gosp, opis w --help przed na7), ForgeView (BkVmFixes, csproj - referencja
  TaleWorlds.CampaignSystem.ViewModelCollection). m109-przesluch daje XP przez AddSkillXp, ale innych umiejetnosci (Charm, Roguery) - bez wplywu.

## Odstepstwa od projektu (z powodem)

1. ForgeView: rzad odswieza takze konstruktor naszego mixinu (z ostatniego odswiezenia BK tego samego VM) - kolejnosc tworzenia mixinow BK i naszego w UIExtenderEx
   zalezy od kolejnosci rejestracji modow; bez tego pierwszy widok ekranu moglby byc pusty do pierwszej akcji. Setter IsInArmorMode odswieza rzad tylko przy
   zmianie trybu (BK ustawia go na poczatku kazdego OnRefresh - inaczej podwojne liczenie).
2. Test ekranu: kafle VM porownuje ForgeView (TestStep) z rachunkiem policzonym od nowa ta sama funkcja (ScreenRows) - Armoury nie ma dostepu do projektu broni z
   VM bez ForgeView. Linia: "test ekranu - <rodzaj> | <tryb> <id> | ekran: ... | rachunek: ... | zgodne TAK|NIE | zdjecie ...".
3. Test (b): stan odtwarzany po KAZDYM przedmiocie (przedmioty niezalezne, blizej stanu Jeffa), nie raz na koniec.
4. CRAFT BK: przy kluczu TAK napis braku z rachunku takze wtedy, gdy BK juz odmowil (jego napis jest ogolny); licznik "przycisk zablokowany rachunkiem" liczy
   zablokowanie rachunkiem przy zgodzie BK raz na przedmiot (recenzja KUZ-6), podpowiedz przycisku broni - osobny licznik raz na nowy napis.
5. ScreenRows przy wyjatku zwraca null (stare zachowanie - rzad BK), a bez przedmiotu pusta liste.
6. KUZ-0: linii startu A i B ma byc tyle samo (kazda sesja ma obie).

## Recenzja 23.10 - weryfikacja uwag (dwie recenzje: zgodnosc K1-K5, bezpieczenstwo kodu KUZ-1..KUZ-6)

| Uwaga | Werdykt | Dowod / co zrobione |
|---|---|---|
| K1 test ekranu bez mixinu ForgeView zostawia ekran otwarty (bieg stoi) | PRAWDA | CraftingVM.ExecuteCancel -> PopState tylko przez ForgeView TestStep(9); Blocker przy ekranie kucia daje "nie menu osady". Poprawka: VM z ForgeJob.VmCtorPost (ForgeBillTest.NoteVm, tylko ekran otwarty przez test), CloseScreen (ExecuteCancel x4, awaryjnie PopState) przy kazdym przerwaniu, limicie 60 s na ekranie i po koncu; prog KUZ-8d |
| K2 test A daje NIE ponad limitem nauki (czynnik 0) i przy duzym XP (float) | PRAWDA | CalculateLearningRate: LimitMin(0) i -1 - 0.1 x nadwyzka. Poprawka: ciecie mierzone w samej latce (przed -> po), przyrosty Smithing/Athletics/ustawiania tylko gdy mierzalne (czynnik > 0 i przyrost >> blad float 2 ulp), 100 XP zamiast 5 |
| K3 ciecie zmniejsza tez poziom postaci z kowalstwa | PRAWDA (decyzja: tniemy tylko umiejetnosc) | G_HeroDeveloper.AddSkillXp: GainRawXp(rawXp) z tym samym rawXp. Zadanie i Jeff mowia o kowalstwie. Poprawka: prefiks oddaje GainRawXp(Round(przed) - Round(po)), Priority.Low (po ROT patch30 - dokladnie takze na sluzbie); KUZ-1b |
| K4 opis MCM: "caps above", "any smith at the anvil" | PRAWDA | RefineXpCap w grupie Forge fee; ciecie obejmuje kazdego bohatera i kazde XP Crafting z czynnikiem nauki. Opis poprawiony (+ poziom postaci bez ciecia), gen_mcm |
| K5 aksamit tarczy blokuje przy BkShieldBillByRecipe NIE (nikt go nie zdejmuje) | PRAWDA (tylko przy kluczach innych niz u Jeffa) | ShieldVelvetNeed/BkShieldSpendPostfix wychodza przy !ShieldRecipeOn. Poprawka: wiersz aksamitu poza tablica BK tylko przy FletchForge.ShieldRecipeOn (teraz internal) |
| KUZ-1 pociski procy w CRAFT: rachunek z przepisu, BK zdejmuje swoj kwit | PRAWDA | ArmorCraftingVM.RefreshValues przepuszcza amunicje nie-dystansowa/nie-biala (sling_stoneammo/chiseledammo/leadammo w SandBoxCore weapons.xml, bez is_merchandise=false); BKSmithingModel: [7]=2, [Iron3..6]=2; nasze latki kwitu tylko pancerz/tarcza; BkCraftPrefix przepuszcza nie-RangedType do BK. Poprawka: CheckBkTable dla kazdego CRAFT poza RangedType; licznik "kwit wlasny BK" osobno (KUZ-9 liczy tylko "inny niz przepis") |
| KUZ-2 pole BK CraftingMixin.crafting - po aktualizacji BK pusty rzad | PRAWDA (tylko po zmianie BK) | Dzis pole jest (dekompilacja), ale Sync przy null zwraca true i czysci rzad BK. Poprawka: SameVm - pole nieznane = mixin biezacego ekranu (ekran kucia jest jeden); blad Look idzie do logu Armoury (KUZ-3) |
| KUZ-3 bledy ForgeView tylko w ForgeView.log (nadpisywany, nie zbierany) | PRAWDA | ForgeView Log.Init: File.WriteAllText; logi-noc/<bieg> bez ForgeView.log. Poprawka: ForgeBillMixin.Error i ForgeBillLink.Rows -> ScreenNote "Kuznia wymagania (m109): ForgeView blad ..." (pierwsze 3 i co 100.); KUZ-9 je liczy |
| KUZ-4 rzad zachodzi na podglad 3D pancerza (ok. 34 px), kafle przechwytuja mysz | NIEPEWNE | Geometria z prefabu bez zdjecia; kafle nie maja DoNotAcceptEvents (potrzebne do podpowiedzi). Bez zmiany - do planu autotestu (zdjecie trybu pancerza, obrot modelu nad rzedem) |
| KUZ-5 test (b) zostawia slady prawdziwego zuzycia (liczniki doby, linie, komunikaty) | PRAWDA (slad, nie blad) | Test idzie ta sama droga co klikniecie - tak ma byc. Poprawka: linia "test - zuzycie probne: poczatek" (do "test - wynik" to kucia probne); w planie: regresje kuzni (m96receptury R96-3, m97surowce, nekon) czytac z tym albo na biegu bez flagi |
| KUZ-6 _dBlocked liczy wywolania, miesza przycisk CRAFT z podpowiedzia broni | PRAWDA | Poprawka: przycisk CRAFT raz na zablokowanie przedmiotu, podpowiedz broni osobny licznik raz na nowy napis; linia doby "podpowiedz przycisku broni N", "kwit wlasny BK (...) N" |

## Status

DO SPRAWDZENIA - nic nie wgrane, gra nie uruchamiana (Jeff gra). Buildy: Armoury 0 bledow, ForgeView 0 bledow; gen_mcm Armoury 2320 ustawien.
Po recenzji (b5df594e): build Armoury 0 i ForgeView 0 bledow, gen_mcm, sprawdz_narzedzie OK, --grupa m109kuz: KUZ-S i KUZ-S2 TAK.

## Plan autotestu

- Build Armoury i ForgeView (kod wyjscia 0), gen_mcm.
- Przed pierwszym biegiem kopia logow Jeffa z dnia (zasada z pamieci).
- `testy-1033.ps1` z `-ExtraFlags armoury-kuznia.flag`: p20 (prolog 20 dob; gracz czeka w menu miasta - szansa na test ekranu; razem z flaga dziennika),
  wczyt (autotest-dlugi + 20 dob), bitwa (4 bitwy z autotest-F-d240 - goraca sciezka XP walki przez nasz prefiks + test A).
- Na kazdym logu: `--grupa m109kuz`; regresje `m96receptury` (R96-4 kwit zgodny, R96-5), `m97surowce`, `nekon` (NE-5 kuznia), `etap8` (ksiega E8/P1); `m105dziennik`
  dla p20; zwykly pelny zestaw. Zdjecia `CrashScribe\zdjecia\kuznia-m109-*.png` - ocena reczna (polozenie rzedu, nachodzenie na podglad 3D, czytelnosc liczb).
- Po recenzji (23.10) w tym samym biegu sprawdzic:
  - KUZ-1 i KUZ-1b (K2, K3): ciecie w latce 0.700, przyrost umiejetnosci proporcja 0.70 (albo "pominieta" z powodem), poziom postaci +100 wobec 100;
    linia startu A "poziom postaci bez ciecia TAK (GainRawXp)" (KUZ-0).
  - KUZ-8d (K1): kazde wyjscie z ekranu "ekran zamkniety"; "zamkniety (Armoury ..." = ekran stal bez obslugi ForgeView (brak mixinu) - NIE; bieg nie moze stanac
    na ekranie kucia (limit 60 s na ekranie).
  - KUZ-9 (KUZ-3): linii "ForgeView blad" 0. KUZ-I: "kwit wlasny BK" - INFO (pociski procy w CRAFT).
  - KUZ-1 recenzji kodu: CRAFT -> sling_stoneammo (jesli jest na liscie) - rzad = kwit BK (2 ladunki drewna, 2 sztaby Iron3), linia ekranu "(kwit wlasny BK)",
    po kuciu sakwy zmniejszone o to samo (recznie albo log "Surowce/Drewno" - BK zdejmuje bez naszych linii).
  - KUZ-5: linie "Drewno w kuzni", "Wegiel w kuzni", "Surowce w kuzni (kg)" miedzy "test - zuzycie probne: poczatek" a "test - wynik" to kucia probne; regresje
    kuzni (m96receptury R96-3 / R96-7, m97surowce S97-5, nekon NE-5) na biegu z flaga czytac z tym, albo puscic je na biegu bez armoury-kuznia.flag.
- NIEPEWNE (do sprawdzenia na zdjeciu / w grze):
  - KUZ-4: tryb pancerza - czy rzad (MarginBottom 118, kafel 124 px) zachodzi na dol podgladu 3D (rachunek z prefabu: ok. 34 px) i czy nad paskiem kafli da sie
    obracac / przyblizac model (kafle przyjmuja zdarzenia - podpowiedz). Gdy zachodzi: TileH 121 (tyle wynosi tresc) i mniejszy MarginBottom albo rzad w bok
    (stale w jednym miejscu: ForgeBillRowExtension).
  - podpowiedz po najechaniu kafla przy DoNotAcceptEvents na rzedzie i liscie (struktura kafla jak pasek gry: DoNotPassEventsToChildren + HintWidget).
  - K5: przy kluczach Jeffa nie wystepuje (BkShieldBillByRecipe i ShieldNoVelvet TAK) - sprawdzone w kodzie; bieg z BkShieldBillByRecipe NIE i ShieldNoVelvet NIE
    tylko, jesli skrypt biegow ustawia klucze MCM (tarcza t5+ w CRAFT bez aksamitu - przycisk czynny).
- U Jeffa po wgraniu: CRAFT -> luk - rzad pokazuje drewno, rog/sciegno i cieciwe w kg z pamiecia; brak = czerwona liczba i przycisk szary z napisem; tempo Smithing
  (poziom postaci z kowalstwa jak dotad).
