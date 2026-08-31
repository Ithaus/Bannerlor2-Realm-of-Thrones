# DZIENNIK ZMIAN

## 2026-08-31 — PODLOGA SPRZETU: nikt nie wyjdzie nagi ani bez broni (audyt wielo-agentowy)
**Mod:** Armoury + CrashScribe | **Pliki:** `Armoury/src/DragonUnmount.cs`, `CrashScribe/src/Mends.cs`
**Problem:** Jeff po Prawie Wagi: "tylko zeby nadzy nie wyszli". Audyt
(4 rownolegle analizy kodu + symulacja na 1062 jednostkach ROT) wykazal,
ze ryzyko golizny jest NISKIE, ale galaz null jest ZYWYM kodem:
`DragonUnmount.cs` w trzech miejscach wpisywal `new EquipmentElement(null)`,
gdy nie znalazl lzejszego zamiennika - jedyne miejsce w repo zerujace slot
pancerza (logowalo to nawet wprost: "goly slot"). Drugi, powazniejszy
wynik: `SkillLawWard` (DTE) zdejmuje takze BRONIE bez zadnego fallbacku,
a awaryjki DTE (AssignWeaponToUnarmed, ApplyEmergencyLoadout) przebiegaja
PRZED naszym postfixem i juz sie nie powtorza - w druzynie gracza mogl
powstac zolnierz z golymi rekami. DressCode uzupelnia wylacznie pancerz
(5 slotow), broni nie tyka.
**Zmiana:** (1) DragonUnmount - trzy podlogi: legenda schodzi zawsze, ale
zolnierz dostaje zamiennik -> wzorzec klasy -> dopiero nic; bron ponad
skill bez zamiennika ZOSTAJE przy swoim (bezbronny gorszy niz
przepakowany); pancerz ponad Atletyke bez lzejszej sztuki ZOSTAJE na
grzbiecie. Meldunki throttlowane (pierwsze 20 na sesje). (2) SkillLawWard
- zbiera liste do zdjecia, a jesli zeszlyby WSZYSTKIE bronie jednostki,
najlzejsza (najnizsze Difficulty) zostaje w rece; log rozroznia zdjete
od zostawionych.
**Ryzyko / co sprawdzic:** log "Mends: swieta zasada skilli w DTE -
zdjeto N sztuk..., M razy ostatnia bron zostala w rece"; w Armoury.log
linie "ItemReq: ... zostaje w swoim". Zasada nadrzedna dziala jak dotad -
podloga odpala sie WYLACZNIE tam, gdzie alternatywa jest goly slot.
**Status:** WGRANE

## 2026-08-31 — Prawo Wagi: finalnie 0.25 kg/pkt, wspolczynnik jako SUWAK w MCM
**Mod:** Armoury + CrashScribe | **Pliki:** `Armoury/src/Settings.cs` (+McmSettings), `CrashScribe/src/Mends.cs`, `docs/pancerze-waga-atletyka.md`
**Problem:** Jeff stroil wspolczynnik (0.25 -> 0.2 -> 0.3 -> "zostaw
0.25") - jasny sygnal, ze to ma byc suwak, nie stala.
**Zmiana:** KgPerAthleticsPoint w MCM Armoury (dom. 0.25); WeightLaw
w CrashScribe czyta go przez reflection przy starcie sesji (fallback
0.25, clamp 0.05-2). Common Armor 20 kg -> Athletics 80. Tabela docs
przeliczona na 0.25. UWAGA: zmiana suwaka dziala od NASTEPNEGO
wczytania (prawo biega raz, na starcie sesji).
**Ryzyko / co sprawdzic:** log "prawo wagi ... (0.25 kg na punkt)";
suwak w MCM Armoury sekcja przy WorkshopNightRest.
**Status:** WGRANE

## 2026-08-31 — Prawo Wagi zaostrzone: 0.2 kg na punkt Atletyki (waga x 5)
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`, `docs/pancerze-waga-atletyka.md`
**Problem:** Jeff: "daj jednak 0.2 kg / 1 atletyka".
**Zmiana:** wspolczynnik 4 -> 5 (100 Atletyki niesie 20 kg, 200 niesie
40 kg). Common Armor 20 kg wymaga teraz Athletics 100; Volantene Heavy
31 kg -> 155. Tabela w docs przeliczona.
**Ryzyko / co sprawdzic:** tooltip Common Armor: Athletics 100;
najciezsze plyty (28-31 kg) na progu 140-155 - tylko dla wytrenowanych.
**Status:** WGRANE

## 2026-08-31 — Prawo Wagi: Atletyka niesie 0.25 kg pancerza na punkt (+ pelna tabela pancerzy)
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`, `docs/pancerze-waga-atletyka.md` (nowy)
**Problem:** Jeff: "zrob tabele wszystkich pancerzy - waga vs wymagana
atletyka; te wymagania sa z dupy. Powinno byc: kazdy punkt Atletyki
pozwala nosic 0.25 kg (100 -> 25 kg, 200 -> 50 kg)". Audyt 1841
pancerzy: 46% (857) mialo difficulty ZERO, w tym 105 ciezkich plyt
>= 8 kg calkiem bez wymagan (Volantene Heavy 31 kg, Braavosi Curiass
28 kg - za darmo). Osobno wyjasnione: wartosci OCHRONY w grze sa
wyzsze niz w XML (Common Armor 42 -> 100), bo RBM przelicza pancerze
w locie - WAGI i difficulty sie zgadzaja i to na nich stoi regula.
**Zmiana:** mend WeightLaw (przed SkillSinew, zeby mundury jednostek
dostaly atletyke pod nowe progi): difficulty = max(stare, waga x 4)
dla wszystkich pancerzy (Head/Body/Leg/Hand/Cape). Podnosimy TYLKO
w gore - celowe blokady person (zbroja NK 250, suknie Daenerys 200)
zostaja. Skutki: Common Armor 20 kg z prog 30 -> 80; looterzy
(Ath ~20-30) NATURALNIE traca plyty przez DTE SkillLawWard - problem
"band w plytach" rozwiazany fizyka, bez klasowych regul; gracz tez
musi miec Atletyke pod ciezar. Pelna tabela (1841 wierszy, bylo/jest)
w docs/pancerze-waga-atletyka.md.
**Ryzyko / co sprawdzic:** log "Mends: prawo wagi - wymagania
podniesione N pancerzom"; tooltip Common Armor pokaze Athletics 80;
jednostki w mundurach dostana atletyke wg nowych progow (SkillSinew
z sufitem tieru); wlasna partia gracza - kwatermistrz zdejmie plyty
ponad skill.
**Status:** ZBUDOWANE - watcher (rozszerzony na 5 modow) wgra po zamknieciu gry

## 2026-08-31 — Restytucja po czystce: znane sztuki wracaja do sakw
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff: "oddaj mi zbroje, ktore zdobylem". Czystka (przed
fixem) zjadla lupy z dwoch bitew - log NIE zapisal id zdobycznych
pancerzy (tylko liczby 21/18 szt.), wiec pelne odtworzenie niemozliwe
bez zgadywania.
**Zmiana:** jednorazowa restytucja (flaga w save): trzy sztuki znane
z logu co do id i stanu wracaja do sakw z modifierami zuzycia
(battania_civil_cape/ripped, bandit_saddle_steppe/ripped,
sturgia_sword_1_t2/rusty). Zdobyczne pancerze: Jeff wypisze nazwy
z pamieci -> dolozymy imiennie ta sama droga.
**Ryzyko / co sprawdzic:** przy nastepnym uruchomieniu komunikat
"The quartermaster returns what the purge wrongly burned - 3 pieces".
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-31 — Fix: czystka sakw nie zjada juz wzietych lupow (znikajacy pancerz)
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff: "ostatnie dwie bitwy - biore pancerz, wchodze
w inventory i pancerz znika". Log: po obu bitwach
"CleanseTrashInBags: 21/18 szt. ponizej progu zniszczenia wyrzucono
z sakw" - czystka wyrzucala KAZDY item z kondycja ponizej
LootMinConditionPercent, w tym swiezo wziete lupy (stany 35-41)
i WLASNY sprzet wojska zuzyty w walce (rusty_sword stan 6).
**Przyczyna:** drugi prog kondycji NA SAKWACH dublowal prog wrakow
na polu i nadpisywal decyzje gracza; zuzyty sprzet to u nas surowiec
do NAPRAWY (mending), nie smiec.
**Zmiana:** prog kondycji WYCIETY z CleanseTrashInBags - czystka
zostawia tylko swoje dwie stare reguly (slonie-towar, zalegle
legendy). Prawdziwy zlom nadal odsiewa prog wrakow NA POLU
(BattlefieldLaw, LootMinConditionPercent) zanim cokolwiek trafi na
ekran lupow.
**Ryzyko / co sprawdzic:** wziety z lupow pancerz LEZY w inventory
po bitwie; zuzyte sztuki mozna naprawiac; smieci na polu dalej
odpadaja przed ekranem. DLL wgra watcher po zamknieciu gry.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-31 — Czeladz tez spi: warsztat stoi noca 23-5
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`, `Settings.cs` (+McmSettings)
**Problem:** Jeff do rytmu kowadla: "czeladz tez musi spac!" - projekt
tykal 24h/dobe niezaleznie od pory.
**Zmiana:** AdvanceProjects pomija krok godzinowy miedzy 23:00 a 5:00
(WorkshopNightRest, dom. wlaczone) - szesc ciemnych godzin bez postepu
i bez XP. "Dni pracy" projektu = dni PRZY KOWADLE: robota trwa realnie
~1/3 dluzej kalendarza. Spina sie z rytmem kowala-gracza (18h zmiana +
sen wg rachunku) i z calym prawem nocy.
**Ryzyko / co sprawdzic:** pasek czekania przy projekcie noca stoi
w miejscu (6h ciszy), rano rusza; wyrob konczy sie pozniej niz
w starym zegarze - zgodnie z intencja.
**Status:** WGRANE

## 2026-08-31 — Rytm doby przy kowadle: 18h pracy, potem sen (6h albo splata dlugu) z meldunkiem
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`, `Patches.cs`, `Settings.cs` (+McmSettings)
**Problem:** Jeff: "przy kuciu/naprawach nie moge pracowac 33 godzin -
pracuje 18h, potem sen 6h (przy dlugu 9h itd.); musi byc info, ze
spie". Czekanie przy projekcie (arm_project_wait) lecialo jednym
ciagiem bez snu.
**Zmiana:** AnvilShift w ticku czekania przy projekcie: po
AnvilShiftHours (dom. 18) pracy meldunek "18 hours at the anvil - you
bed down by the forge (Xh of sleep)" i faza snu = NightRest.
NeededHours() (6h bez dlugu, 9/15/21 przy dlugu; dopisek "paying off
the debt"); po niej "You wake and return to the anvil" i nowa zmiana.
W czasie snu STAMINA regeneruje po obozowemu (wyjatek w NoRestAtWork
mimo ForgeWorkNoRest); rachunek snu nalicza sie sam (postoj w osadzie),
wiec dlug realnie schodzi. Projekt idzie dalej (czeladz kuje wg
ForgeWorksWithoutYou - sen kowala to jego potrzeba, nie pauza swiata).
Cykl zeruje sie przy kazdym wejsciu w czekanie. Dwa suwaki MCM.
**Ryzyko / co sprawdzic:** dluga robota (np. T5, 10 dni): co ~18h
meldunek snu, stamina rosnie w czasie snu, swit melduje sen z czapka;
"Step away" dziala jak dotad.
**Status:** WGRANE (gra byla zamknieta - najnowszy build w Modules)

## 2026-08-31 — Fix zegarka switu: dzien pracy w miescie to nie 18h snu
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff (screen z Riverrun, day labour BK): "pracuje
w miescie za 38 stag, a on mysli ze spalem 18 godzin - spalem 6".
Postoj w osadzie nalicza odpoczynek CALA dobe (noc 1.0/h + dzien
x DayRestFactor), a swit raportowal sume jako sen. Mechanika dlugu
byla poprawna (splata i tak ograniczona do NeededHours) - klamal
tylko komunikat.
**Zmiana:** raport switu z CZAPKA do realnej potrzeby:
min(naliczone, NeededHours) - przy dlugu 0 najwyzej "about 6h of
sleep", wiecej tylko przy odsypianiu dlugu (9/15/21h). Atletyka za
prace fizyczna JUZ dziala - to BannerKings daje skill za day labour
(screen: +1 Athletics), nic nie dokladamy.
**Ryzyko / co sprawdzic:** dzien pracy najemnej w miescie -> swit
melduje ~6h, nie 18. DLL wgra watcher po zamknieciu gry (gra dziala).
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-31 — Fix: oboz zatrzymuje marsz (koniec spania w biegu)
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff: "maly bug - przy auto-obozie sen startuje, czas
leci, a ludzik dalej idzie po mapie do zaznaczonego celu; spimy
i idziemy jednoczesnie". MakeCampNow/SleepInit nie zdejmowaly rozkazu
ruchu - menu snu (StartWait, czas plynie) nie blokuje marszu partii.
**Zmiana:** (1) MakeCampNow: SetMoveModeHold PRZED rozbiciem obozu -
kazda sciezka (auto-zmierzch, popup, klawisz O) najpierw zatrzymuje
kolumne; (2) SleepInit: to samo przy kladzeniu sie poza osada.
Po pobudce partia stoi - gracz sam wskazuje dalsza droge (Wayfinder
melduje kurs od reki).
**Ryzyko / co sprawdzic:** auto-oboz o 21 w marszu: kolumna staje,
namiot, pasek snu - ludzik NIE przesuwa sie po mapie; rano stoi tam,
gdzie zasnal.
**Status:** WGRANE

## 2026-08-31 — Analiza logow pierwszej sesji nowej kampanii + fix SkillSinew na nowej grze
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "czy z logow gry wyczytales bledy - czy cos sie
wysypalo?". Analiza sesji 31.08 11:32 (nowa kampania, ~30 min):
(1) W TRAKCIE GRY CZYSTO - zero bledow po wejsciu do kampanii;
wszystkie 20+ mendow zainicjowanych, wszystkie nowe prawa wstaly
(WorldPace 4 modele predkosci + 2 oblezen, WinterBite, ScorchedEarth,
Wayfinder, kary snu 6/3 modeli, panika 4 modele morale, T6 pole+sym,
unikaty: 39 itemow z handlu, 1 kopia z targow); (2) wyjatki TYLKO
startowe first-chance, wszystkie ZLAPANE - glownie martwe patche
cudzego BKROTPatch (DynamicPartySizePerformancePatch,
RebellionsGameEntityInstantiatePatch celuja w inna wersje gry;
Harmony je raportuje i jedzie dalej - nieszkodliwe, znane zjawisko);
(3) JEDNA REGRESJA: "SkillSinew - podbita 0 jednostkom" - na NOWEJ
grze SessionLaunched biegnie zanim ekwipunki jednostek sie
zmaterializuja (wszystkie maxDiff=0 -> petla pusta); na save'ach
dzialal (169 podbic w logach 30.08).
**Zmiana:** SkillSinew liczy `seen` (jednostki z realnym ekwipunkiem);
seen == 0 -> flaga SinewApplied zostaje false i mend POWTARZA SIE
z pierwszym tickiem dnia, az ekwipunki beda (log: "ekwipunki jeszcze
niezaladowane (nowa gra), powtorze z pierwszym dniem").
**Ryzyko / co sprawdzic:** w biezacej kampanii Jeffa po pierwszym
przeskoku dnia log "SkillSinew - Atletyka podbita N jednostkom"
z N ~169; kontrola w grze: encyklopedia -> Giant -> Athletics 150.
**Status:** WGRANE

## 2026-08-31 — Panel obozowy pod klawiszem O: nocna polityka zmienialna w kazdej chwili
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff: "moge zmienic zdanie - Never ask nie moze byc stale".
**Zmiana:** pytajka klawisza O zamieniona w PANEL OBOZOWY: (0) Make
camp here; (1/2/3) Nightfall orders: ask / always camp / never ask -
aktualny tryb oznaczony "(current)", wybor przestawia CampPromptMode
od reki (zapis w save jak dotad). Decyzja ze zmierzchowego popupu
przestala byc wiazaca - O otwiera zmiane w 2 klikniecia.
**Ryzyko / co sprawdzic:** O na mapie pokazuje 4 pozycje z markerem
biezacego trybu; zmiana dziala natychmiast i przezywa save/load.
**Status:** WGRANE

## 2026-08-31 — Zmierzchowy popup: "Night Falls - make camp?" z auto-obozem i wylacznikiem
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`, `Settings.cs` (+McmSettings)
**Problem:** Jeff: "jak jade i zapada noc, powinien byc popup czy
rozbijamy oboz - i moge zaznaczyc auto-oboz na kazda noc albo
wylaczyc pytanie".
**Zmiana:** o 21:00 kolumna GRACZA w ruchu na czystej mapie (nie
w osadzie/bitwie/armii/na morzu/w sluzbie ROT, nie spiaca, nie
w obozie) dostaje wybor: (1) Make camp - ta sama sciezka co klawisz O
(oboz BK albo nasz, namiot, menu z "Bed down"); (2) March on - dlug
snu robi swoje; (3) Always make camp at nightfall - odtad auto-oboz
o zmierzchu bez pytania ("Night falls - the column makes camp");
(4) Never ask again - marsz nocny bez pytan (klawisz O nadal dziala).
Wybor trybu (pytaj/zawsze/nigdy) zyje W SAVE (rachunek snu, pole 4);
pytanie najwyzej raz na dobe; master-wylacznik w MCM
(NightfallPromptEnabled). Refaktor: wspolna sciezka MakeCampNow()
dla klawisza O i popupu.
**Ryzyko / co sprawdzic:** o 21:00 w marszu wyskakuje "Night Falls";
wybor "Always" stawia oboz kazdego zmierzchu automatycznie; "Never"
ucisza na stale (w tej kampanii); popup NIE wyskakuje w armii lorda
ani na morzu. Inquiry z hourly ticku - ta sama bezpieczna sciezka co
pytajka klawisza O.
**Status:** WGRANE

## 2026-08-31 — Audyt spojnosci przed nowa kampania: 0 konfliktow, 1 falszywy alarm
**Mod:** — (dokumentacja) | **Pliki:** `docs/AUDYT-2026-08-31.md` (nowy)
**Problem:** Jeff: "zrob audyt naszych zmian - czy nic sie nie gryzie,
nie ma konfliktow, sprzecznosci ani bugow".
**Zmiana:** przeglad kodu wszystkich praw sesji 30-31.08 w parach
ryzyka + weryfikacja w zrodlach ROT/BK/RBM/DTE/RC. WYNIK: zero
konfliktow krytycznych; jedyny alarm (nocleg AI mogacy usypiac
oblegajacych) okazal sie FALSZYWY - wykluczenie BesiegerCamp juz
istnialo. Macierz ~20 par z werdyktami w docs/AUDYT-2026-08-31.md;
cztery pozycje do OBSERWACJI (strojenie suwakow, nie bledy): tempo
dezercji AI, zimowa kaskada glodu, wydajnosc petli dziennych, leczenie
w skali czasu. Spisana zasada spojnosci swiata (umarli bez potrzeb;
wszyscy zywi rowno; kary od wyniku; suwak + wpis na kazde prawo).
**Ryzyko / co sprawdzic:** pierwsza sesja nowej kampanii = test calosci;
przyslac sekcje "Mends:" z logu CrashScribe i zajrzec w Armoury.log.
**Status:** ZAMKNIETE

## 2026-08-31 — Spalona ziemia + Ksiega Wojny: foraging i blizny wiosek, dezercje za zold, krater po szturmie
**Mod:** Armoury | **Pliki:** `Armoury/src/ScorchedEarth.cs` (nowy), `Armoury/src/WarLedger.cs` (nowy), `Settings.cs`, `SubModuleMain.cs`, `ArmouryBehavior.cs` (+McmSettings)
**Problem:** Jeff ("rob 3" + "4: nie ma kasy - dezercja i minus morale;
miasto wziete szturmem - minus do prosperity"). Fakty z kodu: wioska
<300 palenisk odrasta +4/dzien (po rabunku "jak nowa" w tygodnie);
vanilla JUZ karze morale za niewyplacony zold (HasUnpaidWages) - wiec
morale nie dublujemy, dokladamy dezercje.
**Zmiana:** SPALONA ZIEMIA: (1) foraging - wroga armia lordowska
(>=100 ludzi, promien 3) drenuje wioske ~0.8 paleniska/dzien na 500
ludzi i bierze zboze do taboru ("The men live off the enemy's land");
PODLOGA 25 palenisk - marsz uszczupla, nie zabija; jedna wioska
dziennie na partie; umarli nie zeruja; (2) blizna - wioska <150
palenisk odbudowuje sie w 25% tempa (z +4 do +1/dzien; sezon-dwa
zamiast tygodni), a <40 palenisk "uchodzcy wracaja" +0.5/dzien flat -
regiony nigdy nie umieraja na stale; postfix Priority.Last PO
modyfikatorach BK; JEDNA os kary (paleniska) - produkcja/rekruci/
podatki spadaja emergentnie, zero spirali. KSIEGA WOJNY: (3) zold -
po 2 dniach laski dezercje 0.5%/dzien narastajaco za kazdy dzien
zwloki, ELITY PIERWSZE (najwyzszy tier); AI placi polowe stawki
(biedni lordowie BK nie moga stopniec globalnie); garnizony i umarli
poza prawem; (4) osada wzieta OBLEZENIEM (BySiege - szturm i poddanie
po oblezeniu): prosperity -15%, lojalnosc -15; komunikat zdobywcy-
graczowi "Rebuild what you broke". Suwaki MCM na wszystko.
**Ryzyko / co sprawdzic:** po przemarszu wrogiej armii wioski w logu
chudna do podlogi; zlupiona wioska w rozpisce ma wpis "War scars";
gracz bez zlota widzi ostrzezenie, po 3. dniu dezercje elit; zdobycie
miasta obleczeniem tnie prosperity (log "WarLedger"). AI-safety:
polowa stawki dezercji + cap dzienny; obserwowac pierwsze tygodnie.
**Status:** WGRANE

## 2026-08-31 — Zima z zebami + Dluga Noc: sezon glodu, gradient Polnocy, jesienne spichrze
**Mod:** Armoury | **Pliki:** `Armoury/src/WinterBite.cs` (nowy), `BkSupplyTemper.cs`, `Settings.cs`, `SubModuleMain.cs`, `ArmouryBehavior.cs` (+McmSettings)
**Problem:** Jeff ("rob 2"): zima w ROT byla kosmetyczna (snieg i tyle),
a w lore to kryzys egzystencjalny. Fakty z kodu: ROT ma zwykle 4 pory
roku; nasz kalendarz daje zime 42 dni/rok; vanilla juz tnie marsz na
sniegu (-10%), wiec predkosci nie ruszamy.
**Zmiana:** (1) konsumpcja partii zima +50% (patch TYLKO na modelu
bazowym - ROT deleguje, a dla umarlych w ogole nie deleguje, wiec horda
NK nie placi); (2) produkcja wiosek -50% zima - ceny zywnosci rosna
emergentnie z podazy; (3) spichlerze miast topnieja szybciej (skala
z prosperity) - zimowe oblezenie + glod + zaraza to trojca lamiaca
twierdze; (4) jesienia cap zapasow AI x2 (BkSupplyTemper przycina
teraz do sezonowego capu - Jeffowa poprawka: cap jesienny faktycznie
WYZSZY, nie tylko prog); (5) GRADIENT POLNOCY +-25% po osi Y mapy -
pod Winterfell przednowek, w Dorne lagodnie; (6) DLUGA NOC: gdy zywy
bohater kultury whitewalker jest w polu, KAZDY dzien liczy sie jak
zimowy ("The Long Night falls - winter marches with the dead");
komunikaty sezonowe. Siedem suwakow MCM.
**Ryzyko / co sprawdzic:** wejscie zimy -> komunikat i wpisy "Winter"
w rozpiskach jedzenia/produkcji; jesienia AI woze 8 dni zapasow;
podczas inwazji NK zima nie konczy sie ze zmiana sezonu.
**Status:** WGRANE

## 2026-08-31 — Prawo Zarazy Obozowej: dlugie oblezenia rodza dyzenterie, medycyna jest tarcza
**Mod:** Armoury | **Pliki:** `Armoury/src/CampFever.cs` (nowy), `ArmouryBehavior.cs`, `Settings.cs` (+McmSettings)
**Problem:** Jeff ("rob" po projekcie): historycznie choroby zabijaly
wiecej wojska niz zelazo, a u nas armia stala pod murami pol roku
bez jednego kichniecia - czekanie bylo darmowe.
**Zmiana:** dzienny tick po wszystkich oblezeniach swiata:
(1) inkubacja 9 dni (sprawne oblezenie nic nie odczuje); potem
zachorowania: baza 0.6%/dzien x narastanie +15%/dzien x TLOK
(sqrt(ludzi/500), 0.6-2.5 - doomstacki gnija najszybciej);
(2) chorzy ida W RANNYCH (wracaja naszym powolnym leczeniem - armia
zmielona jeszcze tygodnie po oblezeniu), co dziesiaty umiera;
(3) obroncy lapia 40% tempa oblegajacych, ale GLOD (pusty spichlerz)
podwaja ich tempo - wyscig spichlerza z latrynami; (4) MEDYCYNA:
skill chirurga partii -0.25%/pkt (cap -50% przy 200; kazda partia
armii liczy SWOJEGO medyka), perk Preventive Medicine -15%, Siege
Medic smiertelnosc /2, Pristine Streets gubernatora -30% za murami
i glod boli o polowe mniej; Walk It Off/Good Lodging/Triage/Sledges
dzialaja same, bo chorzy sa rannymi; (5) bohaterowie nie choruja
z ticka; UMARLI nie choruja wcale - horda NK oblega bez zegara;
(6) komunikaty gracza (ostrzezenie przy wejsciu zarazy + dzienne
zniwo), sumaryczna linia w logu per oblezenie AI. Siedem suwakow MCM.
**Ryzyko / co sprawdzic:** przy oblezeniu >9 dni komunikat "Camp fever
stirs..."; w logu "CampFever: <osada> dzien N - oblegajacy X/Y...";
armia 1200 przez 25 dni traci ~15-18% w chorych. Wydajnosc: tick raz
DZIENNIE po aktywnych oblezeniach - pomijalna.
**Status:** WGRANE

## 2026-08-31 — Nawigator kursu (km, godziny, dni + flaga celu) i namioty v2 (promien 100)
**Mod:** Armoury | **Pliki:** `Armoury/src/Wayfinder.cs` (nowy), `NightRest.cs`, `Settings.cs`, `SubModuleMain.cs` (+McmSettings)
**Problem:** Jeff: (1) "klikam gdzie jechac - niech pokaze znaczek na
mapie i ile to km i godzin marszu"; (2) "namioty glupio wygladaja: moj
czasami sie nie uruchamia, a spiace AI stoi jako konik/piechur jakby
nic - promien ~100 ode mnie".
**Zmiana:** (1) Wayfinder - postfix na SetMoveGoToPoint/Settlement
partii gracza: meldunek kursu z dystansem PO TRASIE (MapDistanceModel),
kilometrami w skali lore (4.75 km/jedn.), godzinami w siodle i dniami
drogi (uwzglednia 6 h snu); cel-osada dostaje vanillowa FLAGE sledzenia
(te od questow) + strzalke kompasu - flaga schodzi po dotarciu lub
przy nowym kursie; questowych flag nie ruszamy (CheckTracked). Kreski
trasy silnik nie narysuje bez grzebania w renderze mapy - flaga +
meldunek. Throttle 2 s na powtorzony klik. Ustawienie
CoursePlotterEnabled. (2) Namioty: gracz dostaje namiot AUTOMATYCZNIE
na kazdym nocnym postoju w polu (nie tylko z menu/klawisza O); noca
namiot dostaje tez KAZDA stojaca kolumna lorda/karawany w zasiegu -
takze te zatrzymane przez vanilla AI, nie tylko nasi spiacy (test
bezruchu: pozycja niezmieniona miedzy odswiezeniami co ~2 s realne;
umarli nie obozuja); AiTentRadius 35 -> 100, AiTentCap 40 -> 60.
Wizerunki wciaz ruszane TYLKO przy zmianie stanu (pulapka z CLAUDE.md).
**Ryzyko / co sprawdzic:** klik w miasto -> meldunek "Course set for...
~N km - about H h in the saddle, D days on the road" + flaga na mapie
i kompasie, schodzi po wjezdzie; nocny postoj = namiot od reki; spiace
kolumny w promieniu 100 wygladaja jak obozy. Wydajnosc: petla nocna po
partiach co ~2 s realne z twardym capem 60 namiotow.
**Status:** WGRANE

## 2026-08-31 — Tempo swiata 50%: marsz i oblezenia w skali podwojonego roku
**Mod:** Armoury | **Pliki:** `Armoury/src/WorldPace.cs` (nowy), `MarchPace.cs`, `Settings.cs`, `SubModuleMain.cs` (+McmSettings)
**Problem:** Jeff: "ile dziennie moze maszerowac armia? nasz czas leci
x2 wzgledem zwyklego (bylo x4, wydluzylismy rok); oblezenia powinny
trwac x2, wszystko trzeba uwzglednic". Pomiar z mapy ROT i lore
(Mur->Sunspear = 1012 jedn. = ~3000 mil z ksiazek => ~4.75 km/jedn.;
kontrola: Winterfell->KP 465 jedn. = ~2200 km, zgadza sie z lore):
armia (speed ~4, ~18 h marszu po naszym snie) robila ~340 km NA DZIEN
GRY - Winterfell->Krolewska Przystan w 6.5 dnia zamiast ksiazkowego
miesiaca; oblezenia konczyly sie w 3-5 dni ze 168-dniowego roku.
**Zmiana:** dwa suwaki MCM, oba domyslnie 50%: (1) WorldPacePercent -
postfix na CalculateBaseSpeed WSZYSTKICH modeli predkosci: baza kazdej
partii (gracz, AI, karawany, wiesniacy - swiat jednym rytmem) x50%;
teren, ladunek, kary snu i sufity licza sie dalej od wolniejszej bazy;
sufity kolumny MarchPace przeskalowane tym samym mnoznikiem. Efekt:
Winterfell->KP ~13 dni ze 168-dniowego roku = lore'owy miesiac jako
ulamek kalendarza; (2) SiegePacePercent - postfix na
GetConstructionProgressPerHour wszystkich modeli oblezen: budowa machin
x50% => oblezenia ~2x dluzsze, glodzenie twierdzy wraca do gry (zapasy
miasta topnieja przez dluzsze tygodnie).
Systemy juz w skali (bez zmian): rok 168 dni, kucie 2 dni/tier, regen
ochotnikow 25%, samonaprawa 10%/dzien, sen 6 h/dobe. Do obserwacji
w nowej kampanii: tempo leczenia rannych i plodnosc/starzenie (vanilla
dni - przy wolniejszym swiecie relatywnie szybsze; decyzja po testach).
**Ryzyko / co sprawdzic:** rozpiska predkosci pokazuje wpis "World
pace"; podroz White Harbor->Winterfell ~3 dni (bylo ~1.5); AI dalej
dziala normalnie (to mnoznik bazy, zadnych nowych regul); oblezenie
buduje machiny 2x wolniej - patrz pasek budowy.
**Status:** WGRANE

## 2026-08-31 — Smoki ma TYLKO Daenerys (plus gracz z questa); zapasy AI na 4 dni
**Mod:** CrashScribe + Armoury | **Pliki:** `CrashScribe/src/Mends.cs`, `Armoury/src/Settings.cs` (+McmSettings)
**Problem:** Jeff: "smoki ma TYLKO Daenerys! plus gracz, jesli wykona
questa - wszystkie inne wywal, nie da sie ich zdobyc!". Zrodla smokow
w ROT: Daenerys (kod ROT, odzysk po niewoli), quest Valyrian Thief
(gracz dostaje dragon_red), dialog ganku "Your dragon will fight for
me now" (zabieranie smoka pokonanym) i lupy -> DTE (kawalerzysta na
smoku). Osobno: "BkSupplyDaysCap daj na 4".
**Zmiana:** mend DragonLaw: (1) DragonPurge (sesja + co dzien):
bohaterom spoza klanu gracza i sponad Daenerys smok schodzi z siodla;
rostery partii AI i targi miast wymiatane ze smokow; wszystkie itemy
smocze poza handel (NotMerchandise); (2) pula DTE: smok nigdy nie
trafia pod szeregowego; (3) dialog ganku smoka ZABLOKOWANY (prefix na
ROTGankBehavior.CanTakeDragon) - jedyna droga gracza to quest;
(4) NIETKNIETE: Daenerys (jej smok + odzysk po niewoli dziala), klan
gracza (questowy dragon_red zostaje), quest Valyrian Thief.
Nasz starszy CleanseDragonStables dalej czysci tabor gracza ze smokow
zlupionych. Do tego BkSupplyDaysCap 1 -> 4 (zdrowsza logistyka AI,
mniej sztucznych glodowek). Decyzje Jeffa odnotowane: cap ochotnikow
ODPUSZCZONY; Zlote Plaszcze BEZ wyjatku mundurowego (tier 2 nosi
zwykle zbroje).
**Ryzyko / co sprawdzic:** log "Mends: smoki tylko dla Daenerys -
zdjeto z siodel N, wymieciono M"; dialog zabrania smoka nie pojawia
sie u pokonanych; Daenerys lata jak latala; po quescie ValyrianThief
gracz zatrzymuje czerwonego smoka.
**Status:** WGRANE

## 2026-08-31 — Prawo Przeprawy etap 2: straz zawraca wrogie armie AI; Moat Cailin i Bloody Gate strzezone
**Mod:** Armoury | **Pliki:** `Armoury/src/CrossingLaw.cs`, `Settings.cs` (+McmSettings)
**Problem:** Jeff: "Crossing law - robimy, tylko sprawdz, czy AI sie
nie zablokuje i bedzie wiedzial, o co chodzi".
**Zmiana:** (1) przeprawy: The Twins + Moat Cailin (castle_B1) +
Bloody Gate (castle_EN2); (2) wrogie PARTIE LORDOW AI w strefie sa
zawracane. Zabezpieczenie przed zapetleniem pathfindingu: odbita
partia dostaje CEL-ODWROT (punkt po swojej stronie strefy, wiec ma
dokad isc) i osobisty cooldown 3 h, w ktorym straz jej nie tyka -
zero wibrowania na granicy; po cooldownie ewentualnie znow. Oblezenie
przeprawy LEGALNE (BesiegerCamp skip) - AI, ktore chce na druga
strone, moze zdobyc warownie; eskorty ida za wodzem (AttachedTo skip).
Meldunki o zawracaniu AI ograniczone do 3/dobe. Nowe ustawienie
CrossingLawAi (on). AI nie "zrozumie" prawa (vanilla pathfinding go
nie zna) - ale nigdy nie zamarza: zawsze ma cel i wolna decyzje.
**Ryzyko / co sprawdzic:** pod Twins wrogowie Freyow krecia sie po
swojej stronie/oblegaja zamiast przenikac; watch log "CrossingLaw: AI
... zawrocone"; wydajnosc ticku (skan partii co ~2 s realne).
**Status:** WGRANE

## 2026-08-31 — Handel jencami wedle geografii: Westeros paser 20%, Essos pelna cena
**Mod:** RealisticCaptivity | **Pliki:** `RealisticCaptivity/src/FairRansom.cs`, `Settings.cs` (+McmSettings)
**Problem:** Jeff ("robimy jencow"): w Westeros niewolnictwo jest
zakazane - sprzedaz jencow wszedzie po tej samej stawce nie miala
sensu swiata.
**Zmiana:** w postfixie FairRansom (po podlodze ceny): sprzedaz GRACZA
w osadzie z X <= 770 na mapie (Westeros; granica po Waskim Morzu -
Sunspear 689, Braavos 857) placi WesterosFencePercent (dom. 20%) stawki;
osady Essos placa pelna. Wyceny AI w tle i okupy bohaterow nietkniete.
Ustawienia: PrisonerGeoSale (on), WesterosFencePercent (20).
**Ryzyko / co sprawdzic:** sprzedaz tego samego jenca w King's Landing
vs Pentos - ~5x roznica; okupy lordow bez zmian.
**Status:** WGRANE

## 2026-08-31 — Rozbiorka unikatu: Smithing 200 i nauka pewna (jedyna sztuka nie plonie)
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`
**Problem:** Jeff: "unikat przy rozbiorce uczy zawsze - NAPRAW, ale
rozebrac mozesz tylko przy Smithing minimum 200". Dotad rzut na
porazke mogl SPALIC jedyny egzemplarz bez nauki.
**Zmiana:** TakeApartApply, sciezka unikatow: Smithing < 200 = odmowa
i sztuka ZOSTAJE w sakwach ("This famed piece is beyond your hands...");
przy 200+ zadnego rzutu - sztuka przepada jak przy kazdej rozbiorce,
wzor wchodzi do ksiegi ZAWSZE, XP 20/tier. Zwykle wzory: stary rzut
bez zmian.
**Ryzyko / co sprawdzic:** rozbiorka unikatu przy niskim Smithing nie
niszczy przedmiotu; przy 200+ komunikat "with a master's care".
**Status:** WGRANE

## 2026-08-31 — Swieta zasada skilli w DTE + sprzet umarlych niszczony + smoczy ogien pali Innych w polu
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: (1) "DTE lamie swieta zasade skilli - NAPRAW";
(2) "lodowych broni Wedrowcow ludzie nie moga uzywac, niszczone zawsze;
martwe konie ABSOLUTNIE nie dla ludzi - nic martwego!"; (3) smoczy
ogien w bitwie recznej podlegal cieciu T6.
**Zmiana:** (1) SkillLawWard - postfix na DTE DoAssignAsync: po
przydziale kazdy slot (bron, zbroja, kon) z Difficulty ponad
GetSkillValue(RelevantSkill) jednostki schodzi z grzbietu; sztuka NIE
przepada (pula DTE to odbicie taboru); (2) IsDeadGear (kultury
wights/whitewalker + ice_*/wight_*/nightking_blade/white_walker_saddle):
wypada z puli DTE bez wyjatkow, a po kazdej bitwie gracza lupy z lodu
i martwego ciala ROZPUSZCZAJA SIE (listener MapEventEnded, komunikat
"The ice of the Others melts..."); partia gracza-Othera zachowuje
swoje. UWAGA: nightking_blade przez to NIE do nauczenia (bron lodowa) -
zbroje NK pozostaja zdobywalne; (3) ValyrianWard: cios agenta-smoka
(Monster.MonsterUsage zawiera "dragon") nie podlega cieciu T6 - ogien
pali Innych pelnia takze w polu (w symulacji juz dzialal przez ROT).
**Ryzyko / co sprawdzic:** log "swieta zasada skilli w DTE - zdjeto N";
po bitwie z Wedrowcami komunikat o topnieniu; rekrut nie biega w plycie
z taboru.
**Status:** WGRANE

## 2026-08-31 — Zegarek snu v2 (godzina i pora dnia) + kara snu tnie realna predkosc kolumny
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`, `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff: "dodaj godzine i pore dnia i ile spalismy" oraz
"przyjrzyj sie predkosci ruchu, czy ma to sens". Audyt stosu predkosci:
vanilla trzyma twarde minimum 1.0 (ujemna niemozliwa), BannerKings
predkosci nie rusza, ROT dodaje +20% tylko partii, w ktorej gracz
sluzy. JEDEN nonsens: kara snu (AddFactor od bazy) biegla PRZED
sufitem kolumny MarchPace - w wolnej, objuczonej kolumnie -25% bywalo
NIEWIDOCZNE (baza 8 -> kara 6 -> sufit 4 = dalej 4).
**Zmiana:** (1) pobudka melduje godzine i pore dnia: "It is 6:00, dawn.
You slept 8.5h of sound night sleep." (pory: dawn/morning/midday/
afternoon/dusk/night); (2) NightRest.ApplyAll przeniesione ZA
MarchPace.ApplyAll i kara liczona od WYNIKU (Add(-Result*k) zamiast
AddFactor) - zmeczenie tnie realna predkosc kolumny po wszystkich
sufitach; minimum 1.0 vanilla nadal chroni przed absurdem.
**Ryzyko / co sprawdzic:** tooltip predkosci: "Sleepless nights"
odejmuje widoczna wartosc takze przy wolnej kolumnie; pobudka z godzina
i pora dnia.
**Status:** WGRANE

## 2026-08-31 — Zegarek snu: pobudka mowi ile spales i jak dobrze
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff: "budze sie i chce wiedziec ile spalem, plus jakosc
snu - podepnijmy ten zegarek". Pobudka mowila tylko "The men wake
rested", bez liczb.
**Zmiana:** (1) sen w menu zapamietuje godzine polozenia sie; pobudka
(takze przerwana) melduje: godziny ZEGARA, godziny NALICZONE (dzien
liczy sie slabiej wg DayRestFactor) i jakosc: >=90% nocnych = "sound
night sleep", >=70% = "fair rest, part of it by daylight", nizej =
"fitful daylight sleep - it counted for Xh"; przy wiszacym dlugu
dopisek ile godzin kosztuje pelna splata; (2) spanie BEZ menu (postoj
noca): swit melduje "The night gave the men about Xh of sleep".
**Ryzyko / co sprawdzic:** polozyc sie w obozie wieczorem -> rano
komunikat z liczbami; przespac noc stojac na mapie -> o 6:00 melduje
godziny nocy.
**Status:** WGRANE

## 2026-08-31 — Zapasowa zbroja Nocnego Krola jedzie w jego taborze
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff do wpisu nizej: "Nocny Krol moze ja miec, zapasowa
zbroje, w swoim ekwipunku" - nightking_armor (wariant bez kolcow) byl
celowo poza obiegiem.
**Zmiana:** nowy tryb "bag" w kaskadzie DressTheNamesakes: sztuka
trafia do TABORU partii wlasciciela (ItemRoster). nightking_armor
wjezdza do taboru Nocnego Krola - wypadnie z lupow dopiero temu, kto
rozbije jego partie. NK bez partii przy starcie sesji (przed inwazja)
= akcja czeka i ponawia sie kazda sesje, az wlasciciel sie pojawi.
DTE jej wightom nie zalozy (UniqueWard), czystka targow jej nie tknie
(lezy w partii, nie w miescie).
**Ryzyko / co sprawdzic:** log "Mends: Night King Armor wjezdza do
taboru Night King jako zapas" (albo "czeka na wlasciciela", poki NK
nie wstanie); po rozbiciu partii NK zbroja w lupach.
**Status:** WGRANE

## 2026-08-31 — Kazdy unikat ma dom: imiennicy ubrani, spadki rozdane, relikwie na targach
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** analiza wykazala 34 BEZPANSKIE unikaty (nikt ich nie nosi,
a po zdjeciu z handlu staly sie niezdobywalne): pelne zestawy Ramsaya,
Cersei, Stannisa, Daenerys, Renly'ego, Rhaegara, Aemona, korony
(Robert/Joffrey/Euron/Renly) i inne. Jeff: "ubierz postacie ktore
istnieja; nie zyja - spadkobiercom; nie ma ich - jedna sztuka lezy
w miescie historycznie poprawnym".
**Zmiana:** mend DressTheNamesakes (start sesji, kaskada per item):
(1) zywy wlasciciel po imieniu -> zaklada (battle lub civilian slot -
korony ida w stroj cywilny, krol w polu nosi helm); (2) inaczej zywy
spadkobierca: korona Roberta -> STANNIS (prawowity; imie wlasciciela
celowo scisle "Robert Baratheon", zeby nie ubrac Roberta Arryna),
korona Joffreya -> Tommen w odwodzie; (3) inaczej JEDNA sztuka na targ
miasta z sensem: zestaw Rhaegara -> King's Landing (trofea znad
Tridentu; Aegon, formalny dziedzic, nosi juz Blackfyre'a), zestaw
Aemona Smoczego Rycerza -> Dragonstone (imie celowo PUSTE, zeby nie
ubrac plyty na 100-letniego maestera Aemona z Muru), wariant zbroi
Brienne -> Storm's End, houndskull -> Lannisport, zapasowy naramiennik
Ramsaya -> Barrowton, czerwona suknia Cersei -> King's Landing.
nightking_armor (wariant bez kolcow) CELOWO poza obiegiem - trupami
sie nie handluje. Akcje jednorazowe per kampania (save
"cs_uniq_homes"); czystka targow (UniqueWares) omija relikwie
(RelicIds), zeby nie zjadac ich co sesje. Fallbacky odporne: brak
zywego bohatera -> polka; brak miasta -> log "relikwia czeka".
**Ryzyko / co sprawdzic:** log CrashScribe "Mends: kazdy unikat ma dom
- ubrano N, polozono na targach M"; Ramsay w bitwie w swoim pancerzu
ze skora na naramiennikach; w King's Landing na targu lezy po 1 szt.
zestawu Rhaegara. Kupiona relikwia = zwykly zdobyty unikat (przetop/
rozbiorka -> nauka -> kucie).
**Status:** WGRANE

## 2026-08-30 — Unikaty: bez limitu sztuk po opanowaniu; nauka TYLKO z egzemplarza (dwie drogi)
**Mod:** Armoury | **Pliki:** `Forge.cs`, `RangedLore.cs`, `SmeltTab.cs`, `ArmouryBehavior.cs`, `FletchForge.cs`
**Problem:** Jeff: (1) "moge kuc wiecej niz jeden, jak juz opanuje";
(2) "kucie pancerzy NIE odblokowuje receptur unikatow - musze zdobyc
i albo przetopic, albo nauczyc sie patternu u kowala"; (3) "w CRAFT
pancerze maja sie odblokowywac losowo w kategorii, jak przy broni".
Audyt: punkt (3) JUZ DZIALA od dawna (szkoly RangedLore: helmy, korpusy,
nogi, rece, plaszcze, tarcze, kropierze; Seed tieru 1, kazde wykucie
uczy i losowo odkrywa nastepne wzory w szkole, kowadlo milczy przy
nieznanym wzorze). Unikaty przypadkiem juz NIE wpadaly z losowan
(Teachable odrzuca NotMerchandise) - zostaly szwy.
**Zmiana:** (1) Forge.LegendAllowed: wzor z UniqueLore wolny od reguly
"tylko jedna sztuka" - legendarny KOSZT zostaje (materialy wielokrotne,
Valyrian Steel, prog skilla); (2) RangedLore.Learn i CanLearnFrom znaja
unikaty: rozbiorka u kowala ("Take a piece apart to copy its pattern")
uczy unikatu tak samo jak przetop (obie drogi -> ArmouryBehavior.
LearnUnique; dubel nauki w SmeltTab usuniety - idzie jedna sciezka
OnSmelted -> Learn); (3) kowadlo przy nieopanowanym unikacie mowi
wprost: "Seize the original and study it - melt it down, or take it
apart at the smith" zamiast mylacego "the craft itself will teach you".
**Ryzyko / co sprawdzic:** zdobyc unikat -> rozbiorka u kowala uczy
(komunikat "You have studied the make of X"); wykuc DWA egzemplarze
nauczonego unikatu - drugi nie moze byc zablokowany "there can be
only one"; zwykle pancerze odkrywaja sie kuciem jak dotad.
**Status:** WGRANE

## 2026-08-30 — Nauczone unikaty kuje sie w CRAFT (nie w menu), a wojsko moze je nosic
**Mod:** Armoury + ForgeView + CrashScribe | **Pliki:** `Armoury/src/RangedLore.cs`, `Armoury/src/SmithMenu.cs`, `ForgeView/src/ArmourFilterMixin.cs`, `CrashScribe/src/Mends.cs`
**Problem:** Jeff do wersji z opcja menu: "nie, po co - kucie jest
w kuzni, a dokladnie w CRAFT"; oraz: "jak zdobede pancerz Brienny
i sie go naucze, to DTE moze go wykorzystac".
**Zmiana:** (1) opcja "Forge a studied masterwork" USUNIETA z menu
kowala; (2) ForgeView doklada nauczone unikaty (Armoury.UniqueLore,
przez reflection) na liste zakladki CRAFT - BK sam je odrzuca, bo maja
NotMerchandise; (3) RangedLore.KnownOf: unikat znany WYLACZNIE po
przetopieniu egzemplarza - nauczone laduja w znanych, gotowe do kucia;
(4) CrashScribe UniqueWard przepuszcza nauczone wzory do puli DTE -
wykute egzemplarze wolno nosic wojsku (DTE rozdaje tylko FIZYCZNE
sztuki, kopie z powietrza nie istnieja). BONUS spostrzezenie: unikaty
maja NotMerchandise i wysoka wartosc, wiec Recipes.IsLegendary lapie
je automatycznie - kuja sie jako LEGENDY (materialy wielokrotnie,
Valyrian Steel obowiazkowo, mistrzowski prog skilla, jedna naraz).
**Ryzyko / co sprawdzic:** przetopic unikat -> pojawia sie na liscie
CRAFT jako znany (log ForgeView "Nauczone unikaty na polce CRAFT: +N");
wykuty egzemplarz po bitwie moze wyladowac na zolnierzu (DTE).
**Status:** WGRANE (Armoury, ForgeView, CrashScribe)

## 2026-08-30 — Przetop unikatu uczy kuznie go odtwarzac ("Forge a studied masterwork")
**Mod:** Armoury | **Pliki:** `Armoury/src/UniqueGear.cs` (nowy), `ArmouryBehavior.cs`, `SmeltTab.cs`, `SmithMenu.cs`
**Problem:** Jeff: "jesli pokonam bohatera i przekuje jego pancerz, to
moge potem go tworzyc - inaczej nie da sie tego nauczyc". Unikaty
zeszly z handlu (wpis wyzej), wiec potrzebna legalna droga do ich
wytwarzania.
**Zmiana:** (1) UniqueGear - kopia listy prefiksow unikatow z
CrashScribe/Mends (komentarz krzyzowy: aktualizowac razem);
(2) przetop unikatu w zakladce Smelt uczy wzoru: id trafia do
ArmouryBehavior.UniqueLore (save: "arm_uniq_lore"), komunikat "You have
studied the make of X"; (3) nowa opcja menu kuzni "Forge a studied
masterwork" (nad ksiega zamowien): lista nauczonych wzorow z oplata
kuzienna, wybor -> normalny przeplyw kucia (AskTempo -> materialy,
stamina, czas, jakosc) - unikat kuje sie jak kazda rzecz jego tieru.
Opcja wyszarzona z podpowiedzia, poki niczego nie nauczono. Jeff gra
NOWA GRE - zgodnosc ze starymi save'ami niewymagana (pole i tak
bezpieczne przy braku wpisu).
**Ryzyko / co sprawdzic:** pokonac bohatera z imiennym sprzetem, wziac
pancerz, przetopic w Smelt -> komunikat nauki; w menu kuzni opcja
aktywna, kucie startuje projekt jak zwykle. Egzemplarz ZUZYWA sie przy
nauce (przetop!) - swiadomy koszt.
**Status:** WGRANE

## 2026-08-30 — Valyrianska stal: nazwa, wytop 2x drozszy, przetop 50%/25%
**Mod:** Armoury | **Pliki:** `Armoury/src/ValyrianSteel.cs` (nowy), `Armoury/src/Recipes.cs`, `SubModuleMain.cs`, `ArmouryBehavior.cs`
**Problem:** Jeff: "melting pancerzy koni daje za duzo thamaskene steel
[= najwyzsza stal kuzni]; zmienmy nazwe na Valyrian Steel; smelting ma
dawac o polowe mniej niz produkcja, valyrianska DODATKOWO przez 2
(18/18/18 -> 9/9/4); wytop valyrianskiej 2x drozszy - ma byc bardzo
trudna do stworzenia".
**Zmiana:** (1) item Iron6 ("Thamaskene Steel") przemianowany w runtime
na "Valyrian Steel" - widac wszedzie w UI; (2) Recipes.SmeltYield:
udzial zwrotu przyciety twardo do 50% (koniec z 90% przy wysokim
Smithing; dotyczy tygla SmeltTab, przetopu w menu kowala i odzysku
TakeApartSalvage), a Iron6 po przycieciu dzielony jeszcze przez 2 -
przyklad Jeffa daje dokladnie 9/9/4; (3) postfix na
DefaultSmithingModel.GetRefiningFormulas: formula z wyjsciem Iron6
pobiera PODWOJNE wsady (bylo 2x Iron5 + 1x wegiel, jest 4x + 2x).
BannerKings nie nadpisuje GetRefiningFormulas (sprawdzone) - patch
na modelu bazowym jest jedyna sciezka, bez podwojnego mnozenia.
**Ryzyko / co sprawdzic:** log Armoury "ValyrianSteel: ... przemianowana"
i "wytop 2x drozszy"; w kuzni zakladka Refine pokazuje 4x stal + 2x
wegiel za 1x Valyrian Steel; przetop uprzezy konskiej pokazuje polowki.
Ustawienie SmeltingReturnShare dziala dalej, ale sufit to teraz 50%.
**Status:** WGRANE

## 2026-08-30 — Brama kucia: klingi lore (Needle, Dark Sister...) juz nie od startu
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "troszke bez sensu, ze moge kuc miecz Nocnego Krola
czy inne rzeczy od poczatku; wylaczmy kucie T5/T6, kuj od T1 az sie
udostepnia". Zrodlo: ROT daje is_default="true" 140 czesciom tieru 5 -
w tym klingom imiennym (Needle, Lightbringer, Widow's Wail, Dark
Sister, Tempest) - kazdy swiezy kowal mogl je skladac od pierwszego
dnia (stad tez "50 mieczy Aryi" w obiegu).
**Zmiana:** mend LoreForgeGate (start sesji): czesci PieceTier >= 5
traca IsGivenByDefault - trzeba je odblokowac normalna nauka kowalska.
Vanilla otwiera nowe czesci od NAJNIZSZEGO dostepnego tieru, wiec
klingi lore wypadaja na samym koncu progresji. Czesci T4- zostaja
darmowe jak w ROT (w tym valyrianskie T4 - te ograniczy drozszy wytop
stali, osobny wpis).
**Ryzyko / co sprawdzic:** log "Mends: brama kucia - 140 czesci T5+
przestalo byc darmowych"; w kuzni free-build lista klingow T5 pusta
do czasu odblokowan; czesci JUZ otwarte recznie przez gracza
(_openedPartsDictionary w save) pozostaja otwarte. Zapisane projekty
kutych mieczy nie znikaja (gotowe itemy nietykane).
**Status:** WGRANE

## 2026-08-30 — Unikaty sa unikatowe: sprzet imiennych bohaterow poza handlem i przydzialem
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "kazdy piechur biegal z mieczami/pancerzami bohaterow;
nie moze byc 25 pancerzy Brienny - maja je tylko te postacie!". Analiza
danych ROT: sprzet imienny (Brienne, Ramsay, Rhaegar, Cersei, Stannis,
Hound, Blackfyre, Nocny Krol...) w wiekszosci NIE mial flagi
is_merchandise=false - targi miast LOSOWALY go jak zwykly towar,
a DTE ubieral w niego szeregowych z lupow.
**Zmiana:** mend UniqueWares (start sesji): (1) lista prefiksow id
sprzetu imiennego (z analizy: itemy noszone wylacznie przez lordow,
nigdy przez jednostki; mundury domow typu Baratheon/Valyrian CELOWO
poza lista - to uniformy oddzialow); (2) NotMerchandise=true - targi
przestaja je losowac; (3) jednorazowa czystka polek wszystkich miast
z istniejacych kopii. Do tego prefix UniqueWard na DTE DoAssignAsync:
unikaty nie wchodza do puli przydzialu - zaden szeregowy ich nie
zalozy. NIETYKANE: ekwipunek bohaterow (Brienne nosi swoje - DTE
bohaterom sprzetu nie rusza), stash i sakwy gracza (zdobyczny
egzemplarz zostaje, mozna nosic samemu / dac towarzyszowi RECZNIE).
**Ryzyko / co sprawdzic:** log "Mends: unikaty imienne - N itemow
zeszlo z handlu, M kopii zdjetych z targow"; w sklepach nie ma juz
pancerzy Brienny/Ramsaya; po bitwie z bohaterem jego sprzet w lupach
zostaje, ale piechota go nie wklada. Bronie lore (Needle itd.) nie
istnieja w ROT jako itemy - sa KUTE z czesci; ograniczenie kucia
T5/T6 idzie osobnym wpisem.
**Status:** WGRANE

## 2026-08-30 — Egzotyczne wierzchowce tylko dla wlasnej kultury (koniec kawalerii Polnocy na wielbladach)
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "wielblady, rydwany, slonie - nie moga na nich jezdzic
wojskowi np. Polnocy; karawany okej, moga sprzedac". Zrodlo: DTE
(PartyEquipmentDistributor.GenerateHorseAndHarnessList) zbiera do
przydzialu WSZYSTKIE konie z taboru - takze wielblady z lupow po
poludniowcach - i sadza na nie jezdzcow bez pytania o kulture. Nasze
Stables juz wczesniej blokowaly ZAKUP egzotykow poza kultura; zostala
droga lupow.
**Zmiana:** mend CamelCulling - postfix na GenerateHorseAndHarnessList:
z listy przydzialu schodza wierzchowce o id zawierajacym
camel/chariot/elephant, ktorych kultura itemu (dane ROT: wielblady
i rydwany = aserai/Dorne, slonie = volantine/Essos) rozni sie od kultury
partii (lider -> frakcja). Zwierze zostaje w taborze jako towar -
mozna sprzedac, nikt nie wsiada. Dotyczy i AI, i gracza (partia Starka
nie posadzi ludzi na wielbladach; Dornijczycy u siebie jezdza normalnie).
**Ryzyko / co sprawdzic:** log CrashScribe "Mends: egzotyczne
wierzchowce... DTE nie sadza na nich obcych"; po bitwie z poludniowcami
kawaleria Polnocy zostaje na koniach, wielblady leza w lupach.
**Status:** WGRANE

## 2026-08-30 — Sen: korekta Jeffa - juz pierwsza zarwana noc karze (-25%/-25%)
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff dopowiedzial do nowej zasady snu: pierwsza nieprzespana
noc NIE uchodzi na sucho - "morale -25% i predkosc -25%".
**Zmiana:** tabele kar {0,25,40,90}% predkosci i {0,25,40,95}% morale;
kary naliczane juz od dlugu 1 (bylo od 2); komunikat pierwszej nocy
podaje kary. Reszta zasady (baza 6 h, splata 9/15/21 h, zapasc przy 3)
bez zmian.
**Ryzyko / co sprawdzic:** po jednej zarwanej nocy tooltip predkosci
pokazuje "Sleepless nights -25%".
**Status:** WGRANE

## 2026-08-30 — Sen: eskalacja dlugu (1 noc wolno, 2 lamie, 3 usypia kolumne)
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`, `Armoury/src/Settings.cs` (+McmSettings wygenerowane)
**Problem:** Jeff podal nowa zasade snu: "nieprzespanie jednej nocy jest
mozliwe; drugiej - bardzo powazny minus do morale i predkosci; trzeciej -
wojsko zasypia i zatrzymuje sie tam gdzie jest, predkosc -90%, morale
-95%. Jedna zarwana noc = trzeba przespac 6+3 h, dwie zarwane = 6+9 h."
Stary system: dlug 0..5, kary lagodne (predkosc do -30%, morale do -15
pkt), dobra noc splacala dwie zle, baza snu 5 h.
**Zmiana:** (1) baza snu 6 h (SleepHoursNeeded 5->6); (2) dlug 0..3;
(3) odsetki wg wzoru 3*(2*dlug-1): pelna splata wymaga 9 / 15 / 21 h
snu JEDNYM rachunkiem (CreditRest zeruje dlug dopiero przy pelnej
sumie; przespanie samej bazy nie dodaje dlugu, ale stary stoi);
(4) kary: dlug 1 = nic, dlug 2 = predkosc -40% i morale -40%, dlug 3 =
predkosc -90% i morale -95% (morale teraz PROCENTOWO przez AddFactor,
nie punktowo); (5) przy wejsciu w dlug 3 partia gracza staje w miejscu
(SetMoveModeHold, raz - poza osada i bitwa); (6) pasek snu w menu obozu
celuje w pelna sume (bezpiecznik snu wydluzony, bo 21 h snu to wiecej
godzin zegara przy dziennym snie); (7) stare save'y z dlugiem 4-5
przycinane do 3.
**Ryzyko / co sprawdzic:** przy zapasci morale ~2-3 = PONIZEJ progu
dezercji (10) - wojsko bedzie dezerterowac, poki nie odespane (swiadome,
Jeff podal -95%); sluzba ROT nadal bez dlugu; umarli bez dlugu; AI bez
zmian (lordowie i tak obozuja nocami). Test: zarwac 2 noce -> zolty
komunikat przy 1., czerwony przy 2. (kary widoczne w tooltipie predkosci
"Sleepless nights"), 3. noc -> kolumna staje, sen w obozie przez ~21 h
czysci wszystko ("The debt is paid in full").
**Status:** WGRANE

## 2026-08-30 — Valyrianska zasada T6 takze w autokalkulacji bitew
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "AI walczy z AI na zasadzie autokalkulacji - trzeba
te przewage zaznaczyc". Poprzedni mend (ValyrianWard) dzialal tylko na
polu bitwy; w symulacji (bitwy AI vs AI, "Send Troops", poscig)
Wedrowcy i Nocny Krol nadal padali od zwyklej stali.
**Przyczyna:** symulacja liczy obrazenia w CombatSimulationModel.
SimulateHit, z pominieciem Agent.RegisterBlow.
**Zmiana:** mend ValyrianWardSim - postfix (Priority.Last, PO faktorach
BannerKings) WYLACZNIE na bazowym DefaultCombatSimulationModel.
SimulateHit (landowy overload). ROTCombatSimulationModel deleguje do
bazowego, wiec latanie obu cieloby PODWOJNIE (15% x 15% = 2%) - stad
jeden punkt. Warunek: trafiany = Wedrowiec/NK (WalkerBlood), bijacy
bez broni T6+ we wzorcach bojowych (BestWeaponTier: max tier broni
z BattleEquipments, tarcze pominiete, pulapka (int)Tier+1) -> wynik
ciety do 15% (min 1). Smoki celowo wolne od ciecia: ROT nadpisuje ich
wynik PO naszym postfixie (DragonDamageScaling) - smoczy ogien pali
Innych takze w symulacji, zgodnie z lore.
**Ryzyko / co sprawdzic:** kronika wojen CrashScribe - armie zywych
lordow przestaja wygrywac z horda przez sama mase w autokalku (jesli
nie maja elit z bronia T6); gracz tez nie wezmie NK "za darmo" przez
Send Troops. Log: "Mends: valyrianska zasada T6 dziala tez w
autokalkulacji...". Zalozenie: kazdy aktywny model symulacji deleguje
do Default (ROT tak robi; BK tez tylko postfixuje Default) - gdyby
przyszly mod podmienil model bez delegacji, zasada zniknie z symulacji
(log przy starcie zdradzi, ze patch wszedl, ale warto pamietac).
**Status:** WGRANE

## 2026-08-30 — Valyrianska zasada T6: Wedrowcy i Nocny Krol odporni na zwykla stal
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "Biali Wedrowcy i Nocny Krol maja odpornosc na bron,
ktora nie jest valyrianska stala - u nas bron T6; kazda bron ponizej
powinna zadawac mocno ograniczone obrazenia". Dotad kazdy chlop z widlami
ranil Wedrowca jak czlowieka.
**Przyczyna:** ROT nie ma zadnej mechaniki odpornosci Innych.
**Zmiana:** mend ValyrianWard - prefix (Priority.High) na
Agent.RegisterBlow, jedyne gardlo wszystkich obrazen (melee, pociski,
taranowanie koniem; takze dodatkowe rejestracje RBM z cleave). Cel:
TYLKO Wedrowcy (jednostki whitewalker2/3/4, szablon
ROTuniqueleader_whitewalker) i bohaterowie kultury whitewalker (Nocny
Krol; takze gracz-Other - spojnie w obie strony). Zwykle wighty padaja
od wszystkiego jak dotad. Tier ciosu: melee = przedmiot ze slotu
atakujacego (Blow niesie slot, nie item; PULAPKA (int)Tier+1), pocisk =
bron miotajaca w rece strzelca (luk/kusza/oszczep - strzaly nie bywaja
valyrianskie, liczy sie narzedzie), piesc/kopyto/upadek = tier 0.
T6+ bije normalnie; ponizej zostaje 15% obrazen (min 1).
**Ryzyko / co sprawdzic:** w bitwie z horda Wedrowiec przyjmuje grad
ciosow T3-T5 niemal bez szwanku, a bron T6 (np. kuta w naszej kuzni)
tnie go normalnie; log CrashScribe: "Mends: valyrianska zasada T6...".
Uwaga 1: smoczy ogien ROT (jesli zadaje obrazenia bez broni) tez
zostanie ograniczony do 15% - lore mowi inaczej, ale Jeff gra Polnoca;
do poprawki, gdy smok wejdzie do gry. Uwaga 2: autokalkulacja bitew
NIE zna tej odpornosci (symulacja liczy statystyki) - z Wedrowcami
walczyc recznie.
**Status:** WGRANE

## 2026-08-30 — Umarli nie znaja zmeczenia: stamina i postura RBM bez dna dla wightow
**Mod:** Armoury | **Pliki:** `Armoury/src/BattleWind.cs`
**Problem:** Jeff: "i nie maja staminy oraz nie musza spac w nocy?".
Audyt wykazal: sen juz zalatwiony (Undead.Party w NightRest, 27.08),
nasza zadyszka/rany/krwawienie tez (FieldCraft/BrokenMen), jedzenie
zalatwia sam ROT (DoesPartyConsumeFood=false dla klanow Wedrowcow -
nie gloduja, nie dezerteruja). Zostala JEDNA luka: pula staminy
i postury z RBM (RBMAI.Stance) - liczona z Atletyki dla KAZDEGO agenta.
Wight z Atletyka 50 lapal zadyszke i staggery po zbitej posturze jak
zywy chlop; BattleWind zarzadzal tylko bohaterami.
**Przyczyna:** StaminaInitPostfix/PostureInitPostfix wychodzily przy
hero == null - szeregowi (i bohaterowie-umarli dopiero po nich) szli
czystym RBM.
**Zmiana:** w obu postfixach, PRZED logika bohaterow: agent umarly
(Undead.Character - kultura klanu ROTclan_126 lub sito nazw) dostaje
stamina/postura = max = 1 000 000, regen 1000/tick; zadnego Profile,
wiec tick regen idzie oryginalem RBM. Obejmuje tez Nocnego Krola
i Bialych Wedrowcow (dotad jechali na puli z Endurance jak zwykli
bohaterowie). Konie bez zmian.
**Ryzyko / co sprawdzic:** dziala tylko przy wlaczonym Battle Stamina
w MCM Armoury (caly BattleWind jest za ta bramka) - wylaczenie
mechaniki wylacza tez wyjatek umarlych, co jest spojne (bez naszej
mechaniki zostaje czysty RBM). W bitwie z horda: wighty nie zwalniaja
atakow po dlugiej wymianie, nie da sie ich zmeczyc w klinczu; zywi
lapia zadyszke po staremu.
**Status:** WGRANE

## 2026-08-30 — Umarli nie znaja strachu: panika wylaczona armii Nocnego Krola
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff (po analizie taktyk AI Innych): "nieumarli nie moga
panikowac, sa nieumarli - trzeba wylaczyc panike dla nich". ROT nie ma
ZADNEGO wlasnego kodu bitewnego dla kultury whitewalker - wighty
(angry_wight, level 11, bez tarcz) uzywaly vanillowego morale i przy
stratach panikowaly i uciekaly z pola jak chlopi.
**Przyczyna:** brak latki w ROT; silnik pyta
BattleMoraleModel.CanPanicDueToMorale(agent) (CommonAIComponent.CanPanic,
pierwsza linia) i dla wightow dostawal true jak dla kazdego chlopa.
**Zmiana:** mend DeadDontPanic - postfix na KAZDA zaladowana
implementacje CanPanicDueToMorale (skan assembly po potomkach
BattleMoraleModel; SandboxBattleMoraleModel, CustomBattle, ewentualne
modowe podmiany; RBM modelu nie podmienia - sprawdzone w zrodlach).
Agent kultury whitewalker nigdy nie dostaje prawa do paniki - silnik
wtedy trzyma morale na 0.01 i jednostka walczy do konca (ta sama
sciezka, ktorej vanilla uzywa dla perka Loyalty and Honor). Dziala
takze dla wightow w armii gracza, gdyby Jeff gral po stronie Innych.
Wskrzeszania W TRAKCIE bitwy nadal nie ma (i nie bylo w ROT) -
nekromancja pozostaje mechanika mapy kampanii.
**Ryzyko / co sprawdzic:** bitwa z horda NK - wighty nie rzucaja sie
do ucieczki mimo strat (bitwy beda dluzsze i krwawsze, do ostatniego
trupa); w logu CrashScribe linia "Mends: umarli nie znaja strachu -
panika wylaczona kulturze whitewalker (N implementacji modelu morale)"
z N >= 2. Zolnierze ZYWI panikuja po staremu.
**Status:** WGRANE

## 2026-08-30 — Panel kucia: naprawiona semantyka wiersza statow (+N jak przy mieczach) + diagnoza
**Mod:** Armoury | **Pliki:** `Armoury/src/CraftPopup.cs`
**Problem:** Jeff (Balanced Ravens' Teeth): "mam balanced, ale nie mam
graficznego +2 jak w mieczach". Dekompilacja widgetu wiersza
(CraftedWeaponDesignResultListPanel) pokazala SEMANTYKE: InitValue ma byc
BAZA, ChangeAmount roznica - widget sam animuje baze -> baza+roznica
i pokazuje zielone "+N". My podawalismy InitValue = wartosc JUZ
z bonusem - przy niezerowej roznicy widget doliczalby bonus DRUGI raz,
a wyswietlana wartosc startowa byla zmodyfikowana (bez animacji plusa
z bazy). Ustalone tez po drodze: RBM patchuje ModifyDamage (Damage
dziala u niego PROCENTOWO), ModifySpeed/MissileSpeed zostawia vanilla -
wiec Balanced (+5 speed wg RBM XML) MA dawac widoczny plus przy Fire Rate.
**Zmiana:** (1) AddProp podaje (baza, roznica) zgodnie z widgetem;
(2) twarda linia diagnostyczna w logu przy KAZDYM popupie: "CraftPopup
diag: mod=<id> dmg= spd= msl= hp= pm= | Stat base->mod; ..." - nastepne
kucie Jeffa rozstrzygnie na liczbach, gdyby plusy dalej nie wchodzily.
**Ryzyko / co sprawdzic:** wykuc luk z jakoscia -> Fire Rate startuje od
bazy i po chwili animuje sie w gore z zielonym +N; w Armoury.log linia
"CraftPopup diag" z polami modyfikatora.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — Kwatermistrz przyjmuje tez KONIE (ten sam blad co ze strzalami)
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** Jeff ze screenem ("Horse 52/83"): "wrzucam konie, kwatermistrz
mowi ze wszystko na zielono, a jak zabieram - ze brakuje; to samo co
z lukami wczesniej". Ta sama bramka, ktora 29.08 wycinala amunicje:
NoteDeposit przyjmowal wylacznie itemy z weapon/armor componentem -
KON (HorseComponent) wypadal z rejestru wymian, wiec wklad konski nie
szedl ani do wymiany za gorsze, ani do przyjecia do kompletu (need =
jezdzcy). Wisial wiecznie jako wlasnosc gracza: polki pelne (meldunek
zielony), ksiega gracza pelna (po zabraniu - znow braki).
**Przyczyna:** bramka `!weapon && !armor -> return` w NoteDeposit.
**Zmiana:** konie (HasHorseComponent) wchodza do rejestru jak wszystko
inne: wymiana za gorszego konia (tier/wartosc), reszta do kompletu
(need = liczba jezdzcow), nadwyzka zostaje graczowi. Uprzeze juz
wchodzily (maja ArmorComponent). Zasada skilli dziala (Riding przez
AnyoneCanUse, gdy kon ma Difficulty).
**Ryzyko / co sprawdzic:** wrzucic konie przy brakach 52/83 -> po
zamknieciu ekranu znikaja z listy gracza, licznik rosnie do kompletu,
komunikat "go to the men"; nadwyzka ponad 83 zostaje na liscie.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — PRAWO PRZEPRAWY: The Twins nie przepuszczaja wrogow (nowy CrossingLaw)
**Mod:** Armoury | **Pliki:** `Armoury/src/CrossingLaw.cs` (nowy),
`Settings.cs`, `McmSettings.cs` (gen), `ArmouryBehavior.cs`
**Problem:** Jeff ze screenem spod Twins: "jak to mozliwe, ze jako wroga
armia moge przejsc przez Twins?! Ten, kto ma zamek, przepuszcza
sojusznikow, a wrogow nie!". Bliznaki (ROT_town3) to JEDYNY most przez
Zielona Galaz - Freyowie zyli z bramkowania przeprawy, a gra traktuje
most jak zwykly teren przechodni.
**Zmiana:** nowy CrossingLaw (tick co 0.5 s): partia GRACZA w stanie
WOJNY z wlascicielem przeprawy nie wjedzie w jej strefe (CrossingRadius,
domyslnie 3) - straz mostu wypycha ja SetMoveGoToPoint na ostatnia
bezpieczna pozycje (zero teleportow, naturalny ruch; po wczytaniu save'a
w strefie - odepchniecie prosto od zamku). Sojusznicy, neutralni i
wlasna frakcja przechodza. Komunikat: "The Twins bar the crossing...
Take the castle, make peace, or go by sea" (throttle 5 s). Pomijane:
gracz w osadzie, w bitwie, w cudzej armii (decyduje jej dowodca).
Pokretla MCM: Crossing Law Enabled, Crossing Radius. Lista przepraw
w kodzie (na razie The Twins; kolejne mosty-warownie do dopisania).
ETAP 1 CELOWO TYLKO GRACZ: wypychanie wrogich armii AI grozi zapetleniem
ich pathfindingu (armia klinuje sie o strefe w nieskonczonosc) - decyzja
po testach.
**Ryzyko / co sprawdzic:** podjechac pod Twins bedac w wojnie z
wlascicielem -> partia zawraca sama, komunikat; po pokoju/zdobyciu -
przejazd wolny; strefa nie moze lapac za rzeka (jesli promien 3 za
szeroki/waski - suwak w MCM).
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — Legendarny luk bez plusow: RBM chowal bonus w Fire Rate, ktorego nie pokazywalismy
**Mod:** Armoury | **Pliki:** `Armoury/src/CraftPopup.cs`, `Armoury/src/QualityRich.cs`
**Problem:** Jeff ze screenem (Legendary Ravens' Teeth Longbow): "wykulem
legendary luk i w ogole nie ma plusow na zielono, plus jest wiecej statystyk
niz te". Log dowodzi, ze modyfikator BYL (jakosc=legendary_bow) - ale RBM
NADPISUJE item_modifiers i lukom daje damage=0, missile_speed=0, a caly
bonus jakosci wsadza w speed=+15 (szybkostrzelnosc) i hit_points=+30.
Nasz popup pokazywal dla luku akurat te staty, ktorych RBM nie rusza
(Missile Damage/Speed, Accuracy) - stad zero plusow przy dzialajacym modzie.
Do tego nasz QualityRich (dopisywanie brakujacych statow po RBM) mial
bramke `Damage==0 -> to nie bron` - RBM-owe luki (damage=0) wypadaly
z wzbogacenia w calosci.
**Zmiana:** (1) popup dla lukow/kusz pokazuje tez **Fire Rate**
(SwingSpeed + ModifySpeed) - tam siedzi glowny bonus jakosci w swiecie
RBM; (2) bramka QualityRich poznaje modyfikator broni po JAKIMKOLWIEK
polu bojowym (Damage/Speed/MissileSpeed/HitPoints), wiec luki i kusze
dostaja brakujacy MissileSpeed wedle stopnia jakosci jak reszta broni.
**Ryzyko / co sprawdzic:** wykuc luk z jakoscia - popup ma 5 wierszy,
Fire Rate z zielonym plusem (legendary +15), Missile Speed z plusem od
QualityRich; log "QualityRich: N modyfikatorow wzbogaconych" ma urosnac.
**Status:** ZBUDOWANE - do wgrania z pakietem

## 2026-08-30 — Rycerstwo: najwyzszy tier przyjmuje WAR **LUB** NOBLE Mount
**Mod:** Armoury | **Pliki:** `Armoury/src/Stables.cs`
**Problem:** Jeff ze screenem ("Upgrade to Northern Mounted Warlord -
Required: Noble Mount (You have none)" przy 39 koniach): "rycerstwo na
ostatni tier wymaga War Mount lub Noble Mount". Twarde `tier>=6 -> tylko
NobleHorse` blokowalo awanse na ostatni tier, gdy w taborze staly same
konie bojowe.
**Zmiana:** nowa zasada RequiredMountFor: t6+ siada na SZLACHETNYM, jesli
stajnia go ma, a gdy nie ma - wystarczy porzadny kon BOJOWY (war);
t4-5 jak dotad war; nizej zwykly. Getter (ekran gracza, vanilla sam
zdejmuje konia) liczy po taborze gracza; sciezki AI (FilterTargets/
PayInHorses) licza po taborze WLASNEJ partii, nie gracza.
**Ryzyko / co sprawdzic:** awans t6 z samymi war horse - przechodzi
i zdejmuje konia bojowego; z noble w taborze - zdejmuje szlachetnego.
**Status:** ZBUDOWANE - do wgrania z pakietem

## 2026-08-30 — Panel kucia: TYLKO RAZ, martwe Done ubite, jakosc takze dla zlecen "van"
**Mod:** Armoury | **Pliki:** `Armoury/src/CraftPopup.cs`, `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff ze screenem (seria Albion I-IV): "po co drugi raz popup
z wykuciem - to juz bylo, i nie dziala Done" oraz "popup nie pokazuje
+1/-1 przy lukach/pancerzach, nie ma Legendary ani spartaczonych".
Dwie przyczyny: (1) odbior KILKU wyrobow naraz wolal Show per sztuka -
kazdy kolejny popup NADPISYWAL warstwe Gauntlet poprzedniego bez jej
zamkniecia; martwe warstwy trzymaly restrykcje inputu i Done klikalo
w proznie. (2) vanillowe CreateCraftedWeaponInFreeBuildMode przyjmuje
modyfikator jakosci Z ZEWNATRZ (domyslnie null) - nasza sciezka zlecen
"van" dostawala golego, wiec kazdy wyrob wychodzil pospolity bez rzutu,
a popup nie mial czego pokazac (mod=null => zero roznic, tytul bez jakosci).
**Zmiana:** (1) panel otwarty = kolejne wyroby ida BEZ okna i bez dzwieku
(sa w komunikatach i logu); pas bezpieczenstwa Close() przed kazdym
otwarciem - nigdy dwoch warstw; (2) jakosc "van" rzucana RAZ, PRZY
KOWADLE (nasza wierna formula vanilla z kuznia-1do1 krok 1), jedzie
z projektem i wraca przy dostawie - zadnego drugiego rzutu; (3) tytul
panelu z jakoscia ("Fine Albion IV"), roznice +/- licza sie od modyfikatora.
**Ryzyko / co sprawdzic:** wykuc bron po vanillowemu -> po dostawie panel
z jakoscia w tytule i roznicami; odebrac KILKA zlecen naraz -> JEDEN
panel, Done dziala, reszta wyrobow w komunikatach.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — Czeladnik przy miechu: WYBOR postaci + nauka kowalstwa
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`, `Helper.cs`,
`Forge.cs`, `ArmouryBehavior.cs`
**Problem:** Jeff: "trzeba dac opcje w forge: wybierz pomocnika kowala,
wybieram postac, ona uczy sie kowalstwa, dostaje XP i mi pomaga" -
dotad pomocnik byl dobierany automatycznie (najlepszy kowal druzyny,
prog 20 skilla) i nie dalo sie go wskazac.
**Zmiana:** (1) nowa opcja w menu kuzni "Choose your helper at the
bellows" - okno wyboru (MultiSelectionInquiry): Best available (auto) /
Work alone / lista towarzyszy z ich Smithing (aktualny oznaczony
[current]); wybor zapisywany w save (arm_smith_helper). (2) Helper.Find
honoruje wybor: wskazana postac pomaga BEZ progu skilla (terminator od
zera), "none" = sam, auto = po staremu; wskazanego nieobecnego zastepuje
auto. (3) nauka: XP przy rozpoczeciu (bylo) + XP za caly projekt przy
Finish: ProjectXp * (15% podlogi terminatora + 40% * wklad-ulga).
Ulgi bez zmian: do -40% staminy i -30% czasu przy skillu 150.
**Ryzyko / co sprawdzic:** opcja widoczna gdy masz towarzysza; wybrac
nowicjusza -> po ukonczeniu projektu jego Smithing rosnie (log
"Pomocnik:"); ulgi liczone od jego skilla.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — RATUNEK: opcje wyjscia z wait-menu ruszaja zegar (deadlock White Harbor)
**Mody:** Armoury, RealisticCaptivity | **Pliki:** `SmithMenu.cs`,
`HideoutPurge.cs`, `Work.cs`
**Problem:** Jeff ze screenem (White Harbor, kolejka kowala 185h): "zawislo
sie - nie moge uruchomic czasu i nie moge Step away". Diagnoza: po
WCZYTANIU SAVE'A w srodku wait-menu init menu (z StartWait) nie biegnie -
czas stoi na Stop i nie daje sie odpauzowac, a nasze opcje wyjscia z
korekty ExitToLast czekaja na TICK, ktory bez plynacego czasu nigdy nie
tyknie. Deadlock: ani czekac, ani wyjsc. (Armoury.log bez bledow - tick
zdrowy, tylko uspiony.)
**Zmiana:** wszystkie 4 opcje wyjscia (Put the work aside / Step away /
Call the search off / Enough of this toil) oprocz flagi RUSZAJA ZEGAR
same: TimeControlMode = StoppablePlay + GameMenu.StartWait() w try/catch.
Tick budzi sie w nastepnej klatce i przelacza jak w korekcie.
**Ryzyko / co sprawdzic:** wczytac save zrobiony w srodku czekania u
kowala -> klik "Step away" wychodzi (najpierw ruszy czas na ulamek
sekundy); zwykle wyjscia bez zmian. Jesli po load ktos chce CZEKAC dalej:
wyjsc opcja i wejsc ponownie (init ze StartWait naprawi zegar).
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry (Jeff: ESC ->
Save & Exit, watcher wgrywa, wczytaj - klik zadziala)

## 2026-08-30 — TroopSelfMend: procent uszkodzen dziennie zamiast plaskich 10 sztuk
**Mod:** Armoury | **Pliki:** `Armoury/src/TroopSelfMend.cs`, `Settings.cs`, `McmSettings.cs` (gen)
**Problem:** Jeff: "10 sztuk dziennie to bez sensu - niech naprawiaja
procent uszkodzen, calosc trwa ~10 dni, a jak chce szybciej, place sam".
Plaska liczba nie skalowala sie: przy 250 wojska i setkach zuzytych sztuk
samonaprawa trwala wiecznosc, przy malej druzynie byla ekspresem.
**Zmiana:** pokretlo TroopSelfMendPerDay (sztuki) zastapione
TroopSelfMendPercentPerDay (domyslnie 10): kazdego dnia postoju w MIESCIE
wojsko naprawia 10% CALEJ puli zuzytych sztuk magazynu (min. 3 szt., zeby
ogon nie wisial wiecznie), z wlasnego zoldu. Pelny remont = ~100/procent
dni postoju NIEZALEZNIE od wielkosci armii. Platny kowal ("Send the men's
worn gear to the smith") bez zmian = natychmiast za zloto gracza.
Stare pole usuniete (zapis MCM Jeffa nie zawieral klucza - sprawdzone).
**Ryzyko / co sprawdzic:** log "TroopSelfMend: wojsko naprawilo N sztuk"
- N ma rosnac z wielkoscia zaleglosci (10% puli), nie stac na 10.
**Status:** WGRANE (30.08, gra zamknieta)

## 2026-08-30 — Dzialka kapitanska 40% -> 30% (historyczne "thirds")
**Mod:** Armoury | **Pliki:** `Armoury/src/Settings.cs`, `McmSettings.cs` (gen)
**Problem:** Jeff po rozmowie o realiach kompanii najemnych ("to dajemy
30%"): dzialka gracza z lupow zdjetych przez wojsko z zabitych ma
odpowiadac sredniowiecznej trzeciej czesci kapitana.
**Zmiana:** domyslne PlayerLootSharePercent 40 -> 30. Zapis MCM Jeffa
nie zawiera klucza (sprawdzone), default wchodzi od nastepnego startu.
**Ryzyko / co sprawdzic:** log kwatermistrza po bitwie: "N szt. (30%)
z magazynu wojska czeka na ekran lupow".
**Status:** WGRANE (30.08, gra zamknieta)

## 2026-08-30 — SlowMuster: odnawianie ochotnikow sciete z 50% na 25%
**Mod:** Armoury | **Pliki:** `Armoury/src/Settings.cs`, `McmSettings.cs` (gen)
**Problem:** Jeff: "obnizamy dostepnych poborowych jeszcze o 50% - caly
czas za duzo jest wojska do poboru w miastach i wioskach".
**Zmiana:** domyslne VolunteerRegenPercent 50 -> 25 (cwierc vanillowego
tempa dziennego odnawiania slotow u notabli). Zapis MCM Jeffa NIE zawiera
tego klucza (sprawdzone w Armoury.json), wiec nowy default wchodzi u niego
od nastepnego startu gry.
**Ryzyko / co sprawdzic:** UWAGA na nature pokretla: to TEMPO odnawiania,
nie wielkosc puli - sloty (do 6/notabla) nadal Z CZASEM dojda do pelna,
tylko 4x wolniej niz vanilla. Jesli po kilku dniach gry miasta wciaz
pekaja od rekrutow, nastepny krok to CAP samej puli (np. polowa slotow
zawsze pusta) - osobna zmiana, na haslo.
**Status:** WGRANE (30.08, gra zamknieta)

## 2026-08-30 — KOREKTA CaptiveRags: lachmany w DUZYM podgladzie 3D, ikony przywrocone
**Mod:** Armoury | **Pliki:** `Armoury/src/CaptiveRags.cs`
**Problem:** Jeff ze screenem: "zmieniles ich ikony na takie same, ale
widok 3D zostal w pancerzu - a mi chodzilo, aby widok 3D byl w lachmanach".
Pierwsza wersja patcha celowala w PartyCharacterVM.GetCharacterCode -
a to zrodlo MINIATUREK wierszy (CharacterImageIdentifierVM Code), nie
duzego modelu. Efekt dokladnie odwrotny do zamowionego.
**Przyczyna:** duzy podglad to CharacterTableauWidget w PartyScreen.xml
z DataSource={SelectedCharacter} (HeroViewModel na PartyVM), wypelniany
przez PartyVM.RefreshCurrentCharacterInformation (FillFrom + SetEquipment
z SZABLONU jednostki) - zupelnie inna sciezka niz ikony.
**Zmiana:** postfix przeniesiony na PartyVM.RefreshCurrentCharacter
Information: gdy CurrentCharacter to JENIEC-szeregowy, po wypelnieniu
modelu podmieniamy mu ekwipunek (SelectedCharacter.SetEquipment) na puste
sloty + najtansze lachmany. GetCharacterCode NIETKNIETE - ikony listy
wracaja do oryginalnych portretow w zbrojach.
**Ryzyko / co sprawdzic:** klik na typ jenca -> DUZY model w lachmanach,
ikona wiersza po staremu (w pancerzu); zwykli zolnierze i bohaterowie-
jency bez zmian.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry (biezaca sesja
Jeffa chodzi na wersji z podmienionymi IKONAMI - po restarcie wroci
porzadek)

## 2026-08-30 — Tabor Spoils (Baggage Train) wyciety patchem na stale
**Mod:** Armoury | **Pliki:** `Armoury/src/BattlefieldLaw.cs`
**Problem:** uzupelnienie prawa zachowania lupu; Jeff wybral Droge 2
("robimy to"). Spoils of War robi WLASNY, drugi ekran bagazy ze zdjecia
zawartosci wozow wroga z POCZATKU bitwy - rownolegle vanilla dzieli TE SAME
wozy miedzy zwyciezcow wg wkladu. Te same rzeczy wpadaly dwa razy, bez
ogladania na sojusznikow. Checkbox "Enable baggage train looting" w MCM
Spoils wraca ON przy kazdym presecie/resecie.
**Zmiana:** postfix na getter RealisticLoot.Settings.MCMSettings
.EnableBaggageTrain -> zawsze false (gdy BattlefieldLawEnabled). Caly kod
taboru Spoils spi u zrodla; wozy pokonanych dzieli wylacznie vanilla.
**Ryzyko / co sprawdzic:** po bitwie NIE pojawia sie drugi ekran
"przeszukujesz tabor"; w logu "tabor Spoils (Baggage Train) wyciety na
stale". Wylaczenie naszego BattlefieldLaw w MCM przywraca Spoilsowi
wlasnosc checkboxa.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — CaptiveRags: jency w podgladzie druzyny stoja w LACHMANACH
**Mod:** Armoury | **Pliki:** `Armoury/src/CaptiveRags.cs` (nowy),
`Settings.cs`, `McmSettings.cs` (gen), `SubModuleMain.cs`
**Problem:** Jeff: "jak klikne na jenca w panelu party, stoi w pelnej
zbroi - a przeciez obrobilismy go ze sprzetu. Ikony zostawiamy, ale po
klikniecu pokazujemy w lachmanach, tylko w wersji jeniec". Panel druzyny
buduje podglad 3D z SZABLONU jednostki (PartyCharacterVM.GetCharacterCode
-> character.Equipment), wiec obszukany jeniec pozowal w rynsztunku.
**Zmiana:** postfix na GetCharacterCode: dla wierszy typu Prisoner
(szeregowi; bohaterowie i zwykli zolnierze nietkniete) kod wygladu
budowany z PUSTEGO ekwipunku + najtansze lachmany na grzbiet (ta sama
heurystyka co GiveRags w RealisticCaptivity: rag/burlap/sack/peasant/
beggar, cache). Twarz/cialo/kultura bez zmian. Ikony listy bez zmian.
Pokretlo MCM CaptiveRagsPreview (ON), aktywne tylko przy wlaczonym
CaptiveSpoilsEnabled (pokazujemy golych TYLKO gdy naprawde rozbieramy).
Czysto wizualne - zero wplywu na walke/ekonomie.
**Ryzyko / co sprawdzic:** panel druzyny -> sekcja jencow -> klik na typ:
model w lachmanach; wlasni zolnierze i bohaterowie-jency po staremu.
**Status:** ZBUDOWANE - watcher wgra po zamknieciu gry

## 2026-08-30 — PRAWO ZACHOWANIA LUPU: koniec podwojnego liczenia sprzetu wroga
**Mod:** Armoury | **Pliki:** `Armoury/src/BattlefieldLaw.cs`, `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff (w trakcie gry): "walczyly tez inne partie, a ja dostalem
100% loot - bez sensu! [...] nie mozemy dostac NIC ponad to, co ma wroga
armia". Log 09:57: dzialka DTE ledwie 29 szt. (40%), a ekran lupow dostal
464 pozycje. Przesledzenie CALEGO potoku lupu (dekompilacja MapEvent, DTE,
Spoils of War) wykazalo, ze sprzet wroga w bitwie osobistej liczyl sie
WIELOKROTNIE:
(1) zabity placi RAZ fizycznie na scenie (DTE -> magazyn partii ZABOJCY)
i DRUGI raz szablonowo w vanilla MapEvent.LootDefeatedPartyCasualties
(loteria "z szablonu" za kazdego DiedInBattle ORAZ WoundedInBattle,
dzielona wg wkladu);
(2) NASI polegli: DTE zwraca ich fizyczny sprzet do magazynu
(ItemsToRecover), a nasz GatherFallen DOSYPYWAL drugi komplet z szablonu;
(3) wraki (czesc rozbita ciosem) zbieralismy z CALEGO pola, takze z
zabojstw sojusznikow;
(4) jency z bitwy od 29.08 NIE byli obszukiwani (bramka anty-dubel z
vanilla loteria) - sluszne wtedy, zbedne po wycieciu loterii.
**Zmiana (model docelowy - kazda sztuka wroga placi DOKLADNIE RAZ):**
- zabity wrog -> scena DTE (magazyn partii zabojcy; gracz widzi swoja
  dzialke PlayerLootSharePercent);
- ranny wziety do niewoli -> obszukanie jenca (przywrocone dla bitew,
  "do naga", fizycznie raz);
- ranny, ktory uciekl -> NIC (uciekl w swoim);
- bagaze pokonanych -> vanilla podzial wg wkladu (jak dotad);
- nasi polegli -> fizyczne sztuki zwraca DTE, GatherFallen JUZ TYLKO
  przeksiegowuje na gracza (zero dosypywania; ducha przytnie
  ReconcileStock);
- wraki -> tylko z zabojstw PARTII GRACZA (Agent.Origin -> Party);
- vanilla LootDefeatedPartyCasualties -> prefix return-false w bitwie
  osobistej z DTE (flaga CasualtyLootCut; symulacje i stack bez DTE
  ida po staremu).
**Ryzyko / co sprawdzic:** po bitwie z sojusznikami ekran lupow ma byc
WYRAZNIE chudszy (Twoja dzialka, nie cale pole); po wzieciu jencow
komunikat "The captives are stripped at the rope"; w logu linie
"szablonowa loteria za poleglych/rannych pominieta". UWAGA - Spoils of
War ma WLASNY "Baggage Train" (drugi ekran bagazy ze snapshotu z POCZATKU
bitwy) - to potencjalny dubel bagazy POZA naszym kodem: wylaczyc
"Enable Baggage Train" w MCM Spoils of War.
**Status:** ZBUDOWANE - CZEKA NA ZAMKNIECIE GRY (watcher wgra DLL
automatycznie po wyjsciu z gry; do tego czasu gra chodzi na starym
buildzie!)

## 2026-08-30 — SkillSinew: wlasny mundur kazdy umie nosic (sufit wg tieru) - zastepuje GiantSinew
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff po spisie docs/ROT-rozjazdy-skilli.md (548 rozjazdow,
169 jednostek: milicje z Atletyka 0 przy zbroi 70-120, Zlote Plaszcze bez
prawa do wlasnych plaszczy, tully_knight 110 vs 140): "jednym ruchem -
ale z logika: tier 1 nosi slabe pancerze i jego Atletyka nie bedzie 150;
tier 6/7 moze miec pod tier 6/7; tier 2 nie moze miec Atletyki tieru 6".
**Zmiana:** GiantSinew zastapiony ogolnym Mends.SkillSinew (OnSessionLaunched):
kazdej jednostce (nie-hero) Atletyka podbijana do najwyzszego difficulty
pancerza z JEJ WLASNEGO wzorca, ale nie wyzej niz SUFIT TIERU:
20 + 30 * tier (t1=50, t2=80, t3=110, t4=140, t5=170, t6=200). Jednostka,
ktorej ROT wpisal mundur ponad sufit tieru, dalej go nie uniesie -
degradacja zostaje (wina danych, nie skilla). Giganci (wysoki tier)
dostaja z reguly dokladnie swoje 150 - efekt GiantSinew zachowany.
Zasada nadrzedna bez zmian gryzie przy przydziale CUDZEGO sprzetu
(magazyn DTE, ksiega musztry, kwatermistrz).
**Ryzyko / co sprawdzic:** log CrashScribe przy starcie sesji:
"Mends: SkillSinew - Atletyka podbita N jednostkom ... M przypadkow
zostalo ponizej munduru"; wpisy per jednostka tylko dla rozjazdow >=50.
Zlote Plaszcze: goldcloak_armor 120 vs milicja niskiego tieru - nadal
degradowane (sufit), zgodnie z logika Jeffa.
**Status:** WGRANE (30.08, gra zamknieta)

## 2026-08-30 — Mendy: giganci nosza wlasna skore (Atletyka 150) + martwy widget DTE uspiony (A6)
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`, `CrashScribe/src/SubModuleMain.cs`
**Problem:** (1) Jeff: "czemu gigant ma atletyke 40?" - dane ROT: jednostki
giant/giant_archer/giant_rider maja Atletyke 20-40 (ROT-Troops.xml), a ich
wlasne pancerze difficulty 120-150 (giant_garb 150, giant_boots 120 w
ROTassets.xml). Vanilla difficulty sprawdza tylko GRACZOWI, wiec autorzy ROT
wpisali skille jednostek na odczepnego; nasza zasada nadrzedna egzekwuje je
takze jednostkom - egzekutor rozbieral gigantow z wlasnej skory i wsadzal
w ludzkie kaftany. (2) A6 z ERRORS.md: martwy wskaznik gotowosci zbrojowni
DTE na pasku mapy (nigdy sie nie wyswietla - pada w inicjalizacji na 1.4.8)
rzucal AmbiguousMatchException przy KAZDYM odswiezeniu paska: 604/sesje.
Jeff potwierdzil, ze zadnego paska nie widzi.
**Zmiana (decyzje Jeffa: "NAPRAWIC" + mend za darmo):** (1) Mends.GiantSinew
(nowy MendsBehavior, OnSessionLaunched): jednostkom o id giant*/*_giant
Atletyka podbita do 150 przez PropertyOwner.SetPropertyValue - chirurgicznie,
reszta z 548 rozjazdow danych ROT nietknieta; log per gigant. (2) prefix
return-false na MapArmoryReadinessMixin.OnRefresh - odswiezanie uspione,
przycisk otwierania zbrojowni zostaje (osobna droga).
**Ryzyko / co sprawdzic:** w logu CrashScribe przy starcie sesji linie
"Mends: gigant ... Atletyka 20 -> 150" i "martwy wskaznik ... uspiony";
w SUMMARY sesji AmbiguousMatchException ma zniknac (bylo 604/8); giganci
w bitwie znow w garbach.
**Status:** WGRANE (30.08, gra zamknieta)

## 2026-08-30 — Stajnie AI: wielblady i rydwany tylko u swoich + id ras w logu
**Mod:** Armoury | **Pliki:** `Armoury/src/Stables.cs`
**Problem:** Jeff: "czemu rydwany i camels sa w armiach AI na POLNOCY
i dostepne do zakupu? kamele w zimowym srodowisku?!". Nasze stajnie AI
kupowaly wierzchowce dwoma drogami bez patrzenia na klimat: (1) z targu
osady - najtanszy IsPlainMount, wiec zawleczony handlem/lupami wielblad
w Winterfell szedl prosto pod siodlo Boltonow; (2) u hodowcy (CheapestMount)
- fallback "najtanszy kon, jakiego zna swiat" mogl globalnie wybrac
wielblada/rydwan (ROT ma chariot1-4 jako wierzchowce). Log nie zapisywal
CO kupiono, wiec nie bylo jak tego wysledzic.
**Przyczyna:** brak filtra kulturowego dla ras egzotycznych.
**Zmiana:** obie sciezki pomijaja itemy o id zawierajacym camel/chariot,
gdy kultura itemu != kulturze osady (u Dornijczykow/Essos kupuja jak dotad);
log zakupu podaje teraz id ras: "kupil N koni [id1,id2] w ...".
**Ryzyko / co sprawdzic:** w Armoury.log wpisy "Stajnia AI: ... [sumpter_horse]
w Karhold" - zadnych camel/chariot poza poludniem; DRUGA sciezka zawleczenia
(lupy DTE po bitwach z poludniem - magazyn partii przydziela jezdzcom co ma)
zostaje - to inna mechanika, do decyzji osobno.
**Status:** WGRANE (30.08, gra zamknieta)

## 2026-08-30 — KOREKTA po przegladzie: wyjscie z wait-menu przez TICK (ExitToLast byl bledny)
**Mody:** Armoury, RealisticCaptivity | **Pliki:** `Armoury/src/SmithMenu.cs`,
`Armoury/src/HideoutPurge.cs`, `RealisticCaptivity/src/Work.cs`
**Problem:** wpis "4 opcje wait-menu schodza z zakazanego SwitchToMenu" opisal
zachowanie, ktorego kod nie mial. Przeglad adwersaryjny (dekompilacja
GameMenuManager/MapState): ExitToLast NISZCZY MenuContext - stosu menu NIE MA,
a menu po zniszczeniu dobiera Campaign.Tick z GetGenericStateMenu(). Skutki
starej "poprawki": w MIESCIE opcje ladowaly w town_outside (menu bram) zamiast
w kuzni; we WSI ("Enough of this toil") gracz zostawal BEZ ZADNEGO menu
z wiszacym PlayerEncounter (model nie ma przypadku dla partii w srodku wioski) -
regresja BLOKUJACA.
**Przyczyna:** falszywy model mentalny "krok wstecz"; NightRest.ExitToLast
dziala, bo sen konczy sie na mapie, nie w osadzie.
**Zmiana:** wzorzec vanilla (uzywany juz w SmithMenu.WorkTick): opcja podnosi
FLAGE, a SwitchToMenu wykonuje TICK wait-menu w nastepnej klatce - cel
dokladnie jak w oryginale (Menu kuzni / arm_hideout_search / _cameFrom).
Flagi zerowane w initach. SwitchToMenu z ticka jest legalne; z opcji
wait-menu pozostaje zakazane (CLAUDE.md).
**Ryzyko / co sprawdzic:** kazda z 4 opcji wraca tam, gdzie wracala przed
pakietem; UWAGA: klik przy CALKOWICIE zapauzowanym czasie zadziala dopiero
po odpauzowaniu (tick musi tyknac) - jesli to bedzie wkurzac, dorobimy
odpauzowanie w opcji.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Naprawy po przegladzie podwojnym: Reset/Cancel ekranu, degradacja DressCode i domkniecia
**Mody:** Armoury, GrandTourney | **Pliki:** `QuartermasterLaw.cs`,
`ArmouryBehavior.cs`, `DressCode.cs`, `SlowHealing.cs`, `Settings.cs`,
`SaveDefiner.cs`, `GrandTourney/src/Log.cs`
**Problem (znaleziska przegladu adwersaryjnego, 5 recenzentow):**
(1) POWAZNE: InventoryLogic.Reset/Cancel przywraca rostery HURTEM bez
TransferItem - ksiega wkladow i rejestr wymian zostawaly ze stanem sprzed:
wklad + Reset + zamkniecie = "wymiana widma" (darmowy sprzet wojska),
wyjecie + Reset = strata ksiegi. (2) POWAZNE: bramka ItemReq w DressCode
zostawiala GOLY slot, a wzorce ROT lamia wlasne wymogi w ~548 miejscach
(giganci ath 20-40 vs pancerz 120-150 itd.) - czesciowy powrot "golych
wojakow". (3) drobne: pasy bezpieczenstwa mogly rozliczyc wymiany przy
ekranie otwartym przez moda-save; ReconcileStock mogl przyciac wklad
wiszacy w NIEROZWIAZANYCH pozycjach DTE; komunikat "full kit" klamal
przy komplecie 0; komentarz o prefiksach Harmony byl falszywy (bool-prefix
NIE biegnie po czyims false; void biegna zawsze); GrandTourney.log mial
naglowek "Realistic Captivity"; ArmSaveDefiner nie pokrywal
Dictionary<string,string> (piny MusterBook); suwak AiHealingRegenPercent
mial martwy zakres 101-400.
**Zmiana:** (1) migawka ksiegi przy otwarciu ekranu + postfix na
InventoryLogic.Reset cofa ksiege i rejestr do migawki (flaga sesji
_screenOpen); (2) DressCode degraduje jak DragonUnmount: sztuka ponad
skill -> SkillsDecide.TopArmor w ramach Atletyki, nie goly slot;
(3) SafetyRelease dla pasow (wymiany rozliczane tylko po realnym
zamknieciu ekranu), ReconcileStock odklada sie przy nierozwiazanych
pozycjach DTE, osobny komunikat "no man of the company carries...",
poprawiony komentarz, naglowek GT, definicja <string,string> przy braku
ROT, procenty >100 przyspieszaja gojenie (gracz i AI).
**Sprostowania do wczesniejszych wpisow (bez ich edycji):** commitow bez
wpisu z okna 27-28.08 bylo 11 (nie 10, jeden z 26.08); kosmetyka objela
11 plikow (nie 9), a "czysty grep" dotyczy KODU (dokumenty i dziennik
zawieraja polskie znaki z natury); kara -25% "w dryfie" siedzi w kodzie
CampaignSystem (integracja morska), nie w samym NavalDLC.dll; opis
SlowHealing z 28.08 "wynik ciety na koncu" byl nieprecyzyjny - AddFactor
jest addytywny z medycyna (prostowal juz wpis "Ranni AI", tu formalnie).
Wpis zalegly: commit 0174744 dodal guard ReconcileStock na PUSTY magazyn
(DTE po load odtwarza roster z opoznieniem - przyciecie wtedy wyzerowaloby
cala ksiege gracza).
**Ryzyko / co sprawdzic:** wrzucic sprzet, kliknac Reset na ekranie,
zamknac -> zero komunikatow wymiany, licznik wkladow jak przed otwarciem;
kryjowka: zbojcy ubrani (w gorszy pancerz zamiast zadnego); w logu
ewentualne "ReconcileStock: ... odlozona" przy egzotycznych itemach.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — HungerLaw: glodna partia AI kupuje jedzenie BEZ limitu ceny
**Mod:** Armoury | **Pliki:** `Armoury/src/HungerLaw.cs` (nowy), `Settings.cs`,
`McmSettings.cs` (gen), `SubModuleMain.cs`
**Problem:** Jeff (po diagnozie glodu): "jak gloduja, to cena nie gra roli -
kupuja, bo sa glodni". Vanilla (DefaultPartyFoodBuyingModel:37) i BannerKings
(BKPartyBuyingFoodModel:35) odmawiaja AI zakupu jedzenia od 120 denarow
wzwyz - w wojennej drozyznie lord stoi na PELNYM targu i gloduje, a glod
zamienia 25% szeregowych dziennie w rannych. ROT-owy model tylko deleguje
do BK, wiec limit obowiazywal wszedzie.
**Przyczyna:** twardy prog 120 w obu implementacjach FindItemToBuy.
**Zmiana:** postfix na KAZDA zadeklarowana implementacje
PartyFoodBuyingModel.FindItemToBuy (petla po zaladowanych typach, kazda
latka w osobnym try): gdy model nie wybral nic, a partia AI (nie gracz)
GLODUJE - wybieramy najtansza sztuke jedzenia, na ktora ja stac (zloto
lorda, jak w BK), bez limitu. Parametry Harmony po indeksach __0..__3,
bo ROT nazywa out-parametr inaczej niz vanilla/BK. Pokretlo MCM
AiStarvingBuysAnyPrice (The lean quartermasters, domyslnie ON).
Syty targuje sie po staremu - postfix nie rusza udanych wyborow modelu.
**Ryzyko / co sprawdzic:** w logu linia "HungerLaw: ... modeli zakupu
jedzenia zalatanych" (powinno byc >=2: Default+BK; ROT deleguje) oraz
z czasem "glodne partie kupily juz N szt. ponad limit 120". Na mapie:
glodujace armie AI stojace w osadach powinny w 1-2 dni przestac glodowac
(o ile targ ma COKOLWIEK i lorda stac); ranni zaczna schodzic w tempie
vanilla (AiHealingRegenPercent=100).
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — WPIS UZUPELNIAJACY: commity 27-28.08 bez wpisow (naruszenie sekcji 9)
**Mod:** Armoury | **Pliki:** rozne (patrz commity)
**Problem:** audyt wykazal 10 commitow kodu z okna 27.08 wieczor - 28.08 rano
BEZ zadnego wpisu w dzienniku - drugie konto Claude nie mialo skad o nich
wiedziec, a ostatni wpis o odwecie kryjowki podawal wartosci sprzeczne
z kodem (promien 20 i pogon 24h vs realne 40 i 48h).
**Uzupelnienie (stan koncowy w kodzie):**
- `ef0891b` GuardMaster: slad zycia w logu.
- `bc3aa52` + `ce577bb` SmithAudit: diagnoza rafinacji (formula i stan
  wyniku w sakwach przed/po - lapanie "Thamaskene sie nie dodaje").
- `4dc6bbc` + `2d778a0` + `cc267f5` odwet kryjowki, stan koncowy:
  walka przerywa pladrowanie, vendetta JEDNORAZOWA, pogon **48h**,
  promien odwetu **40** (nie 20/24h jak mowil wczesniejszy wpis).
- `60f75e1` garbarnia miejska.
- `c823bc6` + `708f6b4` + `fdbf05e` BkSupplyTemper, stan koncowy: zapasy
  BK partii AI ciete do **1 dnia / 12 sztuk** na kategorie.
- `57cdf23` CleanseDragons takze w lupach.
**Status:** WGRANE (od 28.08; wpis czysto dokumentacyjny)

## 2026-08-30 — Audyt: kosmetyka - naglowek Armoury.log + diakrytyki/cyrylica precz
**Mody:** Armoury, RealisticCaptivity, CrashScribe | **Pliki:** `Armoury/src/Log.cs`
+ 9 plikow z komentarzami
**Problem:** znaleziska audytu: (1) naglowek Armoury.log podpisany
"=== Realistic Captivity ===" (kopiuj-wklej w Log.Init); (2) 15 miejsc
z polskimi diakrytykami w komentarzach wbrew CLAUDE.md sekcja 1, w tym
DWA znaki cyrylicy udajace lacinskie (GuardMaster.cs "skladа",
EnlistedWounds.cs "zaciezного").
**Zmiana:** naglowek na "=== Armoury ==="; wszystkie diakrytyki i cyrylica
zamienione na ASCII (ucieklem, choragiew, oboz, wiesniacy, podkowek, caly,
bral, kon, doklada, zlapali, zacieznego, jukow). Repo przechodzi czysty
grep na diakrytyki.
**Ryzyko / co sprawdzic:** zadne (same komentarze + naglowek logu).
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Ranni w armiach AI: przyczyna GLOD; leczenie AI wraca do vanilla + czynnik nie tnie kar
**Mod:** Armoury | **Pliki:** `Armoury/src/SlowHealing.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen)
**Problem:** Jeff: "Ramsay Bolton i sasiedni lord - sami ranni, zdrowi tylko
dowodcy; w dwoch oddzialach obok siebie to wyglada na bug". Sledztwo
(4 rownolegle tropy, dekompilacja calego stacku): jedyny kod zamieniajacy
zdrowych szeregowych AI w rannych poza bitwa to UJEMNE dzienne leczenie
konsumowane przez PartyHealCampaignBehavior.ReduceHpMemberRegulars (filtr
IsRegular - BOHATEROWIE NIGDY, oni osobnym torem traca HP i dlugo wygladaja
na zdrowych). Ujemne robi sie wylacznie z glodu: vanilla Starving -25%
skladu/dzien (DefaultPartyHealingModel:132-146), BannerKings -10%/dzien
w OBLEGANEJ glodujacej osadzie (VanillaModelTweakPatches:719-740), NavalDLC
-25%/dzien w dryfie. Ranni od glodu NIE umieraja - stan zatrzymuje sie
DOKLADNIE na "100% szeregowych rannych, lord zdrowy". Dwie sasiednie partie
= wspolny glodujacy teatr wojny (zima 1087, 13 wojen, targi puste - AI
kupuje jedzenie tylko ponizej 120 denarow). NASZ WKLAD w objaw: (1) wspolne
-50% wydluzalo wyjscie z dolka do tygodni (AI leczy 2.5-5.5 ludzi/dzien);
(2) paradoks: czynnik -50% na ujemnym wyniku LAGODZIL kare glodowa o polowe;
(3) SlowMuster dlawi doplyw swiezych zdrowych; (4) BkSupplyTemper tnie
zywnosc z sakw BK (mieso/ser/ryby z 10 dni do 1 dnia) - przyspiesza glod
o 2-4 dni (zboz nie tyka). RBM dodatkowo podbija odsetek rannych zamiast
zabitych w bitwach AI (usuwa vanillowe tlumienie x0.25 bonusu chirurga).
**Przyczyna:** patch bez rozrozniania gracz/AI i bez rozrozniania
leczenie/kara.
**Zmiana:** (1) nowe pokretlo AiHealingRegenPercent (MCM "The slow mending",
domyslnie 100 = vanilla) - AI leczy sie normalnie, gracz zostaje na 50;
(2) czynnik stosowany tylko przy wyniku DODATNIM - kary glodowe biegna
pelna vanilla sila takze u gracza; (3) log startowy pokazuje oba procenty.
**Ryzyko / co sprawdzic:** test z tropu: najechac na partie Ramsaya, odczekac
1 dzien gry bez bitwy - jesli zdrowi ubywaja ~25%/dzien, to AKTYWNY glod
(sprawdzic targ: jest jedzenie < 120 denarow?); jesli stoja i ranni powoli
schodza ~5-8/dzien - to juz tylko zdrowienie po glodzie w tempie vanilla.
U gracza: glod rani teraz PELNA stawka (bez -50% ulgi) - zgodnie z "kary
maja bolec".
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: kolejnosc prefiksow na DoSmelting jawna (Priority.High)
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithAudit.cs`
**Problem:** znalezisko audytu: CraftingCampaignBehavior.DoSmelting patchuja
dwa nasze prefiksy - SmithAudit.SmeltPrefix (pomiar staminy "przed") i
SmeltTab.DoSmeltingPrefix (przetop pancerzy nasza sciezka, sam pobiera
stamine i zwraca false). Poprawnosc wisiala na NIEJAWNEJ kolejnosci
rejestracji w SubModuleMain (SmithAudit przed SmeltTab): przy odwroceniu
Settle mierzylby stamine juz PO pobraniu, widzial drop=0 i wymuszal koszt
DRUGI raz [enforced].
**Przyczyna:** rowne priorytety Harmony = kolejnosc rejestracji.
**Zmiana:** prefiksy SmithAudit dostaja Priority.High (pomiar zawsze
pierwszy), niezaleznie od kolejnosci ApplyAll.
**Ryzyko / co sprawdzic:** przetop pancerza z zakladki Smelt - stamina
schodzi RAZ (komunikat "Smelting: -N stamina" bez [enforced]).
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: dwa martwe okruchy Watch naprawione
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Watch.cs`
**Problem:** znaleziska audytu: (1) prefix ScreenPopped(ScreenBase screen)
celowal w bezparametrowe ScreenManager.PopScreen() - co sesje 8x wyjatek
"Parameter screen not found" przy ladowaniu, okruch "closed screen" nigdy
nie dzialal; (2) okruch menu celowal w GameMenuManager.SwitchToMenu,
ktorego w 1.4.8 nie ma (log: "brak metody") - slad przelaczen menu martwy.
**Przyczyna:** sygnatury spisane z innej wersji gry.
**Zmiana:** (1) ScreenPopped() bez parametrow, zamykany ekran czytany
z ScreenManager.TopScreen (prefix biegnie przed zdjeciem); (2) cel
przeniesiony na statyczny TaleWorlds.CampaignSystem.GameMenus.GameMenu
.SwitchToMenu(menuId) - sygnatura potwierdzona dekompilacja.
**Ryzyko / co sprawdzic:** w trail.txt maja pojawic sie okruchy
"screen: closed ..." i "menu: <id>"; przy ladowaniu ma zniknac 8x
"Parameter screen not found".
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: Armoury ma wlasny definer save (Dictionary<string,int>)
**Mod:** Armoury | **Pliki:** `Armoury/src/SaveDefiner.cs` (nowy)
**Problem:** znalezisko audytu: Armoury zapisuje Dictionary<string,int>
(arm_player_stock) bez wlasnego SaveableTypeDefiner - zapis dzialal tylko
dzieki definicji ROT-a (HeroRaceMapSaveableTypeDefiner) albo RC
(RcSaveDefiner przy braku ROT). Granie samym Armoury = "Cannot create
save data".
**Przyczyna:** kontener zdefiniowany "u kogos", nigdy u nas.
**Zmiana:** ArmSaveDefiner (base id 928371600, rozlaczne z RC 928371500)
definiuje kontener TYLKO gdy nie ma ani ROT-a, ani RC - podwojna definicja
wywala kolekcje typow rownie skutecznie jak jej brak.
**Ryzyko / co sprawdzic:** w biezacej kampanii (ROT obecny) definer nie
robi nic - save/load bez zmian; zabezpiecza wylacznie konfiguracje bez
ROT i RC.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: uwolnienie towarzysza NIE rozbiera go drugi raz
**Mod:** RealisticCaptivity | **Pliki:** `RealisticCaptivity/src/CaptivityBehavior.cs`
**Problem:** znalezisko audytu; Jeff potwierdzil intencje "z niewoli
wychodzisz nagi - sprzet jest zabierany, tak ma byc". Zabieranie dziala
przy POJMANIU (StripCompanionGear w OnHeroPrisonerTaken, sprzet do ksiegi
_companionGear + cena odkupu). Ale OnHeroPrisonerReleased woLal
StripCompanionGear DRUGI raz - kopiuj-wklej z pojmania. Zwykle no-op
(towarzysz i tak juz goly), ale gdy gracz wyszedl z niewoli wczesniej
(RestoreCompanionGear przywraca wtedy sprzet wszystkim towarzyszom, takze
uwiezionym), pozniejsze uwolnienie towarzysza rozbieralo go PONOWNIE:
sprzet wracal do ksiegi-limbo i sztucznie podbijal cene odkupu.
**Przyczyna:** skopiowany blok z OnHeroPrisonerTaken.
**Zmiana:** przy uwolnieniu towarzysza nie robimy NIC (nagi wychodzi -
zgodnie z intencja; rozbieranie zostaje wylacznie przy pojmaniu).
**Ryzyko / co sprawdzic:** pojmany towarzysz nadal traci sprzet (komunikat
"was stripped of gear as well"); po wykupieniu/uwolnieniu wraca goly,
cena odkupu bez podwojnych pozycji.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: DressCode dostaje bramke ItemReq (zasada nadrzedna skilli)
**Mod:** Armoury | **Pliki:** `Armoury/src/DressCode.cs`
**Problem:** znalezisko audytu: DressCode (dopelnianie pustych slotow pancerza
ze wzorca oddzialu, prefix Priority.Last na Mission.SpawnAgent) ubieral
zolnierza BEZ sprawdzenia ItemReq - mogl wlozyc hełm difficulty 140 ponad
Athletics jednostki, w tym sztuke, ktora chwile wczesniej zdjal egzekutor
skilli w DragonUnmount (bo biegnie OSTATNI). Jedyna dziura w zasadzie
"bez skilla nie uzywasz" (SkillsDecide/DragonUnmount/kwatermistrz/GrandTourney
egzekwuja ja wszedzie indziej).
**Przyczyna:** DressCode powstal do golych zbojcow w kryjowkach, przed
domknieciem zasady nadrzednej.
**Zmiana:** kazda dokladana sztuka przechodzi ItemReq.Meets(CharacterObject,
item); bez skilla slot zostaje pusty (kara underequipped DTE i tak liczona
z przydzialu, nie z naszych dolozek).
**Ryzyko / co sprawdzic:** kryjowka z niskotierowymi zbojcami - dalej maja
byc ubrani (ich wzorce maja niskie difficulty); log "DressCode: ... dostal
ubranie ze wzorca" moze pokazywac mniejsze liczby przy elitarnych wzorcach.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: 4 opcje wait-menu schodza z zakazanego SwitchToMenu (znany CTD)
**Mody:** Armoury, RealisticCaptivity | **Pliki:** `Armoury/src/SmithMenu.cs`,
`Armoury/src/HideoutPurge.cs`, `RealisticCaptivity/src/Work.cs`
**Problem:** znalezisko audytu: cztery opcje menu OCZEKIWANIA wolaly
GameMenu.SwitchToMenu z wnetrza konsekwencji opcji - dokladnie wzorzec,
ktory dal CTD "Rouse the men early" (NRE w GameMenuVM.OnFrameTick,
CLAUDE.md sekcja 7) i zostal naprawiony w NightRest, a te miejsca zostaly:
arm_work_wait "Put the work aside", arm_project_wait "Step away",
arm_hideout_search_wait "Call the search off", rc_work_* "Enough of this toil".
**Przyczyna:** kod starszy niz odkrycie pulapki; nikt nie wrocil z poprawka.
**Zmiana:** wszystkie cztery na GameMenu.ExitToLast() wzorem NightRest.
ExitToLast wraca do menu, z ktorego sie weszlo (kuznia/warsztat, kryjowka,
town/village) - czyli tam, gdzie i tak celowal SwitchToMenu. Wywolania
SwitchToMenu z TICKOW wait-menu (SmithMenu:187/1485, Work:344) zostaja -
to wzorzec vanillowy, sam w sobie nie crashowal.
**Ryzyko / co sprawdzic:** kliknac kazda z 4 opcji w trakcie czekania -
powrot do poprzedniego menu bez crasha; "Put the work aside" z warsztatu
naprawy wraca teraz do MENDMENU (krok wstecz), nie do glownego menu kuzni -
jesli to przeszkadza, mozna przelaczyc w nastepnym ticku.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: magazyn w calosci gracza (Active=false) dziala jak nalezy
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** znalezisko audytu: gdy CALY magazyn nalezy do gracza, HoldReserve
nie ma czego chowac (Active=false) i wtedy (1) prog need/have potrafil
ZABLOKOWAC odbior WLASNYCH sztuk gracza ("the men still use these"), a
(2) ReleaseReserve wychodzil wczesnym returnem i wymiana barterowa wkladow
z tej sesji w ogole nie startowala - wklady wisialy w _pendingSwaps do
nastepnego otwarcia, gdzie kasowal je swiezy Clear().
**Przyczyna:** prog pisany z mysla o sztukach wojska; early-return Release
zawiazany na _held, nie na sprawy do rozliczenia.
**Zmiana:** (1) w Prefixie: sztuki pokryte ksiega gracza (StockOf >= take)
zawsze przechodza; (2) ReleaseReserve nie wychodzi, gdy _pendingSwaps ma
wpisy - ProcessSwaps biegnie takze przy pustym depozycie.
**Ryzyko / co sprawdzic:** swiezy magazyn zlozony tylko z wkladow gracza:
wszystko da sie wyjac bez komunikatow; wrzucone wklady rozliczaja sie przy
zamknieciu (przyjecie do kompletu / nadwyzka zostaje).
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: ksiega-duch przycinana do realnych polek
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`, `Armoury/src/QuartermasterLaw.cs`
**Problem:** znalezisko audytu: sztuki zaksiegowane na gracza znikaja z polek
drzwiami, ktore NIE ida przez StockWithdraw - DTE zdejmuje kolczan przy
spawnie lucznika, kolczan wystrzelany do zera przepada, CleanseTrashInBags
(dzis 1533+3612 szt.) i sanacja ujemnych stosow tna magazyn. Ksiega gracza
puchla ponad polki: WarOwnedOf zanizal stan wojska (za duzo wkladow "do
kompletu"), a HoldReserve pokazywal graczowi cudze sztuki jako wlasne.
**Przyczyna:** brak rekonsyliacji ksiegi z polkami.
**Zmiana:** ArmouryBehavior.ReconcileStock(why): per id clamp ksiegi do sumy
el.Amount na polkach, z logiem "przycieta o N szt. ducha"; wolane na poczatku
HoldReserve (przed schowaniem skarbca), gdy escrow nieaktywny.
**Ryzyko / co sprawdzic:** po bitwie z lucznikami otworzyc zbrojownie -
w logu moze pojawic sie linia ReconcileStock; liczniki na liscie gracza
nie moga przekraczac tego, co fizycznie lezy.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: uczciwa ksiega wkladow (ksiegowanie PO transferze + wycofanie kasuje wymiane)
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** dwa znaleziska audytu o tej samej ksiedze: (1) Prefix na
InventoryLogic.TransferItem ksiegowal ruch ZANIM transfer mogl zostac
zablokowany (nasz wlasny return false przy progu need/have albo prefix DTE
CommandersGreed - w Harmony 2 wszystkie prefiksy biegna po czyims false);
ksiega rozjezdzala sie z faktycznym ruchem sprzetu. (2) Wycofanie wkladu
w TEJ samej sesji ekranu nie kasowalo wpisu w _pendingSwaps - przy
zamknieciu kwatermistrz rozliczal wklad, ktorego juz nie bylo (wydawal
gorsze sztuki za nic albo zdejmowal z ksiegi sztuki dawno zabrane).
**Przyczyna:** ksiegowanie w prefixie bez wiedzy, czy oryginal pobiegl;
NoteDeposit bez lustrzanego NoteWithdraw.
**Zmiana:** (1) ksiegowanie przeniesione do BookPostfix z wstrzykiwanym
__runOriginal (Harmony 2.4) - ksiega rusza sie tylko po REALNYM transferze;
(2) nowy QuartermasterEscrow.NoteWithdraw zdejmuje wycofane sztuki z
rejestru wymian od najnowszych wpisow, wolany z BookPostfix przy wyjeciu.
**Ryzyko / co sprawdzic:** wrzucic i od razu wyjac te same strzaly, zamknac
ekran -> zadnej wymiany, zadnych komunikatow kwatermistrza; sprobowac wyjac
sprzet ponizej progu (odbity komunikatem) -> licznik wkladow bez zmian.
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Audyt: komplet 0 = wojsko nie bierze NIC
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** znalezisko audytu (33 agentow, potwierdzone adwersaryjnie): gdy
kompania nie ma ani jednego nosiciela typu (np. strzaly przy zerze lucznikow),
`room = needT > 0 ? ... : kept` oddawal wojsku CALY wklad. Model Jeffa mowi
odwrotnie: wojsko bierze najwyzej do kompletu, komplet 0 = bierze 0.
**Przyczyna:** galaz `: kept` w QuartermasterLaw.ProcessSwaps - relikt zasady
"kwatermistrz zawsze przyjmuje" sprzed wpisu o komplecie.
**Zmiana:** `room = Math.Max(0, needT - warHave)` bez galezi; nadwyzka zostaje
graczowi z istniejacym komunikatem "spare stays on YOUR shelf".
**Ryzyko / co sprawdzic:** wrzucic strzaly nie majac lucznikow -> zostaja na
liscie gracza (moze poza wymiana barterowa, jesli wojsko ma gorsze kolczany).
**Status:** WGRANE (30.08 ~08:52, gra zamknieta, 4 DLL-e zweryfikowane hashem)

## 2026-08-30 — Wojsko bierze najwyzej do KOMPLETU, nadwyzka zostaje graczowi
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** Jeff (po potwierdzeniu, ze wklad juz znika): "skoro lucznicy
potrzebuja 214 sztuk, to max znika 214 - bo tyle jest na ludziach".
Kwatermistrz zabieral CALY wklad bez gornej granicy.
**Przyczyna:** blok `kept` oddawal wojsku wszystko, czego nie wymieniono,
nie patrzac na zapotrzebowanie kompanii.
**Zmiana:** (1) nowy pomocnik `WarOwnedOf(armory, type)` - ile sztuk danego
typu nalezy do WOJSKA (calosc polek minus ksiega wkladow gracza, liczona
per id z uwzglednieniem wielu stosow); (2) wojsko przyjmuje najwyzej
`need - warHave` sztuk, reszta ZOSTAJE wlasnoscia gracza; (3) dwa jasne
komunikaty: "N go to the men (X of Y now in hand)" oraz "the men are at
full kit - the spare N stays on YOUR shelf".
**Ryzyko / co sprawdzic:** wrzucic wiecej kolczanow niz brakuje do 214 ->
znika tylko brakujaca czesc, nadwyzka widoczna na liscie gracza.
**Status:** WGRANE (plik zweryfikowany przez cmp; GRA WYMAGA RESTARTU -
przy wgrywaniu w pamieci byly 2 procesy Bannerlorda)

## STAN SPRAWY "AMUNICJA" NA 30.08 (dla drugiego konta Claude)
Model docelowy, potwierdzony przez Jeffa punkt po punkcie:
1. Gracz wrzuca sprzet do zbrojowni -> po zamknieciu ekranu wklad ZNIKA
   z jego listy, bo kwatermistrz go przyjal. Na liscie gracza pokazuje sie
   WYLACZNIE to, co wojsko oddalo w zamian.
2. Wymiana: za sztuke lepsza wojsko oddaje swoja NAJGORSZA tego samego typu.
   "Gorsza" = nizszy tier, a przy rownym tierze nizsza wartosc. Porownanie
   po samej cenie NIE dziala dla amunicji (wszystkie strzaly danego rodzaju
   maja identyczna cene bazowa 550) - to byl glowny blad.
3. Wojsko bierze najwyzej do kompletu (need = Bows*2 dla strzal, liczone
   z lucznikow). Nadwyzka zostaje graczowi.
Naprawione po drodze: strzaly/belty wpuszczone do wymiany (byly celowo
wykluczone w NoteDeposit); licznik amunicji x2 na glowe w ksiedze musztry.
COFNIETE jako bledne: prog nadwyzki blokujacy wymiane przy brakach;
jednorazowy zwrot 104 kolczanow (RefundSeizedAmmo - metoda zostaje w kodzie,
nikt jej nie wola).
WAZNE Z AUDYTU DTE (20 agentow, potwierdzone w zdekompilowanym kodzie):
- licznik "have" liczy CALA zawartosc polek, razem z wkladem gracza;
- jednostka to KOLCZAN, nie strzala (49/214 = kolczany, ~20-30 strzal kazdy);
- DTE odejmuje kolczan z magazynu przy spawnie zolnierza, a po bitwie
  zwraca go TYLKO gdy zostala w nim choc jedna strzala. Kolczan wystrzelany
  do zera przepada na trwale - to jest naturalne zrodlo ubytku amunicji,
  nie blad naszego kodu.
NIEZWERYFIKOWANE PRZEZ JEFFA: panel wyniku kucia (dzwiek + Done bez crasha),
ksiega stanow magazynu przez save (arm_armory_wear), log "AWANS ... magazyn".

## 2026-08-30 — Jeff gral na STARYM DLL; zwrot kolczanow wylaczony
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff pokazal krok po kroku: 49 strzal -> wrzuca 86 -> zamyka
i otwiera ekran -> strzaly NADAL na jego liscie (licznik 135), a po ich
zabraniu znow 49. Objaw identyczny jak przed poprawka.
**Przyczyna (dowod, nie hipoteza):** wgrany plik miał date 17:17 i 413184
bajtow, a zbudowana poprawka 17:25 i 413696 bajtow - gra chodzila na
buildzie SPRZED naprawy. Watcher czekal na zamkniecie gry, ktore nie
nastapilo, wiec poprawka "wklad znika z polki" nigdy nie trafila do gry.
Sama logika ksiegowa jest kompletna: QuartermasterLaw.cs:626 (za kazda
wymiane) + :638 (reszta) zdejmuja CALY wklad z ksiegi gracza.
**Zmiana:** (1) wgrany wlasciwy plik; (2) jednorazowy zwrot 104 kolczanow
(RefundSeizedAmmo) WYLACZONY - Jeff wyjasnil, ze wklad MA przechodzic do
wojska, wiec oddawanie kolczanow na jego polke dzialaloby przeciw temu,
czego chce. Metoda zostaje w kodzie, ale nikt jej nie wola.
**Ryzyko / co sprawdzic:** wrzucic strzaly, zamknac i otworzyc ekran -
listy maja byc PUSTE, licznik podniesiony. Jesli wklad dalej zostaje,
w logu bedzie linia "brak gorszej sztuki ... kandydatow wojskowych: N"
albo komunikat o braku umiejetnosci - to wskaze gałąź, ktora go zatrzymala.
**Status:** WGRANE (gra zamknieta, plik zweryfikowany co do rozmiaru)

## 2026-08-29 — Wklad ZNIKA z polki, wymiana szuka po TIERZE (nie po cenie)
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** Jeff (trzecie tlumaczenie, dosadne): "wrzucam strzaly, one maja
ZNIKNAC, bo kwatermistrz je przyjal, a na liscie maja sie pokazywac TYLKO
rzeczy wymienione - byl komplet, wrzucam 10 lepszych, 10 lepszych znika,
dostaje 10 gorszego tieru". Na zrzucie: 86 kolczanow lezy dalej na polce
gracza, a po zabraniu ich licznik spada z 135 na 49.
**Przyczyna:** (1) po cofnieciu z 17:20 wklad zostawal wlasnoscia gracza -
czyli nie znikal; (2) GLOWNE: warunek "gorsze" patrzyl WYLACZNIE na cene
(v < newVal). Strzaly tego samego rodzaju maja identyczna cene bazowa 550,
wiec zaden kolczan wojska nigdy nie przechodzil progu i wymiana nie miala
czego wydac - stad "brak gorszych sztuk" w logu przy 49 kolczanach wojska
na polkach.
**Zmiana:** (1) wklad ZAWSZE przechodzi na stan wojska i znika z listy
gracza - na liscie zostaje wylacznie to, co wojsko oddalo w zamian;
(2) "gorsze" = NIZSZY TIER albo ten sam tier i nizsza wartosc; (3) log
diagnostyczny "brak gorszej sztuki X - kandydatow wojskowych: N".
**Ryzyko / co sprawdzic:** wrzucic 10 lepszych strzal przy komplecie ->
znikaja, na liscie 10 kolczanow nizszego tieru; wrzucic przy brakach ->
znikaja, komunikat "nothing worse to trade back". Pancerze bez zmian
(tam wymiana i tak znajdowala gorsze).
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — COFNIETE: kwatermistrz pozeral wklad + zwrot 104 kolczanow
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`, `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff: "teraz pozera kazda ilosc strzal!". Build z 17:07 oddawal
wojsku KAZDA niewymieniona sztuke wkladu - gracz wrzucal strzaly, znikaly
z jego polki bezpowrotnie i nie dostawal nic w zamian. Dowod w logu:
"1 + 82 + 4 szt. ravens_teeth_arrows, 12 blunt_arrows, 5 range_arrows
przejete przez wojsko" = 104 kolczany.
**Przyczyna:** dopisalem blok przekazujacy reszte wkladu na stan wojska
(StockWithdraw), zeby "kwatermistrz zawsze przyjmowal". Prawdziwa przyczyna
odmowy przy strzalach byla inna i juz naprawiona - warunek `it == newItem`
pomijal ten sam przedmiot w gorszym stanie. Ten blok byl nadmiarem.
**Zmiana:** (1) blok COFNIETY - zasada wraca do "wymiana 1:1 albo nic",
czego wojsko nie odkupilo swoim starym sprzetem, zostaje wlasnoscia gracza;
(2) jednorazowy zwrot 87 ravens_teeth + 12 blunt + 5 range kolczanow do
ksiegi gracza (flaga arm_ammo_refund_v1, clamp do tego, co realnie lezy
na polkach) - sztuki nigdy nie zniknely fizycznie, przepadl tylko zapis
wlasnosci.
**Ryzyko / co sprawdzic:** po wejsciu do gry komunikat "a miscount is set
right - N quivers are back on YOUR shelf"; wrzucone strzaly maja od teraz
zostawac na liscie gracza, gdy nie ma czym ich wymienic.
**Status:** WGRANE (gra zamknieta)

## 2026-08-29 — Kwatermistrz ZAWSZE przyjmuje wklad (jak z pancerzem)
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** Jeff: "wrzucilem czerwone i ich nie przyjmuje mimo ze licznik
sie zwiekszyl. Czy to nie moze dzialac tak jak z pancerzem - wrzucam lepszy,
znika, dostaje w zamian starszy?". Na zrzucie: 4 pozycje strzal Ravens' Teeth
(30+15+4+38) LEZA na polce gracza po zamknieciu i otwarciu ekranu.
**Przyczyna - dwie, obie moje:** (1) prog nadwyzki dodany godzine wczesniej
blokowal wymiane przy brakach, a wklad zostawal wtedy WLASNOSCIA gracza -
stad "nie przyjmuje"; ten sam prog zepsulby tez pancerze (244/246 = braki);
(2) w petli wymiany warunek `it == newItem` pomijal ten sam przedmiot
w gorszym stanie - przy amunicji, gdzie wojsko ma te same strzaly bez
modyfikatora, nie bylo NIC do wydania i wklad zostawal na polce.
**Zmiana:** prog nadwyzki USUNIETY (wymiana jak przy pancerzu, bez warunkow);
`it == newItem` usuniete - ten sam przedmiot w gorszym stanie jest legalna
wymiana (prog v<newVal i tak odsiewa rowne i lepsze); reszta wkladu, ktorej
nie ma za co wymienic, PRZECHODZI NA STAN WOJSKA i znika z polki gracza
+ komunikat "they had nothing worse of that kind to hand back".
**Ryzyko / co sprawdzic:** wrzucic strzaly -> po ponownym otwarciu ekranu
NIE MA ich na liscie gracza; jesli wojsko mialo gorsze, leza w zamian stare.
Pancerze maja dzialac jak dotad.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — Barter zjadal kazde uzupelnienie: wymiana TYLKO z nadwyzki
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** Jeff: "wrzucam strzaly, kwatermistrz przyjmuje i wydaje mi
wymienione stare, zabieram je do siebie - i znowu 49/214". Uzupelnianie
amunicji bylo NIEMOZLIWE, licznik nigdy nie rosl.
**Przyczyna (arytmetyka, nie wyswietlanie):** ProcessSwaps wymienia sztuke
za sztuke BEZ sprawdzania, czy wojsku brakuje. Bilans: +30 nowych do
magazynu, -30 starych przeksiegowanych na gracza i przez niego zabranych
= 49 jak bylo. Barter to wymiana JAKOSCI, a Jeff probowal nim uzupelnic
ILOSC. Licznik "have" (ShortageLines:186-192) liczy cala zawartosc magazynu
poprawnie - to nie on klamal.
**Zmiana:** wymiana rusza dopiero, gdy magazyn ma NADWYZKE danego typu
(have - need > 0), i nie wieksza niz ta nadwyzka. Przy brakach wklad
zostaje u wojska, a kwatermistrz mowi wprost: "the men are still SHORT of X
(N of M needed) - your gift fills the gaps. Nothing goes back on your shelf
until they have a full set."
**Ryzyko / co sprawdzic:** wrzucic strzaly przy brakach -> licznik ma
WZROSNAC, komunikat o brakach zamiast wymiany; wrzucic luki t6 przy pelnym
stanie lukow -> wymiana dziala jak dotad.
**Status:** WGRANE (gra zamknieta)

## 2026-08-29 — Kwatermistrz nie przyjmowal strzal + licznik amunicji klamal
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`, `Armoury/src/MusterBook.cs`
**Problem:** Jeff: "mam 49/214 arrow i nie przyjmuje mi kwatermistrz strzal
tier 6". W logu bartery szly (39/24/23 szt.), o strzalach ani sladu.
**Przyczyna:** (1) NoteDeposit CELOWO wykluczal Arrows/Bolts z wymiany
barterowej - wklad strzal lezal bez rozliczenia i bez komunikatu;
(2) licznik ksiegi musztry liczyl amunicje 1:1 na glowe, a lucznik nosi
2 kolczany (RearmBySkill) - "49/214" zamiast prawdziwego "49/428".
**Zmiana:** (1) strzaly i belty wchodza do wymiany jak kazda bron
(wrzucasz lepsze -> wojsko bierze, oddaje najgorsze swoje); (2) NeedFor:
Arrows/Bolts licza sie x2 na glowe, tekst podpowiedzi "X of Y needed".
**Ryzyko / co sprawdzic:** wrzucic strzaly t6 kwatermistrzowi, zamknac okno
-> stare strzaly wojska na liscie gracza (log "wymiana barterowa"); karta
strzal w ksiedze pokazuje /men*2.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — "Awans wycina sprzet z magazynu?" — diagnostyka
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff: uzupelnil sprzet u kwatermistrza, po awansach jednostek
"nagle brakuje sprzetu ktorego wczesniej nie brakowalo".
**Ustalenia (kod, nie zgadywanie):** awansow NIKT nie slucha - DTE nie ma
handlera OnTroopUpgraded (sprawdzone dekompilacja EveryoneCampaignBehavior
i ArmyArmoryBehavior), nasz kod tez nie, a wymog konia przy awansie DTE
wylacza (GetUpgradeRequiresItemFromCategoryPatch zwraca null). Awans nie ma
zadnej sciezki do magazynu. Najpewniejsze wyjasnienie: po awansie jednostka
to INNY typ z INNYM wzorcem - karta w muster book pokazuje nowe pozycje
(lepsze luki/pancerze), ktorych stary tier nie potrzebowal, stad "SHORT"
na rzeczach "ktorych wczesniej nie brakowalo".
**Zmiana:** log diagnostyczny: przy kazdym awansie jednostek gracza wpis
"AWANS: X -> Y xN | magazyn: S szt." - jesli S spada miedzy kolejnymi
awansami, zlodziej istnieje i bedzie w logu widoczny; jesli stoi, potwierdza
sie zmiana wzorca.
**Ryzyko / co sprawdzic:** Jeff robi serie awansow -> czytamy sumy.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — CTD po Done na panelu kucia + brak odglosu kucia
**Mod:** Armoury | **Pliki:** `Armoury/src/CraftPopup.cs`
**Problem:** Jeff wykul Ravens' Teeth Longbow - panel z modelem 3D SIE POKAZAL,
ale klik Done = CTD. Dowod (crash-original 14:54): NullReferenceException w
`WeaponDesignResultPopupVM.ExecuteFinalizeCrafting()`. Do tego brak dzwieku kucia.
**Przyczyna:** Done wola ExecuteFinalizeCrafting, ktore w pierwszej linii robi
`_crafting.SetCraftedWeaponName(...)` - u nas _crafting==null (panel bez
projektanta broni) -> NRE. Dzwiek: `event:/ui/crafting/craft_success` gra
vanillowy GauntletCraftingScreen, a sciezka BK go omija.
**Zmiana:** (1) prefix na ExecuteFinalizeCrafting - gdy _crafting==null tylko
`_onFinalize` (zamkniecie panelu), vanilla nietknieta; (2) w Show()
`SoundEvent.PlaySound2D("event:/ui/crafting/craft_success")`; (3) log
"panel dla X - statow N" (na screenie stat-lista wygladala pusto, ale
zaslanialo ja okno crasha - log rozstrzygnie).
**Ryzyko / co sprawdzic:** wykuc luk/pancerz -> dzwiek + panel + Done bez crasha;
w logu "statow N" (luk powinien miec 4: Missile Damage/Speed/Accuracy/Weight).
**Status:** WGRANE (gra byla zamknieta po crashu)

## 2026-08-29 — SAVE GUBI STANY MAGAZYNU (intuicja Jeffa trafna): wlasna ksiega zuzycia
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs` (arm_armory_wear)
**Problem:** Jeff: "wydaje mi sie, ze load automatycznie naprawia sprzet
wojska - save nie trzyma stanu rzeczy". DOWOD z dekompilacji DTE
(ArmyArmoryBehavior.Data): magazyn zapisywany jako Dictionary<string,int> =
itemId -> liczba, BEZ MODIFIEROW. Kazdy save splaszczal zbite sztuki,
kazdy load odtwarzal je CZYSTE - darmowa naprawa calego magazynu. To takze
wspol-przyczyna "pustej listy pancerzy" (zuzyte wczoraj = czyste dzisiaj).
**Zmiana:** wlasna ksiega stanow (arm_armory_wear w SyncData): przy zapisie
zrzut "itemId|modifierId|ile" wszystkich zbitych sztuk magazynu; przy
wczytaniu (gdy DTE juz odtworzy roster - proba przy sesji i przy kazdym menu
do skutku) czyste sztuki sa z powrotem "zbijane" do zapisanych stanow
(clamp do dostepnych czystych). Log "ArmoryWear: odtworzono stany N szt.".
**Ryzyko / co sprawdzic:** save -> load -> Mending Bench: zbite pancerze
maja NADAL byc zbite. TroopSelfMend znow ma sens (naprawa nie jest juz
darmowa loadem).
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — pancerze na liscie naprawy NAPRAWDE: parytet typow przy limicie
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`, `Armoury/src/ArmouryBehavior.cs`
**Problem (Jeff, trzeci raz, slusznie wsciekly):** pancerzy dalej nie bylo
na liscie naprawy. LANCUCH trzech przyczyn, kazda kolejna maskowana przez
poprzednia: (1) 999 pancerzy ROT bez grup modyfikatorow (naprawione wczoraj -
WearGroups, 361 sztuk dostalo grupy przy tym wczytaniu); (2) limit ucinal
roster przed sortem (naprawione); (3) FINALNA: sort uklada BRONIE PRZED
pancerzami, a limit MaxItemsListed=24 (preset MCM Jeffa) ucinal KONIEC listy -
przy dziesiatkach zbitych broni ze [STORES] pancerze zawsze wypadaly za burte,
mimo ze zuzycie juz je tyka ("Zuzycie wojska: 234 sztuk" 14:32).
**Zmiana:** limit z GWARANCJA PARYTETU TYPOW: kazdy obecny typ dostaje pule
max(4, cap/liczba_typow) miejsc (grupami, w kolejnosci typow), nadwyzki
dobieraja wolne miejsca; cap min. 24. Log "pokazane N (po X/typ)".
Bonus ze screena: [STORES] Lady Forlorn = legendy zalegajace w magazynie
GRACZA z dawnych lupow - CleanseTrashInBags tnie teraz takze IsLegend
(magazyn i sakwy, przy kazdym menu).
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — panel kucia NAPRAWIONY: NRE w RefreshUsages przy _crafting=null
**Mod:** Armoury | **Pliki:** `Armoury/src/CraftPopup.cs` (ApplyAll + prefix), `SubModuleMain.cs`
**Problem (Jeff + screen):** przy kuciu luku dalej wyskakiwalo tekstowe
okienko (fallback). Dowod z logu 14:21:33: NullReferenceException
w WeaponDesignResultPopupVM.RefreshUsages - vanillowy VM siega po
_crafting.GetCurrentCraftedItemObject(), a nasze popupy nie ida przez
projektanta broni (crafting=null).
**Zmiana:** Harmony-prefix na RefreshUsages: gdy _crafting==null (tylko nasze
popupy), zakladki uzyc buduja sie wprost z item.Weapons (z filtrem
IsItemUsageApplicable); vanilla sciezka nietknieta. Dla pancerzy (zero uzyc)
DesignResultPropertyList ustawiane wprost po utworzeniu VM. Fallback tekstowy
zostaje na kazdy przyszly zgrzyt.
**Status:** WGRANE (watcher - gra dzialala przy buildzie); test = wykuc luk,
ma byc PELNY panel z modelem, nie okienko.

## 2026-08-29 — puste okno rekrutacji: diagnostyka licznikow ochotnikow
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/WarReport.cs`
**Problem (Jeff + screen):** okno REKRUTACJI w Barrow puste ("Recruit All (0)",
zero wierszy, glowki notabli wystaja nad panelem). W logach ZERO wyjatkow
z tego momentu - okno nie crashuje, tylko buduje zero pozycji. Dwie mozliwosci:
(a) STAN: wszyscy notable maja 0/6 ochotnikow (wolny zaciag 50% + wojna
wysysa rekrutow) i BK-ekran nie renderuje pustych; (b) BUG UI w ekranie BK.
**Zmiana:** spis OSADA przy wejsciu gracza wypisuje teraz per notabl
"ochotnicy n/6" - nastepne wejscie do wioski rozstrzygnie miedzy (a) i (b).
**Test dla Jeffa:** inna wioska + proba rekrutacji przez DIALOG notabla
(sciezka vanilla) - jesli dialog daje rekrutow, a okno puste, to (b).
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — ZBIERAMY POLEGLYCH: rynsztunek wlasnych zabitych wraca na wozy (dla gracza)
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs` (SnapshotOwnRanks/GatherFallen)
**Problem (Jeff):** "polegli mi zolnierze i ich ekwipunek zniknal - ma zasilac
armoury i moge go zabrac".
**Zmiana:** snapshot skladu przy starcie bitwy gracza; po MapEventEnded roznica
liczebnosci per typ = polegli (ranni nie ubywaja z Number, wiec nie klamie).
Za kazdego poleglego jego rynsztunek wedle szablonu (sloty bez choragwi,
smokow i legend; stan bojowy PickWornModifier) wraca do magazynu
i jest PRZEKSIEGOWANY NA GRACZA (StockDeposit) - lezy na TWOJEJ liscie
w Manage Armoury, do zabrania lub oddania wojsku. Komunikat "The fallen are
gathered - N pieces ... yours to claim".
**Ryzyko / co sprawdzic:** przy przegranej tez zbiera (fabularnie ok);
gdyby DTE kiedys sam zwracal sprzet wlasnych poleglych, byłby nadmiar -
obserwacja Jeffa mowi, ze nie zwraca ("znika"), wiec dubla nie ma.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — okno kucia 1:1 z bronia: vanillowy panel NewCraftedWeaponPopup dla lukow i pancerzy
**Mod:** Armoury | **Pliki:** `Armoury/src/CraftPopup.cs` (przepisany na Gauntlet), `Armoury.csproj` (+5 referencji UI), `libs/` (+5 dll)
**Zlecenie Jeffa (screeny):** "nie tekstowe okienko - IDENTYCZNY panel jak
Weapon Crafted! przy mieczach: model przedmiotu, tabela statow z roznicami".
**Zmiana:** CraftPopup laduje VANILLOWY prefab NewCraftedWeaponPopup wlasna
warstwa GauntletLayer, z vanillowym WeaponDesignResultPopupVM: ItemCollection
ElementViewModel daje podglad 3D sztuki (z modifierem), nasza funkcja
BuildProps podaje liste WeaponDesignResultPropertyItemVM (wartosc + roznica
od jakosci: pancerz per czesc, luk dmg/speed/accuracy, tarcza HP, melee
komplet, waga) - kolory plusow/minusow robi prefab. Done zdejmuje warstwe.
Stare okienko tekstowe zostalo jako KOLO ZAPASOWE (kazdy zgrzyt Gauntleta
-> fallback, log "CraftPopup.Gauntlet").
**Ryzyko / co sprawdzic:** pierwszy popup po wykuciu luku/pancerza - czy
panel wyglada jak przy mieczu i czy Done zamyka czysto; jesli wyskoczy
tekstowe okienko zamiast panelu, w logu bedzie blad Gauntleta do naprawy.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — wymiana barterowa v2: NATYCHMIAST, warunkiem czlowiek ze skillem, stare na LISTE gracza
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs` (AnyoneCanUse + ProcessSwaps)
**Korekta Jeffa (scenariusz 1:1):** "wkladam 4 luki t6 i zamykam okno: jesli sa
lucznicy z Bow 150 - biora OD RAZU, otwieram i zamiast moich lukow leza
wymienione (gorsze); jesli nikt nie ma skilla - otwieram i sa te same luki
plus komunikat, ze ponad poziom jednostek". Zadnego czekania na bitwe.
**Zmiana:** (1) warunek wymiany: AnyoneCanUse - w kompanii musi byc jednostka
spelniajaca wymogi wkladu (bron wg skilla, pancerz wg atletyki); jak nie ma:
zero wymiany, wklad zostaje na liscie gracza, komunikat "no man of the company
can handle the X (needs Bow 150; the best of them has Y)". (2) wydawane stare
sztuki NIE ida do sakw - zostaja w magazynie PRZEKSIEGOWANE na gracza
(ksiega wkladow +stare/-nowe): otwierasz ponownie i na liscie zamiast twoich
t6 leza stare do zabrania/sprzedania.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — WYMIANA BARTEROWA kwatermistrza + zamiennik za-skill przy spawnie
**Mod:** Armoury | **Pliki:** `QuartermasterLaw.cs` (NoteDeposit/ProcessSwaps), `SkillsDecide.cs` (PatternFor), `DragonUnmount.cs` (log + swap)
**Zlecenie Jeffa:** "wrzucam luki t6 do armii - lucznicy maja je wziac, a MNIE
wydac 4 gorsze luki, ktore sprzedam". Plus diagnoza "kwatermistrz nie przyjal":
przyjal (wklady widoczne na liscie - tak dziala skarbiec), tylko od wrzucenia
NIE BYLO bitwy (wydanie = spawn), a jedynym ryzykiem jest wymog Bow 150 tego
200-funtowego luku.
**Zmiany:** (1) wymiana barterowa: wklady tej sesji ekranu (bronie bez amunicji
+ pancerze) rejestrowane; przy ZAMKNIECIU ekranu za kazda wlozona sztuke
kwatermistrz wydaje DO SAKW najgorsza wojskowa sztuke TEGO SAMEGO typu
o nizszej wartosci (piny z ksiegi i wklady gracza nietykalne), a wklad
przechodzi na wlasnosc wojska (ksiega -1). Komunikat "Quartermaster's
exchange: ... N of their old pieces are yours to sell". Bez gorszej sztuki -
wklad zostaje wkladem (mozna cofnac). (2) spawn za-skill: bron ponad
umiejetnosci NIE zostawia golego slotu - zolnierz dostaje najlepsza bron
W RAMACH skilla (PatternFor), a log "ItemReq: <troop> nie udzwignie <item>
(Requires ...) - dostaje <swap>" mowi wprost, czemu np. luk 150 nie poszedl.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — okno wyniku kucia TAKZE dla lukow z CRAFT i pancerzy BK
**Mod:** Armoury | **Pliki:** `Armoury/src/Forge.cs` (Smith), `Armoury/src/FletchForge.cs` (BkCraftPostfix)
**Problem (Jeff):** "wykulem luk i nie pojawilo sie okienko ze statystykami -
miales to zrobic dla lukow i pancerzy!". CraftPopup byl podpiety tylko w
Forge.Finish/Deliver (projekty z naszego menu kuzni), a luk kuty w zakladce
CRAFT (BK) idzie natychmiastowa sciezka Forge.Smith - bez okna.
**Zmiana:** CraftPopup.Show w Forge.Smith (luki/kusze/pancerze robione od reki;
amunicja bez popupu - seryjna, spam) oraz w BkCraftPostfix dla pancerzy kutych
droga BK (jakosc odczytana z OSTATNIEGO stacka itemu w sakwach - swieze
laduje na koncu rosteru).
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — DUBEL WYKRYTY PRZEZ JEFFA: jency z bitwy nie sa juz obszukiwani
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs` (OnPrisonerTaken)
**Problem (Jeff, celne):** "zbieram lupy po bitwie, potem biore jencow i sa
rozbierani do naga - czy to nie podwojne liczenie?". TAK, BYL DUBEL: sprzet
pokonanych JUZ trafia do lupow po kazdej bitwie (realna - DTE zbiera z pola;
symulowana - pelny drop przez SimBattleFullDrop), a jeniec z bitwy to jeden
z pokonanych - obszukanie dokladalo DRUGI komplet (szablonowy) za ten sam
rynsztunek.
**Zmiana:** OnPrisonerTaken sprawdza, czy jeniec przyszedl Z BITWY (PlayerMapEvent
/ PlayerEncounter.Battle zyje). Z bitwy -> tylko aktualizacja ksiegi jencow
(zeby hourly/menu ich pozniej nie "doszukalo"), ZERO obszukiwania. Obszukiwanie
zostaje wylacznie dla kapitulantow bez walki (dialog na mapie, poddanie band) -
tam zaden lup z pola nie padl, wiec rozbieranie jest jedyna zdobycza.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — SKARBIEC WOJSKA: lupy 60% niewidoczne, gracz rusza TYLKO wlasne wklady, starocie znikaja
**Mod:** Armoury | **Pliki:** `ArmouryBehavior.cs` (ksiega wkladow + TrimWarStores), `QuartermasterLaw.cs` (escrow + ledger), `MusterBook.cs` (IsPinnedItem + depozyty)
**Model Jeffa (jego projekt, wprost):** "po bitwie wojsko przezbraja sie, ale
lupy 60% NIE sa widoczne dla mnie w Armoury; widze tylko to, co sam wrzucilem
(dam im lepsze - moge ruszac swoje); sprzet wymieniony po prostu znika".
**Zmiany:** (1) Ksiega wkladow gracza (arm_player_stock, SyncData): kazdy ruch
na ekranie zbrojowni ksiegowany (wkladasz + / wyjmujesz -), przenosiny
z ksiegi musztry (sakwy/worn -> stores) tez licza sie jako wklad. (2) Escrow
ekranu zbrojowni przebudowany: zamiast chowac "noszone", chowa CALY skarbiec
wojskowy - lista pokazuje wylacznie sztuki do wysokosci wkladu gracza
(komunikat "the company war-chest is the men's, not yours"). (3) TrimWarStores
po kazdej swiezej bitwie (okno spoils): wojskowa czesc magazynu trzyma per typ
najwyzej tylu sztuk, ilu ludzi (amunicja x2) - najgorsze nadwyzki znikaja
("wymienione znika"); wklady gracza i itemy przypisane w ksiedze NIETYKALNE.
**Co NIE zmienione:** przydzial DTE (ubiera z calosci), MendingBench [STORES]
(naprawa sluzy wojsku), ksiega musztry (rozkazy widza caly stan).
**Ryzyko / co sprawdzic:** stare sztuki sprzed ksiegi wkladow beda NIEWIDOCZNE
(ksiega startuje pusta) - to zgodne z modelem (to lupy wojska); jesli Jeff mial
w magazynie COS wloznego wczesniej recznie, odzyska przez ponowne wlozenie
czegokolwiek?... NIE - stare wklady przepadly na rzecz wojska (jednorazowy
koszt wdrozenia). Ekran zbrojowni po bitwie: lista krotka = poprawne.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — SKAD SLONIE NA POLNOCY (nasza stajnia!) + audyt podzialu lupow 60/40
**Mod:** Armoury | **Pliki:** `Armoury/src/Stables.cs`, `Armoury/src/BattlefieldLaw.cs`
**Problem (Jeff):** (1) "skad slonie na polnocy?"; (2) "sprawdz, czy nie ma
podwojnego otrzymywania lupow i czy 60% dla wojska / 40% dla mnie jest
respektowane".
**Ustalenie (1):** lancuch sloni domkniety - ROT daje sloniowi is_merchandise
+ item_category="horse", wiec: (a) ekonomia rozwozila go po targach calej mapy
(Winterfell!), (b) NASZA Stajnia AI kupowala lordom slonie JAKO KONIE na
awanse kawalerii (IsPlainMount przepuszczal kategorie horse) - stad slonie
w bagazach partii Polnocy i w lupach Jeffa. FIX: IsPlainMount wyklucza
elephant/rot_elephant_*/dragon_*.
**Audyt (2), przeplyw lupow:** podzial jest FIZYCZNY i pojedynczy: DTE znosi
sprzet zabitych do magazynu wojska; nasz postfix WYJMUJE z magazynu 40%
(PlayerLootSharePercent) do kolejki gracza - wojsko zatrzymuje 60%, nic nie
istnieje w dwoch miejscach. Bagaze pokonanych to OSOBNA pula (idzie do gracza
w calosci przez ekran). Jency - osobna (zywi, nie zabici). Symulacje - vanilla.
ZNALEZIONA I DOMKNIETA szczelina: gdy ekran lupow przyszedl PO flushu
godzinowym (kolejka juz oddana do sakw), na ekranie zostawala VANILLOWA
LOTERIA = druga nagroda. Teraz realna bitwa zawsze czysci ekran do zera
przed wsypka naszej dzialki.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — lupy: 3% wciskane przez Spoils PO filtrze + slonie z bagazy pokonanych
**Mod:** Armoury | **Pliki:** `BattlefieldLaw.cs` (CleanseTrash), `ArmouryBehavior.cs` (CleanseTrashInBags + menu-hak), `LegendaryLaw.cs` (SweepWorld bagaze)
**Problem (Jeff po bitwie):** (1) "dostalem ekwipunek z 3% zuzycia" - Spoils
naklada stany NA KONCU, po naszym CleanseTrash w AfterGenerateLoot, wiec prog
nie mial jak ich zlapac. (2) "w bitwie nie bylo sloni, a po pladrowaniu mam
slonie - slonie ma Zlota Kompania, wywal!" - pokonane partie wozily slonie
W BAGAZACH (nakupione przed kwarantanna targow); loot bagazy = slonie.
**Zmiana:** (1) CleanseTrashInBags wolane TAKZE przy kazdym otwarciu menu
mapy (po bitwie zawsze) - smieci <=progu i slonie-towar wylatuja z sakw
i magazynu od reki, zanim gracz je zobaczy w ekwipunku. (2) filtr lupow
(CleanseTrash) tnie elephant/rot_elephant_* niezaleznie od stanu; przy okazji
warunek legend w lupach przelaczony na LegendaryLaw.IsLegend (zbior fabryczny -
spojnosc z fixem lukow). (3) SweepWorld przy wczytaniu czysci slonie takze
z bagazy WSZYSTKICH partii AI (zrodlo wysycha). Bojowe slonie Zlotej Kompanii
zyja w szablonach jednostek - NIETKNIETE.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — lista naprawy: limit ucinal pancerze (zbierz->sortuj->tnij) + czystka starych 3%
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`, `Armoury/src/ArmouryBehavior.cs`
**Problem (Jeff, slusznie wsciekly):** "pancerze SA uszkodzone i chce je
naprawic, a lista ich nie ma!". Przyczyna: MendPickConsequence cielo liste
limitem MaxItemsListed W TRAKCIE iteracji rosteru - masa zbitych broni z
wielkich bitew zapychala limit, a pancerze z dalszej czesci sakw nie wchodzily
WCALE; sort (dodany pozniej) dzialal juz po cieciu. Stad "kiedys bylo widac"
(mniej broni w sakwach), teraz nie.
**Zmiana:** (1) zbierz WSZYSTKIE zbite sztuki (grzbiet + sakwy + [STORES]),
kazda w osobnym try (jedna zla sztuka nie ucina reszty), POTEM sort po typach,
NA KONCU limit ekranu (min 10; log ile przycieto). (2) Na polecenie Jeffa
("usun jednak te 3%"): CleanseTrashInBags przy kazdym wczytaniu wyrzuca
z sakw i magazynu DTE sztuki o stanie <= LootMinConditionPercent (stare wraki
sprzed progu; nowych prog nie wpuszcza) - komunikat "The ruined scraps are
thrown out".
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — 999 pancerzy ROT bez modifier_group: pancerz NIGDY nie mial stanu (WearGroups)
**Mod:** Armoury | **Pliki:** `Armoury/src/WearGroups.cs` (nowy), `ArmouryBehavior.cs`
**Problem (Jeff + screeny):** "mam mase rzeczy w ekwipunku, a na liscie naprawy
TYLKO bron!". Lista naprawy filtruje po stanie (<100%), a pancerze zawsze 100%.
**Przyczyna (twardy dowod z danych):** ROTassets.xml: 999 itemow pancernych,
0 z modifier_group. Bez grupy gra nie umie nadac stanu (dented/rusty/...),
wiec CALY lancuch zuzycia pancerzy byl martwy od poczatku: WearTheTroops,
loot-wear, obszukiwanie jencow, [STORES] - wszystko dzialalo tylko na
vanillowych broniach (te maja grupy). RBM MA ujemne stany pancerne
(dented 60%, rusty 30%, damage_50 10%) - lezaly niepodpiete.
**Zmiana:** WearGroups.Fix przy kazdym wczytaniu: item pancerny bez grupy
dostaje grupe wg materialu (Plate->plate, Chainmail->chain, Leather->leather,
reszta->cloth); luki/kusze bez grupy -> bow/crossbow. Backing field
ItemComponent.<ItemModifierGroup>.
**Ryzyko / co sprawdzic:** log "WearGroups: nadano grupy ... pancerzom";
OD TERAZ pancerze zaczna sie zuzywac (WearTheTroops 12%/bitwa!) i pojawiac
na liscie naprawy oraz w [STORES]; stare sztuki w sakwach pozostaja czyste
(stan nadaje sie przy zdarzeniach, nie wstecz). Ceny lupow pancernych spadna
(stany < 100%) - to zamierzone.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — KONFLIKT DWOCH PRAW LEGEND naprawiony (drogie luki wracaja do kucia) + liczniki zapasow + [WORN BY YOU] + diagnostyka sortu CRAFT
**Mod:** Armoury + ForgeView | **Pliki:** `LegendaryLaw.cs` (BuildLegendSet), `MusterBook.cs` (MenOf/SupplyLine/worn), `ForgeView/src/SortKnownFirst.cs` (log)
**Problem (Jeff):** (1) "wykulem 3x Ravens' Teeth, leza w sakwach - ksiega ich
nie widzi"; (2) "dragon war bow nosze, nie moge wykuc nowego - info LEGENDARY,
mialo byc odblokowane"; (3) "brakuje info ile lukow juz maja, ile brakuje";
(4) "dostepne wzory CRAFT na dole listy zamiast na gorze".
**Przyczyna (1)(2):** kolizja dwoch systemow legend: nowy prog 100k (Legendary
Law) lapal tez DROGIE LUKI -> SweepWorld nadawal im NotMerchandise -> stara
regula kuzni (Recipes.IsLegendary = NotMerchandise + wartosc => "legenda moze
byc tylko JEDNA") blokowala kucie, a Fits ksiegi je odfiltrowywal.
**Zmiana:** BuildLegendSet RAZ na proces, PRZED SweepWorld: legenda = bron
z FABRYCZNYM is_merchandise=false i wartoscia 100k+ (wszystkie klingi lore
maja te flage w XML) plus lista person. Drogie luki i seryjne valyriany
(fabrycznie kupowalne) wypadaja spod prawa legend - kuwalne bez limitu,
widoczne w ksiedze, nieruszane przez sweepy. (3) ksiega: liczniki "x12/25"
i "In the stores: 12 for 25 men - 13 SHORT" (magazyn, sakwy, worn).
(4) zrodlo [WORN BY YOU]: zalozona sztuka schodzi z grzbietu na stan przy
przypisie. ForgeView: log "SortKnownFirst: znane N na gore" - jesli ekran
dalej pokazuje odwrotnie, ktos sortuje PO nas (do namierzenia z logu).
**WYMOG:** flagi itemow zyja w RAM procesu - zeby odswiezyc fabryczne,
Jeff MUSI zrobic PELNY restart gry (do pulpitu), nie sam load save'a.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — Mending Bench widzi MAGAZYN wojska + sort po typach na obu listach
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`, `Armoury/src/MusterBook.cs`
**Problem (Jeff + screenshot):** "nie moge naprawic calego ekwipunku, widze tylko
bron". Przyczyna: zbite PANCERZE wojska leza w magazynie DTE (zuzycie WearThe
Troops dziala na stanie kompanii), a lista "Pick a damaged piece" czytala tylko
sakwy i grzbiet gracza - tam faktycznie leza glownie zbite bronie z lupow.
Plus: "sortuj rzeczy po typach - wszystkie luki, potem strzaly, straszny chaos".
**Zmiana:** (1) trzecie zrodlo naprawy [STORES]: zbite sztuki z magazynu DTE
na liscie (slot=-2), naprawa (kowal za zloto albo wlasne rece) wraca CZYSTA
sztuka na stan wojska; licznik w podpowiedzi opcji ("N in bags, W on back,
S in the company stores"). (2) wspolny TypeRank (luki, strzaly, kusze, belty,
1H, 2H, drzewce, oszczepy, tarcze, helm->plaszcz, kon, rzad; w typie tier
malejaco) - sortuje liste naprawy ORAZ liste przypisania w ksiedze musztry.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — TroopFit: jednostki dostaja skille do WLASNEGO szablonu (audyt + wyrownanie)
**Mod:** Armoury | **Pliki:** `Armoury/src/TroopFit.cs` (nowy), `LegendaryLaw.cs` (wywolanie po sweepie), `Settings.cs` + `McmSettings.cs` (gen; 261)
**Zlecenie Jeffa:** "przejrzyj jednostki - czy maja umiejetnosci do noszonego
pancerza, koni i broni, aby na swoim tierze niesc swoj tier sprzetu".
**Zmiana:** przy kazdym wczytaniu (PO sweepie legend, zeby nie liczyc wymogow
z klingi, ktora znika): dla kazdej jednostki liczymy najwyzszy wymog kazdego
skilla w JEJ szablonie (bron wg klasy, kon wg Riding, pancerz wg Athletics
z difficulty ROT) i podnosimy skill DO wymogu (nigdy w dol; SetPropertyValue
na DefaultCharacterSkills przez reflection). Efekt: elita ma miesnie do swojej
plyty - zasada nadrzedna nikogo nie rozbiera z ZAMIERZONEGO sprzetu, a dalej
broni przed absurdem (rekrut nie udzwignie zbroi elity z magazynu).
Raport do logu: liczba wyrownanych + top 15 deficytow ("TroopFit: ...").
**Ryzyko / co sprawdzic:** log po wczytaniu - skala deficytow ROT; jesli
jakis skill-set jest wspoldzielony miedzy jednostkami, podniesienie w gore
moglo podniesc tez blizniacze (nieszkodliwe kierunkowo).
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — CALY EKWIPUNEK pod zasada skilla (konie=Riding, pancerz=Athletics) + AUDYT calosci
**Mod:** Armoury | **Pliki:** `ItemReq.cs` (nowy), `SkillsDecide.cs`, `MusterBook.cs`, `MusterOut.cs`, `DragonUnmount.cs`
**Zlecenie Jeffa:** "Konie i pancerze tez! Caly ekwipunek!" + "przejrzyj wszystkie
zmiany, zrob audyt czy nic sie nie wyklucza".
**Zmiany:** (1) ItemReq - jeden egzekutor wymagan: bron/kon wg RelevantSkill+
Difficulty, PANCERZ wg ATLETYKI (ROT wpisuje difficulty pancerzom - od teraz
obowiazuje). (2) MusterBook: sloty konia i rzedu (10/11) w karcie jednostki;
wyszarzanie wg ItemReq wszedzie. (3) SkillsDecide: TopArmor liczone w RAMACH
atletyki jednostki (kubelki 25); wzorzec konia = najlepszy kon w ramach Riding
(smoki i slonie poza wzorcem, pin moze slonia gdy Riding pozwala); piny
wszystkich slotow przez ItemReq. (4) Spawn-net (DragonUnmount): na scenie
bron ponad skill schodzi, pancerz ponad atletyke -> najlepszy dozwolony,
kon ponad Riding -> najlepszy dozwolony albo pieszo.
**AUDYT - kolizje znalezione i NAPRAWIONE w tym buildzie:**
(a) AutoSort partii kasowal wybor skladu do bitwy (BattleMuster) - teraz sort
NIE dziala w trakcie MapEventu; (b) pin luku/kuszy bez amunicji w slotach -
FillFree doklada wzorcowy kolczan/sajdak; (c) dobor DTE mogl wydac sztuke
ponad wymogi (wzorzec ograniczony, ale "closest" z magazynu nie) - domkniete
spawn-netem.
**AUDYT - swiadome konsekwencje (bez zmian):** prog lupu 3% de facto wygasza
wraki (decyzja Jeffa); NAJWAZNIEJSZE: zasada atletyki moze ROZEBRAC elitarne
jednostki z najciezszych zbroi szablonowych (difficulty 140-200 vs ich
Athletics) - to wprost wynika z zasady nadrzednej; obserwowac w bitwach,
w razie przegiec dostroimy.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — ZASADA NADRZEDNA: wymogi umiejetnosci swiete + pancerz celuje w gore
**Mod:** Armoury | **Pliki:** `Armoury/src/SkillsDecide.cs`, `Armoury/src/MusterBook.cs`
**Zlecenia Jeffa:** (1) "nie moge dac luku ponad wymagania - opcja wyszarzona,
nie da sie go dac" + "zasada umiejetnosci jest NADRZEDNA: jesli ich nie masz,
nie mozesz uzywac". (2) "czy AI nie zacznie rozbierac lucznikow do bazowego
wzorca pancerza?" - zasadne: DTE co bitwe dobiera sprzet NAJBLIZSZY wzorcowi,
a wzorzec pancerza byl bazowym szablonem (t3 sciagaloby w dol mimo t6 na stanie).
**Zmiany:** (1) MusterBook: pozycje ponad wymogi jednostki (RelevantSkill/
Difficulty) WYSZARZONE z podpowiedzia "Requires Bow 150 - this troop has 90"
(magazyn i sakwy); SkillsDecide pomija pin ponad wymogi (stare piny tez).
(2) SkillsDecide: wzorzec pancerza (sloty 5-9 bez pinu, gdy szablon ubiera slot)
= NAJLEPSZA sztuka danego typu w grze (TopArmor, bez koron) - "najblizsze
wzorcowi" znaczy odtad "najlepsze, co magazyn ma"; degradacja do szablonu
niemozliwa. Zasada zapisana w pamieci projektu (skill-rule-supreme).
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — ksiega musztry v1.1: sakwy gracza na liscie, przeniesienie do Manage Armoury
**Mod:** Armoury | **Pliki:** `Armoury/src/MusterBook.cs`
**Zgloszenia Jeffa po pierwszym tescie:**
(1) "nie widzi lukow, ktore wykulem i mam w ekwipunku" -> lista przypisania
pokazuje teraz MAGAZYN + TWOJE SAKWY (pozycje z dopiskiem [YOUR BAGGAGE]);
wybor sztuki z sakw PRZENOSI wszystkie jej egzemplarze na stan magazynu
(kwatermistrz wydaje tylko ze stanu). (2) "muster book powinno byc w manage
armoury" -> opcja przeniesiona z menu miasta/wioski/kuzni DO submenu magazynu
DTE (army_armory_submenu, tuz pod wejsciem w ekran magazynu). (3) Pytania
wyjasnione: przypis to WZORZEC dla calego typu - 1 luk na stanie = dostaje go
1 zolnierz, reszta bierze najblizsze z magazynu; karta jednostki pokazuje
WZORZEC (szablon + [ASSIGNED]), nie aktualnie noszone sztuki (te roznia sie
per zolnierz wedle zapasow); wymieniony stary sprzet zostaje na stanie
magazynu i mozna go odebrac w Manage Armoury (mechanika DTE). Ravens' Teeth
Longbow to zwykly item (nie legenda) - nie bylo go na liscie, bo lezal poza
magazynem; po tej zmianie widac go, gdy jest w sakwach albo na stanie.
UWAGA: ma difficulty=150 (wymog Bow 150) - autodobor go unika u slabszych,
ale pin gracza wymusza.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-29 — COFNIETE: oddawanie sprzetu przez zwalnianych (Jeff: MA BYC ODWROTNIE)
**Mod:** Armoury | **Pliki:** `Armoury/src/MusterOut.cs` (przepisany), `Settings.cs` + `McmSettings.cs` (gen; 260)
**Powod:** Jeff wprost: "MA NIE ODDAWAC - zwalniany zolnierz odchodzi ZE SWOIM
ekwipunkiem, tak ma byc! Masz to zmienic, ma byc odwrotnie". Wczesniejsza
interpretacja jego zgloszenia byla bledna.
**Zmiana:** mechanika oddawania rynsztunku do magazynu przy zwolnieniu USUNIETA
w calosci (kod + ustawienie DismissedLeaveGear). Zwalniany odchodzi po staremu
(vanilla), magazyn kwatermistrza nietkniety. AutoSort partii ZOSTAJE (patch
tylko na PartyScreenLogic.Initialize).
**Status:** COFNIETE (dismissal) / WGRANE (auto-sort bez zmian)

## 2026-08-29 — KSIEGA MUSZTRY: podglad jednostek i przypisywanie sprzetu z magazynu
**Mod:** Armoury | **Pliki:** `Armoury/src/MusterBook.cs` (nowy), `SkillsDecide.cs` (piny), `SubModuleMain.cs`, `Settings.cs` + `McmSettings.cs` (gen; 261)
**Zlecenie Jeffa:** "klikam grupe -> popup jednostki: doswiadczenie do awansu,
pelne uzbrojenie, i przypisuje jaki pancerz/bron maja nosic - pojedynczo lub
dla calej grupy".
**Zmiana:** opcja "Open the muster book" w menu miasta, wioski i naszej kuzni.
Lancuch okien z portretami/miniaturami: (1) lista oddzialow [formacja tX] nazwa
xN z XP do awansu w podpowiedzi; (2) karta jednostki: 9 slotow (4 bronie,
helm/korpus/buty/rekawice/plaszcz) z aktualna sztuka i znacznikiem [ASSIGNED];
(3) wybor przedmiotu Z MAGAZYNU DTE pasujacego do slotu (+ opcja powrotu do
wzorca kompanii). Pin zapisuje sie w save (armouryMusterBookPins) i dziala na
CALY stack tego typu: SkillsDecide wstrzykuje pin do referencji zolnierza
przed rozdaniem sprzetu (bron sloty 0-3 nadpisuje logike skilli; pancerz 5-9
wprost). Legendy i smoki nie do przypisania.
**Ryzyko / co sprawdzic:** przydzial faktyczny zalezy od zapasow magazynu
(kwatermistrz wydaje "as stores allow"); pin broni obchodzi dobor wg skilli -
to swiadoma decyzja gracza. Sprawdzic, ze [ASSIGNED] sztuki laduja na
zolnierzach w bitwie.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — pakiet armii: popup wyniku kucia, pelne modyfikatory jakosci, zwalniani oddaja sprzet, samonaprawa wojska, auto-sort partii
**Mod:** Armoury | **Pliki:** `CraftPopup.cs` (nowy), `QualityRich.cs` (nowy), `MusterOut.cs` (nowy), `TroopSelfMend.cs` (nowy), `Forge.cs`, `ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs` + `McmSettings.cs` (gen; 260 ustawien)
**Zlecenia Jeffa 29.08:**
(1) "kucie pancerza/luku ma otwierac popup ze statami jak przy broniach, plusy
przy master, minusy przy fuszerce" -> CraftPopup.Show w Forge.Finish i Deliver:
kazda stata z wartoscia po modyfikatorze i roznica w nawiasie.
(2) "masterwork dotyka tylko jeden wskaznik, w vanilli bylo wiecej" -> winowajca
RBM (RBMCombat_item_modifiers.xml: legendary_sword = SAM damage 20). QualityRich
przy wczytaniu dopisuje brakujace staty TYLKO gdy pole==0: bron reczna Speed
+2/+4/+6 (fine/master/legendary), strzelecka MissileSpeed +3/+6/+9, fuszerka
na minus (inferior/poor).
(3) "zwalniani odchodza z bronia i pancerzem - zabrac" -> MusterOut: snapshot
skladu przy otwarciu ekranu partii, diff przy zamknieciu; ubytek niepokryty
awansem (UpgradeTargets) = zwolnieni; ich wyposazenie wg szablonu (ze stanem
bojowym, bez choragwi/smokow/legend) wraca do magazynu DTE.
(4) "wojsko ma naprawiac ze swojego zoldu" -> TroopSelfMend: kazdego dnia
w MIESCIE do TroopSelfMendPerDay (10) najgorszych sztuk z magazynu wraca do
stanu czystego - z kieszeni wojska, gracz nie placi. Reszta - recznie jak dotad.
(5) "jednostki maja sie same segregowac: kawaleria, konni lucznicy, piechota,
strzelcy, po tierze" -> AutoSort przy kazdym otwarciu ekranu partii (rebuild
rosteru, XP jedzie ze stackiem, herosi na gorze).
**Nastepny krok (zapowiedziany):** ksiega musztry - podglad i przypisywanie
sprzetu per jednostka/grupa (pin do ReferenceEquipment w SkillsDecide).
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-29 — FREEZE od naszego profilera (COFNIETY) + porzadek w godzinach kuzni + smelt legend oddaje komplet czesci
**Mod:** CrashScribe + Armoury | **Pliki:** `CrashScribe/src/SubModuleMain.cs` (Sampler off), `Armoury/src/ArmouryBehavior.cs`, `Armoury/src/SmithMenu.cs`, `Armoury/src/SmeltTab.cs`
**Problem 1 (FREEZE 08:06, gra wisi na glucho):** nasz Sampler (profiler FF)
robil Suspend+StackTrace na watku glownym co 0.5 s; zlapanie watku w srodku
locka (profil: 17% Monitor.Enter) = zakleszczenie gry NA STALE. COFNIETE -
Sampler wylaczony na twardo (dane zebrane: FF muli od Campaign.RealTick,
czyli symulacji silnika, nie od modow). Straznik zawieszen zostaje (Suspend
tylko przy realnym hangu, rzadko).
**Problem 2 (Jeff: "straszny balagan z godzinami kuzni"):** trzy rozne liczby
z trzech zrodel: BK liczy godziny PRZY kowadle, nasz komunikat po wykuciu mowi
o dodatkowych godzinach WYKANCZANIA u kowala (np. 33h dla Albion IV), a menu
czekania w kuzni pokazuje SUME wszystkich zlecen w osadzie. Do tego XP projektu
"van" liczylo sie z pancerzowego przelicznika (DaysPerTier zamiast
WeaponDaysPerTier). Zmiany: komunikaty mowia wprost CZYJA to praca i CO znaczy
liczba ("the SMITH'S finishing work - he works it himself, wherever you ride";
wait-menu: "about N hours until ALL your work here is done, he needs no
watching"); XP "van" z wlasciwego przelicznika. Zasada bez zmian: kowal konczy
sam (ForgeWorksWithoutYou), czekanie w zamku liczy sie CELOWO.
**Problem 3 (Jeff: "smelt Brightroar musi dac wszystkie czesci"):** czesci
legend sa ukryte w projektowniku, wiec posiadacz oryginalu nie mialby jak go
odkuc po rozbiorce. DoSmeltingPostfix: przetop LEGENDY odkrywa (IsHiddenOn
Designer=false) i odblokowuje (OpenPart) WSZYSTKIE jej czesci - jedyna droga
do odkucia legendy to miec i rozebrac oryginal. Dziala dla kazdej legendy.
**Status:** WGRANE (oba DLL; watcher wgral 08:10 po ubiciu gry, Armoury
dograne recznie z ostatnim buildem)

## 2026-08-28 — kuznia nie powiela legend: dedykowane czesci ukryte w projektowniku
**Mod:** Armoury | **Pliki:** `Armoury/src/LegendaryLaw.cs` (LockLegendPieces)
**Problem:** Jeff: "jak wykuje legende, to moze byc wiecej niz jedna - popraw to".
ROT daje czesci legend (brightroar_blade/guard/handle/pommel...) z is_default=true -
kazdy kowal mogl zlozyc legende od reki i mnozyc unikaty.
**Zmiana:** przy wczytaniu czesci skladowe legendarnych klng dostaja
IsHiddenOnDesigner=true - znikaja z projektownika kuzni. Bez klingi Brightroara
nie zlozysz Brightroara. Czesci WSPOLDZIELONE ze zwykla bronia zostaja widoczne
(normalne kucie nietkniete) - wystarczy jedna ukryta dedykowana czesc.
**Ryzyko / co sprawdzic:** log "N dedykowanych czesci legend ukrytych w kuzni";
w smithingu nie widac czesci typu Brightroar Blade; zwykle wzory kuja sie
jak dotad.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-28 — legendy precz TAKZE z magazynow armii AI + prawda o "atletyce pancerzy"
**Mod:** Armoury | **Pliki:** `Armoury/src/LegendaryLaw.cs` (SweepAiArmories)
**Problem:** Jeff: "usun z innych armii AI unikatowe bronie - moze byc jedna
na swiecie i ktos ja nosi, ale nie ze polowa armii ja ma". Wirtualne magazyny
DTE partii AI (EveryoneCampaignBehavior.PartyArmories, public static
Dictionary<MBGUID, Dictionary<ItemObject,int>>) byly pelne legend z recyklingu.
**Zmiana:** SweepAiArmories przy kazdym wczytaniu + raz dziennie: klucze
IsLegend wypadaja ze wszystkich magazynow partii AI. Bohaterowie NOSZACY
swoje klingi - nietykani (ich egzemplarz siedzi w equipmencie w save).
**Ustalenie (dekompilacja, nie zgadywanie):** pancerze NIE maja wymogu
umiejetnosci w silniku - ItemObject.RelevantSkill zwraca skill tylko dla
broni (klasa broni) i koni (Riding), dla zbroi null; CanUseItem sprawdza
wymog tylko przy RelevantSkill != null; UI (ItemMenuVM) pokazuje wymog tez
tylko wtedy. "Athletics" przy pancerzu w tooltipie to perk FormFittingArmor
(ZMNIEJSZA WAGE zbroi), nie wymog zalozenia. ROT wpisuje difficulty w XML
pancerzy (np. 200), ale silnik tego dla zbroi nie egzekwuje.
**Ryzyko / co sprawdzic:** log "LegendaryLaw: magazyny AI - N legend przepadlo
z M partii" po wczytaniu.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-28 — STATYSTYKI RZADZA SPRZETEM: koniec limitu tier+2, bronie wg umiejetnosci
**Mod:** Armoury | **Pliki:** `Armoury/src/SkillsDecide.cs` (nowy), `Settings.cs` + `McmSettings.cs` (gen), `SubModuleMain.cs`
**Problem:** Jeff: "max 2 tiery wyzej jest bezsensowne - wojsko moze uzywac
czego chce, jesli statystyki pozwalaja. Glowna bron = najwyzsza umiejetnosc,
zapasowa = druga. Lucznik: luk + 2 kolczany + bron z drugiego skilla".
**Zmiana (SkillsDecideEnabled, MCM "Skills rule the gear"):**
(1) prefix na DTE PartyEquipmentDistributor.GetMaxAllowedTier -> zawsze 6
(limit "domyslny tier +2" zdjety). (2) postfix na ctor DTE Assignment:
ReferenceEquipment (PRYWATNA kopia per zolnierz - Assignment robi Clone,
szablony nietkniete) dostaje sloty broni wg skilli jednostki: glowna klasa =
najwyzszy skill; zapas = najwyzszy z pozostalych (po glownej strzeleckiej
zapas musi byc reczny); luk/kusza dostaja 2 kolczany/2 sajdaki; tarcza
z szablonu zostaje przy broni jednorecznej. Wzorzec kazdej klasy = najlepsza
bron, ktorej Difficulty <= skill jednostki (kubelki co 25 pkt, cache) -
DTE dobiera potem z magazynu "najblizsze wzorcowi", wiec wymagania
statystyczne steruja doborem. Legendy i smoki poza wzorcami.
**Ryzyko / co sprawdzic:** sklad broni jednostek w bitwie ma odpowiadac ich
skillom (lucznik z mieczem zamiast wloczni przy wyzszym OneHanded); konie
i pancerze bez zmian logiki (pancerze nie maja wymagan w grze). Gdyby armia
wygladala dziwnie - wylacznik SkillsDecideEnabled w MCM.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-28 — AUDYT LEGEND: mlot Roberta (x6!) i persony bez value + czystka totalna sakw
**Mod:** Armoury | **Pliki:** `Armoury/src/LegendaryLaw.cs`, `Armoury/src/DragonUnmount.cs`
**Problem:** Jeff: "mam 6 mlotow Roberta Baratheona - jest JEDEN na swiecie!
Usun wszystkie unikaty u mnie (mlot tez), zrob pelny audyt przedmiotow".
**Audyt (ROTassets.xml, 1197 itemow):** wszystkie klasyczne legendy maja value
150k-350k (Ice 350k, Widow's Wail/Oathkeeper/Dawn/Longclaw/Blackfyre/Dark Sister
300k, valyriany 200k, Renly 180k, Gregor 175k, Skull 150k) - prog 100k je lapie.
ALE bronie-persony jako CraftedItem BEZ wpisanego value (wartosc liczona z czesci,
kilka tys.): baratheon_hammer (Robert!), needle (igla Aryi), gendry_hammer -
prog ich nie lapal. Do tego ROT ma masowa jednostke "Baratheon Hammerman"
(ROT-Troops.xml) - mloty Roberta sypaly sie z kazdej bitwy ze Stormlands.
**Zmiana:** (1) LegendIds - jawna lista person (baratheon_hammer, needle,
gendry_hammer); IsLegend = lista LUB value>=prog; uzywana wszedzie (sweep
szablonow, rostery losowane, spawn, lupy przez CleanseTrash NIE - tam tylko
prog... UWAGA: CleanseTrash w BattlefieldLaw filtruje po floor - ale legendy
z listy maja niska wartosc i nie wpadna do progu; sweep szablonow i spawn-net
je tna u zrodla, wiec do lupu nie maja skad wpasc). (2) Czystka sakw gracza
v2 (klucz armouryLegendsCulledAll): WSZYSTKIE legendy znikaja co do sztuki -
takze zostawione wczesniej kolekcjonerskie po 1.
**Ryzyko / co sprawdzic:** log "LegendaryLaw: sakwy gracza - N legendarnych
broni usunieto CO DO SZTUKI" + "The stolen legends are gone from your packs";
Baratheon Hammerman w bitwie ma zwykly mlot t5.
**Status:** WGRANE (gra byla zamknieta)

## 2026-08-28 — PRAWO LEGEND: nazwane klingi wracaja do unikatowosci
**Mod:** Armoury | **Pliki:** `Armoury/src/LegendaryLaw.cs` (nowy), `Armoury/src/DragonUnmount.cs`, `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff: "legendarne bronie sa unikatowe, nie moga wszyscy wojacy w nich
biegac - wywalic nadwyzki u mnie i u wszystkich. Moga miec zwykla klinge t5/t6.
Tak bylo w oryginalnym ROT??" TAK - zrodlo w danych ROT 8.1.8: szablony wladcow
(vla_bat_template_tywin z Item.brightroar, value=300000) maja culture=
neutral_culture + IsLordTemplate, wiec gra LOSUJE je przypadkowym bohaterom;
polegli oddaja klingi do magazynow DTE, DTE ubiera w nie szeregowych - stad
46x "Brightroar Silver" w sakwach po wielkiej bitwie. Nikt ich nie kul.
**Zmiana (LegendaryLaw, prog value >= LegendaryLootValueFloor 100k):**
(1) SweepTemplates przy kazdym wczytaniu: legendy znikaja z szablonow JEDNOSTEK
(nie-bohaterow) ORAZ z losowanych rosterow szablonowych (MBEquipmentRoster) -
zamiast nich najlepszy ZWYKLY odpowiednik tej samej klasy broni, tier <= legendy.
Istniejacy wladcy (Tywin itd.) trzymaja swoje kopie w save - bohaterow nie tykamy.
(2) Siatka przy spawnie misji (w DragonUnmount): szeregowy z legenda (np. z
magazynu DTE) dostaje zamiennik na polu. (3) Jednorazowa czystka sakw gracza
(SyncData armouryLegendsCulled): z kazdej klingi zostaje JEDEN egzemplarz
w najlepszym stanie, nadwyzki znikaja.
**Ryzyko / co sprawdzic:** log "LegendaryLaw:" - ile zdjeto z szablonow, jaki
zamiennik za co ("zamiennik dla brightroar -> ..."), ile nadwyzek usunieto
("The named blades are one of a kind again"). Wladca w bitwie MA swoja klinge.
**Status:** WGRANE (gra byla zamknieta, DLL 16:59)

## 2026-08-28 — cztery zlecenia Jeffa: wolny zaciag, wolne gojenie, smieci/legendy poza lupem, slonie w kwarantannie
**Mod:** Armoury | **Pliki:** `SlowMuster.cs` (nowy), `SlowHealing.cs` (nowy), `ElephantQuarantine.cs` (nowy), `BattlefieldLaw.cs` (CleanseTrash + prog wrakow), `ArmouryBehavior.cs` (tick sloni), `Settings.cs` + `McmSettings.cs` (gen), `SubModuleMain.cs`
**Problem (Jeff):** (1) "straty nie sa odczuwalne, zaraz sa nowi rycerze i rekruci";
(2) "regeneracja zdrowia na mapie za szybka, ma byc dwa razy dluzsza, perki niech
dzialaja"; (3) sakwy pelne legendarnych mieczy na 3% (Brightroar x12, Brightroar
Silver x46, Widow's Wail...) - "czy to nie unikaty?!" + "wszystko ponizej 3%
uznajemy za zniszczone, nie pojawia sie w loocie"; (4) "moge kupic slonia
w Winterfell - zrob cos!".
**Zmiany:** (1) SlowMuster: postfix na BKVolunteerModel.GetDailyVolunteerProduction
Probability x VolunteerRegenPercent (50%) - lordowie i gracz rowno. (2) SlowHealing:
postfix AddFactor -50% na GetDailyHealingForRegulars/HpForHeroes (HealingRegen
Percent=50) - perki licza sie normalnie, wynik ciety na koncu. (3) BattlefieldLaw.
CleanseTrash w obu sciezkach lupu: modifier <= LootMinConditionPercent (3%) =
zniszczone; bron o value >= LegendaryLootValueFloor (100k - wszystkie nazwane
klingi ROT, np. brightroar value=300000) nie leza w workach; AppendWrecks nie
wpuszcza wrakow ponizej progu (WreckModifier "heavy" = te 3% z ekranu Jeffa).
(4) ElephantQuarantine: slon (elephant, rot_elephant_*) schodzi z targu osad
o innej kulturze niz przedmiot (volantine) - DailyTickSettlement + wejscie gracza.
**Zrodlo legend w sakwach:** ROT daje nazwane miecze elitarnym JEDNOSTKOM w ich
szablonach (brightroar w equipment setach), a DTE sciaga je z zabitych - stad
46 sztuk "unikatu". Sztuki JUZ w sakwach Jeffa zostaja (jego decyzja co z nimi).
**Ryzyko / co sprawdzic:** wraki na 3% znikaja z lupow CALKIEM (WreckSalvage
de facto wygaszony przy progu 3 - obnizenie progu w MCM je przywraca); komunikat
"lup przesiany - N zniszczonych, M legendarnych odpadlo" w logu. Gojenie: wpis
"Wounds knit slowly" w rozpisce leczenia.
**Status:** WGRANE (watcher 4h - gra dzialala przy buildzie)

## 2026-08-28 — wybor skladu jak w hideoucie (OpenTroopSelection), koniec triku z rannymi
**Mod:** Armoury | **Pliki:** `Armoury/src/BattleMuster.cs` (przepisany)
**Problem:** Jeff: "po co kombinowac z rannymi - jest gotowy widok jak przy
hideout, lista wojska i klikam ktorych biore; tylko powiekszyc limit z 15 na
tyle slotow ile mam w tej bitwie".
**Zmiana:** dokladnie wzorzec hideoutu: args.MenuContext.OpenTroopSelection(
roster, preselekcja GetStrongestAndPriorTroops, CanChange, OnPicked, max, 1).
Limit = szacowane sloty gracza: GetRealBattleSize()/2 * (nasi zdrowi / zdrowi
calej naszej strony), clamp [1, nasi]. Po zatwierdzeniu wybrane ODDZIALY ida
na gore rosteru (zdjecie i dolozenie stackow nie-bohaterow, XP wraca ze
stackiem) - scena spawnuje od gory, wiec wybrani wchodza w sloty PIERWSI,
reszta czeka jako posilki. Zero rannych, zero SyncData, zero przywracania.
Warunek bez zmian: po stronie gracza walczy ktos poza jego partia.
**Ryzyko / co sprawdzic:** kolejnosc jednostek w ekranie party zmienia sie po
wyborze (wybrani na gorze) - to zamierzone. Sloty to estymata - dokladny
przydzial i tak robi silnik przy spawnie.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-28 — odwod przy WSPOLNEJ bitwie (druga korekta warunku)
**Mod:** Armoury | **Pliki:** `Armoury/src/BattleMuster.cs`
**Problem:** Jeff (po pierwszej korekcie "tylko join_encounter"): "moze tez byc
MOJA bitwa - opcja gdy po mojej stronie walczy wiecej niz tylko ja".
**Zmiana:** opcja w obu menu (encounter + join_encounter), ale warunek:
w MapEvencie po stronie gracza wiecej niz 1 partia (sojusznicy w tym samym
starciu) ALBO menu dolaczenia do trwajacej bitwy. Sam na sam - opcji nie ma,
wystawiasz wszystkich jak w vanilli.
**Status:** WGRANE (watcher - gra dzialala przy buildzie)

## 2026-08-28 — odwod przed bitwa: wybierasz KIM walczysz (jak przy hideout)
**Mod:** Armoury | **Pliki:** `Armoury/src/BattleMuster.cs` (nowy), `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff: "jak dolaczam do czyjejs/wspolnej bitwy, chce wybrac jakie
wojsko wystawiam - jak przy hideout".
**Zmiana:** nowa opcja w menu potyczki ("encounter" i "join_encounter"):
"Hand-pick who fights (the rest wait in reserve)". Otwiera ekran party
(PartyScreenHelper.OpenScreenWithCondition) - na LEWA strone ("Reserve") odsylasz
tych, ktorzy maja przeczekac. Mechanizm bez patchowania misji: odwod na czas
bitwy liczy sie jako RANNI (gra nie wystawia rannych ani na scenie, ani w
autoresolve), po bitwie (MapEventEnded) wstaje zdrowy. Zapisywane w SyncData +
self-heal przy wczytaniu (gra padla w bitwie = odwod tez wraca). Faktycznie
rannych nie dotykamy (clamp do stanu sprzed i po).
**Ryzyko / co sprawdzic:** licznik rannych w party ROSNIE na czas bitwy o odwod
- to zamierzone; komunikaty "The reserve stands down/falls back in". Sprawdzic,
ze po bitwie liczba rannych wraca do prawdy.
**Status:** WGRANE

## 2026-08-28 — DragonUnmount: wyjatek dla Daenerys i gracza
**Mod:** Armoury | **Pliki:** `Armoury/src/DragonUnmount.cs`
**Problem:** Jeff: "smoki ma tylko Daenerys, ona ma trzy! Inni maja nie miec.
Chyba ze gracz z questa." Pierwsza wersja patcha zdejmowala smoka KAZDEMU.
**Zmiana:** przy spawnie przepuszczamy smoka, gdy jezdzcem jest Daenerys
(StringId lord_1_14, ROT-Content lords.xslt) albo postac gracza. Cala reszta
(wylosowane szablony, lupy DTE u band) traci wierzchowca jak dotad.
**Ryzyko / co sprawdzic:** bitwa z Dany - jej smok ma zostac; log "DragonUnmount:"
dalej wypisuje kazde zdjecie z nazwa jednostki.
**Status:** WGRANE

## 2026-08-28 — smoki-wierzchowce schodza przy spawnie misji (crash/hang wielkiej bitwy)
**Mod:** Armoury | **Pliki:** `Armoury/src/DragonUnmount.cs` (nowy), `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff wszedl w koncu w te bitwe (3000+ wojska): "sa dwa smoki na polu
bitwy, czyje to smoki?" i hang w trakcie (hang-14-28-20: watek glowny "deep in
native"). Wczesniej trzy crashe przy wejsciu w TE SAMA bitwe.
**Przyczyna:** ktos w tej bitwie ma smoka (dragon_*) w SLOCIE WIERZCHOWCA -
zrodla: ROT-owy szablon lorda ghi_bat_template_dany (dragon_black) i/lub wirtualne
magazyny DTE band (bandyci lupia pokonanych; DTE ekwipuje AI). Silnik krztusi sie
smokiem jako zwyklym koniem w polowej bitwie. Do tego skala: 3000 agentow +
DTE RandomizeNonHeroLedAiPartiesArmor=true (losowanie pancerzy AI przy spawnie).
**Zmiana:** DragonUnmount - prefix na Mission.SpawnAgent: dragon_* w slocie konia
(10) zdjety PRZED spawnem (+ rzad konski, slot 11), jednostka idzie pieszo.
Log "DragonUnmount: dragon_X zdjety przy spawnie z: <nazwa>" powie CZYJ byl smok.
Smoki spawnowane przez ROT jako osobne stwory - nietkniete.
**Ryzyko / co sprawdzic:** wejsc w te bitwe raz jeszcze; w Armoury.log szukac
"DragonUnmount:". Hang od samej skali 3000 moze zostac - to limit silnika,
osobna rozmowa (Battle Size w opcjach gry).
**Status:** WGRANE

## 2026-08-28 — TRZECI crash w bitwie: pulapka na pierwotny wyjatek (przed reporterem BLSE)
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs` (CrashCallerTattle + Install)
**Problem:** trzeci crash przy wejsciu w te sama bitwe (14:17, pozycja identyczna
z 14:08). Imiona kultur zalatane NA PEWNO ("dziurawych zostalo 0" w logu 14:17:01),
a crash wrocil - czyli to INNY blad. W logu za kazdym razem tylko wtorne bledy
reportera BLSE (Silk.NET/AsmResolver); pierwotny wyjatek byl powtorka wczesniejszego
i dedup Scribe.Report polykal go bez sladu (drukuje tylko #5, #25, #250...).
Ostatnie zycie przed smiercia: Armoury.log 14:17:51 DressCode ubiera Forest Bandit/
NW Ranger Recruit/Highwayman + BattleWind init - czyli pada w trakcie SPAWNU misji.
**Zmiana:** prefix na BLSE ExceptionInterceptorFeature.HandleException - pelny
pierwotny wyjatek (typ, message, stos, inner do 5 poziomow) laduje BEZ DEDUPU
w Documents\...\CrashScribe\crash-original-*.log, zanim reporter cokolwiek zrobi.
**Ryzyko / co sprawdzic:** Jeff wchodzi w te bitwe raz jeszcze; po crashu czytamy
crash-original-*.log i naprawiamy w punkt. Zero wplywu na rozgrywke.
**Status:** WGRANE

## 2026-08-28 — DRUGI crash w bitwie: karmienie imion mialo dziure (zenskie listy) + bezpiecznik + PROFILER x3
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`, `CrashScribe/src/Sampler.cs` (nowy), `CrashScribe/src/SubModuleMain.cs`
**Problem:** Jeff: drugi crash przy wejsciu w bitwe (14:08), mimo karmienia kultur.
Log 14:03:41: TEN SAM ArgumentNullException NameGenerator - PO nakarmieniu 14 kultur.
Plus Jeff: "ogromny spadek FPS na forward x3, nie da sie grac - sprawdz to".
**Przyczyna:** (1) karmienie bralo JEDNEGO dawce po sumie imion - wygral looters;
jesli dawca sam nie ma ktorejs listy (zenskie u bandytow!), biorcy dalej mieli
tam null i duchowna-kobieta wywalala jak przedtem. (2) FPS: nikt nie mierzyl,
w czyim kodzie stoi watek glowny na przyspieszeniu.
**Zmiana:** (1) dawca OSOBNO dla listy meskiej/zenskiej/klanowej + samokontrola
w logu ("dziurawych zostalo N"). (2) bezpiecznik: prefix na NameGenerator.
GetNameListForCulture - kultura z null/pusta lista dostaje liste zapasowa
najbogatszej kultury, zero wyjatkow, nawet dla kultur spoza object managera.
(3) Sampler.cs: TYLKO na fast-forwardzie poza misja co 0.5 s zdejmuje stos watku
glownego (mechanizm straznika zawieszen) i co ~30 s pisze do logu "PROFIL
FAST-FORWARD: NN% ramka <mod>" - po paru minutach gry na x3 log powie, kto zre.
**Ryzyko / co sprawdzic:** po wczytaniu w logu "dziurawych zostalo 0"; po grze
na x3 wpisy "PROFIL FAST-FORWARD" - przeslac mi. Suspend watku raz na 0.5 s
tylko na przyspieszeniu - koszt pomijalny, ale gdyby cokolwiek dziwnego, wylaczyc
mozna przez usuniecie linii Sampler.Start w SubModuleMain.
**Status:** WGRANE

## 2026-08-28 — CRASH przy wejsciu w bitwe: kultura bez list imion zabija NameGenerator
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs` (FeedNamelessCultures), `CrashScribe/src/WarReport.cs`
**Problem:** Jeff: "crash jak wszedlem do bitwy". Log 11:11:48: ArgumentNullException
w NameGenerator.GetNameListForCulture <- HeroCreator.CreateSpecialHero <- BannerKings
Religion.GenerateClergymanHero (BKSettlementBehavior.TickSettlementData). O 11:15:58
wyjatek wyszedl na ApplicationTick i BLSE otworzyl crash-reporter (jego wtorne bledy
Silk.NET/AsmResolver zaslonily pierwotny slad). Moment bitwy = przypadek, bomba
tykala w tle na kazdym dziennym ticku osad.
**Przyczyna:** kultura (ROT-owy preset wiary) bez wpisow male_names/female_names
w XML ma MaleNameList/FemaleNameList = null; vanilla GetNameListForCulture robi
na null IsEmpty() -> ArgumentNullException. Pada tworzenie KAZDEGO bohatera takiej
kultury: duchowni BK, dzieci, wedrowcy.
**Zmiana:** Mends.FeedNamelessCultures przy OnSessionLaunched: kultury z null/pusta
lista imion (meskie/zenskie/klanowe) dostaja referencje list od najbogatszej kultury.
Log wypisze KTORE kultury byly dziurawe ("kultura X nie miala list imion").
**Ryzyko / co sprawdzic:** imiona nowych bohaterow dziurawych kultur beda obce
(kulture duchownego i tak prostuje latka "kaplan jest stad"). W logu po wczytaniu
szukac "kultur bez imion nakarmione" - lista pokaze, co ROT ma zepsute.
**Status:** WGRANE

## 2026-08-28 — MULENIE GRY: lawina BK relations odcieta brama PRZED metoda (Essos bez tytulow)
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs` (EssosTitleGate + Install)
**Problem:** Jeff: "strasznie muli gra". Log sesji 10:43-11:00: "BK relations uratowane
(NullReference), lacznie 12901 razy" w 17 minut = ~13 wyjatkow NA SEKUNDE. Finalizer
ratowal przed crashem, ale samo rzucanie i lapanie tylu wyjatkow muli gre.
**Przyczyna:** jednorazowy pelny slad (uzbrojony 27.08) nazwal null po imieniu:
hero=Rhogaro of Tolarra (notabl, qartheen, osada ROT_town37_village3), target=Daario
(lord). W galezi notabl->lord BKRelationsModel.CalculateModifiers robi
GetTitle(hero.CurrentSettlement).DeFacto - a pol Essos NIE MA tytulow feudalnych
w BK, wiec GetTitle daje null i kazda para (essoski notabl x lord) pada codziennie.
**Zmiana:** prefix EssosTitleGate na CalculateModifiers: notabl bez osady albo
z osada bez tytulu -> pusta lista modyfikatorow (to samo, co po wywrotce oddawal
finalizer) i pominiecie oryginalu - zero wyjatkow. Finalizer SafeRelations zostaje
na nieznane przypadki. Log "odciete N razy" co 2000.
**Ryzyko / co sprawdzic:** licznik "BK relations uratowane" ma przestac rosnac
(albo prawie); w zamian rzadkie "relacje notabl-lord bez tytulu osady (Essos) -
odciete". Relacje notabli essoskich z lordami traca modyfikatory BK - i tak ich
nie mialy (wyjatek ucinal metode w tym samym miejscu).
**Status:** WGRANE (gra byla zamknieta, DLL juz w grze)

## 2026-08-28 — smok w bitwie PO STRONIE GRACZA: czystka smokow z sakw i magazynu DTE
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs` (CleanseDragonStables)
**Problem:** Jeff: "mialem smoka w bitwie po mojej stronie, wywal go". Smoki z lupow
lezaly nie tylko w sakwach - takze w magazynie DTE (ArmyArmory), a DTE ubiera
kawalerzystow z magazynu, wiec zolnierz wyjechal do bitwy NA SMOKU.
**Przyczyna:** stare lupy (sprzed filtra CleanseDragons) zdazyly wpasc do sakw
i do zbrojowni DTE; zaden sanitizer ich stamtad nie wymiatal.
**Zmiana:** CleanseDragonStables - usuwa wszystkie dragon_* (typ Horse) z sakw
MainParty i z QuartermasterLaw.DteArmory(); wolane przy SessionLaunched ORAZ przy
kazdym otwarciu menu mapy (po bitwie menu zawsze sie otwiera, wiec smok wylatuje
zanim DTE go znowu osiodla). Komunikat graczowi tylko gdy cos usunieto.
**Ryzyko / co sprawdzic:** trzy smoki z sakw ZNIKNA przy nastepnym wczytaniu
(decyzja Jeffa: "wywal"). W logu szukac "Smocza stajnia: dragon_black x..".
**Status:** WGRANE (DLL czeka na zamkniecie gry - watcher)

## 2026-08-28 — smoki w sakwach: zrodlo znalezione + druga dziura zatkana (jency)
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs` (TryStripNewCaptives)
**Problem:** Jeff: "jakim cudem w moim ekwipunku mam trzy smoki" + "ja nie pokonalem
zadnych smokow". Grep po WSZYSTKICH modulach: jedyny przedmiot dragon_* w wyposazeniu
to `ghi_bat_template_dany` w ROT_sandboxcore_equipment_sets.xml (szablon lorda,
culture=neutral_culture, slot Horse = Item.dragon_black). Zaden zwykly troop nie ma
smoka.
**Przyczyna:** vanilla dorzuca do puli lupow wierzchowce pokonanych BOHATEROW - takze
w bitwach auto-rozstrzyganych, gdzie smoka nie widac na oczy. Lord z wylosowanym
szablonem Dany w pokonanej armii = smok w lupach. Filtr CleanseDragons byl juz
napisany, ale DLL czekal na zamkniecie gry - kolejne bitwy dosypywaly.
Druga dziura: TryStripNewCaptives (obszukiwanie jencow) tez bierze sloty konskie
(CaptiveSpoilsIncludeMounts=true) i NIE przechodzi przez CleanseDragons.
**Zmiana:** w petli slotow obszukiwania jenca pominiecie dragon_* typu Horse -
"smoka nie poprowadzisz na powrozie". Lupy bitewne filtruje CleanseDragons (juz
w kodzie), jency filtrowani u zrodla.
**Ryzyko / co sprawdzic:** trzy smoki JUZ w sakwach zostaja (nic ich nie usuwa) -
Jeff decyduje: usunac czy zostawic. Nowe smoki nie maja prawa wejsc zadna sciezka.
**Status:** WGRANE

## 2026-08-28 — kuznia: JEDNA linia czasu u kowala (projekty w kolejce, nie rownolegle)
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs` (AdvanceProjects)
**Problem:** Jeff: "jestem w trakcie wytapiania mieczy a moge znowu wejsc do kuzni
i wytapiac... powinna byc jedna linia czasu, dopoki nie zakoncze wytapiac tamtych".
Kazdy zlecony projekt tykal ROWNOLEGLE - kowal w jednej osadzie robil piec rzeczy
naraz.
**Przyczyna:** AdvanceProjects odejmowal DaysLeft kazdemu projektowi w kazdej osadzie
bez zadnej kolejnosci.
**Zmiana:** HashSet busyForge po SettlementId - w danej osadzie tyka TYLKO najstarszy
niedokonczony projekt; reszta czeka w kolejce (gotowe do odbioru wydaja sie normalnie).
Zlecac mozna dalej ile wlezie - ale kowal kuje po kolei.
**Ryzyko / co sprawdzic:** wiszace projekty z wielu osad tykaja niezaleznie (kazda
osada = wlasny kowal, wlasna kolejka). Czas oddania zlecen zlozonych "na raz" liczy
sie teraz szeregowo.
**Status:** WGRANE

## 2026-08-28 — COFNIETE: browar i garbarnia miejska (dosypki podazy); zostaje ciecie popytu AI
**Mod:** Armoury | **Pliki:** `Armoury/src/BkSupplyTemper.cs` (z AleSupply.cs), `Armoury/src/SubModuleMain.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen)
**Powod:** Jeff 28.08: "USUN te dodatkowe browary i garbarnie" - po scieciu popytu AI
(1 dzien zapasu + sufit 12 sztuk na dobro) sztuczna podaz jest zbedna, vanillowa
produkcja ma wystarczyc. Behavior AleSupply usuniety w calosci (dosypki beer/wine/
mead/leather/hides), ustawienia AleSupply*/TannerySupply* wyleciely z MCM.
Zostal BkSupplyTemper (BkSupplyDaysCap=1, BkSupplyMaxPieces=12) - sekcja MCM
"The lean quartermasters".
**Co sprawdzic:** po paru dniach gry targi maja miec alkohol i skore z SAMEGO
spadku popytu; jesli dalej pustynia - wracamy do rozmowy o podazy.
**Status:** COFNIETE (dosypki) / WGRANE (ciecie popytu)


> **ZASADA OBOWIAZKOWA DLA KAZDEGO CLAUDE (kazde konto, kazdy komputer):**
> po **kazdej** zmianie w kodzie dopisz wpis na GORZE tej listy i zrob commit + push.
> Dzieki temu drugie konto zawsze wie, co sie stalo, bez czytania calej rozmowy.
>
> Format wpisu:
> ```
> ## RRRR-MM-DD — krotki tytul
> **Mod:** nazwa | **Pliki:** sciezki
> **Problem:** co bylo nie tak (objaw u gracza + dowod z logu, jesli byl)
> **Przyczyna:** co konkretnie w kodzie
> **Zmiana:** co zrobiono
> **Ryzyko / co sprawdzic:** na co patrzec w grze i w logu
> **Status:** WGRANE / COFNIETE / DO SPRAWDZENIA
> ```

---

## 2026-08-27 — odwet kryjowki NAPRAWDE goni + ekran lupow po przeszukaniu + min 4h + koniec czerwonych TEMP
**Mod:** Armoury | **Pliki:** `Armoury/src/HideoutPurge.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen)
**Problem:** Jeff: "wojska sie nie zebraly i mnie nie scigaly". Log 17:52: odwet ruszyl
(15 band, 160 vs 245), ale przeszukanie przy 132 ludziach trwalo 2 h gry = 2 SEKUNDY
realne - bandy nie mialy kiedy dojsc, a SetInitiative to sugestia, ktora AI odrzuca
widzac przewage gracza. Plus: po przeszukaniu ma byc ekran lupow jak po bitwie,
minimum 4 h zamiast 2, i zadnych czerwonych TEMP w tlach menu.
**Zmiana:** (1) odwet rusza JUZ przy zwyciestwie (OnHideoutBattle), nie przy starcie
paska. (2) TWARDY ROZKAZ: MobilePartyAi.SetAiBehavior(EngageParty, gracz) przez
refleksje (internal) + SetInitiative(2.0, 48h), PONAWIANY co ~3 s realne przez cala
dobe odwetu - takze po spladrowaniu (chca swojego zlota; pack czyszczony z martwych).
(3) BuildLoot: lup przedmiotowy kryjowki (tier 1-3 z kramow, 3+bandy+los sztuk,
czasem beczka piwa) pokazywany ekranem lupow jak po bitwie
(InventoryScreenHelper.OpenScreenAsLoot) na czystej mapie po przeszukaniu.
(4) HideoutSearchMinHours 2 -> 4. (5) SetSceneBackground na obu menu kryjowki
(grafika kultury gracza, fallback wait_fallback) - koniec czerwonych TEMP.
**Ryzyko / co sprawdzic:** bandy ida na gracza w trakcie i PO przeszukaniu (do 24 h);
po pasku ekran lupow z przedmiotami; tla menu normalne. SetAiBehavior przez refleksje -
gdy sygnatura sie nie zgadza, pogon dziala tylko na inicjatywie (log bez bledow).
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — odwet: bandy SCALAJA sie w jedna horde (Jeff: "pojedynczo nie maja przewagi i uciekaja")
**Mod:** Armoury | **Pliki:** `Armoury/src/HideoutPurge.cs`
**Zmiana:** przy odwecie caly pack scala sie w najwieksza bande: MemberRoster,
PrisonRoster i ItemRoster przechodza do bossa, oproznione bandy znikaja
(DestroyPartyAction). Jedna horda z suma sil dostaje rozkaz pogoni. Log
"odwet rusza JEDNA HORDA (N band scalono, M ludzi...)".
**Ryzyko / co sprawdzic:** po kryjowce na mapie pojawia sie jedna duza banda
zamiast rozsypanych; horda przekracza limit wielkosci bandy - to zamierzone
(nie rekrutuje, tylko goni). Bandy w trakcie wlasnych bitew nie sa scalane.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — kryjowka: czas przeszukania od liczby rak + ODWET okolicznych band (3:1 = tchorza)
**Mod:** Armoury | **Pliki:** `Armoury/src/HideoutPurge.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen)
**Problem:** Jeff: przeszukanie ma zalezec od ludzi ("sam - caly dzien, minus 0.5 h
za czlowieka, minimum 2 h"), a okoliczni bandyci maja sie zebrac i odbic kryjowke
w trakcie grzebania - chyba ze przewaga gracza 3:1, wtedy podchodza i uciekaja.
**Zmiana:** (1) czas = max(HideoutSearchMinHours, HideoutSearchSoloHours - 
HideoutSearchPerManHours x (ludzie-1)); domyslnie 24h solo, -0.5h/czlowiek, min 2h
(stare HideoutSearchHours zastapione trzema pokretlami). (2) Reprisal przy starcie
przeszukania: bandyckie partie w promieniu HideoutReprisalRadius (20) sumuja sile
(EstimatedStrength); gracz >= HideoutReprisalFleeOdds (3.0) x ich sila -> meldunek
"one look at your line and they melt away"; ponizej -> SetInitiative(2.0, 24h)
na kazda bande - natywna smialosc ataku gry, spotkanie przerwie przeszukanie.
**Ryzyko / co sprawdzic:** czas przeszukania w meldunku logu zalezy od wielkosci
partii; przy malych silach gracza bandy z okolicy ida na niego w trakcie paska.
SetInitiative to sugestia dla AI, nie rozkaz - starcie zalezy od ich wlasnej oceny.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — ZNIKAJACY EKWIPUNEK, przyczyna wlasciwa: ekran zbrojowni DTE traktowany jak SMIETNIK przy discard-za-XP
**Mod:** Armoury | **Pliki:** `Armoury/src/QuartermasterLaw.cs`
**Problem:** Jeff doprecyzowal: "wchodze w inventory, zakladka pancerzy, discarduje
pancerz za XP dla wojska (perk) - i znika pozostaly ekwipunek, kazda kategoria".
Wczorajsza hipoteza przetopowa byla NIETRAFIONA (fix i sanacja zostaja - ujemne
stosy naprawiaja sie same przy wczytaniu).
**Przyczyna:** ekran zbrojowni DTE otwiera sie w trybie, w ktorym vanilla ustawia
CanGainXpFromDiscarding=true (Default bez OtherParty). Wtedy: XP z donacji liczy sie
od _rosters[0] - czyli od CALEJ ZBROJOWNI - a przy zatwierdzeniu ekranu
OnItemsDiscardedByPlayer(_rosters[0]) leci z calym magazynem wojska jako stosem
porzuconych rzeczy. Stad kosmiczne XP "za jeden pancerz" i znikajacy magazyn.
**Zmiana:** postfix na InventoryLogic.InitializeXpGainFromDonations: gdy lewa strona
ekranu to zbrojownia DTE, CanGainXpFromDiscarding gasnie (backing field). XP za
discard dziala dalej na ekranach lupow - tam lewa strona to prawdziwy smietnik.
**Ryzyko / co sprawdzic:** na ekranie zbrojowni discard nie daje XP (i nie kasuje
magazynu); na ekranie lupow po bitwie discard-za-XP dziala normalnie. Log
"QuartermasterLaw: ekran zbrojowni - XP za discard zgaszone".
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — ZNIKAJACY EKWIPUNEK: przetop bez eventu robil ujemne stosy w sakwach (fix + sanacja save)
**Mod:** Armoury | **Pliki:** `Armoury/src/SmeltTab.cs`, `Armoury/src/ArmouryBehavior.cs`
**Problem:** Jeff: "discarduje armour zeby dostac XP [przetop w Smelt] i potem znika
CALY ekwipunek - kazda kategoria". 
**Przyczyna:** vanilla DoSmelting konczy sie eventem OnEquipmentSmeltedByHero, na ktorym
UI odswieza liste przetopu. Nasz DoSmeltingPrefix (pancerze/luki bez WeaponDesign)
NIE odpalal eventu - po zdjeciu OSTATNIEJ sztuki pozycja zostawala klikalna, drugi
klik robil AddToCounts(-1) na nieistniejacym wpisie i w sakwach powstawal stos
o stanie -1. ItemRoster z ujemnym wpisem wywraca caly ekran ekwipunku (pustka we
wszystkich kategoriach). Rzeczy NIE gina - sa w rosterze, tylko ekran ich nie udzwignie.
**Zmiana:** (1) straznik: pozycji nie ma w sakwach -> klik zignorowany, zero zdjec.
(2) po przetopie leci OnEquipmentSmeltedByHero (przez refleksje - dispatcher moze
byc internal) - UI odswieza liste jak w vanilli. (3) CleanseNegativeStacks przy
kazdym wczytaniu: ujemne stosy w sakwach i zbrojowni DTE zerowane, komunikat
"The quartermaster set the ledgers straight...".
**Ryzyko / co sprawdzic:** po wczytaniu save z "pustym" ekwipunkiem wszystko wraca
(sanacja + log "Sanacja sakw: ..."); przetop wielu sztuk tej samej pozycji dziala,
lista odswieza sie po kazdej sztuce.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — depozyt kwatermistrza NIE wchodzi do save'a + pasek przeszukania kryjowki + tygiel wypada z menu
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`, `Armoury/src/HideoutPurge.cs`, `Armoury/src/SmithMenu.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen)
**Problem:** (1) Jeff: "zniknal mi caly ekwipunek!" - FALSZYWY ALARM: ekran zbrojowni
pokazuje tylko nadwyzki (depozyt kwatermistrza schowal 1177 szt. noszonych; wszystko
wraca po zamknieciu ekranu, a save 16:33 byl SPRZED schowania). ALE dziura realna:
schowane sztuki zyja w RAM poza rosterem - zapis gry w tym oknie utrwalilby save
BEZ nich. (2) Jeff: po kryjowce "pasek ile to zajmie, jak po bitwie". (3) Jeff:
"Break metal down at crucible niepotrzebne - smelt jest w smithy".
**Zmiana:** (1) OnBeforeSaveEvent -> ReleaseReserve (przed KAZDYM zapisem depozyt
wraca na polki); samonaprawa w OnGameMenuOpened (menu gry = nie ekran zbrojowni ->
oddaj, gdyby Release przy zamykaniu nie odpalil). ReleaseReserve jest idempotentne.
(2) "Search the hideout" przelacza w wait-menu z paskiem (AddWaitGameMenu, wzorzec
snu): HideoutSearchHours (2 h), nagrody DOPIERO po dojechaniu paska; "Call the search
off" wraca do wyboru. (3) opcja tygla zdjeta z menu kuzni - przetop w zakladce Smelt.
**Ryzyko / co sprawdzic:** zbrojownia po wczytaniu save 16:33 kompletna; przy zapisie
w trakcie otwartego ekranu zbrojowni nic nie ginie; po kryjowce pasek 2 h i dopiero
lup; w menu kuzni brak opcji tygla.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — przetrzebiona kryjowka: przeszukanie po zwyciestwie, zrabowane zloto, renown i wdziecznosc okolicy
**Mod:** Armoury | **Pliki:** `Armoury/src/HideoutPurge.cs` (NOWY), `Armoury/src/Settings.cs`, `Armoury/src/SubModuleMain.cs`, `Armoury/src/McmSettings.cs` (gen)
**Problem:** Jeff: "jak pokonam kryjowke, nie od razu nagroda - musze ja przeszukac,
jak po bitwie; w kryjowce powinno byc zrabowane zloto z okolicznych napasci; renown
powinien pojsc i reputacja w poblizu, im blizej tym wiecej. To big deal."
**Zmiana:** behavior HideoutPurge: po zwycieskiej bitwie w kryjowce
(OnHideoutBattleCompleted, endState=Victory, gracz wygral) na czystej mapie otwiera
sie menu "Search the hideout / Leave without searching". Przeszukanie daje:
(1) zloto = HideoutGoldBase(150) + HideoutGoldPerBand(120) x liczba rozbitych band
(kazda banda zyla z rozboju okolicy) +-25%; (2) renown HideoutRenown(5);
(3) relacje z notablami wiosek/miast/zamkow w okregu HideoutRepRadius(50):
od HideoutRepMax(5) przy samej kryjowce malejaco do 0 na skraju. Vanillowy lup
przedmiotowy bez zmian - nasze lezy "glebiej". 6 ustawien MCM (The hideout purge).
**Ryzyko / co sprawdzic:** po kryjowce menu przeszukania; komunikaty o zlocie
i "Word of the purge spreads"; log "HideoutPurge: ...". Menu aktywuje sie tylko
na czystej mapie (wzorzec z klawisza O). Save miedzy bitwa a menu = nagroda przepada
(swiadomie - stan nie jedzie w save).
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — menu kowala: luczarnia wypada (dublowala zakladke CRAFT)
**Mod:** Armoury | **Pliki:** `Armoury/src/SmithMenu.cs`
**Problem:** Jeff: "wywal z forge kucie lukow i strzal - kucie jest w smithy;
te opcje sie dubluja".
**Zmiana:** opcja "String bows and fletch arrows" (arm_fletcher) zdjeta z menu
Work the forge - luki/kusze/strzaly/belty kuje sie w zakladce CRAFT (ForgeView
wstrzykuje je na liste, FletchForge przejmuje robote). Kod Fletchera zostaje,
CRAFT z niego korzysta. Reszta opcji (pancerze na zamowienie, naprawy, przetop,
rozkladanie wzorow, zamowienia dla wojska) NIE dubluje CRAFT i zostaje.
**Ryzyko / co sprawdzic:** w menu kuzni brak opcji luczarni; kucie strzeleckie
w CRAFT dziala jak dotad.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — kolumna marszowa: czapka predkosci WIDOCZNA w rozpisce (z nazwana przyczyna)
**Mod:** Armoury | **Pliki:** `Armoury/src/MarchPace.cs`
**Problem:** Jeff: "cos jest nie tak z mechanika predkosci na mapie". Czapka kolumny
(LimitMax 4.0/4.2/6.5) ciela tempo BEZ SLADU w tooltipie - partia wlecze sie 4.0
i nie wiadomo czemu (starczy jeden pieszy jeniec bez luzaka).
**Przyczyna:** goly LimitMax nie zostawia pozycji w ExplainedNumber.
**Zmiana:** gdy czapka realnie tnie, do rozpiski predkosci wchodzi UJEMNY wpis
z przyczyna: "Marching column: N afoot" / "baggage train" / "all riders";
LimitMax zostaje jako pas bezpieczenstwa. Arytmetyka bez zmian.
**Ryzyko / co sprawdzic:** tooltip predkosci pokazuje pozycje "Marching column..."
z ujemna wartoscia; suma = czapka. Zasady: ktos pieszo (zolnierz/jeniec bez luzaka)
-> 4.0; wszyscy konno z taborem ponad 1 juczne/4 ludzi -> 4.2; czysta jazda -> 6.5.
Do tego kary snu (dlug >=2) i vanilla kary terenu licza sie PRZED czapka.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — panika: nieumarli bez strachu, koniec zapetlonych krzykow przy rozbiciu; nauka wzorow naprawde losowa
**Mod:** Armoury | **Pliki:** `Armoury/src/BrokenMen.cs`, `Armoury/src/RangedLore.cs`
**Problem:** Jeff: (1) "krzyki zapetlone paniki na mapie bitwy jak jest ucieczka";
(2) "nieumarli nie wpadaja w panike - trzeba zaznaczyc"; (3) "pancerze odkrywane
od samego dolu jeden po drugim, a powinny byc losowo w danym tierze i kategorii".
**Przyczyna:** (1) BrokenMen patrzyl tylko na IsRetreating - przy ROZBICIU armii
ludzie biegna vanillowa ucieczka (IsRunningAway, IsRetreating=false) i pchalismy
Panic() w kolko, a kazde pchniecie gralo wrzask od nowa. (2) BrokenMen nie filtrowal
wightow (FieldCraft juz filtrowal). (3) TryUnlockRandom liczyl cene od WYLOSOWANEGO
wzoru - przy malych punktach przechodzily tylko najtansze trafienia i odkrycia szly
sekwencyjnie od dolu.
**Zmiana:** (1) BrokenMen pomija agentow z IsRunningAway (i czysci ich liczniki).
(2) BrokenMen pomija Undead.Character. (3) TryUnlockRandom: cena = 3 x tier sztuki,
PRZY KTOREJ sie uczysz (kucie: los z [tier..6] za 3xTier kutego; przetop/rozlozenie:
los z [1..tier] za 3xTier sztuki) - kazdy wzor w zakresie ma rowna szanse. Ledger
opisuje nowa cene.
**Ryzyko / co sprawdzic:** rozbita armia ucieka bez chorusu wrzaskow; wighci walcza
do konca; odkrycia wzorow skacza po tierach i pozycjach listy losowo. Kucie t1 moze
odkryc t6 za 3 pkt - zamierzone (Jeff: "losowo, tier lub wyzej, jak przy broniach").
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — martwy dialog preachera: rejestracja ducha w religii BK przy rozmowie + kontekst lawiny relacji
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** Jeff: "jak sie wejdzie w preachera i klika click to continue, nic sie
nie dzieje" - dialog utyka bez zadnej opcji.
**Przyczyna:** powitanie BK (bk_preacher_introduction) i WSZYSTKIE opcje preachera maja
warunek ReligionsManager.IsPreacher. Preacher bez wpisu w menedzerze religii (ROT-owe
osady czesto nie maja religii, wiec CleanClergymen BK go nie rejestruje - rejestruje
tylko przy istniejacej religii osady) wypada z wlasnego dialogu: gra pokazuje cudze
powitanie, po ktorym nie ma ZADNEJ linii do przejscia. W logach cisza - BK lyka.
**Zmiana:** (1) prefix RegisterStrayPreacher na OnConditionClergymanGreeting: preacher
nieznany menedzerowi dostaje wpis do religii (wlasnej, a gdy brak - GetIdealReligion
dla jego kultury) przez Religion.AddClergyman - dialog wstaje w TEJ SAMEJ rozmowie.
Gdy nie ma religii/osady - log i odpuszczamy. (2) SafeRelations: jednorazowy raport
lawiny dostaje KONTEKST (hero/target: kultura/klan/osada, co jest NULL) - slad z 27.08
pokazal tylko ramke inline, nastepny nazwie null po imieniu.
**Ryzyko / co sprawdzic:** rozmowa z preacherem daje opcje (What are you preaching itd.);
log "Mends: preacher X dopisany do religii - dialog ozyl". Preacher moze dostac religie
kultury zamiast "wlasciwej" ROT-owo - lepsze to niz martwy dialog.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — namiot gracza wraca po odbudowie wizerunku + zuzycie sprzetu WOJSKA po bitwach
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`, `Armoury/src/ArmouryBehavior.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen)
**Problem:** Jeff: (1) "jak jest oboz, to nie ma ikony namiotu"; (2) "muster kwatermistrza:
zolnierze zawsze maja 100%, jakby sprzet sie nie psul, nawet jak dostaja zepsute rzeczy".
**Przyczyna:** (1) namiot stawiany RAZ przy rozbiciu obozu, a silnik mapy przy
odswiezeniach widoku (menu, pauza, wczytanie) odbudowuje wizerunek partii - namiot
znika, wraca konik; zadnego mechanizmu przywracania (w logu zero potkniec Tent).
(2) Zuzycie (ApplyWear/ApplyWearPerSlot) tykalo WYLACZNIE Hero.MainHero.BattleEquipment -
zbrojownia DTE zyla wiecznie nowa, wiec muster uczciwie pokazywal 100%.
**Zmiana:** (1) ReassertPlayerTent w OnTick (raz na 5 s, nie co klatke - pulapka
z CLAUDE.md): odcisk palca = liczba dzieci StrategicEntity zapisana przy stawianiu;
gdy sie zmieni, namiot staje od nowa (log "silnik odbudowal wizerunek").
(2) WearTheTroops po kazdej bitwie: dla kazdego typu sprzetu TroopWearPercent (12%)
sztuk W UZYCIU (wg WornFor/CountNeeds) schodzi o JEDEN stopien drabinki modyfikatorow
(ta sama co lupy; sztuki na dnie drabinki zostaja). 2 nowe ustawienia MCM
(Troop Wear Enabled/Percent). Naprawa istniejacym "Mend the men's kit".
**Ryzyko / co sprawdzic:** namiot obozu widoczny takze po wejsciu/wyjsciu z menu
i wczytaniu; po bitwie komunikat "The battle wore the men's kit - N pieces...",
muster pokazuje kondycje < 100% i rosnaca liczbe "worn". Sztuki wkladane recznie
przez NIEKTORE sciezki DTE (AddItemToArmory po ItemObject) gubia modyfikator -
to uproszczenie DTE, nie nasze; nasz system i tak ubrudzi magazyn po bitwach.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — kuznia: kowal pracuje bez ciebie, gotowy wyrob czeka na odbior
**Mod:** Armoury | **Pliki:** `Armoury/src/ArmouryBehavior.cs`, `Armoury/src/Settings.cs`
**Problem:** Jeff: "wykulem miecz, odczekalem dlugo i przepadl". Log 13:44:54 "Projekt
dodany: crafted_item_101|3.02|1|ROT_town3|van|" i potem w kolko "Projekt stoi - gracz
poza osada" - czekal na mapie obok miasta, a zegar projektu tykal TYLKO gdy gracz
stal w osadzie kuzni. Miecz nie przepadl - wisi w kolejce, ale nigdy sie nie konczyl.
**Przyczyna:** AdvanceProjects: `atForge = Settlement.CurrentSettlement == projekt` -
bez tego zero postepu; zadnego komunikatu na ekranie, tylko log.
**Zmiana:** nowe ustawienie ForgeWorksWithoutYou (domyslnie ON): robota idzie ZAWSZE,
XP "za prace w trakcie" tylko gdy gracz na miejscu (jego rece). Wyrob ukonczony pod
nieobecnosc: zostaje w kolejce z DaysLeft=0 i komunikat "The smith has finished your X -
collect it at Y"; wydanie przy wejsciu do osady (CollectReadyProjects w OnGameMenuOpened,
takze na godzinnym ticku na miejscu). ProjectSummary pokazuje "READY, waiting for
collection". Stary tryb (zegar tylko na miejscu) zostaje pod wylacznikiem.
**Ryzyko / co sprawdzic:** wiszacy miecz Jeffa (crafted_item_101 w ROT_town3) dokonczy
sie sam po wgraniu; komunikat o ukonczeniu i odbior przy wejsciu do ROT_town3.
Stare projekty w kolejce ruszaja z miejsca.
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-27 — sen: pasek jest prawda - pobudka dopisuje przespane godziny do rachunku doby
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff: "rozbilem oboz w nocy, przespalem noc (wake rested) i rano mam,
ze men nie spali" - swit doliczyl dlug snu mimo ukonczonego paska.
**Przyczyna:** dwie ksiegowosci. Pasek snu (od wczoraj wlasny licznik z dt) liczy czas
ciagly, a rachunek doby (_restTonight) nalicza sie tylko pelnymi godzinowymi tickami
OnHourly - pierwsza godzina po przyjezdzie na miejsce przepada (regula moved), brzegowe
niepelne godziny nie istnieja. Pasek: 5,0 h "wyspani"; rachunek: 4/5; swit: Debt++.
**Zmiana:** SleepInit zapamietuje stan doby (_menuBase); LeaveSleep po pobudce robi
_restTonight = max(_restTonight, _menuBase + _menuRest) i od razu splaca dlug wspolnym
CreditRest (wydzielone z OnHourly - ta sama splata "od reki" w obu miejscach).
**Ryzyko / co sprawdzic:** po przespanej nocy zadnego "The men marched through the
night" o swicie; splata dlugu ("Well slept...") przychodzi najpozniej przy pobudce.
Sen przechodzacy przez swit daje niewielki kredyt godzin na nowa dobe - zamierzone
(czlowiek dospal swoje po swicie).
**Status:** WGRANE (albo czeka, jesli gra otwarta)

## 2026-08-26 — korona Sansy: reszta pancerza na zero + jednorazowy zwrot z ekwipunku gracza
**Mod:** Armoury | **Pliki:** `Armoury/src/Uniques.cs`
**Problem:** Jeff: "sansa crown body armor 0, usun z mojego ekwipunku i oddaj kase".
**Przyczyna:** korony ROT maja material "plate"; poza glowa nic nie powinny chronic.
Jeff kupil korone jako helm-75 za pelna cene targowa - przed nerfem.
**Zmiana:** (1) wszystkie korony: body/leg/arm armor = 0 (head jak dotad wg ustawienia).
(2) RefundSansa (raz na kampanie, flaga w save): korona Sansy schodzi z sakw ORAZ
z zalozonych slotow (bojowych i cywilnych) gracza, zwrot = pelna wartosc SPRZED nerfa
(odczytana zanim staty zeszly) x liczba sztuk; komunikat "Sansa's Crown returns to
the realm - N denars refunded".
**Ryzyko / co sprawdzic:** po wczytaniu save zielony komunikat o zwrocie i zloto
na koncie; korony brak w ekwipunku. Flaga armouryUniquesSansaRefunded jedzie w save -
korona zlupiona w przyszlosci z lorda NIE zostanie zabrana.
**Status:** WGRANE (DLL czeka na zamkniecie gry)

## 2026-08-26 — CRAFT: znane wzory ZAWSZE na gorze listy (takze po sortowaniu BK)
**Mod:** ForgeView | **Pliki:** `ForgeView/src/SortKnownFirst.cs` (NOWY), `ForgeView/src/ArmourFilterMixin.cs`
**Problem:** Jeff: "lista pokazuje najpierw niedostepne - najpierw dostepne, zeby nie
zjezdzac na dol i nie szukac".
**Przyczyna:** nasz filtr (ArmourFilterMixin.Apply) ustawia znane przed zamknietymi,
ale sortownik BK (ArmorCraftingSortController.SortByCurrentState - przyciski
Type/Name/Yield) sortuje cala liste od nowa i miesza grupy.
**Zmiana:** Harmony postfix na SortByCurrentState: po kazdym sortowaniu BK stabilne
przegrupowanie - znane najpierw, porzadek sortowania zachowany w obrebie obu grup.
Known() z mixina otwarte jako internal.
**Ryzyko / co sprawdzic:** po kliknieciu sortowania Type/Name/Yield znane wzory
zostaja na gorze; szare (nieznane) zawsze pod nimi.
**Status:** WGRANE

## 2026-08-26 — nocleg: oboz BK nie znika w klatke, pasek snu bez podwojnego liczenia, swit nie karze spiacego
**Mod:** Armoury | **Pliki:** `Armoury/src/NightRest.cs`
**Problem:** Jeff: (1) "czasami nie tworzy sie namiot podczas postoju", (2) "postoj nie
zawsze lapie zakonczenie spania i wyspanie", (3) "czasami liczy podwojnie - pasek snu
raz i za chwile ponownie".
**Przyczyna:** (1) sciezka obozu BannerKings (TryBkCamp) ustawiala gola flage
PlayerCamped=true BEZ Tent() - _campPos zostawalo z poprzedniego obozu i straznik
w OnTick (dystans > 0.25) zwijal oboz W NASTEPNEJ KLATCE. (2+3) pasek snu liczyl sie
z _restTonight, a SettleNight o 6:00 ZERUJE _restTonight - sen przez swit: pasek spadal
do zera i startowal od nowa, a swit doliczal dlug "nieprzespanej nocy" komus, kto spal.
**Zmiana:** (1) TryBkCamp idzie przez Tent(true) - ustawia i namiot, i _campPos.
(2) Sen w menu ma WLASNY licznik (_menuRest/_menuTarget, cel = ile brakuje do wyspania),
nalicza z dt ticku menu (noc pelna stawka, dzien wg DayRestFactor), pasek monotoniczny.
(3) Nowa flaga _sleeping (SleepInit/LeaveSleep); SettleNight traktuje spiacego jak
wyspanego - zadnego dlugu w polowie snu.
**Ryzyko / co sprawdzic:** oboz z klawisza O stoi (namiot widoczny takze przy obozie BK),
pasek snu plynie raz od 0 do konca takze gdy sen przechodzi przez 6:00; po wyspaniu
"The men wake rested". Rozliczenie dlugu bez zmian poza switem w trakcie snu.
**Status:** WGRANE

## 2026-08-26 — klejnoty koronne: korony unikatowe (pancerz 10, poza handlem i kuznia, polki oprozniane)
**Mod:** Armoury | **Pliki:** `Armoury/src/Uniques.cs` (NOWY), `Armoury/src/Settings.cs`, `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff kupil korone Sansy na targu: "takie rzeczy musza byc unikatowe, czemu
ona daje tyle pancerza - to tylko korona!".
**Przyczyna:** ROT daje KAZDEJ koronie head_armor=75 (jak pelny helm) i 7 z 8 koron
zostawia jako towar (tylko cersei_crown ma is_merchandise=false). Nazwane miecze
(Longclaw, Ice, Oathkeeper...) ROT juz trzyma poza handlem - koron nie.
**Zmiana:** behavior Uniques (OnSessionLaunched): itemy *_crown dostaja pancerz glowy
UniqueCrownHeadArmor (10), NotMerchandise=true (znikaja z zaopatrzenia sklepow;
przez regule Teachable "pancerz spoza kramow to legenda" nie da sie ich wykuc ani
poznac ich wzoru), zalegajace korony schodza z polek miast i zamkow. Egzemplarze
w rekach bohaterow (kupiona korona Sansy) zostaja jedynymi. 2 ustawienia MCM.
**Ryzyko / co sprawdzic:** log Armoury "Uniques: ... pancerz 75 -> 10, poza handlem"
i "zdjeto z polek N szt."; korona Sansy w ekwipunku ma pancerz 10; koron nie ma
w sklepach ani na liscie kucia. Backing-fieldy (<HeadArmor>k__BackingField) sprawdzone
w TaleWorlds.Core 1.4.8 - gdy nie pasuja, behavior loguje i odpuszcza.
**Status:** WGRANE

## 2026-08-26 — browar miejski: targi utrzymuja piwo i wino (BK wypija krolestwo do dna)
**Mod:** Armoury | **Pliki:** `Armoury/src/AleSupply.cs` (NOWY), `Armoury/src/Settings.cs`, `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff: "nie ma nigdzie alkoholu, zjechalem mase miast - kara do morale jest,
a kupic nie ma czego".
**Przyczyna:** BannerKings PartySupplies kaze KAZDEJ partii kupowac alkohol na 10 dni
zapasu (kategorie wine/beer/mead, BuyItems co tick zaopatrzenia) - setki partii
ogolacaja targi szybciej, niz vanillowa produkcja odrasta. Itemy beer/wine istnieja
(SandBoxCore), ROT ich nie wycina - po prostu popyt BK zjada cala podaz.
**Zmiana:** behavior AleSupply (DailyTickSettlement): kazde miasto co dzien dowarza
piwo i wino do progu (beer 15, wine 8, mead polowa wina - jesli item istnieje),
w granicach dziennej warki (10 szt./miasto). 4 ustawienia MCM.
**Ryzyko / co sprawdzic:** po paru dniach gry piwo/wino sa do kupienia w miastach,
kara morale za brak alkoholu schodzi; log "AleSupply: <miasto> dowarzyl ...".
Podaz to strumyk, nie potop - ceny nie powinny sie zawalic (MarketGlut czuwa).
**Status:** WGRANE

## 2026-08-26 — luczarnia: luki i kusze tieru 5-6 kosztuja x2 materialow
**Mod:** Armoury | **Pliki:** `Armoury/src/Recipes.cs`, `Armoury/src/Settings.cs`
**Problem:** Jeff: "luki i kusze 5 i 6 tieru za latwo sie tworzy, daj x2 zasobow".
**Przyczyna:** receptura luczarni liczyla materialy plasko od wagi/tieru - mistrzowski
luk kosztowal ledwie wiecej niz prosty.
**Zmiana:** dla lukow i kusz tieru >= 5 caly rachunek materialowy x RangedHighTierCostFactor
(nowe ustawienie MCM, domyslnie 2.0, zaokraglenie w gore). Amunicji nie dotyczy.
**Ryzyko / co sprawdzic:** receptura luku t5/t6 w menu kuzni pokazuje podwojone ilosci;
t1-t4 bez zmian.
**Status:** WGRANE

## 2026-08-26 — BattleWind: oddech bitewny bohaterow wg Endurance (pula wykladniczo, regen 10-200 pkt/s, zadyszka)
**Mod:** Armoury | **Pliki:** `Armoury/src/BattleWind.cs` (NOWY), `Armoury/src/FieldCraft.cs`, `Armoury/src/Settings.cs`, `Armoury/src/McmSettings.cs` (gen), `Armoury/src/SubModuleMain.cs`
**Problem:** Jeff: "po 10 machnieciach nie mam sily przy 150 atletyki". Postura RBM przy
Ath/bron 150 to ~175 pkt (atletyka 0 vs 150 = roznica 6 pkt puli), machniecie ~15-20;
regeneracja mala i z gumka DO GORY (im pusciej tym szybciej - odwrotnie niz zadyszka);
atrybut Endurance nie wystepowal w formulach RBM wcale.
**Przyczyna:** formuly RBMAI.Stance (InitializeStamina/InitializePosture/tick*Regen)
+ stary liniowy bonus FieldCraft (4%/pkt END) byl kosmetyczny.
**Zmiana:** nowy BattleWind (Harmony na RBMAI.Stance, TYLKO bohaterowie - gracz, lordowie,
towarzysze; szeregowi zostaja na RBM): (1) pula postury I staminy x 2^((END-2.5)/2.5) -
END 5 = x2, END 10 = x8; postfix na InitializePosture mnozy przy KAZDYM przeliczeniu
(RBM liczy max od zera przy zmianie broni). (2) Regen staminy wykladniczo od END:
10 pkt/s (END 1) -> 200 pkt/s (END 10), razy (1+Ath/500), ciezki korpus >=30 tnie na pol
jak w RBM. (3) Zadyszka zamiast gumki: tempo x (0.25 + 0.75 x stan/max) - pusty pasek
oddycha na 25%. Regen postury x mnoznik END (pasek wiekszy, wraca w tym samym czasie -
balans wymian bez zmian) + ta sama zadyszka. FieldCraft: EndBonus przeszedl na te sama
krzywa wykladnicza, stare jednorazowe mnozenie puli RBM pominiete gdy BattleWind aktywny
(bylby podwojny), oddech sprintera pominiety dla bohaterow BattleWind (dlug oddalby sie
podwojnie). 5 nowych ustawien MCM (Battle Stamina...), StaminaEndurancePerPoint usuniety.
**Ryzyko / co sprawdzic:** log Armoury przy spawnie gracza "BattleWind: gracz END-mul ...,
stamina ..., regen ... pkt/s"; pasek postury wyraznie wiekszy (przy END ~7: ~600 zamiast
175), pusty wraca z wyrazna zadyszka. NPC szeregowi maja zachowanie jak dotad. Custom
battle (bez kampanii): brak bohatera -> czysty RBM. Gdy pola/metody RBMAI sie nie
zgadzaja (inna wersja RBM), BattleWind loguje i odpuszcza w calosci.
**Status:** WGRANE

## 2026-08-26 — CRAFT: skora/len przy kuciu pancerza BK schodza KATEGORIA, nie sztywnym ID
**Mod:** Armoury | **Pliki:** `Armoury/src/FletchForge.cs`, `Armoury/src/Recipes.cs`
**Problem:** Jeff: licznik skory/lnu w lewym dolnym rogu ekranu CRAFT "nie zgadza sie
z iloscia w Armoury".
**Przyczyna:** rozjazd trzech definicji. Licznik (nasz BkExtraMatPostfix) i BK HasMaterials
licza CALA kategorie handlowa (leather+hides+fur...), ale BK CraftingMixin.SpendMaterials
zdejmuje wylacznie item o sztywnym ID "leather"/"linen". Kucie pancerza z samych zamiennikow
ROT wpychalo do sakw UJEMNE wpisy leather/linen - stany rozjezdzaly sie z kazda sztuka.
**Zmiana:** prefix BkSpendMaterialsPrefix na CraftingMixin.SpendMaterials - twarde surowce
1:1 jak oryginal, skora/len przez Recipes.Take (ta sama kategoria co licznik, nigdy na minus).
Recipes.Take -> internal, nowe wlasciwosci Recipes.SoftLeather/SoftLinen.
**Ryzyko / co sprawdzic:** wykuc pancerz majac tylko zamienniki (np. hides, bez czystego
leather) - licznik i ekwipunek maja zejsc spojnie, zadnych ujemnych wpisow w sakwach.
Gdy rachunku nie da sie odczytac (inna sygnatura BK), prefix oddaje robote oryginalowi.
**Status:** WGRANE

## 2026-08-26 — nauka wzorow: kucie odkrywa LOSOWO (tier kutego lub wyzej), przetop nieznanego uczy go w calosci
**Mod:** Armoury | **Pliki:** `Armoury/src/RangedLore.cs`, `Armoury/src/Forge.cs`, `Armoury/src/SmeltTab.cs`
**Problem:** odkrycia szly sztywna kolejka "najtanszy nieznany" - zero niespodzianki,
a przetopienie pancerza nie uczylo jego wzoru (Jeff chcial: "przetapiam - ucze sie tego
dokladnie; kuje - losowo tier lub wyzej, jak przy broniach").
**Przyczyna:** TryUnlock wybieral deterministycznie najtanszy wzor; przetop dawal tylko
punkty (Study), Learn byl wylacznie przy rozlozeniu.
**Zmiana:** (1) TryUnlockRandom(szkola, minTier, maxTier) - losuje kandydata MBRandom-em
z nieznanych wzorow w zakresie tierow; cena bez zmian (3 x tier), jak nie stac - punkty
czekaja. (2) Kucie (OnCrafted): zakres [tier kutego..6]. (3) Nowe OnSmelted podpiete
w obu przetopach (tygiel Armoury i zakladka Smelt): wzor NIEZNANY -> Learn w calosci
+ meldunek; znany -> punkty jak dotad (0.5 x tier) i losowanie [1..tier sztuki].
(4) Nieudane rozlozenie (Study) losuje w zakresie [1..tier sztuki]. (5) Ledger mowi
"next pattern: random (cheapest is tier N)" zamiast obiecywac konkretny wzor.
**Ryzyko / co sprawdzic:** przetop nieznanego pancerza -> komunikat "pattern is yours now"
i wzor znika z szarych; kucie -> baner "New pattern unlocked" z LOSOWYM wzorem o tierze
>= kutego. Punkty i zapis (Export/Import) bez zmian - stare sejwy wczytaja sie normalnie.
**Status:** WGRANE

## 2026-08-26 — CRAFT: nieznany wzor zbroi = caly kafelek wyszarzony (jak czesci broni), nie czerwony napis
**Mod:** ForgeView | **Pliki:** `ForgeView/src/ArmourListColour.cs`
**Problem:** Jeff: "nie napis na czerwono, tylko caly kafelek jak w broniach - posiadana
czesc kolorowa, nieposiadana szara".
**Przyczyna:** poprzednia wersja podmieniala tylko RichTextWidget nazwy (czerwony tekst).
**Zmiana:** ArmourListColour przerobiony z Replace wezla nazwy na Append szarej plachty
(BlankWhiteSquare_9, #141414FF, alpha 0.62, IsVisible=@FvLocked) za ostatnim widgetem
kafelka - przyciemnia CALY wiersz (portret + napisy) dla nieznanych wzorow; znane zostaja
w naturalnych barwach. XPath celuje w wiazanie @ItemTypeText (sprawdzone w prefabu
BK.Redux na dysku). Flaga Applied bez zmian - dopisek "- LOCKED" schodzi jak dotad.
**Ryzyko / co sprawdzic:** w CRAFT nieznane wzory maja byc przyciemnione calym kafelkiem,
znane normalne; log ForgeView "kafelki listy zbroi dostaly szara plachte". Jesli XPath
nie trafi (inny prefab): czerwony komunikat UIExtenderEx i wraca dopisek "- LOCKED".
**Status:** WGRANE

## 2026-08-26 — relacje BK: brama na notabli bez osady (zrodlo lawiny NullReference) + jednorazowy pelny slad
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Mends.cs`
**Problem:** freez gry o 15:02 po 47 min sesji; log: "Mends: BK relations uratowane (NullReference)"
12 201 razy, chwilami 700 w 6 s, plus 2500+ powtorek NRE w HeroRelations.GetHeroesToUpdate
(HeroRelations.cs:115) z DailyTickHero.
**Przyczyna:** BK HeroRelations.GetHeroesToUpdate w galezi dla notabla bez klanu robi
Hero.CurrentSettlement.OwnerClan.Heroes bez null-checkow - notabl bez osady (lub z osada
bez wlasciciela) pada za kazdym razem. Dotychczasowy finalizer SafeRelations lapal tylko
CalculateModifiers (objaw), a lawina kosztowala tyle, ze mrozila watek glowny.
**Zmiana:** (1) Harmony prefix RelationsUpdateGate na HeroRelations.UpdateRelations - notabl
bez klanu i bez osady/wlasciciela jest pomijany w calosci, zanim cokolwiek wybuchnie (log co
500. pominiecie). (2) SafeRelations przy PIERWSZYM zlapaniu pisze pelny raport ze stosem -
w sesji z lawina nie bylo ani jednego sladu, ktora linia CalculateModifiers naprawde pada;
nastepna sesja to pokaze i wtedy zalatamy druga przyczyne.
**Ryzyko / co sprawdzic:** licznik "BK relations uratowane" powinien przestac rosnac lawinowo
(pojedyncze zlapania OK); nowy log "relacje BK - pominieto notabla...". Relacje BK dla
pomijanych notabli nie beda sie aktualizowac - to bohaterowie z uszkodzonymi danymi, ktorych
BK i tak nie umial policzyc. Sprawdzic tez raport "pelny slad lawiny (jednorazowo)".
**Status:** WGRANE

## 2026-08-26 — straznik zawieszen: raport HANG osobnym kanalem (hang-*.log), nie przez blokade logu
**Mod:** CrashScribe | **Pliki:** `CrashScribe/src/Watchdog.cs`, `CrashScribe/src/Scribe.cs`
**Problem:** freez 26.08 15:02 - gra stala 20 min (132 watki w Wait), a straznik zawieszen
nie zapisal NIC: ostatnia linia sesji nalezala do Scribe, raportu GAME HANG brak.
**Przyczyna:** Watchdog.Dump pisal przez Trail.Drop i Scribe.Raw - oba biora wspolne blokady
(Trail.Gate, Scribe.Gate), ktorych uzywa tez watek glowny. Gdy zamrozony watek glowny trzymal
blokade, straznik wisial razem z nim i nigdy nie doszedl do zapisu.
**Zmiana:** Dump pisze do WLASNEGO pliku hang-RRRR-MM-DD_HH-mm-ss.log wprost (File.WriteAllText),
etapami: naglowek -> stos -> stan gry (najcenniejsze najpierw - gdy ktorys etap utknie,
poprzednie sa juz na dysku). Trail.Drop usuniety ze sciezki dumpa. Do glownego logu tylko
PROBA dopisania nowym Scribe.TryRaw (Monitor.TryEnter z limitem 2 s - nigdy nie wisi).
Prune sprzata tez stare hang-*.log (limit jak sesje).
**Ryzyko / co sprawdzic:** przy nastepnym freezie w Documents\...\CrashScribe powinien
pojawic sie hang-*.log ze stosem watku glownego. Watchdog dalej uzywa Suspend/Resume na
watku glownym - bez zmian wzgledem dotychczasowego zachowania.
**Status:** WGRANE

## 2026-08-26 — lista zbroi w CRAFT: zamkniete wzory na czerwono zamiast dopisku "- LOCKED"
**Mody:** ForgeView, Armoury | **Pliki:** `ForgeView/src/ArmourListColour.cs` (NOWY), `Armoury/src/BkArmourList.cs`, `docs/PLAN-kuznia-1do1.md`
**Problem:** zamkniete wzory na liscie zbroi mialy tekstowy dopisek "- LOCKED" - malo czytelne,
Jeff chcial kolor jak przy zablokowanych czesciach broni. Zadanie czekalo "na Windows",
bo prefab BK jest w plikach modulu, nie w DLL.
**Przyczyna:** wiersz nazwy renderuje prefab BannerKings ArmorCraftingCategory.xml - znaleziony
w ZRODLACH BK na githubie (R-Vaccari/bannerlord-banner-kings): RichTextWidget z Text="@ItemName"
w ItemTemplate listy SmeltableItemList. Semantyka Replace i moment wolania gettera Nodes
potwierdzone w zrodlach UIExtenderEx (PrefabComponent: XPath przez SelectSingleNode, przy chybieniu
blad na ekranie i prefab NIETKNIETY; getter Nodes biegnie dopiero PO trafieniu XPath).
**Zmiana:** (1) ForgeView.ArmourListColour - PrefabExtension na prefab "ArmorCraftingCategory",
XPath descendant::*[@Text='@ItemName'] (celuje w wiazanie, nie strukture - odporniejsze na roznice
w BK.Redux), InsertType Replace: wezel nazwy podmieniony na dwa warianty przelaczane IsVisible -
normalny @FvKnown i czerwony #D65252FF @FvLocked (mixin ArmourItemMixin juz te pola ma). ZADNEGO
SetAttribute - ten wieszal ekran (lekcja z MaterialRowExtension). (2) BkArmourList.NamePostfix:
dopisek "- LOCKED" schodzi TYLKO gdy flaga ArmourListColour.Applied (przez reflection) mowi,
ze latka realnie weszla; inaczej dopisek zostaje jako zapas.
**Ryzyko / co sprawdzic:** w CRAFT wiersze nieodkrytych wzorow czerwone, znanych normalne, bez
dopisku "- LOCKED"; przy wejsciu w zakladke log ForgeView "ArmourListColour: wiersze listy zbroi
dostaly warianty known/locked". Jesli Redux ma inny prefab: czerwony komunikat UIExtenderEx
"Failed to apply extension to ArmorCraftingCategory" i stary dopisek dalej widoczny (nic nie znika).
Kolor odswieza sie przy przebudowie listy - wzor odkryty w trakcie sesji moze zostac czerwony
do ponownego wejscia w zakladke (dopisek zachowywal sie tak samo). NIEZBUDOWANE - pisane
w kontenerze bez bibliotek gry; przed wgraniem `./build.sh` na maszynie z libs.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kryjowka: realny okrzyk alarmu (audio) + halas 0,2 m/kg
**Mod:** Armoury | **Pliki:** `src/HideoutAlarm.cs`, `src/Settings.cs`
**Zmiany:** (1) sztafeta alarmu jest SLYSZALNA: jeden czlowiek na kazdy krag fali wydaje
prawdziwy okrzyk bojowy gry (Agent.MakeVoice, VoiceType.Yell) - lancuch pojedynczych
wrzaskow niesie sie przez oboz, celowo nie chor wszystkich naraz (lekcja z BrokenMen);
wylacznik Hideout Alarm Voice (ON). (2) Hideout Noise Per Armor Kg z 0,4 na 0,2 m/kg
(decyzja Jeffa) - skora ~21,6 m noca, plyta ~26 m.
**Ryzyko / co sprawdzic:** przy alarmie slychac okrzyki kolejnych ludzi; zadnego chóru.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kryjowka: sztafeta krzyku, zaniepokojenie zamiast alarmu, halas wg wagi pancerza
**Mod:** Armoury | **Pliki:** `src/HideoutAlarm.cs`, `src/Settings.cs`
**Zmiany (trzy zamowienia Jeffa):**
1. SZTAFETA KRZYKU: obudzony zbojca TEZ krzyczy - alarm skacze od czlowieka do czlowieka
   (kazdy kolejny musi stac w zasiegu krzyku poprzedniego), wiec niesie sie przez oboz
   lancuchem, ale nigdy nie przeskakuje pustki wiekszej niz jeden krzyk. BFS falami,
   bezpiecznik 64 kregi. Przy cichej likwidacji swiadek przy ciele (12 m) krzyczy juz
   PELNYM glosem i sam uruchamia sztafete. Wylacznik Hideout Alarm Relay (ON).
2. ZANIEPOKOJENIE: kroki biegnacego NIE stawiaja juz obozu na nogi - patrolujacy w zasiegu
   sluchu przechodzi w stan Cautious (bron w dloni, rozglada sie, prowadzi go silnik);
   pelny alarm robi dopiero walka albo to, co zobaczy natywnym wzrokiem.
3. HALAS WG PANCERZA: do promienia slyszalnosci biegu dochodzi waga noszonego pancerza
   x Hideout Noise Per Armor Kg (0,4 m/kg; GetTotalWeightOfArmor jak w FieldCraft) -
   zwiadowca w skorze biega niemal cicho, rycerz w plycie dudni na pol obozu.
**Ryzyko / co sprawdzic:** bieg przy obozie -> zbojcy TYLKO czujni (szukaja, nie szarzuja);
strzal niecelny -> lancuch alarmu poprzez stojacych blisko siebie (log "N zbojcow w M
kregach"); cichy strzal bez swiadkow -> dalej cisza; ciezki pancerz -> budzisz z daleka.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — mijane obozy z namiotem od reki, natury band, sluch w kryjowce, kolor wzoru w CRAFT
**Mody:** Armoury, ForgeView | **Pliki:** `Armoury/src/NightRest.cs`, `Armoury/src/HideoutAlarm.cs`, `Armoury/src/Settings.cs`, `ForgeView/src/ArmourItemMixin.cs`, `ForgeView/src/TableauExtension.cs`
**Zmiany:**
1. MIJANE OBOZY (Jeff: "stoi konik, ma byc namiot"): przydzial namiotow szedl raz na
   godzine GRY - podjezdzajac do spiacego widziales figurke. Teraz lista spiacych (_camping)
   + RefreshNearbyTents co ~2 s REALNE z OnTick: namioty wchodza, gdy podjezdzasz, schodza
   gdy odjezdzasz albo partia sie budzi (Ai.IsDisabled jako sygnal snu). Limit AiTentCap
   bez zmian, wizerunki ruszane tylko przy zmianie stanu.
2. NATURY BAND (Jeff: "niektorzy poluja w dzien, inni w nocy"): stala natura z Id bandy -
   3/4 to nocni lowcy (leza 10-16, chodza noca), 1/4 dzienni (poluja w slonce, leza 23-5);
   o swicie i wieczorem wszyscy na nogach. Pogon/ucieczka/wrogi lord ucina drzemke.
3. SLUCH W KRYJOWCE (Jeff: "chodzilo mi o hideout, nie mape"): wzrok AI w misji jest
   natywny, ale SLUCH dokladamy: biegnacy czlowiek (predkosc > 3,5 m/s) budzi zbojcow
   w promieniu Hideout Hear Night (20 m) noca / Hear Day (10 m) za dnia - chod na
   Left Ctrl (WalkKey) jest cichy, wiec skradanie dziala. Tick co 1 s.
4. KOLOR WZORU W CRAFT (Jeff: "locked malo czytelne, ma byc jak przy broniach"):
   pod podgladem 3D w zakladce CRAFT stoi teraz czerwone "PATTERN NOT LEARNED" albo
   zielone "PATTERN KNOWN" (Brush.FontColor, wlasny widget ForgeView). Kolorowanie
   SAMYCH WIERSZY listy wymaga plikow prefab BK, ktore sa TYLKO na maszynie z gra -
   ZADANIE DLA CLAUDE NA WINDOWS: patrz docs/PLAN-kuznia-1do1.md.
**Ryzyko / co sprawdzic:** podjazd noca pod spiacego lorda - namiot w ~2 s; bandyci
w dzien rzadsi, noca gestsi; w kryjowce bieg budzi (log HideoutAlarm), chod nie;
zakladka CRAFT - kolorowy stan wzoru pod modelem 3D.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — paczka uwag Jeffa: wzrok dzien/noc, namioty przy graczu, bandyci noca, brama DTE w kryjowce, wspolne etykiety czasu
**Mod:** Armoury | **Pliki:** `src/SightRange.cs` (NOWY), `src/HideoutSpawnShim.cs` (NOWY), `src/NightRest.cs`, `src/Project.cs`, `src/ArmouryBehavior.cs`, `src/SmithMenu.cs`, `src/SubModuleMain.cs`, `src/Settings.cs`
**Zmiany:**
1. WZROK ZA SLONCEM: zasieg wykrywania partii za dnia x1,15, w nocy x0,65 (lagodnie,
   bo w nocy slychac wiecej); postfix na GetPartySpottingRange we wszystkich modelach.
2. WEDRUJACY NAMIOT naprawiony: straznik CO KLATKE zdejmuje wizerunek namiotu gracza,
   gdy partia sie ruszy (stary kod gasil tylko flage raz na godzine - namiot jechal po mapie).
3. NAMIOTY TYLKO WOKOL GRACZA: ikona namiotu AI tylko w promieniu Ai Tent Radius (35)
   od gracza; po odjezdzie obrazki schodza (partie dalej spia). Limit AiTentCap bez zmian.
4. NIE KAZDY OBOZUJE: Ai Camp Skip Percent (15%) kolumn maszeruje przez cala noc
   (deterministycznie per partia i noc); pogon/ucieczka/wrog w poblizu jak dotad nie spia.
5. BANDYCI NOCNI LOWCY (Bandits Rest By Day, ON): w dzien 10-16 wieksza czesc band lezy
   w ukryciu (AI stoi, bez namiotow), cwierc poluje mimo slonca; noca normalnie - razem
   z krotszym wzrokiem nocnym robi sie ich pora.
6. BRAMA DTE W KRYJOWCE (Hideout Armoury Gear, ON): patch spawnu DTE wymaga
   IMissionAgentSpawnLogic, ktorego zwykla kryjowka nie ma (zasadzke wyjatkowali) - stad
   wojsko szlo we wzorcowym rynsztunku zamiast magazynowego. HideoutSpawnShim implementuje
   interfejs pusto (DTE uzywa go TYLKO jako testu obecnosci, silnik w kryjowce nie wola) -
   przydzial z magazynu dziala jak w polu. Log "HideoutSpawnShim: brama DTE otwarta".
7. JEDNA ETYKIETA CZASU (Jeff: "raz 2,5 dnia, raz 7,5 godziny"): Project.TimeLabel -
   ponizej 2 dob godziny, wyzej dni+godziny; uzyta w tempie kucia, komunikacie wykonczenia
   broni, banerze i podsumowaniu projektow (z dopiskiem, ze zegar tyka tylko w tej osadzie).
**Ryzyko / co sprawdzic:** kryjowka - wojsko w sprzecie z magazynu (i log bramy); namiot
gracza znika w chwili ruszenia; w nocy krotszy zasieg wykrywania (tooltip predkosci
"Darkness"); bandyci rzadsi za dnia; obce namioty widac tylko blisko gracza.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — znikajacy miecz: dostawa broni BEZ drugiego rzutu + glosne banery
**Mod:** Armoury | **Pliki:** `src/Project.cs`, `src/ArmouryBehavior.cs`, `src/Forge.cs`
**Problem (Jeff):** wykul miecz w vanilla kuzni, gra napisala "dodano do ekwipunku",
po czym miecz zniknal i po czasie nie wrocil. Do tego wynik wielodniowej roboty byl
cicha linijka na czacie.
**Przyczyna:** OnNewItemCrafted zabiera wyrob na "wykonczenie" (tier x WeaponDaysPerTier dni)
i oddaje przez Forge.Finish - ktore rzuca DRUGI RAZ na porazke wedle NASZYCH wymogow skilla
(vanilla juz osadzila sukces i jakosc przy kowadle). Podwojny osad: przy pechu miecz "pekal
przy hartowaniu" mimo udanego kucia, a wylosowana przez vanilla jakosc i tak przepadala.
Projekt do tego stoi, gdy gracz wyjedzie z osady - wyglada na "minal czas i nic".
**Zmiana:** projekty maja rodzaj: "van" (bron z vanilla kucia) = DOSTAWA przez nowe
Forge.Deliver - bez rzutu, z TA SAMA jakoscia (modyfikator jedzie w zapisie projektu,
pola 5-6, stare zapisy kompatybilne). Zabranie wyrobu krzyczy banerem na srodku ekranu
(ile dni i w KTORYM miescie + "Stay or return to collect it"), dostawa i wyniki projektow
naszej kuzni (sukces/pekniecie) tez ida banerem.
**Ryzyko / co sprawdzic:** wykuty miecz ma wrocic po N dniach POBYTU w tym samym miescie,
z jakoscia z momentu kucia; baner przy zabraniu i przy dostawie. Stare projekty z zapisu
(sprzed tej zmiany) nie maja znacznika "van" i przejda jeszcze stara sciezka z rzutem.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kuznia 1:1 kroki 2-3: nauka za przetop, pancerze i luki w zakladce Smelt + przeglad sprzetu wojska
**Mod:** Armoury | **Pliki:** `src/SmeltTab.cs` (NOWY), `src/RangedLore.cs`, `src/Forge.cs`, `src/SmithMenu.cs`, `src/SubModuleMain.cs`
**Zmiana (krok 2):** przetop uczy wzorow (Study, pol stawki kucia); odblokowanie wzoru
pokazuje vanillowy baner (MBInformationManager.AddQuickInformation) obok zielonej linijki.
**Zmiana (krok 3):** zakladka Smelt ekranu kuzni przyjmuje metalowe pancerze/tarcze i
luki/kusze: postfix SmeltingVM.RefreshList (doklada pozycje tym samym SmeltingItemVM,
delegaty do prywatnych callbackow), postfix GetSmeltingOutputForItem we wszystkich modelach
SmithingModel (wyjscie z naszej receptury SmeltYield), prefix DoSmelting dla przedmiotow
bez WeaponDesign (oryginal walil w item.WeaponDesign.Template = NullReference; nasza
sciezka: materialy, XP, stamina, nauka wzorow). Stary tygiel w menu miasta ZOSTAJE.
**Zmiana (przeglad):** nowa opcja "Muster the men's kit" w The mending bench (Jeff: "gdzie
sprawdze srednia jakosc sprzetu wojska") - per typ: sztuki na stanie / potrzebne, braki,
sredni tier, sredni stan %, ile zuzytych; na dole rachunek naprawy calosci z rabatem.
**Ryzyko / co sprawdzic:** zakladka Smelt - pancerz na liscie, wyjscie materialow sie
zgadza, przetop nie crashuje (NullReference by byl bez prefixa); baner przy odblokowaniu;
przeglad kwatermistrza pokazuje sensowne srednie. UWAGA: BK/RBM moga podmieniac SmithingModel
- patch lapie wszystkie pochodne (log "wyjscie w N modelach").
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kuznia 1:1 krok 1: jakosc pancerzy ta sama formula co bron
**Mod:** Armoury | **Pliki:** `src/Forge.cs`, `src/Settings.cs`
**Problem:** jakosc naszych wyrobow (pancerze, luki) chodzila po wlasnej krzywej
(Shoddy*/Jackpot), innej niz vanilla kucie broni - Jeff kazal zrownac 1:1
(docs/PLAN-kuznia-1do1.md).
**Zmiana:** RollQuality to teraz wierna kopia DefaultSmithingModel.GetCraftedWeaponModifier:
ExplainedNumber(-trudnosc) + bonus Smithing (SkillHelper/DefaultSkillEffects, limit +-300),
sigmoidy k=0,018 (Poor 0,36/Inferior 0,45/Common/Fine 0,36/Masterwork 0,27/Legendary 0,18
przy progach -70/-55/25/40/70/115), normalizacja, perki Experienced/Master/Legendary Smith
przesuwaja szanse jak w oryginale (Legendary + (skill-275)/5 x 1%), sufit od tieru wyrobu
(1-3 max Fine, 4 max Masterwork, 5-6 bez limitu; vanillowy AdjustQualityRegardingDesignTier
liczony z tieru WYROBU zamiast sredniej czesci), na koncu GetModifiersBasedOnQuality na
grupie modyfikatorow przedmiotu. Tempo (nasze): z dbaloscia +20 do wyniku, w pospiechu ~-17.
Ustawienia ShoddyChanceAtZeroMargin, ShoddyMarginRange, JackpotChance,
LegendaryPerkJackpotBonus USUNIETE (stare XML-y je zignoruja).
**Ryzyko / co sprawdzic:** rozklad jakosci wykutych pancerzy ma odpowiadac broni przy tym
samym marginesie skilla; przy niskim skillu pojawiaja sie Battered/Rusty (odpowiedniki Poor/
Inferior), przy wysokim Fine/Masterwork/Legendary wedle tieru.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — alarm w kryjowce: walka budzi oboz, cichy strzal nie
**Mod:** Armoury | **Pliki:** `src/HideoutAlarm.cs` (NOWY), `src/SubModuleMain.cs`, `src/Settings.cs`
**Problem:** w kryjowce zbojcy 10 m od bijatyki udaja, ze nic sie nie dzieje - vanilla budzi
tylko zaczepiona grupke.
**Zmiana:** nowy MissionBehavior (tylko misje z HideoutMissionController): kazde wrogie
trafienie, ktore NIE zabija, budzi wrogow w promieniu Scream Radius (40 m) od ofiary
(ranny krzyczy); zabojstwo JEDNYM ciosem budzi tylko swiadkow w Witness Radius (12 m)
od ciala - bez swiadkow oboz spi (cicha likwidacja lukiem dziala). Ci 200 m dalej nie
slysza nic. Budzenie publicznym Agent.SetAlarmState(Alarmed) - ten sam stan co vanilla;
lancuch niesie sie sam (obudzeni dobiegaja, walka przy nich budzi nastepnych). 3 ustawienia
MCM, Hideout Alarm Enabled domyslnie ON.
**Ryzyko / co sprawdzic:** wejsc do kryjowki, strzelic w goscia przy grupce - grupka ma
ruszyc (log "HideoutAlarm: walka obudzila N"); zabic samotnego jedna strzala - cisza.
UWAGA: mod Blackmamba's Hideout Overhaul rusza te same mechanizmy - nie laczyc bez testu.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — ranni przestaja zapetlac krzyk (BrokenMen z odstepem prob)
**Mod:** Armoury | **Pliki:** `src/BrokenMen.cs`
**Problem:** zolnierz ponizej progu ucieczki stal w miejscu i krzyczal ZAPETLONYM, nakladajacym
sie na siebie glosem ("jakby ktos puszczal kilka nagran naraz nie czekajac konca") i czasem
wcale sie nie wycofywal.
**Przyczyna:** model morale (RBM) potrafi cofnac Retreat (StopRetreating zeruje flagi), a nasz
kod pchal czlowieka od nowa CO POL SEKUNDY - kazde pchniecie odpalalo wrzask od poczatku,
a czlowiek szarpany tam i nazad stal w miejscu.
**Zmiana:** proby z rosnacym odstepem: Panic, potem Retreat po 3 s, 6 s, 9 s; po trzech
nieudanych 20 s spokoju (albo odejdzie, albo morale go pozbieralo i walczy dalej). Zaden
krzyk nie nachodzi na poprzedni. Slowniki czyszczone w OnAgentDeleted.
**Ryzyko / co sprawdzic:** ranni schodza z pola plynnie, krzyk pojedynczy; przy RBM patrzec,
czy ktos nie zostaje w wiecznym tam-i-nazad (wtedy zwiekszyc odstepy).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — oboz i sen: namiot przy spaniu, rozliczenie od reki, bandyci za przelacznikiem, bitwa w obozie (eksperyment)
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`, `src/CampScene.cs` (NOWY), `src/SubModuleMain.cs`, `src/Settings.cs`
**Problem:** (1) ikona gracza nie zmieniala sie w namiot przy spaniu ("kiedys dzialalo") -
namiot stawal tylko przy wlasnym obozie z klawisza O, a jedna wywrotka Tent() gasila namioty
CALEMU swiatu (_tentBroken); (2) splata dlugu snu czekala do przeliczenia o 6:00, nawet gdy
wojsko dospalo w poludnie; (3) ustawienia Ai Bandits Camp Too i Ai Tent Cap byly MARTWE
(bandyci nigdy nie obozowali, limit ikon nieegzekwowany); (4) napasc na oboz = bitwa na
golym polu.
**Zmiana:** (1) wejscie w sen (arm_sleep_wait) stawia namiot, pobudka zwija (chyba ze stoi
wlasny oboz - wtedy zwija go Break camp); _tentBroken zastapiony licznikiem 3 potkniec
Z RZEDU (sukces zeruje) wedle zasady "licz potkniecia, nie gas funkcji". (2) dlug snu
schodzi NATYCHMIAST, gdy uzbieraja sie godziny (flaga _credited, swit nie liczy drugi raz;
zapis 3-polowy, stary 2-polowy wczytuje sie dalej). (3) bandyci obozuja TYLKO gdy Ai Bandits
Camp Too = ON (domyslnie OFF - ich hurtowe wlaczenie polozylo gre 25.08); limit ikon
Ai Tent Cap (40) egzekwowany, reszta spi bez obrazka. (4) NOWE Camp Battle Props (EKSPERYMENT,
domyslnie OFF): napadnieci W OBOZIE dostaja scenografie obozu na polu bitwy (3 namioty
polkolem za graczem, ognisko, 4 pochodnie, plot) mechanizmem z open-source Homesteads
(GameEntity.Instantiate); nazwy prefabow to kandydaci - brakujace pomijane z logiem
"CampScene: brak prefabu X". Do tego NightRest.PlayerCamped sledzi stan obozu gracza
(takze obozu BK; ruch zeruje).
**Ryzyko / co sprawdzic:** namiot pojawia sie przy Bed down i znika po pobudce; komunikat
splaty dlugu przychodzi w chwili dospania; po wlaczeniu Camp Battle Props w MCM dac sie
napasc w obozie i przyslac linie "CampScene:" z logu (czyscimy liste prefabow po tescie);
z wlaczonymi bandytami patrzec na wydajnosc nocy i crashe menu.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — goli zolnierze w kryjowce: DressCode dopelnia KAZDY pusty slot
**Mod:** Armoury | **Pliki:** `src/DressCode.cs`
**Problem:** zolnierze gracza stoja nadzy podczas walki w kryjowce rozbojnikow (w polu OK).
**Przyczyna (ze ZRODEL DTE, github tategotoazarasi/Bannerlord.DynamicTroop):** (1) DTE
druzynie gracza CELOWO nie dopelnia pustych slotow (kara underequipped) - zolnierz dostaje
tylko to, co magazyn ma; (2) awaryjne ubranie DTE, ktore ratuje w polu, jest w kryjowkach
WYLACZONE; (3) nasz stary DressCode odpuszczal, gdy byla CHOC JEDNA czesc pancerza (same
buty = "ubrany"), i modyfikowal wspoldzielony Equipment przydzialu DTE, co moglo falszowac
zdejmowanie sztuk z polek magazynu w postfixie DTE.
**Zmiana:** DressCode dopelnia kazdy pusty slot pancerza Z OSOBNA (glowa/korpus/nogi/rece/
peleryna) ubraniem ze wzorca oddzialu, na KLONIE ekwipunku - przydzial DTE nietkniety,
rozliczenie magazynu bez zmian. Prefix dalej Priority.Last (po prefixie DTE).
**Ryzyko / co sprawdzic:** kryjowka - nikt nagi; w logu linie "DressCode: ... pustych slotow
pancerza"; po bitwie polowej stan magazynu DTE bez dziwnych ubytkow. Kara morale
underequipped DTE dalej dziala (to tylko ubranie, nie pancerz z magazynu).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — liczniki surowcow i staminy w ekranie kuzni odswiezaja sie na zywo
**Mod:** Armoury | **Pliki:** `src/FletchForge.cs`
**Problem:** po wykuciu (np. luku) w zakladce CRAFT panel dolny dalej pokazywal stara ilosc
drewna/zelaza/wegla i stara stamine - trzeba bylo sie przeklikac miedzy zakladkami.
**Przyczyna:** vanilla przelicza panel (CraftingVM.UpdateAll: materialy, stamina, skille,
dostepnosc przycisku) tylko po WLASNYCH akcjach; nasze Forge.Smith zdejmuje materialy wprost
z sakw, wiec ekran nie wiedzial. Stary refresh w prefixie robil tylko stamine bohatera i
OnRefresh mixina BK - za malo.
**Zmiana:** RefreshCraftScreen wolany z postfixa ExecuteMainActionBK (postfix biegnie TAKZE
gdy prefix przejal robote strzelecka, i przy pancerzach BK): CraftingVM.UpdateAll przez
reflection + OnRefresh mixina. Podwojny refresh z prefixa usuniety.
**Ryzyko / co sprawdzic:** po wykuciu luku licznik drewna/zelaza i stamina maja zejsc OD RAZU;
to samo po pancerzu z zakladki BK. UpdateAll jest prywatne - jesli nazwa sie kiedys zmieni,
Traverse zwroci null i zostanie stary objaw (bez crasha).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — tansza naprawa wojska: wrak max 10% wartosci + rabat hurtowy
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/Settings.cs`
**Problem:** Jeff po obejrzeniu przykladow: wrak (1%) placony ~12,5% wartosci to za drogo,
i ma byc "jeszcze taniej jesli hurtowo naprawiamy".
**Przyczyna:** stara stawka liczona jako polowa ceny naprawy wlasnej sztuki.
**Zmiana:** cena sztuki wojska liczy sie teraz WPROST od wartosci: wrak (1%) = Troop Mend
Wreck Share (10%) wartosci, lzejsze zuzycie liniowo mniej (stan 60% = 4% wartosci). Do tego
rabat hurtowy z calej roboty: kazda sztuka -0,5% z rachunku (Troop Mend Bulk Discount PP),
pulap 30% (Max). Rabat w podpowiedzi i przy placeniu liczony z tej samej liczby sztuk.
Ustawienie Troop Mend Cost Factor USUNIETE (zastapione przez Wreck Share); stare XML-e je
zignoruja.
**Ryzyko / co sprawdzic:** kwoty w podpowiedzi (np. kolczuga 2000 przy 3%: bylo 242, ma byc
~194 minus rabat); przy pelnych polkach rabat do 30%.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — porzadek w menu kuzni: naprawy pod jednym wejsciem
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`
**Problem:** menu Work the forge uroslo do 12 pozycji, z czego 5 to rozne odmiany naprawy -
nie bylo widac, ktora do czego (Jeff: "sprawdz czy opcje maja sens i czy czegos nie wywalic").
**Przyczyna:** kazda nowa funkcja dokladala wlasny wiersz do glownego menu.
**Zmiana:** nowe podmenu "The mending bench - your gear and the men's" (armoury_mend), a w nim:
wybor sztuki, naprawa calego rynsztunku wlasnorecznie / u kowala, lupy z sakw hurtem, sprzet
wojska. Podpowiedz wejscia od razu liczy: ile zuzytych na grzbiecie / w sakwach / na polkach
wojska. Glowne menu ma teraz 8 pozycji. Nic nie wylecialo - wszystkie funkcje zostaly,
zmienilo sie tylko ulozenie. Po skonczonej robocie wracasz do glownego menu kuzni.
**Ryzyko / co sprawdzic:** wejscie i wyjscie z podmenu (SwitchToMenu miedzy zwyklymi menu,
NIE z menu oczekiwania - pulapka CTD nie dotyczy); wszystkie 5 opcji dziala jak przedtem.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — zamawianie brakujacego sprzetu wojska u kowala
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/QuartermasterLaw.cs`, `src/Settings.cs`
**Problem:** kwatermistrz melduje "brakuje Thrown 5/24", ale nie bylo JAK dokupic brakow -
trzeba bylo biegac po kramach i zgadywac.
**Przyczyna:** brak takiej funkcji.
**Zmiana:** opcja "Order kit for the men - the smith procures it": lista typow ze stanem
polki/potrzeba (braki podswietlone), wybor tieru 1-6 z najtansza kupna sztuka i cena od
sztuki (wartosc rynkowa x Troop Order Markup, domyslnie 1,15), ilosc 1/5/10/uzupelnij brak;
dostawa wprost na polki zbrojowni DTE po 1 h + 0,2 h na sztuke (max 24 h). Konie celowo
wylaczone (od tego stajnie). 2 nowe ustawienia MCM.
**Ryzyko / co sprawdzic:** ceny przy tierach maja wygladac rynkowo; po odbiorze zloto schodzi
o wypisana kwote, a sztuki laduja w zbrojowni (DTE sam ubiera ludzi).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — naprawa sprzetu WOJSKA u kowala (zbrojownia DTE)
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/QuartermasterLaw.cs`, `src/Settings.cs`
**Problem:** zbrojownia DTE trzyma modyfikatory stanu, a ConditionScaling skaluje pancerz
KAZDEGO noszacego - zolnierz w kirysie 3% ma realnie ~11% ochrony (krzywa: 1% ~ -89%).
Nie bylo ZADNEJ drogi naprawy polek wojska ani informacji, ze taka potrzeba istnieje.
**Przyczyna:** opcje kowala obejmowaly tylko sakwy i grzbiet gracza.
**Zmiana:** nowa opcja "Send the men's worn gear to the smith" w Work the forge: skan zbrojowni
DTE, stawka HURTOWA (Troop Mend Cost Factor, domyslnie 50% ceny naprawy wlasnej sztuki),
najtansze najpierw, pelny koszt wypisany Z GORY w podpowiedzi, robota max Troop Mend Max Hours
(24 h). Kwatermistrz przy otwarciu zbrojowni melduje na zolto ile sztuk jest zuzytych i ze
kowal je naprawi. 3 nowe ustawienia MCM (grupa The men's gear).
**Ryzyko / co sprawdzic:** tooltip z kwotami na zdrowym rozsadku; po naprawie polki maja czyste
przedmioty (bez stanu) w tej samej liczbie; zloto schodzi dokladnie o wypisana kwote.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — luczarnia: pamiec ostatniej polki + stamina i ryzyko jak przy broni
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/Recipes.cs`, `src/Forge.cs`, `src/Settings.cs`, `tools/gen_mcm.py`
**Problem:** (1) wejscie w luki wymaga za kazdym razem klikania typ->tier od nowa;
(2) luk kosztowal tier x 25 x 0,8 staminy (t5 = 100 - DWA luki i koniec sesji), a ryzyko
zepsucia bylo takie samo jak przy hartowaniu plachy ("zawsze 50%, bez sensu" - Jeff);
kuznia broni pozwala kuc duzo wiecej.
**Przyczyna:** mnoznik 0.8 zaszyty w BuildRecipe; FailureChance nie odroznia luczarni od platnerki;
brak pamieci nawigacji.
**Zmiana:** (1) menu luczarni pamieta ostatni typ+tier i pokazuje na gorze "Back to the bench
you left"; po kuciu natychmiastowym (zakladka CRAFT) lista zostaje otwarta na tej samej polce.
(2) nowe ustawienia: Ranged Stamina Factor (0,35 - t5 luk = 43 staminy, 4-5 lukow na sesje)
i Ranged Failure Factor (0,5 - ryzyko lukow/kusz/amunicji o polowe nizsze). Flaga Ranged w
recepturze. PRZY OKAZJI: gen_mcm.py mial zaszyte sciezki /home/claude ze starego komputera -
teraz liczy sciezki wzgledem repo.
**Ryzyko / co sprawdzic:** procent ryzyka na polce lukow (ma byc ~polowa dawnego), ile lukow
schodzi na jednej stamince, czy "Back to the bench" wraca na wlasciwa polke.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — miniatury 3D w listach rozbiorki, naprawy i tygla
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`
**Problem:** w listach "Take a piece apart", "Pick a damaged piece to mend" i "Break metal down"
po samym opisie nie widac, co to za przedmiot - lista kucia miala obrazek, te trzy nie.
**Przyczyna:** InquiryElement dostawal `null` zamiast ItemImageIdentifier.
**Zmiana:** wspolny pomocnik `ItemPic(item)` (ItemImageIdentifier w try/catch), uzyty we
wszystkich czterech listach (kucie tez przepiete na niego).
**Ryzyko / co sprawdzic:** czy miniatury sie renderuja i nic nie wywala przy otwieraniu list.
**Status:** DO SPRAWDZENIA

## 2026-08-25 — wyjscie ze snu przez ExitToLast (naprawa CTD)
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`
**Problem:** klikniecie **"Rouse the men early"** w menu snu wywalalo gre. Log:
`NullReferenceException | BLAME: SandBox | GameMenuVM.OnFrameTick()`, `Menu: camp`.
**Przyczyna:** obie drogi wyjscia ze snu wolaly `GameMenu.SwitchToMenu(_sleepReturn)`
z wnetrza opcji menu oczekiwania — VM menu jest wtedy w polowie klatki.
Sasiedni, dzialajacy przycisk "Break camp" od zawsze uzywal `ExitToLast()`.
**Zmiana:** nowa metoda `LeaveSleep()` wolajaca `GameMenu.ExitToLast()`; uzyta w opcji
"Rouse the men early" i przy naturalnym koncu snu.
**Ryzyko / co sprawdzic:** wyjscie ze snu wraca teraz o poziom wyzej (zwykle na mape),
a nie do menu obozu. Sprawdzic, czy nie ma crasha.
**Status:** WGRANE

## 2026-08-25 — cofniecie calego dziennego bloku obozowania
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`, `src/Settings.cs`
**Problem:** crash w `GameMenuVM.OnFrameTick` w menu obozu. Przeszukanie 23 logow z 5 dni:
sygnatura wystepuje w **2 sesjach po zmianie**, w **21 wczesniejszych zero**.
**Przyczyna:** naprawa `_tentBroken` (globalnego wylacznika namiotow) **plus** wlaczenie
obozowania bandom naraz. Do tej pory tent-system praktycznie sie wylaczal po pierwszym
wyjatku; po naprawie zaczal obslugiwac setki partii i grzebac w ich wizerunkach na mapie
przy kazdym nocnym ticku.
**Zmiana:** cofniete do stanu porannego: obozuja tylko lordowie i karawany, bez dlugu snu
w pogoni, bez limitu namiotow, ze starym `_tentBroken`.
**Ryzyko / co sprawdzic:** **wraca stary blad** — namiot gracza moze zniknac po pierwszej
wywrotce. Do zrobienia na nowo, ostroznie, po jednej rzeczy.
**Status:** COFNIETE

## 2026-08-25 — kultura duchownych i ochotnicy miejscowi
**Mod:** CrashScribe | **Pliki:** `src/Mends.cs`, `src/WarReport.cs`, `src/Config.cs`
**Problem:** w polnocnych wsiach (White Ranch, Last River, Snowwood) mozna bylo zobaczyc
**wolny lud** i **Westerlands Noble Youth** wsrod ochotnikow.
**Przyczyna:** BK tworzy duchownego z presetu wiary (`HeroCreator.CreateSpecialHero`),
wiec kaplan ma kulture presetu, nie osady — a ochotnikow dobiera sie wedle kultury notabla.
Nasza wczesniejsza latka na `GetPossibleSpawns` leczyla objaw; lista i tak sie odnawiala.
**Zmiana:** `Mends.LocalLevies(Settlement)` prostuje kulture notabla do kultury osady
i zeruje obce sloty ochotnikow — przy wejsciu do osady i raz dziennie.
**Ryzyko / co sprawdzic:** w logu `Mends: <imie> w <osada> byl X, jest Y (Preacher)`.
W sesji 25.08: **558 poprawionych notabli**, spisy osad czyste.
**Status:** WGRANE

## 2026-08-25 — odblokowanie BannerKings (dwie latki na BKROTPatch)
**Mod:** CrashScribe | **Pliki:** `src/Mends.cs`
**Problem:** **pusty ekran rekrutacji** ("Recruit All (0)"), 11 487 wyjatkow na sesje.
**Przyczyna:** patrz `docs/ERRORS.md` A1 i A2 — BKROTPatch dusil generowanie duchownych
(`Invoke(null)` na metodzie instancyjnej) i przerywal inicjalizacje stylow zycia.
**Zmiana:** `Mends.Unhook()` zdejmuje te dwa prefixy z metod BannerKings.
**Ryzyko / co sprawdzic:** w logu `Mends: BKROTPatch nie dusi juz duchownych BK...`.
Ekran rekrutacji ma byc pelny.
**Status:** WGRANE

## 2026-08-25 — reszta prac tego dnia (skrot)
**Mody:** Armoury, RealisticCaptivity, CrashScribe
- **Ksiega wzorow** — po kazdym wykuciu widac ile wzorow znasz i ile do nastepnego.
- **"Take a piece apart to copy its pattern"** — rozbiorka zdobytej sztuki omija kolejke nauki.
- **Kolejka pancerza rozbita na 7 galezi** (helmy/kirysy/nogawice/rekawice/plaszcze/tarcze/kropierze).
- **Ucieczka konno ze spotkania** (`HorseFlight.cs`) — caly oddzial w siodle moze uciekac,
  straz tylna tylko przeciw jezdzie wroga.
- **Lamanie sie rannych** (`BrokenMen.cs`) — ponizej 20% HP AI panikuje i schodzi z pola.
- **Stamina kowalska** regeneruje sie wszedzie poza kuznia (przedtem tylko w 4 konkretnych menu).
- **Rozrusznik fabuly ROT** + dziennik kolejki (`Fabula.cs`).
- **Spis notabli** przy wejsciu do osady (`WarReport.cs`).
**Status:** WGRANE

