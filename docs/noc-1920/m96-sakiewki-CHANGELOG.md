## m96-sakiewki - oszczednosci wedlug potrzeby: sakiewki zolnierzy do 15 dni zoldu (Armoury, WGRANIE 96)

**Dla Jeffa (prosto):** zolnierze trzymali w sakiewkach okolo 22-24 dni zoldu, bo odkladali na lepszy sprzet, ktorego na targach po prostu nie ma.
Teraz odkladaja tylko na to, co widzieli na straganie i na co ich nie bylo stac - lepsza sztuke albo brakujaca, na ktora zabraklo pieniedzy; reszte
wydaja jak dotad - u markietanow w polu i w karczmach w miescie, a zalogi w swoim miescie albo zamku. Pieniadze nie znikaja: ida do kas miast i zamkow,
do kies ludu i wsi. W Twojej kampanii zmiana wchodzi lagodnie przez 28 dob (uwolni ok. 4 dni zoldu, ok. 50 tys. dziennie). Wylacznik: MCM Armoury ->
"Coin flow (night 16.10)" -> **Savings By Need** (wylaczony = jak dzis).

**Problem:** PLAN-DO-KONCA-MODA, etap 5, "Skonczone, gdy": "sakiewki zolnierzy najwyzej 15 dni zoldu"; RAPORT-NOCNY-2026-10-20 rozdz. 5: "sakiewki
zolnierzy 21 dni zoldu (prog 15)". Prog `sprawdz_logi --grupa etap5` E5/A3 A3-2 (oczekiwane w projekcie A3: 5-12 dni) dalej NIE.

**Dowod (6 biegow 20.10, `D:\Backup-Bannerlord\logi-testow\2026-10-20`; linie "Obieg" - sakiewki ludzi / zold partii i zalog, "Markietani (E5/A3)",
"Dozbrajanie: dzien", "Zold: dzien"):**

| Bieg | Sakiewki (Obieg) | Partie rodow | w tym oszczednosci K1 | Zalogi (dni zoldu zalog) | Karawany |
|---|---|---|---|---|---|
| WGRANIE 95, 160 dob z d240 (12-25-19) | 21.7 -> 24.5 dni | 14.75 mln = 23.5 dni | 8.93 mln = 14.2 dni | 5.89 mln = 25.4 | 0.78 mln |
| fala 1, 160 dob (09-14-49) | 21.6 -> 23.1 | 13.71 mln = 21.8 | 8.84 mln = 14.0 | 5.95 mln = 25.5 | 0.71 mln |
| WGRANIE 92, 160 dob (04-01-39) | 21.6 -> 22.5 | 13.30 mln = 21.2 | 8.42 mln = 13.4 | 5.69 mln = 25.0 | 0.69 mln |
| WGRANIE 91, 160 dob (00-44-29) | 21.7 -> 22.9 | 13.21 mln = 21.8 | 8.48 mln = 14.0 | 5.60 mln = 24.7 | 0.71 mln |
| fala 1, nowa kampania 120 dob (08-45-54) | 2.0 -> 21.0 (dalej +1.4/10 dob) | 8.15 mln = 17.9 | 4.82 mln = 10.6 | 5.24 mln = 25.6 | 0.68 mln |
| WGRANIE 92, nowa 120 dob (03-32-17) | 1.9 -> 20.7 (dalej rosnie) | 8.15 mln = 17.5 | 4.65 mln = 10.0 | 5.29 mln = 25.4 | 0.79 mln |
| gra Jeffa save040 (Modules/Armoury 13-28-19 -> 14-53-56, d108864-108874) | 8.8 -> 10.3 (rosnie ok. 0.3/dobe) | 3.73 mln = 9.4 | 1.33 mln = 3.4 (3.8 w d108874) | 0.93 mln = 5.9 | - |

Dozbrajanie (srednio na dobe w tych biegach): ludzie odkladaja na lepszy sprzet przy wyjazdach 59-76 tys., lordowie kupuja lepszy za 12.7-16.8 tys.,
zalogi za 21-33 tys. (braki zalog z ich sakiewek 14.8-21.3 tys.). Pominiete koszyki: **"brak lepszej na polce" 19.3-22.5 tys./d, "za malo w sakiewce"
39-124/d** - pieniadz jest powodem w 0.2-0.6% przypadkow. Odplyw w polu (markietani N6) i "zycie" w miastach dzialaja: wolna czesc sakiewek partii to
ok. 8.4 dnia (rownowaga N6: 10% x wolne/10 dni = 1 dzien zoldu na dobe przy ok. 10 dniach wolnej sakiewki).

**Przyczyna (kod, nie zgadywane):**
- `MenPurse.OnLeft` (K1 A3): przy wyjezdzie z miasta 50% przyrostu sakiewki idzie do oszczednosci "na lepszy sprzet", limit `MenGearSaveDays` (30) dni
  zoldu; oszczednosci chroni tez `MenPurse.CampReserve` przed markietanami. Jedyne ich ujscie to zakup lepszej sztuki - a tej na polkach nie ma.
- N6 (`Sutlers.IdleShare`): partia bez zakupu od 14 dob chroni w polu `IdleKeep` 0.5 limitu = **15 dni** - dokladnie tyle (13.4-14.5 dnia) stoi
  w oszczednosciach partii rodow; plus wolna czesc ok. 8.4 i naprawy ok. 0.9 = 21-24 dni.
- Zalogi (`SoldierPay.GarrisonShare`): 50% zoldu do sakiewki zalogi do 30 dni zoldu; sakiewka zalogi nie ma zadnego ujscia poza zakupami sprzetu, wiec
  stoi pod limitem (24.7-26.6 dnia; czesc zalog przy limicie - "zalogi do sakiewek" 152 z 227).
- Za maly odplyw nie jest przyczyna (markietani i zycie zjadaja to, co wolne), za duzy zold tez nie (zold do sakiewek = zold naliczony). Przyczyna to
  oszczednosci bez celu: odkladane na towar, ktorego nie ma.

**Zmiana (Armoury/src/SavingsByNeed.cs, klucz `SavingsByNeed`, domyslnie TAK):** jedna regula dla lordow AI, ludzi gracza i zalog.
- Cel oszczedzania - z JEDNEJ wizyty na targu (okno `VisitBegin`/`VisitEnd`: `AiGear.TryBuy` - braki i lepsze lordow i zalog; `MenPurse.OnEntered` /
  `OnDailyTickParty` - braki i lepsze gracza): suma cen najtanszej sztuki w kazdym koszyku dozbrajania odrzuconym "za malo w sakiewce" (`MenUpgrade.Pick`,
  UpNoMoney) i w kazdym koszyku brakow, w ktorym pasujaca sztuka lezala, ale byla za droga na reszte budzetu (`AiGear.BuyLoop`, `MenPurse.BuyPlayerGaps`).
  Wizyta bez przegladu (bramka doby) - cel bez zmian; przeglad bez takich koszykow - cel 0.
- Zaloga zamku w dniu bez zamowienia wozem widzi tylko polke zamku - taki wezszy przeglad nie obniza celu z dnia, w ktorym widziala targ miasta (najwyzej
  7 dob; wyzszy nowy cel wchodzi zawsze).
- Partie: limit oszczednosci w `OnLeft` i `CampReserve` = najwyzej zalegle naprawy + cel (i nie wiecej niz dotad). Reszta wolna - jak dotad: markietani
  w polu (N6 bez zmian), "zycie" przy wyjezdzie z miasta (dawna "nadwyzka ponad limit").
- Zalogi: chronione zalegle naprawy + cel; wolna reszta schodzi co dobe na zycie w osadzie ta sama miara co markietani N6 (SutlerDailyShare 10% x
  min(3, dni wolnej / 10)) do kasy osady, 75/25 kupcy / lud (`TownFolk.EarnService` KGarrison) - jak czesc zoldu zalogi wydawana na miejscu. Pomijane:
  oblezona osada, lupiona osada (L3, `CitySack.NoSpend` - w linii lupu liczone jako "zycie"). Wplyw zoldu do sakiewki zalogi bez zmian.
- Rozruch: pierwsza doba z kluczem liczy, ile nowa regula uwolnilaby od razu (oszczednosci K1 partii rodow chronione dzis + wolna czesc sakiewek zalog).
  Wiecej niz 1 dzien zoldu partii rodow i zalog (trwajaca kampania) - nowa regula wchodzi liniowo w `SutlerRampDays` (28) dob; nowa kampania (puste
  sakiewki) - od razu. Zapis `arm_m96sak` (r = rozruch, t = cele partii `id:zl:doba`); stary zapis bez klucza wczytuje sie (cele 0, rozruch rozstrzyga
  pierwsza doba); starsza DLL klucz pomija.
- Wpiecia: MenPurse (Reset, OnPartyDestroyed, OnLeft, CampReserve, okno wizyty gracza x2, braki gracza), MenUpgrade (Saw przed Run x2, cena w Pick),
  AiGear (okno w TryBuy, Saw w TryBuyCore, za droga sztuka w BuyLoop), ArmouryBehavior (Daily po Sutlers.Daily - ta sama pozycja ksiegi MSutlers,
  DayProfile.Lap; SyncData), MoneyLedger (opis MSutlers, dopisek "Obieg"), Settings (1 klucz), tools/sprawdz_logi.py (grupa m96sakiewki).
- Linie: "Sakiewki wedlug potrzeby (m96): dzien N | udzial nowej reguly | cele (przegladow, wezszych, koszykow lepsze / braki) | limit oszczednosci partii |
  zalogi: ... = X dni ... kontrola: zeszlo z sakiewek = przybylo w kasach osad + do kies ludu (roznica 0) | koszt"; raz: "... rozruch - ... nowa regula
  uwolni X = Y dni zoldu ..."; w "Obieg" (sakiewki ludzi [P]): "zalogi na zycie w osadzie (m96) X (do kas miast A, do kas zamkow B)".

**Poprawki po recenzjach (gra, technika) - kazda sprawdzona:**
- (wazne, PRAWDA) Cel liczyl tylko dozbrajanie, a braki placi najpierw ta sama sakiewka (gracz - tylko ona: `MenPurse.BuyPlayerGaps` budzet = sakiewka -
  naprawy; AI: `AiGear.TryBuyCore` budzet = przydzial pana + sakiewka, zaplata najpierw z sakiewki). Poprawione: brak, na ktory zabraklo pieniedzy,
  jest celem (jak wyzej), okno obejmuje cala wizyte. Uzupelnienie dowodu: u lordow AI pieniadz na braki nie jest waskim gardlem - "Budzet rodow (166)"
  d109235: przydzial sprzetu 238 tys./d, wydane przez panow 46 tys./d, przeterminowane po 30 dniach 125 tys./d; koszt brakow przechodzi z sakiewki na
  przydzial pana (miejsce jest). Nowe progi SN-10 (zakupy AI braki + lepsze >= 80% bazy) i SN-11 (pokrycie korpusu lordow >= baza - 2 pp).
- (wazne, PRAWDA) Opis rozruchu nie pasowal do gry Jeffa: prog "sakiewki > 15 dni", a save040 ma 8.8-10.3 dnia - dostalby regule od razu. Poprawione
  w kodzie (nie tylko w opisie): rozruch wedlug tego, co regula naprawde uwolni (> 1 dzien zoldu). Save040: K1 ok. 1.3-1.5 mln + wolne w zalogach ok.
  0.8 mln = ok. 4 dni zoldu (0.55 mln/d) -> rozruch 28 dob. Poprawiony tez szacunek szczytu (dotad szczyt ponizej sredniej) - liczby nizej.
- (drobne, PRAWDA) Cel nadpisywany wezszym przegladem zamku - poprawione (wezszy przeglad, 7 dob).
- (drobne, PRAWDA) Kontrola "zdjete = do osad" byla prawdziwa z definicji (ta sama zmienna). Poprawione: ubytek sakiewki z odczytu przed/po `Take`
  (liczony zaraz po zdjeciu) wobec przyrostu kasy osady przed/po `ChangeGold` + przyjete przez kiese ludu; wyjatek miedzy zdjeciem a wplata da roznice.
- (drobne, PRAWDA) Brak `DayProfile.Lap`, opis MSutlers bez zalog, wydatek zalog niewidoczny w "Obieg" - poprawione (Lap, opis "m96: sakiewki zalog ->
  kasy ich osad i kiesy ludu", dopisek w Obieg po E5/B6 - zadna regula sprawdz_logi nie czyta sasiedztwa). `CitySack.NoSpend` liczy zaloge jako "zycie"
  w linii lupu - zostawione (to jest wydatek na zycie), opisane w linii doby.

**Oczekiwany wynik (rachunek z liczb wyzej i symulacji rozruchu):** partie rodow: naprawy 0.9 + cele ok. 0.3-1.5 + wolna ok. 8-9 = ok. 9-11 dni;
zalogi: rownowaga N6 przy wplywie 50% zoldu ok. 7.5 dnia (5-9); karawany bez zmian (0.7-0.8 mln). Razem: **sakiewki 8-13 dni zoldu (rachunek ok. 10;
dzis 21-24.5)** - prog PLAN "najwyzej 15" i projekt A3 (5-12). Oszczednosci K1 partii 0.3-3 dni (dzis 10-14.5).
- Zapis d240 (autotest): uwolni ok. 13-14 mln = ok. 16 dni zoldu -> rozruch 28 dob. Partie ok. 320 tys./d przez 28 dob (8.9 mln), zalogi rosna do ok.
  340 tys./d w dobach 8-14; **szczyt razem ok. 0.6-0.7 mln/d (doby 8-14)**, srednio ok. 0.37 mln/d przez ok. 35 dob, potem zalogi ok. 120 tys./d
  (ich 50% zoldu przechodzi przez sakiewke do osady - dotad przy limicie i tak szlo do kas).
- Gra Jeffa (save040): rozruch 28 dob; partie ok. 50 tys./d, zalogi (5.9 dnia - ponizej rownowagi) dalej sie napelniaja do ok. 7.5 dnia, wydatek zalog
  rosnie do ok. 80-100 tys./d; szczyt razem ok. 150 tys./d pod koniec rozruchu.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Zamknieta ekonomia: sakiewka zalogi -> kasa osady + kiesa ludu (posiadacze "Pieniadz swiata"); partie - drogi juz istniejace (markietani, zycie).
  Kontrola w linii z odczytow przed/po. Przejscie w zapisie d240 podnosi na 1-2 miesiace kasy miast i zamkow oraz zawory panow (ok. 13 mln) - patrzec
  "Pieniadz swiata", "Przeplywy osad", "Zawor zamkow (110)", kiesy panow zamkow (B3); w grze Jeffa ok. 2 mln.
- Braki AI: koszt przechodzi z sakiewek na przydzial pana (166) - patrzec "Budzet rodow (166)" sprzet: wydane przez panow (dzis 46 tys./d, miejsce do ok.
  240 tys./d), SN-10, SN-11, "ZakupyAI wedlug kupujacego (174b)". Gracz: brak, na ktory zabraklo, chroni odpowiednia czesc sakiewki przy wyjezdzie.
- Markietani: wiecej wolnej sakiewki = wyzszy mnoznik N6 w rozruchu (do x3) - zamierzone; NE-18 (pierwsza doba) bez skoku, bo pierwsza doba z kluczem
  liczy jeszcze stara regule (udzial 0 do konca markietanow tej doby). F6-2 (S >= K) bez zmian - obie liczby z tej samej CampReserve.
- Noclegi TownInns (CampFree) i oboz widza wiecej wolnej sakiewki - wydatek ludzi, zamierzone.
- Okno wizyty: `AiGear.TryBuy` i okno gracza w try/finally; `VisitBegin` zawsze od zera (bez zagniezdzen); `Saw` tylko po bramkach (bez przegladu cel
  zostaje). Pick/BuyLoop poza oknem - `NoteWant` nic nie robi.
- Koszt: raz na dobe ok. 230 zalog z odczytem napraw (AiWear.OutstandingCost bez szacunku materialu); rozruch raz: KeptFor ok. 650 partii; w Cap, Pick,
  BuyLoop i oknie tylko arytmetyka i odczyt slownika, bez alokacji, watek glowny (zdarzenia osad, dobowy tick). Wyjatek - licznik potkniec.
- Wylacznik: klucz NIE = limit MenGearSaveDays jak dotad, zalogi bez wydatku, dopisek Obieg pusty, rozruch od nowa przy ponownym wlaczeniu.

**Status:** DO SPRAWDZENIA (zbudowane, kod 0; bez autotestu - gra Jeffa dziala).

**Test w grze (autotest, noc WGRANIA 96):**
- `python -I tools/sprawdz_logi.py --grupa m96sakiewki <Armoury-*.log>`: SN-0 linia codziennie, SN-2 kontrola 0 / potkniecia 0 / ERROR 0,
  SN-3 sakiewki <= 15 (oczekiwane 8-13), SN-4 K1 <= 3 (0.3-3), SN-5 zalogi <= 10 (5-9), SN-6 bez wzrostu w ostatnich 28 dobach, SN-7 <= 2%,
  SN-8 koszt <= 30 ms, SN-10 zakupy AI >= 80% bazy (d240 >= 93 tys. zl/d, nowa >= 58 tys.), SN-11 korpus lordow >= 53% (d240) / >= 55% (nowa).
  Progi SN-3..7, 10, 11 licza sie 14 dob po pelnej regule.
- 160 dob z zapisu d240: linia rozruchu "nowa regula uwolni ok. 13-14 mln = ok. 16 dni ... > 1 dnia: nowa regula wchodzi w 28 dob", w ostatniej dobie
  Obieg "= 8-13 dni zoldu naliczonego", dopisek "zalogi na zycie w osadzie (m96)" ok. 120 tys./d po rozruchu.
- Nowa kampania 120 dob: "bez rozruchu", d120 Obieg 8-13 (dzis 20.7-21.0), zalogi 5-9 dni (dzis 25.4-25.6).
- `--grupa etap5` E5/A3 A3-2 (<= 15) ma przejsc na TAK. Grep: `Sakiewki wedlug potrzeby (m96): dzien`, `Sakiewki wedlug potrzeby (m96): rozruch`,
  `ERROR in SavingsByNeed`, `dni zoldu naliczonego`, `zalogi na zycie w osadzie (m96)`.
