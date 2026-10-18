## 2026-10-17 (KS runda 4 - "Tanszy koszyk biednych w Essos + place rosna, gdzie brakuje rak": TYLKO WYJASNIENIE, kod bez zmian)

**Mod:** brak zmian w kodzie (Armoury, `tools/sprawdz_logi.py`, `tools/gen_mcm.py` - nietkniete) | **Galaz:** `w-toku/r4-koszyk` (na efbbf0dc = `w-toku/noc-ks4` "KS runda 4 cz. 5") | **Tor:** S (sam dokument) | **Klucze:** brak (`KsCheapBasket`, `KsWageByHands` NIE dodane) | **Zapis:** bez zmian.

Jeff w tym przebiegu napisal: "Tanszy koszyk dla biednych w Essos, place rosna tam, gdzie brakuje rak - wyjasnij mi to". Zrobione samo wyjasnienie, oparte na kodzie (efbbf0dc, build rc 0) i na logu fin78 (`D:\Backup-Bannerlord\logi-testow\Armoury-2026-10-17_15-37-31.log` + CSV). Wdrozenie czeka na slowo Jeffa i na decyzje z punktu 6.

Recenzja: 1 uwaga, poprawione 1, odrzucone 0 (opis dwoch drabin - Planu i gry - byl wrzucony do jednego worka; ponizej rozdzielone, sprawdzone w kodzie).

**Dla Jeffa (prosto):**

1. **O co chodzi.** Plan KS liczy, czy kazda klasa w miescie stac na swoj koszyk: W = wyrobnicy, R = rzemieslnicy, P = kupcy i szlachta. Jedzenie niewolnikow placa ich wlasciciele: R 40%, P 60%. W Essos R i P nie stac na caly koszyk, wiec Plan uznaje, ze sa "na pol glodni". Prawdziwy biedak kupilby tansze kalorie: wiecej chleba i ziarna, mniej miesa i wina. Twoja decyzja z 17.10 miala dwie czesci: (a) koszyk dopasowuje sie do kieszeni, (b) tam, gdzie pracodawcom brakuje rak, dniowka powoli rosnie.

2. **Jak jest dzis (fin78, 120 dob).**
   - Plan: w Essos R placi 42% -> 73% koszyka, P 35% -> 68%. W Westeros R i P placa 89-97%.
   - W miastach Essos zboze i chleb sa zaplacone prawie w calosci (niezaplacone 13 szt./d). Brakuje bialka (mieso 202, ryby 382, ser i maslo 75), oliwy 238, piwa i wina 473, owocow ok. 700 szt./d.
   - W grze 71 z 97 miast stac na cale jedzenie. W 26 miastach dziala drabina. Lud nie kupuje 4.6% budzetu jedzenia miast (16 592 zl/d).
   - **Sa DWIE drabiny i dzialaja inaczej** (poprawka po recenzji):
     - **Drabina Planu** (`KsBasket.Ladder`, `StartCalibration.Deliver.cs:296`, szczeble `Rungs` :287). Szczeble po kolei: (1) CALE zboze, chleb, placek i sol, (2) napoje, (3) bialko, (4) reszta jedzenia (owoce, oliwa, miod, przyprawy), (5) przedza domowa, (6) rzeczy. Szczebel kupowany jest w calosci. Na szczeblu, na ktory nie starcza pieniedzy, kazdy towar dostaje ten sam ulamek, a dalsze szczeble dostaja zero. W szczeblu nie ma wyboru najtanszego, nic tez nie jest zamieniane miedzy szczeblami. Cale zboze idzie pierwsze, dlatego w Planie Essos zboze jest zaplacone prawie w calosci.
     - **Drabina w grze** (`Zc4Ladder`, `StartCalibration.Zc4.cs:697`). Wlacza sie tylko wtedy, gdy kiesy nie stac na cale jedzenie koszyka; wolanie w `TownFolk.Live.cs:429`. Kolejnosc:
       1. Przetrwanie: 0.47 kg ziarna na osobe (x lambda), w tej formie z ziarna / chleba / placka, ktora jest najtansza za kg i lezy na polce. Ta jedna forma moze wziac potrzebe pozostalych form zboza, wiec tu jest mala zamiana, ale tylko wewnatrz zboza.
       2. Sol.
       3. Napoje, najtansze za litr najpierw.
       4. Bialko, najtansze za kcal najpierw.
       5. Reszta po rowno: zboze ponad 0.47 kg oraz owoce, oliwa, miod i przyprawy.

       W krokach 2-4 kazdy towar dostaje najwyzej swoja potrzebe. Tanszy jest kupowany pierwszy, ale nie dostaje wiecej sztuk w zastepstwie drozszego.
   - Wspolne dla obu drabin: kalorie nie sa zamieniane miedzy kategoriami. Kto nie ma na mieso, nie kupi w zamian wiecej chleba. Napoj (w Essos czesto drogie wino) jest kupowany przed bialkiem.
   - Roznica, ktora wyszla przy sprawdzaniu: w grze zboze ponad 0.47 kg jest kupowane DOPIERO NA KONCU, po napoju i bialku, razem z owocami. W Planie cale zboze jest pierwsze. Opis `Rungs` ("ta sama kolejnosc co drabina zakupow ZC4") zgadza sie co do kolejnosci kategorii, ale nie co do zboza.

3. **Tanszy koszyk - co by robil.** Gdy klasy nie stac na pelny koszyk, kupuje w tej kolejnosci:
   1. kalorie z najtanszego zrodla na swoim targu, az do pelnej potrzeby kcal;
   2. bialko;
   3. napoj;
   4. reszta.

   Mieso, wino i ser dopiero z tego, co zostanie. Ile kcal daje 1 zl (wartosc bazowa gry, sztuka 10 kg): ziarno ok. 11 000, chleb 5 500, mieso 2 400, oliwa 2 200, piwo 1 300, ryby 1 100, wino 540, daktyle 470. Ryby nie sa tanimi kaloriami: weszlyby tylko tam, gdzie na miejscu sa tanie (porty). "Kasza" w grze liczy sie jako ziarno, a straczki sa domowe i nie ida przez targ. Rynek i Plan widzialyby koszyk naprawde kupiony: w Essos wiecej zboza, mniej wina i miesa. Za tym poszlyby ceny i Plan, czyli wiecej pol zboza i mniej winnic. Ekonomia zostaje zamknieta: ta sama kiesa kupuje inny towar.
   - **Zakres po recenzji:**
     - W grze to NIE jest przepisanie wyboru najtanszego w kategorii, bo on juz jest. Brakuje zamiany miedzy kategoriami: dodatkowe zboze lub chleb ponad 0.47 kg, gdy nie stac na bialko i napoj, reszta zboza przed napojem i bialko przed napojem.
     - W Planie trzeba dodac jedno i drugie: najtanszy najpierw w szczeblu (dzis rowne ciecie) i zamiane kcal miedzy szczeblami.
     - Wiekszosc luki jest w Planie.

4. **Place rosna, gdzie brakuje rak - co by robilo.**
   - Dzis dniowka = stawka historyczna x poziom plac miasta (`TownWage`: dobrobyt / 4800, w granicach 0.5-1.5). Nic nie patrzy na brak rak.
   - Lud zarabia glownie udzialami, a nie dniowka (`TownFolk.Live.cs:176` SplitOf):
     - robocizna handlu W90/R10;
     - doplata do wartosci wyrobow W70/R30;
     - wydatki zolnierzy W80/R10/P10;
     - obrot P.
   - Pomysl: tam, gdzie warsztaty i przetwory ZC5 nie maja rak, wskaznik plac rosnie o kilka % na tydzien, do gornej granicy. Placa pracodawcy: kasa miasta i wlasciciele warsztatow. Gdzie wolnych rak jest duzo, wskaznik wraca do 1.0.
   - Dzis braku rak prawie nie ma. W ZC5 zajete jest 1 791 z 20 610 roboczodni (9%), a koniec "brak rak" zdarza sie ok. 5 miasto-dob na pare. Place prawie by sie nie ruszyly, a podwyzka trafilaby glownie do W.

5. **Najwazniejsze: dlaczego R i P w Essos sa naprawde biedni.** Niewolnikow liczymy dwiema roznymi liczbami.
   - Koszyk liczy niewolnikow z ksiegi stanow (`StartCalibration.Zc4.cs:334`: 40% do R, 60% do P). W Volantis to 230 820 niewolnych na 329 391 ludzi (70%).
   - Zarobek z pracy niewolnikow idzie do wlascicieli wedlug glow BK (`TownFolk.Live.cs:202` Credit: udzial W x HS / (HS + HW); `TownFolk.cs:60` OwnR 0.4 / OwnP 0.6). W Volantis to 2 853 niewolnych na 25 676 wyrobnikow, czyli 10%.
   - Wlasciciele karmia wiec 70% miasta, a zaplate dostaja za prace 10%. Reszta tych pieniedzy trafia do wolnych W.
   - Skutki w Essos:
     - W ma budzet 283 tys. zl/d przy koszyku 69 tys.;
     - kiesy w Volantis: W 387 937, R 3 683, P 2 501;
     - dostatek biedoty w Volantis to 27.7 razy koszyk przetrwania.
   - Pieniedzy w miastach Essos jest dosc: 427 tys. zl/d budzetu na 224 tys. koszyka. Leza tylko w zlej kiesie.
   - Druga przyczyna dotyczy takze wolnych miast (Pentos 0.66, Braavos 0.71): rzemieslnicy to 30-33% ludzi, a staly podzial daje R tylko 10-30% zarobku.
   - Tanszy koszyk glownie zamaskuje ten problem, a place za brak rak dalyby wiecej glownie W.

6. **Propozycja (do Twojej decyzji, jedna zmiana naraz):**
   1. Jedna liczba niewolnikow: zarobek z pracy niewolnych przenosic do wlascicieli wedlug udzialu z ksiegi stanow, tego samego, z ktorego placa ich koszyk. Te same pieniadze przechodza od W do R i P, nic nie powstaje.
   2. Potem tanszy koszyk (`KsCheapBasket`), w zakresie z punktu 3.
   3. Place za brak rak (`KsWageByHands`) na pozniej albo z innym sygnalem, np. FreeHands, czyli straty mezczyzn na wojnie.

**Problem:** Jeff prosi o wyjasnienie dwoch pomyslow z decyzji 17.10 (tanszy koszyk biednych w Essos, place za brak rak). Dowod z logu fin78:
- "Koszyk stanow (ZC4): dzien 109195 ... drabina (kiesy nie stac na cale jedzenie) 26 ... stac na cale jedzenie: miasta 73.2% (71/97), jedzenie niekupione z braku pieniedzy: miasta 4.6% budzetu jedzenia (16592 zl)";
- Plan Essos R 73.2%, P 68.0% koszyka w dniu 120;
- `kiesa-ludu.csv` Volantis: kiesy W 387937 / R 3683 / P 2501.

**Przyczyna:**
- (a) Obie drabiny (Plan i gra) nie zamieniaja kalorii miedzy kategoriami, a napoj stawiaja przed bialkiem. Gra kupuje zboze ponad 0.47 kg dopiero na koncu.
- (b) Niewolni sa liczeni dwiema liczbami: koszyk z ksiegi stanow (70% w Volantis), zarobek wedlug glow BK (10%). Pieniadze wlascicieli R i P przeciekaja do W.
- (c) W ZC5 prawie nie ma sygnalu braku rak.

**Zmiana:** brak zmian w kodzie. Dodany tylko ten dokument (wyjasnienie + poprawka recenzji: rozdzielony opis drabiny Planu `KsBasket.Ladder` i drabiny gry `Zc4Ladder`).

**Ryzyko / co sprawdzic:**
- Do gry nic nie idzie, DLL bez zmian. Build rc 0. `sprawdz_logi.py --grupa nks` na fin78 przechodzi bez wyjatku (53 progi, NIE 18 - bez zmian, kod nietkniety). Grupa `nkoszyk` nie powstala, bo nie ma czego sprawdzac.
- Na wypadek wdrozenia (kontrola z zasady 0, w kodzie):
  1. `KsCheapBasket` zmienia znaczenie `TownPaid`. Zboze kupione ponad potrzebe da wartosc > 1, a dzis jest przyciete do 0..1 (`Zc4.cs:281` i :330 `Math.Min(1f, tp[z])`). `TownPaid` siedzi w zapisie stanu zastosowanego. Starsza DLL przytnie to do 1, ale trzeba sprawdzic eksport i import wpisow '@'.
  2. Wiekszy popyt na zboze w Essos podniesie cene zboza i zjedzie czesc taniosci. Przesunie sie bilans Planu NKS-3, a pary ZC5 wino i oliwa straca zbyt.
  3. W grze zamiana miedzy kategoriami zmienia `_zLadCut` (linia "jedzenie niekupione z braku pieniedzy") i podzial targu. W Planie najtanszy najpierw zmienia `paid[]` z jednego ulamka na szczebel na ulamek na towar, a czytaja go `StartCalibration.Core.cs:1350` i :1543.
  4. `KsWageByHands`: wyzszy WageIndex podnosi tez utrzymanie warsztatow (`WorkshopTrade.UpkeepAt`) i dniowke ZC5, wiec mniej par sie oplaca i powstaje petla zwrotna. Pieniadze ida glownie do W.
  5. Poprawka liczby niewolnych (pkt 6.1) to najwiekszy przeplyw: W w Essos traci wiekszosc dochodu z pracy niewolnych. W Volantis udzial przenoszony skacze z ok. 0.1 do ok. 0.7-0.8. Trzeba sprawdzic, czy W dalej stac na koszyk, oraz zadowolenie R14, mobilnosc ZC6 i suchy model `TownFolk.cs` z ta sama regula.
- Liczby pochodza z fin78 (stan a6c56999, sprzed KsRecipes). Test efbbf0dc (fin80) moze je przesunac.

**Status:** DO SPRAWDZENIA (samo wyjasnienie; kod czeka na decyzje Jeffa: najpierw pkt 6.1 czy od razu tanszy koszyk, jaki sygnal braku rak).
