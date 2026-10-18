## 2026-10-18 (paczka f14-wsie - NDS-3a fin92: kiesy wsi 6.14 -> 5.46 mln = -0.466%/d; "dokad ida pieniadze wsi i skad maja wracac")

**Mod:** bez zmiany DLL - tylko czytnik `tools/sprawdz_logi.py` (grupa nds, wiersze NDS-3) | **Galaz:** `w-toku/f14-wsie` (od 8cd7b556 = test fin92) | **Tor:** bez gry (analiza logow fin92 `Armoury-2026-10-18_09-33-39.log` i fin91 `Armoury-2026-10-18_06-29-09.log`; build Armoury kod 0 bez zmian kodu) | **Klucz:** zaden (zadna domyslna nie zmieniona, `gen_mcm` niepotrzebny) | **Zapis:** bez zmian.

**Dla Jeffa (prosto):**
- Wsie nie traca pieniedzy na stale. Po wlaczeniu nowych danin (DS) wsie przez ok. 60 dob prawie nic nie kupowaly (koszyk kupowany w miescie rusza dopiero z kalibracja startu KS) i zebraly nadwyzke: kiesy wszystkich wsi urosly z 3.70 do 7.46 mln. Od konca rampy KS wydaja ja na towar, roboty gminy i danine od skarbca - kiesy schodza do rownowagi, a nie do zera.
- Tempo spadku maleje z kazdym tygodniem (fin92, sredni dryf w blokach 2 tygodni: -39 tys., -36, -31, -21, -21, -11 tys., a w ostatnich 11 dobach +1 tys./d). W fin91 kiesy stoja na 5.04 mln od doby 109204. Rownowaga to ok. 4.3-4.9 mln - wiecej niz na starcie (3.70 mln) i ponad dwa razy wiecej niz zapas wsi na czarna godzine (skrzynie 2.0 mln).
- Czytnik mierzyl spadek w oknie 26 dob zaraz po rampie, czyli w polowie schodzenia. Teraz mierzy koniec biegu. W fin92 wynik to -0.19%/d (prog -0.2%/d), w fin91 -0.01%/d.
- Do dalszej pracy zostaja dwie rzeczy. (1) W fin92 armie dostaja o 9% mniej zoldu niz w fin91, wiec mniej wydaja w polu i do wsi trafia o 10 tys./d mniej. To skutek nizszego dochodu rodow (NDS-9 0.517), nie blad wsi. (2) Przybywa wsi bez grosza: fin92 2.7% -> 11.5%, fin91 4.6% -> 14.0% (prog 15%). Suma jest w porzadku, ale bogate wsie trzymaja pieniadze, a biedne schodza do zera.

**Problem (dowody z logu):**
- fin92 `NDS-3a: kiesy wsi - dryf 26 dob od doby 109169 ... | NIE | 6.14 -> 5.46 mln = -0.466%/d` (fin91: -0.381%/d). Czytnik wolany z domyslnym `--do-doby 120` widzial tylko doby 109075-109194 z 160.
- Seria "Przeplywy osad (kiesy wsi)" (stan): start 109076 3.70 mln, szczyt 109132 7.46 mln, 109172 6.06, 109196 5.37, 109220 5.01, koniec 109234 4.98 mln. fin91: 3.69 -> 6.94 (szczyt) -> 5.04-5.06 od 109204 do konca.
- Koszyk wsi ZC4 (linia "Koszyk stanow (ZC4)", "wsie: zakupow N za X zl"): 0 zl/d do 109124, 14383 (109128), 48084 (109132), 80681 (109136), 115368 (109140), potem 100-125 tys./d. Rampa KS: "KS: rampa dzien 109110 | doba rampy 0 (A 109111, D 109125, koniec popytu 109139)". DS F1 od 109076 (`NDS-13 DayOn [109076.0]`).
- Dryf 26 dob kroczaco (fin92, dwa konce): 109139-165 -0.586%/d, 109169-195 -0.473%/d, 109187-213 -0.361%/d, 109199-225 -0.269%/d, 109205-231 -0.194%/d. fin91: ... 109199-225 -0.019%/d, 109205-231 -0.011%/d.

**Gdzie ida pieniadze wsi (fin92, srednia 26 ostatnich dob 109209-109234, kiesy 5.01 mln; ksiega "Przeplywy osad (kiesy wsi)" + linie zrodlowe):**

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

- Bez ubytku w nicosc: `NDS-2b` reszta kies wsi po nowym pomiarze 218/d, `NDS-2d` reszta bilansu swiata -308/d, `NDS-14a` utarg DS / 112 = 1.000.
- **Pytania z zadania:**
  - kupcy KS-H: fracht +0.3 tys./d. Kupcy kupujacy we wsiach: 6.9 tys./d, w calosci do kies wsi (linia "Utarg wsi (112)": "oddane w calosci kiesom wsi (jak zakup gracza)", zniklo 0).
  - daniny DS: czynsz 10.6, korona przy wozie 13.5, dziesiecina netto 3.3 tys./d (jalmuzna zostaje).
  - renty: czynsz to pozycja "renty" ksiegi.
  - roboty i odbudowa: 22.9 tys./d. Robote wsi robi gmina wlasnymi rekami, pieniadz idzie tylko za prawdziwy material i rzemieslnikow.
  - pobor i werbunek: oplata za ochotnikow 7.1 tys./d idzie do notabli, nie do kies wsi ("Werbunek"). Wraca przez sakiewki zwolnionych 14.4 tys./d.
  - uchodzcy RT: kilkadziesiat zl/d.
  - niewola: brak przeplywu ze wsi.
  - turnieje: sakiewka turniejowa idzie z kies ludu miast, nie ze wsi.
  - weglarze (`NWE-2b` 0 zl): **tak ma byc**. To rece tej samej wsi gorniczej, a drewno z lasu jej okregu idzie prosto do jej dymarki. Zaplata jest utarg za lupy, ktory wraca do tej samej kiesy. Przelew wies -> ta sama wies nic nie zmienia, a przelew do innej wsi nie zmienia sumy kies wsi.
  - pozyczki miejskie f8: **wsie nie sa zrodlem kapitalu**. `NPZ-2`: zrodla to kiesy W/R/P, kasy kupcow i podzamcza. `NPZ-12`: do kies wsi 0 zl/d (wsie sa wierzycielem tylko przy przejeciu dlugow obieg2 - "przeniesione z obieg2 0").
- **Dlaczego fin92 gorzej niz fin91 (te same 26 dob):**
  - wplywy -7.0 tys./d: markietani -6.0, sakiewki zwolnionych -4.8, przelewy gry +3.8.
  - Przyczyna lezy po stronie wojska: zold dnia 583.7 tys. wobec 642.2 tys. (-9.1%), sakiewki -> markietani 325.8 tys. wobec 395.1 tys. (-17.6%).
  - Rody maja nizszy dochod staly: `NDS-9` 0.517 wobec 0.767 w fin91 (fala 6/7). Wies jest tu tylko odbiorca.
  - Danina od skarbca wsi sama zmalala (27.4 wobec 32.5 tys.). Bez tego roznica bylaby o 5 tys./d wieksza.

**Przyczyna (NIE w NDS-3a):**
- Wsie nie traca pieniedzy przez blad. Spadek to zejscie nadwyzki z rampy, a czytnik mierzyl je za wczesnie.
- Nadwyzka powstala w oknie DS bez koszyka KS: DS F1 zdjal stare pobory od doby 1, a koszyk wsi ZC4 ruszyl dopiero w rampie KS (109125-109139).
- Po rampie odplyw z kiesy rosnie razem z kiesa: krok 5 planuje (kiesa - skrzynia) / 15, a danina od skarbca bierze 3%/d ponad 3 x skrzynia. Odplyw krancowy zmierzony w blokach 14 dob to ok. 1.6-1.8%/d (fin92 1.58, fin91 1.77), czyli stala zejscia 56-63 dob.
- Okno czytnika zakladalo stala 15 dob: "+ 2 x DsSurplusSpendDays", komentarz "krok 5 reaguje na nowy odplyw ze stala 15 dob".
  - Krok 5 wydaje jednak tylko ok. 1/5 planu. fin92: plan 223 tys./d, towar 26 + roboty 20 tys.
  - Reszta jest "niewydane" 90 tys. (brak towaru i materialu) albo "potrzeba pokryta" 87 tys.
- Okno 109169-109195 lezalo wiec w polowie zejscia: 30 dob po rampie przy stalej ok. 60 dob.
- Domyslne `--do-doby 120` dodatkowo ucinalo ostatnie 40 dob biegu fin92, w ktorych dryf schodzi do zera.

**Zmiana (`tools/sprawdz_logi.py`, `nds_main`, NDS-3):**
- **NDS-3a / NDS-3b** mierza stan ustalony: nachylenie MNK stanu kies wsi z **ostatnich 26 dob okna**, nie wczesniej niz dotychczasowy poczatek (koniec rampy + 2 x 15).
  - Progi bez zmian (>= -0.2%/d, <= +0.2%/d).
  - MNK, a nie dwa konce, zeby jedna doba nie przestawiala wyniku. W fin92 ostatnia doba to +74.6 tys.: dwa konce dalyby -0.115%/d, MNK daje gorsze -0.191%/d.
  - Okno krotsze niz 14 dob - jak dotad ostatnie 30 dob (z nich 26 ostatnich).
- **NDS-3a0 (INFO):** dotychczasowy pomiar od konca rampy do konca okna (dwa konce), opisany jako przejscie.
- **NDS-3f (INFO):** rownowaga z blokow 14 dob od konca rampy (dryf bloku = a + b x sredni stan).
  - Pokazuje odplyw krancowy, stala zejscia, S* = -a/b wobec startu i skrzyni swiata oraz bloki.
  - Mniej niz 5 blokow - "za krotko". Przy 4 blokach szacunek fin91 dawal 1.06 mln - za malo danych.
  - b >= 0 - wiersz kaze szukac odplywu niezaleznego od kiesy.
- **NDS-3g (INFO):** odsetek wsi z kiesa < 1000 zl - 7 dob od konca rampy wobec 7 ostatnich dob.
- Opis grupy nds w naglowku: NDS-3 MNK z 26 dob, akapit f14-wsie, uwaga o `--do-doby <dlugosc biegu>`.

**Wynik czytnika po zmianie (bez wyjatku):**
- fin92 `--do-doby 160`:
  - NDS-3a TAK: 5.13 -> 4.98 mln, MNK -9586/d = -0.191%/d.
  - NDS-3a0 INFO: -0.320%/d.
  - NDS-3f INFO: odplyw krancowy 1.58%/d, stala 63 doby, S* 4.26 mln = 1.15 x start, skrzynia 2.02 mln.
  - NDS-3g INFO: 2.7% -> 11.5%.
  - Razem NIE 3 (dotad 4 przy tym samym `--do-doby`): NDS-7b x2 i NDS-9 - korona i D rodow, poza ta paczka.
- fin92 domyslne `--do-doby 120`: NDS-3a NIE -0.466%/d. Okno konczy sie 55 dob po rampie, w trakcie zejscia - wynik prawdziwy dla doby 120. NDS-3f "za krotko".
- fin91 `--do-doby 160`:
  - NDS-3a TAK -0.006%/d.
  - NDS-3f: 1.77%/d, stala 56 dob, S* 4.48 mln = 1.21 x start.
  - NDS-3g 4.6% -> 14.0%.
- Stare logi:
  - fin78 `2026-10-17_15-37-31`: NDS-3a TAK -0.188%/d, NDS-3g 9.9% -> 20.5%.
  - fin80 `2026-10-17_16-26-10`: NDS-3a TAK +0.167%/d (NDS-3a0 dwa konce -0.024%/d - jak dotad NDS-3a).
  - baza `2026-10-16_18-23-34`: "brak linii", kod 0.
- Pozostale wiersze nds - co do znaku jak przed zmiana (diff wynikow).
- `npozyczka` i `nwegiel` na fin92 - wynik identyczny z przebiegiem orkiestratora (24 progi / 0 NIE i 15 progow / 0 NIE).

**Oczekiwany dryf (z liczb fin92):**
- Bez zmiany gry.
- Kiesy wsi zmierzaja do ok. 4.3-4.9 mln (szacunek z blokow 4.26, z przeplywow ok. 4.4-4.8). Na koncu biegu dryf wynosi -0.1..-0.2%/d i maleje: MNK 26 dob -0.191%/d, 14 dob -0.112%/d, srednia 11 ostatnich dob +1.3 tys./d.
- W rownowadze kiesy to ok. 2% zlota swiata (start 1.74%).

**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0):**
- **Regresje:** zmieniony tylko czytnik. Gra, DLL, klucze MCM i zapis bez zmian, wiec nie ma nic do wgrania. Inne grupy nie czytaja NDS-3: `nakum` NAKUM-5 liczy dryf kies wsi wlasnym kodem, bez zmian.
- **Kolizje:** wiersze NDS-3a0/3f/3g to INFO i nie zmieniaja licznika NIE. NDS-3a/3b maja ten sam prog, inne okno (koniec biegu). Przy biegu 120 dob okno jest takie jak dotad, wiec wynik pokrywa sie z dawnym (fin92 -0.466%/d).
- **Spojnosc:** NDS-3a mierzy teraz to, co mowi prog ("kiesy plaskie po rampie" = stan ustalony). Gdyby wsie naprawde tracily pieniadz (odplyw niezalezny od kiesy), MNK konca biegu dalej by to pokazal, a NDS-3f dostalby b >= 0 albo S* ponizej skrzyni.
- **Do decyzji / dalszej pracy (nie w tej paczce):**
  - (1) **Dochod rodow (NDS-9 0.517 w fin92 wobec 0.767 w fin91):** nizszy zold to o 10 tys./d mniej wydatkow markietanow we wsiach. Naprawa dochodu rodow podniesie tez rownowage kies wsi.
  - (2) **Ogon biednych wsi (NDS-3g):** liczba wsi z kiesa < 1000 zl rosnie przy plaskiej sumie (fin91 79 z 571 = 14% na koncu, prog NDS-3d 15%; fin78 20.5%). Wsie ponizej skrzyni traca tylko wtedy, gdy utarg jest mniejszy niz koszyk + czynsz.
    - Danina od skarbca dziala dopiero ponad 3 x skrzynia, krok 5 dopiero ponad skrzynia, wiec same nie spychaja wsi do zera.
    - Przyczyna wedlug miast targowych (`ks-miasta.csv`, 109165-109228): najszybciej traca wsie przy tanim zbozu i jedzeniu (Volon Therys indeks zboza 0.94, -1.42%/d; Selhorys 0.94; Myrth 1.02) oraz w krainach wojny (Reach -280 tys., Riverlands -119 tys.).
    - Zyskuja wsie przy drogim jedzeniu (Elyria 3.23, +1.37%/d). Korelacja dryfu z indeksem zboza wynosi tylko +0.22.
    - Zatkane wsie 109234: 16 spladrowanych, 3 najezdzane.
    - Kopalnie z pusta kiesa nie kupia drewna: "pusta kiesa wsi 1295" ladunkow, kredyt drewna zatrzymany po 30 dobach.
    - Log nie ma kiesy wies po wsi (kiesa-ludu.csv pisze tylko miasta i zamki), wiec zeby to rozstrzygnac, potrzebny jest CSV wsi (kiesa, utarg, koszyk, czynsz, stan wsi) - osobna paczka diagnostyczna.
- Status: **DO SPRAWDZENIA** (czytnik: przy nastepnym tescie wolac `--grupa nds ... --do-doby <dlugosc biegu>`).
