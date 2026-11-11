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
    /// nie kopniak / uderzenie tarcza, klasa TwoHandedAxe i SwingDamageType == Blunt w DANYCH broni (nie typ ciosu - ten RBM zmienia). Znacznik jest
    /// nadpisywany przy kazdym ciosie i zdejmowany przy pierwszym wywolaniu okna, wiec nie przechodzi na cudzy cios. Zwykly topor trafiajacy
    /// trzonkiem ma SwingDamageType Cut - zostaje przy x1.2 jak dotad. Drugie wywolanie okna (lowerArmorQualityCheck, rbm.cs:5521) liczy wynik,
    /// ktorego potem nie uzywa - bez skutku.
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
        private static bool _native;                         // znacznik: biezacy cios to zamach toporem 2h z rodzima glowica obuchowa

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
                        && ps[5].ParameterType == typeof(float) && ps[5].Name == "skillModifier";
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
                        h.Patch(blow, prefix: new HarmonyMethod(typeof(BluntHammers417), nameof(MarkPrefix)) { priority = Priority.First });
                        if (Owned(blow, h.Id)) _patched++; else miss += " prefiks znacznika NIE wszedl;";
                    }
                    catch (Exception e) { miss += " prefiks znacznika BLAD " + e.GetType().Name + ";"; Log.Error("BluntHammers417.Patch(blow)", e); }
                }
            }

            var s = Settings.Current;
            _note = "mloty i buzdygany 2h " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Mloty (417): latka raz na proces - " + _note + ". Klucz NativeBluntHammersEnabled " + (s != null && s.NativeBluntHammersEnabled ? "TAK" : "NIE")
                     + ": topor 2h z rodzima glowica obuchowa (mloty ROT, maczuga olbrzyma) liczony oknem buzdyganu 2h zamiast ciosu trzonkiem"
                     + " | klucz TwoHandedMaceSkillScaleFix " + (s != null && s.TwoHandedMaceSkillScaleFix ? "TAK" : "NIE")
                     + ": wspolczynnik umiejetnosci buzdyganu 2h 1.125 -> 0.1125 (okno 15-22 x 4.6 bez zmian).");
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

        /// <summary>Przed prefiksem RBM na ComputeBlowDamage: znacznik "zamach toporem 2h z rodzima glowica obuchowa". Same odczyty pol.</summary>
        private static void MarkPrefix(ref AttackCollisionData attackCollisionData, WeaponComponentData attackerWeapon)
        {
            _native = attackerWeapon != null
                      && attackerWeapon.WeaponClass == WeaponClass.TwoHandedAxe
                      && attackerWeapon.SwingDamageType == DamageTypes.Blunt
                      && attackCollisionData.StrikeType == 0
                      && !attackCollisionData.IsMissile
                      && !attackCollisionData.IsAlternativeAttack;
        }

        /// <summary>Okno umiejetnosci RBM. Zwraca true (oryginal RBM) dla wszystkiego poza buzdyganem 2h nie-Pierce i mlotem z rodzimym obuchem.
        /// Liczniki rosna takze przy kluczach NIE (pomiar "przed"). Wzor galezi "TwoHandedMace" przepisany 1:1 z rbm.cs:5794-5811, lacznie
        /// z regula "magnituda do 1 przechodzi bez zmian".</summary>
        private static bool SkillPrefix(ref float __result, float magnitude, string weaponType, DamageTypes damageType, float effectiveSkill, float skillModifier)
        {
            bool native = _native;
            _native = false;                                                                 // znacznik jednorazowy
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
            if (mace)
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
            _native = false;
            _hamBlows = 0; _hamChanged = 0; _maceBlows = 0; _maceChanged = 0; _maceLower = 0;
            _hamBefore = 0f; _hamAfter = 0f; _maceBefore = 0f; _maceAfter = 0f;
            _hHam = 0; _hHamHeavy = 0; _hMace = 0; _hMaceHeavy = 0; _hHamMax = 0; _hMaceMax = 0;
            _hpHam = 0f; _hpMace = 0f;
        }

        private static string Avg(float sum, int n) { return n > 0 ? (sum / n).ToString("0.0", CultureInfo.InvariantCulture) : "-"; }

        private static void Flush(Mission m)
        {
            if (_hamBlows == 0 && _maceBlows == 0 && _hHam == 0 && _hMace == 0) { Reset(); return; }   // misja bez mlotow i buzdyganow 2h - bez linii
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
                     + "; trafienia w ludzi " + _hMace + " srednio " + Avg(_hpMace, _hMace) + " HP, najwiecej " + _hMaceMax + ", od 60 HP " + _hMaceHeavy + ".");
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
