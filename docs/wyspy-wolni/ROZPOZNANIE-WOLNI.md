# ROZPOZNANIE: WOLNI LUDZIE I NOCNA STRAZ - ZOLD, LUP, WOJSKO (14.10.2026, tylko odczyt)

Zakres: decyzja Jeffa 14.10 o Wolnych Ludziach ("nie pobierali kasy jak Nocna Straz, walczyli za sprawe, nie za kase; nie pobieraja kasy albo pobieraja znacznie
mniej, albo nie pobieraja, ale zyja z lupow wojennych"). Pytania: (1) jak dzis Nocna Straz nie placi zoldu, (2) Wolni Ludzie dzis, (3) skad mogliby zyc z lupow,
(4) co z ich wojskiem bez zoldu, (5) lore. Bez zmian w kodzie, bez commitow, gra nieuruchamiana, nic nie wgrane.

Znaczniki: **[P]** pomiar z logu albo tabeli CSV, **[K]** odczyt kodu (nasze mody, ROT, BK, gra - plik:linia), **[S]** szacunek albo wniosek (metoda podana).
Czego nie da sie ustalic - "nie wiem" + linia diagnozy (rozdz. 7). Przyczyn nie zakladam.

**Zrodla.**
- Bieg dwuletni (728 dob): `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log`, tabele `...\2026-10-14-noc\Logs\2026-10-13_21-14-13\`
  (`budzet-rodow.csv`, `bitwy.log`, `ludzie-regiony.csv`, `kiesa-ludu.csv`, `kiesa-obieg.csv`, `kasy-miast.csv`, `balans-krolestw.csv`, `ludnosc-bk.csv`, `wioski-okregi.csv`),
  sesja CS `session-2026-10-13_21-14-11.log` (KRONIKA WOJEN). Bieg 120 dob: `...\Armoury-2026-10-14_05-22-14.log` (do porownania).
- Kod: drzewo `zb\ww` (galaz `w-toku/wyspy-wolni`, 63aaaedf): `CrownGifts.cs` (182), `MountedWage.cs`, `ClanBudget.cs` (166), `WatchLaw.cs` (E4/9), `GentryService.cs` (179),
  `WarLedger.cs`, `DesertionLaw.cs`, `DistrictRaid.cs` (E7/R1), `SmallGold.cs` (E5/A7), `BattlePlunder.cs` (A6), `BeatenPurse.cs` (3.2), `CasualtyLoot.cs`, `SoldierPay.cs`,
  `PrisonerLaw.cs`, `PeopleLedger.cs`; BK `e4tools\bkfull` (`BKPartyWageModel.cs`); gra `dec32\cs_full`; ROT - dekompilacja `rotdec` (moj scratchpad c36b73ca).
- Liczby pieniedzy roku 2: `docs/pieniadze-rok-2/ROZPOZNANIE-RODY.md` rozdz. 2 (te same linie W5 / 165 / Skarbce i ten sam CSV).

Doba = dzien gry - 108836 + 1. Kwartaly po 91 dob: Q1 d2-91 ... Q4 d274-364 (koniec roku 1), Q5 d365-455 ... Q8 d638-728. Kwoty: tys. zl na dobe (srednia kwartalu),
chyba ze napisano inaczej. Id krolestw (linia "KingdomBalance (175.0)"): Wolni Ludzie `freefolk`, Nocna Straz `nightswatch`, Polnoc `battania`, Zelazne Wyspy `sturgia`.

---

## 0. Dla Jeffa - osiem zdan

1. Nocna Straz nie placi zoldu przez nasza regule 182 (Twoja decyzja 07.10): zold kazdego zolnierza w jej druzynach i zalogach (takze druzyny gracza) jest 0; BK i tak liczy minimum 1 zl dziennie na czlowieka (w zalodze ok. pol), wiec Straz placi ok. 1 zl za brata.
2. Liczbe braci trzyma "pulap w ludziach": tylu ludzi, ilu Straz moglaby oplacic, gdyby placila - wiec dalej z jej dochodu i oszczednosci; Straz ma 1-2 mln zl, trzymala 2.5-6 tys. ludzi, a dary koron Westeros przez 2 lata nie byly potrzebne ani razu.
3. Wolni Ludzie maja 7 rodow z ziemia (3 obozy-miasta bez mieszczan, 4 zamki, 18 wsi, 280 tys. ludzi), ale caly ich lud ma 64 tys. zl oszczednosci (0.2% swiata) i zarabia 5 tys. zl dziennie; rody przejadly 1.1 mln zl startowych w roku 1, w roku 2 brakuje im 21 tys. zl dziennie na zold.
4. Ich wojsko w polu spadlo z 2.6 do 0.7 tys. ludzi, zalogi (3 tys.) zjadaja 3/4 ich pieniedzy; w 2 lata przegrali 1 221 z 1 358 bitew - bija ich Straz (karawany i tabory wsi) i Inni, a oni sami atakuja tylko bandy.
5. Z lupow dzis prawie nic nie maja: nasz rabunek wsi daje ulamek kiesy wsi (caly swiat: 61 tys. zl w 2 lata), a ROT pozwala Wolnym Ludziom wojowac tylko ze Straza - Polnocy rabowac nie moga; Dar Strazy to 11 wsi (od d512 tylko 5) - najechali go 4 razy, w pierwszym kwartale.
6. Dzis jest odwrotnie niz w lore: to Straz lupi Wolnych Ludzi - 302 ich karawany i 284 tabory wsi w 2 lata, a cala kiesa rozbitej karawany idzie do zwyciezcy.
7. Regula Strazy 1:1 malo Wolnym Ludziom da: ich "pulap w ludziach" liczylby sie z ich malego dochodu - na starcie ok. 0.8 tys. ludzi (dzis maja 3.8 tys.), po ok. 1.5 roku oszczedzania 2-2.5 tys.; "walka za sprawe" wymaga innego hamulca niz zloto (ludzie, jedzenie, lup) - to decyzja projektu.
8. Bez zoldu armia nie urosnie bez konca: trzymaja ja limity druzyn gry (ok. 5.3 tys.), zalogi gry, jedzenie (AI kupuje je za zloto wodza) i straty; ludzi do walki im nie zabraknie (pula ok. 80 tys.); zniknie dezercja z zaleglego zoldu, a ich lud straci czesc z 1.8 tys. zl dziennie, ktore dzis zarabia na wojsku (jaka czesc - nie wiem).

---

## 1. Nocna Straz - jak dzis nie placi zoldu

### 1.1 Mechanizm [K]

To NIE jest `WatchLaw` ani ROT. `WatchLaw.cs` (E4/9) decyduje tylko, SKAD sa nowi bracia (Mur, pule wyrzutkow Westeros, ludnosc Westeros) i zakazuje Strazy palic wsie (H1).
Brak zoldu to paczka **182 "WatchUnpaid"** (decyzja Jeffa 07.10) w czterech miejscach:

| # | co | gdzie | szczegol |
|---|---|---|---|
| 1 | zold jednostki = 0 | `MountedWage.cs:79` (postfiks `GetCharacterWage` kazdego `PartyWageModel`, tylko w kontekscie `GetTotalWage` = zold partii) | warunek `CrownGifts.WatchUnpaidOn && CrownGifts.IsWatchParty(partia)`; `IsWatchParty` (`CrownGifts.cs:72-75`) = partia lorda, **zaloga** albo **druzyna gracza** frakcji `nightswatch`. Bohaterowie (towarzysze) dalej biora zold (`!__0.IsHero`). Cena werbunku i awansu - bez zmian (nominal). |
| 2 | minimum BK 1 zl | BK `BKPartyWageModel.cs:70` `(int)Math.Max(TroopWage * BaseWage, 1f)` | zold 0 z modelu gry BK podnosi do **1 zl na czlowieka na dobe**; w zalogach dziala jeszcze obnizka zalog BK - zmierzone ok. 0.5 zl (tab. 1.2) |
| 3 | rycerze 179 = 0 | `GentryService.cs:169, 328` | zold rycerzy w druzynach Strazy 0 |
| 4 | pulap w LUDZIACH zamiast w zlocie | `ClanBudget.cs:561-587` (`b.Zero`, klucz `UnpaidTroopsCapInMen`) | pulap rodu liczony jak u kazdego (`Ceiling`, `ClanBudget.cs:200`: 0.6 x D x f + skrzynia wojenna 0.8 x (G - R) / 45), potem `MenCap = (pulap - udzial dworu) / nominalny zold czlowieka` (`:584`; nominal = `MountedWage.Nominal`, zold z modelu bez zera). **Jedna pula calej Strazy** (suma rodow). Pula zatrzymuje werbunek (`CheckRecruiting`), nowe partie (`SpawnLordParty`) i przyrost zalog (`ClanBudget.cs:1317-1320`). |
| 5 | bez zwolnien | `ClanBudget.cs:897` (`WatchNoReleases`) | Straz nikogo nie zwalnia z braku budzetu; ubytek tylko bitwy i zgony |
| 6 | zalogi pelne | `ClanBudget.cs:686` (`GarFull`: `b.Zero`) | zalogi Strazy bez celu budzetu - pulap gry |
| 7 | poza doplatami korony | `ClanBudget.cs:285, 304, 320, 338, 360, 591` | rody Strazy (i gracz w Strazy) poza W5, W6, M1, W7, kredytem wojennym 168 i chlebem pana M3 |
| 8 | dary koron | `CrownGifts.cs:49-66, 160-200` (182, Jeff 10.10) | 8 koron Westeros (Polnoc 5%, Zelazny Tron 4%, Dorne 3%, Reach 3%, Dolina 2%, Krainy Burzy 2%, Dorzecze 2%, Smocza Skala 1% wplywow dnia) daje Strazy TYLKO, gdy oszczednosci jej rodow < 60 dob kosztow; koszt doby = zold partii i zalog (czyli minimum BK) + jedzenie 1 zl na brata |

- Klucze: `WatchUnpaid` (on), `UnpaidTroopsCapInMen` (on), `WatchNoReleases` (on), `GiftWatch*`; wszystkie wymagaja `ClanBudgetEnabled`. W `Armoury.json` Jeffa zadnego z nich nie ma - dzialaja domyslne [P grep].
- **Zalogi: tak, regula obejmuje zalogi** (pkt 1, 6) [K].
- **Gracz w Strazy: ta sama regula** - druzyna gracza ma zold 0 (pkt 1); gracz nie ma budzetu 166, wiec u niego pulapu w ludziach nie ma - trzyma go tylko limit druzyny gry; W5/W6 gracza w Strazy - nie (`ClanBudget.cs:360`) [K].
- ROT: wlasny model zoldu ROT jest nieczynny i zmienia tylko Unsullied i Innych (`MountedWage.cs:18-21`, sprawdzone przy paczce 160) [K wedlug komentarza]. ROT ma za to reguly wojny:
  "Night's Watch May Not War" (Straz wojuje tylko z Wolnymi Ludzmi), "Free Folk May Not War" (Wolni Ludzie tylko ze Straza), "Kings Protect Night's Watch"
  (`ROTSettingsMCM.cs:50-58`, `ROTKingdomDecisionPermissionModel.cs:58-66`, `ROT.HarmonyPatches.ModCompatibility/Diplomacy.cs:27`) - domyslnie wlaczone; wyjatek - krolestwo, ktorym rzadzi gracz [K].

### 1.2 Co to daje w biegu dwuletnim [P]

| Nocna Straz | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| rody z budzetem | 6 | 6 | 6 | 6 | 6 | 5 | 3 | 3 |
| ludzie w puli (druzyny + zalogi, tys.) | 4.3 | 5.7 | 5.9 | 6.1 | 6.1 | 5.6 | 3.0 | 2.5 |
| pulap w ludziach (tys.) | 5.7 | 5.6 | 5.8 | 6.0 | 5.6 | 4.1 | 2.3 | 2.1 |
| oszczednosci rodow (mln, linia 182) | 1.49 | 1.64 | 1.98 | 2.24 | 2.26 | 1.73 | 1.04 | 1.08 |
| koszt doby do testu daru (zold BK + jedzenie) | 8.3 | 10.7 | 11.0 | 11.2 | 11.2 | 10.5 | 5.6 | 4.7 |
| **dar koron Westeros** | **0** | **0** | **0** | **0** | **0** | **0** | **0** | **0** |
| D staly rodow | 18.1 | 18.7 | 20.3 | 19.8 | 20.4 | 19.5 | 12.8 | 9.1 |
| - renty 180 od wlasnej korony | 4.1 | 6.5 | 7.5 | 8.6 | 8.2 | 6.8 | 6.3 | 3.6 |
| - jednorazowe (lup, okupy, sakwy) | 4.1 | 4.4 | 4.8 | 3.3 | 3.1 | 5.3 | 1.5 | 2.5 |
| pulap zoldu (zloto, przed zamiana na ludzi) | 29.8 | 32.0 | 38.4 | 43.5 | 44.1 | 33.0 | 20.6 | 20.0 |
| zold druzyn / ludzie w druzynach (zl na czlowieka) | 1.07 | 0.99 | 1.00 | 0.98 | 0.98 | 1.01 | 1.02 | 1.04 |
| zold zalog / ludzie w zalogach (zl na czlowieka) | 0.51 | 0.52 | 0.52 | 0.52 | 0.52 | 0.52 | 0.52 | 0.52 |
| dezercja (ludzi na dobe, linia 4.15) | 0 | 0 | 0 | 0 | 0 | 2 | 4 | 4 |

Zrodla: linie "Budzet rodow (166)" (Straz w ludziach / pulap), "Dary koron (182)", "Straz i Wolni Ludzie (4.15)", `budzet-rodow.csv` (skrypty `x_log.py`, `q_csv.py`, rozdz. 8).
- Oszczednosci Strazy przez caly bieg byly 1-2 mln przy koszcie 60 dob 0.3-0.7 mln, wiec warunek daru nie zaszedl ani razu [P]. Straz zyje z rent wlasnej korony, ziemi Daru i lupu.
- Spadek w Q6-Q7 to nie bieda: w d512 wojna domowa w Strazy (okno "Civil War Breaks Out", CS `session-...` 23:02) - rody Snow, Tollett i Thorne z Nightfort, Shadow Tower i The Wall utworzyly
  "Confederation of Snow (Nights Watch Rebels)"; Strazy zostaly Castle Black i Eastwatch (`ludzie-regiony.csv`, d109347) [P]. Przyczyny buntu nie sprawdzalem.
- Pulap w ludziach liczony ze zlota: Straz ma duzy D (18-20 tys.) i skrzynie ponad rezerwe (G 2.2 mln przy R 0.57 mln -> 0.8 x 1.65 mln / 45 = ok. 29 tys. zl/d pulapu ze skrzyni) [K wzor, P liczby]. Skrzynia sie nie wyczerpuje, bo zold nie schodzi - pulap stoi [S].

---

## 2. Wolni Ludzie dzis

### 2.1 Rody, osady, ludzie [P]

| | stan |
|---|---|
| rody z ziemia (7) | Rayder - Frostfang's Camp (miasto, krol Mance Rayder), Styr Magnar - Thenn (miasto), Dogshead - Hardhome (miasto), Giantsbane - Fist of the First Men, Sixskins - Craster's Keep, Dalba - Hornfoot, Frozen Shore - Frozen Shore (zamki) |
| rody bez ziemi | 3 rody BK "gentry" (Wightsbane, Crowsbane, Frosttooth - bez druzyn, kiesy 24-36 tys.) + dwor BK; linia Skarbce: "rodow 11" |
| osady | 3 "miasta" (obozy) bez mieszczan (`kiesa-ludu.csv`: ludzie miasta 0 - caly lud mieszka we wsiach), 4 zamki, 18 wsi; twierdz 7 -> 7, zadnej nie stracili (`balans-krolestw.csv`) |
| ludnosc regionow | 299.6 tys. (d1) -> 286.1 (d365) -> 279.4 tys. (d728), -6.8% w 2 lata (`ludzie-regiony.csv`); tabela 300 tys. i "walcza wszyscy dorosli" (pula ok. 81 tys.) - `REGULY-KRAIN-I-DLUGU-2026-10-06.md` rozdz. 3 |
| jedzenie regionow | zapas 120-130 tys., bilans dnia +1.8 tys. (d1) -> +0.45 tys. (d728), glod 0 |
| wojny (KRONIKA WOJEN) | z Nocna Straza 728 z 728 dob (wojna fabularna ROT, wymuszana co dobe); ze Skagos 74 doby (wezwanie Strazy); **z Polnoca nigdy** |
| pieniadz ludu (d722) | kiesy ludu 64 tys. zl (0.2% swiata); kasy 3 miast 30 / 13 / 26 tys. (d728); skarbiec korony na podlodze 0.50 mln od Q3 |
| zarobek ludu (`kiesa-obieg.csv`, okna 7 dob) | rok 1: 8.46 tys. zl/d (w tym "z wojska" 3.31, robocizna 2.97, warsztaty 1.04); rok 2: 5.05 (z wojska 1.80, robocizna 1.83, warsztaty 0.77) |

Dla porownania [P]: Polnoc 3.22 mln ludzi i 2.10 mln zl oszczednosci ludu; ludzie Strazy 9.4 tys. z 0.72 mln, rebelianci 11.9 tys. z 0.61 mln (d722-728).

### 2.2 Pieniadze i wojsko, kwartalami [P]

`budzet-rodow.csv` (sumy rodow AI krolestwa) + linia W5 (z `ROZPOZNANIE-RODY.md` rozdz. 2) + linie "Skarbce" i "Straz i Wolni Ludzie (4.15)". tys. zl/d, chyba ze napisano inaczej.

| Wolni Ludzie | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| doby wojny | 89/89 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | | |
| potrzeba W5 (druzyny do 60% limitu) | 5.0 | 14.5 | 17.3 | 20.5 | 22.1 | 23.4 | 23.3 | 21.8 | 14.4 | 22.6 |
| danina P1 wlasnego ludu | 3.7 | 7.9 | 5.2 | 2.9 | 2.1 | 2.2 | 2.0 | 1.5 | 4.9 | 2.0 |
| **niepokryte (brak na zold) po W5/W5b/W6** | 0.0 | 2.4 | 11.7 | 17.6 | 20.0 | 21.2 | 21.2 | 20.3 | 7.9 | **20.7** |
| niepokryte po W7 | 0.0 | 1.1 | 5.9 | 13.0 | 16.6 | 18.0 | 18.2 | 17.8 | 5.0 | 17.7 |
| D staly rodow | 13.4 | 8.4 | 8.1 | 5.9 | 4.3 | 5.6 | 5.8 | 6.6 | 9.0 | 5.6 |
| - ziemia | 3.7 | 0.9 | 1.0 | 0.8 | 0.7 | 1.2 | 1.0 | 1.2 | 1.6 | 1.0 |
| - korona (renty 180 + zwrot 70%) | 6.1 | 4.0 | 3.6 | 2.6 | 1.9 | 2.4 | 2.4 | 2.9 | 4.1 | 2.4 |
| - jednorazowe (lup, okupy, sakwy) | 1.0 | 2.1 | 1.9 | 0.9 | 0.2 | 0.5 | 1.0 | 0.8 | 1.5 | 0.6 |
| pulap zoldu rodow | 28.1 | 22.8 | 17.6 | 10.9 | 7.3 | 8.2 | 7.7 | 6.9 | 19.8 | 7.5 |
| zold druzyn | 11.2 | 15.3 | 14.1 | 9.0 | 6.5 | 6.2 | 5.7 | 4.8 | 12.4 | 5.8 |
| zold zalog | 4.3 | 5.2 | 5.3 | 6.1 | 6.1 | 5.6 | 5.1 | 5.0 | 5.2 | 5.5 |
| zold przyciety (niezaplacony) | 0.0 | 0.6 | 1.9 | 2.8 | 3.6 | 3.2 | 2.5 | 2.0 | 1.3 | 2.8 |
| ludzie w druzynach / limit druzyn (tys.) | 2.6 / 5.8 | 3.0 / 5.8 | 2.5 / 5.5 | 1.5 / 5.6 | 1.0 / 5.6 | 0.9 / 5.4 | 0.8 / 5.2 | 0.7 / 5.2 | | |
| ludzie w zalogach (tys.) | 2.1 | 2.8 | 2.9 | 3.1 | 3.1 | 3.0 | 2.9 | 2.9 | | |
| kiesy rodow G (mln) | 1.31 | 0.57 | 0.37 | 0.26 | 0.23 | 0.21 | 0.20 | 0.19 | | |
| rody biedne w wojnie (G < R) z 11 | 1 | 4 | 8 | 9 | 10 | 10 | 10 | 10 | | |
| dezercja (ludzi na dobe) | 0 | 12 | 4 | 2 | 4 | 8 | 7 | 5 | | |
| zabici (ludzi na dobe) | 13 | 18 | 25 | 22 | 15 | 15 | 11 | 14 | | |

- Zold na czlowieka w Q8: druzyny 6.8 zl, zalogi 1.7 zl [P]. Zalogi zjadaja pulap: 5.0 z 6.9 tys. (72%) w Q8, 84% w Q5 - i sa placone pierwsze (`ClanBudget.cs` naglowek: "w wojnie pulap gry bez zmian i finansowane pierwsze") [P, K].
- Na koniec biegu (d728): druzyny 777 ludzi (linia Skarbce; w CSV 754) przy limitach 5 303 (wypelnienie 15%) - Rayder 360, Styr 124, Dogshead 98, Giantsbane 51, Frozen Shore 47, Dalba 42, Sixskins 32; zalogi 3 001 [P].
- Od startu sesji (2 lata): do Wolnych Ludzi przybylo 34 276 ludzi, zginelo 12 005, zdezerterowalo 3 967 (linia 4.15; liczy wszystkie partie frakcji - takze milicje, tabory i karawany) [P].
- Bieg 120 dob (05-22-14) idzie ta sama droga: kiesy 1.21 -> 0.63 mln w d91-120, w d120 W5 jeszcze pokrywa potrzebe (niepokryte 1.3 tys.) [P].

### 2.3 Bitwy - kto kogo bije [P]

`bitwy.log` (linie "Bitwa: dzien N - typ: A (frakcja) vs B (frakcja); wygrywa ..."), skrypty `bitwy.py`, `bitwy2.py`.

| Wolni Ludzie | rok 1 | rok 2 |
|---|---:|---:|
| bitew | 773 | 585 |
| - jako obronca | 714 | 535 |
| - jako atakujacy (bitwy w polu) | 55 | 50 |
| - rabunki wsi ("Raid") | 4 (Q1: Hogton 3, Queenscrown 1 - wsie Strazy w Darze) | 0 |
| wygrane / przegrane | 72 / 700 | 64 / 521 |
| zabici swoi / wrogow | 7 082 / 2 729 | 4 867 / 1 737 |
| Straz bije: karawany WL / tabory wsi WL / druzyny i patrole WL | 191 / 184 / 3 | 111 / 100 / 1 |
| Inni bija: karawany / druzyny / tabory i patrole | 124 / 119 / 32 | 105 / 70 / 11 |
| WL atakuja: Broken Men / Wildling Renegades / inne | 29 / 26 / 0 | 43 / 2 / 5 (Sea Raiders 4, Inni 1) |
| WL atakuja druzyny Strazy w polu (poza 4 rabunkami wsi) | 0 | 0 |

- Nocna Straz w tych samych latach: 407 / 270 bitew, wygrane 393 / 246, prawie wszystkie jako atakujacy na Wolnych Ludzi (591 w 2 lata) [P].
- Rabunki okregow wsi (`wioski-okregi.csv`, osobne dni "ostatni_najazd"): w regionach Strazy i Wolnych Ludzi 3 - wszystkie w okregu Eastwatch, w roku 1; w roku 2 zero. Swiat: 7 706 [P].
- Straz nie rabuje wsi Wolnych Ludzi od H1 (`WatchLaw.cs:26-27`; linia "Straz z Westeros": odrzucone opcje rabunku 108 dziennie) [K, P] - ale dalej rozbija ich tabory wsi i karawany w polu.

---

## 3. Skad Wolni Ludzie mogliby zyc z lupow

| zrodlo | regula dzis | ile dalo w 2 latach / skala | ograniczenie |
|---|---|---|---|
| **najazd na Polnoc** | ROT "Free Folk May Not War": Wolni Ludzie nie wojuja z nikim poza Straza (`ROTKingdomDecisionPermissionModel.cs:63-66`, Diplomacy `:27`); bez wojny AI nie rabuje [K] | 0 dob wojny z Polnoca [P] | to regula wojen ROT (ustawienie MCM ROT); fabuly ROT nie ruszamy |
| **najazd na Dar (wsie Strazy)** | rabunek wsi E7/R1: lup = s x kiesa wsi, s = trafieni w sesji / ludzie w domu na starcie; towar s x magazyn wsi; jedzenie ze spichlerza; zlota "za hearth" z niczego juz nie ma (`DistrictRaid.cs:38-45, 469-493`, `SmallGold.cs:20-29`) [K] | swiat: 2 078 sesji rabunku, **61 tys. zl** zlota z kies wsi (ok. 29 zl na sesje), towar 3 tys. zl, 15 tys. szt. jedzenia [P linia R1]; WL: 4 starcia w Q1, potem 0 [P] | Dar = 11 wsi Strazy, od d512 tylko 5 (6 przeszlo do rebeliantow, z ktorymi WL nie sa w wojnie) [P]; ludzie Daru ok. 21 tys. (Straz 9.4 + rebelianci 11.9), ich kiesy 1.3 mln - ale rabunek bierze z kiesy WSI, nie z kiesy ludu [K] |
| **lup z bitwy - zloto wodza** | A6: pokonany wodz traci 5% swojego zlota, najwyzej 10 000 na partie; dzielone miedzy zwyciezcow (`BattlePlunder.cs:7-10`) [K] | WL wygrywaja 64-72 bitwy rocznie, prawie tylko z bandami [P] | wodzowie Strazy bogaci (Mormont 0.88 mln -> 10 000 na wygrana), ale WL ani razu nie zaatakowali druzyny Strazy [P] |
| **kiesa rozbitej partii bez wodza** | 3.2: cala kiesa karawany, taboru wsi, bandy do zwyciezcy (`BeatenPurse.cs`) [K] | kiesy band swiata 264 tys. zl w 1 097 bandach (ok. 240 zl na bande, linia "Paser" d728) [P] | Straz nie ma karawan (zold karawan Strazy ok. 0) [P]; dzis to WL traca w ten sposob karawany i tabory na rzecz Strazy (tab. 2.3) |
| **lup z cial** | z sakiewek ludzi przegranych: 1/3 wodzowi, 2/3 sakiewkom zwyciezcow (`CasualtyLoot.cs:13-24`) [K] | - | bracia Strazy biora ok. 1 zl dziennie - ich sakiewki sa male, wiec lup z nich maly [S] |
| **jency** | kraina Wolnych Ludzi bez niewoli - szeregowi jency do domu (`PrisonerLaw.cs:35-40`); okupy bohaterow (178) [K] | nie mierzylem okupow WL osobno | - |
| **przedmioty (stal)** | AI sprzedaje lup w miastach (gra `PartiesSellLootCampaignBehavior`), DTE bierze sprzet do zbrojowni; WL nie kuja stali (`CraftRealm.cs:312`) [K] | - | to towar, nie zloto; zloto za niego placi kasa miasta, w ktorym sprzedaja [K] |
| **lup juz liczy sie do budzetu** | `ClanBudget.cs:55, 434`: "najezdzcy" (Zelazne Wyspy, Dothrakowie, Wolni Ludzie) maja jednorazowe wplywy (lup, okupy, sakwy) w D - podnosza pulap (wyjatek lore W-1) [K] | jednorazowe WL 1.46 / 0.62 tys. zl/d (rok 1 / rok 2) [P] | regula juz dziala - za malo lupu, zeby cos dawala |
| **bandy "Wildling Renegades"** | klan bandycki `wildlings` (is_bandit) - napada kazdego, lup sprzedaje paserowi (`OutlawLaw`) [K] | w regionach WL bandy 30 -> 1 016 ludzi (d1 -> d728) [P] | to nie sa rody Wolnych Ludzi; WL z nimi walcza |

Wniosek [S]: "zyc z lupow" w dzisiejszych regulach sie nie da - (a) jedyny wrog w zasiegu to Straz z 5-11 malymi wsiami i bez karawan, (b) rabunek wsi daje ulamek kiesy wsi
(caly swiat 61 tys. zl w 2 lata), (c) AI Wolnych Ludzi nie atakuje druzyn Strazy ani nie rabuje Daru po Q1 (dlaczego - nie wiem, rozdz. 7 pkt 1), (d) wielkie pieniadze
blisko Muru ma Polnoc (lud 2.1 mln zl), a z nia ROT zabrania wojny.

---

## 4. Wojsko Wolnych Ludzi bez zoldu - co je trzyma

### 4.1 Hamulce dzis i bez zoldu

| hamulec | Wolni Ludzie dzis | Nocna Straz (bez zoldu) | gdyby WL nie brali zoldu [S] |
|---|---|---|---|
| pulap zoldu 166 (zloto) | 6.9-28 tys. zl/d; G < R u 10 z 11 rodow -> mnoznik biedy 0.8-0.5; zold przycinany 2-3.6 tys. zl/d [P] | zamieniony na pulap w ludziach = pulap / nominal [K] | z regula Strazy 1:1 pulap w ludziach liczony z tego samego malego D: na starcie ok. **0.8 tys. ludzi** przy 3.8 tys. dzis (rachunek nizej) - werbunek staje, zalogi zostaja (GarFull, bez zwolnien), druzyny nie dostaja nowych ludzi; po ok. 1.5 roku oszczedzania ok. 2-2.5 tys. |
| limit druzyn gry | 5.2-5.8 tys. (wypelnienie 13-52%) [P] | 4.9 tys. (2.2-2.4 po buncie) [P] | gorna granica druzyn bez wzgledu na zloto [K] |
| zalogi | pulap gry, placone pierwsze; 2.1 -> 3.0 tys. [P] | `GarFull`, bez celu [K] | rosna do pulapu gry, jak dzis [S] |
| zwolnienia 166 | 15% nadwyzki dziennie po 3 dobach ponad 1.1 x pulap (`Releases`) [K] | `WatchNoReleases` - nikt [K] | do decyzji (zakon na Murze vs lud w obozach) |
| zalegly zold | `WarLedger.cs:14-22`: po 2 dobach laski 0.5% ludzi dziennie za kazda dobe zwloki, elity pierwsze, podloga 30; kara morale gry: -20 x udzial niezaplaconego zoldu stale i -3 x udzial dziennie (`DefaultPartyMoraleModel.cs:33-45, 240`, `DefaultClanFinanceModel.ApplyMoraleEffect`) [K]; dezercja WL 2-12 ludzi/d [P] | brak zaleglosci [K] | znika [K] |
| dezercja z morale | progi wedlug tieru 25..10, 1%/pkt (`DesertionLaw.cs`, takze AI - 183) [K] | jak WL | bez zmian; morale dalej spada z glodu i porazek [K] |
| jedzenie | AI kupuje jedzenie tylko za kiese partii (`PartiesBuyFoodCampaignBehavior.cs:67`: cena <= PartyTradeGold; u druzyny lorda to zloto wodza) [K]; kiesy glow 11 rodow WL razem ok. 0.1 mln w roku 2, zloto rodzin z ziemia 0.3-31 tys. na rod (d728) [P] | Straz bogata [P] | bez zoldu kiesy glow przestaja schodzic na zold - jedzenie dalej z kiesy wodza; regiony WL maja zapas 128 tys. i dodatni bilans [P] |
| ludzie | `FreeHands`: WL 100%, bez hamulca naboru (`FreeHands.cs:124`) [K]; pula walczacych ok. 81 tys. przy 300 tys. [P wedlug REGULY-KRAIN 3.2]; ludnosc -6.8% w 2 lata [P] | rekrut z calego Westeros (`WatchLaw`) [K] | ludzie nie hamuja wojska do kilkunastu tysiecy [S] |
| straty | zabici 11-25 dziennie (12 005 w 2 lata, glownie Straz i Inni) [P] | 0-3 dziennie [P] | bez zmian [S] |
| minimum BK | - | 1 zl/czlowieka (zaloga ok. 0.5) [K, P] | "bez zoldu" = ok. 1 zl dziennie za czlowieka, chyba ze BK dostanie wyjatek [K] |

**Rachunek "regula Strazy 1:1 dla WL" [S, metoda]:** wzor `Ceiling` bez doplat korony (b.Zero wyklucza W5/W7), D, G, R siedmiu rodow z ziemia z d728 (`budzet-rodow.csv`):
Rayder 0.6 x 2 363 x 0.5 (G < 0.25 R) = 709, Styr Magnar 504, Dogshead 438, Dalba 260, Frozen Shore 245, Giantsbane 241, Sixskins 149 - razem ok. 2.5 tys. zl/d.
Nominal czlowieka WL (zold z modelu bez BK x1.25 i bez obnizki zalog) ok. 3.3 zl (druzyny 6.8 / 1.25 = 5.4, zalogi ok. 2.8, wazone ludzmi) -> pulap ok. 0.8 tys. ludzi.
Potem pulap powoli rosnie: D 6.1 tys. minus dwor, sprzet i budowy (0.2 + 0.17 + 0.10 = 0.47 D, ok. 2.9 tys.) minus minimum BK ok. 2.3 tys. zl/d (0.75 tys. ludzi w druzynach
po 1 zl, 3 tys. w zalogach po 0.5) - zostaje ok. 0.9 tys. zl/d; rezerwa R siedmiu rodow 327 tys. przy G 109 tys. -> skrzynia (G > R) dopiero po ok. 8 miesiacach, po kolejnym roku
ok. 0.8 x 330 tys. / 45 = 5.9 tys. zl/d pulapu wiecej, czyli ok. 2-2.5 tys. ludzi razem. Tak wlasnie urosla Straz: duzy D (18-20 tys.) i 1-2 mln oszczednosci, ktore nie schodza.

### 4.2 Czy armia nie urosnie bez granic [S]

- Z pulapem w ludziach liczonym ze zlota (regula Strazy) - nie urosnie; najpierw zmaleje, potem rosnie tak wolno, jak rosna kiesy rodow (pkt 4.1).
- Bez zadnego pulapu w ludziach trzymaja ja tylko: limit druzyn gry (ok. 5.3 tys. w 7 rodach), pulap zalog gry, jedzenie (zakup za zloto wodza), straty w bitwach (11-25 ludzi dziennie) i
  dezercja z morale. Pula ludzi (ok. 80 tys. walczacych, 280 tys. ludzi) tego nie zatrzyma. Druzyny moglyby wiec dojsc do ok. 5 tys., zalogi do pulapu gry - razem rzad 8-9 tys.
  (dzis 3.8 tys.) [S z limitow P].
- Nowe partie rodow: dzis wstrzymane, gdy w pulapie nie ma miejsca na 30 ludzi (`MinNewPartyMen`, 166) - bez pulapu w zlocie ten hamulec tez znika albo musi liczyc ludzi [K].

### 4.3 Obieg pieniadza (zasada 0) [K + P + S]

- Dzis zold WL nie znika: druzyna -> sakiewki ludzi (wydaja w miastach i wsiach), zaloga -> kasa jej zamku albo miasta (`SoldierPay.cs` naglowek) [K]. Lud WL zarabia
  "z wojska" 1.80 z 5.05 tys. zl/d w roku 2 (3.31 z 8.46 w roku 1) [P `kiesa-obieg.csv`] - to wydatki wojska w ich osadach (sakiewki, zakupy). Ile z tego to sam zold - nie wiem
  (u Strazy, ktora zoldu prawie nie placi, lud tez ma "z wojska" 1.26-1.67 tys.) [P].
- Bez zoldu: rody WL placa zamiast ok. 10.5 tys. zl/d (5.8 druzyny + 5.5 zalogi, rok 2) samo minimum BK ok. 2.3 tys., lud i kasy osad WL traca czesc wplywow "z wojska", mniej daniny P1 (dzis 2 tys.)
  i mniej rent dla tych samych rodow - petla jest mala i zamknieta w krainie [S]. Zloto nie powstaje ani nie znika - zmienia sie tylko, kto je trzyma.
- Lup jako dochod ma platnika tylko wtedy, gdy ktos traci: dzis Straz zyskuje na WL (karawany, tabory); odwrocenie strumienia = WL zyskuja na Strazy albo na Darze [S].

---

## 5. Lore

Parafrazy z ksiag G.R.R. Martina (Piesn Lodu i Ognia), bez cytatow.

| fakt lore | gdzie | co znaczy dla gry [S] |
|---|---|---|
| Wolni Ludzie nie bija monety i nie handluja na pieniadz; bogactwo to naramienniki ze zlota i brazu, futra, bursztyn, bron. Gdy Jon Snow wpuszcza ich za Mur, kaze kazdemu oddac kosztownosci jako oplate - Tormund oddaje swoje naramienniki. | Taniec ze smokami (rozdz. Jona) | ich "zloto" w grze to umowna wartosc dobr, nie zold; zold na monete jest sprzeczny z lore |
| Nikt im nie placi za walke. Mance Rayder, dawny zwiadowca Strazy, zjednoczyl klany (Thennow, olbrzymow, Hornfoot, klany rzek lodowych) dla przetrwania - uciekali przed Innymi na poludnie, za Mur. Krol za Murem jest wybierany, sluchaja go z wyboru. | Nawalnica mieczy | "walczyli za sprawe" (Jeff 14.10) = lore; wojsko z ludu, nie z kiesy |
| Napady zza Muru: male grupy (dziesiatki ludzi) wspinaja sie na Mur albo przechodza przelecza / lodziami przez Zatoke Fok (Bay of Seals); biora stal, zywnosc, kobiety; dowodcy napadow to m.in. Alfyn Crowkiller, Rattleshirt, the Weeper, Harma Dogshead. | Gra o tron, Starcie krolow, Nawalnica mieczy | lup jako dochod to lore; skala napadu - kilkadziesiat ludzi, nie armia |
| Dar (Dar Brandona i Nowy Dar) - ziemie Strazy na poludnie od Muru - w duzej czesci opustoszaly, bo ludzie uciekali przed najazdami; Thennowie Styra przechodza przez Queenscrown i napadaja Kretowe Miasto (Mole's Town). | Gra o tron, Nawalnica mieczy | cele najazdu w ROT (Queenscrown, Molestown, Hogton) to lore; Dar jest maly i biedny |
| Stal maja z lupu i wymiany (Craster handluje ze Straza); sami znaja tylko braz - Thennowie. | Starcie krolow, Nawalnica mieczy | zgodne z `CraftRealm` (WL nie kuja stali, wyjatek braz Thennow) [K] |
| Obyczaj: co zdobedziesz, jest twoje; wolny czlowiek nie kleka; kradziez zony to sposob na malzenstwo. | Nawalnica mieczy (Ygritte) | rabunek nie jest dla nich przestepstwem - lup jest "normalnym" dochodem |
| Nocna Straz: bracia przysiegaja bez zon, ziemi i potomstwa, nie dostaja zoldu; zakon zyje z Daru, datkow lordow i krolow, rekrut to czesto skazancy. | Gra o tron | regula 182 (bez zoldu, dary koron) i E4/9 (skazancy na Mur) sa zgodne z lore [K] |
| Hardhome - dawne miasto Wolnych Ludzi, zniszczone ok. 600 lat temu, w ksiegach schronienie (Matka Kretow); Craster's Keep - dwor Crastera, nie wodza Wolnych Ludzi. | Taniec ze smokami, Starcie krolow | w ROT to "miasto" i "zamek" WL - ROT upraszcza; fabuly ROT nie ruszamy |

---

## 6. Co z tego wynika dla projektu (kierunki, nic nie zrobione) [S]

Warianty Jeffa (14.10) wobec tego, co jest w kodzie:
1. **"Nie pobieraja kasy"** - regula 182 jest gotowym torem (zold 0 w druzynach, zalogach i druzynie gracza; poza W5/W6/M1/W7), ale jej pulap w ludziach liczy sie ze zlota - u WL wyszloby
   na starcie ok. 0.8 tys. ludzi, po ok. 1.5 roku 2-2.5 tys. Potrzebny inny pulap w ludziach (np. z ludzi i jedzenia krainy albo z lupu) - to nowa regula. `IsWatch` sprawdza na sztywno `nightswatch` (`CrownGifts.cs:36`).
2. **"Pobieraja znacznie mniej"** - ten sam postfiks zoldu z mnoznikiem zamiast zera: pulap w zlocie bez zmian, wiecej ludzi za te same pieniadze (przy mnozniku 1/3 pulap roku 2
   7.5 tys. zl/d oplacilby ok. 3 razy wiecej ludzi; potrzeba W5 tez spada do ok. 1/3, 22.6 -> ok. 7.5 tys.). Minimum BK 1 zl dalej dziala (w zalodze ok. 0.5 zl - jak u Strazy); zalogi WL
   (dzis 1.7 zl na czlowieka) zeszlyby do ok. 0.6 zl.
3. **"Nie pobieraja, ale zyja z lupow"** - lup juz wchodzi do D (W-1), ale dzis lupu prawie nie ma (0.6 tys. zl/d), bo jedyny wrog to Straz, a rabunek wsi daje ulamek kiesy wsi.
   Bez ruszania wojen ROT zrodlem moze byc tylko Straz i Dar (11 / 5 wsi, ok. 21 tys. ludzi) albo regula poza wojna.
- W kazdym wariancie: jedna regula dla gracza (dzis `IsWatchParty` obejmuje druzyne gracza - tak samo trzeba objac gracza-wasala WL), zalegly zold i kara morale znikaja same,
  obieg krainy maleje o wydatki wojska (pkt 4.3), a gorne granice armii to limity gry, jedzenie i straty (pkt 4.2).

---

## 7. Czego nie wiem (+ linia diagnozy)

1. Dlaczego AI Wolnych Ludzi po Q1 nie rabuje Daru i nigdy nie atakuje druzyn Strazy (male druzyny? wynik opcji AI? droga przez Mur?). Diagnoza: licznik jak H1 (`WatchLaw` - postfiks na
   `AiHourlyTick`) dla partii WL: ile opcji rabunku i ataku na Straz dostaly, najlepszy wynik, ile wybrano, sila druzyny wobec celu.
2. Ile z jednorazowych wplywow Strazy (3-5 tys. zl/d) pochodzi z kies karawan i taborow WL. Diagnoza: linie A6 i 3.2 z podzialem "krolestwo zwyciezcy x krolestwo pokonanego".
3. Czy druzyny WL dzis gloduja (jedzenie za kiese wodza). Diagnoza: linia "Dezercja AI (183)" - "w glodzie" na krolestwo, albo kolumna glodu druzyn w `budzet-rodow.csv`.
4. Dokladny nominalny zold czlowieka WL (rachunek 4.1 szacuje 3.3 zl z zoldu BK). Diagnoza: pomiar "Budzet rodow (na sucho)" z pulapem w ludziach dla WL (tylko log).
5. Kiesy wsi Daru (lup R1 bierze s x kiesa wsi). Diagnoza: "Przeplywy osad (kiesy wsi)" ma tylko swiat - dopisac krolestwo albo region.
6. Czy WL maja statki i moga napadac morzem (NavalDLC, Zatoka Fok) - nie sprawdzalem.
7. Przyczyna wojny domowej Strazy w d512 (okno BK/Diplomacy "Civil War Breaks Out") - nie sprawdzalem; skutek: Dar Strazy 11 -> 5 wsi, pula Strazy 6 -> 2.5 tys.
8. Ile z "z wojska" w zarobku ludu WL to sam zold (sakiewki) - kolumna laczy sakiewki i zakupy wojska.

---

## 8. Pliki i polecenia

Skrypty (dysk C - tymczasowe): `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\wolni\`
- `q_csv.py <dwc.pkl> "<krolestwa>" "<kolumny>"` - kwartaly z `budzet-rodow.csv` (pickle z `..\rody2\x2_csv.py`).
- `x_log.py <log>` - linie "Dary koron (182)", "Budzet rodow (166)" (Straz w ludziach), "Straz i Wolni Ludzie (4.15)", "Skarbce" WL i Strazy -> kwartaly.
- `bitwy.py <bitwy.log> "<frakcje>"`, `bitwy2.py <bitwy.log> "Free Folk"` - bitwy wedlug kwartalu, roli, typu, przeciwnika i rodzaju partii.
- `rab.py <log>` - sumy linii "Rabunek osada po osadzie (E7/R1)" i "Drobne zrodla (E5/A7)".

```
L=D:\Backup-Bannerlord\logs\2026-10-14-noc
python -I q_csv.py ..\rody2\dwc.pkl "Free Folk,Nights Watch,Iron Islands" "G,D,R,pulap,zold_partii,zold_zalog,ludzi_partie,ludzi_zalogi,limit_partii,d_ziemia,d_korona,sr_jednorazowe,zold_przyciety,renta_180"
python -I x_log.py L\Armoury-2026-10-13_21-14-13.log
python -I bitwy.py L\Logs\2026-10-13_21-14-13\bitwy.log "Free Folk,Nights Watch,Iron Islands"
python -I bitwy2.py L\Logs\2026-10-13_21-14-13\bitwy.log "Free Folk"
python -I rab.py L\Armoury-2026-10-13_21-14-13.log
```
Pozostale liczby - `awk` na `ludzie-regiony.csv`, `kiesa-ludu.csv`, `kiesa-obieg.csv`, `kasy-miast.csv`, `ludnosc-bk.csv`, `wioski-okregi.csv` (kolumny po naglowku) i `grep` linii
"KRONIKA WOJEN" w sesji CS.
