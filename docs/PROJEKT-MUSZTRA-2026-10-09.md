# MUSZTRA - codzienne cwiczenia wojska i zapas do cwiczen (Z14a, Z14b, decyzja 2) - projekt, 09.10.2026 (wersja 2 po krytyce)

Status: projekt poprawiony po krytyce (rozdz. 12) i WYKONANY w drzewie `SCR\noc2\musztra` (galaz `w-toku/musztra`, commit lokalny 0015a84, CHANGELOG "MUSZTRA",
status NIEWGRANE; Release zbudowany, kod wyjscia 0). Gry nie uruchamialem, niczego nie wgrywalem, autotestu nie bylo.
Poprawki po recenzji kodu (rozdz. 13, CHANGELOG "MUSZTRA-p", commit lokalny f24055d, DLL `SCR\test\Armoury-musztra.dll` md5 9751b8ccf3121491dfbf9c184cc21247): kara glodu i snu dla wszystkich lordow AI (nowy `DrillPenaltyAi`, domyslnie wlaczony - Jeff 09.10:
"takie same kary jak gracz"), zapas AI tylko z nadwyzki zbrojowni (tabor - lup i zaopatrzenie BK - sie nie liczy), zapas gracza tylko przy `DrillLaw` i Z1,
zasilenie autotestu do pelnego co dobe, pomiar Z14b bez zapasu i bez dni kary, koszt liczony w calosci.
**Rozdz. 14 (decyzje Jeffa 09.10 ok. 07:10): JEDEN WZOR dla gracza, lordow AI i glow rodow AI (bez bazy glowy rodu 15 + 3 x tier, Z14b domyslnie wlaczone) i
niewyspanie liczone od switu - projekt "MUSZTRA-j" na `noc/grupa11` (30ed818), skutek dla AI policzony z logow `SCR\kopia-grupa11`; kod jeszcze niezmieniony.**
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
    *[Odpowiedz Jeffa 07:10: jeden wzor dla wszystkich - baza 10 + 2 x tier takze dla glow rodow AI, AI na tym samym wzorze (Z14b wlaczone); noc bez snu = nastepny dzien
    bez cwiczen, liczone od switu. Projekt i skutek dla AI: rozdz. 14.]*

---

## 1. Regula (jedna dla gracza i AI)

Doswiadczenie na czlowieka na dobe, dla kazdego oddzialu (elementu rosteru) partii lorda, poza bitwa:

**XP = (B x L x D x S + P) x A**, a przy glodzie albo dlugu snu **XP = 0** (cale - takze perki).

| Czlon | Co to jest | Skad |
|---|---|---|
| B | baza gry: 10 + 2 x tier; druzyna glowy rodu AI 15 + 3 x tier (u gracza 10 + 2 x tier - pytanie 11); w bitwie 0 (jak w grze) *[MUSZTRA-j: 10 + 2 x tier dla wszystkich, baza glowy rodu znika - rozdz. 14]* | [K] `G\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyTrainingModel.cs:21-31`, BK `BK\BannerKings.Models.Vanilla\BKPartyTrainningModel.cs:41-50` |
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
| `DrillLawAi` | **false** | Z14b: L i D dla AI poza Z14a - do pomiaru (wlaczony daje tez kare, jak `DrillPenaltyAi`) *[MUSZTRA-j: domyslnie **true** - decyzja Jeffa 07:10, rozdz. 14; wylaczony = plaski trening gry z baza gry, jak dzis]* |
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
**[ODPOWIEDZ JEFFA 07:10: jeden wzor dla wszystkich - 10 + 2 x tier dla Ciebie i dla glow rodow AI (ich baza 15 + 3 x tier znika), AI na tym samym wzorze. Rozdz. 14.]**

**[grupa11-p] Ktory dzien traci niewyspany.** Gra rozklada codzienne cwiczenia partii po calej dobie, a musztra patrzy na dlug snu w chwili cwiczen. Dlatego
po jednej zarwanej nocy partia, ktora cwiczy tuz przed switem, a wieczorem odespi w obozie, moze nie stracic zadnego dnia, a partia, ktora cwiczy o 7:00,
traci caly dzien. Regula jest ta sama dla Ciebie i dla AI, wiec srednio jest rowno, ale dla jednej druzyny wychodzi to losowo. Zostawic tak, czy zawsze
"noc bez snu = nastepny dzien bez cwiczen" (liczone od switu, niezaleznie od godziny cwiczen)?
*Moja rada: zmienic na "od switu" - prosciej do zrozumienia i bez losu. W kodzie dzis: dlug w chwili cwiczen (bez zmian do Twojego slowa; AI ma juz zapisany
dlug ze switu - `AiSleep.DawnDebt`, dla Ciebie trzeba dodac takie samo pole przy swicie w `SettleNight`).*
**[ODPOWIEDZ JEFFA 07:10: (b) - od switu, dla Ciebie i AI tak samo. Rozdz. 14.2 i 14.4.]**

**[grupa11-p, do zrobienia przy nastepnej pracy nad musztra - nie pytanie]** Maska godzin ruchu idzie do zapisu tylko u gracza, wiec po kazdym wczytaniu partie
Twojego rodu i lordowie w Twojej armii (Z14a) maja pierwsza dobe liczona jak marsz (x0.9 zamiast x1.5) - czeste wczytywanie troche obniza ich musztre. Naprawa:
zapis masek partii Z14a (`NoGameBase`) w `arm_drill` przez `SaveText.Sync` (nowy format napisu). NIE maska 0 po wczytaniu - to dawaloby darmowy postoj po
kazdym wczytaniu (wbrew "wczytanie nie daje postoju").
**[MUSZTRA-jp - ZROBIONE szerzej: od Z14b domyslnie TAK ten sam blad zabieral prawdziwe XP wszystkim partiom AI (zapis 362, doba 109199: postoj 3%, "dzien x0.91"
wobec 11-23%, x0.96-1.01 w kolejnych dobach). Maski WSZYSTKICH partii lordow poza graczem ida do `arm_drill` (segment `T=id,maska,wiek`), odtwarzane przy
wczytaniu jak maska gracza; godziny spoza zapisu dalej = ruch. Rozdz. 14.7, uwaga 1.]**

**[MUSZTRA-jp, pytanie] Dlug, ktory zostaje po przespanej nocy.** Dzien cwiczen zabiera swit z dlugiem snu - tym samym, ktory zabiera predkosc i morale. Kto po
zarwanej nocy przespi tylko zwykle 6 godzin, ma dalej dlug (jedna zarwana noc odsypia sie 9 godzinami), wiec traci tez nastepny dzien cwiczen - i kolejny, az
raz odespi do konca. Zostawic tak (jedna prawda: niewyspany = wolniejszy, mniej morale, bez cwiczen), czy zabierac tylko dzien po samej nocy bez snu, a dlug
niech dalej boli tylko predkosc i morale?
*Moja rada: zostawic - komunikat switu mowi to teraz wprost ("No drill today - sleep the debt off before the next dawn, or tomorrow's drill is lost too."). W kodzie:
dlug o swicie (bez zmian do Twojego slowa).*

**[MUSZTRA-jp, pytanie] Czy chcesz widziec musztre w grze codziennie.** Dzis gra pokazuje tylko dzien bez cwiczen (swit z dlugiem snu, glod). Ile Twoi ludzie
nauczyli sie danego dnia i dlaczego tyle (Przywodztwo, postoj albo marsz, zapas, bron, perki), widac tylko w logu. Dodac jedna linie dziennie, np. "Drill today:
1240 XP (12 per man) - Leadership x1.12, at rest x1.5, drill kit x1.10, armed 84%, perks +3 per man" (z wylacznikiem w MCM)?
*Moja rada: tak, z wylacznikiem (domyslnie wlaczona) - wtedy wzor jest widoczny tam, gdzie grasz. W kodzie: nic (nowy komunikat dopiero po Twoim slowie).*

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

## 14. Jeden wzor (decyzja Jeffa 07:10) - projekt "MUSZTRA-j"

**Decyzje:** [J 07:10, 1] "to ma zalezec od wielu czynnikow - przywodztwa, zmeczenia itp.; jest jedna zasada, wzor dla wszystkich, pod ten wzor podpinaja sie
dane danego lorda i wychodzi, jak szybko" -> jeden wzor musztry dla gracza, lordow AI i glow rodow AI; Z14b (AI na tym samym wzorze) wlaczone domyslnie.
[J 07:10, 2] odpowiedz "b" na pytanie 11: "noc bez snu = nastepny dzien bez cwiczen", liczone od switu (niezaleznie od godziny cwiczen), gracz i AI tak samo.
Skroty: `G11\` = `SCR\noc2\grupa11\Armoury\src\` (galaz `noc/grupa11`, HEAD 30ed818 - na nim liczone linie). Logi: `SCR\kopia-grupa11\Armoury-2026-10-09_06-47-39.log`
(nowa kampania, 40 dob: 108837-108876), `...\Armoury-2026-10-09_07-00-54.log` (zapis doby 362, 8 dob: 109198-109205), `...\2026-10-09_06-47-39\budzet-rodow.csv`.
Rachunek (do powtorzenia, `python -I`): `SCR\jedenwzor\parse.py <log> [<log>]` (doby), `krol2.py <log40> <log362> <budzet-rodow.csv>` (swiat i krolestwa,
wspolczynnik wielkosci z doby 40), `w.py <log40> <log362>` (okna 21-40 i 36-40), `swit.py <log> [<log>]` (dlug o swicie wobec dni bez cwiczen).

**Dla Jeffa (do for_jeff, slowami gracza):**
1. Wszyscy - Ty, partie Twojego rodu, lordowie AI i glowy rodow AI - cwicza wedlug jednego wzoru. Baza zalezy tylko od stopnia zolnierza, a tempo daja dane
   danego lorda: jego Przywodztwo, to, czy jego ludzie stoja, maszeruja, sa glodni albo po nocy bez snu, czy maja bron swojego rodzaju, ile maja zapasu do
   cwiczen i jakie perki ma dowodca.
2. Glowy rodow AI traca premie gry "z samego tytulu" (ich ludzie cwiczyli o polowe szybciej niz u pozostalych lordow). Glowa rodu z dobrym Przywodztwem,
   ktora stoi w miescie albo obozie, dalej szkoli szybko; slaba, w ciaglym marszu - wolno.
3. Skutek dla AI (z testu 40 dob nowej kampanii i 8 dob zapisu 362) zmienia sie w czasie. W pierwszym miesiacu nowej kampanii armie AI szkola sie z musztry
   ok. 20% wolniej niz dzis w grze (doby 2-10: -22%, 11-20: -20%, 21-30: -18%), bo na starcie ROT wystawia prawie same druzyny glow rodow (ok. 80% bazy);
   potem ok. 12% wolniej (doby 31-40, glowy rodow juz ok. 45%), a w starym swiecie (zapis 362) tyle samo co dzis (+1..2%), bo maja tam duzo zapasowej broni
   do cwiczen. Druzyny glow rodow 20-30% wolniej, pozostali lordowie ok. 8% szybciej (w starym swiecie, z zapasem, wiecej).
4. Najwiecej traca krolestwa ze slabymi dowodcami (Przywodztwo ich lordow wychodzi na 0.82-0.97 wzoru wobec 1.07 swiata) - nie przez marsz: Braavos stoi
   20% dob, wiecej niz swiat (14%). Czas, w jakim rekrut dochodzi do t6 z samej musztry, rosnie tam: Braavos 1.6 -> 2.6 roku (x1.57), Ibben x1.44,
   Tyrosh x1.42, Lorath, Qarth i Lys x1.40, Riverlands x1.39, Myr x1.30, Zelazne Wyspy x1.29; prawie bez zmian Stormlands, Reach, Dorne i Nocna Straz.
   Srednio w swiecie rekrut AI do t6: ok. 2.1 roku zamiast ok. 1.9 przy pelnej broni, przy dzisiejszych brakach broni (171) ok. 3 lat zamiast ok. 2.7.
   Do tego dochodzi doswiadczenie z bitew - bez zmian. To liczby z 40 dob jednej kampanii; dokladne udzialy glow rodow na krolestwo poda nowa linia (14.6).
5. Twoja druzyna - bez zmian (juz cwiczyla od bazy 10 + 2 x tier, czyli tak, jak w tabeli, ktora zatwierdziles). Zmienia sie tylko u lordow AI, ktorzy sa
   glowami swoich rodow i ida w Twojej armii: ich ludzie cwicza teraz od 10 + 2 x tier zamiast 15 + 3 x tier (o 1/3 wolniej, jak wszystkie glowy rodow).
   Wyjatki od wzoru: Inni (umarli nie spia i nie znaja zmeczenia - cwicza jak w grze) oraz partia poza mapa (niewola, rejs, sluzba ROT) - nie cwiczy wcale,
   jak dotad Twoje partie, teraz takze lordowie AI.
6. Noc bez snu = nastepny dzien bez cwiczen, liczony od switu do switu - niezaleznie od tego, o ktorej godzinie Twoi ludzie cwicza; u AI tak samo. Jesli do
   nastepnego switu nie odespicie dlugu (ten sam dlug, ktory zabiera predkosc i morale; jedna zarwana noc odsypia sie 9 godzinami snu), nastepny dzien
   tez przepada. Mowi to komunikat switu ("No drill today - sleep the debt off before the next dawn, or tomorrow's drill is lost too."), a po odespaniu
   w ciagu dnia - "Drill resumes at the next dawn." (MUSZTRA-jp; pytanie, czy tak zostawic - rozdz. 11).

### 14.1 Wzor i skad kto bierze dane

**XP na czlowieka na dobe = (B x L x D x S + P) x A** dla kazdego oddzialu (bez bohaterow, z kultura, nie Inni) kazdej partii lorda poza zalogami;
**XP = 0** (takze perki, zapas sie nie zuzywa) w dobie, ktora zaczela sie od switu z dlugiem snu (>= 1), i w dobie glodu.

| Czlon | Wartosc | Gracz (i Z14a: rod gracza, lordowie w armii gracza) | Lord AI i glowa rodu AI | Kod |
|---|---|---|---|---|
| B baza | 10 + 2 x tier oddzialu; w bitwie 0 (jak w grze) | tak samo | tak samo - **bez 15 + 3 x tier glowy rodu** | nowe `Drill.RuleBase` (dzis `BaseOf`, `G11\Drill.cs:331-336`) |
| L dowodca | Przywodztwo przywodcy partii / 170, granice 0.5-1.5; bez przywodcy 0.5 | Twoje Przywodztwo (partie rodu i armii - ich dowodcy) | Przywodztwo tego lorda; w armii AI kazda partia swoj dowodca (jak dzis) | `G11\Drill.cs:355-356` |
| D dzien ("zmeczenie") | 1.5 postoj (ruch < 4 h z 24), 0.9 marsz, 0 glod albo dlug snu o ostatnim swicie | maska godzin `Drill.RestHour`; glod `Party.IsStarving`; dlug o swicie - NOWE pole `NightRest.DawnDebt` | to samo; dlug o swicie `AiSleep.DawnDebt` (ksiega T10, `G11\NightMarch.cs:66`, ustawiany o swicie `:893`) | `G11\Drill.cs:357-364`; jedno wejscie NOWE `NightRest.DawnDebtOf` |
| S zapas | 1 + 0.10 x uB x kB + 0.10 x uZ x kZ (1.00-1.30) | od Ciebie + nadwyzka ludzi w zbrojowni DTE (`StockOn`) | nadwyzka zbrojowni (`DrillStockAi`) | `G11\Drill.cs:365-373` |
| P perki treningu | wynik modelu gry/BK minus baza gry (Combat Tips, Raise the Meek, Drills, Bow Trainer ...) | gra nie daje Ci bazy - P = caly wynik | wynik modelu minus jego baza (15 + 3 x tier albo 10 + 2 x tier) | NOWE `Drill.GameBase` - baza gry sluzy juz tylko do wydzielenia P |
| k perki kwatermistrza | Giving Hands (bron), Paid in Promise (zbroja): czesc S x1.5 | tak | tak | `G11\Drill.cs:370-373` |
| A bron ludzi | udzial uzbrojonych 171 (bron swojego typu o tierze >= t-1) | `DrillNeedsArmsPlayer` (zbrojownia DTE) | `ArmsDrill.Gated` (`AiGear.On && PartyDrillNeedsArms`) | `G11\ArmsDrill.cs:255-270` |

"Zmeczenie" w slowach Jeffa to D: marsz (x0.9) i noc bez snu (0) - z tej samej ksiegi snu, ktora zabiera predkosc i morale (gracz `NightRest`, AI ksiega T10).
Nowych czynnikow nie dodaje (CLAUDE.md 8.3). Liczby (0.5-1.5, 1.5/0.9, +10%/+10%, x1.5) bez zmian - tabela zatwierdzona [J 04:00, 2].

### 14.2 Co znika, co zostaje

- **Baza glowy rodu 15 + 3 x tier** (gra `G\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyTrainingModel.cs:23-26`, BK
  `BK\BannerKings.Models.Vanilla\BKPartyTrainningModel.cs:43-46`) - znika takze u AI. 15 + 3 x tier = dokladnie 1.5 x (10 + 2 x tier), wiec druzyna glowy rodu
  traci 1/3 bazy, a o jej tempie decyduje teraz L x D x S jak u innych.
- **Osobne sciezki** - dzis `Drill.Shape` ma dwie galezie (`G11\Drill.cs:507-518`): Z14a liczy B x L x D x S + P od 10 + 2 x tier, AI liczy od bazy, ktora dal
  model gry albo BK (B_gry x [L x D przy Z14b] x S + P). Po zmianie jedna galaz dla wszystkich; to, ktory model (gra czy BK) i z jaka baza policzyl trening,
  przestaje miec znaczenie - oba daja tylko perki P. Wylacznik `DrillLawAi` zostaje jako cofniecie: wylaczony = plaski trening gry z baza gry, jak dzis.
- **Z14b domyslnie wlaczone** (`DrillLawAi = true`). "Prog wlaczenia Z14b" (rozdz. 10: 0.95-1.05 swiat, >= 0.95 krolestwa) i autotest B przestaja byc bramka -
  decyzje podjal Jeff; zostaja pomiarem skutku (14.3, 14.6). Kara glodu i snu AI byla juz domyslnie (`DrillPenaltyAi`); `DrillLawAi` ja i tak wlacza.
- **Pytanie 11 (baza Twojej druzyny)** - zamkniete: 10 + 2 x tier dla wszystkich. Twoj wynik sie nie zmienia.
- **Dlug snu w chwili cwiczen** (`NightRest.DebtOf`, `G11\NightRest.cs:1119-1125`, czyta tylko musztra) -> **dlug o ostatnim swicie** (`NightRest.DawnDebtOf`).
  Predkosc i morale dalej z dlugu biezacego (`DebtFor`): splata zdejmuje kare marszu od razu, ale dzien cwiczen jest juz stracony - o dniu cwiczen decyduje swit
  (decyzja Jeffa), o kolumnie - biezacy stan. Ta sama ksiega, ten sam dlug; rozni sie tylko chwila odczytu.
- Zostaje bez zmian: liczby wzoru, zapas i jego zuzycie (u AI teraz x D - patrz 14.4 `Wear`), 171, Z1, kowale i zlom, zalogi, bandy, karawany.

### 14.3 Skutek dla AI - liczby z logow `kopia-grupa11`

**Metoda.** Log nie rozdziela glow rodow, ale da sie je policzyc: linia "Musztra AI" podaje baze gry i ludzi (baza gry na glowe), linia krolestw - sredni
tier (wazony ludzmi). Gdyby wszyscy mieli 10 + 2 x tier, baza na glowe wynosilaby 10 + 2 x sredni tier; nadwyzka to glowy rodow (x1.5). Stad
r = (10 + 2 x tier) / (baza gry na glowe) = baza wzoru / baza gry, a udzial glow rodow w ludziach (wazony baza) m = 2 x (1/r - 1). Nowy XP wobec gry =
r x "razem" (L x D x S, z zerami w dni kary, wazone baza gry); grupa11 wobec gry = "po czynnej regule" / "baza gry"; A (171) mnozy wszystkie trzy tak samo.
Obciazenie [S]: ludzie w bitwie w chwili treningu maja baze gry 0 w liczniku, a wchodza do 10 + 2 x tier - r jest zawyzone o udzial ludzi w bitwie
(ok. 1-3%), czyli skutek moze byc o tyle silniejszy; wagi "razem" sa wagami bazy gry (glowy rodow z waga 1.5) - jesli glowy rodow maja wyzsze Przywodztwo,
nowa srednia L wyjdzie troche nizsza. Oba obciazenia zmierzy wprost nowa linia (14.6).

**Swiat** [P z logow, r i m - rachunek]:

| Okno | baza gry / glowe | 10 + 2 x tier | r | glowy rodow (m) | L x D x S "razem" | grupa11 / gra | **wzor / gra** | **wzor / grupa11** | XP na glowe na dobe przed bronia: gra / grupa11 / wzor | A (171, sr. partii) | po broni: gra / grupa11 / wzor |
|---|---|---|---|---|---|---|---|---|---|---|---|
| nowa kampania, doby 11-40 | 18.77 | 14.73 | 0.785 | 55% | x1.062 | x1.015 | **x0.83** | **x0.82** | 18.8 / 19.1 / 15.6 | 0.69-0.70 | 13.0 / 13.2 / 10.8 |
| nowa kampania, doby 36-40 | 18.17 | 14.77 | 0.813 | 46% | x1.082 | x1.014 | **x0.88** | **x0.87** | 18.2 / 18.4 / 16.0 | 0.70 | 12.7 / 12.9 / 11.2 |
| zapis 362, doby 2-8 | 19.54 | 16.28 | 0.833 | 40% | x1.215 | x1.140 | **x1.01** | **x0.89** | 19.5 / 22.3 / 19.8 | 0.84-0.88 | 16.6 / 18.9 / 16.8 |

Udzial glow rodow spada w nowej kampanii (doba 4: 82%, doba 19: 64%, doba 39: 45%) - na starcie ROT wystawia prawie same druzyny glow rodow, potem dochodza
partie pozostalych lordow; stan ustalony to okno 36-40 i zapis 362 (40-46%). Dla porownania: rodow z wojskiem w polu 248, partii lordow AI 666-687 (37% partii
to co najwyzej glowy rodow; ich druzyny sa wieksze - limit druzyny glowy rodu +25 na szczebel rodu zamiast +15, `DefaultPartySizeLimitModel.cs:45-47`).
Zapas (S) w starym swiecie x1.16 (duza nadwyzka zbrojowni) - dlatego tam wzor wobec gry wychodzi na zero.

**Przebieg w czasie** [MUSZTRA-jp, uwaga 9 recenzji; ta sama metoda, `SCR\jedenwzor2\okna.py <log40> <log362>`, `python -I`]:

| Okno | r | glowy rodow (m) | L x D x S "razem" | grupa11 / gra | **wzor / gra** | wzor / grupa11 |
|---|---|---|---|---|---|---|
| nowa kampania, doby 2-10 | 0.712 | 81% | x1.089 | x1.016 | **x0.78** | x0.76 |
| nowa kampania, doby 11-20 | 0.746 | 68% | x1.068 | x1.017 | **x0.80** | x0.78 |
| nowa kampania, doby 21-30 | 0.788 | 54% | x1.045 | x1.013 | **x0.82** | x0.81 |
| nowa kampania, doby 31-40 | 0.809 | 47% | x1.072 | x1.016 | **x0.87** | x0.85 |
| zapis 362, doby 3-8 (bez pierwszej doby po wczytaniu) | 0.831 | 41% | x1.227 | x1.137 | **x1.02** | x0.90 |

Pierwszy miesiac nowej kampanii: -18..-22% wobec gry (nie -12% - to stan po ok. 35 dobach). Pierwsza doba po wczytaniu zapisu 362 (109199) miala postoj 3%
i "razem x1.14" wobec x1.19-1.26 w kolejnych dobach - maski godzin ruchu AI nie szly do zapisu (cala doba jako marsz); od MUSZTRA-jp ida (uwaga 1), wiec
"x1.01" z tabeli wyzej to x1.02 bez tej doby.

**Tempo awansu t1 -> t6** (14 250 XP; doby kalendarza 364-dniowego; tylko musztra, bez bitew i perkow; przed udzialem broni A - z A dzielic przez 0.70 w nowej
kampanii, 0.85 w starym swiecie):

| Partia | gra (dzis w grze) | grupa11 (S, kara) nowa / zapis 362 | jeden wzor nowa (doby 36-40) / zapis 362 |
|---|---|---|---|
| druzyna glowy rodu AI | 556 (1.5 roku) | 548 / 487 | 770 (2.1 roku) / 686 (1.9 roku) |
| inna partia lorda AI | 833 (2.3 roku) | 822 / 731 | 770 / 686 |
| srednio (przy sredniej predkosci swiata) | 677 / 694 | 668 / 609 | 770 / 686 |
| srednio z udzialem broni A | 967 (2.7 roku) / 816 | 954 / 716 | 1 100 (3.0 roku) / 807 |

Czyli: glowy rodow ok. 28% mniej XP (nowa kampania; 19% wobec gry w starym swiecie), pozostali lordowie ok. 8% wiecej (21% wobec gry w starym swiecie -
zapas), swiat -12% / +1% wobec gry i -13% / -11% wobec grupa11.

**Wedlug krolestw** - nowa kampania, czynniki: srednia 6 linii krolestw (doby 108846-108875), partie, ludzie, tier i t3+ z doby 40 [P]; udzial glow rodow na
krolestwo [S] = (rodow z wojskiem w polu w krolestwie / partii) x 1.21 (wspolczynnik wielkosci druzyny glowy rodu dopasowany do swiata w dobie 40: m 0.45 przy
udziale liczby 0.372), gorna granica 0.95. "L x D" - dni bez kary, bez zapasu (= "PROG Z14b" 4 h); "wzor/gra" = r x L x D (z karami) x S; "wzor/grupa11" =
r x L x D (dni bez kary). Krolestwa z 1-2 partiami (Yi Ti Exiles, Skagos, Summer Isles, bez krolestwa) pominiete.

| Krolestwo | partii | ludzi | rodow z wojskiem | glowy rodow [S] | L x D [P] | S [P] | **wzor / gra** [S] | **wzor / grupa11** [S] | t1->t6 srednio: gra -> wzor (doby, przed A) | tier / t3+ |
|---|---|---|---|---|---|---|---|---|---|---|
| The North | 56 | 10 666 | 22 | 0.48 | 1.07 | 1.07 | 0.92 | 0.86 | 673 -> 731 | 2.21 / 31% |
| House Baratheon of King's Landing | 58 | 9 218 | 25 | 0.52 | 1.06 | 1.06 | 0.82 | 0.84 | 660 -> 809 | 2.22 / 29% |
| The Reach | 49 | 8 030 | 16 | 0.40 | 1.14 | 1.00 | 0.95 | 0.95 | 695 -> 730 | 2.30 / 33% |
| Dorne | 53 | 7 038 | 15 | 0.34 | 1.08 | 1.07 | 0.99 | 0.92 | 711 -> 721 | 2.50 / 40% |
| The Vale | 37 | 6 401 | 12 | 0.39 | 1.10 | 1.00 | 0.92 | 0.92 | 696 -> 756 | 2.26 / 33% |
| Stormlands | 38 | 5 899 | 13 | 0.42 | 1.18 | 1.07 | 1.05 | 0.98 | 690 -> 657 | 2.43 / 37% |
| Iron Islands | 33 | 4 921 | 10 | 0.37 | 0.89 | 1.03 | 0.77 | 0.75 | 704 -> 910 | 2.46 / 41% |
| Riverlands | 14 | 4 391 | 14 | 0.95 (granica) | 1.06 | 1.00 | 0.72 | 0.72 | 565 -> 786 | 2.34 / 35% |
| Dragonstone | 28 | 4 075 | 10 | 0.43 | 1.10 | 1.02 | 0.89 | 0.90 | 685 -> 766 | 2.61 / 46% |
| House Targaryen | 23 | 3 291 | 8 | 0.42 | 1.12 | 1.02 | 0.93 | 0.92 | 688 -> 736 | 3.55 / 65% |
| Nights Watch | 20 | 3 221 | 5 | 0.30 | 1.09 | 1.01 | 0.95 | 0.94 | 724 -> 759 | 2.22 / 34% |
| Braavos | 23 | 3 146 | 15 | 0.79 | 0.83 | 1.07 | 0.64 | 0.60 | 597 -> 939 | 2.40 / 39% |
| Volantis | 19 | 3 078 | 6 | 0.38 | 1.06 | 1.01 | 0.90 | 0.89 | 699 -> 777 | 2.52 / 45% |
| Sarnor | 24 | 2 890 | 7 | 0.35 | 1.04 | 1.00 | 0.89 | 0.89 | 708 -> 800 | 2.18 / 30% |
| Dothraki Horde | 18 | 2 531 | 7 | 0.47 | 1.04 | 1.01 | 0.85 | 0.84 | 674 -> 791 | 2.40 / 38% |
| Qohor | 17 | 2 519 | 6 | 0.43 | 1.03 | 1.01 | 0.85 | 0.85 | 686 -> 808 | 2.29 / 35% |
| Norvos | 18 | 2 483 | 5 | 0.34 | 1.03 | 1.00 | 0.89 | 0.89 | 713 -> 804 | 2.37 / 35% |
| Ibben | 12 | 2 307 | 5 | 0.51 | 0.87 | 1.00 | 0.69 | 0.70 | 665 -> 958 | 2.19 / 31% |
| Myr | 17 | 2 281 | 5 | 0.36 | 0.90 | 1.00 | 0.77 | 0.77 | 707 -> 922 | 2.47 / 43% |
| Pentos | 17 | 2 248 | 5 | 0.36 | 1.04 | 1.00 | 0.88 | 0.88 | 707 -> 804 | 2.53 / 43% |
| House Targaryen, Aegon | 12 | 2 129 | 5 | 0.51 | 1.06 | 1.00 | 0.85 | 0.85 | 665 -> 785 | 2.32 / 35% |
| Lorath | 18 | 2 092 | 6 | 0.40 | 0.86 | 1.00 | 0.71 | 0.71 | 693 -> 971 | 2.65 / 46% |
| Qarth | 12 | 2 003 | 4 | 0.40 | 0.86 | 1.01 | 0.71 | 0.71 | 693 -> 973 | 2.17 / 32% |
| Free Folk | 20 | 1 913 | 7 | 0.42 | 1.12 | 1.00 | 0.90 | 0.93 | 687 -> 763 | 2.33 / 35% |
| Tyrosh | 13 | 1 636 | 5 | 0.47 | 0.87 | 1.02 | 0.70 | 0.71 | 676 -> 963 | 2.48 / 44% |
| Lys | 12 | 1 538 | 5 | 0.51 | 0.89 | 1.01 | 0.71 | 0.71 | 665 -> 933 | 2.54 / 44% |

Zapis 362 (2 linie krolestw, doby 109200 i 109205; L x D dni bez kary / S) [P] - wzor / grupa11 ~ L x D x r swiata (0.833): slabe - Iron Islands 0.88/1.15 (~0.73),
Tyrosh 0.85/1.13, Myr 0.84/1.19, Ibben 0.83/1.20, Lorath 0.83/1.15, Confederation of Antaryon (Braavos Rebels) 0.82/1.17, Principality of Saldorys 0.86/1.12,
Sarnor 0.91/1.08 (~0.68-0.76); mocne - House Targaryen 1.33/1.16 (~1.11), Mopatis League 1.29/1.22, Dragonstone 1.25/1.14, Stormlands 1.21/1.21,
Baratheon 1.16/1.16 (~0.97-1.07); The North 1.09/1.12, Dorne 1.07/1.21, The Reach 1.09/1.19, Riverlands 1.02/1.20 (~0.85-0.91).
**Wzor sie powtarza w obu biegach:** wolne miasta Essos i Zelazne Wyspy maja dowodcow ze slabym Przywodztwem i stoja malo (L x D 0.83-0.91) - traca
25-35%; wielkie domy Westeros z dobrymi dowodcami (Stormlands, Dragonstone, Targaryen) - prawie nic albo zyskuja. Wieksze straty tam, gdzie prawie kazda partia
to glowa rodu (Riverlands w nowej kampanii: 14 rodow, 14 partii; Braavos: 15 rodow, 23 partie) - te liczby to szacunek [S], dokladne poda nowa linia krolestw.

**Przyczyna i czas do t6 jako mnoznik** [MUSZTRA-jp, uwaga 9; `SCR\jedenwzor2\krolL.py <log40> 108846`: D z godzin ruchu (postoj = 0 i 1-3 h), L = (L x D) / D,
szacunek [S]]: najslabsze krolestwa maja slabych dowodcow, nie wiecej marszu - Braavos L ~0.82 przy postoju 20% dob (swiat 14%), Lorath 0.86 (17%), Tyrosh 0.87
(17%), Iron Islands 0.92 (12%), Qarth 0.93 (3%), Ibben 0.93 (7%), Myr 0.96 (7%), Lys 0.97 (4%); mocne - Stormlands 1.21, The Reach 1.18, The Vale 1.17. Czas t1 -> t6
z samej musztry (kolumna "gra -> wzor" wyzej) jako mnoznik: Braavos x1.57 (597 -> 939 dob, 1.6 -> 2.6 roku), Ibben x1.44, Tyrosh x1.42, Lorath x1.40, Qarth x1.40,
Lys x1.40, Riverlands x1.39, Myr x1.30, Iron Islands x1.29; Stormlands x0.95, The Reach x1.05, Dorne x1.01. Lista krolestw ponizej x0.75 i prawdziwy udzial glow
rodow - po biegu A1 (prog J2, 14.6).

**Sen od switu (AI)** [P, `swit.py`]: o swicie z dlugiem jest srednio 1.3% partii lordow AI (nowa kampania doby 11-40: 225 partio-dni na ok. 17 000; zapis 362:
1.3%); z tych 225 w ciagu dnia odespalo 93 (41%) - dzis czesc z nich jeszcze cwiczyla (gdy ich trening wypadl po splacie), od switu - nie. Gorna granica
dodatkowej straty: 93 / 17 000 = 0.55% partio-dni, realnie ok. 0.3% XP AI - pomijalne. Przypadki "dlug 1 dalej 1" (przespali baze, nie splacili odsetek; 37
partio-dni) traca drugi dzien tak samo jak dzis (dlug trwa caly dzien).

### 14.4 Zmiany w kodzie (plik:linia w `G11\`, HEAD 30ed818)

| Miejsce | Zmiana |
|---|---|
| `Drill.cs:23-47` naglowek | B = 10 + 2 x tier dla wszystkich (bez bazy glowy rodu gry i BK); dlug snu o ostatnim swicie; Z14b domyslnie wlaczone; baza gry tylko do P i pomiaru |
| `Drill.cs:331-336` `BaseOf` | -> `RuleBase(ch)` = 10 + 2 x tier (bez galezi glowy rodu) oraz `GameBase(mp, ch)` = predykat gry (`DefaultPartyTrainingModel.cs:21-29` = BK `:41-49`): 0 przy `NoGameBase` albo `MapEvent`; 15 + 3 x tier, gdy `LeaderHero == ActualClan.Leader`; inaczej 10 + 2 x tier - tylko do wydzielenia P, do linii "baza gry" i do wylaczonego `DrillLawAi` |
| `Drill.cs:338-344` `SleepDebt` | wola `NightRest.DawnDebtOf(mp)` zamiast `NightRest.DebtOf(mp)` |
| `Drill.cs:82-90` `Ctx`, `:346-378` `CtxOf` | nowe pola tylko do linii: `DebtNow` (dlug biezacy - kontrola), `ClanHead` (`LeaderHero == ActualClan.Leader`); `c.Debt` = dlug o swicie |
| `Drill.cs:490-525` `Shape` | jedna galaz zamiast dwoch (`:507-518`): `gb = GameBase`; `P = game >= gb - 0.01 ? game - gb : 0` (model nie dal bazy - wyjatek BK: P = 0, licznik "model bez bazy"); `law = noBase ? s.DrillLaw : s.DrillLawAi`; `B = mapEvent ? 0 : (law ? RuleBase : gb)`; `pre = c.Off ? 0 : B x (law ? L x D : 1) x S + P`. Z14a - wynik bez zmian (gb = 0 -> P = caly wynik) |
| `Drill.cs:529-600` `Done` | liczniki AI: baza gry (`_aGame`, jak dzis), baza wzoru, baza gry glow rodow, perki P, XP gry po broni (`_eGame x share` = "dzis w grze"), XP wzoru po broni (`final`), ludzio-dni; to samo w `KAcc` (`:133-138`) na krolestwo; sen: partie z dlugiem o swicie, w tym splacone przed treningiem (`DebtNow == 0`), "dlug teraz > dlug o swicie" (kontrola, ma byc 0) |
| `Drill.cs:684-690` `Wear` | bez zmiany kodu: `law = c.NoBase || s.DrillLawAi` - przy domyslnym Z14b zuzycie zapasu AI x D (1.5 / 0.9) zamiast plaskiego 1 |
| `Drill.cs:999-1029` `SessionStart` (linia startowa) | "jeden wzor: baza 10 + 2 x tier dla wszystkich (baza glowy rodu gry/BK 15 + 3 x tier wylaczona), Z14b TAK; niewyspanie: dlug o swicie (gracz NightRest, AI ksiega T10)" |
| `Drill.cs:1302-1330` `PlayerLine` (`:1317`) | "DLUG SNU O SWICIE n (teraz m) - bez cwiczen" |
| `Drill.cs:1348-1381` `AiLine` | nowe pola (14.6); segment "PROG Z14b" bez zmian (porownywalnosc z `kopia-grupa11`) |
| `Drill.cs:1384-1428` `KingdomLine` | na krolestwo: XP na glowe na dobe po broni gra -> wzor (x), udzial glow rodow w bazie gry, t1 -> t6 przy tym tempie (doby) |
| `ArmsDrill.cs:254` komentarz | "1 - AI z baza gry" -> "1 - AI wedlug jednego wzoru (albo bazy gry przy wylaczonym DrillLawAi)" |
| `Settings.cs:850` | `DrillLawAi = true`; opis (ang.): AI lords' parties drill by the same rule as yours - base 10 + 2 x tier for every lord, clan leaders included (the game gives a clan leader's men 15 + 3 x tier), the commander's Leadership, rest or march, no drill after a night without sleep or when starving; off = the game's flat daily training |
| `Settings.cs:845, 849` | opisy `DrillLaw`, `DrillPenaltyAi`: "short of sleep" -> "after a night without sleep (sleep debt at dawn - no drill until the next dawn)" |
| `python tools/gen_mcm.py` | `McmSettings.cs:2945` (domyslnie true) i opisy |
| `NightRest.cs:37` | NOWE `internal static int DawnDebt;` - dlug gracza zaraz po swicie |
| `NightRest.cs:1039-1110` `SettleNight` | na kazdej sciezce (sluzba ROT -> 0, przespana baza -> dlug bez zmian, dlug +1) na koncu `DawnDebt = Debt` (try/finally); `:98` (Inni) -> `DawnDebt = 0` |
| `NightRest.cs:1119-1125` `DebtOf` | -> `DawnDebtOf(mp)` (czyta tylko musztra): `NightRestEnabled`; gracz `DawnDebt`; AI `AiDebtLive(s) && _ai[mp]` -> `DawnDebt`. Slownik `_ai` tylko z watku glownego (trening partii jest na glownym) - nie wolac z predkosci (rownolegle; ta dalej `DebtFor`), komentarz w kodzie |
| `NightRest.cs:1616-1638` `Export` / `Import` | piate pole `DawnDebt` w napisie "arm_nightrest" (juz przez `SaveText.Sync`, `ArmouryBehavior.cs:423-425`); stary zapis bez pola -> `DawnDebt = Debt` |
| `NightMarch.cs:1346-1384` `ExportAi`, `:1430-1445` (wpis z zapisu) | szoste pole wpisu `DawnDebt`; do zapisu takze wpis z samym `DawnDebt > 0`; format "v1" zostaje (stary DLL czyta pola 0-4, nowy pole 5, jesli jest); stary zapis -> `DawnDebt = Debt` (jak dzis `:1441`); klucz "arm_nightrest_ai" juz przez `SaveText.Sync` (`ArmouryBehavior.cs:427-429`). **Nowego napisu w SyncData nie ma.** |
| `NightMarch.cs:1053-1065` `LogPlayerDawn` | "; dlug o swicie (musztra) n" |
| `NightMarch.cs:865-895` `SettleAi` | bez zmian (`e.DawnDebt = e.Debt` jest w `:893`) |
| `CHANGELOG.md` | wpis MUSZTRA-j, status NIEWGRANE; commit lokalny, bez pushu |

### 14.5 Kontrola calosci (CLAUDE.md 8.0)

- **Regresje:** Twoj wynik bez zmian poza dniem kary (dlug o swicie zamiast w chwili treningu); Z14a tak samo; AI - nowy wynik (cel zmiany). "Cwiczenia (171)":
  mnoznik sredni to udzial, nie zalezy od bazy - T3 dalej obowiazuje; "doswiadczenia mniej przez brak broni" spadnie razem z XP AI.
- **Kolizje:** 171 - ten sam postfiks, kolejnosc Shape -> A bez zmian. BK - ten sam predykat co gra (`BKPartyTrainningModel.cs:41-49`), postfiks tylko na
  zewnetrznym modelu (`_tDepth`, log startowy "modele szkolenia 3, czynny BK"); `TryCatch` BK gubiacy baze - P = 0, baza ze wzoru, licznik w linii. T10 - musztra
  czyta dlug o swicie z tej samej ksiegi; predkosc i morale - dlug biezacy (celowo: decyzja Jeffa). Zapas - zuzycie AI teraz x D (srednio ok. x0.95-1.05 przy
  postoju 4-23%) - linia "zuzyto" i prog T7.
- **Spojnosc:** jedna regula dla wszystkich partii lordow; jeden dlug snu z jednej ksiegi; "nic z niczego" bez zmian (XP nie jest towarem, zapas jak dzis).
- **Zapis:** zadnego nowego klucza i napisu; dwa istniejace napisy (juz przez `SaveText.Sync`) dostaja po polu; zgodne w obie strony (stary DLL ignoruje pole).
- **MCM:** `Armoury.json` Jeffa nie ma zadnego klucza `Drill*` (sprawdzone 09.10: grep 0 trafien) - domyslne `DrillLawAi = true` z kodu zadziala bez edycji pliku
  (pulapka wpisu 82 nie dotyczy).
- **Krawedzie:** (a) kto po nocy bez snu nie odespi dlugu przed nastepnym switem (dlug 1 = 9 h odpoczynku, `NightRest.cs:79`; AI-dluznicy obozuja od 20:00),
  traci tez nastepny dzien - to ten sam dlug, ktory dalej zabiera predkosc i morale; przy odespaniu przed switem Jeffowe "noc bez snu = nastepny dzien"
  spelnia sie dokladnie. Wariant "znacznik nocy" (dzien przepada tylko po nocy bez snu, nawet przy niesplaconym dlugu) odrzucony: musztra rozjechalaby sie
  z kara predkosci i morale (dwie prawdy o jednej partii). MUSZTRA-jp: komunikat switu mowi to wprost, a wybor wariantu jest pytaniem do Jeffa (rozdz. 11). (b) Tick treningu przesuniety przez gre przez swit (`CampaignPeriodicEventManager`) moze dac 0 albo 2
  treningi w jednym oknie swit-swit - rzadkie i tak samo jak dzis. (c) Wczytanie nie zdejmuje dlugu o swicie (pole w zapisie) - jak "wczytanie nie daje postoju".
- **Gra:** armie AI srednio wolniej (pierwszy miesiac nowej kampanii -18..-22%, po ok. 35 dobach -12%, w starym swiecie 0..+2%), krolestwa ze slabymi dowodcami
  (wolne miasta Essos, Zelazne Wyspy, Riverlands) czas do t6 x1.29-1.57 - skutek decyzji Jeffa, pokazany, nie bramka; autotest B pokaze go w tierach.
- **Wyjatki od wzoru (MUSZTRA-jp, nazwane):** (1) Inni (umarli) - trening gry, z premia glowy rodu (nie spia i nie znaja zmeczenia, `Undead.cs`; wzor ma
  czynniki snu i glodu). (2) Partia nieaktywna (poza mapa: niewola, rejs BK, sluzba ROT) przy wzorze - nie cwiczy (Z14a jak dotad; AI przy Z14b od MUSZTRA-jp,
  wczesniej caly wzor z zamrozonej maski); gra daje perki tylko partii aktywnej (`DefaultPartyTrainingModel.cs:31-87`). [S] najwyzej ok. 2-4% partii lordow AI
  w danej chwili. (3) No Rest for the Wicked (`DefaultPartyTrainingModel.cs:88-91`: +20% dla oddzialow kultury bandyckiej, `Culture.IsBandit`) - mnoznik
  wyniku modelu, wiec P = wynik - baza gry = 1.2 x perki + 0.2 x baza gry: u glowy rodu AI zostaje okruch jej premii (0.2 x (15 + 3 x tier)), a +20% nie
  mnozy B x L x D x S; u Ciebie (baza gry 0) dziala tylko na perki - jak w grze. Znikome (perk Roguery u dowodcy
  i bandyccy zolnierze); poprawka (P / (1 + f), wynik x (1 + f)) tylko na slowo Jeffa. (4) Zalogi, bandy, karawany - poza musztra (jak dotad).
- **Skutek drugiego rzedu (niezmierzony):** wolniejsze awanse AI = mniej zlota znikajacego na koszt awansu (`PartyUpgraderCampaignBehavior.cs:144-149`:
  `GiveGoldAction` do nikogo) - lordowie zatrzymuja wiecej zlota, mniejszy popyt AiGear na bron wyzszych tierow. "Nic z niczego" nie lamie (zloto znika jak dotad,
  tylko mniej); pomiar w biegu B (14.6).
- **Zuzycie zapasu AI x D:** w zapisie 362 grupa11 zuzycie AI roslo w 6 dob do 247 szt./dobe (109203; prog T7 <= 300) przy dw = 1; teraz dw = D (1.5 postoj /
  0.9 marsz), a partie z zapasem moga czesciej stac w miastach - T7 sprawdzic w A3 (linia "zuzyto", doby 4-8, wobec `kopia-grupa11`). Powyzej 300 - opisac
  Jeffowi jako skutek jednego wzoru (wiecej cwiczen = wiecej zuzycia), bez zmiany "przy okazji".

### 14.6 Plan testu (po wykonaniu MUSZTRA-j; teraz nic nie uruchamiane)

Biegi jak w rozdz. 10 na `noc/grupa11` + MUSZTRA-j: A1 nowa kampania 40 dob (porownanie z `kopia-grupa11\Armoury-2026-10-09_06-47-39.log`), A3 zapis 362
8 dob (z `...\Armoury-2026-10-09_07-00-54.log`), A2 wczytanie zapisu z A1, A5 bieg z wojskiem gracza (do przygotowania), B 2 x 120 dob z tego samego zapisu.
Porownanie "przed / po" w tej samej linii (gra -> wzor) - dwie nowe kampanie roznia sie losem, wiec liczby miedzy biegami tylko orientacyjnie.

Linie logu (format; liczby przykladowe z doby 40 `kopia-grupa11`, przeliczone recznie):
- `Musztra: start - ...; jeden wzor: baza 10 + 2 x tier dla wszystkich (baza glowy rodu gry/BK 15 + 3 x tier wylaczona), Z14b (Drill Law Ai) TAK; niewyspanie: dlug o swicie (gracz NightRest TAK, AI ksiega T10 TAK); ...`
- `Musztra AI: dzien N - partii 687, ludzi 103654; baza gry 1842012 XP (glowy rodow 46%), baza wzoru 1532000 (x0.83), perki P ...; dowodca x1.07 (...), dzien x0.98 (postoj 16%, marsz 82%, glod 0%, sen 2% - etykieta bez zmian, MUSZTRA-jp), ...; XP po broni (171): gra 1290000 -> wzor 1135000 (x0.88), na glowe na dobe gra 12.4 -> wzor 10.9; model bez bazy 0; sen od switu: partii 13 (w tym splacone przed treningiem 3 - dawniej moglyby cwiczyc), dlug teraz > dlug o swicie 0; PROG Z14b ... (bez zmian); ...`
- co 5 dob, na krolestwo: `The North: partii 56, ludzi 10666, ..., XP na glowe na dobe (po broni) gra 12.5 -> wzor 11.5 (x0.92), glowy rodow 48% bazy gry, t1->t6 przy tym tempie gra 961 -> wzor 1044 dob, sredni tier 2.21, t3+ 31%, konni 4%`
- gracz: `Musztra (gracz): dzien N - ... x dzien 0.00 (DLUG SNU O SWICIE 1, teraz 0 - bez cwiczen) ...` oraz `NocnyMarsz: gracz o swicie - ... dlug przed 0, po 1 - wzor ...: zgodny; dlug o swicie (musztra) 1.`
- wczytanie: `NocnyMarsz: wczytano ksiege snu AI - wpisow N (z dlugiem 1/2/3 ..., z dlugiem o swicie M; stary zapis bez pola: dlug o swicie = dlug) ...`

| Prog | Bieg | Warunek |
|---|---|---|
| J1 wzor swiata | A1, A3 | "baza wzoru / baza gry": A1 doby 36-40 0.78-0.85 (z logow 0.813 [S], obciazenie bitwa -1..-3%), A3 0.80-0.87 (0.833); glowy rodow 35-55% bazy gry; "XP po broni wzor / gra": A1 doby 36-40 0.83-0.93 (0.88), A3 0.96-1.06 (1.01). Poza zakresem = blad rachunku albo kodu - szukac przed wgraniem |
| J2 krolestwa | A1 | linia co 5 dob z nowymi polami dla >= 95% partii; "wzor / gra" krolestw w granicach +-0.10 od tabeli 14.3 (udzial glow rodow w tabeli to szacunek); lista krolestw ponizej x0.75 - do raportu dla Jeffa |
| J3 tempo awansu | B (`DrillLawAi` false / true, ten sam zapis startowy) | raport, nie bramka: sredni tier i t3+ krolestw w dobie 120 wobec biegu bez Z14b; oczekiwane: swiat t3+ nizej o kilka punktow, najbardziej Essos i Zelazne Wyspy |
| J4 sen od switu - AI | A1, A3 | "sen od switu: partii N" = suma "z dlugiem teraz 1/2/3" z linii "NocnyMarsz: swit dnia" tej doby (+- partie bez treningu: bitwa, nieaktywne); "dlug teraz > dlug o swicie" = 0 w kazdej dobie; "splacone przed treningiem" <= "dlug 1 z poprzedniego switu: splacony" nastepnego switu; udzial dni kary snu 1-3% wagi (dzis 0-3%) |
| J5 sen od switu - gracz | A5 + proba reczna R5 | doba po nocnym marszu: "DLUG SNU O SWICIE 1", XP 0 (z perkami) i komunikat w grze, niezaleznie od godziny treningu, takze gdy rano dlug splacony ("teraz 0"); po odespaniu przed nastepnym switem - zwykly trening; "dlug o swicie (musztra)" w linii NocnyMarsz = dlug "po" |
| J6 zapis | A2, A3 | A3 (stary zapis, bez nowych pol): 0 bledow, dlug o swicie = dlug; A2: dlug o swicie gracza i AI po wczytaniu = przed zapisem (linia wczytania) |
| T1, T3, T7 | jak rozdz. 10 | 0 bledow i potkniec; T3 (mnoznik 171) bez zmian; T7 - zuzycie AI (teraz x D) <= 300 szt./dobe (A3: linia "zuzyto", doby 4-8, wobec `kopia-grupa11` - tam do 247) |
| J7 maski ruchu AI w zapisie (MUSZTRA-jp) | A1 -> A2 | linia zapisu "maski godzin ruchu partii lordow AI N (z K obserwowanych)", N ~ 600-700; po wczytaniu linia startowa "odtworzone N z N" (roznica = partie zniszczone); "Musztra AI" pierwszej pelnej doby po wczytaniu A2: postoj i "dzien" jak w dobach przed zapisem (nie 0-3% / x0.91). A3 (zapis 362 bez segmentu): "brak w zapisie" i pierwsza doba jak dotad - J1/J2 w A3 liczyc bez pierwszej doby |
| J8 komunikaty (MUSZTRA-jp) | R5 Jeffa (autotest nie czyta ekranu) | swit z dlugiem: komunikat predkosci/morale + "No drill today - sleep the debt off before the next dawn, or tomorrow's drill is lost too."; splata w dzien: "... wake fresh again. Drill resumes at the next dawn."; glod: jeden komunikat przy treningu (glod i sen - oba powody); "Musztra (gracz)" w dzien kary snu - XP 0 |
| J9 kontrola po zmianie MCM | reczna (opcjonalnie) | wylacz Ai Sleep Debt w trakcie gry - w "Musztra AI" tej doby "dlug teraz > dlug o swicie 0" (bez falszywego BLAD); "model bez bazy (elementy)" = 0 w kazdej dobie (> 0 = partia bez ActualClan albo inny wyjatek BK - wyjasnic) |
| J10 skutek drugiego rzedu | B (2 x 120 dob) | raport, nie bramka: zloto AI wydane na awanse na dobe i zakupy AiGear wedlug tierow - przy DrillLawAi tak / nie (pomiar do dopisania przed biegiem B; dzis nie ma takiej linii) |

Proba reczna dla Jeffa (R5 na nowo): przejdz noc w marszu bez snu -> od switu do nastepnego switu Twoi ludzie nie cwicza (jedno zdanie w grze), nawet jesli
rano odespisz; poloz sie wieczorem na 9 h (dlug 1) - nastepnego dnia cwicza normalnie.

### 14.7 Recenzja kodu MUSZTRA-j (17 uwag) i odpowiedzi - poprawki "MUSZTRA-jp"

Drzewo `G11\` (galaz `noc/grupa11`): MUSZTRA-j afeb7cb, poprawki MUSZTRA-jp 1f52d56, osobno NightRest-reset 4463a82 (uwagi 6, 13). Zbudowane (kod 0), nie uruchomione.

| # | Waga | Uwaga (skrot) | Werdykt | Co zmienione |
|---|---|---|---|---|
| 1 | wazne | Maska ruchu w zapisie tylko u gracza - po wczytaniu partie AI ok. doby jako marsz; przy Z14b TAK to prawdziwe XP | PRZYJETA | Sprawdzone w logu zapisu 362 (doba 109199: postoj 3%, "dzien x0.91", "razem x1.14"; potem 11-23%, x0.96-1.01, x1.19-1.26). Segment `T=id,maska,wiek` w `arm_drill` (juz `SaveText.Sync`), `Drill.RestoreMasks` w `SessionStart` (Stamp = teraz - wiek); stary DLL segment pomija; linie startowa i zapisu. Zakres rozdz. 11 poszerzony na wszystkie partie lordow |
| 2 | drobne | Etykieta "sen od switu" psuje regexy `parse.py`, `swit.py`; zdanie w CHANGELOG falszywe | PRZYJETA | Sprawdzone (`sen (\d+)%\)`). Etykieta "sen " wraca; regexy `parse.py`, `swit.py`, `w.py` sprawdzone na linii wzorcowej; zdanie MUSZTRA-j poprawione |
| 3 | drobne | Po wylaczeniu dlugu AI w MCM `DebtOf` czyta stary slownik kar do 1 h - falszywy "BLAD" | PRZYJETA | Sprawdzone (`NightMarch.cs:747-764, 921-935`). `SleepDebtNow`: AI za bramka `AiDebtLive` (jak `DawnDebtOf`) |
| 4 | drobne | (a) nieaktywna Z14a 0, AI caly wzor z zamrozonej maski; (b) Inni na treningu gry z premia glowy rodu - niewidoczne | PRZYJETA | (a) jedna regula: partia nieaktywna przy wzorze (Z14a, AI przy Z14b) nie cwiczy - gra i tak daje perki tylko aktywnej, D bez obserwacji nie ma; wylaczony Z14b jak dotad. (b) swiadomy wyjatek (umarli nie spia, nie znaja zmeczenia) - opis w kodzie, 14.5, "Dla Jeffa", CHANGELOG |
| 5 | drobne | Dlug o swicie = caly niesplacony dlug; komunikat sugerowal jedna dobe | PRZYJETA | Komunikat switu "No drill today - sleep the debt off before the next dawn, or tomorrow's drill is lost too."; pytanie do Jeffa (rozdz. 11); zachowanie bez zmian do jego slowa |
| 6 | drobne | Nowa kampania dziedziczy `Debt`/`DawnDebt` gracza (ResetWorld) | PRZYJETA - osobny commit | `NightRest.ResetWorld` zeruje `Debt`, `DawnDebt`, `_restTonight`, `_credited` (wolany tylko w konstruktorze, przed `Import`) |
| 7 | wazne | Gracz nie widzi w grze, ile cwiczyl i dlaczego | DO DECYZJI JEFFA | Nowy komunikat = CLAUDE.md 8.3; pytanie w rozdz. 11 (linia dzienna z wylacznikiem MCM); bez kodu |
| 8 | wazne | O dniu bez musztry gracz dowiaduje sie przy treningu; "wake fresh" kontra "no drill"; "old weariness" bez slowa o musztrze | PRZYJETA | `NightRest.DrillNote` w kazdym komunikacie switu z dlugiem (przy Drill Law), "Drill resumes at the next dawn." przy splacie; komunikat przy treningu tylko dla glodu |
| 9 | wazne | Skutek dla AI zanizony: pierwszy miesiac -18..-22%, Braavos x0.64 (t6 +57%), przyczyna slabe Przywodztwo | PRZYJETA | Sprawdzone (`okna.py`, `krolL.py`): doby 2-10 x0.78, 11-20 x0.80, 21-30 x0.82, 31-40 x0.87; L Braavos ~0.82 przy postoju 20% (swiat 14%). "Dla Jeffa" pkt 3-4, 14.3 (tabela w czasie, mnoznik czasu t6), 14.5 |
| 10 | wazne | (= 2) | PRZYJETA | jak 2 |
| 11 | drobne | Zuzycie zapasu AI x D - prog T7 | PRZYJETA bez kodu | 14.5 i T7 w 14.6 (A3, doby 4-8, wobec 247 szt./dobe w `kopia-grupa11`) |
| 12 | drobne | Komentarz "nic z niczego" przy `noModel` mowi tylko o P | PRZYJETA | Komentarz: perki 0, baza ze wzoru (przy law) albo to, co dal model; J9: "model bez bazy" = 0 |
| 13 | drobne | (= 6) | PRZYJETA - osobny commit | jak 6 |
| 14 | drobne | Po wczytaniu AI doba jako marsz - zostawic i opisac ("dotyczy tez gracza") | ODRZUCONA CZESCIOWO | Przeslanka falszywa: maska gracza jest w zapisie (`M=`), gracz tego nie traci. Asymetrie usuwa poprawka 1; A3 (stary zapis) - J1/J2 bez pierwszej doby (14.6 J7) |
| 15 | drobne | No Rest for the Wicked: +20% trafia do P (okruch premii glowy rodu), nie mnozy wzoru | PRZYJETA bez kodu | Sprawdzone (`DefaultPartyTrainingModel.cs:88-91`). Znany wyjatek w 14.5; poprawka tylko na slowo Jeffa |
| 16 | drobne | Wolniejsze awanse AI = mniej zlota znikajacego na koszt awansu | PRZYJETA bez kodu | Sprawdzone (`PartyUpgraderCampaignBehavior.cs:144-149`). Skutek w 14.5, pomiar J10 w biegu B |
| 17 | drobne | (a) glod i sen - komunikat tylko o glodzie; (b) "Twoja druzyna bez zmian" bez glow rodow AI w armii gracza | PRZYJETA | (a) jedno zdanie z oboma powodami; (b) "Dla Jeffa" pkt 5 |
