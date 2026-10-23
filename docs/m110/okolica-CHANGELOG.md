## m110-okolica - jeniec podaje tylko okolice kryjowki, kryjowke trzeba wypatrzyc zwiadem (23.10)

**Mod:** Armoury (nowe `CampInterrogation.Area.cs`; `CampInterrogation.cs`, `CampInterrogation.Test.cs`, `HideoutSight.cs` - jedna metoda tylko do odczytu `Knows`;
`Settings.cs` + `McmSettings.cs` z `gen_mcm.py`), `tools/sprawdz_logi.py` (nowa grupa `m110okol`, poprawka PRZES-2 grupy `m109przes`). Bez latek Harmony.

**Dla Jeffa (prosto):**
- Jeniec, ktory mowi w obozie ("Question the prisoners"), nie pokazuje juz dokladnej kryjowki. Mowi tylko, gdzie mniej wiecej jej szukac, liczac od najblizszego
  miasta, zamku albo wsi: np. "about 100 km north-east of Fever".
- Herszt i czlowiek z bandy, ktora niedawno rozbiles, znaja droge - podaja odleglosc w km (co 25 km). Zwykly bandyta, ktory zna tylko kryjowke swoich ludzi, mowi
  jak plotka z karczmy: "about half a day's ride west of ..." (pol dnia, dzien, dwa, trzy dni drogi).
- Na mapie pojawia sie znacznik "Outlaw lair - roughly here" (herb bandy) w opisanym miejscu, na tyle dob co flaga kryjowki (Hideout Flag Days, u Ciebie 3).
  Mozesz go odpiac kulka jak kazdy znacznik. Sama kryjowka NIE jest pokazana.
- Kryjowke wypatrujesz zwyklym zwiadem: jak po plotce z karczmy (Hideout Rumour Reveals) - gdy podjedziesz blisko, gra ja wypatruje jak zawsze. Wtedy dostajesz
  zwykly meldunek "Hideout spotted" z flaga, a znacznik okolicy znika sam.
- Pomiar na mapie ROT (266 kryjowek): od herszta albo czlowieka z bandy znacznik stoi srednio ok. 4 jedn. mapy (ok. 18 km) od kryjowki - mniej wiecej co drugi raz
  kryjowke widac juz przy znaczniku, prawie zawsze po malym kolku. Od zwyklego bandyty ok. 7 jedn. (ok. 30 km) - trzeba przeszukac okolice.
- Ten sam jeniec (ta sama banda) nie wskaze drugi raz kryjowki, ktorej juz szukasz. Wyjatek: jesli szukasz jej tylko po przyblizonej wskazowce zwyklego
  bandyty ("half a day's ride ..."), a zlapiesz herszta albo czlowieka z tej bandy - on ja doprecyzuje (km), stary znacznik zastapi nowy. Limity dnia bez zmian.
- Odpiety znacznik zostaje odpiety takze po zapisie gry (wczesniej po kazdym zapisie znowu przyklejal sie do brzegu ekranu).
- Jesli wylaczysz flagi (Hideout Flag Enabled) i potem wlaczysz, znaczniki trwajacych wskazowek wroca w ciagu godziny gry (nie trzeba wczytywac).
- Wylaczasz w MCM: "Interrogation Area Only" (NIE = jeniec pokazuje dokladna kryjowke jak dotad - od razu, takze te, ktorych juz szukasz po wskazowce).

**Problem:** Jeff 23.10 ok. 15:50 na pytanie "czy jeniec ma podawac dokladna kryjowke (jak teraz), czy tylko okolice, ktora trzeba jeszcze sprawdzic zwiadem":
"zwiadem". W 104 (m109-przesluch) jeniec ujawnial dokladna kryjowke: `Hideout.IsSpotted`, `Settlement.IsVisible` i flaga `HideoutSpotter.Mark` - zwiad nie byl potrzebny.

**Przyczyna:** `CampInterrogation.Act` przy MOWI ustawial `IsSpotted`/`IsVisible` i flage na samej kryjowce (zasada m109 "ujawnienie jak zwiad gry").

**Zmiana (klucz `InterrogationAreaOnly`, domyslnie TAK; NIE = 1:1 jak b49bdf78):**
- `CampInterrogation.Area.cs` - przy MOWI zamiast ujawnienia **wskazowka** (`Give`):
  - opis: najblizsza osada gry (miasto / zamek / wies, ta sama petla co `HideoutSpotter.Where`), 8 stron swiata (co 45 st.), odleglosc wedlug tego, co jeniec wie
    (zrodlo z `Target`, nie rzut): skad bohater|banda - km co 25 (najmniej 25); skad kultura - slowa pasm jak `Rumours.Days` (22/60/110/160 jedn., punkt w srodku
    pasma 16/41/85/135, dalej 185). Punkt opisany przyciety do `Campaign.Min/MaxSettlement*`.
  - znacznik obszaru: `MapMarkerManager.CreateMapMarker` (QuestId `arm_lair_hint`, herb klanu kryjowki) przy `HideoutFlagEnabled` i `HideoutFlagDays` > 0;
    zdejmowany przed kazdym zapisem (`OnBeforeSaveEvent`) i stawiany po zapisie (`OnSaveOverEvent`, takze nieudanym) i po wczytaniu (start sesji) - zapis gry
    nigdy nie ma naszego znacznika (wzor NoticeBoard / FreeFolkRaids). Odpiecie przez gracza pamietane.
  - wiedza: `HideoutSight.Heard` (jak plotka z karczmy) - przy Hideout Scouting Chance i Hideout Rumour Reveals wypatrzenie jak w grze przy podejsciu.
  - koniec: `OnHideoutSpottedEvent` (od razu) albo przeglad co godzine (`IsSpotted` bez zdarzenia, termin = Hideout Flag Days dob - co najmniej 1, kryjowki
    juz nie ma); znacznik zdjety, linia logu, suma "znalezione". HideoutSpotter bez zmian (meldunek i flaga przy prawdziwym wypatrzeniu).
  - `Target`/`Open` pomijaja kryjowki z czynna wskazowka; nowy powod odmowy `WhyAllKnownArea` tylko, gdy pominieto wskazana.
  - zapis: szosta czesc `arm_interrogation` "kryjowka,odniesienie,termin,x,y,skad,wskazanie,przypiety;..." (godziny kampanii), sumy 6-7 (wskazane, znalezione).
- `CampInterrogation.cs`: 4 sluchacze (godzina przez `DayProfile.Hr`, wypatrzenie, przed zapisem, po zapisie); w `Act` galaz okolicy (linia "wskazowka" tuz po
  linii proby m109; w trybie okolicy "ujawnione" = 0, liczniki zrodel w m110); teksty EN okolicy (`AreaStory`, `LairHint`, podpowiedz opcji, `Log.Player`);
  notka zapisu m109 + ", wskazane N, znalezione M".
- `HideoutSight.Knows(id)` - tylko odczyt `_known` (pole "wiedza" w logu i test).
- Test autotestu (`armoury-przesluch.flag`, bez nowych hakow): T0 pomija wskazane; T1 (klucz TAK): wskazowka TAK, H2 NIE wypatrzona, IsVisible jak przed, znacznik
  przypiety przy flagach, wiedza przy kluczach m100, opis z osada odniesienia; T1b (oba tryby) Target(A) != H2; T7 wskazowka H1; T12 zapis (znaczniki zdjete i
  oddane z przypieciem); T10 wypatrzenie H1 -> "znaleziona", suma +1; T11 probna wskazowka po terminie -> "po terminie"; T4 MOWI => wskazowka; T9 zapis z
  wskazowkami i 8 sumami; sprzatanie: probne wskazowki zdjete, wskazowki prob zostaja jako slad biegu.
- Log "Przesluchanie (m110): " - start sesji, wskazowka, koniec wskazowki, zapis / po zapisie (pierwsze 5 na proces), doba.
- `tools/sprawdz_logi.py`: PRZES-2 przyjmuje MOWI z "po NIE, flaga NIE" + doklejona wskazowke tej kryjowki (przypadki syntetyczne "okolica" TAK i
  "okolica-bez-wskazowki" NIE); nowa grupa `m110okol` (OKOL-S, OKOL-S2, OKOL-0..9; opis w --help).

**Po recenzjach (poprawnosc + kod, 23.10) - weryfikacja i poprawki:**
- R1 (obie recenzje) PRAWDA: odpiety znacznik po zapisie wygladal na przypiety. Dowod (dekompilacja 1.4.8): `MapMarkerManager.CreateMapMarker` = `RegisterObject`
  -> `OnMapMarkerCreated` -> `MapTrackerProvider.AddIfEligible` -> `new MapMarkerTrackerItemVM`, konstruktor `MapTrackerItemVM<T>` czyta `IsTracked = CheckTracked`
  (wtedy TAK), potem zmienia go tylko `Track`/`Untrack` (klik kulki). Poprawka: `PutMarker` po odpieciu oglasza znacznik drugi raz
  (`CampaignEventDispatcher.Instance.OnMapMarkerRemoved` + `OnMapMarkerCreated`) - provider buduje kafelek od nowa jako odpiety. Nazwy tych zdarzen sa tylko
  w TaleWorlds.CampaignSystem i SandBox.ViewModelCollection (grep po DLL gry i 51 modulow). Test T12: "kafelek odpietych odswiezony N/N".
- R2 poprawnosc / R3 kod PRAWDA: klucz NIE nie byl 1:1 przy czynnych wskazowkach z czasu klucza TAK (`Hinted` bez `AreaOn`). Poprawka: `HintBlocks` -
  przy kluczu NIE nic nie pomija; kryjowka ujawniona dokladnie, wskazowke konczy przeglad godzinny ("znaleziona (droga przeglad)", znacznik zdjety).
- R3 poprawnosc PRAWDA (zadanie: blokada tylko "ta sama grupa"): herszt / czlowiek bandy pomijal swoja kryjowke, gdy miala wskazowke przyblizona od kogos
  innego, i wskazywal inna. Poprawka: `HintBlocks(st, exact)` - zrodlo bohater|banda pomija tylko wskazowke dokladna; przyblizona ("kultura") konczy sie
  linia "koniec wskazowki ... | doprecyzowana" i zaraz po niej idzie nowa (km). Przyblizona na przyblizona i dokladna na dokladna - dalej pominiete.
  Test T7 (klucz TAK): najpierw probna przyblizona H1, herszt ja doprecyzowuje ("doprecyzowanie TAK"). Prog OKOL-4.
- R4 (obie) PRAWDA: wlaczenie flag w trakcie sesji nie stawialo znacznikow czynnym wskazowkom (do wczytania). Poprawka: `_markersOwed` (wskazowka bez
  znacznika przy flagach wylaczonych: `Give`, zapis, start sesji) - pierwszy przeglad godzinny przy flagach wlaczonych stawia brakujace znaczniki raz (linia
  "flagi wlaczone - znaczniki postawione N z M"). Test T13. Wylaczenie flag bez zmian (znaczniki znikaja przy najblizszym zapisie).
- R2 kod PRAWDA: OKOL-6 dawal falszywe NIE (a) przy "Save And Exit" - `MbEvent.AddNonSerializedListener` dopisuje na poczatek listy, wiec `MapScreen.OnSaveOver`
  (pozniej zarejestrowany) idzie przed naszym i konczy gre; (b) przy flagach wylaczonych. Poprawka: linia "po zapisie" i linia doby licza
  "nieodtworzone: flagi wylaczone N, brak kampanii M"; OKOL-6: odtworzone + nieodtworzone z powodem = zdjete. Zgubiony bez powodu dalej NIE (test syntetyczny).
- R5 kod NIEPEWNE (interpretacja "wpis o sukcesie"): przy znalezieniu jest linia logu "koniec wskazowki | znaleziona", suma "znalezione" w zapisie i w linii
  doby; gracz widzi zwykly meldunek HideoutSpotter ("Hideout spotted") z flaga i znika znacznik okolicy. Osobnego komunikatu "to kryjowka z zeznania" nie
  dodano (zero kodu przy okazji) - do decyzji Jeffa po grze.

**Odstepstwa od projektu (male):**
- zapis wskazowki ma godzine wskazania (`startH`) zamiast doby (linia konca podaje "od wskazania N godz.");
- `Give` liczy liczniki doby i sume "wskazane" dla kazdej wskazowki (takze probnej testu) - jedna zasada liczenia z suma "znalezione" w `End`;
- linia wskazowki ma pole "przyciety TAK|NIE" (OKOL-2 liczy wzor bledu tylko bez przyciecia) i "od godz. N" w terminie (OKOL-5 sprawdza termin = wskazanie + doby x 24);
- linia konca zawsze "(skad S, wypatrzona T, zajeta T)" oraz wynik "zdjeta (sprzatanie testu)" dla probnych;
- T1: wiedza sprawdzana jako "klucze m100 TAK => wiedza TAK" (przy kluczach NIE stara wiedza z plotki nie daje falszywego NIE);
- pasmo "many days' ride" (ponad 160 jedn.) - punkt 185 jedn. (w ROT najblizsza osada kryjowki jest zawsze blizej);
- linia startu m110 logowana osobnym `try` po linii m109 (potkniecie linii m109 nie blokuje odtworzenia znacznikow).

**Ryzyko / co sprawdzic (kontrola calosci CLAUDE.md 8.0):**
- Kto czyta `IsSpotted`/`IsVisible`: tryb okolicy ich nie ustawia. Leaks310 - herszt pojawia sie dopiero po prawdziwym wypatrzeniu (pozniej niz w 104, zamierzone).
  Plotki karczmy moga dalej mowic o wskazanej kryjowce (`Heard` - "juz znana", bez szkody). HideoutSpotter przy prawdziwym wypatrzeniu: meldunek + flaga z terminem m110-flaga.
- `HideoutSight._known` rosnie jak przy plotkach (czysci `MarkCleared`), KR-4 bez zmian (droga "plotka").
- `VisualTrackerManager`: MapMarker to osobny obiekt (flagi osad Wayfinder, NoticeBoard, zadan - bez kolizji); `MapMarkerManager` w modach uzywa tylko NavalDLC (wlasne id).
- Zapis: znacznik zdejmowany przed zapisem - zapis go nie ma; gdyby trafil (potkniecie), nowa DLL sprzata go przy starcie ("stare znaczniki w zapisie N", OKOL-6 NIE).
  Szosta czesc i sumy 6-7: DLL 104 czyta czesci 1-5 i pierwsze 6 sum (`Parse` 104: petla do dlugosci wlasnej tablicy). Stary zapis: brak czesci = brak wskazowek.
- Klucz NIE: zachowanie 1:1 (takze przy wskazowkach z czasu klucza TAK - `HintBlocks` nic nie pomija); w zapisie dochodzi pusta szosta czesc i 2 zera sum
  (dane). Wskazowki z czasu klucza TAK dobiegaja terminu albo koncza sie przy dokladnym ujawnieniu (przeglad godzinny).
- `HideoutFlagEnabled` przelaczony na NIE w trakcie: znaczniki znikna przy najblizszym zapisie (nie wracaja po nim, linia "nieodtworzone: flagi wylaczone N");
  z powrotem na TAK - wracaja w pierwszym przegladzie godzinnym (`MarkersOwed`, raz na zmiane stanu).
- Doprecyzowanie: suma "wskazane" liczy obie wskazowki tej kryjowki (przyblizona i dokladna); termin liczy sie od nowa od dokladnej.
- Ogloszenie odpietego znacznika drugi raz: zdarzenia `OnMapMarkerRemoved`/`OnMapMarkerCreated` sluchane tylko przez `MapTrackerProvider` (kafelek usuniety
  i zbudowany od nowa) - tylko przy stawianiu odpietego (po zapisie, po wczytaniu, przeglad po wlaczeniu flag), nie co klatke.
- Zamknieta ekonomia: zero zlota i towarow. Limity doby, miara perswazji, tortury i kary bez zmian.
- Niesprawdzone w grze (okno - sprawdza Jeff): wyglad znacznika na mapie ROT (herb bandytow, napis), dymek, odpinanie, teksty. Zasieg wzroku noca i z modelami
  widocznosci BK / BKROTPatch / AIInfluence / NavalDLC nieznany (probka 3.6 jedn. z logu Jeffa).
- Przy Hideout Rumour Reveals = NIE wskazowka przy niskim Zwiadzie prawie bezuzyteczna (wybor ustawien). Cel "kultura" moze lezec do 160 jedn. - znacznik (3 doby)
  moze zejsc przed dojazdem; wiedza zostaje.
- Galaz m110-stan rownolegle: mozliwy konflikt tekstowy w Settings.cs / McmSettings.cs - po scaleniu `python -I tools/gen_mcm.py`.
- Slady testu w zapisach autotestu: wskazowka H2 do terminu, `_known` H1/H2.

**Plan autotestu:** p4 (flaga przesluch: T0-T13 TAK albo POMINIETY, w tym T7 "doprecyzowanie TAK", T12 "kafelek odpietych odswiezony 1/1", T13 TAK;
grupy m109przes, m110okol, m100kryjowki, m98plotki) -> lc (DLL 104.1 wczytuje zapis p4 po nazwie: linia m109 "zapis: pamiec ...", PRZES-1 i KR-1 0) -> c4
(nowa DLL, ten sam zapis: "zapis: wskazowek >= 1 (zlych 0)", "stare znaczniki w zapisie 0", koniec H2 droga godzinna) -> p20 -> wczyt (stary zapis: "brak
czesci wskazowek" / "brak klucza") -> bitwa (0 ERROR, m110 tylko start i doba). We wszystkich: OKOL-4 (doprecyzowanych >= 1 w p4 przy T7), OKOL-6 0
niezgodnych (pary "po zapisie" z "nieodtworzone: ..."), linia "flagi wlaczone" tylko z T13.
NIEPEWNE do sprawdzenia (autotest tego nie widzi): wyglad kafelka odpietego po zapisie (R1 - okno, sprawdza Jeff: odpiety znacznik po autozapisie nie wraca na
brzeg ekranu); "Save And Exit" przy czynnym znaczniku (R2 kod a - w logu "po zapisie ... brak kampanii 1" albo "odtworzone 1", OKOL-6 TAK w obu); klucz NIE
przy czynnej wskazowce (R2 - tylko w kodzie; bieg z kluczem NIE na zapisie c4: wskazana kryjowka ujawniona dokladnie, koniec "znaleziona (droga przeglad)");
"wpis o sukcesie" dla gracza (R5 - decyzja Jeffa).

**Status:** DO SPRAWDZENIA (build Armoury 0 bledow; progi: OKOL-S/S2 i PRZES-S/S2 TAK, w tym nowe przypadki syntetyczne zapis bez flag, zapis i wyjscie,
zgubiony znacznik, doprecyzowanie dobre i zle; na logach 104 m110okol - INFO, m109przes bez zmian werdyktow - PRZES-5 NIE w 109f-p20 jest i w bazie b49bdf78).
Poprawki po recenzjach: commity 4e3c23c2 (kod), 9072aab9 (progi).
