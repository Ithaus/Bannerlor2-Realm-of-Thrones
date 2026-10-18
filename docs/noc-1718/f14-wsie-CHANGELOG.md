## 2026-10-18 (paczka f14-wsie - NDS-3a fin92: kiesy wsi 6.14 -> 5.46 mln = -0.466%/d; "dokad ida pieniadze wsi i skad maja wracac") - ROZPOZNANIE + plik wsie-kiesy.csv + czytnik; DECYZJA Jeffa (DS przed KS)

**Mod:** Armoury - tylko diagnostyka (nowy plik `wsie-kiesy.csv`, zadnej zmiany zlota ani logiki gry) + czytnik `tools/sprawdz_logi.py` (grupa nds, wiersze NDS-3) | **Galaz:** `w-toku/f14-wsie` (od 8cd7b556 = test fin92) | **Tor:** bez gry (analiza logow fin92 `Armoury-2026-10-18_09-33-39.log` i fin91 `Armoury-2026-10-18_06-29-09.log`; build Armoury kod 0) | **Klucz:** zaden nowy (zadna domyslna nie zmieniona, `gen_mcm` niepotrzebny; plik pisany przy wlaczonym logu) | **Zapis:** bez zmian.

**Dla Jeffa (prosto):**
- **To jest rozpoznanie, nie naprawa.** W tej paczce gra liczy pieniadze wsi tak samo jak dotad. Dochodzi jeden plik do logow (kiesa kazdej wsi co 7 dob, z rozbiciem: skad przyszlo, na co poszlo) i dokladniejszy czytnik.
- Skad spadek kies wsi: przez pierwsze ok. 50 dob nowe daniny (DS) dzialaja juz od pierwszego dnia, a koszyk wsi kupowany w miescie (KS) rusza dopiero po 28 dobach pomiaru i 14 dobach rampy. Wsie przestaly wtedy oddawac stare pobory, a nie mialy jeszcze na co wydac. Kiesy wszystkich wsi urosly z 3.70 do 7.46 mln i od konca rampy KS schodza.
- **W fin92 kiesy na koncu biegu dalej spadaja, ok. -0.22%/d** (prog -0.2%/d). Nie sa jeszcze w rownowadze. W fin91 kiesy stoja (-0.01%/d).
- **Przybywa wsi bez grosza, a suma kies stoi.** W ostatnich 28 dobach przybywa ok. 0.5-0.7 takiej wsi na dobe (fin92 51 -> 66 wsi, fin91 67 -> 81). Ksiegi zbiorcze nie mowia, czy pieniadze plyna od biednych wsi do bogatych albo do panow. To pokaze nowy plik w nastepnym tescie.
- **Pytanie do Ciebie (DECYZJA):** czy daniny DS maja ruszac dopiero razem z koszykiem KS (tak mowil projekt DS), czy zostaje jak jest (jednorazowa gorka kies wsi po wlaczeniu, schodzi przez ok. 4-5 miesiecy gry)? Szczegoly nizej. W tej paczce nic tu nie zmieniam.

**Problem (dowody z logu):**
- fin92 `NDS-3a: kiesy wsi - dryf 26 dob od doby 109169 ... | NIE | 6.14 -> 5.46 mln = -0.466%/d` (fin91: -0.381%/d). Czytnik wolany przez orkiestratora (`--baza-doby`, bez `--do-doby`) widzial tylko doby 109075-109194 ze 160.
- Seria "Przeplywy osad (kiesy wsi)" (stan): start 109076 3.70 mln, szczyt 109132 7.46 mln, 109172 6.06, 109196 5.37, 109220 5.01, koniec 109234 4.98 mln. fin91: 3.69 -> 6.94 (szczyt) -> 5.04-5.06 od 109204 do konca.
- DS F1 od 109076 (`NDS-13 DayOn [109076.0]`), stare pobory slabna przez `DsRampDays` 28 (do 109104).
- KS: pomiar przed wlaczeniem od 109076 ("srednia ksiegi towarow N/28 dob"), wlaczenie A 109111. Linia "KS: rampa dzien 109110 | doba rampy 0 (A 109111, D 109125, koniec popytu 109139)".
- Koszyk wsi ZC4 (linia "Koszyk stanow (ZC4)", "wsie: zakupow N za X zl"): 0 zl/d do 109124, 14383 (109128), 48084 (109132), 80681 (109136), 115368 (109140), potem 100-125 tys./d. Do lambdy ZC4 wsie kupuja tylko stary koszyk BV.
- Dryf MNK 26 dob wedlug doby konca okna (fin92): 109226 -0.235%/d, 109229 -0.243, 109230 -0.242, 109231 -0.234, 109232 -0.221, 109233 -0.214, 109234 -0.191. Ostatnia doba (+74 631) sama przesuwa wynik ponad prog. fin91: od -0.006 do -0.012%/d, niezaleznie od konca okna.
- Wsie z kiesa < 1000 zl (linia "rozklad [P] ... ponizej 1000 zlota N"): fin92 start 109076 60 z 571 (10.5%), dolek konca rampy 11-14, koniec 66. fin91 start 61, w 109203-109234: 59 -> 69 -> 76 -> 79 -> 81 przy kiesach 5.02-5.06 mln.

**Gdzie ida pieniadze wsi (fin92, srednia 26 ostatnich dob 109209-109234, kiesy 5.01 mln; ksiega "Przeplywy osad (kiesy wsi)" + linie zrodlowe) - SUMA wszystkich wsi:**

| Wplywy do kies wsi | zl/d | Skad / dowod |
|---|---|---|
| utarg wozow - czesc wsi | +144 891 | wozy oddaly 259 642 (kasy miast i zamkow za plon); pan (licznik podatku) 91 729 = 35%, majatki BK 23 055 = 9%, wies 55.8% ("Utarg wsi (112)") |
| markietani (zolnierze w polu kupuja we wsiach) | +30 103 | sakiewki -> wsie 48 632, z tego danina pana E3-G2 15 281 na licznik podatku |
| sakiewki zwolnionych (166) | +14 377 | zolnierze zwolnieni do wsi wracaja z sakiewka |
| przelewy gry (lordowie, notable) | +12 765 | GiveGoldAction |
| fracht KS-H | +322 | wiesniacy wioza jedzenie kupcow |
| **razem** | **+202 458** | |

| Wydatki z kies wsi | zl/d | Dokad / dowod |
|---|---|---|
| koszyk wsi ZC4 (jedzenie i rzeczy z polki miasta targowego) | -105 356 | prawdziwy towar -> kasa miasta (pozycja MFolk) |
| danina od skarbca wsi (DsHoardDueVillages, 3%/d ponad 3 x skrzynia) | -27 390 | panowie 18 349, kasy osad 9 041 ("Akumulator kies") - maleje z kiesa: 69.7 tys. przy 7.0 mln, 27.4 tys. przy 5.0 mln |
| drewno dymarek (wsie gornicze) | -5 317 | targ 1 147 + wozy 4 170 -> kasy miast (linia "Zelazo (lupy) (214)") |
| zbytki z nadwyzki (krok 5, towar) | -26 096 | prawdziwy towar -> kasy miast (MBuild) |
| roboty gminy (material + rzemieslnicy) | -19 553 | material 13 049 -> kasy targow, rzemieslnicy 6 504 -> lud targow |
| septy z dziesieciny | -3 327 | material 832 + rzemieslnicy 2 495 (polowa dziesieciny - jalmuzna - zostaje w kiesie) |
| czynsz DS (W3) | -10 634 | panowie ("Prawa panskie": oplaty stale wsi) |
| udzial korony przy wozie (W4) | -13 464 | skarbce krolestw (reszta [R] ksiegi = N169Dues w "Przyczyny") |
| **razem** | **-209 910** | **netto -7 452/d** (ksiega: -7 470/d) |

- Ksiegi zbiorcze sie zamykaja: `NDS-2b` reszta kies wsi po nowym pomiarze 218/d, `NDS-2d` reszta bilansu swiata -308/d, `NDS-14a` utarg DS / 112 = 1.000. **To wyklucza zloto znikajace w nicosc, ale NIE wyklucza stalego przeplywu miedzy wsiami** (od biednych do bogatych albo do panow) - stad plik wsie-kiesy.csv.
- **Pytania z zadania (suma wsi):**
  - kupcy KS-H: fracht +0.3 tys./d. Kupcy kupujacy we wsiach: 6.9 tys./d, w calosci do kies wsi (linia "Utarg wsi (112)": "oddane w calosci kiesom wsi (jak zakup gracza)", zniklo 0).
  - daniny DS: czynsz 10.6, korona przy wozie 13.5, dziesiecina netto 3.3 tys./d (jalmuzna zostaje).
  - renty: czynsz to pozycja "renty" ksiegi.
  - roboty i odbudowa: 22.9 tys./d. Robote wsi robi gmina wlasnymi rekami, pieniadz idzie tylko za prawdziwy material i rzemieslnikow.
  - pobor i werbunek: oplata za ochotnikow 7.1 tys./d idzie do notabli, nie do kies wsi ("Werbunek"). Wraca przez sakiewki zwolnionych 14.4 tys./d.
  - uchodzcy RT: kilkadziesiat zl/d. Niewola: brak przeplywu ze wsi. Turnieje: sakiewka turniejowa idzie z kies ludu miast, nie ze wsi.
  - weglarze (`NWE-2b` 0 zl): **tak ma byc**. To rece tej samej wsi gorniczej, a drewno z lasu jej okregu idzie prosto do jej dymarki; zaplata to utarg za lupy, ktory wraca do tej samej kiesy.
  - pozyczki miejskie f8: **wsie nie sa zrodlem kapitalu**. `NPZ-2`: zrodla to kiesy W/R/P, kasy kupcow i podzamcza. `NPZ-12`: do kies wsi 0 zl/d.
- **Dlaczego fin92 gorzej niz fin91 (te same 26 dob):** wplywy -7.0 tys./d (markietani -6.0, sakiewki zwolnionych -4.8, przelewy gry +3.8). Zold dnia 583.7 tys. wobec 642.2 tys. (-9.1%), sakiewki -> markietani 325.8 tys. wobec 395.1 tys. (-17.6%). Rody maja nizszy dochod staly: `NDS-9` 0.517 wobec 0.767 w fin91 (fala 6/7). Danina od skarbca wsi sama zmalala (27.4 wobec 32.5 tys.).

**Przyczyna (dwa mechanizmy gry - znalezione, w tej paczce NIE zmieniane):**
1. **DS rusza przed koszykiem KS (rozjazd startu, DECYZJA).** DS F1 dziala od pierwszej doby z kluczem (`DuesMedieval` domyslnie wlaczony), stare pobory resztowe kies wsi slabna przez 28 dob. Koszyk wsi ZC4 rusza dopiero z lambda KS (D 109125 - 109139), bo KS czeka 28 dob na pomiar ksiegi towarow. Naglowek `DsVillage.cs` sam to mowi: "bez koszyka KS wies nie ma na co wydac uwolnionych pieniedzy", a odstepstwo od projektu brzmi "DS dziala BEZ kalibracji startu KS ... On(st) nie czyta StartCalibration.AppliedFor". Skutek: nadwyzka ok. 3.8 mln w kiesach wsi.
   - Zejscie nadwyzki ma stala 56-63 dob (odplyw krancowy 1.58-1.77%/d z blokow 14 dob, `NDS-3f`), nie 15. Krok 5 wydaje ok. 1/5 planu (fin92: plan 223 tys./d, towar 26 + roboty 20 tys., reszta "niewydane" i "potrzeba pokryta"), danina od skarbca bierze tylko ponad 3 x skrzynia.
   - fin92 jest w 160. dobie dalej ponad rownowaga (szacunek `NDS-3f` S* 4.26 mln; dzis 4.98 mln, -0.22%/d). To nie jest stan ustalony.
   - To samo stanie sie u Jeffa przy wczytaniu zapisu bez KS zastosowanego (bieg testu startuje wlasnie tak: "KS: pomiar przed wlaczeniem dzien 109076 | stan UZBROJONY").
2. **Ogon biednych wsi rosnie przy plaskiej sumie (przyczyna nieznana).** Ok. +0.5..+0.7 wsi/d bez oznak zatrzymania (fin91 przy kiesach 5.02-5.06 mln). Podejrzenie: utarg wozu ponizej koszyka i czynszu w czesci wsi (tanie zboze w miescie targowym, krainy wojny, wsie spladrowane - z `ks-miasta.csv` 109165-109228 korelacja dryfu kies targu z indeksem zboza tylko +0.22). **Nie sprawdzone wies po wsi** - log nie mial kiesy pojedynczej wsi.

**Zmiana:**
- **Armoury - plik `wsie-kiesy.csv` (nowe `Armoury/src/MoneyLedger.Villages.cs`; haki: `MoneyLedger.Reset/BlockOpen/Mark/Daily`, `VillageTakings.EnterPostfix -> CartCsv`):**
  - Co 7 dob (doba podzielna przez 7 przy `LogGate.Lines`, jak `ks-miasta.csv`) wiersz na wies: kiesa na koncu i na poczatku okna, zmiana okna, skrzynia DS tej doby (`DsVillage.ChestToday`), stan wsi (normalna / najezdzana / spladrowana), miasto targowe (`TradeBound`, inaczej `Bound`) i jego oblezenie, typ wsi, ogniska, krolestwo.
  - Zmiana kiesy w naszym ticku rozbita na pozycje ksiegi przeplywow - te same co w linii "Przeplywy osad (kiesy wsi)": migawka kiesy kazdej wsi przy `BlockOpen`, przy kazdym `Mark` i w `Daily` (reszta bloku - `MRest`), jak migawki klas w `Snap`. Kolumny: renty, budowy, korona, budzet 166, markietani, kiesa ludu, KS, KS-H, ZC6, inne.
  - `poza_tickiem` = zmiana okna - suma pozycji ticku: utarg wozow po udziale pana i daninach przy wozie, przelewy gry, sakiewki rozbitych partii.
  - Woz: liczba powrotow, gotowka oddana przez wozy i przyrost kiesy netto po wszystkich krokach okna powrotu (112, F4b, prace okregu, daniny DS, splata drewna f5). Liczone dla wsi, ktorej kiesa urosla: macierzystej, a gdy woz oddal utarg w obcej wsi - tej obcej.
  - Linia logu przy kazdym pliku: `Kiesy wsi wies po wsi (f14-wsie): wsie-kiesy.csv N wierszy (doba D, okno K dob) | zmiana kies okna X, w tym poza tickiem Y | potkniecia ...`.
  - Tylko odczyt: zadnego `ChangeGold`. Zbieranie tylko przy `Settings.LogEnabled`, kazdy krok we wlasnym try, potkniecia liczone, pierwszy wyjatek do logu raz (bez globalnych wylacznikow).
- **Czytnik `tools/sprawdz_logi.py`, `nds_main`, NDS-3:**
  - **NDS-3a / NDS-3b** (recenzja): nachylenie MNK stanu kies wsi w **5 oknach 26 dob konczacych sie w 5 ostatnich dobach okna** (nie wczesniej niz koniec rampy + 2 x 15). Prog musi przejsc **w kazdym oknie**, wiec jedna doba nie przestawia wyniku. Wiersz pokazuje wszystkie 5 wartosci, mediane i najgorsza. Okno krotsze niz 30 dob - tyle okien 26 dob, ile sie miesci (przy 120 dobach fin92 - jedno okno 109169-109194).
  - **Okno NDS-3** (recenzja): bez `--do-doby` - do konca logu (NDS-3 to stan kies samego biegu, bez bazy). Z `--do-doby N` - jak reszta grupy. **Reszta grupy zostaje przy 120 dobach**, bo NDS-7b, NDS-9 i NDS-11 porownuja z baza 120 dob. Przy oknie 160 dob traca odniesienie bazy (fin92 `--do-doby 160`: NDS-9 "pierwsze doby DS tego biegu ... bez bazy - slabe odniesienie", wiersz NDS-11 znika - baza nie ma tych dob; razem NIE 4 zamiast 5 tylko przez slabsze odniesienie).
  - **NDS-3a0 (INFO):** pomiar od konca rampy do konca okna (dwa konce) - przejscie. Liczba dob = dlugosc calego okna przed cieciem (recenzja: dotad zla w oknie zastepczym).
  - **NDS-3f (INFO):** rownowaga z blokow 14 dob od konca rampy (bez zmian wzgledem pierwszej wersji paczki).
  - **NDS-3g (INFO)** (recenzja): wsie z kiesa < 1000 zl - **7 pierwszych dob okna** -> 7 ostatnich (jak NDS-3e), koniec rampy osobno, oraz MNK liczby takich wsi z 28 ostatnich dob (czy ogon dalej rosnie).
  - **NDS-3h (INFO, nowy):** z `wsie-kiesy.csv` (katalog CSV obok logu przez `session_file_find`, albo `--csv <katalog>/budzet-rodow.csv`) - ostatnie 4 okna pliku: wsie z kiesa < 1000 zl na koncu wobec reszty, zl na wies i dobe (zmiana, woz netto, poza tickiem, pozycje ticku), ile zeszlo ponizej 1000 w tych oknach, odsetek z najazdem / grabieza / oblezonym targiem, targi z najwiecej biednymi wsiami. Log sprzed f14-wsie - "brak pliku" (INFO).
  - Opis grupy nds w naglowku pliku poprawiony.

**Wynik czytnika (bez wyjatku; `--baza-doby Armoury-2026-10-16_18-23-34.log`, jak orkiestrator):**
- **fin92, wolanie standardowe (bez `--do-doby`):** `NDS-3a NIE` - okna konczace sie w 109230-109234: -0.242 / -0.234 / -0.221 / -0.214 / -0.191%/d (mediana -0.221). NDS-3a0 INFO 6.14 -> 4.98 mln = -0.320%/d (66 dob). NDS-3f: odplyw krancowy 1.58%/d, stala 63 doby, S* 4.26 mln. NDS-3d 11.5% (TAK). NDS-3g: 8.6% -> 11.5% (koniec rampy 2.7%), 28 ostatnich dob 51 -> 66 wsi, +0.71 wsi/d. NDS-3h: brak pliku (log sprzed paczki). **Razem NIE 5** - te same wiersze co w `fin92/nds.txt` orkiestratora (NDS-3a, NDS-7b x2, NDS-9, NDS-11).
- fin92 `--do-doby 120`: NDS-3a NIE -0.466%/d (jedno okno 109169-109194, jak dotad).
- fin92 bez `--baza-doby` i bez `--do-doby`: NDS-3a NIE (te same 5 okien), razem NIE 3 (NDS-3a, NDS-7b po rampie, NDS-9 - dwa ostatnie ze slabym odniesieniem "bez bazy").
- **fin91, wolanie standardowe:** NDS-3a TAK (-0.007 / -0.010 / -0.008 / -0.010 / -0.006%/d). NDS-3d 14.0% (prog 15% - blisko). NDS-3g 8.5% -> 14.0% (koniec rampy 4.6%), 67 -> 81 wsi, +0.47 wsi/d. Razem NIE 4.
- Stare logi: fin78 `2026-10-17_15-37-31` NDS-3a TAK (najgorsze -0.195%/d), NDS-3g 11.9% -> 20.5%, +1.11 wsi/d; fin80 `2026-10-17_16-26-10` NDS-3a TAK (+0.118..+0.167%/d); baza `2026-10-16_18-23-34` - brak linii DS, kod 0.
- NDS-3h na sztucznym pliku 50 wsi x 5 okien - wiersz liczy sie, kod 0.
- Poza NDS-3 wyniki grupy identyczne z HEAD paczki (diff wynikow fin92).

**DECYZJA Jeffa - DS przed koszykiem KS:**
- **A. Zostaje jak jest.** Jednorazowo po wlaczeniu DS (i u Jeffa po wczytaniu zapisu bez KS) kiesy wsi rosna o ok. 3.8 mln (ok. 1.7% zlota swiata) przez ok. 50 dob, potem schodza ze stala ok. 2 miesiecy. Zloto nie ginie (zamknieta ekonomia): wraca przez towar i roboty do kas miast oraz przez danine od skarbca do panow i kas osad.
- **B. DS czeka na KS** (tak mowil projekt DS: wies DS tylko w stanie zastosowanym KS). Pod nowym kluczem; klucz wylaczony = dzis. Do wlaczenia KS wsie zostaja przy starych zasadach, a rampa DS rusza razem z rampa KS.
  - Dotyka `DsVillage.On/Live/Ramp`, `DsTowns`, `DsSeason` (zwrot korony K5) i czynszu W3 (zamiast renty 20%). Dlatego osobna paczka z wlasnym testem, nie poprawka w tym kroku.
- Rekomendacja: najpierw jeden test z `wsie-kiesy.csv` (ogon biednych wsi), potem B jako osobna paczka, jesli Jeff zgodzi sie z projektem.

**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0):**
- **Regresje (gra):** zadnej zmiany zlota ani decyzji gry. `MoneyLedger.Mark/BlockOpen/Daily` - nowe wywolania na koncu, kazde we wlasnym try (wyjatek nie przerywa ksiegi ani linii). `VillageTakings.EnterPostfix` - `CartCsv` ostatni, po `F5WoodCredit`, tylko odczyt kies i licznik.
- **Kolizje:** ta sama technika co `AlleyProbe.OnMark` (migawki per osada w `Mark`), osobny stan. Linie ksiegi bez zmian. Nowa linia `Kiesy wsi wies po wsi (f14-wsie)` pisze sie po liniach ksiegi w `Daily` (nie wchodzi miedzy linie ksiegi, zaden wzorzec czytnika jej nie lapie - sprawdzone `grep`). Inne grupy czytnika nie czytaja NDS-3 (`nakum` NAKUM-5 ma wlasny kod).
- **Koszt:** ok. 35 wywolan `Mark` na dobe x 571 wsi = ok. 20 tys. odczytow `Village.Gold` na dobe. Plik ok. 571 wierszy co 7 dob (ok. 100 KB). W grze Jeffa przy cichych logach - tez co 7 dob. Zbieranie stoi przy wylaczonym logu.
- **Spojnosc pliku:** `zmiana_okna = poza_tickiem + suma tick_*` z definicji. Dzien wylaczonego logu w srodku okna - zmiany ticku tego dnia ida do `poza_tickiem` (tylko diagnostyka). Pierwsze okno po wczytaniu jest krotsze (kolumna `dob_okna`).
- **Czytnik:** NDS-3a ostrzejszy (5 okien), wiec log z jednym dobrym dniem na koncu nie przejdzie. NDS-3c/3d/3e przy wolaniu standardowym patrza teraz na koniec logu (fin92 160 dob), nie na dobe 120 - fin91 NDS-3d 14.0% (dotad 9.4% przy 120 dobach), blisko progu 15%.
- **Nie twierdze, ze wsie sa zdrowe.** Zostaja trzy otwarte rzeczy: (1) fin92 kiesy -0.22%/d na koncu biegu (NDS-3a NIE); (2) ogon biednych wsi +0.5..0.7/d - do rozpoznania z NDS-3h w nastepnym tescie; (3) nizszy dochod rodow w fin92 (NDS-9 0.517) - o 10 tys./d mniej markietanow we wsiach.
- **Status: DO SPRAWDZENIA** (plik `wsie-kiesy.csv` i NDS-3h w nastepnym tescie: linia `Kiesy wsi wies po wsi` co 7 dob, potkniecia 0, `zmiana kies okna` zgodna ze zmiana sumy kies wsi w "Przeplywy osad (kiesy wsi)" za te 7 dob). **DECYZJA Jeffa:** A albo B (DS przed koszykiem KS). Paczka **nie** naprawia NDS-3a.
