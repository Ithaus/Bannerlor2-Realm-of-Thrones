# PROJEKT ETAP 3.1 + 3.3 - szczelnosc pieniadza (164a) i statki (181) - 10.10.2026, wersja 2 (po krytyce)

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane. Bez 3.2 (zloto rozbitych - robione osobno, galaz
`w-toku/e3-32`, `BeatenPurse.cs`) i bez etapu 5 (pasmo notabli 164c, kapital karawan notabli, zakupy mieszkancow, kiesa ludu 173, regulator kas K6).
Wersja 1 (przed krytyka): `projekt31\PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10.v1.md`. Krytyka: `projekt31\KRYTYKA.md`; co przyjeto i odrzucono - rozdz. 8.

Podstawa:
- rozpoznania `projekt31\A-majatki-bk.md` (A), `B-drobne.md` (B), `C-statki.md` (C) - ich dowody nie sa tu powtarzane w calosci;
- kod Armoury `noc2\e2c` (galaz w-toku/e2c = gra Jeffa po WGRANIU 21 + poprawki nocy 10/11.10): skrot **A:** = `e2c\Armoury\src`; galaz 3.2: **3.2:** = `scratchpad\e3\32\Armoury\src`;
- dekompilacje gry 1.4.8, NavalDLC, BK/BKROTPatch, DTE, ROT (`scratchpad\e3\dec\` (w tym `dec\tcs` - gra, `dec\bkproj` - BK), `dek\`, `32dec\`, `38dec\`):
  skroty **gra:**, **Naval:**, **BK:**, **DTE:**, **ROT:**;
- log dlugiego biegu (457 dob, gra jak u Jeffa): `kopia-okup1-457\Armoury-2026-10-10_10-38-24.log` = **log:N** (numer linii); CSV tego biegu
  (`kopia-okup1-457\2026-10-10_10-38-24\budzet-rodow.csv`, `kasy-miast.csv`); doba N = N-ta linia "Pieniadz swiata (bilans - przyczyny)" (dzien gry = 108836 + N);
- rachunki: `projekt31\py\sr.py` (srednie), `projekt31\py\pula_wariant.py` -> `py\pula_wariant.out` (NOWE: wyplaty portow wedlug regul, kontrfakt wojska,
  glowy < 5 000, lup z cial) - na `scratchpad\e3\skrypty\statki_warianty.py` / `statki_baza.py` (przypisanie zlota statkow do rodow, C 4);
- dokumenty: PLAN-DO-KONCA-MODA (etap 3), PROJEKT-EKONOMIA-OBIEG (OB; 4.1, 9, 12), PROJEKT-ETAP2-BANKRUCTWA (E2; rozdz. Z i caly etap 2 - w grze; progi
  rozdz. 1: glowy < 5 000 <= 10 w dobach 120, 364, 728; wojsko w wojnie 95-115 tys., podloga 85), audyt 15, STAN-PRAC (decyzje Jeffa - wiazace).

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek/rachunek, [D] decyzja Jeffa (data, godzina). Kwoty: zl na dobe (srednie);
"d31-120" itp. = okna dob biegu 457. Rok gry = 364 doby.

---

## 0. Dla Jeffa - prostym jezykiem

1. Etap 3 zamyka zloto, ktore gra robi z powietrza, i pieniadze, ktore znikaja w nicosc: kazda moneta ma kogos, kto placi, i kogos, kto dostaje.
2. **Kolejnosc (zmiana po krytyce):** najpierw sam pomiar (40 dni testu, gra bez zmian) - ma nazwac pieniadze, ktore dzis znikaja bez nazwy (to jest cel
   etapu: "reszta niewyjasniona" ponizej 10 tys. zl dziennie). Potem paczka uszczelnien (test 120 dni). Statki osobno, po Twojej decyzji.
3. **Majatki Banner Kings** przestaja wyplacac co dzien "dochod z produkcji" z niczego (ok. 293 tys. zl dziennie, prawie wszystko notablom wiejskim).
   Majatek zarabia swoj udzial z plonu, ktory wozy sprzedaja w miescie. W oknie majatku zobaczysz srednia z ostatnich 4 tygodni (nizszy dochod, nizsza cena kupna).
4. Jencow sprzedanych w miescie (takze Twoich u posrednika), lup z oblezenia i nagrode turnieju placi kasa tego miasta z pieniedzy ponad zapas kupcow -
   biedne miasto zaplaci mniej (dostaniesz komunikat, ile).
5. Zold ludzi karawan i oplata za awans zolnierzy (takze Twoja z ekranu druzyny) trafiaja do sakiewek tych ludzi, a oni wydaja je w miastach (dzis znikaja).
6. Zloto z cial poleglych: zwyciezca AI bierze je z sakiewek pokonanych zolnierzy (lord 1/3, jego ludzie 2/3); pusta sakiewka (np. banda) - nic.
   Znikna skoki po kilka milionow zl w jednej dobie. Wojsko lordow prawie bez zmian (ok. -0,5%).
7. Konwoje zaopatrzenia (mod DTE) nie dostaja kasy z powietrza; karawana kupiona przez lorda albo przez Ciebie dostaje kapital z zaplaty;
   druzyna rozwiazana albo usunieta bez bitwy oddaje kiese swojemu rodowi.
8. Podatek od warsztatow notabli naprawde placi notabl panu miasta (dzis notablowi znika, a panu gra robi go z powietrza) - dochod panow prawie bez zmian.
   Oplaty BK maja odbiorce (tytuly i dylematy - skarbiec, dwor - kasa siedziby, pasowanie rycerza - nowy rod). Zloto zmarlego bez spadkobiercy zostaje w osadzie.
9. Zold Innych (nieumarlych) - jeszcze nie: najpierw pomiar, skad maja pieniadze (inaczej zloto zbieraloby sie w ich kiesach).
10. **Statki (osobno):** gdy druzyna zostaje rozbita na ladzie albo rozwiazana, jej statki nie zamieniaja sie juz w worek zlota z powietrza (dzis ok. 300 tys. zl
    dziennie). Kupuje je najblizszy port wlasnego krolestwa, a gdy ten nie ma pieniedzy - port krolestwa, z ktorym rod nie walczy; placi cene gry
    (30% wartosci minus naprawa) z pieniedzy ponad zapas kupcow - tak samo Tobie i AI. Handel statkami w porcie i pryzy z bitew morskich - przez kase portu.
11. **Wojsko (statki):** pierwsza liczba (-7%) byla za optymistyczna - zakladala, ze porty zawsze maja pieniadze. Poprawiony rachunek: po roku wojsko lordow
    w wojnie ok. 89-91 tys. zamiast ok. 99 tys. (ok. -9..-11%), a biednych glow rodow ok. 13-19 zamiast 1 (gorna granica). To lamie dwa progi etapu 2.
    Wyrownac to moze podatek wojenny korony z etapu 5. **To jedyne pytanie do Ciebie** (rozdz. 4); rada: statki wlaczyc razem z podatkiem wojennym.
12. Zloto swiata: bez statkow rosnie dalej jak dzis (ok. +450 tys. dziennie); ze statkami ok. +100..+250 tys.; "stoi" dopiero po etapie 5.
13. Kazda czesc ma wlasny wylacznik w menu, wiec zle dzialajaca czesc mozna wylaczyc bez reszty.

---

## 1. Co dzis (liczby z logu)

| Pozycja | d31-120 | d336-457 | ost. 28 (430-457) | Dowod |
|---|---:|---:|---:|---|
| **Reszta niewyjasniona R** (srednia dobowa) | -17.3 tys. | -15.5 tys. | -15.1 tys. | "Pieniadz swiata (bilans - przyczyny)" |
| R - srednia 28 dob (prog etapu < 10 tys.) | **-18.9 tys.** (d120) | | **-15.1 tys.** (d457) | log:32587, log:123820 |
| w tym RB3 "od budow do korony" (portfele wezwan 168) | -5.2 | -3.6 | -1.2 | B 4.1; A: `CrownCallToWar.cs:240-252` |
| w tym "poza naszym tickiem" (trop: konwoje DTE) | -11.9 | -11.8 | -13.8 | B 4.2 |
| statki NavalDLC inne (z niczego; czesc moze byc podwojna - rozdz. 3 E3/0) | 314.5 | 355.1 | **445.6** | C 2 (mediana 457 dob 190.8); Naval: `NavalShipDistribution.cs:24-48` |
| statki: sprzedaz osadom + premie (z niczego) / kupno od osad (w nicosc) | 23.9 / 10.8 | 24.6 / 8.4 | 29.7 / 8.9 | j.w. |
| majatki BK - dochod z produkcji (z niczego) | 293.0 | 293.8 | 294.2 | A 2; log:123829 (do notabli 293 207) |
| lup z cial dla AI (Z1, z niczego) | 22.8 | 198.5 (med. 20.8) | 31.6 | B 2.6 (skoki d290, 325, 419-421; d420: 371 043 wywolan, log:113728) |
| jency (partie + twierdze + rabunek BK) | 14.0 | 10.7 | 10.1 | bilans "F:" |
| lup z oblezen + nagrody turniejow / myto | 0.3 / 1.5 | 0.4 / 0.6 | 0.0 / 1.0 | bilans "F:", Z5 |
| "bitwy" (z niczego; czesc to statki zatopione - niezmierzona) | 50.0 | 75.6 | 102.4 | B 2.8, C 1.1 |
| kapital nowych karawan lordow (z niczego) / cena kupna BK (w nicosc) | 14.8 / 1.3 | 18.0 / 2.9 | 22.5 / 6.4 | bilans Z2, U6 |
| zold karawan notabli (w nicosc) / zold karawan lordow (nieprzekazany) | 195.5 / 14.6 | 143.8 / 6.2 | 144.9 / ok. 8 | log:123829, log:123779 |
| awanse (w nicosc) | 10.4 | 12.3 | 12.1 | bilans "T:awanse" |
| werbunek ochotnikow: pobrane - zwrot (w nicosc) | 6.0 | 4.9 | | B 2.5 |
| podatek warsztatow BK od notabli (w nicosc u notabla; panowie miast dostaja te sama linie z niczego przez model - rozdz. 3 E3/8a) | 54.4 | 53.5 | 55.8 | B 2.8; log:123829; BK: `BKTaxModel.cs:198-205`; A: `PopulationLaw.cs:226-231` |
| zold nieumarlych (w nicosc; zrodlo ich dochodu nieznane) | 10.5 | 8.7 | (d457: 4.8; sr. 458 dob 7.7) | log:123779 |
| oplaty lordow BK bez kupna karawan (tytuly, dwor, dylematy) | 2.4 | 6.2 | 6.9 | bilans U6 |
| zgony notabli - zloto przepada | 5.6 | 2.5 | 3.9 | bilans U7 |
| **zmiana pieniadza swiata** | +446.8 | +536.6 | +338.5 | "Pieniadz swiata" |
| wojsko w druzynach lordow w wojnie | 93.1 tys. (d93-120: 96.4) | 104.5 tys. (d365-457) | **99.4 tys.** | C 4 (linia "Skarbce", suma rodow w WOJNIE) |
| glowy rodow < 5 000 / bankruci | | | 3 / 0 | log:123784, "Dlugi" log:123787 |
| rycerze (gentry): rody / glowy < 5 000 / kiesy rodzin | | | 86 / 0 / 6.56 mln (+3.5 tys./d) | log:123792, A 2 |
| porty (53 osady z `port_posX`): nadwyzka kasa - cel regulatora, d457 | | | srednia 84 404, mediana 41 301 (po tarczy: 42 568 / 27 735); 73% portow ma nadwyzke >= cena 1 statku | `kasy-miast.csv`, `py\krytyka\porty2.py` |
| porty: regulator kasuje / dosypuje (razem) | 67.0 / 28.0 | | 253.8 / 11.7 (sr. 458 dob 130.8 / 19.1) | `kasy-miast.csv` |

Uwagi do liczb:
- POPRAWKA (krytyka K1): wersja 1 podawala "srednia nadwyzka portow 2.66 mln" - to byla kolumna `kasa_do_celu` (stosunek kasa/cel, The Eyrie 2.65), nie zlotowki.
- R ma dwie przyczyny (B 4): **RB3** - skarbiec placi do portfela wezwanego krolestwa (`CrownCallToWar.cs:248-252`), a ksiega liczy tylko
  `KingdomBudgetWallet` (A: `MoneyLedger.cs:686`); gra wyplaca portfel rodom i zdejmuje go z portfela (gra: `DefaultClanFinanceModel.cs:628-638`) -
  [P] RB3 + "zaplacone do portfeli" + "dlug sprzed paczki" = -8 / +29 zl srednio. **Reszta poza tickiem** - konwoje DTE dostaja kapital
  z niczego (`DTE: CutTheirSupplyBehavior.cs:1012`, gra: `CaravanPartyComponent.cs:242-243`), a DTE kasuje je przez `MobileParty.RemoveParty`
  (`DTE: :1153-1176`), ktore wysyla tylko `OnPartyRemoved` (gra: `MobileParty.cs:3516-3534`, `IsActive=false` przed zdarzeniem), a nasz nasluch
  slucha `MobilePartyDestroyed` (A: `ArmouryBehavior.cs:615`, `CirculationWindows.cs:1502-1521`); [S] korelacja 30-dobowa z kapitalem lordow -0.70.
- Wszystkie pozostale pozycje z tabeli sa **nazwane** - ich zamkniecie zmienia ilosc zlota swiata, nie reszte. Prog etapu (R < 10 tys.) zalatwia
  E3/0; reszta kawalkow to "nic z niczego, nic w nicosc".

---

## 2. Reguly (jedna na zjawisko, platnik -> odbiorca)

**Z czego placi osada** (jedna definicja dla wszystkich regul nizej - po krytyce bez osobnej "puli przymusowej 25%"): z **nadwyzki** =
kasa - zapas kupcow - pieniadze pod tarcza (zold i dwor w drodze do pana) - to, co osada juz dzis wydala na te cele (jeden licznik na osade i dobe).
Zapas kupcow: miasto 10 000 + 12 x dobrobyt (cel regulatora gry, gra: `DefaultSettlementEconomyModel.cs:75-79`; A: `MoneyLedger.Obieg.cs:504-509`);
zamek - `CastlePurse.Spendable` (A: `CastlePurse.cs:377-392`, zapas `Reserve` :191-199 - ta sama regula wydatkow kasy zamku co B-2). Tarcze:
`SoldierPay.HeldOf` (A: `SoldierPay.cs:790`) + znacznik dworu (A: `SoldierPay.cs:586-600`). Osada nigdy nie schodzi ponizej zapasu przez nasze wyplaty
(ponizej regulator gry dosypuje z niczego). Dlaczego bez puli 25%: (1) w zamkach regulator nic nie kasuje (A: `CastlePurse.cs:280-282`), nadwyzka to
pieniadze pana - uzasadnienie "pieniadze, ktore i tak by zniknely" tam nie dziala (krytyka D1); (2) to samo zjawisko "port kupuje statek" mialo dwie granice
(krytyka W4); (3) przy 25% porty placily 22% naleznego (rozdz. 4). Kwot wymuszonych (lup z oblezenia, nagroda, premia zarzadcy) jest malo (ok. 1.4 tys./d).

| # | Zjawisko | Regula | Platnik -> odbiorca | Dzis |
|---|---|---|---|---|
| R1 | statki, ktorych wlasciciel nie zatrzyma (rozbita na ladzie albo rozwiazana druzyna, pryz z bitwy morskiej) | sprzedaz portowi za cene gry `GetShipTradeValue(statek, sprzedajacy, port)` (= 0.3 x wartosc z politykami - naprawa, Naval: `NavalDLCShipCostModel.cs:45-59` - ta sama, co zwykla sprzedaz portowi; AI i gracz). Kupuje port ze stocznia i miejscem: najpierw wlasne krolestwo, potem krolestwa nie w wojnie z rodem; kto ma cala cene w nadwyzce - najblizszy; nikt - ten z najwieksza nadwyzka placi, ile ma (sprzedaz z koniecznosci); 0 zl albo cena <= 0 albo brak miejsca - statek ginie (zlom) | nadwyzka portu -> glowa rodu / zwyciezcy | z niczego |
| R2 | zakup przez osade (jency-zolnierze; statek kupiony / sprzedany w porcie) | cena gry, osada placi z nadwyzki (jency: brak = mniej pieniedzy; AI nie sprzedaje statku portowi bez pieniedzy); kupno statku od portu (takze przez karawane) - do kasy portu | kasa osady kupujacej -> sprzedajacy; kupujacy -> kasa portu | z niczego / w nicosc |
| R3 | nagroda lub prowizja od miasta (turniej, lup z oblezenia, premia perka zarzadcy, myto) | z nadwyzki miasta; myto z kasy wjezdzajacej karawany | kasa miasta / kasa karawany -> zdobywca, zarzadca | z niczego |
| R4 | koszty ludzi partii (zold karawany, oplata za awans - AI i gracz) | do sakiewki ludzi tej partii (MenPurse), wydaja w miescie; zaloga - do limitu sakiewki zalogi (K1), reszta do kasy jej osady; partia bez sakiewki (banda) -> kasa najblizszego miasta. Sakiewka rozbitej partii: zwyciezca z bohaterem 1/3 - 2/3; zwyciezca bez bohatera, ale handlujacy (banda) - do jego kasy; inaczej osada niszczyciela, potem najblizsze miasto (ta sama regula co kiesa w 3.2) | kiesa pana / kasa karawany -> sakiewka ludzi -> kasa miasta | w nicosc |
| R5 | zloto na cialach poleglych | z sakiewki ludzi przegranej partii (udzial na glowe, raz); zwyciezca: 1/3 do kasy partii zwyciezcy (u lorda = kiesa wodza), 2/3 do sakiewki jego ludzi; pusta sakiewka = 0 | sakiewka przegranych -> zwyciezca + jego ludzie | z niczego |
| R6 | dochod z ziemi i majatku | tylko z utargu za sprzedany plon (to, co BK juz dzieli przy powrocie taboru); nic za sama prace | kasa miasta (kupila plon) -> tabor -> pan / majatek / kiesa wsi | majatek BK: drugi raz z niczego |
| R7 | podatek od dochodu notabla (warsztaty BK) | placi naprawde notabl (to, co BK mu zatrzymuje) - panu miasta (jak BK), czesc autonomii miasta do kasy miasta; linia "Taxes from" w podatku miasta znika dla warsztatow notabli | notabl (kapital warsztatu) -> pan miasta + kasa miasta | w nicosc u notabla + z niczego u pana (para przez model) |
| R8 | oplaty lordow BK | tytuly i dylematy -> skarbiec krolestwa platnika (bez krolestwa - kasa najblizszego miasta); zmiana wydatkow dworu -> kasa siedziby rodu; przeniesienie dworu -> kasa nowej siedziby; pasowanie (wiano do 50 000) -> glowa nowego rodu rycerza | kiesa lorda -> skarbiec / kasa siedziby / nowy rod | w nicosc |
| R9 | kapital nowej partii handlowej | z zaplaty wlasciciela (cena kupna: kapital do kwoty gry, reszta do kasy miasta zakupu - zwierzeta, wozy, najem); AI (BK) i gracz; partia, ktora nie handluje (konwoj DTE) - kapital 0 | kupujacy -> kasa miasta -> kasa karawany | z niczego / w nicosc |
| R10 | partia znika bez bitwy (rozwiazanie: niszczyciel null; usuniecie przez `RemoveParty`; przypadki "poza regula" 3.2) | kiesa -> glowa rodu wlasciciela (brak - kasa najblizszego miasta); sakiewka ludzi -> kasa najblizszego miasta | kasa partii -> glowa rodu / miasto | w nicosc |
| R11 | zgon bohatera, ktorego zlota gra nikomu nie oddaje (notabl i wedrowiec bez rodu, glowa rodu bez nastepcy) | zloto do kasy jego osady (notabl), osady, w ktorej przebywa, inaczej najblizszego miasta; bohater z rodem / glowa z nastepca - jak gra (gra: `KillCharacterAction.cs:98`, `ChangeClanLeaderAction.cs:24`) | kiesa zmarlego -> kasa osady | w nicosc |
| R12 | werbunek | sprzedajacy dostaje dokladnie to, co naprawde zeszlo z kiesy kupujacego (gra obcina do kiesy); notabl zmarly/brak - kasa osady; ochotnik z mapy (dzis zablokowany) - kiesa najblizszej wsi | kupujacy -> notabl / osada | czesc w nicosc |
| R13 | zold istot, ktore nic nie wydaja (Inni) | ODLOZONE do ustalenia zrodla ich dochodu (krytyka W12) - zamkniecie samego zoldu zamieniloby pare "z niczego -> w nicosc" na gromadzenie | - | w nicosc |
| R14 | zysk karawany pana | wplata ponad 10 000 w kazdym miescie krolestwa wlasciciela, nie tylko w jego siedzibie (D-9 mowil "w najblizszym" - swiadoma zmiana, E3/9) | kasa karawany -> pan | bez zmian (przelew), tylko pozniej |
| R15 | portfele wezwan do wojny (168) | portfel wezwanego to posiadacz zlota w ksiedze (pomiar); wplata do portfela ujemnego - osobna pozycja | skarbiec -> portfel -> rody wezwanego | niepoliczone (RB3) |

Wspolne zasady (jak w E2 2.0b): jedna regula dla gracza i AI; kazdy przelew przez `GiveGoldAction` z prawdziwym platnikiem albo z licznikiem
w ksiedze; kwota oddawana odbiorcy = to, co naprawde zeszlo platnikowi (migawka kiesy przed/po), nie kwota z modelu - gra obcina wyplate do kiesy
(gra: `GiveGoldAction.cs:11-25`); zapasowy odbiorca (glowa rodu -> kasa najblizszej osady), licznik "bez odbiorcy" z progiem 0; kazdy kawalek ma wlasny
wylacznik (wylaczony = stan sprzed); wyjatki liczone per obiekt (licznik potkniec), bez globalnych wylacznikow (CLAUDE.md 7).

---

## 3. Kawalki do kodu - trzy fale

Kawalki nazwane **E3/0 ... E3/12** (zeby nie mylic z pozycjami PLAN 3.x). Numery paczek: 164a-0..164a-8, 187, 181a-c (wolne: 186 = korona pozycza,
187+ nieuzyte w repo i CHANGELOG; 3.2 nie ma numeru). Kazdy kawalek = osobny commit + wpis CHANGELOG.

**Fale (po krytyce W10 - OB rozdz. 12 "najpierw pomiar, potem ujscia, potem zrodla", B 5 pkt 1 "reszta < 10 tys. przed zmianami zlota", CLAUDE.md zasada 2):**

| Fala | Kawalki | Tor | Test | Warunek wejscia |
|---|---|---|---|---|
| 1 | E3/0 (sam pomiar, gra bez zmian) | S | nowa kampania 40 dob + zapis d362 9 dob | - |
| 2 | E3/1 - E3/9 (bez E3/8d) | L ([D] 09.10 12:45) | bieg bazowy 120 dob (DLL fali 2, nowe wylaczniki OFF) + bieg etapu 120 dob + zapis 9 dob | fala 1: R nazwane (albo plan B); E3/3 tylko gdy 3.2 jest w grze |
| 3 | E3/10 - E3/12 (statki) | L | bieg 120 dob (baza = fala 2) + zapis 9 dob; potem kontrfakt roczny | odpowiedz Jeffa (rozdz. 4); przy (b) - kod i test teraz, wgranie z wylacznikami OFF do etapu 5 |

Gdy prog fali 2 nie przejdzie: wylaczamy jeden kawalek w `Armoury.json` autotestu i powtarzamy - jedna zmiana naraz.

**Wspolne dla wszystkich:** latki w kampanii - w `EnsureHooks()` wolanym z `OnSessionLaunched` (A: `ArmouryBehavior.cs:1129` i dalej, obok
`GentryService.EnsureHooks()` :1159), latki na typach gry - w `ApplyAll` (A: `SubModuleMain.cs`); typy NavalDLC/BK/DTE przez `AccessTools.TypeByName`,
brak metody = wpis "BRAK" w linii startowej i kawalek nieczynny. Nowe klucze w `Settings.cs`, potem `python tools/gen_mcm.py` (CLAUDE.md 3).
Okna ksiegi 169 (A: `CirculationWindows.cs`) na tych samych metodach maja prefiksy `void` z `Priority.First` (A: `CirculationWindows.cs:1685`) - biegna takze,
gdy nasz prefiks `bool` zwraca false (sprawdzone przez A w `0Harmony.dll`, `HarmonyLib.MethodCreator`), wiec kontrolki okien zostaja i pokazuja "wywolan n /
trafien z niczego 0". Nasze prefiksy zmieniajace zloto maja priorytet nizszy niz `First` (inaczej migawka okna widzi juz zmiane - krytyka D3).
Kazda zmiana kasy osady poza `GiveGoldAction` - licznik `MoneyLedger.Note(...)` (linia "Przeplywy osad", reszta kas miast dzis +3.9 tys./d sr. 28, log:123827).

### FALA 1

### E3/0 - Pomiar (164a-0, tylko log) - S
Cel: nazwac obie czesci R, zmierzyc statki w "bitwach", podwojny zwrot za statki, powtorne lupienie cial i dochod Innych. Gra bez zmian.
- **Portfele wezwan:** `MoneyLedger.ReadHolders` (A: `MoneyLedger.cs:664-707`, obok :686) - nowy posiadacz `HCallToWar` = suma `max(0, k.CallToWarWallet)`
  krolestw niewyeliminowanych; linia "Pieniadz swiata" - "portfele wezwan (168)". Splata dlugu portfela sprzed paczki (A: `CrownCallToWar.cs:176-183`,
  skarbiec placi, a ujemny portfel nie jest posiadaczem) -> nowa nazwana pozycja ujsc "dlug portfela wezwania sprzed paczki 168" (`AddWorld` w bloku;
  licznik `_dLegacyPaid` juz jest). **Krolestwo naraz wzywajace i wezwane** (jedno pole netto, A: `CrownCallToWar.cs:248-250`): wplata do portfela, ktory
  zostaje ujemny, nie jest posiadaczem i nie trafi do rodow (gra wyplaca tylko z dodatniego, `DefaultClanFinanceModel.cs:630`) -> osobna pozycja ujsc
  "wplaty do ujemnego portfela wezwania" (czesc wplaty ponizej zera, liczona w `Pay` przed i po) (krytyka D8). Ksiega: RB3 -> ok. 0; wyplata portfela rodom
  staje sie przelewem (posiadacz -> posiadacz), wiec "rozliczenia rodow na plus" spadna o te kwote (ok. 3.4 tys./d) - zgodnie.
- **Partie usuwane bez zniszczenia:** nasluch `CampaignEvents.OnPartyRemovedEvent` w `RegisterEvents` (A: `ArmouryBehavior.cs:615`) ->
  `CirculationWindows.OnPartyRemoved(PartyBase)`: partia bez wodza z kiesa, ktorej NIE widzial `OnPartyDestroyed` (zbior id oznaczanych w
  `OnPartyDestroyed` **w pierwszej linii, przed bramka `Gate`, `!IsActive` i wyjsciem dla partii lorda** - A: `CirculationWindows.cs:1504-1507`, krytyka D9;
  czyszczony w `ClearDay`; nie mozna uzyc `IsActive` - gra zeruje je przed zdarzeniem) -> nowa pozycja **U8r "kiesy partii usunietych bez zniszczenia"**
  (konwoje DTE = karawany z `Ai.DoNotMakeNewDecisions`, inne karawany, inne). Etykieta U8 jest zajeta: "kiesy partii bez wodza, ktore zniknely z mapy"
  (A: `MoneyLedger.Obieg.cs:348`) - zostaje; w U8 nowy podzial "w tym rozwiazane (niszczyciel null)" (krytyka W5). Sakiewki ludzi takich partii - informacja.
- **Zwrot za statki dwa razy (krytyka W6):** NavalDLC slucha `OnPartyDisbanded` i `MobilePartyDestroyed` (Naval: `NavalShipDistribution.cs:24-25`), oba wolaja
  `DistributePartyShipsAndRecoverGold` (:28-48); `DestroyPartyAction.ApplyForDisbanding` wysyla oba po kolei (gra: `DestroyPartyAction.cs:40-41`, potem :23),
  a `RecoverGold...` (:50-70) statkow nie zabiera - rozwiazanie na ladzie placi te same statki drugi raz. Okno "statki-inne" (A: `CirculationWindows.cs:1761`)
  dostaje podzial: zwrot przy rozwiazaniu / przy zniszczeniu / **drugi zwrot tej samej partii w dobie** (zbior id) - kwota podwojna w linii "Obieg: okna".
- **Statki w "bitwach":** prefiks/finalizer na `MapEvent.LootDefeatedPartyShips` (gra: `MapEvent.cs:1750-1818`) - suma `PlunderedGold` zwyciezcow
  przed i po -> informacja "w tym statki zatopione w bitwach morskich" przy pozycji "bitwy" w ZRODLACH.
- **Powtorne lupienie cial:** licznik wywolan `LootCasualtyCharacter` na jedno `MapEvent` wobec liczby cial (zabici + ranni pokonanych) - w "Obieg: okna"
  liczba bitew z wywolaniami > 3 x ciala i najwieksza krotnosc (skad 371 043 wywolan w d420, log:113728).
- **Inni (krytyka W12):** kiesy rodow nieumarlych (`Undead.Party` / kultura Innych, rod "Others") razem i ich wplywy wedlug zrodla: rozliczenie rodu (model),
  zwrot korony 165 ze skarbca ich krolestwa, zloto z bitew (`PlunderedGold`, lup z cial), Bank, inne - w linii "Szczelnosc (164a)". Sprawdzone: zapomoga ROT
  `ROTCoreBehavior.DailyTickClan` (ROT: `32dec\ROT.cs:46915-46921`, Tier x 5000 z niczego) jest u nas zablokowana (A: `KingdomTreasury.cs:547-559`,
  `NoRotClanBailout = true`), a `ROTClanFinanceModel` nic nie dopisuje (ROT: `dec\rotfin.cs`) - zrodlo zoldu Innych (sr. 7.7 tys./d) jest nieznane.
- **Narzedzie:** `tools/sprawdz_logi.py --grupa etap3` - progi z rozdz. 5.
- Klucze: brak nowych (`CirculationLedgerEnabled`). Koszt: O(1) na zdarzenie.
- Oczekiwane [S]: RB3 -> 0, "poza tickiem" -> 0..-3 tys. (gdy trop DTE sie potwierdzi) => R ok. -1..0 tys. Gdy nie: plan B (rozdz. 7) przed fala 2.

### FALA 2

### E3/1 - Prawdziwy platnik i kasy osad (164a-1) - M
Wspolny mechanizm dla E3/1, E3/10-E3/12 i czesci E3/8 oraz pierwsze zastosowania (jency, oblezenia, turnieje, myto, jency z rabunkow BK).
- **Nowy plik `RealPayer.cs`:**
  - prefiks (priorytet nizszy niz okna) na `GiveGoldAction.ApplyInternal` (gra prywatna, `GiveGoldAction.cs:9-39`; parametry `giverHero, giverParty, recipientHero,
    recipientParty, ref goldAmount`). Gdy otwarty kontekst (stos do 4 poziomow: rodzaj, platnik `PartyBase`, odbiorca zastepczy, oczekiwany odbiorca/platnik
    gry, limit) i zdarzenie jest "z niczego" (oba giver null) do oczekiwanego odbiorcy -> `giverParty` = osada/partia platnika, `goldAmount` = min(kwota, limit).
    Symetrycznie "w nicosc" (oba recipient null) -> `recipientParty` = osada. Gra obcina kwote do CALEJ kasy platnika (`GiveGoldAction.cs:21-25`), dlatego
    limit = nadwyzka (rozdz. 2) liczymy sami; zdarzenie idzie z prawdziwym platnikiem - ksiega widzi przelew. Bez kontekstu: `return true` w pierwszej linii.
  - `Spare(Settlement)` - nadwyzka z rozdz. 2 (miasto: kasa - (10 000 + 12 x dobrobyt) - `HeldOf` - wydane dzis; zamek: `CastlePurse.Spendable` - `HeldOf` - wydane dzis;
    slownik "wydane dzis" czyszczony raz na dobe).
  - `ApplyForSettlementToCharacter` nie obcina do kasy osady (gra: `GiveGoldAction.cs:52-55` -> :11-14 z ujemna kwota) - kazde nasze bezposrednie uzycie
    (E3/10, E3/12) samo pilnuje kwota <= `Spare` (krytyka D2).
  - Sprawdzone: BK, BKROTPatch (`dec\bkrot_all.cs`), ROT, BetterEconomy (`32dec\`) nie lataja `GiveGoldAction`; nasza jedyna latka to
    `ApplyForCharacterToSettlement` (A: `Patches.cs:483`, zapora godzinowki kuzni) - inna metoda. Przy kodzie: zrzut `Harmony.GetPatchInfo` dla
    `ApplyInternal` w linii startowej (Diplomacy, DTE, RBM, Spoils nie sprawdzone).
- **Zastosowania (prefiks otwiera kontekst, finalizer zamyka):**
  - jency: `SellPrisonersAction.ApplyInternal` (gra: `SellPrisonersAction.cs:9-90`); platnik = osada kupujaca: `buyerParty.Settlement`, inaczej `settlement` z :12,
    **inaczej (sprzedajacy ruchomy, kupujacy null - gracz u posrednika `PartyScreenHelper.cs:614-617` i "sprzedaj wszystkich" `PlayerTownVisitCampaignBehavior.cs:590-594`)
    `Settlement.CurrentSettlement`** (krytyka W1); limit = wartosc jencow-zolnierzy (te same wywolania modelu co gra :61-62) i nadwyzka osady. Gracz: gdy osada
    zaplacila mniej niz cena z ekranu (ekran posrednika pokazuje pelna wycene - `PartyScreenLogic.cs:826`), komunikat po angielsku: "{TOWN} could pay only {GOLD}
    of {FULL} for your prisoners." (przy kodzie sprawdzic, czy wycene ekranu da sie obciac do nadwyzki - wtedy obciac, komunikat zostaje jako zapas).
    Czesc za **bohaterow** zostaje jak dzis (z niczego; u gracza rownowazy ja 178 - A: `Ransom178.cs:495-540`, RC: `FairRansom.cs:115-125`), wyplacona
    w finalizerze osobnym zdarzeniem w oknie O09 - nic nie placi sie dwa razy. Sprzedaje sama osada (twierdza, wyplata do jej kasy z :86-87, "jency do kas twierdz"
    0.1 tys./d): placi kupujacy (jego kiesa albo kasa), brak kupujacego - 0 (jency wypuszczeni). Twierdza kupujaca (zamek) placi z `CastlePurse.Spendable`
    (pieniadze pana - jak kazdy wydatek kasy zamku, krytyka D1). Prefiks BK na tej metodzie (polityka karna, A: `PrisonerLaw.cs:21, 221`) moze jencow stracic
    przed zaplata - placi sie tylko za tych, ktorych gra liczy;
  - lup z oblezenia: `SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied` (gra: `:123`, wyplaty `:164, :168`); platnik = zdobyta osada;
  - nagroda turnieju: `TournamentManager.GivePrizeToWinner` (gra: `:180-192`); platnik = miasto-gospodarz (`tournament.Town`);
  - myto (perk Tollgates): `CaravansCampaignBehavior.OnSettlementEntered` i BK `OnSettlementEntered` karawan (A: okna `CirculationWindows.cs:1770, 1811`);
    platnik = wjezdzajaca karawana (jej kasa);
  - jency z rabunkow BK: BK `BKRaidCaptureBehavior.ExecuteCapture` (A: okno :1786); platnik = najblizsze miasto krolestwa najezdzcy.
- **Klucz:** `TownPaysRewards = true` - MCM "Towns Pay For Prisoners, Prizes and Siege Loot" / "Prisoners sold in a town, tournament prizes, siege loot and
  toll fees are paid from the town's (or the caravan's) spare money above its merchants' reserve instead of out of thin air. A poor town pays less."
  (klucz `ForcedSaleSpareShare` z wersji 1 usuniety - jedna definicja nadwyzki.)
- **Log:** nowa linia "Szczelnosc (164a): dzien N | ..." - sekcja "platnik": na rodzaj kwota gry / zaplacone przez osady / uciete (brak nadwyzki) /
  osady bez nadwyzki; wyplaty ponad nadwyzke (prog 0); gracz u posrednika: kwota ekranu / zaplacone; linia startowa: latki wpiete / BRAK.
- **Ksiega:** "jency sprzedani przez partie", "jency do kas twierdz", "jency z rabunkow BK", "lup z oblezen", "nagrody turniejow", "myto" w ZRODLACH -> 0
  (zostaje czesc bohaterow); zdarzenia staja sie przelewami osada -> bohater, ktore "Przeplywy osad" juz licza (A: `MoneyLedger.cs:330-331`).

### E3/2 - Majatki BK: dochod tylko ze sprzedazy (187) - S
Projekt A rozdz. 4, z poprawka sredniej (krytyka W7).
- **Nowy plik `EstateIncome187.cs`:** prefiks `bool` na BK `BannerKings.Managers.Populations.Estates.EstateData.DailyProductionIncome` (BK: `EstateData.cs:106-193`,
  wolane `BKSettlementBehavior.cs:264-273`), przy wylaczniku `return false` (bez wyplaty z niczego). **`LastIncome`:** BK dopisuje udzial z utargu przez
  `LastIncome += ...` (BK: `EstateData.cs:96-98`), a jedyne zerowanie bylo w wylaczanej metodzie (`LastIncome = num11`, :181-187) - samo zerowanie co dobe
  dawaloby "0" w wiekszosc dni i skaczaca cene majatku (`LastIncome x DaysInYear`, BK: `BKEstatesModel.cs:504`), a brak zerowania - wzrost bez konca.
  Dlatego w tym samym prefiksie: srednia biezaca 28 dob udzialu z utargu na majatek - `dzis = LastIncome - sr_wczoraj` (to, co BK dopisalo od wczoraj),
  `sr = sr_wczoraj + (dzis - sr_wczoraj) / 28`, `LastIncome = sr`. `sr_wczoraj` w slowniku sesji (po wczytaniu = `LastIncome` z zapisu BK), wiec bez nowego
  stanu w zapisie. Getter `Estate.EstimatedDailyIncome` (BK: `Estate.cs:128` zwraca `LastIncome`) - bez latki. Wyjatek: licznik potkniec, wyplata i tak pominieta.
- **Klucz:** `BkEstateIncomeFromSalesOnly = true` - MCM "BK Estates: Income Only From Village Sales" / "Banner Kings estates no longer pay a daily production
  income out of thin air; an estate earns only its share of the harvest the village carts sell in town. The estate window shows the 28-day average."
- **Log:** "Majatki BK (187): dzien N | wsie / majatki pominiete, udzial z utargu (z 112), srednia 28 dob razem, majatki z dochodem w 28 dobach, a dzis 0 (prog 0),
  sredni wplyw notabli wiejskich, pasmo (z "Obieg")".
- **Ksiega:** "majatki BK - dochod z produkcji" -> 0, kontrolka `majatki-BK 571/0` (okno A: `CirculationWindows.cs:1831` zostaje); prawdziwy dochod
  (112, ok. 51 tys./d, log:123832) bez zmian; pasmo notabli: w nicosc o ok. 230-290 tys./d mniej, dosypka +0..60 tys. [S, A 3.1]. Zloto swiata ok. 0 +-50 tys.
- Kolejnosc: musi wejsc przed 164c (etap 5) - spelnione.

### E3/3 - Lup z cial z sakiewki przegranych (164a-2) - S - tylko razem z 3.2
- **Plik `CasualtyLoot.cs` (164a-2):** prefiks `bool` (priorytet nizszy niz okno O16 - `Priority.First`, A: `CirculationWindows.cs:1685`, krytyka D3) na
  `MapEvent.LootCasualtyCharacter` (gra: `MapEvent.cs:1850-1876`, tylko galaz AI - wodz zwyciezcow nie jest graczem): kwota gry `num` (ten sam wzor
  `(int)(Round(oczekiwana) x aiTradePenalty)`, :1864); limit na cialo = sakiewka przegranej partii w chwili pierwszego wywolania dla tego `MapEvent` / ludzie
  tej partii przed bitwa (migawka na `MapEventParty`, czyszczona po bitwie); `t = MenPurse.Take(przegrana, min(num, limit))` (A: `MenPurse.cs:157`);
  `t/3` -> `winnerParty.Party.MobileParty.PartyTradeGold += t/3` (to samo miejsce co gra :1866: u lorda = kiesa wodza, gra: `MobileParty.cs:425-433`;
  u karawany z bohaterem - kasa karawany), reszta -> `MenPurse.Add(zwyciezca)`; u lorda `ClanIncomeBook.NoteInflow(wodz, t/3, KThird)` jak A: `MenPurse.cs:104`;
  `SkillLevelingManager.OnAIPartyLootCasualties(t, ...)` jak gra; `return false`. Galaz gracza (przedmioty) bez zmian.
  Gracz przegrany: AI zabiera z sakiewki jego ludzi (jedna regula) - komunikat po angielsku raz na bitwe ("The victors stripped your fallen men of {GOLD}.").
- 1/9 korony: czesc lorda (`t/3`) do podstawy prawa trzecich przez `Ransom178.NoteBeatenGold` z 3.2 ([D] 09.10 03:45 nr 18). **E3/3 wchodzi tylko, gdy 3.2
  jest w grze** (krytyka D4; PLAN 3.1 "zalezy od 3.2") - bez 3.2 klucz wymuszony na false i wpis w linii startowej.
- **Klucz:** `CasualtyLootFromPurse = true` - MCM "Battle Loot Comes From The Fallen's Purses" / "The gold an AI victor takes from the bodies comes out of the
  purse of the beaten men (a third to the lord, the rest to his men), not out of thin air; an empty purse gives nothing."
- **Log:** sekcja "lup z cial" w "Szczelnosc (164a)": ciala, kwota gry, z sakiewek, do lordow / do sakiewek, uciete (pusta sakiewka), bitwy z powtornym lupieniem.
- **Ksiega:** Z1 "lup z cial dla AI" -> 0: okno O16 (A: `CirculationWindows.cs:783-811`) liczy zmiane `mp.PartyTradeGold` zwyciezcy; `LootFin` odejmuje kwote
  `t/3` przekazana w tym wywolaniu (pole statyczne ustawione w prefiksie) - przelew sakiewka -> kasa/sakiewka to posiadacze (bez pozycji). Skoki 1-10 mln znikaja same.
- Kolejnosc w bitwie: `LootDefeatedPartyCasualties` biegnie w `CalculateAndCommitMapEventResults` (gra: `MapEvent.cs:1423`) przed zniszczeniem partii,
  wiec potem `MenPurse.OnPartyDestroyed` (A: `MenPurse.cs:91-121`) oddaje tylko reszte sakiewki - bez podwojnej wyplaty. Bitwy osobiste z DTE: gra
  tego nie wola (A: `BattlefieldLaw.cs:578-588`).
- Wojsko [S, `py\pula_wariant.out`]: sam E3/3 (lordowie w wojnie traca 2/3..100% zlota z cial, skoki ponad 60 tys./d uciete) - d430-457 -0.3..-0.5%, glowy < 5 000 bez zmian.

### E3/4 - Zold karawan do sakiewek ludzi karawan (164a-3) - M
- **Karawany notabli:** finalizer na `NotablesCampaignBehavior.ManageCaravanExpensesOfNotable` (gra: `:309-331`; okno O02 A: `CirculationWindows.cs:666-721`,
  wpiete :1754): na kazda karawane zold, ktory naprawde zeszedl (migawka kasy przed/po bez doplaty do 5 000 - przelew notabl -> karawana) ->
  `MenPurse.Add(karawana, zeszlo)`. Zeszlo z kiesy notabla (karawana pusta) - tez do sakiewki tej karawany.
- **Karawany lordow:** A: `SoldierPay.cs:487` (`else _dOther += amt;` "karawany i inne partie: bez zmian") -> galaz `mp.IsCaravan`: `MenPurse.NoteWage`,
  `MenPurse.Add(mp, amt)`, `MoneyLedger.NoteWageRouted(true, amt)` - jak partie lordow (:480-485). Inne partie - bez zmian (`_dOther` zostaje, krytyka D10).
- **Wydatek:** `MenPurse.OnLeft` (A: `MenPurse.cs:256-308`) - karawana: cala sakiewka "na zycie" do kasy miasta przy wyjezdzie: **bez oszczednosci**
  na sprzet (:277-289 - karawany nie kupuja sprzetu, przy ok. 800 karawanach zamrozilyby kilka mln), **bez odziezy** (:268), **bez tarczy zoldu**
  (`SoldierPay.Hold` :301 - inaczej panowie miast +100-150 tys./d i zmiana budzetu 166; B 2.1). `NoteOwnPaid` (:297) zostaje (licznik "wlasne" D).
- **Sakiewka rozbitej karawany (krytyka W13, [D] 09.10 04:55 Q3b "jedna regula: gracz, AI, bandyci"):** w `MenPurse.OnPartyDestroyed` (A: `MenPurse.cs:99-117`)
  ta sama kolejnosc co kiesa w 3.2 (3.2: `BeatenPurse.cs:288-317`): zwyciezca z zywym bohaterem - 1/3 - 2/3 jak dzis; zwyciezca bez bohatera, ale handlujacy
  (`IsPartyTradeActive`, nie Inni - `Undead.Party`) - do jego `PartyTradeGold` (banda bierze); inaczej osada niszczyciela, potem najblizsze miasto.
  Dzis to dotyczy tylko sakiewek partii lordow (karawany sakiewek jeszcze nie maja), po E3/4 - takze karawan. Usunieta bez bitwy - E3/7.
- **Klucz:** `CaravanWageToPurse = true` - MCM "Caravan Wages Go To The Caravan's Men" / "The daily wages of caravan guards (notables' and lords' caravans)
  go to the purse of those men, who spend all of it in the next town they leave - not into thin air."
- **Log:** sekcja "karawany": zold do sakiewek (notable / lordowie), wydane w miastach, sakiewki karawan razem (prog: <= 3 dni ich zoldu), sieroty, sakiewki
  rozbitych karawan: do zwyciezcow z bohaterem / do band / do osad.
- **Ksiega:** U1 "zold karawan notabli" zostaje jako pomiar okna, a obok "minus oddane do sakiewek ludzi karawan (164a)" -> netto 0 (wzor: zwrot LevyGold,
  A: `MoneyLedger.Obieg.cs:337`, `MoneyLedger.cs:402`); w `CaravanWageFin` probka swiata dalej "rozjazd 0". Linia "Zold": nowa pozycja "karawany" -> 0
  (kwota w "zold, ktory nasze rozliczenie oddalo do sakiewek", A: `MoneyLedger.Obieg.cs:306`); "inne partie (bez zmian)" zostaje.

### E3/5 - Awanse do sakiewki (164a-4) - S
- **AI:** prefiks + postfiks na `PartyUpgraderCampaignBehavior.ApplyEffects` (gra: `PartyUpgraderCampaignBehavior.cs:139-151`; okno O05 A: `CirculationWindows.cs:548-559`, :1757):
  migawka kiesy platnika, ktorego wybierze gra (Owner zywy, inaczej wodz zywy - :141-150), przed i po; oddajemy **to, co naprawde zeszlo** (gra sprawdza zloto
  wodza :113-120, a placi Owner, ktory moze miec mniej - `GiveGoldAction` obcina, krytyka W8) -> `MenPurse.Add(partia, zeszlo)` (zolnierz kupuje sprzet
  swojego stopnia - spojne z K1/175); banda/partia bez sakiewki -> kasa najblizszego miasta (`MoneyLedger.Note`).
- **Zaloga (krytyka D5):** do sakiewki zalogi najwyzej do limitu K1 (to samo `room` co `SoldierPay.GarrisonShare`: `MenGearSaveDays` x zold - sakiewka,
  A: `SoldierPay.cs:491-501`); reszta - do kasy osady zalogi (`MoneyLedger.Note`), jak czesc zoldu zalog (`coffers`).
- **Gracz (krytyka W2):** gra pomija gracza w `UpgradeReadyTroops` (`PartyUpgraderCampaignBehavior.cs:170`); gracz placi na ekranie druzyny - koszt do
  `PartyGoldChangeAmount` (gra: `PartyScreenLogic.cs:922`), potem `GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, kwota < 0)` (:1258) - w nicosc.
  Zmiana: prefiks na `PartyScreenLogic.UpgradeTroop` dopisuje koszt awansu tego ekranu (zerowane przy otwarciu, `Reset` i anulowaniu), prefiks/postfiks na
  `DoneLogic` (latany juz przez A: `Stables.cs:1107`, `GarrisonArmory.cs:1096` - czytamy tylko kiese gracza przed/po, bez kolizji): gdy ekran sie zamknal
  z zatwierdzeniem i bez `DoNotApplyGoldTransactions` -> `MenPurse.Add(MainParty, min(suma awansow, spadek kiesy gracza))`. Ta sama regula co AI.
- **Klucz:** `UpgradeCostToPurse = true` - MCM "Upgrade Fees Go To The Men" / "The gold paid to upgrade troops (by you or the AI) goes to the purse of the
  party's men (they buy the kit of their new rank in town), not into thin air."
- **Ksiega:** "awanse" w nicosc minus "oddane (164a)" -> netto 0 (ta sama nowa pozycja "minus oddane odbiorcom przez 164a" co E3/4); awanse gracza z ekranu - osobno w linii.
- Bez zmian: `Stables` (kon za awans), `OutlawLaw` (awanse band u pasera), `MenUpgrade` (dozbrajanie z sakiewki).

### E3/6 - Werbunek: dokladny zwrot (164a-5) - S
- A: `LevyGold.cs:26-49` liczy koszt OD NOWA po werbunku (`:35`), a gra liczy go PRZED dodaniem rekruta (gra: `RecruitmentCampaignBehavior.cs:608`),
  pobiera go `GiveGoldAction` z obcieciem do kiesy wodza (:624-628) i pomija notabla `null`/martwego (A :39) - stad 4.9-6.0 tys./d w nicosc [P, B 2.5].
  Zmiana: prefiks na `RecruitmentCampaignBehavior.ApplyInternal` zapisuje w `__state` kiese `LeaderHero` przed; postfiks oddaje **to, co naprawde zeszlo**
  (przed - po; krytyka W8 - nie kwota modelu); notabl brak/zmarly -> kasa osady (`__1`); `VolunteerFromMap` (dzis zablokowane latka CS T2b, gra :629-632)
  -> kiesa najblizszej wsi (regula na 100% przypadkow).
- **Klucz:** bez nowego - poprawka `RecruitGoldToSeller` (A: `Settings.cs:558`).
- **Ksiega:** "werbunek ochotnikow od notabli" w nicosc - zwrot = 0 (+- zaokraglenia). Zwrot najemnikow (A: `LevyGold.cs:50-86`) bez zmian.

### E3/7 - Kapital karawan i partie znikajace bez bitwy (164a-6) - S
- **Konwoj DTE - kapital 0:** prefiks/finalizer na DTE `DynamicTroopEquipmentReupload.CutTheirSupplyBehavior.SpawnReinforcementCaravan` (DTE: `:971-1040`,
  prywatna) otwiera kontekst; postfiks `Priority.Last` na `CaravanModel.GetInitialTradeGold` (te same typy, ktore wpina okno O17 - A: `CirculationWindows.EnsureModelHooks`)
  zwraca 0 w kontekscie. Konwoj nie handluje (`SetDoNotMakeNewDecisions(true)`, DTE :1018). Skutek uboczny (krytyka D6): rod dopelnia kase partii do
  5 000 (gra: `DefaultClanFinanceModel.cs:106` `PartyGoldLowerThreshold`, :826-859) - ok. 5 000 na konwoj wraca do glowy rodu przy usunieciu (nizej);
  zgodne z R9, w logu ("doplaty rodow do konwojow") i w kontroli budzetu 166.
- **Kupno karawany przez lorda BK:** kontekst w BK `BKLordPropertyBehavior.OnSettlementEntered` (okno "kupno-BK" A: :1810; zakup w lambdzie
  `RunWeekly`: `lord.ChangeHeroGold(-cena)` + `CreateCaravanParty`, BK: `BKLordPropertyBehavior.cs:60-79`; `RunWeekly` wykonuje od razu - `dek\BannerKingsBehavior.cs:10-16`);
  postfiks na `CaravanPartyComponent.CreateCaravanParty` (gra: `:240-247`) dla wlasciciela = ten lord: kasa karawany = min(kapital gry, cena), reszta ceny ->
  kasa miasta zakupu (`target`).
- **Gracz tworzy karawane (krytyka D7):** `CaravanConversationsCampaignBehavior.conversation_magistrate_form_a_caravan_accept_on_consequence` (gra: `:243-252`) -
  najpierw `CreateCaravanParty` (kapital z niczego przez `GetInitialTradeGold`), potem gracz placi cene kasie miasta (`ApplyForCharacterToSettlement`).
  Prefiks otwiera kontekst (kapital 0), finalizer po zaplacie: przelew miasto -> karawana min(kapital gry, cena) (`GiveGoldAction.ApplyForSettlementToParty`,
  najwyzej to, co miasto wlasnie dostalo). Ta sama regula co BK.
- **Partie znikajace bez bitwy (R10; krytyka W5):** (1) `OnPartyRemoved` bez `MobilePartyDestroyed` (nasluch z E3/0; DTE `RemoveParty`), (2) `MobilePartyDestroyed`
  z niszczycielem null (rozwiazanie: `DestroyPartyAction.ApplyForDisbanding`, gra: `DestroyPartyAction.cs:34-41`; 3.2 zostawia to "poza regula", 3.2: `BeatenPurse.cs:280`),
  (3) z 3.2 - przypadki "odd" (3.2: `BeatenPurse.cs:290`; galaz "odd" wola nasza metode zamiast samego licznika): kiesa partii bez wodza ->
  `GiveGoldAction.ApplyForPartyToCharacter(partia, glowa rodu wlasciciela)` (brak glowy - kasa najblizszego miasta, `VillageTakings.NearestTown`, w 3.2 internal).
  Sakiewka ludzi: przy niszczycielu null juz dzis do najblizszego miasta (A: `MenPurse.cs:111-119`); przy `OnPartyRemoved` (sierota) - do najblizszego miasta
  (`MoneyLedger.Note169` jak A: `MenPurse.cs:113-116`). Kolejnosc nasluchow `MobilePartyDestroyed`: po `BeatenPurse` (3.2), przed oknem O38
  (A: `CirculationWindows.cs:1502-1521`) - gra wola od ostatnio dopisanego (B-1), wiec dopisujemy miedzy nimi.
- **Klucz:** `CaravanCapitalReal = true` - MCM "Caravan Capital From The Buyer" / "A caravan bought by a lord or by you starts with capital taken out of the
  payment (the rest stays with the town that sold the beasts and carts); supply convoys that do not trade start with none. A party disbanded or removed
  without a battle hands its purse to its owner's house."
- **Log:** sekcja "kapital": konwoje DTE (n, kapital 0, doplaty rodow), kupno BK i gracza (cena, kapital, do kasy miasta), znikajace bez bitwy wedlug rodzaju
  (rozwiazane / usuniete / "odd" 3.2): kiesy -> glowy / miasta, sakiewki -> miasta.
- **Ksiega:** Z2 "lordowie" -> tylko zakupy z kapitalem z ceny (nazwane "kapital z ceny kupna" i odjete od Z2 i U6 "kupno karawan" - netto 0);
  U8 (istniejaca) -> ok. 0 po 3.2; U8r "usuniete bez zniszczenia" (E3/0) -> 0.

### E3/8 - Podatek warsztatow BK, oplaty lordow BK, zloto zmarlych (164a-7) - M (trzy czesci; czesc (d) Inni - ODLOZONA)
- **(a) podatek warsztatow BK - prawdziwy platnik (krytyka K2):** dzis BK odejmuje podatek od wyplaty notabla (BK: `dek\BKClanFinanceModel.cs:28-36`;
  podatek = `ProfitMade x stawka`, `WorkshopExtensions.cs:10-16`; wyplata brutto = `ProfitMade / 2`, :39), a gra zdejmuje z warsztatu wyplate brutto
  (`DefaultClanFinanceModel.cs:908-919`) i wyplaca tylko `num > 0` (`ClanVariablesCampaignBehavior.cs:479-486`) - u notabla podatek znika. Rownoczesnie BK
  dopisuje ten sam podatek panu miasta jako linie "Taxes from {WORKSHOP}" (BK: `BKTaxModel.cs:198-205`, `GetWorkshopTaxes` :87-95 = ta sama `TaxExpenses()`),
  a Armoury te linie celowo zostawia (A: `PopulationLaw.cs:226-231`, `KeptTownLines` :257-261, x (1 - 0.6 x autonomia) :276-279) - pan dostaje ja z niczego
  w rozliczeniu rodu (gra: `DefaultClanFinanceModel.cs:403`). Wersja 1 (wplata do kasy miasta) dokladalaby trzecia noge - zloto z niczego ok. 50 tys./d.
  Zmiana (jedna noga zamiast pary przez nicosc):
  - prefiks + postfiks na BK `BKClanFinanceModel.CalculateNotableDailyGoldChange(hero, applyWithdrawals)` tylko przy `applyWithdrawals = true` i `hero.IsNotable`:
    prefiks liczy `podatek_w = TaxExpenses(w)` kazdego warsztatu (ta sama funkcja BK, przez refleksje); postfiks: zniklo naprawde
    `Z = min(suma podatku, max(0, wynik + suma podatku))` (wynik <= 0 - gra nic nie wyplaca, zniknal tylko podatek do wysokosci brutto);
  - `Z` rozdzielone na warsztaty wedlug `podatek_w`; odbiorca kazdej czesci - pan miasta warsztatu (`town.OwnerClan.Leader`, jak BK :200-201); czesc autonomii
    `0.6 x autonomia` (ta sama, ktora dzis obcina linie panu, A: `PopulationLaw.cs:276-279`) -> kasa miasta (`Town.ChangeGold` + `MoneyLedger.Note`), reszta ->
    `GiveGoldAction.ApplyBetweenCharacters(null, pan, x)` w kontekscie: licznik ksiegi "podatek warsztatow BK oddany panom (164a)" (netto 0 z "minus podatek
    warsztatow BK" w "zloto z niczego netto", log:123829); wplata liczona jak renta miasta - `PopulationLaw.RentToday[rod pana]`, a `ClanIncomeBook` pomija
    ja w "jednorazowych" - wiec czesc "ziemia" D (dzis ta linia siedzi w `wd` -> `QLand`, A: `ClanIncomeBook.StableD.cs:431`) i powinnosci 165 bez zmian;
  - `KeptTownLines` (A: `PopulationLaw.cs:257-261`) pomija warsztaty, ktorych wlasciciel jest notablem (`w.Owner.IsNotable`) - te ida przelewem. Warsztaty
    lordow w cudzych miastach zostaja jak dzis: obie nogi siedza w modelu (wydatek "Workshop taxes" wlasciciela, BK: `BKClanFinanceModel.cs:236-251`, i linia
    pana miasta) - rozdz. 7.
  - Dochod panow miast: bez zmian poza przypadkiem `wynik <= 0` (dzis pan dostaje caly podatek, choc notabl zaplacil mniej) [S < 5 tys./d].
  - Klucz `BkWorkshopTaxToLord = true` - MCM "BK Workshop Tax Paid By The Owner" / "The Banner Kings tax on a notable's workshop is really paid by the notable
    to the lord of the town (the town keeps its autonomy share), instead of vanishing on one side and being created from nothing on the other."
- **(b) oplaty lordow BK - odbiorca wedlug rodzaju (krytyka W11):** po oknach migawek (A: `CirculationWindows.cs:1797-1806` - BK zdejmuje zloto lorda bez odbiorcy) ta sama kwota:
  - tytuly (roszczenia, nadania, odebrania, uzurpacje - BK `TitleManager.cs:562, 586, 620, 727`) i dylematy (`BKDilemmaBehavior.cs:399-406`) -> `KingdomBudgetWallet`
    krolestwa lorda (bez krolestwa - kasa najblizszego miasta); korona liczy to jako wplyw dnia (A: `CrownIncome.cs:43-55`);
  - zmiana poziomu wydatkow dworu (`CourtGrace.AddExpense`, BK: `CourtGrace.cs:375-382`) -> kasa siedziby rodu (ta sama siedziba co dwor 162m, A: `ClanBudget.cs` `Seat`);
  - przeniesienie dworu (`MoveCourtDecision.ApplyGoal`, BK: `MoveCourtDecision.cs:101-110`) -> kasa nowej siedziby (`town` z decyzji);
  - pasowanie (`BKKnighthoodBehavior.CreateClan`, BK: `BKKnighthoodBehavior.cs:228-235` - stary rod placi do 50 000 przy zalozeniu rodu rycerza) -> glowa nowego rodu (wiano).
  - Klucz `BkLordFeesHaveRecipients = true` - MCM "BK Lord Fees Have A Recipient" / "Gold Banner Kings takes from lords goes somewhere: title and dilemma costs to
    the realm's treasury, court costs to the seat's purse, a new knight's endowment to the knight's new house."
- **(c) zloto zmarlego, ktorego gra nikomu nie oddaje (R11, krytyka D13):** prefiks + finalizer na `KillCharacterAction.ApplyInternal` (gra: `KillCharacterAction.cs:31`):
  po calej akcji (gra oddala juz zloto glowie rodu, :98, albo nastepcy glowy, `ChangeClanLeaderAction.cs:24`) - gdy zmarlemu (nie gracz) zostalo zloto ->
  `GiveGoldAction.ApplyForCharacterToSettlement(zmarly, osada, reszta)`: notabl - jego osada; inni (wedrowiec, glowa bez nastepcy) - osada, w ktorej przebywal,
  inaczej najblizsze miasto. Obejmuje 100% przypadkow bez wyliczania ich. Klucz `DeadHeroGoldStays = true` - MCM "A Dead Man's Gold Stays In The World" /
  "When nobody inherits a dead hero's gold (a notable, a wanderer, the last of a house), it goes to the purse of his settlement instead of vanishing."
- **(d) Inni bez zoldu - ODLOZONE (krytyka W12):** zrodlo ich dochodu nieustalone (E3/0 mierzy); zamkniecie samego zoldu zamienia pare "z niczego -> w nicosc"
  na gromadzenie w kiesach Innych (+5-10 tys./d). Po pomiarze: zamknac obie strony naraz (zold 0 u zrodla jak Straz, A: `MountedWage.cs:79`, i to samo zrodlo).
- **Log:** sekcje w "Szczelnosc (164a)": podatek warsztatow (zniklo u notabli / do panow / do kas miast - autonomia / wynik <= 0), oplaty BK wedlug odbiorcy,
  zmarli (notable / wedrowcy / glowy bez nastepcy -> osady).
- **Ksiega:** (a) "zloto z niczego netto (... minus podatek warsztatow BK)" -> ok. 0 przez nowa pozycje ZRODEL "podatek warsztatow BK oddany panom (164a)",
  a "rozliczenia rodow na plus" spadna o linie "Taxes from" warsztatow notabli (zrodlo znika - zgodnie); (b) U6 "pozostale/dylematy" minus "oddane odbiorcom (164a)"
  -> 0; (c) U7 "zgony" -> 0 (przelew przed wyjsciem ze swiata).

### E3/9 - Zysk karawany pana w kazdym miescie jego krolestwa (164a-8, D-9) - S
- BK `BKPartyBehavior.AddRealisticIncome` (BK: `:941-975`) wplaca zysk ponad 10 000 tylko w siedzibie, w osadzie wlasciciela albo u udzialowca;
  zysk lezy w kasie karawany (5.19 -> 7.22 mln, B 2.3). Prefiks/postfiks: gdy BK nie wplacilo (kasa bez zmian), a cel jest miastem krolestwa wlasciciela
  i wlasciciel jest lordem -> ta sama wplata `GiveGoldAction.ApplyForPartyToCharacter(karawana, wlasciciel, kasa - 10 000)` (przelew).
- **Swiadoma zmiana wobec audytu 12 D-9** ("w najblizszym miescie jego krolestwa", `12-DOCHODY-RODOW.md:205`; krytyka D14): karawana i tak jezdzi od miasta
  do miasta, wiec pierwsze miasto krolestwa na jej drodze jest w praktyce bliskim miastem; "najblizsze" wymagaloby liczenia odleglosci przy kazdym wjezdzie
  i odrzucaloby wplate w dobrym, ale nie najblizszym miescie. Skutek ten sam (pan dostaje zysk), regula prostsza.
- **Klucz:** `CaravanProfitInRealmTown = true` - MCM "Lord Caravans Deposit In Any Town Of The Realm".
- **Ksiega:** bez zmian (przelew posiadacz -> posiadacz); licznik w "Szczelnosc (164a)". Skutek: panowie z karawanami +60-120 zl/d (B) -> D "majatek" (A: `ClanBudget.cs:23`).

### FALA 3 (statki - po odpowiedzi Jeffa, rozdz. 4)

**Wybor portu (wspolny dla E3/10 i E3/12):** kandydaci = miasta ze stocznia (`GetShipyard()?.CurrentLevel > 0` - to samo, co sprzedaz gry,
Naval: `ShipTradeCampaignBehavior.cs:371-374`) z miejscem (`AvailableShips.Count < GetMaxShipCountForTown`, Naval: `ShipProductionCampaignBehavior.cs:242-247`
prywatna - refleksja), nieoblezone; kolejnosc: (1) z `ShipSalePortsTried` najblizszych portow **wlasnego krolestwa** pierwszy, ktorego `Spare` >= cena;
(2) to samo wsrod portow **krolestw nie w wojnie z rodem** (rod bez krolestwa / krolestwo bez portu - od razu tu); (3) inaczej kandydat z najwieksza
nadwyzka placi min(cena, `Spare`) (sprzedaz z koniecznosci); (4) zaplata 0, cena <= 0 albo brak kandydata z miejscem - statek ginie (zlom; port nie dostaje statku
za darmo - krytyka D11). Wyplata z jednego licznika "wydane dzis" (E3/1). Port obcego krolestwa: ta sama dopuszczalnosc co kupno statku przez AI
w obcym miescie (Naval: `ShipTradeCampaignBehavior.cs:194-201`, krolestwa nie w wojnie); dla sprzedazy z koniecznosci kupiec jest tam, gdzie sa pieniadze -
przy samym wlasnym krolestwie porty placa tylko 41% naleznego (rozdz. 4).

### E3/10 - Statki rozbitej albo rozwiazanej druzyny do portu (181a) - M
- **Nowy plik `ShipsToPort181.cs`:** prefiks `bool` na Naval `NavalDLC.CampaignBehaviors.NavalShipDistributionCampaignBehavior.RecoverGoldFromRemainingShipsAfterDistribution`
  (Naval: `NavalShipDistribution.cs:50-70`, prywatna; wolana po `DistributeShips` :72-86 przy `MobilePartyDestroyed` na ladzie i `OnPartyDisbanded`):
  - te same warunki co gra (:52); kazdy statek osobno: **cena = `GetShipTradeValue(statek, druzyna.Party, port.Settlement.Party)`** (sprzedajacy ruchomy, kupujacy
    osada: 0.3 x wartosc z politykami - koszt naprawy, Naval: `NavalDLCShipCostModel.cs:26-59`) - ta sama cena co zwykla sprzedaz portowi, dla AI i gracza
    (dzis AI x 1.0 bez naprawy, gracz x 0.3 bez naprawy - :55-60; krytyka W4); cena <= 0 - zlom;
  - port - "wybor portu" wyzej; kwota <= `Spare(port)` pilnowana w kodzie (krytyka D2);
  - przelew `GiveGoldAction.ApplyForSettlementToCharacter(port, glowa rodu, kwota)`; statek `ChangeShipOwnerAction.ApplyByTransferring(port.Party, statek)`
    (ten sam wzor co `DistributeShips` :82, wiec bezpieczny w tym zdarzeniu; transfer, nie handel - bramka E3/11 go nie dotyczy); `return false`.
  - **Podwojne wywolanie (krytyka W6):** przy rozwiazaniu na ladzie metoda biegnie dwa razy; po pierwszym statki sa juz w porcie (drugie wywolanie: `Ships.Count <= 0`,
    gra :52 konczy); statki zlomowane (krok 4) zostaja przy partii - zbior id partii w dobie, drugie wywolanie nic nie liczy i nic nie placi.
  - gracz - komunikaty jak gry (:63-66): rozwiazana - "{LEADER_NAME}'s party has disbanded. The port of {TOWN} paid you {GOLD} for its ships."; rozbita
    (niszczyciel nie null) - "{LEADER_NAME}'s party was lost. The port of {TOWN} paid you {GOLD} for its ships." (krytyka D12); zlom - "...Nobody could buy its ships."
  - Wyjatek per druzyna - licznik potkniec.
- **KW 168:** wplata od portu wpadnie do `real` w `ClanIncomeBook.OnEvent` (A: `:154-166`, `TodayEvtSettl`) i do sredniej lupow limitu KW
  (A: `DebtLadder.cs:185, 204, 401-414`); E2 R16 "limit bez statkow" -> nowe `ClanIncomeBook.NoteShipSale(rod, kwota)` (pole `TodayShips` odjete
  od `real` w `OnceToday`, A: `ClanIncomeBook.cs:139-150`; w `all` zostaje - splata KW 50% jak z lupu). Sprzedaz wlasnych statkow nie jest lupem - bez 1/9.
- **Klucze:** `ShipsToPort = true` - MCM "Lost Fleets Are Sold To A Port" / "When a party is destroyed on land or disbanded, the ships its house cannot use
  are sold to the nearest port of the realm with room in its shipyard (or, if the realm's ports cannot pay, to a port of a realm at peace with the house).
  The port pays the usual sale price (30% of value minus repairs) out of its spare money - the same for you and the AI. No buyer: the ships are lost."
  `ShipSalePortsTried = 3` - "How many nearest ports are asked for the full price before the one with the most spare money pays what it can": 3, bo 73% portow
  ma nadwyzke >= cena jednego statku (d430-457) - szansa, ze 3 najblizsze naraz nie maja, ok. 0.27^3 = 2%; wiecej portow = dalsze porty i wiekszy koszt przegladu.
- **Log:** nowa linia "Statki (181): dzien N | statki rozbitych: rozdane w rodzie / do portow wlasnego krolestwa / do portow obcych / zlom (brak miejsca, brak pieniedzy,
  cena <= 0); nalezne / zaplacone (sprzedaz z koniecznosci: ile mniej); wyplaty ponad nadwyzke (prog 0); drugie wywolania (W6); porty: ile zaplacilo, bez nadwyzki;
  wedlug rodzaju rodu (zamek / miasto / bez lenna / najemnik); gracz".
- **Ksiega:** "statki NavalDLC inne" -> 0; kontrolka "statki-inne" (A: `CirculationWindows.cs:1761`) - wywolan n, trafien z niczego 0; wplaty to przelewy
  osada -> bohater ("Przeplywy osad" juz je licza).

### E3/11 - Handel statkami przez kase portu (181b) - M
- Kontekst `RealPayer` w prefiksie `ChangeShipOwnerAction.ApplyInternal` (gra: `ChangeShipOwnerAction.cs:19-71`), tylko `ApplyByTrade`:
  - port sprzedaje (`owner.IsSettlement`, :24-44): "w nicosc" -> odbiorca = kasa portu (cena gry; dla AI z rabatem gry 0.01 - 181d, bez zmian). Dotyczy takze
    karawan kupujacych statek (gra: `CaravansCampaignBehavior.cs:831`, zaplata `ApplyForPartyToCharacter(karawana, null, ...)` - `ChangeShipOwnerAction.cs:27-30`);
  - port kupuje (:46-65): "z niczego" -> platnik = kasa portu, limit = `Spare(port)` - dla AI i gracza (krytyka W3: "z calej kasy" z wersji 1 schodzilo
    ponizej zapasu, a tam regulator dosypuje z niczego).
  - **Bramka AI (krytyka W3):** prefiks `bool` na `ChangeShipOwnerAction.ApplyInternal` (priorytet nizszy niz okno O07): `ApplyByTrade`, wlasciciel - partia AI,
    nowy wlasciciel - osada, `Spare(port) < cena` -> `return false` (cala transakcja pominieta, statek zostaje, bez premii zarzadcy). Prefiks na
    `ConsiderSellingShips` tego nie zrobi - gra losuje port w srodku (Naval: `ShipTradeCampaignBehavior.cs:340-343`, `GetTownToSellShip` :371-374).
    **Wymiana przy zakupie** (`TryPurchasingShipFromTown`, Naval: `:167-171` - najpierw sprzedaz starego statku portowi, potem kupno nowego): prefiks/finalizer
    na `TryPurchasingShipFromTown` otwiera kontekst; gdy bramka odrzuci sprzedaz, odrzuca tez nastepne kupno w tym kontekscie (inaczej druzyna mialaby statek ponad limit).
  - **Gracz:** port placi z nadwyzki; przy kodzie sprawdzic w dekompilacji ekranu portu NavalDLC, skad ekran bierze cene. Jesli wycene da sie obciac - oferta
    najwyzej `Spare`; jesli nie - przy otwarciu ekranu komunikat "The port can pay at most {GOLD} for ships today." i wyplata obcieta do nadwyzki (gracz wie przed sprzedaza).
  - premie perkow zarzadcy (Naval: `ShipTradeCampaignBehavior.cs:395-429`, MerchantPrince i MasterShipwright) - platnik = nadwyzka tego portu.
- Kolizja: na tej samej metodzie prefiks okna O07 (A: `CirculationWindows.cs:576-581`, :1759) - zostaje; po zamianie platnika zdarzenia nie sa
  "z niczego / w nicosc", wiec okno pokazuje trafienia 0.
- **Klucz:** `ShipTradeThroughPort = true` - MCM "Ship Trade Goes Through The Port's Purse" / "Ships bought from or sold to a port are paid into or out of the
  port's purse; a port without spare money does not buy ships."
- **Ksiega:** "statki: sprzedaz osadom i premie zarzadcow" -> 0, "statki: kupno od osad" -> 0.

### E3/12 - Pryzy z bitwy morskiej do portu (181c) - M
- Kontekst w `MapEvent.LootDefeatedPartyShips` (gra: `MapEvent.cs:1750-1818`): prefiks na `DestroyShipAction.Apply` w kontekscie - statek niezabrany przez
  zwyciezcow (:1786-1804, zniszczenie :1803) **tylko gdy gra placi zwyciezcom** (`flag` :1783 - wodz zwyciezcow z bohaterem i sa partie mogace lupic;
  przy `flag = false` gra nikomu nie placi - statek ginie jak dzis, krytyka D11) zamiast zniszczenia idzie do portu ("wybor portu" wyzej, wzgledem rodu wodza
  zwyciezcow); cena = `GetShipTradeValue(statek, Winner.LeaderParty, port)` (jak E3/10; uszkodzony po bitwie statek ma wysoki koszt naprawy - cena <= 0 -> statek ginie);
  port placi z nadwyzki, dzielone miedzy zwyciezcow wedlug wkladu jak gra (:1806-1818), od razu przelewem (wodz - bohater, partia bez wodza - jej kasa).
  Postfiks: `PlunderedGold` kazdego zwyciezcy pomniejszone o dopisek gry za statki (:1817), wiec `CommitGoldChanges` nie placi go drugi raz z niczego.
- Pryz to lup: czesc wodzow do podstawy 1/9 (jak 3.2); do KW - `NoteShipSale` (R16 "bez statkow").
- **Klucz:** `ShipPrizesToPort = true` - MCM "Prize Ships Are Sold To A Port".
- **Log:** sekcja "pryzy" w "Statki (181)": statki, nalezne, zaplacone, zatopione bez kupca (bez placacego zwyciezcy / cena <= 0 / brak portu).
- **Ksiega:** "bitwy" w ZRODLACH spada o czesc statkow (informacja E3/0 "w tym statki" -> 0).

### Podsumowanie kawalkow

| Kawalek | Fala | Paczka | Wylacznik (dom.) | Pozycja ksiegi, ktora znika (d336-457) | Wielkosc |
|---|---|---|---|---|---|
| E3/0 | 1 | 164a-0 | (CirculationLedgerEnabled) | R: RB3 -3.6, poza tickiem -11.8 -> ok. 0 (U8r, portfele) | S |
| E3/1 | 2 | 164a-1 | `TownPaysRewards` | jency 10.7, oblezenia/turnieje 0.4, myto 0.6 | M |
| E3/2 | 2 | 187 | `BkEstateIncomeFromSalesOnly` | majatki 293.8 (+ pasmo -230..-290) | S |
| E3/3 | 2 (z 3.2) | 164a-2 | `CasualtyLootFromPurse` | lup z cial 20.8 (med.), skoki do 10 mln | S |
| E3/4 | 2 | 164a-3 | `CaravanWageToPurse` | zold karawan 143.8 + 6.2 | M |
| E3/5 | 2 | 164a-4 | `UpgradeCostToPurse` | awanse 12.3 (+ gracz) | S |
| E3/6 | 2 | 164a-5 | (`RecruitGoldToSeller`) | werbunek 4.9 | S |
| E3/7 | 2 | 164a-6 | `CaravanCapitalReal` | kapital lordow 18.0, cena BK 2.9, U8 (rozwiazane), U8r | S |
| E3/8 | 2 | 164a-7 | `BkWorkshopTaxToLord`, `BkLordFeesHaveRecipients`, `DeadHeroGoldStays` | podatek 53.5 (para), oplaty 6.2, zgony 2.5 | M |
| E3/9 | 2 | 164a-8 | `CaravanProfitInRealmTown` | (przelew, bez pozycji) | S |
| E3/10 | 3 | 181a | `ShipsToPort`, `ShipSalePortsTried` 3 | statki inne 355.1 | M |
| E3/11 | 3 | 181b | `ShipTradeThroughPort` | sprzedaz 24.6, kupno 8.4 | M |
| E3/12 | 3 | 181c | `ShipPrizesToPort` | czesc "bitew" (pomiar E3/0) | M |
| (E3/8d) | po pomiarze | - | (`UndeadUnpaid`) | zold nieumarlych 8.7 + ich zrodlo | S |

Pieniadz swiata wobec biegu bez kawalkow [S, d336-457]:
- **fala 2:** zamkniete zrodla ok. -60 tys./d (lup z cial 21, kapital DTE 18 - ten i tak znikal bez nazwy, jency 11, myto/oblezenia/turnieje 2, ...) i majatki ok. 0 +-50;
  zamkniete ujscia ok. +180 tys./d (zold karawan 150, awanse 12, oplaty BK 6, werbunek 5, zgony 3); podatek warsztatow - para zamieniona w przelew (ilosc zlota bez zmian);
  z ok. 165 tys./d, ktore trafia do kas miast, regulator gry skasuje ok. 78% (proporcja 25 : 7 regulatora i zaworu, C 5) - razem ok. -20..+30 tys./d
  (zloto swiata dalej ok. +450 tys./d);
- **fala 3:** statki inne -355..-445 z niczego, porty placa ok. 65-85 tys./d (rozdz. 4) z nadwyzki, ktorej ok. 78% i tak skasowalby regulator -> ok. -300..-380 tys./d;
- **razem** z ok. +450..+540 do ok. +100..+250 tys./d. Zloto swiata "stoi" dopiero w etapie 5 (zakupy mieszkancow ok. 560 tys./d, regulator).

---

## 4. Wojsko - rachunek 181 i decyzja

**Rachunek (kontrfakt na `budzet-rodow.csv` biegu 457 dob, `py\pula_wariant.py`; gorna granica spadku - bez skutkow drugiego rzedu).** Wersja 1 liczyla
wariant "port placi zawsze 30%" (`run(0.7)`), a regula placila z puli 25% nadwyzki - krytyka K1 pokazala, ze porty placa wtedy tylko 22% naleznego.
Teraz kazdy wariant jest symulowany: dobowe nadwyzki 53 portow z `kasy-miast.csv` (kasa - cel - tarcza), statek po statku (35 tys. wartosci, C 1.3),
wyplata obniza nadwyzke portu takze w kolejnych dobach (luka maleje x 0.75 na dobe - regulator 25%), wplaty przypisane do rodu i doby.

| Wariant (ost. 28 dob, d430-457) | Porty placa (z 91.3 tys./d naleznego) | Wojsko lordow w wojnie | d365-457 | Glowy z lennem < 5 000 (d364 / d457) | Rody w wojnie z G < 0 |
|---|---:|---:|---:|---:|---:|
| bieg dzis (zloto statkow z niczego) | - | 99.4 tys. | 104.5 tys. | 0 / 1 | 0 |
| v1: pula 25% (regula wersji 1) | 21.9% | 85.9 tys. (-13.6%) | -14.8% | 23 / 25 | 27.7 |
| cala nadwyzka, tylko wlasne krolestwo | 40.6% | 87.8 tys. (-11.7%) | -12.7% | 18 / 25 | 25.2 |
| **regula v2 (wlasne krolestwo, potem nie w wojnie) - dolna granica** (obce porty tylko krolestw bez zadnej wojny) | **72.9%** | **90.4 tys. (-9.0%)** | -10.1% | 16 / 17 | 18.4 |
| **regula v2 - gorna granica** (obce porty wszystkich krolestw) | **90.5%** | **91.3 tys. (-8.2%)** | -9.0% | 12 / 17 | 16.6 |
| v1 "B" (port zawsze placi 30%) - dla porownania | 100% | 91.9 tys. (-7.6%) | -8.2% | 12 / 17 | 15.8 |
| raty (reszta czeka na nadwyzke kolejnych dob) - odrzucone | 82.9% | 91.0 tys. (-8.5%) | -9.2% | 15 / 18 | 19.5 |
| A (port nic nie placi) | 0% | 83.7 tys. (-15.8%) | -16.8% | 29 / 32 | 32.4 |

**Caly etap (krytyka W9) [S]:** do statkow dochodzi lup z cial (E3/3: lordowie w wojnie traca 2/3..100% zlota z cial, rozdzielone wedlug ludzi w partiach,
skoki ponad 60 tys./d uciete): regula v2 + E3/3 = **88.8-90.3 tys. (-9.2..-10.6%)**, glowy 13-19 (d364) / 18 (d457); przy samym wlasnym krolestwie 86.0-86.7 tys.
(-12.8..-13.5%), glowy 22 / 25. Sama fala 2 (bez statkow): -0.3..-0.5%, glowy bez zmian. Nie policzone (brak danych na rod): jency przy pustej nadwyzce
(E3/1, malo - jency 10 tys./d, 83% z 97 miast ma nadwyzke > 0 w d430-457, `kasy-miast.csv`), pryzy (E3/12 - po pomiarze E3/0), po stronie plus zold karawan i awanse w kasach miast
-> zaworem do panow miast (ok. 35 tys./d) i E3/9. Niepewnosc W6: jesli czesc dzisiejszego zlota statkow to podwojny zwrot, wplata od portu (liczona od kwoty
z podwojeniem) jest w rachunku zawyzona - spadek moze byc o ok. 0.5-1 pkt wiekszy; E3/0 to zmierzy przed fala 3.

- Mechanizm (C 4): dzis do zoldu trafia ok. 20% zlota statkow (kiesy panow zamkow 25.2 -> 76.0 mln przy 68.3 mln ze statkow; pulap wiaze w 19% rodo-dob),
  wojsko traci dopiero, gdy kontrfaktyczna kiesa by sie wyczerpala - **spadek rosnie z czasem** (d121-364: -6..-7%). Kredyt wojenny nie pomaga
  (limit wyczerpuje sie w kilka tygodni). Glowni odbiorcy zlota statkow: panowie samych zamkow (d430-457 ok. 5.2-6.0 tys. ludzi mniej), bez lenna 1.5-1.6, miasta 1.4.
- Raty odrzucone: dlug portow rosnie bez konca (7.1 mln na koniec biegu) i to nowy stan w zapisie; lepszy wynik daje szerszy krag kupcow (porty nie w wojnie).
- Progi etapu 2 (E2 rozdz. 1): wojsko 95-115 tys. (podloga 85) i glowy < 5 000 <= 10 w dobach 120 / 364 - **statki lamia oba po roku** (89-91 tys.; 12-19 glow
  w d364), choc nie podloge. E2 rozdz. Z (`:782-783`) dla tego przypadku kazalo wybrac: wyrownanie przed 181 albo jawny spadek za zgoda Jeffa.

**Decyzja projektu:** regula v2 (rozdz. 2 R1, "wybor portu"); statki **w fali 3, z wylacznikami domyslnie OFF do etapu 5** - chyba ze Jeff wybierze (a). Uzasadnienie:
1. Statki rozbitych do portu - [D] 09.10 03:45 nr 11 "TAK"; cena = zwykla cena sprzedazy portowi (0.3 - naprawa) - jedna regula dla gracza i AI bez nowej liczby.
2. [D] 09.10 10:45: wojsko "dynamicznie wedlug tego, czy stac", bez stalego spadku - spadek po 181 nie jest ustawiony, wynika z tego, ze znika zloto z powietrza;
   ale prog wojska i glow z E2 zostal przez Jeffa przyjety, a po roku nie przejdzie.
3. Wyrownanie przed 181 (Z15-5) - rachunek C: +1 tys. ludzi, tyle samo biednych (przesuwa pieniadze miedzy lordami) - nie wystarcza; jedyne wyrownanie tej
   wielkosci to podatek wojenny 184 (32-54 tys./d), placony z kiesy ludu 173 (E2 Z) - etap 5.
4. Cel etapu 3 (R < 10 tys.) nie zalezy od statkow - zloto statkow jest nazwane w ksiedze.
5. Kod i test fali 3 teraz: w etapie 5 wlaczenie to jedno ustawienie, a test 120 dob pokaze dzialanie (wyplaty portow, kasy portow) juz dzis.

**Pytanie kierunkowe do Jeffa (jedno):**
> Zamkniecie zlota ze statkow (Twoje "tak" z 09.10) zabiera lordom AI ok. 300 tys. zl dziennie z powietrza, z ktorych dzis placa czesc zoldu w wojnie.
> Statki kupuja porty (najpierw wlasnego krolestwa, potem neutralne) z pieniedzy, ktore maja ponad zapas kupcow. Poprawiony rachunek na biegu 457 dni:
> po roku wojsko w druzynach lordow w wojnie ok. 89-91 tys. zamiast ok. 99 tys. (ok. -9..-11%, glownie panowie samych zamkow), biednych glow rodow
> ok. 13-19 zamiast 1 (gorna granica). Pierwsza liczba (-7%) byla za optymistyczna. Wyrownac to moze podatek wojenny korony w etapie 5.
> **(a)** statki od razu po tescie i przyjac ten spadek do etapu 5;
> **(b)** statki zrobione i przetestowane teraz, ale wlaczone dopiero razem z podatkiem wojennym w etapie 5 (rada) - do tego czasu zostaje ok. 300 tys. zl
> dziennie z powietrza (nazwane w ksiedze), reszta etapu 3 wchodzi bez zmian.

Po tescie fali 3 powtarzamy kontrfakt (`py\pula_wariant.py`) z faktycznym udzialem wyplat portow ("Statki (181)") i z podwojnym zwrotem zmierzonym w E3/0;
pelny skutek sprawdzi test roczny etapu 5.

---

## 5. Test

**Biegi** (`Armoury.json` autotestu ustawia wylaczniki; kazda fala - nowa kampania + zapis z doby 362 (9 dob)):
1. **Fala 1:** nowa kampania 40 dob, E3/0 czynny (gra bez zmian) + zapis 9 dob.
2. **Fala 2 - bazowy 120 dob:** DLL fali 2, nowe wylaczniki OFF, E3/0 czynny - podstawa progow "wobec bazy". Baza = e2c + 3.2 (E3/3 tylko z 3.2).
3. **Fala 2 - etap 120 dob:** wylaczniki fali 2 ON. Progi = doby 93-120 (ostatnie 28) o ile nie napisano inaczej.
4. **Fala 3 - 120 dob:** wylaczniki fali 3 ON (baza = bieg 3).

**Fala 1 (40 dob):**

| Obszar | Prog | Linia logu |
|---|---|---|
| bledy i koszt | 0 bledow Armoury; nowe pozycje (portfele wezwan, wplaty do ujemnego portfela, U8r, drugi zwrot za statki, Inni) codziennie; potkniecia 0; czas doby <= +2% | log, autotest |
| reszta | RB3 \|sr.\| < 500 zl/d; \|R\| srednia 28 dob w dobie 40 < 10 000; gdy nie - plan B (rozdz. 7) przed fala 2 | "Pieniadz swiata (bilans - przyczyny)" |
| pomiar statkow | drugi zwrot tej samej partii, statki w bitwach - liczby podane (bez progu; wejscie do kontrfaktu fali 3) | "Obieg: okna" |
| Inni | kiesy rodow nieumarlych i ich wplywy wedlug zrodla - podane; zrodlo nazwane (warunek E3/8d) | "Szczelnosc (164a)" |

**Fala 2 (120 dob, progi d93-120):**

| Obszar | Prog | Linia logu | Dzis |
|---|---|---|---|
| **reszta ksiegi** | srednia 28 dob \|R\| < 10 000 w dobie 120 i w kazdym oknie 28 dob od doby 60; RB (nasz tick) \|sr.\| < 1 000 | "Pieniadz swiata (bilans - przyczyny)" | -18.9 / -15.1 tys. |
| zamkniete pozycje | kazda < 500 zl/d albo < 1% bazy: majatki BK, jency (bez bohaterow), lup z oblezen, nagrody, myto, lup z cial, kapital lordow (DTE), U8 (rozwiazane), U8r; netto 0 (\|.\| < 1% bazy): awanse (AI i gracz), werbunek ochotnikow, zold karawan notabli, linia "Zold" - "karawany" -> 0 (inne partie bez zmian), U6, U7, podatek warsztatow BK (zniklo u notabli = do panow + do kas miast) | j.w. + "Zold" | tab. rozdz. 1 |
| wyplaty osad | wyplaty ponad nadwyzke 0; zadna osada ponizej zapasu kupcow przez nasze wyplaty | "Szczelnosc (164a)" | - |
| kontrolka okien | probki swiata "rozjazd 0"; okna jency, lup-z-cial, majatki-BK: wywolan > 0, trafien z niczego 0 | "Obieg: okna (kontrolka)" | rozjazd 0 (log:123831) |
| pieniadz swiata | zmiana tempa wobec bazy = - zamkniete zrodla + zamkniete ujscia + zmiana regulatora (wszystko z logu) +-50 tys./d | "Pieniadz swiata", "bilans - przyczyny" | +447 tys./d |
| **wojsko lordow w wojnie** | >= 0.97 x baza; podloga 85 tys.; zadne krolestwo < -10% wobec bazy (poza lista biednych z lore: Zelazne Wyspy, Smocza Skala, Sarnor) | "Skarbce" (suma "wojsko rodow" krolestw z WOJNA) | baza 457 d93-120: 96.4 tys. |
| **glowy < 5 000** | <= 10 w dobie 120 (rody z budzetem, bez dworzan BK i Innych) | "Budzet rodow (166)" | 3 |
| **bankruci / zajecia** | 0 bankrutow; <= 1 nowe zajecie w 120 dobach | "Dlugi" | 0 / 2 |
| dochod panow miast | czesc "ziemia" D panow miast >= 0.97 x baza (E3/8a nie zmienia D) | "Budzet rodow (166)" (czesci D) | - |
| kasy miast | mediana kas miast >= 0.9 x baza; plon kupiony >= 95% | "Kasy miast (169c)" + `kasy-miast.csv`, "Utarg wsi (112)" | - |
| **rody gentry** | glowy < 5 000 = 0; zmiana kies rodzin na 28 dob >= 0; 7 rodow zyjacych z majatku (Bywater, Pryor, Moore, Lychester, Whent, Kellington, Crowsbane) - kiesa > 10 000 | "Wydatki rycerzy (169c)", "Rycerze (179)" | 0; +3.5 tys./d |
| majatki BK | majatki z dochodem w ostatnich 28 dobach, a dzis "0" w oknie - 0 (srednia, W7) | "Majatki BK (187)" | - |
| notable | karawany notabli >= 0.9 x baza; ochotnicy (zloto za ochotnikow) >= 0.9 x baza; sredni wplyw notabli wiejskich 150-300; kiesy notabli bez spadku > 10% | "Obieg: notable i karawany", "Werbunek", "Majatki BK (187)" | 756 karawan; 16 tys. |
| sakiewki karawan | razem <= 3 dni zoldu karawan; sieroty 0 | "Szczelnosc (164a)", "Zold" | - |
| pasmo notabli | w nicosc >= 200 tys./d mniej niz w bazie; dosypka <= +60 tys./d | "Obieg: notable i karawany" | -539 / +84 tys. |
| reszta kas miast | srednia 28 dob < 10 000 | "Przeplywy osad (przyczyny)" | +3.9 tys. |
| bandy | sredni tier, kiesy band, rozbite tabory: <= +20% wobec bazy (lup z cial bandom 0; sakiewki rozbitych karawan dla band - nowe, W13) | "Utarg wsi (112)" (pomiar S16) | tier 2.6 |
| bledy i koszt | 0 bledow Armoury; czas doby <= +5% wobec bazy; zapis 9 dob 0 bledow, 0 napisow > 32 767 B (sakiewki karawan w `arm_menpurse`) | log, autotest | - |

**Fala 3 (120 dob, baza = fala 2, progi d93-120):**

| Obszar | Prog | Linia logu |
|---|---|---|
| zamkniete pozycje | statki inne, sprzedaz osadom, kupno od osad < 500 zl/d; "bitwy - w tym statki" -> 0; kontrolka "statki", "statki-inne": trafien z niczego 0 | "Pieniadz swiata (bilans - przyczyny)", "Obieg: okna" |
| wyplaty portow | **zaplacone >= 70% naleznego** (symulacja reguly v2: 73-91%); wyplaty ponad nadwyzke 0; statki przekazane za 0 zl - 0; zlom podany wedlug przyczyny | "Statki (181)" |
| kasy portow | zaden port ponizej zapasu kupcow przez 181; mediana kas portow >= 0.8 x baza | "Kasy miast (169c)" + `kasy-miast.csv` |
| wojsko | >= 0.97 x baza (120 dob spadku nie pokaze - kiesy pelne) | "Skarbce" |
| **prognoza 181** | kontrfakt po roku (`py\pula_wariant.py` z faktycznym udzialem wyplat i podwojnym zwrotem z E3/0): **>= 0.90 x bieg bez 181** i >= 85 tys.; glowy < 5 000 w d364 - liczba podana Jeffowi | `budzet-rodow.csv` + "Statki (181)" |
| bledy i koszt | 0 bledow; czas doby <= +3% wobec bazy; zapis 9 dob 0 bledow | log, autotest |

Progi "zaplacone >= 70%" i "prognoza >= 0.90" sa z tej samej symulacji (70% wyplat rownomiernie -> 89.7 tys. = 0.90 x 99.4; krytyka K1: w wersji 1 byly sprzeczne).

**Co czytac w logu (kolejnosc):** "Szczelnosc (164a)", "Majatki BK (187)", "Statki (181)" (nowe) -> "Pieniadz swiata (bilans - przyczyny)" i "Pieniadz swiata"
-> "Obieg: okna (kontrolka)" -> "Obieg: notable i karawany", "Zold", "Sakiewka ludzi", "Werbunek" -> "Przeplywy osad", "Kasy miast (169c)" + `kasy-miast.csv`
(porty), "Utarg wsi (112)" -> "Budzet rodow (166)", "Skarbce", "Dlugi", "Rycerze (179)", "Wydatki rycerzy (169c)", "Okupy (178)" (podstawa 1/9) -> "Kiesy rozbitych (3.2)".
Narzedzie: `tools/sprawdz_logi.py --grupa etap3` (z E3/0) wypisuje progi z werdyktem; pierwszy log obejrzec tez z `--surowe`.

---

## 6. Ryzyka i kontrola calosci (zasada 0)

**Regresje** (kto czyta i pisze ten sam stan):
- `GiveGoldAction.ApplyInternal` (E3/1) - kazdy przelew gry; zamiana tylko w otwartym kontekscie i tylko dla oczekiwanego odbiorcy/platnika; kontekst
  zamykany w finalizerze (takze po wyjatku), stos <= 4; licznik "zamian poza kontekstem" = 0 w linii.
- `MenPurse` (E3/3-E3/5, E3/7): nowi posiadacze sakiewek (karawany, ok. 800) - `OnEntered` dotyczy tylko partii lordow i gracza (A: `MenPurse.cs:203-217`),
  `OnLeft` dostaje galaz karawan; `OnPartyDestroyed` dostaje galaz "zwyciezca handlujacy bez bohatera" (W13); zapis w tym samym `arm_menpurse` (SaveText w kawalkach, paczka 161).
- `LevyGold` (E3/6): ten sam postfiks, kwota z migawki kiesy; galaz najemnikow i zwrot konia do kasy karawany (A: `LevyGold.cs:78`) nietkniete.
- `SoldierPay` (E3/4): galaz karawan (A: `SoldierPay.cs:487`) - bez `Hold` (tarcza), `MountedWage` (karawana bez premii konnego) bez zmian; `GarrisonShare` (:491-501) czytany przez E3/5 (tylko odczyt limitu).
- `PopulationLaw.KeptTownLines` (E3/8a) - pomija warsztaty notabli; czytaja go `TownTaxPostfix` (:220-232) i ekrany podatku (te same linie) - przy wylaczonym
  `BkWorkshopTaxToLord` stan sprzed. `ClanIncomeBook.StableD` - wplata podatku jako renta (`RentToday`), nie jako jednorazowe (inaczej D spada).
- `PartyScreenLogic` (E3/5 gracz): `DoneLogic` latany juz przez `Stables` (A: `Stables.cs:1107`) i `GarrisonArmory` (:1096); nasza para tylko czyta kiese gracza
  i dopisuje do sakiewki - kolejnosc bez znaczenia; `UpgradeTroop` - tylko licznik.
- `KillCharacterAction` (E3/8c) - finalizer dziala po calej akcji gry (takze po przekazaniu zlota nastepcy); okno O37 "stan-bohatera" (A: `CirculationWindows.cs:1475-1495`) liczy
  zloto, ktore wyszlo ze swiata - po E3/8c powinno spasc (zamierzone).
- `ChangeShipOwnerAction.ApplyInternal` (E3/11) - prefiks `bool` (bramka AI) i kontekst; okno O07 (prefiks void) zostaje; transfery (`ApplyByTransferring`, E3/10) i lupy nie sa bramkowane.
- Okna 169 na tych samych metodach (wszystkie zostaja, prefiksy void): O02 (:666-721, :1754), O05 (:548-559, :1757), O07 (:576-581, :1759), O08 (:584-590,
  :1761-1762), O09 (:592, :1763), O10 (:1764), O11 (:1765), O14 (:1766, `FlagFin` :487-493), O16 (:783-811, :1768 - `LootFin` dostaje odjecie E3/3), O17 (:1769),
  O33 (:1770, :1811), O36 (:1771), kupno-BK (:1810), tytuly/pasowanie/dwor/dylematy/przeniesienie (:1797-1806), majatki-BK (:1390-1399, :1831), O38 (:1502-1521 - oznaczanie id na poczatku, D9).
- `ClanIncomeBook.OnceToday` (E3/10, E3/12): `TodayShips` odjete tylko od `real`; budzet 166 (zapas wojny z `all`) widzi wplaty od portow - zamierzone.

**Kolizje z istniejacymi latkami i innymi zmianami:**
- **178 / RealisticCaptivity** - sprzedaz lordow przez gracza (A: `Ransom178.cs:353, 495-540`; RC: `FairRansom.cs:115-125, 230`): E3/1 placi osada tylko za
  zolnierzy; czesc bohaterow jak dzis (178 rownowazy u gracza; RC/178 nie uzywaja `GiveGoldAction`, A: `Ransom178.cs:516-560` - RealPayer ich nie zlapie).
  U AI czesc bohaterow (okup przy kupujacym w pokoju z jencem, gra: `SellPrisonersAction.cs:30-45, 60-63`) zostaje z niczego - zmierzona w linii; gdy > 2 tys./d - dopisac do 178.
- **3.2** (`w-toku/e3-32`): nasluch `MobilePartyDestroyed` (`BeatenPurse`), "bitwy - zloto partii bez wodza", U8 - E3/7 biegnie po `BeatenPurse` i przed O38;
  galaz "odd" 3.2 wola E3/7; regula sakiewek (W13) = regula kies 3.2; 1/9 z E3/3 i E3/12 przez `Ransom178.NoteBeatenGold` z 3.2 (E3/3 tylko z 3.2).
- **KW 168** (A: `DebtLadder.cs:185, 204, 401-414`) - statki poza limitem (R16); jency od osad (E3/1) to lup od prawdziwego platnika - wchodza do limitu (jak okupy).
- **Nadwyzka a zawor i regulator:** wyplaty z kas osad w ciagu dnia zmniejszaja nadwyzke, z ktorej nasz tick liczy zawor (2/3 pan, 1/3 korona; zamki A:
  `CastlePurse.cs:191-205`, miasta 114) i z ktorej regulator gry kasuje 25% - w miastach i portach ok. 78% wyplaty to pieniadze regulatora, ok. 22% zaworu panow
  [S, C 5]; w zamkach (regulator nie kasuje) - pieniadze pana, tak jak kazdy wydatek kasy zamku (B-2). Tarcze (zold, dwor) nie wchodza do nadwyzki.
- **Jeden licznik wydatkow na osade i dobe** - jency, statki, pryzy, nagrody, lup z oblezenia tego samego dnia zmniejszaja te sama nadwyzke (kto pierwszy);
  zakupy plonu wsi nietkniete (Z15-8).
- **Budzet 166** - pieniadze do kas miast (zold karawan, awanse) wracaja zaworem do panow miast -> "ziemia" w D; E3/8a - "ziemia" bez zmian (renta zamiast linii modelu);
  E3/9 -> "majatek" w D (A: `ClanBudget.cs:23`); rycerze tylko -1.7 tys./d (majatki poza D); konwoje DTE - doplata rodu do 5 000 i zwrot przy usunieciu (D6).
- **Pasmo notabli** (gra: `NotablePowerManagementBehavior.cs:47-61`) - po E3/2 wplyw notabli wiejskich spada do ok. 150-300 (gra: `DefaultNotablePowerModel.cs:106-110`),
  a wplyw wchodzi do szansy ochotnika BK (BK: `BKVolunteerModel.cs:362`) - prog ochotnikow w tescie. 164c (pasmo na kasach osad) dopiero po E3/2 (etap 5).
- **BattlefieldLaw** (A: `BattlefieldLaw.cs:233-237, 578-588`) - w bitwach osobistych z DTE lupu z cial nie ma; E3/3 tez nie.
- **DTE** - konwoj z kapitalem 0: zold karawan lordow placi rod (gra `DefaultClanFinanceModel.AddPartyExpense`); E3/4 kieruje go do sakiewki; DTE usuwa konwoj
  (DTE: `:1153-1176`) -> E3/7 oddaje reszte glowie rodu.
- **GentryService** (zakupy majatkow rycerzy, A: `GentryService.cs:503-505, 656+`) - inny kierunek (kiesa -> miasto), bez zmian.
- **Nie liczone dwa razy:** zwrot werbunku obejmuje oba rodzaje (B 2.5); E3/3 bierze z sakiewki przed `MenPurse.OnPartyDestroyed`; E3/12 zdejmuje dopisek gry
  z `PlunderedGold`, zanim `CommitGoldChanges` go wyplaci; E3/8a: linia modelu znika dla warsztatow notabli w tej samej chwili, w ktorej powstaje przelew;
  E3/10: drugie wywolanie NavalDLC przy rozwiazaniu (W6) nic nie placi.
- **Martwy kod -> zywy:** nasluch `OnPartyRemoved` (nowy, O(1)); petla E3/10 - do ok. 3 wyplat x 3 statki dziennie x <= 2 x 3 porty + przeglad portow;
  E3/12 tylko w bitwach morskich; bramka E3/11 - przy kazdej sprzedazy AI (rzadkie: 10% szansy na rod i dobe). Zywy -> martwy: okna statki/statki-inne/majatki-BK/
  jency/lup-z-cial przestaja trafiac (zamierzone - kontrolka).

**Spojnosc logiki:** jedna regula na zjawisko (rozdz. 2), **jedna definicja nadwyzki osady** (bez osobnej puli), jedna cena statku (cena sprzedazy portowi gry)
dla AI i gracza i dla wszystkich sciezek "port kupuje", jeden podzial lupu ludzi (1/3 - 2/3) i jedna regula sakiewek/kies rozbitych (3.2 = W13); kwoty
oddawane = to, co naprawde zeszlo platnikowi; kolejnosc w dobie bez zmian (zdarzenia gry w ciagu dnia, nasz tick na koncu); stan dzienny (licznik wydatkow,
migawki bitew, zbiory id) czyszczony w `ClearDay`, wszystkie `Reset` w konstruktorze `ArmouryBehavior` (nowa kampania). Jedyny swiadomy wyjatek: rabat AI 0.01
przy kupnie statku (181d) - niezgodny z "jedna regula", zostaje jawnie (rozdz. 7).

**Cudzy kod (sprawdzone w dekompilacji):** gra 1.4.8 - `GiveGoldAction.cs:9-80`, `ChangeShipOwnerAction.cs:19-80`, `SellPrisonersAction.cs:9-95`, `DestroyPartyAction.cs:9-46`,
`MapEvent.cs:1410-1430, 1659-1708, 1750-1818, 1850-1876`, `MobileParty.cs:425-440, 3516-3534`, `KillCharacterAction.cs:31, 85-98`, `ChangeClanLeaderAction.cs:15-24`,
`TournamentManager.cs:180-192`, `SiegeAftermathCampaignBehavior.cs:123-168`, `RecruitmentCampaignBehavior.cs:606-640`, `PartyUpgraderCampaignBehavior.cs:100-180`,
`PartyScreenLogic.cs:909-930, 1245-1265`, `PartyScreenHelper.cs:614-617`, `PlayerTownVisitCampaignBehavior.cs:590-594`, `CaravanConversationsCampaignBehavior.cs:243-252`,
`CaravanPartyComponent.cs:240-247`, `CaravansCampaignBehavior.cs:800-835`, `DefaultClanFinanceModel.cs:106, 361-378, 403, 628-638, 826-860, 870-920, 953-958`,
`ClanVariablesCampaignBehavior.cs:479-486`, `DefaultSettlementEconomyModel.cs:75-79`, `Settlement.cs:291`, `Town.cs:277`; Naval - `NavalShipDistribution.cs:1-110`,
`NavalDLCShipCostModel.cs:20-75, 162`, `ShipTradeCampaignBehavior.cs:95-215, 331-430`, `ShipProductionCampaignBehavior.cs:67-131, 242-247`; BK - `EstateData.cs:80-193`,
`Estate.cs:128, 301`, `BKEstatesModel.cs:504`, `BKClanFinanceModel.cs:28-95, 236-251`, `BKTaxModel.cs:164-215`, `WorkshopExtensions.cs:10-16`, `CourtGrace.cs:375-382`,
`MoveCourtDecision.cs:101-110`, `BKKnighthoodBehavior.cs:215-240`, `BKLordPropertyBehavior.cs:40-85`, `BKPartyBehavior.cs:941-975`; DTE - `CutTheirSupplyBehavior.cs:971-1040, 1123-1176`;
ROT - `ROTCoreBehavior.DailyTickClan` (`32dec\ROT.cs:46915-46921`), `ROTClanFinanceModel` (`dec\rotfin.cs`).
Do sprawdzenia przy kodzie (wpisane w kawalkach): ekran portu NavalDLC (E3/11, gracz), obciecie wyceny posrednika jencow (E3/1, gracz), latki innych modow na
`GiveGoldAction.ApplyInternal` (E3/1, zrzut przy starcie). Nazwy prywatnych metod NavalDLC/BK/DTE moga zmienic sie z aktualizacja - "BRAK" w linii startowej,
kawalek nieczynny, reszta dziala.

**Gracz:** jency (takze u posrednika) i lup z oblezenia w biednym miescie warte mniej (komunikat); zwrot za statki od portu (cena sprzedazy portowi - jak dzis 30%,
ale minus naprawa), port moze zaplacic mniej albo nikt nie kupi (komunikat); sprzedaz statku w porcie - z nadwyzki portu; jego ludzie traca sakiewke, gdy przegra
(E3/3), i zyskuja z cial wrogow tylko przedmioty (jak dzis); jego awanse z ekranu druzyny - do sakiewki jego ludzi; jego karawany: zold do ich ludzi, kapital
z zaplaty; oplaty BK - do odbiorcow; okno majatku BK pokazuje srednia prawdziwego dochodu, cena kupna majatku nizsza.

**MCM / zapis:** 14 nowych kluczy (fala 2: 9, fala 3: 4 + `ShipSalePortsTried`) - zadnego nie ma w `Armoury.json` Jeffa (dzialaja domyslne; przed wgraniem
`grep` kazdego klucza, CLAUDE.md 7); `ShipSalePortsTried` to liczba projektu ([D] 06.10 nr 2). Przy odpowiedzi (b): `ShipsToPort`, `ShipTradeThroughPort`,
`ShipPrizesToPort` domyslnie false. Stan nowy w zapisie: tylko sakiewki karawan (w istniejacym `arm_menpurse`); srednia majatkow w `LastIncome` BK (bez nowego
pola); licznik wydatkow, migawki i zbiory id sa dzienne (nie w zapisie).

---

## 7. Co odlozone i dlaczego

| Co | Dokad | Dlaczego |
|---|---|---|
| zloto taboru, karawany i bandy dla zwyciezcy (3.2) | osobno (`w-toku/e3-32`) | zadanie; tu tylko kolizje (rozdz. 6) |
| Inni bez zoldu (E3/8d) | po pomiarze E3/0 | zrodlo ich dochodu nieznane - zamknac obie strony naraz (krytyka W12) |
| statki (fala 3) - wlaczenie | etap 5 z 184 (rada, czeka na Jeffa) | spadek wojska -9..-11% i 12-19 biednych glow po roku lamie progi E2 (rozdz. 4) |
| podatek warsztatow lordow w cudzych miastach (para w modelu: wydatek "Workshop taxes" wlasciciela i linia pana miasta) | etap 5 z K6 | obie nogi w rozliczeniach rodow (przelew miedzy rodami liczony przez model, bez nicosci po jednej stronie); kwota mala, niezmierzona osobno |
| pasmo notabli na kasach osad (164c), nowy notabl bez 10 tys., kapital karawan notabli z ich kiesy (B 2a: 105.6 tys./d z niczego) | etap 5 (5.3) | notable i kiesy osad zalezne od kiesy ludu; kapital z kiesy notabla przy dzisiejszym pasmie 4.5-10.5 tys. utnie karawany notabli (prog -10%) - razem z 164c (B 2.2, OB 13 pkt 2) |
| "zakupy" mieszkancow (ok. 560 tys./d z niczego), regulator kas (K6/111'), dosypka trybu 1 | etap 5 (5.1, 5.5, 5.8) | bezpiecznik Z9: siatke zdejmujemy po spelnieniu progu reszty przez 28 dob |
| BK "rynek osady" (`HandleMarketGold`, -82 tys./d netto, rosnie z kasami) | etap 5 z K6 | to blizniak regulatora gry (1% kasy ponad limit BK); zamkniety sam przeniosloby kasowanie na regulator gry (ten sam nadmiar, 25%/d) - nic nie zamyka, a zmienia zawor panow miast; jedno zjawisko = jedno zamkniecie z K6 |
| tarcza zoldu dla sakiewek karawan | etap 5 (z K6) | B 2.1 ryzyko 2: +100-150 tys./d dla panow miast zmienialoby budzet 166 - 164a ma zamknac ksiege, nie zmieniac dochodow |
| wyrownanie panow zamkow Z15-5 (renty 2/1.5/0.25, zawor miast 1/2) | etap 5 (5.7) z 173 | [D] 04:25 E "tak" jako czesc kiesy ludu; rachunek C: przed 181 daje +1 tys. ludzi |
| podatek wojenny korony 184 | etap 5 (5.6) z 173 | placi go kiesa ludu (E2 Z); pokrywa brak zoldu po 181 |
| raty portow za statki | nie | dlug portow rosnie bez konca (7.1 mln w biegu 457), nowy stan w zapisie; szerszy krag portow daje wiecej (rozdz. 4) |
| rabat AI 0.01 przy kupnie statku (181d) i wycena A | etap 5 (z 184 i K6) | niezgodny z "jedna regula", ale bez niego AI nie stac na floty (kupno tylko przy cenie < 20% kiesy, Naval: `ShipTradeCampaignBehavior.cs:129, 150`), a od flot zaleza przeprawy (IslandRoads); wycena A = -15.8% wojska |
| statki z produkcji portu z niczego i kasowanie nadmiaru stoczni; statki ginace bez kupca (zlom) | etap 4 (ksiega sztuk) | to sztuki, nie zloto (Naval: `ShipProductionCampaignBehavior.cs:67-131`; gra: `MobileParty.cs:3528-3531`) |
| jeniec sprzedany w miescie jako mieszczanin, ochotnik ubywa ze wsi (164b) | etap 4 (4.7) | ksiega ludzi |
| Rada BK - pensje od podatku, ktorego nie ma (D-8, ok. 10 tys./d) | PLAN 3.5 | osobna pozycja planu (blad BK) |
| BK niewolnicy, wyposazenie rycerza, wedrowcy BK, ROT (inwazje, zadania), drobne 3.9-3.10 | PLAN 3.4, 3.6, 3.7, 3.9, 3.10 | osobne pozycje planu |
| bohaterowie wracajacy do swiata (Z4: 0.2 tys./d, jednorazowe skoki) | obserwacja | rzadkie; nazwane w ksiedze |
| plan B pomiaru reszty: kontrolka godzinowa (zloto swiata co pelna godzine, ok. 12 ms/dobe) | tylko gdy E3/0 nie potwierdzi tropu DTE | B 4.3 |
| okup krola, rachunek okupow AI z kupujacym w pokoju (czesc bohaterow w `SellPrisonersAction`) | 178, gdy pomiar E3/1 > 2 tys./d | dzis niezmierzone osobno |

---

## 8. Krytyka - co przyjeto/odrzucono

Kazda uwaga sprawdzona w kodzie albo w danych (dowody ponizej). Skrypty: krytyki `py\krytyka\*.py` (powtorzone - te same wyniki), projektu `py\pula_wariant.py`.

**KRYTYCZNE**
- **K1 (rachunek wojska zaklada 100% wyplat portow) - PRZYJETE, z inna poprawka niz w krytyce.** Powtorzone `py\krytyka\pula_statki.py`: pula 25% - porty placa
  19 962 z 91 305 zl/d (21.9%), cala nadwyzka 45.6%; `porty2.py`: srednia nadwyzka d457 84 404, mediana 41 301 (kolumna `kasa_do_celu` to stosunek - The Eyrie 2.65).
  Blad "2.66 mln / 665 tys./d puli" poprawiony w rozdz. 1. Zamiast przyjac -14% zmieniona regula (rozdz. 2 R1, "wybor portu"): cala nadwyzka (= R2, W4) i porty
  krolestw nie w wojnie, gdy wlasne nie maja pieniedzy; symulacja `py\pula_wariant.py` z wyczerpywaniem nadwyzki w kolejnych dobach: porty placa 73-91%, wojsko
  d430-457 90.4-91.3 tys. (-8..-9%), caly etap 88.8-90.3 tys. Rada w pytaniu do Jeffa zmieniona na (b), bo spadek lamie progi E2 (95-115 tys., glowy <= 10 w d364).
  Progi testu (zaplacone >= 70%, prognoza >= 0.90) z tej samej symulacji. Nieprzyjete z poprawki: "limit stoczni i wspolny licznik z jencami" w symulacji - CSV nie ma
  stanu stoczni ani jencow na osade; zapisane jako niepewnosc (kontrfakt po tescie fali 3 z faktycznymi wyplatami).
- **K2 (E3/8a liczy podatek warsztatow drugi raz) - PRZYJETE.** BK: `BKTaxModel.cs:198-205` (linia "Taxes from" = `GetWorkshopTaxes`, :87-95), A: `PopulationLaw.cs:226-231,
  257-261` (linia celowo zostawiona; `RentReplacesTownTax = true` - `Settings.cs:610`, nie ma go w `Armoury.json` Jeffa), gra: `DefaultClanFinanceModel.cs:403`
  (podatek miasta w dochodzie pana). Kwota, ktora naprawde znika: `min(podatek, brutto)` - `ClanVariablesCampaignBehavior.cs:479-486`, `DefaultClanFinanceModel.cs:908-919`.
  Wybrany wariant (b) krytyki: przelew notabl -> pan dokladnie tego, co zniknelo, linia modelu znika dla warsztatow notabli, autonomia -> kasa miasta; D panow bez zmian
  przez `RentToday` (A: `ClanIncomeBook.StableD.cs:383-431` - dzis linia w `wd`). R7 "do kasy osady" odrzucone (spadek D panow ok. 40 tys./d).

**WAZNE**
- **W1 (gracz u posrednika bez platnika) - PRZYJETE.** gra: `PartyScreenHelper.cs:614-617` i `PlayerTownVisitCampaignBehavior.cs:592` (kupujacy null),
  `SellPrisonersAction.cs:12` (`settlement` null dla partii ruchomej). Platnik `Settlement.CurrentSettlement` + komunikat; obciecie wyceny ekranu - do sprawdzenia przy kodzie.
- **W2 (awanse gracza w nicosc) - PRZYJETE.** gra: `PartyUpgraderCampaignBehavior.cs:170` (pomija gracza), `PartyScreenLogic.cs:922, 1258`. Para na `UpgradeTroop`/`DoneLogic`.
- **W3 (port ponizej zapasu) - PRZYJETE CZESCIOWO.** Prawda: regulator dosypuje ponizej celu (gra: `DefaultSettlementEconomyModel.cs:77-78`), `GiveGoldAction` obcina
  do calej kasy (:21-25), prefiks na `ConsiderSellingShips` nie zna portu (Naval: `:340-343, 371-374`), wymiana przy zakupie omija bramke (:167-171) - limit = nadwyzka,
  bramka na `ChangeShipOwnerAction.ApplyInternal`, wymiana odrzucana w calosci. **Odrzucone:** "karawany (`CaravansCampaignBehavior.cs:831`) omijaja bramke" -
  tam karawana KUPUJE statek od portu (`ApplyByTrade(caravan.Party, item)`, port sprzedaje), wiec to nie jest sciezka "port kupuje"; zaplata karawany idzie dzis
  w nicosc (`ChangeShipOwnerAction.cs:27-30`) i E3/11 kieruje ja do kasy portu (dopisane wprost).
- **W4 (dwie ceny i dwie granice "port kupuje statek") - PRZYJETE.** Naval: `NavalDLCShipCostModel.cs:45-59` (sprzedajacy ruchomy, kupujacy osada: x 0.3 - naprawa).
  E3/10 i E3/12: `GetShipTradeValue(statek, sprzedajacy, port)`, granica = nadwyzka; dopisane: cena <= 0 (uszkodzony statek) - zlom.
- **W5 (druzyny rozwiazane dalej traca kiese; U8 zajete) - PRZYJETE.** gra: `DestroyPartyAction.cs:34-41` (niszczyciel null), 3.2: `BeatenPurse.cs:280, 290`,
  A: `MoneyLedger.Obieg.cs:348` (U8). E3/7 obejmuje niszczyciela null i "odd" 3.2; nowa etykieta U8r.
- **W6 (podwojny zwrot za statki) - PRZYJETE.** Naval: `NavalShipDistribution.cs:24-48`, gra: `DestroyPartyAction.cs:40-41` (oba zdarzenia), `RecoverGold` statkow nie zabiera.
  E3/0 mierzy; E3/10 odporny (statki przeniesione przy 1. wywolaniu, zbior id dla zlomu). Uscislenie kierunku: podwojenie zawyza w kontrfakcie wplate od portu,
  wiec prawdziwy spadek jest nieco WIEKSZY (rozdz. 4).
- **W7 (`LastIncome` skacze albo rosnie) - PRZYJETE.** BK: `EstateData.cs:96-98` (`+=`), :181-187 (jedyne zerowanie), `BKEstatesModel.cs:504`. Srednia 28 dob w samym `LastIncome`.
- **W8 (kwota z modelu zamiast pobranej) - PRZYJETE** (takze jako zasada wspolna rozdz. 2). gra: `GiveGoldAction.cs:11-14`, `PartyUpgraderCampaignBehavior.cs:113-120,
  141-150`, `RecruitmentCampaignBehavior.cs:608-628`; A: `LevyGold.cs:35, 40`.
- **W9 (rachunek wojska tylko dla E3/10) - PRZYJETE CZESCIOWO.** Policzone: statki + lup z cial (E3/3) i sam E3/3 (`py\pula_wariant.out`). Nie policzone (brak danych na rod):
  jency przy pustej nadwyzce, pryzy, plus z zaworu panow miast - opisane w rozdz. 4 jako zakres; pelny kontrfakt fali 3 po tescie z faktycznymi przeplywami.
- **W10 (13 kawalkow w jednym tescie) - PRZYJETE.** OB rozdz. 12 (`PROJEKT-EKONOMIA-OBIEG-2026-10-08.md:514`), B rozdz. 5 pkt 1 (`B-drobne.md:181`), CLAUDE.md zasada 2.
  Trzy fale (rozdz. 3).
- **W11 (zly odbiorca czesci oplat BK) - PRZYJETE.** BK: `CourtGrace.cs:375-382`, `MoveCourtDecision.cs:101-110`, `BKKnighthoodBehavior.cs:228-235`; okna A:
  `CirculationWindows.cs:1803-1806` (pasowanie = `CreateClan`, dwor = `AddExpense`).
- **W12 (Inni bez zrodla dochodu) - PRZYJETE.** Sprawdzone dodatkowo: zapomoga ROT (ROT: `32dec\ROT.cs:46915-46921`) zablokowana (A: `KingdomTreasury.cs:547-559`),
  `ROTClanFinanceModel` nic nie dopisuje (`dec\rotfin.cs`) - zrodlo dalej nieznane. E3/8d odlozone, E3/0 mierzy.
- **W13 (sakiewka rozbitej karawany do miasta, kiesa do bandy) - PRZYJETE.** A: `MenPurse.cs:99-117` vs 3.2: `BeatenPurse.cs:300-305`; [D] 09.10 04:55 Q3b.

**DROBNE**
- **D1 (pula 25% w zamkach to pieniadze pana) - PRZYJETE**, i rozwiazane szerzej: pula 25% usunieta w ogole; zamki placa z `CastlePurse.Spendable` (A: `CastlePurse.cs:377-392`;
  regulator zamkow tylko dosypuje - :280-282).
- **D2 (`ApplyForSettlementToCharacter` nie obcina) - PRZYJETE.** gra: `GiveGoldAction.cs:52-55` -> :11-14 (kwota ujemna nie jest obcinana do kasy osady).
- **D3 (priorytet wzgledem O16; 1/3 do kasy partii; KThird) - PRZYJETE.** A: `CirculationWindows.cs:783-806, 1685`; gra: `MapEvent.cs:1866`, `MobileParty.cs:425-433`.
- **D4 (E3/3 bez 3.2 = bez 1/9) - PRZYJETE.** E3/3 tylko z 3.2.
- **D5 (awans zalogi omija limit K1) - PRZYJETE.** A: `SoldierPay.cs:491-501`.
- **D6 (doplata rodu do 5 000 dla konwoju) - PRZYJETE** (log i kontrola 166). gra: `DefaultClanFinanceModel.cs:106, 826-859`.
- **D7 (karawana gracza) - PRZYJETE.** gra: `CaravanConversationsCampaignBehavior.cs:243-252` (najpierw tworzenie, potem zaplata - stad finalizer).
- **D8 (portfel naraz wzywajacy i wezwany) - PRZYJETE.** A: `CrownCallToWar.cs:248-250`, gra: `DefaultClanFinanceModel.cs:630`.
- **D9 (oznaczanie id przed wczesnymi `return`) - PRZYJETE.** A: `CirculationWindows.cs:1504-1507`.
- **D10 (prog "karawany -> 0") - PRZYJETE.** A: `SoldierPay.cs:487`.
- **D11 (pryz przy fladze false) - PRZYJETE**, rozszerzone na E3/10 (zaplata 0 albo cena <= 0 - zlom). gra: `MapEvent.cs:1783, 1799-1803`.
- **D12 (komunikat dla rozbitej) - PRZYJETE.**
- **D13 (R11 nie na 100%) - PRZYJETE.** gra: `KillCharacterAction.cs:98`, `ChangeClanLeaderAction.cs:15-24` (bez nastepcy - `return` przed przekazaniem). E3/8c jako
  finalizer: wszystko, co zmarlemu zostalo po akcji gry, do osady (klucz `DeadHeroGoldStays`).
- **D14 (E3/9 a D-9) - PRZYJETE.** `audyt-2026-10-09\12-DOCHODY-RODOW.md:205` ("najblizsze miasto"); swiadoma zmiana z uzasadnieniem w E3/9.

**Z "Sprawdzone - zgadza sie" krytyki (rozdz. 4) projekt korzysta bez zmian:** RealPayer i obcinanie do kasy osady, zapas = cel regulatora, rozdzielenie czesci bohaterow
w jencach, kolejnosc lupu z cial przed zniszczeniem partii, kolejnosc pryzow wobec 3.2, zbior id w dobie, `RunWeekly` BK od razu, turniej tylko AI, portfele wezwan
tylko z dodatniego, zold karawan notabli bez `GiveGoldAction`, majatki BK bez podwojnej wyplaty modelu, okna 169 z prefiksami void.
