# m96-kuznia - wywrotka w projektowaniu broni (20.10 14:27)

**Problem (Jeff 20.10: "zobacz czemu mialem crash"):** CrashScribe crash-original-2026-10-20_14-27-46.log - Eyrie, menu arm_tm_smithy_menu (kuznia), klik czesci broni
(WeaponDesignVM.OnSetItemPieceManually -> OnSetItemPiece -> RefreshAlternativeUsageList_Patch1 -> UpdateSecondaryUsageIndex -> RefreshStats_Patch1 ->
DoesCurrentItemHaveSecondaryUsage_Patch1): NullReferenceException.
**Przyczyna:** gra: DoesCurrentItemHaveSecondaryUsage robi _crafting.GetCurrentCraftedItemObject().Weapons.Count, a ItemObject.Weapons => WeaponComponent?.Weapons
(null, gdy wykuwany przedmiot nie ma czesci broni - zly zestaw czesci). Nasz straznik CrashScribe Mends.VmNeedsItem (z 24.08) sprawdzal tylko, czy przedmiot istnieje.
**Zmiana:** VmNeedsItem wymaga przedmiotu z WeaponComponent i niepustymi Weapons; do 4 metod (DoesCurrentItemHaveSecondaryUsage, RefreshAlternativeUsageList,
RefreshStats, AddClassFlagsToPiece) finalizer VmSafeBool/VmSafeVoid - NullReference/ArgumentOutOfRange nie wywraca gry (bool -> false), linia "Mends: kuznia - odswiezenie
projektu broni bez czesci broni pominiete" (do 5 razy). Czysta naprawa bledu - bez klucza MCM.
**Ryzyko:** przy zlym zestawie czesci statystyki projektu chwilowo sie nie odswieza (do nastepnej poprawnej czesci).
**Test w grze:** kuznia -> Craft -> zmieniaj czesci broni (tez dziwne zestawy, np. uchwyt innego typu) - bez wywrotki.
**Dla Jeffa:** Wywrotke w kuzni dawal nasz CrashScribe: jego oslona projektowania broni nie lapala przedmiotu bez czesci broni. Teraz lapie i nic sie nie wywraca.
