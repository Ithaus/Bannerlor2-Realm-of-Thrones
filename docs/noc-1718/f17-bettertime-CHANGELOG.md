## 2026-10-18 (paczka f17-bettertime - Jeff: "better time wgraj, wrzuc go i tam sa strzalki, ustaw x1, x3 i x6")

**Mod:** Armoury (`MapSpeed.cs`, `MapClock.cs`, `MapSpeedUi.cs`).
**Problem:** Jeff chce moda Better Time (Nexus 2849, v1.1.3, sciagniety przez Jeffa 18.10) z jego przyciskami >> i >>> (x3 i x6). Nasze f9 i stary zegar mapy nadpisywaly
`Campaign.SpeedUpMultiplier`: postfiks paska co klatke (przy MapSpeedButtons) i `MapClock.ApplySpeed` co godzine gry (FastForwardMultiplier = 8) - wybrany w Better Time x3 / x6
wracalby do x8 najpozniej po godzinie gry; klawisz 4 mial dwa znaczenia (Better Time: >>>, nasz: x1); dwie wstawki UIExtenderEx w ten sam srodkowy panel MapBar.
**Przyczyna:** dwa mody ustawialy ten sam mnoznik czasu.
**Zmiana:** `MapSpeed.BetterTimeActive` (lista modulow z linii polecen gry, `Utilities.GetModulesNames`, raz na proces). Gdy Better Time jest w grze: bez naszej wstawki MapBar
i mixinu (linia startu "Predkosc mapy (f9/f17) ... NIE rejestrowane - w grze jest modul Better Time"), postfiks paska nie rusza mnoznika ani klawiszy 4/5/6, `MapClock.ApplySpeed`
nic nie robi (zegar z godzina na pasku zostaje). Bez Better Time - stara droga co do znaku.
**Ryzyko / co sprawdzic:** Better Time 1.1.3 na 1.4.8 + ROT + NavalDLC (NavalMapScreen) - czy jego panel sie pokazuje (wstawka do MapBar CenterPanel); po wczytaniu gra startuje z x4 gry,
dopoki Jeff nie kliknie >> albo >>>; nasza linia profilu f9 ("Predkosc mapy (f9)") dalej mierzy klatki. Ustawienia Better Time w MCM (FastForwardMultiplier 3, ExtraFastForwardMultiplier 6)
sa RequireRestart = false (zmiana bez zamykania gry).
**Status:** DO SPRAWDZENIA (build Armoury kod 0).
