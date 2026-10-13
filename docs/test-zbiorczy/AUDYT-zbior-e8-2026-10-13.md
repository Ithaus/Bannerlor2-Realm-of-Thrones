# PELNY AUDYT zbior-e8 e60928a (12/13.10.2026, wf_512814c1-89f)

Liczby: {'wszystkich': 13, 'po_odsianiu': 12, 'sprawdzonych': 3, 'niesprawdzonych': 0}


## Znaleziska krytyczne i wazne (po niezaleznym sprawdzeniu)


### [latki] Siec CrashScribe: finalizer z parametrem __originalMethod na KAZDEJ metodzie Armoury / RC / GT kosztuje 0.07-1.6 us i alokacje na kazde wywolanie (takze bez wyjatku) - podatek na caly nowy kod etapow 5-8, niewidoczny w 'Profil doby' i 'Profil CS' (E8/0) (wazny) - CrashScribe/src/Net.cs:20

**Paczki:** CrashScribe Net (plik sprzed W33, poza diffem) x kazda nowa metoda paczek etapow 5-8 (Armoury 7112 -> 7834 metod pod siecia); E8/0 (pomiar tego nie widzi); E8/O1 i dalsze optymalizacje (mierza metody razem z siecia)

**Scenariusz:** Net.WrapModules (Net.cs:76-100, wolane z CrashScribe SubModuleMain.cs:90 w menu glownym) robi harmony.Patch(m, finalizer: ModFinalizer) (Net.cs:123) na kazdej metodzie bez akcesorow w zestawach z WrapModules = 'RealisticCaptivity,GrandTourney,Armoury' (Config.cs:13,23; zywy plik Modules\CrashScribe\ModuleData\CrashScribe.settings.xml:18,23 - wlaczone; log sesji CS 12.10: 'Net cast over Armoury: 7112 methods' o 15:34:02, 7834 o 22:39:16, samo zakladanie 11-12 s startu). ModFinalizer(Exception __exception, MethodBase __originalMethod) (Net.cs:20): Harmony przy KAZDYM wywolaniu zalatanej metody wylicza __originalMethod przez MethodBase.GetMethodFromHandle - nowy RuntimeMethodInfo + zamek + liniowe szukanie w pamieci metod typu - zanim finalizer sprawdzi '__exception == null'. Pomiar poza gra na tym samym 0Harmony 2.4.2 z gry, CLR 4 x64 (zrodla i exe: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\audyt\obszar3\inl\bench.cs, bench2.cs): klasa 20 metod 74-109 ns na wywolanie, 300 metod 155-405 ns, 1000 metod 266-1580 ns; 574 kolekcje GC gen0 na 15 mln wywolan; TEN SAM finalizer bez parametru __originalMethod: 0.7 ns i 0 kolekcji; bez latki 0.4 ns. Zgodne z logiem gry Modules\Armoury\Armoury-2026-10-12_15-33-44.log: 'SleepGate N x, sr. 0.4 us' (45 618 wywolan; logika to 2 odczyty slownika + jedno zagniezdzone wywolanie SleepKind), 'ceny sprzetu (PricePostfix) 544-1157 ms / 37-57 tys.' = 15-20 us na jedna cene przy dobie 12.4-13.4 s. Skutek: kazda mala metoda pomocnicza, lambda sortowania i kazdy prefiks / postfiks w goracej sciezce (ceny, pamiec polki, bramka SetMove, znaczniki SkillLedger M1-M22 + MEnd na ok. 100 metodach menedzera umiejetnosci, QuestPay8.Enter / Leave, XpFair) placi ten koszt i smieci GC; liczba wywolan na dobe nieznana (kilkaset tysiecy to pewne z licznikow logu: 158 tys. odczytow pamieci polki + 57 tys. cen na dobe; przy milionach to sekundy na dobe gry). Linia 'Profil CS (E8/0)' (Blame.cs:122-133) liczy tylko FirstChance i Blame.Culprits, 'Profil doby (E8/0)' nie wydziela sieci - Jeff chce szybszej gry, a ten koszt nie ma dzis zadnego pomiaru.

**Proponowana poprawka:** CrashScribe/src/Net.cs: ModFinalizer (linia 20) i WatchFinalizer (linia 37) bez parametru 'MethodBase __originalMethod'; metode do opisu brac dopiero w galezi z wyjatkiem (new StackTrace(__exception, false) - pierwsza ramka z zestawu moda, tak jak robi to Blame.Culprits, albo __exception.TargetSite) i podac ja do Describe(). Zachowanie (zapis bledu, polkniecie przy SwallowModErrors) bez zmian, koszt wywolania spada do ok. 0.3 ns. Przed decyzja o wgraniu: 3 doby autotestu z WrapModMethods=true i false (albo z poprawka) i porownanie 'doba N ms (zegar)' z linii 'Koszt 171-174' - to da udzial sieci w dobie. Zmiana dotyczy CrashScribe poza paczkami zbioru - decyzja prowadzacego; bez niej gra dziala tak samo, tylko wolniej.

**Sprawdzenie: potwierdzone**

Proba obalenia nieudana - mechanizm jest prawdziwy, powtorzony niezaleznym pomiarem, nie ma straznika i nie jest nigdzie opisany jako znany/odlozony. Niepewna zostaje tylko SKALA na dobe (ponizej).

1) Kod (zb\int, HEAD e60928a): CrashScribe\src\Net.cs:20 ModFinalizer(Exception __exception, MethodBase __originalMethod) i :37 WatchFinalizer - ten sam parametr; Net.cs:120-123 harmony.Patch(m, finalizer: fin) na kazdej metodzie przepuszczonej przez Patchable (:131-146 - odpadaja tylko get_/set_/add_/remove_, Finalize/GetHashCode/Equals/ToString, abstrakcyjne, generyczne i typy Log/Settings/McmSettings; lambdy i metody pomocnicze sa pod siecia). Wlaczone: Config.cs:13 WrapModMethods=true, :23 WrapModules="RealisticCaptivity,GrandTourney,Armoury"; zywy Modules\CrashScribe\ModuleData\CrashScribe.settings.xml:18,23 - to samo. Wolane z SubModuleMain.cs:90. Logi sesji CS 12.10: "Net cast over Armoury (Armoury): 7112 methods" (15:34:02, 11 s po RC), 7834 (22:39:16 i 23:35:42, 12 s). Moje liczenie Cecil-em na Armoury\bin\Release\Armoury.dll z drzewa: 7864 metody, 1038 typow; 15% metod w klasach >=100 metod (PeopleBook 293, TownFolk 179, NightRest 163), 43% w klasach 30-99.

2) Cudzy kod: 0Harmony 2.4.2 z gry (Modules\Bannerlord.Harmony), dekompilacja MethodCreatorTools.EmitCallParameter case InjectionType.OriginalMethod -> EmitOriginalBaseMethod: "ldtoken <metoda>; call MethodBase.GetMethodFromHandle" wstawiane przed KAZDYM wywolaniem finalizera (takze w galezi bez wyjatku, MethodCreator AddFinalizers(catchExceptions:false)). mscorlib 4.x (gra to .NET Framework - brak runtimeconfig, net472): RuntimeType.GetMethodBase -> RuntimeTypeCache.GetMethod -> MemberInfoCache.AddMethod: new RuntimeMethodInfo[1] + new RuntimeMethodInfo, potem Insert = Monitor.Enter na pamieci typu + MergeWithGlobalList (liniowe CacheEquals po wszystkich metodach typu). Czyli alokacja + zamek + szukanie liniowe na kazde wywolanie, zanim finalizer sprawdzi __exception == null.

3) Niezalezny pomiar (wlasny program, nie bench zglaszajacego; ten sam 0Harmony 2.4.2, CLR 4.0.30319 x64, latanie jak Net: GetMethods + Patch finalizer): bez latki 0.4 ns; finalizer BEZ __originalMethod 1.2-1.3 ns, 0 B, 0 kolekcji; finalizer Z __originalMethod: klasa 20 metod 74-109 ns, 100 metod 73-262 ns, 300 metod 164-392 ns, zawsze 240 B smieci na wywolanie i 114 kolekcji gen0 na 3 mln wywolan (1 na ok. 26 tys.). Liczby zglaszajacego potwierdzone.

4) Zgodnosc z gra: SleepGate.cs:208-264 - stoper obejmuje tylko SleepKind (zagniezdzona metoda NightRest pod siecia, klasa 163 metod) + 2-3 odczyty slownika, log daje "sr. 0.4 us" - pasuje do ok. 0.3 us samej sieci. Sciezka ceny (SupplyDemand.cs:437-525 + Factor :337, Demand :182-188, Stock :314, Prices :361; ArmsPricing.Multiplier :402, BaseOf :304, MaterialIndex :381; PricePrefix/PriceFinalizer, MarketGlut.PricePostfix, Cost174.Begin/EndDt, SellByCondition.*) to ok. 20 metod pod siecia na jedna cene; log 15-33-44: 39-42 tys. cen na dobe po ok. 10.6 us -> same ceny to ok. 0.8 mln wywolan = 0.1-0.3 s i ok. 200 MB smieci na dobe (doba 17-21 s), czyli 30-50% kosztu warstwy cen to siec. Bramka SetMove* (E7/O1): GateXxx + Gate (+ GateOn + SleepKind noca) = 2-4 oplaty na kazdy rozkaz ruchu AI.

5) Nie jest znane ani odlozone: docs\etap8\rozpoznanie-D-optymalizacja.md:41,156,183 wymienia w CrashScribe tylko FirstChance + Blame.Culprits; CHANGELOG 2807/2844 zna siec tylko jako pulapke wklejania (inlining), nie koszt wywolania; STAN-PRAC i PROJEKT-ETAP8 - brak. To nie decyzja Jeffa (Jeff chce sieci lapiacej bledy, nie parametru __originalMethod).

ZASTRZEZENIA: (a) liczby wywolan metod pod siecia na dobe nikt nie zmierzyl - pewne minimum to ok. 1-2% doby z samych cen; calosc (Armoury ma 5-6 s wlasnego czasu na dobe w "Profil doby") szacuje na kilka procent doby, ale to szacunek, nie pomiar; "Profil doby" pokazuje +3400-3700 kolekcji gen0 na dobe w calym procesie - udzial sieci nieznany. Waga "wazny" jest wiec uzasadniona warunkowo; rozstrzyga bieg A/B. (b) Plik Net.cs jest sprzed W33 i poza diffem - to koszt zastany, ktory etapy 5-8 powiekszaja (+722 metody, nowe gorace prefiksy), a nie regresja paczki. (c) PROPONOWANA POPRAWKA W ZGLOSZONYM KSZTALCIE JEST BLEDNA: sprawdzilem, ze ramka metody zalatanej to kopia dynamiczna (RTDynamicMethod "Typ.Metoda_Patch1", DeclaringType == null; w logach gry tak samo, np. session-2026-10-12_15-33-43.log:92 "..._Patch1"), wiec ani __exception.TargetSite, ani "pierwsza ramka z zestawu moda jak Blame.Culprits" (Blame.cs:97-99 - ModuleOf(null) = null) NIE wskaza metody Armoury - WHERE daloby "(nieznana metoda)", a czerwona notka "error in ?". Poprawna wersja ponizej.

Pliki pomiaru: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\audyt\latki-wer-net\b\bench.cs (netbench.exe) i b2\fix.cs (fixtest.exe).

**Poprawka ostateczna:** Wielkosc S, jeden plik: C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int\CrashScribe\src\Net.cs (w repo: CrashScribe/src/Net.cs).

CO ZMIENIC:
1) ModFinalizer (linia 20) i WatchFinalizer (linia 37): usunac parametr "MethodBase __originalMethod" - sygnatura zostaje (Exception __exception). Pierwsze dwie linie bez zmian (if (__exception == null) return null; if (_inside) return __exception;).
2) W galezi z wyjatkiem, BEZPOSREDNIO w ciele finalizera (nie w metodzie pomocniczej - ramka 1 musi byc kopia zastepcza Harmony): 
   MethodBase m = null;
   try { m = Harmony.GetOriginalMethodFromStackframe(new System.Diagnostics.StackFrame(1, false)); } catch { }
   i dalej jak dzis: Describe(m), Scribe.Report(...), Notify(m, __exception), zwrot wedlug SwallowModErrors (Watch: zawsze __exception).
   NIE uzywac __exception.TargetSite ani "pierwszej ramki moda ze sladu" (dla metod pod siecia DeclaringType == null - patrz uzasadnienie).
3) Opcjonalnie w Describe/Notify: gdy m.DeclaringType == null (mapowanie sie nie udalo), wypisac samo m.Name (nazwa kopii "Typ.Metoda_Patch1" zawiera typ) zamiast "?".
GetMethods w WrapModules/WrapAssembly/WrapHotspots, _inside, polykanie - bez zmian.

SPRAWDZONE poza gra (ten sam 0Harmony 2.4.2 i CLR 4 x64, b2\fixtest.exe): ten ksztalt podaje wlasciwa metode oryginalna dla metody statycznej, instancyjnej, metody struktury, lambdy, wyjatku rzuconego w BCL pod metoda moda, lancucha Outer->Mid->Inner oraz metody zalatanej drugi raz przez inny mod PO sieci; goraca sciezka 1.65 ns na wywolanie razem z petla, 0 kolekcji gen0 na 20 mln wywolan (dzis 75-400 ns i 240 B). Harmony.GetOriginalMethodFromStackframe jest w 0Harmony z gry (Harmony.cs:292); przed buildem sprawdzic, ze jest tez w libs/0Harmony.dll, z ktorym kompiluje sie CrashScribe (csproj :22).

RYZYKO DLA INNYCH SYSTEMOW:
- Tekst WHERE ("[Armoury] Armoury.Typ.Metoda") wychodzi identyczny, wiec klucz powtorek Scribe.Report (Scribe.cs:98 - rodzina zawiera where), Summary i narzedzie sprawdz_logi.py (T1 czyta raporty CS) bez zmian. Gdyby mapowanie w grze zawiodlo: gorszy opis w raporcie, ale zapis bledu i polkniecie dzialaja jak dzis (pelny slad stosu i tak jest w raporcie).
- Pulapka wklejania z CHANGELOG 2807/2844 (siec kompiluje kopie metod w menu glownym przed bramka SetMove*) - bez zmian, siec dalej zaklada sie w tym samym miejscu i na tych samych metodach.
- E8/0 "Profil CS" / "Profil doby" - bez zmian w kodzie; czasy w liniach spadna, bo metody byly mierzone razem z siecia. Porownania przed/po dla paczek E8/O1-O6 i progi ms w narzedziach robic na TEJ SAMEJ wersji CrashScribe (inaczej zysk sieci zostanie przypisany paczce optymalizacji).
- Zapis gry, ekonomia, MCM - nie dotyczy (zero stanu).
- CrashScribe jest poza paczkami zbioru: osobny wpis CHANGELOG, osobny build i autotest; decyzja prowadzacego, czy przed testem zbiorczym, czy po.

POMIAR SKALI (przed uznaniem wagi za ostateczna): 3 doby autotestu na tym samym zapisie z obecnym CrashScribe.dll i z probnym po poprawce; porownac "doba N ms (zegar)" z linii "Koszt 171-174", "GC 0/1/2" z "Profil doby (E8/0)" i "SleepGate sr. us" (powinno spasc z 0.4 do ok. 0.1). Nie mierzyc przez WrapModMethods=false: to wylacza tez polykanie bledow i zmienia moment kompilacji metod (pulapka wklejania), wiec porownanie nie bedzie czyste; poza tym plik ustawien w folderze gry jest dla agenta zablokowany.


### [ustawienia] Pulapka MCM: CampBattlePropsEnabled zapisany u Jeffa jako false - scenografia obozu (E7/O2) nie ruszy bez poprawki pliku przy wgraniu (wazny) - Armoury/src/Settings.cs:1228

**Paczki:** E7/O2 (229) - plik ustawien Jeffa

**Scenariusz:** Jedyna zmiana domyslnej od WGRANIA 33, ktora trafia na zapisany klucz. Settings.cs:1228 ma CampBattlePropsEnabled = true (przed: false). Plik C:\Users\GAME\Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json linia 302: "CampBattlePropsEnabled": false. McmSettings.Apply (SubModuleMain.cs:365, potem co godzine gry) nadpisuje domyslna wartoscia z pliku, wiec SubModuleMain.cs:341 nie dodaje zachowania misji CampScene: brak linii startowej (prog E7/O2 T1 = NIE) i brak namiotow przy napadzie na oboz. Druga zmieniona domyslna, WarExpectationEnabled true -> false (Settings.cs:520), dziala: klucza nie ma ani w Armoury.json, ani w Modules\Armoury\ModuleData\Armoury.settings.xml (75 starych kluczy). Rzecz jest opisana w CHANGELOG paczki E7/O2 jako krok wgrania - zglaszam, bo bez niego test zbiorczy pokaze falszywe NIE.

**Proponowana poprawka:** Przy wgraniu (gra zamknieta, kopia Armoury.json.bak-<data>): w Armoury.json linia 302 zmienic "CampBattlePropsEnabled": false na true (albo usunac te linie - wtedy dziala domyslna z kodu). Innych kluczy w pliku nie ruszac. Kodu nie zmieniac.

**Sprawdzenie: potwierdzone**

Fakty sie zgadzaja i nie ma straznika, ktory by temu zapobiegl. To jednak NIE jest blad kodu ani zderzenie paczek, tylko znany, niewykonany krok wgrania - waga realnie "krok listy wgrania", nie "wazny blad".

DOWODY (drzewo zb\int, HEAD e60928a):
- Armoury/src/Settings.cs:1228 CampBattlePropsEnabled = true; w noc/wgranie-33 bylo false (git diff).
- Plik Jeffa Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json linia 302: "CampBattlePropsEnabled": false (plik z 08.10 08:17, 335 kluczy).
- McmSettings.cs:6494-6497 Apply() kopiuje Instance do Settings.Current (:6191 s.CampBattlePropsEnabled = CampBattlePropsEnabled); wolane w SubModuleMain.cs:365, ArmouryBehavior.cs:1261 (OnSessionLaunched) i co godzine (:725-726).
- Skutek: CampScene.cs:75 StartLine wychodzi przed linia logu (wolane z ArmouryBehavior.cs:1288, PO Apply z :1261); SubModuleMain.cs:341-343 nie dodaje CampScene do misji; CampScene.cs:90 i tak by zgasil.
- Dowod z gry: wszystkie 5 dzisiejszych logow Armoury (15:33, 16:11, 22:38, 23:26, 23:35) maja 0 linii "CampScene", choc kod E7 w nich dziala (log 15:33 linia 249 "MapRoadsProbe: wylaczona" = E7/D0, pozniejsza paczka tej samej galezi). Autotest biegnie na pliku ustawien Jeffa.
- Prog istnieje: tools/sprawdz_logi.py:8973-8976 - brak linii = NIE, z tekstem "wpis Armoury.json Jeffa poprawiony przy wgraniu".
- To nie zamiar Jeffa: STAN-PRAC :846 "namioty i ogniska przy napadzie na oboz - WLACZYC" (09.10).

SPRAWDZENIE MECHANICZNE TWIERDZENIA "jedyna zmiana domyslnej trafiajaca na zapisany klucz": porownalem wszystkie pola Settings.cs (1177 w wgranie-33, 1295 w HEAD) z Armoury.json. Zmienione domyslne: tylko WarExpectationEnabled (true -> false, klucza w pliku NIE ma, nie ma go tez w Modules\Armoury\ModuleData\Armoury.settings.xml) i CampBattlePropsEnabled (false -> true, klucz JEST = false). Z 118 nowych pol zadne nie wystepuje w pliku. Pol usunietych 0. Twierdzenie prawdziwe.

DUPLIKAT / RZECZ ZNANA: opisane juz w CHANGELOG paczki E7/O2 ("przy wgraniu poprawic na true"), w liscie wgrania E7 w CHANGELOG, w STAN-PRAC :1285 i :1323 oraz w TRIAZ-zb58-120 rozdz. "E7/O2 T1 - brak linii startowej CampScene" (werdykt BYLO_W_33, "Tylko klucz", pole Poprawka puste). Czyli znalezione wczesniej, ale do dzis nie wykonane - plik nadal ma false.

CZEGO ZGLASZAJACY NIE ZAUWAZYL (istotne dla poprawki): klucz musi isc w parze z DLL. Stary CampScene z WGRANIA 33 (git show noc/wgranie-33:Armoury/src/CampScene.cs) przy kluczu true stawia obiekty Z FIZYKA (GameEntity.Instantiate) i ognisko NA pozycji gracza (Ring(m, center, 0f, 0f)) - dokladnie to, co E7/O2 naprawia. Autotest po biegu przywraca zatwierdzone DLL; jesli w pliku zostanie true, zatwierdzony build 33 dostaje wlaczona stara, wadliwa scenografie.

**Poprawka ostateczna:** Kodu nie zmieniac. Wielkosc S (jedna linia pliku ustawien, poza DLL).

CO ZROBIC: w C:\Users\GAME\Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json USUNAC linie 302 ("CampBattlePropsEnabled": false,) - zalecam usuniecie zamiast zmiany na true. Gra zamknieta, wczesniej kopia Armoury.json.bak-<data>. Innych kluczy nie ruszac (WorldPacePercent 75 zostaje). Sasiednie linie 301 i 303 maja przecinki, JSON zostaje poprawny.

DLACZEGO USUNIECIE: bez klucza dziala domyslna z tego DLL, ktory akurat jest wgrany - nowy DLL (E7/O2) = true, a po cofnieciu do WGRANIA 33 = false, wiec stara scenografia z fizyka i ogniskiem na graczu sama sie nie wlaczy. MCM nie przepisuje pliku przy wczytaniu (plik ma 335 kluczy i date 08.10 mimo dzisiejszych biegow; 960 pol bez klucza dziala z domyslnych), zapisuje go dopiero, gdy Jeff zmieni cos w MCM w grze. Wariant "zmienic na true" tez dziala, ale wtedy przy kazdym cofnieciu DLL do 33 trzeba przywrocic .bak.

KIEDY: PRZED uruchomieniem autotestu testu zbiorczego, nie dopiero przy trwalym wgraniu - autotest biegnie na pliku Jeffa, inaczej prog E7/O2 T1 znow pokaze falszywe NIE (jak w biegu zb58-120 i w 5 dzisiejszych logach).

RYZYKO DLA INNYCH SYSTEMOW: niskie.
- Klucz czytaja tylko SubModuleMain.cs:341 i CampScene.cs:75/:90; ekonomia i zapis nietkniete (brak stanu w zapisie).
- Po wlaczeniu pierwszy raz w grze wykona sie 12 wywolan GameEntity.PrefabExists przy starcie sesji: to gole wywolanie silnika po nazwie (TaleWorlds.Engine GameEntity.cs:423-425, bez sceny), kazde w try/catch (CampScene.cs:62-63), cale StartLine w try/catch (ArmouryBehavior.cs:1288).
- W misji dziala tylko przy napadzie na oboz gracza w bitwie polowej (NightRest.PlayerCamped) - autotest takich bitew nie gra, T2 zostaje testem Jeffa w nowej kampanii.
- Jesli zabezpieczenia zablokuja edycje pliku ustawien: Jeff przelacza "Camp Battle Props Enabled" w MCM (grupa "A night's rest"); linia startowa pojawi sie dopiero przy nastepnym starcie sesji, wiec dla autotestu to nie wystarczy - w raporcie progu T1 NIE czytac wtedy jako "tylko klucz".


### [ustawienia] Wylacznik LootIsTransfer nie wraca do stanu sprzed: przy wylaczonym uprzaz rozbitej partii powstaje drugi raz (kopia DTE + przeniesienie E8/H) (wazny) - Armoury/src/HorseFate.cs:906

**Paczki:** E8/L (LootIsTransfer) x E8/H (RiderNeedsHarness)

**Scenariusz:** Stan: LootIsTransfer = false (wylacznik E8/L), RiderNeedsHarness = true, HorsesDieOnlyKilled = true (obie domyslne). Partia lorda z uprzeza w zbrojowni zostaje rozbita w bitwie. (1) Okno DTE: HorseFate.cs:405 rest = TransferOn = false, wiec LootSnap/ArmsOf (HorseFate.cs:655) sledzi tylko konie; DTE DistributeLootRandomly kopiuje cala zbrojownie pokonanych zwyciezcom (dekompilacja EveryoneCampaignBehavior.cs:617-640, GetAllLootItems :684-708 - bez zdejmowania u pokonanego), a LootMove zdejmuje u pokonanego same konie - uprzaz zostaje. (2) Przy zniszczeniu partii HorseFate.Spoils: withHarness = SaddleLaw.KeyOn (HorseFate.cs:906, bez warunku TransferOn) - cala uprzaz zbrojowni idzie jeszcze raz do taboru zwyciezcow albo na polke (:907-927). Skutek: kazda sztuka uprzezy rozbitej partii x2. Przed E8/H zbrojownia znikala z partia (DTE OnMobilePartyDestroyed :435), wiec kopia DTE byla przeniesieniem; dla koni jest juz analogiczny straznik (:903, !DieOn -> arm = null). Przy domyslnych (LootIsTransfer true) problemu nie ma - LootMove zdejmuje uprzaz przed Spoils. Kod przeczytany, nie uruchamiany.

**Proponowana poprawka:** HorseFate.cs:906: bool withHarness = SaddleLaw.KeyOn && (!fromBattle || TransferOn); - uprzaz ze zbrojowni w Spoils tylko wtedy, gdy okno DTE tej bitwy zdjelo juz kopie (albo gdy to nie lup bitwy). Przy LootIsTransfer off uprzaz znika wtedy z partia jak przed E8/H.

**Sprawdzenie: potwierdzone**

Blad jest prawdziwy w kodzie, ale wystepuje TYLKO przy recznie wylaczonym LootIsTransfer (przy domyslnych nie ma go wcale). Nie udalo sie go obalic - brak straznika.

Przesledzone krok po kroku (drzewo zb/int, HEAD e60928a):
1. Armoury/src/HorseFate.cs:405 - "bool die = DieOn, rest = TransferOn;" -> przy LootIsTransfer=false i HorsesDieOnlyKilled=true idzie LootSnap(me, true, false) (:411).
2. ArmsOf (:638-657, warunek :655 "IsHorse(kv.Key) ? horses : rest") przy rest=false widzi same konie, wiec LootMove (:682-726) liczy przyrost u zwyciezcow i zdejmuje u pokonanych wylacznie konie. Uprzaz zostaje w zbrojowni pokonanego.
3. DTE (dekompilacja scratchpad/dte/Everyone.cs): OnMapEventEnded :529-563 -> DistributeLootRandomly :751-826 -> GetAllLootItems :828-853 bierze slownik SanitizePartyArmory pokonanego i dopisuje wszystko zwyciezcom (AddItemToPartyArmory / ArmyArmory.AddItemToArmory) BEZ zdejmowania u pokonanego. ItemBlackList.Test filtruje po id/nazwie, nie po typie - uprzaz przechodzi.
4. Gra (dec32/cs_full/TaleWorlds.CampaignSystem.MapEvents/MapEvent.cs:2079 OnMapEventEnded, potem :2158 HandleMapEventEnd -> MapEventSide.cs:445 DestroyPartyAction.Apply(leaderParty, party)) - partia rozbita jest niszczona PO oknie DTE, z niszczycielem != null.
5. Nasluchy MobilePartyDestroyed ida od ostatnio dopisanego (MbEvent.cs:176-182), wiec GoodsLedger.OnPartyDestroyed (GoodsLedger.cs:805 -> HorseFate.Spoils) biegnie przed DTE OnMobilePartyDestroyed (Everyone.cs:510-522, PartyArmories.Remove). Dowod z biegu: log Armoury-2026-10-12_15-33-44.log ma 12 dob z "w tym konie zbrojowni rozbitych" > 0 (np. 95, 60, 32) - Spoils realnie widzi zbrojownie DTE.
6. HorseFate.Spoils: :867 fromBattle = true; :903 arm = ArmOf(mp) (bo DieOn=true i nie SimWin); :906 "bool withHarness = SaddleLaw.KeyOn;" - bez warunku TransferOn; :909 bierze kazda uprzaz ze zbrojowni; :920-927 przenosi ja do taboru zwyciezcow albo na polke.
Skutek: uprzaz rozbitej partii jest u zwyciezcow dwa razy - kopia w zbrojowni DTE (krok 3) i sztuka w taborze / na polce (krok 6). Sztuki z niczego, kilkadziesiat na rozbita partie jazdy (E8/H: lord AI kupuje siodlo kazdemu jezdzcy).

Dlaczego to regresja wylacznika, a nie zamierzony skutek: w bazie noc/wgranie-33 okno DTE sledzilo tylko konie, a Spoils uprzezy nie ruszal - kopia DTE + skasowanie zbrojowni z partia dawalo przeniesienie (x1). Obie linie sa nowe w diffie (:405 z E8/L, :906 z E8/H). Opis klucza w Settings.cs:265 mowi "off = as before: the survivors keep a copy" - dotyczy ocalalych, nie rozbitych. CHANGELOG E8/H (wpis 271, linia ok. 142) twierdzi "uprzaz zbrojowni rozbitej partii przenosi tylko Spoils, raz" - prawda tylko przy LootIsTransfer wlaczonym; zderzenie nie jest tam opisane jako znane ani odlozone. Dla koni analogiczny straznik juz jest (:903).

Zastrzezenia co do wagi: przy domyslnych (LootIsTransfer=true) LootMove zdejmuje uprzaz u pokonanego przed Spoils - brak podwojenia (sprawdzone w kodzie). W pliku MCM Jeffa (Armoury.json z 08.10) nie ma kluczy LootIsTransfer / RiderNeedsHarness / HorsesDieOnlyKilled, wiec dzialaja domyslne - test zbiorczy i nowa kampania Jeffa NIE sa dotkniete. Waga "wazny" trzyma sie tylko z definicji "wylacznik nie wraca do stanu sprzed"; praktycznie dolna granica tej wagi. Kod przeczytany, nie uruchamiany.

**Poprawka ostateczna:** Wielkosc S, jedna linia + komentarz.

Plik: Armoury/src/HorseFate.cs, metoda Spoils, linia 906:
  bylo:  bool withHarness = SaddleLaw.KeyOn;
  ma byc: bool withHarness = SaddleLaw.KeyOn && (!fromBattle || TransferOn);
Komentarz nad linia dopisac: przy LootIsTransfer wylaczonym DTE skopiowal uprzaz zwyciezcom, a LootMove jej nie zdjal - bez drugiej sztuki (zniknie ze zbrojownia jak przed E8/H); ten sam wzor co straznik koni w :903.
Dodatkowo poprawic zdanie w CHANGELOG wpisu E8/H (271): "uprzaz zbrojowni rozbitej partii przenosi tylko Spoils, raz" -> przy LootIsTransfer wl. przenosi ja okno DTE (LootMove), Spoils tylko reszte; przy wyl. - kopia DTE, Spoils uprzezy z bitwy nie rusza.

Macierz po poprawce (fromBattle = true):
- HorsesDieOnlyKilled tak, LootIsTransfer tak (domyslne): wyrazenie = KeyOn, zachowanie identyczne jak teraz.
- HorsesDieOnlyKilled tak, LootIsTransfer nie: Spoils bierze tylko konie; uprzaz x1 (kopia DTE, oryginal znika z partia) - stan sprzed E8/H.
- HorsesDieOnlyKilled nie albo SimWin: arm = null juz teraz (:903), bez zmiany.
fromBattle = false (rozwiazanie partii, niszczyciel bez bitwy): bez zmiany - uprzaz dalej idzie na polke / do niszczyciela.

Ryzyko dla innych paczek: przy domyslnych zerowe (warunek zawsze prawdziwy). Przy wylaczonym LootIsTransfer zostaje maly ubytek: uprzaz, ktorej DTE w tej bitwie nie skopiowal (pozycja z czarnej listy DTE albo bitwa bez rozdania lupu DTE), zniknie z partia zamiast isc do zwyciezcow - dokladnie jak przed E8/H, tylko w stanie wylaczonym. Licznik SaddleLaw.NoteSpoil ("dobytek rozbitych: uprzaz zbrojowni") w tym stanie pokaze mniej; tools/sprawdz_logi.py:12149-12152 tylko sumuje te liczbe bez progu - narzedzia nie trzeba ruszac. E8/L (liczniki _dTr*, linia "Lup i dary"), E8/P2 (pomiar kopii), E8/O1 (ShelfBuy) i gen_mcm - nietkniete. Brak zmian zapisu i ustawien.

Odrzucona alternatywa: sledzic uprzaz w oknie DTE razem z konmi takze przy LootIsTransfer wylaczonym - zmienialoby znaczenie wylacznika dla ocalalych pokonanych (opis "off = survivors keep a copy") i liczniki E8/L; wieksza zmiana bez potrzeby.

**Powiazane:**  | tez zderzenia: Wylacznik LootIsTransfer=false przy wlaczonym RiderNeedsHarness: uprzaz rozbitej partii liczona dwa razy (kopia DTE u zwyciezcow + przeniesienie E8/H w HorseFate.Spoils)

## Drobne


### [zloto] Kopalnia BK w rekach gracza wytapia lupy bez drewna i bez zaplaty za drewno (regula dymarki E6/R1 jest tylko na sciezce notabla) - Armoury/src/WorkshopLaw.cs:1535

Nowa kampania (Bloomery.On), gracz kupuje kopalnie BK w miescie (linia bez wsadu, wyrob kategorii iron). Gra rozdziela cykl po wlascicielu: gracz -> TickOneProductionCycleForPlayerWorkshop, notabl -> ...ForNotableWorkshop (dekompilacja gry WorkshopsCampaignBehavior.cs:1406). U notabla WorkshopLaw.CyclePrefix wola Bloomery.TownMineBlocked (WorkshopLaw.cs:236): brak drewna na polce albo kapitalu = cykl nie idzie, a po udanym cyklu Bloomery.CyclePostfix zdejmuje WoodPerBloom ladunkow drewna z polki i przelewa cene z kapitalu warsztatu do kasy miasta (Bloomery.cs:956-996). U gracza PlayerCyclePrefix (WorkshopLaw.cs:1535-1556) ma po E8/B tylko BulkLineSkip (:1545) i ToolsByUse (:1548) - TownMineBlocked nie ma, a postfiks dymarki jest wpiety wylacznie na metode notabla (Bloomery.cs:1118-1122). Skutek: kazda lupa kopalni gracza powstaje bez spalonego drewna (ok. 5 ladunkow na lupe), warsztat nie placi miastu za drewno, ksiegi drewna (OreLedger.NoteBloomeryWood, ramka FBloom) tego nie widza; cena lupy zawiera wegiel dymarki, ktorego nikt nie kupil. Dwie reguly gracz/AI dla tego samego zjawiska. Skala mala (jeden warsztat gracza, kilkanascie-kilkadziesiat zl na lupe).

**Poprawka:** WorkshopLaw.PlayerCyclePrefix: po linii BulkLineSkip dopisac 'if (Bloomery.TownMineBlocked(__0, __1)) { __result = false; return false; }' (ten sam warunek co u notabla; niezaleznie od PlayerWorkshopsSameRule albo pod nim - do decyzji). Bloomery.ApplyAll: postfiks CyclePostfix wpiac takze na WorkshopsCampaignBehavior.TickOneProductionCycleForPlayerWorkshop (ta sama sygnatura production, workshop, effectCapital -> bool) - bez tego przy _cycleWired = true TownMineBlocked tylko rezerwuje (_pw) i drewno nigdy nie zejdzie. Linia 'kopalnie BK w miastach' policzy wtedy takze kopalnie gracza.


### [sztuki] Ksiega towarow liczy lup z rabunku (E7/R1) jako produkcje 'zboze ze spichlerza' - tymczasowy roster got[] stoi wewnatrz ramki FRaid - Armoury/src/DistrictRaid.cs:542

Stan wejsciowy: AI konczy krok rabunku, w magazynie wsi lezy towar z ksiegi (sol, glina, drewno, lupa...). DistrictRaid.Loot otwiera ramke GoodsLedger.Begin(FRaid, st) (DistrictRaid.cs:532) i w niej robi TRZY AddToCounts na sztuke: roster wsi -n (:538), tabor napastnika +fit (:542 / :552) oraz tymczasowy roster got[p] +fit (:542 / :552, new ItemRoster z :527 - lista dla OnItemsLooted). GoodsLedger.Tap (GoodsLedger.cs:314-336) jest postfiksem na KAZDYM ItemRoster.AddToCounts w otwartej ramce i nie pyta, czyj to roster (f.Rec: Net[i] += n), wiec Net = -n + 2 x fit zamiast -(n - fit). Ramka FRaid (GoodsLedger.cs:511-512) ksieguje Net > 0 jako ZRODLO 'zboze ze spichlerza okregu dla napastnikow', Net < 0 jako 'spalone'. Skutek: kazda zabrana sztuka towaru to falszywa produkcja +fit i 'bez wyjasnienia' -fit; spalone jest zanizone o fit (przy fit = n/2 znika calkiem). Dowod z biegu 120 dob (Armoury-2026-10-12_15-33-44.log): linia 4822 'Towary: dzien 108850 - salt ... rabunek osada po osadzie (E7/R1) 1 (zboze ze spichlerza okregu dla napastnikow 1) ... bez wyjasnienia -1' przy linii rabunku tej doby 'towar z magazynu 1 szt., spalone 0'; linia 16999 'dzien 108889 - clay ... (zboze ze spichlerza ... 5) ... bez wyjasnienia' i limestone 2 przy 'towar z magazynu 8 szt.'; to samo iron, hardwood, fish, grape, hog (124 linie Towary z ta pozycja, 0 linii 'spalone w rabunku'). Sol, glina i lupy 'ze spichlerza' to artefakt, nie towar. Sztuki sie zgadzaja (bilans v2 linii rabunku = 0) - blad jest tylko w ksiedze, ale test zbiorczy dostaje falszywa produkcje i falszywa 'reszte', a przy wiekszych rabunkach (gracz E7/R2, pelne magazyny) liczby urosna. Ten sam podsluch dopisuje fit do f.ArmsNet (uzbrojenie w magazynie wsi) - szum w przeciekach 174.

**Poprawka:** Armoury/src/DistrictRaid.cs, Loot: nie dopisywac do got[p] wewnatrz ramki. W petli (:535-562) zbierac ruchy do zwyklej listy (p, EquipmentElement, ile), a rostery got[p] zbudowac dopiero PO 'finally { GoodsLedger.End(gf); }' (:564), tuz przed OnItemsLooted (:570-575). Wtedy Net ramki = -n + fit = -(spalone) i etykieta 'zboze ze spichlerza' zostaje tylko dla Eat (:443-445), gdzie jest prawdziwa. Zmiana nie rusza sztuk ani zlota - tylko ksiege.


### [latki] Karawany (E8/G1): 'po otwarciu: tiki BK' liczy takze przebiegi, w ktorych cialo tiku BK nie pobieglo (kontrakt 174 blokuje go prefiksem) - licznik nie dowodzi, ze BK decyduje - Armoury/src/CaravanFlow.cs:164

Karawana AI z kontraktem surowca (MaterialOrders._byCar) stoi w miescie poza swoja godzina doby: BKROT Prefix zwraca false, CaravanFlow.ThrottlePostfix (CaravanFlow.cs:127-151) zmienia wynik na true (_dOpened++, _fParty = karawana); nastepny prefiks MaterialOrders.HourlyPrefix (MaterialOrders.cs:956-965) zwraca false - cialo BK nie biegnie; postfiks Harmony biegnie zawsze, wiec BkHourlyPostfix (CaravanFlow.cs:155-169) dolicza _dRan++ i czas pustego przebiegu. Linia doby (CaravanFlow.cs:449) i odczyt narzedzia (tools/sprawdz_logi.py:11577) pokazuja 'tiki BK' = 'otwarte' takze wtedy, gdy BK niczego nie zdecydowal; 'koszt dodatkowych tikow' zawiera przebiegi bez ciala BK. Dzialanie gry bez wplywu - sam pomiar i prog G1.

**Poprawka:** CaravanFlow.BkHourlyPostfix: dopisac parametr 'bool __runOriginal' (Harmony 2.4.2) i liczyc _dRan / _dOrders / czas tylko przy true; przy false osobny licznik w linii doby ('zablokowane kontraktem 174').


### [zapis] Stan paczek E8/H (SaddleLaw) i E8/Q (CrossingLaw AI) czyszczony tylko pod kluczem E8/S - przy StateHygieneEnabled = false nie ma zadnego Reset - Armoury/src/StateHygiene.cs:44

StateHygiene.ResetAll konczy sie na `if (!_wasOn) return;` (StateHygiene.cs:43-44) PRZED krokami Stables.Reset / CrossingLaw.Reset (:49-50). SaddleLaw.Reset ma jednego wolajacego - Stables.Reset (Stables.cs:803), a Stables.Reset jednego - StateHygiene (grep). Stan wejsciowy: Jeff wylacza w MCM StateHygieneEnabled (opis klucza: 'off = wszystko jak przed E8/S'), gra kampanie A, wychodzi do menu, startuje nowa / wczytuje inny zapis w tej samej sesji. Skutek: pamiec E8/H (SaddleLaw.cs:55-57 _itemKind/_troopKind z ItemObject/CharacterObject kampanii A, :283 _lastSale 'partia -> doba sprzedazy', _errSites, _stumbles) i E8/Q (CrossingLaw.cs:203-204 _aiOutside/_dayTurns z MobileParty kampanii A, _stamp) zostaje do konca procesu. To stan NOWYCH paczek, nie 'stan sprzed E8/S'. Falszywych trafien nie ma (MBObjectBase nadpisuje tylko GetHashCode, Equals = referencja - sprawdzone ilspycmd TaleWorlds.ObjectSystem.MBObjectBase :79-82; Game tworzy nowy MBObjectManager na kazda gre - TaleWorlds.Core.Game :160-175, :255), wiec skutek to trzymanie w pamieci obiektow starej kampanii i licznik H-6b 'kupno po sprzedazy' liczony z dob innej kampanii (SaddleLaw.cs:642). Przy domyslnym kluczu (true) problemu nie ma.

**Poprawka:** Armoury/src/WorldMeasure.cs (Reset, obok CaravanFlow.Reset) albo konstruktor: wolac SaddleLaw.Reset() bezwarunkowo i zdjac je z Stables.Reset; w CrossingLaw wydzielic czesc E8/Q (_aiOutside, _aiDrop, _dayTurns, _errSites, _stamp, DayReset) do osobnej metody wolanej bezwarunkowo, pod kluczem zostawic tylko pola sprzed E8 (_lastSafe, _haveSafe, _aiCooldown).


### [zapis] Flaga NOWA KAMPANIA dymarek (arm_e6_bloom 'nk') stawiana niezaleznie od klucza - wlaczenie Bloomery At Mine w trakcie kampanii zamienia stara rude w lupy - Armoury/src/Bloomery.cs:156

OnNewGame ustawia NewCampaign = true zawsze (Bloomery.cs:156, nasluch ArmouryBehavior.cs:685), Export pisze 'nk' gdy NewCampaign (:228), a o lupie decyduje CanApply = klucz && NewCampaign && latki (:167-171) liczone przy KAZDYM starcie sesji (HistoricalPrices.cs:442, dodatkowo HistBulkUnitFactor > 1.01). Stan wejsciowy: nowa kampania zaczeta z BloomeryAtMine = false (albo z wylaczonymi cenami historycznymi / HistBulkUnitFactor = 1) - zapis i tak niesie 'nk'; po N dobach klucz zostaje wlaczony w MCM. Skutek: od nastepnego wczytania CanApply = true i kazda istniejaca sztuka 'iron' (ladunki rudy 100 kg na polkach, w magazynach wsi, taborach, wozach kupcow, zapas startowy) staje sie lupa 15 kg w cenie lupy, bez spalenia 5 ladunkow drewna na sztuke - dokladnie to, przed czym flaga miala chronic ('stara ruda stalaby sie lupami', Bloomery.cs:18-19). W druga strone (klucz zdjety w kampanii z lupami) lupy staja sie ladunkami rudy; _pending i wozy z drewnem zostaja zamrozone w zapisie (Export :230 oddaje napis bez zmian - nic nie ginie). Przy ustawieniach domyslnych (klucz true od startu nowej kampanii Jeffa) nie wystapi.

**Poprawka:** Bloomery.cs: zapamietac w zapisie, czy rezim lupy faktycznie wszedl w pierwszej sesji kampanii (np. 'nk1' po MarkApplied w nowej kampanii, 'nk0' gdy CanApply bylo false przy pierwszym OnSessionLaunched); CanApply przy wczytaniu wymaga znacznika 'nk1' (Import :261-266 czyta oba; sam 'nk' ze starszego zapisu tej galezi = jak dotad). Zmiana klucza w trakcie kampanii - linia w logu, ze zadziala od nowej kampanii.


### [ustawienia] Opisy MCM po polsku: 4 stare klucze Take Apart (regula: wszystko po angielsku); 1 klucz GrandTourney bez opisu - Armoury/src/Settings.cs:1314

Settings.cs:1314-1317: TakeApartEnabled, TakeApartBaseChance, TakeApartSkillSpan, TakeApartSalvage maja komentarze po polsku (np. "rozlozenie gotowej rzeczy na czesci, zeby zdjac z niej wzor"), a gen_mcm przepisuje je jako HintText (McmSettings.cs:4203-4211) - Jeff widzi je w MCM. GrandTourney/src/Settings.cs:17 MinLordsToHold nie ma zadnego opisu. Wszystkie 118 kluczy dodanych od WGRANIA 33 ma opisy po angielsku - to nie jest regresja tego zbioru.

**Poprawka:** Przetlumaczyc 4 komentarze w Armoury/src/Settings.cs:1314-1317, dopisac opis w GrandTourney/src/Settings.cs:17, uruchomic python tools/gen_mcm.py.


### [zderzenia] Nowa partia lorda w dobach 1-3 nowej kampanii dostaje konie dwa razy: dorobek startowy E8/T (z niczego, dla kazdego jezdzca) i zaraz potem zakup koni z polki siedziby (E6/K1 SeatGifts) za zloto pana - Armoury/src/HorseFate.cs:1151

Nowa kampania, doby 1-3, klucze domyslne (ColdStartLateLordParties = true, NewPartyHorsesFromSeat = true, TroopsFightWithOwnKitOnly = true). Gra tworzy partie lorda: HeroSpawnCampaignBehavior.cs:194 SpawnLordParty -> zdarzenie OnMobilePartyCreated -> LevyGold.PartyCreatedPrefix (LevyGold.cs:183) -> ColdStart.LateLord (ColdStart.cs:318-337) -> FillToTemplate: przy OwnKitOn sloty 10-11, czyli kon i rzad KAZDEGO jezdzca do zbrojowni DTE (ColdStart.cs:93-126). Zaraz potem gra wola GiveInitialItemsToParty (HeroSpawnCampaignBehavior.cs:197) -> HorseFate.GiftPrefix -> SeatGifts (HorseFate.cs:1151-1223): liczba koni z formuly gry (x7 x produkcja wsi w zasiegu x ludzie/200), bez patrzenia na zbrojownie, kupiona z polki siedziby rodu za zloto pana (GiveGoldAction :1218, GearSpent :1219) do taboru. Skala z biegu 120 dob (Armoury-2026-10-12_15-33-44.log, pierwsza linia 'Konie - smierc, lup, dobytek (E6/K1): dzien 108837'): 'dary startowe (partii 237): z polki konie 639 ... brak koni 769 ... zloto do siedzib 728213'. Po scaleniu te same partie maja juz konie z dorobku, wiec 639 koni i wieksza czesc 728 tys. zl pierwszej doby to zakup ponad potrzebe: polki koni siedzib sa wymiatane ('brak koni 769') w dobie, w ktorej Stajnia AI i awanse K3 szukaja tam koni, a przydzial sprzetu rodu schodzi na konie luzem (zostaja w taborze jako zapas K2; nadmiar ponad 1.5 x zapas wraca na polke ze strata na marzy). Zloto i konie nie powstaja ani nie znikaja - to podwojne zaopatrzenie jednego zjawiska, nie dziura w ekonomii.

**Poprawka:** Armoury/src/HorseFate.cs SeatGifts (:1151): w oknie dorobku E8/T pominac czesc 'konie' (zywnosc zostaje) dla partii, ktora wlasnie dostala dorobek - np. ColdStart wystawia 'internal static bool GotLateKit(MobileParty)' (zbior partii z LateLord = true tej doby, czyszczony w LateFlush) i SeatGifts przy IsHorse(kv.Key) robi continue; albo ogolniej: potrzebe koni z formuly gry pomniejszyc o konie zbrojowni ponad jezdzcow (Stables.Balance Free). Ewentualnie tylko odnotowac w STAN-PRAC jako swiadomy zapas startowy.


### [wydajnosc] E7/W4: licznik '3 potkniecia z rzedu' warstwy napisow jest zerowany co klatke - wylacznik napisow nie zadziala przy stalym wyjatku w LabelTick - Armoury/src/MapVillageLook.cs:535

Stan: warstwa napisow stoi i w kadrze jest co najmniej 1 napis (_lblCount > 0), a LabelTick zaczyna rzucac wyjatek przy kazdym wywolaniu (4 razy na sekunde, przed linia '_lblCount = n'). LabelTick -> catch -> LabelStumble (MapVillageLook.cs:470, :621-630) podnosi _lblStrikes o 1. Ale MapVillagesView.OnVisualTick wola LabelFrame w KAZDEJ klatce, gdy _lblCount > 0 (MapVillagesView.cs:1773), a udany LabelFrame robi '_lblStrikes = 0' (MapVillageLook.cs:535). Miedzy dwoma tickami (0.25 s) jest kilkanascie-kilkadziesiat klatek, wiec licznik nigdy nie dojdzie do LabelStrikesMax = 3 (:626) i '_lblBroken' nie zostanie ustawione. Skutek: zamiast 'napisy wylaczone do nastepnego wczytania mapy' wyjatek leci 4 razy na sekunde do konca sesji mapy (log dlawiony: 3 pierwsze i co 500.), kazdy przechodzi tez przez FirstChance CrashScribe. Dzis nie znam wyzwalacza takiego wyjatku - to wada samej siatki bezpieczenstwa, nie czynny blad.

**Poprawka:** MapVillageLook.cs: zerowac _lblStrikes tylko po udanym LabelTick (na koncu bloku try w LabelTick, po '_lblCount = n'), a w LabelFrame nie zerowac (albo miec dwa osobne liczniki: tick i klatka). Jedna linia, bez zmiany dzialania w zwyklej grze.


### [gracz] Praca najemna (RC): po wczytaniu zapisu zrobionego w menu pracy we WSI kazde wyjscie z pracy - takze nowe automatyczne StopForPurse z E8/RC - przelacza na menu "town"; init menu miasta na wsi rzuca NullReference - RealisticCaptivity/src/Work.cs:144

Stan wejsciowy: gracz najmuje sie do pracy we wsi (menu rc_work_labour), gra robi zapis (np. autozapis w trakcie kilkudniowej pracy), potem zapis jest wczytany w nowym uruchomieniu gry albo po tym, jak ostatnia praca w tej sesji byla w miescie. Work.cs:29 `_cameFrom = "town"` jest polem statycznym bez zapisu i bez odtworzenia (ustawiane tylko w opcjach startu :243 i :249; WorkInit :343-354 go nie rusza). Menu oczekiwania wraca z zapisu (gra trzyma GameMenuId - dekompilacja PlayerEncounter.cs:1026), a oczekiwanie rusza samo (GameMenu.cs:158-166 -> RunWaitMenuCondition -> StartWait). Skutek: przy pierwszym wyjsciu z pracy - reczne "Enough of this toil" (:383), alarm StillSafe (:484) albo NOWE z E8/RC: wies bez nadwyzki na dniowke -> StopForPurse (:144, wolane z LabourTick :410) - idzie GameMenu.SwitchToMenu("town") przy Settlement.CurrentSettlement = wies. Init menu miasta (dekompilacja PlayerTownVisitCampaignBehavior.cs:670-689) wola UpdateMenuLocations("town") (:698-702, lokacje center/arena/house_x, ktorych wies nie ma) i `Settlement.CurrentSettlement.Town.GarrisonParty` (:679) - Town wsi jest null -> NullReferenceException w srodku przelaczania menu. Wyjatek lapie try w LabourTick (:413), ale kontekst menu jest juz przelaczony na "town" z niedokonczonym initem: gracz we wsi zostaje w menu miasta (opcje miasta liczone dla wsi), bez czystego wyjscia. Przed E8/RC trzeba bylo samemu kliknac stop albo trafic na alarm; po E8/RC wystarczy, ze wies nie ma nadwyzki - dzieje sie samo po pierwszej dobie.

**Poprawka:** RealisticCaptivity/src/Work.cs: nie ufac polu statycznemu - we wszystkich czterech miejscach przelaczenia (:144 StopForPurse, :383 LabourTick, :420 GuardTick, :484 StillSafe) brac menu powrotu z osady, w ktorej gracz stoi: np. `string back = Here != null && Here.IsVillage ? "village" : "town";` (albo ustawic _cameFrom tak samo w WorkInit :343, ktory biegnie takze po wczytaniu). Bez zmian w zapisie.


## Sprawdzone bez uwag


- zloto: Obszar ZLOTO, drzewo zb/int HEAD e60928a, sam odczyt. Jedno znalezisko drobne (wyzej); krytycznych i waznych problemow zlota nie znalazlem.

Metoda: wszystkie dopisane linie ruszajace zloto w diffie noc/wgranie-33..HEAD (GiveGoldAction, ChangeHeroGold, ChangeGold, PartyTradeGold, KingdomBudgetWallet, TradeTaxAccumulated, MenPurse.Take/Add) wypisane skryptem i przeczytane w kontekscie: platnik, odbiorca, rowna kwota po obu stronach, obciecie do stanu kiesy. Dekompilacja gry: GiveGoldAction (ApplyForSettlementToCharacter nie tnie do kasy), SettlementComponent.ChangeGold (tnie do 0), Hero.Gold (t


- sztuki: Przejrzane w kodzie drzewa zb/int (HEAD e60928a), z dekompilacja gry tam, gdzie zmiana na niej stoi, i z logiem biegu 120 dob. Poza jednym bledem ksiegi (wyzej) nie znalazlem sztuki, konia ani czlowieka z niczego albo w nicosc, ktorych paczki same nie opisaly.

R1 + F4a/F4b (Bloomery.cs caly, VillageWoodlot, OreLedger, MaterialLaw.ProdPostfix, WorkshopLaw): drewno las okregu -> dymarka, polka -> dymarka, polka -> woz -> dymarka albo zawrocenie na polke zrodla ze zwrotem; kazde zdjecie ma dopisanie tej samej liczby. Zapis sekcji kopalni / @w / @b i ResolvePending bez dziur, cel znikl = CartBack


- latki: ZAKRES: wszystkie latki dodane / zmienione od W33 w Armoury (pliki nowe: BkDipGold, Bloomery, CaravanFlow, CraftOrders8, DistrictRaid, HarvestValve, PlayerFees8, PlayerRecruit, QuestPay8, SaddleLaw, SkillLedger, SleepGate, SmallGold, Stage8World, WorldTime7, XpFair, GiverPay, FastCall, MercFeast; zmienione: AmmoRecovery, BattlefieldLaw, SpoilsSeal, SpoilsCompany, RealPayer, WorkshopTrade, TownFolk.Live, GarrisonKit, HistoricalPrices), CrashScribe E8/0 i E8/M, GT / RC F7 (refleksja). Latek w krytycznym ani waznym stanie w samej warstwie Harmony NIE znalazlem.

SYGNATURY (dekompilacja 1.4.8: gra


- zapis: Obszar 4 przejrzany w kodzie drzewa int (HEAD e60928a), w dekompilacji gry i na logach; problemow krytycznych ani waznych nie znalazlem, dwa drobne wyzej. Skrypty i wyniki: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\audyt\obszar4\ (resets2.py, uncleared.py, uncleared2.py, savechg.py).

(a) ZAPIS. Wszystkie 139 wywolan SyncData / SaveText.Sync w 5 modach. Klucze sa w zasiegu JEDNEGO zachowania (gra: CampaignBehaviorDataStore.cs:66-80, BehaviorSaveData.SyncData robi _records.Add


- ustawienia: Tylko odczyt; nic nie budowane w drzewie, gra nie uruchamiana. Pliki pomocnicze: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\audyt\obszar5\ (usage.txt, pl_out.txt, fields.json, skrypty).

(a) Nowe pola: Armoury 116, RealisticCaptivity 2, GrandTourney 0. gen_mcm.py uruchomiony na kopii w scratchpadzie daje McmSettings.cs identyczne z drzewem (1293 / 111 / 40). Kazde nowe pole jest w MCM, ma opis po angielsku i jest czytane w kodzie pod wlasna nazwa (zadnego martwego, zadnego pod


- zderzenia: Drzewo C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int (HEAD e60928a), tylko odczyt; pliki pomocnicze w ...\dbc3d3d1-...\scratchpad\audyt\obszar6\ (diffy, dekompilacja DTE EveryoneCampaignBehavior, skrypty frag.py / consts.py / subseq.py).

SCALENIE (mechanicznie): konstruktor ArmouryBehavior - suma Reset obu rodzicow (ce6524b, 6c2c453) bez brakow; takt doby i rejestracje nasluchow - kolejnosc kazdego rodzica zachowana (podciag), 0 zgubionych wywolan; kazdy Reset ma wolajacego (StateHygiene.ResetAll przez 


- wydajnosc: Obszar 7 przejrzany w kodzie drzewa int (HEAD e60928a) i w dekompilacji; poza jednym drobiazgiem nie znalazlem problemu krytycznego ani waznego. Gry nie uruchamialem, niczego nie budowalem.

(a) Praca co klatke / tick mapy: MapVillagesView.OnVisualTick - praca 4 razy na sekunde; co klatke tylko LabelFrame (najwyzej 30 przeliczen punktu na ekran, tylko gdy sa napisy). W4 (MapVillageLook.cs): przebudowa obrazka tylko przy zmianie stanu ksiegi (numer zmiany + kolejka 32 wiosek i najwyzej 4 przebudowy na cwierc sekundy, histereza). W5 (MapVillagePennants.cs): wybor 80 najblizszych bez alokacji, bu


- gracz: Przeszedlem kod kazdej sciezki gracza z listy; cudze zachowanie sprawdzone w dekompilacji (gra dec32/cs_full, BK bkfull) i wlasnym ilspycmd na zywych DLL (SandBox, Native GauntletUI, TaleWorlds.Library / Engine / Core.ViewModelCollection, RBMTournament, Spoils RealisticLoot) - wyniki w C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\audyt\obszar8\. Nie znalazlem sciezki konczacej sie wywrotka ani zakleszczeniem menu wprowadzonej przez paczki od WGRANIA 33; jedyne znalezisko to star
