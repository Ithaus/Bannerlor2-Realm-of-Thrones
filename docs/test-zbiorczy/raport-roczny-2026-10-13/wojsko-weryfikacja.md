# WERYFIKACJA rozdzialu WOJSKO (plik analityka: wojsko.md)

Praca tylko do odczytu. Nic nie uruchamialem, nic nie zmienialem.
Policzylem od zera wlasnymi skryptami: raport-rok\skrypty\wojsko-wer\ (w00..w15, kazdy ma plik .out.txt).
Skrypty analityka czytalem dopiero po wlasnych wynikach, tylko zeby wyjasnic roznice.

Oznaczenia:
- LOG = D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log, Lnnn = numer linii.
- LOG34 = ...\Armoury-2026-10-13_02-15-21.log (poprzedni bieg).
- CSV = ...\Logs\2026-10-13_06-24-10\budzet-rodow.csv (kolumna dzien 108836 = doba 1, 109199 = doba 364).
- BITWY = ...\Logs\2026-10-13_06-24-10\bitwy.log.
- [P] = pomiar, [S] = szacunek (rachunek na pomiarach).
- Srednia 28 dob = doby 337-364.

## Najkrocej

1. Liczby analityka sa policzone rzetelnie. Z 24 sprawdzonych 21 wyszlo mi tak samo, 2 prawie (renty, liczba krolestw w wojnie), 1 inaczej (najemnicy). Przy 4 zgodnych liczba jest dobra, ale wniosek z niej za mocny albo opis bledny (nachylenie, renty w czasie, koszt progu, wypady).
2. Glowny wniosek stoi: wojska w polu jest 79.4 tys., prog 85 tys. nie wyszedl, 71% luki to pulap zoldu, nie brak ludzi.
3. Piec rzeczy trzeba poprawic, zanim to pojdzie do Jeffa:
   - **Koszt progu jest zanizony.** "30-45 tys. zl dziennie" nie liczy dziury: 93 rody juz dzis placa 31 tys. zl dziennie ponad swoj pulap. Z zasypaniem dziury tansza droga do 85 tys. to ok. 50 tys. zl dziennie [S].
   - **Niespojnosc H jest opisana odwrotnie.** W dobie 364 to Dorzecze oblegalo, a wypad zrobilo i wygralo King's Landing. W calym roku nie bylo ani jednego przegranego wypadu: 38 wypadow, 38 wygral ten, kto wyszedl z twierdzy. Oblegajacy stracili w nich 55 tys. ludzi. Naprawa "przegrany wypad wraca za mury" nie zmienilaby nic.
   - **Zrodlo "lud odklada 56-70 tys. dziennie" gasnie samo.** Przyrost kies ludu spada caly rok: 132 -> 116 -> 84 -> 75 -> 70 tys. zl dziennie. Nie da sie obiecac, ze tyle bedzie mozna brac trwale.
   - **Podatek zbiera korona u siebie, a pieniadz i potrzeba leza w roznych krolestwach.** Polowa najtanszej drogi do 85 tys. to Zelazne Wyspy, Wolni Ludzie i Ibben - tam lud miast prawie nic nie odklada.
   - **Najemnicy: 74% umowy to pomylka miar.** Ta sama linia logu daje 82%.

## 1. Liczby: analityk a moje

| nr | co | analityk | moje | zgodne? | zrodlo mojej liczby |
|---|---|---|---|---|---|
| 1 | druzyny krolestw w wojnie, srednia 28 dob | 79 409 | 79 409 [P] | TAK | linie "Skarbce", LOG L119481-L129131; w02 |
| 2 | to samo, ostatnia doba / srednia bez niej / mediana | 73 578 / 79 631 / 79 857 | 73 578 / 79 631 / 79 596 [P] | TAK (mediana: analityk wzial gorny z dwoch srodkowych wyrazow, roznica 261) | LOG L129103-129131; w02, w08 |
| 3 | poprzedni bieg, srednia 28 dob; roznica | 64 013; +24% | 64 013; +24.0% [P] | TAK | LOG34 linie "Skarbce" L119236-L128894; w02 |
| 4 | szczyt | 108 131 w dobie 68 | 108 131 w dobie 68 [P] | TAK | LOG L24069; w02 |
| 5 | nachylenie od doby 183 | -36 ludzi na dobe | -35.7 (doby 183-364) [P] | TAK co do liczby; ksztalt inny - patrz pkt 2, A | w02, w08 |
| 6 | rody na pulapie w dobach 91/182/273/364 | 72 / 91 / 96 / 110 | 72 / 91 / 96 / 111 [S na danych P] | TAK | CSV, definicja analityka; w04 |
| 7 | wypelnienie druzyn: rody na pulapie a rody z wolnym miejscem | 38% a 64% | 37.9% a 63.9% (111 i 86 rodow) | TAK | CSV doby 337-364; w04 |
| 8 | luka do limitu trzymana pulapem | 69 952 z 98 827 = 70.8% | 69 930 z 98 610 = 70.9% [S] | TAK | CSV; w06 |
| 9 | rody, ktorych zold zalog przekracza caly pulap | 16 rodow, 58 ludzi z limitu 9 766 | 16 rodow, 58 ludzi, limit 9 766 [P] | TAK | CSV doba 364; w06 |
| 10 | zalogi a druzyny krolestw, doba 364 | 83 593 a 74 456 | 83 593 a 74 456 [P] | TAK; ale srednio 28 dob: 83 593 a 80 011 | LOG L129266, L129103-129131; w07 |
| 11 | kiesy ludu miast | 11.0 -> 44.1 mln; +70 tys./dobe; lokata +56 tys. | 10.96 -> 44.03 mln (linia "Kiesa ludu") albo 11.03 -> 44.12 mln (linia "Pieniadz swiata"); +70 135; +56 429 [P] | TAK | LOG L10837, L10804, L129238, L129205; w07 |
| 12 | skarbce krolestw | 54.6 -> 27.1 mln | 54 566 878 -> 27 108 168 [P]; w dobie 1 bylo 57.9 mln | TAK | LOG L10804, L129205, L707; w07 |
| 13 | doplata ze skarbcow; renty | 111 593 -> 35 105; 309 836 -> 140 689 | te same [P] | liczby TAK; wniosek o rentach za mocny - patrz pkt 2, C | LOG L10706, L129101; w07 |
| 14 | podatek wojenny ludu | 15 tys./dobe, pulap 3%, 20 z 26 krolestw na pulapie | 14 908 w dobie 364; srednio 15 020; srednia stawka 2.37%; 20 na pulapie [P] | TAK | LOG L129100; w07 |
| 15 | renty: ile trafia do rodow z luka (a) | 85 480 z 133 869 = 64% | 85 780 z 140 689 = 61% [P] | PRAWIE - mianownik analityka pomija 6.8 tys. zl rent | CSV kolumna renta_180, doba 364; suma zgodna z LOG L129098; w06, w14 |
| 16 | zloto do progu 85 tys. (krzywa "kazdy rod co najmniej 25% limitu") | 36 918 zl/dobe, +6 773 ludzi | 37 002 zl/dobe, +6 748 ludzi [S] | TAK jako rachunek; NIE jako pelny koszt - patrz nr 25 | CSV doby 309-364; w06 |
| 17 | zabici w druzynach w roku; sredni stan | 146 594; 88 tys. | 146 593; 88 303 [P] | TAK | linie "Ludzie:" (363 doby z licznikiem); w07 |
| 18 | jazda t3+ / cel; czeka; odrzucone; wolne konie; nieudane zakupy | 17.6% / 27.5%; 5 453 w 294 druzynach; 4 133; 2 328; brak koni 21, zlota 2 | te same [P] | TAK | LOG L129201, L129202; w09 |
| 19 | zbroja, helm, rekawice; brak sztuk; sprzet: przydzial, wydane, przeterminowane | 70%, 66%, 61%; 131 386; 239 797 / 123 386 / 92 162 | te same w dobie 364 [P]; srednio 28 dob: 244 127 / 85 214 / 112 970 | TAK; doba 364 to dzien wyjatkowo duzych zakupow | LOG L129173, L129141; w09 |
| 20 | zalegly zold | 2.49% partii; 0.12% zlota | 2.49% (631 z 25 384, czyli 22-23 partie i zalogi dziennie); w zlocie 0.22% moja prosta miara, 0.12% parser progow [P] | TAK | linie "Budzet rodow (na sucho)" i "Zold:"; w12 |
| 21 | kompanie najemne: ludzie a umowa | 2 190 przy 2 970 = 74% | 2 425 przy 2 970 = 82% (srednia 28 dob); doba 364: 2 462 przy 3 008 [P] | NIE | LOG L129095 i 27 linii przed nia; w11 |
| 22 | kiesy kompanii najemnych | 3.8 -> 5.6 mln | 3.81 -> 5.65 mln, te same 11 rodow [P]; tempo: 6.7 tys./dobe od doby 91, 4.3 tys./dobe w ostatnich 28 dobach | TAK; tempo spada | CSV kolumna G; w11 |
| 23 | bitwy doby 364: Dorzecze stracilo 2 148 i 1 259 ludzi "w wypadach" | jak obok | liczby te same [P]; ale Dorzecze bylo strona oblegajaca | liczby TAK, opis NIE - patrz pkt 2, H | BITWY L25373, L25400; w10 |
| 24 | krolestwa w wojnie 342-364 doby | 26 z 29 | 25; Dorzecze 277 dob; 3 w pokoju [P] | PRAWIE | linie "Skarbce"; w02 |
| 25 | (moja, brak u analityka) zold placony dzis ponad pulap | - | 93 rody, 31 126 zl/dobe, w tym 7 318 to same zalogi ponad caly pulap [P]; ok. 3.4 tys. ludzi druzyn oplacanych ponad pulap [S] | - | CSV doby 337-364; w15 |
| 26 | (moja) najtansza droga do 85 tys. z zasypaniem tej dziury | 30-45 tys. | ok. 50.6 tys. zl/dobe [S] (x1.37) | NIE | w15 |

Drobiazgi, ktore tez sprawdzilem i sa zgodne [P]: weterani w karczmach 8 456 (L129162), byli zolnierze w lesie 10 571 (L129134), dezercja 17.4 dziennie, zwolnieni z druzyn 332 wobec 520 dziennie, werbunek roczny 293 288, bitew lordow 1 869 i 14 twierdz zdobytych (L129204), 73 nowe wojny w dobie pokoju (L129230), zold druzyn 456 860 zl dziennie (linia "Zold:").

Dodatkowa kontrola z samego logu, niezalezna od CSV [P]:
- "rody ponad pulapem partii": 68 / 79 / 91 / 94 w dobach 91/182/273/364 (L32198, L64439, L96820, L129141).
- "rody w wojnie, ktorym zalogi i karawany zajely caly pulap": 47, z tego 42 bez ludzi w druzynach (L129141). To szersza miara niz "16 rodow" analityka i mocniejsza.

## 2. Niespojnosci analityka - proba obalenia

| nr | tytul | werdykt | dlaczego |
|---|---|---|---|
| A | Wojsko ponizej 85 tys. i dalej topnieje | **prawdziwa**, z dwiema poprawkami | patrz A nizej |
| B | Zalogi zjadaja budzet wojska polowego | **prawdziwa** | patrz B |
| C | Pieniadz wojny staje u ludu miast, korony i panowie ubozeja | **czesciowo** | patrz C |
| D | Pieniadz korony idzie do wszystkich panow wedlug lenn | **prawdziwa** | patrz D |
| E | Wojna bez konca i prawie bez rozstrzygniec | **czesciowo** | patrz E |
| F | Jazdy o jedna trzecia za malo, choc konie i zloto sa | **czesciowo** | patrz F |
| G | Druzyny niedozbrojone, pieniadz na sprzet przepada | **prawdziwa** | patrz G |
| H | Przegrany wypad z twierdzy to strata calej armii | **czesciowo** (zjawisko prawdziwe i wieksze, opis i naprawa odwrotne) | patrz H |
| K | Trzy krolestwa prawie bez wojska polowego | **prawdziwa**; naprawa "tam, gdzie A i B" tylko w polowie | patrz K |
| I | Zalegly zold w partiach ponad prog | **prawdziwa** (mala) | patrz I |
| J | Kompanie najemne ponizej umowy i odkladaja zloto | **czesciowo** (74% to artefakt miary) | patrz J |

### A. Wojsko ponizej 85 tys. i topnieje - PRAWDZIWA

Sprawdzilem cztery mozliwe artefakty:
- Miara. Linia "Skarbce" to ta sama miara, ktorej uzywa parser progow. Nie ma tu pomylki definicji.
- Baza porownania. Oba biegi roczne maja ten sam start i te same 364 doby. Ale roznica +24% to WGRANIE 35-36 + runda 3 + sluby NPC razem, nie sama runda 3. I to po jednym biegu z kazdej strony.
- Rozrzut miedzy biegami [P]: w oknie dob 93-120 test 120 dob rundy 3 mial 101 144 (progi-fin4\etap2.txt), a bieg roczny w tym samym oknie 95 428 (w14). To 5.7 tys. roznicy miedzy dwoma biegami podobnego kodu. Brak do progu 85 tys. wynosi 5.6 tys. Kierunek jest pewny, dokladna odleglosc od progu juz nie.
- Pierwszy rok. Szczyt 108 tys. to rozruch na kiesach startowych: "skarbiec wojenny" rodow (0.8 nadwyzki kiesy ponad rezerwe przez 45 dni) dawal 38% pulapu w dobie 91 i 26-29% od doby 182 (CSV, w06) [S]. Spadek ze szczytu do doby 182 to wiec zjawisko pierwszego roku. Spadek w drugim polroczu juz nie.

Dwie poprawki:
1. "Topnieje 36 ludzi dziennie" to srednia ze schodkow, nie rowny ubytek [P, w08]. Doby 197-276: +1 na dobe (stoi na ok. 83 tys.). Doba 277: -3 999 w jedna dobe. Doby 309-363: +11 na dobe (stoi na ok. 79.4 tys.). Doba 364: -3 858. Wojsko nie topnieje, tylko nie odrabia strat po wielkiej bitwie, bo rody sa na pulapie. Liczba rodow na pulapie tez rosnie skokiem, nie rowno: 85-93 w dobach 113-280, potem 108-111 (w04).
2. Koszt "30-45 tys. zl dziennie" to dolna granica, patrz pkt 3.1.

Czego analityk nie dopowiedzial, a wzmacnia jego wniosek [P]: doplata ze skarbcow (dzis 35 tys. zl dziennie) bedzie gasla dalej. Przy regule 1/360 nadwyzki za rok zostanie z niej ok. 13 tys. zl dziennie [S]. Czyli bez nowego pieniadza poziom 79 tys. tez sie nie utrzyma. Gdzie sie zatrzyma - nie wiem.

### B. Zalogi zjadaja budzet - PRAWDZIWA

- Kod potwierdza: w wojnie zalogi sa platne pierwsze, druzyny dostaja reszte (ClanBudget.cs 455, Settings.cs 1056: GarrisonWarFull = true). To decyzja projektu, nie blad.
- Liczby sie zgadzaja, a wlasny licznik logu jest jeszcze mocniejszy: 47 rodow z zerowym pulapem na druzyny (L129141).
- To nie jest zjawisko pierwszego roku ani biegu bez gracza: zold zalog stoi na 183-191 tys. zl dziennie od doby 57 do konca (CSV, srednie 28-dobowe, w08). Zalogi nie rosna - kurczy sie pulap.
- Trzy uscislenia:
  - "Wiecej zolnierzy w zalogach niz w polu" jest prawda, ale 9.1 tys. roznicy to doba dolka po bitwie. Srednio 28 dob: 83.6 tys. a 80.0 tys., roznica 3.6 tys. [P].
  - "Zjadaja budzet" w zlocie znaczy 29.5% calego zoldu (190 862 z 647 722 zl dziennie, linia "Zold:", srednia 28 dob) [P]. Czlowiek zalogi kosztuje 2.28 zl dziennie, czlowiek druzyny 5.75 zl. U rodow na pulapie zalogi to 38% pulapu - tu sie zgadzam.
  - Liczba takich rodow nie rosnie rowno: 24 / 48 / 30 / 43 / 47 w dobach 91/182/273/336/364 (L32198, L64439, L96820, L119166, L129141).
- Brak: naprawa "korona doplaca do zoldu zalog" nie ma ceny. Zold zalog rodow z luka (a) to 143 728 zl dziennie, wszystkich rodow 191 tys. (CSV doba 364, w06). Pelna doplata bylaby 3-4 razy wieksza niz cala potrzeba na 85 tys. Trzeba powiedziec, jaka czesc i komu.

### C. Pieniadz staje u ludu, korony i panowie ubozeja - CZESCIOWO

Prawdziwe [P]: kiesy ludu miast to jedyny duzy zbiornik, ktory rosnie (44.1 mln, 21% pieniadza swiata). Podatek wojenny ludu stoi na pulapie 3% i daje 15 tys.

Co jest artefaktem albo za mocne:
- **Skarbce maleja z zalozenia.** Regula oddaje codziennie 1/360 nadwyzki ponad 500 tys. (L129101). To zaplanowane wydawanie zapasu startowego, czyli zjawisko pierwszego roku, nie przeciek.
- **Panowie nie ubozeja od polowy roku.** Glowy rodow: 47.8 mln (doba 182), 47.5 mln (doba 273), 47.2 mln (doba 364); w ostatnich 28 dobach +16 tys. zl dziennie (L64500, L96881, L129205) [P]. Kiesy rodzin z lennem w wojnie: 40.7 -> 39.2 -> 39.2 mln (CSV, w06).
- **Renty "310 -> 141 tys." to porownanie dwoch dni.** Doba 30 to rozruch, doba 364 to slaby dzien. W srednich 28-dobowych renty wynosza ok. 195-223 tys. w dobach 85-252 i ok. 167-181 tys. w dobach 253-364 (CSV kolumna renta_180, w08) [P]. Spadek o ok. 17%, nie o 55%.
- **Strumien do ludu sam gasnie.** Przyrost kies ludu: 132 tys. zl dziennie (doby 30-91), 116 (91-182), 84 (182-273), 75 (273-364), 70 (336-364) (linia "Kiesa ludu", w07) [P]. Idzie w parze z ubytkiem skarbcow: -71 tys. (doby 182-273), -51 tys. (273-364), -49 tys. (336-364). W poprzednim biegu lud odkladal pod koniec tylko 38 tys. zl dziennie (LOG34 L119006, L129003).

Wniosek dla raportu: zdanie "lud odklada 56-70 tys. zl dziennie - tyle mozna brac trwale" jest szersze niz dane. Pewne jest tylko to, ze u ludu lezy 29.5 mln ponad jego zapas (L129238). Ile bedzie odkladal za rok - nie wiem.

### D. Pieniadz korony idzie do wszystkich wedlug lenn - PRAWDZIWA

- 86 rodow z wolnym miejscem: zold 48% pulapu, kiesy 25.9 mln, wypelnienie 63.9% - zgodne (w04).
- Udzial rent: w dobie 364 do rodow z luka (a) idzie 61% (85 780 z 140 689), nie 64% [P]. Liczone dla rodow "na pulapie" (111 rodow) i jako srednia 28 dob: 48% rent (83 025 z 172 002) i 47.5% zwrotu zoldu (w14) [P]. Czyli wniosek analityka jest raczej za ostrozny.
- Wzor "1 zl daje 0.45-0.64 zl zoldu" odtworzylem z kodu (ClanBudget.cs 191-210, zwrot 70%): wychodzi to samo. Przy udziale 48% zamiast 64% wychodzi 0.37-0.54 [S]. Miesci sie w jego szerokich widelkach.
- Niezalezne potwierdzenie z porownania biegow [P]: wplywy dnia koron byly prawie takie same (451 070 w poprzednim biegu, 449 808 w tym, srednia 28 dob), a wojska jest o 15.4 tys. wiecej. Wiec o wojsku decyduje to, do kogo pieniadz trafia, nie tylko ile go jest.
- To decyzja projektu (renty wedlug wag lenn), nie blad kodu.

### E. Wojna bez konca - CZESCIOWO

- Liczby zgodne z drobna poprawka: 25 krolestw w wojnie 342-364 doby, Dorzecze 277 dob (w02). Zdarzen "WOJNA" 103, "POKOJ" 88, nowa wojna w dobie pokoju 73 razy (w11).
- 86 ze 103 wojen wypowiedziala fabula ROT (ROTStorylineWars.EnforceWars). To cecha Realm of Thrones, nie naszych modow.
- Mozliwy artefakt biegu bez gracza: fabula ROT w roku odpalila 5 zdarzen, a 17 jest zaleglych na osi (session-2026-10-13_06-24-09.log, linia "FABULA: dzien 364 | zaleglych na osi: 17"). Czy z graczem fabula szlaby dalej i konczyla wojny - **nie wiem**.
- "Prawie bez rozstrzygniec" ma konkretna przyczyne, ktorej analityk nie nazwal - patrz H.

### F. Jazdy za malo, choc konie i zloto sa - CZESCIOWO

- Niedobor jest prawdziwy i trwaly [P]: jazda t3+ stoi na 16.9-17.8% od doby 85 do konca, w obu biegach (srednie 28-dobowe, w09). To nie dochodzenie do celu w pierwszym roku.
- "Zloto jest" - tak: nieudane zakupy z braku zlota 2.4 dziennie.
- "Konie sa" - tylko w czesci: wolnych koni w zbrojowniach jest 2 328, a czeka 5 453 ludzi. Nawet rozwiezienie wszystkich zostawia 3.1 tys. bez konia [P]. Na targach koni brakuje: 21.9 nieudanych zakupow dziennie, z tego 17.0 przy pustej polce (L129202, srednia 28 dob).
- Wolne konie w zbrojowniach rosna caly rok (1 232 -> 2 328), czyli leza nieuzyte - ta czesc naprawy ma podstawe.

### G. Druzyny niedozbrojone - PRAWDZIWA

- Zbroja wlasciwego szczebla stoi na 67-72% od doby 29 do konca, w obu biegach (w09) [P]. To stan trwaly. Helm, tarcza i rekawice poprawiaja sie caly rok.
- "Brakuje towaru, nie zlota" potwierdza osobna linia [P]: na polkach calego swiata lezy 129 zbroi t3-4 i 98 zbroi t5-6 przy braku 30 255 zbroi (linia "Zbroja na polkach (174b)", doba 364).
- Dwa uscislenia: 96% z doby 4 to darmowy komplet startowy, nie uczciwa baza. Pieniadz "nie przepada" - po 30 dniach idzie do kasy siedziby rodu (wplacone srednio 87 475 zl dziennie, L129141 i srednia 28 dob).

### H. Przegrany wypad to strata calej armii - CZESCIOWO

Fakt prawdziwy: regula ludzkich strat (H3) obejmuje tylko bitwy w polu (LosersFlee.cs, warunek IsFieldBattle; L128988 "pominiete: nie w polu 7"). Kto przegra poza polem, traci wszystkich.

Ale opis jest odwrotny [P]:
- W obu bitwach doby 364 strona "atakujacy" to King's Landing, i to ona wygrala (BITWY L25373, L25400). W wypadzie atakuje ten, kto wychodzi z twierdzy. Obie twierdze nalezaly tego dnia do King's Landing: Deep Den do rodu Lydden, Willow Wood do rodu Lannister (ludzie-regiony.csv, dzien 109199, kolumny krolestwo i rod; Willow Wood bylo Dorzecza jeszcze w dobie 265). Dorzecze oblegalo i zostalo rozbite wypadem.
- W calym roku: 40 wypadow, 38 wygral wychodzacy, 0 oblegajacy, 2 bez rozstrzygniecia (w10). Przegranego wypadu nie bylo ani razu.
- Oblegajacy stracili w tych 38 bitwach 55 018 ludzi: 19 328 zabitych i 35 687 rannych, ktorzy poszli do niewoli. Za kazdym razem 100%, zero "rozbitych". To 13% wszystkich zabitych w druzynach w roku.
- Podobnie bitwy pod murami (SiegeOutside): 21 bitew, przegrani 18 480 ludzi, w 13 z 13 duzych strata calkowita.

Skutek: naprawa "przegrany wypad wraca za mury" nie dotyczy zadnego przypadku z tego roku. Regula powinna objac pobitego OBLEGAJACEGO (i przegranego pod murami). To tez wyjasnia E: oblezenia prawie nigdy sie nie udaja, bo armia oblegajaca ginie w calosci (14 twierdz zdobytych na 40 wypadow).
Liczby "zwyciezcy 12.6% zabitych, przegrani 41.6%" to srednia ze wszystkich rodzajow starc (takze rabunki i oblezenia; BattleChronicle.cs 72-93), nie z bitew w polu. Stoi obok punktu 9 analityka (22.9% i 4.6% w polu) i moze mylic. Nie sprawdzalem jej do konca.

### K. Trzy krolestwa prawie bez wojska - PRAWDZIWA

- Liczby zgodne (w13): Wolni Ludzie 566 ludzi, Ibben 413, Zelazne Wyspy 1 578.
- Sprawdzilem, czy to nie artefakt miary. Linia "Straz i Wolni Ludzie" podaje 9 051 ludzi w "partiach" Wolnych Ludzi (L129271), ale to wszystkie partie tej frakcji - milicje, tabory wsi, karawany (PeopleLedger.cs 98-110 i 438-453). Druzyn lordow jest 696. Wiec niespojnosc stoi.
- Uwaga do tabeli analityka w pkt 1: wiersze "Nocna Straz (druzyny + zalogi)" i "Wolni Ludzie (druzyny + zalogi)" maja zla nazwe. To wszystkie partie frakcji plus zalogi.
- "Naprawa tam, gdzie A i B" - tylko w polowie. Te krolestwa sa po prostu biedne: Wolni Ludzie maja pulap 8 670 zl dziennie, z czego zalogi biora 5 918. A podatek od ludu im nie pomoze: przyrost kies ludu miast w ostatnich 28 dobach to -1 887 zl dziennie u Wolnych Ludzi, +179 w Ibbenie, +3 874 na Zelaznych Wyspach, przy potrzebie 4 750 / 3 875 / 6 080 zl dziennie (w13).

### I. Zalegly zold w partiach - PRAWDZIWA (mala)

2.49% (631 z 25 384 rozliczen w 28 dobach). To 22-23 partie i zalogi dziennie, nie "kilkanascie". W poprzednim biegu 3.17%. "Zniknie razem z A" to przypuszczenie bez pomiaru.

### J. Kompanie najemne - CZESCIOWO

- 74% to artefakt: 2 190 to szeregowi z CSV, 2 970 to umowa z linii "Kontrakty". Ta sama linia podaje ludzi 2 425, czyli 82% umowy.
- Umowa placi za ludzi pod bronia (L129095: "kontrakt za ludzi pod bronia"), wiec stan ponizej umowy jest zgodny z regula.
- Kiesy rosna naprawde (3.81 -> 5.65 mln, te same 11 rodow), ale coraz wolniej: 4.3 tys. zl dziennie w ostatnich 28 dobach.

## 3. Czego brakuje

### 3.1 Koszt progu nie liczy dziury (wazne dla Jeffa)

93 rody w wojnie placa dzis zold wiekszy niz ich pulap: razem 31 126 zl dziennie ponad (CSV, srednia 28 dob, w15) [P]. Regula to toleruje do 110% pulapu (zwolnienia dopiero powyzej, ClanBudget.cs 711-724), ale taki rod nie werbuje: limit zoldu druzyny jest wtedy ciety ponizej jej dzisiejszego zoldu (ClanBudget.cs 565-580). Zeby zaczal werbowac, trzeba najpierw zasypac dziure.

| kazdy rod co najmniej | przyrost ludzi | zloto wedlug analityka | zloto z zasypaniem dziury [S] |
|---|---|---|---|
| 20% limitu | 4 689 | 25 903 | 38 439 |
| 25% limitu (prog 85 tys.) | 6 748 | 37 002 | 50 558 |
| 30% limitu | 9 186 | 49 615 | 64 272 |
| 40% limitu (ok. 94 tys.) | 15 425 | 81 521 | 99 970 |

Do tego to jest zold, nie podatek. Od nowego zoldu druzyn korona zwraca 70% z tych samych wplywow dnia, wiec albo ten zwrot tez trzeba sfinansowac, albo zmaleja renty wszystkim. Ile dokladnie - **nie wiem**, zalezy od formy doplaty.

### 3.2 Pieniadz i potrzeba leza w roznych krolestwach

Podatek zbiera korona danego krolestwa i oddaje wlasnym panom. Analityk liczy swiat jako jeden worek.
Z 37 tys. zl dziennie najtanszej drogi do 85 tys. az 14.7 tys. (40%) przypada na Zelazne Wyspy, Wolnych Ludzi i Ibben. Razem 18.3 tys. zl dziennie (50%) to potrzeba w krolestwach, gdzie lud miast nie odklada tyle albo wcale (w13) [S na danych P].
Lud odklada najwiecej tam, gdzie wojska juz jest duzo: Polnoc +14.4 tys. zl dziennie, Dolina +10.8 tys., Volantis +7.8 tys., King's Landing +7.0 tys., Dorzecze +5.9 tys. (kiesa-ludu.csv, dni 109171-109199).
Wniosek: podatek od ludu da wojsko glownie w bogatych krolestwach. Do 85 tys. mozna tak dojsc, ale trzy najslabsze krolestwa zostana bez wojska.

### 3.3 Odpowiedz na pytanie Jeffa jest niepelna

Jeff pyta, gdzie podniesc podatki na czas wojny. Analityk daje zrodla i kwoty. Brakuje:
- Ile kazda propozycja da zolnierzy po uwzglednieniu 3.1 i 3.2.
- Ceny doplaty do zoldu zalog (patrz B).
- Co z ludem po podatku. "Stac jedzenie" jest dzis na 98.5% i 9 miast ma ponizej 95% (L129238). Analityk odsyla to do rozdzialu o gospodarce - slusznie, ale w raporcie dla Jeffa te dwie rzeczy musza stac obok siebie.
- Jasnego zdania, ze "na czas wojny" znaczy tu "na stale" (jest w pkt 7.4, ale nie w streszczeniu).
- Prognozy na drugi rok. Doplata ze skarbcow gasnie, strumien do ludu gasnie, rodow na pulapie przybywa. Bez nowego pieniadza takze cel 70-80 tys. nie jest pewny. Gdzie wojsko sie zatrzyma - **nie wiem** i analityk tez nie wie.

### 3.4 Wnioski szersze niz dane

- "Sufit swiata ok. 100 tys." zaklada, ze rod na pulapie po dostaniu pieniedzy zachowa sie jak rod z wolnym miejscem. Przy innych definicjach "na pulapie" wychodzi 96-99 tys.; z wypelnieniem z doby 91 (68%) ok. 105 tys. (w04) [S]. Sam bieg trzymal 104.0 tys. srednio w dobach 57-84, gdy 41 rodow bylo juz na pulapie. Uczciwie: 96-105 tys.
- "+24% dala runda 3" - to roznica dwoch pojedynczych biegow i trzech zmian naraz.
- "Zalegly zold zniknie razem z A" - bez pomiaru.

### 3.5 Liczby bez zrodla albo nie do odtworzenia

- Mianownik rent 133 869 (log i CSV daja 140 689).
- Mediana 79 857 (jest 79 596).
- "100 wojen, 86 pokojow" - linie zdarzen daja 103 i 88; 86 to liczba wojen z woli fabuly ROT.
- "2 190 przy 2 970" - dwie rozne miary.
- "12.6% / 41.6%" - nie jest powiedziane, ze to wszystkie rodzaje starc.

## 4. Czego sam nie wiem

- Czy fabula ROT z graczem konczylaby wojny.
- Gdzie zatrzyma sie wojsko bez nowego pieniadza.
- Ile nowego podatku trzeba na 1 zl nowego zoldu przy doplacie celowanej - zalezy od formy, a zadna nie byla sprawdzona biegiem.
- Rozrzutu miedzy biegami: mam tylko jedna pare okien (5.7 tys. ludzi w dobach 93-120).
