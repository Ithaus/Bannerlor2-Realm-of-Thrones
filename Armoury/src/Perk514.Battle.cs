using System;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 514 - czesc bitewna (klucz BkPerkHalvesBattle; konie Ghazw - klucz BkPerkHalvesLifestyle). Latki wpinane raz na proces w SubModuleMain (obok
    /// FriendlyFire - CalculateDamage jest wolane niewirtualnie z Mission, wiec zanim JIT skompiluje wolajacych):
    ///  (1) postfiks AgentApplyDamageModel.CalculateDamage (klasa bazowa, DeclaredMethod) - mnozniki obrazen z opisow perkow:
    ///      82 JomsvikingShieldwall: ty i piechota twojego szyku +8% obrazen wrecz, -5% obrazen wrecz otrzymanych;
    ///      82 SjofarandiCoastalHunter: ty i lucznicy twojego szyku +6% obrazen z luku;
    ///      86 FianRanger (Polnoc): +8% twoich obrazen z luku (BK nie ma tej polowy; slad partii to osobna poprawka);
    ///      90 VaryagShieldBrother: piechota partii wodza z perkiem -4% obrazen wrecz w szyku sciany tarcz;
    ///      97 JomsvikingBoardingFury: +12% twoich obrazen wrecz ponizej polowy zdrowia (BK daje tylko 1.06 w bitwie morskiej - zostaje).
    ///  (2) postfiks SandboxAgentStatCalculateModel.UpdateAgentStats (Priority.Last - po RBM): CoastalHunter - celnosc luku +4% (WeaponInaccuracy x0.96).
    ///  (3) postfiks SandboxAgentStatCalculateModel.GetEffectiveMaxHealth (Priority.Low - po BK): 89 JawwalGhazw - BK daje koniom 10%, opis 12%:
    ///      doplata x1.12/1.10 (bez postfiksu BK - pelne x1.12). Licznik koni z jezdzcem / bez jezdzca w chwili liczenia HP (audyt: niepewne, czy BK
    ///      w ogole widzi jezdzca - linia doby rozstrzyga). RitterIronHorses (10% = opis) bez zmian.
    /// "Twoj szyk" = kapitan szyku (Formation.Captain) z perkiem; "piechota" = bez konia i bez broni dystansowej; "lucznik" = z bronia dystansowa.
    /// Tylko kampania (Campaign.Current). Nic co klatke - tylko w trafieniu / przeliczeniu statystyk. Liczniki Interlocked, linia w dobie Perk514.Daily.
    /// </summary>
    internal static partial class Perk514
    {
        private static string _battleHooks = "nie wpiete";
        private static bool? _bkHealthPatched;
        private static System.Reflection.MethodBase _mHealth;

        private static int bShieldOut, bShieldIn, bCoastal, bRanger, bBro, bFury, bAim;
        private static long bHpBefore10, bHpAfter10;

        private static void ResetBattleDay() { bShieldOut = bShieldIn = bCoastal = bRanger = bBro = bFury = bAim = 0; bHpBefore10 = bHpAfter10 = 0; }

        private static string BattleHookState() { return _battleHooks; }

        private static string BattleDayText()
        {
            int any = bShieldOut + bShieldIn + bCoastal + bRanger + bBro + bFury + bAim;
            if (any == 0) return null;
            return "Shieldwall zadane / przyjete " + N(bShieldOut) + " / " + N(bShieldIn) + ", Coastal Hunter luk " + N(bCoastal) + " (celnosc " + N(bAim)
                   + "), Ranger luk " + N(bRanger) + ", Shield Brother sciana tarcz " + N(bBro) + ", Boarding Fury " + N(bFury)
                   + ", obrazenia przed / po " + (bHpBefore10 / 10.0).ToString("0.#", Inv) + " / " + (bHpAfter10 / 10.0).ToString("0.#", Inv);
        }

        /// <summary>SubModuleMain (menu glowne, przed JIT wolajacych): latki misji. Klucze czytane na zywo w latkach.</summary>
        internal static void ApplyMission(Harmony unused)
        {
            var h = new Harmony(Owner + ".battle");
            int ok = 0;
            var miss = new System.Collections.Generic.List<string>();
            try
            {
                var dmg = AccessTools.DeclaredMethod(typeof(TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel), "CalculateDamage");
                if (dmg == null) miss.Add("CalculateDamage (brak)");
                else { h.Patch(dmg, postfix: new HarmonyMethod(typeof(Perk514), nameof(DamagePost))); ok++; }
            }
            catch (Exception e) { miss.Add("CalculateDamage (wyjatek)"); Log.Error("Perk514.ApplyMission (obrazenia)", e); }
            try
            {
                var t = typeof(SandBox.GameComponents.SandboxAgentStatCalculateModel);
                var st = AccessTools.DeclaredMethod(t, "UpdateAgentStats", new[] { typeof(Agent), typeof(AgentDrivenProperties) });
                if (st == null) miss.Add("UpdateAgentStats (brak)");
                else { h.Patch(st, postfix: new HarmonyMethod(typeof(Perk514), nameof(StatsPost)) { priority = Priority.Last }); ok++; }
                _mHealth = AccessTools.DeclaredMethod(t, "GetEffectiveMaxHealth", new[] { typeof(Agent) });
                if (_mHealth == null) miss.Add("GetEffectiveMaxHealth (brak)");
                else { h.Patch(_mHealth, postfix: new HarmonyMethod(typeof(Perk514), nameof(HealthPost)) { priority = Priority.Low }); ok++; }
            }
            catch (Exception e) { miss.Add("statystyki (wyjatek)"); Log.Error("Perk514.ApplyMission (statystyki)", e); }
            _battleHooks = ok + "/3" + (miss.Count > 0 ? " (NIE: " + string.Join(", ", miss) + ")" : "");
        }

        private static Hero HeroOf(Agent a)
        {
            var c = a != null ? a.Character as CharacterObject : null;
            return c != null ? c.HeroObject : null;
        }

        private static Hero CaptainHero(Agent a)
        {
            var f = a != null ? a.Formation : null;
            return f != null ? HeroOf(f.Captain) : null;
        }

        private static Hero LeaderOf(Agent a)
        {
            var o = a != null ? a.Origin : null;
            var pb = o != null ? o.BattleCombatant as PartyBase : null;
            return pb != null && pb.MobileParty != null ? pb.MobileParty.LeaderHero : null;
        }

        private static bool Infantry(Agent a) { return a != null && a.IsHuman && !a.HasMount && !a.IsRangedCached; }

        /// <summary>Postfiks AgentApplyDamageModel.CalculateDamage: mnozniki obrazen z perkow stylow (opis w naglowku pliku).</summary>
        public static void DamagePost(in AttackInformation attackInformation, in AttackCollisionData collisionData, ref float __result)
        {
            if (!(__result > 0f) || !BattleOn || Campaign.Current == null) return;
            try
            {
                var a = attackInformation.AttackerAgent;
                var v = attackInformation.VictimAgent;
                if (a == null || v == null || ReferenceEquals(a, v) || !a.IsHuman || collisionData.IsFallDamage || collisionData.IsHorseCharge) return;
                float mul = 1f;
                Hero ah = HeroOf(a), cap = CaptainHero(a);
                if (!collisionData.IsMissile)
                {
                    // premie ZADANE tylko z broni (bez kopniec / uderzen tarcza - jak 507 Perk507Strike); oslony przyjete - kazdy cios wrecz
                    bool weaponHit = !collisionData.IsAlternativeAttack;
                    if (weaponHit && (Has(ah, BShieldwall) || (Infantry(a) && Has(cap, BShieldwall)))) { mul *= 1.08f; Interlocked.Increment(ref bShieldOut); }
                    if (weaponHit && ah != null && a.HealthLimit > 0f && a.Health < a.HealthLimit * 0.5f && Has(ah, BFury)) { mul *= 1.12f; Interlocked.Increment(ref bFury); }
                    if (v.IsHuman)
                    {
                        if (Has(HeroOf(v), BShieldwall) || (Infantry(v) && Has(CaptainHero(v), BShieldwall))) { mul *= 0.95f; Interlocked.Increment(ref bShieldIn); }
                        var vf = v.Formation;
                        if (vf != null && Infantry(v) && vf.ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.ShieldWall && Has(LeaderOf(v), BShieldBro))
                        { mul *= 0.96f; Interlocked.Increment(ref bBro); }
                    }
                }
                else
                {
                    var wc = attackInformation.AttackerWeapon.CurrentUsageItem;
                    if (wc != null && (wc.WeaponClass == WeaponClass.Arrow || wc.WeaponClass == WeaponClass.Bow))
                    {
                        if (Has(ah, BCoastal) || (a.IsRangedCached && Has(cap, BCoastal))) { mul *= 1.06f; Interlocked.Increment(ref bCoastal); }
                        if (Has(ah, BRanger)) { mul *= 1.08f; Interlocked.Increment(ref bRanger); }
                    }
                }
                if (mul == 1f) return;
                float before = __result;
                __result = before * mul;
                Interlocked.Add(ref bHpBefore10, (long)Math.Round(before * 10f));
                Interlocked.Add(ref bHpAfter10, (long)Math.Round(__result * 10f));
            }
            catch (Exception e) { Stumble("DamagePost", e); }
        }

        /// <summary>Postfiks SandboxAgentStatCalculateModel.UpdateAgentStats (po RBM): CoastalHunter - luk w reku, celnosc +4%.</summary>
        public static void StatsPost(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            if (agent == null || agentDrivenProperties == null || !agent.IsHuman || !BattleOn || Campaign.Current == null) return;
            try
            {
                var w = agent.WieldedWeapon;
                if (w.IsEmpty || w.CurrentUsageItem == null || w.CurrentUsageItem.WeaponClass != WeaponClass.Bow) return;
                if (!Has(HeroOf(agent), BCoastal) && !Has(CaptainHero(agent), BCoastal)) return;
                agentDrivenProperties.WeaponInaccuracy *= 0.96f;
                Interlocked.Increment(ref bAim);
            }
            catch (Exception e) { Stumble("StatsPost", e); }
        }

        /// <summary>Postfiks SandboxAgentStatCalculateModel.GetEffectiveMaxHealth (po BK): Ghazw - konie 12% (BK 10%).</summary>
        public static void HealthPost(Agent agent, ref float __result)
        {
            if (agent == null || agent.IsHuman || !LifeOn || Campaign.Current == null) return;
            try
            {
                var r = agent.RiderAgent;
                if (r == null) { Interlocked.Increment(ref dHorseNoRider); return; }
                Interlocked.Increment(ref dHorseRider);
                if (!Has(LeaderOf(r), BGhazw)) return;
                if (_bkHealthPatched == null) _bkHealthPatched = BkPostfixOn(_mHealth);
                __result *= _bkHealthPatched == true ? 1.12f / 1.1f : 1.12f;
                Interlocked.Increment(ref dGhazw);
            }
            catch (Exception e) { Stumble("HealthPost", e); }
        }
    }
}
