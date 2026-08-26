# DZIENNIK ZMIAN

> **ZASADA OBOWIAZKOWA DLA KAZDEGO CLAUDE (kazde konto, kazdy komputer):**
> po **kazdej** zmianie w kodzie dopisz wpis na GORZE tej listy i zrob commit + push.
> Dzieki temu drugie konto zawsze wie, co sie stalo, bez czytania calej rozmowy.
>
> Format wpisu:
> ```
> ## RRRR-MM-DD — krotki tytul
> **Mod:** nazwa | **Pliki:** sciezki
> **Problem:** co bylo nie tak (objaw u gracza + dowod z logu, jesli byl)
> **Przyczyna:** co konkretnie w kodzie
> **Zmiana:** co zrobiono
> **Ryzyko / co sprawdzic:** na co patrzec w grze i w logu
> **Status:** WGRANE / COFNIETE / DO SPRAWDZENIA
> ```

---

## 2026-08-26 — goli zolnierze w kryjowce: DressCode dopelnia KAZDY pusty slot
**Mod:** Armoury | **Pliki:** `src/DressCode.cs`
**Problem:** zolnierze gracza stoja nadzy podczas walki w kryjowce rozbojnikow (w polu OK).
**Przyczyna (ze ZRODEL DTE, github tategotoazarasi/Bannerlord.DynamicTroop):** (1) DTE
druzynie gracza CELOWO nie dopelnia pustych slotow (kara underequipped) - zolnierz dostaje
tylko to, co magazyn ma; (2) awaryjne ubranie DTE, ktore ratuje w polu, jest w kryjowkach
WYLACZONE; (3) nasz stary DressCode odpuszczal, gdy byla CHOC JEDNA czesc pancerza (same
buty = "ubrany"), i modyfikowal wspoldzielony Equipment przydzialu DTE, co moglo falszowac
zdejmowanie sztuk z polek magazynu w postfixie DTE.
**Zmiana:** DressCode dopelnia kazdy pusty slot pancerza Z OSOBNA (glowa/korpus/nogi/rece/
peleryna) ubraniem ze wzorca oddzialu, na KLONIE ekwipunku - przydzial DTE nietkniety,
rozliczenie magazynu bez zmian. Prefix dalej Priority.Last (po prefixie DTE).
**Ryzyko / co sprawdzic:** kryjowka - nikt nagi; w logu linie "DressCode: ... pustych slotow
pancerza"; po bitwie polowej stan magazynu DTE bez dziwnych ubytkow. Kara morale
underequipped DTE dalej dziala (to tylko ubranie, nie pancerz z magazynu).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — liczniki surowcow i staminy w ekranie kuzni odswiezaja sie na zywo
**Mod:** Armoury | **Pliki:** `src/FletchForge.cs`
**Problem:** po wykuciu (np. luku) w zakladce CRAFT panel dolny dalej pokazywal stara ilosc
drewna/zelaza/wegla i stara stamine - trzeba bylo sie przeklikac miedzy zakladkami.
**Przyczyna:** vanilla przelicza panel (CraftingVM.UpdateAll: materialy, stamina, skille,
dostepnosc przycisku) tylko po WLASNYCH akcjach; nasze Forge.Smith zdejmuje materialy wprost
z sakw, wiec ekran nie wiedzial. Stary refresh w prefixie robil tylko stamine bohatera i
OnRefresh mixina BK - za malo.
**Zmiana:** RefreshCraftScreen wolany z postfixa ExecuteMainActionBK (postfix biegnie TAKZE
gdy prefix przejal robote strzelecka, i przy pancerzach BK): CraftingVM.UpdateAll przez
reflection + OnRefresh mixina. Podwojny refresh z prefixa usuniety.
**Ryzyko / co sprawdzic:** po wykuciu luku licznik drewna/zelaza i stamina maja zejsc OD RAZU;
to samo po pancerzu z zakladki BK. UpdateAll jest prywatne - jesli nazwa sie kiedys zmieni,
Traverse zwroci null i zostanie stary objaw (bez crasha).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — tansza naprawa wojska: wrak max 10% wartosci + rabat hurtowy
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/Settings.cs`
**Problem:** Jeff po obejrzeniu przykladow: wrak (1%) placony ~12,5% wartosci to za drogo,
i ma byc "jeszcze taniej jesli hurtowo naprawiamy".
**Przyczyna:** stara stawka liczona jako polowa ceny naprawy wlasnej sztuki.
**Zmiana:** cena sztuki wojska liczy sie teraz WPROST od wartosci: wrak (1%) = Troop Mend
Wreck Share (10%) wartosci, lzejsze zuzycie liniowo mniej (stan 60% = 4% wartosci). Do tego
rabat hurtowy z calej roboty: kazda sztuka -0,5% z rachunku (Troop Mend Bulk Discount PP),
pulap 30% (Max). Rabat w podpowiedzi i przy placeniu liczony z tej samej liczby sztuk.
Ustawienie Troop Mend Cost Factor USUNIETE (zastapione przez Wreck Share); stare XML-e je
zignoruja.
**Ryzyko / co sprawdzic:** kwoty w podpowiedzi (np. kolczuga 2000 przy 3%: bylo 242, ma byc
~194 minus rabat); przy pelnych polkach rabat do 30%.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — porzadek w menu kuzni: naprawy pod jednym wejsciem
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`
**Problem:** menu Work the forge uroslo do 12 pozycji, z czego 5 to rozne odmiany naprawy -
nie bylo widac, ktora do czego (Jeff: "sprawdz czy opcje maja sens i czy czegos nie wywalic").
**Przyczyna:** kazda nowa funkcja dokladala wlasny wiersz do glownego menu.
**Zmiana:** nowe podmenu "The mending bench - your gear and the men's" (armoury_mend), a w nim:
wybor sztuki, naprawa calego rynsztunku wlasnorecznie / u kowala, lupy z sakw hurtem, sprzet
wojska. Podpowiedz wejscia od razu liczy: ile zuzytych na grzbiecie / w sakwach / na polkach
wojska. Glowne menu ma teraz 8 pozycji. Nic nie wylecialo - wszystkie funkcje zostaly,
zmienilo sie tylko ulozenie. Po skonczonej robocie wracasz do glownego menu kuzni.
**Ryzyko / co sprawdzic:** wejscie i wyjscie z podmenu (SwitchToMenu miedzy zwyklymi menu,
NIE z menu oczekiwania - pulapka CTD nie dotyczy); wszystkie 5 opcji dziala jak przedtem.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — zamawianie brakujacego sprzetu wojska u kowala
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/QuartermasterLaw.cs`, `src/Settings.cs`
**Problem:** kwatermistrz melduje "brakuje Thrown 5/24", ale nie bylo JAK dokupic brakow -
trzeba bylo biegac po kramach i zgadywac.
**Przyczyna:** brak takiej funkcji.
**Zmiana:** opcja "Order kit for the men - the smith procures it": lista typow ze stanem
polki/potrzeba (braki podswietlone), wybor tieru 1-6 z najtansza kupna sztuka i cena od
sztuki (wartosc rynkowa x Troop Order Markup, domyslnie 1,15), ilosc 1/5/10/uzupelnij brak;
dostawa wprost na polki zbrojowni DTE po 1 h + 0,2 h na sztuke (max 24 h). Konie celowo
wylaczone (od tego stajnie). 2 nowe ustawienia MCM.
**Ryzyko / co sprawdzic:** ceny przy tierach maja wygladac rynkowo; po odbiorze zloto schodzi
o wypisana kwote, a sztuki laduja w zbrojowni (DTE sam ubiera ludzi).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — naprawa sprzetu WOJSKA u kowala (zbrojownia DTE)
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/QuartermasterLaw.cs`, `src/Settings.cs`
**Problem:** zbrojownia DTE trzyma modyfikatory stanu, a ConditionScaling skaluje pancerz
KAZDEGO noszacego - zolnierz w kirysie 3% ma realnie ~11% ochrony (krzywa: 1% ~ -89%).
Nie bylo ZADNEJ drogi naprawy polek wojska ani informacji, ze taka potrzeba istnieje.
**Przyczyna:** opcje kowala obejmowaly tylko sakwy i grzbiet gracza.
**Zmiana:** nowa opcja "Send the men's worn gear to the smith" w Work the forge: skan zbrojowni
DTE, stawka HURTOWA (Troop Mend Cost Factor, domyslnie 50% ceny naprawy wlasnej sztuki),
najtansze najpierw, pelny koszt wypisany Z GORY w podpowiedzi, robota max Troop Mend Max Hours
(24 h). Kwatermistrz przy otwarciu zbrojowni melduje na zolto ile sztuk jest zuzytych i ze
kowal je naprawi. 3 nowe ustawienia MCM (grupa The men's gear).
**Ryzyko / co sprawdzic:** tooltip z kwotami na zdrowym rozsadku; po naprawie polki maja czyste
przedmioty (bez stanu) w tej samej liczbie; zloto schodzi dokladnie o wypisana kwote.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — luczarnia: pamiec ostatniej polki + stamina i ryzyko jak przy broni
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/Recipes.cs`, `src/Forge.cs`, `src/Settings.cs`, `tools/gen_mcm.py`
**Problem:** (1) wejscie w luki wymaga za kazdym razem klikania typ->tier od nowa;
(2) luk kosztowal tier x 25 x 0,8 staminy (t5 = 100 - DWA luki i koniec sesji), a ryzyko
zepsucia bylo takie samo jak przy hartowaniu plachy ("zawsze 50%, bez sensu" - Jeff);
kuznia broni pozwala kuc duzo wiecej.
**Przyczyna:** mnoznik 0.8 zaszyty w BuildRecipe; FailureChance nie odroznia luczarni od platnerki;
brak pamieci nawigacji.
**Zmiana:** (1) menu luczarni pamieta ostatni typ+tier i pokazuje na gorze "Back to the bench
you left"; po kuciu natychmiastowym (zakladka CRAFT) lista zostaje otwarta na tej samej polce.
(2) nowe ustawienia: Ranged Stamina Factor (0,35 - t5 luk = 43 staminy, 4-5 lukow na sesje)
i Ranged Failure Factor (0,5 - ryzyko lukow/kusz/amunicji o polowe nizsze). Flaga Ranged w
recepturze. PRZY OKAZJI: gen_mcm.py mial zaszyte sciezki /home/claude ze starego komputera -
teraz liczy sciezki wzgledem repo.
**Ryzyko / co sprawdzic:** procent ryzyka na polce lukow (ma byc ~polowa dawnego), ile lukow
schodzi na jednej stamince, czy "Back to the bench" wraca na wlasciwa polke.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — miniatury 3D w listach rozbiorki, naprawy i tygla
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`
**Problem:** w listach "Take a piece apart", "Pick a damaged piece to mend" i "Break metal down"
po samym opisie nie widac, co to za przedmiot - lista kucia miala obrazek, te trzy nie.
**Przyczyna:** InquiryElement dostawal `null` zamiast ItemImageIdentifier.
**Zmiana:** wspolny pomocnik `ItemPic(item)` (ItemImageIdentifier w try/catch), uzyty we
wszystkich czterech listach (kucie tez przepiete na niego).
**Ryzyko / co sprawdzic:** czy miniatury sie renderuja i nic nie wywala przy otwieraniu list.
**Status:** DO SPRAWDZENIA

## 2026-08-25 — wyjscie ze snu przez ExitToLast (naprawa CTD)
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`
**Problem:** klikniecie **"Rouse the men early"** w menu snu wywalalo gre. Log:
`NullReferenceException | BLAME: SandBox | GameMenuVM.OnFrameTick()`, `Menu: camp`.
**Przyczyna:** obie drogi wyjscia ze snu wolaly `GameMenu.SwitchToMenu(_sleepReturn)`
z wnetrza opcji menu oczekiwania — VM menu jest wtedy w polowie klatki.
Sasiedni, dzialajacy przycisk "Break camp" od zawsze uzywal `ExitToLast()`.
**Zmiana:** nowa metoda `LeaveSleep()` wolajaca `GameMenu.ExitToLast()`; uzyta w opcji
"Rouse the men early" i przy naturalnym koncu snu.
**Ryzyko / co sprawdzic:** wyjscie ze snu wraca teraz o poziom wyzej (zwykle na mape),
a nie do menu obozu. Sprawdzic, czy nie ma crasha.
**Status:** WGRANE

## 2026-08-25 — cofniecie calego dziennego bloku obozowania
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`, `src/Settings.cs`
**Problem:** crash w `GameMenuVM.OnFrameTick` w menu obozu. Przeszukanie 23 logow z 5 dni:
sygnatura wystepuje w **2 sesjach po zmianie**, w **21 wczesniejszych zero**.
**Przyczyna:** naprawa `_tentBroken` (globalnego wylacznika namiotow) **plus** wlaczenie
obozowania bandom naraz. Do tej pory tent-system praktycznie sie wylaczal po pierwszym
wyjatku; po naprawie zaczal obslugiwac setki partii i grzebac w ich wizerunkach na mapie
przy kazdym nocnym ticku.
**Zmiana:** cofniete do stanu porannego: obozuja tylko lordowie i karawany, bez dlugu snu
w pogoni, bez limitu namiotow, ze starym `_tentBroken`.
**Ryzyko / co sprawdzic:** **wraca stary blad** — namiot gracza moze zniknac po pierwszej
wywrotce. Do zrobienia na nowo, ostroznie, po jednej rzeczy.
**Status:** COFNIETE

## 2026-08-25 — kultura duchownych i ochotnicy miejscowi
**Mod:** CrashScribe | **Pliki:** `src/Mends.cs`, `src/WarReport.cs`, `src/Config.cs`
**Problem:** w polnocnych wsiach (White Ranch, Last River, Snowwood) mozna bylo zobaczyc
**wolny lud** i **Westerlands Noble Youth** wsrod ochotnikow.
**Przyczyna:** BK tworzy duchownego z presetu wiary (`HeroCreator.CreateSpecialHero`),
wiec kaplan ma kulture presetu, nie osady — a ochotnikow dobiera sie wedle kultury notabla.
Nasza wczesniejsza latka na `GetPossibleSpawns` leczyla objaw; lista i tak sie odnawiala.
**Zmiana:** `Mends.LocalLevies(Settlement)` prostuje kulture notabla do kultury osady
i zeruje obce sloty ochotnikow — przy wejsciu do osady i raz dziennie.
**Ryzyko / co sprawdzic:** w logu `Mends: <imie> w <osada> byl X, jest Y (Preacher)`.
W sesji 25.08: **558 poprawionych notabli**, spisy osad czyste.
**Status:** WGRANE

## 2026-08-25 — odblokowanie BannerKings (dwie latki na BKROTPatch)
**Mod:** CrashScribe | **Pliki:** `src/Mends.cs`
**Problem:** **pusty ekran rekrutacji** ("Recruit All (0)"), 11 487 wyjatkow na sesje.
**Przyczyna:** patrz `docs/ERRORS.md` A1 i A2 — BKROTPatch dusil generowanie duchownych
(`Invoke(null)` na metodzie instancyjnej) i przerywal inicjalizacje stylow zycia.
**Zmiana:** `Mends.Unhook()` zdejmuje te dwa prefixy z metod BannerKings.
**Ryzyko / co sprawdzic:** w logu `Mends: BKROTPatch nie dusi juz duchownych BK...`.
Ekran rekrutacji ma byc pelny.
**Status:** WGRANE

## 2026-08-25 — reszta prac tego dnia (skrot)
**Mody:** Armoury, RealisticCaptivity, CrashScribe
- **Ksiega wzorow** — po kazdym wykuciu widac ile wzorow znasz i ile do nastepnego.
- **"Take a piece apart to copy its pattern"** — rozbiorka zdobytej sztuki omija kolejke nauki.
- **Kolejka pancerza rozbita na 7 galezi** (helmy/kirysy/nogawice/rekawice/plaszcze/tarcze/kropierze).
- **Ucieczka konno ze spotkania** (`HorseFlight.cs`) — caly oddzial w siodle moze uciekac,
  straz tylna tylko przeciw jezdzie wroga.
- **Lamanie sie rannych** (`BrokenMen.cs`) — ponizej 20% HP AI panikuje i schodzi z pola.
- **Stamina kowalska** regeneruje sie wszedzie poza kuznia (przedtem tylko w 4 konkretnych menu).
- **Rozrusznik fabuly ROT** + dziennik kolejki (`Fabula.cs`).
- **Spis notabli** przy wejsciu do osady (`WarReport.cs`).
**Status:** WGRANE
