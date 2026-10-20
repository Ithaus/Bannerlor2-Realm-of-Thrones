# p5-armia - dzien odpoczynku armii (R3-08a) i Twoj marsz forsowny (R3-08c), galaz w-toku/p5-armia na 5189c0fe (kandydat WGRANIA 92)

## Dla Jeffa (prostymi slowami)
Armie lordow po 6 dniach marszu z rzedu staja na caly dzien odpoczynku; potem maja wieksza spojnosc (+2) i lepsze morale (+5 dla kazdej druzyny w obozie).
Nie staja, gdy oblegaja, walcza, ida na odsiecz albo gdy blisko jest silniejszy wrog - i zrywaja postoj, gdy przyjdzie niebezpieczenstwo. Twoja armia dostaje
to samo, jesli po takim marszu sama postoi dzien (o swicie dostaniesz komunikat, ze postoj sie nalezy). Nowy rozkaz "Forced march" (klawisz O albo pytanie
"Night Falls"): druzyna idzie o 25% szybciej, ale nikt nie spi, a kazde 24 godziny takiego marszu kosztuja jeszcze jedna zarwana noc - przy marszu bez
przerwy dlug snu rosnie co 12 godzin, wiec to zryw na dzien-dwa, nie sposob na cala wyprawe.

## Problem / zrodlo pomyslu
- Jeff 20.10 o liscie `docs/POMYSLY-35-2026-10-20.md`: "28 - nie, 29 - nie. Pozostale tak". Pozycje 21 (R3-08a) i 23 (R3-08c), zrodlo: audyt 09.10
  `docs/audyt-2026-10-09/07-mapa-czas-obozy.md` (Dla Jeffa pkt 7 i 1, tabela 4.4 N-2 i N-4).
- R3-08a: armie AI maszeruja bez dnia odpoczynku; jedyny postoj to oboz nocy 0-6. Audyt: "armia AI co 7. dzien marszu stoi dobe; spojnosc +2, morale +5"
  i ryzyko: StrategicCampaignAI przerywa postoj, liczby sprzed spowolnienia rozpadu armii z 14.10.
- R3-08c: gracz ma tylko "March on" (marsz noca bez premii); AI od 10.10 ma "najwyzej 2 noce forsownego marszu". Audyt: "+25% predkosci, sen nie liczy
  sie wcale, dlug rosnie co 12 h", lancuch predkosci `NightRest.SpeedPostfix`.
- Stan dzis (log testu 160 dob na WGRANIU 91, `Modules/Armoury/Armoury-2026-10-20_00-44-29.log`): "Spojnosc armii (AC): armie 17 ... spojnosc srednio
  90.2 | zmiana na dobe srednio teraz -0.52" i "AC wiek": rozwiazane z powodu Inactivity 64 (srednio 18.9 dob), ArmyLeaderIsDead 43, LeaderPartyRemoved 43,
  FoodProblem 11 - zadna armia nie rozpadla sie przez spojnosc.

## Zmiana (Armoury; dwa nowe pliki, dwa klucze MCM domyslnie TAK)
### R3-08a `ArmyRestDay` (`Armoury/src/ArmyRestDay.cs`, grupa MCM "Armies of the realms (175)")
- Doba armii swit-swit (swit = `NightRest.PlayerDawn`, ta sama godzina, o ktorej oboz swiata budzi armie). Co godzine pozycja wodza: godzina "w marszu" =
  przesuniecie > 0.35 jedn. poza osada (stala `Drill.RestStep`, jak ksiega snu); tick nadrabiany powtarza poprzednia godzine (`NightClock.CatchUp`).
- O swicie: >= 4 h marszu = doba marszu (seria +1); <= 2 h ruchu bez godziny bitwy/oblezenia = doba odpoczynku (seria 0, a po serii >= progu premia);
  reszta (oblezenie, bitwa, krotki marsz) serii nie zmienia - liczy sie "co 7. dzien MARSZU". Niepelna doba (< 18 h w ksiedze, np. nowa armia) sie nie liczy.
- Postoj AI: po serii >= `ArmyRestDayAfterMarchDays` (6) armia AI stoi od tego switu do nastepnego. W dzien trzymamy my: `DisableForHours(1)` co godzine,
  Hold, straznik co 0.1 h gry (tick mapy) cofa cudzy rozkaz i zjazd > 0.3 jedn. (SCA daje rozkazy co 4 h). W nocy obozu, snie dlugu, alarmie nocy
  i wstrzymaniu w osadzie rzadzi NightRest (my nic nie ruszamy - jego alarmy od wroga dzialaja jak zawsze). Po postoju AI decyduje od nowa (rozkaz sprzed
  postoju nie wraca - jak swit obozu przy Hold).
- Nie zaczyna (odklada do nastepnego switu): bitwa, oblezenie (oboz obleznikow, oblezona osada), morze, odsiecz/obrona (DefendSettlement), szturm, cel
  oblezenia/najazdu w promieniu zagrozenia, ucieczka, alarm nocy, cudza blokada AI (uczta / gentry BK), licznik bezczynnosci gry >= 44 z 48, silniejszy
  wrog (>= 0.8 sily armii, lord albo armia w polu) w promieniu max(6, AiCampDangerRadius) jedn., oblezona albo lupiona osada wlasnego krolestwa w 3 x tym
  promieniu. W trakcie postoju co godzine przerywaja go: bitwa, oblezenie, morze, doczepienie, silniejszy wrog i osada krolestwa w potrzebie (AI wodza
  od reki z powrotem - samo ocenia walke albo ucieczke).
- Bezczynnosc gry [K `Army.CheckInactivity`]: kazda godzina wodza na Hold = +1, 48 = rozwiazanie armii (Inactivity). W godzinach, w ktorych to my trzymamy
  armie, licznik wraca do wartosci z poczatku postoju (odpoczynek nie jest bezczynnoscia); noc obozu liczy sie jak zawsze.
- Premia (czysta doba odpoczynku po serii >= progu - po postoju AI, postoj sam z siebie albo armia gracza): spojnosc + `ArmyRestDayCohesion` (2, do 100),
  `RecentEventsMorale` + `ArmyRestDayMorale` (5) wodza i kazdej doczepionej druzyny (gra zdejmuje 10% tego dodatku na dobe).
- Gracz: nikt go nie zatrzymuje; o swicie po serii komunikat ("Your army has marched N days in a row..."), czysta doba postoju daje te sama premie
  (komunikat zielony).
- Inni (Nieumarli) nie odpoczywaja i nie sa liczeni.
- Zapis `arm_armyrest` (SaveText.Sync): wpis na armie po StringId wodza (seria, liczniki doby, postoj, licznik bezczynnosci z poczatku postoju).
### R3-08c `ForcedMarch` (`Armoury/src/ForcedMarch.cs`, grupa MCM "A night's rest")
- Wlaczenie: klawisz O (panel obozu, nowa pozycja "Forced march" / "End the forced march") albo pytanie "Night Falls" (nowa pozycja "Forced march" obok
  "March on"). Bez nowego klawisza - nie zderzy sie z innym modem. Wymaga `NightRestEnabled`.
- Predkosc: +`ForcedMarchSpeedPercent` (25%) od WYNIKU lancucha modeli, raz na lancuch, w `NightRest.SpeedPostfix` PRZED kara dlugu (kara liczy sie od
  predkosci z premia: dlug 1 = 1.25 x 0.75 = 0.94, dlug 2 = 0.75); sufit lancucha podniesiony o ten sam procent (jak premia karczmy). W okienku predkosci
  linia "Forced march". Dziala dla partii gracza i armii, ktora gracz prowadzi (doczepieni biora predkosc wodza - `MobileParty.CalculateSpeed`).
  Gra trzyma predkosc w pamieci podrecznej (odswieza ja m.in. przy zmianie pola siatki w ruchu) - premia wchodzi w pierwszych chwilach marszu, jak kara dlugu.
- Sen sie nie liczy: godzina z rozkazem nie dodaje odpoczynku doby; postoj w nocy nie stawia obozu automatycznego; brak pytania o nocleg i obozu z rozkazu
  "always make camp".
- Dlug: swit liczy dobe jak zawsze (bez snu = +1), a do tego kazde 24 h rozkazu = jeszcze jedna noc dlugu (komunikat; dlug o swicie dla musztry tez, jak
  `SettleDeferred`). Przy marszu bez przerwy = 2 noce na dobe, czyli co 12 h. Krotszy zryw zostawia "zmeczenie" (godziny), ktore liczy sie do nastepnego
  marszu forsownego i schodzi o 2 h za kazda dobe zwyklego wypoczynku (baza snu, zero godzin rozkazu) - nie ma darmowych zrywow przez wylaczanie przed 24 h.
- Koniec sam: oboz (O, Night Falls, oboz BK), sen w menu, wejscie do osady (takze karczma), niewola, sluzba ROT, doczepienie do cudzej armii, morze, dlug 3
  (zapasc - kolumna staje), wylaczenie klucza. Nie zaczyna: niewola, sluzba ROT, osada, morze, cudza armia, sen, Nieumarli, dlug 3.
- Zapis `arm_forcedmarch` (SaveText.Sync): wlaczony, zmeczenie, godziny w biezacej dobie.
### Wpiecia w pliki wspolne (minimalne)
- `NightRest.cs`: OnHourly (godzina marszu forsownego przed dodaniem odpoczynku, warunek obozu automatycznego, brak pytania o nocleg), SpeedPostfix (1 linia),
  panel O (pozycja 4 + case), Night Falls (pozycja 4 + case). Odczyty prywatnego stanu NightRest - w `partial class NightRest` w nowych plikach.
- `ArmouryBehavior.cs`: Reset w konstruktorze, dwa klucze SyncData, `ArmyRestDay.Hourly()` zaraz po `NightRest.OnHourly()`, `ArmyRestDay.OnTick()` w TickEvent.
- `Settings.cs`: 2 bloki pol (przy NightMarchNotice i przy ArmyCohesionYearScale), `McmSettings.cs` z `tools/gen_mcm.py`, w `gen_mcm.py` zakresy 4 suwakow.

## Ryzyko / co sprawdzic (kontrola calosci)
- Regresje: z wylaczonymi kluczami kod wraca do starej sciezki co do zachowania (FM: `forced` zawsze false, brak pozycji w menu; Rest: brak wpisow,
  `OnTick` wychodzi na `_resting == 0`). Ksiega snu gracza zmieniona tylko w godzinach z rozkazem.
- Kolizje: (1) NightRest oboz nocy / sen dlugu / alarm - postoj nie dotyka wodza, ktorego trzyma NightRest (`NightRest.NightHolds`), a swit postoju jest
  PO swicie obozu w tym samym ticku. (2) SleepGate (bramka SetMove*) - dotyczy tylko spiacych w obozie i snie dlugu, nasze Hold w dzien przechodzi.
  (3) Cudza blokada AI (BK) - postoj nie zaczyna sie (`IsDisabled` bez NightRest), nasze `DisableForHours(1)` nie wyglada dla NightRest na cudza blokade
  (termin <= 1 h). (4) StrategicCampaignAI145 - co 4 h `TryRetreatFromBadSiege` / `TryReinforceFragileGarrison`; oblezenie postoju nie zaczyna, rozkaz
  wzmocnienia zalogi straznik cofa (licznik "wstrzymania straznika"). (5) Premia karczmy i marsz forsowny moga sie sumowac (oba od wyniku).
- Spojnosc logiki: ekonomia nie dotknieta (bez zlota i rzeczy). Spojnosc +2: dzis spadek srednio -0.52 na dobe (AC), armia 5 druzyn ok. -0.8 u siebie,
  -1.6 na ziemi wroga - +2 to 1-2.5 doby spadku; audyt dawal +2 przy -3.6 na dobe. Przeliczone x 0.23 wyszloby +0.46, mniej niz jedna doba spadku przy
  koszcie doby drogi - zostawiam +2 (do zmiany suwakiem). Spojnosc dzis i tak nie rozwiazuje armii - wiekszy skutek da morale +5 (mniej dezercji).
- Wojny: armie AI na dlugich marszach ida ok. 1/7 wolniej (tylko przy 6+ dobach marszu z rzedu - oblezenia serii nie zeruja). Postoj moze dac wrogowi
  okazje (zamiar pomyslu), ale nie stoi przy oblezeniu/najezdzie wlasnej osady w 3 x promieniu ani przy silniejszym wrogu.
- Bezczynnosc: ochrona licznika gry tylko w godzinach naszego postoju. UWAGA (obserwacja, nie zmieniane - poza zadaniem): Inactivity to najczestsza przyczyna
  rozwiazania armii (64 ze 162 w 160 dobach) - Hold obozu nocy dodaje +6 na noc do tego licznika gry; do sprawdzenia osobno.
- Marsz forsowny: przy 25% i karach dlugu jest oplacalny tylko na dzien-dwa (dlug 1 zjada prawie caly zysk). Lordowie AI doczepieni do armii gracza nie
  dostaja dodatkowej nocy z rozkazu (ich ksiega snu jak dotad - i tak nie spia w marszu, wiec swit daje im +1). Zmeczenie zostaje miedzy marszami (zanik 2 h/dobe).
- Wydajnosc: tick godzinowy - O(armii) (~17) + przeglad zagrozen (lordowie swiata, ok. 600 odleglosci) tylko dla armii w postoju; straznik co 0.1 h gry tylko
  dla armii w postoju; SpeedBonus wolany rownolegle - tylko pole volatile i odczyt ustawien, bez alokacji.
- Zapis: nowe klucze `arm_forcedmarch`, `arm_armyrest`; stary zapis bez nich = stan zerowy; stary DLL ich nie czyta (rozkaz i serie przepadaja).
- Scalanie: wpiecia w NightRest.OnHourly (3 miejsca), SpeedPostfix, panel O i Night Falls; ArmouryBehavior (konstruktor, SyncData po arm_nightrest_ai,
  tick godzinowy po NightRest.OnHourly, TickEvent po CrossingLaw); Settings (2 bloki); gen_mcm RANGES po MaxForcedNights.

## Status: DO SPRAWDZENIA (build Armoury 0 bledow; bez autotestu - zakaz w tym zadaniu)

## Decyzje (projektowal Claude - Jeff: "ekonomie projektuje Claude")
1. R3-08c dlug "co 12 h": swit bez zmian + 1 noc za kazde 24 h rozkazu (przy marszu bez przerwy = co 12 h). Odrzucone: krok co 12 h + swit (24 h = zapasc)
   i krok co 12 h z pominieciem switu (marsz forsowny noca tanszy niz zwykly "March on").
2. R3-08c zmeczenie z krotkich zrywow zostaje i zanika 2 h na dobe wypoczynku - bez tego wylaczanie przed 24 h dawaloby +25% za darmo codziennie.
3. R3-08c wlaczenie w panelu O i w Night Falls, nie nowy klawisz (audyt: "nowy klawisz moze sie zderzyc z innym modem").
4. R3-08c postoj w nocy przy rozkazie nie stawia obozu - rozkaz konczy sie tylko oboz/sen/osada/morze itd., zeby krotki postoj nie gasil rozkazu.
5. R3-08a prog: 6 dob marszu (>= 4 h ruchu), postoj 7. dnia od switu do switu; oblezenie i bitwa serii nie zeruja ("co 7. dzien marszu").
6. R3-08a spojnosc +2 zostaje mimo spowolnienia rozpadu (wyliczenie wyzej), morale +5 jak w audycie.
7. R3-08a armia nie stoi, gdy trzeba bronic wlasnych (oblezenie / najazd w poblizu) albo grozi silniejszy wrog; postoj zrywa niebezpieczenstwo.
8. R3-08a gracz: komunikat i ta sama premia, bez zatrzymywania (propozycja z opisu pomyslu 21).
9. R3-08a licznik bezczynnosci gry chroniony w dzien postoju - inaczej dzienny Hold (+18) rozwiazywalby armie, ktore juz maja noce obozu na liczniku.

## Test w grze
- Autotest (log Armoury): linia "Dzien odpoczynku armii (R3-08a): dzien N" co dobe - po ok. 7 dobach "postoje zaczete" > 0, "z premia ... po postoju AI" > 0,
  "przerwane" z powodami, "bezczynnosc gry cofnieta" > 0; przyklady "staje na dobe po N dobach marszu" i "postoj przerwany: ...". "Spojnosc armii (AC wiek)":
  Inactivity nie powinno rosnac szybciej niz dotad (64 na 160 dob). Potkniecia 0.
- Jeff (marsz forsowny - autotest go nie wlacza): na mapie O -> "Forced march": komunikat, w okienku predkosci "Forced march +..."; postoj noca bez namiotu;
  po 24 h komunikat o dodatkowej nocy; wejscie do miasta -> "The forced march ends."; w logu linie "Marsz forsowny (R3-08c):" (wlaczony, koniec, swit gracza).
- Jeff (armia): po 6 dniach marszu Twojej armii komunikat o swicie; dzien postoju -> "A day of rest: your army's cohesion +2...".
