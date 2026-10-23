## m109-czas - kucie trwa o 25% dluzej (23.10)

**Problem:** Jeff 23.10: "za szybko powstaja rzeczy, wydluz robienie wszystkiego o 25%".

**Przyczyna:** czas pracy gracza przy kowadle = miara BK (zuzyta stamina / 6 h) dla sztuk z ekranu kuzni i dla pracy bez sztuki (wytop, rafinacja,
zlecenia, spartaczone); w menu "Work the forge" - tier x DaysPerTier x tempo; przy C5 (ForgeLikeTownSmith, u Jeffa NIE) - dni kowala z warsztatu AI.

**Zmiana:** klucz ForgeTimeMult = 1.25 (MCM "Forge Time Mult", grupa Time at the anvil; 1 = jak dotad). Mnoznik w jednym miejscu dla sztuk -
ForgeClock.WorkHours (bron "van", CRAFT BK zbroi, lukow, kusz, amunicji w Forge.Smith, termin zamowien przy C5) - oraz dla pracy bez sztuki
(ForgeJob.PrepHours/Close: napis "Hours spent for all actions" i wpis kolejki te same), w menu z tempem (Forge.Begin i napis okna SmithMenu).
XP w trakcie pracy (AdvanceProjects) rozlozone na dluzsza robote - suma XP sztuki bez zmian (ciecie XP o 30% robi galaz m109-kuznia).
Linia "Robota w kuzni (m96): ..." ma "czas kuzni x1.25 (m109)".

**Ryzyko / co sprawdzic:** (1) doba kuzni (ForgeHireHistorical) placona za kazda dobe roboty - dluzsza robota = proporcjonalnie wiecej dob najmu
(zgodnie z zasada "najem za dzien kuzni"); (2) sztuki juz w kolejce (z zapisu) bez zmian; (3) Forge Fee przy kluczu NIE ForgeHireHistorical bez zmian.

**Status:** DO SPRAWDZENIA (test kuzni w paczce n34).
