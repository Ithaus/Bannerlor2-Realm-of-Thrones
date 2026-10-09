# PACZKA 174 - PRODUKCJA UZBROJENIA (specyfikacja dla wykonawcy, 09.10.2026) - WERSJA 2 PO KRYTYCE

Status: SAM PROJEKT. Nic nie zbudowane, nic nie wgrane, nic nie zacommitowane, gra nie uruchamiana. Jedyny pisany plik - ten.
Wersja 2: wprowadzone 36 uwag krytykow (35 prawdziwych, 1 czesciowo) - kazda sprawdzona w kodzie albo w logu; lista i odpowiedzi w rozdz. 11.
Wersja 1 (przed krytyka) lezy w `SCR\p174\PROJEKT-174-v1-przed-krytyka.md`.
Baza kodu: galaz `w-toku/172-strzaly` (commit 77ee649 = 171 + 172 + poprawki po recenzjach) + 172b w toku (karawany nie handluja
amunicja), drzewo `SCR\noc\n172` (SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`).
Sciezki `[K]` wzgledem `SCR\noc\n172\Armoury\src`; gra i BK - dekompilacje `SCR3\ore-supply\cs` i `\bk`; DTE - `SCR\dte\DynamicTroopEquipmentReupload`
(SCR3 = `...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`).

Oznaczenia: **[K]** przeczytane w kodzie, **[P]** pomiar z logu, **[H]** historia ze zrodlem (adresy w rozdz. 10), **[H?]** liczba
z drugiej reki albo kanon bez strony, **[S]** szacunek albo rachunek (podane zalozenia), **[S z H]** szacunek zbudowany z danych historycznych.
Rok gry = 364 dni. Ladunek rudy/drewna = 100 kg; skora, plotno, len = sztuka 10 kg.

Podstawa (przeczytane w calosci): `SCR\p174\bilans.md`, `SCR\p174\lancuch.md`, `SCR\p174\historia.md`, spec. 171 i 172,
`docs/audyt-2026-10-09/01-dochody-ludnosci.md` (L8), `03-surowce-naprawy.md` (1.5-1.6, 2.3, P1-D), `09-historyk-liczby.md` (P-8),
`docs/AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md` (W7), `docs/PLAN-K13-2026-10-07.md` (132, 133), `docs/STAN-PRAC.md`,
`docs/HISTORIA-WARSZTATY.md`. Pomiar glowny: autotest 171+172, 40 dob nowej kampanii (`SCR\kopia171\Armoury-2026-10-08_20-47-10.log`
+ `SCR\kopia171\2026-10-08_20-47-10\*`, doby 108836-108875) = **A171**; porownanie: 5 biegow bez 171 (`SCR\kopia169`, `SCR\s1kopia`) i rok TOWARY 3.
Rachunki tej wersji (powtarzalne, tylko odczyt logu): `SCR\p174\skrypty\rozbicie.py` (przyrost braku: nowi ludzie / pogorszenie),
`reszta.py` (reszta polek w dziesiatkach dob), `odplyw.py` (odplyw
z polek wedlug typu), `towary.py` (ksiega Towary, srednie okien), `rece.py` i `rece2.py` (rece wedlug krolestw, stary i nowy wzor).
Wykonawca 174.0 przenosi `reszta.py` do repo jako `tools/p174_reszta.py` (w tym samym commicie co linia "Uzbrojenie (ujscia 174)").

Decyzje Jeffa (wiazace):
- 09.10 (dla tej paczki): "jak czego brakuje, to sprawdz, czy produkcja jest odpowiednia, czy za malo nie ma rzemieslnikow, jesli nie ma
  jak uzbrajac wojska - ale nie ma mowy, aby dostawali sprzet za darmo z niczego".
- 08.10: "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko z czegos wynika".
- 05.10: wojsko ok. 108 tys., gospodarke dociagamy do niego; produkcja wsi stopniami x1.3 / x1.6 / x1.9 razem z popytem; ruda - wariant B
  "ruda razem z rekami"; **karawany rozwoza surowce masowe (ruda, drewno, skory, len, welna) tam, gdzie brakuje - prawdziwe partie na mapie,
  nie niewidzialny przerzut** (STAN-PRAC.md:332-333); **werbunek gracza: rekrut od tieru 2 tylko z tym, co kupil mu notabl (jak AI) -
  koniec darmowego kompletu** (STAN-PRAC.md:354 i :448).
- 09.10 (okupy): "jedna regula dla gracza i AI".
- Parametry dobiera projekt z uzasadnieniem historycznym (bez pytan o liczby); mechanika obejmuje 100% swiata; pytania tylko o zmiany rozgrywki.
- Wpis 92 (przyjety): rekrut tieru 1 przychodzi z rzeczami z domu. 171: rekrut wyzej - "z tym, co ma"; reszte kupuje pan. 171 B5 (domyslne
  171 - "TAK" 09.10): dorobek startowy nowej kampanii (ColdStart).

---

## 0. Dla Jeffa - prostym jezykiem

1. **Sprawdzilem, czy produkcja jest odpowiednia. W sumie prawie tak, w skladzie - nie.** W pierwszych 40 dniach nowej kampanii armie AI
   rosly z 86 do 160 tys. ludzi i nadal rosly pod koniec testu (o ok. 1 100 ludzi dziennie). Wiekszosc "potrzeby" (ok. 2 tys. sztuk
   dziennie) to nowi ludzie, a nie zuzycie. Gdyby armie nie rosly, potrzebowalyby ok. 0,8-1,0 tys. sztuk dziennie - tyle mniej wiecej,
   ile robia wszystkie kuznie (1,1 tys.). Ale kuznie robia nie to, co trzeba: zbroi na tulow 51 dziennie wobec potrzeby do ok. 360,
   helmow 140 wobec ok. 345, tarcz 60 wobec ok. 255, rekawic 50 wobec ok. 190 - za to za duzo butow i broni jednorecznej w zlych tierach.
2. **Co znaczy "50% zbroi" w logu.** To nie "co drugi zolnierz bez zbroi", tylko: w polowie miejsc na zbroje brakuje sztuki tieru zolnierza
   (albo o jeden nizej). Wielu ma starsza, gorsza sztuke - gra jej dzis nie liczy, a w bitwie z Toba DTE ja zaklada. Dokladam druga miare
   ("cokolwiek na tulow"), zanim porownamy z historia.
3. **Dlaczego zly sklad (od najwazniejszego):**
   - **Za malo rzemieslnikow.** Ok. 3 tys. "rak" dziennie (z kowalami strzal ok. 3,7 tys.) na swiat 52 mln ludzi - w sredniowieczu
     5-9 razy wiecej. I liczymy je od bogactwa miasta, nie od ludzi: Qarth (1,6 mln ludzi rynku) ma tylu platnerzy, co male miasteczko.
   - **Za malo rudy i lezy nie tam, gdzie trzeba.** Na bron idzie 5-30 razy mniej zelaza niz w sredniowieczu. 38 miast nie ma rudy wcale,
     30 ma jej za duzo - i nikt jej nie przewozi tam, gdzie kowale stoja.
   - **Kowal bez rudy siedzi z zalozonymi rekami**, zamiast pomagac tarczownikom albo lucznikom.
   - **Warsztat robi to, co szybkie, a nie to, czego brakuje.** Krawcy szyja codziennie 110-150 par damskich pantofli (nie nosi ich zaden oddzial).
   - **Codziennie ok. 0,7-1,0 tys. sztuk znika z targow bez sladu** (mieszczanie "zjadaja" bron i zbroje, gra kasuje co dzien czesc sztuk,
     Banner Kings zjada bron i tarcze w taborach AI).
4. **Co robie - piec krokow, kazdy osobno z testem, nic z niczego:**
   - **174.0 Koniec znikania.** Bron i zbroja nie gnija na straganie i nie sa "jedzeniem" mieszczan; BK przestaje zjadac bron i tarcze armii
     AI. Sklad nie jest za darmo: kowale miasta poswiecaja troche czasu na czyszczenie i oliwienie zapasu (jak w Tower). Do tego nowa miara
     "cokolwiek na tulow".
   - **174.0b Robota w toku przetrwa zapis.** Dzis kazde wczytanie gry kasuje zaczete zbroje razem z kupiona juz ruda.
   - **174.1 Rece i wybor.** Rzemieslnik bez surowca idzie tam, gdzie brakuje i gdzie jest material. Warsztat najpierw robi to, czego w miescie
     brakuje najbardziej; czesc zbroi t1-t3 "na amunicje" (szybsza, prostsza sztuka - jak Paryz 1384). Damskich pantofli kuznie juz nie szyja.
   - **174.2 Zamowienia surowca jezdza prawdziwa karawana.** Miasto, w ktorym kowale stoja bez rudy (albo tkacze bez lnu, garbarze bez
     skor), zamawia ja w pobliskim miescie z nadwyzka; jedzie prawdziwa karawana na mapie - bandyci moga ja rozbic. Ruda tylko z bliska
     (2-3 dni drogi) - historycznie daleko wozono sztaby, nie rude.
   - **174.3 Rzemieslnicy od liczby ludzi.** Rece miasta od ludnosci jego rynku z tabeli krain (stala liczba - nie rosnie z bledu tabeli
     ludzi) i od bogactwa; zadne miasto nie traci rzemieslnikow wobec dzis. Swiat ok. 2 razy wiecej rak; razem z tym kopalnie x1,5 i lasy
     wsi x1,6 (Twoja zasada "stopniami razem z popytem" - wegiel dla kuzni to nowy odbiorca drewna); BK przestaje kasowac zapasy rudy,
     skory i plotna. Dorobek startowy nowej kampanii NIE rosnie razem z rekami.
5. **Czego sie spodziewac** (szacunek, test pokaze): produkcja z ok. 1,1 tys. do ok. 1,7-2,2 tys. sztuk dziennie; zbroje na tulow
   51 -> 120-190 dziennie, helmy 140 -> 200-230, tarcze 60 -> 150-240, bron ok. 280 -> 480-550, strzaly 150 -> 280-360 snopow.
   W nowej kampanii zbroja na tulow skonczy sie na targach po ok. 1-4 miesiacach (dzis 48 dni); potem kuznie robia, a wojsko od razu kupuje.
   Przy armii, ktora juz nie rosnie, kuznie pokryja ok. 35-55% potrzeby zbroi na tulow (przy 108 tys. wojska 50-80%), helmy 60-65% (85-100%),
   tarcze 60-95% (85-100%), bron i strzaly w calosci.
6. **Czego to nie zalatwi:** wzrostu armii. Dopoki armie rosna (w tescie 160 tys. w 40. dniu, Twoja decyzja to ok. 108 tys.), kazdy nowy
   czlowiek przychodzi z rzeczami z domu (tier 1), a reszte trzeba kupic - to ok. 2 tys. sztuk dziennie, ktorych zadna produkcja nie da.
   Tak bylo w sredniowieczu: nowa armie zbrojono z domow i zbrojowni latami. Liczbe wojska zapisuje jako osobna sprawe (rozdz. 9).
7. **Sprzet, ktory dzis nadal przychodzi bez materialu** (zebys widzial calosc): dorobek startowy nowej kampanii - na targach 44 tys. sztuk
   i w zbrojowniach 345 tys. (raz), komplety 1 476 ochotnikow (raz); rzeczy z domu rekrutow tieru 1 (ok. 4,6 tys. sztuk dziennie);
   pelny komplet DTE dla rekrutow GRACZA (Twoja decyzja 05.10 to zamyka - paczka 175); w Twojej kampanii ok. 1 mln sztuk na targach,
   w ok. 2/3 z dawnych darmowych kompletow. 174 niczego z tej listy nie powieksza. Pytania 4 i 6.
8. **Szesc pytan** (rozdz. 8): (1) kto zyskuje rzemieslnikow (tabela krolestw); (2) czy AI bez broni ma kupic jakakolwiek bron, a bez
   zbroi - gorsza zbroje; (3) zbroja "na amunicje" w sklepach; (4) Twoj stos 1 mln: zostawic czy powoli na zlom dla kowali; (5) gracz
   jedna regula z AI (zaopatrzenie BK); (6) czy dorobek startowy i rzeczy z domu rekruta zostaja.

---

## 1. Bilans - dziura w kazdym koszyku (liczby)

### 1.1 Koszyk po koszyku - bieg A171 (bilans.md rozdz. 2; "popyt" = zeszlo z polek netto + przyrost braku w zbrojowniach AI)

| Koszyk | Polki d1 -> d40 | Produkcja/d (d31-40) | Popyt/d 40 dob (d31-40) | Dziura % popytu 40 dob (d31-40) | Glowne zrodlo liczby |
|---|---|---|---|---|---|
| Zbroja korpusu | 13 954 -> 1 137, zero ok. d48 | 24 (51) | ok. 1 490 (745) | **98% (93%)** | brak w zbrojowniach 18 415 -> 58 134 [P] |
| Helm | 2 773 -> 187 ("dno" od d20) | 139 (140) | ok. 1 290 (625) | **89% (78%)** | [P] produkcja, [S] brak ze wzorcow |
| Nogi | 1 605 -> 257 | 250 (237) | ok. 630 (40) | 60% (brak dziury) | ok. 60% to damskie pantofle [P] |
| Rece (rekawice) | 662 -> 101 | 68 (49) | ok. 510 (295) | **87% (83%)** | [S] |
| Tarcza | 1 538 -> 218 ("dno" od d23) | 51 (59) | ok. 895 (500) | **94% (88%)** | [S] |
| Bron razem (1H, 2H, drzewce, miotana, luk, kusza) | 17 396 -> 11 366 | 240 (276) | ok. 1 590 (710) | **85% (61%)** | brak 25 390 -> 67 301 [P] |
| Strzaly (snopy) | 1 563 -> 261; w karawanach 698 -> 4 157 | 106 (153) | ok. 560 (270) | 81% (43%) | [P]/[S] |
| Belty (snopy) | 1 002 -> 402; w karawanach 378 -> 1 764 | 48 (39) | ok. 110 (55) | 56% (28%) | [P]/[S] |
| Plaszcz, uprzaz | 6 478 -> 2 539 | 71 (61) | >= 170 (120) | ok. 55% | lordowie uprzezy nie kupuja [K] |
| Konie | 2 213 -> 9 826 | 0 (wsie +190/d) | - | brak dziury | [P] |
| **RAZEM (bez koni)** | 46 971 -> 16 468 | **ok. 1 000 (1 065)** (warsztaty 843 + strzelarze 154) | **ok. 7 240 (3 360)** | **86% (68%)** | brak razem 105 541 -> 299 462 [P] |

Liczby z linii "Warsztaty: dzien", "Strzelarze (172)", "Rynek broni: na polkach", "Pokrycie zbrojowni AI (171)" [P]; podzial braku na helm,
tarcze, nogi, rece, strzaly i belty - ze wzorcow ROT (`ROT-Content/ModuleData/ROT-Troops.xml`), blad sumy < 5% (bilans.md rozdz. 1) [S].
**Uwaga:** kolumna d31-40 to NIE stan "gdy armie przestaly rosnac" (tak pisala wersja 1 - blad). W d31-40 armie AI urosly o 11 065 ludzi
(+7,4%) - rozbicie w 1.1a.

### 1.1a Rozbicie popytu d31-40: nowi ludzie, pogorszenie, znikanie [P]/[S]

Ludzie AI (linia "Pokrycie zbrojowni AI (171)", doby 108865 i 108875): partie rodow 94 182 -> 98 207, zalogi 55 241 -> 62 281 (+12,7%);
razem **149 423 -> 160 488 (+11 065, ok. +1,1 tys./d)**. Brak na czlowieka: 1,847 -> 1,866 szt. Nowy czlowiek przychodzi z dobytkiem t1
(wpis 92), wiec do braku dochodzi mu ok. tyle, ile brakuje przecietnemu zolnierzowi. Rachunek: `SCR\p174\skrypty\rozbicie.py`.

| Strumien d31-40 (na dobe) | Razem | Korpus | Bron | Jak liczone |
|---|---|---|---|---|
| Przyrost braku w zbrojowniach | 2 353 | 551 | 507 | linia "Pokrycie" [P] |
| - z tego **nowi ludzie** (przyrost ludzi x brak na czlowieka w d30) | **ok. 2 043 (87%)** | ok. 390 (71%) | ok. 461 | [S] |
| - z tego **pogorszenie pokrycia** obecnych ludzi | **ok. 309** | ok. 161 | ok. 46 | reszta [S] |
| Zeszlo z polek netto | 1 213 | 195 | 201 | bilans.md 2.2 [P] |
| - z tego **znikanie bez sladu** (reszta polek d31-40, rozdz. 1.3) | **ok. 670-690** | ? | ? | `reszta.py` [P] |
| - z tego **zakupy wojska netto** | **ok. 520-540** | <= 195 | <= 201 | [S] |
| **Popyt bez wzrostu armii** (zakupy netto + pogorszenie) | **ok. 0,8-1,0 tys./d** | **<= ok. 356** | <= ok. 250 | [S] |
| **Strumien wzrostu armii** (nowi ludzie) | **ok. 2,0 tys./d** | ok. 390 | ok. 460 | [S] |

Popyt bez wzrostu wedlug koszyka (gorna granica = zeszlo z polek netto + pogorszenie; pogorszenie helmu, tarczy, rekawic i nog liczone
z procentow linii "Pokrycie" x udzialy wzorcow ROT, blad zaokraglen procentu ok. +-100/d) [S]:

| Koszyk | Zeszlo netto/d | Pogorszenie/d | Popyt bez wzrostu (160 tys.) | Przy 108 tys. (x0,675) | Produkcja A171 d31-40 |
|---|---|---|---|---|---|
| Korpus | 195 | 161 | **<= 356** | <= 240 | 51 |
| Helm | 144 | ok. 200 | **<= 345** | <= 233 | 140 |
| Rekawice | 48 | ok. 140 | **<= 190** | <= 128 | 49 |
| Tarcza | 60 | ok. 195 | **<= 255** | <= 172 | 59 |
| Bron razem | 201 | 46 | <= 250 | <= 169 | 276 |
| Nogi | 258 | ok. -360 (pokrycie rosnie) | ok. 0 | ok. 0 | 237 |
| Strzaly / belty | 151 / 38 | ok. 15 / 2 | <= 165 / 40 | <= 111 / 27 | 153 / 39 |
| Plaszcz, uprzaz | 118 | - | <= 118 (glownie znikanie) | - | 61 |

Wniosek: **w sumie produkcja (1 065/d) jest blisko popytu armii, ktora nie rosnie (0,8-1,0 tys./d); dziura to zly sklad** (korpus 7x za
malo, helm i rekawice 2,5-4x, tarcze 4x; buty i bron 1H w nadmiarze albo w zlych tierach) **i wzrost armii** (ok. 2 tys./d). Popyt bez
wzrostu jest przy tym zanizony przez hamulce awansow: notable cofaja ok. 900 awansow dziennie z braku towaru, druzyny cwicza x0,6-0,7 [P]
- prawdziwa potrzeba jest wyzsza, ale nie umiem jej zmierzyc bez sprzetu na polkach.

### 1.2 Pokrycie zbrojowni AI - co mierzy linia "Pokrycie zbrojowni AI (171)"

**Miara gry to pokrycie SZCZEBLA, nie odsetek ludzi z czymkolwiek** [K]: `AiGear.Deficit` (AiGear.cs:191-235) liczy sloty wzorca wedlug
(typ, tier) i uznaje za pokrycie tylko sztuke tego samego typu o tierze >= t-1 (zastepstwo: wyzsze, potem t-1); `ArmsDrill` (:114-119,
:337-359) tak samo ("glowna bron (wedlug szczebla)"). Sztuka t1 w zbrojowni zolnierza t3 zostaje (`MenPurse.SellAiSurplus` liczy nadwyzke
wedlug TYPU, MenPurse.cs:322-331), ale miara uznaje ja za brak. W bitwie z graczem DTE zaklada sztuke o tierze <= t+2 bez dolnej granicy
(PartyEquipmentDistributor.cs:1300-1320, :1769-1778), a kara "Underequipped" to -2 morale za kazdy brakujacy tier, najwyzej -20 (:78, :1904-1936).

| Pokrycie szczebla (miara gry) | Partie rodow AI d5 -> d40 [P] | Zalogi AI d5 -> d40 [P] |
|---|---|---|
| Zbroja korpusu | 62% -> 50% | 93% -> 84% |
| Zbroja korpusu t3+ | 48% -> 27% | 92% -> 77% |
| Helm | 50% -> 32% | 90% -> 73% |
| Tarcza | 46% -> 23% | 69% -> 52% |
| Bron glowna (wedlug szczebla) | 71% -> 72% | 96% -> 92% |
| Rekawice | 49% -> 33% | 88% -> 71% |
| Strzaly / belty | 54% / 35% -> 43% / 30% | 97% / 86% -> 86% / 73% |

Historia (armia w polu, ODSETEK LUDZI z czymkolwiek) [S z H]: ochrona tulowia jakakolwiek 75-95% (pikowana 35-55%), metal 25-45%, helm
60-90%, tarcza 20-60% wedlug kultury, bron prawie 100% (Winchester 1285; HISTORIA-ZBROJE-STRATY 3.2, historia.md 5.4: 1322 - 300 aketonow
na 500 ludzi; 1324 - 25% w pelnym zestawie; Florencja 1429-33).
**Tych dwoch kolumn nie wolno porownywac wprost** (wersja 1 to robila - blad). Porownanie z historia dopiero na nowej mierze "dowolny
szczebel" (174.0, rozdz. 3.1 e). Wniosek, ktory stoi bez tej miary: braki szczebla sa najwieksze w helmach, tarczach i rekawicach
(rzeczach tanich), a bron glowna 72% przy 6 293 sztukach 1H i 3 191 2H na polkach w d40 [P] znaczy, ze bron jest, tylko w zlych tierach
i kulturach (AiGear.BuyLoop kupuje tylko ten sam typ i tier t albo t-1, AiGear.cs:262-270) - pytanie 2.

### 1.3 Skad dzis przychodzi sprzet i gdzie znika (na dobe, A171) [P]/[S]

| Strumien | Szt./d | Z czego | Status |
|---|---|---|---|
| Warsztaty zbrojne (WorkshopLaw) | 843 (d31-40 ok. 870) | ruda, drewno, skora, len, rece | produkcja |
| Strzelarze (172) | 154 snopy (d31-40 192) | ruda, drewno, rece | produkcja |
| Dobytek tieru 1 (regula 92) | ok. 4 600 (1 420 rekrutow x 3,2) [S] | dom rekruta | przyjete (Jeff 05.10); pytanie 6 |
| ColdStart - polki miast (raz, nowa kampania) | 44 087 szt. raz (korpus 14 983) [P] | "dorobek stuleci", 60 dni rak | przyjete (171 B5); 174 NIE powieksza (3.4 E) |
| ColdStart - zbrojownie (raz) | 344 779 szt. raz w 262 partiach [P] | "dorobek stuleci" | przyjete (171 B5); pytanie 6 |
| ColdStart - komplety ochotnikow (raz) | 1 476 kompletow [P] | "dorobek stuleci" | przyjete (171 B5) |
| Komplet DTE rekruta GRACZA | kazdy rekrut gracza = pelny wzorzec (w autotescie 0 - gracz nie werbuje) [K] RecruitKit.cs:26, :464 | z niczego | decyzja 05.10: zamknac - paczka 175 (3.6) |
| Darmowy drugi komplet AI (BEZ 171) | ok. 9 460 [S] | z niczego | ZAMKNIETE przez 171 |
| **Znika z polek bez sladu** | d2-10: 1 019, d11-20: 955, d21-30: 816, d31-40: 684 (bez amunicji karawan) [P] | - | **DO ZAMKNIECIA (174.0)** |
| Amunicja kupowana przez karawany z polek | +573/d w d2-10, ok. 0 od d11 (tabory 1 076 -> 5 921 snopow) [P] | - | zamyka 172b |

Rachunek reszty (`SCR\p174\skrypty\reszta.py`): zmiana polek bez koni = warsztaty + strzelarze + skup (lordowie, inne partie, sakiewki,
notable) + nadwyzki i tabory zalog - ZakupyAI - zakupy notabli (Ochotnicy) + RESZTA. Reszta w dziesiatkach dob: -1 592, -930, -822, -673
(d2-40 srednio -989; wersja 1 podawala -926 z innym rozgraniczeniem dob). Karawany kupuja amunicje z polki przez `SellItemsAction.Apply(town.Owner,
caravan)` (CaravansCampaignBehavior.cs:1342), czego linia skupu nie liczy - po odjeciu: -1 019, -955, -816, -684 [P].
**Reszta NIE jest stala** (wersja 1 - blad): maleje razem z zapasem (1,0 -> 0,7 tys./d), tak jak ujscia zalezne od liczby stosow na polce
(kasowanie 5% stosow z modyfikatorem, zjadanie przez mieszczan z budzetem kategorii). W biegach bez 171 przy rosnacych polkach: 935-1 068.

### 1.4 Kampania Jeffa (zapis ok. doby 360; T3 jako wzor) [P]/[S]

- Polki po roku: ok. 1,02-1,06 mln sztuk, w tym zbroja korpusu 254 tys., bron 1H 384 tys., nogi 295 tys.; reszta (helmy, tarcze,
  drzewcowa, luki, plaszcze, rekawice, amunicja) ok. 85-90 tys. (spec. 171 rozdz. 7.1) [P]. Ten stos to w ok. 2/3 sprzet z niczego
  (darmowe komplety, "nadwyzki" sakiewek), glownie t1-t2.
- Tarcze, luki, helmy i amunicja leza "na dnie" mimo zalewu (spec. 171 rozdz. 7.4) [P]; zbroi t3+ brakowalo takze przy pelnych polkach
  (notable cofaja ok. 340 awansow dziennie z braku zbroi korpusu t3 w KAZDYM biegu, z 171 i bez) [P].
- Po 171 (bez darmowych kompletow) zakupy AI z polek rosna z ok. 1,9 tys. do ok. 3,2 tys. szt./d [S] (spec. 171 rozdz. 7.2).
- **174.0 zamyka ujscia, ktore ten stos powoli zjadaly (ok. 1 tys./d)** - bez decyzji stos z niczego zostaje na stale. Pytanie 4 (zlom).

---

## 2. Przyczyna kazdej dziury

### 2.1 Tabela przyczyn (pierwsza = wiaze najmocniej)

| Koszyk | 1. przyczyna | 2. | 3. | Dowod |
|---|---|---|---|---|
| Zbroja korpusu t3+ | **rece platnerzy** (ok. 600 roboczodni/d na wszystkie zbroje) | **ruda + wegiel** | wybor wyrobu (helmy i pantofle szybsze); linie cechu po rowno | bilans 7.1-7.3; WorkshopLaw.cs:521-550 |
| Zbroja korpusu t1-t2 | **wybor wyrobu** (krawcy szyja pantofle; przeszywanice t1 sa w linii krawca "garment", t2+ u platnerza) | len (62 miasta bez lnu dla tkaczy) | znikanie (mieszczanie: garment, light_armor) | warsztaty.log: ladys_shoe 110-150/d [P]; 2.6 |
| Helm | rece platnerzy | ruda (rozdzial) | znikanie | [P] |
| Rekawice | rece platnerzy | wybor (linie po rowno) | - | [P] |
| Tarcza | **rece tarczownikow** (5% rak = ok. 140 roboczodni) | ruda: 0,03-0,09 ladunku na tarcze blokuje warsztat bez rudy po ok. 10-30 sztukach (2.6) | znikanie (BK: Shield2/3) | Settings.cs:446; BK PartySupplies.cs:123-127 |
| Bron biala | **rozdzial rudy** (856 cykli/d "brak rudy") | znikanie (BK: MeleeWeapons2/3; mieszczanie) | zle tiery na polkach (AI kupuje t i t-1) | WorkshopLaw.cs:307; AiGear.cs:247-295 |
| Luk, kusza | rece lucznikow (10%) | ruda (kusza) | - | [P] |
| Strzaly, belty | **ponad 90% amunicji jezdzi w karawanach** | ruda/drewno w miastach | - | 172b to zamyka; strzelarze zajeci 49-61% [P] |
| Nogi | brak dziury | - | - | ale z pantofli |
| Uprzaz | lordowie AI jej nie kupuja (AiGear.Order bez HorseHarness) | znikanie | - | AiGear.cs:142-149 [K] |
| WSZYSTKO | **znikanie 0,7-1,0 tys./d** | **sygnal ceny zatkany** (sufit x4) | wzrost armii (1.1a) | rozdz. 2.5, 2.7 |

**Pieniadz nie blokuje:** "bez zysku" 84 cykli/d wobec 1 850 "w robocie" i 1 013 "brak surowca"; "brak zlota/kupca" 0 [P]; warsztaty
sprzedaja za 2,6 raza kosztu [P]. **Kasy miast nie topnieja** (wersja 1 - blad): spadly w d1-10 (rozruch: 14,18 mln w d2 -> 7,45 mln
w d10), potem stoja i lekko rosna - 7,16 mln (d20), 7,33 (d30), 7,58 (d40); trend d31-40 ok. +25 tys./d, wahania doby +-180 tys. [P]
(linia "Przeplywy osad (kasy miast)"). Ani jedna kasa ponizej 1 000.

### 2.2 Rece [K][P][S z H]

- Wzor: `WorkshopLaw.TownHands` (WorkshopLaw.cs:505-510) = dobrobyt / `WorkshopProsperityPerHand` (170), w granicach 6..60
  (Settings.cs:430-432). Ten sam wzor w `Hands` (:512-518) dla ukrytego warsztatu "artisans". **Ludnosc nie wchodzi wcale.**
- Swiat d40: 2 827 roboczodni/d miast (mediana 29,2; sufit 60 nie wiaze nigdzie) + warsztaty notabli 72 x 6 = 432 + strzelarze 847,5
  (0,3 x rece, TownFletchers.cs:385) [P]. **Rece broni, zbroi i amunicji na 1000 mieszkancow swiata: ok. 0,07** [P/S].
- Rozklad zly: dobrobyt ma rozpietosc 625-7 934, ludnosc rynkow 20 tys. - 2,2 mln - Qarth (rynek 1,6 mln) ma 47 rak, Kyth 28 [P]
  (`ludzie-regiony.csv`, doba 108875; zamki przypisane do miast krolestwa proporcjonalnie - przyblizenie TradeBound).
- Historia **[S z H]** (nie [H] - wersja 1 - blad oznaczenia): kraj **0,3-0,6 rzemieslnika broni, zbroi i amunicji na 1000 mieszkancow**
  (bez krawcow; HISTORIA-WARSZTATY "Przeliczenie [SZAC]" - platnerze 250-800 + luki/strzaly 300-800 + miecznicy, siodlarze, tarczownicy;
  Anglia). Mocniejsze dane miejskie [H]: Paryz 1292 - ok. 200 mistrzow "naszych" cechow, z czeladzia 2-3 na 1000 mieszkancow miasta;
  Mediolan 1288 (osrodek eksportowy) 3-5 na 1000 [H?]; Londyn XIV w. 1,5-5 (historia.md rozdz. 2). W roboczodniach (0,75 roboczodnia
  na osobe) i z krawcami (30% naszych rak): **0,32-0,64 roboczodnia na 1000 mieszkancow dziennie**.
- Porownanie uczciwe (z amunicja po obu stronach): gra 0,07 (rece + notable + strzelarze) wobec 0,32-0,64 -> **5-9 razy za malo**
  (wersja 1 pisala raz "4-6", raz "4-9" - ujednolicone).
- Linie stoja nad zaczetymi sztukami: "w robocie (cykle)" 1 812-2 429/d, "w toku" 860 -> 2 018 sztuk [P] - reka jest waskim gardlem tam,
  gdzie surowiec jest.

### 2.3 Surowiec [P][H]

Ruda (ksiega Towary, iron, srednia d32-40, `towary.py`) [P]:

| Ruda, ladunki/d | Wartosc |
|---|---|
| Wydobycie wsi | 169,2 (+ inne ticki osad 8,0 = 177,2) |
| Warsztaty zbrojne | 67,9 |
| Strzelarze (172) | 11,0 |
| Narzedzia (linie warsztatow "smithy") + rzemieslnicy BK (iron>tools) | 24,2 + 8,8 |
| Naprawy kowali miasta (135) | 3,6 |
| Przepadlo z rozbitymi partiami (wozy wsi, inne partie) | 15,8 |
| Inne ticki osad, BetterEconomy | 4,7 + 0,4 |
| "Bez wyjasnienia" | -3,4 |
| **Zapas swiata** | **+37,4/d** (3 683 -> 3 945; miasta 2 244, zamki 198, wsie 537, tabory 966) |

Suma: 177,2 - 136,3 - 3,4 = +37,5 - zgodna. Historia [H]/[S z H]: cale zelazo Europy 0,3-0,75 kg/glowe/rok wobec 0,02 w grze (15-40 razy
mniej); **zelazo na bron** (10-25% calego, rok wojny) 290-1 800 ladunkow rudy dziennie na swiat tej wielkosci wobec ok. 79 dzis -
**5-30 razy mniej** (historia.md 4.2, 7.2). Gra kieruje na bron ok. 45% rudy.

| Surowiec | Swiat na dobe (A171 d32-40 [P]; rok T3 w nawiasie) | Gdzie wiaze | Historia |
|---|---|---|---|
| Drewno | produkcja 1 561 (las wsi 1 280, drwale 270), zuzycie 1 079 (budowy 463, zbrojne 417, strzelarze 72, przepadlo 36, mieszczanie 35, naprawy 23, BK gnicie 9) - **nadwyzka +470/d** (d1-40: +345 przy budowach 719/d; rok T3: +109, BK gnicie 179) | wegiel: ok. 6 ladunkow drewna na ladunek rudy (5 dymarka + kuznia) | dymarki przy lesie i kopalni (Bedburn 1408, Weald) |
| Skora (sztuka 10 kg) | produkcja 91,3 (rzemioslo 148: 76,2; garbarnie 14), zuzycie 86,3 (zbrojne 26,4; odziez wojska 31,0; mieszczanie 21,6; naprawy 3,1) - **nadwyzka +5,0/d**; miast bez skory 18 | skorznie, tarcze, naprawy | garbowanie, nie skory (audyt 03 L8) |
| Plotno (sztuka 10 kg) | produkcja 91,4 (148: 52,2; tkalnie 38,7), zuzycie 80,2 (zbrojne 36,3; odziez wojska 21,3; mieszczanie 20,9) - **nadwyzka +11,1/d**; miast bez plotna 60 | przeszywanice | - |
| Len, skory surowe | len +171/d (zapas 10 011 -> 11 378), skory surowe +168/d (4 620 -> 5 945) - surowca jest dosc, lezy nie tam: tkanie konczy sie "bez zysku / brak surowca / rece" 19/62/16 miast, garbowanie 55/18/24 (d40) | rozdzial, nie ilosc | - |

Ile potrzeba dla dziury korpusu bez wzrostu armii: <= 356 sztuk/d x ok. 1,2 ladunku (sredni korpus metalowy po kalibracji 4.0) = do ok.
430 ladunkow rudy/d - ok. 2,5 raza wydobycie swiata [S]. Dla calego popytu z wzrostem (745/d) - ok. 5 razy.

### 2.4 Rozdzial surowca - zle miejsce, brak przewozu [P][K]

- Miast bez rudy: 79 (d1) -> 38 (d40), plateau 35-41 od d26; 30 miast z nadwyzka (indeks 0,11); w miescie bez rudy pierwszy ladunek po
  36-37 d (4,5 x wartosci) [P]. Cena krzyczy, a nikt nie wiezie.
- Warsztat bierze surowiec TYLKO z polki wlasnego miasta (WorkshopLaw.cs:251, :275) [K].
- Woz wsi (`MarketCarts.Choose`, MarketCarts.cs:382-480) to prawdziwa partia gry (tabor wiesniakow): caly magazyn do miasta, ktore da
  najwiecej, w zasiegu 250; 16 miast lezy dalej niz 250 od kazdej kopalni [P]; wozow z ruda 4/d, do miasta bez rudy 1/d [P].
- Karawany (`CaravanBulk`): sprzedaja tylko do celu miasta (8-10 ladunkow), dokad jada - decyduje BK; 556 ladunkow w jukach, 107 ze 155
  karawan stoi w miastach [P]. Proba zmiany celu BK juz byla i nie zmienila liczby miast bez rudy (CaravanBulk.cs:73-76) [K].
- Handel gotowa bronia (`SupplyDemand.DailyTrade`, SupplyDemand.cs:379-491) przewozi tylko gotowe sztuki, nigdy surowiec [K].
- Decyzja Jeffa 05.10: surowce masowe wozi prawdziwa partia na mapie - dlatego 174.2 nie moze byc przerzutem "bez partii" (3.3).

### 2.5 Ceny - sygnal braku "zatkany" na suficie [K][P]

- `SupplyDemand.Factor` (SupplyDemand.cs:259-268): ((popyt+1)/(polka+1))^0,5 w granicach 0,25-**4** (Settings.cs:668, :671). Popyt koszyka
  = 4 x dobrobyt/3000 x waga tieru + zamowienia (do 60, wygasanie 15%/d - SupplyDemand.cs:100-118, :145-162).
- Kazdy pusty koszyk z popytem >= 15 stoi na x4 [K]; d40: 2 611 koszykow z zamowieniami, 58 925 sztuk [P]. Ranking warsztatu nie
  odroznia wtedy "brak 60 zbroi" od "brak 15 pantofli".
- Zysk nie hamuje dzis (F ok. 4). Ryzyko na pozniej (O7 w lancuch.md): przy F = 1 miasta z dobrobytem > ok. 5 140 przestana robic sztuki
  pracochlonne (dniowka x indeks placy, a wartosc po dniowce bazowej) - pozycja dla innych paczek (rozdz. 9).

### 2.6 Wybor wyrobu [K][P]

- Ranking (`WorkshopLaw.Candidates`, :344-401): 40 losowych sztuk linii po filtrze :370 (bez unikatow, legend i `WorkshopForbiddenIds`:
  weirwood, giant_, ravens_teeth, dragonglass, obsidian, val_steel, valyrian, dragonbone - Settings.cs:433), kultura miasta albo neutralne;
  ocena = (przychod - surowiec - place) / dni. Przy F = 4 wygrywaja szybkie sztuki z duzym udzialem materialu.
- **Kategoria = tier** [K]: zbroja bez `item_category` dostaje kategorie z tieru (`DefaultItemCategorySelector`: t1 "garment", t2 "light_armor",
  t3 "medium", t4 "heavy", t5-6 "ultra"). Linia krawca ("garment") robi wiec kazda zbroje t1 - takze przeszywanice t1 (23 przeszywanice
  ROT nie maja kategorii) - a przeszywanice t2+ robi platnerz (light_armor). `ladys_shoe` (t1, `Civilian="true"`, SandBoxCore leg_armors.xml:693-709)
  nie wystepuje w zadnym wzorcu oddzialu ROT (0 w ROT-Troops.xml) - to stroj dla notabli i mieszczan.
- Podzial rak (`LineShare`, :521-550): cechy wedlug Paryza 1292 (krawiec 0,30, platnerz 0,20, miecznik 0,20, siodlarz 0,15, lucznik 0,10,
  tarczownik 0,05) liczone wsrod CZYNNYCH cechow; w cechu **kazda czynna linia po rowno** - linia "ultra_armor" (t5-6) ma tyle rak, co "light_armor".
- **Linia czynna = ma cos oplacalnego PO CENACH** (:527-532) - bez sprawdzenia, czy surowiec lezy na polce. Kowal bez rudy dostaje swoja
  czesc rak i nic nie robi; 624-980 cykli/d "brak rudy" = ok. 25-35% rak swiata [S].
- **Surowiec ulamkowy** [K]: `take = floor(owed + need)` (:265-277) - tarcza (0,03-0,09 ladunku rudy) czy wlocznia (0,10) nie potrzebuja
  rudy na polce, dopoki dlug warsztatu nie dojdzie do 1 ladunku; potem caly warsztat stoi na rudzie, takze na tarczach.
- Wyniki [P]: ladys_shoe 110-150/d (60% "nog"), pirate_axe 18-22, military_fork_pike_t3 10-28; zbroja korpusu 24-51/d.

### 2.7 Ujscia w nicosc (znikanie 0,7-1,0 tys./d) - trzech podejrzanych [K]

1. **Mieszczanie "zjadaja" bron i zbroje.** Konsumpcje robi prefiks **BK `ItemConsumptionPatch`** (bk EconomyPatches.cs:579+), ktory zastepuje
   `MakeConsumption` gry; budzet kategorii liczy BK `CalculateBudget`, a nasz `HistoricalPrices.BudgetPostfix` (HistoricalPrices.cs:792-810,
   wpiecie :959-960; linia startowa "zakupy mieszczan (BK CalculateBudget) - ... wpiete" jest w A171) mnozy go przez `TownUse` - 1 dla kazdej
   kategorii poza surowcami i "arrows" (:812-827). BK wola tez `MakeConsumptionInTown` dla zamkow (BKSettlementBehavior.cs:685-690) - zjadanie
   dotyczy takze polek zamkow. Zloto z niczego trafia do kasy osady.
2. **Gra kasuje sztuki z modyfikatorem.** `ItemConsumptionBehavior.DeleteOverproducedItems` (gra, :79-97, tylko miasta - :64): co dobe kazdy
   stos z `ItemModifier` traci 1 sztuke z szansa 5%; stosy `IsCraftedByPlayer` i sztandary - cale (to samo w BK `DeleteOverProduction`,
   BKSettlementBehavior.cs:329-331, dla zamkow). Wyrob warsztatu dostaje losowy modyfikator (WorkshopLaw.cs:317).
3. **BK zaopatrzenie partii AI.** `PartySupplies` (bk PartySupplies.cs:118-127, :296-315, :327-330) kupuje i niszczy bron (MeleeWeapons2/3)
   i tarcze (Shield2/3): 0,006 / 0,003 na zolnierza na dobe (BKPartyNeedsModel :22-26), zloto lorda w nicosc. Armoury tnie sufit
   (`BkSupplyTemper`, 12 szt. / 4 doby), zeruje strzaly (172) i tekstylia (150) - bron i tarcze nie [K]. Gorna granica: 98 tys. ludzi w
   partiach x 0,009 = ok. 880 szt./d [S].
Podzialu miedzy nich z dzisiejszego logu nie da sie zrobic (ksiega towarow 146 nie liczy uzbrojenia) - 174.0 najpierw mierzy, potem zamyka.
Reszta maleje z zapasem (1.3), wiec kasowanie i zjadanie zaleza od liczby stosow - pasuje do 1. i 2.

### 2.8 Wydajnosc na reke - gra wobec historii (czy skracac dni pracy?) [K][H]

| Wyrob | Gra (`ArmsPricing.Compute`, ArmsPricing.cs:165-232; `HistDays`) | Historia [H] (historia.md rozdz. 3) | Werdykt |
|---|---|---|---|
| Kolczuga | 5 dni na kg (haubergeon 9 kg = 45 dni, hauberk 18 kg = 90) | 40-60 roboczodni na haubergeon w warsztacie z podzialem pracy; Tower 1353-60 | zgodne - nie ruszac |
| Zbroja "na amunicje" (plyty, cotte) | 1,5 dnia na kg + tier | Paryz 1384: 500 kompletow + 300 par nagolennikow w < 3 miesiace (1/30-1/40 roboczodnia) | zgodne |
| Pelna zbroja plytowa t6 | ok. 118 dni (wpis 52) | Greenwich 1544: 18 ludzi = 32 zbroje/rok (ok. 170 dni; zbroje dworskie) | szybciej niz historia - nie skracac |
| Helm | plyta: w x 1,5 + t dni x q (t1-t2: ok. 4-6 dni) | 3-10 dni mistrza z pomocnikiem = **6-20 roboczodni** (1/6-1/20) | **na dolnej granicy** (nie "zgodne" - wersja 1); patrz nizej |
| Miecz | `DaysWeapon` 1-12 | klinga 1-3 dni + oprawa i pochwa osobno | zgodne |
| Tarcza / pawez | `DaysShield` 1-5 | Tower 1399: 500 pawezy = 2 040 dniowek (ok. 4 dni) | zgodne |
| Luk | `DaysBow` 2-12 | Brugia: 4 242 luki w 2 lata (z gotowych kosturow - ok. 1 dnia); z ceny 12-18 d: 3-5 dni | w pasmie (nasz lucznik robi tez kostur) |
| Strzaly | 12,5 strzaly na roboczodzien (`HistAmmoLaborMultiplier` 8) | rachunki robocizny 25-35; z ceny snopa 7-10 | w pasmie cen - zostaje 8 (strzelarze zajeci w 49-61%) |

**Wniosek: wydajnosc na reke jest historyczna albo na dolnej granicy. Skracanie dni pracy byloby robota z niczego - 174 tego NIE robi.**
Helm: po 174.3 produkcja 200-230/d jest powyzej historycznego pasma dla swiata tej wielkosci (58-173/d, historia.md 7.2), po S5 ok. 4 razy
powyzej. Swiadomie: popyt wojska gry rosnie 2 razy szybciej niz historyczny rekord (Calais 1346-47, historia.md 7.2), a helm jest drugim
brakiem. Dni helmu t1-t2 zostaja; wrocic po S5 (rozdz. 9).

---

## 3. Poprawki

### 3.0 Zasady calej paczki

- **Zero sprzetu z niczego - takze posrednio.** Kazda nowa sztuka ma rude/drewno/skore/len z polki, rece i zaplate; kazdy przewoz ma
  platnika, droge i prawdziwa partie na mapie (decyzja 05.10). Nie ma dosypki rudy, skrocenia dni pracy, kompletu ze wzorca ani "pomocy"
  w bitwie. **Zaden istniejacy strumien bez materialu nie rosnie przy okazji** (ColdStart liczy rece starym wzorem - 3.4 E).
- **Jedna zmiana naraz** (CLAUDE.md zasada 2): piec podpaczek, kazda jeden commit, wlasne wylaczniki MCM, linia startowa i dzienna, autotest
  40 dob. Kolejnosc: 174.0 -> 174.0b -> 174.1 -> 174.2 -> 174.3; na koniec autotest 120 dob calosci i 40 dob na zapisie roku (rozdz. 6).
  Kazda podpaczka porownywana z biegiem poprzedniej (nie z A171), bo 174.0 zmienia baze.
- **Tylko NOWE klucze MCM.** Zadnej zmiany domyslnej w istniejacych kluczach (`WorkshopProsperityPerHand`, `MineOutputMultiplier`,
  `VillageWoodlotLoads`, `GuildShare*`, `WorkshopArtisansMax`) - pulapka z CLAUDE.md rozdz. 7 (zapisany `Armoury.json` Jeffa nadpisuje domyslne).
- **Parametry z historii, stopniami, jawnie** (K13, decyzja 05.10): rece rosna z ruda i drewnem razem (wariant B); stopien lasu wybrany wedlug popytu na wegiel (4.5).
- **Jedna regula dla gracza i AI** tam, gdzie to mozliwe bez zmiany rozgrywki; tam, gdzie zmienia - pytanie (rozdz. 8, pytanie 5).

### 3.1 174.0 KONIEC ZNIKANIA (pomiar + zamkniecie ujsc + nowa miara pokrycia)

**Regula:** bron i zbroja to nie towar domowy i nie gnija na straganie; zuzycie broni armii AI liczy jedna regula (Armoury: AiWear,
naprawy kowali, wraki 158), nie druga (BK). Konie nie wchodza (zywy inwentarz - osobna regula, 156).

(a) **Mieszczanie.** `HistoricalPrices.TownUse` (HistoricalPrices.cs:812-827) przy `ArmsNotHouseholdGoods`: 0 dla kategorii uzbrojenia -
ten sam predykat co `WorkshopLaw.GuildOf` / `ColdStart.GuildOfCategory` (id konczy sie na "_armor" albo zaczyna od "melee_weapons",
"ranged_weapons", "shield", "horse_equipment"). Dziala przez `BudgetPostfix` na BK `CalculateBudget` (wpiecie :959-960) - BK
`ItemConsumptionPatch` bierze sztuki tylko z kategorii z budzetem, wiec zero budzetu = zero zjadania; dotyczy miast i zamkow (BK wola
`MakeConsumptionInTown` takze dla zamkow). W `BudgetPostfix` mnoznik uzbrojenia stosowac takze przy wylaczonym `TownHouseholdUse` (jak "arrows").
**"garment" zostaje** (odziez cywilna t1 - prawdziwe uzycie; zloto z niczego za te zakupy to temat kiesy ludu 173), ale jest liczona osobno
w linii dziennej (nie jako "uzbrojenie"). "arrows" bez zmian (172).
- Historia [H]: Winchester 1285 - bron w domu jest zapasem na przeglad, nie towarem zuzywalnym; zuzycie zbroi to zywot 3-30 lat
  (historia.md rozdz. 6), juz liczony w naszym zuzyciu i naprawach.
- Skutek dla kas [S]: znika zloto z niczego za "zjedzona" bron (rzad 10-20 tys. d/d) - mierzone w "Przeplywy osad" (MoneyLedger :455-480).

(b) **Kasowanie gry.** Nowy plik `Armoury/src/ArmsLeaks.cs` (`internal static class ArmsLeaks`): prefiks na
`ItemConsumptionBehavior.DeleteOverproducedItems(Town)` przy `ArmsNoStallDecay`: ta sama petla co gra, **z pominieciem
`SupplyDemand.Equipmentish(it)` bez koni**; zwraca false. Wyroby gracza (`IsCraftedByPlayer`) i sztandary - cale stosy jak w grze,
**jako jawna regula z powodem technicznym**: to dynamicznie tworzone przedmioty zapisu (kazda sztuka kuta przez gracza to osobny obiekt
w zapisie) - trzymanie ich na polkach rozdyma zapis; ujscie liczone osobno w linii ("wyroby gracza X szt.") i opisane w MCM. Licznik: stosy
pominiete i oczekiwane sztuki (0,05 x stos). Postfiks `MoneyLedger.ShelfPostfix` biegnie dalej (Harmony wola postfiksy po pominieciu oryginalu).
- Historia [H]: zbroja nie gnije; rdza i uszkodzenia = stan sztuki (RBM, wraki na zlom - 158). Sklad nie byl jednak darmowy - patrz (e).

(c) **BK zaopatrzenie AI.** `BkSupplyTemper` - ten sam wzor co strzaly 172 (BkSupplyTemper.cs:94-140): postfiksy Priority.Last na
`BKPartyNeedsModel.CalculateWeaponsNeed` i `CalculateShieldsNeed` -> 0 dla partii AI przy `BkSuppliesNoArms` i `AiGear.On`;
zerowanie zapisanych `WeaponsNeed` / `ShieldsNeed` (settery jak `ArrowsNeed`, PartySupplies.cs:65, :74) w prefiksie `PartySupplies.BuyItems()`
- dzisiejszy `ArrowsBuyPrefix` uogolnic na trzy potrzeby (`NeedsBuyPrefix`). Partia gracza: osobny klucz `BkSuppliesNoArmsPlayer`
(bron, tarcze i strzaly) - domyslnie wedlug odpowiedzi na pytanie 5 (rekomendacja: tak - jedna regula; dzis gracz placi zuzycie dwa razy:
BK i Armoury).

(d) **Pomiar ujsc (GoodsLedger).** Ksiega towarow (GoodsLedger.cs, ramki :1040-1080) liczy dzis tylko towary handlowe i zywy inwentarz. Dodac
sledzenie uzbrojenia (`Equipmentish` bez koni) zbiorczo wedlug `ItemTypeEnum` (14 typow) i tieru w istniejacych ramkach + nowa ramka
wewnetrzna "kasowanie gry" na `DeleteOverproducedItems`. Zapas uzbrojenia NIE jest liczony (zbrojownie DTE to nie rostery) - linia podaje
ujscia. Do tego **liczba stosow uzbrojenia na polkach** (przedmiot + modyfikator) i jej przyrost - po zamknieciu kasowania 5% to jedyny
pomiar, ze petle po polkach (BK konsumpcja, BuyItems, AiGear, DailyTrade, SupplyDemand) nie puchna. Wylacznik: `GoodsLedgerEnabled` (istniejacy).

(e) **Konserwacja zapasu (sklad nie jest darmowy)** (`ArmsStallUpkeepManDaysPerPiece` = 0,02 roboczodnia na sztuke na rok). Historia [H]:
Tower lata 1340. - 3 200 lukow czysci i oliwi 11 ludzi w 5 dni (0,017 roboczodnia na sztuke); 1375-77 - 125 mieczy konserwuje 1 robotnik
w 24 dni (0,19 na miecz); Jan bez Trwogi - ok. 8% wydatkow na bron szlo na utrzymanie (historia.md 6). Regula: kazde miasto codziennie
odejmuje od rak platnerzy i miecznikow (jak naprawy `SmithHours`, WorkshopLaw.cs LineShare) `stos_uzbrojenia_na_polce x 0,02 / 364`
roboczodni. Rece, nie zloto - nic z niczego i nic w nicosc. Skala [S]: nowa kampania ok. 20-40 tys. sztuk na polkach = 1-2 roboczodni/d na swiat
(< 0,1% rak); kampania Jeffa 1 mln = ok. 55 roboczodni/d (ok. 1% rak) - tyle, ile kosztuje trzymanie takiego zapasu.

(f) **Nowa miara pokrycia "dowolny szczebel"** (`ArmsDrill`, ta sama petla co linia "Pokrycie"): dla kazdego typu slotu (korpus, helm,
tarcza, rekawice, nogi, glowna bron) - potrzeba wedlug typu (suma tierow) wobec sztuk tego typu w zbrojowni w DOWOLNYM tierze; odsetek
ludzi z czymkolwiek = min(1, sztuki / potrzeba) liczony na partie i sumowany wedlug ludzi. To odpowiada temu, co DTE zalozy w bitwie
(tier <= t+2 bez dolnej granicy). Miara szczebla zostaje bez zmian (cwiczenia i awanse - ArmsDrill - patrza na nia).

**Ile da [S]:** ok. 0,6-0,7 tys. szt./d przestaje znikac (reszta d31-40 ok. 0,68 tys./d minus to, co mieszczanie dalej biora jako "garment"
- zmierzy pierwsza linia "Uzbrojenie (ujscia 174)"). Wersja 1 obiecywala 0,6-1,0 tys. - za duzo (odziez zostaje, a reszta maleje z zapasem).

### 3.1b 174.0b ROBOTA W TOKU PRZETRWA ZAPIS (audyt 05.10 W7 - otwarte)

Dzis trzy rzeczy zyja tylko w pamieci sesji [K]: `WorkshopLaw._wip` (sztuki w robocie: 2 018 w d40; surowiec juz zdjety z polki -
`shelf.AddToCounts(-take)` - i zapisany w `OreLedger.NoteWorkshop`), `_owed` (dlug ulamkowy surowca), `SupplyDemand._unmet` (zamowienia;
SupplyDemand.cs:96). `ArmouryBehavior()` wola `WorkshopLaw.Reset` (ArmouryBehavior.cs:389), a `SyncData` (:391-502) nie ma dla nich klucza.
Kazde wczytanie: ruda, drewno, skora i plotno zaczetych sztuk znikaja bez wyrobu (ujscie w nicosc), dlug ulamkowy daje kazdemu warsztatowi
znowu do 1 jednostki surowca za darmo, a zamowienia (sygnal popytu, W_b w 174.1) startuja od zera. 174.1 (E) i 174.3 (rece x2) zwiekszaja
liczbe sztuk w toku - wiec to warunek wstepny.
- Zapis: `arm_workshops` przez `SaveText.Sync` w kawalkach (paczka 161; limit napisu 32 KB): na warsztat - osada (StringId), indeks
  warsztatu w `town.Workshops`, typ warsztatu, linia (LineKey), przedmiot (StringId), dni, praca, koszt surowca, dzien; `_owed` (4 liczby);
  `_unmet` (klucz, wartosc). Wzor: `MarketGlut.Export/Import`.
- Wczytanie: pominac warsztaty, ktorych juz nie ma albo zmienily typ, i przedmioty, ktorych nie ma (zepsuty zapis - audyt 04.10, wpis 24);
  surowiec pominietej sztuki ksiegowac w OreLedger/GoodsLedger jako "przepadlo przy wczytaniu" (jawne ujscie, nie ciche). Stary zapis: pusto.
- Linia po wczytaniu: `Warsztaty (zapis 174): wczytano sztuk w toku X (surowca: ruda a, drewno b, skora c, len d), pominieto Y (surowiec
  przepadl: ...), dlugow surowca Z warsztatow, zamowien W (szt. V).`
- TownFletchers i TownCrafts maja wlasne dlugi rak/surowca - przejrzec przy okazji i zapisac tym samym kluczem, jesli zyja tylko w pamieci.

### 3.2 174.1 RECE IDA DO SUROWCA, WYROB WEDLUG BRAKU (WorkshopLaw.cs, te same rece)

**(A) Linia czynna = moze dzis pracowac** (`WorkshopHandsFollowMaterial`). W `LineShare` (:521-550) linia jest czynna, gdy (1) ma sztuke
w robocie (`_wip[(warsztat, linia)].Item != null`) albo (2) w jej rankingu jest sztuka, ktora da sie zaczac DZIS: surowiec na polce
(`MissMask(ShelfHave(polka), owed, need) == 0`, :178-198 - ten sam warunek co start :269-278), zysk >= `WorkshopMinProfitPercent` (:288),
kapital warsztatu >= koszt surowca (:290) **i warunek (E) "skonczy w planie"**. Linia nieczynna dostaje 0 - takze w cechu z innymi czynnymi
liniami. Cech bez czynnej linii odpada z sumy wag (:540).
- **Wolne rece miedzy cechami wedlug braku** (`WorkshopFreedHandsByShortage`): dzis rece cechu bez czynnej linii ida do pozostalych cechow
  wedlug wag Paryza (gdy stoja miecznicy: krawcy 37,5%, platnerze 25%, siodlarze 19%, lucznicy 12,5%, tarczownicy 6%; gdy stoja tez platnerze:
  krawcy 50%, siodlarze 25%, lucznicy 17%, tarczownicy 8%) - czyli glownie do krawcow i siodlarzy, a uprzezy AI nie kupuje. Nowa regula:
  rece czynnych cechow = H x w_g / suma_czynnych(w) jak dzis (Paryz - historyczny udzial zostaje dla rak "wlasnych"), a rece **zwolnione**
  (H x suma_nieczynnych(w) / suma_wszystkich(w)) ida do czynnych cechow wedlug braku ich koszykow (short_g / suma short); gdy suma braku = 0 -
  wedlug wag Paryza (dzis). Historia [H]: w wojnie rzemiosla pokrewne braly zamowienia sasiednich (Tower 1399 - pawezy od ciesli; Montefioralle
  - cala wies kowali na groty beltow, historia.md rozdz. 2-3); czeladnik szedl tam, gdzie byla robota.
- Koszt: ranking juz w pamieci dnia (`_rank`); do pozycji rankingu dopisac przychod, koszt, potrzeby i dni z jednego przebiegu
  `Candidates` - zero dodatkowych wywolan `Factor`; `ShelfHave` raz na warsztat na dobe.

**(B) Wybor wedlug braku, potem marzy** (`WorkshopChooseByShortage`). Jeff 04.10: "co brakuje i ma najlepsza marze" - dzis dziala tylko
druga polowa. W `Candidates` (:344-401), **po filtrze :370 bez zmian** (unikaty, legendy, `WorkshopForbiddenIds`; nowe pozycje rankingu
tylko ze sztuk po filtrze - stali valyrianskiej nie wykuwa sie od Zaglady):
1. Dla kazdej sztuki z rankingu: koszyk b = typ x tier, d_b i s_b z tego samego wywolania `SupplyDemand.Factor(..., out d, out s)`,
   ktore juz liczy przychod (`Revenue` :463-471 - oddac d i s).
2. Brak koszyka w miescie: `short_b = max(0, d_b - s_b - wip_b)`, gdzie wip_b = sztuki tego koszyka w robocie w tym miescie (nowy licznik
   miasto -> koszyk, aktualizowany przy starcie i oddaniu sztuki; zapisywany w 174.0b).
3. Waga braku `W_b = F_bez_sufitu / F_z_sufitem` (>= 1; F bez sufitu = ((d+1)/(s+1))^0,5). Przy pustej polce i 60 zamowieniach W ok. 2,1.
4. Kolejnosc: koszyki wedlug `W_b x najlepszy zysk na roboczodzien w koszyku` (malejaco); w koszyku - patrz (C).
- **Przedmioty cywilne bez zolnierza** (`Civilian="true"` i nieobecne w zadnym wzorcu oddzialu - lista liczona raz przy starcie z
  `CharacterObject.All`): poza liniami zbrojnymi (pomijane w `Candidates`), poza ColdStart i poza liczbami produkcji uzbrojenia. ladys_shoe
  i podobne przestaja byc "butami wojska"; mieszczanie kupuja dalej inna odziez t1.
- Nic z niczego: zmienia sie tylko kolejnosc istniejacej roboty; bramka zysku przy starcie (:288) liczy dalej po prawdziwej cenie (z sufitem).

**(C) Zbroja "na amunicje" - tylko czesc i tylko t1-t3** (`WorkshopMunitionGrade`, `WorkshopMunitionMaxTier` = 3, `WorkshopMunitionShare` = 0,5).
W koszyku z brakiem (`short_b > 0`) i tierze <= 3 co druga rozpoczeta sztuka linii (licznik linii) to sztuka o najmniejszej liczbie dni pracy
(remis - zysk na dzien); pozostale - zysk na dzien jak dzis. Tiery 4-6 bez zmian. Powod [K]: dni = podstawa x q, gdzie q = skutecznosc /
mediana tieru w granicach 0,6-1,8 (ArmsPricing.cs:213-216, :226) - "najmniej dni" to najslabsza sztuka tieru; stosowana zawsze zrobilaby
z kazdego sklepu skup najslabszych sztuk na miesiace. Historia [H]: Paryz 1384 - 500 kompletow "na amunicje" OBOK zamowien lepszych zbroi
(Bernard 2015); 1322 - 300 aketonow i 400 bascinetow na 500 ludzi (Kirkland 2015); Florencja 1431 - kupowano takze stare i gorsze (Picchianti 2025).
Linia: srednie q zrobionych sztuk wedlug tieru. Zmiana rozgrywki (sklepy) - pytanie 3.

**(D) Linie cechu wedlug braku** (`WorkshopLineShortageShare` = 0,75). W `LineShare` (:549) zamiast `h / lines`:
`udzial_L = h_cechu x (0,25 / n + 0,75 x short_L / suma_short)`, gdzie **n i suma_short licza tylko linie czynne wedlug (A) z (E)**;
short_L = suma short_b koszykow, ktore linia robi (z jej rankingu); gdy suma = 0 - po rowno. **Dokoncz zaczete:** linia ze sztuka w robocie
ma podloge `h_cechu / n` (rowny podzial, jak dzis), a pozostale linie skaluja sie tak, zeby **suma udzialow = h_cechu** (kontrola w logu
co do 0,1%). Cwierc rak cechu rozlozona rowno [S: mistrz trzyma stalych klientow; zadna linia nie gasnie przez jeden dzien zamowien].

**(E) Nie zaczynaj sztuki, ktorej nie skonczysz** (`WorkshopPlanDays` = 60). Przy starcie (petla :261-303) pomijac kandydata, gdy
`dni > w.Labor + max(udzial_L, h_cechu / n) x WorkshopPlanDays`. Plan liczony z **rownego podzialu jako podlogi** - wersja 1 liczyla
z udzialu (D), a linia "ultra" dostaje przy (D) ok. 0,5 reki w medianie miasta (29 rak x 0,20 platnerzy = 5,8; 0,25/4 x 5,8 + maly brak),
czyli moglaby zaczac sztuke do ok. 60 dni: pelna plyta t6 (118 dni) i ciezka kolczuga 18 kg (90 dni, ArmsPricing.cs:185-186) przestalyby
powstawac prawie wszedzie [K/S]. Z podloga: tam, gdzie dzis da sie zaczac t5-6, da sie dalej; "dokoncz zaczete" (D) konczy ja w planie.
60 = dzisiejszy bank roboty (:247 - bank tez czyta ten klucz, domyslnie bez zmiany). Male miasto (6 rak) nie zamraza 3 ladunkow rudy
na kolczuge na rok.

**Ile da 174.1 sama [S] (z mechaniki, bez nowego surowca):** w 38 miastach bez rudy zwolnione rece ida do czynnych cechow wedlug braku -
w praktyce do linii bez rudy: przeszywanice t1 (krawiec, len/skora), skorznie i przeszywanice t2+ (platnerz light), luki, bron i tarcze
calkiem drewniane; tarcze z odrobina rudy tylko do chwili, gdy dlug warsztatu dojdzie do 1 ladunku. Krawcy przestaja szyc ladys_shoe (B).
Korpus 51 -> ok. 55-75/d, tarcze 59 -> 60-80/d, nogi w dol (bez pantofli), reszta +-10%; razem ok. 1,02-1,25 tys./d. Wiekszy skok (tarcze,
korpus metalowy) dopiero z rudy (174.2) - wersja 1 brala progi 174.1 z prognozy laczonej 174.1 + 174.2 (blad).

### 3.3 174.2 ZAMOWIENIA SUROWCA - KONTRAKT DLA PRAWDZIWEJ KARAWANY (decyzja 05.10)

**Regula:** miasto, w ktorym wczoraj warsztaty zbrojne, strzelarze albo rzemioslo miasta 148 (tkacze, garbarze, folusznicy) odpuscili cykl
z braku surowca m (ruda, drewno, skora, plotno, len, skory surowe, welna), zamawia go w pobliskim niewrogim miescie albo zamku, ktore ma go
ponad prog nadwyzki. **Towar wiezie prawdziwa karawana na mapie** stojaca w miescie-zrodle: kupuje go tam po cenie targu zrodla (zloto
karawany -> kasa zrodla, `SellItemsAction` - jak zwykly handel), jedzie do miasta-zamawiajacego (bandy i wrogowie moga ja rozbic - lup bierze
zwyciezca wedlug zasad gry, decyzja 05.10 "paser"), na miejscu sprzedaje po cenie targu zamawiajacego (kasa miasta -> zloto karawany).
Marza karawany to zaplata za droge; kontrakt powstaje tylko, gdy oczekiwana marza >= koszt drogi. Warsztat kupuje potem z polki swojego miasta
jak dotad (WorkshopLaw.cs:269-299) - nie ma podwojnej zaplaty. Wersja 1 proponowala "woz bez partii" (wzor `GarrisonCarts`) - to bylby
niewidzialny przerzut wbrew decyzji 05.10, bez strat w drodze (a prawdziwe wozy traca rude - 15,8 ladunku/d "przepadlo z rozbitymi
partiami"), wiec wypieralby prawdziwy transport. Odrzucone.
- Historia [H]: kowale kupowali zelazo od kupcow (ironmongers), nie z kopalni (AUDYT-2026-10-05:1259); **daleko wozono sztaby i osmundy
  (Biskaje, Szwecja, Gorny Palatynat), nie rude** - sztaba wazy ok. 1/7 rudy i nie potrzebuje w miescie 6 ladunkow drewna na wegiel
  (historia.md 4.2). Dlatego ruda jedzie tylko blisko; daleko - dopiero sztaby po paczce 133. Oplata ladowa ok. 1,5 d za tone na mile
  (Anglia XIV w.) [H?: Masschaele 1993, Economic History Review 46 - liczba z drugiej reki, ta sama co w opisie `RawPrice`, RawPrice.cs:46-48];
  przewoz morski kilka razy taniej za tone-mile [H?: tamze - do sprawdzenia w zrodle przed wdrozeniem].

Algorytm (raz na dobe, w `ArmouryBehavior.OnDailyTick` po `SupplyDemand.DailyTrade` i `GarrisonCarts.Daily`; nowy plik
`Armoury/src/MaterialOrders.cs`, `internal static class MaterialOrders`):
1. **Sygnal:** licznik miasto -> surowiec "brak surowca" z wczoraj: w `WorkshopLaw.CyclePrefix` obok `_skipMatBy[m]++` (:307) dopisac
   `_skipMatTown[town][m]++`; w `TownFletchers` obok `_dMissBy`; w `TownCrafts` przy koncu przerobu "brak surowca" (len, skory surowe, welna).
   Miasto zamawia surowiec m najwyzej raz na `TownMaterialOrderDays` (3) dob.
2. **Ile:** cel = zapas na 10 dob zmierzonego zuzycia miasta (srednia z ok. 14 dob: warsztaty + strzelarze + rzemioslo 148 - te same srednie,
   ktore 148 juz liczy) minus polka minus w drodze; najwyzej udzwig karawany (wolne miejsce w jukach).
3. **Skad:** osady niewrogie, w zasiegu: **ruda `TownMaterialOrderRangeOre` = 200** (2-3 doby drogi [S]: `MarketCarts.PerDay` ok. 61 jednostek/d),
   pozostale surowce `TownMaterialOrderRange` = 600; droga ladem, a gdy jej nie ma (16 wysp z jednym miastem - IslandRoads.cs:20; Pyke) -
   ladem i morzem (`NavigationType.All`, jak GarrisonCarts.cs:120-124), tylko gdy karawana umie plynac. **Prog zrodla niezalezny od rak:**
   zrodlo sprzedaje tylko to, co ma ponad `max(Keep wedlug starych rak, 10 dob zmierzonego wlasnego zuzycia)` (zamek: caly zapas - nie ma
   warsztatow). Wybor: najwiekszy oczekiwany zysk karawany na kg.
4. **Karawana:** `CaravanPartyComponent`, nie gracza, nie posilki DTE, aktywna, bez MapEvent i armii, stoi w miescie-zrodle
   (`CurrentSettlement`). Brak takiej karawany = brak kontraktu (linia: "brak karawany w zrodle") - nie ma przewozu bez partii.
   Kontrakt: Armoury ustawia cel (`SetMoveGoToSettlement`) i `Ai.SetDoNotMakeNewDecisions(true)` do przyjazdu (wzor posilkow DTE);
   `CaravanBulk.Trades` i 172b musza przepuszczac karawane z kontraktem (dzis odrzucaja `DoNotMakeNewDecisions`). Przyjazd
   (`OnSettlementEntered`): sprzedaz ladunku, zwolnienie AI, koniec kontraktu. Zniszczenie/pojmanie: kontrakt przepada, ladunek - jak w grze
   (lup); ksiega: "przepadlo z rozbitymi partiami (karawany kontraktowe)". Wrogosc zrodla/celu albo 30 dob - zwolnienie (karawana handluje
   dalej sama, ladunek zostaje jej - prawdziwy towar, nie zwrot z niczego).
5. **Oplacalnosc** (cena po dostawie, oplata od kg): zysk = sum_{i<q} cena_celu(polka + i) - q x cena_zrodla - oplata, gdzie
   `oplata = CarterPencePerKgPer100 (0,0375 d) x waga_kg x odleglosc / 100`; odcinek morski x `SeaFreightShare` (0,25) [H?]. Ladunek rudy
   (100 kg) = 3,75 d na 100 jednostek (jak wersja 1), skora i plotno (10 kg) = 0,375 d - wersja 1 liczyla "za ladunek" i dla skory/plotna
   bylo 10 razy za drogo; liczyla tez zysk po cenie sprzed dostawy (36 d za pierwszy ladunek), czyli kupujacy dokladalby stale. Zysk <= 0 -> brak kontraktu.
6. **Ksiega:** ladunek w jukach karawany to istniejacy posiadacz "karawany" w OreLedger i GoodsLedger - nic nowego do bilansu; licznik
   kontraktow osobno. MoneyLedger: przeplywy to zwykly handel karawan (kasy osad <-> karawany), dopisac pozycje "kontrakty surowca (174)".
7. **Zapis:** `arm_matorders` (lista: karawana Id, cel, surowiec, ilosc, dzien) przez `SaveText.Sync`; po wczytaniu - ponowne
   `SetDoNotMakeNewDecisions` albo zwolnienie, gdy karawany nie ma. Stary zapis: pusto.

**Ile da [S]:** miast bez rudy 38 -> ok. 20-25 (zasieg 200 nie obejmie miast daleko od kopaln - to zostaje dla 133); w miastach bez lnu dla
tkaczy (62) i bez skor dla garbarzy (18) - len i skory surowe, ktorych w swiecie przybywa +170/d, jada do warsztatow 148, wiec plotno
i skora rosna razem z popytem przeszywanic i tarcz. Nie robimy: ruszania celu karawan BK (juz probowane), dosypki rudy, przewozu bez partii.

### 3.4 174.3 RECE WEDLUG LUDNOSCI + KROK 2 WARIANTU B (kopalnie x1,5, las wsi x1,6, koniec gnicia surowcow w BK)

**(A) Wzor rak** (`WorkshopHandsByPeople`). `WorkshopLaw.TownHands` i galaz ukrytego warsztatu w `Hands` (:505-518):

    rece = max( rece_stare,
                min(WorkshopHandsMaxPerTown, WorkshopHandsPer1000People x ludzie_rynku_tab / 1000 x TownWage.Index(town)) )
    rece_stare = dobrobyt / WorkshopProsperityPerHand w granicach WorkshopArtisansMin..WorkshopArtisansMax (dzisiejszy wzor)

- **`ludzie_rynku_tab` - z tabeli krain, nie z rosnacej ksiegi** [K]: `PopulationLaw.PeopleOf` liczy ludzi z hearth wsi i dobrobytu
  (PopulationLaw.cs:115-124) i rosnie z nimi: w A171 swiat 52,51 -> 58,98 mln w 39 dob, Lotus Bay 0,75 -> 2,20 mln przy dobrobycie
  502 -> 625 [P]; w roku T3 52,6 -> 90,5 mln (+72%, Wyspy Letnie x12) - audyt 09.10 (01 L8, 09 P-8): "nowego modelu nie wolno na niej oprzec".
  Wzor wersji 1 dalby rece swiata 5 547 -> 6 325 (+14%) w 39 dob z hearth z niczego (w tym inwestycji BEE), a w kampanii Jeffa ok. 1,5-1,7
  raza wiecej niz prognoza. Dlatego: nowa metoda `PopulationLaw.TablePeopleOf(settlement)` = ten sam wzor co `PeopleOf`, ale wspolczynnik
  liczony co dobe z dzisiejszych sum hearth i dobrobytu kultury (bez zapisu `_k`): **suma swiata = tabela krain (`Table` x `PopulationScale`)
  zawsze**; zmienia sie tylko podzial miedzy osady. `ludzie_rynku_tab` = `TablePeopleOf(miasto)` + suma `TablePeopleOf(wies)` dla wsi z
  `Village.TradeBound == miasto` (ta sama wiez co `ArmyClothing.MarketTown` i wpis 100); pamiec dnia. Gdy ksiega ludzi zostanie naprawiona
  (P-8, paczka 109 - przyrost -1,3..+0,8% rocznie [H] audyt 09), mozna przejsc na nia jednym przelacznikiem.
- **Nikt nie traci rak (`max` ze starego wzoru).** Wzor bez `max` zabieral rece 20 miastom (Castle Black 34 -> 6, Kyth i Saath 28-29 -> 6,
  Hardhome i Thenn 27 -> 6, Zelazne Wyspy razem 153 -> 51 - Pyke 32 -> 10, Lordsport 28 -> 9, Ten Towers 27 -> 8, Blacktyde 27 -> 9;
  Dragonstone 117 -> 84, Ibben 69 -> 27, Sarnor 57 -> 12, Skagos 20 -> 6) [S, `rece.py`]. Kanon [H?]: Zelazne Wyspy maja kopalnie i kowali
  (World of Ice and Fire); Castle Black lezy NA Murze (po poludniowej stronie, nie "za Murem" - wersja 1 - blad) i ma kuznie i platnerza
  (Donal Noye). Bez `max` rece Zelaznych Wysp spadlyby trzykrotnie - wbrew kanonowi i bez powodu w grze.
- `TownWage.Index` (TownWage.cs:17-24) = dobrobyt / 4 800 w granicach 0,5-1,5 - ta sama miara, z ktorej idzie placa miasta (jedna regula:
  bogate miasto placi wiecej i ma wiecej mistrzow; osrodki eksportowe 3-5 na 1000 mieszczan wobec 1-3 w zwyklych).
- `WorkshopHandsPer1000People` = **0,10** roboczodnia dziennie na 1000 ludzi rynku **[S z H]**. Z amunicja i notablami (strzelarze rosna
  razem: 0,3 x rece) swiat ma po kroku ok. 0,15-0,16 na 1000 - **ok. polowa historycznego minimum 0,32** (wersja 1 pisala "1/3 minimum",
  liczac bez strzelarzy - blad). Krok wynika z rudy i drewna, ktore udzwigna swiat po (B)-(C) - rozdz. 4.5.
- `WorkshopArtisansMin` 6 (istniejacy, bez zmiany); sufit bezpieczenstwa `WorkshopHandsMaxPerTown` = 1 000.
- **Dlaczego ludzie rynku, a nie tylko ludzie w murach:** historyczna liczba 0,3-0,6 to szacunek na 1000 mieszkancow KRAJU (rzemieslnicy
  mieszkaja w miastach, ale obsluguja okolice - i z okolicy idzie pobor). Licznik "tylko mury" przenioslby 75% rak do Essos (miasta Essos
  maja 8,2 z 10,9 mln mieszczan), a Westeros ma 30,4 z 59 mln ludzi i wiekszosc wojen.
- **Wynik (ludzie z tabeli = doba 1, dobrobyt doby 40)** [S, `rece2.py`]: swiat **ok. 6 050 roboczodni** (dzis 2 827, x2,14); Westeros 54%;
  zadne miasto ponizej dzisiejszych rak. Wedlug krolestw (stare -> nowe): Reach 185 -> 891 (x4,8), Krolewska Przystan (Baratheon) 220 -> 566
  (x2,6), Qarth 110 -> 507 (x4,6), Volantis 117 -> 477 (x4,1), Braavos 65 -> 290 (x4,5), Dolina 151 -> 318 (x2,1), Polnoc 257 -> 315 (x1,2),
  Dorzecze 204 -> 307 (x1,5), Ziemie Burzy 124 -> 283 (x2,3), Dorne 151 -> 162 (x1,1), Pentos, Qohor, Norvos x2,7, Lys, Tyrosh, Myr x1,8,
  Wyspy Letnie 6 -> 37 (x6,3; Lotus Bay - najbiedniejsze miasto, ale jedyny rynek 0,75 mln ludzi wysp); Zelazne Wyspy, Dragonstone,
  Wolni Ludzie, Ibben, Sarnor, Nocna Straz, Skagos, Yi Ti - x1,0. Przyklady miast: Highgarden 37 -> 225, Qarth 47 -> 223, Winterfell 39 -> 61.
- **Wspolczynnik tradycji rzemiosla wedlug kultury** (ironborn w gore, Wyspy Letnie w dol) - NIE w 174: `max` juz chroni male krolestwa,
  a Lotus Bay robi bron swojej kultury (luki - zgodnie z kanonem). Wraca w audycie armii krolestw (zadanie 09.10, rozdz. 9).

**(B) Kopalnie x1,5** (`MineOutputStep` = 1,5; mnoznik NA `MineOutputMultiplier` w `MaterialLaw.cs:172`): 169 -> ok. 254 ladunkow/d [S].
Zelazo na bron po kroku nadal 3-15 razy ponizej historii [S z H]. Wariant B Jeffa: "ruda razem z rekami".

**(C) Las wsi x1,6 + koniec gnicia surowcow trwalych w BK.**
- `WoodlotStep` = **1,6** (mnoznik NA `VillageWoodlot.Rate`, VillageWoodlot.cs:54): 2,5 -> 4,0 ladunku na wies, ok. +770 ladunkow/d [S].
  Stopien z decyzji 05.10 "x1.3 / x1.6 / x1.9 razem z popytem" wybrany wedlug popytu: drewno zbrojnych i strzelarzy rosnie z ok. 490 do ok.
  1 100-1 250/d (+55-65% calego zuzycia drewna swiata), a las wsi to ok. 82% produkcji. Sprawdzone przy weryfikacji: x1,3 dawalby na bazie roku
  T3 (budowy 579/d) ok. -200/d, x1,6 ok. +100..+250/d (4.5). Wegiel kuzni jest odbiorca (ok. 6 ladunkow drewna na ladunek rudy). W pierwszych
  miesiacach nowej kampanii (malo budow) drewno bedzie rosnac szybciej - hamuje je cena drewna (zysk wsi i wozu).
- **BK `DeleteOverProduction`** (BKSettlementBehavior.cs:333: 2%/d ze stosow towarow handlowych > 500, bez uzbrojenia i jedzenia) - audyt 03
  P1-D, tu jako czesc 174.3 (`BkRawNoRot`): prefiks - ruda, metale, narzedzia, skora, plotno 0%; drewno, len, welna 0,2%/d ze stosow > 500
  (warunek BK zostaje, stawka 10 razy nizsza - gnicie drewna musi wyjsc NIZSZE niz dzis przy tym samym zapasie) (drewno na skladzie
  bez dachu [S]). Dzis drewno 9/d (A171 d31-40), 179/d w roku T3 [P] - wiecej lasu bez tego = wiecej drewna w nicosc (wersja 1 przesuwala
  to za krok lasu). Zawor produkcji wsi (K13) - nadal przed S4.
Po paczce 133 (wytop przy kopalni) las wraca w dol (K13: 2,5 -> ok. 2,2 po 133).

**(D) Petla cyklu** (`WorkshopPiecesPerCycleMax` = 64 zamiast stalej 8, WorkshopLaw.cs:254): petla idzie, poki robota starcza na nastepna
sztuke. Przy rekach x2 linie szybkich sztuk w duzych miastach dochodza do ok. 10 sztuk na wywolanie (Qarth: miecznik ok. 11 roboczodni na linie
x wlocznia t1 1 dzien) [S].

**(E) Co rosnie razem z rekami, a co NIE [K]:**
- ROSNIE (swiadomie, z materialem): rzemioslo miasta 148 (`TownCraftHandsPerArmsHand` 2, TownCrafts.cs:282 - zajete dzis 40%, wiaze surowiec
  i zysk, nie rece; 174.2 dowozi len i skory), strzelarze 172 (0,3 x, TownFletchers.cs:385 - zajeci 49-61%), godziny napraw kowali
  (SmithHours.cs:39 - wiecej napraw = wiecej rudy na naprawy, 4.5).
- **NIE ROSNIE - ColdStart** (`ColdStartLegacyHands` = true): `ColdStart.Markets` (ColdStart.cs:153-186) dopisuje na polki `60 dni x TownHands`
  roboczodni sztuk przez `AddToCounts(it, 1)` - bez rudy, drewna, zlota i warsztatu. Przy nowym wzorze zapas z niczego urosnalby z 44 087
  (korpus 14 983) do ok. 90 tys. sztuk w kazdej nowej kampanii - wbrew decyzji 09.10 (wersja 1 nazywala to "dorobek, raz" - blad). ColdStart
  liczy rece starym wzorem (nowa metoda `WorkshopLaw.LegacyTownHands`); wiekszy dorobek tylko jako osobne pytanie z liczba sztuk (pytanie 6).
- **NIE ROSNIE na czas 174.3 - cel zapasu karawan i prog nadwyzki** (`CaravanBulkLegacyHands` = true): `CaravanBulk.Use` (CaravanBulk.cs:223)
  = rece x PerHand (ruda 0,025) + stala; Target = 10 dob x Use (min. 8 ladunkow), Keep = 2 x Target (:297-303). Z nowymi rekami suma celow rudy
  827 -> ok. 1 630, suma Keep 1 654 -> ok. 3 260, a w miastach lezy 2 244 ladunki (d40) [S] - duze miasta stalyby sie odbiorcami, a zrodla
  174.2 i skup karawan by zniknely. Use czyta stare rece; docelowo (rozdz. 9) Use z zmierzonego zuzycia, nie z rak.

### 3.5 Co dalej (NIE w 174 - warunki wejscia nastepnych krokow)

Historyczne minimum 0,32 roboczodnia na 1000 obejmuje amunicje; w grze strzelarze rosna razem z rekami (x1,3) i dochodza warsztaty notabli,
wiec wspolczynnik rak broni dla minimum to ok. 0,25 (0,25 x 1,3 = 0,33), nie 0,33 (wersja 1 - blad: S5 = 0,33 dawalo z amunicja ok. 0,44,
czyli powyzej minimum).

| Krok | Co | Warunek wejscia (z autotestu) | Uzasadnienie |
|---|---|---|---|
| S4 | `WorkshopHandsPer1000People` 0,10 -> 0,15 (z amunicja ok. 0,2); kopalnie x2 wzgledem dzis; las bez zmiany (x1,6) albo x1,9 wedlug bilansu | po paczce 133 (wytop przy kopalni), P7 (narzedzia wedlug zuzycia) i zaworze K13; "brak rudy" < 20% rozpoczetych sztuk przez 7 dob; zapas rudy swiata nie spada > 30/d | historia.md P1; bez 133 dymarki w miastach zjadaja drewno |
| S5 | 0,15 -> **0,25** (z amunicja ok. 0,33 = historyczne minimum); kopalnie x3-4; las x1,9 (po 133 nizej) | po S4 + K13 132 (len, welna, skory x1,3); drewno bez braku w > 90% miast | 0,32-0,64 roboczodnia/1000 [S z H] |

### 3.6 Gracz - jedna regula z AI (nowe)

- **Komplet DTE rekruta gracza** [K]: `RecruitKit.OnRecruited` (RecruitKit.cs:464) - "gracz: komplet DTE bez zmian" (:26). Kazdy rekrut gracza
  dostaje pelny wzorzec z niczego; AI od 171 nie. Decyzja Jeffa 05.10 (STAN-PRAC.md:354, potwierdzona :448): "rekrut od tieru 2 tylko z tym,
  co kupil mu notabl (jak AI) - koniec darmowego kompletu". "Otwarte sprawy 2" (STAN-PRAC.md:575) jest nieaktualne. **Propozycja: paczka 175
  "Werbunek gracza jak AI" rownolegle do 174** (te same sciezki co AI: zapis notabla, "z tym, co ma", dobytek t1; ColdStart dla pul notabli
  juz jest) - nie pytanie, tylko wdrozenie decyzji. Do tabeli 1.3: strumien gracza z linii "Komplet rekruta" (nowe pole "gracz").
- **Asymetria w bitwie:** bitwy AI z AI nie patrza w zbrojownie (171 rozdz. 1.4), a niedozbrojenie AI dziala tylko w bitwach z graczem: DTE
  daje AI -2 morale za kazdy brakujacy tier, najwyzej -20 (PartyEquipmentDistributor.cs:78, :1904-1936). Do linii bitwy gracza: rozklad
  kary Underequipped u przeciwnikow AI (ilu ludzi z kara 0 / 2-10 / 12-20).
- **Zaopatrzenie BK gracza** (bron, tarcze, strzaly) - 3.1 (c), pytanie 5.

### 3.7 Czego swiadomie NIE robimy

- Skracania dni pracy i "szybszych kowali" - wydajnosc jest historyczna (2.8).
- Sprzetu ze wzorca, dosypki rudy, przerzutu surowca bez partii, "pomocy" dla AI, wiekszego dorobku startowego.
- Zakupu gorszej zbroi i dowolnej broni przez AI bez decyzji Jeffa - to zmiana rozgrywki (pytanie 2).
- Zmiany wag cechow (Paryz 1292 = historyczny udzial rak "wlasnych"); zwolnione rece ida wedlug braku (3.2 A).
- `HistAmmoLaborMultiplier` 8 -> 4-5: strzelarze zajeci w 49-61% - reka nie wiaze; wrocic, gdy zajecie > 90% (rozdz. 9).
- "Dobytek osady" jako policzony zapas (historia.md P3) - strona popytu i zamiana reguly 92; osobna paczka (rozdz. 9).
- Wspolczynnika tradycji rzemiosla wedlug kultury (3.4 A).

---

## 4. Przewidywany bilans po zmianie [S]

### 4.0 Model

Kalibracja = pomiar A171 d31-40 (bilans.md 2.2). Ruda na sztuke dla mieszanki robionej, **przeskalowana do zmierzonych 79-81 ladunkow**
(warsztaty zbrojne 67,9-69,2 + strzelarze 11,0-11,4): korpus 0,48, helm 0,11, bron biala 0,10, kusza 0,22, rekawice i tarcze 0,035, snop strzal
0,06, beltow 0,09 (wersja 1: 0,55 / 0,13 / 0,12 / 0,25 / 0,04 / 0,07 / 0,1 - suma dawala 92, o 14% za duzo). Metal (korpus, helm, rekawice,
bron biala, kusza) dzieli budzet rudy, reszta (tarcze, luki, nogi, plaszcze, przeszywanice) - nie. Budzet rudy dla zbrojnych + strzelarzy:
dzis 79; po 174.0-174.2 najwyzej ok. 110 (79 + nadwyzka 37 minus straty kontraktow w drodze); po 174.3 ok. 175-195 (+85 z kopalni x1,5,
minus wzrost napraw, minus straty w drodze). Popyt bez wzrostu = 1.1a. Rozrzut "dol-gora" = niepewnosc mnoznikow wyboru i przejscia rak (+-30%).

### 4.1 Produkcja wedlug koszyka (szt./dobe)

| Krok | Korpus | Helm | Rekawice | Nogi* | Tarcza | Bron biala | Luk | Kusza | Strzaly | Belty | Plaszcz+uprzaz | **Razem** | Ruda/d |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Dzis A171 d31-40 [P] | 51 | 140 | 49 | 237 | 59 | 210 | 62 | 6 | 153 | 39 | 61 | **1 067** | 79 |
| 174.0 (znikanie) | 51 | 140 | 49 | 237 | 59 | 210 | 62 | 6 | 153 | 39 | 61 | 1 067 (+0,6-0,7 tys./d nie ginie) | 79 |
| 174.1 sama | 55-75 | 135-150 | 50-60 | 200-260 | 60-80 | 200-230 | 65-85 | 6 | 150-175 | 38-45 | 60-80 | **1 020-1 250** | 79-95 |
| 174.1 + 174.2 | 75-100 | 140-155 | 55-65 | 180-240 | 80-120 | 215-240 | 70-90 | 6-8 | 160-185 | 40-45 | 55-75 | **1 080-1 320** | ok. 110 (limit) |
| 174.3 | 120-190 | 200-230 | 85-100 | 250-350 | 150-240 | 340-375 | 130-170 | 10 | 280-360 | 70-90 | 70-100 | **1 700-2 220** | 175-195 |
| S4-S5 (poza 174) | 330-400 | 520-600 | 230-260 | 650+ | 450-750 | 900-1 000 | 350-480 | 25 | 750-1 000 | 190-250 | 200-280 | 4 600-5 900 | 480-560 |

\* Nogi bez przedmiotow cywilnych (ladys_shoe) od 174.1 - "spadek" to przesuniecie rak krawcow, nie strata dla wojska.
Sumy policzone z kolumn (wersja 1 miala rozjazdy: 1 870-2 230 przy sumie kolumn 1 845-2 320). Korpus po 174.3 = metal (ok. 90-130, limit rudy)
+ przeszywanice i skorznie (ok. 30-60; plotno i skora z 174.2 i 148 - 4.5). **Ruda wiaze metal od 174.1** - rece samej (174.3 A) bez kopalni (B)
dalyby tylko tarcze, luki, przeszywanice i strzaly. S4-S5 przeliczone na wspolczynnik 0,15 / 0,25 (3.5).

### 4.2 Pokrycie popytu bez wzrostu armii (produkcja po 174.3 / popyt z 1.1a) i osobno strumien wzrostu

| Armia | Korpus | Helm | Rekawice | Tarcza | Bron razem | Strzaly, belty |
|---|---|---|---|---|---|---|
| 160 tys. (test, d40) | 34-53% | 58-67% | 45-53% | 59-94% | 100% (zle tiery - pytanie 2a) | 100% |
| 108 tys. (decyzja 05.10) | 50-79% | 86-99% | 66-78% | 87-100% | 100% | 100% |
| S5 przy 160 tys. | 93-100% | 100% | 100% | 100% | 100% | 100% |

**Strumien wzrostu armii (osobno):** przy +1,1 tys. ludzi/d (d31-40) ok. 2,0 tys. sztuk/d braku przybywa niezaleznie od produkcji
(nowy czlowiek ma tylko dobytek t1). 174 tego nie pokrywa i nie ma pokrywac - to sprawa liczby wojska (160 tys. w tescie wobec ok. 108 tys.
z decyzji 05.10; do sprawdzenia, czy 108 tys. obejmowalo zalogi - rozdz. 9) i, historycznie, zapasu domow (historia.md 5.2-5.3:
produkcja pokrywala 10-25% fali zaciagu).
Popyt bez wzrostu jest gorna granica dla obecnych ludzi, ale zanizony przez hamulce awansow (1.1a) - po 174 awanse odblokuja sie czesciowo
i popyt wzrosnie; dlatego progi testu stawiamy na pogorszenie pokrycia, nie na sam procent.
**Stan partii AI po 120 dobach [S]** (miara szczebla): bron glowna 80-88%, helm 40-50%, tarcza 35-45%, korpus 50-58% (t3+ 27-33%). To NIE
jest "w pasmie historii" (wersja 1 - blad: helm 60-90% i bron prawie 100% w historii to odsetek ludzi z czymkolwiek, a nasze liczby to
szczebel). Porownanie z historia - na mierze "dowolny szczebel" po pierwszym tescie 174.0; jesli "cokolwiek na tulow" wyjdzie < 75%
albo "jakakolwiek bron" < 95% - pytanie 2 ma podstawe w liczbach.

### 4.3 Nowa kampania - dni do zera polek i stan ustalony (po 174.3, ColdStart BEZ zmiany)

Odplyw z polek przy DOSTEPNYM towarze (produkcja - zmiana polki, `odplyw.py`) [P]: korpus d2-10 582/d, d11-20 249, d21-30 409, d31-40 195
(towar sie konczy); helm d2-10 352, potem 144-181 (dno); tarcza d2-10 160, potem 60-71. Po 174.0 czesc tego odplywu (znikanie) zostaje na
polce - udzial nieznany, przyjmuje 10-25% [S].

| Koszyk | Zapas d1 (ColdStart, bez zmiany) | Odplyw/d przy towarze (po 174.0) | Produkcja/d po 174.3 | Do zera dzis [P] | Do zera po 174 [S] | Stan ustalony |
|---|---|---|---|---|---|---|
| Korpus | 13 954 | 310-520 | 120-190 | ok. 48 dob | **ok. 35-115 dob (1-4 miesiace)** | polka na dnie, produkcja = zakupy (34-53% popytu bez wzrostu przy 160 tys.) |
| Helm | 2 773 | 260-350 | 200-230 | ok. 20 dob ("dno") | ok. 20-90 dob | polka cienka, popyt pokryty w ok. 60% |
| Tarcza | 1 538 | 120-160 | 150-240 | ok. 23 doby ("dno") | nie spada albo powoli | polka rosnie do progu ceny |
| Bron biala | 17 396 | 300-650 (d2-10 1H 654) | 340-375 | (1H rosnie od d11 - zle tiery) | 1H: nie spada | nadmiar t1-2, brak t3+ (pytanie 2a) |
| Strzaly | 1 563 (+4 157 z karawan po 172b) | 95-170 | 280-360 | ok. d28 ("dno") | nie spada | polka rosnie do progu ceny |
| Nogi (bez cywilnych) | ok. 1 000 | 260-370 | 250-350 | ok. 75 | ok. 60-200 | - |

**Ile z tego to produkcja, a ile zapas:** zapas startowy jest ten sam co w A171, wiec cala roznica (korpus: 48 -> 35-115 dob) to
produkcja i zamkniete ujscia. Wersja 1 obiecywala "4-18 miesiecy" - polowa tej obietnicy pochodzila z podwojonego darmowego zapasu ColdStart
(3.4 E), a odplyw liczyla z dna polek (250-350/d) zamiast przy dostepnym towarze (409-582/d) - oba bledy poprawione.
Brak w zbrojowniach AI (A171: +2 353/d w d31-40, z tego ok. 2 040/d nowi ludzie): po 174.3 pogorszenie obecnych ludzi (ok. 309/d dzis)
spada do ok. 0-100/d [S]; skladnik wzrostu zostaje, dopoki armie rosna. Bitwy AI z AI nie patrza w zbrojownie (171 rozdz. 1.4).

### 4.4 Kampania Jeffa (ok. 1 mln sztuk na polkach) po 171 + 174

| Koszyk | Zapas | Odplyw po 171 (zakupy AI bez darmowych kompletow) | Produkcja po 174.3 | Starczy na |
|---|---|---|---|---|
| Korpus (glownie t1-t2) | 254 tys. | ok. 500-700/d | 120-190 | ok. 1-2 lata dla t1-t3; **t4+ od pierwszego dnia z produkcji** |
| Bron 1H | 384 tys. | ok. 300-400/d (wiecej przy pytaniu 2a) | ok. 0 w nadmiarze (bramka ceny - rece ida do koszykow z brakiem) | 3+ lata |
| Nogi | 295 tys. | ok. 50-250/d | malo (nadmiar) | wiele lat |
| Helmy, tarcze, luki, amunicja, rekawice | razem kilka tys. ("dno") | 600-1 000/d | 600-850/d | **od pierwszego dnia z produkcji** (2 x wiecej niz dzis) |

Skutki w trwajacej kampanii [S]: (1) zamkniete znikanie zatrzymuje ok. 0,6-1,0 tys. sztuk dziennie, ktore dzis zjadaja stos - **bez
decyzji stos ok. 2/3 "z niczego" zostaje na stale** (pytanie 4: (a) zostawic albo (b) nadmiar ponad rok popytu koszyka powoli na zlom
- 1% nadmiaru dziennie, przez kowali miasta droga wrakow 158, z zaplata ok. 7-14% ceny nowej jak stare kolczugi w latach 1330. [H] historia.md 6;
zlom to metal dla kowali w miastach bez rudy - nic w nicosc, nic z niczego); (2) konserwacja zapasu (3.1 e) kosztuje ok. 1% rak swiata;
(3) koszyki w nadmiarze maja cene na dnie (0,25) - warsztaty tych koszykow staja (zysk), a ich rece (174.1) ida do koszykow z brakiem;
(4) kontrakty rudy ruszaja od pierwszej doby (zapas rudy swiata po roku T3: 12 651 ladunkow); (5) zbrojownie zalog AI odtworzone raz przy
pierwszym wczytaniu (171 C9a); (6) robota w toku od 174.0b przetrwa zapis; (7) liczba ludzi Jeffa w ksiedze (ok. 90 mln) nie zmienia rak -
174.3 liczy z tabeli krain.

### 4.5 Surowce po krokach (ladunki/dobe; skora i plotno - sztuki 10 kg) [S]

| | Dzis (A171 d32-40 [P]) | Po 174.1-174.2 | Po 174.3 | Uwagi / prog testu |
|---|---|---|---|---|
| Ruda - wydobycie | 169 (+8 inne) | 169 | ok. 254 | `MineOutputStep` 1,5 |
| Ruda - zbrojne + strzelarze | 79 | ok. 105-110 | 175-195 | limit = to, co jest |
| Ruda - narzedzia, BK, naprawy, przepadlo, inne | 24 + 9 + 3,6 + 15,8 + 5 | + straty kontraktow 2-5 | naprawy ok. 7 (SmithHours x2) | P7, rozbite partie - rozdz. 9 |
| Ruda - bilans swiata | +37 | ok. 0..+10 | -15..+10 | prog: >= -30/d |
| Suma celow rudy miast (CaravanBulk) / Keep | 827 / 1 654 | bez zmian | bez zmian (stare rece, 3.4 E) | prog: +-5% A171; bez `CaravanBulkLegacyHands` byloby ok. 1 630 / 3 260 |
| Drewno - zbrojne + strzelarze | 489 | ok. 650-700 | ok. 1 100-1 250 | 6 ladunkow drewna na ladunek rudy |
| Drewno - las wsi | 1 280 | 1 280 | ok. 2 050 (x1,6) | `WoodlotStep` 1,6 |
| Drewno - BK gnicie | 9 (rok T3: 179) | 9 | ok. 1-5 (0,2%/d stosow > 500) | prog: <= 9/d (A171 d31-40) |
| Drewno - bilans swiata | +470 (d1-40 +345; rok T3 +109) | ok. +250..+330 | rok T3 (budowy 579/d): ok. +100..+250; d1-40 nowej kampanii: ok. +300..+450; d31-40: do +600 | prog: >= -50/d; x1,3 dalby na roku T3 ok. -200/d |
| Skora - produkcja / zuzycie | 91 / 86 (zbrojne 26) | 95-105 / 95-105 (zbrojne 35-40) | 120-140 / 120-140 (zbrojne 55-60) | garbowanie 148: dzis 55 miast "bez zysku", 18 "brak surowca" - cena skory rosnie z popytem, skory surowe (+168/d w swiecie) dowozi 174.2; prog: bilans >= -10/d |
| Plotno - produkcja / zuzycie | 91 / 80 (zbrojne 36) | 100-115 / 95-110 (zbrojne 50-60) | 130-160 / 130-160 (zbrojne 75-85) | tkanie 148: dzis 62 miasta "brak lnu", len +171/d w swiecie - dowozi 174.2; prog: bilans >= -10/d, miast bez plotna <= 60 (A171) |

Dlaczego tkanie i garbowanie "x2 rak" nie jest dzwignia (wersja 1 - blad): rzemioslo 148 ma rece zajete w 40% [P]; wiaze je len i skory
surowe w zlym miejscu (62 i 18 miast) oraz zysk (55 miast garbowania "bez zysku"). Dzwignia to dowoz lnu i skor surowych (174.2) i cena.
Skorznia t3 to ok. 0,9 sztuki skory (9 kg) - z dzisiejszej nadwyzki skory (+5/d) wychodzi ok. 5 skorzni dziennie (wersja 1: "+15-20" - blad).

---

## 5. Pliki, metody, klucze MCM, linie logu

Po kazdej zmianie `Settings.cs`: `python tools/gen_mcm.py` w drzewie ("python3" to atrapa Sklepu). Build:
`cd "<drzewo>/Armoury" && dotnet build Armoury.csproj -c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs" > build.log 2>&1; echo rc=$?`.
Klucze MCM - jeden zwarty blok na koncu pol `Settings.cs` (przed `Load`), opisy po angielsku (napisy w grze - CLAUDE.md rozdz. 1).
Linie logu po polsku bez polskich znakow, raz na dobe (jak "Warsztaty: dzien").

### 5.1 174.0

| Plik | Metoda / miejsce | Zmiana |
|---|---|---|
| `HistoricalPrices.cs` | `TownUse` (:812-827), `BudgetPostfix` (:792-810; wpiecie na BK `CalculateBudget` :959-960) | uzbrojenie (predykat jak `GuildOf`) = 0 przy `ArmsNotHouseholdGoods`; dziala tez przy wylaczonym `TownHouseholdUse` |
| NOWY `ArmsLeaks.cs` | `DecayPrefix(Town __0, ...)` na `ItemConsumptionBehavior.DeleteOverproducedItems`; `ApplyAll(Harmony)`, `Reset()`, liczniki dnia | petla gry bez `Equipmentish` (bez koni); wyroby gracza - jak gra, liczone; licznik stosow |
| `BkSupplyTemper.cs` | `WeaponsZeroPostfix`, `ShieldsZeroPostfix`; `ArrowsBuyPrefix` -> `NeedsBuyPrefix`; `ApplyAll` | wzor 172; gracz wedlug `BkSuppliesNoArmsPlayer` |
| `GoodsLedger.cs` | podsluch `AddToCounts` (galaz uzbrojenia wedlug `ItemTypeEnum` i tieru), ramka "kasowanie gry", `Flush` | linia "Uzbrojenie (ujscia 174)" |
| `ArmsDrill.cs` | petla linii "Pokrycie" (:337-359) | miara "dowolny szczebel" (3.1 f) |
| `SmithHours.cs` / `WorkshopLaw.LineShare` | odjecie rak napraw | + konserwacja zapasu (3.1 e) |
| `ArmouryBehavior.cs`, `SubModuleMain.cs` | `ArmsLeaks.ApplyAll`, `Reset` w konstruktorze | zwarte wstawki |
| `tools/p174_reszta.py` | NOWY (z `SCR\p174\skrypty\reszta.py`) | rachunek reszty polek w dziesiatkach dob |
| `Settings.cs` | blok 174.0 | klucze nizej |

| Klucz | Typ / domyslnie | Opis MCM (EN) |
|---|---|---|
| `ArmsNotHouseholdGoods` | bool / true | Townsfolk do not use up arms, armour, shields and harness: these lie on the stalls until a soldier, a notable or a trader buys them (everyday clothing is still worn out by the townsfolk). |
| `ArmsNoStallDecay` | bool / true | The game no longer deletes a piece of arms or armour with a quality modifier from town stalls each day: armour does not rot on a stall - its wear is counted by its condition. Horses keep the game's rule; pieces forged by the player are still cleared from stalls (they are unique save objects). |
| `ArmsStallUpkeepManDaysPerPiece` | float / 0.02 | Man-days a year a town's smiths spend cleaning and oiling each piece of arms on its stalls (the Tower, 1340s: 11 men oiled 3,200 bows in 5 days) - keeping stock is work, not free. |
| `BkSuppliesNoArms` | bool / true | Banner Kings supplies of AI parties no longer buy and use up weapons and shields: the wear of an AI army's weapons is counted by Armoury (battle wear and repairs) - one rule, not two. |
| `BkSuppliesNoArmsPlayer` | bool / (odpowiedz na pytanie 5; rekomendacja true) | The same for your own party: weapons, shields and arrows wear out by Armoury's rules only, not also by Banner Kings supplies. |

Linie logu:
- start: `ArmsLeaks (174.0): mieszczanie bez uzbrojenia CZYNNE|wylaczone (BK CalculateBudget wpiety|BRAK; miasta i zamki); kasowanie 5% stosow uzbrojenia WYLACZONE (latka wpieta)|BRAK latki; BK bron i tarcze partii AI = 0 - wpiete|BRAK (zerowanie zapisanej potrzeby: wpiete|BRAK); gracz: BK jak dotad|= 0; konserwacja zapasu X roboczodnia na sztuke na rok.`
- dzien: `Uzbrojenie (ujscia 174): dzien N - mieszczanie: uzbrojenie X szt., odziez (garment) Y szt.; kasowanie gry: uzbrojenie X szt. (pominieto Z stosow, oczekiwane ok. W szt.), wyroby gracza U szt., konie V szt.; zaopatrzenie BK: partie AI bron X / tarcze Y (potrzeb wyzerowanych Z), gracz bron A / tarcze B; przepadlo z rozbitymi partiami X; inne ticki X; wedlug typu i tieru [BodyArmor t1..t6 ..., ...]; stosy uzbrojenia na polkach S (zmiana +s); konserwacja K roboczodni.`
- w "Pokrycie zbrojowni AI (171)": `| dowolny szczebel: korpus X%, glowna bron Y%, helm Z%, tarcza W%, rekawice R%, nogi N% (ludzi z czymkolwiek)`.

### 5.1b 174.0b

| Plik | Metoda / miejsce | Zmiana |
|---|---|---|
| `WorkshopLaw.cs` | nowe `Export()/Import()/ResolvePending()`; `Reset` bez zmian (nowa kampania) | `_wip`, `_owed`, licznik wip miasto -> koszyk (od 174.1) |
| `SupplyDemand.cs` | `Export()/Import()` dla `_unmet` (:96) | zamowienia |
| `TownFletchers.cs`, `TownCrafts.cs` | jesli dlugi tylko w pamieci - to samo | - |
| `ArmouryBehavior.cs` | `SyncData` (:391-502) | `arm_workshops` przez `SaveText.Sync`; `ResolvePending` po wczytaniu (obok `RecruitKit.ResolvePending`, :988) |
| `OreLedger.cs`, `GoodsLedger.cs` | `Note...` | "przepadlo przy wczytaniu" (surowiec pominietej sztuki) |

Klucz: `WorkshopStateInSave` (bool / true) - "Work in progress at town workshops (pieces begun, raw material already bought, unmet orders) is kept in the save, so a long piece of armour survives saving and loading."
Linia po wczytaniu - 3.1b.

### 5.2 174.1

| Plik | Metoda / miejsce | Zmiana |
|---|---|---|
| `WorkshopLaw.cs` | `_rank` (:340) | pozycja rankingu: przedmiot, koszyk, zysk/dzien, przychod, koszt, potrzeby, dni, d, s |
| | `Candidates` (:344-401) | **filtr :370 bez zmian**; przedmioty cywilne bez zolnierza pomijane; (B) kolejnosc `W_b x zysk/dzien`; (C) co druga sztuka t1-t3 w koszyku z brakiem - najmniej dni |
| | `Revenue` (:463-471) | oddaje d i s z `SupplyDemand.Factor` (bez nowych wywolan) |
| | `LineShare` (:521-550), `_guildCache` (:413) | (A) czynna = sztuka w robocie albo start mozliwy dzis z (E); zwolnione rece cechow wedlug braku; (D) linie wedlug braku tylko wsrod czynnych, podloga dla zaczetych, suma = rece cechu |
| | `CyclePrefix` petla startu (:261-303), bank (:247) | (E) `dni <= w.Labor + max(udzial, h/n) x WorkshopPlanDays`; licznik wip miasto -> koszyk |
| | `Flush` (:572-588), `FlushDiag` (:636-662) | linie nizej |
| `ColdStart.cs` | `Markets` (:142) | pomija przedmioty cywilne bez zolnierza |
| `Settings.cs` | blok 174.1 | klucze nizej |

| Klucz | Typ / domyslnie | Opis MCM (EN) |
|---|---|---|
| `WorkshopHandsFollowMaterial` | bool / true | A workshop's hands go only to lines that can work today - a piece already in hand, or the ore, wood, leather and cloth for a new one on the town's stall. |
| `WorkshopFreedHandsByShortage` | bool / true | Hands of a craft that cannot work today (no ore, no cloth) help the other crafts of the town according to what the town lacks most - not by the fixed shares of Paris 1292. |
| `WorkshopChooseByShortage` | bool / true | Workshops first make what the town lacks most (empty stalls and unmet orders), and only then what pays best - the price cap no longer hides which shortage is worst. Civilian clothes that no soldier wears are left to the tailors of the town, not the armourers' lines. |
| `WorkshopMunitionGrade` | bool / true | When armour of tier 1-3 is short, every second piece begun is the quickest of that kind and tier - munition harness, as Paris made 500 sets in under three months in 1384 beside its better work. |
| `WorkshopMunitionMaxTier` | int / 3 (1-6) | Highest tier made "for munition" when short. |
| `WorkshopMunitionShare` | float / 0.5 (0-1) | Share of the pieces begun in a short kind that are made for munition. |
| `WorkshopLineShortageShare` | float / 0.75 (0-1) | Share of a craft's hands that follow the shortage of its lines; the rest is spread evenly, and a line with a piece in hand keeps at least an even share until it is finished. |
| `WorkshopPlanDays` | float / 60 (14-120) | A workshop starts no piece it cannot finish within this many days with an even share of its craft's hands, and banks no more than this many days of work. |

Linie logu (dopisane do istniejacych):
- `Warsztaty: dzien N - ... | rece: X roboczodni (liniom Y, bezczynne Z), cechy [krawiec a/a', platnerz b/b', miecznik c/c', siodlarz d/d', lucznik e/e', tarczownik f/f' (przydzielone/zuzyte)]; rece przeniesione z cechow bez roboty: R (do krawcow r1, platnerzy r2, miecznikow r3, siodlarzy r4, lucznikow r5, tarczownikow r6); kontrola: suma udzialow linii = rece cechow (odchylenie max E%); wybor: koszyki z brakiem K, sztuk "na amunicje" M (srednie q wedlug tieru [t1 q1, t2 q2, t3 q3]), kandydatow odrzuconych "nie skonczy w planie" N; zrobiono t5-6: U szt.; cywilnych pominietych C.`
- `Warsztaty (diagnoza): najwiekszy brak - [BodyArmor t3: popyt D, polka S, w toku W, zrobiono dzis M; ...] (6 koszykow swiata).`

### 5.3 174.2

| Plik | Metoda / miejsce | Zmiana |
|---|---|---|
| NOWY `MaterialOrders.cs` | `Daily()`, `Place(...)`, `OnEntered(...)`, `OnLost(...)`, `Release(...)`, `Export()/Import()/ResolvePending()`, `Reset()`, liczniki | regula 3.3 |
| `WorkshopLaw.cs` | obok `_skipMatBy[m]++` (:307) | `_skipMatTown[town][m]++` (+ `Reset`, czyszczenie co dobe) |
| `TownFletchers.cs`, `TownCrafts.cs` | obok `_dMissBy` / koniec przerobu "brak surowca" | to samo (len, skory surowe, welna dla 148) |
| `CaravanBulk.cs` | `Trades` (:200-205), `Keep` (:297-303) | przepuszcza karawany z kontraktem; `internal` odczyt Keep wedlug starych rak |
| 172b (karawany bez amunicji) | warunek karawany | przepuszcza karawany z kontraktem |
| `OreLedger.cs`, `GoodsLedger.cs` | posiadacz "karawany" (istniejacy) | licznik kontraktow i strat w drodze |
| `MoneyLedger.cs` | `Note` | pozycja "kontrakty surowca (174)" (kasy osad <-> karawany) |
| `ArmouryBehavior.cs` | `OnDailyTick` po `SupplyDemand.DailyTrade` i `GarrisonCarts.Daily`; `OnSettlementEntered`; zniszczenie partii; `SyncData` | wywolania; zapis `arm_matorders` |
| `Settings.cs` | blok 174.2 | klucze nizej |

| Klucz | Typ / domyslnie | Opis MCM (EN) |
|---|---|---|
| `TownMaterialOrders` | bool / true | A town whose smiths, fletchers, weavers or tanners stood idle for want of a raw material hires a caravan in a nearby friendly town that has more than it needs: the caravan buys the load there at the market price and carries it by road - a real party on the map that bandits can rob - and sells it to the town on arrival. |
| `TownMaterialOrderRangeOre` | float / 200 | Farthest (map distance) iron ore is carried - two or three days. Ore travelled short; iron came from afar as bars. |
| `TownMaterialOrderRange` | float / 600 | Farthest timber, leather, cloth, flax, hides and wool are carried - about ten days on the road. |
| `TownMaterialOrderDays` | int / 3 | A town orders the same raw material at most once in this many days. |
| `CarterPencePerKgPer100` | float / 0.0375 | Cost of carrying one kilogram 100 leagues by road, which the caravan must earn before it takes the order (about 1.5 pence a ton a mile in 14th-century England). |
| `SeaFreightShare` | float / 0.25 | Cost of a sea leg as a share of the same distance by road. |

Linia logu:
- `Kontrakty surowca (174): dzien N - zawarto A [ruda a1, drewno a2, skora a3, plotno a4, len a5, skory surowe a6, welna a7 sztuk] za G d towaru (oczekiwana marza karawan M d); dojechalo B (srednio D dob drogi), w drodze C, rozbite/pojmane L (sztuk l), zwolnione E (wrogosc X, 30 dob Y); brak karawany w zrodle K, brak zrodla w zasiegu H, bez zysku I [3 przyklady: miasto - zrodlo, odleglosc, surowiec]; miast bez rudy J z 97, bez lnu dla tkaczy P, bez skor dla garbarzy Q.`

### 5.4 174.3

| Plik | Metoda / miejsce | Zmiana |
|---|---|---|
| `WorkshopLaw.cs` | `TownHands` (:505-510), `Hands` (:512-518) | wzor 3.4 (A) przy `WorkshopHandsByPeople`; `max` ze starego; nowa `LegacyTownHands` (dzisiejszy wzor) |
| | nowy `MarketPeople(Town)` | `TablePeopleOf(miasto)` + wsie z `TradeBound == miasto`; pamiec dnia |
| | `CyclePrefix` (:254) | `guard < WorkshopPiecesPerCycleMax` |
| `PopulationLaw.cs` | nowa `TablePeopleOf(Settlement)` | wspolczynnik z dzisiejszych sum kultury, suma = `Table` x `PopulationScale`; `_k` i `PeopleOf` bez zmian |
| `ColdStart.cs` | `Markets` (:161) | `LegacyTownHands` przy `ColdStartLegacyHands` |
| `CaravanBulk.cs` | `Use` (:223) | `LegacyTownHands` przy `CaravanBulkLegacyHands` |
| `MaterialLaw.cs` | :172 | `m = s.MineOutputMultiplier * s.MineOutputStep` |
| `VillageWoodlot.cs` | `Rate` (:54) | `x WoodlotStep` |
| NOWY prefiks w `MaterialLaw` albo `FreeSupplies` | BK `BKSettlementBehavior.DeleteOverProduction` | ruda, metale, narzedzia, skora, plotno 0%; drewno, len, welna 0,2%/d ze stosow > 500 przy `BkRawNoRot`; licznik |
| `TownCrafts.cs`, `TownFletchers.cs`, `SmithHours.cs` | bez zmian w kodzie | czytaja `TownHands` - rosna razem (3.4 E) |
| `Settings.cs` | blok 174.3 | klucze nizej |

| Klucz | Typ / domyslnie | Opis MCM (EN) |
|---|---|---|
| `WorkshopHandsByPeople` | bool / true | A town's arms craftsmen are counted from the people of its market - the town and the villages that sell there, as the realm tables give them - and a richer town keeps more masters; no town has fewer than under the old rule. Off: the old rule only. |
| `WorkshopHandsPer1000People` | float / 0.10 | Man-days of arms work a day for each 1000 people of a town's market (fletchers come on top). Estimate from history: 0.3 to 0.6 armourers, weaponsmiths, bowyers, fletchers and saddlers per 1000 people of the realm; raised in steps together with ore and timber. |
| `WorkshopHandsMaxPerTown` | float / 1000 | Safety ceiling on one town's arms craftsmen under the population rule. |
| `ColdStartLegacyHands` | bool / true | The merchants' starting stock of a new campaign is counted from the old number of craftsmen - more craftsmen do not mean more free stock at the start. |
| `CaravanBulkLegacyHands` | bool / true | The stock of ore, timber, leather and cloth a town keeps before it sells to caravans is counted from the old number of craftsmen, so towns with many craftsmen do not stop selling their surplus. |
| `MineOutputStep` | float / 1.5 | Further step of iron ore digging on top of Mine Output Multiplier - more ore together with more craftsmen; still several times less iron for arms than medieval Europe. |
| `WoodlotStep` | float / 1.6 | Further step of timber from village woods on top of Village Woodlot Loads - for the charcoal of the smiths (about six loads of wood for every load of ore); one of the steps 1.3 / 1.6 / 1.9, chosen by the smiths' need. |
| `BkRawNoRot` | bool / true | Banner Kings no longer deletes 2% a day of large stocks of ore, metal, tools, leather and cloth in towns; large stocks of timber, flax and wool lose 0.2% a day instead of 2% (stores without a roof). |
| `WorkshopPiecesPerCycleMax` | int / 64 | Most pieces one workshop line may finish in one cycle of the game (was 8) - a big town's spear-makers are not held back by the counter. |

Linie logu:
- start: `Rece (174): wzor ludnosc rynku (tabela krain) x poziom placy, nie mniej niz dawny wzor - swiat X roboczodni (Westeros a, Essos b; dawny wzor c); 0.10 na 1000 ludzi rynku (ludnosc z tabeli M mln, poza rynkiem N); miasto min/mediana/max p/q/r; na dawnym wzorze s miast; wedlug krolestw [..]; ColdStart i cel karawan na dawnych rekach; suma celow rudy T, Keep K; kopalnie x1.5, las wsi x1.6, BK gnicie surowcow trwalych wylaczone (drewno, len, welna 0.2%)|BRAK; petla cyklu do 64 szt.`
- dzien: w "Warsztaty: dzien" - `rece swiata X (zmiana od startu +Y%)`; w "Towary" - `BK osady (gnicie 0.2%)`.

---

## 6. Testy (autotest; zgoda Jeffa 07.10 - gre uruchamia sesja glowna, wykonawca NIE)

Kazda podpaczka: build kod 0, `gen_mcm.py`, autotest **40 dob nowej kampanii**; porownanie z biegiem poprzedniej podpaczki (te same linie,
skrypty `SCR\p174\skrypty\*` / `tools/p174_reszta.py`, z wejscia). Na koniec calosc (171 + 172 + 172b + 174.0-174.3): **120 dob nowej
kampanii** i **40 dob na zapisie roku** (`-LoadSave autotest-rok-360`). Bledy (CrashScribe, `Log.Error`) = 0 w kazdym tescie.
Progi liczone na srednich 10-dobowych (d31-40, d111-120), nigdy na jednej dobie. Pokrycie - zawsze obie miary (szczebel i "dowolny szczebel").

| Test | Prog (zaliczony, gdy wszystkie spelnione) | Linia |
|---|---|---|
| 174.0 / 40 | mieszczanie: uzbrojenie 0 szt./d; kasowanie gry: uzbrojenie 0 (stosy pominiete > 0); BK partie AI bron 0, tarcze 0 | "Uzbrojenie (ujscia 174)" |
| | reszta skorygowana d31-40 = reszta - "mieszczanie: odziez" - zmiana amunicji w karawanach: \|.\| <= 150 szt./d (A171: -684); podana tez w dziesiatkach dob | `p174_reszta.py` |
| | produkcja +-10% A171; s/dobe <= 1,05 x A171; konserwacja < 0,2% rak swiata; stosy uzbrojenia: liczba podana (baza dla 120 dob) | "Warsztaty", "Uzbrojenie" |
| | miara "dowolny szczebel" w linii "Pokrycie" (wartosci = baza dla pytania 2) | "Pokrycie zbrojowni AI (171)" |
| 174.0b | zapis w d20, wczytanie, 20 dob dalej: sztuk w toku po wczytaniu = przed (+-1%), dlugi i zamowienia odtworzone (+-1%); pominiete sztuki z surowcem w ksiedze "przepadlo przy wczytaniu"; stary zapis (bez klucza) wczytuje sie z pustym stanem | "Warsztaty (zapis 174)" |
| 174.1 / 40 | rece bezczynne <= 10% rak; rece przeniesione > 0 i podzial podany; suma udzialow = rece cechow (<= 0,1%) | "Warsztaty" |
| | wobec biegu 174.0: korpus d31-40 >= 1,1 x; tarcze >= 1,0 x; zrobione t5-6 >= 0,7 x; srednie q sztuk "na amunicje" podane; ladys_shoe i cywilne 0 | "Warsztaty", diagnoza |
| | zapas rudy swiata d31-40 >= -10/d; pokrycie partii AI d40 (obie miary) nie nizsze niz bieg 174.0 o wiecej niz 2 pp | "Ruda", "Pokrycie" |
| 174.2 / 40 | miast bez rudy d40 <= 25 (38); dojechalo >= 80% zawartych; rozbite/pojmane <= 10%; "Towary (bilans)" ZGODNA (ruda, drewno, skora, plotno, len) kazdej doby od drugiej; 0 kontraktow starszych niz 30 dob | "Kontrakty surowca (174)", "Towary" |
| | "brak surowca: ruda" d31-40 <= 500 cykli/d (A171 624-980); tkanie "brak surowca" <= 45 miast (62); bilans skory i plotna swiata >= -10/d; miast bez plotna <= 60 | "Warsztaty", "Rzemioslo miasta (148)", "Ceny surowcow" |
| 174.3 / 40 | rece swiata start 5 700-6 400; 0 miast ponizej dawnego wzoru; rece swiata d40 / d1 <= +2% (tabela stala); Westeros 50-58% | "Rece (174)" |
| | ColdStart zapas kupiecki = A171 +-5% (44 087); suma celow rudy = A171 +-5% (827) | "ColdStart", "Rece (174)" |
| | produkcja d31-40 >= 1 600 szt./d (z amunicja); korpus >= 100, helm >= 180, tarcza >= 120, bron biala >= 300, strzaly >= 230 snopow | "Warsztaty", "Strzelarze (172)" |
| | wydobycie rudy 240-290/d; zapas rudy swiata d31-40 >= -30/d; miast bez rudy <= 25; drewno >= -50/d, BK gnicie drewna <= 9/d (A171), miast bez drewna <= A171 + 5; skora i plotno >= -10/d | "Ruda", "Towary" |
| | pogorszenie pokrycia obecnych ludzi (rozbicie 1.1a, d31-40) <= 150/d (A171 ok. 309); s/dobe <= 1,10 x A171 | "Pokrycie", czas doby |
| Calosc / 120 | pogorszenie obecnych ludzi d81-120 <= 100/d; obie miary pokrycia d120 podane; partie AI (szczebel) d120: bron glowna >= 80%, helm >= 40%, tarcza >= 35%, korpus >= 50% | "Pokrycie" |
| | zamowienia czekajace d120 <= 58 925 (A171 d40); stosy uzbrojenia d120 <= 2 x d40 tego biegu; s/dobe <= 1,10 x | "Rynek broni", handel.log, "Uzbrojenie" |
| | ruda i drewno: bilans d81-120 jak w 174.3; "Towary (bilans)" ZGODNA; kasy miast: srednia zmiana d111-120 >= -30 tys./d (A171 d31-40: +25 tys./d); przeplyw kasa miasta -> warsztaty zbrojne podany wobec zakupow AI | "Ruda", "Drewno", "Przeplywy osad (kasy miast)" |
| Zapis roku / 40 | 0 bledow; s/dobe <= 1,10 x 24,2; kontrakty rudy dzialaja; koszyki w nadmiarze (1H, nogi) - warsztaty tych koszykow "bez zysku", rece w liniach z brakiem; rece z tabeli (nie z 90 mln ksiegi); robota w toku przetrwala ponowny zapis | wszystkie powyzej |

Jesli prog nie przejdzie - cofnac podpaczke (wylacznik MCM) i szukac przyczyny w logu przed nastepna (CLAUDE.md zasady 1, 5).

---

## 7. Ryzyka / co sprawdzic (kontrola wedlug zasady 0 CLAUDE.md)

**Regresje i kolizje (kod):**
1. `TownHands` czyta 6 miejsc. Rosna razem (swiadomie): `TownCrafts` (bramka zysku, nie rece), `TownFletchers` (dziela polke rudy z kuznia;
   174.2 liczy ich braki), `SmithHours` (wiecej napraw = wiecej rudy, 4.5). NIE rosna: `ColdStart` i `CaravanBulk.Use/Target/Keep` (`LegacyTownHands`)
   - inaczej zapas z niczego x2 i zrodla 174.2 znikaja. `RawPrice` czyta `CaravanBulk.Usage` - przy starych rekach cena surowca w duzych
   miastach nie podskoczy (dobrze: kontrakt i karawany licza sie z prawdziwa cena polki).
2. `TablePeopleOf` - kolejnosc wobec `PopulationLaw.Calibrate` i `PopulationScale`; kultury spoza `Table` (Castle Black, Hardhome, Thenn,
   Frostfang's Camp...) daja 0 - `max` trzyma dawny wzor.
3. `LineShare` + `Candidates` - jedyni czytelnicy `_rank` i `_guildCache` to `WorkshopLaw` (grep); zmiana kolejnosci nie zmienia bramki zysku
   (:288) ani zaplat (:313-325). Kontrola sumy udzialow w logu (3.2 D).
4. `ArmsLeaks` - prefiks z kopia petli gry: sprawdzic w dekompilacji BK/BEE/DTE, czy ktos inny patchuje `DeleteOverproducedItems` (dzis:
   tylko nasz `MoneyLedger.ShelfPostfix` - postfiks, dalej dziala). BK `DeleteOverProduction` (BKSettlementBehavior.cs:314-338) dotyczy
   towarow handlowych > 500 bez broni i zbroi - zamykany osobno w 174.3 (`BkRawNoRot`).
5. `BkSupplyTemper` - te same punkty co 172: partie ponizej `MinimumSoldiersThreshold` nie licza potrzeb - zerowanie w `BuyItems()` to lapie.
   Morale BK za brak broni/tarcz - przy potrzebie 0 brak kary (jak strzaly).
6. `MaterialOrders` - karawana z `DoNotMakeNewDecisions`: `CaravanBulk.Trades` i 172b dzis ja odrzucaja (to ich warunek na posilki DTE) -
   wyjatek po Id kontraktu; BK moze przejmowac decyzje karawan (`IslandRoads` juz przechwytuje cele BK) - kontrakt trzyma cel przez prefiks
   na tym samym miejscu, ktore przechwytuje IslandRoads; sprawdzic, ze karawana gracza nigdy nie dostaje kontraktu.
7. 174.0b - zapis przez `SaveText.Sync` w kawalkach (limit 32 KB, paczka 161); 2-5 tys. wierszy; wczytanie z pominieciem brakow (wpis 24).
8. 172b (karawany bez amunicji) - bez kolizji poza wyjatkiem kontraktu; strzelarze moga zostac bez rudy w miastach, gdzie warsztaty wiecej biora (prog strzal).

**Wydajnosc gry:** ranking bez nowych wywolan `Factor`; sprawdzenie czynnej linii raz na linie na dobe; pamiec "brak koszyka" na miasto
na dobe (ok. 97 x 50 koszykow); kontrakty - O(miasta x zrodla) raz na dobe, 10-40 kontraktow naraz [S]; ksiega uzbrojenia - jedna galaz
w podsluchu `AddToCounts`. **Stosy na polkach:** kasowanie 5% bylo jedynym hamulcem liczby stosow (przedmiot + modyfikator; kazda sztuka
warsztatu ma losowy modyfikator, WorkshopLaw.cs:317); po jego wylaczeniu i przy x2 produkcji stosow przybywa, a petle po polkach (BK konsumpcja,
`BuyItems`, `AiGear`, `DailyTrade`, `SupplyDemand`) zwalniaja. W zapisie Jeffa lezy ok. 1 mln sztuk. Miary: liczba stosow w linii 174.0,
prog 120 dob (<= 2 x d40) i s/dobe na zapisie roku. Odbiorca sztuk zniszczonych, ktorych nikt nie kupi: zlom do kowala (158) - rozdz. 9 i
pytanie 4. Optymalizacja calosci - na koncu (decyzja Jeffa 08.10).

**Ceny i inflacja:** wiecej podazy -> mnoznik polki schodzi z x4 -> bron i zbroje taniej dla AI i gracza (lup gracza wart mniej) - to jest
cel (sygnal braku wraca). Warsztaty: przychod na sztuke spada, bramka 5% hamuje dopiero przy zawalonej polce. Pozniejsze ryzyko O7:
bogate miasta (najwiecej rak po 174.3) przestana robic sztuki pracochlonne przy F ok. 1 - mierzyc "bez zysku" wedlug dobrobytu; P6 w rozdz. 9.
**Kasy miast** [P/S]: dzis stoja (+25 tys./d w d31-40). Przeplyw kasa miasta -> warsztaty zbrojne w d40: +12 306 (surowiec i place wracaja
do kasy) / -38 812 (zaplata za wyrob), netto ok. -26 tys./d; po 174.3 ok. x2 (netto ok. -50 tys./d) wobec zakupow AI +73,7 tys./d (d40),
ktore tez rosna z towarem; zysk warsztatow ukrytych wraca do notabla (ArtisanInputs.cs:26). 174.0 zabiera kasom "zloto z niczego" za zjedzona
bron (10-20 tys./d [S]). Prog testu: srednia d111-120 >= -30 tys./d. Kontrakty 174.2 to zwykly handel karawan (kasy <-> karawany).

**Ruda, drewno, wsie:** kopalnie x1,5 - magazyn wsi gorniczej (5 dob produkcji) i woz (122: caly magazyn) rosna same; utarg 26 wsi gorniczych
+ok. 700 d/d [S]. Las x1,6 - kazda wies (poza drwalami) wiezie +60% drewna; gnicie BK drewna 2% -> 0,2%; zatkanie magazynow wsi - linie "Dowoz";
w pierwszych miesiacach nowej kampanii drewno rosnie szybciej (malo budow) - hamuje je cena. Gdy drewno spada < -50/d - krok lasu
przed krokiem rak S4 (nie odwrotnie).

**Geografia i rozgrywka:** rece rosna najmocniej w Reach, Qarth, Volantis, Braavos (x4-5); Polnoc x1,2, Dorne x1,1, male krolestwa x1,0
(nikt nie traci). Bron z duzych miast idzie do reszty przez `SupplyDemand.DailyTrade` (zasieg 250) i zakupy lordow (pytanie 1).

**Siodlarze (15% rak) i tarczownicy (5%):** uprzezy AI nie kupuje - po 174.1 siodlarze nie dostaja zwolnionych rak, chyba ze w ich koszykach
jest brak; tarcze dostaja zwolnione rece wedlug braku. Decyzja o wagach - po 120 dobach (rozdz. 9).

**Zapis gry:** od 174.0b robota w toku, dlugi i zamowienia sa w zapisie (wersja 1 pisala "wczytanie zapisu bez zmian" - blad); 174.2 -
kontrakty (`arm_matorders`); 174.3 - nic (rece liczone co dobe z tabeli). Wylaczenie podpaczki w MCM = dzisiejsze zachowanie.

---

## 8. Pytania do Jeffa (tylko zmiany rozgrywki; praca nie czeka - domyslnie rekomendacja)

1. **Kto zyskuje rzemieslnikow.** Po 174.3 rzemieslnicy beda tam, gdzie ludzie, a nikt nie bedzie mial mniej niz dzis. Najwiecej zyskaja:
   Reach (x4,8 - Highgarden 37 -> 225), Qarth (x4,6), Braavos (x4,5), Volantis (x4,1), Pentos, Qohor, Norvos (x2,7), Krolewska Przystan
   (x2,6), Ziemie Burzy (x2,3), Dolina (x2,1). Mniej: Dorzecze (x1,5), Polnoc (x1,2 - Winterfell 39 -> 61), Dorne (x1,1). Bez zmian: Zelazne
   Wyspy, Dragonstone, Nocna Straz, Wolni Ludzie, Skagos, Ibben, Sarnor. Wyspy Letnie 6 -> 37 (luki). Najlepszy sprzet kupisz w wielkich
   miastach poludnia i Essos. Zgoda? **Rekomendacja: tak** (Paryz, Mediolan, Londyn); tradycje rzemiosla kultur (np. Zelazne Wyspy w gore)
   - w audycie armii krolestw.
2. **Gorszy sprzet zamiast zadnego** (dwie osobne sprawy):
   (a) Zolnierz AI bez broni swojego szczebla kupuje dowolna bron biala tieru <= swojego z polki (dzis tylko ten sam typ, tier t albo t-1,
   a na targach leza tysiace mieczy i toporow w innych tierach). Do cwiczen i awansu liczy sie dalej tylko bron szczebla. W Twoich bitwach
   AI czesciej ma bron (DTE i tak zaklada to, co jest). **Rekomendacja: tak** - to zapas, nie sprzet z niczego.
   (b) Zolnierz AI bez zbroi na tulow kupuje przeszywanice albo zbroje 2 tiery nizej; liczy sie do "ochrony", nie do cwiczen ani awansu
   (w 171 postanowilismy "nie", bo liczylaby sie do awansu - tu juz nie). **Rekomendacja: tak, jesli pierwszy test pokaze, ze "cokolwiek na
   tulow" ma mniej niz 75% ludzi** (historia 75-95%); inaczej nie trzeba.
3. **Zbroja "na amunicje" w sklepach.** Gdy brakuje zbroi tieru 1-3, co druga nowa sztuka bedzie najszybsza w robocie - zwykle najslabsza
   w swoim tierze. Wiecej tanich zbroi dla wojska, w sklepach troche wiecej slabszych sztuk tych tierow; tiery 4-6 bez zmian. **Rekomendacja: tak.**
4. **Twoj stos ok. 1 mln sztuk.** Ok. 2/3 to dawne darmowe komplety. Dzis powoli go zjadaly ujscia, ktore 174.0 zamyka - bez decyzji zostanie
   na zawsze. (a) Zostawic. (b) Nadmiar ponad rok popytu danego rodzaju (glownie bron 1H t1-t2, buty) kowale miasta skupuja powoli na zlom
   (1% nadmiaru dziennie, za 7-14% ceny nowej - jak stare kolczugi w Anglii lat 1330.); zlom to metal dla kowali w miastach bez rudy.
   **Rekomendacja: (b).**
5. **Gracz - jedna regula z AI.** (a) Werbunek bez darmowego kompletu DTE - to Twoja decyzja z 05.10; wdrazam jako paczke 175 (bez pytania).
   (b) Zaopatrzenie BK Twojej partii (bron, tarcze, strzaly) przestaje sie zuzywac osobno - zuzycie liczy tylko Armoury (bitwy, naprawy),
   jak u AI; dzis placisz to zuzycie dwa razy. Morale BK za brak tych zapasow znika. **Rekomendacja: tak.**
6. **Dorobek startowy i rzeczy z domu.** Nowa kampania daje na start 44 tys. sztuk na targach i 345 tys. w zbrojowniach (raz), a kazdy
   rekrut tieru 1 przychodzi z rzeczami z domu (ok. 4,6 tys. sztuk dziennie). To "dorobek lat", a nie produkcja. 174 tego NIE powieksza.
   Zostaje tak? **Rekomendacja: tak** - historycznie sprzet nowej armii pochodzil glownie z domow i zbrojowni (historia.md 5.2); bez tego
   pierwsze miesiace bylyby bez armii.

Informacja (bez pytania): bron i zbroja na targach beda tansze, gdy produkcja dogoni braki (sprzedany lup tez da mniej); ruda w miastach
przy kopalniach (do 2-3 dni drogi) potanieje; karawany zaczna wozic rude, len i skory na zamowienie (mozna je obrabowac - Ty tez).

---

## 9. Pozycje dla innych paczek (zapisane, nie ruszane w 174)

- **175 Werbunek gracza jak AI** (decyzja 05.10; 3.6) - rownolegle do 174.
- **Liczba wojska**: test 160 tys. w d40 (partie 98 tys. + zalogi 62 tys.) i nadal rosnie, decyzja 05.10 ok. 108 tys. - sprawdzic, co liczylo
  108 tys., i czy hamulce wzrostu (podatek wojenny 173/165, lordowie rozpuszczaja poczet w pokoju) wystarcza.
- **133 Wytop przy kopalni** (PLAN-K13 :213-234) - warunek S4/S5 i dalekiego przewozu zelaza (sztaby); po 133 las wsi w dol.
- **K13 krok 1 (132): len, welna, skory x1,3** razem z popytem - przeszywanice i skorznie; warunek S5. **Zawor produkcji wsi K13** - przed S4.
- **`CaravanBulk.Use` z zmierzonego zuzycia** zamiast z rak (koniec `CaravanBulkLegacyHands`).
- **Ludnosc (P-8, paczka 109)** - po naprawie ksiegi ludzi `TablePeopleOf` moze przejsc na nia.
- **P7 narzedzia wedlug zuzycia** (lancuch.md P7): 24 ladunki rudy/d na narzedzia - zwalnia ok. 15 ladunkow.
- **Ruda "przepada z rozbitymi partiami" ok. 16/d** (9% wydobycia) - czy zwyciezca bierze ladunek ("jak znika - zamykamy").
- **GarrisonCarts (171) i `SupplyDemand.DailyTrade`** to tez przewoz bez partii - przejrzec wobec decyzji 05.10 (jak 174.2).
- **Uprzaz konska bez kupca AI** (AiGear.Order bez HorseHarness; 160/167) - zakup uprzezy razem z koniem albo waga siodlarzy wedlug popytu.
- **Tarczownicy 5%** - jesli po 120 dobach pokrycie tarcz partii AI < 35% (szczebel): wojenna pomoc ciesli jako nowy klucz wagi.
- **Placa miasta w cenie sztuki (O7/P6)** - gdy polki zaczna sie napelniac (F ok. 1).
- **Strzaly: `HistAmmoLaborMultiplier` 8 -> 5** - gdy strzelarze zajeci > 90%.
- **Dni helmu t1-t2** (na dolnej granicy historii) - wrocic po S5.
- **Zlom ze stosow nikomu niepotrzebnych** (sztuki zniszczone, nadmiar) - 158; zalezy od pytania 4.
- **Wspolczynnik tradycji rzemiosla kultur** - audyt armii krolestw (zadanie 09.10).
- **"Dobytek osady"** (historia.md P3) - zamiast wzorca t1 z reguly 92; strona popytu.
- **Poprawki dokumentow repo** (historia.md rozdz. 8): HISTORIA-KOSZT-NAPRAWY rozdz. 6 "300 snopkow" -> 1 906 snopow (45 744 / 24);
  HISTORIA-ZBROJE-STRATY 1.2 - ffoulkes nie nazywa bitwy (Maclodio to dopisek); 2.2 - "1-2 snopy dziennie" to caly lancuch jednej osoby;
  STAN-PRAC "Otwarte sprawy 2" (werbunek gracza) - nieaktualne wobec decyzji 05.10.

---

## 10. Zrodla

**Pomiar [P]:** `SCR\kopia171\Armoury-2026-10-08_20-47-10.log` + `SCR\kopia171\2026-10-08_20-47-10\{warsztaty,handel,zakupy}.log`,
`ludzie-regiony.csv` (doby 108836-108875; linie "Warsztaty: dzien", "Strzelarze (172)", "Rynek broni: na polkach", "Pokrycie zbrojowni AI (171)",
"Ruda: dzien", "Towary: dzien", "Ceny surowcow", "Rzemioslo miasta (148)", "Ochotnicy", "Komplet rekruta", "Skup sprzetu (doba)",
"Zbrojownie zalog (171)", "Sakiewka ludzi", "ZakupyAI", "Przeplywy osad", "ColdStart"); `SCR\kopia169\`, `SCR\s1kopia\` (biegi bez 171);
rok TOWARY 3 (audyt 03 rozdz. 1.5, spec. 171 rozdz. 7.1). Analizy: `SCR\p174\bilans.md`, `lancuch.md`, `historia.md`, `SCR\a171\`;
skrypty tej wersji `SCR\p174\skrypty\{reszta,odplyw,towary,rece,rece2}.py`.

**Kod [K]** (`SCR\noc\n172\Armoury\src`): WorkshopLaw.cs (:43 Reset, :50 _owed, :155-198 Needs/MissMask, :200-337 CyclePrefix, :344-401
Candidates, :370 filtr, :412 _wip, :417-435 GuildOf, :463-479 Revenue, :481-494 GuildWeight, :505-518 TownHands/Hands, :521-550 LineShare);
SupplyDemand.cs (:96 _unmet, :100-162, :259-268 Factor, :379-491 DailyTrade); HistoricalPrices.cs (:792-827, :959-960); BkSupplyTemper.cs (:69-208);
GoodsLedger.cs (:1040-1080); MoneyLedger.cs (:455-480); TownFletchers.cs (:385); TownCrafts.cs (:282); ColdStart.cs (:133-195);
AiGear.cs (:142-149, :191-235, :247-295); ArmsDrill.cs (:110-120, :335-360); MenPurse.cs (:316-334); RecruitKit.cs (:20-27, :455-470);
GarrisonCarts.cs (:13-22, :118-126); CaravanBulk.cs (:73-76, :123-129, :200-232, :290-306); MarketCarts.cs (:1-50, :338-346, :382-480);
IslandRoads.cs (:20); PopulationLaw.cs (:37, :72-124, :333-360); ArmsPricing.cs (:183-188, :211-228); ArmouryBehavior.cs (:389, :391-502, :988);
MaterialLaw.cs (:172); VillageWoodlot.cs (:54); TownWage.cs (:17-24); Settings.cs (:377, :430-435, :482-483). Gra: `ItemConsumptionBehavior.cs`
(:64, :79-97), `CaravansCampaignBehavior.cs` (:1342), `CaravanPartyComponent.cs` (:242), `DefaultItemCategorySelector.cs` (TaleWorlds.Core).
BK: `EconomyPatches.cs` (:579 ItemConsumptionPatch), `BKSettlementBehavior.cs` (:314-338, :672-690), `PartySupplies.cs`, `BKPartyNeedsModel.cs`.
DTE: `PartyEquipmentDistributor.cs` (:74-80, :1296-1322, :1765-1780, :1904-1936). Dane: SandBoxCore `leg_armors.xml` (:693-709),
`horses_and_others.xml` (leather, linen: weight 10); ROT-Content `ROTassets.xml` (przeszywanice bez item_category), `ROT-Troops.xml`.

**Historia [H]** (pelne adresy w `SCR\p174\historia.md` rozdz. 10 i w dokumentach repo HISTORIA-WARSZTATY, HISTORIA-ZBROJE-STRATY,
HISTORIA-STRZALY, HISTORIA-KOSZT-NAPRAWY): Statut z Winchesteru 1285 (statutes.org.uk); Paryz 1292 - Geraud 1837 / B. Crowder "Occupations
in 1292 Paris"; Paryz 1384 i organizacja platnerzy - M. Bernard, Medievales 69 (2015); Mediolan 1288 - Bonvesin, De magnalibus (przez
myArmoury) [H?]; Londyn - B. Kirkland, "Now thrive the Armourers", PhD York 2015; Greenwich 1544, Mediolan 1427 - C. ffoulkes, The Armourer
and His Craft (1912); Tower 1320-1410 - T. Richardson, PhD York 2012; Malemort, garderoba 1336-60 - R. Storey (De Re Militari); Florencja
1429-33 - S. Picchianti 2023 i 2025 (Uniwersytet Lodzki); zelazo Europy - Brill 2009 (Statista), Johannsen wg Ress; Anglia ok. 1300 - NAMHO
SECTION_5_Iron; koszty przewozu ladem i morzem - J. Masschaele, "Transport costs in medieval England", Economic History Review 46 (1993)
[H? - liczby 1,5 d/t/mile i relacja morze/lad z drugiej reki, do sprawdzenia]; kanon: G.R.R. Martin i in., The World of Ice and Fire
(Zelazne Wyspy - kopalnie), A Game of Thrones (Donal Noye, Castle Black) [H?].

---

## 11. Krytyka i odpowiedzi (36 uwag, 09.10)

Kazda uwaga sprawdzona w kodzie (n172, dekompilacje gry, BK, DTE) albo w logu A171 (skrypty `SCR\p174\skrypty`). Werdykt: PRAWDA -
wprowadzona; CZESCIOWO - wprowadzona z poprawka; FALSZ - odrzucona jednym zdaniem (zadnej nie odrzucilem w calosci).

| # | Waga | Teza krytyka (skrot) | Werdykt i sprawdzenie | Co zmienione |
|---|---|---|---|---|
| 1 | krytyczne | Popyt d31-40 to nie "armie przestaly rosnac" - nowi ludzie tlumacza 87% przyrostu braku | PRAWDA: ludzie 149 423 -> 160 488 (+11 065), brak/czlowieka 1,847 -> 1,866; nowi 20 434 z 23 527, korpus 3 897 z 5 509 [P] | 0 pkt 1 i 6; 1.1 uwaga; nowy 1.1a (rozbicie, popyt bez wzrostu dla 160 i 108 tys.); 4.2 wobec popytu bez wzrostu + strumien wzrostu osobno; rozdz. 9 (liczba wojska); `reszta.py` |
| 2 | krytyczne | ColdStart.Markets liczy z TownHands - 174.3 podwaja zapas z niczego | PRAWDA: ColdStart.cs:161 `hands = TownHands`, `AddToCounts(it,1)` bez materialu; 44 087 -> ok. 90 tys. [K][S] | 3.0, 3.4 (E) `ColdStartLegacyHands`; 4.3 przeliczone bez podwojenia; 0 pkt 7 lista strumieni bez materialu; pytanie 6 |
| 3 | krytyczne | 174.2 "woz bez partii" lamie decyzje 05.10 (prawdziwe partie) | PRAWDA: STAN-PRAC.md:332-333; GarrisonCarts bez bandytow; prawdziwe wozy traca 15,8 ladunku/d [P] | 174.2 przebudowana: kontrakt dla prawdziwej karawany (3.3, 5.3); decyzja w naglowku; 0 pkt 4; rozdz. 9 (GarrisonCarts, DailyTrade) |
| 4 | wazne | Reszta polek maleje; amunicja karawan w "bez sladu"; 174.0 nie zamknie 0,6-1,0 | PRAWDA: reszta d2-10/11-20/21-30/31-40 = -1 592/-930/-822/-673; amunicja karawan +573/d w d2-10 (1 076 -> 5 921) [P] | 1.3 (tabela w dziesiatkach, wzor), 2.7, 3.1 "Ile da" 0,6-0,7; test 174.0 na reszcie skorygowanej; `tools/p174_reszta.py` |
| 5 | wazne | _wip, _owed, _unmet nie sa w zapisie - "wczytanie bez zmian" falszywe | PRAWDA: ArmouryBehavior.cs:389 (Reset), SyncData :391-502 bez klucza; audyt W7 otwarty [K] | nowa 174.0b (3.1b, 5.1b, test); 7 "Zapis gry" poprawiony |
| 6 | wazne | Zwolnione rece ida wagami Paryza (krawcy, siodlarze), nie wedlug braku; tarcze tez potrzebuja rudy | PRAWDA: LineShare :538-541; `take = floor(owed+need)` :265-277; udzialy 37,5/25/19/12,5/6% przeliczone [K] | 3.2 (A) `WorkshopFreedHandsByShortage`; 2.6 (surowiec ulamkowy); prognoza 174.1 osobno (4.1) i jej progi; linia "rece przeniesione" |
| 7 | wazne | Bilans skory i plotna niepoliczony; tkanie i garbowanie "x2 rak" to nie dzwignia | PRAWDA: d32-40 skora 91,3/86,3 (+5,0), plotno 91,4/80,2 (+11,1); 148 zajete 40%, len brak w 62 miastach [P] | 2.3, 4.5 (liczby po krokach), 174.2 wozi len i skory surowe dla 148; progi >= -10/d, miast bez plotna <= 60 |
| 8 | wazne | CaravanBulk Target/Keep rosna z rekami - zrodla 174.2 znikaja | PRAWDA: Use = rece x 0,025 + 0,05 (CaravanBulk.cs:223); cele 827 -> ok. 1 630, Keep 3 260 > 2 244 rudy w miastach [S] | 3.4 (E) `CaravanBulkLegacyHands`; prog zrodla 174.2 niezalezny od rak; sumy w linii "Rece (174)", 4.5, test |
| 9 | wazne | 4.3 liczy odplyw z dna polek; "4-18 miesiecy" glownie z darmowego zapasu | PRAWDA: odplyw korpusu przy towarze 582 (d2-10), 409 (d21-30); helm 352 [P] | 4.3 przeliczone (korpus 1-4 miesiace); skutek produkcji osobno; 0 pkt 5 |
| 10 | drobne | "Kasy miast topnieja" falszywe; prog z jednej doby | PRAWDA: 7,45 (d10) -> 7,58 mln (d40); trend d31-40 +25 tys./d [P] | 2.1, 7 (przeplyw do warsztatow liczony), prog 10-dobowy w 6 |
| 11 | drobne | Rozjazdy liczb (ruda w 2.3, krotnosci, sumy 4.1, wspolczynniki rudy, drewno z jednej doby, strzelarze) | PRAWDA (a-f): suma rudy 2.3 nie zgadzala sie z wydobyciem; model rudy 92 wobec 80,6; drewno +569 to d40 (srednia +470) [P] | 2.3 bilans rudy z ksiegi (zgodny), 4.0 kalibracja, 4.1 sumy z kolumn, 4.5 srednie, 2.2 strzelarze 847,5; krotnosci ujednolicone |
| 12 | drobne | (D): n i suma short musza liczyc tylko linie czynne; (A) bez (E) | PRAWDA (specyfikacja) | 3.2 (A) z (E), (D) tylko czynne + normalizacja + kontrola w logu |
| 13 | drobne | Oplata "za ladunek" 10x za droga dla skory/plotna; zysk po cenie sprzed dostawy | PRAWDA: leather, linen weight 10 (horses_and_others.xml) [K] | 3.3 pkt 5: oplata od kg (`CarterPencePerKgPer100`), zysk po cenie po dostawie (karawana nie dostaje doplat) |
| 14 | drobne | Konsumpcje robi BK ItemConsumptionPatch; zamki; wyroby gracza kasowane | PRAWDA: EconomyPatches.cs:579; BKSettlementBehavior.cs:685-690, :329-331 [K]; linia startowa "CalculateBudget wpiete" jest w A171 | 2.7 miejsca kodu; 3.1 (a) miasta i zamki, (b) wyroby gracza jako jawna regula z powodem technicznym i licznikiem; linia startowa |
| 15 | drobne | Lotus Bay dostaje rece z hearth; Zelazne Wyspy traca > 50% | PRAWDA: Lotus Bay 37 -> 110 w 39 dob przy wzorze z ksiegi; Pyke 32 -> 10 [S] | 3.4 (A): ludnosc z tabeli (Lotus Bay 37) i `max` ze starego (nikt nie traci); pytanie 1 z lista krolestw |
| 16 | drobne | Bez kasowania 5% stosow przybywa - petle po polkach zwalniaja | PRAWDA (mechanizm: WorkshopLaw.cs:317 losowy modyfikator) | 3.1 (d) licznik stosow; 7 "Wydajnosc"; prog 120 dob; zlom - pytanie 4, rozdz. 9 |
| 17 | drobne | 174.3 dodaje drewna, a BK kasuje 2% stosow > 500 | PRAWDA: BKSettlementBehavior.cs:333; drewno 9/d (A171), 179/d (rok T3) [P] | 3.4 (C) `BkRawNoRot` (P1-D) w 174.3; prog gnicia drewna |
| 18 | krytyczne | PeopleOf rosnie z hearth (z niczego) - nie wolno na nim opierac rak | PRAWDA: A171 52,51 -> 58,98 mln w 39 dob; audyt 01 L8, 09 P-8; wzor v1: rece 5 547 -> 6 325 (+14%) [P][S] | 3.4 (A) `TablePeopleOf` (suma = tabela krain), test "rece d40/d1 <= +2%", kampania Jeffa liczona z tabeli |
| 19 | krytyczne | "Pokrycie" mierzy szczebel, nie ludzi; zdania dla Jeffa i porownanie z historia bledne | PRAWDA: AiGear.Deficit :191-235 (t-1), MenPurse po typie, DTE tier <= t+2 bez dolnej granicy [K] | 0 pkt 2; 1.2 przepisane; nowa miara "dowolny szczebel" (3.1 f); progi na obu miarach |
| 20 | krytyczne | ColdStart x2 to polowa obietnicy; testy przejda dzieki zapasowi | PRAWDA (jak 2) | jak 2; test 174.3: ColdStart = A171 +-5%; prog "korpus d120 >= 3 000" usuniety |
| 21 | wazne | Komplet DTE gracza - najwiekszy strumien z niczego; decyzja 05.10 juz zapadla | PRAWDA: RecruitKit.cs:26, :464; STAN-PRAC.md:354 i :448 (decyzja), :575 (nieaktualne "otwarte") | nowy 3.6 "Gracz"; 1.3 wiersz; paczka 175 rownolegle; pytanie 5 (zaopatrzenie BK); rozklad kary Underequipped do linii bitwy |
| 22 | wazne | Rozklad wedlug krolestw: male traca, Reach x5; kanon | PRAWDA: Zelazne Wyspy 153 -> 51, Reach 185 -> 982, Polnoc x1,36 przy wzorze v1 [S] | `max` ze starego wzoru; tabela krolestw w 3.4 i pytaniu 1 ("kto zyskuje"); wspolczynnik kultury - nie w 174 (powod w 3.4 A), rozdz. 9 |
| 23 | wazne | 4.2 "w pasmie historii" sprzeczne z 1.2; pytanie 2 bez podstawy | PRAWDA: helm 45-55% wobec 60-90%; bron 1H 6 293 i 2H 3 191 na polkach d40; BuyLoop tylko t / t-1 (AiGear.cs:262-270) [P][K] | 4.2 zdanie poprawione; pytanie 2 rozdzielone (a) bron - tak, (b) zbroja - po pomiarze |
| 24 | wazne | Dalekie wozenie rudy niehistoryczne (wozono sztaby), marnuje drewno; brak reguly wysp | PRAWDA: historia.md 4.2 (sztaba 1/7 rudy, miasta kupowaly sztaby) | 3.3: ruda <= 200 (2-3 doby), reszta 600; odcinek morski (`SeaFreightShare`) [H?]; daleko - po 133 |
| 25 | wazne | 0,3-0,6 to [S z H] i zawiera strzelarzy; S5 = 0,33 to ponad minimum | PRAWDA w czesci strzelarzy (HISTORIA-WARSZTATY "[SZAC]", luki/strzaly 300-800); rzemioslo 148 (sukno, skora) nie jest bronia, wiec go nie dolicza | 2.2 oznaczenie i porownanie z amunicja (5-9x); 3.4 (A) "ok. polowa minimum"; 3.5 S4 = 0,15, S5 = 0,25; MCM "Estimate from history" |
| 26 | wazne | (E) + (D) zatrzymaja t5-6 (plyta 118 dni, kolczuga 90) prawie wszedzie | PRAWDA: bank `share x 60` (:247) - start do `share x 120`; linia ultra ok. 0,5 reki w medianie [K/S] | 3.2 (E) plan z podloga rownego podzialu, (D) "dokoncz zaczete"; linia "zrobiono t5-6"; prog >= 0,7 x bieg 174.0 |
| 27 | wazne | "Na amunicje" = zawsze najslabsza sztuka tieru; zmiana rozgrywki bez pytania | PRAWDA: dni x q, q = skutecznosc / mediana (ArmsPricing.cs:213-216, :226) [K] | 3.2 (C): tylko t1-3, co druga sztuka; srednie q w logu; pytanie 3 |
| 28 | wazne | Stos Jeffa 2/3 z niczego zostaje na stale po 174.0 - brak wyboru | PRAWDA (wniosek z 1.4 i 3.1) | 1.4, 4.4; pytanie 4 (zlom za 7-14% ceny, historia.md 6) |
| 29 | drobne | Castle Black jest na Murze, nie za nim; ma kuznie (Donal Noye) | PRAWDA [H? kanon] | 3.4 (A) opis; `max` - Nocna Straz zostaje przy 34 |
| 30 | drobne | Krotnosci rak (4-6 / 4-9) i zelaza (10-20 / 15-40 / 10-25) sie rozjezdzaja; porownywac zelazo na bron | PRAWDA | 0, 2.2 (5-9x), 2.3 (cale zelazo 15-40x, na bron 5-30x), MCM `MineOutputStep` |
| 31 | drobne | Helm na dolnej granicy historii, nie "zgodny"; po 174.3 ponad pasmo | PRAWDA: 3-10 dni mistrza z pomocnikiem = 6-20 roboczodni; gra 4-6 dni; pasmo 58-173/d (historia.md 7.2) | 2.8 werdykt i akapit o swiadomym nadmiarze; rozdz. 9 |
| 32 | drobne | ladys_shoe jest cywilny; czy przeszywanica jest u krawca? | CZESCIOWO: Civilian prawda i nie ma go w zadnym wzorcu ROT; ale "garment" to w grze kazda zbroja tieru 1 (DefaultItemCategorySelector), nie flaga - przeszywanice t1 sa u krawca, t2+ u platnerza [K] | 2.6 (kategoria z tieru); 3.2 (B) przedmioty cywilne bez zolnierza poza liniami zbrojnymi, ColdStart i liczbami; 4.1 przypis |
| 33 | drobne | Filtr :370 (unikaty, valyrian...) musi zostac przy nowym rankingu | PRAWDA (filtr: WorkshopLaw.cs:370, Settings.cs:433) | 2.6, 3.2 (B), 5.2 "filtr :370 bez zmian"; ColdStart ma ten sam filtr (ColdStart.cs:143) |
| 34 | drobne | Gracz dalej ma zuzycie w BK i w Armoury - dwie reguly | PRAWDA (BkSupplyTemper: partia gracza bez zmian) | 3.1 (c) `BkSuppliesNoArmsPlayer`; pytanie 5 (zmiana rozgrywki gracza) |
| 35 | drobne | Prog kas miast z jednej doby (-184 tys.); trend plaski | PRAWDA (jak 10): 7,19 (d20) -> 7,58 (d40) [P] | 6: srednia d111-120 >= -30 tys./d; 9 - pozycja "kasy topnieja" usunieta |
| 36 | drobne | Tower: sklad wymagal konserwacji - "zero kosztu" nie ma przypisu | PRAWDA: 3 200 lukow, 11 ludzi, 5 dni; 125 mieczy, 24 dni (historia.md 6) | 3.1 (e) konserwacja zapasu rekami (`ArmsStallUpkeepManDaysPerPiece` 0,02); przypis w 3.1 (b) poprawiony |

Wlasne poprawki przy weryfikacji (poza uwagami): las wsi x1,6 sprawdzony na dwoch bazach (rok T3: x1,3 -> ok. -200/d drewna, x1,6 -> ok.
+100..+250/d; 4.5) - zostaje x1,6, ale z zamknietym gniciem BK; prog 174.2 "miast bez rudy" 20 -> 25 (zasieg rudy 200); progi kazdej
podpaczki wobec biegu poprzedniej, nie A171; S4-S5 przeliczone na nowe wspolczynniki (3.5, 4.1).
