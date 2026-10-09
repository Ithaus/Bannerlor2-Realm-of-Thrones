# 13 - UMIEJETNOSCI: XP za oddawanie broni i przeglad wszystkich umiejetnosci wobec naszych zmian (09.10.2026, wersja 2 po krytyce)

Status: tylko dokument. W kodzie, grze, zapisach i ustawieniach nic sie nie zmienilo, a gry nie uruchamialem.
Wersja 2 uwzglednia 23 uwagi krytyki (rozdz. 7 "Krytyka i odpowiedzi"): 20 przyjetych, 2 przyjete czesciowo z inna poprawka, 1 przyjeta z innym wnioskiem.
Podstawa to dwa raporty badawcze z 09.10, przeczytane w calosci:
`...\7016f733-...\scratchpad\umiejetnosci\zrodla-xp.md` (skad bierze sie XP za sprzet) oraz `...\umiejetnosci\calosc.md`
(18 umiejetnosci gry, 3 z BK i 3 z NavalDLC). Kazda liczbe z krytyki sprawdzilem sam w dekompilacji, danych i logach (rozdz. 7).
Kod w wersji z gry: Armoury 0f8a80b0 / CrashScribe 269cc980 (`scratchpad\sklad4`). Paczka 171 (zatwierdzona przez Jeffa "K domyslne 171 TAK",
czeka na wgranie razem ze 174): `scratchpad\noc\n172`. Gra 1.4.8, BK, BKROTPatch i ROT z dekompilacji `ore-supply`; Spoils z `umiejetnosci\dec\spoils`.
Decyzje Jeffa z 09.10 (A-L) sa w `docs/STAN-PRAC.md:768-771`.

Oznaczenia: [K] kod (plik:linia), [D] dane albo ustawienia (XML, json MCM Jeffa), [P] pomiar z logu (autotest 120 dob
`scratchpad\kopiaT9-120`, testy 171 `kopia171`, `kopiaS4`, `kopia172b`; zrzut przedmiotow `Documents\...\CrashScribe\items-dump.csv`),
[H] historia ze zrodlem, [L] lore ze zrodlem, [S] szacunek z wzoru (rzad wielkosci).
"Surowe XP" to liczba, ktora dostaje gra. Bohater mnozy ja przez swoje tempo nauki, zwykle x4-x8 [K `DefaultCharacterDevelopmentModel.cs:223-235`].
Wokol poziomu 100 jeden poziom kosztuje ok. 6 080 XP efektywnego, czyli ok. 1 000 surowego przy tempie x6 [K tamze :95-103, :127].
Ceny: wszystkie wartosci przedmiotow sa w nowej monecie (pensy), PO `HistoricalPrices.Apply` [P items-dump.csv, zrzut "PO naszych prawach", `Mends.cs:2162-2170`].

---

## 0. Dla Jeffa

1. Exp za oddana bron i zbroje daja perki Twojego kwatermistrza "Giving Hands" (bron) i "Paid in Promise" (zbroja): 35-300 XP za kazda sztuke wedlug tieru, bez wzgledu na cene i stan, a po przejsciu na pensy tunika za 6-12 d daje 75 XP, wiec to najtanszy sklep z doswiadczeniem w grze.
2. Najgorsze sa ekrany Spoils "War stockpile" i "Inspect trophies", bo rzeczy zostaja na miejscu, a gra placi za cala kupke przy kazdym zamknieciu, takze po "Cancel", wiec mozna klikac w kolko.
3. Przy okazji znalazlem blad: na 7 ekranach Spoils z napisem "take back what you want to keep" przycisk "Cancel" niczego nie cofa, tylko oddaje wszystko (caly sprzet i konie dowodcy albo miastu, cale jedzenie na dar, caly lup zalodze).
4. Drugi blad jest w BannerKings: handel liczy "sprzedane" rzeczy wzgledem taboru z ostatniego wejscia na targ miasta, wiec sprzet oddany pozniej (zbrojownia, dary) przy nastepnym handlu we wsi uczy handlu tak, jakby zostal sprzedany.
5. Naprawa to trzy male latki pod jednym wylacznikiem w MCM (oddany sprzet nie uczy ani w grze, ani w Spoils; napisy Spoils nie obiecuja treningu; handel liczy tylko to, co sprzedales przy tym straganie) i osobna latka "Cancel = nic sie nie dzieje"; nasza kuznia i naprawy sa czyste.
6. "Equip and train the leader" w nowej monecie daje za bron grosze (miecz t5 to 5 XP), ale koni nie przeliczalismy, wiec rumak szlachecki to ok. 1 000 XP Jazdy dowodcy, mniej wiecej jeden poziom, i to tez zamykam.
7. Po zamknieciu dziury Twoje wojsko poza bitwa nie bedzie mialo skad brac doswiadczenia, bo codzienny darmowy trening gra daje tylko AI, dlatego razem z ta latka proponuje musztre dla Twojej druzyny: na postoju szybciej, w marszu wolniej, zalezna od Twojego Przywodztwa (pytanie c).
8. Twoje zalogi sa poza systemem 171, bo nie kupuja same broni (bralyby Twoje pieniadze bez pytania), wiec cwicza po staremu z budynkow; jesli w MCM wlaczysz "Garrison Buys Gear Player", kupia brakujaca bron z Twojej kiesy, a cwiczenia zaczna zalezec od broni jak u AI.
9. Musztre wojsk AI ruszam dopiero po pomiarze i po zmianie skladu armii z audytu 14, i tak, zeby armie AI srednio nie oslably (Twoja decyzja C), tylko zeby lepsi dowodcy i armie na postoju szkolily lepiej.
10. Reszta umiejetnosci ma sens; do poprawy sa jeszcze XP z RBM za kazdy strzal (takze chybiony i na arenie), zarzadca uczacy sie od wielkosci wojska, zwiad od dlugosci kolumny i 24 ksiazki BK po 7-10 d, a trzy decyzje dla Ciebie sa w rozdz. 4.

---

## 1. "Przekazujesz bron, rosnie exp": skad dokladnie, ile, jak wyklikac i jak naprawic

### 1.1 Glowna przyczyna: perki zarzadcy kwatermistrza

- **Perki** [K gra `DefaultPerks.cs:2268, 2270`]:
  - *Paid in Promise* (Steward 100). Pierwszy efekt dla przywodcy: -25% pensji towarzyszy. Drugi, dla kwatermistrza:
    "Discarded armors are donated to troops for increased experience".
  - *Giving Hands* (Steward 125). Pierwszy efekt dla kwatermistrza: "Discarded weapons are donated...". Drugi, dla gubernatora: +10% cla.
  - Kazdy z nich to wybor zamiast innego perku (Efficient Campaigner albo Logistician). Gra sprawdza perk u kwatermistrza druzyny
    gracza [K `DefaultItemDiscardModel.cs:12-24`].
- **Ile** [K `DefaultItemDiscardModel.cs:26-53`]: za sztuke wedlug tieru przedmiotu: t1 75, t2 150, t3 250, t4-t6 300, ponizej t1 35 XP.
  Wartosc i stan (Rusty, Mangled) nie maja znaczenia. Konie nie sa ani bronia, ani zbroja, wiec ich to nie dotyczy.
- **Kiedy gra placi** [K]:
  - `InventoryLogic.cs:486`: XP jest wlaczone w trybie Loot, a takze w trybie Default bez drugiej partii (Stash i Warehouse - bez XP).
  - `InventoryLogic.cs:579-582`: przy zatwierdzeniu leci `OnItemsDiscardedByPlayer(_rosters[0])`. Liczy sie CALA lewa strona ekranu.
  - `DiscardItemsCampaignBehavior.cs:25-32`: wyplata przez `MobilePartyHelper.PartyAddSharedXp`, miedzy oddzialy, ktore moga jeszcze rosnac.
    Sufit jednego zamkniecia to tyle, ile brakuje wszystkim do najblizszego awansu.
  - `InventoryScreenHelper.cs:157-170`: **Cancel tez placi**. Najpierw jest `Reset`, a potem i tak `DoneLogic()`.
- **Koszt awansu u nas**: gra 300 / 550 / 900 / 1 300 / 1 700 XP (na t2...t6) [K `DefaultPartyTroopUpgradeModel.cs:34-60`], BK mnozy x3.0
  [D BannerKings.json `TroopUpgradeXp` 3.0; K BK `VanillaModelTweakPatches.cs:1559`], czyli **900 / 1 650 / 2 700 / 3 900 / 5 100 XP**.
- **Ile kosztuje to XP w nowej monecie** [P items-dump.csv]: mediana wartosci broni jednorecznej t1 to 8 d (15 sztuk), zbroi ponizej t1 8 d
  (725 sztuk, gl. odziez), zbroi t1 22 d. Zaloga King's Landing kupila tunike `crownlands_garb` (t1, wartosc 12) za 6 d [P `zakupy.log` 22:25:23].
  Kazda taka sztuka daje 75 XP (t1) albo 35 (ponizej t1). **12 tunik t1 za ok. 70-150 d to awans rekruta t1 -> t2**; trzy miecze t4
  (86-109 d na targu [P `zakupy.log`]) tez. Nowa moneta zrobila z discardu tani sklep z XP: XP liczy sie od tieru, a ceny spadly.
- **[H]** Jeff korzystal z tego 27.08: "discarduje pancerz za XP dla wojska (perk)" (`CHANGELOG.md:7314-7331`). Wtedy zalatalismy tylko
  zbrojownie DTE (`QuartermasterLaw.XpDonationsPostfix`, :799-816). Dzis ta latka nic nie robi, bo zbrojownia DTE 1.4.7 otwiera sie
  jako schowek, a schowek nie daje XP [K DTE `ArmyArmoryBehavior.cs:393` `OpenScreenAsStash`].

### 1.2 Jak to wyklikac bez konca: ekrany Spoils of War

Spoils otwiera swoje ekrany (11) przez `InventoryScreenHelper.OpenScreenAsReceiveItems`, czyli `new InventoryLogic(null)` w trybie Default.
Wedlug gry to ekran wyrzucania, wiec placi XP [K gra `InventoryScreenHelper.cs:307-315`; Spoils `DonateEquipmentBehavior.cs:139`,
`LootCollectionBehavior.cs:2986, 3175, 3535`, `QuartermasterBehavior.cs:817, 874, 944, 1326, 1423`, `SubClanBehavior.cs:1316, 1661`].
Dwa z tych ekranow maja **trwala** lewa strone:

| Ekran | Jak klikac | Ile | Dowod |
|---|---|---|---|
| **War stockpile** (magazyn wojenny u kwatermistrza w miescie) | otwierasz i zamykasz (Done albo Cancel), awansujesz wojsko, powtarzasz | 75-300 za KAZDA sztuke magazynu (do 500 szt.), przy kazdym zamknieciu, az do sufitu "do najblizszego awansu" | [K] Spoils `QuartermasterBehavior.cs:1316-1356` (lewa strona budowana z zapisanego magazynu i zapisywana z powrotem); [D] `RealisticLootSettings.json` StockpileMaxItems 500, EnableQuartermaster true |
| **Inspect trophies** (trofea po bitwie) | otwierasz i zamykasz wiele razy az do "Leave" | jw. za cala kupke trofeow | [K] `LootCollectionBehavior.cs:502, 1893-1900, 3163-3175` |
| Tabor Spoils (Baggage) | dzialaloby tak samo | - | **u nas martwy**: [P] log Armoury 22:24:11 "BattlefieldLaw: tabor Spoils (Baggage Train) wyciety na stale" |

Przyklad [S]: 100 sztuk broni t4 w magazynie to 30 000 XP na jedno zamkniecie, czyli 33 rekrutow na t2 albo 11 ludzi z t3 na t4.
Hamuje to tylko zloto za awanse. Napis "Your troops will get N experience" liczy wylacznie to, co przelozyles w tej sesji
[K `InventoryLogic.cs:655-673`], wiec na tych ekranach **zaniza** wyplate.

Inne ekrany placa raz, za to, co zostaje po lewej przy zamknieciu [K]: wyposazenie zalogi, milicja i przetop Spoils
(`QuartermasterBehavior.cs:817, 874, 1423`), dar dla klanu (`SubClanBehavior.cs:1316`), zdarzenie ROT "King's Landing Armory"
(ROT `KingsLandingEvent.cs:133`), lup ROT przy zaciagu (`EnlistmentPatches.cs:432`, tryb Loot), nasz lup z kryjowki (`HideoutPurge.cs:152`, tryb Loot)
i zwykly lup po bitwie (tryb Loot, `InventoryLogic.cs:486`). Wszystkie zamyka Z1.

**[P] Brak pomiaru:** autotest gra AI, a gracz nie otwieral tych ekranow. Petle wynikaja z kodu, w grze ich nie zaobserwowano.

### 1.3 "Cancel" na ekranach "take back what you want to keep" oddaje wszystko (blad, nie tylko XP)

Na 7 ekranach Spoils rzeczy przy otwarciu sa ZDEJMOWANE z Twojego taboru i kladzione po lewej ("zabierz, co chcesz zatrzymac").
Cancel to `InventoryScreenHelper.CloseScreen(fromCancel: true)`: `InventoryLogic.Reset` przywraca obie strony do stanu z otwarcia
[K gra `InventoryLogic.cs:715-727`, kopia z otwarcia `SetCurrentStateAsInitial` :1145-1152], a potem i tak idzie `DoneLogic` i funkcja zamkniecia Spoils
[K `InventoryScreenHelper.cs:157-170`]. Stan z otwarcia to "wszystko po lewej", wiec **Cancel znaczy "oddaj wszystko"**:

| Ekran | Co laduje po lewej przy otwarciu | Skutek Cancel | Dowod [K] |
|---|---|---|---|
| Equip and train the leader (pod-klan) | cala bron, zbroje i konie z taboru | wszystko do taboru dowodcy (+XP dowodcy, 1.4); bez partii - przepada | `SubClanBehavior.cs:1326-1363` (MoveEquipmentToTempRoster), :1651, :1720-1723 |
| Donate gear to clan (pod-klan) | jw. | nasz `SpoilsSeal.DonatePrefix` sprzedaje caly sprzet miastu | `SubClanBehavior.cs:1307, 1316`; `SpoilsSeal.cs:598` |
| Donate food (kwatermistrz) | cale jedzenie | dar calego jedzenia (+2 Charm za jednostke), druzyna bez jedzenia | `QuartermasterBehavior.cs:930-944` |
| Equip garrison | caly lup "Plundered" (bron i zbroja) | do zalogi | `QuartermasterBehavior.cs:806-817` |
| Supply militia | caly lup "Plundered" | do milicji | `QuartermasterBehavior.cs:860-874` |
| Salvage | caly lup "Plundered" | na przetop | `QuartermasterBehavior.cs:1410-1423` |
| Donate plundered equipment to the town | caly lup "Plundered" | dar dla miasta (+3 Trade i +2 Charm za sztuke) | `DonateEquipmentBehavior.cs:120-139` |

War stockpile i Inspect trophies tego nie maja: tam lewa strona to magazyn albo kupka trofeow, a Cancel ja zostawia.
Nasz `QuartermasterLaw.ResetPostfix` (:916-935) chroni tylko zbrojownie DTE. W CHANGELOG nie ma wpisu o tym dla Spoils (wpis 30.08, `CHANGELOG.md:5921`, dotyczy DTE).
Naprawa: Z5b (rozdz. 1.8). [P] brak pomiaru, jak w 1.2.

### 1.4 Spoils "Equip and train the leader" (menu Twojego pod-klanu)

- [K] `SubClanBehavior.cs:1700-1745`: za kazda sztuke oddana dowodcy XP = wartosc x mnoznik stanu x **0.05** x ilosc (:1724).
  Trafia do umiejetnosci wedlug typu przedmiotu (`GetSkillObjectForItemType`, :1766): bron jednoreczna i tarcza One Handed,
  dwureczna Two Handed, drzewcowa Polearm, luk i strzaly Bow, kusza i belty Crossbow, bron miotana Throwing, helm, korpus, nogi,
  rece i plaszcz Athletics, kon i uprzaz Riding. Nie ma przerwy ani sufitu. Rosnie tez poziom bohatera.
- Rzeczy laduja w taborze jego partii. **Jesli partii nie ma, przepadaja**, a XP i tak jest naliczane (:1720-1723).
- Ile [P items-dump.csv, wartosci po HistoricalPrices; konie nie sa przeliczane - `HistoricalPrices.cs:15-27` "Konie bez zmian"]:

| Przedmiot | Wartosc | XP dowodcy | Poziomy (okolice 100, tempo x6) |
|---|---|---|---|
| miecz t4 (mediana 44 sztuk) | 63 d | 3 One Handed | 0.02 |
| `battania_sword_5_t5` | 104 d | 5 One Handed | 0.03 |
| pancerz korpusu t6 (mediana 144 sztuk) / `sturgian_fortified_armor` | 2 594 / 2 316 d | 130 / 116 Athletics | ok. 0.1 |
| najdrozsza zbroja (`mountain_armor`) | 13 633 d | 682 Athletics | ok. 0.7 |
| unikat ROT `dragonbone_bow` (ROT wpisuje 100 000, my przywracamy 61 [P log Armoury :185 "HistoricalPrices: kontrola"]) | 61 d | 3 Bow | 0 |
| **kon** (mediana 75 koni) | 3 973 d | **199 Riding** | ok. 0.2 |
| **rumak szlachecki** (`noble_horse_western`) | 20 000 d | **1 000 Riding** | **ok. 1** |
| jednorozec ROT (`unicorn1-3`) | 30 000 d | 1 500 Riding | ok. 1.5 |

  Za bron i zbroje to drobne kwoty. Duze sa tylko konie, bo ich nie przeliczalismy. Kazda sztuka kosztuje Cie sam przedmiot (idzie do dowodcy),
  wiec to nie petla bez konca, ale XP z oddania. To samo zamkniecie odpala tez perki z 1.1 (Twoje wojsko dostaje XP drugi raz).

### 1.5 Handel: migawka BK liczy oddany sprzet jako sprzedany (sciezka 25, poza raportem `zrodla-xp.md`)

[K bk `BannerKings.Behaviours/BKTradeGoodsFixesBehavior.cs`; rejestracja `BannerKings/Main.cs:137`]:
- `MarketPatch` (:18-37) robi migawke Twojego taboru **tylko** po kliknieciu "town market" w menu miasta.
- `OnProfitMade` (:50-104) jest podpiety pod `OnPlayerTradeProfitEvent` (:41). Gra odpala to zdarzenie przy KAZDYM zamknieciu ekranu handlu,
  takze przy zysku 0 albo ujemnym [K gra `TradeSkillCampaignBehavior.cs:134-152`], w miescie albo wsi (:65).
- Kazda sztuke, ktorej teraz brakuje wzgledem migawki, BK uznaje za sprzedana. Porownanie idzie po calym przedmiocie ze stanem
  (`IsEqualTo`, :79), bez filtra stanu. XP Handlu = 0.5 x (zysk z gry + suma[max(0, cena - wartosc bazowa) + 5% ceny] x sztuki) (:86-98),
  dla przywodcy partii, czyli Ciebie (`PartyRole` 5 = PartyLeader, :100).

Skutki:
- (a) Sprzedaz zuzytego sprzetu UCZY handlu, ok. 2.5% ceny, nawet ze strata. Gra sama pomija sztuki ze stanem
  (`ProcessSales`, `ItemModifier == null`), ale BK nie. Pierwsza wersja dokumentu pisala, ze "handel zuzytym sprzetem nie uczy". To bylo bledne.
- (b) Migawka nie odswieza sie przy handlu we wsi ani przy BK "Trade" w zamku [K bk `BKSettlementActions.cs:415-429`, warunek `IsCastle`].
  Wszystko, co ubylo z taboru od ostatniego klikniecia targu miasta (sztuki oddane do zbrojowni DTE, do magazynu Spoils, dary, zolnierzom,
  zjedzone jedzenie, zuzyte strzaly), liczy sie jako "sprzedane". Prawdziwa sprzedaz z targu miasta liczy sie ponownie przy KAZDYM nastepnym handlu
  we wsi, az do kolejnego klikniecia targu w miescie.
- Przyklad [S]: 20 mieczy t4 (63 d) oddanych do zbrojowni po wizycie na targu daje ok. 32 XP Handlu przy kazdym handlu we wsi. Przy pensach to malo,
  ale petla jest prawdziwa i jest to dokladnie "oddajesz sprzet, rosnie exp". Naprawa: Z3 (migawka), docelowo Z15.

### 1.6 Drobniejsze z tej samej rodziny

| Co | Ile | Dowod |
|---|---|---|
| Spoils, resztki trofeow przy "Leave" ("used to train your soldiers") | wartosc / 10 jako XP wojska, dzielone **calkowicie** na glowe: `(int)(XP / liczba ludzi)`; przy pensach zwykle 0-2 XP na czlowieka [S: 40 sztuk po 30 d = 120 XP / 100 ludzi = 1]; rzeczy znikaja | [K] `LootCollectionBehavior.cs:3243-3272` (:3270), wolane z :1958 |
| Spoils "Donate plundered equipment to the town" | +3 Trade i +2 Charm **za sztuke**, nie od wartosci; rzeczy znikaja, a bezpieczenstwo i milicja rosna | [K] `DonateEquipmentBehavior.cs:229-230`; [D] EnableDonateEquipment true |
| Spoils "Donate food" | +2 Charm za jednostke jedzenia; jedzenie jest tanie, wiec to sklep z Charm | [K] `QuartermasterBehavior.cs:976`; [D] EnableQMDonateFood true |
| ~~BK obrzed "Weapon Offering"~~ | **martwy w ROT** | [K] BKROTPatch `DefaultFaithsInitializePatch.cs` (Prefix `return false`) wylacza `DefaultFaiths.Initialize`, ktore wczytuje `bk_faiths.xml` z obrzedami lance/great sword/axe/iron offering; 75 obrzedow BKROTPatch (`BKROTPatch.Rites`) to 35 CompositeOffering i 27 Offering z towarow oraz 13 Sacrifice - zaden nie bierze broni; [P] session log :1480 "AUDYT RELIGII: religii 22" = 22 religie `ROTReligions.cs` |

### 1.7 Co jest czyste (sprawdzone w kodzie)

- Nasza zbrojownia DTE i kwatermistrz: schowek, 0 XP [K DTE `ArmyArmoryBehavior.cs:393`; gra `InventoryLogic.cs:486`].
- Sprzedaz na targu wedlug gry: 0.5 x zysk ponad srednia cene zakupu, tylko sztuki bez stanu [K `TradeSkillCampaignBehavior.cs` `ProcessSales`, `RecordSales` :84-120].
  Czesc BK - **nie jest czysta** (1.5).
- Przetop: Crafting z sufitem, kosztem sztuki i staminy [K `SmeltTab.cs:146`].
- Naprawa wlasnymi rekami: 30 + 20 x tier, material i stamina [K `SmithMenu.cs:1094, 1129`]; u kowala za pieniadze 0 XP.
- Rozbiorka: [K `SmithMenu.cs:2226-2255`]. Kucie i pomocnicy: dni pracy x tier [K `Forge.cs:325-342`].
- RBL "repair equipment": martwe (`EquipmentEnabled` false [D]).
- Dwie sprawy niespojne z zasada "XP nie od ceny i nie za darmo":
  - **Paser** daje Roguery = 5% zaplaty [K nasz `SpoilsSeal.cs:483`, za Spoils `LootCollectionBehavior.cs:2618` w `ExecuteFenceSale`].
    To ten sam wzorzec, ktory odrzucam przy "equip leader" (Z2) i RBL (Z17). Z17 daje za to stale 25 Roguery za udana sprzedaz.
    Uczy sie sztuki paserstwa (znalezc kupca, ukryc lup, nie wpasc), nie wartosci towaru. Przy pensach to podobna kwota jak dzis
    [S: 40 sztuk po 20 d = 800 d, 5% = 40 XP], a znika zwiazek "drozszy lup = wiecej nauki". Wyjatek od zasady zostaje tylko w Handlu,
    bo tam cena jest trescia pracy.
  - **Samonaprawa przy kowadle** daje tyle samo XP za lekko drasnieta sztuke co za wrak [K `Forge.cs:511` `ProjectXp(r,false) x 0.6`;
    `SmithMenu.cs:1094` 30 + 20 x tier]. Z17.

### 1.8 Naprawa (trzy latki pod jednym wylacznikiem + latka Cancel)

**Z1 - DonationXpLaw (najwazniejsze, zamyka 1.1 i 1.2 w calosci).** Przeszukalem napisy we WSZYSTKICH 263 DLL w
`Modules/*/bin/Win64_Shipping_Client`: zero odwolan do `ItemDiscardModel`, `GetXpBonusForDiscarding` i `OnItemsDiscardedByPlayer`. Sa tylko
w samej grze (`bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll`) [K grep]. AIInfluence (`OpenScreenAsLoot`, `AddXpToTroop`) jest
wylaczony w `LauncherData.xml` [D]. Jest wiec jedna implementacja, `DefaultItemDiscardModel`. Korzystaja z niej napis na ekranie
(`InventoryLogic.cs:662, 672`), dymek przedmiotu (VM `vmc.cs:37904`) i wyplata (`DiscardItemsCampaignBehavior.cs:27`). Wystarczy jedno miejsce.
Nowy plik `Armoury/src/DonationXpLaw.cs`:

```csharp
internal static class DonationXpLaw
{
    // ODDANY SPRZET NIE UCZY (Jeff 09.10). Perki Giving Hands / Paid in Promise placily 35-300 XP za sztuke,
    // a ekrany Spoils z trwala lewa strona - przy kazdym zamknieciu. Zero w modelu = zero wszedzie.
    public static void Zero(ref int __result) { var s = Settings.Current; if (s == null || s.DonationXpOff) __result = 0; }
    internal static void ApplyAll(Harmony h)
    {
        try {
            var post = new HarmonyMethod(typeof(DonationXpLaw), nameof(Zero)); int n = 0;
            foreach (var name in new[] { "GetXpBonusForDiscardingItem", "GetXpBonusForDiscardingItems" })
            { var m = AccessTools.Method(typeof(DefaultItemDiscardModel), name); if (m != null) { h.Patch(m, postfix: post); n++; } }
            Log.Info("DonationXpLaw: XP za oddany sprzet (Giving Hands / Paid in Promise) wylaczone w " + n + "/2 metodach.");
        } catch (System.Exception e) { Log.Error("DonationXpLaw.ApplyAll", e); }
    }
}
```

- `Settings.cs`: `public bool DonationXpOff = true; // gear you give away (discard, Spoils screens, trophies, gifts) no longer trains anyone; off = vanilla`.
  Potem `python tools/gen_mcm.py`.
- `SubModuleMain.cs:56`: `DonationXpLaw.ApplyAll(_harmony);` obok `FairXpPatch.ApplyAll(_harmony);`.
- Drugie polowy perkow (clo gubernatora, pensje towarzyszy) zostaja. Opisy perkow `{=WaGKvsfc}` i `{=1eKRHLur}` podmieniam razem z nowym dzialaniem
  (pytanie a); jesli Jeff go nie chce - opis bez zdania o XP.
- `QuartermasterLaw.XpDonationsPostfix` staje sie zbedny. Zostawiam go, bo nic nie psuje.

**Z2 - Spoils "Equip and train the leader" bez XP.** W `SpoilsSeal.ApplyAll` (`SpoilsSeal.cs` ok. :1189-1195, obok "dar dla klanu", gdzie `_tSub`
i `Wire` juz sa): `Wire(h, _tSub, "GetSkillObjectForItemType", null, "NoTrainingSkill", "uzbrojenie dowodcy bez XP", true);` oraz
`public static void NoTrainingSkill(ref SkillObject __result) { try { if (Settings.Current == null || Settings.Current.DonationXpOff) __result = null; } catch { } }`.
Kod Spoils sam pomija XP, gdy umiejetnosc jest pusta (:1727), a rzeczy dalej ida do taboru dowodcy. Metoda jest `private static` (:1766), wiec JIT moglby ja wkleic;
jesli w grze dalej bedzie "Training XP" > 0, zapasem jest transpiler na `OnEquipLeaderScreenClosed` (`Hero.AddSkillXp` -> pusta metoda).
Waga: drobne dla broni i zbroi, istotne dla koni (1.4).

**Z3 - reszta rodziny (1.4-1.6), ten sam wylacznik DonationXpOff.**
- Prefiks `return false` na `LootCollectionBehavior.GiveLeftoverXpToTroops` (:3243; metoda robi tylko XP i komunikat).
- Transpiler `Hero.AddSkillXp` -> pusta metoda w `DonateEquipmentBehavior.OnDonateScreenClosed` i `QuartermasterBehavior.OnFoodScreenClosed`.
  Bezpieczenstwo, milicja i relacje zostaja. Charm i tak rosnie z kazdej relacji, ktora naprawde zyskasz (gra, BK x0.5 [D]).
- **Napisy Spoils** bez obietnicy treningu: postfiks na `LootCollectionBehavior.UpdateCompleteText` (:1835) podmienia tekst menu `{=RL_Menu_Complete}`
  (:1881, ostatnie zdanie "Items you leave behind will be used to train your soldiers.") na wersje bez tego zdania; postfiks na
  `OnCompleteLeaveCondition` (:1909) podmienia podpowiedz `{=RL_Tip_LeftBehind}` (:1929) na "Remaining {COUNT} items will be left on the field.".
  Postfiks, a nie plik napisow, bo kolejnosc nadpisywania napisow miedzy modulami nie jest pewna.
- **Migawka BK przy kazdym handlu** (1.5): postfiks na `InventoryScreenHelper.OpenScreenAsTrade` i `OpenTradeWithCaravanOrAlleyParty`
  (gra `InventoryScreenHelper.cs:317, 333`) ustawia prywatne statyczne pole BK `BKTradeGoodsFixesBehavior.roster` (`AccessTools.Field`) na kopie
  Twojego taboru w chwili otwarcia. BK liczy wtedy tylko to, co sprzedales przy tym straganie. Czlon "5% ceny" BK zostaje do Z15 (handel to praktyka
  sprzedazy, takze ze strata). Jesli pola nie ma (nowa wersja BK), latka loguje "nie znaleziono" i nic nie robi.

**Z5 i Z5b - Spoils: rzeczy nie przepadaja (osobny wylacznik SpoilsCancelKeeps, bo to blad przedmiotow, nie XP).**
- Z5: prefiks na `SubClanBehavior.OnEquipLeaderScreenClosed`: gdy dowodca nie ma partii, rzeczy wracaja do Twojego taboru [K :1720-1723].
- Z5b (1.3): jeden postfiks w `SpoilsSeal` na `InventoryLogic.Reset(bool fromCancel)` (gra :715). Gdy `fromCancel` i lewa strona (`_rosters[0]`,
  ta sama referencja co lista Spoils [K `InventoryLogic.cs:494-501`]) jest jedna z 7 list ekranow "take back" (pola `_equipLeaderScreenRoster`,
  `_donateScreenRoster` w SubClanBehavior; `_foodScreenRoster`, `_garrisonScreenRoster`, `_militiaScreenRoster`, `_salvageScreenRoster`
  w QuartermasterBehavior; `_donateScreenRoster` w DonateEquipmentBehavior - porownanie referencji przez `AccessTools.Field` na instancjach z
  `Campaign.Current.GetCampaignBehavior`), postfiks przenosi cala lewa strone na prawa (Twoj tabor). Funkcje zamkniecia Spoils dostaja pusta liste
  i nic nie robia: kazda liczy tylko sztuki z `Amount > 0` [K `QuartermasterBehavior.cs:947-951`, :820-823, :877-880, salvage :1426-1440,
  `DonateEquipmentBehavior.OnDonateScreenClosed`], a nasz `DonatePrefix` sprzedaje pusta liste. War stockpile i trofea sa poza lista celowo.
  Jeden postfiks zamiast siedmiu prefiksow: mniej miejsc, ktore moga sie rozjechac z nowa wersja Spoils.

**Uzasadnienie.** XP to umiejetnosc, a ta bierze sie z cwiczenia albo z walki, nie z oddania miecza.
- **[H]** Assize of Arms (1181) i Statute of Winchester (1285) kazaly ludziom MIEC bron, a cwiczenie bylo osobnym obowiazkiem.
  Proklamacja Edwarda III z 1363 nakazywala w swieta strzelac do celu. Wegecjusz, "De re militari" ks. I: rekrut cwiczy bron i marsz codziennie.
  Sprzet byl warunkiem cwiczen, nie ich zamiennikiem.
- **[L]** Syrio Forel uczy Arye cwiczeniem, nie podarunkiem ("Gra o tron"). Giermek uczy sie latami przy rycerzu ("Rycerz Siedmiu Krolestw").

---

## 2. Wszystkie umiejetnosci: skad rosna dzis, czy to ma sens, problem, propozycja

Ocena: OK (ma sens); DARMO (XP bez wysilku albo z niczego); KLIK (do wyklikania); MARTWA (nie ma jak rosnac albo nic nie daje);
NIESPOJNA (nasze zmiany przeciely zwiazek przyczyny i skutku). "Z" to numer zmiany z rozdz. 3.

### 2.1 Tabela

| Umiejetnosc | Skad rosnie dzis | Czy ma sens | Problem | Propozycja (liczby, uzasadnienie) |
|---|---|---|---|---|
| **One Handed** | ciosy w bitwie (RBM + nasz FairXp: XP wedlug obrazen, zabicie x2), arena 20% i turniej 50% (FairXp), bloki i parady (RBM), bitwy symulowane AI, straz RC (dniowka + 150 za bojke), BK "train guards", ksiazki BK, Spoils equip leader [K `FairXp.cs`; D ArenaXpPercent 20, TournamentXpPercent 50; K RC `Work.cs` :365, :381] | OK | Spoils: XP za oddanie (1.4), w nowej monecie kilka XP za sztuke. Drobne: blok na arenie placi wiecej niz trafienie (RBM liczy blok jak bitwe) [K `CreateMeleeBlowPatch`] | **Z2** (domkniecie rodziny). Reszta bez zmian. Skutek umiejetnosci jest u nas mocny i spojny: Prawo Tieru 35 na tier (723 broniom podniesiony wymog [P session log :281]), AutoParry, SkillsDecide, brama GT 100 |
| **Two Handed** | jak One Handed | OK | jw. | **Z2** |
| **Polearm** | jak One Handed, perk Drills dla wojska | OK | jw. | **Z2** |
| **Bow** | trafienia (FairXp), **RBM: 30 XP za KAZDY strzal bohatera, takze chybiony, takze na arenie i w turnieju** [K `rbmcombat.cs:3628-3677`], BK polowanie (czas), Spoils (luk, strzaly) | KLIK | Na arenie trafienie daje 20% (FairXp), a strzal w sciane pelne 30. Strzelanie uczy wiec wiecej niz trafianie. W bitwie 60 strzalow to 1 800 surowego, ok. 2 poziomy w okolicy 100, za samo wypuszczenie strzal [S] | **Z7:** XP za strzal tylko w prawdziwej bitwie (jest MapEvent) i w 1/3 (luk 10, kusza 20, bron miotana 17, proca 10). Na arenie, w turnieju i w cwiczeniach 0. Trafienie juz placi wedlug obrazen; strzal pod ostrzalem cos uczy, ale mniej niz celny. [H] cwiczenie z 1363 to godziny przy celu, nie oplata za strzale |
| **Crossbow** | jak Bow, RBM **60** za strzal | KLIK | jw. | **Z7** (20 w bitwie, 0 poza nia) |
| **Throwing** | jak Bow, RBM **50** za rzut | KLIK | jw. | **Z7** (17) |
| **Riding** | marsz konno (0.3 x predkosc co 4 h), walka konno, symulacja, **Spoils equip leader: kon 200-1 500 XP** [K `DefaultSkillLevelingManager` :297-305; P items-dump: konie nieprzeliczone] | OK poza Spoils | Spoils: rumak szlachecki = ok. 1 poziom za oddanie konia (1.4). Marsz daje malo (ok. 1.4 na 4 h przy 4.55 [P "Audyt predkosci"]); glowne zrodlo to walka | **Z2** (tu ma najwieksza wage). Reszta bez zmian; przy tempie swiata "ksiazkowym" XP z marszu spadnie razem z predkoscia, ale to grosze |
| **Athletics** | marsz pieszo, walka pieszo, praca RC, Spoils (zbroja), BK polowanie | NIESPOJNA (brak wymogu u bohaterow) | Skutki Atletyki juz dzialaja: predkosc biegu +0.1%/pkt i -0.1%/pkt kary za wage [K gra `DefaultSkillEffects.cs:260-261`; `SandboxAgentStatCalculateModel`: predkosc = 0.7 x (1 + 0.001 x Atl) - 0.2 x (1 - 0.001 x Atl) x obciazenie / waga ciala]; nasz `BattleWind` x(1 + Atl/500) do oddechu. Niezaleznie od Atletyki ROT karze plyte: bohater w pelnej plycie zamach i pchniecie x0.9, predkosc w walce x0.66 (AI zawsze, gracz przy `PlayerPlateEncumbrance`) [K ROT `ROTAgentStatCalculateModel.UpdateAgentStats`; D RealmOfThrones_v1.json:49 true]. **Brakuje tylko wymogu**: Prawo Wagi (0.25 kg na punkt, na sztuke [K `ItemReq.cs:8-31`; D `docs/pancerze-waga-atletyka.md`]) pilnuje zolnierzy zawsze, a bohatera tylko raz, `StartKit` na starcie [K `StartKit.cs`]. Ekran ekwipunku pyta `CharacterHelper.CanUseItem`, ktory patrzy tylko na `RelevantSkill`, a zbroja jej nie ma. To sprzeczne z zasada Jeffa z 29.08 "umiejetnosci sa swiete... CALY ekwipunek" (`ItemReq.cs:8-11`) | **Z16** (po pytaniu b). Rekomenduje **zakaz** jak u zolnierzy, liczony **na sztuke** (ten sam `ItemReq`), bez nowej kary (byloby trzecia kara za ten sam ciezar). Warunek dla AI: najpierw `SkillSinew` dla bohaterow (podnies Atletyke lordow i towarzyszy do ich wlasnego sprzetu z XML, nigdy nie obnizaj) - dzis co najmniej 268 z 811 lordow ma sztuke ponad swoj wymog [S: skrypt `krytyka13b/lordarmor4.py` na ROT_lords.xml + lords.xslt: 202 z 298 "dandy" (Atl 70), 66 ze 101 "chatelaine" (Atl 100)]. Zrodlo XP: na postoju bohater w zbroi od 15 kg dostaje 10 Atletyki dziennie (cwiczenie w zbroi). [H] Boucicaut wedlug swojej kroniki (ok. 1409) cwiczyl codziennie w pelnej zbroi |
| **Smithing** | kuznia: dni przy kowadle x tier (100 / dzien / tier, sufit 1 300 x tier), naprawa wlasnymi rekami 30 + 20 x tier, rozbiorka, przetop i rafinacja (sufit), CRAFT BK (sufit 500 x tier, od dawnych wartosci) [K `Forge.cs` :325-342, `SmithMenu.cs` :1094, :1129; D Armoury.json] | **OK, najlepiej zrobione** ("placimy za czas i trudnosc, nie za cene wyrobu") | (1) samonaprawa daje to samo XP za drasniecie co za wrak [K `Forge.cs:511`]; (2) kowale miast i zolnierze naprawiajacy swoj sprzet (TroopSelfMend) nie maja zadnej umiejetnosci | **Z17:** XP samonaprawy x udzial naprawionego stanu (min. 25%). **Z18** (pozniej): platnerz w taborze - najlepszy Smithing w druzynie skraca czas napraw wojska; XP tylko za godziny faktycznie przydzielone kowalowi (jak dni przy kowadle w Forge), z sufitem; zuzycia metalu nie ruszam bez ponownej kalibracji T3. [H] wyprawy angielskie XIV w. mialy na zoldzie platnerzy i kowali |
| **Scouting** | co godzine marszu: predkosc x (1 + ludzie^0.66) x 0.15 (las, pustynia, snieg 0.25), placone dopiero od 5; AI ma dodatkowo 3-5 na godzine [K `DefaultSkillLevelingManager.cs:418-440`]; kryjowki, slady | DARMO (AI) + NIESPOJNA (noc) | (1) XP rosnie z dlugoscia kolumny: czynnik 5.6 przy 10 ludziach i 53 przy 400. (2) Partia do ok. 16 ludzi na rowninie nie dostaje nic (prog 5) [S]. (3) Dodatek tylko dla AI. (4) Nasza noc i teren (`TerrainEase`, `NightRest`, `SightRange`) w ogole nie czytaja Scouting [K grep Armoury]. (5) Wzor mnozy predkosc, wiec tempo ksiazkowe i drogi +15% (decyzja H) przesuwaja XP | **Z10:** stale XP za godzine marszu: 15 na rowninie, 25 w trudnym terenie. To tyle, ile dzis dostaje kolumna 100 ludzi przy 4.55 [S: 4.55 x 21.9 x 0.15 = 15]. Karawany polowa. Bez progu i bez dodatku tylko dla AI. Skutek: gracz ze 100 ludzmi bez zmian, lord AI z 350-450 ok. -50%, maly oddzial z 0 na 15. **Z13:** zwiadowca noca. [H] przewodnicy i "scurriers" w armiach angielskich XIV w.; [L] zwiadowcy Nocnej Strazy zyja z tropienia za Murem |
| **Tactics** | 2% XP walki dowodcy (symulacja, trafienia pod jego dowodztwem) [K :86-90, :125-129] | OK / NIESPOJNA | Skutek w grze: przewaga w symulacji +0.1% na punkt, mniej poswieconych przy ucieczce. H3 (`LosersFlee.cs`) nie widzi dowodcow: p = 0.15 + 0.35C + 0.15T + 0.15O + 0.20Q - 0.10F(1-T), w granicach 5-65% [K naglowek] | **Z12:** w H3 p + 0.10 x clamp((Tactics zwyciezcy - Tactics przegranego) / 300, -1, 1). Najwyzej 10 punktow, widelki 5-65% zostaja. [H] pod Crecy (1346) Edward III zakazal wychodzenia z szyku w poscig i po lup; pod Bannockburn (1314) rozbici Anglicy stracili wielu ludzi w ucieczce przez potok |
| **Roguery** | rabunek (0.5 x wartosc towarow + konie x100 + bydlo x25), napady na tabory, sprzedaz jencow (2 x tier x liczba [K `DefaultSkillLevelingManager.cs:261-269`]), lapowki, niewola 0.5 na godzine, zaulki, kryjowki, Spoils (zbieranie lupu, paser 5% zaplaty [K `SpoilsSeal.cs:483`]), RBL szpieg 50 i sabotaz 150 za zloto [K RBL `rbl.cs:2123-2132`] | OK w wiekszosci | (1) Paser i RBL: XP od zaplaty (1.7). (2) Sprzedaz jenca daje grosze (lord t6: 12 XP), a decyzja I (jency do domu, przestepcy na Mur) i tak usunie wiekszosc sprzedazy w Westeros. (3) Nowa moneta: towar z rabunku w pensach, wiec czesc "towary" daje kilka-kilkanascie razy mniej, a konie i bydlo bez zmian [K `HistoricalPrices.cs`; P 525 rabunkow w 120 dob] | **Z17:** paser 25 Roguery za udana sprzedaz zamiast 5% zaplaty; RBL 0 XP za zaplate; wyslanie na Mur bez Roguery (to sad, nie handel - przy I1). Okupu nie ruszam (12 XP, koszt bez skutku). Rabunek "osada po osadzie" (projekt w toku): XP od liczby spladrowanych osad, nie od ceny towaru |
| **Charm** | relacje (BK x0.5 [D CharmXpMultiplier 0.5]), perswazja, pojedynki ROT, BK "meet nobility", **Spoils dary (+2 za sztuke, +2 za jedzenie)**, **RBL glos w radzie +100 za zloto** [K `rbl.cs:1320`; D PoliticsEnabled true] | OK + DARMO | XP za rzeczy i za zloto, a nie za zjednanie sobie ludzi | **Z3** (dary 0), **Z17** (RBL 0 XP za zaplate). Dar, ktory naprawde poprawia relacje, i tak uczy Charm przez relacje |
| **Leadership** | werbunek (liczba x tier x 2), awanse 2.5%, nadwyzka XP po bitwie 2.5%, dowodzenie armia co godzine, BK nowy rod rycerski +40 000, rada [K :190-196, :327-370, :443-455; BK `BKKnighthoodBehavior` :240-252] | OK | Brak zwiazku z treningiem wojska: dzis cwiczenie w ogole nie zalezy od dowodcy | **Z14a/Z14b musztra:** XP wojska x dowodca (Leadership / 170, granice 0.5-1.5; 170 to mniej wiecej srednia lordow z danych). Dowodca dostaje jako Leadership 2.5% rozdanej musztry, tyle co dzis za awanse |
| **Trade** | Twoj handel wedlug gry: 0.5 x zysk ponad srednia cene zakupu, **tylko sztuki bez stanu** [K `TradeSkillCampaignBehavior` `ProcessSales`/`ProcessPurchases`: `ItemModifier == null`]; **BK: 0.5 x (zysk + (cena - wartosc bazowa) + 5% ceny) za kazda sztuke, ktorej brakuje wzgledem migawki z targu miasta, ze stanem lub bez** (1.5); karawany; **Spoils dar dla miasta +3 za sztuke** | NIESPOJNA + KLIK | (1) Sprzet oddany po wizycie na targu liczy sie jako sprzedany przy kazdym handlu we wsi (1.5). (2) Zuzyty sprzet uczy tylko przez czlon BK 5% obrotu; prawdziwy zysk "kup zuzyte, napraw, sprzedaj" nie uczy, bo gra pomija sztuki ze stanem. Przy kluczu "(przedmiot, stan) z zakupu" tez by nie uczyl: naprawa zmienia stan (kowal "doprowadza do stanu fabrycznego" [K `SmithMenu.cs:349`], samonaprawa przywraca stan oryginalny [K `ArmouryBehavior.cs:2379-2381`]), a gra szuka zapisu po samym przedmiocie [K `RecordSales` :84-120]. (3) Gra i BK licza ten sam zysk dwa razy (gra 0.5 x zysk, BK znowu 0.5 x zysk + wlasny czlon). (4) Dar dla miasta uczy handlu bez handlu | **Z3** (migawka przy kazdym otwarciu handlu; dar 0). **Z15:** jedna ksiega handlu Armoury zamiast `BKTradeGoodsFixesBehavior.OnProfitMade` (prefiks `return false`): liczy tylko sztuki sprzedane w tej sesji (`_transactionHistory.GetSoldItems`); podstawa kosztu wedlug (przedmiot, stan), **przenoszona przy naprawie** (wpis ze starym stanem -> nowy stan, plus robocizna i material po cenie targu); XP = 0.5 x max(0, cena - koszt) dla sztuk ze stanem (sztuki bez stanu liczy jak dzis gra, bez dublowania) + 2.5% ceny za kazda sprzedana sztuke (obrot, dzisiejszy czlon BK). Cena jest tu trescia pracy, wiec XP od ceny zostaje tylko w Handlu. [H] Pegolotti, "Pratica della mercatura" (ok. 1340): handel bronia byl zwyklym handlem, a nauke dawala praktyka |
| **Steward** | **jedzenie:** codziennie 100 x dzienne zuzycie x (rodzaje - 2) / 3, od 4 rodzajow [K `DefaultSkillLevelingManager.cs:457-464`]; ukonczony projekt budowy 1 000; rzadzenie (wzrost dobrobytu x30); wydany wplyw x10; BK sprawy notabli; BK nowy rod rycerski +60 000 | DARMO (jedzenie) + MARTWA (budowy) | (1) Lord z 350 ludzmi i 5 rodzajami: 1 750 surowego dziennie [S], najwiekszy staly strumien XP w grze. (2) **Zima +50% XP** tylko dlatego, ze WinterBite podnosi zuzycie [K `WinterBite.cs:154-158`, latka na `CalculateDailyBaseFoodConsumptionf`]: wiecej zjedzone = wiecej nauki. (3) Jedno jablko i jeden ser to dwa "rodzaje" wiecej. (4) Budowy stoja: 7 poziomow ukonczonych w calym swiecie w 120 dob, 175 z 223 budow stoi [P "Budowy:"]. (5) Nasz kwatermistrz, naprawy i zakupy nie czytaja Stewarda | **Z8:** XP z jedzenia = 50 x pierwiastek(ludzie) x (rodzaje - 2) / 3, rodzaj liczy sie od pol dnia zapasu dla calego wojska; **zima x1.25** jako premia za trudnosc (jak trudny teren w Z10), liczona od pory roku, nie od zjedzonej ilosci. Druzyna 100 ludzi: 500 jak dzis; 350 ludzi: 935 (-47%), zima 1 169 zamiast ok. 2 625; 10 ludzi: 158 zamiast 50. Steward z budow wroci sam, gdy budowy rusza (ekonomia). **Z18** (pozniej): kwatermistrz Armoury czyta Steward. [H] "Seneschaucy" (ok. 1270) i Walter z Henley: zarzadca uczy sie rachunkami i zapasami; [L] Winterfell liczy zapasy przed zima ("Gra o tron", Vayon Poole) |
| **Medicine** | leczenie rannych (liczba x tier, x2 w miescie), operacje w bitwie (10 x tier udana, 5 nieudana), BK opieka rodziny, zaraza oblezen [K :272-276, :308-325; `CampFever.cs`] | NIESPOJNA (H3) | Skutek dziala: przezycie rannych, tempo leczenia (`SlowHealing`: gracz 50%, AI 100% [D]), zaraza obozowa -0.25% na punkt. Ale **H3 liczy smierc przegranych bez chirurga** [K `LosersFlee.cs`]. Po decyzji F (H3 takze przy Twojej autobitwie) Twoj chirurg przestanie liczyc sie i u Ciebie | **Z12:** smiertelnosc przegranych w H3 x (1 - 0.25 x min(1, Medicine chirurga przegranego / 250)). Przy 17.2% zabitych [P "Przegrani (H3)"] chirurg 250 daje 12.9%. [H] John of Arderne (XIV w.): armie wozily chirurgow; [L] maester z ogniwem medycyny przy kazdym dworze |
| **Engineering** | tylko oblezenia (machiny 30 + 2 x trudnosc, wylom 250, godziny oblezenia), ksiazka BK [K :94-100, :333-354, :391-397] | **MARTWA** | Oblezen prawie nie ma: Siege 7, SiegeOutside 8, SallyOut 15 na 120 dob [P bitwy.log]. Premia gubernatora +0.25% na punkt do budowy i perki (Carpenters, Stonecutters, Military Planner, Clockwork) **sa wylaczone przez nasze BuildFunding** [K `BuildFunding.cs:62-79` podmienia caly wynik na oplacona prace] | **Z11:** oplacone punkty budowy x (1 + 0.0025 x Engineering gubernatora) plus perki budowy, liczone na oplaconej pracy. XP: 10 Engineering za kazdy oplacony punkt, ok. 100 dziennie dla gubernatora [P "Budowy oplacone": 695 punktow na 68 osad w dobe]. [H] Master James of St George uczyl sie przy budowach zamkow Edwarda I w Walii (1277-1300+) |
| **Lordship (BK)** | rada (10 x liczba radnych dziennie u glowy rodu), stanowiska, tytuly, roszczenia [K BK `BKClanBehavior` :864, `CouncilData` :277] | OK | nasze zmiany tego nie dotykaja | bez zmian |
| **Scholarship (BK)** | +1 dziennie ponizej 30, filozof w radzie do 15 dziennie, styl zycia, ksiazki (1 500 XP) i jezyki, lekcje [K BK `BKEducationBehavior` :146-158] | OK, ale 24 ksiazki DARMO | Dwie skale cen [P items-dump]: 24 ksiazki BK (umiejetnosci, opowiesci) maja wartosc 7-10 d, bo BKROTPatch dzieli ja przez 100 [K bkrot `BKItemsInitializePatch.cs:87`]; 36 ksiazek ROT (religie, historie, slowniki) ma 1 000-1 500 d. BK liczy cene "wartosc x 1000" [K BK `BKEducationBehavior.cs:436, 448`], a nasz `BookTranspiler` zamienia to na "wartosc x 1" [K `HistoricalPrices.cs:860-873`]. Ksiazka-opowiesc daje darmowy punkt skupienia | **Z6:** w `HistoricalPrices.Apply` ksiazki kategorii Book z wartoscia ponizej 100 x100 (24 sztuki: 700-1 000 d). Ksiazki ROT bez zmian, `BookTranspiler` bez zmian. Wartosc jest wtedy jedna dla kupca, targu i lupu. [H] rekopis w XIV w. kosztowal od kilku szylingow do kilku funtow (1 funt = 240 d) |
| **Theology (BK)** | obrzedy ROT (ofiary z towarow i zwierzat, poboznosc x1.2), kaplan, perk 2 dziennie, zalozenie wiary [K BK `BKSkillBehavior` :58; bkrot `BKROTPatch.Rites`] | OK | Ofiara z broni BK w ROT nie istnieje (1.6) | bez zmian |
| **Mariner (Naval)** | trafienia na morzu, symulacja morska 2% [K `naval.cs` :108862-109160] | OK | nasze drogi i wyspy nie dotykaja statkow | bez zmian |
| **Boatswain (Naval)** | uszkodzenia przyjete x0.1, naprawy statkow x0.05 | OK | - | bez zmian |
| **Shipmaster (Naval)** | rejs 1.4 x predkosc co 4 h | OK | - | bez zmian |

### 2.2 Doswiadczenie zolnierzy (awanse), bo tu laczy sie wszystko powyzej

| Zrodlo | Kto dostaje | Ile | Ocena | Zmiana |
|---|---|---|---|---|
| Walka (bitwy, symulacja, oblezenia) | wszyscy | gra | OK | - |
| **Trening dzienny** - zasada samej gry, BK ja tylko kopiuje [K gra `DefaultPartyTrainingModel.cs:21-31`, BK `BKPartyTrainningModel.cs:41-50`] | partie lordow AI spoza klanu gracza, nie w Twojej armii, poza bitwa, **takze w marszu**; Twoja druzyna, partie Twojego klanu i lordowie w Twojej armii - nic | 10 + 2 x tier na czlowieka dziennie, glowa rodu 15 + 3 x tier | wyrownanie dla AI, ktore nie ma kanalow XP gracza; nierowne | 171 (zatwierdzone): x udzial uzbrojonych oddzialu (0..1) [K n172 `ArmsDrill.cs:222-235`; P "Cwiczenia (171)": partie AI x0.36-0.40 w 1. dobie, x0.63-0.66 w 40.]. Potem **Z14a** (Ty) i **Z14b** (AI) |
| Perki w treningu (Combat Tips, Raise the Meek, Drills, Bow Trainer, Walk It Off...) | partie z perkiem, takze Twoja [K BK tamze :52-117] | wedlug perku | OK | zostaja |
| Budynki zalog (Training Fields, codzienny projekt Drills 8) | zalogi | gra [K `DefaultBuildingTypes.cs:197, 256, 325`] | OK, to sa cwiczenia | 171: zalogi AI cwicza wedlug broni w zbrojowni [K n172 `ArmsDrill.cs:189-200` Gated; P x0.93-0.96]. **Twoje zalogi po staremu** (nizej) |
| BEE obozy szkoleniowe AI | zamki AI | - | zamkniete przez 170 [P "XP obozow AI: 10-16" zablokowanych dziennie] | - |
| Discard z perkiem / ekrany Spoils | wojsko gracza | 75-300 za sztuke | KLIK | **Z1** |
| Spoils resztki trofeow | wojsko gracza | wartosc / 10, zwykle 0-2 na glowe | DARMO (drobne) | **Z3** |

**Twoje zalogi i Twoja druzyna a 171 (odpowiedz na pytanie K).** 171 ma dla zalog dwie czesci: zakupy brakujacej broni na targu ich miasta i cwiczenia
zalezne od broni. Obie sa dla Ciebie wylaczone tym samym ustawieniem [K n172 `ArmsDrill.cs:189-200`: zaloga gracza tylko przy `GarrisonBuysGearPlayer`;
D n172 `Settings.cs:402` `GarrisonBuysGearPlayer = false`]. Zakupy bralyby pieniadze z Twojej kiesy bez pytania, a cwiczenia wedlug broni bez zakupow
zatrzymalyby szkolenie, bo zaloga nie mialaby skad uzupelnic broni (DTE kasuje zbrojownie zalog co dobe, komentarz :184-187). Twoje zalogi cwicza wiec
po staremu z budynkow, bez sprawdzania broni. Po wlaczeniu "Garrison Buys Gear Player" w MCM kupuja brakujaca bron z Twojej kiesy, a ich cwiczenia zaczynaja zalezec
od broni jak u AI. Twojej druzyny 171 nie dotyka (`mp != MainParty`, `ActualClan != PlayerClan`), a gra i tak nie daje jej codziennego treningu, tylko perki.

### 2.3 Sprawy przekrojowe

- **Dlugi rok (364 dni).** Na dobe XP jest takie samo jak w zwyklej grze, ale bohater starzeje sie 4.3 razy wolniej [K `Calendar.cs`].
  Przez cale zycie zbiera wiec 4.3 razy wiecej XP z czasu: marsz, rada BK, jedzenie, czytanie. Przyklad [S]: radny BK z +10 dziennie
  to 3 640 surowego na rok gry (w zwyklej grze 840), czyli ok. poziomu 45 z samej rady. Hamulcem jest limit nauki. Nie tne
  na slepo: suwak `HeroLearningPercent` z AUDYT-CZAS-MAPA nie jest wdrozony [K `Settings.cs`]. Najpierw Z4 i pomiar roczny.
- **Nowa moneta.** `HistoricalPrices` **potanil** bron i zbroje (wzor gry t1 -> t6 x157 byl przesadzony [K `HistoricalPrices.cs:15-27`]); konie zostaly
  bez zmian. Skutki: XP liczone od wartosci (Spoils equip leader, resztki) spadlo do grosza za bron i zbroje, ale nie za konie; XP liczone od tieru
  (discard) stalo sie tanie do kupienia (1.1). Smithing liczony od dawnych wartosci jest naprawiony [K `HistoricalPrices.cs:880-890`].
  Trade i Roguery z rabunku licza sie w pensach i trzeba je zmierzyc (Z4).
  Uwaga na przyszlosc: probki w linii "ArmsPricing ... Przyklady" sa logowane PRZED `HistoricalPrices.Apply` (`ArmouryBehavior.cs:998`, `ArmsPricing.cs:138-150`),
  wiec pokazuja dawne wartosci; pierwsza wersja tego dokumentu sie na tym pomylila. Logowac je po Apply albo w obu skalach.
- **Tempo swiata i drogi (decyzja H).** Wzory zwiadu i marszu mnoza predkosc. Z10 liczy XP od godzin, nie od predkosci, i jest na to odporne.
- **Proba zimy (decyzja J).** Dzis zima podnosi XP zarzadcy o ok. 50% przez wieksze zuzycie. Z8 (zima x1.25 od pory roku) trzeba wgrac przed proba zimy.
- **Gracz a AI.** AI ma codzienny trening z zasady gry i dodatek zwiadu; gracz nie. 171 celowo wylacza gracza z cwiczen wedlug broni (2.2).
  Po Z1-Z3 i Z14a obie strony maja trening; po Z10, Z14b i Z17 te same reguly, z jednym wyjatkiem do decyzji Jeffa: czy udzial uzbrojonych (171)
  liczy sie tez dla Twojej druzyny (pytanie c).
- **Logi.** [P] W logach nie ma ani jednej linii o XP bohaterow. Sa tylko linie startowe: "FairXp: nauka z ciosu wyrownana w 3 modelach",
  "XP kowalstwa od dawnych wartosci 3/3". Dlatego Z4 idzie przed Z7-Z14b.
- **Do wiadomosci (poza umiejetnosciami):** oplata za awans zolnierza w grze znika z obiegu [K 171 rozpoznanie (d) 7:
  `PartyUpgraderCampaignBehavior` :139-150]. To sprawa ekonomii, wedlug zasady "jak znika, to zamykamy".

### 2.4 Czego brakuje (nowe zwiazki przyczyny i skutku)

1. **Musztra (Z14a, Z14b):** trening wojska z czasu, dowodcy i broni, jedna regula dla gracza i AI, bez oslabiania armii AI srednio.
2. **Zwiadowca noca i w terenie (Z13):** kara nocnego marszu i lasu x (1 - min(0.5, Scouting / 400)). Mistrz zwiadu (200+) zmniejsza
   nocne -0.5 do -0.25 [P linia startowa "Kary terenu po naszemu"]. Wzrok noca: NightSightFactor + (1 - NightSightFactor) x min(0.5, Scouting / 400).
   Do tego +5 XP za godzine nocnego marszu, bo to najtrudniejsza robota zwiadowcy.
3. **Inzynieria przy budowach (Z11).**
4. **Medycyna i dowodcy w H3 (Z12).**
5. **Handel sprzetem ze stanem i jedna ksiega handlu (Z15).**
6. **Wymog Atletyki u bohaterow (Z16):** Prawo Wagi na sztuke plus cwiczenie w zbroi na postoju.
7. **Platnerz w taborze, kwatermistrz Armoury = Steward (Z18, pozniej):** XP tylko z godzin faktycznie przepracowanych przy naprawach, z sufitem.
8. **Nowe dzialanie perkow Giving Hands / Paid in Promise** (pytanie a): mniejsze zuzycie wydanego sprzetu, nie nagroda za zapas.
9. Niski priorytet: ucieczka konno (RC HorseFlight) z Riding i Scouting; gospodarz turnieju GT dostaje Steward i Charm za zorganizowanie
   (dzis tylko renome [K `TourneyBehavior.cs:600`]).

---

## 3. Zmiany do zrobienia, w kolejnosci (najpierw male i bezpieczne)

Kazda zmiana ma: wylacznik w MCM (domyslnie wlaczony, angielski opis), wpis w CHANGELOG z "Ryzyko / co sprawdzic" (zasada 0) i test.
Wgrywanie tylko po "wgraj" albo na zgode na noc, zawsze z kopia. Jedna zmiana w DLL na raz, chyba ze dzielimy wylacznik (Z1-Z3).
Numery Z sa te same co w wersji 1 (latwiej porownac); nowe maja litery.

| Kolejnosc | Nr | Co | Gdzie w kodzie | Wylacznik | Test | Zalezy od |
|---|---|---|---|---|---|---|
| 1 | **Z1** | XP za oddany sprzet = 0 (perki, wszystkie ekrany Spoils, lupy, ROT) | nowy `Armoury/src/DonationXpLaw.cs`; postfiks na `DefaultItemDiscardModel.GetXpBonusForDiscardingItem` i `...Items`; `Settings.cs`; `SubModuleMain.cs:56` | DonationXpOff | log "DonationXpLaw: ... 2/2 metodach"; w grze: kwatermistrz z Giving Hands, tunika t1 przelozona w lewo nie pokazuje "Your troops will get"; War stockpile otwarty i zamkniety (takze Cancel) nie zmienia XP oddzialow; dymek przedmiotu bez XP; autotest 10 dob bez bledow (AI tego nie uzywa) | - |
| 2 | **Z2** | Spoils equip leader bez XP | `SpoilsSeal.cs` `ApplyAll` (ok. :1189-1195): `Wire` postfiks na `SubClanBehavior.GetSkillObjectForItemType` -> null | DonationXpOff | komunikat "Training XP: 0" takze dla konia, umiejetnosci dowodcy bez zmian, linia SpoilsSeal "uzbrojenie dowodcy bez XP"; jesli > 0, transpiler | Z1 (ten sam wylacznik) |
| 3 | **Z3** | Spoils: resztki, dar dla miasta, jedzenie bez XP; napisy bez obietnicy treningu; migawka BK przy kazdym otwarciu handlu | `SpoilsSeal.cs`: prefiks `return false` na `LootCollectionBehavior.GiveLeftoverXpToTroops` (:3243); transpiler `Hero.AddSkillXp` -> pusta metoda w `DonateEquipmentBehavior.OnDonateScreenClosed` i `QuartermasterBehavior.OnFoodScreenClosed`; postfiksy na `UpdateCompleteText` (:1835) i `OnCompleteLeaveCondition` (:1909); postfiks na gra `InventoryScreenHelper.OpenScreenAsTrade` / `OpenTradeWithCaravanOrAlleyParty` (:317, :333) ustawiajacy pole BK `BKTradeGoodsFixesBehavior.roster` | DonationXpOff | "Leave" bez "Soldiers distributed ... combat experience"; menu i podpowiedz "Leave" bez "train your soldiers"; dar dla miasta nie zmienia Trade ani Charm, a bezpieczenstwo i milicja rosna dalej; **handel**: wizyta na targu miasta, 20 sztuk do zbrojowni DTE, kupno 1 sztuki zboza we wsi - Trade bez zmian; sprzedaz 1 miecza na targu - Trade rosnie raz, a nastepny handel we wsi juz go nie liczy | Z1 |
| 4 | **Z5 + Z5b** | Rzeczy dowodcy bez partii nie przepadaja; Cancel na 7 ekranach "take back" = nic sie nie dzieje | `SpoilsSeal.cs`: prefiks na `SubClanBehavior.OnEquipLeaderScreenClosed` (dowodca bez partii -> Twoj tabor) [K :1720-1723]; postfiks na gra `InventoryLogic.Reset(bool)` (:715): przy `fromCancel` i lewej stronie = jednej z 7 list Spoils (1.8) cala lewa strona do `_rosters[1]` | SpoilsCancelKeeps | kazdy z 7 ekranow otwarty i zamkniety przez Cancel: tabor bez zmian (liczba sztuk i jedzenia przed = po), zero XP, zero sprzedazy, zero daru; Done dziala jak dotad; War stockpile i trofea - Cancel bez zmian; dowodca bez partii: przedmioty z powrotem u Ciebie, licznik w logu | Z2 |
| 5 | **Z14a** | Musztra Twojej druzyny (i partii Twojego klanu, i lordow w Twojej armii) | rozszerzenie 171 w tym samym postfiksie: n172 `ArmsDrill.TrainingPostfix` (:222-235, Priority.Last na modelu zewnetrznym, `_tDepth`): dla partii, ktorym gra nie daje bazy, dodaje baza x dowodca x dzien; potem udzial uzbrojonych tylko wedlug decyzji c(2); licznik godzin ruchu partii z `WorldMeasure.cs` (miara marszu T6); Inni wylaczeni jak w `Gated` (`Undead.Party`, :192) | DrillLaw | Twoja druzyna 100 ludzi t1-t3 na postoju w miescie 10 dob: XP oddzialow rosnie o baza x Leadership/170 x 1.5 na czlowieka; w marszu x0.9; glodna albo z dlugiem snu 0; ksiega Z4 "musztra gracza"; AI bez zmian (porownanie linii "Cwiczenia (171)" przed i po) | 171 w grze albo w tej samej paczce; pytanie c |
| 6 | **Z4** | Ksiega umiejetnosci (sam log) | nowy `Armoury/src/SkillLedger.cs`: prefiks na `HeroDeveloper.AddSkillXp` (umiejetnosc, kwota surowa i efektywna, bohater: gracz, towarzysze, lordowie AI) i na `TroopRoster.AddXpToTroop`; zrodlo z krotkich prefiksow znanych wolajacych ([ThreadStatic] znacznik jak w FairXp): walka, strzal RBM, marsz, jedzenie, rada BK, budowy, ksiazki, Spoils, discard, kuznia, trening (gra/BK/171/musztra), budynki zalog, handel BK; **raz na dobe sredni tier i odsetek t3+ armii AI wedlug krolestwa; udzial dni postoju partii AI (ruch < 4 h w dobie)** | SkillLedgerLog | autotest 40 i 120 dob: raz na dobe linie "Umiejetnosci:", "XP wojska:", "Armie wedlug krolestw:", "Postoj:"; raz na tydzien rozklad poziomow lordow AI (mediana, p90); koszt < 0.1 s na dobe; 0 bledow. Pomiar "przed" dla Z7-Z14b | - |
| 7 | **Z6** | 24 ksiazki BK po 700-1 000 d | `HistoricalPrices.Apply`: przedmioty kategorii Book z wartoscia < 100 -> x100 (warunek < 100 = bez podwojenia przy kolejnym wczytaniu); `BookTranspiler` bez zmian | BookPriceFix | log "ksiazek BK przeliczonych 24"; u uczonego ksiazka BK 700-1 000 d, ksiazki ROT dalej 1 000-1 500 d; items-dump: zadnej ksiazki < 100 | - |
| 8 | **Z7** | RBM: XP za strzal tylko w bitwie, 1/3 | `Armoury/src/FairXp.cs`: prefiks `return false` na `RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix` (prywatna klasa, `AccessTools.TypeByName`; metoda robi TYLKO XP [K `rbmcombat.cs:3628-3677`]); nasza wersja: w bitwie kampanii 10 / 20 / 17 / 10, inaczej 0 | ShotXpFair | arena: 20 strzalow w sciane nie zmienia Bow; bitwa: ksiega Z4 "strzal RBM" ok. 1/3 wartosci sprzed zmiany; log "FairXp: strzal RBM wpiety"; nie ruszac drugiej klasy o tej samej nazwie w `RangedRework` (:6882) | Z4 |
| 9 | **Z8** | Steward z jedzenia: pierwiastek z ludzi, rodzaj od pol dnia zapasu, zima x1.25 od pory roku | prefiks na `DefaultSkillLevelingManager.OnFoodConsumed` (:457; NavalDLC przekazuje do niego [K `naval.cs:109092-109095`]); pora roku z `WinterBite`, ktorego nie ruszamy | StewardFoodXpFair | ksiega Z4: Steward lordow AI z "jedzenia" na dobe przed i po (350 ludzi ok. -47%); zima: skok x1.25, nie x1.5 | Z4; przed proba zimy (J) |
| 10 | **Z10** | Scouting z marszu: stale na godzine | prefiks na `DefaultSkillLevelingManager.OnTraverseTerrain` (15 / 25 na godzine, karawany polowa, bez progu 5) i `OnAIPartiesTravel` (0) [K :418-440] | ScoutXpFair | ksiega Z4: Scouting gracza i AI na dobe; partia 1-10 ludzi dostaje XP; AI 350+ ok. -50% | Z4 |
| 11 | **Z13** | Zwiadowca noca i w terenie | `TerrainEase.cs` (kary x (1 - min(0.5, Sc / 400)); Priority przed MarchPace), `SightRange.cs` (wzrok noca, `_depth`), +5 XP na godzine nocnego marszu w prefiksie z Z10 | NightScout | "Audyt predkosci" z rozpiska zwiadowcy: noc przy Scouting 200 daje -0.25 zamiast -0.5 | Z10 |
| 12 | **Z11** | Engineering w budowie za pieniadze | `BuildFunding.cs` `PowerPostfix` / `PowerIntPostfix` (:62-79): punkty x (1 + 0.0025 x Engineering gubernatora) + perki budowy; XP 10 za punkt; uwazac na `_depth` i Boost | EngineeringOnPaidWork | log "Budowy oplacone": punkty na pensa w osadzie z gubernatorem Eng 100 o 25% wyzsze; ksiega: Engineering gubernatorow | Z4 |
| 13 | **Z12** | H3: Medicine i Tactics | `LosersFlee.cs`: smiertelnosc przegranych x (1 - 0.25 x min(1, Med / 250)); p + 0.10 x clamp((Tw - Tp) / 300, -1, 1); granice 5-65% bez zmian | H3SkillsCount | linia "Przegrani (H3)" z rozbiciem wedlug chirurga i taktyka; widelki 15-40% (przegrani) i 1-5% (zwyciezcy) zostaja | Z4; decyzja F |
| 14 | **Z15** | Jedna ksiega handlu: zysk na sprzecie ze stanem, koszt przenoszony przy naprawie, bez dublowania z gra i BK | nowe zachowanie Armoury: prefiks `return false` na BK `BKTradeGoodsFixesBehavior.OnProfitMade` (:50); sprzedaz z `_transactionHistory.GetSoldItems` (zdarzenie `OnPlayerInventoryExchange`, gra `InventoryLogic.cs:583`); srednia cena zakupu wedlug (przedmiot, stan); przepisanie wpisu przy naprawie w `SmithMenu` (kowal) i samonaprawie (`ArmouryBehavior.cs:2379-2381`, `Forge.SelfRepair`) z robocizna i materialem po cenie targu; zapis przez `SaveText.Sync` (limit napisu 32 KB, najwyzej 300 wpisow, najstarsze odpadaja) | GearTradeXp | kupic zuzyty miecz, zapisac i wczytac, naprawic u kowala, zapisac i wczytac, sprzedac drozej: Trade rosnie o 0.5 x (cena - zakup - naprawa) + 2.5% ceny; sprzedaz ze strata: tylko 2.5% ceny; sztuka bez stanu: XP jak dzis z gry, nie podwojnie; sztuki oddane do zbrojowni: 0 | Z3, Z4 |
| 15 | **Z16** | Wymog Atletyki u bohaterow (zakaz albo nic - pytanie b) | najpierw `SkillSinew` dla bohaterow w CrashScribe (`Mends.cs:2400-2420` pomija dzis bohaterow, :2409): Atletyka lordow i towarzyszy do maksimum wymogu ich wlasnego sprzetu bojowego, nigdy w dol; potem postfiks `CharacterHelper.CanUseItem` dla zbroi przez `ItemReq.Meets` (na sztuke, 0.25 kg na punkt); +10 Atletyki dziennie na postoju w zbroi od 15 kg | HeroWeightLaw | autotest: lordow ze sztuka ponad wymog przed i po SkillSinew (dzis >= 268 z 811 [S], po 0); zaden lord nie traci zbroi; Ty z Atletyka 60: plyta 37 kg (wymog 148) sie nie zaklada | pytanie b |
| 16 | **Z17** | Drobne | paser 25 Roguery za udana sprzedaz zamiast 5% (`SpoilsSeal.cs:483`); RBL 0 XP za zaplate (`rbl.cs:1320, 2123-2132`); "na Mur" bez Roguery (razem z I1); samonaprawa: XP x udzial naprawionego stanu, min. 25% (`Forge.cs:511`, `SmithMenu.cs:1094, 1129`); gospodarz GT Steward i Charm (`TourneyBehavior.cs:600`) | po jednym na sprawe | po jednym tescie na sprawe | decyzja I |
| 17 | **Z14b** | Musztra AI: ta sama regula, srednio bez oslabienia armii AI | ten sam postfiks co Z14a: dla partii, ktorym gra daje baze, wynik += baza x (dowodca x dzien - 1), przed udzialem 171; stale "dzien" dobrane z pomiaru Z4 | DrillLaw (ten sam) | autotest 120 dob z 171: sredni tier i odsetek t3+ armii AI **kazdego krolestwa** w dobie 40 i 120 w granicach -5% wobec samej 171; swiat w granicach +-5%; odsetek konnych nie nizszy; 0 bledow | Z4, Z14a, P1 z audytu 14 (najpierw sklad wojsk), 171 + 174 w grze |
| 18 | **Z9** | Spoils: dokad ida dary i resztki | dar dla miasta trafia do zbrojowni zalogi tego miasta (DTE, jak 171), resztki trofeow na zlom na targ najblizszego miasta (jak paczka 158) | SpoilsGoodsHome | sztuki nie znikaja: ksiega obiegu 169 bez "znikniec" ze Spoils | Z3, 171 |
| 19 | **Z18** | Platnerz w taborze, kwatermistrz Armoury = Steward | `TroopSelfMend`, `MendMaterial`, `QuartermasterLaw`; XP tylko za godziny przydzielone kowalowi, z sufitem; metal napraw bez zmian (T3) | - | do zaprojektowania | po ekonomii |

**Luka miedzy Z1 a musztra (powiedziane wprost).** Po Z1 Twoje wojsko poza bitwa nie ma zadnego zrodla XP poza perkami, bo codzienny trening gra daje tylko AI
[K `DefaultPartyTrainingModel.cs:21-31`], a awanse u nas kosztuja x3 [D BannerKings.json]. AI dalej trenuje (z 171 ok. x0.65). Dlatego Z14a powinno wejsc
w tej samej paczce co Z1, na galezi z 171 (171 + 172 + 172b + 174 czekaja na wgranie). Jesli wolisz Z1 od razu, luka trwa do wgrania 171 z Z14a,
a Twoje wojsko uczy sie wtedy tylko w bitwach i z perkow.

**Szczegoly musztry (Z14a i Z14b).** Jeden postfiks, jedno miejsce: `ArmsDrill.TrainingPostfix` (171). XP na czlowieka na dobe = baza x dowodca x dzien x udzial171.
- **Baza:** jak w grze: 10 + 2 x tier; druzyna glowy rodu 15 + 3 x tier. Dla AI gra juz ja daje (dodajemy tylko roznice), dla Ciebie dodajemy cala.
- **Dowodca:** Leadership dowodcy partii / 170, w granicach 0.5-1.5. 170 to mniej wiecej srednia lordow z danych [D ROT_lords.xml + lords.xslt: 811 lordow,
  342 "knight" (Leadership 190), 298 "dandy" (130), 101 "chatelaine" (200), 70 "viking" (150) wg `sandbox_skill_sets.xml` i NavalDLC `naval_skill_sets.xml`;
  sredni czynnik [S] ok. 0.97]. Potem stala z ksiegi Z4 (prawdziwe Leadership dowodcow w grze).
- **Dzien:** 1.5 na postoju (ruch krocej niz 4 h w dobie: oboz, osada, oblezenie, lezy zimowe); 0.9 w dzien marszu; 0, gdy partia glodna
  (`Party.IsStarving`) albo ma dlug snu (`NightRest.Debt`, tylko Twoja druzyna). Glodne wojsko nie cwiczy: [H] Wegecjusz, "De re militari" ks. III rozdz. 3 -
  glod niszczy wojsko czesciej niz bitwa. Udzial dni postoju trzeba zmierzyc (Z4): linia "Miara:" liczy tylko partie z >= 12 h ruchu i pokazuje, ze w 1. dobie
  ponizej 12 h bylo 185 z 279 sledzonych, a w 120. tylko 77 z 693 [P kopiaT9-120 Armoury log :218 i :25919]. Stale 1.5/0.9 dobieram tak, zeby srednia
  z 120 dob wyszla 0.95-1.0 dla AI.
- **Bron:** jedynym czynnikiem sprzetu jest udzial uzbrojonych z 171 (0..1 wedlug szczebla broni oddzialu). Nie ma osobnego "0.5 bez broni".
  Dla Twojej druzyny - wedlug decyzji c(2).
- **Kogo nie dotyczy:** Inni (`Undead.Party`, jak `Gated` :192). Nocna Straz i Wolni Ludzie - ta sama regula, bo to ludzie i krolestwa z partiami lordow
  ([L] Jon Snow cwiczy rekrutow na dziedzincu Czarnego Zamku, "Gra o tron").
- **Perki BK i gry zostaja.** Dowodca dostaje jako Leadership 2.5% rozdanej musztry.
- **Skutek [S]** (t3, baza 16; dzis AI 16, z 171 x0.65 = 10.4):

| Kto | Postoj | Marsz | Uwagi |
|---|---|---|---|
| lord AI "knight" (Leadership 190, x1.12) | 26.9 (z 171: 17.5) | 16.1 (z 171: 10.5) | lepsi dowodcy szkola lepiej |
| lord AI "dandy" (Leadership 130, x0.76) | 18.2 (z 171: 11.9) | 11.0 (z 171: 7.1) | |
| srednio AI (89% dni w marszu, doba 120) | - | - | ok. 0.94 x dzis, stale dobrane do 0.95-1.0 |
| Twoja druzyna, Leadership 100 (x0.59) | 14.1 | 8.5 | dzis 0 (poza perkami) |
| Twoja druzyna na starcie, Leadership 20 (x0.5, dolna granica) | 12.0 | 7.2 | mlody dowodca szkoli slabiej niz lord |

- Awans t3 -> t4 (2 700 XP) z samej musztry: AI dzis 169 dni, z 171 ok. 260, z 171 i Z14b ok. 270-285; Ty (Leadership 100) 191 dni na postoju,
  318 w marszu. Rekrut t1 -> t2 (900 XP) u Ciebie na postoju: ok. 85 dni. Do tego walka.
- **Dlaczego srednio bez zmian dla AI.** Tier decyduje o wojnach AI: autobitwe AI z AI wygrywa wieksze "ludzie x tier" w 98 na 100, sredni tier armii AI to 2.3-2.5,
  a konni zaczynaja sie od t3-4, wiec konnych ubywa (12.5% w 1. dobie, 8.2% w 120.) [P 14-ARMIE-KROLESTW.md rozdz. 0 pkt 1 i 6, rozdz. 2.3]. Audyt 14 chce
  konnych wiecej (20-35% wedlug ksiazek). Nizszy trening AI to mniej t3+, czyli jeszcze mniej konnicy, wbrew decyzji C ("mniej wojska AI NIE").
  Dlatego Z14b jest tylko przesunieciem nauki (dowodcy, postoj), a celem testu jest sredni tier armii kazdego krolestwa, nie XP. Krolestwa z dlugimi marszami
  traca wzgledem innych ([L] Polnoc jest w "Grze o tron" opisana jako niemal tak rozlegla jak reszta Siedmiu Krolestw razem), wiec test liczy kazde krolestwo osobno.
- **[H]** Armie cwiczyly w lezach zimowych i w garnizonach, w polu malo. Milicje cwiczyly w swieta (1363).

---

## 4. Pytania do Jeffa (tylko zmiany rozgrywki; liczby dobieram sam)

**(a) Perki zarzadcy "Giving Hands" i "Paid in Promise".** Po Z1 nie dadza juz XP za wyrzucona bron i zbroje. Wolisz, zeby dostaly nowe dzialanie
("kwatermistrz pilnuje wydanego sprzetu: bron (Giving Hands) albo zbroja (Paid in Promise) Twojego wojska zuzywa sie w bitwie o 20% wolniej"),
czy maja zostac tylko ze swoja druga polowa (+10% cla dla gubernatora, -25% pensji towarzyszy)?
*Moja rada: nowe dzialanie. Nagradza dbanie o sprzet, ktory ludzie juz maja, a nie trzymanie zapasu w taborze (rynek zbroi i tak pustoszeje -
w tescie 171 zbroja korpusu do zera ok. doby 48 [P RAPORT-NOCNY-2026-10-09.md:56-58]). Bez tego Giving Hands przegrywa z perkiem obok i nikt go nie wybierze.
[H] Privy Wardrobe w Tower of London w XIV w. przechowywala, konserwowala i wydawala bron wojsk Edwarda III (T. F. Tout, "Chapters in the Administrative History", t. IV).*

**(b) Prawo Wagi dla Ciebie, towarzyszy i lordow.** Dzis zolnierz nie zalozy sztuki zbroi ciezszej, niz udzwignie jego Atletyka (0.25 kg na punkt),
a Ty i lordowie mozecie po starcie kampanii zalozyc wszystko. Kary za ciezar juz sa: gra spowalnia bieg (mniej przy wyzszej Atletyce), ROT w pelnej plycie
daje zamach x0.9 i predkosc w walce x0.66, a Twoj oddech odnawia sie szybciej z Atletyka. Wolisz **zakaz** jak u zolnierzy (sztuka ponad Atletyke sie nie zalozy;
lordom i towarzyszom podniose Atletyke do ich wlasnego sprzetu, zeby nikt nie stracil zbroi) czy **zostawic jak jest**?
*Moja rada: zakaz - to Twoja zasada z 29.08 ("umiejetnosci sa swiete... CALY ekwipunek"), a dokladanie trzeciej kary za ten sam ciezar byloby niepotrzebne.*

**(c) Musztra.** (1) Czy Twoja druzyna ma cwiczyc codziennie jak wojsko AI: na postoju szybciej (x1.5), w marszu wolniej (x0.9), glodna albo niewyspana wcale,
tempo wedlug Twojego Przywodztwa? (2) Czy te cwiczenia maja tez zalezec od broni dla Twoich ludzi (jak u AI po 171: kto nie ma broni swojego rodzaju i szczebla,
cwiczy wolniej), czy Ty zbroisz swoich ludzi sam i to Cie nie dotyczy? (3) Czy ta sama regula (postoj, marsz, dowodca) ma potem objac AI, przy sredniej
bez zmian? Dla porownania: AI juz dzis po 171 cwiczy wolniej przez brak broni (x0.36-0.40 na starcie kampanii, x0.63-0.66 po 40 dobach [P]), a Z14b nie doklada
spadku, tylko przesuwa nauke (decyzja C zostaje).
*Moja rada: (1) tak - razem z Z1, inaczej Twoje wojsko poza bitwa stanie; (2) tak, jedna regula; (3) tak, po pomiarze i po zmianie skladu wojsk z audytu 14.*

---

## 5. Czego nie wiem / ryzyka

- Petle Spoils (1.2) i Cancel (1.3) wynikaja z kodu. W grze ich nie widzialem, bo autotest nie otwiera ekranow gracza. Nie wiem tez, czy masz teraz
  Giving Hands, czy tylko Paid in Promise. Z1 zamyka oba.
- Z5b: jesli Spoils zmieni nazwy pol list ekranow, postfiks nie rozpozna listy i nic nie zrobi (zaloguje "nie znaleziono"); nie moze przeniesc cudzej listy,
  bo porownuje referencje, a nie typ ekranu.
- Z3 (migawka BK) dotyka prywatnego pola BK. Nowa wersja BK moze je zmienic; latka ma wtedy zalogowac "nie znaleziono", a nie sie wywrocic.
- Nie wiem, ile rodzajow jedzenia wioza lordowie AI (dotyczy Z8) ani jaki jest prawdziwy udzial dni postoju (dotyczy Z14). Z4 to mierzy.
- Liczby lordow ponad wymog Atletyki (Z16) sa z XML (szablony i pierwszy zestaw bojowy), nie z gry; w grze umiejetnosci lordow moga sie roznic.
- XP bohaterow i wojska nie ma w zadnym logu. Wszystkie "ile dziennie" w rozdz. 2 to szacunki [S]. Z4 to naprawia.
- Z14b zmienia rozklad tierow armii AI. Musi isc po P1 z audytu 14 i po wgraniu 171 + 174.
- Z7 lata prywatna klase RBM. Nowa wersja RBM moze zmienic nazwe; latka ma wtedy zalogowac "nie znaleziono".

## 6. Zrodla

- Raporty: `...\7016f733-...\scratchpad\umiejetnosci\zrodla-xp.md` (24 drogi "sprzet -> XP", tab. 1), `...\umiejetnosci\calosc.md`
  (tabela 24 umiejetnosci, dowody 4.1-4.13, pomiary rozdz. 6); dekompilacje RBM, Spoils, RBL, TXP, BEE, Naval i VM w `umiejetnosci\dec`.
- Gra 1.4.8 (`3cf3e0ac-...\scratchpad\ore-supply\cs`): `DefaultItemDiscardModel.cs`, `DiscardItemsCampaignBehavior.cs`, `InventoryLogic.cs`
  (:486, :494-501, :579-583, :655-673, :715-727, :1145-1152), `Helpers/InventoryScreenHelper.cs` (:157-170, :307-333), `TradeSkillCampaignBehavior.cs` (:84-170),
  `DefaultPerks.cs` (:2268-2270), `DefaultPartyTroopUpgradeModel.cs` (:34-60), `DefaultPartyTrainingModel.cs` (:21-31), `DefaultSkillLevelingManager.cs`
  (:132-139, :261-269, :418-464), `DefaultSkillEffects.cs` (:260-261), `PartyRole.cs`, `DefaultCharacterDevelopmentModel.cs` (:95-127, :223-235);
  SandBox.dll `SandboxAgentStatCalculateModel` (ilspycmd, `scratchpad\krytyka13\sandboxstat.cs` :1069-1086).
- BK: `BKPartyTrainningModel.cs` (:41-50), `VanillaModelTweakPatches.cs:1559`, `BKTradeGoodsFixesBehavior.cs` (:18-104), `Main.cs:66-75, 137`,
  `BKSettlementActions.cs:415-429`, `BKEducationBehavior.cs:436, 448`, `BKItems.cs:391-396`, `DefaultFaiths.cs:47, 122`, `RiteRegistry.cs:18-22`, `bk_faiths.xml`.
  BKROTPatch: `DefaultFaithsInitializePatch.cs`, `BKItemsInitializePatch.cs:87`, `ROTReligions.cs` (22), `BKROTPatch.Rites` (75), `BKROTPatch.cs:52, 114`.
- ROT: `ROTAgentStatCalculateModel.cs` (UpdateAgentStats), `KingsLandingEvent.cs:133`, `EnlistmentPatches.cs:432`.
- Spoils (`umiejetnosci\dec\spoils`): `SubClanBehavior.cs` (:1300-1363, :1651-1661, :1700-1745, :1766), `QuartermasterBehavior.cs` (:575-600, :800-880,
  :925-960, :1316-1356, :1405-1440), `DonateEquipmentBehavior.cs` (:115-150, :229-230), `LootCollectionBehavior.cs` (:1835-1881, :1909-1929, :1958, :2532-2618, :3243-3272).
- Nasz kod (`scratchpad\sklad4`): `ArmouryBehavior.cs` (:998, :2370-2385), `ArmsPricing.cs:130-152`, `HistoricalPrices.cs` (:15-27, :860-921), `FairXp.cs`,
  `QuartermasterLaw.cs` (:799-816, :905-935), `SpoilsSeal.cs` (:483, :560-640, :1180-1200), `SubModuleMain.cs:56`, `Forge.cs`, `SmithMenu.cs` (:347-351, :1093-1130,
  :2226-2255), `SmeltTab.cs:146`, `HideoutPurge.cs:150-153`, `StartKit.cs`, `ItemReq.cs`, `BattleWind.cs:21, 93-96`, `BuildFunding.cs`, `LosersFlee.cs`,
  `TerrainEase.cs`, `SightRange.cs`, `WinterBite.cs`, `NightRest.cs:37`, `WorldMeasure.cs`, `Calendar.cs`; CrashScribe `Mends.cs` (:2162-2170, :2400-2420).
  Paczka 171 (`scratchpad\noc\n172`): `Armoury/src/ArmsDrill.cs` (:16-31, :189-200, :222-235, :247-268), `Settings.cs` (:401-402, :805-806).
- Ustawienia Jeffa [D]: `Configs\ModSettings\Global`: BannerKings.json (TroopUpgradeXp 3.0, CharmXpMultiplier 0.5),
  RealisticLootSettings.json (StockpileMaxItems 500, EnableQuartermaster, EnableSubClan, EnableDonateEquipment, EnableQMDonateFood: true),
  RealmOfThrones/RealmOfThrones_v1.json (:49 PlayerPlateEncumbrance true; GlobalXPBonus 1.0), Armoury.json; `Configs\LauncherData.xml` (czynne moduly, AIInfluence wylaczony).
- Dane [D]: ROT-Content `ROT_lords.xml` + `lords.xslt`, SandBox `sandbox_skill_sets.xml`, NavalDLC `naval_skill_sets.xml`; skrypty `scratchpad\krytyka13b\lordarmor2-4.py`.
- Logi [P]: `kopiaT9-120\Armoury-2026-10-08_22-23-58.log` (:155 ColdStart, :185 HistoricalPrices kontrola, :218 i :25919 Miara, ArmsPricing, Budowy, Przegrani (H3)),
  `kopiaT9-120\session-2026-10-08_22-23-56.log` (:281, :1480), `kopiaT9-120\2026-10-08_22-23-58\zakupy.log`, `bitwy.log`;
  `kopia171\Armoury-2026-10-08_20-47-10.log` (:369, :7957), `kopiaS4\Armoury-2026-10-08_20-57-24.log` (:385, :8464), `kopia172b\Armoury-2026-10-09_00-22-12.log` (:377, :7932);
  `Documents\Mount and Blade II Bannerlord\CrashScribe\items-dump.csv` (09.10 01:14, te same wartosci co zrzut z 00:23).
- Historia i decyzje: `CHANGELOG.md:5921, 7314-7331`, `docs/STAN-PRAC.md:740, 768-771`, `docs/RAPORT-NOCNY-2026-10-09.md:56-58`,
  `docs/audyt-2026-10-09/14-ARMIE-KROLESTW.md` (rozdz. 0, 2.3, 4 P1), `docs/pancerze-waga-atletyka.md`, `docs/rozpoznanie-2026-10-08/paczka-171-wynik-3.md` (d).
- [H]: Assize of Arms (1181); Statute of Winchester (1285); proklamacja Edwarda III o cwiczeniu lucznictwa (1363); Wegecjusz, "De re militari" ks. I i ks. III rozdz. 3;
  "Le Livre des fais ... Bouciquaut" (ok. 1409); "Seneschaucy" (ok. 1270) i Walter z Henley (XIII w.); F. B. Pegolotti, "Pratica della mercatura" (ok. 1340);
  John of Arderne (XIV w.); Master James of St George (Walia 1277-1300+); Crecy (1346), Bannockburn (1314); T. F. Tout, "Chapters in the Administrative History
  of Mediaeval England", t. IV (Privy Wardrobe w Tower).
- [L]: G.R.R. Martin, "Gra o tron" (Syrio Forel, Vayon Poole, Jon Snow na dziedzincu Czarnego Zamku, rozleglosc Polnocy), "Uczta dla wron" (Cytadela, ogniwa lancucha),
  "Rycerz Siedmiu Krolestw" (Dunk i Egg).

---

## 7. Krytyka i odpowiedzi

Kazda uwage sprawdzilem sam w kodzie, danych i logach (dowody w rozdz. 1-3 i 6). Werdykt: PRZYJETA (wprowadzona), CZESCIOWO (diagnoza prawdziwa,
poprawka inna), ODRZUCONA (z jednym zdaniem dlaczego).

| Nr | Waga | Uwaga (krotko) | Werdykt | Co zrobilem |
|---|---|---|---|---|
| 1 | wazne | Wartosci z linii "ArmsPricing ... Przyklady" sa sprzed `HistoricalPrices.Apply`; w grze pancerz t6 2 316 d, miecz t5 104 d | PRZYJETA | Sprawdzone: `ArmouryBehavior.cs:998` wola `ArmsPricing.Build()` przed `HistoricalPrices.Apply()`; items-dump: 2 316 / 104 / 28 d; log :185 `dragonbone_bow 100000->61`; ColdStart 373 029 d / 10 864 szt. = 34 d. Tabela 1.4 przeliczona (116 zamiast 2 560 XP), pkt 6 dla Jeffa, akapit 2.3 "Nowa moneta" odwrocony; dodany "tani sklep z XP" (1.1) i resztki z dzieleniem calkowitym (1.6). Dodatkowo znalazlem, ze konie NIE sa przeliczane (mediana 3 973 d, rumak 20 000 d), wiec Z2 jest drobne dla broni, ale nie dla koni |
| 2 | wazne | Pominieta sciezka BK `BKTradeGoodsFixesBehavior`: migawka tylko po kliknieciu targu, zuzyty sprzet uczy handlu, sprzet oddany po targu liczy sie jako sprzedany | PRZYJETA | Sprawdzone w kodzie BK i gry (:18-104; zdarzenie przy kazdym zamknieciu handlu, :134-152; zamek `IsCastle`). Nowa sciezka 25 (1.5); poprawione pkt 4 i wiersz Trade; Z3 dostaje odswiezanie migawki przy kazdym otwarciu handlu (wariant 1 - jedna latka, niezalezna od wnetrza BK); Z15 zastepuje `OnProfitMade` jedna ksiega, ktora liczy tylko sztuki sprzedane w sesji, zostawia czlon 2.5% obrotu i nie dubluje zysku z gra. Test z uwagi dopisany do Z3 |
| 3 | wazne | Cancel na 7 ekranach "take back" oddaje wszystko | PRZYJETA | Sprawdzone wszystkie 7 miejsc i mechanizm Reset/DoneLogic. Nowy rozdz. 1.3 i Z5b. Zamiast 7 prefiksow - jeden postfiks na `InventoryLogic.Reset(fromCancel)`, ktory rozpoznaje liste Spoils po referencji (`_rosters[0]` to ta sama lista [K :494-501]) i oddaje ja do taboru; funkcje Spoils dostaja pusta liste i nic nie robia (sprawdzone, licza tylko `Amount > 0`). Mniej miejsc do rozjechania. Test z uwagi dopisany |
| 4 | drobne | Pkt 4 "dwie latki", pkt 8 "trzy latki" | PRZYJETA | Ujednolicone: trzy latki pod DonationXpOff (Z1 Armoury, Z2 i Z3 SpoilsSeal) + osobna latka Cancel (SpoilsCancelKeeps), opisane slowami gracza w pkt 5 |
| 5 | drobne | Napisy Spoils dalej obiecuja trening | PRZYJETA | Z3: postfiksy na `UpdateCompleteText` (:1835, napis :1881) i `OnCompleteLeaveCondition` (:1909, :1929), ten sam wylacznik; test dopisany. Postfiks zamiast pliku napisow, bo kolejnosc nadpisywania napisow miedzy modulami nie jest pewna |
| 6 | drobne | [K] wskazywal wiersze raportu zamiast plik:linia | PRZYJETA | Wszystkie linie sprawdzone i wpisane wprost w 1.2 i 1.7 |
| 7 | drobne | Paser (5% zaplaty) oceniony jako czysty, choc to XP od ceny | PRZYJETA | 1.7 i Z17: stale 25 Roguery za udana sprzedaz; zasada "XP od ceny tylko w Handlu" zapisana wprost |
| 8 | drobne | Lista przeszukanych modow niepelna | PRZYJETA | Przeszukalem 263 DLL w `Modules/*/bin/Win64_Shipping_Client`: zero trafien (sprawdzian: te same napisy sa w `TaleWorlds.CampaignSystem.dll`); AIInfluence wylaczony w LauncherData.xml. Zdanie w Z1 zastapione |
| 9 | krytyczne | Z14 nie widzi 171 (ArmsDrill mnozy caly wynik przez udzial uzbrojonych), bron liczona dwa razy, "zdjecie bazy" daje bzdure, sprzecznosc "bez broni wcale" / "0.5" | PRZYJETA | Sprawdzone (n172 `ArmsDrill.cs:189-200, 222-235, 247-268`; `Settings.cs:806`; logi 171 x0.36-0.66, utrata ok. 650-745 tys. XP dziennie). Z14 to teraz rozszerzenie ArmsDrill w tym samym postfiksie; jedynym czynnikiem broni jest udzial z 171; bez odejmowania bazy (dodajemy roznice albo cala baze tam, gdzie gra jej nie daje); pkt 7/9 i pytanie (c) poprawione; kolizja z ArmsDrill wpisana w tabele |
| 10 | wazne | Kalibracja na Leadership 50 i "70% dni w marszu"; cel testu sprzeczny z wyliczonym spadkiem; brak odniesienia do decyzji C | PRZYJETA | Sprawdzone: szablony lordow 130-200 (811 lordow); Miara doba 120: 77 z 693 ponizej 12 h. Przyklady w tabeli Z14 z danych (knight 190, dandy 130); czynnik dowodcy znormalizowany do 170. Cel zmieniony na wariant z uwagi "dobrac stale tak, zeby srednie XP AI z 171 zostalo w granicach": srednia 0.95-1.0, wiec Z14b nie oslabia AI (C). Nowy licznik dni postoju w Z4. Widelki z 171 w pytaniu (c) |
| 11 | wazne | Zalogi gracza i jego druzyna nie sa objete 171; zdanie "obie strony te same reguly" falszywe | PRZYJETA | Sprawdzone (`Gated` :189-200, `GarrisonBuysGearPlayer = false`). Nowy akapit w 2.2 z odpowiedzia na pytanie K, pkt 8 dla Jeffa, poprawione 2.3 "Gracz a AI"; decyzja o czynniku broni dla gracza w pytaniu c(2) |
| 12 | wazne | Z14 obniza tiery AI, a tier decyduje o wojnach i konnicy; trening AI to zasada gry, nie BK | PRZYJETA | Sprawdzone (`DefaultPartyTrainingModel.cs:21-31`; 14-ARMIE: 98 na 100, tier 2.3-2.5, konni 12.5% -> 8.2%). Z14 podzielone: Z14a (gracz) teraz, Z14b (AI) po P1 z audytu 14; cel testu = sredni tier i t3+ kazdego krolestwa (Z4 je raportuje); postoj (lezy zimowe, zamek, oblezenie) x1.5. Wiersz 2.2 przypisuje trening grze |
| 13 | wazne | "Atletyka daje tylko oddech" - falsz; gra i ROT juz karza ciezar, kara z Z16 bylaby trzecia | PRZYJETA | Sprawdzone (`DefaultSkillEffects.cs:260-261`, wzor predkosci w SandBox.dll, ROT x0.9 / x0.66, `PlayerPlateEncumbrance` true). Wiersz Athletics i pytanie (b) opisuja kary, ktore dzialaja; rekomendacja zmieniona z "kary" na "zakaz" przez `ItemReq` (zasada Jeffa z 29.08), bez nowej kary |
| 14 | wazne | Wzor Z16 niejednoznaczny (sztuka czy komplet); lordowie AI straciliby sprzet | PRZYJETA | Regula na sztuke, jak `ItemReq`. Policzylem wlasnym skryptem: przy samej wadze 132 lordow ma korpus ponad udzwig, a z wymogami z XML co najmniej 268 z 811 ma jakas sztuke ponad wymog. Z16 zaczyna sie od `SkillSinew` dla bohaterow (raise never lower, dyrektywa z 15.09); test "lordowie ponad wymog przed/po" |
| 15 | wazne | Z15: naprawa zmienia stan, wiec klucz (przedmiot, stan) z zakupu nie zadziala | PRZYJETA | Sprawdzone (`SmithMenu.cs:349`, `ArmouryBehavior.cs:2379-2381`, `RecordSales`). Z15 przenosi podstawe kosztu przy naprawie (kowal i samonaprawa) z robocizna i materialem; test z zapisem i wczytaniem miedzy krokami |
| 16 | wazne | Po Z1 postep Twojego wojska stanie do czasu Z14 | PRZYJETA | Akapit "Luka miedzy Z1 a musztra" w rozdz. 3 i pkt 7 dla Jeffa; Z14a (musztra Twojej druzyny) na 5. miejscu kolejki, w paczce z Z1 na galezi 171 |
| 17 | drobne | Z6: zamiast x100 na Value zmienic w BookTranspiler 1000 na 100 | CZESCIOWO | Diagnoza prawdziwa (BKROTPatch `BKItemsInitializePatch.cs:87` dzieli przez 100, nasz transpiler zamienia x1000 na x1). Poprawke odrzucam: 36 ksiazek ROT ma wartosc 1 000-1 500 d [P items-dump], wiec przy 1000 -> 100 kosztowalyby 100-150 tys. d. Z6 zmienione: w `HistoricalPrices.Apply` x100 tylko dla ksiazek z wartoscia < 100 (24 sztuki), co daje jedna cene u uczonego, na targu i w lupie |
| 18 | drobne | Z8 zeruje zime, a 2.4 proponowalo zime jako probe Stewarda | PRZYJETA | Z8: zima x1.25 jako premia za trudnosc, liczona od pory roku, nie od zjedzonej ilosci; punkt z 2.4 wlaczony w Z8 |
| 19 | drobne | "Glodne - spojne z decyzja A" to bledny cytat (A dotyczy ludnosci) | PRZYJETA | Odwolanie do A usuniete; uzasadnienie wlasne: [H] Wegecjusz ks. III rozdz. 3 i istniejacy stan `Party.IsStarving` |
| 20 | drobne | Z14 bez wylaczen 171 (Inni); brak decyzji o Nocnej Strazy i Wolnych Ludziach | PRZYJETA | Inni wylaczeni jak w `Gated` (`Undead.Party`); Nocna Straz i Wolni Ludzie - ta sama regula ([L] dziedziniec Czarnego Zamku) |
| 21 | drobne | Ofiara z broni BK to obrzed Polnocy (Starzy Bogowie), nie septu | CZESCIOWO | Sprawdzilem glebiej: w naszej grze tego obrzedu w ogole nie ma. BKROTPatch wylacza `DefaultFaiths.Initialize` (ktore wczytuje `bk_faiths.xml` z ofiarami z broni), a 75 obrzedow ROT to ofiary z towarow i zwierzat (22 religie, zgodne z logiem). Wiersz z 1.6 oznaczony jako martwy, punkt z Z17 usuniety, wiersz Theology poprawiony; kierowanie przedmiotow wedlug wiary jest wiec zbedne |
| 22 | drobne | (1) Okup lorda to 12 Roguery - koszt bez skutku; (2) Z18 przeczy zasadzie "tylko za prace przy kowadle" i rusza T3 | PRZYJETA | Sprawdzone (`DefaultSkillLevelingManager.cs:261-269`). (1) okup usuniety z Z17; (2) Z18: XP tylko za godziny przydzielone kowalowi, z sufitem; metal napraw bez zmian bez ponownej kalibracji T3 |
| 23 | drobne | Nowe dzialanie perkow (+25% musztry przy zapasie) zacheca do chomikowania tego, czego brakuje | PRZYJETA | Sprawdzone (RAPORT-NOCNY :56-58: zbroja korpusu do zera ok. doby 48). Pytanie (a) zmienione: perki zmniejszaja zuzycie wydanego sprzetu o 20% (dbanie o sprzet ludzi, nie zapas); premia za zapas usunieta z Z14 |

Odrzuconych w calosci: 0. Dwie uwagi (17 i 21) przyjete czesciowo: diagnoza prawdziwa, ale poprawka z uwagi bylaby bledna (17) albo zbedna (21), wiec wprowadzilem inna.
