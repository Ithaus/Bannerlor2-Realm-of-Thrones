# Dlaczego gra zwalnia i co z tym zrobic (raport dla Jeffa, 07.11)

Wszystkie liczby pochodza z logow Armoury i CrashScribe z Twoich sesji. W tych sesjach nie ma linii AUTOTEST. Gdzie nie mam dowodu, pisze to wprost.

## 1. Dlaczego gra zwalnia, od najwazniejszego

**A. Gra zwalnia w trakcie jednej sesji, a po wczytaniu wraca do normy. To jedyny twardy slad "coraz wolniej" i nie wiemy jeszcze, kto za nim stoi.**
- W sesji 06.11 18:15 (90 dob) doba gry na mapie trwala na poczatku srednio 22.8-24.4 s, a pod koniec 33.4-36.4 s. To okolo 50% dluzej.
- Sesja 17:34 pokazuje to samo: 22 s, potem 24.5 s po okolo 45 dobach.
- Spowolnienie zaczyna sie po okolo 45-60 dobach gry, czyli mniej wiecej po godzinie grania. Po wczytaniu zapisu kazda sesja znow zaczyna od 22-24 s.
- Razem z tym rosnie "smiecenie" w pamieci (male sprzatania pamieci przez .NET na sekunde gry): z okolo 45 do okolo 80.
- **Nasz dobowy koszt Armoury w tym czasie stoi w miejscu** (2140-2568 ms na dobe, bez trendu). Liczba partii na mapie tez stoi (4711-4774).
- Sprawca jest wiec poza tym, co mierzymy. Moze to byc silnik, StrategicCampaignAI, BannerKings, RBM, Diplomacy albo nasze latki dzialajace co klatke lub co godzine. **Dowodu, czyje to jest, nie ma.**

**B. Przyciecie o polnocy, czyli nasz takt doby. Ten koszt jest staly i nie rosnie w trakcie sesji.**
- Jedno wywolanie o polnocy trwa u Ciebie srednio okolo 0.9 s (sesja 07.11 00:27, mediana 894 ms). 22.10 bylo to okolo 0.4 s.
- Co 7 dob przyciecie siega 1.2-1.3 s, a w pierwszej dobie po wczytaniu 1.5 s ([00:40:08] 1482.7 ms).
- Szczyt co 7 dob robia nasze sondy diagnostyczne cen: PriceWalk299, PriceWalk299b i PlayerHaggle. Kosztuja razem okolo 270 ms i niczego nie zmieniaja w grze, tylko pisza do logu.
- Razem z kosztami partii nasz kod bierze okolo 1.8-2.6 s na dobe. To okolo 4% czasu doby: czuc to jako przyciecie, nie jako ciagle wolniejsza gre.
- Wzrost z 0.4 do 0.9 s pochodzi z nowych paczek kodu, nie z wieku kampanii. Na tym samym kodzie i tych samych dniach (108650-658) roznica to tylko +13%.

**C. Najdrozsze czesci naszego kodu na dobe (Twoja sesja 07.11):**

| Czesc | Koszt na dobe | Co robi |
|---|---|---|
| AiGear, zalogi | 340-480 ms | zakupy i naprawy sprzetu w 227 garnizonach |
| Musztra lordow i liczenie nadwyzki sprzetu | ~175 + ~143 ms | rosnie z liczba druzyn lordow |
| SupplyDemand | ~130-180 ms | wywoz broni przez kupcow; zalegajacy towar 30-33 tys. sztuk |
| MorningReport | 90-190 ms | poranny meldunek kwatermistrza; skacze razem z tym, ile pustych miejsc ma rynsztunek Twojej armii |
| ClanIncomeBook, KingdomTreasury | ~85-100 i ~70 ms | ksiegi pieniedzy klanow i krolestw |

**D. Swaty ROT (cudzy mod).** Codziennie trwaja 535-573 ms: 2231-2299 lordow szukajacych zony, okolo 250 tys. ocen par (CrashScribe 07.11 02:30-02:31). To najdrozsza pojedyncza rzecz z cudzego moda, jaka mierzymy.

**E. Swiat rosnie powoli, ale to nie jest glowna przyczyna:**
- zywi bohaterowie: 7021, a dorosli lordowie AI: 2457;
- zolnierze w sluzbie: 247 tys. -> 268 tys. w 50 dob, czyli okolo +40% rocznie;
- wiersze zbrojowni DTE: okolo +50% rocznie, bez gornej granicy;
- plik zapisu: 34.1 -> 38.3 MB w tydzien (+12%).

**Co NIE jest przyczyna:**
- logowanie: najwyzej okolo 4 ms na dobe trwajaca okolo 53 s;
- liczba partii na mapie;
- przecieki w kolekcjach Armoury (sprawdzone wyrywkowo, nie przy kazdej kolekcji).

## 2. Co mozemy zrobic w naszym kodzie

Kolejnosc wedlug stosunku zysku do ryzyka. Zgodnie z zasadami: jedna zmiana naraz i osobny test.

1. **Pomiar spowolnienia w trakcie sesji** (punkt A). Trzeba dopisac do linii doby liczbe klatek mapy i sredni czas klatki, a co 30 dob krotki pomiar godziny. Sam pomiar niczego nie przyspiesza, ale bez niego nie wiemy, co naprawiac. Ryzyko male, bo niczego nie ruszamy na mapie.
2. **Wylaczyc w Twojej grze sondy cen PriceWalk299 i 299b albo puszczac je co 28 dob.** Zysk: okolo 270 ms co 7 dob, szczyt spada z ~1.25 s do ~0.9 s. Ryzyko niskie, bo sondy tylko czytaja. Trzeba wczesniej ustalic, czy sprawdzarka logow ich potrzebuje.
3. **Swaty ROT: kazdy wolny lord sprawdzany raz na 7 dob, z 7 razy wieksza szansa.** Zysk: okolo 0.4-0.5 s na dobe. Liczba slubow w roku zostaje ta sama. Ryzyko: inne pary, czyli inny los.
4. **Zakupy i naprawy w garnizonach rozlozone na tydzien**: kazdy garnizon raz w tygodniu, z kwota tygodniowa. Zysk: okolo 150-250 ms na dobe (szacunek z probki). Ryzyko srednie: zbrojownie przed oblezeniem beda mniej swieze, a zloto bedzie plynac rzadziej i wiekszymi kwotami. Potrzebny audyt ekonomii.
5. **Rozlozyc ciezkie raporty (MorningReport, SupplyDemand, NewLordKit, Sutlers) na rozne godziny.** Zysk: przyciecie o polnocy spada do okolo 0.4-0.6 s. Laczny koszt zostaje ten sam, tylko nie ma jednego zaciecia. Ksiegi pieniedzy zostaja tam, gdzie sa.
6. **SupplyDemand: najpierw stoper na kazda czesc, potem podzial na 2-4 godziny i ponowne uzycie gotowych list zamiast tworzenia nowych co dobe.** Zysk: okolo 50-100 ms na dobe i mniej smiecenia pamieci. Ryzyko: kolejnosc w ekonomii, potrzebny audyt.
7. **Musztra lordow i liczenie nadwyzki:** zapamietac wynik do zmiany skladu druzyny, a musztre liczyc co 2-3 doby. Zysk: okolo 70-120 ms na dobe. Ryzyko: inne tempo treningu.
8. **Pamiec cen na dobe dla zakupow AI.** Zysk: okolo 40-70 ms na dobe. Ryzyko: rozjazd z cena na ekranie handlu.
9. **MorningReport:** usunac dzien z klucza pamieci podgladu. Najpierw trzeba sprawdzic w kodzie gry, czy sklad armii i tak nie zmienia "wersji" co dobe przez XP i leczenie. Jesli zmienia, ta zmiana nic nie da. Zysk niepewny, do 90-190 ms.
10. **Log "weto teatru":** jedna linia na pare wodz-cel na sesje zamiast co dobe. Log jest mniejszy o 27% (51 -> ~37 MB na sesje), a procesor prawie nic nie zyskuje.

Laczny zysk z punktow 2-8 to okolo 1 s na dobe. Usuwa to wiekszosc przyciecia o polnocy, ale **nie usunie narastania w trakcie sesji** (punkt A), dopoki nie znajdziemy jego sprawcy.

## 3. Co mozesz zrobic sam

- **Zapisz i wczytaj gre co okolo 45-60 minut grania.** To dziala od razu i daje najwiecej: doba wraca z ~35 s do ~23 s, czyli do 35% szybciej.
- **Save Compact** w MCM Armoury (domyslnie wylaczony) zmniejsza nasza czesc zapisu. Na plynnosc mapy nie wplywa, a zapis skroci najwyzej o ulamek sekundy. Uwaga przy Ironman: jesli kiedys wrocimy do starszego DLL, trzeba najpierw wylaczyc Save Compact i zapisac gre, inaczej stary DLL nie odczyta naszych danych. Decyzja nalezy do Ciebie, moim zdaniem nie warto.
- Stare logi ScoutingFog i AIInfluence oraz logi silnika zabieraja tylko miejsce na dysku. Mozesz je skasowac, ale gra nie przyspieszy.
- **Nie mam dowodu**, ze wylaczenie ktoregos cudzego moda przyspieszy gre. Nie wylaczaj niczego w ciemno.

## 4. Czego nie wiemy i jak to zmierzyc

- **Kto spowalnia gre w trakcie sesji.** Potrzebny pomiar klatek mapy na dobe i sredniego czasu klatki oraz smiecenia pamieci przed i po sluchaczach co godzine. Bez tego nie da sie wskazac winnego.
- **Koszt cudzych modow w czasie** (StrategicCampaignAI, Diplomacy, BannerKings, RBM). Nasz profil ich nie widzi.
- **Czas sprzatania pamieci w ms.** Nie mierzymy go, a pamieci natywnej silnika nie mierzy nikt.
- **Czy dobowy koszt partii i treningu w grze jest liczony w jednym wywolaniu, czy rozlozony na godziny.** Nie sprawdzilem w kodzie gry.
- **Ile kosztuje jeden zolnierz w sluzbie** i jaki jest podzial na lordow, zalogi i milicje. Brak pomiaru.
- **Czy DTE usuwa puste wiersze zbrojowni.** Nie sprawdzilem w dekompilacji.
- **Porownanie z pazdziernikiem.** Better Time steruje predkoscia mapy, a pazdziernikowych logow pod tym katem nie sprawdzalem. Autotest musi miec staly mnoznik predkosci, np. 20 (nie 16), wpisany do linii startu.
- **Autotest nie oddaje Twojej gry**: sondy cen dzialaja w nim co dobe, a meldunku porannego nie ma. Decyzje o wydajnosci opieraj na Twoich sesjach.

---

## Lista zadan dla programisty (w kolejnosci, jedna zmiana = jedno wgranie)

| # | Plik | Zmiana | Zysk | Ryzyko |
|---|---|---|---|---|
| 1 | DayProfile.cs (+ nowy pomiar godziny i klatki, tylko odczyt) | Do linii doby dodac liczbe klatek mapy, sredni i maksymalny czas klatki, a co 30 dob GC gen0 przed i po sluchaczach HourlyTick. Dodac Process.PrivateMemorySize64 i liczebnosc najwiekszych slownikow. | Wskazuje sprawce spowolnienia w sesji (do 35% doby) | Male. Nie dotykac wizerunkow na mapie (CLAUDE.md pkt 7). |
| 2 | ArmouryBehavior.cs l.2981-2992, LogGate.cs l.78, Settings.cs (+gen_mcm.py) | Klucz MCM: PriceWalk299, 299b i sonda PlayerHaggle co 28 dob albo wylaczone. Nie usuwac zerowania licznikow ksiegi w PlayerHaggle.Daily. | ~270 ms co 7 dob, szczyt 1.25 -> 0.9 s | Niskie (kod tylko czyta). Uzgodnic grupy sprawdzarki 299/299b. |
| 3 | CrashScribe, Mends "Swaty ROT (Z.3)" | Wolny lord sprawdzany raz na 7 dob (wedlug id % 7), szansa razy 7. Regulator MarriageTargetPerYear bez zmian. | ~0.4-0.5 s na dobe | Inny ciag losowy. Sprawdzic "oczekiwane slubow" przed i po. |
| 4 | WarFront334.Theatre.cs l.48-51, 227-231 | Klucze "teatr\|" w osobnym zbiorze na cala sesje albo 1-2 przyklady na dobe plus licznik. Przy okazji sprawdzic w dekompilacji, czy ThinkPostfix moze biec rownolegle, bo HashSet jest bez blokady. | Log -27% (~14 MB na sesje), CPU kilka ms | Niskie (tylko log) |
| 5 | AiGear.cs OnDailyTickParty l.292-304, AiWear.MendInTown, GarrisonArmory.cs | Zakupy i naprawy zalog rozlozone na 7 dob wedlug indeksu osady, z kwota tygodniowa. | ~150-250 ms na dobe (szacunek z probki 1/16) | Srednie: ksiega pieniadza, SellWeek, swiezosc zbrojowni przed oblezeniem. Pelny audyt. |
| 6 | ArmouryBehavior.cs (kolejnosc taktu) | MorningReport, SupplyDemand, NewLordKit i Sutlers na rozne godziny. ClanIncomeBook i KingdomTreasury zostaja ("PO ksiedze pieniadza", l.3270-3300). | Przyciecie 0.9-1.3 s -> 0.4-0.6 s | Srednie: kolejnosc zdarzen w dobie |
| 7 | SupplyDemand.cs DailyTradeCore l.707-860 | Najpierw stoper na czesci: spis polek, ShelfBuy.Take/GetPrice, TradeCarts. Potem ponowne uzycie Dictionary (l.730) i HashSet (l.777) oraz ewentualnie podzial na 2-4 godziny. | ~50-100 ms na dobe i mniej GC | Srednie: KS-H, TradeCarts.Flush, MoneyLedger.Mark (l.700). Audyt zamknietej ekonomii. |
| 8 | Trening musztry (MobilePartyTrainingBehavior postfix), CtxOf/AiSurplus | Pamiec wyniku AiSurplus na partie do zmiany rostera; musztra lordow co 2-3 doby z mnoznikiem. | ~70-120 ms na dobe | Tempo treningu i zuzycia. Porownac linie musztry. |
| 9 | ArmsPricing/PricePostfix, ShelfMemory | Pamiec ceny (osada, przedmiot, doba) uniewazniana zmiana polki, dla AiGear i VolunteerKit. | ~40-70 ms na dobe | Rozjazd z cena na ekranie handlu |
| 10 | KitPreview.cs Key l.356-361, Get l.219 | Najpierw dekompilacja: czy TroopRoster.VersionNo zmienia sie przy dziennym XP i leczeniu. Jesli nie, usunac dzien z klucza dla wywolania porannego. Sprawdzic, czy dzien nie jest w kluczu celowo (K7). | 0 do ~90-190 ms na dobe | Niepewny zysk; losowy pancerz wzorca |
| 11 | LocalFront334e.cs (Raider) | Pamiec oceny celu na 2-3 godziny dla pary partia-cel | ~80-120 ms na dobe, rozlozone na godziny | Wolniejsza reakcja rabusiow |
| 12 | Dekompilacja DTE, potem GarrisonArmory/Stage6Measure | Sprawdzic, czy wiersze z iloscia 0 sa usuwane. Jesli nie, usuwac tylko puste. Szczyt GarrisonArmory.Hourly (126 ms) rozlozyc. | Szczyt godziny 126 -> <10 ms, hamuje wzrost ~50% rocznie | CLAUDE.md pkt 0 (DTE juz kiedys kasowal zbrojownie) |
| 13 | Linia doby lub ksiegi (tylko pomiar) | Rozbicie SLUZBY na lordow, zalogi i milicje; rozmiar kluczy SyncData na starcie | Dane do decyzji | Male |
| 14 | Autotest (skrypt) | Staly mnoznik predkosci (np. 20), wpisany do linii startu; sondy cen jak u Jeffa | Rzetelne "przed i po" | Brak |

Zrodla:
- logi Armoury: C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury\Armoury-2026-11-07_00-27-28.log, Armoury-2026-11-06_18-15-03.log, Armoury-2026-11-06_17-34-*.log
- logi CrashScribe: C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\session-2026-11-07_00-27-27.log, session-2026-11-06_18-14-59.log
- kod: C:\Users\GAME\AppData\Local\Temp\claude\bfd708ab6e1d6456\264cfac3-8338-4c40-91b8-6c1304da3584\scratchpad\n347\Armoury\src

Niczego nie zmienialem w plikach gry, repo ani w Configs.