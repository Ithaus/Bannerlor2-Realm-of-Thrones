## 2026-10-19/20 (noc 19/20.10, przeglad calosci n20-calosc: poprawki po 5 recenzentach)

**Mody:** Armoury, RealisticCaptivity (+ `tools/sprawdz_logi.py`) | **Galaz:** `w-toku/n20-calosc` (scalenie 11 galezi od `934cc039` = WGRANIE 88) | **Commity:** `a0fd4dba` (kolizje), `a24390d9` (napisy, watki, furtka, karawana), ten wpis | **Tor:** M (bez gry; gen_mcm po zmianie Settings.cs; build Armoury i RealisticCaptivity: kod 0, 0 Error(s); Armoury.dll md5 `c7e60f924046329cf329e5bd6159948e`, RealisticCaptivity.dll md5 `8c0d882aab82492bb2bee89dbf929996`) | **Klucze:** zadnego nowego, zadna wartosc domyslna bez zmian (zmienione tylko 2 opisy MCM) - `Armoury.json` Jeffa bez zmian | **Zapis:** bez zmian formatu.

**Dla Jeffa (prosto):**
- Nocleg w karczmie i w zajezdzie: towarzysz z zoldem wedlug poziomu placi za lozko tylko ze swojej kiesy (tam idzie jego zold); kogo nie stac, spi w tanszym lozku albo w stajni - zolnierze juz za niego nie doplacaja.
- Tarcze band: herby lordow i panow warowni, ktorzy przyjda Ci z odsiecza (Dynamic Reinforcements), nie trafia na tarcze bandytow w tej samej bitwie.
- Towarzysz prowadzacy karawane Twojego rodu dostaje swoj zold do wlasnej kiesy (dotad szedl do sakiewki ludzi karawany).
- Napisy: nocleg we wlasnej wiosce mowi o dachach wiesniakow, nie o lozkach i karczmie; okno "Look over your village" dzieli sie na strony, gdy tekst sie nie miesci; stare plotki z zapisu nie mowia juz "Broken Men burned ..." tylko "a band of outlaws"; ksiega zamowien kowala pisze "One-handed weapons", "Body armour" zamiast nazw z kodu gry; zakupy towarzyszy: "Steel Helmet (300, instead of Leather Cap)", a zdanie o zdjetych rzeczach tylko gdy cos zdjeli; niewola: "1 piece", "the other one is gone" zamiast "1 pieces"; opis startu BK "Indebted Lord" mowi o wiosce pobocznej, nie o tytule Lordship (tylko nowa kampania).

### Znaleziska i wynik

| # | wymiar / waga | znalezisko | wynik |
|---|---|---|---|
| 1 | kolizje / wazne | TownInns.Plan: przy CW brak oplaty towarzysza idzie z sakiewki ludzi, choc jego zold juz tam nie trafia; napisy i MCM mowia "where their pay goes" | **poprawione** |
| 2 | kolizje / wazne | BandShields: herby odsieczy DR (dolacza po pierwszym wyborze) moga byc na tarczach band | **poprawione** |
| 3 | kolizje / drobne | sprawdz_logi NC-21: prog nasza/BK 0.1 nie pasuje do ceny z zoldu CW | **poprawione** |
| 4 | kolizje / drobne | linia DS "pan i majatki" liczy udzial wioski gracza | **poprawione** |
| 5 | napisy / drobne | nocleg we wlasnej wiosce - napisy karczmy ("beds", "comfort of the inn", "Slept in a bed") | **poprawione** |
| 6 | napisy / drobne | "Look over your village" moze nie zmiescic sie w ramce (ok. 22 linie) | **poprawione** |
| 7 | napisy / drobne | stare wiesci (WGRANIE 88) z nazwa klanu band | **poprawione** |
| 8 | napisy / drobne | opis startu BK "Indebted Lord" obiecuje tytul Lordship | **poprawione** |
| 9 | napisy / drobne | Order Ledger: surowe nazwy ItemTypeEnum | **poprawione** |
| 10 | napisy / drobne | CompanionGear: "X for Y" dwuznaczne, zdanie o zdjetych sztukach zawsze | **poprawione** |
| 11 | napisy / drobne | RealisticCaptivity: brak liczby pojedynczej | **poprawione** |
| 12 | napisy / drobne | opis MCM Side Village Fief Lodging: brak "Night Rest" | **poprawione** |
| 13 | watki / drobne | FletchForge: stary napis o aksamicie (_velvetShort nie czyszczony) | **poprawione** |
| 14 | watki / drobne | CompanionWage/CompanionGear: _errLogged i _stumbles na proces, nie na sesje | **poprawione** |
| 15 | zapis / drobne | CompanionAutotest dziala tez w biegu profilu | **poprawione** (wylaczony bieg profilu; zwykly autotest bez zmian) |
| 16 | zapis / drobne | komentarz formatu arm_side_fief (sfp) niezgodny z kodem | **poprawione** |
| 17 | ekonomia / drobne | towarzysz prowadzacy karawane rodu - zold CW do sakiewki ludzi karawany | **poprawione** |
| 18 | ekonomia / drobne | SmeltTruth: bron z 1 sztaby oddaje 100% metalu | **pominiete** (swiadoma decyzja 7 galezi kuznia) |

### Szczegoly (dowod -> zmiana)

**1. Nocleg towarzyszy przy CW (prawda).** `SoldierPay.Route` (galaz partii lorda): `CompanionWage.PayFromWage(mp, amt, r.Wage)` -> `h.ChangeHeroGold(pay)`, do `MenPurse.Add` idzie tylko reszta; `MenPurse.DueInRoster` wylacza zold tych bohaterow z limitu sakiewki. `TownInns.Plan` dalej bral brak z `left` (wolna sakiewka ludzi). Zmiana: w petli po `b.Comp` przy `CompanionWage.Applies(h)` zasieg tylko `gold` (bez `left`), `fromPurse = 0`; napis okna karczmy warunkowo (`CompanionWage.On`), opis Town Inns w Settings/MCM: "companions pay from their own purses (with Companion Wage By Level off, the rest from the men's purse, where their pay then goes)". Napis po zaplacie (`compPursePaid > 0`) zostaje - przy CW nie wystapi. `TownInns.Road` korzysta z tego samego `Plan` (CompPurseSum = 0 przy CW). Bez CW - jak dotad.

**2. Herby odsieczy DR (prawda).** Dekompilacja `DynamicReinforcements.dll`: `BattleStateBehavior.AfterStart` - `PartyScanner.Scan(MapEvent)` + `ReinforcementCalculator.Evaluate` -> `Candidates` (pola `Accepted`, `Side`, `Party`, `Settlement`, `Faction`, `Leader`); partia wchodzi do MapEvent dopiero przy spawnie. `DefaultBattleMissionAgentSpawnLogic` spawnuje ludzi w `OnMissionTick` (po wszystkich `AfterStart`), wiec kandydaci sa gotowi przed pierwszym `Decide`. Zmiana: `BandShields.Begin` -> `DrFriends(m, PlayerSide)`: odbiciem (typ i pola raz na proces) przyjeci kandydaci po stronie gracza - do `_friendCodes` herb rodu wodza, rodu partii (`ActualClan`), wlasciciela osady i frakcji (bez bandyckiej). Linia "Tarcze band (herby): ..." - w nawiasie "partii N, odsiecz DR po stronie gracza M". Bez DR - nic. Nie robilem (2)/(3) z propozycji (dopisywanie herbow agentow strony gracza w trakcie) - listy herbow band licza sie po `Begin`, wiec (1) wystarcza dla DR; inne mody z posilkami w trakcie bitwy - nie znalezione.

**3. NC-21 (prawda).** `CompanionPay.OurPrice`: `wage = CompanionWage.WageFor(...)` - przy CW zold z tabeli. Zmiana `sprawdz_logi.py`: gdy w liniach doby "Towarzysze (C)" jest "zold wedlug poziomu tak" - prog dolny nasza/BK 0.02 (inaczej 0.1); opis NC-22 przy CW "zold z tabeli CW wedlug poziomu, poz. 19 = 23 d".

**4. Linia DS (prawda, tylko licznik).** `VillageTakings.EnterPostfix`: `SideFief` zwraca teraz wyplate (`SideVillageFief.AtCart` -> `long`), `DsDues(st, works, side)` -> `DsVillage.DuesAtCart(..., works, side)`: `_cSide` osobno, `_cLord` bez niego. Linia "Daniny wsi (DS)": po "prace okregu (inwestorom)" dopisek ", wioska gracza (z udzialu pana) N" (gdy > 0). Wzorzec checkera `carts` ("pan i majatki (bez zmian) N, czesc wsi N (bez przyrostu kiesy N)") bez zmian. Pieniadze bez zmian.

**5. Napisy noclegu we wlasnej wiosce (prawda).** `TownInns.Wake`, `Hourly`, `SpeedBonus`, `MoraleBonus` przy `HostUid != null`: "The men were up before they had slept their fill under your villagers' roofs - nobody wakes rested.", "Dusk - the night's rest under your villagers' roofs wears off.", linia rozpiski "Slept under a roof", dopisek dlugu snu bez "bed's comfort".

**6. Okno "Look over your village" (prawda co do braku liczenia linii).** Zmiana: `ShowLook` dzieli tekst po akapitach na strony (`Rumours.EstLines`, `Rumours.PageLines` = 17), `LookPage`: "More"/"Done", tytul "(1/2)" tylko przy wielu stronach. Zwykle (14-15 linii) - jedno okno jak dotad.

**7. Stare wiesci z nazwa bandy (prawda).** Zmiana: `Rumours.Import` po wczytaniu (przy `InnGossipLeaderNames`) `OldBandNames()`: nazwy klanow `Clan.BanditFactions` w A/B wiesci NRaided -> A "a band of outlaws", B ""; NBattle -> strona z nazwa klanu "a band of outlaws". Linia startu plotek: ", przerobione stare wiesci z nazwa bandy N".

**8. Opis startu BK (prawda).** Dekompilacja BK: `BKCampaignStartBehavior.OnSessionLaunched` wola `DefaultStartOptions.Instance.Initialize()` (opcje od nowa - kolejnosc wobec naszego OnSessionLaunched nieznana), okno czyta `StartOptionVM.ShortDescription => Option.ShortDescription.ToString()`. Zmiana: w `SideVillageFief.EnsureHooks` (raz na proces, w kampanii) postfiks gettera `StartOptionVM.ShortDescription` (metoda z wywolaniami, nie 1-bajtowy `ret`; czytana przez wiazanie okna, nie wklejana): opcja `start_lord` przy `SideVillageFiefStart` - "Start as a lord in a kingdom, holding one of its side villages - it flies your banner and pays you a share of the lord's tax. Reduced influence for 5 years. Gain Scholarship skill." Klucz wylaczony - tekst BK. Linia "Wioska gracza (n20): ... latki: ..." - ", opis startu BK (StartOptionVM.ShortDescription) wpiety/BRAK". Dotyczy tylko nowej kampanii.

**9. Order Ledger (prawda).** `SmithMenu.KitName(type)`: Helmets, Body armour, Boots, Gloves, Capes, One-handed weapons, Two-handed weapons, Polearms, Shields, Bows, Crossbows, Arrows, Bolts, Throwing weapons, Mounts, Horse harness. W wierszu listy, tytule wyboru tieru (z polki i bez), wierszach ilosci, komunikacie dostawy i braku na polce. Identyfikator wiersza i linia logu "Zamowienie dla wojska" bez zmian.

**10. CompanionGear (prawda).** Wiersz zakupu "X (cena, instead of Y)"; licznik `_swapped` (zdjete sztuki w wizycie) - "The pieces they took off are in your inventory." tylko przy > 0.

**11. Niewola (prawda).** `CaptivityBehavior.Count(n, one, many)`; "1 piece", "the one piece they left you", "your war gear comes back to you" (1 sztuka, bez "all 1 pieces"), "The other one is gone", "your last coin is theirs now", "(1 item)"; takze dwa starsze komunikaty jukow.

**12. Opis Side Village Fief Lodging (prawda).** `TownInns.On = TownInns && NightRestEnabled`. Settings.cs: "Needs Town Inns and Night Rest." + gen_mcm.

**13. FletchForge (prawda, dekompilacja BK `CraftingMixin.UpdateMainAction`: `if (!IsInArmorMode) return; ... if (!HasEnergy()) ... else if (!HasMaterials())`).** `BkMainActionPostfix`: napis zabrany i wyczyszczony, podmiana tylko gdy `IsInArmorMode` i `IsMainActionEnabled == false`.

**14. Potkniecia na sesje (prawda).** `CompanionWage.SessionStart` i `CompanionGear.SessionStart`: `_stumbles = 0; _errLogged = false;`.

**15. Furtka CompanionAutotest (prawda co do biegu profilu).** `CompanionAutotest.Daily`: dodatkowo `LogGate.Mode != 1 -> return` (bieg profilu = 2 mierzy gre jak u Jeffa). Zwykly autotest (1) - jak dotad (najem i wyplata probna sa potrzebne, zeby test widzial CW/CG). Pliku-flagi nie dodawalem: nocny bieg testowy nie wie o nowej fladze i przestalby sprawdzac CW/CG. W grze Jeffa (DLL bez typu Autotest) - bez zmian (nic).

**16. Komentarz zapisu (prawda).** `SideVillageFief` pole `_pendKingdom`: komentarz mowi o zapisie "sfp|krolestwo|doba". `Reset()` w konstruktorze ArmouryBehavior czysci oczekiwanie miedzy kampaniami - sprawdzone, bez bledu.

**17. Zold towarzysza w karawanie (prawda).** `CompanionWage.TotalPostfix` liczy korekte dla kazdej partii z wodzem z rodu gracza; `SoldierPay.Route` placil bohaterowi tylko w galezi `IsLordParty`. Zmiana: w galezi karawan, dla karawany rodu gracza (`ActualClan == Clan.PlayerClan`), `PayFromWage` przed `Seal31B.CaravanWage` (licznik `_dComp` linii "Zold:"). Karawany AI - od razu dalej (bez petli po rosterze). Limit sakiewki (`DueInRoster`) juz liczyl karawany rodu gracza bez tego zoldu.

**18. SmeltTruth - pominiete.** Prawda co do liczb (31 wzorow z 1 sztaba oddaje te sztabe), ale to swiadoma decyzja 7 galezi kuznia (wpis kuznia-CHANGELOG: "Zmiana tej regul wymagalaby slowa Jeffa"). Proponowany los (`MBRandom`) psulby zgodnosc podgladu przetopu z wynikiem - `WeaponMetal` liczy i podglad (OutputPostfix), i prawdziwy przetop. Do decyzji Jeffa.

### Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)
- Nowe latki: 1 (postfiks `StartOptionVM.ShortDescription` BK - getter z wywolaniami; wolany tylko z okna startu BK). Pozostale zmiany bez nowych latek.
- `BandShields.DrFriends`: odbicie na typach DR raz na proces; wyjatek - potkniecie "Begin(odsiecz DR)", reszta Begin dziala. Sprawdzic w autotescie z bitwa linie "Tarcze band (herby): rozstawienie | ... (partii P, odsiecz DR po stronie gracza M)" - M >= 0, potkniecia 0.
- TownInns: przy CW towarzysz bez zlota spi w stajni (zgodnie z zasada zolnierzy); linia "Karczma" pokazuje "sakiewka ludzi 0" przy towarzyszach CW.
- SoldierPay (karawana): `PayFromWage` - ten sam kod co w partii lorda; linia "Zold: ... towarzyszom gracza do ich kies (CW) N (K partii)" - K moze wzrosnac o karawany z towarzyszem.
- DS: suma "pan i majatki" + "wioska gracza (z udzialu pana)" = dawne "pan i majatki" (gdy wioska gracza istnieje).
- Plotki: przerobka starych wiesci tylko przy wczytaniu i tylko gdy A/B rowne nazwie klanu band - nazwy lordow bez zmian.
- Napisy: wszystkie po angielsku; checker nie czyta zmienionych zdan (grep `delivered to the`, `Not a single sound`, `Which grade of`, `pieces` - brak wzorcow).

**Status:** DO SPRAWDZENIA (zbudowane, bez gry; autotest calosci n20 jeszcze przed nami).
