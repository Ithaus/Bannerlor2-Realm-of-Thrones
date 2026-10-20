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
- Skan zywego procesu: 16104 metod z detourem, z tego **142 z kodem JIT < 5 B (TINY)** - 95 w naszych DLL (lambdy,
  puste SyncData - to NASZA siec CrashScribe je zalatala), 47 cudzych (22 TaleWorlds.MountAndBlade m.in.
  OnApplicationTick / AfterAsyncTickTick / OnNetworkTick wolane co klatke, 11 CampaignSystem, 8 BLSE.Shared, 2 BK,
  1 Library - zalatane przez BLSE / ButterLib / BK, nie przez nas).

## Przyczyna

JIT kladzie opis stosu metody (UNWIND_INFO) wyrownany do 4 bajtow **zaraz za kodem**. Metoda 1-bajtowa (`ret`) ma
UNWIND_INFO na +4. Jego pierwszy bajt (wersja+flagi) to oryginalnie **0x19** (wersja 1, EHANDLER|UHANDLER), a dalej
`00 00 00` (prolog/liczba kodow/rejestr) i RVA procedury obslugi na +8 (`40 00 00 00`). 5-bajtowy skok `E9 rel32`
zajmuje +0..+4 i nadpisuje **dokladnie ten jeden bajt +4** (0x19 -> 0xFD, wysoki bajt przesuniecia). Nowy unwinder
Windows 11 25H2 (build 26200) na zlej wersji opisu rzuca `RtlRaiseStatus`; a gdy dzieje sie to w trakcie zawieszania
watkow do GC, CLR 4.8.9363 zakleszcza sie (musi przydzielic obiekt wyjatku w srodku GC). Uklad potwierdzony w uprzezy:
niezalatana mala metoda ma bajty `C3 00 00 00 19 00 00 00 40 00 00 00` - identycznie jak watek glowny gry.

Zapalnikiem TEGO zawieszenia byla alokacja w `NeedCapPostfix` (Traverse na kazde wywolanie, na watkach AI partii) -
ale wisi kazda z 142 metod, gdy GC zastanie watek na jej skoku; co klatke stoja na nich OnNetworkTick / OnApplicationTick.

## Zmiana

1. **CrashScribe - siec nie zaklada sie na male metody** (`Net.cs`, `Config.NetSkipTinyMethods` dom. true, prog
   `NetTinyIlBytes` = 12 B). Usuwa wszystkie 95 naszych lampek naraz (lambdy, SyncData) - te metody przestaja dostawac
   skok Harmony, wiec nie maja juz zepsutego opisu stosu. Wyjatek z nich i tak lapie siec metody wolajacej.
2. **CrashScribe - naprawa opisu stosu** (`UnwindRepair.cs`, nowy, `Config.UnwindRepair` dom. true). Dla kazdej malej
   zalatanej metody (nasze i CUDZE: gra, BLSE, ButterLib, BK) przepisuje skok tak, by szedl przez wlasna trampoline
   (`VirtualAlloc` w oknie [start+5+0x19000000, +0x19FFFFFF], `FF 25` + 8-bajtowy adres celu), dzieki czemu wysoki bajt
   przesuniecia (ten na +4) znow rowna sie 0x19 - opis stosu poprawny, a skok trafia tam, gdzie trafial (latka dziala
   bez zmian). Zapis skoku atomowy (`InterlockedCompareExchange64` ze starym wzorcem jako oczekiwanym). Skan: w menu
   (po zalozeniu sieci), na starcie sesji i raz na dobe; lekkie sprawdzenie co 30 s z OnApplicationTick (porownanie
   bajtu +4 znanych metod, bez alokacji w klatce) - naprawia ponownie, jesli czyjas latka nadpisala nasz skok. Linia
   logu: `Opis stosu (UW, <faza>): ...`.
3. **Armoury - `NeedCapPostfix` bez alokacji na watkach AI** (`BkSupplyTemper.cs`): `AutoBuying` czytane getterem
   skompilowanym raz w `ApplyAll` zamiast `Traverse` co wywolanie (to byla alokacja-zapalnik). Wynik bez zmian.
4. **CrashScribe - linia GC** (`GcPulse.cs`, `Config.GcPulseLog` dom. true): raz na minute zegara linia `GC (UW)`
   z liczba odsmiecan gen0/1/2 i ich przyrostem - zeby widziec, jak czesto GC zatrzymuje watki.

### Wybor sposobu naprawy - test w uprzezy (tools/uw-uprzaz, konsola .NET 4.8 x64 z 0Harmony.dll z gry, Win 11 25H2):
- **A** (pusta metoda wirtualna z latka Harmony, uklad kodu jak w grze): **ZAWIESZA sie 3/3** w < 15 s przy
  intensywnym GC. Potwierdza przyczyne.
- **B** (metoda nie-tycia z latka): bez zawieszenia 300 s, ~976 tys. GC gen0.
- **C** (mala metoda BEZ latki = nasza regula "siec bez malych metod"): bez zawieszenia 600 s+, ~1,37 mln GC gen0.
- **D** (A + nasza naprawa UnwindRepair): bez zawieszenia (bieg 11 min, przyrost GC jak w A), skok po naprawie
  `E9 1B 61 00 19 ...` (bajt +4 = 0x19), postfix dalej wolany (licznik trafien rosnie).
Wybrano sposob C naprawy (przepisanie skoku), bo nie wymaga zmiany struktur CLR i zostawia dzialanie latki nietkniete.

## Decyzje (co postanowilem sam i jakim kluczem to wylaczyc)

- **Naprawa cudzych metod (gra/BLSE/ButterLib/BK), nie tylko naszych** - bo metoda, na ktorej gra Jeffa zawisla
  (OnNetworkTick), jest cudza, wolana co klatke. Sama regula "siec bez malych metod" (p.1) usuwa tylko NASZE 95; 47
  cudzych, w tym te najniebezpieczniejsze (co klatke), naprawia dopiero UnwindRepair. Wylacznik: `UnwindRepair=false`
  (w CrashScribe.settings.xml) - wtedy nic nie piszemy do pamieci, metody zostaja zepsute jak dotad.
- **Naprawa domyslnie WLACZONA.** Jeff ma rano grac finalna wersje, a to jedyne, co zdejmuje ryzyko tego zawieszenia
  z metod wolanych co klatke. Downside jest ograniczony: piszemy do pamieci TYLKO gdy caly wzorzec pasuje (E9 na
  starcie, naglowek kodu zgodny z uchwytem metody - phdrMDesc == MethodHandle.Value, opis stosu dokladnie na +4, bajty
  za nakladka 00 00 00, niezerowy adres obslugi na +8); przy jakiejkolwiek niezgodnosci metoda jest POMIJANA i liczona
  w logu - nigdy nie psujemy metody o innym ukladzie, najwyzej jej nie naprawimy.
- **Prog 12 B** dla "malej metody" (siec i skan): w grze wszystkie 95 naszych metod z kodem JIT < 5 B mialy cialo IL
  <= 9 B; 12 B daje zapas, a i tak liczy sie realny warunek nachodzenia (offset opisu stosu == 4) sprawdzany per metoda.

## Ryzyko / co sprawdzic

- **Regresje:** p.1 (siec bez malych metod) zmniejsza liczbe metod pod siecia CrashScribe o ok. 95 - te metody
  (lambdy menu, SyncData) nie beda juz lapane finalizatorem CrashScribe, ale wyjatek z nich lapie siec metody wolajacej
  (lambdy sa wolane z naszych metod pod siecia), a SyncData nie rzuca w normalnej grze. Linia `Net (UW): ... male metody
  bez sieci: N` pokazuje, ile pominieto.
- **Naprawa:** pisze do zywego kodu. Ograniczenia bezpieczenstwa opisane wyzej (wzorzec albo pomijam). Zapis atomowy
  8 B - watek czytajacy instrukcje widzi stary albo nowy skok, oba poprawne i trafiaja w ten sam cel. Zdjecie latki
  przez MonoMod przywraca ZAPISANA kopie oryginalnych bajtow (sprawdzone w dekompilacji MonoMod.Core
  SimpleNativeDetour.Undo - pisze `DetourBackup` bez czytania biezacych bajtow), wiec nasza zmiana mu nie przeszkadza;
  ponowne latkowanie nadpisze nasz skok swiezym detourem, a straz 30 s / skan dobowy naprawi go znow.
- **Czego nie dalo sie sprawdzic:** nie wolno uruchomic gry (zajeta testem), wiec naprawy nie potwierdzono NA METODACH
  GRY w zywej kampanii - tylko mechanizm w uprzezy (ta sama 0Harmony.dll, ten sam CLR/OS/JIT, prawdziwy detour i GC).
  Pierwszy autotest/sesja musi pokazac linie `Opis stosu (UW)` z liczbami i zero bledow (szczegoly nizej).
- **12-bajtowy detour** (mov rax,imm64; jmp rax) - w skanie 19.10 NIE wystapil (wszystkie nasze i widziane cudze to
  `jmp rel32`); UnwindRepair obsluguje tylko E9 (inne pomija z powodem "nie E9"). Jesli pojawi sie w logu duzo "nie E9"
  wsrod malych metod - dopisac obsluge 12 B.

## Test w grze (co ma pokazac autotest 5 dob + bitwa, albo 160 dob)

- Linia (w menu, zaraz po "Net ready."): `Opis stosu (UW, menu): zalatanych metod ~16000, malych ~140, nachodzacych
  na opis stosu M, naprawionych teraz K, juz dobrych 0, pominietych P (powody)`. Oczekiwane: K > 0 (naprawione realne
  metody gry/BLSE/ButterLib), P male albo 0.
- Linia `Net (UW): siec modow - male metody bez sieci (IL <= 12 B): ~95 (Armoury .., RealisticCaptivity .., GrandTourney ..)`.
- Na starcie sesji: `Opis stosu (UW, sesja): ...`; raz na dobe `Opis stosu (UW, doba): ...` (juz dobrych rosnie,
  naprawionych teraz zwykle 0).
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
w takim kodzie - gdy GC trafi w ten moment, objaw jest identyczny. Obejscie Jeffa (MCM "Craft Result Popup" off) ma
sens jako doraznie, ale gdy UnwindRepair naprawi tez te lambde, CraftPopup powinien przestac byc grozny. Do
potwierdzenia dopiero w grze (linia `Opis stosu (UW)` policzy, czy ta lambda zostala naprawiona).

## Status: DO SPRAWDZENIA
Zbudowane (CrashScribe, Armoury - 0 Error). Mechanizm naprawy potwierdzony w uprzezy (A wisi, B/C/D nie). W grze
Jeffa jeszcze nie uruchomione.
