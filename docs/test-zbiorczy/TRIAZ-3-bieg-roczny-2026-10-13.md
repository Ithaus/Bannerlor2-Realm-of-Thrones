# TRIAZ 3 - bieg roczny 364 doby (WGRANIE 34/35; 13.10.2026, wf_b57efd4d-9ac): wojsko lordow i zelazo


## Obszar: wojsko

**Streszczenie:** Wojsko lordow topnieje, bo panom konczy sie zloto na zold, a nie dlatego, ze brakuje ludzi: ludzi na swiecie ubylo przez rok tylko 0.27%, u notabli stoi kilkanascie tysiecy chetnych, a zwolnieni zolnierze wracaja do wsi, do karczm albo ida w bandy. Budzet rodu odprawia dzis tylu ludzi, ilu ginie w bitwach (po ok. 410 dziennie). Sa cztery przyczyny: po roku wojny zolnierz jest weteranem i kosztuje o jedna trzecia wiecej, wiec za te same pieniadze jest mniej glow (wydatki na zold spadly tylko o 14%, liczba ludzi o jedna trzecia); wojna w tym swiecie nigdy sie nie konczy, wiec oszczednosci rodow wypalily sie raz, w pierwszych miesiacach; biedny pan odprawia najpierw najtanszych, czyli najwiecej ludzi naraz; i jest pulapka bez wyjscia - pan z mala druzyna nie jest wzywany do armii, wiec korona odbiera mu rente za brak sluzby i oddaje ja bogatym, a on juz nigdy nie odbuduje wojska (57 rodow, w Polnocy polowa lordow nie ma ani jednego zolnierza, choc Starkowie i Boltonowie siedza na ponad milionie). Bez zmian swiat zejdzie z dzisiejszych 62 tys. do ok. 50 tys. ludzi w druzynach lordow w ciagu pol roku do roku gry i nie zatrzyma sie wczesniej; zalogi twierdz zostaja na ok. 74 tys. W starym biegu wojsko trzymalo 120 tys. tylko dlatego, ze gra dosypywala wtedy ok. 0.6 mln zlota dziennie z niczego - po zamknieciu ekonomii tego poziomu nie da sie uczciwie utrzymac. Proponuje dwie male poprawki (pan, ktorego nie stac na wiecej, nie traci renty; zwolnienia po rowno z kazdego szczebla) i jedna srednia (kompanie najemne zebraly 18 mln zlota, ktorego nie maja jak wydac), co powinno dac ok. 72-82 tys. zamiast 50 tys. Reszta zlota stoi w kiesach mieszczan (+29 mln w rok) - to sprawa obszaru gospodarki, nie wojska.


### O1-2 PULAPKA BEZ WYJSCIA: pulap zoldu -> druzyna ponizej 60% -> gra nie wzywa do armii -> brak sluzby -> renta korony przepada innym -> druzyna 0

**Werdykt:** BLAD | **pewnosc:** wysoka | **podwazenie - broni sie:** False

**Przyczyna:**

Lancuch czterech regul, z ktorych kazda z osobna dziala jak zamierzono, a razem tworza stan bez powrotu. (1) Pulap 166 trzyma biedny rod na 27% limitu druzyny (rody na pulapie d364: 17 339 ludzi przy limicie 64 607). (2) Gra wzywa do armii i pozwala ja zebrac tylko druzynie z PartySizeRatio > 0.6: C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\dec32\cs_full\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManagementCalculationModel.cs:33 (AIMobilePartySizeRatioToCallToArmy => 0.6f), :124, :132; BK (BKArmyManagementModel.cs:116-150) wola base i dokłada tylko druzyny wlasnego rodu wodza. (3) Renta 180 wymaga 20 dob sluzby z 60 dob wojny ze stykiem (SRC\CrownRents.cs:356-411 pomiar, :465-478 warunek), a sluzba to glownie armia (d337-364: sluzy 107 z 294 rodow na dobe, w tym armia 52). (4) E5/F9 oddaje udzial rodu bez warunku rodom sluzacym (CrownRents.cs:484). Bez renty D rodu spada do samej ziemi (zamek 40-460 zl/d), a zold zalogi (placonej pierwszej) jest wyzszy niz caly pulap - na druzyne zostaje 0. DOWOD (csv; skrypty ratio.py, split.py, split2.py, trace.py): rody z lennem z wypelnieniem > 60%: 115 (d84) -> 95 -> 80 -> 73 -> 76 -> 70 -> 64 -> 59 -> 56 -> 57 (d364); rody z lennem bez warunku renty (linia 'Renty korony (180)' :128504): 11 (5%, d28) -> 29 (d84) -> 25 (d196) -> 46 (d252) -> 57 (28.8%, d364; sluzba 47, zaloga 3, oba 7), w tym 20 rodow 'sluzba 0/20'. Tych 57 rodow tuz przed utrata renty: wypelnienie mediana 0.27, ponizej progu 0.6 bylo 47 z 57, na pulapie zoldu 39 z 57. Ich wojsko: 24 264 (d57-84) -> 11 233 tuz przed utrata renty -> 5 785 (d337-364); zold partii 115.1 -> 76.2 -> 43.0 tys./d; utracona renta 73.9 tys./d. Udzialy oddane sluzacym rosna: 16-29 tys./d w pierwszym polroczu -> 62.6 tys./d w d337-364 (31% rent); ok. 70% z tego trafia do rodow z luzem, ktore tego nie wydaja (ich G rosnie). Przyklad Tallhart (Polnoc, zamek): 508 ludzi (d98) -> 137 (d224, pulap 1 891, ziemia 356 + renta 957 + zwrot 944) -> utrata renty d252 -> 0 ludzi od d294, zaloga 600 zl/d przy D 188, kiesa 42 -> 17 tys. (za ok. 29 dob nie oplaci zalogi). Polnoc: Stark, Bolton, Dustin, Ryswell biora 60.4 z 85.5 tys. D krolestwa i stoja na limicie druzyn z kiesami 1.26 / 1.53 / 0.72 mln; Manderly 899 -> 25, Glover 775 -> 1, Cerwyn, Hornwood, Stout, Forrester, Locke, Karstark 0 ludzi; cala Polnoc 11 254 -> 4 855 przy rosnacym zlocie krolestwa (4.51 -> 5.04 mln). Poprawka P2/C4 (wymog dopiero od 20 dob styku) jest w tym biegu czynna i nie wystarcza - odsuwa pulapke tylko na poczatku wojny.

**Poprawka:**

Wielkosc S. SLUZBA NA MIARE SRODKOW: rod, ktorego gra nie moze wezwac do armii i ktorego nie stac na wiecej ludzi, sluzy tym, co ma. (1) SRC\ClanBudget.cs - nowa metoda internal static bool AtMeans(Clan c): rod ma wpis budzetu z ostatniej doby (nie Straz, nie najemnik), zold partii + zalog + karawan >= 0.95 x pulap ORAZ zadna czynna druzyna rodu nie ma PartySizeRatio > ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy (albo rod nie ma druzyny). (2) SRC\CrownRents.cs po linii 382: gdy how == 0 i klucz czynny i ClanBudget.AtMeans(c) -> how = 8 (doba liczona jak sluzba, osobny licznik w linii 'sluzba dzis: ... na miare srodkow N'); w bloku warunku (:471-478) dla takiego rodu Need = 0 w tej dobie. Gdy rod odbuduje druzyne ponad 60% albo zejdzie pod pulap - wraca zwykly wymog 20 z 60, a maska ma juz doby zaliczone, wiec renta nie znika w dniu przekroczenia progu. (3) SRC\Settings.cs obok :1105 nowy klucz bool CrownRentServiceAtMeans = true + python tools/gen_mcm.py (klucza nie ma w Armoury.json Jeffa - zadziala domyslna). Gracz bez zmian (nie ma budzetu 166, wiec AtMeans = false). Kolejnosc doby: CrownRents.Daily (ArmouryBehavior.cs:1647) biegnie przed ClanBudget.Daily (:1670) - czyta budzet z wczoraj, bez kolizji. Zloto: renta zmienia tylko odbiorce w tym samym krolestwie (skarbiec -> glowa jak dotad), zapis arm_rent180 bez zmian, czas doby ok. 0. RACHUNEK: do 57 rodow wraca ok. 62.6 tys. zl/d; z tego ok. 43.8 tys. dzis lezy u rodow z luzem (niewydawane), 18.8 tys. traca rody na pulapie. Zold partii grupy: +0.6..0.8 x 62.6 tys. z dodatnim sprzezeniem zwrotu zoldu (59% zoldu wraca) minus zalogi ponad pulap 10.6 tys. = ok. +35..75 tys./d, minus ok. 15..28 tys./d u rodow, ktore traca doplate = netto ok. +3 do +7 tys. ludzi i koniec przyrostu rodow bez wojska. RYZYKO: rod moze wisiec przy progu 60% i sluzyc co drugi miesiac (ograniczone, nie rosnie); przez pierwsze doby rente dostaja takze rody bez druzyny - to cel. Sprawdzic w biegu: 'warunek_180' w csv - rody 'sluzba 0/20' = 0; rody z lennem bez renty <= 15%; rody z lennem z 0 ludzi w druzynach nie rosna (dzis 8 -> 29); Polnoc >= 8 tys. Nie robic: nie wylaczac F9 (wraca staly przeciek 58 tys./d w skarbcach) i nie znosic warunku sluzby (regula Jeffa K10).


**Podwazenie - uwagi:**

WNIOSEK BRONI SIE TYLKO CZESCIOWO. Zostaje: werdykt BLAD, istnienie stanu, z ktorego rod sam nie wychodzi (brak renty -> zold zalogi >= caly pulap -> druzyna 0), liczby rodow i ludzi oraz kierunek poprawki. Nie broni sie: ogniwo "prog 60% -> gra nie wzywa -> brak sluzby" jako przyczyna, okreslenie "bez wyjscia", waga zjawiska i poprawka w podanym ksztalcie (nie obejmuje 100%).
Skrypty i wyniki: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\triaz3\podw-o1-2\ (load.py, a1-a11.py, b1-b3.py). Dane: D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Logs\2026-10-13_02-15-21\budzet-rodow.csv i bitwy.log, log Armoury-2026-10-13_02-15-21.log.

POTWIERDZONE
- Kod: DefaultArmyManagementCalculationModel.cs:33, :124, :132 (prog 0.6), BK MobilePartyExtensions.cs:17 (IsAvailableForArmies, 0.6 na sztywno), CallBannersGoal.cs:211; MobileParty.cs:1177 (PartySizeRatio = ludzie / Party.PartySizeLimit - limit zoldu go nie zmienia); CrownRents.cs:356-411, :465-478, :484. Armoury nie rusza wzywania do armii (grep).
- Liczby: bez renty 11 (d28) / 29 (d84) / 25 (d196) / 46 (d252) / 57 (d364); 57 rodow: 24 264 ludzi (d57-84) -> 11 233 (7 dob przed utrata) -> 5 785 (d337-364); F9 62 558 zl/d w d337-364 (31% rent); rody na pulapie d364: 17 339 ludzi. Rody z lennem z 0 ludzi: 8 (d84) -> 15 (d196) -> 29 (d364); z zoldem zalog >= caly pulap: 10 -> 16 -> 28, z tego 24 bez renty.

OBALONE / SPROSTOWANE
1. PROG 60% NIE JEST BRAMKA SLUZBY. Bez Polnocy, doby 300 i 364 razem, srednie wypelnienie druzyn z 60 dob -> rody z warunkiem sluzby (srednia dob sluzby z 60): 0 ludzi 0 z 10; 0-15% 20 z 56 (17.1); 15-30% 24 z 35 (27.7); 30-45% 44 z 50 (39.1); 45-60% 57 z 60 (42.0); 60-80% 69 z 71 (46.7); 80%+ 23 z 24. Rody z 30-60% sluza prawie tak samo jak pelne. Zalamanie jest dopiero ponizej ok. 15% i przy 0 ludzi. Przyklady z The Reach: Florent przy 31-35% na pulapie sluzyl 21 dob w d281-308 (1/20 -> 22/20), Footly przy 45-61% 48 dob w d309-364 (2/20 -> 49/20).
2. POLNOC (16 z 57 rodow, sztandarowy przyklad wniosku) MA INNA PRZYCZYNE. Skok 25 -> 46 miedzy d196 a d252 to prawie sama Polnoc: 0 rodow bez sluzby do d196, 2 w d224, 18 w d252. Sluzbe stracily w tych samych dobach (d241-250) rody z pelnymi druzynami: Flint Lyessa 99%, Mormont 100% (d200), Whitehill 80-88%, Flint 80%, Umber 72%, Manderly 80% (d252), a chwilowo nawet Dustin 93% (17/20) i Ryswell 99% (14/20). Powod: w d113-140 sluzylo wszystkie 20 rodow po 17 dob (armia fabularna ROT, 51 partii - takze Karstark przy 3% i Cerwyn przy 29%), a od d141 Polnoc nie zebrala ani jednej armii (bitwy.log: 1 zdarzenie armii w calym roku). Doby wojny ze stykiem nabijaja samotne rajdy 2-5 bogatych rodow na wsie wroga na poludniu (river/crownlands/vlandia): dni ze zdarzeniem 18 z 56 (d141-196), 15 z 28, 20 z 28, 56 z 56 (d253-308), 50 z 56; wlasna ziemia Polnocy napadnieta 0-2 dni na 91. Pewne doby sluzby rodow Polnocy: d113-140 341 (20 rodow), d141-168 8 (2 rody), d169-196 11 (3), d225-252 1 wobec 280 pewnych dob bez sluzby. CrownRents.cs:125-129 liczy rajd i bitwe samotnego lorda jako styk calego krolestwa. Poprawka z wniosku tych rodow w chwili utraty nie obejmuje (maja druzyne > 60%); objelaby je dopiero po zwolnieniu ludzi - rod wisialby przy 60% z renta wlaczana i wylaczana.
3. "BEZ WYJSCIA" - NIE. 164 epizody bez warunku sluzby u 113 rodow: 109 zakonczonych odzyskaniem (mediana 19 dob; z 89 epizodow >= 28 dob odzyskane 44, trwa 44), 29 odzyskan przy wypelnieniu < 30%. Liczba rodow bez sluzby nie rosnie rowno: 20-27 przez d56-224, skok Polnocy, 46-48 w d253-336, 54 w d364; bez Polnocy 25 -> 41 w 280 dob. Stan bez powrotu to wezszy przypadek: zold zalogi (w wojnie placonej pierwszej, ClanBudget.cs:402, :458) >= caly pulap.
4. SKALA. Partie rodow 97 139 (d57-84) -> 63 129 (d337-364) = -34.0 tys. Rody z renta na pulapie (64): 28 185 -> 14 832 = -13.4 tys. (pulap 292 -> 176 tys. zl/d przy D 194 -> 166, kiesy 15.2 -> 7.2 mln - to obszar pulapu, nie rent). 57 rodow bez renty: -18.5 tys., z tego -13.0 tys. (71%) PRZED utrata renty, a po utracie -5.4 tys. (Polnoc -2.6 tys. od d253, reszta -2.9 tys.). Pulapka to ok. 16% rocznego spadku; "+3 do +7 tys." z wniosku ma gorna granice 5.4 tys.
5. POPRAWKA Z WNIOSKU NIE OBEJMUJE 100%. Z 54 rodow bez sluzby w d364 kryterium "zold >= 0.95 pulapu" lapie 40. Poza nim zostaja rody z 0-13 ludzmi i malym pulapem (Stout pulap 70 / zaloga 28, Forrester pulap 0, Gargalen 71/67, Westerling, Crane, Celeris) - nie sa "na 95%", a na druzyne ich nie stac. Kryterium "miejsce w pulapie partii < koszt 60% limitu" lapie 48 z 54; pozostale 6 to 3 rody z druzyna > 60% i 3 z luzem (prawdziwy brak sluzby). 7 rodow "oba" (Gargalen, Estatis, Forrester, Karstark, Locke, Stout, Beesbury) zostaje bez renty mimo zwolnienia ze sluzby - maja tez zaloge ponizej normy (drugi zamek tej samej pulapki, wniosek go nie wymienia).
6. Zwolnienie objeloby polowe swiata: przy progu 0.6 "na miare srodkow" jest 51 rodow z lennem w d84, 94 w d196, 118 z 198 w d364 (z tego 70 i tak ma dzis warunek). W zlocie to obojetne, ale regula sluzby zostaje realna tylko dla ok. 40% rodow, dopoki pulap trzyma reszte ponizej 60%.
7. Drobne: linia :128504 to doba 363 (55 rodow; liczby 57 / 47 / 3 / 7 sa w :128859); ArmouryBehavior.cs: CrownRents.Daily :1649, ClanBudget.Daily :1672 (kolejnosc zgodna).

BRAKUJACE POMIARY (log nie pozwala ustalic)
- zrodlo styku na krolestwo i dobe (armia w polu / oblezenie / napad na wlasna osade / tylko rajdy wlasnych lordow) - bez tego nie da sie policzyc skutku zmiany definicji styku poza Polnoca;
- rodzaj sluzby rodu w dobie i najwyzsze wypelnienie pojedynczej druzyny w budzet-rodow.csv (dzis tylko suma rodu);
- dlaczego Polnoc nie zbiera armii po d140 (BK: Kingdom.CreateArmy przechodzi tylko przy CanCreateArmy, a BKArmyBehavior.FindArmyObjective wymaga twierdzy wroga w zasiegu 280 jednostek od wodza albo wlasnej oblezonej w 350 - prawdopodobne, nie zmierzone).
Poza zakresem, do obszaru pulapu: kiesy 11 kompanii najemnych 3.0 -> 19.9 mln w roku (+46 tys. zl/d) przy kiesach wasali -23 mln.

**Poprawka ostateczna:**

WERDYKT: BLAD logiki warunku sluzby (doba wojny liczy sie rodowi, ktorego nikt nie wezwal albo ktory nie ma czym sluzyc, a kara zabiera srodki potrzebne do spelnienia warunku). Zakres: ok. 5.4 tys. ludzi z 34 tys. rocznego spadku; reszta to pulap zoldu (osobny wniosek). Dwie male zmiany w jednym miejscu (CrownRents.Daily), osobne klucze, jeden bieg 364 doby. Wielkosc S + S. Jesli ma isc tylko jedna - najpierw A.

A. SLUZBA NA MIARE SRODKOW - z poprawionym kryterium (zamiast "zold >= 0.95 pulapu")
1. C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int\Armoury\src\ClanBudget.cs, obok TryCap (:203): internal static bool AtMeans(Clan c). b = Of(c) (budzet z wczoraj; brak wpisu, Straz b.Zero albo najemnik -> false). prog = Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy (0.6, bez nowej liczby). Petla po czynnych druzynach lordow rodu: ktorakolwiek z PartySizeRatio > prog -> false; sumuj limit (Party.PartySizeLimit), ludzi (TotalRegulars), zold (TotalWage + GentryService.KnightWage). Zold na czlowieka = zold / ludzie przy >= 20 ludziach, inaczej Campaign.Current.AverageWage. Potrzeba = prog x limit x zold na czlowieka; rod bez druzyny: 30 x zold na czlowieka (ta sama liczba co regula "nowa partia" 166). Wynik: b.PartyCap < potrzeba, czyli pulap po zalogach i karawanach nie pozwala dojsc do progu wezwania.
2. ...\Armoury\src\CrownRents.cs: zbior _means (id rodow; czyszczony w ClearState i razem z lista gone). Po :382, w petli rodow w wojnie: m = klucz && ClanBudget.AtMeans(c); _means dodaj albo usun; gdy how == 0 i m -> how = 8 (bit sluzby w dobie ze stykiem, licznik "na miare srodkow N" przy :410-411 i w linii "sluzba dzis"). W bloku warunku po :476: gdy Served < Need i rod jest w _means -> Need = 0. Renta wraca od razu, nie po 20 dobach - inaczej zaloga zdazy spasc ponizej normy i powstaje stan "oba".
3. ...\Armoury\src\Settings.cs obok :1105: public bool CrownRentServiceAtMeans = true; potem python tools/gen_mcm.py. Klucza nie ma w Armoury.json Jeffa.

B. DOBA WOJNY ZOBOWIAZUJE KROLESTWO TYLKO PRZY WEZWANIU ALBO NAPADZIE NA WLASNA ZIEMIE (przyczyna Polnocy)
1. CrownRents.cs OnMapEvent :128-129, nowy klucz CrownRentContactOnlyWhenCalled = true: zamiast dodawac do _contact oba krolestwa - tylko krolestwo, do ktorego nalezy me.MapEventSettlement (rabunek, wymuszenie, szturm, wypad, blokada wlasnej osady). Bitwa w polu i rajd samotnego lorda nie zobowiazuja jego krolestwa. Uczestnicy dalej maja sluzbe 3/4/6/7 w dobach ze stykiem. Daily :389-403 bez zmian: armia krolestwa w polu i obie strony oblezenia licza sie jak dotad.
2. Settings.cs: klucz obok CrownRentWarDayNeedsContact (:1102) + gen_mcm.
Skutek pewny dla Polnocy: od d141 0 dob zobowiazania (brak armii, wlasna ziemia napadnieta 0-2 dni na 91) -> maski stoja na 17-35 dobach sluzby przy wymogu 12 -> 20 z 20 rodow z renta. Poza Polnoca kierunek ten sam (zobowiazan nigdy wiecej niz dzis), wielkosc nieznana do pomiaru z C.

C. POMIARY (tylko log i CSV, razem z A)
- linia "Renty korony (180)": krolestwa ze stykiem wedlug zrodla (armia w polu / oblezenie / napad na wlasna osade / tylko rajdy i bitwy wlasnych lordow), rody "na miare srodkow" (ile z nich i tak sluzylo);
- budzet-rodow.csv: kolumny sluzba_dzis (0-8) i wyp_max (najwyzsze PartySizeRatio druzyny rodu);
- sprawdzic regexy tools/sprawdz_logi.py na linii 180 (:7651-7730) po dopisaniu pol.

RACHUNEK
Zloto nie powstaje: renta zmienia odbiorce w tym samym krolestwie (skarbiec -> glowa jak dotad), zapis arm_rent180 bez zmian, koszt doby pomijalny (ok. 200 rodow x kilka druzyn). Do rodow objetych wraca do ok. 62.6 tys. zl/d (F9 d337-364), dzis prawie w calosci u rodow z luzem: ich renta 92 -> 143 tys. zl/d przy zoldzie partii stalym ok. 200 tys., kiesy +17 tys. zl/d. Ludzie: gorna granica +5.4 tys. (powrot do stanu sprzed utraty renty), realnie +3.5 do +5 tys. (63.1 -> ok. 67-68 tys. w d337-364), z tego Polnoc +2.0 tys. przy samym A i do +2.6 tys. z B (7.5-7.9 tys. zamiast 4.9). To NIE zamyka spadku 97 -> 63 tys.

RYZYKO
- A: przy progu 0.6 zwolnionych jest 51 (d84) do 118 (d364) rodow z lennem - regula sluzby K10 zostaje realna dla ok. 40% rodow, dopoki pulap trzyma reszte ponizej 60% (prog 0.3 objalby 84 rody i 44 zamiast 48 rodow bez sluzby - zostawic 0.6 jako prog samej gry). Rod przy granicy moze miec rente co kilka dob (D to srednia 28 dob, wiec pulap nie skacze). Pierwsza doba po wczytaniu: brak wpisu budzetu -> jedna doba bez zaliczenia. W pokoju flaga _means zostaje z ostatniej doby wojny; po wczytaniu w pokoju jest pusta - dotyczy tylko rodu zwolnionego krocej niz 20 dob styku przed pokojem.
- B: w wojnie prowadzonej samymi rajdami nikt nie jest zobowiazany - renta wedlug wag lenn jak w pokoju; samotni lupiezcy przestaja zgarniac cudze udzialy (Polnoc: 4 rody biora dzis 60.4 z 85.5 tys. D).
- Zostaje: rody "oba" i sama "zaloga" (dzis 10; w nowej kampanii z A oczekiwane 2-4, bo utrata renty nie zdazy zdjac zalogi). Jesli po 120 dobach "oba" > 2 - ta sama zasada dla warunku zalogi jako drugi krok.

SPRAWDZIC W BIEGU 364 DOB
rody z lennem bez warunku <= 10% w kazdym oknie (dzis 27%); "sluzba" <= 8, "oba" <= 2; rody z lennem z 0 ludzi <= 12 (dzis 29) i nie rosna po d196; F9 <= 20 tys. zl/d (dzis 62.6); Polnoc >= 7.5 tys. w d337-364 i 0 rodow Polnocy bez renty przy samych rajdach; partie rodow d337-364 >= 66 tys.; suma zlota swiata bez zmiany.

NIE ROBIC
- nie wylaczac F9 i nie znosic warunku sluzby (jak we wniosku);
- nie obnizac progu wezwania 0.6 w modelu armii (zmienia wojne AI w calym swiecie, wielkosc M-L, nie pomaga krolestwu bez armii ani rodom z 0 ludzi);
- nie ciac zalog w wojnie pod pulap (koliduje z warunkiem zalogi i decyzja "zalogi finansowane pierwsze").


### O1-3 kolejnosc zwolnien: budzet odprawia najtanszych, wiec za te same pieniadze zostaje mniej glow

**Werdykt:** STROJENIE | **pewnosc:** srednia | **podwazenie - broni sie:** True

**Przyczyna:**

SRC\ClanBudget.cs:757 'stacks.Sort((x, y) => x.Character.Tier.CompareTo(y.Character.Tier))' w ReleaseFrom (:743-811) - po najemnikach z karczmy odchodzi najnizszy tier (opis klucza Settings.cs:1046 'as men, lowest first'). Zeby zdjac 100 zl zoldu, pan odprawia ok. 50 ludzi t1 zamiast ok. 8 z t5; rod na pulapie nie werbuje tez nowych tanich (limit zoldu partii, ClanBudget.cs:489-531). Skutek w csv (srednie d57-84 -> d337-364, skrypt grp.py): zold partii na czlowieka u rodow bez renty 4.74 -> 7.43 zl, u rodow z renta na pulapie 5.13 -> 7.97 zl, u rodow z luzem 4.36 -> 5.26 zl (+21% - zwykle starzenie sie wojska). Gdyby rody na pulapie mialy zwykly wzrost +21% (5.7 i 6.2 zl), za dzisiejszy zold (43.0 i 118.2 tys./d) mialyby 7.5 i 19.1 tys. ludzi zamiast 5.8 i 14.8 tys. = ok. +6 tys. glow bez jednej monety wiecej. Potwierdzenie posrednie z linii 'Weterani (E4/10)' d165: wsrod odchodzacych uprawnieni t3+ 292 wobec 316 ponizej t3 - rody na pulapie nie maja juz tanich ludzi do odprawienia. Uboczny skutek: mniej glow = wypelnienie druzyny ponizej 60% = wejscie w pulapke O1-2. Brak bezposredniego pomiaru tieru zwalnianych (do dopisania w linii 166).

**Poprawka:**

Wielkosc S; to wybor zasady, nie blad - zalecam zmiane. SRC\ClanBudget.cs ReleaseFrom (:757-778): po przebiegu 'tylko najemnicy z karczmy' zdejmowac kwote PO ROWNO z kazdego tieru wedlug udzialu w zoldzie partii (n_i = min(Number_i, ceil(kwota x Number_i / suma zoldu partii))), reszte z zaokraglen od najnizszego tieru jak dotad. Klucz bool ReleaseAcrossTiers = true w SRC\Settings.cs przy :1046 (wylaczony = stara kolejnosc) + python tools/gen_mcm.py; w linii 'Budzet rodow (166)' dopisac 'zwolnieni wedlug tieru t1-2 / t3-4 / t5+'. Rachunek: zold na czlowieka rodow na pulapie przestaje rosnac ponad zwykle +21%; oczekiwane ok. +4 do +8 tys. glow w 2-3 miesiace przy tym samym zoldzie i wiecej druzyn ponad progiem 60%. Zloto i ludzie bez zmian (ci sami zwalniani ida do wsi i do karczmy tymi samymi drogami). RYZYKO: wiecej t3+ trafia do karczm (E4/10: 33% uprawnionych) - pula weteranow rosnie szybciej (dzis 10 094, wejscia 134/d, najem 80/d, powrot do domu po 120 dobach), najmuja ich bogate rody; druzyny biednych rodow beda slabsze jakosciowo, ale liczniejsze (w rozstrzyganiu bitew gry niski tier daje wiecej sily za 1 zl). Sprawdzic w audycie kodu, co dzieje sie ze sprzetem i sakiewka zwalnianych t5+ (ToVillage, VetPurse). Odrzucone: 'najdrozsi najpierw' (dalby do +20 tys. glow, ale rozbiera elite panow - zbyt duza zmiana charakteru wojska bez decyzji Jeffa).


**Podwazenie - uwagi:**

WNIOSEK SIE BRONI CO DO MECHANIZMU I WERDYKTU (STROJENIE), ALE NIE JAKO GLOWNA PRZYCZYNA TRENDU, A WZOR POPRAWKI JEST WADLIWY. Skrypty: ...\scratchpad\triaz3\podw-o1-3\ (rel, flow, relw, sq, trend, vol, amt, ovh .py); dane: log 02-15-21 i Logs\2026-10-13_02-15-21\budzet-rodow.csv, balans-krolestw.csv.

CO POTWIERDZILEM (niezaleznie, mocniej niz autor):
1. Kod: ClanBudget.cs:757 sortuje po tierze rosnaco, przebieg 0 = tylko najemnicy z karczmy (:710, :753); Settings.cs:1046 'lowest first'. To zamierzona regula 166, nie blad.
2. 37 rodow na pulapie z >=100 ludzi w obu oknach: zold partii 99.4 -> 100.8 tys./d (bez zmiany), ludzie 18 867 -> 12 259 (-35%), zl/czl 5.27 -> 8.22 (+56%); rody z luzem 4.31 -> 5.27 (+22%). Narzut zoldu bohaterow/rycerzy pomijalny (mediana 33 zl/rod).
3. Bezposredni pomiar: w dniach zwalniania (dni_ponad>=3) ubywajacy kosztuja 3.65 zl/czl (40 036 ludzi, 145 978 zl), a srednia tych partii to 9.28 zl/czl. Przyklad Maegyr (clan_empire_south_2) d320-336: kiesa -130 tys. w dobe, pulap 6250 -> 3924, zold partii -25% (3670 -> 2754), ludzie -54% (597 -> 274).
4. Projekt juz to znal: MercContract.cs:175-177 ('zwolnienia od najtanszych zabieraja duzo wiecej ludzi niz zlota, a awanse do limitu podnosza zold czlowieka').

CO OBALAM / POPRAWIAM:
A. To nie glowna przyczyna spadku. Partie rodow z budzetem 95.1 -> 60.8 tys. (-34 tys.). Podzial wg stanu d337-364: klasa A (38 rodow, zalogi+karawany >= 80% pulapu) 17 950 -> 616 ludzi (-17.3 tys.): pulap 178 -> 31 tys., D staly 105 -> 39 tys., G 8.25 -> 2.0 mln, zold zalog staly 41 -> 36 tys. - partie zwolnione DO ZERA, kolejnosc nie ma znaczenia (w 25% dni-rodu zwalniania kwota dnia >= 63% zoldu partii, w 10% >= 169%). Klasa B (73 rody na pulapie) 33 941 -> 18 335 (-15.6 tys.): pieniadz -4.5, zwykle starzenie -5.1, nadwyzka -6.0 tys. Klasa C (86 z luzem) bez zmian. Kolejnosc tlumaczy wiec najwyzej 6 z 34 tys. (18%; gorna granica 9.8 tys., gdyby B zeszlo do zl/czl klasy C).
B. Trend: w klasie B zl/czl rosnie caly rok bez stabilizacji (4.99, 5.31, 5.72, 5.93, 6.02, 6.33, 6.82, 6.92, 7.12, 7.22, 8.01); w klasie C plateau 5.2-5.4 od d225; sredni tier swiata 2.43 -> 2.96, plateau od d225.
C. Pominieta druga polowa zapadki: awanse gry wypelniaja wolne miejsce limitu natychmiast (PartyUpgraderCampaignBehavior.cs:112-114), a pulap skacze z kiesa (skarb wojenny 0.8(G-R)/45): 382 przejscia 'pod pulapem -> ponad 1.10' w 64 rodach (6 na rod), 56% luki ze spadku pulapu. Stad wolumen: zwolnieni 148 569/rok (z partii 129 063: 215/d w d57-84 -> 520/d w d337-364), tyle co polegli w partiach (132 029).
D. Zdanie 'rod na pulapie nie werbuje' jest falszywe: ta grupa przyjela +100 678 ludzi w dniach bez zwalniania (zwolnila 37 631) - to rotacja werbunek -> zwolnienie.
E. 'Potwierdzenie' z linii Weterani d165 zle odczytane: 292/316 to wszystkie zrodla (H3, W4c, I1, rozwiazane), nie same 166. Z wejsc 166 do karczmy (33% uprawnionych): t3+ to 22% zwalnianych (d85-112) -> 32% (d337-364); 68-78% zwalnianych to nadal t1-2.
F. Wzor n_i = ceil(kwota x N_i / zold) nie zadziala: mediana kwoty dnia rodu 60 zl = 8.6% zoldu partii (36% dni < 5%), czesc stosu < 1 czlowiek, ceil daje po 1 z kazdego stosu = kilka razy wiecej niz kwota, a z przerwaniem po osiagnieciu kwoty wraca 'od najnizszych'.
G. Ryzyko karczm przeszacowane: dzis t3+ ok. 115-165/d z 520; po zmianie ok. 135-150/d z ok. 245 (mniej glow, wyzsze tiery). Ale pula weteranow juz dzis sie nie stabilizuje (309 -> 10 094, +38/d na koncu) - pilnowac.
H. Nic z niczego: bez zastrzezen (te same drogi ludzi; sakiewka :789-800 idzie proporcjonalnie do glow, wiec do wsi pojdzie ok. polowa dzisiejszej kwoty, reszta zostaje w sakiewkach). Wydajnosc: bez znaczenia. Nie sprawdzone: los sprzetu i koni zwalnianych t5+ (ToVillage :862-907 sprzetu nie rusza; nadwyzki zbrojowni ida po ok. 2 zl/szt.).

**Poprawka ostateczna:**

Wielkosc M (nie S): zmiana skladu wojsk AI w calym swiecie - 1 recenzja + autotest 120 dob. Jedna paczka, klucz wylacza.

1. SRC\Settings.cs przy :1046: public bool ReleaseAcrossTiers = true; opis po angielsku ('men sent home for lack of budget are drawn evenly from the whole party, not lowest tier first; tavern mercenaries still go first; off = lowest tier first'); potem python tools/gen_mcm.py; sprawdzic, ze klucza nie ma w Armoury.json.

2. SRC\ClanBudget.cs ReleaseFrom (:743-806), tylko gdy !mercsOnly i klucz wlaczony (przebieg najemnikow z karczmy bez zmian; ta sama regula dla partii i zalog - jedna zasada):
- NIE wzor z ceil na stos. Losowanie czlowieka: kazdy czlowiek partii ma rowna szanse (stos z waga = liczba ludzi, MBRandom.RandomInt(razem)), bez zwracania, az done >= amount; ostatni czlowiek przy reszcie mniejszej niz jego zold - z prawdopodobienstwem reszta/zold (srednio dokladnie kwota, bez zatoru i bez stalego nadmiaru).
- Najpierw zebrac liczby na stos (take[i]), potem dla kazdego stosu z take>0 istniejace cialo petli bez zmian (ranni proporcjonalnie, PeopleBook.Take -> ToVillage -> AddToCounts, done liczone tylko z wykonanych) - najwyzej 1 wywolanie ToVillage na stos, zadnego nowego stanu w zapisie.
- Klucz wylaczony = dzisiejsza petla :765-779 bez zmian.

3. Pomiar w linii 'Budzet rodow (166)' (Report, 3+3 liczniki dnia): 'zwolnieni wedlug tieru t1-2 / t3-4 / t5+ (ludzi) i ich zold (zl)' oraz 'partie zwolnione do zera przy zalogach >= pulap (rodow)'. To brakujacy pomiar - bez niego skutku nie da sie potwierdzic.

4. Przed wlaczeniem - audyt kodu (odczyt): co dzieje sie ze sprzetem i koniem zwalnianego t5+ (zbrojownia partii, sprzedaz nadwyzek ok. 2 zl/szt., 'kon weterana' E6/K1), zeby drogi sprzet nie szedl za bezcen.

RACHUNEK: dotyczy tylko klasy B (73 rody na pulapie, 18.3 tys. ludzi, zold 146.8 tys./d, 8.01 zl/czl). Dzis zwalniany kosztuje 3.65 zl, po zmianie srednia partii (ok. 8 zl): przy tym samym zoldzie zwalnianym (ok. 474 zl/d w grupie) odchodzi ok. 59 zamiast 128 ludzi/d = +69 glow/d na poczatku, malejaco. Cel: zl/czl klasy B 8.0 -> 5.2-6.0 = +6 do +9.8 tys. glow w 3-6 miesiecy bez monety wiecej (partie swiata 60.8 -> ok. 67-70 tys.). Klasa A (-17.3 tys., partie wycisniete przez zalogi i zapadniety pulap) nie zyska nic - to osobna sprawa (zalogi w wojnie placone pierwsze, GarrisonWarFull :458/:402, renta 180).

PROGI TESTU 120 dob (klucz wl. wobec biegu WGRANIA 35): zl/czl rodow na pulapie nie rosnie szybciej niz u rodow z luzem o wiecej niz 5 pkt proc.; 'zwolnieni z partii' w glowach ok. polowa przy podobnym zoldzie zwalnianych; ludzie partii >= +3 tys. w d120; pula weteranow nie rosnie szybciej niz dzis (+38/d); 0 ERROR.

RYZYKO: elita i jazda rodow na pulapie rzednie (t5+ odchodzi wedlug udzialu); druzyny liczniejsze, nizszego tieru; do kies wsi z sakiewek zwolnionych ok. polowa dzisiejszej kwoty. Odrzucone: wzor autora (zaokraglenia), 'najdrozsi najpierw', blokada awansow przy pulapie (latka na kod gry, zatrzymuje rozwoj wojska).


### O1-4 kompanie najemne zbieraja zloto, ktorego nie maja jak wydac: 2.9 -> 20.6 mln w rok

**Werdykt:** BLAD | **pewnosc:** srednia | **podwazenie - broni sie:** True

**Przyczyna:**

budzet-rodow.csv, 11 rodow najemnych (skrypt merc.py): G razem 2.87 mln (d1) -> 20.60 mln (d364), +17.73 mln = 48.7 tys./dobe; to 33% zlota wszystkich rodow przy 2 494 ludziach (limit druzyn 6 805). Skad: (a) 202 skoki kiesy ponad 30 tys. w jednej dobie na 10.77 mln przy 681 zmianach pracodawcy w roku (kazda kompania zmienia krola co ok. 6 dob) - jednorazowa zaplata krola z wlasnej kiesy przy najmie, wskazana juz w TRIAZ-2 (C:\Users\GAME\Bannerlor2-Realm-of-Thrones\docs\test-zbiorczy\TRIAZ-2-zbior-e8-120-2026-10-13.md:411-418; podejrzana droga DiplomaticBartersBehavior.ConsiderClanJoinAsMercenary -> ExecuteAiBarter - w logu samego przelewu nie widac); po drugiej stronie 305 spadkow kiesy glow nie-najemnych ponad 50 tys. w dobie na 23.6 mln; (b) kontrakt 185: wplyw 14.47 mln wobec zoldu 4.41 mln w roku (linia 'Kontrakty najemnikow (185)' d364: nalezne 30 383/d przy zoldzie 16 613/d - placone za 'ludzi z umowy' 3 429, nie za 2 578 prawdziwych). Odplywu nie ma: pulap najemnika = zold ludzi z umowy, bez dworu i budow (SRC\ClanBudget.cs, blok 'if (merc)' w Daily ok. :305). Zloto wychodzi z kies krolow i skarbcow (skarbce krolestw 57.4 -> 26.3 mln w roku), czyli z tych, ktorzy placa zold, zwrot zoldu i renty.

**Poprawka:**

Wielkosc M, najpierw krotkie rozpoznanie w dekompilacji (jak zalecal TRIAZ-2 pkt 3): potwierdzic droge przelewu krol -> kompania przy najmie AI-AI (C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\dec32\cs_full, DiplomaticBartersBehavior i czynny DiplomacyModel) i dopisac jej licznik do linii 185. Kierunek: (1) przy czynnym kontrakcie 185 najem AI-AI bez zlota z kiesy krola - jedna zaplata za jedna sluzbe (SRC\MercContract.cs, latka w kampanii, nie w SubModuleMain); (2) kontrakt liczony od prawdziwych ludzi kompanii, nie od 'ludzi z umowy' (dzis 1.83 x zold). Rachunek: ok. 29.5 tys./d zostaje w kiesach krolow i ok. 13.8 tys./d w skarbcach = 43 tys./d; przy udziale zoldu 0.6-0.8 i 6.2 zl na czlowieka to do +4-5 tys. ludzi, realnie mniej (czesc krolow stoi na limicie druzyny) - przyjmuje +2 do +5 tys. Ryzyko: kompanie rzadziej zmieniaja strone (mniej chaosu na mapie - do oceny), zapis bez zmian. Nagromadzone 20.6 mln zostaje u kompanii w biezacej kampanii; Jeff gra nowa, wiec wystarczy zamknac doplyw.


**Podwazenie - uwagi:**

WNIOSEK SIE BRONI CO DO ISTOTY (kompanie zbieraja zloto bez ujscia, glowne zrodlo = jednorazowa zaplata krola przy kazdym najmie, werdykt BLAD), ale droga przelewu jest INNA niz podejrzewano, dwie liczby sa zle, przyczyna pierwotna nie zostala nazwana, a poprawka (2) w podanym brzmieniu zepsulaby C1-m. Pliki pomocnicze: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\triaz3\podw-o1-4\ (m2-m6.py, kings*.py, k185.py, sk.py).

POTWIERDZONE (wlasny rachunek z budzet-rodow.csv biegu 364 i linii logu):
- 11 kompanii: G 2.87 -> 20.60 mln (+17.73 mln, 48.7 tys./d), 33.1% G wszystkich rodow (62.20 mln) przy 2 494 ludziach, limit 6 788. Trend G na koniec okien 30 dob (mln): 3.30 / 4.83 / 6.47 / 7.97 / 9.44 / 11.26 / 12.83 / 14.56 / 16.49 / 17.90 / 18.95 / 20.30 - rosnie liniowo caly rok (35-64 tys./d), bez stabilizacji.
- 681 zmian pracodawcy w roku; log 185: nowe umowy 678, przeglad w dol 2, odeszly 0.

KOREKTA 1 - DROGA PRZELEWU TO NIE ExecuteAiBarter, TYLKO ZADATEK BANNERKINGS:
- C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\e4tools\bkfull\BannerKings.Behaviours.Mercenary\BKMercenaryCareerBehavior.cs:133-139 (OnClanChangedKingdom, detail JoinAsMercenary -> AddDownPayment) i :146-151: kwota = min(a, RulingClan.Gold); kingdom.RulingClan.Leader.ChangeHeroGold(-kwota); mercenaryClan.Leader.ChangeHeroGold(+kwota).
- Wzor: bkfull\BannerKings.Models.Vanilla\BKDiplomacyModel.cs:361-381 GetMercenaryDownPayment = (5% zlota rodu krola + sila kompanii x MercenaryAwardMultiplier / 2) x reputacja kompanii (0-1), najwyzej cala kiesa krola.
- ChangeHeroGold bez zdarzenia gry - dlatego ksiega (ClanIncomeBook.cs:156-172, :341) tego nie widzi. Przelew barterem szedlby przez GiveGoldAction (GoldBarterable.cs:64-69) i bylby w kolumnie 'zdarzenia': w dobach najmu zdarzenia = 1 215 zl srednio, w pozostalych 1 172 - brak roznicy, czyli barter to nie ta droga.
- Dowod liczbowy: przyrost kiesy kompanii ponad ksiege (dk - wplyw_doby + zold) w 681 dobach najmu = +12.09 mln (33.2 tys./d; srednio 17.7 tys. na najem, mediana 11.2, p90 51.6, max 182 920), poza dobami najmu -4.37 mln (zwykle wydatki). 209 najmow pasuje co do 3% do spadku kiesy jednego rodu nowego krolestwa i jest to zawsze rod krola (Targaryen Aegon 537 tys., Lo 437, Mormont 342, Emeros 339, Rayder 290...). Przyklad: doba 334 Bright Banners Dragonstone -> House Targaryen, Aegon: kompania +182 920, krol z 454 906 traci 183 283 (40% kiesy); doba 225 Brotherhood -> Talordis +53 040, krol -54 108.
- Trend okna 60 dob: 23.4 / 32.0 / 39.1 / 40.5 / 35.3 / 29.8 tys./d przy 119 / 130 / 114 / 122 / 105 / 86 najmach; srednio na najem 11.8 -> 20.8 tys. (reputacja rosnie z renomy).
- Wniosek podawal 10.77 mln (202 skoki ponad 30 tys.) - zanizone, pomija 479 mniejszych zadatkow.

KOREKTA 2 - KONTRAKT 185: '14.47 mln wplywu' to CALA ksiega wplywow kompanii, nie kontrakt. Rozbicie roku: kontrakt 8.49 mln (suma 'zaplacone z wplywow dnia' z 364 linii logu: nalezne 8 849 699, zaplacone 8 492 101), zdarzenia (lup, jency) 4.71, model 0.58, trzecia 0.17, inne 0.5. Zold 4.41 mln. Nadplata ponad zamierzone 1.30 x zold = 2.76 mln/rok = 7.6 tys./d srednio (8.8 tys./d w dobie 364: nalezne 30 383 przy zoldzie 16 613), NIE 13.8 tys./d (wniosek odjal 1.0 x zold zamiast 1.3 x). Bilans roku sie zamyka: 12.09 + 14.47 - 4.41 - 4.37 = +17.77 mln.

KOREKTA 3 - PRZYCZYNA PIERWOTNA, KTOREJ WNIOSEK NIE NAZWAL: skakanie miedzy krolami. Mediana sluzby 4 doby, 38-49% sluzb trwa <= 3 doby, tylko 3 z 670 trwaly >= 28 dob. Wszystkie 681 zmian to przejscie WPROST z krola do krola (zero odejsc do 'bez krolestwa'):
- gra: cs_full\TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors\DiplomaticBartersBehavior.cs:99-143 - kompania w sluzbie z szansa 40% na dobe rozwaza inne krolestwo; MercenaryJoinKingdomBarterable.cs:65-68 wola ApplyByJoinFactionAsMercenary z default(CampaignTime), wiec ChangeKingdomAction.cs:39-42 nie ustawia zadnego terminu (ShouldStayInKingdomUntil = zero), a bramka :117 nigdy nie trzyma;
- BK: BKDiplomacyModel.cs:444-490 MercenaryLeaveScore nie odejmuje oceny obecnego pracodawcy (gra odejmuje), a mnoznik -0.8 'kontrakt w toku' zmniejsza kare z -5000 do -1000; kary BK za zerwanie rocznego kontraktu (MercenaryCareer.cs:436-485 RemoveKingdom: reputacja, relacje) dzialaja tylko przy odejsciu, a przy przejsciu wprost MercenaryCareer.cs:117-123 po prostu zaczyna nowy rok.
Skutki: kazdy skok = nowy zadatek BK oraz nowa umowa 185 z podloga 50% pelnej wielkosci (MercContract.cs:155-158), a przeglad co 28 dob (:172) nigdy nie nastepuje - stad 'ludzie z umowy' 3 300 przy 2 300 prawdziwych przez caly rok. Uwaga do ryzyka z wniosku: zdjecie zadatku NIE zmniejszy skakania (ocena najmu nie zawiera zadatku; zmian bylo tyle samo w B1, B2 i W33: 221 / 219 / 255 na 120 dob).

KOREKTA 4 - 'odplywu nie ma': kompanie wydaja poza zoldem 4.37 mln/rok (sprzet, jedzenie, werbunek, okupy), ale nic nie rosnie z kiesa: ClanBudget.cs:311-320 (pulap = zold ludzi z umowy, dwor 0, budowy 0; we wniosku ':305', w kodzie 35 jest :320). Spadek skarbcow 57.8 -> 26.3 mln to glownie inne reguly (kontrakt idzie z wplywow dnia, 8.49 mln/rok) - nie nalezy go przypisywac najemnikom.

SKUTEK DLA WOJSKA (sprawdzony niezaleznie, szacunek +2 do +5 tys. sie broni): krolowie z kiesa ponizej 20 tys.: 0 (d30), 1 (d60), 4 (d120), 3 (d180), 6 (d240), 15 (d300), 11 (d364) z 31. 21 krolow, ktorzy koncza rok z G < R, zaplacilo 8.70 mln zadatkow w 538 najmach przy stalym dochodzie D (107 -> 102 tys./d): ich G 4.97 -> 1.32 mln, pulap zoldu 97.8 -> 54.3 tys./d, wlasne druzyny 9 461 (d60) -> 6 791 (d120) -> 5 486 (d240) -> 3 736 (d364) przy limicie 16 tys. Pozostalych 13 krolow (3.39 mln, 143 najmy): 8 430 -> 8 755. Przyklady: Qarth/Emeros kiesa 519 775 -> 1 745, druzyna 708 -> 26, zadatki 528 tys.; Myr/Saldorys 390 394 -> 4 269, 562 -> 68; Dragonstone/Stannis 347 572 -> 1 383, 557 -> 0; linia Skarbce: Free Folk krol Rayder kiesa 370 205 (dzien 108840) -> 8 763 (dzien 109199). Zatrzymane 23.9 tys./d u tych krolow to ok. +3-4 tys. ludzi, plus do ok. 1 tys. z 7.6 tys./d w skarbcach. To ok. 10% rocznego spadku wojska (97.6 -> 61.3 tys.) - reszta ma inne przyczyny.

WERDYKT: BLAD - utrzymany. Paczka 185 zalozyla 'gra dla AI w sluzbie nie placi nic z siebie' i wylaczyla dwie wyplaty gry, ale nie znala trzeciej (zadatek BK; w repo i dokumentach brak wzmianki). Pewnosc co do mechanizmu: wysoka. Bez poprawki swiat sie nie stabilizuje: kompanie +48.7 tys./d bez konca, krolowie malych krolestw bez wlasnych druzyn.

CZEGO NIE SPRAWDZILEM: skladnikow wzoru zadatku na pojedynczym najmie (sily, mnoznika i reputacji nie ma w logach) - zgodnosc potwierdza platnik, moment i brak zdarzenia, nie rachunek co do zlotowki.

**Poprawka ostateczna:**

Dwie poprawki S teraz (razem z testem 120 dob - M), trzecia S jako osobna decyzja. Rozpoznanie z wniosku jest zrobione - droga potwierdzona. Wszystko w C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\MercContract.cs (+ Settings.cs i python tools/gen_mcm.py). Zapis bez zmian, nic nie powstaje z niczego, koszt dla szybkosci gry zerowy.

A) ZADATEK BK DLA KOMPANII AI WYLACZONY (S) - 'jedna zaplata za jedna sluzbe'.
- Gdzie: MercContract.EnsureHooks() (w kampanii, obok dwoch istniejacych latek; wzor szukania typow BK jak w BkDipGold.cs:393-422): AccessTools.TypeByName("BannerKings.Behaviours.Mercenary.BKMercenaryCareerBehavior"), metoda prywatna AddDownPayment(Clan mercenaryClan, Kingdom kingdom), prefiks Harmony.
- Prefiks: gdy On i nowy klucz MercBkDownPaymentAiOff (domyslnie true) i mercenaryClan != Clan.PlayerClan -> licznik doby +1 i return false; inaczej return true. Wyjatek = licznik potkniec, return true. Typ albo metoda nie znaleziona -> 'BRAK' w linii startowej 185.
- Log: do linii 'Kontrakty najemnikow (185): dzien N' dopisac 'zadatek BK wstrzymany: N najmow'.
- Gracz-najemnik bez zmian (zostaje na kontrakcie gry i zadatku BK, jak w opisie 185). Gracz-krol najmujacy kompanie AI tez nie placi zadatku (placi skarbiec kontraktem 185) - do odnotowania w CHANGELOG.
- Rachunek: 12.09 mln/rok = 33.2 tys./d zostaje w kiesach krolow (681 najmow, srednio 17.7 tys.); u 21 zubozalych krolow 8.70 mln = 23.9 tys./d.

B) KONTRAKT PLACONY ZA LUDZI POD BRONIA, UMOWA DALEJ WYZNACZA PULAP (S) - zamiast poprawki (2) z wniosku.
- Poprawka (2) w brzmieniu 'kontrakt liczony od prawdziwych ludzi' cofnelaby C1-m: pulap 166 = zold ludzi z umowy (CapOf, MercContract.cs:111-119), wiec kompania najeta po bitwie nie odrosnie nigdy (test 120 dob: najemnicy -79%, Moon Brothers 31 z 350). Dlatego zmieniamy tylko WYPLATE.
- Gdzie: MercContract.Daily(). Przed skalowaniem podloga (:155-156) zapamietac prawdziwy zold dnia: long wageNow = wage. W :190 zamiast 'due = war ? d.K : (long)(d.K * peace)': long kNow = Math.Min(d.K, (long)Math.Round(factor * wageNow)); due = war ? kNow : Math.Min((long)(d.K * peace), kNow).
- d.K, d.Wage, d.Men, podloga 0.5, przeglad 28 dob i CapOf bez zmian: kompania dobiera ludzi do umowy z wlasnej kiesy, a korona od nastepnej doby placi 1.30 x za tych, ktorzy sa. Kompania bez ludzi w polu (wodz w niewoli): zold 0 -> wyplata 0 (dzis dostaje pelny kontrakt za nic).
- Rachunek: wyplaty 8.49 -> ok. 5.73 mln/rok (1.30 x 4.41), czyli 7.6 tys./d srednio (8.8 tys./d pod koniec roku) zostaje we wplywach dnia na zwrot zoldu i renty. Nie 13.8 tys./d.

Po A i B bilans kompanii: 1.30 x zold + zdarzenia 12.9 tys./d - zold - pozostale wydatki 12.0 tys./d = ok. +4.5 tys./d (w tym 3.6 tys./d marzy 0.30 na jedzenie i sprzet) zamiast +48.7 tys./d. Jeff gra nowa kampanie, wiec nagromadzonych 20.6 mln nie trzeba ruszac.

C) MINIMALNY CZAS SLUZBY 28 DOB (S, OSOBNA DECYZJA - zmienia zachowanie na mapie; do zlota niepotrzebne po A i B).
- Postfiks na ChangeKingdomAction.ApplyByJoinFactionAsMercenary: klan AI w sluzbie -> clan.ShouldStayInKingdomUntil = CampaignTime.DaysFromNow(MercMinServiceDays), klucz 28 (ta sama miara co MercReviewDays i MercUnpaidLeaveDays). Pole gry, publiczne, zapisywane; gra sama sprawdza je przy przejsciu do innego krola (DiplomaticBartersBehavior.cs:117), przy odejsciu (:147) i przy odejsciu od zadluzonego krola (ClanVariablesCampaignBehavior.cs:404). Nasze odejscie po niedoplacie, zwolnienie przez BK i upadek krolestwa ida akcja wprost i nie sa blokowane.
- Skutek: 681 -> najwyzej ok. 125 najmow w roku; przeglad 28 dob wreszcie zadziala; kompania ma czas dobrac ludzi i dojsc na front (dzis mediana sluzby 4 doby).
- Zalecam po tescie A+B, nie razem - zeby wiedziec, co dalo efekt.

OCZEKIWANY SKUTEK DLA WOJSKA: ok. +3 do +5 tys. ludzi po roku (wlasne druzyny krolow malych krolestw), czyli ok. 10% rocznego spadku. Nie obiecywac wiecej.

SPRAWDZIAN W TESCIE 120 DOB (porownanie z biegiem rocznym, okno d1-120):
1) linia 185: 'zadatek BK wstrzymany' ok. 1.9 najmu na dobe; 'kontrakty nalezne' ok. 1.30 x zold (13-16 tys./d, bylo 23 tys.);
2) budzet-rodow.csv, rodzaj najemnik: suma G w d120 ok. 3-4 mln (bylo 7.97 mln);
3) linia Skarbce: krolow z kiesa ponizej 20 tys. w d120 najwyzej 1 (bylo 4);
4) 'ludzie najemnikow AI' nie ponizej 2 000 (kontrola, ze B nie dusi kompanii jak w tescie C1);
5) wlasne druzyny krolow w d120 (budzet-rodow.csv: krol = rod z linii Skarbce) nie mniej niz w d60.

RYZYKA: (A) bogatsi krolowie podnosza mnoznik nagrody gry (DefaultMinorFactionsModel.cs:26-41 liczy zloto wodzow) - kompanie chetniej ida do tych samych krolestw, bez wplywu na zloto. (B) jesli marza 0.30 nie pokryje jedzenia, sprzetu i werbunku, kompanie beda powoli malec - pilnowac punktu 4; kiesy startowe ok. 240 tys. daja zapas. (C) kompania stoi do 28 dob w krolestwie, ktore zawarlo pokoj (na polowie kontraktu) - zgodne z regula 'w pokoju polowa'.


### O1-7 skad sie bierze brak zlota u panow: zloto przeszlo do kies ludu, sakiewek, kompanii i zwyciezcow

**Werdykt:** STROJENIE | **pewnosc:** srednia | **podwazenie - broni sie:** True

**Przyczyna:**

Linia 'Pieniadz swiata' d4 -> d364 (LOG :128970; razem 208.33 -> 206.03 mln, swiat zamkniety): skarbce krolestw 57.45 -> 26.34 mln (-31.1; regula 165: 1/360 zapasu ponad 0.5 mln na dobe - zasilek 117 tys./d na starcie, 33 tys./d w d364, wygasa sam), kasy miast 20.82 -> 10.86, notable 30.41 -> 22.53, karawany 3.88 -> 2.29, Bank 5.00 -> 2.83; glowy rodow razem bez zmian 56.91 -> 56.66 mln, ale w srodku wasale ok. -18 mln, kompanie najemne +17.7 mln (O1-4). Przybylo: kiesy ludu miast 7.17 -> 36.59 mln (+29.4; w d364 dalej +53.5 tys./d, 'lokata' 23.8 mln - lud nie ma czego kupic: linia 'Kiesa ludu' wsie 'bez towaru 500', zakupy 6.5% chcianych), sakiewki zolnierzy 1.16 -> 13.70 mln (+12.5, stoja od ok. d100), kiesy wsi 1.46 -> 10.19 mln (+8.7, stoja od ok. d90). Rozklad zlota rodow: mediana G 228.9 tys. (d7) -> 85.7 tys. (d364), gorne 10% rodow 20% -> 57% calosci, dolna polowa 19.0 -> 5.6 mln; 20 najbogatszych rodow ma 29.5 z 62.1 mln i 9.9 tys. ludzi. Dochod panow d364 (linia 'D staly (169c)'): ziemia 163 tys./d (podatek wsi 75, zawor miast 69, cla 15, zawor zamkow 4 - 33 zl na zamek), korona 435 tys./d (zwrot zoldu 217 + renty 159), jednorazowe (lup, okupy) 362 tys./d - te trafiaja do zwyciezcow. Sama ziemia (163 tys.) nie pokrywa nawet zoldu zalog (172 tys./d): cale wojsko polowe zyje z przelewow korony i z lupu.

**Poprawka:**

Nie do tego obszaru - przekazac do obszaru zloto/kiesa ludu (E5/B1): (1) lokata ludu miast +29.4 mln w roku i dalej +53.5 tys./d - to najwieksze miejsce, w ktorym pieniadz staje; kazde 10 tys. zl/d, ktore wroci do obiegu panow, to ok. 1-1.3 tys. ludzi w partiach (udzial zoldu 0.6-0.8, 6.2 zl na czlowieka); (2) zasilek ze skarbcow wygasa z reguly (zostalo 33 tys./d) - po nim wplywy koron to ok. 385 tys./d. W obszarze wojska nie dosypywac panom nic z niczego i nie podnosic pulapu 166. Odrzucone po rachunku: limit zalog do polowy pulapu w wojnie (26 rodow ma zold zalogi wyzszy niz caly pulap, 62 ponad polowe) - zdjalby 26.7 tys. zl/d, czyli tylko ok. 4.3 tys. ludzi polowych (zolnierz zalogi kosztuje 2.3 zl, polowy 6.2 zl) kosztem oslabienia twierdz i warunku zalogi renty 180.


**Podwazenie - uwagi:**

WERDYKT PODWAZENIA: rachunek "gdzie jest zloto" i werdykt STROJENIE bronia sie, ale z 5 poprawkami; jedna z nich to BLAD (zloto w nicosc), a trend wojska lepiej tlumaczy inna przyczyna niz odplyw zlota.

CO SIE ZGADZA (sprawdzone w logu rocznym Armoury-2026-10-13_02-15-21.log):
- Linie "Pieniadz swiata" :2068 (dzien 108840 = baza wniosku) i :128970 (d364): wszystkie kwoty wniosku co do setnej. Regula 165 = CrownIncome.cs:168-193 (Release = (zapas - 500 000)/360); suma zasilku w roku 26.0 mln (116 -> 33 tys./d).
- "D staly (169c)" :128911: ziemia 163 025, korona 434 813, jednorazowe 362 099 - zgodne. Zalogi: 26 rodow z zoldem zalogi ponad pulap, 62 ponad polowe, nadwyzka 27.4 tys./d, zold 2.32 / 6.22 zl - odrzucenie limitu zalog sluszne.
- Top 20: 29.48 mln i 9 872 ludzi - zgodne, ALE 10 z tych 20 to kompanie najemne (20.06 mln, 2 367 ludzi); wasali jest tam 10 (9.42 mln, 7 505 ludzi). Mediany 85.7 tys. nie odtworzylem (wszystkie rody AI 63.3 tys., z lennem 111.5 tys.).

CO SIE NIE BRONI:
1) "Swiat zamkniety" - nie: razem 208.40 -> 206.03 mln (-2.37; d1-120 +0.1, d121-240 -1.07, d241-364 -1.40; w d364 -14.0 tys./d). Z tego 2.005 mln to BLAD w 168: kazde zerwane wezwanie do wojny (:24932 16 963, :47526 869 737, :60938 1 004 403, :75474 666 428, :125291 1 001 224) nastepnej doby wraca z TA SAMA kwota jako "dlug portfela sprzed paczki: nowy" (:47881, :61289) i skarbiec placi ja w nicosc po 1/182 na dobe. Nowa kampania - dlugu sprzed paczki byc nie moze; w dobach ksiegowania suma swiata nie skoczyla, wiec nikt tego zlota nie dostal. Zaksiegowano 3.56 mln, zniszczono 2.0 mln, w kolejce 1.55 mln (9-14 tys./d, placone PRZED zwrotem zoldu i rentami).
2) Cytat "wsie bez towaru 500, zakupy 6.5%" dotyczy kies WSI, nie lokaty ludu miast. Dla miast dowod to: budzet po ofercie nadwyzki 1.15 -> 3.63 mln/d, a wydatek gry 289 -> 417 tys./d i stoi na 417 od d271; wydatek/budzet gry 43-51% caly rok - oferta nie kupuje nic.
3) "Lokata to najwieksze miejsce i dalej +53.5 tys./d" - to jedna doba. Okna 30 dob (zmiana kies): +190, +130, +126, +103, +96, +78, +97, +78, +65, +50, +44, +33 tys./d - gasnie razem z zasilkiem 165. Kompanie najemne: rowno ok. 1.6 mln na 30 dob caly rok (2.94 -> 20.60 mln, 11 kompanii, 2 494 ludzi); w d330-364 kompanie +1.65 mln, lud +1.24 mln. Na przyszlosc pierwszy zbiornik to kompanie.
4) Skad ubytek wasali (G z budzet-rodow.csv, bez najemnikow): d30 64.8 -> d120 48.6 -> d240 43.2 -> d270 42.0 -> d364 41.6 mln. d30-120 (-16.2): jednorazowe napelnienie sakiewek zolnierzy +10.2 i kies wsi +4.1, kompanie +4.9. d120-364 (-7.0): kompanie +12.6. Lokata ludu (+26.1 od d30) ma lustro w skarbcach (-28.2) w kazdym oknie. Od d270 zloto wasali STOI, a wojsko dalej spada (72.5 -> 61.3 tys.).
5) Rachunek "10 tys. zl/d = 1-1.3 tys. ludzi" to gorna granica: na pulapie jest 117 z 332 rodow (33% dochodu); 203 rody maja luz pulapu ok. 297 tys./d i partie na 53% limitu. Realnie 0.3-0.45 tys. ludzi na 10 tys. zl/d. "Wplywy koron po zasilku 385 tys." - log: 401 tys. (d364), srednio 445 (d331-360).

BRAKUJACA PRZYCZYNA KIES LUDU: danina wojenna (Settings.cs:948-950, KingdomTreasury.cs:398-412) bierze nadwyzke kas kupcow (24 036/d) i kies wsi (192 766/d z 10.19 mln = 1.9% dziennie - dlatego kiesy wsi stoja od d90), a kies ludu miast nie siega, bo E5/B1 trzyma je poza Gold osady: lud z 36.6 mln placi tylko 12 925/d (184). Lokata 23.78 mln = 24 dni wydatkow W (projekt: "lokata < 5 dni", "zbiornik bez wyjscia: zaden" - PROJEKT-KIESA-LUDU-2026-10-09.md:231, :491; pulap 30 dni porzucony w PLAN-KODU-E5B1.md:41-42); 42% w 10 miastach, 82% w 30; 42 miasta maja ponizej 10 dni.

INNA PRZYCZYNA SPADKU WOJSKA (lepiej tlumaczy caly rok): linia "Zold konnych" :21522 (d61) 477 736 zl / 98 657 ludzi = 4.84 zl; :128965 (d364) 426 340 / 66 381 = 6.42 zl. Rachunek zoldu -10.8%, ludzi -32.7%: 1/3 spadku (ok. -10.6 tys.) to mniejszy budzet, 2/3 (ok. -21.7 tys.) to drozszy czlowiek (awanse). To samo w biegu 728 dob przy nadmiarze zlota: 5.46 (d121) -> 6.35 (d361) -> 6.70 (d481) -> 6.48 (d721), czyli plateau ok. 6.5-6.7. Mechanizm: zwolnienia 166 od najnizszego tieru - 129 063 ludzi z partii w roku (286-543/d od d61) przy 290 633 zwerbowanych i 132 029 zabitych; rody zwalniajace 12 -> 59.

DOKAD ZMIERZA SWIAT: nie stabilizuje sie. Stary swiat (bieg 728, etap 4) drukowal ok. 460 tys./d (suma 186 -> 353 mln w roku) i z tego bylo 113-121 tys. wojska; zamkniecie w etapie 5 to decyzja Jeffa (ZAMIERZONE). 95 tys. ludzi po 6.42 zl = 610 tys./d zoldu wobec 426 dzis; oba zbiorniki razem daja najwyzej ok. 83 tys./d. W roku 2 zasilek 165 spada 33 -> ok. 12 tys./d, kompanie biora dalej ok. 50 tys./d - zloto wasali znow zacznie spadac (ponad rezerwe R zostalo 14.4 mln, 161 rodow juz ponizej R).

**Poprawka ostateczna:**

W OBSZARZE WOJSKA DLA O1-7: bez zmiany kodu (zgoda z wnioskiem: nic z niczego, pulap 166 bez zmian, zalog nie ciac). Do obszaru zloto przekazac TRZY rzeczy w tej kolejnosci, nie jedna:

A) BLAD, wielkosc S - wezwania do wojny 168.
Plik: C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int\Armoury\src\CrownCallToWar.cs, metoda Daily.
- :212-216 (zerwanie z braku wplywow): po beh.EndCallToWarAgreement sprawdzic, czy wpis dalej jest w _agr; jesli tak - samemu oddac portfelowi reszte (calling.CallToWarWallet += a.Left) i dopiero wtedy _agr.Remove(a). Dzis usuniecie "na wypadek" zostawia portfel na minusie. Dlaczego EndPostfix (:121-136) nie przywraca portfela - nie ustalone (gra w EndCallToWarAgreement portfeli nie rusza; mozliwe wklejenie krotkiej metody przez JIT); poprawka dziala niezaleznie od przyczyny.
- :161-166 (wykrywanie "dlugu sprzed paczki"): tylko raz, przy pierwszej dobie po wczytaniu zapisu bez klucza 168; w kampanii zaczetej z paczka - nigdy.
Bez nowego klucza. Skutek: 2.0 mln/rok (9-14 tys./d pod koniec) zostaje we wplywach dnia koron na zwrot zoldu i renty; suma swiata przestaje spadac. Ryzyko male (ta sama arytmetyka co EndPostfix; sprawdzic, ze nie dodaje reszty dwa razy, gdy postfiks jednak zadziala - stad warunek "wpis dalej w _agr"). Test: "splacone dzis ze skarbcow w nicosc" = 0 kazdej doby, "nowy" = 0, zloto swiata na 28 dob w progu 0.1%.

B) LUKA PROJEKTU E5/B1, wielkosc M - danina wojenna takze z nadwyzki kies ludu miast.
Pliki: ...\Armoury\src\KingdomTreasury.cs (Levies, :391-412) i ...\Armoury\src\TownFolk.Live.cs (TownLive krok 5, :809-829, gdzie liczona jest nadwyzka ov = kiesa W - zapas dni x potrzeba; R i P ponad 21 / 30 dni).
Regula: w wojnie krolestwa wlasciciela, osada nieoblezona - z nadwyzki kiesy ponad zapas dni klasy idzie do skarbca udzial dzienny; wplyw dnia 165 (LeviesToday), licznik w "Korona: danina wojenna" i w kontroli "Kiesa ludu" (wydatki), przelew w "Obieg".
Klucz: nowy LaySubsidyFolkShare = 0.01 (tyle co LaySubsidyTownShare; z polityka War Tax x LaySubsidyWarTaxMultiplier 2). Po dodaniu gen_mcm.py.
Rachunek: stan ustalony lokaty = nadwyzka dzienna ludu / stawka. Nadwyzka 33-100 tys./d -> lokata 3.3-10 mln = 3-10 dni wydatkow W (0.99 mln/d) zamiast 23.8 mln i rosnie; przy War Tax 1.7-5 mln (prog projektu "ponizej 5 dni"). Do koron stale 33-100 tys./d (tyle, ile dzis staje w kiesach), dalej zwrotem zoldu i rentami 180 do panow. Wojsko: realnie +1 do +3 tys. ludzi (tylko rody na pulapie), gorna granica 4-11 tys. - samo NIE da 85 tys.
Zamknieta ekonomia: przelew lud -> skarbiec, nic z niczego; ta sama zasada, ktora Jeff przyjal 04.10 (pietnastka i dziesiecina) i 09.10 C (podatek wojenny zamiast mniejszego wojska).
Ryzyko: (1) biedne miasta nie placa (42 miasta ponizej zapasu; "stac jedzenie" bez zmian); (2) oferta nadwyzki maleje - i tak nie kupuje; (3) stawka 184 sama spadnie, bo korona ma wiecej; (4) w pokoju daniny nie ma - lokata krolestw w pokoju zostaje (jak u wsi); (5) koszt ponizej 0.1 ms/d. Sprawdzic biegiem 120 dob: lokata, glowy ponizej 5 000, wojsko.
Uwaga: ta poprawka nie byla liczona na symulacji obiegu - liczby stanu ustalonego sa z rachunku, nie z biegu.

C) Drugi zbiornik - kompanie najemne (O1-4, MercContract.cs:111-119): pulap = zold z umowy, kontrakt 1.3 x zold, bez dworu (niewydany przydzial sprzetu zostaje w kiesie: srednio 22.1 tys./d). 20.6 mln u 2.5 tys. ludzi, ok. 50 tys./d bez hamulca. Wyjscie ma dac O1-4; bez niego B nie zatrzyma odplywu od wasali.

POPRAWKI DO TRESCI WNIOSKU: przelicznik 0.3-0.45 tys. ludzi na 10 tys. zl/d (gorna granica 1-1.3); "swiat zamkniety" -> ubytek 2.37 mln/rok, w tym 2.0 mln z A; cel 95-115 tys. wojska byl strojony na swiecie drukujacym ok. 460 tys./d - w zamknietym, przy 6.4 zl na czlowieka, wymaga 610 tys./d zoldu partii (jest 426). To decyzja dla Jeffa w obszarze wojska: przyjac ok. 60-70 tys. albo obnizyc zold na czlowieka.

BRAKUJACE POMIARY: (1) w 168 stan portfela wzywajacego przed i po zerwaniu; (2) w "Kiesa ludu" ile oferty nadwyzki faktycznie kupiono (dzis logowana jest tylko oferta); (3) bieg 120 dob po A i B.


### O1-1 GLOWNE: wojsko lordow topnieje przez pieniadz (pulap zoldu 166), nie przez ksiege ludzi

**Werdykt:** ZAMIERZONE | **pewnosc:** wysoka

**Przyczyna:**

Zrodla: LOG = D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log (linie 'Ludzie:' :29692 d84, :129031 d364; 'Budzet rodow (166)' :29221, :128549; 'Zold'; 'Skarbce'), CSV = ...\Logs\2026-10-13_02-15-21\budzet-rodow.csv i balans-krolestw.csv; SRC = C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int\Armoury\src. TREND (okna 28 dob; partie rodow / zold partii na dobe / zl na czlowieka / sredni tier / D razem / pulap razem / zwolnieni 166 z partii na dobe / rody ponad pulapem partii): d57-84 99 668 / 460 tys. / 4.62 / 2.43 / 691 tys. / 1 112 tys. / 215 / 31; d85-112 94 467 / 464 / 4.91 / 2.60 / 654 / 1 009 / 452 / 68; d141-168 85 814 / 453 / 5.28 / 2.78 / 716 / 1 039 / 310 / 71; d197-224 79 806 / 439 / 5.50 / 2.89 / 681 / 972 / 394 / 85; d253-280 76 162 / 430 / 5.65 / 2.93 / 658 / 934 / 459 / 95; d309-336 72 537 / 413 / 5.69 / 2.94 / 641 / 907 / 465 / 94; d337-364 68 698 / 393 / 5.73 / 2.96 / 629 / 891 / 520 / 105. Wedlug linii 'Skarbce' (krolestwa w wojnie, okna 30 dob): 96.1 (d61-90), 90.5, 83.5, 83.5, 80.5, 73.1, 72.8, 69.5, 69.2, 64.6 tys. (d331-360); spadek ostatnich 112 dob ok. -67/dobe - plateau NIE MA. LUDZI NIE BRAKUJE I NIE GINA Z KSIEGI: ludzie zywi 52 170 805 (d2) -> 52 029 433 (d364) = -141 tys. (-0.27%; polegli od siewu 232 828, narodziny netto +107 996); pod bronia 1.01% mezczyzn; hamulec wolnych rak czynny w 5-8 z 227 regionow (Sarnor, Ibben, Crownlands, Dragonstone - Sarnor 1987 -> 665, Ibben 1742 -> 709, razem ok. -2.3 tys.); kontrola ksiegi 'zmiana stanu - (wejscia - wyjscia) = 0.00'. BILANS PARTII RODOW d85-364 (na dobe): zwerbowani +642, nowe druzyny z szablonu +450, z zalog netto +139; zabici -411, ZWOLNIENI PRZEZ BUDZET 166 -412, dezercja -31, rozbici/pojmani/rozwiazane -497; razem -121. Zwolnienia budzetu sa rowne stratom w bitwach. ROZKLAD SPADKU -34.0 tys. (srednia d57-84 97 139 -> d337-364 63 129, csv; skrypt grp.py): (a) ok. -14 tys. (41%) ten sam pieniadz, drozszy zolnierz - zold partii spadl tylko o 14.5% (459.5 -> 393.0 tys./d), a zold na czlowieka wzrosl 4.73 -> 6.23 zl (+32%), tier 2.43 -> 2.96 (takze u rodow bez klopotow +21%; w biegu 728 dob na W28 tak samo +22%); tier stoi od ok. d225; (b) ok. -6 tys. (18%) kolejnosc zwolnien - ustalenie O1-3; (c) ok. -7 tys. netto (21%) koniec skrzyn wojennych: pulap = 0.60 D + 0.8 (G-R)/45 (SRC\ClanBudget.cs:181-200), a wojna w tym swiecie nie konczy sie (linia 166: w trybie wojny 293-310 z 312 rodow przez caly rok; wojny fabularne ROT 'EnforceWars') - nadwyzka G ponad rezerwe R wypala sie raz, w pierwszych 100-180 dobach; rody z lennem z G < R: 2 (d56), 17 (d84), 46 (d168), 78 (d252), 89 (d364); (d) ok. -7 tys. (20%) pulapka rent korony - ustalenie O1-2. Partie rozjezdzaja sie na trzy grupy (rody z lennem, d57-84 -> d337-364): 57 rodow bez renty korony 24 264 -> 5 785 ludzi; 64 rody z renta na pulapie zoldu 28 185 -> 14 832; 77 rodow z luzem 38 813 -> 36 604 (trzon stabilny, G 21.8 -> 25.3 mln). DO CZEGO ZMIERZA (punkt staly pulapu bez skrzyni, skrypt eq2.py; ziemia 161 tys./d + renty 204 tys./d, zalogi 173 tys./d placone pierwsze): bez renty 0.7-2.0 tys., na pulapie 3.7-8.2 tys., trzon ok. 36.5 tys., bez lenna i najemnicy ok. 5.9 tys. = ok. 47-53 tys. w partiach lordow za ok. 150-250 dob przy dzisiejszym tempie; zalogi stoja na ok. 74 tys. POROWNANIE Z BIEGIEM 728 DOB (W28, kod sprzed etapu 4; C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\kopia-728\Armoury-2026-10-10_20-58-02.log): partie rodow 101.6 tys. (d57-112) -> 121.8 tys. (d337-392), spadek dopiero w roku 2 przy pokoju. Tam swiat nie byl zamkniety: pieniadz swiata 217.4 mln (d84) -> 354.7 mln (d364), czyli +0.6 mln/dobe z niczego; ziemia panow 898 tys./d (zawor miast 759 tys.) wobec 163 tys./d dzis (zawor miast 69 tys.); D razem 1.0-1.36 mln/d wobec 0.63 mln; ludnosc 56 -> 88 mln. Dzis pieniadz swiata stoi (208.3 -> 206.0 mln). Prog 95-115 tys. byl wiec mierzony w dobie 120 na swiecie z dosypka, na swiezym tanim zolnierzu i na skrzyniach wojennych.

**Poprawka:**

Parametrow budzetu 166 (0.60 D, prog 1.10, 15%/d) nie ruszac - zold jest placony w calosci (zalegly zold 0.54% zlota), wyzszy pulap tylko szybciej oprozni kiesy. Do zrobienia sa trzy rzeczy z osobnych ustalen: O1-2 (pulapka rent, S), O1-3 (kolejnosc zwolnien, S), O1-4 (kompanie najemne, M). Oczekiwany skutek razem: zamiast zejscia do ok. 50 tys. poziom ok. 72-82 tys. w dobie 364 (62.5 + 3..7 + 4..8 + 2..5); pasma 95-115 tys. w zamknietej ekonomii NIE obiecywac. NARZEDZIE (S, C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int\tools\sprawdz_logi.py, okolice :14081-14157): obok liczby glow dopisac wiersze INFO 'zold partii rodow (28 dob)', 'zold na czlowieka i sredni tier partii', 'rody z lennem z wypelnieniem druzyn > 60%', 'rody z lennem bez renty korony %', 'rody z lennem z G < R' - sam licznik glow myli tanszego rekruta z weteranem (w zoldzie wojsko spadlo o 14.5%, w glowach o 31-35%). Prog glow dla doby 364 ustalic dopiero po biegu rocznym z poprawkami O1-2 i O1-3 (propozycja robocza: podloga 70 tys., cel 75-90 tys.).


### O1-5 ludzie: brak przecieku; byli zolnierze wracaja do domu, czesc stoi w karczmach i idzie w bandy

**Werdykt:** ZAMIERZONE | **pewnosc:** wysoka

**Przyczyna:**

Dokad poszlo 31 tys. ludzi z partii lordow (okna d57-84 -> d337-364, linie 'Ludzie:', 'Ludzie (ksiega)', 'Pochodzenie (E4/7)', 'Weterani (E4/10)'): zalogi 73.6 -> 74.1 tys. (bez zmian), milicje 63.5 -> 74.6 tys. (+11.1), bandy 8.3 -> 20.0 tys. (+11.7; 945 band), pula wyrzutkow 1.4 -> 9.1 tys. (+7.7), weterani w karczmach 0.8 -> 9.6 tys. (+8.8; d364 10 094), jency szeregowi 2.7 tys. bez zmian (w lochach ok. 30), niewolni 28.7 -> 43.1 tys., Straz 7.6 -> 7.9 tys., Inni 1.8 -> 4.8 tys. Zwolnieni przez budzet (128 660 w roku) wracaja 'do domu z pochodzeniem' (637 tys. powrotow w roku wszystkimi drogami). Niewola lordow nie blokuje: w niewoli 30-55 naraz, mediana 3 doby, ponad 60 dob: 0; pojman 7.0 na rod w roku. Dezercja partii rodow 8 931 w roku (31/d) - 7% zwolnien budzetu. Liczba partii lordow 659 -> 617; krolestw 29 -> 31 (dwa bunty), zadne nie upadlo, w wojnie 26-28 przez caly rok, zdobytych twierdz 25; rody z lennem 218 -> 198. Pula weteranow rosnie (wejscia 37 176, najem 25 177, powroty 2 214 w roku), bo najem z karczmy w grze zalezy od kwadratu zlota wodza (RecruitmentCampaignBehavior.cs:470-493 w cs_full) - biedni panowie nie najmuja, bogaci maja pelne druzyny; po 120 dobach weteran wraca do domu, wiec pula ma sufit. Osobno (inny obszar): konto UCHODZCY 0 -> 176 816 (nowi ok. 530/d, powroty ok. 23/d) rosnie bez plateau.

**Poprawka:**

Bez poprawki w ksiedze ludzi. Do obserwacji w nastepnym biegu rocznym: bandy + wyrzutki (dzis 29.9 tys., +63/d) - rosna, gdy slabna partie lordow (sprzezenie: mniej panow w polu = wiecej band = mniej utargu wsi = mniejsza ziemia panow); pula weteranow powinna stanac na ok. 12-16 tys. po wejsciu powrotow po 120 dobach (jesli nie stanie - osobne ustalenie). Uchodzcy - do obszaru wsi/spustoszenia, nie do wojska.


### O1-6 nabor: ochotnicy stoja u notabli, panowie ich nie biora (brak pomiaru wprost)

**Werdykt:** BRAK_DANYCH | **pewnosc:** srednia

**Przyczyna:**

Posrednio: linia 'Pobor' liczy tylko losowania PUSTYCH miejsc (SRC\Levy.cs:123 - miejsce zajete wychodzi przed licznikiem). Doba startu 20 801 losowan (notabli ok. 3 456 x 6 miejsc = 20 736), doba 2: 7 727, potem 4 000 (d8) -> 2 480 (d60) -> 1 797 (d90) -> 1 750-2 100 do konca roku (LOG :29384 d84, :128705 d364). Czyli pustych miejsc jest ok. 1.8-2.0 tys. z 12.5-20.8 tys. (dolna granica, gdyby doba startu liczyla dwa przejscia) - u notabli stoi ok. 10-19 tys. ochotnikow, a werbunek panow od notabli to 508-600/d (bylo 1 679/d w d1-28). Okno zniw (d197-252) zbilo chec x0.37 -> x0.28, a werbunek spadl tylko o ok. 15% - podaz nie wiaze. Wiaze strona pana: limit zoldu partii z budzetu 166 (gra: CheckRecruiting / RecruitVolunteersFromNotable w C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\dec32\cs_full\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs:415-562 - IsWageLimitExceeded, GetAvailableWageBudget, jeden rekrut na notabla na godzine, szansa 1.1 - wypelnienie). Rody z luzem trzymaja wypelnienie 52-58% przez caly rok (bez trendu) - to zwykla rownowaga werbunku i strat AI, nie przyczyna spadku. Hamulec wolnych rak (decyzja Jeffa 11.10) odmawia 350-850 losowan/d w 6-15 regionach (Sarnor zawsze) - skutek logiczny, ok. -2.3 tys. ludzi w Sarnorze i Ibben.

**Poprawka:**

Lista brakujacych pomiarow (sam log, wielkosc S, przy okazji poprawek O1-2 / O1-3): (1) w linii 'Pobor' albo 'Pule ochotnikow (171)': ilu ochotnikow stoi u notabli razem i w miejscach dostepnych dla pana tego krolestwa (VolunteerModel.MaximumIndexHeroCanRecruitFromHero - BK liczy to z praw majatkowych), wedlug tieru i dob czekania; (2) w linii 166: postoje lordow w osadzie z ochotnikami, przy ktorych werbunek zatrzymal limit zoldu partii (dzis licznik jest tylko dla Strazy); (3) w linii 'Renty korony (180)' i w csv: na rod - najwyzsze wypelnienie druzyny i doby w armii, zeby pulapke O1-2 bylo widac bez liczenia z csv; (4) liczba armii na krolestwo na dobe; (5) tier zwalnianych 166 (O1-3); (6) licznik przelewu krol -> kompania przy najmie (O1-4).


## Obszar: zelazo

**Streszczenie:** Zelaza na swiecie nie brakuje: po roku lezy go 14,5 tys. ladunkow, czyli zapas na trzy miesiace, a mimo to 40-60 miast z 97 nie ma go wcale. Kopalnie woza zelazo tylko do miast w zasiegu 4 dni drogi i tam ono zostaje - w czterech miastach (Barrowton, Pyke, Lordsport, Ten Towers) lezy dwie trzecie zelaza wszystkich miast, 3,6 tys. utknelo w zamkach (woz uciekajacy przed wrogiem do zamku sprzedaje tam wszystko, a z zamku nic nie wraca), a 2,6 tys. woza karawany, ktore dojezdzaja tylko tam, gdzie da sie dojechac ladem. Dalej zelazo mialo jechac na zamowienie miasta, ale zamowic mozna tylko u karawany, ktora akurat stoi w miescie z zelazem - a odkad karawany stoja godzine zamiast trzech dni, takich zamowien jest 1 dziennie zamiast 13. Wyspy i miasta za morzem nie dostawaly zelaza nigdy, takze w starszych wersjach - stary bieg dwuletni mial stale 34-47 miast bez rudy, wiec wynik 27 z poprzedniego wgrania byl jednym dobrym miesiacem, nie norma. Od polowy roku doszla druga rzecz: regulator, ktory pilnuje, zeby wsie nie robily wiecej, niz swiat zuzywa, widzi wielki nieruszany zapas i przykreca wszystkie kopalnie (do 61%), wiec zelaza zaczyna brakowac takze tam, gdzie dotad dochodzilo - miast bez zelaza przybywa z 35 do 55-61 i samo sie to nie zatrzyma. Proponuje, zeby miasto bez zelaza zamawialo je wozem kupcow z najblizszego miasta z nadwyzka, ladem albo morzem, do ok. 10 dni drogi, za pieniadze z kasy miasta (nic z niczego) - tak jak juz jezdzi bron i drewno do dymarek - oraz zatkac przeciek do zamkow. Wedlug rachunku zostanie ok. 8-12 miast bez zelaza, gra nie zwolni (kilka milisekund na dobe), ale to trzeba potwierdzic biegiem. Jedna rzecz wymaga Twojego slowa: taki woz jedzie z czasem drogi, ale nie jest druzyna na mapie i bandyci go nie rozbija, a kiedys chciales, zeby surowce wozily prawdziwe karawany - wariant z prawdziwymi karawanami nie obejmie jednak wysp.


### O2-1 GLOWNE: brak ogniwa miasto z nadwyzka -> miasto bez zelaza (kontrakty 174.2 martwe, zasieg 200, wyspy bez konwoju)

**Werdykt:** BLAD | **pewnosc:** wysoka | **podwazenie - broni sie:** False

**Przyczyna:**

ZELAZA NIE BRAKUJE - NIE DOCIERA. Bilans roku (linie 'Zelazo (lupy) (214)' i 'Towary: iron', 364 doby, log Armoury-2026-10-13_02-15-21): lupy wytopione 75 640 (208/d) + zlom 3 872 = podaz 79,5 tys.; zuzycie 66 154 (182/d = 83%); zapas swiata 1 035 -> 14 517 (+37/d). Doba 364 (L128767): miasta 6 748, zamki 3 654, karawany 2 582, wozy wsi 756, wsie 643, lordowie 110. Tego samego dnia (L128843): 'ruda: bez towaru 55, brakuje 617, w jukach karawan 2582' - same karawany woza 4x wiecej, niz brakuje.
GDZIE LEZY (linia 'Miasta bez rudy i strzal (174b)', co 5 dob): Barrowton 278 (d34) -> 1 396 (d184) -> 2 442 (d364), Pyke 177 (d94) -> 1 572, Lordsport 892, Ten Towers 294-866; srednio d91-364 te 4 miasta trzymaja 3 877 z ok. 6 100 na polkach wszystkich miast (64%). W starym biegu 728 dob (kopia-728, kod etapu 4, kontrakty czynne 11-17/d) po dwoch latach to samo: Pyke 5 631, Barrowton 3 403, Lordsport 2 801, Ten Towers 2 632, Stonehelm 1 735 przy 33-53 miastach bez rudy (okna 91 dob: 40,1 / 34,1 / 35,7 / 40,9 / 47,3 / 33,9 / 44,3 / 36,8) - problem jest stary i sam sie nie stabilizuje nizej niz ok. 34; 26,7 z W33 to jedno okno 30 dob, nie poziom dlugiego biegu.
LANCUCH (srednie okien 30 dob, d1-30 ... d331-360):
(1) lupy 182 / 224 / 239 / 244 / 254 / 264 / 230 / 179 / 186 / 182 / 159 / 158 na dobe; bez wegla 36% / 19% / 12% / 9% / 5,5% / 2,6% / potem 2,2-3,5%.
(2) wozy wsi dowoza 183/d (66 607 w roku), kopalnianych wozow 4,6/d, z tego do miasta bez zelaza 1,6/d (35%); woz wybiera tylko miasta do 250 drogi (Settings.cs:627), wiec Stoney Shore ma do wyboru Barrowton i Flint's Finger, kopalnie Wysp - tylko wyspy. Utarg za lupe 34,8 -> 12,5 d (d181-210) przy wartosci 38 d; miasta bez zelaza placa 143-187 d za pierwsza sztuke.
(3a) karawany: 440-515 sztuk, 86-125 wjazdow/d; zelazo wiezie 47-122 karawan (2 200-2 800 lup od d75); kupuja 53/d, sprzedaja 34,5/d; wjazdow z zelazem 9-33/d, w tym do miasta z brakiem 2-9 (d91-240: 12%). Cel jazdy wybiera BK, nasza regula go nie rusza (CaravanBulk.cs:73-76), sprzedaz tylko miastu z brakiem (:601).
(3b) kontrakty 174: zawarto 322 w roku (0,9/d; W33 12,7/d; bieg 728: 15/d), zelaza 1 586 lup (4,4/d = 2% podazy). Powody 'bez kontraktu' na dobe (wszystkie 7 surowcow, d91-364): brak karawany w zrodle 128, brak zrodla w zasiegu 186, bez drogi 123 [wyspa bez portu 26, lad ponad zasieg 8, morze poza zasiegiem 20, brak konwoju w porcie zrodla 70]; brak kiesy/miejsca 2,7; bez zysku 6,4. Kod: przewoznikiem moze byc tylko karawana STOJACA w zrodle w chwili taktu doby (MaterialOrders.cs:494-510, :544, :586), a po E8/G1 stoi ich srednio 21 (Z33: 355; postoj mediana 1,4 h, L128735); zelazo ma zasieg 200 zamiast 600 (MaterialOrders.cs:466, Settings.cs:1487); morzem tylko konwoj stojacy w porcie (:588). Zloto nie jest przeszkoda (kasy miast 10,86 mln, 2,63 mln ponad zapasem w 79 miastach).
(3c) hurt kupcow wozem R3 (TradeCarts) zelaza NIE wozi - tylko bron, zbroje, uprzaz i konie (SupplyDemand.cs:564-679; 1 650-3 900 szt./d).
(4) zuzycie roku: warsztaty zbrojne 128,6/d, narzedzia (kuznie + rzemieslnicy BK) 31/d, strzelarze 9,7/d, inne 6,5/d. Ponizej zapasu docelowego 44-76 miast, pustych 35-64, do zapasu brakuje 349-694 lup; cykle 'brak surowca [ruda' 552/d w roku (d91+ 477/d) przy 1 146 sztukach zrobionych na dobe. Niezaspokojony popyt szacuje na 60-100 lup/d (477 cykli x 0,12 lupy na sztuke; to szacunek, nie pomiar).
GEOGRAFIA (55 probek d91-364): z 40,1 miast bez zelaza najblizsze zrodlo z nadwyzka >= 8 lup lezy w linii prostej do 200: 14,7 miasta; 200-400: 15,4; 400-600: 7,7; 600-1000: 2,0; dalej 0,2. Czyli 63% zamowien zelaza odpada na zasiegu 200, a reszta na braku karawany albo statku.
Porownanie biegow d91-120 (ten sam parser): rok 37,4 / test koncowy 44,3 / limit 3: 45,1 / bieg 2: 30,9 / bieg 1: 46,4 - rok i test koncowy to TEN SAM kod, wiec rozrzut miedzy biegami to +-4-7 miast; liczba karawan (439-1 562) nie jest dzwignia.

**Poprawka:**

A. KONTRAKT WOZEM KUPCOW (M; klucz TownMaterialOrderByCart = true; NK nie jest potrzebna, ale Jeff i tak gra nowa). Plik Armoury\src\MaterialOrders.cs, Order(): gdy zadna karawana nie dala kontraktu (bestSrc == null), drugi przebieg po tych samych zrodlach bez wymogu przewoznika: zrodlo = miasto z nadwyzka ponad SourceKeepDay, niewrogie, nieoblezone, droga TradeCarts.Road(src, dest) (TradeCarts.cs:126 - ladem, wyspa ladem i morzem) <= TownMaterialOrderRange 600; dla 'iron' przy Bloomery.On zasieg 600 zamiast 200 takze w starej sciezce (:466). Wygrywa najwieksza marza na kg jak dotad (:575). Zakup sztuka po sztuce z polki zrodla (ShelfBuy.Take - wzor SupplyDemand.cs:673), placi kasa miasta-celu ponad TownPurse.BuyFloor -> kasa miasta-zrodla; towar jedzie TradeCarts.Ship (:149) i wchodzi na polke celu po czasie drogi (predkosc wozu wsi, do ok. 10 dob); cel oblezony - czeka, 30 dob - wraca ze zwrotem (TradeCarts.Arrivals/Back). Bez oplaty frachtu, jak R3 i wozy dymarek F4a. InTransit (:230) ma doliczac ladunek w wozach, inaczej miasto zamowi dwa razy. Regulator wsi liczy wozy sam (HarvestValve.cs:340). Ksiegi 'Towary' i 'Zelazo (214)' potrzebuja posiadacza 'wozy kupcow' (GoodsLedger.cs, OreLedger.cs), linia 'Kontrakty surowca (174)' - licznikow 'wozem zawarto / dojechalo / w drodze'. Jedna regula dla wszystkich 7 surowcow (skutek glownie zelazo, skory surowe, welna - tam sa zrodla z nadwyzka).
RACHUNEK: w zasiegu 600 (linia prosta) zrodlo ma 37,8 z 40,1 miast bez zelaza; droga jest dluzsza od prostej, wiec licze 30-35. Zostaje ok. 8-12 miast (przedzial 5-15; cel projektu <= 15) - to rachunek, do potwierdzenia biegiem 120 i 364 dob. Podaz wystarcza: dzis odklada sie 37 lup/d, regulator zdjal w roku 14,8 tys. (40/d), razem ok. 77/d wobec brakujacych 60-100/d; nadwyzki zrodel z listy 3 500-5 700 lup wobec braku 349-694.
CZAS DOBY: MaterialOrders.Order dzis 10,5 ms na 481 wywolan (linia 'Koszt 171-174'); drugi przebieg dla ok. 130-150 zamowien + przyjazdy = +2-5 ms z 13 000 ms doby. Zadnych nowych partii na mapie.
RYZYKO: (1) KIERUNEK DO POTWIERDZENIA PRZEZ JEFFA - 05.10 chcial, zeby surowce wozily 'prawdziwe partie na mapie, nie niewidzialny przerzut'; woz z czasem drogi jest juz przyjety dla broni (R3) i drewna dymarek (F4a), ale bandyci go nie rozbija. Wariant z prawdziwa karawana (zamowienie czeka w zrodle na pierwsza wjezdzajaca karawane) nie obejmie wysp - 'brak konwoju w porcie zrodla' 70/d - wiec nie spelnia 100% przypadkow. (2) Karawany straca czesc zarobku na zelazie - patrzec na kiesy karawan (G1-5c). (3) Wojna zrodlo-cel blokuje dostawe (Zelazne Wyspy z ladem) - skutek logiczny, zostaje. (4) Bilans ksiag musi zostac zamkniety (bez wyjasnienia 0).


**Podwazenie - uwagi:**

WERDYKT PODWAZENIA: wniosek broni sie tylko w czesci. Mechanizm "nadwyzka nie wyjezdza z miasta-zrodla" jest prawdziwy i poprawka A jest potrzebna, ale (1) to nie on robi trend roku, (2) rachunek "zostaje 8-12 miast" jest obalony, (3) poprawka A w podanym ksztalcie nie obejmuje polowy problemu (Essos, zamki, regulator). Pliki pomocnicze: ...\scratchpad\triaz3\podwazenie-zelazo\ (a1.py, a2.py, g2.py, g3.py, geo.json, rok.json, bieg1/bieg2/fin1/cap3.json).

CO SPRAWDZILEM I SIE ZGADZA (log Armoury-2026-10-13_02-15-21): lupy 75 640 + zlom 3 872; zapas 1 035 (L473) -> 14 517 (L128767: miasta 6 748, zamki 3 654, karawany 2 582); kontrakty 322 w roku, zelaza 1 586 lup; L128843 (brakuje 617, w jukach 2 582); L128735 postoj mediana 1,4 h; L128956 Barrowton 2 442 / Pyke 1 572 / Lordsport 892 / Ten Towers 294; te 4 miasta srednio 3 877 lup (d91-364); 25,0 z 40,1 miast bez zelaza nie ma zadnej nadwyzki w 200; koszt Order 10,5 ms/481 (L128731). Kod: MaterialOrders.cs:494-510, :544, :586-588 (przewoznik musi stac w zrodle), :466 + Settings.cs:1487 (ruda 200), wzor R3 SupplyDemand.cs:663-683 i TradeCarts.cs:126/:149 - istnieja.

CO SIE NIE BRONI:
1. "KONTRAKTY MARTWE" NIE SA PRZYCZYNA POZIOMU. Bieg 1 (kontrakty zywe: 10,6-13,1/d, zelaza 51-55 lup/d = 30% produkcji, 230-325 karawan stoi w miastach) mial d91-120 46,4 miasta bez zelaza - najgorzej ze wszystkich; bieg 2 (kontrakty 1,7-2,3/d) 30,9; rok 37,4; test koncowy 44,3; limit 3: 45,1. Bieg 728 (kontrakty 11-17/d): 34-47 miast i zapas 24 900. Wniosek: przywrocenie kontraktow w starym ksztalcie (takze tanszy wariant "karawana wjezdzajaca", G1-6b z TRIAZ-2) niczego nie zmieni - nie wybierac go.
2. TREND DRUGIEJ POLOWY ROKU ROBI REGULATOR WSI (E6/W3), nie brak ogniwa. Decyzje dla iron: L59488 d168 x1,00, stos 56 dni zuzycia; L69401 d196 x1,00 -> x0,73 (65 dni); L79360 d224 x0,72; L109252 d308 -> x0,61 (86 dni); L119181 d336 x0,61, zuzycie/produkcja 0,90, stos 95 dni. Produkcja lup/d oknami 30 dob: 182/224/239/244/254/264/230/179/186/182/159/158; zuzycie okna 56 dob 11 362 -> 8 404 (203 -> 150/d); miast bez zelaza 63,7/54,0/41,6/37,4/37,0/35,9/35,0/38,5/39,0/46,3/49,7/51,1, a d361-365 61,5. Kod: spis stosu liczy wszystko - polki miast, ZAMKOW, wsi, schowki, kazda partie, wozy (HarvestValve.cs:323-345); ciecie gdy zuzycie < 90% produkcji i stos > 60 dni (:453), w gore dopiero przy stosie < 30 dni (:462); mnoznik jeden na towar dla calego swiata (MaterialLaw.cs:220-224). Zelazo, ktore lezy tam, skad nie wyjezdza, regulator bierze za nadmiar i tnie wszystkie 26 kopaln, takze te przy pustych miastach; mniej zelaza -> mniejsze zuzycie -> "stos w dniach" rosnie 56 -> 95 -> kolejne ciecie. To petla. Swiat NIE stabilizuje sie przy 34-47 (to wynik biegu 728 na kodzie etapu 4, bez regulatora) - zmierza do mnoznika ok. 0,55-0,6 i 55-65 miast bez zelaza (rachunek z ostatnich decyzji, nie pomiar). Agent policzyl "regulator zdjal 14,8 tys." jako rezerwe podazy.
3. W ESSOS ZELAZA NAPRAWDE BRAKUJE. Podzial wedlug ROT-Map settlements.xml (x < 800 = Westeros: 60 miast, 18 kopaln; Essos: 37 miast, 8 kopaln; hearth kopaln 71% / 29%). Miast bez zelaza oknami 30 dob od d91: Essos 21,3/20,7/21,5/21,2/20,8/23,7/21,5/22,5/24,7 (plasko 56-67%, takze przy pelnej produkcji x1,00); Westeros 15,2/15,3/14,2/12,7/16,0/15,3/22,5/23,7/25,7 - caly wzrost drugiej polowy to Westeros po cieciach. Nadwyzka zrodel: Westeros 4 530 lup, Essos 110 (d91-365; d271+: 5 475 / 70). "Zrodlo z nadwyzka >= 8 lup w 600" ma 37,8 miast, ale nadwyzke >= 200 lup w 600 linii prostej ma 26,6 z 40,1, w Essos 9,3 z 22,1 (i to przez morze, droga dluzsza). Rachunek agenta liczy zasieg, nie ilosc. Moj rachunek po samej poprawce A (zgrubny): Westeros ok. 5-8 (nadwyzke >= 200 lup w 400 prostej = ok. 600 drogi ma 13,3 z 18,0), Essos ok. 16-21 (nie ma tam czego rozwozic) -> razem ok. 20-29, nie 8-12. Miesci sie w progu narzedzia (30), nie w celu projektu (15).
4. ZAMKI TO DRUGI MARTWY MAGAZYN, ktorego A nie rusza: 298 -> 3 654 lup (25% zapasu swiata), +9,2/d w roku, +13,8/d w d181-360. Rosnie skokami calych wozow: 87 dob wzrostu, razem +3 850 (d116 +165, d330 +133, d362 +124 - L127726/L128078: zamki 3 536 -> 3 660 przy "wozy do osad 223"), ubywa 494 w roku. Linia "Targ przy zamku" tego nie widzi (wizyt = dalej do miasta, "zamek kupil caly woz" 0): woz nieskierowany wjezdza do zamku i gra sprzedaje caly ladunek (CastleMarket.cs:327 return true). 11 z 26 kopaln to wsie zamkowe (w Essos 4 z 8). Powod wjazdu (ucieczka przed bandami?) NIEZMIERZONY.
5. "ZLOTO NIE JEST PRZESZKODA" - nie dla wszystkich: wsrod miast bez zelaza kase <= zapas kupcow (TownPurse.BuyFloor, TownPurse.cs:269) ma 3 / 12 / 4 / 4 / 5 / 8 miast (d119/179/239/299/359/364, kasy-miast.csv). Te miasta nie kupia zadna droga.
6. "STOI 21 KARAWAN": 15 z 17 stojacych stoi 7+ dob (L128735), w tym 12-13 karawan bez statkow uwiezionych w Pyke przez caly rok (L128841: 13 [Pyke 13], najdluzej 363 doby; 11 to karawany kupcow z Pyke). Osobny blad (S) - zajmuja limit karawan kupcow.
7. LUKI W SAMEJ POPRAWCE A: (a) ilosc zamowienia = max(10, D+4) dob zuzycia, D z NoteTrip, domyslnie 4 (MaterialOrders.cs:464, :610-617); woz na 600 jedzie do ok. 10 dob - bez wpisania dni wozu miasto na koncu zasiegu stoi puste ok. 4 doby w kazdym cyklu; (b) "droga <= 600" jest ostrzejsza niz dzisiejsza regula morska (rejs <= 1000, koszt rejs x 0,25 <= zasieg; :472, :529); (c) wspolna lista TradeCarts zmieni liczniki linii "Przewoz kupcow wozem (215)", na ktorych stoja progi etapu 6; klucz Incoming to koszyk uzbrojenia (typ x tier), nie surowiec.

ZAMKNIETA EKONOMIA I CZAS: poprawka A niczego nie tworzy (sztuka z polki zrodla, zloto kasa celu -> kasa zrodla, zwrot przy zawroceniu najwyzej z kasy zrodla - jak R3); panowie traca clo ok. 10% z tych sprzedazy (jak w R3); +2-5 ms na dobe z 12 500 ms - bez znaczenia. Karawany zostana z ok. 2 600 lup kupionych po 11-14 d i wyladuja je regula (c) CaravanBulk - patrzec na kiesy karawan.

**Poprawka ostateczna:**

KOLEJNOSC: P1 + P2 w jednym wgraniu -> bieg 120 i 364 doby -> P3 wedlug pomiaru. Nowa kampania (Armoury.json nie ma zapisanego zadnego z tych kluczy - sprawdzone, dzialaja wartosci z kodu).

P1 (M) - KONTRAKT WOZEM, czyli poprawka A agenta z piecioma zmianami. Plik Armoury\src\MaterialOrders.cs, Order(): gdy bestSrc == null, drugi przebieg bez przewoznika; klucz TownMaterialOrderByCart = true (Settings.cs + python tools/gen_mcm.py).
 1. Zrodla: miasta ponad SourceKeepDay ORAZ ZAMKI (polka zamku ponad 10 dob jego wlasnego zuzycia; zaplata do kasy zamku przez Town.ChangeGold - wzor R3, SupplyDemand.cs:574 i :663-683; stary powod "zamki nie sa zrodlem" dotyczyl SellItemsAction, tu go nie ma). Bez tego 3 654 lup w zamkach (+10-14/d) zostaje martwe.
 2. Zasieg: lad TradeCarts.Road <= TownMaterialOrderRange 600 dla wszystkich 7 surowcow; dla iron przy Bloomery.On 600 takze w starej sciezce (:466). Morze - jak dzis w :486/:529 (oba porty, rejs <= TownMaterialOrderSeaMaxRoute, rejs x SeaFreightShare <= zasieg), nie ostrzej.
 3. Ilosc: want jak w :464, ale D = planowane doby wozu dla tej pary (TradeCarts.DaysFor(droga)), a przy dostawie NoteTrip(miasto, surowiec, doby od zakupu).
 4. Warunek zysku z oplata drogi jak dzis (:567-569) zostaje jako warunek; zakup sztuka po sztuce ShelfBuy.Take, placi kasa celu ponad TownPurse.BuyFloor.
 5. Osobna lista wozow surowcow (albo znacznik rodzaju w TradeCarts.Cart) z wlasnym slownikiem "w drodze: cel x surowiec" doliczanym w InTransit (:230) i wlasnymi licznikami w linii "Kontrakty surowca (174)": wozem zawarto / dojechalo / w drodze / zawrocone + kontrola reszta 0. Linia 215 bez zmian. Ksiegi: GoodsLedger (posiadacz "wozy surowcow"), OreLedger linia 214 (zapas + w drodze), spis HarvestValve (:338) ma liczyc te liste.
 Oczekiwany skutek (rachunek, do potwierdzenia biegiem): miast bez zelaza ok. 20-29 zamiast 50-60 pod koniec roku; Westeros ok. 5-8, Essos ok. 16-21. W nowej kampanii regulator w 1. roku nie powinien ciac zelaza (stos bez nadwyzek 4 miast i zamkow ok. 37-42 dni przy progu 60).
 Poza zasiegiem P1 (nie 100%): Essos (brak produkcji), 3-12 miast z kasa na zapasie kupcow, pary w wojnie, miasta ponad 600 drogi bez portu.
 Ryzyko: kierunek "woz zamiast karawany" do potwierdzenia przez Jeffa (05.10 chcial prawdziwych partii; woz z czasem drogi jest juz przyjety dla R3 i F4a); progi G1-6b w tools\sprawdz_logi.py do zmiany na liczniki wozow; drewno - ten sam prog nadwyzki co wozy dymarek F4a (Bloomery.Daily biegnie pierwsze, zostaje).

P2 (S, tylko log) - pomiary, bez ktorych P3 jest zgadywaniem:
 a. linia 214: produkcja, zuzycie, zapas i miasta bez zelaza osobno dla Westeros i Essos (podzial ladem, nie stala x);
 b. linia 174b: 10 zamkow z najwiekszym zapasem zelaza i licznik "caly woz sprzedany w zamku poza Targiem przy zamku" (zamek, sztuk, powod wjazdu) - zaczep w CastleMarket.SellPrefix :327;
 c. linia 174: powody "bez kontraktu" osobno dla kazdego surowca;
 d. dla miast-portow bez zelaza: dlugosc rejsu do najblizszego zrodla z nadwyzka >= 200 lup (czy Wyspy -> Essos miesci sie w 2 400).

P3 (po biegu z P1+P2) - regulator nie moze ciac kopaln, ktorych rynek jest pusty. Dwie drogi, wybor wedlug P2:
 - MORZE (S, jedna liczba, zalecana jesli P2d wyjdzie <= 2 400): Settings.cs:1526 TownMaterialOrderSeaMaxRoute 1000 -> 2400 (= zasieg 600 / udzial morza 0,25, czyli limit robi sam koszt). Porty swiata staja sie jednym rynkiem, nadwyzka Wysp i Polnocy plynie do portow Essos, jeden regulator swiatowy znow liczy prawde. Koszt: rejsy po 4-6 tygodni wozem bez partii, zamowienia na 30-40 dob zuzycia.
 - REGULATOR NA LAD (M-L): HarvestValve liczy stos, zuzycie i mnoznik osobno dla Westeros i Essos (ta sama regula :453/:462, inny zakres spisu :323-345, mnoznik w MaterialLaw.cs:220 wedlug ladu wsi). Essos przy pustych polkach dostaje krok x1,3 (ok. +22 lup/d wobec niedoboru ok. 30), Westeros tnie swoje nadwyzki.
 Bez P3 cel <= 15 jest nieosiagalny, a w 2. roku nowej kampanii nadwyzka Westeros znow uruchomi ciecie calego swiata.

OSOBNO (S): 12-13 karawan bez statkow uwiezionych w Pyke caly rok - do rozpoznania w obszarze karawan (IslandRoads).

BRAKUJACE POMIARY (dzis nie do odczytania z logu): podzial produkcji i zuzycia zelaza na lady; ktore zamki trzymaja zelazo i jak wozy tam trafiaja; udzial zelaza w powodach "bez kontraktu"; dlugosci rejsow Wyspy/Barrowton -> porty Essos.


### O2-2: regulator wsi (221) przykreca kopalnie, bo liczy martwy zapas - petla, ktora sama sie nie zatrzymuje

**Werdykt:** STROJENIE | **pewnosc:** srednia | **podwazenie - broni sie:** True

**Przyczyna:**

Regulator dziala zgodnie z kodem (HarvestValve.cs:453: ciecie, gdy zuzycie < 90% produkcji i stos > 60 dni zuzycia; cel 0,95 x zuzycie), ale stos liczy z WSZYSTKICH osad i partii (:322-337), takze z zamkow i z polek, z ktorych nikt nie wywozi. Decyzje dla 'iron' (linia 'Regulator wsi (221)'): d56 x1,00, zuzycie/produkcja 0,61, stos 44 dni; d84 0,74 / 41; d112 0,80 / 43; d140 0,80 / 49; d168 0,79 / 56; d197 (dzien 109032) 'x1.00 -> x0.73, 0.77 (11293/14713), stos 13191 = 65 dni, w dol'; d225 x0,72; d253 1,03 bez zmian; d281 0,96; d309 (dzien 109144) 'x0.72 -> x0.61, 0.89 (9140/10249), stos 14025 = 86 dni, w dol'; d337 '0.90 (8404/9300), stos 14218 = 95 dni'.
SKUTEK: model kopaln 280 -> 172 lup/d, lupy 264 (d151-180) -> 158 (d331-360); regulator zdjal ok. 14,8 tys. lup w d197-364 (88/d). Zuzycie okna 56 dob spada razem z produkcja: 11 293 -> 10 788 -> 10 560 -> 9 900 -> 9 140 -> 8 404 (202 -> 150/d), bo zuzycie jest ograniczone dowozem, a nie popytem. Miasta z nadwyzka 39 (d181-210) -> 12-16; miasta bez zelaza 35,0 (d181-210) -> 38,5 -> 39,0 -> 46,3 -> 49,7 -> 51,1 -> 61,5 (ostatnie 5 dob); cykle 'brak rudy' 428 -> 491 -> 683/d. Pod koniec roku bez zelaza sa nawet miasta przy kopalniach Essos (lista d364: Meereen, Yunkai, Tolos, Mantarys, Vaes Diaf).
CZY SIE STABILIZUJE: nie w oknie 364 dob. Ciecie zmniejsza dowoz, wiec zmniejsza zuzycie, wiec stos w dniach zuzycia rosnie (65 -> 95) mimo ciecia, a sam stos tez rosnie (13 191 -> 14 218). Warunek powrotu w gore (zuzycie >= 98% i stos < 30 dni) jest nieosiagalny, dopoki 80% zapasu jest poza zasiegiem zuzycia. Podloga mnoznika 0,1. Stary bieg 728 dob nie mial regulatora i trzymal 34-47 miast bez rudy - dlatego pogorszenie z ostatnich 3 miesiecy przypisuje regulatorowi z pewnoscia srednia (rozrzut dlugich biegow to +-7 miast).

**Poprawka:**

Najpierw O2-1 (A) i O2-3 - wtedy martwy zapas schodzi wozami, zuzycie rosnie o 60-100 lup/d i regula regulatora sama wraca do x1,0 (przy pelnym dowozie zuzycie 235-270 wobec produkcji 264 + zlom 20, czyli stosunek ok. 0,9-1,0; w razie niedoboru regulator ma jeszcze krok w gore do x1,3 = 364 lup/d). W nowej kampanii z poprawka od pierwszej doby ciecie z d197 nie powinno wystapic.
Dopiero gdy bieg 364 dob po poprawce nadal pokaze ciecie zelaza przy pustych miastach - bezpiecznik (S, plik Armoury\src\HarvestValve.cs, Close(), klucz np. ValveNoCutWhileShort = true): towar z tabeli surowcow karawan nie dostaje ciecia 'w dol', gdy bez niego jest wiecej niz 15 z 97 miast (licznik juz jest: CaravanBulk, pole Empty). Sens: brak w co szostym miescie to zly dowoz, nie nadprodukcja. Ryzyko: przy zepsutym dowozie stos znow by rosl bez konca - dlatego tylko jako dodatek do A, nie zamiast. Progow 30/60 dni i 90%/98% nie ruszac.
Narzedzie progow: nowy wiersz - mnoznik zelaza regulatora w kazdej decyzji >= 0,9 przy miastach bez zelaza > 15 = NIE.


**Podwazenie - uwagi:**

WERDYKT PODWAZENIA: przyczyna i werdykt STROJENIE bronia sie (regulator dziala jak napisany, tnie przez zapas, ktory nie dochodzi do odbiorcow). Nie bronia sie trzy rzeczy: opis "petla, ktora sama sie nie zatrzymuje", prognoza "po poprawce dowozu ciecie nie wystapi" oraz bezpiecznik w podanej postaci. Tresci O2-1 (A) i O2-3 nie widzialem - oceniam tylko czesc regulatora.

Zrodla: log D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log (linie "Regulator wsi (221)", "Towary: ... iron", "Zelazo (lupy) (214)", "Karawany (stan)", "Miasta bez rudy i strzal (174b)"); kod C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int\Armoury\src\ (HarvestValve.cs, CaravanBulk.cs, ArmouryBehavior.cs, Settings.cs); moje szeregi: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\triaz3\podw-o22\ (ts.json, ts.py, win.py).

CO SIE POTWIERDZA
1. Kod: ciecie HarvestValve.cs:453-461, powrot w gore :462-466 (wymaga stosu < 30 dni zuzycia), spis stosu :322-337 (wszystkie osady i partie). Zgodne z wnioskiem.
2. Decyzje dla iron zgodne co do liczby (doby to d56, d84 ... d336, wniosek ma przesuniecie o 1): d196 x1.00->0.73 (11293/14713, stos 13191 = 65 dni), d224 x0.72, d308 x0.72->0.61 (9140/10249, 86 dni), d336 bez zmian (8404/9300, 95 dni).
3. Trend w oknach 28 dob (ksiega "Towary: iron"; lupy na dobe):

| okno | wsie | zlom | zuzycie | wozy do osad | zapas na koniec | w tym zamki | miasta bez | miasta z nadwyzka |
|---|---|---|---|---|---|---|---|---|
| d140-167 | 265 | 8 | 207 | 234 | 11202 | 986 | 36.5 | 33 |
| d168-195 | 261 | 11 | 201 | 250 | 13078 | 1263 | 35.4 | 38 |
| d196-223 | 185 | 13 | 198 | 177 | 12970 | 1589 | 36.4 | 37 |
| d224-251 | 185 | 17 | 202 | 152 | 12901 | 2124 | 38.6 | 27 |
| d252-279 | 184 | 19 | 187 | 141 | 13276 | 2339 | 41.5 | 17 |
| d280-307 | 183 | 20 | 176 | 178 | 14010 | 2651 | 47.4 | 18 |
| d308-335 | 150 | 21 | 164 | 135 | 14180 | 3223 | 49.6 | 15 |
| d336-363 | 158 | 21 | 159 | 145 | 14517 | 3654 | 53.4 | 12 |

4. "80% zapasu poza zasiegiem" zgadza sie: d364 zamki 3654 + juki karawan 2582 + cztery miasta (Barrowton 2442, Pyke 1572, Lordsport 892, Ten Towers 294) 5200 = 11436 z 14517 (79%).
5. Pierwsze ciecie wywolal martwy zapas. W oknie d140-195 stos rosl o 61/d: cztery miasta ok. 30/d, zamki 9/d, pozostale miasta ok. 17/d, reszta 5/d. Bez czterech miast i zamkow stosunek bylby 0.92 (brak ciecia), a stos ok. 28 dni.
6. Ciecie zabralo prawdziwe zuzycie. Zdjeto ok. 14.9 tys. lup (89/d); zuzycie spadlo 201 -> 159/d (-21%, razem ok. 3.3 tys. lup w d252-363). Miasta, ktore przed cieciem mialy zelazo zawsze, w d308-364 sa bez niego w 42-75% probek: Seagard, Lannisport, Kayce, Starfall, Astapor, Last Hearth, Stoney Sept, The Twins. Produkcja spadla dokladnie o mnoznik (wsie 152 wobec modelu 165 - rabunki to ok. 3-5 pp), wiec to nie wojna.

CO SIE NIE BRONI
A. To zapadka, nie samonapedzajaca sie petla. Stosunek po cieciach: 0.87, 1.03, 0.96, 0.89, 0.90; decyzja d364 z danych ksiegi wyszlaby ok. 0.94 (P 8630, przyrost stosu ok. 507), czyli bez zmian. Mnoznik zatrzymuje sie w okolicy 0.55-0.61, ale nie moze wrocic, a kazde wahniecie ponizej 0.90 tnie na stale. Poziom, do ktorego swiat zmierza bez zmian: wsie ok. 155/d + zlom 21, zuzycie ok. 160/d, 50-60 miast bez zelaza. Dalszy spadek w latach to szacunek - brak biegu 728 dob na tym kodzie.
B. Drugie ciecie (d308) zapadlo, gdy swiat zuzywal 99% wydobycia: okno d252-307 wsie 10249, zuzycie z ksiegi 10167. Stos urosl o 1109 = dokladnie tyle, ile dal zlom (19.8/d), a przybylo go w zamkach (+527) i w wozach w drodze (+447, wahanie). Stosunek 0.8918 wobec progu 0.90; bez wahania wozow byloby 0.935. Regulator z zalozenia nie liczy zlomu jako produkcji (HarvestValve.cs:30), wiec zlom 20/d trwale obniza stosunek o ok. 0.11. Dlatego "202 -> 150" we wniosku zawyza spadek - prawdziwe zuzycie to 201 -> 159.
C. Zamki to osobny, rosnacy odplyw: 385 -> 3654 w rok, po cieciu 14/d. Wplyw skokami po 30-165 lup w 87 dobach (razem +3850), ubytek tylko 494 w rok. Wyglada na cale ladunki wozow sprzedane zamkowi; sciezki w kodzie nie ustalilem - brakujacy pomiar.
D. Prognoza "ciecie z d197 nie powinno wystapic" nie ma pokrycia. We wszystkich czterech biegach d112 przy x1.00 wyglada tak samo: bieg 2 z 1570 karawanami i tylko 28 miastami bez rudy - 0.71, stos 54 dni; test koncowy - 0.70, 60 dni; limit 3 - 0.82, 40 dni; roczny - 0.80, 43 dni. Sama przepustowosc karawan nie podnosi stosunku ponad 0.90. Do tego w jukach karawan stoi stale 2300-2900 lup (CaravanBulkTransitCover = 6 x brak 350-640, Settings.cs:660), a cele zapasu miast licza sie z dawnych rak (suma celow rudy 1058 = ok. 5 dob prawdziwego zuzycia) - sam system dowozu trzyma 30-45 dni zapasu, wiec prog 60 dni peka po ok. 3000 lup martwego zapasu.
E. Nawet przy pelnym dowozie baza jest lekko za wysoka: popyt 240-260/d wobec 263 + zlom 20, czyli stosunek 0.84-0.91. Ciecie do ok. 0.8-0.9 moze wtedy zapasc i bedzie nieszkodliwe (zapas osiagalny).
F. Bezpiecznik w podanej postaci ma trzy wady:
 - CaravanBulk.Empty to prywatne pole prywatnej klasy (CaravanBulk.cs:90, :402), przeliczane w CaravanBulk.Daily, ktore idzie PO HarvestValve.Daily (ArmouryBehavior.cs:1614 wobec :1629). Po wczytaniu zapisu jest 0, przy wylaczonej regule karawan tez 0 - bezpiecznik otwieralby sie po cichu.
 - Blokuje kazde ciecie, takze cofniecie wlasnego kroku x1.3. Len (75 miast bez towaru, dzis x1.30) i welna (35 miast) nie moglyby nigdy zejsc.
 - Nie ma drogi powrotu: ciecie zapadle przy 15 lub mniej pustych miastach zostaje na zawsze, gdy dowoz pozniej siadzie.
G. Odkladanie bezpiecznika do nastepnego biegu rocznego kosztuje drugi bieg (ok. 80 min) przy 4 z 4 biegow idacych w ciecie. Informacja nie ginie, jesli linia decyzji pisze "ciecie wstrzymane".

RYZYKO ZALECANEJ POPRAWKI
- Nic z niczego: bezpiecznik tylko nie obniza bazowego wydobycia; wytop dalej ogranicza drewno i kiesa kopalni (Tylemos i King's Valley juz staja z pusta kiesa).
- Dopoki martwe konce nie sa naprawione, zapas rosnie jak przed cieciem, ok. 60/d (do ok. 23 tys. lup na d364 zamiast 14.5 tys.). Dlatego to dodatek do poprawek dowozu, nie zamiennik.
- Szybkosc gry: 97 miast x 4 towary raz na 28 dob - pomijalne.
- W biegu 120 dob ciecie i tak nie zapada, wiec dzialanie bezpiecznika potwierdzi dopiero bieg roczny.

**Poprawka ostateczna:**

KOLEJNOSC: poprawki dowozu i martwych koncow zostaja pierwsze, a bezpiecznik regulatora wchodzi RAZEM z nimi, nie po kolejnym biegu rocznym.

1. MARTWE KONCE (cele dla poprawek dowozu, z liczbami biegu rocznego):
 - cztery miasta-zrodla (Barrowton, Pyke, Lordsport, Ten Towers): 5200 lup, przed cieciem +30/d;
 - zamki: 3654 lup, +14/d, zuzycie wlasne ok. 1.4/d;
 - juki karawan: 2300-2900 lup stale, sprzedaz 27-58/d.

2. BEZPIECZNIK REGULATORA - wielkosc S.
Plik: Armoury\src\HarvestValve.cs, metoda Close(), blok decyzji :446-466. Do tego Settings.cs obok :1688-1691 i python tools/gen_mcm.py.
Klucze: ValveHoldWhileShort = true, ValveShortTowns = 15 (cel projektu R1-8b: najwyzej 15 miast bez zelaza). Nowe klucze - plik MCM Jeffa ich nie nadpisze.
 a) Licznik liczony w Close(), bez pola CaravanBulk.Empty: dla towaru z listy CaravanBulk.Items() (w regulatorze: iron, hardwood, flax, wool) policzyc miasta z Town.AllTowns, w ktorych polka ma 0 sztuk. Pusta lista (regula karawan niegotowa) = bezpiecznik nieczynny i dopisek w linii.
 b) Brak = pustych miast > ValveShortTowns. Przy braku ciecie nie schodzi ponizej x1.00: w :459 m1 = max(m1, 1.0). Cofniecie wlasnego kroku x1.3 dziala dalej.
 c) Powrot: przy braku i mnozniku < 1.00 -> m1 = min(1.0, m0 x 1.3), bez warunku stosu < 30 dni. To tylko cofniecie wlasnego ciecia, nigdy ponad baze.
 d) Linia decyzji: dopisek "ciecie wstrzymane: bez towaru N miast" albo "powrot do bazy: bez towaru N miast".
 e) Progow 30/60 dni i 90%/98% nie ruszac. Stanu w zapisie nie przybywa.
Rachunek dla biegu rocznego: d196 - 34 puste miasta > 15, wiec ciecie wstrzymane. Wsie zostaja przy 261-265/d zamiast 185 -> 154, zuzycie przy ok. 200/d zamiast 159, miasta bez zelaza przy ok. 35 zamiast 49-53. Dla drewna (8 pustych miast) regulator dziala jak dotad.

3. NARZEDZIE PROGOW (tools\sprawdz_logi.py):
 - NIE, gdy w ktorejkolwiek decyzji mnoznik iron < 1.00 przy miastach bez zelaza > 15 (z linii "Karawany (stan)" tej doby);
 - INFO: liczba decyzji "ciecie wstrzymane" na towar;
 - INFO: zapas zelaza wedlug posiadaczy na koniec okna (zamki, juki karawan, cztery najwieksze miasta) i ich udzial; w biegu rocznym 79%.

4. BRAKUJACE POMIARY:
 - ktora sciezka kodu sprzedaje zamkom cale ladunki wozow z zelazem (87 dob, +3850 lup w rok);
 - bieg 364 dob po poprawkach: czy cztery miasta i zamki przestaja rosnac - jesli nie, nastepnym krokiem jest ciecie miejscowe (tylko kopalnie, ktorych miasto zbytu ma nadmiar), wielkosc M;
 - udzial zuzycia swiata w pustych miastach (CaravanBulk.UseNow) - zeby ocenic, czy prog 15 miast warto zastapic udzialem.


### O2-3: zamki to studnia bez dna dla zelaza - woz uciekajacy do zamku sprzedaje tam caly ladunek

**Werdykt:** BLAD | **pewnosc:** srednia | **podwazenie - broni sie:** True

**Przyczyna:**

Zelazo w zamkach: 297 (d2) -> 3 654 (d364), czyli +3 357 w roku (25% zapasu swiata); tempo w oknach 30 dob: 2,9 / 0,9 / 0,4 / 10,5 / 4,7 / 4,6 / 11,9 / 18,1 / 14,8 / 6,9 / 19,0 / 13,3 lup/d. Przyrasta skokami wielkosci jednego wozu kopalni: d116 +165, d330 +133, d362 +124, d233 +112, d193 +92 (25 najwiekszych skokow = ok. 2 200 z 3 357). Tego dnia zamki placa wozom niewiele ponad jedzenie (d116: 'zaplata taborom wsi -6573' przy jedzeniu za 5 872) - 165 lup poszlo po ok. 4 zl, bo zamek zelaza nie zuzywa. W starym biegu 728 dob to samo: zamki 342 -> 3 026.
DROGA (kod): partia wsi uciekajaca przed wrogiem chowa sie w najblizszej warowni, takze w zamku (gra MobilePartyAi.cs:781-794, warunek IsFortification w GetBehaviorForNearbySettlementToFlee); latka BK VillagerSettlementEnterPatch sprzedaje przy kazdym wjezdzie do osady z Town != null, czyli takze w zamku (bkfull\BannerKings.Patches\EconomyPatches.cs:903-913); gra sprzedaje caly ladunek z calej kasy zamku (SellGoodsForTradeAction.cs:17-66). Nasz targ przy zamku (5.11) lapie tylko woz skierowany tam po jedzenie; kazdy inny woz idzie regula gry (CastleMarket.cs:325-327: 'woz nie skierowany przez targ przy zamku - jak dotad'), a po pierwszym wjezdzie woz traci znacznik (:330), wiec ucieczka z powrotem do tego samego zamku tez konczy sie sprzedaza calosci. Liczniki celowego kierowania do zamku sa zerowe caly rok (linia 'Dowoz': do zamku brak targu 0, targ za daleko 0, wojna 0; 'bez miasta dalej' 0), wiec to nie jest zamierzona droga.
Z zamku zelazo nie wraca: karawany tam nie jezdza, zamek nie jest zrodlem kontraktu (MaterialOrders.cs:33-34). Towar nie znika, ale jest stracony dla kuzni i zawyza stos regulatora (O2-2). To samo dotyczy innych surowcow (drewno w zamkach 17 222).
BRAKUJACY POMIAR: w logu nie ma licznika 'woz wjechal do zamku poza targiem 5.11 i co sprzedal' - droge potwierdza kod i wielkosc skokow, nie wpis sztuka po sztuce.

**Poprawka:**

S, plik Armoury\src\CastleMarket.cs, SellPrefix (:323-327), klucz np. CastleCartFoodOnly = true: woz wsi, ktory wjechal do zamku, a nie jest tam skierowany jako do celu (brak miasta w zasiegu - MarketRoad), sprzedaje tylko brakujace jedzenie ta sama funkcja FeedFromDistrict (:405) i zwraca false (sprzedaz gry pominieta); reszta ladunku jedzie dalej do miasta. Woz bez zadnego miasta w zasiegu - jak dotad (w roku 0 takich przypadkow). Do linii 'Targ przy zamku (5.11)' licznik: wjazdy poza targiem, sztuki zatrzymane w wozie wedlug towaru.
RACHUNEK: +9 lup/d sredniorocznie (13-19/d w drugim polroczu) zostaje w obiegu = 4% produkcji, do 8% po cieciu regulatora; zapas zamkow przestaje rosnac. Czas doby: 0.
RYZYKO: male - sprawdzic, ze zwrot nadplaty 119 (MarketCarts.SellPostfix) i ramka ksiegi towarow widza pusta sprzedaz bez 'niezgodne'; BK VillagersCastleTick odsyla woz stojacy w zamku do domu (BKPartyBehavior.cs:392) - woz z ladunkiem ma dostac rozkaz do miasta jak po targu 5.11 (straz HoldHours juz istnieje).
Zapasu juz lezacego w zamkach nie ruszac (nowa kampania startuje z ok. 300 lup w zamkach).


**Podwazenie - uwagi:**

BRONI SIE CZESCIOWO. Mechanizm z wniosku jest prawdziwy i jest BLEDEM, ale tlumaczy ok. 2/3 doplywu zelaza do zamkow. Pozostala ok. 1/3 idzie inna droga, ktorej wniosek nie widzi i ktorej proponowana poprawka nie zamyka. Sama poprawka ma tez trzy dziury.

1. LICZBY WNIOSKU - ZGODNE (log Armoury-2026-10-13_02-15-21, linia "Zelazo (lupy) (214)")
- Zamki 298 (d1) / 297 (d2) -> 3654 (d364), czyli 25,2% zapasu swiata (14 517).
- Tempo w oknach 30 dob: 2,9 / 0,9 / 0,4 / 10,5 / 4,7 / 4,6 / 11,9 / 18,1 / 14,8 / 6,9 / 19,0 / 13,3 lup/d.
- Skoki d116 +165, d330 +133, d362 +124, d233 +112, d193 +92; 25 najwiekszych = 2132.
- Liczniki kierowania do zamku w roku: brak targu 0, za daleko 0, wojna 0, daleki targ 0, "bez miasta dalej" 0, "inny zamek" 4, rezerwacje wygasle 34.
- Stary bieg 728 dob: 298 -> ok. 2960 w 638 dob, bez stabilizacji.

2. DROGA A (teza wniosku) - JEST DOWOD WPROST, ktorego autor nie znalazl
- Linia "Przeplywy osad: utarg taborow wsi" (MoneyLedger.cs:280-297) liczy kazdy wjazd taboru do zamku: 15 214 w roku. Wizyt skierowanych 5.11 bylo 12 207.
- Zostaje 3007 wjazdow nieskierowanych: 4,7/d w I polroczu, 11,8/d w II.
- Wizyt z towarem po wizycie bylo 12 213, czyli tylko skierowane. Kazdy wjazd nieskierowany konczy sie pustym wozem.
- Zamki zaplacily taborom 4 480 876 zl. Jedzenie 5.11 to 2 481 862 zl po cenie pierwszej sztuki (ok. 2,08 mln po zwrocie 119). Za zrzuty calych wozow poszlo wiec 2,0-2,4 mln zl/rok (3,9-5,1 tys./d w I polroczu, 7,0-8,1 tys./d w II).
- Kod potwierdzony: ucieczka MobilePartyAi.cs:792-794 -> :1494-1531 (warunek IsFortification jest w :1504, nie w :781-794); wjazd EncounterManager.cs:291-293; gra sprzedaje tylko w IsTown (VillagerCampaignBehavior.cs:324-330); BK rozszerza na Town != null (EconomyPatches.cs:908-913); CastleMarket.cs:327 i :331 przepuszczaja ("jak dotad").
- Wjazdy nieskierowane rosna razem z bandami (402 -> 953 band) i rozbitymi taborami (4,9 -> 17,6/d); korelacja 14-dobowa 0,82.
- To nie jest sprawa zelaza. Udzial zamkow w towarze wozow: glina 4,9%, welna 4,6%, len 4,5%, sol 3,9%, srebro 5,3%, zelazo 5,8%, drewno 2,7%; w II polroczu 5-10%.

3. DROGA B - POMINIETA WE WNIOSKU: "dobytek rozbitych" (E6/K1 pkt 2, HorseFate.Spoils)
- Gdy banda rozbija woz, stos, ktory nie miesci sie w jej udzwigu, idzie CALY i ZA DARMO na polke najblizszej warowni. Put: HorseFate.cs:816-838; NearestMarket :232-247 szuka po Town.AllFiefs, wiec takze po zamkach; dla bandy wychodzi najblizsza dowolna warownia.
- Linia "Konie - smierc, lup, dobytek (E6/K1)": w roku 8893 partie, na polki osad 240 533 szt. (ponad udzwig 213 969). To 404 szt./d w I polroczu i 916/d w II.
- Bilans zelaza roku: zaladowane na wozy 75 437, sprzedane w ramkach sprzedazy 66 607, przyrost na wozach 731. Z rozbitych wozow zeszlo 8099 lup (10,7%). Miasta i zamki dostaly poza ramkami sprzedazy 8294 lup.
- Dowod dzienny d330: zamki +133, a caly licznik "wozy do osad" to 102 i miasta wziely z wozow ok. 123. Te 133 lupy nie przeszly przez sprzedaz; dobytek na polki tego dnia 1000 szt. To samo d233 (+112, dobytek 1394), d234, d261, d307, d311, d350.
- Rozdzial skokow >= 20 lup: w liczniku sprzedazy wozow 2248 lup (37 dni), poza nim 1345 lup (24 dni), male skoki 257. Regresja: 0,69 +/- 0,08 doplywu siedzi w sprzedazy.
- WYNIK: droga A ok. 2250-2650 lup/rok (6-7/d, II polrocze ok. 9,6/d). Droga B ok. 1200-1350 lup/rok (ok. 3,5/d, II polrocze 5,7/d), blad ok. +/- 300. Rozbicia dobytku wedlug towaru w logu nie ma - to szacunek z bilansu.
- Werdykt dla B: STROJENIE. Regula jest zamierzona (R14: nic w nicosc), ale w zamku towar nie-zywnosciowy jest martwy.

4. CO TO ZMIENIA WE WNIOSKU
- Rachunek "+9 lup/d zostaje w obiegu" jest zawyzony o ok. 1/3. Sama poprawka A daje 6-7 lup/d.
- "Zapas zamkow przestaje rosnac" - nieprawda przy samej A. Droga B dalej dosypuje 3,5-5,7 lup/d.
- "Studnia bez dna" - niescisle. Zamki zjadaja 0,1-0,3% zapasu na dobe (-494 w roku, "inne ticki dobowe (osady)"). Rownowaga wypada dopiero przy ok. 11-13 tys. lup, a doplyw rosnie z bandami, wiec w grywalnym czasie stabilizacji nie ma.
- "Po ok. 4 zl" dotyczy tylko d116. Z 25 skokow wychodzi srednio ok. 11 zl/lupa (z duzym rozrzutem) wobec ok. 19 w miastach; czesc "tanich" dni to droga B, gdzie zamek nie placi nic.

5. DZIURY W PROPONOWANEJ POPRAWCE
(a) IsVillager to takze lodzie rybackie NavalDLC (FishingPartyComponent : VillagerPartyComponent). PickTown + Move wyslalby lodz na targ ladem. Gra sama pomija je w HourlyTickParty (VillagerCampaignBehavior.cs:264, HasLandNavigationCapability).
(b) "Straz HoldHours juz istnieje" - niepelne. Po 6 h CastleTickPrefix (CastleMarket.cs:462-466) zdejmuje wpis, zanim BK odesle woz do domu, wiec licznik "wrocily do domu z ladunkiem" (Sweep :607) tego nie widzi. Jego 0 w logu niczego nie dowodzi. Woz chowajacy sie przed wrogiem dluzej niz 6 h pojedzie do domu z ladunkiem - towar nie ginie, kurs przepada.
(c) Woz wsi MIEJSKIEJ, gdy Choose nie znajdzie miasta: PickTown zwraca null (MarketRoad.cs:96) i zostaje zrzut calego wozu w zamku.

6. SKUTKI UBOCZNE - SPRAWDZONE
- Nic z niczego. Czas doby 0 (najwyzej kilkanascie wyborow miasta dziennie po ok. 0,2 ms).
- Budowy zamkow biora material z najblizszego miasta (BuildFunding.cs:167), wiec polka zamku nie jest niczyim zrodlem.
- Kasy zamkow zatrzymaja 2,0-2,4 mln zl/rok. Wsie zyskaja: zrzut dawal w II polroczu ok. 500-900 zl na wjazd wobec ok. 3600 zl za wizyte w miescie.
- Podzamcza straca przypadkowy towar nie-zywnosciowy (zjadaly rocznie gline 4093, sol 3893, drewno 9957 szt.). To zgodne z decyzja 5.11 (zamek kupuje tylko jedzenie); obserwowac "podzamcze zaplacone" w linii "Kiesa ludu".

7. NIEWYJASNIONE (brak pomiaru)
- Wjazdy nieskierowane skacza ok. d155 trzykrotnie (62 -> 175 na 14 dob) bez skoku rozbitych taborow. Log nie podaje powodu wjazdu ani tego, czy to woz, czy lodz.

Skrypty i dane: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\triaz3\podw-o2-3\ (ts.py, ts2.py, ts3.py, an1.py-an8.py, rok.json).

**Poprawka ostateczna:**

DWIE OSOBNE ZMIANY, kazda S. Razem tor M (jeden workflow, jedna recenzja), bo dotykaja dwoch systemow. Kolejnosc: A, autotest 120 dob, potem B. Zapasu juz lezacego w zamkach nie ruszac (nowa kampania startuje z ok. 300 lup).

ZMIANA A (S) - woz, ktory wjechal do zamku nieskierowany, sprzedaje tylko brakujace jedzenie
Plik: Armoury\src\CastleMarket.cs, SellPrefix (:323-366). Klucz CastleCartFoodOnly = true w Settings.cs, potem tools\gen_mcm.py. Czynne tylko przy On(s) i Ready(); wylaczone = jak dotad.
Zamiast "return true" w :327 (woz bez wpisu w _bound) i w :331 (inny zamek) wspolna galaz:
1. Partia bez HasLandNavigationCapability (lodz rybacka NavalDLC): return true jak dotad, plus licznik.
2. home albo wies null, albo zamek Innych (Others): return true.
3. Woz bez towaru (!Sellable): return true.
4. sales = FeedFromDistrict(fort, woz, s, ..., sales) - ta sama funkcja (:405): tylko brakujace jedzenie do CastleMarketFoodDays, tylko ze Spendable, z odjeciem rezerwacji innych wozow.
5. Po sprzedazy woz pusty: NoteSale, return false.
6. go = MarketRoad.PickTown(woz, home, v, s, false). Gdy null, a v.Bound jest miastem nieoblezonym i niewrogim: go = v.Bound (wies miejska). Gdy dalej null: Undo(sales), licznik "nieskierowany bez miasta", return true (jak dotad).
7. MarketRoad.Move(Villagers(), woz, go); _onward[woz] = (zamek, godzina); return false. Znacznika _visited nie stawiac - woz, ktory nie byl jeszcze w swoim zamku, moze tam dowiezc jedzenie.

CastleTickPrefix (:453-471):
- Dla wjazdu nieskierowanego straz 24 h zamiast 6 h. Woz czeka w zamku, az wrog odejdzie; straz tylko blokuje odeslanie do domu przez BK (BKPartyBehavior.cs:392-398), nie trzyma wozu.
- Nowy licznik "straz wygasla, woz z ladunkiem" liczony w chwili wygasniecia (:462-466), bo Sweep :607 tego nie widzi.

Liczniki w linii "Targ przy zamku (5.11)":
- wjazdy nieskierowane: z ladunkiem / puste / lodzie;
- cel domyslny rozny od tego zamku (czyli ucieczka);
- jedzenie sprzedane; sztuki zatrzymane w wozie: zelazo, drewno, inne;
- dalej do miasta; bez miasta; straz wygasla z ladunkiem.

Rachunek A:
- W obiegu zostaje ok. 2250-2650 lup/rok = 6-7 lup/d, w II polroczu ok. 9,6/d. To ok. 3% produkcji (218/d).
- To samo dla drewna, lnu, soli, gliny, welny.
- Kasy zamkow zatrzymuja 2,0-2,4 mln zl/rok (5,5-6,6 tys./d).

Ryzyko A: male.
- Zwrot nadplaty 119 (MarketCarts.SellPostfix) i ramka ksiegi towarow dzialaja juz tak dla 12 207 wizyt 5.11 w roku ("niezgodne" 54 na 40 974 sprzedaze).
- Sprawdzic w autotescie: licznik lodzi, licznik "straz wygasla z ladunkiem", "podzamcze zaplacone".
- Prog testu: wizyty taborow w zamkach z pustym wozem po wizycie (linia "Przeplywy osad") spadaja z ok. 8/d do liczby wozow pustych i lodzi.

ZMIANA B (S) - porzucony dobytek nie-zywnosciowy na targ miasta, nie do zamku
Plik: Armoury\src\HorseFate.cs. Klucz SpoilsNonFoodToTown = true.
- NearestMarket (:232-247) dostaje parametr "tylko miasta". Dla towaru handlowego, ktory nie jest jedzeniem (TownFolk.IsFoodCategory), koniem ani uprzeza, szuka po miastach (IsTown) w tej samej kolejnosci: wlasna frakcja -> niewroga -> dowolna. Brak miasta: jak dotad.
- Uzyc w trzech miejscach: Put, galaz "ponad udzwig" (:831) oraz Dest.Shelf bedacy zamkiem (zaloga albo milicja zwyciezca); Spoils, galaz bez zwyciezcy (:884-890); BkPostfix (:981).
- Jedzenie i konie: jak dotad. Podzamcze je zjada, zamki-siedziby kupuja konie.
- Linia "Konie - smierc, lup, dobytek (E6/K1)": rozbic "na polki osad" na zamki i miasta, osobno zelazo i drewno.

Rachunek B: ok. 1200-1350 lup/rok (ok. 3,5/d, w II polroczu 5,7/d) trafia na targ miasta zamiast do zamku. Razem z A doplyw zelaza do zamkow spada z 10,6/d brutto do ok. zera, a zapas zamkow maleje o wlasne zuzycie.
Ryzyko B: male. Przerzut na polke bez platnika istnieje juz dzis, zmienia sie tylko adres (srednio dalej). Ilosc towaru i zlota bez zmian, czas 0.
Jesli Jeff chce zostawic B bez zmian: zapas zamkow rosnie dalej o 3,5-5,7 lup/d i trzeba to przyjac jako skutek zamierzony.

POMIARY DO DODANIA przy A (bez nich reszta to szacunek)
- Powod wjazdu taboru do zamku (ucieczka czy rozkaz) i rodzaj partii (woz czy lodz).
- Dobytek rozbitych wedlug towaru i rodzaju osady.
- Wyjasnienie skoku wjazdow nieskierowanych ok. d155.


### O2-7: zelazo znika bez sladu z taborow lordow i karawan - 1 474 lupy w roku

**Werdykt:** BLAD | **pewnosc:** niska | **podwazenie - broni sie:** True

**Przyczyna:**

Ksiega 'Towary: iron' ma w roku 54 doby z ujemnym 'bez wyjasnienia', razem -1 474 lupy (1,9% podazy; zawsze na minus, nigdy na plus): d1-90 -252, d91-180 -654, d181-270 -304, d271-364 -264. Najwieksze: d165 (dzien 109000) -207 - juki karawan spadly o 245 przy handlu -30 (jedna karawana z ladunkiem znikla); d351 -174, d53 -101, d96 -78, d135 -78 - tyle samo ubylo z taborow lordow bez sprzedazy.
Prawdopodobna droga u lordow (kod gry, nie pomiar): DiscardItemsCampaignBehavior wyrzuca nadbagaz partii lorda (dec32\cs_full\...\DiscardItemsCampaignBehavior.cs:40-49 warunek TotalWeightCarried > InventoryCapacity, :114-126 AddToCounts ujemne) - lord bierze lupy jako lup z wozu albo wsi i gra je kasuje. Nasz kod zna te droge dla koni i uzbrojenia (HorseState.cs:41, ArmsBook.cs:244), dla towarow nie. Dla karawany z d165 drogi nie ustalilem (rozwiazanie albo zniszczenie bez zdarzenia lupu).
To 4 lupy/d - nie zmienia liczby miast bez zelaza, ale lamie zasade 'nic w nicosc'. W tej samej linii bilansu inne towary gubia wiecej (doba 364: razem -1 144 szt., zboze -308, ryby -177) - to wspolna sprawa calej ksiegi towarow, poza moim obszarem.

**Poprawka:**

Najpierw pomiar (S, sam log): Armoury\src\GoodsLedger.cs - ramka ksiegi wokol DiscardItemsCampaignBehavior (prefiks i postfiks jak w HorseState.cs:434) i przy usunieciu partii z niepustymi jukami poza bitwa; w linii 'Towary (bilans)' nowe pozycje 'wyrzucone przez gre u lordow' i 'partia usunieta z towarem' wedlug towaru. Decyzja co z tym zrobic (np. lord zostawia nadbagaz na polce najblizszej osady zamiast go kasowac) - dopiero po liczbach z biegu, razem dla wszystkich towarow. Bez zmian w dzialaniu gry teraz.


**Podwazenie - uwagi:**

WNIOSEK SIE BRONI (werdykt BLAD = przeciek w kodzie gry, ktorego nasze mody nie domykaja), ale z poprawkami. Pewnosc podnosze z niskiej na srednio-wysoka; bezposredniego pomiaru towaru w tej drodze nadal nie ma.

1. RACHUNEK POTWIERDZONY (bieg roczny, 363 doby z bilansem): 54 doby ujemne, razem -1474 lupy, zero dob dodatnich; 1,85% podazy (79 553), 4,1/d. Kwartaly i najwieksze doby zgodne (109000 -207, 109186 -174, 108888 -101, 108931 -78, 108970 -78). Trend bez narastania, okna 60 dob: -171 / -231 / -515 / -206 / -88 / -263 (0,7-3,2% podazy). W czterech biegach po 119 dob to samo zjawisko: -46, -180, -357, -80, zawsze tylko na minus.

2. DOWODY ZA DROGA DiscardItems, ktorych autor nie mial:
a) Ta sama metoda gry kasuje co dobe konie lordow: linia "Marsz - konie partii (E6/K0)" - w roku 8830 koni "ponad udzwig" (24,3/d) i 3468 "nadmiar stada". Droga jest wiec czynna codziennie, a nie hipotetyczna.
b) Korelacja dzienna (363 doby) miedzy konmi "ponad udzwig" a suma "bez wyjasnienia" 25 towarow nie-zywnosciowych: r = 0,61 (doby z <=10 koni: srednio 29 szt. straty, z >=30 koni: 98).
c) Rozdzial zelaza wedlug posiadacza (zmiana stanu minus handel): 17 dob, gdy traci WYLACZNIE klasa lordow, -374; 21 dob, gdy lord traci i w tej dobie cos zabrano karawanie albo wozowi, -595; 14 dob, gdy lord zyskal czesc lupu, a reszta znikla, -297; 2 doby bez zmiany u lordow, -208. Razem z lordami 1266 z 1474 (86%).
d) Lup band jest zachowany, lup lordow nie: paser przeniosl w roku 4858 lup z band na polki miast (OutlawLaw.cs:1960-2075, sztuka za sztuke), a lordowie z ok. 3390 zdobytych lup sprzedali 1807, maja 110, zgubili 1474. DiscardItems dotyczy tylko partii lordow z wodzem (DiscardItemsCampaignBehavior.cs:19).
e) Wykluczone inne drogi: "bez miejsca" w linii K1 = 0 przez 364 doby (HorseFate.Put :833 nie gubi); "przepadlo z rozbitymi partiami" dla zelaza 4 lupy w roku; blad ksiegi przy rabunku (AUDYT [sztuki] DistrictRaid) jest w tym kodzie naprawiony - 0 falszywych wpisow dla zelaza; skan 263 DLL: zaden mod poza Armoury nie dotyka DiscardItemsCampaignBehavior; zwycieska karawana - handler BK biegnie przed handlerem gry (MbEvent.cs:24-30, nowy nasluch idzie na poczatek listy), nasz postfiks zlapal 418 szt. w 21 dobach.
f) Ramki ksiegi obejmuja tylko dobowe dyspozytory (GoodsLedger.cs:1147-1149), a kasowanie jest godzinowe (DiscardItems :14) - dlatego laduje w "bez wyjasnienia".

3. PRZYCZYNA GLEBIEJ NIZ W WNIOSKU: gra rozdaje lup pokonanego sztuka po sztuce z szansa ok. 50% BEZ sprawdzania udzwigu (MapEvent.cs:1595-1632, DefaultBattleRewardModel.cs:282-320), a nasza regula dobytku udzwig sprawdza (HorseFate.cs:824, nadmiar na polke). Lord przeladowany czescia gry traci nadmiar w najblizszej godzinie, od konca listy. Lupa wazy 15 kg, woz 100-250 lup to 1,5-3,7 t.

4. DOBA 109000 WYJASNIONA (autor: "drogi nie ustalilem"). Okno tej linii ksiegi to 02:51:04-02:51:19 czasu logu. W oknie przegraly trzy karawany: Caravan of Brogg z bandami Robber Knights (02:51:07), Caravan of Xanda the Wool Weaver z lordem Black Rat (02:51:16), Armed Caravan of Kermit z lordem Harys Swyft (02:51:17). Bilans: karawany -215, wozy i wsie -68, bandy +39 (sprzedane paserowi w nastepnym ticku), polki miast +37, zniklo 207. Zadna karawana nie "znikla bez zdarzenia" - lup wzieli lordowie i wyrzucili przed koncem doby. To wnioskowanie z czasow bitew, nie pomiar.

5. BLEDY W WNIOSKU:
- "Nasz kod zna te droge dla koni i uzbrojenia" - tylko dla koni (HorseBook.Cat, ArmsBook.cs:274-284: ItemType == Horse). Uzbrojenie z taboru lorda tez znika niepoliczone.
- "prefiks i postfiks jak w HorseState.cs:434" - tam sa same prefiksy.
- "partia usunieta z towarem" jako nowy pomiar jest zbedna: istnieje (GoodsLedger.cs:801-825, pozycja "przepadlo z rozbitymi partiami").
- Sam pomiar zostawia przeciek otwarty na kolejny bieg (80 min), a nie jest potrzebny osobno - patrz poprawka.

6. PULAPKA DAT: linie "Konie - smierc, lup, dobytek", "Paser", "Lup i dary" maja numer dnia o 1 wiekszy niz "Towary" i "Marsz - konie partii" z tego samego ticku (HorseFate.cs:1338 bierze Now, ksiega Now-1). Przy zestawianiu trzeba przesunac.

7. OBOK WNIOSKU, WIEKSZE OD NIEGO: zelazo zalega na polkach ZAMKOW - 298 lup w dobie 1, 3654 w dobie 364 (+9,2/d, 25% zapasu swiata), zuzycie w zamkach 0. To ponad dwa razy wiecej niz przeciek. Zasilaja je wozy wsi i nadmiar dobytku (NearestMarket bierze tez zamki, HorseFate.cs:235); podzialu nie zmierzylem. Do sprawdzenia w obszarze zelaza.

8. Dla koni (obszar E6, nie sprawdzalem czynnego modelu): wedlug modelu gry kon w taborze wazy 0, a juczny daje +100 kg udzwigu (DefaultInventoryCapacityModel.cs:41-45, :79), wiec kasowanie koni "ponad udzwig" nie odciaza, tylko zmniejsza udzwig.

Skrypty i wyciagi: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\triaz3\podw-o2-7\ (parse.py, a1-a11.py, rok.pk).

**Poprawka ostateczna:**

JEDNA PACZKA ZAMIAST "NAJPIERW POMIAR": to, co gra kasuje lordowi, idzie na polke najblizszego przyjaznego MIASTA - ta sama regula co juz przyjeta dla dobytku ponad udzwig zwyciezcy (E6/K1, R14). Latka przenosi dokladnie tyle, ile gra zdjela, wiec sama jest pomiarem. Wielkosc M (5 plikow, 1 recenzja), tor z autotestem.

1. Armoury\src\HorseState.cs :434-441 - do istniejacych prefiksow na DiscardItemsCampaignBehavior.DiscardNecessaryAmountOfItems i DiscardAnimal dopisac postfiks (MobileParty mobileParty, ItemRosterElement itemRosterElement): zniklo = itemRosterElement.Amount (stan sprzed, struktura przez wartosc) minus stan stosu teraz (FindIndexOfElement / GetElementNumber). Gdy zniklo > 0 -> HorseFate.Discarded(partia, element, zniklo, stado czy udzwig). Wywolanie HorseBook.NoteDiscard przeniesc z prefiksow do tej metody.

2. Armoury\src\HorseFate.cs - nowa metoda Discarded:
- przedmiot NotMerchandise, choragiew, zadaniowy albo unikat (te same wyjatki co :900): oddac z powrotem do taboru lorda, licznik "zostalo";
- inaczej polka: nowa NearestTown(pozycja, frakcja) = regula NearestMarket (:232-248: ta sama frakcja i nieoblegane, dalej niewrogie, na koniec dowolne), ale tylko st.IsTown - zamek jest slepym magazynem (zelazo w zamkach 298 -> 3654 w roku);
- m.ItemRoster.AddToCounts(element, zniklo); bez zaplaty (porzucony dobytek, jak R14);
- brak miasta: licznik "bez polki" i strata zaksiegowana z przyczyna.
Nowy fragment linii "Konie - smierc, lup, dobytek (E6/K1)": "porzucone przez lordow (gra): ponad udzwig X, stado Y; towar / zywnosc / konie / uzbrojenie; na polki miast N, zostalo w taborze Z, bez polki 0".

3. Armoury\src\GoodsLedger.cs - NoteDiscard(przedmiot, n, naPolke): na polke -> nowa pozycja przewozu "lordowie porzucili na polki N" w linii kazdego towaru (TDiscard, Transfers 8 -> 9); bez polki albo klucz wylaczony -> ujscie z nazwa "porzucone przez lordow (gra)" w SinkOrder. W obu przypadkach "bez wyjasnienia" tej drogi schodzi do zera i sa liczby wedlug towaru - to jest pomiar, o ktory prosil autor.

4. Armoury\src\ArmsBook.cs (HorseBook.NoteDiscard :331) - kon przeniesiony na polke to nie ujscie: osobny licznik przeniesienia, ujscie tylko dla "bez polki". Bez tego ksiega koni liczylaby dwa razy.

5. Armoury\src\Settings.cs - klucz LordDiscardToShelf (bool, domyslnie true; false = jak dzis, ale z liczeniem w ksiedze), potem python tools\gen_mcm.py. tools\sprawdz_logi.py: nowe progi - "bez polki" = 0; zelazo "bez wyjasnienia" w biegu rocznym > -150 (dzis -1474); 25 towarow nie-zywnosciowych > -2000 (dzis -20 136); poprawic odczyt linii K0 "konie porzucone".

LICZBY (skutek): zelazo +4,1 lupy/d na polki miast (1474 w roku, 1,85% podazy) - liczby miast bez zelaza to nie zmieni; towary nie-zywnosciowe od 27 do 55 szt./d (dolna granica = spadki widoczne u lordow 9824 w roku, gorna = cale "bez wyjasnienia" 20 136); konie 34/d (12 298 w roku, zmierzone); zywnosc nieznana - pokaze ja ta sama linia.

KONTROLA ZASAD: nic z niczego - przenosi sie tylko sztuke, ktora gra wlasnie zdjela (roznica stanu stosu), bez zlota. Inne systemy: lord traci to samo co dzis, wiec AI i tempo marszu bez zmian; gracz nietkniety (gra kasuje tylko lordom AI). Szybkosc: postfiks biegnie tylko przy faktycznym kasowaniu, ok. 50-150 razy na dobe, kazde to jedno przejscie po 227 warowniach - ponizej 1 ms na dobe przy 13 000 ms.

RYZYKO: (1) 34 konie/d wracaja na targ zamiast znikac - zmienia bilans koni E6 (podaz w gore, regulator hodowli K4 zareaguje); progi E6 obejrzec w pierwszym biegu, wlasciciel obszaru koni powinien to potwierdzic. (2) Jesli wiekszosc "bez wyjasnienia" zywnosci (ok. 815 szt./d) to tez ta droga, miasta dostana duzo darmowej zywnosci - pierwsza linia pomiaru rozstrzygnie; klucz pozwala wrocic. (3) Lord dalej bywa przeladowany lupem gry; to zostaje, bo latka nie zmienia rozdania lupu.

ODRZUCONE: (a) sam pomiar (propozycja autora) - przeciek otwarty przez kolejny bieg, a ramka na HandlePartyInventory to 15 600 otwarc na dobe bez potrzeby; (b) zablokowanie kasowania - lordowie zostaliby przeladowani i wolni do najblizszego miasta, gorsza wojna AI.

OSOBNO, POZA TA PACZKA: zaleganie zelaza w zamkach (+9,2 lupy/d) i wybor zamku przez NearestMarket w istniejacej regule R14 - wieksze niz ten przeciek, wymaga wlasnego rozpoznania.


### O2-4: geografia - ktore miasta sa stale bez zelaza i czy tak bylo wczesniej

**Werdykt:** BRAK_DANYCH | **pewnosc:** srednia

**Przyczyna:**

STALE BEZ ZELAZA w biegu rocznym (udzial 55 probek d91-364): Omber 100%, Driftwood Hall 98, Mhysa Faer 96, Mormont Keep 96, Ibben 95, Myrth 93, Lys 91, Aquos Dhaen 89, Tyrosh 87, Elyria 87, Lonely Light 87, Bolozo 84, Valysar 82, Pebbleton 82, New Ghys 82, Lotus Bay 80 (16 miast = 14,3 z 40,1); dalej 60-78%: Sisterton, Myr, New Ibbish, Celtigar Keep, Selhorys, Hull, Kyth, Pentos, Volon Therys, White Harbor, Rhyos, Sharp Point, Qarkash, Karhold, Evenfall Hall, Braavos, Vaith, Ifequeveron, Lorath, Tolero Aeksio, Volantis (21 miast = 14,5). Pozostale 60 miast daje razem 11,3.
To glownie wyspy i miasta za morzem (Skagos, Wyspa Niedzwiedzia, Ibben, Lys, Tyrosh, Elyria, Nowe Ghis, Wielka Wyk, Samotne Swiatlo, Trzy Siostry, Driftmark, Szpon, Tarth, Wyspy Letnie) oraz pld.-zach. Essos bez kopalni w poblizu (Myr, Myrth, Pentos, Bolozo, Selhorys, Valysar, Volon Therys). Odleglosc do najblizszej kopalni (settlements.xml ROT-Map, linia prosta): 81 miast do 200 - srednio 35% dob bez zelaza; 15 miast 200-400 - 72%; Ibben 408 - 95%. Gra sama liczy 16 miast bez drogi ladowej do innego miasta (linia 'Karawany (E8/G1)': 'slepe zaulki mapy dla karawany bez statkow: miasta 16') - nazw w logu nie ma.
NIE TYLKO DALEKO: White Harbor (189 w linii od Barrowton z 2 442 lupami) 69% dob bez zelaza, Karhold 67%, Flint's Finger 45%, Deepwood Motte 42%; Pebbleton lezy 39 od kopalni Saltshore i ma 82%.
CZY TAK BYLO: log W33 nie istnieje (rotacja), wiec porownania miasto po miescie z W33 nie da sie zrobic. Zastepczo: w 5 biegach 120 dob (rok, test koncowy, limit 3, bieg 2, bieg 1; okno d61-125) ta sama jedenastka ma >= 67% w kazdym biegu: Aquos Dhaen, Driftwood Hall, Hull, Ibben, Lonely Light, Mhysa Faer, Mormont Keep, Myrth, New Ghys, Pebbleton, Sisterton - takze w biegu 1, gdzie kontrakty jeszcze dzialaly (10,6-13,1/d, w miastach stalo 300 karawan). W biegu 728 dob (kod etapu 4) czolowka ta sama: Ibben 123/127 probek, New Ibbish 122, Sisterton 122, White Harbor 116, Omber 112, Celtigar Keep 108, Lonely Light 105, Myrth 105. Wniosek: wyspy nie dostawaly zelaza nigdy; dokument TRIAZ-zb58 podaje, ze W33 nie zszedl ponizej 19 - to jest ten zbior.
Miasta kopalni Zatoki (Yunkai 9%, Meereen 11%, Tolos 20%, Mantarys 22%, Vaes Diaf 29%) po wozach dymarek F4a maja zelazo przez wiekszosc roku; znikaja dopiero po cieciu regulatora (O2-2).

**Poprawka:**

Bez osobnej zmiany - to zakres O2-1 (woz kupcow jedzie ladem i morzem, wiec obejmuje wyspy).
Brakujace pomiary (S, sam log, plik MaterialOrders.cs, linia 'Miasta bez rudy i strzal (174b)'): (1) powody 'bez kontraktu' osobno dla zelaza (dzis jeden licznik dla 7 surowcow); (2) przy kazdym miescie bez zelaza: najblizsze zrodlo z nadwyzka, droga ladem / morzem i powod odmowy; (3) pelna lista zrodel zamiast 15 najwiekszych; (4) nazwy 16 miast bez drogi ladowej. Z tym kolejny bieg pokaze, ktore miasta zostaja po poprawce i dlaczego.
Narzedzie: prog R1-8 liczyc z calego okna od d91 i dodatkowo osobno dla listy wysp; nie porownywac z 26,7 z W33 (jedno okno 30 dob) - dlugi bieg starego kodu dawal 34-47.


### O2-5: karawany woza 2 600 lup i sprzedaja 35 na dobe (pokrycie brakow x6)

**Werdykt:** ZAMIERZONE | **pewnosc:** wysoka

**Przyczyna:**

Regula CaravanBulk: karawana kupuje z nadwyzki, dopoki wszystkie karawany razem woza mniej niz 6 x brak miast (CaravanBulkTransitCover = 6, Settings.cs:660; bramka CaravanBulk.cs:813), a sprzedaje tylko w miescie ponizej zapasu docelowego (:601; zapas = 10 dob zuzycia, nie mniej niz 8 lup). Opis klucza sam mowi: 'wiecej towaru w drodze'.
Przebieg roku (linia 'Karawany (kierunek)' i '(stan)'): w jukach 206 (d1-30) -> 823 -> 1 922 -> 2 207 i dalej 2 270-2 800 do konca; przy braku 349-694 lup to 4-6 x brak, czyli pod sufitem 6 - poziom staly od ok. d75. Zelazo wiezie 47-122 karawan (srednio 22-55 lup na karawane); w karawanach stojacych w miastach tylko 10-40 lup, wiec 13 karawan stojacych rok w Pyke (linia 'Wyspy i drogi': 'Pyke 13, najdluzej 363.0 dob') nie trzyma zelaza - to osobna usterka handlu Pyke, nie ta sprawa. Obrot: 2 600 lup przy sprzedazy 35/d = ok. 75 dob w jukach. Limit karawan 2 albo 3 nie zmienia liczby miast bez zelaza (44,3 wobec 45,1), bo docieraja tylko tam, gdzie da sie dojechac ladem.
DO JAKIEGO POZIOMU ZMIERZA: stabilny, 2,2-2,8 tys. lup w drodze (18% zapasu swiata).

**Poprawka:**

Nie ruszac. Po poprawce O2-1 braki miast spadna, wiec zadziala istniejaca regula 'w drodze jest wiecej, niz swiat potrzebuje' (CaravanBulk.cs:372-375, :601): kupno staje, a karawany rozladowuja zelazo w kazdym miescie do progu nadwyzki - te 2,6 tys. lup samo wroci na polki. Obnizenie pokrycia z 6 do 3 rozwazac dopiero wtedy, gdy po biegu z poprawka w jukach nadal bedzie lezalo ponad 2 tys. lup przy mniej niz 15 miastach bez zelaza (S, sam klucz).


### O2-6: lupy niewytopione z braku wegla i cykle 'brak drewna' - rozruch, potem stabilnie

**Werdykt:** ZAMIERZONE | **pewnosc:** wysoka

**Przyczyna:**

Lupy bez wegla wobec modelu kopaln w oknach 30 dob: 36% / 19% / 11,7% / 9,3% / 5,5% / 2,6% / 2,7% / 2,4% / 3,5% / 2,2% / 3,1% / 3,3% (w sztukach 103 -> 54 -> 33 -> 25 -> 15 -> 7 -> potem 4-7 lup/d). Wynik narzedzia 5,2% (d61-364, prog 5) i 7-14% w biegach 120 dob to rozruch pierwszych 150 dob (regulator drewna decyduje pierwszy raz w d56, wozy dymarek jada srednio 5-6 dob), a nie stan trwaly. Zostaja stale te same kopalnie: Tylemos [Tolos] 189 dob z 304, King's Valley [Stonehelm] 118, Harridan Hall 113 - z pusta kiesa wsi (d364: 'Tylemos kiesa 0, drewno po 10 zl'; brak zlota wsi + pusta kiesa = 5,3% potrzeby drewna).
Cykle warsztatow 'brak surowca [drewno': 563 / 365 / 321 / 298 / 151 / 100 / 66 / 62 / 59 / 44 / 56 / 60 na dobe - schodzi i stoi na ok. 60 (prog 177).
To 4-7 lup/d z ok. 200 - nie ono trzyma miasta bez zelaza. Poziom docelowy: 2-3,5% lup bez wegla, ok. 60 cykli/d bez drewna.

**Poprawka:**

Bez zmian w grze. Narzedzie: prog R1-7 (<= 5%) liczyc od d151 (stan ustalony), d61-150 jako INFO - inaczej kazdy bieg 120 dob da NIE z samego rozruchu.
Spodziewany skutek uboczny O2-1 (do sprawdzenia w biegu, nie obiecuje): gdy wozy kupcow zdejma nadwyzke z Tolos i Stonehelm, lupa bedzie tam drozsza niz dzisiejsze 12-25 d, wiec kopalnie Tylemos i King's Valley beda mialy za co kupic drewno.
