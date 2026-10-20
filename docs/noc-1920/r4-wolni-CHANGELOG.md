# r4-wolni - Wolni Ludzie: walcza wszyscy dorosli (PLAN 4.17), galaz w-toku/r4-wolni na 1ffbd891 (WGRANIE 91)

Wersja 2 (po recenzji, commit po d478a6ef): pula = wszyscy dorosli 16-60 (54% ludu), zalogi WL bez zmian (jak 1ffbd891), w logu bramki
werbunku gry (limit druzyny, zloto, glod) dla kazdej druzyny lorda WL, bramka WL-7 w sprawdz_logi.py, poprawione podpowiedzi MCM i liczby dla Jeffa.

## Problem (dowod)
Logi testow na WGRANIU 91 (`Modules/Armoury/Armoury-2026-10-20_00-44-29.log` - 160 dob z zapisu, d109076-d109236;
`Armoury-2026-10-20_00-17-30.log` - nowa kampania 120 dob):
- linia "Budzet rodow (166)", odcinek "Wolni Ludzie bez zoldu (WL1)": pula **5276-5294** = 1.8% x ludzie 293-294 tys. (WL2, FreeFolkLevyShare 0.018);
  w ludziach (druzyny + zalogi rodow WL) **5326-5422** - przez cale 160 dob na pulapie. Werbunek wstrzymany przez pule **180-330 prob na dobe**,
  przyrost zalog wstrzymany przez pule 4-6/d, nowe partie wstrzymane 1/d; WL3 (jedzenie) wstrzymuje 0-35/d.
- nowa kampania: pula 5394 od pierwszej doby; wojsko rosnie 2648 -> 5352 i od ok. 40. doby stoi na puli (wstrzymane 150-270/d).
- "Straz i Wolni Ludzie (4.15)": Wolni Ludzie stan 10035 na starcie sesji -> **9022** na koncu (partie 6933, zalogi 2089; liczone z bohaterami,
  rannymi, taborami wsi i karawanami). To jest miara z pytania do Jeffa (RAPORT-NOCNY-2026-10-20 pkt 4c: "Wolni Ludzie ok. 8,5 tys. pod bronia
  (1,8% ludzi) - zostaje?"). Jeff 20.10 (STAN-PRAC, odpowiedzi na stare pytania): **"3. walcza wszyscy dorosli"**.
- "Krainy (4.15)": freefolk ludzie 293517, mezczyzni 79250 (27%), prog wolnych rak 100% (E4/7 - "walcza wszyscy" juz jest w ksiedze rak).
- Linia skarbcow: rody WL "kiesy razem" 1 363 729 (d109076) -> 809 708 (koniec), "na minusie" 0 -> 4-7 rodow z 18 przez caly bieg; krol Rayder
  272 tys. -> 107 tys. (bilans -0.8..-1.7 tys./d); wojsko rodow 3.3-3.5 tys.

## Przyczyna
WL2 (`ClanBudget.FfPoolCap`) liczy pule jako `FreeFolkLevyShare x ludzie regionow WL` = 1.8% - tymczasowy pulap "tyle, ile mieli w pierwszym
polroczu", nie decyzja "walcza wszyscy dorosli". Prog wolnych rak WL (100%) jest juz zrobiony; zostala sama pula wojska.

## Zmiana (Armoury, nowy klucz `FreeFolkAllAdultsFight` "Free Folk All Adults Fight", domyslnie TAK, grupa MCM "Reavers and the Free Folk", zaraz pod Free Folk Levy Share)
1. `ClanBudget.FfPoolShare`: przy kluczu pula WL = **wszyscy dorosli 16-60** (mezczyzni i wlocznice) = `FfAdultsShare` = 2 x `PeopleBook.MenShare`
   (model demografii, DEMOGRAFIA-SILA-ROBOCZA H4: mezczyzni 16-60 = 27% ludzi, plec 49:51 - kobiet 16-60 tyle samo; dzieci 37.5%, ponad 60 lat ok. 7.5%)
   = **54% x ludzie** regionow WL, dzis ok. **158 tys.** zamiast 5.3 tys. Bez klucza - FreeFolkLevyShare jak dotad. Pula dziala jak dotad wszedzie,
   gdzie ja czytano (werbunek od notabli, jency AI, nowe partie, zalogi, N7 zbiorka rozbitych, zold gracza-wasala WL) - nowych mechanik nie ma.
2. Zalogi WL: **bez zmian** wobec 1ffbd891 (`GarGate` - rosna do FreeFolkGarrisonPoolShare puli). Przy puli 158 tys. ta bramka nie wiaze; zalogi trzyma
   pulap zalogi gry (`GarrisonRecruitmentCampaignBehavior`: werbunek przyciety do `PartySizeLimit - NumberOfAllMembers`, tylko przy `FoodChange > 0`) -
   ROZPOZNANIE-WOLNI: zalogi WL rosly do ok. 3.0 tys. tego pulapu. (Wersja 1 liczyla udzial od zywego wojska - cofniete po recenzji, patrz nizej.)
3. Log, linia "Budzet rodow (166)": odcinek WL1 pokazuje prawdziwy udzial ("z ludu (WL2, ...): 54.0% x ludzie") i nowy odcinek
   **"Wolni Ludzie - walcza wszyscy dorosli (4.17, Free Folk All Adults Fight tak): ludzie N, dorosli 16-60 N (mezczyzni i wlocznice, 2 x 27% ludzi),
   pula N (54.0% x ludzie - wszyscy dorosli 16-60), pod bronia N (druzyny N, zalogi N) = X% puli, Y% ludzi; trzyma dzis: pula (werbunek N, jency N,
   nowe partie N, zalogi N), jedzenie WL3 N, zalogi ponad 50% puli N; druzyny lordow WL N (ludzie N / limity druzyn N): pelne N, bez zlota na
   werbunek (prog gry) N, w glodzie N, moga werbowac N; reszte trzymaja liczba wodzow, cel zalog gry i straty"**.
   Druzyny liczy nowa metoda `FfLordGates` (tylko odczyt, raz na dobe, ok. 23 druzyny) wedlug bramek gry z `RecruitmentCampaignBehavior.CheckRecruiting`
   1.4.8, kazda druzyna w jednej kategorii w tej kolejnosci: pelne (`NumberOfAllMembers + 0.5 > PartySizeLimit`), bez zlota (`PartyTradeGold <=
   HeroHelper.StartRecruitingMoneyLimit` = 50 + 20 x min(150, ludzie druzyny), albo u lorda, ktory nie jest glowa rodu, `Clan.Gold <=
   StartRecruitingMoneyLimitForClanLeader` = 50 + zold + 40 x ludzie druzyny glowy rodu), w glodzie (`IsStarving`), reszta - moga werbowac.
4. Podpowiedzi MCM: "Free Folk Men From People" i "Free Folk Levy Share" mowia, ze Levy Share dziala tylko przy "Free Folk All Adults Fight" = nie;
   podpowiedz nowego klucza bez "half" (wszyscy dorosli 16-60, ok. 54% ludu) i z lista hamulcow (limit druzyny, zloto wodza, jedzenie, liczba wodzow, straty).
   `tools/gen_mcm.py` - McmSettings.cs wygenerowany (2057 ustawien). Domyslne wartosci istniejacych kluczy bez zmian.
5. `tools/sprawdz_logi.py`, grupa wyspywolni: bramka **WL-7** z odcinka 4.17 (udzial 53.5-54.5%, pula - udzial x ludzie <= 1, bramki puli 0,
   pod bronia koniec >= start; druzyny pelne / bez zlota / w glodzie - INFO, sr. 28 dob). Sprawdzona na kopii logu 160 dob z dopisanym odcinkiem
   (TAK) i na prawdziwym logu (bez odcinka - INFO, bez wyjatku).

## Co naprawde trzyma wojsko WL po zmianie (kod gry 1.4.8 + log + ROZPOZNANIE-WOLNI rozdz. 3-4.2)
- **Limit druzyn gry** (PartySizeLimit, BK): u rodow bez zoldu limit w zlocie = max (`ApplyPartyLimits`: `if (b.Zero) lim = max`), wiec wiaze sam limit
  druzyny. ROZPOZNANIE-WOLNI (pomiar 2 lat): limity druzyn WL **5.2-5.8 tys.** w 7 rodach z ziemia; dzis w druzynach ok. 3.3 tys.
- **Zloto wodza (bramka werbunku gry)** - pominiete w wersji 1, recenzja ma racje. `CheckRecruiting` werbuje tylko, gdy `PartyTradeGold` > 50 + 20 x
  min(150, ludzie druzyny) (do ok. 3 tys. zl), a lord, ktory nie jest glowa rodu, takze gdy kiesa rodu > 50 + zold + 40 x ludzie druzyny glowy rodu
  (ok. 6 tys. zl). Werbunek kosztuje zloto (`GetTroopRecruitmentCost`; nasze RecruitCost nie robi wyjatku dla WL). Zadna nasza latka tego nie zmienia.
  Log: 4-7 z 18 rodow WL jest "na minusie" przez cale 160 dob - te rody juz dzis nie werbuja; kiesy rodow WL -554 tys. w 160 dob (srednio -3.5 tys./d,
  w ostatnich 48 dobach -1.8 tys./d) przy 3.3 tys. w druzynach. Przy 5-5.8 tys. w druzynach (minimum zoldu BK ok. 1 zl/czl/d, koszt werbunku,
  zakupy jedzenia) spadek przyspieszy do ok. -4..-6 tys./d: 0.81 mln wystarczy na ok. 130-200 dob (SZACUNEK). Potem werbunek WL stanie z braku zlota,
  nie z limitow druzyn. Do tej pory pula wstrzymywala te proby wczesniej (nasz RecruitPrefix biegnie przed bramka zlota gry), wiec w logu jej nie bylo widac.
- **Liczba wodzow / partii**: 23 druzyny WL; nowe partie wystawia gra (ConsiderSpawningLordParties - wolni bohaterowie rodu, limit dowodcow wedlug
  tieru); nasza bramka puli przestaje je wstrzymywac (dzis 1/d). Rody "gentry" BK (Wightsbane, Frosttooth, Crowsbane) bez druzyn - bez zmian.
- **Jedzenie**: WL3 (FreeFolkRecruitNeedsFood) - druzyna werbuje tylko z jedzeniem na NeededFoodsInDaysThresholdForRaid dni z nowymi ludzmi; AI kupuje
  jedzenie tylko za zloto wodza (znowu zloto). Dzis wstrzymuje 0-35/d (nowa kampania do 94/d); gra nie werbuje tez druzyny glodujacej (`IsStarving`).
- **Zalogi**: cel zalogi gry (jedzenie i zold - `CalculateSettlementGarrisonPartySizeLimitWithFoodAndWage`), przy K1 tylko z werbunku od notabli z ludzi
  regionu (4.7), tylko przy `FoodChange > 0`. ROZPOZNANIE: zalogi rosly do ok. 3.0 tys. pulapu gry.
- **Straty i morale**: zabici 11-25/d (glownie Straz i Inni), w 160 dobach zginelo 1012; dezercja z morale (glod, porazki) bez zmian.
- **Zold**: WL1 - zold 0, zostaje minimum BK ok. 1 zl/czl w druzynie, 0.5 w zalodze (dzis 3585 + 1027 zl/d), zalegly zold 0 przez 160 dob. Sam zold
  nie wiaze, ale placi go ta sama kiesa, ktora potem musi przejsc bramke zlota werbunku (wyzej).
- **Ludzie**: ksiega ludzi (pobor ze wsi werbunku, 4.8) - 293 tys. ludzi i prog wolnych rak 100%: kilka tysiecy wiecej to ok. 1% ludu.

**Ile wojska realnie wyjdzie (SZACUNEK)**, w mierze z pytania do Jeffa (linia 4.15 "Wolni Ludzie: stan", dzis 9.0 tys.): ok. **10-12 tys.**,
czyli ok. 3.5-4% ludu. Druzyny rodow 3.3 -> do 5-5.8 tys. (gorna granica = limity druzyn wodzow; rody bez zlota - 4-7 z 18 - nie dorosna), zalogi
2.1 -> do ok. 3 tys. (pulap zalogi gry). W mierze puli (druzyny + zalogi rodow): 5.4 -> ok. 7-9 tys. Przyrost rozlozony na tygodnie (ochotnicy
u notabli odnawiaja sie z czasem). Po ok. 4-7 miesiacach (SZACUNEK) zacznie trzymac zloto wodzow. Wiecej niz ok. 4% ludu wymagaloby zmiany limitow
druzyn, liczby wodzow albo werbunku bez zlota - tego nie ruszalem (zadanie: tylko zdjac pulap ludzi), to decyzja Jeffa.

## Ryzyko / co sprawdzic (kontrola calosci)
- **Mur i Nocna Straz**: Straz 8423 (partie 6682) wygrywa prawie wszystkie starcia z WL (ROZPOZNANIE: 393/407 bitew w roku 1), WL w polu nie atakuja
  druzyn Strazy. Wiecej WL w polu = wiecej celow dla Strazy i Innych: wiecej poleglych WL (dzis 1012 w 160 dob), wiecej strat Strazy (dzis 274), wiecej
  rabunkow Daru (WW1/WW3 dla WL - klucz ReaverRaidByProfitFreeFolk jest pierwszym krokiem wycofania).
- **Fabula ROT WallSiege** (WallSiegeEvent - os Fabuli 267 x FabulaTimeScale, AUDYT-KALENDARZ ok. 1157 dob): port CrashScribe `WallSetupWith`
  (Mends.cs:847-905) zbiera do armii oblezenia WSZYSTKIE druzyny lordow WL - armia pod Murem bedzie ok. 1.5-1.7 x wieksza (do 5-5.8 tys. zamiast 3.3 tys.,
  jesli zloto pozwoli dobic do limitow). W biegu 160 dob WallSiege nie wypada; pierwszy prawdziwy sprawdzian w kampanii Jeffa.
- **Nocny Krol / Inni**: OthersRise (4.18) - ciala tylko z poleglych za Murem i przy Murze, gdy wygrywa banda Innych; wiecej WL w polu = wiecej cial
  dla Innych (rosna szybciej, ale tylko z prawdziwych poleglych - zamknieta ksiega). Bramka WL-5 (trupy Innych <= 1.2 x baza) moze przez to nie przejsc -
  oczekiwane do ok. 1.5 x baza; ponad to - sprawdzic, skad ciala.
- **Glod za Murem**: druzyny - WL3 trzyma werbunek przy braku jedzenia, ale kupuja jedzenie z miast WL za zloto wodza; wieksze druzyny = wiekszy popyt
  na zywnosc w kilku miastach WL. Zalogi - cel gry liczy jedzenie. Sprawdzic "w glodzie" w linii 4.15 i w odcinku 4.17, zapas zywnosci miast WL.
- **Zloto WL**: wieksze wojsko szybciej oprozni kiesy rodow WL (wyzej). Zamknieta ekonomia bez zmian - nie ma nowych przeplywow zlota; to te same
  oplaty (minimum zoldu BK, koszt werbunku, jedzenie), tylko za wiecej ludzi.
- **Regresja**: bez klucza kod = 1ffbd891 (FfLevyShare). `GarGate` = 1ffbd891 (tylko komentarz). Wszyscy czytelnicy puli (`_ff.Cap` / `PoolCap(true)`):
  RecruitPrefix, FfPrisonPrefix, SpawnPrefix, GarGate, WatchRoom (N7 - i tak min z limitem druzyny), `_ffPlayerUnpaid` (gracz-wasal WL: zold 0, dopoki
  armia WL <= 1.05 x pula - przy 158 tys. praktycznie zawsze). Straz bez zmian (inne galezie ff = false). Bez nowych latek Harmony, bez zapisu (pula
  liczona co dobe z ksiegi ludzi), bez watkow. `FfLordGates` - raz na dobe przy linii 166, petla po ok. 23 druzynach, wolanie dwoch funkcji gry
  (HeroHelper) i `PartySizeLimit`; wyjatek liczony w Stumble, linia pisze sie dalej.
- **Martwy kod ozywa**: bramka puli przestaje blokowac 180-330 prob werbunku/d - kazda dochodzi do bramek gry (zloto, limit druzyny, glod), a te,
  ktore przejda, wolaja RecruitVolunteersFromNotable (ksiega ludzi odejmuje rekruta ze wsi). To normalna sciezka werbunku innych krolestw; nie ma tu
  namiotow ani wizerunkow.
- **Armoury.json Jeffa**: klucz NOWY (recenzja sprawdzila: w pliku Jeffa go nie ma; FreeFolkUnpaid i FreeFolkMenFromPeople sa tam true), wiec domyslne
  true zadziala od razu - pulapka MCM (CLAUDE.md 7) nie dotyczy. FreeFolkLevyShare zostaje (0.018) jako wartosc przy kluczu "nie". Zmienione tylko
  podpowiedzi, nie wartosci domyslne.
- Rownolegle galezie r4 (zloto, zlecenia, przetop, wioska) nie dotykaja ClanBudget.cs (recenzja).

## Status
DO SPRAWDZENIA (zbudowane: Armoury kod 0, 0 Error(s); gen_mcm.py 2057 ustawien; gry nie uruchamiano).

## Decyzje
- **Pula = wszyscy dorosli 16-60 (54% ludu), nie polowa (27%)**: Jeff 20.10 doslownie "walcza wszyscy dorosli", zadanie mowi to samo (z kobietami -
  wlocznice). Zapis decyzji 06.10 (REGULY-KRAIN-I-DLUGU 3.2) mial dopisek "pula ok. polowy doroslych" - slowo Jeffa z 20.10 jest nowsze. Ponad 60 lat
  (ok. 7.5% ludu) nie licze - w modelu demografii nie sa sila robocza. Udzial z jednej stalej modelu (2 x `PeopleBook.MenShare`), bez nowego suwaka.
  W grze wynik ten sam co przy 27%: pula i tak nie wiaze.
- **Zalogi bez zmian** (recenzja): regula "zalogi nie wieksze niz druzyny" z wersji 1 cofnieta - nikt o nia nie prosil, a jej powod (39 tys. zalog)
  obala kod gry (pulap zalogi gry). Jesli Jeff zechce, zeby zalogi WL nie rosly ponad druzyny w polu, to osobna decyzja pod wlasnym kluczem.
- **Nie ruszam limitow druzyn, liczby partii, jedzenia, zoldu ani bramki zlota werbunku** - zadanie: dorosli moga walczyc, nic ponad to. Dla Jeffa
  zostaje pytanie: czy Wolni Ludzie (walcza za lud, nie za monete) maja werbowac bez zlota? Bez tego po kilku miesiacach zloto zatrzyma ich wojsko.
- W logu bramki werbunku gry liczone dla druzyn (`FfLordGates`) - to pomiar, nie mechanika; bez niego test nie pokaze, czy wojsko trzyma limit, zloto czy glod.

## Recenzja (PELNA, d478a6ef) - co sprawdzone i co zrobione
1. (wazne) Liczby dla Jeffa w innej mierze niz pytanie - **PRAWDA** (pytanie 4c: 8,5 tys. z linii 4.15, dzis 9022; akapit mowil o mierze puli 5.4 -> 8-9 tys.).
   Poprawione: akapit dla Jeffa i szacunek w mierze 4.15 (9 -> ok. 10-12 tys., ok. 4% ludu) i dlaczego nie wiecej.
2. (wazne) Pominiete zloto - **PRAWDA**. Sprawdzone w dekompilacji: `CheckRecruiting` (RecruitmentCampaignBehavior l.460) i `HeroHelper.StartRecruitingMoneyLimit`
   / `...ForClanLeader` (l.504-520) dokladnie jak w recenzji; w repo zadnej latki (grep 0); log: kiesy rodow WL 1.36 -> 0.81 mln, 4-7 rodow na minusie.
   Poprawione: zloto na liscie hamulcow z liczbami, nowy pomiar w logu (druzyny bez zlota), progi w tescie (kiesy, rody na minusie).
3. (wazne) GarGate od zywego wojska to nowa regula - **PRAWDA**. Sprawdzone w dekompilacji GarrisonRecruitmentCampaignBehavior (l.185/202 PartySizeLimit,
   l.231 FoodChange > 0). Poprawione: GarGate = 1ffbd891, etykiety logu "zalogi ponad udzial puli" zostaja prawdziwe.
4. (drobne) Pula = polowa doroslych, podpowiedz "half" - **PRAWDA**. Poprawione: pula = 2 x MenShare (54%), podpowiedz bez "half".
5. (drobne) Nieaktualne podpowiedzi Free Folk Men From People / Free Folk Levy Share - **PRAWDA**. Poprawione + gen_mcm.py.
6. (drobne) Brak bramki 4.17 w sprawdz_logi, WL-5 moze nie przejsc - **PRAWDA**. Dodana WL-7; o WL-5 - w opisie grupy i w tescie. Etykieta "zalogi ponad
   udzial puli" po cofnieciu zmiany zalog jest znow prawdziwa (nie zmieniam).

## Test w grze
Linie: `Wolni Ludzie - walcza wszyscy dorosli (4.17` (w linii "Budzet rodow (166)"), "Straz i Wolni Ludzie (4.15)" i linia skarbcow ("Free Folk (WOJNA)").
Automat: `python -I tools/sprawdz_logi.py --grupa wyspywolni <log> --start-kampanii <start> --do-doby <N>` (WL-1..WL-7). Progi w biegu 160 dob z zapisu
(d109076: pod bronia 5.4 tys., pula 5.3 tys., 4.15 stan 9.0-10.0 tys.):
- pula 150-165 tys. (54.0% x ludzie), "trzyma dzis: pula (werbunek 0, jency 0, nowe partie 0, zalogi 0)" przez caly bieg (WL-7);
- pod bronia (druzyny + zalogi rodow WL): rosnie z 5.4 tys.; d+60 co najmniej 6.5 tys., d+160 7-9 tys.; druzyny <= ok. 6 tys. ("ludzie / limity druzyn"
  - ludzie nie ponad limity); zalogi <= ok. 3.2 tys.; w linii 4.15 "Wolni Ludzie: stan" na koncu 10-12 tys. (dzis 9.0 tys.);
- druzyny lordow WL (odcinek 4.17): "pelne" rosnie z czasem; "bez zlota na werbunek" - jesli ponad polowe druzyn przez ostatnie 28 dob, wojsko trzyma
  zloto (zapisac w raporcie, nie blad); "w glodzie" najwyzej 3;
- zloto WL (linia skarbcow): "kiesy razem" rodow WL na d+160 nie nizej niz ok. 0.3 mln (dzis 0.81 mln po 160 dobach, start 1.36 mln); rody "na minusie"
  najwyzej 9 z 18 (dzis 4-7); skarbiec WL bez spadku ponizej 0.9 mln (start 0.98, koniec 1.23 mln);
- jedzenie WL3 rosnie (oczekiwane), ale "w glodzie" w 4.15 najwyzej 3 z ok. 23-30 druzyn i bez trwalego wzrostu; zapas zywnosci miast WL bez spadku do 0;
- Nocna Straz: stan nie spada ponizej 7.5 tys. (dzis 8423); zgineli Strazy w 160 dob najwyzej ok. 2x dzisiejszych 274;
- oblezenia Muru: zadna osada Strazy nie zmienia wlasciciela (Castle Black, Eastwatch, Shadow Tower); WallSiege w 160 dob nie oczekiwany; FABULA
  w CrashScribe bez nowych bledow;
- Inni: WL-5 trupy w polu do ok. 1.5 x baza (wiecej poleglych WL) - ponad to sprawdzic, skad ciala;
- Wolni Ludzie ludnosc (293 tys.) nie spada szybciej niz ok. 2% w 160 dob; polegli WL w 4.15 do ok. 2x dzisiejszych 1012;
- brak potkniec "GarRecruitPrefix" / "FfLordGates" / "FreeFolkPool" w logu.

**Dla Jeffa:** Zrobilem "walcza wszyscy dorosli": Wolni Ludzie moga teraz wziac do walki kazdego doroslego - mezczyzn i wlocznice - a nie tylko 1,8%
ludu jak dotad. Tylu jednak nie bedzie: wojsko trzymaja wielkosc druzyn wodzow, zloto wodza na werbunek, jedzenie i straty, wiec z ok. 9 tysiecy
pod bronia (te 8,5 tys. z pytania) zrobi sie ok. 10-12 tysiecy, czyli ok. 4% ludu. Po kilku miesiacach zacznie ich trzymac zloto, bo kiesy rodow
Wolnych Ludzi juz dzis topnieja - jesli chcesz, zeby werbowali bez zlota (walcza za lud, nie za monete) albo zeby wodzowie mieli wieksze druzyny,
powiedz, to osobna zmiana. Na Murze bedzie goreco: wiecej bitew ze Straza i z Innymi, a pod wielki szturm Mance'a przyjdzie ok. poltora raza wiecej
dzikich. Wylacznik: "Free Folk All Adults Fight".
