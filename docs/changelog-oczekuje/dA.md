## Grupa A (galaz noc/d-walka z origin/noc/scal-perki e46a8c9a): decyzje Jeffa D1, D9, D16 - Armoury (klucze PaviseBackShieldMelee, NavalAmmoPerksRecover, NavalAmmoPerkPercent; CombatXpPerksMultiply rozszerzony)

Zrodlo decyzji: Jeff, czat 14.11, odpowiedz na D1-D16 po serii perkow 505-515: "D1 tak / ... / D9 - tak / ... / D16 - odzysk". Opis pytan: docs/audyty-2026-11-14/PERKI-DECYZJE.md (sekcja D1-D16). Kazda decyzja osobna sekcja nizej; jeden DLL (Armoury) - przy tescie sprawdzac trzy linie logu osobno.

### D1: mnozenie premii XP takze na arenie i w turnieju

**Problem:** decyzja Jeffa "D1 tak" (PERKI-DECYZJE: "D1 mnozenie XP takze arena i turniej: TAK"). Po 505 waga obrazen w bitwie MNOZY perki XP, a na arenie i w turnieju stawka (ArenaXpPercent 20%, TournamentXpPercent 50%) dalej sie DODAWALA do sumy czynnikow - perk +50% na arenie dawal baza x (1 + 0.5 - 0.8) = 0.7 bazy zamiast 0.2 x 1.5 = 0.3 bazy.

**Zmiana:**
- Armoury/src/XpMultiply505.cs:73-95 - nowa FactorPractice(ref wynik, pct): przy kluczu CombatXpPerksMultiply TAK czynnik = (1 + suma czynnikow) x (pct - 1), czyli wynik = baza x (1 + suma) x pct; klucz NIE / wyjatek = pct - 1 (jak dotad). Liczniki doby osobne (_dPrac*, :28-29, zerowane :36).
- Armoury/src/FairXp.cs:46 (arena, PracticeFight) i :51 (turniej) - AddFactor(XpMultiply505.FactorPractice(...)) zamiast AddFactor(pct - 1).
- Linia startu 505 (:104) mowi "od D1 takze areny i turnieju"; linia doby (:116) dopisuje "arena i turniej (D1): wywolan N, z premia K, XP dodawanie / mnozenie A / B".
- Opis klucza Combat Xp Perks Multiply (Settings.cs:2499, McmSettings.cs:6596) - dopisane arena i turniej (po angielsku). Bez nowego klucza: jedna zasada na jedno zjawisko (stawka/waga mnozy perki), ten sam wylacznik.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Kto dopisuje czynniki do GetXpFromHit przed FairXp (Priority.Last) - wedlug wpisu 505 (dekompilacja): RBM InspiringLeader, PerkXp503 (tylko Battle / SimulationBattle - na arenie nic), NavalDLC (bitwa morska), BK Gladiator +2 (TYLKO Tournament). Skutek D1: Gladiator BK w turnieju liczy sie od stawki - dotad baza x (1 + 2 + 0.5 - 1) = 2.5 bazy, teraz baza x 3 x 0.5 = 1.5 bazy (bez perka 0.5 bazy w obu). To zamierzone (D1), ale XP z turnieju z Gladiatorem spada o 40% - do potwierdzenia przez Jeffa w grze.
- Brak premii (suma 0): wynik identyczny jak dotad (czynnik pct - 1). Ujemna suma (np. polityka -15%) - mnozy sie tez (baza x 0.85 x pct). 1 + suma <= 0 - stary wzor.
- Galaz bitwy (Factor) bez zmian; wywolania tylko z FairXp (grep: FactorPractice w FairXp.cs:46 i :51). Licznik _depth FairXp dalej liczy raz na zewnetrzny model.
- Stan: tylko liczniki doby, bez zapisu.

**Linie logu:** start "Perki XP x waga (505): start sesji | klucz CombatXpPerksMultiply TAK ... | dotyczy bitwy i symulacji, od D1 takze areny i turnieju (stawka % mnozy perki) | potkniecia 0."; doba "Perki XP x waga (505): dzien N | ... | arena i turniej (D1): wywolan N, z premia K, XP dodawanie / mnozenie A / B | potkniecia doby 0". Po walce na arenie z perkiem XP: B < A, gdy premia > 0 i stawka < 100%.

**Status:** DO SPRAWDZENIA (zbudowane 0 bledow, nie wgrane).

### D9: Pavise chroni tarcze na plecach takze przed ciosami wrecz

**Problem:** decyzja Jeffa "D9 - tak" (PERKI-DECYZJE: "D9 Pavise: tez ciosy wrecz w tarcze na plecach"). Po 506 Pavise (+30% wytrzymalosci, -25% zuzycia) dzialal tylko na pociski zatrzymane tarcza na plecach (RBM przepisuje je na blok - droga MissionCombatMechanicsHelper.ComputeBlowDamageOnShield i nasz ShieldPostfix). Cios wrecz w tarcze na plecach RBM liczy obok: prefiks RBM na ComputeBlowDamage (rbmcombat DamageRework.cs:538-553) przy CollidedWithShieldOnBack wola RBMComputeBlowDamageOnShield WPROST, absorbedByArmor = 0, a wynik idzie jako obrazenia ciosu (to, co przeszlo przez tarcze) - ShieldPostfix tam nie biegnie.

**Zmiana:**
- Armoury/src/Perk506.cs:145-166 - latka (raz na proces, w kampanii, jak reszta 506): postfiks Priority.Low na RBMCombat.DamageRework.RBMComputeBlowDamageOnShield (AccessTools.DeclaredMethod, metoda statyczna; Owned() potwierdza wpiecie, licznik latek 506 x/y obejmuje ja).
- Perk506.cs:406-432 PaviseMeleePostfix: tylko CollidedWithShieldOnBack, NIE pocisk, NIE AttackBlockedWithShield (blok idzie przez ComputeBlowDamageOnShield -> ShieldPostfix, wiec bez podwojenia); klucze PerkShieldsAfterRbm i PaviseBackShieldMelee; ofiara-czlowiek z perkiem Pavise (perki kampanii odswiezane na starcie misji - poza kampania nieczynne): obrazenia / (1 + PaviseBackShieldDurability) (ta sama liczba co dla pociskow, 30%), zaokraglenie i min 1 jak w ShieldPostfix; ustawia out inflictedDamage i attackCollisionData.InflictedDamage (RBM ustawia oba). Sam Pavise - bez ArrowCatcher (D9 dotyczy Pavise).
- Liczniki misji (_pvMelee, :92, zerowane w Reset) i linia konca misji 506 (:516): "Pavise wrecz w tarcze na plecach (D9): ciosow N, obrazenia A -> B"; linia startu 506: "ciosy wrecz w tarcze na plecach (D9) TAK (latka wpieta)".
- Klucz PaviseBackShieldMelee (TAK): Settings.cs:236, McmSettings.cs:733-736 (+ ApplyTo :13705), grupa "Wear and tear" (recznie, bez gen_mcm.py). Opis Pavise Back Shield Durability (Settings.cs:234, McmSettings.cs:725) - zamiast "melee blows on the back shield are not changed" mowi o ciosach wrecz.
- Komentarz klasy (:43-53) - nowy opis drogi ciosu wrecz.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Wywolania RBMComputeBlowDamageOnShield (dek rbmcombat): DamageRework.cs:180 (prefiks RBM na ComputeBlowDamageOnShield - blok, AttackBlockedWithShield = true -> nasz postfiks wychodzi) i :553 (prefiks ComputeBlowDamage - tarcza na plecach). Pocisk przebijajacy / Impale w plecy (IsMissile) - wychodzi, jak w 506 bez ulgi; RBM RangedRework.cs:1582-1602 sam odejmuje tarczy HP po tej liczbie tylko dla pociskow.
- Ciecie zuzycia Pavise (-25%) tu NIE dziala: FieldCraft (FieldCraft.cs:1180-1222) liczy cios wrecz w plecy jako pancerz (nie AttackBlockedWithShield) - zuzycie pancerza spada przez mniejsze InflictedDamage, ale to ta sama regula co dla kazdego ciosu (raw = InflictedDamage + AbsorbedByArmor).
- Cudze latki na tej metodzie: brak w naszym kodzie (grep RBMComputeBlowDamageOnShield - tylko komentarz RotData418). ShieldGuard 414 jest na ComputeBlowDamageOnShield, nie tu - pociski bez zmian.
- Ta sama regula dla gracza i AI; tylko ofiary z perkiem (zwykle bohaterowie). Koszt: jedno sprawdzenie flag na cios w plecy z tarcza, perk czytany tylko przy trafieniu.
- Liczba RBM w tej drodze to obrazenia przechodzace przez tarcze (na cialo), nie HP tarczy - "tarcza wytrzymalsza o 30%" = 30% mniej przechodzi. Jesli Jeff chcial inaczej (np. odejmowanie HP tarczy) - do decyzji.

**Linie logu:** start "Perki pancerza i tarcz (506): latka raz na proces - pancerz i tarcze z perkow 4/4 ... Pavise tarcza na plecach +30% ..., -25% zuzycia, ciosy wrecz w tarcze na plecach (D9) TAK (latka wpieta); ShieldWall ..."; koniec misji "... | Pavise wrecz w tarcze na plecach (D9): ciosow N, obrazenia A -> B | ...". Gracz z Pavise i tarcza na plecach, trafiony wrecz w plecy: N > 0, B ~ A / 1.3.

**Status:** DO SPRAWDZENIA (zbudowane 0 bledow, nie wgrane).

### D16: perki amunicji NavalDLC - odzysk zamiast dodatkowej amunicji

**Problem:** decyzja Jeffa "D16 - odzysk" (PERKI-DECYZJE: "D16 amunicja NavalDLC: odzysk (jak 512)"). Po 515 Boatswain.WellStocked (+30% amunicji na stos, +2 bron miotana) i Boatswain.ShipwrightsInsight (druga premia +25% amunicji zalogi) daja tylko dodatkowa amunicje w bitwie morskiej - klucz NavalDeployAmmoFix domyslnie NIE, wiec perki nic nie robily.

**Zmiana:**
- Armoury/src/Perk512.cs:180-249 - czesc F: perki NavalDLC odbiciem (NavalPerks+Boatswain, wlasciwosci WellStocked / ShipwrightsInsight, czytane przy kazdym uzyciu - obiekt biezacej kampanii), NavalBonus: tylko bitwa morska, klucz NavalAmmoPerksRecover TAK i NavalDeployAmmoFix NIE; partia (MobileParty.HasPerk - rola perka: WellStocked glowna FirstMate, ShipwrightsInsight druga FirstMate, czyli EffectiveFirstMate) dostaje +NavalAmmoPerkPercent (10%) za kazdy perk. Recover (bitwa gracza, AmmoRecovery.cs:91): bitwa morska = Mission.IsNavalBattle (wypad z morza nie), bez misji MapEvent.IsNavalMapEvent; odzysk x (1 + premia B 512 + premia F), najwyzej 100%; odzysk 0 (pole stracone) zostaje 0 - tylko po wygranej.
- AiLoss (:251-266) dostaje MapEvent (AiWear.cs:631 - przekazuje me): bitwy AI bez misji, IsNavalMapEvent, ten sam wzor natezenie x (1 - odzysk x (1 + premie)).
- Klucze NavalAmmoPerksRecover (TAK) i NavalAmmoPerkPercent (10): Settings.cs:1650-1652, McmSettings.cs:4310-4317 (+ ApplyTo :14601), grupa "Ammunition and animal perks" (recznie). Opis Naval Deploy Ammo Fix (Settings / MCM) - zamiast "extra arrows wait for a decision" mowi o odzysku.
- Linia startu 512 (:517-520) - "D16 perki NavalDLC ... -> odzysk po wygranej bitwie morskiej TAK +10% za perk - perki znalezione" (NIECZYNNE, gdy NavalDeployAmmoFix TAK); linia doby (:553) - "D16 odzysk z perkami NavalDLC (bitwa morska): agenci bitwy gracza N, pozycje AI M".
- NavalDeploy515.cs:36-37 - komentarz: przy NIE perki daja odzysk (Perk512 F).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Zamknieta ekonomia: odzysk nie tworzy amunicji - ogranicza strate tego, co wystrzelone (kolczan wraca z prawdopodobienstwem / mniej sztuk schodzi ze zbrojowni AI), jak 512 B. Brak podwojenia: przy NavalDeployAmmoFix TAK (dodatkowa amunicja NavalDLC) odzysk z tych perkow wylaczony - jedna zasada na perk.
- Sumuje sie z 512 B (DeepQuivers / Fletcher / WellPrepared wodza) jednym mnoznikiem - przy domyslnych 10% + 10% + 10% odzysk 65% x 1.3 = 84.5% (ponizej 100%).
- Kolczany / bron miotana / tarcze OKRETU (AdditionalArcherQuivers, AdditionalThrowingWeaponStack, CrewShieldHitPointsFactor - wyposazenie statku, nie perki) zostaja pod NavalDeployAmmoFix (NIE) bez zmian - D16 dotyczy perkow; czy tarcze okretu (nie amunicja) wlaczyc osobno - do decyzji Jeffa.
- Rola perka: NavalDLC w bitwie sprawdza kapitana szyku (Formation.Captain); odzysk liczony po bitwie dla partii - bierzemy role perka (FirstMate partii, jak MobileParty.HasPerk). Partia bez pierwszego oficera: EffectiveFirstMate gry (zwykle wodz).
- AiWear.AmmoUse dziala tylko przy AiAmmoUse (wpis 249) - bez niego D16 dla AI tylko w pomiarze. Brak NavalDLC: perki null, premia 0 (linia startu "brak NavalDLC").
- Wywolania AiLoss / Recover: tylko AiWear.cs:631 i AmmoRecovery.cs:91 (grep) - sygnatura AiLoss zmieniona, oba miejsca zbudowane.

**Linie logu:** start "Perki amunicji i zwierzat (512): start sesji | AmmoPerksRework TAK (...) | D16 perki NavalDLC (WellStocked, ShipwrightsInsight) -> odzysk po wygranej bitwie morskiej TAK +10% za perk - perki znalezione | ..."; doba "Perki amunicji i zwierzat (512): dzien N | ... | D16 odzysk z perkami NavalDLC (bitwa morska): agenci bitwy gracza N, pozycje AI M | ...". Po wygranej bitwie morskiej gracza z pierwszym oficerem z WellStocked: N > 0 i linia AmmoRecovery "wrocilo" wieksza niz bez perka.

**Status:** DO SPRAWDZENIA (zbudowane 0 bledow, nie wgrane). Armoury.dll md5 4b8fceb0.

### Recenzja grupy A (14.11, commit 90b9ee95) - rozpatrzenie uwag

Recenzja potwierdzila D1, D9 i D16 bez bledow poprawnosci, regresji ani kolizji. Jedna uwaga (low):

**Uwaga 1 (low) - zakres D16: kolczany i bron miotana OKRETU bez odzysku.** Sprawdzona: PRAWDZIWA jako niejasnosc zakresu, ODRZUCONA jako zmiana kodu w tej poprawce.
- Pytanie zadane Jeffowi (CHANGELOG, wpis 515, recenzja pkt 100): "WellStocked / ShipwrightsInsight / kolczany okretu to DODATKOWA amunicja ... wlaczyc albo dac TYM PERKOM nowy efekt (odzysk / zuzycie jak 512)". Odpowiedz Jeffa: "D16 - odzysk"; notatka w PERKI-DECYZJE: "D16 amunicja NavalDLC: odzysk (jak 512)". Pytanie wymienia kolczany okretu, ale nowy efekt proponuje "tym perkom" - nie da sie jednoznacznie wyczytac, czy ulepszenie okretu (AdditionalArcherQuivers, AdditionalThrowingWeaponStack - wyposazenie statku, nie perk) tez ma dawac odzysk.
- Dlaczego bez kodu: zasada 3 CLAUDE.md ("zanim cos dodasz przy okazji - nie dodawaj"). Odzysk z ulepszen okretu wymagalby nowego odczytu ulepszen statkow partii po bitwie (NavalDLC, odbiciem) - nowy kod i nowe ryzyko bez wyraznej decyzji Jeffa.
- Stan po tej poprawce (wprost, zeby drugie konto nie bralo tego za przeoczenie): przy NavalDeployAmmoFix = NIE (domyslnie) kolczany i bron miotana OKRETU nie daja nic - ani dodatkowej amunicji, ani odzysku; ulepszenie okretu jest dla gracza martwe. Tarcze okretu (CrewShieldHitPointsFactor) to nie amunicja - poza D16, tez martwe przy NIE.
- **PYTANIE DO JEFFA (D16b):** czy dodatkowe kolczany / bron miotana okretu maja dawac odzysk po wygranej bitwie morskiej (np. +10% za ulepszenie, ten sam warunek NavalDeployAmmoFix = NIE, wspolny limit 100% z 512 B i D16), czy zostaja martwe? Osobno: czy wlaczyc same tarcze okretu? Jesli "tak" - dopisac w Perk512 NavalBonus premie wedlug ulepszen okretow partii (najpierw sprawdzic w dekompilacji NavalDLC, skad NavalAgentStatCalculateModel bierze AdditionalArcherQuivers).

Kod bez zmian w tej poprawce (tylko ten zapis); Armoury przebudowany kontrolnie.
