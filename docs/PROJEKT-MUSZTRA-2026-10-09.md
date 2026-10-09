# MUSZTRA - codzienne cwiczenia wojska i zapas do cwiczen (Z14a, Z14b, decyzja 2) - projekt, 09.10.2026 (wersja 2 po krytyce)

Status: projekt poprawiony po krytyce (rozdz. 12) i WYKONANY w drzewie `SCR\noc2\musztra` (galaz `w-toku/musztra`, commit lokalny 0015a84, CHANGELOG "MUSZTRA",
status NIEWGRANE; Release zbudowany, kod wyjscia 0). Gry nie uruchamialem, niczego nie wgrywalem, autotestu nie bylo.
Poprawki po recenzji kodu (rozdz. 13, CHANGELOG "MUSZTRA-p", commit lokalny f24055d, DLL `SCR\test\Armoury-musztra.dll` md5 9751b8ccf3121491dfbf9c184cc21247): kara glodu i snu dla wszystkich lordow AI (nowy `DrillPenaltyAi`, domyslnie wlaczony - Jeff 09.10:
"takie same kary jak gracz"), zapas AI tylko z nadwyzki zbrojowni (tabor - lup i zaopatrzenie BK - sie nie liczy), zapas gracza tylko przy `DrillLaw` i Z1,
zasilenie autotestu do pelnego co dobe, pomiar Z14b bez zapasu i bez dni kary, koszt liczony w calosci.
Baza kodu: `noc/sklad6` 0c41eee (Armoury w grze 17a700d7 + 171/172/172b/174). K1 (`SCR\noc2\k1`) tylko czytany.
SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`.
Skroty sciezek: `M\` = `SCR\noc2\musztra\Armoury\src\`, `G\` = dekompilacja gry `...\3cf3e0ac-...\scratchpad\ore-supply\cs\`, `BK\` = `...\ore-supply\bk\`,
`SP\` = Spoils of War 1.8.4 `SCR\umiejetnosci\dec\spoils\`.
Podstawa: `docs/audyt-2026-10-09/13-UMIEJETNOSCI.md` (Z1-Z3, Z14a, Z14b, rozdz. 2.2, "Szczegoly musztry", pytania a/c), `docs/STAN-PRAC.md` decyzje 09.10
(03:45 pkt 1 i 2, 04:00 pkt 1 i 2), specyfikacja K1, logi testow `SCR\kopia-sklad6`, `SCR\kopiaT9-120`.
Oznaczenia: [K] kod (plik:linia), [P] pomiar z logu, [S] szacunek/dobor z rachunku albo wielkosc NIEZNANA, [J] decyzja Jeffa.

**Decyzje Jeffa, ktore ten projekt wykonuje:**
- [J 03:45, 1a] Twoja druzyna (i partie Twojego rodu, lordowie w Twojej armii) cwiczy codziennie: postoj x1.5, marsz x0.9, glodna albo niewyspana - wcale;
  tempo od Przywodztwa dowodcy: Przywodztwo / 170, granice 0.5-1.5. [1b] cwiczenia wedlug broni ludzi (udzial uzbrojonych jak u AI po 171). [1c] potem ta sama regula dla AI.
- [J 04:00, 2] tempo z tabeli zatwierdzone: BK TroopUpgradeXp 3.0 (900/1650/2700/3900/5100), baza 10 + 2 x tier, t1->t6 na postoju ok. 1 rok przy 255+, ok. 2.6 roku przy 100.
- [J 03:45, 2] zamiast XP z perkow Giving Hands / Paid in Promise (zamknietych przez Z1): ZAPAS DO CWICZEN - oddana bron i zbroja daja mnoznik musztry,
  zapas sie zuzywa (zuzyty sprzet -> zlom dla kowali albo przepada z ksiega), perki wzmacniaja bonus, jedna regula dla gracza i AI.
- [J 04:00, 1, powtorzone 09.10] samotne kolumny AI "maja miec takie same kary jak gracz" - dotyczy tez musztry: glodni i niewyspani nie cwicza (cale XP, takze
  perki, bez zuzycia zapasu) - u WSZYSTKICH partii lordow AI, niezaleznie od Z14b (`DrillPenaltyAi`, domyslnie wlaczony). Glod od razu; dlug snu: gracz i jego armia
  od razu, reszta AI od scalenia z T10 (hak `Drill.SleepDebtOf`, rozdz. 8).

---

## 0. Dla Jeffa

1. Twoja druzyna, partie Twojego rodu i lordowie w Twojej armii cwicza teraz codziennie: na postoju szybciej, w marszu wolniej, a glodni albo niewyspani wcale (wtedy nie ucza tez perki).
2. Tempo zalezy od Przywodztwa dowodcy, jak w tabeli, ktora zatwierdziles: rekrut do t6 na samym postoju ok. 1 roku przy 255+, ok. 1.5 roku przy 170, ok. 2.6 roku przy 100 i ok. 3 lat przy 50, a do tego dochodzi doswiadczenie z bitew.
3. Cwiczy sie bronia, wiec kto nie ma w zbrojowni broni swojego rodzaju i stopnia, uczy sie wolniej, dokladnie tak jak ludzie lordow AI - i to dotyczy rowniez XP z perkow treningu (Combat Tips, Drill Sergeant itd.), ktore dzis dostajesz w calosci.
4. Bron, tarcze i zbroje, ktore wyrzucasz na zwyklym ekranie ekwipunku, zostawiasz na ekranie lupu gry albo zostawiasz po zbieraniu trofeow Spoils, nie przepadaja juz, tylko trafiaja do zapasu cwiczebnego Twoich ludzi, dopoki jest miejsce (dwa komplety: sztuka broni i sztuka zbroi na trzech ludzi). Komunikat mowi, z iloma sztukami ludzie cwicza (razem z ich zapasowa bronia w zbrojowni) i ile jeszcze od Ciebie wezma. W dzien bez cwiczen (glod albo noc bez snu) dostaniesz jedno zdanie, ze ludzie nie cwiczyli.
5. Do zapasu liczy sie tez to, co Twoi ludzie maja w zbrojowni ponad swoje komplety (bez Twoich wkladow), i ta nadwyzka tez sie na cwiczeniach zuzywa.
6. Pelny zapas, czyli jedna sztuka broni i jedna sztuka zbroi na trzech ludzi, daje +20% musztry; z perkami kwatermistrza Giving Hands (bron) i Paid in Promise (zbroja) do +30%, czyli dolna i gorna granica Twojego "np. do +25-30%".
7. Zapas sie zuzywa (przy 100 ludziach na postoju mniej wiecej sztuka na dwa dni), z zuzytej sztuki zostaje zlom, a kowale najblizszego miasta, do ktorego wejdziecie, odkupuja go jako rude i placa Twoim ludziom do ich sakiewki - nic sie nie bierze z niczego i nic nie wraca do Twoich sakw.
8. Zapasu nie da sie wyjac ani sprzedac - nalezy do ludzi i zostaje z druzyna jak Twoja zbrojownia; nie jest wiec darmowym magazynem.
9. Lordowie AI cwicza z tym samym zapasem od razu - u nich to tylko zapasowa bron ich ludzi w zbrojowni (tak jak u Ciebie nadwyzka ludzi); ich tabor (lup do sprzedania i zaopatrzenie Banner Kings) sie nie liczy, tak jak Twoje sakwy. Kare maja taka sama jak Ty: glodni albo niewyspani nie cwicza wcale (niewyspanie AI zacznie sie liczyc po scaleniu z nocnym marszem T10). *[grupa11: liczy sie - niewyspany to ten sam dlug snu, ktory zabiera predkosc i morale; lordowie w Twojej armii licza wlasny sen (ida z Toba noca, wiec traca te same noce), a gdy spisz w obozie, spia razem z Toba, takze przez swit (grupa11-p).]* Zasade dowodcy i postoju/marszu wlacze im dopiero, gdy autotest pokaze, ze ich armie srednio nie slabna - do tego czasu log tylko mierzy, co by sie zmienilo.
10. Mam do Ciebie jedno pytanie (rozdz. 11): czy Twoja druzyna ma dostac baze glowy rodu, tak jak druzyny glow rodow AI.

---

## 1. Regula (jedna dla gracza i AI)

Doswiadczenie na czlowieka na dobe, dla kazdego oddzialu (elementu rosteru) partii lorda, poza bitwa:

**XP = (B x L x D x S + P) x A**, a przy glodzie albo dlugu snu **XP = 0** (cale - takze perki).

| Czlon | Co to jest | Skad |
|---|---|---|
| B | baza gry: 10 + 2 x tier; druzyna glowy rodu AI 15 + 3 x tier (u gracza 10 + 2 x tier - pytanie 11); w bitwie 0 (jak w grze) | [K] `G\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyTrainingModel.cs:21-31`, BK `BK\BannerKings.Models.Vanilla\BKPartyTrainningModel.cs:41-50` |
| L | dowodca: Przywodztwo przywodcy partii / 170, w granicach 0.5-1.5; partia bez przywodcy 0.5 | [J 1a] |
| D | dzien: 1.5 postoj, 0.9 marsz | [J 1a]; definicje nizej |
| S | zapas do cwiczen: 1 + 0.10 x uB x kB + 0.10 x uZ x kZ (do 1.20; z obu perkami do 1.30) | [J 2]; rozdz. 3 |
| P | perki gry i BK w treningu (Combat Tips, Raise the Meek, Drills, Bow Trainer ...) | [K] `DefaultPartyTrainingModel.cs:32-80`, `BKPartyTrainningModel.cs:52-117` |
| A | udzial uzbrojonych oddzialu z 171 (0..1: kto ma w zbrojowni bron swojego typu o tierze >= t-1) | [K] `M\ArmsDrill.cs:113-182` |

- **Godzina postoju** (`Drill.RestHour`): partia w osadzie, w obozie obleznikow albo przesunieta najwyzej o 0.35 jedn. od poprzedniej godziny - ten sam
  prog i ta sama granica co ksiega snu (`M\NightRest.cs` OnHourly: "ruszyl sie" = krok > 0.35) i T10 R2. To **czesc wspolna** z ksiega snu, nie cala jej
  definicja: sen dolicza po swojej stronie morze (`SleepAtSeaFree` - zaloga spi na wachty), sluzbe ROT (o snie decyduje lord) i oboz swiata T10. Dla musztry
  to nie postoj - partia na morzu plynie, w sluzbie ROT idzie z lordem (ruch jest w kroku); oboz obleznikow stoi, wiec miesci sie i tak. Przy scaleniu T10
  `NightRest.OnHourly` i R2 wolaja `Drill.RestHour` dla czesci wspolnej (rozdz. 8). **Dzien postoju** = mniej niz 4
  godziny ruchu z ostatnich 24 (maska 24 bitow na partie). Godzina niezaobserwowana (nowa partia, AI po wczytaniu) liczy sie jako ruch - wczytanie nigdy
  nie daje darmowego postoju. Maska gracza idzie do zapisu.
- **Zero (XP = 0, takze perki; zapas sie nie zuzywa)**: `Party.IsStarving` (gra: brak jedzenia) albo dlug snu >= 1: gracz `NightRest.Debt` (`M\NightRest.cs:37`);
  partie doczepione do armii gracza (`AttachedTo == MainParty`) - dlug gracza od razu (ida z nim noca); pozostale AI - hak `Drill.SleepDebtOf` (T10, rozdz. 8;
  do czasu T10 = 0). Kara dotyczy wszystkich partii lordow: Z14a zawsze, AI poza Z14a przy `DrillPenaltyAi` (domyslnie wlaczony) albo `DrillLawAi`.
  **[grupa11 - ZMIENIONE, nie cofac:** hak `Drill.SleepDebtOf` usuniety, zastapiony jedna funkcja `NightRest.DebtOf(mp)`: gracz - `NightRest.Debt`, kazda inna
  partia lorda - ksiega snu AI T10 (ten sam dlug, ktory zabiera predkosc i morale), TAKZE doczepieni do armii gracza (licza wlasny sen - krytyka 5 zmieniona,
  powod: jedna prawda z kara morale). grupa11-p: doczepieni spia z graczem w menu ("Bed down"), takze przez swit (`NightMarch.SleepsWithLeader`). Bez ksiegi
  dlugu AI (`AiCampsAtNight` / `AiSleepDebt` wylaczone, rowne godziny obozu, DLL na sucho) zaden lord - takze Z14a - nie ma kary snu w musztrze, gracz ma.**]**
  Gracz w dzien kary dostaje w grze jedno zdanie ("Your men were too hungry / too tired to drill today ...").
- **Bitwa** (`MapEvent != null` w chwili treningu): B = 0 jak w grze, P zostaje (gra tez daje wtedy perki).
- **Kogo dotyczy:** partie lordow (`IsLordParty`), oddzialy bez bohaterow, z kultura (jak BK `:39`, BEE `PartyTrainingPatch`), bez Innych (`Undead.Party`).
  Zalogi - bez zmian (budynki + 171). Bandyci, karawany, wiesniacy - bez zmian (gra nie daje im bazy).
- **Z14a** (`DrillLaw`, domyslnie wlaczone): partie, ktorym gra NIE daje bazy - ten sam predykat co gra (`DefaultPartyTrainingModel.cs:21`: `Army.LeaderParty == MainParty`
  albo `Party.Owner.Clan == PlayerClan`): Twoja druzyna, partie Twojego rodu, lordowie w Twojej armii. Dostaja cala regule.
- **AI poza Z14a**: `XP = (B x [L x D, gdy DrillLawAi] x [S, gdy DrillStockAi] + P) x A(171)`; zero przy glodzie/snie przy `DrillPenaltyAi` (domyslnie
  WLACZONY) albo `DrillLawAi`. Kara to nie wspolczynnik czekajacy na pomiar, tylko decyzja Jeffa ("takie same kary jak gracz") - dlatego osobny wylacznik.
  `DrillStockAi` (domyslnie wlaczony): S >= 1, wiec nie moze oslabic AI (decyzja c). `DrillLawAi` (Z14b, domyslnie WYLACZONY): czeka na pomiar L x D (rozdz. 10).
- **Udzial uzbrojonych A**: Twoja druzyna - przy `DrillNeedsArmsPlayer` (zbrojownia DTE gracza); partie AI w Z14a - ten sam warunek co `ArmsDrill.Gated`
  (`AiGear.On && PartyDrillNeedsArms` - zbrojownie partii Twojego rodu uzupelnia tylko AiGear); AI poza Z14a - jak dzis (`Gated`).
- **Dowodca nie dostaje dodatkowego Przywodztwa z musztry** (audyt proponowal 2.5%): gra juz daje mu 2.5% kosztu kazdego awansu
  [K `G\...\DefaultSkillLevelingManager.cs:356-370`], a musztra konczy sie awansem - byloby liczone dwa razy.

## 2. Liczby i uzasadnienie

**Tempo t1 -> t6 z samej musztry** (14 250 XP; A = 1, bez bitew i perkow; doby kalendarza 364-dniowego) [S, rachunek z kosztow BK i B = 10 + 2 x tier:
900/18 + 1650/21 + 2700/24 + 3900/27 + 5100/30 = 556 dob przy L = 1 i postoju]:

| Przywodztwo | L | postoj | postoj, pelny zapas (x1.2) | postoj, zapas + oba perki (x1.3) | marsz |
|---|---|---|---|---|---|
| 50 | 0.50 (granica) | 1 111 (3.1 roku) | 926 | 855 | 1 852 |
| 100 | 0.59 | 944 (2.6 roku) | 787 | 726 | 1 574 |
| 170 | 1.00 | 556 (1.5 roku) | 463 | 427 | 926 |
| 255+ | 1.50 (granica) | 370 (1 rok) | 309 | 285 | 617 |

To jest tabela zatwierdzona przez Jeffa [J 04:00, 2]; zapas skraca ja o 1/6 do 1/4.
Przyklad dzienny (sprawdzalny recznie): 112 ludzi t1-t3, suma baz B = 1 568 (srednio 14), Przywodztwo 100 (L = 0.588), postoj (D = 1.5),
zapas: bron 30 na 38 potrzebnych (uB = 0.789, bez Giving Hands), zbroje 40 na 38 (uZ = 1, z Paid in Promise kZ = 1.5) -> S = 1 + 0.079 + 0.150 = 1.229;
1 568 x 0.588 x 1.5 x 1.229 = 1 700; + perki P = 210 -> 1 910; x udzial uzbrojonych A = 0.97 -> **1 852 XP** (16.5 na glowe). W marszu (D = 0.9): 1 020 + 210 = 1 230 x 0.97 = 1 193.

| Liczba | Wartosc | Dlaczego |
|---|---|---|
| postoj / marsz / glod, sen | 1.5 / 0.9 / 0 (cale XP) | [J 1a] "glodna albo niewyspana - wcale" |
| godzina postoju | osada, oboz obleznikow albo ruch <= 0.35 jedn./h | czesc wspolna z ksiega snu gracza i T10 R2 - ten sam prog i granica (CLAUDE.md 8.0: jedna zasada na jedno zjawisko); morze i sluzba ROT to zasady snu, nie postoju |
| dzien postoju | ruch < 4 h w 24 h | definicja z audytu 13 (Z14): krotki przejazd (z obozu do miasta) nie psuje dnia cwiczen, dzien w kolumnie - tak |
| dowodca | Przywodztwo / 170, 0.5-1.5 | [J 1a]; 170 ~ srednia z szablonow lordow ROT (audyt 13: 811 lordow, sredni czynnik ok. 0.97 [S z XML]) |
| pelny zapas | 1 sztuka broni (z tarczami) + 1 sztuka zbroi na 3 ludzi (zaokraglone w gore) | [S] cwiczy sie zmianami: co trzeci czlowiek walczy na sparingu sprzetem z zapasu; 100 ludzi = 34 + 34 sztuki |
| bonus zapasu | +10% za bron, +10% za zbroje (razem +20%) | Jeff: "np. do +25-30%"; baza 20, zeby perki cos znaczyly (z perkami 30) |
| perki | Giving Hands (kwatermistrz): czesc broni x1.5; Paid in Promise (kwatermistrz, druga rola): czesc zbroi x1.5 | te same role, ktore gra sprawdza przy oddawaniu [K `G\...\DefaultItemDiscardModel.cs:12-24`] |
| limit przyjecia (gracz) | 2 x pelny na grupe (wedlug dzisiejszej liczby ludzi) | jeden komplet w uzyciu, drugi na wymiane zuzytych; bez ludzi (0) nic nie wchodzi |
| zuzycie | sztuka w uzyciu sluzy 200 "pelnych" dni cwiczen (x D: ok. 130 dni postoju, 220 dni marszu) | [S] pelny zapas 100 ludzi (68 szt.) traci ok. 0.5 szt./dobe na postoju i 0.3 w marszu |
| zlom | ruda = ruda, z ktorej sztuke wykuto x 0.5 | ta sama regula i to samo ustawienie co zlom 174 (`OldStockScrapYield`, wzor `M\ArmsScrap.cs:121-122`) |

**Z14b (DrillLawAi) czeka na pomiar** [S - wielkosc NIEZNANA]. Prawdziwego udzialu dni postoju AI nikt nie zmierzyl: linia "Miara: marsz" (WorldMeasure)
liczy tylko partie, ktore ruszaly sie 1-11 h (`else if (t.MoveH > 0) few++`, `M\WorldMeasure.cs:158`), a partie stojace caly dzien (osada, oblezenie,
doczepione do armii - `M\WorldMeasure.cs:80-81`) nie trafiaja do zadnej grupy, wiec "sledzonych 648, ponizej 12 h: 88" nic nie mowi o postoju. Srednie D
moze byc 0.9 (wszyscy w marszu) albo blisko 1.5 (duzo stania) - Z14b moze AI oslabic albo wzmocnic. Prawdziwego Przywodztwa przywodcow AI tez nikt nie
zmierzyl (0.97 to szablony XML). Dlatego pomiar (rozdz. 10) podaje rozklad godzin ruchu (0 / 1-3 / 4-11 / 12+) wedlug krolestw i "razem" dla progow
postoju 4 / 8 / 12 h - liczby do jednej propozycji po tescie, regula sie nie zmienia.

## 3. Zapas do cwiczen

**Co sie liczy:** bron - jednoreczna, dwureczna, drzewcowa, luk, kusza, rzucana, tarcza; zbroja - korpus, helm, nogi, rece. Bez: plaszczy, koni, rzedow,
amunicji, sztandarow, unikatow (`ArmsPricing.IsUnique`), sztuk zakazanych w bitwie (`QuartermasterLaw.BarredInBattle`) i wyrobow gracza (`IsCraftedByPlayer`).
Stan sztuki nie gra roli: na sparing idzie i obita kolczuga.

**Skad zapas (jedna regula: "sprzet ludzi, ktorego nikt nie nosi"):**
- **AI** (takze partie Twojego rodu i lordowie w Twojej armii): nadwyzka zbrojowni ponad komplet - po typie, jak przy sprzedazy nadwyzek
  (`M\MenPurse.cs:328-374`, bez progu `SurplusKeepPercent`; bron biala grupa przy `AiAnyMeleeWhenShort`; tylko sztuki zdatne). **Tabor sie nie liczy**
  (recenzja kodu, rozdz. 13): to lup lorda (sprzedaje go w calosci w miescie - nic nie oddaje) i zaopatrzenie BK (`BK\...\PartySupplies.cs:118-126, 312-330`:
  BK kupuje do taboru kazdej partii lorda bron i tarcze MeleeWeapons2/3, Shield2/3 za zloto lorda i sam je codziennie zuzywa) - liczenie go dawalo AI premie
  prawie za darmo i podwojne zuzycie zaopatrzenia BK. Tak samo jak u Ciebie: Twoje sakwy sie nie licza.
- **Gracz:** (1) wlasny zapas cwiczebny - to, co ludziom dales; (2) nadwyzka ludzi w Twojej zbrojowni DTE ponad komplet, liczona jak w `MenPurse.SellPlayerSurplus`
  (`QuartermasterLaw.NeedForType` / `HaveFor`), bez Twoich wkladow (`ArmouryBehavior.StockOf`), tylko sztuki zdatne (jak u AI - unikat nie robi nadwyzki
  zwyklej sztuki). Twoje sakwy sie nie licza - to Twoj towar.
- Zapas gracza dziala tylko przy `DrillLaw` (bez musztry nic nie daje, wiec nic nie przyjmuje) i przy Z1 `DonationXpOff` (zapas zastepuje XP za oddany sprzet;
  przy wylaczonym Z1 gra daje XP za oddanie, wiec zapas nie przyjmuje - albo XP, albo zapas): `Drill.StockOn`.

**Jak dajesz (ekrany, na ktorych dzis sprzet przepada):**
1. Zwykly ekran ekwipunku, lewa strona "Discard" (`InventoryScreenHelper.OpenInventoryPresentation`, `G\Helpers\InventoryScreenHelper.cs:174-195`).
2. Ekran lupu gry (tryb Loot, `OpenScreenAsLoot` :276-285) - to, czego nie wziales (oblezenia, kryjowki, karawany, lup bez Spoils).
3. Spoils "Inspect trophies" -> "Leave": to, co zostawiasz na polu (`SP\...\LootCollectionBehavior.cs:1936-1958`). Spoils obiecywal tu trening -
   teraz to prawda, ale w sprzecie, nie w darmowym XP.
- Rozpoznanie ekranu 1-2: znacznik na czas metody otwierajacej + postfiks `InventoryLogic.Initialize` (zapamietuje logike ekranu); przyjecie w zdarzeniu gry
  `OnItemsDiscardedByPlayerEvent` (gra je odpala w `InventoryLogic.DoneLogic`, `G\...\InventoryLogic.cs:579-581`, z lewa lista ekranu) tylko, gdy lista zdarzenia
  to lewa lista zapamietanej logiki (ta sama referencja). Biala lista jest zamknieta - ekrany Spoils, magazyn, zbrojownia DTE, ekrany innych modow nic nie wleja.
  W trybie oszustw (`Game.Current.CheatMode`, lewa strona ekranu 1 = 10 sztuk wszystkiego) ekran 1 nic nie daje.
- Do limitu (2 x pelny na grupe), najtansze sztuki najpierw; przyjete schodza z listy ekranu, reszta jak dzis (przepada / zostaje na polu). Komunikat
  w grze podaje stan laczny (od Ciebie + zapasowa bron ludzi) wobec pelnego i osobno wolne miejsce, np. "Your men took 12 pieces into their drill stock.
  They now drill with 30 weapons and 40 pieces of armour, their spare kit in the armoury included (a full set is 38 of each - one per three men). They will
  take 54 more weapons and 36 more pieces of armour from you. 3 pieces are lost - no room."
- Nie ma XP za oddanie (Z1 zostaje) i nie ma odbierania z zapasu.
- **Zapas zostaje z druzyna** jak Twoja zbrojownia DTE (ludzie zostawieni w zalodze, zwolnieni albo polegli nie zabieraja jej ze soba - `M\MusterOut.cs:15-18`,
  `KitMovesWithMen` dotyczy tylko AI). Nigdy nie wraca do sakw gracza i nie wazy (jak zbrojownia DTE) - nie da sie go wyjac, wiec to nie magazyn.

**Zuzycie (tylko w dni cwiczen: D > 0 i bez kary glodu/snu):** w kazdej grupie w uzyciu = min(zapas, pelny); licznik += w uzyciu x D / 200 (AI bez `DrillLawAi`:
D = 1, jak plaski trening gry); za kazda cala jednostke odchodzi najgorsza sztuka (najnizsza wartosc; u AI stan ze `AiWear.TakeCondition`, `M\AiWear.cs:249`,
wolany PRZED zmniejszeniem zbrojowni - jak `MenPurse.SellAiSurplus`, inaczej spis `AiWear` gubil obita sztuke i po bitwie dopisywal fantom). Kolejnosc:
gracz - najpierw zapas od Ciebie, potem nadwyzka ludzi (nigdy ponizej kompletu i nigdy Twoje wklady); AI - nadwyzka zbrojowni (tabor nietkniety).

**Zlom (wlasnosc ludzi - nic dla gracza):** metal sztuki (`WorkshopLaw.Needs(it)[0]`, `M\WorkshopLaw.cs:157`) x `OldStockScrapYield` (0.5) -> ruda
"czekajaca" w liczniku partii (sztuki ludzi: nadwyzka zbrojowni, sztuki od gracza - juz ludzi). Przy wejsciu do miasta (i w dobie spedzonej w miescie) kowale
odkupuja cale ladunki rudy po cenie skupu tego miasta (`MenPurse.SellPrice`), najwyzej za tyle, ile ma kasa miasta; ruda trafia na polke miasta w ramce ksiegi
towarow "zlom z cwiczen (musztra)" i do ksiegi rudy. Zaplata jak przy sprzedazy nadwyzek: gracz - cala do sakiewki ludzi; AI - trzecia (`LordLootThirdPercent`)
lordowi (w ksiedze rodu 169 jako "trzecia lorda"), reszta do sakiewki ludzi (`M\MenPurse.cs:363-369`; w partiach Twojego rodu tak samo jak przy sprzedazy nadwyzek -
trzecia dla dowodcy partii). Bez `MenPurseEnabled` cala zaplata dla przywodcy partii. Drewno, skora i plotno sztuki zuzywaja sie na cwiczeniach - liczone
w linii dnia jako "bez metalu". Partia rozbita - czekajacy zlom przepada (licznik). Zapis: format v1 bez zmian (piate pole licznika partii zawsze 0; zlom
"lorda" ze starszych zapisow probnych przechodzi do zlomu ludzi).

**Perki - nowe opisy (po angielsku):** Giving Hands, czesc kwatermistrza: "Weapons and shields in your men's drill stock (gear you discard or leave on the field,
and their spare arms) count 50% more in their daily drill."; Paid in Promise, czesc kwatermistrza: "Armour in your men's drill stock (gear you discard or leave
on the field, and their spare armour) counts 50% more in their daily drill." Druga polowa obu perkow (clo, pensje) bez zmian.
Podmiana przy starcie kampanii (przy `Drill.StockOn`): `PerkObject.PrimaryDescription` / `SecondaryDescription` (refleksja, `G\...\PerkObject.cs:35-37`) i zmienne STR1/STR2
w `Description` (gra sklada opis z tych dwoch, `PerkObject.cs:66-81`).

## 4. Gdzie w kodzie (drzewo `SCR\noc2\musztra`)

| Miejsce | Zmiana |
|---|---|
| NOWY `M\Drill.cs` | stale; licznik godzin ruchu (maska 24 bitow, `RestHour` - definicja ksiegi snu); kontekst partii na tick (L, D, S); zapas gracza (`ItemRoster`); przyjecie z ekranow 1-3; zuzycie i zlom; sprzedaz zlomu kowalom; linie dnia; opisy perkow; zasilenie zapasu w autotescie; `Export`/`Import`; hak `Func<MobileParty,int> SleepDebtOf` (T10) |
| `M\ArmsDrill.cs` `TrainingPostfix` | `Drill.Shape` przed 171 (nowy wynik B x L x D x S + P; Z14a takze, gdy gra dala 0); udzial A: Z14a wedlug `Drill` (gracz `DrillNeedsArmsPlayer`, AI `AiGear.On && PartyDrillNeedsArms`), AI poza Z14a - jak dzis (`Gated`); `Drill.Done` - liczniki z wynikiem koncowym |
| `M\ArmsDrill.cs` `ShareOf` | dla `MainParty` zbrojownia z `QuartermasterLaw.DteArmory()` (suma po przedmiocie) zamiast `AiGear.Armories()` - ten sam algorytm typ x tier >= t-1 |
| prefiks + postfiks + finalizer `MobilePartyTrainingBehavior.OnDailyTickParty` (`G\...\MobilePartyTrainingBehavior.cs:42-51`) | prefiks: znacznik "trening tej partii" (liczniki tylko w ticku), dla gracza migawka XP rosteru i limitu awansu (`PartyBase.OnXpChanged`, `G\...\PartyBase.cs:1184-1194`); postfiks: zuzycie zapasu, sprzedaz zlomu w miescie, kontrola gracza (wyliczone = przyjete + uciete) |
| prefiks + finalizer `InventoryScreenHelper.OpenInventoryPresentation` (prywatna, nie jednolinijkowiec) i `OpenScreenAsLoot`; postfiks `InventoryLogic.Initialize` (13 parametrow) | znacznik "otwierany ekran z bialej listy" na czas metody otwierajacej; `Initialize` w jej trakcie zapamietuje swoja `InventoryLogic` - biala lista ekranow 1-2 (nie zalezy od chwili, w ktorej gra aktywuje stan ekranu); w zdarzeniu wyrzucenia porownanie lewej listy tej logiki z lista zdarzenia |
| `M\ArmouryBehavior.cs` | `Drill.Reset()` w konstruktorze; `OnItemsDiscardedByPlayerEvent` -> `Drill.OnDiscarded`; `SettlementEntered` -> `Drill.OnEntered` (zlom); `HourlyTickEvent` -> `Drill.Hourly`; `Drill.SessionStart()` w OnSessionLaunched; `Drill.Daily()` obok `ArmsDrill.Daily`; `SyncData` klucz "arm_drill" przez `SaveText.Sync` we wlasnym try |
| `M\SpoilsSeal.cs` | prefiks `LootCollectionBehavior.OnCompleteLeaveConsequence`: bron i zbroje z `_lootScreenRoster` do zapasu PRZED `TryCreateBattlefieldRemnant`/`GiveLeftoverXpToTroops` (przy `Drill.StockOn`); napisy `MenuText`/`TipText` mowia o zapasie tylko przy `Drill.LeaveOn` (zapas czynny i ten prefiks wpiety - `DrillLeaveWired`) |
| `M\GoodsLedger.cs`, `M\OreLedger.cs` | nowa ramka `FDrill` "zlom z cwiczen (musztra)" (zrodlo rudy, ujscie uzbrojenia z rosterow) i licznik rudy "zlom z cwiczen" - linie "Ruda:" i "Towary (bilans)" zostaja ZGODNE |
| `M\SubModuleMain.cs` | `Drill.ApplyAll(_harmony)` obok `ArmsDrill.ApplyAll` |
| `M\Settings.cs` + `python tools/gen_mcm.py` | 7 wylacznikow (rozdz. 5) |

Czego NIE ruszam: `MenPurse`, `QuartermasterLaw`, `AiGear`, `AiWear`, `NightRest`, `DonationXpLaw` - tylko czytam ich funkcje (mniej kolizji z K1 i T10).
`DonationXpLaw.Closing` z wersji 1 odpadl (krytyka 13).

## 5. Wylaczniki MCM (opisy po angielsku)

| Klucz | Domyslnie | Co |
|---|---|---|
| `DrillLaw` | true | Z14a: Twoja druzyna, partie Twojego rodu, lordowie w Twojej armii (L, D, zero przy glodzie/snie) |
| `DrillNeedsArmsPlayer` | true | udzial uzbrojonych dla Twojej druzyny (zbrojownia DTE), takze dla perkow |
| `DrillStock` | true | zapas gracza: przyjecie z ekranow 1-3, nadwyzka ludzi, S, zuzycie, zlom; nowe opisy perkow. Czynny tylko przy `DrillLaw` i `DonationXpOff` (`Drill.StockOn`; linia startowa mowi "NIECZYNNY", gdy brakuje warunku) |
| `DrillStockAi` | true | zapas AI (wszystkie partie lordow AI, takze Z14a; tylko nadwyzka zbrojowni): S, zuzycie, zlom |
| `DrillPenaltyAi` | **true** | kara AI: glod albo dlug snu = XP 0 (takze perki), bez zuzycia zapasu - wszystkie partie lordow AI poza Z14a, niezaleznie od Z14b ("takie same kary jak gracz"); dlug snu AI dziala od scalenia z T10 |
| `DrillLawAi` | **false** | Z14b: L i D dla AI poza Z14a - do pomiaru (wlaczony daje tez kare, jak `DrillPenaltyAi`) |
| `DrillLog` | true | linie dnia "Musztra (gracz)", "Musztra AI", co 5 dob "Musztra AI wedlug krolestw" |

Uwaga na pozniej: wlaczenie Z14b po pomiarze to zmiana `DrillLawAi` w `Armoury.json` Jeffa (MCM trzyma wartosc), nie tylko domyslnej w kodzie (CLAUDE.md, pulapka wpisu 82).
`DrillStockAi` domyslnie wlaczony: S >= 1 nie moze obnizyc XP AI (warunek c spelniony z definicji); jedyne ryzyko to sprzezenie z rynkiem (zuzycie
nadwyzki, ktora dzis idzie na targ) - mierzy je linia "Musztra AI" (szt./dobe) i prog T7 (rozdz. 10); po przekroczeniu - wylacznik i wracam z pomiarem.

## 6. Skutki

- **Ty:** dzis poza bitwa Twoje wojsko uczy sie tylko z perkow (po Z1 juz bez XP z oddawania), teraz ok. 5-30 XP na glowe dziennie (t1-t3, Przywodztwo 50-170,
  marsz bez zapasu .. postoj z pelnym zapasem). Oplaca sie stac w miescie albo obozie, karmic ludzi, spac w nocy, trzymac bron w zbrojowni i oddawac stara bron
  zamiast ja wyrzucac. Perki w treningu tez zaleza od broni (jak u AI) i znikaja w dzien glodu albo niewyspania. Zlom z zapasu sprzedaja kowale miasta - zaplata do sakiewki ludzi.
- **Partie Twojego rodu:** dotad bez bazy - teraz regula jak u Ciebie, z Przywodztwem swojego dowodcy i zapasem z wlasnej nadwyzki i taboru.
- **Lordowie w Twojej armii:** dotad tracili trening na czas sluzby w Twojej armii (gra im go zabierala) - teraz cwicza wedlug tej samej reguly i ponosza wlasny
  dlug snu *[grupa11: z ksiegi snu AI, nie Twoj - ida z Toba noca, wiec traca te same noce, a gdy spisz w obozie, spia z Toba (grupa11-p); lord, ktory dolaczyl
  wyspany, nie placi za Twoje wczesniejsze nocne marsze, i odwrotnie]*.
- **AI (domyslnie):** zapas z nadwyzki zbrojowni (S >= 1, czyli troche szybciej tam, gdzie maja nadwyzke) i jego zuzycie; kara jak u gracza - glodne (od razu)
  i niewyspane (od T10) partie nie cwicza; zasada dowodcy i dnia (Z14b) wylaczona - linia pomiaru.
  **Po wlaczeniu Z14b:** lepsi dowodcy i armie na postoju szkola lepiej; srednio w granicach -5%/+5% wobec samej 171 (warunek wlaczenia).
- **Gospodarka:** zapas gracza zuzywa ok. 0.5 szt./dobe na 100 ludzi; AI zuzywa swoj zapas przy wlaczonym `DrillStockAi` (domyslnie - takze partie Z14a);
  szacunek gornej granicy ok. 200 szt./dobe na swiat [S] wobec ok. 900-1 200 szt. kupowanych dziennie przez AI [P "ZakupyAI", `SCR\kopia172b`]. Zuzyta sztuka
  wraca w polowie metalu jako ruda u kowali - pomaga 174 (wiecej rudy), zabiera troche nadwyzki, ktora dzis idzie na targ jako uzywana.

## 7. Zamkniety obieg (nic z niczego)

- Kazda sztuka zapasu ma pochodzenie: Twoja (wyrzucona), wroga (trofea, lup ekranu gry) albo nadwyzka zbrojowni ludzi (Twoich i AI). Zapas niczego nie tworzy.
- Wyjscie jest jedno: zuzycie. Sztuka zuzyta -> ruda (50% metalu) czekajaca w liczniku partii -> kowale miasta (kasa miasta placi wlascicielowi);
  drewno, skora, plotno - zuzyte, liczone. Partia rozbita - czekajacy zlom przepada (licznik w linii).
- Zapas nie wraca do sakw i nie da sie go wyjac; zostaje z druzyna jak zbrojownia DTE.
- Zloto: jeden nowy przeplyw z platnikiem i odbiorca (kasa miasta -> sakiewka ludzi / lord) za prawdziwy towar (ruda na polke). XP: tylko z czasu, dowodcy,
  broni i zapasu - nie z wartosci przedmiotow i nie z klikania (Z1 bez zmian).
- Linia dnia gracza bilansuje zapas od Ciebie: wczoraj + przyjete - zuzyte = dzis (ZGODNY / NIEZGODNY).

## 8. Zgranie z innymi paczkami

- **Z1 / Z1b (DonationXpLaw, SpoilsSeal 11-13):** XP za oddanie zostaje 0. Zapas przyjmuje tylko przy wlaczonym Z1 (`Drill.StockOn`) - przy wylaczonym Z1 gra
  i Spoils daja XP za oddanie jak dawniej, a zapas nic nie bierze (nie oba naraz). Napisy Spoils (`TipText`, `MenuText`) mowia o zapasie tylko przy `Drill.LeaveOn`.
  Pytanie gry "You are discarding items" na zwyklym ekranie zostaje (rzeczy naprawde odchodza z sakw).
- **171 (ArmsDrill):** ten sam postfiks, ta sama zmienna A; linia "Cwiczenia (171)" bez zmian dla AI (partie Z14a nie wchodza do jej licznikow - maja swoja linie).
- **174 (zlom OldStockToScrap):** ten sam uzysk rudy (jedno ustawienie); zlom 174 skupuja kowale z polek, zlom z cwiczen kowale odkupuja od wlasciciela sztuki.
- **K1 (dozbrajanie, `SCR\noc2\k1`, baza sklad5):** musztra czyta nadwyzke tak, jak liczy ja dzisiejszy `MenPurse`. K1 A9 zmienia nadwyzke na liczona po dopasowaniu
  (najgorsze uzyteczne ponad komplet + 10%) i ksiege "sztuka ludzi / sztuka gracza". Przy scaleniu K1 z musztra: funkcja nadwyzki musztry -> funkcja K1 (jedna definicja),
  zuzycie dalej "najgorsze najpierw" (K1 trzyma w zapasie najlepsze - bez sprzecznosci). Wymiana 1:1 z K1 oddaje Ci gorsze sztuki - mozesz je wyrzucic do zapasu.
  **Warunek scalenia (zapisany tez w naglowku `Drill.cs`):** bez wspolnej funkcji zuzycie musztry zabiera sztuki, ktore K1 uznaje za potrzebne (+10%), a K1 je
  odkupuje - petla kupna i zuzycia na koszt lordow. Autotest po scaleniu: "ZakupyAI" wobec "Musztra AI: zuzyto" (zuzycie nie moze podnosic zakupow AI).
- **T10 (nocny marsz AI, w toku, `SCR\noc2\t10`):** AI niewyspane ma te same kary co Ty [J 04:00, 1]. Musztra czyta hak `Drill.SleepDebtOf` (domyslnie 0)
  i definicje godziny postoju `Drill.RestHour` (osada, oboz, <= 0.35 jedn./h - czesc wspolna ze snem). **Wpina je ten, kto scala drugi:**
  `Drill.SleepDebtOf = NightRest.AiDebtOf` przy starcie, a `NightRest.OnHourly` i R2 wolaja `Drill.RestHour` dla czesci wspolnej i dokladaja po swojej
  stronie morze, sluzbe ROT i oboz swiata (zapisane tez w PROJEKT-T10). Ustawienie T10 nazywa sie `AiSleepDebt` (bool) - inna rzecz niz hak.
  Kara snu AI w musztrze dziala od chwili wpiecia haka przy domyslnym `DrillPenaltyAi` - nie czeka na Z14b. Musztra nie edytuje `NightRest.cs`.
  Do czasu T10 u AI poza armia gracza liczy sie tylko glod; zepsuty hak liczy sie do potkniec (`Drill.SleepDebt`).
  **[grupa11 (scalenie MUSZTRA + T10, galaz `noc/grupa11`) - ZROBIONE INACZEJ, nie wracac do haka:** hak `Drill.SleepDebtOf` i `NightRest.AiDebtOf` usuniete,
  zastapione jedna funkcja `NightRest.DebtOf(mp)` wolana wprost z `Drill.SleepDebt` (gracz - `NightRest.Debt`, kazda inna partia lorda - ksiega snu AI, takze
  doczepieni do armii gracza: krytyka 5 zmieniona, powod - jedna prawda z kara predkosci i morale, ktora T10 bierze z ksiegi AI). grupa11-p: doczepieni spia
  z graczem w menu, takze przez swit (`NightMarch.SleepsWithLeader`: wodzem moze byc gracz, `_sleeping`), a ksiega AI z zapisu wchodzi od pierwszej klatki
  po wczytaniu (`AiImportNow`). `NightRest.OnHourly` i R2 wolaja `Drill.RestHour`; jedna stala `Drill.RestStep`. Godzina niezaobserwowana: sen = postoj,
  musztra = ruch (roznica celowa, opis w CHANGELOG grupa11-p).**]**
- **Z4 (ksiega umiejetnosci, niezrobiona):** dopoki jej nie ma, musztra podaje co 5 dob sredni tier, t3+ i konnych wedlug krolestw (potrzebne do progu Z14b).
- **175 (armie):** zmienia wzorce, wiec potrzeby i udzial A - musztra liczy z biezacych wzorcow, nic do zgrywania.

## 9. Ryzyka / co sprawdzic przy wykonaniu

- `TrainingPostfix` dziala tylko na zewnetrznym modelu (`_tDepth`); log startowy "modele szkolenia 3, czynny BK" (sklad6: tak [P]).
- Zapas gracza w zapisie: id przedmiotu albo modyfikatora nieznane po wczytaniu (zmiana modow) - pominiete i policzone w linii "z zapisu odrzucono N szt.".
- Wiele partii: L, D, S liczone raz na partie na tick (kontekst), licznik godzin O(1) na partie - jak "Miara" (0.5-1.3 ms na dobe [P]).
- Opisy perkow podmieniane przy starcie kampanii: zmiana `DrillStock` w trakcie gry dziala na opisy po ponownym wczytaniu (jak napisy Spoils pod-klanu).
- Waga: zapas nie wazy (jak zbrojownia DTE) - i nie da sie go wyjac.
- Kolejnosc zdarzen: trening partii idzie w jej ticku dobowym (gra rozklada je na cala dobe - `CampaignPeriodicEventManager`), linie dnia o polnocy - linia gracza mowi o ostatnim treningu w minionej dobie.
- Kara AI (`DrillPenaltyAi`) zmienia linie "Cwiczenia (171)": glodna partia ma wynik 0, wiec 171 jej w tej dobie nie liczy (`ArmsDrill.TrainingPostfix` liczy tylko wynik > 0) -
  srednia T3 moze sie przesunac o udzial glodnych partii (linia "Musztra AI" podaje "partii bez cwiczen").
- Koszt (T1): jeden zegar na wejscia z zewnatrz (godzina, model treningu `Shape`/`Done`, tick partii z zuzyciem i kowalami, ekrany zapasu, wejscie do miasta, linie dnia);
  linie dnia licza sie do kosztu nastepnej doby (linia jest wypisana, zanim skonczy sie ich pomiar). Nie liczy udzialu uzbrojonych 171 (`ShareFor` - koszt 171).

## 10. Plan testu

**Autotest A - ustawienia domyslne (Z14b wylaczone, kara AI wlaczona):**
- **Zasilenie zapasu** (tylko w autotescie - `CrashScribe.Autotest.Active`): od doby 2 co dobe, dopoki zapas od gracza nie jest pelny w obu grupach (sztuka na
  3 ludzi), ta sama funkcja przyjecia co ekrany: brakujace sztuki z taboru gracza, reszta kupiona najtaniej z polki miasta za zloto gracza, gdy druzyna stoi
  w miescie (handel, nie z niczego). Flaga w zapisie dopiero przy pelnym zapasie. Linia "Musztra (autotest): zasilenie zapasu - proba N, ludzi M (pelny P) ...".
  **Zaden dotychczasowy bieg nie ma wojska gracza:** A1 - "DesertionLaw: partia gracza 1 ludzi", zloto 100 d (`SCR\kopia-sklad6\Armoury-2026-10-09_03-48-35.log`);
  zapis 362 - tez "partia gracza 1 ludzi, ... glod: TAK", zloto 0-100 d, "ludzie gracza 0" w linii zoldu (`...\Armoury-2026-10-09_04-03-18.log`). Ten jeden
  czlowiek to bohater, wiec gra nie wola treningu dla zadnego oddzialu - linia gracza mowi "brak treningu" i to jest poprawne. Czesc gracza (T2, T5, T6)
  wymaga osobnego biegu A5.
- A1: nowa kampania 40 dob - AI (T1, T3, T4, T7), linia startowa, 0 bledow; zasilenie: "proba 1 ... druzyna bez zolnierzy" (i co 10 prob). Na koncu zapis.
- A2: wczytanie zapisu z A1 (tryb AT3 `LoadName`), 8 dob - T6 czesc "pusto po pusto" (maska, liczniki AI, flaga zasilenia nie ustawiona).
- A3: zapis doby 362 (stary, bez klucza "arm_drill"), 8 dob - linia "z zapisu: brak klucza - zapas pusty", 0 bledow, AI na starym swiecie.
- **A5 (DO PRZYGOTOWANIA): bieg z wojskiem gracza, 40 dob** - potrzebny zapis z druzyna gracza (kopia - zapisy Jeffa nietkniete) albo werbunek w autotescie
  zwykla droga gry za zloto (osobna zmiana CrashScribe, nie w tej paczce - w obu dotychczasowych biegach gracz ma 0-100 d). Na nim: zasilenie w pierwszym
  miescie, T2 codziennie, T5 (zuzycie gracza ok. 0.25 szt./dobe na grupe przy 100 ludziach na postoju - w 40 dob kilka sztuk), sprzedaz zlomu w miescie;
  A6 - wczytanie zapisu z konca A5, 8 dob - T6. Do czasu A5 czesc gracza sprawdzaja tylko proby reczne Jeffa (R1-R7).

Linie logu (format do sprawdzenia recznie):
- `Musztra (gracz): dzien N - ludzi 112; B 1568 x dowodca 0.588 (Przywodztwo 100) x dzien 1.50 (postoj, ruch 2 h z 24) x zapas 1.229 = 1700; + perki P 210 = 1910; bron A 0.970 (sr. wazona ludzmi); (B x L x D x S + P) x A = 1852.7 wobec XP wyliczone 1852 (0% roznicy; 16.5 na glowe); przyjete przez roster 1852, uciete limitem awansu 0 -> ZGODNE; zapas: bron 30/38 (od Ciebie 22, nadwyzka ludzi 8), zbroje 40/38 (od Ciebie 40, nadwyzka 0), Giving Hands nie, Paid in Promise tak; przyjeto 12 (wyrzucone 12, lup 0, trofea 0), bez miejsca 3; zuzyto 1 (bron 0, zbroje 1; bez metalu 0), zlom czeka 0.4 rudy, sprzedano 0 ladunkow za 0 d; bilans zapasu od Ciebie ZGODNY (wczoraj 51 + przyjete 12 - zuzyte 1 = dzis 62); partie rodu i armii: 3 (ludzi 260, XP 2100, dowodca sr. x0.82, na postoju 1 z 3); potkniecia 0.`
- `Musztra AI: dzien N - partii 689, ludzi 102808; wazone baza gry: dowodca x0.97 (Przywodztwo mediana 165, p10 120, p90 210), dzien x0.93 (postoj 9%, marsz 88%, glod/sen 3%), dowodca x dzien x0.90 (Z14b WYLACZONA - pomiar); zapas x1.03 (CZYNNY); razem x0.93; razem przy progu postoju 4/8/12 h: x0.93/x0.97/x1.02; PROG Z14b - dowodca x dzien bez zapasu, dni bez kary (97% wagi): x0.93, przy progu postoju 4/8/12 h: x0.93/x0.97/x1.01; godziny ruchu w dobie: 0 h 120, 1-3 h 50, 4-11 h 90, 12+ h 429; kara glod/sen CZYNNA (partii bez cwiczen 18, zabrane XP gry 41000); XP: baza gry 1.62 mln, po czynnej regule 1.67 mln; zuzyto 41 szt. (bron 33, zbroje 8), zlom czeka 210.3 rudy, sprzedano 12 ladunkow za 480 d (lordowie 160, sakiewki 320), przepadlo z rozbitymi 0.0; potkniecia 0.`
- co 5 dob: `Musztra AI wedlug krolestw: dzien N - Polnoc: partii 40, ludzi 11868, dowodca x dzien x0.92, zapas x1.02, prog Z14b (bez zapasu, dni bez kary) 4/8/12 h: x0.93/x0.97/x1.01, godziny ruchu 0/1-3/4-11/12+: 30/10/40/120, sredni tier 2.71, t3+ 31%, konni 5% | ...`

| Prog | Bieg | Warunek przejscia |
|---|---|---|
| T1 bledy i koszt | A1, A3, A5 | 0 bledow `Drill.*` / `ArmsDrill.*`, potkniecia 0 we wszystkich liniach (ciche wyjatki w `Eligible`, `Men`, `SleepDebt`, `Scrap`, `TakeCondition` tez sie licza); koszt musztry < 30 ms na dobe (zegar na wszystkich wejsciach, rozdz. 9); s/dobe w granicach +3% od ostatniego testu (13.1 s nowa kampania, 21.8 s z zapisu 362) |
| T2 gracz | A5 | linia gracza w kazdej dobie z treningiem; "XP wyliczone = przyjete + uciete" (do 1 XP na oddzial) - jedyna kontrola niezalezna; "(B x L x D x S + P) x A" z A wazonym liczba ludzi = wyliczone w granicach +-5% (A rozne w oddzialach); dzien = postoj, gdy ruch < 4 h; XP = 0 (z perkami) i komunikat w grze w dobie z glodem albo dlugiem snu |
| T3 AI bez zmian (171) | A1 | "Cwiczenia (171)" partii rodow AI: srednia x0.38 (doba 1) -> x0.68 (doba 40) +-0.05 wobec `SCR\kopia-sklad6\Armoury-2026-10-09_03-48-35.log` (glodne partie wypadaja z tej linii - rozdz. 9) |
| T4 pomiar Z14b | A1 | linia AI codziennie, czynniki dla >= 95% partii lordow AI (partii w linii / partii w "Cwiczenia (171)" + partii bez cwiczen); linia krolestw co 5 dob |
| T5 obieg | A5 (ksiegi rudy i towarow takze A1) | bilans zapasu gracza ZGODNY codziennie; zasilenie do pelnego; co najmniej jedno zuzycie gracza i zlom (czekajacy albo sprzedany); "Ruda:" i "Towary (bilans)" ZGODNA; ruda sprzedana <= 0.5 x metal zuzytych sztuk |
| T6 zapis | A2, A3, A6 | A6: linia "Musztra: z zapisu zapas N szt., godzin ruchu M z 24, zlom czeka X" = stan z ostatniej linii A5; A2: pusto po pusto, pierwsza doba po wczytaniu nie daje AI postoju za darmo; A3: brak klucza - pusto, 0 bledow |
| T7 rynek | A1 | zuzycie zapasu AI <= 300 szt./dobe na swiat; "Pokrycie zbrojowni AI (171)" glowna bron w dobie 40 nie nizej niz w kopia-sklad6 - 5 pkt |

**Prog wlaczenia Z14b** (dopiero z T10 w drzewie, `AiSleepDebt` wlaczony - inaczej mierzymy inna regule niz docelowa): srednia z dob 11-40
wspolczynnika "PROG Z14b - dowodca x dzien bez zapasu, dni bez kary" z linii AI (wazona baza gry; kara dziala w obu wariantach, wiec dni kary sie skracaja)
w granicach **0.95-1.05** dla calego swiata I >= 0.95 dla kazdego krolestwa (linia krolestw: ten sam wspolczynnik przy progu 4 h). Progi postoju 8 i 12 h -
te same liczby obok, do wyboru progu po pomiarze.
Spelnione -> **autotest B**: dwa biegi po 120 dob z tego samego punktu (ten sam zapis startowy): bez Z14b i z Z14b; w dobie 120 sredni tier i odsetek t3+
kazdego krolestwa >= -5% wobec biegu bez Z14b, swiat +-5%, konnych nie mniej, 0 bledow. Niespelnione -> Z14b zostaje wylaczone; wracam z pomiarem
(ktory czynnik, ktore krolestwo, ktory prog postoju 4/8/12 h) i jedna propozycja.

**Proby reczne dla Jeffa** (autotest nie klika ekranow, jak przy Z1): R1 w polu wyrzuc 10 mieczy na ekranie ekwipunku -> komunikat "Your men took 10 pieces...",
w linii "przyjeto 10"; R2 Spoils: zbierz trofea, "Leave" - podpowiedz z liczba miejsc, sztuki w zapasie; R3 karta postaci kwatermistrza: nowe opisy Giving Hands /
Paid in Promise; R4 dzien w miescie i dzien marszu - "dzien 1.50" i "dzien 0.90" w linii; R5 noc w marszu bez snu -> nastepnego dnia XP 0 (takze perki);
R6 zapisz, wczytaj - zapas i godziny bez zmian; R7 wejdz do miasta po kilku dniach marszu - "sprzedano N ladunkow", sakiewka ludzi wieksza.

## 11. Pytania do Jeffa (tylko zmiana rozgrywki spoza Twoich decyzji)

**Baza Twojej druzyny.** Gra daje druzynie glowy rodu wieksza baze (15 + 3 x tier zamiast 10 + 2 x tier) i tak cwicza druzyny glow rodow AI. Ty tez jestes glowa
rodu, ale tabela, ktora zatwierdziles, liczyla 10 + 2 x tier. Zostawic tempo z tabeli (t1 -> t6 na postoju ok. 1.5 roku przy Przywodztwie 170), czy dac Ci baze
glowy rodu jak AI (wtedy ok. 1 roku przy 170 i ok. 1.7 roku przy 100)?
*Moja rada: zostawic tabele - to tempo wybrales swiadomie, a AI i tak cwiczy wolniej przez braki broni (171). W kodzie: tabela (10 + 2 x tier).*

**[grupa11-p] Ktory dzien traci niewyspany.** Gra rozklada codzienne cwiczenia partii po calej dobie, a musztra patrzy na dlug snu w chwili cwiczen. Dlatego
po jednej zarwanej nocy partia, ktora cwiczy tuz przed switem, a wieczorem odespi w obozie, moze nie stracic zadnego dnia, a partia, ktora cwiczy o 7:00,
traci caly dzien. Regula jest ta sama dla Ciebie i dla AI, wiec srednio jest rowno, ale dla jednej druzyny wychodzi to losowo. Zostawic tak, czy zawsze
"noc bez snu = nastepny dzien bez cwiczen" (liczone od switu, niezaleznie od godziny cwiczen)?
*Moja rada: zmienic na "od switu" - prosciej do zrozumienia i bez losu. W kodzie dzis: dlug w chwili cwiczen (bez zmian do Twojego slowa; AI ma juz zapisany
dlug ze switu - `AiSleep.DawnDebt`, dla Ciebie trzeba dodac takie samo pole przy swicie w `SettleNight`).*

**[grupa11-p, do zrobienia przy nastepnej pracy nad musztra - nie pytanie]** Maska godzin ruchu idzie do zapisu tylko u gracza, wiec po kazdym wczytaniu partie
Twojego rodu i lordowie w Twojej armii (Z14a) maja pierwsza dobe liczona jak marsz (x0.9 zamiast x1.5) - czeste wczytywanie troche obniza ich musztre. Naprawa:
zapis masek partii Z14a (`NoGameBase`) w `arm_drill` przez `SaveText.Sync` (nowy format napisu). NIE maska 0 po wczytaniu - to dawaloby darmowy postoj po
kazdym wczytaniu (wbrew "wczytanie nie daje postoju").

## 12. Krytyka i odpowiedzi

| # | Waga | Uwaga (skrot) | Werdykt | Co zmienione |
|---|---|---|---|---|
| 1 | wazne | "AI marszuje 87-90% dni" to zle odczytanie linii "Miara" - partie stojace caly dzien nie sa liczone | PRZYJETA | Sprawdzone: `M\WorldMeasure.cs:80-81` (ruch tylko poza osada/oblezeniem/armia), `:158` (`few` = 1-11 h). Liczby przeklasyfikowane na [S] nieznane (rozdz. 2); linia "Musztra AI" podaje rozklad godzin ruchu 0/1-3/4-11/12+ (swiat co dobe, krolestwa co 5 dob) i "razem" dla progow postoju 4/8/12 h; regula bez zmian |
| 2 | wazne | Zlom z nadwyzki ludzi szedl do sakw gracza; u AI cala ruda do lorda - zly odbiorca, dwie reguly | PRZYJETA | Sprawdzone: `M\ArmouryBehavior.cs:92-95` (lupy 60% = wojsko), `M\MenPurse.cs:255-269, 363-369`. Zlom czeka w liczniku partii osobno "sztuki ludzi" i "sztuki lorda" (tabor AI); kowale miasta odkupuja go przy wejsciu do miasta / w dobie w miescie (wzor MenPurse: stary sprzet przy pierwszej wizycie, bez teleportu do najblizszego miasta); kasa miasta placi; podzial jak sprzedaz nadwyzek; ruda na polke w ramce `FDrill` i w ksiedze rudy; nic do sakw gracza |
| 3 | wazne | Zapas AI pod tym samym wylacznikiem co L x D; S >= 1 nie oslabia; laczny wylacznik miesza skutki | PRZYJETA | Wylaczniki rozdzielone: `DrillStockAi` (S i zuzycie wszystkich partii lordow AI, takze Z14a; domyslnie wlaczony - decyzja 2 "jedna regula", S >= 1) i `DrillLawAi` (L i D, domyslnie wylaczony). W linii AI osobno "zapas" i "dowodca x dzien"; ryzyko rynku nazwane wprost (rozdz. 5) i prog T7 |
| 4 | wazne | D = 0 zerowalo tylko B; perki dalej uczyly glodnych i niewyspanych (sprzeczne z T2/R5) | PRZYJETA | Glod albo dlug snu zeruja caly wynik (B i P) - "wcale" doslownie; w bitwie (MapEvent) B = 0 i P zostaje, jak w grze |
| 5 | wazne | Lordowie w armii gracza bez kary snu; prog Z14b bez dlugu snu AI; nazwa haka koliduje z ustawieniem T10 | PRZYJETA | Partie doczepione do armii gracza (`AttachedTo == MainParty`) biora `NightRest.Debt` od razu; prog Z14b i autotest B dopiero z T10 w drzewie; hak `Drill.SleepDebtOf` (nie `AiSleepDebt`); w obu projektach: wpina ten, kto scala drugi (dopisek na koncu PROJEKT-T10). **grupa11 - ZMIENIONE:** hak zastapiony `NightRest.DebtOf`; doczepieni licza wlasny sen z ksiegi AI (powod: jedna prawda z kara morale i predkosci T10), grupa11-p - spia z graczem w menu, takze przez swit |
| 6 | wazne | T2 "przyrost = wzor" nie przejdzie przy t6 i ludziach czekajacych na awans (limit XP oddzialu) | PRZYJETA | Sprawdzone: `G\...\PartyBase.cs:1184-1194` (Xp <= Number x najwyzszy koszt awansu; t6 - limit 0). Linia gracza: "XP wyliczone", "przyjete przez roster", "uciete limitem awansu" (limit liczony przed dodaniem, w prefiksie treningu); T2: wyliczone = przyjete + uciete |
| 7 | wazne | Autotest nie zasila zapasu - T5/T6 przechodza trywialnie 0 = 0; stary zapis bez klucza | PRZYJETA | Zasilenie zapasu w autotescie (doba 2, ta sama funkcja przyjecia; z taboru, brakujace kupione z polki za zloto gracza); T6 na biegu A2 (wczytanie zapisu z A1, AT3 `LoadName`); T5 wymaga zuzycia i zlomu; zapis 362 jako osobny A3 (brak klucza) |
| 8 | wazne | Zapas wracal do sakw, gdy druzyna bez zolnierzy - darmowy niewazacy magazyn | PRZYJETA CZESCIOWO | Zdanie o powrocie do sakw usuniete: zapasu nie da sie wyjac ani sprzedac, nigdy nie wraca do sakw. Przeniesienie czesci do zalogi - ODRZUCONE z powodu: zbrojownia DTE gracza tez nie idzie z ludzmi (`M\MusterOut.cs:15-18` - zwalniany odchodzi tylko ze swoim; `KitMovesWithMen` jest dla AI, `M\GarrisonArmory.cs:75, 314-335`); jedna regula: zapas jest czescia zbrojowni druzyny i zostaje z druzyna. Bez prawa odbioru nie ma magazynu |
| 9 | drobne | Przyklady sie nie sumuja | PRZYJETA | Przyklady przeliczone (rozdz. 2 i 10: 1 568 x 0.588 x 1.5 x 1.229 = 1 700 + 210 = 1 910 x 0.97 = 1 852; S = 1 + 0.079 + 0.150); linie podaja B, L, D, S, P, A osobno |
| 10 | drobne | T3 porownywal z kopia172b zamiast sklad6 | PRZYJETA | Sprawdzone w `SCR\kopia-sklad6\Armoury-2026-10-09_03-48-35.log`: x0.38 (doba 1), x0.68 (doba 40). T3 wobec tej kopii, +-0.05 |
| 11 | drobne | Gorna granica 1.10 bez uzasadnienia; B 40 dob bez mocy, dwie rozne kampanie | PRZYJETA | Prog 0.95-1.05 (swiat +-5% jak w audycie 13), krolestwa >= 0.95; autotest B: dwa biegi po 120 dob z tego samego zapisu startowego, rozstrzyga tier i t3+ krolestw w dobie 120 |
| 12 | drobne | Trzecia definicja "stoi" (0.01 jedn./h) obok ksiegi snu i T10 (0.35) | PRZYJETA | Godzina postoju = definicja odpoczynku ksiegi snu: osada, oboz obleznikow albo ruch < 0.35 jedn./h - jedna funkcja `Drill.RestHour`, do uzycia przez T10 R2 przy scaleniu |
| 13 | drobne | `DonationXpLaw.Closing` zalezy od pola BK; postfiks na jednolinijkowcu `OpenScreenAsInventory` moze nie zadzialac | PRZYJETA | Bez `Closing` (DonationXpLaw nietkniety). Ekran rozpoznany w `InventoryLogic.Initialize` w trakcie `OpenInventoryPresentation` (prywatna, z petla - nie jednolinijkowiec) albo `OpenScreenAsLoot` (prefiks + finalizer = znacznik); w zdarzeniu wyrzucenia lewa lista tej logiki == lista zdarzenia (zamiast aktywnego stanu: `GameStateManager.PushState` moze odlozyc aktywacje, gdy trwa inna zmiana stanu - wtedy aktywny stan bylby jeszcze poprzedni); linia startowa "Musztra: start - ekrany zapasu wpiete N/2 + Spoils 1/1" |
| 14 | drobne | Ktory wylacznik A rzadzi lordami w armii gracza; partie rodu gracza bez AiGear stanelyby | PRZYJETA | Partie AI w Z14a: A przy `AiGear.On && PartyDrillNeedsArms` (jak `Gated`); `DrillNeedsArmsPlayer` tylko dla `MainParty` (zbrojownia DTE gracza) |
| 15 | drobne | Rozdzial dla Jeffa pomijal zmiany rozgrywki; zdanie o zuzyciu AI falszywe | PRZYJETA | Rozdz. 0 pkt 3 (perki tez x bron, dzis pelne), 5 (nadwyzka ludzi w zapasie i zuzyciu), 4 (ekran lupu gry), 6 (+20% bez perkow, +30% z perkami wobec "np. do +25-30%"); rozdz. 6 poprawiony (AI zuzywa przy `DrillStockAi`, takze partie Z14a) |

## 13. Recenzja kodu (19 uwag) i odpowiedzi - poprawki "MUSZTRA-p"

| # | Waga | Uwaga (skrot) | Werdykt | Co zmienione |
|---|---|---|---|---|
| 1 | wazne | Glod i dlug snu zeruja trening AI spoza Z14a tylko przy `DrillLawAi` (domyslnie wylaczony) - AI nie ma kar gracza | PRZYJETA | Nowy wylacznik `DrillPenaltyAi` (domyslnie wlaczony): kara (XP 0 z perkami, bez zuzycia) dla wszystkich partii lordow AI niezaleznie od Z14b; `DrillLawAi` steruje tylko L x D (wlaczony tez daje kare). Linia AI: "kara glod/sen CZYNNA (partii bez cwiczen, zabrane XP gry)". Rozdz. 0, 1, 5, 8 i dopisek PROJEKT-T10 poprawione |
| 2 | wazne | Tabor AI to tez zaopatrzenie BK (bron i tarcze z PartySupplies) - musztra liczyla je do S i zjadala najpierw | PRZYJETA | Sprawdzone: `BK\...\PartySupplies.cs:118-126` (MeleeWeapons2/3, Shield2/3), `:312-330` (kupno i codzienne zuzycie), `BKPartyNeedsBehavior.cs:227-229` (kazda partia lorda). Tabor AI nie liczy sie do zapasu i nie jest zuzywany (wariant z uwagi 10b) |
| 3 | drobne | `WearAiOne`: zbrojownia zmniejszana przed `AiWear.TakeCondition` - Sync gubi obita sztuke, fantom po bitwie | PRZYJETA | Sprawdzone `M\AiWear.cs:105-133, 252-262`. `TakeCondition` przed zmiana zbrojowni (jak `MenPurse.SellAiSurplus`) |
| 4 | drobne | `DrillStock` bez `DrillLaw` przyjmuje bez skutku; napisy Spoils przy wylaczonym Z1 | PRZYJETA | `Drill.StockOn` = `DrillStock` && `DrillLaw` && Z1 (przyjecie, S i zuzycie gracza, opisy perkow, prefiks Leave); napisy Spoils przy `Drill.LeaveOn` (+ `DrillLeaveWired`) |
| 5 | drobne | Zlom z taboru partii rodu gracza - zaplata do towarzysza; `NoteInflow` bez czesci lorda | PRZYJETA CZESCIOWO | Czesc lorda znikla razem z taborem (uwaga 2/10); `NoteInflow` dostaje cala kwote lorda. Trzecia dla dowodcy partii rodu gracza zostaje - ODRZUCONE: to ta sama regula co sprzedaz nadwyzek tych partii (`M\MenPurse.cs:180, 363-366`) |
| 6 | drobne | Komunikat przyjecia liczy tylko zapas od gracza (np. 60/38) | PRZYJETA | Stan laczny (od Ciebie + zapasowa bron ludzi) wobec pelnego i osobno wolne miejsce na grupe (rozdz. 3) |
| 7 | drobne | (a) RestHour to nie ta sama definicja co sen; (b) godziny i kowale pracuja przy wszystkim wylaczonym; (c) koszt bez Shape/Done | PRZYJETA (a czesciowo) | (a) granica `<= 0.35` jak `NightRest`; opis: czesc wspolna - morze i sluzba ROT to zasady snu, nie postoju (partia plynie / idzie z lordem), przy scaleniu T10 sen wola `RestHour`; (b) `Hourly` i tick partii pomijane, gdy musztra wylaczona i nic nie czeka; (c) jeden zegar na wszystkich wejsciach |
| 8 | wazne | (= 1) gracz w armii AI idzie noca i traci dzien, lordowie nie | PRZYJETA | Jak 1; kara snu AI zadziala sama po wpieciu haka T10. Pomiar Z14b liczony bez zapasu i tylko w dniach bez kary ("PROG Z14b"), bo kara dziala w obu wariantach |
| 9 | wazne | Autotest A1: gracz bez wojska i 100 d - zasilenie puste, `_fed` przed proba; 8+8 za malo na zuzycie | PRZYJETA CZESCIOWO | Zasilenie co dobe od doby 2 do pelnego zapasu (sztuka na 3 ludzi), flaga dopiero przy pelnym; proby logowane. Propozycja "zapis 362, gracz ma wojsko" - FALSZ: log `kopia-sklad6\Armoury-2026-10-09_04-03-18.log` - "partia gracza 1 ludzi, ... glod: TAK", zloto 0-100 d. Bieg z wojskiem gracza (A5) do przygotowania; tabela progow z kolumna "bieg" |
| 10 | wazne | U AI caly tabor liczy sie do S bez oddawania; gracz musi oddac na zawsze | PRZYJETA | Wariant (b): zapas AI = nadwyzka zbrojowni (jak nadwyzka ludzi gracza); tabor - jak sakwy gracza - sie nie liczy, takze w partiach rodu gracza |
| 11 | drobne | `PlayerSurplus` liczy w "have" unikaty i sztuki zakazane | PRZYJETA | Do "have" tylko sztuki `Eligible`, jak w `AiSurplus` |
| 12 | drobne | Koszt nie obejmuje Shape/Done, ticku, kowali, ekranow, linii | PRZYJETA | Zegar z licznikiem glebokosci (`Clk`/`Unclk`) na wszystkich wejsciach; linie dnia wchodza do kosztu nastepnej doby; linia mowi, co liczy |
| 13 | drobne | Ciche `catch` poza potknieciami | PRZYJETA | `Stumble` w `Eligible`, `Men`, `NoGameBase`, `SleepDebt`, `Scrap`, `TakeCondition`, `KOf`, `TrophyTip` |
| 14 | drobne | "Razem przy progu 4/8/12 h" zawiera S; brak L x D bez S dla 8 i 12 h, takze w krolestwach | PRZYJETA | Osobne sumy w x L x dt bez S (dni bez kary) dla 4/8/12 h - w linii AI i krolestw |
| 15 | drobne | "x bron A" = Fin/Pre - kontrola T2 zawsze przechodzi; w dzien glodu mylace "= 0.0; + perki" | PRZYJETA | A drukowane niezaleznie (srednia udzialu wazona liczba ludzi z `ShareFor`), kontrola "(B x L x D x S + P) x A wobec wyliczone" z % roznicy; w dzien kary "perki P x -> 0 (glod/sen - perki tez 0)". T2 opisany na nowo |
| 16 | drobne | Napisy: "40/38", "room for two", idiom w perkach, brak informacji o dniu bez cwiczen | PRZYJETA | Nowy komunikat przyjecia, opisy perkow "count 50% more" ze zrodlem zapasu, jedno zdanie w grze w dzien kary |
| 17 | drobne | Spoils: napisy bez `DrillLeaveWired`; przyjecie bez `DrillLaw`; przy wylaczonym Z1 i XP, i zapas | PRZYJETA | Jak 4 (`LeaveOn`, `StockOn` z Z1 - albo XP gry, albo zapas) |
| 18 | drobne | (= 7a) gracz na morzu wypoczety dla snu, w marszu dla musztry | PRZYJETA CZESCIOWO | Jak 7a: jedna funkcja na czesc wspolna, roznica celowa i opisana |
| 19 | drobne | Nadwyzka sprzed K1 - po scaleniu petla kupna i zuzycia | PRZYJETA | Warunek scalenia w naglowku `Drill.cs` i przy obu funkcjach, w rozdz. 8 i w CHANGELOG; autotest po scaleniu: "ZakupyAI" wobec zuzycia musztry |
