# KS round 4 plan (KS-R4): KS round 3 + DS dues + merchants' credit + fixes from the round-3 final test

Date 17.10.2026. Track L (full chain). Nothing has been coded or deployed. This is a plan only.

- **Base:** int **ed949c36** (KS round 3 final, branch w-toku/noc-ks3 5c895704) + branch **w-toku/noc-ds** (DS F1 villages, F3 towns/crown; HEAD 2d2ab146 or later; built from WGRANIE 74 fe1b271e, DS without KS, API `DsVillage.BudgetShare / G / RentPerDay / Chest`).
- **Round-3 final game test:** `Armoury-2026-10-17_09-07-09.log` + CSV `2026-10-17_09-07-09`. `sprawdz_logi --grupa nks`: 24 of 53 not passed (18 NIE + 6 ETAP).
- **Inputs:** three read-only diagnoses (prices, corridors/granary/BK/controller, cost), work files in `ksim/r4/nf`, `ksim/r4/tr`, `ksim/r4/cost_*`, and the specs `docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md`, `docs/PROJEKT-DANINY-SREDNIOWIECZNE-2026-10-17.md` and STAN-PRAC ### 64-71.
- **Jeff's rules this plan must keep:**
  - closed economy, with nothing from nothing and nothing into nothing;
  - balanced start, every good at 95-105%;
  - production only from real hands and land;
  - a dynamic medieval market;
  - medieval dues, with no residual takers;
  - kings never short of army money (moderate tax rises, loans);
  - Jeff's Ironman save stays safe;
  - Claude designs the parameters (do not ask Jeff about numbers).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 0. In short

Round 3 fixed the grain distribution:
- grain shelf median 3.6 days;
- q90 8 days;
- 14-16% of towns under 0.5 days;
- villages buy 77-79% of what they want.

What remains comes from six causes:

1. **Village money loop.** Measured alpha is 0. The Plan gives villages a budget from what is left in the purse after the residual takers. DS replaces it with a budget from the rules (`BudgetShare`), and no input of that budget reads the purse.
2. **Recipe mismatch for meat.** The Plan counts 10/6/2 meat per cow/hog/sheep, while the game's hidden artisans make 6/2/1 and take 79-97% of the animals. Real meat output is 650/d against 1330/d in the Plan. Fix: one yield table for the Plan, ZC5 and the artisans.
3. **Phantom price demand.**
   - FoodSubstitute B2 still runs under KS: cheese +45%, grape +33%, meat +29%, while dates and butter are drained.
   - Olive demand includes oil-pressing input that never happens: 23-33% of olive demand.
4. **Distribution.** KS-H sells only above 1.25 x T*, chooses a ship only when the sea distance is shorter (not when it is faster in days), and searches further towns only when none of the 12 nearest can sell. The destination coffer blocks 24% of orders. Fixes: a relative keep, ship by days, always searching further towns, and merchants' credit (DS F2).
5. **Start stock and measurement.** At D the shelves held only 7-53% of T*, and the 28-day price average carries pre-KS prices into the acceptance window. Fixes: a stock-gap margin in the start window, and a price average restarted at D+14.
6. **Safety nets and controls that hide or mis-steer the signals:**
   - the granary floor (51% of villages);
   - the BK village market spending 5% of the purse per item, not per day;
   - the controller reading unmet demand through a market-wide stock ratio;
   - the regional cut trimming exporters whose partners are short;
   - cost (pickup day 28 ms, A7 days, village purchases).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 1. Order of work

Track L. One thing at a time, all in this one chat. Update STAN-PRAC after each stage. Pause at 90% of the 5-hour limit.

| stage | what | done when | time (est.) |
|---|---|---|---|
| A | merge + identity + sub-timers | build rc 0; keys off = round 3 bit for bit; sub-timers in the log | 2 h |
| B | **simulator first**: calibrate the sim to the round-3 game, prototype every rule, fix parameters | S-0 reproduces round 3; prototype gates pass (section 12) | 4 h |
| C | code (sections 2-10) | build rc 0 after every step; port tests and harness identical with new keys off | 8 h |
| D | checker sprawdz_logi (section 11) | groups nks + nds run on the round-3 log without exceptions; restated gates documented | 1 h |
| E | acceptance: simulator on the ported C# rules + port tests + 2 reviews (economy; code/save) + fixes | all S gates TAK, or only items marked DECYZJA remain | 4 h |
| F | ONE in-game test, 120 days (section 13) | log + CSV copied; nks, nds, nzc0-2, nbitwa; report to Jeff | 1 h game + 1 h analysis |

**Stage A (details)**
1. Create branch `w-toku/noc-ks4` from int ed949c36 and merge `w-toku/noc-ds` with `tools/scalanie/scal.sh` (watch the known pitfalls: a patch inserting a dispatch into someone else's `if`, and same-named threshold groups from parallel branches).
2. Expected conflicts:
   - `BkSeal34.cs` RunVillage;
   - `TownFolk.Live.cs` (village spending after the basket, DsVillageDay);
   - `VillageTakings.cs` EnterPostfix/Whole112;
   - `KingdomTreasury.cs` Levies, `KingdomTreasury.LordLevy.cs`;
   - `PopulationLaw.cs` rent;
   - `Sutlers.cs`;
   - `TownFolk.Guard.cs` PayHoardVillage, `TownFolk.Loans.cs`;
   - `McmSettings.cs` / `Settings`;
   - `ArmouryBehavior.cs` SyncData;
   - `tools/sprawdz_logi.py` (groups nks and nds).
3. Resolution rules:
   - DS hooks keep their DS logic;
   - KS-only branches (ZC4 food skip, KsVilOut) keep the KS logic;
   - where both apply, see section 6.
4. **Identity check** with every new R4 key off and DuesMedieval off:
   - Plan harness `kmoney/h4` output bit for bit against ed949c36 (`id336`/`idnew` style compare);
   - KS-H port `kmoney/port4`: 1251 lots identical;
   - build rc 0.
5. **Cost item T0 first:** about 10 sub-timers in `StartCalibration.LiveDaily` (Live.cs:440-605): snapshot, 1/7 recompute, reuse, ramp, town fields, rule, Zc4Build, floor/shelf/lines. Needed to measure the ordinary 4.28 ms day (2.66 ms of it is not timed today).

**Stage C (coding order, build after each step)**
1. **M1-M9** money (section 2).
2. **P1** one slaughter table (game + Plan + ZC5).
3. **H1-H8** KS-H (section 3).
4. **P2-P7** prices.
5. **G1** granary.
6. **B1-B4** BK and village order.
7. **C1-C3** controller.
8. **K1-K3** Plan corridors.
9. **T1-T12** cost (bit for bit), then T13 only if the sub-timers show the median above 3 ms.
10. Keys, save, Ironman (section 10).
11. P8-P10 diagnostics and measurement.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 2. Step 8 money: the DS budget from the rules replaces alpha_net (M1-M9)

**Today** (`StartCalibration.Money.cs:139-152` VilAlphaFor with SwAlphaNet), alpha_net is:

    alpha = 0.95 x (inflow + KS-H freight - outflow outside the basket) / (handed / Phi)

The outflow is exactly the residual takers: the crown levy on the purse, the savings tax, the BK market, the old list and W7. Each of them takes a share of the purse above a threshold. The more the village spends on the basket, the less the residual takers leave, and the lower the next alpha. That is a loop: 0.47 -> 0.22 -> 0.03 -> 0.

**Round 4** (new key `KsAlphaRules`, default true, active only where KS is applied).

- **M1, `Money.cs`, new `VilAlphaRules(Village v, out double fix)` next to VilAlphaFor:**

      k_v   = DsVillage.BudgetShare(v, kingdom)      // (u_v - t) x (1 - g_k x DsWarShareVillage) + t/2
      a_v   = (1 - VilReserve) x k_v x PhiPlan[m]    // VilReserve 0.05 = the 5% that goes to the 21-day chest
      fix_v = (1 - VilReserve) x (O_v x (1 - g_k x DsWarShareVillage x m_v) - R_v)
      return clamp(a_v, 0, 0.95)

  - u_v is the village share of handed takings from the lord's decree (SettlementTaxModel, the same model as BK and 112), not from the purse.
  - t is the tithe 5% (half returns as alms).
  - g_k is the war grant: 1 in war, 1.5 with War Tax, 0 in peace.
  - R_v is `DsVillage.RentPerDay(v)`.
- **M2, `Core.cs`:**
  - new field `KsDistrict.Fix` (next to `Alpha`, :85);
  - `MDist.Fix` (:1052), copied at :1186;
  - budget at :1343 becomes `double budget = Math.Max(0.0, v.Alpha * salesB + v.Fix);`;
  - the 8d re-run (`MoneyStep(..., table)`) uses the same formula.
- **M3, `Plan.cs:173`:**
  - `kd.Alpha = SwAlphaRules ? VilAlphaRules(v, out kd.Fix) : VilAlphaFor(v);`
  - default for villages without data: `inp.VilAlpha[m]` = the market median of a_v.
- **M4, Phi per market and the land gradient:**
  - `_phiHand` / `_phiProd` (Money.cs:91-113) are split by market, so W and E get their own ratio of cart takings to base value.
  - `PhiPlan[m]` moves at most ±7% per Plan (the same step as the controller).
  - When `R4LandPrice` is on, salesB (Core.cs:1336-1337) is valued at the district's land gradient, the same `GradZ` the basket already uses. Income and basket then share one price base.
- **M5, O_v counters:** new `StartCalibration.KsVilIn(Village v, long x, bool sutler)` next to `KsVilOut` (R3.cs:499), with the same 28-day weight as the takings and m_v = the sutler share. Hooks:
  - sutlers' village part after W4, `Sutlers.cs:381-393`;
  - released soldiers' purses, `ClanBudget.cs:1236-1248`;
  - lords' purchases in the village, `VillageTakings.cs:383-407`;
  - heirless notable gold, `Seal31B.cs:1057-1068`;
  - KS-H freight to carters, the existing `KsVilFreight`.

  Counters run from the session start (measurement only), so the Apply Plan already has 28 days.
- **M6, DS off (key off, or a village without DS):**
  - **frozen alpha**, i.e. VilAlphaFor measured at the Apply Plan (A) and never re-measured after A;
  - log line "KS bez DS: alfa zamrozona z doby A";
  - no loop; risk: purses drain slowly under the old takers.
- **M7, log line "KS: pieniadze planu":**
  - added: "alfa z praw (DS) min/med/max W/E, czesc stala O - R swiata, g krolestw, Phi W/E";
  - kept as INFO next to it: "alfa zmierzona (netto)".
- **M8, ramp alignment:** for a KS village, DS DayOn = A and the effective DS ramp = A..D (14 days). The old residual takers are gone before basket buying starts at D (lambda 0 -> 1 between D and D+14).
- **M9, chest under KS:** in `TownFolk.DsVillageDay`, nv = `StartCalibration.Zc4BasketBudget(v)` (the Plan-paid basket per day) when KS is applied; otherwise buy1 as on the DS branch. Chest = 21 x (nv + rent).

**Why there is no feedback loop.** None of the inputs reads the purse or any outflow:

| input | depends on | depends on village spending? |
|---|---|---|
| u_v | decree / tax model (law) | no |
| t, g_k, w | settings, war state | no |
| Phi_m | world cart prices / base value, 28 days | only through prices (damped, see below) |
| salesB | the Plan's own production (inside the Plan's fixed point) | no |
| O_v | sutlers, released soldiers, lords' purchases, notables, KS-H freight | no |
| R_v | heads x rent per head x decree | no |

- **Inside the Plan, the remaining loop is sales <-> home work.** Its slope is 0.95 x k x Phi ≈ 0.95 x 0.476 x 0.72 = 0.33 < 1. The existing fixed point (damping 0.5) converges.
- **Through prices:** more village buying -> higher prices -> higher Phi -> slightly higher budget. That is a real market effect, bounded by the ±7%/Plan step in M4.
- **In-game check:** "alfa z praw" against "alfa zmierzona", and purse drift ≥ -0.2%/d (NDS-3).

**Expected, from the DS section 4.2 recomputation on the round-3 CSVs:**
- alpha from the rules: median 0.45-0.46 at g = 1 (p10 0.37-0.38), 0.41 at g = 2;
- world village budget 156-164k/d, against 16-36k after the collapse;
- paid share of the basket 35-40% (was 5-14%);
- home work: Westeros from 43.5% of hands toward about 25-30%;
- village sales to market up by the same amount.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 3. KS-H on the merchants' own capital (DS F2) + routing (H1-H8)

**H1 - ship by days (KsFoodCarts.cs:228).**
- `daysLand = ceil(dl / per)`, `daysSea = ceil(da / (per x ShipSpeedK)) + PortDays`, `sea = !landOk || (allOk && daysSea < daysLand)`.
- `DaysVer` "S" -> "T".
- The whole table is built on the first day in one go (4828 lookups, about 1 ms, with a 20 ms stopwatch guard that falls back to 200 per day).
- The Plan corridors read the same table (Plan and KS-H stay consistent).
- Key `KsShipByDays`.
- Replay: Qartheen grain 872 -> about 2290/d (Plan 2217); 52 of 2414 pairs get on average 13.7 days faster.

**H2 - further towns always (KsFoodCore.cs:106).**
- Condition `cand.Count == 0 && ...` -> `(a.FarAlways || cand.Count == 0) && ...`, up to 8 further towns in range (NCandFar 4 -> 8 when FarAlways).
- A further town is checked first by `offer[s*NK+k] >= 1` (cheap), then days and war.
- Key `KsFarAlways`.
- Replay (H1+H2): +25% volume; Crownlands 0 -> 300, Aserai 0 -> 831, Qartheen 2881.

**H3 - relative source keep (KsFoodCore.cs:79-80).** Inside Decide, after the first loop:

      med[m,k]  = median over towns of market m with dzB > 0 of (sh / dzB)       // days on hand
      keepT     = RelKeep ? max(0.5 x T*, min(SrcKeep x T*, 1.1 x med[m,k])) : SrcKeep x T*
      sur       = sh - keepT x dzB - Keep
      offer     = floor(share x sur),  share = Staple[k] ? 1.0 : 0.67   (RelKeep; else 0.5)

- When the whole market is short, exporters sell down toward the market median, never below T*/2. Shortage is then shared by price, the way a medieval grain market works.
- When the market is well stocked, the keep stays at 1.25 x T*.
- The profit check and the source price index still stop over-selling.
- Key `KsRelKeep`.
- Expected:
  - Westeros cheese on offer 64-210 -> about 1200-1500 units;
  - Essos grape towns under 0.5 T* priced 4.2 -> under 2;
  - NKS-23b falls from 21-93% toward 10-20% (meat apart).

**H4 - one price for the merchant's decision and for the bill, `DestValue(t, k, n)`.**
- Value of n units arriving at the destination, as the game's price formula walks up the stock: a closed form from today's ZC4 price p0 and stock S0,

      Σ p0 x (S0 / (S0 + j x wv))^0.6

  integrated in one Pow. Each unit is capped at 3 x start price x value (F5: the empty-stall cap of 1.5 x start at Zc4.cs:311 no longer blocks profitable famine lots), then multiplied by (1 - Margin 0.03), the destination market's margin.
- It replaces `destUnit x n` in the profit check (KsFoodCore.cs:140/146) and is also the bill at arrival (H5).
- Cost: one Pow per lot instead of GetPrice calls.
- Expected: most of the 28% of lots skipped as unprofitable for cheese/butter come back; +2-12% volume.

**H5 - merchants' credit (key `KsFoodCartsConsign`, default true, only with KS applied).**
- **Departure** (`Ship`, KsFoodCarts.cs:595-658):
  - the goods leave the source shelf as today; **the destination pays nothing**;
  - freight is paid by the **source** coffer: by land through `pendFreight` (carters, the source's villages, unchanged), by sea through `TownFolk.KsCrewPay(srcT, src, fr)` paid from srcT;
  - `Cart.Paid = 0`, `Cart.Freight = paid freight`;
  - no new cart fields: Paid == 0 marks a consignment, and old carts with Paid > 0 finish under the old rules.
- **Decision** (KsFoodCore): new `In.Consign`, `In.SrcRoom[s]`, `In.DstOverdue[t]`.
  - With Consign:
    - destinations with `DstOverdue` (a bill older than `KsConsignOverdueDays` = 30) are skipped;
    - the budget[t] / budgetShort[t] cap is not used;
    - `avail = SrcRoom[s] - srcSpent[s]`, where `SrcRoom[s] = max(0, KsConsignSourceExposure (0.5) x Gold[s] - Exposure[s])`;
    - Exposure = open bills owed to s + Σ consigned carts of s in transit x today's source unit price + their freight;
    - Ship also checks `srcT.Gold >= fr`;
    - new counters: `Stats.SrcCap` (lots blocked because the source has no room) and `Stats.Overdue`.
  - With Consign off, the code path is exactly today's (port test 1251 lots).
- **Arrival** (`Arrivals`, :341-380):
  - after spoilage, the goods go on the destination shelf as today;
  - bill `B = DestValue(dst, k, n)` at the arrival-day price;
  - **paid now**: `P = min(B, max(0, dst.Gold - KsConsignFloorShare x reserve))`, dst -> src;
  - **the unpaid rest** `B - P` becomes a bill: `KsFoodCarts.Credit.Open(src, dst, B - P, today)`.
  - **KsConsignFloorShare = 0.5** (the DS draft said 0.25). It is the same floor as round 3's short-stall rule and keeps NKS-16b (q10 of coffer/reserve ≥ 0.5) true. Poor importers keep half their merchants' reserve and the rest waits as a bill.
- **Daily repayment** (new `KsFoodCarts.Credit.PayDaily()`, called from `SupplyDemand.DailyTrade` before TradeCarts and before the valve):
  - each destination pays its bills oldest first from gold above 0.5 x reserve;
  - a war between source and destination -> the bill is forfeited ("kupcy stracili", counter; the claim vanishes, no gold appears or disappears);
  - a change of owner inside one kingdom keeps the bill.
- **Return** (`Back`, :383-404): Paid == 0 -> all goods go back to the source shelf, with no refund (nothing was paid) and the freight is lost to the carters.
- **Save:** new key `arm_daniny_kupcy` (SaveText.Sync): accounts per (source, destination) pair, up to 8 dated entries per pair (the newest merged when full), "zrodlo~cel~zostalo~doba". Gate: ≤ 64 KB, like arm_ks_kupcy. A missing key means no bills. An older DLL ignores the key.

**H6 - KS-H line and identities.**
- New in the line "kredyt kupiecki: otwarte X zl (n rachunkow, m miast), zaplacone przy dostawie, splacone, starsze niz 30 dni, przepadle (wojna), zablokowane: zrodlo bez miejsca / cel z zaleglym".
- Gold identities:
  - goods: src receives = dst pays (at delivery + repayments);
  - freight: src pays = carters + crews receive.
- The goods identity is unchanged.

**H7 - Plan corridors with the credit model** (Corridors.cs:129 link, :349 / :387 / :593 budgets, :405 cap):
- **link days** = demand-weighted best days from the land's importing towns to the exporter's surplus towns (top-3 surplus towns), from the same table as H1;
- **importer capacity** = Σ over the land's towns of 15% x max(0, gold28 - 0.5 x reserve) per day (pay on delivery from above half the reserve);
- **exporter capacity** = Σ over surplus towns of 0.5 x gold28 / (haul days + 3);
- the free flow the Plan promises shrinks to what town merchants on credit can run, and the land gradient then pays hands in the right lands.

**H8 - port test `kmoney/port5`.**
- C# `KsFoodCore` with all new flags off: 1251 lots identical (source, destination, good, units, payment, freight).
- With all flags on: identical to the Python port `ksim/sim/r5port.py` on the same 9 states.

**Expected (replay of the round-3 state, ksim/r4/tr/a5_out.txt):**
- H1+H2+H5: 14.9k units/day (+31%);
- with H3 for grain and salt: about 20-24k/d (a one-off release of piled stock, then exporters settle about 2 days lower);
- blocked by the destination: 24% -> about 0 (only overdue bills);
- open bills, mainly Pentos and Lys: 0.1-0.4M;
- merchant margin median ≥ 0.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 4. Prices (P1-P10)

**P1 - one slaughter table for the game, the Plan and ZC5 (meat; also hides).**
- New `KsTab.LiveYield[cow, hog, sheep]` = (meat per head, hides per head), from the Plan's own rule (MeatOfLive 0.5: 10/6/2 meat) and 1 hide per head (what N5-B gives in the game).
- Used by:
  - the Plan: Tables.cs:52, Deliver.cs:74 and :194-200, Core.cs:482;
  - ZC5: Crafts.cs:84-94;
  - the game's hidden artisans: new `KsMeatYield` in `SlaughterHides.cs`. When `StartCalibration.Applied` and the key `KsMeatYield` is on, it sets the meat count of every single-livestock artisan line, both vanilla (sheep 1, hog 2) and BK (sheep 1 + hide + wool, hog 2 + hide), to 2/6/10 per head (cow 10). The hide stays at 1 (N5-B). Same mechanism as N5-B: the shared XML list object, re-run on KS Apply and OnSessionLaunched, restored on Undo, on key off and on Reset. Workshop types are not saved, so the save is unchanged.
- ZC5 slaughter:
  - input reserve 0 for live inputs (Crafts.cs:191, 5 head today);
  - slaughter pairs daily instead of every 2nd day (Crafts.cs:142).
- The Plan's hide rule follows the game (1 per head). NKS-5b hides then reads against the right Plan value (today Plan 11.1 against game 0.9-1.1).
- Expected:
  - meat output 650 -> about 1260/d (95% of the Plan's 1330);
  - unmet 362 -> under 70/d;
  - meat index 3.3-4.7 -> about 1.5-2 within 3-4 weeks and about 1.0-1.3 once shelves reach T* (6-10 weeks without P4, 3-5 weeks with P4).

**P2 - FoodSubstitute B2 gated under KS (FoodSubstitute.Shift, the source loop and SetDemand at :245 / :255).**
- For a town in the ZC4 map, the share is multiplied by (1 - lambda_town) for ZC4 basket categories. At full ramp B2 is off there, because the ZC4 ladder (grain and salt -> drink -> protein -> rest) already does the substitution.
- Non-KS towns (Jeff's game) are unchanged.
- Key `KsFoodSubstituteGate` (new, default true). The log line keeps B2 totals for KS towns (expected 0 after D+14).
- Expected:
  - Westeros grape 2.1 -> about 1.0-1.2 (stock-rich towns about 0.85-1.0);
  - Westeros cheese x0.8;
  - meat x0.85-0.9.
  - Butter, dates and eggs read higher, which is honest: Westeros butter +20%, Westeros dates about 1.5-2 until supply grows.

**P3 - processing input demand from what processing really does (Zc4Derived, Zc4.cs:379-420).**
- For every ZC5 pair: `made = min(outD x f, Made7[p] + max(0, 4 x outD - shelfOut) / 7)`.
- `Made7` is the 7-day average ZC5 output of that pair in that town (new `ZTown.Made7[]`, session only; the first 7 days use outD x f).
- Input Der = Ratio x made (bread/pie/beer keep their "miss -> grain" rule).
- Expected:
  - olive Der -550..-720/d (23-30% of olive demand);
  - olive index x0.82 at the same stock;
  - KS-H stops overshipping olives (stock growth +33..53% per day of consumption -> about 0);
  - grape Der barely changes (wine is realized).

**P4 - stock-gap margin in the start window (Plan.cs:29 `ShelfMargin` 1.03 -> per market x good).**
- From the Apply Plan (A) to D+28:

      f[m,g] = clamp(1 + (T* x Dpaid - shelves - in transit) / (28 x Dpaid), 1.00, 1.25)

  with the target split by land in the gradient step.
- The Apply Plan sets f directly; later Plans move it at most 7% per Plan. After D+28 the controller's ±5% gap rule applies again (section 7).
- The Plan line prints f per good; NKS-3 divides NA TARG by f (section 11).
- Hands come from home work (real hands), not from goods made from nothing.
- Expected:
  - Westeros cheese about +26% (+94/d);
  - dates and olives fill in about 4 weeks instead of 8-16;
  - the NKS-7 window lands on filled shelves, which removes the stock-level share of the excess (W cheese about 49%, olives about 24%, meat 78-126%).

**P5 - distribution fixes for cheese, butter, grape, olives and salt:** H1, H2, H3 and H4 (section 3).

**P6 - regional surplus cut guarded (R3.cs LandGlutWeekly, cut at about :348).**
- A land's good is cut only when the market's median town days ≥ T* (nobody short to feed).
- Otherwise the line logs "nadmiar bez ujscia (rozwoz)" and there is no cut.
- Expected: Volantis and Stormlands grape (x0.81) and Volantis oranges (x0.73) come back, about +10% supply where it is needed.

**P7 - NKS-7 measurement (Rule.cs:62-95 PricesDaily).**
- The KS price average (weight 1/4 over 7-day samples) restarts at D+14, the end of the ramp, so the window D+22..D+49 measures KS prices, not pre-KS prices. The log marks the restart.
- New INFO line: instantaneous median per market x good (7-day mean) at D+49.
- With today's data, Westeros butter, oranges and dates, Essos dates and olives already sit in band by D+74.

**P8 - non-food raw goods (wool, flax, cotton, hardwood, clay, ores): diagnose first, then fix.**
- Evidence (log "Towary: dzien 109165"):
  - flax: production 67/d, consumption 208/d, of which town crafts (148) take 188;
  - wool: 97 against 154 (weaveries 69, town crafts 71);
  - cotton Westeros: 59% of towns at the price ceiling, with no Westeros cotton.
- Step 1 (harness): GoodsLedger 28-day per-sink means against the Plan's demand per sink.
- Step 2, one of:
  - (a) the intake of town crafts 148 and workshop lines for a KS raw good is bounded by the product's paid demand (the ZC5 room rule: output ≤ 4 days of purchases);
  - (b) the Plan takes the measured intake as Der.
- Cotton Westeros: if crafts are bounded by product demand, the demand disappears where there is no supply. Otherwise NKS-7b lists it as "no market supply", which is a DECYZJA (inter-market trade).

**P9 - town classes R/P (NKS-3c fails on Essos R 41-54% / P 35-46% of basket, Westeros R/P 88-95%).**
- Harness split of the unpaid R/P basket into food kcal and goods.
- If food is ≥ 95% paid (expected: the class ladder buys food first), NKS-3c gates on food and goods go to INFO.
- If food is short, the cause is town earnings (F3 town money, round 5): a DECYZJA for Jeff in plain words.

**P10 - NKS-5u measurement.** Party food eaten away from a settlement is attributed to the market of the party's last visited town (GoodsLedger frames). Fish shows 13.3% "without market" today.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 5. Granary floor (G1-G3)

**G1 (StartCalibration.Live.cs:236-240 GranaryFloor).** Under the new key `KsGranaryRationsOnly` (default true), a KS village's harvest = **KS rations** (own + home work + bought), with no `max(..., formula)`.
- Barns sit at the 2-year cap (728 days; Night's Watch 590), so hunger shows as barn drawdown, an honest signal.
- An emergency floor applies only when the barn falls below 60 days: `max(rations, formula)`, counted and logged "z niczego (awaryjnie)".
- The old floor counters (R3.cs:501-552) stay as INFO.
- Expected: villages on the floor 51% -> about 0% in the test; floor kcal 3.2% -> 0; hunger deaths 0 for about 4 years even in the worst land.

**G2 - not needed with G1.** The streak rule (R3.cs:529) only feeds the INFO line.

**G3 - DECYZJA for Jeff.** The Plan leaves too much uncovered in a few lands: North 36%, Free Folk 47%, Night's Watch 70%, Skagos 31%, Ibben 20%. These need village money (DS) or land/hands. With G1 their barns empty in years, not in the test.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 6. BK village market and village spending order (B1-B4)

**B1 (BkSeal34.cs RunVillage, merge of DS and KS):**

      ds = DsVillage.On(st); ks = SwPaidFirst && Zc4BasketBudget(village, out ksBud)
      budget = ds ? DsVillage.BkBudget(st, gold) : ks ? min(5% gold, gold - 7 x ksBud) : 5% gold
      loop: if (ks && ZFoodItem(item)) continue;  ...  if (ds || ks) budget -= price;   // a real daily budget, not per item

- Spending is booked as `DsVillage.NoteBkSpent` (DS) or `KsVilOut` (KS without DS, which feeds only the frozen fallback).
- Expected:
  - 52 villages x 478 zl/d -> a budget of at most 1/30 of the surplus above the chest;
  - after the first 2-4 weeks (when old savings are released) the BK market is about 5-8% of village inflow;
  - purses positive.

**B2 - chest nv = Plan-paid basket (M9).**

**B3 (TownFolk.Live.cs village day, merge).** For KS + DS villages:
1. rent (PopulationLaw, before TownFolk);
2. ZC4 ladder (the Plan-paid basket; own food first);
3. E3-W1;
4. chest;
5. DS step 5 (1/2 P5 list, 1/2 BK) from the surplus above the chest.

The old list runs x (1 - lambda) only during the ramp. There is no VillageLadder outside step 5.

**B4 - NKS-29 restated for DS** (section 11): BK spending only from the surplus above the chest, never below it, no food for ZC4 villages, and its share of inflow is INFO with a 10% bound on the last 28 days.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 7. Market controller (C1-C3, NKS-27)

- **C1 (R3.cs:281).**
  - Replace `uf = 1 - h/hs` (market-wide) with a per-town measure: `cu = c28 + U28town`.
  - U28town = 28-day EMA of Σ over towns with shelf < 0.5 T* of (paid demand - 7-day offtake).
  - The per-town unmet is computed daily from the ZC4 map: ZDp, ZOff7, Cnt.
- **C2.** While more than 25% of a market x good's paid demand sits in towns under 0.5 T*, the factor may not go down: a transport fault is not a reason to cut hands.
- **C3.**
  - Bounds: 0.85-1.25 from A to D+28 (the gap term of P4 is active from A, CtlGap 0.25), then 0.85-1.10 with CtlGap 0.05.
  - The NKS-27 step is checked with a tolerance of 0.001: the two "jumps" in round 3 were exactly 7% read from 3-decimal values.
- **Expected:**
  - Essos cheese, grape, dates and butter come back from x0.85 to about 1.0 (+15% supply);
  - fish 0.82 and olives 0.79 stay low, but P3 removes the olive phantom and the fish over-planning is real;
  - at D+49 every food factor sits within 0.90-1.10.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 8. Corridors in the Plan and NKS-24 (K1-K3)

- **K1:** link days and capacities as in H7.
- **K2:** NKS-24 is restated from per corridor to **land-level imports** (section 11). Even the physical ceiling passes only 24 of 74 corridors, because the Plan solves a land-level cheapest route and town merchants use other routes (3.1k/d of today's flow).
- **K3:** the list of exporter lands with no outlet ("bez ujscia") stays in the corridor line (INFO), and NKS-23 uses it.
- **Expected:** importing land x good ≥ 0.8 of Plan imports from 20 of 61 to 40-50 of 61. War-blocked lands (Reach-Dorne) are listed.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 9. Cost (T0-T13; NKS-9, 9b, 28)

[B] = bit for bit, verified by the harness/port tests and by identical log lines on a 10-day replay with the same seed.

| item | change (file:line) | effect (mid estimate) |
|---|---|---|
| T0 | sub-timers in LiveDaily (Live.cs:440-605) | measurement (the 2.66 ms with no timer) |
| T1 [B] | "na targ" block of PlanLines (Plan.cs:484-505) on the worker thread, which already runs BuildLive + ComputeDistrict in PlanCsvText (:518-553); KsResult carries mk/tf/own/kcal/need/nd | pickup day -20 ms; q95 28.1 -> about 11 |
| T2 [B] | RuleDaily: V[town][occupation] = Σ_g mv x Px once, Income = L x V - KCost - Rent x Ha (Rule.cs:160-166, 201-205), same addition order | A7 days -3.6 ms |
| T3 [B] | NearTowns: session cache of GetDistance per (village, town), same 7-day rebuild and sort (TownFolk.Ks.cs:493-506) | -1.25 ms on 2 of 7 days |
| T4 [B] | versioned cache of StartIndex, LandGradZ, P2Price, P2Floor per (market/land, z), version bumped on `_plan`, `_grad`, `_landCut` (Zc4.cs:823, R3.cs:909-923) | village purchases -0.85 ms |
| T5 [B] | ZcBuy: resolve ZTown once, memo (price, have) per z within one call (TownFolk.Ks.cs:95-120, 389, 417, 278-319) | -0.65 ms |
| T6 [B] | RampZ as int[33]; skip dpM/pipe/target/ForEachInTransit when t > RampDemand (Ramp.cs:156-193) | -0.3 ms |
| T7 [B] | Zc4Build shelf scan: item-type mask + RefEq dictionary, skip GetPrice for z nobody reads, daily StartIndex table for the cap (Zc4.cs:296-313), IndexOf lifted (:351), same filter in the ramp scan | -0.4 ms |
| T8 [B] | ZC5: one pass over SoldItems, local counts instead of GetItemNumber, PriceIndex memo per call (Crafts.cs:115-124, 179, 192, 205) | -0.28 ms |
| T9 [B] | KS-H: type filter in the scan (KsFoodCarts.cs:521-527); Decide split Prepare/Run, Ix only where offer ≥ 1 or eligible target; ZDp/ZOff7 lifted; war memo per pair; days array direct | -0.35 ms |
| T9b | KS-H LotPrice: 2 GetPrice calls + power-law integral instead of 3-point (not [B], but the 1/64 exact probe checks it; it also fixes NKS-22 "probka max 11.8%") | -0.1 ms, probe error under 1% |
| T10 [B] | K4a skip unchanged districts on non-step days; K5 KsSnapshot without copy; K7 FloorDaily one pass; K8 lines formatted off the main thread; L arrays in Zc4Ladder; S skip the KsUnmet loop after D+49, guarded on DDay | about -0.4 ms (+ -0.35 after D+49) |
| T11 [B] | RefreshNeedMult, MoneyLines and PlanLines strings on the worker (Live.cs:631-655) | pickup -1 ms |
| T12 | ShelfLine counts from the KS-H Ix snapshot (log only) | -0.08 ms |
| T13 | town fields every 2 days by Fnv parity (Live.cs:556-565, 695-736); **only if** T0 shows the median still > 3 ms | -0.37 ms |

**New costs in round 4:**
- H2 far towns: +0.3-0.5 ms, held down by the offer-first check;
- H4 DestValue: one Pow per lot, ~0;
- credit PayDaily: ~0.05 ms at 1000 bills;
- more KS-H lots (about 136 -> 200/d at 4.5 us): +0.3 ms;
- DS hooks (cart, rent, chest): measured in the nds line, not in NKS-9b.

**Projection:**
- NKS-9 median 3.2-3.6 ms: the 3 ms target is not secured; T0 decides T13;
- NKS-9 q95 about 11 ms (limit 15);
- NKS-9b about 9.0-9.6 ms (limit 10; thin margin);
- NKS-28 about 1.4-1.6 ms (limit 1.5; thin; T9 + offer-first is the margin).

---------------------------------------------------------------------------------------------------------------------------------------------------

## 10. Keys, save, Jeff's Ironman

- **New MCM keys** (no existing default changes; the MCM pitfall from CLAUDE.md):
  - KsAlphaRules true;
  - KsFoodCartsConsign true, KsConsignFloorShare 0.5, KsConsignSourceExposure 0.5, KsConsignOverdueDays 30;
  - KsShipByDays true, KsFarAlways true, KsRelKeep true;
  - KsMeatYield true, KsFoodSubstituteGate true, KsGapMargin true;
  - KsGranaryRationsOnly true;
  - **DsWithoutKs false**: a DS F1 village needs KS applied unless this key is on. The DS branch acts without KS; turning that on in Jeff's game is Jeff's word.
  - DS F3 `DsTowns` stays false (round 5).
- **Save:**
  - new `arm_ks_r4`: O_v and m_v per village, PhiPlan W/E, frozen alpha, gap factors f, per-town U28;
  - `arm_daniny` (DS);
  - `arm_daniny_kupcy` (bills);
  - `arm_ks` stays ks3 and `arm_ks_r3` keeps its format, so the round-3 DLL reads them and skips the new keys;
  - the KS-H day table goes to "T" (one-day rebuild);
  - NKS-20: arm_ks + arm_ks_r3 + arm_ks_r4 ≤ 150 KB, arm_ks_kupcy ≤ 64 KB, arm_daniny_kupcy ≤ 64 KB.
- **Jeff's game:**
  - KS Apply stays blocked (LandGateReady = false; Arm/Confirm need 28 session days), so no KS-R4 rule acts there;
  - DS F1 does not act without KS (DsWithoutKs false);
  - nothing is tested or deployed while Jeff plays;
  - deployment only after all gates pass **and** Jeff's word (DS is a big economy change), with a DLL backup.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 11. Checker changes (tools/sprawdz_logi.py)

| check | change | why |
|---|---|---|
| NKS-3 | NA TARG divided by the gap margin f per good (from the Plan line) | f is intended stock building from real hands, not imbalance |
| NKS-3c | village part = "alfa z praw" median ≥ 0.40 W and E (with NDS-4); R/P per P9 (food ≥ 95%; goods INFO) or DECYZJA | alpha_net is gone; R/P is town money (F3) |
| NKS-7 | window unchanged; reads the price line restarted at D+14; + NKS-7d INFO instantaneous median at D+49 | the old line carried pre-KS prices |
| NKS-22 | under Consign "kasa celu za mala" -> "cel z zaleglym" ≤ 5% of ordered; probe mean ≤ 1%, max ≤ 3% | the destination no longer pays upfront |
| NKS-24 | every importing land x good with Plan import ≥ 50/d gets ≥ 0.8; per corridor INFO; war lands listed | the per-corridor test fails by construction (24/74 at the physical ceiling) |
| NKS-26 | villages on the emergency floor ≤ 2%, kcal "z niczego" ≤ 0.5%; barn drawdown by land INFO | the always-on floor is removed (G1) |
| NKS-27 | step tolerance 0.001 | rounding |
| NKS-29 | DS villages: BK only above the chest (purse after BK ≥ chest), no food for ZC4 villages; share of inflow INFO, ≤ 10% over the last 28 days | DS defines BK as surplus spending; 2% conflicts with DS |
| NKS-5u | consumption away from a settlement attributed to the market (P10) | measurement |
| nds | DS group NDS-1..9 from the DS branch on the merged build; + NDS-10 INFO: DS hooks time | DS acceptance |

Restated gates are not loosened targets. Each states the same goal measured where the mechanism now lives. Every restatement goes into the report to Jeff.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 12. Simulator first (stages B and E)

Tools:
- `ksim/sim/r5port.py`: r4port + the round-4 rules + DS village order + credit KS-H;
- `kmoney/h5`: the Plan harness with the DS budgets;
- `kmoney/port5`: the C# KsFoodCore with the new flags.

Start from the same KS dump and the round-3-final CSVs. 3 seeds, 120 days, window D+22..D+49.

**S-0 calibration (before any rule changes):** r5port with the round-3 rules plus the two "game effects" the sim lacks (artisans' meat 6/2/1 per head taking 79-97% of livestock; B2 phantom demand by category and market from the log totals) must reproduce the round-3 final game:
- grain shelf median 3.6 ± 0.6 days;
- 14-16% of towns under 0.5 days (± 3);
- KS-H 12.3k ± 10% units/day;
- destination coffer blocked 24 ± 5%;
- NKS-7 medians ± 20%: meat W 3.1 / E 3.7, cheese 2.4 / 2.1, grape 2.7 / 1.9, olives 2.2 / 1.5;
- BK 17-24% of village inflow;
- village purses -14k/d ± 30%.

If S-0 fails, the sim is not trusted for that good, and the item goes to the game test as a diagnosis, not as an acceptance.

**S-1 Plan harness h5 (DS budgets):**
- NA TARG / f in 0.95-1.05 for every good on 5 warm Plans (round-3 exceptions), with meat in band under the one slaughter table;
- alpha from the rules: median ≥ 0.40 W and E, p10 ≥ 0.30, no drop > 0.05 from Plan to Plan;
- world village budget 140-180k/d;
- fixed point convergent in both markets within 14 rounds;
- Plan uncovered kcal E ≤ 3.5%, W reported by land (DECYZJA lands);
- Plan time ≤ 25 s;
- keys off = h4 bit for bit.

**S-2 prototypes (each rule alone, then all together):** meat yield, B2 off, P3, P4, H1-H5, C1-C3, P6, G1, B1. This step fixes the parameters:
- keep 1.1 x median / 0.5 T*;
- offer 0.67 (staples 1.0);
- unit cap 3 x start;
- floor 0.5 and exposure 0.5;
- gap cap 1.25;
- CtlGap 0.25 until D+28.

**S-3 acceptance on the ported C# rules** (gates must hold in all 3 seeds):

| gate | target |
|---|---|
| S-G1 | grain S/C 0.95-1.05 in both markets |
| S-G2 | raw food S/C: at most 4 of 28 out (exceptions ±10% of the Plan) |
| S-G3 | grain towns: < 0.5 d ≤ 5%, < 2 d ≤ 10%, q90 ≤ 3 x T*, none > 30 d outside no-outlet lands |
| S-G4 | NKS-7: grain + ≥ 24 of 26 foods in start ±0.3; meat, cheese, butter, grape, olives, oranges and dates listed one by one |
| S-G5 | ceiling/floor ≤ 20% of towns for every good |
| S-G6 | uncovered kcal E ≤ 5%, W ≤ 8% (lands listed) |
| S-G7 (DS S3) | purse drift ≥ -0.2%/d, median ≥ 0.9 x start, villages < 1000 zl ≤ 15% |
| S-G8 (DS S4) | KS-H: 15-35k units/d, haul ≤ 12 d, in transit ≤ 250k, blocked by an overdue destination ≤ 5%, open bills ≤ 10 days of deliveries, bills > 30 days ≤ 10%, Pentos gets grain, merchant margin median ≥ 0, identities 0 |
| S-G9 | D+7: ≥ 50% of market medians in band |
| S-G10 | land imports: ≥ 0.8 for every importing land x good ≥ 50/d (war lands listed) |
| S-G11 | NKS-23b: paid demand in towns < 0.5 T* ≤ 10% per food (meat ≤ 20%) |
| S-G12 | meat: market output ≥ 0.95 x Plan; meat index median ≤ 1.3 in the window |
| S-G13 | granary: emergency floor ≤ 2% of villages, kcal "z niczego" ≤ 0.5% |
| S-G14 | BK: never below the chest, food 0, ≤ 10% of inflow over the last 28 days |
| S-G15 | controller: |k - 1| ≤ 0.10 for every food at D+49; no cut while > 25% of demand is in empty towns |
| DS S1 | Plan convergent, alpha from the rules median ≥ 0.40, p10 ≥ 0.30, budget 140-180k/d |

- **Stress (3 seeds each):**
  - Reach-Dorne war;
  - harvest -30% for 28 days: purses ≥ -0.3%/d, alpha ≥ 0.25, hunger deaths 0, overdue bills ≤ 15%.
- **Port tests:** port5 identical lots C# = Python (flags on); flags off = 1251 lots; h5 keys off = h4.
- **Cost:** micro-benchmark `ksim/r4/bench` with DestValue (≤ 0.1 us), PayDaily (≤ 0.05 ms at 1000 bills), KS-H with FarAlways (≤ +0.3 ms in the port, net8).
- **Go / no-go:** the game test runs only when every S gate is TAK, or only items marked DECYZJA remain (G3 lands, P9 classes, cotton). Otherwise iterate stage C/E, at most 3 rounds.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 13. One in-game test, 120 days

**Setup**
- Armoury from the merged int (ks4) after stage E.
- Autotest flag `CrashScribe/armoury-ks-wlacz.flag` (delete it after the test).
- DuesMedieval on (DsWithoutKs false, so DS starts at A with a 14-day ramp), DsTowns off.
- 1 battle (`test/bieg-bitwa.ps1 -Battles`).

**Before the test**
- Jeff is not playing.
- Copy Jeff's logs (Armoury keeps 12 logs).
- Touch the game folder only by script, with a backup.

**Run**
- D+7 early control: log only; run to the end for the data.

**After the test**
- Copy the log and the CSV folder to `D:/Backup-Bannerlord/logi-testow`.
- Delete the flags.
- Restore the approved DLLs.
- Run sprawdz_logi: nks, nds, nzc0, nzc1, nzc2, nbitwa.
- Write the report (plain words for Jeff) and update STAN-PRAC.
- Deploy only after all gates pass and with Jeff's word.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 14. Expected nks after round 4 (estimate; round 3: 24 of 53 not passed)

| check | round 3 | round 4 expected | carried by |
|---|---|---|---|
| NKS-3 Plan NA TARG | NIE (W grain 1.07) | TAK (risk: the Arbor district flips olives/salt/oranges) | P4/f in the check, M1 |
| NKS-3c money | NIE (R/P E 54/46%) | village part TAK (alpha about 0.45); R/P: TAK if food ≥ 95%, else DECYZJA | M1-M9, P9 |
| NKS-5 window S/C | ETAP 31/46 out | ETAP about 10-16/46 out (non-food P8 decides; food mostly in band) | P1, P3, P4, H1-H5 |
| NKS-5b exceptions | NIE | NIE likely (hides fixed; whale oil, spice, fur, carrot, honey remain) | P1 |
| NKS-5u off-market | NIE (fish 13%) | TAK | P10 |
| NKS-5e first week | NIE 36/46 | NIE (transitional, about 20/46) | - |
| NKS-6 stocks | ETAP 29 | ETAP about 10-15 (still filling long-T* goods; non-food drains) | P4, P8 |
| NKS-7 prices | ETAP 42 out | ETAP about 6-12 out (food mostly in band; meat borderline; olives W/Dorne, dates W; non-food) | P1-P7, H |
| NKS-7b ceiling/floor | ETAP (cotton W 59%, iron E 23%) | ETAP/DECYZJA (cotton) | P8 |
| NKS-7c slope | NIE | TAK likely (meat may still rise while filling) | P7 |
| NKS-7e D+7 | NIE | NIE likely (shelves still filling at D+7) | - |
| NKS-8c uncovered | ETAP W 12.6 / E 5.5% | W about 8-10% (DECYZJA lands), E about 3-4% | M1, G3 |
| NKS-13 basket | NIE | TAK or ETAP | M1, B3 |
| NKS-13b ladder 1 | NIE (no goods 12%, no purse 9-12%) | no purse about 2-4% TAK; no goods about 5-8% borderline | M1, H |
| NKS-22 KS-H | NIE (coffer 24%, probe 11.8%) | TAK | H5, T9b |
| NKS-23 grain towns | ETAP | TAK or ETAP (< 0.5 d about 5-8%) | H1-H3 |
| NKS-23b | NIE (21-93%) | NIE for meat and W cheese, others about 10-20% | H3, P1, P4 |
| NKS-24 corridors | NIE 71/80 | land-level 40-50 of 61; strict "every land ≥ 50/d" likely NIE in war lands | H1-H5, H7 |
| NKS-26 granary | NIE 51% | TAK | G1 |
| NKS-27 controller | NIE 20 | TAK likely | C1-C3 |
| NKS-29 BK | NIE 17-24% | TAK (restated) | B1, B4 |
| NKS-9 cost | NIE median 4.89, q95 28.1 | q95 TAK; median 3.2-3.6 NIE unless T0/T13 cut more | T0-T13 |
| NKS-9b | NIE 14.5 | TAK thin (about 9-9.6) | T1-T11 |
| NKS-28 | NIE 1.76 | borderline 1.4-1.6 | T9 |
| nds NDS-1..9 | - | TAK likely; NDS-7 crown in the ramp -5..-8% (gate 0.85) | DS |

**Total:** about 8-12 of 53 not passed (round 3: 24), most of them ETAP or DECYZJA:
- NKS-5b, 5e, 7e;
- one or two of NKS-5, 6 and 7;
- NKS-9 median;
- NKS-24 war lands;
- NKS-23b meat;
- NKS-8c West.

**Remaining risks**
1. **Westeros structural hunger:** North 36%, Free Folk 47%, Night's Watch 70% uncovered in the Plan. NKS-8c W cannot pass from transport (DECYZJA G3).
2. **Essos town classes R/P** can pay only 41-54% of their basket. If food is part of the shortfall, it is a round-5 matter (F3 town money) or a decision.
3. **Plan instability:** the Arbor district (294k people) flips between olives and salt; oranges/olives in W can jump between Plans.
4. **Non-food raw goods** (flax 3x over-consumed by town crafts 148, wool, cotton in W): the cause is not yet measured per sink. P8 may need a round of its own.
5. **Crown in the DS ramp:** -5..-8% of daily income on the worst days. Army refunds are untouched (rents absorb it); small kingdoms without rents lean on the Bank as today.
6. **Release of savings at BK** in the first 2-4 weeks after the DS ramp may lift non-food prices for a while.
7. **Meat fill:** reaching T* needs P1 + P4. If artisan line patching fails at Apply (shared XML lists), meat stays at 50%. The log line "KsMeatYield" must show the patched lines.
8. **B2 attribution was inferred, not measured.** If grape W stays at 2x after P2, the residual is the game's own CanSubstitute shift (not logged), the next suspect.
9. **Credit:** poor towns (Pentos, Lys) may run into the 30-day overdue rule and stop receiving food. Watch the "cel z zaleglym" counter and NKS-23 for them.
10. **Cost:** the NKS-9 median of 3 ms is not secured without sub-timer findings; NKS-28 and NKS-9b have thin margins.
11. **Merge risk:** DS was built from WGRANIE 74 and touches 10+ files that KS also changed; the scal.sh pitfalls apply.
12. **One game test only:** D+7 will likely say "abort" (shelves still filling). The run goes to the end for data, and the acceptance is the window D+22..D+49.

---------------------------------------------------------------------------------------------------------------------------------------------------

## 15. Dla Jeffa (prosto)

1. Runda 4 = daniny sredniowieczne (DS) + kupcy jedzenia na kredyt + poprawki z testu 120 dob. Nic nie wgrane, Twoja gra i zapis bez zmian.
2. Wies dostaje budzet z praw (jej czesc utargu po panu, dziesiecinie i koronie, minus czynsz), nie z tego, co zostalo w kiesie - koniec petli, w ktorej wsie przestawaly kupowac.
3. Kupcy wioza jedzenie na swoj koszt; miasto placi przy dostawie, czego nie ma - zostaje rachunek; kto nie splaca 30 dni, dostaw nie dostaje.
4. Mieso: rzezie w miastach daja z krowy, swini i owcy tyle miesa, ile liczy Plan (dzis polowe) - koniec braku miesa.
5. Ser, winogrona, oliwki: wylaczam przy KS stary "zamiennik jedzenia" (dopisywal sztuczny popyt), kupcy sprzedaja z miast z zapasem wiekszym niz srednia, statkiem tam, gdzie szybciej.
6. Spichlerz wsi bez darmowej podlogi: glod widac jako spadek zapasu (zapasy sa na 2 lata).
7. Kolejnosc: najpierw symulator, potem jeden test 120 dob w grze; wgranie tylko po Twoim slowie.
8. Do Twojej decyzji: glod Polnocy, Wolnych Ludzi i Nocnej Strazy (ich ziemia nie wyzywi ludzi) oraz biedni rzemieslnicy Essos (zarabiaja na 40-55% koszyka).
9. Korona w pierwszych tygodniach DS traci ok. 5-8% wplywow dnia; zold armii bez zmian (niosa to renty).
10. Spodziewam sie ok. 8-12 z 53 bramek niezaliczonych (dzis 24) - nie bedzie to jeszcze "gotowe".
