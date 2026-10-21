# m99-optym - optymalizacja przelomu doby (noc 20/21.10, galaz w-toku/m99-optym, baza 7f155112 = WGRANIE 98)

Jeff 20.10: "Na koncu zrob optymalizacje gry, czy da sie przyspieszyc jeszcze." Plan: docs/PLAN-OPTYMALIZACJA-NOC.md (pkt 1 i 4; pkt 2 - rozlozenie na godziny - poza zakresem, zmienia kolejnosc).

## Dla Jeffa (prosto)
Zmierzylem, co najdluzej liczy sie o polnocy w grze (ok. 0.55-0.6 s na dobe). Cztery najwieksze kawalki to dalej te same: ksiega dochodow rodow, handel bronia
kupcow, skarbce krolestw i markietani - razem prawie polowa. Najwieksza czesc z nich to liczenie dochodu rodu przez sama gre i Banner Kings, a to liczenie po
drodze zapisuje pensje rady i podatki warsztatow - pominiecie go mogloby zmienic pieniadze w grze, wiec tego nie ruszalem. Bezpiecznie przyspieszylem jedna
rzecz (skarbiec pyta o wojne raz na krolestwo zamiast raz na kazdy rod) - zysk maly (ok. 1-2 ms na dobe), liczby gry te same. Wylacznik: Optim Daily Cache.

## Pomiar przed (WGRANIE 98, logi Armoury, linia "Profil doby (E8/0)", ostatnie 60 dob kazdego biegu)
Takt doby: nowa kampania 180 dob (Armoury-2026-10-20_23-25-07.log) mediana 530.5 / srednia 537.2 / max 587.2 ms;
160 dob z zapisu (Armoury-2026-10-21_00-07-42.log) mediana 597.5 / srednia 603.4 / max 684.4 ms.

| sekcja | nowa 180: srednia / mediana ms | z zapisu 160: srednia / mediana ms |
|---|---|---|
| ClanIncomeBook.Daily | 82.5 / 82.2 | 95.0 / 95.0 |
| SupplyDemand.DailyTrade | 67.9 / 67.5 | 66.8 / 66.0 |
| KingdomTreasury.Daily | 60.8 / 60.7 | 72.8 / 71.8 |
| Sutlers.Daily | 37.0 / 37.2 | 48.0 / 46.8 |
| BuildFunding.Daily | 23.8 / 23.6 | 25.7 / 25.1 |
| ClanBudget.Daily | 18.7 / 18.5 | 22.9 / 22.7 |
| WarLedger.OnDaily | 18.5 / 18.5 | 19.3 / 19.1 |
| TownFolk.Daily | 10.8 / 10.6 | 17.6 / 17.1 |
| Stage6Measure.Daily | 13.6 / 13.4 | 16.8 / 16.4 |
| OutlawLaw.Daily | 3.4 / 0.0 | 14.2 / 13.2 |

Nowe sekcje WGRAN 96-98 (QualityScarcity, BandsEatFood, TownDebt, RecipesByMass, VillageBook/SideVillage, CampDrill, InnDrink/Rumours) nie wchodza do "top 15"
linii profilu w zadnej z 60 dob (kazda ponizej ok. 9 ms) - gora tabeli jak w planie z 20.10. Te same liczby daje grupa m99optym (MO-1).

Rozbicie (linia "Szybkosc doby (perf-d2d3)", doba 109235): w ClanIncomeBook.Daily sam dochod modelu finansow (gra + BK, z opisami) to 78.6 ms z ok. 95
(krolow 29 rodow 7.1, z lennami 178 rodow 31.5, bez lenn 372 rodow 40.0 ms). KingdomTreasury.Daily: ok. 537 wywolan tego samego modelu (bez opisow).

## Zmiana dla kazdej sekcji
1. **KingdomTreasury.Daily, test wojny - ZROBIONE (OptimDailyCache, KingdomTreasury.Optim.cs).** "Czy krolestwo rodu jest w wojnie z innym krolestwem"
   (przeglad Kingdom.All z IsAtWarWith) liczone raz na krolestwo w petli rodow zamiast raz na kazdy rod z dochodem (ok. 537 rodow/dobe). Pamiec `_optWar`
   czyszczona na poczatku kazdej doby (OptBegin). Klucz NIE: WarOld - kod sprzed zmiany 1:1.
2. **KingdomTreasury.Daily, model dochodu - NIE RUSZANE (opis).** Przy podstawie "ziemia" (165, CrownDuesFromLand) dochod modelu jest liczony i od razu
   nadpisany przez StableLand / SavedLand u wszystkich 537 rodow (log: "czesc ziemia D stalego u 537 rodow, stara ... u 0") - liczba idzie tylko do porownania
   169b (NoteModelIncome). Pominiecie daloby ok. 55-65 ms/dobe, ALE wywolanie ma skutki uboczne w stanie gry (dekompilacja BannerKings.dll i nasz kod):
   (a) PopulationLaw.TownTaxPostfix -> CouncilWages35.TownBase zapisuje pensje rady BK (DueWage) miast i zamkow rodu z podstawy, w ktorej jest renta z
   biezacej kasy miasta; (b) BKClanFinanceModel.CalculateOwnerIncomeFromWorkshop zapisuje pamiec podatku warsztatow `workshopTaxes` (czytana przez
   BKTaxModel.CalculateTownTax, PopulationLaw, Seal31B, WorkshopTrade); (c) probki 1/64 PerfD2.FastProbe przesuwaja sie. Miedzy KingdomTreasury.Daily a
   ClanIncomeBook.Daily (ktory zapisuje (a) i (b) znowu) biegna kroki czytajace te liczby (np. IronBank.Daily liczy dochod modelu z linia "Councillor role").
   Bez wywolania czytalyby wartosc z innej chwili - liczby gry moglyby sie zmienic. Do decyzji: jesli kiedys - z odtworzeniem tych samych zapisow (TownBase +
   workshopTaxes) albo z przeniesieniem liczenia D(a), co zmienia kolejnosc doby.
3. **ClanIncomeBook.Daily - NIE RUSZANE.** Ok. 80% to model finansow z opisami; jego wynik idzie do D rodow (pierscien 28 dob -> powinnosci, budzety, Bank),
   a opisy sa potrzebne do rozbicia D stalego (StableClan). Reszta (ok. 15 ms) juz na buforach wielokrotnych; raport i CSV tylko w dobie pomiaru (LogGate).
4. **SupplyDemand.DailyTrade - NIE RUSZANE.** Juz po E8/O3 (TradeExportMemo: cele, popyt i mnoznik z pamieci, probka R9). Koszt to wycena sztuka po sztuce
   przy wywozie i petla celow, w ktorej kolejnosc rozstrzyga remis zysku (pierwszy w Settlement.All) - przebudowa = ryzyko innej kolejnosci; nie w tym czasie.
5. **Sutlers.Daily - NIE RUSZANE.** Koszt w MenPurse.CampPlan (rezerwy napraw AiWear) i ReadSaved -> KeptFor (tylko log, ale linia "Markietani (E5/A3)" idzie
   codziennie z oszczednosciami K1 - zmiana jej czestotliwosci zmienia linie czytane przez grupy progow; tego nie sprawdzilem, wiec nie ruszalem).

## Dowod rownowaznosci (KingdomTreasury.Daily, test wojny)
- Wejscie: stan dyplomacji (Kingdom.All, IsEliminated, IsAtWarWith). W petli rodow nic go nie zmienia: kroki petli to CalculateClanIncome bez pobrania
  (applyWithdrawals = false - bez wypowiedzen wojny i pokoju), PopulationLaw.RentToday (odczyt), StableLand / SavedLand (odczyt), ChangeHeroGold,
  KingdomBudgetWallet, CirculationWindows.NoteHeroGold i DuesToday (zloto i liczniki). Dla dwoch rodow tego samego krolestwa stara droga daje te sama odpowiedz.
- Ta sama funkcja: WarOld to doslownie dawny kod (ten sam warunek k != krolestwo rodu, !IsEliminated, IsAtWarWith, break, catch { } -> to, co ustalone przed
  wyjatkiem). Pamiec trzyma wynik pierwszego liczenia w dobie; liczenie jest deterministyczne (bez losowan), wiec kazde nastepne w tej dobie dawaloby to samo.
- Kolejnosc: petla rodow, kolejnosc platnosci i zaokraglenia bez zmian; w tym kawalku nie ma MBRandom; pamiec od zera co dobe (wojna albo pokoj miedzy
  dobami - nowe liczenie). Zapis gry bez zmian (pamiec tylko w sesji).
- Tryb kontrolny **OptimVerify** (domyslnie NIE): liczy tez stara droge, gra bierze JEJ wynik, linia "Optym (m99): dzien N | kontrola (Optim Verify) ...
  porownan X, roznic Y" (grupa m99optym MO-2: roznic 0, porownan > 0).
- Linia startu kampanii: "Optym (m99): start | Optim Daily Cache TAK/NIE, Optim Verify TAK/NIE | wlaczone: ... | bez zmian: ...".

## Oczekiwany zysk
KingdomTreasury.Daily: ok. 537 przegladow Kingdom.All -> jeden na krolestwo; szacunek 0.5-2 ms/dobe (ok. 0.1-0.3% taktu doby). Uczciwie: maly. Duzy zysk
(55-65 ms, ok. 10% taktu doby) bylby z pkt 2 - zablokowany przez skutki uboczne modelu finansow (wyzej), do decyzji.

## Ryzyko / co sprawdzic
- Regresje: zmieniony jeden warunek w KingdomTreasury.Daily (wojna -> stawka powinnosci); inne miejsca liczace wojne (ArrearLine, AtWar(k), petla
  krolestw w KingdomTreasury.cs) bez zmian. Kolizje: brak - nowy plik czesciowej klasy, nowe pola prywatne, nazwy OptBegin/OptEnd/WarOf/WarOld/
  OptimStartLine nie wystepuja nigdzie indziej w klasie.
- Wpiecia: 2 linie w KingdomTreasury.Daily (OptBegin, OptEnd) + zamiana testu wojny na WarOf, 1 linia w ArmouryBehavior.OnSessionLaunched (linia startu,
  wlasny try). Watek glowny (jak dotad).
- Klucze MCM: OptimDailyCache (TAK), OptimVerify (NIE) - nowe, nie ma ich w Armoury.json Jeffa (dzialaja domyslne). gen_mcm uruchomiony.
- Autotest: bieg z OptimVerify = TAK (MO-2 roznic 0) i bieg zwykly (MO-1 takt wobec bazy WGRANIA 98:
  `python -I tools/sprawdz_logi.py --grupa m99optym <log> --baza-doby Armoury-2026-10-21_00-07-42.log`), reszta grup sprawdz_logi wobec bazy - tylko zmiennosc.

## Status
DO SPRAWDZENIA - autotest porowna takt doby (MO-1) i progi (MO-2 kontrola roznic 0, MO-3 ERROR 0, pozostale grupy jak w bazie). Build Release: kod 0.
