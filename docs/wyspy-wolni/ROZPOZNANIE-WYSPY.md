# ROZPOZNANIE: ZELAZNE WYSPY - PIENIADZE Z RABUNKU (decyzja Jeffa 14.10) - tylko odczyt

Zakres: decyzja Jeffa 14.10 ("Zelazne Wyspy powinny miec kase z rabunkow morskich na zachodnim brzegu, i korsarzy, i sami rabowac ... rabowali, aby miec kase").
Pytania (1)-(5) z zadania + "jak dzis Nocna Straz nie placi zoldu". Bez zmian w kodzie, bez commitow, gra nieuruchamiana, nic nie wgrywane, w folderze gry nic nie zapisane (tylko odczyt plikow).
Znaczniki: **[P]** pomiar z logu / CSV, **[K]** odczyt kodu (nasz, gry, modow - z dekompilacji), **[S]** szacunek albo wniosek (metoda podana), **[L]** lore ASOIAF (ksiazki, opis wlasnymi slowami).
Czego nie da sie ustalic - "nie wiem" + linia diagnozy (rozdz. 8).

**Zrodla.**
- Bieg dwuletni (728 dob): `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log`, CSV w `...\2026-10-14-noc\Logs\2026-10-13_21-14-13\` (`budzet-rodow.csv`, `kiesa-ludu.csv`, `bitwy.log`, `wioski-okregi.csv`, `balans-krolestw.csv`, `plon-wsi.csv`), sesja CS `...\session-2026-10-13_21-14-11.log` (KRONIKA WOJEN).
- Bieg 120 dob: `...\Armoury-2026-10-14_05-22-14.log`, CSV w `Modules\Armoury\Logs\2026-10-14_05-22-14\` (tylko odczyt), sesja `session-2026-10-14_05-22-11.log`.
- Wyciagi dobowe z rozpoznania A3: `...\c36b73ca...\scratchpad\rody2\dw.pkl`, `dwc.pkl` (linie 165, W5, Skarbce; sumy `budzet-rodow.csv`).
- Kod: drzewo `zb\ww` (galaz `w-toku/wyspy-wolni`, 63aaaedf): `ClanBudget.cs`, `ClanIncomeBook*.cs`, `DistrictRaid.cs` (E7/R1), `Devastation.cs`, `ThrallLaw.cs`, `FreeHands.cs`, `WatchLaw.cs`, `CrownGifts.cs`, `MountedWage.cs`, `OutlawLaw.cs`, `ShipsToPort181.cs`, `SiegeRansom.Act.cs`, `KingdomBalance.cs`, `CrownRents.cs`.
  Gra `dec32\cs_full` (`AiMilitaryBehavior`, `DefaultTargetScoreCalculatingModel`, `DefaultMobilePartyAIModel`); NavalDLC `a6dec\naval` (`PiratesCampaignBehavior`, `NavalKingdomPolicyCampaignBehaviour`, `NavalPolicies`, `NavalTargetScoreCalculatingModel`, `NavalDLCRaidModel`);
  BK `e4tools\bkfull` (`VanillaModelTweakPatches.BKTargetScoreTweakPatches`); StrategicCampaignAI145 (`...\7016f733...\scratchpad\w04\scai.cs`); ROT `c36b73ca...\scratchpad\rotdec` (`ROTStorylineWars.cs`).
- Mapa: `Modules\ROT-Map\ModuleData\settlements.xml` (nazwy, kultury, polozenie), `ROT-Content\ModuleData\spclans.xml` i `NavalDLC\ModuleData\clans.xml` (bandy).

Doba = dzien gry - 108836 + 1. Kwartaly po 91 dob jak w `ROZPOZNANIE-RODY.md`: Q1 d2-91 ... Q4 d274-364 (rok 1), Q5 d365-455, Q6 d456-546 (od d460 zima), Q7 d547-637, Q8 d638-728. Kwoty: tys. zl na dobe (srednia kwartalu), chyba ze napisano inaczej.
Id: krolestwo `sturgia` = Zelazne Wyspy, `vlandia` = Krolewska Przystan (Baratheonowie z KP; ma Zachod), kultura regionu `vlandia` = Zachod (Westerlands), `battania` = Polnoc, `freefolk` = Wolni Ludzie [P linia "KingdomBalance (175.0): krolestwa id=nazwa"].

---

## 0. Dla Jeffa - osiem zdan

1. Dzis Wyspy nie zyja z rabunku: rody maja ok. 20 tys. zl dziennie, z tego ziemia 2 tys., lup, okupy i statki razem najwyzej 2.7 tys.; reszte daja korona (renty, doplata 6 tys.), pobor pana z wlasnego ludu (8 tys.) i Dorzecze, ktore placi im za wojne z Krolewska Przystania (ok. 4.6 tys. dziennie w roku 2); zold kosztuje 31 tys.
2. Rabunek wsi prawie nic nie daje - nikomu na swiecie: przez dwa lata wszyscy najezdzcy razem zabrali wsiom 61 tys. zl i towar za 3 tys. zl (calosc, nie dziennie); Wyspy z tego ok. 1 tys. zl, bo wies na mapie to cala okolica (na Zachodzie mediana 96 tys. ludzi), a druzyna 140 ludzi w jednym najezdzie dotyka kilkuset i lup liczy sie tylko od nich.
3. Wyspy rabowaly malo i tylko Zachod: 47 najazdow z walka w dwa lata (Banefort, Turnia, Faircastle na Fair Isle, Kayce), wszystkie w dobach 182-405; potem zaden - ani Zachod, ani Polnoc, choc fabula ROT kaze im od d434 "lupic Polnoc"; Seagard, Wyspy Tarczowe i Reach - nigdy (z nimi nie bylo wojny).
4. Gra pozwala rabowac tylko wroga w zasiegu (ok. 5 odleglosci miedzy sasiednimi miastami), tylko pojedynczej druzynie (nie armii) i tylko z zapasem jedzenia na 8 dni; wojny fabularne Wysp to Dolina (po drugiej stronie kontynentu, do d506) i od d434 Polnoc (w zasiegu tylko Flint's Finger), a Zachod byl wrogiem tylko wtedy, gdy Dorzecze ich wynajelo.
5. Na morzu Wyspy sa ofiara, nie lowca: stracily 20 wlasnych karawan (9 zatopionych przez flote Zachodu), same zdobyly 6, wygraly 3 bitwy morskie w dwa lata; okupow zaplacily 139 tys. zl, nie dostaly zadnego.
6. Korsarze to w grze bandy NavalDLC: "Sea Raiders" (plywaja kolo Wyspy Niedzwiedziej, Flint's Finger, nawet pod Pyke i Old Wyk) i "Stepstone Pirates" na poludniu - wrogowie wszystkich; kaperstwa gra nie zna, jedyne gotowe furtki to paser najblizszego miasta (dzialka zostaje w kasie miasta) i polityka NavalDLC "Amnestie dla piratow" (piraci oddaja statki i czesc ludzi do zalogi).
7. Kod pozwala popchnac AI do najazdow bez lamania jej - dopiskiem do oceny celu, tak jak dzis rozejm po okupie oblezenia zeruje ocene oblezenia, a zakaz rabunku Strazy - ocene najazdu - ale tylko miedzy celami, ktore AI i tak rozwaza (wrog, zasieg, jedzenie, druzyna); najazdu na kogos, z kim nie ma wojny, nie da sie dodac bez lamania pokoju.
8. Zeby Wyspy pokryly z lupu brak 16-24 tys. zl dziennie, lup musialby byc ok. 200-300 razy wiekszy niz dzisiejszy lup najazdow calego swiata (ok. 0.07-0.09 tys. zl dziennie) - to wymaga nowego przeplywu z platnikiem (lud i kasy wybrzeza, karawany, okupy), nie podkrecenia liczb; lore daje gotowe nazwy: "zelazna cena", thralle (juz sa - 26 tys. na Wyspach), Stary Obyczaj tylko w wojnie.

---

## 1. Skad Wyspy maja pieniadze i na co ida - kwartalami [P]

Zrodlo: `budzet-rodow.csv` (sumy rodow AI krolestwa "Iron Islands", skrypt `csvk.py`), linia 165 "na krolestwo" (`dw.pkl` z `rody2\x1_log.py`), `kiesa-ludu.csv`, linie "Okupy (178): AI-AI".
Rodow AI 33 (rok 2), z tego **10 z lennem** (Greyjoy, Harlaw, Goodbrother, Botley, Blacktyde, Merlyn, Farwynd, Drumm, Orkwood, Sunderly) i **23 bez lenna** (21 rodow drobnej szlachty BK "...oving" + dworzanie BK) - te 23 nie maja druzyn (0.00-0.03 tys. ludzi) ani zoldu; caly zold i 90% wplywow niosa rody z lennem [P].
Kolumny D169 to srednie 28 dob (czesci "D stalego" i "poza D" z 169c) - suma czesci = wplyw doby [K `ClanIncomeBook.StableD.cs` naglowek].

### 1.1 Rody Wysp (tys. zl/d)

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| doby wojny | 68/89 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | | |
| **wplyw doby razem (D169)** | 23.6 | 16.1 | 19.3 | 23.8 | 22.6 | 10.1 | 23.2 | 22.4 | 20.7 | **19.6** |
| - ziemia (podatek wsi BK, renta wsi, zawor miast i zamkow) | 7.8 | 1.5 | 1.9 | 2.3 | 1.9 | 1.6 | 2.2 | 2.0 | 3.4 | 1.9 |
| - korona (renty 180, zwrot 70%, polityki) | 9.6 | 6.9 | 7.3 | 9.8 | 9.0 | 0.9 | 1.6 | 6.7 | 8.4 | 4.5 |
| -- w tym renty 180 | 4.1 | 4.9 | 4.8 | 8.8 | 6.5 | 0.1 | 1.6 | 5.9 | 5.7 | 3.5 |
| -- w tym zwrot 70% zoldu | 5.0 | 2.1 | 2.7 | 1.1 | 1.5 | 0.4 | 0.9 | 1.0 | 2.7 | 0.9 |
| - "wlasne" (wlasne pieniadze rodu wracajace zaworem z kas wlasnych miast) | 2.4 | 1.6 | 3.3 | 5.7 | 6.4 | 3.7 | 3.9 | 5.5 | 3.3 | 4.9 |
| - **jednorazowe** (sprzedany lup, lup bitew A6, okupy, statki od portow, trzecia) | 2.8 | 1.6 | 4.3 | 1.8 | 2.2 | 2.8 | 3.8 | 2.0 | 2.6 | **2.7** |
| - inne z modelu (glownie wezwania do wojny przyjete, majatki BK) | 0.1 | 2.8 | 0.9 | 2.8 | 1.6 | 0.3 | 11.1 | 5.3 | 1.6 | 4.6 |
| - przelewy w rodzie (m.in. 10% kiesy karawan do glowy) | 0.0 | 0.3 | 0.2 | 0.2 | 0.3 | 0.1 | 0.0 | 0.2 | 0.2 | 0.2 |
| - majatek (warsztaty, zysk karawan) / kontrakt | 0 / 0 | 0 / 0 | 0 / 0.3 | 0 / 0.1 | 0 / 0.2 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 |
| poza D: doplata korony W5 (+W5b) | 6.9 | 19.6 | 10.5 | 9.2 | 8.5 | 4.1 | 4.0 | 7.0 | 11.5 | 5.9 |
| poza D: stawka wojenna pana W7 (z wlasnego ludu) | 0.1 | 3.4 | 9.8 | 11.4 | 11.1 | 8.4 | 5.2 | 6.1 | 6.2 | 7.7 |
| **zold druzyn** | 14.4 | 24.3 | 24.8 | 23.5 | 23.4 | 15.1 | 11.3 | 15.2 | 21.8 | 16.2 |
| **zold zalog** | 9.3 | 13.0 | 13.5 | 14.7 | 15.6 | 14.3 | 13.9 | 15.7 | 12.6 | 14.9 |
| zold karawan | 1.8 | 1.8 | 1.1 | 1.0 | 0.7 | 0.4 | 0.4 | 0.4 | 1.4 | 0.5 |
| zold przyciety (brak w kiesie) | 0.0 | 1.3 | 3.2 | 0.9 | 2.1 | 10.0 | 3.7 | 3.5 | 1.3 | 4.8 |
| ludzie w druzynach / zalogach (tys.) | 3.2 / 3.4 | 5.1 / 5.1 | 4.6 / 5.1 | 3.8 / 5.1 | 3.6 / 5.1 | 2.2 / 4.9 | 1.8 / 4.6 | 2.6 / 5.1 | | |
| kiesy rodow G (mln) | 2.67 | 1.34 | 1.09 | 1.09 | 1.18 | 0.89 | 0.98 | 1.07 | | |
| okupy zaplacone / otrzymane (tys. zl w kwartale) | 0 / 0 | 0 / 0 | 139 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 139 / 0 | 0 / 0 |

- Okupy: 6 panow Wysp wykupionych w Q3 (bitwy z Zachodem) za 139 124 zl (Harras Harlaw, Denys Drumm, Greydon Goodbrother 33 248, Alyn Orkwood, Wex Sunderly ...), zaden jeniec Wysp nie przyniosl okupu [P linie "Okupy (178): AI-AI"].
- "Inne z modelu" w Q7-Q8 to w zdecydowanej wiekszosci zaplaty Dorzecza: 7 porozumien "Riverlands wzywa Iron Islands przeciw House Baratheon of King's Landing" po 5 686-6 203 zl/d przez do 182 dob [P linie 168]; rachunek dni wojny pod porozumieniem daje ok. 0.8 mln w roku 1 i 1.7 mln w roku 2 = 2.1 i 4.7 tys. zl/d [S], zgodne z kolumna (1.6 / 4.6) [P]. Wezwanie placi krolestwo wzywajace z wplywow dnia [K `CrownCallToWar.cs`, rozdz. 5.2 ROZPOZNANIE-RODY].
- Wplyw "wlasne" to nie dochod: to zold zalogi, sprzet i wydatki ludzi rodu wplacone do kasy wlasnej twierdzy, ktore wracaja zaworem [K 169c, E5/A2b]. Prawdziwy dochod z zewnatrz w roku 2: ziemia 1.9 + korona 4.5 + jednorazowe 2.7 + wezwania i inne 4.6 = ok. 13.7 tys. zl/d [S z P].
- W5 i W7 sa poza D (ksiega dochodu ich nie widzi, ida "znaczone" na zold) [K `KingdomTreasury.NeedAid.cs:41`, `KingdomTreasury.LordLevy.cs:27`].

### 1.2 Korona Wysp (tys. zl/d)

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| wplywy dnia (165) | 10.4 | 22.8 | 17.6 | 19.2 | 18.6 | 12.2 | 12.3 | 14.6 | 17.5 | 14.4 |
| - danina z oszczednosci ludu (P1) | 2.7 | 16.3 | 10.2 | 9.1 | 8.6 | 5.7 | 5.2 | 7.0 | 9.6 | 6.6 |
| - reszta (powinnosci wasali, clo, czesc zaworu miast i zamkow, 1/9 trzecich) | 7.7 | 6.4 | 7.4 | 10.0 | 10.0 | 6.5 | 7.0 | 7.5 | 7.9 | 7.8 |
| 1/360 zapasu skarbca | 1.1 | 0.2 | 0.0 | 0 | 0 | 0 | 0 | 0 | 0.3 | 0 |
| wydano: doplata W5 | 2.4 | 16.0 | 10.2 | 9.1 | 8.5 | 4.1 | 4.0 | 7.0 | 9.4 | 5.9 |
| wydano: renty 180 | 4.1 | 4.9 | 4.8 | 8.8 | 6.5 | 0.1 | 1.6 | 5.9 | 5.7 | 3.5 |
| wydano: wezwania (Dorzeczu przeciw Polnocy, d435) | 0.0 | 0.0 | 0.0 | 0.0 | 1.8 | 7.7 | 5.8 | 0.0 | 0.0 | 3.8 |
| wydano: zwrot 70% / kontrakty najemnikow | 5.0 / 0 | 2.1 / 0 | 2.7 / 0 | 1.1 / 0.1 | 1.5 / 0.3 | 0.4 / 0 | 0.9 / 0 | 1.0 / 0.8 | | |
| skarbiec na koniec kwartalu (mln) | 0.70 | 0.51 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | | |

Korona Wysp w roku 2 dostaje od Dorzecza za wojne z Krolewska Przystania (do rodow, przez portfel wezwan) ok. 4.7 tys. zl/d [S] i placi Dorzeczu za wojne z Polnoca 3.8 tys. zl/d (1.38 mln za porozumienie d435) [P] - netto pieniadze wojny Wysp z sojusznikami to ok. +1 tys. zl/d [S].

### 1.3 Lud Wysp (10 miast i zamkow; tys. zl/d, kiesy w mln) [P `kiesa-ludu.csv`, krolestwo "Iron Islands"]

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| kiesy ludu (W+R+P, mln) | 0.54 | 0.76 | 0.41 | 0.37 | 0.34 | 0.23 | 0.21 | 0.28 |
| dochod ludu | 14.1 | 17.7 | 17.3 | 20.2 | 19.6 | 10.4 | 12.1 | 14.3 |
| kasy osad (mln) | 0.22 | 0.20 | 0.26 | 0.35 | 0.34 | 0.26 | 0.29 | 0.30 |

Wsie Wysp: 28 wsi po ok. 14.6-16.7 tys. ludzi (ok. 450 tys.), w tym 4 kopalnie zelaza (Harridan Hall, Kenning, Saltshore, Shatterstone), 2 kopalnie srebra (Nagga's Cradle, Sunderly), sol (Vorfjall), 6 wsi rybackich, 7 zbozowych [P `plon-wsi.csv`, kultura startu `sturgia`].

### 1.4 Wyspy na tle swiata, rok 2 (tys. zl/d) [P `dwc.pkl`]

| krolestwo | wplyw rodow | ziemia | korona | jednorazowe (lup, okupy, statki) | zold druzyn | zold zalog | rodow |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dorzecze | 185.3 | 12.5 | 83.5 | **60.9** | 45.0 | 14.0 | 15 |
| Krolewska Przystan (z Zachodem) | 175.3 | 15.5 | 79.4 | **61.5** | 54.7 | 18.4 | 29 |
| Polnoc | 159.6 | 15.9 | 100.9 | 19.2 | 74.5 | 22.4 | 26 |
| Dothrakowie | 85.9 | 4.4 | 55.2 | 5.7 | 16.5 | 5.6 | 19 |
| **Zelazne Wyspy** | **19.6** | 1.9 | 4.5 | **2.7** | 16.2 | **14.9** | 33 |
| Wolni Ludzie | 5.6 | 1.0 | 2.4 | 0.6 | 5.8 | 5.5 | 11 |

Swiat razem: jednorazowe ok. 304 tys. zl/d (linia "D staly (169c)", d728) - Wyspy maja z tego 0.9% przy 3-4% ludzi w druzynach swiata [P `balans-krolestw.csv`]. Wojna "placi" dzis krolestwom ladowym, ktore bija sie z lordami (okupy, lup bitew), nie najezdzcom [S].

---

## 2. Co Wyspy juz robia "zbojecko" [P]

Zrodlo: `bitwy.log`, linie "Bitwa: B175" (typ, kontekst, region, strony, wynik, osada) - skrypt `bitwy.py`. **Uwaga**: B175 zapisuje bitwe tylko, gdy byla walka albo wynik [K `KingdomBalance.OnBattle`: `la + ld == 0 && brak zwyciezcy -> return`] - najazd na wies bez obroncow nie ma wiersza; liczby to dolna granica liczby najazdow. Druga miara: zmiany `ostatni_najazd` w `wioski-okregi.csv` (wszystkie najazdy, bez napastnika).

### 2.1 Najazdy Wysp (A = Wyspy, typ Raid)

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| najazdy z walka (ladem, kontekst Village) | 0 | 0 | 23 | 1 | 11 | 0 | 0 | 0 | 24 | 11 |
| najazdy z morza (kontekst NavalRaid) | 0 | 1 | 5 | 2 | 4 | 0 | 0 | 0 | 8 | 4 |
| - wygrane / bez rozstrzygniecia | 0/0 | 0/1 | 26/2 | 3/0 | 13/2 | 0/0 | 0/0 | 0/0 | 29/3 | 13/2 |
| udzial Wysp w najazdach swiata (z walka) | | | | | | | | | 1.7% (32 z 1 881) | 1.35% (15 z 1 113) |

- **Gdzie**: wylacznie Zachod (kultura `vlandia`, wsie krolestwa Krolewskiej Przystani): Banefort 14, Turnia (The Crag) 12, Faircastle (Fair Isle) 11, Kayce 10 [P]. Polnoc, Dorzecze (Seagard), Reach (Wyspy Tarczowe, Oldtown, Arbor) - **0** [P].
- Druzyna najezdzcy srednio 143 ludzi; w 47 najazdach zabili lub rozbili 1 781 obroncow (milicja, tabory), stracili 380 [P].
- Te same wsie najezdzala tez Polnoc (Banefort 22, Turnia 6, Casterly Rock 5 - Polnoc byla w wojnie z KP) i Dorzecze (Ashemark 9) [P].
- Najazdy na Wyspy: Polnoc 2 razy (Q7, raz z morza) [P].
- Najwiecej najazdow w roku 2 maja KP 172, Dorzecze 118, Polnoc 135 [P].

### 2.2 Wojny Wysp - kiedy w ogole moga rabowac [P KRONIKA WOJEN + "Wojna i pokoj (E7/0)"]

| para | doby | przyczyna | zasieg (Pyke -> najblizsze osady wroga, linia prosta, jednostki mapy) |
|---|---|---|---|
| Dolina - Wyspy | d23-506 (pokoj d413 i ta sama doba wznowiona przez ROT; od d507 pokoj) | fabula ROT `ROTStorylineWars.EnforceWars` (Vale vs Iron Islands, 5-100) | 298-545 (Dolina po drugiej stronie kontynentu) |
| Wyspy - Krolewska Przystan | d131-223, 253-255, 290-295, 331-423, 438-507, 558-651, 666-728 | **CausedByCallToWarAgreement** - Dorzecze wzywa i placi | Banefort 43, Turnia 75, Faircastle 83, Kayce 122, Lannisport 152 |
| Wyspy - Polnoc | d434-728 | fabula ROT: "Balon Greyjoy orders his Ironborn to reave the North" (StartDay 100, EndDay 235 w kodzie ROT) [K `ROTStorylineWars.cs:86`] | Flint's Finger 170, Barrowton 273, Torrhen's Square 332, Deepwood Motte 444, Wyspa Niedzwiedzia 501 |
| Dorzecze, Reach | nigdy w wojnie | (Dorzecze to sojusznik) | Seagard 175, Wyspy Tarczowe 288-297, Oldtown 409 |

- Kazdy z 47 najazdow przypada na doby wojny z KP (serie d182-222, d254, d341-405) [P]; **po d405 zaden**, choc w wojnie z KP byly jeszcze trzy okresy (d438-507, 558-651, 666-728 - razem ok. 225 dob), a z Polnoca 295 dob [P]. Przyczyny nie znam (rozdz. 8 pkt 1).
- Bieg 120 dob (nowa kampania, 05-22-14): Wyspy w wojnie tylko z Dolina (od d23) - **0 najazdow, 0 bitew Wysp** [P]; w d93-121 doplata W5 juz 16.9 tys. zl/d, druzyny 5.1 tys. ludzi [P `budzet-rodow.csv` tego biegu].

### 2.3 Bitwy morskie i karawany Wysp (2 lata) [P]

| | liczba | wynik |
|---|---:|---|
| karawana Wysp atakowana przez lorda KP na morzu | 9 | 9 przegranych (Q2 3, Q5 2, Q6 1, Q7 2, Q8 1) |
| karawana Wysp atakowana na ladzie (KP 6, Polnoc 3, Dolina 2) | 11 | 11 przegranych |
| lord Wysp atakowany na morzu przez lorda KP | 1 | przegrana |
| lord Wysp atakuje na morzu: karawane Polnocy / lorda Polnocy | 2 / 1 | 3 wygrane (Q6-Q7) |
| druzyna Wysp atakuje karawane na ladzie (KP 3, Dolina 1) | 4 | 4 wygrane |
| bitwy lordow Wysp z lordami (pole) | 12 | 4 wygrane [P `balans-krolestw.csv`] |
| szturmy i oblezenia Wysp | 0 | 10 twierdz od startu do konca bez zmian |

Lodzie rybackie NavalDLC: 786 nowych lodzi (15.7 tys. ludzi) na swiecie w 2 lata [P linia "Partie (E4/0)"], konwoje BK 13 067 - z Wyspami w B175 brak (B175 nie wpisuje lodzi osobnym rodzajem - "inna", 4 bitwy "lord Wysp vs inna" na Zachodzie) [P / K `KingdomBalance.Kind`].

### 2.4 Ile zlota daje dzis najazd - swiat i Wyspy

Linia "Rabunek osada po osadzie (E7/R1)" (sumy dobowe, skrypt `raidloot.py`) [P]:

| swiat | rok 1 | rok 2 |
|---|---:|---:|
| sesji rabunku AI zakonczonych | 1 302 | 776 |
| pelnych krokow (osad spalonych) | 266 | 198 |
| **zloto z kies wsi do wodzow (zl za caly rok)** | **37 665** | **23 446** |
| towar z magazynow wsi (zl za caly rok) | 2 109 | 919 |
| jedzenie ze spichlerzy (szt.) | 7 804 | 7 466 |
| pojmani do lochu najezdzcow z niewola (ludzie) | 3 259 | 1 187 |

- To ok. **0.1 tys. zl dziennie dla calego swiata** [P]. Udzial Wysp wedlug udzialu w najazdach z walka (1.7% / 1.35%): ok. 0.6 tys. zl w roku 1 i 0.3 tys. zl w roku 2 - **calosc, nie dziennie** [S].
- Dlaczego tak malo [K `DistrictRaid.cs` naglowek E7/R1]: wies na mapie to okreg - mediana ludzi wsi: Zachod 96 tys. (33 wsie), Polnoc 46 tys., Dorzecze 69 tys., Reach 157 tys., Wyspy 16 tys. [P `plon-wsi.csv`]; lup = s x kiesa wsi i s x magazyn wsi, gdzie s = trafieni w sesji / ludzie w domu. AI robi jeden krok na sesje i konczy; typowa sesja trafia 80-500 ludzi (linie E7/R1) -> na Zachodzie s ok. 0.1-0.5%, a kiesa wsi ma mediane 8 675 zl (571 wsi, razem 7.7 mln - linia "Przeplywy osad (kiesy wsi)" d168) [P]. Do tego wiekszosc sesji jest przerywana (np. d168: 2 z 2, d665: 1 z 1) [P]. Gra dawala dawniej zloto "za hearth" i towar "z produkcji" z niczego - E7/R1 to wycial (obieg zamkniety) [K].
- Ta sama regula obejmuje gracza (E7/R2: lup kroku = trafieni kroku / ludzie w domu) [K] - gracz jako wasal Wysp dostalby tak samo malo.
- "Lup w D" najezdzcow juz istnieje: `ClanBudget.Raiders = { "sturgia", "khuzait", "freefolk" }` - jednorazowe wchodza tym krolestwom do D pulapu zoldu (wyjatek lore W-1), takze graczowi (`PlayerCaps`) [K `ClanBudget.cs:55, 369, 434`]. Przy 2.7 tys. zl/d jednorazowych to podnosi pulap Wysp o kilka procent [S].

### 2.5 Pryzy morskie i statki (181) - swiat [P linia "Statki (181)", skrypt `statki.py`]

| mln zl za rok | rok 1 | rok 2 |
|---|---:|---:|
| porty placa rodom za statki rozbitych druzyn (1) | 29.0 | 29.6 |
| porty kupuja statki od lordow i karawan (2) | 5.8 | 5.2 |
| **pryzy: porty placa zwyciezcom bitew morskich za zatopione statki (4)** | **4.2** (872 statki) | **4.4** (990 statkow) |

Pryzy to dzis jedyny "morski lup" o znaczacej skali (ok. 12 tys. zl/d swiata, ok. 4.4 tys. zl na statek) [P]; placi kasa portu (181) [K `ShipsToPort181.cs`]. Wyspy wygraly 3 bitwy morskie w 2 lata [P] - ich czesc pryz jest mala; dokladnej kwoty nie znam (rozdz. 8 pkt 3).

### 2.6 Niewolni (thralle) [P linia "Niewolni (E4/9)"]

Stan na Wyspach: d2 26 993 -> d364 26 089 -> d728 25 684 (-5%: smierc 5%/rok wieksza niz doplyw ze sprzedazy jencow); na swiecie sprzedanych w niewole w 2 lata 19 308, glownie w Essos. Prog wolnych rak Wysp 40% (thralle pracuja, wiecej mezczyzn moze isc na rabunek) [P linia "Wolne rece (E4/7)", K `FreeHands.cs:157`].

---

## 3. Korsarze - kto to w grze i czy Wyspy moga z nich ciagnac

| | co jest | znacznik |
|---|---|---|
| bandy morskie | `northern_pirates` "Sea Raiders" (kultura looters, NavalDLC + ROT spclans), `southern_pirates` "Stepstone Pirates" (ROT; w NavalDLC "Corsairs") | [K spclans.xml, clans.xml] |
| bandy "Pirates" ladem | `sea_raiders` (ROT, "Pirates", kryjowka nadmorska, bez statkow) - bija sie na Polnocy kolo Hornwood, Barrowton, Dreadfort, Ramsgate | [K, P B175] |
| skad sie biora | NavalDLC `PiratesCampaignBehavior`: banda morska = klan bandycki z kultura bez osad i z mozliwoscia zeglugi; strefy patrolu z punktow mapy morskiej o nazwie klanu; partia ma zablokowany lad (`SetLandNavigationAccess(false)`); liczba do `BanditDensityModel` | [K PiratesCampaignBehavior.cs:425-560] |
| nasze prawa band | narodziny pirata tylko z puli wyrzutkow (`OutlawLaw.GatePirate`), bez zlota i jedzenia z niczego na start | [K OutlawLaw.cs:1452, 2389-2417] |
| gdzie dzialaja | Sea Raiders: Mormont Keep (Wyspa Niedzwiedzia) 15 bitew, The Rills, Flint's Finger, Kayce, Pyke, Old Wyk, Braavos ...; napadaja karawany i tabory wsi Polnocy (rok 2: 14 taborow i 11 karawan Polnocy) | [P B175 - tylko bitwy z udzialem Polnocy albo krolestw, inne niewidoczne] |
| Wyspy vs piraci | 0 bitew w B175 | [P] |
| sojusz z banda | gra nie zna: klan bandycki jest w wojnie ze wszystkimi; brak kaperstwa | [K] |
| polityki NavalDLC | "Amnesties for Pirates" (`KingsPardonForPirates`): 5%/d szansy, ze banda morska w zasiegu 50 od portu krolestwa poddaje sie - statki do stoczni, 25% ludzi do zalogi (do budzetu zalogi), -5 bezpieczenstwa; "Writs of Reprisal" (`RaidersSpoils`): +5 wplywu za udany najazd, -1 bezpieczenstwa miasta na kazdego lorda w miescie; "Royal Ransom Claim": 15% okupow rodow do krola; "Kings Tithe on Keels": 15% ceny statku sprzedanego przez osade do krola | [K NavalKingdomPolicyCampaignBehaviour.cs, NavalPolicies.cs:176-180] - czy Wyspy je maja: nie wiem |
| paser | bandy z ladunkiem sprzedaja lup najblizszemu miastu w promieniu 200 (dzialka bandy 0.50 -> 0.25), reszta (dzialka pasera) zostaje w kasie miasta: swiat ok. 3.9 tys. zl/d do band, 8 tys. zl/d do kas miast (d728) | [P linia "Paser", K OutlawLaw] |
| "kaperstwo" w praktyce | Wyspy sa dzis wynajmowane jak korsarze: Dorzecze placi 5.7-6.2 tys. zl/d za wojne z KP (7 porozumien), co w roku 2 daje ok. 4.7 tys. zl/d - wiecej niz lup, okupy i ziemia razem | [P linie 168, S rachunek] |

---

## 4. Jak AI Wysp wybiera cele i czy da sie ja popchnac do najazdow morskich [K]

**Lancuch modeli w grze Jeffa.** Czynny `TargetScoreCalculatingModel` = `StrategicCampaignAI145.StrategicTargetScoreModel` [P log 05-22-14: "latka rozejmu: wpieta (Default, StrategicTargetScoreModel)"]. SCAI liczy `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` i zmienia wynik tylko dla twierdz - **wies (najazd) wraca bez zmian** [K scai.cs:1832]. BK dopisuje postfiksy na Default: `RaidingFactor x (1 + RaidIncentive)` (tylko dla partii juz rabujacej), wagi wojny BK `RaidWeight` w `CurrentObjectiveValue` i `GetTargetScoreForFaction` tylko dla armii [K VanillaModelTweakPatches.cs:1626-1745]. NavalDLC zmienia patrole portowe, najazd liczy gra [K NavalTargetScoreCalculatingModel.cs].

**Kiedy AI w ogole rozwaza najazd** [K `AiMilitaryBehavior.cs`]:
1. Tylko wsie krolestw w wojnie z partia (`mapFaction.FactionsAtWarWith` :166).
2. Tylko pojedyncza druzyna - nie armia i nie gdy zbiera armie (`case Raider: if (Army != null || WillGatherAnArmy) break` :157-160).
3. Jedzenie: druzyna musi miec zapas na `NeededFoodsInDaysThresholdForRaid` = **8 dni**, inaczej wynik 0 (:200; DefaultMobilePartyAIModel :37; NavalDLC bez zmian).
4. Zasieg: najlepsza droga (lad albo morze do portu wsi) krotsza niz **5 x srednia odleglosc dwoch najblizszych miast** dla nawigacji partii (:318-345); morze wybierane, gdy wies ma port i droga morska jest krotsza (stad kontekst NavalRaid).
5. Sila: druzyna musi miec >= 0.75 x (obroncy wsi + czesc sily calego krolestwa wroga na osade) (`DefaultTargetScoreCalculatingModel` :205-243) - silny wrog (Polnoc) "broni" kazdej wsi czescia swojej sily.
6. Wynik: stosunek sil (do 2) x odleglosc x wartosc wsi dla wroga x 1.5 x **dopisek biedy gry**: `1 + (10 000 - kiesa rodu) / 20 000` przy kiesie rodu < 10 000 (najwyzej x1.5) (:288-295) x relacja z panem wsi; potem x odleglosc x wielkosc druzyny x jedzenie (:451-460).

**Wniosek** [S]: Wyspy nie maja dzis kogo rabowac przez wieksza czesc kampanii - wojna fabularna z Dolina (d23-506) jest poza zasiegiem (Pyke - najblizsza osada Doliny ok. 300 w linii prostej przy progu ok. 180 [S: 5 x 36.2, srednia odleglosc miasta do najblizszego miasta na mapie ROT w linii prostej, 216 miast]); Polnoc w zasiegu tylko Flint's Finger (170); Zachod (43-152) - tylko gdy Dorzecze wciagnie Wyspy do wojny z KP.

**Czy mozna popchnac AI bez lamania jej** [K]:

| droga | co robi | precedens w naszym kodzie | granice |
|---|---|---|---|
| A. postfiks na `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` dla `ArmyTypes.Raider` | mnozy ocene najazdu (np. wedlug biedy rodu, wsi z portem) - AI dalej sama wybiera miedzy najazdem, oblezeniem, obrona, patrolem | `SiegeRansom.ScorePost` (zeruje oblezenie pod rozejmem; wpiety na Default i na StrategicTargetScoreModel) [K SiegeRansom.Act.cs:396-405, 658-685] | nie dodaje celow: wojna, zasieg, 8 dni jedzenia, druzyna (nie armia) zostaja |
| B. postfiks na `CampaignEventDispatcher.AiHourlyTick` (lista `PartyThinkParams.AIBehaviorScores`) | zmienia wynik juz policzonych opcji `RaidSettlement` (SetBehaviorScore); mozna tez dopisac opcje (AddBehaviorScore) | `WatchLaw.AiTickPostfix` (H1: Straz nie pali wsi - opcje rabunku -1) [K WatchLaw.cs:361-392] | dopisanie celu spoza zasiegu = rejs dluzszy niz zapas jedzenia; ryzyko glodu i "wiecznego rejsu" |
| C. ustawienie BK `RaidIncentive` | mnozy `RaidingFactor` (trzymanie rabunku, ktory juz trwa) | - | dla wszystkich krolestw, nie dla jednego; nie tworzy nowych najazdow |
| D. rabunek bez wojny | - | - | lamie pokoj (najazd to akt wojny w grze); wojny Wysp to fabula ROT i porozumienia - **nie ruszac** |

Jedna regula dla gracza i AI: A i B dotycza tylko decyzji AI; gracz-wasal wybiera cel sam - regula pieniedzy z najazdu (E7/R1/R2) i tak jest wspolna [K].

---

## 5. Lore ASOIAF -> mechanika

| lore [L] | co jest dzis w grze | luka |
|---|---|---|
| **Zelazna cena** - zelazny czlowiek bierze lup mieczem; placenie "zlota cena" (kupowanie) to wstyd w Starym Obyczaju; pierscienie i zbroje nosi sie zdobyte (AGOT/ACOK - Theon, Asha; AFFC - rozdzialy wysp) | lup z wygranej bitwy (A6: zloto pokonanych do zwyciezcow), lup miast (L0-L5: laska / grabiez / spustoszenie), kiesy rozbitych karawan i taborow (3.2), pryzy morskie (181) [K/P] | z najazdu na wies lup liczony od garstki trafionych (rozdz. 2.4) - "zelazna cena" w gospodarce prawie nie wystepuje |
| **Thralle** - jency z najazdow pracuja w kopalniach i na polach wysp; nie sa niewolnikami w sensie Essos (dzieci rodza sie wolne, nie wolno ich sprzedac); dzieki nim wolni moga isc na morze (AFFC, TWOIAF) | `ThrallLaw` (konto NIEWOLNI, 27 tys. na start, 5%/rok smierci, bez dzieci), 10% trafionych w najezdzie do lochu najezdzcy z niewola, prog wolnych rak Wysp 40% [K ThrallLaw.cs, FreeHands.cs] | thralle nie rosna (26 993 -> 25 684); najazdow za malo, by je odnowic [P] |
| **Zony solne** - branki z najazdow jako drugie zony | brak | (bez mechaniki; nie potrzeba do pieniedzy) |
| **Stary Obyczaj vs pokoj krola** - po podboju Targaryenow lupienie zabronione; Wyspy wracaja do niego w buntach (Dagon Greyjoy, bunt Balona 289 - spalenie floty Lannisterow pod Lannisportem, Fair Isle, Seagard) | AI rabuje tylko wroga w wojnie [K] - zgodne z lore: rabunek = wojna | brak wlasnej wojny Wysp z bogatym wybrzezem poza wezwaniami Dorzecza |
| **Cele na zachodnim brzegu** - Zachod (Lannisport, Kayce, Fair Isle, Banefort), Polnoc (Kamienny Brzeg, Wyspa Niedzwiedzia, Torrhen's Square, Deepwood Motte, Moat Cailin - ACOK), Reach (Wyspy Tarczowe, Mander, Arbor - AFFC, Euron), Dorzecze (Seagard Mallisterow - twierdza przeciw najezdzcom z wysp) | najazdy z morza (NavalRaid) dzialaja: 12 najazdow Wysp z morza na Zachod [P]; fabula ROT: wojna z Polnoca "Balon orders his Ironborn to reave the North" [K ROTStorylineWars.cs:86] | na Polnoc, Reach i Seagard - 0 najazdow [P] |
| **Bieda wysp** - chuda ziemia, malo zboza, rybolowstwo, kopalnie (zelazo, cyna, olow; Harlaw najbogatsza wyspa); bez rabunku sprowadzaja zywnosc (ACOK, AFFC, TWOIAF) | ziemia 1.9 tys. zl/d na 450 tys. ludzi; 4 kopalnie zelaza, 2 srebra, sol [P] | - |
| **Najemni korsarze** - Wyspy walcza za cudza sprawe tylko, gdy im to sie oplaca (Balon odrzuca sojusz z Robbem i bierze Polnoc dla siebie - ACOK) | porozumienia 168: Dorzecze placi Wyspom za wojne z KP [P] | lore odwrotne niz mapa wojen gry (w ksiazce Wyspy bija Polnoc, w grze sa sojusznikiem Dorzecza) |
| **"We Do Not Sow"** (slowa Greyjoyow) - kto nie sieje, ten bierze | rody Wysp maja D z ziemi najnizsze wsrod duzych krolestw [P] | - |

---

## 6. Nocna Straz - jak dzis nie placi zoldu [K] (wzor dla Wolnych Ludzi)

- **Paczka 182 "Straz bez zoldu"** (`WatchUnpaid`, decyzja Jeffa 07.10): zold kazdej jednostki (nie bohatera) w partiach i zalogach Strazy = 0 - postfiks `MountedWage.WagePostfix` w kontekscie zoldu partii [K MountedWage.cs:77-79, CrownGifts.cs:18-19]. Dziala tylko z budzetem rodu 166 (`WatchUnpaidOn = WatchUnpaid && ClanBudgetEnabled`).
- **Hamulec liczebnosci w ludziach**: `UnpaidTroopsCapInMen` - rod Strazy ma `b.Zero`; pulap w ludziach = (pulap zoldu - udzial dworu) / nominalny zold czlowieka (taki, jaki by bral) [K ClanBudget.cs:561-590]. Werbunek, nowe partie i przyrost zalog Strazy staja na tym pulapie. Bez tego Straz rosnie bez konca (pieniadze nie hamuja).
- Straz poza doplata W5 i tarczowym W6 (`PartyCapOwn`, `ScutageCap` i `PlayerCaps` zwracaja false przy `b.Zero`), rycerze Strazy bez zoldu (`GentryService`) [K ClanBudget.cs:304, 338, 360; GentryService.cs:169, 328].
- Pieniadze Strazy: dar Polnocy 25% wplywow dnia jej skarbca (`GiftNorthToWatchShare`) do glow rodow Strazy wedlug stalych wag [K CrownGifts.cs:13-17]; jedzenie i sprzet z daru i wlasnych wsi.
- Straz nie pali wsi: `WatchDoesNotRaid` (H1) [K WatchLaw.cs:26, 371-392]; rekrut tylko z ludzi Westeros [K WatchLaw.cs:18-25].
- Przy okazji (dla rozpoznania Wolnych Ludzi) [P]: Wolni Ludzie maja 4 najazdy z walka w Q1 i ani jednego pozniej; zold druzyn 5.8 i zalog 5.5 tys. zl/d w roku 2 przy wplywie rodow 5.6; ich krolestwo jest w `ClanBudget.Raiders` (lup w D) jak Wyspy.

---

## 7. Co z tego wynika dla projektu - skala (nic nie zrobione) [S]

Decyzje i liczby naleza do projektu (zasada Jeffa); tu tylko rachunek skali na pomiarach z rozdz. 1-4.
1. **Brak Wysp w roku 2**: 23.9 tys. zl/d po W5, 16.2 po W7 [P ROZPOZNANIE-RODY]. Dzisiejszy lup najazdow swiata ok. 0.1 tys. zl/d, pryzy morskie swiata ok. 12 tys. zl/d - zaden istniejacy kanal "zbojecki" nie ma tej skali; regula "Wyspy zyja z rabunku" to nowy przeplyw, nie korekta wspolczynnika.
2. **Kto moglby placic** (obieg zamkniety): lud i kasy osad zachodniego wybrzeza - Zachod (kultura vlandia, 15 osad): kiesy ludu 1.29 -> 0.83 mln, kasy osad 1.60 -> 1.23 mln, dochod ludu 109 -> 29 tys. zl/d (d364 -> d728); Polnoc (22 osady): 2.11 / 1.94 mln, kasy 1.43 / 1.52 mln, dochod 59 / 54 tys. zl/d; Reach (16): 1.66 / 1.37 mln, kasy 2.05 / 2.67 mln, dochod 70 tys. zl/d; Dorzecze (16): 1.88 / 3.46 mln, dochod 55 / 74 tys. zl/d [P `kiesa-ludu.csv`]. 16-24 tys. zl/d to 55-83% dziennego dochodu ludu Zachodu z d728 (15-22% z d364) albo 2-3% dziennie jego oszczednosci - za duzo jak na jedna kraine [S]; rozlozone na Zachod + Polnoc + Dorzecze (razem 157 tys. zl/d dochodu ludu w d728) - 10-15% [S].
3. **Gdzie lup sie gubi dzis**: (a) s = trafieni / ludzie okregu (rozdz. 2.4); (b) brak wojny w zasiegu przez wieksza czesc kampanii (rozdz. 2.2, 4); (c) po d405 zero najazdow mimo wojny (przyczyna nieznana - rozdz. 8 pkt 1). Kazda regula pieniedzy z najazdu nic nie da, dopoki (b) i (c) nie sa rozpoznane - najpierw linia diagnozy.
4. **Kierunki do rozwazenia w projekcie** (bez rozstrzygniecia): lup z najazdu z morza wedlug "zasiegu strachu" (cala okolica placi, nie tylko trafieni) z platnikiem = kiesy ludu i kasa wsi okregu; okup wybrzeza (wies albo miasto portowe placi, zeby nie palono - historycznie "appatis", w lore slabe, bo Zelazni Ludzie gardza zlota cena); udzial korony Wysp w pryzach i statkach zdobytych przez jej panow; paser Wysp jako odbiorca lupu piratow; postfiks oceny najazdu (droga A z rozdz. 4) - wedlug biedy rodu (G < R), jedna regula dla kazdego biednego rodu, nie tylko Wysp (zasada "zachowanie z biezacego zysku").
5. **Ryzyko**: wiecej najazdow = mniej ludzi i plonu na Zachodzie (spustoszenie E4/5, uchodzcy, ksiega wiosek E4/11) i wiecej strat Wysp (380 ludzi na 47 najazdow) [P]; Polnoc juz dzis rabuje Zachod (Banefort 22) - Zachod jest najbardziej spustoszonym wybrzezem.

---

## 8. Czego nie wiem (+ linia diagnozy)

1. **Dlaczego po d405 Wyspy nie rabuja wcale**, choc sa w wojnie z KP (ok. 225 dob w trzech okresach) i Polnoca (295 dob, fabula ROT "reave the North"). Kandydaci [S]: zima od d460 (jedzenie partii +42.5%, plon wsi wysp -50%) i prog 8 dni jedzenia; druzyny skurczone do 1.7-2.5 tys. ludzi wobec sily calego krolestwa wroga w ocenie (pkt 5 rozdz. 4); udzial w armiach (armia nie rabuje); zasieg (Polnoc poza nim poza Flint's Finger). Diagnoza: linia doby "Wyspy - decyzje AI" dla druzyn lordow Wysp: liczba opcji `RaidSettlement` w `PartyThinkParams` i najlepszy wynik, ile druzyn w armii, dni jedzenia (min/mediana), obecne `DefaultBehavior` (rabunek / oblezenie / obrona / patrol / postoj), cel i odleglosc.
2. **Prawdziwy prog zasiegu najazdu** - `GetAverageDistanceBetweenClosestTwoTownsWithNavigationType` dla Default / Naval / All na mapie ROT (moje 180 to linia prosta x 5 - przyblizenie). Diagnoza: jednorazowy wpis przy starcie sesji z trzema wartosciami i odlegloscia drogowa Pyke -> Flint's Finger, Seagard, Lannisport, Wyspy Tarczowe.
3. **Z czego skladaja sie "jednorazowe" Wysp** (2.7 tys. zl/d): sprzedany lup, lup bitew A6, okupy (0 otrzymanych - wiec nie okupy), statki od portow za wlasne rozbite druzyny (181 pkt 1), pryzy (181 pkt 4). Diagnoza: w CSV rodow kolumny jednorazowych rozbite wedlug zrodla (`TodayShips`, `TodayRansom`, zdarzenia od osady / od bohatera i partii) - pola juz sa w `ClanIncomeBook.Rec`, brak tylko w CSV.
4. **Ile band morskich (Sea Raiders, Stepstone) jest na mapie i gdzie** - linia "Wyrzutki" liczy wszystkie bandy razem, B175 widzi tylko bitwy z Polnoca albo krolestwami. Diagnoza: w linii "Wyrzutki" liczba band i ludzi wedlug klanu bandyckiego + kiesy band morskich + ich sprzedaz u pasera wedlug miasta.
5. **Czy korona Wysp ma polityki NavalDLC** ("Writs of Reprisal", "Amnesties for Pirates", "Royal Ransom Claim", "Kings Tithe on Keels"). Diagnoza: wpis raz na 28 dob - polityki krolestw z listy NavalPolicies.
6. **Ile najazdow Wysp bylo bez walki** (B175 ich nie widzi). Diagnoza: nasluch `RaidCompletedEvent` / `VillageBeingRaided` z krolestwem napastnika - licznik na krolestwo w linii E7/R1 (wzor: `WatchLaw.OnRaidCompleted`).
7. **Czy w wojnie z Polnoca AI Wysp choc raz rozwazala najazd na Flint's Finger albo Wyspe Niedzwiedzia** - to samo co pkt 1.

---

## 9. Pliki i polecenia

Skrypty i wyniki: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\wyspy\` (dysk C - tymczasowy):
- `pref.py <log>` - spis rodzajow linii (`pref_dw.txt`); `last.py <log> "klucz@@klucz" "dzien N"` - przykladowe linie.
- `setmap.py <settlements.xml> setmap.json` - id osady -> nazwa, kultura, polozenie.
- `bitwy.py <bitwy.log> sturgia` - najazdy, bitwy morskie, rodzaje stron kwartalami (`bitwy_sturgia.txt`, `bitwy_120.txt`, `bitwy_rows.json`).
- `wars.py <session.log> "Iron Islands"` - wojny Wysp z KRONIKI WOJEN.
- `csvk.py <budzet-rodow.csv> "Iron Islands" "kolumny"` - sumy rodow krolestwa kwartalami z podzialem lenno / bez lenna / najemnik (`csv_ii.txt`).
- `raidloot.py <log>` - lup E7/R1 swiata; `statki.py <log>` - wyplaty portow (181) wedlug rodzaju; `najazdy.py <wioski-okregi.csv>` - zmiany `ostatni_najazd` na region.
- korona Wysp i porownanie krolestw - z `rody2\dw.pkl` i `rody2\dwc.pkl` (rozpoznanie A3).

```
L=D:/Backup-Bannerlord/logs/2026-10-14-noc
python -I bitwy.py $L/Logs/2026-10-13_21-14-13/bitwy.log sturgia
python -I wars.py $L/session-2026-10-13_21-14-11.log "Iron Islands"
python -I csvk.py $L/Logs/2026-10-13_21-14-13/budzet-rodow.csv "Iron Islands" "D,d_ziemia,d_korona,sr_wlasne,sr_jednorazowe,sr_inne_z_modelu,doplata_w5,pobor_wojenny_w7,zold_partii,zold_zalog,G"
python -I raidloot.py $L/Armoury-2026-10-13_21-14-13.log
python -I statki.py $L/Armoury-2026-10-13_21-14-13.log
```
