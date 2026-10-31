# PRZEKAZANIE 24.10.2026 (aktualne 22:30 CEST) - kontynuacja na drugim koncie (limit tygodnia tego konta 100%)

## DOPISEK 31.10 ok. 04:35 CEST - NOWA SESJA (d16b0e41): Jeff wyjezdza do niedzieli, pelna zgoda "pracuj tak dlugo az naprawisz", wgrywac samemu
- Jeff gra NOWA kampanie: Ironman0gXONzURi8yO.sav (rod KARDA, bohater Przemyslaw, doby 108648-108878, ok. 220k zl). Logi 30.10 18:01/19:48/21:04-03:30 skopiowane do
  D:\Backup-Bannerlord\logi-jeffa\2026-10-31-sesje-30-10 i scratchpad d16b0e41\logi. Stary zapis testowy (KkUpHftX7ZIq) przeniesiony na D:\...\zapisy-testowe-archiwum\2026-10-31-stary-przekierowany.
- Zadania Jeffa: (1) karawany - dzialanie, zysk, OSOBNA linia kosztu karawan w wydatkach rodu; (2) warsztaty prawie nic nie zarabiaja - blad czy ekonomia (bez sztucznego zarobku);
  (3) bitwy - logi, poprawki, mocniejsze/rozsadniejsze AI; (4) kowalstwo - balans XP luki vs miecze; (5) inwestycje - czy dzialaja; (6) reszta bledow z logu i zapisu.
- LINIA: noc/305-jeff-31-10 (worktree scratchpad d16b0e41\zb\n305 od 6888ffa3 = WGRANE 140). Diagnoza wf_4fb41c95-b70 w toku; autotest bazowy 141base (140 na nowym zapisie,
  -JeffSave autotest-przekierowany-Ironman0gXONzURi8yO, 7 bitew + 90 dob) w toku - wynik m113\seria-141base.out.txt.
- 05:00 DIAGNOZA GOTOWA (scratchpad d16b0e41\diagnoza.json, 6 obszarow x diagnoza + recenzja). Najwazniejsze: karawana Bjorna +168k netto w 175 dobach (2/3 z jednej
  podejrzanej transakcji 10k -> 119k), zniknela 108877 z 15k kapitalu; zold karawan w linii 'Garrison and Party Expense' (BK PartyExpensesPrefix); WARSZTATY: kuznia gracza nie
  zarabia na broni (WorkshopLaw.cs:241 gracz = vanilla, gra robi bron bez zlota), cena kupna z pamieci notabla (Fairmarket 55k -> 2 zl/d), zmiana produkcji przesuwa znacznik
  kapitalu (zwrot kapitalu jako zysk z podatkiem); BITWA: kompanie nie lamia sie (bramka morale 50), poscig 148 nigdy nie przydziela, FF 27.9% (szarze przez swoich), spam
  wyjatku ROT smoka; KOWALSTWO: blad zaokraglenia XP (miecz i przetop 1 XP), luk XP z 'dni plyty'; INWESTYCJE: prace okregu dzialaja (ok. 15%/rok), panel dzieli przez 28 dob.
- 05:05 Workflow wf_ceed2ee0-bec: paczki 305a karawany / 305b warsztaty / 305c bitwa / 305d kowalstwo / 305e drobne (worktree zb\n305a..e, galezie noc/305a-karawany itd.
  od noc/305-jeff-31-10), kazda: kod -> recenzja -> poprawki. Potem scalenie do noc/305-jeff-31-10, test 141a, porownanie z 141base, WGRANIE 141.
- 05:35 141base (140 na nowym zapisie): 7 bitew + 90 dob kod 0, ERROR 0 (logi m113\logi\141base-*). Paczki 305a-e gotowe i scalone (union.py w scratchpad d16b0e41 dla
  CHANGELOG/sprawdz_logi/McmSettings + gen_mcm 2645 ustawien) -> noc/305-jeff-31-10 e739ad5c wypchniete; Armoury 7bca3bbe (build 0 bledow; UWAGA worktree bez libs:
  -p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs). CrashScribe testowy z latka smokow: scratchpad d16b0e41\cs-at305\CrashScribe-at-n305.dll (kopia cs-at283 + DragonShips).
  Produkcyjny CrashScribe z n305: e158884f (NIE wgrany). AUTOTEST 141a w toku (m113\seria-141a.out.txt) + przeglad scalenia wf_a2e498c8-afe.

## DOPISEK 30.10 ok. 04:45 CEST - WGRANIE 140 (noc/304-karawany-odcinki c9fca017, status 6888ffa3 wypchniety) - KONIEC NOCY, START DLA NASTEPNEJ SESJI / DRUGIEGO KONTA
- W GRZE: Armoury.dll 2845835689cd2ca7e26199c38d8d279c (140). Kopie 139: Armoury.dll.bak-2026-10-30-przed-wgranie140 i D:\Backup-Bannerlord\wgrane\2026-10-30-wgranie140-przed.
  CrashScribe bef9db58 i pliki danych bez zmian od 136. Skrypt scratchpad b2a84731\wgraj140.ps1. Zapis testowy D:\...\zapisy-testowe-archiwum\2026-10-30-140 (Game Saves czyste).
- LINIA GLOWNA: noc/304-karawany-odcinki (worktree scratchpad sesji b2a84731\zb\n304). Kolejne paczki: nowa galaz od 6888ffa3.
- Wersje tej sesji: 137 (290-299), 138 (299b/c ceny plaskie, 291d obronca, 300b lupy gracza, 138c wywoz), 139 (139 czekanie/limit BK/trasa, 139b/c dowodca karawany), 140 (odcinek
  z karawan w ruchu, utkniete szukaja miasta placacego). 140a wobec 136a: placace zold ok. 0.37 -> 0.584, mediana -52 -> +46 zl/d, wynik floty +6k -> +102k zl/d, psucie 216 -> ok. 0.
- OTWARTE: placace >= 0.60 (0.584; doby 61-90 0.547), zelazo zima -5..-19/d (caly rok ok. 0), strata 1.3-1.7k zl/d, N283-2/3 scenariusze testowe (reakcja w bitwach dziala),
  NWAR-15; DO SPRAWDZENIA PRZEZ JEFFA W GRZE: kryjowka 293 (dialog szefa), lupy 300b (ok. 33% wartosci), dowodca karawany 139b/c (ceny, XP Handlu), bieg piechoty bez tarcz 291.
- Procedura testu: m113\testy-noc-283.ps1 -Tree <wt> -Dll mfinalNNN -NowyArmoury <dll> -Prefix NNN -Runs kopia,bitwaN,probyN -ProbyDays 90 -Harasser 3 -CsAt scratchpad 1e1c276a
  \cs-at283\CrashScribe-at-n283.dll -ExtraFlags armoury-dzwiek-bitwy.flag,armoury-pokoj264.flag,armoury-zbroja268.flag,armoury-rownowaga274.flag,armoury-karawany.flag,armoury-rody277.flag;
  zapis testowy: skopiowac Ironman Jeffa jako autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (gdy gral: usunac m113\zapisy\baza-*.sav), po serii przeniesc na D:.
  Grupy: python -I <wt>\tools\sprawdz_logi.py --grupa <g> <log> (kopia checkera z commitu testu, bo worktree moze sie zmieniac).

## DOPISEK 30.10 ok. 04:00 CEST - 140 GOTOWE (noc/304-karawany-odcinki aa37ccd6 + c9fca017, Armoury 28458356), AUTOTEST 140a w toku; tydzien 90%
- Analiza 139a: odcinek 288 = wszystkie karawany / wyjazdy - liczyl karawany stojace w zamkach/wsiach (17 -> 38, +19), to +0.22 z +0.42 dnia; zawyzony odcinek podnosil spizarnie 7 -> 8 dob
  i RoadDaysTo. 116 'czeka' trzyma 585k zl towaru, mediana 16 dob bez sprzedazy, dla 85% jest miasto placace 2.75 x koszt ok. 1.5 dnia drogi; regula C widziala tylko 11 utknietych decyzji/d.
- 140: klucz CaravanLegMovingOnly (odcinek tylko z karawan w ruchu; linia postoju z rozbiciem zamki/wsie), CaravanStuckSeekPayingTown (utknieta karawana szuka miasta placacego - takze
  przy pustym pierwszym przebiegu BK). Oczekiwane: K294-1 0.58-0.62, odcinek 4.5-4.8.
- Seria m113\seria-140a.out.txt (DLL scratchpad\dll140a). Po tescie: porownanie z 139a, WGRANIE 140 gdy lepiej (wgraj139.ps1 jako wzor), potem STOP (tydzien) i przekazanie.

## DOPISEK 30.10 ok. 03:15 CEST - Jeff "pracuj jeszcze nad karawana dobranoc" (spi) -> runda 140 w toku (wf_a6b759b2-cac, noc/304-karawany-odcinki, zb\n304 od c825acb0 = WGRANE 139); tydzien 89%
- Zakres: czemu odcinki 4.47 -> 5.09 dnia w 139a (regula C skraca zmienione wybory), co trzyma 44% karawan ponizej zoldu (K294-22 ok. 110 'czeka', p25 -78), strata 1300 zl/d.
- Potem: test 140a (bitwy + 90 dob; gra wylaczona, Jeff spi), WGRANIE 140 gdy lepiej niz 139a. Przy ok. 97% tygodnia: stop i przekazanie (nic nie zaczynac, czego nie da sie zapisac).

## DOPISEK 30.10 ok. 03:10 CEST - WGRANIE 139 (noc/303-integracja c333583e, status c825acb0 wypchniety) - START DLA NASTEPNEJ SESJI
- W GRZE: Armoury.dll 49fdc97f885c1fe783b82264c7cc67ef (139). Kopie 138: Armoury.dll.bak-2026-10-30-przed-wgranie139 i D:\Backup-Bannerlord\wgrane\2026-10-30-wgranie139-przed.
  Skrypt scratchpad b2a84731\wgraj139.ps1. Zapis testowy D:\...\zapisy-testowe-archiwum\2026-10-30-139 (Game Saves czyste). LINIA GLOWNA: noc/303-integracja (zb\n303).
- 139a (grupy-139a, checker scratchpad b2a84731\chk139) wobec 138c: placace 0.529 -> 0.560, mediana +11 -> +37, wynik floty +78.9k -> +128.6k zl/d, T1 0.342 -> 0.400, zelazo zima
  -19.3 -> -5.5, strata 1014 -> 1300; NIE: odcinek 4.47 -> 5.09 (K294-19/31; regula C skraca zmienione wybory 5.11 -> 2.83 dnia - wydluzenie z innej przyczyny), N283-2/3/4.
- Otwarte: odcinki, placace >= 0.60, N283 scenariusze testowe, NWAR-15; do sprawdzenia przez Jeffa: kryjowka 293, lupy 300b, dowodca karawany 139b/139c.

## DOPISEK 30.10 ok. 02:30 CEST - 139c gotowe (n303 d4ad91e3 + c333583e, Armoury 49fdc97f), AUTOTEST 139a w toku (7 bitew + 90 dob)
- 139c: J = SkillJudge (tylko karawany z bohaterem-dowodca): regula A prog czekania obnizony o J x (prog - koszt), nigdy ponizej kosztu; trasa C zasieg x (1 + J); klucz
  CaravanLeaderJudgement; segment 'dowodca (139c)' w linii 139, wiersz K294-34. J=0 = 139 co do bitu (samotest C#).
- Seria m113\seria-139a.out.txt (DLL scratchpad\dll139a). Po tescie porownanie z 138c, WGRANIE 139 gdy lepiej.

## DOPISEK 30.10 ok. 02:20 CEST - 139 + 139b scalone (noc/303-integracja 265223bd od 713e3d1c, zb\n303, Armoury e36c20e0); 139c (SkillJudge w reguly 139) w toku (wf_dee3d176-53a)
- 139 (f9243c43 + 83894650): A CaravanWaitCostsWage (T1 ponad polowe stosu, gdy czekanie kosztuje wiecej niz da), B limit BK BuyCategory 1500 w skali ceny (transpiler; straznik
  zakupu tylko w locie podniesionym), C trasa z kosztem drogi (SaleValue tymi regulami kasy co sprzedaz; limit odcinka). Oczekiwane: placace 0.529 -> 0.54-0.58.
- 139b (d3d405d1 + 3df325fd): kara karawany z bohaterem-dowodca x max(0, 2H-1) (H jak 300b; S=300 -> marza T1 54 -> 61%), XP Handlu 0.5 x zysk ze sprzedazy; AI bez bohaterow (0/393).
- Potem: test 139a (bitwy + 90 dob przy wylaczonej grze), porownanie z 138c, WGRANIE 139.

## DOPISEK 30.10 ok. 01:25 CEST - Jeff "tak zrob": Handel + perswazja dowodcy karawany -> 139b w toku (wf_756ce95f-bfc, noc/302-karawana-handel-dowodcy, zb\n302 od 713e3d1c)
- Dzis: dowodca karawany wplywa tylko przez kare gry 1/(1+0.002 x Handel) na mala kare karawan (~0.5-1% ceny), BK nie patrzy na umiejetnosci, XP Handlu za zysk karawan nie widac.
- 139b: ceny karawany wedlug Handlu + perswazji dowodcy (ksztalt jak 300b), pomocnik SkillJudge(mp) dla decyzji 139 (czekanie/droga), XP Handlu z zysku wedlug stawki gry; grupa dowodca139.
- Potem scalic 301 (139) + 302 (139b) na nowej galezi od 713e3d1c, test 139a, WGRANIE 139.

## DOPISEK 30.10 ok. 01:05 CEST - Jeff "Napraw karawany" -> runda 139 w toku (wf_4fb21713-6bf, noc/301-karawany-czekanie, zb\n301 od 713e3d1c = WGRANE 138); tydzien 85%
- Zakres: (A) czekanie na rynek kosztem zoldu (106 ze 139 bez sprzedazy 'czeka' w 138c), (B) limit BK BuyCategory 1500 zl w skali ceny towaru (bawelna/bizuteria/futra 0-2 szt.),
  (C) wybor miasta BK z kosztem drogi (zold x dni) przy tych samych regulach kasy co sprzedaz (lekcja 294c D1). Cel 90 dob: K294-1 >= 0.58, odcinki nie dluzsze.
- Potem: build, test 139a (bitwy + 90 dob przy wylaczonej grze; zapis z Ironmana Jeffa), WGRANIE 139 gdy lepiej niz 138c.

## DOPISEK 30.10 ok. 00:45 CEST - WGRANIE 138 (noc/300-integracja fc263b03, status 713e3d1c wypchniety) - START DLA NASTEPNEJ SESJI
- W GRZE: Armoury.dll b17e67c108f7dfc8d550341cc74ed128 (138). Kopie 137: Armoury.dll.bak-2026-10-30-przed-wgranie138 i D:\Backup-Bannerlord\wgrane\2026-10-30-wgranie138-przed.
  CrashScribe bef9db58 i pliki danych bez zmian. Skrypt scratchpad b2a84731\wgraj138.ps1. Zapis testowy na D:\...\zapisy-testowe-archiwum\2026-10-30-138 (Game Saves czyste).
- 138 = 137 + 299b (pamiec polki sprzetu, plaski krok dla wszystkich przedmiotow) + 291d (obronca trzyma przy wylaczonej obronie gry) + 299c (waga od razu 0.14 x 0.06, pamiec podazy 0.15)
  + 300b (sprzedaz sprzetu gracza od ok. 31% wartosci, Handel + perswazja; ksiega odkupu) + 138c (wywoz kupcow: cel z polki fizycznej z towarem w drodze).
- Wyniki 138c (grupy-138c, checker scratchpad b2a84731\chk138c) wobec 299a: placace 0.564 -> 0.529, mediana +26 -> +11, wynik floty +31.5k -> +78.9k zl/d, strata 2245 -> 1014 zl/d,
  glodne 0.126, wywoz sprzetu x0.88, zelazo caly bieg 0.0 (zima -19); bitwy B291-2 TAK; J204-2/3 i N283-1 w tym biegu bez epizodu jazdy (kod bitew jak 138b, tam TAK - losowe).
- LINIA GLOWNA: noc/300-integracja (zb\n300). Otwarte: placace >= 0.60, zelazo zima, N283-2/3 scenariusze testowe, NWAR-15; kryjowka 293 i lupy 300b - czekaja na gre Jeffa.

## DOPISEK 30.10 ok. 00:20 CEST - 138c GOTOWE (n300 b37fbf0d + fc263b03, Armoury b17e67c1), AUTOTEST 138c w toku (7 bitew + 90 dob)
- Analiza 299a vs 138b: (b) rozklad - zysk floty bez zmian w granicach szumu (90.9k +- 6k vs 92.4k), przeszedl z T1 (16.5k -> 11.5k) na hurtowa sprzedaz BK/gry (6.6k -> 13.0k);
  T1 blokowane kasa miast (BuyFloor 124 -> 152 krokow/d). Kasy drenowal wywoz kupcow sprzetu: cel wedlug pamieci polki (sztuka w drodze 6%) -> ping-pong, wozy 9.5k -> 30.5k szt.,
  zloto 0.20M -> 1.31M zl/d. Krzywa DepthNet - nie glowna przyczyna; strata K294-3 - pojedyncze doby (szum); glod - dluzsze odcinki i stojace karawany.
- 138c (klucz TradeExportPhysicalShelf): cel wywozu z polki fizycznej z towarem w drodze, zrodlo z pamieci polki (ta sama, z ktorej kupiec placi); linia 'Rozklad wyniku karawan
  (138c)' p25/p50/p75/top10% (K294-26 INFO), C299-18e INFO. Seria m113\seria-138c.out.txt (DLL scratchpad\dll138c).

## DOPISEK 29.10 ok. 23:25 CEST - WYNIK 138b (ca055bde, 7 bitew + 90 dob, kod 0, ERROR 0) - NIE wgrane (karawany typowo gorzej); 138c w toku (wf_2b4fafc8-046)
- grupy-138b (checker scratchpad\chk138b). 299a -> 138b: placace 0.564 -> 0.485, mediana +26 -> -11, wynik floty +31.5k -> +33.4k, strata wszystkie drogi 2245 -> 4301 zl/d,
  glodne 0.120 -> 0.153, wywoz sprzetu kupcow x3.26 (C299-18 NIE), zelazo ost. 30 dob -24.6 -> -9.3 (TAK); C299-14 spadek 20. sztuki 63% -> 19% (TAK); B291-2 TAK; tylko N283-2/3 NIE.
- 138c: 2 analizy (karawany: niezgodnosc krzywej DepthNet/MaxAtCost z plaska sprzedaza 299c?, rozklad zysku; sprzet: wywoz x3.3) -> poprawka -> recenzja, na n300.
- Zapis testowy przeniesiony do D:\...\zapisy-testowe-archiwum\2026-10-29-138b. W grze 137.

## DOPISEK 29.10 ok. 22:30 CEST - 299c + 300b scalone (n300 ca055bde = 137 + 299b + 291d + 299c + 300b, Armoury 08fc9aec), AUTOTEST 138b w toku (7 bitew + 90 dob); tydzien 79%
- 299c (8e09811c + 7a863675): PriceInstantShare 0.06 (waga od razu 0.0084 / podaz 0.1316) osobno od PriceSupplyMemoryDaily = 0.15 (tempo gry). Dowod 138a: przesyt w miastach
  drozszych (287 p75) x0.79-0.81 w dobach 31-90, marza miast 94.5% -> 71%, karawany stloczone (K294-18 0.666 -> 0.588). Oczekiwane: karawany wracaja do poziomu 299a.
- Seria m113\seria-138b.out.txt (DLL scratchpad\dll138b; zapis testowy skopiowany z Ironmana 01:31). Po tescie: porownanie z 299a, WGRANIE 138 gdy bez regresji;
  sprzatnac zapis testowy (D:\...\zapisy-testowe-archiwum), CHANGELOG statusy, push noc/300-integracja, pamiec jeff-gra-od-15-10-monitoring.

## DOPISEK 29.10 ok. 22:15 CEST - 300b GOTOWE (noc/300b-lupy-cena 7a5e74c4, Armoury 8650b541, po recenzji); czeka na 299c, potem scalenie do n300 i test 138b
- PlayerHaggle: strona gracza (gracz, partie klanu z bohaterem) przy sprzedazy broni/zbroi/uprzezy bez x5 BK, kara gry x 1/(1/tfHandel + 0.002 x perswazja) - perswazja
  (Persuasion.Measure: Urok + perki) dodana do Handlu punkt za punkt (stawka gry 0.002). Ulamek wartosci (bez perkow, bpf 0.8): tier1 0.31 (S=0) / 0.35 (100) / 0.39 (250),
  tier6 0.21 / 0.24 / 0.275, kon 0.54 / 0.58 / 0.64; Jeff (Handel 18 + Urok 18) tier1 0.33. Kupno: narzut gracza RetailMarkupPercent x h (10% -> mniej z umiejetnoscia).
- Sufit 1/10 ceny nowej (SellByCondition.Cap, Jeff 07.10 - petla kup wrak/napraw/sprzedaj) dla gracza zastapiony ksiega odkupu PlayerRebuyCap (sprzedaz nie drozej niz kupiles
  w tym miescie); zawsze cena sprzedazy <= kupno - 1. Grupa lupy300. DO POWIEDZENIA JEFFOWI.

## DOPISEK 29.10 ok. 22:05 CEST - WYNIK 138a (137 + 299b + 291d, 7 bitew + 90 dob, kod 0): bitwy OK (B291-2 TAK), karawany GORZEJ -> 299c w toku; NIE wgrane
- grupy-138a (checker scratchpad\chk138): B291-2 TAK (0 s ataku przy trzymaniu, bez wagi obrony 0); N283-4 NIE (FF lucznikow 4 przy 66 strzalach); C299-14 TAK (spadek 20. sztuki
  sprzetu 63% -> 18%). ALE karawany 299a -> 138a: placace 0.564 -> 0.402, mediana +26 -> -31, wynik floty +31.5k -> +14.0k zl/d, miasta bez rudy 11.0 -> 16.1, wywoz sprzetu x1.77.
- Hipoteza: pamiec podazy 6%/d trzyma przesyt po sprzedazy 2.5x dluzej (karawany sprzedaja taniej); Jeffowi chodzilo o spadek OD RAZU. 299c (wf_a0026752-793, na n300):
  PriceInstantShare 0.06 (waga od razu 0.14 x 0.06) osobno od PriceSupplyMemoryDaily z powrotem 0.15. Potem test 138b, 300b (lupy gracza) do scalenia.
- Zapis testowy przeniesiony do D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-29-138a (Jeff moze grac na 137).

## DOPISEK 29.10 ok. 21:45 CEST - 291d scalone do n300 (438ae94f = 137 + 299b + 291d, Armoury e92b9e81), AUTOTEST 138a w toku (7 bitew + 90 dob)
- 291d (071d93d5 + 7304bd1e po recenzji): postfiks TeamAIComponent.CheckIsDefenseApplicable - obronca trzymajacy (regula B) dostaje obrone gry TAK (gra gasila ja po poleglych od
  pociskow: wzor (czlonkow x MaxUnderRangedAttackRatio + zabici pociskiem) / ... ^(3 x udzial strzelcow wroga) > 1.5); po S2 bez wymuszenia (bez wahadla).
- Jeff nie gral na 137 (zapis 01:31, md5 2fd910a1) - zapis testowy ten sam. UWAGA: stare logi m113\logi\138a-* z 25.10 - brac najnowszy po dacie.
- Seria m113\seria-138a.out.txt (DLL scratchpad\dll138a). 300b (lupy gracza 30% + Handel/perswazja, wf_3a7bf5da-2e1) w toku - do scalenia po tescie.

## DOPISEK 29.10 ok. 21:10 CEST - po WGRANIU 137: 299b gotowe, 291d w recenzji, 300b (lupy gracza) w toku; linia 138 = noc/300-integracja (zb\n300)
- noc/300-integracja od 94d88559 (137): merge 299b (b5b9f4be, Armoury f1961410) - pamiec podazy 6%/d (RawPrice.SupplyPacePostfix na DefaultSettlementEconomyModel), plaski krok
  ceny sprzetu/lupow/koni (ShelfMemory w SupplyDemand). Odkrycie 299b: gracz sprzedaje sprzet za ok. 8% wartosci przez kare BK x5 (AI lordowie 37-43%, inne partie 60-64%).
- Jeff 21:05: "dajmy startowo np. 30% ale ceny zalezne sa od persuazion i trade im wiecej trade i persuwazion tym mam lepsze ceny, tak aby to mechnik aograc a nie na sztywno"
  -> 300b (wf_3a7bf5da-2e1, noc/300b-lupy-cena od b5b9f4be, zb\n300b).
- 291d (wf_94efa800-3f4, noc/291d-obronca od 94d88559): obronca bez wagi obrony tez trzyma (071d93d5 + recenzja).
- Nastepne: scalic 291d i 300b do n300, test 138a (bitwy + 90 dob, gra wylaczona, zapis testowy z Ironmana Jeffa jesli gral), WGRANIE 138.

## DOPISEK 29.10 ok. 20:40 CEST - WGRANIE 137 (noc/296-integracja 204deda8, status 94d88559 wypchniety) - START DLA NASTEPNEJ SESJI
- W GRZE: Armoury.dll be4919561c34926b534d2b2d7672cf78 (137). Kopie 136: Modules\Armoury\bin\Win64_Shipping_Client\Armoury.dll.bak-2026-10-29-przed-wgranie137 i
  D:\Backup-Bannerlord\wgrane\2026-10-29-wgranie137-przed. CrashScribe bef9db58 i pliki danych bez zmian. Skrypt: scratchpad sesji b2a84731\wgraj137.ps1.
  Zapis testowy przeniesiony do D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-29-137 (w Game Saves brak autotest-*); BannerlordConfig LatestSave = Ironman Jeffa.
- 137 = 136 + 290 (zelazo/jedzenie karawan), 291/291b/291c (bitwa: armia nie staje, obronca trzyma, bieg bez tarcz, poscig 360 m), 292 (stamina x2), 293 (boss kryjowki - bez
  autotestu), 294/294b/294c(D1/D2 NIE)/298/298b (karawany: jedzenie wedlug rodzaju, cena wedlug glebokosci rynku, bramka jedzenia z miast placacych, spizarnia na odcinek,
  straz gracza jak AI), 295 (orszaki BK z ludzi wsi), 297/297b (zima nie tnie kopaln, eskorta milicji wozow), 299 (plaski krok ceny towarow handlowych 0.021/0.119).
- Wyniki 299a wobec 136a: placace zold 0.37 -> 0.56, mediana -52 -> +26 zl/d, wynik floty +6k -> +31.5k zl/d, psucie 216 -> 6 szt./d, glodne 0.15 -> 0.12, zelazo ost. 30 dob
  -66 -> -25 (caly bieg +2.2; zima -30). Grupy: m113\wyniki\grupy-299a (checker scratchpad b2a84731\chk299).
- LINIA GLOWNA od teraz: noc/296-integracja (zb\n296-integracja, scratchpad sesji b2a84731). Starsze noc/285-* NIEAKTUALNE.
- W TOKU / NASTEPNE: 299b (wf_cade3230-ca8, noc/299b-ceny-wszystkie od 204deda8) - plaski krok dla lupow/sprzetu/koni + pamiec podazy 6%/d (Jeff: "15% dziennie za duzo,
  dajmy 5-8%"); B291-2 obronca bez wagi obrony atakuje (1 z 7 bitew 30 s) - wymusic trzymanie; cele nieosiagniete: placace >= 0.60 (0.56), zelazo w zimie -30/d, N283-2/3
  scenariusze testowe; NWAR-15; L2 (limit BK 1500 zl na kategorie) jako kandydat karawan.

## DOPISEK 29.10 ok. 20:25 CEST - Jeff o cenach: "tak dla wszysatkich - 15% dziennie za duzo, dajmy 5-8% dziennie. Czemu 15%?" -> 299b w toku
- 15% = stala gry dailyChangeFactor (DefaultSettlementEconomyModel.GetSupplyDemandForCategory), ustawiona pod rok 84 dni; u nas rok 364. Decyzja: 6%/d.
- Workflow wf_cade3230-ca8: 299b na noc/299b-ceny-wszystkie (zb\n299b od 204deda8): plaski krok ceny dla WSZYSTKICH przedmiotow (lupy, sprzet, konie) + pamiec podazy 6%/d
  (wagi 299: od razu 0.14 x 0.06, podaz reszta). Test 299a (bez 299b) dalej w toku jako baza; potem test 299b, WGRANIE 137 z lepszej.

## DOPISEK 29.10 ok. 20:15 CEST - 299 scalone (n296 204deda8 = 298b + 299, Armoury be491956), AUTOTEST 299a (7 bitew + 90 dob) w toku - kandydat WGRANIE 137
- 299 (f3ad423f + 9de47b57): PriceFlatterLotWalk - w RawPrice.FactorPrefix (GetBasePriceFactor, tylko towary handlowe) swiezy towar wazy od razu 0.021 zamiast 0.04, podaz 0.119
  zamiast 0.10 (razem 0.14 jak gra) - lot przesuwa cene ok. 1.9x mniej, cena rownowagi bez zmian, bez arbitrazu w jednej wizycie; DepthNet 294b po recenzji liczy kolejke krzywa gry;
  sonda i linie PriceWalk299, grupa ceny299. NIE obejmuje lupow/sprzetu (recenzja: w logu Jeffa tylko sprzedaz sprzetu) - pytanie do Jeffa.
- Seria m113\seria-299a.out.txt (DLL scratchpad\dll299a). Po tescie: grupy bitew + 90 dob (chk z 204deda8), porownanie z 298a, WGRANIE 137 gdy bez regresji.

## DOPISEK 29.10 ok. 19:35 CEST - WYNIK 298a (298 + 298b, 90 dob, kod 0) - lepiej niz 296e; czeka 299 ceny
- (checker scratchpad\chk298, wyniki m113\wyniki\grupy-298a-v298 i grupy-296e-v298) 296e / 298a: placace ost. 7 dob 0.474 / 0.484 (doby 61-90 0.415 / 0.426), mediana -10 / -4,
  wynik floty po wszystkim +22 975 / +20 382 zl/d, strata wszystkie drogi 2713 / 1817 zl/d, sprzedaz jedzenia 625 / 935 szt./d, glodne 0.111 / 0.128, zelazo caly bieg -0.6 / +7.4
  (zima -21.6 / -10.7), miasta bez rudy 9.3 / 13.2 (Z297-5 NIE w 298a). K294-22: 144 z 420 bez sprzedazy, w tym 108 'towar z rynkiem z zyskiem - czeka'.
- Karawana Jeffa w 298a rozbita dzien 109086 przez armie krola Joffreya (370 ludzi, wojna) - nie przez zwolnienia strazy.
- Nastepne: 299 (ceny plaskie - glebokosc rynku, wiecej sztuk z zyskiem na wizyte) + ewentualnie L2 (limit BK 1500 zl na kategorie), scalenie 298+299 do n296, test.

## DOPISEK 29.10 ok. 19:00 CEST - 298 + 298b na noc/298-karawany-l1 (ef19a8fb, Armoury 242a9e71), AUTOTEST 298a w toku; 299 ceny w toku (wf_ad3c0943-9e5)
- 298 (1b5a72a0 + poprawki po recenzji): L1 CaravanFoodGateByPayingTowns (bramka psujacego jedzenia = N miast placacych x zjedzone w odcinku), miary M1-M6 (K294-6, K294-2b
  pelny wynik floty, K294-3 wszystkie drogi, K294-1/2 doby 61-90, linia 'Karawany bez handlu i kohorta (298)'). Bazy poprawionymi miarami 296c / 296d: wynik floty po wszystkim
  +7460 / -2819 zl/d; strata wszystkie drogi 3169 / 5580 zl/d; K294-1 doby 61-90 0.427 / 0.392.
- 298b (ef19a8fb): E5 CaravanPantryLegDays (spizarnia eskorty = odcinek + 2 doby, max 10) i CaravanGuardPlayerAfterNotice TAK (Jeff 18:35).
- Seria m113\seria-298a.out.txt (DLL scratchpad\dll298a). Potem: scalic 298 i 299 do n296, test 90 dob (+ bitwy przed wgraniem), WGRANIE 137.

## DOPISEK 29.10 ok. 18:45 CEST - dwie decyzje/prosby Jeffa w trakcie
- Jeff 18:35: "moja krawana tez moze zwlaniac strazników jak AI" -> CaravanGuardPlayerAfterNotice domyslnie TAK (klucza nie ma w jego Armoury.json); do zrobienia na n298 po workflow 298.
- Jeff 18:40: "plus zmiana ceny wydjae mi sie ze za bardzo spada cena jak sprzedaje towar, ze trzeba bardziej wyplaszczyc zmiane kursu twoaru gdy jest sprzedaz i zakup"
  -> paczka 299 (wf_ad3c0943-9e5: 2 analizy -> projekt -> kod -> recenzja) na noc/299-ceny-plaskie (zb\n299-ceny od 2db296ae); potem scalic z 298, test 90 dob.

## DOPISEK 29.10 ok. 18:25 CEST - WYNIK 296e (2db296ae = 294c bez D1/D2, 90 dob, kod 0) + analiza wf_75d54b6f-4eb; runda 4 (298) w toku
- 296c / 296d / 296e: placace 0.503 / 0.441 / 0.474; mediana -1 / -26 / -10; glodne 0.204 / 0.174 / 0.111 (TAK); odcinek 4.31 / 5.39 / 4.33; zelazo caly bieg -7.5 / +5.0 / -0.6
  (zima -32 / -20 / -22); miasta bez rudy 14.4 / 10.6 / 9.3 (TAK); sprzedaz jedzenia karawan 939 / 609 / 625 szt./d; ruda do miast z brakiem 26.4/d.
- Analiza: D1 zerowal miasta z kasa <= rezerwy kupcow (54% zer), a BK placi z calej kasy - sprzedaz z takich miast -24k zl/d, kursy x1.25; D2 bez zysku. Dzwignia: bramka psujacego
  jedzenia swiata 124 ryb wobec popytu 1718/d (L1). Miary: K294-6 'pula 0' to blad checkera, K294-3 tylko T1, K294-1/2 bez najmu strazy i jedzenia drogi. Plan: scratchpad\plan-runda4.md.
- Decyzje: D1/D2 zostaja NIE; bulk D1 niepotrzebny (ruda do miast z brakiem 1.6 wjazdu/d); E5 - sprzedaz jedzenia < 750 przy H1 - spizarnia eskorty na zmierzony kurs zamiast 10 dob.
- W toku wf_9644b104-bae: 298 (L1 CaravanFoodGateByPayingTowns + miary M1-M6) na noc/298-karawany-l1 (zb\n298-karawany-l1 od 2db296ae); potem E5 i test 298a.

## DOPISEK 29.10 ok. 17:50 CEST - WYNIK 296d (294c, 90 dob, kod 0): karawany GORZEJ niz 296c, zelazo lepiej; eksperyment 296e (D1/D2 NIE) w toku + analiza wf_75d54b6f-4eb
- grupy-296d (chk296d): placace 0.503 -> 0.441, mediana -1 -> -26 (wynik 65 / zold 89), sprzedaz w 7 dobach 0.71 -> 0.61, odcinek 4.31 -> 5.39 dob, K294-6 'pula 0' (miara?),
  glodne 0.174 (zima 0.087 TAK), strata 2461 zl/d, karawana Jeffa rozbita po 54 dobach; zelazo caly bieg +5.0/d, ost. 30 dob -15.6 (zima -20), miasta bez rudy 10.6 TAK.
- 2db296ae: D1 CaravanRouteBySale i D2 CaravanRouteByBuyRules domyslnie NIE (G/F/H1 zostaja), Armoury 47fb37ca -> test 296e (m113\seria-296e.out.txt).
  Rownolegle analiza (3 soczewki: czemu D1/D2 zle, sufit zysku z roznicy cen, poprawnosc miar K294) + plan rundy 4.

## DOPISEK 29.10 ok. 17:10 CEST - 294c GOTOWE (n296 7cbdc711 + ce9f772e po recenzji, Armoury 04a7bc8b), AUTOTEST 296d w toku (90 dob)
- 294c: D1 CaravanRouteBySale (wybor miasta BK wedlug tego, ile towaru sie tam sprzeda powyzej progu, z kolejka innych karawan i kasa miasta), D2 CaravanRouteByBuyRules (BK nie jedzie
  kupowac tam, gdzie nasze reguly zablokuja), G CaravanOreGateByShortTowns (bramka rudy z popytu miast z brakiem), F KsIronWorldFlowOwnTownCut (krok 290 tnie przy PELNY tylko
  powyzej progu i tylko kopalnie miast pelnych), H1 CaravanPantryAllSales (spizarnia eskorty przy kazdej sprzedazy). Kapital obrotowy (CapFloor) NIE ruszany - projektant: nie wiaze.
- Oczekiwane wg projektu: placace 0.56-0.65, mediana +10..+30, glodne 0.12-0.17, strata 1-2k zl/d (dalej NIE), karawana Jeffa -8..-12k (straz gracza bez zwolnien
  - CaravanGuardPlayerAfterNotice NIE - decyzja Jeffa). Seria m113\seria-296d.out.txt (DLL scratchpad\dll296d).

## DOPISEK 29.10 ok. 15:55 CEST - WYNIK 296c (90 dob, n296 971570ad z 294b, kod 0): karawany duzo lepiej, ale nie u celu; runda 3 (294c) w toku na n296
- grupy-296c (checker scratchpad\chk296c): K294-1 placace 0.325 -> 0.503, K294-2 mediana -54 -> -1 zl/d (wynik 87 / zold 83), K294-6 pula x10.8, K294-11 obrot 17 dob, K294-13
  liczba 0.91; NIE: K294-3 strata T1 3728 zl/d (stary towar regula 5), K294-9 glodne 0.204, K294-10 sprzedaz w 7 dobach 0.71, karawana Jeffa -15 908 w 89 dobach,
  udzial wyplat 259 0.123; zelazo w zimie -32/d (296b -9), cena rudy bez rudy/zrodla 2.81 -> 4.07, miasta bez rudy 14.4.
- Workflow wf_4b3014e3-6ad (294c: kapital obrotowy CapFloor, glod, strata starego towaru, karawana gracza, ruda karawanami) pisze prosto na noc/296-integracja (zb\n296-integracja).
  Potem test 296d (probyN 90; bitwy bez zmian od 296b).

## DOPISEK 29.10 ok. 15:15 CEST - WYNIK 296b (bitwy + 90 dob na n296 2fa9d4d3 = 291b/c + 297/297b, bez 294b): BITWY i ZELAZO OK; 294b scalone (971570ad, Armoury 61c746de), AUTOTEST 296c w toku
- Wyniki m113\wyniki\grupy-296b (checker scratchpad\chk296b). Bitwy: B291-1/2 TAK, N283-4 TAK (jazda 113 m), S292-7/8 x1.97/x2.04, S292-9 TAK; NIE tylko scen. testowe N283-2 B / N283-3 C
  (w bitwach wykrycia wrecz 156 - reakcja dziala, scenariusz testu slaby). ZELAZO: Z297-1 zima 1.00, Z297-3 ost. 30 dob -8.3/d (caly bieg -9.5), Z297-4 2.6%, Z297-10 zatkane
  kopalnie 0.30/26, miasta bez rudy 12.7 (TAK). Karawany (bez 294b): placace 0.405, mediana -45, glodne 24.5% (NIE).
- 294b (55f4568f + 3b94543a): cena sztuki wedlug glebokosci rynku (CaravanBuyByMarketDepth), bramka jedzenia 290 bez spizarni eskorty (CaravanFoodGateTradeStock), jedzenie
  na zmierzona droge (CaravanRoadFoodMeasuredLeg); 294 B/C (zwijanie, bramka nowych) domyslnie NIE. Oczekiwane wg projektanta: placace 0.40-0.50 (ogranicza kapital obrotowy
  CapFloor ok. 4.9k miejsca wobec 5.7-8k towaru) - nastepna runda: kapital obrotowy karawan.
- Seria m113\seria-296c.out.txt (testy-noc-204.ps1 kopia,probyN 90, DLL scratchpad\dll296c).

## DOPISEK 29.10 ok. 14:45 CEST - 291b/291c + 297/297b na n296 (2fa9d4d3, Armoury 60b6de3e), AUTOTEST 296b w toku; 294b karawany w toku
- 291b (c472b9f2 + 65d2134a): scen. C celuje w najblizszego pieszego strzelca wroga, pieszy nekajacy testu z limitem oddechu; smycz jezdzca od srodka szyku (LeashHome);
  S292-9 konie - roznica to inne konie w zapisie (porownanie tylko tych samych). 291c (629b5264) Jeff 14:30: "moze zostac te 358 metrow" - RideLeash 360 m (noca polowa),
  N283_CAV_FAR 375. 297 (zima nie tnie rudy/wyrebu) + 297b (eskorta milicji wsi dla wozow trzymanych przez bandy - zatkane kopalnie, m107 K1) scalone (merge 2fa9d4d3).
- Otwarte z 297b: miasta bez rudy (poza zasiegiem wozow kopaln 250 - to robota karawan/zamowien 282; karawany kupno rudy stoi) - do 294b/nastepnej rundy.
- Seria m113\seria-296b.out.txt (DLL kopia scratchpad\dll296b, prefiks 296b, bitwaN + probyN 90). Potem: 294b do n296, test 296c.

## DOPISEK 29.10 ok. 14:10 CEST - WYNIK 296a (7 bitew + 90 dob, kod 0, gra przywrocona) - NIE do wgrania (karawany); runda 2 w toku
- Wyniki m113\wyniki\grupy-296a (bitwaN-*, probyN-*; checker z 1e75662c w scratchpad\chk296). BITWY: B291-1 armia nie staje (R5 0), B291-2 obronca trzyma (862 s),
  stamina x1.97/x2.03 TAK; NIE: N283-3 scen. C (wrecz) wykryc 0, N283-4 smycz jazdy 358 m > 265, S292-9 konie dluzszy galop (77->88, 68->94 s), B291-3 bieg bez tarcz 0 wejsc.
- 90 DOB: D295-2 'w nicosc inne' 4.26 -> 0.06/d TAK, jedzenie psuje sie 0. ZLE: karawany 294 - zwijanie zabilo 2/3 handlu (zmierzonych 423 -> 133, pula wyniku 0.43),
  wyprzedaz zwinietych ponizej kosztu 3195 zl/d, placace zold 0.325, mediana wynik-zold -54 (zold 111), glodne w drodze 24.6%; zelazo przed zima 0.0/d, w zimie -146/d (297 nie w tym tescie);
  NWAR-15 plotno/len 1.33 -> 1.08 dalej NIE; rozbite karawany ost. 30 dob 4.93/d.
- W toku: wf_a7f0e9a1-0e4 (297b zatkane kopalnie, n297), wf_79a02b88-d4b (291b bitwa runda 2 na n296-integracja), wf_d420bf82-97f (294b karawany od nowa: 3 analizy ->
  projekt -> kod na n294-karawany-zysk). Potem: scalic 294b + 297 do n296, test 296b (bitwy + 90 dob).

## DOPISEK 29.10 ok. 13:40 CEST - 291-295 SCALONE (noc/296-integracja 1e75662c, zb\n296-integracja, Armoury 1f40b2bf), AUTOTEST 296a w toku (7 bitew + 90 dob)
- 291 bitwa (d7e34c5b): bez R5 (cala armia nie staje przez nekajacego), R4 tylko strzelcy, smycz R2 od miejsca wyslania (ArmyMarchesPastHarassers); obronca atakowany
  trzyma pozycje, konni lucznicy nekaja (DefendersHoldTheirGround, Doctrine.Hold291.cs); piechota bez tarcz biegnie pod ostrzalem (UnshieldedRushUnderFire); test 283 B/C oslona
  nekajacego testu, J204-1 regex. 292 stamina x2 (HumanStaminaMultiplier 2.0, RBM pula/stan/regeneracja, oddech 137). 293 boss kryjowki (HideoutBossTalkFix - wiersz rozmowy
  ponad guard gry + zapas walki; brak autotestu kryjowki - sprawdzi Jeff). 294 karawany: jedzenie psuje sie wedlug rodzaju (CaravanFoodKeepsByKind), stratne karawany zwijane
  (CaravanLosingWoundUp), nowe tylko gdy handel placi (CaravanEntryWhenTradePays). 295 orszaki dworow BK z ludzi wsi (BkRetinueFromVillage) - 'w nicosc/z niczego inne'.
- Seria: m113\seria-296a.out.txt (testy-noc-283.ps1 -Prefix 296a -Runs kopia,bitwaN,probyN -ProbyDays 90 -Harasser 3, CsAt cs-at283). 297 zelazo w toku (wf_e3b8a33f-099) - do scalenia potem.

## DOPISEK 29.10 ok. 12:50 CEST - WYNIK 137a (sama 290, 90 dob, kod 0, gra przywrocona do 136) - 290 NIE do wgrania samo
- Wyniki m113\wyniki\grupy-137a. DOBRZE: sprzedaz na stracie 2649 -> 8 zl/d (Z290-4 TAK), jedzenie na stracie 0. ZLE: zelazo swiata ostatnie 30 dob -106.6/d (136a -66.5),
  wydobycie 358 -> 242 lup/d przy zuzyciu ok. 340 (rece tylko -19%, model wsi 355 -> 235 - przyczyna nie w rekach), miasta bez rudy 7.6 -> 11.9; K288-3 karawany glodne
  w drodze 26% (136a 13.4%) - bramka jedzenia 290 za ciasna (do 294); H287-2 wynik handlu 30 wobec zoldu 85 zl/d (bez zmian).
- Workflow wf_e3b8a33f-099: paczka 297 zelazo (noc/297-zelazo, zb\n297-zelazo od 0b2ed557) - do scalenia recznie do n296 po workflow wf_113dbfca-f46.

## DOPISEK 29.10 ok. 12:15 CEST - NOWE KONTO (sesja b2a84731): 290 zacommitowane, test 137a w toku, 4 uwagi Jeffa + drobne w workflow wf_113dbfca-f46
- W GRZE dalej 136 (Armoury ba0fbc05, CrashScribe bef9db58 - sprawdzone md5). Logi sesji Jeffa 29.10 00:09-01:52: D:\Backup-Bannerlord\logi-jeffa\2026-10-29-1200-sesja-0009.
- 290 (agent poprzedniego konta urwany limitem tuz przed commitem) = commit 0b2ed557 na noc/285-zold-srebro-ruda (zb\n285), Armoury a1fe2235, bez recenzji.
- AUTOTEST 137a = sama 290, kopia,probyN 90 dob (m113\seria-137a.out.txt; zapis testowy odswiezony z Ironmana Jeffa 01:31, md5 2fd910a1).
- Jeff 12:00: (1) AI bitwy - cala armia staje, gdy gracz atakuje jazde; piechota miota sie miedzy graczem z tylu a lucznikami z przodu (ma isc glowna sila, maly oddzial na gracza);
  atakowany przez gracza broni sie w miejscu zamiast kontratakowac; piechota bez tarcz pod ostrzalem ma biec, z tarczami mur tarcz; (2) stamina ludzi x2 (konie bez zmian);
  (3) kryjowka: brak dialogu bossa, boss bez obrazen, trzeba Retreat; (4) karawany glownie traca - jedzenie ma sie nie psuc albo duzo wolniej.
  Potem: "i napraw pozostale bledy i pusc test 90, rob tak dlugo porawi az bedziesz zadowlony z wyniku."
- Galezie od 0b2ed557 (worktree scratchpad sesji b2a84731\zb\): noc/291-bitwa-ai (+ testy 283 B/C, J204-1), noc/292-stamina, noc/293-kryjowka-boss, noc/294-karawany-zysk,
  noc/295-drobne (NWAR-15, 'w nicosc', bledy z logu Jeffa); scalenie -> noc/296-integracja (zb\n296-integracja). Workflow wf_113dbfca-f46 (skrypt
  ~\.claude\projects\C--Program-Files--x86--Steam-...\b2a84731-...\workflows\scripts\jeff-29-10-uwagi-wf_113dbfca-f46.js; wznowienie tylko w tej sesji - inaczej sprawdzic git log galezi).
- Dalej: test 90 dob + bitwy na n296 (testy-noc-204.ps1 probyN 90 + testy-noc-283.ps1 bitwy), poprawki w petli az wynik dobry, WGRANIE 137.

## DOPISEK 29.10 - Jeff "sprobuj to" (zelazo swiata -66/d, sprzedaz na stracie 2.6k/d) - paczka 290 w toku (workflow wf_86d2f62a-4ca)
- Skrypt workflows/scripts/paczka-290-wf_86d2f62a-4ca.js (bez args; resumeFromRunId), galaz noc/285-zold-srebro-ruda (zb/n285, od 40d2e207 = WGRANE 136).
  Potem test 90 dob (testy-noc-204.ps1 -Runs kopia,probyN -ProbyDays 90; Jeff gra na 136 - test dopiero po zamknieciu gry; odswiezyc zapis testowy) i WGRANIE 137.

## DOPISEK 29.10 ok. 00:15 CET - START DLA DRUGIEGO KONTA (Jeff: "ok zapisz do przekazania na drugie konto") - stan zebrany w jednym miejscu
- W GRZE: WGRANE 136 = Armoury.dll md5 ba0fbc0566593f77093b2b4fa07628e8 (galaz noc/285-zold-srebro-ruda, commit statusu po 52da829d, wypchnieta).
  CrashScribe bef9db58, pliki danych: arm_houses_canon.tsv z 277 (5e1d8c20). Kopie poprzednich: Modules\Armoury\bin\Win64_Shipping_Client\Armoury.dll.bak-*
  i D:\Backup-Bannerlord\wgrane\2026-10-2x-wgranieNNN-przed. Kampania Jeffa: IronmanKkUpHftX7ZIq.sav (Ironman), Jeff gra jako najemnik Polnocy (battania).
- LINIA GLOWNA kodu: worktree scratchpad sesji fffc991e (projekt C--Users-GAME-Bannerlor2-Realm-of-Thrones)\zb\n285, galaz noc/285-zold-srebro-ruda.
  Historia wgran: 129 (241/259/264/268/272/274), 130 (277), 131 (280/281/282), 132 (284), 133 (283), 134 (285), 135 (286/286b/287), 136 (288/289).
  UWAGA: noc/161-integracja i noc/132-handel-bez-283 sa STARSZE - nie budowac z nich. Kolejne paczki: nowa galaz od noc/285-zold-srebro-ruda.
- PROCEDURA (sprawdzona dzis): build `cd zb\<w>\Armoury && dotnet build Armoury.csproj -c Release -v q --nologo`; autotest przy WYLACZONEJ grze:
  scratchpad sesji 6570af7b...\m113\testy-noc-204.ps1 -Dll <katalog> -NowyArmoury <dll> -Tree <worktree> -Prefix <id> -Runs kopia,bitwaN,probyN -ProbyDays 30|90
  -CsAt <...\6ba92574...\scratchpad\test\CrashScribe-at-n204.dll> -ExtraFlags armoury-pokoj264.flag,armoury-zbroja268.flag,armoury-rownowaga274.flag,
  armoury-karawany.flag,armoury-rody277.flag (bitwy 283: testy-noc-283.ps1 z 7 bitwami i -CsAt scratchpad sesji 1e1c276a\cs-at283\CrashScribe-at-n283.dll).
  Przed testem: gdy Jeff gral - skopiowac jego Ironman do autotest-przekierowany-IronmanKkUpHftX7ZIq.sav i usunac m113\zapisy\baza-*.sav (wrapper zrobi nowe zdjecie).
  Grupy: python -I <worktree>\tools\sprawdz_logi.py --grupa <g> <log> (w logi\<id>-probyN moze lezec stary log - brac najnowszy po dacie).
  Wgranie: skrypt scratchpad sesji 1e1c276a\wgrajNNN.ps1 (wzor wgraj136.ps1: sprawdza gre, md5 starej i nowej, kopie .bak i na D:), potem zapis testowy na D:
  (zapisy-testowe-archiwum), statusy CHANGELOG (python z bytes([92]) dla backslashy - heredoc je gubi), pamiec jeff-gra-od-15-10-monitoring + MEMORY.md.
  Scalanie CHANGELOG/sprawdz_logi: scratchpad sesji 1e1c276a\scal_sklad.py <plik> <BAZA> <GALAZ1> <GALAZ2>.
- OTWARTE (kolejnosc wedlug wagi dla Jeffa):
  1. ZELAZO SWIATA spada (136a: -66.5/d, rece przy zelazie -27%); miast bez rudy malo (6.8), ale zapas sie kurczy - KsIronFullOnlyOwnTown/KsIronLocalGlutBand/
     KsIronStockTrend ciagle tna; spadek rak Essos miedzy krokami f5 (109084-109091) niewyjasniony.
  2. KARAWANY: wynik handlu mediana 33 zl/d wobec zoldu 85 (37% karawan >= zold); udzial wyplat 0.146; sprzedaz na stracie 2649/d (H284-4); srebro w jukach;
     karawana Jeffa: zysk z handlu juz dodatni, netto po zoldzie ujemne.
  3. 283 bitwa: scenariusze testowe B (pieszy) i C (wrecz) nie wykrywaja (testowy ranny/w panice), J204-1 regex; scen. A i FF dzialaja.
  4. Drobne: NWAR-15 klin plotno/len, 5-8 ludzi/dobe 'w nicosc ... inne' (E4/7), R274-2 (bieg startujacy poza pasmem - nie blad).
- ZASADY JEFFA (pamiec): stala zgoda na wgranie po udanym tescie (nie gdy gra); jego przyklady = przypadki testowe, dedukowac pelna logike; ekonomia z podazy
  i popytu, zamknieta, liczby z istniejacych modeli; mowic prosto po polsku; nie wgrywac, gdy cos psuje karawane/zapis Jeffa.

## DOPISEK 29.10 ok. 00:05 CET - WGRANIE 136 (135 + 288 + 289)
- 136a (90 dob, m113/wyniki/grupy-136a): zold karawan x0.499, stawki t1-3 2.21 / t4 3.71 / t5+ 5.60 / najemnicy 3.39, jedzenie w drodze 83% (glodne 13.4%),
  zakupy na droge 15 640 szt. za 231 060 zl; mediana zysku karawan 414 (135c 368), udzial wyplat 0.146 (135c 0.115), wynik handlu 33 wobec zoldu 85 zl/d
  (37% karawan >= zold), rozbite 2.27/d, miasta bez rudy 6.8; NIE: zelazo swiata -66.5/d (rosnie spadek - DO NAPRAWY), sprzedaz na stracie 2649/d.
  Karawana Jeffa w tescie przezyla (132 doby), zysk ze sprzedazy +3 896, netto -34 841 (w tym 42 doby na pelnym zoldzie).
- WGRANIE 136 o 23:59 (wgraj136.ps1), kopie .bak-2026-10-29-przed-wgranie136 i D:/Backup-Bannerlord/wgrane/2026-10-29-wgranie136-przed; zapis testowy na D:.
- Nastepne: zelazo swiata (-66.5/d) - rece przy zlozach i spadek Essos poza f5; obrot karawan (wynik 33 wobec zoldu 85); sprzedaz na stracie.

## DOPISEK 28.10 ok. 23:45 CET - 288 + 289 gotowe (noc/285-zold-srebro-ruda 52da829d, Armoury ba0fbc05), AUTOTEST 90 DOB 136a w toku
- 288 (c304ab3c + 7f8a6afe): CaravanWageHalf (MountedWage.CaravanHalf, linia podpowiedzi 'Merchant escort wage'); CaravanRoadFood (CaravanEscort.cs) - karawany
  wczesniej NIE jadly w drodze (DoesPartyConsumeFood false dla IsCaravan), teraz 1 szt./20 ludzi/dobe z jukow + zakup na droge przy wyjezdzie z kiesy karawany;
  w miescie nocleg i jedzenie dalej z sakiewek strazy. Grupa karawany288.
- 289 (52da829d, sam): EfficientCampaignerAlways - MountedWage.TotalPostfix AddFactor(SecondaryBonus) poza armia dla kwatermistrza z perkiem; CHANGELOG 289.
- AUTOTEST 136a (zadanie bqtzdmoi3, m113/seria-136a.out.txt; kopia,probyN 90 dob; zapis testowy odswiezony z zapisu Jeffa 23:20, stare zdjecie bazy usuniete).
  Grupy karawany288 handel287 handel286 handel284 karawany259 ruda282 nruda + reszta; porownac z grupy-135c; WGRANIE 136 gdy bez regresji.

## DOPISEK 28.10 ok. 23:30 CET - log Jeffa na 135 (sesja 22:18, doby 109025-109045): gospodarka zamknieta, wojna
- ARYTMETYKA OK; war241/karawany259/rownowaga274/pokoj264 bez NIE. Handel 287: sprzedaz karawan miastom 1524 szt./d, kupno stoi w 10 z 36 kategorii, w jukach
  39 724 szt.; mediana wyniku handlu karawany 10 zl/d wobec zoldu 169 zl/d (tylko 62 z 449 >= zold) - po 288 (-50%) dalej za malo: nastepny krok - czemu obrot
  na karawane taki maly przy marzy 78% (juki 26 dob sprzedazy, malo kursow?). Karawana Jeffa: netto -19 676 w 42 dobach (zold 20 295, wyplaty 1 819).
  Srebrnik Seagard: zysk dnia 875, wyplata 437. Braki surowcow 206 -> 156, ruda w jukach 2246 -> 1406.
- Wojna: unia 272 zrobiona (Dorzecze zniszczone, Robb krol Polnocy, gracz najemnik battania). A (Polnoc+Dorzecze) sila 21 056 / 113 partii / 39 twierdz
  (start 38) wobec B (korona) 13 695 / 67 / 18 (start 19); B/A 0.650 (pasmo 0.67-1.5, poza 43 doby, dzwignie dla B czynne); 28 dob: A 11 bitew 3 oblezenia,
  B 11 bitew 0 oblezen. Pokoj korona-Polnoc zablokowany do Krwawych Godow (wniosek Marbranda 109043).

## DOPISEK 28.10 ok. 23:25 CET - Jeff: Efficient Campaigner ma dzialac CALY CZAS, nie tylko w armii
- Jeff: "tak nie tylko w armii efektywny campaigner oznacza ze caly czas a nie ze w armii". DO ZROBIENIA po 288 (ten sam plik MountedWage.cs - nie rownolegle):
  w TotalPostfix GetTotalWage, gdy partia NIE w armii i HasPerk(Steward.EfficientCampaigner, true) (kwatermistrz) -> AddFactor(SecondaryBonus = -0.25,
  nazwa perka); w armii BK juz dodaje - nie dublowac. Dla wszystkich partii z perkiem (AI tez). Klucz MCM 'EfficientCampaignerAlways' TAK. Razem z 288 w tescie.
- Jeff pytal o slub z towarzyszka: BK BKMarriageModel dopuszcza towarzyszki (CompanionOf: -25% wartosci malzonka i posagu; warunki: klan, nie w armii/bitwie/
  niewoli, wiek, wiara i pokrewienstwo z doktryny religii).

## DOPISEK 28.10 ok. 23:15 CET - Jeff: zold karawan -50% dla calego swiata (kupiec 3, straznik 4, weteran 6) + kto placi jedzenie i nocleg - paczka 288 w toku
- Workflow wf_b1c73599-411 (skrypt workflows/scripts/paczka-288-wf_b1c73599-411.js, bez args; resumeFromRunId) na noc/285-zold-srebro-ruda (zb/n285, od 352ebab2).
  Klucz CaravanWageHalf (MountedWage.TotalPostfix), wszystkie odczyty zoldu karawan (259, 285/286, 284, 166, linie, grupy), jedzenie/nocleg karawan.
- Jeff gra (sesja od 22:18, 135). Pytal o Efficient Campaigner (-25% zoldu w armii): BK BKPartyWageModel stosuje przy Army != null i HasPerk(perk, true)
  (kwatermistrz); Jeff jest kwatermistrzem, poza armia brak obnizki - zgodnie z opisem perka.

## DOPISEK 28.10 ok. 21:45 CET - WGRANIE 135 (134 + 286 + 286b + 287); LINIA GLOWNA noc/285-zold-srebro-ruda (zb/n285)
- 135c (90 dob, wyniki m113/wyniki/grupy-135c) wobec 134b: sprzedaz karawan 167 -> 1062 szt./d, mediana zysku karawan 54 -> 368 (7 dob), bramka zamknieta
  w 9% kategorii (bylo 100%), rozbite 4.21 -> 2.98/d (ostatnie 30 dob 3.27), miasta bez rudy 22.8 -> 7.9, udzial wyplat 0.128 -> 0.115, zelazo -28 -> -36/d,
  srebro w jukach 1053 -> 1745, sprzedaz na stracie 348 -> 2964/d. H287-2 mediana wyniku handlu 18 wobec zoldu 159 zl/d (NIE). Karawana Jeffa w tescie rozbita
  po 57 dobach (bez zwolnien strazy; w 134b przezyla) - przypadek bandy czy straz za slaba przy kluczu tylko-dobor - DO SPRAWDZENIA.
- WGRANIE 135 o 21:42 (wgraj135.ps1), kopie .bak-2026-10-28-przed-wgranie135 i D:/Backup-Bannerlord/wgrane/2026-10-28-wgranie135-przed; zapis testowy na D:.
- Dalej: karawana gracza (czemu rozbita, straz pelna), wynik handlu wobec zoldu (H287-2), zelazo -36/d i spadek rak Essos poza f5, srebro, bramka surowcow
  masowych z popytu miast (jak 287), sprzedaz na stracie 2964/d.

## DOPISEK 28.10 ok. 19:15 CET - 287 GOTOWE (a2c137f6 + c3b5528d, Armoury 5cfd8c21), AUTOTEST 90 DOB 135c w toku
- Przyczyna handlu: NIE za male roznice cen (futra Westeros 7.36 / Essos 3.93 itd.), tylko bramka przeplywu 284 mierzona sprzedaza samych karawan -
  kupno stalo w 35-36 z 36 kategorii przez 84 doby, karawany tylko wyprzedawaly juki z wczytania. Klucz CaravanFlowByMarketDemand (zbyt = max(sprzedaz
  karawan, popyt targow miast)). CaravanGuardPlayerAfterNotice domyslnie NIE. KsIronFullOnlyOwnTown (przy pelnym rynku tna sie tylko kopalnie pelnych miast).
  R274-2 to nie blad 274 (bieg startuje poza pasmem, wielka bitwa 109049-109051). Grupa handel287. Bramka surowcow masowych (osobny kod) - nastepny kandydat.
- AUTOTEST 135c (zadanie biwz7ifr3, m113/seria-135c.out.txt; kopia,probyN 90 dob). Porownac z grupy-134b (udzial wyplat 0.128, rozbite 4.21, zelazo -28,
  bez rudy 22.8) i karawana Jeffa; WGRANIE 135 gdy lepsza i karawana zyje.

## DOPISEK 28.10 ok. 18:50 CET - Jeff "rob teraz" - paczka 287 w toku (workflow wf_50bb38ba-8e2)
- Skrypt workflows/scripts/paczka-287-wf_50bb38ba-8e2.js (bez args; resumeFromRunId), galaz noc/285-zold-srebro-ruda (zb/n285, od 409dc1db).
  Zakres: rozrzut cen regionalnych wobec kosztu drogi (marza kupcow), CaravanGuardPlayerAfterNotice domyslnie NIE, zelazo -74/d, R274-2. Potem test
  90 dob (testy-noc-204.ps1 -Runs kopia,probyN -ProbyDays 90, ok. 31 min) i porownanie z grupy-134b; WGRANIE 135 gdy lepsza od 134 i karawana Jeffa zyje.

## DOPISEK 28.10 ok. 18:40 CET - POROWNANIE 90 DOB: 134 (w grze) vs 135b (286b) - 135 NIE WGRANE; w grze dalej 134
- Te same 90 dob na kopii zapisu Jeffa (m113\wyniki\grupy-134b i grupy-135b):
  | miara                         | 134 (134b) | 135b (286b) |
  | rozbite karawany / d (90 dob)  | 4.21       | 2.37        |
  | udzial karawan z wyplata 259   | 0.128      | 0.081       |
  | zold swiata / handel (k zl/d)  | 84.7 / ?   | 89.8 / 55.6 |
  | srebro w jukach na koniec      | 1053       | 1501        |
  | miasta bez rudy ost. 10 dob    | 22.8       | 15.4        |
  | zelazo swiata / d              | -28        | -74         |
  | rece przy zelazie              | -20%       | -30%        |
  | sprzedaz na stracie / d        | 348        | 664         |
  | R274-2 dob w pasmie rownowagi  | 10% (!)    | 73%         |
- KARAWANA JEFFA ('Caravan of Isa Storm'): w 134b zyje, ale netto dla rodu -52 728 w 112 dobach (zold 54 945, wyplaty 5 217, zysk ze sprzedazy -4 599);
  w 135b po zwolnieniu 30 strazy (CaravanGuardPlayerAfterNotice) ROZBITA w dobie ok. 109062 (netto -16 950 do tego czasu). Dlatego 135 nie wgrane.
- WNIOSEK STRUKTURALNY: mediana zysku handlu karawany ok. 50-70 zl/d wobec zoldu ok. 470-490 zl/d (karawana Jeffa) - marza miedzy miastami za mala
  (reguly ceny sprawiedliwej 241/E8 sciskaja rozrzut cen); sama straz tego nie naprawi. Nastepny krok: zmierzyc rozrzut cen towarow miedzy regionami
  wobec kosztu przewozu (dni drogi x zold/ladunek) i dac kupcom marze z prawdziwej roznicy regionalnej (np. towary regionalne, dalekie trasy), a strazy
  gracza NIE zwalniac (CaravanGuardPlayerAfterNotice domyslnie NIE). Tez: R274-2 w 134 (rownowaga wojny kanonu poza pasmem 68 dob) - do sprawdzenia.
- Galaz noc/285-zold-srebro-ruda (409dc1db) = 134 + 286 + 286b - niewgrane; zachowane klucze dzialajace: CaravanGuardByRoadStrength (rozbite 4.2 -> 2.4/d).

## DOPISEK 28.10 ok. 18:05 CET - 135b (286b, 90 dob) wyniki mieszane; BAZA 134 na 90 dob (134b) w toku dla porownania
- 135b: rozbite 2.17/d (TAK), zold 89.8k wobec handlu +55.6k (NIE), udzial wyplat 0.135 -> 0.081 (NIE; 135a 0.023), sprzedaz na stracie 3704 -> 664/d (TAK),
  srebro 4180 -> 1501, ruda: miasta bez rudy ostatnie 10 dob 15.4, zelazo swiata -74/d, rece -30%; R274-2 73% dob w pasmie (prog 80%).
- 134b (zadanie bx4elury2, m113/seria-134b.out.txt): ta sama seria na zainstalowanej 134 (test/mfinal134a) - decyzja o WGRANIU 135 po porownaniu tych samych
  liczb (udzial wyplat, rozbite, zelazo, miasta bez rudy, srebro) w 90 dobach.

## DOPISEK 28.10 ok. 15:15 CET - 286b gotowe (1001b6cf + 409dc1db, Armoury ea5b76bf), AUTOTEST 90 DOB 135b w toku (bez bitew)
- Przyczyny: wyplaty 259 padly od 109055 przez fale wyprzedazy starego towaru ponizej wartosci ksiegowej 259 (1.07M zl za towar kosztujacy 2.34M, 33-36
  kategorii, nie tylko srebro); KsIronLocalGlut ciela rece przy braku rynku (ok. 1300 wpisow, ~20k rak). Klucze CaravanAgedGlutAtBookWorth (wyprzedaz tylko
  towarow > 30 dob sprzedazy swiata w jukach i nie ponizej wartosci 259 po myle) i KsIronLocalGlutBand (pelny = nadwyzka ponad 2T > tydzien wydobycia, przy
  braku rynku bez ciecia, odrost przy spadku ponizej celu). Spadek rak Essos 109084-109091 poza krokiem f5 - niewyjasniony.
- AUTOTEST 135b (zadanie be60hicf8, m113/seria-135b.out.txt; kopia,probyN 90 dob). Grupy handel286 handel285 handel284 karawany259 ruda282 nruda nwar +
  reszta; WGRANIE 135 przy H286-3 >= ok. 0.2 i bez zapasci rudy (RU282-1).

## DOPISEK 28.10 ok. 14:55 CET - Jeff "dzialaj tutaj" - poprawka 286b w toku (workflow wf_a79698e9-c4c) mimo 99% tygodnia
- Skrypt workflows/scripts/poprawka-286b-wf_a79698e9-c4c.js (bez args; resumeFromRunId), galaz noc/285-zold-srebro-ruda (zb/n285). Zakres: regresje 135a -
  wyplaty 259 (0.023) i wyprzedaz starego towaru, ciecie rak przy zelazie (KsIronLocalGlut). Potem test 90 dob (135b) i WGRANIE 135 przy H286-3 >= 0.25,
  RU282-1 i NRU-3 TAK.

## DOPISEK 28.10 ok. 14:45 CET - TEST 90 DOB 135a: 286 NIE WGRANE (regresje); W GRZE DALEJ 134 (13ee0fc5); TYDZIEN 99% - KONIEC PRAC NA TYM KONCIE
- 135a (noc/285-zold-srebro-ruda cd44422b, Armoury 17668068): bitwaN + probyN 90 dob (109025 -> 109114) kod 0, ERROR 0. Logi m113\logi\135a-probyN\
  Armoury-2026-10-28_14-12-51.log, wyniki m113\wyniki\grupy-135a\.
- DOBRZE: rozbite karawany 2.38/d (90 dob; ostatnie 30 dob 2.67, baza 2.23, 134a 3.83); zold swiata 144k -> 89.6k zl/d; stawka 9.54 -> 8.37 zl/czl, ludzi 17443 ->
  11680; zwalniani najdrozsi (12.39 zl wobec 8.45); gracz zwalniany po komunikacie (TAK); srebro w jukach 4180 -> 633; ruda w jukach 2246 -> 275; stosunek cen
  rudy 2.46 -> 2.07 (RU282-3 TAK); mediana zysku karawan > 0 (H284-1 TAK).
- REGRESJE (dlatego bez wgrania): (1) udzial karawan z wyplata 259: 0.150 -> 0.023 (!) - przy 134a bylo 0.246; wynik handlu swiata spadl do +52k zl/d (134a ok.
  +89k) - podejrzana CaravanAgedGlutSells (stary towar w przesycie schodzi porcjami po cenie dnia - sprzedaz ponizej kosztu H284-4 3894 -> 4930/d) i/lub
  kapital karawan; (2) KsIronLocalGlut: rece przy zelazie 113344 -> 73226 (-35%, NRU-3 NIE), zelazo swiata -17/d, miasta bez rudy na koniec 25 (ostatnie 10 dob
  srednio 30.9; RU282-1 NIE) - za mocne ciecie rak przy 'pelnym' miescie targowym; (3) NWAR-3/4a/15 (przezbrojone warsztaty 81 wobec 335 spodziewanego,
  kuznie srebra/aksamit 11 -> 14, klin plotno/len 1.47 -> 1.24).
- NASTEPNE KONTO: na galezi noc/285-zold-srebro-ruda (zb\n285) zostawic CaravanGuardByRoadStrength + CaravanGuardPlayerAfterNotice (dzialaja), a:
  (a) KsIronLocalGlut - ciac rece tylko o nadwyzke ponad 2T, z progiem i bez ciecia przy braku rynku/regionu; albo klucz NIE; (b) CaravanAgedGlutSells - tylko
  srebro/kategorie z udzialem w jukach > N dob sprzedazy i nie ponizej kosztu przy miastach z brakiem; sprawdzic, czemu wyplaty 259 padly (kiesa vs kapital
  po wyprzedazy); (c) test 90 dob ponownie (testy-noc-204.ps1 -ProbyDays 90, ok. 30 min probyN); WGRANIE 135 dopiero, gdy H286-3 >= 0.25 i RU282-1/NRU-3 TAK.
- Inne otwarte bez zmian: 283 scenariusze testowe B/C i J204-1 regex; NWAR-15; 5-8 ludzi/dobe 'w nicosc ... inne'.

## DOPISEK 28.10 ok. 14:00 CET - 286 GOTOWE (02916a7f + cd44422b, Armoury 17668068), AUTOTEST 90 DOB 135a w toku
- Zold strazy: stawka wedlug tieru jak wszyscy, ale karawany to same tiery 3-5 (9.68 zl/czl wobec 5.26 partie rodow, 4.56 garnizony), a 285 zwalnial najtanszych.
  286: CaravanGuardByRoadStrength (zagrozenie wzdluz calej drogi, dobor sily nie liczby, odchodza najdrozsi za jednostke sily), CaravanGuardPlayerAfterNotice
  (karawana gracza po komunikacie), CaravanAgedGlutSells (stary towar w przesycie schodzi porcjami po cenie dnia), KsIronLocalGlut (kopalnia przy pelnym
  miescie traci rece). Grupa handel286 (H286-1..8).
- AUTOTEST 135a (zadanie bax5id0mg, m113/seria-135a.out.txt; testy-noc-204.ps1 kopia,bitwaN,probyN -ProbyDays 90). Grupy handel286 handel285 handel284
  ruda282 nruda karawany259 ksiega280 + reszta; WGRANIE 135 (DLL test/mfinal135a, stara 13ee0fc5).

## DOPISEK 28.10 ok. 13:35 CET - Jeff: "napraw to, moze zold jest za wysoki ochrony? kolejna runda poprawek i test 90 dniowy" - paczka 286 w toku
- Workflow wf_5a92f7bf-0f4 (skrypt workflows/scripts/poprawka-286-wf_5a92f7bf-0f4.js, bez args; resumeFromRunId), na galezi noc/285-zold-srebro-ruda (zb/n285).
  Zakres: straz wedlug calej trasy (H285-3), stawka zoldu strazy karawan (tier najemnikow, zold tylko w drodze), srebro w jukach, rak przy zlozach mniej
  przy zalanych zrodlach (RU282-3), karawany gracza ta sama regula po komunikacie. Potem AUTOTEST 90 DOB: testy-noc-204.ps1 -Runs kopia,bitwaN,probyN
  -ProbyDays 90 (limit czasu liczy wrapper: 90 x 25 s + 10 min), grupy handel286/285/284 ruda282 + reszta, WGRANIE 135.

## DOPISEK 28.10 ok. 13:25 CET - WGRANIE 134 (133 + 285); LINIA GLOWNA = noc/285-zold-srebro-ruda (zb\n285); TYDZIEN 98% - stop nowych prac na tym koncie
- 134a (f73fcbd8, Armoury 13ee0fc5): 4 bitwy + probyN 30 dob kod 0, ERROR 0. Wyniki m113\wyniki\grupy-134a\. handel285: H285-0 TAK, H285-2 TAK (ludzi na karawane
  35.1 -> 25.4, potrzeba 22.3), H285-5 TAK; NIE: H285-1 zold z modelu 148k -> 104k zl/d wobec wyniku handlu ok. +89k (blisko, ale wciaz wiecej), H285-3 rozbite
  karawany 3.83/d (ostatnie 10 dob 4.00) wobec bazy 2.23/d - mniejsza straz = wiecej napadow, H285-4 srebro: wozy/kontrakty 376 szt., juki 4180 -> 2326 (dalej
  90 dob sprzedazy). handel284: H284-1 TAK (mediana +55..+74), H284-2 udzial wyplat 0.150 -> 0.246 (cel 0.25 - prawie), H284-5 braki 225 -> 135. ruda282: RU282-1
  TAK (9.3), RU282-6 TAK (juki 2236 -> 787), RU282-7 TAK (+1.3/d); RU282-3 stosunek cen 2.42 -> 3.59 NIE (zrodla taniej, bo zalane).
- WGRANIE 134 o 13:23 (wgraj134.ps1), kopie .bak-2026-10-28-przed-wgranie134 i D:\Backup-Bannerlord\wgrane\2026-10-28-wgranie134-przed; zapis testowy na D:.
  CHANGELOG 285 = WGRANE 134 (13630d15, wypchniete).
- NASTEPNE (drugie konto): (1) H285-3 - straz karawan: potrzeba z bandy w dniu drogi zaniza strefy po trasie; rozbitych 2.2 -> 3.8/d - doliczyc bandy na trasie
  (miedzy miastami), albo wieksza rezerwa przy wartosciowym ladunku; (2) H285-1 zold nadal > handel; (3) srebro dalej 90 dob w jukach - bramka/zamowienia
  srebra wolno; (4) RU282-3 rozrzut cen rudy (zrodla zalane - mniej rak przy zlozach po trendzie?); (5) 283 scenariusze testowe B/C, J204-1 regex;
  (6) NWAR-15, 5-8 ludzi/dobe 'w nicosc ... inne'. Karawany gracza tylko dobieraja straz (nie zwalniaja) - zold karawany Jeffa bez zmian.

## DOPISEK 28.10 ok. 12:45 CET - 285 GOTOWE (f73fcbd8, Armoury 13ee0fc5), AUTOTEST 134a w toku
- 285: 1fc91a09 + d101aea1 + recenzja f73fcbd8 (4 uwagi naprawione). CaravanGuards285.cs (CaravanGuardByRisk - straz wedlug najsilniejszej bandy w dniu drogi
  od miasta i celu oraz wartosci ladunku; nadwyzka odprawiana przy wyjezdzie BK raz na dobe droga 166), tabela zamowien 10 surowcow (srebro, bawelna, glina;
  TownMaterialOrderLuxuryInputs), CaravanGlutToShortTown, KsIronFullCountsTransit + KsIronStockTrend (f5 z droga i trendem). Grupa handel285 (H285-0..7).
- AUTOTEST 134a (zadanie b677ht74u, m113/seria-134a.out.txt; testy-noc-204.ps1 kopia,bitwaN,probyN 30). Grupy handel285 handel284 ruda282 nruda karawany259
  ksiega280 + reszta; potem WGRANIE 134 (DLL test/mfinal134a, stara 85a4c057).

## DOPISEK 28.10 ok. 11:50 CET - Jeff "Rob to!" (zold karawan, srebro, ceny rudy) - paczka 285 w toku (workflow wf_94a337d3-cfa)
- zb/n285, galaz noc/285-zold-srebro-ruda od d7b23443 (= WGRANE 133). Skrypt workflows/scripts/paczka-285-wf_94a337d3-cfa.js (bez args; resumeFromRunId).
  Zakres: straz i zold karawan wedlug niebezpieczenstwa drogi i wartosci ladunku (bez placenia za postoj), srebro/bawelna/glina w tabeli zamowien (zmienna
  liczba surowcow, zapis kontraktow wstecznie zgodny) + bramka uwalnia srebro, ruda: czemu rozrzut cen rosnie i zelazo +52/d. Potem: autotest 134a
  (testy-noc-204.ps1 kopia,bitwaN,probyN 30) -> WGRANIE 134. Logi dowodowe: m113/logi/133a-probyN/Armoury-2026-10-28_02-13-05.log.

## DOPISEK 28.10 ok. 02:30 CET - WGRANIE 133 (132 + 283); LINIA GLOWNA = noc/132-handel-bez-283 (zb\n132h)
- 133a (ea1df735, Armoury 85a4c057): 7 bitew + probyN 30 dob kod 0, ERROR 0, m110anim czysto. nekanie283: N283-1 A TAK (wykryc 13, lucznikow 42, strzaly 30,
  jezdzcow 7 / wolnej 6, szykow stoi 3), N283-4 TAK (FF 1/30), N283-5 TAK, J204-3 TAK (kontakt 6.6 s); NIE: N283-2 B (testowy pieszy - 'odwrot rannego', wykryc 0),
  N283-3 C (testowy wrecz - panika/odwrot, wykryc 0), J204-1 (wykryc 13, a prog liczy linie testu - do sprawdzenia regexu). W bitwach ogolnie: wykryc 555
  (pieszy strzelec 127, wrecz 83) - reakcje dzialaja tez dla nie-testowych. Ekonomia jak 132c (H284-1 TAK; H284-2/3/4, RU282-1/3/6/7 NIE poza celem).
- WGRANIE 133 o 02:29 (wgraj133.ps1); kopie .bak-2026-10-28-przed-wgranie133 i D:\Backup-Bannerlord\wgrane\2026-10-28-wgranie133-przed; zapis testowy na D:.
  CHANGELOG 283 = WGRANE 133 (d7b23443 na noc/132-handel-bez-283, wypchniete).
- noc/161-integracja (8983380b) jest NIEAKTUALNA (283 bez 283c) - kolejne paczki od noc/132-handel-bez-283.
- Do zrobienia: scenariusze testowe B/C 283 (nekajacy pieszy i wrecz ranny przed wykryciem), J204-1 regex, zold karawan vs marza (H284-2), srebro/bawelna/glina
  w tabeli zamowien, ruda RU282-3/7, NWAR-15, 5-8 ludzi/dobe 'w nicosc ... inne'.

## DOPISEK 28.10 ok. 02:05 CET - 283c gotowe (b469d60d), 133 zbudowane (noc/132-handel-bez-283 ea1df735, Armoury 85a4c057), AUTOTEST 133a w toku
- 283c: wspolna przyczyna - nekajacy testu jechal tempem szyku (gra ustawia predkosc z szyku); PostSpeed zdejmuje limit tylko w autotescie. Postoj: brama 'strona
  lamana' byla odwrocona, 110 m liczone do pieszych szykow wroga >= 8; R5 po 3 trafieniach; szyk obraca sie frontem. FF: przyjaciel przy celu w rozrzucie blokuje
  strzal, wrecz z przyjacielem w 6 m - bez strzalu. C: straz strzelcow - piechota do 110 m. Linia 'faza szkoda' - sprawdzic przed uznaniem TAK.
- AUTOTEST 133a (zadanie bs7mx6gt7, m113/seria-133a.out.txt; testy-noc-283.ps1 7 bitew + probyN 30). Gdy nekanie283 bez NIE i reszta jak 132c -> WGRANIE 133
  (DLL test/mfinal133a, stara 1a112ad9). Jesli 283 dalej NIE - 132 zostaje.

## DOPISEK 28.10 ok. 01:40 CET - Jeff: "dokoncz na tym koncie" - 283c w toku (workflow wf_9a99a574-e3d)
- Skrypt: workflows/scripts/poprawka-283c-wf_9a99a574-e3d.js (bez args; resumeFromRunId). Agent poprawia zb/n283 (od 8fe43105). Potem: 133 = noc/132-handel-bez-283
  + merge 283 (CHANGELOG/sprawdz_logi scal_sklad z bazy 9d807118: galezie 14b55ae5 i 283 - albo rozwiazac recznie), build, autotest testy-noc-283.ps1 7 bitew
  -CsAt cs-at283/CrashScribe-at-n283.dll + probyN 30 dob, grupy nekanie283 m204jazda m110anim handel284 + reszta, WGRANIE 133 gdy nekanie283 bez NIE.

## DOPISEK 28.10 ok. 01:20 CET - WGRANIE 132 (131 + 284, BEZ 283); START DLA NASTEPNEGO KONTA: 283c
- 132c (zb\n132h, noc/132-handel-bez-283 a2ef2e32): bitwaN 4 bitwy + probyN 30 dob kod 0, ERROR 0; handel284 jak 132b (H284-1 TAK, H284-6 TAK; H284-2/3/4/5 NIE
  poza celem), ruda282 RU282-1/3/7 jak przed, E223-8 jednorazowe, NWAR-15 jak 132a. Wyniki m113\wyniki\grupy-132c\.
- WGRANIE 132 o 01:15 (scratchpad\wgraj132.ps1): Armoury 1a112ad9; kopie .bak-2026-10-28-przed-wgranie132 i D:\Backup-Bannerlord\wgrane\2026-10-28-wgranie132-przed;
  zapis testowy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-28-132. CHANGELOG 14b55ae5 na noc/132-handel-bez-283 (wypchniete).
- LINIE: zainstalowane = noc/132-handel-bez-283 (131 + 284). noc/161-integracja (8983380b) = 131 + 283 + 284 (283 NIEGOTOWE). Nastepna paczka: naprawic 283
  na galezi noc/283-reakcja-na-nekanie-z-tylu (8fe43105), potem scalic 283 do noc/132-handel-bez-283 (albo przesunac n161 = n132h + 283) i test.
- 283c ZADANIE (z 132b, log m113\logi\132b-bitwaN\Armoury-2026-10-28_00-15-31.log): (1) postoj R4/R5 nie zatrzymuje szyku w marszu (stoi 0 przy marsz TAK);
  (2) FF lucznikow 5 przy 43 strzalach (> 5%) - linia strzalu przepuszcza swoich; (3) scen. B 'brak pieszego szyku wroga' (scenariusz CrashScribe-at-n283
  - wrog bez piechoty w tej bitwie?); (4) scen. C wykryc 2, piechoty 0, wynik panika/odwrot; (5) J204-3 brak kontaktu jezdzcow. Test: testy-noc-283.ps1
  (7 bitew) -CsAt scratchpad sesji 1e1c276a\cs-at283\CrashScribe-at-n283.dll; grupy nekanie283 m204jazda m110anim.
- Inne otwarte: zold karawan ok. 146k/d wobec marzy (H284-2 udzial wyplat 0.19 < 0.25), srebro/bawelna/glina w tabeli zamowien, NWAR-15 klin plotno/len,
  RU282-3 cena rudy bez rudy / zrodlo. Zostaje 5-8 ludzi/dobe 'w nicosc ... inne' (E4/7).

## DOPISEK 28.10 ok. 00:50 CET - 132b: 284 OK, 283 NIE; 132 = 131 + 284 (bez 283), autotest 132c w toku; 283c DO ZROBIENIA; TYDZIEN 97%
- 132b (n161 8983380b): kod 0, ERROR 0. handel284 probyN: H284-1 TAK (mediana zysku karawan ostatnie 10 dob 88, 69, 67, 76 ... > 0), H284-6 TAK (ARYTMETYKA OK,
  nic z niczego), H284-5 brak surowcow 238.6 -> 144.5 (ruda 196 -> 114, drewno 23 -> 13, skora 18 -> ...), tylko srebro wsad 4.8 -> 5.3; NIE poza celem:
  H284-2 udzial z wyplata 0.148 -> 0.188 (cel 0.25), H284-3 14 kategorii nie zeszlo do bramki, H284-4 sprzedaz na stracie 3746 -> 3451/d (cel 0.3x).
  ruda282 RU282-1 bez rudy 18 -> 6 na koniec (dni 21-30 srednio 14.2).
- 283 NIE GOTOWE: scen. A wykryc 12, lucznikow 143, strzaly 49, jezdzcow 2, ALE stoi 0 (postoj nie dziala); B 'brak pieszego szyku wroga' (scenariusz);
  C wykryc 2, piechoty 0, wynik panika/odwrot; N283-4 REGRESJA: FF lucznikow 5 przy 43 strzalach (> 5%); J204-3 brak kontaktu. Logi: m113\logi\132b-bitwaN\
  Armoury-2026-10-28_00-15-31.log, wyniki m113\wyniki\grupy-132b\. 283 zostaje na galezi noc/283-reakcja-na-nekanie-z-tylu (8fe43105) i w n161 (8983380b) - NIE wgrywac.
- 132 = WGRANE 131 + 284: worktree zb\n132h, galaz noc/132-handel-bez-283 (a2ef2e32; scalenie 284 od 23cc393c bez konfliktow), build Armoury 1a112ad9.
  AUTOTEST 132c (zadanie biqckgjkn, m113\seria-132c.out.txt; testy-noc-204.ps1 kopia,bitwaN,probyN 30 dob). Gdy grupy (handel284 jak 132b, reszta bez nowych NIE)
  -> WGRANIE 132 (DLL test\mfinal132c, z kopia 131 591d0ecc; pliki danych bez zmian), statusy CHANGELOG 284 = WGRANE 132 na galezi n132h.
- DALEJ (drugie konto, gdy limit): 283c - postoj armii (R4/R5 nie zatrzymuje szyku w marszu), FF lucznikow (linia strzalu przepuszcza swoich), scenariusz B bez
  pieszego szyku wroga, C bez oddzialu piechoty; potem nowa linia glowna: n161 ma 283+284, n132h tylko 284 - kolejne paczki od n161 po naprawie 283.
  Tez: H284-2/3/4 (zold karawan 146k/d wobec marzy), srebro/bawelna/glina w tabeli zamowien, NWAR-15 klin plotno/len.

## DOPISEK 28.10 ok. 00:40 CET - poprawki 283b/284b scalone (n161 8983380b, Armoury 2849ca38), AUTOTEST 132b w toku
- 283b (8fe43105): nekajacy z testu nie dochodzil za wroga - ograniczenie predkosci marszu (MarchToContact.PostSpeed) obejmowalo go jako czlonka szyku; obejscie
  przez skrzydlo (TestRoute), postoj przy celu, rozkaz tylko przy zmianie punktu; nekajacy testu nie jest 'Group'. Logika reakcji dzialala dla innych (R1 24,
  R2 19, jezdzcow 16). DLL CrashScribe-at-n283 bez zmian.
- 284b (4161b3e3): (1) sprzedaz gry CaravansCampaignBehavior.SellGoods (sciezka 'inne') ignorowala koszt: 30 dob -599k; teraz straznik 'nie ponizej kosztu' tez
  tam; (2) towar bez wpisu kupna BK dostawal date 7 dob wstecz przy wczytaniu i po 30 dobach caly naraz szedl na wyprzedaz (dzien 109049, ok. 300 szt./d) -
  teraz towar po 30 dobach na strate tylko bez rynku z zyskiem (AgeMarket). CaravanSizeByTrade z powrotem NIE (865cfeb9 - przyczyna strat gdzie indziej).
  Spodziewane: H284-1 TAK; H284-4 pewnie dalej NIE (sprzedaz z braku gotowki i psujace sie jedzenie; zold ok. 146k/d wobec marzy - nieruszone).
- AUTOTEST 132b (zadanie bui9nrk0s, m113\seria-132b.out.txt; testy-noc-283.ps1, 7 bitew + probyN 30 dob). Potem grupy jak 132a; WGRANIE 132 gdy nekanie283
  i handel284 (poza H284-4/H284-3 jesli wyjasnione) bez NIE.

## DOPISEK 28.10 ok. 00:00 CET - 132a: stabilnie, ale 283 i 284 NIE DZIALAJA jak trzeba - 132 NIE WGRANE; poprawki 283b/284b w toku; TYDZIEN 96%
- 132a (n161 e714ccca, Armoury 6b2ef43e): bitwaN 7 bitew + probyN 30 dob kod 0, zapisy Jeffa nietkniete, ERROR 0. Grupy: m113\wyniki\grupy-132a\.
  UWAGA: w logi\132a-probyN lezy tez stary log z 25.10 - brac Armoury-2026-10-27_23-38-07.log; bitwaN: Armoury-2026-10-27_23-24-37.log.
- 283 (nekanie283): N283-1 scen. A (konny za lucznikami): wykryc 2, lucznikow na cel 11, strzaly 38, ALE jezdzcow 0 (wolnej 6), stoi 0 (marsz TAK);
  N283-2 scen. B (pieszy): wykryc 0; N283-3 scen. C (wrecz): wykryc 0, R1 219. Do diagnozy: wykrywanie pieszego i wrecz, jazda nie wyslana, postoj nie dziala
  (moze tez sam scenariusz testowy CrashScribe-at-n283 - sprawdzic linie 'Nekanie (283) TEST').
- 284 (handel284, probyN): H284-1 mediana zysku karawan ostatnie 10 dob [6,11,7,7,-7,-28,-58,-88,-185,-272] - SPADA; H284-2 udzial z wyplata 0.172 -> 0.177 (cel 0.25);
  H284-3 8 kategorii nie zeszlo do bramki (whale_oil, planks ...); H284-4 sprzedaz T1 na stracie 3550/d -> 44783/d (ROSNIE 12x - prawdopodobnie CaravanSizeByTrade
  albo bramka/kapital zmuszaja do wyprzedazy). Ruda RU282-1 18 -> 8 bez rudy (cel <= 10 w dobach 21-30: 12.9). NWAR-15 plotno/len 1.59 -> 1.14.
- Poprawki: workflow wf_58c46e57-213 (skrypt workflows\scripts\poprawki-283b-284b-wf_58c46e57-213.js, bez args; resumeFromRunId) (283b w zb\n283, 284b w zb\n284) - diagnoza z logow 132a + naprawa + build + commit; potem ponowne scalenie do n161 (scal_sklad),
  autotest 132b (testy-noc-283.ps1, -CsAt cs-at283\CrashScribe-at-n283.dll), WGRANIE 132 tylko gdy nekanie283 i handel284 bez NIE.
- W grze dalej 131 (Armoury 591d0ecc). Przy 99% tygodnia - stop i drugie konto (wszystko tu + journal.jsonl runow).

## DOPISEK 27.10 ok. 23:25 CET - 283 + 284 SCALONE (n161 e714ccca, Armoury 6b2ef43e), AUTOTEST 132a w toku
- 284 (zb\n284): 0a3596d2 + da10b580 + recenzja 93bac7ea (Armoury 7bd84054); 9 uwag recenzji naprawione. Ksiega kosztu karawan w zapisie (arm_cg284), sprzedaz
  bez zoldu juz wydanego, BK i surowce nie ponizej kosztu, zakupy BK wedlug sprzedazy (bramka kazdego towaru, arm_cs284), kapital obrotowy, sprawdzenie zysku,
  bramka 282 i rowne pokrycie dla 7 surowcow tabeli, wsad warsztatow towarowych (CommodityInputs284.cs). Wnioski: marza handlu swiata ok. 89k/d < zold karawan
  ok. 153k/d - dlatego przy scaleniu CaravanSizeByTrade domyslnie TAK (e714ccca; karawana bez zysku na zold nie dobiera strazy). Srebro, bawelna, glina poza
  tabela zamowien (na pozniej, jesli warsztaty dalej stoja).
- Scalenie: 283 (cbba16f2; CHANGELOG/sprawdz_logi scal_sklad z bazy 9d807118, galezie 23cc393c + 283), 284 (3222286f; sprawdz_logi scal_sklad z bazy 6f30e9ac,
  HEAD + 284), gen_mcm, build 0 bledow. Self-testy N283, H284, J204, RU282, K259 TAK.
- AUTOTEST 132a (zadanie bouf9dys6, m113\seria-132a.out.txt): NOWY wrapper m113\testy-noc-283.ps1 (= 204 z 7 bitwami w bitwaN), -CsAt scratchpad sesji
  1e1c276a\cs-at283\CrashScribe-at-n283.dll, kopia,bitwaN,probyN 30 dob. Werdykt 'linie misji = liczba bitew' moze oczekiwac 4 - sprawdzic recznie.
  Grupy: nekanie283 m204jazda m110anim handel284 ruda282 karawany259 ksiega280 e223 + reszta. Potem bieg wczytania (H284-7 ksiega kosztu po wczytaniu) i WGRANIE 132.

## DOPISEK 27.10 ok. 23:05 CET - WGRANIE 131 (280+281+282); w toku 284; potem 283+284 -> 132
- Biegi wczytania (bieg-bitwa-204.ps1 -Days 4 -LoadSave, A 22:44 zapis, B 22:54 wczytanie): kod 0 oba; ksiega280 na logu B NIE 0 - K280-2 odstep W 24.02 h,
  K280-3 swiat po wczytaniu wobec zapisu +0.1 (przed 280: -82..-707 na wczytanie).
- WGRANIE 131 o 23:04 (scratchpad\wgraj131.ps1): Armoury 591d0ecc (n161 6f30e9ac); pliki danych bez zmian od 130; kopie .bak-2026-10-27-przed-wgranie131 i
  D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie131-przed. Zapis testowy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-27-131a.
  CHANGELOG 23cc393c (statusy 280/281/282 = WGRANE 131).
- Nastepne: po 284 (wf_627247df-0c5) scalic 283 (od 9d807118) i 284 (od 6f30e9ac) do n161 (scal_sklad: baza 6f30e9ac dla CHANGELOG/sprawdz_logi - 283 ma
  inna baze, uwaga), build, autotest 132a: bitwaN z -Battles 7 i -CsAt cs-at283\CrashScribe-at-n283.dll (scenariusze A/B/C 283) + probyN 30 dob;
  grupy nekanie283 m110anim m204jazda handel284 ruda282 + reszta; WGRANIE 132.

## DOPISEK 27.10 ok. 23:00 CET - 131a OK (czeka bieg wczytania), 283 GOTOWE (nie scalone), 284 w toku
- 131a (n161 6f30e9ac, Armoury 591d0ecc): bitwaN 13.2 min + probyN 30 dob kod 0, zapisy Jeffa nietkniete. Grupy (m113\wyniki\grupy-131a; bitwaN log
  Armoury-2026-10-27_22-14-49.log - w katalogu lezy tez stary log z 25.10, nie brac globem): ksiega280 / rody277 / m204jazda / nwar / war241 itd. NIE 0.
  NIE: E223-8 dwa zwolnienia po 15.6 i 13.4 doby (wpisy z zapisu starej DLL - jednorazowo; E223-5 12.1 TAK); RU282-1 (bez rudy 17.4 -> 13.4, cel <= 10;
  wszystkich bez rudy 18 -> 9), RU282-3 (cena bez rudy / zrodlo 2.32 -> 3.55 - GORZEJ, do 284), RU282-7 (zelazo +20.6/d, cel <= 10); zamowienia rudy 441,
  dowiezione 5736 szt. (bylo ~0), juki 2246 -> 1032. bitwaN: NRU-10 i P264-7 tylko krotki bieg (w probyN TAK), K259-7 T2 jak zawsze.
- W toku: biegi wczytania dla 280 (zadanie bq6xwg7us): A 4 doby z zapisem, B wczytuje zapis A 4 doby; logi m113\seria-131a-wczytA/B.out.txt -> ksiega280 K280-3/8/9.
  Jesli OK: WGRANIE 131 (DLL test\mfinal131a, md5 591d0ecc; pliki danych bez zmian od 130? sprawdzic git diff 9d807118..6f30e9ac ModuleData).
- 283 (zb\n283): 0da797db + 0c2d6081 + recenzja 4bfffb1b (Armoury 4afa102e); 10 uwag recenzji naprawione. Autotest wymaga CrashScribe-at-n283.dll
  (scratchpad sesji 1e1c276a\cs-at283\, md5 8922f2fc) i -Battles 7 (scenariusze A konny za lucznikami, B pieszy, C miecz) + grupa nekanie283 i m110anim.
- 284 w toku (wf_627247df-0c5). Potem: scalic 283 + 284 do n161, autotest 132a, WGRANIE 132.

## DOPISEK 27.10 ok. 22:20 CET - 280+281+282 SCALONE (n161 6f30e9ac, Armoury 591d0ecc), AUTOTEST 131a w toku; paczka 284 karawany i wszystkie surowce
- Scalenie: git merge 280 (bez konfliktow), 281 i 282 (CHANGELOG/sprawdz_logi zlozone scal_sklad z bazy 9d807118), gen_mcm, build Release 0 bledow.
  Self-testy K280, E223, RU282, R277 TAK. Zapis testowy odswiezony z zapisu Jeffa 22:09 (zdjecie bazy usuniete - wrapper robi nowe).
- AUTOTEST 131a (zadanie btw7kzz1c, m113\seria-131a.out.txt): kopia,bitwaN,probyN 30 dob, flagi jak 130a. PO NIM (dla 280 B2): drugi bieg wczytujacy zapis
  zostawiony przez probyN, 4 doby (autotest-bitwa-204.ps1 -Days 4 -LoadSave autotest-przekierowany-IronmanKkUpHftX7ZIq) -> grupa ksiega280 K280-3/8/9.
  Grupy: ksiega280 e223 ruda282 nruda rody277 + 15 z 129a. Potem WGRANIE 131 (280+281+282).
- Jeff 22:10: karawana nic nie zarabia - PRAWDA: 'Caravan of Isa Storm' 22 doby: sprzedaz 22857, zakupy 17991, zysk ze sprzedazy -5071, zold 10890,
  netto -11890, wartosc 17744 -> 14703 (< kapitalu 15000, wyplat 0). Swiat: 88 karawan rodow, wyplacilo 13, 63 z kiesa na podlodze. Warsztat Silversmith
  Seagard ok. 208/dobe (4588 w 22 doby, podatek 1310). Jeff: 'rob te plotna skory wszystkie surowce aby ta gospodarka zyla'.
- 284 (zb\n284, noc/284-karawany-surowce-przeplyw od 6f30e9ac; workflow wf_627247df-0c5): diagnoza strat karawan, zysk z prawdziwych roznic cen,
  bramka przeplywu 282 dla wszystkich towarow, kolejnosc zamowien i rowne pokrycie dla wszystkich surowcow warsztatow, marze warsztatow po 241; grupa handel284.
- 283 nadal w toku (wf_4a1d4aa3-b15). Log Jeffa calej sesji 20:56-22:09: D:\Backup-Bannerlord\logi-jeffa\2026-10-27-2210-po-grze\.
- Limit tygodnia 94% o 22:15.

## DOPISEK 27.10 ok. 22:40 CET - 282 GOTOWE (zbudowane, po recenzji, NIE scalone, NIE testowane); czeka 283
- 282 (zb\n282): 90699a3e + 0564e798 + recenzja 04810741 (Armoury 0fac1b16). Przyczyna: karawany trzymaja ok. 1/4 zelaza swiata w jukach (1666-2463),
  prog zrodla 174 sumuje sie ponad polki, zamowienia do najtanszego zrodla nawet 8 dob drogi w dowolnej kolejnosci. F1 CaravanBulk.cs (bramka przeplywu rudy
  karawan, zapis arm_cb282), F2 najbardziej potrzebujace miasto pierwsze + zrodlo wedlug zysku na dobe drogi (7 surowcow), F3 rowne pokrycie rudy, F4 rece
  przy kopalniach licza towar w drodze, F5 linia Drewno bez 'bez wyjasnienia' (to nie wyciek). Grupa ruda282 (RU282-0..9), nruda NRU-2 zmienione.
  Do zrobienia potem (decyzja Claude): ta sama bramka dla plotna (ok. 2500 w jukach przy braku 430) i skory - teraz tylko 'na sucho' w linii 282.

## DOPISEK 27.10 ok. 22:10 CET - 280 i 281 GOTOWE (zbudowane, po recenzji, NIE scalone, NIE testowane)
- 280 (zb\n280): 6e964978 + 673fb061 (Armoury 2a9f1216); recenzje 2 soczewki - 0 uwag. DayGate.cs (zapis arm_daygate, klucz DailyTickOncePerDay; P1 przestawia
  nastepny takt doby gry na kanoniczna pore po wczytaniu, prefiks Campaign.DailyTick pomija pierwszy takt z tym samym numerem), narodziny w ticku wsi
  (PopulationLaw.FloorPrefix/Finalizer, klucz BirthsAtVillageTick), udzialy band w zapisie (pd:), sw: v2 (polegli bez pochodzenia, reszta przy zapisie),
  reszta -5.4k ZAMROZONA i pokazywana osobno ('od 280'), B3 tekst, B4 'Bitwy: dzien N (doba ksiegi'. Grupa ksiega280. Test: bieg A 4 doby z zapisem,
  bieg B wczytanie zapisu z A 4 doby (K280-3 skok ~0), bitwy (K280-7). Zostaje 5-8 ludzi/dobe 'w nicosc ... inne' (E4/7) - poza 280.
- 281 (zb\n281): bcb49094 + 52416361 + recenzja 7a2b03c3 (Armoury 8654c20e). Release() na kazdej drodze, ReleaseLate przed wczesnymi return, przeglad godzinny
  (mysl co 6 h), Restore wpisow z zapisu przed wczesnymi return; grupa e223 + E223-8, E223-9.
- Czekaja: 282 (wf_7236362f-1af), 283 (wf_4a1d4aa3-b15). Potem scalenie 4 galezi, autotest 131a, WGRANIE 131.

## DOPISEK 27.10 ok. 21:50 CET - paczka 283 reakcja na nekanie z tylu (workflow wf_13ad8807-a09); TYDZIEN 93%
- Jeff gra na 130 (sesja od 20:56, log skopiowany: D:\Backup-Bannerlord\logi-jeffa\2026-10-27-2145-bitwa\). Uwagi do bitwy (21:40): strzelal z 3 m w plecy lucznikow
  i piechoty (konno, potem pieszo) - lucznicy szli dalej, konnica ruszyla tylko czesciowo, piechota nie reagowala. Log 204 (linia 7304): wykryc 52, epizody 2,
  jezdzcy 3, lucznicy na cel 32, ale 'cel zdjety - swoi przy strzelcu 19' i powroty 'swoi przy strzelcu 204' (straznik FF po bliskosci zamiast linii strzalu);
  204 liczy tylko KONNYCH strzelcow - pieszy gracz bez reakcji.
- 283 (zb\n283, noc/283-reakcja-na-nekanie-z-tylu od 9d807118): zatrzymanie/obrot szyku pod ostrzalem z tylu/boku, lucznicy strzelaja (FF = linia strzalu),
  cala konnica na konnego, oddzial piechoty na pieszego, wodz wstrzymuje marsz; autotest -Harasser rozszerzony o pieszego i pozycje tuz za lucznikami.
- 21:57 Jeff doprecyzowal: konnica w zwiazanej walce walczy dalej, chyba ze ostrzelany szyk wazniejszy albo jest w nim wodz party (wodz chroniony); atak gracza
  mieczem na lucznikow = lucznicy strzelaja, piechota biegnie, konnica (niezwiazana) szarzuje. Run wf_13ad8807-a09 ZATRZYMANY, nowy: wf_20af758b-7a4.
- 22:00 Jeff: "ja mam ci pisac kazde zachowanie ... nie umiesz sam wydedukowac" -> 283 od nowa jako PELNA MACIERZ reakcji (zagrozenie x typ oddzialu x stan),
  przyklady Jeffa = przypadki testowe; grupa nekanie283. Run wf_20af758b-7a4 ZATRZYMANY, nowy: wf_4a1d4aa3-b15.
- Wznowienie: scriptPath paczki-280-281-wf_d8c1bd65-fa1.js, resumeFromRunId wf_4a1d4aa3-b15 (args w journal.jsonl).
- W toku rownolegle: 280+281 (wf_d8c1bd65-fa1), 282 (wf_7236362f-1af), 283. Potem: scalenie wszystkich czterech do n161 (git merge + scal_sklad), autotest 131a
  (bitwaN z -Harasser + probyN 30 dob + wczytanie zapisu dla 280 B2), grupy ksiega280 e223 ruda282 m204jazda/nekanie283 + reszta, WGRANIE 131 gdy Jeff nie gra.
- LIMIT TYGODNIA 93% o 21:45 - przy 99% zatrzymac sie i przekazac drugiemu kontu (wszystkie runy wznawialne z journal.jsonl).

## DOPISEK 27.10 ok. 21:15 CET - paczka 282 ruda (workflow wf_7236362f-1af) obok 280/281
- Jeff pytal o inwestycje (odpowiedz z logu 12:57: kuznia ~123 d/dobe, tkalnia welny ~98, len ~68; tartak ~0, rzeznik -5, platnerz zbroi konskich -8 - zmiana branzy
  E3-D3 po 60 dobach strat), potem: "tak zob paczke, ale ta rude to my chyba juz ktoreys raz naprawiamy i mowiles ze naprawione".
- 282 (zb\n282, noc/282-ruda-przeplyw od 9d807118): DIAGNOZA czemu f5-ruda / 174 / 174b / 214 nie dzialaja w kampanii Jeffa (28 z 97 miast bez rudy, kontrakty rudy
  ~0, zapas w Norvos/Oldtown/Fairmarket/Lannisport), potem naprawa przyczyny; tez 'Drewno ... bez wyjasnienia -1562/dobe'. Grupa ruda282.
- Wznowienie: ten sam scriptPath co 280/281 (paczki-280-281-wf_d8c1bd65-fa1.js), resumeFromRunId wf_7236362f-1af, args z journal.jsonl runu.
- Potem: scalenie 280+281+282 do n161, autotest 131a, WGRANIE 131.

## DOPISEK 27.10 ok. 20:55 CET - Jeff "tak napraw wszystko": paczki 280 i 281 w toku (workflow wf_d8c1bd65-fa1)
- 280 (zb\n280, noc/280-ksiega-doba-po-wczytaniu od n161 9d807118 = WGRANE 130): B1 narodziny wsi ksiegowane w ticku wsi (nie prognoza doby moda; niezaleznie od
  Measure40.On), reszta niewyjasnionych skokow, decyzja o -5.4k w zapisie Jeffa; B2 centralny straznik doby moda (raz na numer doby, nadrobienie pominietej, zapis);
  B3 tekst linii 'Ludzie (ksiega)'; B4 dzien w linii 'Bitwy'; B5 licznik poleglych bez pochodzenia; grupa sprawdz_logi ksiega280. Projekt + wykonanie + 2 recenzje.
- 281 (zb\n281, noc/281-eskorta-postoj-12-dob od 9d807118): LordEscort.ThinkPostfix - zwolnienie po 12 dobach na kazdej drodze (wczesne return 410-414), bez
  zostawionych NoAtk/kolejek przy glodzie; E223-5 ma przejsc.
- Wznowienie: Workflow scriptPath ~\.claude\projects\C--Program-Files--x86--...\1e1c276a-...\workflows\scripts\paczki-280-281-wf_d8c1bd65-fa1.js,
  resumeFromRunId wf_d8c1bd65-fa1, te same args (w journal.jsonl runu). Potem: scalenie obu do n161 (git merge + scal_sklad dla CHANGELOG i sprawdz_logi z bazy
  9d807118), build, autotest 131a (bitwaN + probyN 30 dob + bieg z wczytaniem zapisu - B2), grupy ksiega280 + e223 + reszta, WGRANIE 131 gdy Jeff nie gra.
- Limit: tydzien 90% o 20:50 (do 99% wolno, potem przekazanie na drugie konto).

## DOPISEK 27.10 ok. 20:45 CET - WGRANIE 130 (277 rody kanonu Zachodu); nic w toku, czeka odpowiedz Jeffa o 280
- Autotest 130a (20:10-20:40): bitwaN + probyN 30 dob (109002 -> 109031) kod 0, zapisy Jeffa nietkniete, bledy CrashScribe 27/25 jak 128a. Grupy (m113\wyniki\grupy-130a):
  rody277 13 progow NIE 0 w obu biegach; przeniesienie na kopii zapisu Jeffa: panow 11 (Spicer rycerski do Krwawych Godow), rycerzy z nazwa kanonu 4 (Greenfield,
  Hetherspoon, Lorch, Ruttiger), nowe druzyny (limit 190). NIE tylko: K259-7 T2 w bitwaN (jak 129a) i E223-5 "najdluzej w stanie" 27.1 dob (Ookpik Wolfcaller,
  Wolni Ludzie) - starsze 223: LordEscort.ThinkPostfix wychodzi (Unready/CapTrip, DoNotChangeBehavior, IsStarving - linie 410-414) PRZED zwolnieniem po 12 dobach
  (:469-477), stan wisi; 277 LordEscort.cs nie rusza. Do poprawki osobno (razem z 280?).
- WGRANIE 130 o 20:42 (scratchpad\wgraj130.ps1): Armoury.dll 88d10a9a (n161 12eb9216) + ModuleData\arm_houses_canon.tsv 5e1d8c20; kopie .bak-2026-10-27-przed-wgranie130
  (DLL i tsv) i D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie130-przed. Zapis testowy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-27-130a.
  CHANGELOG 1c0059d2 + 9d807118 (wypchniete). Przy pierwszym wczytaniu kampanii Jeffa: przeniesienie raz (nazwy i herby NIEODWRACALNE w Ironmanie).
- Do sprawdzenia w logu Jeffa po jego grze: linie "Rody kanonu (277)" (przeniesienie raz, panowie 11, druzyny), stosunek B/A w linii 274.
- Czeka na Jeffa: paczka 280 (B1-B4 z dopisku 19:55) + E223-5.

## DOPISEK 27.10 ok. 20:15 CET - 277 gotowe i scalone (n161 12eb9216, build 88d10a9a), AUTOTEST 130a w toku
- 277 (zb\n277): 6ff77201 + fbd0f287 + recenzja 653bdca6 (Armoury 711c6712). CanonHouses277.cs/.Pay.cs/.Test.cs; 12 rodow Zachodu i ziem koronnych z rycerzy BK korony
  panami (nazwa i herb z arm_houses_canon.tsv, pelne parostwo BK, tier 3, druzyna glowy SpawnLordParty); zold od pana lennego regula W5 (Lannister / Joffrey);
  raz na zapis (arm_rody277); Spicer rycerski do Krwawych Godow; klucze "Canon Houses of the West" / "- Book Names" / "- Liege Pays the Host"; grupa rody277.
  Szacunek B/A 0.50 -> 0.54-0.58 (reszte trzyma 274). NIEODWRACALNE w Ironmanie: nazwy i herby rodow zostaja w zapisie (klucz wylacza tylko zold i nowe druzyny).
  Dane: arm_houses_canon.tsv (status lordly 6 rodow ziem koronnych) - przy wgraniu 130 RAZEM z DLL.
- Scalenie: git merge bez konfliktow (12eb9216), build Release kod 0, Armoury 88d10a9a.
- AUTOTEST 130a (od ok. 20:10, zadanie bn33kv8y4, m113\seria-130a.out.txt): kopia,bitwaN,probyN -ProbyDays 30, flagi jak 129a + armoury-rody277.flag.
  tsv 277 wgrany do gry NA CZAS TESTU, kopia 129: D:\Backup-Bannerlord\wgrane\2026-10-27-test130a-tsv (komenda sama przywraca po tescie - SPRAWDZIC md5).
  Potem grupy (rody277 + 15 z 129a), zapis testowy na D:, WGRANIE 130 = DLL + arm_houses_canon.tsv, gdy Jeff nie gra.
- Jeff pytany 19:55 o paczke 280 (B1 narodziny przy wczytaniu, B2 podwojna/brakujaca doba moda po wczytaniu, B3 tekst) - czeka na odpowiedz.

## DOPISEK 27.10 ok. 19:55 CET - straty ludnosci: wynik sprawdzenia (wf_ac2622a4-2b8, 2 sledztwa + 2 recenzje)
- Straty SA odejmowane trwale: rekrut schodzi z DOM przy werbunku (FromVillage hearth -1/k, FromTown stan E4/4; PeopleBook.Origin.cs:1187-1228), polegly nie wraca
  (DieShares Fallen, Origin.cs:811-827), wracaja tylko zywi z pochodzeniem. Narodziny = wlasny model PopulationLaw.GrowthOf (0.5%/rok + dobrobyt - wojna - glod),
  NIE zalezy od poleglych (zbieznosc +86.6k ~ +86.9k w oknie 108768-109002 to zbieg; wojna obniza g z 0.60% do 0.41%/rok). Od siewu 108648 zywych +30k.
- Skutki w grze: mniej ludzi = mniejszy plon wsi, renta/oplaty wsi, rynek miasta, milicja. NIE: dobrobyt miast (ksiega 10.43 mln vs z dobrobytu 14.77 mln,
  ZC6 wylaczone - decyzja projektu), miejsca u notabli (LevyByPeople nie). Hamulec wolnych rak dziala w 1 regionie z 227 (ibbenese).
- BLEDY (do paczki - Jeff pytany o zgode 19:55):
  B1 narodziny wsi ksiegowane raz na dobe moda jako prognoza pelnej doby (PopulationLaw.cs:970-988), a hearth rosnie w ciaglym ticku wsi (zapisywanym); doba moda
     dostaje nowa pore po wczytaniu (Campaign.CreateCampaignEvents initialWait) -> kazde wczytanie zmienia 'swiat' o (W-1 doby) x narodziny (-707..+112).
     Poprawka: NoteBorn w ticku wsi (wlasna latka, NIE zalezna od Measure40.On = LogEnabled && PeopleMeasureEnabled), w GrowthDaily tylko log. Czesc skokow
     (-150..-270 przy 108984 i 108987, dryf w sesjach wojennych -6..-15/dobe) niewyjasniona.
  B2 po wczytaniu cala doba moda (OnDailyTick) moze isc 2x tego samego dnia gry (W<1) albo przepasc (W>1); brak straznika (ArmouryBehavior.cs:2733, Campaign.cs:1002).
     W logach: podwojne 108829, 108836, brak 108986. Czesc modulow ma wlasny straznik numeru doby (ArmsScrap, KsHunger, PlagueWatch, MapFields, InfluenceWatch, BkrotChains).
  B3 tekst linii 'Ludzie (ksiega)': 'werbunek jeszcze nie zdejmuje z DOM' (PeopleBook.cs:1177, komentarze :127, ClanBudget.cs:1634) - nieaktualny.
  B4 kosmetyka: 'Bitwy: dzien N' = dzien ksiegi + 1.
- Dane: scratchpad\ksiega_ludzi.py/.tsv, krainy.py; pelny wynik: tasks\w0tuyln4t.output (sesja 1e1c276a).

## DOPISEK 27.10 ok. 19:25 CET - WGRANIE 129 (241+259+264+268+272+274); w toku 277 i sprawdzenie strat ludnosci
- Autotest 129a (18:50-19:15): bitwaN + probyN kod 0, zapisy Jeffa nietkniete, bledy CrashScribe 25 = jak 128a (cudze latki Harmony). 15 grup sprawdz_logi x 2 biegi
  (war241 karawany259 pokoj264 z268 krol272 rownowaga274 p202 p203 m204jazda odbicie222 e223 m109muz m165mapa m6a nwar) - NIE 0, jedyny NIE: K259-7 T2 w bitwaN (bieg
  bitew za krotki; w probyN T2 TAK). Wyniki: m113\wyniki\grupy-129a\. NWAR-10 tym razem TAK.
- WGRANIE 129 o 19:19: Armoury.dll a25f28c0 (n161 2a96d15b), skrypt scratchpad\wgraj129.ps1; kopie Armoury.dll.bak-2026-10-27-przed-wgranie129 i
  D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie129-przed. Pliki danych bez zmian od 128. Zapis testowy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum\2026-10-27-129a.
  CHANGELOG statusy 6 paczek = WGRANE 129 (086fc0c9, wypchniete).
- Jeff 19:05: "sprawdz w logu ile osob pochlonela wojna, ile ludnosci stracily wioski i miasta i czy straty sa odejmowane od liczby ludnosci".
  Dane (scratchpad\ksiega_ludzi.py -> ksiega_ludzi.tsv, krainy.py): dzien 108768 -> 109002: polegli +86 876 (od siewu 108648: 108 478), cywile 1 176, wsie -102 tys.,
  miasta -42 tys. (razem -0.28%), SLUZBA +104 tys., uchodzcy 3.6 -> 28 tys., narodziny netto +86.6 tys.; reszta rownania krain -1582 -> -5365 (skoki przy wczytaniu).
  Weryfikacja w kodzie: workflow wf_ac2622a4-2b8 (rachunek strat + dryf rownania, z recenzja).
- Nastepne: 277 (wf_f21d9616-2f0) -> scalenie do n161 -> autotest 130 -> WGRANIE 130.

## DOPISEK 27.10 ok. 19:20 CET - paczka 277 rody kanonu Zachodu/Ziem Koronnych "po ksiazce"; autotest 129a w toku
- Jeff 19:10: "Czemu lannisterowie maja tak malo rodow, w ksiazce tez tak malo mieli?" -> odpowiedz: w ksiazce Tywin ok. 20k + Jaime ok. 15k, dziesiatki rodow
  Zachodu; w ROT strona korony 50 rodow, 17 z ziemia, 19 twierdz (strona Robba 95 / 34 / 38). Pieniadze NIE sa przyczyna (KL 6.2 mln skarbiec). Kredyt W3 niepotrzebny.
  Jeff: "ksiazce" -> paczka 277 (zb\n277, noc/277-rody-kanonu-zachod od n161 2a96d15b, workflow wf_f21d9616-2f0): rozpoznanie rodow kanonu, potem siedziby/tier/drużyny
  wedlug ksiazek, bez zabierania stronie Robba, migracja zapisu Jeffa raz (flaga), klucz MCM, linia 'Rody kanonu (277)'.
- Autotest 129a (od 18:50, m113\seria-129a.out.txt; 6 paczek, build a25f28c0). Po nim: grupy (war241 karawany259 pokoj264 z268 krol272 rownowaga274 + stare),
  zapis testowy na D:, WGRANIE 129 (bez 277) gdy Jeff nie gra; 277 osobno potem (130).

## DOPISEK 27.10 ok. 18:50 CET - 274 gotowe; WSZYSTKIE 6 PACZEK SCALONE w n161 (2a96d15b, Armoury a25f28c0, NIE wgrane); nastepny krok AUTOTEST 129a
- 274 (zb\n274): 3b37dd98 + 22a7ca04 + recenzja 48710016 (cc91a05c). CanonWarClock.cs, CanonWarBalance(.Levers/.Restraint/.Test).cs; klucze Canon War Balance /
  Restraint / Muster; zapis arm_cwb274; koniec przy RedWeddingEvent >= 2 albo smierci Robba (jak 264). Przyczyna slabosci B: ROT - B ma 17 rodow z lennami i 19
  twierdz, A 34 i 38 (pelne B to ok. 0.78 A). Siedziby z "strona domowa" (odbicie wlasnej siedziby bez weta). Decyzja dla Jeffa: W3 (wiekszy kredyt 168 dla slabszego).
- Scalenie 274: konflikt Settings.cs (bloki 272 i 274 - oba), McmSettings z gen_mcm, CHANGELOG i sprawdz_logi scal_sklad z bazy 056ae5f4 i 6 galezi.
- AUTOTEST 129a do zrobienia (gra wylaczona o 18:49): wrapper m113\testy-noc-204.ps1 -Tree n161 -Dll mfinal129a -Runs kopia,bitwaN,probyN -Harasser 3
  -CsAt test\CrashScribe-at-n204.dll -ExtraFlags armoury-dzwiek-bitwy.flag,armoury-pokoj264.flag,armoury-zbroja268.flag,armoury-rownowaga274.flag,armoury-karawany.flag
  (+ odswiezyc zapis testowy z zapisu Jeffa, przeniesc stare zdjecie bazy m113\zapisy na D:; logi Jeffa z dnia na D: przed testem). Potem grupy war241 karawany259
  pokoj264 z268 krol272 rownowaga274 + stare; dlugi bieg swiata (241/259/274) osobno.

## DOPISEK 27.10 ok. 20:00 CET - 268, 272 gotowe; 241+259+264+268+272 SCALONE w n161 (cf193290, build 2a226448, NIE wgrane); 274 w toku
- 268 (zb\n268): 9fc4889c + c087422c + recenzja 03d5313c. p9 liczy B (udzial ludzi ze zbroja korpusu), ArmourOrders.cs (zamowienia zbroi/helmow pelne, cap 60),
  E3-Z1 bez limitu 1/dzien dla korpusu i helmu, klucze AiArmsCountsBodyArmour, AiArmourOrderFull; flaga armoury-zbroja268.flag, grupa z268.
- 272 (zb\n272): 25436c96 + 05eee9cc + recenzja 45048904. KingInTheNorth.cs (zapis arm_k272, klucz CanonNorthRiverlandsUnion): unia po ROT RiverlandsDeclareWarEvent
  stan 2 albo 2 dni od daty z planu Fabuli, Robb zyje, przed Krwawymi Godami; rody przez ApplyByJoinToKingdom / najemnicy ApplyByJoinFactionAsMercenary (kariera BK
  przepisana); skarbiec: splata Banku, reszta do Polnocy; DestroyKingdomAction Dorzecza; okno "The King in the North and the Trident"; ExecuteRobb postfiks
  (Freyowie, Mootonowie, Coxowie, Brackenowie do korony). Grupa krol272.
- SCALANIE n161: 241 (ff), 259 ab0f28b2, 264 2deb7796 (konflikt konstruktora ArmouryBehavior - suma Reset()), 268 a00425ea, 272 96c073f3; CHANGELOG i sprawdz_logi
  zlozone scal_sklad z bazy 056ae5f4 (cf193290). Wszystkie grupy (war241 karawany259 pokoj264 z268 krol272 + stare) daja tabele; NWAR-10 NIE starsze (tez na 056ae5f4).
- Po 274: scalic 274 (git merge + scal_sklad z 6 galeziami), build, autotest: bitwaN (flagi pokoj264, zbroja268) + dlugi bieg probyN (120 dob dla 241/259/274, flagi
  krol272/odbicie) -> grupy -> wgranie 129 po zamknieciu gry.
- Na pozniej (z audytu): daniny z miast od biezacego zysku, pamiec cen karawan w zapisie, karawany z zysku, sprzedaz na przyszlosc, licznik podatku 2 wsi BK (NaN),
  ubytek przy budowach, log trafien, NWAR-10 (pola KS w F4 6.74x).

## DOPISEK 27.10 ok. 18:40 CET - 241 i 259 GOTOWE (zbudowane po recenzji, nie wgrane)
- 241 (zb\n241): 6cf804cd + 56485d08 + recenzja 237e6c60 (Armoury 0a0b45e8). MarketSales.cs (sprzedaz miast 28 dni, zapis arm_mktsales); kupiec placi cene z
  historii sprzedazy albo pulap ludu; rzemieslnicy 35% (TownFolkCraftWageShare); WorkshopTaxOnPayout (podatek BK od wyplaty, 1x stawka); ClampBack (BK placi ponad
  kase miasta -> cofniete z kapitalu warsztatu). sprawdz_logi --grupa war241. Test: 120 dob swiata (liczby warsztatow, ceny bizuterii/aksamitu/narzedzi, kasy miast).
- 259 (zb\n259): 7da9cfd1 + 361f8451 + recenzja 6366da19 (Armoury f0381e70). Rozliczenie karawan rodow raz na dobe w prefiksie AddIncomeFromParty; kapital =
  min(model, dane przez E3/7) (arm_259_kapital); BK wplata i E3/9 dla nich wylaczone; linia "Majatek gracza (karawany i warsztaty)"; sprawdz_logi --grupa karawany259.
- W toku: 268+272 (wf_755f2f7e-043), 274 (wf_1f03e327-b32). Potem scalanie (scal_sklad dla sprawdz_logi i CHANGELOG), autotest dlugi.

## DOPISEK 27.10 ok. 17:35 CET - 264 GOTOWE (zbudowane po recenzji, nie wgrane)
- 264 (zb\n264, noc/264-pokoj-wojny-kanonu): 114ba627 + 34feb90a, recenzja 351fac2a (Armoury be852bc6). CanonPeaceLock.cs (klucz CanonWarNoPeaceUntilRedWedding):
  pary battania-vlandia i riverlands-vlandia; blokada do RedWeddingEvent ROT stan 2 (zakonczone) albo smierci Robba (lord_5_1) - ZNACZNIK DLA 274; Diplomacy
  ApplyPeace / Kingdom.AddDecision / MakePeaceKingdomDecision.IsAllowed; gracz-wladca: wybor tylko gdy Diplomacy daje Decline. PeaceHold (M6a-3) czeka do 72 h na ROT.
  Data wojny z rekordu gry. CanonWarWatch.cs: 4 linie wladcow (Robb, Joffrey, Tully, rod Tywina) + 3 linie krolestw dziennie.
- Autotest: flaga CrashScribe\armoury-pokoj264.flag, sprawdz_logi --grupa pokoj264 (P264-7 T1-T7 TAK, P264-0 4 haki, P264-5 linie co dobe); potem m6a i m104wojny.
- W toku: 241+259 (wf_d5f20a7a-22b), 268+272 (wf_755f2f7e-043), 274 (wf_1f03e327-b32).

## DOPISEK 27.10 ok. 17:00 CET - paczka 274 rownowaga wojny kanonu do Krwawych Godow (Jeff)
- Jeff 17:00: "lannisterowie i krolewska przystan byli minimum porownywalnie tak samo silni jak polnoc, chodzi o to zeby zadna ze stron nie uzyskala wyraznej
  przewagi az do krwawych godow aby walczyli i aby sily byly porownywalne dorzecze + polnoc i lannisterowie i krolewska przystan".
- Stan z audytu: strona Robba ok. 17.7 tys. w polu (Polnoc 11.0k + Dorzecze 6.7k) wobec KL 7.9k.
- 274 (zb\n274, noc/274-rownowaga-wojny-kanonu, workflow wf_1f03e327-b32): najpierw przyczyny slabosci KL, potem dzwignie tylko z zamknietej ekonomii
  (kredyt Banku/Iron Bank, rekruci z prawdziwej ludnosci, najemnicy za kredyt, pelne druzyny, armie), powsciagliwosc silniejszego (bez oblezen gniazd rodow przed
  Krwawymi Godami), obie strony walcza; wylacznik po znaczniku Krwawych Godow (wspolny z 264); zgodnosc z 272 (Krol Polnocy). Test 120 dob.
- W toku: 264 (wf_09849f4a-635), 241+259 (wf_d5f20a7a-22b), 268+272 (wf_755f2f7e-043), 274 (wf_1f03e327-b32). Limit 5h 24%, tydzien 80% (17:00).

## DOPISEK 27.10 ok. 16:45 CET - po odnowieniu limitu: 264 wznowione, 241+259 i 268+272 w toku
- Workflowy (skrypt workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js): 264 wf_09849f4a-635 (wznowiony, args scratchpad\args-264.json),
  241+259 wf_d5f20a7a-22b, 268+272 wf_755f2f7e-043. Przy przerwaniu: wznowic resumeFromRunId z TYMI SAMYMI args (z pliku runu w workflows\wf_*.json, pole args).
- Uwaga: skrypt tnie liste potwierdzonych uwag dla poprawiajacego do 20000 znakow - po wyniku sprawdzic w journal.jsonl, czy wszystkie potwierdzone uwagi poprawione.
- Limit 5h 0% o 16:42, tydzien 74%.

## DOPISEK 27.10 ok. 15:35 CET - PAUZA do odnowienia limitu (Jeff: "zrob pauze i kontynuuj po resecie limitu")
- 264 zatrzymane (run wf_09849f4a-635: projekt gotowy w pamieci podrecznej, wykonanie przerwane - drzewo zb\n264 czyste). Wznowienie po 16:40:
  Workflow(scriptPath=workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js, resumeFromRunId=wf_09849f4a-635, args = scratchpad 1e1c276a...\scratchpad\args-264.json).
- Potem uruchomic 241, 259, 268, 272 (opis w dopisku 15:30). Budzik w tle na 16:42.

## DOPISEK 27.10 ok. 15:30 CET - AUDYT GOTOWY (docs/AUDYT-2026-10-27.md); Jeff: "tak zacznij od tych pieciu" + Krol Polnocy
- Jeff 27.10 ok. 15:25: "tak zacznij od tych pieciu, a tak potem jak dolaczaja do roberta to staja sie jednym krolestwem King in the North".
- Paczki (galezie od noc/161-integracja 056ae5f4 = WGRANE 128, drzewa scratchpad fffc991e...\zb\nNNN):
  241 warsztaty-cena-rynkowa-podatek: "cena sprawiedliwa" -> kupiec miejski placi tyle, za ile odsprzeda (srednia sprzedazy 28 dni albo cena graniczna ludu 1.5x
      wartosci), producent dostaje cene rynkowa; podatek BK warsztatow liczony od dziennej wyplaty (raz), cena warsztatu od 1x stawki (audyt pkt 1.1 i 1.2). Test 120 dob.
  259 karawany-gracza-rozliczenie: karawany gracza (i lordow), ktorym zold placi rod: codziennie nadwyzka ponad kapital z modelu gry (10 000 / 17 500 elitarna /
      30 000 morska, bez rezerwy 30 dni zoldu) do wlasciciela przelewem, bez podwojnej wyplaty z gry; linia logu + komunikat "Twoje karawany i warsztaty" (audyt pkt 4).
  264 pokoj-wojny-kanonu: pokoj z decyzji krolestwa zablokowany w wojnie kanonu do Krwawych Godow, sprawdzenie jencow M6a-3 po ROT, data wojny w logu, linia planow
      wladcow (Robb) - URUCHOMIONE 15:26 (workflow wf_09849f4a-635).
  268 gotowosc-zbroja: ocena gotowosci p9 liczy zbroje na tulow (prog 60% jak bron), zamowienia zbroi bez limitu 1/dzien, braki liczone w miescie (audyt pkt 2).
  272 krol-polnocy: Polnoc i Dorzecze jednym krolestwem, gdy Dorzecze dolacza do Robba (kanon: Robb ogloszony Krolem Polnocy i Tridentu w Riverrun) - wydarzenie
      (Kronika/prolog), przeniesienie rodow Dorzecza do krolestwa Polnocy, dyplomacja, przynaleznosc gracza (Jeff jest w Dorzeczu?) - najpierw projekt.
- Limit 5h 82% o 15:25 (odnowienie 16:40) -> 241, 259, 268, 272 uruchomic po 16:42 (budzik w tle). Skrypt workflow paczek: workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js
  (args: lista paczek {id, wt, br, design, auth, task, lens1, lens2}). Po paczkach: scalanie TYLKO scal_sklad.py/scal_wstawki.py, self-testy i pelne grupy sprawdz_logi,
  autotest (bitwaN + probyN, przy 241 i 259 dluzszy bieg), wgranie po zamknieciu gry.

## DOPISEK 27.10 ok. 15:10 CET - AUDYT GRY JEFFA W TOKU (po awarii pradu); w grze 128
- Jeff 27.10 ok. 13:45: "zaproponuj naprawy ... czy ekonomia dziala poprawnie, czemu wojsko czesto nie ma pancerza albo ma tylko ja zle trafiam i mam pecha, i czemu
  dorzecze i polnoc jest slabsza od lannisterow i krolewskiej przystani to nie ma sensu, zrob audyt ... i te karawany". Wczesniej (13:40) odpowiedz o karawanie
  (kapital obrotowy max(10000, 30 dni zoldu) w kiesie, BK placi tylko w osadzie wlasciciela - Jeff bez twierdzy) i warsztacie (cena sprawiedliwa: koszt + 25%,
  nadmiar bizuterii 6/dobe vs popyt 1, podatek BK 57.8% w Lannisport; wyplata ok. 22 zl/dobe).
- Kopie logow do audytu: scratchpad 1e1c276a...\scratchpad\audyt (Armoury-2026-10-27_12-57-35.log, Armoury-2026-10-26_21-30-37.log, session-2026-10-27_12-57-33.log).
- Workflow audyt-gry-jeffa-27-10 (run wf_02052cf7-373; skrypt workflows\scripts\audyt-gry-jeffa-27-10-wf_02052cf7-373.js): 4 sledztwa (ekonomia, pancerz, krolestwa,
  karawany) + sceptycy + synteza po polsku. Po awarii pradu wznowiony z resumeFromRunId (15:08). Wynik = propozycje dla Jeffa, NIC nie wdrazac bez jego zgody.
- Po awarii sprawdzone: Armoury 35f2df7e / CrashScribe bef9db58 w grze, zapis Jeffa (13:49) bez zer, repo i n161 bez wyzerowanych plikow.

## DOPISEK 27.10 ok. 02:00 CET - WGRANE 128 (222d); nic w toku
- WGRANE 128 (01:58): Armoury 35f2df7e (n161 6eda5012, status 056ae5f4, wypchniete) = 127 + 222d. Kopie: Armoury.dll.bak-2026-10-27-przed-wgranie128 i
  D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie128-przed (a06033ef = 127).
- 222d (20719bda + recenzja 6eda5012): zimny trop -> lordowie w zasiegu (droga gry, ReachOwnDays/ReachHelpDays) szukaja wokol osady najblizszej miejsca pojmania
  albo twierdzy rodu porywacza przez 72 h od swojej wiesci; nikt w zasiegu -> "czeka (poza zasiegiem)", budzi sie przy sladzie / wiesci w zasiegu / lochu;
  komunikat wedlug wyniku (ride to free / searching / too far away - for now / not strong enough - for now). Okno wiesci (Heard) w zapisie (pole 24).
- Autotest 128a (probyN 8 dob, zapis Jeffa z Tywinem): kod 0, ERROR 0, odbicie222 12 progow 0 NIE; Tywin: wiesc po 140.4 h, czeka (poza zasiegiem), T9 TAK.
- Do sprawdzenia u Jeffa w grze: ekran druzyny (202), jazda AI na konnego strzelca (204 - autotest bez wrogiej jazdy), komunikat o Tywinie po ok. 6 dobach gry,
  muzyka bitwy -40%, bieg w zbroi i kon (203), eskorty lordow (223).
- Zapis testowy na D:\Backup-Bannerlord\zapisy-testowe-archiwum (..._po-128a.sav). W Game Saves brak autotest-*.

## DOPISEK 27.10 ok. 01:15 CET - WGRANE 127 (202, 203, 204, 222/222c, 223/223b); w toku 222d
- WGRANE 127 (01:10): Armoury a06033ef (n161 6e5a9186, statusy a485ec5a, wypchniete n161 i galezie 202/203/204/222/223) + 40 ogg Bitwa\lufs21.4/24.4/27.4/30.4.
  Kopie: Armoury.dll.bak-2026-10-27-przed-wgranie127 i D:\Backup-Bannerlord\wgrane\2026-10-27-wgranie127-przed (f67326e2 = 126, lista pliki-127.txt).
- Autotesty: 127a (bitwaN + 3 konni lucznicy CrashScribe-at-n204, probyN 6 dob; build c6b74f23) i 127b (probyN 8 dob; a06033ef): kod 0, ERROR 0, 0 NIE.
  203: muzyka bitwy z lufs24.4 przy suwaku 3, bieg w zbroi gracza 39 -> 70 s, kon 47 -> 94 s. 223: slabe partie 358/2138, 16 eskort (935 ludzi), wladcy/glowy slabe 42 -> 19.
  204: wykrycia 158 (pudla 28), koszt 0.02 ms - reakcji jazdy autotest NIE sprawdzil (wrog bez jazdy) - sprawdzi gra Jeffa.
  222c: wiesc o Tywinie po 139 h (bylo 13 dob); ale potem "trop zimny" (wszyscy lowcy 850 jedn. dalej) a komunikat "Their lords ride to free him" i tak sie pokazuje -> 222d.
- NAPRAWY NARZEDZI: tools/sprawdz_logi.py zlozony od nowa (fb66a074) - scalenia "zostaw oba" w 202/203/204/222 zgubily konce p202_main/p203_main.
  Odtad scalanie: scratchpad 1e1c276a...\scratchpad\scal_sklad.py <plik> <BAZA> <galezie...> albo scal_wstawki.py; po scaleniu uruchomic KAZDA grupe w calosci.
- W toku: workflow 222d wf_2d1e8111-b57 (zimny trop -> szukanie w okolicy pojmania i lennach porywacza; nikt w zasiegu -> czeka; uczciwy komunikat). Potem autotest + WGRANIE 128.
- Zapis testowy przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum (autotest-...-2026-10-27_po-127b.sav); w m113\zapisy jest swieze zdjecie bazy z zapisu Jeffa 26.10 22:19.

## DOPISEK 27.10 ok. 00:20 CET - n161 = 126 + 202 + 203 + 204 + 222 + 223 (584f9f96, Armoury c6b74f23); AUTOTEST 127a W TOKU
- Scalenia: 202+203 f3413043, 204 e4817be0, 222 7a51c053 (ZEPSUTE sprawdz_logi - naprawa 99956c53), 223 8d7becef, polaczenie 222+223 584f9f96
  (RescueHunt.Hunters pomija LordEscort.HeldBack). UWAGA: rozwiaz_oba.py ("zostaw oba") psuje sprawdz_logi, gdy git sparuje wspolne konce funkcji
  dwoch galezi - uzywac scratchpad 1e1c276a...\scratchpad\scal_wstawki.py <plik> [OURS THEIRS [BAZA]] (wstawia bloki dopisane wzgledem bazy; odmawia,
  gdy galaz cos zmienia) i ZAWSZE sprawdzic self-testy po scaleniu (ladowanie pliku ok. 60 s).
- 204 dokonczone: 65fc23dc + 5a63aa89 (koszt passu, lamiaca sie strona, FF lucznikow co pass). Testowy CrashScribe-at-n204.dll (test\, md5 6160e8ef = n103 +
  konni lucznicy "harasser"); wrapper m113\testy-noc-204.ps1 -Harasser 3.
- 222 recheck 038ffb78 (odsiecz OB5 wladcy), 223 recheck 5dd6d0d2 (223b znani lordowie: wielkie rody kanonu, glowy bez lenna, postacie ksiazek) + 5f050614.
- Zapis testowy odswiezony z zapisu Jeffa 26.10 22:19 (Tywin w niewoli); stare zdjecie bazy -> D:\Backup-Bannerlord\zapisy-testowe-archiwum. Logi Jeffa z 26.10
  recznie na D:\Backup-Bannerlord\logi-jeffa\2026-10-26-wieczor-dodatkowo (bieg "kopia" po polnocy ich nie bral).
- 00:14 AUTOTEST 127a (m113\seria-127a.out.txt): kopia, bitwaN (harasser 3), probyN 6 dob, flagi dzwiek-bitwy + odbicie. Potem sprawdz_logi grupy p202 p203
  m204jazda odbicie222 e223 m109muz m165mapa; zapis testowy na D:; WGRANIE 127 = DLL + Bitwa\lufs21.4/24.4/27.4/30.4 (40 ogg).

## DOPISEK 27.10 ok. 00:35 CET - 222 i 223 zbudowane po recenzji; recheck w toku; 204 dokonczenie w toku
- 222 (zb\n222): 96a90b9b + d245fde1 + recenzja 47b9f8b8 (md5 8f06d41c). Tywin (juz jeniec) przejety przy starcie sesji - komunikat w 1. godzinie po wgraniu.
  Probe: flaga CrashScribe\armoury-odbicie.flag, sprawdz_logi --grupa odbicie222.
- 223 (zb\n223): e0e600a8 + 6ddfb3f5 + recenzja 11b6dffc (md5 2663767f). Tywin: 95 ludzi, 90% sprzetu, sam zaatakowal (sila gry z tieru), nocny marsz -25%.
  sprawdz_logi --grupa e223. Jeff "znani lordowie a juz glowy rodu" -> 223b: znani lordowie bez ziemi (Jaime itd.) w workflow wf_b6fa88ea-c4c.
- Uwagi recenzji w plikach: scratchpad 1e1c276a...\scratchpad\recenzje\uwagi-222.json, uwagi-223.json; 204: scratchpad\n204\uwagi-204-recenzja.json.
- Plan: po wf_0055b61c-6fc (204) i wf_b6fa88ea-c4c (222/223) scalic 204, 222, 223 do n161 (rozwiaz_oba.py), build, autotest 127a (bitwaN z CrashScribe-at-n204
  i "harasser": 3, probyN 6 dob, flaga armoury-odbicie.flag), sprawdz_logi p202 p203 m204jazda odbicie222 e223 m109muz m165mapa -> po zamknieciu gry WGRANIE 127.

## DOPISEK 26.10 ok. 23:40 CET - 202, 203 gotowe i scalone do n161 (f3413043, NIE wgrane); 204 dokonczenie; 222/223 w toku
- n161 HEAD f3413043 = 126 + 202 (426fe0ba) + 203 (8f48dbcb); build kod 0, Armoury 06c8ab82 (do testu; NIE wgrane - w grze dalej 126).
  203 usunal z repo stare Bitwa\lufs17-26\bit_*.ogg, nowe zestawy Bitwa\lufs21.4/24.4/27.4/30.4 (40 ogg) - przy wgraniu dodac te katalogi (stare moga zostac).
- 204 (zb\n204): 6c561288 + 6f04a755 + recenzja bafb4574 (7 uwag potwierdzonych; poprawiajacy dostal ucieta liste). Dokonczenie: wf_0055b61c-6fc
  (pozostale uwagi: koszt przebiegu, lamiaca sie strona; recheck; testowy CrashScribe-at-n204.dll z konnymi lucznikami "harasser": 3 - tylko do testu).
- 222/223: wf_63b46d86-20c w toku (UWAGA: ten sam skrypt ucina liste uwag do 20000 znakow - sprawdzic po wyniku).
- Konflikty przy scalaniu typu "obie paczki dopisaly w tym samym miejscu" (CHANGELOG, sprawdz_logi): scratchpad 1e1c276a...\scratchpad\rozwiaz_oba.py (zostawia oba).

## DOPISEK 26.10 ok. 22:30 CET - uwagi Jeffa z bitwy na 126; paczki 202-223 w toku (Jeff gra)
- Rycerz Lannisterow z jencow z "full kit" (sledztwo wf_ba8fb83f-df5): NIE blad - kon, uprzaz i zbroja z JEGO zbrojowni (wolny kon bojowy + uprzaz, potem jego
  Chain Mail Horse Armor); ekran druzyny pokazuje wzorzec. Jeff wybral "B" = paczka 202 (prawdziwy sprzet na ekranie druzyny + linie logu werbunku jencow).
- Uwagi Jeffa 22:15 (dokladne slowa w promptach workflow): muzyka bitwy -40%; zbroja meczy o polowe mniej na kg; kon w galopie x2 dluzej; jazda AI ma przeganiac
  konnego strzelca (gracza); po pojmaniu Tywina Lannisterowie po wiesci (kruki) maja go scigac; glowy rodow nie jezdza z garstka biedoty.
- Galezie od eac436b3 (126), drzewa scratchpad fffc991e...\zb\n202, n203, n204, n222, n223:
  202 ekran-druzyny-sprzet, 203 bitwa-glosnosc-stamina (muzyka, zbroja, kon), 204 jazda-na-strzelca, 222 odbicie-lorda, 223 eskorta-lordow.
- Workflowy: wf_0d787158-815 (202+203), wf_a555d7f1-449 (204), wf_63b46d86-20c (222+223); skrypt workflows\scripts\paczki-jeff-26-10-wieczor-wf_03ae36fd-43a.js
  (args = lista paczek). Pierwsze uruchomienie 202+203 (wf_03ae36fd-43a) padlo na bledzie skryptu (null z etapu) - poprawione.
- Po nich: scalic do n161, build, autotest (bitwaN + probyN), dopiero po zamknieciu gry przez Jeffa -> WGRANIE 127.

## DOPISEK 26.10 ok. 21:30 CET - WGRANE 126 (muzyka bitwy 164 + muzyka mapy 165/165b); Jeff sprawdza uchem
- WGRANE 126 (21:25): Armoury f67326e2 (build scalenia 8574a3ad; kod C# = 11d6fcfa) + 80 ogg z drzewa n161 (Lektor\Muzyka\Bitwa\lufsNN\bit_*, Lektor\Muzyka\Mapa\lufsNN\map_*).
  Kopie: Armoury.dll.bak-2026-10-26-przed-wgranie126 i D:\Backup-Bannerlord\wgrane\2026-10-26-wgranie126-przed (8e488bd6 = 125, lista nowych plikow nowe-pliki-126.txt).
  Wycofanie: przywrocic DLL 125 i usunac pliki z listy (stara wersja ich nie czyta, wiec mozna je zostawic).
- Autotest 126b: kod 0, ERROR 0; sprawdz_logi m109muz + m165mapa 0 NIE (MAP-7: psai 0 co klatke, motyw mapy gry zatrzymywany, 1 nasze zdarzenie). Recheck bezpieczenstwa
  (wf_96b60ce9-683): StopFree/ReleaseById bez zastrzezen; poprawiony tylko MAP-7 w sprawdz_logi (resztki odcinkow bitwy, 20788b14).
- Statusy 164/165 w CHANGELOG n161 eac436b3; push n161/n164/n165 (ok. 100 MB muzyki i glosow). Zapis testowy przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.
- DO SPRAWDZENIA u Jeffa: czy na mapie gra jedna muzyka (126a: "leca dwie naraz"), glosnosc mapy -23 LUFS, muzyka bitwy. Wylacznik MCM "Our Map Music" / "BattleMusicOurs".
- ZNANE DROBNE: 159c - po wczytaniu zapisu partia z zegarem po terminie dostaje raz wyjazd po bron zamiast odsylki; leftover glos psai po bitwie (fade 1.00) slyszalny
  dopiero, gdy oddamy muzyke grze (jak w czystej grze). Kredyty ElevenLabs ok. 9 tys. (200 nowych kwestii glosu czeka).

## DOPISEK 26.10 ok. 21:05 CET - 165b (dwie muzyki) zbudowane; AUTOTEST 126b + recheck bezpieczenstwa w toku
- Diagnoza (wf_9cd7649b-016): nasz MapHold pisal psai 0 tylko raz (odczyt przed zapisem); psai co 30-120 s startuje nowy odcinek mapy na innym glosie
  i natywny kanal najpewniej nie zachowuje 0 -> muzyka gry pod naszym utworem; odczyt "psai 0.00" to tylko pole managed (nie mikser). Inne mody wykluczone.
- 165b (zb\n165): 981f17c5 + a86cc5d1, recenzja 5ad02d06 + 40c923ac: psai 0 co klatke, ForceStopThemeWithFadeOut motywu mapy gry co >= 5 s, trzymanie 0 przy
  ladowaniu misji (20 s), linia "kontrola" co 30 s, sprawdz_logi MAP-7 (jedno zrodlo), StopFree (Stop + zwolnienie po id - TEZ dla dzwiekow bitwy!).
- Scalone do n161: 8574a3ad, build kod 0, Armoury f67326e2. 21:03 AUTOTEST 126b (m113\seria-126b.out.txt) + workflow wf_96b60ce9-683 (recheck: bezpieczenstwo
  natywne StopFree/ReleaseById, koszt, ForceStop). Po obu: sprawdz_logi --grupa m165mapa i m109muz na logach 126b, potem odsluch Jeffa -> WGRANIE 126.

## DOPISEK 26.10 ok. 20:25 CET - 126 WSTRZYMANE: Jeff slyszal dwie muzyki naraz na mapie (okno autotestu 126a)
- Autotest 126a: bitwaN + probyN kod 0, ERROR 0, gra przywrocona 20:22 (w grze 125: Armoury 8e488bd6, CS bef9db58; po tescie zostaly puste foldery Lektor\Muzyka\Mapa\lufsNN - bez znaczenia).
- Jeff ok. 20:10 patrzyl na okno autotestu (myslal, ze to jego gra): "muzyki na mapie kampanii na siebie nachodza, leci nasza muzyka a w tle muzyka z gry", "i leca dwie naraz".
  Log twierdzi, ze psai = 0.00 i gra jeden nasz utwor (20:07-20:12 map_07, map_08, map_03) - druga muzyka spoza naszej kontroli albo psai podnoszone przez gre/mod.
- Workflow wf_9cd7649b-016 (naprawa-165-dwie-muzyki): 3 sledztwa (silnik/psai, inne mody, nasz kod) -> diagnoza -> poprawka 165b w zb\n165 + linia kontrolna co 30 s
  + test sprawdz_logi (jedno zrodlo) -> recenzja -> fix. Potem: scalic n165 do n161, autotest, odsluch Jeffa (jego ucho rozstrzyga), dopiero wtedy WGRANIE 126.

## DOPISEK 26.10 ok. 20:05 CET - 164 + 165 scalone do n161; AUTOTEST 126a W TOKU (muzyka bitwy i mapy)
- 10 utworow mapy nagrane (zostalo ok. 9 tys. kredytow realnie; licznik z opoznieniem 13.5 tys.), wyslane Jeffowi. Przerobione bitwa_v2.py --prefiks map -> 40 ogg OK.
- 164 recheck 7fe8c6d0 (motyw ROT stop 50 ms), 165 e96eb615..bc0010c1 + pliki 16f37999. Scalone do n161: 604cb213 (CHANGELOG: wpisy 165, 164 nad 159).
  Build n161 kod 0, Armoury md5 cda4a309 (build z 604cb213). gen_mcm bez zmian, selftesty m109muz i m165mapa OK.
- WAZNE: n161 mial STARE glosy bitwy (WGRANE 123 bylo ze scratchpad, nie z repo) - 315 plikow Lektor\Bitwa innych niz w grze. Naprawione: 4c63bc38 = glosy 123
  z gry do repo (md5 zgodne z manifestem 395/395). Teraz drzewo n161 = gra (832 takie same, 0 roznych) + 80 nowych (bit_*, map_*). NIE wgrywac wgraj-v5 z drzew
  sprzed 4c63bc38 (cofnelyby glosy 123).
- 20:01 AUTOTEST 126a (m113\seria-126a.out.txt; -Dll mfinal126a -Tree n161, bitwaN + probyN 6 dob). Po TAK -> WGRANIE 126 = DLL cda4a309 + 80 ogg
  (Lektor\Muzyka\Bitwa|Mapa\lufs17-26) z drzewa n161; potem statusy 164/165 w CHANGELOG, push n161/n164/n165; zapis autotest-przekierowany-* na D:.

## DOPISEK 26.10 ok. 19:35 CET - WGRANE 125 (159 + 159b + 159c); muzyka mapy nagrywana; 165 w toku
- WGRANE 125 (19:27): Armoury 8e488bd6 = build scalenia f5048698 noc/161-integracja (status 6674fb98, wypchniete; noc/159-nadzy-lordowie tez wypchniete).
  Kopie: Armoury.dll.bak-2026-10-26-przed-wgranie125 i D:\Backup-Bannerlord\wgrane\2026-10-26-wgranie125-przed (05503a8c = 124). CrashScribe bef9db58, flag brak.
  Autotest 163c: bitwaN + probyN 6 dob kod 0, ERROR 0, potkniecia 0; (2b) 32 -> 18-20 gotowych ponad % po wyjazdach, do domu 161 ludzi w 5 partiach (163b: 120).
- Muzyka mapy: Jeff 19:15 "to zrob pozostale 9" - generuj.py --wszystkie (muzyka_mapy\probka, log generuj.log). Kredyty przed: 26 302.
- W toku: wf_d370822d-ca4 (recheck 164 -> paczka 165 w zb\n165). Potem: bitwa_v2/mapa na 10 mp3 -> scalic 164+165 do n161 -> autotest -> WGRANIE 126
  (DLL + Lektor\Muzyka\Bitwa\lufsNN\bit_*.ogg + Lektor\Muzyka\Mapa\lufsNN\map_*.ogg). Zapis testowy autotest-przekierowany-* w Game Saves przeniesc na D: po serii 126.

## DOPISEK 26.10 ok. 19:07 CET - 159c ZBUDOWANE i scalone; AUTOTEST 163c W TOKU
- 159c (zb\n159): c7c00ae2 + 20aacfb7 (recenzja 2), ed31a2c2 + 8c65f8bb (recenzja 3: 4 potwierdzone + drobne poprawione). Recheck: tylko 1 drobna (po wczytaniu
  zapisu partia z zegarem po terminie dostaje raz wyjazd po bron zamiast odsylki, takze gdy przed zapisem nie byla w drodze) - zostawiona, do pozniejszej poprawki.
- Scalone do n161: f5048698 (konflikt CHANGELOG rozwiazany skryptem). Build n161 kod 0, Armoury md5 8e488bd6.
- 19:05 AUTOTEST 163c (m113\seria-163c.out.txt; -Dll mfinal163c, bitwaN + probyN 6 dob). Jesli urwany: gra wedlug pamieci przerwany-autotest-sprzatanie
  (w grze ma byc Armoury 05503a8c), potem powtorzyc tym samym poleceniem z -Prefix 163d. Po TAK -> WGRANIE 125 = DLL 8e488bd6 (kopia .bak + D:, md5, statusy, push).

## DOPISEK 26.10 ok. 18:55 CET - 164 ZBUDOWANE po recenzji; 165 muzyka mapy (Jeff 18:30); 159c w toku
- 164 (zb\n164, noc/164-muzyka-bitwy): 3d1d6b18 + cc9d4cda + recenzja 5171df53 (Armoury md5 8ebc3f2f). Recenzja: "Stop Event Themes On Map" nie dzialal (ROT gra motywy
  402-415 przez PlaySegment na glosach 7/8 warstwy psai) - teraz refleksja psai m_logik/m_playbackChannels + startFade. Pliki bit_* (40 ogg + 10 mp3) w repo galezi.
- Jeff 18:30: "zrob mi jeszcze 10 utworow na mape kampanii w stylu gry o tron lub sredniowiecza minimum 2 minuty ... nie zbyt intensywna, nie rozprasza, dobry podklad".
  Zadania: scratchpad 1e1c276a...\scratchpad\muzyka_mapy (zrob_zadania.py, zadania\map_01..10_*.json po 125 s, generuj.py - pomija gotowe, STOP < 2500 kredytow).
  Probka map_01_krolewski_trakt.mp3 (2:05) wyslana Jeffowi do odsluchu; reszta 9: po jego OK albo (bez odpowiedzi) po WGRANIU 125. Kredyty przed: 26 302.
- Workflow wf_d370822d-ca4: recheck poprawki 164 -> paczka 165 muzyka mapy w zb\n165 (galaz noc/165-muzyka-mapy od 5171df53): nasze utwory map_* tylko na mapie
  kampanii (psai wyciszone), losowo bez powtorek, przerwa 20-40 s, okna wydarzen / misje przejmuja, klucz MCM "Our Map Music".
- Kolejnosc wgran: 125 (159+159b+159c, po autotescie 163c) -> 126 (164 + 165, pliki bit_* i map_* do Lektor\Muzyka\Bitwa|Mapa\lufsNN, po autotescie).

## DOPISEK 26.10 ok. 18:20 CET - NOWE KONTO (poprzednie: limit tygodnia o 15:17); 159c i 164 w toku
- Test 163b (159 + 159b-fix, build 9650e63d = merge 9263c755 w n161): bitwaN + probyN kod 0, ERROR 0, gra przywrocona 15:22 (Armoury w grze 05503a8c = 124).
- Druga recenzja 159b (wf_e0014686-1ec): 2 prawdziwe usterki -> paczka 159c. Wynik recenzji: scratchpad sesji 1e1c276a...\scratchpad\recenzja-159b-fix2-wynik.json.
- Workflowy z 15:17 (159c wf_0e9e7939-ff8, 164 wf_57fa2a26-aee) padly na limicie - 159c nie ruszyl; 164 zostawil NIEZACOMMITOWANE zmiany w zb\n164
  (EventMusic.Bitwa.cs, tools/muzyka/bitwa_v2.py); ogg 10 utworow x 4 lufs gotowe w scratchpad fffc991e...\muzyka_bitwy\out.
- 18:15 nowe konto: workflow 159c wf_f37a28aa-e17 (zb\n159: poprawka, 3 recenzje + obalanie, fix2, recheck) i 164 wf_3f58b257-583 (zb\n164: dokonczenie, recenzja, fix).
  Skrypty: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\1e1c276a-7a4b-41a8-acfd-5b40353b59ab\workflows\scripts\
  Po 159c: merge noc/159-nadzy-lordowie -> noc/161-integracja (zb\n161), build, autotest 163c (bitwaN,probyN 6 dob), dopiero po TAK -> WGRANIE 125 (159+159b+159c).
  Po 164: merge do n161 po 125, autotest, WGRANIE 126 (DLL + bit_*.ogg do Modules\Armoury\ModuleData\Lektor\Muzyka\Bitwa\lufsNN).
- Wrapper testow: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (163b: -Dll test\mfinal163b -NowyArmoury ... -Prefix 163b -Runs bitwaN,probyN).

## DOPISEK 26.10 ok. 13:45 CET - STAN NA WYPADEK URWANIA (Jeff: "zapisz stan, zaraz moze urwac sie tokeny")
**W GRZE (zatwierdzone):** WGRANE 124 = Armoury 05503a8c (commit 87aaa764 galezi noc/161-integracja), CrashScribe bef9db58, glosy 123 (dane).
Gra posprzatana po URWANYM tescie 163a (13:04): przywrocone z C:\Users\GAME\autotest-kopie\2026-10-26_13-04-00 (Armoury, CrashScribe, arm_canon_moves.tsv,
BannerlordConfig.txt LatestSaveGameName, engine_config.txt safely_exited), flaga usunieta. Procedura: pamiec przerwany-autotest-sprzatanie.md.
**UWAGA:** HEAD noc/161-integracja (worktree zb\n161) ma JUZ scalone 159 (merge, build 9650e63d) - NIE wgrane. Wgrane 124 = 87aaa764.
**W TOKU (jesli urwie sie - co zrobic):**
1. Autotest 163b (159 na 124; bitwaN + probyN 6 dob): m113\seria-163b.out.txt. Jesli urwany: SPRAWDZ GRE (md5 Armoury 05503a8c, CS bef9db58, flagi, Configs)
   wedlug pamieci przerwany-autotest-sprzatanie.md, potem powtorz: testy-noc.ps1 -Dll mfinal163c -NowyArmoury zb\n161\Armoury\bin\Release\Armoury.dll
   -Prefix 163c -Runs bitwaN,probyN -ProbyDays 6 -CsAt test\CrashScribe-at-n103.dll -ExtraFlags armoury-dzwiek-bitwy.flag (run_in_background).
   Kryteria: kod 0, ERROR 0 (bledy 27 first-chance = latki BKROTPatch/BK, tak samo w kazdym tescie); w logu linia dobowa "Nowe druzyny i sprzet (159)".
2. Recenzja 159b fix2 (workflow wf_e0014686-1ec): wznowic Workflow(scriptPath=...\workflows\scripts\recenzja-159b-fix2-wf_e0014686-1ec.js, resumeFromRunId).
   Gdy recenzja i test OK -> WGRANIE 125 (159+159b): kopia .bak + D:, md5, statusy w CHANGELOG n161, push.
3. Muzyka: GOTOWE wszystkie 10 utworow (13:50) w scratchpad\muzyka_bitwy\probka\bit_01..10_*.mp3 (01 = 120 s, reszta 150 s). Jeff o probce bit_04:
   "tak swietny klimat". Kredyty ElevenLabs po muzyce: ok. 26.3k (licznik aktualizuje sie z opoznieniem).
4. Paczka 164 (muzyka tylko w bitwie, motywy wojenne off z mapy) - workflow wf_57fa2a26-aee w zb\n164 (galaz noc/164-muzyka-bitwy od 87aaa764).
   Po nim: tools/muzyka/bitwa_v2.py na 10 mp3 -> ogg w 4 zestawach lufs -> wgrac pliki + DLL 164 (scalony z 159 jesli 125 wgrane) po autotescie.
**DECYZJE JEFFA 26.10:** nadzy lordowie "Pozyczka + mniejsze" + max 10% bez broni (brak pancerza OK); ubrania chlopow barwy ziemi bez szlacheckich;
glosy dziko (v4, bloki, RRRAAAH rzadko); muzyka 10 utworow tylko w bitwie, rozdzielic od mapy; wybor glosow zostawil Claude.
**OTWARTE / POZNIEJ:** pytanie do Jeffa "spotkate koszule" = kolory (zrobione) czy dlugosc?; 200 nowych kwestii glosu (ranni hp/pn/dy, sztandar bl/bg/bt/eb, chor ou -
scratchpad\glosy_v4\klipy_v4_nowe.csv, blok_v4.py) nagrac razem z paczka runtime (ranni krzycza z miejsca 50%, max 4 naraz); sztandar B (przejecie, odbijanie)
i C (piesn zwyciezcow); muzykant w szyku (rog/bodhran, raport w transkrypcie: cornu + bodhran z grajkow karczmy, 2 linie action_sets); drobne: sprawdz_logi ND-7
(13 probek zamiast 6), 146 falszywa UWAGA JIT, Fall Back za mape = PANIKA, phantom wpis DTE w SpawnPost (sprzed 159).

## DOPISEK 26.10 ok. 12:35 CET - WGRANE 121-124 (ubrania, jezdziec, glosy v4, sztandar); w toku 159b, muzyka
- LINIA GLOWNA: noc/161-integracja (worktree scratchpad\zb\n161) = 120 + 160 + 157 + 158 (+ wpis 123). Kolejne paczki scalac tu.
- WGRANE 121 (11:45): Armoury 72b07d42 = 160 ubrania chlopow w barwach ziemi (bez barw rodu - UseTeamColor, bez farbowanych ROT; tekstury sprawdzone).
- WGRANE 122 (12:14): Armoury 2d20df52 = 157 jezdziec: tarcza pod ostrzalem (bez zmiany w szarzy szyku), kontra 1-2 jezdzcow na strzelca (20 s), pomiar konnicy AI.
- WGRANE 123 (12:15, DANE): 314 glosow bitwy na nowo (eleven_v4, bloki, 14 glosow z konta, Karda fonetycznie "Kar-da"); kopie starych: Modules\Armoury\kopie\Bitwa-przed-wgranie123 i D:.
  Narzedzia: scratchpad\glosy_v4 (blok_v4.py, nagraj_v4.py, roles_v4.json, klipy_v4.csv, klipy_v4_nowe.csv - 200 nowych NIE nagranych).
- WGRANE 124 (12:29): Armoury 05503a8c = 158 biegacz po upuszczony sztandar (w autotescie niesprawdzony - szyki puste).
- W TOKU: 159b (nadzy lordowie, regula Jeffa: tylko bez broni, max 10%) w zb\n159; muzyka: probka bit_04_szarza czeka na odsluch Jeffa
  (scratchpad\muzyka_bitwy, zadania/*.json, probka_muzyki.py); pytanie do Jeffa: "spotkate" koszule = kolory czy dlugosc.

## DOPISEK 26.10 ok. 11:05 CET - WGRANE 119 (pasek konia) i 120 (dzwiek 3D); w toku 157-160, glosy v4, muzyka
- LINIA GLOWNA: noc/156-dzwiek-3d-na-119 (worktree scratchpad\zb\n156) = 118 + 155 + 154. Nowe galezie 157-160 odgalezione od niej.
- WGRANE 119 (10:49): Armoury 4372c1cb = 155 pasek staminy konia pod paskiem zdrowia konia (UIExtenderEx, klucz HorseStaminaBar). Autotest 155a TAK.
- WGRANE 120 (11:03): Armoury 7b6dc24c = 154 dzwiek bitwy 3D: 395 klipow przez event:/mod/mission/voice_shout / voice / ambient_3d_big
  (Extra/voiceover bylo 2D - stad "werble przy uchu"), odleglosci przy zrodle, pozycja co klatke, blad 'Save the banner!' przy smierci dowodcy.
  Autotest 156a: kod 0, ERROR 0, zdarzenia 3D odrzucone 0. Slyszalnosc 3D - tylko ucho Jeffa. tools/sprawdz_logi.py ND-7 do poprawy (13 probek, nie 6).
- Uwagi Jeffa z gry 25.10 22:45 (przez sesje SKYRIM) i sledztwo: scratchpad\sledztwo-2245.txt. Zold: nie blad (druzyna 331 + karawana 313, regula BK).
- Decyzje Jeffa 26.10: nadzy lordowie = "Pozyczka + mniejsze" + najwyzej 10% nieuzbrojonych; ubrania chlopow - kolory ziemi, dluzsze, bez barw szlacheckich;
  glosy - dziko jak runda 2 (eleven_v4, bloki, wybor glosow zostawil Claude); muzyka - 10 utworow tylko w bitwie, rozdzielic od mapy; muzykant w szyku - pozniej.
- W TOKU: workflow paczek 157 (jezdziec: tarcza, kontra na strzelca, pomiar konnicy), 158 (biegacz po sztandar), 159 (nadzy lordowie), 160 (ubrania chlopow);
  glosy v4: scratchpad\glosy_v4 (klipy_v4.csv, roles_v4.json, blok_v4.py - nagrywanie blokami z with-timestamps, probka bloki_probka czeka na odsluch Jeffa);
  muzyka: scratchpad\muzyka_bitwy\RAPORT.md (10 utworow, ElevenLabs Music, ok. 22-28k kredytow; najpierw 1 probka).

## DOPISEK 25.10 ok. 22:20 CET - WGRANE 118 (fala "AI walczy o zycie" 139-149 + 153 + 117)
- WGRANE 118 (22:13): Armoury 86195e02 z galezi noc/139-ai-zycie (worktree scratchpad\zb\n139, build 8995f2ab, wpis 86fc8054). Galaz zawiera scalone noc/138-calosc (117 + 153),
  wiec to jest TERAZ linia glowna - nowe prace odgalezniac od noc/139-ai-zycie. CrashScribe bez zmian (bef9db58). Kopie: Armoury.dll.bak-2026-10-25-przed-wgranie118, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie118-przed.
- Autotest 118a: kod 0, ERROR 0, 4/4 bitwy; kontrola fali: python -I tools/sprawdz_logi.py --grupa ai139 <Armoury-*.log> (galaz n139) - 9/10 TAK.
  NIE: tarcza pod ostrzalem po 1.5 s 24/36 (67%, prog 90%) - nastepna paczka: pomiar po 3 s i powod braku tarczy.
- Po pierwszej grze Jeffa: --grupa ai139 na jego logu; 141/146/148/149 i oblezenia (145, 147) autotest nie sprawdza. Odlozone drobne: 146 falszywa UWAGA JIT przy bandytach,
  Fall Back za mape = PANIKA. Decyzje Jeffa 25.10 ok. 22:25: odwrot rannego z bronia w oblezeniach/kryjowkach - NIE (zostaje tylko bitwa polowa, bez zmian kodu);
  poscig za armia AI w odwrocie na rozkaz wodza - "zobacze jak pogram" (czekamy na jego uwagi po grze, na razie NIE).

## DOPISEK 25.10 ok. 18:30 CET - WGRANE 117 (151 + 152 + 150a + 150b + 117r); fala AI 139-149 nadal w toku
- WGRANE 117 (18:23): Armoury 2c95388a (galaz noc/138-calosc, commit c55b2c58; statusy 746e87a2/7cc6ba66), CrashScribe bez zmian (bef9db58). Kopie: Armoury.dll.bak-2026-10-25-przed-wgranie117
  obok DLL i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie117-przed.
  - 151: odnowa oddechu wykladniczo od Atletyki (wolny 2x szybciej co 100 pkt od 100; 300 - po wyczerpaniu ok. 3 min, 10 - ponad 20 min).
  - 152: ponad 300 pula RBM 2800 x 2^((A-300)/31) (310 ok. 3500, 330 ok. 5500); oddech i bieg razem z nia; sufit sprawnosci biegu 2.5.
  - 150a: proba albumu obrazkow okna lupu (tylko z flaga armoury-zdjecia-obrazkow.flag). 150b: obrazek okna lupu wedlug miejsca (region kultury osady, Inni, dzicy,
    morze, turniej - sala tronowa tylko w King's Landing, bez Joffreya). 117r: 6 poprawek po niezaleznej recenzji (Skagos to nie dzicy - kultura rodu; bez Ship1/Ship2;
    osada obrazka od miejsca bitwy; osady Innych; krzywa 1.0 + mistrzostwo; opis suwaka).
  - Autotesty 152a, 117a, 117b: kod 0, ERROR 0, 4/4 bitwy, zdjecia okien 22/22. Zdjecia okien lupu: docs/zdjecia-okien-2026-10-25/okna-132-[5-7]-*-117.jpg.
- Znane, niepoprawione (nastepna paczka): tekst okna "Near X" bierze Hero.LastKnownClosestSettlement (rzadko odswiezane) - w autotescie bitwa pod Riverrun, tekst "Near Pendric";
  obrazek juz liczy osade od miejsca bitwy (117r), tekst jeszcze nie.
- W TOKU: workflow fali AI 139-149 (galaz noc/139-ai-zycie, worktree zb\n139) - po nim scalic noc/138-calosc (151/152/150/117r) do 139, test bitwaN, wgranie 118.

## DOPISEK 25.10 ok. 17:20 CET - WGRANE 116; w toku fala AI 139-149 i obrazki lupu 150
- WGRANE 116 (17:15): Armoury 25f69127 + CrashScribe bef9db58 z galezi noc/138-calosc (worktree scratchpad\zb\n138): 132 proba zdjec okien, 133a-g okno Review the army
  (Kit for the next battle z podgladu DTE, Last battle, konie, Speed, Herd, Pay x3, Recent messages; 133a CrashScribe - cisza podgladu), 136 stamina koni (HorseWind), 137 oddech dwufazowy.
  Autotest 138a kod 0, ERROR 0; zdjecia okien w docs/zdjecia-okien-2026-10-25. 133a NIE sprawdzony autotestem (testowy CrashScribe-at) - w logu CS Jeffa linia
  'Mends: podglad gotowosci DTE (133a) - pole _isReadinessPreview JEST'. T1 konia gracza (czy MountSpeed spowalnia) - tylko gra Jeffa: linia 'Konie (136)' v/(Vbase x m) ok. 1.
- Drobne uwagi z przegladu 138 (do poprawy w nastepnej paczce, wpis 138 w CHANGELOG): liczniki NB2 przy koncu odpoczynku konnicy, linia 'BattleWind (130)' z martwa regeneracja,
  opisy MCM po 136/137, licznik E oddechu, Atletyka biegu 127 surowa.
- W TOKU: workflow fali "AI walczy o zycie" (galaz noc/139-ai-zycie od 138, worktree zb\n139; kroki 139 pomiar i przelacznik paniki, 140 tarcza pod ostrzalem, 141 obronca naprzod,
  142 panika bez powrotu, 143 ranny <20% odwrot z bronia, 144 AI oszczedza sily + animacje/glos zadyszki, 145 ranny ostrozny, 146 szyk pod ostrzalem, 147 panika do wyczerpania,
  148 poscig konnicy AI, 149 bohaterowie ostrozni). Decyzje Jeffa 25.10: ranny <20% odwrot z bronia; panika bez powrotu (zbiorka pozniej); lordowie ostrozni; poscig z limitem.
- W TOKU: 150 obrazki okien lupu wedlug miejsca - krok 1 album 48 sprite'ow ROT (galaz noc/150-obrazki, flaga armoury-zdjecia-obrazkow.flag), potem dobor (region, przeciwnik, rodzaj).
- Wrapper testow: m113\testy-noc.ps1 rozbija -ExtraFlags po przecinku (od 13:00).

## DOPISEK 25.10 ok. 13:15 CET - WGRANE 114 i 115; w toku: konie, regeneracja, zdjecia okien, raport armii
- WGRANE 114 (12:31): Armoury 3cab5bea z noc/final (125 okno lupu po turnieju, 126 przypinanie typow wojska). Crash 126a w bitwie 4/4 = AccessViolation w natywnym Agent.MakeVoice
  z Immersive_Battlefields.PlayOrderGesturesPatch.Prefix (IB i gra wolaja Owner.MakeVoice bez IsActive; autotest ponawia rozkaz szarzy co 15 s) - powtorka 126b czysta.
- WGRANE 115 (13:10): Armoury 70246086 z galezi noc/127-bitwa-cialo (worktree scratchpad\zb\n127; CHANGELOG 127-131) + 16 klipow mo_karda w Lektor/Bitwa/motto i manifest.
  127 bieg wedlug fizjologii (jedna regula, w biegu bez oddechu), 128 szarza z daleka marszem (bieg od 60 m), 129 okrzyki krolestwa gracza "Karda" (gdy Jeff wlada krolestwem),
  130 stamina tylko z Atletyki (bez Endurance bohaterow) + krzywa 1.12, 131 udzwig +5 kg za punkt Endurance kazdego bohatera w partii.
- BLAD WRAPPERA (naprawiony 13:00): testy-noc.ps1 '-ExtraFlags a,b' przez powershell -File = JEDNA flaga 'a,b' - dlatego w nocy proby P4/T14/T15 nie ruszaly. Teraz rozbicie po przecinku.
  Proby P4 (przeglad armii), T14/T15 (przesluchanie), test okna turnieju - DO PUSZCZENIA jeszcze raz (biegi probyN / turniejN).
- W TOKU (workflowy tej sesji): rozpoznanie staminy koni (Jeff: "konie tez powinny miec stamine ... zalezna od cech konia"), regeneracja wedlug fizjologii (Jeff: "wyjscie z zadyszki
  jak w prawdziwej fizjologii"), paczka 132 proba zdjec nowych okien (worktree zb\n132, galaz noc/132-zdjecia - flaga armoury-zdjecia-okien.flag), rozpoznanie 133
  raportu w przegladzie armii (Jeff: braki sprzetu i dlaczego nie zaklada, zold, konie, predkosc, herding).
- Jeff wie: stamina koni, regeneracja i 133 beda osobnymi paczkami; zdjecia okien wysle po biegu 132.

## DOPISEK 25.10 ok. 06:30 CET - WGRANE 113 (nocny pakiet gotowy przed 12:00)
- W grze: Armoury 6b4f188b = galaz noc/final 1e7593e5 (CHANGELOG 203ac63a): 114 marsz i doktryny kultur (+S1 ostrzal, S3 flanka, S4 lucznicy za piechota), 115 choragwie, 116 przeglad armii w obozie, 117 okno lupu (bitwa, oblezenie),
  118 friendly fire (strzaly 100% / oblezenie 50%, wrecz 50%, szarza 50%), 119 dzwiek bitwy, 120 przesluchanie (juz w 112), 121 podatek warsztatow (juz w 112), 122 gotowosc AI (sila z uzbrojeniem, dozbrajanie), 123 szturm murow i bebny oblezenia, 124 domyslne + nagrania.
- Pliki poza DLL w grze: Modules\Armoury\ModuleData\Lektor\Bitwa (379 ogg + manifest.tsv, 15 MB) i Modules\Armoury\GUI\Prefabs\ArmSpoilsWindow.xml. Kopia przed: Armoury.dll.bak-2026-10-25-przed-wgranie113, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie113-przed.
- Nagrania ElevenLabs: 298 glosow + 81 SFX, 16 glosow z konta Jeffa (bez dodawania z biblioteki - konto 33 glosy przy limicie 30), kredyty 88 120 -> 76 203. Narzedzia tools/lektor/nagraj_bitwa.py, sfx_bitwa.py na galezi noc/p2a (19a871a5); kopia nagran D:\Backup-Bannerlord\nagrania-bitwa-2026-10-25.
- Autotesty 124b/124d: ERROR 0, 4/4 bitwy. NIE pokryte: oblezenie (szturm, ogien, tlum, bebny), okno lupu po oblezeniu, proba P4 i T14/T15 (nie odpalily w 124c - przyczyna nieustalona), sluch i wyglad.
- Do sprawdzenia w logu Jeffa po pierwszej sesji: ERROR 0; "Dzwiek bitwy (noc-p2): misja" (wyciek 0, odrzucenia wedlug grup), "FF (pole|oblezenie)" (udzial FF), "Choragwie (CH) ekonomia" (odlozone == oddane), "Okno lupu (117)", "Gotowosc do bitwy (p9)" linia doby, "Przesluchanie (m109): pochodzenie - banda", D11-3 "w tym gracz zaplacil".
- Decyzje Jeffa otwarte: dubel SiegeAssaultCrowd z ROTBA Contextual Combat Shouts; glosy z biblioteki zamiast narratorow konta (wymaga zwolnienia miejsc); splity RBM a doktryny (Polnoc, Zelazne, Dorne, Lorath); przeglad armii v2 (przypinanie typow, pelne okno); okno lupu po turnieju (v1.1); wyzwiska (v1.1).
- Zapis testowy autotest-przekierowany przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.

## DOPISEK 25.10 ok. 04:05 CET - WGRANE 112
- W grze: Armoury 68f74d0f = galaz noc/wgranie112 (a14c76d3 + noc/p7-przesluchanie 443b13f5 + noc/p8-podatek 8be4aeca; CHANGELOG 120, 121). Kopia przed: ...\Armoury.dll.bak-2026-10-25-przed-wgranie112 i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie112-przed.
- Autotest 112a: ERROR 0, 4/4 bitwy. Do sprawdzenia w grze Jeffa: "Przesluchanie (m109): pochodzenie - banda", "bez migawki 0", opcja szara; D11-3 "w tym gracz zaplacil X".
- KOLEJNA INTEGRACJA (noc/calosc z workflow noc-bitwa-kod) MUSI scalic noc/wgranie112 i noc/p9-gotowosc (gotowosc AI) + dzwiek szturmu murow i bebny oblezenia (do dopisania).
- Logi sesji Jeffa 02:11-03:46: D:\Backup-Bannerlord\logi-jeffa\2026-10-25_03-50-po-grze. Zapis testowy = kopia jego zapisu z 03:46 (md5 0ea55e89).

## DOPISEK 25.10 ok. 02:35 CET - NOC W TOKU (workflow noc-bitwa-kod, run wf_d049918d-367)
- Projekty z rozpoznania: scratchpad fffc991e...\scratchpad\noc\projekty\ (ai-marsz-taktyki.json, dzwiek-glosy.json, choragwie.json, przeglad-armii.json, okno-lupu.json, friendly-fire.txt, krytyk.txt - podzial P0-P5, kolizje K1-K17, zakres v1).
- Galezie: noc/p0 (baza: klucze, wpiecia, kontrakty) -> noc/p1-marsz, noc/p2a (nagrania ElevenLabs, budzet 25k znakow + 120 SFX), noc/p2b-dzwiek, noc/p3-choragwie, noc/p4-przeglad, noc/p5-lup, noc/p6-ff -> noc/calosc (scalenie, CHANGELOG 114-119) -> audyt.
  Drzewa: scratchpad\zb\noc-<nazwa>. Baza a14c76d3 (= wgrane 111).
- Potem sesja glowna: autotest bitew (UWAGA zmiana czasu), wgranie DLL + plikow poza DLL (Armoury/ModuleData/Lektor/Bitwa/**, Armoury/GUI/Prefabs/ArmSpoilsWindow.xml) przed 12:00.
- Osobno: diagnoza warsztatu Jeffa ("czemu moj warsztat nie daje kasy") - agent tylko czyta logi z D:\Backup-Bannerlord\logi-jeffa\2026-10-25_01-00-po-grze.

## DOPISEK 25.10 ok. 02:15 CET (po zmianie czasu) - WGRANE 111
- W grze: Armoury 9e301ed6 = galaz w-toku/m117-ubranie b74049c9 (111 ubranie cywilne v3, 112 szyk armii na postoju, 113 kij z domu; CHANGELOG na galezi, a14c76d3). Reszta modow bez zmian
  (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e). Kopia przed: Modules\Armoury\bin\...\Armoury.dll.bak-2026-10-25-przed-wgranie111 i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie111-przed.
- Autotest 117a: 4 bitwy, KONIEC OK, ERROR 0; kij 58/58 potwierdzone tymczasowe; szyk 4.05 -> 0.42-0.50 jedn.; ubrania 0 przypadkow (sprawdzic w grze Jeffa). PULAPKA: zmiana czasu 03:00 -> 02:00 myli wrapper (szuka logu po czasie startu).
- Zapis testowy Game Saves\autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (kopia zapisu Jeffa z 00:59, md5 9a0b4ab9) zostaje na nocne testy - po serii przeniesc na D:.
- NOC (Jeff "start" 02:55): pakiet bitwy sredniowiecznej + przeglad armii + okno lupu + friendly fire + dzwiek (przemowy rodow, motta przy szarzy, jeki rannych po bitwie, konie, szczek broni, deszcz/wiatr,
  ptaki i kruki po bitwie, ogien przy oblezeniu) - gotowe na 25.10 12:00. Rozpoznanie: workflow noc-bitwa-rozpoznanie (5 czesci + krytyk) i osobne rozpoznanie friendly fire.

## DOPISEK 25.10 00:20 (drugie konto)
- Kontrola 110 w sesji Jeffa (start 22:24, Armoury-2026-10-24_22-24-27.log): OK - wojna korona-Dorzecze raz (108869, E7/0 + kruk), sojusz Polnoc+Dorzecze
  (108870, do dnia 586), zwrot Neda 292902 raz (nr=done), Cape Wrath jedno oblezenie (bez petli), Stoneknoll 31 przy powrocie wozu, zloto przy wczytaniu +616,
  ERROR Armoury 0. Jeszcze nie bylo: ekran trofeow (bitwa gracza 22:58 jest - sprawdzic linie m114), chleb (po zapisie i wczytaniu), okno ROT o Dorzeczu (dzien 108).
- m117-ubranie (Jeff 23:05: goli zolnierze maja dostac najtansza tunike i buty swojej kultury): galaz w-toku/m117-ubranie (bb367a94, od 6cd80130, wypchnieta),
  drzewo scratchpad fffc991e...\scratchpad\zb\m117-ubranie, Armoury.dll md5 14e193c7 (tylko Armoury; CrashScribe bez zmian). CHANGELOG 111 na galezi.
  Status: ZBUDOWANE, recenzja 2x (2 uwagi minor poprawione), CZEKA na zamkniecie gry -> autotest bitwy na kopii zapisu Jeffa -> wgranie.
- Pytanie Jeffa o rozjechana armie (Gregor Clegane 1577 przy Mousedown Mill, dzien odpoczynku R3-08a): nasz kod nie rusza pozycji doczepionych; szyk gry
  (Army.GetRelativePositionForParty) stawia druzyny za wodzem do ok. 1.5-2 jedn. i nie dociaga ich na postoju. Czeka na Jeffa: najechac na grupe i ewentualnie
  zgoda na sciaganie figurek na poczatku postoju (tylko obraz, raz przy zmianie stanu).

## NAJWAZNIEJSZE - STAN NA 22:30
- W grze: WGRANIE 110 (Armoury ae412d24; galaz w-toku/m116-fabula 6cd80130, wypchnieta; drzewo scratchpad 6570af7b...\scratchpad\zb\m116-fabula). Reszta modow: CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e. Lancuch galezi: 107 (n36 d3cada05) -> 108 (n37 9300d259) -> 109 (n38 34f0db38) -> 109.1 (m115-okno e2417cd3) -> 110 (m116-fabula 6cd80130).
- Zapis Jeffa: Game Saves\IronmanKkUpHftX7ZIq.sav (21:03, md5 35d84a9c; kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze). W Game Saves brak zapisow autotest-* (archiwum D:\Backup-Bannerlord\zapisy-testowe-archiwum, 426 plikow).
- 110 przy pierwszym wczytaniu w kampanii Jeffa: Dorzecze w wojnie z korona, sojusz Polnoc+Dorzecze, zwrot 292 902 Polnocy (raz) - SPRAWDZIC w jego logu (linie 'Wojny kanonu (m104): (4) Dorzecze', 'nr=done:292902', ERROR 0).

## DO ZROBIENIA (kolejnosc)
1. Kontrola 110 w logu Jeffa po jego pierwszej sesji (wyzej) + ekran trofeow ('Ekran trofeow (m114): bitwa ... poza ekranem 0') + wyplaty Stoneknoll przy powrocie wozu.
2. Raport swiata 24.10 (szczegoly nizej): dryf skarbcow koron (36% pieniedzy, cel 22%, bezpiecznik oszczednosci na suficie 3.7), zloto znikajace przy wczytaniu (-43..-84 tys.) i 3 doby z RESZTA ok. -55 tys., martwy front Joffrey-Robb (19 dni bez bitwy; 1 zamek zdobyty w 215 dni), biedne wsie Polnocy i Reach (wsie < 1000 zl 10 -> 51).
3. Mniejsze: ROT wojna #2 EndDay 0 (Dorzecze nie poddaje sie po Krwawych Godach - sprawdzic przy Godach), Beric w Essos, obrazki scen prologu z ROT, nagrody turniejow lokalnych 7-85 zl, Nocna Straz za silna / Dothrakowie za slabi, wywrotka silnika przy wyjsciu (zrzut).
4. Pytanie bez odpowiedzi Jeffa: limit taboru w kolumnie marszowej (MarchPackAllowance 0.25 = 1 zwierze juczne/hodowlane na 4 ludzi; Jeff pytal o -0.62 'Marching column: baggage train') - zaproponowano 1 na 2, czeka na decyzje.

## PULAPKI TESTOW (24.10)
- Wrapper scratchpad\m113\testy-m114.ps1 trzyma zdjecie bazy w m113\zapisy\baza-autotest-przekierowany-*.sav i PRZYWRACA je na starcie, jesli plik w Game Saves jest inny - przed seria na nowszym zapisie Jeffa usunac stare zdjecie (bieg 116a poszedl przez to na zapisie z 02:26).
- Gra testowa wisi przy wyjsciu po 'KONIEC OK' - zamknac PID gry testowej (Bannerlord.BLSE.Standalone), wrapper sam przywraca pliki.
- Katalog DLL z wrappera nie ma CrashScribe.dll - do wgrania dolozyc produkcyjny CrashScribe (84205371) z poprzedniego katalogu.
- Po serii przeniesc autotest-*.sav na D: (NIGDY przy wlaczonej grze).

---
## (dalej: tresc z 21:10)

## Stan gry Jeffa
- W grze: WGRANIE 109.1 (Armoury feb5abdb; galaz w-toku/m115-okno e2417cd3 = w-toku/n38-calosc 34f0db38 + okno inwestycji). Reszta modow jak 106.1 (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e).
- Kampania Jeffa: Ironman Documents\Mount and Blade II Bannerlord\Game Saves\IronmanKkUpHftX7ZIq.sav (ostatni zapis 21:03, kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze).
- 425 zapisow testowych przeniesione na D:\Backup-Bannerlord\zapisy-testowe-archiwum (Game Saves: 105 zapisow, Ithauser tylko jeden). Kopie Ithauser z testow: D:\Backup-Bannerlord\zapisy-testowe-ithauser.
- Jeff skonczyl grac o 21:03. Gra zamknieta.

## Co dzis wgrano (CHANGELOG 107, 108, 109, 109.1)
- 107: walka w bitwach (m113-walka2), pobor we wsiach pobocznych (pula 10%, odrost z przyrostu naturalnego E4/4), kupno ziemi (cena 14-20 lat wedlug relacji), GrowthMaxPercent 1.3 (tez w Armoury.json Jeffa).
- 108: caly lup z bronia na ekranie trofeow (SpoilsSoldierSplitOff - wylaczony ukryty podzial Spoils EnablePrioritySystem), bron ze stanem z ciala (LootWeaponsKeepCondition), dochod prac na zielono (WorksIncomeShown). Potwierdzone w grze: ekran 70/70 i 79/79 sztuk.
- 109: wyplata prac z udzialu dodatku w targu (WorksPayMarketShare; Stoneknoll 1.7 -> 4.5/dobe), komunikat musztry wedlug przyczyny, Lordship i zamek wlaczone po tescie 114a-ziemiaD (T11 Sealbay, T12 Ramsgate TAK).
- 109.1: okno 'Invest in its works' - 5 linii opisu, lista kwot widoczna.

## m116-fabula - WGRANE 22:24 jako 110 (patrz CHANGELOG); ponizej opis zakresu
Jeff 21:00 (wsciekly): "Dorzecze jest w pokoju - co to za bzdura, to bylo pierwsze, co zostalo zaatakowane". Kanon: po pojmaniu Tyriona Gora pali Dorzecze, Jaime bije lordow Dorzecza i oblega Riverrun PRZED egzekucja Neda; potem Polnoc i Dorzecze razem przeciw koronie. U nas plan wpuszczal Dorzecze do wojny ok. 108 dnia opowieci - blad.
Workflow wf_4fdd61ac-eb2 (task waydblgky) - skrypt: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\6570af7b-c8f5-4f3e-827c-790a5efcedd4\workflows\scripts\m116-dorzecze-w-wojnie-i-fabula-wf_4fdd61ac-eb2.js (kopia: docs/przekazanie-m116-workflow.js). Worktree (jesli powstal): scratchpad 6570af7b...\scratchpad\zb\m116-fabula, galaz w-toku/m116-fabula od e2417cd3 (w klonie scratchpad\zb\n35-calosc, origin = to repo).
Zakres (jedna paczka):
1. Dorzecze w wojnie z korona od konca prologu (nowe kampanie) + JEDNORAZOWY catch-up w zapisie Jeffa przy wczytaniu: Dorzecze wypowiada wojne Joffreyowi, Polnoc i Dorzecze razem (Diplomacy jesli sie da), kruk; stary termin ~108 ma byc no-op. Sprawdzic cala mape wojen 298-300 z kanonem (Renly/Stannis vs Joffrey itd.); wojny ROT wybrane przez Jeffa (Dolina-Zelazne, Reach-Dorne) zostaja.
2. Okup Neda: blokada wykupu od konca prologu do egzekucji (Prologue.World.cs RansomDailyPrefix / EndCaptivity tylko p01/p10); jednorazowy zwrot 292 902 ze skarbca korony do Polnocy (log [20:06:26] 'Okupy (178): AI-AI - Eddard Stark ... wolny za 292902').
3. Petla oblezenia Cape Wrath (armia Renly'ego zaczyna i zwija oblezenie 60+ razy - linie 'Koniec oblezenia (KO) ... ROT_castle18 ... odejscie z obozu (zmiana celu ...)'): minimum trwania / histereza celu + kruk 'oblezone' raz na (armia, osada).
4. Chleb biedoty (TownFolk.Bread.cs:17 'pamiec sesji') - zapisac w SaveText.Sync.
5. Kruki: smierc Roberta z pustym tekstem ([18:49:27] Kruki (R3-05c)) - tekst; pojmanie Jaimego (arm_canon_moves.tsv p10 bez wiadomosci, przypisane Dorzeczu) - wiadomosc i Robb/Polnoc.
Potem: build, autotest na kopii zapisu Jeffa 2-3 doby (wojna jest, zwrot raz, ERROR 0), wgranie, CHANGELOG 110.

## Raport swiata (24.10 21:00, pelny w odpowiedzi czatu) - do zrobienia pozniej
- Dryf skarbcow koron (36% pieniedzy, cel 22%), bezpiecznik oszczednosci na suficie 3.7 od 108732; wsie < 1000 zl 10 -> 51; Polnoc i Reach biednieja.
- Pieniadze przy wczytaniu: -43..-84 tys. przy 3 z 4 wczytan; 3 dni z RESZTA ok. -55 tys. (108781, 108817, 108844).
- Wojna Joffrey-Robb bez bitew 19 dni; 1 zamek zdobyty w 215 dni (martwy swiat).
- Beric jeniec, potem w Essos; obrazki scen prologu z ROT; nagrody turniejow lokalnych 7-85 zl; Nocna Straz za silna (7900), Dothrakowie za slabi (173).
- Wywrotka silnika przy wyjsciu 0xC0000005 (znana, ERRORS.md) - zrzut pamieci.

## Jak testowac i wgrywac (sprawdzone dzis)
- Kopia zapisu Jeffa do testu: Copy IronmanKkUpHftX7ZIq.sav -> autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (Ironman zapisuje pod ta nazwa); PO SERII przeniesc autotest-*.sav na D: (NIGDY gdy gra dziala - 17:19 crash przy wczytaniu).
- Wrapper: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (biegi: kopia, bitwa, bitwaB, bitwaOFF, ziemia, ziemiaD, pobor, poborF, wczyt; -Dll <nowy katalog w test\>, -NowyArmoury <dll>, -Tree <drzewo>, -CsAt test\CrashScribe-at-n103.dll, -Prefix, -BezKopii, -PoborDays). Plan: m113\PLAN-TESTOW.md.
- Wgranie: test\wgraj-v5.ps1 -DllDir test\<katalog z 5 DLL> -Tree <drzewo> -Tag 2026-10-xx-wgranieNNN (katalog test = scratchpad 6ba92574...\scratchpad\test). Gry nie moze byc wlaczonej.
- Monitoring gry Jeffa: Monitor na najnowszym Modules\Armoury\Armoury-*.log (ERROR, 'Ekran trofeow (m114): bitwa', 'Ziemia (m113): KUPNO').

## Zasady Jeffa (pamiec)
- Wersje jak najszybciej ("ciagle cos naprawiamy zamiast pograc"): jedna seria testow, potem wgranie; male zmiany wgrywac po zamknieciu gry bez testu ("wgraj jak wylacze gre, na moim logu sprawdzisz").
- Luzne pytania - krotka odpowiedz bez agentow. Liczby z istniejacych modeli moda (nie nowe stale). Jedna regula gracz/AI. Nic nie wgrywac/testowac, gdy gra dziala.
