
## 3. Zdobyte twierdze: zaloga, milicja, armia oblegajaca

### 3.1 Liczby zbiorcze (57 zdobyc oblezeniem)

Zrodla: lista zdobyc [P] A, 57 linii "Karawany w zdobytym miescie (L5b-2)" (numery linii w `zdobycia-fin30.tsv`); zaloga i milicja\* [P] T `ludzie-regiony.csv`; poczatek oblezenia i liczba druzyn [P] A, linie "Okup oblezenia (O0)" (113 decyzji gry o szturmie, `okup-O0-fin30.tsv`); bitwy [P] T `bitwy.log` (linie bez "H3", `bitwy-pod-murami.tsv`). Wyliczenie: `zdobycia-szczegoly.tsv`.

| Co | Wartosc |
|---|---|
| zdobycia rok 1 / rok 2 | 31 / 26 (zamki 44, miasta 13) |
| zdobywcy | Reach 11 (8 z Dorne, 2 ze Smoczej Skaly, 1 ze Stormlands), Dorzecze 10, Baratheon KP 6, Polnoc 5, Stormlands 5, Targaryen 4, Volantis 3, Myr 2, Sarnor 2, Antaryon 2, po 1: Tyrosh, Lorath, Ibben, Nocna Straz, Drahar, Smocza Skala, Braavos |
| stracili | Baratheon KP 13, Dorne 8, Smocza Skala 7, Dorzecze 6, Braavos 3, po 2: Qohor, Pentos, Ibben, Targaryen Aegon, Dothrakowie; reszta po 1 |
| pogranicze KP - Dorzecze | 16 zdobyc, z tego 12 to 6 zamkow przechodzacych z rak do rak (Willow Wood 3x, Stone Hedge, Acorn Hall, Wayfarer's Rest, Pinkmaiden, Deep Den po 2x) |
| doba oblezenia przy decyzji o szturmie | mediana 8.4 doby (4.2-12.7) we wszystkich 113 decyzjach; 8.0 w 53 zdobyciach |
| armia oblegajaca przy decyzji (113) | mediana 13 druzyn (3-32), 2352 ludzi, 208 ludzi na druzyne |
| szturm (54 bitwy "Siege" wygrane przez atak) | atak mediana 2995 ludzi, 16 druzyn (1192-9708); obrona mediana 431 ludzi |
| stosunek atak / obrona w szturmie | mediana 7.4 : 1 (1.5-45.1); ponizej 3 : 1 tylko 7 razy, ponizej 5 : 1 16 razy, 10 : 1 i wiecej 18 razy |
| sila gry przy decyzji (oblegajacy / obroncy) | mediana 5.7 : 1 (113 decyzji); w zdobyciach 8.0 : 1 |
| atak wobec (zaloga + milicja\*) na poczatku oblezenia | mediana 6.1 : 1 (min 1.2) |
| zaloga na poczatku oblezenia | zamki mediana 254 (101-360), miasta 517 (281-625) |
| zaloga na starcie kampanii (d1, te same twierdze) | zamki 125, miasta 328 |
| milicja\* na poczatku oblezenia | zamki 178, miasta 228 |
| zaloga w ostatnim wierszu przed zdobyciem | zamki mediana 92, miasta 253; zaloga 0 w 8 zamkach |
| glod w twierdzy w czasie oblezenia | 0 przypadkow (kolumna glodna_warownia = 0 we wszystkich) [P] T `spichlerze-regiony.csv` |
| szturmy odparte | 0 (`balans-krolestw.csv`, szturmy_odparte = 0 u wszystkich); 18 bitew "Siege" skonczylo sie "bez rozstrzygniecia" |
| oblezenia zakonczone okupem (O) | 26 z 113 decyzji ("wynik okup AI z AI") |

### 3.2 Zaloga ginie w polu przed szturmem (bitwa pod murami)

- W 11 z 57 zdobyc w ciagu 12 dob przed zdobyciem oblegajacy wygrali bitwe typu **SiegeOutside** pod ta sama twierdza; obroncy tracili w niej 256-1843 zabitych, a nastepny szturm szedl na sama milicje (obrona 92-110 ludzi): Fellwood d39, Bloody Gate d115, Demongate d131, Blackmont d148, Qamayl d193, Deep Den d217 i d576-577, Skyreach d272, Vaes Dothrak d427-428, **Krolewska Przystan d530**, Silverhill d611 i d614 [P] `bitwy-pod-murami.tsv`.
- Przyklad Bloody Gate: zaloga 276 do d113, w wierszu d114 zaloga 0 (milicja\* 332 -> 214); bitwa d115 "SiegeOutside pod Bloody Gate: Tristan Ryger (Riverlands, partii 28) 4607 vs Bloody Gate (The Vale, partii 10) 1371 - wygrywa atakujacy", obrona: zabici 691, ranni 682; szturm d116 "4239 vs 94" [P] T `bitwy.log` linie 6704, 6778; T `ludzie-regiony.csv` castle_EN2.
- Skad bitwa w polu przy szturmie - kod gry: gdy do szturmu dolacza po stronie obrony oddzial, ktory nie stoi w twierdzy, gra zmienia typ bitwy ze szturmu na SiegeOutside [K] `dec32\cs_full\TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:651-654`; atak na oddzial pod oblegana twierdza to tez SiegeOutside [K] `...\Actions\StartBattleAction.cs:65-77`. Czy w SiegeOutside zaloga walczy z premia murow - nie sprawdzone (rozdz. 9).
- Gdy w polu przegrywa **oblegajacy**, nasze reguly N3/H3 robia z 70 % przegranych rozbitych (wracaja): 111 563 ludzi pobitych oblegajacych, zabici + jency 33 540, rozbici 78 023; gdy przegrywa **strona twierdzy** - "jak w grze" (17 bitew pod murami, 2 wypady) [P] A, suma linii "Przegrani pod murami (N3)". W bitwach SiegeOutside z tabeli obrona ma 0 rozbitych (np. Bloody Gate d115: 691 / 682 / 0) [P] `bitwy-pod-murami.tsv`.

### 3.3 Tabela 57 zdobyc

Kolumny: d - doba zdobycia (linia L5b-2); dni obl. - "oblezenie X dob" z ostatniej linii O0 (decyzja gry o szturmie); zaloga d1 - stan na starcie kampanii; start obl. - wiersz z doby przed poczatkiem oblezenia (doba O0 minus dni oblezenia); przed - ostatni wiersz, gdy twierdza nalezala jeszcze do obroncy; bitwa pod murami - SiegeOutside wygrana przez oblegajacych w 12 dobach przed; szturm - ostatnia bitwa "Siege" wygrana przez atak. "-" = brak linii. Harrenhal d128 to szturm fabularny ROT (rozdz. 7.4).

