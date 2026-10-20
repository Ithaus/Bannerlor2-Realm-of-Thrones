# noc-1920 / zawieszenie - naprawa zawieszenia GC i malych metod z zepsutym opisem stosu (142 funkcje)

## Problem z dowodem

Gra Jeffa zamarzla 19.10 o 16:09:23 (proces zyl dalej, zero raportu gry i CrashScribe). Dowod zebrany z zywego
procesu (ClrMD + dbghelp z symbolami MS, D:\Backup-Bannerlord\zawieszenie-2026-10-19-1609\):

- Watek AI partii 28732 (Campaign.PartiesThink -> MobilePartyAi.Tick) alokowal w `Armoury.BkSupplyTemper.NeedCapPostfix`
  (JIT_New) i to uruchomilo odsmiecanie pamieci: `GCHeap::GarbageCollectGeneration -> SuspendEE -> SuspendRuntime ->
  Thread::HandledJITCase(watek glowny) -> StackWalkFramesEx -> EECodeManager::EnsureCallerContextIsValid ->
  VirtualUnwindNonLeafCallFrame -> RtlVirtualUnwind -> RtlpUnwindPrologue -> RtlRaiseStatus -> ProcessCLRException ->
  ExceptionTracker::CreateThrowable -> AllocateObject -> GCHeap::Alloc -> try_allocate_more_space -> wait_for_gc_done`
  (ten sam watek czeka na GC, ktory sam prowadzi = zakleszczenie na zawsze).
- Watek glowny 8628 stal zawieszony (tryb Cooperative) na RIP = +0x0 metody
  `TaleWorlds.MountAndBlade.MBSubModuleBase.OnNetworkTick` (wolana co klatke z `Module.OnNetworkTick`). Bajty: RIP =
  `E9 6B A4 47 FD` (5-bajtowy skok Harmony/MonoMod do `OnNetworkTick_Patch1`), potem `00 00 00`, `40 00 00 00`.
- Skan zywego procesu (skan.txt): 16104 metod z detourem (16097 `jmp rel32`, 7 `jmp [rip]` - wszystkie 7 na duzych
  metodach), z tego **142 z kodem JIT < 5 B (TINY)**:
  - **95 w naszych DLL** (Armoury 93, RealisticCaptivity 1, GrandTourney 1 - lambdy, puste SyncData; zalatala je NASZA
    siec CrashScribe);
  - **47 w cudzych DLL** (22 TaleWorlds.MountAndBlade - m.in. MBSubModuleBase.OnApplicationTick / AfterAsyncTickTick
    wolane co klatke, 14 TaleWorlds.CampaignSystem, 8 Bannerlord.BLSE.Shared, 2 BannerKings, 1 TaleWorlds.Library).
    Czesc z nich latamy MY (poprawka po recenzji - wczesniej pisalem, ze "nie przez nas"): TournamentManager
    .OnPlayerJoinMatch / OnPlayerWinMatch / OnPlayerWinTournament (straz miejsc gry CrashScribe, `Net.WrapHotspots`),
    DefaultSkillLevelingManager.OnShipDamaged / OnShipRepaired / OnTravelOnWater (Armoury `SkillLedger` - wszystkie
    metody menedzera), DefaultWorkshopModel.GetConvertProductionCost (Armoury `WorkshopTrade`). Reszta - latki innych
    modow (BLSE, ButterLib, BK i in.; kto dokladnie - nie ustalalem, bo naprawa i tak obejmuje kazda zalatana metode).

## Przyczyna

JIT kladzie opis stosu metody (UNWIND_INFO) wyrownany do 4 bajtow **zaraz za kodem**. Metoda 1-4-bajtowa (`ret`,
`mov eax,ecx; ret`) ma UNWIND_INFO na +4. Jego pierwszy bajt (wersja+flagi) to oryginalnie **0x19** (wersja 1,
EHANDLER|UHANDLER), a dalej `00 00 00` (prolog/liczba kodow/rejestr) i RVA procedury obslugi na +8 (`40 00 00 00`).
5-bajtowy skok `E9 rel32` zajmuje +0..+4 i nadpisuje **dokladnie ten jeden bajt +4** (wysoki bajt przesuniecia, np.
0xFD albo 0x00). Nowy unwinder Windows 11 25H2 (build 26200) na zlej wersji opisu rzuca `RtlRaiseStatus`; a gdy dzieje
sie to w trakcie zawieszania watkow do GC, CLR 4.8.9363 zakleszcza sie (musi przydzielic obiekt wyjatku w srodku GC).
Uklad potwierdzony w uprzezy: niezalatana mala metoda ma bajty `C3 00 00 00 19 00 00 00 40 00 00 00` - identycznie
jak watek glowny gry; naglowek kodu (RealCodeHeader spod start-8: +0x18 phdrMDesc == MethodHandle.Value, +0x20
nUnwindInfos = 1, +0x24 RUNTIME_FUNCTION, UnwindData - BeginAddress = 4) - to samo na prawdziwych metodach gry.

Zapalnikiem TEGO zawieszenia byla alokacja w `NeedCapPostfix` (Traverse na kazde wywolanie, na watkach AI partii) -
ale wisi kazda z 142 metod, gdy GC zastanie watek na jej skoku; co klatke stoja na nich OnNetworkTick / OnApplicationTick.

## Zmiana

1. **CrashScribe - siec nie zaklada sie na metody o kodzie JIT 1-4 B** (`Net.cs` Patchable, `Config.NetSkipTinyMethods`
   dom. true). Po recenzji: decyduje **rozmiar kodu JIT** czytany z naglowka (`UnwindCore.NativeTiny`: PrepareMethod +
   naglowek, przesuniecie opisu stosu <= 4), a `NetTinyIlBytes` = 12 B to tylko tani filtr wstepny (naglowki czytamy tylko
   dla metod o IL <= 12 B). Gdy naglowka nie da sie odczytac - metoda jest pomijana ostroznie po samym IL (osobny licznik
   w linii logu). Sprawdzone poza gra na prawdziwych DLL (uprzaz G): Armoury 13528 kandydatow (tyle samo co "Net cast
   over Armoury" w logu gry), 720 z IL <= 12 B, z nich **93 z kodem 1-4 B - nazwy identyczne z 93 TINY Armoury ze skanu
   zywej gry**, 627 zostaje pod siecia, 0 nierozpoznanych; RealisticCaptivity 1 z 24, GrandTourney 1 z 1. Razem siec
   znika z **95 metod naszych modow + 3 metod strazy miejsc gry** (TournamentManager) - a nie z ok. 745 jak w etapie 1.
2. **CrashScribe - naprawa opisu stosu** (`UnwindRepair.cs` + rdzen `UnwindCore.cs`, `Config.UnwindRepair` dom. true).
   Dla kazdej zalatanej metody (nasze i CUDZE: gra, BLSE, ButterLib, BK) z naglowka kodu bierzemy polozenie opisu stosu,
   z bajtow skoku jego dlugosc (E9 = 5 B, FF 25 rel32 = 6 B, FF 25 0 + adres = 14 B - wzorce MonoMod.Core 1.3.3; stary
   48 B8..FF E0 = 12 B) i liczymy nakladke. Skok E9 przy opisie na +4 przepisujemy tak, by szedl przez wlasna trampoline
   (miejsce 16 B w bloku 4 KB z `VirtualAlloc` w oknie [start+5+0x19000000, +0x19FFFFFF], `FF 25` + 8-bajtowy adres
   celu; bloki wspolne dla metod lezacych blisko), dzieki czemu wysoki bajt przesuniecia (+4) znow = 0x19 - opis stosu
   poprawny, a skok trafia tam, gdzie trafial (latka dziala bez zmian). Zapis skoku atomowy (`InterlockedCompareExchange64`
   ze starym wzorcem jako oczekiwanym) + FlushInstructionCache. Inne nakladki (6/12/14 B) - nie naprawiamy, liczymy i
   wypisujemy z nazwa metody. Punkt wejscia -> poczatek kodu: podazamy za prekodem `E9`, `48 B8 adres FF E0` i thunkiem
   typow MarshalByRefObject (te same wzorce, za ktorymi idzie MonoMod), zawsze z weryfikacja phdrMDesc == MethodHandle.Value.
   **Kiedy** (po recenzji): skan zlecony na pierwszy OnApplicationTick po OnBeforeInitialModuleScreenSetAsRoot (gdy
   WSZYSTKIE mody skoncza swoje latki z tej chwili) i po OnSessionLaunched (po wszystkich sluchaczach, takze innych modow),
   raz na dobe, pelny skan co 5 min (ciche - linia tylko przy zmianie; znane metody z pamieci podrecznej, wiec tanio;
   lapie latki zakladane np. w bitwie) i lekka straz co 30 s (bajty znanych malych metod, bez alokacji; linia przy kazdej
   zmianie, takze nieudanej naprawie). Potkniecia liczone per metoda (3 bledy przejsciowe - czekamy, az bajty sie zmienia).
3. **Armoury - `NeedCapPostfix` bez alokacji na watkach AI** (`BkSupplyTemper.cs`): `AutoBuying` czytane getterem
   skompilowanym raz w `ApplyAll` zamiast `Traverse` co wywolanie (to byla alokacja-zapalnik). Wynik bez zmian.
4. **CrashScribe - linia GC** (`GcPulse.cs`, `Config.GcPulseLog` dom. true): raz na minute zegara linia `GC (UW)`
   z liczba odsmiecan gen0/1/2 i ich przyrostem - zeby widziec, jak czesto GC zatrzymuje watki.

### Wybor sposobu naprawy i testy w uprzezy (tools/uw-uprzaz, konsola .NET 4.8 x64 z 0Harmony.dll z gry, Win 11 25H2)
Rdzen naprawy (`CrashScribe/src/UnwindCore.cs`) uprzaz kompiluje wprost z drzewa - testy dotycza dokladnie kodu z gry.
- **A** (pusta metoda wirtualna z latka Harmony, uklad kodu jak w grze): **ZAWIESZA sie** (3/3 w etapie 2, i znow po
  recenzji) w < 15 s przy intensywnym GC. Potwierdza przyczyne.
- **B** (metoda nie-tycia z latka): bez zawieszenia 300 s, ~976 tys. GC gen0.
- **C** (mala metoda BEZ latki = regula "siec bez malych metod"): bez zawieszenia 600 s+, ~1,37 mln GC gen0.
- **D** (A + naprawa): etap 2 - 11 min, 1,6 mln GC gen0; po recenzji (UnwindCore) - 150 s, 462 tys. GC gen0, bez
  zawieszenia, postfix wolany przy kazdym wywolaniu (licznik trafien = liczba petli), skok po naprawie `E9 1B 61 00 19`.
- **L** (rdzen na swiezych metodach): Tiny/StaticTiny/lambda - NativeTiny 1, opis stosu +4; duza - 0, +32; po naprawie
  cel skoku TEN SAM (przez trampoline), postfix +1; Unpatch (MonoMod przywraca kopie, Harmony zaklada nowy skok) i
  ponowna latka - wykryte i naprawione znowu, postfix +1; UnpatchAll bez wyjatku; 2 metody - 1 blok trampolin.
- **G** (regula sieci na prawdziwych DLL) - wyniki w p.1.
- **P** (pusty postfix na prawdziwych metodach gry poza gra): MBSubModuleBase.OnApplicationTick / AfterAsyncTickTick /
  OnNetworkTick, DefaultSkillLevelingManager.OnShipDamaged, DefaultWorkshopModel.GetConvertProductionCost,
  ViewModel.OnFinalize, BK PopulationClass.get_count, CampaignEventReceiver.DailyTickClan - kazda: naglowek zgodny,
  opis stosu +4, skok psuje bajt +4, naprawa `Repaired`, drugi raz `AlreadyOk`.
Wybrano sposob C (przepisanie skoku), bo nie wymaga zmiany struktur CLR i zostawia dzialanie latki nietkniete.

## Decyzje (co postanowilem sam i jakim kluczem to wylaczyc)

- **Naprawa cudzych metod (gra/BLSE/ButterLib/BK), nie tylko naszych** - bo metoda, na ktorej gra Jeffa zawisla
  (OnNetworkTick), jest cudza, wolana co klatke. Regula "siec bez malych metod" (p.1) usuwa tylko nasze 95 (+3 strazy);
  pozostale 44 (w tym co klatke i te, ktore latka Armoury przez SkillLedger/WorkshopTrade) naprawia UnwindRepair.
  Wylacznik: `UnwindRepair=false` (w CrashScribe.settings.xml) - wtedy nic nie piszemy do pamieci (linia "naprawa WYLACZONA").
- **Naprawa domyslnie WLACZONA.** Jeff ma rano grac finalna wersje, a to jedyne, co zdejmuje ryzyko tego zawieszenia
  z metod wolanych co klatke. Downside jest ograniczony: piszemy do pamieci TYLKO gdy caly wzorzec pasuje (E9 na
  starcie, start wyrownany do 8, naglowek kodu zgodny z uchwytem metody, jeden RUNTIME_FUNCTION, opis stosu dokladnie na
  +4, bajty +5..+7 = 00 00 00, niezerowy adres obslugi na +8); przy jakiejkolwiek niezgodnosci metoda jest POMIJANA i
  liczona w logu z nazwa - nigdy nie psujemy metody o innym ukladzie, najwyzej jej nie naprawimy.
- **Regula sieci po rozmiarze kodu JIT, nie IL** (po recenzji). `NetTinyIlBytes` (12 B) zostaje jako filtr wstepny.
  Gdy naglowka nie da sie przeczytac - pomijam metode po samym IL (ostroznie; na naszych DLL takich metod 0). Wylacznik
  reguly: `NetSkipTinyMethods=false` (siec na wszystkich metodach jak dotad; naprawe opisu stosu robi wtedy UnwindRepair).
- **Prog malej metody dla sieci: kod 1-4 B** (tylko te psuje skok 5 B; MonoMod uzywa skoku 6/14 B tylko, gdy cel jest
  dalej niz 2 GB - w grze 7 z 16104, wszystkie na duzych metodach). Gdyby skok 14 B trafil kiedys na metode 5-12 B,
  UnwindRepair wypisze ja z nazwa ("skok FF25+adres 14 B nad opisem stosu").
- **UnwindRepair ma wlasny prog wstepny IL 64 B** (stala w kodzie, niezalezna od NetTinyIlBytes - po recenzji),
  a o nakladce decyduje naglowek.
- **Naprawiamy tylko skok E9** (142 z 142 w grze). Skoki 6/12/14 B nad opisem stosu - liczone i wypisywane, bez naprawy.

## Ryzyko / co sprawdzic

- **Regresje:** p.1 zdejmuje siec CrashScribe z 95 metod naszych modow (lambdy menu, puste SyncData) i 3 metod
  TournamentManager. Metody 1-4 B nic prawie nie robia (zwracaja stala, argument albo pole); jedyny mozliwy wyjatek to
  NullReference przy odczycie pola - wtedy lapie go siec metody wolajacej (jesli nasza) albo zapisuje go licznik
  FirstChance CrashScribe ("ERROR CAUGHT FURTHER UP"). Linia `Net (UW): ...` pokazuje liczby. `Net cast over Armoury`
  pokaze 13435 metod (bylo 13528), RealisticCaptivity 183 (184), GrandTourney 56 (57), straz miejsc gry 38 (41).
- **NativeTiny kompiluje (PrepareMethod) metody o IL <= 12 B przed decyzja** - te same, ktore do WGRANIA 88 siec
  latala, a MonoMod przy latce i tak je kompilowal (PlatformTriple.Compile) - wiec bez nowego zachowania.
- **Naprawa:** pisze do zywego kodu. Ograniczenia bezpieczenstwa opisane wyzej (wzorzec albo pomijam). Zapis atomowy
  8 B - watek wykonujacy widzi stary albo nowy skok, oba poprawne i trafiaja w ten sam cel. Zdjecie latki przez MonoMod
  przywraca ZAPISANA kopie oryginalnych bajtow (MonoMod.Core 1.3.3 SimpleNativeDetour.UndoCore pisze `DetourBackup` bez
  czytania biezacych bajtow; Harmony 2.4.2 PatchTools.DetourMethod robi Dispose starego i CreateDetour nowego) - nasza
  zmiana mu nie przeszkadza (sprawdzone tez w uprzezy L: Unpatch, ponowna latka, UnpatchAll bez wyjatku); nowy skok
  naprawi straz 30 s / skan.
- **Trampoliny nie sa zwalniane** (watek moze akurat przez nie przechodzic). Koszt: miejsce 16 B na naprawe, bloki 4 KB
  wspolne (w uprzezy 2 metody - 1 blok); linia UW podaje liczbe blokow.
- **Kolizje:** nikt inny nie pisze tych bajtow poza MonoMod (skoki latek). Watek glowny: wszystkie skany i straz biegna
  z OnApplicationTick / DailyTick (watek glowny), nic z watkow AI. Zapis kampanii nietkniety (SyncData pusty).
- **Czego nie dalo sie sprawdzic:** nie wolno uruchomic gry (zajeta testem), wiec naprawy nie potwierdzono w zywej
  kampanii - tylko w uprzezy (ta sama 0Harmony.dll, ten sam CLR/OS/JIT, prawdziwy detour i GC, prawdziwe metody gry).
  Pierwszy autotest/sesja musi pokazac linie `Opis stosu (UW)` z liczbami i zero bledow (szczegoly nizej).

## Test w grze (co ma pokazac autotest 5 dob + bitwa, albo 160 dob)

- `Net (UW): siec modow - male metody bez sieci (kod JIT 1-4 B, sprawdzane IL <= 12 B): 95, wg samego IL (naglowek
  nieczytelny): 0 (RealisticCaptivity 1, Armoury 93, GrandTourney 1).` i `Net (UW): straz miejsc gry - ...: 3, ...: 0.`
- `Net cast over Armoury (Armoury): 13435 methods.` (RealisticCaptivity 183, GrandTourney 56), `Watch over the game's hotspots: 38 methods.`
- W menu (pierwsza klatka po zaladowaniu modow): `Opis stosu (UW, menu): zalatanych metod ~16000, malych (kod <= 12 B)
  S, nachodzacych na opis stosu M - naprawionych teraz K, juz dobrych 0, pominietych 0; nierozpoznanych U (...); blokow
  trampolin B | co klatke: MBSubModuleBase.OnApplicationTick naprawiona, AfterAsyncTickTick naprawiona, OnNetworkTick
  naprawiona.` Oczekiwane: K = M, pominietych 0, B kilka; M w menu mniejsze niz 44 (czesc latek, np. Armoury
  SkillLedger/WorkshopTrade, wchodzi dopiero w kampanii), a po linii `sesja` "naprawionych" z menu + sesji razem ok. 44
  (142 - 95 - 3), co klatke wszystkie trzy "naprawiona" (w linii sesji: "dobra"). Jesli OnNetworkTick = "bez latki",
  to w tej sesji latka siedzi na wrapperze BLSE (MBSubModuleBaseSimpleWrapper.OnNetworkTick - jest na liscie TINY) -
  wtedy liczy sie, ze K = M i pominietych 0.
- Na starcie sesji: `Opis stosu (UW, sesja): ...` - nowe latki z OnSessionLaunched (naprawionych teraz zwykle kilka,
  reszta "juz dobrych"); raz na dobe `Opis stosu (UW, doba): ...` (naprawionych teraz zwykle 0). Co 5 min linia
  `Opis stosu (UW, 5 min)` tylko przy zmianie; `Opis stosu (UW, straz 30s)` tylko gdy czyjas latka przepisala skok.
- Jesli `pominietych` > 0 - linia `Opis stosu (UW, <faza>): pominiete (do 20): Typ.Metoda (powod); ...`; jesli
  `nierozpoznanych` > 0 - linia `... nierozpoznane (pierwsze 12): ...` (to metody bez zgodnego naglowka - NGEN, wspolny
  kod generyczny; nie musza nachodzic, sa podane, zeby bylo wiadomo, czego nie sprawdzono).
- `GC (UW): minuta N | gen0 ... (+..), gen1 ..., gen2 ... | pamiec zarzadzana .. MB` - raz na minute.
- Zero ERROR w session-*.log i Armoury.log; gra nie zawisa przez caly bieg (160 dob albo 5 dob + bitwa x20).
- Specjalny plik/flaga: nie potrzeba. Klucz awaryjny: `UnwindRepair=false` w CrashScribe.settings.xml cofa calosc
  naprawy (pomiar GC i siec-bez-malych zostaja).

## Crash 19.10 10:55 - czy to samo?

Gra stanela 1-2 s po zamknieciu 3. okna CraftPopup, proces zyl 2 min, bez raportu. Brak zrzutu stosu z tego
zawieszenia (straznik zawieszen CrashScribe tez nie dal zrzutu - w obu zawieszeniach 10:55 i 16:09 jego linia zrzutu
nie powstala; zgodne z zakleszczeniem GC, bo straznik to watek ZARZADZANY, wiec GC zamroziby i jego). Bez stosu
nie da sie tego potwierdzic. PRZESLANKA, ze to ta sama rodzina bledu: w sciezce CraftPopup jest mala lambda
`Armoury.CraftPopup+<>c.<ShowGauntlet>b__9_1` (na liscie 142 TINY), a zamykanie okna zostawia watek glowny na krotko
w takim kodzie - gdy GC trafi w ten moment, objaw jest identyczny. Po zmianie p.1 ta lambda nie ma juz latki (jest
wsrod 93 metod Armoury bez sieci - uprzaz G), wiec nie ma tez zepsutego opisu stosu. Obejscie Jeffa (MCM "Craft Result
Popup" off) mozna zostawic do pierwszej sesji z ta wersja; potwierdzenie dopiero w grze.

## Recenzja (19.10) - co z kazdym znaleziskiem

- **(wazne) Siec po IL zdejmuje ok. 745 zamiast ok. 95** - PRAWDA (720 Armoury z IL <= 12 B, z tego 627 z kodem >= 5 B).
  Poprawione: decyduje kod JIT z naglowka (p.1); sprawdzone na prawdziwych DLL - 93 + 1 + 1, nazwy jak w skanie gry.
  Liczby w "Test w grze" poprawione.
- **(wazne) Pominiecia "nie E9" niewidoczne, nierozpoznane liczone jako nachodzace** - PRAWDA. Poprawione: najpierw
  naglowek (bez zgodnego naglowka - osobny licznik "nierozpoznanych" z nazwami), potem nakladka liczona dla kazdego
  rodzaju skoku (5/6/12/14 B); skoki inne niz E9 nad opisem stosu - "pominiete" z powodem i nazwa metody; potwierdzenie
  stanu MBSubModuleBase.OnApplicationTick / AfterAsyncTickTick / OnNetworkTick w kazdej linii; straz 30 s pisze tez
  nieudane naprawy. Zdanie o detourze 12 B poprawione: MonoMod.Core 1.3.3 ma 5 / 6 / 14 B (x86_64Arch: Rel32Ind64Kind
  Size 6, Abs64Kind Size 14).
- **(drobne) Wspolny prog wiaze dwa wylaczniki** - PRAWDA. UnwindRepair ma wlasny prog wstepny (64 B, stala).
- **(drobne) Skan w menu za wczesnie, skan sesji przed innymi sluchaczami** - PRAWDA. Skany zlecane na pierwszy
  OnApplicationTick po tej chwili (Module.SetInitialModuleScreenAsRootScreen wola OnBeforeInitialModuleScreenSetAsRoot
  wszystkich submodulow w jednej petli - dekompilacja), plus pelny skan co 5 min.
- **(drobne) Bledne twierdzenia w CHANGELOG** - PRAWDA dla "47 cudzych nie przez nas" (poprawione w "Problem z dowodem")
  i "11 CampaignSystem" (jest 14). Dalsza czesc tekstu recenzji doszla ucieta; pozostale twierdzenia przejrzalem sam
  (detour 12 B, "wyjatek lapie siec metody wolajacej" - doprecyzowane w "Ryzyko").
- **(drobne) Luki w strazy odczytu** - PRAWDA (czytanie naglowka do +0x30 przy sprawdzeniu do +0x28). Poprawione: Readable
  na caly odczytany zakres (naglowek, cala tablica RUNTIME_FUNCTION, 16 B kodu; straz 30 s tez przez Readable).
- **(drobne) Trampoliny sie nie zwalniaja** - PRAWDA i celowo (nie wiadomo, czy jakis watek przez nie nie przechodzi).
  Zmniejszone: bloki 4 KB wspolne zamiast rezerwacji 64 KB na kazda metode.

## Status: DO SPRAWDZENIA
Zbudowane (CrashScribe 0 ostrzezen, Armoury - 0 Error). Mechanizm naprawy potwierdzony w uprzezy (A wisi, B/C/D nie;
L, G, P zgodne). W grze Jeffa jeszcze nie uruchomione.
