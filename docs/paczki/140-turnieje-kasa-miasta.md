<!-- SKLAD TOWARY 3 (07.10.2026), galaz t3-sklad w klonie lancuch, baza 126 (4563c75). NIEWGRANE - **Status** dopisze sie przy wgraniu.
     Paczka 140 = commity cc40d0a (zrodlo e010a25, n131g-turnieje-stawki-historyczne) i cb75543 (poprawka po przegladzie, zrodlo ad6f6ce) na t3-sklad; GrandTourney. Opis: docs/przekazanie-lawa-2026-10-07/CHANGELOG-wpisy-3-6.md (UWAGA autorow: liczby kuzni w opisie 137 - projekt 3 + 2 x tier, karnet 15 d - sa NIEAKTUALNE po 142:
     dzien kuzni 3 d x poziom plac). -->

## 2026-10-07 (140) - TURNIEJE: OPLATA GOSPODARZA DO KASY MIASTA, PULA DLA ZWYCIEZCY, UTARG Z KASY MIASTA, KWOTY X POZIOM PLAC
**Mod:** GrandTourney | **Pliki:** NOWY `TownWageLink.cs` (jak w RealisticCaptivity), `TourneyBehavior.cs` (`HostFee`, `PurseOf`, `PayPurse`, `HostTournament`, zwroty przy odwolaniu, `OnTournamentFinished`), `Settings.cs` + `McmSettings.cs` (NOWE `HistoricalTownRates` = true, `HostFeePence` 4400, `HostTakingsBasePence` 480; 37 -> 40).

**Problem:** decyzja Jeffa 07.10 jak wyzej; spis (pozycje 43-45): oplata gospodarza 2000 + 0.5 x dobrobyt i pula gracza (3000 / 8000 / 15 000) ZNIKAJA (`ApplyBetweenCharacters(gracz, null, ...)`), pula nie trafia do zwyciezcy (sluzy tylko do zasiegu zaproszen), przy odwolaniu wraca polowa puli "od nikogo", utarg 200 na lorda + 0.1 x dobrobyt przychodzi od nikogo.

**Zmiana (wylacznik `HistoricalTownRates`):** oplata = `HostFeePence` (4400 d = to, co dotad w srednim miescie) x poziom plac (z Armoury) - do kasy miasta (ciesle, herold, uczta); pula schodzi z kiesy przy obwieszczeniu i czeka (zapisana w obwieszczeniu jak dotad) - przy zakonczeniu `PayPurse`: zwyciezca-bohater dostaje cala pule do sakiewki, zwyciezca spoza bohaterow - pula do kasy miasta; przy odwolaniu wraca CALA pula (oplata zostaje w miescie - przygotowania byly); utarg = (`HostTakingsPerLord` x lordowie + `HostTakingsBasePence`) x poziom plac - z kasy miasta, najwyzej tyle, ile ma.

**Przyklady (proba, sekcja 15; 6 lordow):** srednie miasto: oplata 4400 d (jak dotad), utarg 1680 d (jak dotad); Kings Landing: oplata 6373 (dotad 5476), utarg 2433 (dotad 1895); Pebbleton: 2875 (dotad 3568), utarg 1098 (dotad 1513). Pula 3000 -> zwyciezca (tu gracz) +3000; zwyciezca spoza bohaterow - kasa miasta +500.

**Ryzyko / co sprawdzic:** REGRESJE - przy wylaczonym jak dotad (GrandTourney.json Jeffa zawiera stare klucze - zostaja dla wylacznika). SPOJNOSC - kwoty w skali historycznej (oplata ok. 18 funtow, nagrody juz historyczne 400-1250 d), poziom plac z Armoury; pula nie znika. OTWARTE - turnieje AI nie maja puli w zlocie (bez zmian). Zwyciezca-lord AI dostaje pule gracza do sakiewki (wydaje ja jak swoje zloto). **Co Jeff zobaczy:** zwyciezca Twojego turnieju dostaje pule; jesli wygrasz sam - "The champion's purse is yours"; w bogatym miescie oplata i utarg wyzsze.

**Skladanie TOWARY 3:** commit cc40d0a - bez konfliktow; build GrandTourney kod 0. Proba lawy, sekcja 15 (z DLL GrandTourney lancucha) - zaliczona.

## 2026-10-07 (140, poprawka po przegladzie) - TURNIEJE: PULA GRACZA WRACA, GDY GRA ODWOLA TURNIEJ (OBLEZENIE MIASTA)
**Mod:** GrandTourney | **Pliki:** `TourneyBehavior.cs` (NOWE `OnTournamentCancelled`, `RefundPurse`; dzienny tick - wpis bez turnieju zwraca pule).

**Problem:** przeglad stosu (3/3, waga wysoka): pula gracza od paczki turniejow (e010a25) czeka na zwyciezce poza kiesa. Gdy miasto zostalo oblezone, gra sama odwolywala turniej (`ResolveTournament` -> zdarzenie `TournamentCancelled`, nie `TournamentFinished`); GrandTourney tego nie sluchal, a dzienny tick zdejmowal wpis bez zwrotu - pula (np. 15 000) przepadala.

**Przyczyna:** brak sluchacza `CampaignEvents.TournamentCancelled`; galaz `game == null` w ticku usuwala wpis bez rozliczenia.

**Zmiana:** `OnTournamentCancelled` zwraca pule (przy stawkach historycznych cala, inaczej `CancelledFeeRefund` - jak przy innych odwolaniach) i wycisza przejmowanie turnieju; wpis bez turnieju w ticku tez zwraca pule przed zdjeciem. Nasze wlasne odwolania zdejmuja wpis PRZED `ResolveTournament`, wiec zwrotu nie ma dwa razy. Komunikat: "The tourney at X is called off before a champion was crowned. N gold of your purse comes back to you."

**Przyklady (proba, sekcja 15):** pula 5000, oblezenie: gracz +5000 (dotad 0); drugie zdarzenie: +0; turniej AI bez puli: +0; przy wylaczonym `HistoricalTownRates`: zwrot wedle `CancelledFeeRefund`.

**Ryzyko / co sprawdzic:** REGRESJE - zwykle zakonczenie (`TournamentFinished`) bez zmian. SPOJNOSC - kazde odwolanie oddaje pule ta sama regula. **Co Jeff zobaczy:** po oblezeniu miasta z ogloszonym turniejem pula wraca z komunikatem.

**Skladanie TOWARY 3:** commit cb75543 - bez konfliktow.

**Poprawka po audycie TOWARY 3 (08.10):** (1) pula gracza zdjeta przed `CreateTournament` / `AddTournament` wraca do gracza, gdy ogloszenie padnie wyjatkiem przed wpisem (Z6 audytu); (2) opisy MCM `HostBaseFee`, `HostFeeProsperityFactor`, `HostTakingsProsperityFactor`, `CancelledFeeRefund` - "(not used while Historical Town Rates is on)". ZAPIS (sprostowanie SKLAD.md sekcja 5): format i znaczenie czwartego pola `gt_events` sie NIE zmieniaja - juz w n120 bylo to `prizeGold` (pula gracza; n120 zdejmowal ja z kiesy w nicosc razem z oplata); zmienia sie rozliczenie: przy zakonczeniu pula idzie do zwyciezcy, przy odwolaniu wraca cala (`HistoricalTownRates`). Zapis z turniejem gracza ogloszonym w n120: pula zdjeta juz przez n120 trafi do zwyciezcy albo wroci cala - bilans zlota sie zgadza (zdjeta w n120, wyplacona w t3).
