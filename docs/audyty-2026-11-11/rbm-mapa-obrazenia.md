# MAPA: rdzen obrazen RBM (wrecz, kopia, kon, upadek) - audyt, tylko czytanie

Zrodla. Numery linii "rbm:N" = `bad4/rbm.cs` (moja dekompilacja RBMCombat.dll + RBMConfig.dll, 6937 linii; kopia `bad4/rbm_mine.cs`).
Silnik: `rally_dec/mb_full.cs` ("mb:N"), `bad4/obr_csc.cs` (TaleWorlds.Core.CombatStatCalculator), `bad4/obr_sbsm.cs` (SandboxStrikeMagnitudeModel),
`bad4/obr_sbad.cs` (SandboxAgentApplyDamageModel). Kalkulator odwzorowujacy wzory: `bad4/obr_calc.py` (python obr_calc.py).
Konfiguracja Jeffa (`Documents/.../Configs/RBM/config.xml`): ArmorMultiplier 2, ArmorThresholdModifier 1, BluntTraumaBonus 0, MaceBluntModifier 1,
ThrustMagnitudeModifier 0.05 (=> One/TwoHandedThrustDamageBonus = 1/0.05 = 20, rbm:6570-6572), Posture 1, Stamina 1. Wszystko = wartosci domyslne RBM.
Pociski, tarcze i postura - osobna mapa (`bad4/mapa-pociski.md`); tu tylko wzmianki.

## 0. Wnioski w piecu zdaniach
1. RBM NIE jest symulacja fizyczna w cieciu i obuchu: fizyka zamachu daje 3-40 jednostek, po czym `GetSkillBasedDamage` dodaje skladnik z umiejetnosci
   i WCISKA wynik w waskie okno [min, max] zalezne tylko od klasy broni i umiejetnosci. Masa, dlugosc, wywazenie i szybkosc broni w obrebie klasy prawie nie graja.
2. Pancerz dziala progowo: ponizej progu (pancerz x 2-5) NIC nie przebija i zostaje tylko "uraz tepy" = 10-35% (obuch 70%) sily x 100/(100 + 2 x pancerz).
   Skutek: skora ROT (61), kolczuga (88) i plyta (120) daja prawie to samo przeciw mieczowi (8 / 6 / 5 HP przy umiejetnosci 100).
3. "Obrazenia" broni z karty przedmiotu (damage_factor) nie zwiekszaja sily ciosu - tylko obnizaja prog przebicia (dzielnik sqrt). Przeciw ciezkiej zbroi
   jakosc i zuzycie ostrza nie maja ZADNEGO wplywu na wynik.
4. Trzy rzeczy wygladaja na bledy autora RBM, nie na decyzje: mlot dwureczny (wspolczynnik 1.125 zamiast ~0.11 - zawsze maksimum okna), martwy kod
   "czesciowego przebicia" pchniec (znosi sie do zera), premia +30% pancerza dla WYPOCZETEJ ofiary liczona odwrotnie niz sugeruje nazwa.
5. Na styku z naszym Armoury jest realne ryzyko: bron w stanie Wrecked (-82% przez nasza latke ModifyDamage) przy niskim damage_factor daje sqrt z liczby
   ujemnej = NaN = 0 obrazen (21 ze 171 grotow/kling ROT w pchnieciu, ~100 z 235 kling RBM/vanilla w cieciu). Do sprawdzenia w grze.

## 1. Lancuch: od zamachu do HP
Silnik (mb:76703 GetAttackCollisionResults) -> ComputeBlowMagnitude (mb:77121) -> [Melee mb:77147 | Missile | Fall | HorseCharge] -> ComputeBlowDamage (mb:77000)
-> Mission.GetAttackCollisionResults (mb:58469) -> AgentApplyDamageModel.CalculateDamage (mb:142456: perki, sztandary, mnozniki agenta) -> Blow.

Latki RBM (wszystkie prefiksy zwracajace false = pelne zastapienie):
| Cel silnika | Latka RBM | rbm |
|---|---|---|
| MissionCombatMechanicsHelper.CalculateBaseMeleeBlowMagnitude | MagnitudeChanges.CalculateBaseMeleeBlowMagnitudePatch | 672-840 |
| CombatStatCalculator.CalculateStrikeMagnitudeForPassiveUsage (kopia couched) | MagnitudeChanges.ChangeLanceDamage | 860-875 |
| CombatStatCalculator.CalculateStrikeMagnitudeForThrust (zapas, gdy brak agenta) | CalculateStrikeMagnitudeForThrustPatch | 877-896 |
| ComputeBlowMagnitudeMissile | ComputeBlowMagnitudeMissilePatch -> CalculateMissileMagnitude | 843-858, 1424-1526 |
| ComputeBlowMagnitudeFromHorseCharge | HorseChanges.ChangeHorseDamageCalculation | 2921-2944 |
| ComputeBlowDamage (pancerz -> HP) | DamageRework.OverrideDamageCalc.Prefix | 3349-3612 |
| ComputeBlowDamageOnShield | RBMComputeBlowDamageOnShield | 3614-3800 |
| Agent.GetBaseArmorEffectivenessForBodyPart | ArmorRework.ChangeBodyPartArmor | 10-19, 459-509 |
| ItemModifier.ModifyArmor / ModifyDamage (modyfikator = PROCENT, nie dodatek) | ModifyArmorPatch / ModifyModifyDamagePatch | 587-651 |
| GetEntityDamageMultiplier (obiekty, machiny) | GetEntityDamageMultiplierPatch | 3265-3301 |
Innych modow na tych celach nie ma (skan DLL wszystkich modulow po nazwach metod; BKROTPatch.ROTCalculateDamagePatch celuje w nieistniejaca
w 1.4.8 metode ComputeBlowDamageOnAgent - Prepare zwraca false). Nasz Armoury dotyka tylko ModifyArmor/ModifyDamage (Patches.cs:634, 654 ConditionScaling),
ComputeBlowDamageOnShield (ShieldGuard.cs:56) i CalculateDamage (FriendlyFire.cs:108).

### 1a. Umiejetnosc (uzywana wszedzie)
- `Utilities.GetEffectiveSkillWithDR(s) = 600/(600+s) x s` (rbm:5283): 30->28.6, 100->85.7, 200->150, 300->200.
- `Utilities.CalculateSkillModifier(s) = clamp(s/250, 0, 1)` (rbm:5273), dalej "sm".
- Vanillowa premia obrazen z umiejetnosci (SandboxAgentStatCalculateModel.GetWeaponDamageMultiplier) jest KASOWANA: `magnitude = BaseMagnitude / weaponDamageMultiplier` (rbm:3356).

### 1b. Ciecie / zamach (rbm:806-838)
progressEffect podloga 0.5 (pchniecie 0.1); n = sqrt(progress).
`omega = SwingSpeed/4.5454545 x n x K x (1 + DR/D) x n` - n wchodzi DWA razy (energia ~ progress^2).
K/D: miecze i sztylet 0.83/800; topor 1h, buzdygan 1h i 2h, drzewcowa 1h i 2h 0.83/1000; topor 2h 0.75/800.
`M_fiz = CombatStatCalculator.CalculateStrikeMagnitudeForSwing(omega, punkt, masa, dlugosc, inercja, srodekMasy, predkoscDodatkowa)` (obr_csc.cs:21-35) =
0.067 x (energia przed - energia po zderzeniu w punkcie trafienia + 0.5). Roznice wobec vanilli: JEDNA probka w punkcie trafienia (vanilla: najlepsza z 5,
punkt max 0.93) - trafienie czubkiem lub przy rekojesci realnie oslabia; predkosc dodatkowa (kon, bieg) wchodzi w 100% (vanilla x0.7).
Trafienie drzewcem/rekojescia (`Utilities.HitWithWeaponBlade` rbm:5211: punkt < dlugosc - dlugoscOstrza - 0.15) zmienia typ na Blunt (rbm:3398-3402).

### 1c. Pchniecie (rbm:696-787, 5237-5271, 6150-6214)
Statystyka "thrust speed" broni jest IGNOROWANA. Predkosc z symulacji trzech "warstw" ciala (`SimulateThrustLayer`, moc/sila/masa: 250 W/48 N/4 kg,
170/24/2, 90/15/0.5, droga 0.6 m): `v0 = 3.85 / (0.33 x suma czasow)`, masa ruchoma = 1.8 + waga + 0.2 x (inercja + waga x CoM^2). Daje 5.4-8.8 m/s.
`v = v0 x K x (1 + DR/D) x n`: miecze 0.7/800, drzewcowa 2h 0.65/800, drzewcowa 1h i buzdygany 0.75/1000, topor 2h 0.9/1000.
Jednoreczne (`CalculateThrustMagnitudeForOneHandedWeapon`): pchniecie z gory v x1.33, v max 9; masa efektywna = waga + R x (1 + 2 x sm(DR)), R = 2.5 (z gory 1.5);
`E = min(0.5 x mEf x v^2 + 0.5 x waga x (v + vDod)^2, LIMIT)`, LIMIT = clamp(1.5 x 0.5 x mEf x v^2, 0, 180 J).
Dwureczne: v max 6 (z gory +1), mEf = R x (1 + 2 sm), R = 5 (z gory 4), LIMIT max 250 J. Wynik x 0.05, a w RBMComputeDamage z powrotem x 20 - czyli dzule.
Kary: drzewcowa trafiona nie grotem (punkt < 0.9 z gory / < 0.7 z dolu) x0.2 (rbm:780-783); miecz pchniety nie czubkiem (punkt/dlugosc < 0.85,
`ThurstWithTip` rbm:5184) -> typ Cut (rbm:3403-3408). Silnik: trafienie reka zamiast bronia x0.034 (ThrustHitWithArmDamageMultiplier).

### 1d. GetSkillBasedDamage - okno umiejetnosci (rbm:5669-5947) - SERCE uproszczenia
Dla M > 1: `M = clamp(M_fiz + DR x a, MIN x (1 + sm), MAX x (1 + 2 sm)) x k`
| Klasa | Ciecie a / MIN / MAX / k | Obuch (rekojesc lub typ broni) a / MIN / MAX / k |
|---|---|---|
| Miecz 1h, sztylet | 0.133 / 5 / 15 / 4.6 | 0.075 / 15 / 20 / 1.6 |
| Miecz 2h | 0.199 / 12 / 20 / 4.6 | 0.112 / 20 / 26 / 1.6 |
| Topor 1h | 0.1 / 10 / 18 / 4.6 | 0.075 / 15 / 20 / 1.2 |
| Topor 2h | 0.15 / 15 / 24 / 4.6 | 0.112 / 20 / 26 / 1.2 |
| Buzdygan 1h | 0.075 / 10 / 15 / 4.6 (kazdy typ poza Pierce) | - |
| Mlot/buzdygan 2h | **1.125** / 15 / 22 / 4.6 (rbm:5801) | - |
| Drzewcowa 1h | 0.1 / 15 / 24 / 4.0 | 0.075 / 15 / 20 / 1.2 |
| Drzewcowa 2h | 0.1495 / 18 / 28 / 4.0 | 0.0975 / 20 / 26 / 1.2 |
Pchniecie (Pierce + Thrust) przechodzi BEZ okna. Zakres okna miecza 1h: umiejetnosc 0 -> M 23-69; 100 -> 32-124; 250+ -> 46-207.
Mlot 2h: DR x 1.125 przy umiejetnosci 20 daje juz +21.8 > MAX - wynik ZAWSZE = MAX okna (101 przy 0, 182 przy 100, 304 przy 250), niezaleznie od zamachu.
Wszystkie pokrewne wspolczynniki to 0.075-0.199; 1.125 to niemal na pewno literowka (0.1125 = 0.075 x 1.5).

### 1e. Kopia z konia - couched (rbm:860-875 + 5845-5886 / 5900-5941)
Silnik: `extra = predkosc wzgledna x cos` (+ perki Lancer itd., obr_sbsm.cs); piechur kontra piechur = 0. RBM: `M0 = 0.5 x waga x extra^2 x 0.05`.
GetSkillBasedDamage (isPassiveUsage): masaReki = 0.5 + 0.02 x DR (+0.5 gdy bron < 2.1 kg); `E = 0.5 x (waga + masaReki) x v^2`;
PLATEAU = 150 + 1.5 x DR [J]: jesli E > PLATEAU, a energia samej broni < PLATEAU -> E = PLATEAU; jesli sama bron >= PLATEAU -> E = energia broni; sufit 430 J.
Typ Blunt (tepa kopia): sufit 260. Trafienie zawsze przewraca (BlowFlags.KnockDown, rbm:3833-3837). Zrzucenie z konia: BaseMagnitude > 2.4 + 0.01 x jezdziectwo ofiary
(glowa/szyja) albo > 3 + ... (tulow/barki) (rbm:2897-2917).

### 1f. Szarza konia (rbm:2921-2944, 2752-2769)
`M = predkoscWzgledna x cos x MountChargeDamage / 70`, MountChargeDamage = masa konia + zbroja konia + jezdziec + jego pancerz + bron + 100 (kg).
Vanilla: v^2 x cos^3 x wlasnosc konia. Typ Blunt, klasa "otherDamage" (wspolczynniki 1). Kon dostaje odbicie: ComputeRawDamage(Blunt, M/6, pancerz piersi) (rbm:3917-3923).
Uwaga na skale ROT: uprzeze ROT waza 70-140 kg (RBM: 8-21 kg) -> szarza ROT o ~15% mocniejsza i kon wolniejszy niz zaklada RBM (rbm:2737-2749).

### 1g. Upadek (mb:77249) - bez zmian RBM
`M = (vz^2 x 0.575 - 54) x sqrt(1 + obciazenie/masa)` (x1.41 dla konia; jezdziec vz x0.77); < 3 -> 0. FallDamageMultiplier/Absorption w RBM = vanilla.
W RBM ComputeBlowDamage pancerz = 0 i brak mnoznika czesci ciala: obrazenia = M. Przyklad: 10 m/s (ok. 5 m) -> 4; 14 m/s (10 m) -> ok. 69; 17 m/s -> ok. 130.

## 2. Pancerz: od sily do HP
### 2a. Ktora liczba pancerza (ArmorRework rbm:459-509)
glowa = helm.head; szyja = helm.arm + korpus.arm; barki = naramiennik.(body + arm) + korpus.arm; piers = brzuch = korpus.body; rece = rekawice.arm;
nogi = 0.5 x (buty.leg + korpus.leg). Material (plyta/kolczuga/skora/sukno) czytany osobno (rbm:511-557).
Poprawki w OverrideDamageCalc: perki (CalculateAdjustedArmorForBlow, obr_sbsm.cs); trafienie w twarz (z przodu, normalna w dol) -> pancerz = helm.BODY_armor,
bez helmu x0.15 (rbm:3411-3474); pod pache (kosci 15/22) -> korpus.arm + naramiennik.arm (rbm:3475-3497); ofiara lezaca / odrzucona x0.75 (rbm:3510);
kondycja: sila atakujacego x lerp(0.85, 1, stamina), pancerz ofiary x (0.3 + max(0.7, stamina)) = x1.0 (wyczerpany) do x1.3 (wypoczety) (rbm:3545-3562).

### 2b. Wzor (Utilities.RBMComputeDamage rbm:5949-6040, WeaponTypeDamage rbm:6042-6148)
```
df      = SwingDamageFactor albo ThrustDamageFactor broni + (ModifyDamage(100) - 100)/100      (rbm:6349-6367)
T       = pancerz x PROG x ArmorThresholdModifier / sqrt(df)
przebite = max(0, M - T)
uraz     = M x TEPY x (M - przebite)/M x 100/(100 + pancerz x ArmorMultiplier[2])
HP       = floor(floor(przebite + uraz) x mnoznikCzesciCiala x trudnosc) ; potem perki CalculateDamage
```
PROG / TEPY (config Jeffa): miecze ciecie 5 / 0.25, pchniecie 3.5 / 0.35; topory ciecie 5 / 0.3; drzewcowe ciecie 5 / 0.3, pchniecie 3 / 0.35;
buzdygany ciecie 4 / 0.1, pchniecie 2 / 0.25; KAZDY obuch 5 / 0.7; strzala i belt pchniecie 2 / 0.15 (i pancerz x0.5, gdy material nie jest plyta, rbm:5951-5954);
oszczep 3 / 0.2; kon, piesc, inne 1 / 1 (obuch 5 / 0.7).
"Czesciowe przebicie" pchniec (num7 do 15, rbm:6106-6117) odejmuje sie i dodaje z powrotem - wynik identyczny jak bez niego (martwy kod).
Zaokraglenie w dol (vanilla w gore) - drobne ciosy daja 0; HandleBlowPatch (rbm:3979-3993) podnosi do 1 tylko ciosy z odrzutem/powaleniem/kopniakiem.

### 2c. Mnozniki czesci ciala (GetBodyPartDamageMultiplier rbm:4023-4073; vanillowy DamageMultiplierOfBone jest ignorowany)
glowa i szyja 1.5 | brzuch 1.0 | piers 0.9 | barki: ciecie/pchniecie 0.6, obuch 0.7 | rece i nogi: obuch 0.7, ciecie 0.6, pchniecie 0.5.
(Vanilla: glowa 1.2-2.0, nogi 0.8, reszta 1.0.)

### 2d. Skala pancerzy ROT pod RBM
ROT.dll `ROT.HarmonyPatches.ModCompatibility.RBM.DeserializeArmorComponent` (RBMIntegration = true w RealmOfThrones_v1.json) mnozy pancerze ROT przy wczytaniu:
korpus plyta x2.4, kolczuga x2.1, skora x2.2, sukno x0.7; helm plyta x2.5, kolczuga x1.5, skora x1.25; naramiennik plyta x4.1; rekawice i buty plyta x2.35.
Helm bez body_armor dostaje body = head/2 (twarz). Klingi ROT: damage_factor x0.4. Tarcze ROT: HP x1.75 (drewno) / x4 (metal).
Mediany po przeliczeniu (ROTassets.xml): korpus plyta 120 (72-136), kolczuga 88 (42-207), skora 61 (22-176), sukno 14; helm plytowy 97 (62-200); naramiennik plytowy 65 + arm 36;
rekawice plytowe 56; buty plytowe 58. RBM dla wlasnych (vanillowych) przedmiotow: korpus plyta mediana 77, kolczuga 47, skora 18, sukno 8; helm plytowy 85.
Rycerz ROT w pelnej plycie: glowa 97, twarz 48, SZYJA 24 (tylko korpus.arm - 1 z 274 helmow ROT ma arm_armor; w RBM polowa helmow daje szyi mediana 20),
barki 125 (bez naramiennika 24), piers/brzuch 120, rece 56, nogi 42.
damage_factor: klingi RBM ciecie mediana 0.90 (0.25-1.70), pchniecie 0.80; ROT x0.4 ciecie 1.80 (0.96-2.80), pchniecie 1.24 (0.56-4.0) -
bron ROT ma prog przebicia nizszy o ~29% (ciecie) i ~20% (pchniecie) niz bron zbalansowana przez RBM.

## 3. Przyklady policzone (obr_calc.py; trafienie w piers x0.9; pancerz bez premii staminy; bez perkow)
Zalozenia fizyczne broni (bron ROT jest kuta z czesci - prawdziwej masy/inercji nie odczytalem, patrz "nie ustalono"):
miecz 1h 1.2 kg / 0.95 m / inercja 0.09 / CoM 0.12 / swing 95, df 1.8 i 1.24; topor 2h 2.6 kg / 1.3 m / 0.45 / 0.55 / swing 78; buzdygan 1.6 kg; mlot 2h 3.2 kg;
wlocznia 1h 1.6 kg; pika 2.4 kg; kopia 2.6 kg. Format: HP (przebite + uraz).

Umiejetnosc 100 (DR 85.7, sm 0.4):
| Cios | M | goly | sukno 14 | skora 61 | kolczuga 88 | plyta 120 |
|---|---|---|---|---|---|---|
| 1. Miecz 1h ciecie (fizyka 6.4 + 11.4 z umiejetnosci, okno 7-27) | 82 | 72 | 35 (30+10) | 8 (0+9) | 6 (0+7) | 5 (0+6) |
| 2. Miecz 1h ciecie z konia +10 m/s | 86 | 77 | 39 | 8 | 6 | 5 |
| 3. Miecz 1h pchniecie (v 6.2 m/s) | 129 | 115 | 86 | 18 | 14 | 11 |
| 4. Topor 2h ciecie (fizyka 22.6) | 163 | 146 | 110 | 19 | 15 | 12 |
| 5. Buzdygan 1h (okno: MIN) | 86 | 76 | 54 | 23 | 18 | 15 |
| 6. Mlot 2h (zawsze MAX okna) | 182 | 163 | 140 | 51 | 41 | 33 |
| 7. Wlocznia 1h pchniecie (v 6.1) | 139 | 125 | 99 | 18 | 15 | 12 |
| 8. Wlocznia 1h w biegu +4 m/s | 164 | 146 | 122 | 22 | 18 | 14 |
| 9. Pika 2h pchniecie (v 4.9) | 131 | 117 | 92 | 18 | 14 | 11 |
| 10. Kopia couched 8 m/s | 154 | 138 | 113 | 21 | 17 | 13 |
| 11. Kopia couched 12 m/s (plateau 278.6) | 279 | 250 | 225 | 126 | 63 | 25 |
| 12. Kopia couched 16 m/s | 333 | 298 | 274 | 174 | 112 | 37 |
| 13. Kopia couched 20 m/s (sufit 430) | 430 | 387 | 361 | 261 | 199 | 125 |
| 14. Kon 8 m/s, masa 700 kg | 80 | 72 | 43 | 22 | 18 | 14 |

Ten sam miecz 1h, ciecie, wg umiejetnosci (goly / sukno / skora / kolczuga / plyta): 30 -> 38 / 7 / 3 / 2 / 2; 200 -> 112 / 74 / 12 / 9 / 8; 300 -> 143 / 105 / 15 / 12 / 9.
Mlot 2h: 30 -> 112 / 90 / 35 / 27 / 22; 300 -> 272 / 250 / 123 / 68 / 55. Topor 2h: 300 -> 243 / 207 / 65 / 26 / 20.
Szyja rycerza w plycie (pancerz 24, x1.5), umiejetnosc 100: ciecie mieczem 19 HP, pchniecie mieczem ok. 105 HP (zabija) - jedyne realne slabe miejsce.
Rozklad progu: miecz 1h (df 1.8) przebija ciecie dopiero gdy M > pancerz x 3.73 - przy umiejetnosci 300 (M 160) najwyzej pancerz 42.
Zaden cios wrecz z tabeli nie przebija kolczugi ROT 88 ani plyty 120 - robi to tylko kopia od ~12-16 m/s; skore 61 przebijaja dopiero wlocznia/pika od umiejetnosci 200 i topor/mlot 2h przy 200-300. Z premia staminy (pancerz x1.3) miecz 1h w plyte daje 3 HP zamiast 5.

## 4. Ocena: co ma sens, co jest uproszczone
MA SENS (zgodne z realiami):
- podzial na przebicie + uraz tepy; ciecie nie tnie plyty; obuch (70% urazu) 3x skuteczniejszy na zbroje niz miecz; pchniecie lepsze od ciecia na pancerz;
- kopia z rozpedu jako jedyna bron przebijajaca plyte; szyja, twarz, pacha jako slabe miejsca; lezacy latwiejszy do dobicia; trafienie drzewcem = obuch.
UPROSZCZONE / WATPLIWE:
1. Okno umiejetnosci zjada fizyke (1d). Miecz 1h: fizyka 3-8, dodatek z umiejetnosci 4-27. Ciezszy/dluzszy/lepiej wywazony miecz = ta sama sila.
   Rozped konia daje cieciu +5% (wiersz 2), choc fizyka rosnie o 70-130% - szarza z szabla nie istnieje; liczy sie tylko kopia.
2. Mlot 2h (1.125) - zawsze maksimum; przy umiejetnosci 30 bije jak miecznik 300. Jesli to literowka, bron obuchowa 2h jest ~2x za mocna u slabych wojsk.
3. Prog jest liniowy i "wszystko albo nic": plyta 120 i kolczuga 88 roznia sie o 1-3 HP na cios, skora ROT 61 chroni przed cieciem jak kolczuga.
   Mnoznik skory ROT x2.2 (mediana 61, do 176) stawia skore powyzej kolczugi RBM (47) - lekkozbrojni ROT sa niemal odporni na miecze.
4. Material pancerza nie gra w walce wrecz (tylko strzaly x0.5 na nie-plyte i szansa zuzycia). Kolczuga nie jest slabsza na pchniecie/obuch niz plyta o tej samej liczbie.
5. damage_factor broni tylko przesuwa prog (sqrt). Przeciw zbroi ponad progiem bron mistrzowska = zardzewiala. Nasze zuzycie ostrza (ConditionScaling) dziala wiec
   tylko na lekko opancerzonych.
6. Pchniecie: statystyka szybkosci pchniecia nieuzywana; energia zalezy od masy i umiejetnosci, sufit 180/250 J osiagany juz przy umiejetnosci ~200 (wlocznia 1h) -
   powyzej umiejetnosc nic nie daje, bieg tez nie (wiersz 8 przy 200+: 180 = 180).
7. Kopia: schodek. Przy 8-12 m/s i niskiej umiejetnosci 13-25 HP w plyte (slabiej niz mlot 2h), od ~16-20 m/s 37 -> 125. Plateau rosnie z umiejetnoscia (150 + 1.5 x DR J),
   nie z masa konia i jezdzca (masa "reki" 0.5-4.5 kg).
8. Premia staminy: wypoczeta ofiara ma pancerz x1.3, zmeczona x1.0 - dziala, ale to ukryte +30% pancerza na poczatku kazdej bitwy (przyklady w tabeli sa bez niej).
9. Martwy kod czesciowego przebicia - pchniecie tuz pod progiem nie daje nic ponad uraz, wbrew zamiarowi autora.
10. Mnozniki konczyn 0.5-0.7 + nogi z polowy pancerza: trafienia w rece/nogi sa slabe mimo niskiego pancerza (nogi rycerza 42).
STYK Z NASZYM KODEM (do decyzji, nic nie zmienialem):
- NaN: `sqrt(df + (ModifyDamage(100)-100)/100)` (rbm:3587-3590). Armoury Wrecked = -0.82, Mangled = -0.51 (Patches.cs:626-632, GearState.Value 0.05/0.25, ConditionPenaltyMax 90,
  wykladnik 2). df <= 0.82: 21/171 kling i grotow ROT w pchnieciu, 103/235 ciecie i 90/163 pchniecie w czesciach RBM (bron vanillowa); df <= 0.51: 25 kling RBM.
  Wynik: num2 = NaN -> floor(NaN) -> 0 obrazen, takze w nagiego. RBM sam ma modyfikatory damage tylko -15..+20, wiec u niego to nie wystepuje.
- Zuzycie pancerza liczy sie przed progiem (GetModified*Armor -> ModifyArmor): zbroja Mangled (-51%) z 120 robi ~59 - dalej ponad progiem miecza. Efekt zuzycia zbroi
  widac wiec glownie w urazie tepym (100/(100+2A)): plyta 120 -> 59 podnosi obrazenia miecza z 5 do ok. 8 HP.
- RBM sam degraduje pancerz w bitwie (lowerArmorQualityCheck rbm:5482-5622: szansa 5% + absorbed/(pancerz x prog)/5 x material, podmiana modyfikatora na SpawnEquipment) -
  osobny mechanizm obok naszego zuzycia; czy sie dubluje - nie sprawdzalem (poza zakresem tej mapy).

## 5. Czego nie udalo sie ustalic
- Prawdziwych parametrow fizycznych broni ROT (masa, inercja, srodek masy, swing speed): bron jest skladana z czesci przez Crafting w silniku, nie odtwarzalem tego.
  Liczby "fizyka" w tabeli sa z zalozonych wartosci; okna umiejetnosci, progi i wyniki przeciw pancerzowi ponad progiem zaleza od nich slabo, wyniki "goly/sukno" mocniej.
- Czy bron w stanie Wrecked/Mangled faktycznie trafia do rak walczacych (lup, AiWear) - od tego zalezy, czy NaN wystepuje w grze. Nie uruchamialem gry, logu z bitwy nie czytalem.
- Realnych predkosci wzglednych przy szarzy (MountSpeed po latce RBM, ciezkie uprzeze ROT) - progi kopii 12/16/20 m/s policzone, ale nie wiem, jak czesto sa osiagane.
- Regul po nazwie przedmiotu ("Closed" w nazwie helmu -> twarz jak kolczuga; "Brigandine over Hauberk" itd. pod pacha, rbm:3464, 3487) - dzialaja na nazwy vanilli; dla helmow ROT nie sprawdzalem trafien.
- Jak RBM laduje swoje XML parametrow silnika (RBM_spear_managed_core_parameters.xml nie jest wpisany w SubModule.xml wprost) - wartosci upadku sa i tak rowne vanilli.
- HP postaci (baza, perki, BK) i mnoznik trudnosci gracza - poza tym plikiem; pociski, tarcze, postura - w mapa-pociski.md.
- Czy 1.125 u mlota 2h i odwrocona premia staminy to bledy czy zamiar autora RBM - z kodu nie wynika; brak zrodel RBM do porownania.
