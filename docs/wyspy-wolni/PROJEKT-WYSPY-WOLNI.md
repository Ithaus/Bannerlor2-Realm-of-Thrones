# PROJEKT: ZELAZNE WYSPY Z RABUNKU, WOLNI LUDZIE BEZ ZOLDU (decyzja Jeffa 14.10) - 14.10.2026

Autor: krok projektanta workflow "wyspy-wolni". Baza (wszystkie nowe klucze "off") = `w-toku/zbior-e8` **63aaaedf** (z pieniedzmi roku 2: M1, M2a-c, M3, B4).
Galaz kodu: `w-toku/wyspy-wolni` w drzewie `zb\ww`. Tor **L**. Status: **PROJEKT** - kodu nie ma, nic nie wgrane, gry nie uruchamialem.
Znaczniki: **[P]** pomiar z logu / CSV, **[K]** odczyt kodu (nasz, gra, BK, NavalDLC - plik:linia), **[S]** szacunek (metoda podana), **[L]** lore.
Zrodla: `ROZPOZNANIE-WYSPY.md` (RW), `ROZPOZNANIE-WOLNI.md` (RWL), `../pieniadze-rok-2/PROJEKT-WYBRANY.md` (PW), bieg dwuletni
`D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log` + `Logs\2026-10-13_21-14-13\*.csv` (BD), bieg 120 dob `...\Armoury-2026-10-14_05-22-14.log`.
Doba = dzien gry - 108836 + 1; rok 2 = d365-728. Kwoty: tys. zl na dobe, chyba ze napisano inaczej. Id: Wyspy `sturgia`, Wolni Ludzie `freefolk`, Straz `nightswatch`.

---

## 0. Dla Jeffa - prosto

1. **Zelazna cena.** Najazd Zelaznych Wysp zabiera polowe skrzyni wsi (dzis ulamek procenta - caly swiat zabral wsiom w roku 2 23 tys. zl). Najazd z morza na wies portu
   bierze tez 10% oszczednosci biedoty tego portu ponad ich zapas na 10 dni. Gracz jako wasal Wysp - dokladnie tak samo.
2. **Lup idzie na zalogi okretow.** Kazdy denar z zelaznej ceny podnosi o denar to, ile pan moze placic druzynom w polu - nie zalogom zamkow i nie dworowi.
3. **Lordowie Wysp licza zysk.** Najazd na bogata wies kusi ich do 3 razy mocniej niz dzis, na pusta - tak jak dzis. Dalej rabuja tylko wroga w wojnie i w zasiegu;
   wojen, pokoju ani fabuly ROT nie ruszam.
4. **Korsarze.** Bandy morskie "Sea Raiders" (plywaja kolo Pyke, Old Wyk, Wyspy Niedzwiedziej) sprzedaja lup i wydaja zloto w portach Wysp, a czwarta czesc tego,
   co dostana od pasera, oddaja panu portu za schronienie. To dziala takze w pokoju.
5. **Ile to da.** Wyspom brakuje dzis 23.9 tys. zl dziennie na zold (po regulach roku 2 ok. 20 tys.). Lup da 1-6 tys. dziennie, zaleznie od tego, ile razy AI wyplynie
   (dzis ok. 10 najazdow rocznie). Celu "brak najwyzej 8 tys." sam lup nie osiagnie: skrzynie wsi Zachodu i zachodniej Polnocy zarabiaja razem ok. 6-8 tys. dziennie -
   wiecej tam po prostu nie ma.
6. **Pytanie do Ciebie (kodu nie pisze):** czy zalogi okretow Wysp maja brac polowe zoldu "w lupie" (Zelazny Czlowiek nie bierze zoldu, bierze zelazna cene)? Wtedy brak spada do ok. 4-8 tys.
7. **Koszt dla swiata:** skrzynie wsi Zachodu i Polnocy na wybrzezu chudna (30-80% w rok), korona Krolewskiej Przystani bierze z nich mniej; na Wyspach przybywa thralli.
8. **Wolni Ludzie nie biora zoldu - jak Nocna Straz.** BK i tak liczy 1 zl dziennie na czlowieka (w zalodze pol) - zold spada z 11 do ok. 3.6 tys. zl dziennie,
   kiesy wodzow przestaja topniec, "brak na zold" Wolnych Ludzi znika.
9. **Ich wojsko trzyma lud, nie zloto:** najwyzej 1.8% ludzi krainy (ok. 5 tys.; tyle mieli w pierwszym polroczu, gdy pieniadze ich jeszcze nie hamowaly - dzis 3.6 tys.).
   Zalogi najwyzej polowa tego, reszta w polu: druzyny z 0.7 do ok. 2.1 tys.
10. **Jedzenie z lupu:** wodz Wolnych Ludzi werbuje tylko, gdy ma jedzenie na 8 dni (tyle gra wymaga do najazdu); jedzenie i zloto biora z najazdow na Dar, bitew i spichlerzy.
11. **Nocna Straz bez zmian.** Ryzyko: wiecej Wolnych Ludzi w polu = wiecej bitew ze Straza (ma 2.5 tys. ludzi) i wiecej trupow dla Innych - test to mierzy.
12. **Najpierw dowod:** po dobie 405 Wyspy nie zrobily ani jednego najazdu, choc byly w wojnie. Nowa linia pokaze dlaczego (sila, jedzenie, armie, zasieg);
    poprawka na przyczyne - dopiero po tescie, nie na zapas.

---

## 1. Punkt wyjscia i cel

| | Zelazne Wyspy | Wolni Ludzie |
|---|---:|---:|
| brak na zold rok 2 po W5 (BD, PW A3) [P] | **23.9** | **20.7** |
| to samo po M1 i M2b (63aaaedf; PW 3.1 "rok 2 do W5: Wyspy 3.5, WL 2.3") [S, rec. PW 8.3 - nie zmierzone] | ok. 20.4 | ok. 18.4 |
| zold druzyn / zalog rok 2 (rody z lennem) [P `budzet-rodow.csv`, skrypt `ww2\gr.py`] | 16.4 / 14.9 | 5.8 / 5.5 |
| ludzie druzyny / zalogi / limit druzyn (tys.) [P] | 2.54 / 4.92 / 7.99 | 0.84 / 2.97 / 5.36 |
| D rodow / kiesy G / rezerwa R (rody z lennem, mln dla G i R) [P] | 18.2 / 0.34 / 0.77 (G < R u wszystkich) | 5.6 / 0.12 / 0.40 |
| jednorazowe (lup, okupy, statki) [P] | 2.8 | 0.6 |
| doplata W5 / pobor pana W7 [P] | 5.9 / 7.7 | 2.0 / 3.0 |

**Cel z zadania:** Wyspy - brak rok 2 <= 1/3 dzisiejszego = **<= 8.0 tys. zl/d**; Wolni Ludzie - zold 0 albo znacznie mniej, zycie z lupu.
**Dlaczego Wyspom nie wystarcza dochod [K + P]:** zalogi (14.9) sa placone pierwsze (`ClanBudget.cs:610` PartyCap = Cap - zalogi - karawany; `GarFull` w wojnie), a wlasny
pulap rodow Wysp bez W5/W7 to ok. 8 tys. (pulap 21.6 minus doplaty 13.6) - na druzyny z wlasnych pieniedzy zostaje 0. Kazdy denar w D podnosi pulap o 0.6 x f (f ok. 0.89
przy G < R, `ClanBudget.Ceiling`) i najpierw "dopelnia" zalogi. Stad regula WW2: nowy lup omija zalogi i dwor.

---

## 2. ZELAZNE WYSPY - reguly

Kolejnosc w dobie: najazd (WW1 - koniec kroku rabunku, w ticku) -> ksiega dochodu (zelazna cena w osobnym koszu) -> `ClanBudget.Daily` (WW2: pulap druzyn z lupu)
-> tick godzinowy AI (WW3: ocena opcji rabunku). Korsarze (WW4) - w dobie band (`OutlawLaw`: paser i "zycie w miastach"). Lista najezdzcow nowych regul:
`ReaverRealms = { "sturgia", "freefolk" }` (stala w kodzie jak `ClanBudget.Raiders`); **Dothrakowie (`khuzait`) poza** - Jeff o nich nie decydowal, zostaja na W-1 (lup w D).

### 2.0 WW0 - linia "dlaczego nie rabuja" (pomiar, bez klucza)

- **Co:** postfiks `CampaignEventDispatcher.AiHourlyTick` (ten sam co WW3; precedens `WatchLaw.AiTickPostfix` H1 [K WatchLaw.cs:361-392]) tylko liczy, dla partii lordow AI z `ReaverRealms`:
  ile partii, ile w armii, ile w oblezeniu / rabunku / obronie / postoju, dni jedzenia (min / mediana; ile < 8 = prog gry `NeededFoodsInDaysThresholdForRaid` [K DefaultMobilePartyAIModel:37]),
  ile partii mialo choc jedna opcje `RaidSettlement` w `PartyThinkParams`, ile opcji, najlepszy wynik rabunku wobec najlepszego wyniku innej opcji, wybrane zachowanie (histogram), ilu wrogow w wojnie.
  Raz na sesje: `GetAverageDistanceBetweenClosestTwoTownsWithNavigationType` dla Default / Naval / All (prog zasiegu = 5 x) i odleglosc drogowa Pyke -> Flint's Finger, Seagard, Lannisport, Wyspy Tarczowe.
- **Po co:** RW "nie wiem" 1, 2, 7 i RWL 7.1 (dlaczego WL nie rabuja Daru). Przyczyn nie zakladam - kazda poprawka na sile (`DefaultTargetScoreCalculatingModel` :205-243),
  jedzenie albo armie powstanie dopiero po tej linii, jako osobny krok.
- **Linia:** `Najezdzcy - decyzje AI (WW0): dzien N | Iron Islands: partii P (armia A, oblezenie O, rabunek R, obrona D, postoj H), jedzenie min/med X/Y dni (< 8: K), z opcja rabunku Q (opcji S), wynik rabunku/innej (mediana) a/b, wybrano rabunek W; wrogow w wojnie E (w zasiegu: Z) | WW3: mnoznik mediana m, x3 n | Free Folk: ...`.
  Jednorazowo: `Najezdzcy - zasieg (WW0): srednia odleglosc Default/Naval/All a/b/c, prog 5x ...; Pyke -> ...`.
- **Prog:** WW-0 (linie sa, `ERROR in` WW*/WL* = 0).

### 2.1 WW1 - zelazna cena (lup z najazdu)

- **Klucz:** `ReaverIronPrice` (bool, **on**), `ReaverIronPriceShare` (**0.50**), `ReaverWaterfrontShare` (**0.10**). Off = E7/R1 jak w 63aaaedf (udzial s = trafieni / ludzie w domu).
- **Regula.** Gdy wodz rabunku (AI albo gracz, E7/R1 i E7/R2) nalezy do krolestwa z `ReaverRealms`, udzial lupu kroku = **max(s, Q x u)**, gdzie Q = 0.50, u = postep kroku
  (1 po pelnym kroku; przerwany - czesc zrobiona, jak dzis [K DistrictRaid.cs:465-493]). Ten sam udzial z kiesy wsi i z magazynu wsi. Gracz (E7/R2) - kazdy pelny krok bierze
  Q z tego, co ZOSTALO (nic dwa razy): krok 1 - 50%, krok 2 - kolejne 25% skrzyni.
  **Nabrzeze (tylko najazd z morza):** gdy zdarzenie rabunku jest morskie (`MapEvent.IsNavalMapEvent` - ta sama miara co kontekst "NavalRaid" w B175 [K KingdomBalance.cs:234, 276])
  i osada macierzysta wsi (`Village.Bound`) ma port - dodatkowo Qw x u = 10% kiesy klasy W (robotnicy, rybacy, dokerzy) tej osady **ponad zapas `TownFolkReserveDays` (10) dni**
  potrzeby - ta sama podloga, ktorej uzywa danina P1 [K TownFolk.Live.cs:860-862]. Klasy R i P (mieszczanie za murem) - nie.
- **Kto -> komu:** kiesa wsi -> wodz napastnika (ta sama funkcja platnika `SmallGold.PayRaidLoot` -> `LootPayer` [K SmallGold.cs:366-384]); towar z magazynu wsi -> napastnicy
  (jak dzis); kiesa W portu -> wodz napastnika (nowy przelew kiesa ludu -> bohater; pozycja w kontroli zamknietej kies "Kiesa ludu" `_ctlIn`, r = 0).
  Przyrost ponad s placony OSOBNYM przelewem w kontekscie "zelazna cena" - ksiega dochodu rodu ksieguje go w koszu WW2, nie w jednorazowych W-1 (bez podwojnego liczenia).
- **Liczby (decyzja):**
  - Q = 0.50: [L] zelazny czlowiek bierze to, co znajdzie w palonych domach, a reszta okregu oddaje kosztownosci, zeby ich oszczedzono - polowa skrzyni. [S] Kiesa wsi
    w roku 2: swiat 7.2 mln (d365, mediana 7.6 tys.) -> 14.7 mln (d728, mediana 10.2 tys.; srednia 25.7 tys.) [P "Przeplywy osad (kiesy wsi)"]; Zachod jest spustoszony, wiec
    przyjmuje ok. 10-15 tys. na wies - najazd daje ok. 5-7 tys. Druzyna Wysp ok. 250 ludzi kosztuje ok. 1.8 tys. dziennie (6.5 zl zoldu + jedzenie) - polowa skrzyni sredniej wsi
    to 3-4 dni jej zoldu, bogatej - do 2 tygodni. Mniej niz 0.5 (np. 0.25) nie pokrywa nawet dni wyprawy; 1.0 oproznialoby wies jednym krokiem (nastepny najazd 0, AI
    wybieraloby zawsze nowe wsie, a gospodarka wsi wroga padalaby od jednego statku).
  - Qw = 0.10 ponad zapas 10 dni: nabrzeze portu to ludzie poza murami; ta sama podloga co danina korony - najazd nigdy nie bierze ludowi wiecej niz to, co korona
    uznaje za nadwyzke. Przy Lannisport (kiesa W 168 tys. w d728 [P `kiesa-ludu.csv`]) ok. 5-8 tys. na najazd, przy zamkach portowych (W 7-20 tys.) 0.3-1 tys.
- **Skutek na logu dwuletnim [S]:** dzis Wyspy ok. 10 sesji rabunku w roku 2 (1.35% z 776 sesji swiata [P RW 2.4]) i ok. 0.3 tys. zl lupu za CALY rok.
  Na najazd wedlug WW1 ok. 6.7 tys. (wies 5.5-6 + nabrzeze sredniego najazdu ok. 0.7 - co trzeci najazd z morza jak w B175: 12 z 47 [P]). Doby wojny w zasiegu rok 2 ok. 350
  (KP d365-423, Polnoc d434-728 + okresy KP [P RW 2.2]). Lup roku 2 wedlug czestosci najazdow:

  | sesji na dobe wojny | dzis (ok. 0.03) | 0.1 | 0.3 | 0.6 | 1.0 (sufit) |
  |---|---:|---:|---:|---:|---:|
  | lup WW1 tys. zl/d [S] | 0.2 | 0.6 | 1.8 | 3.6 | 6.0 |

  Sufit: skrzynie wsi Zachodu (33 wsie) i zachodniego wybrzeza Polnocy zarabiaja ok. 6-8 tys. dziennie [S: doplyw do kies wsi swiata ok. 160 tys. zl/d (utarg taborow 115,
  zakupy 22, lordowie 23 [P d728]) x udzial tych wsi 4-5%]; przy czestych najazdach lup schodzi do tego doplywu. Thralle: 10% trafionych do lochu napastnika z niewola (E4/9)
  - przy 0.3 sesji/d ok. 3-4 tys. nowych rocznie wobec dzisiejszego ubytku 1.3 tys. w 2 lata [P RW 2.6].
- **Kontrola calosci:** `SmallGold` "rabunek wsi z kiesy wsi" rosnie (oczekiwane); "lup z niczego" (Hearth E4/0) = 0 bez zmian; bilans v2 linii E7/R1 (zloto wodza - kiesa wsi
  - kiesa W) = 0; kiesa wsi nigdy < 0 (platnik przycina do stanu); E7/R2 okienko gracza pokazuje kwote z Q; czujnosc wsi (ceil(1 + 100 d)) bez zmian; Q dotyczy tez Wolnych
  Ludzi (najazdy na Dar) - ta sama regula dla obu ludow lupiezcow.
- **Linia:** `Zelazna cena (WW1): dzien N | sesji Wyspy X (z morza Y), Wolni Ludzie Z; zloto z kies wsi A (w tym ponad s: A1), z nabrzeza B (portow K, ponizej zapasu 0), towar C szt. (D zl); jency (thralle) J | ofiary wedlug krolestwa: ... | kiesy wsi wedlug krolestwa (stan): Zachod ..., Polnoc ..., Dar ... | kontrolki: kiesa < 0 = 0, nabrzeze ponizej zapasu = 0, bilans v2 = 0`.
  Dopisek do "Przeplywy osad (kiesy wsi)": rozbicie stanu na krolestwo (RW "nie wiem" - kiesy wsi Zachodu i Daru).
- **Prog:** WW-1 (kontrolki 0), WW-5 (Zachod / Polnoc - ryzyko).
- **Ryzyko dla swiata:** skrzynie wsi Zachodu i zachodniej Polnocy chudna - przy 0.3-0.6 sesji/d o 30-80% w rok [S]; korona KP i Polnocy bierze z nich mniej (korony biora
  z kies wsi swiata 134 tys. zl/d [P d728]); nabrzeze Lannisport i Kayce biednieje -> wiecej glodu E5/C2 w tych miastach -> chleb M3 placi korona KP (z jej 79 tys. zl/d
  wplywow - w wojnie korona karmi lud, ktory Wyspy rabuja; lancuch zamkniety). Wiecej spustoszenia (E4/5), uchodzcow i strat Wysp (ok. 8 ludzi na najazd [P RW 2.1]).

### 2.2 WW2 - lup na zalogi okretow

- **Klucz:** `ReaverLootToCrews` (bool, **on**; wymaga WW1). Off = zelazna cena trafia do jednorazowych i do D (W-1) jak caly lup dzis.
- **Regula.** Dla rodu AI z `ReaverRealms` z budzetem 166, nie Strazy bez zoldu (`b.Zero`), nie najemnika: zloto zelaznej ceny (przyrost WW1 ponad s) i dziesiecina korsarzy (WW4)
  tego rodu z ostatnich 28 dob / 28 = `b.Loot`. W `ClanBudget.Daily`: `Cap += b.Loot`, `PartyCap += b.Loot` **po** odjeciu zalog i karawan (lup nie dopelnia zalog), bez wejscia
  do D (dwor, sprzet, budowy, limit Banku go nie widza). `PartyCapOwn` = PartyCap - Aid zawiera `b.Loot` (to pieniadze rodu) - wiec potrzeba W5 spada o lup 1:1.
  Wolni Ludzie przy WL1/WL2 maja `b.Zero` - WW2 ich nie dotyczy (bez zoldu nie ma pulapu zoldu).
- **Dlaczego tylko NOWY lup (nie dzisiejsze 2.8 tys. jednorazowych):** dzis zalogi Wysp sa placone ponad wlasny pulap (14.9 wobec ok. 8), wiec ok. 0.53 kazdego denara jednorazowego
  (udzial pulapu 0.6 x f) nie zmienia zadnej wyplaty i zostaje w kiesie [S z K `Ceiling`, `ClanBudget.cs:610`]. Przesuniecie tych 2.8 tys. na druzyny wydaloby co dzien 1.5 tys. wiecej niz wplywa - kiesy rodow z lennem (0.34 mln) skonczylyby sie w ok. 7 miesiecy
  i wrocilby zalegly zold. Nowy lup wplywa i wychodzi w tej samej kwocie - kiesa stoi.
- **Kto -> komu:** nikt nowy - zloto juz jest w kiesie glowy (WW1/WW4); regula zmienia tylko, na co rod je wydaje: zold druzyn (sakiewki ludzi, kasy miast).
- **Liczby (decyzja):** 28 dob - to samo okno co D staly 169c; najazd daje lup jednym kawalkiem, okno rozklada go na miesiac zoldu. 1:1 (nie 0.6 x f jak D) - [L] zelazna cena
  jest dola zalogi, nie dochodem dworu.
- **Skutek [S]:** brak Wysp spada o caly lup WW1 + dziesiecine WW4 (0.6-7 tys. zl/d wedlug tabeli 2.1 i 2.4). Pamiec sesji: po wczytaniu okno rosnie od 0 przez 28 dob
  (bez nowego pola zapisu; ok. 1/2 lupu pierwszego miesiaca po wczytaniu nie podnosi pulapu - zostaje w kiesie).
- **Kontrola calosci:** W-1 (`ClanBudget.cs:369, 434` `d += once`) liczy jednorazowe BEZ kosza WW2 (inaczej podwojnie); W5 (PartyCapOwn), W5b regula 1 (brak sprzed M1 mniejszy
  -> nizsza stawka daniny ludu Wysp - mniejszy garnek; zamierzone: lud Wysp placi mniej, gdy panowie maja lup), W6 tarczowe (`ScutageCap` - wolny pulap) i M1 widza nizszy brak;
  zwolnienia 166 licza PartyCap z lupem (`capRel` jak przy Aid [K ClanBudget.cs:906]); nowa druzyna (`MinNewPartyMen`) - pulap z lupem; rod gracza bez budzetu - poza (jego lup
  jest jego).
- **Linia:** w "Budzet rodow (166)" odcinek `| lup na zalogi (WW2): rodow N, w oknie 28 dob X zl/d, podniesienie pulapu druzyn Y, zold druzyn tych rodow Z (wobec pulapu z lupem)`;
  w "Doplata wedlug potrzeby (W5)" w wierszu krolestwa `, lup WW2 X`; CSV `budzet-rodow.csv` nowa ostatnia kolumna `lup_na_zaloge_ww2`.
- **Prog:** WW-2 (cel), WW-4 (kiesy i zalegly zold Wysp).
- **Ryzyko:** rody Wysp werbuja wiecej ludzi do druzyn - wiecej ludzi z puli Wysp (450 tys. ludzi, FreeHands 40% [K]) i wiecej ich w polu; gdy lup ustanie (pokoj), pulap z lupu
  gasnie w 28 dob -> zwolnienia 166 z druzyn (jak dzis po koncu doplat). Nic dla reszty swiata.

### 2.3 WW3 - najazd z rachunku (AI z biezacego zysku)

- **Klucz:** `ReaverRaidByProfit` (bool, **on**), `ReaverRaidPayDays` (**4**). Off = oceny rabunku jak gra / SCAI / BK.
- **Regula.** Postfiks `CampaignEventDispatcher.AiHourlyTick` (Low; ten sam co WW0, precedens H1): dla partii lorda AI z `ReaverRealms` kazda opcja `RaidSettlement` w
  `PartyThinkParams.AIBehaviorScores` dostaje wynik x **M = 1 + min(2, E / (C x T))**, gdzie E = lup, ktory WW1 dalby dzis z tej wsi (Q x kiesa wsi + Q x wartosc magazynu,
  + nabrzeze, gdy wies ma port macierzysty, a partia jest na morzu), C = koszt dnia partii (zold `TotalWage` + jedzenie: ludzie / 20 x cena zboza najblizszego miasta),
  T = 4 dni. Jedna zmiana na tick (lista opcji jest swieza co godzine - mnoznik nie narasta). Opcji nie dodaje: wojna, zasieg, 8 dni jedzenia, druzyna (nie armia) i prog sily
  zostaja z gry [K AiMilitaryBehavior :157-200, :318-345; DefaultTargetScoreCalculatingModel :205-243].
- **Dlaczego tak:** postoj, patrol i oblezenie nie przynosza lupu, a zold placi sie i tak - kazdy najazd z lupem jest lepszy niz stanie (M >= 1, nigdy kara). Im wiecej lupu
  wobec kosztu wyprawy, tym mocniej: wies, ktorej skrzynia placi druzynie T dni, ma M = 2; bogata - 3 (gorna granica, zeby AI nie porzucala obrony wlasnych twierdz). T = 4: mediana
  dob na osade w kroku 1.91 [P linia E7/R1 d1021] + ok. 2 dni drogi tam i z powrotem. AI sama wybiera bogate wsie (a nie te, ktore wczoraj ograbila - E po WW1 spada o polowe).
  Dla Wolnych Ludzi C to prawie samo jedzenie (zold ok. 1 zl) - wies Daru z kilkoma tysiacami w skrzyni ma M ok. 2-3 [S]: "zyja z lupu".
- **Dlaczego nie na `GetTargetScoreForFaction`:** czynny model to SCAI, ktory dla wsi wola Default i zwraca wynik bez zmian [K scai.cs:1832, RW rozdz. 4] - postfiks na obu
  (jak `SiegeRansom.ScorePost`) mnozylby dwa razy; zerowanie jest bezpieczne, mnoznik nie. Postfiks ticku jest jeden.
- **Kto -> komu:** nic (tylko decyzja AI). Gracz wybiera sam (regula pieniedzy WW1 jest wspolna).
- **Skutek [S]:** dzis ok. 0.03 sesji na dobe wojny (ok. 10 w roku 2). W Q3 roku 1, gdy Wyspy w ogole rabowaly, ok. 0.2 sesji/d [P RW 2.1: 28 najazdow z walka w 91 dob, ok. 0.7
  sesji na najazd z walka wedlug swiata - 776 sesji / 1 113 najazdow]. Mnoznik 1.8-3 przy opcjach, ktore juz sa, to ok. 0.3-0.6 sesji/d - **o ile opcje rabunku w ogole sa**
  (po d405 nie wiem - WW0). Jesli WW0 pokaze, ze opcji nie ma (sila, jedzenie, armie), WW3 nic nie da i nastepny krok jest osobny.
- **Kontrola calosci:** H1 Strazy (ten sam punkt, inna frakcja - bez kolizji); BK `RaidingFactor x (1 + RaidIncentive)` dziala tylko na rabunek, ktory juz trwa [K BK 1626-1745] -
  inny etap; NavalDLC nie zmienia oceny rabunku [K NavalTargetScoreCalculatingModel]; SCAI liczy twierdze - bez zmian.
- **Linia:** w WW0 (`WW3: mnoznik mediana m, x3 n, opcji pomnozonych K`), wyjatek per partia - licznik potkniec.
- **Prog:** WW-3 (sesje Wysp na dobe wojny).
- **Ryzyko:** druzyny Wysp czesciej poza domem - slabsza obrona wlasnych twierdz (dzis 0 oblezen Wysp w 2 lata [P]); wiecej malych bitew z milicja Zachodu i Polnocy.

### 2.4 WW4 - port korsarzy (korsarze: wybor)

- **Wybor i uzasadnienie.** Dwie drogi z zadania: (a) bandy morskie oddaja Wyspom czesc lupu za schronienie, (b) wlasne partie piratow Wysp. **Biore (a).**
  (b) to nowe partie z ludzi i zlota Wysp - te same pieniadze i ci sami ludzie co druzyny lordow (WW1-WW3), tylko bez pana; kazdy ich lup bylby lupem rodow i tak.
  (a) ma prawdziwego platnika z zewnatrz (ofiary band: tabory i karawany Polnocy - w roku 2 Sea Raiders napadli 14 taborow i 11 karawan Polnocy [P RW 3]), istnieje juz na mapie
  (NavalDLC `northern_pirates` "Sea Raiders": strefy kolo Pyke, Old Wyk, Flint's Finger, Wyspy Niedzwiedziej [P/K RW 3]), nie wymaga wojny (dziala w pokoju, gdy WW1-3 nie
  dzialaja) i z Wyspami nie maja bitew (0 w B175 [P]). [L] Pyke jako przystan lupiezcow, panowie Wysp biora dole z lupu kazdej zalogi.
- **Klucz:** `SeaRaidersIronHarbour` (bool, **on**), `SeaRaidersHarbourTithe` (**0.25**). Off = paser i "zycie w miastach" band jak w 63aaaedf.
- **Regula.** Dla band klanu `northern_pirates`: (1) **paser** - lista paserow bandy (`OutlawLaw.Reach` [K OutlawLaw.cs:1815-1840]) dostaje miasta krolestwa Wysp z portem
  (dzis Lordsport, Lonely Light, Pebbleton [P settlements.xml]) w odleglosci do 600 jak miasto "przy bandzie" (dzialka bandy 0.50 - dom korsarza); bandy wybieraja najlepsza oferte
  jak dzis, wiec port Wysp wygrywa, gdy ma towar w cenie; dzialka pasera zostaje w kasie portu Wysp (jak dzis w kazdym miescie); (2) **dziesiecina** - z zaplaty pasera portu Wysp
  dla bandy 25% idzie do glowy rodu wlasciciela portu (gracz-wlasciciel - tak samo); (3) **zycie w miastach** (`OutlawLaw.LifeDay` [K :1859-1863]) - najblizszy port Wysp do 600
  zamiast najblizszego miasta.
- **Kto -> komu:** kiesa bandy (z kies ofiar - 3.2, i z towaru ofiar sprzedanego paserowi) -> kasa portu Wysp (dzialka, zycie) i glowa rodu pana portu (dziesiecina -> kosz WW2).
- **Liczby (decyzja):** 600 - Wyspa Niedzwiedzia lezy 501 od Pyke [P RW 2.2], strefy Sea Raiders koncza sie na niej; dalej (Braavos) port Wysp przestaje byc "domem". 25% -
  pan portu bierze mniej niz paser (dzialka bandy przy miescie 0.50, czyli paser bierze polowe), zeby port Wysp zostal najlepsza oferta dla bandy.
- **Skutek [S]:** paser swiata w d728: bandy dostaly 7.0 tys. zl, kasy miast 10.7 tys., bandy wydaly w miastach 28.4 tys. zl na dobe [P linia "Paser"]; udzial Sea Raiders
  - nie wiem (RW "nie wiem" 4), przyjmuje 5-15%. Do Wysp: dzialka 0.5-1.6, zycie 1.4-4.2 (do kas portow -> zawor do panow, lud -> danina P1 -> garnek W5),
  dziesiecina 0.1-0.25 tys. zl/d (do WW2). Na brak Wysp ok. 0.3-1.3 tys. zl/d [S: dziesiecina 1:1, reszta przez zawor i danine ok. 1/4].
- **Kontrola calosci:** `OutlawLaw.GatePirate` (narodziny piratow tylko z puli wyrzutkow) bez zmian; suma pasera swiata bez zmian (zmienia sie tylko miasto); kryjowki bez zmian;
  B175 / 3.2 bez zmian.
- **Linia:** w "Paser:" odcinek `| korsarze Wysp (WW4): band Sea Raiders z ladunkiem N, sprzedaly w portach Wysp X zl (dzialka w kasach portow Y), dziesiecina Z zl do panow portow (rodow K), zycie w portach Wysp W zl; band poza 600 M`;
  do linii "Wyrzutki" rozbicie band i ludzi wedlug klanu bandyckiego (RW "nie wiem" 4).
- **Prog:** WW-6 (INFO + kontrolka sumy pasera).
- **Ryzyko:** miasta Polnocy i Zachodu traca sprzedaz lupu band morskich i ich wydatki (kilka tys. zl/d kas miast); porty Wysp maja wiecej zlota w kasach (zawor do panow Wysp).

### 2.5 Karawany, statki, kupcy - bez nowej reguly (dlaczego)

- **Karawany i tabory:** rozbita karawana / tabor oddaje zwyciezcy CALA kiese (3.2 [K BeatenPurse]) - zelazna cena w pelni juz jest. Brak sposobu "polowania" w AI gry
  (silnik nie ma takiej opcji w `PartyThinkParams`) - nie dopisuje opcji, ktorej gra nie zna (CLAUDE.md 8.3). Pomiar: WW1 rozbija 3.2 Wysp osobno.
- **Statki (pryzy):** port placi zwyciezcom za zatopione statki (181 [K ShipsToPort181]); Wyspy wygraly 3 bitwy morskie w 2 lata [P] - zmiana ceny nic by nie dala.
- **Kupcy (kasy portow):** nadwyzka kas portow placi juz pryzy i statki (181), ponizej nadwyzki jest zapas kupcow na skup od wsi - ruszenie go zatrzymaloby targi wroga dla
  kilku tysiecy zl. Nie ruszam.

### 2.6 Rachunek celu Wysp i pytanie do Jeffa (WW-J, bez kodu)

| scenariusz (sesji na dobe wojny) | lup WW1 | WW4 | brak rok 2 (z ok. 20.4 po M1) [S] | % dzisiejszych 23.9 |
|---|---:|---:|---:|---:|
| bez WW (63aaaedf) | 0 | 0 | ok. 20.4 | 85% |
| 0.1 (WW3 nie dziala) | 0.6 | 0.3 | ok. 19.5 | 82% |
| 0.3 (srodek) | 1.8 | 0.7 | ok. 17.9 | 75% |
| 0.6 | 3.6 | 1.0 | ok. 15.8 | 66% |
| 1.0 (sufit kies wsi) | 6.0 | 1.3 | ok. 13.1 | 55% |

**Wniosek:** cel 1/3 (<= 8.0) z samego lupu jest **nieosiagalny w obiegu zamknietym** - nawet przy najazdzie co dobe brakuje ok. 5 tys. zl/d, bo na wybrzezu w zasiegu
nie ma wiecej zlota (skrzynie wsi Zachodu i zachodniej Polnocy: doplyw ok. 6-8 tys./d [S]). Wieksze Q albo wiecej platnikow (lud miast za murami, kasy kupcow) zabieraloby
Zachodowi, ktory juz dzis ma dochod ludu 29 tys. zl/d (z 109 [P RW 7]), wiecej niz polowe - tego nie projektuje bez Twojej zgody.
**WW-J (pytanie, kodu nie pisze - precedens M4 w PW 3.8):** [L] zelazni ludzie nie biora zoldu, plyna po zelazna cene. Wariant: w druzynach lordow Wysp zold jednostki = 0.5 x
nominal (zalogi zamkow - pelny), druga polowa to ich dola z lupu (WW1 idzie wtedy w polowie do sakiewek ludzi, w polowie do pana). Potrzeba W5 druzyn Wysp spada z ok. 31 do 15.6
tys. zl/d -> brak ok. 4-8 tys. (cel osiagniety bez wzgledu na liczbe najazdow). To zmienia Twoja decyzje (Wyspy "maja kase z rabunku", nie "biora mniej") - dlatego pytam.

---

## 3. WOLNI LUDZIE - reguly

### 3.1 WL1 - Wolni Ludzie bez zoldu

- **Klucz:** `FreeFolkUnpaid` (bool, **on**; wymaga `ClanBudgetEnabled`). Off = WL placa zold jak w 63aaaedf.
- **Wybor: 0, nie "znacznie mniej".** (1) Twoja pierwsza wersja ("nie pobieraja kasy"); (2) [L] Wolni Ludzie nie znaja monety, walcza dla przetrwania (RWL 5); (3) regula 182
  Strazy jest sprawdzona w dwuletnim biegu - te same miejsca w kodzie, jedna regula "sluzba bez zoldu"; (4) wariant x1/3 zostawia wojsko na smyczy zlota: pulap rok 2 7.5 tys. /
  (3.3 x 1/3) = ok. 3.4 tys. ludzi, mniej niz pula WL2 i dalej "za kase" [S z RWL 6.2].
- **Regula.** Jak paczka 182 [K MountedWage.cs:79, CrownGifts.cs:72-75]: zold kazdej jednostki (nie bohatera) w partii lorda, **zalodze** i **druzynie gracza** frakcji `freefolk` = 0
  (tylko zold partii; cena werbunku i awansu - nominal). Rycerze 179 w druzynach WL - 0 jak u Strazy [K GentryService.cs:169, 328]. Minimum BK (1 zl na czlowieka, w zalodze ok. 0.5
  [K BKPartyWageModel.cs:70, P RWL 1.2]) zostaje - jak u Strazy. W kodzie: `IsWatchParty` -> `IsUnpaidParty` (Straz zawsze przy `WatchUnpaid`, WL przy `FreeFolkUnpaid`);
  `IsWatch` (dary koron 182) zostaje tylko dla Strazy.
- **Kto -> komu:** nic nowego; rody WL przestaja placic ok. 7.6 tys. zl/d (rok 2: 11.2 -> ok. 3.6 przy wiekszym wojsku WL2).
- **Skutek [S z P]:** zold rok 2 11.2 tys. zl/d -> minimum BK ok. 3.6 tys. (2.1 tys. ludzi w druzynach x 1 zl + 3.0 tys. w zalogach x 0.5); brak na zold WL 20.7 -> 0
  (b.Zero - poza W5); zalegly zold (2.0-3.6 tys./d przycinany [P]) i dezercja z niego (WarLedger 0.5%/d) znikaja; kara morale za zalegly zold gry znika. Kiesy rodow WL (1.31 -> 0.19 mln
  w BD) przestaja topniec: D 5.6 + renty bez W5 (korona WL nie placi juz 2.0 tys. W5 - idzie w renty) - dwor, sprzet, budowy (ok. 0.47 D) - minimum BK - jedzenie druzyn ok. 0.
- **Kontrola calosci:** Straz - te same liczby co w 63aaaedf (kontrolka WL-4); `b.Zero` wylacza WL z W5, W5b, W6, M1, W7, kredytu 168, chleba pana M3 i W6b gracza
  [K ClanBudget.cs:285, 304, 320, 338, 360] - zamierzone (bez zoldu nie ma czego doplacac); lud WL traci czesc zarobku "z wojska" (1.8 z 5.05 tys. zl/d [P RWL 2.1]) - ile, nie wiem
  (RWL "nie wiem" 8); danina P1 ludu WL (2.0 tys.) moze spasc; korona WL dalej placi chleb M3 swoim miastom.
- **Linia:** w "Straz i Wolni Ludzie (4.15)" odcinek `| WL bez zoldu (WL1): zold druzyn X (ludzi M, zl/czl), zalog Y (ludzi N, zl/czl), zalegly zold 0, dezercja z zoldu 0`.
- **Prog:** WL-1.
- **Ryzyko dla swiata:** lud WL biedniejszy o wydatki zolnierzy (najwyzej ok. 1-1.5 tys. zl/d) - wiecej glodu w obozach WL (chleb M3 z korony WL).

### 3.2 WL2 - granica wojska z ludu (i co z zalogami)

- **Klucz:** `FreeFolkMenFromPeople` (bool, **on**; wymaga WL1), `FreeFolkLevyShare` (**0.018**), `FreeFolkGarrisonPoolShare` (**0.50**). Off = przy WL1 pula liczona ze zlota
  jak u Strazy (wtedy ok. 0.8 tys. ludzi na starcie - RWL 4.1; dlatego WL2 domyslnie wlaczone).
- **Regula.** Rody WL dostaja `b.Zero` (jak Straz: zwolnien 166 brak - `WatchNoReleases`; poza doplatami), ale **pula w ludziach calego krolestwa WL** = LevyShare x ludzie regionow
  krolestwa WL (ksiega ludzi - ta sama liczba co `ludzie-regiony.csv`, liczona raz na dobe), nie ze zlota. Pula zatrzymuje werbunek druzyn (`CheckRecruiting`), nowe partie
  (`SpawnLordParty`) i przyrost zalog [K ClanBudget.cs:1300-1320, 1397] - te same bramki co u Strazy. **Zalogi:** zaloga WL rosnie tylko, dopoki zalogi WL razem < 50% puli;
  zalogi ponad to zostaja (bez zwolnien), tylko nie rosna. Druzyny dostaja reszte puli.
- **Liczby (decyzja):** 1.8% - tyle wojska WL mialy, gdy pieniadze ich jeszcze nie hamowaly: Q1 4.7 tys. (1.57% z 299.6 tys.), Q2 5.8 tys. (1.97%) -> srednio 1.8% [P RWL 2.2].
  Pula idzie za ludem: gdy WL traca ludzi (Straz, Inni, glod), pula maleje sama. 50% zalog - w roku 2 zalogi zjadaly 72-85% pieniedzy WL [P RWL 2.2], a [L] Wolni Ludzie walcza
  w polu, nie w murach; polowa puli zostaje na druzyny. Ludzie nie hamuja (pula walczacych ok. 81 tys. [P RWL 2.1]); limit druzyn gry (5.2-5.8 tys.) nie wiaze przy 2.1 tys.
- **Skutek [S z P]:** pula 1.8% x 279-286 tys. = 5.0-5.15 tys. (rok 2; dzis WL maja 3.6-3.8 tys.); zalogi 3.0 tys. (ponad 50% - stoja), druzyny 0.7-0.84 -> ok. 2.0-2.2 tys.
  Bieg 120 dob: pula 1.8% x ok. 300 tys. = 5.4 tys. od startu (dzis Q1 4.7 tys.).
- **Kontrola calosci:** pula Strazy liczona dalej ze zlota (jedna pula na krolestwo - osobna dla Strazy i dla WL; kod puli Strazy `_watchPoolCap` nie zmienia liczby); gracz w WL -
  bez budzetu 166, wiec bez puli (jak gracz w Strazy - tylko limit druzyny gry); `MinNewPartyMen` (30) z puli WL; "na sucho" linia 166 pokazuje pule WL.
- **Linia:** w "Budzet rodow (166)" odcinek `| Wolni Ludzie w ludziach (WL2): pula P = 1.8% x ludzie L; ludzie M (druzyny D, zalogi Z = z% puli); werbunek wstrzymany: pula a, zalogi 50% b, jedzenie (WL3) c; nowe partie wstrzymane d`.
- **Prog:** WL-2.
- **Ryzyko dla swiata:** druzyny WL x3 w polu - wiecej bitew ze Straza (Straz 2.5 tys. po buncie d512 [P RWL 1.2]); Castle Black i Eastwatch moga byc oblegane (dzis WL nie oblegaja);
  wiecej zabitych WL = wiecej trupow dla nekromancji Innych (ROT); Inni bili WL 105 karawan i 70 druzyn w roku 2 [P]. WL dalej wojuja tylko ze Straza (ROT "Free Folk May Not War") -
  Polnoc bezpieczna.

### 3.3 WL3 - werbunek tylko z jedzeniem (zycie z lupu)

- **Klucz:** `FreeFolkRecruitNeedsFood` (bool, **on**; wymaga WL1). Off = werbunek WL bez warunku jedzenia.
- **Regula.** Druzyna lorda AI WL werbuje (`CheckRecruiting`) tylko, gdy po werbunku ma jedzenie na **8 dni** (zapas / dzienne zuzycie ludzi po werbunku). Prog = `NeededFoodsInDaysThresholdForRaid`
  gry (czytany z modelu, nie wpisany na sztywno) - jedna liczba, jedno znaczenie: wodz bierze tylu ludzi, ilu moze zabrac na najazd.
  Jedzenie i zloto na nie WL biora z lupu, ktory juz jest: spichlerz okregu w czasie rabunku (K4 - zboze rowne zuzyciu napastnikow [K DistrictRaid.cs:35]), zelazna cena z kiesy
  wsi Daru (WW1 - Q dotyczy tez WL), kiesy rozbitych (3.2) i lup bitew (A6); zakup jedzenia za zloto wodza [K PartiesBuyFoodCampaignBehavior.cs:67] - bez zoldu wodzowie maja na to pieniadze.
  WW3 pcha druzyny WL do najazdow na Dar (M prawie 3 przy zoldzie ok. 1 zl).
- **Dlaczego:** bez zoldu jedynym hamulcem poza pula sa glod i dezercja z morale (DesertionLaw) - bez WL3 AI werbowaloby glodnych, ktorzy potem uciekaja albo gina (marnotrawstwo ludzi).
  [L] wodz za Murem prowadzi tylu, ilu wyzywi.
- **Kto -> komu:** nic (bramka werbunku).
- **Skutek [S]:** nie wiem, czy druzyny WL dzis gloduja (RWL "nie wiem" 3); regiony WL maja zapas 120-130 tys. i bilans +0.45 tys. dziennie [P RWL 2.1] - 2.1 tys. ludzi w druzynach jedza ok. 105 szt.
  dziennie, wiec jedzenie w krainie jest; bramka wiaze wodzow bez zlota i bez lupu.
- **Linia:** w odcinku WL2 (`jedzenie (WL3) c`) + w "Dezercja AI (183)" `w glodzie` wedlug krolestwa (RWL "nie wiem" 3).
- **Prog:** WL-3.
- **Ryzyko:** wolniejszy przyrost druzyn WL zima (od d460 jedzenie partii +42.5% [P RW 8]) - zamierzone.

### 3.4 Nocna Straz - bez zmian

Paczka 182 (`WatchUnpaid`, `UnpaidTroopsCapInMen`, `WatchNoReleases`, dary koron) zostaje co do zlotowki: pula Strazy ze zlota, dary tylko dla Strazy, H1 (Straz nie pali wsi).
Zmiana w kodzie to tylko uogolnienie `IsWatchParty` -> `IsUnpaidParty` i `b.Zero` z osobnym zrodlem puli dla WL. Kontrolka WL-4 porownuje liczby Strazy z baza.

---

## 4. Progi (grupa `wyspywolni` w `tools/sprawdz_logi.py`) i plan testu

**T1 - rok 2:** zapis `autotest-rok2-d780-fin24` + 60 dob (781-840) na tej galezi; **baza = osobny bieg na 63aaaedf** na tym samym zapisie (`--baza-doby`). W d780 Wyspy sa w wojnie
z Polnoca (fabula) i z KP (porozumienie Dorzecza) - najazdy moga byc; WL w wojnie ze Straza. Bez bazy progi "x baza" - INFO.
**T2 - rok 1:** nowa kampania 120 dob. Wyspy tylko w wojnie z Dolina (poza zasiegiem [P RW 2.2]) - WW1-WW3 oczekiwane 0 (bez lupu nie ma czego rozliczac; WW4 dziala); WL od d1.
Rok 1 bez pogorszenia: R2-T2-1 i R2-T2-2 z grupy `rok2` (WL poza W5 - miara W5-7 swiata spada o udzial WL, ok. 1.3 tys. w d93-120 [S] - prog liczony bez WL).

| prog | co | T1 | T2 |
|---|---|---|---|
| WW-0 | linie WW0, WW1, WW2 (166), WW4 (Paser), WL1 (4.15), WL2 (166); `ERROR in` WW*/WL* = 0 | TAK | TAK |
| WW-1 | kontrolki WW1: kiesa wsi < 0, nabrzeze ponizej zapasu 10 dni, bilans v2 | 0 / 0 / 0 | 0 / 0 / 0 |
| WW-2 | **(cel)** brak Wysp po M1 (niepokryte W5, sr. 28 dob, d813-840) | <= 8.0 TAK; 8.0 - baza INFO "cel nieosiagniety" -> WW-J do Jeffa; > baza NIE | INFO |
| WW-3 | sesje rabunku Wysp na dobe wojny w zasiegu (WW0/WW1) | >= 3 x baza (bez bazy >= 0.1) | INFO (0 - brak wroga w zasiegu) |
| WW-4 | rody Wysp z lennem: G na koniec >= 0.9 x baza; zold przyciety <= baza + 1 tys.; lup WW2 wydany na zold druzyn >= 0.8 x lup | TAK | INFO |
| WW-5 | ryzyko: kiesy wsi Zachodu i Polnocy (WW1 rozbicie) >= 0.5 x baza; glod E5/C2 miast Zachodu <= baza + 1; chleb M3 korony KP | TAK / TAK / INFO | INFO |
| WW-6 | korsarze: dziesiecina i dzialka w portach Wysp; suma pasera swiata = suma bez rozbicia (kontrolka); bitwy Wyspy - Sea Raiders | INFO / 0 / INFO | INFO / 0 / INFO |
| WL-1 | zold WL na czlowieka: druzyny <= 1.1 zl, zalogi <= 0.6 zl; zalegly zold WL = 0; dezercja z zoldu WL = 0 | TAK | TAK |
| WL-2 | ludzie WL <= 1.05 x pula; pula = 1.8% x ludzie (kontrolka +-1); zalogi WL <= max(stan startu, 0.5 x pula); druzyny WL >= 2 x baza | TAK | TAK (druzyny INFO) |
| WL-3 | druzyny WL w glodzie (183) <= baza; werbunek wstrzymany z braku jedzenia | TAK / INFO | TAK / INFO |
| WL-4 | Straz bez zmian: pula Strazy, zold na czlowieka, dary - jak baza (+-2%); twierdze Strazy na koniec | TAK / INFO (DECYZJA przy stracie) | TAK / INFO |
| WL-5 | Inni: "UMARLI ... trupow w polu" (CS) na koniec <= 1.2 x baza | INFO (DECYZJA powyzej) | INFO |

Polecenia (sesja glowna; logi z `Modules\Armoury\`, gdzie jest katalog `Logs`):
```
python -I tools/sprawdz_logi.py --grupa wyspywolni --start-kampanii 108836 --do-doby 840 --baza-doby <log bazy 63aaaedf na zapisie d780> <log T1>
python -I tools/sprawdz_logi.py --grupa wyspywolni --do-doby 120 <log T2>
python -I tools/sprawdz_logi.py --grupa rok2 --do-doby 120 <log T2>        (R2-T2-1/2 - rok 1 bez pogorszenia)
```

---

## 5. Wdrazanie i wycofanie

Jeden DLL (jeden test T1 i jeden T2), kazda regula z wlasnym kluczem; wszystkie nowe klucze w `Settings.cs` + `tools/gen_mcm.py`. W `Armoury.json` Jeffa ich nie ma (nowe) - domyslne zadzialaja
(CLAUDE.md 7). Nowych pol zapisu brak (WW2 - okno 28 dob w pamieci sesji; pula WL - liczona co dobe). Reset w konstruktorze `ArmouryBehavior`. Wyjatek per obiekt - licznik potkniec,
bez globalnych wylacznikow.
- **NIE w WW-5** (Zachod / Polnoc za biedne): `ReaverWaterfrontShare` 0.10 -> 0, potem `ReaverIronPriceShare` 0.50 -> 0.30, potem `ReaverRaidByProfit` off.
- **NIE w WW-4** (kiesy Wysp, zalegly zold): `ReaverLootToCrews` off.
- **NIE w WL-4 / WL-5** (Straz traci twierdze, Inni rosna): `FreeFolkLevyShare` 0.018 -> 0.014 (Q8 dzis), potem `FreeFolkMenFromPeople` off razem z `FreeFolkUnpaid` (WL1 bez WL2 daje pule ze zlota ok. 0.8 tys. - nie wlaczac osobno).
- **NIE w R2-T2-1** (rok 1 gorszy): w d1-120 dzialaja tylko WL1-3 i WW4 - wylaczac WL3 -> WL2+WL1 -> WW4.

---

## 6. Czego nie wiem (+ linia diagnozy)

1. **Dlaczego Wyspy po d405 i WL po Q1 nie rabuja** (RW 8.1, RWL 7.1). Od tego zalezy, czy WW3 cokolwiek zmieni. Linia WW0 (opcje rabunku, wynik, jedzenie < 8, armie, wybrane zachowanie).
   Gdy przyczyna = prog sily (sila krolestwa wroga w ocenie), jedzenie albo armie - osobny projekt po T1, nie teraz.
2. **Kiesy wsi Zachodu, Polnocy i Daru** - linia "Przeplywy osad" ma tylko swiat; szacunek 10-15 tys. na wies Zachodu. Diagnoza: rozbicie stanu na krolestwo (dopisek WW1).
3. **Ile jest band Sea Raiders i ile sprzedaja** (RW 8.4) - WW4 szacuje 5-15% band swiata. Diagnoza: rozbicie "Wyrzutki" i "Paser" na klan bandycki (dopisek WW4).
4. **Prawdziwy prog zasiegu** (RW 8.2) - jednorazowa linia WW0.
5. **Czy druzyny WL gloduja i ile jedzenie kosztuje ich wodzow** (RWL 7.3) - linia "Dezercja AI (183)" `w glodzie` na krolestwo + odcinek WL3.
6. **Ile ze "z wojska" ludu WL to zold** (RWL 7.8) - skutek WL1 dla ludu WL; pomiar: `kiesa-obieg.csv` z_wojska WL wobec bazy.
7. **Czy M1 naprawde zmniejsza brak Wysp o 3.5 tys.** (PW rec. 8.3 - nie zmierzone) - punkt wyjscia 20.4 jest [S]; T1 z baza 63aaaedf daje prawdziwy punkt.
8. **Czy wieksze druzyny WL zaczna oblegac Straz** - AI WL nigdy nie oblegalo; WL-4 i KRONIKA WOJEN.

---

## 7. Wdrozenie (kod, 14.10) - galaz `w-toku/wyspy-wolni`, tor L, status DO SPRAWDZENIA (build kod 0, gen_mcm; bez testu w grze; nic nie wgrane)

Commity (od 63aaaedf): WW0 pomiar, WW1, WW2, WW3, WW4, WL1, WL2, WL3, narzedzie (`--grupa wyspywolni`). Kazda regula z kluczem (off = 63aaaedf), wpis CHANGELOG.
Klucze (wszystkie nowe, w `Armoury.json` Jeffa ich nie ma - domyslne zadzialaja): `ReaverIronPrice` (tak), `ReaverIronPriceShare` 0.50, `ReaverWaterfrontShare` 0.10,
`ReaverLootToCrews` (tak), `ReaverRaidByProfit` (tak), `ReaverRaidPayDays` 4, `SeaRaidersIronHarbour` (tak), `SeaRaidersHarbourTithe` 0.25, `FreeFolkUnpaid` (tak),
`FreeFolkMenFromPeople` (tak), `FreeFolkLevyShare` 0.018, `FreeFolkGarrisonPoolShare` 0.50, `FreeFolkRecruitNeedsFood` (tak). WW-J - bez kodu (pytanie do Jeffa).

Odstepstwa od tekstu projektu (z powodem):
1. Nabrzeze WW1: zdjecie z kiesy ludu ksiegowane w `_ctlOut` (wydatek kies - jak lup L2 i pobor W7), nie `_ctlIn`; miara zapasu takze bez oferty nadwyzki na jutro (jak `LevyRoom` W7 - najazd nie bierze tego, co lud juz postanowil wydac).
2. WW2 wymaga WW1, WW3 wymaga WW1 (E i kosz to zelazna cena). Kredyt wojenny 168 pozycza tylko brak ponad pulap i lup; `OnceToday` (168) bez kosza WW2; gracz-wasal (W6b) - pulap W5/W6 z jego oknem lupu (ta sama regula). Dwor i niewydany sprzet licza zold bez czesci z lupu (dwor jak bez lupu).
3. WW4: porty Wysp sa "domem" korsarza w obie strony pasera (sprzedaz lupu i zakup sprzetu - `Reach` jest wspolny). Rozbicie band na klan bandycki - w linii "Paser:" (nie "Wyrzutki:").
4. WL3: "po werbunku" = z kolejnym czlowiekiem (gra nie mowi z gory, ilu dobierze; jedno wywolanie moze dobrac kilku, nastepne widzi ich zuzycie).
5. WW0 "w zasiegu" = rozne wsie w opcjach rabunku (gra daje opcje tylko w zasiegu 5x).
6. Narzedzie: WW-5 glod E5/C2 - liczba miast swiata (linia nie ma rozbicia na krainy); WL-2b - ludzie w druzynach lordow WL z `budzet-rodow.csv` (`ludzi_partie` - jest tez w 63aaaedf); WL-5 - tylko INFO z `--crashscribe`.
