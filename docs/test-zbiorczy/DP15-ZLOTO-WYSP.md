# DP15 - zloto Zelaznych Wysp (bieg dwuletni fin30, d500-728)

Pytanie (DP15 z `BIEG-DWULETNI-FIN30-2026-10-14.md`, rozdz. 0 pkt 10 i 4.1): skad w Q7-Q8 przyszly pieniadze korony Zelaznych Wysp (Iron Islands, id `sturgia`) i ich rodow - czy to zloto z niczego / podwojne liczenie (BLAD), czy ktos za nie zaplacil (CECHA).
Dane: log `A` = `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log`, tabele `T` = `...\tabele-06-01-15\`, `bitwy.log` w `T`. Kod biegu = `w-toku/zbior-e8` **6e6313c** (wyciagniety z klonu `zb\int` przez `git archive`, tylko do odczytu); numery linii [K] sa z 6e6313c, przy `CrownIncome.cs` w nawiasie numer w `zb\int` HEAD 7bc5dd0b.
Doba `d` = "dzien" z linii 165 - 108835 (dzien 109540 = d705; ta sama doba co w raporcie BIEG). Zdarzenie z godzin miedzy tickami liczone jest w dobie, w ktorej zloto pokazuje sie w linii 165 (nastepny tick). Kwoty w tys. zl na dobe, chyba ze napisano inaczej.
Znaczniki: [P] pomiar z logu / tabel (linia albo plik), [K] kod plik:linia, [S] szacunek / wniosek. Gra nie uruchamiana, kod bez zmian, bez commita.

## 0. Prostym jezykiem

1. **CECHA, nie blad.** Zloto Wysp ma platnika. Zlota z niczego ani podwojnego liczenia nie ma. **Poprawka po weryfikacji (rozdz. 11):** najwiecej koronie Wysp zaplacil jej **wlasny lud** - danina P1 4.07 z 12.01 mln (34%), a z kasami miast Wysp 6.33 mln (53%). **Polnoc** dala koronie wprost ok. 3.1 mln (26%), w tym najwieksza pojedyncza wplata - okup krola Robba 2.08 mln. Za to Polnoc jest glownym zrodlem **nowego** pieniadza Wysp (okupy 5.25 mln zaplacone + lup z bitew wobec przyrostu calych Wysp o 7.31 mln). Lud i miasta Wysp placa wiecej, bo krazy u nich ten lup oraz zold i dwor wiekszego wojska oplacanego z rent korony - to obieg, nie nowe zloto.
2. Polnoc przegrala z Wyspami trzy duze bitwy (Lonely Light d586, Blacktyde d603, Flint's Finger d663): lup z sakiewek rozbitych druzyn i 40 okupow lordow Polnocy (3.69 mln) poszly do rodow Wysp, a korona brala z tego swoja 1/9.
3. Krol Polnocy Robb Stark trafil do niewoli Wysp: skarbiec Polnocy zaplacil **2 084 825 zl** (162 870 od razu + 44 raty po ok. 44 tys. dziennie, d676-719); przez te 6 tygodni rody Polnocy dostawaly od swojej korony prawie zero rent (59.4 -> 0.8 tys./d).
4. Korona Wysp nic nie odklada (skarbiec stoi na progu 500 tys.), wiec wszystko, co wplynie, tego samego dnia idzie do rodow jako renty i zwrot zoldu - stad "korona placi 15 razy wiecej", kiesy rodow 0.82 -> 6.49 mln i wojsko 3.2 -> 7.7 tys. (d705).
5. Ksiega pieniadza swiata tego nie widzi jako nowego zlota: suma swiata 207.86 -> 207.63 mln, "zrodla z niczego" spadaja (797 -> 621 tys./d), reszta niewyjasniona w 182 dobach razem -23 tys. zl; Polnoc chudnie dokladnie wtedy, gdy Wyspy tyja (kiesy Polnocy 3.79 -> 1.54 mln).
6. Do raportu BIEG: spadek "braku na zold" o 19.9 tys./d u Wysp to nie poprawka systemu, tylko wynik wygranej wojny (pieniadz Polnocy - okupy i lup - i jego obieg przez lud i miasta Wysp) - nie liczyc go jako poprawy; co zostaje do decyzji projektowej - rozdz. 8.

## 1. Korona Wysp w oknach - skad wplywy dnia (165) [P]

Zrodla kolumn: "Korona: wplywy dnia (165)", fragment "na krolestwo ... Iron Islands W+Z (184 x) (danina P1 y) ... rata +r ... zwrot a/b renty c" (A, kazda doba); zawor kas miast do korony = suma `do_korony` miast Wysp (T `kasy-miast.csv`); 1/9 trzecich (szac.) = `trzecia`/3 + `zdarzenia`/9 rodow Wysp (T `budzet-rodow.csv`) [S]; okupy rodow Wysp = linie "Okupy (178): AI-AI ... -> <glowa rodu Wysp>" (A). Srednia tys. zl/d.

| okno | wplywy dnia (165) | danina P1 + 184 (lud Wysp) | zawor kas miast | 1/9 trzecich z bitew [S] | okupy wielkich jencow do skarbca | reszta (patrz nizej) | raty okupu krola od Polnocy | renty 180 | zwrot zoldu | doplata W5 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Q6 d456-546 | 14.0 | 6.8 | 4.5 | 0.4 | 0 | 2.3 | 0 | 5.1 | 2.3 | 6.6 |
| d547-582 | 15.4 | 7.8 | 4.6 | 0.4 | 0 | 2.7 | 0 | 6.2 | 1.8 | 7.5 |
| d583-637 | 57.2 | 17.4 | 10.3 | 6.2 | 2.5 | 20.8 | 0 | 36.7 | 15.6 | 3.5 |
| d638-675 | 65.9 | 27.8 | 13.5 | 3.9 | 3.0 | 17.7 | 0 | 34.2 | 23.1 | 2.5 |
| d676-719 | 72.7 | 35.5 | 16.2 | 2.4 | 3.7 | 14.9 | **43.7** | 87.9 | 28.0 | 0.2 |
| d701-728 | 72.7 | 38.8 | 14.9 | 3.2 | 0 | 15.8 | 28.2 | 72.4 | 28.2 | 0 |

- 1/360 zapasu = 0 przez cale okno: skarbiec Wysp 500 005-500 409 przy progu 500 000 (najnizej d663 po nagrodach 178) [P] (A "Skarbce: ... Iron Islands", np. d546 l. 213243: 500 179, d728 l. 285548: 500 294). Korona wydaje dokladnie to, co wplynelo tego dnia - nic nie odklada.
- Wydatki = wplywy: d547-728 wplynelo 12.01 mln (wplywy dnia 10.09 + raty 1.92), wydano 11.82 mln (renty 7.85, zwrot 3.27, W5 0.57, kontrakty 0.12, rata Banku 0.02; reszta ok. 0.2 mln to nagrody zdobywcow 178 z d663-665) [P]/[S].
- "Reszta" 2.72 mln w d547-728: z tego 1/9 okupow lordow Polnocy otrzymanych przez rody Wysp ok. 0.41 mln (3.69 mln / 9 [P] linie 178 / [S] udzial) i podatek BK od bogatych rodow ok. 0.34 mln (0.1% kiesy glowy ponad 100 000 [K] BK `EconomyPatches` - dekompilacja `...\3cf3e0ac...\scratchpad\audyt-pieniadz\bkall\BannerKings.Patches\EconomyPatches.cs:211-219`; [S] z `kiesa_glowy`). Ostatnie ok. 1.97 mln (10.8 tys./d, przed wojna 2.3-2.7) to strumienie, ktorych log nie rozbija na krolestwo: danina wojenna z kas wsi, udzial korony z zaworu zamkow (114), clo, powinnosci, przeniesione dlugi 1/9 - **nie wiem dokladnie, ile ktory**. Kazdy z nich w kodzie ma platnika (kasa osady / licznik cel / kiesa glowy rodu) [K] `KingdomTreasury.cs:370-443` (danina wojenna z kasy osady, clo z licznika cel), `KingdomTreasury.cs:99-103` (powinnosci z kiesy glowy rodu), `Ransom178.cs:717-725` (1/9), a ksiega swiata nie pokazuje zlota bez platnika (rozdz. 5).

## 2. Dzien po dniu - skoki i ich platnicy [P]

Pelna tabela dobowa d500-728 - Aneks A. Tu tylko doby, w ktorych wplywy dnia Wysp wyskoczyly ponad 2x tla:

| doba | wplywy dnia | co sie stalo (dowod) | kto zaplacil |
|---|---:|---|---|
| d583-588 | 26 / 36 / 55 / 83 / 35 / 30 | Polnoc (Halys Hornwood, 8 druzyn ok. 1 350) blokuje Lonely Light; wypad Wysp (Gylbert Farwynd, 20 druzyn, 3 163) rozbija ja w dniu gry 109420 (bitwy.log l. 40285; l. 40311 i 40313: Hornwood rozbity, "rozbici 566"); okup Ramsaya Snow 390 170 -> Goodbrother (A l. 228453), Ryswell 140 133 i Locke 50 398 -> Farwynd (l. 229244, 229246), Norrey 138 629 (l. 229642); trzecia lordow Wysp d586 175 798 (`budzet-rodow.csv`) | rozbite druzyny Polnocy (sakiewki ludzi), rody Bolton, Ryswell, Locke, Norrey |
| d603 | **171.8** | bitwa pod Blacktyde, dzien gry 109437: Rodrik Harlaw (Wyspy, 26 druzyn, 3 870) bije Howlanda Reeda (Polnoc, 16 druzyn, 2 625) (bitwy.log l. 41357); trzecia lordow Wysp 353 501 (`budzet-rodow.csv`, dzien_gry 109438); prawo trzecich swiata: "trzecie lordow 424265 (korona 1/3) ... sciagniete do skarbcow 158957" (A l. 235823); kasa miasta Blacktyde 65 -> 280 -> 582 tys. (`kasy-miast.csv` l. 58378, 58475) | sakiewki ludzi rozbitych druzyn Polnocy (1/3 do lorda zwyciezcy, korona 1/3 z tego; sakiewki druzyn niepokonanych przez wodza - do najblizszego miasta, Blacktyde) |
| d605-610 | 85 / 81 / 59 / 67 / 64 / 70 | okupy po Blacktyde: Reed 121 365, Mormont 172 274 (A l. 236344-5), Flint 236 830 (l. 236734), Karstark 62 240 (l. 238304) -> Baelor Blacktyde; danina P1 Wysp 8 -> 20 tys./d (lud Blacktyde wydaje lup: `kiesa-obieg.csv` Wysp, okno do dzien_gry 109438: `z_wojska` 181 099 wobec 27 403 tydzien wczesniej) | rody Reed, Mormont, Flint, Karstark; lud Wysp |
| d619 | 103.0 | okup Roose Bolton 677 980 -> Baelor Blacktyde (A l. 241952) - korona bierze 1/9 | rod Bolton |
| d620 | **180.7** | okup nastepcy tronu Polnocy Hallisa Mollena 139 534 "-> skarbiec Iron Islands (wielki jeniec)" (A l. 242347; pojmany d603, l. 235556) | rod Stark (gotowka) |
| d663-665 | 97 / 85 / 65 | Flint's Finger (dzien gry 109496-498): Wyspy rozbijaja ok. 20 druzyn Polnocy, pojmany Robb Stark (bitwy.log l. 45012; A l. 259531 "okup ok. 2143289 ... nagroda dla zdobywcy 214328 zl ze skarbca"); okupy Karstark 149 370, Dustin 101 281 i in. (l. 259911, 260309). **Korona Wysp w tych dobach nie daje rent** - placi nagrody zdobywcom 97 561 / 84 475 / 42 554 (A "Okupy (178): dzien 109498-109500 ... nagrody zdobywcow") i pozycza w Banku 23 459 + 20 343 na zwrot zoldu (165, A l. 259699, 260093) | lup i okupy - Polnoc; nagrody - z wplywow korony Wysp |
| d675 | **184.2** | okup Hallisa Mollena 115 030 -> skarbiec Wysp (A l. 264302) | rod Stark |
| d676 | **236.4** | okup krola: "Robb Stark ... wolny za 2084825 zl (D 104241, placi skarbiec The North): gotowka 162870, dlug 1921955 -> skarbiec Iron Islands" (A l. 264702); skarbiec Polnocy tego dnia -68 446 (A l. 264897) | skarbiec Polnocy |
| d677-719 | +43.7/d raty | 44 raty dlugu okupu krola: u Wysp "rata +r", u Polnocy "rata -r" w tej samej linii 165, suma po obu stronach **1 921 955** (A l. 265301 ... 281952; d705 l. 276382: "Iron Islands 60118+0 ... rata +44944 ... renty 75560", "The North 89228+661 ... rata -44944 ... renty 1") | skarbiec Polnocy (z wplywow dnia, najwyzej 50%) |

## 3. Kto zaplacil - d547-728 [P]/[S]

| platnik | ile (mln zl) | droga | dowod |
|---|---:|---|---|
| skarbiec Polnocy | 2.08 | okup krola 178 (gotowka 0.16 + 44 raty 1.92) do skarbca Wysp | A l. 264702; raty - linie 165 d676-719 |
| rod Stark | 0.25 | okupy wielkiego jenca (Hallis Mollen x2) do skarbca Wysp | A l. 242347, 264302 |
| rody Polnocy | 3.69 | 40 okupow lordow Polnocy do glow rodow Wysp (korona Wysp 1/9 = ok. 0.41) | linie "Okupy (178): AI-AI ... -> <rod Wysp>" d584-713 [P], 1/9 [S] |
| ludzie rozbitych druzyn Polnocy | b.d. | sakiewki ludzi -> 1/3 lord zwyciezca, 2/3 jego ludzie albo najblizsze miasto; korona 1/9 (szac. 0.63 mln w d547-728) | `budzet-rodow.csv` `trzecia`; [K] `MenPurse.cs:112-119`, `Ransom178.cs:711-725` |
| lud Wysp | 4.22 | danina z oszczednosci P1 (4.07) i podatek wojenny 184 (0.15) z kies ludu | 165 "(danina P1 y)", "(184 x)" [P] |
| kasy miast Wysp | 2.10 | udzial korony z zaworu kas miast (E5/A1) | `kasy-miast.csv` `do_korony` [P] |
| rody Wysp, wsie, zamki, clo | ok. 2.3 | podatek BK 0.1% (0.34 [S]), danina wojenna wsi, zawor zamkow 114, clo, powinnosci (rozdz. 1) | nie rozbite w logu |

Kolejnosc w tabeli nie jest kolejnoscia kwot: najwieksza pozycja wplywu korony to lud Wysp (4.22), potem kasy miast Wysp (2.10) i skarbiec Polnocy (2.08) - rozdz. 11.

Lud i kasy miast Wysp placa wiecej, bo do nich splywa lup: kiesy ludu Wysp (W) 0.29 mln (dzien_gry 109417) -> 0.58 (109438) -> 1.18 (109508) -> 1.44 mln (109557), wplyw "z wojska" w oknach 7 dob 57 tys. -> 181 tys. po Blacktyde i 267 tys. po Flint's Finger, "z dworu" (wydatki bogatych rodow) 0.6 -> 75.6 tys. (`kiesa-obieg.csv`, suma osad Wysp) [P]. Tak samo kasy miast Wysp: srednio 187 tys. (Q6) -> 484 tys. (d701-728), doplyw netto 11.4 -> 41.2 tys./d (`kasy-miast.csv`) [P].

## 4. Rody Wysp - skad kiesy 0.82 -> 6.49 mln [P]

- Kiesy rodow (linia "Skarbce: ... Iron Islands ... kiesy razem"): d546 822 020 (A l. 213243), d603 1 968 819, d620 3 807 352, d676 4 240 238, d705 5 947 934, d728 6 493 108 (A l. 285548). Wojsko rodow 3 153 -> 7 664 (d705) -> 6 770.
- Do rodow w d547-728: od korony 11.8 mln (renty 7.85, zwrot 3.27, W5 0.57, kontrakty 0.12 - rozdz. 1), okupy lordow Polnocy 3.69 mln, trzecia z lupu (suma `trzecia` d547-728 w `budzet-rodow.csv`) [P]; z tego rody oplacily wieksze wojsko (zold partii 23.0 -> 47.5 tys./d, `zold_partii` d580 / d705) i dwor.
- "Dochod D 19.5 -> 176.7, w tym od korony 7.5 -> 111.0" z raportu BIEG to te same liczby (`budzet-rodow.csv`: Q6 D 20.2, d_korona 7.3; d701-728 D 176.7, d_korona 111.0) - D jest srednia kroczaca, wiec d_korona (111.0) chwilowo wyzsza niz renty+zwrot tej doby (100.6).
- Zwrot zoldu 1.6 -> 29 tys./d ("Skarbce" d546 "zwrot zoldu 1552 dla 4 rodow" -> d705 "29246 dla 10 rodow") rosnie, bo rosnie wojsko w polu (zwrot = 70% zoldu partii w polu) i jest z czego zwrocic - nie jest osobnym zrodlem.

## 5. Ksiega pieniadza swiata - czy "zrodla bez platnika" rosna razem ze skokiem [P]

Linie "Pieniadz swiata" i "Pieniadz swiata (bilans - przyczyny)" (A; dzien ksiegi = doba 165 minus 1), srednie tys. zl/d:

| okno | zrodla z niczego S | w tym zold oddany do obiegu (para z rozliczeniem rodow) | w tym "bitwy" (gra) | reszta niewyjasniona R | R w naszym ticku RB | zmiana sumy swiata |
|---|---:|---:|---:|---:|---:|---:|
| Q6 d456-546 | 797.0 | 773.0 | 23.5 | +0.2 | +0.2 | -0.7 |
| d547-582 | 657.0 | 639.6 | 16.8 | -0.6 | +0.2 | -1.8 |
| d583-637 | 642.4 | 620.8 | 21.1 | +0.1 | 0.0 | -0.8 |
| d638-675 | 642.6 | 628.4 | 13.4 | -0.1 | 0.0 | -2.0 |
| d676-728 | 621.4 | 608.5 | 12.3 | 0.0 | +0.1 | -0.9 |

- S **nie rosnie** ze skokiem Wysp (spada 797 -> 621). R w d547-728 razem -23 131 zl (max dobowy +12 372, min -47 651), RB (nasz tick) razem +13 045 zl - przy +5.67 mln w kiesach Wysp. ARYTMETYKA OK w 182 z 182 dob.
- Suma swiata: 207 864 073 (A l. 213357, ksiega dzien 109380 = d546) -> 207 634 239 (l. 285666, d728). Gdyby korona Wysp dostawala zloto z niczego, wyszloby w R (skarbce krolestw sa posiadaczem w ksiedze), a nie wyszlo.
- "bitwy" (zloto gry dla zwyciezcow) skacze w doby bitew Wysp (d586 43 011, d603 57 104, d664 59 852), ale lup A6 zdejmuje je pokonanym: "netto z niczego na ladzie +0 ... pokonani bez platnika 0" w dniach gry 109418-421, 109437-438, 109497-498 (A "Lup w bitwie (A6)", np. l. 235918).
- Lustro: Polnoc. Kiesy rodow Polnocy 3 791 775 (d675, A l. 264497) -> 1 540 625 (d719, l. 281960) -> 1 549 729 (d728, l. 285551); skarbiec 825 741 -> 721 921; renty korony Polnocy do jej rodow 59.4 (d640-675) -> **0.8** (d676-719) tys./d, zwrot 42.2 -> 22.4, raty do Wysp 43.7 (linie 165) [P]. Brak na zold Polnocy (W5 "niepokryte") 0.2 / 0.0 / 2.3 tys./d w Q6 / Q7 / Q8 - Polnoc nie odczula tego w W5, bo jej rody przejadaly kiesy (3.8 -> 1.5 mln) [P]/[S].

## 6. Kod - dlaczego to nie jest podwojne liczenie [K]

- Wplywy dnia sa roznica stanu skarbca, a nie suma licznikow: `Own = dues + lev + cas + tow + thirds + wt + wa`, `Other = w - w0 - Own`, `Measured = max(0, Own + Other)` = `w - w0` - `CrownIncome.cs:191-195` (zb\int: 206-210). Zle przypisany licznik moze najwyzej przesunac kwote miedzy "nasze" i "inne", ale nie podwoic wplywu.
- Migawka `w0` jest robiona w `End()` po wszystkich wydatkach i ratach dnia - `CrownIncome.cs:306` (int: 322); rata przyjeta tego dnia (`db.Left += ip`) siedzi juz w migawce, wiec jutro nie wraca jako "inne".
- Rata okupu krola / reparacji: najpierw placacy, potem odbiorca, ta sama kwota - `CrownIncome.cs:244` i `:250` (int: 259, 265); dlug przepada, gdy krolestwa nie ma (`:234`) - nikt nie dostaje z niczego.
- Okup krola ze skarbca: gotowka zdejmowana ze skarbca placacego `Ransom178.cs:311`, reszta jako dlug korony `:314`; odbiorca dostaje do skarbca `:288`. Okup zwyklego lorda: gotowka zdejmowana z kies rodziny `Ransom178.cs:179` (Collect).
- 1/9 trzecich: z kiesy lorda do skarbca, ta sama kwota - `Ransom178.cs:723` i `:725`.
- Lup z rozbitej druzyny: sakiewka zdjeta (`MenPurse.cs:112`), potem 1/3 do wodza zwyciezcow i 2/3 do jego ludzi (`:119`) albo do najblizszego miasta (`:113`, BeatenPurse) - stad skok kasy Blacktyde.
- Podatek BK od bogatych (cudzy kod, dekompilacja BK `EconomyPatches.cs:211-219`): `KingdomBudgetWallet += (gold - 100000) * 0.001` i ta sama kwota jako wydatek rodu w rozliczeniu; ksiega widzi pare ("podatek BK od bogatych do skarbcow ... (przelew)", "skarbce krolestw w trakcie rozliczen +x").

## 7. Mechanizmy sprawdzone i wykluczone [P]

- Reparacje Diplomacy: 3 w calym biegu, zadna z Wyspami (A l. 45933 Vale -> Riverlands, 170455 Dorne -> Reach, 178324 Norvos -> Qohor). Wyspy nie mialy pokoju z Polnoca (wojna od d434, A l. 169250; z Dragonstone od d674, l. 264303).
- Skarbiec upadlej korony G14: Stormlands d536 -> kapital Banku 852 980 (A l. 209469), Norvos d635 -> kiesa glowy rodu Hortyr 500 199 (l. 248748); do Wysp nic.
- Lup stolicy L5b-1: Wyspy nie zdobyly zadnej twierdzy (`balans-krolestw.csv` sturgia `twierdze_zdobyte` 0 do konca; stracily 1 - Lonely Light).
- Okup oblezenia: Wyspy tylko jako oblegani i bez wyplaty (O0 - pomiar: Blacktyde d597 A l. 233577, Lonely Light d607 l. 237528); do Wysp 0.
- Dary 182, kontrakty 185 (Wyspy placa Stone Crows 1.6-1.8 tys./d), kredyt Banku 186 (Wyspy pozyczaly - dlug 43 865 w d674 po nagrodach 178) - zadne z nich nie zasila Wysp z zewnatrz.

## 8. Co z tego wynika (nie bledy, ale do wiedzy przy pracy) [S]

1. Raport BIEG rozdz. 0 pkt 10 i 4.1: "Zelazne Wyspy 20 -> 0 ... tego nie umiemy wyjasnic" - wyjasnione: to przelew od Polnocy po wygranej wojnie i lup w kasach Wysp. Spadek "razem fin30" 58.8 -> 22.9 nie jest poprawa systemu w 19.9 tys./d; Polnoc zaplacila za to kiesami rodow (-2.25 mln w d675-719), co w W5 jeszcze nie wyszlo.
2. Okup krola ze skarbca (178) = `RansomKingDays` (20) x srednie wplywy korony - `Ransom178.cs:131-136` ("Jeff 10.10 (wariant 1): krol 20 dni wplywow korony"); raty najwyzej `CrownReparationShare` 0.5 wplywow dnia (`McmSettings.cs:3361`). Wyszlo 2.08 mln = 44 doby po ok. 50% wplywow Polnocy; przez 6 tygodni rody Polnocy prawie bez rent korony. Zgodne z kodem i decyzja; czy ma byc tak ciezkie - sprawa projektu wojny / oblezen, nie DP15.
3. Nagroda zdobywcy 10% okupu (Robb 214 328) jest placona z wplywow dnia, zanim okup wplynie - korona Wysp w d663-665 dala nagrody zamiast rent i pozyczyla 45 tys. w Banku na zwrot zoldu. Zgodne z kodem, zloto ma platnika.
4. Petla "korona -> rody -> zold i dwor -> lud i kasy miast -> P1 i zawor -> korona" kreci te same pieniadze (P1 Wysp 6.6 -> 38.8 tys./d); to obieg, nie nowe zloto - ale przy pustym skarbcu (prog 500 tys.) kazdy wplyw tego samego dnia staje sie renta, wiec korona Wysp nie ma zadnej poduszki na czas, gdy lup sie skonczy.

## 9. Czego nie wiem

- Rozbicia "reszty" ok. 1.97 mln (10.8 tys./d) na danine wojenna wsi, zawor zamkow 114, clo i powinnosci Wysp - log podaje te strumienie tylko dla calego swiata. Platnik jest w kodzie dla kazdego, a ksiega swiata nie ma dziury (rozdz. 5).
- Dokladnej 1/9 korony Wysp na dobe (linia 178 podaje podstawe tylko dla swiata) - w tabeli jest szacunek z kolumn rodow.

## 10. Pliki i polecenia

Skrypty i pliki posrednie (dysk C, katalog tymczasowy): `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\dp15\` - `ext.py` (strumieniowo z A: 165, 180, Skarbce, P1, W5, ksiega -> `ii.json`), `km.py` (`kasy-miast.csv` -> `km.json`), `br.py` (`budzet-rodow.csv` -> `br.json`), `ob.py` (Obieg korony swiata), `zr.py` (zrodla z niczego), `win.py` (okna), `gen.py` (Aneks A), `l165_y2.txt` (linie 165 d565-728 do rat Polnoc -> Wyspy), `src6e\` (kod 6e6313c). Uruchamiane `python -I`.

## 11. Weryfikacja (sceptyk, 14.10 - niezalezny przelicz z logu)

**Werdykt koncowy: CECHA, nie BLAD - stoi.** Zloto Wysp ma platnika, a ksiega swiata nie ma dziury. **Nie stoi jedno zdanie:** "zaplacila glownie Polnoc". Najwieksza pozycja wplywu korony Wysp to danina P1 **wlasnego ludu Wysp**. Polnoc jest glownym zrodlem **nowego** pieniadza Wysp, ale nie glownym platnikiem korony. Poprawione: rozdz. 0 pkt 1 i 6, uwaga pod tabela w rozdz. 3.

Jak liczone: wlasne skrypty (`...\c36b73ca-...\scratchpad\dp15v\` - `ext.py`, `an.py`, `tab.py`, `ob.py`, `python -I`, strumieniowo). Z A wziete: fragment "na krolestwo" linii 165 dla Iron Islands i The North (182 doby d547-728, zadnej brakujacej), "Skarbce:", "Pieniadz swiata", "Pieniadz swiata (bilans - przyczyny)" (pole "kontrola") i "Okupy (178): AI-AI". Z T: `kasy-miast.csv` (`do_korony`, `kasa`), `kiesa-ludu.csv`, `kiesa-obieg.csv`, `budzet-rodow.csv` (`trzecia`). Kod: `git show 6e6313c:...` w `zb\int`.

### 11.1 Wplywy korony Wysp d547-728 wedlug platnika

| pozycja | platnik | zl | udzial | dowod |
|---|---|---:|---:|---|
| danina z oszczednosci ludu P1 | kiesy ludu Wysp | 4 072 953 | 33.9% | [P] 165 "(danina P1 y)"; [K] `TownFolk.Savings.cs:89` (zdjete z kiesy klasy ludu), `KingdomTreasury.WarAid.cs:224` (do skarbca) |
| zawor kas miast (E5/A1) | kasy miast Wysp | 2 108 567 | 17.6% | [P] `kasy-miast.csv` `do_korony` |
| okup krola Robba: gotowka 162 870 + 44 raty 1 921 955 | skarbiec Polnocy | 2 084 825 | 17.4% | [P] A l. 264702; kazda z 44 rat Wysp (d676-719) rowna co do zlotowki "rata -" Polnocy w tej samej linii 165 |
| 1/3 trzeciej lordow Wysp (lup z sakiewek ludzi pobitych druzyn) | glownie rozbite druzyny Polnocy | ok. 468 000 | 3.9% | [S] `budzet-rodow.csv` `trzecia` 1 405 tys. / 3 |
| 1/9 okupow lordow Polnocy | rody Polnocy | 0.32-0.41 mln | ok. 3% | [S] 1/9 od gotowki 2 912 098 i od kazdej splaconej raty dlugu - [K] `Ransom178.cs:293`, `DebtLadder.cs:851` |
| okupy wielkiego jenca (Hallis Mollen x2) | rod Stark | 254 564 | 2.1% | [P] A l. 242347, 264302 |
| podatek wojenny ludu (184) | kiesy ludu Wysp | 150 891 | 1.3% | [P] 165 "(184 x)" |
| reszta: BK 0.1%, danina wojenna wsi, zawor zamkow, clo, powinnosci, 1/9 innych zdarzen | nie rozbite na krolestwo | ok. 2.4-2.5 mln | ok. 20% | [S] rozdz. 1 i 9 |
| **razem** (W 10 089 018 + raty 1 921 955) | | **12 010 973** | 100% | [P] 165 |

- Lud i miasta Wysp razem: 6 332 411 zl (52.7%) [P]. Polnoc wprost: 2 339 389 zmierzone [P] + ok. 0.8-0.9 mln szacunku [S] = ok. 3.1-3.2 mln (26-27%).
- To samo w samym **przyroscie** ponad tempo Q6 (13.96 tys./d x 182 = 2.54 mln; przyrost 9.47 mln): lud i miasta Wysp +4.27 mln (45%), Polnoc wprost ok. +3.1 mln (33%), reszta ok. 2.1 mln [P]/[S].
- Najwieksza **pojedyncza** wplata to okup Robba (2 084 825, skarbiec Polnocy). Najwiekszy **strumien** to P1 ludu Wysp.

### 11.2 Skad lud Wysp ma na P1 [P]

`kiesa-obieg.csv`, osady Wysp, tys. zl/d: zarobek ludu z zewnatrz i z kas 16.8 (Q6) -> 36.1 (d547-728) -> 49.3 (d663-728). Przyrost w Q7-Q8:
- wojsko +6.2 - wydatki zolnierzy w miescie z ich sakiewek, a sakiewki pelnia sie z zoldu i z 2/3 lupu ([K] `MenPurse.cs:119`, `:319`);
- robocizna handlu +5.8;
- dwor bogatych rodow +4.0;
- zawor +1.3;
- obrot +0.9;
- warsztaty +0.8.

Danina okna (22.6 tys./d) to ta sama kwota, co P1 w linii 165 (22.4 tys./d). Wojsko i dwor to wydatki rodow Wysp. Rody placa je z rent korony (to obieg) i z okupow oraz lupu od Polnocy (to nowy pieniadz) [S].

### 11.3 Bilans calych Wysp i Polnocy, d546 -> d728 [P]

| | Wyspy | Polnoc |
|---|---:|---:|
| kiesy rodow ("Skarbce") | 822 020 -> 6 493 108 (+5 671 088) | 3 866 434 -> 1 549 729 (-2 316 705) |
| skarbiec korony | 500 179 -> 500 294 (+115) | 964 745 -> 721 921 (-242 824) |
| kiesy ludu (`kiesa-ludu.csv`) | 285 025 -> 1 564 227 (+1 279 202) | 2 365 360 -> 1 891 322 (-474 038) |
| kasy miast (`kasy-miast.csv`) | 168 791 -> 532 284 (+363 493) | 1 075 838 -> 985 965 (-89 873) |
| **razem** | **+7 313 898** | **-3 123 440** |

- **Okupy od Polnocy do Wysp, d547-728** (razem ceny 6 057 155) [P]:
  - rody: 41 okupow na 3 717 766, z czego gotowka 2 912 098, a dlug 805 668;
  - rod Stark: 254 564;
  - krol ze skarbca: 2 084 825, splacone w calosci do d719.

  Gotowka i raty razem: 5 251 487.
- **Lup:** 1/3 dla lordow Wysp to 1 405 tys. (`trzecia`) [P]. Reszta lupu poszla do ich ludzi i miast - tej czesci log nie mierzy na krolestwo.
- **Pokrycie przyrostu:** nowy pieniadz od Polnocy to co najmniej 6.66 mln. Pokrywa to ok. 90% przyrostu Wysp (7.31 mln) [S].
- **Polnoc** stracila tylko 3.12 mln, bo ma wlasne wplywy: jej korona dostala w tych dobach 18.47 mln [P].
- **Uwaga do pkt 0.2:** 40 okupow na 3.69 mln to doby d585-714. W calym oknie d547-728 sa 41 okupy na 3.72 mln - dochodzi Cley Cerwyn d547, 31 468 zl (A l. 213445). 0.81 mln z tej kwoty to dlug okupu, a nie gotowka.

### 11.4 Pieniadz swiata - sprawdzone [P]

- **Ksiega:** dni 109381-109562 (182 doby, czyli zmiany d547-728), "ARYTMETYKA OK" w 182 z 182.
  - Reszta R razem -23 131 zl (min -47 651, max +12 372), w naszym ticku (RB) +13 045.
  - Suma swiata 207 864 073 (A l. 213357) -> 207 634 239 (l. 285666), czyli -229 834.
- **Zrodla z niczego S** (tys. zl/d): 795.6 (Q6) -> 656.2 -> 641.8 -> 642.9 -> 621.2. Spadaja wtedy, gdy Wyspy rosna.
- **Lup A6 w doby bitew Wysp:** "netto z niczego na ladzie +0". Dni gry 109420, 109421, 109437, 109438, 109439, 109498, 109499; A l. 229204, 229606, 235918, 236306, 236695, 260268, 260669.
- **Skarbiec Wysp** przez 182 doby: najnizej 500 005 (d663), najwyzej 500 409 (d662). 1/360 zapasu = 0.
- **Wydatki korony Wysp:** 11 816 262 zl = renty 7 849 520 + zwrot 3 266 217 + W5 566 877 + kontrakty 117 395 + rata Banku 16 253.
  - Wplynelo 12 010 973 zl, do tego kredyt Banku 45 286.
  - Po odjeciu wydatkow zostaje ok. 0.24 mln. Tyle wyplacono jako nagrody zdobywcow 178 w d663-665: 97 561 + 84 475 + 42 554 zl.
- **Wniosek:** korona wydaje dokladnie to, co wplynelo. Gdyby W bylo zawyzone, skarbiec zszedlby ponizej progu - a nie zszedl.

### 11.5 Kod sprawdzony na 6e6313c [K]

- `CrownIncome.cs`:
  - `:191-195` - W = w - w0;
  - `:306` - migawka w `End()`;
  - `:244`, `:250` - rata: najpierw placacy, potem odbiorca.
- `Ransom178.cs`:
  - `:311`, `:313`, `:314` - okup krola: gotowka ze skarbca placacego, reszta jako dlug korony;
  - `:288` - odbiorca: skarbiec;
  - `:179` - gotowka okupu lorda z kies rodziny;
  - `:723`, `:725` - 1/9 z kiesy lorda do skarbca.
- `MenPurse.cs:112-119` - sakiewka pobitego zdjeta: 1/3 dla wodza, 2/3 dla jego ludzi albo do miasta.
- P1: `TownFolk.Savings.cs:89` i `KingdomTreasury.WarAid.cs:224`.
- **Polnoc w d676 - zloto zeszlo naprawde** [P] (A l. 264897):
  - W = 0, bo `Measured = max(0, ...)`, a gotowka okupu zjadla wplyw dnia;
  - skarbiec -68 446;
  - 23 004 zl pozyczone w Banku;
  - zwrot zoldu 23 198 z 37 493 naleznego.

### 11.6 Co z rozpoznania stoi bez zmian [P]

- Okup krola (A l. 264702) i 44 raty = 1 921 955 (d676-719).
- Renty Polnocy 59.0 -> 0.8 tys./d (d638-675 -> d676-719).
- Kiesy rodow Polnocy 3 791 775 -> 1 540 625 (A l. 264497, 281960).
- Skarbiec Wysp na progu; kiesy rodow Wysp 0.82 -> 6.49 mln.
- Wykluczone sa reparacje, G14, L5b-1 i okup oblezenia. Przeczytane linie: A l. 45933, 170455, 178324, 209469, 248748, 233577, 237528.
- **Drobne poprawki do opisu bitew:**
  - Lonely Light, d586: glowna bitwa (bitwy.log l. 40285) skonczyla sie "bez rozstrzygniecia", choc Polnoc stracila 32% zabitych. Rozbita zostala dopiero reszta Hornwooda (l. 40313).
  - Flint's Finger, d661-663: to nie jedna bitwa, tylko seria malych zwyciestw nad rozbitymi juz druzynami Polnocy (bitwy.log l. 44902-45070).

## Aneks A. Korona Zelaznych Wysp dzien po dniu, d500-728 [P]

Kolumny (tys. zl): wplywy dnia (165) | P1 + 184 (lud Wysp) | zawor kas miast do korony (`kasy-miast.csv`) | pozostale wplywy = wplywy - P1 - 184 - zawor (1/9 trzecich i okupow, okupy wielkich jencow do skarbca, danina wojenna, zawor zamkow, clo, BK) | raty okupu krola od Polnocy | renty 180 | zwrot zoldu | doplata W5 | kiesy rodow (mln, "Skarbce") | wojsko rodow | zdarzenie (A = log Armoury, linia). Skarbiec Wysp przez caly czas 500 0xx-500 4xx.

| d | wplywy | P1+184 | miasta | pozostale | raty krola | renty | zwrot | W5 | kiesy | wojsko | zdarzenie |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 500 | 14.5 | 6.1 | 3.6 | 4.7 | 0.0 | 8.2 | 0.4 | 5.9 | 0.88 | 3074 |  |
| 501 | 12.1 | 6.3 | 3.9 | 2.0 | 0.0 | 5.0 | 1.0 | 6.0 | 0.87 | 3066 |  |
| 502 | 13.3 | 6.6 | 4.5 | 2.2 | 0.0 | 4.2 | 2.8 | 6.3 | 0.87 | 3046 |  |
| 503 | 12.8 | 6.6 | 4.1 | 2.2 | 0.0 | 3.8 | 2.7 | 6.3 | 0.86 | 3062 |  |
| 504 | 14.7 | 6.6 | 5.5 | 2.6 | 0.0 | 5.5 | 2.9 | 6.4 | 0.87 | 3014 |  |
| 505 | 13.1 | 6.5 | 4.3 | 2.3 | 0.0 | 5.6 | 1.3 | 6.3 | 0.86 | 2984 |  |
| 506 | 14.0 | 6.4 | 5.1 | 2.5 | 0.0 | 5.1 | 2.7 | 6.2 | 0.86 | 3013 |  |
| 507 | 15.5 | 6.7 | 6.0 | 2.8 | 0.0 | 8.5 | 0.5 | 6.4 | 0.86 | 3015 |  |
| 508 | 15.1 | 6.7 | 5.9 | 2.6 | 0.0 | 5.8 | 2.9 | 6.4 | 0.86 | 3015 |  |
| 509 | 15.0 | 6.6 | 5.8 | 2.6 | 0.0 | 5.7 | 2.8 | 6.4 | 0.86 | 3015 |  |
| 510 | 15.6 | 6.8 | 6.2 | 2.7 | 0.0 | 6.9 | 2.3 | 6.5 | 0.86 | 2990 |  |
| 511 | 15.9 | 6.8 | 6.4 | 2.7 | 0.0 | 6.8 | 2.5 | 6.5 | 0.86 | 2990 |  |
| 512 | 17.5 | 6.9 | 5.7 | 5.0 | 0.0 | 10.3 | 0.6 | 6.6 | 0.86 | 3015 |  |
| 513 | 15.3 | 7.0 | 5.8 | 2.5 | 0.0 | 6.9 | 1.6 | 6.7 | 0.86 | 3077 |  |
| 514 | 16.5 | 7.1 | 6.7 | 2.8 | 0.0 | 7.1 | 2.6 | 6.8 | 0.86 | 3134 |  |
| 515 | 14.9 | 7.1 | 5.4 | 2.5 | 0.0 | 5.6 | 2.5 | 6.8 | 0.86 | 3149 |  |
| 516 | 15.0 | 7.0 | 5.1 | 2.9 | 0.0 | 5.2 | 3.0 | 6.8 | 0.86 | 3182 |  |
| 517 | 15.3 | 7.2 | 5.6 | 2.5 | 0.0 | 7.1 | 1.2 | 6.9 | 0.86 | 3232 |  |
| 518 | 17.7 | 7.5 | 7.2 | 3.0 | 0.0 | 8.0 | 2.5 | 7.2 | 0.85 | 3146 |  |
| 519 | 15.3 | 7.4 | 5.2 | 2.7 | 0.0 | 5.0 | 3.2 | 7.1 | 0.85 | 3174 |  |
| 520 | 14.7 | 7.2 | 4.9 | 2.5 | 0.0 | 4.7 | 2.9 | 7.0 | 0.85 | 3135 |  |
| 521 | 15.2 | 7.4 | 5.2 | 2.6 | 0.0 | 5.6 | 2.5 | 7.1 | 0.85 | 3102 |  |
| 522 | 15.2 | 7.4 | 5.2 | 2.6 | 0.0 | 5.3 | 2.8 | 7.2 | 0.85 | 3116 |  |
| 523 | 15.0 | 7.4 | 5.0 | 2.6 | 0.0 | 5.1 | 2.6 | 7.2 | 0.85 | 3117 |  |
| 524 | 15.5 | 7.5 | 5.3 | 2.6 | 0.0 | 7.1 | 1.1 | 7.3 | 0.85 | 3157 |  |
| 525 | 15.7 | 7.6 | 5.3 | 2.7 | 0.0 | 7.2 | 1.1 | 7.4 | 0.85 | 3165 |  |
| 526 | 15.6 | 7.5 | 5.3 | 2.8 | 0.0 | 5.7 | 2.6 | 7.3 | 0.85 | 3176 |  |
| 527 | 15.2 | 7.6 | 5.0 | 2.6 | 0.0 | 5.0 | 2.8 | 7.4 | 0.85 | 3198 |  |
| 528 | 16.5 | 7.7 | 5.1 | 3.8 | 0.0 | 7.9 | 1.2 | 7.4 | 0.85 | 3221 |  |
| 529 | 15.7 | 7.7 | 4.1 | 3.9 | 0.0 | 5.9 | 2.3 | 7.4 | 0.85 | 3192 |  |
| 530 | 13.6 | 7.6 | 3.7 | 2.2 | 0.0 | 3.5 | 2.7 | 7.4 | 0.84 | 3241 |  |
| 531 | 13.3 | 7.5 | 3.6 | 2.2 | 0.0 | 3.1 | 2.9 | 7.3 | 0.84 | 3226 |  |
| 532 | 14.4 | 7.6 | 4.4 | 2.4 | 0.0 | 5.8 | 1.2 | 7.3 | 0.84 | 3243 |  |
| 533 | 15.5 | 7.8 | 4.9 | 2.8 | 0.0 | 6.2 | 1.8 | 7.5 | 0.84 | 3329 |  |
| 534 | 15.9 | 7.9 | 5.3 | 2.7 | 0.0 | 7.0 | 1.3 | 7.6 | 0.84 | 3346 |  |
| 535 | 17.3 | 8.0 | 6.3 | 2.9 | 0.0 | 7.0 | 2.6 | 7.7 | 0.84 | 3342 |  |
| 536 | 18.3 | 8.0 | 5.9 | 4.3 | 0.0 | 8.0 | 2.5 | 7.7 | 0.84 | 3331 |  |
| 537 | 14.8 | 7.8 | 4.4 | 2.6 | 0.0 | 5.2 | 2.0 | 7.6 | 0.84 | 3324 |  |
| 538 | 13.9 | 7.6 | 3.9 | 2.4 | 0.0 | 3.7 | 2.8 | 7.4 | 0.83 | 3308 |  |
| 539 | 12.1 | 7.9 | 2.3 | 1.9 | 0.0 | 3.3 | 1.2 | 7.6 | 0.83 | 3361 |  |
| 540 | 12.3 | 7.9 | 2.5 | 1.9 | 0.0 | 1.6 | 3.1 | 7.6 | 0.82 | 3360 |  |
| 541 | 12.6 | 7.7 | 2.9 | 2.0 | 0.0 | 2.8 | 2.3 | 7.4 | 0.82 | 3361 |  |
| 542 | 12.6 | 7.5 | 3.0 | 2.1 | 0.0 | 2.3 | 3.0 | 7.3 | 0.82 | 3312 |  |
| 543 | 13.5 | 7.6 | 3.6 | 2.2 | 0.0 | 3.6 | 2.4 | 7.4 | 0.82 | 3257 |  |
| 544 | 13.6 | 7.5 | 2.9 | 3.2 | 0.0 | 4.2 | 2.2 | 7.3 | 0.82 | 3218 |  |
| 545 | 13.3 | 7.4 | 3.5 | 2.4 | 0.0 | 3.9 | 2.2 | 7.2 | 0.82 | 3178 |  |
| 546 | 13.0 | 7.2 | 3.4 | 2.4 | 0.0 | 4.5 | 1.6 | 7.0 | 0.82 | 3153 |  |
| 547 | 16.6 | 7.1 | 3.7 | 5.8 | 0.0 | 9.0 | 0.7 | 6.9 | 0.85 | 3129 |  |
| 548 | 14.6 | 7.1 | 2.9 | 4.6 | 0.0 | 5.5 | 2.3 | 6.9 | 0.84 | 3098 |  |
| 549 | 12.1 | 7.1 | 3.0 | 2.0 | 0.0 | 3.6 | 1.7 | 6.9 | 0.84 | 3121 |  |
| 550 | 12.2 | 7.0 | 3.2 | 2.1 | 0.0 | 3.2 | 2.2 | 6.8 | 0.83 | 3161 |  |
| 551 | 12.8 | 7.1 | 3.5 | 2.1 | 0.0 | 4.2 | 1.6 | 6.9 | 0.83 | 3159 |  |
| 552 | 13.5 | 7.2 | 4.0 | 2.3 | 0.0 | 3.7 | 2.9 | 6.9 | 0.83 | 3150 |  |
| 553 | 13.7 | 7.2 | 4.1 | 2.5 | 0.0 | 6.0 | 0.8 | 6.9 | 0.83 | 3180 |  |
| 554 | 14.4 | 7.2 | 4.6 | 2.7 | 0.0 | 5.2 | 2.3 | 6.9 | 0.83 | 3181 |  |
| 555 | 14.4 | 7.2 | 4.5 | 2.7 | 0.0 | 5.8 | 1.7 | 6.9 | 0.83 | 3169 |  |
| 556 | 14.2 | 7.1 | 4.4 | 2.6 | 0.0 | 5.0 | 2.3 | 6.9 | 0.83 | 3185 |  |
| 557 | 15.1 | 7.2 | 5.1 | 2.9 | 0.0 | 5.6 | 2.5 | 6.9 | 0.84 | 3194 |  |
| 558 | 15.6 | 7.2 | 5.4 | 2.9 | 0.0 | 6.2 | 2.4 | 7.0 | 0.84 | 3198 |  |
| 559 | 16.1 | 7.4 | 5.7 | 3.0 | 0.0 | 7.3 | 1.7 | 7.1 | 0.84 | 3204 |  |
| 560 | 18.0 | 7.5 | 5.2 | 5.3 | 0.0 | 9.3 | 1.5 | 7.2 | 0.84 | 3261 |  |
| 561 | 15.7 | 7.6 | 4.7 | 3.5 | 0.0 | 5.7 | 2.7 | 7.3 | 0.85 | 3226 |  |
| 562 | 15.1 | 7.7 | 4.8 | 2.7 | 0.0 | 6.3 | 1.5 | 7.4 | 0.85 | 3236 |  |
| 563 | 15.8 | 7.8 | 5.0 | 3.0 | 0.0 | 7.5 | 0.8 | 7.5 | 0.85 | 3268 |  |
| 564 | 15.5 | 7.7 | 4.8 | 2.9 | 0.0 | 6.2 | 1.8 | 7.5 | 0.86 | 3273 |  |
| 565 | 15.8 | 7.8 | 5.1 | 2.9 | 0.0 | 6.2 | 2.1 | 7.6 | 0.86 | 3273 |  |
| 566 | 14.8 | 7.8 | 4.3 | 2.7 | 0.0 | 6.3 | 1.0 | 7.5 | 0.86 | 3271 |  |
| 567 | 16.8 | 8.0 | 5.3 | 3.5 | 0.0 | 7.4 | 1.8 | 7.7 | 0.86 | 3259 |  |
| 568 | 16.3 | 8.0 | 4.5 | 3.8 | 0.0 | 7.3 | 1.2 | 7.7 | 0.86 | 3264 |  |
| 569 | 16.5 | 8.1 | 4.7 | 3.7 | 0.0 | 8.2 | 0.5 | 7.8 | 0.86 | 3282 |  |
| 570 | 16.0 | 8.2 | 5.1 | 2.8 | 0.0 | 5.2 | 3.0 | 7.9 | 0.86 | 3309 |  |
| 571 | 16.3 | 8.1 | 5.4 | 2.8 | 0.0 | 6.1 | 2.4 | 7.8 | 0.86 | 3310 |  |
| 572 | 18.1 | 8.3 | 6.6 | 3.1 | 0.0 | 7.4 | 2.6 | 8.1 | 0.86 | 3285 |  |
| 573 | 16.2 | 8.5 | 5.0 | 2.7 | 0.0 | 7.1 | 0.9 | 8.2 | 0.86 | 3319 |  |
| 574 | 16.6 | 8.5 | 5.3 | 2.8 | 0.0 | 7.3 | 1.1 | 8.2 | 0.86 | 3336 |  |
| 575 | 16.5 | 8.5 | 5.2 | 2.8 | 0.0 | 7.2 | 1.1 | 8.2 | 0.87 | 3336 |  |
| 576 | 18.3 | 8.5 | 5.1 | 4.8 | 0.0 | 9.7 | 0.4 | 8.2 | 0.87 | 3387 |  |
| 577 | 16.8 | 8.8 | 5.2 | 2.8 | 0.0 | 7.8 | 0.5 | 8.5 | 0.86 | 3401 |  |
| 578 | 17.5 | 8.8 | 4.9 | 3.9 | 0.0 | 7.9 | 1.2 | 8.5 | 0.86 | 3436 |  |
| 579 | 16.3 | 8.8 | 4.9 | 2.6 | 0.0 | 7.3 | 0.5 | 8.5 | 0.86 | 3436 |  |
| 580 | 13.9 | 7.6 | 4.0 | 2.3 | 0.0 | 5.2 | 1.3 | 7.4 | 0.86 | 3451 |  |
| 581 | 13.6 | 7.4 | 3.9 | 2.3 | 0.0 | 2.1 | 4.3 | 7.2 | 0.86 | 3451 |  |
| 582 | 13.8 | 7.1 | 2.7 | 4.0 | 0.0 | 2.6 | 4.2 | 7.0 | 0.86 | 3442 |  |
| 583 | 26.5 | 6.9 | 2.5 | 17.1 | 0.0 | 16.5 | 3.2 | 6.8 | 0.87 | 3287 |  |
| 584 | 35.9 | 6.7 | 2.5 | 26.7 | 0.0 | 26.5 | 2.9 | 6.5 | 0.93 | 2800 |  |
| 585 | 55.4 | 6.7 | 2.7 | 46.0 | 0.0 | 46.1 | 3.0 | 6.3 | 1.30 | 2631 | okup Ramsaya Snow 390 170 -> Goodbrother (A 228453); korona 1/9 |
| 586 | 83.4 | 10.3 | 6.6 | 66.5 | 0.0 | 71.0 | 4.0 | 8.4 | 1.33 | 2752 | Lonely Light: wypad Wysp rozbija armie Hornwooda (bitwy.log 40285, 40311); trzecia lordow 176 tys. |
| 587 | 34.6 | 11.0 | 5.4 | 18.1 | 0.0 | 19.1 | 6.4 | 9.1 | 1.45 | 2906 | okupy Ryswell 140 133 i Locke 50 398 -> Farwynd (A 229244, 229246) |
| 588 | 29.8 | 10.5 | 6.2 | 13.1 | 0.0 | 13.7 | 7.6 | 8.5 | 1.54 | 3102 | okup Norrey 138 629 -> Farwynd (A 229642) |
| 589 | 21.9 | 10.6 | 6.4 | 4.9 | 0.0 | 3.7 | 9.3 | 8.9 | 1.54 | 3496 |  |
| 590 | 20.1 | 9.8 | 6.2 | 4.1 | 0.0 | 3.0 | 8.5 | 8.6 | 1.54 | 3649 |  |
| 591 | 22.1 | 10.0 | 5.5 | 6.6 | 0.0 | 4.1 | 9.7 | 8.3 | 1.53 | 3823 |  |
| 592 | 20.5 | 10.0 | 6.1 | 4.5 | 0.0 | 1.2 | 11.3 | 8.0 | 1.53 | 3847 |  |
| 593 | 33.5 | 9.8 | 6.2 | 17.5 | 0.0 | 13.6 | 11.4 | 8.5 | 1.56 | 3863 |  |
| 594 | 25.7 | 9.8 | 5.1 | 10.8 | 0.0 | 5.7 | 11.3 | 8.7 | 1.55 | 3879 |  |
| 595 | 25.1 | 9.7 | 5.1 | 10.3 | 0.0 | 5.4 | 11.1 | 8.6 | 1.55 | 3881 |  |
| 596 | 30.8 | 9.6 | 5.5 | 15.7 | 0.0 | 10.9 | 11.4 | 8.5 | 1.54 | 3882 |  |
| 597 | 35.6 | 9.6 | 5.1 | 20.9 | 0.0 | 15.6 | 11.5 | 8.6 | 1.62 | 3883 |  |
| 598 | 27.9 | 9.8 | 5.8 | 12.3 | 0.0 | 7.8 | 11.2 | 8.9 | 1.61 | 3916 |  |
| 599 | 26.7 | 6.2 | 3.0 | 17.6 | 0.0 | 9.8 | 10.9 | 6.0 | 1.59 | 3910 |  |
| 600 | 26.3 | 5.9 | 3.0 | 17.3 | 0.0 | 9.7 | 10.8 | 5.8 | 1.57 | 3904 |  |
| 601 | 31.7 | 8.5 | 5.5 | 17.7 | 0.0 | 14.9 | 9.2 | 7.5 | 1.56 | 3506 |  |
| 602 | 30.7 | 8.2 | 4.9 | 17.6 | 0.0 | 14.5 | 8.9 | 7.3 | 1.54 | 3270 |  |
| 603 | 171.8 | 15.0 | 13.8 | 143.0 | 0.0 | 137.1 | 8.6 | 11.0 | 1.97 | 3699 | Blacktyde: Harlaw (26 druzyn, 3 870) bije armie Reeda (16 druzyn, 2 625) (bitwy.log 41357); trzecia lordow 353 tys.; kasa Blacktyde +296 tys. (kasy-miast.csv 58378) |
| 604 | 76.1 | 23.1 | 26.3 | 26.7 | 0.0 | 61.9 | 9.9 | 4.3 | 2.01 | 3772 | kasa Blacktyde +427 tys. (kasy-miast.csv 58475) |
| 605 | 84.7 | 21.4 | 20.9 | 42.4 | 0.0 | 66.9 | 15.4 | 2.5 | 2.16 | 4223 | okupy Reed 121 365, Mormont 172 274 -> Blacktyde (A 236344, 236345) |
| 606 | 80.5 | 19.9 | 19.9 | 40.7 | 0.0 | 57.8 | 18.2 | 3.0 | 2.86 | 4448 | okup Flint 236 830 -> Blacktyde (A 236734) |
| 607 | 59.0 | 19.8 | 18.8 | 20.4 | 0.0 | 36.8 | 17.9 | 2.5 | 2.91 | 4455 |  |
| 608 | 66.7 | 19.5 | 18.1 | 29.1 | 0.0 | 45.1 | 17.9 | 2.0 | 2.93 | 4449 |  |
| 609 | 64.3 | 19.3 | 16.1 | 28.9 | 0.0 | 41.7 | 18.6 | 2.3 | 2.95 | 4445 |  |
| 610 | 70.2 | 19.1 | 15.2 | 35.9 | 0.0 | 47.3 | 18.4 | 2.8 | 3.02 | 4444 |  |
| 611 | 54.0 | 15.9 | 14.7 | 23.4 | 0.0 | 31.7 | 18.0 | 2.6 | 3.02 | 4440 |  |
| 612 | 90.5 | 17.7 | 8.0 | 64.8 | 0.0 | 68.6 | 18.2 | 2.0 | 3.09 | 4456 | trzecia lordow 106 tys. |
| 613 | 69.9 | 20.0 | 13.4 | 36.5 | 0.0 | 50.7 | 17.1 | 0.4 | 3.07 | 4604 |  |
| 614 | 67.1 | 20.5 | 12.4 | 34.2 | 0.0 | 49.7 | 15.8 | 0.0 | 3.09 | 4804 |  |
| 615 | 65.4 | 20.0 | 13.0 | 32.4 | 0.0 | 42.3 | 21.4 | 0.0 | 3.09 | 4901 |  |
| 616 | 41.8 | 20.1 | 14.1 | 7.6 | 0.0 | 21.7 | 18.3 | 0.2 | 3.09 | 5174 |  |
| 617 | 41.9 | 20.3 | 13.6 | 7.9 | 0.0 | 17.8 | 22.4 | 0.0 | 3.12 | 5412 |  |
| 618 | 44.4 | 20.4 | 12.6 | 11.4 | 0.0 | 23.6 | 19.1 | 0.0 | 3.11 | 5419 |  |
| 619 | 103.0 | 20.3 | 13.9 | 68.8 | 0.0 | 79.3 | 22.0 | 0.0 | 3.66 | 5239 | okup Roose Bolton 677 980 -> Baelor Blacktyde (A 241952) |
| 620 | 180.7 | 20.1 | 11.3 | 149.2 | 0.0 | 156.9 | 22.0 | 0.0 | 3.81 | 5159 | okup Hallis Mollen 139 534 -> skarbiec Wysp (A 242347) |
| 621 | 57.0 | 20.0 | 11.5 | 25.5 | 0.0 | 32.2 | 23.0 | 0.0 | 3.86 | 5236 |  |
| 622 | 43.2 | 19.8 | 9.9 | 13.5 | 0.0 | 18.2 | 23.2 | 0.0 | 3.84 | 5249 |  |
| 623 | 58.8 | 19.7 | 9.3 | 29.7 | 0.0 | 33.8 | 23.2 | 0.0 | 3.84 | 5253 |  |
| 624 | 64.4 | 20.3 | 10.7 | 33.4 | 0.0 | 40.9 | 21.8 | 0.0 | 3.86 | 5297 |  |
| 625 | 70.2 | 26.7 | 13.6 | 29.9 | 0.0 | 53.5 | 15.0 | 0.0 | 3.87 | 5437 |  |
| 626 | 65.6 | 26.3 | 13.1 | 26.2 | 0.0 | 42.3 | 21.5 | 0.0 | 3.86 | 5537 |  |
| 627 | 60.0 | 25.8 | 13.2 | 21.1 | 0.0 | 37.1 | 19.4 | 0.0 | 3.73 | 5902 |  |
| 628 | 68.5 | 25.4 | 11.3 | 31.8 | 0.0 | 41.1 | 23.9 | 0.0 | 3.73 | 5929 |  |
| 629 | 66.1 | 25.9 | 11.8 | 28.4 | 0.0 | 44.8 | 17.9 | 0.0 | 3.73 | 6204 |  |
| 630 | 67.4 | 26.4 | 11.7 | 29.2 | 0.0 | 43.8 | 20.1 | 0.0 | 3.79 | 6322 |  |
| 631 | 68.0 | 26.3 | 7.6 | 34.1 | 0.0 | 41.5 | 23.0 | 0.0 | 3.75 | 6543 |  |
| 632 | 58.1 | 26.2 | 8.9 | 23.0 | 0.0 | 34.8 | 19.9 | 0.0 | 3.74 | 6608 |  |
| 633 | 54.2 | 26.2 | 9.5 | 18.4 | 0.0 | 27.7 | 23.0 | 0.0 | 3.73 | 6697 |  |
| 634 | 61.4 | 26.7 | 11.1 | 23.6 | 0.0 | 42.0 | 17.6 | 0.0 | 3.15 | 6513 |  |
| 635 | 70.9 | 26.9 | 11.6 | 32.4 | 0.0 | 46.0 | 23.1 | 0.0 | 3.17 | 6544 |  |
| 636 | 71.0 | 26.4 | 12.5 | 32.2 | 0.0 | 44.1 | 25.2 | 0.0 | 3.17 | 6597 |  |
| 637 | 67.0 | 26.4 | 13.2 | 27.3 | 0.0 | 42.8 | 22.4 | 0.0 | 3.17 | 6607 |  |
| 638 | 61.9 | 26.3 | 13.4 | 22.1 | 0.0 | 34.9 | 25.1 | 0.0 | 3.18 | 6623 |  |
| 639 | 66.4 | 26.1 | 12.8 | 27.5 | 0.0 | 36.1 | 26.4 | 2.1 | 3.17 | 6628 |  |
| 640 | 65.4 | 27.0 | 13.9 | 24.4 | 0.0 | 41.4 | 20.3 | 2.1 | 3.17 | 6705 |  |
| 641 | 72.7 | 26.7 | 13.6 | 32.4 | 0.0 | 46.1 | 22.5 | 2.1 | 3.10 | 6884 |  |
| 642 | 75.9 | 26.2 | 13.1 | 36.6 | 0.0 | 48.3 | 23.4 | 2.5 | 3.14 | 6886 |  |
| 643 | 60.8 | 26.1 | 12.5 | 22.1 | 0.0 | 32.9 | 22.9 | 2.9 | 3.16 | 6916 |  |
| 644 | 68.5 | 26.0 | 12.5 | 30.0 | 0.0 | 38.1 | 25.4 | 3.0 | 3.25 | 6930 |  |
| 645 | 67.5 | 26.1 | 13.8 | 27.5 | 0.0 | 40.0 | 22.6 | 2.9 | 3.24 | 6949 |  |
| 646 | 65.3 | 26.5 | 13.2 | 25.7 | 0.0 | 45.2 | 14.8 | 3.6 | 3.24 | 6951 |  |
| 647 | 57.7 | 26.3 | 12.2 | 19.1 | 0.0 | 30.8 | 21.5 | 3.2 | 3.23 | 6995 |  |
| 648 | 69.4 | 26.1 | 12.9 | 30.4 | 0.0 | 43.1 | 21.0 | 2.9 | 3.22 | 7037 |  |
| 649 | 48.8 | 26.1 | 12.7 | 10.0 | 0.0 | 17.2 | 26.0 | 3.1 | 3.22 | 7062 |  |
| 650 | 61.7 | 26.2 | 13.6 | 21.9 | 0.0 | 36.0 | 19.7 | 3.5 | 3.22 | 7083 |  |
| 651 | 66.4 | 25.8 | 12.4 | 28.1 | 0.0 | 34.8 | 25.7 | 3.5 | 3.23 | 7087 |  |
| 652 | 64.3 | 25.7 | 12.5 | 26.1 | 0.0 | 36.1 | 24.1 | 1.7 | 3.22 | 7316 |  |
| 653 | 48.2 | 25.9 | 13.1 | 9.2 | 0.0 | 21.9 | 20.8 | 3.1 | 3.21 | 7342 |  |
| 654 | 49.7 | 25.9 | 14.1 | 9.6 | 0.0 | 19.2 | 26.3 | 1.8 | 3.19 | 7385 |  |
| 655 | 49.4 | 25.7 | 13.9 | 9.8 | 0.0 | 19.1 | 26.1 | 1.8 | 3.20 | 7382 |  |
| 656 | 48.5 | 25.5 | 13.3 | 9.7 | 0.0 | 15.8 | 27.1 | 3.2 | 3.20 | 7387 |  |
| 657 | 49.0 | 25.7 | 13.6 | 9.7 | 0.0 | 21.3 | 22.2 | 3.1 | 3.21 | 7409 |  |
| 658 | 48.9 | 25.9 | 13.4 | 9.6 | 0.0 | 19.6 | 24.2 | 2.7 | 3.21 | 7420 |  |
| 659 | 50.4 | 26.1 | 14.4 | 9.9 | 0.0 | 19.5 | 25.0 | 3.5 | 3.22 | 7432 |  |
| 660 | 49.4 | 25.8 | 13.7 | 9.9 | 0.0 | 18.4 | 25.0 | 3.6 | 3.22 | 7373 |  |
| 661 | 49.3 | 25.8 | 13.5 | 10.0 | 0.0 | 22.1 | 24.1 | 3.1 | 2.80 | 7119 |  |
| 662 | 55.2 | 25.7 | 12.7 | 16.7 | 0.0 | 26.8 | 25.5 | 2.9 | 2.83 | 7102 |  |
| 663 | 97.2 | 25.6 | 7.9 | 63.6 | 0.0 | 0.0 | 23.5 | 0.0 | 3.04 | 7051 | Flint's Finger: pojmani Robb Stark i lordowie Polnocy (bitwy.log 45012; A 259531); korona placi nagrode zdobywcom 97 561, kredyt Banku 23 459 |
| 664 | 84.6 | 25.9 | 6.0 | 52.8 | 0.0 | 0.0 | 20.3 | 0.0 | 3.24 | 7076 | okupy Karstark 149 370 i in. -> Merlyn (A 259911); nagroda 84 475 |
| 665 | 64.7 | 26.6 | 6.8 | 31.2 | 0.0 | 0.0 | 23.3 | 0.0 | 3.44 | 7079 | okup Dustin 101 281 -> Drumm (A 260309); nagroda 42 554 |
| 666 | 47.0 | 26.5 | 6.9 | 13.5 | 0.0 | 18.9 | 24.9 | 2.9 | 3.48 | 7069 |  |
| 667 | 61.5 | 28.1 | 11.7 | 21.7 | 0.0 | 33.8 | 24.0 | 3.4 | 3.57 | 7071 |  |
| 668 | 66.7 | 32.9 | 18.8 | 14.9 | 0.0 | 39.7 | 23.4 | 3.3 | 3.60 | 7043 |  |
| 669 | 80.4 | 35.3 | 15.2 | 29.9 | 0.0 | 50.1 | 26.8 | 3.3 | 3.72 | 7014 |  |
| 670 | 70.3 | 34.0 | 14.4 | 22.0 | 0.0 | 45.5 | 23.0 | 1.6 | 3.79 | 7055 |  |
| 671 | 68.9 | 33.8 | 16.9 | 18.1 | 0.0 | 48.8 | 16.8 | 3.0 | 3.81 | 7136 |  |
| 672 | 68.3 | 34.1 | 18.2 | 16.0 | 0.0 | 45.1 | 20.1 | 2.9 | 3.82 | 7232 |  |
| 673 | 70.6 | 34.6 | 19.4 | 16.7 | 0.0 | 50.5 | 17.1 | 2.8 | 3.85 | 7230 |  |
| 674 | 70.8 | 34.4 | 19.1 | 17.2 | 0.0 | 39.9 | 27.8 | 2.8 | 3.88 | 7246 |  |
| 675 | 184.2 | 34.5 | 21.9 | 127.9 | 0.0 | 162.3 | 19.0 | 2.6 | 4.01 | 7223 | okup Hallis Mollen 115 030 -> skarbiec Wysp (A 264302) |
| 676 | 236.4 | 33.9 | 22.1 | 180.4 | 0.4 | 206.4 | 27.8 | 2.3 | 4.24 | 7234 | okup Robba Starka 2 084 825 placi skarbiec Polnocy: gotowka 162 870 + dlug 1 921 955 (A 264702) |
| 677 | 70.1 | 34.0 | 22.7 | 13.4 | 55.7 | 103.8 | 20.0 | 1.8 | 4.19 | 7350 | pierwsza pelna rata okupu krola (Polnoc 'rata -55668', A 265301) |
| 678 | 69.4 | 34.1 | 22.0 | 13.3 | 46.7 | 93.4 | 21.1 | 1.4 | 4.26 | 7367 |  |
| 679 | 80.1 | 34.0 | 22.1 | 24.0 | 45.9 | 97.5 | 27.1 | 1.1 | 4.40 | 7403 |  |
| 680 | 67.9 | 34.0 | 19.6 | 14.2 | 45.3 | 85.0 | 27.0 | 0.8 | 4.44 | 7415 |  |
| 681 | 66.7 | 33.9 | 19.8 | 12.9 | 44.9 | 85.4 | 25.3 | 0.6 | 4.51 | 7433 |  |
| 682 | 72.5 | 33.6 | 18.3 | 20.6 | 45.6 | 90.6 | 27.0 | 0.3 | 4.58 | 7444 |  |
| 683 | 72.5 | 33.6 | 18.2 | 20.7 | 45.8 | 93.3 | 24.6 | 0.0 | 4.71 | 7427 |  |
| 684 | 67.1 | 33.7 | 18.1 | 15.3 | 45.4 | 83.6 | 28.6 | 0.0 | 4.77 | 7440 |  |
| 685 | 66.4 | 33.8 | 19.3 | 13.3 | 45.5 | 85.8 | 25.9 | 0.0 | 4.83 | 7380 |  |
| 686 | 65.1 | 33.5 | 18.3 | 13.3 | 48.1 | 84.7 | 28.3 | 0.0 | 4.89 | 7377 |  |
| 687 | 76.1 | 33.4 | 17.5 | 25.2 | 47.8 | 98.5 | 25.1 | 0.0 | 4.99 | 7415 |  |
| 688 | 63.4 | 33.3 | 16.9 | 13.3 | 47.0 | 79.8 | 30.3 | 0.0 | 5.04 | 7441 |  |
| 689 | 78.7 | 34.1 | 15.1 | 29.5 | 48.8 | 113.5 | 13.7 | 0.0 | 5.25 | 7468 | okup Rhonda Stout 148 702 -> Farwynd (A 269865) |
| 690 | 65.1 | 34.6 | 16.2 | 14.4 | 48.0 | 86.1 | 26.8 | 0.0 | 5.30 | 7568 |  |
| 691 | 63.1 | 34.0 | 15.8 | 13.3 | 47.1 | 78.7 | 31.3 | 0.0 | 5.35 | 7595 |  |
| 692 | 64.0 | 34.3 | 16.2 | 13.5 | 47.9 | 88.5 | 23.2 | 0.0 | 5.39 | 7603 |  |
| 693 | 65.3 | 34.3 | 16.2 | 14.8 | 48.0 | 82.1 | 30.9 | 0.0 | 5.43 | 7614 |  |
| 694 | 63.7 | 33.9 | 15.8 | 14.0 | 47.1 | 80.9 | 29.6 | 0.0 | 5.47 | 7674 |  |
| 695 | 62.7 | 33.9 | 14.9 | 13.9 | 46.7 | 82.8 | 26.3 | 0.0 | 5.51 | 7732 |  |
| 696 | 62.6 | 34.0 | 15.0 | 13.7 | 47.9 | 77.6 | 32.7 | 0.0 | 5.55 | 7799 |  |
| 697 | 63.6 | 34.4 | 15.2 | 14.0 | 48.6 | 84.1 | 27.8 | 0.0 | 5.59 | 7790 |  |
| 698 | 60.1 | 34.0 | 12.5 | 13.6 | 47.0 | 73.5 | 33.3 | 0.0 | 5.62 | 7828 |  |
| 699 | 60.2 | 33.8 | 12.7 | 13.7 | 46.8 | 74.5 | 32.3 | 0.0 | 5.65 | 7867 |  |
| 700 | 62.0 | 34.1 | 13.8 | 14.1 | 45.3 | 73.3 | 33.7 | 0.0 | 5.68 | 7907 |  |
| 701 | 62.4 | 34.2 | 13.9 | 14.2 | 44.8 | 76.7 | 30.3 | 0.0 | 5.71 | 7884 |  |
| 702 | 62.0 | 33.9 | 13.9 | 14.2 | 43.4 | 72.6 | 32.6 | 0.0 | 5.74 | 7825 |  |
| 703 | 61.5 | 33.5 | 13.7 | 14.2 | 43.7 | 73.0 | 31.9 | 0.0 | 5.77 | 7667 |  |
| 704 | 99.4 | 34.1 | 11.0 | 54.2 | 44.9 | 112.4 | 31.7 | 0.0 | 5.93 | 7558 |  |
| 705 | 60.1 | 34.7 | 11.5 | 13.9 | 44.9 | 75.6 | 29.2 | 0.0 | 5.95 | 7664 |  |
| 706 | 63.9 | 35.4 | 11.8 | 16.6 | 43.0 | 82.0 | 24.7 | 0.0 | 5.97 | 7604 |  |
| 707 | 84.2 | 39.5 | 14.3 | 30.5 | 43.6 | 106.2 | 21.4 | 0.0 | 6.13 | 7655 | okup Barbrey Dustin 152 168 -> Orkwood (A 276986) |
| 708 | 73.3 | 39.3 | 14.8 | 19.1 | 42.8 | 85.3 | 30.5 | 0.0 | 6.17 | 7580 |  |
| 709 | 70.0 | 38.2 | 16.1 | 15.7 | 42.1 | 81.9 | 30.0 | 0.0 | 6.22 | 7497 |  |
| 710 | 71.7 | 38.4 | 17.3 | 16.0 | 42.5 | 89.4 | 24.6 | 0.0 | 6.26 | 7467 |  |
| 711 | 72.5 | 38.3 | 16.0 | 18.2 | 41.9 | 81.7 | 32.4 | 0.0 | 6.29 | 7401 |  |
| 712 | 75.9 | 39.0 | 17.1 | 19.8 | 40.9 | 87.1 | 29.4 | 0.0 | 6.33 | 7415 |  |
| 713 | 71.2 | 39.0 | 16.3 | 15.9 | 41.2 | 82.5 | 29.7 | 0.0 | 6.37 | 7403 |  |
| 714 | 72.0 | 38.5 | 15.4 | 18.2 | 40.8 | 82.0 | 30.5 | 0.0 | 6.41 | 7434 |  |
| 715 | 71.6 | 39.0 | 15.7 | 16.9 | 40.2 | 83.5 | 28.1 | 0.0 | 6.45 | 7470 |  |
| 716 | 70.0 | 39.4 | 14.1 | 16.5 | 40.1 | 83.1 | 26.7 | 0.0 | 6.49 | 7503 |  |
| 717 | 68.1 | 39.6 | 13.2 | 15.3 | 39.0 | 75.0 | 31.9 | 0.0 | 6.51 | 7432 |  |
| 718 | 70.0 | 39.9 | 14.5 | 15.7 | 39.9 | 82.2 | 27.6 | 0.0 | 6.53 | 7319 |  |
| 719 | 74.1 | 40.6 | 17.0 | 16.5 | 29.1 | 72.5 | 30.4 | 0.0 | 6.56 | 7192 | ostatnia rata 29 058; 44 raty = 1 921 955 (A 281952) |
| 720 | 76.3 | 41.0 | 14.7 | 20.6 | 0.0 | 46.3 | 29.7 | 0.0 | 6.55 | 7215 |  |
| 721 | 69.3 | 40.7 | 12.8 | 15.8 | 0.0 | 40.7 | 28.3 | 0.0 | 6.54 | 7130 |  |
| 722 | 72.2 | 40.9 | 14.7 | 16.6 | 0.0 | 49.4 | 22.5 | 0.0 | 6.53 | 6836 |  |
| 723 | 85.7 | 41.5 | 13.9 | 30.3 | 0.0 | 57.4 | 28.0 | 0.0 | 6.52 | 6963 |  |
| 724 | 72.7 | 41.6 | 14.8 | 16.3 | 0.0 | 49.8 | 22.6 | 0.0 | 6.51 | 6944 |  |
| 725 | 74.8 | 41.7 | 16.0 | 17.0 | 0.0 | 48.7 | 25.8 | 0.0 | 6.50 | 7091 |  |
| 726 | 75.1 | 41.5 | 16.2 | 17.5 | 0.0 | 46.1 | 28.8 | 0.0 | 6.48 | 6872 |  |
| 727 | 76.3 | 41.6 | 17.3 | 17.4 | 0.0 | 52.5 | 23.5 | 0.0 | 6.49 | 6797 |  |
| 728 | 78.7 | 41.3 | 18.1 | 19.4 | 0.0 | 51.3 | 27.1 | 0.0 | 6.49 | 6770 |  |
