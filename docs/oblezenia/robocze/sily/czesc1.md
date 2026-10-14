# ROZPOZNANIE: SILY KROLESTW (bieg dwuletni fin30, 14.10)

Rozpoznanie do sprawy Jeffa z 14.10: "sily nie sa rowne, oblezenia sa za szybkie i za latwe".
Tylko pomiar i kod - bez zmian, bez propozycji rozwiazania (to robia projekty).
Znaczniki: [P] pomiar (plik / linia), [K] kod (plik:linia), [U] ustawienie (plik), [S] szacunek.

## 0. W skrocie (prostym jezykiem)

1. Krolestwa jako calosc nie sa bardzo nierowne: zdobywca mial zwykle 1.1-2.3 razy wiecej ludzi w druzynach niz obronca. Nierownosc robi sie pod murami: przychodzi armia 13-16 lordow (ok. 2400-3000 ludzi) na ok. 430 obroncow - mediana 7.4 : 1, ani jednego odpartego szturmu.
2. Zalogi nie sa mniejsze niz w grze bez modow (zamek 254, miasto 517 ludzi na poczatku oblezenia; gra daje 200 / 400 miejsc). Ale w 11 z 57 zdobyc obroncy przegrali tuz przed szturmem bitwe pod murami (gra robi ja ze szturmu, gdy dolacza oddzial spoza murow) i szturm szedl juz tylko na milicje.
3. Odsieczy prawie nie ma: Krolewska Przystan padla, gdy lordowie Baratheonow bili sie w Dorzeczu (42 z 59 ich bitew); Dorne broni sie po kawalku, malymi oddzialami przeciw bandom.
4. Przegrany w polu prawie nie ginie (nasze reguly: 152 tys. zabitych zamiast 399 tys. w grze, 166 tys. rozbitych wraca pod choragiew), a zaloga pobita pod murami ginie jak w grze - armia oblegajaca sie odradza, obrona nie.
5. Wojsko idzie za pieniedzmi, a pieniadze za ludzmi: Reach (8 mln ludzi) ma pulap zoldu 3.2 razy wiekszy niz Dorne (1.5 mln), wiec Dorne wypelnia druzyny w 36-42 %, Reach w 75 %; kto traci zamek, traci dochod i wojsko.
6. Krolestwo w pokoju prawie nie ma wojska (Dorne 5 %, Dolina 14 %, Targaryen 19 % limitu na koniec), a w roku 2 budzet rodow zwolnil z zalog 49 tys. ludzi (w roku 1 - 5 tys.).
7. Stormlands zniknely przez fabule ROT (smierc Renly'ego d536: 16 twierdz do Smoczej Skaly), nie przez oblezenia. Krolewska Przystan i cztery inne zdobycze Polnocy dostal rod Bolton. Harroway w tabelach caly bieg nalezy do Dorzecza.
8. Nowe krolestwa (19, z tego 18 w roku 2, wszystkie w Essos) to rody, ktore odchodza z krolestwa razem ze swoimi lennami - bez oblezen.

## 1. Zrodla i umowy

| Co | Gdzie |
|---|---|
| log Armoury fin30 (A) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log` (287 MB) |
| sesja CrashScribe fin30 (S) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-14_06-01-14.log` |
| tabele fin30 (T) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15\` - `balans-krolestw.csv`, `budzet-rodow.csv`, `ludzie-regiony.csv`, `spichlerze-regiony.csv`, `bitwy.log` |
| tabele fin21 (porownanie) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_21-14-13\` |
| kod gry 1.4.8 | `...\ae222cb7...\scratchpad\dec32\cs_full` |
| kod BK | `...\ae222cb7...\scratchpad\e4tools\bkfull` |
| kod ROT | `ROT-Core\bin\Win64_Shipping_Client\ROT.dll` (dekompilacja RenlyDeathEvent: `robocze\sily\dekompilacja\RenlyDeathEvent.cs`, zgodna 1:1 z `...\scratchpad\a6dec\rot`) |
| nasz kod | drzewo `...\scratchpad\zb\int` (galaz w-toku/zbior-e8, 24bd64b3 - 34 commity po fin30 = 6e6313c; wartosci K8 i NorthHomeEdge sprawdzone w logu fin30) |

- **Doba wzgledna**: d = dzien gry - 108835 (Harrenhal d128, Krolewska Przystan d530 - jak w raporcie). Log obejmuje d1-d729 [P] (`balans-krolestw.csv`, pierwszy wiersz 108837, ostatni 109564).
- **Kalendarz**: rok = 364 doby [P] (A, linia "Kalendarz: rok ma 364 dni"); d364 = koniec roku 1.
- **Krolestwa wedlug id** [K] `a6dec\rot\ROT.Misc\ROTKingdoms.cs:12-36`: vlandia = Baratheon KP (w kodzie ROT "Westerlands"), battania = Polnoc, empire_w = Reach, aserai = Dorne, empire = Dolina, sturgia = Zelazne Wyspy, empire_s = Targaryen (Dany), khuzait = Dothrakowie, nord = Lorath, valyrian = Targaryen Aegon.
- **Wiersz dnia w `ludzie-regiony.csv` / `spichlerze-regiony.csv`** czasem pokazuje juz stan po zdarzeniach z pierwszych godzin nastepnej doby (Cape Wrath: wiersz 108857 ma juz nowego wlasciciela, szturm w `bitwy.log` ma dzien 108858) [P]. Dlatego "przed zdobyciem" = ostatni wiersz, w ktorym twierdza nalezy jeszcze do obroncy.
- **Milicja\*** w tabelach regionow = milicja twierdzy + milicja jej wsi [K] `zb\int\Armoury\src\PeopleLedger.cs:408-419`. W szturmie bierze udzial tylko milicja twierdzy.
- Pliki posrednie (TSV, skrypty liczace w scratchpadzie): `docs\oblezenia\robocze\sily\` - lista w rozdz. 10.

## 2. Wojsko krolestw w czasie

### 2.1 Ziemia, ludzie, druzyny, zalogi, milicja

Zrodla: twierdze, druzyny, zalogi - [P] T `balans-krolestw.csv` (kolumny twierdze, ludzie_partie, ludzie_zalogi); ludzie i milicja\* - [P] T `ludzie-regiony.csv` (suma regionow krolestwa). Wolne Miasta = Braavos, Pentos, Norvos, Qohor, Myr, Lys, Tyrosh, Lorath, Volantis (bez buntownikow i hrabstw). Wyliczenie: `robocze\sily\sily-krolestw.tsv`.

