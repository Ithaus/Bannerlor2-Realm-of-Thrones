# PROJEKT WYBRANY - PIENIADZE ROKU 2 (A3, A4, A5, A15) - 14.10.2026

Autor kodu: sesja c36b73ca (workflow "pieniadze roku 2", krok autora). Baza (wszystkie klucze "off") = `w-toku/zbior-e8` **449d453** (W52 + szybkie wlaczenia + drobne bledy).
Galaz kodu: `w-toku/pieniadze-rok2` w drzewie `zb\pr2`. Tor **L**. Status wszystkich regul: **DO SPRAWDZENIA** (nic nie wgrane, gry nie uruchamialem).
**Po dwoch recenzjach (14.10) - rozdz. 8 "Recenzja i poprawki";** miejsca zmienione w tekscie maja znacznik **[rec. 8.x]**.
Znaczniki: **[P]** pomiar z logu / kodu, **[S]** szacunek (metoda podana). Zrodla liczb: `PROJEKT-MINIMUM.md`, `PROJEKT-BUDZET.md`, `PROJEKT-PRZEPLYW.md` (ten katalog),
`ROZPOZNANIE-RODY.md`, `ROZPOZNANIE-LUD.md`, BD = `docs/test-zbiorczy/BIEG-DWULETNI-2026-10-14.md`, log Z.1 (zapis d720 + 60 dob)
`D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_02-30-30.log` (doby 721-781; z niego zapis `autotest-rok2-d780-fin24`).

---

## 0. Dla Jeffa - prosto

1. Trzy projekty, trzech sedziow. Wygral **MINIMUM** (2 z 3 glosow, 21.5 pkt wobec 18 i 18): najmniej zmian, najmniej ryzyka dla pierwszego roku, kazda regula
   z osobnym wylacznikiem. Z dwoch pozostalych wzialem to, co sedziowie zgodnie chwalili.
2. **Korona w wojnie placi najpierw zold, potem renty** (M1). Dzis renty ida wedlug ziemi, nawet gdy panowie tego krolestwa nie maja na zold. Po zmianie reszta
   dochodu korony idzie najpierw na zold druzyn, ktore stoja w polu; renty dostaja to, co zostanie.
3. **Sojusznik dostaje pieniadze tylko za dni, w ktorych naprawde walczy** (M2a); gdy przez 7 dni nie walczy, porozumienie wygasa - bez pokoju. **Korona placi
   sojusznikowi dopiero po wlasnych zolnierzach** (M2b). **Gdy korony nie stac na rate, porozumienie trwa, tylko dzisiejsza rata przepada** (M2c) - koniec
   "wojen jednodniowych", w ktorych sojusznik wychodzil z wojny po jednym dniu. **[rec. 8.2]** Gdy korona przez tydzien nie zaplaci sojusznikowi ani raty,
   porozumienie sie konczy - bez pokoju, ale sojusznik moze juz sam zawrzec pokoj (gra zabrania mu tego, dopoki porozumienie trwa).
4. **Chleb dla glodnych miast** (M3). Gdy mieszczanie jakiegos miasta albo podzamcza od tygodnia nie maja na jedzenie (ta sama definicja glodu co dzis w grze),
   korona doplaca im z dochodu dnia roznice do dwoch dni jedzenia; gdy korony nie stac - pan tego miasta, ale tylko ze zlota ponad swoja rezerwe wojenna i tylko
   z tej czesci skrzyni wojennej, ktorej nie bierze zold **[rec. 8.4]**. Twoja kiesa nigdy. Lud wydaje kiese jak zawsze (najpierw jedzenie, potem reszta), wiec chleb
   w praktyce pokrywa caly dzienny brak ludzi, nie samo jedzenie **[rec. 8.4]**. Przez pierwsze 119 dni kampanii regula nic nie robi; od dnia 120 moze wejsc od razu.
5. **Zalogi w pokoju na 70% zalogi wojennej zamiast 50%** (B4) - z pieniedzy rodu, korona nic nie placi. Polpuste twierdze krolestw w pokoju znikaja.
6. **Danina w dlugim pokoju - NIE wlaczam.** Zgodziles sie na danine "na czas wojny". Kod nie powstal; gdy bieg pokaze, ze lezy tam ponad 8 mln, zapytam Cie z liczbami.
7. Czego to nie zrobi: Zelazne Wyspy i Wolni Ludzie dalej beda biedni (ok. 30-35 tys. zl dziennie braku) - u nich nikt nie ma pieniedzy, a przelew z innych
   krolestw to nowa regula. 85 tys. wojska w roku 2 rozstrzygnie mapa wojen (Z.1 i zmeczenie wojna / 4.33), nie te reguly. Ile ludzi daja - nie wiem: szacunki 0.2-0.5 tys. ludzi
   sa ponizej rozrzutu miedzy biegami **[rec. 8.3]**. Brak na zold (A3) zostaje otwarty.

---

## 1. Glosy sedziow i wybor

| projekt | sedzia 1 | sedzia 2 | sedzia 3 | suma | zwyciestwa |
|---|---:|---:|---:|---:|---:|
| **MINIMUM** | **8.0** | 6.0 | **7.5** | **21.5** | **2** |
| BUDZET | 6.5 | 5.0 | 6.5 | 18.0 | 0 |
| PRZEPLYW | 5.0 | **7.0** | 6.0 | 18.0 | 1 |

**Dlaczego MINIMUM.** (1) Jedyny poprawny wobec kodu w sprawie garnka W5: M1 to czwarty szczebel PO W5b i W6, wiec W5b regula 2 i W6 zostaja co do zlotowki,
a W5b regula 1 (krok stawki daniny) dalej czyta brak z samego garnka daniny. BUDZET B1 i PRZEPLYW PZ-3 (`row.Pot = LeftFor`) wypieraja W5b i W6 i obnizaja stawke
daniny w krolestwach z brakiem - bez rachunku (sedzia 1, sedzia 2 "przeszczep z M1"). (2) Najmniejszy ruch w roku 1: w d93-120 ok. 3 tys. zl/d. (3) Szanuje
zgode Jeffa "na czas wojny" (M4 wylaczona; PRZEPLYW PZ-1 wlaczal podatek w pokoju - sedziowie 1, 2, 3 przeciw). (4) Zadnych nowych pol zapisu.

---

## 2. Przeszczepy - przyjete i odrzucone

| # | przeszczep (kto prosil) | decyzja | dlaczego |
|---|---|---|---|
| P1 | **B4 `GarrisonPeaceShare` 0.50 -> 0.70** jako osobny krok (s1, s2, s3) | **TAK** (B4) | Wszyscy trzej. Istniejacy klucz; w `Armoury.json` Jeffa go nie ma [P grep] - nowa domyslna zadziala. Placi pulap rodu, korona 0. Prog pilnuje druzyn Z.1. Do linii 166 dopisek "zalogi w pokoju (B4)" - odpowiada, czy w pokoju wiaze cel, czy werbunek (s3). |
| P2 | **Chleb M3 placony w kroku korony PO zwrocie zoldu** z reszty wplywow dnia przed rentami, luka liczona w kotwicy (s1, s2) | **TAK** | Platnosc w `TownFolk.Daily` (przed `CrownIncome.Begin`) obnizala wplywy dnia przed zwrotem -> brak zwrotu w wojnie = kredyt Banku (`KingdomTreasury.cs:296-305`, `CrownBorrow.Lend`) [P kod]. |
| P3 | **Histereza konca glodu** (s1) i **jedna definicja glodu Hunger.Of E5/C2** jako wejscie (s3) | **TAK** | Wejscie = `TownFolk.MoneyHungry` (Afford jedzenia < 0.95 przez 7 dob, kampania >= 120 dob - ta sama funkcja, ktora karze zadowolenie) [P `Hunger.cs`]. Wyjscie = 7 kotwic z rzedu bez luki. Wyzwalacz "zapobiegawczy" (s2) odrzucony: druga definicja tego samego zjawiska (zasada 0). |
| P4 | **M2 rozdzielone na dwa klucze** (s1) + **B2 "bez zerwan" osobnym kluczem** po sprawdzeniu w dekompilacji (s2, s3) | **TAK** (M2a, M2b, M2c) | Sprawdzone [P]: `Bannerlord.Diplomacy.1.4.5/1.4.7.dll` nie zawiera zadnej nazwy z "CallToWar"; gra czyta `CallToWarWallet` tylko w `DefaultAllianceModel.GetScoreOfCallingToWar` (:380 - portfel wzywajacego < -100 000 blokuje NOWE wezwania) i w `DefaultClanFinanceModel` (dochod / wydatek rodow - zalatane przez 168). Niedoplacony sojusznik nie wychodzi wiec z wojny przez portfel. |
| P5 | Linia zmiany zlota swiata w rozliczeniu rodow: zold osobno od reszty (A15-5, s1, s3) | **TAK** (L2) | Sama linia - zamyka "+373 tys." dowodem z logu zamiast decyzji. |
| P6 | Miara A3 "z platnikiem / bez platnika" (s1, s2, s3) | **TAK** | Odcinek M1 linii W5. Prog 0 tylko dla "z platnikiem" (kontrola kodu), "bez platnika" INFO (Wyspy, Wolni Ludzie). |
| P7 | Prog lokaty rozbity: w wojnie <= 10 mln, w pokoju INFO (s2, s3) | **TAK** (prog) | Ostrzejszy niz MINIMUM (16 mln); mierzy, czy dziala istniejacy kanal daniny. |
| P8 | Komunikaty dla gracza po angielsku (s3) | **TAK, waska wersja** | Raz na 7 dob: krol-gracz - ile korona dala na zold przed rentami (M1) i na chleb (M3); pan glodnego miasta - kto zaplacil chleb jego ludziom. |
| P9 | Teza "ok. 44 z 54 tys. spadku to mapa" do raportu (s2) | **TAK** | Rozdz. 0 pkt 7. |
| P10 | Dyscyplina M4: danina pokoju wylaczona do slowa Jeffa; po "tak" wersja PZ-1 "na potrzebe" (s2, s3) | **TAK (bez kodu)** | Kodu M4 nie pisze (CLAUDE.md 8.3: nie dodawac "na zapas"). Pytanie do Jeffa z liczbami - rozdz. 3.8. |
| P11 | Podzial alarmu 5 na "G<R" i "na pulapie" (s2) | **TAK (prog bez kodu)** | Skrypt `a5_saldo56.py` (MINIMUM rozdz. 10); w grupie rok2 nie ma CSV rodow tej proby - opis w planie testu. |
| P12 | B5 "miasto bez zarobku nie placi daniny" (s1, opcjonalnie) | **NIE** | Obniza danine o 1-4 tys. zl/d przy alarmie 2 na granicy (s2, s3); M3 leczy to samo bezposrednio. Druga regula na to samo zjawisko. |
| P13 | Zwolnienie nakarmionych z podatku 184 (z M3 MINIMUM) | **NIE** | Wyjatek malo czytelny (s3); chleb i tak doplaca do luki po wszystkich podatkach - zwolnienie przesuwaloby te same pieniadze w kolko. |
| P14 | PZ-6 zrodla "inni panowie krolestwa", zawor kasy (PRZEPLYW) | **NIE** | Skrzynia 0.2 x (G-R)/45 w wojnie to budzet sprzetu (`ClanBudget.cs:503`) [P]; obcy pan nie ma interesu karmic cudzego miasta; zawor to zakres drobne-2 (A12). Pan zastepczo placi tylko za WLASNE miasto, w tempie skrzyni. |

---

## 3. Reguly

Kolejnosc doby korony po zmianach (165): wplywy dnia -> rata Banku (186) -> dary (182) -> raty reparacji i okupow -> kontrakty (185) -> **wezwania 168 (M2a/b/c)**
-> nagrody 178 -> zwrot zoldu -> [w `CrownRents.Daily`, na krolestwo] warunki rent -> W5 danina -> W5b skarbiec -> W6 tarczowe -> **M1 z reszty wplywow** ->
**M3 chleb z reszty wplywow** -> renty 180. Krolestwa bez wiersza rent (nikt z lennem) - chleb w kroku zaraz po rentach (`TownFolk.BreadRest`). Pan zastepczo - w tym samym kroku.

### 3.1 M1 - korona w wojnie placi najpierw zold, potem renty (A3)

- **Klucz:** `NeedAidFromRents` (bool, **on**). Off = renty dziela cala reszte wplywow jak w 449d453.
- **Regula.** Po szczeblach danina -> W5b -> W6 zostaje niepokryta potrzeba U0 = potrzeba - garnek - pula W5b - pula W6. Czwarty szczebel: pula M1 = min(U0, reszta wplywow
  dnia po zwrocie i po wyplacie szczebla daniny). Poziom wyrownania liczony ta sama regula co W5 (garnek = danina + W5b + W6 + M1), gotowka = zold ludzi, ktorzy dzis
  SA w druzynach, ponad to, co rod juz dostal tej doby (`_naPaid`); reszta puli to miejsce w pulapie na krok werbunku (jak W5). Gotowka schodzi z reszty wplywow
  (`CrownIncome.Spent`) - renty dziela to, co zostanie. W 165 osobna pozycja `KDay.NeedAidRent` (obok doplaty W5, nie w niej - zamkniecie W5-1 linii W5 z 165 zostaje).
- **Kto -> komu:** `KingdomBudgetWallet` -> kiesa glowy rodu z potrzeba W5 (najpierw skarbiec, potem glowa; wyjatek - zloto wraca).
- **Liczby (decyzja):** bez nowego parametru - cel 60% limitu (`CrownWarAidFillTarget`) i krok 10% (`CrownWarAidRecruitStep`) te same. Wyzszego celu nie daje: rody z doplata i tak
  nie werbuja do celu (W5-4 43.4 -> 40.5% [P BD 7 nr 31]).
- **Kontrola calosci:** W5b regula 1 (`WarTaxDaily` -> `NeedAidMeasure`) czyta brak **sprzed** M1 (nowe pole `UncovPreRent`) - danina z ludu nie slabnie; W7 (`LvPlan`) dostaje poziom
  **po** M1; gotowka M1 jest gotowka W5: `_naCash` (bez zwrotu 70% nastepnej doby), `_naPaid` (`NeedAidCashOf` - dwor i sprzet), `_naBoost` (pulap 166). Rod gracza (W6b) - ta sama
  regula, sama gotowka. M2b rezerwa czyta brak sprzed M1.
- **Skutek [P MINIMUM `m_m1m2.py` / S]:** rok 2 do W5 ok. 7.0 tys. zl/d (Wyspy 3.5, Wolni Ludzie 2.3, Pentos 0.6); netto zold ok. +2.8 tys. zl/d = ok. +0.5 tys. ludzi.
  **[rec. 8.3]** +2.8 to [S] przy 0.4 zl pulapu na 1 zl przesuniety; recenzja 2 liczy sam przelew miedzy rodami - ok. 1.1 tys. zl/d w oknie Z.1 (reszta puli to renta tych
  samych rodow). Zadna liczba nie jest zmierzona - test mierzy R2-1s i R2-1d. Pula M1 bez calego garnka daniny i najwyzej skarbiec po szczeblu daniny **[rec. 8.5]**.
  Rok 1: Q2/Q3/Q4 4.8/8.4/12.0 tys. zl/d, **d93-120 0.9**. Z.1 d754-781 [P log Z.1]: renty w krolestwach z brakiem 11.4 tys. zl/d przy wplywach 27.1 tys., niepokryte 37.1 tys. zl/d.
- **Linia:** w "Doplata wedlug potrzeby (W5)" odcinek `| z reszty wplywow dnia (M1): ...` (pula, wyplacono, rodow, krolestw; niepokryte przed / po; z platnikiem / bez platnika)
  i w wierszu krolestwa `, z rent M1 X (pula Y)`; w 165 na krolestwo ` z rent M1 X`; w "Obieg" `gotowka korony na zold druzyn z reszty wplywow (M1)`.

### 3.2 M2a - rata wezwania tylko za dni wojny (A15-2, A3)

- **Klucz:** `CallToWarPaysForWar` (bool, **on**).
- **Regula.** Dla porozumienia (bez wpisow "dlug sprzed paczki"): gdy wezwany NIE jest w wojnie z celem (`called.IsAtWarWith(against)`) - dzis bez raty. Po 3 dobach laski od
  porozumienia liczymy doby bez wojny z rzedu; po **7** porozumienie konczy gra (`EndCallToWarAgreement`), reszta ceny skreslona sciezka wyrownania P3/W4 (`_ending`, "wpis dalej
  na liscie"), **bez** `MakePeaceAction`. Licznik - pamiec sesji (bez pola zapisu).
- **Dowod:** Straz -> Skagos d13-70: 150 879 zl przy 0 dobach wojny Skagos [P ROZP-RODY 5.2]. Gra konczy porozumienie tylko przy pokoju WZYWAJACEGO z celem, terminie albo koncu
  sojuszu (`AllianceCampaignBehavior.OnMakePeace` / `DailyTickClan` / `EndAlliance`) [P kod gry] - pokoj wezwanego z celem porozumienia nie konczy.
- **Liczby (decyzja):** 3 doby laski - wypowiedzenie przy porozumieniu idzie od razu (`StartCallToWarAgreement` -> `DeclareWarAction`), 3 doby to zapas na blokady ROT; 7 dob - dluzej
  niz przerwa pokoju fabularnego ROT (wznowienie w 2 doby, 111 z 111 [P CS T9]).
- **Kto -> komu:** nikt nikomu - rata zostaje w skarbcu wzywajacego (dalej zwrot, W5, M1, chleb, renty).
- **Linia:** w "Wezwania do wojny (168)" `| M2a za wojne: dob bez wojny X (porozumien N), zakonczone bez wojny Z (skreslono R)` i kontrolka `rata w dobie bez wojny 0`.
- **Komunikat gracza (EN):** "{ally} has not taken the field against {enemy}. {crown} stops paying for the war and the agreement is ended."

### 3.3 M2b - rata dopiero po wlasnych zolnierzach (A3)

- **Klucz:** `CallToWarAfterOwnWages` (bool, **on**).
- **Regula.** Rezerwa = wczorajszy zwrot zoldu nalezny wzywajacego (`KDay.RefundDue`, migawka przy `CrownIncome.End`) + wczorajszy garnek daniny W5 **[rec. 8.6]** + wczorajsza
  niepokryta potrzeba W5 SPRZED M1 (`UncovPre`) - wszystko, czego W5 wczoraj potrzebowalo z wplywow dnia (`NeedAidFromIncomeYesterday`).
  Rata, gdy reszta wplywow - rezerwa >= rata. Gdy reszta >= rata, ale po rezerwie nie starcza - **dzis bez raty, porozumienie trwa** (czesc dnia nie przechodzi na jutro;
  reszte ceny skresla gra na koncu porozumienia). Pierwsza doba po wczytaniu - rezerwa 0 (jak dzis).
- **Skutek [P MINIMUM / BUDZET, zbiezne]:** rok 2 wstrzymane 1.8-1.9 mln (5 tys. zl/d; Wyspy 1.39 mln) - z M1 idzie w druzyny Wysp (alarm 10: min 1.4 -> ok. 2.4 tys. [S]);
  rok 1 0.46 mln, d93-120 2.4 tys. zl/d.
- **Linia:** `| M2b po wlasnych zolnierzach: wstrzymane W zl (czesci N) u K koron (rezerwa: zwrot wczoraj + brak W5 wczoraj)`.

### 3.4 M2c - bez zerwan z braku wplywow (A15-2)

- **Klucz:** `CallToWarNoBreak` (bool, **on**).
- **Regula.** Gdy reszta wplywow < rata: zamiast zerwania i `MakePeaceAction` - dzisiejsza czesc przepada, porozumienie trwa (do terminu gry albo do konca z M2a).
  **[rec. 8.2]** Po 7 dobach wojny bez raty z rzedu (M2c przepadlo albo M2b wstrzymane) porozumienie konczy sie sciezka M2a - bez pokoju; wezwany moze wtedy sam
  zawrzec pokoj (gra zabrania pokoju wezwanego z celem, dopoki porozumienie istnieje - `DefaultKingdomDecisionPermissionModel` :45-53).
- **Dowod i ryzyko:** rok 1 6 zerwan z pokojem sojusznika (wojny jednodniowe, raport roczny nr 20) -> 0 [P BUDZET]. Portfel wezwan wzywajacego stoi dluzej na minusie; ponizej
  -100 000 gra nie pozwala mu wzywac NOWYCH sojusznikow (`DefaultAllianceModel` :380) [P] - zamierzone ("nie stac cie na raty - nie wzywasz kolejnych").
- **Linia:** `| M2c bez zerwan: przepadlo dzis X zl (czesci N)`; stara pozycja "zerwane z braku wplywow" przy kluczu = 0.

### 3.5 M3 - chleb dla glodnych miast (A4)

- **Klucz:** `BreadForThePoor` (bool, **on**). Off = jak 449d453 (stan osad na chlebie znika).
- **Wejscie (jedna definicja glodu):** osada (miasto albo podzamcze z kiesa czynna) z glodem z braku pieniedzy wedlug `Hunger.Of` / `TownFolk.MoneyHungry` (Afford jedzenia na jutro
  < 0.95 przez 7 dob z rzedu, kampania >= 120 dob; licznik glodu biegnie od startu - osada glodna od tygodnia wchodzi juz w dobie 120 **[rec. 8.4]**). **Wyjscie (histereza):** 7 kotwic z rzedu, w ktorych lud sam ma na dwa dni jedzenia (luka 0). Stan - pamiec sesji.
- **Luka (w kotwicy, `TownFolk.Daily`):** miasto - suma klas W/R/P: max(0, 2 x potrzeba dzienna klasy x udzial jedzenia (0.80 / 0.70 / 0.45) - kiesa klasy); podzamcze -
  max(0, 2 x potrzeba x udzial jedzenia - (kiesa + kredyt dworu)).
- **Platnik (krok korony, po zwrocie zoldu, przed rentami):** (1) korona krolestwa osady z reszty wplywow dnia (`CrownIncome.LeftFor`, `Spent`) - nigdy z zapasu skarbca, nigdy
  z kredytu; (2) zastepczo pan osady (glowa rodu wlasciciela) - tylko ponad rezerwe wojny R budzetu 166 i tylko z czesci tempa skrzyni (G - R) / `WarChestDays`, ktorej
  nie bierze zold: w wojnie 0.2 (ze skrzyni sprzetu tej doby), w pokoju 0.68 **[rec. 8.4]**; dluznik, rod w zajeciu i najemnik nie placa; **nigdy gracz** (osada gracza -
  tylko korona); (3) gdy nikt - luka zostaje (licznik "nie stac nikogo").
- **Liczby (decyzja):** 2 dni jedzenia - chleb wplywa po kotwicy doby, wiec trafia do kotwicy NASTEPNEJ doby; z celem 1 dnia stan skakalby co drugi dzien (pelny / glodny); 2 dni =
  1 dzien zapasu na opoznienie, w rownowadze koszt = dzienny brak (potrzeba - zarobek), jednorazowo +1 dzien jedzenia na osade (zostaje w kiesie ludu). **[rec. 8.4]** "Tylko jedzenie" bylo
  nieprawdziwe: kiesa klasy jest jedna i lud kupuje z niej tez reszte koszyka - w rownowadze chleb pokrywa caly dzienny brak koszyka. Pan w sumie nie szybciej niz
  skrzynia - bez tego miasto glodne latami zjadloby wojsko pana (skrzynia w wojnie: 0.8 (G-R)/45 zold + 0.2 sprzet; chleb pana bierze czesc sprzetu).
- **Kto -> komu:** skarbiec (albo glowa rodu pana) -> kiesy ludu osady -> kupcy za jedzenie (kasa) -> robocizna i zawor. Zamkniete; przelew miedzy posiadaczami ksiegi pieniadza.
- **Kontrola calosci:** kontrola zamknieta kies (`_ctlIn` - linia "Kiesa ludu", r = 0); nowy wydatek w 165 (`KDay.Bread`) i "Obieg"; R14 / E5/C2 gasna w tych osadach - zamierzone;
  P1 danina i W7 biora tylko ponad zapas dni (10/21/30) - 2 dni jedzenia ich nie dotykaja; kredyt dworu podzamcza liczony przed luka - bez drugiego karmienia; gracz-krol - placi
  skarbiec krolestwa.
- **Skutek [S z P MINIMUM `m_chleb.py`]:** **d93-120 = 0** (okno do doby 120 = wiek kampanii 119; wejscie od wieku 120 - **[rec. 8.4]**); rok 1 Q3-Q4 do ok. 3-6 tys. zl/d; rok 2 Q5-Q8 6-15, d701-728 ok. 21 tys. zl/d (gorna
  granica - MINIMUM liczyl kazda luke, tu tylko osady glodne 7 dob) + jednorazowo ok. 1 dzien jedzenia na wchodzaca osade. Miast z jedzeniem < 95%: 4-5 -> ok. 0-2 **[rec. 8.7: nieosiagalne - inna definicja niz wejscie; prog na glodzie E5/C2]**;
  chleb swiata (P1-6) 88.8-89.7 -> ok. 94-95%.
- **Linia:** nowa `Chleb biedoty (M3): dzien N | ...` (osad na chlebie, weszlo / wyszlo, luka, korona / pan / nie stac nikogo, najnizsze G/R placacego pana, kontrolki: pan ponizej R 0,
  kiesa gracza 0, top 5 osad, na krolestwo); w 165 na krolestwo ` chleb M3 X`; w "Obieg" `chleb biedoty (M3) skarbce -> kiesy ludu A, glowy rodow -> kiesy ludu B`.
- **Komunikat gracza (EN, raz na 7 dob):** pan-gracz: "The labourers of your {town} could not afford bread; {payer} paid {x} denars for their food this week."; krol-gracz:
  "This week the crown paid {a} denars of wages to field companies before rents and {b} denars for bread in hungry towns."

### 3.6 B4 - zalogi w pokoju na 70% zalogi wojennej (A15-1)

- **Klucz:** istniejacy `GarrisonPeaceShare` **0.50 -> 0.70** (0.50 = 449d453). Limit 80% pulapu (`GarrisonMaxShareOfBudgetPeace`) bez zmian. W `Armoury.json` Jeffa klucza nie ma [P] - domyslna zadziala.
- **Kto placi:** pulap pokoju rodu (0.28 D + 0.4 x skrzynia, Z.1); korona 0.
- **Skutek [S BUDZET]:** z Z.1 zalogi w pokoju 14.6 -> ok. 20.4 tys., swiat 64.3 -> ok. 70.1 tys. (prog N4 69 020); koszt ok. 18 tys. zl/d z luzu pulapu pokoju (56 tys. zl/d);
  0.60 daje tylko 67.2 tys. Rok 1: na starcie kampanii mniej przycinania zalog krolestw w pokoju (3 krolestwa).
- **Ryzyko:** u rodow na pulapie pokoju zaloga (placona pierwsza) zjada druzyny Z.1 - prog R2-B4-2. "Nie wiem", czy w pokoju wiaze cel, czy tempo werbunku zalog gry - stad dopisek.
- **Linia:** w "Budzet rodow (166)" `| zalogi w pokoju (B4): udzial X, zalog z celem N, na celu (zold >= 95% celu) M, ponizej celu L; zold W wobec celu T; rodow z celem przycietym do 80% pulapu K`.

### 3.7 L1 i L2 - same linie (napis 165 z PZ-7; A15-5) [rec. 8.8]

- **L1:** linia 165: `raty reparacji X (przyjete Y; L1 rozbicie: okupy krolow (178) Z, reparacje R)` - stary poczatek napisu zostaje (czytniki), rozbicie dopisane
  (dzis ok. 90% to okupy [P ROZP-RODY 5.3]); przy dlugach `(L1: w tym okupy krolow (178) - reszta X zl, nowe dzis N na M)`. Bez klucza. To nie jest A15-4 **[rec. 8.8]**.
- **L2:** `MoneyLedger` - w linii "Pieniadz swiata (rody - przyczyny)" kwoty PRZENIESIONE (zmiana zlota swiata w rozliczeniu rodu: czesc zoldu i reszta) obok naliczonych. Bez klucza.

### 3.8 M4 - danina w dlugim pokoju (A5) - bez kodu, pytanie do Jeffa

Zgoda Jeffa 13.10 dotyczy daniny "na czas wojny" (STAN-PRAC "DO JEFFA" 09.10). Wariant na "tak" (s2, s3): w krolestwie w pokoju >= 91 dob korona bierze z oszczednosci ludu
ponad zapas dni **tylko tyle, ile wydala tej doby na wlasne potrzeby** (chleb M3 jej osad), najwyzej 0.5%/d - nie stala stawka do rent (PRZEPLYW: stala stawka przenioslaby ok.
100 tys. zl/d z kies ludu do skrzyn panow, gdzie dalej leza). Pytanie z liczbami, gdy bieg pokaze lokate krolestw w pokoju > 8 mln w d728 albo przyrost > 20 tys. zl/d. **Ten warunek juz jest spelniony:** log Z.1 (d781) - lokata W krolestw
w pokoju 19.55 mln, w wojnie 7.45 mln [P `--grupa rok2` na logu Z.1] - pytanie idzie do Jeffa z raportem tej pracy (kodu M4 dalej nie ma).

---

## 4. Kolejnosc wdrazania i wycofanie

Kod jednym DLL (jeden test roku 2 i jeden 120 dob), ale kazda regula ma wlasny klucz. Przy **NIE w R2-T2-1** (rok 1 gorszy): wylaczac po kolei **M2c -> M2b -> M2a -> M1**
(B4 i M3 w d1-120 prawie nie dzialaja: M3 = 0 z definicji, B4 tylko krolestwa w pokoju). Przy NIE w progu druzyn pokoju (R2-B4-2) - `GarrisonPeaceShare` 0.60, potem 0.50.

---

## 5. Progi (grupa `rok2` w `tools/sprawdz_logi.py`) i plan testu [rec. 8.9 - po recenzji]

**T1 - rok 2:** zapis `autotest-rok2-d780-fin24` + 60 dob (doby 781-840) na tej galezi. **Baza T1 = osobny bieg na 449d453** (fin26) na tym samym zapisie + 60 dob
(`--baza-doby`). Bieg 14.10 04:50 (`Armoury-2026-10-14_04-50-35.log`) to fin28 = 62eb9f4 (STAN-PRAC 11:51 / 12:08), NIE 449d453 - nie jest baza. Bez bazy R2-1, R2-4d,
R2-5b, R2-6, R2-7 narzedzie pokazuje tylko jako INFO (Z.1 d754-781 to inne doby i inna mapa wojen; fin28 na tym zapisie bez zadnej reguly: R2-4d 90.0%, R2-5b 11 256,
R2-6 13.75 mln).
**T2 - rok 1:** nowa kampania 120 dob. Progi ze sredniej pieciu biegow 120 dob z 14.10 bez pakietu (W52, W53, fin27, fin28, fin29) minus 2 odchylenia - rozrzut miedzy
biegami jest wiekszy niz skutek regul w d93-120; W53 (`--baza-doby`) tylko do porownania (INFO).
**Logi z CSV:** R2-5 czyta `balans-krolestw.csv` z `<katalog logu>\Logs\<znacznik>\` - podawac log z `Modules\Armoury\` (tam jest katalog `Logs`) albo skopiowac katalog
`Logs\<znacznik>` obok kopii na D:.

| prog | co | T1 | T2 |
|---|---|---|---|
| R2-0 | odcinki nowych regul w liniach (W5 "M1", 168 "M2a/M2b/M2c", "Chleb biedoty (M3)", 166 "B4"), bledy `ERROR in` M1-M3 = 0 | TAK | TAK |
| R2-0b | linia kontrolna M3 - numery grup wyrazenia (rec. 8.1) | TAK | TAK |
| R2-1 | (A3) niepokryte po wszystkich zrodlach (po M1), sr. 28 dob - ksiegowe, M1 zmniejsza je z samej budowy | INFO | INFO |
| R2-1s | (A3) druzyny rodow z potrzeba W5: ludzie i % limitu, sr. 28 dob (wobec bazy) | INFO | INFO |
| R2-1d | skad gotowka M1: renty rodow z potrzeba / bez potrzeby / udzialy bez warunku | INFO | INFO |
| R2-1b | niepokryte "z platnikiem" (kontrola kodu) i kontrolka gotowki M1 | 0 / 0 | 0 / 0 |
| R2-1c | niepokryte "bez platnika" wedlug krolestw | INFO | INFO |
| R2-2 | (A15-2) rata zaplacona w dobie bez wojny wezwanego; obok kwota wstrzymana za doby bez wojny | 0 | 0 |
| R2-3 | zerwania z braku wplywow i pokoj sojusznika z 168 | 0 / 0 | 0 / 0 |
| R2-3b | rata ponad (reszta - rezerwa) - kontrola M2b | 0 | 0 |
| R2-3c | porozumienia zakonczone po 7 dobach wojny bez raty (rec. 8.2) | INFO (KRONIKA WOJEN) | INFO |
| R2-4 | (A4) chleb: pan placacy ponizej R, kiesa gracza, kontrola kies r | 0 / 0 / 0 | 0 / 0 / 0 |
| R2-4b | chleb: koszt sr. 28 dob (caly dzienny brak koszyka); pan w wojnie / w pokoju | <= 30 tys. zl/d | 0 (okno do wieku 119) |
| R2-4c | glod z braku pieniedzy E5/C2 (wejscie M3; miasta + podzamcza), sr. 28 dob | <= 1.0 (bez reguly 3.15-3.85) | INFO |
| R2-4c2 | miast ze stac jedzenie < 95% (ktorakolwiek klasa) | INFO | INFO |
| R2-4d | chleb swiata (pelny koszyk W/R/P, swiat), sr. 28 dob | >= baza + 2 pkt (bez bazy INFO) | >= 96.9% |
| R2-4e | chleb na krolestwo (korona / pan / nie stac nikogo) | INFO | INFO |
| R2-5 | (A15-1, B4) zalogi swiata ostatnia doba (balans-krolestw.csv) | >= 69 020; DECYZJA, gdy zold / cel zalog nie rosnie w oknie | >= 84 096 |
| R2-5b | ludzie w partiach rodow w pokoju (166 Z.1, sr. 28 dob) | >= 0.9 x baza (bez bazy INFO) | INFO |
| R2-6 / R2-6b | (A5) lokata W krolestw w wojnie / w pokoju | INFO (A5 otwarte) | INFO |
| R2-7 | Zelazne Wyspy - wojsko rodow (Skarbce), minimum okna | INFO | INFO |
| R2-T2-1 | rok 1 bez pogorszenia: miara W5-7 d93-120 | - | >= 96.8 tys. (sr. 105.2, odch. 4.2) |
| R2-T2-2 | danina P1 d91-120 | - | >= 131.1 tys. zl/d (sr. 156.6, odch. 12.8) |
| R2-8 | Pieniadz swiata: zrodla bez platnika (L2 / ksiega) | INFO | INFO |

Polecenia (sesja glowna):
```
python -I tools/sprawdz_logi.py --grupa rok2 --start-kampanii 108836 --do-doby 840 --baza-doby <log bazy 449d453 na zapisie d780> <log T1>
python -I tools/sprawdz_logi.py --grupa rok2 --do-doby 120 [--baza-doby <Modules\Armoury\Armoury-2026-10-14_03-24-46.log>] <log T2>
python -I tools/sprawdz_logi.py --grupa w6b --start-kampanii 108836 --do-doby 840 <log T1>     (W6b-1 liczy teraz M1 - kolumna doplata_m1)
```
Wycofanie bez zmian (rozdz. 4): NIE w R2-T2-1 - wylaczac po kolei M2c, M2b, M2a, M1. Alarm 5 po rozbiciu (s2): `a5_saldo56.py` na `budzet-rodow.csv` T1 - "G<R" >= -30 tys. zl/d, "na pulapie" INFO.

---

## 6. Armoury.json Jeffa (tylko odczyt)

`Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json` (13.10 02:10, 336 linii) [P grep]: nie ma `NeedAidFromRents`,
`CallToWarPaysForWar`, `CallToWarAfterOwnWages`, `CallToWarNoBreak`, `BreadForThePoor` (nowe) ani `GarrisonPeaceShare` (istniejacy) - wszystkie domyslne z kodu zadzialaja.
Jesli Jeff otworzy MCM i zapisze ustawienia, MCM wpisze aktualne wartosci - od tej chwili zmiana domyslnej w kodzie juz nie dziala (CLAUDE.md 7).

---

## 7. Czego nie wiem (+ linia diagnozy)

1. Ile krolestw zostanie w wojnie w roku 2 po /4.33 - od tego zalezy wiekszosc alarmow. Linia: Skarbce (WOJNA), W5 na krolestwo.
2. Czy w pokoju zalogi trzyma cel 50/70%, czy tempo werbunku zalog gry. Linia: 166 "zalogi w pokoju (B4)" - na celu / ponizej celu.
3. Jak koszt chleba rozlozy sie miedzy korony i panow i czy miasta-panstwa w pokoju (Braavos: 1.1 tys. zl/d wplywow korony) beda mialy platnika. Linia M3 "na krolestwo".
4. Czy w miastach z chlebem starczy jedzenia na polkach (Braavos 3.5, Reach 4.9 dnia w Q8) - wiecej kupujacych. Linia GL2 "jedzenie na polkach".
5. Sprawnosc M1 (ok. +0.4 zl zoldu na 1 zl przesuniety) [S]. Linia W5 "z rent M1" + `budzet-rodow.csv` kolumna doplata_w5.
6. Czy brak raty (M2a/M2b/M2c) zmienia decyzje AI o pokoju przez inna droge niz portfel (np. relacja) - w kodzie gry i Diplomacy nie znalazlem; linia: KRONIKA WOJEN dla par z wstrzymana rata.
   **[rec. 8.2]** Inna droga jest: samo porozumienie - gra zabrania pokoju wezwanego z celem, dopoki trwa. Stad zwolnienie po 7 dobach bez raty. Czy zmeczenie wojna
   Diplomacy omija `IsPeaceDecisionAllowedBetweenKingdoms` - nie wiem; linia: KRONIKA WOJEN dla par z "wstrzymane" / "przepadlo" / "zwolnione" w linii 168.

---

## 8. Recenzja i poprawki (14.10, dwie recenzje: "obieg pieniadza i kod" = R1, "liczby i skutek" = R2)

Autor poprawek: sesja c36b73ca (krok autora po recenzjach). Kazdy zarzut sprawdzony w kodzie (`zb\pr2`, dekompilacja gry `dec32\cs_full`) albo w logu. Kod: galaz
`w-toku/pieniadze-rok2`, build kod 0, gen_mcm 1414, py_compile kopii narzedzia; narzedzie sprawdzone na linii kontrolnej M3, syntetycznym logu nowych formatow
i logach Z.1 / fin28 d780 / fin29 (bez wyjatku). Nic nie wgrane, gra nie uruchamiana. Wpis CHANGELOG "pieniadze roku 2, RECENZJA".

### 8.0 Dla Jeffa - prosto

- Narzedzie progow wywracalo sie na pierwszym logu z chlebem - poprawione i sprawdzane samo na linii kontrolnej.
- Sojusznik, ktoremu korona nie placi, nie moze sam zawrzec pokoju, dopoki trwa porozumienie (tak dziala gra). Dlatego po tygodniu bez zaplaty porozumienie sie
  konczy - bez wymuszonego pokoju, ale sojusznik jest wolny.
- Pan glodnego miasta placi chleb tylko z tej czesci swoich oszczednosci, ktora i tak szlaby na sprzet (w wojnie) albo lezala (w pokoju) - jego wojsko na tym nie traci.
- Chleb w praktyce pokrywa caly dzienny brak mieszczan (jedzenie i reszte), nie samo jedzenie - opisy poprawione.
- Brak na zold w roku 2 (A3) zostaje otwarty: "najpierw zold, potem renty" w duzej czesci przesuwa pieniadze miedzy tymi samymi rodami; ile ludzi z tego bedzie - nie wiem.
  Test pokaze to dwiema nowymi liniami. Rody bez ziemi (np. 14 rodow Smoczej Skaly) sa poza ta regula.
- Test roku 2 potrzebuje osobnego biegu bazowego na starej wersji z tego samego zapisu; bez niego wiekszosc liczb to tylko informacja.

### 8.1 Przyjete i poprawione

| # | zarzut | dowod (sprawdzony) | poprawka |
|---|---|---|---|
| 8.1 | R1-1 = R2-1: `--grupa rok2` - IndexError na linii M3 | `R2_M3` ma 18 grup, kod czytal `group(19)`; G/R, "nie stac nikogo", "ponizej R" przesuniete o jeden (policzone na wyrazeniu) | grupy 14 / 15 / 16 / 17 / 18; linia kontrolna wedlug `BreadLine` sprawdzana w narzedziu (R2-0b, `r2_selftest`) |
| 8.2 | R1-2: M2b / M2c trzymaja niezaplaconego sojusznika w wojnie 182 doby | `DefaultKingdomDecisionPermissionModel.cs:45-53` (pokoj zabroniony przy `IsAtWarByCallToWarAgreement`), `MakePeaceKingdomDecision.cs:163`, `KingdomDecisionProposalBehavior.cs:270-277`; porozumienie konczy tylko pokoj WZYWAJACEGO, termin albo koniec sojuszu (`AllianceCampaignBehavior`) | licznik dob wojny bez raty z rzedu (M2b wstrzymane, M2c przepadlo; zaplata zeruje); po 7 - `Release` sciezka M2a (`EndCallToWarAgreement`, reszta ceny zdjeta, bez `MakePeaceAction`); linia 168 "zwolnione po 7 dobach wojny bez raty"; komunikat gracza (EN); R2-3c |
| 8.3 | R2-3: M1 to w duzej czesci renta tych samych rodow; R2-1 mierzy etykiete | kandydaci W5/M1 = rody z wierszem rent (`CrownRents.cs`); pula = min(brak, reszta) - R2-1 spada z budowy | R2-1 = INFO; nowe R2-1s (ludzie / limit rodow z potrzeba wobec bazy) i R2-1d z nowego odcinka linii W5 "skad (bez M1 poszloby w renty)" (`RentAidSplit` - wagi lenn tej doby, tylko pomiar); A3 otwarte (rozdz. 0, 3.1) |
| 8.4 | R1-5 = R2-7: chleb pana ponad skrzynie; R1-7 = R2-13: "tylko jedzenie"; R1-8: wejscie od wieku 120 | skrzynia w wojnie 0.8 + 0.2 (G-R)/45 (`ClanBudget.cs` Ceiling, Gear); `Anchor` kupuje reszte koszyka z tej samej kiesy; `HungerDays` od startu, prog wieku tylko w `MoneyHungry` | pan placi z czesci tempa skrzyni bez zoldu: wojna 1 - WarChestToWages = 0.2 i `ClanBudget` odejmuje zaplacony chleb od skrzyni sprzetu tej doby, pokoj 1 - 0.4 x 0.8 = 0.68 (w sumie <= (G-R)/45); linia M3 "pan w wojnie / w pokoju"; opisy MCM i projektu poprawione |
| 8.5 | R1-9 = R2-8: M1 - rachunek dostepnych pieniedzy | `RentAid.cs`: `LeftFor - aidPaid` (wyplacona czesc garnka juz zdjeta ze skarbca), `pot1` liczy niewyplacona reszte garnka drugi raz | pula = min(reszta wplywow - caly garnek, skarbiec - niewyplacona reszta garnka) |
| 8.6 | R2-6: rezerwa M2b bez garnka daniny | garnek = min(danina, reszta wplywow) (`NeedAid.cs`); rata 168 biegnie przed W5 | rezerwa = zwrot nalezny + garnek + brak przed M1 (wczoraj) - `NeedAidFromIncomeYesterday` |
| 8.7 | R1-3: falszywe NIE w R2-1b; R2-5: prog R2-4c nieosiagalny; R1-6: falszywe NIE w W6b-1 | `LeftFor = 0` - `RentAll` zostawal false (log Z.1: 24 747 zl); R2-4c liczy miasto przy KTOREJKOLWIEK klasie < 0.95 (`TownFolk.Live.cs` :907), wejscie M3 - srednia i 7 dob; gotowka M1 w `doplata_w5` | `row.RentAll = RentAidOn && !m1`; R2-4c = glod E5/C2 (miasta + podzamcza) <= 1.0 w T1 (bez reguly: Z.1 3.85, fin28 d813-840 3.15 - zmierzone), stara miara R2-4c2 INFO; kolumna `doplata_m1` i W6b-1 = W5 + W5b + W6 + M1 |
| 8.8 | R1-10a-d, R2-12: drobne | `ChestOf` bez dluznikow i zajec; `CrownIncome.Reset` bez `_dNewKing`; napis P1 "pozostale"; napis L1 inny niz w projekcie; L1 opisane jako A15-4 | `ChestOf` wyklucza dluznika, zajecie i najemnika; Reset zeruje L1; napis P1 z M1 i chlebem; 3.7 poprawione; A15-3 i A15-4 bez zmian z decyzji PZ-7 (ROT wznawia 111 z 111 wojen fabularnych w 2 doby; najemnicy krajow w pokoju z zapasu 0.1-0.6 tys. zl/d i gasnie), A5 otwarte (M4) |
| 8.9 | R1-4 = R2-4: T1 bez waznej bazy; R2-2: progi roku 1 ponizej szumu; R2-10: R2-5 zawsze DECYZJA; R2-11: R2-6 i R2-7 nie mierza reguly | 04-50-35 = fin28 (STAN-PRAC); piec biegow 120 dob przeliczone narzedziem (W5-7 103.5 / 111.9 / 106.6 / 102.1 / 102.0 tys., danina 155.2 / 169.4 / 143.7 / 170.0 / 144.8 tys., zalogi d120 z CSV w `Modules\Armoury\Logs` - zgodne z recenzja) | rozdz. 5 od nowa: baza T1 = bieg 449d453; bez bazy INFO; T2 = srednia - 2 odchylenia (R2-T2-1 >= 96.8 tys., R2-T2-2 >= 131.1 tys., R2-5 >= 84 096, R2-4d >= 96.9%); R2-5 DECYZJA tylko przy stosunku zold / cel, ktory nie rosnie; R2-6, R2-7 INFO; M2a: kwota "rata wstrzymana za doby bez wojny" |

### 8.2 Odrzucone (w calosci albo wariant) - z uzasadnieniem

1. **R1-7 wariant "w kotwicy odjac od kiesy niewydany chleb".** Pieniadz w kiesie klasy jest jeden. Osobna "kiesa chleba" to nowy stan osady (pamiec albo zapis) i druga
   regula na te sama kiese (zasada 0). Przyjete zamiast tego: prawdziwy opis (chleb pokrywa caly dzienny brak koszyka); koszt pilnuje R2-4b <= 30 tys. zl/d.
2. **R1-8 wariant "liczyc 7 dob dopiero od wieku 120".** Wejscie M3 to ta sama funkcja, ktora karze zadowolenie (`MoneyHungry`, E5/C2). Drugi licznik = druga definicja
   tego samego glodu. Kara i chleb zaczynaja sie w tej samej chwili; poprawione opisy.
3. **R2-3 czesciowo: "M1 w ok. 90% bez skutku, uczciwie <= 1.1 tys. zl/d".** Liczba 1.1 tys. to tylko przelew miedzy rodami. W kodzie zmiana etykiety u tego samego rodu
   tez cos zmienia: renta wchodzi do D (`CrownRents` -> `ClanIncomeBook.NoteInflow` KRent) i podnosi pulap w wojnie o 0.6 x renta (`Ceiling`: 0.6 D x f + skrzynia), a przy
   G < R nie ma skrzyni; gotowka M1 podnosi pulap tej doby 1:1 (`_naBoost` -> `b.Aid`), ale zmniejsza jutrzejszy zwrot 70% (`_naCash`). Ani "+2.8 tys." autora, ani
   "<= 1.1 tys." recenzji nie jest zmierzone - **nie wiem**; linia diagnozy: R2-1s i R2-1d wobec bazy. Wniosek recenzji (A3 otwarte, R2-1 nie jest miara skutku) przyjety.
4. **R2-3 "100% rodow nie jest spelnione" - nie poprawiane w tym kroku.** Rody bez lenna sa poza W5 od poczatku W5 (kandydaci = wiersze rent 180); M1 jest szczeblem W5.
   Objecie ich to nowa regula (W5 dla rodow bez ziemi), nie poprawka recenzji - zapisane jako otwarte przy A3 (Smocza Skala 14 rodow, 1 790 ludzi, 12.8 tys. zl/d zoldu
   [P recenzja 2, `an/fief.py`, bieg dwuletni d701-728]).
5. **R2-9 czesciowo: "kontrolki zawsze 0 z samej budowy".** Zostaja jako kontrola kodu (R2-1b pokazalby blad z 8.7, R2-2 i R2-3 pilnuja kluczy). Do skutku sa R2-1s,
   R2-1d, R2-3c, R2-4c i kwota M2a.
6. **R1-5 / R2-7 wariant "najwyzej 0.2 x (G-R)/45 w wojnie" sam.** 0.2 PONAD skrzynie to dalej 1.2 x tempo. Wybrane: 0.2 ze skrzyni sprzetu (odjete w budzecie doby), 0.68 w pokoju.

### 8.3 Ryzyka po poprawkach

- Zwolnienie po tygodniu bez raty: wezwany moze zawrzec pokoj - mniej wojny niz w wersji przed recenzja (sojusznik przymusowo w wojnie do terminu), wiecej niz przed
  paczka (zerwanie + natychmiastowy pokoj). Rezerwa M2b z garnkiem wstrzymuje wiecej rat, wiec zwolnien moze byc wiecej. KRONIKA WOJEN dla par z linii "zwolnione".
- M3: pan placi mniej (0.2 / 0.68 tempa) - wiecej "nie stac nikogo" (R2-4e na krolestwo); w wojnie skrzynia sprzetu pana z glodnym miastem mniejsza o chleb.
- Koszt chleba wyzszy niz szacunek MINIMUM (caly dzienny brak koszyka, nie samo jedzenie) - prog R2-4b <= 30 tys. zl/d.
- `doplata_m1` - nowa ostatnia kolumna `budzet-rodow.csv` (czytniki po nazwach).
- Klucze wylaczone = 449d453 (zwolnienie tylko przy M2b / M2c; chleb pana i skrzynia sprzetu tylko przy chlebie > 0; RentAll tylko przy M1).
