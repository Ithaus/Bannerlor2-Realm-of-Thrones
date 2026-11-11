using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 417 (audyt RBM 11.11, B2 + C5): MLOTY DWURECZNE BIJA JAK BRON OBUCHOWA + SKALA UMIEJETNOSCI BUZDYGANOW 2H.
    ///
    /// B2. ROT zapisuje mloty dwureczne i maczuge olbrzyma szablonem TwoHandedAxe z glowica o obrazeniach Blunt (zrzut przedmiotow 11.11:
    /// baratheon_hammer, gendry_hammer, westerling_hammer, sledgehammer, giant_club - piec sztuk, innych toporow 2h z rodzimym obuchem nie ma).
    /// RBM w oknie umiejetnosci (RBMCombat.Utilities.GetSkillBasedDamage, rbm.cs:5669) dla klasy "TwoHandedAxe" z typem Blunt zaklada cios TRZONKIEM
    /// (typ Blunt nadaje sam, gdy topor trafil nie ostrzem - rbm.cs:3410) i liczy  clamp(M + DR x 0.112, 20(1+sm), 26(1+2sm)) x 4 x 0.3;
    /// rodzima glowica obuchowa wpada w te sama galaz. Buzdygan 2h ("TwoHandedMace") ma clamp(M + DR x a, 15(1+sm), 22(1+2sm)) x 4.6 - ok. 3-4x wiecej.
    /// Dalsza czesc wzoru (RBMComputeDamage) dla typu Blunt nie patrzy na klase broni, wiec cala roznica siedzi w tym jednym oknie.
    ///
    /// C5. W galezi "TwoHandedMace" a = 1.125 (rbm.cs:5801). Pokrewne wspolczynniki: miecz 0.133 -> 2h 0.199, topor 0.1 -> 2h 0.15, rekojesci
    /// 0.075 -> 2h 0.112, buzdygan 1h 0.075 - wszedzie x1.5; 0.075 x 1.5 = 0.1125. Przy 1.125 juz umiejetnosc 20 wybija wynik ponad sufit okna,
    /// czyli kazdy cios = maksimum niezaleznie od zamachu. To literowka skali (przecinek o jedno miejsce).
    ///
    /// Jak: bez ruszania danych ROT i bez podmiany IL. Prefiks na GetSkillBasedDamage (publiczna statyczna, dlugi switch po napisie - JIT jej nie
    /// wkleja) liczy galaz buzdyganu 2h sam i pomija oryginal w dwoch przypadkach: (1) "TwoHandedMace" nie-Pierce przy kluczu C5; (2) "TwoHandedAxe"
    /// + Blunt, gdy to RODZIMY obuch. Rodzimosci w tej metodzie nie widac (dostaje tylko napis klasy i typ juz po zamianie na Blunt), wiec znacznik
    /// stawia wlasny prefiks (Priority.First - przed RBM) na MissionCombatMechanicsHelper.ComputeBlowDamage: zamach (StrikeType 0), nie pocisk,
    /// nie kopniak / uderzenie tarcza, klasa TwoHandedAxe i SwingDamageType == Blunt w DANYCH broni (nie typ ciosu - ten RBM zmienia). Zwykly topor
    /// trafiajacy trzonkiem ma SwingDamageType Cut - zostaje przy x1.2 jak dotad.
    ///
    /// 417-p (po recenzji): znacznik ODWROCONY. Okno ma jeszcze wolajacych POZA ComputeBlowDamage, ktorych wynik idzie do gry: karta / porownanie
    /// broni (GetRBMMeleeWeaponStats, rbm.cs:1693-1694) i - w osobnej kopii okna RBMAI.Utilities.GetSkillBasedDamage (rbmai.cs:11937) - obrazenia ciosu,
    /// ktory przebil postawe (calculateHealthDamage, rbmai.cs:7203, wolane z 6421 przy crushThrough). Wszyscy oni podaja typ obrazen Z DANYCH broni
    /// (SwingDamageType), wiec poza ciosem "TwoHandedAxe" + Blunt + zamach = zawsze rodzimy obuch; tylko wewnatrz ComputeBlowDamage typ Blunt moze byc
    /// trzonkiem zwyklego topora. Dlatego: prefiks na ComputeBlowDamage stawia "w ciosie" + "rodzimy obuch tego ciosu", postfiks zdejmuje "w ciosie"
    /// (postfiks biegnie takze, gdy prefiks RBM pominal oryginal; po wyjatku w srodku flage nadpisuje nastepny cios, a do tego czasu wywolania spoza
    /// ciosu ida starym wzorem - nigdy nowym dla cudzej broni). Kopia RBMAI: jej galaz "TwoHandedMace" nie ma literowki (0.0975, okno 20-26, x4),
    /// a "TwoHandedAxe" + Blunt to ta sama liczba x0.3 - prefiks podmienia tam sam napis klasy na "TwoHandedMace" i puszcza oryginal RBMAI (mlot
    /// przebijajacy postawe liczony jak buzdygan 2h TEJ kopii; C5 jej nie dotyczy). Liczniki "ciosy" licza tylko wywolania z wnetrza ComputeBlowDamage;
    /// wywolania poboczne (karta broni, lowerArmorQualityCheck rbm.cs:5521 - wynik nieuzywany) ida do osobnej rubryki.
    /// Mlot trafiajacy drzewcem tez dostaje pelne okno (RBM nie rozroznia tego dla typu juz-Blunt, tak samo buzdygan 2h) - zawyza srednie w scisku.
    ///
    /// Predkosc zamachu (klasa TwoHandedAxe: x0.75 x (1 + DR/800), buzdygan: x0.83 x (1 + DR/1000)), koszt postury i reszta zostaja wedlug klasy
    /// z danych - zmienia sie tylko okno umiejetnosci. Wszystko w glownym watku (GetAttackCollisionResults). Zero alokacji, bez try/catch.
    /// Jedna regula dla gracza i AI. Brak RBM = nieczynne (linia logu). Latki w kampanii (NightHooks.InCampaign, raz na proces) - jak 413 / 415 / 416.
    /// </summary>
    internal static class BluntHammers417
    {
        private const float MaceScaleRbm = 1.125f;           // stala RBM (rbm.cs:5801)
        private const float MaceScaleFixed = 0.1125f;        // 0.075 (buzdygan 1h) x 1.5 - jak miecz i topor 1h -> 2h
        private const float HandleScale = 0.112f;            // galaz "TwoHandedAxe" + Blunt (cios trzonkiem) - tylko do pomiaru "przed" w logu

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        private static bool _inBlow;                         // 417-p: jestesmy wewnatrz ComputeBlowDamage (miedzy naszym prefiksem a postfiksem)
        private static bool _native;                         // biezacy cios (tylko przy _inBlow) to zamach toporem 2h z rodzima glowica obuchowa
        private static int _sideCalls, _aiHam, _aiHamChanged; // 417-p: wywolania okna poza ciosem (mloty i buzdygany 2h) / okno RBMAI dla mlota / z tego podmienione

        /// <summary>417-p: latka zostaje w procesie po wyjsciu z kampanii - zachowanie misji dodawane takze w bitwie wlasnej (jak 415-p / 416-p).</summary>
        internal static bool Patched { get { return _patched > 0; } }

        // liczniki misji (glowny watek)
        private static int _hamBlows, _hamChanged, _maceBlows, _maceChanged, _maceLower;
        private static float _hamBefore, _hamAfter, _maceBefore, _maceAfter;
        private static int _hHam, _hHamHeavy, _hMace, _hMaceHeavy, _hHamMax, _hMaceMax;
        private static float _hpHam, _hpMace;

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 0;
            string miss = "";
            Type tUtil = null;
            try { tUtil = AccessTools.TypeByName("RBMCombat.Utilities"); } catch { }
            if (tUtil == null)
            {
                _note = "RBM nieobecny (brak RBMCombat.Utilities) - mloty i buzdygany 2h nietkniete";
                Log.Info("Mloty (417): " + _note + ".");
                return _note;
            }

            // okno umiejetnosci RBM - sygnatura sprawdzana, zeby prefiks nie wszedl na inna wersje metody
            _wanted++;
            MethodInfo skill = null;
            try { skill = AccessTools.DeclaredMethod(tUtil, "GetSkillBasedDamage"); } catch { }
            bool sigOk = false;
            if (skill != null && skill.IsStatic && skill.ReturnType == typeof(float))
            {
                var ps = skill.GetParameters();
                sigOk = ps.Length == 8
                        && ps[0].ParameterType == typeof(float) && ps[0].Name == "magnitude"
                        && ps[2].ParameterType == typeof(string) && ps[2].Name == "weaponType"
                        && ps[3].ParameterType == typeof(DamageTypes) && ps[3].Name == "damageType"
                        && ps[4].ParameterType == typeof(float) && ps[4].Name == "effectiveSkill"
                        && ps[5].ParameterType == typeof(float) && ps[5].Name == "skillModifier"
                        && ps[6].ParameterType == typeof(StrikeType) && ps[6].Name == "strikeType";
            }
            MethodInfo blow = null;
            try { blow = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamage"); } catch { }
            if (skill == null) miss += " brak RBMCombat.Utilities.GetSkillBasedDamage;";
            else if (!sigOk) miss += " GetSkillBasedDamage ma inna sygnature - latka NIE zalozona;";
            else
            {
                try
                {
                    h.Patch(skill, prefix: new HarmonyMethod(typeof(BluntHammers417), nameof(SkillPrefix)));
                    if (Owned(skill, h.Id)) _patched++; else miss += " prefiks okna NIE wszedl;";
                }
                catch (Exception e) { miss += " prefiks okna BLAD " + e.GetType().Name + ";"; Log.Error("BluntHammers417.Patch(skill)", e); }

                // znacznik rodzimego obuchu - tylko gdy okno weszlo (bez okna znacznik nie ma odbiorcy)
                _wanted++;
                if (blow == null) miss += " brak MissionCombatMechanicsHelper.ComputeBlowDamage (mloty nieczynne, buzdygany 2h dzialaja);";
                else if (_patched > 0)
                {
                    try
                    {
                        // 417-p: prefiks i postfiks jednym wywolaniem - albo oba, albo zaden (sam prefiks zostawilby "w ciosie" na stale)
                        h.Patch(blow, prefix: new HarmonyMethod(typeof(BluntHammers417), nameof(MarkPrefix)) { priority = Priority.First },
                                postfix: new HarmonyMethod(typeof(BluntHammers417), nameof(MarkPostfix)));
                        if (Owned(blow, h.Id) && OwnedPost(blow, h.Id)) _patched++; else miss += " znacznik ciosu (prefiks + postfiks) NIE wszedl;";
                    }
                    catch (Exception e) { miss += " znacznik ciosu BLAD " + e.GetType().Name + ";"; Log.Error("BluntHammers417.Patch(blow)", e); }
                }
            }

            // 417-p: kopia okna w RBMAI (obrazenia ciosu przebijajacego postawe) - osobna latka, brak modulu AI RBM = tylko linia
            _wanted++;
            Type tAi = null;
            try { tAi = AccessTools.TypeByName("RBMAI.Utilities"); } catch { }
            MethodInfo aiSkill = null;
            if (tAi != null) { try { aiSkill = AccessTools.DeclaredMethod(tAi, "GetSkillBasedDamage"); } catch { } }
            bool aiSig = false;
            if (aiSkill != null && aiSkill.IsStatic && aiSkill.ReturnType == typeof(float))
            {
                var ps = aiSkill.GetParameters();
                aiSig = ps.Length == 8
                        && ps[2].ParameterType == typeof(string) && ps[2].Name == "weaponType"
                        && ps[3].ParameterType == typeof(DamageTypes) && ps[3].Name == "damageType"
                        && ps[6].ParameterType == typeof(StrikeType) && ps[6].Name == "strikeType";
            }
            if (tAi == null) miss += " brak RBMAI.Utilities (przebicie postawy mlotem po staremu);";
            else if (aiSkill == null) miss += " brak RBMAI.Utilities.GetSkillBasedDamage;";
            else if (!aiSig) miss += " RBMAI GetSkillBasedDamage ma inna sygnature - latka NIE zalozona;";
            else
            {
                try
                {
                    h.Patch(aiSkill, prefix: new HarmonyMethod(typeof(BluntHammers417), nameof(AiSkillPrefix)));
                    if (Owned(aiSkill, h.Id)) _patched++; else miss += " prefiks okna RBMAI NIE wszedl;";
                }
                catch (Exception e) { miss += " prefiks okna RBMAI BLAD " + e.GetType().Name + ";"; Log.Error("BluntHammers417.Patch(aiSkill)", e); }
            }

            var s = Settings.Current;
            _note = "mloty i buzdygany 2h " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Mloty (417): latka raz na proces - " + _note + ". Klucz NativeBluntHammersEnabled " + (s != null && s.NativeBluntHammersEnabled ? "TAK" : "NIE")
                     + ": topor 2h z rodzima glowica obuchowa (mloty ROT, maczuga olbrzyma) liczony oknem buzdyganu 2h zamiast ciosu trzonkiem"
                     + " | klucz TwoHandedMaceSkillScaleFix " + (s != null && s.TwoHandedMaceSkillScaleFix ? "TAK" : "NIE")
                     + ": wspolczynnik umiejetnosci buzdyganu 2h 1.125 -> 0.1125 (okno 15-22 x 4.6 bez zmian)"
                     + " | 417-p: mlot liczony tak samo w ciosie, w karcie broni i przy przebiciu postawy (okno RBMAI: galaz buzdyganu 2h tej kopii, x4 zamiast x1.2).");
            return _note;
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Prefixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(BluntHammers417)) return true;
            }
            catch { }
            return false;
        }

        private static bool OwnedPost(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Postfixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(BluntHammers417)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Przed prefiksem RBM na ComputeBlowDamage: "w ciosie" + znacznik "zamach toporem 2h z rodzima glowica obuchowa". Same odczyty pol.</summary>
        private static void MarkPrefix(ref AttackCollisionData attackCollisionData, WeaponComponentData attackerWeapon)
        {
            _inBlow = true;
            _native = attackerWeapon != null
                      && attackerWeapon.WeaponClass == WeaponClass.TwoHandedAxe
                      && attackerWeapon.SwingDamageType == DamageTypes.Blunt
                      && attackCollisionData.StrikeType == 0
                      && !attackCollisionData.IsMissile
                      && !attackCollisionData.IsAlternativeAttack;
        }

        /// <summary>417-p: koniec ComputeBlowDamage (takze gdy prefiks RBM pominal oryginal) - od teraz wywolania okna sa "poza ciosem".</summary>
        private static void MarkPostfix()
        {
            _inBlow = false;
            _native = false;
        }

        /// <summary>417-p: kopia okna RBMAI (cios, ktory przebil postawe). Wolajacy podaja typ z danych broni, wiec "TwoHandedAxe" + Blunt + zamach to
        /// zawsze rodzimy obuch: podmiana napisu klasy na "TwoHandedMace" i oryginal RBMAI (jego galaz buzdyganu 2h). Stala napisowa - bez alokacji.</summary>
        private static void AiSkillPrefix(ref string weaponType, DamageTypes damageType, StrikeType strikeType)
        {
            if (damageType != DamageTypes.Blunt || strikeType != StrikeType.Swing || weaponType == null || weaponType.Length != 12
                || !string.Equals(weaponType, "TwoHandedAxe", StringComparison.Ordinal)) return;
            _aiHam++;
            var s = Settings.Current;
            if (s == null || !s.NativeBluntHammersEnabled) return;
            _aiHamChanged++;
            weaponType = "TwoHandedMace";
        }

        /// <summary>Okno umiejetnosci RBM. Zwraca true (oryginal RBM) dla wszystkiego poza buzdyganem 2h nie-Pierce i mlotem z rodzimym obuchem.
        /// Liczniki rosna takze przy kluczach NIE (pomiar "przed"). Wzor galezi "TwoHandedMace" przepisany 1:1 z rbm.cs:5794-5811, lacznie
        /// z regula "magnituda do 1 przechodzi bez zmian". 417-p: w ciosie rodzimosc mowi znacznik, poza ciosem - sam typ Blunt przy zamachu
        /// (typ z danych broni); liczniki "ciosy" tylko z wnetrza ComputeBlowDamage.</summary>
        private static bool SkillPrefix(ref float __result, float magnitude, string weaponType, DamageTypes damageType, float effectiveSkill, float skillModifier,
            StrikeType strikeType)
        {
            bool inBlow = _inBlow;
            bool native = inBlow ? _native : strikeType == StrikeType.Swing;
            if (weaponType == null) return true;
            bool mace;
            if (weaponType.Length == 13)
            {
                if (damageType == DamageTypes.Pierce || !string.Equals(weaponType, "TwoHandedMace", StringComparison.Ordinal)) return true;
                mace = true;
            }
            else if (native && damageType == DamageTypes.Blunt && weaponType.Length == 12 && string.Equals(weaponType, "TwoHandedAxe", StringComparison.Ordinal))
                mace = false;
            else return true;

            if (!(magnitude > 1f)) return true;                                              // RBM takiej magnitudy nie rusza w zadnej z galezi (takze NaN)
            var s = Settings.Current;
            bool fix = s != null && s.TwoHandedMaceSkillScaleFix;
            float min = 15f * (1f + skillModifier);
            float max = 22f * (1f + 2f * skillModifier);
            float after = MBMath.ClampFloat(magnitude + effectiveSkill * (fix ? MaceScaleFixed : MaceScaleRbm), min, max) * 4.6f;
            if (!inBlow)
            {
                // 417-p: karta broni, lowerArmorQualityCheck - ta sama regula co w ciosie, ale poza licznikami "ciosy" i srednimi
                _sideCalls++;
                if (mace ? !fix : (s == null || !s.NativeBluntHammersEnabled)) return true;
            }
            else if (mace)
            {
                _maceBlows++;
                float before = MBMath.ClampFloat(magnitude + effectiveSkill * MaceScaleRbm, min, max) * 4.6f;
                _maceBefore += before;
                if (!fix) { _maceAfter += before; return true; }
                _maceChanged++; _maceAfter += after;
                if (after < before) _maceLower++;
            }
            else
            {
                _hamBlows++;
                float before = MBMath.ClampFloat(magnitude + effectiveSkill * HandleScale, 20f * (1f + skillModifier), 26f * (1f + 2f * skillModifier)) * 4f * 0.3f;
                _hamBefore += before;
                if (s == null || !s.NativeBluntHammersEnabled) { _hamAfter += before; return true; }
                _hamChanged++; _hamAfter += after;
            }
            __result = after;
            return false;
        }

        private static void Reset()
        {
            _native = false; _inBlow = false;
            _sideCalls = 0; _aiHam = 0; _aiHamChanged = 0;
            _hamBlows = 0; _hamChanged = 0; _maceBlows = 0; _maceChanged = 0; _maceLower = 0;
            _hamBefore = 0f; _hamAfter = 0f; _maceBefore = 0f; _maceAfter = 0f;
            _hHam = 0; _hHamHeavy = 0; _hMace = 0; _hMaceHeavy = 0; _hHamMax = 0; _hMaceMax = 0;
            _hpHam = 0f; _hpMace = 0f;
        }

        private static string Avg(float sum, int n) { return n > 0 ? (sum / n).ToString("0.0", CultureInfo.InvariantCulture) : "-"; }

        private static void Flush(Mission m)
        {
            if (_hamBlows == 0 && _maceBlows == 0 && _hHam == 0 && _hMace == 0 && _aiHam == 0) { Reset(); return; }   // misja bez mlotow i buzdyganow 2h - bez linii
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Mloty (417): koniec misji (" + kind + ") | latka " + _patched + "/" + _wanted
                     + " | MLOTY (topor 2h z rodzimym obuchem), klucz " + (s != null && s.NativeBluntHammersEnabled ? "TAK" : "NIE")
                     + ": ciosy " + _hamBlows + ", policzone oknem buzdyganu " + _hamChanged
                     + ", sila po oknie srednio " + Avg(_hamBefore, _hamBlows) + " (trzonek) -> " + Avg(_hamAfter, _hamBlows)
                     + "; trafienia w ludzi (nie zablokowane) " + _hHam + " srednio " + Avg(_hpHam, _hHam) + " HP, najwiecej " + _hHamMax + ", od 60 HP " + _hHamHeavy
                     + " | BUZDYGANY 2H, klucz " + (s != null && s.TwoHandedMaceSkillScaleFix ? "TAK" : "NIE")
                     + ": ciosy " + _maceBlows + ", policzone z 0.1125 " + _maceChanged + " (ponizej sufitu okna " + _maceLower + ")"
                     + ", sila po oknie srednio " + Avg(_maceBefore, _maceBlows) + " (1.125) -> " + Avg(_maceAfter, _maceBlows)
                     + "; trafienia w ludzi " + _hMace + " srednio " + Avg(_hpMace, _hMace) + " HP, najwiecej " + _hMaceMax + ", od 60 HP " + _hMaceHeavy
                     + " | 417-p: przebicie postawy mlotem (okno RBMAI) " + _aiHam + ", z tego liczone jak buzdygan 2h " + _aiHamChanged
                     + "; wywolania okna poza ciosem (karta broni, kontrola pancerza - poza srednimi) " + _sideCalls
                     + " | ciosy = wywolania okna z wnetrza ComputeBlowDamage (takze zablokowane i w konie); mlot trafiajacy drzewcem liczony jak glowica (jak buzdygan 2h w RBM).");
            Reset();
        }

        /// <summary>Liczniki misji: zerowanie na starcie, trafienia mlotami i buzdyganami 2h w ludzi, linia sumy na koncu. Dziala takze przy kluczach NIE.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                // bez alokacji: klasa broni z danych, te same warunki co znacznik
                if (attackerWeapon == null || isBlocked || affectedAgent == null || collisionData.IsMissile || collisionData.IsAlternativeAttack
                    || collisionData.StrikeType != 0) return;
                var wc = attackerWeapon.WeaponClass;
                bool ham = wc == WeaponClass.TwoHandedAxe && attackerWeapon.SwingDamageType == DamageTypes.Blunt;
                if (!ham && wc != WeaponClass.TwoHandedMace) return;
                if (!affectedAgent.IsHuman) return;
                int hp = (int)damagedHp;
                if (ham) { _hHam++; _hpHam += damagedHp; if (hp >= 60) _hHamHeavy++; if (hp > _hHamMax) _hHamMax = hp; }
                else { _hMace++; _hpMace += damagedHp; if (hp >= 60) _hMaceHeavy++; if (hp > _hMaceMax) _hMaceMax = hp; }
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("BluntHammers417.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
