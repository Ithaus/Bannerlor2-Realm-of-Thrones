# OKUP ZAMIAST SZTURMU - projekt (13.10.2026, bez kodu)

Status: TYLKO PROJEKT. Decyzja Jeffa 13.10 (pyt. 5b "tak, i tak"): skarbiec korony w stolicy, okup zamiast szturmu, karawany wroga w murach - TAK, jako ostatni
etap. Dwie pierwsze rzeczy sa w kodzie (galaz `w-toku/lup-5b`: L5b-1 `CapitalTreasury.cs`, L5b-2 `EnemyCaravans.cs`); okup wymaga lupu z monety (L2,
galaz `w-toku/lup-miast`) - kod dopiero po scaleniu obu galezi. Oznaczenia jak w `LUP-ZDOBYTEGO-MIASTA-2026-10-13.md`: [K] kod (G = gra 1.4.8, dekompilacja
`dec32\cs_full`), [P] pomiar (bieg roczny kodu 43, `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_15-02-59.log`), [S] szacunek.

## 0. Dla Jeffa - prostym jezykiem

1. Dzis miasto ma dwa wyjscia: wytrzymac albo pasc. Nie moze sie wykupic. Wojsko pod murami tez nie ma wyboru "wezcie zloto i odejdzcie".
2. Propozycja: gdy szturm jest juz blisko, miasto moze zaplacic okup - czesc pieniedzy kupcow i mieszczan. Wojsko bierze zloto, zwija oboz i przez 60 dob
   nie wraca pod to miasto. Miasto zostaje u swojego pana, bez spalonych budowli, bez zabitych i bez grabiezy - ale biedniejsze.
3. Ile: jedna czwarta monety miasta (kasa kupcow + kiesy mieszczan). To mniej niz grabiez (trzecia czesc), bo wojsko nie traci ludzi w szturmie, a miasto
   placi tylko pieniedzmi. Przyklad z biegu: Saltpans przed szturmem mial 176 tys. zl w monecie - okup 44 tys. (grabiez wzielaby 59 tys. i spalila reszte).
4. Kto dostaje: tak samo jak lup - zolnierze 2/3 do sakiewek, wodz 1/3, z niej korona trzecia czesc. Jedna zasada dla lupu i okupu.
5. Kiedy AI przyjmuje: gdy jego wojsko jest w potrzebie (zalegly zold albo kiesa wodza ponizej dwoch tygodni zoldu) albo gdy szturm bylby krwawy (sila
   oblegajacych mniej niz poltora raza ponad obrone murow). Nigdy pod stolica krolestwa (tam ida po koronny skarbiec) i nigdy, gdy okup to mniej niz doba
   zoldu armii. AI-obronca proponuje okup, gdy mury nie wytrzymaja (oblegajacych wiecej niz obroncow liczac przewage murow), a miasto ma z czego zaplacic.
6. Co widzisz: oblegasz - w menu oblezenia nowa opcja "Demand a ransom" i okno z kwota (przyjmij / odrzuc). Bronisz albo jestes panem miasta - okno
   "The besiegers of X demand N denars to lift the siege" (zaplac z kasy miasta / odmow). Nieobecny pan - wiadomosc, ile miasto zaplacilo.
7. Ile to w skali swiata: w roku kodu 43 bylo 34 szturmy i 18 zdobyc; gdyby polowa szturmow skonczyla sie okupem, to ok. 0.4-0.8 mln zl rocznie [S] -
   tyle co lup, nie wiecej. Okup nie jest zrodlem pieniedzy na wojne (jak lup - rozdz. 5.2 projektu lupu); to wybor "zloto teraz albo miasto".
8. Najwieksze ryzyko: menu oblezenia to "menu oczekiwania" - wolanie `SwitchToMenu` z niego konczy sie wywrotka gry (CLAUDE.md rozdz. 7). Dlatego tylko okno
   z pytaniem (popup) i dalej zwykla droga gry.

## 1. Jak dziala oblezenie dzis [K]

- **Poczatek**: armia albo lord z celem `BesiegeSettlement` staje pod murami - `SiegeEvent`, oboz `BesiegerCamp`, przygotowania `SiegePreparations` i machiny
  (G `TaleWorlds.CampaignSystem.Siege\BesiegerCamp.cs:45-47`).
- **Decyzja AI o szturmie**: co mysl AI `MobilePartyAi.GetBesiegeBehavior` (G `...Party\MobilePartyAi.cs:709-718`) - wodz obozu przechodzi na `AssaultSettlement`,
  gdy `BesiegerCamp.IsReadyToBesiege` = przygotowania 100% i `StartingAssaultOnBesiegedSettlementIsLogical` (G `BesiegerCamp.cs:375-430`): stosunek sily
  oblegajacych do sily obroncow x przewaga osady^w (w = 0.8, po 4 dobach maleje o 0.02 na godzine, x1.25 bez taranu, x1.25 bez wiezy) musi byc > 1; potem
  kara za mury i machiny obroncy i los: szansa = 10% stosunku na kazda mysl. Pusta warownia - szturm od razu.
- **Szturm**: `EncounterManager` (G `...CampaignSystem\EncounterManager.cs:246-270`) -> `StartBattleAction.ApplyStartAssaultAgainstWalls`; wygrana ->
  `KingdomManager.SiegeCompleted` -> zmiana pana (G `KingdomManager.cs:239-278`) -> nastepstwa (lup, L0-L7 w galezi `w-toku/lup-miast`).
- **Gracz-oblegajacy**: menu oczekiwania `menu_siege_strategies` (G `...CampaignBehaviors\SiegeEventCampaignBehavior.cs:243-248`): "Lead an assault",
  "Send troops", "Leave". **Gracz-obronca**: w tym samym menu "Request a parley" -> `request_meeting_with_besiegers` (rozmowa - dzis tylko bezpieczne przejscie).
- **Liczby roku** [P]: 829 dob oblezen twierdz (miasta 205, zamki 624; srednio 2.3 naraz - linia "Oblezone (E5/A4)"), 34 szturmy (`bitwy.log` B175 "Siege"),
  18 zdobyc, 0 odpartych, 16 szturmow bez rozstrzygniecia; oblezona osada nie placi panu ani koronie (BesiegedPay) - pieniadz lezy w kasie.

## 2. Regula

| Co | Ustalenie (wybor autora) | Dlaczego |
|---|---|---|
| Z czego | kasa kupcow (`Town.Gold`) i kiesy ludu W, R, P, gmina (zamek: kasa + podzamcze) - te same zbiorniki co lup L2, przez te sama ksiege kies (`TownFolk.SackTake`) | jedna zasada "lup bierze to, co lezy"; kontrola kies r = 0 |
| Ile | okup = floor(1/4 x moneta osady); nie mniej niz doba zoldu armii (inaczej AI odmawia), nie wiecej niz 1/3 | mniej niz grabiez (1/3), bo bez strat w szturmie i bez ognia |
| Komu | jak lup L2: druzyny armii wedlug ludzi, w druzynie 2/3 sakiewki, 1/3 wodz, korona 1/9 (prawo trzecich) | jedna zasada z lupem |
| Kiedy proponuje AI-obronca | od 2 doby oblezenia, raz na dobe, gdy stosunek sily z `StartingAssaultOnBesiegedSettlementIsLogical` > 1 (mury nie wytrzymaja) i moneta >= 4 x doba zoldu armii | miasto placi, gdy i tak padnie |
| Kiedy przyjmuje AI-oblegajacy | armia w potrzebie (ta sama co L4: zalegly zold, kiesa wodza < 14 dob zoldu) ALBO stosunek sily < 1.5 (szturm krwawy); NIE pod stolica przegranego (L5b-1), NIE gdy Inni, NIE w oblezeniach fabuly ROT | zachowanie z biezacej potrzeby (zasada Jeffa) |
| Potem | oboz zwiniety droga gry (`SiegeEvent` konczony jak przy odejsciu oblegajacych), rozejm: frakcja oblegajacych nie oblega tej osady 60 dob (pole zapisu: osada -> frakcja -> doba) | bez rozejmu oblezenie wracaloby nazajutrz |
| Zamek | ta sama regula (kasa zamku + podzamcze) | 624 z 829 dob oblezen to zamki |
| Laska po okupie | brak blizny, brak zabitych BK, budowle stoja, pan ten sam | to cena okupu |

Dowod obiegu: kasa i kiesy osady -> sakiewki, kiesy wodzow -> (nastepnego ranka) korona 1/9. Zdjete = dopisane w linii; "Pieniadz swiata" bez zmian; okno O10
nie dotyczy (bez nastepstw). Nic z niczego, nic w nicosc.

## 3. Co widzi gracz (napisy po angielsku)

- Oblegajacy (wodz): opcja "Demand a ransom" w `menu_siege_strategies` (tylko gdy AI-obronca zgodzilby sie zaplacic): okno `InformationManager.ShowInquiry`
  "The people of {TOWN} offer {N} denars to see your army gone. Your men would get {M}, you {K}. Take the ransom and lift the siege for 60 days?" - Accept / Refuse.
- Obronca (gracz w miescie albo pan osady): okno "The besiegers of {TOWN} demand {N} denars from its merchants and townsfolk to lift the siege." - Pay / Refuse.
  Gracz nieobecny: wiadomosc "{TOWN} paid a ransom of {N} denars; the enemy has lifted the siege."
- Gracz w cudzej armii: wiadomosc z czescia jego druzyny (jak L2).

## 4. Pulapki i ryzyka

1. **GameMenu**: `menu_siege_strategies` to menu oczekiwania - zadnego `SwitchToMenu` z opcji ani z okna (CLAUDE.md rozdz. 7: CTD w `GameMenuVM.OnFrameTick`).
   Okno popup, a po "Accept" tylko akcje gry (koniec oblezenia); menu zmienia sama gra, jak przy "Leave". Test reczny obowiazkowy (autotest nie widzi gracza).
2. **Wojna bez zdobyc**: AI placi i placi - mniej zdobyc. Prog O-4. Rozejm 60 dob i kurczaca sie moneta miasta to hamulec.
3. **Petla gracza**: oblegaj - wez okup - wroc po 60 dobach. Moneta miasta po okupie odrasta tygodniami (rozdz. 3.6 projektu lupu), wiec kolejny okup mniejszy.
4. **Kolizje**: L2 (te same zbiorniki - okup i lup nigdy w tej samej dobie: po okupie nie ma szturmu), L4 (ta sama "potrzeba armii" - jedna funkcja), BesiegedPay
   (pieniadz wstrzymany w oblezeniu jest czescia okupu - slusznie), Diplomacy (rozejm to nie pokoj - nie wolac jego akcji), BK (stabilnosc - bez zmian), ROT
   (oblezenia skryptowe, np. Harrenhal - wykluczone).
5. **Prog 85 tys.**: okup to zold dla zolnierzy oblegajacych (sakiewki) - wojska nie ubywa; obronca traci monete miasta, nie skarbiec. Sprawdzic progiem O-5.

## 5. Paczki i progi

| # | Paczka | Tor | Tresc |
|---|---|---|---|
| O0 | pomiar na sucho | S | linia przy kazdej dobie oblezenia: moneta osady, okup wedlug reguly, stosunek sily, "AI-obronca zaproponowalby / AI-oblegajacy przyjalby", wynik oblezenia |
| O1 | okup AI - AI | M | regula 2 bez gracza; rozejm 60 dob (jedno pole zapisu przez SaveText.Sync) |
| O2 | gracz-obronca | M | okno popup, wiadomosc nieobecnego pana |
| O3 | gracz-oblegajacy | M | opcja "Demand a ransom" + okno; test reczny |

Kolejnosc: po scaleniu `w-toku/lup-miast` (L2, L4) i `w-toku/lup-5b`. Klucze `SiegeRansom*` domyslnie wlaczone, wylaczone = stan sprzed paczki.

| # | Prog | Wartosc |
|---|---|---|
| O-1 | linii pomiaru = dob oblezen z linii "Oblezone (E5/A4)" | rowne |
| O-2 | kazdy okup: zdjete z kasy i kies = dopisane sakiewkom, wodzom, kiesom druzyn; kiesy ludu r = 0 | 0 zl |
| O-3 | oblezenie tej samej osady przez te sama frakcje w 60 dob po okupie | 0 |
| O-4 | zdobycia twierdz w roku wobec biegu bez paczki | >= 50% (ponizej - DECYZJA) |
| O-5 | druzyny lordow krolestw w wojnie (miara W5-7), bieg roczny | >= 85 tys. |
| O-6 | okup pod stolica, przez Innych, w oblezeniu fabuly ROT | 0 |
| O-7 | ERROR i potkniecia; wywrotki w menu (test reczny: zamek, oblegajacy i obronca) | 0 |

Czego nie wiem: jak czesto AI-obronca bylby gotow placic (mierzy O0); ile dni trwa dzis oblezenie zakonczone szturmem (log ma doby oblezen, nie
poczatek i koniec kazdego); czy Diplomacy albo BK reaguja na koniec oblezenia bez bitwy (do sprawdzenia w dekompilacji przed O1).
