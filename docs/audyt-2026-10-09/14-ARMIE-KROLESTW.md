# Audyt 14 - Armie krolestw: jednostki, balans i zgodnosc z lore (09.10.2026, tylko odczyt; wersja 2 po krytyce)

Oznaczenia: **[K]** kod (plik/klasa), **[D]** dane XML gry i modow, **[D-wzorzec]** dane wzorca jednostki, ktorych gra w bitwie nie musi uzyc (np. kon - patrz 1.0),
**[P]** pomiar z logu autotestu 120 dob (kopiaT9-120, 08.10, doby 108836-108955, bez gracza w wojnach), **[S]** szacunek z rachunku, **[L]** lore z ksiazek
G.R.R. Martina, "The World of Ice & Fire" (TWOIAF), "Fire & Blood" i wypowiedzi Martina (rozdzial w nawiasie), **[L-pol]** polkanoniczny podrecznik d20 z 2005 r.,
**[H]** prawdziwa historia (zrodlo w nawiasie), **[ocena]** moj wniosek.
Nic w grze, w kodzie ani w ustawieniach nie zostalo zmienione. Gra nie byla uruchamiana.

Wersja 2 (09.10, po krytyce 29 uwag): przeliczone bitwy (tylko lord z lordem; poprawione rozpoznanie Aegona), tier i konni tylko dla partii lordow, wszystkie
szablony rodow (wczesniej 6 najczestszych - brakowalo Umberow, Mormontow i Cerwynow), wagi = ludzie w partiach, nowy wskaznik Sily (bron, ktora jednostka nosi;
kusza porownywana z kusza; szarza slonia ucieta) w dwoch wariantach: **w grze** i **wedlug XML ROT (bez naszego SkillSinew)**. Odpowiedzi na kazda uwage - rozdzial 8.

Zrodla robocze (scratchpad sesji 7016f733): `armie/dane.md`, `armie/lore.md`, `armie/jednostki.csv` (1216 jednostek), `armie/szablony.csv`;
skrypty tej wersji `armie14r/narzedzia/` (kultury2.py - Sila, krolestwa2.py - sklad i bitwy, sila_koszt2.py - sila wzorca i koszt, podbicia.py - co podbija
umiejetnosc, tabele14r.py i porownanie2.py - tabele); wyniki `armie14r/*.json`.

---

## 0. Dla Jeffa

1. Gdy AI bije sie z AI (autobitwa), gra liczy tylko liczbe ludzi i ich tier - pancerz, bron i umiejetnosci nie maja znaczenia (tak jest w kodzie gry); silniejsza strona wygrala 98 na 100 bitew lordow, ale glownie dlatego, ze AI wybiera bitwy, w ktorych ma zwykle 3 razy wiecej sily - w bitwach wyrownanych silniejszy wygrywa 88 razy na 100.
2. Poprzednia wersja liczyla jako "bitwy krolestw" takze napady na wiesniakow i karawany, wiec ranking "kto wygrywa wojny" byl falszywy - teraz bitwy lord z lordem i stracone wsie/karawany sa osobno (np. Dothrakowie wygrywaja 67% bitew lordow, a nie 30%).
3. Jakosc zolnierzy czujesz tylko w bitwach, w ktorych sam walczysz, i tu najwazniejsze odkrycie: wiekszosc roznic w jakosci miedzy krainami robi nasza zasada "umiejetnosc do wlasnego sprzetu" (SkillSinew), bo tarcze rodowe tieru 6 podnosza piechocie miecz do 175, a belty tieru 6 podnosza kusze do 175 nawet rekrutom tieru 2.
4. Wedlug samych danych ROT 92 na 100 grup jednostek miesci sie w 90-110 na 100 sredniej swiata (skrajne 85-109), a w grze, po naszej zasadzie, juz tylko 82 na 100 (skrajne 73-125).
5. Za mocni w grze sa przez to Pentos (piechota 125, a wedlug ROT 108), Qarth (116 / 104), Braavos (113 / 101) i Zlota Kompania Aegona (115 / 103), za slabi Dothrakowie (piechota 75, jazda 73), Nocna Straz (74) i piechota Myr (78).
6. Zold zalezy tylko od tieru i konia (konny placi 1,5 raza wiecej), wiec zadna kraina nie jest "tania" ani "droga" na tier - roznice robi sklad armii.
7. Polnoc nie ma dzis "swietnej piechoty" w jakosci (98 na 100), ma ja w liczbie: najwiecej ludzi w polu (11,9 tys. u lordow), a w bitwie jej wojsko to najpewniej w wiekszosci piechota, bo rekruci walcza pieszo (log nie rozroznia piechoty od lucznikow), jazdy ma tylko 4,5%, a docelowy sklad ROT ciagnie dojrzale partie do przewagi lucznikow.
8. Konnicy w polu jest wszedzie malo (u lordow AI 12,5% w 1. dobie i 8,2% w 120.), a glownym zrodlem jezdzcow AI sa najemnicy konni z karczm (8 617 w 120 dob, coraz mniej) - ile awansow na jezdzca odpadlo z braku konia, jeszcze nie zmierzylem i proponuje to zmierzyc, zanim cokolwiek zmienimy.
9. Najwieksze rozjazdy z ksiazkami: Dothrakowie (w docelowym skladzie 72% pieszych), Zelazne Wyspy (21% jazdy), Norvos (41% jazdy), Volantis (52% strzelcow i do 3 sloni bojowych w szablonie kazdego lorda), a dobrze pasuja Westerlands, Dorne, Nocna Straz, Wolni Ludzie z gigantami, Nieskalani i Zlota Kompania.
10. Propozycje najpierw zmieniaja sklad wojsk (dziala tez w autobitwach), a jakosc - przez tier tarcz, beltow i broni we wzorcach, a nie przez obnizanie umiejetnosci, ktore nasza zasada i tak przywroci; pomysl "rycerz z wlasnym koniem" wycofalem, bo dawal konia z niczego wbrew Twojej decyzji z 30.08.
11. Na koncu jest 7 pytan o zasady gry i kanon (rozdzial 5), m.in. o "swietna piechote" Polnocy, konia rycerza, Dothrakow pod murami i slonie Volantis.

---

## 1. Jednostki kazdej krainy i sklad wojsk AI

### 1.0 Jak czytac tabele i skad sie biora zolnierze

**Kolumny tabel jednostek [D]+[K]:**
- **T** - tier w grze: min(max(ceil((poziom-5)/5), 0), 6) (`DefaultCharacterStatsModel.GetTier`).
- **Rodzaj** - P piechota, S strzelcy, J jazda, KL konni lucznicy; z `default_group` w XML (z tego gra bierze formacje i to, czy zolnierz jest "konny").
- **Skad** - `wies` = drzewo zwyklego rekruta kultury (ochotnicy u notabli), `szlachta` = drzewo szlacheckie (wsie przy zamkach), `rod X` = drzewo z szablonu rodu
  (podmiana ROT dla lordow AI), `kasztelan` = oddzial domowy do kupienia tylko przez gracza, `milicja` = obsada milicji osad, `najemnicy X` = kompania najemna ROT.
  W tabelach sa wszystkie jednostki z droga do wojska (1034 wiersze); pominieci sa tylko straznicy karawan, bandyci i jednostki bez drogi w danych
  (m.in. najemnicy z karczm - patrz "Zold").
- **Umiejetnosci w grze** - wartosci PO naszym `SkillSinew` (CrashScribe), ktory podnosi kazda umiejetnosc do najwyzszej trudnosci wlasnego sprzetu jednostki
  i nigdy jej nie obniza ([K] `Mends.SkillSinew`, Mends.cs:2403-2479, "raise, never lower", bez sufitu). Trudnosc sprzetu ustawiaja nasze prawa tieru:
  bron, tarcze i amunicja 35 punktow na tier (`Mends.WeaponTierLaw`, :2348-2399 - tarcza i belt tieru 6 = 175), pancerz - Prawo Wagi i prawo tieru pancerza.
  Tarcza wymaga w grze umiejetnosci **Jednorecznej** (`WeaponComponentData.GetRelevantSkillFromWeaponClass`: SmallShield/LargeShield -> OneHanded), belty - Kuszy.
  Skroty: 1H, 2H, Dr (drzewce), Luk, Kusz, Rzut, Jaz (jazda konna), Atl (atletyka). Gwiazdka `*` = podbite o 50 lub wiecej ponad XML ROT;
  `*T` = podbite przez **tarcze**, `*A` = przez **amunicje** (belty/strzaly), sama `*` = przez bron albo pancerz (`armie14r/podbicia.json`).
  Pokazana jest umiejetnosc broni, ktora jednostka NOSI: piechota - bron wrecz i Atletyka (plus Rzut, gdy nosi bron rzucana), strzelcy - luk/kusza, wrecz, Atletyka,
  jazda - Jazda i bron wrecz, konni lucznicy - Jazda i strzelanie. Pelne 8 umiejetnosci jest w `armie/jednostki.csv`.
- **Pancerz** - suma glowa + korpus + rece + nogi, srednia z zestawow, wartosci po RBM i naszych prawach (zrzut z gry `items-dump.csv`).
  Dla skali: srednia swiata piechoty to 74 / 146 / 270 / 346 / 440 / 482 dla tierow 1-6.
- **Uzbrojenie** - najczestsze klasy broni w zestawach; przy luku i kuszy liczba = naciag w funtach (pod RBM obrazenia lukow sa w danych stale, naciag siedzi
  w polu predkosci pocisku). "tarcza" = we wszystkich zestawach, "tarcza 50%" = w polowie. W bitwie DTE rozdaje zbroje i bron z zbrojowni partii: najpierw szuka
  sztuki z wzorca, potem najblizszej skutecznoscia ([K] DTE `PartyEquipmentDistributor.cs`:1262-1285).
- **Kon (wzorzec) [D-wzorzec]** - kon z wzorca XML: nazwa (skrocona) i szybkosc / szarza; "(egz.)" = slon, mamut albo jednorozec. **To nie jest kon, na ktorym
  jednostka walczy:** DTE sadza na koniu wylacznie konmi ze zbrojowni partii, najlepsze konie (wedlug tieru i wartosci) dostaja jednostki konne w kolejnosci tieru,
  a na konia z wzorca w ogole nie patrzy ([K] `PartyEquipmentDistributor.AssignHorseAndHarness`, :800-830, sortowanie :731). Lord AI przy awansie na jezdzca
  oddaje najtanszego konia z kategorii wynikajacej z tieru celu (patrz nizej). Skala koni: Dothraki Palfrey 54/16, Sand Steed 60/18, Western Courser 58/30,
  Andalos Destrier 68/36, Khal's Steed 70/30, slon 20/350, mamut 20/400, jednorozec 55/80. Kon `Hunter` (55/7) to kon bojowy RBM w innej skali
  (RBMCombat_horses.xml:449: trudnosc 60, zwrotnosc 65, +150 punktow zycia) - ma najslabsza szarze, ale 3 razy wiecej zycia niz kon ROT.
- **Zold** - stawka dzienna gry wedlug tieru 1/2/3/5/8/12/17 (tier 0-6); po ukosniku z premia konnych Armoury x1.5 (`MountedWage`: partie rodow i zalogi).
  Mnoznik gry x1.5 dla `Occupation.Mercenary` dotyczy tylko najemnikow z karczm (`basic_mercenary_troops` kultur w spcultures.xml: `western_mercenary`, `eastern_mercenary`, `sword_sisters_sister_t3`, `ROT_mercenary1` i ich awanse);
  wszystkie 41 jednostek kompanii najemnych ROT z tabel ma `occupation=Soldier`, wiec placi sie im jak zwyklym zolnierzom.
  BannerKings mnozy jeszcze zold calej partii swoimi czynnikami (`DefaultPartyWageModel`, BK) - to dotyczy wszystkich krain tak samo.
- **Sila gra / XML** - moj wskaznik jakosci: 100 = srednia jednostek tego samego rodzaju i tieru we wszystkich 31 kulturach (jednostki dostepne dla AI: wies,
  szlachta, rody, szablony kultur). **Gra** = umiejetnosci po SkillSinew (to, z czym walczysz); **XML** = umiejetnosci z danych ROT, liczone wzgledem sredniej XML
  (to, co zaprojektowal ROT). Roznica gra - XML to wplyw naszej zasady. Wzor: piechota = umiejetnosc noszonej broni wrecz + 0.5 Atletyki + 0.4 pancerza + 20 za tarcze;
  strzelcy = luk/kusza/rzut + 0.3 wrecz + 0.3 pancerza + 0.2 naciagu, **porownywani w obrebie klasy broni** (kusznik z kusznikami, lucznik z lucznikami tego tieru;
  na tierach 2 i 6 kusznikow jest za malo, wtedy porownanie z wszystkimi strzelcami tieru); jazda = Jazda + wrecz + 0.4 pancerza + 0.5 x (szybkosc + szarza konia,
  szarza ucieta do 40 - slon, mamut i jednorozec licza sie jak najlepszy kon); konni lucznicy = Jazda + strzelanie + 0.3 pancerza + 0.5 x szybkosc konia.
  Kon we wzorze to kon z wzorca [D-wzorzec], wiec skladnik konia mowi o wzorcu, nie o bitwie. To miarka do porownan, nie symulacja bitwy (nie zna dlugosci broni,
  szyku ani AI formacji). "-" = za malo jednostek tego rodzaju i tieru do sredniej.

**Skad sie biora zolnierze lordow AI [K]:**
- **Ochotnicy u notabli.** BannerKings losuje warstwe ludnosci, ale jego lista wlasnych rekrutow jest w tym zestawie modow pusta, wiec zawsze dziala zasada
  vanilli: wies przy zamku daje linie szlachecka (`elite_basic_troop`), reszta - linie zwykla (`basic_troop`). Ochotnik ma kulture NOTABLA, nie osady.
- **Podmiana ROT (`ROT.CampaignBehaviors.ROTTroopRecruiter`).** Pula szablonu (`Settings`, :470-521) to kazda jednostka ze stosu szablonu rodu
  (`Clan.DefaultPartyTemplate`, a gdy rod nie ma wlasnego - szablonu kultury) **razem z calym jej drzewem awansow** (`TraverseTree`). Przy werbunku, codziennie
  i po lupach ROT zamienia lordowi AI kazdego zolnierza spoza tej puli na czlowieka z puli tego samego tieru (`DetermineReplacement`, :222-257 - losuje rowno
  sposrod pasujacych; gdy tieru brak, bierze tier nizej albo wyzej, a roznice ceny werbunku rozlicza z sakiewka lorda, :205-212) i wybiera tylko takich, ktorych linia
  prowadzi do formacji brakujacej wzgledem "celu" szablonu (`ShouldRecruit`). **Cel** (`GetTemplateComposition`, :411-441) liczony jest tylko ze stosow tieru 3+,
  ale ROT porownuje z nim CALA partie: kazdy czlowiek liczy sie do formacji, do ktorej dojdzie po awansach (`GetPartyComposition`, :326-409). Rekrut t1-t2
  walczy jednak pieszo, dopoki nie awansuje, wiec w polu piechoty jest wiecej, a strzelcow i jazdy mniej niz w celu. Awanse AI ida zwyklymi drzewami
  (`upgrade_targets`), wiec jednostki t4-t6 w polu to nie stosy szablonu, tylko to, do czego doszli ludzie. To samo ROT robi codziennie z zalogami twierdz AI
  (`DailyTickSettlement`, :51-79). Dlatego w tabelach podaje: **cel**, **swieza partia** (caly szablon z rekrutami) i pomiar konnych z bitew.
- **Kogo ROT nie rusza [K]:** gracza, bohaterow klanu gracza, karawany i zalogi twierdz klanu gracza (`IsHeroManageable`, `IsGarrisonManageable`, :523-559);
  przy wlaczonej opcji ROT "MyLittleWarband" takze wasali Twojego krolestwa, gdy jestes krolem. W Twojej druzynie i Twoich zalogach zostaje to, kogo sam
  zwerbujesz i awansujesz.
- **Zaciag rodowy Armoury (`HouseLevies`, wlaczony):** na wlasnej ziemi rodu szlachecki ochotnik u notabla staje sie czlowiekiem tego rodu tego samego tieru
  (log: srednio 136 dziennie [P]).
- **Najemnicy z karczm [P]:** lordowie AI werbuja konnych najemnikow z wlasnym koniem: 8 617 w 120 dob (167 dziennie w dobach 1-10, 85 w 31-60, 28 w 91-120),
  a konnych ochotnikow od notabli tylko 448 (linia Armoury "Konie rekrutow (160)"). Najemnik nie nalezy do puli szablonu, wiec ROT zamienia go na czlowieka
  z puli tego samego tieru i brakujacej formacji - a brakuje prawie zawsze jazdy. To dzis glowne zrodlo jazdy AI.
- **Awans na jezdzca (Armoury `Stables`, `CavalryNeedsMounts: true`) [K]:** awans pieszego na konnego zabiera konia z taboru, takze lordowi AI. Kategoria konia
  wynika z tieru celu, a nie z XML: zwykly kon do tieru 3, kon bojowy od tieru 4, szlachetny od 6 (gdy go brak - bojowy) (`RequiredMountFor`, Stables.cs:202-211);
  AI oddaje najtanszego konia z tej kategorii (`Consume`, :231-250). Awans z jezdzca na jezdzca jest darmowy (`RiderOnlyTarget`; log startu: "128 celow osiagalnych
  wylacznie z siodla"). Lord bez wlasciwego konia nie awansuje na jezdzca, a oddzial z rozwidleniem skreca w piechote (`FilterTargets`, :256-305).
  Lordowie dokupuja konie na targu i we wsiach za wlasne zloto, gdy ktos czeka na awans (`OnSettlementEntered`, :353-489): [P] 592 zakupy, 1 591 koni,
  laczne "czekalo na awans" 3 311 (zakupy.log). **Kon, ktorego AI oddaje za awans, znika** (nie trafia do zbrojowni DTE nowego jezdzca) - inaczej niz u gracza,
  ktorego kon za awans przechodzi do zbrojowni (`BankPaidHorses`, :64-88). Ile awansow AI odpadlo z braku konia, log nie liczy.
  Model awansow ROT sam nie wymaga od AI zadnych przedmiotow (`ROTTroopUpgradeModel`) - wymog konia przywraca tylko Stables.

**Co mierzy log [P]:** dla kazdej bitwy polowej obie strony: nazwa, krolestwo, liczba ludzi, udzial konnych (`IsMounted`) i sredni tier (`bitwy.log`, linie H3);
stan partii i zalog rodow w 120. dobie (`budzet-rodow.csv`); co dobe udzial konnych i zold wszystkich partii rodow AI (linia Armoury "Zold konnych (160)").
Log NIE rozroznia piechoty od strzelcow - podzial P/S jest tylko z szablonu. Strony bitew rozdzielam na **partie lordow**, **grupy wiesniakow** ("Villagers of"),
**karawany** i **patrole**: w 932 bitwach polowych, w ktorych obie strony naleza do krolestw, 513 to bitwy lorda z lordem, 219 - rozbite karawany, 181 - grupy wiesniakow,
11 - patrole i inne. "Tier w bitwach" i "konni w bitwach" licze tylko dla partii lordow (wiesniacy maja tier 0 i 0% konnych, karawany 40-60% konnych).
"Zold na czlowieka" i sila wzorca sa wazone ludzmi w partiach rodow krolestwa (doba 120), a nie liczba rodow.

**Szybki przeglad** wszystkich krolestw w jednej tabeli jest w rozdziale 2.2; ponizej pelne listy jednostek kultura po kulturze (Westeros, potem Essos).


### Westeros


#### 1.1 Polnoc (`battania`)

- Jednostek z droga do wojska: **117** (P 67, S 27, J 22, KL 1). Rekrut wsi `battanian_volunteer`, szlachecki `battanian_highborn_youth`. Sila wzgledna t3+ gra/XML [D]: P 98/100 / S 107/103 / J 100/100 / KL 106/106.
- Krolestwo **Polnoc** (rodow 26, szablonow 9): ludzi w partiach lordow 11868, w zalogach 8880 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 35%/43%/21%/0%, swieza partia 65%/24%/12%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 4.5% (doby 1-30: 6.7, 91-120: 4.1), sredni tier 2.76 [P]. Zold na czlowieka: swieza partia 5.87, rozwinieta 8.8 zl/dobe [K+D]. Bitwy lord z lordem [P]: 5, wygrane 4 (80%) - mala proba. Stracone w polu grupy wiesniakow / karawany: 1 / 27; wygrane napady na wiesniakow / karawany wroga: 3 / 9 [P]. Szablony (ludzie): `battania` 5178, `clan_bolton` 1230, `clan_stark` 1171, `clan_karstark` 988, `clan_glover` 942, `clan_umber` 723, `clan_manderly` 707, `clan_mormont` 572, `clan_cerwyn` 357.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Northern Recruit | 1 | P | wies | 1H 20, Atl 20 | 78 | mlot 2r, topor | - | 2 | 73 / 103 |
| Northern Soldier | 2 | P | wies | 1H 70, Atl 70 | 153 | miecz, wlocznia, tarcza | - | 3 | 105 / 100 |
| Northern Woodsman | 2 | P | wies | 1H 40, Atl 70, Rzut 105* | 177 | topor, oszczepy | - | 3 | 82 / 93 |
| Northern Raider | 3 | P | wies | Dr 105*, Atl 105, Rzut 70 | 271 | topor, wlocznia, toporki | - | 5 | 87 / 90 |
| Northern Man at Arms | 3 | P | wies | 1H 140*, Atl 105 | 271 | wlocznia, miecz, tarcza | - | 5 | 105 / 98 |
| Northern Bowman | 3 | S | wies | Luk 70, 1H 50, Atl 105 | 269 | topor, luk 60 | - | 5 | 102 / 103 |
| Northern Scout | 4 | J | wies | Jaz 100, 1H 140 | 269 | wlocznia, miecz, tarcza | Palfrey 52/20 | 8/12 | 93 / 92 |
| Northern Winter Warrior | 4 | P | wies | 2H 105, Atl 105 | 345 | miecz 2r | - | 8 | 78 / 90 |
| Northern Sergeant | 4 | P | wies | 1H 140, Atl 105 | 318 | wlocznia, miecz, tarcza | - | 8 | 90 / 96 |
| Northern Archer | 4 | S | wies | Luk 100, 1H 105, Atl 105 | 269 | topor, luk 70 | - | 8 | 96 / 91 |
| Northern Horseman | 5 | J | wies | Jaz 130, 1H 140 | 376 | wlocznia, miecz, tarcza | Courser 55/22 | 12/18 | 92 / 93 |
| Northern Pikeman | 5 | P | wies | 1H 140, Atl 175 | 476 | drzewc 2r, topor, tarcza | - | 12 | 102 / 104 |
| Northern Colonel | 5 | P | wies | 1H 140, Atl 175, Rzut 140 | 476 | wlocznia, miecz, oszczepy, tarcza | - | 12 | 102 / 101 |
| Northern Winter Champion | 5 | P | wies | 2H 130, Atl 175, Rzut 120 | 476 | miecz 2r, toporki | - | 12 | 95 / 96 |
| Northern Ranger | 5 | S | wies | Luk 130, 1H 140, Atl 175 | 376 | miecz, luk 100, tarcza | - | 12 | 98 / 101 |
| Northern Noble Youth | 2 | P | szlachta | 1H 140*, Atl 40 | 91 | miecz, wlocznia, toporki, tarcza | - | 3 | 122 / 83 |
| Northern Noble Warrior | 3 | P | szlachta | 1H 140*, Atl 105, Rzut 90 | 291 | miecz, wlocznia, toporki, tarcza | - | 5 | 108 / 112 |
| Northern Hero | 4 | P | szlachta | Dr 175*, Atl 140, Rzut 105 | 320 | miecz, wlocznia, toporki, tarcza | - | 8 | 104 / 97 |
| Northern Warlord | 5 | P | szlachta | 1H 140, Atl 175, Rzut 120 | 551 | miecz, wlocznia, toporki, tarcza | - | 12 | 109 / 112 |
| Northern Mounted Warlord | 6 | J | szlachta | Jaz 220, 1H 230, Rzut 210 | 551 | miecz, wlocznia, toporki, tarcza | Thoroughbred Destr 68/37 | 17/26 | 102 / 102 |
| Bolton Recruit | 1 | P | rod Bolton | 1H 20, Atl 20 | 76 | buzdygan, topor | - | 2 | 72 / 102 |
| Bolton Levy | 2 | P | rod Bolton | 1H 50, Atl 70 | 122 | buzdygan, tarcza | - | 3 | 87 / 102 |
| Bolton Footman | 3 | P | rod Bolton | 1H 80, Atl 105 | 284 | miecz, tarcza | - | 5 | 87 / 107 |
| Bolton Archer | 3 | S | rod Bolton | Luk 80, 1H 70, Atl 105 | 257 | topor, luk 70 | - | 5 | 110 / 111 |
| Bolton Horseman | 4 | J | rod Bolton | Jaz 100, Dr 100 | 327 | miecz, wlocznia, tarcza | Palfrey 52/20 | 8/12 | 89 / 98 |
| Bolton Veteran | 4 | P | rod Bolton | 1H 140, Atl 175* | 354 | drzewc 2r, buzdygan, tarcza | - | 8 | 103 / 103 |
| Bolton Veteran Archer | 4 | S | rod Bolton | Luk 110, 1H 105, Atl 105 | 284 | topor, luk 120 | - | 8 | 106 / 106 |
| Bolton Cavalry | 5 | J | rod Bolton | Jaz 130, Dr 140 | 460 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 99 / 100 |
| Dreadfort Blackguard | 5 | P | rod Bolton | 1H 140, Atl 175 | 407 | drzewc 2r, buzdygan, tarcza | - | 12 | 95 / 98 |
| Dreadfort PIkeman | 5 | P | rod Bolton | Dr 175, Atl 175 | 407 | buzdygan, drzewc 2r, tarcza | - | 12 | 104 / 97 |
| Bolton Hunter | 5 | S | rod Bolton | Luk 175, 1H 110, Atl 120 | 317 | topor, luk 180 | - | 12 | 110 / 102 |
| Bolton Flayer | 6 | P | rod Bolton | 1H 240, Atl 230, Rzut 230 | 609 | drzewc 2r, buzdygan, noze, tarcza | - | 17 | 109 / 109 |
| Cerwyn Recruit | 1 | P | rod Cerwyn | 1H 35, Atl 20 | 76 | topor, buzdygan | - | 2 | 90 / 102 |
| Cerwyn Levy | 2 | P | rod Cerwyn | 1H 70, Atl 70 | 163 | topor, tarcza | - | 3 | 107 / 114 |
| Cerwyn Soldier | 3 | P | rod Cerwyn | Dr 140*, Atl 140* | 299 | topor, wlocznia, tarcza | - | 5 | 114 / 109 |
| Cerwyn Scout | 4 | J | rod Cerwyn | Jaz 100, Dr 140 | 326 | miecz, wlocznia, tarcza | Palfrey 52/20 | 8/12 | 98 / 98 |
| Cerwyn Axeman | 4 | P | rod Cerwyn | 1H 140, Atl 140 | 326 | topor, wlocznia, tarcza | - | 8 | 95 / 99 |
| Cerwyn Archer | 4 | S | rod Cerwyn | Luk 100, 1H 90, Atl 140* | 299 | miecz, luk 100 | - | 8 | 100 / 102 |
| Cerwyn Horseman | 5 | J | rod Cerwyn | Jaz 130, Dr 140 | 342 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 90 / 91 |
| Cerwyn Veteran Axeman | 5 | P | rod Cerwyn | 2H 140, Atl 140 | 342 | topor 2r, topor, tarcza | - | 12 | 85 / 92 |
| Cerwyn Veteran Archer | 5 | S | rod Cerwyn | Luk 175, 1H 120, Atl 140 | 326 | miecz, luk 180 | - | 12 | 111 / 101 |
| Cerwyn Marauder | 6 | P | rod Cerwyn | 2H 250, Atl 240, Rzut 240 | 342 | topor 2r, toporki | - | 17 | 89 / 89 |
| Glover Recruit | 1 | P | rod Glover | 1H 20, Atl 20 | 76 | mlot 2r, topor | - | 2 | 72 / 102 |
| Glover Levy | 2 | P | rod Glover | 1H 50, Atl 70 | 163 | topor, tarcza | - | 3 | 96 / 114 |
| Glover Footman | 3 | P | rod Glover | Dr 140*, Atl 105 | 269 | topor, wlocznia, tarcza | - | 5 | 105 / 104 |
| Glover Rider | 4 | J | rod Glover | Jaz 100, Dr 140 | 346 | miecz, wlocznia, tarcza | Palfrey 52/20 | 8/12 | 100 / 100 |
| Glover Man at Arms | 4 | P | rod Glover | 1H 110, Atl 175* | 346 | topor, wlocznia, tarcza | - | 8 | 94 / 102 |
| Glover Archer | 4 | S | rod Glover | Luk 100, 1H 90, Atl 105 | 269 | miecz, luk 100 | - | 8 | 97 / 98 |
| Glover Horseman | 5 | J | rod Glover | Jaz 130, Dr 140 | 439 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 98 / 99 |
| Glover Warrior | 5 | P | rod Glover | 1H 130, Atl 175, Rzut 140 | 439 | topor, wlocznia, oszczepy, tarcza | - | 12 | 96 / 99 |
| Glover Veteran Archer | 5 | S | rod Glover | Luk 175, 1H 120, Atl 175* | 389 | miecz, luk 180 | - | 12 | 118 / 107 |
| Glover Bushranger | 6 | P | rod Glover | 1H 240, Atl 240, Rzut 240 | 439 | topor, wlocznia, oszczepy, tarcza | - | 17 | 98 / 98 |
| Karstark Recruit | 1 | P | rod Karstark | 1H 20, Atl 20 | 76 | mlot 2r, topor | - | 2 | 72 / 102 |
| Karstark Levy | 2 | P | rod Karstark | 1H 50, Atl 70 | 152 | buzdygan, tarcza | - | 3 | 93 / 111 |
| Karstark Brute | 3 | P | rod Karstark | 1H 80, Atl 105 | 224 | buzdygan, tarcza | - | 5 | 79 / 97 |
| Karstark Outrider | 4 | J | rod Karstark | Jaz 100, Dr 140 | 245 | miecz, wlocznia, tarcza | Palfrey 52/20 | 8/12 | 90 / 94 |
| Karstark Spearman | 4 | P | rod Karstark | Dr 110, Atl 110 | 245 | miecz, wlocznia, tarcza | - | 8 | 75 / 89 |
| Karstark Archer | 4 | S | rod Karstark | Luk 100, 1H 90, Atl 105 | 224 | miecz, luk 120 | - | 8 | 93 / 94 |
| Karstark Shock Cavalry | 5 | J | rod Karstark | Jaz 130, Dr 150 | 350 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 92 / 95 |
| Karstark House Guard | 5 | P | rod Karstark | Dr 140, Atl 175 | 350 | miecz, wlocznia, tarcza | - | 12 | 90 / 92 |
| Karstark Elite Archer | 5 | S | rod Karstark | Luk 130, 1H 120, Atl 120 | 245 | topor, luk 120 | - | 12 | 85 / 89 |
| Karstark Loyalist | 6 | P | rod Karstark | 1H 240, Atl 240, Rzut 230 | 350 | miecz, drzewc 2r, toporki, tarcza | - | 17 | 91 / 92 |
| Mormont Recruit | 1 | P | rod Mormont | 1H 35, Atl 20 | 76 | topor, buzdygan | - | 2 | 90 / 102 |
| Mormont Woodswoman | 2 | P | rod Mormont | 1H 50, Atl 70, Rzut 105* | 173 | topor, oszczepy | - | 3 | 87 / 102 |
| Mormont Footman | 3 | P | rod Mormont | 1H 80, Atl 105 | 282 | topor, wlocznia, tarcza | - | 5 | 87 / 106 |
| Mormont Trapper | 3 | S | rod Mormont | Luk 90, 1H 80, Atl 105 | 282 | topor, luk 120 | - | 5 | 128 / 129 |
| Mormont Scout | 4 | J | rod Mormont | Jaz 100, Dr 105 | 360 | wlocznia, miecz, tarcza | Palfrey 52/20 | 8/12 | 93 / 101 |
| Mormont Man at Arms | 4 | P | rod Mormont | 1H 110, Atl 175* | 333 | topor, wlocznia, tarcza | - | 8 | 92 / 100 |
| Mormont Huntress | 4 | S | rod Mormont | Luk 175*, 1H 100, Atl 175* | 379 | topor, luk 180 | - | 8 | 150 / 129 |
| Mormont Horseman | 5 | J | rod Mormont | Jaz 130, Dr 140 | 449 | wlocznia, miecz, tarcza | Hunter 55/7 | 12/18 | 97 / 98 |
| Mormont Mounted Huntress | 5 | KL | rod Mormont | Jaz 140, Luk 150 | 379 | topor, luk 135 | Palfrey 52/20 | 12/18 | 106 / 106 |
| Mormont House Guard | 5 | P | rod Mormont | 1H 140, Atl 175 | 449 | topor, wlocznia, tarcza | - | 12 | 99 / 102 |
| Mormont Veteran Huntress | 5 | S | rod Mormont | Luk 175, 1H 130, Atl 175 | 379 | topor, luk 180 | - | 12 | 118 / 114 |
| Mormont Bowmaiden | 6 | S | rod Mormont | Luk 250, 2H 220, Atl 230 | 528 | topor 2r, luk 180 | - | 17 | 104 / 104 |
| Stark Recruit | 1 | P | rod Stark | 1H 70*, Atl 20 | 78 | miecz, topor | - | 2 | 132 / 103 |
| Stark Levy | 2 | P | rod Stark | 1H 70, Atl 70 | 165 | miecz, tarcza | - | 3 | 108 / 114 |
| Stark Footman | 3 | P | rod Stark | 1H 175*T, Atl 105 | 271 | miecz, wlocznia, tarcza | - | 5 | 116 / 105 |
| Stark Bowman | 3 | S | rod Stark | Luk 70, 1H 70, Atl 70 | 163 | miecz, luk 70 | - | 5 | 88 / 87 |
| Stark Horseman | 4 | J | rod Stark | Jaz 100, 1H 175*T | 318 | wlocznia, miecz, tarcza | Palfrey 52/20 | 8/12 | 106 / 97 |
| Stark Soldier | 4 | P | rod Stark | 1H 175*T, Atl 175* | 368 | wlocznia, miecz, tarcza | - | 8 | 113 / 104 |
| Stark Longbowman | 4 | S | rod Stark | Luk 100, 1H 90, Atl 105 | 269 | miecz, luk 100 | - | 8 | 97 / 98 |
| Stark Cavalry | 5 | J | rod Stark | Jaz 130, 1H 175 | 459 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 106 / 102 |
| Stark House Guard | 5 | P | rod Stark | 1H 175, Atl 175 | 459 | wlocznia, miecz, tarcza | - | 12 | 108 / 103 |
| Stark Pikeman | 5 | P | rod Stark | 1H 175, Atl 175 | 459 | drzewc 2r, miecz, tarcza | - | 12 | 108 / 106 |
| Stark Master Longbowman | 5 | S | rod Stark | Luk 175, 1H 110, Atl 175* | 312 | miecz, luk 180 | - | 12 | 109 / 102 |
| Stark Sworn Sword | 6 | P | rod Stark | 1H 250, Atl 250, Rzut 230 | 590 | miecz, drzewc 2r, toporki, tarcza | - | 17 | 111 / 111 |
| Umber Recruit | 1 | P | rod Umber | 1H 35, Atl 20 | 76 | topor, buzdygan | - | 2 | 90 / 102 |
| Umber Levy | 2 | P | rod Umber | 1H 70, Atl 70 | 152 | topor, tarcza | - | 3 | 105 / 104 |
| Umber Footman | 3 | P | rod Umber | 1H 80, Atl 105 | 186 | topor, tarcza | - | 5 | 74 / 90 |
| Umber Scout | 4 | J | rod Umber | Jaz 100, Dr 140 | 293 | miecz, wlocznia, tarcza | Palfrey 52/20 | 8/12 | 95 / 94 |
| Umber Man at Arms | 4 | P | rod Umber | 1H 140, Atl 140 | 322 | wlocznia, topor, tarcza | - | 8 | 95 / 95 |
| Umber Archer | 4 | S | rod Umber | Luk 100, 1H 90, Atl 140* | 293 | topor, luk 120 | - | 8 | 101 / 103 |
| Umber Horseman | 5 | J | rod Umber | Jaz 130, Dr 150 | 346 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 92 / 95 |
| Umber Axeman | 5 | P | rod Umber | 2H 140, Atl 140 | 353 | topor, topor 2r, tarcza | - | 12 | 86 / 93 |
| Umber House Guard | 5 | P | rod Clegane/Umber | Dr 175, Atl 140 | 365 | miecz, wlocznia, tarcza | - | 12 | 96 / 91 |
| Umber Marksman | 5 | S | rod Umber | Luk 175, 1H 110, Atl 140 | 334 | topor, luk 180 | - | 12 | 111 / 100 |
| Umber Berzerker | 6 | P | rod Umber | 2H 250, Atl 240, Rzut 220 | 365 | topor 2r, wlocznia, oszczepy | - | 17 | 91 / 91 |
| Manderly Recruit | 1 | P | rod Manderly | 1H 35, Atl 20 | 73 | miecz, topor | - | 2 | 89 / 100 |
| Manderly Levy | 2 | P | rod Manderly | 1H 70, Atl 70 | 176 | miecz, tarcza | - | 3 | 110 / 117 |
| White Harbor Squire | 3 | J | rod Manderly | Jaz 80, 1H 175* | 277 | miecz, wlocznia, tarcza | Palfrey 52/20 | 5/8 | 118 / 107 |
| Manderly Footman | 3 | P | rod Manderly | 1H 175*, Atl 140* | 277 | miecz, tarcza | - | 5 | 123 / 103 |
| White Harbor Knight | 4 | J | rod Manderly | Jaz 110, 1H 175* | 429 | miecz, wlocznia, tarcza | Hunter 55/7 | 8/12 | 118 / 115 |
| Manderly Man at Arms | 4 | P | rod Manderly | 1H 175*, Atl 175* | 398 | miecz, wlocznia, tarcza | - | 8 | 117 / 105 |
| Manderly Archer | 4 | S | rod Manderly | Luk 100, 1H 175*, Atl 140* | 277 | miecz, luk 100 | - | 8 | 108 / 99 |
| White Harbor Elite Knight | 5 | J | rod Manderly | Jaz 140, 1H 175 | 563 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 117 / 115 |
| White Harbor Pike Knight | 5 | P | rod Manderly | 1H 175, Atl 175 | 517 | miecz, drzewc 2r, tarcza | - | 12 | 114 / 111 |
| Manderly Veteran Archer | 5 | S | rod Manderly | Luk 175, 1H 175*, Atl 175* | 338 | miecz, luk 180 | - | 12 | 118 / 102 |
| White Harbor Knight Commander | 6 | J | rod Manderly | Jaz 240, Dr 250 | 565 | miecz, wlocznia, tarcza | Destrier 62/23 | 17/26 | 107 / 107 |
| Northern Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 117 | buzdygan, topor, luk 90 | - | 3 | 135 / 145 |
| Northern Militia Spearman | 2 | P | milicja | 1H 80, Atl 105 | 180 | wlocznia, miecz, tarcza | - | 3 | 126 / 143 |
| Northern Militia Veteran Archer | 4 | S | milicja | Luk 140, 1H 110, Atl 130 | 117 | topor, miecz, luk 120 | - | 8 | 98 / 100 |
| Northern Militia Veteran Spearman | 4 | P | milicja | 1H 130, Atl 110 | 211 | wlocznia, miecz, tarcza | - | 8 | 76 / 91 |
| Wild Hare Footman | 2 | P | najemnicy Wild Hares | Dr 140*, Atl 40 | 103 | miecz, wlocznia, tarcza | - | 3 | 125 / 93 |
| Wild Hare Archer | 3 | S | najemnicy Wild Hares | Luk 80, 1H 70, Atl 105 | 175 | topor, luk 120 | - | 5 | 102 / 103 |
| Wild Hare Horseman | 4 | J | najemnicy Wild Hares | Jaz 100, 1H 140* | 175 | miecz, wlocznia, tarcza | Courser 55/22 | 8/12 | 84 / 83 |
| Moon Brother Woodsman | 3 | S | najemnicy Moon Brothers | Luk 90, 1H 80, Atl 100 | 118 | topor, luk 70, tarcza | - | 5 | 94 / 95 |
| Moon Brother Berzerker | 4 | P | najemnicy Moon Brothers | 2H 120, Atl 120, Rzut 110 | 173 | topor 2r, toporki | - | 8 | 66 / 78 |

#### 1.2 Dolina Arrynow (`vale`)

- Jednostek z droga do wojska: **55** (P 26, S 12, J 17, KL 0). Rekrut wsi `vale_recruit`, szlachecki `vale_page`. Sila wzgledna t3+ gra/XML [D]: P 94/100 / S 92/94 / J 99/103 / KL -/-.
- Krolestwo **Dolina** (rodow 21, szablonow 4): ludzi w partiach lordow 7738, w zalogach 5264 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 32%/35%/33%/0%, swieza partia 60%/26%/15%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 12.6% (doby 1-30: 12.4, 91-120: 14.0), sredni tier 2.62 [P]. Zold na czlowieka: swieza partia 5.5, rozwinieta 9.19 zl/dobe [K+D]. Bitwy lord z lordem [P]: 6, wygrane 5 (83%) - mala proba. Stracone w polu grupy wiesniakow / karawany: 6 / 15; wygrane napady na wiesniakow / karawany wroga: 3 / 3 [P]. Szablony (ludzie): `vale` 4829, `clan_arryn` 1362, `clan_royce` 776, `clan_grafton` 719.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Vale Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Vale Footman | 2 | P | wies | 1H 70, Atl 70 | 133 | miecz | - | 3 | 89 / 80 |
| Vale Bowman | 2 | S | wies | Luk 50, 1H 70, Atl 40 | 105 | miecz, luk 60 | - | 3 | 105 / 104 |
| Vale Rider | 3 | J | wies | Jaz 70, 1H 70 | 274 | miecz, tarcza | Andalos Palfrey 53/20 | 5/8 | 84 / 100 |
| Vale Soldier | 3 | P | wies | 1H 70, Atl 175* | 322 | miecz, wlocznia, tarcza | - | 5 | 100 / 107 |
| Vale Archer | 3 | S | wies | Luk 80, 1H 70, Atl 70 | 149 | miecz, luk 100 | - | 5 | 95 / 94 |
| Vale Lancer | 4 | J | wies | Jaz 100, Dr 100 | 320 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 88 / 97 |
| Vale Voulgier | 4 | J | wies | Jaz 100, Dr 140 | 320 | miecz, drzewc 2r, tarcza | Andalos Palfrey 53/20 | 8/12 | 98 / 97 |
| Vale Man at Arms | 4 | P | wies | 1H 140*, Atl 175* | 342 | miecz, wlocznia, tarcza | - | 8 | 101 / 96 |
| Vale Elite Archer | 4 | S | wies | Luk 100, 1H 80, Atl 80 | 149 | miecz, luk 100 | - | 8 | 80 / 81 |
| Vale Elite Lancer | 5 | J | wies | Jaz 130, 1H 140 | 450 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 100 / 101 |
| Vale Elite Voulgier | 5 | J | wies | Jaz 130, 1H 140 | 450 | miecz, drzewc 2r, tarcza | Western Courser 58/30 | 12/18 | 100 / 101 |
| Vale House Guard | 5 | P | wies | 1H 140, Atl 175* | 479 | miecz, wlocznia, tarcza | - | 12 | 102 / 100 |
| Vale Master Archer | 5 | S | wies | Luk 130, 1H 80, Atl 140 | 271 | miecz, luk 100 | - | 12 | 82 / 86 |
| Vale Page | 2 | P | szlachta | 1H 70, Atl 30 | 97 | miecz | - | 3 | 70 / 81 |
| Vale Squire | 3 | P | szlachta | 1H 70, Atl 105 | 163 | miecz, tarcza | - | 5 | 68 / 78 |
| Vale Knight | 4 | J | szlachta | Jaz 110, 1H 140* | 342 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 102 / 102 |
| Vale Elite Knight | 5 | J | szlachta | Jaz 140, 1H 140 | 452 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 102 / 105 |
| Knight of the Vale | 6 | J | szlachta | Jaz 240, Dr 230 | 479 | miecz, wlocznia, tarcza | King's Destrier 69/36 | 17/26 | 101 / 101 |
| Arryn Recruit | 1 | P | rod Arryn | 1H 70*, Atl 20 | 97 | miecz | - | 2 | 142 / 116 |
| Arryn Levy | 2 | P | rod Arryn | 1H 70, Atl 70 | 135 | miecz, wlocznia, tarcza | - | 3 | 101 / 106 |
| Arryn Rider | 3 | J | rod Arryn | Jaz 70, Dr 80 | 272 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 5/8 | 87 / 103 |
| Arryn Footman | 3 | P | rod Arryn | 1H 70, Atl 70 | 151 | miecz, wlocznia, tarcza | - | 5 | 61 / 78 |
| Arryn Horseman | 4 | J | rod Arryn | Jaz 110, Dr 140 | 331 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 101 / 104 |
| Arryn Man at Arms | 4 | P | rod Arryn | Dr 105, Atl 175* | 331 | miecz, wlocznia, tarcza | - | 8 | 91 / 95 |
| Arryn Archer | 4 | S | rod Arryn | Luk 110, 1H 80, Atl 105 | 247 | miecz, luk 100 | - | 8 | 97 / 98 |
| Arryn Knight | 5 | J | rod Arryn | Jaz 140, 1H 140 | 524 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 108 / 111 |
| Arryn House Guard | 5 | P | rod Arryn | 1H 140, Atl 175 | 498 | miecz, wlocznia, tarcza | - | 12 | 104 / 106 |
| Arryn Master Archer | 5 | S | rod Arryn | Luk 140, 1H 80, Atl 105 | 280 | miecz, luk 100 | - | 12 | 87 / 90 |
| Arryn Winged Knight | 6 | J | rod Arryn | Jaz 250, Dr 250 | 658 | miecz, wlocznia, tarcza | Thoroughbred Charg 61/28 | 17/26 | 114 / 114 |
| Grafton Recruit | 1 | P | rod Grafton | 1H 35, Atl 20 | 73 | miecz | - | 2 | 89 / 100 |
| Grafton Levy | 2 | P | rod Grafton | 1H 50, Atl 105* | 276 | miecz, tarcza | - | 3 | 131 / 146 |
| Grafton Footman | 3 | P | rod Grafton | 1H 105, Atl 105 | 288 | buzdygan, wlocznia, tarcza | - | 5 | 96 / 107 |
| Grafton Rider | 4 | J | rod Grafton | Jaz 100, Dr 100 | 304 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 87 / 96 |
| Grafton Man at Arms | 4 | P | rod Grafton | 1H 140, Atl 175* | 374 | topor, wlocznia, tarcza | - | 8 | 105 / 102 |
| Grafton Archer | 4 | S | rod Grafton | Kusz 175*A, 1H 105, Atl 105 | 288 | buzdygan, kusza 230 | - | 8 | 101 / 98 |
| Grafton Horseman | 5 | J | rod Grafton | Jaz 130, 1H 175* | 485 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 12/18 | 108 / 104 |
| Grafton House Guard | 5 | P | rod Grafton | 1H 140, Atl 175 | 485 | topor, wlocznia, tarcza | - | 12 | 103 / 106 |
| Grafton Elite Archer | 5 | S | rod Grafton | Kusz 140, 1H 120, Atl 120 | 304 | buzdygan, kusza 300 | - | 12 | 90 / 99 |
| Grafton Flaming Knight | 6 | P | rod Grafton | 2H 250, Atl 250 | 611 | topor, wlocznia, tarcza | - | 17 | 112 / 113 |
| Royce Recruit | 1 | P | rod Royce | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Royce Footman | 2 | P | rod Royce | 1H 70, Atl 70 | 135 | miecz, wlocznia, tarcza | - | 3 | 101 / 109 |
| Royce Soldier | 3 | P | rod Royce | 1H 80, Atl 105, Rzut 70 | 270 | miecz, wlocznia, toporki, tarcza | - | 5 | 85 / 106 |
| Royce Rider | 4 | J | rod Royce | Jaz 110, Dr 110, Rzut 100 | 332 | miecz, wlocznia, toporki, tarcza | Andalos Palfrey 53/20 | 8/12 | 94 / 104 |
| Royce Warrior | 4 | P | rod Royce | Dr 140, Atl 140, Rzut 100 | 332 | miecz, wlocznia, toporki, tarcza | - | 8 | 96 / 104 |
| Royce Archer | 4 | S | rod Royce | Luk 100, 1H 90, Atl 105 | 270 | miecz, luk 100 | - | 8 | 97 / 98 |
| Royce Cavalrywomen | 5 | J | rod Royce | Jaz 150, 1H 140, Rzut 140 | 430 | miecz, wlocznia, toporki, tarcza | Western Courser 58/30 | 12/18 | 102 / 105 |
| Royce Elite Warrior | 5 | P | rod Royce | 1H 150, Atl 175, Rzut 130 | 421 | miecz, wlocznia, toporki, tarcza | - | 12 | 99 / 103 |
| Royce Veteran Archer | 5 | S | rod Royce | Luk 130, 1H 120, Atl 140 | 332 | miecz, luk 140 | - | 12 | 95 / 99 |
| Royce Heroine | 6 | J | rod Royce | Jaz 240, Dr 240, Rzut 230 | 430 | miecz, wlocznia, toporki, tarcza | Andalos Destrier 68/36 | 17/26 | 99 / 99 |
| Vale Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 122 | miecz, buzdygan, luk 90 | - | 3 | 142 / 147 |
| Vale Militia Spearman | 2 | P | milicja | 1H 80, Atl 70 | 140 | wlocznia, topor, tarcza | - | 3 | 108 / 132 |
| Vale Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 140 | topor, miecz, luk 90 | - | 5 | 111 / 112 |
| Vale Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 175* | 343 | wlocznia, miecz, tarcza | - | 5 | 113 / 125 |
| Moon Brother Tribesman | 2 | P | najemnicy Moon Brothers | 1H 70, Atl 50 | 99 | topor, toporki, tarcza | - | 3 | 87 / 96 |

#### 1.3 Dorzecze (`river`)

- Jednostek z droga do wojska: **74** (P 40, S 20, J 14, KL 0). Rekrut wsi `river_recruit`, szlachecki `river_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 96/97 / S 99/100 / J 96/95 / KL -/-.
- Krolestwo **Dorzecze** (rodow 15, szablonow 6): ludzi w partiach lordow 7526, w zalogach 3585 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 35%/40%/25%/0%, swieza partia 62%/27%/12%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 4.9% (doby 1-30: 6.5, 91-120: 4.5), sredni tier 2.33 [P]. Zold na czlowieka: swieza partia 5.8, rozwinieta 9.33 zl/dobe [K+D]. Bitwy lord z lordem [P]: 5, wygrane 1 (20%) - mala proba. Stracone w polu grupy wiesniakow / karawany: 3 / 1; wygrane napady na wiesniakow / karawany wroga: 6 / 10 [P]. Szablony (ludzie): `river` 3069, `clan_tully` 2234, `clan_frey` 1263, `clan_mallister` 683, `clan_blackwood` 187, `clan_bracken` 90.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Riverlands Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Riverlands Footman | 2 | P | wies | 1H 70, Atl 70 | 150 | miecz, tarcza | - | 3 | 104 / 99 |
| Riverlands Bowman | 2 | S | wies | Luk 50, 1H 70, Atl 35 | 84 | miecz, luk 60 | - | 3 | 99 / 97 |
| Riverlands Soldier | 3 | P | wies | Dr 175*, Atl 105 | 263 | miecz, wlocznia, tarcza | - | 5 | 115 / 103 |
| Riverlands Archer | 3 | S | wies | Luk 80, 1H 70, Atl 70 | 150 | miecz, luk 100 | - | 5 | 95 / 94 |
| Riverlands Horseman | 4 | J | wies | Jaz 100, Dr 175* | 244 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 99 / 89 |
| Riverlands Man at Arms | 4 | P | wies | Dr 175*, Atl 110 | 275 | miecz, wlocznia, tarcza | - | 8 | 95 / 89 |
| Riverlands Elite Archer | 4 | S | wies | Luk 110, 1H 80, Atl 70 | 150 | miecz, luk 100 | - | 8 | 84 / 86 |
| Riverlands Cavalry | 5 | J | wies | Jaz 130, 1H 140 | 371 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 92 / 93 |
| Riverlands Axeman | 5 | P | wies | 2H 130, Atl 140, Rzut 130 | 371 | topor 2r, toporki | - | 12 | 81 / 87 |
| Riverlands House Guard | 5 | P | wies | Dr 175*, Atl 140 | 371 | miecz, wlocznia, tarcza | - | 12 | 96 / 92 |
| Riverlands Pikeman | 5 | P | wies | 1H 140, Atl 140 | 371 | miecz, drzewc 2r, tarcza | - | 12 | 88 / 93 |
| Riverlands Ranger | 5 | S | wies | Luk 140, 1H 140, Atl 120 | 302 | miecz, luk 120 | - | 12 | 96 / 98 |
| Riverlord's Son | 2 | P | szlachta | 1H 70, Atl 30 | 87 | miecz | - | 3 | 67 / 78 |
| Riverlands Swordsman | 3 | P | szlachta | 1H 140*, Atl 70 | 221 | miecz, miecz 2r, tarcza | - | 5 | 93 / 90 |
| Riverlands Elite Swordsman | 4 | P | szlachta | 1H 140, Atl 140 | 371 | miecz, miecz 2r, tarcza | - | 8 | 100 / 105 |
| Riverlands Swordmaster | 5 | P | szlachta | 2H 140, Atl 140 | 371 | miecz, miecz 2r, tarcza | - | 12 | 88 / 93 |
| Riverlands Admiral | 6 | P | szlachta | 2H 230, Atl 220 | 371 | miecz, miecz 2r, tarcza | - | 17 | 89 / 90 |
| Blackwood Recruit | 1 | P | rod Blackwood | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Blackwood Levy | 2 | P | rod Blackwood | 1H 70, Atl 105* | 257 | miecz, tarcza | - | 3 | 138 / 140 |
| Blackwood Footman | 3 | P | rod Blackwood | Dr 175*, Atl 105 | 279 | miecz, wlocznia, tarcza | - | 5 | 117 / 106 |
| Blackwood Bowman | 3 | S | rod Blackwood | Luk 80, 1H 70, Atl 105 | 279 | miecz, luk 120 | - | 5 | 120 / 121 |
| Blackwood Scout | 4 | J | rod Blackwood | Jaz 110, Dr 175* | 279 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 105 / 96 |
| Blackwood Man at Arms | 4 | P | rod Blackwood | 1H 140, Atl 140 | 314 | miecz, wlocznia, tarcza | - | 8 | 94 / 94 |
| Blackwood Archer | 4 | S | rod Blackwood | Luk 110, 1H 90, Atl 105 | 306 | miecz, luk 110 | - | 8 | 106 / 108 |
| Blackwood Horseman | 5 | J | rod Blackwood | Jaz 130, 1H 140 | 343 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 91 / 92 |
| Blackwood House Guard | 5 | P | rod Blackwood | 1H 140, Atl 140 | 343 | miecz, wlocznia, tarcza | - | 12 | 85 / 92 |
| Blackwood Longbowman | 5 | S | rod Blackwood | Luk 175, 1H 130, Atl 120 | 319 | miecz, luk 180 | - | 12 | 112 / 108 |
| Ravens' Teeth | 6 | S | rod Blackwood | Luk 260, 1H 230, Atl 230 | 368 | miecz, luk 210 | - | 17 | 99 / 99 |
| Bracken Recruit | 1 | P | rod Bracken | 1H 35, Atl 20 | 66 | miecz | - | 2 | 85 / 95 |
| Bracken Levy | 2 | P | rod Bracken | 1H 50, Atl 140* | 260 | miecz, tarcza | - | 3 | 137 / 141 |
| Bracken Footman | 3 | P | rod Bracken | 1H 80, Atl 140* | 294 | miecz, wlocznia, tarcza | - | 5 | 94 / 108 |
| Bracken Rider | 4 | J | rod Bracken | Jaz 100, Dr 100 | 294 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 86 / 95 |
| Bracken Man at Arms | 4 | P | rod Bracken | 1H 140, Atl 140 | 318 | miecz, wlocznia, tarcza | - | 8 | 94 / 95 |
| Bracken Archer | 4 | S | rod Bracken | Luk 100, 1H 90, Atl 140* | 294 | miecz, luk 100 | - | 8 | 100 / 101 |
| Bracken Horseman | 5 | J | rod Bracken | Jaz 130, 1H 140 | 361 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 92 / 95 |
| Bracken House Guard | 5 | P | rod Bracken | 1H 140, Atl 140 | 372 | miecz, wlocznia, tarcza | - | 12 | 88 / 95 |
| Bracken Elite Archer | 5 | S | rod Bracken | Luk 140, 1H 120, Atl 140 | 307 | miecz, luk 125 | - | 12 | 95 / 98 |
| Bracken Pikemaster | 6 | P | rod Bracken | Dr 250, Atl 240, Rzut 220 | 391 | drzewc 2r, miecz, toporki, tarcza | - | 17 | 96 / 96 |
| Frey Recruit | 1 | P | rod Frey | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Frey Levy | 2 | P | rod Frey | 1H 50, Atl 70 | 122 | buzdygan, tarcza | - | 3 | 87 / 102 |
| Frey Cutthroat | 3 | P | rod Frey | 1H 80, Atl 105 | 195 | miecz, tarcza | - | 5 | 75 / 92 |
| Frey Crossbowman | 3 | S | rod Frey | Kusz 175*A, 1H 70, Atl 105 | 195 | miecz, kusza 120 | - | 5 | 96 / 95 |
| Frey Rider | 4 | J | rod Frey | Jaz 100, Dr 140 | 305 | miecz, wlocznia, tarcza | Palfrey 52/20 | 8/12 | 96 / 98 |
| Frey Man at Arms | 4 | P | rod Frey | 1H 110, Atl 110 | 282 | miecz, tarcza | - | 8 | 79 / 93 |
| Frey Veteran Crossbowman | 4 | S | rod Frey | Kusz 175*A, 1H 100, Atl 105 | 222 | miecz, kusza 300 | - | 8 | 99 / 101 |
| Frey Horseman | 5 | J | rod Frey | Jaz 130, Dr 140 | 324 | miecz, wlocznia, tarcza | Courser 55/22 | 12/18 | 88 / 91 |
| Guard of the Crossing | 5 | P | rod Frey | 1H 140, Atl 140 | 324 | miecz, wlocznia, tarcza | - | 12 | 84 / 90 |
| Frey Sharpshooter | 5 | S | rod Frey | Kusz 140, 1H 140, Atl 130 | 241 | miecz, kusza 270 | - | 12 | 85 / 93 |
| Frey Assassin | 6 | S | rod Frey | Kusz 250, 1H 220, Atl 230 | 241 | miecz, kusza 270 | - | 17 | 90 / 90 |
| Mallister Recruit | 1 | P | rod Mallister | 1H 35, Atl 20 | 73 | miecz | - | 2 | 89 / 100 |
| Mallister Levy | 2 | P | rod Mallister | 1H 50, Atl 140* | 260 | miecz, tarcza | - | 3 | 137 / 141 |
| Mallister Footman | 3 | P | rod Mallister | 1H 80, Atl 140* | 272 | miecz, wlocznia, tarcza | - | 5 | 91 / 105 |
| Mallister Horseman | 4 | J | rod Mallister | Jaz 100, Dr 100 | 272 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 84 / 92 |
| Mallister Man at Arms | 4 | P | rod Mallister | 1H 175*T, Atl 175* | 453 | buzdygan, wlocznia, tarcza | - | 8 | 122 / 112 |
| Mallister Archer | 4 | S | rod Mallister | Luk 100, 1H 90, Atl 140* | 326 | miecz, luk 100 | - | 8 | 104 / 105 |
| Mallister Knight | 5 | J | rod Mallister | Jaz 130, 1H 175* | 431 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 12/18 | 104 / 100 |
| Mallister House Guard | 5 | P | rod Mallister | 1H 175, Atl 175 | 523 | wlocznia, buzdygan, tarcza | - | 12 | 114 / 110 |
| Mallister Elite Archer | 5 | S | rod Mallister | Luk 140, 1H 120, Atl 175* | 387 | miecz, luk 125 | - | 12 | 102 / 107 |
| Mallister Eagle Knight | 6 | J | rod Mallister | Jaz 240, Dr 240 | 550 | miecz, wlocznia, tarcza | Western Courser 58/30 | 17/26 | 105 / 105 |
| Tully Recruit | 1 | P | rod Blackfish/Tully | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Tully Footman | 2 | P | rod Blackfish/Tully | 1H 70, Atl 70 | 200 | miecz, tarcza | - | 3 | 115 / 124 |
| Tully Soldier | 3 | P | rod Blackfish/Tully | Dr 175*, Atl 105 | 253 | miecz, wlocznia, tarcza | - | 5 | 114 / 101 |
| Tully Rider | 4 | J | rod Blackfish/Tully | Jaz 110, Dr 175* | 253 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 103 / 93 |
| Tully Man at Arms | 4 | P | rod Blackfish/Tully | Dr 175*, Atl 140 | 337 | miecz, wlocznia, tarcza | - | 8 | 105 / 97 |
| Tully Archer | 4 | S | rod Blackfish/Tully | Luk 110, 1H 80, Atl 105 | 227 | miecz, luk 100 | - | 8 | 94 / 96 |
| Tully Knight | 5 | J | rod Blackfish/Tully | Jaz 140, 1H 140 | 396 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 97 / 98 |
| Tully House Guard | 5 | P | rod Blackfish/Tully | Dr 175, Atl 140 | 364 | miecz, wlocznia, tarcza | - | 12 | 96 / 94 |
| Tully Longbowman | 5 | S | rod Blackfish/Tully | Luk 150, 1H 140, Atl 120 | 315 | miecz, luk 120 | - | 12 | 100 / 102 |
| Riverrun Captain | 6 | J | rod Blackfish/Tully | Jaz 240, 1H 230 | 396 | miecz, wlocznia, tarcza | Western Destrier 64/32 | 17/26 | 95 / 95 |
| Riverlands Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 122 | miecz, buzdygan, luk 90 | - | 3 | 142 / 147 |
| Riverlands Militia Spearman | 2 | P | milicja | 1H 80, Atl 70 | 140 | wlocznia, topor, tarcza | - | 3 | 108 / 132 |
| Riverlands Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 140 | topor, miecz, luk 90 | - | 5 | 111 / 112 |
| Riverlands Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 175* | 340 | wlocznia, miecz, tarcza | - | 5 | 112 / 125 |

#### 1.4 Zelazne Wyspy (`sturgia`)

- Jednostek z droga do wojska: **44** (P 28, S 10, J 6, KL 0). Rekrut wsi `sturgian_recruit`, szlachecki `sturgian_warrior_son`. Sila wzgledna t3+ gra/XML [D]: P 102/105 / S 100/102 / J 99/102 / KL -/-.
- Krolestwo **Zelazne Wyspy** (rodow 30, szablonow 3): ludzi w partiach lordow 5765, w zalogach 4825 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 38%/41%/21%/0%, swieza partia 66%/22%/11%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 4.2% (doby 1-30: 5.3, 91-120: 3.0), sredni tier 2.52 [P]. Zold na czlowieka: swieza partia 5.82, rozwinieta 8.7 zl/dobe [K+D]. Bitew lord z lordem [P]: brak. Stracone w polu grupy wiesniakow / karawany: 0 / 2; wygrane napady na wiesniakow / karawany wroga: 0 / 2 [P]. Szablony (ludzie): `sturgia` 3916, `clan_greyjoy` 1342, `clan_harlaw` 507.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Ironborn Deckhand | 1 | P | wies | 1H 35, Atl 20 | 73 | topor, buzdygan | - | 2 | 89 / 100 |
| Ironborn Sailor | 2 | P | wies | 1H 70, Atl 70 | 144 | miecz, topor, tarcza | - | 3 | 103 / 98 |
| Ironborn Rower | 2 | P | wies | 1H 40, Atl 70, Rzut 105* | 125 | topor, oszczepy | - | 3 | 70 / 78 |
| Ironborn Brigand | 3 | P | wies | 1H 80, Atl 175*, Rzut 105 | 320 | miecz, oszczepy, tarcza | - | 5 | 103 / 113 |
| Ironborn Pirate | 3 | P | wies | Dr 105, Atl 175* | 317 | topor, wlocznia, tarcza | - | 5 | 111 / 106 |
| Ironborn Hunter | 3 | S | wies | Luk 80, 1H 60, Atl 70 | 144 | topor, luk 70 | - | 5 | 89 / 90 |
| Ironborn Scout | 4 | J | wies | Jaz 100, 1H 105, Rzut 105 | 346 | wlocznia, miecz, oszczepy, tarcza | Garron 51/18 | 8/12 | 91 / 99 |
| Ironborn Shipwrecker | 4 | P | wies | 2H 140, Atl 175 | 431 | topor 2r, miecz | - | 8 | 105 / 109 |
| Ironborn Spearman | 4 | P | wies | 1H 105, Atl 175* | 346 | wlocznia, miecz, tarcza | - | 8 | 93 / 97 |
| Ironborn Archer | 4 | S | wies | Luk 100, 1H 100, Atl 105 | 226 | miecz, luk 120 | - | 8 | 94 / 95 |
| Ironborn Horseman | 5 | J | wies | Jaz 130, Dr 130, Rzut 130 | 346 | wlocznia, miecz, oszczepy, tarcza | Garron 51/18 | 12/18 | 87 / 90 |
| Ironborn Heavy Spearman | 5 | P | wies | 1H 140, Atl 175* | 431 | miecz, wlocznia, tarcza | - | 12 | 98 / 99 |
| Ironborn Buccaneer | 5 | P | wies | 2H 140, Atl 175 | 431 | topor 2r, miecz | - | 12 | 93 / 97 |
| Ironborn Axe Master | 5 | P | wies | 1H 130, Atl 175, Rzut 130 | 431 | topor, toporki, tarcza | - | 12 | 95 / 97 |
| Ironborn Veteran Bowman | 5 | S | wies | Luk 140, 1H 120, Atl 175 | 346 | miecz, luk 120 | - | 12 | 98 / 102 |
| Ironborn Salt Recruit | 2 | P | szlachta | 1H 70, Atl 20 | 85 | miecz, tarcza | - | 3 | 75 / 88 |
| Ironborn Pillager | 3 | P | szlachta | 1H 100, Atl 175*, Rzut 70 | 346 | topor, toporki, tarcza | - | 5 | 113 / 121 |
| Ironborn Ravager | 4 | P | szlachta | Dr 140*, Atl 175*, Rzut 100 | 389 | miecz, wlocznia, toporki, tarcza | - | 8 | 106 / 105 |
| Ironborn Reaver | 5 | P | szlachta | Dr 150, Atl 175 | 530 | wlocznia, topor, tarcza | - | 12 | 109 / 112 |
| Ironborn Kraken | 6 | P | szlachta | 1H 230, Atl 230 | 549 | wlocznia, topor, tarcza | - | 17 | 103 / 103 |
| Greyjoy Deckhand | 1 | P | rod Greyjoy | 1H 35, Atl 20 | 74 | topor, buzdygan | - | 2 | 89 / 101 |
| Greyjoy Footman | 2 | P | rod Greyjoy | 1H 70, Atl 105* | 264 | miecz, tarcza | - | 3 | 140 / 142 |
| Greyjoy Soldier | 3 | P | rod Greyjoy | 1H 105, Atl 105 | 267 | miecz, wlocznia, tarcza | - | 5 | 93 / 104 |
| Greyjoy Rider | 4 | J | rod Greyjoy | Jaz 100, 1H 140 | 431 | miecz, wlocznia, tarcza | Garron 51/18 | 8/12 | 108 / 108 |
| Greyjoy Deckman | 4 | P | rod Greyjoy | 1H 140, Atl 175* | 346 | miecz, wlocznia, tarcza | - | 8 | 102 / 102 |
| Greyjoy Archer | 4 | S | rod Greyjoy | Luk 100, 1H 90, Atl 105 | 294 | miecz, luk 100 | - | 8 | 100 / 101 |
| Greyjoy Horseman | 5 | J | rod Greyjoy | Jaz 130, 1H 140, Rzut 140 | 431 | miecz, wlocznia, oszczepy, tarcza | Mountain Garron 57/20 | 12/18 | 97 / 100 |
| Greyjoy Finger Dancer | 5 | P | rod Greyjoy | 1H 140, Atl 175, Rzut 140 | 431 | miecz, toporki, tarcza | - | 12 | 98 / 100 |
| Greyjoy Marksman | 5 | S | rod Greyjoy | Luk 140, 1H 140, Atl 175* | 431 | miecz, luk 160 | - | 12 | 111 / 112 |
| Greyjoy Sniper | 6 | S | rod Greyjoy | Luk 250, 1H 220, Atl 220 | 431 | miecz, luk 160 | - | 17 | 98 / 98 |
| Harlaw Deckhand | 1 | P | rod Harlaw | 1H 35, Atl 20 | 80 | topor, buzdygan | - | 2 | 92 / 105 |
| Harlaw Levy | 2 | P | rod Harlaw | 1H 70, Atl 70 | 163 | miecz, tarcza | - | 3 | 107 / 114 |
| Harlaw Footman | 3 | P | rod Harlaw | Dr 105, Atl 140* | 288 | topor, wlocznia, tarcza | - | 5 | 101 / 107 |
| Harlaw Scout | 4 | J | rod Harlaw | Jaz 100, 1H 140 | 425 | miecz, wlocznia, tarcza | Garron 51/18 | 8/12 | 108 / 108 |
| Harlaw Seaman | 4 | P | rod Harlaw | 1H 110, Atl 175*, Rzut 90 | 374 | topor, wlocznia, toporki, tarcza | - | 8 | 97 / 105 |
| Harlaw Archer | 4 | S | rod Harlaw | Luk 100, 1H 90, Atl 140* | 315 | miecz, luk 100 | - | 8 | 102 / 104 |
| Harlaw Raider | 5 | J | rod Harlaw | Jaz 130, 1H 140, Rzut 140 | 511 | miecz, wlocznia, oszczepy, tarcza | Mountain Garron 57/20 | 12/18 | 103 / 107 |
| Harlaw Chief Mate | 5 | P | rod Harlaw | 1H 140, Atl 175, Rzut 140 | 484 | topor, wlocznia, toporki, tarcza | - | 12 | 103 / 106 |
| Harlaw Longbowman | 5 | S | rod Harlaw | Luk 140, 1H 140, Atl 175* | 425 | miecz, luk 160 | - | 12 | 110 / 112 |
| Harlaw Captain | 6 | P | rod Harlaw | 1H 250, Atl 240, Rzut 240 | 541 | topor 2r, topor, toporki, tarcza | - | 17 | 106 / 107 |
| Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 137 | topor, miecz, luk 60 | - | 3 | 141 / 145 |
| Militia Spearman | 2 | P | milicja | 1H 105, Atl 70 | 132 | wlocznia, topor, tarcza | - | 3 | 120 / 130 |
| Militia Veteran Archer | 4 | S | milicja | Luk 140, 1H 110, Atl 130 | 142 | topor, miecz, luk 120 | - | 8 | 102 / 103 |
| Militia Veteran Spearman | 4 | P | milicja | 1H 130, Atl 175* | 386 | wlocznia, topor, tarcza | - | 8 | 103 / 113 |

#### 1.5 Westerlands (Lannisterowie) (`vlandia`)

- Jednostek z droga do wojska: **68** (P 36, S 16, J 16, KL 0). Rekrut wsi `vlandian_recruit`, szlachecki `vlandian_squire`. Sila wzgledna t3+ gra/XML [D]: P 104/104 / S 95/95 / J 102/103 / KL -/-.
- Krolestwo **Korona (Joffrey: Westerlands + Krolewska Przystan)** (rodow 28, szablonow 5): ludzi w partiach lordow 10148, w zalogach 7682 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 36%/34%/31%/0%, swieza partia 60%/27%/13%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 8.8% (doby 1-30: 10.7, 91-120: 7.6), sredni tier 2.52 [P]. Zold na czlowieka: swieza partia 5.64, rozwinieta 9.76 zl/dobe [K+D]. Bitwy lord z lordem [P]: 5, wygrane 1 (20%) - mala proba. Stracone w polu grupy wiesniakow / karawany: 3 / 9; wygrane napady na wiesniakow / karawany wroga: 1 / 25 [P]. Szablony (ludzie): `vlandia` 7093, `clan_lannister` 1437, `crownlands` 975, `clan_clegane` 349, `clan_westerling` 287.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Westerlands Recruit | 1 | P | wies | 1H 20, Atl 20 | 79 | buzdygan, wlocznia | - | 2 | 73 / 104 |
| Westerlands Footman | 2 | P | wies | 1H 70, Atl 105* | 237 | wlocznia, miecz, tarcza | - | 3 | 134 / 124 |
| Westerlands Levy Crossbowman | 2 | S | wies | Kusz 175*A, 1H 70, Atl 70 | 165 | miecz, kusza 120 | - | 3 | 223 / 121 |
| Westerlands Infantry | 3 | P | wies | 1H 105, Atl 140* | 281 | miecz, wlocznia, tarcza | - | 5 | 101 / 100 |
| Westerlands Spearman | 3 | P | wies | 1H 175*T, Atl 140* | 299 | wlocznia, miecz, tarcza | - | 5 | 126 / 103 |
| Westerlands Crossbowman | 3 | S | wies | Kusz 175*A, 1H 70, Atl 70 | 165 | miecz, kusza 160 | - | 5 | 95 / 94 |
| Westerlands Scout | 4 | J | wies | Jaz 100, 1H 140* | 302 | wlocznia, miecz, tarcza | Western Rounsey 53/22 | 8/12 | 96 / 95 |
| Westerlands Axeman | 4 | P | wies | 2H 105, Atl 175* | 324 | topor 2r, miecz | - | 8 | 85 / 89 |
| Westerlands Swordsman | 4 | P | wies | 1H 140, Atl 175* | 324 | miecz, wlocznia, tarcza | - | 8 | 99 / 94 |
| Westerlands Hardened Crossbowman | 4 | S | wies | Kusz 175*A, 1H 100, Atl 100 | 181 | miecz, kusza 270 | - | 8 | 94 / 90 |
| Westerlands Horseman | 5 | J | wies | Jaz 130, 1H 140 | 460 | wlocznia, miecz, tarcza | Western Courser 58/30 | 12/18 | 100 / 101 |
| Westerlands Duelist | 5 | P | wies | 1H 175, Atl 175 | 473 | buzdygan, wlocznia, tarcza | - | 12 | 110 / 101 |
| Westerlands Skirmisher | 5 | P | wies | 1H 130, Atl 175, Rzut 140 | 434 | topor 2r, miecz, oszczepy | - | 12 | 91 / 92 |
| Westerlands Sharpshooter | 5 | S | wies | Kusz 175, 1H 130, Atl 130 | 253 | miecz, kusza 330 | - | 12 | 98 / 94 |
| Westerlands Noble Youth | 2 | P | szlachta | 1H 70, Atl 30 | 94 | wlocznia, buzdygan, tarcza | - | 3 | 80 / 84 |
| Westerlands Squire | 3 | P | szlachta | Dr 140, Atl 140* | 302 | wlocznia, miecz, tarcza | - | 5 | 115 / 108 |
| Westerlands Knight | 4 | J | szlachta | Jaz 100, Dr 140 | 373 | wlocznia, buzdygan, tarcza | Western Rounsey 53/22 | 8/12 | 103 / 106 |
| Westerlands Champion | 5 | J | szlachta | Jaz 130, Dr 160 | 373 | wlocznia, buzdygan, tarcza | Western Courser 58/30 | 12/18 | 97 / 100 |
| Westerlands Banner Knight | 6 | J | szlachta | Jaz 230, Dr 250 | 483 | wlocznia, miecz, tarcza | Western Courser 58/30 | 17/26 | 101 / 101 |
| Casterly Rock Champion | 5 | J | rod Lannister | Jaz 140, 1H 140, Rzut 140 | 549 | wlocznia, miecz, oszczepy, tarcza | Western Courser 58/30 | 12/18 | 110 / 113 |
| Casterly Rock Master Crossbowman | 5 | S | rod Lannister | Kusz 175, 1H 130, Atl 175 | 387 | miecz, kusza 330 | - | 12 | 109 / 106 |
| Guardian of the Rock | 6 | P | rod Lannister | Dr 250, Atl 240, Rzut 180 | 614 | drzewc 2r, miecz, oszczepy, tarcza | - | 17 | 112 / 112 |
| Clegane Recruit | 1 | P | rod Clegane | 1H 20, Atl 20 | 80 | buzdygan, wlocznia | - | 2 | 74 / 105 |
| Clegane Levy | 2 | P | rod Clegane | 1H 70, Atl 70 | 122 | topor, tarcza | - | 3 | 98 / 95 |
| Clegane Footman | 3 | P | rod Clegane | 1H 80, Atl 105 | 212 | topor, tarcza | - | 5 | 78 / 95 |
| Clegane Scout | 4 | J | rod Clegane | Jaz 100, Dr 140 | 306 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 97 / 96 |
| Clegane Man at Arms | 4 | P | rod Clegane | 1H 140, Atl 140 | 357 | wlocznia, topor, tarcza | - | 8 | 98 / 100 |
| Clegane Archer | 4 | S | rod Clegane | Luk 100, 1H 90, Atl 140* | 306 | miecz, luk 120 | - | 8 | 103 / 104 |
| Clegane Brigand | 5 | J | rod Clegane | Jaz 130, Dr 150 | 463 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 103 / 106 |
| Clegane House Guard | 5 | P | rod Clegane | 1H 140, Atl 175 | 463 | miecz, wlocznia, tarcza | - | 12 | 101 / 101 |
| Clegane Elite Archer | 5 | S | rod Clegane | Luk 130, 1H 110, Atl 140 | 357 | miecz, luk 120 | - | 12 | 95 / 99 |
| Mountain's Man | 6 | P | rod Clegane | 2H 250, Atl 240, Rzut 220 | 463 | miecz 2r, wlocznia, toporki | - | 17 | 97 / 98 |
| Lannister Recruit | 1 | P | rod Lannister | 1H 70*, Atl 20 | 94 | buzdygan, miecz | - | 2 | 140 / 114 |
| Lannister Levy | 2 | P | rod Lannister | 1H 70, Atl 140* | 271 | wlocznia, miecz, tarcza | - | 3 | 151 / 144 |
| Lannister Footman | 3 | P | rod Lannister | 1H 140*T, Atl 140* | 284 | miecz, wlocznia, tarcza | - | 5 | 112 / 107 |
| Lannister Bowman | 3 | S | rod Lannister | Luk 70, 1H 70, Atl 70 | 182 | miecz, luk 70 | - | 5 | 92 / 92 |
| Lannister Horseman | 4 | J | rod Lannister | Jaz 100, 1H 140 | 435 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 109 / 112 |
| Lannister Man at Arms | 4 | P | rod Lannister | 1H 140, Atl 175* | 464 | miecz, wlocznia, tarcza | - | 8 | 114 / 116 |
| Lannister Archer | 4 | S | rod Lannister | Luk 100, 1H 90, Atl 90 | 182 | miecz, luk 100 | - | 8 | 85 / 87 |
| Lannister Knight | 5 | J | rod Lannister | Jaz 140, 1H 140 | 506 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 106 / 109 |
| Lannister House Guard | 5 | P | rod Lannister | 1H 140, Atl 175 | 506 | miecz, wlocznia, tarcza | - | 12 | 105 / 108 |
| Lannister Officer | 5 | P | rod Lannister | Dr 175, Atl 175 | 502 | drzewc 2r, miecz, tarcza | - | 12 | 112 / 107 |
| Lannister Longbowman | 5 | S | rod Lannister | Luk 130, 1H 110, Atl 110 | 182 | miecz, luk 120 | - | 12 | 78 / 81 |
| Lannister Prideknight | 6 | J | rod Lannister | Jaz 240, Dr 240 | 502 | miecz, wlocznia, tarcza | Western Destrier 64/32 | 17/26 | 103 / 103 |
| Westerling Recruit | 1 | P | rod Westerling | 1H 35, Atl 20 | 73 | miecz | - | 2 | 89 / 100 |
| Westerling Levy | 2 | P | rod Westerling | 1H 50, Atl 105* | 276 | miecz, tarcza | - | 3 | 131 / 146 |
| Westerling Footman | 3 | P | rod Westerling | 1H 105, Atl 105 | 289 | buzdygan, wlocznia, tarcza | - | 5 | 96 / 108 |
| Westerling Scout | 4 | J | rod Westerling | Jaz 100, Dr 100 | 289 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 85 / 94 |
| Westerling Man at Arms | 4 | P | rod Westerling | 1H 140, Atl 175* | 493 | topor, wlocznia, tarcza | - | 8 | 117 / 117 |
| Westerling Archer | 4 | S | rod Westerling | Luk 100, 1H 105, Atl 105 | 289 | buzdygan, luk 100 | - | 8 | 101 / 101 |
| Westerling Knight | 5 | J | rod Westerling | Jaz 130, 1H 175* | 545 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 12/18 | 113 / 109 |
| Westerling House Guard | 5 | P | rod Westerling | 1H 140, Atl 175 | 545 | topor, wlocznia, tarcza | - | 12 | 108 / 112 |
| Westerling Elite Archer | 5 | S | rod Westerling | Luk 140, 1H 120, Atl 140 | 314 | buzdygan, luk 125 | - | 12 | 95 / 99 |
| Westerling Hedgeknight | 6 | P | rod Westerling | 2H 250, Atl 250 | 584 | topor, wlocznia, tarcza | - | 17 | 110 / 111 |
| Casterly Rock Guard | 2 | P | kasztelan | 1H 140*T, Atl 70 | 180 | wlocznia, miecz, tarcza | - | 3 | 150 / 119 |
| Casterly Rock Squire | 3 | J | kasztelan | Jaz 80, 1H 105 | 262 | buzdygan, wlocznia, tarcza | Western Rounsey 53/22 | 5/8 | 96 / 105 |
| Casterly Rock Soldier | 3 | P | kasztelan | 1H 140*T, Atl 140* | 292 | wlocznia, miecz, tarcza | - | 5 | 113 / 108 |
| Casterly Rock Knight | 4 | J | kasztelan | Jaz 110, Dr 140 | 572 | wlocznia, buzdygan, tarcza | Hunter 55/7 | 8/12 | 123 / 128 |
| Casterly Rock Pikeman | 4 | P | kasztelan | 1H 140, Atl 175* | 572 | drzewc 2r, miecz, tarcza | - | 8 | 126 / 127 |
| Casterly Rock Crossbowman | 4 | S | kasztelan | Kusz 175*A, 1H 100, Atl 175* | 387 | miecz, kusza 270 | - | 8 | 112 / 113 |
| Casterly Rock Marshal | 5 | P | kasztelan | Dr 175, Atl 175 | 614 | drzewc 2r, miecz, tarcza | - | 12 | 123 / 119 |
| Westerlands Militia Archer | 2 | S | milicja | Kusz 175*A, 1H 50, Atl 70 | 105 | topor, buzdygan, kusza 120 | - | 3 | 203 / 145 |
| Westerlands Militia Spearman | 2 | P | milicja | 1H 175*T, Atl 60 | 126 | wlocznia, topor, tarcza | - | 3 | 155 / 128 |
| Westerlands Militia Veteran Archer | 4 | S | milicja | Kusz 175, 1H 110, Atl 130 | 153 | miecz, kusza 230 | - | 8 | 90 / 100 |
| Westerlands Militia Veteran Spearman | 4 | P | milicja | 1H 175, Atl 110 | 153 | wlocznia, topor, tarcza | - | 8 | 82 / 84 |
| Brotherhood Fighter | 2 | P | najemnicy Brotherhood without Banners | 1H 70, Atl 70 | 179 | wlocznia, miecz, tarcza | - | 3 | 111 / 118 |
| Brotherhood Hunter | 3 | S | najemnicy Brotherhood without Banners | Luk 90, 1H 110, Atl 100 | 153 | miecz, luk 120 | - | 5 | 111 / 112 |
| Knight of Hollow Hill | 4 | J | najemnicy Brotherhood without Banners | Jaz 130, 1H 140 | 286 | wlocznia, miecz, tarcza | Western Courser 58/30 | 8/12 | 104 / 111 |

#### 1.6 Ziemie Korony (Krolewska Przystan) (`crownlands`)

- Jednostek z droga do wojska: **25** (P 14, S 6, J 5, KL 0). Rekrut wsi `crownlands_recruit`, szlachecki `crownlands_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 92/98 / S 104/106 / J 97/100 / KL -/-.
- Uwaga: Ziemie Korony nie maja wlasnego krolestwa - 1 rod z tym szablonem jest w krolestwie Joffreya (patrz Westerlands).

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Crownlands Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Crownlands Levy | 2 | P | wies | 1H 70, Atl 70 | 152 | miecz | - | 3 | 93 / 86 |
| Crownlands Bowman | 2 | S | wies | Luk 40, 1H 70, Atl 70 | 136 | miecz, luk 60 | - | 3 | 104 / 100 |
| Gold Cloak Soldier | 3 | P | wies | 1H 70, Atl 140* | 314 | miecz, wlocznia, tarcza | - | 5 | 93 / 105 |
| Gold Cloak Archer | 3 | S | wies | Luk 70, 1H 70, Atl 140* | 314 | miecz, luk 60 | - | 5 | 113 / 112 |
| Gold Cloak Rider | 4 | J | wies | Jaz 100, Dr 100 | 365 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 93 / 102 |
| Gold Cloak Petty Officer | 4 | P | wies | 1H 140, Atl 140, Rzut 100 | 372 | topor 2r, miecz, toporki, tarcza | - | 8 | 100 / 105 |
| Gold Cloak Elite Archer | 4 | S | wies | Luk 100, 1H 90, Atl 140* | 314 | miecz, luk 100 | - | 8 | 102 / 104 |
| Gold Cloak Captain | 5 | J | wies | Jaz 140, Dr 140 | 384 | miecz, wlocznia, tarcza | Charger 54/26 | 12/18 | 95 / 98 |
| Gold Cloak Halberdier | 5 | P | wies | Dr 175, Atl 140 | 384 | miecz, drzewc 2r, tarcza | - | 12 | 97 / 96 |
| Gold Cloak Sniper | 5 | S | wies | Luk 140, 1H 110, Atl 140 | 365 | miecz, luk 100 | - | 12 | 98 / 102 |
| Crownlands Noble's Son | 2 | P | szlachta | 1H 70, Atl 40 | 113 | miecz | - | 3 | 76 / 89 |
| Crownlands Squire | 3 | P | szlachta | 1H 70, Atl 70 | 150 | miecz, wlocznia, tarcza | - | 5 | 60 / 78 |
| Realm Hedge Knight | 4 | P | szlachta | 1H 140, Atl 175* | 406 | miecz, wlocznia, tarcza | - | 8 | 108 / 106 |
| Realm Knight | 5 | J | szlachta | Jaz 130, 1H 140, Rzut 140 | 475 | miecz, wlocznia, oszczepy, tarcza | Western Rounsey 53/22 | 12/18 | 100 / 101 |
| Realm Paladin | 6 | J | szlachta | Jaz 230, 1H 230, Rzut 230 | 524 | miecz, wlocznia, oszczepy, tarcza | Western Courser 58/30 | 17/26 | 100 / 100 |
| Kingsguard's Page | 2 | P | kasztelan | 1H 70, Atl 50 | 90 | miecz | - | 3 | 74 / 79 |
| Kingsguard's Squire | 3 | P | kasztelan | 1H 80, Atl 80 | 150 | miecz, wlocznia, tarcza | - | 5 | 65 / 84 |
| Kingsguard Initiate | 4 | P | kasztelan | 1H 140, Atl 175* | 447 | miecz, wlocznia, tarcza | - | 8 | 112 / 114 |
| Kingsguard | 5 | P | kasztelan | 2H 140, Atl 175 | 600 | miecz, wlocznia, tarcza | - | 12 | 113 / 117 |
| Captain of the Kingsguard | 6 | J | kasztelan | Jaz 250, 2H 270 | 600 | miecz, wlocznia, tarcza | Western Destrier 64/32 | 17/26 | 114 / 114 |
| City Watch Crossbowman | 2 | S | milicja | Kusz 175*A, 1H 50, Atl 140* | 188 | topor, buzdygan, kusza 120 | - | 3 | 224 / 170 |
| City Watch Spearman | 2 | P | milicja | 1H 175*T, Atl 140* | 314 | wlocznia, topor, tarcza | - | 3 | 220 / 182 |
| City Watch Veteran Crossbowman | 4 | S | milicja | Kusz 175, 1H 110, Atl 140 | 314 | miecz, kusza 230 | - | 8 | 104 / 118 |
| City Watch Veteran Spearman | 4 | P | milicja | 1H 175, Atl 140 | 365 | wlocznia, topor, tarcza | - | 8 | 108 / 110 |

#### 1.7 Reach (Tyrellowie) (`reach`)

- Jednostek z droga do wojska: **51** (P 29, S 12, J 10, KL 0). Rekrut wsi `reach_recruit`, szlachecki `reach_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 100/103 / S 102/101 / J 97/99 / KL -/-.
- Krolestwo **Reach** (rodow 17, szablonow 4): ludzi w partiach lordow 6929, w zalogach 6358 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 33%/36%/32%/0%, swieza partia 60%/26%/14%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 7.9% (doby 1-30: 10.0, 91-120: 7.7), sredni tier 2.52 [P]. Zold na czlowieka: swieza partia 5.95, rozwinieta 10.09 zl/dobe [K+D]. Bitwy lord z lordem [P]: 37, wygrane 21 (57%). Stracone w polu grupy wiesniakow / karawany: 7 / 14; wygrane napady na wiesniakow / karawany wroga: 8 / 14 [P]. Szablony (ludzie): `reach` 4066, `clan_tyrell` 1186, `clan_hightower` 954, `clan_tarly` 723.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Reach Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Reach Levy | 2 | P | wies | 1H 70, Atl 70 | 152 | miecz | - | 3 | 93 / 86 |
| Reach Bowman | 2 | S | wies | Luk 40, 1H 70, Atl 70 | 136 | miecz, luk 60 | - | 3 | 104 / 100 |
| Reach Soldier | 3 | P | wies | 1H 70, Atl 105 | 267 | miecz, wlocznia, tarcza | - | 5 | 82 / 98 |
| Reach Archer | 3 | S | wies | Luk 70, 1H 70, Atl 70 | 152 | miecz, luk 60 | - | 5 | 85 / 84 |
| Reach Rider | 4 | J | wies | Jaz 100, Dr 100 | 291 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 85 / 94 |
| Reach Man at Arms | 4 | P | wies | 1H 140, Atl 140 | 317 | wlocznia, miecz, tarcza | - | 8 | 94 / 93 |
| Reach Elite Archer | 4 | S | wies | Luk 100, 1H 90, Atl 105 | 248 | miecz, luk 100 | - | 8 | 94 / 95 |
| Reach Horseman | 5 | J | wies | Jaz 140, Dr 140 | 398 | miecz, wlocznia, tarcza | Charger 54/26 | 12/18 | 97 / 100 |
| Reach Axeman | 5 | P | wies | 2H 130, Atl 175 | 398 | wlocznia, topor 2r | - | 12 | 88 / 88 |
| Reach House Guard | 5 | P | wies | 1H 140, Atl 175 | 398 | wlocznia, miecz, tarcza | - | 12 | 95 / 93 |
| Reach Master Archer | 5 | S | wies | Luk 140, 1H 140, Atl 120 | 291 | miecz, luk 100 | - | 12 | 93 / 94 |
| Reach Noble's Son | 2 | P | szlachta | 1H 70, Atl 40 | 89 | miecz | - | 3 | 71 / 82 |
| Reach Voulgier | 3 | P | szlachta | Dr 140*, Atl 105 | 248 | drzewc 2r, luk 60 | - | 5 | 95 / 92 |
| Reach Hedge Knight | 4 | P | szlachta | 1H 140*T, Atl 175* | 464 | drzewc 2r, miecz, tarcza | - | 8 | 114 / 116 |
| Reach Champion | 5 | P | szlachta | 1H 140*T, Atl 175 | 549 | drzewc 2r, miecz, tarcza | - | 12 | 109 / 112 |
| Reach Flower Knight | 6 | J | szlachta | Jaz 230, Dr 230 | 549 | drzewc 2r, miecz, tarcza | Charger 54/26 | 17/26 | 101 / 101 |
| Hightower Recruit | 1 | P | rod Hightower | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Hightower Levy | 2 | P | rod Hightower | 1H 70, Atl 105* | 271 | miecz, wlocznia | - | 3 | 130 / 130 |
| Hightower Soldier | 3 | P | rod Hightower | 1H 105, Atl 175* | 341 | miecz, wlocznia, tarcza | - | 5 | 114 / 116 |
| Hightower Horseman | 4 | J | rod Hightower | Jaz 100, Dr 140 | 392 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 105 / 105 |
| Hightower Guard | 4 | P | rod Hightower | 1H 140, Atl 175* | 392 | wlocznia, miecz, tarcza | - | 8 | 107 / 107 |
| Hightower Crossbowman | 4 | S | rod Hightower | Kusz 175*A, 1H 100, Atl 175* | 341 | miecz, kusza 300 | - | 8 | 110 / 110 |
| Hightower Cavalry | 5 | J | rod Hightower | Jaz 130, 1H 140 | 485 | miecz, wlocznia, tarcza | Hunter 55/7 | 12/18 | 100 / 103 |
| Hightower Captain | 5 | P | rod Hightower | 1H 140, Atl 175 | 560 | wlocznia, miecz, tarcza | - | 12 | 110 / 113 |
| Hightower Marksmen | 5 | S | rod Hightower | Kusz 175, 1H 175, Atl 175* | 392 | miecz, kusza 330 | - | 12 | 113 / 107 |
| Guardian of Oldtown | 6 | P | rod Hightower | 1H 230, Atl 240 | 560 | wlocznia, drzewc 2r, tarcza | - | 17 | 104 / 105 |
| Tarly Recruit | 1 | P | rod Tarly | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Tarly Footman | 2 | P | rod Tarly | 1H 70, Atl 105* | 177 | miecz, wlocznia | - | 3 | 109 / 96 |
| Tarly Soldier | 3 | P | rod Tarly | 1H 70, Atl 105 | 266 | miecz, wlocznia, tarcza | - | 5 | 81 / 99 |
| Tarly Horseman | 4 | J | rod Tarly | Jaz 100, 1H 140 | 334 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 99 / 99 |
| Tarly Man at Arms | 4 | P | rod Tarly | 1H 140, Atl 175* | 334 | wlocznia, miecz, tarcza | - | 8 | 101 / 97 |
| Tarly Crossbowman | 4 | S | rod Tarly | Kusz 175*A, 1H 100, Atl 105 | 320 | miecz, kusza 270 | - | 8 | 106 / 106 |
| Tarly Knight | 5 | J | rod Tarly | Jaz 130, 1H 140 | 471 | miecz, wlocznia, tarcza | Hunter 55/7 | 12/18 | 99 / 102 |
| Tarly House Guard | 5 | P | rod Tarly | 1H 140, Atl 175 | 471 | wlocznia, miecz, tarcza | - | 12 | 101 / 102 |
| Tarly Elite Crossbowman | 5 | S | rod Tarly | Kusz 175, 1H 130, Atl 130 | 338 | miecz, kusza 330 | - | 12 | 105 / 102 |
| Tarly Vanguard | 6 | P | rod Tarly | 1H 240, Atl 240, Rzut 230 | 477 | wlocznia, miecz, oszczepy, tarcza | - | 17 | 100 / 101 |
| Tyrell Recruit | 1 | P | rod Tyrell | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Tyrell Footman | 2 | P | rod Tyrell | 1H 70, Atl 70 | 177 | miecz, wlocznia | - | 3 | 99 / 96 |
| Tyrell Soldier | 3 | P | rod Tyrell | 1H 90, Atl 140* | 274 | miecz, wlocznia, tarcza | - | 5 | 95 / 101 |
| Tyrell Scout | 4 | J | rod Tyrell | Jaz 110, Dr 140 | 272 | miecz, wlocznia, tarcza | Andalos Palfrey 53/20 | 8/12 | 96 / 95 |
| Tyrell Man at Arms | 4 | P | rod Tyrell | 1H 140, Atl 175* | 468 | wlocznia, miecz, tarcza | - | 8 | 115 / 114 |
| Tyrell Longbowman | 4 | S | rod Tyrell | Luk 110, 1H 100, Atl 140* | 272 | miecz, luk 120 | - | 8 | 104 / 106 |
| Tyrell Horseman | 5 | J | rod Tyrell | Jaz 140, 1H 140 | 387 | miecz, wlocznia, tarcza | Hunter 55/7 | 12/18 | 94 / 97 |
| Tyrell House Guard | 5 | P | rod Tyrell | 1H 140, Atl 175 | 468 | wlocznia, miecz, tarcza | - | 12 | 101 / 100 |
| Tyrell Elite Longbowman | 5 | S | rod Tyrell | Luk 140, 1H 140, Atl 175 | 387 | miecz, luk 160 | - | 12 | 107 / 109 |
| Tyrell Cavalier | 6 | J | rod Tyrell | Jaz 240, Dr 230 | 468 | miecz, wlocznia, tarcza | Charger 54/26 | 17/26 | 98 / 98 |
| Reach Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 122 | miecz, buzdygan, luk 90 | - | 3 | 142 / 147 |
| Reach Militia Spearman | 2 | P | milicja | 1H 80, Atl 70 | 140 | wlocznia, topor, tarcza | - | 3 | 108 / 132 |
| Reach Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 140 | topor, miecz, luk 90 | - | 5 | 111 / 112 |
| Reach Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 175* | 343 | wlocznia, miecz, tarcza | - | 5 | 113 / 125 |

#### 1.8 Krainy Burzy (`stormlands`)

- Jednostek z droga do wojska: **54** (P 28, S 15, J 11, KL 0). Rekrut wsi `stormlands_recruit`, szlachecki `stormlands_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 97/98 / S 96/96 / J 98/100 / KL -/-.
- Krolestwo **Krainy Burzy (Renly)** (rodow 14, szablonow 4): ludzi w partiach lordow 5401, w zalogach 3606 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 40%/35%/25%/0%, swieza partia 63%/26%/11%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 8.1% (doby 1-30: 9.2, 91-120: 7.2), sredni tier 2.43 [P]. Zold na czlowieka: swieza partia 5.83, rozwinieta 9.85 zl/dobe [K+D]. Bitwy lord z lordem [P]: 120, wygrane 54 (45%). Stracone w polu grupy wiesniakow / karawany: 57 / 38; wygrane napady na wiesniakow / karawany wroga: 19 / 5 [P]. Szablony (ludzie): `stormlands` 3506, `clan_baratheon` 870, `clan_tarth` 638, `clan_dondarion` 387.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Stormlands Recruit | 1 | P | wies | 1H 20, Atl 20 | 73 | buzdygan | - | 2 | 71 / 100 |
| Stormlands Levy | 2 | P | wies | 1H 40, Atl 70 | 129 | buzdygan, tarcza | - | 3 | 83 / 93 |
| Stormlands Bowman | 2 | S | wies | Luk 40, 1H 30, Atl 70 | 143 | buzdygan, luk 60 | - | 3 | 95 / 102 |
| Stormlands Man at Arms | 3 | P | wies | Dr 105, Atl 140* | 319 | buzdygan, wlocznia, tarcza | - | 5 | 105 / 110 |
| Stormlands Archer | 3 | S | wies | Luk 70, 1H 50, Atl 140* | 267 | buzdygan, luk 60 | - | 5 | 102 / 102 |
| Stormlands Maceman | 4 | P | wies | 1H 100, Atl 175*, Rzut 105 | 376 | buzdygan, oszczepy, tarcza | - | 8 | 94 / 101 |
| Stormlands Spearman | 4 | P | wies | Dr 140, Atl 175* | 376 | buzdygan, wlocznia, tarcza | - | 8 | 105 / 101 |
| Stormlands Crossbowman | 4 | S | wies | Kusz 175*A, 1H 70, Atl 140* | 317 | buzdygan, kusza 120 | - | 8 | 94 / 91 |
| Stormlands Elite Archer | 4 | S | wies | Luk 100, 1H 70, Atl 140* | 312 | buzdygan, luk 100 | - | 8 | 99 / 101 |
| Stormlands Horseman | 5 | J | wies | Jaz 130, Dr 140 | 376 | buzdygan, wlocznia, tarcza | Western Rounsey 53/22 | 12/18 | 92 / 93 |
| Stormlands Elite Maceman | 5 | P | wies | 1H 130, Atl 175, Rzut 120 | 404 | buzdygan, oszczepy, tarcza | - | 12 | 93 / 94 |
| Stormlands House Guard | 5 | P | wies | 1H 140, Atl 175 | 404 | miecz, wlocznia, tarcza | - | 12 | 95 / 94 |
| Stormlands Heavy Crossbowman | 5 | S | wies | Kusz 175, 1H 100, Atl 175* | 376 | buzdygan, kusza 200 | - | 12 | 98 / 98 |
| Stormlands Master Archer | 5 | S | wies | Luk 130, 1H 90, Atl 175* | 376 | buzdygan, luk 100 | - | 12 | 94 / 97 |
| Stormlands Noble's Son | 2 | P | szlachta | 1H 70, Atl 30 | 82 | miecz | - | 3 | 66 / 77 |
| Stormlands Squire | 3 | P | szlachta | 2H 105, Atl 70 | 150 | miecz 2r, wlocznia | - | 5 | 65 / 69 |
| Stormlands Knight | 4 | J | szlachta | Jaz 100, 2H 105 | 376 | miecz 2r, wlocznia | Western Rounsey 53/22 | 8/12 | 95 / 103 |
| Stormlands Fell Knight | 5 | J | szlachta | Jaz 130, 2H 140 | 404 | miecz 2r, wlocznia | Western Rounsey 53/22 | 12/18 | 95 / 98 |
| Stormlands Thunder Knight | 6 | J | szlachta | Jaz 230, 2H 230 | 404 | miecz 2r, wlocznia | Courser 55/22 | 17/26 | 93 / 93 |
| Baratheon Recruit | 1 | P | rod Baratheon/Renly | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Baratheon Footman | 2 | P | rod Baratheon/Renly | 1H 70, Atl 70 | 177 | miecz, tarcza | - | 3 | 110 / 118 |
| Baratheon Soldier | 3 | P | rod Baratheon/Renly | Dr 105, Atl 105 | 257 | miecz, wlocznia, tarcza | - | 5 | 92 / 102 |
| Baratheon Bowman | 3 | S | rod Baratheon/Renly | Luk 80, 1H 50, Atl 70 | 177 | buzdygan, luk 60 | - | 5 | 92 / 93 |
| Baratheon Horseman | 4 | J | rod Baratheon/Renly | Jaz 100, Dr 140 | 379 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 104 / 104 |
| Baratheon Hammerman | 4 | P | rod Baratheon/Renly | Dr 175*, Atl 175* | 348 | drzewc 2r | - | 8 | 106 / 86 |
| Baratheon Archer | 4 | S | rod Baratheon/Renly | Luk 110, 1H 70, Atl 70 | 206 | buzdygan, luk 100 | - | 8 | 90 / 92 |
| Baratheon Knight | 5 | J | rod Baratheon/Renly | Jaz 130, Dr 140 | 464 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 101 / 104 |
| Baratheon House Guard | 5 | P | rod Baratheon/Renly | Dr 175*, Atl 175 | 479 | miecz, drzewc 2r, tarcza | - | 12 | 110 / 105 |
| Baratheon Longbowman | 5 | S | rod Baratheon/Renly | Luk 140, 1H 90, Atl 90 | 222 | buzdygan, luk 120 | - | 12 | 83 / 87 |
| Baratheon Hammerknight | 6 | P | rod Baratheon/Renly | Dr 240, Atl 230 | 479 | drzewc 2r, miecz, tarcza | - | 17 | 100 / 100 |
| Dondarrion Recruit | 1 | P | rod Dondarrion | 1H 20, Atl 20 | 73 | buzdygan | - | 2 | 71 / 100 |
| Dondarrion Levy | 2 | P | rod Dondarrion | 1H 70, Atl 70 | 169 | miecz, tarcza | - | 3 | 108 / 115 |
| Dondarrion Footman | 3 | P | rod Dondarrion | Dr 105, Atl 140* | 228 | miecz, wlocznia, tarcza | - | 5 | 94 / 97 |
| Dondarrion Horseman | 4 | J | rod Dondarrion | Jaz 100, Dr 140 | 297 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 96 / 95 |
| Dondarrion Man at Arms | 4 | P | rod Dondarrion | 1H 140, Atl 140 | 186 | buzdygan, wlocznia, tarcza | - | 8 | 80 / 81 |
| Dondarrion Bowman | 4 | S | rod Dondarrion | Luk 100, 1H 70, Atl 140* | 294 | buzdygan, luk 100 | - | 8 | 97 / 99 |
| Dondarrion Knight | 5 | J | rod Dondarrion | Jaz 130, 1H 140 | 537 | buzdygan, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 107 / 108 |
| Dondarrion House Guard | 5 | P | rod Dondarrion | Dr 175, Atl 175 | 537 | drzewc 2r, buzdygan, tarcza | - | 12 | 116 / 110 |
| Dondarrion Veteran Bowman | 5 | S | rod Dondarrion | Luk 130, 1H 90, Atl 140* | 321 | buzdygan, luk 100 | - | 12 | 88 / 92 |
| Dondarrion Boltknight | 6 | J | rod Dondarrion | Jaz 230, 1H 240 | 573 | buzdygan, wlocznia, tarcza | Mountain Destrier 62/27 | 17/26 | 105 / 105 |
| Tarth Recruit | 1 | P | rod Tarth | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Tarth Militia | 2 | P | rod Tarth | 1H 70, Atl 105* | 177 | miecz, tarcza | - | 3 | 120 / 118 |
| Tarth Man at Arms | 3 | P | rod Tarth | Dr 105, Atl 105 | 266 | miecz, wlocznia, tarcza | - | 5 | 93 / 104 |
| Tarth Rider | 4 | J | rod Tarth | Jaz 100, Dr 140 | 266 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 93 / 92 |
| Tarth Soldier | 4 | P | rod Tarth | Dr 110, Atl 175* | 387 | miecz, wlocznia, tarcza | - | 8 | 98 / 107 |
| Tarth Crossbowman | 4 | S | rod Tarth | Kusz 175*A, 1H 100, Atl 105 | 266 | miecz, kusza 300 | - | 8 | 103 / 102 |
| Tarth Horseman | 5 | J | rod Tarth | Jaz 130, Dr 140 | 453 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 12/18 | 99 / 100 |
| Tarth Halberdier | 5 | P | rod Tarth | Dr 175, Atl 175 | 441 | drzewc 2r, miecz, tarcza | - | 12 | 107 / 100 |
| Tarth Elite Crossbowman | 5 | S | rod Tarth | Kusz 175, 1H 175, Atl 120 | 320 | miecz, kusza 330 | - | 12 | 107 / 100 |
| Tarth Master Halberdier | 6 | P | rod Tarth | Dr 240, Atl 230 | 441 | drzewc 2r, miecz, tarcza | - | 17 | 97 / 97 |
| Stormlands Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 122 | miecz, buzdygan, luk 90 | - | 3 | 142 / 147 |
| Stormlands Militia Spearman | 2 | P | milicja | 1H 80, Atl 70 | 140 | wlocznia, topor, tarcza | - | 3 | 108 / 132 |
| Stormlands Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 140 | topor, miecz, luk 90 | - | 5 | 111 / 112 |
| Stormlands Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 175* | 343 | wlocznia, miecz, tarcza | - | 5 | 113 / 125 |

#### 1.9 Smocza Skala (`dragonstone`)

- Jednostek z droga do wojska: **41** (P 23, S 9, J 9, KL 0). Rekrut wsi `dragonstone_recruit`, szlachecki `dragonstone_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 105/102 / S 97/97 / J 103/103 / KL -/-.
- Krolestwo **Smocza Skala (Stannis)** (rodow 12, szablonow 4): ludzi w partiach lordow 4953, w zalogach 3297 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 29%/46%/25%/0%, swieza partia 61%/25%/14%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 11.5% (doby 1-30: 11.2, 91-120: 10.4), sredni tier 2.66 [P]. Zold na czlowieka: swieza partia 6.28, rozwinieta 9.36 zl/dobe [K+D]. Bitwy lord z lordem [P]: 107, wygrane 61 (57%). Stracone w polu grupy wiesniakow / karawany: 18 / 4; wygrane napady na wiesniakow / karawany wroga: 55 / 35 [P]. W liczbach sa rody najemne: Wild Hares 433 ludzi (9%). Szablony (ludzie): `dragonstone` 3436, `clan_velaryon` 577, `clan_celtigar` 507, `forest_people` 433.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Dragonstone Recruit | 1 | P | wies | 1H 35, Atl 20 | 73 | topor | - | 2 | 89 / 100 |
| Dragonstone Footman | 2 | P | wies | 1H 40, Atl 70 | 139 | topor | - | 3 | 74 / 82 |
| Dragonstone Soldier | 3 | P | wies | 1H 140*T, Atl 140* | 301 | topor, wlocznia, tarcza | - | 5 | 115 / 103 |
| Dragonstone Bowman | 3 | S | wies | Luk 70, 1H 70, Atl 70 | 139 | miecz, luk 90 | - | 5 | 87 / 85 |
| Dragonstone Rider | 4 | J | wies | Jaz 100, 1H 140* | 316 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 98 / 97 |
| Dragonstone Halberdier | 4 | P | wies | 1H 140*T, Atl 175* | 397 | drzewc 2r, topor, tarcza | - | 8 | 107 / 106 |
| Dragonstone Man at Arms | 4 | P | wies | 1H 140, Atl 175* | 397 | miecz, wlocznia, tarcza | - | 8 | 107 / 103 |
| Dragonstone Archer | 4 | S | wies | Luk 100, 1H 80, Atl 80 | 160 | miecz, luk 110 | - | 8 | 82 / 83 |
| Dragonstone Horseman | 5 | J | wies | Jaz 130, 1H 140 | 397 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 95 / 96 |
| Dragonstone Elite Halberdier | 5 | P | wies | 1H 140, Atl 175 | 451 | drzewc 2r, topor, tarcza | - | 12 | 100 / 101 |
| Dragonstone House Guard | 5 | P | wies | 1H 140, Atl 175 | 451 | miecz, wlocznia, tarcza | - | 12 | 100 / 100 |
| Dragonstone Elite Archer | 5 | S | wies | Luk 130, 1H 140, Atl 140 | 316 | miecz, luk 110 | - | 12 | 93 / 93 |
| Dragonstone Noble's Son | 2 | P | szlachta | 1H 70, Atl 30 | 82 | miecz | - | 3 | 66 / 77 |
| Squire | 3 | P | szlachta | 1H 140*, Atl 70, Rzut 105 | 151 | miecz, oszczepy, tarcza | - | 5 | 84 / 82 |
| Dragonstone Knight | 4 | J | szlachta | Jaz 100, 1H 140*, Rzut 105 | 451 | miecz, wlocznia, oszczepy, tarcza | Western Rounsey 53/22 | 8/12 | 111 / 111 |
| Dragonstone Shock Knight | 5 | J | szlachta | Jaz 130, 1H 140, Rzut 140 | 491 | miecz, wlocznia, oszczepy, tarcza | Western Rounsey 53/22 | 12/18 | 102 / 101 |
| Queen's Man | 6 | J | szlachta | Jaz 230, Dr 230, Rzut 230 | 491 | miecz, wlocznia, oszczepy, tarcza | Western Courser 58/30 | 17/26 | 99 / 99 |
| Celtigar Recruit | 1 | P | rod Celtigar | 1H 20, Atl 20 | 73 | buzdygan, wlocznia | - | 2 | 71 / 100 |
| Celtigar Levy | 2 | P | rod Celtigar | 1H 40, Atl 105* | 279 | topor | - | 3 | 115 / 125 |
| Celtigar Footman | 3 | P | rod Celtigar | 1H 140*T, Atl 105 | 307 | topor, wlocznia, tarcza | - | 5 | 110 / 106 |
| Celtigar Horseman | 4 | J | rod Celtigar | Jaz 100, 1H 140*T | 447 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 110 / 111 |
| Celtigar Man at Arms | 4 | P | rod Celtigar | 1H 140, Atl 110 | 341 | miecz, wlocznia, tarcza | - | 8 | 93 / 98 |
| Celtigar Archer | 4 | S | rod Celtigar | Luk 100, 1H 100, Atl 105 | 307 | miecz, luk 120 | - | 8 | 104 / 106 |
| Celtigar Knight | 5 | J | rod Celtigar | Jaz 130, 1H 140 | 553 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 108 / 111 |
| Celtigar Halberdier | 5 | P | rod Celtigar | Dr 175, Atl 175 | 553 | drzewc 2r, miecz, tarcza | - | 12 | 117 / 113 |
| Celtigar Veteran Archer | 5 | S | rod Celtigar | Luk 130, 1H 130, Atl 130 | 341 | miecz, luk 120 | - | 12 | 95 / 99 |
| Celtigar Banneret | 6 | P | rod Celtigar | Dr 240, Atl 240, Rzut 230 | 571 | drzewc 2r, miecz, oszczepy, tarcza | - | 17 | 107 / 107 |
| Velaryon Recruit | 1 | P | rod Velaryon | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Velaryon Shipmate | 2 | P | rod Velaryon | 1H 40, Atl 140* | 276 | topor | - | 3 | 124 / 125 |
| Velaryon Sailor | 3 | P | rod Velaryon | 1H 140*T, Atl 140* | 274 | topor, wlocznia, tarcza | - | 5 | 111 / 101 |
| Velaryon Scout | 4 | J | rod Velaryon | Jaz 100, 1H 140*T | 303 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 96 / 96 |
| Velaryon Marine | 4 | P | rod Velaryon | 1H 175*, Atl 175* | 384 | miecz, wlocznia, tarcza | - | 8 | 115 / 103 |
| Velaryon Crossbowman | 4 | S | rod Velaryon | Kusz 175*A, 1H 100, Atl 140 | 301 | miecz, kusza 300 | - | 8 | 106 / 106 |
| Velaryon Horseman | 5 | J | rod Velaryon | Jaz 130, 1H 175* | 469 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 108 / 102 |
| Velaryon Renegade | 5 | P | rod Velaryon | 1H 175, Atl 175 | 469 | miecz, wlocznia, tarcza | - | 12 | 109 / 104 |
| Velaryon Marksman | 5 | S | rod Velaryon | Kusz 175, 1H 175, Atl 175 | 384 | miecz, kusza 330 | - | 12 | 112 / 106 |
| Velaryon Sea Guard | 6 | P | rod Velaryon | 1H 230, Atl 240, Rzut 230 | 469 | miecz, topor 2r, toporki, tarcza | - | 17 | 98 / 98 |
| Dragonstone Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 122 | miecz, buzdygan, luk 90 | - | 3 | 142 / 147 |
| Dragonstone Militia Spearman | 2 | P | milicja | 1H 80, Atl 70 | 140 | wlocznia, topor, tarcza | - | 3 | 108 / 132 |
| Dragonstone Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 140 | topor, miecz, luk 90 | - | 5 | 111 / 112 |
| Dragonstone Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 175* | 347 | wlocznia, miecz, tarcza | - | 5 | 113 / 126 |

#### 1.10 Dorne (`aserai`)

- Jednostek z droga do wojska: **56** (P 28, S 13, J 12, KL 3). Rekrut wsi `aserai_recruit`, szlachecki `aserai_youth`. Sila wzgledna t3+ gra/XML [D]: P 100/106 / S 102/103 / J 98/101 / KL 103/102.
- Krolestwo **Dorne** (rodow 38, szablonow 4): ludzi w partiach lordow 6776, w zalogach 4152 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 21%/39%/13%/26%, swieza partia 30%/45%/8%/18% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 15.4% (doby 1-30: 16.3, 91-120: 18.7), sredni tier 2.66 [P]. Zold na czlowieka: swieza partia 6.51, rozwinieta 8.74 zl/dobe [K+D]. Bitwy lord z lordem [P]: 24, wygrane 8 (33%). Stracone w polu grupy wiesniakow / karawany: 6 / 12; wygrane napady na wiesniakow / karawany wroga: 6 / 13 [P]. Szablony (ludzie): `aserai` 4312, `clan_martell` 1305, `clan_dayne` 747, `clan_yronwood` 412.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Dornish Recruit | 1 | P | wies | 1H 35, Atl 20 | 73 | miecz, buzdygan | - | 2 | 89 / 100 |
| Dornish Levy | 2 | P | wies | 1H 70, Atl 70 | 172 | miecz, tarcza | - | 3 | 109 / 106 |
| Dornish Bowman | 2 | S | wies | Luk 40, 1H 40, Atl 70 | 131 | miecz, luk 90 | - | 3 | 100 / 107 |
| Dornish Mounted Bowman | 3 | KL | wies | Jaz 70, Luk 70 | 284 | miecz, luk 90 | Dornish Rounsey 54/14 | 5/8 | 111 / 111 |
| Dornish Footman | 3 | P | wies | 1H 70, Atl 105, Rzut 105 | 285 | miecz, oszczepy, tarcza | - | 5 | 84 / 103 |
| Dornish Spearman | 3 | P | wies | Dr 105, Atl 105, Rzut 105 | 285 | miecz, wlocznia, oszczepy, tarcza | - | 5 | 95 / 105 |
| Dornish Archer | 3 | S | wies | Luk 80, 1H 60, Atl 105 | 284 | miecz, luk 90 | - | 5 | 115 / 116 |
| Dornish Horseman | 4 | J | wies | Jaz 110, Dr 110, Rzut 105 | 311 | wlocznia, miecz, oszczepy, tarcza | Dornish Rounsey 54/14 | 8/12 | 92 / 101 |
| Dornish Horse Archer | 4 | KL | wies | Jaz 100, Luk 105 | 311 | miecz, luk 135, tarcza | Dornish Rounsey 54/14 | 8/12 | 98 / 96 |
| Dornish Soldier | 4 | P | wies | Dr 120, Atl 140, Rzut 120 | 344 | miecz, drzewc 2r, oszczepy, tarcza | - | 8 | 92 / 104 |
| Dornish Elite Archer | 4 | S | wies | Luk 105, 1H 90, Atl 105 | 285 | miecz, luk 135 | - | 8 | 104 / 103 |
| Dornish Cobra Cavalry | 5 | J | wies | Jaz 140, Dr 140, Rzut 120 | 344 | miecz, drzewc 2r, oszczepy, tarcza | Sand Steed 60/18 | 12/18 | 92 / 95 |
| Dornish Sidewinder | 5 | KL | wies | Jaz 140, Luk 140 | 314 | miecz, luk 135, tarcza | Sand Steed 60/18 | 12/18 | 100 / 100 |
| Dornish Prince's Guard | 5 | P | wies | Dr 140, Atl 175, Rzut 140 | 403 | miecz, drzewc 2r, oszczepy, tarcza | - | 12 | 95 / 98 |
| Dornish Master Archer | 5 | S | wies | Luk 140, 1H 120, Atl 120 | 311 | miecz, luk 135 | - | 12 | 96 / 100 |
| Dornish Youth | 2 | P | szlachta | 1H 70, Atl 50 | 97 | miecz, tarcza | - | 3 | 87 / 88 |
| Dornish Glaiveman | 3 | P | szlachta | Dr 80, Atl 175* | 327 | miecz, drzewc 2r, tarcza | - | 5 | 104 / 114 |
| Dornish Elite Spearman | 4 | P | szlachta | Dr 140, Atl 175*, Rzut 120 | 442 | drzewc 2r, miecz, oszczepy, tarcza | - | 8 | 112 / 118 |
| Dornish Spearmaster | 5 | P | szlachta | Dr 150, Atl 175, Rzut 150 | 442 | drzewc 2r, miecz, oszczepy, tarcza | - | 12 | 101 / 105 |
| Dornish Viper | 6 | P | szlachta | Dr 240, Atl 230, Rzut 230 | 450 | drzewc 2r, miecz, oszczepy, tarcza | - | 17 | 97 / 98 |
| Dayne Recruit | 1 | P | rod Dayne | 1H 20, Atl 20 | 84 | buzdygan, wlocznia | - | 2 | 76 / 107 |
| Dayne Levy | 2 | P | rod Dayne | 1H 70, Atl 105* | 258 | miecz, tarcza | - | 3 | 138 / 141 |
| Dayne Footman | 3 | P | rod Dayne | Dr 105, Atl 105 | 271 | miecz, wlocznia, tarcza | - | 5 | 93 / 105 |
| Dayne Horseman | 4 | J | rod Dayne | Jaz 100, Dr 105 | 352 | wlocznia, miecz, tarcza | Dornish Rounsey 54/14 | 8/12 | 92 / 100 |
| Dayne Man at Arms | 4 | P | rod Dayne | 1H 110, Atl 140 | 374 | buzdygan, wlocznia, tarcza | - | 8 | 92 / 103 |
| Dayne Archer | 4 | S | rod Dayne | Luk 100, 1H 90, Atl 105 | 271 | miecz, luk 100 | - | 8 | 97 / 98 |
| Dayne Knight | 5 | J | rod Dayne | Jaz 140, Dr 140 | 459 | wlocznia, miecz, tarcza | Sand Steed 60/18 | 12/18 | 101 / 102 |
| Dayne Pikeman | 5 | P | rod Dayne | Dr 175, Atl 175 | 486 | drzewc 2r, buzdygan, tarcza | - | 12 | 111 / 105 |
| Dayne Veteran Archer | 5 | S | rod Dayne | Luk 130, 1H 120, Atl 120 | 320 | miecz, luk 125 | - | 12 | 93 / 96 |
| Knights of Starfall | 6 | J | rod Dayne | Jaz 230, Dr 240, Rzut 230 | 581 | wlocznia, miecz, oszczepy, tarcza | Sand Steed 60/18 | 17/26 | 104 / 104 |
| Martell Recruit | 1 | P | rod Martell | 1H 35, Atl 20 | 76 | miecz, buzdygan | - | 2 | 90 / 102 |
| Martell Levy | 2 | P | rod Martell | 1H 70, Atl 50 | 95 | miecz, tarcza | - | 3 | 86 / 94 |
| Martell Footman | 3 | P | rod Martell | 1H 80, Atl 105, Rzut 105 | 272 | miecz, oszczepy, tarcza | - | 5 | 85 / 105 |
| Martell Bowman | 3 | S | rod Martell | Luk 70, 1H 70, Atl 105 | 284 | miecz, luk 90 | - | 5 | 111 / 112 |
| Martell Rider | 4 | J | rod Martell | Jaz 110, 1H 140 | 346 | miecz, wlocznia, tarcza | Dornish Rounsey 54/14 | 8/12 | 102 / 102 |
| Martell Spearman | 4 | P | rod Martell | 1H 140, Atl 175*, Rzut 110 | 435 | wlocznia, miecz, oszczepy, tarcza | - | 8 | 111 / 111 |
| Martell Archer | 4 | S | rod Martell | Luk 100, 1H 90, Atl 105 | 311 | miecz, luk 100 | - | 8 | 102 / 103 |
| Martell Horseman | 5 | J | rod Martell | Jaz 140, 1H 140, Rzut 120 | 435 | miecz, wlocznia, oszczepy, tarcza | Thoroughbred Sand  68/20 | 12/18 | 100 / 104 |
| Martell House Guard | 5 | P | rod Martell | 1H 140, Atl 175, Rzut 130 | 458 | miecz, wlocznia, oszczepy, tarcza | - | 12 | 100 / 102 |
| Martell Veteran Archer | 5 | S | rod Martell | Luk 130, 1H 140, Atl 175* | 346 | miecz, luk 125 | - | 12 | 97 / 98 |
| Water Gardens Sentinel | 6 | J | rod Martell | Jaz 240, Dr 240, Rzut 200 | 458 | miecz, wlocznia, oszczepy, tarcza | Thoroughbred Sand  68/20 | 17/26 | 100 / 100 |
| Yronwood Recruit | 1 | P | rod Yronwood | 1H 20, Atl 20 | 73 | buzdygan | - | 2 | 71 / 100 |
| Yronwood Levy | 2 | P | rod Yronwood | 1H 70, Atl 140* | 260 | miecz, tarcza | - | 3 | 149 / 141 |
| Yronwood Man at Arms | 3 | P | rod Yronwood | Dr 105, Atl 175*, Rzut 105 | 351 | miecz, wlocznia, oszczepy, tarcza | - | 5 | 115 / 118 |
| Yronwood Horseman | 4 | J | rod Yronwood | Jaz 100, Dr 105 | 436 | wlocznia, miecz, tarcza | Dornish Rounsey 54/14 | 8/12 | 100 / 109 |
| Yronwood Pikeman | 4 | P | rod Yronwood | 1H 110, Atl 175* | 436 | drzewc 2r, miecz, tarcza | - | 8 | 103 / 111 |
| Yronwood Archer | 4 | S | rod Yronwood | Luk 100, 1H 90, Atl 175* | 330 | miecz, luk 100 | - | 8 | 104 / 106 |
| Yronwood Knight | 5 | J | rod Yronwood | Jaz 140, Dr 140 | 436 | wlocznia, miecz, tarcza | Sand Steed 60/18 | 12/18 | 99 / 101 |
| Yronwood Veteran Pikeman | 5 | P | rod Yronwood | Dr 175, Atl 175 | 436 | drzewc 2r, miecz, tarcza | - | 12 | 106 / 100 |
| Yronwood Veteran Archer | 5 | S | rod Yronwood | Luk 130, 1H 120, Atl 175* | 351 | miecz, luk 125 | - | 12 | 96 / 100 |
| Boneway Guardian | 6 | J | rod Yronwood | Jaz 230, Dr 240, Rzut 230 | 436 | wlocznia, miecz, oszczepy, tarcza | Sand Steed 60/18 | 17/26 | 96 / 96 |
| Dornish Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 128 | topor, luk 60 | - | 3 | 133 / 143 |
| Dornish Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 137 | wlocznia, topor, tarcza | - | 3 | 141 / 131 |
| Dornish Militia Veteran Archer | 4 | S | milicja | Luk 140, 1H 110, Atl 130 | 178 | miecz, luk 130 | - | 8 | 107 / 109 |
| Dornish Militia Veteran Spearman | 4 | P | milicja | Dr 140, Atl 175* | 393 | wlocznia, miecz, tarcza | - | 8 | 107 / 114 |
| Bright Banners Horseman | 4 | J | najemnicy Bright Banners | Jaz 110, Dr 175* | 258 | miecz, wlocznia, tarcza | Charger 54/26 | 8/12 | 104 / 97 |

#### 1.11 Nocna Straz (`nightswatch`)

- Jednostek z droga do wojska: **18** (P 7, S 10, J 1, KL 0). Rekrut wsi `nightswatch_recruit`, szlachecki `nightswatch_ranger_recruit`. Sila wzgledna t3+ gra/XML [D]: P 75/85 / S 93/93 / J 82/85 / KL -/-.
- Krolestwo **Nocna Straz** (rodow 6, szablonow 1): ludzi w partiach lordow 2100, w zalogach 1435 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 23%/60%/17%/0%, swieza partia 57%/33%/9%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 3.6% (doby 1-30: 3.7, 91-120: 3.7), sredni tier 2.51 [P]. Zold na czlowieka: swieza partia 6.13, rozwinieta 9.17 zl/dobe [K+D]. Bitwy lord z lordem [P]: 100, wygrane 52 (52%). Stracone w polu grupy wiesniakow / karawany: 0 / 0; wygrane napady na wiesniakow / karawany wroga: 20 / 44 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Night's Watch Recruit | 1 | P | wies | 1H 35, Atl 20 | 73 | topor | - | 2 | 89 / 100 |
| Night's Watch Soldier | 2 | P | wies | 1H 70, Atl 70 | 135 | miecz | - | 3 | 90 / 81 |
| Night's Watch Shieldbrother | 3 | P | wies | 1H 80, Atl 105 | 191 | miecz, wlocznia, tarcza | - | 5 | 75 / 89 |
| Night's Watch Crossbowman | 3 | S | wies | Kusz 175*A, 1H 70, Atl 70 | 153 | miecz, kusza 120 | - | 5 | 91 / 86 |
| Night's Watch Defender | 4 | P | wies | 1H 105, Atl 105 | 212 | miecz, wlocznia, tarcza | - | 8 | 69 / 80 |
| Night's Watch Elite Crossbowman | 4 | S | wies | Kusz 175*A, 1H 80, Atl 80 | 180 | miecz, kusza 200 | - | 8 | 88 / 86 |
| Night's Watch Horseman | 5 | J | wies | Jaz 130, 1H 130 | 299 | miecz, wlocznia, tarcza | Battanian Pony 44/10 | 12/18 | 82 / 85 |
| Night's Watch Stalwart | 5 | P | wies | 1H 140, Atl 140 | 299 | miecz, wlocznia, tarcza | - | 12 | 81 / 86 |
| Night's Watch Master Crossbowman | 5 | S | wies | Kusz 175, 1H 110, Atl 110 | 191 | miecz, kusza 200 | - | 12 | 84 / 82 |
| Night's Watch Ranger Recruit | 2 | S | szlachta | Luk 50, 1H 70, Atl 30 | 91 | miecz, luk 60 | - | 3 | 101 / 105 |
| Night's Watch Ranger | 3 | S | szlachta | Luk 80, 1H 70, Atl 140* | 247 | miecz, luk 100 | - | 5 | 112 / 113 |
| Night's Watch Elite Ranger | 4 | S | szlachta | Luk 110, 1H 105, Atl 140* | 247 | miecz, luk 100 | - | 8 | 100 / 99 |
| Night's Watch Master Ranger | 5 | S | szlachta | Luk 140, 1H 120, Atl 140 | 321 | miecz, luk 100 | - | 12 | 94 / 98 |
| Night's Watch Protector of the Realm | 6 | S | szlachta | Luk 230, 1H 210, Atl 220 | 326 | miecz, luk 100 | - | 17 | 84 / 84 |
| Night's Watch Militia Archer | 2 | S | milicja | Luk 80, 1H 70, Atl 70 | 135 | miecz, buzdygan, luk 90 | - | 3 | 146 / 151 |
| Night's Watch Militia Spearman | 2 | P | milicja | 1H 80, Atl 105 | 164 | wlocznia, topor, tarcza | - | 3 | 123 / 139 |
| Night's Watch Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 153 | topor, miecz, luk 90 | - | 5 | 114 / 115 |
| Night's Watch Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 105 | 164 | wlocznia, miecz, tarcza | - | 5 | 78 / 95 |

#### 1.12 Wolni Ludzie (`freefolk`)

- Jednostek z droga do wojska: **26** (P 17, S 7, J 2, KL 0). Rekrut wsi `freefolk_recruit`, szlachecki `freefolk_thenn_son`. Sila wzgledna t3+ gra/XML [D]: P 95/95 / S 108/104 / J 98/96 / KL -/-.
- Krolestwo **Wolni Ludzie** (rodow 11, szablonow 2): ludzi w partiach lordow 2063, w zalogach 1406 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 51%/29%/20%/0%, swieza partia 66%/26%/8%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 4.8% (doby 1-30: 4.4, 91-120: 5.0), sredni tier 2.44 [P]. Zold na czlowieka: swieza partia 5.19, rozwinieta 9.3 zl/dobe [K+D]. Bitwy lord z lordem [P]: 100, wygrane 48 (48%). Stracone w polu grupy wiesniakow / karawany: 20 / 44; wygrane napady na wiesniakow / karawany wroga: 0 / 0 [P]. Szablony (ludzie): `freefolk` 1575, `clan_rayder` 488.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Free Folk Clansman | 1 | P | wies, rod Giantsbane/Rayder | 1H 70*, Atl 20 | 99 | topor | - | 2 | 143 / 117 |
| Free Folk Warrior | 2 | P | wies, rod Giantsbane/Rayder | 1H 70, Atl 70 | 178 | topor, tarcza | - | 3 | 110 / 118 |
| Free Folk Bowman | 2 | S | wies, rod Giantsbane/Rayder | Luk 50, 1H 70, Atl 70 | 178 | topor, luk 70 | - | 3 | 126 / 127 |
| Free Folk Axeman | 3 | P | wies, rod Giantsbane/Rayder | 1H 105, Atl 80 | 204 | topor, tarcza | - | 5 | 81 / 93 |
| Free Folk Archer | 3 | S | wies, rod Giantsbane/Rayder | Luk 80, 1H 140*, Atl 70 | 204 | miecz, luk 70 | - | 5 | 113 / 102 |
| Free Folk Berzerker | 4 | P | wies, rod Giantsbane/Rayder | 2H 110, Atl 140, Rzut 105 | 248 | topor 2r, oszczepy | - | 8 | 74 / 83 |
| Free Folk Shieldman | 4 | P | wies, rod Giantsbane/Rayder | 1H 140, Atl 140 | 248 | topor, tarcza | - | 8 | 87 / 86 |
| Free Folk Spearman | 4 | P | wies, rod Giantsbane/Rayder | Dr 175*, Atl 140, Rzut 105 | 248 | wlocznia, miecz, oszczepy, tarcza | - | 8 | 96 / 88 |
| Free Folk Sharpshooter | 4 | S | wies, rod Giantsbane/Rayder | Luk 110, 1H 140*, Atl 140* | 239 | miecz, luk 120 | - | 8 | 105 / 100 |
| Free Folk Horseman | 5 | J | wies, rod Giantsbane/Rayder | Jaz 130, Dr 175, Rzut 120 | 272 | wlocznia, miecz, oszczepy, tarcza | Garron 51/18 | 12/18 | 90 / 84 |
| Free Folk Frosthedge | 5 | P | wies, rod Giantsbane/Rayder | 1H 140, Atl 140 | 296 | topor, tarcza | - | 12 | 81 / 87 |
| Free Folk Wildling Berzerker | 5 | P | wies, rod Giantsbane/Rayder | 2H 140, Atl 140, Rzut 130 | 296 | topor 2r, oszczepy | - | 12 | 76 / 82 |
| Free Folk Hawkeye | 5 | S | wies, rod Giantsbane/Rayder | Luk 140, 1H 140, Atl 140 | 248 | miecz, luk 140 | - | 12 | 92 / 92 |
| Freefolk Son of Thenn | 2 | P | szlachta | 2H 140*, Atl 70 | 132 | topor 2r | - | 3 | 128 / 94 |
| Freefolk Thenn | 3 | P | szlachta | 2H 140*, Atl 105 | 250 | topor 2r, miecz, tarcza | - | 5 | 102 / 103 |
| Freefolk Thenn Warrior | 4 | P | szlachta | 2H 140, Atl 140 | 334 | topor 2r, miecz, tarcza | - | 8 | 96 / 98 |
| Freefolk Thenn Cannibal | 5 | P | szlachta | 2H 150, Atl 175, Rzut 130 | 437 | topor 2r, miecz, toporki, tarcza | - | 12 | 101 / 103 |
| Freefolk Thenn Impaler | 6 | P | szlachta | 2H 220, Atl 220, Rzut 210 | 517 | topor 2r, miecz, toporki, tarcza | - | 17 | 98 / 98 |
| Giant | 5 | P | rod Giantsbane/Rayder | 2H 260, Atl 200* | 550 | topor 2r | - | 12 | 135 / 122 |
| Mammoth Riding Giant | 6 | J | rod Giantsbane/Rayder | Jaz 250, 2H 260 | 550 | topor 2r, luk 500 | Mammoth 20/400 (egz.) | 17/26 | 107 / 107 |
| Elder Giant | 6 | P | rod Giantsbane/Rayder | 2H 300, Atl 200* | 550 | topor 2r, luk 500 | - | 17 | 109 / 95 |
| Giant Archer | 6 | S | rod Giantsbane/Rayder | Luk 250, 2H 260, Atl 200* | 550 | topor 2r, luk 500 | - | 17 | 121 / 121 |
| Freefolk Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 55 | buzdygan, topor, luk 90 | - | 3 | 118 / 127 |
| Freefolk Militia Spearman | 2 | P | milicja | 1H 80, Atl 60 | 99 | wlocznia, topor, tarcza | - | 3 | 95 / 120 |
| Freefolk Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 149 | topor, miecz, luk 90 | - | 5 | 113 / 114 |
| Freefolk Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 80 | 169 | wlocznia, miecz, tarcza | - | 5 | 74 / 96 |

#### 1.13 Skagos (`skagosi`)

- Jednostek z droga do wojska: **13** (P 7, S 3, J 3, KL 0). Rekrut wsi `skag_recruit`, szlachecki `skag_recruit`. Sila wzgledna t3+ gra/XML [D]: P 107/101 / S 93/93 / J 109/103 / KL -/-.
- Uwaga: krolestwo Skagos ma kulture `freefolk`, ale jego jedyny rod uzywa szablonu `skagosi`.
- Krolestwo **Skagos** (rodow 1, szablonow 1): ludzi w partiach lordow 76, w zalogach 236 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 34%/48%/17%/0%, swieza partia 64%/26%/9%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 17.6% (doby 1-30: 17.6, 91-120: -), sredni tier 2.6 [P]. Zold na czlowieka: swieza partia 5.62, rozwinieta 8.34 zl/dobe [K+D]. Bitew lord z lordem [P]: brak. Stracone w polu grupy wiesniakow / karawany: 0 / 0; wygrane napady na wiesniakow / karawany wroga: 0 / 0 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Skagosi Lowborn | 1 | P | wies, szlachta | 1H 35, Atl 20 | 73 | topor | - | 2 | 89 / 100 |
| Skagosi Footman | 2 | P | wies, szlachta | 1H 175*T, Atl 54 | 79 | topor, tarcza | - | 3 | 143 / 90 |
| Skagosi Soldier | 3 | P | wies, szlachta | 1H 175*T, Atl 140* | 290 | topor, wlocznia, tarcza | - | 5 | 125 / 108 |
| Skagosi Bowman | 3 | S | wies, szlachta | Luk 70, 1H 70, Atl 70 | 79 | topor, luk 70 | - | 5 | 74 / 74 |
| Skagosi Rider | 4 | J | wies, szlachta | Jaz 140, 1H 175*, Rzut 140* | 343 | miecz, wlocznia, oszczepy, tarcza | Unicorn 55/80 (egz.) | 8/12 | 121 / 105 |
| Skagosi Savage | 4 | P | wies, szlachta | 1H 175*, Atl 140 | 332 | miecz, topor 2r, tarcza | - | 8 | 105 / 100 |
| Skagosi Spearman | 4 | P | wies, szlachta | 1H 175*, Atl 140 | 332 | miecz, wlocznia, tarcza | - | 8 | 105 / 100 |
| Skagosi Archer | 4 | S | wies, szlachta | Luk 110, 1H 100, Atl 140* | 290 | topor, luk 70 | - | 8 | 102 / 104 |
| Skagosi Stoneborn | 5 | J | wies, szlachta | Jaz 140, 1H 175, Rzut 140 | 477 | miecz, wlocznia, oszczepy, tarcza | Unicorn 55/80 (egz.) | 12/18 | 112 / 108 |
| Skagosi Barbarian | 5 | P | wies, szlachta | 2H 140, Atl 175*, Rzut 140* | 437 | topor 2r, oszczepy | - | 12 | 94 / 94 |
| Skagosi Master Spearman | 5 | P | wies, szlachta | 1H 175, Atl 175 | 437 | miecz, wlocznia, tarcza | - | 12 | 106 / 101 |
| Skagosi Huntsman | 5 | S | wies, szlachta | Luk 150, 1H 175*, Atl 140 | 303 | miecz, luk 125 | - | 12 | 103 / 101 |
| Skagosi Stoneborn Champion | 6 | J | wies, szlachta | Jaz 220, 1H 210, Rzut 210 | 479 | miecz, wlocznia, oszczepy, tarcza | Ebony Unicorn 60/90 (egz.) | 17/26 | 95 / 95 |

#### 1.14 Inni (umarli) (`whitewalker`)

- Jednostek z droga do wojska: **12** (P 8, S 3, J 1, KL 0). Rekrut wsi `wight_recruit`, szlachecki `wight_recruit`. Sila wzgledna t3+ gra/XML [D]: P 69/77 / S 75/78 / J 87/80 / KL -/-.
- Uwaga: Inni nie sa krolestwem (klan Others); ich sily opisuje audyt 06-nieumarli.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Wight | 1 | P | wies, szlachta, najemnicy Others | 1H 35, Atl 20 | 0 | topor | - | 2 | 54 / 51 |
| Angry Wight | 2 | P | wies, szlachta, najemnicy Others | 1H 50, Atl 50 | 58 | topor | - | 3 | 55 / 70 |
| Scorned Wight | 3 | P | wies, szlachta, najemnicy Others | 1H 80, Atl 80 | 102 | topor, buzdygan | - | 5 | 53 / 68 |
| Cursed Wight | 4 | P | wies, szlachta, najemnicy Others | 1H 100, Atl 110 | 188 | topor, wildling_shield | - | 8 | 61 / 72 |
| Beastbound Wight | 5 | J | wies, szlachta, najemnicy Others | Jaz 130, Dr 175, Rzut 120 | 216 | wlocznia, wildling_shield, oszczepy | Wight Horse 50/30 | 12/18 | 87 / 80 |
| Vicious Wight | 5 | P | wies, szlachta, najemnicy Others | 1H 140, Atl 140 | 285 | topor, wlocznia, toporki, tarcza | - | 12 | 80 / 86 |
| Dexterous Wight | 5 | S | wies, szlachta, najemnicy Others | Luk 140, 1H 110, Atl 110 | 114 | topor, luk 120 | - | 12 | 75 / 78 |
| Sadistic Wight | 6 | P | wies, szlachta, najemnicy Others | 2H 220, Atl 220, Rzut 220 | 346 | topor 2r, toporki | - | 17 | 82 / 83 |
| Wight Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 62 | buzdygan, topor, luk 90 | - | 3 | 120 / 129 |
| Wight Militia Spearman | 2 | P | milicja | 1H 80, Atl 60 | 90 | wlocznia, topor, tarcza | - | 3 | 93 / 118 |
| Wight Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 95 | topor, miecz, luk 90 | - | 5 | 104 / 104 |
| Wight Militia Veteran Spearman | 3 | P | milicja | 1H 100, Atl 80 | 150 | wlocznia, miecz, tarcza | - | 5 | 72 / 92 |

### Essos


#### 1.15 Braavos (`empire`)

- Jednostek z droga do wojska: **40** (P 20, S 11, J 8, KL 1). Rekrut wsi `imperial_recruit`, szlachecki `imperial_vigla_recruit`. Sila wzgledna t3+ gra/XML [D]: P 110/101 / S 95/99 / J 102/94 / KL 96/96.
- Krolestwo **Braavos** (rodow 6, szablonow 1): ludzi w partiach lordow 2185, w zalogach 1407 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 52%/36%/6%/6%, swieza partia 64%/30%/3%/3% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 5.0% (doby 1-30: 6.4, 91-120: 3.7), sredni tier 2.55 [P]. Zold na czlowieka: swieza partia 5.48, rozwinieta 8.55 zl/dobe [K+D]. Bitwy lord z lordem [P]: 33, wygrane 7 (21%). Stracone w polu grupy wiesniakow / karawany: 0 / 2; wygrane napady na wiesniakow / karawany wroga: 6 / 2 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Braavosi Recruit | 1 | P | wies | 1H 35, Atl 20 | 76 | wlocznia, drzewc 2r | - | 2 | 90 / 102 |
| Braavosi Footman | 2 | P | wies | 1H 105*T, Atl 70 | 122 | miecz, tarcza | - | 3 | 118 / 91 |
| Braavosi Archer | 2 | S | wies | Luk 40, 1H 40, Atl 70 | 136 | buzdygan, luk 60 | - | 3 | 96 / 103 |
| Braavosi Soldier | 3 | P | wies | 1H 175*, Atl 140*, Rzut 105 | 291 | miecz, oszczepy, tarcza | - | 5 | 125 / 102 |
| Braavosi Trained Archer | 3 | S | wies | Luk 70, 1H 70, Atl 70 | 145 | miecz, luk 70 | - | 5 | 85 / 86 |
| Braavosi Rider | 4 | J | wies | Jaz 100, 1H 175* | 291 | drzewc 2r, miecz | Andalos Palfrey 53/20 | 8/12 | 104 / 94 |
| Braavosi Man at Arms | 4 | P | wies | 1H 175*, Atl 175*, Rzut 140* | 388 | miecz, oszczepy, tarcza | - | 8 | 115 / 102 |
| Braavosi Crossbowman | 4 | S | wies | Kusz 105, 1H 175*, Atl 105 | 269 | miecz, kusza 270 | - | 8 | 87 / 100 |
| Braavosi Veteran Archer | 4 | S | wies | Luk 100, 1H 100, Atl 105 | 257 | miecz, luk 100 | - | 8 | 96 / 98 |
| Braavosi Horseman | 5 | J | wies | Jaz 130, 1H 175 | 388 | drzewc 2r, miecz | Andalos Palfrey 53/20 | 12/18 | 100 / 94 |
| Braavosi Scout | 5 | KL | wies | Jaz 100, Kusz 140 | 408 | miecz, kusza 300 | Andalos Palfrey 53/20 | 12/18 | 96 / 96 |
| Braavosi Captain | 5 | P | wies | 1H 175, Atl 175, Rzut 140* | 466 | miecz, oszczepy, tarcza | - | 12 | 109 / 104 |
| Sealord's Guard | 5 | S | wies | Luk 160, 1H 175, Atl 175 | 408 | miecz, luk 135, tarcza | - | 12 | 117 / 117 |
| Braavosi Chief Crossbowman | 5 | S | wies | Kusz 130, 1H 175, Atl 140 | 303 | miecz, kusza 270 | - | 12 | 90 / 95 |
| Braavosi Noble Recruit | 2 | P | szlachta | 1H 105, Atl 20 | 79 | wlocznia, miecz, tarcza | - | 3 | 94 / 86 |
| Braavosi Swordsman Apprentice | 3 | P | szlachta | 1H 175*, Atl 105 | 257 | wlocznia, miecz, tarcza | - | 5 | 115 / 100 |
| Braavosi Swordsman | 4 | P | szlachta | 1H 175*, Atl 140 | 303 | wlocznia, miecz, tarcza | - | 8 | 102 / 95 |
| Braavosi Swordmaster | 5 | P | szlachta | 1H 175, Atl 175 | 408 | wlocznia, miecz, tarcza | - | 12 | 104 / 101 |
| Water Dancer | 6 | P | szlachta | 1H 250, Atl 230 | 466 | wlocznia, miecz, tarcza | - | 17 | 100 / 101 |
| Braavosi Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 125 | topor, miecz, luk 60 | - | 3 | 132 / 142 |
| Braavosi Militia Spearman | 2 | P | milicja | 1H 80, Atl 70 | 131 | wlocznia, topor, tarcza | - | 3 | 106 / 130 |
| Braavosi Militia Veteran Archer | 4 | S | milicja | Luk 140, 1H 110, Atl 130 | 158 | miecz, topor, luk 100 | - | 8 | 102 / 103 |
| Braavosi Militia Veteran Spearman | 4 | P | milicja | 1H 140, Atl 175* | 377 | wlocznia, topor, tarcza | - | 8 | 105 / 112 |
| Bright Banners Soldier | 2 | P | najemnicy Bright Banners | 1H 50, Atl 70, Rzut 105 | 198 | buzdygan, oszczepy, tarcza | - | 3 | 104 / 124 |
| Bright Banners Archer | 3 | S | najemnicy Bright Banners | Luk 80, 1H 70, Atl 70 | 224 | buzdygan, luk 90 | - | 5 | 107 / 108 |
| Brave Companion Croswbowman | 1 | S | najemnicy Brave Companions | Kusz 175*A, 1H 40, Atl 105* | 151 | miecz, kusza 120 | - | 2 | - / - |
| Brave Companion Warrior | 3 | P | najemnicy Brave Companions | 1H 175*T, Atl 150*, Rzut 105 | 313 | miecz, oszczepy, tarcza | - | 5 | 129 / 112 |
| Brave Companion Mutilator | 4 | P | najemnicy Brave Companions | 1H 175*T, Atl 150, Rzut 120 | 320 | miecz, oszczepy, tarcza | - | 8 | 105 / 98 |
| Windblown Footman | 2 | P | najemnicy The Windblown | 1H 70, Atl 50 | 142 | miecz, tarcza | - | 3 | 97 / 101 |
| Windblown Scout | 3 | J | najemnicy The Windblown | Jaz 60, Dr 140*, Rzut 105 | 234 | wlocznia, miecz, oszczepy | Dornish Rounsey 54/14 | 5/8 | 96 / 93 |
| Windblown Cavalry | 4 | J | najemnicy The Windblown | Jaz 110, Dr 140, Rzut 105 | 335 | wlocznia, miecz, oszczepy, tarcza | Sand Steed 60/18 | 8/12 | 102 / 105 |
| Faith Militant Initiate | 2 | P | najemnicy Faith Militant | 1H 40, Atl 40 | 43 | buzdygan | - | 3 | 43 / 55 |
| Faith Militant Acolyte | 3 | P | najemnicy Faith Militant | 1H 70, Atl 70 | 43 | buzdygan | - | 5 | 40 / 51 |
| Faith Militant Devout | 4 | P | najemnicy Faith Militant | 1H 100, Atl 100 | 43 | buzdygan | - | 8 | 44 / 52 |
| Long Lance Freerider | 2 | J | najemnicy Long Lances | Jaz 50, Dr 140* | 148 | wlocznia, miecz | Andalos Palfrey 53/20 | 3/4 | - / - |
| Long Lance Lancer | 3 | J | najemnicy Long Lances | Jaz 80, Dr 140* | 238 | wlocznia, miecz, tarcza | Andalos Palfrey 53/20 | 5/8 | 103 / 98 |
| Long Lance Elite Lancer | 4 | J | najemnicy Long Lances | Jaz 120, Dr 140 | 352 | wlocznia, miecz, tarcza | Charger 54/26 | 8/12 | 107 / 109 |
| Company of the Cat Bowman | 2 | S | najemnicy Company of the Cat | Luk 60, 1H 40, Atl 70 | 161 | miecz, luk 100 | - | 3 | 128 / 138 |
| Company of the Cat Footman | 3 | P | najemnicy Company of the Cat | 1H 105, Atl 140* | 254 | miecz, wlocznia, tarcza | - | 5 | 97 / 97 |
| Company of the Cat Horseman | 4 | J | najemnicy Company of the Cat | Jaz 110, 1H 175*T | 323 | miecz, wlocznia, tarcza | Charger 54/26 | 8/12 | 110 / 104 |

#### 1.16 Pentos (`pentoshi`)

- Jednostek z droga do wojska: **19** (P 10, S 4, J 4, KL 1). Rekrut wsi `pentoshi_recruit`, szlachecki `pentoshi_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 122/110 / S 102/104 / J 110/106 / KL 102/102.
- Krolestwo **Pentos** (rodow 13, szablonow 1): ludzi w partiach lordow 2718, w zalogach 1826 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 30%/40%/27%/3%, swieza partia 61%/22%/15%/2% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 7.8% (doby 1-30: 15.9, 91-120: 7.6), sredni tier 2.58 [P]. Zold na czlowieka: swieza partia 7.0, rozwinieta 10.73 zl/dobe [K+D]. Bitwy lord z lordem [P]: 32, wygrane 9 (28%). Stracone w polu grupy wiesniakow / karawany: 1 / 8; wygrane napady na wiesniakow / karawany wroga: 1 / 1 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Pentoshi Recruit | 1 | P | wies | 1H 35, Atl 28 | 73 | miecz | - | 2 | 93 / 100 |
| Pentoshi Footman | 2 | P | wies | 1H 175*T, Atl 140* | 218 | miecz, tarcza | - | 3 | 198 / 119 |
| Pentoshi Soldier | 3 | P | wies | 1H 175*T, Atl 175* | 328 | miecz, triangular_spear_t3, tarcza | - | 5 | 135 / 108 |
| Pentoshi Horseman | 4 | J | wies | Jaz 110, 1H 175*T | 354 | miecz, triangular_spear_t3, tarcza | Palfrey 54/16 | 8/12 | 112 / 103 |
| Pentoshi Man at Arms | 4 | P | wies | 1H 175*T, Atl 175* | 354 | miecz, wlocznia, tarcza | - | 8 | 112 / 98 |
| Pentoshi Archer | 4 | S | wies | Luk 110, 1H 90, Atl 175* | 328 | miecz, luk 90 | - | 8 | 107 / 109 |
| Pentoshi Cavalry | 5 | J | wies | Jaz 150, 1H 175 | 477 | miecz, wlocznia, tarcza | Courser 59/19 | 12/18 | 112 / 106 |
| Pentoshi Mounted Archer | 5 | KL | wies | Jaz 140, Luk 140 | 354 | miecz, luk 110 | Palfrey 54/16 | 12/18 | 102 / 102 |
| Pentoshi Spearman | 5 | P | wies | 1H 175, Atl 175, Rzut 140 | 451 | miecz, wlocznia, oszczepy, tarcza | - | 12 | 108 / 102 |
| Pentoshi Elite Archer | 5 | S | wies | Luk 130, 1H 140, Atl 175 | 354 | miecz, luk 110 | - | 12 | 97 / 100 |
| Pentoshi Noble Youth | 2 | P | szlachta | 1H 50, Atl 50 | 97 | buzdygan | - | 3 | 64 / 81 |
| Pentoshi Pikeman | 3 | P | szlachta | 1H 140*, Atl 175* | 410 | drzewc 2r, miecz | - | 5 | 128 / 119 |
| Pentoshi Pike Warrior | 4 | P | szlachta | 1H 175*T, Atl 175* | 507 | drzewc 2r, miecz, tarcza | - | 8 | 128 / 122 |
| Pentoshi Lancer | 5 | J | szlachta | Jaz 140, 1H 175 | 559 | miecz, wlocznia, tarcza | Courser 59/19 | 12/18 | 116 / 115 |
| Magister Guard Elite | 6 | J | szlachta | Jaz 220, Dr 220, Rzut 210 | 584 | miecz, wlocznia, oszczepy, tarcza | Courser 59/19 | 17/26 | 100 / 100 |
| Pentoshi Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 122 | miecz, luk 90 | - | 3 | 137 / 147 |
| Pentoshi Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 144 | wlocznia, topor, tarcza | - | 3 | 142 / 133 |
| Pentoshi Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 144 | miecz, luk 125 | - | 5 | 116 / 117 |
| Pentoshi Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 390 | wlocznia, miecz, tarcza | - | 5 | 143 / 133 |

#### 1.17 Lys (`lyseni`)

- Jednostek z droga do wojska: **20** (P 13, S 5, J 2, KL 0). Rekrut wsi `lyseni_recruit`, szlachecki `lyseni_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 103/100 / S 98/99 / J 106/98 / KL -/-.
- Krolestwo **Lys** (rodow 7, szablonow 2): ludzi w partiach lordow 1083, w zalogach 1269 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 30%/47%/23%/0%, swieza partia 61%/26%/13%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 8.3% (doby 1-30: 7.8, 91-120: 7.0), sredni tier 2.6 [P]. Zold na czlowieka: swieza partia 6.11, rozwinieta 9.14 zl/dobe [K+D]. Bitwy lord z lordem [P]: 70, wygrane 18 (26%). Stracone w polu grupy wiesniakow / karawany: 9 / 0; wygrane napady na wiesniakow / karawany wroga: 10 / 0 [P]. W liczbach sa rody najemne: Stone Crows 24 ludzi (2%). Szablony (ludzie): `lyseni` 1059, `mercenary_sturgia` 24.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Lyseni Recruit | 1 | P | wies | 1H 35, Atl 20 | 66 | miecz | - | 2 | 85 / 95 |
| Lyseni Footman | 2 | P | wies | 1H 40, Atl 70 | 131 | topor, tarcza | - | 3 | 83 / 94 |
| Lyseni Soldier | 3 | P | wies | Dr 140*, Atl 105 | 228 | miecz, wlocznia, tarcza | - | 5 | 99 / 91 |
| Lyseni Bowman | 3 | S | wies | Luk 70, 1H 50, Atl 105* | 228 | topor, luk 100 | - | 5 | 100 / 100 |
| Lyseni Horseman | 4 | J | wies | Jaz 100, 1H 175* | 339 | miecz, wlocznia, tarcza | Dornish Rounsey 54/14 | 8/12 | 108 / 98 |
| Lyseni Warrior | 4 | P | wies | 1H 175*, Atl 175* | 379 | miecz, wlocznia, tarcza | - | 8 | 114 / 101 |
| Lyseni Archer | 4 | S | wies | Luk 100, 1H 80, Atl 105 | 278 | topor, luk 100 | - | 8 | 96 / 98 |
| Lyseni Cavalry | 5 | J | wies | Jaz 130, 1H 175* | 435 | miecz, wlocznia, tarcza | Sand Steed 60/18 | 12/18 | 104 / 98 |
| Lyseni Glaiveman | 5 | P | wies | 1H 175, Atl 175 | 435 | drzewc 2r, miecz, tarcza | - | 12 | 106 / 101 |
| Lyseni Spearman | 5 | P | wies | 1H 175, Atl 175 | 435 | miecz, wlocznia, tarcza | - | 12 | 106 / 101 |
| Lyseni Elite Archer | 5 | S | wies | Luk 140, 1H 120, Atl 120 | 327 | topor, luk 125 | - | 12 | 97 / 100 |
| Lyseni Noble Youth | 2 | P | szlachta | 1H 50, Atl 50 | 90 | buzdygan | - | 3 | 62 / 79 |
| Lyseni Axe Apprentice | 3 | P | szlachta | 1H 70, Atl 140*, Rzut 80 | 263 | topor, toporki, tarcza | - | 5 | 87 / 99 |
| Lyseni Axeman | 4 | P | szlachta | 1H 175*T, Atl 175*, Rzut 110 | 392 | topor, toporki, tarcza | - | 8 | 116 / 104 |
| Lyseni Executioner | 5 | P | szlachta | 2H 140, Atl 175, Rzut 140 | 494 | topor 2r, toporki | - | 12 | 99 / 102 |
| Lyseni Enforcer | 6 | P | szlachta | 2H 230, Atl 230, Rzut 230 | 544 | topor 2r, toporki | - | 17 | 99 / 99 |
| Lyseni Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 105 | miecz, luk 90 | - | 3 | 132 / 142 |
| Lyseni Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 127 | wlocznia, topor, tarcza | - | 3 | 138 / 128 |
| Lyseni Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 127 | miecz, luk 125 | - | 5 | 113 / 114 |
| Lyseni Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 386 | wlocznia, miecz, tarcza | - | 5 | 143 / 132 |

#### 1.18 Myr (`myrish`)

- Jednostek z droga do wojska: **19** (P 8, S 9, J 2, KL 0). Rekrut wsi `myrish_recruit`, szlachecki `myrish_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 88/94 / S 101/105 / J 96/102 / KL -/-.
- Krolestwo **Myr** (rodow 7, szablonow 2): ludzi w partiach lordow 3739, w zalogach 2050 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 21%/55%/24%/0%, swieza partia 57%/30%/13%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 7.5% (doby 1-30: 8.0, 91-120: 8.3), sredni tier 2.69 [P]. Zold na czlowieka: swieza partia 5.99, rozwinieta 8.96 zl/dobe [K+D]. Bitwy lord z lordem [P]: 32, wygrane 23 (72%). Stracone w polu grupy wiesniakow / karawany: 1 / 1; wygrane napady na wiesniakow / karawany wroga: 1 / 8 [P]. W liczbach sa rody najemne: Bright Banners 386 ludzi (10%). Szablony (ludzie): `myrish` 3353, `outlaw_aserai` 386.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Myrish Recruit | 1 | P | wies | 1H 35, Atl 20 | 66 | miecz | - | 2 | 85 / 95 |
| Myrish Footman | 2 | P | wies | Dr 70, Atl 70 | 144 | miecz, wlocznia | - | 3 | 92 / 84 |
| Myrish Soldier | 3 | P | wies | 1H 70, Atl 70 | 144 | miecz, wlocznia, tarcza | - | 5 | 60 / 77 |
| Myrish Bowman | 3 | S | wies | Luk 70, 1H 50, Atl 70 | 144 | miecz, luk 90 | - | 5 | 84 / 85 |
| Myrish Horseman | 4 | J | wies | Jaz 100, 1H 100 | 371 | miecz, wlocznia, tarcza | Dornish Rounsey 54/14 | 8/12 | 93 / 102 |
| Myrish Warrior | 4 | P | wies | Dr 140, Atl 175* | 371 | miecz, wlocznia, tarcza | - | 8 | 104 / 100 |
| Myrish Archer | 4 | S | wies | Luk 100, 1H 80, Atl 70 | 144 | miecz, luk 90 | - | 8 | 79 / 80 |
| Myrish Cavalry | 5 | J | wies | Jaz 130, Dr 140 | 464 | miecz, wlocznia, tarcza | Sand Steed 60/18 | 12/18 | 100 / 101 |
| Myrish Legionnaire | 5 | P | wies | 1H 140, Atl 175, Rzut 140 | 464 | miecz, wlocznia, oszczepy, tarcza | - | 12 | 101 / 104 |
| Myrish Elite Archer | 5 | S | wies | Luk 130, 1H 120, Atl 175* | 371 | miecz, luk 110 | - | 12 | 97 / 101 |
| Myrish Noble Youth | 2 | P | szlachta | 1H 50, Atl 50 | 90 | buzdygan | - | 3 | 62 / 79 |
| Myrish Crossbowman | 3 | S | szlachta | Kusz 175*A, 1H 70, Atl 175* | 344 | miecz, kusza 160 | - | 5 | 114 / 129 |
| Myrish Elite Crossbowman | 4 | S | szlachta | Kusz 175*A, 1H 105, Atl 175* | 419 | miecz, kusza 160 | - | 8 | 109 / 112 |
| Myrish Master Crossbowman | 5 | S | szlachta | Kusz 175, 1H 130, Atl 175 | 477 | miecz, kusza 300, tarcza | - | 12 | 115 / 119 |
| Myrish Artisan of War | 6 | S | szlachta | Kusz 240, 1H 210, Atl 210 | 595 | miecz, kusza 330, tarcza | - | 17 | 112 / 112 |
| Myrish Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 105 | miecz, luk 90 | - | 3 | 132 / 142 |
| Myrish Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 127 | wlocznia, topor, tarcza | - | 3 | 138 / 128 |
| Myrish Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 127 | miecz, luk 125 | - | 5 | 113 / 114 |
| Myrish Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 360 | wlocznia, miecz, tarcza | - | 5 | 139 / 128 |

#### 1.19 Tyrosh (`tyroshi`)

- Jednostek z droga do wojska: **19** (P 12, S 5, J 2, KL 0). Rekrut wsi `tyroshi_recruit`, szlachecki `tyroshi_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 109/103 / S 99/97 / J 106/98 / KL -/-.
- Krolestwo **Tyrosh** (rodow 7, szablonow 2): ludzi w partiach lordow 2068, w zalogach 1452 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 30%/50%/20%/0%, swieza partia 63%/26%/11%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 7.0% (doby 1-30: 5.3, 91-120: 6.4), sredni tier 2.49 [P]. Zold na czlowieka: swieza partia 5.89, rozwinieta 8.81 zl/dobe [K+D]. Bitwy lord z lordem [P]: 70, wygrane 52 (74%). Stracone w polu grupy wiesniakow / karawany: 10 / 0; wygrane napady na wiesniakow / karawany wroga: 9 / 0 [P]. W liczbach sa rody najemne: Moon Brothers 336 ludzi (16%). Szablony (ludzie): `tyroshi` 1732, `outlaw_battania` 336.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Tyroshi Recruit | 1 | P | wies | 1H 35, Atl 20 | 66 | miecz | - | 2 | 85 / 95 |
| Tyroshi Footman | 2 | P | wies | Dr 140*, Atl 105* | 228 | miecz, wlocznia | - | 3 | 160 / 107 |
| Tyroshi Soldier | 3 | P | wies | 1H 175*T, Atl 140* | 270 | topor, wlocznia, tarcza | - | 5 | 122 / 98 |
| Tyroshi Bowman | 3 | S | wies | Luk 70, 1H 50, Atl 105* | 228 | miecz, luk 100 | - | 5 | 100 / 100 |
| Tyroshi Horseman | 4 | J | wies | Jaz 100, 1H 175*T | 329 | topor, wlocznia, tarcza | Dornish Rounsey 54/14 | 8/12 | 107 / 97 |
| Tyroshi Axeman | 4 | P | wies | 2H 110, Atl 175*, Rzut 60 | 426 | topor 2r, toporki | - | 8 | 97 / 104 |
| Tyroshi Archer | 4 | S | wies | Luk 100, 1H 140*, Atl 105 | 228 | topor, luk 100 | - | 8 | 98 / 91 |
| Tyroshi Cavalry | 5 | J | wies | Jaz 130, 1H 175*T | 430 | topor, wlocznia, tarcza | Sand Steed 60/18 | 12/18 | 104 / 98 |
| Tyroshi Renegade | 5 | P | wies | 2H 150, Atl 175, Rzut 140 | 475 | topor 2r, toporki | - | 12 | 99 / 102 |
| Tyroshi Elite Archer | 5 | S | wies | Luk 140, 1H 140, Atl 140 | 329 | topor, luk 125 | - | 12 | 99 / 101 |
| Tyroshi Noble Youth | 2 | P | szlachta | 1H 50, Atl 50 | 90 | buzdygan | - | 3 | 62 / 79 |
| Tyroshi Boatswain | 3 | P | szlachta | 1H 175*T, Atl 105, Rzut 140* | 228 | topor, oszczepy, tarcza | - | 5 | 111 / 97 |
| Tyroshi Quartermaster | 4 | P | szlachta | 1H 175*T, Atl 175*, Rzut 140 | 454 | topor, oszczepy, tarcza | - | 8 | 122 / 115 |
| Tyroshi Firstmate | 5 | P | szlachta | 1H 175, Atl 175, Rzut 140 | 506 | topor, oszczepy, tarcza | - | 12 | 113 / 108 |
| Tyroshi Corsair | 6 | P | szlachta | 1H 230, Atl 230, Rzut 230 | 506 | topor, oszczepy, tarcza | - | 17 | 100 / 100 |
| Tyroshi Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 105 | miecz, luk 90 | - | 3 | 132 / 142 |
| Tyroshi Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 127 | wlocznia, topor, tarcza | - | 3 | 138 / 128 |
| Tyroshi Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 127 | miecz, luk 125 | - | 5 | 113 / 114 |
| Tyroshi Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 364 | wlocznia, miecz, tarcza | - | 5 | 140 / 129 |

#### 1.20 Volantis (`volantine`)

- Jednostek z droga do wojska: **29** (P 12, S 12, J 5, KL 0). Rekrut wsi `volantine_recruit`, szlachecki `volantine_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 106/104 / S 106/105 / J 101/100 / KL -/-.
- Uwaga: w tej kulturze sa tez jednostki Zlotej Kompanii (`golden_*`, `Skad` = rod Aegon/Connington/...) - to armia Aegona, nie Volantis. Slonie ma jednak takze sama Volantis: `tigercloak_camel_cavalry` to Volantene Mahout na sloniu (`Item.elephant` + `rot_elephant_armor3`, ROT-Troops.xml:16132-16161), stos 3 w szablonie kultury Volantis. W Sile jezdzcow na sloniu szarza jest ucieta do 40 (patrz 1.0).
- Krolestwo **Volantis** (rodow 9, szablonow 3): ludzi w partiach lordow 2678, w zalogach 2187 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 24%/52%/24%/0%, swieza partia 57%/30%/14%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 5.6% (doby 1-30: 10.7, 91-120: 8.7), sredni tier 2.52 [P]. Zold na czlowieka: swieza partia 6.05, rozwinieta 9.02 zl/dobe [K+D]. Bitwy lord z lordem [P]: 15, wygrane 11 (73%). Stracone w polu grupy wiesniakow / karawany: 3 / 2; wygrane napady na wiesniakow / karawany wroga: 0 / 2 [P]. W liczbach sa rody najemne: Brave Companions 25 ludzi (1%), Long Lances 24 ludzi (1%). Szablony (ludzie): `volantine` 2629, `mercenary_vlandia` 25, `outlaw_sturgia` 24.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Volantene Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Volantene Footman | 2 | P | wies | 1H 40, Atl 70 | 157 | buzdygan | - | 3 | 78 / 91 |
| Volantene Soldier | 3 | P | wies | Dr 105, Atl 140* | 307 | buzdygan, wlocznia, tarcza | - | 5 | 104 / 106 |
| Volantene Bowman | 3 | S | wies | Luk 70, 1H 60, Atl 105 | 210 | buzdygan, luk 90 | - | 5 | 97 / 98 |
| Volantene Rider | 4 | J | wies | Jaz 100, 1H 105 | 346 | topor, wlocznia, tarcza | Palfrey 54/16 | 8/12 | 92 / 99 |
| Volantene Warrior | 4 | P | wies | 1H 105, Atl 140 | 346 | topor, wlocznia, tarcza | - | 8 | 88 / 98 |
| Volantene Archer | 4 | S | wies | Luk 110, 1H 90, Atl 105 | 222 | buzdygan, luk 135 | - | 8 | 98 / 99 |
| Volantene Mahout | 5 | J | wies | Jaz 130, Dr 130 | 477 | topor, wlocznia, tarcza | Elephant 20/350 (egz.) | 12/18 | 97 / 100 |
| Volantine Elite Warrior | 5 | P | wies | 1H 140, Atl 175 | 477 | topor, wlocznia, tarcza | - | 12 | 102 / 105 |
| Volantene Master Archer | 5 | S | wies | Luk 175, 1H 120, Atl 140 | 346 | topor, luk 135 | - | 12 | 110 / 106 |
| Volantene Noble Youth | 2 | P | szlachta | 1H 70, Atl 50 | 97 | miecz | - | 3 | 75 / 81 |
| Tigercloak Elite Initiate | 3 | S | szlachta | Luk 70, Dr 70, Atl 105 | 228 | drzewc 2r, luk 90 | - | 5 | 102 / 101 |
| Tigercloak | 4 | S | szlachta | Luk 110, Dr 110, Atl 175* | 454 | drzewc 2r, luk 135 | - | 8 | 130 / 132 |
| Tigercloak Elite | 5 | S | szlachta | Luk 175, Dr 140, Atl 175 | 454 | drzewc 2r, luk 135 | - | 12 | 123 / 119 |
| Triarch Guardian | 6 | S | szlachta | Luk 240, Dr 230, Atl 220 | 536 | drzewc 2r, luk 135 | - | 17 | 102 / 102 |
| Golden Company Recruit | 2 | P | rod Aegon/Connington/Duckfield+ | 1H 140*, Atl 140* | 273 | miecz | - | 3 | 180 / 131 |
| Golden Company Giltblade Warriors | 3 | P | rod Aegon/Connington/Duckfield+ | 1H 175*T, Atl 140* | 273 | miecz, wlocznia, tarcza | - | 5 | 122 / 105 |
| Goldenmark Marksmen | 3 | S | rod Aegon/Connington/Duckfield+ | Kusz 175*A, 1H 140*, Atl 105 | 203 | miecz, kusza 120 | - | 5 | 104 / 96 |
| Golden Steed Riders | 4 | J | rod Aegon/Connington/Duckfield+ | Jaz 110, 1H 175*T | 369 | miecz, wlocznia, tarcza | Dornish Rounsey 54/14 | 8/12 | 113 / 104 |
| Golden Company Aurum Spearbearers | 4 | P | rod Aegon/Connington/Duckfield+ | 1H 175*T, Atl 175* | 380 | miecz, wlocznia, tarcza | - | 8 | 115 / 107 |
| Gleaming Shaft Marksmen | 4 | S | rod Aegon/Connington/Duckfield+ | Kusz 175*A, 1H 140, Atl 175* | 276 | miecz, kusza 200 | - | 8 | 102 / 99 |
| Golden Company Elephant Rider | 5 | J | rod Aegon/Connington/Duckfield+ | Jaz 140, 1H 175*T | 407 | miecz, wlocznia, tarcza | Elephant 20/350 (egz.) | 12/18 | 102 / 98 |
| Golden Company Gilt Pike Wardens | 5 | P | rod Aegon/Connington/Duckfield+ | 1H 175, Atl 175 | 407 | miecz, drzewc 2r, tarcza | - | 12 | 104 / 102 |
| Gilded Bolt Rangers | 5 | S | rod Aegon/Connington/Duckfield+ | Kusz 175, 1H 140, Atl 175 | 287 | miecz, kusza 200 | - | 12 | 94 / 99 |
| Golden Company Mahout | 6 | J | rod Aegon/Connington/Duckfield+ | Jaz 260, Dr 260, Rzut 260 | 419 | miecz, wlocznia, oszczepy, tarcza | Elephant 20/350 (egz.) | 17/26 | 101 / 101 |
| Volantene Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 122 | miecz, luk 90 | - | 3 | 137 / 147 |
| Volantene Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 144 | wlocznia, topor, tarcza | - | 3 | 142 / 133 |
| Volantene Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 144 | miecz, luk 125 | - | 5 | 116 / 117 |
| Volantene Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 388 | wlocznia, miecz, tarcza | - | 5 | 143 / 133 |

#### 1.21 Norvos (`norvos`)

- Jednostek z droga do wojska: **23** (P 13, S 6, J 4, KL 0). Rekrut wsi `norvos_recruit`, szlachecki `norvos_initiate`. Sila wzgledna t3+ gra/XML [D]: P 98/99 / S 95/96 / J 103/102 / KL -/-.
- Krolestwo **Norvos** (rodow 6, szablonow 1): ludzi w partiach lordow 1858, w zalogach 1242 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 32%/27%/41%/0%, swieza partia 57%/26%/17%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 14.5% (doby 1-30: 13.2, 91-120: 12.7), sredni tier 2.46 [P]. Zold na czlowieka: swieza partia 5.26, rozwinieta 9.27 zl/dobe [K+D]. Bitwy lord z lordem [P]: 49, wygrane 31 (63%). Stracone w polu grupy wiesniakow / karawany: 3 / 5; wygrane napady na wiesniakow / karawany wroga: 0 / 4 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Norvoshi Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Norvoshi Footman | 2 | P | wies | 1H 70, Atl 70 | 145 | miecz, tarcza | - | 3 | 103 / 98 |
| Norvoshi Bowman | 2 | S | wies | Luk 70, 1H 70, Atl 70 | 122 | miecz, luk 90 | - | 3 | 133 / 102 |
| Norvoshi Horseman | 3 | J | wies | Jaz 70, Dr 140* | 319 | miecz, wlocznia | Palfrey 54/16 | 5/8 | 110 / 106 |
| Norvoshi Soldier | 3 | P | wies | 1H 70, Atl 105 | 265 | topor, tarcza | - | 5 | 81 / 97 |
| Norvoshi Archer | 3 | S | wies | Luk 70, 1H 70, Atl 70 | 169 | miecz, luk 90 | - | 5 | 92 / 91 |
| Norvoshi Cavalry | 4 | J | wies | Jaz 100, Dr 140 | 419 | miecz, wlocznia, tarcza | Palfrey 54/16 | 8/12 | 107 / 107 |
| Norvoshi Axeman | 4 | P | wies | 2H 110, Atl 105 | 334 | topor, topor 2r | - | 8 | 78 / 92 |
| Norvoshi Spearman | 4 | P | wies | Dr 140, Atl 105 | 334 | topor, wlocznia | - | 8 | 86 / 92 |
| Norvoshi Elite Archer | 4 | S | wies | Luk 100, 1H 90, Atl 105 | 272 | miecz, luk 110 | - | 8 | 98 / 99 |
| Norvoshi Priest Guard | 5 | J | wies | Jaz 130, Dr 140 | 419 | topor, wlocznia, tarcza | Courser 59/19 | 12/18 | 96 / 97 |
| Norvoshi Master Axeman | 5 | P | wies | 2H 140, Atl 175 | 419 | topor, topor 2r, tarcza | - | 12 | 97 / 98 |
| Norvoshi Pikeman | 5 | P | wies | Dr 140, Atl 175 | 419 | drzewc 2r, topor, tarcza | - | 12 | 97 / 98 |
| Norvoshi Master Archer | 5 | S | wies | Luk 140, 1H 120, Atl 120 | 319 | topor, luk 110 | - | 12 | 95 / 99 |
| Norvoshi Initiate | 2 | P | szlachta | 1H 70, Atl 50 | 97 | miecz | - | 3 | 75 / 81 |
| Norvoshi Acolyte | 3 | P | szlachta | Dr 175*, Atl 140* | 298 | drzewc 2r, miecz | - | 5 | 119 / 101 |
| Norvoshi Bearded Priest | 4 | P | szlachta | Dr 175*, Atl 175* | 393 | drzewc 2r, miecz, tarcza | - | 8 | 116 / 107 |
| Norvoshi Devout Bearded Priest | 5 | P | szlachta | Dr 175, Atl 175 | 482 | drzewc 2r, miecz, tarcza | - | 12 | 111 / 105 |
| Norvoshi Grand Bearded Priest | 6 | J | szlachta | Jaz 220, Dr 230 | 511 | drzewc 2r, miecz, tarcza | Courser 59/19 | 17/26 | 98 / 98 |
| Norvoshi Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 122 | miecz, luk 90 | - | 3 | 137 / 147 |
| Norvoshi Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 144 | wlocznia, topor, tarcza | - | 3 | 142 / 133 |
| Norvoshi Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 144 | miecz, luk 125 | - | 5 | 116 / 117 |
| Norvoshi Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 105 | 302 | wlocznia, miecz, tarcza | - | 5 | 120 / 118 |

#### 1.22 Qohor (`qohorik`)

- Jednostek z droga do wojska: **22** (P 13, S 5, J 4, KL 0). Rekrut wsi `qohorik_recruit`, szlachecki `qohorik_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 108/102 / S 105/102 / J 107/100 / KL -/-.
- Krolestwo **Qohor** (rodow 7, szablonow 2): ludzi w partiach lordow 2114, w zalogach 1342 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 31%/42%/27%/0%, swieza partia 62%/23%/15%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 12.0% (doby 1-30: 14.9, 91-120: 11.5), sredni tier 2.63 [P]. Zold na czlowieka: swieza partia 5.7, rozwinieta 8.43 zl/dobe [K+D]. Bitwy lord z lordem [P]: 49, wygrane 18 (37%). Stracone w polu grupy wiesniakow / karawany: 0 / 4; wygrane napady na wiesniakow / karawany wroga: 3 / 5 [P]. W liczbach sa rody najemne: Faith Militant 201 ludzi (10%). Szablony (ludzie): `qohorik` 1913, `outlaw_empire_w` 201.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Qohorik Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Qohorik Footman | 2 | P | wies | 1H 40, Atl 140* | 255 | buzdygan, tarcza | - | 3 | 131 / 129 |
| Qohorik Rider | 3 | J | wies | Jaz 70, 1H 175* | 293 | wlocznia, miecz, tarcza | Palfrey 54/16 | 5/8 | 117 / 102 |
| Qohorik Soldier | 3 | P | wies | 1H 175*, Atl 140* | 293 | wlocznia, miecz, tarcza | - | 5 | 125 / 102 |
| Qohorik Bowman | 3 | S | wies | Luk 70, 1H 70, Atl 140* | 255 | miecz, luk 100 | - | 5 | 108 / 109 |
| Qohorik Horseman | 4 | J | wies | Jaz 110, 1H 175* | 322 | wlocznia, miecz, tarcza | Palfrey 54/16 | 8/12 | 109 / 102 |
| Qohorik Spearman | 4 | P | wies | 1H 175*, Atl 140, Rzut 140* | 322 | miecz, wlocznia, oszczepy, tarcza | - | 8 | 104 / 94 |
| Qohorik Swordsman | 4 | P | wies | 1H 175*, Atl 175*, Rzut 140* | 432 | miecz, oszczepy, tarcza | - | 8 | 120 / 111 |
| Qohorik Archer | 4 | S | wies | Luk 110, 1H 175*, Atl 140* | 267 | miecz, luk 125 | - | 8 | 113 / 104 |
| Qohorik Lancer | 5 | J | wies | Jaz 150, 1H 175 | 458 | wlocznia, miecz, tarcza | Courser 59/19 | 12/18 | 110 / 104 |
| Qohorik Elite Spearman | 5 | P | wies | 1H 175, Atl 175, Rzut 140 | 458 | wlocznia, miecz, oszczepy, tarcza | - | 12 | 108 / 103 |
| Qohorik Falxman | 5 | P | wies | 2H 150, Atl 175, Rzut 110 | 458 | miecz 2r, toporki | - | 12 | 98 / 101 |
| Qohorik Elite Archer | 5 | S | wies | Luk 130, 1H 175, Atl 140 | 294 | miecz, luk 110 | - | 12 | 95 / 94 |
| Qohorik Noble Youth | 2 | P | szlachta | 1H 175*, Atl 50 | 97 | miecz | - | 3 | 134 / 81 |
| Black Goat Initiate | 3 | P | szlachta | Dr 105, Atl 140*, Rzut 105 | 289 | wlocznia, miecz, oszczepy | - | 5 | 95 / 99 |
| Black Goat Warrior | 4 | P | szlachta | 1H 175*, Atl 140, Rzut 110 | 345 | wlocznia, miecz, oszczepy, tarcza | - | 8 | 106 / 101 |
| Black Goat Devout | 5 | P | szlachta | 1H 175, Atl 175, Rzut 140 | 439 | wlocznia, miecz, oszczepy, tarcza | - | 12 | 107 / 101 |
| Black Goat Sacrificer | 6 | J | szlachta | Jaz 210, 1H 220, Rzut 220 | 439 | wlocznia, miecz, oszczepy, tarcza | Courser 59/19 | 17/26 | 91 / 91 |
| Qohorik Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 117 | miecz, luk 90 | - | 3 | 135 / 145 |
| Qohorik Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 139 | wlocznia, topor, tarcza | - | 3 | 141 / 132 |
| Qohorik Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 139 | miecz, luk 125 | - | 5 | 115 / 116 |
| Qohorik Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 391 | wlocznia, miecz, tarcza | - | 5 | 144 / 133 |

#### 1.23 Lorath (`nord`)

- Jednostek z droga do wojska: **22** (P 15, S 5, J 2, KL 0). Rekrut wsi `nord_youngling`, szlachecki `nord_ungmann`. Sila wzgledna t3+ gra/XML [D]: P 106/99 / S 113/106 / J 107/100 / KL -/-.
- Krolestwo **Lorath** (rodow 6, szablonow 1): ludzi w partiach lordow 2743, w zalogach 1927 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 37%/40%/23%/0%, swieza partia 65%/22%/13%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 5.1% (doby 1-30: 4.4, 91-120: 5.5), sredni tier 2.57 [P]. Zold na czlowieka: swieza partia 6.72, rozwinieta 10.23 zl/dobe [K+D]. Bitwy lord z lordem [P]: 34, wygrane 26 (76%). Stracone w polu grupy wiesniakow / karawany: 6 / 2; wygrane napady na wiesniakow / karawany wroga: 0 / 3 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Lorathi Recruit | 1 | P | wies | 1H 35, Atl 20 | 73 | miecz | - | 2 | 89 / 100 |
| Lorathi Footman | 2 | P | wies | 1H 70, Atl 70 | 168 | miecz, tarcza | - | 3 | 108 / 105 |
| Lorathi Soldier | 3 | P | wies | Dr 140*, Atl 140* | 298 | wlocznia, miecz, tarcza | - | 5 | 114 / 103 |
| Lorathi Horseman | 4 | J | wies | Jaz 110, 1H 175*T | 332 | wlocznia, miecz, tarcza | Palfrey 54/16 | 8/12 | 110 / 103 |
| Lorathi Man at Arms | 4 | P | wies | 1H 175*T, Atl 140 | 337 | wlocznia, miecz, tarcza | - | 8 | 105 / 96 |
| Lorathi Archer | 4 | S | wies | Luk 110, 1H 90, Atl 140* | 301 | miecz, luk 120 | - | 8 | 107 / 108 |
| Lorathi Cavalry | 5 | J | wies | Jaz 150, 1H 175 | 385 | wlocznia, miecz, tarcza | Courser 59/19 | 12/18 | 104 / 98 |
| Lorathi Axeman | 5 | P | wies | 2H 150, Atl 175, Rzut 110 | 385 | topor 2r, toporki | - | 12 | 91 / 93 |
| Lorathi Maceman | 5 | P | wies | 1H 175, Atl 175 | 385 | buzdygan, wlocznia, tarcza | - | 12 | 102 / 96 |
| Lorathi Elite Archer | 5 | S | wies | Luk 175, 1H 175, Atl 140 | 345 | miecz, luk 180 | - | 12 | 119 / 104 |
| Lorathi Noble Youth | 2 | P | szlachta | 1H 50, Atl 50 | 97 | buzdygan | - | 3 | 64 / 81 |
| Lorathi Squire | 3 | P | szlachta | 1H 175*, Atl 140* | 301 | drzewc 2r, miecz | - | 5 | 119 / 101 |
| Lorathi Knight | 4 | P | szlachta | 1H 175*T, Atl 175* | 387 | drzewc 2r, miecz, tarcza | - | 8 | 115 / 107 |
| Lorathi Pikemaster | 5 | P | szlachta | 1H 175, Atl 175 | 400 | miecz, drzewc 2r, tarcza | - | 12 | 103 / 100 |
| Lorathi Mazeknight | 6 | P | szlachta | Dr 220, Atl 220 | 497 | miecz, drzewc 2r, tarcza | - | 17 | 96 / 97 |
| Lorathi Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 122 | miecz, luk 90 | - | 3 | 137 / 147 |
| Lorathi Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 144 | wlocznia, topor, tarcza | - | 3 | 142 / 133 |
| Lorathi Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 144 | miecz, luk 125 | - | 5 | 116 / 117 |
| Lorathi Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 140* | 311 | wlocznia, miecz, tarcza | - | 5 | 127 / 120 |
| Stone Crow Tribesman | 2 | P | najemnicy Stone Crows | 1H 70, Atl 50 | 69 | wlocznia, topor, tarcza | - | 3 | 80 / 87 |
| Stone Crow Hunter | 3 | S | najemnicy Stone Crows | Luk 70, 1H 70, Atl 70 | 103 | topor, luk 70 | - | 5 | 78 / 79 |
| Stone Crow Savage | 4 | P | najemnicy Stone Crows | 1H 140, Atl 110 | 150 | wlocznia, topor, tarcza | - | 8 | 73 / 80 |

#### 1.24 Targaryenowie (kultura valyrian) (`valyrian`)

- Jednostek z droga do wojska: **24** (P 11, S 5, J 8, KL 0). Rekrut wsi `valyrian_recruit`, szlachecki `valyrian_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 117/111 / S 99/101 / J 112/109 / KL -/-.
- Uwaga: przepis lordow Aegona to glownie jednostki Zlotej Kompanii (`golden_*`, kultura `volantine`) - sa w tabeli Volantis jako `rod Aegon/...`; jednostki `valyrian` przychodza z wiosek i zamkow kultury valyrian (Daenerys i Aegon).
- Krolestwo **Targaryen (Aegon, Zlota Kompania)** (rodow 7, szablonow 3): ludzi w partiach lordow 2233, w zalogach 2162 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 22%/46%/32%/0%, swieza partia 57%/26%/17%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 9.8% (doby 1-30: 10.3, 91-120: 7.4), sredni tier 2.62 [P]. Zold na czlowieka: swieza partia 6.67, rozwinieta 10.25 zl/dobe [K+D]. Bitwy lord z lordem [P]: 15, wygrane 4 (27%). Stracone w polu grupy wiesniakow / karawany: 0 / 3; wygrane napady na wiesniakow / karawany wroga: 3 / 2 [P]. W liczbach sa rody najemne: Brotherhood without Banners 263 ludzi (12%). Szablony (ludzie): `clan_aegon` 1831, `outlaw_vlandia` 263, `ghiscari` 139.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Valyrian Recruit | 1 | P | wies | 1H 70*, Atl 20 | 73 | miecz | - | 2 | 130 / 100 |
| Valyrian Levy | 2 | P | wies | 1H 70, Atl 105* | 257 | miecz, wlocznia | - | 3 | 127 / 126 |
| Valyrian Soldier | 3 | P | wies | 1H 175*T, Atl 175* | 341 | miecz, wlocznia, tarcza | - | 5 | 137 / 116 |
| Valyrian Bowman | 3 | S | wies | Luk 70, 1H 70, Atl 105 | 172 | miecz, luk 90 | - | 5 | 92 / 91 |
| Valyrian Scout | 4 | J | wies | Jaz 110, 1H 175*T, Rzut 140* | 392 | miecz, wlocznia, oszczepy, tarcza | Western Rounsey 53/22 | 8/12 | 116 / 110 |
| Valyrian Man at Arms | 4 | P | wies | 1H 175*T, Atl 175* | 420 | miecz, drzewc 2r, tarcza | - | 8 | 119 / 114 |
| Valyrian Archer | 4 | S | wies | Luk 100, 1H 90, Atl 175* | 341 | miecz, luk 110 | - | 8 | 107 / 108 |
| Valyrian Cavalry | 5 | J | wies | Jaz 150, 1H 175, Rzut 140 | 519 | miecz, wlocznia, oszczepy, tarcza | Western Courser 58/30 | 12/18 | 116 / 115 |
| Valyrian Elite Pikeman | 5 | P | wies | 1H 175, Atl 175 | 519 | miecz, drzewc 2r, tarcza | - | 12 | 114 / 112 |
| Valyrian Captain | 5 | P | wies | 1H 175, Atl 175 | 519 | miecz, wlocznia, tarcza | - | 12 | 114 / 110 |
| Valyrian Master Archer | 5 | S | wies | Luk 140, 1H 120, Atl 175* | 341 | miecz, luk 135 | - | 12 | 99 / 103 |
| Valyrian Noble's Son | 2 | P | szlachta | 1H 70, Atl 50 | 82 | miecz | - | 3 | 72 / 84 |
| Targaryen Squire | 3 | P | szlachta | 1H 175*T, Atl 105 | 231 | miecz, wlocznia, tarcza | - | 5 | 111 / 104 |
| Valyrian Squire | 3 | P | szlachta | Dr 140*, Atl 105 | 269 | miecz, wlocznia, tarcza | - | 5 | 105 / 110 |
| Targaryen Knight | 4 | J | szlachta | Jaz 110, 1H 175*T | 382 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 115 / 112 |
| Valyrian Knight | 4 | J | szlachta | Jaz 110, Dr 175* | 410 | miecz, wlocznia, tarcza | Western Rounsey 53/22 | 8/12 | 118 / 115 |
| Targaryen Queen's Guard | 5 | J | szlachta | Jaz 140, 1H 175 | 561 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 118 / 114 |
| Valyrian Dragonknight | 5 | J | szlachta | Jaz 140, Dr 175 | 471 | miecz, wlocznia, tarcza | Western Courser 58/30 | 12/18 | 110 / 107 |
| Captain of the Queen's Guard | 6 | J | szlachta | Jaz 220, Dr 220 | 596 | miecz, wlocznia, tarcza | Western Courser 58/30 | 17/26 | 102 / 102 |
| Valyrian Dragonlord Protector | 6 | J | szlachta | Jaz 220, Dr 220 | 537 | miecz, wlocznia, tarcza | Western Courser 58/30 | 17/26 | 98 / 98 |
| Valyrian Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 122 | miecz, luk 90 | - | 3 | 137 / 147 |
| Valyrian Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 144 | wlocznia, topor, tarcza | - | 3 | 142 / 133 |
| Valyrian Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 144 | miecz, luk 125 | - | 5 | 116 / 117 |
| Valyrian Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 353 | wlocznia, miecz, tarcza | - | 5 | 139 / 127 |

#### 1.25 Ghis / Zatoka Niewolnicza (Nieskalani) (`ghiscari`)

- Jednostek z droga do wojska: **27** (P 16, S 7, J 3, KL 1). Rekrut wsi `ghiscari_recruit`, szlachecki `ghiscari_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 97/93 / S 102/104 / J 94/95 / KL 95/96.
- Uwaga: krolestwo Daenerys (House Targaryen) ma kulture `valyrian`, ale jego lordowie uzywaja szablonu kultury `ghiscari` (5 rodow) i szablonu rodu Targaryen (3 rody: 25 Unsullied t6 + Dothrakowie z tabeli Dothrakow).
- Krolestwo **Targaryen (Daenerys)** (rodow 9, szablonow 2): ludzi w partiach lordow 3244, w zalogach 2436 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 53%/18%/17%/12%, swieza partia 54%/18%/8%/20% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 30.2% (doby 1-30: 41.0, 91-120: 25.9), sredni tier 3.6 [P]. Zold na czlowieka: swieza partia 9.24, rozwinieta 12.77 zl/dobe [K+D]. Bitwy lord z lordem [P]: 44, wygrane 14 (32%). Stracone w polu grupy wiesniakow / karawany: 4 / 1; wygrane napady na wiesniakow / karawany wroga: 17 / 29 [P]. Szablony (ludzie): `clan_targaryen` 1882, `ghiscari` 1362.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Ghiscari Recruit | 1 | P | wies, rod Aegon/Connington/Duckfield+ | 1H 35, Atl 20 | 73 | miecz | - | 2 | 89 / 100 |
| Ghiscari Footman | 2 | P | wies, rod Aegon/Connington/Duckfield+ | 1H 40, Atl 56 | 99 | miecz | - | 3 | 61 / 71 |
| Ghiscari Bowman | 2 | S | wies, rod Aegon/Connington/Duckfield+ | Luk 40, 1H 35, Atl 56 | 85 | miecz, luk 90 | - | 3 | 86 / 91 |
| Ghiscari Horseman | 3 | J | wies, rod Aegon/Connington/Duckfield+ | Jaz 70, 1H 105 | 268 | miecz, tarcza | Dornish Rounsey 54/14 | 5/8 | 93 / 98 |
| Ghiscari Warrior | 3 | P | wies, rod Aegon/Connington/Duckfield+ | 2H 140*, Atl 105 | 268 | miecz 2r | - | 5 | 98 / 64 |
| Ghiscari Archer | 3 | S | wies, rod Aegon/Connington/Duckfield+ | Luk 70, 1H 60, Atl 105 | 268 | miecz, luk 90 | - | 5 | 107 / 108 |
| Ghiscari Cavalry | 4 | J | wies, rod Aegon/Connington/Duckfield+ | Jaz 100, Dr 140 | 296 | miecz, wlocznia, tarcza | Dornish Rounsey 54/14 | 8/12 | 95 / 94 |
| Ghiscari Mounted Archer | 4 | KL | wies, rod Aegon/Connington/Duckfield+ | Jaz 100, Luk 110 | 269 | miecz, luk 110 | Dornish Rounsey 54/14 | 8/12 | 95 / 96 |
| Ghiscari Soldier | 4 | P | wies, rod Aegon/Connington/Duckfield+ | 1H 105, Atl 175*, Rzut 105 | 377 | miecz, oszczepy, tarcza | - | 8 | 96 / 101 |
| Ghiscari Pikeman | 4 | P | wies, rod Aegon/Connington/Duckfield+ | Dr 175*, Atl 140 | 330 | miecz, drzewc 2r, tarcza | - | 8 | 105 / 103 |
| Ghiscari Elite Archer | 4 | S | wies, rod Aegon/Connington/Duckfield+ | Luk 100, 1H 90, Atl 105 | 269 | miecz, luk 110 | - | 8 | 97 / 99 |
| Ghiscari Queen's Guard | 5 | J | wies, rod Aegon/Connington/Duckfield+ | Jaz 130, 2H 140 | 377 | miecz, wlocznia, tarcza | Sand Steed 60/18 | 12/18 | 93 / 94 |
| Ghiscari Elite Pikeman | 5 | P | wies, rod Aegon/Connington/Duckfield+ | Dr 175, Atl 140 | 330 | miecz, drzewc 2r, tarcza | - | 12 | 92 / 93 |
| Ghiscari Manticore | 5 | S | wies, rod Aegon/Connington/Duckfield+ | Luk 140, 2H 140, Atl 175* | 377 | miecz 2r, luk 110 | - | 12 | 102 / 104 |
| Ghiscari Former Slave | 2 | P | szlachta | 1H 60, Atl 50 | 87 | miecz | - | 3 | 67 / 85 |
| Ghiscari Legion Trainee | 3 | P | szlachta | Dr 140*, Atl 105 | 270 | miecz, wlocznia, tarcza | - | 5 | 105 / 96 |
| Ghiscari Legionnaire | 4 | P | szlachta | 2H 140, Atl 105, Rzut 105 | 296 | miecz 2r, wlocznia, oszczepy, tarcza | - | 8 | 87 / 94 |
| Ghiscari Elite Legionnaire | 5 | P | szlachta | 2H 140, Atl 175, Rzut 130 | 377 | miecz 2r, wlocznia, oszczepy, tarcza | - | 12 | 93 / 94 |
| Ghiscari Lockstep Legionnaire | 6 | P | szlachta | 2H 240, Atl 230, Rzut 220 | 377 | miecz 2r, wlocznia, oszczepy, tarcza | - | 17 | 92 / 93 |
| Unsullied | 6 | P | rod Barristan/Jorah/Mormont+ | 1H 270, Atl 270, Rzut 270 | 370 | miecz, wlocznia, oszczepy, tarcza | - | 17 | 101 / 101 |
| Ghiscari Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 105 | miecz, luk 90 | - | 3 | 132 / 142 |
| Ghiscari Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 127 | wlocznia, topor, tarcza | - | 3 | 138 / 128 |
| Ghiscari Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 127 | miecz, luk 125 | - | 5 | 113 / 114 |
| Ghiscari Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 80 | 263 | wlocznia, miecz, tarcza | - | 5 | 111 / 112 |
| Harpy Footman | 1 | P | najemnicy Sons of the Harpy | 1H 70, Atl 105 | 150 | miecz | - | 2 | 218 / 278 |
| Harpy Archer | 3 | S | najemnicy Sons of the Harpy | Luk 100, 1H 100, Atl 105 | 150 | miecz, luk 100 | - | 5 | 112 / 113 |
| Harpy Ambusher | 4 | P | najemnicy Sons of the Harpy | 1H 175, Atl 140, Rzut 140 | 360 | miecz, oszczepy | - | 8 | 103 / 105 |

#### 1.26 Qarth (`qartheen`)

- Jednostek z droga do wojska: **21** (P 10, S 8, J 3, KL 0). Rekrut wsi `qartheen_recruit`, szlachecki `qartheen_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 113/102 / S 104/105 / J 107/97 / KL -/-.
- Krolestwo **Qarth** (rodow 5, szablonow 1): ludzi w partiach lordow 1933, w zalogach 1304 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 43%/24%/33%/0%, swieza partia 62%/24%/13%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 10.1% (doby 1-30: 9.9, 91-120: 6.3), sredni tier 2.4 [P]. Zold na czlowieka: swieza partia 4.92, rozwinieta 8.62 zl/dobe [K+D]. Bitwy lord z lordem [P]: 23, wygrane 16 (70%). Stracone w polu grupy wiesniakow / karawany: 7 / 9; wygrane napady na wiesniakow / karawany wroga: 4 / 0 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Qartheen Recruit | 1 | P | wies | 1H 35, Atl 20 | 29 | miecz | - | 2 | 68 / 70 |
| Qartheen Footman | 2 | P | wies | 1H 40, Atl 40 | 43 | miecz | - | 3 | 43 / 55 |
| Qartheen Bowman | 2 | S | wies | Luk 40, 1H 35, Atl 30 | 29 | miecz, luk 90 | - | 3 | 70 / 74 |
| Qartheen Camel Rider | 3 | J | wies | Jaz 70, 1H 175*T | 305 | miecz, wlocznia, tarcza | Camel 44/10 | 5/8 | 116 / 101 |
| Qartheen Soldier | 3 | P | wies | 1H 175*T, Atl 140* | 305 | miecz, wlocznia, tarcza | - | 5 | 127 / 104 |
| Qartheen Archer | 3 | S | wies | Luk 70, 1H 60, Atl 140* | 309 | miecz, luk 90 | - | 5 | 114 / 115 |
| Qartheen Cameleer | 4 | J | wies | Jaz 100, 1H 175*T | 343 | miecz, wlocznia, tarcza | Camel 44/10 | 8/12 | 106 / 97 |
| Qartheen Hoplite | 4 | P | wies | 1H 175*T, Atl 140 | 374 | miecz, wlocznia, tarcza | - | 8 | 109 / 108 |
| Qarthene Veteran Archer | 4 | S | wies | Luk 100, 1H 90, Atl 140* | 305 | miecz, luk 110 | - | 8 | 102 / 103 |
| Qartheen Master Cameleer | 5 | J | wies | Jaz 130, 1H 175*T | 385 | miecz, wlocznia, tarcza | Husnphree 52/15 | 12/18 | 99 / 93 |
| Qartheen Elite Hoplite | 5 | P | wies | 1H 175, Atl 140 | 396 | miecz, wlocznia, tarcza | - | 12 | 98 / 99 |
| Qartheen Longbowman | 5 | S | wies | Luk 140, 1H 140*, Atl 140 | 336 | miecz, luk 87 | - | 12 | 97 / 96 |
| Qartheen Pureborn Youth | 2 | P | szlachta | 1H 60, Atl 50 | 43 | miecz | - | 3 | 58 / 73 |
| Qartheen Pureborn Fighter | 3 | P | szlachta | 1H 175*T, Atl 140* | 283 | miecz, wlocznia, tarcza | - | 5 | 124 / 98 |
| Qartheen Pureborn Warrior | 4 | P | szlachta | 1H 175*T, Atl 140, Rzut 105 | 358 | miecz, wlocznia, oszczepy, tarcza | - | 8 | 108 / 101 |
| Qartheen Pureborn Champion | 5 | S | szlachta | Luk 140, 1H 175, Atl 175 | 453 | miecz, luk 87, tarcza | - | 12 | 112 / 112 |
| Qartheen Enthroned Guardian | 6 | S | szlachta | Luk 250, 1H 240, Atl 250 | 453 | miecz, luk 87, tarcza | - | 17 | 97 / 97 |
| Qartheen Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 48 | topor, buzdygan, luk 100 | - | 3 | 118 / 127 |
| Qartheen Militia Spearman | 2 | P | milicja | 1H 80, Atl 60 | 57 | wlocznia, topor, tarcza | - | 3 | 86 / 109 |
| Qartheen Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 112 | topor, luk 100 | - | 5 | 108 / 109 |
| Qartheen Militia Veteran Spearman | 3 | P | milicja | 1H 140, Atl 80 | 139 | wlocznia, miecz, tarcza | - | 5 | 84 / 91 |

#### 1.27 Dothrakowie (`khuzait`)

- Jednostek z droga do wojska: **24** (P 7, S 5, J 3, KL 9). Rekrut wsi `khuzait_nomad`, szlachecki `khuzait_noble_son`. Sila wzgledna t3+ gra/XML [D]: P 75/86 / S 95/96 / J 83/91 / KL 99/99.
- Krolestwo **Dothrakowie** (rodow 16, szablonow 1): ludzi w partiach lordow 2112, w zalogach 2262 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 24%/48%/7%/21%, swieza partia 54%/25%/4%/18% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 18.5% (doby 1-30: 23.4, 91-120: 19.3), sredni tier 2.41 [P]. Zold na czlowieka: swieza partia 5.3, rozwinieta 7.93 zl/dobe [K+D]. Bitwy lord z lordem [P]: 21, wygrane 14 (67%). Stracone w polu grupy wiesniakow / karawany: 11 / 19; wygrane napady na wiesniakow / karawany wroga: 0 / 1 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Dothraki Nomad | 1 | P | wies | 1H 20, Atl 70* | 106 | buzdygan | - | 2 | 116 / 122 |
| Dothraki Tribal Warrior | 2 | KL | wies, rod Barristan/Jorah/Mormont+ | Jaz 50, Luk 40 | 108 | miecz, luk 90 | Palfrey 54/16 | 3/4 | - / - |
| Dothraki Footman | 2 | P | wies | 1H 70, Atl 70 | 108 | miecz | - | 3 | 83 / 84 |
| Dothraki Rider | 3 | J | wies, rod Barristan/Jorah/Mormont+ | Jaz 70, 1H 80 | 160 | miecz | Palfrey 54/16 | 5/8 | 73 / 87 |
| Dothraki Raider | 3 | KL | wies, rod Barristan/Jorah/Mormont+ | Jaz 70, Luk 70 | 159 | miecz, luk 125 | Palfrey 54/16 | 5/8 | 94 / 94 |
| Dothraki Warrior | 3 | P | wies | 1H 80, Atl 105 | 253 | miecz | - | 5 | 76 / 93 |
| Dothraki Hunter | 3 | S | wies, rod Barristan/Jorah/Mormont+ | Luk 70, 1H 70, Atl 105 | 159 | miecz, luk 100 | - | 5 | 91 / 92 |
| Dothraki Horse Warrior | 4 | J | wies, rod Barristan/Jorah/Mormont+ | Jaz 100, 1H 110 | 283 | miecz, drzewc 2r | Palfrey 54/16 | 8/12 | 87 / 95 |
| Dothraki Horse Archer | 4 | KL | wies, rod Barristan/Jorah/Mormont+ | Jaz 110, Luk 100 | 280 | miecz, luk 100 | Palfrey 54/16 | 8/12 | 96 / 97 |
| Dothraki Savage | 4 | P | wies | 1H 100, Atl 110, Rzut 140* | 285 | miecz, oszczepy | - | 8 | 71 / 84 |
| Dothraki Archer | 4 | S | wies, rod Barristan/Jorah/Mormont+ | Luk 110, 1H 90, Atl 105 | 252 | miecz, luk 125 | - | 8 | 101 / 102 |
| Dothraki Grass Sea Lancer | 5 | J | wies, rod Barristan/Jorah/Mormont+ | Jaz 150, 1H 140 | 287 | miecz, wlocznia | Palfrey 54/16 | 12/18 | 89 / 91 |
| Dothraki Mounted Bowlord | 5 | KL | wies, rod Barristan/Jorah/Mormont+ | Jaz 130, Luk 140 | 283 | miecz, luk 110 | Palfrey 54/16 | 12/18 | 94 / 94 |
| Dothraki Barbarian | 5 | P | wies | 1H 140, Atl 150, Rzut 140 | 289 | miecz, wlocznia, oszczepy | - | 12 | 77 / 82 |
| Dothraki Bowlord | 5 | S | wies, rod Barristan/Jorah/Mormont+ | Luk 140, 1H 130, Atl 130 | 275 | miecz, luk 110 | - | 12 | 92 / 95 |
| Dothraki Ko's Son | 2 | KL | szlachta | Jaz 40, Luk 40 | 134 | miecz, luk 90 | Palfrey 54/16 | 3/4 | - / - |
| Dothraki Screamer | 3 | KL | szlachta | Jaz 70, Luk 70 | 160 | miecz, luk 100 | Palfrey 54/16 | 5/8 | 95 / 95 |
| Dothraki Marauder | 4 | KL | szlachta | Jaz 110, Luk 150 | 276 | miecz, luk 125 | Palfrey 54/16 | 8/12 | 111 / 111 |
| Dothraki Elite Screamer | 5 | KL | szlachta, rod Barristan/Jorah/Mormont+ | Jaz 140, Luk 160 | 289 | miecz, luk 135 | Palfrey 54/16 | 12/18 | 102 / 102 |
| Dothraki Khal's Guard | 6 | KL | szlachta, rod Barristan/Jorah/Mormont+ | Jaz 240, Luk 260 | 293 | miecz, luk 135 | Palfrey 54/16 | 17/26 | - / - |
| Dothraki Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 108 | miecz, luk 90 | - | 3 | 133 / 143 |
| Dothraki Militia Spearman | 2 | P | milicja | Dr 140*, Atl 105 | 133 | wlocznia, topor, tarcza | - | 3 | 150 / 130 |
| Dothraki Militia Veteran Archer | 4 | S | milicja | Luk 140, 1H 110, Atl 130 | 133 | miecz, luk 125 | - | 8 | 101 / 102 |
| Dothraki Militia Veteran Spearman | 4 | P | milicja | 1H 130, Atl 110 | 275 | wlocznia, miecz, tarcza | - | 8 | 83 / 99 |

#### 1.28 Sarnor (`sarnor`)

- Jednostek z droga do wojska: **22** (P 11, S 6, J 5, KL 0). Rekrut wsi `sarnor_recruit`, szlachecki `sarnor_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 99/94 / S 99/98 / J 101/97 / KL -/-.
- Krolestwo **Sarnor** (rodow 7, szablonow 2): ludzi w partiach lordow 2917, w zalogach 2093 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 29%/33%/38%/0%, swieza partia 58%/27%/15%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 13.7% (doby 1-30: 12.6, 91-120: 15.3), sredni tier 2.41 [P]. Zold na czlowieka: swieza partia 5.04, rozwinieta 8.92 zl/dobe [K+D]. Bitwy lord z lordem [P]: 15, wygrane 6 (40%). Stracone w polu grupy wiesniakow / karawany: 0 / 4; wygrane napady na wiesniakow / karawany wroga: 5 / 0 [P]. W liczbach sa rody najemne: Sons of the Harpy 345 ludzi (12%). Szablony (ludzie): `sarnor` 2572, `outlaw_empire_e` 345.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Sarnori Recruit | 1 | P | wies | 1H 35, Atl 20 | 66 | miecz | - | 2 | 85 / 95 |
| Sarnori Footman | 2 | P | wies | 1H 175*T, Atl 40 | 94 | miecz, tarcza | - | 3 | 142 / 84 |
| Sarnori Bowman | 2 | S | wies | Luk 40, 1H 35, Atl 56 | 73 | miecz, luk 90 | - | 3 | 83 / 87 |
| Sarnori Horseman | 3 | J | wies | Jaz 70, 1H 140* | 259 | miecz, wlocznia | Palfrey 54/16 | 5/8 | 103 / 97 |
| Sarnori Warrior | 3 | P | wies | 1H 175*T, Atl 105 | 233 | miecz, wlocznia, tarcza | - | 5 | 111 / 96 |
| Sarnori Archer | 3 | S | wies | Luk 70, 1H 60, Atl 105 | 229 | miecz, luk 90 | - | 5 | 100 / 101 |
| Sarnori Cavalry | 4 | J | wies | Jaz 100, 1H 175*T | 303 | miecz, wlocznia, tarcza | Palfrey 54/16 | 8/12 | 104 / 95 |
| Sarnori Glaiveman | 4 | P | wies | 1H 175*T, Atl 105 | 303 | miecz, drzewc 2r, tarcza | - | 8 | 97 / 95 |
| Sarnori Spearman | 4 | P | wies | 1H 175*T, Atl 105 | 303 | miecz, wlocznia, tarcza | - | 8 | 97 / 95 |
| Sarnori Elite Archer | 4 | S | wies | Luk 100, 1H 140*, Atl 105 | 233 | miecz, luk 100 | - | 8 | 98 / 93 |
| High King Guardian | 5 | J | wies | Jaz 130, 1H 175*T, Rzut 130 | 406 | miecz, wlocznia, oszczepy, tarcza | Courser 59/19 | 12/18 | 102 / 96 |
| Sarnori Master Spearman | 5 | P | wies | 1H 175, Atl 175, Rzut 130 | 384 | miecz, wlocznia, oszczepy, tarcza | - | 12 | 101 / 94 |
| Sarnori Longbowman | 5 | S | wies | Luk 140, 1H 140, Atl 175* | 340 | miecz, luk 120 | - | 12 | 100 / 101 |
| Sarnori Noble's Son | 2 | P | szlachta | 1H 50, Atl 50 | 90 | miecz | - | 3 | 62 / 79 |
| Sarnori Javelineer | 3 | P | szlachta | 1H 140*, Atl 105, Rzut 105 | 259 | drzewc 2r, miecz, oszczepy | - | 5 | 97 / 94 |
| Sarnori Elite Javelinier | 4 | P | szlachta | 1H 140, Atl 140, Rzut 110 | 325 | drzewc 2r, miecz, oszczepy | - | 8 | 90 / 93 |
| Sarnori Master Javelinier | 5 | J | szlachta | Jaz 150, 1H 140, Rzut 140 | 406 | drzewc 2r, miecz, oszczepy | Palfrey 54/16 | 12/18 | 98 / 101 |
| Sarnori Spider | 6 | J | szlachta | Jaz 260, Dr 230, Rzut 250 | 406 | drzewc 2r, oszczepy | Courser 59/19 | 17/26 | 97 / 97 |
| Sarnori Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 94 | miecz, luk 90 | - | 3 | 129 / 139 |
| Sarnori Militia Spearman | 2 | P | milicja | Dr 140*, Atl 60 | 116 | wlocznia, topor, tarcza | - | 3 | 133 / 125 |
| Sarnori Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 116 | miecz, luk 125 | - | 5 | 111 / 112 |
| Sarnori Militia Veteran Spearman | 3 | P | milicja | 1H 175*T, Atl 175* | 336 | wlocznia, miecz, tarcza | - | 5 | 136 / 124 |

#### 1.29 Ibben (`ibbenese`)

- Jednostek z droga do wojska: **19** (P 13, S 4, J 2, KL 0). Rekrut wsi `ibbenese_recruit`, szlachecki `ibbenese_noble_recruit`. Sila wzgledna t3+ gra/XML [D]: P 102/102 / S 106/100 / J 92/92 / KL -/-.
- Krolestwo **Ibben** (rodow 7, szablonow 3): ludzi w partiach lordow 2274, w zalogach 1550 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 34%/40%/26%/0%, swieza partia 64%/22%/14%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 5.4% (doby 1-30: 8.7, 91-120: 5.7), sredni tier 2.48 [P]. Zold na czlowieka: swieza partia 6.6, rozwinieta 10.15 zl/dobe [K+D]. Bitwy lord z lordem [P]: 15, wygrane 9 (60%). Stracone w polu grupy wiesniakow / karawany: 5 / 0; wygrane napady na wiesniakow / karawany wroga: 1 / 2 [P]. W liczbach sa rody najemne: Company of the Cat 24 ludzi (1%), The Windblown 64 ludzi (3%). Szablony (ludzie): `ibbenese` 2186, `mercenary_aserai` 64, `mercenary_empire` 24.

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Ibbenese Recruit | 1 | P | wies | 1H 35, Atl 36 | 62 | miecz | - | 2 | 93 / 92 |
| Ibbenese Footman | 2 | P | wies | 1H 40, Atl 40 | 126 | miecz | - | 3 | 62 / 78 |
| Ibbenese Tracker | 3 | P | wies | 1H 140*, Atl 105 | 248 | miecz, wlocznia, tarcza | - | 5 | 102 / 94 |
| Ibbenese Rider | 4 | J | wies | Jaz 100, 1H 140* | 248 | miecz, wlocznia, tarcza | Hunter 55/7 | 8/12 | 90 / 88 |
| Ibbenese Warrior | 4 | P | wies | 1H 140, Atl 140 | 341 | miecz, wlocznia, tarcza | - | 8 | 97 / 104 |
| Ibbenese Hunter | 4 | S | wies | Luk 110, 1H 90, Atl 105 | 248 | miecz, luk 100 | - | 8 | 98 / 100 |
| Ibbenese Horseman | 5 | J | wies | Jaz 130, 1H 140 | 422 | miecz, wlocznia, tarcza | Hunter 55/7 | 12/18 | 95 / 96 |
| Ibbenese Timberman | 5 | P | wies | 2H 140, Atl 175, Rzut 130 | 480 | topor 2r, oszczepy | - | 12 | 98 / 100 |
| Ibbenese Whaler | 5 | P | wies | Dr 150, Atl 175 | 480 | miecz, wlocznia, tarcza | - | 12 | 105 / 108 |
| Ibbenese Master Huntsman | 5 | S | wies | Luk 175, 1H 140*, Atl 140 | 326 | miecz, luk 180 | - | 12 | 113 / 101 |
| Ibbenese Deckhand | 2 | P | szlachta | 1H 60, Atl 50 | 126 | miecz | - | 3 | 76 / 96 |
| Ibbenese Rower | 3 | P | szlachta | 1H 140*, Atl 105 | 248 | miecz, wlocznia, tarcza | - | 5 | 102 / 92 |
| Ibbenese Sailor | 4 | P | szlachta | 1H 140, Atl 140, Rzut 105 | 366 | miecz, wlocznia, oszczepy, tarcza | - | 8 | 99 / 102 |
| Ibbenese Mariner | 5 | P | szlachta | 1H 140, Atl 175 | 488 | miecz, drzewc 2r, tarcza | - | 12 | 103 / 106 |
| Ibbenese Navigator | 6 | P | szlachta | Dr 250, Atl 250, Rzut 230 | 518 | miecz, drzewc 2r, oszczepy, tarcza | - | 17 | 106 / 106 |
| Ibbenese Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 66 | topor, buzdygan, luk 60 | - | 3 | 116 / 124 |
| Ibbenese Militia Spearman | 2 | P | milicja | Dr 105, Atl 60 | 117 | wlocznia, topor, tarcza | - | 3 | 114 / 126 |
| Ibbenese Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 105 | 174 | topor, luk 120 | - | 5 | 121 / 122 |
| Ibbenese Militia Veteran Spearman | 3 | P | milicja | 2H 105*, Atl 105 | 220 | wlocznia, topor, tarcza | - | 5 | 87 / 104 |

#### 1.30 Yi Ti (`yiti`)

- Jednostek z droga do wojska: **15** (P 7, S 5, J 3, KL 0). Rekrut wsi `yiti_recruit`, szlachecki `yiti_recruit`. Sila wzgledna t3+ gra/XML [D]: P 97/90 / S 99/101 / J 98/94 / KL -/-.
- Krolestwo **Yi Ti (wygnancy)** (rodow 2, szablonow 1): ludzi w partiach lordow 98, w zalogach 503 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 24%/48%/28%/0%, swieza partia 58%/26%/15%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 6.7% (doby 1-30: 2.9, 91-120: 0.0), sredni tier 2.33 [P]. Zold na czlowieka: swieza partia 6.19, rozwinieta 9.38 zl/dobe [K+D]. Bitew lord z lordem [P]: brak. Stracone w polu grupy wiesniakow / karawany: 0 / 0; wygrane napady na wiesniakow / karawany wroga: 0 / 0 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Yi Ti Recruit | 1 | P | wies, szlachta | 1H 20, Atl 20 | 88 | buzdygan, wlocznia | - | 2 | 78 / 110 |
| Yi Ti Footman | 2 | P | wies, szlachta | 1H 50, Atl 70 | 136 | miecz | - | 3 | 78 / 92 |
| Yi Ti Infantryman | 3 | P | wies, szlachta | 1H 175*T, Atl 70 | 206 | miecz, tarcza | - | 5 | 102 / 87 |
| Yi Ti Bowman | 3 | S | wies, szlachta | Luk 70, 1H 60, Atl 105 | 223 | miecz, luk 90 | - | 5 | 99 / 100 |
| Yi Ti Rider | 4 | J | wies, szlachta | Jaz 110, 1H 175*T | 254 | miecz, wlocznia, tarcza | Palfrey 54/16 | 8/12 | 102 / 92 |
| Yi Ti Spearman | 4 | P | wies, szlachta | 1H 175*T, Atl 110 | 254 | miecz, wlocznia, tarcza | - | 8 | 93 / 90 |
| Yi Ti Archer | 4 | S | wies, szlachta | Luk 100, 1H 100, Atl 105 | 254 | miecz, luk 110 | - | 8 | 97 / 98 |
| Yi Ti Glaiveman | 5 | J | wies, szlachta | Jaz 140, 1H 175*T | 403 | miecz, drzewc 2r, tarcza | Palfrey 54/16 | 12/18 | 103 / 99 |
| Yi Ti Shi | 5 | P | wies, szlachta | Dr 175, Atl 150, Rzut 150 | 403 | miecz, drzewc 2r, noze | - | 12 | 96 / 94 |
| Yi Ti Marksman | 5 | S | wies, szlachta | Luk 140, 1H 120, Atl 140 | 373 | miecz, luk 135 | - | 12 | 102 / 106 |
| Yi Ti Mounted Shi | 6 | J | wies, szlachta | Jaz 210, 1H 210, Rzut 250 | 456 | miecz, drzewc 2r, noze | Grass Sea Steed 60/20 | 17/26 | 90 / 90 |
| Yitish Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 122 | miecz, luk 90 | - | 3 | 137 / 147 |
| YiTish Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 136 | wlocznia, topor, tarcza | - | 3 | 140 / 131 |
| YiTish Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 136 | miecz, luk 125 | - | 5 | 115 / 116 |
| YiTish Militia Veteran Spearman | 3 | P | milicja | Dr 105, Atl 140* | 262 | wlocznia, miecz, tarcza | - | 5 | 98 / 111 |

#### 1.31 Wyspy Letnie (`summer`)

- Jednostek z droga do wojska: **15** (P 7, S 6, J 2, KL 0). Rekrut wsi `summer_recruit`, szlachecki `summer_recruit`. Sila wzgledna t3+ gra/XML [D]: P 93/97 / S 100/99 / J 98/98 / KL -/-.
- Krolestwo **Wyspy Letnie** (rodow 2, szablonow 1): ludzi w partiach lordow 128, w zalogach 605 [P, doba 120]. Cel ROT (stosy t3+) P/S/J/KL = 24%/52%/24%/0%, swieza partia 58%/28%/13%/0% [D, wazone ludzmi rodow]. Partie lordow w bitwach polowych: konni 7.9% (doby 1-30: 11.9, 91-120: -), sredni tier 2.65 [P]. Zold na czlowieka: swieza partia 6.02, rozwinieta 9.07 zl/dobe [K+D]. Bitew lord z lordem [P]: brak. Stracone w polu grupy wiesniakow / karawany: 0 / 0; wygrane napady na wiesniakow / karawany wroga: 0 / 0 [P].

| Jednostka | T | Rodzaj | Skad | Umiejetnosci w grze | Pancerz | Uzbrojenie | Kon (wzorzec) | Zold | Sila gra / XML |
|---|---|---|---|---|---|---|---|---|---|
| Summer Isles Recruit | 1 | P | wies, szlachta | 1H 20, Atl 20 | 66 | buzdygan, wlocznia | - | 2 | 67 / 95 |
| Summer Isles Footman | 2 | P | wies, szlachta | Dr 140*, Atl 70 | 105 | miecz, wlocznia | - | 3 | 122 / 83 |
| Summer Isles Infantryman | 3 | P | wies, szlachta | Dr 140*, Atl 105 | 225 | miecz, wlocznia, tarcza | - | 5 | 99 / 97 |
| Summer Isles Bowman | 3 | S | wies, szlachta | Luk 70, 1H 70, Atl 105 | 225 | miecz, luk 130 | - | 5 | 106 / 107 |
| Summer Isles Scout | 4 | J | wies, szlachta | Jaz 100, Dr 140 | 389 | miecz, wlocznia | Dornish Rounsey 54/14 | 8/12 | 104 / 104 |
| Summer Isles Spearman | 4 | P | wies, szlachta | Dr 140, Atl 120 | 281 | miecz, wlocznia, tarcza | - | 8 | 88 / 95 |
| Summer Isles Archer | 4 | S | wies, szlachta | Luk 100, 1H 100, Atl 105 | 225 | miecz, luk 130 | - | 8 | 95 / 96 |
| Summer Isles Horseman | 5 | J | wies, szlachta | Jaz 130, Dr 140 | 363 | miecz, wlocznia, tarcza | Dornish Rounsey 54/14 | 12/18 | 91 / 93 |
| Summer Isles Spearmaster | 5 | P | wies, szlachta | Dr 150, Atl 150, Rzut 140* | 378 | miecz, wlocznia, oszczepy, tarcza | - | 12 | 92 / 99 |
| Summer Isles Longbowman | 5 | S | wies, szlachta | Luk 175, 1H 120, Atl 140 | 288 | miecz, luk 130 | - | 12 | 104 / 97 |
| Goldenheart Warrior | 6 | S | wies, szlachta | Luk 250, 1H 200, Atl 200 | 378 | miecz, luk 200 | - | 17 | 95 / 95 |
| Summer Isles Militia Archer | 2 | S | milicja | Luk 80, 1H 50, Atl 70 | 105 | miecz, luk 90 | - | 3 | 132 / 142 |
| Summer Isles Militia Spearman | 2 | P | milicja | Dr 140*, Atl 70 | 127 | wlocznia, topor, tarcza | - | 3 | 138 / 128 |
| Summer Isles Militia Veteran Archer | 3 | S | milicja | Luk 110, 1H 80, Atl 100 | 127 | miecz, luk 125 | - | 5 | 113 / 114 |
| Summer Isles Militia Veteran Spearman | 3 | P | milicja | Dr 105, Atl 105 | 307 | wlocznia, miecz, tarcza | - | 5 | 98 / 119 |


---

## 2. Porownanie krain i ocena balansu

### 2.1 Jak gra rozstrzyga bitwy AI z AI [K] i co to znaczy dla balansu

Bitwy bez gracza (AI z AI, a takze "wyslij wojsko") ida przez symulacje, nie przez scene bitwy:

- **Moc zolnierza zalezy tylko od tieru** - `DefaultMilitaryPowerModel.GetDefaultTroopPower` = (2 + tier) x (10 + tier) x 0.02, czyli tier 1 = 0.66, t2 = 0.96,
  t3 = 1.30, t4 = 1.68, t5 = 2.10, t6 = 2.56. ROT (`ROT.Models.ROTMilitaryPowerModel`) przekazuje to dalej i dodaje tylko +100 dla smokow.
- **Cios w symulacji** - `DefaultCombatSimulationModel.SimulateHit`: obrazenia = 40 x (moc bijacego / moc trafianego)^0.7 x przewaga strony x los 0.5-1;
  kazdy zolnierz ma 100 punktow zycia (`DefaultCharacterStatsModel.MaxHitpoints`). Metoda, ktora liczylaby pancerz i bron
  (`CharacterObject.GetSimulationAttackPower`), w bitwach 1.4.8 nie jest wolana - jedyne wywolanie w grze to symulacja meczu turnieju
  (`SandBox`, `TournamentFightMissionController.Simulate`); sprawdzone w zdekompilowanym TaleWorlds.CampaignSystem, SandBox i NavalDLC.
  RBM tej czesci nie zmienia (jego `OverrideDefaultMilitaryPowerModel` to w srodku morale i sprzet bohaterow w scenie). DTE tez nie: symulacja nie patrzy
  w zbrojownie (specyfikacja 171, rozdz. 1.4).
- **Dodatki do mocy** (`GetContextModifier`, tabela `_battleModifiers`): jazda atakujaca na rowninie/stepie/pustyni/sniegu +25% (broniaca +10%),
  konni lucznicy +30% / +15%; w lesie jazda -20% / -15%, konni lucznicy -30% / -25%, lucznicy broniacy sie w lesie -50%; przy oblezeniu lucznicy obroncy +30%,
  atakujacy -20%, piechota 0, jazda -10%, konni lucznicy atakujacy -20% (obroncy +30%). Do tego perki dowodcy (+1% do +6% za perk), morale ponizej 30 (x0.7),
  BannerKings: innowacja Strzemiona +20% dla CALEJ kultury i perk Planista Oblezen; nasza zasada valyrianska (Wedrowcy).
  Teren bitew polowych w logu [P]: rownina 53%, las 23%, step 15%, pustynia 9%.
- **Straty przegranych - H3 (Armoury `LosersFlee`, w grze od wgrania 4) [K]:** po bitwie polowej czesc przegranych ginie, czesc trafia do niewoli, reszta ucieka.
  Udzial zabitych p = 0.15 + 0.35 x C + 0.15 x T + 0.15 x O + 0.20 x Q - 0.10 x F x (1 - T), w granicach 5-65% (LosersFlee.cs:57, :643-648), gdzie
  C = o ile zwyciezca ma wiekszy udzial konnych, Q = o ile wyzszy tier (na 3 tiery), O = przewaga liczebna ponad 1,5 raza, T = teren-pulapka,
  F = o ile przegrany ma wiecej konnych (konni lepiej uciekaja). [P] W bitwach krolestw srednie C = 0.04 (najwyzej 0.73), srednie p = 30%.
  **Sklad dziala wiec w autobitwie dwa razy:** przez dodatki terenu (kto wygra) i przez H3 (ilu wrogow zginie, ilu wlasnych ucieknie) - a to decyduje,
  jak dlugo kraina utrzyma ludzi i tier.

**Dowod z logu [P]:** w **513 bitwach lorda z lordem** (obie strony to partie lordow krolestw; bez wiesniakow, karawan i patroli) wygrala strona z wieksza suma
"ludzie x moc tieru" w **98,2%** (504), strona liczniejsza w 97,3%, strona z wyzszym srednim tierem w 60,0%, strona z wiekszym udzialem konnych w 44,6%.
To jednak w duzej mierze skutek tego, ze AI wybiera bitwy, ktore wygra: mediana stosunku sil to 3,0 : 1 (we wszystkich 932 bitwach krolestw 4,3 : 1).
W **bitwach wyrownanych** (stosunek sil ponizej 1,5 : 1) silniejszy wygral **61 z 69 razy (88%)**; liczac wszystkie bitwy krolestw - 76 z 90 (84%).
Wniosek "pancerz, bron i umiejetnosci nie licza sie w autobitwie" wynika z kodu (wyzej), a 98% opisuje glownie to, jak AI wybiera, z kim walczy.

**Wniosek [ocena]:**
- **Balans krolestw na mapie** = ile ludzi kraina wystawia (gospodarka, liczba rodow, zalogi, werbunek), jak wysoki maja tier i ilu traci w przegranych (H3).
  Pancerz, bron i umiejetnosci jednostek nie zmieniaja wyniku wojen AI.
- **Jakosc jednostek** = to, co widzisz w bitwach z Twoim udzialem (i w dzialaniu DTE). Tu sila 75 czy 125 na 100 ma znaczenie.
- **Sklad** dziala w obu miejscach: w autobitwie przez dodatki terenu (jazda na otwartym polu, lucznicy na murach, slabosc w lesie) i przez H3 (jazda zwyciezcy
  zabija wiecej uciekajacych, jazda przegranego latwiej ucieka); w Twoich bitwach wprost. Uwaga: autobitwa liczy jako konnego kazdego z `IsMounted` wzorca,
  takze jezdzca bez konia w zbrojowni (rozdzial 4, "konni na papierze").
- **Moc za zold** spada z tierem: t1 daje 0.33 mocy za 1 zl zoldu dziennie, t3 0.26, t5 0.18, t6 0.15; jezdziec t4 z premia Armoury 0.14 (piechur t4 0.21).
  W autobitwie tani rekruci sa najbardziej oplacalni, a jazda placi 1.5 raza wiecej za ta sama moc i odzyskuje to tylko na otwartym terenie i w H3.

### 2.2 Tabela porownawcza krolestw

**Kolumny:** ludzie w partiach lordow / w zalogach (doba 120) [P]; **partie lordow w bitwach polowych**: sredni tier i udzial konnych (bez wiesniakow, karawan
i patroli) [P]; cel ROT P/S/J/KL [D]; **sila wzorca t3+** = srednia Sila (100 = srednia swiata dla rodzaju i tieru) jednostek tieru 3+ ze stosow szablonow
WSZYSTKICH rodow krolestwa, wazona wielkoscia stosu i liczba ludzi w partiach rodow z tym szablonem - w grze i wedlug XML ROT [D]. To miara **wzorca szablonu**,
a nie tego, co AI dokladnie wystawia: ROT losuje zamiany sposrod calych drzew, a jednostki t4-t6 w polu to wynik awansow (1.0). Srednia ze wszystkich jednostek
t3+ calych drzew szablonow (kazda jednostka puli po rowno, jak w losowaniu ROT) rozni sie zwykle o 0-5 punktow, najwyzej o 10-11 (piechota Aegona 115 -> 104,
piechota i strzelcy Myr 78 / 89 -> 88 / 99, jazda Dothrakow 73 -> 83, strzelcy Volantis 101 -> 110) - podaje ja w 2.3 tam, gdzie zmienia wniosek
(`armie14r/sila_koszt2.json`, pole `drzewa_*`).
Pancerz wzorca = sredni pancerz tych samych jednostek (zalezy tez od tierow, wiec porownuj ostroznie) [D]; zold na czlowieka swiezej i rozwinietej partii z premia
konnych [K+D]; moc autobitwy rozwinietej partii na 100 zl zoldu dziennie [K]; **bitwy lorda z lordem / wygrane** [P] (przy mniej niz 15 bitwach - mala proba);
**stracone wsie / karawany** = ile grup wiesniakow i karawan krolestwa rozbil w polu wrog [P]. Wszystkie kolumny z szablonow sa wazone ludzmi w partiach.

| Krolestwo | Ludzi w partiach / zalogach [P] | Partie lordow w bitwach: tier / konni % [P] | Cel P/S/J/KL % [D] | Sila wzorca t3+ gra P/S/J/KL [D] | Sila wzorca t3+ XML (bez SkillSinew) [D] | Pancerz wzorca P/S/J/KL [D] | Zold na czlowieka swieza / rozwinieta [K+D] | Moc autobitwy na 100 zl [K] | Bitwy lord z lordem / wygrane [P] | Stracone wsie / karawany [P] |
|---|---|---|---|---|---|---|---|---|---|---|
| Polnoc | 11868 / 8880 | 2.76 / 4.5 | 35/43/21/0 | 98/102/96/106 | 99/101/96/106 | 326/273/335/379 | 5.87 / 8.8 | 18.6 | 5 / 80% (mala proba) | 1 / 27 |
| Dolina | 7738 / 5264 | 2.62 / 12.6 | 32/35/33/0 | 95/93/91/- | 101/94/101/- | 333/221/337/- | 5.5 / 9.19 | 17.7 | 6 / 83% (mala proba) | 6 / 15 |
| Dorzecze | 7526 / 3585 | 2.33 / 4.9 | 35/40/25/0 | 101/96/97/- | 99/98/93/- | 301/235/299/- | 5.8 / 9.33 | 18.0 | 5 / 20% (mala proba) | 3 / 1 |
| Zelazne Wyspy | 5765 / 4825 | 2.52 / 4.2 | 38/41/21/0 | 102/95/94/- | 105/97/99/- | 347/236/375/- | 5.82 / 8.7 | 18.6 | brak | 0 / 2 |
| Korona (Westerlands+KP) | 10148 / 7682 | 2.52 / 8.8 | 36/34/31/0 | 103/95/99/- | 102/94/100/- | 345/207/391/- | 5.64 / 9.76 | 17.1 | 5 / 20% (mala proba) | 3 / 9 |
| Reach | 6929 / 6358 | 2.52 / 7.9 | 33/36/32/0 | 95/100/94/- | 101/99/98/- | 335/274/366/- | 5.95 / 10.09 | 17.0 | 37 / 57% | 7 / 14 |
| Krainy Burzy | 5401 / 3606 | 2.43 / 8.1 | 40/35/25/0 | 100/98/95/- | 102/99/96/- | 346/262/381/- | 5.83 / 9.85 | 17.2 | 120 / 45% | 57 / 38 |
| Smocza Skala | 4953 / 3297 | 2.66 / 11.5 | 29/46/25/0 | 109/91/98/- | 103/91/98/- | 365/204/368/- | 6.28 / 9.36 | 17.8 | 107 / 57% | 18 / 4 |
| Dorne | 6776 / 4152 | 2.66 / 15.4 | 21/39/13/26 | 100/108/97/106 | 109/109/101/105 | 335/291/385/296 | 6.51 / 8.74 | 17.7 | 24 / 33% | 6 / 12 |
| Nocna Straz | 2100 / 1435 | 2.51 / 3.6 | 23/60/17/0 | 74/91/82/- | 86/88/85/- | 212/188/299/- | 6.13 / 9.17 | 18.2 | 100 / 52% | 0 / 0 |
| Wolni Ludzie | 2063 / 1406 | 2.44 / 4.8 | 51/29/20/0 | 83/110/91/- | 90/101/85/- | 255/216/288/- | 5.19 / 9.3 | 17.9 | 100 / 48% | 20 / 44 |
| Skagos | 76 / 236 | 2.6 / 17.6 | 34/48/17/0 | 113/86/116/- | 103/86/103/- | 326/171/370/- | 5.62 / 8.34 | 19.1 | brak | 0 / 0 |
| Braavos | 2185 / 1407 | 2.55 / 5.0 | 52/36/6/6 | 113/91/104/96 | 101/96/94/96 | 333/234/291/408 | 5.48 / 8.55 | 19.2 | 33 / 21% | 0 / 2 |
| Pentos | 2718 / 1826 | 2.58 / 7.8 | 30/40/27/3 | 125/104/110/102 | 108/106/104/102 | 387/337/429/354 | 7.0 / 10.73 | 16.6 | 32 / 28% | 1 / 8 |
| Lys | 1083 / 1269 | 2.6 / 8.3 | 30/47/23/0 | 104/98/106/- | 96/99/98/- | 341/254/380/- | 6.11 / 9.14 | 18.1 | 70 / 26% | 9 / 0 |
| Myr | 3739 / 2050 | 2.69 / 7.5 | 21/55/24/0 | 78/89/96/- | 87/90/101/- | 255/218/401/- | 5.99 / 8.96 | 18.2 | 32 / 72% | 1 / 1 |
| Tyrosh | 2068 / 1452 | 2.49 / 7.0 | 30/50/20/0 | 108/99/106/- | 100/97/97/- | 358/230/372/- | 5.89 / 8.81 | 18.4 | 70 / 74% | 10 / 0 |
| Volantis | 2678 / 2187 | 2.52 / 5.6 | 24/52/24/0 | 99/101/94/- | 104/102/99/- | 342/266/400/- | 6.05 / 9.02 | 18.2 | 15 / 73% | 3 / 2 |
| Norvos | 1858 / 1242 | 2.46 / 14.5 | 32/27/41/0 | 82/93/106/- | 96/94/104/- | 307/219/385/- | 5.26 / 9.27 | 17.1 | 49 / 63% | 3 / 5 |
| Qohor | 2114 / 1342 | 2.63 / 12.0 | 31/42/27/0 | 104/108/111/- | 92/105/101/- | 282/264/337/- | 5.7 / 8.43 | 18.5 | 49 / 37% | 0 / 4 |
| Lorath | 2743 / 1927 | 2.57 / 5.1 | 37/40/23/0 | 108/111/107/- | 100/107/101/- | 355/316/355/- | 6.72 / 10.23 | 17.4 | 34 / 76% | 6 / 2 |
| Targaryen - Daenerys | 3244 / 2436 | 3.6 / 30.2 | 53/18/17/12 | 100/95/92/96 | 97/96/95/96 | 355/216/307/227 | 9.24 / 12.77 | 15.8 | 44 / 32% | 4 / 1 |
| Targaryen - Aegon | 2233 / 2162 | 2.62 / 9.8 | 22/46/32/0 | 115/103/105/- | 103/98/102/- | 321/230/386/- | 6.67 / 10.25 | 16.4 | 15 / 27% | 0 / 3 |
| Qarth | 1933 / 1304 | 2.4 / 10.1 | 43/24/33/0 | 116/101/112/- | 104/102/99/- | 342/335/321/- | 4.92 / 8.62 | 18.5 | 23 / 70% | 7 / 9 |
| Dothrakowie | 2112 / 2262 | 2.41 / 18.5 | 24/48/7/21 | 75/95/73/95 | 89/96/87/95 | 267/200/160/222 | 5.3 / 7.93 | 19.1 | 21 / 67% | 11 / 19 |
| Sarnor | 2917 / 2093 | 2.41 / 13.7 | 29/33/38/0 | 106/102/102/- | 96/103/97/- | 265/248/306/- | 5.04 / 8.92 | 17.4 | 15 / 40% | 0 / 4 |
| Ibben | 2274 / 1550 | 2.48 / 5.4 | 34/40/26/0 | 101/103/93/- | 100/100/92/- | 340/274/320/- | 6.6 / 10.15 | 17.4 | 15 / 60% | 5 / 0 |
| Yi Ti | 98 / 503 | 2.33 / 6.7 | 24/48/28/0 | 99/99/101/- | 89/100/94/- | 248/253/335/- | 6.19 / 9.38 | 17.5 | brak | 0 / 0 |
| Wyspy Letnie | 128 / 605 | 2.65 / 7.9 | 24/52/24/0 | 95/102/98/- | 97/102/99/- | 263/244/378/- | 6.02 / 9.07 | 18.1 | brak | 0 / 0 |


### 2.3 Ocena balansu

**Jakosc na tier (dziala tylko w Twoich bitwach) [D]+[K]:** wedlug danych ROT (kolumna XML) wzorce krolestw sa wyrownane - 77 z 84 wartosci
(rodzaj x krolestwo, bez Skagos, Yi Ti i Wysp Letnich) miesci sie w 90-110, skrajne 85-109. W grze, po naszym `SkillSinew`, juz tylko 69 z 84, skrajne 73-125.
Wiekszosc rozjazdow robi wiec nasza zasada "umiejetnosc do wlasnego sprzetu" razem z prawem tieru broni: tarcze rodowe ROT maja w XML trudnosc 50-90,
`WeaponTierLaw` robi z tarczy tieru 6 wymog 175, a tarcza wymaga Jednorecznej - wiec `SkillSinew` podnosi piechocie miecz do 175 (wsrod jednostek t3+ krain z tabeli
nizej tarcza jest zrodlem podbicia u 7 z 12 jednostek Pentos, 9 z 13 Qarth, 6 z 12 Lorath i Tyrosh, 6 z 14 Sarnor). Krainy bez tarcz i zbroi (Dothrakowie, Norvos)
nic nie zyskuja, wiec na tle innych slabna. Przyczyna nalezy do audytu umiejetnosci (13); propozycja 4.8.

| Krolestwo | Rodzaj | Sila w grze | Sila wedlug ROT (XML) | Skad roznica [K]+[D] | Zgodne z lore? [L] |
|---|---|---|---|---|---|
| Pentos | piechota / jazda | 125 / 110 | 108 / 104 | tarcze `pentoshi_shield(2)` t6 -> 1H 175 (Pentoshi Footman t2: 1H 40 -> 175), `pentoshi_armor` -> Atl 175 | nie - traktat zabrania Pentos armii |
| Qarth | piechota / jazda | 116 / 112 | 104 / 99 | tarcze `qarth_shield(2)` t6 -> 1H 175 | nie - miasto zyje z murow i floty |
| Targaryen - Aegon | piechota | 115 | 103 | `golden_company_shield` (XML 90) -> 1H 175 | tak - weterani Zlotej Kompanii |
| Braavos | piechota | 113 | 101 | bron (rapiery t5-t6) i `braavosi_plate` -> 1H i Atl 175 | czesciowo - miasto najmuje |
| Lorath | strzelcy / piechota | 111 / 108 | 107 / 100 | tarcze `lorathi_shield(2)`, bron | nie - "prawie bez wojska" |
| Qohor | jazda / piechota | 111 / 104 | 101 / 92 | bron i pancerz | czesciowo |
| Wolni Ludzie | strzelcy | 110 | 101 | bron | tak - lucznicy z rogu |
| Dothrakowie | piechota / jazda | 75 / 73 | 89 / 87 | brak tarcz i zbroi - nic do podbicia | piechota tak (Dothrakowie nie walcza pieszo), jazda NIE |
| Nocna Straz | piechota / jazda / strzelcy | 74 / 82 / 91 | 86 / 85 / 88 | malo podbic; kusznikom belty `bolt_a` t6 -> Kusza 175 | tak - stary sprzet, zbieranina |
| Myr | piechota | 78 | 87 | malo podbic | tak - Myr najmuje |
| Norvos | piechota | 82 | 96 | brak tarcz - nic do podbicia | nie - kraina dlugich toporow |
| Wolni Ludzie | piechota | 83 | 90 | malo podbic | tak - braz, kamien, skory |
| Dolina | jazda | 91 | 101 | malo podbic (Vale Rider t3 84/100, Vale Lancer t4 88/97) | nie - rycerze Doliny |
| Reach | piechota / jazda | 95 / 94 | 101 / 98 | malo podbic | czesciowo - przecietni zamiast rycerstwa |

Strzelcy: w poprzedniej wersji wzorce Korony/Westerlands (126), Aegona (136) i Nocnej Strazy (119) oraz kultura Myr (115) wychodzily "za mocne" tylko dlatego,
ze wzor dawal +0.2 za naciag, a kusza ma naciag 240-330, luk 70-180 - liczylem typ broni, nie jakosc. Teraz kusznik jest porownywany z kusznikami:
wzorce Korony 95, Aegona 103, Nocnej Strazy 91, Myr 89, kultura Myr 101 (wszyscy przecietni). Osobna sprawa to belty: `bolt_a`/`bolt_b` maja tier 6, wiec `SkillSinew` daje Kusze 175 nawet kusznikom tieru 2-3 (Westerlands Levy Crossbowman t2
Kusza 40 -> 175, Night's Watch Crossbowman t3 i Goldenmark Marksmen t3 70 -> 175) - to tez temat audytu 13 i propozycji 4.10.

Jednostek "zepsutych" (jazda bez konia we wzorcu, pieszy z koniem, strzelec bez broni) z droga do wojska nie ma [D].

**Koszt (dziala wszedzie) [K]+[D]:** zold zalezy tylko od tieru i konia (x1.5), wiec nikt nie jest za tani ani za drogi na tier. Na czlowieka swiezej partii:
od 4.9 zl (Qarth) do 9.2 zl (Daenerys - Nieskalani t6 od startu); rozwinietej: od 7.9 (Dothrakowie) do 12.8 (Daenerys). Moc autobitwy na 100 zl: od 15.8 (Daenerys)
do 19.2 (Braavos) - rozrzut +-10%, caly z tieru i udzialu jazdy. W liczbach kilku krolestw siedza rody najemne [P, doba 120]: Tyrosh - Moon Brothers 336 z 2 068 ludzi (16%),
Targaryen-Aegon - Brotherhood without Banners 263 z 2 233 (12%), Sarnor - Sons of the Harpy 345 z 2 917 (12%), Myr - Bright Banners 386 z 3 739 (10%),
Qohor - Faith Militant 201 z 2 114 (10%), Smocza Skala - Wild Hares 433 z 4 953 (9%), Ibben - Windblown i Company of the Cat 88 z 2 274 (4%); ich udzialu w bitwach
log nie podaje (linia H3 nie ma rodu).

**Sklad [D]+[P]:** prawie wszyscy maja ten sam szablon (16 rekrutow t1, 8 t2, 4 t3, 2 t4, 1 t5 w linii piechoty + galezie strzelcow i jazdy):
swieza partia ok. 60% piechoty i 11-15% konnych, cel ROT w Westeros 30-40% P / 34-46% S / 21-33% J. Cel dziala na cala partie (kazdy czlowiek liczy sie do formacji,
do ktorej dojdzie), ale w polu walcza rekruci pieszo, wiec realny sklad lezy miedzy "swieza partia" a "cel". Inaczej zbudowani sa tylko: Dorne (26% konnych
lucznikow w celu), Dothrakowie, Braavos (52% P, 6% J), Wolni Ludzie (51% P), Daenerys (53% P z Nieskalanymi), Nocna Straz (60% S), Myr (55% S), Volantis (52% S),
Norvos (41% J), Sarnor (38% J).

**Konnych w polu ubywa [P]+[K]:** u wszystkich partii rodow AI 12,5% w 1. dobie, 9,3% w 30., 8,7% w 60., 8,2% w 120. (linia "Zold konnych"), przy czym liczba konnych
rosnie (3 695 -> 8 980), tylko ludzi przybywa szybciej (29,6 tys. -> 109,8 tys.), a nowi to glownie piesi rekruci t1. Skad AI ma jazde:
(1) **konni najemnicy z karczm** - 8 617 w 120 dob, ale coraz mniej (167 dziennie w dobach 1-10, 28 w 91-120), a ROT zamienia ich na jednostki szablonu brakujacej
formacji (czyli zwykle na jazde); (2) **zamiany ROT** - kazdy zolnierz spoza puli tieru 2+ moze zostac jezdzcem szablonu, bez konia; (3) **awanse** - wymagaja konia
z taboru (`Stables`), a lordowie kupili 1 591 koni w 592 zakupach, gdy czekalo na nie lacznie 3 311 ludzi; awans z jezdzca na jezdzca jest darmowy (128 celow);
(4) konni ochotnicy od notabli - tylko 448. Ile awansow odpadlo z braku konia (`FilterTargets`), log nie liczy - **zanim zmienimy zasady koni, trzeba to zmierzyc** (4.4).
Wedlug ksiazek Westeros wystawial 20-35% konnych (Polnoc ok. 1/4, Tywin na Zielonych Widlach ok. 35-40%, Reach 1/4-1/3) [L: AGOT Bran VI, Tyrion VIII;
AWOIAF Military strength]; w grze partie lordow Westeros maja 4-15% konnych (tabela 2.2).

**Wynik w kampanii [P] (decyduje liczba):** w bitwach lorda z lordem najczesciej wygrywaja Lorath (76% z 34), Tyrosh (74% z 70), Volantis (73% z 15), Myr (72% z 32),
Qarth (70% z 23) i Dothrakowie (67% z 21); najrzadziej Braavos (21% z 33), Lys (26% z 70), Targaryen-Aegon (27% z 15), Pentos (28% z 32), Daenerys (32% z 44)
i Dorne (33% z 24). Polnoc (5 bitew), Dolina (6), Dorzecze (5) i Korona (5) prawie nie walczyly z lordami - za mala proba; Zelazne Wyspy - zadnej.
Prawie zawsze wygrywala strona z przewaga liczby x tieru (98%), a w bitwach wyrownanych - 88%. Osobno [P]: najczesciej lupione w polu byly Krainy Burzy
(57 grup wiesniakow i 38 karawan), Wolni Ludzie (20 i 44 - prawie wszystkie przez Nocna Straz, ktora wygrala 64 takie napady) i Dothrakowie (11 i 19);
najwiecej napadow wygrala Smocza Skala (55 grup wiesniakow i 35 karawan). Ile ludzi wystawia kraina, wynika z gospodarki i liczby rodow
(np. Reach 6,9 tys. przy 17 rodach, Polnoc 11,9 tys. przy 26) - to temat audytow 05 i 12, nie danych jednostek.


---

## 3. Zgodnosc z lore - kraina po krainie

Lore [L] za `armie/lore.md` (strony AWOIAF pobrane 09.10 z rozdzialami ksiazek) i AWOIAF "Military strength". Proporcje "z ksiazek" to [ocena] na podstawie
konkretnych armii z ksiazek - ksiazki prawie nigdy nie podaja procentow wprost - i dotycza **calej armii**. W grze porownuje je ze skladem w polu (swieza partia,
pomiar konnych u lordow), a nie z samym celem ROT, ktory liczony jest ze stosow t3+ (1.0). Gra: Sila wzorca P/S/J/KL **w grze / wedlug ROT (XML)**, cel ROT, konni u lordow.

### 3.1 Westeros

| Kraina | Co mowia ksiazki [L] | Co jest w grze [D]/[P] | Zgodnosc | Co nie pasuje |
|---|---|---|---|---|
| **Polnoc** (`battania`) | Liczna, twarda piechota w kolczudze i futrze (topory, wlocznie, wielkie miecze Umberow); rycerzy malo (stara wiara); w Winterfell ok. 12 tys. ludzi, w tym ok. 3 tys. konnych i 300-400 rycerzy (ok. 25%); do Blizniakow dochodzi ok. 20 tys., AWOIAF szacuje ok. 5 tys. jazdy (tez ok. 25%) [L: AGOT Bran VI; AWOIAF Military strength]; piechota przegrala w polu z armia Tywina [L: AGOT Tyrion VIII]; Martin: wzor to Szkocja [L: wywiad 2016 za AWOIAF] | Najwiecej ludzi na swiecie (11 868 + 8 880 w zalogach), 117 jednostek, 9 szablonow rodow (z Umberami, Mormontami i Cerwynami); Sila 98 / 102 / 96 (ROT 99 / 101 / 96); pancerz piechoty na kazdym tierze rowny sredniej swiata (t6 449 przy sredniej 482); cel 35 / 43 / 21 (szablony rodow 28-34 / 41-48 / 21-28), swieza partia 65 / 24 / 12; konni u lordow 4,5% (6,7% w dobach 1-30, 4,1% w 91-120) | **"Swietna piechota": NIE w jakosci (98 na 100), TAK w liczbie.** Pancerz zgodny (kolczuga, nie plyty). W polu Polnoc walczy dzis glownie pieszo | Jazdy w polu 4,5% zamiast ok. 25%; cel ROT ciagnie dojrzale partie do przewagi lucznikow (43%, w szablonach rodow do 48%) |
| **Dolina** (`vale`) | Rycerze Doliny uwazaja sie za najlepszych w Siedmiu Krolestwach [L: AGOT Tyrion V]; bogata, nietknieta wojna; gorskie klany [L: AGOT Tyrion V-VIII] | Sila 95 / 93 / 91 (ROT 101 / 94 / 101); cel 32 / 35 / 33 (najwiecej jazdy w Westeros); konni u lordow 12,6% (drugie miejsce w Westeros po Dorne); sa Arryn Winged Knight t6 (pancerz 658 - najciezszy jezdziec swiata, Sila 114) i Knight of the Vale t6, ale przepis wypelniaja Vale Rider t3 i Vale Lancer t4 z Jazda 70-100 i pancerzem 274-320 (Sila w grze 84 / 88, wedlug ROT 100 / 97) | **Czesciowo** | Jazda t3-t4 przecietna i lekka - rycerstwo z ksiazek widac dopiero na tierze 5-6; w grze slabsza niz wedlug ROT, bo innym nasza zasada podbija wiecej |
| **Dorzecze** (`river`) | 3 piechurow na 1 jezdzca [L-pol]; lucznicy Blackwoodow [L: Fire & Blood]; Freyowie: tysiac rycerzy i konni lucznicy [L: ASOS Catelyn II; ADWD Reek II]; kraina, przez ktora idzie kazda wojna | Sila 101 / 96 / 97 (ROT 99 / 98 / 93); cel 35 / 40 / 25; konni u lordow 4,9%; tier lordow w bitwach 2,33 - najnizszy w Westeros (blisko Qarth 2,40 i Dothrakow 2,41); Ravens' Teeth t6 luk 260, naciag 210 | **Tak** | Drobne: brak konnych lucznikow Freyow |
| **Zelazne Wyspy** (`sturgia`) | Wikingowie: drakkary, topory, taniec palcow; chuda ziemia nie wykarmi koni, szlachta jezdzi na kucykach z Harlaw [L: TWOIAF; ACOK Theon II]; na ladzie przegrywaja [L: ADWD Reek II, The King's Prize] | Sila 102 / 95 / 94 (ROT 105 / 97 / 99); cel 38 / 41 / 21 - Ironborn Scout t4 i Ironborn Horseman t5 sa w drzewie WSI i w szablonie kultury, Greyjoy i Harlaw maja Rider/Horseman; swieza partia 66 / 22 / 11; konni u lordow 4,2%; zadnej bitwy lorda z lordem w 120 dobach | **Piechota tak, sklad nie** | 21% jazdy w celu (z ksiazek ok. 3%) i 41% strzelcow (ok. 17%) |
| **Westerlands + Korona** (`vlandia`) | Najbogatsi (kopalnie zlota), najlepiej uzbrojeni; Tywin: lucznicy w trzech liniach, pikinierzy, 4 tys. ciezkich kopijnikow [L: AGOT Tyrion VIII]; 3 tys. kusznikow w 261 r. [L: TWOIAF]; wzor: Lancasterowie, armia z kontraktow [H: A. Curry 2011] | Sila 103 / 95 / 99 (ROT 102 / 94 / 100); pancerz piechoty t5+ 520 - najwyzszy w Westeros; Guardian of the Rock t6 pancerz 614 (najciezsza piechota swiata); kusze 50% strzelcow, naciag 330 (kusznicy przecietni wsrod kusznikow); cel 36 / 34 / 31; konni u lordow 8,8% | **Tak** | Konnych w polu 9% zamiast ok. 30% |
| **Ziemie Korony** (`crownlands`) | Zlote plaszcze: kolczuga, wlocznie, palki - "nie prawdziwi zolnierze" [L: AGOT Eddard XIII; ACOK] | Sila kultury 92 / 104 / 97 (ROT 98 / 106 / 100); Gold Cloak Halberdier t5; 1 rod (szablon w krolestwie Joffreya) | **Tak** | - |
| **Reach** (`reach`) | Najwieksza armia (80-100 tys. [L-pol]), kolebka rycerstwa; Renly: 20 tys. jazdy, co najmniej 10 tys. ciezkich kopijnikow [L: ACOK Catelyn III/IV; AWOIAF Military strength] | Sila 95 / 100 / 94 (ROT 101 / 99 / 98) - przecietni; ludzi 6 929 - piate w Westeros; cel 33 / 36 / 32; konni u lordow 7,9%; najlepsi sa kusznicy Hightower (naciag 330); we wzorcu Tarly Knight, Tyrell Horseman i Hightower Cavalry (t5) maja konia RBM Hunter (szarza 7, ale +150 zycia) [D-wzorzec] | **Czesciowo** | Rycerstwo z ksiazek jest w grze przecietne; mniej ludzi niz Polnoc, Korona, Dolina i Dorzecze; jazdy w polu 8% zamiast 25-33% |
| **Krainy Burzy** (`stormlands`) | Twardzi lordowie, lucznicy z Marchii; ok. 30 tys.; Dondarrion: 4 tys. piechoty i 800 jazdy (17%) [L: TWOIAF; The Hedge Knight] | Sila 100 / 98 / 95 (ROT 102 / 99 / 96); cel 40 / 35 / 25; konni u lordow 8,1%; 120 bitew lorda z lordem, wygrane 45%; najczesciej lupiona kraina (57 grup wiesniakow i 38 karawan) | **Tak** (przecietni we wszystkim) | - |
| **Smocza Skala** (`dragonstone`) | Najmniej wojska; Stannis 5 tys., mniej niz 400 konnych (ok. 8%) [L: ACOK Catelyn III]; kusznicy w garnizonie [L: Fire & Blood] | Sila 109 / 91 / 98 (ROT 103 / 91 / 98); cel 29 / 46 / 25; konni u lordow 11,5%; 4 953 ludzi (w tym Wild Hares 433); 107 bitew lorda z lordem, wygrane 57%; najwiecej wygranych napadow (90) | **Tak** w sumie | Drobne: 25% jazdy w celu |
| **Dorne** (`aserai`) | Lekkie wojsko: wlocznie, okragle tarcze, oszczepy, luki dwukrzywe i cisowe; zbroje lekkie, emaliowane; konie piaskowe szybkie, ale nie uniosa ciezkiej zbroi; ciezcy rycerze tylko u gorskich rodow Dayne i Yronwood [L: ASOS Tyrion V; TWOIAF]; wzor: Hiszpania, Walia, Palestyna [L: SSM 2000] | Sila 100 / 108 / 97 / 106 (ROT 109 / 109 / 101 / 105); cel 21 / 39 / 13 / 26; konni u lordow 15,4% - najwiecej w Westeros; linie wlocznikow i oszczepow; 3 typy konnych lucznikow; ciezkie zbroje konskie (82) tylko u Dayne Knight, Knights of Starfall, Yronwood Knight i Boneway Guardian, zwykli jezdzcy 8-22 | **Tak** | Drobne: piechota t3-t4 ciezsza niz srednia swiata (298 / 404 wobec 270 / 346); wedlug ROT najmocniejsza piechota Westeros (109) |
| **Nocna Straz** (`nightswatch`) | Mniej niz 1 tys. ludzi; zwiadowcy; obrona Muru lukiem, olejem i kamieniem [L: AGOT Tyrion III; ASOS Jon VII, IX] | Sila 74 / 91 / 82 (ROT 86 / 88 / 85); cel 23 / 60 / 17 (kusznicy t3-t5); konni u lordow 3,6%; 2 100 + 1 435 ludzi; 100 bitew lorda z lordem, wygrane 52%, do tego 64 wygrane napady na wiesniakow i karawany Wolnych Ludzi | **Tak** w duchu | Liczebnosc (3,5 tys. zamiast mniej niz 1 tys.) - sprawa gry, nie jednostek. Zold braci ma byc 0 (Twoja decyzja, STAN-PRAC; w kodzie jeszcze nie ma) |
| **Wolni Ludzie** (`freefolk`) | Ok. 16 tys. wojownikow; kamien, drewno, braz, luki z rogu; giganci i ponad 100 mamutow; Thennowie w brazie; rozbici jedna szarza [L: ASOS Jon VIII, X] | Sila 83 / 110 / 91 (ROT 90 / 101 / 85); cel 51 / 29 / 20; giganci (wrecz 300), Mammoth Riding Giant (mamut, szarza 400); pancerz piechoty t3 208 (swiat 270); 100 bitew lorda z lordem, wygrane 48% | **Tak** | Drobne: Free Folk Horseman t5 daje ok. 20% jazdy w celu (z ksiazek prawie zero) |
| **Skagos** (`skagosi`) | Ludzie kamienia, obsydian, plotki o jednorozcach [L: AWOIAF Skagos za TWOIAF] | Jednorozce (Unicorn 55/80); Sila 113 / 86 / 116 (ROT 103 / 86 / 103); 1 rod, 76 ludzi | **Tak** | - |
| **Inni** (`whitewalker`) | Masa umarlych, slabych pojedynczo | Sila kultury 69 / 75 / 87 (ROT 77 / 78 / 80) | **Tak** | - |

### 3.2 Essos

| Kraina | Co mowia ksiazki [L] | Co jest w grze [D]/[P] | Zgodnosc | Co nie pasuje |
|---|---|---|---|---|
| **Braavos** (`empire`) | Potega morska, Zelazny Bank; na ladzie najmuje; bravo to pojedynkowicze, nie zolnierze [L: TWOIAF; AWOIAF Bravo] | Sila 113 / 91 / 104 / 96 (ROT 101 / 96 / 94 / 96); cel 52 / 36 / 6 / 6; Water Dancer t6 (pancerz 466); 33 bitwy lorda z lordem, wygrane 21% | **Tak** (malo jazdy, piechota miejska) | Drobne: tancerz wody jako ciezka piechota liniowa; sila piechoty w grze z naszej zasady (101 wedlug ROT) |
| **Pentos** (`pentoshi`) | Traktat z Braavos: bez armii i bez najemnikow, najwyzej 20 okretow; placi Dothrakom [L: TWOIAF Pentos] | Pelne drzewo; Sila 125 / 104 / 110 / 102 - najmocniejsza piechota na tier w grze, ale wedlug ROT 108 (tez wysoko); Magister Guard Elite to jazda t6 (pancerz 584); 32 bitwy lorda z lordem, wygrane 28% (malo ludzi) | **Nie** (jakosc i sam fakt armii) | Miasto bez armii ma najlepsza piechote swiata - w 2/3 przez nasza zasade (tarcze t6), w 1/3 przez ROT |
| **Lys, Tyrosh** (`lyseni`, `tyroshi`) | Najmuja, floty, handel niewolnikami [L: TWOIAF] | Lys 104 / 98 / 106 (ROT 96 / 99 / 98), Tyrosh 108 / 99 / 106 (ROT 100 / 97 / 97); duzo "Axeman"; Tyrosh wygrywa 74% z 70 bitew lordow (Moon Brothers to 16% jego ludzi) | Neutralne | Drobne: topory to w ksiazkach Norvos |
| **Myr** (`myrish`) | Kusze (myrijska kusza na trzy belty), sztylety, najemnicy [L: ACOK Tyrion VI; TWOIAF]; wzor: kusznicy genuenscy [H: Wikipedia "Genoese crossbowmen"] | Sila 78 / 89 / 96 (ROT 87 / 90 / 101); kusze 44% strzelcow (kusznicy przecietni wsrod kusznikow); Myrish Artisan of War t6 kusza 240, naciag 330; cel 21 / 55 / 24; Bright Banners to 10% ludzi; 32 bitwy lordow, wygrane 72% | **Tak** (sklad i kusze; jakosc przecietna) | - |
| **Volantis** (`volantine`) | Najwieksza potega ladowa, wojsko z niewolnikow: tygrysie plaszcze w srebrnych kolczugach, z wloczniami [L: ADWD Tyrion VI]; Malaquo: 5 tys. piechoty i 1 tys. jazdy [L: ADWD The Lost Lord]; slonie to dzis stronnictwo kupcow i zwierzeta ulic, ale w wojnie z Garinem (ok. 700 lat wczesniej) pod Volon Therys stalo "sto tysiecy wrogow, sto sloni i trzech smoczych lordow" [L: TWOIAF, AWOIAF Volantis] | Sila 99 / 101 / 94 (ROT 104 / 102 / 99); cel 24 / 52 / 24; w szablonie kultury Volantis 3 slonie bojowe na partie: `tigercloak_camel_cavalry` to **Volantene Mahout na sloniu** (`Item.elephant`, ROT-Troops.xml:16132-16161), a nie wielblad; jednostki `golden_*` (tez z kultura `volantine`) to armia Aegona; 15 bitew lordow, wygrane 73% | **Czesciowo** | Strzelcy przewazaja (52%; z ksiazek ok. 15%), tygrysie plaszcze w mniejszosci; slonie bojowe u kazdego lorda - ksiazki znaja je tylko z dawnej wojny (pytanie 5.7) |
| **Norvos** (`norvos`) | Brodaci kaplani ze straza niewolnikow z dlugimi toporami (Areo Hotah) [L: TWOIAF Norvos; AFFC The Captain of Guards] | Sila 82 / 93 / 106 (ROT 96 / 94 / 104); cel 32 / 27 / 41 - najwiecej jazdy w Essos (Sarnor 38%); konni u lordow 14,5%; konni kaplani (Mounted Priest t6) | **Nie** (sklad) | Kraina toporow ma slaba w grze piechote (bez tarcz nasza zasada jej nie podbija) i 41% jazdy |
| **Qohor** (`qohorik`) | Najlepsi platnerze swiata; miasta bronia Nieskalani; ciezka jazda Qohoru rozbita przez Dothrakow [L: TWOIAF Qohor] | Sila 104 / 108 / 111 (ROT 92 / 105 / 101); pancerz piechoty we wzorcu 282 - drugi najnizszy wsrod Wolnych Miast (po Myr 255; Pentos 387); Faith Militant to 10% ludzi | **Czesciowo** | Miasto platnerzy z lekko opancerzona piechota |
| **Lorath** (`nord`) | Najmniej ludne z Wolnych Miast, prawie bez wojska [L: TWOIAF Lorath] | Sila 108 / 111 / 107 (ROT 100 / 107 / 101); 2 743 ludzi (wiecej niz Pentos, Braavos i Qohor); 34 bitwy lordow, wygrane 76% - najlepszy wynik swiata | **Nie** | Za liczni; za mocni w grze (wedlug ROT przecietni poza lucznikami) |
| **Ghis / Daenerys** (`ghiscari` + szablon rodu Targaryen) | Nieskalani: lekka piechota z wloczniami, najlepsza dyscyplina; 8 600 ludzi; Dothrakowie i najemnicy [L: ASOS Daenerys II-V] | Szablon rodu: 25 Unsullied t6 + Dothrakowie; tier lordow w bitwach 3,60 (najwyzszy), konni 30,2%; Unsullied t6: wrecz 270, Atl 270, pancerz 370 (lekko, jak w ksiazkach), Sila 101; 44 bitwy lordow, wygrane 32% (przewaga liczby u wrogow) | **Tak** | - |
| **Aegon / Zlota Kompania** (`valyrian` + `golden_*`) | 10 tys. weteranow: 500 rycerzy, 1000 strzelcow (1/3 kusze, 1/3 luki dwukrzywe, reszta cisowe), 24 slonie [L: ADWD The Lost Lord, The Griffin Reborn] | Sila 115 / 103 / 105 (ROT 103 / 98 / 102); slonie (Elephant Rider t5 i Mahout t6, 3 na partie); strzelcy to same kusze; 15 bitew lordow, wygrane 27% | **Tak** (weterani) | Drobne: same kusze zamiast 1/3; przewaga piechoty w grze z tarcz `golden_company_shield` (nasza zasada) |
| **Qarth** (`qartheen`) | Straz Obywatelska, flota; jezdzcy na wielbladach w miedzianej lusce; miasto zyje z potrojnych murow [L: ACOK Daenerys II, III] | Sila 116 / 101 / 112 (ROT 104 / 102 / 99); wielblady (Cameleer); 23 bitwy lordow, wygrane 70% | **Klimat tak, sila nie** | Za mocni w grze - glownie przez tarcze t6 (nasza zasada) |
| **Dothrakowie** (`khuzait`) | Wszyscy konni; arakh, luk, bicz; bez zbroi (zbroja to tchorzostwo); jezdza lepiej niz rycerze; luki dalsze niz westeroskie; nie biora murow; przegrywaja z murem wloczni [L: AGOT Daenerys IV; Concordance AGOT I:556; ASOS Daenerys I]; wzor: Mongolowie, Hunowie, Alanowie, Indianie Rowin [L: SSM 2012] | Sila 75 / 95 / 73 / 95 (ROT 89 / 96 / 87 / 95) - najslabszy wzorzec swiata; cel 24 / 48 / 7 / 21 - **72% PIESZYCH** (piechota i piesi lucznicy), swieza partia 54 / 25 / 4 / 18; konni u lordow 18,5%; we wzorcach wszyscy na Dothraki Palfrey 54/16 [D-wzorzec]; pancerz lekki (t5 ok. 285, swiat 440) - zgodnie; 21 bitew lordow, wygrane 67%; stracili w polu 11 grup wiesniakow i 19 karawan | **NIE - najwiekszy rozjazd swiata** | Piechota i piesi lucznicy w szablonie; jazda 73 na 100 (wedlug ROT 87) |
| **Sarnor** (`sarnor`) | Rydwany, "Wysocy Ludzie", zniszczeni przez Dothrakow [L: TWOIAF The Grasslands] | Sila 106 / 102 / 102 (ROT 96 / 103 / 97); cel 29 / 33 / 38; Sons of the Harpy to 12% ludzi | **Tak** (jazda w miejsce rydwanow) | - |
| **Ibben** (`ibbenese`) | Wielorybnicy; zloto, zelazo i cyna w gorach [L: TWOIAF Ib] | Sila 101 / 103 / 93 (ROT 100 / 100 / 92) | Neutralne | - |
| **Yi Ti** (`yiti`) | Ogromne cesarstwo; w ROT wygnancy [L: TWOIAF] | Sila 99 / 99 / 101 (ROT 89 / 100 / 94); 2 rody, 98 ludzi | Neutralne | - |
| **Wyspy Letnie** (`summer`) | Luki ze zlotego serca (najdalszy zasieg), wlocznie, proce, malo pancerza [L: TWOIAF The Summer Isles]; o jezdzie ksiazki milcza [ocena] | Goldenheart Warrior t6: luk 250, naciag 200 - najwiekszy naciag zwyklego lucznika; pancerz 378; cel 24 / 52 / 24 - Summer Isles Scout t4 i Horseman t5 na Dornish Rounsey | **Lucznicy tak, jazda nie** | 24% jazdy w celu |

**Polnoc - odpowiedz wprost:** w grze Polnoc jest "swietna" liczba (najwiecej ludzi i najwiecej linii jednostek), nie jakoscia. Jej piechota to przecietna
piechota w srednich pancerzach (98 na 100 w grze, 99 wedlug ROT). W polu Polnoc walczy dzis glownie pieszo (swieza partia 65 / 24 / 12, jazdy u lordow 4,5%),
ale cel ROT ciagnie dojrzale partie do przewagi lucznikow (cel 35 / 43 / 21; w szablonach Starkow, Boltonow, Karstarkow, Gloverow, Umberow i Cerwynow 34 / 45 / 21,
u Mormontow 28 / 48 / 21 / 3, u Manderlych 31 / 41 / 28). Ksiazki daja Polnocy twarda, liczna piechote (nie najlepiej opancerzona), ok. 1/4 jazdy i garstke rycerzy - wiec zgodnosc jest
w pancerzu i liczbie, a brakuje ok. 20 punktow jazdy w polu i przewagi piechoty w dojrzalych partiach.


---

## 4. Propozycje zmian

**Jak wdrazac [ocena]+[K]:** kazda zmiana danych ROT musi zyc w naszym kodzie, bo aktualizacja ROT nadpisze jego pliki. Zalecam **latke przy wczytaniu
w CrashScribe (`Mends`)**, tak jak dzis `SkillSinew`: zmiana stosow `PartyTemplateObject.Stacks`, `upgrade_targets` jednostek i wzorcow ekwipunku po zaladowaniu
obiektow. Plik XSLT/XML w `ModuleData` tez by zadzialal, ale pliku danych w folderze gry nie wgram sam ani na czas autotestu (zabezpieczenia blokuja
kopiowanie plikow danych do gry) - wymagalby Twojego recznego wgrania. Zasady (konie, autobitwa) - latka kodu Armoury.
**Sprawdzenie kazdej zmiany:** autotest 40 dob i porownanie z dzisiejszym: bitwy lorda z lordem (wygrane, straty H3), oblezenia wygrane i przegrane (dzis H3 loguje
tylko bitwy polowe - trzeba dopisac linie oblezen), udzial konnych u lordow, zold partii wobec dochodu rodow (`budzet-rodow.csv`, audyt 12), bankructwa;
dla jakosci - jedna bitwa reczna.

**Trzy rzeczy, ktore dotycza kazdej zmiany skladu [K]:**
1. **Zalogi tez.** ROT co dobe ustawia zalogi twierdz AI pod ten sam szablon rodu (`DailyTickSettlement`), wiec mniej strzelcow w szablonie = mniej strzelcow na murach.
   W autobitwie oblezniczej lucznik obroncy ma +30% mocy, piechota 0, jazda -10% (`_battleModifiers`).
2. **Kon musi skads byc.** Wiecej jazdy w szablonie to glownie wiecej zamian ROT na jezdzcow - a zamiana daje czlowieka bez konia. Taki "konny na papierze"
   liczy sie jako konny w autobitwie, w H3 i w zoldzie (x1.5, `MountedWage` patrzy na `IsMounted` wzorca), ale w Twojej bitwie walczy pieszo, bo DTE sadza
   na koniu tylko konmi ze zbrojowni, a awaryjny ekwipunek DTE uzupelnia zbroje i bron, nie konie (specyfikacja 171, rozdz. 1.4 i B6; AiGear koni nie kupuje).
   Kon, ktory lord AI oddaje za awans, dzis znika zamiast trafic do zbrojowni (4.4). Przy kazdej propozycji podaje wiec, skad bierze sie kon.
3. **Dobytek i zakupy.** Zmiana wzorcow i drzew zmienia "dobytek" rekruta z paczki 171 (czesc wspolna wzorca przodka t1 i wzorca jednostki, `OwnOf`),
   zakupy `VolunteerKit` i dobor sprzetu DTE ("najpierw sztuka z wzorca") - kazda zmiane wzorca trzeba zgrac z 171.

### P1 - najwazniejsze (sklad zgodny z ksiazkami; dziala tez w autobitwach)

**4.1 Dothrakowie konni** (praca srednia; przed wgraniem autotest 40 dob; pytanie 5.3)
- Szablon `kingdom_hero_party_khuzait_template` (15 rodow + 3 rody Daenerys w szablonie Targaryen bez zmian): **zostaje** stos `khuzait_nomad` t1 (16) - pieszy rekrut z wioski,
  zeby ROT nie zamienial kazdego rekruta na konnego t2 (bez stosu t1 `DetermineReplacement` szuka tieru wyzej, a lord placi roznice ceny; moc w autobitwie
  skacze z 0.66 do 0.96, czyli o 45%). **Usunac** stosy pieszych `khuzait_footman` t2 (8), `khuzait_spearman` t3 (4), `khuzait_spear_infantry` t4 (2),
  `khuzait_darkhan` t5 (1), `khuzait_hunter` t3 (8), `khuzait_archer` t4 (5), `khuzait_marksman` t5 (1).
  **Ustawic** stosy: `khuzait_tribal_warrior` t2 KL z 4 na 8, `khuzait_raider` t3 KL z 3 na 6, `khuzait_horseman` t3 J z 2 na 3, `khuzait_horse_archer` t4 KL z 2 na 4,
  `khuzait_khans_guard` t6 KL zostaje 1; **dodac nowe** stosy `khuzait_lancer` t4 J (2), `khuzait_heavy_horse_archer` t5 KL (2), `khuzait_heavy_lancer` t5 J (1)
  (dzis sa w puli tylko jako awanse). Cel z 24 / 48 / 7 / 21 na 0 / 0 / 32 / 68; swieza partia z 54 / 25 / 4 / 18 na 37 / 0 / 14 / 49 (piesi to tylko rekruci t1) [D+S].
- Drzewo wsi: `khuzait_nomad` awansuje dzis do `khuzait_tribal_warrior` (KL) albo `khuzait_footman` (P) - zostawic tylko konna droge. Piesi Dothrakowie zostaja w milicji.
- **Skad kon:** awans rekruta na tribal_warrior wymaga konia z taboru (`Stables`, jak wszedzie) - lord kupuje go na targu i we wsiach za swoje zloto (`AiBuy`).
  Bez konia rekrut czeka. Zamiany ROT (najemnicy, jency) dadza jezdzcow bez konia - jak u wszystkich (wyzej, pkt 2).
- **Koszt [K+D]:** zold na czlowieka swiezej partii z 5,30 na 6,70 zl (+26%), rozwinietej z 7,93 na 11,79 (+49%), bo konni placa x1.5. Dzis partie Dothrakow
  kosztuja 13,7 tys. zl dziennie, a glowy ich 16 rodow maja razem 0,62 mln zl i jeden rod jest bankrutem [P, doba 120] - bez zmiany dochodow to przepis na bankructwa.
- **Skutek w autobitwie [K]:** konni lucznicy +30% w ataku na otwartym terenie (rownina, step i pustynia to 77% bitew polowych), -30% w lesie, -20% przy oblezeniu; w H3 zabija
  wiecej uciekajacych: przy 60-70% konnych wobec typowych 10% przeciwnika C = ok. 0.5-0.6, czyli ok. +18-21 punktow procentowych smierci przegranych
  (dzis srednio C = 0.04), a sami latwiej uciekaja po przegranej. Dzis Dothrakowie wygrywaja 67% z 21 bitew lordow. Po zmianie moga zalewac Wolne Miasta - stad
  autotest i pytanie 5.3 o hamulec na mury (ksiazki: "nie biora murow" [L: AGOT Daenerys IV; armie/lore.md]).
- Konie we wzorcach (`khuzait_khans_guard` -> Khal's Steed 70/30, t5 -> Grass Sea Steed 60/20, t3-t4 -> Dothraki Courser 59/19) zmieniaja tylko wzorzec [D-wzorzec];
  w bitwie liczy sie, jakie konie lordowie kupia (4.4). Umiejetnosc: linia konna t2-t5 +30 Jazdy ponad dzisiejsze wartosci w grze (wartosc ponad trudnosc sprzetu
  `SkillSinew` zostawia) - jazda z arakhami z 73 do ok. 80-85 [S]; reszte roznicy robi celowo lekki pancerz (zgodnie z lore). To dziala tylko w Twoich bitwach.

**4.2 Zelazne Wyspy bez jazdy** (praca mala; pytanie 5.6)
- Szablon `kingdom_hero_party_sturgia_template` (27 rodow): usunac `sturgian_hardened_brigand` t4 J (4) i `sturgian_horse_raider` t5 J (2); dodac `sturgian_berzerker` t4 (3),
  `sturgian_shock_troop` t5 (2) i `sturgian_ulfhednar` t5 (1); `sturgian_hunter` z 8 na 6. Cel z 41 / 38 / 21 na 67 / 33 / 0, swieza partia z 68 / 21 / 11 na 82 / 18 / 0 [D+S]
  - blisko ksiazkowej calej armii ok. 80 / 17 / 3 [L]+[H: wikingowie]. Strzelcow w celu zostaje 33%, wiec obrona murow slabnie niewiele.
- Drzewo wsi: `sturgian_brigand` (t3) awansuje dzis do Ironborn Scout (J) - przestawic na `sturgian_berzerker`/`sturgian_spearman`.
- Szablon Greyjoy: `greyjoy_rider`, `greyjoy_horseman` -> `greyjoy_fingerdancer` (taniec palcow) i `greyjoy_houseguard`. Harlaw: zostawic `harlaw_rider`
  jako jedyna jazde (kucyki z Harlaw) [L: TWOIAF; ACOK Theon II].
- Koszt: zold rozwinietej partii z 8,03 na 7,37 zl (-8%) - mniej premii konnej. W autobitwie prawie bez zmiany (konnych u lordow i tak 4%); zmienia sie wyglad i Twoje bitwy.

**4.3 Polnoc: wiecej piechoty, mniej lucznikow, troche wiecej jazdy** (praca mala; jakosc zalezy od pytania 5.1)
- Szablon `kingdom_hero_party_battania_template` (17 rodow, w tym 5 rodow szlachty BK; 5 178 ludzi): `battanian_trained_warrior` t3 z 4 na 5, `battanian_picked_warrior` t4 z 2 na 3,
  `battanian_skirmisher` t3 z 8 na 6, `battanian_veteran_skirmisher` t4 z 4 na 3, `battanian_horseman` t5 z 2 na 3. Cel z 38 / 41 / 21 na 45 / 31 / 24, swieza partia
  z 66 / 23 / 11 na 70 / 17 / 13 [D+S]. Skad ta miara: ROT ciagnie cala partie do celu wedlug formacji docelowych, ale rekruci walcza pieszo, wiec sklad w polu lezy
  miedzy swieza partia a celem - przy tym celu ok. 55-60 / 20-25 / 15-20 [S], blisko ksiazkowego 55 / 20 / 25 [ocena]. Jazdy w polu i tak bedzie mniej, dopoki
  lordowie nie maja koni (4.4).
- To samo we **wszystkich 8 szablonach rodow** (Stark, Bolton, Karstark, Glover, Manderly, Umber, Mormont, Cerwyn - razem 6 690 ludzi; dzis cel 28-34 / 41-48 / 21-28):
  stos lucznikow t3-t4 o ok. 1/4 w dol, piechota t3-t4 o tyle samo w gore, jazda t5 +1. Mormontowie moga zostac lucznikami ponad srednia (Niedzwiedzia Wyspa,
  lowczynie) [ocena].
- Koszt: zold swiezej partii +4% (5,42 -> 5,66), rozwinietej +6% (7,97 -> 8,41). Obrona murow: strzelcow w celu z 41-48% na ok. 31-35%, wiec zalogi Polnocy beda
  mialy mniej lucznikow (+30% w autobitwie oblezniczej) - dlatego cel nie schodzi do 21%, jak w pierwszej wersji.
- Jakosc, jesli tak zdecydujesz (pytanie 5.1, wariant a): piechota t3-t6 linii wsi, szlachty i rodow +25 do glownej broni i +25 Atletyki **ponad dzisiejsze wartosci
  w grze** (wartosci ponad trudnosc sprzetu `SkillSinew` zostawia; wartosci ponizej przywroci), pancerz BEZ zmian (ksiazki: kolczuga i futro). Sila piechoty z 98
  do ok. 105-108 [S]. W autobitwie to nie dziala (tylko tier) - stad wariant c w pytaniu.
- Uzasadnienie: [L: AGOT Bran VI - w Winterfell ok. 12 tys., w tym ok. 3 tys. konnych i 300-400 rycerzy (ok. 25%); AWOIAF Military strength - ok. 5 tys. jazdy
  na ok. 20 tys.; Martin - Szkocja]; [H: Bannockburn 1314 - szkocka piechota w schiltronach, co najwyzej 500 lekkich konnych na ok. 7 tys. (Wikipedia "Battle of Bannockburn")].

**4.4 Konie dla jezdzcow AI - najpierw pomiar, potem zasada** (praca mala; pomiar to tylko log)
- **Pomiar (przed kazda zmiana skladu z jazda):** (a) ile awansow AI na jezdzca `FilterTargets` odrzucil albo przycial z braku konia - dziennie i wedlug krolestwa;
  (b) naplyw konnych wedlug zrodla: start, najemnicy z karczm, zamiany ROT, awanse, ochotnicy; (c) licznik "konni bez konia" z 171. Dzis wiem tylko, ze najemnicy
  daja 8 617 konnych w 120 dob, a lordowie kupili 1 591 koni na awanse [P] - nie wiem, ile awansow czekalo bez skutku.
- **Blad do naprawy w 167 (konie):** kon, ktory lord AI oddaje za awans (`Stables.PayInHorses` -> `Consume`), znika z taboru i nie trafia do zbrojowni DTE nowego jezdzca;
  u Ciebie ten sam kon idzie do zbrojowni (`BankPaidHorses`). Skutek: AI placi za konia, a jej nowy jezdziec w bitwie z Toba moze stac bez konia. Jedna zasada dla
  Ciebie i AI: kon za awans laduje w zbrojowni partii.
- **Wycofane:** pierwsza wersja proponowala "rycerz z wlasnym koniem" (awans szlachty na konnego bez konia z taboru) i "Dothrakowie bez wymogu konia". To byl kon
  z niczego - sprzeczne z Twoja decyzja z 30.08 ("jak awansuje na konnice, to musi byc kon ... i AI tez, warunki globalne"), z decyzja z 08.10 ("wszystko z czegos
  wynika") i z paczka 160 (kon jest wlasnoscia zolnierza). Jesli chcesz, zeby rycerz przychodzil z koniem, to tylko z koniem kupionym albo wzietym ze stada wsi
  wlasnego rodu za zloto rodu - to pytanie 5.2.

### P2 - wazne dla lore, mniejszy zasieg

**4.5 Reach i Dolina - lepsza jazda t3-t5** (praca mala)
- Konie we wzorcach XML nie zmieniaja koni w bitwie (DTE rozdaje konie ze zbrojowni, najlepsze dla najwyzszych tierow, a lord AI za awans oddaje najtanszego konia
  z kategorii). Jesli rycerze maja jezdzic na rumakach, trzeba zmienic to, co trafia do zbrojowni: jakie konie lordowie kupuja (`AiBuy` bierze dzis najtansze)
  i czy kon za awans laduje w zbrojowni (4.4) - to robota dla 167. Zmiana koni we wzorcach (`hunter` -> Western Courser, Essosi Charger -> Andalos Destrier)
  zmienia tylko wyglad wzorca [D-wzorzec].
- Umiejetnosci (dziala w Twoich bitwach): Dolina - `vale_rider` t3 i `vale_lancer` t4 +20 Jazdy i +20 broni ponad dzisiejsze wartosci w grze, korpus o stopien ciezszy
  (dzis 274-320); Reach - `reach_rider` t4 +20 Jazdy i +20 broni. Sila jazdy Doliny z 91 do ok. 98-100, Reach z 94 do ok. 98 [S]. Uzasadnienie: [L: AGOT Tyrion V;
  ACOK Catelyn III/IV]. Wedlug samego ROT obie krainy sa przecietne (101 i 98) - odstaja w grze, bo innym nasza zasada podbija wiecej (4.8 czesciowo to wyrowna).

**4.6 Volantis i Norvos - sklad wedlug ksiazek** (praca mala; slonie - pytanie 5.7)
- Volantis `kingdom_hero_party_volantine_template`: `volantine_bowman` t3 z 8 na 4, `tigercloak_archer` t4 z 4 na 2, `tigercloak_master_archer` t5 z 2 na 1;
  `volantine_soldier` t3 z 4 na 6, `tigercloak_warrior` t4 z 2 na 4, `tigercloak_elite_warrior` t5 z 1 na 2. Cel z 23 / 53 / 23 na 43 / 32 / 25, swieza partia z 57 / 30 / 13
  na 69 / 17 / 13 [D+S] [L: ADWD Tyrion VI, The Lost Lord]. Slonie: `tigercloak_camel_cavalry` (Volantene Mahout, stos do 3 na partie) zostaje albo idzie do Zlotej Kompanii -
  wedlug Twojej decyzji (5.7). Koszt prawie bez zmiany (rozwinieta +3%).
- Norvos `kingdom_hero_party_norvos_template`: `norvos_horseman` t3 z 4 na 2, `norvos_cavalry` t4 z 3 na 1; `norvos_axeman` t4 z 2 na 5, `norvos_master_axeman` t5 z 1 na 2.
  Cel z 32 / 27 / 41 na 50 / 27 / 23, swieza partia z 57 / 26 / 17 na 65 / 26 / 9 [D+S] [L: TWOIAF Norvos; AFFC The Captain of Guards]. Konnych kaplanow zostawic
  jako elite. Koszt: rozwinieta -2%.

**4.7 Wyspy Letnie bez jazdy** (praca mala; pytanie 5.5)
- `kingdom_hero_party_summer_template`: usunac `summer_rider` t4 (4) i `summer_horseman` t5 (3); `summer_infantryman` t3 z 4 na 5, `summer_spearman` t4 z 2 na 5,
  `summer_pikeman` t5 z 1 na 2, `summer_longbowman` t5 z 2 na 3 (pozostali lucznicy bez zmian). Cel z 24 / 52 / 24 na 43 / 57 / 0, swieza partia z 58 / 28 / 13
  na 69 / 31 / 0 [D+S] [L: TWOIAF The Summer Isles]. Zasieg maly (2 rody, 128 ludzi w polu). Koszt: rozwinieta -16% (9,07 -> 7,64).

### P3 - jakosc dzialajaca tylko w Twoich bitwach

**4.8 Tarcze i amunicja w prawie tieru (do audytu umiejetnosci 13)** (praca mala, kod CrashScribe)
- Przyczyna przewagi Pentos, Qarth, Aegona, Tyrosh i Sarnor to glownie nasza zasada, nie ROT (2.3). Obnizanie umiejetnosci w XML nic nie da - `SkillSinew` przy
  wczytaniu podniesie je z powrotem do trudnosci sprzetu (np. `pentoshi_pike_warrior` ma w XML 1H 110, w grze 175 z tarczy).
- **Krok 1 - tarcze poza prawem tieru broni** (`WeaponTierLaw`): tarcza zostaje z trudnoscia z XML (tarcze rodowe 50-90), wiec nie podnosi Jednorecznej do 175.
  Wyliczenie na danych (`armie14r/bez_tarcz.json`, liczone tak, jakby tarcza w ogole nie podnosila 1H - przy trudnosci z XML 50-90 roznica jest mala) [S]:
  piechota Pentos 125 -> ok. 117, Qarth 116 -> ok. 107, Aegon 115 -> ok. 109, Tyrosh 108 -> ok. 104, Sarnor 106 -> ok. 98; Polnoc, Korona, Dothrakowie,
  Nocna Straz bez zmian (+-2). Wariant drugi, jesli prawo tieru ma dalej pilnowac tarcz (zeby tarcza tieru 6 byla tylko dla tieru 6, jak strzaly w Twojej zasadzie
  z 02.09): prawo zostaje, a jednostki t2-t4 dostaja we wzorcach tarcze swojego tieru (4.10). To decyzja dla audytu 13, bo dotyczy wszystkich jednostek z tarczami.
- **Krok 2 - Pentos (jesli zostaje z armia, pytanie 5.4):** reszta przewagi to pancerz `pentoshi_armor` (tier 6, Atletyka 175) juz na tierze 3 - we wzorcach t2-t4
  pancerz o stopien nizszego tieru. Lorath i Qarth podobnie (korpus o stopien lzejszy dla piechoty t4-t5: `qartheen_hoplite`, `qartheen_elite_hoplite`, `nord_huscarl`).
  `magister_guard` to jazda t6 - nie ruszac w tej propozycji.
- **Amunicja:** belty `bolt_a`/`bolt_b` tieru 6 daja Kusze 175 kusznikom t2-t3. Twoja zasada z 02.09 mowi "strzaly tieru 6 tylko dla tieru 6" - zamiast obnizac
  prawo, dac kusznikom t2-t4 we wzorcach belty swojego tieru (4.10).

**4.9 Qohor - miasto platnerzy** - piechota t4-t5 (`qohorik_*`) o stopien ciezszy korpus (dzis pancerz wzorca 282, drugi najnizszy wsrod Wolnych Miast)
[L: TWOIAF Qohor - kuznie bez rownych]. Uwaga: wedlug ROT piechota Qohoru ma 92, w grze 104.

**4.10 Rekruci z bronia, tarcza i beltami mistrza** - 33 z 243 jednostek t1-t2 ma w grze 140-175 w jednej broni: 23 przez bron wysokiego tieru we wzorcu (np.
Northern Noble Youth t2 - 1H 140, prawie wszystkie milicje "Militia Spearman" t2 - Drzewce 140, Golden Company Recruit t2 - 1H 140), 6 przez tarcze (Pentoshi Footman,
City Watch Spearman, Westerlands Militia Spearman, Sarnori Footman, Skagosi Footman - 1H 175; Casterly Rock Guard - 1H 140) i 4 przez belty (Westerlands Levy
Crossbowman, City Watch Crossbowman, Westerlands Militia Archer, Brave Companion Crossbowman - Kusza 175). Na tierze 3 tarcza podbija Jednoreczna o 50+ u 35 jednostek.
Proponuje dac im we wzorcach bron, tarcze i belty tieru 1-3 (zasada `SkillSinew` zostaje, skill wtedy nie urosnie), zamiast ograniczac umiejetnosci.
Zgrac z 171 (dobytek rekruta = czesc wspolna wzorcow) i z audytem 13.

**4.11 Dorne lzejsze** (praca srednia) - linia wlocznikow wsi t3-t4: korpus o stopien lzejszy (dzis 298 / 404 przy sredniej swiata 270 / 346); ciezkie zbroje tylko
u Dayne i Yronwood (gorskie rody - [L: ASOS Tyrion V]). Konie Dorne bez zmian: kon jako przedmiot nie ma pancerza, a ciezkie zbroje konskie (82) maja juz tylko
jednostki Dayne, Yronwood i Boneway Guardian [D].

**4.12 Drobne zgodnosci z lore** - konni lucznicy w szablonie rodu Frey (Dorzecze) [L: ADWD Reek II]; w Zlotej Kompanii 1/3 kusz, 1/3 lukow
dwukrzywych, 1/3 cisowych [L: ADWD The Griffin Reborn]; Wolni Ludzie: `freefolk_horseman` z 4 na 1 (mamuty zostaja); Smocza Skala: jazda w celu
z 25% na ok. 10% [L: ACOK Catelyn III]; Nocna Straz: zold braci 0 wedlug Twojej decyzji (STAN-PRAC, pytanie kanonu 4) - wtedy wiersz Nocnej Strazy w 2.2
(zold 6,13 / 9,17, moc 18,2 na 100 zl) przestanie miec sens.

### Tabela zbiorcza propozycji

| Nr | Co | Priorytet | Praca | Autobitwa (teren, H3, oblezenia) | Twoje bitwy | Zold | Zalezy od |
|---|---|---|---|---|---|---|---|
| 4.1 | Dothrakowie konni (rekrut t1 pieszy) | P1 | srednia | tak: +30% na otwartym, +18-21 pkt smierci wrogow w H3, slabsi pod murami | tak | +26% / +49% | autotest, 5.3 |
| 4.2 | Zelazne Wyspy bez jazdy | P1 | mala | malo (konnych i tak 4%) | tak | -8% | 5.6 |
| 4.3 | Polnoc: wiecej piechoty, mniej lucznikow | P1 | mala | tak (sklad); mniej lucznikow na murach | tak | +4% / +6% | 5.1 |
| 4.4 | Konie AI: pomiar + kon za awans do zbrojowni | P1 | mala | nie wprost | tak (AI na koniach) | 0 | 167, 5.2 |
| 4.5 | Reach i Dolina - lepsza jazda t3-t5 | P2 | mala | nie | tak | 0 | 167 (konie) |
| 4.6 | Volantis i Norvos - sklad | P2 | mala | tak (sklad); Volantis mniej lucznikow na murach | tak | +3% / -2% | 5.7 |
| 4.7 | Wyspy Letnie bez jazdy | P2 | mala | malo | tak | -16% | 5.5 |
| 4.8 | Tarcze i belty w prawie tieru | P3 | mala | nie | tak (Pentos, Qarth, Aegon slabsi) | 0 | audyt 13, 5.4 |
| 4.9 | Qohor ciezsza piechota | P3 | mala | nie | tak | 0 | - |
| 4.10 | Rekruci t1-t3 ze sprzetem swojego tieru | P3 | srednia | nie | tak | 0 | 171, audyt 13 |
| 4.11 | Dorne lzejsze | P3 | srednia | nie | tak | 0 | - |
| 4.12 | Drobne (Frey, Zlota Kompania, Wolni Ludzie, Smocza Skala, zold Strazy) | P3 | mala | czesciowo | tak | Straz -100% | - |

Zold: zmiana zoldu na czlowieka swiezej / rozwinietej partii krolestwa [D+S]. Dla porownania: gdyby jazda w polu w Westeros wzrosla z ok. 9% do celu ok. 31%,
rachunek zoldu partii wzrosnie o ok. 11-15% (premia konnych z +7,8% dzis do ok. +20-24%; liczone z linii "Zold konnych" doby 120: konny kosztuje bez premii
srednio 9,8 zl, pieszy 4,9 zl) [P+S].


---

## 5. Pytania do Jeffa (tylko zasady gry i kanon)

**5.1 Co znaczy "swietna piechota" Polnocy?**
(a) jak w ksiazkach: liczna i twarda, w kolczudze i futrze - sklad z przewaga piechoty (4.3) i piechota +25 do broni i Atletyki, pancerz bez zmian;
(b) najlepsza piechota Westeros - do tego ciezsze pancerze (wbrew ksiazkom; Westerlands przestaje byc najlepiej opancerzony);
(c) jak (a), plus przewaga w autobitwach: piechota Polnocy +10% mocy na sniegu i w lesie (jedyny sposob, zeby jakosc Polnocy bylo widac w wojnach AI).
Polecam (a) albo (c).

**5.2 Czy rycerz ma przychodzic na wojne z wlasnym koniem?** Dzis obowiazuje Twoja zasada z 30.08: awans na konnego = kon z taboru, dla Ciebie i AI.
Zmiana bylaby taka: awans linii szlacheckiej i rodowej na konnego nie bierze konia z taboru, tylko rod **kupuje** go dla rycerza we wsiach wlasnych lenn
(stado wsi; gdy pusto - na targu), placac zlotem rodu, a kon trafia do zbrojowni tego jezdzca; linia chlopska dalej potrzebuje konia z taboru.
Koszt: kon bojowy ma wartosc bazowa ok. 1 100-2 400 zl (tier 4), szlachetny ok. 6 000-10 000 zl (items-dump), a lordowie AI placa dzis na targu srednio ok. 450 zl za zwyklego konia (zakupy.log); do tego wiecej jazdy w polu to rachunek zoldu partii wyzszy o ok. 11-15% (premia konnych x1.5).
Polecam: najpierw pomiar (4.4), a decyzje podjac po nim - jesli awanse czekaja na konie, ta zmiana da Westeros jazde z ksiazek; jesli nie, nic nie zmieni.

**5.3 Dothrakowie pod murami.** W ksiazkach Dothrakowie "nie biora murow" i omijaja miasta, ktore placa. Po 4.1 (tylko konni) beda grozni w polu.
(a) zostawic jak w ROT - AI Dothrakow oblega miasta jak kazdy, a autobitwa daje konnym przy oblezeniu -10% do -20% (polecam na start, z autotestem);
(b) dodac hamulec: Dothrakowie nie oblegaja twierdz, tylko lupia wsie i biora trybut (zmiana zachowania AI, praca srednia).

**5.4 Pentos:** zostawic pelna armie ROT, tylko slabsza (polecam: 4.8 krok 1 i 2), czy zrobic z Pentos miasto bez wlasnych zolnierzy, ktore najmuje kompanie
(zgodnie z traktatem z ksiazek, ale to duza zmiana wobec ROT)?

**5.5 Wyspy Letnie:** ksiazki nie wspominaja o ich jezdzie. Usunac 2 konne jednostki z szablonu (polecam, 4.7) czy zostawic dla urozmaicenia?

**5.6 Zelazne Wyspy:** zostawic Harlaw Rider jako jedyna jazde (kucyki z Harlaw - polecam) czy calkiem bez jazdy?

**5.7 Slonie Volantis.** Dzis kazda partia lorda Volantis ma w szablonie 3 slonie bojowe (Volantene Mahout) - w Twoich bitwach to bardzo mocna jednostka.
Ksiazki znaja slonie Volantis jako stronnictwo kupcow, zwierzeta ulic i - dawno temu - sto sloni pod Volon Therys w wojnie z Garinem [L: TWOIAF]; o sloniach
w dzisiejszym wojsku Volantis milcza, a 24 slonie ma Zlota Kompania [L: ADWD]. (a) zostawic slonie Volantis (polecam - jest podstawa w historii miasta), czy
(b) zostawic slonie tylko Zlotej Kompanii, a Volantis dac w tym miejscu jazde tygrysich plaszczy?

---

## 6. Ograniczenia

- Liczby z jednej kampanii autotestu (120 dob, bez gracza w wojnach); inna kampania moze dac inny tier i udzial konnych. Polnoc, Dolina, Dorzecze i Korona
  stoczyly po 5-6 bitew lorda z lordem, Zelazne Wyspy zadnej - ich wyniki w kampanii to mala proba.
- Log nie rozroznia piechoty i strzelcow - podzial P/S w polu jest z szablonu, nie z pomiaru. Nie mierzylem, ile awansow na jezdzca AI odpadlo z braku konia (4.4),
  ani wyniku oblezen (linie H3 sa tylko dla bitew polowych).
- Sila to prosty wskaznik - nie zna dlugosci broni, szyku, tarcz w bitwie ani AI formacji; kon we wzorze to kon z wzorca, a w bitwie DTE daje konie ze zbrojowni.
  Wariant "XML" pokazuje projekt ROT; wariant "gra" - umiejetnosci po `SkillSinew` (to, z czym walczysz). Na tierach 2 i 6 kusznicy sa porownani ze wszystkimi strzelcami.
- "Sila wzorca" to stosy t3+ szablonow; ROT losuje zamiany z calych drzew, a jednostki t4-t6 w polu to wynik awansow - srednia z drzew rozni sie o 0-11 punktow (2.2).
- Szacunki zmian [S] (sklad w polu, sila po zmianie, zold) liczone na szablonach, bez symulacji kampanii - kazda zmiane P1 sprawdza autotest.
- Zold BannerKings (mnozniki calej partii) pominiety - jest taki sam dla wszystkich krain. Dochodow rodow nie oceniam (audyt 12).
- Lore z AWOIAF (pobrane 09.10) i ksiazek; liczby d20 sa polkanoniczne; proporcje "z ksiazek" to [ocena]. Serial HBO nie jest zrodlem.
- Bez drogi w danych sa marynarze NavalDLC (`*_marine_t3-t5`). Najemnikow z karczm (`western_mercenary`, `eastern_mercenary`, `sword_sisters_sister_t3`,
  `ROT_mercenary1` z `basic_mercenary_troops` kultur i ich awanse) pierwsza wersja uznala za jednostki bez drogi - maja ja (karczma), ale nie ma ich w tabelach:
  lordowie AI ich werbuja, a ROT zaraz zamienia ich na jednostki szablonu (1.0).

## 7. Zrodla

**Kod [K]:** gra 1.4.8 (dekompilacja): `TaleWorlds.CampaignSystem.GameComponents.DefaultMilitaryPowerModel` (GetDefaultTroopPower, GetContextModifier,
`_battleModifiers`), `DefaultCombatSimulationModel.SimulateHit`, `MapEvents.MapEvent.SimulateSingleTroopHit`, `CharacterObject.GetSimulationAttackPower`,
`DefaultCharacterStatsModel.MaxHitpoints`, `DefaultCharacterStatsModel.GetTier`, `DefaultPartyWageModel`, `RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount`;
TaleWorlds.Core `ItemObject.RelevantSkill`, `WeaponComponentData.GetRelevantSkillFromWeaponClass` (tarcza -> Jednoreczna, belty -> Kusza);
SandBox `TournamentFightMissionController.Simulate`; ROT: `ROT.Models.ROTMilitaryPowerModel`, `ROT.Models.ROTTroopUpgradeModel`,
`ROT.CampaignBehaviors.ROTTroopRecruiter` (Settings, TraverseTree, ExchangeClanTroops, DetermineReplacement, ShouldRecruit, GetPartyComposition,
GetTemplateComposition, DailyTickSettlement, IsHeroManageable, IsGarrisonManageable); BannerKings `BannerKings.Patches.VanillaModelTweakPatches.BKBattleSimulationTweakPatches`;
RBM `RBMCombat.CampaignChanges.OverrideDefaultMilitaryPowerModel`; DTE `PartyEquipmentDistributor` (AssignHorseAndHarness, dobor zbroi z wzorca);
CrashScribe `Mends.SkillSinew`, `Mends.WeaponTierLaw`, `Mends.ReqSkill`, `Mends.ValyrianWardSim`; Armoury `Stables` (RanksNeedHorses, RiderOnlyTarget,
RequiredMountFor, Consume, FilterTargets, PayInHorses, OnSettlementEntered, BankPaidHorses), `HouseLevies`, `MountedWage`, `LosersFlee` (H3), `AiGear`
(wersja z gry: scratchpad `sklad4`); specyfikacja paczki 171 (`docs/paczki/171-zbrojenie-zalog.md` na galezi `w-toku/171-zbrojenie-zalog`).

**Dane [D]:** `Modules/ROT-Content/ModuleData/ROT-Troops.xml`, `ROTassets.xml`, `items.xml`, `spnpccharacters.xslt`, `naval_characters.xslt`, `spcultures.xml`,
`ROT_spkingdoms.xml`, `spclans.xml`, szablony partii ROT; SandBoxCore `spnpccharacters.xml`; RBM `RBMCombat_unit_overhaul.xml`, `RBMCombat_horses.xml`;
zrzut przedmiotow z gry `Documents/Mount and Blade II Bannerlord/CrashScribe/items-dump.csv`. Przetworzone: `armie/jednostki.csv`, `armie/szablony.csv`,
`armie14r/kultury2.json`, `armie14r/krolestwa2.json`, `armie14r/sila_koszt2.json`, `armie14r/podbicia.json`, `armie14r/bez_tarcz.json`.

**Log [P]:** `kopiaT9-120/2026-10-08_22-23-58/bitwy.log` (2 987 bitew polowych, 932 krolestwo-krolestwo, w tym 513 lord z lordem), `budzet-rodow.csv` (doba 120),
`zakupy.log` (Stajnia AI), `Armoury-2026-10-08_22-23-58.log` (start: Stables, HouseLevies, SkillSinew; co dobe "Konie rekrutow (160)" i "Zold konnych (160)").

**Lore [L]:** AWOIAF (pobrane 09.10): Military_strength, Northmen, The_North, Knights_of_the_Vale, Vale_of_Arryn, The_Riverlands, Westerlands, The_Reach,
Stormlands, The_Crownlands, City_Watch_of_King's_Landing, Dragonstone, Dorne, Dornishmen, Ironborn, Iron_Islands, Night's_Watch, Free_folk, Giants, Thenns,
Skagos, Dothraki, Unsullied, Three_Thousand_of_Qohor, Golden_Company, Volantis, Tiger_cloaks, Braavos, Bravo, Pentos, Lys, Myr, Tyrosh, Norvos, Qohor, Lorath,
Sarnor, Qarth, Yi_Ti, Summer_Isles, Ibben; Westeros.org Concordance (Dothrakowie a zbroja, AGOT I:556). Szczegoly i rozdzialy: `armie/lore.md`.

**Historia [H]:** Wikipedia "Battle of Bannockburn", "Schiltron", "Jinete", "Genoese crossbowmen", "Military of the Mongol Empire", "Knight-service";
A. Curry, wyklad o Agincourt 2011 (history.org.uk); ACOUP 2019 (Nieskalani).


---

## 8. Krytyka i odpowiedzi

29 uwag krytyki (09.10). Kazda sprawdzilem w kodzie, danych albo logu; zadna nie okazala sie w calosci falszywa - tam, gdzie krytyka myli sie w czesci,
pisze to jednym zdaniem. "Przyjeta" = poprawione w tekscie i tabelach; numery propozycji wedlug tej wersji.

| Nr | Waga | Uwaga (krotko) | Werdykt | Co sprawdzilem i co zmienilem |
|---|---|---|---|---|
| 1 | krytyczne | "Bitwy z krolestwami" to tez napady na wiesniakow i karawany | **Przyjeta** | [P] 932 bitwy krolestw: 513 lord z lordem, 219 karawan, 181 grup wiesniakow, 19 innych (patrole). Teraz osobno bitwy lordow i stracone wsie/karawany (2.2, 2.3, wiersze krolestw w 1.x, 3.x); Dothrakowie 30% -> 67%, Polnoc 37% -> 80% (5 bitew), Daenerys 63% -> 32%. Dodane bitwy wyrownane (88%) w 2.1 |
| 2 | wazne | `bitwy.py` ucina "House Targaryen, Aegon" | **Przyjeta** | [K] potwierdzone: 123 bitwy "House Targaryen" = 99 Daenerys + 24 Aegona. Nowy skrypt bierze regex z przecinkiem: Daenerys 44 bitwy lordow (32%), Aegon 15 (27%) |
| 3 | wazne | Tier i konni w bitwach licza wiesniakow i karawany | **Przyjeta** | [P] Dorzecze 1,95 -> 2,33, Polnoc 2,51 -> 2,76; konni Dothrakow 20,8 -> 18,5%. Teraz tylko partie lordow; "ciagle straty" Dorzecza skreslone |
| 4 | wazne | +0.2 x naciag faworyzuje kusze; belty tieru 6 | **Przyjeta** | [D] `bolt_a`/`bolt_b` tier 6, trudnosc 175 (items-dump); [K] `WeaponTierLaw` obejmuje amunicje. Strzelcy porownywani w obrebie klasy broni: wzorce Korony 126 -> 95, Aegona 136 -> 103, Nocnej Strazy 119 -> 91; kultura Myr 115 -> 101. Belty dopisane w 2.3 i 4.10 |
| 5 | wazne | Wskaznik piechoty liczy tarcze podwojnie, pancerz dwa razy | **Przyjeta czesciowo** | [K] tarcza wymaga Jednorecznej (`GetRelevantSkillFromWeaponClass`, `ItemObject.RelevantSkill` przez `PrimaryWeapon`), `pentoshi_shield` XML 50 -> 175. Jednoreczna 175 w grze jest jednak prawdziwa (z nia walczysz), wiec kolumna "gra" ja zostawia; dodalem kolumne "XML" (bez SkillSinew), znaczniki `*T`/`*A` i rozbicie przyczyn w 2.3 - liczby jak u krytyki (Pentos 108 wedlug ROT). Wzor liczy teraz umiejetnosc noszonej broni |
| 6 | wazne | "-20 do broni" (4.9) nie zadziala | **Przyjeta** | [K] `SkillSinew` "raise, never lower" (Mends.cs:2403-2479). Zamiast tego 4.8: tarcze poza prawem tieru broni (Pentos 125 -> ok. 117 [S]) i nizszy tier pancerza we wzorcach; w 4.3 i 4.5 dopisane "ponad wartosci w grze" |
| 7 | wazne | Szarza slonia/mamuta/jednorozca w Sile jazdy | **Przyjeta czesciowo** | [D] Elephant Rider i Mahout na sloniu 20/350, Skagosi na jednorozcu 55/80. Szarza ucieta do 40: jazda Aegona 116 -> 105, "najmocniejszy przepis swiata" i "124" skreslone. Skagos zostaje 116, bo reszta przewagi to podbicia SkillSinew (1H 175, Rzut 140), nie wierzchowiec |
| 8 | wazne | Polnoc bez Umberow, Mormontow, Cerwynow | **Przyjeta** (inna przyczyna) | [K] to nie stary przebieg: `krolestwa.py` zapisywal w kolumnie "szablony" tylko 6 najczestszych szablonow (`most_common(6)`), a nastepne skrypty czytaly te kolumne. Teraz wszystkie 9 szablonow Polnocy; 4.3 obejmuje wszystkie 8 szablonow rodow |
| 9 | wazne | "Sila przepisu" to nie to, co AI wystawia; "zostaja" w 4.1 | **Przyjeta** | [K] pula ROT = drzewa stosow (`TraverseTree`), zamiana losowa (`DetermineReplacement`). Nazwa zmieniona na "sila wzorca (stosy t3+)", dodana srednia z drzew (2.2, 2.3); 4.1 opisuje stosy "usunac / ustawic / dodac nowe" z dzisiejszymi liczbami (4 / 3 / 2 / 2 / 1) |
| 10 | wazne | Sand Steed nie ma pancerza 71 | **Przyjeta** | [D] `t2_aserai_horse` pancerz 0 (items-dump; items.xml:177-179). Zbroja konska 82 tylko u Dayne Knight, Knights of Starfall, Yronwood Knight, Boneway Guardian, zwykli 8-22. Usuniete z 3.1 i z propozycji Dorne (4.11) |
| 11 | drobne | Hunter i Battanian Pony to konie RBM | **Przyjeta** | [D] RBMCombat_horses.xml:449 (Hunter: war_horse, trudnosc 60, +150 zycia) i :918 (Battanian Pony, kon turniejowy). Poprawione w 0, 1.0 i 3.1; kon oznaczony jako [D-wzorzec] |
| 12 | drobne | "Najemnik x1.5" mylace; rody najemne w liczbach | **Przyjeta** | [D] 41 jednostek kompanii ROT ma `occupation=Soldier`. Uzupelnienie: x1.5 dziala na najemnikow z karczm (`Occupation.Mercenary`, gra bierze ich z `Culture.BasicMercenaryTroops`). Udzial rodow najemnych w ludziach dopisany w 2.3 i w wierszach krolestw; w bitwach - log nie podaje rodu |
| 13 | drobne | Kolumny 2.2 wazone inaczej | **Przyjeta** | Wszystkie kolumny z szablonow wazone ludzmi w partiach rodow (doba 120), opisane w 1.0 i 2.2 |
| 14 | drobne | "90-110", "dokladnie", "124" | **Przyjeta** | W grze 69 z 84 wartosci w 90-110 (73-125), wedlug ROT 77 z 84 (85-109); "dokladnie" -> "prawie zawsze"; "124" usuniete |
| 15 | drobne | Pominiete mechanizmy Armoury (Stables, konie najemnikow) | **Przyjeta** | [P] zakupy.log: 592 zakupy, 1 591 koni, "czekalo na awans" 3 311; [P] start: 128 celow "z siodla"; [P] 8 617 konnych najemnikow. Opisane w 1.0, 2.3 i 4.4 |
| 16 | krytyczne | "Rycerz z wlasnym koniem" i 5.3(b) = kon z niczego | **Przyjeta** | [K] Stables.cs:11-20 (decyzja 30.08), CHANGELOG 14.09, `MountedWage`, `ROTTroopRecruiter`. 4.4 zdjete z P1 (teraz pomiar + blad konia za awans), 5.2 postawione uczciwie jako zmiana decyzji 30.08 z kosztem i zrodlem konia (kupiony przez rod we wlasnych wsiach, do zbrojowni jezdzca), 5.3(b) wycofane |
| 17 | krytyczne | 4.1 bez oceny skutkow dla AI | **Przyjeta** | [K] skok tieru bez stosu t1 (`DetermineReplacement`, doplata lorda :205-212), H3 (LosersFlee.cs:57, :643-648), konni lucznicy +30% (1057u). 4.1: stos t1 zostaje, zold +26% / +49%, H3 +18-21 pkt, autotest przed wgraniem, pytanie 5.3 o mury. Krytyka w czesci przesadza: "+31 pkt" zaklada 100% konnych, a przy zostawionych rekrutach t1 bedzie ich ok. 60-70% |
| 18 | wazne | Kolizja z 171 i DTE - "jazda na papierze" | **Przyjeta** | [K] spec. 171 B6 i 1.4, `AiGear` bez koni, `Stables.Consume`/`PayInHorses` (kon AI znika) wobec `BankPaidHorses` (kon gracza do zbrojowni), DTE `AssignHorseAndHarness` (:800-830). Zasada "kon musi skads byc" na poczatku rozdz. 4, blad konia AI w 4.4 (dla 167), uwaga w 2.1 |
| 19 | wazne | Przyczyna spadku jazdy niezmierzona | **Przyjeta** | [P] najemnicy konni 167 -> 85 -> 28 dziennie, razem 8 617; konni ochotnicy 448; konie AI 1 591. Poprawione "Dla Jeffa" pkt 8 i 2.3; pomiar `FilterTargets` i zrodel konnych w 4.4. Zastrzezenie: najemnik nie zostaje najemnikiem - ROT zamienia go na jednostke szablonu, wiec "glowne zrodlo" to najemnicy razem z zamiana ROT |
| 20 | wazne | Lore dotyczy calej armii, a nie celu z t3+ | **Przyjeta czesciowo** | [K] cel liczony jest z t3+, ale ROT porownuje z nim cala partie, liczac kazdego czlowieka do formacji docelowej (`GetPartyComposition`) - wiec cel rzadzi dojrzala armia; w polu jednak walcza rekruci pieszo. Porownanie z lore przez sklad w polu (swieza partia + pomiar konnych), "wiecej lucznikow niz piechoty" doprecyzowane, 4.3 z celem 45 / 31 / 24 (pole ok. 55-60 / 20-25 / 15-20 [S]), 4.2 z cala armia 82 / 18 / 0 |
| 21 | wazne | 4.9 nie zadziala; Pentos przez SkillSinew; Magister Guard to jazda | **Przyjeta** | Jak uwaga 6; [D] `pentoshi_pike_warrior` XML 1H 110 -> 175 (tarcza), `magister_guard` = jazda t6. Atrybucja w 2.3 i 3.2 |
| 22 | wazne | Brak H3; zalogi pod szablonem; lucznicy na murach | **Przyjeta** | [K] H3 (LosersFlee), `DailyTickSettlement` (:51-79), `_battleModifiers` 74u = +0.3 (lucznik obroncy), 70u = 0 (piechota). Akapit H3 w 2.1, punkt 1 na poczatku rozdz. 4, kolumna "Autobitwa" w tabeli zbiorczej; ciecie lucznikow Polnocy zlagodzone (cel 31%, nie 21%); oblezenia do pomiaru w autotescie |
| 23 | wazne | Volantis ma slonie, nie wielblady | **Przyjeta czesciowo** | [D] `tigercloak_camel_cavalry` = Volantene Mahout na sloniu (ROT-Troops.xml:16132-16161) - poprawione w 1.20, 3.2, 4.6. W lore krytyka sie myli: TWOIAF zna sto sloni pod Volon Therys w wojnie z Garinem, wiec slonie Volantis maja podstawe w historii miasta - pytanie 5.7 |
| 24 | wazne | Brak rachunku zoldu wiekszej jazdy | **Przyjeta** | [P] doba 120: 8,2% konnych daje +7,8% zoldu; Westeros przy ok. 31% konnych +11-15%; Dothrakowie +26% (swieza) / +49% (rozwinieta) - nie "dwukrotnie"; kolumna "Zold" w tabeli zbiorczej i koszt przy kazdej propozycji skladu |
| 25 | drobne | Robb: 3 tys. z 20 tys. to nie 1/4 | **Przyjeta** | [L] AWOIAF Military strength: w Winterfell ok. 12 tys., w tym ok. 3 tys. konnych i 300-400 rycerzy; AWOIAF szacuje ok. 5 tys. jazdy na 20 tys. "Armored lances" usuniete (nie potwierdzone) - 3.1 i 4.3 |
| 26 | drobne | 98% to za mocny dowod | **Przyjeta** | [P] mediana stosunku sil 4,3 : 1 (lord z lordem 3,0 : 1); w bitwach wyrownanych 76 z 90 (84%), lord z lordem 61 z 69 (88%). Dowodem jest kod, 98% opisuje wybor bitew (0 pkt 1, 2.1) |
| 27 | drobne | Niespojnosci (124, liczby 4.1, 137 wymogow XML) | **Przyjeta** | "124" usuniete; 4.1 z dzisiejszymi stosami; 1.0 opisuje regule Stables (kategoria z tieru celu, `RiderOnlyTarget`) zamiast wymogu z XML |
| 28 | drobne | XSLT w ModuleData blokowane; zgranie z 171 | **Przyjeta** | Wstep do rozdz. 4: latka przy wczytaniu w CrashScribe; plik danych tylko recznie przez Ciebie; punkt 3 o dobytku 171, VolunteerKit i DTE |
| 29 | drobne | Zold Nocnej Strazy = 0 wedlug decyzji | **Przyjeta** | STAN-PRAC (pytanie kanonu 4); uwaga w 3.1 i w 4.12 |
