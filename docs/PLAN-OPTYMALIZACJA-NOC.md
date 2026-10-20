# PLAN: OPTYMALIZACJA - PRACA NOCNA (zapisane 20.10.2026 ok. 08:25 PDT)

Jeff 08.10: "Optymalizacja gry zajmiemy sie na koncu, zapisz to do zrobienia jak juz skonczymy ja modowac".
Jeff 20.10 (pytanie "czy w koncu zrobilismy ja, aby szybciej wszystko sie przeliczalo?", po wyjasnieniu): **"to zapisz na prace nocna"**.

KIEDY: najblizsza praca nocna PO WGRANIU 94 (fala 2 pomyslow). Wczesniej nie - kazdy nowy pomysl przesuwa gorace miejsca.

## Co juz jest (nie powtarzac)
- Etap 8 (13.10): O1-O6 (zbrojownie, targi, warsztaty, rozjazdy R9 = 0), bieg 3 e9dfa7f - liczby bez zmian.
- noc-wydajnosc (15/16.10): swaty ROT leniwe (MarriageLazyDraw), audyt ekonomii CS raz na 7 dob (EconomyAuditWeekly), ciche logi pomiarowe w grze (QuietLog).
- Linia logu "Profil doby (E8/0)" mierzy takt doby i najwieksze wywolania.

## Stan 20.10 (pomiar, biegi 160 dob z zapisu autotest-F-d240, x8)
| bieg | takt doby (mediana / srednia / max) | s/dobe biegu |
|---|---|---|
| n21-160 (WGRANIE 92) | 593 / 603 / 1359 ms | 15.6 |
| n22a-160 (92 + 15 galezi fali 1) | 570 / 572 / 710 ms | 15.0 |
Najwiecej srednio na dobe (n22a-160): ClanIncomeBook.Daily 92 ms, SupplyDemand.DailyTrade 71, KingdomTreasury.Daily 68, Sutlers.Daily 44,
BuildFunding.Daily 26, ClanBudget.Daily 24, WarLedger.OnDaily 20, Stage6Measure.Daily 17. GC: ok. 4000 gen0/min przy x8 (19.10).
Nowa kampania 120 dob: 12.3 s/dobe (n21) -> 12.0 (n22a).

## Zakres nocy
1. Przelom doby: 4 najwieksze (ClanIncomeBook, SupplyDemand.DailyTrade, KingdomTreasury, Sutlers) - pamiec posrednia, brak przegladania calego swiata kilka razy,
   bez LINQ/alokacji w petlach; cel: takt doby o polowe nizej.
2. Rozlozenie czesci pracy doby na godziny (np. po 1/24 rodow albo osad na godzine), zeby nie bylo jednego przestoju przy zmianie dnia - tylko tam, gdzie
   wynik nie zalezy od kolejnosci w dobie.
3. Smieci GC: listy/slowniki tymczasowe w goracych sciezkach (PartiesThink, tiki godzinowe, modele predkosci/finansow) -> pule i bufory wielokrotne.
4. Pomiar przed/po: ta sama budowa z wylacznikiem, profil doby, gen0/min (GcPulse CrashScribe), s/dobe.

## Zasady
- Liczby gry NIE moga sie zmienic: porownanie wszystkich grup sprawdz_logi (biegi 160 dob z zapisu i nowa 120) przed/po - tylko zmiennosc.
- Kazda zmiana pod wylacznikiem MCM (domyslnie TAK), watkobezpiecznie, zapis bez zmian.
- Tor L: workflow (kod -> 2 recenzje: wynik bez zmian + technika -> poprawki), scalenie, testy, wgranie po tescie; raport rano.
