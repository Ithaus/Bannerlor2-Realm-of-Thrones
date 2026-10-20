# r4-wolni - Wolni Ludzie: walcza wszyscy dorosli (PLAN 4.17), galaz w-toku/r4-wolni na 1ffbd891 (WGRANIE 91)

## Problem (dowod)
Logi testow na WGRANIU 91 (`Modules/Armoury/Armoury-2026-10-20_00-44-29.log` - 160 dob z zapisu, d109076-d109236;
`Armoury-2026-10-20_00-17-30.log` - nowa kampania 120 dob):
- linia "Budzet rodow (166)", odcinek "Wolni Ludzie bez zoldu (WL1)": pula **5276-5294** = 1.8% x ludzie 293-294 tys. (WL2, FreeFolkLevyShare 0.018);
  w ludziach (druzyny + zalogi rodow WL) **5326-5422** - przez cale 160 dob na pulapie. Werbunek wstrzymany przez pule **180-330 prob na dobe**,
  przyrost zalog wstrzymany przez pule 4-6/d, nowe partie wstrzymane 1/d; WL3 (jedzenie) wstrzymuje 0-35/d.
- nowa kampania: pula 5394 od pierwszej doby; wojsko rosnie 2648 -> 5352 i od ok. 40. doby stoi na puli (wstrzymane 150-270/d).
- "Straz i Wolni Ludzie (4.15)" d109235: Wolni Ludzie stan 9022 (partie 6933, zalogi 2089; liczone z bohaterami, rannymi, taborami wsi i
  karawanami), od startu sesji przybylo 3131, zginelo 1012; druzyny WL 23 (w glodzie 1, jedzenie ponizej progu najazdu 3).
- "Krainy (4.15)": freefolk ludzie 293517, mezczyzni 79250 (27%), prog wolnych rak 100% (E4/7 - juz "walcza wszyscy" w ksiedze rak).
- Jeff 20.10 (STAN-PRAC, decyzje do starych pytan): "3. walcza wszyscy dorosli". Decyzja 06.10 (REGULY-KRAIN-I-DLUGU rozdz. 3.2):
  "walcza wszyscy dorosli (pula ok. polowy doroslych)" = 27% ludnosci = ok. 81 tys. przy 300 tys.

## Przyczyna
WL2 (`ClanBudget.FfPoolCap`) liczy pule jako `FreeFolkLevyShare x ludzie regionow WL` = 1.8% - to byl tymczasowy pulap "tyle, ile mieli w
pierwszym polroczu", nie decyzja "walcza wszyscy dorosli". Prog wolnych rak WL (100%) jest juz zrobiony; zostala sama pula wojska.

## Zmiana (Armoury, nowy klucz `FreeFolkAllAdultsFight` "Free Folk All Adults Fight", domyslnie TAK, grupa MCM "Reavers and the Free Folk",
zaraz pod Free Folk Levy Share)
1. `ClanBudget.FfPoolShare`: przy kluczu pula WL = `PeopleBook.MenShare` (0.27 - jedna stala modelu demografii: mezczyzni 16-60, czyli polowa
   doroslych 16-60 mezczyzn i wlocznic) x ludzie regionow WL; dzis ok. **79 tys.** zamiast 5.3 tys. Bez klucza - FreeFolkLevyShare jak dotad.
   Pula dziala jak dotad wszedzie, gdzie ja czytano (werbunek od notabli, jency AI, nowe partie, zalogi, N7 zbiorka rozbitych, zold gracza-wasala WL) -
   nowych mechanik nie ma.
2. `ClanBudget.GarGate`: zalogi WL rosna do FreeFolkGarrisonPoolShare (0.50) **zywego wojska WL** (`min(pula, pod bronia)`), nie puli - przy puli
   79 tys. stary wzor pozwalalby zalogom rosnac do 39 tys. i zamknac wojsko w murach (zasada [L] "walcza w polu, nie w murach"; tak mowi tez opis
   klucza Free Folk Garrison Pool Share "of the host"). Przy 0.50 = zalogi nie wieksze niz druzyny w polu. Bez klucza - od puli jak dotad.
3. Log: w linii "Budzet rodow (166)" odcinek WL pokazuje prawdziwy udzial (27.0% x ludzie), "rosna do 50% wojska WL - 4.17" i nowy odcinek
   **"Wolni Ludzie - walcza wszyscy dorosli (4.17, Free Folk All Adults Fight tak): ludzie N, dorosli 16-60 ok. N (mezczyzni i wlocznice, 2 x 27%
   ludzi), pula N (polowa doroslych), pod bronia N (druzyny N, zalogi N) = X% puli; trzyma dzis: pula (werbunek N, jency N, nowe partie N,
   zalogi N), jedzenie WL3 N, zalogi ponad 50% wojska N; reszte trzymaja limity druzyn gry, liczba wodzow, jedzenie i straty"**.

## Co naprawde trzyma wojsko WL po zmianie (kod + log + ROZPOZNANIE-WOLNI rozdz. 3-4.2)
- **Limit druzyn gry** (PartySizeLimit, BK): u rodow bez zoldu limit w zlocie = max (`ApplyPartyLimits`: `if (b.Zero) lim = max`), wiec wiaze
  sam limit druzyny. ROZPOZNANIE-WOLNI (pomiar 2 lat): limity druzyn WL **5.2-5.8 tys.** w 7 rodach z ziemia; dzis w druzynach ok. 3.3 tys.
- **Liczba wodzow / partii**: 23 druzyny WL; nowe partie wystawia gra (ConsiderSpawningLordParties - wolni bohaterowie rodu, limit dowodcow
  wedlug tieru); nasza bramka puli przestaje je wstrzymywac (dzis 1/d). Rody "gentry" BK (Wightsbane, Frosttooth, Crowsbane) bez druzyn - bez zmian.
- **Jedzenie**: WL3 (FreeFolkRecruitNeedsFood) - druzyna werbuje tylko z jedzeniem na NeededFoodsInDaysThresholdForRaid dni z nowymi ludzmi;
  AI kupuje jedzenie tylko za zloto wodza. Dzis wstrzymuje 0-35/d (nowa kampania do 94/d) - po zmianie stanie sie glownym hamulcem druzyn biednych.
- **Zalogi**: cel zalogi gry (jedzenie i zold - `CalculateSettlementGarrisonPartySizeLimitWithFoodAndWage`), przy K1 tylko z werbunku od notabli
  z ludzi regionu (4.7), i nowy pulap "nie wiecej niz druzyny w polu". ROZPOZNANIE: zalogi rosly do 3.0 tys. pulapu gry.
- **Straty i morale**: zabici 11-25/d (glownie Straz i Inni), w 160 dobach zginelo 1012; dezercja z morale (glod, porazki) bez zmian.
- **Zold**: brak - WL1 (zold 0, zostaje minimum BK ok. 1 zl/czl w druzynie, 0.5 w zalodze: dzis 3585 + 1027 zl/d); skarbiec WL 1.23 mln,
  kiesy rodow 0.81 mln, zalegly zold 0 przez 160 dob - minimum BK dla 5-6 tys. w druzynach (ok. 6 tys. zl/d) to udzwigna dlugo.
- **Ludzie**: ksiega ludzi (pobor ze wsi werbunku, 4.8) - 293 tys. ludzi i prog wolnych rak 100%: pobor kilku tysiecy wiecej to ok. 1% ludu.

**Ile wojska realnie wyjdzie (SZACUNEK)**: druzyny 3.3 -> ok. 5-5.8 tys. (limity druzyn), zalogi 2.1 -> do ok. 3 tys. (cel gry, nie wiecej niz
druzyny), razem w ludziach puli ok. **8-9 tys.** (dzis 5.4 tys.; ROZPOZNANIE 4.2 liczylo "rzad 8-9 tys." bez pulapu w ludziach); w linii 4.15
(z bohaterami, rannymi, taborami wsi i karawanami) ok. 11-12 tys. zamiast 9 tys. To ok. 11% puli i 3% ludu. Przyrost rozlozony na tygodnie:
werbunek od notabli (ochotnicy odnawiaja sie z czasem) i WL3.

## Ryzyko / co sprawdzic (kontrola calosci)
- **Mur i Nocna Straz**: Straz 8423 (partie 6682) wygrywa prawie wszystkie starcia z WL (ROZPOZNANIE: 393/407 bitew w roku 1), WL w polu nie
  atakuja druzyn Strazy. Wiecej WL w polu = wiecej celow dla Strazy i Innych: wiecej poleglych WL (dzis 1012 w 160 dob), wiecej strat Strazy
  (dzis 274), wiecej rabunkow Daru (WW1/WW3 dla WL - klucz ReaverRaidByProfitFreeFolk jest pierwszym krokiem wycofania).
- **Fabula ROT WallSiege** (WallSiegeEvent - os Fabuli 267 x FabulaTimeScale, AUDYT-KALENDARZ ok. 1157 dob): port CrashScribe `WallSetupWith`
  zbiera do armii oblezenia WSZYSTKIE druzyny lordow WL - armia pod Murem bedzie ok. 1.5-1.7 x wieksza (5-5.8 tys. zamiast 3.3 tys.). W biegu
  160 dob WallSiege nie wypada; pierwszy prawdziwy sprawdzian w kampanii Jeffa.
- **Nocny Krol / Inni**: OthersRise (4.18) - ciala tylko z poleglych za Murem i przy Murze, gdy wygrywa banda Innych; wiecej WL w polu = wiecej
  cial dla Innych (rosna szybciej, ale tylko z prawdziwych poleglych - zamknieta ksiega). Pomiar: "UMARLI" (CrashScribe) i E4/2.
- **Glod za Murem**: druzyny - WL3 trzyma werbunek przy braku jedzenia, ale kupuja jedzenie z miast WL; wieksze druzyny = wiekszy popyt na
  zywnosc w kilku miastach WL. Zalogi - cel gry liczy jedzenie. Sprawdzic "w glodzie" w linii 4.15 i zapas zywnosci miast WL.
- Regresja: bez klucza kod = 1ffbd891 (FfLevyShare, zalogi od puli). Wszyscy czytelnicy puli (`_ff.Cap` / `PoolCap(true)`): RecruitPrefix,
  FfPrisonPrefix, SpawnPrefix, GarGate, WatchRoom (N7 - i tak min z limitem druzyny), `_ffPlayerUnpaid` (gracz-wasal WL: zold 0, dopoki armia
  WL <= 1.05 x pula - przy 79 tys. praktycznie zawsze). Straz bez zmian (inne galezie ff = false). Bez nowych latek Harmony, bez zapisu
  (pula liczona co dobe z ksiegi ludzi), bez alokacji w goracej sciezce (GarGate liczy PoolLiveMen raz, jak dotad - raz na zaloge na dobe).
- Martwy kod ozywa: bramka puli przestaje blokowac 180-330 prob werbunku/d - kazda z nich to RecruitVolunteersFromNotable (ksiega ludzi
  odejmuje rekruta ze wsi). To normalna sciezka werbunku innych krolestw; nie ma tu namiotow ani wizerunkow.
- Armoury.json Jeffa: klucz NOWY - pulapka MCM (CLAUDE.md 7) nie dotyczy; FreeFolkLevyShare zostaje (0.018) jako wartosc przy kluczu "nie".

## Status
DO SPRAWDZENIA (zbudowane: Armoury kod 0, 0 Error(s); gry nie uruchamiano).

## Decyzje
- Pula = polowa doroslych (27% ludu), nie wszyscy dorosli (55%): tak brzmi zapis decyzji Jeffa 06.10 ("pula ok. polowy doroslych", 81 tys.
  przy 300 tys.) - w jednej chwili walczy polowa, druga poluje i pasie (REGULY 3.2: plon WL nie spada od mobilizacji). Udzial wziety z jednej
  stalej modelu demografii (`PeopleBook.MenShare`), bez nowego suwaka. Przy 55% pula bylaby 160 tys. - wynik w grze ten sam, bo pula i tak nie wiaze.
- Zalogi od zywego wojska zamiast od puli - inaczej "walcza wszyscy" zamieniloby sie w "siedza w murach" (39 tys. miejsca w zalogach).
- Nie ruszam limitow druzyn, liczby partii, jedzenia ani zoldu - zadanie: dorosli moga walczyc, nic ponad to.

## Test w grze
Linie: `Wolni Ludzie - walcza wszyscy dorosli (4.17` (w linii "Budzet rodow (166)") i "Straz i Wolni Ludzie (4.15)". Progi w biegu 160 dob
z zapisu (d109076 = 5.4 tys. pod bronia):
- pula 75-82 tys. (27% x ludzie), "trzyma dzis: pula (werbunek ...)" = 0 przez caly bieg;
- pod bronia (druzyny + zalogi rodow WL): rosnie z 5.4 tys.; d+60 co najmniej 6.5 tys., d+160 7-10 tys.; druzyny <= ok. 6 tys.,
  zalogi <= druzyny (licznik "zalogi ponad 50% wojska" > 0 dopiero, gdy zalogi dogonia druzyny);
- jedzenie WL3 rosnie (oczekiwane), ale "w glodzie" w 4.15 najwyzej 3 z ok. 23-30 druzyn i bez trwalego wzrostu;
- Nocna Straz: stan nie spada ponizej 7.5 tys. (dzis 8.4 tys.); zgineli Strazy w 160 dob najwyzej ok. 2x dzisiejszych 274;
- oblezenia Muru: zadna osada Strazy nie zmienia wlasciciela (Castle Black, Eastwatch, Shadow Tower); WallSiege w 160 dob nie oczekiwany;
- Wolni Ludzie ludnosc (293 tys.) nie spada szybciej niz ok. 2% w 160 dob; polegli WL w 4.15 do ok. 2x dzisiejszych 1012;
- brak potkniec "GarRecruitPrefix" / "FreeFolkPool" w logu.

**Dla Jeffa:** Wolni Ludzie moga teraz wystawic do walki polowe wszystkich swoich doroslych - mezczyzn i wlocznice - czyli ok. 80 tysiecy, a nie
5 tysiecy jak dotad. W praktyce tylu nie bedzie: wojsko trzymaja wielkosc druzyn wodzow, jedzenie i straty, wiec realnie urosnie z ok. 5.4 do
ok. 8-9 tysiecy. Zeby nie siedzieli w murach, zalogi rosna tylko do wielkosci druzyn w polu. Na Murze bedzie goreco: wiecej dzikich w polu to
wiecej bitew ze Straza i z Innymi, a gdy przyjdzie wielki szturm Mance'a, pod Mur przyjdzie ok. poltora raza wiecej ludzi. Wylacznik: "Free
Folk All Adults Fight".
