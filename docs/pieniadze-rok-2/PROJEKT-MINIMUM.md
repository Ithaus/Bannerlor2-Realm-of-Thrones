# PROJEKT: MINIMUM ZMIAN - PIENIADZE ROKU 2 (A3, A4, A5, A15) - 14.10.2026

Kat projektu: **najmniejszy zestaw poprawek, ktory gasi alarmy roku 2 (rachunek na sucho rozdz. 7.6) przy najmniejszym ryzyku dla roku 1** (bieg 120 dob nie moze sie pogorszyc).
Baza (klucze "off") = `w-toku/zbior-e8` **449d453** (= W53 w grze). Kod nie byl zmieniany, gry nie uruchamialem, nic nie wgrane.
Znaczniki: **[P]** pomiar z logu / tabel / kodu, **[S]** szacunek (metoda podana); czego nie da sie ustalic - "nie wiem" + linia diagnozy (rozdz. 8).
Zrodla: `ROZPOZNANIE-RODY.md`, `ROZPOZNANIE-LUD.md` (ten katalog), BD = `docs/test-zbiorczy/BIEG-DWULETNI-2026-10-14.md`, rachunek 85 tys. rozdz. 7, log dwuletni
`D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log` + tabele `...\Logs\2026-10-13_21-14-13\`, wyciag dobowy rozpoznania `rody2\dw.pkl`, kod drzewa `zb\pr2`.
Wlasne skrypty: rozdz. 10. Doba = dzien gry - 108836 + 1; kwartaly Q1-Q8 jak w rozpoznaniach (Q5 d365-455 ... Q8 d638-728); kwoty w tys. zl na dobe swiata.

---

## 0. Dla Jeffa - prosto

1. Z dziesieciu alarmow roku 2 szesc zapalila glownie **mapa wojen** (16 krolestw zawarlo pokoj w d449-513), nie brak pieniedzy. Na mape juz dzialaja dwie rzeczy w grze:
   wojsko w pokoju (Z.1, WGRANIE 52) i zmeczenie wojna Diplomacy / 4.33 (od 14.10). Ich skutek pokaze dopiero nastepny bieg dwuletni - nie dokladam do tego trzeciej zmiany.
2. W pieniadzach poprawiam tylko to, co **nasze reguly robia zle niezaleznie od mapy** - trzy male zmiany i jedna czekajaca na Twoje slowo.
3. **Korona w wojnie placi najpierw zold, potem renty** (M1): dzis na doplate do zoldu idzie tylko danina, a reszta wplywow dnia idzie rentami wedlug lenn, nawet gdy
   panowie tego krolestwa nie maja na zold. To Twoja zasada z 13.10 ("korona placi wedlug potrzeby"), domknieta na calych wplywach dnia.
4. **Sojusznik dostaje rate tylko za dni, w ktorych naprawde walczy, i dopiero po wlasnych zolnierzach** (M2): Zelazne Wyspy zaplacily Dorzeczu 1.39 mln w roku 2,
   gdy ich panowie nie mieli na zold; Skagos wzial 151 tys. zl, choc nie walczyl ani dnia.
5. **Chleb biedoty** (M3): gdy wyrobnikow miasta nie stac na jutrzejsze jedzenie, korona doplaca im roznice (a gdy korona nie ma - pan miasta). Kosztuje 0 w pierwszych
   120 dobach, ok. 6 tys. zl dziennie pod koniec roku 1 i 15-21 tys. pod koniec roku 2 - przy 600-900 tys. zl wplywow koron dziennie. Siedem biednych miast przestaje glodowac
   z biedy, bez wzgledu na to, czemu zbiednialy.
6. **Danina w trwalym pokoju** (M4) - tylko na Twoje slowo: mieszczanie krolestw w pokoju odkladaja dzis bez konca (16.6 mln, +34-48 tys. zl dziennie), bo danine
   zgodziles sie brac tylko "na czas wojny". Projekt jest gotowy (setna-dwusetna czesc nadwyzki dziennie, dopiero po 91 dobach pokoju), ale bez Twojego "tak" zostaje wylaczony.
   Te pieniadze i tak nie gina - czekaja na wojne jak skarb (tak bylo zaprojektowane w daninie z 13.10).
7. Skutek trzech zmian w roku 2 [S, liczone na biegu dwuletnim]: brak na zold, ktorego nikt nie pokrywa, **54 -> ok. 42 tys. zl dziennie**; wojsko w polu **+ok. 1 tys.**;
   Zelazne Wyspy nie spadaja juz do 1.4 tys. ludzi (alarm 10); chleb biedoty 90 -> ok. 95% pelnego koszyka, jedzenie ok. 100%.
8. Czego minimum nie zrobi: Zelazne Wyspy i Wolni Ludzie zostaja biedne (ok. 35 tys. zl dziennie braku) - ich lud ma 1% oszczednosci swiata. Pokryc to moze tylko
   przelew MIEDZY krolestwami (nowa regula, nie w tym projekcie). Pierwszy rok sie nie zmienia: w dobach 93-120 te zmiany przesuwaja ok. 3 tys. zl dziennie.

---

## 1. Zalozenie: co jest "minimum"

**Alarmy roku 2 (BD 2.6) i ich napedy** [P BD 2.6, `wojsko\a5_saldo56_dw.out.txt`; przypisanie S]:

| # | alarm | rok 2 | naped | kto gasi |
|---|---|---|---|---|
| 1 | miara W5-7 >= 90 | 55.3 | mapa (ok. 44 tys.), pieniadz rodow w wojnie (-4..-6 tys.) | mapa: Z.1 + /4.33; pieniadz: M1 + M2 (+ok. 1 tys.) |
| 3 | biedne rody: ludzie / G/R | min 12.0 tys. | glownie pokoje | Z.1 + /4.33; M1 + M2 do rodow G < R Wysp i Wolnych Ludzi |
| 5 | saldo rodow bez luzu > -45 | -59.9 / -62.4 (d365-476), potem -10.8 sr. | przejscie: gasnie zapas koron 1/360 (64.5 -> 12.0) | czesciowo M1 + M2; prog do rozbicia (rozdz. 5) |
| 7 | luz w puli zoldu >= 15 tys. | 10.3 | mniej rodow w wojnie (132 zamiast 210) | mapa |
| 9 | krolestw w wojnie >= 23 | 17.3 | mapa | Z.1 + /4.33 |
| 10 | Polnoc / Dorne / Wyspy | Dorne 0.09 (pokoj), Wyspy min 1.4 | Dorne - mapa; Wyspy - pieniadz | Dorne: /4.33; Wyspy: M2 + M1 |
| 2 | kiesy ludu / danina (NIE na granicy) | danina min 152.5 (1 okno) | pokoj | /4.33; M4 (gdy wlaczona) daje zapas |

**Regula wyboru** (kazda zmiana musi spelnic wszystkie trzy):
1. dotyczy zachowania naszej reguly, ktore jest zle **niezaleznie od mapy wojen** (zostaje takze po /4.33);
2. dziala na 100% rodow / osad tym samym wzorem (bez wyjatkow dla nazw krolestw);
3. w dobach 93-120 nowej kampanii przesuwa najwyzej kilka tys. zl/d (bieg 120 dob ma zostac ten sam).

Czego NIE robie i dlaczego: nie ruszam fabuly ROT ani Diplomacy (C1 Jeffa, /4.33 juz w grze); nie zmieniam Z.1 (`PeaceChestShare`) ani zalog w pokoju (`GarrisonPeaceShare`) -
najpierw pomiar /4.33; nie dodaje przelewu miedzy krolestwami (nowe zrodlo); nie ruszam zarobku ludu "za ludzmi" (A4/A5-7 - zmiana rynku, ryzyko roku 1); pomysly F1-F4 Jeffa - nie.
Zakresow innych prac nie dotykam: towary-2 (plotno, skora, zbroja, lokata d120), drobne-2 (A10-A14, w tym zawor Braavos GL2-9), C3-C5.

Zbieznosc: niezalezny projekt `PROJEKT-BUDZET.md` doszedl do tych samych trzech zmian co M1 i M2 (tam B1, B2, B3) - moje liczby policzylem osobno (rozdz. 10) i wychodza
te same (M1 7.0 tys. zl/d w roku 2; wstrzymane raty 1.9 mln). Roznice: M2 zostawia zerwanie porozumienia, gdy korony nie stac w ogole (B2 je znosi); A4 przez chleb, nie przez
zwolnienie z daniny (B5); bez zmiany zalog w pokoju (B4) - tu tylko prog na krolestwo.

---

## 2. Zestawienie

| # | regula (klucz MCM, domyslnie; off = 449d453) | poz. | kto placi -> kto dostaje | skutek rok 2 [S] | rok 1 / bieg 120 dob [P/S] |
|---|---|---|---|---|---|
| **M1** | Garnek W5 z reszty wplywow dnia (`NeedAidFromRents`, bool, **on**) | A3 | skarbiec (czesc, ktora szla w renty 180) -> glowy rodow z niepokryta potrzeba W5, za ludzi, ktorzy sa | do W5 +7.0 tys. zl/d (Wyspy 3.5, Wolni Ludzie 2.3, Pentos 0.6); zold netto ok. +2.8 tys. zl/d | Q2 / Q3 / Q4: 4.8 / 8.4 / 12.0 tys. zl/d; d93-120 0.9 |
| **M2** | Rata wezwania 168 za wojne i po wlasnych zolnierzach (`CallToWarPaysForWar`, bool, **on**) | A15, A3 | brak przelewu: rata zostaje w skarbcu wzywajacego (dalej zwrot, W5, M1, renty) | wstrzymane 1.94 mln (5.3 tys. zl/d): Wyspy 1.39, Smocza Skala 0.48 mln | rok 1 wstrzymane 0.46 mln (Pentos 0.26, Aegon 0.10); Skagos 151 tys. zostaje u Strazy; d93-120 2.4 tys. zl/d |
| **M3** | Chleb biedoty (`BreadForThePoor`, bool, **on**) | A4 | skarbiec krolestwa (nie ponizej 250 000), gdy nie ma - glowa rodu pana osady (tylko zloto ponad rezerwe wojny R; nigdy gracz) -> kiesa wyrobnikow / podzamcza | koszt Q5-Q8: 5.9 / 9.2 / 14.0 / 14.8 tys. zl/d (d701-728: 20.6); stac na jedzenie ok. 100%, P1-6 89.7 -> ok. 95% | Q2 0.4, Q3 3.0, Q4 5.6; **d93-120: 0** |
| **M4** | Danina w trwalym pokoju (`PeaceFolkAid`, bool, **off do slowa Jeffa**; `PeaceFolkAidShare` 0.2) | A5 | kiesy ludu krolestwa w pokoju >= 91 dob (ponad zapas dni) -> skarbiec -> renty 180 | start 78-90 tys. zl/d, potem do 34-48; lokata pokoju 16.6 -> ok. 9-10 mln | d93-120 1.2, Q3-Q4 2.7-3.7 tys. zl/d (3 krolestwa zawsze w pokoju) |
| L1 | Napis linii 165: raty okupow krolow osobno od reparacji (bez klucza - sam log) | A15 | - | - | - |
| - | bez kodu: zalogi (prog na krolestwo), pokoj fabularny, najemnicy w pokoju, "+373 tys.", lokata w wojnie (prog) | A15, A5 | - | rozdz. 4 | - |

Razem M1 + M2 [S]: niepokryte po W5/W5b/W6 w roku 2 **53.9 -> ok. 41.6 tys. zl/d** (Wyspy 23.9 -> ok. 16.6; Wolni Ludzie 20.7 -> ok. 18.4; Smocza Skala 1.9 -> ok. 0.5);
po W7 39.1 -> ok. 27. Zold netto ok. +5.5 tys. zl/d = **ok. +1.0 tys. ludzi** w krolestwach w wojnie (po odjeciu strat wezwanych). Zero zlota z niczego, zero nowego dlugu.

---

## 3. Reguly szczegolowo

### M1. Garnek W5 z reszty wplywow dnia (A3)

**Dowod.** Garnek W5 = danina P1 tej doby, najwyzej reszta wplywow dnia po zwrocie [P `KingdomTreasury.NeedAid.cs` :173]; reszta wplywow idzie tej samej doby
w renty 180 wedlug lenn [P `CrownRents.cs` :579]. W krolestwach z niepokryta potrzeba renty to 8.9 tys. zl/d w roku 2 (Wyspy 3.5, Wolni Ludzie 2.3) przy braku 53.9 [P `m_m1m2.py`].
Jeff 13.10 wybral "korona placi wedlug potrzeby (najpierw rody w polu na pulapie zoldu)" [P STAN-PRAC 16:45 13.10].

**Regula.** W wojnie, gdy po szczeblach danina -> skarbiec ponad zapas wojny (W5b) -> tarczowe (W6) zostaje niepokryta potrzeba, dochodzi CZWARTY szczebel:
reszta wplywow dnia (`CrownIncome.LeftFor(k)` po zwrocie i po wyplatach W5) - ten sam poziom wyrownania, ta sama gotowka "za ludzi, ktorzy sa", to samo miejsce na krok
werbunku. Renty dziela to, co zostanie. Szczeble nizej bez zmian co do zlotowki (wzor W5b reguly 2).
- **Kto -> komu:** `Kingdom.KingdomBudgetWallet` -> kiesa glowy rodu (najpierw skarbiec, potem glowa; wyjatek = zloto wraca); `CrownIncome.Spent(k, x)`, `KDay.NeedAid += x`.
- **Liczby (decyzja):** bez nowego parametru - cel 60% limitu i krok 10% zostaja (`CrownWarAidFillTarget`, `CrownWarAidRecruitStep`). Nie podnosze celu: rody z doplata i tak
  nie werbuja do celu (W5-4 43.4 -> 40.5% [P BD 7 nr 31]), wiec wiecej celu = wiecej "miejsca", nie wiecej ludzi.
- **Skutek rok 2 [P przeplyw / S ludzie]:** do W5 7.0 tys. zl/d (Q5 8.7, Q6 2.9, Q7 6.5, Q8 9.9). Ludzi: renta wchodzi do D (pulap 0.60 D x 0.8-1.0, reszta D na dwor, sprzet,
  budowy), doplata W5 idzie na zold 1:1 - netto ok. +0.4 zl zoldu na 1 zl -> ok. +2.8 tys. zl/d = ok. +0.5 tys. ludzi (5.7 zl/d na czlowieka [P Z.1 d752-781]).
  Z M2 wstrzymane raty Wysp tez trafiaja tym szczeblem do druzyn (1:1).
- **Rok 1:** przesuwa renty -> W5 4.8 / 8.4 / 12.0 tys. zl/d w Q2 / Q3 / Q4 (Wyspy, Wolni Ludzie, Lys, Tyrosh), w d93-120 0.9 [P `m_m1m2.py`]. Kierunek dla miary W5-7 dodatni;
  26 krolestw bez braku - bez zmian.
- **Kontrola calosci (zasada 0):** W5b regula 1 (`WarAidFollowsLordsShortfall`) czyta brak SPRZED tego szczebla - osobne pole `UncovPreRent` (inaczej danina z ludu
  rosla by wolniej; regula 1 zostaje co do zlotowki). W7 (`LvPlan`) dostaje poziom PO tym szczeblu (pan bierze od ludu tylko to, czego korona nie pokryla). Gotowka M1
  to gotowka W5: `NeedAidRefundCut` (bez zwrotu 70% nastepnej doby - "1 zl = 1 zl"), `NeedAidCashOf` (dwor i niewydany sprzet), kolumna `doplata_w5`. Kolejnosc doby
  bez zmian (krok w `CrownRents.Daily` przed odczytem puli rent). Rod gracza (W6b): ta sama regula, sama gotowka. Kontrola "gotowka przy zoldzie w pulapie wlasnym" = 0.
- **Klucz:** `NeedAidFromRents` (bool, on). MCM (EN): *"In war, when the day's war aid, the crown's spare treasure and the shield money still leave houses of the realm
  short of wages for their field companies, the crown pays those wages from the rest of its daily income before it shares out rents by fiefs - only for men who stand in the
  companies today. Off = the rest of the day's income goes out as rents, as before."*
- **Linia:** "Doplata wedlug potrzeby (W5)": nowy odcinek `| z reszty wplywow dnia (M1): pula X, wyplacono Y (rodow N), krolestw K; niepokryte przed rentami U0, po rentach U1`
  i w wierszu krolestwa `z rent Y`; w "Korona: wplywy dnia (165)" na krolestwo `doplata W5 Z (w tym z rent Y)`.
- **Prog:** PRM-1 (rozdz. 7).

### M2. Rata wezwania 168 za wojne i po wlasnych zolnierzach (A15 + A3)

**Dowod.** (a) Straz -> Skagos przeciw Wolnym Ludziom, d13-70: 2 647 zl/d x 57 dob = 150 879 zl przy 0 dobach wojny Skagos [P ROZP-RODY 5.2; ten sam przypadek w raporcie rocznym:
117 565 zl]. Kod przed rata nie sprawdza, czy wezwany walczy [P `CrownCallToWar.cs` Daily]. (b) Wezwania placone sa z wplywow dnia PRZED zwrotem, W5 i rentami
[P naglowek `CrownIncome.cs`]; korona Wysp zaplacila 1.39 mln w roku 2 (Q6 7.7 tys. zl/d - renty spadly do 0.1, brak 29.6, zold przyciety 10.0) [P].

**Regula.** Dla kazdego porozumienia (bez wpisow "dlug sprzed paczki", `Called == ""`):
- (a) **rata tylko za dobe wojny:** jesli `called.IsAtWarWith(against)` = nie - dzis bez raty. Po 3 dobach od porozumienia (spoznione wypowiedzenie) liczymy doby bez wojny
  z rzedu; po **7** - porozumienie konczy sie przez gre (`EndCallToWarAgreement`), reszta ceny skreslona sciezka P3/W4 (wyrownanie portfela, gdy postfiks nie zadzialal),
  bez `MakePeaceAction` (nie ma wojny do konczenia). Licznik dob - pamiec sesji (po wczytaniu od 0; bez nowego pola zapisu).
- (b) **najpierw wlasni zolnierze:** rezerwa = wczorajszy zwrot nalezny + wczorajsza niepokryta potrzeba W5 tego krolestwa (przed M1). Rata, gdy `LeftFor - rezerwa >= rata`;
  gdy `LeftFor >= rata`, ale po rezerwie nie starcza - **dzis bez raty, porozumienie trwa** (niezaplacona czesc nie przechodzi na jutro; reszte ceny skresla gra na koncu,
  jak dzis 4.8-5.3 mln rocznie); gdy `LeftFor < rata` - zerwanie jak dzis (bez zmiany sciezki, ktora dala rok 1 108.9 tys.).
- **Kto -> komu:** nikt nikomu - rata zostaje w skarbcu wzywajacego i tej samej doby idzie zwykla kolejka (zwrot, W5, M1, renty). Wezwany dostaje mniej - to zloto, ktorego
  dzis dostaje za wojne, ktorej nie prowadzi (a), albo kosztem glodnych druzyn wzywajacego (b).
- **Liczby (decyzja):** 3 doby laski = najdluzszy znany odstep "porozumienie -> wypowiedzenie" w jednej dobie gry plus zapas; 7 dob = dluzej niz przerwa przy pokoju
  fabularnym ROT (wznowienie w ciagu 2 dob, 111 z 111 [P CS T9]) - para walczaca nie traci porozumienia przez rozejm ROT. Rezerwa bez mnoznika: korona ma oplacic wczoraj
  zmierzone zobowiazania wobec wlasnych ludzi, nic wiecej.
- **Skutek rok 2 [P `m_m1m2.py`]:** wstrzymane 1.94 mln = 5.3 tys. zl/d (Wyspy 1.39, Smocza Skala 0.48, Pentos 0.06 mln); z M1 idzie w druzyny Wysp. Wyspy w Q6 / Q7
  +7.7 / +5.8 tys. zl/d -> ok. +1.0-1.3 tys. ludzi w dolku (alarm 10: min 1.4 -> ok. 2.4) [S: 5.7-6.5 zl/d na czlowieka]. Wezwani (Dorzecze, Pentos, Smocza Skala) traca
  przychod rodow 5.3 tys. zl/d -> ok. -0.3..-0.5 tys. ludzi [S]. Skagos-podobne: rok 2 - 0 przypadkow.
- **Rok 1:** wstrzymane 0.46 mln (1.3 tys. zl/d; Pentos 0.26, Aegon 0.10, Smocza Skala 0.05, Tyrosh 0.04); d93-120 2.4 tys. zl/d (Aegon); Straz zatrzymuje 151 tys. (d13-70).
  Zerwan z pokojem sojusznika tyle samo co dzis (ta sama sciezka przy `LeftFor < rata`).
- **Kontrola calosci:** 168 biegnie przed zwrotem i W5 - rezerwa z wczoraj (pamiec `KDay.RefundDue` i brak W5 zapisywane przy linii W5 do slownika krolestw; pierwsza doba
  po wczytaniu - rezerwa 0, jak dzis). `Measure31.CtwPay` i `EndPostfix` bez zmian; sciezka (a) uzywa tego samego wyrownania co zerwanie (P3/W4, `_ending`, warunek "wpis
  dalej na liscie"). Gracz jako wzywajacy albo wezwany: komunikat (EN) przy (a). Do sprawdzenia przez autora kodu: czy Diplomacy przy decyzji o pokoju czyta portfel wezwan
  (cudzy kod) - jesli tak, wezwany bez raty moze wczesniej konczyc wojne.
- **Klucz:** `CallToWarPaysForWar` (bool, on). MCM (EN): *"The crown pays an ally's daily part of the call-to-war price only for days the ally is really at war with the enemy
  it was called against, and only from what is left of the day's income after yesterday's wage refunds and its own houses' unpaid wages; an unpaid day's part is not carried over.
  An ally that has not been at war with that enemy for seven days in a row (after three days' grace) is released and the rest of the price is struck off. Off = the crown pays
  every day's part while it can, as before."* Komunikat gracza (a): *"{ally} has not taken the field against {enemy}. {crown} stops paying for the war and the agreement is ended."*
- **Linia:** "Wezwania do wojny (168)": `| za wojne (M2): dob bez wojny wezwanego X (porozumien N), zakonczone bez wojny Z (skreslono R zl); wstrzymane po wlasnych zolnierzach
  W zl u K koron (rezerwa: zwrot wczoraj + brak W5 wczoraj)`.
- **Prog:** PRM-2, PRM-3.

### M3. Chleb biedoty (A4)

**Dowod.** Luka jedzenia klas (potrzeba kotwicy x udzial jedzenia FdW 0.80 / FdR 0.70 / FdP 0.45 minus kiesa klasy) [P `m_chleb.py` na `kiesa-obieg.csv`; kod `TownFolk.Live.cs`
Anchor, `TownFolk.cs:50`]: d93-120 **0**, Q2 0.4, Q3 3.0, Q4 5.6, Q5 5.9, Q6 9.2, Q7 14.0, Q8 14.8, d701-728 20.6 tys. zl/d; pelny koszyk 9.6 (Q4) -> 25.8 (Q8) -> 35.6.
W Q8: Oldtown 6.8, Braavos 3.6, Rhyos 1.7, Aquos Dhaen 0.8, Selhorys 0.7, Volon Therys 0.7 (krolestwa: Reach 7.0, Braavos 5.3, Volantis 1.4, Lys 0.8). Przyczyny sa rozne
(Oldtown - za duzy rynek od Q1; Braavos i Rhyos - pusta kasa kupcow, mechanizm nie potwierdzony; Highgarden - danina zdjela zapas; Volantis, Lys - pokoj zabral wojsko
[P ROZP-LUD 7]). Ta regula nie zaklada zadnej z nich - leczy skutek wszedzie tym samym wzorem.

**Regula.** Raz na dobe, w kotwicy "na jutro" (`TownFolk.Live.cs` Anchor), PRZED policzeniem `Afford`: dla wyrobnikow miasta (klasa W) i dla podzamcza luka =
max(0, potrzeba jutra x udzial jedzenia - kiesa; podzamcze: kiesa + kredyt dworu). Luka jest doplacana do kiesy tej klasy:
1. ze skarbca krolestwa wlasciciela osady, najwyzej do stanu 250 000 (polowa `CrownReserveGold`) - wydatek schodzi z wplywow dnia tej korony (165 liczy wplywy netto:
   `CrownIncome.Begin` :195), wiec chleb jest pierwszym wydatkiem dnia, przed rata Banku, zwrotem, W5 i rentami;
2. gdy skarbiec nie ma - z kiesy glowy rodu pana osady, tylko ponad jego rezerwe wojny R (`ClanBudget`); **nigdy z kiesy gracza** (osada gracza - tylko korona);
3. gdy nikt nie ma - luka zostaje (licznik "nie stac nikogo").
Tylko jedzenie (FdW 80% koszyka wyrobnika), nigdy reszta koszyka. Wyrobnicy osady, ktora tej doby dostala chleb, nastepnej doby nie placa podatku wojennego 184 od zarobku
(`PayCrown(f, 0, WtW, eW)`) - korona nie bierze z tego, co sama daje. Danina P1 i W7 bez zmian (i tak biora tylko ponad 10 dni zapasu, czyli nie od biednych).

- **Kto -> komu:** skarbiec (albo glowa rodu pana) -> kiesa W / podzamcza tej osady -> kupcy za jedzenie (kasa miasta) -> robocizna i zawor (pan, korona). Zamkniete.
- **Liczby (decyzja):** prog 250 000 skarbca - korona nigdy nie zostaje bez polowy zapasu (Bank, okupy); tylko jedzenie - "nikt nie glodzi z biedy", a reszta koszyka zostaje
  sprawa zarobku (inny projekt); kolejnosc korona -> pan: korona ma danine (pieniadz ludu wraca do ludu krolestwa) i rozklada koszt na wszystkie rody przez renty (2/3 rent idzie
  do rodow z luzem); pan tylko jako zastepca, bo koron miast-panstw w pokoju nie stac (Braavos 1.1 tys. zl/d wplywow, Lys 0.2 [P 165 d728]), a ich rody maja 0.83 i 0.52 mln.
- **Skutek rok 2 [S]:** koszt 5.9-14.8 tys. zl/d (do 20.6 pod koniec) wobec 594 tys. zl/d wplywow koron i 359 tys. rent (Q8) [P dw.pkl]; Reach placi z rent 51.6 tys. (d728) ok. 7-12;
  Braavos: skarbiec ponad 250 tys. ok. 290 tys. (ok. 50-80 dob), potem rody (0.83 mln). Stac na jedzenie ~1.00 w osadach z platnikiem, miast z jedzeniem < 95%: 3.9 -> ok. 0;
  "zjedzone bez zaplaty" 1.8 tys. zl/d -> ok. 0; P1-6 (pelny koszyk) 89.7 -> ok. 95% (luka jedzenia to 57% luki pelnej). Kasa kupcow Braavos i Rhyos dostaje zaplate za chleb
  -> wraca czesc robocizny (sprawdzi pomiar; zawor Braavos GL2-9 - zakres drobne-2).
- **Rok 1:** 0 do d91, Q2 0.4, Q3 3.0, Q4 5.6 tys. zl/d (Oldtown, Rhyos); **bieg 120 dob: 0** (luka w d93-120 = 0 [P]).
- **Kontrola calosci:** kontrola zamknieta kies (linia "Kiesa ludu", r = 0) - nowa pozycja wplywu "chleb"; `MoneyLedger` - przelew miedzy posiadaczami (skarbiec / glowa
  -> kiesa ludu), nie zrodlo; licznik okna dla linii 165 (jak `FolkWarAidWindow`), zeby "Other" nie chowal chleba; E5/C2 (glod z braku pieniedzy) i R14 (zadowolenie przy
  Afford < 0.98) zgasna w tych osadach - zamierzone; wiecej zakupow jedzenia z polek w tych miastach (Braavos 3.5, Reach 4.9 dnia na polkach w Q8) - do pomiaru; podzamcze:
  luka dopiero po kredycie dworu (`TownFolkCastleCourtFeeds`) - bez drugiego karmienia. Gracz-krol: placi skarbiec krolestwa, nie kiesa gracza.
- **Klucz:** `BreadForThePoor` (bool, on). MCM (EN): *"When the labourers of a town or the folk of a castle cannot pay for tomorrow's food from their purse, the realm's
  treasury pays the difference into their purse (never below a quarter of a million); if it cannot, the lord of the place pays from his gold above his war reserve. Only food,
  never the rest of the basket; your own purse is never used. Labourers fed this way pay no war tax on their earnings the next day. Off = the poor buy only what their purse allows,
  as before."*
- **Linia:** nowa "Chleb biedoty (M3): dzien N | osad z luka X (miasta / podzamcza), luka Y zl; wyplacono Z (korona A, pan B), nie stac nikogo C zl w D osadach;
  zwolnieni z 184: E | top 5 osad | na krolestwo ..."; w "Kiesa ludu" pozycja wplywu "chleb"; w 165 na krolestwo "chleb Z".
- **Prog:** PRM-4.

### M4. Danina w trwalym pokoju (A5) - wylaczona do slowa Jeffa

**Dowod.** Danina P1 w pokoju = 0 [P `KingdomTreasury.WarAid.cs` :118]; lokata krolestw w pokoju 0.83 -> 16.63 mln (d364 -> d728), +34-48 tys. zl/d (Z.1 przyspiesza)
[P ROZP-LUD 4.2-4.3]. Zgoda Jeffa 13.10 i jego "podatki ... na czas wojny" dotycza wojny - dlatego klucz domyslnie wylaczony.

**Regula (gotowa do wlaczenia).** W `WaRate`: krolestwo NIE w wojnie i w pokoju od co najmniej **91 dob** (najnowsza `StanceLink.PeaceDeclarationDate` wobec wszystkich
krolestw - pole gry, bez nowego zapisu) -> stawka dnia = `FolkWarAidShare` x `PeaceFolkAidShare` = 0.025 x 0.2 = **0.5%/d** kiesy ponad zapas dni (te same zapasy W 10 /
R 21 / P 30 / podzamcze 10). Reszta jak P1: przelew do skarbca, wplyw dnia 165, korona wydaje go tej doby rentami 180 (w pokoju nie ma W5 ani zwrotu). Kara lojalnosci P1b
dziala jak przy kazdej daninie.
- **Liczby (decyzja):** 91 dob = Twoja minimalna dlugosc wojny i pokoju w Diplomacy (13.10) - pokoj krotszy to "przerwa w wojnie", w ktorej lud trzyma skarb na wojne;
  0.5%/d - rownowaga na krolestwo jak w wojnie: przyrost w pokoju ok. 3 tys. zl/d na krolestwo / 0.005 = ok. 0.56 mln na krolestwo, w wojnie 10.7 mln / 15.1 = 0.71 (Q8) [S].
- **Skutek rok 2 [P stan / S przebieg, `m_pokoj.py`]:** na starcie 78-90 tys. zl/d (pokoj >= 91 dob, Q8 - d729), spada z polowa co ok. 139 dob do 34-48 tys.; lokata pokoju
  16.6 -> ok. 9-10 mln; renty panow w pokoju +ok. 60% (D w pokoju 124 tys. zl/d [P STAN-PRAC 09:30]) -> pulap pokoju Z.1 +ok. 20 tys. zl/d -> ok. +2.5-3 tys. ludzi w pokoju
  (cel Z.1 11.3-17.8 tys., dzis 13.5); alarm 2: min danina z 152.5 w gore o 5-40 tys. zl/d.
- **Rok 1:** d93-120 1.2 tys. zl/d, Q3-Q4 2.7-3.7 (3 krolestwa w pokoju caly rok) [P].
- **Kiedy wlaczyc:** slowo Jeffa ("tak - w dlugim pokoju mieszczanie tez oddaja koronie czesc oszczednosci"). Bez niego: bieg dwuletni z /4.33 pokaze, czy dziura jest
  (prog: lokata krolestw w pokoju >= 91 dob > 8 mln w d728 albo przyrost > 20 tys. zl/d) - wtedy pytanie z liczbami.
- **Klucz:** `PeaceFolkAid` (bool, **off**), `PeaceFolkAidShare` (float 0.2). MCM (EN): *"In a lasting peace (at least 91 days without war) the townsfolk and castle folk
  give the crown Peace Folk Aid Share of the war aid share a day from their coin above the reserve; the crown shares it out the same day as rents. Off = nothing is taken in peace."*
- **Linia:** "Danina z oszczednosci (P1)": `| pokoj trwaly (M4): krolestw X (>= 91 dob), stawka 0.50%/d, pobrano Y` i w wierszu krolestwa `(pokoj, M4)`; powod "pokoj albo s = 0"
  rozbity na "pokoj < 91 dob" i "s = 0 w wojnie". **Prog:** PRM-7b.

### L1. Napis linii 165 (A15, sam log)

"raty reparacji" / "dlugi reparacji" w linii 165 to w ok. 90% raty okupow krolow (178), bo `CrownIncome.AddCrownDebt` dopisuje je do tej samej listy (rok 2: 16.3 mln rat przy
1.9 mln reparacji) [P ROZP-RODY 5.3]. Napis: `raty dlugow koron: reparacje X / okupy krolow (178) Y; nowe dzis: reparacje N / okupy M`. Zachowanie bez zmian, bez klucza.

---

## 4. Bez zmiany kodu - decyzje z liczbami

| sprawa (inwentarz) | decyzja | dowod / liczba | prog |
|---|---|---|---|
| A15 "korona nie doplaca do zoldu zalog" (58 przy 69 tys.) | bez kodu; prog na krolestwo | w wojnie zalogi stoja na 3.39-3.45 tys. na krolestwo (Q5 89.3 / 25.9, Q8 51.2 / 15.1); spadek to pokoje; Z.1: w pokoju 0.85 tys. na krolestwo (14.5 / 17, d781), swiat 58.7 -> 64.3 tys. w 60 dob [P] | PRM-6: zalogi >= 3.0 tys. x krolestw w wojnie + 0.7 tys. x krolestw w pokoju (z twierdza): d120 81.2 >= 77.8 TAK, d364 89.2 >= 80.1 TAK, d728 bez Z.1 58.2 < 63.9 NIE, Z.1 d781 64.3 >= 62.9 TAK |
| A15 pokoj fabularny - zwyciezca 0 | zostawic (fabula ROT; latka T9 - decyzja Jeffa 08.10) | 111 z 111 wojen ROT wznowil w ciagu 2 dob; reparacja za taki pokoj = przelew za nic; zwykle pokoje roku 2 placa 1.9 mln [P] | - |
| A15 najemnicy w pokoju z zapasu | zostawic | rok 2 0.3-1.8 tys. zl/d kontraktow w pokoju, z zapasu najwyzej 0.1-0.6; zapas gasnie (rok 3 ok. 2-4 tys. zl/d 1/360 swiata) [P/S] | - |
| A15 "+373 tys." z rozliczen rodow | zostawic logike | 0 zrodel bez platnika w 727 dobach; wezwania przyjete 6 528 tys. wobec 6 537 tys. wplaconych do portfeli [P]; rozbicie linii - diagnoza, nie minimum | - |
| A5 lokata w krolestwach w wojnie | zostawic; prog do rownowagi reguly | rownowaga daniny 2.2%/d: d364 15.48 mln przy nadwyzce 250-380 tys. zl/d; prog 6 mln liczony na nadwyzke 49-146 tys. [P ROZP-LUD 4.4] | PRM-7a: lokata W krolestw w wojnie <= 16 mln; w pokoju INFO (M4 off) |
| A3 Wyspy i Wolni Ludzie - reszta braku | poza minimum (przelew miedzy krolestwami = nowe zrodlo) | po M1 + M2 zostaje ok. 35 tys. zl/d; lud obu krajow zarabia 19 tys. zl/d, kiesy 0.06 + 0.30 mln (1.1% swiata); "reszta" daniny krolestw bez braku 200 tys. zl/d idzie w renty - pokrycie = ok. 17% tej reszty [P/S] | PRM-8 (alarm 10 Wyspy) |

---

## 5. Alarmy roku 2 po minimum [S]

| # | dzis (BD 2.6) | po M1 + M2 + M3 (M4 off), ta sama mapa | czego trzeba jeszcze |
|---|---|---|---|
| 1 | 55.3 tys. | ok. 56.3 (+1.0) | mapa: Z.1 + /4.33 (bieg dwuletni) |
| 3 | min 12.0 tys., G/R 0.54 | ludzie +ok. 0.5 tys. u rodow G < R Wysp i Wolnych Ludzi; G/R bez zmian | mapa |
| 5 | -59.9 / -62.4 (d365-476) | ok. -47 / -49: dalej TAK formalnie (o wlos), przejsciowo (wygasanie 1/360 64.5 -> 20.2 w Q5); rok 2 sr. -10.8 -> ok. 0 | **prog do rozbicia:** "G < R" (bieda; dzis -31.7 / -24.0 -> ok. -22 / -14, prog -30: NIE) i "na pulapie" (wydawanie skrzyni wojennej 0.8 (G-R)/45 - zamierzone; INFO) [P `a5_saldo56_dw.out.txt`] |
| 7 | 10.3 tys. | bez zmian | mapa (132 zamiast 210 rodow w wojnie) |
| 9 | 17.3 krolestw | bez zmian | /4.33 |
| 10 | Dorne 0.09; Wyspy min 1.4 | Wyspy min ok. 2.4 (M2 w Q6-Q7 +5.8-7.7 tys. zl/d) - czesc Wysp gasnie | Dorne: /4.33 |
| 2 | NIE na granicy | M3 nie rusza daniny; M4 (gdy on) +5..+40 tys. zl/d w najslabszym oknie | - |

Wniosek: minimum gasi Wyspy w alarmie 10 i biede w alarmie 5 (po rozbiciu progu), doklada ok. 1 tys. ludzi do alarmow 1 i 3. Alarmy 1, 3, 7, 9 i Dorne w 10 rozstrzyga mapa -
pomiar biegu dwuletniego z Z.1 + /4.33; jesli tam mapa wroci (>= 23 krolestw w wojnie), pieniadz roku 2 wedlug rachunku to ok. -6..-11 tys. ludzi wobec roku 1 (BD 2.2), czyli
miara ok. 98-103 tys. przy progu 90 [S].

---

## 6. Co zostaje poza minimum (zeby sedziowie widzieli koszt)

- **Wyspy + Wolni Ludzie ok. 35 tys. zl/d braku** - pokrycie wymaga przelewu miedzy krolestwami (np. czesc "reszty" daniny 200 tys. zl/d krolestw bez braku, z warunkiem "nie dla wroga").
  Daje ok. +5-6 tys. ludzi tylko tym dwom krajom; nowa regula, ryzyko obiegu (placenie wrogowi) - osobny projekt.
- **Pelny koszyk biedoty** (reszta luki 11 tys. zl/d w Q8) i zarobek, ktory nie idzie za dobrobytem (+42% w 2 lata, przyczyna nieznana) - zmiana rynku, ryzyko roku 1.
- **Zalogi w pokoju ponad 50%** (`GarrisonPeaceShare`) - istniejacy klucz; wlaczyc dopiero, gdy PRM-6 da NIE w biegu z /4.33.
- **Zapas koron po roku 2** (1/360: 12.0 -> ok. 2.4 tys. zl/d w roku 3) - z M4 korony krolestw w pokoju dostaja doplyw; bez M4 zyja z wplywow dnia.

---

## 7. Test i progi (grupa `pr2m` dla `tools/sprawdz_logi.py`)

Kolejnosc wdrazania (jedna zmiana naraz w diagnozie, kazda z wlasnym kluczem): **M2 -> M1 -> M3** (M4 tylko po slowie Jeffa). Dwie proby jak w workflow:
(1) zapis `autotest-rok2-d780-fin24` + 60 dob (rok 2; /4.33 juz w ustawieniach), (2) nowa kampania 120 dob (rok 1). Baza: W53 (`Armoury-2026-10-14_03-24-46.log`) i Z.1 (d721-781).

| prog | co | proba | wartosc | dzis |
|---|---|---|---|---|
| PRM-1 | renty krolestw z niepokryta potrzeba po W5/W5b/W6 / ich wplywy dnia; niepokryte po rentach <= przed - wyplacone z rent; gotowka przy zoldzie w pulapie wlasnym | rok 2 | <= 1%; TAK; 0 | renty 8.9 tys. zl/d = ok. 18% wplywow [P] |
| PRM-2 | rata zaplacona w dobie bez wojny wezwanego z celem (po 3 dobach laski) | obie | 0 zl | Skagos 150 879 zl [P] |
| PRM-3 | rata ponad (LeftFor - rezerwa); zerwania z braku wplywow | obie | 0 zl; <= baza (W53 / rok 1: 6 w 364 dobach) | - |
| PRM-4 | osady z jedzeniem W < 0.95 przy platniku z pieniedzmi; koszt chleba; zjedzone bez zaplaty; kontrola kies r | rok 2 / 120 dob | 0; <= 25 / <= 0.5 tys. zl/d; <= 0.2%; 0 | luka 20.6 tys. zl/d (d701-728), 9 osad [P] |
| PRM-5 | rok 1 bez pogorszenia: W5-7 d93-120; danina d91-120; P1-6 | 120 dob | >= W53 - 2.0 tys.; >= 0.97 x W53; >= W53 | W53 [P log] |
| PRM-6 | zalogi swiata | obie | >= 3.0 tys. x krolestw w wojnie + 0.7 tys. x w pokoju (z twierdza) | Z.1 d781 64.3 >= 62.9 [P] |
| PRM-7a / 7b | lokata W krolestw w wojnie / (M4 on) lokata na krolestwo w pokoju >= 91 dob po 182 dobach | obie / rok 2 | <= 16 mln / <= 1.2 x lokata na krolestwo w wojnie | 10.7 mln / 1.12 mln na krolestwo [P] |
| PRM-8 | Zelazne Wyspy: ludzie w druzynach, minimum okna 28 dob | rok 2 | >= 1.5 tys. | min 1.4 [P BD 2.6] |
| PRM-9 | zrodla bez platnika (MoneyLedger); Armoury ERROR; potkniecia M1-M3 | obie | 0 / 0 / 0 | - |

Przy NIE w PRM-5: najpierw off M2 (jedyna z ruchem w d93-120: 2.4 tys. zl/d), potem M1 (0.9); M3 w d93-120 nie dziala.

---

## 8. Czego nie wiem (+ linia diagnozy)

1. Ile krolestw zostanie w wojnie w roku 2 po /4.33, a z tym: zalogi, luz, lokata w pokoju (czy M4 bedzie potrzebna). Linia: bieg dwuletni; wiersze krolestw W5 i P1 juz to licza.
2. Jak koszt chleba rozlozy sie miedzy korony i panow i czy skarbce miast-panstw w pokoju wystarcza. Linia: "Chleb biedoty (M3)" - korona / pan / "nie stac nikogo" na krolestwo.
3. Czy w miastach z chlebem starczy jedzenia na polkach w zimie (Braavos 3.5, Reach 4.9 dnia w Q8). Linia: GL2 "jedzenie na polkach" dla osad z chlebem.
4. Sprawnosc M1 (ok. +0.4 zl zoldu na 1 zl przesuniety) - [S] z formuly pulapu. Linia: W5 "z rent" + `budzet-rodow.csv` (zold druzyn odbiorcow przed / po).
5. Czy wezwany, ktoremu rata nie przyszla, wczesniej konczy wojne (cudzy kod: Diplomacy / gra a `CallToWarWallet`). Linia: KRONIKA WOJEN dla par z wstrzymana rata.
6. Czemu kasa kupcow Braavos i Rhyos pustoszeje (doplyw netto -1.9 tys. zl/d w Q7) - M3 leczy skutek; przyczyna w zakresie drobne-2 (GL2-9). Linia: `kasy-miast.csv` sprzedaz i zakupy osobno.
7. Czy po 60 dobach Z.1 zalogi dalej rosna (64.3 tys. w d781) - PRM-6 w probie d780 + 60 to pokaze.

---

## 9. Kontrola calosci (zasada 0) - skrot

| regula | regresje / kolizje sprawdzone w kodzie | stan |
|---|---|---|
| M1 | `NeedAid.cs` szczeble (danina, W5b `CrownSurplusStep`, W6 `ScutageStep`) bez zmian; W5b regula 1 czyta brak "przed rentami"; `LvPlan` po szczeblu; `NeedAidRefundCut`; `CrownRents.Daily` :579 (pula rent = `LeftFor` po W5) | do kodu |
| M2 | `CrownCallToWar.Daily` (kolejnosc 165 bez zmian), sciezka zerwania P3/W4 i `EndPostfix`; wpisy "dlug sprzed paczki" pominiete; pamiec rezerwy bez zapisu | do kodu; Diplomacy - pkt 8.5 |
| M3 | `TownFolk.Live.cs` Anchor (przed `Afford`), `PayCrown` 184 dla W, linia "Kiesa ludu" (r = 0), `CrownIncome.Begin` (wplywy netto), `MoneyLedger` (przelew), E5/C2, R14, kredyt dworu podzamcza | do kodu |
| M4 | `WaRate` (gate wojny), P1b lojalnosc, `CrownIncome` (pokoj: bez W5 i zwrotu -> renty), `StanceLink.PeaceDeclarationDate` [P pole gry `StanceLink.cs:105`] | off |
| wszystkie | zapis gry bez nowych pol; wyjatek per osada / rod / porozumienie - licznik potkniec (CLAUDE.md 7); napisy gracza po angielsku; `gen_mcm.py` po kluczach; `Armoury.json` Jeffa - nowe klucze, wiec domyslne zadzialaja | - |

---

## 10. Pliki i polecenia

Skrypty (scratchpad sesji c36b73ca, dysk C - tymczasowy): `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\minimum\`
```
T=D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_21-14-13
python -I m_chleb.py T                       # M3: luka jedzenia / pelna klas wedlug kwartalow, top osad i krolestw
python -I m_lud.py T                         # pomocniczo: luka wedlug wydatku gry, danina miast z deficytem, nadwyzka "powod 3"
python -I m_pokoj.py D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log   # M4: nadwyzka krolestw w pokoju (>= 91 dob) z linii P1
python -I m_m1m2.py ..\rody2\dw.pkl 108836 "2-91,92-182,183-273,274-364,365-455,456-546,547-637,638-728,93-120,2-364,365-728"   # M1 i M2b z wyciagu rozpoznania
```
Alarm 5 rozbity: `dwuletni\wojsko\a5_saldo56.py` (grupy "G<R" i "na pulapie" juz w wyniku).
