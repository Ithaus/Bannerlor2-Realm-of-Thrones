
## 8. Bunty i hrabstwa (nowe krolestwa)

### 8.1 Lista

Pierwsza i ostatnia doba nazwy krolestwa [P] T `budzet-rodow.csv`; przejscia twierdz bez oblezenia [P] `zmiany-krolestwa-osad.tsv` (z `ludzie-regiony.csv`); wojna "CausedByKingdomCreation" [P] A, "Wojna i pokoj (E7/0)"; upadki [P] A, "Skarbiec upadlego krolestwa (G14)".

| d | nowe krolestwo | skad | jak (rody i twierdze) | koniec |
|---|---|---|---|---|
| 205 | Antaryon League (Braavos Rebels) | Braavos | wojna domowa Diplomacy (S linia 5947); Antaryon i Celeris z Braavos, Lorath, Rhyos bez oblezenia; potem szturmy Totava d247, Iksa Keria d310 | trwa; Braavos zniszczone d310 (A linia 120720) |
| 428 | Confederation of Phassar (Lorath Rebels) | Lorath | wojna domowa Diplomacy (S 10450); Phassar z Tolero Aeksio | trwa |
| 441 | Kingdom of Volantis | Volantis | Maegyr, Paenymion, Vhassar z 6 twierdzami (Volantis, Selhorys, Valysar, Ar Noy, Demongate, Gargos) | d534 "upadek przy stole pokoju", twierdze wracaja (A 208687) |
| 451 | County of Rhyos | Antaryon League | Celeris z Rhyos | trwa |
| 452 | Barony of Bisa | Lorath | Eranos z Bisa | d549 pokoj, wraca (A 214619) |
| 454 | Kingdom of Norvos | Norvos | Quarro, Hortyr z Norvos i Menetragos | trwa |
| 493 | County of Pentos | Pentos | Mopatis z Pentos | d652 pokoj, wraca (A 255518) |
| 518 | County of Myrth | Myr | Saldorys, Alayris z Myrth, Bolozo, Chroyane, Gelona | trwa |
| 540 | Barony of Ulentor | Norvos | Kasgos, Tavellen z Ulentor, Sambandos | d635 pokoj (A 248745) |
| 543 | County of Norvos | Kingdom of Norvos | Quarro z Norvos | d678 wraca (A 265901) |
| 596 | Barony of Ghoyan Drohe | Pentos | Moesia | d687 pokoj, wraca (A 269475) |
| 609 | Drahar Conspiracy (Myr Rebels) | Myr | wojna domowa Diplomacy (S 14065); Drahar z miastem Myr; szturm Odivo d628 | trwa |
| 621 | Barony of Sambandos | Barony of Ulentor | Tavellen z Sambandos | trwa |
| 628 | Barony of Ny Sar | Norvos | Golathis z Ny Sar | trwa |
| 641 | County of Draconys | Qohor | Adory z Draconys | trwa |
| 673 | Qar Deeth | Qarth | Qar Deeth z Qarkash | trwa |
| 691 | Barony of Tolerrol | Myr | Terys z Tolerrol | trwa |
| 698 | Rakh Jhattar of Vaes Diaf | Dothrakowie | Jhago z Vaes Diaf | trwa |
| 702 | County of Bolozo | County of Myrth | Alayris z Bolozo, Chroyane, Gelona | trwa |

- Razem 19 nowych krolestw: 1 w roku 1, 18 w roku 2; 13 trwa na koniec biegu, 6 wrocilo pokojem. Wszystkie w Essos (Wolne Miasta, Qarth, Dothrakowie) - w Westeros zadne [P].
- Norvos zniknelo d635 bez szturmu i bez pokoju - jego rody odeszly z lennami (d454, d540, d628) [P] A linia 248748.
- Dodatkowo 2 bunty Diplomacy skonczone abdykacja wladcy ("Albersan League of Myr", "Lohar Conspiracy of Lys") [P] S linie 9163-9164.
- Przy kazdym odejsciu rod zostaje wlascicielem tej samej twierdzy (kolumna rod bez zmian), zmienia sie tylko krolestwo - to nie bunt chlopski ani zdobycie [P] `zmiany-krolestwa-osad.tsv`.

### 8.2 Mechanizm w kodzie i w ustawieniach

- BK: rod z lennami bez krolestwa codziennie zamieniany w nowe krolestwo o nazwie z tytulu ("County of X", "Barony of X") [K] `bkfull\BannerKings.Behaviours\BKClanBehavior.cs:366-368` (dzienny przebieg rodu) -> `:454-465` (`MakeRebelKingdom`: `clan.Kingdom == null`, `clan.Settlements.Count > 0` -> `KingdomManager.CreateKingdom`); takze po rozwiazaniu buntu w osadzie `:70-81`.
- Diplomacy [U] `Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json`: DailyChanceToStartRebelFaction 0.05, DailyChanceToJoinRebelFaction 0.1, DailyChanceToStartCivilWar 0.1, MinimumTimeSinceLastCivilWarInDays 1000, MaximumFactionDurationInDays 520. To szanse **na dobe** - w roku 364-dniowym losowan jest 4.33 razy wiecej na rok niz przy tych samych liczbach w roku 84-dniowym [S]; zmeczenie wojna Diplomacy juz poprawiono /4.33 (STAN-PRAC 14.10 13:01), tych szans nie [U].
- Kiesy odchodzacych rodow dzien przed odejsciem: 15 z 21 rodow ma kiese ponad rezerwe R (np. Terys 1.45 mln, Qar Deeth 1.48 mln, Jhago 1.32 mln); 6 ponizej R, z tego 4 z dlugiem w Banku (Vhassar, Quarro, Kasgos, Golathis) [P] T `budzet-rodow.csv`. Bieda nie jest glowna przyczyna odejsc.
- Dlaczego rody odchodza (vanilla "opuszczenie krolestwa", Diplomacy secesja czy cos innego) i czemu dopiero w roku 2 - log tego nie zapisuje (rozdz. 9).

## 9. Czego nie wiem

1. **Przyczyna odejsc rodow z lennami w roku 2** (rozdz. 8) - zadna linia logu nie podaje powodu; stanu frakcji buntu Diplomacy nie logujemy.
2. **Dlaczego lenna Polnocy zawsze dostaje Bolton** - wynik BK `CalculateHeroFiefScore` i glosy nie sa w logu.
3. **Co zabralo zaloge Krolewskiej Przystani d524-d529** (541 -> 271 bez glodu i bez bitwy pod murami tych dob): ostrzal (`DefaultSiegeEventModel.cs:255-263`), patrole BK, lordowie - nie zmierzone. To samo dla innych twierdz, gdzie zaloga spadala przed bitwa (Skyreach 340 -> 289 -> 184 -> 0 w d268-d272).
4. **Czy w bitwie SiegeOutside zaloga ma premie murow** - nie sprawdzilem modelu symulacji bitwy dla tego typu.
5. **Bieg bez naszych modow** - nie ma; porownania "wobec gry" sa tylko z kodu gry i ze stanu startowego ROT (d1).
6. **Co Jeff zobaczyl przy Harroway** - dane mowia: Dorzecze caly bieg.
7. **18 szturmow "bez rozstrzygniecia"** i **26 oblezen zakonczonych okupem** - to sprawa rozpoznania czasu oblezen, tu tylko policzone.
8. **Czy asymetria przegranych (oblegajacy wraca, zaloga ginie) rozstrzyga wojny** - liczby sa, wplyw na wynik wojny nie zmierzony.
9. **Dlaczego Dorne nie zbiera armii** (druzyny bija bandy osobno) - tworzenie armii AI nie jest logowane.
10. **Liczba rodzin i druzyn z ROT** (ile rodow, jaki limit druzyn na rod) jako zrodlo przewagi Polnocy - limity zmierzone (rozdz. 2.2), skad ROT bierze ich wielkosc (BK / ROT model wielkosci druzyny) nie sprawdzilem.

## 10. Pliki posrednie (`docs\oblezenia\robocze\sily\`)

| Plik | Co |
|---|---|
| `zdobycia-fin30.tsv` | 57 zdobyc z linii L5b-2 (numer linii A, doba - 1, osada, zdobywca) |
| `okup-O0-fin30.tsv` | 113 decyzji o szturmie (linie O0): druzyny, ludzie, doby oblezenia, sily, wynik |
| `zdobycia-szczegoly.tsv` | 57 zdobyc: zaloga i milicja\* d1 / start / przed, szturm, O0 |
| `zdobycia-krolestwa-wojny.tsv` | przy kazdym zdobyciu: ludzie w druzynach obu krolestw, liczba wrogow |
| `bitwy-pod-murami.tsv` | 142 bitwy Siege / SiegeOutside / SallyOut z `bitwy.log` |
| `zmiany-krolestwa-osad.tsv` | 120 zmian krolestwa twierdz, z oznaczeniem "oblezenie / BEZ oblezenia" |
| `sily-krolestw.tsv`, `regiony-krolestwa.tsv`, `balans-wybrane-dni.tsv` | rozdz. 2.1 |
| `budzet-krolestwa.tsv`, `doplaty-sumy.tsv`, `doplaty-wobec-zoldu.tsv` | rozdz. 2.2, 5.1 |
| `zalogi-dzienne.txt` | zalogi i milicja swiata co 30 dob |
| `gdzie-armie.txt` | bitwy druzyn KP i Dorne wedlug regionow w oknach oblezen |
| `tagi-fin30.txt` | spis rodzajow linii logu A |
| `dekompilacja\RenlyDeathEvent.cs` | ROT.dll, zdarzenie smierci Renly'ego |
| `tabela-zdobycia.md`, `tabela-krolestwa.md`, `czesc*.md` | kawalki tego dokumentu |

Skrypty liczace (python -I): scratchpad sesji `...\c36b73ca-...\scratchpad\py\` (zdobycia.py, okup.py, krol.py, balans.py, budzet.py, zdob_tabela2.py, wojny.py, gdzie.py, zmiany.py).
