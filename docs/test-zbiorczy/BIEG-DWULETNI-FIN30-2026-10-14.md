# BIEG DWULETNI fin30 (14.10) - SYNTEZA

Bieg: nowa kampania, 728/728 dob, kod `w-toku/zbior-e8` **6e6313c** (= W53 + C3-C5 + swiat + drobne-2 + towary-2 + C5 off + punkty postaci + okup + ciaza i plodnosc; Z.1 wojsko w pokoju; Diplomacy zmeczenie wojna / 4.33 w ustawieniach Jeffa).
**W tym biegu NIE ma** (sa w zbior-e8 po nim): pieniadze roku 2 (M1, M2a-c, M3 - chleb dla glodnych miast, B4 - zalogi w pokoju 70%), towary-3, A16, A17, strzaly, myto. Wyspy-wolni - projekt, kodu nie ma.
Log `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log` (287 MB), tabele `...\tabele-06-01-15\`, sesja CS `session-2026-10-14_06-01-14.log`.
Porownanie: **fin21** = poprzedni bieg dwuletni (`Armoury-2026-10-13_21-14-13.log`, raport `docs/test-zbiorczy/BIEG-DWULETNI-2026-10-14.md`: wojsko d701-728 55.3 tys., fala pokojow d449-513 - 8 z 13 przez zmeczenie Diplomacy).
Miara progu 85 tys. = W5-7: wojsko rodow krolestw w WOJNIE z linii `Skarbce:`, srednia ostatnich 28 dob okna. Doba = dzien gry - 108836 + 1; rok 1 = d1-364, rok 2 = d365-728; kwartal = 91 dob (Q1-Q8).
Znaczniki: **[P]** pomiar z logu / tabel, **[S]** szacunek albo wniosek; czego nie da sie ustalic - "nie wiem". Gra nie uruchamiana, kod bez zmian, bez commita.

Raporty zrodlowe (tu wyciag i odsylacze) - `SCRATCH = C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\dwuletni-fin30`:
- `SCRATCH\wojsko\RAPORT.md` - wojsko, pokoje, zalogi i Z.1, pieniadz koron i rodow, oblezenia, okupy (10 grup progow);
- `SCRATCH\ludzie\RAPORT.md` - ludnosc, zima, bandy, ciaze, bohaterowie, towary, chleb (10 grup progow);
- `SCRATCH\paczki\RAPORT.md` - stabilnosc, czas, zapis, nowe paczki, Inni, oblezenia z bitwy.log (12 grup progow; fin21 przeliczony tym samym narzedziem).
Uwaga: SCRATCH lezy na dysku C (katalog tymczasowy) - wyniki narzedzia i skrypty sa tylko tam.

## 0. Dla Jeffa - prostym jezykiem

(Po krytyce 14.10 poprawione punkty 2-5, 7, 8, 10 i 16 - co i dlaczego, rozdz. 13.)

1. **Dwa lata gry bez wywrotki:** 728/728 dob, 0 bledow naszych modow, 13 zapisow OK; doba 13.0 s (poprzednio 12.3) - nasze mody kosztuja tyle samo co wtedy, skad reszta - nie wiem [P].
2. **Oblezenia - masz racje, ze sa za latwe:** oblegajacy ma 6-7 razy wiecej ludzi niz obroncy (ok. 2 700-2 900 na 460), wygrywa 3 szturmy na 4 (w roku 2 - 83%), a **obronca nie wygral ani jednego szturmu w 2 lata** (0 z 73; reszta bez rozstrzygniecia) [P]. Gra decyduje o szturmie srednio po 8 dobach oblezenia (najdluzej 13) [P]; czy to "za szybko" - bieg nie ma wzorca (ile oblezenie powinno trwac), wiec to ocena, nie pomiar [S]. To nie jest nowe: w poprzednim biegu bylo podobnie (stosunek 5.9:1, oblegajacy wygral 66%, obronca 0 z 58) [P].
3. Skutek: **57 zdobyc w 2 lata - 48 roznych twierdz (co piata z 227; poprzednio 39 zdobyc, 34 twierdze)**; stolica padla 7 razy, ale w 4 krolestwach (Braavos 3 razy z rzedu, King's Landing 2 razy, Lorath, Dothrakowie), w tym King's Landing w dobie 530 - Robb Stark ok. 3 000 ludzi na 92 obroncow [P]. Reach zabral pol Dorne, Dorzecze i Polnoc - 13 twierdz King's Landing, Krainy Burzy zniknely [P]. Wiecej zdobyc niz poprzednio jest w roku 2 (26 wobec 12), gdy w tym biegu wojowalo wiecej krolestw (18-22 wobec 14-15) - wiec to nie dowod, ze oblezenia staly sie latwiejsze [P liczby, S wniosek].
4. **Sily nie sa rowne:** twierdza ma srednio 290-400 ludzi zalogi, armia pod murami 1.4-5 tys. [P]. Przed czescia szturmow (15 z 55 zdobyc) jest bitwa pod murami, zwykle w dobie szturmu albo dzien wczesniej: strona twierdzy (zaloga, milicja i druzyny w srodku) wygrywa, gdy ma armie (14 bitew, ok. 31 druzyn, zadna z tych twierdz nie padla), a przegrywa, gdy jest sama zaloga z paroma druzynami (17 bitew, ok. 5 druzyn, 20.9 tys. ludzi; 15 z tych 17 twierdz padlo) [P]. King's Landing: 1 156 i 227 obroncow w dwoch bitwach tego samego dnia co szturm [P]. Zadna twierdza nie padla z glodu - wszystkie szturmem [P]/[S].
5. Twierdza z armia w srodku sie broni: 42 wygrane wypady i po zadnym twierdza nie padla [P]. Okup zamiast szturmu dziala czysto (26 okupow, 0.96 mln zl) [P]; obejmuje tylko "krwawe" oblezenia, a 44 z 55 zdobyc szlo przy przewadze, gdzie okupu nie ma; w tym biegu okup zabral ok. 9 zdobyc [S]. **Oblezenia i sily robi teraz osobna praca** (workflow oblezenia-i-sily); co mierzy ten bieg - rozdz. 1.
6. **Wojsko w roku 1: 103.4 tys.** lordow w wojnie na koniec roku przy progu 85 tys. - trzyma (poprzednio 108.9) [P].
7. **Wojsko w roku 2: 62.4 tys. - ok. 23 tys. pod progiem**, ale o 7 tys. lepiej niz poprzednio (55.3); najnizej 48.1 tys. w dobie 550 [P]. Zima przyszla w innej dobie niz poprzednio, wiec ile z tych 7 tys. to nasze poprawki - nie wiem [S].
8. **Zmeczenie wojna / 4.33 zadzialalo:** w roku 2 zero pokojow "ze zmeczenia" (poprzednio 15) [P]. Pokojow razem 41 zamiast 151, ale roznica to glownie pokoje fabularne, ktore ROT i tak od razu wznawial; pokojow, ktore naprawde konczyly wojne, bylo 28 wobec 40 [P]. Fala pokojow i tak przyszla - zaraz po tym, jak ROT przestal wymuszac wojny Essos i Dorne-Reach (doba 433): 15 pokojow w dobach 435-538, wszystkie przeglosowane przez rody; poprzednio w tych dobach 16 (9 ze zmeczenia, 7 glosem) - rody przeglosowaly to, co wczesniej konczylo zmeczenie [P].
9. Krolestwo w pokoju dalej nie wraca do wojny (Dolina 293 doby) - dlaczego, nie wiem; Z.1 dziala czesciowo: krolestwo w pokoju trzyma ok. 10% wojska zamiast 1%, ale na koniec 6.3 tys. w druzynach przy celu 11-18 tys. [P].
10. **Pieniadze:** brak na zold spadl w roku 2 z 62 do 23 tys. zl dziennie (poprzednio stal na 51-58) [P] - ale ponad polowa spadku to Zelazne Wyspy (z 20 do 0), ktorym korona nagle placi 15 razy wiecej, i tego nie umiemy wyjasnic (nizej) [P]; ale biedne rody w wojnie traca na koniec 65-84 tys. zl dziennie (alarm od 45), dlug koron w Banku 2.0 mln (King's Landing 1.0, Pentos 0.7) [P]. Wolni Ludzie brakuje caly rok 12-19 tys. zl dziennie [P]. Skad zloto Zelaznych Wysp i czy ma platnika - nie wiem; do sprawdzenia przed wyspy-wolni [S].
11. **Ludzie:** 52.1 -> 51.6 -> 51.7 mln, z glodu nie umarl nikt [P]. Zima przyszla dopiero w dobie 629 (poprzednio 460) - pory roku ROT roznia sie miedzy kampaniami [P]. W zimie druzyny lordow dezerteruja z glodu (94 ludzi dziennie), bandy urosly do 20 tys. ludzi [P].
12. **Biedota wielkich miast** biednieje juz przed zima: chleb swiata 88% przy celu 98%, 8 miast nie stac na pelny koszyk (Braavos, Oldtown, Volantis, Tyrosh i in.) [P]; poprawki (chleb dla glodnych miast, M3) w tym biegu jeszcze nie ma.
13. **Ciaze dzialaja:** 391 poczec, 273 dzieci, 0 martwych, ciaza 273 doby - ale poczec jest ok. 2 razy za duzo (0.54 zamiast najwyzej 0.25 na mezatke na rok) [P].
14. **Towary:** plotno naprawione (brakuje w 2 miastach z 97 zamiast 77, cena zeszla z sufitu) i drewno (brak w 1-7 miastach zamiast 29-53) [P]; skory brakuje dalej - w zimie w 45 miastach, wojsko dostaje polowe potrzeby [P].
15. **Progi:** 634 wiersze w 23 grupach, 34 NIE + 2 DECYZJE [P]; prawdziwe bledy 2 (ciaze za czeste, stara ksiega uprzezy), reszta to progi w zlym oknie, zima, rok 2 albo czas [S]. Naprawione wobec fin21: licznik podatku wsi bez skokow o miliardy, karawany wroga w zdobytym miescie, kasy miast [P].
16. **Co slabe - kolejnosc:** oblezenia i sily; wojsko roku 2 (pulapka pokoju i glosy rodow za pokojem); zloto Zelaznych Wysp (krytyka); pieniadz biednych rodow na koncu roku 2; biedota wielkich miast; glod druzyn w zimie; skora; ciaze. Lista z liczbami i z tym, co moze zalatwic paczka juz zrobiona po tym biegu - rozdz. 10.

## 1. Oblezenia i sily - uwaga Jeffa (zrodla: `wojsko` rozdz. 8, `paczki` rozdz. 6, `ludzie` rozdz. 10)

### 1.1 Ile [P]

| | fin30 rok 1 | fin30 rok 2 | fin30 2 lata | fin21 2 lata |
|---|---:|---:|---:|---:|
| twierdze zdobyte (linie L0) | 31 | 26 | **57** (zamki 44, miasta 13; szturmem 55, wypadem 2); **48 roznych twierdz**, 8 zdobytych 2-3 razy (krytyka) | 39 (szturmem 38); 34 rozne |
| zdobycia w kwartalach | 5 / 9 / 8 / 9 | 10 / 6 / 9 / **1** | | |
| upadki stolic (L5b-1) | 4 | 3 | **7** (4 krolestwa: Braavos 3, KL 2, Lorath, Dothrakowie - po upadku BK robi stolica nastepna twierdze) | 3 |
| decyzje gry o szturmie z pomiarem okupu (O0) | 70 | 43 | 113 | linii nie bylo |
| okupy zamiast szturmu (O1) | 17 (0.71 mln zl) | 9 (0.25 mln zl) | 26 (0.96 mln zl) | paczki nie bylo |

- Stolice: Lorath d145, Totava d247, Iksa Calo d283, Iksa Keria d310 (koniec Braavos), Vaes Dothrak d428, **King's Landing d530 (Polnoc)**, Silverhill d615 [P].
- Ostatnie zdobycie d646, ostatni okup d641 - przez 82 doby (do d728) nie padla zadna twierdza; w Q8 oblezonych naraz srednio 0.07 miasta i 0.85 zamku [P]. Dlaczego - nie wiem.

### 1.2 Jak szybko [P] (`wojsko` 8.2, `paczki` 6; linie O0 sa od paczki okup, wiec tylko fin30)

- Od poczatku oblezenia do decyzji gry o szturmie: **mediana 8.4 doby** (p10 6.4, p90 10.7, min 4.2, max 12.7; zamki 8.5, miasta 8.3; rok 1 8.6, rok 2 8.0); zadnej decyzji po 14+ dobach [P].
- Szturm przychodzi 3-7 dob po decyzji (53 z 72), wiec oblezenie do szturmu trwa ok. 11.8 doby (zamek) i 13.3 doby (miasto) [S: doby z O0 + odstep].
- Wobec roku: do decyzji mija ok. 2.3% roku 364 dob; przy roku gry 84 dni byloby to ok. 10% [S]. To ten sam czynnik 4.33, ktorym poprawiono zmeczenie wojna Diplomacy [S]. (Krytyka: to tylko porownanie; oblezenie w grze liczy sie w dobach, nie w czesci roku, a wzorca dlugosci oblezenia - gra bez modow albo cel projektu - bieg nie ma. "4.33 x dluzej" to decyzja projektowa, nie wniosek z pomiaru [S].)
- W chwili decyzji "stosunek gry" (z czlonem czasu) ma mediane 48.7 przy stosunku murow 1.50 [P]; jak czas podnosi stosunek gry - kodu nie sprawdzano, nie wiem. (Krytyka: linia O0 podaje wzor: obroncy x przewaga murow ^ wykladnik; mediana przewagi murow 4.25; wykladnik spada z czasem: +1.08 po 4.2 dobach, -0.57 po 6.6, -1.66 po 8.4, -2.86 po 10.4, -4.22 po 12.7 [P] - czyli od ok. 5.5-6 doby mury w tym wzorze przestaja pomagac obroncy, a potem dzialaja na jego niekorzysc [S z liczb linii]. 57 ze 113 decyzji zapadlo przy stosunku murow < 1.5, w tym 22 przy < 1.0 - gra idzie do szturmu, choc wedlug wlasnej miary murow nie jest silniejsza [P].)
- Zapas jedzenia warowni nigdy ponizej 150 dni, glodnych warowni 0 przez 728 dob [P] - zadne oblezenie nie skonczylo sie glodem [S].

### 1.3 Jak latwo - szturmy (bitwy.log B175, bitwa Siege) [P]

Dwa skrypty licza szturmy troche inaczej (70 wobec 73; w fin21 58 wobec 67) - ktore bitwy dziela, nie sprawdzalem. Wniosek ten sam w obu.
**Krytyka - rozbieznosc wyjasniona [P]:** `analiza2` liczy wszystkie linie Siege, takze "szturmy" z oblegajacym `rodzaj=lord` i **0 ludzi** (fin30 2, fin21 9 - wszystkie bez rozstrzygniecia); `a9` ich nie liczy. Bez nich: fin30 71 szturmow, 55 wygranych (77%), stosunek 6.7, < 2:1 - 0; fin21 58, 38 (66%), 5.9, < 2:1 - 1 (wlasny skrypt `krytyka\k2_bitwy.py`, `k4_okup.py`). Roznica 70/71 w fin30 - 1 bitwa, nie wiem ktora. Wiec "fin21 57%" i "fin21 9 szturmow < 2:1" to linie-widma; porownywac 77% z 66% - roznica 11 pkt przy 71 i 58 szturmach to ok. 1.4 bledu standardowego, czyli slaby dowod, ze w fin30 szturm jest latwiejszy niz w fin21 [S].
**Krytyka - obronca nie wygral zadnego szturmu [P]:** wynik szturmu to "wygrywa A" albo "brak" (bez rozstrzygniecia); "wygrywa O" - 0 z 73 w fin30 i 0 z 67 w fin21.

| | `wojsko` a9: rok 1 / rok 2 / 2 lata | `paczki` analiza2: rok 1 / rok 2 / 2 lata | fin21 2 lata (a9 / analiza2) |
|---|---|---|---|
| szturmow | 40 / 30 / 70 | 43 / 30 / 73 | 58 / 67 |
| wygrywa oblegajacy | 72% / **83%** / **77%** | 70% / **83%** / **75%** | 66% / 57% |
| ludzie oblegajacego, mediana | 2 593 / 3 463 / 2 903 | 2 266 / 3 414 / 2 724 | 2 534 / 2 211 |
| obroncy, mediana | 451 / 502 / 462 | 451 / 490 / 462 | 421 / 404 |
| **stosunek ludzi, mediana** | 6.0 / 7.2 / **6.7** | 5.5 / 7.2 / **6.1** | 5.9 / 5.2 |
| szturmy przy stosunku < 2:1 | 0 / 0 / 0 | 3 / 0 / 3 | 1 / 9 |
| strata oblegajacego, mediana | 21.0% / 22.4% / 22.0% | 19.7% / 22.4% / 21.0% | 23.5% / 20.2% |
| strata obroncy, mediana | 100% | 100% | 100% |
| obronca to "zaloga" | 70 z 70 | 73 z 73 | wszystkie |

- Rozklad stosunku (a9, 70 szturmow): < 2:1 - 0, 2-4 - 19, 4-8 - 25, 8-16 - 15, >= 16:1 - **11** (fin21: 1 / 13 / 29 / 11 / 4) [P].
- 16 szturmow przegranych przez oblegajacego (krytyka: dokladnie - bez rozstrzygniecia, obronca ich nie wygral) tez szlo przy przewadze 2.7-10.8:1 (np. d213 castle_A4 4 999 na 462) [P, `wojsko\skrypty\a9_bitwy_oblezen.out.txt`].
- W chwili decyzji (O0): zamki - oblegajacych 2 950, obroncow 499, mury 1.87; miasta - 4 450 / 952, mury 1.14 (mediany); moc oblegajacy / obronca 5.7 (zamki 6.7, miasta 4.6) [P].

### 1.4 Sily - zalogi wobec armii [P]

| | fin30 | fin21 |
|---|---|---|
| zalogi swiata (d93-120 / d337-364 / d701-728) | 87.2 / **91.9** / **65.4** tys. | 81.2 / 89.3 / 58.2 tys. |
| na twierdze (227 twierdz) | ok. 290-400 ludzi | |
| zaloga + milicja dzien przed zdobyciem, mediana (rok 1 / rok 2) | 245 / 291 | 272 / 209 |
| - miasta: zaloga / milicja; zamki: zaloga / milicja | 280 / 150; 119 / 127 | |
| napastnicy dzien zdobycia (druzyny z podzialu lupu L2), mediana | 2 166 / 3 482 | 2 162 / 2 748 |
| stosunek napastnicy / (zaloga + milicja), mediana | **9.95** | 10.0 |
| bitwy pod murami (SiegeOutside) z zaloga po stronie twierdzy: zaloga przegrala | **17 z 31 (20.9 tys. ludzi zalog)** - krytyka: ludzi CALEJ strony twierdzy, nie zalog (rodzaj "zaloga" = strona prowadzona przez osade; partii mediana 10, w przegranych 5, do 38) | 10 z 26 (18.5 tys.) |
| pojedynczy lord atakuje armie pod murami: przegral | 28 z 28 | 25 z 25 |
| wypady (SallyOut): wygrywa strona "A" | 42 z 44 (95%), stosunek 1.9:1 | 77 z 78 |

- Ze "A" w wypadzie to twierdza z armia w srodku - [S] (z regionu bitwy, nie z kodu); ze lordowie atakujacy armie pod murami to proby odsieczy - [S].
- **Krytyka - bitwa pod murami to faza szturmu, nie "wypad zalogi w pole" [P]/[S]** (`krytyka\k2_bitwy.py`, `k5_wypady.py`): w 31 bitwach SiegeOutside atakuje zawsze armia oblegajaca, a twierdza sie broni. 16 z 17 przegranych przez strone twierdzy jest 0-4 doby przed szturmem tej samej twierdzy (13 w dobie szturmu albo dzien przed), 15 z tych 17 twierdz padlo w 14 dob [P]. 14 wygranych przez strone twierdzy: partii mediana 31 (armia w srodku), zadna z tych twierdz nie padla [P]. Po 42 wygranych wypadach - tez zadna [P]. Przegrane strony twierdzy szly przy stosunku ludzi mediana 3.4:1, ale 5 z 17 przy <= 2:1, w tym d190 (2 189 obroncow wobec 1 025) i d614 Silverhill (5 076 wobec 5 135) [P]. Ze garnizon wchodzi do takiej bitwy z mechaniki gry, a nie z wyboru AI - [S], do sprawdzenia w kodzie.
- Armie krolestw na koniec roku 1 (d337-364): Polnoc 10.9, Reach 10.0, KL 9.1, Dolina 7.4, Dorzecze 7.3, Krainy Burzy 5.3, Dorne 4.7 tys.; male krolestwa Essos 1.4-3.1 tys. kazde [P].
- **King's Landing d530** [P]: Polnoc 4 409 ludzi rozbija zaloge 1 156, potem 3 006 na 227, potem szturm 2 994 na **92** obroncow (strata atakujacego 3 ludzi) (`wojsko`); dzien przed zdobyciem zaloga 271 (w d364 664) i milicja 234, napastnicy 3 981; miasto ma 324 tys. mieszkancow i w dniu zdobycia traci 26 844 (`ludzie`). 92 obroncow w bitwie wobec 271 + 234 dzien wczesniej - roznica pomiaru, dokladnie nie wiem. (Krytyka [P]: obie bitwy pod murami i szturm sa w tej samej dobie d530, kilka sekund po sobie; strona KL: 5 partii / 1 156, 4 / 227, w szturmie 2 / 92 - wiec 1 156 to nie sama zaloga, bo zaloga + milicja dzien wczesniej to 505; ok. 650 ludzi to inne druzyny po stronie miasta [S]. "Zaloga 1 383 rozbita wczesniej" - nie: to strona miasta w dwoch bitwach w dniu szturmu.)

### 1.5 Kto czyje twierdze bierze (2 lata, start -> koniec, +zdobyte / -stracone) [P] (`wojsko` 8.3)

| krolestwo | fin30 | fin21 |
|---|---|---|
| Reach | 16 -> **26** (+11 / -1), 8 z Dorne | 16 -> 22 |
| Polnoc | 22 -> 28 (+5), 4 z KL, 1 z Wysp | 22 -> 23 |
| Dorzecze | 14 -> 17 (+10 / -6), 9 z KL | 14 -> 10 |
| Smocza Skala | 10 -> 20 (rody Krain Burzy d537) | 10 -> 17 |
| **KL (Baratheon)** | 21 -> **15** (+6 / -13) | 21 -> 24 |
| **Dorne** | 16 -> **8** (-8) | 16 -> 13 |
| Krainy Burzy | 13 -> 0 (zniszczone d537) | 13 -> 0 |
| Braavos / Myr / Norvos | 5 -> 0 kazde | 5 -> 6 / 3 / 3 |

### 1.6 Okupy oblezen (paczka okup) [P]

113 decyzji gry o szturmie: obronca zaplacilby 55, AI przyjalby 59, oba 26 -> 26 okupow za **963 046 zl** (zaplacone = otrzymane; miasta 8 za 598 094, zamki 18 za 364 952); powod zawsze "krwawy szturm"; wykluczone: stolica 10, fabula 1; rozejm 60 dob zlamany 0 razy; potkniecia 0. Zdobyc nie ubylo: 55 szturmem wobec 38 w fin21 (OKP-10: 145%). Grupa okup 14 progow, 1 NIE (OKP-1 - okno za waskie, rozdz. 9).
Krytyka: "zdobyc nie ubylo" porownuje dwa biegi z inna liczba wojen (rok 2: 18-22 krolestw w wojnie wobec 14-15) - to nie mowi, co zrobil okup. W tym samym biegu [P] (`krytyka\k4_okup.py`): decyzje przy murach < 1.5 bez okupu - zdobycie w 14 dob 11 z 31 (35%); z okupem - 26, zdobyc 0; decyzje przy murach >= 1.5 (okup niemozliwy) - zdobycie 44 z 56 (79%). Okup zabral wiec ok. 9 zdobyc [S: 26 x 35%], a 44 z 55 zdobyc szlo przy murach >= 1.5, gdzie okup z zalozenia nie dziala [P].

### 1.7 Co z pomiaru wynika dla pracy "oblezenia i sily" [S]

Pomiar pokazuje trzy miejsca, w ktorych oblezenie jest latwe:
- **(a) czas** - decyzja po ok. 8 dobach niezaleznie od roku 364 dob (2.3% roku), a "stosunek gry" w chwili decyzji jest ok. 30 razy wiekszy od stosunku murow (48.7 wobec 1.50) [P liczby]; ze to czlon czasu przewaza decyzje - [S], do sprawdzenia w kodzie gry;
- **(b) przewaga** - zaloga 120-280 ludzi + milicja wobec armii 2-3.5 tys.; zaden szturm (a9) nie szedl przy przewadze < 2:1, a 11 przy >= 16:1 [P];
- **(c) zaloga w polu** - zalogi przegrywaja bitwy pod murami jeszcze przed szturmem (17 bitew, 20.9 tys. ludzi); dlaczego zaloga wychodzi z murow - nie wiem. **Krytyka - poprawione:** **(c) bitwa pod murami w dniu szturmu** - strona twierdzy (zaloga, milicja i inne druzyny) przegrywa 17 z 31 bitew SiegeOutside, 13 z nich w dobie szturmu albo dzien przed, i wtedy twierdza pada (15 z 17); z armia w srodku wygrywa (14, zadna nie padla) [P]. Czy to faza szturmu w mechanice gry - [S], do sprawdzenia w kodzie (MapEvent SiegeOutside).
- **(d, krytyka) obronca nie wygrywa szturmu** - 0 z 73 (fin21 0 z 67); szturm konczy sie zdobyciem albo bez rozstrzygniecia [P].
Czego ten bieg nie rozstrzyga: jak gra liczy decyzje o szturmie (kod), czy zaloga dzien przed zdobyciem jest juz po stratach ostrzalu, dlaczego w fin30 szturm wygrywa czesciej niz w fin21 (75-77% wobec 57-66%) - zbiega sie z wieksza armia oblegajaca (2 724-2 903 wobec 2 211-2 534) przy podobnej zalodze [P liczby, S zwiazek]. (Krytyka: porownywalnie 77% wobec 66% - "57%" fin21 zawiera 9 linii-widm; roznica slaba statystycznie, rozdz. 1.3. Nierownosc sil jest taka sama w obu biegach: napastnicy / (zaloga + milicja) 9.95 wobec 10.0 [P] - to stala cecha swiata modow, nie skutek paczek fin30.)
Skutki uboczne do policzenia w projekcie [S]: trudniejsze oblezenia = mniej zdobyc, wiec krolestwa (Krainy Burzy, Dorne, KL) zyja dluzej i mapa wojen roku 2 sie zmienia - to wplywa na miare 85 tys. (rozdz. 2); linie O0 / O1 paczki okup daja gotowy pomiar dlugosci oblezen do nastepnego biegu.

## 2. Wojsko i prog 85 tys. (zrodlo: `wojsko` rozdz. 1-4)

### 2.1 Miara W5-7 [P]

| okno | fin30 miara | krolestw w wojnie | wojsko wszystkich krolestw | w tym krolestwa w pokoju | fin21 miara | fin30 - fin21 |
|---|---:|---:|---:|---:|---:|---:|
| d93-120 | 108.0 | 26.0 | 108.8 | 0.8 | 99.5 | +8.5 |
| d337-364 (koniec roku 1) | **103.4** | 25.0 | 104.8 | 1.4 | 108.9 | -5.5 |
| d421-448 | 96.4 | 24.4 | 101.4 | 4.9 | 109.7 | -13.3 |
| d505-532 | 73.2 | 22.0 | 85.1 | 11.9 | 47.7 | +25.5 |
| d701-728 (koniec roku 2) | **62.4** | 19.9 | 68.7 | 6.3 | 55.3 | **+7.1** |

| kwartal roku 2 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|
| fin30 miara (tys.) | 101.0 | 73.8 | 55.0 | 62.9 |
| fin30 krolestw w wojnie | 24.4 | 21.8 | 18.4 | 18.8 |
| fin21 miara | 110.4 | 59.9 | 56.7 | 55.1 |
| fin21 krolestw w wojnie | 25.9 | 14.0 | 14.7 | 15.1 |

- Minimum roku 2: 48.1 tys. w d550 (fin21 36.1 w d537); dob roku 2 z miara >= 85 tys.: 82 z 364, ostatnia d452 (fin21 92) [P]. Narzedzie `--grupa w5`: W5-7 62.4 - to samo [P].
- Krytyka: wlasny skrypt (`krytyka\k1_log.py`, linie Skarbce) daje to samo - fin30 108.05 / 103.38 / 96.44 / 73.24 / 62.40, fin21 d701-728 55.32, min roku 2 48.1 w d550, 82 doby >= 85 tys., ostatnia d452 [P]. Ale +7.1 tys. wobec fin21 nie da sie przypisac paczkom: zima w roku 2 trwala 100 dob w fin30 i 269 w fin21; dezercja z glodu druzyn lordow w roku 2 - fin30 11.9 tys., fin21 16.9 tys., za to w ostatnich 100 dobach fin30 9.4 tys. wobec 4.3 tys. [P]. Kierunek wplywu zimy na miare d701-728 - nie wiem [S].

### 2.2 Skad spadek d337-364 -> d701-728 - te same rody (budzet-rodow.csv) [P] (`wojsko` rozdz. 3)

| grupa rodow | fin30: rodow / ludzie w partiach | wypelnienie limitu | fin21: rodow / ludzie |
|---|---|---|---|
| w wojnie w obu oknach | 193 / 65 164 -> 59 041 (**-6.1 tys., -9.4%**) | 66.4% -> 60.7% | 176 / 56 544 -> 50 683 (-10.4%) |
| z wojny do pokoju | 132 / 34 681 -> 5 934 (**-28.7 tys.**) | 61.2% -> 17.9% | 132 / 38 125 -> 1 486 |

Rody w pokoju trzymaja dzis 17% ludzi (fin21 4%) - to Z.1 [P]/[S]. Te same rody w wojnie traca 9-10% w obu biegach; czy to pieniadz, czy straty w bitwach - nie wiem (ich kiesy rosna 40.5 -> 45.5 mln) [P].

### 2.3 Pokoje i wypowiedzenia (sesja CS T9 + E1) [P] (`wojsko` 4.1-4.2)

| | fin30 rok 1 | fin30 rok 2 | fin21 rok 1 | fin21 rok 2 |
|---|---:|---:|---:|---:|
| pokoje razem | 17 | 24 | 91 | 60 |
| - fabularne ROT (wojna zaraz wznowiona) | 10 | 3 | 79 | 32 |
| - glos rodow (E1 "POKOJ"; z tego nad naszym wnioskiem z biedy E1b) | 6 (1) | **21** (1) | 8 | 13 |
| - **zmeczenie wojna Diplomacy** | 1 | **0** | 4 | **15** |
| wypowiedzenia wojen razem | 34 | 30 | 112 | 65 |
| - powstanie krolestwa (bunt, hrabstwo BK) | 1 | **15** | 1 | 10 |

- **Fala roku 2** [P]: d435-457 8 pokojow (Dolina - Wyspy, Reach - Dorne, Krainy Burzy - Reach, Lys - Tyrosh, Qarth - Targaryen, Aegon - Volantis, Ibben - Sarnor, Norvos - Qohor), zaraz po koncu wymuszania wojen ROT w d433; d485-538 kolejne 7 - wszystkie glos rodow. W fin21 fala to 13 pokojow d449-513, 8 przez zmeczenie.
- Wypadek mniejszy niz w fin21, bo wojny Westeros trwaja caly rok 2 i doszlo 15 wojen buntow i hrabstw BK [P]. Krolestw: d2 29, d728 39 (fin21 35) [P].
- Diplomacy w calym biegu: wnioski gry ConsiderPeace 11 168, odrzucone "This war hasn't gone on long enough" 4 618 [P].
- **Krytyka [P]** (`krytyka\k3_pokoje.py`, wlasna klasyfikacja T9 + E1, te same liczby co wyzej): pokoje niefabularne (te, ktore naprawde konczyly wojne) fin30 7 + 21 = **28**, fin21 12 + 28 = **40**; spadek 151 -> 41 to w 98 ze 110 pokoje fabularne (111 -> 13), ktore ROT wznawial tego samego dnia. W oknie fali d435-538: fin30 15 niefabularnych (15 glosem), fin21 16 (9 zmeczenie + 7 glosem) - fali nie ubylo, glosy rodow zastapily zmeczenie. "Zmeczenie / 4.33 zadzialalo" jest prawda o Diplomacy, nie o liczbie wojen w roku 2.

### 2.4 Pulapka pokoju - krolestwa w pokoju na koniec [P] (`wojsko` 4.3)

| krolestwo | w pokoju od | wojsko d337-364 -> d729 | fin21 |
|---|---:|---|---|
| Dolina | d436 | 7 413 -> 785 | od d508, 26 |
| Lys | d440 | 1 504 -> 152 | od d451, 28 |
| Ibben / Sarnor | d453 | 1 861 -> 158 / 2 757 -> 158 | Sarnor od d477, 294 |
| Tyrosh | d494 | 1 673 -> 102 | - |
| Qarth | d520 | 3 272 -> 753 | od d637, 56 |
| Volantis | d535 | 3 559 -> 497 | - |
| Targaryen / Aegon | d536 | 3 270 -> 572 / 2 043 -> 371 | Targaryen od d684, 617 |
| Dorne | d538 | 4 742 -> 414 | od d628, 70 |
| Pentos | d688 | 1 405 -> 382 | - |

Krolestwo w pokoju zostawia ok. 10% wojska (fin21 ok. 1%) i nie wraca do wojny [P]; dlaczego nie wraca - log nie podaje powodu decyzji AI, nie wiem.

## 3. Zalogi i Z.1 (zrodlo: `wojsko` rozdz. 5) [P]

| okno | zalogi / miasta / zamki (tys.) | fin21 zalogi | Z.1: rody w pokoju / ich druzyny / ich zalogi | skrzynia / pulap pokoju (tys. zl/d) |
|---|---|---:|---|---|
| d337-364 | **91.9** / 51.2 / 40.7 | 89.3 | 5 / 1.2 / 1.3 | 6.6 / 10.5 |
| d505-532 | 78.1 / 44.3 / 33.9 | 63.5 | 85 / 11.4 / 14.5 | 105.4 / 145.6 |
| d701-728 | **65.4** / 36.1 / 29.3 | **58.2** | 126 / **6.1** / **11.6** | 64.8 / 95.1 |

- Grupa z1: 5 progow, 0 NIE; Z1-1 druzyny krolestw w pokoju 6.3 tys. przy celu 11.3-17.7 (INFO); szczyt 18.9 tys. w d533-560 [P]. Spadek pozniej [S]: skrzynia pokoju = 0.8 x (G - R) / 45 maleje z kiesami rodow w pokoju (17.9 -> 14.3 mln).
- Zalogi -26.5 tys. w roku 2 [P]: krolestwa w pokoju (Dolina -3.6, Dorne -3.0, Volantis -3.0 tys.), Krainy Burzy -6.7 (zniszczone), Myr i Norvos do 0, KL -3.2 (13 twierdz stracone); Smocza Skala +5.0 (twierdze Krain Burzy).

## 4. Pieniadz (zrodlo: `wojsko` rozdz. 6-7)

### 4.1 Brak na zold po W5, W5b i W6, srednia tys. zl/d [P]

| | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|
| Wolni Ludzie fin30 | 13.4 | 16.5 | 18.9 | 12.4 | 11.7 |
| Zelazne Wyspy fin30 | 17.1 | 18.8 | 19.9 | 9.3 | **0.0** |
| **razem fin30** | 53.6 | **62.4** | **58.8** | **39.1** | **22.9** |
| razem fin21 | 51.9 | 54.8 | 57.8 | 51.9 | 51.0 |

- Wolni Ludzie: potrzeba 15-16 tys. zl/d, danina ich ludu 2.2-2.3 tys., platnikow tarczowego brak [P] - workflow wyspy-wolni (decyzja Jeffa 14.10), nie ma go w tym biegu.
- **Zelazne Wyspy w Q7-Q8** [P]: dochod D ich rodow 19.5 -> 176.7 tys. zl/d, w tym od korony (d_korona) 7.5 -> 111.0 tys., jednorazowe 2.0 -> 23.6; kiesy rodow 0.84 -> 6.49 mln; wojsko 3.1 -> 7.3 tys. Skad te wplywy korony - **nie wiem**.
- **Krytyka:** spadek braku na zold Q6 -> Q8 (58.8 -> 22.9, -35.9 tys. zl/d) to w 19.9 tys. Zelazne Wyspy, 7.2 Wolni Ludzie, 8.8 reszta [P] - ponad polowa "poprawy" to niewyjasniony skok. Skarbiec Wysp stoi na 500.0-500.4 tys. (prog 1/360), a "zwrot zoldu" rosnie 1.6 tys. (d546) -> 29 tys. zl/d (d705) dla 10-11 rodow (7 probek linii Skarbce d465-728) [P]; czy to zloto ma platnika (zasada "nic z niczego") - nie wiem. To nie powinno stac w liscie jako poprawa ani jako sprawa nr 15 - do sprawdzenia przed wyspy-wolni (DP 15).

### 4.2 Strumienie koron, srednia tys. zl/d [P]

| strumien | fin30 rok 1 | fin30 rok 2 | fin30 d701-728 | fin21 rok 2 |
|---|---:|---:|---:|---:|
| danina P1 (minimum okna roku 2: 177.8) | 274.5 | 297.0 | 283.1 | 273.3 |
| W5 wyplacono | 59.4 | 35.5 | 53.3 | 32.9 |
| W7 stawka pana | 13.2 | 16.4 | 9.6 | 14.7 |
| 1/360 zapasu koron | 66.8 | **14.6** | 7.0 | 12.0 |
| niepokryte po W5 / W5b / W6 | 25.4 | 45.8 | 26.5 | 53.9 |

### 4.3 Skarbce, dlug, kiesy (mln) [P]

| okno | skarbce wszystkie / w wojnie | dlug koron w Banku | kiesy rodow | kiesy ludu | fin21: skarbce / dlug / kiesy ludu |
|---|---|---:|---:|---:|---|
| d337-364 | 25.85 / 22.88 | 0.45 | 58.12 | 26.70 | 24.34 / 0.94 / 20.80 |
| d701-728 | **16.04** / 7.68 | **1.98** (max 2.25 w d634) | 58.17 | **37.11** | 15.74 / 0.42 / 28.87 |

Dlug na d728: KL 1.01 mln, Pentos 0.69 mln, reszta <= 0.06 [P]; przy KL zgadza sie z utrata 13 twierdz i stolicy [S]; dokladna przyczyna - nie wiem.

### 4.4 Biedne rody w wojnie [P] (`wojsko` 7.3)

| | d337-364 | d533-560 | d701-728 | fin21 d701-728 |
|---|---|---|---|---|
| rody G<R: rodow / ludzie / G/R | 133 / 23.4 tys. / 0.60 | 77 / **10.6** / 0.48 | 91 / 16.7 / **0.45** | 17.5 tys. / 0.55 |
| klif G < 0.25 R: ludzie | 0.32 tys. | 1.70 | 1.98 | |

Saldo rodow bez luzu (okna 56 dob, tys. zl/d): d365-420 +28.6, d477-532 -39.4, d589-644 -19.9, **d645-700 -64.7, d673-728 -84.4** (alarm < -45; fin21 -20.6 i -14.9) [P]. Rody z luzem w tym samym oknie +73.1 [P]. Dlaczego koniec roku 2 - nie wiem.

### 4.5 Dziesiec progow alarmowych (rachunek na sucho rozdz. 7.6) [P]/[S]

5 TAK (1 miara 62.4; 3 biedne rody min 10.6 tys.; 5 saldo -64.7 / -84.4; 9 krolestw w wojnie 19.9; 10 Dorne 0.4 tys.), 3 NIE (2 kiesy ludu / danina; 4 klif; 6 skarbce / dlug - blisko: max 2.25 mln przy progu 3), 2 nie wiem (7 i 8 - N7 nie liczone). fin21: tez 5 TAK.

### 4.6 Okupy jencow-lordow [P]

2 511 okupow AI-AI za ok. 144 mln zl w 2 lata (rok 1 68.4, rok 2 75.6 mln); powtorne 28% (po 65% ceny); pojmano 4 409 lordow (6.9 na rod AI na rok); grupa m6a 12 progow, 0 NIE.

## 5. Ludzie (zrodlo: `ludzie` rozdz. 1-5, 7)

### 5.1 Ludnosc [P]

| | d1 | d364 | d728 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|
| fin30 ludnosc (mln) | 52.134 | 51.635 | 51.659 | -0.96% | +0.05% |
| fin21 | 52.134 | 51.600 | 51.618 | -1.02% | +0.03% |

Zmarli z glodu 0, glodne warownie 0 przez 728 dob; przyrost naturalny +173 / +195 tys., zabici 222 / 169 tys. (fin21 253 / 180) [P]. Crownlands -6.2% w roku 2 - King's Landing stracil 26 844 ludzi w dniu zdobycia [P].

### 5.2 Zima, glod druzyn, bandy [P]

- Zima od d629 (fin21 d460); dlugosc por ROT rozni sie miedzy kampaniami (lato 3 837 / 3 776 dni, jesien 432 / 324, zima 1 145 / 1 316) [P]; ze ROT losuje pory przy starcie - [S].
- Dezercja z glodu w druzynach lordow: **94 ludzi/d w d629-728** (poza zima 4-20/d; fin21 61-70/d od d460), ok. 9.4 tys. w 100 dobach [P]. W zimie dziala nasza regula WinterBite (druzyny jedza +43%, plon wsi -41%) [P wedlug kodu i logu]; ze ona wywoluje glod - [S]; ktore krolestwa traca najwiecej - nie wiem.
- Pula wyrzutkow 1 691 -> 5 262 w zimie; bandy na koniec 1 032 / 20 096 ludzi (progi etapu 4: pula <= 3 053, bandy <= 867 / 15 044) [P].

### 5.3 Uchodzcy [P]

Szczyt 141 821 w d423 (fin21 160 895), na koniec 83 200; w 2 lata nowych 271.9 tys., wrocilo 181.1 tys., zmarlo 2 536 [P].

### 5.4 Ludzie znikajacy z partiami [P]

5 055 w 2 lata (rok 1 2 874, rok 2 2 181; fin21 9 581); rownanie krain -8 903 (0.017%), najgorsza Nocna Straz -372 (-1.5%, od ok. d547). Linie diagnozy: 13 druzyn lordow skasowanych przez gre po bitwie z ludzmi w skladzie (`DestroyPartyAction` <- `MapEventSide.HandleMapEventEndForPartyInternal`) i 1 banda. Poprawka A17 jest po tym biegu - niesprawdzona.

### 5.5 Bohaterowie [P]

- Dorosli lordowie AI 1 201 -> 1 624 -> **1 969** (+64%; fin21 +60%) - nie z porodow (273 dzieci); skad - nie wiem. Klanow 332 -> 381, slubow 313, notable 3 110 -> 3 236.
- Mediana sumy umiejetnosci lordow AI **2 260 -> 1 432**, najmocniej w d687-715 (1 997 -> 1 479) przy +39 lordach; Tactics 158 -> 45, Engineering 47 -> 12 (fin21 2 125 -> 1 702) [P]. Przyczyna - nie wiem.
- Punkty postaci (grupa rozwoj): 8 progow, 0 NIE; do d325 AI 2 209 awansow, +713 punktow cech, +359 skupienia [P].
- Swaty ROT (latka CrashScribe Z.3): 467 -> 1 523 ms/dobe (ok. 12% doby; fin21 1 257) [P] - lista optymalizacji na koniec.

## 6. Ciaze (zrodlo: `ludzie` rozdz. 6, `paczki` rozdz. 3) [P]

| | d2-91 | d92-182 | d183-273 | d274-364 | d365-455 | d456-546 | d547-637 | d638-728 | razem |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| poczecia | 66 | 57 | 46 | 40 | 53 | 43 | 47 | 39 | **391** |
| porody | 0 | 0 | 0 | 64 | 59 | 46 | 40 | 52 | **261** (273 dzieci, 0 martwych) |
| poczecia na zamezna 18-45 na rok | 1.19 | 0.84 | 0.59 | 0.47 | 0.57 | 0.42 | 0.42 | 0.32 | **0.544** |

- Ciaza 273.0 doby (CR-2), pierwszy porod d275; mnoznik 0.2308 (84/364) dziala - poczecia / suma szans 1.02 (CR-4) [P].
- **CR-5 NIE: 0.544 przy progu <= 0.25**; bez mnoznika gra daje 2.32 na rok, wiec baza gry jest 2-3 razy wyzsza niz w symulacji (0.16-0.25) [P]; dlaczego - nie wiem (model plodnosci BK / ROT? [S]). Czestosc spada z liczba dzieci i z dojrzewaniem malzenstw [S]. Do decyzji: dodatkowy mnoznik ok. 0.45 [S] albo zmiana progu (0.3-0.5 na rok to plodnosc naturalna [S]).

## 7. Towary i chleb (zrodlo: `ludzie` rozdz. 8-9) [P]

| | fin30 | fin21 |
|---|---|---|
| miast bez plotna (z 97) od d274 | 1.4-3.1 | 72-85 |
| indeks ceny plotna (koniec) | 0.46 | 10.0 (sufit) |
| plotno do wojska szt./d | 47-80 (prog 75) | 10-26 |
| miast bez skory | 20-40, w zimie **39.5-44.6** | 70-85 |
| skora do wojska szt./d | 36-65 (prog 75) | |
| skory surowe szt./d (jesien -> zima) | 517 -> 360 (-30%) | 356 (rok 2) |
| miast bez drewna | 0.7-6.8 | 29-53 |
| zboze szt./d (do zimy / zima); zapas d364 -> d728 | 9.9 tys. / 7.0 tys.; 205 -> 109 tys. | 9.5 / 7.2 tys.; 173 -> 89 tys. |
| chleb swiata: d301-364 / d601-628 / d665-728 (cel 98%) | 96.5 / **89.7** / **88.2%** | 97.1 / 90.3 / 89.7% |
| miast < 0.95 pelnego koszyka (d728) | 8 (Braavos 0.20, Valysar 0.35, Volantis 0.51, Aquos Dhaen, Oldtown, Rhyos, Selhorys, Tyrosh) | 7 |
| robocizna ludu (zarobek z kas) rok 2 | 251 -> 206 tys. zl/d (-18%) | -24% |

- Biedota wielkich miast to nie sprawa zimy [S, mocne]: chleb spada do 89.7% juz w d601-628, przed zima fin30 [P]. Pelnej przyczyny nie wiem; robocizna -18% - [S]. Poprawki M3 (chleb dla glodnych miast) w tym biegu nie ma.
- Skora: garbarze bez surowca w 76 z 97 miast (d728; d365: 32) [P]; ze to zima i brak zwierzat na rzez - [S] (rozpoznanie towary-3: K5 zwierzeta pod regulator).
- Wartosc koszyka na 1 000 mieszczan +14% (fin21 +17%); indeksu cen jedzenia log nie ma - nie wiem [P].

## 8. Stabilnosc, czas, zapis, nowe paczki, Inni (zrodlo: `paczki` rozdz. 1-5) [P]

| | fin30 | fin21 |
|---|---|---|
| AUTOTEST KONIEC | OK 728/728, 158.0 min | OK 728/728, 149.8 min |
| ERROR / Exception / BLAD w logu Armoury; RC i GrandTourney | 0; 0 / 0 | 0; brak kopii |
| CS: ERROR CAUGHT FURTHER UP / nowe sygnatury / GAME HANG / Mends polknal | 25 (23 przy starcie + 2 w d367, BKROTPatch AmbiguousMatch) / 0 / 0 / 0 | 25 / - / 1 / 1 |
| s/dobe rok 1 / rok 2 | 12.96 / 13.10 | 12.43 / 12.27 |
| "nasze razem" rok 1 / rok 2 (ms/dobe) | 1 424 / 1 623 | 1 570 / 1 527 |
| pamiec procesu d728 | 1 762 MB | 1 478 MB |
| zapisy / plik koncowy / pole N7 max | 13 OK (4.8-6.0 s) / 31 649 060 B / 28 277 znakow (< 32 767) | 13 / 31 773 618 B / 34 057 |
| skoki Pieniadza swiata > 50 mln | 0 (straznik licznika podatku: 16 przepelnien pod 2^31 zdjetych, 1 336 napraw, wyjatki 0) | 5 (2.1-4.3 mld) |
| karawany wroga przy zdobyciu w murach / zajete; C-4b | 3 / 1; 0 | 0 / 0; 9 |

- Progi grup paczek (12 grup): 458 progow, 12 NIE + 1 DEC; fin21 tym samym narzedziem 422 / 21 + 1 DEC [P]. Grupy bez NIE: drobne2, drobne14, rozwoj, n7, lup, lup5b, m6a.
- Szpilki profilu `MenPurse.OnEntered` 4-14 s w 5 dobach roku 2 przy normalnej dlugosci doby - raczej blad stopera [S]; przyczyna nie wiem.
- **Inni (B5):** 616 -> 3 474 (d364) -> 5 498 (d728), fin21 8 925; nekromancja +5 926 (fin21 +10 631). Zew w d728 obnizyl premie limitu hordy (2 184 -> 2 042), D2-10 TAK. Ale banda Kylegg przekroczyla 1 140 juz w d234, w d728 ma 2 782 ludzi (2.4 x limitu) i rusza na Craster's Keep (8.7 x przewagi) - co dalej, nie wiem (bieg sie skonczyl) [P].
- Okno roku 1 (`--do-doby 364`): etap8 9 NIE + 1 DEC (dodatkowo Z-2, Z-3, N5-3 - w oknie 728 TAK) [P].

## 9. Lista NIE z ocena (okno 728 dob; 23 grupy, 634 wiersze, 34 NIE + 2 DECYZJA) [P]

Ocena: **BLAD** / **PROG** (prog do zmiany) / **ROK2** (stan swiata w roku 2) / **ZIMA** / **ZNANE** (jak w poprzednich biegach) / **SLABE** / **CECHA** / **CZAS** (optymalizacja na koniec) / **INFO** / **DECYZJA**. Kolumna "DP" = numer w liscie DO POPRAWY (rozdz. 10).

| # | grupa | wiersz | wartosc | prog | fin21 | ocena | DP |
|---|---|---|---|---|---|---|---|
| 1 | ciaza | CR-5 poczecia na zamezna 18-45 na rok | 0.544 (gra bez mnoznika 2.32) | <= 0.25 | paczki nie bylo | **BLAD** kalibracji, NOWE | 13 |
| 2 | etap8 | H-4b reszta uprzezy w ksiedze | -29 037 przy zrodlach 38 661 (-75%) | <= 10% | -81% | **BLAD** stary | 18 |
| 3 | etap8 | O5-2 sprzedaz zalog wobec Z33 (d1-120) | x0.35 szt. / x0.22 zl; utarg 865 zl/d | +-15% | x0.91 / x0.75; 1 407 zl/d | PROG + ZMIANA (przyczyna nie wiem) | 19 |
| 4 | etap8 | G1-5a suma kas miast wobec Z33 | +18.9% (d728) | 80-110% w d120 | +12.5% | PROG (okno) | 24 |
| 5 | etap8 | G1-5c kiesa na karawane wobec Z33 | +85.6% | +-20% w d120 | +99.2% | PROG (okno) | 24 |
| 6 | etap8 | Q-3 partia zawracana > 3 razy na dobe | 2 partio-doby, max 4 | 0 | 7 | PROG (dubel D2K-8, prog <= 4 - TAK) | 24 |
| 7 | etap8 | P3W-5 ludzie w partiach rodow AI | 67 984 | >= 70 000 | 56 969 | ROK2 (lepiej niz fin21) | 4 |
| 8 | etap8 | N5-2 skory surowe | 339/d, miast bez skory 44.4 | >= 430/d | 312/d | ROK2 + ZIMA | 11 |
| 9 | etap8 | N5-5 budowy: brak materialow | 36% | <= 30% | 30% | ZNANE | 23 |
| 10 | jeff14 | J5-0 kuznia jak u kowali (C5) | wylaczona | CZYNNA | - | CECHA (decyzja "C5 off") | - |
| 11 | szybkie | SZ-A8-1 jazda t3+ partii rodow AI | 13.0% (d699-728) | >= 15.4% (baza roku 1 - 1 pkt) | 12.2% | PROG (okno; w roku 1 16.6% TAK) | 24 |
| 12 | okup | OKP-1 szturm AI ma linie O0 w d-2..d | 53 z 73 bez | 0 | - | PROG (72 z 73 ma O0 3-7 dob wczesniej; wyjatek Harrenhal - fabula) | 24 |
| 13 | w5 | W5-4 wypelnienie rodow z doplata | 57.4% -> 53.2% | rosnie | NIE | ZNANE (przyczyna nie wiem) | 5 |
| 14 | kiesa1 | P1-1 lokata W d728 | 32.85 mln (22.23 w krolestwach bez poboru) | <= 6 mln | 25.08 | ZNANE -> DECYZJA danina w dlugim pokoju (M4) | 7 |
| 15 | kiesa1 | P1-6 chleb (d301-364) | 96.5% (rok 2: 88.2%) | >= 98% | 97.1% | **SLABE** | 9 |
| 16 | kiesa1 | P1-8 dlug koron d364 wobec d182 | 0.45 wobec 0.25 mln | d364 <= d182 | NIE | ZNANE | 6 |
| 17 | kiesa1 | P1-19 czas poboru daniny | 0.133 ms | < 0.1 ms | | CZAS | 22 |
| 18 | plotno | PL-3 plotno wywiezione | 58.0 szt./d | >= 60 | | PROG (na granicy) | 24 |
| 19 | plotno | PL-4 regulator lnu | x1.00 | >= 1.20 | | PROG (lnu nie brakuje: GLB-P5-3 TAK, stos 33 dni) | 12 |
| 20 | towary2 | T2-K2-2 plotno do wojska | 54.1 szt./d | >= 75 | | ROK2 / PROG (mniej ludzi na zoldzie 199 -> 135 tys.) | 12 |
| 21 | towary2 | T2-K2-5 regulator lnu | x1.00 | | | PROG (= PL-4) | 12 |
| 22 | towary2 | T2-K3-1 miast bez skory | 44.6 | <= 20 | 70-85 | ZIMA, NOWE (w d547-637 24.6) | 11 |
| 23 | towary2 | T2-K3-2 skora do wojska | 33.6 szt./d | >= 75 | | ZIMA | 11 |
| 24 | towary2 | T2-K3-3 cykle warsztatow bez skory | 59.4 | <= 50 | | ZIMA | 11 |
| 25 | gl2 | GL2-2 r (okno d91-120) | 0.66 | | | ZNANE (okno na sztywno) | 24 |
| 26 | gl2 | GL2-6 Qarth min dni (okno d91-120) | 2.4 doby | | | CECHA (Qarth) | - |
| 27 | gl2 | GL2-8 dni oszczednosci min (okno d91-120) | 41 | | | ZNANE (okno na sztywno) | 24 |
| 28 | gl2 | GL2-9 Braavos bez zaworu | 160 dob | | 220 | CECHA (kasa ponizej zapasu; D2K-5 TAK) - krytyka: Braavos to tez najbiedniejsze miasto (stac pelny koszyk 0.20, 1 dzien oszczednosci) - nie CECHA, tylko slad do DP 9-10 [S] | 9 |
| 29 | zbytki | ZB-2 kupione / oferta | 19.5% | | 25.2% | ZNANE, gorzej | 10 |
| 30 | zbytki | ZB-6 miast z kasa ponizej zapasu | 13 | <= 4 | 10 | ZNANE, gorzej | 10 |
| 31 | zbytki | ZB-7 bawelna | 2 029 | <= 2 000 | | PROG (na granicy) | 24 |
| 32 | zbytki | ZB-11 czas | 4.13 ms | | | CZAS | 22 |
| 33 | zbytki | ZB-13 czas obiegu kiesy | 6.78 ms | | | CZAS | 22 |
| 34 | glb | GLB-P4-2 rekruci p90/p10 | 28.0 (LevyByPeople off) | | 100.7 | INFO / CECHA - krytyka: wojsko krolestwa nie zalezy od ludnosci; to dotyczy "sily nie sa rowne", jesli Jeff mowi o krolestwach (DP 2) | 2 |
| D1 | kiesa1 | P1-3 kiesy ludu miast d728 | 37.10 mln (bez lokaty krolestw bez poboru 14.87) | <= 20 mln | | DECYZJA (jak P1-1) | 7 |
| D2 | etap8 | C-1e barda t6 | mediana 2 760 | <= 2 600 | to samo | DECYZJA stara | - |

- Grupy bez NIE (okno 728): w6, w7, z1, n7, m6a, lup, lup5b, drobne2, drobne14, rozwoj, gl1 [P].
- **Rozbieznosc okup:** `wojsko` ma "okup 14 progow, 0 NIE", `paczki` ma 1 NIE (OKP-1) - narzedzie szuka `Logs\<znacznik>\bitwy.log`, a tabele leza w `tabele-06-01-15`; bez opakowania (`paczki\run_tool.py`) OKP-1, OKP-3 i OKP-10 sa INFO "brak bitwy.log". Wynik wlasciwy: 1 NIE (OKP-1, PROG) [P].
- Ubylo wobec fin21: C-4b, P3-2, H-5b, P3W1-1, P3W1-3, P3W3-3, P3W1-6, P3W2-4, N5-3, D2K-7, GL1 winogrona [P].

## 10. DO POPRAWY przed testem koncowym

Kolumna "kto": paczka zrobiona juz po tym biegu (pieniadze roku 2 = M1, M2a-c, M3, B4; towary-3; A16; A17; strzaly; wyspy-wolni - projekt) - czy ja zalatwi, jest **niesprawdzone** do nastepnego biegu; albo **nowa praca**. Kolejnosc = waga dla gry [S].

| # | co | liczba [P] | przyczyna | kto |
|---|---|---|---|---|
| 1 | **Oblezenia za latwe (i wedlug Jeffa za szybkie)** (uwaga Jeffa) | szturm wygrywa 77% (rok 2 83%), obronca 0 z 73; decyzja o szturmie po 8.4 doby (max 12.7) - wzorca dlugosci brak; 57 zdobyc 48 twierdz w 2 lata (fin21 39 / 34), stolica 7 razy w 4 krolestwach | [P z linii O0] wykladnik przewagi murow spada z czasem (+1.08 po 4.2 dobach, -1.66 po 8.4, -4.22 po 12.7); 57 ze 113 decyzji przy murach < 1.5 - gra idzie do szturmu wbrew wlasnej mierze murow; kodu nie sprawdzano | **nowa praca** - w toku workflow oblezenia-i-sily (`docs/oblezenia/`, wynik `PROJEKT-WYBRANY.md`); okup bierze tylko mury < 1.5 (zabral ok. 9 zdobyc [S]), 44 z 55 zdobyc szlo przy murach >= 1.5 |
| 2 | **Sily nierowne: zaloga wobec armii** (i byc moze krolestwa wobec krolestw - zapytac Jeffa) | zaloga 290-400 na twierdze (65-92 tys. na 227); w szturmie 462 na 2 724-2 903 (6.1-6.7:1, nigdy < 2:1 w a9, 11 razy >= 16:1); dzien przed zdobyciem 10:1 - **tak samo w fin21 (10.0, szturm 5.9:1)**; krolestwa: Reach 10.0 wobec Dorne 4.7, Polnoc + Dorzecze 18.2 wobec KL 9.1 tys. - wedlug tego padaly twierdze (Reach 8 z Dorne, Polnoc + Dorzecze 13 z KL); GLB-P4-2 rekruci na 1 000 ludzi roznia sie 28 razy (LevyByPeople off) | nie wiem; stala cecha swiata modow, nie skutek paczek fin30 [P/S] | **nowa praca** (ten sam workflow); B4 (zalogi w pokoju 70%) dotyczy krolestw w pokoju, nie twierdz pod oblezeniem [S] |
| 3 | **Bitwa pod murami w dniu szturmu** (bylo: "zalogi gina w polu") | strona twierdzy (zaloga + milicja + inne druzyny, partii mediana 5) przegrala 17 z 31, 20.9 tys. ludzi tej strony; 13 z 17 w dobie szturmu albo dzien przed, 15 z 17 twierdz padlo; z armia w srodku (partii ok. 31) wygrala 14, zadna nie padla; 5 z 17 przegranych przy <= 2:1; KL d530: 1 156 + 227 w dwoch bitwach tego dnia, szturm na 92 | [S] faza szturmu w mechanice gry (SiegeOutside) - do sprawdzenia w kodzie; dlaczego strona twierdzy przegrywa przy rownych silach - nie wiem | **nowa praca** (ten sam workflow) |
| 4 | **Wojsko roku 2 pod 85 tys. (pulapka pokoju)** | 62.4 tys. w d701-728 (-22.6 tys.); rody z wojny do pokoju -28.7 tys.; krolestwa w pokoju nie wracaja do wojny (Dolina 293 doby); P3W-5 67 984 | pokoje d435-538 po koncu wymuszania ROT w d433 (glosy rodow) [P]; powrotu do wojny brak - nie wiem | pieniadze roku 2 - nie (projekt sam szacuje +0.2-0.5 tys. ludzi [S]); **nowa praca**: powrot krolestw z pokoju do wojny |
| 5 | Z.1 nie trzyma celu do konca | druzyny krolestw w pokoju 6.3 tys. przy celu 11.3-17.7 (szczyt 18.9 w d533-560); W5-4 57.4 -> 53.2% | [S] skrzynia pokoju maleje z kiesami (17.9 -> 14.3 mln); W5-4 nie wiem | pieniadze roku 2 (B4) czesciowo - zalogi, nie druzyny [S]; reszta **nowa praca** |
| 6 | Pieniadz biednych rodow w wojnie na koncu roku 2 | saldo -64.7 i -84.4 tys. zl/d (alarm -45; fin21 -20.6 / -14.9); G/R 0.45; dlug koron 1.98 mln (max 2.25; KL 1.01, Pentos 0.69); P1-8 | nie wiem (KL - utrata 13 twierdz [S]) | **pieniadze roku 2** (M1 zold przed rentami, M2b rata po wlasnych zolnierzach) - moze |
| 7 | Lokata ludu w krolestwach bez poboru | P1-1 22.23 mln z 32.85 mln; P1-3 kiesy ludu 37.10 mln | korona w pokoju nie bierze daniny - projekt | **DECYZJA Jeffa**: danina w dlugim pokoju (M4) - warunek projektu pieniadze roku 2 "> 8 mln" spelniony drugi raz |
| 8 | Wolni Ludzie bez pieniedzy na zold | 11.7-18.9 tys. zl/d caly rok 2; W7 pokrycie 19% | [P] potrzeba 15-16 tys., danina ich ludu 2.2-2.3 tys., platnikow tarczowego brak | **wyspy-wolni** (Wolni Ludzie bez zoldu - decyzja Jeffa 14.10) |
| 9 | Biedota wielkich miast | chleb swiata 88.2% (cel 98%); 8 miast < 0.95, Braavos 0.20; P1-6 | [S] robocizna ludu -18%; pelnej przyczyny nie wiem; nie zima | **pieniadze roku 2** (M3 chleb dla glodnych miast) |
| 10 | Kasy wielkich miast i zbytki | ZB-6 13 miast z kasa ponizej zapasu (fin21 10); ZB-2 19.5% (fin21 25.2%) | nie wiem | pieniadze roku 2 (M3) moze [S]; inaczej **nowa praca** |
| 11 | Skora | miast bez skory 44.6 (prog 20), skora do wojska 33.6/d (75), cykle bez skory 59.4 (50); N5-2 339/d (430) | [S] zima - skory surowe -30%; zwierzeta na rzez nie pod regulatorem | **towary-3** (K5 zwierzeta pod regulator, markietani) |
| 12 | Plotno do wojska, regulator lnu | T2-K2-2 54.1/d (75); PL-3 58.0 (60); PL-4 / T2-K2-5 x1.00 | [P] druzyny w polu kupuja odziez tylko przy wyjezdzie z miasta (rozpoznanie towary-3); lnu nie brakuje - prog | **towary-3** (markietani SutlerClothing; prog regulatora lnu) |
| 13 | Ciaze za czeste | CR-5 0.544 poczecia na zamezna na rok (prog <= 0.25); bez mnoznika 2.32 | baza gry 2-3 x wyzsza niz w symulacji - dlaczego, nie wiem | **nowa praca** (decyzja: mnoznik ok. 0.45 [S] albo prog) |
| 14 | Glod druzyn lordow w zimie, bandy | dezercja z glodu 94 ludzi/d (ok. 9.4 tys. w 100 dobach); pula 5 262 (prog 3 053), bandy 20 096 (prog 15 044) | [S] nasza regula WinterBite (+43% jedzenia druzyn, -41% plonu); ktore krolestwa - nie wiem | **nowa praca** / decyzja Jeffa o WinterBite; towary-3 K5 (wiecej miesa) moze troche [S] |
| 15 | Zelazne Wyspy - skok wplywow od korony (krytyka: mozliwy blad "nic z niczego", podniesc przed 4-14) | d_korona 7.5 -> 111 tys. zl/d w Q7-Q8; kiesy 0.84 -> 6.49 mln; brak na zold 0 w Q8 = 19.9 z 35.9 tys. zl/d "poprawy" braku na zold Q6 -> Q8; skarbiec korony stoi na 500 tys., zwrot zoldu 1.6 -> 29 tys. zl/d | nie wiem | **nowa praca** (rozlozyc d_korona na zrodla, sprawdzic platnika) - wazne dla wyspy-wolni, bo zmienia punkt startu, i dla oceny pieniedzy roku 2 |
| 16 | Ludzie znikajacy z partiami | 5 055 w 2 lata; rownanie krain -8 903; Nocna Straz -372 (-1.5%) | [P] gra kasuje druzyne lorda po bitwie z ludzmi w skladzie; Straz - nie wiem | **A17** (DestroyedPartyMenRouted); Straz - nie wiem |
| 17 | Lordowie: przybywa, umiejetnosci spadaja | dorosli lordowie AI +768 (+64%); mediana sumy umiejetnosci 2 260 -> 1 432, Engineering 47 -> 12 | nie wiem | **A16** (linia zgonow i narodzin bohaterow) - pokaze skad; spadek umiejetnosci - **nowa praca** (diagnoza) |
| 18 | Ksiega uprzezy | H-4b -75% (fin21 -81%, Z33 -82%) | nie wiem, gdzie uprzaz znika (D2K-6: zbrojownie DTE +4 887, polki +2 599) | **nowa praca** (stary blad) |
| 19 | Sprzedaz zalog | O5-2 x0.35 szt. / x0.22 zl wobec Z33; utarg 865 zl/d wobec 1 407 w fin21 (-39%) | nie wiem | **nowa praca** (diagnoza) + prog do przepisania |
| 20 | Inni ponad limitem hordy | Kylegg 2 782 ludzi (2.4 x 1 140) od d234; w d728 rusza na Craster's Keep | [P] B5 blokuje tylko podnoszenie premii Zewu, bandy nie zmniejsza | **nowa praca** / decyzja (czy B5 ma ciac bande) |
| 21 | Gra wolniejsza, wiecej pamieci | 13.0 wobec 12.3 s/dobe (+5%); pamiec d728 1 762 wobec 1 478 MB; szpilki MenPurse 4-14 s w 5 dobach | nasze mody kosztuja tyle samo; reszta nie wiem; szpilki - [S] stoper | **nowa praca** - optymalizacja na koniec (decyzja Jeffa 08.10); szpilki stopera - drobne |
| 22 | Czas paczek i swatow | Swaty ROT 1 523 ms/dobe (12% doby); ZB-13 6.78 ms, ZB-11 4.13 ms, P1-19 0.133 ms | rosnie liczba lordow szukajacych (1 165 -> 1 730) [P]; reszta nie wiem | **nowa praca** - optymalizacja na koniec |
| 23 | Budowy bez materialow | N5-5 36% (prog 30%; fin21 30%) | nie wiem | **nowa praca** (znane, niski priorytet) |
| 24 | Narzedzie progow | progi w zlym oknie: G1-5a, G1-5c, SZ-A8-1, OKP-1, Q-3 (dubel D2K-8), O5-2, PL-3, ZB-7; okna na sztywno: GL2-2/6/8, kiesa1 P1-2/4/6/7/8; nie znajduje tabel / bitwy.log w ukladzie `tabele-<znacznik>`; LUP-11 = 1.00 drugi bieg z rzedu (czy czula - nie wiem); krytyka: skrypt `analiza2` liczy szturmy-widma (oblegajacy `lord`, 0 ludzi: fin30 2, fin21 9) - przyszly pomiar szturmow bez nich | [P] | **nowa praca** (narzedzie) - przed testem koncowym, inaczej test da falszywe NIE |
| 25 | Strzaly (do obserwacji, bez NIE) | pokrycie strzal 69% (fin21 74%), na polkach 3 465 (4 632), zloto zakupow -25% | nie wiem | **strzaly** |
| 26 | (krytyka) Porownanie roku 2 z fin21 nieczyste | zima w roku 2: fin30 100 dob (od d629), fin21 269 (od d460); dezercja z glodu w roku 2 11.9 wobec 16.9 tys., w ostatnich 100 dobach 9.4 wobec 4.3 tys.; wojen w roku 2 wiecej (Q6-Q8 18-22 wobec 14-15 krolestw) | [P] los kampanii (pory ROT, bunty) | metoda: +7.1 tys. W5-7, wiecej zdobyc i spadek braku na zold nie przypisywac paczkom bez drugiego biegu z ta sama zima albo bez rozbicia na krolestwa [S] |
| 27 | (krytyka) Fala pokojow roku 2 - glosy rodow zamiast zmeczenia | d435-538: 15 pokojow glosem (fin21 16: 9 zmeczenie + 7 glos); pokoje konczace wojne 28 wobec 40 w 2 lata | [P] po koncu wymuszania ROT w d433 rody glosuja za pokojem; dlaczego - nie wiem | dolaczyc do DP 4 (**nowa praca**: powrot do wojny i glos rodow o pokoj) |

## 11. Czego nie wiem

- Jak gra liczy decyzje o szturmie (czlon czasu), dlaczego zalogi wychodza w pole, czy zaloga dzien przed zdobyciem jest po stratach ostrzalu, dlaczego szturm wygrywa czesciej niz w fin21, dlaczego od d646 do d728 nie padla zadna twierdza.
- Dlaczego krolestwa w pokoju nie wracaja do wojny (log nie podaje powodu decyzji AI); czy pokoje d435-457 przyszly, bo wojny przestaly byc fabularne w d433 (zgodne z data - [S]).
- Skad wplywy korony rodow Zelaznych Wysp w Q7-Q8; dlaczego saldo rodow bez luzu spada do -84 tys. zl/d na koncu roku 2; skutek N7 w mierze (alarmy 7 i 8).
- Skad +768 doroslych lordow AI i dlaczego ich umiejetnosci spadaja o 1/3; liczba bohaterow i zgonow na koniec (linii nie ma - A16).
- Dlaczego baza szansy poczecia jest 2-3 x wyzsza niz w symulacji.
- Czy glod druzyn w zimie to WinterBite, czy brak jedzenia na targach; ktore krolestwa traca najwiecej.
- Dlaczego cala gra jest o 5% wolniejsza i zajmuje +284 MB; czy szpilki MenPurse to stoper.
- Co Inni zrobili po d728; czy zapis wczytuje sie bez straty; sciezki gracza (okup O2/O3, okno lupu L5, pojmanie C4, Qohor) - autotest ich nie dotyka.
- ~~Ktore bitwy roznia skrypty `wojsko` a9 i `paczki` analiza2 (70 wobec 73 szturmow).~~ Krytyka: wyjasnione - linie-widma z oblegajacym `lord` i 0 ludzi (rozdz. 1.3); zostaje 1 bitwa (70 wobec 71) - nie wiem.
- (krytyka) Czy bitwa pod murami (SiegeOutside) w dniu szturmu to czesc szturmu w mechanice gry i dlaczego strona twierdzy przegrywa ja takze przy rownych silach; czy zloto Zelaznych Wysp (zwrot zoldu 29 tys. zl/d przy skarbcu stojacym na 500 tys.) ma platnika.
- Ceny zywnosci (logu brak indeksu); ze zima przyjdzie w innej dobie w kampanii Jeffa - [S].

## 12. Pliki i polecenia

- Raporty: `SCRATCH\wojsko\RAPORT.md`, `SCRATCH\ludzie\RAPORT.md`, `SCRATCH\paczki\RAPORT.md`.
- Wyniki narzedzia progow: `SCRATCH\wojsko\progi\sl_{w5,w6,w7,z1,n7,m6a,lup,lup5b,okup,kiesa1}.out.txt`; `SCRATCH\ludzie\sl\{ciaza,rozwoj,drobne2,plotno,towary2,gl1,gl2,kiesa1,zbytki,glb}-728.txt`; `SCRATCH\paczki\grupa-*.txt`, `por\f21-*.txt` (fin21 tym samym narzedziem), `por\d364-*.txt` (okno roku 1).
- Oblezenia: `SCRATCH\wojsko\skrypty\a9_bitwy_oblezen.out.txt` (szturmy, pod murami, wypady, przegrane szturmy), `a12_kto_kogo.out.txt`, `a15_okupy.out.txt`, `a11_oblezone.out.txt`; `SCRATCH\paczki\analiza2-fin30.txt`, `okp1-fin30.txt`; `SCRATCH\ludzie\zdobycia30.md`, `zdobycia21.md`.
- Narzedzie: `python -I <SCRATCH ae222cb7>\zb\int\tools\sprawdz_logi.py --grupa <g> --do-doby 728 <log>` (w5 / w6 / w7 z `--csv budzet-rodow.csv`, z1 z `--baza-doby` fin21; grupa okup przez `SCRATCH\paczki\run_tool.py`, ktory podmienia sciezke tabel). Polecenia skryptow - rozdz. 11 `wojsko`, rozdz. 14 `ludzie`, rozdz. 9 `paczki`.
- `SCRATCH\ludzie\log\` - kopia logu, sesji CS i tabel (ok. 580 MB, md5 zgodne z D:) - mozna usunac po pracy.
- Kontekst: `docs/STAN-PRAC.md` (wpis 15:41 UTC 14.10 - bieg fin30 i workflow oblezenia-i-sily), `docs/pieniadze-rok-2/PROJEKT-WYBRANY.md`, `docs/towary-2/ROZPOZNANIE-TOWARY-3.md`, `docs/reszta-2026-10-14/ROZPOZNANIE-A16.md`, `ROZPOZNANIE-A17.md`, `docs/wyspy-wolni/PROJEKT-WYSPY-WOLNI.md`.
- Krytyka: `SCRATCH\krytyka\k1_log.py` (W5-7, O0, L0, L5b-1, dezercja z glodu; wyniki `k1_fin30.out.txt`, `k1_fin21.out.txt`), `k2_bitwy.py` (szturmy, SiegeOutside, wypady; `k2_fin30.out.txt`, `k2_fin21.out.txt`), `k3_pokoje.py` (pokoje T9 + E1 obu biegow; `k3.out.txt`), `k4_okup.py` (decyzje O0 -> zdobycie wedlug murow i okupu, szturmy bez linii-widm; `k4.out.txt`), `k5_wypady.py` (czy twierdza padla po wypadzie / bitwie pod murami; `k5.out.txt`).

## 13. Krytyka (14.10, niezalezne przeliczenie z logu)

Sprawdzone wlasnymi skryptami (nie skryptami trzech raportow) na logu, bitwy.log i sesji CS obu biegow [P]:

| liczba | w syntezie | krytyk | wynik |
|---|---|---|---|
| W5-7 d701-728 (fin30 / fin21) | 62.4 / 55.3 tys. | 62.40 / 55.32; min roku 2 48.1 w d550; 82 doby >= 85 tys., ostatnia d452 | zgodne |
| pokoje roku 2 ze zmeczenia (fin30 / fin21); pokoje razem | 0 / 15; 41 / 151 | 0 / 15; 41 / 151 (rok 2 fin30: 21 glosem, 3 fabularne) | zgodne; interpretacja poprawiona (nizej 4) |
| dob do decyzji o szturmie (O0) | mediana 8.4, max 12.7 | 8.40 (p10 6.3, p90 10.8), max 12.7; 113 decyzji | zgodne |
| zdobycia (L0) i stolice (L5b-1) | 57, 7 | 57 (55 szturm, 2 wypad; 31 / 26; kwartaly 5/9/8/9/10/6/9/1), 7 | zgodne; nazwanie poprawione (nizej 3) |
| szturmy: wygrywa oblegajacy; stosunek ludzi | 75-77%; 6.1-6.7 | wszystkie linie 55 z 73 (75%), 6.25; bez linii-widm 55 z 71 (77%), 6.68 | zgodne; fin21 poprawione (nizej 2) |
| bitwy pod murami: strona twierdzy przegrala | 17 z 31, 20.9 tys. "ludzi zalog" | 17 z 31, 20 948 ludzi - ale calej strony twierdzy (partii mediana 5, do 38) | liczba zgodna, nazwa bledna (nizej 1) |
| dezercja z glodu w zimie | 94 ludzi/d | 94.0 (d629-728, suma 9 403) | zgodne |

**Czy wniosek stoi:** tak, w czesci "oblezenia sa za latwe" - nawet mocniej, niz pisze synteza (obronca nie wygral zadnego szturmu w obu biegach; twierdza bez armii w srodku pada w 15 z 17 przypadkow) [P]. "Za szybkie" to ocena bez wzorca [S]. "Sily nierowne" stoi, ale to stala cecha (fin21 tak samo), nie pogorszenie fin30. Reszta syntezy (wojsko 85 tys., pieniadz, ludzie, ciaze, towary, progi) - liczby sprawdzone wyrywkowo sie zgadzaja; zastrzezenia do przypisania przyczyn nizej.

Przesady i przemilczenia (poprawione w tekscie, oznaczone "krytyka"):
1. **"Zalogi gina w polu przed szturmem" / "20.9 tys. ludzi zalog" / "KL: zaloga 1 383 rozbita wczesniej"** - zle nazwane. W bitwach SiegeOutside atakuje armia oblegajaca, a po stronie twierdzy stoja zaloga, milicja i inne druzyny (partii mediana 10; zaloga + milicja to 2 partie). 13 z 17 przegranych jest w dobie szturmu albo dzien przed, 15 z 17 twierdz pada; KL: obie bitwy i szturm w tej samej dobie, a zaloga + milicja dzien wczesniej to 505, nie 1 383 [P]. To raczej faza szturmu niz "wyjscie zalogi z murow" [S]. Poprawione: 0.4, 1.4, 1.7 (c), DP 3.
2. **fin21 "57%" i "9 szturmow < 2:1"** - linie-widma (oblegajacy `lord`, 0 ludzi, 9 w fin21) w skrypcie `analiza2`; porownywalnie 77% wobec 66%, roznica statystycznie slaba. Rozbieznosc skryptow 70/73 wyjasniona (zostaje 1 bitwa). Poprawione: 1.3, 1.7, 11, DP 24.
3. **"57 twierdz zmienilo pana (co czwarta)" i "padlo 7 stolic"** - 57 zdobyc to 48 roznych twierdz (co piata); 7 upadkow stolicy to 4 krolestwa (Braavos 3 razy z rzedu, KL 2 razy). Wiecej zdobyc niz w fin21 wypada w roku 2, gdy w fin30 wojowalo wiecej krolestw - to nie dowod, ze oblezenia staly sie latwiejsze [P/S]. Poprawione: 0.3, 1.1, DP 1.
4. **"Zmeczenie / 4.33 zadzialalo: 41 pokojow zamiast 151"** - przesada w liczbie: 98 ze 110 ubytku to pokoje fabularne wznawiane tego samego dnia; pokojow konczacych wojne 28 wobec 40, a w fali d435-538 15 wobec 16 - glosy rodow zastapily zmeczenie [P]. Poprawione: 0.8, 2.3, DP 27.
5. **"Okup dziala, ale zdobyc nie ubylo"** - porownanie dwoch biegow z inna liczba wojen. W tym biegu okup zabral ok. 9 zdobyc [S], a 44 z 55 zdobyc szlo przy murach >= 1.5, gdzie okupu z zalozenia nie ma [P]. Poprawione: 0.5, 1.6, DP 1.
6. **"Brak na zold spadl 62 -> 23"** podany jako poprawa - ponad polowa (19.9 z 35.9 tys. zl/d) to Zelazne Wyspy przez niewyjasniony skok wplywow korony (skarbiec stoi na 500 tys., zwrot zoldu do 29 tys. zl/d) - to sprawa do sprawdzenia ("nic z niczego"), nie poprawa [P/S]. Poprawione: 0.10, 4.1, DP 15.
7. **"+7.1 tys. wobec fin21"** - zima w roku 2 trwala 100 wobec 269 dob, wojen bylo wiecej; czesci tej roznicy nie da sie przypisac /4.33 ani Z.1 [S]. Poprawione: 0.7, 2.1, DP 26.
8. **"Za szybkie" i "ten sam czynnik 4.33"** - bieg nie ma wzorca dlugosci oblezenia; 4.33 to podpowiedz projektowa, nie pomiar. Za to linia O0 pokazuje, jak czas dziala w decyzji gry (wykladnik przewagi murow z +1.08 do -4.22; 57 ze 113 decyzji przy murach < 1.5, 22 przy < 1.0) [P] - to konkret dla workflow oblezenia-i-sily. Poprawione: 0.2, 1.2.
9. **Cecha zamiast sladu** - GL2-9 "Braavos bez zaworu 160 dob" (CECHA) dotyczy najbiedniejszego miasta (0.20) - slad do DP 9; GLB-P4-2 (LevyByPeople off, INFO/CECHA) - dotyczy "sily nie sa rowne", jesli Jeff mowi o krolestwach. Poprawione: rozdz. 9 (#28, #34).
10. Drobne: "16 szturmow przegranych przez oblegajacego" - to szturmy bez rozstrzygniecia (obronca nie wygral zadnego) [P]; "twierdza z armia w srodku sie broni" - teraz [P]: po 42 wygranych wypadach i 14 wygranych bitwach pod murami zadna z tych twierdz nie padla w 14 dob.

Nie sprawdzalem: liczb pieniadza rodow i koron (saldo -64.7 / -84.4, dlug 1.98 mln), ludnosci, ciaz, towarow, progow narzedzia (zgodnosc sum 23 grup / 634 wierszy / 34 NIE + 2 DEC - policzona z tabel raportow, zgadza sie), kodu gry dla SiegeOutside. Gra nie uruchamiana, kod bez zmian, bez commita.
