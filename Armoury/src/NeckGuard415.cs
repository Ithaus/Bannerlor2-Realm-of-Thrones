using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 415 (audyt RBM 11.11, B1 + C4): SZYJA I BARKI z helmu i pancerza tulowia, LEZACY z wlasnym mnoznikiem pancerza.
    ///
    /// Gdzie RBM liczy pancerz czesci ciala (rbm.cs:10-19, 459-509): prefiks RBMCombat.ArmorRework.ChangeBodyPartArmor na
    /// Agent.GetBaseArmorEffectivenessForBodyPart zwraca false i podstawia ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM(agent, czesc):
    ///   szyja = helm.arm + korpus.arm ; barki = naramiennik.(body + arm) + korpus.arm ; piers = brzuch = korpus.body.
    /// Te sama metode RBM wola wprost przy obnizce stanu pancerza (lowerArmorQualityCheck, rbm.cs:5495), a przez metode agenta - silnik
    /// (AttackInformation.ArmorAmountFloat, mb_full.cs:47195) i RBMAI (srednia z 9 czesci przy przelamanej posturze, rbmai.cs:7126-7134).
    /// Dane ROT: 1 z 274 helmow ma arm_armor, wiec szyja rycerza w plycie = samo korpus.arm (ok. 24) przy glowie 97; barki bez naramiennika tez ok. 24
    /// przy tulowiu 120. Log: szyja mediana 101 HP na trafienie, barki 45, glowa 11.
    ///
    /// B1 - postfiks na GetBaseArmorEffectivenessForBodyPartRBM (cel RBM, przez TypeByName + DeclaredMethod; metoda statyczna z dwoma switchami -
    /// za duza na wklejenie przez JIT), tylko ludzie, tylko Neck / ShoulderLeft / ShoulderRight, bez alokacji, bez try/catch (same odczyty struktur):
    ///   szyja  += max(0, NeckFromHelmetShare x helm.head - helm.arm)   - helm chroni szyje co najmniej udzialem swojej ochrony glowy
    ///            (to wariant danych z audytu "arm = 0.18-0.25 x head" liczony na zywo; helm z wlasnym arm_armor nie liczy sie dwa razy);
    ///   barki  = max(obecne, ShoulderFromBodyShare x korpus.body)       - naramiennik lepszy od udzialu zostaje bez zmiany.
    /// Trafienie pod pache i w twarz RBM liczy osobno (rbm.cs:3411-3497, wprost z przedmiotow) - zostaja slabymi miejscami.
    ///
    /// Gdzie RBM liczy lezacego (rbm.cs:3508-3511, OverrideDamageCalc.Prefix na MissionCombatMechanicsHelper.ComputeBlowDamage): pancerz x0.75, gdy
    /// ofiara jest w akcji Fall (powalony) ALBO StrikeKnockBack (zatoczony), a napastnik nie jest null. Mnoznik siedzi w srodku prefiksu RBM.
    /// C4 - wlasny prefiks (Priority.First, przed RBM) na te sama metode silnika: tylko czlowiek w akcji Fall, pancerz wejsciowy
    /// (AttackInformation.ArmorAmountFloat) x ProneArmorPercent / 75 - po x0.75 RBM wychodzi dokladnie ProneArmorPercent. Zatoczony zostaje przy
    /// x0.75. Suwak 75 = liczba nietknieta. Prefiks w glownym watku (Mission.GetAttackCollisionResults), struktura budowana na jedno trafienie
    /// (mb_full.cs:58466-58470) - zmiana pola nie wycieka. Osobno od B1 (a nie w postfiksie pancerza), bo metode pancerza wolaja tez obnizka stanu,
    /// postura i cechy oddzialu (HasHeavyArmor) - lezacy ma byc latwiejszy do dobicia, nie "lzej opancerzony" we wszystkich rachunkach.
    /// Trafienie lezacego w twarz / pod pache: RBM podmienia pancerz po naszym prefiksie - tam zostaje x0.75 (odlozone).
    ///
    /// Jedna regula dla gracza i AI. Brak RBM = obie czesci nieczynne (linia logu). Latki w kampanii (NightHooks.InCampaign, raz na proces) - jak 413.
    /// </summary>
    internal static class NeckGuard415
    {
        private const float RbmPronePercent = 75f;

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        private static MethodBase _blow;                     // MissionCombatMechanicsHelper.ComputeBlowDamage
        private static bool _rbmBlow;                        // prefiks RBM (mnoznik x0.75) jest teraz na ComputeBlowDamage

        // liczniki misji - zwykle pola (przyblizone, gdy metode pancerza zawola watek poboczny; trafienia i prefiks - glowny watek)
        private static int _neckCalls, _neckRaised, _shCalls, _shRaised;
        private static float _neckBefore, _neckAfter, _shBefore, _shAfter;
        private static int _proneBlows, _proneChanged;
        private static float _proneArmBefore, _proneArmAfter;
        private static Agent _lastProne;                     // ofiara ostatniego ciosu policzonego jako lezaca - do licznika trafien
        private static int _hNeck, _hNeckHeavy, _hSh, _hShHeavy, _hProne, _hProneHeavy, _hOther;
        private static float _hpNeck, _hpSh, _hpProne, _hpOther;

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 0;
            string miss = "";
            Type t = null;
            try { t = AccessTools.TypeByName("RBMCombat.ArmorRework"); } catch { }
            if (t == null)
            {
                _note = "RBM nieobecny (brak RBMCombat.ArmorRework) - szyja / barki i lezacy nieczynne";
                Log.Info("Szyja i barki (415): " + _note + ".");
                return _note;
            }

            // B1: pancerz czesci ciala RBM
            _wanted++;
            MethodInfo armor = null;
            try { armor = AccessTools.DeclaredMethod(t, "GetBaseArmorEffectivenessForBodyPartRBM", new[] { typeof(Agent), typeof(BoneBodyPartType) }); } catch { }
            if (armor == null || !armor.IsStatic || armor.ReturnType != typeof(float))
                miss += " brak ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM(Agent, BoneBodyPartType);";
            else
            {
                try
                {
                    h.Patch(armor, postfix: new HarmonyMethod(typeof(NeckGuard415), nameof(ArmorPostfix)) { priority = Priority.Last });
                    if (Owned(armor, h.Id, false)) _patched++; else miss += " pancerz czesci ciala NIE wszedl;";
                }
                catch (Exception e) { miss += " pancerz czesci ciala BLAD " + e.GetType().Name + ";"; Log.Error("NeckGuard415.Patch(armor)", e); }
            }

            // C4: mnoznik lezacego - przed prefiksem RBM na ComputeBlowDamage (metoda zadeklarowana w MissionCombatMechanicsHelper, prywatna statyczna)
            _wanted++;
            MethodInfo blow = null;
            try { blow = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamage"); } catch { }
            _blow = blow;
            if (blow == null) miss += " brak MissionCombatMechanicsHelper.ComputeBlowDamage;";
            else
            {
                try
                {
                    h.Patch(blow, prefix: new HarmonyMethod(typeof(NeckGuard415), nameof(PronePrefix)) { priority = Priority.First });
                    if (Owned(blow, h.Id, true)) _patched++; else miss += " lezacy NIE wszedl;";
                }
                catch (Exception e) { miss += " lezacy BLAD " + e.GetType().Name + ";"; Log.Error("NeckGuard415.Patch(blow)", e); }
                RefreshRbm();
                if (!_rbmBlow) miss += " ComputeBlowDamage teraz bez prefiksu RBM (modul walki RBM wylaczony?) - lezacy czeka;";
            }

            var s = Settings.Current;
            _note = "pancerz szyi / barkow i lezacy " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Szyja i barki (415): latka raz na proces - " + _note + ". Klucz NeckShoulderCoverEnabled " + (s != null && s.NeckShoulderCoverEnabled ? "TAK" : "NIE")
                     + ": szyja += max(0, " + F(s != null ? s.NeckFromHelmetShare : 0f) + " x helm.head - helm.arm), barki = max(obecne, "
                     + F(s != null ? s.ShoulderFromBodyShare : 0f) + " x korpus.body); lezacy (akcja Fall) pancerz x" + F((s != null ? s.ProneArmorPercent : RbmPronePercent) / 100f)
                     + " (RBM x0.75, zatoczony bez zmian).");
            return _note;
        }

        /// <summary>RBM zdejmuje i zaklada swoje latki przy zmianie ustawien (RBM.SubModule.ApplyHarmonyPatches). Mnoznik lezacego ma sens tylko,
        /// gdy prefiks RBM (x0.75) jest na ComputeBlowDamage - sprawdzane przy zakladaniu i na starcie kazdej misji, nie przy ciosie.</summary>
        private static void RefreshRbm()
        {
            bool on = false;
            try
            {
                var info = _blow != null ? Harmony.GetPatchInfo(_blow) : null;
                if (info != null)
                    foreach (var p in info.Prefixes)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType != null
                            && (p.PatchMethod.DeclaringType.FullName ?? "").StartsWith("RBMCombat.", StringComparison.Ordinal)) { on = true; break; }
            }
            catch { }
            _rbmBlow = on;
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

        private static bool Owned(MethodBase mi, string id, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in prefix ? info.Prefixes : info.Postfixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(NeckGuard415)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>B1. Po RBM GetBaseArmorEffectivenessForBodyPartRBM(agent, bodyPart). Czyta tylko SpawnEquipment (jak RBM) - bez stanu misji,
        /// bez wywolan silnika, bez alokacji; bezpieczne takze z watku pobocznego (liczniki wtedy przyblizone).</summary>
        private static void ArmorPostfix(Agent __0, BoneBodyPartType __1, ref float __result)
        {
            bool neck = __1 == BoneBodyPartType.Neck;
            if (!neck && __1 != BoneBodyPartType.ShoulderLeft && __1 != BoneBodyPartType.ShoulderRight) return;
            if (__0 == null || !__0.IsHuman) return;
            var s = Settings.Current;
            if (s == null || !s.NeckShoulderCoverEnabled) return;
            var eq = __0.SpawnEquipment;
            if (eq == null) return;
            if (neck)
            {
                _neckCalls++;
                float share = s.NeckFromHelmetShare;
                if (!(share > 0f)) return;
                if (share > 1f) share = 1f;
                var helm = eq[EquipmentIndex.Head];
                var it = helm.Item;
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.HeadArmor) return;
                float add = helm.GetModifiedHeadArmor() * share - helm.GetModifiedArmArmor();   // helm.arm RBM juz doliczyl
                if (!(add > 0f)) return;
                _neckRaised++; _neckBefore += __result; _neckAfter += __result + add;
                __result += add;
            }
            else
            {
                _shCalls++;
                float share = s.ShoulderFromBodyShare;
                if (!(share > 0f)) return;
                if (share > 1f) share = 1f;
                var body = eq[EquipmentIndex.Body];
                var it = body.Item;
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.BodyArmor) return;
                float want = body.GetModifiedBodyArmor() * share;
                if (!(want > __result)) return;
                _shRaised++; _shBefore += __result; _shAfter += want;
                __result = want;
            }
        }

        /// <summary>C4. Przed prefiksem RBM na ComputeBlowDamage. Warunki jak w RBM (ofiara i napastnik nie null), ale tylko czlowiek i tylko akcja Fall.
        /// Licznik ciosow w lezacych rosnie takze przy suwaku 75 (pomiar "przed").</summary>
        private static void PronePrefix(ref AttackInformation attackInformation)
        {
            if (!_rbmBlow) return;                                                           // bez RBM nie ma mnoznika lezacego - nic do zmiany
            var v = attackInformation.VictimAgent;
            if (v == null || attackInformation.AttackerAgent == null || !v.IsHuman) return;
            if (v.GetCurrentActionType(0) != Agent.ActionCodeType.Fall) return;
            _proneBlows++;
            _lastProne = v;
            var s = Settings.Current;
            if (s == null) return;
            float pct = s.ProneArmorPercent;
            if (pct > RbmPronePercent - 0.01f && pct < RbmPronePercent + 0.01f) return;       // 75 = RBM bez zmian
            if (pct < 0f) pct = 0f; else if (pct > 100f) pct = 100f;
            float a = attackInformation.ArmorAmountFloat;
            if (!(a > 0f)) return;
            float b = a * (pct / RbmPronePercent);
            attackInformation.ArmorAmountFloat = b;
            _proneChanged++; _proneArmBefore += a * 0.75f; _proneArmAfter += b * 0.75f;
        }

        private static void Reset()
        {
            _neckCalls = 0; _neckRaised = 0; _shCalls = 0; _shRaised = 0;
            _neckBefore = 0f; _neckAfter = 0f; _shBefore = 0f; _shAfter = 0f;
            _proneBlows = 0; _proneChanged = 0; _proneArmBefore = 0f; _proneArmAfter = 0f;
            _lastProne = null;
            _hNeck = 0; _hNeckHeavy = 0; _hSh = 0; _hShHeavy = 0; _hProne = 0; _hProneHeavy = 0; _hOther = 0;
            _hpNeck = 0f; _hpSh = 0f; _hpProne = 0f; _hpOther = 0f;
        }

        private static string Avg(float sum, int n) { return n > 0 ? F1(sum / n) : "-"; }

        private static void Flush(Mission m)
        {
            if (_hNeck + _hSh + _hProne + _hOther == 0 && _proneBlows == 0) { Reset(); return; }   // misja bez trafien w ludzi - bez linii
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Szyja i barki (415): koniec misji (" + kind + ") | klucz " + (s != null && s.NeckShoulderCoverEnabled ? "TAK" : "NIE")
                     + " udzial szyi " + F(s != null ? s.NeckFromHelmetShare : 0f) + " barkow " + F(s != null ? s.ShoulderFromBodyShare : 0f)
                     + " lezacy " + (s != null ? ((int)s.ProneArmorPercent).ToString() : "?") + "% | latki " + _patched + "/" + _wanted + (_rbmBlow ? "" : " (prefiks RBM na ComputeBlowDamage NIEOBECNY - lezacy nieczynny)")
                     + " | TRAFIENIA w ludzi (nie zablokowane): szyja " + _hNeck + " srednio " + Avg(_hpNeck, _hNeck) + " HP, od 60 HP " + _hNeckHeavy
                     + "; barki " + _hSh + " srednio " + Avg(_hpSh, _hSh) + " HP, od 60 HP " + _hShHeavy
                     + "; lezacy " + _hProne + " srednio " + Avg(_hpProne, _hProne) + " HP, od 60 HP " + _hProneHeavy
                     + "; reszta " + _hOther + " srednio " + Avg(_hpOther, _hOther) + " HP"
                     + " | PANCERZ szyi liczony " + _neckCalls + " razy, podniesiony " + _neckRaised + " (srednio " + Avg(_neckBefore, _neckRaised) + " -> " + Avg(_neckAfter, _neckRaised) + ")"
                     + "; barkow " + _shCalls + ", podniesiony " + _shRaised + " (srednio " + Avg(_shBefore, _shRaised) + " -> " + Avg(_shAfter, _shRaised) + ")"
                     + " | ciosy w lezacych " + _proneBlows + ", pancerz zmieniony " + _proneChanged
                     + " (srednio po mnozniku " + Avg(_proneArmBefore, _proneChanged) + " -> " + Avg(_proneArmAfter, _proneChanged) + ").");
            Reset();
        }

        /// <summary>Liczniki misji: zerowanie na starcie, trafienia wedlug czesci ciala (wrecz i pociski - linie HIT sa tylko dla pociskow),
        /// linia sumy na koncu. Dziala takze przy kluczach NIE / 75 - pomiar "przed".</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
                RefreshRbm();
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                // bez alokacji i bez wywolan silnika; ofiara lezaca = ta, ktora prefiks policzyl przy tym ciosie (znacznik zdejmowany przy kazdym trafieniu)
                var prone = _lastProne;
                _lastProne = null;
                if (isBlocked || affectedAgent == null || !affectedAgent.IsHuman) return;
                if (collisionData.IsFallDamage) return;
                if (prone != null && ReferenceEquals(prone, affectedAgent))
                {
                    _hProne++; _hpProne += damagedHp; if (damagedHp >= 60f) _hProneHeavy++;
                    return;
                }
                var part = collisionData.VictimHitBodyPart;
                if (part == BoneBodyPartType.Neck) { _hNeck++; _hpNeck += damagedHp; if (damagedHp >= 60f) _hNeckHeavy++; }
                else if (part == BoneBodyPartType.ShoulderLeft || part == BoneBodyPartType.ShoulderRight) { _hSh++; _hpSh += damagedHp; if (damagedHp >= 60f) _hShHeavy++; }
                else { _hOther++; _hpOther += damagedHp; }
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("NeckGuard415.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
