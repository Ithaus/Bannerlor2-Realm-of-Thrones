# PACZKA I1b - QARTH Z NIEWOLA + JENCY GRACZA W KRAINACH Z NIEWOLA + KANON QOHORU I LORATH (09.10.2026)

Status: projekt POPRAWIONY po krytyce (rozdz. 10) i WYKONANY w drzewie: galaz `w-toku/i1b-qarth`, commit lokalny **33038b0** (na `noc/sklad6` 0c41eee),
build Release kod 0 (bez nowych ostrzezen), gen_mcm uruchomiony, wpis w CHANGELOG drzewa (NIEWGRANE). Gra nie uruchamiana, autotest nie uruchamiany, folder gry
nietkniety, nic nie wypchniete.
**Drugi przeglad (16 uwag, rozdz. 11) - wszystkie prawdziwe, poprawione:** drugi commit lokalny **1590deb** na tej samej galezi (wpis CHANGELOG "I1b-p"), build Release
kod 0, DLL probny `SCR\test\Armoury-i1b.dll` md5 3bdb69b368f7d4a6d21969f6bdc62687. Numery linii w rozdz. 1-10 - stan 33038b0; zmiany kodu opisuje rozdz. 11.

Drzewo: `SCR\noc2\i1b`. SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`.
Sciezki `[K]` bez przedrostka: `SCR\noc2\i1b\Armoury\src\...`; numery linii - stan po commicie 33038b0 (stare numery z 0c41eee oznaczone "dawne").

Oznaczenia: **[K]** przeczytane w kodzie, **[D]** dane XML ROT (`Modules\ROT-Map\ModuleData\settlements.xml`, `ROT-Content\ModuleData\ROT_spkingdoms.xml`,
`spclans.xml`), **[P]** pomiar z logow autotestow 09.10 (`Modules\Armoury\Armoury-2026-10-09_02-39-03.log`, `_03-29-22.log`, `_03-48-35.log` - po 40 dob nowej
kampanii, wersja z I1; liczby zawsze w tej kolejnosci logow), **[S]** szacunek z rachunku, **[L]** kanon (AWOIAF, rozdzialy ksiazek), **[ocena]** moj wniosek.

Decyzje Jeffa (wiazace): 07.10 pkt 5 - "Pentos i Lorath BEZ niewolnictwa", pytanie 6a - jency biora tylko kultury z niewola w swiecie Martina (STAN-PRAC :444-448);
09.10 ok. 03:45 pkt **19 "Qarth z niewola - TAK"** i pkt **24b "Qohor i Lorath bez niewoli TAK"** z zastrzezeniem "Qohor w kanonie kupowal Nieskalanych -
sprawdzic przed wdrozeniem" (STAN-PRAC :811, :816); zasada "rozwiazanie ma objac 100%" i "jedna regula dla gracza i AI" (np. 03:45 pkt 6, 04:00 pkt 1).

---

## 0. Dla Jeffa - prostym jezykiem

Qarth dostaje niewole, jak zdecydowales: jency sprzedani przez lordow w Qarthu, Nowym Ghis, Qarkash i Miescie Kosci zostaja tam niewolnikami, zamiast wracac
do domu, a armie Qarthu po wygranej bitwie biora w niewole garstke pobitych chlopow z taborow - tak samo jak Dothrakowie i Zatoka Niewolnicza. Cena jencow
w Qarthu jest ta sama, zmienia sie tylko to, dokad ida ludzie. Skala jest mala - w calym swiecie ok. 1 czlowiek na dobe zostaje niewolnikiem Qarthu zamiast
wrocic do swojej wsi.
**Twoi jency:** jesli sprzedasz jencow u posrednika w krainie z niewola (Qarth, Zatoka, Volantis, Lys, Myr, Tyrosh, Qohor, Norvos, Valyria, Dothrakowie, Zelazne
Wyspy), zostaja niewolnikami tego miasta, jak u lordow - dotad po prostu znikali z gry; jesli ich tam wypuscisz albo zostawisz po bitwie, wracaja do domu.
**Twoja polityka karna** we wlasnym miescie lub zamku dziala tak samo na jencow lordow i twoich, w kazdej krainie: "Execution" - straceni (takze twoi
sprzedani u posrednika w Westeros - dotad wracali do domu albo szli na Mur), "Forgiveness" - wolni, wracaja do najblizszego domu (takze jency lordow
w krainie z niewola - dotad BK slal ich do pierwszego na liscie miasta ich kultury, a bandytow gubil). Przy domyslnym "Enslavement" nic sie nie zmienia.
**Prawo krainy idzie za kultura miasta:** BK po podboju powoli zmienia kulture miasta (asymilacja) - Qarth zdobyty i zasymilowany np. przez Targaryenow
przestaje byc kraina z niewola, a miasto zasymilowane przez Ghiscari staje sie nia.
Poprawiam tez stary, nieaktualny opis klimatu wsi w menu.
Qohor sprawdzilem w ksiazkach: niewola tam jest (straz miasta to Nieskalani - niewolni zolnierze kupieni w Astapor; Silnego Belwasa sprzedano do Qohoru;
kowale wedlug maestera Pola skladaja w ofierze niemowleta niewolnikow), wiec radze zostawic Qohor z niewola - tak jest w grze od 07.10. Lorath zostaje bez
niewoli: zalozyli go wyzwolency i zbiegli niewolnicy, a o dzisiejszych niewolnikach w Lorath ksiazki milcza. **Qohor czeka na Twoje slowo
(pytanie 1):** do odpowiedzi zostaje z niewola, jak od 07.10.
Miecz valyrianski - zdecydowane 04:05, paczka 177 (kanon to potwierdza: nowej stali nikt nie wykuje, Qohor tylko przekuwa).

---

## 1. Regula

**Kraina z niewola = kultura osady (miasta lub zamku) na jednej liscie `LosersFlee.SlaveCultureIds`** (`LosersFlee.cs:73` [K]). Ta sama lista rzadzi dwiema
rzeczami:
- I1 prawo jenca (`PrisonerLaw.Land`, `PrisonerLaw.cs:109` [K]): sprzedaz w warowni kultury z listy - jency zostaja niewolnikami tej osady (BK "Enslavement",
  kazdy czlowiek raz - `LosersFlee.AddSlaves`); poza lista - Westeros: do domu albo przestepca na Mur; inne krainy: do domu.
  **I1b, gracz (jedna regula z AI):** sprzedaz gracza u posrednika w krainie z niewola - niewolnicy osady, w ktorej stoi (Enslavement); przy nieodczytanej
  polityce - wolni, do domu. Wypuszczeni przez gracza i zostawieni po jego bitwie w krainie z niewola - wolni, do domu (bez Muru). Wczesniej w obu
  przypadkach ludzie znikali.
  **I1b po drugim przegladzie - polityka karna osady jedna dla AI i gracza w KAZDEJ krainie:** Execution - straceni (BK nic nie dopisuje; dotad gracz poza
  krainami z niewola szedl do domu / na Mur), Forgiveness - wolni, do domu przez `Route` (w krainie z niewola takze AI - dotad BK: Tenants do pierwszej
  osady kultury, bandyci znikali). Polityke zmienia tylko gracz (AI jej nie rusza; dawna osada gracza w rekach AI zachowuje jego wybor).
- H3 przegrani uchodza (`LosersFlee.cs:883`, `:916` [K]): zwyciezca, ktorego **frakcja** (krolestwo, a rod bez krolestwa - sam rod, np. bright_banners i
  sons_of_the_harpy - ghiscari) ma kulture z listy, bierze w niewole takze czesc pobitych taborow wsi i rybakow (5% szeregowych, 30% weteranow tier 4+
  sposrod ocalalych); reszta zwyciezcow taborow nie bierze.
- **Kultura osady jest biezaca:** BK zmienia `Settlement.Culture` na kulture dominujaca przy wczytaniu zapisu (`BKManagerBehavior.cs:162-183`) i codziennie
  przy asymilacji >= 0.55 (`CultureData.cs:205-209`) [K]. Prawo krainy (I1, I1b) idzie za nia: Qarth zdobyty i zasymilowany przez Targaryenow przestaje
  byc kraina z niewola, warownia zasymilowana przez kulture z listy - staje sie nia. Zgodne z regula "kultura osady"; lista kultur sie nie zmienia.

I1b: do listy dochodzi `qartheen`. Lorath (`nord`) i Qohor (`qohorik`) - bez zmian (rozdz. 7).

## 2. Liczby (z uzasadnieniem)

| Co | Ile | Skad |
|---|---|---|
| Kultury na liscie | 10 -> **11** (+`qartheen`) | `LosersFlee.cs:73` [K] |
| Warownie kultury `qartheen` | **4**: Qarth (ROT_town35), New Ghys (ROT_town36), Qarkash (ROT_town37), City of Bones (ROT_castle70) | [D] settlements.xml (sprawdzone ponownie skryptem) |
| Warownie z niewola w swiecie | 59 -> **63** (ghiscari 8, khuzait 7, lyseni 5, myrish 5, norvos 5, qohorik 5, sturgia 10, tyroshi 5, valyrian 2, volantine 7 + qartheen 4) | [D] |
| Krolestwo do H3 | `qarth` (kultura `qartheen`) | [D] ROT_spkingdoms.xml |
| Jency w "innych krainach bez niewoli" (Braavos, Pentos, Lorath, Qarth, Wolni Ludzie, Ib, Sarnor, Wyspy Letnie, Yi Ti - 37 warowni) | 309 / 371 / 338 ludzi na 40 dob | [P] linia `Prawo jenca (I1): dzien` |
| Udzial Qarthu w tej grupie | 4 z 37 warowni = 11% -> **[S] ok. 35-40 ludzi na 40 dob (ok. 1 na dobe)** przechodzi z "do domu" na "niewolnicy Qarthu". Liczba tylko orientacyjna (T4 informacyjny) | [S] |
| Niewolnicy swiata dzis | 877 / 218 / 893 na 40 dob -> +4-16% | [P], [S] |
| Tabory wsi w bitwach H3 (caly swiat) | 5286 / 4055 / 5004 ludzi na 40 dob, w niewole 14.9 / 4.9 / 18.9 = **0.28 / 0.12 / 0.38%** (5-19 ludzi); czesc Qarthu - kilka osob na 40 dob | [P] linia `Przegrani (H3)`, czlon `tabory wsi N: ... jency x%` |
| Sprzedaz / wypuszczenia gracza w krainach z niewola | 0 przypadkow w autotestach (gracz nie gra) - dlatego reczny krok w planie testu | [P] |
| Ludnosc regionu Qarth | 4.0 mln (`PopulationLaw.cs:62`) - 40 niewolnikow to 0.001% | [K], [S] |
| BK sprzedaz nadwyzki niewolnikow (zloto z niczego, O22) | **0** w 40 dobach | [P] linia ksiegi obiegu "BK sprzedaz niewolnikow 0" |

Nowych pokretel nie ma - lista to decyzja (kanon + Jeff), nie parametr.

## 3. Gdzie w kodzie (wykonane, commit 33038b0)

1. **`LosersFlee.cs:69-76`** - lista + `"qartheen"`; komentarz: "Qarth (I1b: decyzja Jeffa 09.10 pkt 19; ADWD 16, 23 - Qarth zyje z niewolnikow; ROT: qartheen =
   Qarth, Nowe Ghis, Qarkash, Miasto Kosci, krolestwo qarth)".
2. **`LosersFlee.cs:83` NOWY `SlaveListText()`** - jeden przebieg po `Settlement.All` (IsTown || IsCastle), licznik warowni wedlug `Culture.StringId` dla kultur z listy
   ("ghiscari 8, khuzait 7, ..., qartheen 4, ... (razem 63); id bez warowni: -" - id bez warowni = literowka, wypisane) oraz drugi czlon z `Kingdom.All`:
   "krolestwa z niewola (H3, kultura krolestwa - zwyciezca bierze tez tabory wsi): <nazwa> (qarth/qartheen), ...". Tylko log.
3. **`ArmouryBehavior.cs:1026`** (OnSessionLaunched, zaraz po `VillageClimate.Apply`) - `try { PrisonerLaw.SessionLine(); } catch (Exception e) { Log.Error(...) }`.
   `ApplyAll` idzie przy ladowaniu modulu, gdy osad jeszcze nie ma - stad osobna linia przy starcie sesji. Komentarz nasluchu `OnPrisonerReleasedEvent` (:578).
4. **`PrisonerLaw.cs`**:
   - komentarz klasy: Qarth poza "innymi krainami bez niewoli"; regula gracza w krainie z niewola; osada null = tylko sprzedaz gracza (gra: `PlayerTownVisitCampaignBehavior:592`,
     `PartyScreenHelper:616`; inne wolania `SellPrisonersAction` maja osade sprzedajacego albo kupujacego - sprawdzone w dekompilacji);
   - **`SendOffPrefix` :219, galaz krainy z niewola :235-265** - polityka odczytywana takze dla gracza; `Enslavement` -> `AddSlaves(st, n)`: przyjeci (> 0, albo 0 u AI)
     -> `done`, liczniki (`_slavePlayer` u gracza, `_woundedOnce` tylko u AI - u gracza BK niczego nie dopisywal), `AddBy(_slavesBy, ...)`, `return false`;
     `AddSlaves` < 0 u AI - BK po staremu jak dotad; u gracza (< 0 albo 0 przy n > 0) - do domu. Inna polityka: AI albo gracz z Execution -> `_slaveOther`, BK
     jak dotad (`return true`); gracz z Forgiveness / nieodczytana -> do domu. Wspolna petla `Route` (:276) dostaje `slv = land == LandSlave`;
     `Execution` poza krainami z niewola - `else if`, jak dotad;
   - **`Route` :168** - opcjonalny parametr `bool slaveLand = false` (dotychczasowe wolania bez zmian): licznik `_slaveFree` zamiast `_freeOther`, reszta identyczna
     (Mur tylko w Westeros bez niewoli);
   - **`OnReleased` :331** i **`LootEndPrefix` :397** - w krainie z niewola `Route(..., slaveLand: true)` zamiast licznika "znikaja" (`_slaveReleased` usuniety);
     ekran Aftermath dalej rozpoznany przed galezia krainy (bez dubla);
   - **linia dnia** (`Daily` :469): naglowek "krainy bez niewoli i wolni od gracza w krainach z niewola: do domu ..."; "| Westeros x, inne krainy bez niewoli y,
     krainy z niewola - wolni od gracza (wypuszczeni, zostawieni po jego bitwie, sprzedaz gracza bez Enslavement) z"; w czesci z niewola "w tym sprzedaz gracza
     u posrednika (niewolnicy osady, jak AI) p" zamiast "sprzedaz gracza - BK bez osady (znikaja jak dotad)" i "wypuszczeni ... (znikaja jak dotad)";
   - **NOWA `SessionLine` :510** - `Prawo jenca (I1b): krainy z niewola (lista H3, warownie wedlug kultury osady): <SlaveListText> | Westeros bez niewoli: ... ;
     reszta bez niewoli | prawo jenca I1 wlaczone, przegrani uchodza H3 wlaczone | gracz w krainie z niewola: ...`.
   - Bez `_freeBy` i bez zmiany `PlayerLand` (uwaga krytyki 3).
5. **`Settings.cs:588`** (opis PrisonerLawEnabled -> MCM): "... Slaver's Bay, Qarth and New Ghis, Volantis, Lys, Myr, Tyrosh, Qohor, Norvos, Valyria, the Dothraki
   and the Iron Islands (thralls) do: there captives sold, by the lords or by you at the ransom broker, become slaves of the town (in your own fiefs your criminal
   policy decides), and captives you set free or leave behind after your battles go home".
6. **`Settings.cs:807`** (opis VillageClimateFix; tabela `VillageClimate.cs:53-93`): "12 villages whose main produce cannot grow where they stand get one that can
   (no more cotton at the Wall, in Braavos, Norvos, the Vale mountains, the Riverlands, the Kingswood, the Crownlands and Sarnor, no vineyard in Lorath, no dates on
   Tarth), and 11 warm villages (Qarth, Volantis and the Rhoyne, Myr, Meereen, the old Valyrian lands, the Reach) take up cotton so the world keeps as much cotton
   as before; ..." (11 = Shirosi, Port Yhos [Qarth]; Sagora, Lanthas, Tamnuh [Volantis, Rhoyne]; Tasko [Myr]; Ulaan [Meereen]; Usek, Karakalat [Oros, Mantarys];
   Cider Hall, Berrybush [Reach]; baza bawelny 112 = 112 wedlug komentarza tabeli [K]).
7. `python tools/gen_mcm.py` -> `McmSettings.cs` (2 x HintText; 759 ustawien Armoury, RC i GT bez zmian). Wartosci domyslne bez zmian - `Armoury.json` Jeffa nie
   trzeba ruszac. W DLL: "11 warm villages", "Qarth and New Ghis", "Prawo jenca (I1b)", "qartheen" - sa.
8. `CHANGELOG.md` drzewa: wpis I1b (status NIEWGRANE), commit lokalny 33038b0.

Czego NIE ruszac: RealisticCaptivity (`RealisticCaptivity/src/FairRansom.cs:42-51` - cena wedlug mapy X > 770, Qarth juz placi pelna cene niewolnika), polityki
karnej BK, rabunku BK, SyncData (nic nowego w zapisie), listy Westeros (`PrisonerLaw.cs:54-55`), drogi AI w `SendOffPrefix` (te same warunki i liczniki).

Scalanie:
- `w-toku/k1-dozbrajanie` (3 commity, baza d486813): zmienia `ArmouryBehavior.cs` (m.in. hunk w `OnSessionLaunched` - `GarrisonKit.AddMenus` obok `IronBank.AddMenus`),
  `Settings.cs`, `McmSettings.cs` i `tools/gen_mcm.py` - te same pliki co I1b; po scaleniu **ponownie `python tools/gen_mcm.py`** i build.
- `w-toku/musztra`, `w-toku/t10-nocny-marsz` - dzis bez zadnego commita ponad 0c41eee (nic do scalania); gdy dostana zmiany w `Settings.cs` / `OnSessionLaunched` -
  jw. gen_mcm po scaleniu.

## 4. Wylaczniki

Bez nowego. Dzialaja istniejace:
- `PrisonerLawEnabled` (I1, obejmuje takze czesc gracza z I1b) - wylaczony: BK jak dotad; jency sprzedani w KAZDEJ krainie (takze Qarth) sa niewolnikami osady,
  wiec dla Qarthu jest tak samo co do losu ludzi (niewolnicy osady); przy wylaczonym I1 BK liczy rannych dwa razy, jak dotad, a jency gracza (sprzedani u
  posrednika, wypuszczeni) znikaja jak przed I1.
- `LosersFleeEnabled` (H3) - wylaczony: gra bierze jencow po swojemu, lista nie gra roli.
Osobny wylacznik "Qarth" nie ma sensu: lista jest jedna, decyzja Jeffa zamknieta; cofniecie = jedna pozycja listy.

## 5. Skutki dla gracza i AI

- **Gracz:** cena jencow bez zmian (RealisticCaptivity: Essos placi pelna cene, BK - cena niewolnika). W krainach z niewola (wszystkie 11 kultur) jency sprzedani
  u posrednika zostaja niewolnikami miasta (dotad znikali), wypuszczeni i zostawieni po bitwie wracaja do domu (dotad znikali). W Qarthu dotad wszystko to
  wracalo do domu - teraz sprzedani zostaja niewolnikami Qarthu, jak u lordow. Polityka karna jego osady obejmuje tak samo jego sprzedaz i sprzedaz lordow,
  w kazdej krainie (Execution - straceni, Forgiveness - do domu).
- **Kultura miasta (BK):** prawo krainy idzie za biezaca kultura miasta - po podboju i asymilacji (BK) Qarth moze stracic niewole, a inne miasto ja zyskac.
- **AI:** lordowie Qarthu sprzedaja jencow w swoich 4 warowniach - ok. 1 czlowiek na dobe trafia do ludnosci BK Qarthu jako niewolnik (BK: niewolni pracuja
  i wplywaja na bezpieczenstwo i lojalnosc osady wedlug polityki "Enslavement", jak w Volantis czy Meereen). Armie Qarthu biora w niewole kilka osob z taborow
  na 40 dob. Wsie i miasta wrogow Qarthu (dzis wojna z Targaryenami) dostaja z powrotem o tyle mniej ludzi.
- Kultury, cena, werbunek, rozmiar armii - bez zmian.

## 6. Zamkniety obieg (nic z niczego, nic w nicosc)

| Droga | Przed I1b | Po I1b | Obieg |
|---|---|---|---|
| Sprzedaz przez AI w warowni Qarthu | do domu (`SendHome`) | niewolnicy osady (`AddSlaves`, kazdy raz) | ludzie - zamkniety; zloto za jencow z niczego (gra: `SellPrisonersAction.ApplyInternal:83/:88`, `GiveGoldAction` z `null`) - bez zmian, 164b otwarte |
| Patrol BK wracajacy do Qarthu | do domu | niewolnicy osady (kod BK, jak w innych krainach z niewola) | zamkniety |
| H3: zwyciezca z krolestwa Qarth, tabory wsi | uciekaja do domu | 5% / 30% ocalalych do niewoli, reszta do domu | zamkniety (plan H3 sprawdza K + J + F = n) |
| Sprzedaz przez **gracza** w krainie z niewola (11 kultur; w Qarthu dotad do domu) | **znikali** (BK bez osady) | niewolnicy osady (`AddSlaves`, jak AI); Execution w jego osadzie - straceni (jak AI); Forgiveness / nieodczytana polityka / blad w prefiksie - do domu | ludzie - zamkniety; zloto z niczego jak u AI (bez zmian, 164b otwarte) |
| Polityka karna wybrana przez gracza (jego osady), sprzedaz AI i gracza | Execution: AI straceni, gracz poza krainami z niewola do domu / na Mur; Forgiveness w krainie z niewola: AI - BK Tenants do PIERWSZEJ osady kultury, bandyci znikali | Execution: straceni (AI i gracz, kazda kraina); Forgiveness: do domu przez `Route` (AI i gracz, kazda kraina; bandyci - pula wyrzutkow albo "z szablonu") | jedna regula; straceni - swiadomie (wybor gracza), licznik `_executed` |
| Sprzedaz gracza we WLASNYM miescie w krainie z niewola z decyzja BK `decision_slaves_sell` | loch -> niewolnicy | posrednik -> niewolnicy (szybsza droga niz loch) | BK `SellSurplusSlaves` (`BKPartyBehavior.cs:669-676`, :1056) placi za nadwyzke zlotem z niczego (O22) - I1b otwiera graczowi szybsza droge; mierzy ksiega obiegu "BK sprzedaz niewolnikow" (R1) |
| Wypuszczeni przez gracza / zostawieni po jego bitwie w krainie z niewola | **znikali** | wolni, do domu (`Route` -> `SendHome`) | zamkniety |
| BK sprzedaz nadwyzki niewolnikow (O22) | - | mozliwa, gdy niewolnych za duzo wzgledem udzialu BK | zloto z niczego, ale **0 w 40 dobach** [P]; mierzy ksiega obiegu (osobna sprawa) |

Paczka I1c (z pierwszej wersji projektu) wchodzi w I1b - to ta sama galaz `SendOffPrefix` i te same dwa miejsca (`OnReleased`, `LootEndPrefix`); bez tego I1b
otworzylby "nicosc" w 4 warowniach, w ktorych dzis jency gracza wracaja do domu.

## 7. Kanon: Qohor i Lorath

Zrodla czytane 09.10 jako surowy tekst stron AWOIAF (wiki cytuje rozdzialy ksiazek; WebFetch blokowany, pobrane przez `action=raw` do `SCR\kanon-web`):
https://awoiaf.westeros.org/index.php/Slavery , /Qohor , /Lorath , /Qarth , /New_Ghis , /Belwas , /Three_Thousand_of_Qohor , /City_watch_(Qohor) ,
/Valyrian_steel , /Tobho_Mott , /Yi_Ti , /Free_Cities . Konkordancja Citadel (westeros.org) - 403, tekstu ksiazek w sieci brak.

**Qohor - NIEWOLA JEST [L]:**
- **Glowny dowod - maester Yandel (TWOIAF, "The Free Cities: Qohor")** nazywa obrone miasta Nieskalanymi-niewolnikami. AWOIAF, City watch (Qohor), streszczajac
  Yandela: "a small city watch supports Qohor's primary defense, the Unsullied slave soldiers". Niewolni zolnierze jako stala obrona miasta = niewola w prawie
  i w budzecie miasta.
- Jorah (ASOS 8): od Trzech Tysiecy z Qohoru straz miasta to wylacznie Nieskalani, kazdy z warkoczem na wloczni; Qohor wyslal posla do Zatoki Niewolniczej, zeby
  ich KUPIC (AWOIAF Three Thousand of Qohor, ASOS 8).
- Silny Belwas, niewolnik z Meereen, zostal sprzedany do Qohoru, a stamtad do Pentos (ACOK 63).
- Maester Pol (TWOIAF, Qohor; AWOIAF Valyrian steel): kowale Qohoru skladaja krwawe ofiary - takze z niemowlat niewolnikow - probujac dorownac stali valyrianskiej.
- Jedyny trop "przeciw": zdanie AWOIAF (strona Slavery), ze w Qohorze, "jak w Pentos", mozni kupuja niewolnikow wbrew prawu - przypis wskazuje tylko ACOK 63,
  a ten rozdzial mowi wylacznie o sprzedazy Belwasa; o zakazie w Qohorze nie pisze ani TWOIAF, ani strona Qohor. Slaby dowod.
- Werdykt [ocena]: Qohor trzyma niewolnikow (co najmniej straz i ofiary; handel przez miasto). Nawet przy zakazie "jak w Pentos" niewola jest faktem - a Pentos
  zostal bez niewoli z decyzji Jeffa 07.10, bo tam zakaz narzucil Braavos i miasto wycofalo sie z handlu (ADWD 1, TWOIAF); o Qohorze nic takiego nie wiadomo.

**Lorath - BEZ NIEWOLI [L]:** wyspy zalozyli wyznawcy Slepego Boga Boasha, ktorzy uczyli, ze wszyscy sa rowni, i (AWOIAF wedlug TWOIAF) sami nie trzymali
niewolnikow; wyspy staly sie schronieniem wyzwolencow i zbieglych niewolnikow, z ich wiosek wyroslo miasto (TWOIAF "The Free Cities: Lorath"). Dzisiejszy
Lorath handluje glownie z Braavos, Norvos i Ib (dorsz, foki, morsy, tran), walczyl razem z Braavos i Pentos przeciw Triarchii. Zadne zrodlo nie wspomina
o niewolnikach w dzisiejszym Lorath. Werdykt: bez niewoli - tak jak w kodzie od 07.10 (`nord` poza lista).

**Qarth (dla porzadku) [L]:** Xaro - wspanialosc Qarthu stoi na grzbietach niewolnikow (ADWD 16); Hizdahr - Qarth zalezy od niewolnikow (ADWD 23); Nowe Ghis
(w ROT kultura qartheen) - miasto zalezne od niewoli (ADWD 23). Zgodne z decyzja Jeffa pkt 19.

**Yi Ti (sprawdzone przy okazji, bez zmian):** strona Yi Ti nie wspomina niewoli; jedyny slad - Yi Ti bierze danine m.in. w niewolnikach od Jogos Nhai (TWOIAF).
Za malo na targ niewolnikow; ROT ma 1 warownie "Yi Ti Exiles" - zostaje wolna [ocena].

**Rekomendacja dla Jeffa:** Qohor ZOSTAJE z niewola (bez zmiany kodu - jest na liscie od 07.10), Lorath bez niewoli (bez zmiany). Twoje "Qohor bez niewoli - TAK"
z pkt 24b przeczy kanonowi - potrzebne potwierdzenie (pytanie 1). Gdyby Jeff jednak chcial Qohor bez niewoli: jedna pozycja listy (usunac `"qohorik"`), skutek
31 / 8 / 10 ludzi na 40 dob [P] wraca do domu zamiast niewoli.

**24a (stal valyrianska):** zdecydowane 04:05, paczka 177 (kanon to potwierdza: nowej stali nikt nie wykuje, Qohor tylko przekuwa - ASOS 32, TWOIAF Qohor;
Tobho Mott z Qohoru przekul Lod na dwa miecze). To nie jest czesc I1b.

**Inna lista w dokumentach:** `PROJEKT-KIESA-LUDU-2026-10-09.md` L8 / H6 ma wlasna liste krolestw niewolniczych (bez Zatoki i Valyrii, Qohor "wolny") - przy
wdrazaniu 173 uzyc `LosersFlee.SlaveCulture`, nie osobnej listy (jedna regula na jedno zjawisko).

## 8. Plan testu

Budowa (zrobione): `dotnet build Armoury/Armoury.csproj -c Release -p:GameLibs=...\lancuch\libs` - kod wyjscia 0; `python tools/gen_mcm.py`;
`grep "Qarth (with New Ghys, Qarkash and the City of Bones)"` i `grep "11 warm villages"` w `McmSettings.cs` - po 1 trafieniu (po drugim przegladzie).

Autotest (po scaleniu na sklad, jeden przebieg): nowa kampania **40 dob** + zapis doby 362 (**8 dob**). Linie logu i progi przejscia:

| # | Linia | Prog |
|---|---|---|
| T1 | `Prawo jenca (I1b): krainy z niewola (lista H3, warownie wedlug kultury osady): ...` (raz na sesje, w obu przebiegach) | **nowa kampania** (kultury z XML): zawiera `qartheen 4`, `(razem 63)`, `id bez warowni: -` (twardy). **Zapis** (BK zmienia kulture osad przy wczytaniu i po asymilacji - `BKManagerBehavior.cs:162-183`, `CultureData.cs:205-209`): twardo `qartheen N` z N >= 1 i `id bez warowni: -` albo tylko id, ktore w linii nowej kampanii mialy warownie (wtedy zmiana kultury przez BK - zapisac do raportu); liczby warowni i `razem` - informacyjnie (roznica wobec nowej kampanii = warownie, ktore zmienily kulture) |
| T1b | ta sama linia, czlon `krolestwa z niewola (H3, kultura krolestwa ...)` | zawiera `qarth/qartheen` (twardy, oba przebiegi) |
| T1c | ta sama linia, czlon `rody bez krolestwa z niewola (H3, kultura rodu ...)` | informacyjny; start ROT: `bright_banners/ghiscari`, `sons_of_the_harpy/ghiscari` (spclans.xml - is_minor_faction, bez super_faction) |
| T2 | `Prawo jenca (I1): dzien N` | nowa kampania 40/40 linii; zapis **>= 8 linii** (pierwsza linia po wczytaniu to niepelna doba - moze byc zerowa; wyjsciowy log 02-52-25 ma 9 linii); kazda `potkniecia 0` i `latki: sprzedaz tak, patrol BK tak, Aftermath bitwy gracza tak`; nigdzie `znikaja jak dotad` (twardy) |
| T4 | ta sama linia, `niewolnikow N (z patroli BK ..; ...)` | **tylko informacyjny** (bez progu): suma `qartheen` za 40 dob, [S] ok. 35-40; Qarth moze nie miec jencow, gdy nie walczy |
| T5 | `Przegrani (H3): dzien` | 40/40 linii dobowych; 0 bledow `LosersFlee.Plan`; tabory wsi "jency" w calym swiecie **<= 1%** (dzis 0.1-0.4%, 5-19 ludzi na 40 dob) |
| T6 | CrashScribe `session-*.log`, `Armoury.log` | 0 nowych bledow Armoury (`PrisonerLaw.SessionLine`, `PrisonerLaw.Route`, `PrisonerLaw.SendOffPrefix` itd.) |
| T7 | czas w linii I1 | **nowa kampania:** srednia < 1 ms i maksimum < 3 ms (stan wyjsciowy [P], logi 02-39-03 / 03-29-22 / 03-48-35: srednio 0.17 / 0.18 / 0.29 ms, max 0.6 / 0.6 / 1.7 ms). **Zapis:** maksimum < 6 ms (stan wyjsciowy, logi 02-52-25 / 03-40-35 / 04-03-18: max 3.6 / 4.0 / 3.7 ms - zawsze w pierwszej pelnej dobie po wczytaniu, np. 869 sprzedanych naraz; srednio 1.0 / 1.0 / 0.9 ms) |
| T8 | ksiega obiegu `BK sprzedaz niewolnikow` | dalej 0 albo male; > 0 = zapisac kwote do raportu (O22, osobna sprawa) |

Twardym dowodem zachowania I1 dla Qarthu jest T1 (lista) razem z recznym krokiem R1 - autotest nie gra gracza (0 przypadkow).

Reczne (Jeff, w grze, gdy bedzie w Essos):

| # | Krok | Co ma wyjsc w logu (linia `Prawo jenca (I1): dzien` tej doby) |
|---|---|---|
| R1 | sprzedac N jencow (szeregowych) u posrednika w karczmie w Qarthu (albo New Ghys / Qarkash) | `w tym sprzedaz gracza u posrednika (niewolnicy osady, jak AI) N`; w nawiasie kultur niewolnikow `qartheen` wieksze o N; `potkniecia 0`. Jesli to WLASNE miasto gracza z decyzja BK "sell slaves" (`decision_slaves_sell`) - w nastepnych dobach odczytac w ksiedze obiegu wiersz `BK sprzedaz niewolnikow` (zloto z niczego za nadwyzke, O22) i zapisac kwote |
| R2 | wypuscic jencow (ekran oddzialu) albo zostawic ich na ekranie Aftermath w krainie z niewola | `krainy z niewola - wolni (...) M` > 0, `do domu` rosnie; zrodlo `wypuszczeni przez gracza` / `jency zostawieni po bitwie gracza` |
| R3 | we wlasnym miescie ustawic polityke karna Execution, sprzedac N jencow u posrednika (dowolna kraina, takze Westeros) | `straceni (polityka Execution osady - ...) N`; `sprzedaz gracza` w "wedlug zrodla" bez zmian (straceni nie ida przez `Route`); Forgiveness - `do domu` +N |

Zapis: I1b nic nie zapisuje (lista statyczna, liczniki dobowe) - stary zapis dziala od pierwszej doby po wczytaniu; T1 (progi zapisu), T1b, T2 musza przejsc takze po
wczytaniu.

## 9. Pytania do Jeffa (tylko zmiany rozgrywki spoza jego decyzji)

1. **Qohor:** ksiazki pokazuja niewole w Qohorze (straz z kupionych Nieskalanych - maester Yandel nazywa ich niewolnymi zolnierzami, Belwas sprzedany do Qohoru,
   ofiary z niemowlat niewolnikow wedlug maestera Pola). Rekomenduje: Qohor ZOSTAJE z niewola (jak w grze od 07.10), Lorath bez niewoli. Zgoda? (Twoje "TAK"
   z 24b bylo pod warunkiem sprawdzenia kanonu.) **Status 24b: OTWARTE** - kod bez zmian (Qohor z niewola, jak od 07.10); przy NIE - usunac `"qohorik"`
   z `LosersFlee.SlaveCultureIds` (jedna pozycja listy).
2. **Cena jencow - jedna regula dla gracza i lordow (RealisticCaptivity, osobna paczka, POZA I1b):** dzis (`RealisticCaptivity/src/FairRansom.cs:42-51` [K])
   cena zalezy od tego, gdzie stoi GRACZ (`Settlement.CurrentSettlement`), i dotyczy kazdej wyceny bez lorda-sprzedawcy (`sellerHero == null`): gdy gracz stoi
   w Westeros, ulamek pasera dostaja takze lochy miast AI w Essos (codzienna sprzedaz 10% lochu, `PartiesSellPrisonerCampaignBehavior:85`) i rozwiazane partie
   bez dowodcy; lord AI z dowodca dostaje w Westeros pelna cene, gracz - ulamek. Rekomenduje jedna regule: cena wedlug prawa krainy OSADY SPRZEDAZY (osada
   sprzedajacego albo kupujacego, ta sama lista co I1) dla gracza i lordow - w krainach bez niewoli cena pasera, w krainach z niewola pelna. Robic?
3. **Polityka karna (zrobione po drugim przegladzie, do Twojego wgladu):** Execution i Forgiveness w Twojej osadzie dzialaja tak samo na Twoja sprzedaz
   i na sprzedaz lordow, w kazdej krainie (Twoja zasada "jedna regula dla gracza i AI"). Jedyna widoczna zmiana: sprzedajesz jencow u posrednika we wlasnym
   miescie w Westeros z polityka Execution - sa straceni (dotad wracali do domu / szli na Mur). Jesli wolisz, zeby Twoja wlasna sprzedaz zawsze szla
   do domu - jedno slowo, przywroce wyjatek.

## 10. Krytyka i odpowiedzi (09.10, przed wykonaniem)

Kazda uwage sprawdzilem w kodzie, logach i zrodlach. Wszystkie 9 przyjete.

| # | Uwaga (waga) | Sprawdzenie | Odpowiedz / co zmienione |
|---|---|---|---|
| 1 | Sprzedaz gracza w Qarthu zamieni sie z "do domu" w "nicosc"; to samo wypuszczeni i zostawieni; regula gracza i AI sie rozjezdza; opis MCM nieprawdziwy dla gracza (wazne) | PRAWDA. Dawne `PrisonerLaw.cs:233` (`_slavePlayer`, `return true`), :332, :394 (`_slaveReleased`). BK `SendOffPrisoners`: osada null -> `return`. Osade null daje w grze tylko sprzedaz gracza (`PlayerTownVisitCampaignBehavior:592`, `PartyScreenHelper:616`) - wiec nowa galaz nie lapie przypadkiem sprzedazy AI. Uwaga: decyzja Jeffa 04:00 dotyczy nocnego marszu, ale ta sama zasada (jedna regula dla gracza i AI, "na 100%") stoi w jego innych decyzjach | **Przyjete, I1c wchodzi w I1b.** Gracz: Enslavement -> `AddSlaves` jak AI (`return false`); Execution w jego osadzie -> jak BK u AI; Forgiveness / nieodczytana / nie da sie dopisac -> do domu (`Route`), a nie w nicosc. Wypuszczeni i zostawieni w krainie z niewola -> `Route` (do domu). Rozdz. 0, 1, 3, 5, 6, 8 (R1, R2) poprawione; opis MCM mowi o sprzedazy "by the lords or by you at the ransom broker" |
| 2 | Test nie sprawdza czesci H3; T5 ze zlym stanem wyjsciowym i progiem 5-15x ponad dzis (wazne) | PRAWDA. Moje przeliczenie 3 logow (czlon `tabory wsi N: ... jency x%` linii `Przegrani (H3)`): 5286 / 4055 / 5004 ludzi, jency 14.9 / 4.9 / 18.9 = 0.28 / 0.12 / 0.38%. `BattleLine` pomija bitwy < `BattleChronicleMinMen` (`LosersFlee.cs:1269`), linia dobowa nie dzieli wedlug krolestwa | **Przyjete.** `SlaveListText` ma czlon z `Kingdom.All` (krolestwa z kultura z listy); nowy twardy prog T1b `qarth/qartheen`. T5: stan 0.1-0.4% (5-19 ludzi), prog <= 1%. Rozdz. 2 poprawiony |
| 3 | `_freeBy`, zmiana sygnatury `Route` i `PlayerLand` - diagnostyka ponad zakres, T3 to tautologia (drobne) | PRAWDA: `Land()` sprawdza liste niewoli przed Westeros / wolnymi - `qartheen` z lista nie trafi do "innych krain bez niewoli" | **Przyjete.** Bez `_freeBy`, bez T3, `PlayerLand` bez zmian. `Route` dostal tylko opcjonalny parametr `slaveLand` - potrzebny funkcjonalnie (uwaga 1), zeby wolni od gracza w krainach z niewola nie liczyli sie jako "inne krainy bez niewoli"; 4 stare wolania bez zmian |
| 4 | Trzy rozne oczekiwania dla udzialu Qarthu, T4 bez progu (drobne) | PRAWDA | **Przyjete.** Jedna liczba [S] ok. 35-40; T4 tylko informacyjny; twardy dowod = T1 + R1 |
| 5 | "Qarth zachowuje sie tak samo przy wlaczonym i wylaczonym I1" - nieprawda co do rannych (drobne) | PRAWDA: przy wylaczonym I1 BK dopisuje `GetRosterCount` = Number + WoundedNumber | **Przyjete.** Rozdz. 4: "tak samo co do losu ludzi; przy wylaczonym I1 BK liczy rannych dwa razy, jak dotad" (+ jency gracza znikaja jak przed I1) |
| 6 | Niewolnicy swiata posortowani, a nie w kolejnosci logow (drobne) | PRAWDA: w kolejnosci 02-39, 03-29, 03-48 = 877 / 218 / 893 (inne krainy 309 / 371 / 338 - dobrze) | **Przyjete.** Rozdz. 2 i zasada "liczby w kolejnosci logow" w naglowku |
| 7 | Scalanie: brak `w-toku/k1-dozbrajanie`, musztra i T10 bez commitow (drobne) | PRAWDA: k1 = 3 commity na d486813, zmienia `ArmouryBehavior.cs` (hunk w `OnSessionLaunched`), `Settings.cs`, `McmSettings.cs`, `tools/gen_mcm.py`; musztra i T10 = 0c41eee | **Przyjete.** Rozdz. 3 "Scalanie"; gen_mcm po scaleniu |
| 8 | Odpowiedz o mieczu valyrianskim nieaktualna (drobne) | PRAWDA: STAN-PRAC "DECYZJA JEFFA 09.10 ok. 04:05 (24a) - TAK -> paczka 177" | **Przyjete.** Rozdz. 0 i 7 - jedno zdanie, zdecydowane |
| 9 | Brak cytatu po angielsku, Yandel jako glowny dowod; zla sciezka FairRansom (drobne) | PRAWDA: `SCR\kanon-web\City_watch_Qohor.txt` i `Qohor.html` ("slave soldiers from Astapor", ASOS 8); `FairRansom.cs` lezy w `RealisticCaptivity/src` | **Przyjete.** Rozdz. 7: cytat (13 slow, AWOIAF streszczajace Yandela - tekst TWOIAF niedostepny w sieci, zaznaczone), Yandel jako glowny dowod; sciezka `RealisticCaptivity/src/FairRansom.cs:42-51` (rozdz. 3 i CHANGELOG) |

## 11. Drugi przeglad (09.10, po wykonaniu) - 16 uwag

Kazda uwage sprawdzilem w kodzie drzewa, dekompilacji gry i BK, danych ROT i logach. Wszystkie 16 prawdziwe, zadna odrzucona (uwagi 9-12 dubluja 2, 3, 11, 1).
Kod: commit lokalny **1590deb** na `w-toku/i1b-qarth`, wpis CHANGELOG "I1b-p"; build Release kod 0; DLL probny `SCR\test\Armoury-i1b.dll` md5 3bdb69b368f7d4a6d21969f6bdc62687.

| # | Uwaga (waga) | Sprawdzenie | Co zmienione |
|---|---|---|---|
| 1 | T1 "qartheen 4 / razem 63" twardo takze na zapisie, a BK zmienia kulture osad; rozdz. 0 i 5 nie mowia, ze prawo idzie za kultura miasta (wazne) | PRAWDA: `BKManagerBehavior.cs:162-183` (OnGameLoaded: `Settlement.Culture = DominantCulture`), `CultureData.cs:205-209` (asymilacja >= 0.55) | T1 rozdzielony: nowa kampania twardo, zapis - `qartheen` >= 1, `id bez warowni` z wyjatkiem zmian kultury, liczby informacyjnie. Rozdz. 0, 1, 5 + opis MCM ("A land follows the present culture of its town or castle") + komentarz `SlaveListText` |
| 2 | Execution: w krainie z niewola obejmuje sprzedaz gracza, poza nia nie (`!player &&`) - dwie reguly; opis MCM prawdziwy tylko w polowie swiata (drobne) | PRAWDA: dawne `PrisonerLaw.cs:264` i `:266`; BK `SendOffPrisoners` przy Execution nic nie robi (`SettlementPatches.cs:37-76` - brak galezi) | `SendOffPrefix`: `pol` raz po krainie; `Execution` -> straceni dla AI i gracza w kazdej krainie (`_executed`, `return true`). Wybrany wariant "jedna regula" (zasada Jeffa); pytanie 3 w rozdz. 9 - do wgladu, z droga powrotu |
| 3 | Forgiveness w krainie z niewola: AI do BK (Tenants do PIERWSZEJ osady kultury, bandyci znikaja), gracz przez `Route` (drobne) | PRAWDA: `SettlementPatches.cs:42-74` - `FirstOrDefault(x => x.Culture == pair.Key)`, pomija `Occupation` 15 (Bandit) i `IsBandit` | Forgiveness (AI i gracz) w krainie z niewola -> `Route(..., slaveLand: true)`, `return false` (BK nie dopisuje Tenants - bez dubla); licznik `_slaveFree` ("krainy z niewola - wolni (...)"). Komentarz poprawiony |
| 4 | `catch` u gracza przed `done` -> `return true` -> BK z osada null nic, jency znikaja (drobne) | PRAWDA: `SettlementPatches.cs:32-35` | `catch`: `if (done \|\| !player \|\| st == null) return !done;` - gracz idzie do petli `Route`; `st`, `land` inicjowane przed `try` |
| 5 | Linia startowa bez rodow bez krolestwa; H3 patrzy na `win.MapFaction.Culture` (drobne, log) | PRAWDA: `LosersFlee.cs:883`; ROT `spclans.xml`: bright_banners i sons_of_the_harpy (ghiscari, is_minor_faction, bez super_faction) - jedyne takie rody w danych (skrypt) | `SlaveListText`: czlon "rody bez krolestwa z niewola (H3, kultura rodu - tez biora tabory wsi)" z `Clan.All` (`Kingdom == null`, `!IsEliminated`, `SlaveCulture`). T1c informacyjny |
| 6 | Rozdz. 6: "zloto za jencow placi kasa miasta (164b)" - nieprawda (drobne) | PRAWDA: `SellPrisonersAction.ApplyInternal:83` (`GiveGoldAction.ApplyBetweenCharacters(null, ...)`), `:88` (`ApplyForPartyToSettlement(null, ...)`); PROJEKT-EKONOMIA-OBIEG :527 - 164b do zrobienia | Rozdz. 6: "zloto za jencow z niczego (gra) - bez zmian, 164b otwarte" (wiersz AI i gracza) |
| 7 | 24b Qohor bez sladu, ze czeka na Jeffa (drobne) | PRAWDA: STAN-PRAC 03:45 pkt 24b "TAK" z zastrzezeniem kanonu; I1b rekomenduje odwrotnie | CHANGELOG (I1b Status + I1b-p Ryzyko), rozdz. 0 i 9 (pytanie 1 - "Status 24b: OTWARTE"), linia w STAN-PRAC |
| 8 | T7 "< 2 ms na dobe (dzis 0.4-0.7)" nie zgadza sie z logami (wazne) | PRAWDA (przeliczone skryptem): nowa kampania srednio 0.17 / 0.18 / 0.29, max 0.6 / 0.6 / 1.7 ms; zapis max 3.6 / 4.0 / 3.7 ms (druga linia = pierwsza pelna doba po wczytaniu) | T7: nowa kampania srednia < 1 ms i max < 3 ms; zapis max < 6 ms; stan wyjsciowy wpisany |
| 9 | = 2 (wazne): Execution gracz / AI w lennie gracza | PRAWDA (jak 2) | jak 2 |
| 10 | = 3: Forgiveness - dwa skutki | PRAWDA (jak 3) | jak 3 |
| 11 | Straceni od gracza w krainie z niewola w `_slaveOther`, nie w `_executed` (drobne) | PRAWDA: dawne `:264`, linia dnia `:487`, `:496` | Execution zawsze `_executed`; `_slaveOther` = tylko sprzedaz AI przy nieodczytanej polityce (etykieta "sprzedaz AI przy nieodczytanej polityce BK (BK jak dotad)") |
| 12 | = 1: T1 twardo w zapisie (drobne) | PRAWDA (jak 1); dzis liczby wsi wedlug klimatu (T8 klimatu) w nowej kampanii i zapisie 362 identyczne | jak 1 |
| 13 | T2 "8/8 linii" - log zapisu 02-52-25 ma 9 linii, pierwsza zerowa (drobne) | PRAWDA: dni 109198-109206, pierwsza linia same zera (0.0 ms) | T2: zapis >= 8 linii, pierwsza po wczytaniu moze byc zerowa |
| 14 | Tabela obiegu: zloto (164b) nieprawdziwe; I1b otwiera szybsza droge do O22 (sprzedaz we wlasnym miescie + `decision_slaves_sell`) (drobne) | PRAWDA: `BKPartyBehavior.cs:669-676` (`SellSurplusSlaves`, :1056) | Rozdz. 6 - nowy wiersz O22; R1 - odczyt ksiegi obiegu "BK sprzedaz niewolnikow" po sprzedazy we wlasnym miescie |
| 15 | Opis MCM: "New Ghis" (na mapie "New Ghys"), bez Qarkash i City of Bones, "slaves of the town" (drobne) | PRAWDA: `settlements.xml` ROT_town36 `{=ROTnghys}New Ghys`, `str_english.xml` "New Ghys"; ROT_town37 Qarkash; ROT_castle70 City of Bones (zamek) | `Settings.cs:588`: "Qarth (with New Ghys, Qarkash and the City of Bones)", "slaves of that town or castle"; gen_mcm (759 ustawien) |
| 16 | Cena wedlug miejsca GRACZA tnie takze lochy AI (sellerHero null); pytanie 2 tylko o cene gracza (drobne, POZA I1b) | PRAWDA: `FairRansom.cs:42-51` (`Settlement.CurrentSettlement` gracza, `sellerHero == null \|\| MainHero`); loch: `PartiesSellPrisonerCampaignBehavior:85` (sprzedajacy `settlement.Party`, bez lorda) | Pytanie 2 przeformulowane: jedna regula ceny wedlug prawa krainy osady sprzedazy dla gracza i AI - osobna paczka RealisticCaptivity (kod I1b bez zmian) |

**Kontrola calosci (CLAUDE.md zasada 0):** droga AI przy domyslnej polityce Enslavement (wszystkie osady AI) - bez zmian; Policy wolane tak samo czesto jak
dotad (+ rzadka sprzedaz gracza); zmiana rozgrywki tylko w osadach z polityka wybrana przez gracza (Execution - jego sprzedaz poza krainami z niewola, Forgiveness -
sprzedaz AI w krainach z niewola); zadnego dubla z BK (Execution `true` - BK nic; Forgiveness `false` - BK nic); `Route`/`SendHome` SrcLaw - te same co
w I1 (kontrola H3 "prawo jenca I1 ... BK z Murem" obejmuje nowe przypadki). Bez nowych ustawien, bez SyncData.
