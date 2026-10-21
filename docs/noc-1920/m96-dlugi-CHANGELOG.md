## m96-dlugi - dlugi rad miast i podatek nadzwyczajny (Armoury)

**Dla Jeffa (prosto):** miasto ma teraz wlasna rade, ktora umie pozyczac. Gdy kasa miasta nie ma z czego zaplacic panu i koronie ich
naleznosci, rada pozycza brakujace pieniadze - najpierw u bogatych kupcow i rzemieslnikow miasta, potem u notabli, na koncu w Banku
Zelaznym - i od razu placi. Potem splaca dlug z odsetkami z tego, co kasa ma ponad zapas kupcow i najblizsza rate; dopoki miasto jest w dlugach, jego
dobrobyt powoli spada, a renta pana z tego miasta nie rosnie (i schodzi przy przegladzie). Gdy kredyt sie skonczy, rada SAMA naklada
podatek nadzwyczajny na mieszczan i notabli (najwyzej raz na 28 dni): placi nim wierzycielom i panu, ale miasto traci lojalnosc i
dobrobyt, a notable mniej lubia pana. Twoje miasta dzialaja tak samo - w **Town affairs** jest nowa opcja **The council's debts**
(ile, komu, ile kosztuje), a o pierwszej pozyczce, podatku i splacie dostaniesz krotki komunikat. Wylacznik: MCM Armoury ->
"The townsfolk's purse" -> **Town Debt**.

**Problem:** pomysl Jeffa z 07.10 ("zapisz na pozniej", PLAN-KOSZTY-MIASTA-2026-10-07 rozdz. 6): miasto trzyma rezerwe kasy; gdy ma dlugi -
splaca i pozycza, a dobrobyt spada; gdy jest niewyplacalne, samo (nie lord) podnosi podatki. 20.10: "dlugi miast tak, robimy".
Dzis rada miasta nie istnieje - jest tylko kasa kupcow; nie ma pozyczki miasta ani jego wlasnego podatku.

**Dowod (log 160 dob WGRANIA 95, `D:\Backup-Bannerlord\logi-testow\2026-10-20\Armoury-2026-10-20_12-25-19.log`, doba 109234):**
- linia "Firmy miast i zamkow (DS)": "zaleglosci wedlug osad (f14-miary): miasta 381134 zl u 8 (...); najwieksze: Duskendale (miasto) 277302 = 5.1 rat
  (...), kasa 24397 wobec podlogi 21032"; raty dzis: 32 osad (14 miast), nowe zaleglosci 11409 zl/d;
- linia "Kasy miast (169c)" (co 10 linii od startu): kas ponizej zapasu kupcow 2-19 z 97, ponizej 1000 zawsze 0, przy 0 zawsze 0, najdluzej Pentos 71 dob,
  Rhyos 61 dob z rzedu; miast >= 28 dob ponizej zapasu 1-4;
- "Pieniadz swiata" tej doby: kiesy ludu miast 9 992 034, notable 22 461 527, Bank Zelazny 4 638 402 (prog kredytu koron 2 mln) - jest od kogo pozyczac.
- (recenzja) z 381 134 zl zaleglosci miast czesc to oszczednosci ludu (ALf - placa kiesy ludu, nie kasa; paczka jej nie pozycza ani nie zbiera): Duskendale
  277 302 = panu 69 830 + koronie 93 670 + z oszczednosci ludu 113 801 (41%), Rhyos 46 271 bez ALf; pozostale 6 miast 57 561 zl bez rozbicia w logu. Czesc
  z KASY (panu + koronie) to wiec co najmniej 209 771 zl - ta liczba jest baza M96D-3.

**Przyczyna (kod):** jedynym obowiazkowym wydatkiem kasy miasta jest rata firmy DS (`DsTowns.Pay`: zaleglosc i rata z kasy ponad podloge
`DsFarmFloorShare` x zapas = 0.5 x `TownPurse.Reserve`; brak -> `f.AL` / `f.AC`, po 2 dniach kwartalu zajecie z kiesy gminy przez pana). Roboty miast
(`TownHire` - tylko ponad `HireKeep`), zboze (`TownGranary` - "kasa kupuje tyle, ile ma"), nasze zakupy (`TownPurse.BuyFloor`) biora tylko z nadwyzki, wiec
nie tworza braku. Zadna droga nie pozwalala miastu pozyczyc na rate ani samemu opodatkowac mieszczan - zaleglosc lezala i czekala na zajecie.

**Zmiana (klucz `TownDebt`, domyslnie TAK; nowe pliki `Armoury/src/TownDebt.cs`, `TownFolk.TownDebt.cs`, `DsTowns.TownDebt.cs`):**
1. **Pozyczka:** codziennie (po `TownFolk.Daily` i `TownGranary`, przed `Mark(MFolk)`) miasto z zaleglosciami firmy z kasy (pan + korona) >= 500 zl - bez tej
   czesci, ktora kasa sama zaplaci w dzien raty (kasa ponad podloge firm) - pozycza na nie najwyzej do limitu **Town Debt Limit Reserve** (3) x zapas kupcow:
   kiesa P (kupcy i patrycjat), kiesa R (rzemieslnicy) - polowa tego, co maja ponad Town Loan Folk Reserve Times x zapas dni, wspolny pulap dzienny z pozyczka
   f8 (`_tlTaken`); notable miasta - polowa zlota ponad **Town Debt Notable Keep** (5000; pasmo gry 4500-10500); Bank - z kapitalu ponad Crown Loan Bank Floor,
   1/60 nadwyzki na dobe dla wszystkich miast. Pozyczone -> kasa -> od razu zaleglosc panu (GiveGoldAction, RentToday) i koronie (skarbiec, CrownToday) -
   `DsTowns.TdPayArrears`, ta sama droga co rata. Bez zywego pana i w zwolnieniu DS (oblezenie, 28 dob po zdobyciu / zmianie krolestwa - `DsTowns.RemitOf`;
   recenzja) - nic: tylko odsetki i dobrobyt.
2. **Odsetki:** **Town Debt Rate** 10%/rok (mieszczanie, notable), **Town Debt Bank Rate** 20%/rok (Bank, jak krol), co dobe do dlugu; rok z modelu czasu gry.
   Odsetki wplacone kiesie klasy - zarobek okna (podstawa podatkow pana, jak f8).
3. **Splata:** co dobe **Town Debt Repay Share** (0.5) nadwyzki kasy ponad wieksze z: zapas kupcow albo podloga firm + cala najblizsza rata z kasy + to, co
   kasa juz winna (`DsTowns.TdKeep`; tylko bez zaleglosci firmy) - Bank pierwszy, potem notable i kiesy w stosunku dlugu (kazda czesc najwyzej do reszty
   splaty). **Kosztem renty pana:** splata nie jest poborem (bez `TownPurse.NoteTake`) - doplyw netto kupcow maleje, a przeglad firmy DS idzie za nim;
   dlug rady liczy sie w DS jak zaleglosc (`TownDebt.ArrearsLike` w `DsTowns.Review` i `SurplusReview`: bez podwyzki firmy) i ma WLASNY licznik dni kwartalu
   (`Book.DTerms`, `TownDebt.NoteTerm` w `DsTowns.Terms`, `TownDebt.TermsLike` w `Review`): przeglad wymuszony w dol (-15%), gdy zaleglosc + dlug >= raty
   przez 2 dni kwartalu. Zajecie z kiesy gminy (DS, licznik `ATerms`) - co do monety jak przed paczka, tylko przy prawdziwej zaleglosci.
4. **Dobrobyt:** co dobe -**Town Debt Prosperity Per Day** (0.5) x min(1, dlug / limit).
5. **Podatek nadzwyczajny (rada, nie pan):** dlug >= 90% limitu albo zaleglosc, na ktora dzis nikt nic nie pozyczyl (dzienna pula Banku wyczerpana przez
   miasta wczesniej na liscie to nie brak kredytu - miasto czeka do jutra, licznik "czeka na pule Banku") -> **Town Debt Levy Share** (10%) tego,
   co W / R / P maja ponad 1 x zapas dni, i tego, co notable maja ponad 5000, najwyzej dlug + zaleglosc; zebrane -> kasa -> wierzyciele (Bank pierwszy) ->
   zaleglosc firmy. Koszt pelny przy zbiorce 10% x zapas kupcow (mniej - proporcjonalnie): lojalnosc -**Town Debt Levy Loyalty** (5, nie ponizej 20 tym
   podatkiem), dobrobyt -**Town Debt Levy Prosperity** (1%), relacja kazdego placacego notabla z panem -**Town Debt Levy Relation** (2). Przerwa
   **Town Debt Levy Gap Days** (28).
6. **Gracz:** te same reguly. Opcja "The council's debts" w menu miasta (`arm_towndebt_town`, m95-menu przenosi do **Town affairs**) - widoczna przy dlugu albo
   podatku w ostatnich 28 dobach; okno: dlug, limit, wierzyciele, odsetki rocznie / tygodniowo, utrata dobrobytu na dobe, ostatni podatek. Komunikaty (EN) dla
   miast gracza: pierwsza pozyczka, podatek nadzwyczajny, splata do zera. Podpowiedzi kasy w grze nie ruszalem (nasz kod nie ma podpowiedzi kasy miasta).
7. **Nic z niczego:** przelewy miedzy posiadaczami "Pieniadza swiata" (kiesy ludu, notable, Bank, kasa, pan, skarbiec), dlug to zapis. Bank: `CapitalAdd` w parze
   z `ChangeGold`, koszt `IronBank.NoteCrownLent("td:<miasto>")`, dlugi w rozliczeniu zysku (`IronBank.Dividends.DvDay` -> `TownDebt.BankInto`) - odsetki to zysk,
   splata kapitalu nie. Ruchy kas w pozycji MFolk ksiegi "Przeplywy osad"; dopisek w linii "Obieg" (`TownDebt.ObiegNote`). Notabl-wierzyciel zmarly albo juz nie
   notabl - jego czesc na kiese P (spadkobiercy). Kiesa nieczynna przy splacie - zloto w kasie, dlug bez zmiany.
8. **Zapis:** klucz `arm_towndebt` ("td1|id;P;R;B;IP;IR;dzien podatku;od kiedy;ostatni podatek;notabl~kwota~odsetki,...;dni kwartalu z dlugiem"), `SaveText.Sync`,
   pisany tylko z danymi; 11. pole (recenzja) opcjonalne - zapis z 10 polami wczytuje sie z licznikiem 0.
   Kampania Jeffa (save z WGRANIA 94/95) klucza nie ma -> rady bez dlugow, start od pierwszej zaleglosci; nic do naprawiania w jego zapisie. Starsza DLL klucz pomija.
9. **Wpiecia (minimalne):** `ArmouryBehavior` (reset `_m96DlugiReset`, `SyncData`, `TownDebt.Daily` przed `Mark(MFolk)`, `TownDebt.AddMenus`), `DsTowns.cs`
   (`partial`, 2 warunki "zaleglosc" z `ArrearsLike`, `Terms` + `NoteTerm`, `Review` z `TermsLike`), `IronBank.Dividends.cs` (1 linia), `MoneyLedger.Obieg.cs`
   (1 dopisek), `TownMenus.cs` (1 Move),
   `Settings.cs` + `tools/gen_mcm.py` (12 kluczy w grupie "The townsfolk's purse"), `tools/sprawdz_logi.py` (grupa `m96dlugi_dlugi`).

**Linia logu (co dobe):** `Dlugi miast (m96-dlugi): dzien N | miasta z dlugiem X (nowe, splacone do zera), dlug razem D (limit razem) - wierzyciele: P, R,
notable, Bank; najwiekszy | zaleglosci firm DS z kasy przed pozyczka | pozyczki dzis (P, R, notable, Bank z puli) -> zaleglosci panom, koronie | odsetki |
splaty z nadwyzki kas (ponad zapas kupcow i najblizsza rate) | niewyplacalne (zaleglosc bez kredytu; czeka na pule Banku), podatki nadzwyczajne (W, R, P,
notable) -> wierzycielom, na zaleglosci, w kasie | skutki: dobrobyt, lojalnosc, relacje | bez zywego pana, oblezone, po zdobyciu (28 dob bez rat DS) |
zaleglosci firm DS miast z kasy na koniec (panu + koronie, bez czesci z oszczednosci ludu) | kontrola: dlug - (wczoraj + pozyczone + odsetki - splacone -
zaokraglenia z) 0; podatek - (wierzycielom + na zaleglosci + w kasie) 0 | czas | potkniecia`. Start sesji:
"Dlugi miast (m96-dlugi): zapis bez klucza arm_towndebt ..." albo "wczytane N ksiag rad".

**Oczekiwane liczby - WIDAC w tescie 160 dob z WGRANIEM 96 (porownanie z 95):** miast z dlugiem 3-15 (95: 8 miast z zaleglosciami); dlug razem < 2 mln
i <= limit razem; zaleglosci firm DS miast Z KASY (panu + koronie, segment "zaleglosci firm DS miast z kasy na koniec") <= 50% z 209 771 = 104 885 zl
(zostaja tylko u miast na limicie, np. Duskendale: limit 3 x 42 064 = 126 192 przy 163 500 z kasy); podatki nadzwyczajne 0-10 w 160 dobach, lojalnosc na
podatek <= 5; kontrola dlugu |r| <= 5 zl, kontrola podatku = 0 w kazdej linii; "czeka na pule Banku" zwykle 0; czas < 1 ms/d; potkniecia 0; linia DS
"kontrola: platnicy - odbiorcy = 0" bez zmian i "zajete z kies gmin 0" jak w 95; "Pieniadz swiata" bez nowego rozjazdu. Sprawdzenie:
`python -I tools/sprawdz_logi.py --grupa m96dlugi_dlugi <Armoury-*.log>` (M96D-0..7).
**NIE widac w 160 dobach (recenzja):** przeglad wymuszony firm DS (-15%) i zajecia z kiesy gminy wymagaja 2 dni kwartalu PO ustaleniu firm (95: firmy ustalone
w dobie 109131, dni kwartalu 109200 i 109291 - drugi poza testem). Do sprawdzenia w biegu ok. 240+ dob albo w kampanii Jeffa: linia DS "w dol wymuszone (-15%)
... " u miast z dlugiem rady > 0, "zajete z kies gmin" nie wiecej niz bez paczki (u miast z dlugiem rady zajecie tylko przy prawdziwej zaleglosci).

**Ryzyko / co sprawdzic (kontrola calosci):**
- regresje: DS liczy dlug rady jak zaleglosc w `Review` / `SurplusReview` (bez podwyzki) i we wlasnym liczniku przegladu wymuszonego (`TermsLike`) - przy
  kluczu wylaczonym `ArrearsLike` = 0 i `TermsLike` = `ATerms` (DS co do monety jak dotad); `ATerms` (zajecie z kiesy gminy, linie "do zajecia") liczony jak przed
  paczka - tylko z prawdziwej zaleglosci (po pozyczce zaleglosci ~0 - zajec bedzie mniej, wiecej przegladow wymuszonych w dol - widac dopiero po 240+ dobach);
- kolizje: pulap dzienny kies wspolny z f8 (rada bierze pierwsza w dobie - pozyczki lordow z kies tych miast mniejsze); Bank: pula 1/60 kapitalu ponad 2 mln
  osobno od kredytu koron (oba z tej samej nadwyzki - Bank moze zejsc szybciej do progu); pozyczka f8 z KASY kupcow (TlKasa) i korona2 z kas nie patrza na dlug
  rady - miasto z dlugiem moze pozyczyc panu z kasy ponad 1.5 x zapas (nie zmienialem - do decyzji);
- splata z nadwyzki zmniejsza doplyw netto kupcow (TownPurse) - przeglad firm DS obniza firme pana i korony; wyplata kupcom M6 i roboty miast biora mniej;
- notable: zwrot ponad 10 500 gra zamienia na sile (zloto do kasy miasta - NotableGold), wiec splata notabli wraca czesciowo do kasy (przelew, nie z niczego);
- dobrobyt i lojalnosc ustawiane wprost (`Town.Prosperity`, `Town.Loyalty` - jak CitySack, W7; dekompilacja 1.4.8: zwykle settery, Prosperity >= 0, Loyalty 0-100,
  bez zdarzen); BK bunty przy niskiej lojalnosci - podatek nie spycha ponizej 20 (gra: bunt ponizej 15, stan buntowniczy ponizej 25);
- zapis: nowy klucz, stary zapis bez klucza = brak dlugow; wersja "td1" sprawdzana; wylaczenie klucza = dlugi stoja (bez odsetek), Bank dalej widzi ich koszt.

**Poprawki po recenzjach (gra, technika):**
- WAZNE (obie recenzje, prawdziwe): dlug rady byl dopisany do licznika `ATerms` w `DsTowns.Terms`. Ten sam licznik uruchamia zajecie z kiesy gminy
  (`DsTowns.Pay` krok 4: `f.ATerms >= DsFarmDistraintTerms (2 u Jeffa) && f.AL + f.AC + f.ALf >= 1`), a `Pay` biegnie w `PopulationLaw.Daily` PRZED
  `TownDebt.Daily` - u miasta z dlugiem rady przez 2 dni kwartalu kazdy brak w dzien raty zabieralaby najpierw gmina, zanim rada pozyczy. Poprawka: `ATerms`
  znow tylko z prawdziwej zaleglosci (kod jak w 848b2e80), osobny licznik rady `Book.DTerms` (zapis, 11. pole) tylko dla przegladu wymuszonego (`TermsLike` =
  wieksze z `ATerms` i `DTerms`). Do tego splata z nadwyzki zjadala kase ponad 1.0 x zapas, a rata idzie z kasy ponad 0.5 x zapas: miasto z rata > 0.5 x zapas
  mialo brak w kazdy dzien raty i pozyczalo znowu na te sama rate (przyklad recenzji: doplyw 50 tys./tydz., rata 45 tys., zapas 40 tys.). Poprawka: splata
  tylko ponad podloge firm + cala najblizsza rate z kasy (`DsTowns.TdKeep`, skladniki jak `CofferOwes`, ale z cala rata).
- drobne (prawdziwe, poprawione): (a) zaokraglenia w `Repay` mogly dac wyplaty o 1-2 zl ponad `pay` (kasa doplacala, kontrola podatku -1, falszywe NIE w
  M96D-1) - teraz kazda czesc najwyzej do reszty `pay`, a spadek dlugu inny niz splata (ostatnia moneta, reszta < 1 skreslona) liczony w "zaokraglenia" -
  kontrola dlugu dokladna; (b) rada pozyczala i nakladala podatek w 28 dobach zwolnienia DS po zdobyciu - teraz `RemitOf != 0` (oblezenie albo zdobycie): tylko
  odsetki i dobrobyt, `TdPayArrears` tez nic; (c) wyczerpana dzienna pula Banku liczyla sie jak brak kredytu (podatek trafial miasta z konca listy) - teraz
  "czeka na pule Banku", bez podatku z tego powodu; (d) baza M96D-3 i dowod zawieraly czesc z oszczednosci ludu - M96D-3 czyta nowy segment linii (panu +
  koronie) i porownuje z 209 771; (e) oczekiwane rozdzielone na widoczne w 160 dobach i wymagajace 240+ dob.
- bez zmian (sprawdzone, nie blad): MinNeed 500 zl - zaleglosc ponizej nie wchodzi do niewyplacalnosci (ulamki raty; kasa placi je sama w dzien raty).
- dalej nie zrobione: podpowiedz kasy miasta (pkt 3 zadania) - nasz kod nie ma podpowiedzi kasy; menu "The council's debts" i komunikaty zostaja jedynym widokiem.

**Status:** DO SPRAWDZENIA (build Armoury kod 0; gry nie uruchamialem - zakaz; wielki test nocny WGRANIA 96).

**Test w grze:** test 160 dob (nowa kampania) - grupa `m96dlugi_dlugi` + linie DS ("zaleglosci wedlug osad", "kontrola: platnicy - odbiorcy = 0") i "Obieg"
(dopisek "dlugi rad miast"); u gracza: wlasne miasto z zaleglosciami -> komunikat "The council of X has borrowed ...", opcja Town affairs -> The council's debts.
