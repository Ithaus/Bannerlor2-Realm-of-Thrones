## 2026-10-21 (m103-gospodarka: P0, P1, P6, P7a, P7b z docs/PLAN-GOSPODARKA-2026-10-21.md)

**Mod:** Armoury (+ `tools/sprawdz_logi.py`) | **Galaz:** `w-toku/m103-gospodarka` (od `ed9273fa` = WGRANIE 101) | **Status:** DO SPRAWDZENIA (nic nie wgrane; test w grze dopiero w jednym finalnym autotescie po scaleniu)

Kazdy krok to osobny commit. Wpisy ponizej w kolejnosci krokow.

---

### P0. Blad SideVillageVisit.Today przy starcie nowej kampanii (bez klucza MCM - poprawka bledu, liczby gry bez zmian)

**Problem (log):** CrashScribe `session-2026-10-21_07-17-34.log` linia 800, 07:18:26: `# ERROR IN A MOD` / `WHERE : [Armoury] Armoury.SideVillageVisit.Today` (NullReferenceException), stos ArmouryBehavior..ctor -> M97WioskiReset -> SideVillageVisit.Reset -> RaidReset -> ProtSync -> Today0 -> Today. Podsumowanie sesji (linia 7184): `1 x ERROR IN A MOD|[Armoury] Armoury.SideVillageVisit.Today`. Ten sam blok w kazdej nowej kampanii od m97-wioski (20-10_20-17, 20-10_21-49, 21-02-12, 21-04-09, 21-06-23 prolog, 21-07-17); w biegach z zapisu 0. Psuje progi etap4 T1 i etap8 E8 T0; gracz widzi czerwona notke CrashScribe na starcie kampanii z prologiem.

**Przyczyna:** `Armoury/src/SideVillageVisit.Raid.cs:77` (stary kod) - `ProtSync()` zawsze czytal zegar, takze zaraz po `_prot.Clear()` w `RaidReset` (:72). `Today0` (:82) sprawdzal tylko `Campaign.Current != null`. Dekompilacja `TaleWorlds.CampaignSystem.Campaign`: `SetLoadingParameters` (Current = this; GameStarted = true tylko dla SavedCampaign), `OnInitialize` wola `GameManager.OnGameStart` (konstruktor ArmouryBehavior) PRZED `MapTimeTracker = new MapTimeTracker(...)` (tylko nowa kampania); `GameStarted = true` dla nowej gry dopiero w pierwszym `RealTick`. `CampaignTime.Now` = `Campaign.Current.MapTimeTracker.Now` -> NRE. Finalizator CrashScribe na kazdej metodzie Armoury lapal wyjatek w `Today()` przed `catch` w `Today0`.

**Zmiana:** `SideVillageVisit.Raid.cs` `ProtSync`: zegar czytany tylko przy `_prot.Count > 0`. `Today0`: `var c = Campaign.Current; return c != null && c.GameStarted ? Today() : 0;` (wzor `Live` z `A17Fix.cs:77`). 0 = "dzien nieznany", jak dotad po wyjatku.

**Inne drogi do Today() przy starcie/zmianie kampanii (sprawdzone w kodzie):**
- `Reset()` (SideVillageVisit.cs:77) -> `DayZero` -> `RaidDayZero` -> `DefDayZero` -> `DefPrune` (Defenders.cs:142) -> `LackNow`/`Fallen` -> `Today0` - slowniki `_lack`/`_fallen` sa tu jeszcze z poprzedniej kampanii (czyszczone dopiero w RaidReset -> DefReset). Przy zmianie kampanii w jednym procesie to byla uspiona druga droga NRE - teraz `Today0` daje 0 bez wyjatku (nowy obiekt Campaign ma GameStarted = false).
- `RaidReset` -> `ProtSync` (pusta lista - zegar nieczytany) -> `DefReset` (czysci slowniki, potem DefPrune po pustych).
- `Import` (wczytanie, ArmouryBehavior.cs:1371) -> `RaidReset` (jak wyzej); sam Import zegara nie czyta (migawka bez zdejmowania wygaslych).
- `SideVillageFief.Reset` (SideVillageFief.cs:86) i `SideVillageHome.Reset` (SideVillageHome.cs:70) - zegara nie czytaja; `SideVillageFief.Today()` (:110) wolany tylko z taktow/menu/Import (Import: zegar z zapisu, GameStarted = true).
- Pozostali wolajacy `Today0` (`Spared`, `PaidShare`, `Fallen`, `LackNow`, `AddFallen`, `AddLack`) dzialaja w trakcie gry (GameStarted = true). W oknie tworzenia nowej kampanii listy ochron, poleglych i braku rak sa puste - wynik bez zmian.

**Ryzyko / co sprawdzic:** minimalne. Gdyby ktos kiedys wolal `Today0` w `OnNewGameCreated` i potrzebowal prawdziwej daty, dostanie 0 (semantyka opisana w ProtSync/Fallen/LackNow). Przy scalaniu z galeziami ruszajacymi `SideVillageVisit.Raid.cs` sprawdzic diff `ProtSync`/`Today0` (pulapka scal.sh: latka w cudzym if). Anomalia 98nowa (brak bloku przy tym samym kodzie) bez wyjasnienia - poprawka usuwa sam wyjatek, wiec od niej nie zalezy.

**Oczekiwane progi (nowa kampania):** CrashScribe `grep -c SideVillageVisit` = 0, `# ERROR IN A MOD` = 0; etap4 T1 = TAK (bloki 0, powtorki 0); etap8 E8 T0 = TAK (0 i 0); m97wioski WP-1/WP-2 i m97obroncy OB-1 dalej TAK. Bieg z zapisu: dalej 0 blokow.

**Build:** Armoury kod 0 (6 ostrzezen jak dotad).
