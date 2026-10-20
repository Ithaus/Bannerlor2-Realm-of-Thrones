## m96-dlugi - dlugi rad miast i podatek nadzwyczajny (Armoury)

**Dla Jeffa (prosto):** miasto ma teraz wlasna rade, ktora umie pozyczac. Gdy kasa miasta nie ma z czego zaplacic panu i koronie ich
naleznosci, rada pozycza brakujace pieniadze - najpierw u bogatych kupcow i rzemieslnikow miasta, potem u notabli, na koncu w Banku
Zelaznym - i od razu placi. Potem splaca dlug z odsetkami z tego, co kasa ma ponad zapas kupcow; dopoki miasto jest w dlugach, jego
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
   `DsTowns.TdPayArrears`, ta sama droga co rata. Bez zywego pana i w oblezeniu - nic.
2. **Odsetki:** **Town Debt Rate** 10%/rok (mieszczanie, notable), **Town Debt Bank Rate** 20%/rok (Bank, jak krol), co dobe do dlugu; rok z modelu czasu gry.
   Odsetki wplacone kiesie klasy - zarobek okna (podstawa podatkow pana, jak f8).
3. **Splata:** co dobe **Town Debt Repay Share** (0.5) nadwyzki kasy ponad zapas kupcow (tylko bez zaleglosci firmy) - Bank pierwszy, potem notable i kiesy
   w stosunku dlugu. **Kosztem renty pana:** splata nie jest poborem (bez `TownPurse.NoteTake`) - doplyw netto kupcow maleje, a przeglad firmy DS idzie za nim;
   dlug rady liczy sie w DS jak zaleglosc (`TownDebt.ArrearsLike` w `DsTowns.Terms`, `Review`, `SurplusReview`): bez podwyzki firmy i z przegladem wymuszonym
   w dol (-15%), gdy dlug >= raty przez 2 dni kwartalu. Zajecie z kiesy gminy (DS) - jak dotad, tylko przy prawdziwej zaleglosci.
4. **Dobrobyt:** co dobe -**Town Debt Prosperity Per Day** (0.5) x min(1, dlug / limit).
5. **Podatek nadzwyczajny (rada, nie pan):** dlug >= 90% limitu albo zaleglosc, na ktora dzis nikt nic nie pozyczyl -> **Town Debt Levy Share** (10%) tego,
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
8. **Zapis:** klucz `arm_towndebt` ("td1|id;P;R;B;IP;IR;dzien podatku;od kiedy;ostatni podatek;notabl~kwota~odsetki,..."), `SaveText.Sync`, pisany tylko z danymi.
   Kampania Jeffa (save z WGRANIA 94/95) klucza nie ma -> rady bez dlugow, start od pierwszej zaleglosci; nic do naprawiania w jego zapisie. Starsza DLL klucz pomija.
9. **Wpiecia (minimalne):** `ArmouryBehavior` (reset `_m96DlugiReset`, `SyncData`, `TownDebt.Daily` przed `Mark(MFolk)`, `TownDebt.AddMenus`), `DsTowns.cs`
   (`partial` + 3 warunki "zaleglosc" z `ArrearsLike`), `IronBank.Dividends.cs` (1 linia), `MoneyLedger.Obieg.cs` (1 dopisek), `TownMenus.cs` (1 Move),
   `Settings.cs` + `tools/gen_mcm.py` (12 kluczy w grupie "The townsfolk's purse"), `tools/sprawdz_logi.py` (grupa `m96dlugi_dlugi`).

**Linia logu (co dobe):** `Dlugi miast (m96-dlugi): dzien N | miasta z dlugiem X (nowe, splacone do zera), dlug razem D (limit razem) - wierzyciele: P, R,
notable, Bank; najwiekszy | zaleglosci firm DS z kasy przed pozyczka | pozyczki dzis (P, R, notable, Bank z puli) -> zaleglosci panom, koronie | odsetki |
splaty z nadwyzki kas | niewyplacalne, podatki nadzwyczajne (W, R, P, notable) -> wierzycielom, na zaleglosci, w kasie | skutki: dobrobyt, lojalnosc, relacje |
kontrola: dlug - (wczoraj + pozyczone + odsetki - splacone) 0; podatek - (wierzycielom + na zaleglosci + w kasie) 0 | czas | potkniecia`. Start sesji:
"Dlugi miast (m96-dlugi): zapis bez klucza arm_towndebt ..." albo "wczytane N ksiag rad".

**Oczekiwane liczby (test 160 dob z WGRANIEM 96, porownanie z 95):** miast z dlugiem 3-15 (95: 8 miast z zaleglosciami); dlug razem < 2 mln i <= limit razem;
zaleglosci firm DS miast na koncu (linia DS f14-miary) <= 50% z 381 134 (zostaja tylko u miast na limicie, np. Duskendale); podatki nadzwyczajne 0-10 w 160
dobach, lojalnosc na podatek <= 5; obie kontrole 0 w kazdej linii; czas < 1 ms/d; potkniecia 0; linia DS "kontrola: platnicy - odbiorcy = 0" bez zmian;
"Pieniadz swiata" bez nowego rozjazdu. Sprawdzenie: `python -I tools/sprawdz_logi.py --grupa m96dlugi_dlugi <Armoury-*.log>` (M96D-0..6).

**Ryzyko / co sprawdzic (kontrola calosci):**
- regresje: DS liczy dlug rady jak zaleglosc w 3 miejscach - przy kluczu wylaczonym `ArrearsLike` = 0 (DS co do monety jak dotad); zajecie z kiesy gminy nadal tylko
  przy prawdziwej zaleglosci (po pozyczce zaleglosci ~0 - zajec bedzie mniej, wiecej przegladow wymuszonych w dol);
- kolizje: pulap dzienny kies wspolny z f8 (rada bierze pierwsza w dobie - pozyczki lordow z kies tych miast mniejsze); Bank: pula 1/60 kapitalu ponad 2 mln
  osobno od kredytu koron (oba z tej samej nadwyzki - Bank moze zejsc szybciej do progu); pozyczka f8 z KASY kupcow (TlKasa) i korona2 z kas nie patrza na dlug
  rady - miasto z dlugiem moze pozyczyc panu z kasy ponad 1.5 x zapas (nie zmienialem - do decyzji);
- splata z nadwyzki zmniejsza doplyw netto kupcow (TownPurse) - przeglad firm DS obniza firme pana i korony; wyplata kupcom M6 i roboty miast biora mniej;
- notable: zwrot ponad 10 500 gra zamienia na sile (zloto do kasy miasta - NotableGold), wiec splata notabli wraca czesciowo do kasy (przelew, nie z niczego);
- dobrobyt i lojalnosc ustawiane wprost (`Town.Prosperity`, `Town.Loyalty` - jak CitySack, W7); BK bunty przy niskiej lojalnosci - podatek nie spycha ponizej 20;
- zapis: nowy klucz, stary zapis bez klucza = brak dlugow; wersja "td1" sprawdzana; wylaczenie klucza = dlugi stoja (bez odsetek), Bank dalej widzi ich koszt.

**Status:** DO SPRAWDZENIA (build Armoury kod 0; gry nie uruchamialem - zakaz; wielki test nocny WGRANIA 96).

**Test w grze:** test 160 dob (nowa kampania) - grupa `m96dlugi_dlugi` + linie DS ("zaleglosci wedlug osad", "kontrola: platnicy - odbiorcy = 0") i "Obieg"
(dopisek "dlugi rad miast"); u gracza: wlasne miasto z zaleglosciami -> komunikat "The council of X has borrowed ...", opcja Town affairs -> The council's debts.
