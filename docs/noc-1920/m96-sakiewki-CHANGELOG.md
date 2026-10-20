## m96-sakiewki - oszczednosci wedlug potrzeby: sakiewki zolnierzy do 15 dni zoldu (Armoury, WGRANIE 96)

**Dla Jeffa (prosto):** zolnierze trzymali w sakiewkach okolo 22-24 dni zoldu, bo odkladali na lepszy sprzet, ktorego na targach po prostu nie ma.
Teraz odkladaja tylko na to, co widzieli na straganie i na co ich nie bylo stac; reszte wydaja jak dotad - u markietanow w polu i w karczmach
w miescie, a zalogi w swoim miescie albo zamku. Pieniadze nie znikaja: ida do kas miast i zamkow, do kies ludu i wsi. W trwajacej kampanii zmiana
wchodzi lagodnie przez 28 dob. Wylacznik: MCM Armoury -> "Coin flow (night 16.10)" -> **Savings By Need** (wylaczony = jak dzis).

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
- Cel oszczedzania: przy kazdym przegladzie dozbrajania (`MenUpgrade.Run`) suma cen najtanszej lepszej sztuki w kazdym koszyku odrzuconym "za malo
  w sakiewce" (`MenUpgrade.Pick`, werdykt UpNoMoney). Przeglad bez takiego koszyka - cel 0.
- Partie: limit oszczednosci w `OnLeft` i `CampReserve` = najwyzej zalegle naprawy + cel (i nie wiecej niz dotad). Reszta wolna - jak dotad: markietani
  w polu (N6 bez zmian), "zycie" przy wyjezdzie z miasta (dawna "nadwyzka ponad limit").
- Zalogi: chronione zalegle naprawy + cel; wolna reszta schodzi co dobe na zycie w osadzie ta sama miara co markietani N6 (SutlerDailyShare 10% x
  min(3, dni wolnej / 10)) do kasy osady, 75/25 kupcy / lud (`TownFolk.EarnService` KGarrison) - jak czesc zoldu zalogi wydawana na miejscu. Pomijane:
  oblezona osada, lupiona osada (L3, `CitySack.NoSpend`). Wplyw zoldu do sakiewki zalogi bez zmian (zaloga ma z czego kupic braki).
- Rozruch: pierwsza doba z kluczem przy sakiewkach > 15 dni (zapis Jeffa) - nowa regula wchodzi liniowo w `SutlerRampDays` (28) dob; nowa kampania od
  razu. Zapis `arm_m96sak` (r = rozruch, t = cele partii); stary zapis bez klucza wczytuje sie (cele 0, rozruch rozstrzyga pierwsza doba); starsza DLL
  klucz pomija.
- Wpiecia: MenPurse (Reset, OnPartyDestroyed, OnLeft, CampReserve - po 1 linii), MenUpgrade (VisitBegin/VisitEnd wokol Run x2, cena w Pick),
  ArmouryBehavior (Daily po Sutlers.Daily - ta sama pozycja ksiegi MSutlers; SyncData), Settings (1 klucz), tools/sprawdz_logi.py (grupa m96sakiewki).
- Linia doby: "Sakiewki wedlug potrzeby (m96): dzien N | udzial nowej reguly | cele | limit oszczednosci partii | zalogi: ... = X dni ... kontrola
  (roznica 0) | koszt". Linia rozruchu raz: "Sakiewki wedlug potrzeby (m96): rozruch - ...".

**Oczekiwany wynik (rachunek z liczb wyzej):** partie rodow: naprawy 0.9 + cele ok. 0.3 + wolna ok. 8-9 = ok. 9-10 dni; zalogi: dodatni wplyw netto
ok. 0.3 dnia zoldu na dobe, rownowaga N6 ok. 5.5 dnia + naprawy = 4-8 dni; karawany bez zmian (0.7-0.8 mln). Razem: **sakiewki 8-13 dni zoldu
(rachunek ok. 9.5; dzis 21-24.5)** - prog PLAN "najwyzej 15" i projekt A3 (5-12). Oszczednosci K1 partii 0.3-2 dni (dzis 10-14.5). Przejscie w
zapisie Jeffa: ok. 8.7 mln oszczednosci partii i ok. 4.5 mln sakiewek zalog wraca do obiegu w ok. 28-40 dob (szczyt ok. 300-400 tys./d w polowie
rozruchu) - do kas miast i zamkow, kies ludu i wsi; potem zalogi ok. 20-60 tys./d, czyli tyle, ile dzis i tak szlo do kas ponad limit.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Zamknieta ekonomia: sakiewka zalogi -> kasa osady + kiesa ludu (posiadacze "Pieniadz swiata"); partie - drogi juz istniejace (markietani, zycie).
  Kontrola w linii (zdjete = do osad). Przejscie podnosi na 1-2 miesiace kasy miast i zamkow oraz zawory panow (jednorazowo ok. 13 mln) - patrzec
  "Pieniadz swiata", "Przeplywy osad", "Zawor zamkow (110)", kiesy panow zamkow (B3).
- Braki lordow AI (AiGear) placi najpierw sakiewka, reszte kiesa pana: mniejsza sakiewka przy wjezdzie moze przesunac czesc kosztu brakow na kiesy
  panow - patrzec "ZakupyAI wedlug kupujacego (174b)" (sakiewki ludzi / kiesy) i SN-7 ("za malo w sakiewce" <= 2% koszykow "brak lepszej").
- Markietani: wiecej wolnej sakiewki = wyzszy mnoznik N6 w rozruchu (do x3) - zamierzone; NE-18 (pierwsza doba) bez skoku, bo pierwsza doba z kluczem
  liczy jeszcze stara regule (udzial 0 do konca markietanow tej doby). F6-2 (S >= K) bez zmian - obie liczby z tej samej CampReserve.
- Noclegi TownInns (CampFree) i oboz widza wiecej wolnej sakiewki - wydatek ludzi, zamierzone.
- Gracz: ludzie gracza odkladaja tylko na to, na co ich nie bylo stac (komunikat "Your men put by ..." rzadziej) - ta sama regula co AI.
- Koszt: raz na dobe ok. 230 zalog z odczytem napraw (AiWear.OutstandingCost bez szacunku materialu); w Cap/Pick tylko arytmetyka i odczyt slownika,
  bez alokacji, watek glowny (zdarzenia osad, dobowy tick). Wyjatek na zalodze - licznik potkniec, bez globalnego wylacznika.
- Wylacznik: klucz NIE = limit MenGearSaveDays jak dotad, zalogi bez wydatku, rozruch od nowa przy ponownym wlaczeniu.

**Status:** DO SPRAWDZENIA (zbudowane, kod 0; bez autotestu - gra Jeffa dziala).

**Test w grze (autotest, noc WGRANIA 96):**
- `python -I tools/sprawdz_logi.py --grupa m96sakiewki <Armoury-*.log>`: SN-0 linia codziennie, SN-2 kontrola 0 / potkniecia 0 / ERROR 0,
  SN-3 sakiewki <= 15 (oczekiwane 8-13), SN-4 K1 <= 3 (0.3-2), SN-5 zalogi <= 10 (4-8), SN-6 bez wzrostu w ostatnich 28 dobach, SN-7 <= 2%,
  SN-8 koszt <= 30 ms. Progi SN-3..7 licza sie 14 dob po pelnej regule.
- 160 dob z zapisu d240: linia rozruchu "> 15 dni: nowa regula wchodzi w 28 dob", w ostatniej dobie Obieg "= 8-13 dni zoldu naliczonego".
- Nowa kampania 120 dob: "bez rozruchu", d120 Obieg 8-13 (dzis 20.7-21.0), zalogi 4-8 dni (dzis 25.4-25.6).
- `--grupa etap5` E5/A3 A3-2 (<= 15) ma przejsc na TAK. Grep: `Sakiewki wedlug potrzeby (m96): dzien`, `Sakiewki wedlug potrzeby (m96): rozruch`,
  `ERROR in SavingsByNeed`, `dni zoldu naliczonego`.
