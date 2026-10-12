# PLAN KODU - paczka E5/0 (195): pomiar etapu 5 - kiesa ludu "na sucho", nadwyzka D4, bitwy, notable, gotowka wsi, jedzenie z targu, zadowolenie BK, modele czynne

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane.
Wiazace: `docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md` v2 (dalej **P**; E5/0 = P rozdz. 3 "E5/0", reguly R1-R15 rozdz. 2, D1/D4, progi rozdz. 4,
ryzyka rozdz. 5, krytyka rozdz. 8), `docs\etap5\KRYTYKA-E5.md` (K1 = E5K1-09, K2, K3, K7, W1-W3), rozpoznania `docs\etap5\rozpoznanie-A/B/C/D-*.md` (**RA/RB/RC/RD**),
model kiesy `docs\PROJEKT-KIESA-LUDU-2026-10-09.md` (**KL**) i jego rachunek **sim2** (`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad\kiesa\sim2\sim_kiesa2.py`),
decyzje Jeffa `docs\STAN-PRAC.md` (09.10 lista A-L i 04:25/04:55; 11.10 02:45 "statki razem z 184", 03:05 "testy zbiorcze").

Skroty miejsc (SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`):
- **K:** = `SCR\e5\0\Armoury\src\` (galaz `w-toku/e5-0` = 392c519 = zbior 31 + E4/6 + E4/7 scalone; numery linii z tego drzewa - sprawdzone 11.10; wobec P sa przesuniete
  w `ArmouryBehavior.cs`: `CastlePurse.Daily` :1467 (P :1459), `MoneyLedger.Daily` :1550 (P :1542)). **T:** = `SCR\e5\0\tools\`.
- **G:** = `SCR\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `SCR\e4tools\bkfull\` (BannerKings.Redux).
- **L:N** = linia logu biegu A zbioru 31 `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob); **sr28** = srednia dob 93-120 (RA).
- Rownolegle drzewa: E4/8 `SCR\e4\48`, E4/10 `SCR\e4\410`, E4/11 `w-toku/e4-11`, A1 `SCR\e5\a1`, A2-A5 `SCR\e5\a25` - scalanie na koncu (rozdz. 1).

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **Gra bez zmian - twarda zasada paczki.** Zadna latka nie zwraca `false`, nie ma parametru `ref`/`__result` do zapisu, nie wola `ChangeGold`, `AddToCounts`,
   `SetSoldItems`, `UpdateSatisfaction`, `GiveGoldAction`; nie pisze do pol gry ani BK. **E5/0 nie dodaje pozycji `MoneyLedger`** (zadnych `Marks`/`Notes`/`Holders`) -
   wlasne linie logu. Zloto, ludzie, towary, ceny, zadowolenie - jak bez paczki. Wylacznik `TownFolkMode = 0` = stan sprzed paczki (latki wracaja na pierwszej linii).
2. **Nowy kod w NOWYCH plikach:** `TownFolk.cs` (stan sesji, API `Note`/`Sale`, model "na sucho" `Daily`, linia "Kiesa ludu (na sucho)", CSV) i `TownFolk.Probe.cs`
   (`partial` - latki i nasluchy pomiaru, kalibracja D1, linie Bitwy / Notable / Jedzenie / Zadowolenie / Nadwyzka kas, sonda korony, linie startowe). W istniejacych
   plikach tylko **jednolinijkowe wpiecia** (rozdz. 2.2) - kazde we wlasnym `try`, PO przelewie gry, nigdy miedzy zdjeciem a dopisaniem (P4 U24).
3. **Stan tylko w pamieci sesji** (bez `SyncData`, bez klucza zapisu): kiesy na sucho, kalibracja D1, okna 7/28 dob. `TownFolk.Reset()` w konstruktorze `ArmouryBehavior`
   (K:ArmouryBehavior.cs:395). Po wczytaniu zapisu: nowa kalibracja i kiesy od zapasow startowych (linia "sesja od dnia").
4. **Kalibracja D1 (P rozdz. 2 D1)** w pierwszej dobie sesji, w ktorej `PopulationLaw.Calibrated` (K:PopulationLaw.cs:204) - **E5/0 nigdy nie wola `PeopleOf`
   przed kalibracja** (`PeopleOf` wola `Calibrate()`, K:PopulationLaw.cs:215-218 - przesunelibysmy chwile kalibracji ksiegi ludzi; E4/3). Zamek: `PeopleOf` = 0
   (K:PopulationLaw.cs:220-223) - podzamcze liczone z ludzi BK z kalibracji, bez skali ksiegi (odstepstwo O5).
5. **Budzet i wydatek konsumpcji raz na (osade, kategorie, dobe) - K1/E5K1-09.** Nawias konsumpcji: postfiks na nieruszanej przez nikogo prywatnej
   `ItemConsumptionBehavior.UpdateDemandShift` otwiera (slownik dnia, indeks cen, migawka zadowolenia BK), postfiks `Priority.Last` + finalizer na `MakeConsumption`
   zamyka. Postfiks `Priority.Last` na BK `CalculateBudget` (po `HistoricalPrices.BudgetPostfix`) liczy tylko wewnatrz nawiasu tej samej osady (wywolania
   `RawPrice.HouseBudget` refleksja z naszego bloku dobowego sa poza nawiasem - K:RawPrice.cs:227-243, wolane tylko z `RawPrice.Daily` :343). Wydatek kategorii =
   suma "budzet wiersza - reszta po wierszu" sledzona po kolejnych wierszach polki (rozdz. 4.2) - kontrola z `ConsumedValue` BK +-2% (prog E50-T5).
   Wszystkie latki czynne po `Campaign.GameStarted` (W3: gra konsumuje i liczy popyt przy tworzeniu swiata, G ItemConsumptionBehavior.cs:22-40).
6. **Przeplywy do ludu = te same miejsca, ktore B1 zamieni na `Earn`/`NoteSale`** (P E5/0 "Pliki", RB 5.5): 14 miejsc z P (WorkshopLaw :322 - zaplata kupcow za wyrob -
   zastapiona wartoscia z `OnItemProduced`, ktore nasz warsztat wola :326) + 5 uzupelnien z tych samych zjawisk KL
   (karawany E3/4, bandy w miescie, dozbrajanie MenUpgrade, zaopatrzenie BK, sakiewki zwolnionych do wsi - odstepstwo O1). Wartosc wyrobow do plac 35% (KL L7) -
   z nasluchu `OnItemProducedEvent` (gra, BK, nasze warsztaty go wolaja - rozdz. 4.6).
7. **Model "na sucho" = rachunek sim2 przeniesiony z krolestwa na osade** (sim_kiesa2.py:160-205 potrzeby i kotwica, :271-283 dochod klas, :430-480 doba klasy),
   stawki P0 sim2 (:50-80) jako stale w kodzie z komentarzem (klucze stawek dopiero w B1 - P "Klucze B1"); poprawki P v2: D1, R11 (akcyza tylko przy towarze,
   cech 1% R), R13 (wies 65%), R15 (184 przy s = 3% "na probe", stawki v2 W 0.5s / R 1.0s / P 1.5s / podzamcze 1.0s, tylko w wojnie). Wynik: stan wyjsciowy do
   kalibracji B1 (P ryzyko 1), nie przewidywanie.
8. **Rozbicie bitew** (P E5/0, RB 7 pkt 3): wlasny prefiks + postfiks (tylko odczyt) na `MapEventParty.CommitGoldChanges` w nowym pliku - bez przerabiania okna O14
   (`CirculationWindows.FlagFin` jest wspolny dla ok. 20 okien; odstepstwo O2). Decyzja o A6: netto na ladzie sr28 > 5 000/d.
9. **Notable** (P v2 linia "Notable (E5/0)"): prefiks + postfiks (odczyt) na `BalanceGoldAndPowerOfNotable` - pasmo miasta/wsie osobno i ile kasa miasta handlu
   (wies -> `Village.TradeBound`) moglaby pokryc ze swojej nadwyzki D4.
10. **D4 liczona bez skutkow ubocznych:** miasto `Seal31.TownReserve(t)` (K:Seal31.cs:96), zamek `CastlePurse.Reserve(t)` (K:CastlePurse.cs:191). **Nie wolac
    `CastlePurse.Spendable` ani `Seal31.Spare`** - Spendable podbija liczniki linii "Zawor zamkow (110)" (K:CastlePurse.cs:386-388).
11. **Bez wywolan modelu zywnosci** (`Town.FoodChange`) - na tym modelu siedza latki E4/6 i zimy; "jedzenie z targu" z `Town.SoldItems` (to samo, co dodaje
    model gry - G DefaultSettlementFoodModel.cs:82-90), polka zamku z rosteru (odstepstwo O8).
12. **Jeden klucz:** `TownFolkMode` (int 0..1, domyslnie 1) w nowej grupie "The townsfolk's purse"; bez `TownFolkLive` (powstaje w B1 - K3).
13. **Narzedzie:** `T:sprawdz_logi.py --grupa etap5` - szkielet `etap5_main` + blok `etap5_e50` (rozdz. 8); kolejne paczki dopisuja swoje bloki.
14. **Poza E5/0 (nie dopisywac "przy okazji" - CLAUDE.md 8 pkt 3):** kiesa czynna, zapis, stawki w MCM, kotwica na budzecie (B1), zawor/`TownPurse` (A1), markietani i notable
    przez kase (A3), korekta zadowolenia (B1/C2), lup wodza (A6), poprawka `W2` (`HistoricalPrices` m^k) - tylko odnotowana w P.

---

## 1. Zaleznosci i punkty styku (E4 koniec, E4/8, E4/10, A1-A5, B1)

| # | Z kim | Co wspolne | Gdzie | Rozstrzygniecie |
|---|---|---|---|---|
| S1 | E4 (koniec) | `PeopleOf`, `Calibrated`, stan miast "t:" | K:PopulationLaw.cs:204, :215-223 | tylko odczyt; liczby maja sens na kodzie koncowym E4 - E5/0 jedzie w tescie koncowym E4 (P pkt 0.10) |
| S2 | E4/8 (`SCR\e4\48`) | `PopulationLaw.Daily` (stan miast), `ClanBudget`, `CreateCaravanParty` | P 5.1 | E5/0 nie zmienia `PopulationLaw.cs` ani latek karawan; w `ClanBudget.cs` dwie linie (:621, :759) - scalanie tekstowe |
| S3 | E4/10 (`SCR\e4\410`) | sakiewka zwolnionych (`ClanBudget.cs:749-759`), najem `LevyGold` | PLAN-KODU-E410.md:56, 3.2 | wpiecie przy :759 stoi PO `ChangeGold(took)`; E4/10 dzieli zwolnionych przed `ToVillage` - przy scaleniu wpiecie zostaje przy przelewie do wsi (dla puli WETERANI - B6); najem do kas miast (NMerc) NIE jest dochodem ludu (KL 5.2 "Nie: werbunek") |
| S4 | E4/11 | jedzenie warowni od ludzi okregu (postfiks `CalculateTownFoodStocksChange`) | PLAN-KODU-E411.md:64-68, 5.1 | E5/0 nie wola modelu zywnosci (pkt 0.11); linia "Jedzenie z targu" czyta `SoldItems` i polke - B4 bedzie jej uzywal (P v2) |
| S5 | A1 (`SCR\e5\a1`) | `MoneyLedger.cs`, `MoneyLedger.Obieg.cs`, `CastlePurse.cs`, `ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs` | P E5/A1 "Pliki" | E5/0 w `MoneyLedger` tylko wywolania `TownFolk.*` (bez indeksow); A1 numeruje swoje pozycje od pierwszego wolnego - brak kolizji indeksow; konflikty tylko tekstowe |
| S6 | A3 (`SCR\e5\a25`) | `BalanceGoldAndPowerOfNotable` (A3: kontekst Low) | P E5/A3 | obie strony void; E5/0 mierzy zmiane kiesy notabla przed/po - po A3 ta sama linia pokaze "z niczego 0" (prog A3) |
| S7 | B1 | `TownFolk.cs` | P E5/B1 "Pliki" | B1 rozbuduje `TownFolk` (tryb czynny, zapis); miejsca `TownFolk.Note/Sale` z rozdz. 2.2 B1 zamieni na `Earn`/`NoteSale` |

Wniosek: E5/0 da sie napisac i zbudowac od 392c519 bez czekania na kogokolwiek; scalenie z E4/8, E4/10, A1-A5 - tekstowe (rozdz. 10.3).

---

## 2. Pliki i klasy

### 2.1 Nowe pliki
| Plik | Klasa | Zawartosc |
|---|---|---|
| `Armoury/src/TownFolk.cs` (CRLF jak cale repo; ok. 700-900 linii) | `internal static partial class TownFolk` | stale P0 sim2 (rozdz. 5.1), stan sesji (rozdz. 3), `On`/`Live`, `Note`/`Sale`/`OnGold`/`OnTownCoin`/`NoteCartPaid`/`NoteVillageReturn`/`OnItemProduced`, `Daily()` (model na sucho), `Reset()`, linia "Kiesa ludu (na sucho)", CSV `kiesa-ludu.csv`, `VillagePolicySplit(...)` dla linii MoneyLedger, `Stumble` |
| `Armoury/src/TownFolk.Probe.cs` (CRLF; ok. 600-800 linii) | ta sama `partial` | `ApplyAll(Harmony)` + latki (rozdz. 4), odczyt BK refleksja (wzor `Measure169c.ResolvePop` K:Measure169c.cs:516-538), kalibracja D1, modele czynne, `CrownProbe()`, `Lines()` (Bitwy, Notable, Jedzenie z targu, Zadowolenie BK, Nadwyzka kas), linie startowe |

`On` = `Settings.Current.TownFolkMode >= 1 && Settings.Current.LogEnabled`; `Live` = `On && Campaign.Current != null && Campaign.Current.GameStarted` (wzor K:CaravanCapital.cs:36).
Kazde cialo w `try`; `Stumble(where, e)` - licznik potkniec doby i pierwszy wyjatek z miejsca do logu (wzor K:CrownIncome.cs:103-107); **bez globalnych wylacznikow**
(CLAUDE.md 7 - liczymy potkniecia, nie gasimy funkcji).

### 2.2 Wpiecia w istniejace pliki (kazde: jedna linia `try { TownFolk.X(...); } catch { }` albo wywolanie, ktore samo nie rzuca)
| Plik:linia (K) | Po czym | Wywolanie | Rodzaj (rozdz. 5.2) |
|---|---|---|---|
| `MenPurse.cs:299` (po `MoneyLedger.Note(NLife...)` :303) | `st.Town.ChangeGold(life)` | `TownFolk.Note(st, life, KLife)` | zycie zolnierzy |
| `Seal31B.cs:454` (po :456) **[uzupelnienie O1]** | `st.Town.ChangeGold(t)` karawany E3/4 | `TownFolk.Note(st, t, KLifeCaravan)` | zycie ludzi karawan (P R9 "takze karawany") |
| `OutlawLaw.cs:1773` (po :1774) **[O1]** | `town.Town.ChangeGold(spend)` | `TownFolk.Note(town, spend, KLifeBand)` | zycie band w miescie (to samo zjawisko co R9) |
| `SoldierPay.cs:473` (po :476) | `town.ChangeGold(coffers)` | `TownFolk.Note(st, coffers, KGarrison)` | zold zalogi wydany na miejscu |
| `ClanBudget.cs:621` | `seat.Town.ChangeGold(ix)` | `TownFolk.Note(seat, ix, KCourt)` | dwor (w bloku - okno nastepnej doby, rozdz. 3.3) |
| `ClanBudget.cs:759` **[O1]** | `village...ChangeGold(took)` | `TownFolk.Note(village, took, KReleased)` | sakiewka zwolnionych do kiesy wsi (G wsi wedlug polityki) |
| `BuildFunding.cs:98` (drugi `try`) | `town.ChangeGold(revenue)` | `TownFolk.Note(town.Settlement, revenue, KMine)` | place gornikow |
| `BuildFunding.cs:182` | `market.Town.ChangeGold(matSpent)` | `TownFolk.Sale(market, matSpent, MixBuild)` | material budow |
| `BuildFunding.cs:185` | `st.Town.ChangeGold(labourI)` | `TownFolk.Note(st, labourI, KBuildLabour)` | place budowy |
| `WorkshopLaw.cs:321` (po :325) | `town.ChangeGold(wagesI)` | `TownFolk.Note(workshop.Settlement, wagesI, KCraftPaid)` | place cyklu juz wyplacone (kontrola L7) |
| `WorkshopLaw.cs:453` (po :454) | `town.ChangeGold(st.Mc)` | `TownFolk.Sale(workshop.Settlement, st.Mc, MixGoods)` | surowce kupione od kupcow |
| `WorkshopTrade.cs:329` (po :330) | `town.ChangeGold(paid)` | `TownFolk.Note(shop.Settlement, paid, wages ? KCraftPaid : KShopKeep)` | place i utrzymanie z kapitalu |
| `WorkshopTrade.cs:503` (po :504) | `town.ChangeGold(n)` | `TownFolk.Note(w.Settlement, n, KCraftFair)` | nadwyzka ceny wyrobu (tylko odczyt - jest w wartosci wyrobu L7) |
| `AiGear.cs:705` (po :707) | `st.Town.ChangeGold(cost)` | `TownFolk.Sale(st, cost, MixGear)` | sprzet AI |
| `AiGear.cs:747` (po :748) | `market.Town.ChangeGold(cost)` | `TownFolk.Sale(market, cost, MixGear)` | zamowienia zamkow (171) |
| `MenUpgrade.cs:447` (po :450) **[O1]** | `w.Shop.Town.ChangeGold(paid)` | `TownFolk.Sale(w.Shop, paid, MixGear)` | dozbrajanie (ksiega liczy je "jak AiGear") |
| `ArmyClothing.cs:181` (po :182) | `st.Town.ChangeGold(taken)` | `TownFolk.Sale(st, taken, MixCloth)` | odziez z sakiewek |
| `ArmyClothing.cs:238` (po :240) | `market.Town.ChangeGold(spent)` | `TownFolk.Sale(market, spent, MixCloth)` | odziez zalogi zamku kupiona w miescie |
| `BkSupplyMarket.cs:172` (po :173) **[O1]** | `comp.ChangeGold(paid)` | `TownFolk.Sale(st, paid, MixFood)` | zaopatrzenie BK z sakiewek (zywnosc) |
| `SupplyDemand.cs:569` | `src.Town.ChangeGold(cost)` | `TownFolk.OnTownCoin(best, -cost, BOurs)` | zakup kasy odbiorcy (w bloku - poza `MoneyLedger.Note`) |
| `MoneyLedger.cs:391` `Note` (pierwsza linia ciala) | - | `TownFolk.OnTownCoin(st, amount, BOurs)` (liczy tylko ujemne kwoty miast) | nasze sciezki zakupu kas miast (pas ponizej zapasu) |
| `MoneyLedger.Obieg.cs:190` `Note169` (pierwsza linia) | - | jw. | jw. |
| `MoneyLedger.cs:246-252` `OnAfterEntered` (galaz `st.Town != null`) | `_vPaid[c] += ...` | `TownFolk.NoteCartPaid(st, _winGold - now)` | tabory placone przez kase (pas ponizej zapasu, gry/BK) |
| `MoneyLedger.cs:253-261` `OnAfterEntered` (galaz wsi, po `_vEstates += ...` :260) | - | `TownFolk.NoteVillageReturn(home, handed, kept, tax, _winEstates)` | utarg wsi na wies (jedna definicja z "Przeplywy osad") |
| `MoneyLedger.cs:303` `OnGoldTraded` (zaraz po `if (gNone && rNone) return;` :303 - po normalizacji :301, przed galezia majatkow :305-310) | - | `TownFolk.OnGold(gh, gp, rh, rp, a, gNone, rNone, _inBlock)` | karawany i gracz w handlu z miastem; przelewy gry do wsi; przelewy kas miast ponizej zapasu |
| `MoneyLedger.cs:1017` `Daily` (po `ObiegBkLine`) | - | `if (TownFolk.On) try { Log.Info(VillageCashLine(day)); } catch (Exception e) { Log.Error(...); }` | linia "Gotowka wsi (E5/0)" (rozdz. 7.6) |
| `MoneyLedger.Obieg.cs` nowa metoda `VillageCashLine(int day)`; `ObiegLine` :564 dopisek; :654 napis | - | rozdz. 7.6-7.7 | - |
| `ArmouryBehavior.cs:395` (konstruktor) | `... ScorchedEarth.Reset();` | `TownFolk.Reset();` | stan sesji |
| `ArmouryBehavior.cs:626` (`RegisterEvents`, obok `OreLedger.OnProduced`) | - | `CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, TownFolk.OnItemProduced);` | wartosc wyrobow (L7) |
| `ArmouryBehavior.cs:1468` (po `Mark(MCastle)`, przed `GoodsLedger.Begin` :1470) | - | `try { TownFolk.Daily(); } catch (Exception e) { Log.Error("TownFolk.Daily", e); }` | model na sucho (pozycja B1 - P 5.2 pkt 6) |
| `ArmouryBehavior.cs:1510` (po `CrownRents.Daily`, przed `CrownIncome.End` :1511) | - | `try { TownFolk.CrownProbe(); } catch (...)` | `CrownIncome.DayOf` czynne tylko do `End` (K:CrownIncome.cs:108-113) |
| `ArmouryBehavior.cs:1550` (po `MoneyLedger.Daily`, przed `BeatenPurse.Daily` :1553) | - | `try { TownFolk.Lines(); } catch (...)` | linie dnia; przed `Seal31.Daily` :1554, ktory zeruje `Measure31.DayShipBattleGold` (K:Measure31.cs:483) |
| `SubModuleMain.cs:121` (po `RawPrice.ApplyAll`) | - | `try { TownFolk.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownFolk.ApplyAll", e); }` | latki (menu glowne - jak reszta, K:SubModuleMain.cs:42) |
| `Settings.cs:1287` (po grupie "Ships and the ports (stage 3)", przed `Load` :1289) | - | grupa + klucz (rozdz. 6) | - |
| `T:gen_mcm.py` `RANGES` | - | `'TownFolkMode': (0, 1, "0"),` | regula ogolna dalaby suwak 0..10 (T:gen_mcm.py:135-138) |
| `McmSettings.cs` | - | `python tools/gen_mcm.py` (nie `python3`) | - |
| `T:sprawdz_logi.py` | - | `etap5_main`, `etap5_e50`, dispatch `--grupa etap5`, docstring | rozdz. 8 |

Razem: 2 nowe pliki; 13 plikow zywego kodu (20 jednolinijkowych wpiec) + `MoneyLedger.cs` i `MoneyLedger.Obieg.cs` (5 wywolan `TownFolk.*`, nowa metoda linii,
dopisek, napis) + `ArmouryBehavior`, `SubModuleMain`, `Settings`, `McmSettings` + 2 narzedzia.
`PopulationLaw.cs`, `CastlePurse.cs`, `KingdomTreasury.cs`, `CirculationWindows.cs`, `CrownIncome.cs` - **bez zmian** (tylko odczyt istniejacych `internal`).

---

## 3. Stan (pamiec sesji, bez zapisu)

### 3.1 Pola
```
class Folk  { Settlement St; int Kind (0 miasto, 1 zamek, 2 wies); double HeadsBkW, HeadsBkR, HeadsBkP, HeadsBkS (z kalibracji); double PeopleCal (PeopleOf przy kalibracji, zamek 0);
              bool SlaveRealm; double LW, LR, LP (kiesy na sucho; zamek: LW = podzamcze; wies: LW = kiesa wsi); double Gmina; double[] Win (okno doby wedlug rodzaju KINDS);
              double[] SaleV, SaleLab (sprzedaz kupcow i robocizna wedlug grupy 0 jedzenie / 1 towar / 2 bron-zbytki); double[] NextIn (dochod z wewnatrz ludu na jutro, W/R/P);
              double SpendGame, SpendGameFood, SpendGrp[3] (wydatek gry z petli BK; grupy robocizny 0 jedzenie / 1 towar / 2 bron-konie-zbytki), ConsumedBk; double Idx (indeks cen koszyka); double[] Sat0, Sat1 (zadowolenie BK przed/po, 4 grupy);
              int[] FoodSold7 (zamek: jedzenie z polki, pierscien 7 dob); int Policy (wies: polityka podatkowa BK 0 niska / 1 zwykla / 2 wysoka / 3 zwolnienie); ... }
Dictionary<Settlement, Folk> _f;  bool _calDone; int _sessionDay = -1, _calDay = -1;  stringi linii dnia (Daily -> Lines);
// liczniki linii (zerowane w Lines): bitwy, notable, pas ponizej zapasu wedlug zrodla, przelewy wsi, jedzenie z targu, czas (Stopwatch ticks), potkniecia
// korona: Dictionary<Kingdom, ring 7 dob (kredyt 186 > 0), ring 28 dob (zwrot nalezny)>
// nawias konsumpcji: Town _brTown; Dictionary<ItemCategory,float> _brDict; Dictionary<ItemCategory,Track> _brTrack (Track: double Pending=NaN, Spent, Add; int Rows); bool _brOpen
```
Klucz slownika - obiekt `Settlement` (sesja; bez zapisu nie trzeba `StringId`). Osady: `Settlement.All` z `IsTown || IsCastle || IsVillage` (ok. 798 - L:31689 "osad 798").

### 3.2 Kalibracja D1 (`EnsureCalibrated`, pierwsza doba sesji z `PopulationLaw.Calibrated`; wolana z `Daily`)
- Ludzie BK osady: refleksja jak K:Measure169c.cs:516-538 i :560-569 (`IsSettlementPopulated` przed `GetPopData` :566) - **najpierw `IsSettlementPopulated`, dopiero potem `GetPopData`** (przeglad 169c: `GetPopData`
  dla osady bez danych losuje i zapisuje ludnosc w BK); `GetTypeCount(PopType)` dla 0 szlachta -> P, 1 rzemieslnicy -> R, 2 chlopi + 5 dzierzawcy -> W, 3 niewolni ->
  W poza krolestwami niewolniczymi, w nich "utrzymywani" (KL L8, sim :52-58). Indeksy enum jak K:Measure169c.cs:541 (`ClassName`).
- Krolestwa niewolnicze: lista sim P0 `slave_k` (sim_kiesa2.py:56: Volantis, Lys, Myr, Tyrosh, Qarth, Norvos, Iron Islands, Dothraki Horde) dopasowana do
  `Kingdom.Name` - w linii sesji "dopasowane n z 8 (nazwy)" (klucz `TownFolkSlaveRealms` z id ROT - B1).
- Osada bez danych BK (d120: 0, L:31689 "bez danych BK 0"): **mediana skali** ludzie_BK / `PeopleOf` osad tego samego rodzaju w krolestwie (zamek: mediana ludzi BK
  zamkow krolestwa), udzialy klas KL 1.2 (sim :53-55). Odstepstwo O4 (zamiast wzoru BK `GetDesiredTotalPop`) - liczba takich osad w linii sesji.
- `PeopleCal` = `PeopleOf(st)` (miasto, wies; zamek 0). Skala dnia: `r = PeopleCal > 0 ? PeopleOf(st) / PeopleCal : 1` (P D1 "koszyk(osada, dzis) = ludzie_BK(kal) x koszyk x
  PeopleOf(dzis) / PeopleOf(kal)"). Polityka BK wsi: refleksja `PolicyManager.GetPolicy(st, "tax").Policy` (wzor K:PopulationLaw.cs:1097-1121 - nie wolac `TaxDecree`,
  bo zwraca mnoznik ustawien, nie nazwe), odswiezana raz na dobe.
- **Zapasy startowe na sucho** (R5, KL 5.3, sim `seed='dar'` :208-214): kiesa klasy = `rd_c` dni potrzeby (W 10, R 21, P 30, podzamcze 10, wies 15 dni zakupow) -
  suma w linii sesji (to jest wielkosc przelewu R5 dla A1/B1).
- Gdy `!PopulationLaw.Calibrated` - `Daily` konczy sie linia "Kiesa ludu (na sucho): dzien N | czeka na kalibracje ksiegi ludzi" (bez wolania `PeopleOf`).

### 3.3 Okno doby
Przeplywy (`Note`, `Sale`, `OnGold`, nasluchy, nawias konsumpcji) zbieraja sie w oknie od poprzedniego `TownFolk.Daily` do biezacego - tick osady gry rozklada
konsumpcje na cala dobe (`CampaignPeriodicEventManager.PeriodicDailyTick`, G CampaignPeriodicEventManager.cs:397-406), a przeplywy naszego bloku po `Daily`
(budowy :1471, dwor :1522) wchodza do okna nastepnej doby - kazde okno to pelne 24 h. Pierwsze okno sesji jest niepelne: linia z dopiskiem "doba niepelna
(pierwsza w sesji)", narzedzie ja pomija (jak E4/0 "pierwsza doba sesji").

---

## 4. Okna latek i nasluchy (dowod w dekompilacji, wlasciciele, kolejnosc)

**Skan DLL wszystkich modow** (11.10, `Modules\*\bin\Win64_Shipping_Client\*.dll`, nazwy w UTF-8 i UTF-16): `UpdateDemandShift` - zaden mod; `CommitGoldChanges`,
`BalanceGoldAndPowerOfNotable` - tylko Armoury; `CalculateBudget`, `MakeConsumption`, `DeleteOverproducedItems` - tylko Armoury i BannerKings. ROT, RBL, BEE, NavalDLC,
DTE, Spoils, BKROTPatch tych celow nie lataja. Linia startowa wypisuje wlascicieli latek kazdego celu (`Harmony.GetPatchInfo`, jak E4/0 U25).

### 4.1 Otwarcie nawiasu - postfiks na `ItemConsumptionBehavior.UpdateDemandShift(Town, Dictionary<ItemCategory,float>)`
- Cel: G ItemConsumptionBehavior.cs:178-205 (private, instancyjna; petla po `ItemCategories.All` - JIT jej nie wbudowuje). Wolajacy: `MakeConsumptionInTown` :66 (tuz przed
  `MakeConsumption` :67) - dla miast z `DailyTickTown` :47-51 (`DailyTickTownEvent`, tylko `Campaign.AllTowns` - G Town.cs:295, Campaign.cs:546), dla zamkow z BK
  `TickCastle` refleksja (BK BKSettlementBehavior.cs:620-631); oraz `OnNewGameCreatedFollowUpEnd` :30-40 (10 x kazde miasto, `GameStarted` = false - W3).
- Dzis wlasciciele: **nikt** (skan DLL). Nasza: `public static void DemandPostfix(Town __0, Dictionary<ItemCategory,float> __1)`, priorytet domyslny.
- Cialo (`Live`): `_brTown = __0; _brDict = __1; _brOpen = true; _brTrack.Clear()`; indeks koszyka `Idx = sum(dict[c] x GetItemCategoryPriceIndex(c)) / sum(dict[c])` po
  kategoriach z dodatnim popytem (to sam BK liczy w `CalculateBudget`, BK EconomyPatches.cs:1097); budzet gry dnia osady `G0 = sum dodatnich dict` (ta sama definicja co
  `Measure169c.ConsPre`, K:Measure169c.cs:505-513); migawka `EconomicData.Satisfactions` (BK EconomicData.cs:62, publiczna tablica - **kopiowac wartosci**, nie
  referencje). Nic nie zapisuje do slownika ani gry.

### 4.2 Wiersze polki - postfiks `Priority.Last` na BK `EconomyPatches.CalculateBudget(Town, float, ItemCategory)`
- Cel: BK EconomyPatches.cs:1085-1100 (private static, klasa zewnetrzna; `AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget")` jak
  K:HistoricalPrices.cs:1004). Jedyny wolajacy w BK: prefiks konsumpcji :523, **przy kazdym wierszu polki**, z reszta budzetu kategorii (:552
  `categoryDemand[cat] = num2 - num3 x price`). Poza BK: `RawPrice.HouseBudget` refleksja (K:RawPrice.cs:231-234), wolana tylko z `RawPrice.Daily` (:343, blok dobowy
  K:ArmouryBehavior.cs:1486) - poza nawiasem.
- Dzis wlasciciele: `HistoricalPrices.BudgetPostfix` (priorytet domyslny 400, zmienia `__result` - K:HistoricalPrices.cs:832-857). Nasza:
  `public static void BudgetPostfix(Town __0, float __1, ItemCategory __2, float __result)` **Priority.Last** - widzi wynik po HistoricalPrices, ktorym BK liczy (`num2`).
- Cialo: tylko gdy `_brOpen && ReferenceEquals(__0, _brTown)`. Track kategorii: jesli `Pending` nie NaN -> `Spent += Pending - __1` (reszta po poprzednim wierszu =
  wejscie tego); `Rows++`; `Add += __result - __1` ("dodatek BK w petli" - czlon dobrobytu i indeksu doliczany przy kazdym wierszu, K1); `Pending = __result > 0.01f ?
  __result : NaN` (BK przetwarza wiersz tylko przy `num2 > 0.01`, :524). Wiersz bez budzetu nie zmienia slownika, wiec nie ma wydatku.
- Galaz dokupu jedzenia (BK :532-536: `num3` ponad budzet przy zapasie <= 10%) daje reszte ujemna - wzor liczy ja jako wydatek (zgodnie z `ConsumedValue`).

### 4.3 Zamkniecie nawiasu - postfiks `Priority.Last` + finalizer na `ItemConsumptionBehavior.MakeConsumption(Town, Dictionary<ItemCategory,float>, Dictionary<ItemCategory,int>)`
- Cel: G :142-176 (private static). Prefiks BK `ItemConsumptionPatch.Prefix` (BK :505-603) dla osady z PopulationData robi konsumpcje sam i zwraca `false` (:600) -
  postfiksy biegna zawsze (zasada 110, K:CastlePurse.cs:29-31).
- Dzis wlasciciele: BK prefiks bool; `Measure169c.ConsPre` (prefiks void, K:Measure169c.cs:105); `CastlePurse.ConsumePostfix` **First** (cofa "zakupy" zamku,
  K:CastlePurse.cs:251-265); `MoneyLedger.ConsumePostfix` (400, K:MoneyLedger.cs:537-549). Nasza: postfiks `ConsumePostfix(Town __0, Dictionary<ItemCategory,float> __1,
  Dictionary<ItemCategory,int> __2)` **Priority.Last** (po obu - nic nie zmienia, wiec kolejnosc nie ma wplywu na ich wynik) + `ConsumeFinalizer(Exception __exception)`
  (zamyka nawias takze przy wyjatku w cudzym kodzie; zwraca `__exception`).
- Cialo: dla Track z `Pending` -> `Spent += Pending - __1[cat]` (slownik to ten sam obiekt, ktory BK zmniejszal); `SpendGame += sum Spent`, `SpendGameFood += Spent kategorii
  jedzenia` (kategoria jedzenia = `Properties == BonusToFoodStores` jak model gry G DefaultSettlementFoodModel.cs:86, plus piwo i wino - id ROT sprawdzic przy kodzie,
  lista w linii sesji); `SpendGrp[g] += Spent` wedlug grupy robocizny kategorii (jedzenie; bron, zbroje, konie, zbytki - lista id w linii sesji; reszta - towar) - tylko
  udzialy do robocizny od towaru ludu (5.3 krok 3), nie do `SaleV` (to wydatek gry, nie zakup ludu);
  `ConsumedBk += EconomicData.ConsumedValue` (BK :513, :554 - wartosc tej konsumpcji); migawka `Satisfactions` po (Sat1); jedzenie z targu = suma `__2` (saleLog) kategorii
  jedzenia (to wpisze `SetSoldItems` i doda model zywnosci). Potem `_brOpen = false`.
- Osada bez PopulationData (BK prefiks zwraca `true`, vanilla liczy budzet modelem gry G :154): `BudgetPostfix` sie nie odpala - licznik "osad z konsumpcja bez BK n"
  (d120: 0); wydatek gry takich osad - brak (odczyt w linii).

### 4.4 Bitwy - prefiks + postfiks (odczyt) na `MapEventParty.CommitGoldChanges()`
- Cel: G MapEventParty.cs:499-524 (internal, instancyjna). Kwoty ustawia `MapEvent.CalculatePlunderedAndLostGoldAmounts` (G MapEvent.cs:1497-1526: `GoldLost`
  pokonanych z modelu, pula dzielona wedlug wkladu), a za statki zatopione dopisuje `LootDefeatedPartyShips` do `PlunderedGold` (G :1800-1817). Wyplata: wodz -
  `GiveGoldAction` null -> wodz (:512) i wodz -> null (:516); partia bez wodza z kiesa handlowa - `PartyTradeGold += Plundered - GoldLost` (:519-522); inna - nic.
- Dzis wlasciciele: `CirculationWindows.BattlePre` (prefiks **First**) + `FlagFin` (finalizer) - okno O14 (K:CirculationWindows.cs:651-662, wpiecie :1768).
  Przed wyplata kwoty zmieniaja: 3.2 `BeatenPurse.PlunderPostfix` (Last, model) i `SplitPostfix` (K:BeatenPurse.cs:471-486); 181 `PrizeFin` na `LootDefeatedPartyShips`
  (wylaczone - K:ShipsToPort181.cs:763-768); `Measure31.ShipsLootPre/Fin` liczy dopisek statkow (K:Measure31.cs:250-268, wpiecie :424). Wewnatrz `GiveGoldAction`:
  `RealPayer` (prefiks Low na `ApplyInternal`, dziala tylko z wlasnym kontekstem), nasluch `MoneyLedger.OnGoldTraded`.
- Nasza: `BattlePre(MapEventParty __instance, out BattleState __state)` (priorytet domyslny - po O14) i `BattlePost(MapEventParty __instance, BattleState __state)`.
  Prefiks: `Plundered`, `GoldLost`, wodz (`Party.LeaderHero`) i jego `Gold`, albo `PartyTradeGold` partii bez wodza. Postfiks: rzeczywista zmiana kiesy.
  Liczniki doby: a = zeszlo z kies wodzow pokonanych, b = zeszlo z kies partii bez wodza, c1 = dostali wodzowie zwyciezcow, c2 = partie z kiesa, c3 = udzial, ktory
  przepadl (partia bez kiesy handlowej - `PlunderedGold > 0` bez zmiany kiesy), d = `Measure31.DayShipBattleGold` (K:Measure31.cs:208) w `Lines`.
- Netto z niczego na ladzie = c1 + c2 - a - b - d (czesc statkow zamyka 181/B5; P R20). Kontrola: c1 = zrodla "bitwy" i a = ujscia "bitwy" w "Pieniadz swiata (bilans -
  przyczyny)" (GiveGoldAction w oknie O14 - L:31718 "bitwy 85 762", netto 45 147) +-2% (prog E50-T4).

### 4.5 Notable - prefiks + postfiks (odczyt) na `NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable(Hero)`
- Cel: G NotablePowerManagementBehavior.cs:47-61 (private; wola go dzienny tick notabla :35): ponad 10 500 -> `GiveGoldAction` notabl -> null (:52), ponizej 4 500 przy
  sile > 0 -> null -> notabl (:58). Dzis wlasciciele: `CirculationWindows.BandPre` First + `FlagFin` (O03, K:CirculationWindows.cs:533-546, :1757); A3 doda kontekst Low.
- Nasza: `NotablePre(Hero __0, out int __state)` (zloto przed) i `NotablePost(Hero __0, int __state)` (`Live`): delta < 0 -> "w nicosc", > 0 -> "z niczego".
  Miejsce handlu: `__0.CurrentSettlement ?? __0.HomeSettlement`; wies -> `Village.TradeBound` (G Village.cs:103), brak albo zamek -> `Village.Bound`. Pokrycie z D4:
  zapotrzebowanie "z niczego" sumowane na kase miasta/zamku handlu i porownane w `Lines` z nadwyzka tej kasy (miasto `gold - TownReserve`, zamek `gold - Reserve`) -
  "kasa mogla dac e z d". Liczba notabli wiejskich / wszystkich (P: 2 379 z ok. 2 681, L:31735 / L:31727).

### 4.6 Nasluchy (bez Harmony)
- `CampaignEvents.OnItemProducedEvent` (G CampaignEvents.cs:981) - `TownFolk.OnItemProduced(ItemObject, Settlement, int)`: tylko `IsTown || IsCastle` (wies tez wola to
  zdarzenie - G VillageGoodProductionCampaignBehavior.cs:168, BK EconomyPatches.cs:972 - pomijamy); wartosc = sztuki x cena targu osady (`Town.GetItemPrice`). Zrodla:
  warsztaty gry i BK (BK EconomyPatches.cs:735, BK WorkshopData.cs:75), nasze warsztaty zbrojne (K:WorkshopLaw.cs:326). Wartosc wyrobow -> place 35% (KL L7).
- `TownFolk.OnGold` (z `MoneyLedger.OnGoldTraded`, po normalizacji kierunku K:MoneyLedger.cs:297-303; zdarzenie gra wysyla PO przelewie - G GiveGoldAction.cs `ApplyInternal`,
  `OnHeroOrPartyTradedGold` na koncu - wiec kasa w chwili wywolania jest juz "po"): (a) **karawana albo gracz <-> miasto** poza oknem flagowym
  (`CirculationWindows.CurrentKind == 0`, K:CirculationWindows.cs:161 - wyklucza kapital nowych karawan, zold, bitwy): karawana/gracz -> miasto = zakup (`Sale`, robocizna
  ladowania 10% + obrot P 3%), miasto -> karawana/gracz = sprzedaz do miasta (tylko robocizna rozladunku 10%, KL L5 i 5.2); handel karawan idzie przez
  `SellItemsAction.ApplyInternal` -> `GiveGoldAction` (G SellItemsAction.cs:60-101); (b) **przelew do/z kiesy wsi** (obie strony niepuste i `!_inBlock` - ta sama definicja co "przelewy gry" ksiegi, K:MoneyLedger.cs:335-337) - na wies (G wsi wedlug polityki);
  (c) **kasa miasta placi** (osada -> ktos) - czesc ponizej zapasu kupcow: karawana -> "gry/BK (karawany)", reszta -> "inne przelewy" (w tym BK niewolnicy 3.4,
  K:BkSeal34.cs:225; RealPayer placi z `Seal31.Spare`, wiec z definicji 0 ponizej zapasu).
- `TownFolk.NoteCartPaid(st, paid)` / `NoteVillageReturn(home, handed, kept, tax, estates)` - z okna `MoneyLedger.OnBeforeEntered/OnAfterEntered` (K:MoneyLedger.cs:222-262):
  te same liczby co "Przeplywy osad" (jedna definicja utargu).
- `TownFolk.OnTownCoin(st, amount, src)` - czesc platnosci kasy MIASTA ponizej zapasu kupcow: `below = max(0, min(-amount, TownReserve - gold_teraz))` (gold_teraz =
  po przelewie: wpiecia stoja po `ChangeGold`). Kwoty dodatnie i zamki - nic.

### 4.7 Sonda korony `CrownProbe()` (przed `CrownIncome.End`)
Dla kazdego krolestwa `CrownIncome.DayOf(k)` (K:CrownIncome.cs:108-113; pola `KDay` :43-51): `Measured` (= wplywy dnia bez 1/360 - K:CrownIncome.cs:184), `Release`,
`RefundDue`, `LoanIn` (kredyt 186 dzis), skarbiec `KingdomBudgetWallet`, wojna. Pierscienie sesji: 7 dob "kredyt > 0", 28 dob `RefundDue`. Potrzeba R15 (b) "na probe":
kredyt w 7 dobach albo `RefundDue > Measured` albo skarbiec < zapas wojny = `max(CrownReserveGold, 60 x sr28 RefundDue)` (K:Settings.cs:841; `WarTaxReserveDays` 60 - B2).
Tu tez danina z kas miast ponizej zapasu: `KingdomTreasury.TownLevyToday[st]` (K:KingdomTreasury.cs:60, ustawiane w `Levies` :401) i kasa teraz (miedzy `Levies` :1499
a sonda nikt nie rusza kas miast) -> `below = max(0, min(x, TownReserve - gold))`.

### 4.8 Kolejnosc w dobie (K:ArmouryBehavior.cs)
`Seal31.BlockOpen` :1459 -> `MoneyLedger.BlockOpen` :1460 (zeruje `Last*`, w tym `PopulationLaw.ZeroDay169`) -> `PopulationLaw.Daily` :1461 (renty; `RentOf` czytelne do
nastepnego `BlockOpen`, K:PopulationLaw.cs:1015-1019) -> `Seal31B.OwnerTaxDaily/RentMerge` -> `CastlePurse.Daily` :1467 -> `Mark(MCastle)` :1468 -> **`TownFolk.Daily`**
-> `BuildFunding` :1471 -> ... -> `KingdomTreasury.Levies` :1499 -> ... -> `WageRefund` :1509 -> `CrownRents` :1510 -> **`TownFolk.CrownProbe`** -> `CrownIncome.End` :1511 -> ...
-> `ClanBudget.Daily` :1522 -> ... -> `MoneyLedger.Daily` :1550 (w nim "Gotowka wsi (E5/0)"; `ClearDay` w `finally`) -> **`TownFolk.Lines`** -> `BeatenPurse.Daily` :1553
-> `Seal31.Daily` :1554 (zeruje Measure31). `TownFolk.Daily` nie rusza zlota, wiec `Mark` przed i po nie widzi zmiany.

---

## 5. Model "na sucho" (`TownFolk.Daily`)

### 5.1 Stale (sim_kiesa2.py P0 :50-80, KL 1.3-1.5; w kodzie z komentarzem "KL/sim2 P0 - klucze w B1")
koszyk towaru na glowe `b` W 0.27 / R 0.50 / P 1.46 / niewolny 0.20 / podzamcze 0.18 / wies 0.07; jedzenie w koszyku `fd` W 0.80 / R 0.70 / P 0.45 / podzamcze 0.80;
czynsz W 8% / R 13% (polowa notable, polowa P), leczenie 1/2/2% (do R), sluzba P 6% (do W); zapas dni W 10 / R 21 / P 30 / podzamcze 10 / wies 15; nadwyzka 7%/d
(W -> towar; R, P -> 70% place, 30% towar); podatek pana 1/2/3% (podzamcze 3%), cech 1% R, akcyza 2% (tylko przy towarze - R11 v2); korona R15 przy s = 3%: W 0.5s,
R 1.0s, P 1.5s, podzamcze 1.0s (tylko krolestwo w wojnie); gmina 7%/d (1/3 farma panu, 2/3 roboty: 75% place W50/R50, 25% material); robocizna handlu 20/10/4%
(jedzenie / towar / bron-konie-zbytki), podzial robocizny W 90 / R 10; obrot P 3%; oplaty wsi 4 zl/rok na wolna glowe; wies 65% utargu (R13; pan 35% z majatkami);
danina wsi 1.5%/d nadwyzki ponad 15 dni zakupow (x2 War Tax; tylko w wojnie); niewolni w krolestwie niewolniczym: 18.8% dochodu W do wlascicieli R 40 / P 60.

### 5.2 Rodzaje przeplywow (wpiecia rozdz. 2.2) -> lud
| Rodzaj | Zrodlo | Do ludu | Podzial W/R/P | Czesc kupcow (Sale, mix j/t/b) |
|---|---|---|---|---|
| KLife, KLifeCaravan, KLifeBand | MenPurse:299, Seal31B:454, OutlawLaw:1773 | 25% (KL 1.3) | 80/10/10 | 75% (1, 0, 0) |
| KGarrison | SoldierPay:473 | 25% (miasto) / podzamcze 25% (zamek) | 80/10/10 | 75% (1, 0, 0) |
| KCourt | ClanBudget:621 | 20% (podzamcze przy siedzibie-zamku) | 80/20/0 | 80% (0.5, 0.2, 0.3) |
| KBuildLabour | BuildFunding:185 | 100% (to juz place 75% budowy) | 60/40/0 | - |
| KMine | BuildFunding:98 | 100% | W | - |
| place rzemiosla (L7) | `OnItemProduced` x cena | 35% wartosci wyrobow osady | 70/30/0 | - (kontrola: KCraftPaid = place juz wyplacone; KCraftFair, KShopKeep - odczyt) |
| Sale (MixBuild, MixGoods, MixGear, MixCloth, MixFood) | BuildFunding:182, WorkshopLaw:453, AiGear:705/:747, MenUpgrade:447, ArmyClothing:181/:238, BkSupplyMarket:172 | robocizna wedlug mixu | 90/10/0 | obrot P 3% |
| karawany / gracz (OnGold a) | GiveGoldAction | robocizna 10% (+ obrot P 3% przy zakupie) | 90/10/0 | - |
| towar ludu (krok 3) | na sucho | robocizna wedlug grup wydatku gry osady (5.3) - na jutro | 90/10/0 | obrot P 3% |
| zysk kupcow 5% zaworu (L6) | - | **0 - A1** (w linii "- (A1)") | - | - |
| KReleased | ClanBudget:759 | kiesa wsi (100%) | - | - |
Nie sa dochodem ludu (KL 5.2 "Nie"): lup, jency, okupy, przelewy gry bez towaru, werbunek/najem (`LevyGold`), paser band.

### 5.3 Doba osady (port sim :430-480 z krolestwa na osade)
Miasto (kolejnosc):
1. **Dochod okna** z 5.2 i `NextIn` z wczoraj (czynsz, leczenie, sluzba, praca u bogatszych, robocizna od wczorajszego towaru ludu, roboty gminy); w krolestwie
   niewolniczym 18.8% czesci W do R/P (sim :271-279).
2. **Podatki przy dochodzie:** pan 1/2/3% (do glowy rodu pana - tylko licznik), cech 1% R -> gmina, korona R15 (wojna, s = 3%) -> licznik T.
3. **Koszyk i kotwica (R1, KL L3):** `B = sum_c Heads_c x r x b_c x Idx` (+ utrzymanie niewolnych u R/P w krolestwie niewolniczym, sim :168-171); wydatek gry
   `W = SpendGame` okna (rozdz. 4.3); towar ludu `G = min(W, B)`, `m = G / W` (kotwica - B1 bedzie mnozyc nia budzet dnia; P R1 v2: "wobec wydatku, nie budzetu");
   klasa: `goods_c = B_c x G / B`, `need_c = goods_c / keep_c` (keep = 1 - czynsz - leczenie - sluzba, sim :201); `f_c = min(1, L_c / need_c)`, `food_c =
   min(1, L_c / (need_c x fd_c))`; zakup `gd_c = f_c x goods_c`; akcyza 2% -> gmina; reszta -> "zaplacone kupcom" (do szacunku kas po B1); czynsz, leczenie, sluzba -> `NextIn`;
   "zjedzone bez zaplaty" `A = sum_c (1 - f_c) x goods_c` (towar, ktory gra zje, a lud nie zaplaci - R1 "do B1 caly"). Robocizna od towaru ludu = `gd` x stawka wedlug
   udzialu grup w wydatku gry osady (jedzenie 20 / towar 10 / bron-zbytki 4) -> `NextIn` (sim "nxt").
4. **Zapas i nadwyzka:** zapas `rd_c x need_c`; 7%/d ponad zapas: W -> towar (robocizna jutro), R -> 70% place W, P -> 70% place W60/R40, 30% towar (bez lokaty -
   "lokata: - (B1)"; bez polki - "na sucho bez ograniczenia polki").
5. **Gmina:** 7% stanu: 1/3 farma (licznik do pana), 2/3 roboty (75% place W50/R50 -> `NextIn`, 25% material -> robocizna).
6. **Kontrola zamknieta:** `dochod - podatki - towar - czynsz/leczenie/sluzba - nadwyzka (wydana) - zmiana kies = 0` (T3); wewnetrzne przelewy wchodza po obu stronach.
Zamek (podzamcze, R12): dochod = 25% zalogi, 20% dworu (siedziba-zamek), place budowy w zamku, **15% zaworu kasy zamku** (`CastlePurse.LordDuesToday` jest na rod -
zawor zamku z kasy przed: `CastlePurse.Dues(gold_przed, Reserve, CastleDuesShare)` - gold_przed nie jest znany po `Daily`, wiec E5/0 liczy szacunek `zawor = s/(1-s) x
max(0, gold - Reserve)` dla zamku niezoblezonego przy `CastlePurseEnabled` (K:CastlePurse.cs:202-208 `Dues`; miedzy `CastlePurse.Daily` a `TownFolk.Daily` nic nie rusza
kas zamkow) - z dopiskiem "szacunek"); wydatek: towar 0.18 zl/glowe x Idx zamku (z kasy zamku - B1 "placi za to, co dzis zamki zjadaja bez zaplaty"), pan 3%, korona 1.0s,
zapas 10 dni, nadwyzka 7% -> towar. Wydatek gry zamku (SpendGame) = "zjedzone bez zaplaty dzis" (L:31541 sr28 50 105) - porownanie w linii.
Wies (R13): dochod = 65% utargu okna (`NoteVillageReturn.handed`) + przelewy gry do wsi (`OnGold` b) + KReleased; wydatki: oplaty 4 zl/rok x wolne glowy (D1) / 364
(najwyzej stan), zakupy `0.07 x wolne glowy x r` (najwyzej stan; towar w miescie targowym - robocizna MixVillage (0.2, 0.8, 0) do miasta `TradeBound`), danina korony
(wojna) 1.5%/d nadwyzki ponad 15 dni zakupow, nadwyzka 7%/d ponad 15 dni -> dodatkowe zakupy. Zapas startowy 15 dni zakupow.
**Kasy miast po B1 (szacunek):** `K = sum(zaplacone kupcom przez lud i wsie) - sum(robocizna, place 35%, obrot P, czesc ludu z zycia/zalog/dworu) - "zakupy" z niczego okna
(= ConsumedBk miast)` - jedna liczba w linii (do rachunku przed testem B, P ryzyko 1).

---

## 6. Klucz (Settings.cs + gen_mcm)

Nowa grupa po K:Settings.cs:1287:
```
        // --- The townsfolk's purse ---
        // E5/0 (195, projekt etapu 5 rozdz. 3): kiesa ludu na sucho - tylko log; kiesa czynna dopiero kluczem TownFolkLive (B1, K3)
        public int TownFolkMode = 1;                       // 0 off; 1 = the townsfolk's purse is counted every day from the real flows (wages, services, the basket by the people of each town) and written to the log; it changes nothing in the game unless Town Folk Live is on
```
Tekst podpowiedzi - doslownie P "Klucz Settings" (bez "2 = live", K3). `T:gen_mcm.py` `RANGES`: `'TownFolkMode': (0, 1, "0"),` (komentarz `# E5/0: tryb kiesy ludu 0..1`).
**Armoury.json Jeffa** (`Documents\...\Configs\ModSettings\Global\Armoury\Armoury.json`, 9 875 B z 08.10): `grep '"TownFolk'` 11.10 - brak (z kluczy etapu 5 tylko
`WeeksPerSeason`) - domyslna 1 zadziala; przy wgraniu powtorzyc grep w pliku Jeffa i autotestu (CLAUDE.md 7). `Armoury.settings.xml` modulu: brak pola = domyslna
(`Settings.Load`, K:Settings.cs:1289-1302).

---

## 7. Linie logu (raz na dobe przy `TownFolk.On`; napisy logu po polsku jak wszystkie linie pomiaru; liczby `InvariantCulture`)

### 7.1 Startowa (menu glowne, `ApplyAll`)
`Kiesa ludu (E5/0): okna wpiete: budzet dnia (UpdateDemandShift), wiersze polki BK (CalculateBudget, po HistoricalPrices), konsumpcja (MakeConsumption 2/2), bitwy (CommitGoldChanges 2/2), pasmo notabli (BalanceGoldAndPowerOfNotable 2/2); nasluchy: wyroby, przelewy gry, tabory wsi - w ksiedze pieniadza; BRAK: -; wlasciciele latek celow: <cel: id Harmony, ...>; tryb przy starcie N (stan gry - linia sesji).`

### 7.2 Sesji (pierwsza doba sesji z kalibracja)
`Kiesa ludu (E5/0): sesja od dnia D - tryb na sucho (TownFolkMode 1) | kalibracja D1: osad z danymi BK n (miasta a, zamki b, wsie c), bez danych m (mediana skali BK/ksiega) | ludzie BK miast X (W / R / P / niewolni), ksiega miast Y | krolestwa niewolnicze: dopasowane n z 8 (nazwy) | kategorie jedzenia: lista | modele czynne: lojalnosc X (BKLoyaltyModel: tak/nie), dobrobyt Y (postfiksow warstwy BK "BannerKings.BetterEconomyLayering" na lancuchu: k), zywnosc Z (liczy jedzenie z targu: tak/nie) | zapasy startowe na sucho S zl (W a / R b / P c / podzamcze d / wsie e).`
Modele czynne (RB 7 pkt 2 i 4): typ `Campaign.Current.Models.SettlementLoyaltyModel` / `SettlementProsperityModel` / `SettlementFoodModel`; k = liczba metod
`CalculateProsperityChange` w lancuchu klas czynnego modelu, ktore maja postfiks wlasciciela "BannerKings.BetterEconomyLayering" (BK BKEconomyLayerInstaller.cs:452-481 -
latka na KAZDY model pochodny od Default; RBL wola base -> k = 2 = delty BK liczone dwa razy); zywnosc "tak" gdy `DefaultSettlementFoodModel` w lancuchu.

### 7.3 Dzienna "Kiesa ludu (na sucho)" (w `Lines`, z liczb `Daily`)
`Kiesa ludu (na sucho): dzien N | miasta n: ludzie ksiegi X (BK z kalibracji x ksiega: W a / R b / P c), koszyk ludu B, wydatek gry W (jedzenie Wf), budzet gry G0 (dodatek BK w petli G1), kotwica m srednio x (min y, miast z m < 1: k) | dochod Y: wojsko y1 (zycie y1a, karawany y1b, bandy y1c, zalogi y1d), dwor y2, rzemioslo i budowy y3 (place 35% wyrobow y3a z wartosci V, juz wyplacone y3k; budowy y3b; kopalnie y3c; roboty gminy y3d), handel y4 (robocizna y4a, obrot P y4b), karawany y5, zysk kupcow - (A1), wewnatrz ludu y6 | wydatki Z: towar z1, czynsz z2, leczenie i sluzba z3, podatki z4 (pan p1, cech p2, korona p3) | kiesy X (W / R / P), zapas dni W d1 / R d2 / P d3 | stac jedzenie W/R/P %, pelny W/R/P %, miast ze stac jedzenie < 95%: n1, pelny < 95%: n2 | zjedzone bez zaplaty A (q% wydatku gry) | nadwyzka 7%: towar t1, place t2 | gmina: stan g, akcyza a, cech c, farma f, roboty r | podzamcze n: ludzie, koszyk, dochod (zaloga, dwor, budowy, 15% zaworu - szacunek), towar, podatki, kiesa, stac %, wydatek gry zamkow (dzis zjedzone bez zaplaty) | wsie n: wolne glowy, dochod (65% utargu, przelewy, sakiewki zwolnionych), zakupy B z C (%), nadwyzka D, oplaty panu F, danina G, kiesy (mediana, < 1 000 zl: k) | podatek wojenny przy s = 3%: T (krolestw w wojnie k) | kasy miast po B1 (szacunek): K | kontrola: r (q% dochodu) | konsumpcja: wydatek z petli BK S wobec ConsumedValue C (roznica q%), osad z konsumpcja bez BK n | czas: latki a ms, doba b ms | potkniecia P.`
Dopisek "doba niepelna (pierwsza w sesji)" w pierwszej dobie sesji.

### 7.4 "Nadwyzka kas (E5/0)"
`Nadwyzka kas (E5/0): dzien N | miasta: kasy X, zapas kupcow (D4) R, ponad zapasem O w k miastach, ponizej dna 20 000: n, pod tarczami H (zold h1, dwor h2) | zamki: kasy, zapas (CastlePurse.Reserve), ponad | z pasa ponizej zapasu (miasta): renta x1 z X1, danina x2 z X2, polityki/Road Tolls/Spoils - bez rozbicia (A1), RealPayer i porty 0 z definicji (Seal31.Spare) | zakupy kas miast z pasa ponizej zapasu: nasze sciezki X (po A1 = 0), gry/BK Y (tabory y1, karawany y2), inne przelewy Z | K3: ciecie zwrotu dzis C (CrownRefundOwnTownsCut), przy A1 ok. C x 0.67, czesc pana z zaworu A1 (7% D4 x 0.67) W | korona: kredyt 186 dzis L (krolestw a, w 7 dobach b), zwrot nalezny ponad wplywy (bez 1/360) U (krolestw c), zapas wojny (max(500 000, 60 dob sr28 zwrotu)) razem Q, krolestw ze skarbcem ponizej: d (w wojnie e), potrzeba R15 dzis: f krolestw.`
Renta z pasa (liczona w `Daily`, gdy `RentOf` jest jeszcze czytelne): `RentOf(st, out pay, out avail)` (K:PopulationLaw.cs:1015-1019; avail = kasa - 20 000 przed renta) -> `below = max(0, min(pay, R - (avail + 20 000 - pay)))`.

### 7.5 "Bitwy", "Notable", "Jedzenie z targu", "Zadowolenie BK"
- `Bitwy (E5/0): dzien N | partii n (wodzowie n1, bez wodza n2) | pula z pokonanych (zeszlo z kies): wodzowie a, partie bez wodza b | zwyciezcy dostali c (wodzowie c1, partie z kiesa c2), przepadlo c3 | statki zatopione d | z niczego netto na ladzie e = c - a - b - d (morze d osobno) | kontrola z ksiega: zrodla "bitwy" x (c1 - x = ...), ujscia y (a - y = ...).`
- `Notable (E5/0): dzien N | pasmo 4 500-10 500 - miasta: w nicosc a / z niczego b (kasa miasta mogla dac b1); wsie: w nicosc c / z niczego d (kasa miasta targowego mogla dac e; targ - zamek: f notabli) | notable wiejscy n z N | kontrola z ksiega (Obieg: notable): w nicosc a+c wobec x, z niczego b+d wobec y.`
- `Jedzenie z targu (E5/0): dzien N | do zapasu warowni z konsumpcji: miasta a, zamki b (szt.; SoldItems jedzenia) | zapas warowni (FoodStocks) razem F, warowni n | zamki: jedzenie na polce targu d szt., zuzycie podzamcza e szt./d (srednia 7 dob), polka < 10 dni zuzycia: f z 130 (mediana g dni; zamki bez zuzycia h).`
- `Zadowolenie BK (E5/0): dzien N | srednie Food / Industrial / General / Luxury (0..1) miasta x / zamki y, osad ponizej 0.85: n (grupa) | zmiana dnia w konsumpcji: + suma / - suma wedlug grupy (BK +0.001 za wiersz z budzetem, -0.0015 tylko w dokupie jedzenia - przyblizone przez obciecie 0..1).`

### 7.6 "Gotowka wsi (E5/0)" (MoneyLedger.Obieg.cs, nowa `VillageCashLine`, w `MoneyLedger.Daily` przed `ClearDay`)
`Gotowka wsi (E5/0): dzien N | wplynelo G = utarg u + przelewy gry p + sakiewki zwolnionych s | pan (licznik podatku wsi + renta) x%, majatki BK y%, korona z%, wies (zostalo w kiesie) w% | wedlug polityki BK (utarg na wies z TownFolk): niska / zwykla / wysoka / zwolnienie - wsi, G, pan x%, pan z majatkami x2%, w utargu: pan / majatki / wies.`
Liczby swiata z tych samych licznikow co "Przeplywy osad (kiesy wsi)" (`_vHanded`, `_vTax`, `_vKept`, `_vEstates`, przelewy klasy wsi, `ClanBudget.LastReleasedPurse`,
`PopulationLaw.DayVillageRent`, korona klasy wsi) - definicja RA rozdz. 4 (dzis 86.2% / 95.5% / 97.4%). Podzial wedlug polityki: `TownFolk.VillagePolicySplit` (okno
`NoteVillageReturn` + `RentOf` wsi + `OnGold` (b) + KReleased; korona - tylko razem).

### 7.7 Dopiski w istniejacych liniach
- "Obieg" (K:MoneyLedger.Obieg.cs:564-567, odcinek "sakiewki ludzi [P]"), na koncu odcinka, tylko przy `TownFolk.On`: `, = D dni zoldu naliczonego (partie i zalogi)` z `stan /
  (lordAcc + garAcc)` (K:MoneyLedger.Obieg.cs:534; RA: 22.55 mln / 768.9 tys. = 29.3 dnia).
- K:MoneyLedger.Obieg.cs:654 - napis "po planowanej zmianie: zold do sakiewek ludzi karawan" (nieaktualny - P "Pogodzone" ostatni wiersz) -> "zold idzie do sakiewek
  ludzi karawan (E3/4), karawana wydaje je w miescie". Narzedzia tego tekstu nie czytaja (grep T: 11.10). Poprawka napisu - bez wylacznika.

### 7.8 CSV `Logs\<sesja>\kiesa-ludu.csv` (`Log.Csv`, K:Log.cs:78-93; co 7 dob kampanii i w pierwszej pelnej dobie)
`dzien;dzien_gry;osada_id;osada;rodzaj;krolestwo;kultura;polityka_bk;ludzie_ksiegi;ludzie_bk_kal;skala_r;W;R;P;niewolni;koszyk;wydatek_gry;wydatek_jedzenie;m;dochod;wydatki;kiesa_W;kiesa_R;kiesa_P;stac_jedzenie;stac_pelny;kasa;zapas_D4;zjedzone_bez_zaplaty`

---

## 8. Progi - `T:sprawdz_logi.py --grupa etap5` (szkielet + blok E5/0)

Szkielet: `etap5_main(logp, start_arg, until=120)` - start kampanii jak `etap4_main` (linia "Pomiary 169c: okna ... start kampanii dzien N", inaczej pierwsza doba;
T:sprawdz_logi.py:557-567), okno dob 1..until (`--do-doby`), dispatch blokow po liniach startowych: `etap5_e50` (linia "Kiesa ludu (E5/0): okna"); miejsca na `etap5_a1` ...
(komentarz: "kolejne paczki dopisuja bloki; bieg B - kontrola wartosci czynnych K3 w bloku B1"); w `main` (T:sprawdz_logi.py:1670) `if group == "etap5" and logp: return
etap5_main(...)`; docstring `--grupa etap5`. Wzor bloku - `etap4_e46` (T:sprawdz_logi.py:1518-1560). Doby niepelne (pierwsze w sesji) poza progami.

| # | Prog | Jak | Werdykt |
|---|---|---|---|
| E50-T1 | 0 `ERROR in TownFolk`; linia startowa "BRAK: -"; potkniecia <= 5 w biegu (z nazwa miejsca); linia sesji jest | log | TAK/NIE |
| E50-T2 | koszt `czas` latki + doba: srednia <= 15 ms/dobe, max <= 50 ms (P T2; liczone na wywolaniach na wiersz polki) | "Kiesa ludu" | TAK/NIE (max - INFO) |
| E50-T3 | kontrola na sucho \|r\| <= 1% dochodu kazdej pelnej doby | "Kiesa ludu" | TAK/NIE |
| E50-T4 | rozbicie bitew: c1 i a wobec ksiegi "bitwy" +-2% (sr28); **A6**: netto na ladzie e sr28 > 5 000/d -> "A6 POTRZEBNE" | "Bitwy (E5/0)", "Pieniadz swiata (bilans - przyczyny)" | TAK/NIE + DECYZJA |
| E50-T5 | wydatek z petli BK wobec `ConsumedValue` +-2% kazdej doby (K1: budzet raz na kategorie); osad z konsumpcja bez BK - INFO | "Kiesa ludu" | TAK/NIE |
| E50-T6 | notable: a+c i b+d wobec "Obieg: notable" +-1% (sr28) | "Notable (E5/0)" | TAK/NIE |
| E50-T7 | gotowka wsi: suma wsi wedlug polityki (utarg, przelewy, sakiewki) = liczby swiata tej samej linii (+-1 zl na dobe; jedna definicja z "Przeplywy osad"); udzial pana (licznik + renta) sr28 - INFO (dzis 86%) | "Gotowka wsi (E5/0)" | TAK/NIE + INFO |
| E50-T8 | gra bez zmian: reszta ksiegi pieniadza < 10 tys./d - z `--grupa etap3` na tym samym logu (INFO tutaj); w tescie koncowym E4 progi E4 bez zmian wobec biegu bez E5/0 nie sa mierzalne (przebieg niepowtarzalny) - recenzja diffu "tylko odczyt" (rozdz. 10.1) | etap3 | INFO |
| E50-T9 | odczyty do kalibracji B (bez progu, sr28 i d_ostatnia): stac jedzenie / pelny (min osady, swiat), miast < 95%, m (srednia, min), zjedzone bez zaplaty %, kasy po B1 K, sakiewki w dniach (dzis 29), nadwyzka D4 i pas (nasze / gry/BK), K3 C wobec W, potrzeba korony, zamki z polka < 10 dni (f z 130, mediana), zadowolenie BK (srednie, n < 0.85), modele czynne (lojalnosc BK, k warstwy BK), gotowka wsi wedlug polityki | wszystkie linie E5/0 | INFO |
| E50-T10 | zapis (9 dob): linia sesji po wczytaniu, kalibracja od nowa, 0 bledow | log biegu z zapisu | TAK/NIE |

Konwencja blokow (zgodna z PLAN-KODU-E5A1.md 9.1): `etap5_<paczka>(rep, lines, start, win, **kw)` (kw: `until`, `logp`), kazdy rusza tylko przy swojej linii startowej,
brak linii = wiersz INFO "brak linii startowej", nigdy wyjatek.

### 8.1 Wiersze etapu w szkielecie (P rozdz. 4) - w E5/0 INFO "stan wyjsciowy"; paczki, ktore je wiaza, zmieniaja werdykt w swoim bloku
| Wiersz | Zrodlo | Wiaze |
|---|---|---|
| panowie samych zamkow: udzial w D swiata (srednia d252-364, gdy bieg je ma, inaczej ostatnie 28 dob), udzial w D stalym, mediana kiesy glowy - srednia 4 blokow 28 dob, tylko rody z `wojna` >= 21 z 28 dob | `budzet-rodow.csv` (`csv_path_of`, T:sprawdz_logi.py:104; kolumny `rodzaj`=zamek, `D`, `d_staly`, `kiesa_glowy`, `wojna`; wzor `SCR\e5A\csv.py`) | B3 |
| sakiewki zolnierzy <= 15 dni zoldu | "Obieg" dopisek (7.7) | A3 |
| zloto swiata < 0.1% na 28 dob | "Pieniadz swiata" | C1 |
| zatkane magazyny wsi zamkowych (lato / zima osobno) | "Dowoz (skutki)" sr28 (L:31610) | B4 |
| miasta kupuja >= 98% plonu | "Przeplywy osad" (towar niesprzedany po wizycie) | kazdy zbior |
| wojsko lordow w wojnie 95-115 tys. (podloga 85) | "Ludzie:" partie rodow (L:31742) | kazdy zbior |
| stac jedzenie / pelny, zjedzone bez zaplaty | "Kiesa ludu (na sucho)" (odczyt; od B1 "Kiesa ludu:") | B1 |
| pan w utargu i w gotowce wsi wedlug polityki | "Gotowka wsi (E5/0)" | B1, C3 |

---

## 9. Test (P E5/0 "Test", K7)

Jedzie w **tescie koncowym etapu 4** (364 doby + bieg zimowy 120 + zapis 9 dob; gra bez zmian - nie psuje diagnozy E4). Przed nim: autotest 40 dob nowej kampanii
na drzewie scalonym (0 bledow naszych modow, linie E5/0 kazdej doby, E50-T1/T2/T3/T5). Z biegu E4 bierzemy tylko: bledy, koszt, rozbicie bitew (decyzja A6), gotowke wsi
i kontrole (T1-T7, T10); **stawki B1-B4 kalibrujemy z linii E5/0 biegu A zbioru A** (po A1 i A3 - dochod z markietanow, zapasy startowe, zysk kupcow zaleza od A1/A3).
Nie przechodzi -> `TownFolkMode = 0` w `Armoury.json` autotestu (stan sprzed paczki) i poprawka.

---

## 10. Kontrola calosci (zasada 0; do "Ryzyko / co sprawdzic" w CHANGELOG)

### 10.1 Regresje - "tylko odczyt" (lista do recenzji diffu)
Kazda latka: void/finalizer, bez `ref`, bez `__result` do zapisu, bez `return false`. Zakazane wywolania w calej paczce: `ChangeGold`, `ChangeHeroGold`, `GiveGoldAction`,
`AddToCounts`, `SetSoldItems`, `UpdateSatisfaction`, `PeopleOf` przed `Calibrated`, `GetPopData` bez `IsSettlementPopulated`, `CastlePurse.Spendable`, `Seal31.Spare`,
`Town.FoodChange`, zapis do `Satisfactions` (kopiowac tablice). `MoneyLedger`: zadnych nowych `Marks`/`Notes`/`Holders` - wywolania `TownFolk.*` nie zmieniaja jego
licznikow (`Note` liczy jak dotad - wpiecie przed `_inBlock`, ale samo nic w MoneyLedger nie zmienia).

### 10.2 Wspolne cele i stan
| Cel / stan | Dzis | E5/0 | Pilnowac |
|---|---|---|---|
| `UpdateDemandShift` | nikt (skan DLL) | postfiks (otwarcie nawiasu) | bramka `GameStarted` (W3); nawias zamyka finalizer |
| BK `CalculateBudget` | `HistoricalPrices.BudgetPostfix` (400, zmienia wynik) | postfiks Last, tylko w nawiasie | `RawPrice.HouseBudget` poza nawiasem; nic nie mnozy (m^k - K1) |
| `MakeConsumption` | BK prefiks false, `Measure169c.ConsPre`, `CastlePurse.ConsumePostfix` First, `MoneyLedger.ConsumePostfix` | postfiks Last + finalizer | zamek: CastlePurse cofa "zakupy" przed nami - E5/0 bierze `ConsumedValue` BK, nie zmiane kasy |
| `CommitGoldChanges` | O14 First + `FlagFin`; kwoty: 3.2, 181 (OFF), Measure31 | prefiks + postfiks | O14 i ksiega widza to samo; E5/0 nie otwiera okna flagowego |
| `BalanceGoldAndPowerOfNotable` | O03 First + `FlagFin`; A3 (kontekst Low) | prefiks + postfiks | po A3 linia pokaze z niczego 0 (miara A3) |
| `MoneyLedger.OnAfterEntered/OnGoldTraded/Note/Note169` | ksiega | jedno wywolanie `TownFolk.*` w kazdym | wywolanie w `try`, przed wczesnymi `return`, po normalizacji |
| `CrownIncome.DayOf` | 165 | odczyt przed `End` | `_open` (K:CrownIncome.cs:56) |
| `PopulationLaw.RentOf`, `KingdomTreasury.TownLevyToday`, `SoldierPay.HeldOf/CourtOf`, `Measure31.DayShipBattleGold`, `CastlePurse.Reserve`, `Seal31.TownReserve` | odczyty | odczyt | czytane przed zerowaniem (rozdz. 4.8) |

### 10.3 Kolizje z innymi zmianami (scalanie)
- E4/8: `ClanBudget.cs` (inne metody niz :621/:759), `PopulationLaw.Daily` (E5/0 nie zmienia), `CreateCaravanParty` (E5/0 nie lata). E4/10: `ClanBudget.cs:749-759`
  (wpiecie KReleased zostaje przy przelewie do wsi; czesc zwolnionych pojdzie do karczmy - ich sakiewka to B6), `LevyGold` (E5/0 nie liczy najmu jako dochodu ludu).
  E4/11: model zywnosci - E5/0 go nie wola. A1: te same pliki (`MoneyLedger*`, `ArmouryBehavior`, `SubModuleMain`, `Settings`, `McmSettings`), A1 zmienia
  `PopulationLaw` (renta miast -> `TownPurse`) - po A1 `RentOf` miast moze byc puste: linia 7.4 czyta wtedy zawor A1 (wpis przy scaleniu; do tego czasu "renta x1").
  `McmSettings.cs` zawsze od nowa `python tools/gen_mcm.py` po scaleniu.
- Nie liczy drugi raz: wydatek gry to jedna liczba z petli BK (nie suma `__result` - K1); dochod ludu z zycia liczony raz (karawana nie przechodzi `MenPurse:299` - K:MenPurse.cs:266
  `CaravanSpends` zwraca wczesniej); place warsztatow juz wyplacone (KCraftPaid) tylko kontrola, plac 35% liczy wartosc wyrobow (L7 "pomniejszone o wyplacone" - w B1);
  danina korony - tylko odczyt `TownLevyToday`.
- Martwy kod / zywy: hak `CastleDuesSuburbShare` i `VillageRentYieldsToFees` (K:CastlePurse.cs:92, K:VillageTakings.cs:59) - E5/0 ich nie dotyka (B1 - K3).

### 10.4 Spojnosc
Jedna definicja ludzi (D1: `PeopleOf` + skala BK z kalibracji), jeden zapas kupcow (D4: `Seal31.TownReserve` / `CastlePurse.Reserve` - te same, ktore A1 zamknie w
`TownPurse.Spare`), jedna definicja utargu wsi (okno MoneyLedger), jedna definicja wydatku gry (petla BK). Gracz = AI (zycie, dwor, sprzet - te same miejsca; dwor graczowi
nie istnieje [D 08.10]). Stan czyszczony miedzy kampaniami (`Reset` w konstruktorze; brak zapisu).

### 10.5 Nowa praca i koszt [S]
`DemandPostfix` 227 razy/d x ok. 80 kategorii (indeks cen) ~ 18 tys. odczytow; `BudgetPostfix` ok. 20 tys. wierszy/d (97 miast x ~150 + 130 zamkow x ~50) - slownik i
dwa dodawania; `ConsumePostfix` 227/d; bitwy i notable - kilka tysiecy/d; `Daily` ok. 800 osad arytmetyki + refleksja polityki wsi 571/d; `Lines` - petla po osadach i
polkach zamkow (130 x ~30). Oczekiwane < 10 ms/dobe (prog 15 ms, E50-T2; dzis E4/0 calosc 10.8 ms/d). Pamiec: ok. 800 obiektow `Folk`.

### 10.6 Cudzy kod (sprawdzone w dekompilacji do tego planu)
G ItemConsumptionBehavior.cs:17-71, :142-205; G CampaignPeriodicEventManager.cs:205-245, :397-406 (tick osady rozlozony na dobe); G Town.cs:295-297, Campaign.cs:546
(`AllTowns` bez zamkow); BK EconomyPatches.cs:500-611 (prefiks, `ConsumedValue`, zadowolenie :544/:549, dokup :532-536), :1085-1100; BK BKSettlementBehavior.cs:463-490
(`HandleMarketGold`), :620-631 (`TickCastle` -> `MakeConsumptionInTown` refleksja); BK EconomicData.cs:62, :148-152; BK PopulationManager.cs:21-28 (`ConsumptionType`);
BK BKEconomyLayerInstaller.cs:452-481; G MapEventParty.cs:58-61, :499-524; G MapEvent.cs:1497-1526, :1800-1817; G NotablePowerManagementBehavior.cs:35, :47-61;
G Village.cs:103; G SellItemsAction.cs:60-101; G CampaignEvents.cs:631, :981; G DefaultSettlementFoodModel.cs:38-90; BK BKFoodModel.cs:37-48 (baza = Default ->
`SoldItems` liczone); zrodla `OnItemProduced`: BK EconomyPatches.cs:735, :972, BK WorkshopData.cs:75, G VillageGoodProductionCampaignBehavior.cs:168.

### 10.7 Wyjatki (U24) - miejsca do recenzji
Wszystkie wpiecia z 2.2 (20 linii w 13 plikach zywego kodu + 5 wywolan w `MoneyLedger*.cs`) - kazde w `try`, po przelewie i po istniejacym liczniku ksiegi; `TownFolk.Note/Sale/OnGold/OnTownCoin/
NoteCartPaid/NoteVillageReturn/OnItemProduced` maja wlasny `try` i nie rzucaja. Latki: prefiks/postfiks z `__state` (bez statycznego wycieku), finalizer nawiasu.

---

## 11. NK (nowa kampania)
Nic nie wymaga nowej kampanii (tylko log). Stary zapis Jeffa / zapis testowy z doby 362: pomiar od pierwszej doby po wczytaniu (kalibracja D1 i zapasy startowe na sucho
od nowa - linia sesji). Liczby "na sucho" do kalibracji B - tylko z biegu nowej kampanii (zbior A).

---

## 12. Odstepstwa od P (do CHANGELOG) i pytania
- **O1** 5 uzupelnien listy wpiec (Seal31B:454 karawany - P R9 "takze karawany"; OutlawLaw:1773 bandy - to samo zjawisko "zycie w miescie"; MenUpgrade:447 - ksiega liczy je
  "jak AiGear"; BkSupplyMarket:172 - zaopatrzenie zywnoscia z sakiewek, KL NoteSale; ClanBudget:759 - G wsi wedlug polityki, P linia "Gotowka wsi"). Kazde mozna zdjac
  bez zmiany reszty (osobne rodzaje w linii).
- **O2** bitwy - wlasna latka odczytu zamiast przerabiania O14 (P: "CirculationWindows.cs okno O14 rozbite"); `CirculationWindows.cs` bez zmian.
- **O3** notable - wlasna latka odczytu (P nie wskazal okna dla nowej linii).
- **O4** osada bez danych BK - mediana skali BK/ksiega zamiast `GetDesiredTotalPop` (d120: 0 takich osad).
- **O5** zamek - ludzie BK z kalibracji bez skali ksiegi (`PeopleOf` zamku = 0).
- **O6** linia startowa w dwoch czesciach (okna w menu; kalibracja i modele czynne w pierwszej dobie sesji - modele i BK istnieja dopiero w kampanii).
- **O7** "Gotowka wsi" w `MoneyLedger.Obieg.cs` jak P, podzial wedlug polityki z `TownFolk` (korona - tylko razem; brak licznika daniny na wies).
- **O8** "jedzenie z targu wobec zuzycia warowni c" - bez c (model zywnosci ma latki E4/6 i zimy; wolanie go drugi raz zmienialoby koszt i liczniki) - zamiast c zapas warowni.
- **O9** polityki, Road Tolls, Spoils bez rozbicia pasa ponizej zapasu (brak licznika na miasto) - A1 przepina je na `TownPurse.TaxFloor`.
- **O10** 15% zaworu zamku w podzamczu - szacunek z kasy po zaworze (bez akcesora w `CastlePurse.cs`, ktory zmienia A1).
- **Pytania do Jeffa: brak** (pomiar nie zmienia gry; P rozdz. 6 pyt. 1 dotyczy testu porcji A). Do recenzji planu: O1 (czy 5 uzupelnien zostaje), O10 (czy dodac akcesor
  `CastlePurse.DuesOf` po scaleniu z A1 zamiast szacunku).

---

## 13. Kolejnosc kodowania (drzewo `SCR\e5\0`, galaz `w-toku/e5-0` od 392c519; kazdy krok: `cd` do katalogu projektu, `dotnet build Armoury/Armoury.csproj -c Release -v q --nologo` kod 0, osobny commit z wiadomoscia konczaca sie `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`)
1. **K1 szkielet:** `Settings.cs` (grupa, klucz), `T:gen_mcm.py` RANGES, `python tools/gen_mcm.py`; `TownFolk.cs` (stan, `On/Live`, `Reset`, `Stumble`, stale P0), `TownFolk.Probe.cs`
   (`ApplyAll` 4.1-4.3 + linia startowa 7.1, odczyt BK, kalibracja 3.2, linia sesji 7.2); `ArmouryBehavior` (konstruktor, `Daily`, `Lines`), `SubModuleMain`. Pliki CRLF
   (`SCR\e4\crlf.py`).
2. **K2 przeplywy:** wpiecia 2.2 (zywy kod + `MoneyLedger` + nasluch `OnItemProducedEvent`), model na sucho 5.3, linia 7.3, CSV 7.8.
3. **K3 linie pomiaru:** latki 4.4-4.5, `CrownProbe`, linie 7.4-7.5.
4. **K4 ksiega:** `VillageCashLine` 7.6, dopiski 7.7.
5. **K5 narzedzie:** `etap5_main` + `etap5_e50` (rozdz. 8); proba na logu z31-A (bez linii E5/0 - blok "brak linii startowej", bez wyjatku).
6. **K6:** wpis CHANGELOG (rozdz. 14), dwie recenzje diffu (1: "tylko odczyt" i kolizje latek; 2: model na sucho wobec sim2/KL), poprawki, autotest 40 dob na drzewie
   scalonym przed testem koncowym E4.

## 14. Wpis CHANGELOG (szkic, na gorze `CHANGELOG.md`)
- **Problem:** lud nie ma kiesy, wiec etap 5 nie ma liczb do stawek: "zakupy" mieszczan z niczego 425 318 zl/d (L:31717; sr28 421 538), "czy ludzi stac na jedzenie" nie do
  policzenia (RA rozdz. 9); bitwy z niczego bez rozbicia - zrodla 85 762, netto 45 147 (L:31718); pan bierze 86% gotowki wsi (RA rozdz. 4, L:31724); sakiewki = 29 dni zoldu
  (L:31726); pasmo notabli w nicosc 107 500 / z niczego 69 000 bez podzialu miasto-wies (L:31727); wydatek BK 482 241 wobec budzetu gry 1 005 941 (L:31689).
- **Przyczyna:** brak pomiaru: K:MoneyLedger.Obieg.cs:567 ("markietani -"), :654 (napis nieaktualny); okno O14 tylko flaguje bitwy (K:CirculationWindows.cs:651-662);
  budzet BK liczony przy kazdym wierszu polki (BK EconomyPatches.cs:514-552) - suma `__result` nie jest budzetem dnia (K1).
- **Zmiana:** E5/0 (195) wedlug `docs/etap5/PLAN-KODU-E50.md`; klucz `TownFolkMode` (1); odstepstwa O1-O10.
- **Ryzyko / co sprawdzic:** rozdz. 10 (tylko odczyt - lista zakazanych wywolan; wspolne cele 10.2; scalanie 10.3; koszt E50-T2; kalibracja nie przesuwa ksiegi ludzi).
- **Status:** DO SPRAWDZENIA - pomiar jedzie w tescie koncowym etapu 4 (gra bez zmian); stawki B z linii E5/0 biegu A zbioru A po tescie koncowym etapu 4.
