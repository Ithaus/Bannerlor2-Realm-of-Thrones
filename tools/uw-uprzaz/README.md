# Uprzaz UW - test zawieszenia GC na malej metodzie z latka Harmony

Konsola .NET Framework 4.8 x64 z 0Harmony.dll z gry (skopiuj do lib/ z
Modules\Bannerlord.Harmony\bin\Win64_Shipping_Client). Rdzen naprawy NIE jest kopia: projekt kompiluje
prosto CrashScribe/src/UnwindCore.cs (wlasciwosc CoreSrc w harness.csproj; przy budowie poza drzewem
podaj -p:CoreSrc=<pelna sciezka do UnwindCore.cs>), wiec test sprawdza dokladnie kod, ktory idzie do gry.

Budowa: dotnet build -c Release. Uruchomienie przez zewnetrznego straznika (gdy GC
zakleszcza proces, wszystkie watki zarzadzane staja - straznik musi byc osobnym procesem):

  python -I straz.py <harness.exe> <wariant> <sekundy> <watki_alokujace> "" <MB_wypelnienia>

Warianty: A = pusta metoda wirtualna z latka (uklad kodu jak w grze, wypelnienie 64 MB) - ZAWIESZA;
B = metoda nie-tycia z latka - nie; C = mala metoda BEZ latki (nasza regula siec-bez-malych) - nie;
D = A + naprawa (UnwindCore.Locate + Check) - nie.
L (bez straznika: harness.exe L) = rdzen na swiezych metodach: NativeTiny/naglowek/przesuniecie opisu
stosu dla metody wirtualnej, statycznej, lambdy i duzej; naprawa, cel skoku przed/po (przez trampoline),
wywolanie postfixa, zdjecie latki (Unpatch), ponowne zalozenie i ponowna naprawa, wspolny blok trampolin.
Kod wyjscia 0 = "L: WSZYSTKO ZGODNE".
G (harness.exe G <dll> <katalog gry> [IL]) = regula sieci CrashScribe (filtr Net.Patchable + UnwindCore.NativeTiny) na prawdziwej
DLL moda poza gra: ile metod traci siec (kod JIT 1-4 B), ile ja zachowuje, ile nierozpoznanych.
P (harness.exe P <katalog gry> <dll> <typ> <metoda> ...) = pusty postfix Harmony na prawdziwej metodzie gry/moda poza gra i rdzen
naprawy jak w CrashScribe (Locate -> Check dwa razy).

Wyniki 19.10 (etap 2/3): A 3/3 zawieszenie < 15 s, B/C/D czysto (B 300 s, C 600 s, D 11 min przy 1,6 mln GC gen0).
Wyniki 19.10 po recenzji (rdzen UnwindCore.cs): L zgodne (Tiny/StaticTiny/Lambda: NativeTiny 1, opis stosu +4;
NonTiny: 0, +32; cel skoku ten sam; postfix +1 po naprawie i po ponownej latce; 1 blok trampolin na 2 metody);
A zawieszenie < 15 s; D 150 s czysto, 462 tys. GC gen0, wywolan postfixa = liczba petli.
G (19.10, Armoury.dll z tej galezi): kandydatow 13528 (= "Net cast over Armoury" w logu gry), IL <= 12 B: 720 -> kod 1-4 B: 93
(nazwy IDENTYCZNE z 93 TINY Armoury w skan.txt z zywej gry), wiekszy 627, nierozpoznanych 0; RealisticCaptivity 184/24 -> 1;
GrandTourney 57/1 -> 1; ForgeView 51/15 -> 1 (ForgeView nie jest pod siecia).
P (19.10): MBSubModuleBase.OnApplicationTick / AfterAsyncTickTick / OnNetworkTick, DefaultSkillLevelingManager.OnShipDamaged,
DefaultWorkshopModel.GetConvertProductionCost, ViewModel.OnFinalize, BK PopulationClass.get_count, CampaignEventReceiver.DailyTickClan:
naglowek zgodny, opis stosu +4, 1 RUNTIME_FUNCTION, skok z latka psuje bajt +4 (0x00), naprawa Repaired, drugi raz AlreadyOk (bajt +4 = 0x19).
