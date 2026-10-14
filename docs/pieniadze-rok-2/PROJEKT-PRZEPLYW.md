# PROJEKT: PRZEPLYW - lezace pieniadze plyna tam, gdzie brakuje (A3, A4, A5, A15) - 14.10.2026

Kat: **PRZEPLYW** (jeden z trzech projektow etapu pieniadze-rok-2, obok BUDZET i MINIMUM). Tylko projekt: bez kodu, bez commita, gra nieuruchamiana,
nic nie wgrane. Podstawa: `ROZPOZNANIE-RODY.md` i `ROZPOZNANIE-LUD.md` (14.10), kod drzewa `zb\pr2` (galaz `w-toku/pieniadze-rok2` = 449d453),
wlasne przeliczenia na biegu dwuletnim (rozdz. 11).
Znaczniki: **[P]** pomiar z logu / tabel albo odczyt kodu, **[S]** szacunek. Kwoty: tys. zl na dobe (rok = 364 doby), kwartaly jak w rozpoznaniach
(Q1-Q4 rok 1, Q5-Q8 rok 2, zima od d460). "Niepokryte" = linia W5 (potrzeba do 60% limitu druzyn minus garnek, W5b i W6).
Decyzja Jeffa 4.20 ("zostawic jak jest" - niebezpieczenstwo okolicy) - projekt jej nie dotyka. Fabula ROT (wojny i pokoje fabularne) - nie ruszana.
Pomysly odlozone (F1-F4: kasy miast z dlugami, pozyczka miejska, karta miejska, sakiewka turniejowa) - nie uzyte.

---

## 0. Dla Jeffa - prostym jezykiem

1. W swiecie leza pieniadze, ktore nic nie robia: ok. 20 mln oszczednosci mieszczan w krolestwach w pokoju i ok. 18 mln w skrzyniach panow tych
   krolestw ponad ich zapas wojenny (do tego 5 mln u panow Reach). W tym samym czasie Zelazne Wyspy i Wolni Ludzie nie maja na zold, 7-8 wielkich
   miast nie stac na chleb, a krolestwa w pokoju rozpuszczaja zalogi zamkow do polowy.
2. Proponuje jedna zasade: **kazda potrzeba najpierw dostaje pieniadze, ktore leza bezczynnie najblizej niej; renty dla panow ida dopiero z tego,
   co zostanie.** Zadnego nowego zrodla zlota - tylko istniejace drogi: danina, place za roboty, zold, renty.
3. **Glodne miasto:** najpierw doklada jego pan - z tej czesci skrzyni ponad zapas wojenny, ktorej i tak nie wydaje - potem inni panowie
   krolestwa, potem lud krolestwa przez korone, na koncu kupcy miasta i korona. Wyrobnicy dostaja place za roboty publiczne i kupuja chleb.
   W drugim roku bylo to 11-32 tys. zl dziennie (w pierwszym 6-12) i panowie pokryliby cala luke.
4. **Zamki w pokoju:** korona trzyma zalogi na 80% stanu wojennego (dzis pan schodzi do 50%), a placi za to mala danina z oszczednosci mieszczan -
   tylko tyle, ile trzeba (ok. jedna czesc na 600 dziennie). Zalog bedzie ok. 75 tys. zamiast 58 tys., a oszczednosci w krolestwach w pokoju
   przestana rosnac.
5. **Korona, ktorej panowie nie maja na zold,** najpierw placi im - dopiero potem rozdaje renty, placi sojusznikom i daje dar Dothrakom.
6. **Sojusznikowi korona placi tylko za dni, w ktore on naprawde walczy** - Skagos nie wezmie juz 150 tys. zl za wojne, na ktora nie wyszedl.
7. **Czego to NIE naprawi:** Wolni Ludzie i Zelazne Wyspy sa biedni do dna - nie maja ani oszczednosci ludu, ani skrzyn panow, a zadna istniejaca
   droga pieniadza nie przechodzi do nich z innych krolestw. Ich brak zmaleje z ok. 45 do ok. 35 tys. zl dziennie (Wyspy o jedna trzecia, Wolni
   Ludzie o jedna dziesiata); reszta to decyzja kierunku, opisana w rozdz. 8 - nie udaje, ze jest zalatwiona.
8. W pierwszym roku prawie nic sie nie zmienia: zalogi na poczatku kampanii zostaja wieksze, Oldtown od polowy roku dostaje na chleb od panow Reach,
   Skagos nie bierze pieniedzy za nic.

---

## 1. Zasada przeplywu - jedna regula na trzy potrzeby

**Potrzeba placi najblizszy, kto ma pieniadz bez zadania; renty 180 sa zawsze ostatnie.** "Bez zadania" = ponad zapas, ktorego zadna regula
budzetu dzis nie wydaje [P kod]:
- oszczednosci ludu ponad zapas dni (W 10 / R 21 / P 30 / podzamcze 10 - ta sama definicja co danina P1, `KingdomTreasury.WarAid.cs:14`);
- **wolna czesc skrzyni pana**: skrzynia = (G - R) / WarChestDays (45); w wojnie budzet bierze z niej WarChestToWages = 0.8 na zold, w pokoju
  Z.1 bierze PeaceChestShare x 0.8 = 0.32 (`ClanBudget.Ceiling`, `Settings.cs:1055-1061`). Wolna czesc: **wojna 0.20, pokoj 0.68** skrzyni dziennie
  - liczby wynikaja z istniejacych kluczy, nie sa nowe;
- skarbiec korony ponad zapas wojny (W5b, juz w kodzie);
- reszta wplywow dnia korony po zwrocie zoldu (dzis idzie w renty 180).

| potrzeba | 1. | 2. | 3. | 4. | 5. | ostatnie |
|---|---|---|---|---|---|---|
| **zold druzyn w wojnie (W5, A3)** - bez zmian 1-3 | danina P1 (s) | skarbiec ponad zapas wojny (W5b) | tarczowe (W6) | **nowe: reszta wplywow dnia (PZ-3)** | - | wezwania 168, dar Wolnych Miast, renty |
| **zaloga w pokoju do 80% (A15, PZ-2)** | **danina pokoju (PZ-1)** | skarbiec ponad zapas wojny | wolna czesc skrzyn panow krolestwa | reszta wplywow dnia | - | renty |
| **chleb glodnego miasta (A4, PZ-6)** | wolna czesc skrzyni pana miasta | wolna czesc skrzyn innych panow krolestwa | danina krolestwa (wojna: pobrana; pokoj: PZ-1) | zawor kasy miasta | reszta wplywow dnia | renty |

Dlaczego taka kolejnosc: zold druzyn i zaloga chronia krolestwo - placi lud (jak "murage", sredniowieczna oplata mieszczan na mury i straz; danina
P1 juz tak dziala w wojnie). Chleb glodnego miasta to sprawa pana - to jego miasto, jego podatki i lojalnosc; w wojnie danina ma juz zadanie (renty
-> zold), a wolna czesc skrzyni nie ma zadnego, wiec chleb z niej nie zabiera wojska.

Jedno miejsce wydawania: krok korony po zwrocie zoldu (`CrownIncome`: rata 186 -> dary 182 -> raty -> kontrakty 185 -> zwrot -> **[potrzeby: W5,
zalogi, chleb]** -> W5b -> W6 -> **wezwania 168 i dar Wolnych Miast** -> renty 180). Chleb ze skrzyn panow i z zaworu idzie poza skarbcem (glowa ->
kiesa wyrobnikow, kasa miasta -> kiesa wyrobnikow).

---

## 2. Reguly

### PZ-1. Danina pokoju na potrzeby krolestwa (A5, A15)

- **Co:** w pokoju danina z oszczednosci (P1) nie spada do zera, tylko idzie za **potrzeba pokoju** krolestwa: dzisiejsza doplata do zalog (PZ-2)
  + chleb, ktorego nie pokryly skrzynie panow (PZ-6). Stawka dnia krolestwa = min(**0.5%**, potrzeba z wczoraj / nadwyzka ludu z wczoraj) - jedna
  stawka dla W / R / P / podzamczy, ten sam zapas dni, ta sama bramka oblezenia i lojalnosci (P1b) co w wojnie. Bez potrzeby - 0 (zasada Jeffa z 09.10
  "korona bez potrzeby nie bierze" zostaje: w pokoju potrzeba jest prawdziwa - zamki i chleb).
- **Kto placi -> kto dostaje:** kiesy klas ludu miast i podzamczy krolestwa (ponad zapas) -> `Kingdom.KingdomBudgetWallet` (ten sam przelew co
  `FolkWarAid`, wplyw dnia 165) -> w tej samej dobie: glowy rodow (zold zalog, PZ-2) i kiesy wyrobnikow glodnych miast (PZ-6). Niewydana reszta -
  w renty jak kazdy wplyw dnia.
- **Liczby i uzasadnienie:** pulap 0.5%/d = jedna piata stawki wojennej (2.5% przy s = pulap 3%, `Settings.cs:1705`). Potrzeba w roku 2 to 23-36
  tys. zl/d przy nadwyzce ludu krolestw w pokoju 11-21 mln [P `lud_k.out.txt`], czyli stawka skuteczna **0.15-0.18%/d** [S `pr1.out.txt`] - pulap
  wiaze tylko w ubogich Wolnych Miastach (Lys, Braavos, Tyrosh, Ibben: nadwyzka 0.15-0.51 mln), gdzie reszta idzie z dalszych zrodel. Kara lojalnosci
  P1b liczy sie od czesci CALEJ kiesy (`FolkAidLoyalty.cs`): ok. 0.05 punktu dziennie, trwale ok. -0.5 (podloga 60) [S].
- **Skutek rok 2 [S z P]:** danina pokoju Q6 / Q7 / Q8 = 22.9 / 28.3 / 35.7 tys. zl/d; przyrost lokaty krolestw w pokoju z 33.8-36.6 (bez Z.1) i 48.4
  (Z.1) tys. zl/d do ok. 0-15; lokata swiata d728 **25.1 -> ok. 18-20 mln** (w krolestwach w pokoju 16.6 -> ok. 10-11 mln); ok. 1/4 tego zlota wraca
  do ludu jako zarobek z wydatkow zalog (25% wydatkow zolnierzy, `TownFolk.Live.cs:780-830`).
- **Ryzyko rok 1:** krolestwa przed pierwsza wojna (d2-23) i 3-4 krolestwa w pokoju w Q1-Q4 - potrzeba zalog w d2-23 placi glownie skarbiec ponad
  zapas (korony maja po 2 mln), lud prawie nic (lokata w d23 mala) [S]. Lokata d120 nowej kampanii: -0..-0.3 mln [S].
- **Klucz MCM:** `FolkPeaceAid` (bool, tak) + `FolkPeaceAidMax` (0.005). Wylaczony = `WaRate` w pokoju zwraca 0 jak w 449d453.
- **Linia logu:** istniejaca "Danina z oszczednosci (P1): dzien N" dostaje w wierszu krolestwa w pokoju: `pokoj: potrzeba X (zalogi a, chleb b),
  stawka dnia y%, danina z`; w naglowku suma "danina pokoju" i krolestw z poborem w pokoju. Bez nowej linii (jedno zjawisko - jedna linia).
- **Prog:** PRZ-2 (rozdz. 9).

### PZ-2. Zalogi w pokoju na 80% stanu wojennego - doplaca korona (A15-1)

- **Co:** dzis w pokoju cel zalogi = GarrisonPeaceShare (0.5) x srednia zoldu zalogi z dob wojny (`_warGar`, EMA 28 dob, w zapisie), najwyzej 80%
  pulapu rodu; ponad cel 166 zwalnia (`ClanBudget.Garrisons`, `ClanBudget.cs:663-693`). Nowe: **cel 80% (CrownPeaceGarrisonShare)**, przy czym rod
  placi jak dotad do swojego celu (50%, w pulapie), a **korona placi gotowka zold ludzi, ktorzy SA w zalodze miedzy celem rodu a 80%** - doplata idzie
  za ludzmi (wzor W5), nie za obietnica; zwolnienia 166 licza nadwyzke ponad 80%, nie ponad 50%, w dniu, w ktorym korona zaplacila. Werbunek do 80% -
  tylko normalny przyrost zalogi gry, a korona daje miejsce najwyzej 10% celu na dobe (CrownWarAidRecruitStep).
- **Kto placi -> kto dostaje:** skarbiec (zrodla w kolejnosci: danina pokoju PZ-1 -> skarbiec ponad zapas wojny -> wolna czesc skrzyn panow
  krolestwa przez skarbiec (glowa -> skarbiec, jak tarczowe W6) -> reszta wplywow dnia) -> glowa rodu, znaczone na zold zalog (jak `b.Aid` W5: nie
  wchodzi do D, ksiega dochodu go nie widzi, nie jest podstawa zwrotu 70% - zalogi i tak go nie maja).
- **Komu:** rody AI krolestw w pokoju z twierdza i z pamiecia wojny (`_warGar` po co najmniej jednej dobie wojny; przed pierwsza wojna - stan z
  pierwszej doby, jak dzis); bez Strazy (zold 0), bez najemnikow, bez rodu gracza (gracz sam ustala zaloge), bez rodow w zajeciu za dlugi (168).
- **Liczby i uzasadnienie:** 80% = ten sam ulamek, na ktorym stoi prog N4 (69 020 = 0.8 x 86.3 tys. zalog z d93-120 biegu W43 [S - rachunek]);
  80% zostawia twierdze obronna przy pierwszym ataku (na koniec roku 2 zalogi Qarth 93, Lys 90, Qohor 241 ludzi [P BD 2.3]). Koszt pelnej doplaty
  w roku 2: Q6 26.8 / Q7 35.9 / Q8 40.9 tys. zl/d, brakujacych ludzi 12.0 / 16.0 / 18.2 tys. [P `zalogi.out.txt`]; pokrycie: danina pokoju 79-87%,
  renty 6-8%, reszta 4-13% - z wolnych skrzyn panow (Lys, Tyrosh, Braavos maja je ponad zapas) [S `pr1.out.txt`].
- **Skutek rok 2 [S]:** zalogi krolestw w pokoju Q8 **8.7 -> ok. 22-25 tys.** (cel 26.9), zalogi razem d728 **58.4 -> ok. 72-78 tys.** (prog N4 69 020
  - TAK); z Z.1 (64.3 tys. w d781) - ok. 75 tys. Krolestwo, ktore wraca do wojny, ma zaloge od razu (dzis Dorne d537 -> d616 odbudowalo wojsko od 450
  ludzi). Miara W5-7 (druzyny) - bez zmiany, zalogi jej nie licza.
- **Ryzyko rok 1:** w d2-23 (wszyscy w pokoju) 166 dzis tnie zalogi do 50% stanu z pierwszej doby (Q1 62.2 tys. wobec 83 tys. w Q2 [P]); z PZ-2 do
  80% - ok. +15 tys. ludzi przez 3 tygodnie, koszt ok. 57 tys. zl/d ze skarbcow ponad zapas (-1.2 mln z 57.8 mln, 1/360 zapasu mniej o ok. 3 tys.
  zl/d) [S]. Zalogi d120 - wyzej niz dzis (prog N4 "wiaze w d120" - na plus).
- **Klucz MCM:** `CrownPeaceGarrison` (bool, tak) + `CrownPeaceGarrisonShare` (0.80). Wylaczony = cel 50% i zwolnienia jak w 449d453.
- **Linia logu (nowa, jedna na dobe):** "Zalogi w pokoju (PZ-2): dzien N | krolestw w pokoju K, rodow z doplata R | zalogi w pokoju L ludzi, cel 80% C,
  cel rodow (50%) C0 | doplata korony X zl (danina pokoju a, skarbiec ponad zapas b, skrzynie panow c, wplywy dnia d), niepokryte u | zwolnieni z zalog
  ponad cel z doplata 0 | na krolestwo: ... | potkniecia n."
- **Prog:** PRZ-1.

### PZ-3. Garnek potrzeb z calej reszty wplywow dnia (A3)

- **Co:** garnek W5 = dzis "danina P1 tej doby, najwyzej reszta wplywow dnia po zwrocie" (`KingdomTreasury.NeedAid.cs:173`). Nowe: **garnek =
  cala reszta wplywow dnia po zwrocie zoldu** (danina jest w niej i tak). Ten sam poziom wyrownania, te same reguly "za ludzmi", "1 zl = 1 zl"
  (`NeedAidRefundCut`). Renty 180 dostaja to, co zostanie po potrzebach (dzis: po danine). Kredyt 186 dalej tylko na zwrot.
- **Kto placi -> kto dostaje:** skarbiec -> glowy rodow z potrzeba (gotowka W5 zamiast renty tej samej doby) - zmienia sie tylko odbiorca w krolestwie.
- **Liczby i uzasadnienie:** bez nowych liczb. W krolestwie bez niepokrytej potrzeby nic sie nie zmienia (renty jak dzis). Zloty renty podnosi
  biednemu rodowi pulap o 0.6 x f (0.8-1.0), z czego najpierw je zaloga; zloty W5 placi 1 zl zoldu druzyny - dlatego potrzeba przed renta.
- **Skutek rok 2 [S z P `pr3.out.txt`]:** z rent przechodzi do garnka 6.9 tys. zl/d: niepokryte Wysp 23.9 -> ok. 20.4 (z PZ-4 ok. 16.5), Wolnych Ludzi
  20.7 -> 18.4, Smoczej Skaly 1.9 -> ok. 1.2, Pentos 2.3 -> ok. 1.4.
- **Ryzyko rok 1:** w roku 1 z rent do garnka 6.3 tys. zl/d (niepokryte 23.7 -> ok. 17.4); rody bez potrzeby traca ok. 1.6% rent (390 tys. zl/d) [P/S].
- **Klucz MCM:** `CrownNeedsBeforeRents` (bool, tak). Wylaczony = garnek z samej daniny (449d453).
- **Linia logu:** w "Doplata wedlug potrzeby (W5)" w wierszu krolestwa: `garnek g (danina a, z wplywow dnia przed rentami b)`.
- **Prog:** PRZ-3.

### PZ-4. Sojusznicy i dar Dothrakom po potrzebach wlasnych panow (A3, A15-2)

- **Co:** (a) **dar Wolnych Miast dla Dothrakow (182)** liczony od reszty wplywow dnia PO potrzebach (dzis od wplywow brutto, jako drugi wydatek
  dnia); (b) **raty wezwan sojusznikow (168)** placone PO potrzebach (dzis przed zwrotem i W5); gdy na rate nie starcza - istniejaca regula 168:
  porozumienie konczy sie, reszta ceny skreslona (`CrownCallToWar.cs` naglowek).
- **Kto placi -> kto dostaje:** bez zmian (skarbiec -> skarbiec Dothrakow / portfel wezwanego); zmienia sie kolejnosc, wiec czesc zlota zostaje w
  garnku wlasnych panow.
- **Liczby i uzasadnienie:** bez nowych liczb (15% daru zostaje, liczone od innej podstawy). Lore: Wolne Miasta placa khalom z nadwyzki, nie z zoldu
  wlasnej strazy; korona bez pieniedzy na wlasnych panow nie kupuje sojusznikow (raport roczny R20: "nie podpisywac, gdy rata przekracza wplyw dnia").
- **Skutek rok 2 [S z P]:** w dniach braku placono wezwania 5.2 tys. zl/d (Wyspy 3.83 - Dorzeczu, Smocza Skala 1.28) i dar 1.2 tys. zl/d (Q8 2.0, z tego
  Pentos 1.77) -> do garnkow. Razem z PZ-3: **niepokryte roku 2 53.9 -> ok. 40.6 tys. zl/d** (Wyspy -7.4, Wolni Ludzie -2.3, Pentos / Smocza Skala do
  ok. 0.5-1); po W7 39.1 -> ok. 28-30. Dothrakowie (pokoj, skrzynie panow 7.3 mln ponad zapas) traca ok. 1.2 tys. zl/d z 12 tys. daru.
- **Ryzyko rok 1:** wezwania w dniach braku 0.5 tys. zl/d, dar 1.2 tys. zl/d [P]. Ryzyko mapy wojen: zerwane wezwanie = sojusznik wychodzi z tej
  wojny (`MakePeaceAction`, gdy nie trzyma go inne wezwanie). W roku 2 dotyczyloby ok. 1-3 porozumien (Wyspy -> Dorzecze d435, Smocza Skala <-> Pentos);
  przy zmeczeniu /4.33 wojen ma byc wiecej, wiec (b) ma osobny klucz - sedzia moze go odrzucic bez ruszania reszty.
- **Klucze MCM:** `FreeCityGiftAfterNeeds` (bool, tak), `CallToWarAfterOwnNeeds` (bool, tak). Wylaczone = kolejnosc 449d453.
- **Linia logu:** w W5 `dar Wolnych Miast po potrzebach zostal x`; w "Wezwania do wojny (168)": `raty przesuniete za potrzeby n (zl), zerwane z tego m`.
- **Prog:** PRZ-3, PRZ-9.

### PZ-5. Wezwanie placone tylko za walke (A15-2, Skagos)

- **Co:** rata dnia porozumienia 168 jest placona tylko w dobie, w ktorej wezwany jest w wojnie z krolestwem, przeciw ktoremu go wezwano. Porozumienie,
  w ktorym wezwany przez **14 dob z rzedu** (od podpisania albo od ostatniej doby walki) nie jest w wojnie z celem - wygasa (`EndCallToWarAgreement`
  gry; reszta ceny skreslona, portfel wyrownany jak przy zerwaniu - `CallToWarBreakRestoresWallet`).
- **Kto placi -> kto dostaje:** bez zmian (skarbiec wzywajacego -> portfel wezwanego); mniej zaplat za nic.
- **Liczby i uzasadnienie:** 14 dob = dwa tygodnie gry na wypowiedzenie wojny i marsz; w biegu dwuletnim 35 z 38 porozumien mialo wojne z celem od
  poczatku, a 3 bez wojny nie mialy ani jednej doby w 182 dobach [P ROZP-RODY 5.2].
- **Skutek [P]:** Nights Watch -> Skagos d13-70: **150 879 zl -> 0** (wygasa d27); roku 2 - 0 takich zaplat. Straz (zold 0, pulap w ludziach)
  zatrzymuje 2.6 tys. zl/d przez 57 dob - jej renty rosna.
- **Ryzyko rok 1:** jedno porozumienie (d13) - wygasniecie moze skonczyc sojusz gry wczesniej; nic poza tym [S].
- **Klucz MCM:** `CallToWarOnlyWhileFighting` (bool, tak) + `CallToWarIdleDays` (14). Wylaczony = 449d453. Licznik dob bez walki - pamiec sesji (po
  wczytaniu liczy od zera: najwyzej 14 dob bez zaplat wiecej, nigdy zaplata za nic).
- **Linia logu:** w "Wezwania do wojny (168)": `raty wstrzymane - wezwany bez wojny z celem n zl (porozumien m), wygasle po 14 dobach k`.
- **Prog:** PRZ-5.

### PZ-6. Chleb za roboty - kaskada dla glodnych miast (A4)

- **Co:** miasto glodne wedlug jednej definicji glodu (`Hunger.Of` = NoMoney: wyrobnicy nie stac na jedzenie, Afford < 0.95 przez 7 dob, od doby 120;
  `Hunger.cs`) dostaje co dobe **place robot publicznych dla wyrobnikow (W)** = brak koszyka W na jutro: max(0, potrzeba dnia W - kiesa W), najwyzej
  **50% potrzeby dnia W** (polowe miasto zarabia samo). Placi do konca glodu (Afford < 1.0 - histereza). Zrodla w kolejnosci z rozdz. 1: wolna czesc
  skrzyni pana miasta -> wolna czesc skrzyn innych panow krolestwa (wedlug ich wolnej czesci, jak tarczowe) -> danina krolestwa (wojna: z poboru dnia,
  zanim pojdzie w renty; pokoj: PZ-1) -> zawor kasy miasta (czesc ludu z 5% do brakujacej kwoty, cala do W - `TownPurse.Valve`) -> reszta wplywow dnia
  korony przed rentami.
- **Kto placi -> kto dostaje:** glowa rodu (G ponad R, nigdy ponizej R) -> kiesa W miasta; skarbiec -> kiesa W; kasa miasta (ponad zapas kupcow) ->
  kiesa W. Ksiega: zarobek ludu z nowa kolumna "roboty chlebowe" w `kiesa-obieg.csv` (jak z_budow). Bez materialu z polek (nic nie znika z towaru -
  rowniez nie wchodzi w zakres towarow-2).
- **Liczby i uzasadnienie:** 50% - miasto, ktore zarabia 0.35 potrzeby (Oldtown Q8), dochodzi do ok. 0.85-1.0, ale nie zyje z jalmuzny; wolna
  czesc skrzyni 0.20 / 0.68 - z istniejacych kluczy (rozdz. 1). Pan karmi swoje miasto, bo glod miasta zdejmuje zadowolenie BK z jedzenia (E5/C2 R14)
  -> lojalnosc i jego wplywy - to zachowanie z jego biezacego interesu, nie lista.
- **Skutek rok 2 [S z P `chleb2.out.txt`]:** luka glodnych miast Q6 / Q7 / Q8 = 21.3 / 31.4 / 31.7 tys. zl/d; pokrycie: pan miasta 1.3 / 6.1 / 11.4,
  panowie krolestwa 19.9 / 21.6 / 20.0, danina 0 / 0.8 / 0.4, korona 0 / 2.9 / 0 -> **brak 0** we wszystkich kwartalach roku 2 (i w Q3-Q5). Miasta:
  Oldtown 11.9 (panowie Reach - wolna czesc 22.6), Braavos + Rhyos 10.6 (panowie Braavos w pokoju 10.2 + danina), Highgarden 3.4 (Tyrell), Selhorys
  i Volon Therys 3.9 (panowie Volantis), Aquos Dhaen 1.8 (panowie Lys), Tyrosh 0.1. Chleb swiata P1-6 (d665-728) **89.7% -> ok. 96-98%**, miast < 0.95
  na koniec **7 -> 0-2** [S]. Uwaga trwalosci: skrzynie panow Braavos (0.55 mln ponad R) wystarcza na ok. 2-3 miesiace pelnej luki; potem Braavos
  dostaje tyle, ile panowie odkladaja (2-3 tys. zl/d) + korona i danina - luka Braavos/Rhyos wraca do ok. 4-5 tys. zl/d (ok. 12-15% luki swiata),
  dopoki kasa kupcow Braavos jest pusta (przyczyna - A12 "Braavos bez zaworu", zakres drobne-2) [S]. Place chlebowe ida na targ miasta - wracaja do
  pustej kasy kupcow (sprzedaz) i do robocizny, wiec moga przerwac bledne kolo przyciecia plac (`TownFolk.Live.cs:824`) [S].
- **Ryzyko rok 1:** Oldtown od d183 (6.2 / 12.0 tys. zl/d w Q3 / Q4) i Rhyos w Q4 (1.8) - z wolnych skrzyn panow Reach i Braavos [P/S]; panowie Reach
  maja ponad zapas 1.2 mln (d183) -> 3.8 mln (d364) [P]. Przed doba 120 regula milczy (`Hunger.FromDay`). Bieg 120 dob - bez skutku.
- **Klucz MCM:** `BreadWorks` (bool, tak) + `BreadWorksMaxShare` (0.5). Wylaczony = 449d453 (zawor 5% W20/R10/P70, brak plac chlebowych).
- **Linia logu (nowa):** "Chleb (PZ-6): dzien N | miast glodnych K (E5/C2), brak W L zl | zaplacone X: pan miasta a, panowie krolestwa b, danina c,
  zawor d, korona e; niepokryte u | najwyzej 50% potrzeby W: miast z pulapem m | skrzynie: najnizsze G/R placacych (>= 1.00) | na miasto: nazwa
  brak/zaplacone (zrodlo) ... | potkniecia n."
- **Prog:** PRZ-4.

### PZ-7. Pomiary bez zmian w zlocie (A15-3, A15-4, A15-5)

- **A15-3 zwyciezca pokoju fabularnego dostaje 0** - bez zmian: ROT wznowil 111 z 111 takich wojen w ciagu 2 dob [P], reparacja bylaby przelewem za
  nic; fabuly nie ruszamy.
- **A15-4 najemnicy krolestw w pokoju z zapasu** - bez zmian: rok 2 1.0-1.8 tys. zl/d, z zapasu najwyzej 0.1-0.6 i gasnie razem z 1/360 [P].
- **A15-5 "+373 tys." z rozliczen rodow** - tylko linia: w "Pieniadz swiata (rody - przyczyny)" kwoty PRZENIESIONE obok naliczonych (migawka kiesy
  glowy przed / po naliczeniu zoldu w `MoneyLedger.ClanTickPrefix/Postfix`). Ksiega ma 0 zrodel bez platnika [P] - logika bez zmian.
- **Napis 165:** "raty reparacji" rozbite na `raty okupow krolow (178)` i `raty reparacji` (dzis ok. 90% to okupy - ROZP-RODY 5.3). Jesli drobne-2 bierze
  te linie - zostawic im.
- Klucz: brak (same linie). Prog: PRZ-7 (INFO).

---

## 3. Przeplywy w liczbach - rok 2 biegu dwuletniego, przed / po [S z P]

| przeplyw (platnik -> odbiorca) | dzis (449d453) | PRZEPLYW |
|---|---:|---:|
| kiesy ludu krolestw w pokoju -> skarbce (danina pokoju) | 0 | Q6 22.9 / Q7 28.3 / Q8 35.7 |
| skarbce -> glowy rodow, zold zalog w pokoju (znaczone) | 0 | Q6 26.8 / Q7 35.9 / Q8 40.9 (pelna doplata; czesc z rent i skrzyn) |
| wolne skrzynie panow -> kiesy W glodnych miast | 0 | Q6 21.2 / Q7 27.7 / Q8 31.4 |
| skarbce -> W glodnych miast (danina, wplywy) | 0 | 0-3.7 |
| renty -> gotowka W5 (te same skarbce, inni odbiorcy) | 0 | 6.9 (rok 2) |
| wezwania 168 i dar Dothrakom -> gotowka W5 | 0 | 5.2 + 1.2 (rok 2) |
| skarbiec Strazy -> Skagos za wojne bez walki | 150 879 zl (rok 1) | 0 |

| stan | dzis | PRZEPLYW |
|---|---:|---:|
| lokata W swiata d728 | 25.08 mln | ok. 18-20 mln |
| - w krolestwach w pokoju | 16.63 mln | ok. 10-11 mln |
| przyrost lokaty krolestw w pokoju (bieg dwuletni / Z.1) | 33.8-36.6 / 48.4 tys. zl/d | ok. 0-15 |
| zalogi d728 (N4) | 58 366 | ok. 72-78 tys. |
| niepokryte W5 rok 2 (po W7) | 53.9 (39.1) | ok. 40.6 (28-30) |
| - Zelazne Wyspy / Wolni Ludzie | 23.9 / 20.7 | ok. 16.5 / 18.4 |
| luka glodnych miast Q8 | 31.7 (8 miast) | 0 (w roku 3 Braavos ok. 4-5) |
| chleb swiata P1-6 d665-728 | 89.7% | ok. 96-98% |
| miara W5-7 (druzyny w wojnie) | 55.3 tys. | +1..+2 tys. (PZ-3/PZ-4) |

---

## 4. Dowod zamknietego obiegu

| regula | platnik (posiadacz w MoneyLedger) | odbiorca | kolejnosc zapisu | gdy wyjatek |
|---|---|---|---|---|
| PZ-1 | kiesa klasy ludu B1 (zdejmuje wolajacy - `TownFolk.PayAid`) | skarbiec | jak `FolkWarAid`: skarbiec dopisany ostatni | nic nie przelane |
| PZ-2 | skarbiec; wolna skrzynia: glowa -> skarbiec (jak W6) | glowa rodu (zold zalog) | najpierw skarbiec, potem glowa (wzor W5) | zloto wraca |
| PZ-3, PZ-4 | skarbiec | glowa rodu / skarbiec Dothrakow / portfel wezwanego | bez zmian (inna kolejnosc w tej samej dobie) | jak dzis |
| PZ-5 | - (mniej zaplat) | - | wygasniecie = sciezka zerwania 168 z wyrownaniem portfela | jak dzis |
| PZ-6 | glowa (G ponad R) / skarbiec / kasa miasta ponad zapas kupcow | kiesa W (posiadacz B1) | platnik pierwszy, `FromCoffer` dla kasy | zloto wraca |

Zadnej nowej pozycji "z niczego" i "w nicosc"; kazdy przelew ma licznik w swojej linii i w "Obieg". Kontrola zamknieta kies ludu (r = 0) dostaje
dwie nowe pozycje: wydatek "danina pokoju" (w istniejacej daninie P1) i wplyw "roboty chlebowe".

---

## 5. Sto procent - kogo obejmuje

| sprawa | zakres reguly | pokrycie potrzeby (rok 2) | co zostaje |
|---|---|---|---|
| A4 chleb | kazde miasto (97), kazde krolestwo, wojna i pokoj; jedna definicja glodu E5/C2 | 100% w Q3-Q8 [S] | Braavos/Rhyos po wyczerpaniu skrzyn panow Braavos (rok 3): ok. 4-5 tys. zl/d |
| A15-1 zalogi | kazda twierdza rodu AI krolestwa w pokoju | 87-96% pelnej doplaty z daniny i rent, reszta ze skrzyn | ubogie Wolne Miasta bez skrzyn - zaloga 60-80% |
| A5 lokata | kazde krolestwo w pokoju z potrzeba | danina pokoju = potrzeba | krolestwo w pokoju BEZ potrzeby nie placi (zasada 09.10) - lokata tam lezy |
| A15-2 wezwania | kazde porozumienie 168 | 100% zaplat za walke | - |
| **A3 brak na zold** | kazde krolestwo w wojnie | **ok. 25% braku** (13.3 z 53.9) | **ok. 35 tys. zl/d u Wolnych Ludzi i Wysp - w krolestwie nie ma platnika** |

A3 - dlaczego przeplyw nie siega dalej [P]: oszczednosci ludu Wolnych Ludzi 0.06 mln, Wysp 0.30 mln (0.2% i 0.9% swiata); skrzynie panow ponad
zapas: Wolni Ludzie 1 tys. zl (1 rod z 11), Wyspy 138 tys. (18 z 33 rodow, wolna czesc 0.6 tys. zl/d); skarbce na podlodze 0.50 mln od Q3 roku 1;
garnek po PZ-3/PZ-4: Wyspy ok. 13, Wolni Ludzie ok. 4 tys. zl/d przy potrzebie 29.8 i 22.6. Zadna istniejaca droga pieniadza nie przechodzi do nich
z bogatych krolestw: dary 182 maja pary z lore (Polnoc i 7 krolestw -> Straz, Wolne Miasta -> Dothrakowie), wezwania 168 sa decyzja AI wzywajacego,
a Wolni Ludzie nie maja zadnego krolestwa walczacego z tym samym wrogiem (wojny: Straz, Skagos [P KRONIKA d729]).

---

## 6. Gracz - co zobaczy (napisy po angielsku)

- Jego miasta i zamki placa danine pokoju jak wszystkie (krol-gracz: do skarbca). Komunikat raz na pokoj: "In peace the people of {towns} give the
  crown a small share of their savings above their reserve - only what the realm needs to keep its castles manned and its hungry towns fed (now one
  part in {n} a day)."
- Rod gracza nie dostaje doplaty do zalog (sam ustala zaloge); jego kiesa nie jest brana na chleb (jak W6b - tylko z wlasnym kluczem). Glodne miasto
  gracza: najpierw inni panowie krolestwa, korona, potem zawor - gracz widzi mniejsza czesc zaworu: "The labourers of {town} cannot afford their bread.
  The town's merchants paid them {x} coins for public works today instead of sending it to you."
- Krol-gracz co 7 dob: "The crown paid {x} coins this week to keep the realm's garrisons manned in peace and {y} coins for bread in hungry towns."
- Wezwanie gracza (jego korona placi sojusznikowi): "{Ally} has not gone to war with {enemy} for 14 days - the agreement has lapsed and nothing more is
  owed."
- Okno mieszkancow: zdanie o danine pokoju i o placach chlebowych (ile i od kogo).

---

## 7. Zasada 0 - regresje i kolizje (sprawdzone w kodzie 449d453)

- **Z.1 (PeaceChestShare):** PZ-2 nie zmienia celu zaloga rodu ani pulapu pokoju - doplata jest ponad nim, poza pulapem; PZ-6 bierze tylko wolna
  czesc skrzyni (0.68 = 1 - 0.4 x 0.8), wiec skrzynia pokoju Z.1 zostaje cala. Kolizja: brak [P `ClanBudget.Ceiling`].
- **W5 / W5b / W6 / W7:** kolejnosc zrodel W5 bez zmian, PZ-3 dopisuje piaty szczebel przed rentami. W7 bierze tylko niepokryta potrzebe - po PZ-3
  bierze mniej (lud biednych krolestw zyskuje). W5b regula 1 (SN): mniejsza niepokryta potrzeba -> SN rosnie wolniej - zamierzone. Chleb z wolnej
  skrzyni liczony PO tarczowym dnia (wspolna podstawa G - R) - nie dwa razy.
- **P1 / P1b / 184:** PZ-1 to ta sama sciezka `FolkWarAid` z druga stawka w pokoju (bramki: krolestwo, oblezenie, lojalnosc <= 30); podatek 184 od
  zarobku dalej tylko w wojnie. Kara P1b liczona od czesci kiesy - w pokoju ok. -0.5 lojalnosci [S].
- **180 renty:** biora reszte po potrzebach; warunki rent i wagi lenn bez zmian.
- **182 / 168:** zmiana kolejnosci tylko w PZ-4 (osobne klucze); PZ-5 uzywa istniejacej sciezki zerwania z wyrownaniem portfela (P3/W4).
- **Bank (186):** kredyt dalej tylko na zwrot; garnek potrzeb nie bierze kredytu.
- **Kiesa ludu i towary:** place chlebowe to zarobek ludu (nowa kategoria) - bez materialu z polek, bez zbytkow, bez plotna i skory (zakres towarow-2
  nietkniety). Lokata W nie bierze sie z plac chlebowych w miescie glodnym (kiesa W < potrzeba) - kontrola P1-1 bez falszu.
- **Ladunek dla innych prac:** A1 (wojsko w pokoju, w toku) - PZ-2 nie rusza pulapu druzyn w pokoju; A12 (Braavos bez zaworu, drobne-2) - PZ-6 nie
  rusza diagnozy zaworu; 4.20 (niebezpieczenstwo okolicy) - nietkniete.
- **Stan miedzy kampaniami:** PZ-5 licznik dob i PZ-6 histereza - pamiec sesji, czyszczone w Reset; PZ-2 korzysta z `_warGar` (juz w zapisie).
- **Czas doby:** PZ-6 - przejscie po 97 miastach i rodach krolestw z glodem (zwykle 0-8 miast), PZ-2 - po twierdzach rodow w pokoju: razem < 0.5 ms [S].

---

## 8. Rozwazone i odrzucone (z rachunkiem) i decyzja dla reszty A3

| pomysl | dlaczego nie |
|---|---|
| danina pokoju stala 0.5% bez potrzeby | zabralaby ok. 100 tys. zl/d (0.5% x 20.6 mln) do rent bogatych panow w pokoju - pieniadz przechodzi z kiesy ludu do skrzyni pana i dalej lezy; lamie zasade 09.10 |
| "wspolny wrog placi" (korona z nadwyzka -> korona z brakiem walczaca z tym samym wrogiem) | nowa para przeplywu bez lore (Dorzecze -> Zelazne Wyspy ok. 5.6 tys. zl/d); Wolni Ludzie: 0 (nikt nie walczy ze Straza i Skagos) |
| Bank Zelazny pozycza na doplate W5 | dlug bez zrodla splaty (wplywy Wysp 14, Wolnych Ludzi 4 tys. zl/d), po 28 dobach zaleglosc i odciecie; blisko odlozonej pozyczki F2 |
| Wolni Ludzie bez zoldu (jak Straz) | nie dodaje ludzi: pulap w ludziach = pulap zlota / zold nominalny, ten sam D (6.8 tys. zl/d -> ok. 1 tys. ludzi) |
| zysk z rabunku z kies ludu miast | losowe (zalezy od AI), rusza ekonomie rabunku R1; Wyspy - Dolina 342 doby bez bitwy (R29) |
| place ludu "za glowami" (robocizna x m_t) | prawdziwa przyczyna A4/A5-7, ale to zmiana modelu zarobku ludu na kazdy dzien roku 1 - osobny projekt po towarach-2 |
| GarrisonPeaceShare 0.5 -> 0.8 bez korony | zaloga placona pierwsza zjadlaby pulap pokoju i skrzynie Z.1 (druzyny w pokoju -> 0) - kat BUDZET, nie przeplyw |

**Decyzja dla reszty A3 (ok. 35 tys. zl/d przed W7, 28-30 po W7, Wolni Ludzie i Wyspy):** przeplyw nie ma skad. Do wyboru dla sedziow (przeszczep z
innego katu): (a) zalogi rodow G < R w wojnie z celem jak w pokoju (50% wojennej) - przesuwa ok. 10 tys. zl/d z zalog na druzyny u Wysp i Wolnych
Ludzi kosztem ok. 4 tys. ludzi w zalogach [P ROZP-RODY 6.3]; po PZ-2 prog N4 ma ok. 3-9 tys. zapasu, wiec to sie miesci; (b) miara: niepokryte
"w krolestwie z platnikiem" (prog 0) osobno od "bez platnika" (INFO). Moja rada: (a) + (b) - (a) jako osobny klucz w projekcie BUDZET.

---

## 9. Progi (`tools/sprawdz_logi.py --grupa przeplyw`) i plan testu

Testy (sesja glowna uruchamia gre): **T1** = zapis `autotest-rok2-d780-fin24` + 60 dob (rok 2, Z.1 w grze); **T2** = nowa kampania 120 dob (rok 1
bez pogorszenia; porownanie z fin26 = W53, `Armoury-2026-10-14_03-24-46.log`).

| # | test | wiersz | prog | dzis |
|---|---|---|---|---|
| PRZ-1 | T1 | zalogi krolestw w pokoju na koniec / na poczatku; zwolnieni z zalog ponizej 80% przy doplacie | >= +5 tys. ludzi; 0 | Z.1: 6.8 -> 14.5 tys. w 60 dob |
| PRZ-1b | bieg dwuletni | E4/8 N4 zalogi d728 | >= 69 020 | 58 366 |
| PRZ-2 | T1 | przyrost lokaty W krolestw w pokoju (okno 28 dob); danina pokoju > 0 w kazdym krolestwie w pokoju z potrzeba i nadwyzka; stawka dnia | <= 25 tys. zl/d; 100%; <= 0.5% | 48.4 (Z.1); 0%; - |
| PRZ-2b | bieg dwuletni | P1-1 lokata d728 (prog 6 mln nieosiagalny - rownowaga daniny ok. 15 mln nawet w pelnej wojnie, ROZP-LUD A5-5) | <= 20 mln | 25.08 |
| PRZ-3 | T1 | niepokryte W5 w krolestwach z platnikiem (renty dnia > 0); Wyspy d781-840 | <= 1 tys. zl/d; <= 13 | Pentos Q8 6.9; 17.1-18.8 |
| PRZ-3i | T1 | niepokryte "bez platnika" (Wolni Ludzie, Wyspy) | INFO | 35-37 |
| PRZ-4 | T1 | miasta glodne E5/C2 na koniec; stac pelny koszyk swiat (ostatnie 28 dob); chleb niepokryty / luka; placacy pan ponizej R | <= 2; >= 96%; <= 10%; 0 | 7; 90.9%; -; - |
| PRZ-5 | T2 | zaplacone wezwanym bez wojny z celem po 14 dobach | 0 zl | Skagos 150 879 (d13-70) |
| PRZ-6 | T2 | W5-7 d93-120; danina P1 d91-120; zalogi d120; lokata d120; stac pelny d91-120 | >= W53 - 2 tys.; >= W53 - 10%; >= W53; <= W53 + 5%; >= W53 - 0.5 pkt | fin26 |
| PRZ-7 | T1, T2 | zrodla bez platnika (ksiega swiata); kontrola zamknieta kies ludu r; linie A15-5 i 165 obecne | 0; 0; tak (INFO) | 0; 0 |
| PRZ-8 | T1, T2 | czas doby naszych modow | <= +0.5 ms | 398-417 ms |
| PRZ-9 | T1 | porozumien 168 zerwanych przez PZ-4 w 60 dob | <= 3 (INFO powyzej) | - |

Pomiar zmeczenia /4.33: bieg dwuletni po wgraniu - te same wiersze PRZ-1b, PRZ-2b i niepokryte "na krolestwo" (krolestw w pokoju bedzie mniej, wiec
PZ-1/PZ-2 dzialaja ciszej, a danina wojenna bierze wiecej).

---

## 10. Czego nie wiem (+ linia diagnozy)

1. Jak szybko zalogi wracaja z 50% do 80% po wczytaniu zapisu roku 2 (w T1 juz zwolnione) - przyrost zalogi gry nie jest mierzony. Linia: PZ-2 "zalogi
   w pokoju" dzien po dniu; w dwuletnim biegu PZ-2 tylko trzyma (bez werbunku) - szacunek 72-78 tys. dotyczy biegu od nowa.
2. Ile z "potrzeby" glodnych miast to dzielenie wydatku gry przez czynnik kotwicy (ROZP-LUD nie wiem 5) - wtedy place chlebowe sa wyzsze niz trzeba,
   ale samoograniczajace (max(0, potrzeba - kiesa)). Linia: w "Chleb (PZ-6)" potrzeba W przed i po kotwicy.
3. Czy place chlebowe podnosza dobrobyt (a z nim potrzebe) - przyczyna wzrostu dobrobytu +42% w 2 lata nieznana (R16). Linia: dobrobyt glodnych
   miast co 7 dob przed i po PZ-6.
4. Czy zerwanie wezwania przez PZ-4 (b) wywoluje ponowne porozumienia gry co kilka dob (AI wzywajacego decyduje glosowaniem rodow -
   `ProposeCallToWarAgreementDecision`). Linia: nowe porozumienia wzywajacego z niepokryta potrzeba w 28 dobach (PRZ-9).
5. Jak dlugo panowie Braavos utrzymaja chleb Braavos i Rhyos (skrzynia 0.55 mln ponad R, przyrost 2-3 tys. zl/d) - szacunek 2-3 miesiace. Linia:
   "Chleb (PZ-6)" - najnizsze G/R placacych na krolestwo.
6. Skutek zmeczenia /4.33 na liczbe krolestw w pokoju, a wiec na wielkosc PZ-1 / PZ-2 - brak biegu dwuletniego po tej zmianie.

---

## 11. Pliki i polecenia

Skrypty i wyniki (dysk C, katalog tymczasowy):
`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\przeplyw\`
- `k165.py` -> `k165.out.txt` (wplywy i wydatki koron na krolestwo, rok 2), `lud_k.py` -> `lud_k.out.txt` (nadwyzka ludu, danina, luka miast na
  krolestwo i kwartal), `zalogi.py` -> `zalogi.out.txt` i `pr1.py` -> `pr1.out.txt` (PZ-1/PZ-2), `pr3.py` -> `pr3.out.txt` (PZ-3), `chleb.py` /
  `chleb2.py` -> `chleb.out.txt` / `chleb2.out.txt` (PZ-6), `rody_k.py` -> `rody_k.out.txt` (G, R, zalogi na krolestwo d722).
- Wejscie: `...\rody2\dw.pkl`, `dwc.pkl` (wyciagi rozpoznania) i tabele biegu dwuletniego `D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_21-14-13\`
  (`kiesa-ludu.csv`, `kiesa-obieg.csv`, `kasy-miast.csv`, `budzet-rodow.csv`).

```
D=D:/Backup-Bannerlord/logs/2026-10-14-noc/Logs/2026-10-13_21-14-13 ; R=<scratchpad>/rody2
python -I pr1.py $D $R/dw.pkl 0.005
python -I pr3.py $R/dw.pkl
python -I chleb2.py $D $R/dw.pkl
```
