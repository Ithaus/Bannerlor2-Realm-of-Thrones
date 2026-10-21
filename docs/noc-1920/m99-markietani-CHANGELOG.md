## m99-markietani - rzad markietanow w obozie gracza: cyrulicy, zabawa, sklep (Armoury)

**Dla Jeffa (prosto):** w Twoim obozie (klawisz O) jest nowa opcja **Visit the sutlers' row**. Dziala tylko w duzym obozie: od 150 ludzi
w Twojej druzynie albo w calej armii, w ktorej jestes. Im wiekszy oboz, tym wiecej kramow (1 na 100 ludzi, najwyzej 12), a z kramami rosnie oferta.
Sa trzy rzeczy: **cyrulicy** opatruja rannych za oplate (wracaja od razu do sluzby, najpierw weterani), **zabawa** (piwo, kosci, minstrel) raz na dobe
podnosi morale Twojej druzyny, a **sklep** sprzedaje jedzenie, piwo, wino, narzedzia, strzaly i belty z targu najblizszego przyjaznego miasta,
po cenie miasta plus 30% za dowoz (skupuja od Ciebie po cenie miasta minus 30%). Towar naprawde znika z targu tego miasta, a Twoje pieniadze ida do tego
miasta i do pobliskiej wsi, tak jak pieniadze zolnierzy u markietanow.

**Problem:** Jeff 20.10 (`docs/ZADANIA-OBOZ-NOC.md` w repo glownym, lista pkt 7): "Ogolnie mocno rozbudowac obozowanie armii: im wieksza armia, tym wiekszy
oboz, wrecz miasteczko obozowe z markietankami"; DECYZJE JEFFA PO RAPORCIE (14:20) pkt 4: "MARKIETANI: uslugi, rozrywka i sklep - tylko w duzych obozach
(armia)". Rozpoznanie `docs/ROZPOZNANIE-OBOZ-2026-10-20.md` rozdz. 7: "Nasi markietani (Sutlers) to tylko obieg zlota [...] Markietani nie maja obrazka,
menu, sklepu ani postaci"; plan: "Sklep markietanow w menu obozu - towary z polek tego miasta, do ktorego i tak plynie ich zloto, z narzutem za dowoz".

**Dowod (stan przed zmiana, z kodu):** `Sutlers.cs` (E5/A3) - ludzie partii w polu wydaja codziennie czesc sakiewki ludzi, pieniadz do kasy najblizszego
niewrogiego miasta (85%, hak 75/25 TownFolk.EarnService) i kiesy najblizszej wsi (z danina pana VillageTakings.Split) - bez menu i bez towaru.
Menu obozu gracza `arm_camp_wait` (NightRest) - ZWYKLE menu (nie menu oczekiwania), juz z opcjami "Walk through the camp" (CampWalk) i "Train the men"
(CampDrill, SwitchToMenu z opcji - bezpieczne w zwyklym menu). Wzorzec handlu z pola: `SideVillageVisit` V4 (koszyk leniwie, InventoryLogic z wlasnym
IMarketData/listenerem, po "Done" magazyn o roznice koszyka). Gra (`InventoryLogic.DoneLogic`, dekompilacja 1.4.8): bez osady i bez spotkania jedyny przelew to
`GiveGoldAction.ApplyBetweenCharacters(null, gracz, min(-TotalAmount, listener.GetGold()))` - druga strone placimy sami (jak SideVillageVisit.TradeDone).

**Zasada (liczby - decyduje Claude):**
- **Kiedy:** oboz gracza (arm_camp_wait) poza osada, na ladzie, bez bitwy/oblezenia/spotkania. Oboz = kolumna jak w CampWalk: druzyna gracza, a w armii
  (gracz wodzem albo doczepiony do wodza) wodz i wszystkie doczepione partie; liczeni zdrowi i ranni. Rzad stoi od **SutlerRowMinMen = 150** ludzi
  (ponizej opcja szara z podpowiedzia). Kramy = oboz / **SutlerRowMenPerStall 100**, najmniej 1, najwyzej **SutlerRowMaxStalls 12**.
- **Zrodlo:** najblizsze miasto niebedace w wojnie z graczem (to samo `Sutlers.NearestTown`, do ktorego plynie obieg AI; brak - najblizsze w ogole).
- **Zaplata uslug** (cyrulik, zabawa, narzut sklepu) = obieg markietanow jak `Sutlers.One`: `SutlerTownShare` (85%) do kasy miasta-zrodla z hakiem 75/25
  (`TownFolk.EarnService` KLife) i licznikiem `TownFolk.Note`; reszta do kiesy najblizszej niewrogiej wsi w stanie Normal (z danina pana `VillageTakings.Split`
  przy Village Tribute From Sutlers; wies rodu gracza bez daniny); brak wsi - calosc do miasta.
- **Cyrulicy:** ranni zolnierze (bez bohaterow) wracaja do sluzby, najwyzej **SutlerSurgeonPerStall 8** na kram na dobe, najpierw najwyzszy tier;
  cena **SutlerSurgeonFee 15 x (tier + 1)** za czlowieka (T1 30, T3 60, T5 90). Bez latki modelu leczenia gry.
- **Zabawa:** raz na dobe, **SutlerRevelPerMan 2** denary na zdrowego czlowieka druzyny gracza; morale **+SutlerRevelMorale 6** przez RecentEventsMorale
  (jak ArmyRestDay: gra zdejmuje 10% dodatku na dobe - po 3 dobach ok. +4.4, po 7 ok. +2.9). Tylko druzyna gracza (to on placi).
- **Sklep:** koszyk liczony przy kliknieciu z **polki miasta-zrodla**: jedzenie (IsFood), piwo, wino, narzedzia, strzaly, belty; kazdego rodzaju najwyzej
  **SutlerRowStockPerStall 5 x kramy** sztuk na dobe (mniej kupione dzis, nie wiecej niz na polce). Cena kupna = cena miasta x (1 + **SutlerRowMarkupPercent 30%**),
  skup = cena skupu miasta x (1 - 30%). Gotowka na skup = min(kasa miasta, **SutlerRowCashPerStall 1500 x kramy**). Po "Done": kupione schodzi z polki miasta
  (dokladnie roznica koszyka), sprzedane wchodzi na polke. Pieniadz: gracz netto D (zmiana jego kiesy, ktora zrobila gra); narzut kupionego M = wartosc po cenie
  markietanow - po cenie miasta (w 0..D) - obiegiem uslug; D - M do kasy miasta (towar po cenie miasta; ujemne = miasto placi za skup). Suma = D.
- **Oboz 150 / 400 / 1000 ludzi:** kramy 1 / 4 / 10; cyrulicy 8 / 32 / 80 rannych na dobe; towar 5 / 20 / 50 sztuk kazdego rodzaju na dobe; gotowka skupu
  1500 / 6000 / 15000; narzut 30% (ziarno za 10 w miescie - 13 u markietanow, skup 7). Zabawa kosztuje 2 x zdrowi druzyny gracza (150 ludzi - 300).
- **Uzasadnienie:** markietani wozili towar z najblizszych targow i doliczali dowoz i ryzyko - 20-40% ponad cene targu (30% w srodku); cyrulik opatrywal
  kilku-kilkunastu ludzi dziennie (8 na kram); oplata rosnie z tierem (drozszy czlowiek, drozsze opatrunki, wiekszy zysk z powrotu). Zabawa za mniej niz dzienny
  zold - drobny, przemijajacy dodatek do morale, raz na dobe.
- **AI bez zmian** - obieg AI liczy dalej `Sutlers.cs`.

**Zmiana (Armoury):**
- NOWY `Armoury/src/CampSutlers.cs`: opcja "Visit the sutlers' row" w `arm_camp_wait` -> zwykle menu `arm_camp_sutlers` ("Pay the barber-surgeons to tend N
  wounded (X denars)", "Stand the men a round - ale, dice and a minstrel (X denars)", "Trade with the sutlers", "Back to the camp"); ekran handlu gry
  (InventoryState Trade, wlasne `RowMarket : IMarketData` i `RowListener : InventoryListener`), rozliczenie w `DoneLogicExtrasDelegate`.
  Stan dnia (doba, zabawa dzis, opatrzeni dzis, kupione dzis wedlug rodzaju) w zapisie `arm_sutlerrow` przez `SaveText.Sync` (osobny try; brak klucza = pusty dzien).
  Kazda opcja we wlasnym try - wyjatek = licznik potkniec + ERROR raz na miejsce, menu dziala dalej.
- `Sutlers.cs`: `NearestTown` / `NearestVillage` z private na internal (bez zmiany dzialania).
- `ArmouryBehavior.cs`: jedna linia `CampSutlers.RegisterEvents(this)` + blok SyncData `arm_sutlerrow`.
- `Settings.cs` (grupa MCM "Camp and baggage (night 20/21.10)"), `McmSettings.cs` z `gen_mcm.py`, zakresy suwakow w `gen_mcm.py`. Klucze NOWE: **CampSutlersRow**
  (TAK), **SutlerRowMinMen** (150), **SutlerRowMenPerStall** (100), **SutlerRowMaxStalls** (12), **SutlerRowMarkupPercent** (30), **SutlerRowStockPerStall** (5),
  **SutlerRowCashPerStall** (1500), **SutlerSurgeonPerStall** (8), **SutlerSurgeonFee** (15), **SutlerRevelPerMan** (2), **SutlerRevelMorale** (6).
- `tools/sprawdz_logi.py --grupa m99markietani`: MK-0 start (klucz), MK-1 ERROR CampSutlers i potkniecia 0, MK-2 uslugi i sklep tylko w duzym obozie,
  MK-3 pieniadz w obiegu (kazda zaplata = suma odbiorcow, roznica 0; linia doby roznica 0), MK-4 towar z polki miasta = towar w sklepie (zdjete = kupione,
  brak na polce 0, sprzedane = na polke, netto = towar + narzut), MK-5 uzycie (INFO). Sprawdzone na liniach probnych (6/6) i na logu biegu 98 (INFO - brak paczki).
- Linie logu: `Markietani obozu (m99): start sesji | ... klucz Camp Sutlers Row TAK | prog obozu ...`, `... cyrulik | oboz N ludzi (prog P), kramy K | opatrzeni ...
  | gracz zaplacil X -> kasa miasta + lud + kiesa wsi + danina | kontrola: X = Y (roznica 0)`, `... zabawa | ...`, `... sklep otwarty | ... koszyk z polki miasta:
  k rodzajow, n sztuk ...`, `... sklep zamkniety | ... kupione a sztuk (cena markietanow, miasta), z polki miasta zdjete ..., sprzedane ... | gracz netto D = towar
  do kasy miasta + narzut | kontrola ...`, doba (tylko gdy cos sie dzialo): `... dzien N | wizyty | cyrulik | zabawa | sklep | obrot uslug i narzutu -> ... (roznica 0)`.

**Ryzyko / co sprawdzic (kontrola calosci):**
- **Regresje:** Sutlers.cs - tylko widocznosc dwoch metod; AI i obieg sakiewek bez zmian. Sakiewka ludzi gracza (MenPurse) dalej wydaje u markietanow AI-regula -
  to pieniadz ludzi, a rzad markietanow bierze zloto z kiesy GRACZA (Hero.Gold) - nic liczone dwa razy. Menu `arm_camp_wait` - nowa opcja (indeks 3), stare bez zmian.
- **Zamknieta ekonomia:** uslugi - zloto gracza (GiveGoldAction do nikogo) = kasa miasta + lud + kiesa wsi + danina, w jednym kroku bez wyjatkow pomiedzy;
  sklep - gra zmienia kiese gracza, my: polka miasta o roznice koszyka, kasa miasta o (D - M), narzut M obiegiem. Skup: gracz dostaje najwyzej gotowke = min(kasa miasta,
  limit), miasto placi tyle samo (D ujemne). **Do sprawdzenia w grze**: czy `Town.ChangeGold` ujemne nie przycina ponizej zera (limit gotowki to wyklucza).
- **Skup dowolnego towaru:** ekran handlu przyjmuje od gracza kazdy przedmiot (takze lup: bron, zbroje, konie) po 70% ceny skupu miasta, w granicy gotowki
  (1500 na kram) - to nowy "targ w polu" dla lupu (historycznie markietani skupowali lup). Jesli za latwo - zmniejszyc SutlerRowCashPerStall albo podniesc narzut.
- **Leczenie od reki:** ranni wracaja natychmiast (za oplate, z dziennym limitem kramow) - przyspiesza powrot do sily po bitwie w duzym obozie; model leczenia gry
  bez zmian. Bohaterowie (towarzysze) nie sa leczeni.
- **Cudza armia:** opcja jest tylko w naszym menu obozu `arm_camp_wait`; jesli w armii cudzego wodza gra nie pokazuje tego menu (CampDrill: "menu obozu
  niedostepne"), rzadu nie widac - do sprawdzenia; menu oczekiwania armii gry (`army_wait`) celowo NIE ruszane (SwitchToMenu z menu oczekiwania = CTD).
- **Miasto po lupie (CitySack):** zrodlo nie pomija lupionego miasta (Sutlers.One wstrzymuje tam czesc miejska) - narzut i towar ida normalnie. Drobne.
- **Napisy opcji** ustawiane w warunku (MBTextManager) - ten sam sposob co w CampDrill/SideVillageVisit; gdyby napis byl pusty przy pierwszym wejsciu - kosmetyka.
- **Zapis:** nowy klucz `arm_sutlerrow` (maly napis), stary zapis - pusty dzien; wczytanie nie odnawia zabawy ani limitu cyrulikow (stan w zapisie).

**Status:** DO SPRAWDZENIA (build Release kod 0; nie uruchamiane w grze).

**Test w grze krok po kroku:**
1. Wczytac zapis; w `Armoury.log` linia `Markietani obozu (m99): start sesji | ... klucz Camp Sutlers Row TAK`.
2. Druzyna < 150 ludzi, bez armii: klawisz O -> "Visit the sutlers' row" szara, podpowiedz "Only a big camp draws sutlers - 150 men or more (your camp: N)".
3. Druzyna >= 150 albo w armii (razem >= 150): opcja czynna -> menu z opisem kramow i miasta-zrodla.
4. Z rannymi: "Pay the barber-surgeons to tend N wounded (X denars)" -> liczba rannych w druzynie spada o N, kiesa o X, komunikat "You paid X to the barber-surgeons...";
   w logu linia `cyrulik` z `kontrola: X = X (roznica 0)`. Drugi raz tego dnia - najwyzej limit kramow (potem szara).
5. "Stand the men a round" -> komunikat o zabawie i morale (+6 w podpowiedzi morale druzyny jako "Recent events"); drugi raz tej doby szara.
6. "Trade with the sutlers" -> ekran handlu "Sutlers' row" z jedzeniem/piwem/strzalami; ceny ok. 30% wyzsze niz w miescie-zrodle. Kupic cos i sprzedac cos ->
   Done. W logu `sklep zamkniety` (kupione = zdjete z polki, brak 0). Pojechac do miasta-zrodla - na targu mniej tego towaru.
7. Po dobie: linia `Markietani obozu (m99): dzien N | ... (roznica 0)`; `python -I tools/sprawdz_logi.py --grupa m99markietani <Armoury-*.log>` - MK-0..MK-4 TAK.
