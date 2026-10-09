# PACZKA 175 - ARMIE KROLESTW (projekt dla wykonawcy, 09.10.2026)

Status: SAM PROJEKT, **wersja 2 po krytyce (09.10 ok. 04:30; 34 uwagi, odpowiedzi w rozdz. 10)**. Nic nie zbudowane, nic nie wgrane, nic nie zacommitowane,
gra nie uruchamiana, folder gry nietkniety. Jedyny pisany plik - ten (plus skrypty rachunkowe w scratchpadzie, rozdz. 9).

Baza kodu (zatwierdzone w grze od 09.10 ok. 01:50): **CrashScribe e8d460c5** = drzewo `SCR\noc2\a175cs` (galaz `w-toku/175-armie-cs` od `w-toku/e1-pokoj`, 5b4e551),
**Armoury 63640cb3** = drzewo `SCR\noc2\a175arm` (galaz `w-toku/175-armie-arm` od `noc/sklad5`, d486813). Paczki 171/172/172b/174, K1 i Z1 NIE sa w tej bazie.
SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`,
SCR3 = `...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad` (dekompilacje: gra `ore-supply\cs`, BK `ore-supply\bk`, ROT `audyt-pieniadz\rotall`).
Sciezki `[K]` bez przedrostka: CrashScribe = `a175cs\CrashScribe\src`, Armoury = `a175arm\Armoury\src`.

Podstawa (przeczytane): `docs/audyt-2026-10-09/14-ARMIE-KROLESTW.md` (1.0, 2.1-2.3, 4.1-4.12, 5), `13-UMIEJETNOSCI.md`, rozpoznania tej paczki
`SCR\a175\sprzet.md` (sprzet wedlug tieru) i `SCR\a175\sklad.md` (sklad, Polnoc, konie, miara), `docs/STAN-PRAC.md` (decyzje 30.08, 02.09, 05.10, 08.10, 09.10),
specyfikacja K1 (`SCR\noc2\k1\docs\paczki\K1-dozbrajanie.md`). Rachunki tej wersji (powtarzalne, tylko odczyt): rozdz. 9.

Oznaczenia: **[K]** przeczytane w kodzie, **[D]** dane XML, **[P]** pomiar z logu autotestu `SCR\kopiaT9-120` (120 dob nowej kampanii, 08.10, wersja T9 =
Armoury 0f8a80b0 + CrashScribe 269cc980), **[S]** szacunek z rachunku, **[L]** lore, **[ocena]** moj wniosek.

**Decyzje Jeffa (wiazace dla tej paczki):**
- 09.10 ok. 02:35 (STAN-PRAC :782-790): (1) Polnoc wariant C - "Polnocnych jest MNIEJ, ale sa twardsi": +25 broni glownej i Atletyki piechoty t3-t6 i przewaga
  w autobitwie na sniegu/w lesie, z warunkiem "zeby nie zepsuc rownowagi gry, zeby nagle jedna armia nie bila wszystkich"; (2) rycerz z koniem - najpierw
  pomiar, decyzja po pomiarze; naprawic: kon oddany przez AI za awans trafia do zbrojowni nowego jezdzca, nie znika; (3) Dothrakowie konni (4.1), pod murami
  wariant (a) - bez zmian zachowania AI, z autotestem; (4) Pentos (a) - pelna armia, slabsza przez sprzet wedlug tieru (rekomendacja audytu: 4.8 krok 1 i 2);
  (5) Wyspy Letnie - ZOSTAWIC jazde; (6) Zelazne Wyspy (a) - jazda tylko Harlaw; (7) slonie Volantis zostaja; (8) wariant (b) - "kto zrobil, ze tier 4 dostawal
  tarcze tieru 6 i pod to umiejetnosc podnoszono, to jakas bzdura, trzeba wszedzie poprawic".
  **Czego Jeff NIE postanowil** (sprawdzone w STAN-PRAC :782-790 i w pytaniach audytu 14 rozdz. 5): sklad Volantis i Norvos (4.6 - pytanie 5.7 dotyczylo tylko sloni),
  Qohor ciezej (4.9), Dorne lzej (4.11), Wolni Ludzie i Smocza Skala (4.12). Te czesci sa w projekcie zbudowane, ale z wylacznikami domyslnie WYLACZONYMI (rozdz. 5)
  i z jednym pytaniem w rozdz. 0 pkt 9. Qarth zostaje przy Pentos (audyt 4.8 krok 2, polecony do pytania 5.4, ktore Jeff zatwierdzil).
- 09.10 ok. 02:50: wymogi sprzetu ZOSTAJA ("jak nie mam danej umiejetnosci, nie moge zalozyc") - 175 zmienia TYLKO wzorce zolnierzy, nie wymogi przedmiotow.
- 30.08: awans na konnego = kon z taboru, dla gracza i AI ("warunki globalne"); 02.09: "strzaly tieru 6 tylko dla tieru 6"; 08.10: "wszystko z czegos wynika";
  paczka 160: kon jest wlasnoscia zolnierza; 09.10 (C): "mniej wojska AI" - NIE (zamiast tego podatek wojenny korony); 09.10 (K): zolnierze sami sie dozbrajaja (K1).
- Stale: parametry dobiera projekt (bez pytan o liczby), jedna zasada dla gracza i AI, nic z niczego, teksty w grze po angielsku, komentarze bez polskich znakow.

---

## 0. Dla Jeffa - prostym jezykiem

1. Kazdy rodzaj zolnierza (wszystkie krainy, rekruci, milicje, najemnicy, oddzialy rodow - 893 z 1216 rodzajow) dostaje w swoim wzorcu bron, tarcze, strzaly i zbroje najwyzej swojego stopnia, zasada "jak nie masz umiejetnosci, nie zalozysz" zostaje taka jak dzis, a sztucznie napompowani slabna (piechota Pentos ze 125 na 89, Qarth ze 115 na 98, Braavos i Zlota Kompania ze 113-115 na 99-102) i prawie wszystkie krainy wracaja w przedzial 90-110, jak je zaprojektowali autorzy ROT.
2. Na Twoim zapisie Twoi zolnierze 2.-5. stopnia straca podbite umiejetnosci (np. pikinier Pentos ze 175 na 110) i nie udzwigna czesci dawnego sprzetu 5.-6. stopnia - jesli w zbrojowni masz tylko takie tarcze czy strzaly, w pierwszych bitwach czesc ludzi pojdzie bez tarczy albo bez strzal (kwatermistrz pokaze, co dokupic), dlatego polecam wgrac 175 razem z dozbrajaniem (K1) albo po nim: tam zolnierze sami sprzedaja to, czego nie udzwigna, i kupuja sprzet na swoja miare.
3. Twoje pytanie o Polnoc: tak, w ksiazkach ludzie Polnocy uchodza za twardszych od poludniowcow (zwlaszcza w zimie i w marszu), ale w otwartym polu przegrali z Tywinem; dzis ich piechota jest tylko o 2% lepsza od reszty swiata, po 175 bedzie o 28% lepsza i pierwsza z 30 kultur (przed Nieskalanymi), bo Polnoc zostaje mniej wiecej na dzisiejszym poziomie (umiejetnosci ok. 155), a reszta swiata spada do ok. 121.
4. Doslowne "+25 ponad dzisiejsze" daloby przewage +46% i stanelo na tych samych liczbach podbitych tarczami 6. stopnia, ktore nazwales bzdura, wiec licze +25 od uczciwej podstawy - skutek: u 27 z 43 rodzajow piechoty Polnocy pojedyncza liczba bedzie nizsza niz dzis (np. pieszy Starkow w broni 175 -> 105), choc wobec swiata Polnoc rosnie; potwierdz to przy "wgraj" (suwak 0-50 w menu, +15 daje ok. +20%).
5. W autobitwach piechota Polnocy od 3. stopnia ma +10% na sniegu i w lasach Polnocy, ale autobitwe prawie zawsze wygrywa silniejszy (gdy ma choc o czwarta wiecej sily - 32 bitwy z 32), wiec te +10% rozstrzyga tylko bitwy prawie rowne (14 z 520 bitew lordow w 120 dob na calym swiecie) i nie zrobi z Polnocy armii, ktora bije wszystkich - sprawdzi to osobny test z wymuszona wojna Polnocy; +25 i sprzet wedlug stopnia dzialaja tylko w Twoich bitwach, wiec je ocenisz Ty, a autotest zmierzy sklad, pieniadze, twierdze i te +10%.
6. Polnocnych jest dzis w polu najwiecej (ok. 12 tys.) i wedlug ksiazek ma ich byc mniej, ale to ma wyjsc z ich biedy i ludnosci w paczce budzetu rodu (166), nie z dekretu - tu tylko mierzymy.
7. Dothrakowie walcza tylko konno (piesi zostaja w milicji), jezdzcy dostaja +30 do jazdy konnej (bylo w zatwierdzonym punkcie audytu), a obcy zolnierz, ktorego ROT przerabia na Dothraka, zostaje pieszy, jesli nie ma dla niego konia; zold ich partii rosnie jednak o 26-47%, a z liczb wychodzi, ze rodow na to nie stac (dodatkowe 0.8-1.6 mln wydatkow w 120 dob wobec 0.6 mln w kiesach), wiec wylacznik po tescie najpewniej zadziala - na pozniej mam pomysl z ksiazek (trybut Wolnych Miast dla khalow).
8. Kon oddany przez lorda AI za awans trafi do zbrojowni (jak u Ciebie), przy nastepnym awansie lord wezmie najpierw wolnego konia ze zbrojowni (Ty robisz to recznie), a test policzy, ilu awansom brakuje konia - dopiero z tych liczb decyzja o "rycerzu z koniem"; kazda czesc paczki ma osobny wylacznik w menu.
9. Zbudowane, ale WYLACZONE do Twojego slowa, bo to propozycje audytu, o ktore Cie nie pytalem: sklad Volantis i Norvos jak w ksiazkach (wiecej piechoty, mniej lucznikow i jazdy, slonie zostaja), ciezsza piechota Qohoru, lzejsza piechota Dorne, mniej jazdy u Wolnych Ludzi i na Smoczej Skale - wlaczyc?
10. Odkladam konnych lucznikow Freyow, luki Zlotej Kompanii (po K1; wtedy zapytam o jeden wyjatek od Twojej zasady "bron glowna = najwyzsza umiejetnosc"), brak zoldu Nocnej Strazy (paczka 176) i lzejsza piechote Lorath (najblizsza zbroja jest ciezsza); skutki uboczne: Dornijczycy 2.-3. stopnia rzucaja toporkami zamiast oszczepow (oszczepy sa w grze dopiero od 4. stopnia), procarze kamieniami, rekruci maja siekiere zamiast miecza, 3 rodzaje jezdzcow zachowuja podbita umiejetnosc przez konia albo zbroje konia (zmiana zdarlaby barwy rodow wszystkim rycerzom Westeros), a zasada walki z Innymi zostaje dokladnie taka jak dzis.

---

## 1. Sprzet wedlug tieru (decyzja 8b) - 175.2

### 1.1 Zasada
- **Co:** dla kazdej jednostki zolnierskiej (nie bohatera) kazdy przedmiot w kazdym jej zestawie BOJOWYM, w slotach broni 0-3 (bron, tarcze, amunicja)
  i pancerza 5-9 (glowa, korpus, nogi, rece, peleryna), ma tier gry <= tier jednostki. Przedmiot ponad tier zastepujemy przedmiotem tego samego rodzaju (1.3).
- **Czego NIE zmieniamy:** wymogi przedmiotow (Prawo Wagi, prawa tieru pancerza i broni - 35 pkt na tier), `SkillSinew` ("umiejetnosc do wlasnego sprzetu,
  nigdy w dol"), bohaterowie (gracz, towarzysze, lordowie, ich szablony), konie i ladry (slot 10-11 - poza zakresem decyzji 8b), sztandar (slot 4), zestawy
  cywilne, zold (zalezy od tieru jednostki), `IsMounted`/`IsRanged` (z `default_group`), autobitwa (moc z tieru).
- **Tier:** jednostki = `CharacterObject.Tier` (gra: min(max(ceil((poziom-5)/5),0),6)); t0 traktujemy jak t1 (jak `sprzet.py`, `T = max(1, tier)`).
  Przedmiotu = `(int)item.Tier + 1` (pulapka z CLAUDE.md 7: `Tier1 == 0`) - tier liczony przez gre NA ZYWO z wzorow RBM (`ItemValuesTiers.cs`), nie z koncowki `_tN`
  w id (129 z 312 broni z `_tN` ma w grze tier wyzszy niz w nazwie). To ten sam tier, ktorego uzywaja nasze prawa i `HistoricalPrices` (HistoricalPrices.cs:271).
- **Nic z niczego:** to tylko wzorce (opis tego, czym zolnierz "powinien" walczyc). Zaden przedmiot nie powstaje w swiecie; zmienia sie, co AI kupuje
  i co DTE szuka na polce.

### 1.2 Zakres - liczby (rozpoznanie `sprzet.md` rozdz. 3, zestawy z XML + tier i wymog z `items-dump.csv` sesji 09.10 02:39)
- **893 z 1216 jednostek zolnierskich** (occupation Soldier/Mercenary/Bandit/CaravanGuard/Gangster) ma cos ponad tier (bez koni; z konmi 900). Czyste z definicji: t6 (72).
- Bron 524 jednostki (881 wystapien: wrecz 752, rzut 79, luk 22, proca 28), tarcza 213 (293), amunicja 48 (107, w tym belty 91), pancerz 686 (1271: korpus 532,
  glowa 364, peleryna 292, nogi 60, rece 23). Roznica tierow: +1 - 633 broni / 189 tarcz / 955 pancerzy; +3 - 152 wystapienia; +4/+5 - 25 (belty `bolt_a` t6
  u kusznikow t1-t2, tarcze rodowe t6 u t2).
- Wedlug tieru jednostki (jednostek / z czymkolwiek ponad): t1 70/57, t2 200/172, t3 246/183, t4 308/240, t5 319/240, t6 72/0.
- Wedlug drzewa: szablony kultur 316, rody 283, wies 293, kasztelanie gracza 235, szlachta 123, bez drogi (m.in. najemnicy z karczm) 87, milicja 83,
  bandyci 56, kompanie najemne ROT 27, karawany 7 (jednostka liczona w kazdym swoim zrodle).
- Najczestsze: `vlandia_sword_1_t2` (w grze t3) u 70 jednostek od t1; `bolt_a` t6 u 31 od t1; `eastern_spear_1_t2` t5 u 28 od t2; `woodland_longbow` t6 u 17 od t3;
  `pentoshi_shield` t6 u 11 od t2; `qohorik_sword` t6 u 11 od t2. Pelna lista per slot: `SCR\a175\zamiany.csv` (2628 wierszy).
- **Kontrola w logu:** latka ma znalezc te same rzedy wielkosci (893 +-3% jednostek, ok. 2 550 slotow bez koni). Inny wynik = blad zakresu albo tieru.
  **Poprawka (krytyka):** ten rachunek uzyl tierow z `items-dump.csv`, czyli PO `ArmorSanity`/`AmmoSanity` (`ItemDump` jest na koncu lancucha, Mends.cs:4693), a latka
  liczy tier PRZED rozsadkiem - zamieni dodatkowo sloty z przycietymi sztukami (licznik `q`, 1.10). Bramka porownuje wiec `(sloty - q)` z ok. 2 550, nie same sloty.
  Do tego warunki z 1.3 (konny, plec, bandyta) beda odrzucac czesc kandydatow, ktorych rachunek z XML nie znal - liczby 1143/158/13 i 63 pary bez klasy moga
  wyjsc inne; bramka 6.1 pkt 2 dopuszcza dla nich +-15% (zakres jednostek i slotow zostaje +-3%/+-5%).

### 1.3 Zamiennik - regula wyboru (deterministyczna)
- **Pula:** przedmioty gry, ktore sa towarem (`!NotMerchandise`/merchandise) albo juz siedza w jakims wzorcu zolnierza; BEZ: unikatow (`Mends.UniquePrefixes`
  i lista imiennych), klng lore (`Mends.BladePrefixes`), sprzetu umarlych (`Mends.IsDeadGear`), smokow (`dragon_`); `peasant_*` tylko dla t1;
  **nowe:** przedmioty kultur bandytow (`bandit_*`, `tacky_bandit_*`, `looter*`) tylko dla jednostek z occupation Bandit (rozpoznanie dawalo `bandit_cap`
  piechurowi Pentos i `tacky_bandit_boot` Ibbenczykowi); zbroja cywilna (flaga Civilian) tylko za cywilna.
- **Ten sam rodzaj ("scisle"):** ten sam `ItemType` i ta sama `PrimaryWeapon.WeaponClass`; dlugosc broni i tarczy +-max(15 cm, 20%); luk dlugi za dlugi
  (`item_usage` "long_bow"), kusza lekka za lekka; tarcza tej samej klasy (Small/Large); amunicja tego samego typu; pancerz ten sam slot.
  **Do sprawdzenia w kodzie przez latke (dane XML tego nie mowia):** u jednostek konnych (`co.IsMounted`) zamiennik musi byc uzywalny z siodla (`Mends.MountOk`),
  a lanca z "couch" (flaga broni / `ItemUsage`) - tylko za lance z "couch"; wlocznia z "brace" - za wlocznie z "brace"; `ItemFlags.NotUsableByFemale/Male`
  zgodne z plcia jednostki (`co.IsFemale`).
- **Kaskada (kolejnosc wyboru):** 1) kultura jednostki, tier = T; 2) kultura jednostki, T-1; 3) ta sama strona Waskiego Morza (KOPIA listy `MountLaw.Essos` w CS -
  pole jest prywatne w Armoury, a CS nie ma referencji do Armoury; komentarz w kodzie "zrodlo: Armoury/src/MountLaw.cs:30", log "kultur Essos: 19") albo bez kultury,
  tier = T; 4) dowolna kultura, tier = T; 5) kultura jednostki nizej; 6) reszta. "Kultura jednostki" = kultura przedmiotu z XML albo to, ze przedmiot nosi juz
  zolnierz tej kultury. W ramach poziomu: scisly przed luznym, wyzszy tier, noszony juz przez zolnierzy, kultura oryginalu, nizszy wymog, na koniec `StringId`
  (wynik nie zalezy od kolejnosci listy obiektow). Rachunek: zamiennik w tierze jednostki w 1143 z 1314 par, o 1 nizej w 158, o 2-5 nizej w 13.
  Kultura przed tierem dawala absurdy (Dornijski Wlocznik t3 z widlami t0) - dlatego tier wazniejszy, kultura wygrywa tylko do T-1.
- **Jeden wybor na klucz (przedmiot, T, kultura jednostki, konny, plec, bandyta)** - zapamietany w slowniku; ten sam we wszystkich zestawach i jednostkach
  o tych cechach. (Poprawka po krytyce: klucz bez cech jednostki dawalby wybor pierwszej jednostki wszystkim nastepnym - wlocznie dobrana dla piechura
  trafilaby do jezdzca, sztuka niedozwolona dla kobiet do lowczyn.) Log 1.10 liczy odrzuconych kandydatow wedlug powodu (siodlo, couch/brace, plec, bandyta, cywilne).
- **Zawsze WLASNY roster (nie zmiana w miejscu):** `MBEquipmentRoster.AddEquipmentRoster` wklada te SAME obiekty `Equipment` do kazdej postaci z tym samym
  `EquipmentSet` (MBEquipmentRoster.cs:110-119), a `FillFrom`/`InitializeEquipmentsOnLoad` dziela caly roster (BasicCharacterObject.cs:173-176, :309).
  **Wersja 2 (po krytyce):** kazda jednostka w zakresie, ktorej cos zmieniamy, dostaje ZAWSZE nowy `MBEquipmentRoster` (plytka kopia listy `_equipments`,
  nierejestrowany - wzorce jednostek nie ida do zapisu gry) z klonami zmienionych zestawow (`new Equipment(eq)`/`eq.Clone()`, typ Battle zachowany); listy
  starego rostera NIGDY nie zmieniamy. Powod: CrashScribe laduje sie PRZED Sandbox (SubModule.xml: SandBoxCore/Sandbox `LoadAfterThis`), wiec jego
  `AfterRegisterSubModuleObjects` biegnie przed Sandbox (tam na zapisie `InitializeCharactersAfterLoad`, SandBoxManager.cs:384) - mapa "roster -> postacie"
  nie widzialaby bohaterow, ktorzy dostaja roster chwile pozniej. Mapa i analiza wspoldzielenia (`guard_nord`) przestaja byc potrzebne; bohaterowie zostaja
  przy starym, nietknietym rosterze. Lista postaci: `MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()` (w AfterRegister `Campaign.Characters` jeszcze
  nie istnieje - `InitializeCachedLists`, Campaign.cs:1448, biegnie po SandBoxManager.OnCampaignStart, :1415). Wykonawca sprawdza w dekompilacji TaleWorlds.Core
  konstruktor `MBEquipmentRoster`, nazwe pola listy (`_equipments`), pole `_equipmentRoster` postaci (setter przez refleksje) i konstruktor kopiujacy `Equipment`;
  po podmianie `OrderEquipments()`, jesli istnieje (BasicCharacterObject.cs:526).

### 1.4 Brak zamiennika tej samej klasy - 63 pary / 79 wystapien (kazda ma zapas tej samej umiejetnosci)
| Przedmiot (tier gry) | Tier jednostki | Zapas | Ile |
|---|---|---|---|
| miecze `vlandia_sword_1_t2` (t3), `empire_sword_1_t2`, `sturgia_sword_*`, `aserai_sword_*_t2`, `battania_sword_1_t2`, `nord_sword_1_t2`, `falchion_sword_t2`, `simple_back_sword_t2` | t0-t1 | bron 1H tej kultury t1 (buzdygan), `peasant_hatchet_1_t1`, `peasant_pickaxe_1_t1` - miecza t1 w grze nie ma | rekruci ok. 40 kultur/rodow |
| oszczepy `eastern/western/northern/generic_javelin_*` (t4-t5) | t2-t3 | topor do rzucania t1-t2 (`northern/western_throwing_axe_1_t1`, `highland_throwing_axe_1_t2`), wyjatkowo `throwing_stone` | Dorne (Footman, Spearman, Yronwood), Sarnor Javelinier, Smocza Skala Squire, piraci i inni (`sprzet.md` 4.3) |
| proce `sling_wool/braided/reinforced` (wszystkie t6) | t2-t5 | `throwing_stone` (Thrown/Stone, t0); **ich `SlingStones` w innych slotach zestawu zastapic drugim stosem `throwing_stone`** (bez procy kamienie sa martwe) | jawwal, guardians, mercenary_2/4/5/7/8, desert_bandits_bandit |
Usuwanie broni dystansowej bez zamiennika nie wchodzi w gre: jednostka zostaje w formacji strzelcow (`IsRanged` z `default_group`, BasicCharacterObject.cs:495).
**Koszt dla lore (wpisany Jeffowi w rozdz. 0 pkt 10):** oszczepy sa w grze tylko t4-t5, proce tylko t6 (items-dump: Javelin 6 x t4, 12 x t5; `sling_*` t6), miecza t1 nie ma.
Dornijczycy t2-t3 (oszczep to ich znak) dostaja topor do rzucania, procarze kamienie, rekruci ok. 40 kultur topor/kilof. Regula wyjatku "gdy brak przedmiotu tego
rodzaju w tierze <= T, zostaje najnizszy istniejacy tego rodzaju" (oszczep t4 u t3) - NIE wbudowana, bo lamie decyzje 8b doslownie; to jedna linia w kodzie, jesli Jeff zechce.

### 1.5 Przypadki specjalne
- Szablony (`co.IsTemplate`: 104 BK gentry z occupation Lord, 44 `bannerkings_bandithero_*`, `tournament_template_*`, "Special") - pomijamy. Bohaterowie - pomijamy.
- Wiesniacy, mieszczanie, straznicy miast, kupcy (occupation spoza 5 zolnierskich) - pomijamy (to nie wojsko; `SkillSinew` i tak chodzi po nich osobno).
- Rasa `giant` (4 jednostki) - pomijamy calkiem (zamiennik bylby ludzka zbroja; Armoury `GiantGear` pilnuje ich sprzetu). Rasa `wight` (12) - tylko bron, pancerza nie.
  Rasa z `FaceGen.GetRaceNames()[co.Race]`.
- Konie i ladry - poza zakresem (68 jednostek z koniem albo ladrami ponad tier zostaje: konie 27, ladry 45 jednostek). **Swiadomy wyjatek od 8b (rozdz. 0 pkt 10):**
  po 175 mechanizm "sprzet ponad tier podnosi umiejetnosc" zostaje tylko u 3 jednostek: `skagosi_rider` t4 Jazda 100 -> 140 (jednorozec `unicorn3` t5 - jednorozce
  sa w grze tylko t5-t6, a kon to wlasnosc zolnierza, decyzje 30.08/160), `battanian_horseman` t5 Atletyka 80 -> 120 i `karstark_shock_cavalry` t5 110 -> 120
  (ladry `rot_horse_mail3` t6). Ladry rodow Westeros (`rot_horse_armor*`, `rot_horse_mail*`) sa wszystkie t6 (32 sztuki; w t5 sa tylko ladry kultur Essos (12)
  i 2 ogolne) - zamiana zdarlaby barwy rodow 45 jednostkom (43 rycerzy t5), a umiejetnosc wiaze tylko u tych 2 (u reszty Atletyke i tak ustala korpus t5). Uwaga poboczna bez zmian: `ReqSkill`
  liczy ladry jako pancerz jezdzca -> Atletyka - osobne zgloszenie (poza 175).
- 15 nieistniejacych id w zestawach ROT (`kg_gloves`, `westerland_pauldron`, ... i 31 pustych) - gra ich nie wczytuje; latka je pomija (nie naprawia).

### 1.6 Lzej / ciezej wedlug lore, w granicy tieru (decyzja 4a + audyt 4.9, 4.11) - ta sama maszyna, inny sufit
Po zamianie z 1.3 dla wymienionych jednostek drugi przebieg z innym sufitem tieru (ta sama kaskada i te same reguly "scisle"):
| Kto | Sloty | Sufit | Skutek [S, `lekkie.py`] |
|---|---|---|---|
| **Pentos** - wszystkie jednostki t2-t4 (`pentoshi_footman`, `_noble_recruit`, `_militia_archer`, `_militia_spearman`, `_soldier`, `_pikeman`, `_militia_veteran_*`, `_archer`, `_horseman`, `_man_at_arms`, `_pike_warrior`) | caly pancerz | T-1 | pancerz wzorca np. Pikeman t3 244 -> 166, Pike Warrior t4 360 -> 271; piechota Pentos 100 -> 89 |
| **Qarth** `qartheen_hoplite` (t4) | korpus | T-1 | `qarth_armor` t4 -> `eastern_stitched_leather_coat` t3 (pancerz 332 -> 300); `qartheen_elite_hoplite` t5 juz nosi korpus t4 - bez zmiany |
| **Dorne** piechota wsi t3-t4: `aserai_mameluke_axeman` (Footman), `aserai_mameluke_regular` (Spearman), `aserai_mameluke_guard` (Soldier) | korpus | T-1 | lekka piechota z ksiazek; Dayne, Yronwood i szlachta (Glaiveman, Elite Spearman) bez zmian - ciezkie zbroje tylko w gorach [L: ASOS Tyrion V] |
| **Qohor** piechota t4-t5 ciezsza: `qohorik_goat_warrior` t4 -> najciezszy korpus Qohoru t4 (`qohorik_armor3`); `qohorik_elite_spearman`, `qohorik_falxman`, `qohorik_goat_devout` t5 -> najciezszy korpus t5 z kaskady (Qohor nie ma wlasnego t5 - poziom 3, strona Essos) | korpus | T (najciezszy w tierze, nie najwyzszy wymog) | miasto platnerzy [L: TWOIAF Qohor]; Atletyka urosnie przez `SkillSinew` do wymogu nowego korpusu (t5 = 140) - zgodnie z zasada |
| ~~Lorath `nord_huscarl`~~ | - | - | **NIE robimy**: to t6 i jedyny korpus t5 z kaskady (`nord_king_armor`) jest CIEZSZY (497 -> 504) - krok 2 dalby odwrotny skutek; piechota Lorath po samej zamianie 108 -> 99 |
Selekcja Qohoru/Dorne po id (lista wyzej), nie po regule ogolnej - to swiadome wyjatki lore, wpisane w kodzie jedna tabela z komentarzem.
**Wylaczniki (wersja 2):** Pentos i Qarth - `Army175LoreArmor` (dom. TAK, decyzja 4). Dorne i Qohor - osobny klucz `Army175LoreArmorExtra` (dom. **NIE**): audyt 4.9 i 4.11
nie byly pytaniem do Jeffa ani jego decyzja (rozdz. 0 pkt 9). Wiersz 1.8 "Dorne po 1.6" i "+ 1.6 Qohor" dotyczy stanu po wlaczeniu.

### 1.7 Kolejnosc w kodzie (WAZNE - tu jest pulapka TroopFit/ColdStart)
**Problem:** Armoury biegnie w `OnSessionLaunched` PRZED lancuchem Mends (log 09.10: Armoury 02:39:58, Mends 02:39:59; kolejnosc sluchaczy nie jest gwarantowana).
Tam `LegendaryLaw.OnSession` -> `TroopFit.Run()` (LegendaryLaw.cs:72-84, TroopFit.cs:20-88; `TroopSkillAutoFit` u Jeffa `true`, Armoury.json:158) podnosi
umiejetnosci do trudnosci z XML na tym, co jest we wzorcach, a `ColdStart.Run()` (ArmouryBehavior.cs:999, nowa gra) wypelnia zbrojownie startowe wedlug wzorcow.
Zamiana w lancuchu Mends przyszlaby za pozno: 98 jednostek zachowaloby podbite umiejetnosci (np. Pentoshi Footman Atletyka 100 zamiast 40), a startowe
zbrojownie bylyby ze starych wzorcow.

**Rozwiazanie - zamiana PRZED cala sesja, w CrashScribe `SubModuleMain.AfterRegisterSubModuleObjects(bool)`** (MBSubModuleBase :76, wolane z SandBoxManager.cs:340-347):
to pierwszy moment, gdy sa wczytane przedmioty, `EquipmentRosters`, `partyTemplates` (Campaign.cs:1466-1474) i `NPCCharacters` (SandBoxManager.cs:360-383,
po XSLT ROT), a jeszcze przed SyncData zapisu, przed tworzeniem partii i zalog nowej gry i przed jakimkolwiek `OnSessionLaunched`. Ustawienia Armoury
(wylaczniki czytane przez `Mends.ArmouryFloat`) sa juz wczytane (`McmSettings.Apply()` w Armoury `OnGameStart`, Campaign.cs:1391). Obiekty jednostek sa nowe
w kazdej grze (`MBObjectManager.Init()`/`Destroy()`), wiec latka nie kumuluje sie miedzy wczytaniami.

**A co z `ArmorSanity`/`AmmoSanity`** (zmieniaja tier przycietych sztuk; rozpoznanie chcialo zamiany PO nich)? Zostaja tam, gdzie sa (`OnSessionLaunched`).
Ich przeniesienie wczesniej zmieniloby ceny: `HistoricalPrices.Apply` (Armoury, OnSessionLaunched) liczy cene z tieru (HistoricalPrices.cs:271) i dzis widzi
tier PRZED rozsadkiem - 57 pancerzy i 1 pocisk dostalyby nowe ceny (regresja ekonomii), a `ArmorSanity` nie jest idempotentny (przyciety przedmiot moze spasc
do nizszej grupy tieru i zostac przyciety drugi raz). Rozsadek tylko OBNIZA ochrone/obrazenia, wiec tier po nim jest <= tier przed nim. Zamiana liczona na tierze
sprzed rozsadku jest wiec bezpieczna: nic nie zostanie ponad tier; koszt - co najwyzej kilkadziesiat slotow zamienionych niepotrzebnie (57 przycietych sztuk,
glownie ubrania t0-t1 i kilka zbroi t6). Kontrola (nizej) to policzy.

**Lancuch po zmianie:**
1. `AfterRegisterSubModuleObjects` (CS, nowy `Army175.OnObjectsRegistered`): [a] sklad - szablony i `upgrade_targets` (rozdz. 2); [a2] **migawka zasady
   valyrianskiej**: `BestWeaponTier` kazdej jednostki zolnierskiej ze wzorcow SPRZED zamiany (slownik `CharacterObject -> tier`, tylko >= 6; dzis ok. 223) - czyta
   ja `ValyrianWardSim` zamiast wzorca na zywo, zeby 175 nie zmienialo walki z Innymi (1.9); [b] `TierGear` (1.3-1.5); [c] `LoreArmor` (1.6); [d] `GoldenBows`
   tylko gdy wlaczone (2.5, domyslnie wylaczone). Kazdy krok w osobnym `try`, wyjatek na jednostce = licznik potkniec, nie wylaczenie kroku (CLAUDE.md 7).
   Ustawia `TierGearApplied = (widzianych zestawow bojowych > 0)`.
2. `OnSessionLaunched` (CS, Mends.cs:4693, kolejnosc): `ArmorSanity` -> `AmmoSanity` -> **`Army175.TierGearCheck()`** (liczy sloty ponad tier po rozsadku -
   ma byc 0; gdy nie - drugi, idempotentny przebieg `TierGear` i linia OSTRZEZENIE; gdy `!TierGearApplied` - pelny `TierGear` + `LoreArmor`) -> `WeightLaw` ->
   `ArmorTierLaw` -> `WeaponTierLaw` -> `SkillSinew` -> **`Army175.NorthHardy()`** (3.2) -> **`Army175.DothrakiRiders()`** (2.1) -> reszta jak dzis.
3. `DailyTick` (Mends.cs:4698-4699): `DragonPurge(false); if (!Army175.TierGearApplied) Army175.TierGear(); if (!SinewApplied) SkillSinew(); if (SinewApplied) { Army175.NorthHardy(); Army175.DothrakiRiders(); }`
   (oba ze znacznikiem na OBIEKT UMIEJETNOSCI `GetDefaultCharacterSkills()`, nie na postaci - 3.2; powtorzenie nic nie robi). Dzis log nowej kampanii pokazuje `SkillSinew` dzialajacy juz przy starcie sesji
   (2536 jednostek, [P] session log :1439), ale sciezka "zestawy jeszcze puste" zostaje jako zabezpieczenie.
4. Armoury `TroopFit.Run` - **bez zmian w kodzie**: biegnie po zamianie (wzorce juz nowe), podnosi do trudnosci z XML, ktore sa <= trudnosci po prawach,
   wiec `SkillSinew` i tak ustala wynik; kolejnosc TroopFit wobec NorthHardy nie ma znaczenia (oba tylko w gore, NorthHardy liczy od `max(obecna, wymog)`).

### 1.8 Skutek dla umiejetnosci i Sily (przeliczone skryptami audytu 14, `SCR\a175\narz\sila175.py`)
Umiejetnosci (`SkillSinew` na nowych wzorcach = max(XML, wymog wlasnego sprzetu)) [S z danych]: 1H spada u 406 jednostek (srednio -44), Drzewce 265 (-41),
Kusza 41 (-71), Rzut 51 (-47), Luk 22 (-39), Atletyka 657 (-44). Jednostek z umiejetnoscia ponad XML ROT: 986 -> 553. Piechota t3+ swiata (318 jednostek AI):
bron glowna / Atletyka 151/156 -> 123/122 (XML ROT: 122/119).

**Sila wzorca t3+ (100 = srednia swiata danego wariantu; wskaznik audytu 14: bron noszona + 0.5 Atl + 0.4 pancerza + 20 za tarcze dla piechoty itd.;
wagi = ludzie w partiach rodow z doby 120 [P]; P/S/J/KL):**
| Krolestwo | Dzis w grze | Wedlug XML ROT | Po zamianie (stare szablony) | Cala 175 (zamiana + 1.6 + sklad + Polnoc +25) |
|---|---|---|---|---|
| Pentos | P 125 / S 104 / J 110 / KL 102 | 108 / 106 / 104 / 102 | 100 / 101 / 102 / 103 | **89** / 97 / 100 / 103 |
| Qarth | P 115 / S 101 / J 112 | 104 / 102 / 99 | 101 / 99 / 95 | **98** / 100 / 96 |
| Braavos | P 113 / S 91 / J 104 / KL 96 | 100 / 96 / 94 / 96 | 100 / 99 / 96 / 93 | **99** / 99 / 96 / 93 |
| Aegon (Zlota Kompania) | P 115 / S 102 / J 105 | 103 / 98 / 102 | 103 / 99 / 101 | **102** / 99 / 101 |
| Tyrosh | P 108 / S 98 / J 106 | 100 / 97 / 98 | 94 / 98 / 100 | **92** / 98 / 100 |
| Sarnor | P 106 / S 102 / J 102 | 96 / 103 / 97 | 97 / 100 / 98 | **95** / 100 / 98 |
| Polnoc | P 98 / S 102 / J 96 / KL 106 | 99 / 101 / 96 / 106 | 98 / 100 / 98 / 105 | **111** / 100 / 98 / 105 |
| Dothrakowie | P 75 / S 95 / J 73 / KL 95 | 89 / 96 / 87 / 95 | 96 / 99 / 93 / 97 | J **95** / KL **97** (bez piechoty i strzelcow t3+); z +30 Jazdy (2.1, wersja 2): J **104** / KL **105** |
| Nocna Straz | P 74 / S 91 / J 82 | 86 / 88 / 85 | 93 / 91 / 88 | **91** / 91 / 88 |
| (Lorath) | P 108 / S 111 / J 107 | 100 / 107 / 101 | 100 / 107 / 105 | 99 / 107 / 105 |
| (Qohor) | P 104 / S 107 / J 110 | 92 / 105 / 101 | 94 / 102 / 102 | 93 / 102 / 102 (+ 1.6 Qohor ok. +2-3 [S], nie liczone) |
**Rozrzut (84 wartosci krolestwo x rodzaj, bez Skagos, Yi Ti, Wysp Letnich):** dzis 70 w 90-110 (skrajne 73-125); XML ROT 77 (85-109); po samej zamianie 83 (88-107);
cala 175: 78 z 82 (88-111) - poza 90-110 zostaja Pentos P 89 (celowo, decyzja 4a) i Polnoc P 111 (celowo, decyzja 1C), Skagos J 111, Dothrakowie nic.
Dorne po 1.6: piechota ok. 101 -> 99 [S, nie liczone wprost]. Pelna tabela 29 krolestw: wydruk skryptu, plik `SCR\a175\sila175.json`.
Autobitwa tego NIE widzi (moc = tier) - zmiana dziala w bitwach z graczem i w doborze sprzetu (DTE, AI zakupy).
Wersja 2: kolumna "cala 175" liczy sklad 175 dla wszystkich szablonow z 2.1-2.5; przy domyslnych wylacznikach (Volantis/Norvos, Wolni Ludzie, Smocza Skala, Dorne/Qohor
- NIE) te krolestwa maja wartosci z kolumny "po zamianie"; Pentos, Qarth, Polnoc, Dothrakowie, Wyspy - jak w tabeli.

### 1.9 Punkty styku
- **DTE (bitwy z graczem):** wzorzec = `RandomBattleEquipment` (Assignment.cs:327) - dlatego zamiana obejmuje WSZYSTKIE zestawy. **Korekta do rozpoznania i do K1:**
  sufitu "tier wzorca + 2" DTE w tej grze NIE ma - Armoury `SkillsDecide.NoTierCap` (prefiks na DTE `GetMaxAllowedTier`, SkillsDecide.cs:50, :60-64;
  `SkillsDecideEnabled` u Jeffa `true`, Armoury.json:157) zwraca 6, a bron w referencji DTE to "najlepsza bron, ktorej wymog jednostka spelnia" wedlug jej
  umiejetnosci (`RearmBySkill`, SkillsDecide.cs:68-130). Wiec w bitwie z graczem sprzet ogranicza
  UMIEJETNOSC, a ta po 175 wraca do poziomu XML: t3 z 1H 80 dostanie z polki bron do wymogu 70 (t3), t4 z 110 - do 105 (t4). To jest wlasciwy hamulec - wymogi
  zostaja (decyzja 02:50). Stare t5-t6 na polkach wezma tylko ci, ktorzy je udzwigna (`SuitableWard`/`SkillLawWard`, Mends.cs:1071, :1171).
  `MarkUnderEquipped` (PartyEquipmentDistributor.cs:1905-1935) porownuje z nizszym wzorcem - mniej kar morale "underequipped".
- **171 (dobytek rekruta, `RecruitKit.OwnOf`, nie w tej bazie):** `OwnOf` = czesc wspolna 1. zestawu przodka t1 i oddzialu - po zamianie przodek i oddzial dostaja
  rozne zamienniki, wiec dobytek spada 366 -> 239 sztuk (wartosc -58%), 81 linii traci go calkiem [S, `ownof.py`]. Decyzja nalezy do 171 (nie do 175):
  (a) zostawic albo (b) dobytek po RODZAJU (ItemType + klasa) zamiast po id. `OwnOf` czyta wzorce w biegu - nic do zmiany w 175.
  Latka szablonow i drzew (rozdz. 2) jest przed pierwszym `RecruitKit.BuildParents`/`Tier1Root` (n174 RecruitKit.cs:142-188) - warunek spelniony.
- **Stare zbrojownie na zapisie (krytyka, sprawdzone w kodzie):** `AiGear` liczy zapas wyzszego tieru tego samego typu jako pokrycie braku (wpis 89, AiGear.cs:221-241),
  a `MenPurse.SellAiSurplus` liczy nadwyzke po TYPIE i sprzedaje najpierw NAJNIZSZE tiery (MenPurse.cs:336-355). Na zapisie Jeffa zbrojownie AI sa pelne t5-t6
  kupionych pod napompowane wzorce; po 175 ludzie ich nie udzwigna (`SkillLawWard` "bez podlogi" - slot PUSTY, Mends.cs:1152-1170; `DressCode` dopelnia tylko
  pancerz, nie tarcze/amunicje/bron). AI uzna potrzeby za pokryte, nie dokupi t2-t4, a MenPurse sprzeda wlasnie te uzyteczne. To samo u gracza: jego ludzie t2-t5
  pojda bez tarcz/strzal, jesli w zbrojowni sa tylko t5-t6. **Rozwiazanie jest w K1** (spec. K1 A-1 :95 "sztuka trafia do koszyka tylko, gdy ktos z koszyka ja
  udzwignie"; A9 :125-129 "najpierw sprzedaje sztuki, ktorych nikt nie udzwignie"). **Kolejnosc wgrania: 175.2 na zapis Jeffa razem z K1 albo po nim.** Jesli
  175 ma isc pierwsze - most 175.1c w Armoury (ten sam warunek co K1 A-1 w `AiGear` i A9 w `MenPurse.SellAiSurplus`, do usuniecia przy scaleniu z K1); nie
  budowany domyslnie. T2 liczy skale (6.1 pkt 9).
- **K1 (dozbrajanie):** koszyk = typ x **tier sztuki wzorca** (`AiGear.NeedBuckets`, AiGear.cs:136-168; K1 spec :43, :99 "tier nie wyzszy niz tier koszyka").
  Po zamianie koszyk <= tier jednostki - zgodnie z decyzja 8b. Ale w 158 parach zamiennik jest o 1 tier nizej, w 13 nizej o 2-5 - wtedy K1 nigdy nie kupi sztuki
  w tierze jednostki. **Do K1:** sufit zakupu/wymiany = tier JEDNOSTKI (nie tier koszyka); oraz poprawic zdanie spec :337 o "suficie tieru +2" (patrz wyzej).
  Do K1 takze: koszyk liczony ze WSZYSTKICH zestawow bojowych z waga 1/n (warunek dla Zlotej Kompanii, 2.5).
- **174 (produkcja, n174):** `AiGear.Deficit` i szczebel broni (`ArmsDrill`, awanse AI) licza tier SZTUKI WZORCA (AiGear.cs:278-, n174 :47-51, :87) - po zamianie
  szczeble spadaja, wiec "bron szczebla" latwiej o reke i hamulec awansow AI 171/174 puszcza czesciej (do zmierzenia po scaleniu). Popyt koszykow przesuwa sie
  z t5-t6 na t2-t4 - inny rozklad zamowien warsztatow. Nadwyzki MenPurse liczone po typie (n174 MenPurse.cs:314-360) - bez jednorazowej wyprzedazy po wczytaniu.
- **Z1 (XP za dary):** brak styku w kodzie (Z1 zamyka XP za oddany sprzet - wartosc lupu ze wzorca nie ma juz znaczenia dla XP).
- **Ekonomia [S, `wartosc.py`]:** wartosc kompletu wzorca (nowa moneta, bez koni): t1 57 -> 39 (-32%), t2 253 -> 122 (-52%), t3 649 -> 327 (-50%), t4 1389 -> 754 (-46%),
  t5 1979 -> 1306 (-34%), t6 bez zmian. Tansze: zakupy AI (koszyki), `VolunteerKit` (notabl dokupuje roznice wzorca), komplet startowy gracza (DTE `OnTroopRecruited`),
  lupy z poleglych i jencow (Armoury bierze je ze wzorca, ArmouryBehavior.cs:860-875, :1780-1792). Lordowie AI wydadza mniej na sprzet - wiecej zlota na zold
  i werbunek. Mierzymy w tescie (6.2: budzety rodow, ludzie w partiach).
- **Inni (zasada valyrianska w autobitwie):** `ValyrianWardSim` (Mends.cs:1755-1767) tnie cios w Wedrowca do 15%, gdy bijacy nie ma we WZORCU broni t6
  (`BestWeaponTier`, Mends.cs:1721-1743 - liczy tez amunicje). Dzis 223 jednostki zolnierskie maja bron/amunicje t6 we wzorcu, po samej zamianie byloby 27 (same t6);
  Nocna Straz 3 -> 0 (kusznicy z `bolt_a`), Polnoc 30 -> 5 [S]. **Wersja 2: neutralnie** - `ValyrianWardSim` czyta tier z migawki sprzed zamiany (1.7 krok [a2]),
  wiec walka z Innymi zostaje dokladnie jak dzis. Pierwsza wersja nazywala zmiane "zgodna z t6 tylko dla t6" - to rozciagalo zasade o strzalach z 02.09 na inna
  zasade (krytyka slusznie). Prawdziwa regula (smocze szklo, bron t6 w reku ze zbrojowni) - osobno, do decyzji Jeffa przed doba 728.
- **Z16 (wymog Atletyki u bohaterow, po 175):** te same miejsca w Mends.cs - robic na galeziach 175 (STAN-PRAC :800).

### 1.10 Log (CrashScribe, jedna linia + szczegoly)
`Mends: sprzet wedlug tieru (175) - jednostek N z M zolnierskich, slotow S (bron a, tarcze b, amunicja c, pancerz d); zamiennik scisly x, luzny y, zapas innej klasy z
(proce p, oszczepy o, miecze rekrutow m); nowe rostery r (= jednostek ze zmiana); odrzuceni kandydaci: siodlo a1, couch/brace a2, plec a3, bandyta a4, cywilne a5;
kultur Essos 19; migawka valyrianska v; pominiete: szablony t, giganci g, umarli-pancerz w, bohaterowie 0; potkniecia k.` + linia "najwieksze skoki"
(15 par przedmiot t -> zamiennik t, jednostka) + linia `Mends: sprzet wedlug tieru (175) - lore w granicy tieru: Pentos n1, Qarth n2, Dorne n3, Qohor n4 slotow.`
+ w OnSessionLaunched: `Mends: sprzet wedlug tieru (175) - kontrola po rozsadku: slotow ponad tier 0 (zamienionych na zapas, choc po rozsadku by sie miescily: q).`

---

## 2. Sklad wojsk - 175.3 (szablony partii i drzewa awansow)

### 2.0 Jak
- **Gdzie:** CrashScribe, `Army175.Composition()` w `AfterRegisterSubModuleObjects` (krok [a] z 1.7). `PartyTemplateObject.Stacks` to publiczna
  `MBList<PartyTemplateStack>` (PartyTemplateObject.cs:12), `PartyTemplateStack` - struktura z publicznymi polami (PartyTemplateStack.cs:5-29):
  `tpl.Stacks[i] = new PartyTemplateStack(ch, n, n)`, `RemoveAt`, `Add`. `CharacterObject.UpgradeTargets` ma prywatny setter (CharacterObject.cs:314) -
  `AccessTools.PropertySetter(typeof(CharacterObject), "UpgradeTargets")`, nowa tablica. Wymog konia nalezy do CELU (`UpgradeRequiresItemFromCategory`)
  i Stables liczy go z `IsMounted` celu (Stables.cs:282, :327) - zmiana drzew nie wymaga zmian wymogow.
- **Wartosci bezwzgledne** (stos = N, min = max = N; cele = tablica) - idempotentne. Brak id -> linia logu i pominiecie tej jednej zmiany (nie wyjatek).
- **Kto czyta stosy** (wszyscy PO latce): partia lorda przy (re)spawnie (LordPartyComponent.cs:38-39), partie startowe nowej gry (HeroSpawnCampaignBehavior.cs:263-277),
  zalogi startowe (szablon kultury, GarrisonTroopsCampaignBehavior.cs:211-235), sredni zold gry (Campaign.cs:1208-1227), ROT pula/cel (ROTTroopRecruiter.cs:470-521,
  pamiec na bohatera/osade budowana leniwie), szansa awansu ROT (ROTTroopUpgradeModel.cs:61-110), Armoury `HouseLevies.TreeOf`, `Stables.BuildRiderMap`
  (ArmouryBehavior.cs:1002), 171 `RecruitKit.Tier1Root`.
- **Wylaczniki skladu dzialaja od nastepnego wczytania** (ROT trzyma pule do konca sesji) - napisac to w opisie MCM.
- Log: `Mends: sklad 175 - <szablon>: cel P/S/J/KL a -> b, ludzi x -> y` (kazdy szablon) + `Mends: sklad 175 - drzewa: khuzait_nomad [..] -> [..]; ...`.
- Liczby nizej: `SCR\a175\sklad-wyliczenie.txt` (cel = stosy t3+ wedlug `default_group`, jak ROT `GetTemplateComposition`; swieza partia = wszystkie stosy)
  i `SCR\a175\narz\zold175.py` (zold na czlowieka: stawka gry 1/2/3/5/8/12/17 wedlug tieru, konny x1.5 `MountedWage`).

### 2.1 Dothrakowie konni (4.1, decyzja 3) - wylacznik `Army175DothrakiRide`
- `kingdom_hero_party_khuzait_template` (XSLT :127; rody kultury `khuzait` bez wlasnego szablonu: clan_khuzait_1/4/6/8, ROTclan_28, ROTclan_120 + rody tworzone w grze):
  - usunac stosy: `khuzait_footman` t2 (8), `khuzait_spearman` t3 (4), `khuzait_spear_infantry` t4 (2), `khuzait_darkhan` t5 (1), `khuzait_hunter` t3 (8),
    `khuzait_archer` t4 (4-5), `khuzait_marksman` t5 (1);
  - ustawic: `khuzait_tribal_warrior` t2 4 -> 8, `khuzait_raider` t3 2-3 -> 6, `khuzait_horseman` t3 2 -> 3, `khuzait_horse_archer` t4 1-2 -> 4; zostaja `khuzait_nomad` t1 16
    (pieszy rekrut - bez niego ROT szukalby tieru wyzej i lord placilby roznice, moc w autobitwie skoczylaby o 45%) i `khuzait_khans_guard` t6 1;
  - dodac: `khuzait_lancer` t4 (2), `khuzait_heavy_horse_archer` t5 (2), `khuzait_heavy_lancer` t5 (1);
  - **drzewo:** `khuzait_nomad` [tribal_warrior, footman] -> [`khuzait_tribal_warrior`] (XSLT spnpccharacters :2334, :2348-2351).
- Cel P/S/J/KL 24/48/7/21 -> **0/0/32/68**; swieza partia 54/25/4/18 -> 37/0/14/49; ludzi w stosach 57 -> 43 (partia odradzajaca sie w grze startuje mniejsza -
  zostawiam: mniej ludzi "z niczego" przy respawnie i mniejszy rachunek zoldu biednych rodow; nowa gra liczy wagi do limitu, wiec jej nie dotyczy).
- **Zold [S]:** swieza partia 5.28 -> 6.67 zl na czlowieka (+26%), cel t3+ 7.83 -> 11.53 (+47%) (audyt: rozwinieta +49%).
- **Prognoza kasy Dothrakow [S] (wersja 2):** baza [P, `budzet-rodow.csv` kopiaT9-120]: partie 13.7 tys. zl/dobe, zalogi 7.1 tys., saldo modelu -19.3 tys./dobe (d119);
  kiesy glow 1.71 mln (d0) -> 0.99 (d40) -> 1.07 (d80) -> 0.62 mln (d119), 1 bankrut, 3 glowy < 5 000. 175 doklada: zold partii +3.6-6.4 tys./dobe; zalogi do
  +1.8-3.2 tys./dobe (gorna granica - gdy zalogi dojda do celu); konie na awanse nomadow (Stajnia AI kupuje, dopoki budzet z kiesy >= 50 zl, Stables.cs:393-394;
  konie w regionie Dothrakow 620-1 260 zl, srednia AI 463 zl, `zakupy.log`) ok. 600-1 000 koni = 0.4-0.6 mln. **Razem ok. 0.8-1.6 mln w 120 dob wobec 0.62 mln
  w kiesach** - D2 (6.2) prawie na pewno zadziala; wylacznik po tescie to najbardziej prawdopodobny wynik decyzji 3 (Jeff wie z rozdz. 0 pkt 7). Czesc kosztu
  konia przejmuja notable (VolunteerKit, nizej). Pomysl na pozniej (NIE w 175, nie pytanie teraz): w lore khalowie nie biora zoldu - zyja z lupu i z "darow"
  Wolnych Miast (TWOIAF: Pentos placi khalom) - trybut z kas Wolnych Miast dla khalow albo zold konnych z udzialu w lupie.
- **ROT przerabia ludzi na konnych - trzy drogi (wersja 2, sprawdzone w ROTTroopRecruiter.cs):** ROT zamienia KAZDEGO zolnierza spoza puli rodu (co dobe
  `DailyTickParty`/`DailyTickSettlement`, po lupach `OnDistributeLootToParty`, przy werbunku `OnTroopRecruited`; :33-126) na czlowieka z puli tego samego tieru
  i BRAKUJACEJ formacji (`ShouldRecruit`, `DetermineReplacement`, :152-252). Po 2.1 brakuje tylko J i KL, wiec:
  (a) **starzy piesi Dothrakowie** - bez dopisku do puli staliby sie w jedna dobe konnymi bez koni (x1.5 zoldu, +30% konnych lucznikow na otwartym, wyzsze C w H3) -
      dopisek pieszych do puli (nizej, OBOWIAZKOWY);
  (b) **obcy w partii Dothrakow** (zwerbowani jency, najemnicy z karczm, ochotnicy innych kultur t2+) - dzis ok. 28% takich zamian daje konnego, po 175 prawie 100% -
      **straz konia przy zamianie ROT** (175.1b, nizej);
  (c) **start i odrodzenie** - partia odradzajaca sie ze stosow ma 63% konnych (dzis 22%), zalogi startowe NOWEJ gry ze stosow szablonu kultury ok. 80% konnych
      (dzis ok. 28%; `FillGarrisonPartyOnNewGame` daje wage 6 kazdemu `IsRanged`, a konny lucznik jest `IsRanged`, GarrisonTroopsCampaignBehavior.cs:211-235),
      a `ColdStart` koni nie daje (sloty 0-9). To ogolna regula gry (ludzie przy starcie i odrodzeniu biora sie z niczego) - poza 175; mierzymy (konni bez konia,
      zold zalog Dothrakow w d1 i d40 wobec B0). Zapisu Jeffa to nie dotyczy (zalogi juz sa).
  Pierwsza wersja pisala, ze dopisek usuwa "konia z niczego" - nie usuwal; droge (b) zamyka dopiero straz konia, (c) zostaje jak u wszystkich krolestw.
- **Dopisek pieszych do puli ROT (OBOWIAZKOWY):** postfiks CS na `ROT.CampaignBehaviors.ROTTroopRecruiter.Settings(Hero, Settlement)` (internal, refleksja po nazwie
  typu w zestawie ROT; ROTdec :470-521). **Warunek po ZAWARTOSCI wyniku (wersja 2):** `__result?.PartyTemplate` zawiera `khuzait_nomad` (korzen drzewa wsi Dothrakow,
  siedzi tylko w szablonie kultury; NIE "tribal_warrior" - ta jednostka jest tez w pulach Daenerys i Joraha) -> dopisz brakujace z 7 pieszych. Powod: ROT pamieta
  wynik dla osady od pierwszego wywolania i nie czysci go przy zmianie wlasciciela (:475-520; czyszczenie tylko w ROTOthersCampaignBehavior), wiec warunek wedlug
  `settlement.OwnerClan.DefaultPartyTemplate` w chwili wywolania moglby nie pasowac do zapamietanej puli. `PartySettings.PartyTemplate` to publiczna lista -
  dopisek idempotentny (`Contains`); `Composition` bez zmian. Skutek: starzy piesi zostaja; `spearman`, `hunter`, `archer` awansuja dalej swoja linia, ale
  **`khuzait_footman` t2 stoi na zawsze** (ROTTroopUpgradeModel.cs:87-109: oba jego cele maja inny zbior formacji szczytowych niz on, a sklad nie potrzebuje P ani S -
  szansa 0); footmanow ubywa tylko przez straty (licznik w 4.2). Rzadka sciezka awaryjna ROT moze wylosowac pieszego z dopisanej puli - akceptowalne.
  Log: `Mends: sklad 175 - Dothrakowie: piesi w puli ROT (7), cel 0/0/32/68.`
- **Straz konia przy zamianie ROT - 175.1b, Armoury, wylacznik `Army175DothrakiHorseGuard` (dom. TAK):** ten sam hak co pomiar (prefiks + postfiks na
  `ROTTroopRecruiter.ExchangeClanTroops`, 4.2), tylko gdy pula rodu zawiera `khuzait_nomad`. Postfiks liczy, ilu konnych `m` przybylo w tej zamianie (roznica rosteru),
  i dla kazdego bierze konia: najpierw wolnego ze zbrojowni (4.1: konie w zbrojowni minus konni w partii - kon zostaje na miejscu), potem z taboru
  (`Stables.Consume` -> `AiGear.AddToArmory`, jak przy awansie). Dla brakujacych `s` zamienia `s` dodanych konnych na pieszego TEGO SAMEGO tieru z dopisanej puli
  (t1 `khuzait_nomad`, t2 `khuzait_footman`, t3 `spearman`/`hunter`, t4 `spear_infantry`/`archer`, t5 `darkhan`/`marksman`; t6 - pieszego brak, zostaje i idzie
  do licznika). Ten sam tier, wiec ROT nie przelicza zlota. To zasada z 30.08 ("na konnego tylko z koniem") zastosowana do zamian ROT tam, gdzie 175 robi z niej
  problem; dla innych krolestw NIE w 175 (dzis ok. 28% zamian daje tam konnego bez konia) - najpierw pomiar `rot_plus` wedlug zrodla (4.2), potem propozycja
  jednej zasady dla wszystkich. **Styk z 171:** w tej bazie echo werbunku ROT (`OnTroopRecruited` z `recruitmentSource == null`) daje zastepcy przez
  `RecruitKit.OnRecruited` pelny komplet wzorca Z KONIEM (sciezka "bez zapisu", `TemplateItems`, sloty 0-11) - kon z niczego, ktory straz uzna za wolny; 171 to
  zamyka ("echo ROT - nic", n174 RecruitKit.cs:23, :317) - po scaleniu straz widzi juz tylko prawdziwe konie.
- **Ochotnicy u notabli (wersja 2):** gra sama awansuje ochotnikow u notabli losowo po `UpgradeTargets` (RecruitmentCampaignBehavior.cs:241-246) - po zmianie drzewa
  kazdy awansowany ochotnik Dothrakow to konny lucznik t2. `VolunteerKit` kaze notablowi kupic mu na targu konia i rzad (czesci KLUCZOWE, VolunteerKit.cs:137-146);
  bez konia awans jest COFNIETY (:81) - konia z niczego tu nie ma. Przy `RecruitsOwnHorse` = TAK (domyslne; w Armoury.json Jeffa nieustawione) lord nie placi
  przy werbunku za konia, tylko wyzszy zold (x1.5). Skutek: lordowie Dothrakow wezma gotowych konnych t2 od notabli - obraz "partie zastygle w t1" (D4) moze sie nie
  spelnic, a popyt na konie przesuwa sie czesciowo na notabli i targi Dothrakow. Licznik w 4.2.
- Rekruci `khuzait_nomad` w partii bez konia w taborze i zbrojowni CZEKAJA (FilterTargets usuwa jedyny cel). Zalogi Dothrakow: rekruci t1 w zalogach bez koni nie
  awansuja - zalogi z czasem "mlodnieja" (lore: Dothrakowie nie trzymaja miast) - mierzymy sredni tier zalog i twierdze stracone (6.2 B4, B6).
- **+30 Jazdy linii konnej - audyt 4.1 (Jeff zatwierdzil 4.1 w calosci; pierwsza wersja projektu to pominela) - CS `Army175.DothrakiRiders()`, suwak
  `DothrakiRidingBonus` (0-50, dom. 30):** zbior regula (nie lista): kultura `khuzait`, linia wsi, `default_group` Cavalry/HorseArcher, t2-t5 - 7 jednostek
  (Jazda po 175: `tribal_warrior` t2 50, `raider` t3 70, `horseman` t3 70, `lancer` t4 100, `horse_archer` t4 110, `heavy_horse_archer` t5 130, `heavy_lancer` t5 150).
  175 nie zmienia koni, wiec "ponad dzisiejsze" = ponad wartosc po zamianie (tu bez sporu, inaczej niz w 3.2). Ta sama maszyna co NorthHardy (po `SkillSinew`,
  znacznik na obiekcie umiejetnosci, log liczby - ma byc 7). Skutek [S, `SCR\kryt175b\sila_odp.py`]: Sila Dothrakow J 95 -> **104**, KL 97 -> **105** (w przedziale
  90-110; lore: Dothrakowie jezdza lepiej niz rycerze). Dziala tylko w bitwach z graczem (autobitwa = tier) - nie grozi dominacja w wojnach AI.
- Gracz i Daenerys: gracz rekrutujacy `khuzait_nomad` ma tylko konna droge (kon z taboru, decyzja 30.08). Szablon Daenerys (`clan_targaryen_party_template`)
  bez zmian (ma swoich pieszych lucznikow Dothrakow w puli). `rebels_khuzait_template` i patrole Dothrakow (juz konne) bez zmian. Piesi Dothrakowie w milicji - bez zmian.
- Pod murami (decyzja 3a): bez zmian zachowania AI; autobitwa daje konnym przy oblezeniu atak -10%/-20% (`_battleModifiers`). Mierzymy szturmy Dothrakow (6.2).

### 2.2 Zelazne Wyspy - jazda tylko Harlaw (4.2a, decyzja 6) - wylacznik `Army175IronbornFoot`
- `kingdom_hero_party_sturgia_template` (XSLT :55; rody clan_sturgia_8, ROTclan_10/38/39/89/90/91/92 i tworzone w grze): usunac `sturgian_hardened_brigand` t4 (4),
  `sturgian_horse_raider` t5 (2); dodac `sturgian_berzerker` t4 (3), `sturgian_shock_troop` t5 (2), `sturgian_ulfhednar` t5 (1); `sturgian_hunter` 8 -> 6.
  Cel 41/38/21/0 -> **67/33/0/0**, swieza 68/21/11 -> 82/18/0, ludzi 53 -> 51. Drzewo: `sturgian_brigand` [hardened_brigand] -> [`sturgian_berzerker`, `sturgian_spearman`] (XSLT :3632).
- `clan_greyjoy_party_template` (partyTemplatesROT.xml :1059): usunac `greyjoy_rider` t4 (3), `greyjoy_horseman` t5 (3); `greyjoy_houseguard` 4 -> 7, `greyjoy_fingerdancer` 1 -> 4.
  Cel 31/48/21 -> **52/48/0**. Drzewo: `greyjoy_soldier` [houseguard, archer, rider] -> [`greyjoy_houseguard`, `greyjoy_archer`] (ROT-Troops.xml :11694).
- `clan_harlaw_party_template` (:1199) BEZ zmian - `harlaw_rider` t4 (4), `harlaw_horseman` t5 (2) to jedyna jazda Wysp (cel 34/45/21).
- **Patrole Zelaznych Wysp (dopisane - ta sama decyzja 6a):** `patrol_party_sturgia_template_level_1`: `sturgian_hardened_brigand` 2 -> `sturgian_berzerker` 2;
  `_level_2`: `sturgian_horse_raider` 3 -> `sturgian_shock_troop` 3, `sturgian_hardened_brigand` 2 -> `sturgian_berzerker` 2; `_level_3`: horse_raider 4 -> shock_troop 4,
  hardened_brigand 3 -> berzerker 3 (ten sam tier, te same liczby; patroli ROT nie wymienia, wiec bez tego jazda Wysp zylaby w patrolach).
- **Nagroda wasalna (dopisane po krytyce):** `vassal_reward_troops_sturgia` (partyTemplatesROT.xml :212-217; kultura sturgia, spcultures.xml ROT :1099) daje graczowi
  `sturgian_hardened_brigand` t4 (jazda) 1 -> `sturgian_berzerker` t4 1 (ten sam tier); `sturgian_veteran_warrior` 5 bez zmian. Ten sam wylacznik `Army175IronbornFoot`.
- Na zapisie: hardened_brigand, horse_raider, greyjoy_rider, greyjoy_horseman wypadaja z puli - ROT wymienia ich na piechote/strzelcow tego samego tieru (pozadane);
  ich konie zostaja w zbrojowniach jako nadwyzka. Zold: swieza -8%, cel -8% (Greyjoy -10%).

### 2.3 Polnoc - wiecej piechoty, mniej lucznikow, troche wiecej jazdy (4.3, decyzja 1) - wylacznik `Army175NorthFoot`
Regula: stos lucznikow t3-t4 o ok. 1/4 w dol, piechota t3-t4 w gore o tyle samo, jazda t5 +1; Mormontowie zostaja lucznikami ponad srednia (lowczynie).
| Szablon (linia XML) | Zmiany | Cel P/S/J/KL | Swieza | Zold swieza / cel |
|---|---|---|---|---|
| `kingdom_hero_party_battania_template` (XSLT :109; 12 rodow w XML + tworzone) | trained_warrior 4->5, picked_warrior 2->3, skirmisher 8->6, veteran_skirmisher 4->3, horseman 2->3 | 38/41/21 -> **45/31/24** | 66/23/11 -> 70/17/13 | +5% / +6% |
| `clan_stark_party_template` (:975) | stark_bowman 7->5, stark_archer 5->4, stark_footman 4->5, stark_soldier 3->5, stark_cavalry 2->3 | 34/45/21 -> 43/33/23 | 64/25/11 -> 69/19/13 | +5% / +5% |
| `clan_bolton_party_template` (:958) | bolton_archer 7->5, bolton_elite_archer 5->4, bolton_scout 4->5, bolton_veteran 3->5, bolton_knight 2->3 | 34/45/21 -> 43/33/23 | jw. | +5% / +5% |
| `clan_karstark_party_template` (:927) | karstark_archer 8->6, karstark_elite_archer 5->4, karstark_soldier 4->5, karstark_ruffian 4->6, karstark_shock_cavalry 2->3 | 34/45/21 -> 43/33/23 | jw. | +1% / 0 |
| `clan_glover_party_template` (:1184) | glover_archer 8->6, glover_veteran_archer 5->4, glover_footman 4->5, glover_man_at_arms 4->6, glover_horseman 2->3 | 34/45/21 -> 43/33/23 | jw. | +1% / 0 |
| `clan_manderly_party_template` (:1007) | manderly_archer 8->6, manderly_veteran_archer 4->3, whiteharbor_footman 4->5, manderly_man_at_arms 4->6, whiteharbor_elite_knight 1->2 | 31/41/28 -> 40/30/30 | 62/23/15 -> 67/17/17 | +1% / 0 |
| `clan_umber_party_template` (:790) | umber_archer 10->7, umber_marksman 3->2, umber_footman 4->5, umber_man_at_arms 2->5, umber_horseman 2->3 | 34/45/21 -> 47/30/23 | 64/25/11 -> 70/17/13 | +1% / 0 |
| `clan_mormont_party_template` (:1091) | mormont_trapper 8->7, mormont_footman 4->5, mormont_horseman 3->4 (huntress 4, mounted_huntress 1, man_at_arms 3 bez zmian) | 28/48/21/3 -> 30/43/23/3 | 60/26/11/2 -> 61/24/13/2 | +4% / +3% |
| `clan_cerwyn_party_template` (:992) | cerwyn_archer 8->6, cerwyn_veteran_archer 5->4, cerwyn_soldier 4->5, cerwyn_axeman 4->6, cerwyn_horseman 2->3 | 34/45/21 -> 43/33/23 | 64/25/11 -> 69/19/13 | +1% / 0 |
Ludzi w stosach 53 -> 53-54. Uwagi: `mormont_scout` t4 nie ma w XML `upgrade_requires` - Stables i tak zada konia (cel `IsMounted`); `umber_houseguard` jest tez w drzewie Clegane
(zmiana dotyczy obiektu, nie rodu - tu i tak tylko liczby stosow). Na zapisie pula sie nie zmienia (drzewa te same) - sklad przesuwa sie stopniowo przez werbunek i awanse.

### 2.4 Volantis i Norvos (4.6; decyzja 7a dotyczy TYLKO sloni - te zostaja) - wylacznik `Army175VolantisNorvos`, dom. **NIE** (rozdz. 0 pkt 9)
Sklad 4.6 nie byl pytaniem do Jeffa (pytanie 5.7 = slonie). Zbudowane i sprawdzone technicznie w T1, wlaczane dopiero na slowo Jeffa. Do wiadomosci przy decyzji:
Volantis traci prawie polowe premii obrony murow (+13.6% -> +7.1%, 2.6), Norvos (49 bitew lordow w 120 dob) mniej jazdy na otwartym.
- `kingdom_hero_party_volantine_template` (partyTemplatesROT.xml :414): volantine_bowman 8->4, tigercloak_archer 4->2, tigercloak_master_archer 2->1, volantine_soldier 4->6,
  tigercloak_warrior 2->4, tigercloak_elite_warrior 1->2; `tigercloak_camel_cavalry` (slonie, 3) ZOSTAJE. Cel 23/53/23 -> **43/32/25**, swieza 57/30/13 -> 69/17/13,
  ludzi 54 -> 52, zold +1% / +3%.
- `kingdom_hero_party_norvos_template` (:352): norvos_horseman 4->2, norvos_cavalry 3->1, norvos_axeman 2->5, norvos_master_axeman 1->2; konni kaplani (`norvos_priestguard`,
  `mounted_priest`) bez zmian. Cel 32/27/41 -> **50/27/23**, swieza 57/26/17 -> 65/26/9, zold -1% / -1%.

### 2.5 4.12 - drobne zgodnosci z lore
`Army175MinorLore` (Wolni Ludzie, Smocza Skala) - dom. **NIE**: 4.12 nie bylo pytaniem do Jeffa; Smocza Skala to aktywny front (107 bitew lordow w 120 dob, glownie
z Krainami Burzy - 120), wiec mniej jazdy zmienia wynik tej wojny w autobitwie. Zbudowane, sprawdzone w T1, wlaczane na slowo Jeffa (rozdz. 0 pkt 9).
- **Wolni Ludzie** (`Army175MinorLore`): `freefolk_horseman` t5 4 -> 1 w `kingdom_hero_party_freefolk_template` (:19; cel 52/29/19 -> 61/33/6) i `clan_rayder_party_template`
  (:1153; 48/29/24 -> 56/33/11 - zostaje `giant_rider` na mamucie); ludzi 53 -> 50; zold -15% / -16%. Patrole Wolnych Ludzi (po 1 jezdzcu) bez zmian.
- **Smocza Skala** (`Army175MinorLore`), jazda w celu ok. 10%: `kingdom_hero_party_dragonstone_template` (:64): dragonstone_rider 4->1, dragonstone_horseman 3->1,
  dragonstone_man_at_arms 2->4, dragonstone_brute 3->5 (`dragonstone_steel_curtain` t6 zostaje) - cel 31/44/25 -> **45/45/10**, zold -10% / -11%;
  `clan_velaryon_party_template` (:836): velaryon_scout 4->2, velaryon_horseman 2->1, velaryon_warrior 2->5 - 31/48/21 -> 41/48/10; `clan_celtigar_party_template` (:1214):
  celtigar_horseman 4->2, celtigar_knight 2->1, celtigar_man_at_arms 4->7 - 34/45/21 -> 45/45/10.
- **Frey - konni lucznicy: ODLOZONE (nie w 175).** W kulturze `river` nie ma zadnej jednostki konnego lucznika (poprawka po krytyce: pierwsza wersja pisala "w calym
  Westeros poza Dorne" - falsz, jest `mormont_mounted_huntress` t5, kultura battania, jedyny konny lucznik Polnocy); `frey_rider` t4
  i `frey_horseman` t5 to kopijnicy (Luk 5 w XML). Zrobienie go wymaga zmiany roli istniejacej jednostki: formacja na HorseArcher (refleksja `DefaultFormationClass`,
  `_isRanged`), wzorzec z lukiem do jazdy (nie dlugim) i umiejetnosc Luku - a ta nie moze wziac sie z niczego (`SkillsDecide` wybierze lance, dopoki Drzewce > Luk).
  To wiecej niz "tylko wzorce" i poza decyzjami Jeffa - zostaje opisane, nie budowane.
- **Zlota Kompania - 1/3 kusz, 1/3 lukow dwukrzywych, 1/3 cisowych: 175.G, ZBUDOWANE ALE DOMYSLNIE WYLACZONE do czasu K1** (`Army175GoldenBows`).
  Linia `golden_crossbowman` t3 / `golden_veteran_crossbowman` t4 / `golden_master_crossbowman` t5 ma same kusze (po 2 zestawy). Robota: (a) CS `GoldenBows` -
  kazdej z 3 jednostek 3 zestawy bojowe: [kusza + belty] (po zamianie 1.3), [luk dwukrzywy + 2 strzaly], [luk cisowy (dlugi) + 2 strzaly], reszta zestawu jak 1. zestaw,
  luki i strzaly z kaskady 1.3 w tierze <= T (np. t4 `longbow_recurve_desert_bow`/`woodland_yew_bow`, t5 `nomad_bow`/`lowland_yew_bow` - wybiera regula, nie reka);
  `SkillSinew` podniesie Luk do wymogu tych lukow (zasada "do wlasnego sprzetu"); (b) Armoury `SkillsDecide.RearmBySkill`: gdy zestawy jednostki maja ROZNE klasy
  dystansowe, glowna bron dystansowa = klasa z WYLOSOWANEGO zestawu (`ReferenceEquipment` w Assignment), nie najwyzsza umiejetnosc - inaczej wszyscy dostaliby kusze
  (Kusza 110 > Luk 105). **Dlaczego wylaczone:** AI kupuje sprzet wedlug koszykow z 1. zestawu (`AiGear.NeedBuckets` czyta `ch.Equipment`) - zbrojownie Zlotej Kompanii
  nie mialyby lukow, a strzelec z zestawem lukowym stalby w bitwie z graczem bez broni dystansowej. Wlaczyc po K1 z koszykami "ze wszystkich zestawow z waga 1/n" (1.9).
  Autobitwy to nie dotyczy (strzelec to strzelec). **Wlaczenie wymaga tez zgody Jeffa na wyjatek od jego zasady z 28.08** ("glowna bron = najwyzsza umiejetnosc"):
  krok (b) wybiera bron dystansowa z wylosowanego zestawu, nie z najwyzszej umiejetnosci - zapisane w rozdz. 0 pkt 10 ("po K1").
- **Nocna Straz - zold 0 (decyzja kanonu 4, STAN-PRAC :437): sprawdzone - NIE zrobione** w zadnym drzewie (a175arm, a175cs, n174, k1, z1; CHANGELOG a175arm :842 pkt f
  "jeszcze nie w kodzie"). **Nie w 175** - osobna mala paczka zaraz po 175 (proponuje numer 176), bo: zmienia rachunki rodow i bankructwa, ktorymi test 175 mierzy
  Dothrakow (jedna zmiana naraz); styka sie z `MountedWage.Apply` (postfiks GetCharacterWage, MountedWage.cs:58), `SoldierPay` (zold -> sakiewki ludzi), `ArmyClothing`
  (odziez z zoldu), `RecruitCost` (cena werbunku = dni zoldu), `WarLedger.cs:66` (`Math.Max(1, wage)` - partia bez zoldu = nieoplacona -> dezercja), modelem zoldu BK;
  a kto placi wyzywienie i sprzet Strazy - to dar Polnocy dla Strazy z projektu ekonomii (audyt 05, D-5, paczka 165).

### 2.6 Zalogi (ROT ustawia je codziennie pod szablon rodu wlasciciela, ROTTroopRecruiter.cs:51-79)
Sredni dodatek terenu obroncy przy oblezeniu dla CELU szablonu (strzelcy +30%, konni lucznicy +30%, jazda -10%, piechota 0; `_battleModifiers`) [S]; prawdziwa zaloga
lezy miedzy swieza partia a celem:
| Szablon | Dzis | Po 175 | Uwaga |
|---|---|---|---|
| Polnoc (krolestwo) / rody Starkow itd. | +10.2% / +11.4% | +6.9% / +7.6% | mniej lucznikow na murach Polnocy (cel 30-33%, Mormont 43%) - celowo nie schodzimy do 21% |
| Mormont | +13.2% | +11.5% | |
| Dothrakowie | +20.0% | +17.2% | konni lucznicy bronia sie jak lucznicy; ale zold zalog konnych x1.5 (`MountedWage.PartyPays`) i rekruci t1 bez koni nie awansuja |
| Zelazne Wyspy / Greyjoy | +9.3% / +12.3% | +9.9% / +14.4% | bez jazdy (-10%) obrona nawet lepsza |
| Volantis (tylko przy `Army175VolantisNorvos`) | +13.6% | +7.1% | najwiekszy spadek - mniej lucznikow (lore: tygrysie plaszcze to piechota) |
| Norvos | +4.0% | +5.8% | |
| Wolni Ludzie / Rayder | +6.8% / +6.3% | +9.3% / +8.8% | |
| Smocza Skala / Velaryon / Celtigar | +10.7% / +12.3% / +11.4% | +12.5% / +13.4% / +12.5% | |
Zalogi startowe nowej gry bierze gra ze stosow SZABLONU KULTURY (wagi: `IsRanged` 6 - takze konny lucznik, pieszy 2, konny 1) - po latce tez nowe
(u Dothrakow ok. 80% konnych zamiast ok. 28%, 2.1 droga c). Mierzymy szturmy i twierdze zdobyte ORAZ stracone przez kazde krolestwo (6.2 B4/B6).

---

## 3. Polnoc - wariant C (decyzja 1) - 175.4

### 3.1 Lore (odpowiedz na pytanie Jeffa "czy to prawda?")
Polnoc jest ogromna, ale rzadko zaludniona - Martin daje jej sile zbrojna podobna do Doliny i Dorne [L: SSM 2002], podrecznik d20 ok. 45 tys. przy 80-100 tys. Reach
[L-pol]; ludzie Polnocy lepiej znosili marsz w sniegu niz poludniowcy Stannisa [L: ADWD The King's Prize]; ROT w opisie Starkow: "It is said that Stark men are built
sterner than others" [D: spclans.xml :89]; ale w polu piechota Polnocy przegrala z Tywinem [L: AGOT Tyrion VIII]. Wniosek: "twardsi, zwlaszcza w zimie i u siebie" - tak;
"lepsi wszedzie" - nie. Dlatego +25 to jakosc ludzi (dziala w bitwach z graczem), a przewaga w autobitwie tylko na sniegu i w lasach Polnocy.

### 3.2 +25 broni glownej i Atletyki - `Mends`/`Army175.NorthHardy` (CS) - wylacznik-suwak `NorthHardySkillBonus` (dom. 25, 0 = wylaczone)
- **Zbior - 43 jednostki** (kultura `battania`, `default_group` Infantry, tier 3-6, w drzewach `battanian_volunteer` (wies), `battanian_highborn_youth` (szlachta)
  i 9 szablonow Polnocy z 2.3; bez milicji `battanian_militia_*`, bez najemnikow Wild Hares/Moon Brothers, bez t1-t2):
  t3 (11): battanian_highborn_warrior, battanian_raider, battanian_trained_warrior, bolton_scout, cerwyn_soldier, glover_footman, karstark_soldier, mormont_footman,
  stark_footman, umber_footman, whiteharbor_footman; t4 (11): battanian_falxman, battanian_hero, battanian_picked_warrior, bolton_veteran, cerwyn_axeman,
  glover_man_at_arms, karstark_ruffian, manderly_man_at_arms, mormont_man_at_arms, stark_soldier, umber_man_at_arms; t5 (15): battanian_fian,
  battanian_mounted_skirmisher ("Northern Pikeman", piechota), battanian_oathsworn, battanian_veteran_falxman, cerwyn_master_axeman, dreadfort_blackguard,
  dreadfort_pikeman, glover_warrior, karstark_brute, mormont_houseguard, stark_houseguard, stark_pikeman, umber_axeman, umber_houseguard, whiteharbor_elite_pikeman;
  t6 (6): bolton_flayer, cerwyn_marauder, glover_bushranger, karstark_loyalist, stark_swornsword, umber_berzerker. Zbior liczony w kodzie ta regula (nie lista na sztywno),
  log podaje liczbe - ma byc 43.
- **"+25 ponad dzisiejsze wartosci" = ponad wartosci PO zamianie sprzetu (rozdz. 1) - interpretacja do potwierdzenia przez Jeffa przy "wgraj" (rozdz. 0 pkt 4).**
  Inaczej Polnoc zachowalaby wlasnie te napompowane 175 z tarcz t6, ktore Jeff nazwal bzdura (dzis Stark Footman t3 ma 1H 175 tylko przez tarcze). Po 175:
  Stark Footman t3 1H/Atl 80/80 -> **105/105**, Northern Man at Arms (trained_warrior) t3 70/70 -> 95/95, Stark Soldier t4 110/110 -> 135/135, Manderly Man at Arms
  t4 105/110 -> 130/135, Stark Houseguard t5 140/140 -> 165/165, Stark Swornsword t6 250 -> 275.
  **Obie liczby (wersja 2, [S] `SCR\kryt175b\sila_odp.py`, piechota t3+ wsi/szlachty/rodow, bron glowna/Atletyka):** dzis Polnoc 153/158, swiat bez Polnocy 150/156
  (przewaga +2%/+1.5%); po samej zamianie 131/130 wobec 122/121 (+7.5%/+7.8%); po 175 z +25 **156/155 wobec 122/121 (+28.0%/+28.5%)**. Doslowne "+25 ponad dzisiejsze"
  daloby 178/183, czyli +46%/+52% - na liczbach podbitych tarczami t6 (sprzeczne z decyzja 8b i z warunkiem rownowagi). Cena interpretacji: u **27 z 43** jednostek
  zbioru bron glowna albo Atletyka konczy PONIZEJ dzisiejszej wartosci (Stark Footman 1H 175 -> 105, Stark Soldier 175/175 -> 135/135, Manderly Man at Arms 175/175 ->
  130/135, Stark Houseguard 175 -> 165, White Harbor Footman Atl 140 -> 95) - Jeff zobaczy to w swoich bitwach jako "slabsza" pojedyncza jednostke przy mocniejszej
  Polnocy wzgledem swiata. Suwak: +15 daje +20%, +25 = +28%, +50 ok. +48%.
- **Kolejnosc:** PO `SkillSinew` (ten tylko podnosi, wiec "+25 przed nim" zostaloby wchloniete, gdy wymog sprzetu > XML+25) i tylko gdy `SinewApplied`.
  Wartosc docelowa = `max(obecna, wymog wlasnego sprzetu) + bonus` dla glownej broni i dla Atletyki. **Bron glowna (wersja 2, jedna definicja w kodzie i w skrypcie):**
  najwyzsza z umiejetnosci Jednoreczna/Dwureczna/Drzewce po `SkillSinew` (remis: klasa noszona najczesciej, potem 1H > Drzewce > 2H) - ta sama regula co `SkillsDecide`
  (SkillsDecide.cs:84-98: glowna = najwyzsza umiejetnosc), wiec +25 nigdy nie przelaczy broni w bitwie (pierwsza wersja: "najczesciej noszona" - rozjazd ze skryptem
  i z SkillsDecide; rachunkowo roznica 3 z 43 jednostek, wynik ten sam: +27.9%/+28.5%). Ustawienie tym samym `GetDefaultCharacterSkills().Skills.SetPropertyValue`
  co Sinew (Mends.cs:2444-2461). **Znacznik "raz" na OBIEKCIE UMIEJETNOSCI** (`ConditionalWeakTable<MBCharacterSkills, object>`, klucz
  `GetDefaultCharacterSkills()`), nie na postaci: `skill_template` i `FillFrom`/`CreateFrom` dziela `DefaultCharacterSkills` (BasicCharacterObject.cs:208, :301, :337-340) -
  klon jednostki dostalby +25 drugi raz na tym samym obiekcie. Dzis zadna z 43 nie ma `skill_template`. Obiekty sa nowe w kazdej grze - brak kumulacji.
- **Wynik [S]:** piechota t3+ Polnocy bron/Atletyka 131/130 (po zamianie) -> **156/155**; swiat BEZ Polnocy 122/121 (275 jednostek) - **+28.0%/+28.5%**
  (pierwsza wersja podawala +23/+24% wobec sredniej swiata z sama Polnoca w srodku - zanizone). 1. miejsce wsrod 30 kultur (dalej Wolni Ludzie 151/130,
  Ghis/Nieskalani 142/137) - lore z 3.1 mowi "twardsi od poludniowcow", nie "lepsi od Nieskalanych"; powiedziane Jeffowi (rozdz. 0 pkt 3), suwak w jego rekach.
  Sila wzorca krolestwa: piechota 98 -> **111**.
- **Przeciek poza Polnoc (znany):** `umber_houseguard` jest tez w szablonie Clegane (partyTemplatesROT.xml :812, 2 ludzi na partie) - +25 dziala na obiekt jednostki,
  wiec ci 2 tez je maja (zapisane jako znane; wykluczenie zabraloby +25 Umberom). Przewaga +10% w autobitwie (3.3) ma dodatkowy warunek kultury rodu partii.
- **Styk:** t3 z 80 -> 105 osiaga dokladnie wymog broni i pancerza tieru 4 (35 na tier) - `SkillsDecide` (bez sufitu tieru, 1.9) moze w bitwie z graczem dac
  piechocie Polnocy t3 bron t4 z polki, a K1 (dozbrajanie "kto udzwignie") - jesli sufit K1 bedzie tierem jednostki (1.9), to nie kupi t4 dla t3. To jest zamierzone
  znaczenie "twardszych" (umiejetnosc decyduje, wymogi zostaja); wzorce zostaja w tierze. Log liczy, ilu takich t3 jest (z 11 jednostek t3 bron >= 105 ma 9 [S]:
  rodowe footmany i highborn_warrior; `battanian_trained_warrior` i `battanian_raider` 95).
- Log: `Mends: NorthHardy (175) - 43 jednostkom +25 (bron glowna, Atletyka); piechota t3+ Polnocy bron/Atl 156/155 wobec swiata bez Polnocy 122/121 (+28%/+28%); np. stark_footman 1H 80->105 ...`
  + **kontrola poprawnosci (nie hamulec - wersja 2):** przewaga liczona ta sama definicja co wyzej; poza przedzialem 25-31% przy suwaku 25 (albo rozjazd z suwakiem
  przy innej wartosci) - linia `OSTRZEZENIE` (podwojne +25, zly zbior, zla klasa). Pierwsza wersja nazywala to "hamulcem statycznym" - przy stalym bonusie
  nie moze sie wlaczyc, wiec lapie tylko bledy kodu. Hamulcem dla +25 jest suwak i ocena Jeffa w jego bitwach (3.4).

### 3.3 +10% w autobitwie na sniegu i w lasach Polnocy - Armoury `NorthHomeEdge` - suwak `NorthHomeEdgePercent` (dom. 10, 0 = wylaczone)
- **Gdzie:** postfiks na `TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.SimulateHit` (cios w symulacji; wolany z `MapEvent.SimulateSingleTroopHit`,
  MapEvent.cs:968; ROT `ROTCombatSimulationModel` deleguje do bazowego, ROTdec :86-105), **Priority.Low** - po BK (Normal: Strzemiona +20%, Planista Oblezen +15%,
  VanillaModelTweakPatches.cs:313-344), przed CS `ValyrianWardSim` (Priority.Last, mnozy wynik x0.15 - nasz mnoznik przechodzi przez niego proporcjonalnie).
  Wzor ciosu: 40 x (moc bijacego / moc trafianego)^0.7 x ... (DefaultCombatSimulationModel.cs:18-30), wiec +10% mocy = x1.1^0.7 = **x1.069 na cios zadany** przez
  jednostke ze zbioru i **/1.069 na cios otrzymany** przez nia (gdy obaj ze zbioru - znosi sie). Zapis jak w ValyrianWardSim: `__result = new ExplainedNumber(__result.ResultNumber * k)`.
- **Dlaczego nie `GetContextModifier`:** ten zmienia tez `PartyBase.CurrentStrength` (PartyBase.cs:866-881, ok. 120 wywolan - decyzje AI na mapie: kogo atakowac,
  przed kim uciekac), sile stron i XP. Przewaga ma rozstrzygac cios, nie zmieniac zachowania AI.
- **Warunek:** bitwa polowa (nie oblezenie - tam kontekst zawsze `Siege`, przewaga nie dziala: dobrze dla balansu) i `battle.SimulationContext == SnowBattle` (wszedzie;
  snieg z terenu albo z pogody Snowy/Blizzard, DefaultMilitaryPowerModel.cs:192-242) albo `== ForestBattle` i region bitwy jest Polnocy (`OutlawLaw.RegionAt(pozycja)`
  - najblizsza twierdza kultury `battania`, OutlawLaw.cs:137-146, :199). Wynik warunku w `ConditionalWeakTable<MapEvent, ...>` odswiezany raz na godzine gry.
- **Zbior:** ten sam co w 3.2 (43 jednostki, ta sama regula liczona w Armoury przy `OnSessionLaunched`; log liczby - ma byc 43, jak w CS), **i** partia bijacego
  nalezy do rodu kultury `battania` (garnizon - rod wlasciciela osady) - wersja 2: zamyka przeciek przez `umber_houseguard` w partiach Clegane.
- **Swiadome zawezenia (wersja 2, powiedziane Jeffowi w rozdz. 0 pkt 5):** tylko piechota t3-t6 linii wsi, szlachty i rodow (bez rekrutow t1-t2, ktorzy sa
  wiekszoscia swiezych partii) - jakosc "twardszych" to wyszkolony wojownik, nie chlop z widlami; las tylko w regionie Polnocy. **Snieg - wszedzie** (krytyka proponowala
  tylko Polnoc i ziemie za Murem): lore wiaze te twardosc z zimnem, nie z miejscem (marsz w sniegu, 3.1), a w autobitwie +10% rozstrzyga tylko bitwy prawie rowne (nizej),
  wiec poszerzenie po wprowadzeniu zimy na poludniu nie zmienia rownowagi; linia B175 zapisuje region, wiec test pokaze, gdzie przewaga dziala.
- **Dlaczego to przeciwwaga, nie prezent:** snieg jest w grze "plaskim terenem" (ten sam wiersz co rownina): jazda atakujaca +25%, konni lucznicy +30%, piechota 0;
  w lesie piechota juz dzis +5% (`_battleModifiers`, DefaultMilitaryPowerModel.cs:41-83, :159-190).
- **Ile bitew [P]:** w 120 dobach Polnoc stoczyla 5 bitew lord z lordem innego krolestwa (wlasny rachunek 6.3: 1 w 40 dob); w 50 bitwach jej lordow z "lordami"
  (takze bandy) srodek: rownina 35, las 15; srodek "Snow" nie wystapil w zadnej z 2 987 bitew H3 (snieg moze przyjsc tylko z pogody - log tego nie zapisuje).
  [ocena] Ta czesc nie moze sama zrobic z Polnocy armii, ktora bije wszystkich - Polnoc prawie nie walczy z lordami innych krolestw.
  **Dlaczego +10% znaczy malo (wersja 2, [P] `SCR\kryt175b\kfit.py` na kopiaT9-120, 520 bitew lord-lord):** autobitwe wygrywa prawie zawsze silniejszy - przy
  stosunku mocy 1.00-1.10 silniejszy wygral 8 z 14 (57%), 1.10-1.25 - 21 z 23 (91%), 1.25-1.50 - 32 z 32, powyzej 1.5 - 443 z 444. Przewaga x1.069 na cios dla 20-30%
  mocy partii (zbior 43 w dojrzalej partii) to ok. +2-3% mocy partii [S], czyli w bitwie dokladnie rownej szansa ok. 50% -> 57-60% (k = 12.6), a bitew o stosunku
  < 1.1 bylo na calym swiecie 14 w 120 dob. We wszystkich bitwach polowych (takze z bandami) Polnoc byla w ok. 220 z 2 955, srodek w lesie w 31 (14%).
- Licznik dla miary: bitwy z aktywna przewaga i ciosy z mnoznikiem (do linii "Bitwa: B175", 4.4).

### 3.4 Hamulec balansu (warunek Jeffa) - wersja 2, uczciwie: co da sie zmierzyc, a czego nie
**Co jest czym:** +25 (3.2) i sprzet wedlug tieru (rozdz. 1) dzialaja TYLKO w bitwach z graczem - autobitwa liczy moc z tieru. Autotest bez gracza tego nie
zobaczy; ocenia to Jeff w swoich bitwach (rozdz. 0 pkt 5). +10% (3.3) dziala w autobitwie, ale w zwyklym biegu Polnoc ma za malo bitew lord-lord (5 w 120 dob,
1 wyrownana; sniegu w logu nie bylo), wiec progi "na krolestwo" z pierwszej wersji (B1 >= 5 bitew i WPO > +3.0, N1 >= 5 bitew z przewaga) dalyby "za malo danych".
1. **Waski zasieg z definicji:** tylko piechota t3-t6 z linii wsi, szlachty i rodow Polnocy w partiach rodow kultury battania; +10% tylko snieg i lasy Polnocy,
   nigdy przy oblezeniu; +25 nie dziala w autobitwie.
2. **Rachunek z gory [S] (3.3):** +10% daje ok. +2-3% mocy partii; rozstrzyga tylko bitwy o stosunku mocy < ok. 1.1 (14 z 520 na swiat w 120 dob).
3. **Suwaki** `NorthHardySkillBonus` (0-50) i `NorthHomeEdgePercent` (0-25); kontrola poprawnosci +25 przy wczytaniu (3.2, przedzial 25-31%).
4. **Bieg celowany T5 (6.0)** - jedyny sposob, zeby autotest naprawde zmierzyl +10%: wymuszona wojna Polnocy, dwa biegi (+10 i 0), miary 6.2 N1-N2.
5. **Miara zastepcza w zwyklych biegach (T3):** wszystkie bitwy z udzialem zbioru 43 (takze z bandami i Wolnymi Ludzmi, ok. 220 w 120 dob, w lesie ok. 31): liczba
   ciosow z mnoznikiem i wspolczynnik strat Polnocy (straty wlasne / straty wroga) w bitwach z aktywna przewaga wobec B0 - informacyjnie.
6. **Reakcja:** przekroczenie progu (6.2 N1/N2 albo B1/B2/B4 dla Polnocy) -> `NorthHomeEdgePercent` 10 -> 5, przy powtorzeniu -> 0; `NorthHardy` zostaje (autobitwa
   go nie widzi), chyba ze Jeff zglosi z wlasnych bitew, ze Polnoc "bije wszystko". Hamulca samoczynnego (sam sie wylacza, gdy Polnoc wygrywa) NIE robie - nie wynika
   z niczego w swiecie.

### 3.5 Czy liczebnosc Polnocy ma spasc? Tak wedlug lore - ale nie w 175
- Dzis [P, doba 120]: Polnoc ma najwiecej ludzi w polu (11 868, 11.0% swiata; w zalogach 8 880) przy 26 rodach (najwiecej rodow). Lore: sila zbrojna jak Dolina i Dorne.
- **Skad ma spasc:** z PIENIEDZY i LUDZI, nie z dekretu (decyzja 09.10 C: "mniej wojska AI" - NIE). Polnoc jest juz dzis najbiedniejsza: [P] kiesy glow 4.16 mln
  -> 2.16 mln w 120 dob, 2 bankrutow, 4 glowy < 5 000; w dobie 364 skarbiec 9 zl, 22 z 26 rodow na minusie (audyt 05). Paczka **166 BUDZET RODU** ("armia tylko
  z pieniedzmi": pulap zoldu 25% D w pokoju / 55% D w wojnie) sama zetnie partie Polnocy do tego, na co ja stac; demografia (pule ochotnikow wedlug ludnosci,
  PeopleLedger, 173) - do tego, ilu ma ludzi. To robota 166 (i 173), nie 175.
- **W 175 tylko pomiar:** linia "Balans krolestw (175)" podaje ludzi w partiach i zalogach kazdego krolestwa i udzial w swiecie (baza: Polnoc 11.0% w dobie 40 i 120);
  do 166 przekazuje sie liczby (ile Polnoc wystawia na swoj dochod). Kolejnosc ma znaczenie dla warunku Jeffa: dopoki 166 nie wejdzie, jakosc +25 i +10% siedzi na
  najliczniejszej armii swiata - dlatego progi 6.2 obejmuja tez Polnoc.

---

## 4. Konie (decyzja 2) - 175.0 (pomiar) i 175.1 (naprawa)

### 4.1 Naprawa: kon za awans AI trafia do zbrojowni nowego jezdzca (jak u gracza) - wylacznik `AiUpgradeHorseToArmory` (dom. TAK)
- Dzis [K]: `Stables.PayInHorses` (Stables.cs:312-336, prefiks `PartyUpgraderCampaignBehavior.UpgradeTroop`) -> `Consume` (:231-254): najtansze konie kategorii,
  `r.AddToCounts(el, -take)` na taborze i koniec - **kon znika**. U gracza ten sam kon idzie do zbrojowni DTE (`GrabPaidHorses` :47-61 + `BankPaidHorses` :64-89).
- Zmiana: `Consume(party, cat, count, List<KeyValuePair<EquipmentElement,int>> taken)` zapisuje, co zdjal; `PayInHorses` po `Consume`:
  `var mp = party.MobileParty; foreach (taken) if (!AiGear.AddToArmory(mp, el.Item, n)) przepadlo += n;` (`AiGear.AddToArmory`, AiGear.cs:137-140 - DTE
  `AddItemToPartyArmory`; ta sama sygnatura w n174 AiGear.cs:241). Brak DTE / brak zbrojowni -> kon i tak schodzi z taboru (jak dzis) + licznik "przepadlo";
  NIE oddawac do taboru (awans bylby darmowy).
- Dotyczy partii awansujacych przez `PartyUpgraderCampaignBehavior`: lordowie AI, partie towarzyszy gracza, zalogi (garnizon to `MobileParty`). Petli kup-sprzedaj
  nie ma: `MenPurse` nie sprzedaje koni ze zbrojowni (MenPurse.cs:336-348), `NeedBuckets` ich nie liczy, tygodniowe przyciecie DTE wylaczone przy AiGear+MenPurse
  (AiGear.cs:91-118).
- **Modyfikator konia (znane odstepstwo):** zbrojownia AI to `Dictionary<ItemObject,int>` - kon kulawy albo stary trafia tam jako zdrowy (wartosc z niczego), a rasowy
  traci premie; u gracza `BankPaidHorses` zachowuje `EquipmentElement`, wiec "jak u gracza" nie jest scisle. `Consume` bierze najtanszy wedlug `Item.Value`
  (bez modyfikatora, Stables.cs:231-254), wiec kulawe nie ida systematycznie pierwsze. Licznik w 4.2 (konie z modyfikatorem przy przekazaniu, wedlug znaku ceny).
- **Wolne konie ze zbrojowni najpierw - AI (wersja 2, ten sam wylacznik):** dzis Stajnia liczy konie do awansu tylko w TABORZE (`CountInRoster`, `NeedForUpgrades`,
  Stables.cs:213-218, :497-536). Po naprawie kon po poleglym jezdzcu zostaje w zbrojowni, a lord na nastepny awans kupuje nowego - podwojny popyt i odplyw zlota
  (najbardziej u biednych Dothrakow), a pomiar 4.3 moglby dac falszywy wniosek "brak koni na rynku", choc konie leza w zbrojowniach. Zmiana: `wolne = konie
  (Stables.IsPlainMount) w AiGear.Armories()[mp.Id] - konni (IsMounted) w partii` (nie mniej niz 0); `FilterTargets` i `NeedForUpgrades` licza `tabor + wolne`,
  `PayInHorses` najpierw "przypisuje" wolne (kon zostaje w zbrojowni, nic sie nie przesuwa), dopiero potem bierze z taboru. Gracz robi to samo recznie (przenosi
  konia ze zbrojowni do taboru) - jedna zasada: kon musi istniec w partii (30.08); AI nie ma recznego kroku, wiec robi go kod.
- Opis w MCM do poprawy (Settings.cs:291, po angielsku): "...one horse per man; for the AI the horse goes into the new rider's armoury, just as yours does".

### 4.2 Pomiar (przed decyzja o "rycerzu z koniem") - Armoury `HorseCensus` - wylacznik `Army175Measure` (dom. TAK, tylko log)
Liczniki doby wedlug krolestwa (`party.MapFaction`; "AI" = wszystkie partie poza gracza):
| Licznik | Miejsce [K] |
|---|---|
| proby awansu odrzucone z braku konia (ludzie): "skret w inna droge" (lista celow niepusta po usunieciu) / "czeka" (pusta) | `Stables.FilterTargets` (:262-305), przy `list.RemoveAt(i)` (:285) `+= PossibleUpgradeCount`; to PROBY - FilterTargets idzie co dobe i po bitwie |
| przyciete do liczby koni | `FilterTargets` :286-301 `+= need - have` |
| zatrzymane przy zaplacie | `PayInHorses` :331 (`return false`) |
| awanse na jezdzca wykonane (wedlug kategorii konia: zwykly/bojowy/szlachetny i tieru celu) + konie do zbrojowni / przepadle | `PayInHorses` po `Consume` |
| stan doby: "czeka na konia" (ludzie, partie) i "konni bez konia w zbrojowni" (konni `IsMounted` vs konie w `AiGear.Armories()[mp.Id]`, `Stables.IsPlainMount`) | w petli `MountedWage.Daily` (MountedWage.cs:170-210 - juz chodzi po wszystkich partiach; bez drugiej petli); `Stables.NeedForUpgrades` (:497-536) z private na internal |
| zakupy lordow: wizyty z potrzeba, kupione konie i zloto (targ / wsie); nieudane: brak zlota (`budget < 50`), brak koni (polka i wsie puste albo za drogie) | `Stables.OnSettlementEntered` (:353-489); dzis tylko udane do `zakupy.log` |
| naplyw konnych do partii lordow: ochotnicy (notabl), najemnicy (karczma: osada bez notabla), jency | `CampaignEvents.OnTroopRecruited` (`troop.IsMounted x amount`); jency - prefiks na rekrutacji jencow AI (ten sam punkt co 171 `RecruitSources.InPrisonerRecruit`); **echo ROT odcinane** znacznikiem [ThreadStatic] z hooka ponizej. Po scaleniu z 171 uzyc jego `RecruitSources` (jedna klasyfikacja) |
| naplyw: zamiany ROT (+konni / -konni) z rodzajem (przy werbunku / zaloga / doba-lupy) | prefiks+postfiks na `ROTTroopRecruiter.ExchangeClanTroops` (ROTdec :128-220; refleksja): roznica konnych w rosterze; `fireEvent` = przy werbunku, `settlement != null` = zaloga |
| naplyw: awanse pieszy -> konny | `PayInHorses` (wykonane) |
| reszta (straty, ucieczki, przeplywy zaloga-lord) | stan doby minus znane przeplywy |
| **(wersja 2)** wolne konie w zbrojowni (konie `IsPlainMount` minus konni) i ile z nich poszlo na awanse | petla `MountedWage.Daily` + `PayInHorses` |
| **(wersja 2)** straz konia Dothrakow (2.1): konni z zamian ROT z koniem ze zbrojowni / z taboru / zamienieni na pieszych / bez zmiany (t6) | postfiks `ExchangeClanTroops` |
| **(wersja 2)** `rot_plus` wedlug zrodla czlowieka zamienianego: jeniec, najemnik, ochotnik innej kultury, swoj spoza puli | prefiks `ExchangeClanTroops` (kultura i `Occupation` zamienianego; 171 `RecruitSources` po scaleniu) |
| **(wersja 2)** saldo zlota zamian ROT (`GiveGoldAction` z nikad / do nikad przy zmianie tieru, ROTTroopRecruiter.cs:205-212) wedlug krolestwa | ten sam hak: roznica `owner.Gold` w prefiksie/postfiksie |
| **(wersja 2)** ochotnik Dothrakow awansowany u notabla na konnego: z koniem kupionym / cofniety z braku konia | `VolunteerKit.Postfix` (VolunteerKit.cs:81) - podzial licznikow wedlug `IsMounted` celu i kultury |
| **(wersja 2)** `khuzait_footman` w partiach i zalogach Dothrakow (ma tylko spadac przez straty, 2.1) | petla `MountedWage.Daily` |
| **(wersja 2)** konie z modyfikatorem przy przekazaniu do zbrojowni (ujemny/dodatni) | `PayInHorses` |
**Linia dnia** (obok `RecruitCost.Daily`/`MountedWage.Daily`, ArmouryBehavior.cs:1283-1284):
`Konie AI (175): dzien D | awanse na jezdzca: wykonane N (zwykly a, bojowy b, szlachetny c; do zbrojowni k, przepadlo p) | proby odrzucone z braku konia R (skret S, czekaja W), przyciete P, zatrzymane przy zaplacie Z | stan: czeka na konia C ludzi w L partiach, konni bez konia w zbrojowni B z M konnych | zakupy lordow: K koni za G (targ x, wsie y), nieudane: brak zlota u, brak koni v | naplyw konnych do partii lordow: ochotnicy o, najemnicy m, jency j, zamiany ROT +r/-s, awanse a`
+ plik `konie-krolestwa.csv` (`Log.Csv`, Log.cs:78-93): `dzien;krolestwo;konni;wszyscy;czeka;odrzucone;skret;przyciete;wykonane;do_zbrojowni;przepadlo;kupione;zloto;nieudane_zloto;nieudane_brak;bez_konia;naplyw_ochotnicy;naplyw_najemnicy;naplyw_jency;rot_plus;rot_minus;awanse;wolne_w_zbrojowni;awanse_z_wolnych;straz_zbrojownia;straz_tabor;straz_pieszy;straz_t6;rot_plus_jeniec;rot_plus_najemnik;rot_plus_obcy;rot_zloto;ochotnik_konny_kupiony;ochotnik_konny_cofniety;footman_dothrakow;kon_modyfikator`.

### 4.3 Jak z liczb wyjdzie decyzja o "rycerzu z koniem" (NIE wdrazamy jej w 175)
Po tescie 120 dob (6.0, T3), wedlug krolestw Westeros: (a) duze "czeka" i "nieudane: brak koni" przy pelnych sakiewkach i MALO wolnych koni w zbrojowniach -> brakuje
koni na rynku (temat hodowli/targu koni, nie rycerza; gdy wolnych koni w zbrojowniach duzo - blad w 4.1, nie rynek); (b) duze "nieudane: brak zlota" -> lordowie za biedni (166), rycerz z koniem z kiesy rodu nic nie da; (c) oba male, a jazdy dalej malo -> awanse nie sa
waskim gardlem, zrodlem jazdy sa najemnicy i zamiany ROT - "rycerz z koniem" zmieni malo. Dopiero wtedy pytanie do Jeffa z liczbami (audyt 14, 5.2).
Baza do porownania [P, kopiaT9-120]: lordowie kupili 1 591 koni w 592 zakupach w 120 dob (`zakupy.log`); konnych w partiach rodow AI 12.5% (d1) -> 9.1% (d40) -> 8.1% (d120),
w garnizonach 7.8% -> 6.7% -> 5.2% (linia "Zold konnych (160)"); najemnicy konni z karczm 8 617 w 120 dob.

### 4.4 Miara balansu - Armoury `KingdomBalance` (w ramach 175.0, ten sam wylacznik `Army175Measure`)
Dzis H3 loguje bitwe polowa z nazwa krolestwa, ludzmi, % konnych, srednim tierem i terenem srodka (LosersFlee.cs:1228-1258), ale bez rodzaju strony, mocy z gry,
kontekstu symulacji (snieg z pogody), oblezen i sum wedlug krolestwa. Dopisujemy (NIE zmieniac formatu istniejacej linii H3 - parsuja ja skrypty audytu):
- **Linia bitwy** (w `BattleChronicle.OnMapEventEnded`, BattleChronicle.cs:52 - widzi wszystkie typy; do `bitwy.log`):
  `Bitwa: B175 dzien D | FieldBattle|Siege|SiegeOutside|Raid | kontekst ForestBattle | region Winterfell (battania) | A: krol=<StringId> rodzaj=lord|armia|wies|karawana|zaloga|banda partii 2 ludzi 340 moc 512.3 konni 6% KL 0% PolnocT3+ 22% | O: ... | stosunek sil 1.28 (wyrownana) | wygrywa A | przewaga Polnocy: A tak (ciosow 418) | osada <id> (zdobyta tak/nie)`.
  Moc = suma `Campaign.Current.Models.MilitaryPowerModel.GetTroopPower(c, strona, me.SimulationContext, lider)` po skladzie sprzed bitwy (roster + polegli + rozbici,
  jak `LosersFlee.Compose` :609-623). Ta moc NIE zawiera przewagi Polnocy (ta siedzi w ciosie, 3.3) - dzieki temu "wygrane ponad oczekiwane" Polnocy mierza jej skutek.
- **Linia dnia:** `Balans krolestw (175): dzien D | <id>: bitwy lordow W/P (wyrownane W/P), WPO +x.x, szturmy zdobyte/odparte/stracone, twierdze N (start M), ludzie w partiach X (y% swiata), zalogi Z | ... | dominacja: brak / <id> (powod)`
  + plik `balans-krolestw.csv` (dzien;krolestwo;bitwy_lordow;wygrane;wyrownane;wyrownane_wygrane;wpo;szturmy_zdobyte;szturmy_odparte;twierdze;twierdze_start;twierdze_zdobyte;twierdze_stracone;tier_zalog;ludzie_partie;ludzie_zalogi;bitwy_z_przewaga_polnocy;straty_wlasne_przewaga;straty_wroga_przewaga;wsie_karawany_rozbite_przez_dothrakow).
  Twierdze z `CampaignEvents.OnSettlementOwnerChangedEvent` (detail `BySiege`; zdobywca i tracacy) i spisu `Town.AllTowns`/`AllCastles` raz na dobe.
- **WPO - "wygrane ponad oczekiwane":** dla bitwy lord-lord p(wygranej) = 1/(1+exp(-k ln(Ms/Mo))), k dopasowane na wszystkich bitwach przebiegu (w bazie k = 12.6 -
  przy stosunku 1.06 p ok. 68%, przy 1.2 - ok. 91%); WPO krolestwa = suma (wygrana 0/1 - p). Zero = wygrywa tyle, ile wynika z liczb i tieru; dodatnie = przewaga "jakosci"
  (teren Dothrakow, +10% Polnocy). W linii dnia gra liczy WPO ze stalym k z bazy (skrypt analizy dopasowuje k na nowo).
  **Wersja 2:** 11.9 bylo gorna granica siatki w `baza_balans.py` (`range(5,120)/10`) - krytyka slusznie; na siatce do 99.9 optimum to **k = 12.6** przy tej samej
  wiarygodnosci (-21.1), wiec wniosek sie nie zmienia: WPO bliskie zera u wszystkich wynika z gry (autobitwa prawie deterministyczna powyzej stosunku 1.25, 3.3),
  nie ze skryptu. W kodzie k = 12.6, w `p175_balans.py` siatka 0.5-50. WPO zostaje miara POMOCNICZA; progi 6.2 opieraja sie na udziale wygranych w bitwach
  wyrownanych z przedzialem ufnosci, na twierdzach i na pieniadzach.
- **Bitwy z udzialem zbioru 43 (wersja 2):** linia B175 dla KAZDEJ bitwy polowej z udzialem partii Polnocy (takze z bandami i Wolnymi Ludzmi), z polami
  `przewaga Polnocy: tak/nie (ciosow N)` i `straty A/O` - do miary zastepczej 3.4 pkt 5.
- Bez nowych danych w zapisie (liczniki od startu sesji; autotest to jedna sesja) - wiec bez SyncData i bez `SaveText.Sync`.

---

## 5. Wylaczniki (Armoury `Settings.cs` + `tools/gen_mcm.py`; CS czyta przez `Mends.ArmouryFloat`, domyslnie WLACZONE, gdy pola brak)
| Klucz (MCM, po angielsku) | Domyslnie | Co | Gdzie dziala | Kiedy zmiana dziala |
|---|---|---|---|---|
| `Army175TierGear` | TAK | 1.1-1.5 sprzet wedlug tieru | CS | od nastepnego wczytania |
| `Army175LoreArmor` | TAK | 1.6 Pentos krok 2 i Qarth (decyzja 4) | CS | od nastepnego wczytania |
| `Army175LoreArmorExtra` | **NIE** | 1.6 Dorne lzej, Qohor ciezej - propozycja audytu bez decyzji Jeffa (rozdz. 0 pkt 9) | CS | od nastepnego wczytania |
| `Army175Composition` | TAK | glowny wylacznik skladu (2.1-2.5) | CS | od nastepnego wczytania |
| `Army175DothrakiRide` | TAK | 2.1 Dothrakowie konni + dopisek pieszych do puli ROT; **wylaczany po tescie, jesli D1-D2 (6.2)** | CS | od nastepnego wczytania |
| `Army175DothrakiHorseGuard` | TAK | 2.1 straz konia przy zamianach ROT u Dothrakow (175.1b) | Armoury | od razu |
| `DothrakiRidingBonus` (0-50) | 30 | 2.1 +30 Jazdy linii konnej Dothrakow (audyt 4.1) | CS | od nastepnego wczytania |
| `Army175IronbornFoot` | TAK | 2.2 Zelazne Wyspy (z patrolami i nagroda wasalna) | CS | od nastepnego wczytania |
| `Army175NorthFoot` | TAK | 2.3 sklad Polnocy | CS | od nastepnego wczytania |
| `Army175VolantisNorvos` | **NIE** | 2.4 - propozycja audytu bez decyzji Jeffa (slonie i tak zostaja) | CS | od nastepnego wczytania |
| `Army175MinorLore` | **NIE** | 2.5 Wolni Ludzie, Smocza Skala - propozycja audytu bez decyzji Jeffa | CS | od nastepnego wczytania |
| `Army175GoldenBows` | **NIE** | 2.5 Zlota Kompania 1/3 lukow (CS zestawy + Armoury SkillsDecide) - czeka na koszyki K1 i na zgode na wyjatek od zasady 28.08 | CS + Armoury | od nastepnego wczytania |
| `NorthHardySkillBonus` (0-50) | 25 | 3.2 | CS | od nastepnego wczytania |
| `NorthHomeEdgePercent` (0-25) | 10 | 3.3 | Armoury | od razu (czytane przy ciosie) |
| `AiUpgradeHorseToArmory` | TAK | 4.1 naprawa konia za awans + wolne konie ze zbrojowni najpierw (AI) | Armoury | od razu |
| `Army175Measure` | TAK | 4.2 + 4.4 (tylko log i csv) | Armoury | od razu |
"Rycerz z koniem" - nie budowany (zalezy od pomiaru). Pulapka z CLAUDE.md 7: nowe klucze nie istnieja w `Armoury.json` Jeffa, wiec obowiazuja domyslne z kodu;
CS i Armoury 175 wgrywac RAZEM (CS bez nowego Armoury nie znajdzie pol i wezmie domyslne z kodu CS: TAK - poza kluczami z "NIE" w tabeli, dla ktorych
`ArmouryFloat(..., 0f)`: `Army175GoldenBows`, `Army175LoreArmorExtra`, `Army175VolantisNorvos`, `Army175MinorLore`). "Domyslnie WLACZONE" dotyczy wszystkiego,
co Jeff postanowil; NIE - tylko to, co zalezy od pomiaru albo od jego slowa.
Opis kazdego klucza w MCM (angielski) mowi, ze sklad i sprzet dzialaja od nastepnego wczytania.

---

## 6. Plan testu

### 6.0 Biegi (autotest wedlug zgody Jeffa z 07.10: DLL probne na czas testu, potem przywrocenie zatwierdzonych; trwale wgranie tylko na "wgraj")
| Bieg | Co | Po co |
|---|---|---|
| **B0** | zatwierdzone (Armoury 63640cb3 + CS e8d460c5) + **sam 175.0** (pomiar), nowa kampania **120 dob** | baza w tym samym formacie linii B175/CSV; kopiaT9-120 to inna wersja (bez D1, I1, F1, E1 - E1 zmienia wojny i pokoje), wiec tylko przyblizenie |
| **T1** | cala 175 z WSZYSTKIMI czesciami wlaczonymi (takze `LoreArmorExtra`, `VolantisNorvos`, `MinorLore`; `GoldenBows` wyl.), nowa kampania **40 dob** | bramki techniczne 6.1 (czy kazda czesc dziala, gdy Jeff ja wlaczy) + porownanie z B0/kopiaT9-120 do doby 40 |
| **T2** | jak w grze po wgraniu (domyslne wylaczniki), **zapis doby 362** (8-10 dob) | wczytanie starego swiata: wymiany ROT pierwszej doby, stare zbrojownie (6.1 pkt 9), umiejetnosci na zapisie |
| **T3** | domyslne wylaczniki (czesci postanowione przez Jeffa), nowa kampania **120 dob** | progi balansu 6.2; przy "sygnale" drugi bieg T3' (proby sa male) |
| (T3+) | jak T3 + czesci z rozdz. 0 pkt 9 | tylko gdy Jeff powie "wlacz" - te same progi |
| (T4) | jak T3 z `Army175DothrakiRide` = NIE albo `NorthHomeEdgePercent` = 0 | tylko gdy T3/T3' przekroczy prog - wskazac winna czesc bez przebudowy |
| **T5** (celowany) | domyslne wylaczniki, nowa kampania **60 dob**, **wymuszona wojna** Polnoc - Zelazne Wyspy i Polnoc - Westerlands od doby 1, bez pokoju do konca biegu; **dwa biegi**: `NorthHomeEdgePercent` 10 i 0 | jedyny sposob, zeby test zmierzyl +10% Polnocy (zwykly bieg: 5 bitew lord-lord w 120 dob); cel >= 15 bitew lord-lord Polnocy, w tym >= 5 w lesie Polnocy lub na sniegu |
Czas [P]: ok. 13-16 s na dobe nowej kampanii, ok. 24 s na dobe zapisu 362 - B0 i T3 po ok. 30 min, T1 ok. 10 min, T5 2 x ok. 15 min.
**T5 - narzedzie:** parametr trybu autotestu (galaz `narzedzia/autotest` CS + `tools/autotest.ps1`, np. `-ForceWar "battania:sturgia,battania:vlandia"`): w 1. dobie
`DeclareWarAction` dla par, prefiks na zawarcie pokoju dla tych par (tylko w DLL autotestu - nigdy w wersji dla Jeffa; E1/`PovertyPeace` inaczej skonczy wojne).
Nie jest czescia kodu 175 - osobny commit w galezi narzedzi. Gdy sniegu nie bedzie (pogoda), las Polnocy wystarczy; log B175 zapisuje kontekst symulacji.

### 6.1 Bramki techniczne (T1 i T2) - wszystkie musza przejsc
1. 0 bledow (`Scribe.Report`, `Log.Error`), 0 potkniec 175 w liniach logu; czas doby <= +5% wobec biegu zatwierdzonego (T1: 13.1 s, T2: 24.2 s).
2. **TierGear:** linia 1.10 - jednostek 893 +-3%, `(slotow - q)` ok. 2 550 +-5% (1.2), zapas innej klasy ok. 79 wystapien +-15%, zamiennik w tierze jednostki
   ok. 1143 par +-15% (warunki konny/plec/bandyta, 1.3), bohaterowie 0, nowe rostery = jednostki ze zmiana, kultur Essos 19; "kontrola po rozsadku: slotow ponad tier 0".
   Recznie 5 jednostek z rozpoznania w logu `ItemDump`/wzorcach: Pentoshi Footman t2 (tarcza t2, helm t1-t2), Pentoshi Pike Warrior t4, Westerlands Levy Crossbowman t2
   (belty t2), Golden Company Giltblade t3, Dornijski Wlocznik t3 (topor do rzucania zamiast oszczepu).
3. **SkillSinew:** dzis [P] "2536 jednostkom podbito 4829 umiejetnosci" - po 175 wyraznie mniej umiejetnosci (rachunek: jednostek ponad XML 986 -> 553); linie
   "Mends: <id> ... -> 175" dla t2-t4 z tarcz/beltow znikaja. Przyklady: Pentoshi Footman 1H 40 / Atl 40; Westerlands Levy Crossbowman Kusza 40; Pentoshi Pike Warrior 110/110/110.
4. **NorthHardy:** 43 jednostki; piechota t3+ Polnocy 156/155 wobec swiata BEZ Polnocy ok. 122/121 (+28% +-3; ta sama definicja broni glownej w kodzie i w skrypcie);
   brak OSTRZEZENIA; Armoury `NorthHomeEdge` liczy tez 43. **DothrakiRiders:** 7 jednostek, Jazda +30 (np. `khuzait_horse_archer` 110 -> 140).
5. **Sklad:** T1 - 20 szablonow (+3 patrole Wysp, + nagroda wasalna sturgia) z celami jak w rozdz. 2; 3 drzewa (`khuzait_nomad`, `sturgian_brigand`, `greyjoy_soldier`);
   "Dothrakowie: piesi w puli ROT (7)". T2/T3 - tylko szablony czesci wlaczonych domyslnie (log wypisuje pominiete przez wylacznik).
   T2 (zapis): wymiany ROT pieszy -> konny u Dothrakow w 1. dobie ~ 0 (linia "Konie AI (175)": `rot_plus` Dothrakow; straz konia: `straz_pieszy` + `straz_zbrojownia`
   + `straz_tabor` = konni z zamian); jezdzcy Wysp spoza Harlaw -> piechota (rot_minus Wysp > 0).
6. **Konie:** "do zbrojowni" > 0 od 1. doby, "przepadlo" ~ 0; "awanse z wolnych koni" > 0 po ok. 10 dobach; CSV pisze sie dla kazdego krolestwa.
7. **B175:** linie bitew i dnia sa, `balans-krolestw.csv` ma wiersz na krolestwo na dobe; liczba linii B175 FieldBattle lord-lord ~ liczba z H3 (+-5%).
8. Straz DTE w bitwach (CS `SkillLawWard` "PUSTE <klasa>"): nie wiecej niz w biegu zatwierdzonym (w autotescie bez gracza zwykle 0 - sprawdzamy, ze nie ma nowych).
9. **T2 - stare zbrojownie (wersja 2, liczone statycznie przy wczytaniu, bo autotest nie ma bitew z graczem):** dla glownej partii i dla partii AI wedlug krolestwa
   (a) ludzie x sloty (bron, tarcza, amunicja), dla ktorych zbrojownia nie ma sztuki do wymogu po NOWYCH umiejetnosciach (ta sama regula co `SkillLawWard`);
   (b) sztuki w zbrojowniach AI, ktorych nikt w partii nie udzwignie; (c) zakupy `AiGear` na zapisie w 8-10 dobach wobec biegu zatwierdzonego. Bez progu - liczby
   ida do Jeffa (rozdz. 0 pkt 2) i do decyzji o kolejnosci z K1 (1.9). Stare t5-t6 nie znikaja; jesli MenPurse je sprzedaje - widac w jego linii (to obieg, nie blad).
10. **Zasada valyrianska bez zmian:** linia migawki `v` = liczba jednostek z bronia t6 we wzorcu sprzed zamiany (ok. 223, jak dzis); `ValyrianWardSim` czyta migawke.

### 6.2 Progi balansu (T3 wobec B0; przy braku B0 - wobec kopiaT9-120) - warunek Jeffa "zeby jedna armia nie bila wszystkich"
Baza [P, kopiaT9-120, `baza_balans.py`]: bitew lord-lord miedzy krolestwami 116 w 40 dob / 520 w 120 dob, wyrownanych (stosunek mocy < 1.5) 15 / 69; WPO kazdego krolestwa
w 120 dobach miedzy -0.8 a +1.0; najczesciej walcza Krainy Burzy (120), Smocza Skala (107), Nocna Straz i Wolni Ludzie (po 100); Dothrakowie 21 (67% wygranych),
Polnoc 5 (80%), Zelazne Wyspy 0; zdobyczy twierdz szturmem w calym swiecie 5 w 120 dob (`WarLedger: ... wziete obleczeniem`).
**Wersja 2 (po krytyce):** autobitwa jest prawie deterministyczna (3.3), wiec WPO jest bliskie zera z natury i nie moze byc progiem (pierwsza wersja: B1 "WPO > +3.0"
przy najwiekszym |WPO| swiata +1.0 - prog nieosiagalny). Progi licza udzial wygranych z przedzialem ufnosci (dwumian: `sigma = sqrt(p0 (1 - p0) / n)`,
`p0` = udzial tego krolestwa w B0, a gdy w B0 n < 8 - 0.5), twierdze i pieniadze; przy za malej probie raport pisze jawnie **"brak danych"** (nie "zaliczone").
"Zmienione w 175" przy domyslnych wylacznikach = Polnoc, Dothrakowie, Zelazne Wyspy, Pentos, Qarth (+ Volantis, Norvos, Wolni Ludzie, Smocza Skala, Dorne, Qohor,
gdy Jeff je wlaczy).
| Nr | Miara | Prog | Rodzaj |
|---|---|---|---|
| B1 | KAZDE krolestwo: udzial wygranych w bitwach lordow WYROWNANYCH (stosunek mocy gry 1/1.5-1.5), przy n >= 8 | > p0 + 2 sigma | sygnal |
| B2 | KAZDE krolestwo: udzial wygranych bitew lordow (miara z decyzji Jeffa), przy n >= 15 | > max(85%, p0 + 2 sigma) | sygnal |
| B4 | KAZDE krolestwo: twierdze netto (zdobyte - stracone) wobec B0 | >= +3 ponad B0 w 120 dob (baza: caly swiat 5 zdobyczy) | TWARDY |
| B5 | KAZDE krolestwo wyeliminowane (0 osad) - przez kogokolwiek - gdy w B0 przetrwalo | jakiekolwiek | TWARDY |
| B6 | krolestwo zmienione w 175: twierdze stracone netto wobec B0 albo sredni tier zalog | >= 3 ponad B0 albo tier zalog < B0 - 0.3 | sygnal |
| N1 | **T5:** Polnoc - udzial wygranych w bitwach lordow o stosunku mocy 0.9-1.1, bieg +10 wobec biegu 0 | > 75% przy n >= 8 albo roznica > 2 sigma | sygnal |
| N2 | **T5:** Polnoc - wspolczynnik strat (straty wlasne / straty wroga) w bitwach z aktywna przewaga, bieg +10 wobec 0 (oczekiwane ok. -12%: x1.069 w obie strony) | spadek > 25% | sygnal |
| D1 | bankruci rodow (`budzet-rodow.csv` kolumna `bankrut`) - Dothrakowie; takze Polnoc, Zelazne Wyspy, Pentos (i wlaczone pozniej) | > B0 + 2 (baza Dothrakow d120: 1, Polnocy: 2) | Dothrakowie: TWARDY dla 2.1 po powtorzeniu w T3'; inni: sygnal |
| D2 | Dothrakowie: suma kies glow - **srednia z dob 100-120** (nie jeden dzien: w bazie d80 1.07 mln, d119 0.62 mln) i suma `saldo_modelu` z tego okna | < 50% sredniej B0 z tego samego okna | TWARDY dla 2.1 po powtorzeniu w T3' |
| D3 | glowy < 5 000 zl i rody "zagrozone" (kiesa / (-saldo dobowe) < 60 dob) - wszystkie krolestwa zmienione w 175 | > B0 + 3 | sygnal |
| D4 | Dothrakowie: sredni tier partii lordow i udzial "czeka na konia" | tier < B0 - 0.3 (partie zastygle w t1) | sygnal (dla 4.3, nie wylacza) |
| D5 | Dothrakowie: udzial "konnych bez konia w zbrojowni" (4.2 B/M) i `rot_plus`/dobe | > B0 + 10 pkt | sygnal (blad strazy konia 2.1) |
| D6 | "Zalew" Wolnych Miast: wsie i karawany Pentos, Myr, Lys, Tyrosh, Norvos, Qohor, Lorath rozbite przez Dothrakow (B175 `rodzaj` = wies albo karawana) i zabici przez nich w H3; albo ludzie w polu tych 7 miast razem (linia "Balans krolestw") | > 2 x B0; albo < B0 - 20% | sygnal |
Reguly werdyktu: TWARDY -> wylaczyc odpowiedzialna czesc i powtorzyc T3: D1/D2 -> `Army175DothrakiRide` = NIE; B4/B5 dla krolestwa zmienionego -> jego czesc
(Dothrakowie 2.1, Polnoc `NorthHomeEdgePercent`, inni - wylacznik skladu); B4/B5 dla krolestwa NIEzmienionego -> sprawdzic, czyim kosztem (jesli ofiara jest zmieniona
w 175 - jej czesc, np. oslabiona Smocza Skala przy `MinorLore`). Sygnal -> drugi bieg T3'; ten sam sygnal dla tego samego krolestwa w obu biegach = jak TWARDY.
N1/N2 -> 3.4 pkt 6. WPO z k dopasowanym na biegu - tylko informacyjnie (4.4).

**Do raportu (bez progu, informacyjnie):** miara zastepcza Polnocy w T3 (3.4 pkt 5: bitwy z udzialem zbioru 43, ciosy z mnoznikiem, wspolczynnik strat wobec B0);
zold i udzial konnych zalog Dothrakow w d1/d40 (2.1 droga c); saldo zlota zamian ROT wedlug krolestwa (4.2); WPO;
konni u lordow AI wobec bazy (12.5 / 9.1 / 8.1% w d1/d40/d120) - Dothrakowie w gore, Wyspy w dol (Smocza Skala, Wolni Ludzie - przy `MinorLore`);
ludzie w partiach i zalogach kazdego krolestwa (Polnoc 11.0% swiata) - material dla 166; zold partii Dothrakow (+26-47% wedlug rachunku); wydatki AI na sprzet
(linie zakupow AiGear/VolunteerKit - oczekiwany spadek o ok. polowe na komplet t2-t4); bankruci i glowy < 5 000 calego swiata (baza d120 w kopiaT9-120: 15 / 32,
STAN-PRAC :748);
liczniki koni 4.2 (material do 4.3).

### 6.3 Porownanie z dzisiejszym logiem `SCR\kopiaT9-120` (okno 40 dob, [P])
| Miara | Baza d40 | Oczekiwane w T1 |
|---|---|---|
| bitwy lord-lord miedzy krolestwami / wyrownane | 116 / 15 | podobnie (+-25%) |
| Smocza Skala / Krainy Burzy: bitwy, wygrane | 44 (48%) / 44 (52%) | T1 (`MinorLore` wl.): Smocza Skala mniej jazdy (10% celu) - wygrane nie wyzej niz baza + 10 pkt |
| Wolni Ludzie / Nocna Straz | 19 (63%) / 19 (37%) | T1 (`MinorLore` wl.): Wolni Ludzie slabsza jazda - wygrane nie wyzej niz baza |
| Norvos | 7 (71%) | T1 (`VolantisNorvos` wl.): mniej jazdy na otwartym - wygrane nie wyzej niz baza |
| Dothrakowie / Polnoc / Zelazne Wyspy | 2 / 1 / 0 bitew | za malo - werdykt dopiero w T3 |
| konni w partiach rodow AI / garnizonach | 9.1% / 6.7% | +-1.5 pkt (Dothrakowie w gore, Wyspy w dol - wedlug CSV krolestw) |
| Dothrakowie: kiesy glow / glowy < 5 000 / bankruci | 0.99 mln / 1 / 0 | D1-D3 w 40 dobach jako wczesne ostrzezenie |
| Polnoc: ludzie w partiach / zalogach (udzial) | 10 375 (11.0%) / 6 695 | bez progu - do 166 |
| SkillSinew przy starcie | 2536 jednostek / 4829 umiejetnosci | wyraznie mniej (6.1 pkt 3) |
| zakupy koni Stajni AI (`zakupy.log` "Stajnia AI") | 592 zakupy / 1 591 koni w 120 dob | wiecej u Dothrakow (konna droga) |

### 6.4 Narzedzie analizy
`tools/p175_balans.py` (w repo, z commitem 175.0): parsuje "Bitwa: B175", "Balans krolestw (175)", `balans-krolestw.csv`, `konie-krolestwa.csv`, `budzet-rodow.csv`
i (dla bazy bez B175) linie H3 - wypisuje tabele krolestw (bitwy, wygrane, wyrownane, WPO z k dopasowanym na biegu, twierdze, bankruci, konie) dla okien 40 i 120 dob
i werdykt wedlug 6.2 (z sigma i "brak danych"; siatka k 0.5-50). Pierwowzor: `SCR\a175\narz\baza_balans.py` (H3, okna dob, WPO) i `SCR\kryt175b\kfit.py`
(szersza siatka k, wygrane silniejszego wedlug stosunku mocy, bitwy Polnocy ze wszystkimi).

---

## 7. Podzial pracy na dwa drzewa (pliki, metody, commity)

### 7.1 CrashScribe - drzewo `SCR\noc2\a175cs` (galaz `w-toku/175-armie-cs`)
| Plik | Zmiana |
|---|---|
| `CrashScribe/src/SubModuleMain.cs` | override `AfterRegisterSubModuleObjects(bool isSavedCampaign)` -> `Army175.OnObjectsRegistered()` (try/catch + `Scribe.Report`); w `OnGameStart` -> `Army175.Reset()` (stan statyczny: slownik wyborow, flagi, liczniki); w bloku latek (:85-95) `Army175.Install(_harmony)` (postfiks ROT `Settings`) |
| `CrashScribe/src/Army175Sklad.cs` (nowy) | `Composition()` (tabela zmian 2.1-2.5 + nagroda wasalna sturgia jako dane w kodzie, kazdy wiersz pod swoim wylacznikiem), `RotPoolKeepFoot` (postfiks 2.1, warunek: pula zawiera `khuzait_nomad`), log |
| `CrashScribe/src/Army175Gear.cs` (nowy) | `TierGear()` (zakres 1.5, pula/scisle/kaskada/zapasy 1.3-1.4, klucz z cechami jednostki, ZAWSZE wlasny roster z klonami, lista z `MBObjectManager`, kopia listy Essos), `ValyrianSnapshot` (1.7 [a2]), `LoreArmor()` (Pentos, Qarth) i `LoreArmorExtra()` (Dorne, Qohor; wyl.), `GoldenBows()` (2.5, wyl.), `TierGearCheck()`, `TierGearApplied`, log 1.10 |
| `CrashScribe/src/Army175North.cs` (nowy) | `NorthSet()` (regula 3.2), `NorthHardy()` i `DothrakiRiders()` (2.1) - wspolna maszyna: zbior regula, bron glowna jak SkillsDecide, znacznik na `MBCharacterSkills`, suwak, kontrola poprawnosci +28% +-3, log |
| `CrashScribe/src/Mends.cs` | `ArmouryFloat` z `private` na `internal` (uzywa go Army175); `MendsBehavior.RegisterEvents` (:4687-4706): `TierGearCheck` po `AmmoSanity`, `NorthHardy` i `DothrakiRiders` po `SkillSinew`; DailyTick jak 1.7 pkt 3; `ValyrianWardSim` (:1755) czyta `Army175Gear.PreTierBest(c)` zamiast `BestWeaponTier(c)`, gdy migawka istnieje. Komentarz przy `SkillSinew`, ze wzorce sa juz w tierze (175) |
| `CHANGELOG.md` | wpis 175 (CS) z "Ryzyko / co sprawdzic" (CLAUDE.md 8.0) |
Uzyc istniejacych: `Mends.UniquePrefixes`/`BladePrefixes`/`IsDeadGear`/`MountOk`/`ReqSkill` (zrobic `internal` tam, gdzie trzeba), wzorzec `ArmouryFloat`, `Scribe.Line`.

### 7.2 Armoury - drzewo `SCR\noc2\a175arm` (galaz `w-toku/175-armie-arm`)
| Plik | Zmiana |
|---|---|
| `Armoury/src/Settings.cs` + `tools/gen_mcm.py` -> `McmSettings.cs` | 16 kluczy z rozdz. 5 (opisy po angielsku, z "takes effect on the next load" przy skladzie i sprzecie); poprawka opisu `CavalryNeedsMounts` (:291) |
| `Armoury/src/Stables.cs` | `Consume` zwraca zabrane (z `EquipmentElement` - licznik modyfikatora); `PayInHorses` -> najpierw wolne konie zbrojowni, potem tabor -> `AiGear.AddToArmory` + liczniki; `FreeArmoryHorses(mp)` (konie `IsPlainMount` w zbrojowni minus konni); `FilterTargets`/`NeedForUpgrades` licza tabor + wolne (AI); liczniki w `OnSettlementEntered` (nieudane); `NeedForUpgrades` internal |
| `Armoury/src/HorseCensus.cs` (nowy) | liczniki 4.2 (z wierszami wersji 2), hook ROT `ExchangeClanTroops` (prefiks: rosterowa migawka konnych, zrodlo zamienianego, `owner.Gold`; postfiks: roznice), `OnTroopRecruited`, linia dnia, `konie-krolestwa.csv`; `Reset()` dopisany do `ArmouryBehavior()` (:389) |
| `Armoury/src/RotHorseGuard.cs` (nowy) | straz konia Dothrakow (2.1, 175.1b): wolany z postfiksu `HorseCensus` na `ExchangeClanTroops`, gdy pula zawiera `khuzait_nomad` i `Army175DothrakiHorseGuard`; tabela pieszych wedlug tieru; liczniki |
| `Armoury/src/VolunteerKit.cs` | w `Postfix` (:81) liczniki "awans na konnego: kupiony / cofniety" wedlug kultury notabla (tylko gdy `Army175Measure`) |
| `Armoury/src/MountedWage.cs` | w petli `Daily` (:170-210) wywolanie `HorseCensus.CountParty(mp)` (bez drugiej petli po partiach; tez footmani Dothrakow, wolne konie) |
| `Armoury/src/NorthHomeEdge.cs` (nowy) | postfiks `SimulateHit` Priority.Low, zbior 43 + kultura rodu partii battania, warunek snieg (wszedzie)/las Polnocy, pamiec na `MapEvent`, liczniki dla B175 |
| `Armoury/src/KingdomBalance.cs` (nowy) | linia "Bitwa: B175" (bitwy krolestw i KAZDA bitwa z partia Polnocy, ze stratami stron), linia "Balans krolestw (175)" z twierdzami zdobytymi I straconymi, sredni tier zalog, ludzie w polu 7 Wolnych Miast; `balans-krolestw.csv`; `Reset()` w konstruktorze `ArmouryBehavior` |
| `Armoury/src/BattleChronicle.cs` | w `OnMapEventEnded` (:52) wywolanie `KingdomBalance.OnBattle(me)` (przed wyjsciem przy malych bitwach - B175 liczy wszystkie bitwy krolestw) |
| `Armoury/src/SkillsDecide.cs` | (tylko przy `Army175GoldenBows`) klasa dystansowa z wylosowanego zestawu, gdy zestawy jednostki maja rozne klasy dystansowe |
| `Armoury/src/ArmouryBehavior.cs` | `OnSessionLaunched`: `NorthHomeEdge.BuildSet()`, `KingdomBalance.SessionStart()`; dobowe: `HorseCensus.Daily()`, `KingdomBalance.Daily()` obok `MountedWage.Daily` (:1284); `OnSettlementOwnerChangedEvent` -> `KingdomBalance.OnOwnerChanged` (obok :533) |
| `Armoury/src/SubModuleMain.cs` | `NorthHomeEdge.ApplyAll(_harmony)`, `HorseCensus.ApplyAll(_harmony)` (obok `Stables.ApplyAll`, :76) |
| `tools/p175_balans.py` (nowy), `CHANGELOG.md` | analiza 6.4 (progi 6.2 z sigma, "brak danych"); wpis 175 (Armoury) |
| (poza 175, galaz `narzedzia/autotest`) `CrashScribe` tryb autotestu + `tools/autotest.ps1` | parametr `-ForceWar` dla biegu T5 (6.0); nigdy w DLL dla Jeffa |

### 7.3 Kolejnosc commitow (lokalnie, w swoich drzewach; bez push)
1. Armoury **175.0** pomiar (HorseCensus, KingdomBalance, VolunteerKit-liczniki, Settings `Army175Measure`, `tools/p175_balans.py`) - zbudowac, do biegu B0.
2. Armoury **175.1** kon za awans do zbrojowni + wolne konie ze zbrojowni najpierw (AI); **175.1b** straz konia Dothrakow (`RotHorseGuard`).
3. CS **175.2** sprzet wedlug tieru + migawka valyrianska + lore w granicy tieru (Pentos/Qarth; Dorne/Qohor pod osobnym kluczem) (+ `ArmouryFloat` internal, lancuch Mends).
4. CS **175.3** sklad + postfiks ROT + nagroda wasalna; Armoury - nic (klucze w Settings z kroku 1 albo osobny commit kluczy).
5. CS **175.4a** NorthHardy + DothrakiRiders; Armoury **175.4b** NorthHomeEdge.
6. CS+Armoury **175.G** Zlota Kompania (wylaczone).
(Most 175.1c z 1.9 - tylko jesli 175 ma byc wgrane przed K1; domyslnie nie budowany.)
Kazdy krok: `dotnet build` z kodem wyjscia 0 (`cd` do katalogu projektu), `python tools/gen_mcm.py` po Settings.cs, wpis CHANGELOG z "Ryzyko / co sprawdzic".

### 7.4 Zgranie z paczkami w toku (nie edytowac ich drzew)
| Paczka (drzewo) | Styk | Co uzgodnic przy scaleniu |
|---|---|---|
| **171** (dobytek rekruta, zbrojownie zalog; w `w-toku/172b-karawany`, zawarta w n174) | `OwnOf` czyta wzorce (1.9); `Tier1Root`/`BuildParents` czytaja `UpgradeTargets` - po 2.1/2.2 `khuzait_footman`, `sturgian_hardened_brigand`, `greyjoy_rider` traca rodzica (korzen spada na `Culture.BasicTroop`); naplyw konnych - jego `RecruitSources` | decyzja 171 (a)/(b) o dobytku; HorseCensus przelaczyc na `RecruitSources`; `AiGear.AddToArmory` ta sama sygnatura; 171 zamyka komplet z koniem dla echa ROT (2.1 straz konia) - po scaleniu straz liczy tylko prawdziwe konie; licznik 171 `_dMountedNoHorse` = ten sam pomiar co "konni bez konia" w 4.2 (jedna liczba) |
| **174** (n174, ce67b4d) | te same pliki: `AiGear.cs` (175 tylko wola `AddToArmory`), `ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs`/`McmSettings.cs`; szczebel/Deficit z tieru wzorca | scalenie reczne (n174 stoi na 2e235ea, bez H3/D1/I1/F1); `McmSettings.cs` generowac na nowo po scaleniu; zmierzyc hamulec awansow AI po 175 |
| **K1** (spec. `k1/docs/paczki/K1-dozbrajanie.md`; zmiany w AiGear, AiWear, ArmyClothing, QuartermasterLaw, Settings) | koszyki z tieru wzorca; sufit "+2" w spec. :337 nie istnieje (SkillsDecide); konie w zbrojowniach AI po 4.1 (K1 ich nie rusza - spec. "konie i rzedy - Stajnia") | **sufit K1 = tier jednostki**; koszyki ze wszystkich zestawow z waga 1/n (warunek wlaczenia 175.G); t3 Polnocy z 105 - czy K1 ma kupowac t4 (rekomendacja: nie - sufit tieru jednostki); **kolejnosc wgrania: K1 przed 175.2 albo razem** (K1 A-1/A9 - pokrycie tylko sztuka, ktora ktos udzwignie, sprzedaz najpierw tego, czego nikt nie udzwignie - rozwiazuje stare zbrojownie na zapisie, 1.9); `Stables` (175.1: wolne konie zbrojowni) - K1 koni nie rusza |
| **Z1** (z1: DonationXpLaw, SpoilsSeal, Settings, McmSettings) | tylko `Settings.cs`/`McmSettings.cs` (konflikt tekstowy) | scalic klucze, `gen_mcm.py` |
| **Z16** (po 175) | te same miejsca w Mends.cs (SkillSinew dla bohaterow) | robic na galeziach 175 |
| **Armoury MountLaw** (w bazie) | CS ma KOPIE listy `Essos` (pole prywatne, brak referencji CS -> Armoury) | przy zmianie listy w Armoury poprawic tez kopie w `Army175Gear` (komentarz w obu miejscach) |
| **Autotest** (galaz `narzedzia/autotest`) | parametr `-ForceWar` dla T5 | osobny commit w galezi narzedzi; DLL autotestu nigdy nie zostaje w grze |
| **166** (budzet rodu) | liczebnosc Polnocy (3.5), zold Dothrakow (2.1) | liczby z linii "Balans krolestw (175)" |

---

## 8. Ryzyka i co sprawdzic
1. **Dothrakowie bez dopisku do puli ROT = skok mocy w 1. dobie** (konni bez koni) - dopisek obowiazkowy; test T2 pkt 5. Obcy w partiach Dothrakow - straz konia
   (175.1b); start i odrodzenie - konni bez konia jak u wszystkich (2.1 droga c). Ochotnicy t2 od notabli przychodza z koniem kupionym przez notabla (VolunteerKit),
   wiec D4 (partie zastygle w t1) moze nie wystapic.
2. **Zold Dothrakow +26-47% i konie na awanse:** prognoza [S] 0.8-1.6 mln dodatkowych wydatkow w 120 dob wobec 0.62 mln w kiesach - wylacznik `Army175DothrakiRide`
   po tescie jest najbardziej prawdopodobnym wynikiem (Jeff wie, rozdz. 0 pkt 7). Lore-pomysl na pozniej: trybut Wolnych Miast dla khalow (2.1).
2a. **Zalew Wolnych Miast** (konni Dothrakowie w polu) - sygnal D6; pod murami Dothrakowie slabsi (decyzja 3a, -10%/-20% konnych przy oblezeniu).
3. **Kolejnosc sluchaczy:** TierGear musi byc przed `OnSessionLaunched` (AfterRegister) - inaczej TroopFit/ColdStart na starych wzorcach. Sprawdzic w logu znaczniki czasu:
   linia TierGear PRZED pierwsza linia Armoury sesji.
4. **Rozsadek po zamianie:** kilkadziesiat slotow zamienionych "na zapas" (tier sprzed rozsadku) - liczba w linii kontroli; slotow ponad tier po rozsadku musi byc 0.
5. **Wspoldzielone rostery:** zmiana w miejscu przebralaby bohaterow (gentry BK, notable) - wersja 2: ZAWSZE nowy roster dla zmienianej jednostki, stary nietkniety
   (1.3; CS laduje sie przed Sandbox, wiec mapa rosterow na zapisie bylaby niepelna); test: bohaterowie 0 w logu, wyglad lordow bez zmian. Ryzyko szczatkowe: kod,
   ktory porownuje rostery po referencji albo szuka ich po `StringId` w menedzerze obiektow (nowy roster nie jest rejestrowany) - wykonawca grepuje DTE/BK/Armoury
   po `EquipmentRoster`/`_equipmentRoster` przed commitem.
6. **Lance i bron jezdzcow:** zamiennik bez "couch"/nieuzywalny z siodla zepsulby jazde w bitwach z graczem - reguly 1.3; w logu liczba odrzuconych kandydatow z tego powodu.
7. **Wyglad:** korpus nizszego tieru czesto z innego materialu (ten sam material tylko w 97/275 par korpusu); kaskada bierze przedmioty innej kultury tej samej strony morza
   (294 pary) - Jeff zobaczy inne mundury (np. Pentos z tarczami Lorath). To cena reguly "tier przed kultura".
8. **Ekonomia:** tansze komplety wzorcow (-34% do -52%) -> lordowie wydaja mniej na sprzet, wiecej na zold i werbunek -> moga rosnac armie AI (6.2 informacyjnie);
   popyt warsztatow przesuwa sie w dol (174).
9. **Zapis gracza i zbrojownie AI:** zolnierze traca podbite umiejetnosci; sprzet t5-t6 w zbrojowniach nie bedzie wydawany nizszym, a gdy nic innego nie ma -
   slot PUSTY (tarcza, amunicja, bron zapasowa; pancerz dopelnia DressCode). AI do K1 nie dokupi t2-t4 (pokrycie wyzszym tierem) - stad kolejnosc "K1 przed albo
   z 175.2" (1.9) i liczniki T2 (6.1 pkt 9); Jeff wie z rozdz. 0 pkt 2.
10. **Inni:** wersja 2 - bez zmian wobec dzis (migawka sprzed zamiany, 1.9); prawdziwa regula smoczego szkla - osobno, przed doba 728.
10a. **Interpretacja +25 (3.2):** u 27 z 43 jednostek liczba nizsza niz dzis przy przewadze +28% wobec swiata; Polnoc 1. z 30 kultur - potwierdzenie Jeffa przy "wgraj".
10b. **Wolne konie ze zbrojowni (4.1):** gdy DTE gubi konie poleglych inaczej, niz zakladam, "wolne" moga byc zawyzone - licznik `awanse_z_wolnych` wobec `do_zbrojowni`
   i stan "konni bez konia" w CSV pokaze rozjazd (konni bez konia nie moga rosnac przez te zmiane).
10c. **3 jednostki z podbita umiejetnoscia przez konia/ladry (1.5)** - swiadomy wyjatek; ladry rodow t6 zostaja (barwy rodow).
11. **Polnoc t3 z 105:** bron/pancerz t4 z polki w bitwach z graczem (SkillsDecide bez sufitu) - zamierzone; jesli K1 przyjmie sufit "tier jednostki", nie kupi.
12. **Patrole, rebelie, nagrody:** zmieniamy tylko patrole Wysp i nagrode wasalna sturgia (2.2); `rebels_khuzait_template` i patrole Dothrakow juz konne - bez zmian.
13. **Rody tworzone w grze (BK)** bez wlasnego szablonu dostaja szablon kultury - zmiana obejmie tez je (audyt: 15-27 rodow na szablon kultury w dobie 120).
14. **Wylaczniki skladu dzialaja od nastepnego wczytania** (pamiec ROT na sesje) - opis w MCM; wylaczenie Dothrakow w trakcie gry nie zwroci pieszych od razu.
15. **Sila to miarka, nie symulacja** (nie zna dlugosci broni, szyku, AI formacji); liczby 1.8 sa na danych XML + zrzucie przedmiotow - w grze sprawdza je log 6.1.
16. **Czesci bez decyzji Jeffa** (Volantis/Norvos, Dorne/Qohor, Wolni Ludzie/Smocza Skala) - zbudowane i wylaczone; T1 sprawdza, ze dzialaja; balans dopiero w T3+
   po jego "wlacz" (Smocza Skala to aktywny front - 107 bitew w 120 dob).

---

## 9. Pliki robocze i rachunki (SCR, tylko odczyt)
- `SCR\a175\sprzet.md`, `sprzet.json` (per jednostka: przedmioty ponad tier, zamiennik, umiejetnosci XML / dzis / po), `zamiany.csv` (2628 slotow), `narzedzia\*.py`
  (sprzet.py, podsum.py, ownof.py, wartosc.py, tf.py, shared.py ...).
- `SCR\a175\sklad.md`, `sklad-wyliczenie.txt`, `narz\sklad.py` + `narz\propozycje.json` (zmiany stosow 2.1-2.5), `narz\polnoc.py` (zbior 43), `narz\polnoc_bitwy.py`.
- **Nowe w tej wersji:** `narz\sila175.py` -> `a175\sila175.json` (Sila wzorca krolestw: gra / XML / po zamianie / po + sklad / + Polnoc +25 / cala 175 - tabela 1.8);
  `narz\lekkie.py` -> `a175\lekkie.json` (1.6: Pentos t2-t4, Qarth, Lorath - pancerz i Atletyka po kroku 2); `narz\zold175.py` (zold i konni swiezej partii i celu, 2.x);
  `narz\baza_balans.py` (baza 6.2-6.3: bitwy lord-lord wedlug krolestw w oknach 40/120 dob, wyrownane, WPO z dopasowanym k).
  Uruchamianie: `python -I sila175.py <SCR>`, `python -I lekkie.py`, `python -I zold175.py ..\wyciag.json propozycje.json`, `python -I baza_balans.py <katalog z bitwy.log> 40 120`.
- Baza pomiarow: `SCR\kopiaT9-120\` (Armoury-2026-10-08_22-23-58.log, session-2026-10-08_22-23-56.log, `2026-10-08_22-23-58\bitwy.log`, `budzet-rodow.csv`, `zakupy.log`).
- **Wersja 2 (odpowiedzi na krytyke, tylko odczyt):** `SCR\kryt175b\sila_odp.py <SCR>` (Polnoc wobec swiata bez Polnocy w wariantach gra/po/+25, doslowne +25 ponad dzis,
  suwak +15, ranking kultur, definicja broni glownej jak SkillsDecide, Dothrakowie +30 Jazdy - korzysta z `SCR\kryt175\sila_x.py`); `SCR\kryt175b\kfit.py <katalog bitwy.log>`
  (k na siatce 0.5-99.9, wygrane silniejszego wedlug stosunku mocy, bitwy Polnocy ze wszystkimi); skrypty krytyki `SCR\krytyka175b\q1-q6.py` (q5 - budzety
  Dothrakow i Polnocy, q6 - 27 z 43). Kopia wersji 1 projektu: `SCR\kryt175b\PROJEKT-175-v1-kopia.md`.

---

## 10. Krytyka i odpowiedzi (09.10 ok. 04:30; 34 uwagi z dwoch recenzji, kazda sprawdzona w kodzie, danych albo logu)
Werdykt: **P** = przyjeta (wprowadzona), **C** = czesciowo (co przyjete, co nie), **O** = odrzucona (jedno zdanie dlaczego). W nawiasie - gdzie w projekcie.

| Nr | Waga | Uwaga (skrot) | Werdykt i odpowiedz |
|---|---|---|---|
| U1 | wazne | Klucz zamiennika (przedmiot, T, kultura) nie zna cech jednostki - wybor dla piechura trafia do jezdzca, sztuka meska do lowczyn | **P.** Klucz z cechami (konny, plec, bandyta), licznik odrzuconych wedlug powodu, bramka +-15% dla 1143/158/13 i 63 par (1.2, 1.3, 1.10, 6.1 pkt 2). |
| U2 | wazne | Dopisek do puli nie usuwa "konia z niczego": obcy w partii, odrodzenie 63% konnych, zalogi startowe ok. 77% | **P** (sprawdzone ROTTroopRecruiter.cs:33-252, GarrisonTroopsCampaignBehavior.cs:211-235 - zalogi wychodza ok. 80%). Trzy drogi opisane; droge "obcy" zamyka nowa straz konia 175.1b; start/odrodzenie - ogolna regula gry, mierzona (2.1, 4.2, 6.2 D5). Przy okazji znalezione: echo werbunku ROT daje zastepcy komplet wzorca z koniem przez `RecruitKit` (sciezka "bez zapisu") - zamyka 171 (2.1, 7.4). |
| U3 | wazne | Notable awansuja ochotnikow na konnych t2; VolunteerKit kaze kupic konia, RecruitCost dolicza cene; bez konia - kon z niczego | **C.** Prawda: droga istniala i jej nie bylo, a D4 moze sie nie spelnic - dopisane, licznik "kupiony/cofniety" (2.1, 4.2). Falsz: bez konia awans jest cofniety (VolunteerKit.cs:81), a przy `RecruitsOwnHorse` = TAK (domyslne, u Jeffa nieustawione) lord placi wyzszy zold, nie cene konia. |
| U4 | wazne | Na zapisie sloty zostana puste (SkillLawWard bez podlogi, DressCode tylko pancerz); T2 tego nie pokaze; Jeff wie tylko o "oslabieniu" | **P.** Statyczny licznik pustych slotow i nieudzwignietych sztuk w T2 (6.1 pkt 9), zdanie dla Jeffa (rozdz. 0 pkt 2), kolejnosc K1 przed albo z 175.2 (1.9). |
| U5 | wazne | "+25 ponad dzisiejsze" liczone od wartosci po zamianie - Jeff dostaje tylko "+25" | **P.** Obie liczby w rozdz. 0 pkt 3-4 i w 3.2 (dzis 153/158 wobec 150/156, po 175 156/155 wobec 122/121); interpretacja do potwierdzenia przy "wgraj". |
| U6 | wazne | +23% to wobec swiata z Polnoca; bez niej +28%; definicja broni glownej rozna w kodzie i skrypcie | **P** (przeliczone: +28.0/+28.5%). Jedna definicja: najwyzsza z 1H/2H/Drzewce jak `SkillsDecide` (+27.9/+28.5%); kontrola poprawnosci 25-31% (3.2, 6.1 pkt 4). |
| U7 | wazne | Rozdz. 0 obiecuje, ze autotest sprawdzi "nikt nie bije wszystkich", a +25 i sprzet dzialaja tylko z graczem | **P.** Rozdz. 0 pkt 5 mowi wprost, co test mierzy, a czego nie; miara zastepcza (wszystkie bitwy zbioru 43, ciosy, straty) i bieg T5 (3.4, 4.4, 6.0, 6.2). |
| U8 | drobne | CS laduje sie przed Sandbox - mapa rosterow na zapisie niepelna; w AfterRegister brak `Campaign.Characters` | **P.** Zawsze nowy roster dla zmienianej jednostki, stary nietkniety; lista z `MBObjectManager` (1.3, 8 pkt 5). |
| U9 | drobne | `MountLaw.Essos` prywatne, CS bez referencji do Armoury | **P.** Kopia listy w CS z komentarzem o zrodle, log liczby (1.3, 7.4). |
| U10 | drobne | Zakres liczony na tierach po rozsadku, latka liczy przed | **P.** Bramka porownuje `(sloty - q)` (1.2, 6.1 pkt 2). |
| U11 | drobne | `khuzait_footman` t2 nigdy nie awansuje w ROT | **P** (sprawdzone ROTTroopUpgradeModel.cs:87-109). Zdanie poprawione, licznik footmanow (2.1, 4.2). |
| U12 | drobne | ROT pamieta pule osady od 1. wywolania - warunek postfiksu po szablonie wlasciciela moze nie pasowac | **P z poprawka:** warunek po zawartosci wyniku, ale `khuzait_nomad`, nie "nomad albo tribal_warrior" - `tribal_warrior` siedzi tez w pulach Daenerys i Joraha (2.1). |
| U13 | drobne | "W Westeros poza Dorne nie ma konnego lucznika" - falsz (lowczyni Mormontow) | **P.** Poprawione (2.5). |
| U14 | drobne | Lorath pominiety w rozdz. 0 | **P.** Dopisany (rozdz. 0 pkt 10). |
| U15 | drobne | Nagroda wasalna sturgia daje jazde t4 | **P** (sprawdzone partyTemplatesROT.xml :212-217). Zamiana na `sturgian_berzerker` pod `Army175IronbornFoot` (2.2). |
| U16 | drobne | Znacznik "raz" na postaci, a umiejetnosci bywaja wspolne | **P** (sprawdzone BasicCharacterObject.cs:208, :301). Znacznik na `MBCharacterSkills` (3.2, 1.7). |
| U17 | drobne | Progi lapia tylko zdobycze zmienionych; utrata twierdz przez Dothrakow bez progu | **P.** B4/B5 dla kazdego krolestwa, nowy B6 (twierdze stracone, tier zalog) (6.2). |
| U18 | **krytyczne** | Hamulca Polnocy nie da sie zmierzyc: 5 bitew w 120 dob, brak sniegu, B1 nieosiagalny, k = 11.9 to granica siatki, kontrola statyczna nie moze zadzialac | **P z jedna poprawka.** (1) bieg celowany T5 z wymuszona wojna, +10 i 0 (6.0); (2) progi z sigma i jawnym "brak danych" (6.2); (3) prawda, ze 11.9 to granica siatki, ale na siatce do 99.9 optimum 12.6 przy tej samej wiarygodnosci - WPO bliskie zera to cecha autobitwy (powyzej stosunku 1.25 silniejszy wygral 475 z 476), nie skryptu; WPO zostaje pomocnicze (4.4); (4) szacunek z gory: +2-3% mocy partii, w bitwie rownej 50% -> 57-60%, takich bitew 14 na swiat w 120 dob (3.3); (5) kontrola statyczna nazwana uczciwie "kontrola poprawnosci" 25-31% - prog "Sila <= 112 i +5 nad druga kultura" NIE, bo Sila wymaga wag partii z doby 120, ktorych kod przy wczytaniu nie ma (3.2). Rozdz. 0 pkt 5 mowi, ze +25 oceni Jeff. |
| U19 | wazne | U 27 z 43 jednostek wynik ponizej dzisiejszych wartosci | **P** (sprawdzone `krytyka175b\q6.py`: 27). Przyklady w 3.2 i rozdz. 0 pkt 4. |
| U20 | wazne | Brak prognozy kasy Dothrakow - wylacznik prawie na pewno zadziala; D2 z jednego dnia to szum | **P** (sprawdzone `budzet-rodow.csv`: 13.7 / 7.1 tys./dobe, saldo -19.3 tys., 1.71 -> 0.62 mln, d80 1.07). Prognoza 0.8-1.6 mln wobec 0.62 mln (2.1, rozdz. 0 pkt 7); D2 ze sredniej d100-120, "dni do zera", D1/D2 twarde po T3' (6.2). Trybut khalow - pomysl na pozniej, nie pytanie teraz. |
| U21 | wazne | ROT dalej przerabia obcych na konnych bez koni; brak progu i reakcji; pytanie do Jeffa o rozszerzenie zasady 30.08 | **P bez pytania:** D5 (6.2) i straz konia 175.1b u Dothrakow - to sama zasada 30.08 ("na konnego tylko z koniem") tam, gdzie 175 robi problem; jedna zasada dla wszystkich krolestw dopiero po pomiarze `rot_plus` wedlug zrodla (2.1, 4.2). |
| U22 | wazne | Kon w zbrojowni nie liczy sie do awansu - podwojny popyt, falszywy wniosek 4.3 | **P** w 175.1 dla AI (gracz przenosi konia recznie - jedna zasada "kon musi istniec w partii"); licznik wolnych koni i warunek w regule 4.3 (4.1-4.3). |
| U23 | wazne | AiGear liczy t5-t6 jako pokrycie - AI do K1 bez tarcz i zbroi, zloto zamrozone | **P** (sprawdzone AiGear.cs:221-241; do tego `MenPurse.SellAiSurplus` sprzedaje najpierw NAJNIZSZE tiery, czyli wlasnie te uzyteczne, MenPurse.cs:336-355). Rozwiazanie jest w K1 (A-1, A9) - kolejnosc K1 przed albo z 175.2; most 175.1c opisany; liczniki T2 (1.9, 6.1 pkt 9, 7.4). |
| U24 | wazne | Projekt robi wiecej, niz Jeff postanowil (4.6 sklad, 4.9, 4.11, Wolni Ludzie, Smocza Skala) | **P** (sprawdzone: pytanie 5.7 = tylko slonie, STAN-PRAC :782-790). Te czesci zbudowane, domyslnie NIE, osobny klucz `Army175LoreArmorExtra`, jedno pytanie w rozdz. 0 pkt 9; Pentos i Qarth zostaja (decyzja 4), slonie zostaja (decyzja 7) (rozdz. 5, 2.4, 2.5, 1.6). |
| U25 | wazne | B4/B5 tylko dla zmienionych; brak miary zalewu Wolnych Miast | **P.** B4/B5 dla kazdego krolestwa (z regula "czyim kosztem"), D6 (6.2). |
| U26 | wazne | 4.1 zawiera "+30 Jazdy linii konnej" - pominiete bez slowa | **P.** Zrobione maszyna NorthHardy, suwak `DothrakiRidingBonus` 30; Sila J 95 -> 104, KL 97 -> 105 (2.1). |
| U27 | drobne | Zawezenie wariantu C do zbioru 43; snieg dziala wszedzie; przeciek Clegane | **C.** Zawezenie opisane Jeffowi (rozdz. 0 pkt 5, 3.3); Clegane: +10% tylko w partiach rodow kultury battania, przy +25 znany przeciek (3.2, 3.3). **O** dla sniegu tylko na Polnocy: lore wiaze te twardosc z zimnem, nie z miejscem, a +10% rozstrzyga tylko bitwy prawie rowne. |
| U28 | drobne | Ladry i konie dalej pompuja umiejetnosc u 3 jednostek | **P jako swiadomy wyjatek** (drugi wariant krytyki): ladry rodow Westeros sa wszystkie t6 (32), zamiana zdarlaby barwy 45 jednostkom, a umiejetnosc wiaze tylko u 2; jednorozce sa tylko t5-t6 (1.5, rozdz. 0 pkt 10). |
| U29 | drobne | Oszczepy t4-t5, proce t6 - Dorne bez oszczepow, procarze z kamieniami; schowane w 1.4 | **C.** Skutek wpisany w rozdz. 0 pkt 10, wyjatek opisany jako gotowa linia (1.4); pytania nie zadaje - regula 8b Jeffa jest doslowna ("najwyzej swojego tieru"). |
| U30 | drobne | Modyfikator konia ginie w zbrojowni AI; ROT przelicza zloto z nikad | **P.** Znane odstepstwo + liczniki "kon z modyfikatorem" i "saldo zlota zamian ROT" (4.1, 4.2). |
| U31 | drobne | Polnoc najbiedniejsza, a progi bankructw tylko dla Dothrakow | **P.** D1 i D3 dla wszystkich krolestw zmienionych w 175 (6.2). |
| U32 | drobne | Polnoc po +25 pierwsza z 30 kultur, przed Nieskalanymi - wbrew lore | **P.** Powiedziane Jeffowi z suwakiem (+15 = +20%) (rozdz. 0 pkt 3-4, 3.2). |
| U33 | drobne | 175 po cichu zmienia walke z Innymi (Nocna Straz 3 -> 0) | **P.** Migawka sprzed zamiany - dzisiejszy stan zostaje; regula smoczego szkla osobno, przed doba 728 (1.7, 1.9, 6.1 pkt 10). |
| U34 | drobne | Zlota Kompania zmienia zasade Jeffa z 28.08 | **P.** Wlaczenie `Army175GoldenBows` wymaga zgody na ten wyjatek (2.5, rozdz. 0 pkt 10, rozdz. 5). |

**Bilans:** 34 uwagi - 31 przyjetych (U12 i U18 z poprawka), 3 czesciowo (U3, U27, U29), 0 odrzuconych w calosci; odrzucone fragmenty: zawezenie sniegu do Polnocy
(U27) i dwa falszywe twierdzenia w U3 (kon z niczego u notabla, cena konia przy werbunku). **Pytania do Jeffa po wersji 2** (tylko to, co zmienia gre
poza jego decyzjami): rozdz. 0 pkt 9 (czesci audytu bez decyzji - wlaczyc?) oraz potwierdzenie interpretacji +25 przy "wgraj" (pkt 4).

---

## 11. Poprawki po recenzji kodu CS (09.10 ok. 06:00; drzewo `a175cs`, galaz `w-toku/175-armie-cs`) - co w kodzie rozni sie od tekstu wyzej
Pelny opis: wpis 175 w `a175cs\CHANGELOG.md` ("Poprawki po recenzji kodu", 26 uwag). Tu tylko to, co zmienia tekst projektu albo bramki:
- **1.3 ranking:** obowiazuje "SCISLY przed kaskada" (tak liczy `sprzet.py`, z ktorego sa liczby 1.2-1.4 i bramki 6.1), potem poziom kaskady 1-6, wyzszy tier,
  noszony, kultura oryginalu, nizszy wymog **skuteczny** (jak po prawach wagi i tieru - tak bral `sprzet.py` z `items-dump`), `StringId`. Zdanie "w ramach poziomu:
  scisly przed luznym" wyzej jest nieaktualne. Zapas innej klasy tez z warunkiem couch/brace. Pula bez przedmiotow wykutych w grze (`IsCraftedByPlayer`).
- **1.9 / 0 pkt 2 / 8 pkt 9 - kolejnosc z K1 w kodzie:** na ZAPISIE gry bez K1 w Armoury (brak `Armoury.SwapMath` i `MenUpgradeOneTierUp`) 175.2 jest
  pomijane z linia OSTRZEZENIE (a z nim lore 1.6, 175.G i NorthHardy); nowa kampania bez tej strazy. Bieg T2 (zapis 362) - tylko na Armoury ze scalonym K1.
- **1.9 i 3.2 "Styk" - sufit K1:** K1b ma sufit zakupu T+1 (`MenUpgradeOneTierUp`, dom. TAK, "gdy udzwignie"), nie T. NorthHardy ma wiec sufit "bez przeskoku
  tieru": cel = max(podstawa, min(podstawa + bonus, wymog t(T+1) - 1)) dla broni i Atletyki - Polnoc t3 konczy na 104 (nie 105) i nie udzwignie t4 ani z K1b,
  ani z polki w bitwie z graczem (zdanie 3.2 "to jest zamierzone znaczenie" przestaje obowiazywac). Rachunek: 11 z 43 przycietych (highborn_warrior 90 -> 104,
  footmany t3 i 2 pikinierzy t5 o 1), przewaga ok. +27.6% zamiast +28% (w oknie kontroli). NorthHardy dziala tylko, gdy 175.2 zadzialalo.
- **1.6 Qohor:** "najciezszy korpus w tierze" liczony w SESJI po `ArmorSanity` (w `TierGearCheck`, przed prawami i `SkillSinew`), nie przy wczytaniu.
- **2.1 nagroda wasalna Dothrakow:** `vassal_reward_troops_khuzait` - `khuzait_darkhan` t5 -> `khuzait_heavy_lancer` t5 (pod `Army175DothrakiRide`).
- **2.3 tabela:** w 8 szablonach rodow o 1 lucznika mniej (stark_bowman 7 -> 4, bolton_archer 7 -> 4, karstark_archer 8 -> 5, glover_archer 8 -> 5,
  manderly_archer 8 -> 5, umber_archer 10 -> 6, mormont_trapper 8 -> 6, cerwyn_archer 8 -> 5) - ludzi w stosach 53 -> 53 (bylo 53 -> 54).
- **6.1 pkt 2:** linia 175.2 podaje teraz pary (przedmiot, tier, kultura) z rozkladem "w tierze / T-1 / nizej" (porownac z 1143/158/13) i wystapienia;
  "bohaterowie z nowym rosterem", "nowe rostery" i zgodnosc listy Essos z Armoury - w linii kontroli w sesji. `q` jest z pierwszego przebiegu.
- **6.1 pkt 5:** T1 = **19** szablonow (stosy) + 3 patrole + 2 nagrody wasalne (sturgia, khuzait); linia skladu liczy je osobno.
- **6.1 pkt 9:** pomiar "stare zbrojownie" jest w CS (`Army175Pomiar.cs`, linia "Mends: stare zbrojownie (175 ...)", raz po `SkillSinew`, pod `Army175Measure`).
- **6.1 pkt 10:** liczbe ok. 223 porownywac z "migawka valyrianska po rozsadku" w linii kontroli (tak liczy `ValyrianWardSim`), nie z linia przy wczytaniu.
- **2.1 droga c / D5:** nowa linia CS "partie lordow AI z szablonu" (przed pierwsza doba i odrodzenia w dobie: partie/ludzie/konni wedlug krolestwa).
  Pytanie do Jeffa (bez zmiany w kodzie): odrodzenie partii Dothrakow bez koni = piesi tego samego tieru (jak straz konia 175.1b) albo zostawic.

## 12. 175b - decyzje Jeffa 09.10 ok. 05:50 (pytania po wykonaniu) - WYKONANE 09.10 ok. 06:40, NIEWGRANE
Decyzje (STAN-PRAC "DECYZJE JEFFA 09.10 ok. 05:50"): (2) "wlacz" - propozycje audytu z rozdz. 0 pkt 9 domyslnie WLACZONE; (3) "ok" - najprostsze oszczepy
dostaja tier 2 (Dornijczycy t2-t3 z oszczepami, nie z toporkami); (1) Polnoc - +15 zamiast +25 (bez wyraznego wyboru Jeffa, rada Claude).
Commity lokalne: CS `a175cs` d18290f (galaz `w-toku/175-armie-cs`, na e1649e4) i Armoury `a175arm` 2d061c9 (galaz `w-toku/175-armie-arm`, na 3dedf08), wpisy "175b" w obu CHANGELOG; oba zbudowane (kod 0).

**Wylaczniki po 175b (zmiany wobec rozdz. 5):**
| Klucz | Bylo | Jest | Co |
|---|---|---|---|
| `Army175LoreArmorExtra` | NIE | **TAK** | 1.6 Dorne lzej (korpus T-1, 3 jednostki), Qohor ciezej (najciezszy korpus w tierze, 4 jednostki, w sesji po rozsadku) |
| `Army175VolantisNorvos` | NIE | **TAK** | 2.4 (slonie zostaja) |
| `Army175MinorLore` | NIE | **TAK** | 2.5 Wolni Ludzie, Smocza Skala, Velaryon, Celtigar |
| `Army175SimpleJavelins` (NOWY) | - | **TAK** | najprostszy oszczep tier 2 (ponizej); tylko razem z 175.2 (`Army175TierGear`, straz zapisu bez K1) |
| `NorthHardySkillBonus` (0-50) | 25 | **15** | 3.2; przy 15 sufit tnie 1 z 43 (`battanian_highborn_warrior` 90 -> 104), przewaga +19.7% bron / +20.2% Atletyka (okno kontroli 19.8 +-3) |
| `Army175GoldenBows` | NIE | NIE | bez zmian (nie bylo w decyzji) |
CS ma te same domyslne, gdy pola w Armoury brak (`ArmouryFloat(..., 1f)` / 15f). `Armoury.json` Jeffa nie ma zadnego klucza 175 - obowiazuja domyslne z kodu.

**Oszczep tier 2 (pkt 3) - co i dlaczego:** RBM liczy tier oszczepu jak broni bialej (z obrazen), wiec KAZDY oszczep gry ma t4-t5 i 175.2 dawalo 24 rodzajom
t2-t3 z oszczepem toporek (zapas 1.4). CS `Army175.SimpleJavelins` przy wczytaniu, przed pula 175.2, prawami tieru i SkillSinew, ustawia
`ItemObject.TierfOverride` (jak XML `tier_override`) oszczepowi `northern_javelin_1_t2` (Pine Javelin) na tier 2 i liczy jego cene gry od nowa
(`DetermineValue`); umiejetnosci nietkniete. Wybrany JEDEN oszczep (z 5 kandydatow t4): najtanszy (54 d), wszystkie czesci kuzni t2 (grot "Javelin Head"
z kutego zelaza Iron2, drzewce sosnowe), id gry `_t2`. Rachunek `SCR\a175b` (kopia `sprzet.py` z latka tieru): sam Pine Javelin daje oszczep 24 z 24
rodzajow t2-t3 (6 swoj, 18 zamiennik: m.in. 4 Dornijczykow t3, Braavos, Sarnor, Qohor, Tyrosh, Smocza Skala, najemnicy, piraci) przy 0 zmian umiejetnosci;
dodanie dwoch "dartow" `_t2` dawalo 8 rodzajom t4 spadek Rzutu o 5-15 (ich wlasny dart przestaje wymagac 105). Skutki: wymog Rzutu 35 zamiast 105;
cena historyczna Armoury (start sesji, `ArmsPricing` wedlug tieru: kute zelazo zamiast stali, mniej dni pracy) ok. 54 -> 25-40 d [S]; zakupy AI i
wzorzec DTE t2-t3 biora Pine Javelin; pary zapasu 1.4 63 -> 42 (oszczepy 21 -> 0), slotow ponad tier 2552 -> 2544, jednostek 893 bez zmian.
Bramki 6.1 do poprawki: linia 175.2 "zapas innej klasy ... oszczepy 0" (zamiast ok. 79 wystapien zapasu - mniej o oszczepy), NorthHardy "+20% +-3" (bylo +28%),
nowa linia "najprostszy oszczep tier 2 (175b) - northern_javelin_1_t2 (Pine Javelin) t4 -> t2"; w T2/T3 (domyslne) progi 6.2 licza teraz takze Volantis,
Norvos, Wolnych Ludzi, Smocza Skale, Dorne i Qohor.

**Po 175b (STAN-PRAC 09.10 ok. 05:55 i 06:00, zapisane po zleceniu 175b):** Jeff - "zrob 130/135 piechota Polnocy srednio": dodatek do broni 0,
do Atletyki +5, dwa osobne suwaki zamiast `NorthHardySkillBonus` - do zrobienia jako nastepny krok (CS `NorthHardy` + Armoury `Settings.cs`).
