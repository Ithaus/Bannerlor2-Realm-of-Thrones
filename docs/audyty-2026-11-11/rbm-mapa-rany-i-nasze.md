# MAPA: rany, HP, wytrzymalosc (RBM) i nasze nakladki (Armoury) - audyt 11.11, tylko czytanie

Zrodla (wszystko czytane, nic nie zmieniane):
- RBM zdekompilowany wczesniej: `scratchpad/bad3/dec/rbmcombat.cs` (RBMCombat.dll), `rbmai.cs` (RBMAI.dll), `rbmconfig.cs`; DLL z 21.08. Numery linii ponizej = te pliki.
- Konfiguracja Jeffa: `Documents/Mount and Blade II Bannerlord/Configs/RBM/config.xml` (ArmorMultiplier 2, ArmorThresholdModifier 1, MaceBluntModifier 1, BluntTraumaBonus 0, ThrustMagnitudeModifier 0.05, Posture 1, Stamina 1, PlayerPostureMultiplier "0" = x1.0).
- Silnik: `scratchpad/rally_dec/mb_full.cs`, `Mission.cs`; dodatkowo zdekompilowane do `scratchpad/bad4/`: `sb_killed.cs` (SandboxAgentDecideKilledOrUnconsciousModel), `sb_applydmg.cs` (SandboxAgentApplyDamageModel), `healing.cs` (DefaultPartyHealingModel), `wcd.cs` (WeaponComponentData), `itemmodifier.cs`.
- RealisticBannerlord: `bad3/dec/rb.cs`. Nasz kod: `scratchpad/n367c/Armoury/src`. Logi: `Modules/Armoury/Armoury-2026-11-11_*.log` (814 linii HIT, linie Krwawienie 313d, 314a).

Pewnosc: [K] = sprawdzone w kodzie/dekompilacji, [L] = widac w logu Jeffa, [W] = wniosek z kodu, NIE sprawdzony w grze.

---

## 1. Lancuch po trafieniu (co po kolei dzieje sie z czlowiekiem)

### 1.1 HP i obrazenia - tylko to, co dotyczy ran
- RBM zastepuje calosc `MissionCombatMechanicsHelper.ComputeBlowDamage` (prefiks, `return false`) - `rbmcombat.cs:3511-3774`. Wynik to JEDNA liczba HP: `przebicie + tepy uraz po pancerzu`, razy mnoznik czesci ciala (`GetBodyPartDamageMultiplier` :4185): glowa/szyja x1.5, brzuch x1.0, piers x0.9, barki ciecie/kluc x0.6 obuch x0.7, rece i nogi obuch 0.7 / ciecie 0.6 / kluc 0.5.
- Jedna pula HP na czlowieka. NIE MA ran miejscowych: trafienie w noge nie kuleje, w reke nie oslabia ciosu - tylko odejmuje z tej samej puli. [K]
- HP szeregowych w bitwach Jeffa [L]: HealthLimit 100 (235 trafien), 105 (213), 113 (159), 123 (66). Czyli ok. 100-123.
- Stan ofiary zmienia pancerz: ofiara w akcji Fall albo StrikeKnockBack -> pancerz x0.75 (:3670). Stamina ofiary: pancerz x (0.3 + max(0.7, stamina/max)) = x1.3 przy pelnej staminie, x1.0 od 70% w dol (:3718-3723). Stamina atakujacego: sila ciosu x 0.85..1.0 (:3713-3717). [K]
- Samouszkodzenie: cios piescia w pancerz rani bijacego (`RegisterBlowPatch` :4086, blunt BaseMagnitude/2 przez pancerz reki), szarza konia rani konia (BaseMagnitude/6 przez pancerz piersi konia, :4079). [K]

### 1.2 Powalenie czy smierc
- Silnik: `Mission.GetAgentState` (Mission.cs:4974) -> prawdop. smierci = 1 - `PartyHealingModel.GetSurvivalChance` (sb_killed.cs). Bohater z `CanDie == false` -> 0.
- VANILLA (healing.cs:61-115): obuch bez flagi CanKillEvenIfBlunt = ZAWSZE przezywa. RBM (`CampaignChanges.OverrideDefaultPartyHealingModel`, rbmcombat.cs:2081-2140, prefiks na `DefaultPartyHealingModel.GetSurvivalChance`) przepisuje metode BEZ tego warunku: **w RBM obuch (maczuga, mlot, glowica miecza, piesc, kamien z procy, stratowanie koniem) zabija z ta sama szansa co ostrze**. [K] Jedyna latka na tej metodzie u Jeffa to RBM (log 13:27 "DefaultPartyHealingModel.GetSurvivalChance: com.rbmcombat") [L]; czynny model BKROT wola baze i dodaje +0.03 za blogoslawienstwa (bkrot.cs:12190-12223).
- Liczby (szeregowy): smierc = 1 / (1 + premia chirurga + poziom x 0.02 [+ perk PhysicianOfPeople dla tier < 3]). Bez chirurga: poziom 6 -> 89% ginie, poziom 26 -> 66%, poziom 36 -> 58%. Tier, pancerz, czesc ciala, glebokosc rany, rodzaj broni - NIE licza sie wcale. [K]
- Bohater: mianownik x51 i + suma pancerza x0.01 - wiek x0.01 -> ok. 0.7-2.5% na powalenie. [K]
- **Uproszczenie nr 1**: los powalonego to jeden rzut zalezny tylko od poziomu i chirurga. Strzala w szyje za 105 HP i piesc za 3 HP ponad stan daja te sama szanse smierci.

### 1.3 Ogluszenie / przerwanie ciosu / odrzut / powalenie na ziemie
- Przerwanie ciosu ("shrug off"): silnik `DecideAgentShrugOffBlow` (mb_full.cs:76542) - cios <= progu NIE przerywa. Prog w RBM: 5 HP dla ciecia, klucia i obucha (`RBM_spear_managed_core_parameters.xml` DamageInterruptAttackthreshold*), x perki Spartan / DauntlessSteed (sb_applydmg.cs:690). [K]
- Odrzut / powalenie / zrzucenie z konia: silnik `DecideCombatEffect(obrazenia, HealthLimit, odpornosc, penetracja)` - RBM tego nie rusza poza: (a) kopia w szarzy trafiajaca wroga = ZAWSZE KnockDown, zablokowana = KnockBack (`CreateMeleeBlowPatch` :3992-4021); (b) zrzucenie z konia tylko kopia w szarzy/zaparta: glowa/szyja gdy BaseMagnitude > 2.4 + Jazda x0.01, piers/barki > 3.0 + Jazda x0.01 (:3020-3031); (c) topor, topor poltorareczny, bron drzewcowa 2R i mlot 2R zablokowane bronia albo zla strona tarczy -> dodatkowy "cios" NonTipThrust 0 obrazen (zachwianie, ogluszenie obroncy x0.5, :4022-4060), a `HandleBlowPatch` (:4143) zamienia kazde 0 obrazen z flaga odrzutu/powalenia/kopniaka na 1 HP. [K]
- Stanie deba konia: tylko przy predkosci konia <= 5 m/s; pchniecie bronia drzewcowa z przodu >= 13 obrazen, pocisk z przodu >= 26 (:2980-3010). [K]
- Kon zabity w biegu (>= 5 m/s): piesi tuz przed nim dostaja animacje zachwiania (rbmai.cs:7520). [K]
- Stratowani przez konnice: szansa ucieczki (CommonAIComponent.Retreat): powalony od tylu 100%, od przodu 50%; odrzucony od tylu 50%, od przodu 1/max(2, 1+tier) (rbmai.cs:2919-2960). [K]

### 1.4 Krwawienie
- RBM: NIE MA krwawienia. [K] RBM mial natomiast LECZENIE w bitwie: +0.9 HP co 10 s kazdemu ze stamina > 85% (rbmai.cs:7630-7633) - nasza latka 314a `RbmNoRegen.cs` to blokuje; log 13:35: "podniesien HP RBM odrzucone 1364 (agentow 248)" [L].
- Nasze: `Bleed313.cs` (313d) - szansa = BleedChanceDeepWound 0.60 x typ (ciecie 1.0 / kluc 0.8 / obuch 0.15) x czesc ciala x glebokosc (od 10% do 50% pelnego HP) x pancerz (BleedArmourFactor 0.3) x Atletyka; utrata = 0.5 x obrazenia, wygasa wykladniczo (30 s). Settings.cs:612-638.
- [L] Rzeczywistosc z logow 11.11: krwawien 98, z tego **ciete 1, klute i strzaly 97, obuch 0**; rece 37, nogi 38, tulow 19, glowa 4; zgony z krwawienia 22 (5 zabitych / 17 nieprzytomnych). Druga bitwa: 36 krwawien = 6 / 28 / 2. Czyli regula "ciecie krwawi najbardziej" w praktyce nie dziala - krwawia prawie wylacznie strzaly w konczyny.
- Przyczyna [W]: (a) w RBM ciecie przez pancerz prawie nigdy nie przebija (prog ciecia = pancerz x5 dla mieczy, x2-3.5 dla klucia, :6235-6273) - zostaje sam "tepy uraz", zwykle ponizej progu 10% HP; (b) Bleed313 widzi tylko `blow.InflictedDamage` i `AbsorbedByArmor` - nie wie, ile z obrazen to PRZEBICIE (rana otwarta), a ile tepy uraz spod pancerza. RBM liczy obie liczby osobno (`penetratedDamage`, `bluntTraumaAfterArmor` w `Utilities.RBMComputeDamage` :6111, publiczna) ale ich nie wystawia w Blow.

### 1.5 Postura i stamina RBM (wszystko w rbmai.cs)
- Stamina (`Stance.InitializeStamina` :5586): pula = 1000 x (1 + 3 x Atletyka/500) -> Atl 0: 1000, 100: 1600, 250: 2500. Odnowa 0.03 x (1 + 2 x Atl/500) na takt, 30 taktow co 0.5 s = ok. 1.8 pkt/s przy Atl 0, x "gumka" (1 + brak)^2.5 (do x5.7 przy pustym pasku), x3 po 10 s poza walka ponizej 70%. Helm z body_armor >= 30 -> odnowa x0.5 (czyta gniazdo HELMU, nie tulow - znane, CHANGELOG:7090).
- Koszty staminy: celny cios 35 (1R) / 49 (2R); cios zablokowany 35/40 x (1 - skill/1000); blok bronia 40, mala tarcza 43, duza 46, x (1 - skill/1000); przyjety cios = obrazenia x2 + 20; wszystko x (1 + max(0, waga pancerza - Atl/20)/50) (:6396-6400, :6698-6799). Strzal z luku max(20, 70 - (skill - trudnosc)/2), rzut max(50, 100 - skill/2). Trafienie strzala: -25 postury / -50 staminy (glowa 50/100), oszczep -100/-300 (glowa 200/600), w tarcze 20/40 i 70/210 (:7713-7794).
- Skutki niskiej staminy (`AgentAi` :2533-2556): handling x0.5..1, obrona tarcza x0.5..1, predkosc ruchu/zamachu/pchniecia x0.85..1, przeladowanie x0.8..1, celnosc gorsza do 15-30%, decyzje AI x0.85..1; plus sila ciosu x0.85 i utrata premii pancerza x1.3 (pkt 1.1). Pusta stamina NIE unieruchamia i nie zadaje obrazen.
- Postura (`InitializePosture` :5610): max = 30 + 20 x (1 + Atl lub Jazda/500) + 80 x (1 + skill broni/500) + max(0, waga pancerza - 5). Skill 0: 130 + waga; skill 250: 180 + waga. Odnowa ok. 4.8 pkt/s przy zerowych skillach, x3 po 10 s spokoju.
- Obrazenia postury: tabela stalych na klase broni (`PostureDamage` :5727-6003) x stosunek (1 + Atl + skill atakujacego)/(1 + Atl + skill obroncy) (kazde /500) + predkosc wzgledna x4, x modyfikator srodka ciezkosci, x 1..1.25 od zmeczenia. Parametry "actionModifier" (0.85 blok, 0.5 parada itd.) sa w kodzie NADPISYWANE jedynka (:6808, :6860) - martwe. Celny cios: postura ofiary -= obrazenia HP.
- Zlamanie postury (<= 0): blok/parada bronia -> "crush through": obrazenia HP = pelne obrazenia tej broni przez SREDNI pancerz 9 czesci ciala x min(1, nadwyzka/20) (`calculateHealthDamage` :7124), ogluszenie obroncy x5; nadwyzka >= 50% max postury -> upuszczenie broni (gdy ma druga biala) / tarczy; animacja zachwiania; postura wraca na 75%. Atakujacy tez moze sie zlamac (zachwianie albo "zmeczony" po celnym ciosie, powrot 33%).
- **Postura i stamina sa odporne na rany**: HP nie wplywa na pule ani odnowe w RBM. Ranny z 10% HP ma te sama posture co zdrowy (to lataja dopiero nasze WoundedPenalties / WoundedWary).

### 1.6 Wplyw umiejetnosci i perkow
- Skill broni: sila ciosu (`GetSkillBasedDamage` :5831 - dolny i gorny prog rosna ze skillem, mnoznik x4.6 dla ciecia), predkosc zamachu, postura (max, odnowa, stosunek sil), koszt staminy blokow. Atletyka: pula i odnowa staminy, postura, kara za wage pancerza. Jazda: postura konnego, odpornosc na zrzucenie.
- Perki gry dzialaja dalej tam, gdzie RBM nie zastapil metody: `ApplyDamageAmplifications/Reductions` (sb_applydmg.cs:28-453), progi odrzutu/powalenia/zrzucenia (:617-685), prog przerwania (:690). RBM nie patchuje `AgentApplyDamageModel.CalculateDamage`.
- Medycyna: tylko szansa przezycia powalonego (pkt 1.2) i leczenie na mapie.

### 1.7 Konie
- Pancerz konia: wszedzie +10 bazowo; glowa = head uprzezy + 10, szyja = arm + 10, nogi = (2 x leg + body)/3, piers = (leg + body)/2, brzuch = leg (`ArmorRework` :461-486).
- HP koni: RBM `RBMCombat_horses.xml` extra_health mediana 140 (0-290) wobec 30 (0-120) w grze; 19 wierzchowcow ROT ma wlasne wartosci (mediana 200, do 1000) - poza skala RBM.
- Szarza: obrazenia = predkosc wzgledna x zgodnosc kierunku x (masa konia + jezdzca + pancerzy + broni + 100)/70 (:3041-3052); nasz `Charge.cs` mnozy x0.6 i daje krzywa predkosci. Atrybut charge_damage z XML RBM ignoruje (wartosci ROT 30-400 nie maja znaczenia).
- Konie nie maja postury ani staminy RBM (slownik tylko dla ludzi). Zmeczenie koni to w calosci nasz `HorseWind.cs`.

---

## 2. Co po bitwie (mapa kampanii)
- Leczenie: czynny model to BKROT -> (nasz lancuch `BkrotChains.cs`) -> Default. `RealisticBannerlord.RealisticHealingModel` (chirurg ponizej 20 Medycyny = -80% leczenia, zima -20%, osada +50%) jest MARTWY - "samotny" model pominiety w lancuchu (BkrotChains.cs:55-57). Nasz `SlowHealing.cs`: gracz 50% tempa, AI 100% (Armoury.json).
- RealisticBannerlord zywe zachowania: `InfectionBehavior` (rb.cs:1334) - partia gracza poza osada, chirurg < 20 Medycyny: codziennie ginie max(1, 5% rannych), zima x2, zamiec x3, powodz x2.5; ziola/bandaze x0.5. `CriticalWoundBehavior` (15% przy kazdej ranie bohatera, -4 HP/dobe poza osada) - wylaczony nasza latka `HeroWounds.RbCriticalPrefix` (klucz HeroWoundsReplaceRbCritical). `EquipmentWearBehavior` / `EquipmentRepairBehavior` to ATRAPY (sam komunikat; "naprawa" bierze zloto i nic nie naprawia) - u Jeffa EquipmentEnabled = false.
- Nasze `HeroWounds.cs` (401/403): wyzwalacz `CampaignEvents.HeroWounded` w bitwie; rzut Grave 8% (+0.4 pp za rok ponad 50, sufit 20%), Serious 20%, reszta Light; loze 21 / 63 dni (+-1/3), w twierdzy liczone podwojnie; przesilenie Grave po 3-10 dniach: smierc 6% + 1 pp za rok ponad 40 (sufit 40%), x0.6 w twierdzy, x0.75/x0.5 chirurg 100/200, x1.5 jeniec; okaleczenie 20%.
- **Uproszczenie nr 2**: stopien rany bohatera to czysty rzut 8/20/72 - NIE zalezy od tego, co go powalilo (strzala w twarz, kopia w szarzy, piesc), w co trafilo ani o ile obrazenia przekroczyly reszte HP. A wszystkie te dane mamy w `FieldCraft.OnAgentHit` (ostatni Blow: typ, czesc ciala, InflictedDamage, AbsorbedByArmor).

---

## 3. Nasze nakladki a lancuch RBM - spojnosc

### 3.1 Stan pancerza -> ochrona: SPOJNE [K]
- RBM czyta pancerz przez `EquipmentElement.GetModified*Armor` -> `ItemModifier.ModifyArmor`. RBM ma na tej metodzie prefiks (`ModifyArmorPatch` rbmcombat.cs:596): pole Armor to PROCENT (gra: punkty). Nasz postfiks `ConditionScaling.ArmorPatch` (Patches.cs:634) nadpisuje wynik dla zlych stanow: wartosc bazowa x StatFactor = 1 - 0.9 x zuzycie^2 -> Uszkodzony 60% = -14%, Zmasakrowany 25% = -51%, Wrak 5% = -81% (GearState.cs:35, Settings.cs:172-173). Druzyna gracza: z dokladnego HP (`HeroHp.Exact` :154). Log 13:27: "krzywa 90/2: 50 przy 41/56/78% -> 34/41/47, stan Damaged 42 - OK" [L].
- To dziala dla kazdej czesci ciala, konia i tarczy, bo RBM wszedzie idzie przez te sama metode.

### 3.2 Stan BRONI -> obrazenia: NIESPOJNE, mozliwy blad [K kod, W skutek]
- Regula Jeffa (Patches.cs:596): "ostrze w stanie 60% tnie za 60%". W RBM sila ciosu wrecz NIE czyta SwingDamage/ThrustDamage ani ModifyDamage - liczy ja z predkosci, wagi i bezwladnosci (`CalculateBaseMeleeBlowMagnitudePatch` :676). Jedyne miejsce, gdzie stan broni wchodzi do obrazen: `Utilities.getSwingDamageFactor / getThrustDamageFactor` (:6511-6529) = `wcd.SwingDamageFactor + (ModifyDamage(100) - 100)/100`, potem `sqrt(...)` i dzieli PROG PANCERZA (:3747, :6207).
- Skutek 1: zly stan broni NIE zmniejsza obrazen w cel bez pancerza i nie zmniejsza tepego urazu - tylko utrudnia przebicie pancerza. Uszkodzony miecz (wspolczynnik 0.95 -> 0.80) = prog pancerza +9%. To nie jest "tnie za 60%".
- Skutek 2 (mozliwy blad): nasz postfiks daje ModifyDamage(100) = 85 / 49 / 18 dla Uszkodzony / Zmasakrowany / Wrak, czyli -0.15 / -0.51 / -0.82 do wspolczynnika. Wspolczynniki ostrzy w skali RBM (RBMCombat_sword_blades/axe/mace_pieces.xml): 0.25-1.7, mediana 0.95; bron nie-kuta: 0.4 ciecie / 0.32 kluc / 0.8 obuch (wcd.cs:471-499). Gdy wynik < 0 -> `Math.Sqrt` = NaN -> prog NaN -> obrazenia NaN -> `(int)floor(NaN)` -> ClampInt = **0 obrazen przy kazdym ciosie, takze w golego**. Dotyczy: Zmasakrowany - 8% glowni mieczy i 29% glowic maczug RBM (wspolczynnik <= 0.51); Wrak - 42% glowni mieczy (zamach), 56% (pchniecie), 39% toporow, 56% maczug (<= 0.82). Modyfikatory RBM maja najwyzej -10%, wiec sam RBM tam nie wpada - wpada dopiero z nasza krzywa. Druzyna gracza (HeroHp, dokladne d): miecz 0.7 przestaje ranic ponizej ok. 12% wytrzymalosci, 0.4 ponizej ok. 18%.
- Bron ROT (144 kute wyroby w ROTassets.xml, wszystkie na czesciach z `ROT_crafting_pieces.xml`) ma wspolczynniki w skali GRY: zamach 2.4-10.3 (mediana 4.5), pchniecie 1.4-10.0 (mediana 3.1) - RBM ich nie przeskalowal (0 wspolnych id). Tam NaN nie grozi, ale sqrt(4.5) = 2.1 dzieli prog pancerza na pol wobec broni w skali RBM: bron wlasna ROT przebija pancerz ok. 2x latwiej (Valyria 10.3 -> 3.2x). W wyposazeniu wojsk ROT: 310 gniazd broni ROT, 2862 gniazda rzeczy z gry (skala RBM).
- Nie sprawdzone w grze: nie ma u nas dziennika trafien wrecz (HitScribe pisze tylko pociski).

### 3.3 Dwa systemy zuzycia pancerza naraz [K]
- RBM sam psuje pancerz W MISJI: `Utilities.lowerArmorQualityCheck` (:5644) przy kazdym trafieniu > 1 HP bronia: szansa = 5% + AbsorbedByArmor/(pancerz x prog)/5 x mnoznik materialu (obuch w plyte x12, ciecie mieczem w tkanine/skore x5, w plyte x2, w kolczuge x1; inne ciecie x2/x4) -> `lowerArmorQuality` (:5786) przestawia modyfikator sztuki w SpawnEquipment agenta na nastepny gorszy z grupy modyfikatorow. Dotyczy kazdego agenta (takze konia - uprzaz), trwa do konca misji, nie wraca do kampanii (SpawnEquipment to klon).
- RBM projektowal to na drabinke po 5-10% (plate: -10/-20/-30/-40/-50; chain: -5/-10/-15/-20/-25/-30). Nasz `GearState` (GearFourStates) zostawil w 16 grupach tylko dwa zle szczeble: Uszkodzony -14% i Zmasakrowany -51%. Skutek [W]: jedno "trafienie RBM" = od razu -14%, drugie = -51% ochrony tej czesci do konca bitwy. Mlot w plyte (mnoznik x12) praktycznie przy kazdym ciosie.
- Nasz `WearGroups.cs` nadal grupy 361 pancerzom ROT bez grupy (log 13:27) - przed nim RBM nie mial im czego obnizac; teraz obniza.
- Rownolegle nasze trwale zuzycie: gracz/towarzysze - ksiega z surowego ciosu (`FieldCraft.cs:947-985`, raw = InflictedDamage + AbsorbedByArmor, pula = punkty pancerza x 20 x tier, `ArmouryBehavior.ArmorPool` :4142); wojsko - `TroopWearLedger` 5% na trafienie; AI - `AiWear` 4% sztuk na bitwe. Nasze nie patrzy na pare bron-material (RBM patrzy), RBM nie zostawia sladu po bitwie (nasze zostawia).
- HeroHp zna obnizke RBM czesciowo (HeroHp.cs:171 "obnizka RBM", :537), ale jako zasada nigdzie nie jest opisane, ze w bitwie dzialaja dwa mechanizmy i ze drugi ma po naszej zmianie grup skok 14 -> 51%.

### 3.4 Mapowanie czesci ciala: nasze != RBM [K]
- RBM (`ArmorRework` :461-511): glowa = head helmu; **szyja = arm helmu + arm zbroi** (helmu head NIE liczy); barki = body + arm peleryny + arm zbroi; rece = TYLKO rekawice; nogi = 0.5 x leg nogawic + 0.5 x leg zbroi; piers/brzuch = body zbroi.
- Nasze zuzycie (`FieldCraft.ArmorSlotFor` :819): szyja -> helm; barki -> peleryna albo kirys. Trafienie w szyje zuzywa helm, ktory w RBM szyi prawie nie chroni.

### 3.5 Skala pancerzy ROT wobec RBM [K dane, L skutek]
- RBM przeskalowal pancerze gry: plyta tulow mediana 77 (do 135), helm plytowy mediana 85 (do 150), a helmy maja arm_armor (mediana 15, do 50) = oslona szyi; zbroje plytowe arm mediana 35.
- Pancerze ROT (1254 w ROTassets.xml) zostaly w skali gry: plyta tulow mediana 50 (maks 57), helm plytowy mediana 39, helmy arm = 0, zbroje plytowe arm mediana 10. Wojska ROT nosza glownie ROT: tulow 1054 z 1151 gniazd, nogi 1017/1158, helm 710/941.
- Skutek liczony wzorem RBM: szyja rycerza ROT = ok. 10 pancerza przy mnozniku x1.5; nogi = 0.5 x 25 + 0.5 x 11 = 18.
- [L] 814 trafien pociskow z bitew 9-11.11 (strzaly, mediana obrazen / wchloniete): **szyja 105 / 46 (78% trafien >= 50 HP)**, nogi 47 / 18 (juz PO mnozniku x0.5), barki 44, prawa reka 43, lewa 33, brzuch 31, piers 28, **glowa 11 / 276 (tylko 8% >= 50)**. Przy 100-123 HP: strzala w szyje = smierc/powalenie, dwie w nogi = powalenie, a strzala w helm prawie nic. Anatomicznie odwrocone: najbezpieczniej dostac w glowe, najgrozniej w noge i bark.

### 3.6 Tiery (Recipes.Grade) [K wzor, W skutek]
- `Recipes.Grade(item)` = `(int)item.Tier + 1` (Recipes.cs:104). Tier pancerza liczy RBM (`OverrideCalculateArmorTier` rbmcombat.cs:3325): tulow = body x0.05 + leg x0.035 + arm x0.025 - 1; helm = head x0.06 - 1; rekawice/nogawice = wartosc x0.1 - 1. Wzor skrojony na skale RBM.
- Dla pancerzy ROT w skali gry: plyta tulow 50/11/10 -> 2.1 (Grade ok. 3), najlepsza 57/20/15 -> 2.9 (Grade ok. 4); helm plytowy 39 -> 1.3 (Grade 2), najlepszy 80 -> 3.8. Czyli najlepsze plyty Westeros wychodza tier 3-4: nizszy material i prog w kuzni, mniejsza pula wytrzymalosci (pula = punkty x 20 x tier - podwojna kara: malo punktow i niski tier). Nie sprawdzilem w logu kuzni konkretnej sztuki.

### 3.7 Stamina: nasze nakladki na RBM [K]
- `BattleWind.cs`: przy StaminaFromAthleticsOnly = TAK (domyslne, Settings.cs:544) pula i oddech wszystkich z samej Atletyki jak w RBM; mnoznik puli x2 dla kazdego (292); NoBreathWhileRunning; `AiBreath.cs` (AI nie biegnie bez zapasu tchu); `WeaponEase` (log: skill broni lagodzi koszty zamiast (1 - s/1000) RBM). Spojne z RBM - dzialaja na polach `RBMAI.Stance`, nie obok.
- Uwaga: premia pancerza x1.3 przy staminie > 70% (pkt 1.1) i x2 pula = ludzie dluzej trzymaja premie. Nasze `WoundedPenaltiesEnabled` (ruch i zamach wolniejsze ponizej 50% HP, do -30%) i `WoundedWary` uzupelniaja to, czego RBM nie ma (HP nie wplywa na nic w RBM).

### 3.8 Ranni w bitwie - nasze, czego RBM nie ma [K]
- `BrokenMen` / `WoundedWithdraw` (odwrot z bronia ponizej 20% HP, rzut 35%), `PainBreak` (panika ponizej 10%, rzut 50%, x1.5 gdy krwawi, tabela tieru), `WoundedLimp` (<= 10 HP: chod x0.7), ArtemsLivelyAnimations (ponizej progu HP: noga x0.35, brzuch/glowa x0.7 predkosci - jedyny w zestawie skutek MIEJSCA rany). Log 13:35: odwrot 22 z 52 rzutow, zlamani bolem 21 z 62, kustykajacych 63 [L].

---

## 4. Ocena: sensowne czy zbyt uproszczone

Sensowne (trzyma sie kupy):
- Rozdzial przebicie / tepy uraz i progi wedlug klasy broni; pancerz jako prog, nie procent.
- Postura + stamina jako drugi i trzeci pasek: blok kosztuje, zmeczony bije slabiej i gorzej sie zaslania.
- Nasz stan pancerza -> ochrona (jedna metoda, jedna krzywa, takze dokladne HP druzyny).

Zbyt uproszczone albo niespojne (wedlug wagi):
1. Stan broni wrecz nie dziala tak, jak mowi regula i opis: zero wplywu na cel bez pancerza, a przy Zmasakrowany/Wrak czesc broni w skali RBM zadaje 0 (NaN). (3.2)
2. Skala ROT nie pasuje do wzorow RBM: pancerze ROT o polowe nizsze niz zaklada RBM, szyja i nogi prawie gole, glowa najbezpieczniejsza; bron ROT przebija 2-3x latwiej. Potwierdzone w 814 trafieniach. (3.5, 3.2)
3. Smierc czy rana szeregowego = jeden rzut od poziomu; obuch zabija jak ostrze; glebokosc i miejsce rany bez znaczenia. (1.2)
4. Brak ran miejscowych: jedna pula HP, konczyny tylko z mnoznikiem 0.5-0.7. Dwie strzaly w nogi powalaja jak cios w piers. (1.1, 3.5)
5. Krwawienie nie odroznia rany otwartej od tepego urazu spod pancerza; w praktyce krwawia tylko strzaly w konczyny, ciecia 1 na 98. (1.4)
6. Rany bohaterow (401) losowane bez zwiazku z ciosem, ktory powalil. (2)
7. Dwa systemy zuzycia pancerza w bitwie; drabinka RBM zwinieta do skokow -14% / -51%. (3.3)
8. Tier pancerzy ROT liczony wzorem RBM wychodzi 2-4 dla najlepszych plyt. (3.6)

---

## 5. Czego nie udalo sie ustalic
- Czy blad NaN (3.2) wystepuje w grze: wynika z kodu, brak dziennika trafien wrecz i nie uruchamialem gry. Najprostsza proba: bohater z bronia z gry (nie ROT) w stanie Wrak bije nieopancerzonego - 0 obrazen = potwierdzone.
- Dokladna premia chirurga w szansie przezycia (`DefaultSkillEffects.SurgeonSurvivalBonus` - wartosc na punkt Medycyny nie odczytana) - liczby z 1.2 sa dla partii bez chirurga.
- Skad HealthLimit 105 / 113 / 123 u szeregowych (model MaxHitpoints: gra / BK / ROT / perki kapitana) - widac w logu, zrodla nie sledzilem.
- Dlaczego glowa wchlania medianie 276 przy helmach ROT ok. 39: nie rozbilem, ile ofiar mialo helm z gry (skala RBM, 231 z 941 gniazd) i jak czesto wchodzi regula "Face hit" (pancerz twarzy = body_armor helmu, u ROT 0). Rozrzut (min 4, maks 113) sugeruje obie grupy.
- Kolejnosc modyfikatorow w grupach po `GearState` (czy pierwszy ujemny to zawsze Uszkodzony) - od niej zalezy, czy pierwsza obnizka RBM daje -14% czy od razu -51%.
- Realny tier konkretnych pancerzy ROT w kuzni (3.6) - policzone ze wzoru RBM, nie z logu; nie sprawdzilem, czy inny model wartosci (BK/BetterEconomy) nie nadpisuje `CalculateTier`.
- Czesc o sile ciosu, pociskach i tarczach (strona atakujacego) - poza moja mapa, tylko tyle, ile dotyka ran.
- `bad4/rbm.cs` (plik innego agenta) nie czytalem - pracowalem na `bad3/dec/rbm*.cs` (ta sama dekompilacja DLL z 21.08).
