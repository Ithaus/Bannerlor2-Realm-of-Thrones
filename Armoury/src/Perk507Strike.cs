using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 507 (audyt perkow 14.11, punkty 5, 10, 11, 22, 29, 35, 38, 39, 47, 81, 83, 87, 90; Jeff 14.11 "78-105 ok", "rob 1-7 od razu na raz").
    /// SILA CIOSU I PREDKOSC KONIA Z PERKOW PO RBM.
    ///
    /// A) PERKI PREDKOSCI I KUZNI W CIOSIE WRECZ. Gra (SandboxStrikeMagnitudeModel.CalculateStrikeMagnitudeForSwing / ForThrust) mnozy predkosc
    ///    ruchu atakujacego (extraLinearSpeed, tylko gdy &gt; 0 - cios z rozpedu/biegu) przez perki: pieszo RecklessCharge (2H, osobisty), DashAndSlash
    ///    (osobisty), SurgingBlow (osobisty + kapitan); drzewcowa: Lancer (kapitan), konno Lancer (osobisty) i UnstoppableForce (kapitan); potem bron
    ///    wykuta przez gracza (item.IsCraftedByPlayer) x SharpenedEdge (ciecie) / SharpenedTip (pchniecie). RBM (MagnitudeChanges
    ///    .CalculateBaseMeleeBlowMagnitudePatch, prefiks na MissionCombatMechanicsHelper.CalculateBaseMeleeBlowMagnitude) liczy sile ciosu sam
    ///    i NIE wola modelu - wszystkie te premie przepadaja. Postfiks na tej samej metodzie (tylko gdy oryginal NIE biegl, __runOriginal == false,
    ///    czyli prefiks RBM zdjal gre - przy wylaczonym RBM albo przy jego zejsciu do gry (pchniecie bez agenta) perki liczy gra sama): ta sama
    ///    predkosc z perkami co w grze (te same wywolania PerkHelper), a sila ciosu z ta predkoscia liczona PONOWNIE wzorem RBM (wolanie tej samej
    ///    metody - prefiks RBM; nasz postfiks w srodku odbity flaga watku). Potem Sharpened jak w grze. NomadicTraditions (kapitan konnych) swiadomie
    ///    pominiety - punkt 28 audytu (osobna zmiana).
    /// B) RunningThrow (osobisty, Add 0.25): gra dodaje do predkosci pocisku nadwyzke ponad predkosc bazowa x premia. RBM (ComputeBlowMagnitudeMissilePatch)
    ///    liczy sile pocisku sam (MagnitudeChanges.CalculateMissileMagnitude, energia kinetyczna) - premia przepada. Postfiks: ta sama predkosc z premia,
    ///    sila = wynik x (RBM(predkosc z premia) / RBM(predkosc)) - wzor RBM odbiciem. Tylko gdy oryginal nie biegl.
    /// C) PERKI STYLOW BK BEZ KODU (obrazenia): Klibanophori (styl Cataphract) - konno pchniecie +6% (ty i formacja), konno -5% otrzymanych obrazen
    ///    (ty i formacja; trafienia w cialo, nie w tarcze); FianHighlander - pieszo miecz dwureczny +4% (ty i formacja); FianFennid - bron dwureczna +10%
    ///    (ty); VaryagDrengr - piechota formacji, ktora prowadzisz, wrecz +10%. Postfiks na MissionCombatMechanicsHelper.ComputeBlowMagnitude (wszystkie
    ///    rodzaje ciosu) mnozy baseMagnitude i specialMagnitude. RBM (DamageRework) dzieli BaseMagnitude tylko przez GetWeaponDamageMultiplier modelu,
    ///    wiec nasz mnoznik przezywa. Pozostale polowy perkow Varyag/Kheshig - zmiana 514.
    /// D) KONIE: RBM (HorseChanges, postfiks na SandboxAgentStatCalculateModel.UpdateHorseStats) liczy MountSpeed i MountChargeDamage od nowa -
    ///    SweepingWind (+5% predkosci konia, osobisty jezdzca) przepada. Perki BK CataphractEquites (szarza +10%), CataphractAdaptiveTactics (zwrotnosc
    ///    +8%), JawwalCamelMaster (predkosc +8%), KheshigOutrider (predkosc +5%) BK mnozy na agencie JEZDZCA (VanillaModelTweakPatches.UpdateAgentStats),
    ///    a gra czyta je tylko z agenta konia - martwe. Postfiks na UpdateHorseStats (Priority.Low, po RBM): na koniu, tymi samymi wartosciami.
    ///    SweepingWind tylko gdy postfiks RBM jest na celu (inaczej liczy gra). Wynik modelu jest swiezy przy kazdym wolaniu - mnozenie idempotentne;
    ///    HorseWind (136, postfiks Last na UpdateAgentStats) bierze juz predkosc z premia jako Vbase.
    /// Kto: gracz i AI jednakowo (perki osobiste bohatera, kapitana formacji; BK: wyksztalcenie bohatera albo kapitana jego formacji).
    /// Klucze: PerkStrikeAfterRbm (A, B), PerkLifestyleDamage (C), PerkMountAfterRbm (D). Bledy liczone (potkniecia), bez wylacznika. Bez stanu w zapisie.
    /// Linie: "Sila ciosu perkow (507): start sesji ..." (OnSessionLaunched) i "Sila ciosu perkow (507): dzien N ..." (doba z bitwami).
    /// </summary>
    internal static class Perk507
    {
        internal const string P = "Sila ciosu perkow (507): ";
        private const string Owner = "com.jeff.armoury.perk507";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static Harmony _h;
        private static bool _hooked;
        private static string _hMelee = "nie wpieta", _hMissile = "nie wpieta", _hBlow = "nie wpieta", _hHorse = "nie wpieta";
        private static int _stumbles, _dStumbles;

        [ThreadStatic] private static bool _inner;   // wolanie RBM z naszego postfiksu - nie liczyc drugi raz

        // RBM: wzor sily pocisku (odbiciem; null = premia pocisku nieczynna)
        private delegate float MissileFn(WeaponClass weaponClass, float weaponWeight, float missileSpeed, float missileTotalDamage, float momentumRemaining, DamageTypes damageType);
        private static MissileFn _rbmMissile;
        private static MethodBase _horseTarget;

        // ---- liczniki doby (tylko log)
        private static readonly string[] Names =
        {
            "RecklessCharge", "DashAndSlash", "SurgingBlow", "SurgingBlow(kapitan)", "Lancer(kapitan)", "Lancer", "UnstoppableForce(kapitan)",
            "SharpenedEdge", "SharpenedTip", "RunningThrow",
            "BK Highlander", "BK Fennid", "BK Drengr", "BK Klibanophori pchniecie", "BK Klibanophori ochrona",
            "SweepingWind", "BK Equites", "BK AdaptiveTactics", "BK CamelMaster", "BK Outrider"
        };
        private const int IReckless = 0, IDash = 1, ISurge = 2, ISurgeC = 3, ILancerC = 4, ILancer = 5, IUnstopC = 6, IEdge = 7, ITip = 8, IRun = 9,
            IHigh = 10, IFennid = 11, IDrengr = 12, IKlibT = 13, IKlibD = 14, ISweep = 15, IEquites = 16, IAdapt = 17, ICamel = 18, IOutrider = 19;
        private static readonly int[] _dHit = new int[20];
        private static int _dMelee, _dMeleeUp, _dMissileUp, _dHorse;
        private static double _dMeleeRatio, _dMissileRatio;

        // ---- BK (odbiciem, leniwie)
        private const int BHigh = 1, BFennid = 2, BDrengr = 4, BKlib = 8, BEquites = 16, BAdapt = 32, BCamel = 64, BOutrider = 128;
        private static bool _bkTypesTried, _bkTypesOk, _bkOk;   // typy/odbicie raz na proces; obiekty perkow co sesje (recenzja 507)
        private static string _bkState = "nie szukane";
        private static Type _bkPerksT;
        private static bool _bkOnHorse;   // BK sam latka UpdateHorseStats (poprawka u autora) - czesc BK w D pominieta
        private static PropertyInfo _cfgInstance, _eduManager;
        private static MethodInfo _getEducation, _hasPerk;
        private static object[] _bkPerks;   // indeks = bit (0..7)
        private static readonly string[] BkProps =
        {
            "FianHighlander", "FianFennid", "VaryagDrengr", "CataphractKlibanophoros",
            "CataphractEquites", "CataphractAdaptiveTactics", "JawwalCamelMaster", "KheshigOutrider"
        };

        // ---- pamiec misji (wyksztalcenie BK bohatera, obecnosc RBM na koniu)
        private static WeakReference _mission = new WeakReference(null);   // slaba referencja - zakonczona misja nie zostaje w pamieci (recenzja 507)
        private static readonly Dictionary<Hero, int> _mask = new Dictionary<Hero, int>();
        private static int _rbmHorse = -1;
        private static string _rbmVer = "?";

        // drugie wolanie gry bez predkosci (MissionCombatMechanicsHelper.ComputeBlowMagnitudeMelee - do napisu premii predkosci): nie liczyc w logu
        [ThreadStatic] private static bool _expectSecond;
        [ThreadStatic] private static float _lastProgress, _lastImpact;
        private static readonly object _lock = new object();

        private static string F2(double v) { return v.ToString("0.00", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("Perk507." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static void ResetDay()
        {
            Array.Clear(_dHit, 0, _dHit.Length);
            _dMelee = _dMeleeUp = _dMissileUp = _dHorse = 0; _dMeleeRatio = _dMissileRatio = 0; _dStumbles = 0;
        }

        // ============================================================ wpiecie

        /// <summary>OnSessionLaunched: latki raz na proces, BK odbiciem, linia startu.</summary>
        internal static void EnsureHooks()
        {
            ResetDay();
            try
            {
                if (!_hooked)
                {
                    _hooked = true;
                    if (_h == null) _h = new Harmony(Owner);
                    var t = typeof(MissionCombatMechanicsHelper);
                    _hMelee = Hook(AccessTools.DeclaredMethod(t, "CalculateBaseMeleeBlowMagnitude"), nameof(MeleePostfix), Priority.Low);
                    _hMissile = Hook(AccessTools.DeclaredMethod(t, "ComputeBlowMagnitudeMissile"), nameof(MissilePostfix), Priority.Low);
                    _hBlow = Hook(AccessTools.DeclaredMethod(t, "ComputeBlowMagnitude"), nameof(BlowPostfix), Priority.Low);
                    _horseTarget = AccessTools.DeclaredMethod(typeof(SandBox.GameComponents.SandboxAgentStatCalculateModel), "UpdateHorseStats");
                    _hHorse = Hook(_horseTarget, nameof(HorsePostfix), Priority.Low);
                    try
                    {
                        var rt = AccessTools.TypeByName("RBMCombat.MagnitudeChanges");
                        var mm = rt != null ? AccessTools.DeclaredMethod(rt, "CalculateMissileMagnitude") : null;
                        _rbmMissile = mm != null ? AccessTools.MethodDelegate<MissileFn>((MethodInfo)mm) : null;
                    }
                    catch (Exception e) { _rbmMissile = null; Stumble("RbmMissile", e); }
                }
            }
            catch (Exception e) { Stumble("EnsureHooks", e); }
            // co sesje: obiekty perkow BK od nowa (BK tworzy nowe PerkObject przy kazdym starcie gry, HasPerk porownuje referencje), pamiec misji czysta
            lock (_lock) { _mission = new WeakReference(null); _mask.Clear(); _rbmHorse = -1; }
            try { BkRefresh(); } catch (Exception e) { _bkOk = false; _bkState = "wyjatek " + e.GetType().Name; Stumble("BkRefresh", e); }
            _bkOnHorse = ForeignOn(_horseTarget, "BannerKings");
            try
            {
                var rt = AccessTools.TypeByName("RBMCombat.MagnitudeChanges");
                _rbmVer = rt != null ? rt.Assembly.GetName().Version.ToString() : "brak";
            }
            catch { _rbmVer = "?"; }
            string foreignMelee = ForeignPostfixes(AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "CalculateBaseMeleeBlowMagnitude"));

            var s = Settings.Current;
            string rbmMelee = RbmPrefixOn(AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "CalculateBaseMeleeBlowMagnitude"));
            string rbmMissile = RbmPrefixOn(AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowMagnitudeMissile"));
            string rbmHorse = RbmPostfixOn(_horseTarget) ? "TAK" : "NIE";
            Log.Info(P + "start sesji | klucze PerkStrikeAfterRbm " + Yn(s != null && s.PerkStrikeAfterRbm)
                     + ", PerkLifestyleDamage " + Yn(s != null && s.PerkLifestyleDamage) + ", PerkMountAfterRbm " + Yn(s != null && s.PerkMountAfterRbm)
                     + " | postfiksy: cios wrecz " + _hMelee + " (prefiks RBM " + rbmMelee + "), pocisk " + _hMissile + " (prefiks RBM " + rbmMissile
                     + ", wzor RBM pocisku " + (_rbmMissile != null ? "TAK" : "BRAK") + "), cios ogolnie " + _hBlow + ", kon " + _hHorse
                     + " (postfiks RBM na UpdateHorseStats " + rbmHorse + ")"
                     + " | RBM " + _rbmVer + " | obce postfiksy na sile ciosu wrecz: " + foreignMelee
                     + " | BK " + _bkState + (_bkOnHorse ? " | BK juz latka UpdateHorseStats - perki konne BK na koniu pominiete" : "")
                     + " | perki: predkosc wrecz RecklessCharge/DashAndSlash/SurgingBlow/Lancer/UnstoppableForce, kuznia SharpenedEdge/Tip, RunningThrow,"
                     + " SweepingWind; BK Klibanophori/Highlander/Fennid/Drengr (obrazenia), Equites/AdaptiveTactics/CamelMaster/Outrider (kon)"
                     + " | potkniecia " + _stumbles + ".");
        }

        private static string Yn(bool b) { return b ? "TAK" : "NIE"; }

        private static string Hook(MethodBase target, string postfix, int priority)
        {
            if (target == null) return "BRAK celu";
            try
            {
                _h.Patch(target, postfix: new HarmonyMethod(typeof(Perk507), postfix) { priority = priority });
                var info = Harmony.GetPatchInfo(target);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == Owner) return "wpieta";
                return "NIE widac w GetPatchInfo";
            }
            catch (Exception e) { Stumble("Hook(" + target.Name + ")", e); return "wyjatek " + e.GetType().Name; }
        }

        private static bool IsRbm(Patch p)
        {
            if (p == null) return false;
            if (p.owner == "com.rbmcombat") return true;
            var dt = p.PatchMethod != null ? p.PatchMethod.DeclaringType : null;
            return dt != null && dt.FullName != null && dt.FullName.StartsWith("RBMCombat", StringComparison.Ordinal);
        }

        private static string RbmPrefixOn(MethodBase m)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info != null) foreach (var p in info.Prefixes) if (IsRbm(p)) return "TAK";
                return "NIE (gra liczy perki sama - latka nic nie doda)";
            }
            catch { return "?"; }
        }

        private static bool RbmPostfixOn(MethodBase m)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info != null) foreach (var p in info.Postfixes) if (IsRbm(p)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Czy na celu jest latka (dowolna) z typu w przestrzeni nazw ns.</summary>
        private static bool ForeignOn(MethodBase m, string ns)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info == null) return false;
                foreach (var list in new[] { info.Prefixes, info.Postfixes, info.Transpilers })
                    foreach (var p in list)
                    {
                        var dt = p != null && p.PatchMethod != null ? p.PatchMethod.DeclaringType : null;
                        if (dt != null && dt.FullName != null && dt.FullName.StartsWith(ns, StringComparison.Ordinal)) return true;
                    }
            }
            catch { }
            return false;
        }

        /// <summary>Postfiksy innych modow niz RBM i nasz (do linii startu - kolizja widoczna w logu).</summary>
        private static string ForeignPostfixes(MethodBase m)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                var l = new List<string>();
                if (info != null)
                    foreach (var p in info.Postfixes)
                        if (p != null && p.owner != Owner && !IsRbm(p))
                            l.Add(p.owner + (p.PatchMethod != null && p.PatchMethod.DeclaringType != null ? "/" + p.PatchMethod.DeclaringType.Name : ""));
                return l.Count > 0 ? string.Join(", ", l) + " (premia predkosci skaluje ich wynik)" : "brak";
            }
            catch { return "?"; }
        }

        // ============================================================ A) cios wrecz

        /// <summary>Postfiks MissionCombatMechanicsHelper.CalculateBaseMeleeBlowMagnitude - tylko gdy prefiks (RBM) zdjal metode gry.</summary>
        public static void MeleePostfix(in AttackInformation attackInformation, in AttackCollisionData collisionData, StrikeType strikeType,
            float progressEffect, float impactPointAsPercent, float exraLinearSpeed, ref float __result, bool __runOriginal)
        {
            if (__runOriginal || _inner) return;   // gra liczyla sama (perki w wyniku) albo nasze wewnetrzne wolanie RBM
            var s = Settings.Current;
            if (s == null || !s.PerkStrikeAfterRbm) return;
            try
            {
                var ch = attackInformation.AttackerAgentCharacter as CharacterObject;
                if (ch == null || !(__result > 0f)) return;
                MissionWeapon w = attackInformation.AttackerWeapon;
                var usage = w.CurrentUsageItem;
                var item = w.Item;
                if (usage == null || item == null) return;
                // gra wola te metode drugi raz z predkoscia 0 (gdy progressEffect > 0.7) - tylko do napisu premii; w logu liczymy cios raz
                bool second = _expectSecond && exraLinearSpeed == 0f && progressEffect == _lastProgress && impactPointAsPercent == _lastImpact;
                _expectSecond = !second && progressEffect > 0.7f;
                _lastProgress = progressEffect; _lastImpact = impactPointAsPercent;
                if (!second) _dMelee++;
                float result = __result;

                // predkosc z perkami - warunki 1:1 z SandboxStrikeMagnitudeModel (val2 != null && extraLinearSpeed > 0)
                if (exraLinearSpeed > 0f)
                {
                    float speed = SpeedWithPerks(exraLinearSpeed, ch, attackInformation.AttackerCaptainCharacter as CharacterObject,
                        attackInformation.DoesAttackerHaveMountAgent, usage.RelevantSkill);
                    if (speed > exraLinearSpeed + 1e-4f)
                    {
                        // stosunek RBM(predkosc z perkami) / RBM(predkosc) skaluje __result - zmiany obcych postfiksow (wyzszy priorytet) zostaja
                        float up, bas;
                        _inner = true;
                        try
                        {
                            up = MissionCombatMechanicsHelper.CalculateBaseMeleeBlowMagnitude(in attackInformation, in collisionData, strikeType, progressEffect, impactPointAsPercent, speed);
                            bas = MissionCombatMechanicsHelper.CalculateBaseMeleeBlowMagnitude(in attackInformation, in collisionData, strikeType, progressEffect, impactPointAsPercent, exraLinearSpeed);
                        }
                        finally { _inner = false; }
                        if (bas > 0f && up > bas)
                        {
                            float r = up / bas;
                            _dMeleeUp++; _dMeleeRatio += r;
                            result *= r;
                        }
                    }
                }

                // bron wykuta przez gracza - jak w grze, po predkosci
                if (item.IsCraftedByPlayer)
                {
                    var perk = strikeType == StrikeType.Thrust ? DefaultPerks.Crafting.SharpenedTip : DefaultPerks.Crafting.SharpenedEdge;
                    var en = new ExplainedNumber(result, false, null);
                    PerkHelper.AddPerkBonusForCharacter(perk, ch, true, ref en, false);
                    if (en.ResultNumber > result + 1e-5f) { if (!second) _dHit[strikeType == StrikeType.Thrust ? ITip : IEdge]++; result = en.ResultNumber; }
                }
                __result = result;
            }
            catch (Exception e) { Stumble("MeleePostfix", e); }
        }

        /// <summary>Predkosc ruchu z perkami - te same wywolania co SandboxStrikeMagnitudeModel (bez NomadicTraditions - punkt 28).</summary>
        private static float SpeedWithPerks(float speed, CharacterObject ch, CharacterObject captain, bool mounted, SkillObject skill)
        {
            var en = new ExplainedNumber(speed, false, null);
            if (!mounted)
            {
                if (skill == DefaultSkills.TwoHanded) Track(ref en, IReckless, DefaultPerks.TwoHanded.RecklessCharge, ch, null);
                Track(ref en, IDash, DefaultPerks.Roguery.DashAndSlash, ch, null);
                Track(ref en, ISurge, DefaultPerks.Athletics.SurgingBlow, ch, null);
                Track(ref en, ISurgeC, DefaultPerks.Athletics.SurgingBlow, null, captain);
            }
            if (skill == DefaultSkills.Polearm)
            {
                Track(ref en, ILancerC, DefaultPerks.Polearm.Lancer, null, captain);
                if (mounted)
                {
                    Track(ref en, ILancer, DefaultPerks.Polearm.Lancer, ch, null);
                    Track(ref en, IUnstopC, DefaultPerks.Polearm.UnstoppableForce, null, captain);
                }
            }
            return en.ResultNumber;
        }

        private static void Track(ref ExplainedNumber en, int idx, PerkObject perk, CharacterObject personal, CharacterObject captain)
        {
            if (perk == null) return;
            float before = en.ResultNumber;
            if (personal != null) PerkHelper.AddPerkBonusForCharacter(perk, personal, true, ref en, false);
            else if (captain != null) PerkHelper.AddPerkBonusFromCaptain(perk, captain, ref en);
            else return;
            if (en.ResultNumber > before + 1e-6f) _dHit[idx]++;
        }

        // ============================================================ B) pocisk

        /// <summary>Postfiks MissionCombatMechanicsHelper.ComputeBlowMagnitudeMissile - RunningThrow po RBM.</summary>
        public static void MissilePostfix(in AttackInformation attackInformation, in AttackCollisionData collisionData, float momentumRemaining,
            in Vec2 victimVelocity, ref float baseMagnitude, ref float specialMagnitude, bool __runOriginal)
        {
            if (__runOriginal || _rbmMissile == null) return;
            var s = Settings.Current;
            if (s == null || !s.PerkStrikeAfterRbm || !(baseMagnitude > 0f)) return;
            try
            {
                var ch = attackInformation.AttackerAgentCharacter as CharacterObject;
                if (ch == null || !ch.IsHero) return;
                MissionWeapon w = attackInformation.AttackerWeapon;
                var usage = w.CurrentUsageItem;
                if (usage == null) return;
                var ac = usage.AmmoClass;   // jak w grze: 18, 19, 21, 22, 23
                if (ac != WeaponClass.Sling && ac != WeaponClass.Stone && ac != WeaponClass.ThrowingAxe && ac != WeaponClass.ThrowingKnife && ac != WeaponClass.Javelin) return;
                Vec3 mv = collisionData.MissileVelocity;
                float speed = attackInformation.IsVictimAgentNull ? mv.Length : (victimVelocity.ToVec3() - mv).Length;
                float extra = speed - collisionData.MissileStartingBaseSpeed;
                if (!(extra > 0f)) return;
                var en = new ExplainedNumber(0f, false, null);
                PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Throwing.RunningThrow, ch, true, ref en, false);
                float f = en.ResultNumber;
                if (!(f > 0f)) return;
                MissionWeapon ammo = w.AmmoWeapon;
                var item = ammo.Item ?? w.Item;
                if (item == null) return;
                var dt = (DamageTypes)collisionData.DamageType;
                float a = _rbmMissile(usage.WeaponClass, item.Weight, speed, collisionData.MissileTotalDamage, momentumRemaining, dt);
                float b = _rbmMissile(usage.WeaponClass, item.Weight, speed + extra * f, collisionData.MissileTotalDamage, momentumRemaining, dt);
                if (!(a > 0f) || !(b > a)) return;
                float r = b / a;
                baseMagnitude *= r;
                specialMagnitude *= r;
                _dHit[IRun]++; _dMissileUp++; _dMissileRatio += r;
            }
            catch (Exception e) { Stumble("MissilePostfix", e); }
        }

        // ============================================================ C) style BK - obrazenia

        /// <summary>Postfiks MissionCombatMechanicsHelper.ComputeBlowMagnitude - mnozniki perkow stylow BK (wszystkie rodzaje ciosu).</summary>
        public static void BlowPostfix(in AttackCollisionData acd, in AttackInformation attackInformation, ref float baseMagnitude, ref float specialMagnitude)
        {
            var s = Settings.Current;
            if (s == null || !s.PerkLifestyleDamage || !(baseMagnitude > 0f)) return;
            if (!BkResolve() || acd.IsFallDamage) return;
            try
            {
                float f = 1f;
                // atakujacy: wrecz (bez pocisku, szarzy konia, kopniecia/uderzenia tarcza i pustej reki)
                if (!acd.IsMissile && !acd.IsHorseCharge && !acd.IsAlternativeAttack && attackInformation.IsAttackerAgentHuman && !attackInformation.IsAttackerAgentMount)
                {
                    var att = attackInformation.AttackerAgent;
                    MissionWeapon w = attackInformation.AttackerWeapon;
                    var usage = w.IsEmpty ? null : w.CurrentUsageItem;
                    if (att != null && usage != null)
                    {
                        bool mounted = attackInformation.DoesAttackerHaveMountAgent;
                        Hero self = HeroOf(att);
                        Agent capA = CaptainOf(att);
                        Hero cap = HeroOf(capA);
                        int mSelf = Mask(self), mCap = Mask(cap);
                        int both = mSelf | mCap;
                        if (mounted && acd.StrikeType == (int)StrikeType.Thrust && (both & BKlib) != 0) { f *= 1.06f; _dHit[IKlibT]++; }
                        if (!mounted && usage.WeaponClass == WeaponClass.TwoHandedSword && (both & BHigh) != 0) { f *= 1.04f; _dHit[IHigh]++; }
                        if (usage.RelevantSkill == DefaultSkills.TwoHanded && (mSelf & BFennid) != 0) { f *= 1.10f; _dHit[IFennid]++; }
                        if (!mounted && capA != null && capA != att && QueryLibrary.IsInfantry(att) && (mCap & BDrengr) != 0) { f *= 1.10f; _dHit[IDrengr]++; }
                    }
                }
                // ofiara: czlowiek w siodle, trafienie w cialo (nie w tarcze)
                if (attackInformation.IsVictimAgentHuman && attackInformation.DoesVictimHaveMountAgent && !acd.AttackBlockedWithShield && !acd.CollidedWithShieldOnBack)
                {
                    var vic = attackInformation.VictimAgent;
                    if (vic != null && ((Mask(HeroOf(vic)) | Mask(HeroOf(CaptainOf(vic)))) & BKlib) != 0) { f *= 0.95f; _dHit[IKlibD]++; }
                }
                if (f != 1f)
                {
                    baseMagnitude *= f;
                    specialMagnitude = MBMath.ClampFloat(specialMagnitude * f, 0f, 500f);   // jak w grze na koncu ComputeBlowMagnitude
                }
            }
            catch (Exception e) { Stumble("BlowPostfix", e); }
        }

        // ============================================================ D) kon

        /// <summary>Postfiks SandboxAgentStatCalculateModel.UpdateHorseStats (po postfiksie RBM): SweepingWind i perki konne BK na koniu.</summary>
        public static void HorsePostfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            var s = Settings.Current;
            if (s == null || !s.PerkMountAfterRbm || agent == null || agentDrivenProperties == null) return;
            try
            {
                var rider = agent.RiderAgent;
                if (rider == null) return;
                MissionChanged();
                bool any = false;
                var ch = rider.Character as CharacterObject;
                bool rbm = RbmHorse();
                float spd0 = agentDrivenProperties.MountSpeed;   // po RBM (z jego sufitem)
                if (ch != null && ch.IsHero && rbm)
                {
                    var en = new ExplainedNumber(1f, false, null);
                    PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Riding.SweepingWind, ch, true, ref en, false);
                    if (en.ResultNumber > 1.0001f) { agentDrivenProperties.MountSpeed *= en.ResultNumber; _dHit[ISweep]++; any = true; }
                }
                if (!_bkOnHorse && BkResolve())
                {
                    int m = Mask(HeroOf(rider)) | Mask(HeroOf(CaptainOf(rider)));
                    if ((m & BEquites) != 0) { agentDrivenProperties.MountChargeDamage *= 1.1f; _dHit[IEquites]++; any = true; }
                    if ((m & BAdapt) != 0) { agentDrivenProperties.MountManeuver *= 1.08f; _dHit[IAdapt]++; any = true; }
                    if ((m & BCamel) != 0) { agentDrivenProperties.MountSpeed *= 1.08f; _dHit[ICamel]++; any = true; }
                    if ((m & BOutrider) != 0) { agentDrivenProperties.MountSpeed *= 1.05f; _dHit[IOutrider]++; any = true; }
                }
                // RBM sciaga predkosc ponad sufit (10 m/s, 8 przy koniu ponad 500 kg; HorseChanges) - premia nie przebija sufitu, kon juz ponad nim zostaje jak u RBM
                if (rbm && agentDrivenProperties.MountSpeed > spd0)
                {
                    var hw = agent.SpawnEquipment != null ? agent.SpawnEquipment[EquipmentIndex.Horse] : EquipmentElement.Invalid;
                    float cap = !hw.IsEmpty && hw.Weight > 500f ? 8f : 10f;
                    if (agentDrivenProperties.MountSpeed > cap) agentDrivenProperties.MountSpeed = Math.Max(spd0, cap);
                }
                if (any) _dHorse++;
            }
            catch (Exception e) { Stumble("HorsePostfix", e); }
        }

        private static bool RbmHorse()
        {
            if (_rbmHorse < 0) _rbmHorse = RbmPostfixOn(_horseTarget) ? 1 : 0;   // raz na misje (RBM moze sie przepiac miedzy bitwami)
            return _rbmHorse == 1;
        }

        // ============================================================ pomocnicze

        private static Hero HeroOf(Agent a)
        {
            var c = a != null ? a.Character as CharacterObject : null;
            return c != null ? c.HeroObject : null;
        }

        private static Agent CaptainOf(Agent a)
        {
            var f = a != null ? a.Formation : null;
            return f != null ? f.Captain : null;
        }

        private static void MissionChanged()
        {
            var m = Mission.Current;
            if (ReferenceEquals(m, _mission.Target)) return;
            lock (_lock) { _mission = new WeakReference(m); _mask.Clear(); _rbmHorse = -1; }
        }

        /// <summary>Perki BK bohatera (bity), raz na misje na bohatera.</summary>
        private static int Mask(Hero h)
        {
            if (h == null || !_bkOk) return 0;
            MissionChanged();
            int v;
            lock (_lock) { if (_mask.TryGetValue(h, out v)) return v; }
            v = 0;
            try
            {
                object cfg = _cfgInstance.GetValue(null, null);
                object mgr = cfg != null ? _eduManager.GetValue(cfg, null) : null;
                object edu = mgr != null ? _getEducation.Invoke(mgr, new object[] { h }) : null;
                if (edu != null)
                    for (int i = 0; i < _bkPerks.Length; i++)
                        if (_bkPerks[i] != null && (bool)_hasPerk.Invoke(edu, new[] { _bkPerks[i] })) v |= 1 << i;
            }
            catch (Exception e) { Stumble("Mask", e); }
            lock (_lock) { _mask[h] = v; }
            return v;
        }

        /// <summary>Czy perki BK sa gotowe w tej sesji (odswiezane w EnsureHooks).</summary>
        private static bool BkResolve() { return _bkOk; }

        /// <summary>Typy i odbicie BK - raz na proces.</summary>
        private static bool BkTypes()
        {
            if (_bkTypesTried) return _bkTypesOk;
            _bkTypesTried = true;
            var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
            if (cfgT == null) return false;
            _bkPerksT = AccessTools.TypeByName("BannerKings.Managers.Skills.BKPerks");
            var eduMgrT = AccessTools.TypeByName("BannerKings.Managers.EducationManager");
            var eduT = AccessTools.TypeByName("BannerKings.Managers.Education.EducationData");
            _cfgInstance = AccessTools.Property(cfgT, "Instance");
            _eduManager = AccessTools.Property(cfgT, "EducationManager");
            _getEducation = eduMgrT != null ? AccessTools.Method(eduMgrT, "GetHeroEducation", new[] { typeof(Hero) }) : null;
            _hasPerk = eduT != null ? AccessTools.Method(eduT, "HasPerk") : null;
            _bkTypesOk = _cfgInstance != null && _eduManager != null && _getEducation != null && _hasPerk != null && _bkPerksT != null;
            return _bkTypesOk;
        }

        /// <summary>Obiekty perkow BK - co sesje (BK robi nowe PerkObject przy kazdym starcie gry; stare po wczytaniu innego zapisu nie pasuja).</summary>
        private static void BkRefresh()
        {
            _bkOk = false;
            if (!BkTypes())
            {
                _bkState = AccessTools.TypeByName("BannerKings.BannerKingsConfig") == null ? "BRAK (BannerKings nie zaladowany)"
                    : "BRAK typow (cfg " + (_cfgInstance != null) + ", edukacja " + (_getEducation != null) + ", HasPerk " + (_hasPerk != null) + ", BKPerks " + (_bkPerksT != null) + ")";
                return;
            }
            object perks = StaticInstance(_bkPerksT);
            var arr = new object[BkProps.Length];
            var found = new List<string>();
            for (int i = 0; i < BkProps.Length; i++)
            {
                try { arr[i] = perks != null ? Traverse.Create(perks).Property(BkProps[i]).GetValue() : null; } catch { arr[i] = null; }
                found.Add(BkProps[i] + (arr[i] != null ? "" : " BRAK"));
            }
            _bkPerks = arr;
            _bkOk = perks != null;
            _bkState = (_bkOk ? "TAK (obiekty perkow odswiezone w tej sesji)" : "BRAK instancji BKPerks") + " [" + string.Join(", ", found) + "]";
        }

        private static object StaticInstance(Type t)
        {
            // DefaultTypeInitializer<T, X>.Instance - statyczna wlasciwosc w generycznej klasie bazowej (wzor: CrashScribe BkBattlePerks)
            for (var b = t; b != null; b = b.BaseType)
            {
                var p = b.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (p != null) return p.GetValue(null, null);
            }
            return null;
        }

        // ============================================================ doba

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (gdy byly ciosy / konie z premia albo potkniecia TEJ doby) i zerowanie.</summary>
        internal static void Daily(int day)
        {
            try
            {
                int hits = 0;
                for (int i = 0; i < _dHit.Length; i++) hits += _dHit[i];
                if (hits > 0 || _dStumbles > 0)
                {
                    var parts = new List<string>();
                    for (int i = 0; i < _dHit.Length; i++) if (_dHit[i] > 0) parts.Add(Names[i] + " " + _dHit[i]);
                    Log.Info(P + "dzien " + day + " | wolan ciosu wrecz po RBM " + _dMelee + ", z premia predkosci " + _dMeleeUp
                             + (_dMeleeUp > 0 ? " (sila x" + F2(_dMeleeRatio / _dMeleeUp) + " srednio)" : "")
                             + " | pociski z RunningThrow " + _dMissileUp + (_dMissileUp > 0 ? " (x" + F2(_dMissileRatio / _dMissileUp) + ")" : "")
                             + " | przeliczen koni z premia " + _dHorse
                             + " | per perk (trafienia/przeliczenia): " + (parts.Count > 0 ? string.Join(", ", parts) : "-")
                             + " | potkniecia doby " + _dStumbles + " (razem " + _stumbles + ").");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
