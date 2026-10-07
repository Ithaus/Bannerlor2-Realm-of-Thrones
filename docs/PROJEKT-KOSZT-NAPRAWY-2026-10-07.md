> **DECYZJA JEFFA 07.10 ok. 14:25 (przekazana przez sesje lawy): "nasza robocizna jest lepsza" - NIE WDRAZAC tego projektu w miejsce robocizny
> stosu n131b (b36a6d6: 0.2 x ubytek stanu x robota wykonania x dobrobyt miasta, MendMaterial.Labor/LaborF). Inne elementy (godziny zajmujace
> rzemieslnikow, limit AI 50%, rabat do 30%) - tylko na wyrazne slowo Jeffa. Zostaje jako material porownawczy i zrodla historyczne.**

# PROJEKT: koszt naprawy wedle godzin pracy (07.10; historia + mechanika gry -> projekt -> krytyk; NIC NIE ZAKODOWANE)

Pytanie Jeffa 07.10: "25% to chyba za duzo na naprawe, ile by kosztowalo realnie?". Zrodla historyczne: docs/HISTORIA-KOSZT-NAPRAWY-2026-10-07.md.
Robocze: SCRATCH 3cf3e0ac/dzien-6/koszt-naprawy (h-historia.md, g-gra.md, PROJEKT.md).

## Dla Jeffa

Masz rację: dla żelaza i stali 25% to za dużo. Ile realnie? W rachunkach zbrojowni w Tower (1353–1399) i hrabiego Derby (1390–93) naprawa miecza, hełmu czy kolczugi to od godziny do kilku dni pracy. Wychodzi 1–4% ceny przy lekkim zużyciu, 3–12% przy średnim i 9–30% przy ciężkim, razem z materiałem. Dziś za samą robotę bierzemy od półtora do 20 razy więcej. Najbardziej przepłacasz za lekko zużyte drogie zbroje. Skóra i sukno to wyjątek: tanie buty naprawiano prawie za cenę nowych.

Co proponujemy: płacisz za prawdziwe godziny pracy, po tej samej stawce, która siedzi w cenie nowej rzeczy, plus materiał z targu (jak u kwatermistrza). Te godziny naprawdę zajmują rzemieślników miasta, więc duży łup naprawiają dzień albo dwa, a nie pół godziny.

Przykłady (praca + materiał):
- miecz 104 zł, Damaged: 15 → 6 zł
- hełm 314 zł, Damaged: 47 → 16 zł
- Brigandine 2316 zł, Battered: 434 → 142 zł (8,6 dnia pracy)
- buty 28 zł, Battered: 5 → 9 zł
- Twoja zbroja po zwykłej bitwie: 16 → 2 zł

Dlaczego tak: cena zbroi to tygodnie kucia, a naprawa tego nie powtarza. Klepie się wgniecenia, wymienia nity i kółka. Zarobku z niczego nie ma, bo sklep płaci Ci ok. 5% wartości za broń i zbroję w każdym stanie. „Kup zużyte, napraw, sprzedaj” zawsze traci. Wraków kowale miasta nie odnawiają, tak jak ustaliłeś dla kwatermistrza. Lordowie AI dostaną najwyżej połowę godzin rzemieślników miasta, żeby warsztaty nie stanęły.

## Przyklady

Ceny w zł (1 zł = 1 pens). „Dziś ¼” to sama praca u kowala, ludzi, lordów AI i kwatermistrza; po paczce 131 kwatermistrz dolicza jeszcze materiał. Materiał liczony jak w próbie 131 (cena na targu = wartość). Przy zleceniu na 60 i więcej sztuk praca tanieje o 30%.

| Rzecz (wartość) | Stan | Dziś ¼ straty | Dziś: cały rynsztunek (½) | **Nowa reguła: praca + materiał = razem** | Godziny pracy rzemieślników | Nowa reguła / XIV w. (% wartości) |
|---|---|---|---|---|---|---|
| Miecz 104 zł | Plundered | 11 | 23 | 2 + 1 = **3** | 3 h | 2,9% / 1–1,5% |
| | Damaged | 15 | 31 | 5 + 1 = **6** | 8 h (pół dnia szlifierza) | 5,8% / 4–7% |
| | Battered | 19 | 39 | 13 + 1 = **14** | 19 h | 13,5% / 14–29% |
| | Mangled (wrak) | ława 23 | 46 | kowale miasta nie odnawiają; sam przy kowadle: materiał 2 zł, 8 h | – | złom |
| Buty 28 zł | Plundered | 3 | 6 | 1 + 1 = **2** | 4 h | 7% / 7% |
| | Damaged | 4 | 8 | 3 + 1 = **4** | 10 h | 14% / 14–17% |
| | Battered | 5 | 10 | 7 + 2 = **9** (drożej niż dziś) | 19 h | 32% / 29–38% |
| | Mangled (wrak) | ława 6 | 12 | sam: materiał 2 zł, 3 h | – | nowe buty |
| Hełm 314 zł | Plundered | 35 | 70 | 5 + 2 = **7** | 6 h | 2,2% / 1–1,5% |
| | Damaged | 47 | 94 | 13 + 3 = **16** | 16 h (dzień płatnerza) | 5,1% / 3–6% |
| | Battered | 58 | 117 | 40 + 4 = **44** | 48 h | 14% / 10–15% (23–30% z nowym podszyciem) |
| | Mangled (wrak) | ława 70 | 141 | sam: materiał 4 zł, 15 h | – | złom |
| Brigandine 2316 zł | Plundered | 260 | 521 | 13 + 18 = **31** | 16 h | 1,3% / 0,3–0,5% |
| | Damaged | 347 | 694 | 47 + 24 = **71** | 58 h | 3,1% / 1,5–3% |
| | Battered | 434 | 868 | 113 + 29 = **142** | 138 h (8,6 dnia, przy 10 kowalach ok. 1 dzień czekania) | 6,1% / 4–9% |
| | Mangled (wrak) | ława 521 | 1042 | sam: materiał 35 zł, 7,5 h | – | złom, płyty i kółka na łatki |
| Kolczuga 371 zł | Plundered | 41 | 83 | 3 + 5 = **8** | 8 h | 2,2% / 1% |
| | Damaged | 55 | 111 | 9 + 6 = **15** | 25 h | 4,0% / 4–9% |
| | Battered | 69 | 139 | 23 + 8 = **31** | 66 h | 8,4% / 9–24% |
| | Mangled (wrak) | ława 83 | 166 | sam: materiał 9 zł, 2,5 h | – | złom |
| Twój Brigandine po zwykłej bitwie (−1,4%) | – | – | 16 | 1 + 1 = **2** | 0,5 h | 0,1% |
| Twój Brigandine po ciężkiej bitwie (−6,9%) | – | – | 79 | 2 + 3 = **5** | 2,5 h | 0,2% |
| Łup 42 sztuki (po równo Plundered / Damaged / Battered) | – | ok. 770 | – | ok. **300** (z rabatem hurtowym) | ok. 490 h = ok. 2 dni rzemieślników średniego miasta | – |
| Półki wojska, 66 sztuk po bitwie | – | 225 (dziś 10% z rabatem) | – | **190** | ok. 200 h = ok. 1 dzień | – |

Gdyby ława miała jednak odnawiać wraki (decyduje sesja ławy), kowal liczyłby: miecz 36 + 2 = 38, buty 11 + 2 = 13, hełm 106 + 4 = 110, Brigandine 567 + 35 = 602 (43 dni pracy), kolczuga 87 + 9 = 96.

Dla porównania z żołdem: Brigandine Battered kosztuje dziś tyle, co 217 dni żołdu rekruta, a po nowemu 71 dni. Miecz Damaged to 3 dni żołdu rekruta zamiast 7,5.

## Regula koncowa

FINAL RULE AFTER REVIEW. One price-and-hours function for every repair path. The design is unchanged in its core. Seven corrections are marked [K].

0. SCOPE: all 100% of the paths in code (worktree n131, Armoury/src).
   (1) Hero's harness at the smith: ArmouryBehavior.RepairCost/RepairAll :2060/:2175.
   (2) "Pick a damaged piece - the smith": SmithMenu.PieceCost :406.
   (3) Mending bench DoMendLoot :486-526. Jeff's session owns it; it takes the numbers from here.
   (4) RepairAllSelf -> Forge.SelfRepair :2138 / Forge.cs:419.
   (5) "Mend it yourself": SelfMendParts :540-572, :829.
   (6) TroopSelfMend.Hourly :33/:83.
   (6b) [K] TroopSelfMend.Run :119-175. Missing from the design. It is live when MenPurseEnabled = false: 1/4 of the loss from the hero's purse, no hours.
   (7) The men's racks: TroopPieceCost :924-939, cap 24 h at :1007.
   (8) AiWear.MendInTown :218/:250.
   (9-10) Spoils quartermaster SpoilsSeal :744-760: repair all / weapons / armour / budget.
   (11) [K] AmmoRecovery.cs:39: 20% of shot arrows, bolts and thrown weapons are mended by the army's fletchers.
   (12) [K] CleanseAmmo ArmouryBehavior.cs:1839, called at :991 and :1422.
   Paths 11 and 12 stay as they are; see point 11.

1. CONDITION:
   - A piece in bags, armoury or on a shelf: the modifier's PriceMultiplier (Jeff's "price = condition").
   - The hero's equipped harness: the true condition from GetConditionQuiet. The same measure on paths 1, 2 and 4.
   - loss = max(0, 1 - condition).
   - Quality modifiers (>1: Fine to Legendary) are never wear.
   - Repair restores the piece's own stored modifier (OriginalModifier) or a clean piece. It never upgrades.

2. [K] WRECK = condition <= 0.10. This covers Mangled and the RBM *_damage_* states, all priced 0.1.
   - Use one test for both the price and for who repairs. Change LootPrices.IsWreck from "< 0.1" to "<= 0.1" in the SAME step as the price function.
   - Why: the rule puts loss 0.9 at the wreck anchor. If RBM 0.1 states stay "not wrecks", the men and AI pay wreck-share prices: Brigandine 567+35 zl and 691 man-hours, mail 96 zl and 246 h.
   - Town smiths do not restore wrecks on automatic paths (6, 6b, 8, 10).
   - On the player's explicit orders (2, 3, 7) the bench session decides. If allowed, the price is the wreck share + 131 material.

3. MAKING DAYS and LABOUR IN THE PRICE:
   - D = HistoricalPrices.HistDays(item, ArmsPricing.CostOf): the days in the price of a new piece and in WorkshopLaw.
   - L = D x the day wage HistCost uses x (1 + HistProfitPercent). The wage is WageFor(tier) = 3 + 1.5 x (tier - 1).
   - [K] For arrows, bolts and THROWN the wage is WageFor(1), and the days are x8 (HistAmmoLaborMultiplier), as in HistCost. With the tier wage, t6 javelins would cost 3.5x more.
   - Without historical prices: c.Days x SmithDayWage x (1 + SmithProfitPercent).

4. SHARE s(kind, loss): from the hours table, linear between the anchors loss 0 / 0.45 / 0.60 / 0.75 / 0.90.

5. HOURS:
   - man-days = s x D. man-hours = man-days x WorkHoursPerManDay (16).
   - [K] Hours come from the TRADE that does the work, using the same TownHands x guild weight (Paris 1292, wpis 52) as WorkshopLaw:
     - metal (plate, mail, helms, blades, polearm and thrown heads, metal barding): the town's smiths, armourers + swordsmiths (today's SmithHours)
     - leather, boots, gloves, saddles: saddlers
     - cloth, gambesons, capes: tailors
     - bows, crossbows: bowyers
     - shields: shieldmakers
   - Yesterday's repair hours come off that trade's workshop lines the next day, as they already do for smiths.

6. LABOUR PRICE = s x L x (1 - bulk).
   - bulk = 0.5% per piece in the order, at most 30%. These are today's TroopMendBulkDiscountPP/Max, extended to every order.
   - An order is: one click; one lord-day in a town; for the men, the pieces waiting that morning.
   - Hours get no discount.

7. MATERIAL = MendMaterial (131) unchanged: 0.2 x loss of the recipe, from the town's shelf at market price. Missing material means the piece waits.

8. [K] ORDER PRICE = round(sum of labour) + ceil(sum of material), at least 1 zl per ORDER, not per piece.
   - Rounding per piece adds about a third on small pieces: 66 rack pieces cost 253 instead of 191.
   - All money goes to the town treasury: Pay.ToSettlement / Town.ChangeGold + MoneyLedger.Note.

9. TIME:
   - The player's orders: man-hours / that trade's free hands. What does not fit today waits until tomorrow (5-23).
   - The player's orders may take every free hour.
   - [K] Automatic repairs (AI lords + the men's purse) take together at most 50% of each trade's daily hours in a town. Reason: one lord's backlog (about 400 man-hours) is more than a whole day of an average town's smiths (160 h). Without the cap, one visit would leave the town's arms workshops with no hands the next day. With it, they always keep at least half.

10. OWN HANDS AT THE ANVIL:
    - 0 zl for labour.
    - Time = s x your own forging hours for that piece (recipe stamina / 6, ForgeClock), at least 0.5 h.
    - Material 131 from the saddlebags. Stamina as today. Wrecks are allowed, at the wreck share.

11. NOT REPAIRS, left as they are:
    - Fletchers with the army: the cost sits in their wages. Tower 1373-5 gives 0.07 d per sheaf (0.7%), under a penny.
    - CleanseAmmo: ammunition and goods have no condition, so other mods' marks are stripped.
    - Spoils with its switch off.

12. [K] The design's "merchant's floor" is DROPPED. It is not needed: shops pay the player about 5% of base value for weapons and armour in any condition, so the loop always loses in one town (see effects 3). Between towns the floor would not work anyway, because it reads the buying town's prices.

## Tabela godzin

Shares of the making days D (light = loss 0.45 Plundered / medium 0.60 Damaged / heavy 0.75 Battered / wreck 0.90 Mangled and RBM 0.1). Linear from 0 between anchors. Battle wear 0.6 = 89% of "light"; rust 0.3 sits between medium and heavy; own harness at 95% = 11% of "light". Values unchanged from the design; three reasons corrected [K].

| Kind (trade) | Light | Medium | Heavy | Wreck | Reason (one sentence) |
|---|---|---|---|---|---|
| blade: sword, axe, mace, two-hander (smiths) | 2.5% | 6% | 15% | 40% | Tower 1375-7: 125 swords looked after in 24 man-days = 0.2 day each; nicks and a new grip about half a day; a new hilt or scabbard 1-1.5 days (Derby 1390-1). On a sword made in 8 days that is 2.5 / 6 / 15%. |
| polearm and thrown (smiths) | 3% | 8% | 20% | 50% | Tower 1375-7: 427 lance heads in 21 man-days = 0.05 day each; a new ash shaft is a few hours and cheap wood. |
| bow, crossbow (bowyers) | 2% | 5% | 12% | 60% | [K] Tower 1362-4: 60 man-days of labour on 130-195 royal crossbows = 0.3-0.5 man-day each (not 0.5-0.7), against 6-10 days to make one; 3,200 bows cleaned in 55 man-days (1340s); a cracked stave means a new bow. |
| mail: shirt, coif, chausses (smiths) | 1% | 3% | 8% | 30% | Barrel-rolling 1362 about 0.5 man-day a shirt; [K] Tower 1399 about 1 specialist day a hauberk for repair AND enlarging (upper bound); Derby 1392 27 d; heavy = re-ringing about a quarter from scrap mail, 3-4 days. |
| plate and brigandine: body, arms, legs, metal barding (smiths) | 0.7% | 2.5% | 6% | 30% | Derby 1392: 27 d for arm defences, about 120 d for a pair of plates (prices with profit, about 2-10 days' pay); on the 144 days of the Brigandine over Hauberk this gives 1 / 3.6 / 8.6 days, inside the 5-9 days a mail-and-plates repair takes in the records. |
| plate helm (smiths) | 2% | 5% | 15% | 40% | Polish, oil and straps 0.3-0.5 day; dents and rivets about 1 day; new lining plus aventail repair about 3 days (Oxford 1369: stuffing 3s 4d against a 13s 4d shell = 25%; Tower 1399: lining at 8 d a day). |
| leather: boots, gloves, jerkins, saddles (saddlers) | 8% | 18% | 35% | 60% | Colne Priory 1442: clouting 3 d, repair 6-7 d, repairs "at divers times" 12 d against new boots at 12-16 d; Derby 1392: boots mended for about 16 d against a new pair at about 40 d. |
| cloth: gambeson, capes (tailors) | 4% | 10% | 30% | 60% | Darning 0.2-0.3 day, patches half a tailor's day, a new cover 2-3 days, on a gambeson made in 5-8 days (linen 8.5-11 d an ell, Mortimer 1393-4). |
| shield (shieldmakers) | 5% | 15% | 35% | 70% | No 14th-century record; a pavise took about 4 man-days (Tower 1399); rawhide edging takes hours, split boards half a day, heavy damage means rebuilding a third [estimate]. |
| horse armour | by its material | | | | Barding is priced with the same formulas as human armour (ArmsPricing.Compute); saddles count as leather. |
| wreck column | | | | | In the records a wreck is scrap worth 7-14% of a new piece (Tower 1330s), and its rings and plates were used to patch others (1362, 1399). Rebuilding the destroyed part takes 1/3-2/3 of the work. |

Arrows and bolts: no wear (NoWear). The 20% fletchers' share in AmmoRecovery stays inside wages.

Typical pieces (man-hours, labour + material in zl, light / medium / heavy):
- sword t3 (32): 1.2 h 1+1 / 2.9 h 1+1 / 7.2 h 3+1
- spear t3 (19): 0.7 / 1.9 / 4.8 h
- bow t3 (38): 1.3 / 3.2 / 7.7 h
- mail t3, 10 kg (468): 8 h 4+5 / 24 h 11+7 / 64 h 30+8
- plate t4, 15 kg (587): 4.6 h 3+9 / 16.6 h 10+12 / 39.8 h 23+15
- helm t3 (74): 2.2 / 5.4 / 16.2 h
- leather jerkin t3 (92): 10 h 5+3 / 23 h 11+3 / 45 h 21+4
- gambeson t3 (106): 3.2 / 8 / 24 h
- shield t3 (21): 1.6 / 4.8 / 11.2 h

Hourly rate (= the price of a new piece; master's day x 1.25 / 16):
- t1 0.23 d, t2 0.35, t3 0.47, t4 0.59, t5 0.70, t6 0.82 d
- arrows, bolts and thrown: always t1

Historical anchors:
- t1 = a London labourer at 3-3.5 d (1350)
- t3 = a Tower haubergier at 6 d + profit
- t6 = the king's armourer at 12 d

Bulk: 0.5% per piece, at most 30% (Jeff 26.08: "cheaper in bulk"). [K] Better reason: at -30% the town still gets about 0.9 of a master's day for every man-day it gives up, while its workshops pay a journeyman 3 d. The discount cuts the master's margin, not below cost. The Tower 1399 vs Derby comparison compares wages with prices, so it does not prove a bulk effect.

## Skutki

1. PRICE LEVEL. Today's 1/4 against the rule, labour only:
   - sword x5.5 / x3.0 / x1.5
   - helm x7.0 / x3.6 / x1.4
   - Brigandine x20 / x7.4 / x3.8
   - mail x13.7 / x6.1 / x3.0
   - boots x3.0 / x1.3 / x0.7 (dearer)
   [K] So the right range is "1.4 to 20x", not "3 to 20x": a heavily battered sword or helm is overpaid only 1.4-1.5x. The excess is largest on lightly worn, expensive armour.
   - Labour against history: metal 1-17% of the lost value, at or just below the low end of the records.
   - The deficit at heavy states is material, which 131 caps at 20% of the recipe.

2. Repair is always cheaper than new: every share is below 1 and the rate is the same as in a new piece. Swapping at the shop (buy new at 1.10 x value, sell worn at about 5% of value) costs about the whole value of the piece.

3. [K] NO GOLD FROM NOTHING. The design's loop numbers were wrong. Shop sale price for equipment, from the code:
   - The vanilla penalty is 0.06 + 1.5 + 0.25 x (Tierf - 1).
   - BK multiplies it x5 (BKEconomyLayerInstaller.GetTradePenaltyPostfix, weapons, armour and saddles). It is patched on both DefaultTradeItemPriceFactorModel and BKROTPriceModel, which calls base, so the total is x25.
   - Log 05.10 confirms this on the buy side: the chain price for karstark_boots is 145 at value 73 = 0.8 x (1 + 0.06 x 25).
   - Then the MarketGlut floor applies: 5% of BASE value, not the condition.
   - Log 04.10 (D:\Backup-Bannerlord\...\Armoury-2026-10-04_15-54-06.log): common_armor 1217 -> chain 60 = 4.9%.
   So the player gets about 5% of base value in any condition. A clean piece gets 8-12% only with Trade 300 and every perk.
   - Same-town loop "buy worn x1.10, repair, sell clean" on the five pieces, even with the best sale case: from -3 to -1248 zl.
   - A wreck repaired at the anvil and sold: from -3 to -105 zl.
   - Repairing before selling never pays.
   - The design's +87..+589 and the n129 tables' "~800 from nothing" used sale prices of 30-85% of value that the game does not pay.
   Repair now matters only for gear that is used.

4. WRECKS. Town smiths do not restore them.
   - RBM 0.1 pieces become scrap (about 20% of AI armour loot). That gives MendMaterial a scrap source in the 38 of 97 towns without ore.
   - Restoring at the anvil is the player's own work. It pays as gear to use, not as trade.

5. AI MONEY. 0.43x today per piece: 3,762 -> about 1,600-2,100 zl a day; in war 9,774 -> about 4,200. Outstanding repairs per party fall from about 840 to about 360 against a purse of about 7,000. They can afford it.

6. AI HOURS. About 10 man-hours a piece. [K] By trade: smiths 58% (5.8 h), saddlers 25%, tailors 10%, shieldmakers 5%, bowyers 2%.
   - At today's 285 pieces a day: 11% of the world's smiths (war 19%) instead of the design's 18% (32%); saddlers 12% (21%).
   - Repairing the whole inflow (807 a day) would need 30% of the smiths and 34% of the saddlers. They cannot keep up, and the backlog grows faster (already 254 -> 9,924 in 40 days).
   - The 50% cap per town protects the workshops.
   - AI soldiers still fight in sound kit (the DTE armoury has no condition), so only the books, the purse reserve and the state of sold surplus change.

7. THE PLAYER:
   - Own Brigandine: 2 zl after a median battle (16 today), 5 after a heavy one (79).
   - 42 pieces of loot: about 300 zl instead of about 770, but about 490 man-hours, about 2 days of an average town's trades working in parallel (smiths 1.8, saddlers 2.0) instead of 25 h.
   - Racks, 66 pieces: 190 zl instead of 225, about 1 day.
   - Quartermaster test 131: 166 zl instead of 541. The Brigandine needs 138 h, so part of the order waits until the next day.

8. Material for the men and AI (K13 step 139): 13-22 ore loads a day against 180 produced.

9. Town treasuries barely change (the men spend the rest of their purse in the same town). Nothing is created or destroyed. Payments get MoneyLedger.Note.

10. n129 (armour from the forge): a botched Rusty Brigandine now costs about 118 zl at the smith to make ordinary, instead of 405; a Dented one about 27 instead of 231. The n129 text for Jeff changes. At the anvil it was already almost free.

## Ryzyka

1. Hours are the biggest effect, and the hands are only estimated (970 smith-days a day; the code minimum is 233). Step 1 must log repair hours per town and trade a day, plus the hands workshops lose.

2. The AI backlog grows faster. The purse reserve (OutstandingCost) grows too, so money sits longer in purses; it is not lost.
   Proposal: reserve only what one town-day can repair, or sell pieces that cannot be repaired as worn surplus. Measure first.

3. The player waits a day or two for a large haul. That is a gameplay decision; the anvil stays fast.

4. Heavy leather, cloth and shields get dearer than today: boots 9 instead of 5, a jerkin about 25 instead of 17, shields 1.7-1.9x. This matches 1442.
   [K] Jeff's wish was 26.08 (CHANGELOG "tansza naprawa wojska: wrak max 10% wartosci + rabat hurtowy"), not 30.08, and it was about a troop wreck. Under the rule:
   - metal stays at or below 14% of value
   - heavy leather, cloth and shields reach 20-32% (a few zl)
   - wrecks are not repaired by town smiths at all, which contradicts the racks today (wreck 10%)

5. Wrecks on explicit orders: the bench session decides. Our number if they are allowed: the wreck share + material (Brigandine 567 + 35, 43 man-days).

6. RBM 0.1 as wrecks: the player's men's RBM pieces need the anvil or melting. Root cause: Armoury's wear picks the lowest-price modifier, and every RBM *_damage_* is priced 0.1, even "Scratched" at -5 armour (RBMCombat_item_modifiers.xml:740-867).
   Separate step: Armoury wear should not assign *_damage_* states; use Rusty 0.3 or Mangled.

7. Trade pools are new code in SmithHours and WorkshopLaw. Check collisions with K13-3 (129, cloth and leather) and K13-4 (130, troop clothing), which touch tailors and leatherworkers.

8. The sale finding rests on decompiled BK/BKROT (ilspycmd on the installed DLLs; BannerKings.dll contains GetTradePenaltyPostfix) and on the 04-05.10 logs. PROJEKT's explanation that AIInfluence put sales on the floor was probably wrong. If BK x5 or the MarketGlut floor ever changes, recompute the loop.

9. A hole outside the rule: the MarketGlut floor is 5% of item.Value (base), so a wreck sells for as much as a clean piece. A Brigandine wreck bought on a glutted shelf (x0.25-0.5) and sold on a normal or starved one gives +50..+400 zl with no work.
   Fix, as a separate step: put the floor on ItemValue (with condition).

10. Material 131 charges 9% of the recipe at light wear (Brigandine Plundered: 18 of 31 zl), where history has oil and sand. It is too low for new linings and covers. Kept as Jeff's 131 rule.

11. The shares are estimates [S] calibrated on the reference pieces. The game counts more making days for leather and for t5-t6 plate than history does.

12. Settings that fall out of use. [K] All seven are in Jeff's Armoury.json, not four:
   - RepairCostFactor 0.5
   - TroopMendWreckShare 0.1
   - MendLootHoursPerPiece 0.6
   - SmithRepairHoursPerPiece 1.5
   - SelfRepairHoursPerPiece 2.5
   - TroopMendMaxHours 24
   - SelfRepairMaterialFactor 0.25
   New settings: the share table and AutoMendMaxShare 50. Run gen_mcm afterwards.

13. Anvil stamina has two rates (40% for the harness, 30% for a piece). Unify later.

14. History check:
   - Verified at the source: London 1350 (6/5 d, labourer 3.5/3 d, boots 3s 6d, shoes 6d), the Statute 1351 (3/2 d, 4/3 d) and Colne 1442 (12/7/6/3 d, boots 12-16 d).
   - Arithmetic checked: Tower 4x226x6 = 5424 d, 4x45x6 = 1080, 20x24x12 = 5760 = £24, 4x10x8 = 320 = 26s 8d, 144/125 = 1.15 d; 104 d = 8s 8d, 314 d = 26s 2d, 2316 d = £9 13s.
   - Currencies: 12 d = 1 s, 20 s = £1, mark 13s 4d, noble 80 d = 24-26 scot, scot = 30 pf ≈ 3.2-3.3 d, Prussian mark = 24 scot ≈ 80 d, florin ≈ 36 d.
   - Not re-read: the Richardson PDF (over 10 MB), Derby's text and Oxford 1369.
   - [K] N3 (Tower 1399) priced the 22 old haubergeons at the new price. As scrap (7-14%) the material is 1-2 d, so about 13 d a piece = 4.5% of a 24s shirt, labour about 90%, and the job included enlarging.
   - [K] h-historia section 8 adds up to 5-9 days (56-100 d), not 6-13 days (60-130 d).

15. ORDER, one change at a time:
   (1) trade pools + a daily log of repair hours, behaviour as today
   (2) the price/hours function on all paths + IsWreck <= 0.1 + rounding per order + the 50% cap
   (3) material for the men and AI (K13 139)
   (4) Armoury wear without RBM 0.1
   (5) MoneyLedger.Note
   Separate, not a repair step: the MarketGlut floor on ItemValue.
   The checks behind all this: scratchpad dzien-6\koszt-naprawy\k_krytyk.py.
