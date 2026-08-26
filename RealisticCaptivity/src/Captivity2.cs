using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace RealisticCaptivity
{
    /// <summary>Druga polowa moda: glod, sprzedaz jenca, towarzysze, dlug, pomoc z zewnatrz, zniewaga.</summary>
    public partial class CaptivityExtras
    {
        // ---------------------------------------------------------------- glod w lochu
        internal static void DailyStarvation(bool lowborn, bool onParole)
        {
            try
            {
                var s = Settings.Current;
                if (!s.StarvationEnabled || !PlayerCaptivity.IsCaptive) return;
                float loss = s.StarvationHealthPerDay;
                if (lowborn) loss *= s.StarvationLowbornFactor;
                if (onParole) loss *= s.StarvationParoleFactor;
                int dmg = Math.Max(1, (int)loss);
                int before = Hero.MainHero.HitPoints;
                Hero.MainHero.HitPoints = Math.Max(1, before - dmg);
                if (Hero.MainHero.HitPoints <= Hero.MainHero.MaxHitPoints * 0.25f)
                    Log.Player("Hunger and filth are wearing you down. You are growing dangerously weak.", true);
                Log.Info("Glod: -" + dmg + " HP (" + before + " -> " + Hero.MainHero.HitPoints + ")");
                MuscleAtrophy(lowborn);
            }
            catch (Exception e) { Log.Error("DailyStarvation", e); }
        }


        /// <summary>
        /// Bandyci nie prowadza domu wykupu. Jesli po kilku dniach nikt nie placi, a jeniec
        /// nie ma nic przy duszy, przestaje byc wart miski strawy - rozcinaja wiezy i zostawiaja
        /// go na trakcie. Ograbionego, uponizonego, ale wolnego. Lordowie tak nie robia -
        /// dla nich jeniec to polityka, nie ladunek.
        /// </summary>
        internal static bool TryBanditDump(int daysCaptive)
        {
            try
            {
                var s = Settings.Current;
                if (!s.BanditDumpEnabled || !PlayerCaptivity.IsCaptive) return false;
                if (daysCaptive < s.BanditDumpAfterDays) return false;

                var captorParty = PlayerCaptivity.CaptorParty;
                if (captorParty == null) return false;
                // tylko bandy bez lorda - u lorda w lochu te zasady nie graja
                if (captorParty.LeaderHero != null) return false;

                if (Hero.MainHero.Gold >= s.BanditDumpWorthlessGold) return false;

                float chance = s.BanditDumpChancePercentPerDay / 100f;
                bool dying = Hero.MainHero.HitPoints <= Hero.MainHero.MaxHitPoints * 0.2f;
                if (dying) chance = MathF.Min(0.95f, chance * s.BanditDumpDyingFactor);
                if (MBRandom.RandomFloat >= chance) return false;

                Log.Info("Bandyci wyrzucaja jenca (dzien " + daysCaptive + ", zloto " + Hero.MainHero.Gold +
                         (dying ? ", ledwo zywy" : "") + ")");
                Log.Player(dying
                    ? "Too weak to walk, not worth a crust. They cut your bonds and leave you by the road to live or die."
                    : "No ransom came and your purse is empty. They take one last look, cut your bonds and ride on. A beggar is not worth feeding.",
                    true);
                EndCaptivityAction.ApplyByEscape(Hero.MainHero);
                return true;
            }
            catch (Exception e) { Log.Error("TryBanditDump", e); return false; }
        }

        /// <summary>Miesnie siadaja: losowa strata punktu w umiejetnosci fizycznej.</summary>
        private static readonly string[] AtrophyMessages =
        {
            "Weeks on gruel have wasted your muscle.",
            "Your arms have gone soft in the irons.",
            "Chained and idle, your body forgets its trade.",
            "You are thinner than you were. Weaker, too."
        };

        internal static void MuscleAtrophy(bool lowborn)
        {
            try
            {
                var s = Settings.Current;
                if (s.AtrophyChancePercentPerDay <= 0) return;
                int chance = s.AtrophyChancePercentPerDay;
                if (lowborn) chance = Math.Min(100, (int)(chance * 1.5f));
                if (MBRandom.RandomInt(100) >= chance) return;

                // tylko cielesne umiejetnosci - wzrok i rozum w lochu nie slabna
                var physical = new List<SkillObject>
                {
                    DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm,
                    DefaultSkills.Bow, DefaultSkills.Crossbow, DefaultSkills.Throwing,
                    DefaultSkills.Riding, DefaultSkills.Athletics
                };

                var candidates = new List<SkillObject>();
                foreach (var sk in physical)
                {
                    if (sk == null) continue;
                    if (Hero.MainHero.GetSkillValue(sk) > s.AtrophyMinSkillValue) candidates.Add(sk);
                }
                if (candidates.Count == 0) { Log.Info("Atrofia: brak umiejetnosci powyzej progu."); return; }

                var skill = candidates[MBRandom.RandomInt(candidates.Count)];
                int cur = Hero.MainHero.GetSkillValue(skill);
                int target = Math.Max(s.AtrophyMinSkillValue, cur - Math.Max(1, s.AtrophyPointsPerHit));
                if (target >= cur) return;

                Hero.MainHero.HeroDeveloper.SetInitialSkillLevel(skill, target);

                var flavour = AtrophyMessages[MBRandom.RandomInt(AtrophyMessages.Length)];
                Log.Player(flavour + " (" + skill.Name + " " + cur + " -> " + target + ")", true);
                Log.Info("Atrofia: " + skill.StringId + " " + cur + " -> " + target);
            }
            catch (Exception e) { Log.Error("MuscleAtrophy", e); }
        }

        // ---------------------------------------------------------------- sprzedanie jenca
        internal static Hero TrySellPrisoner()
        {
            try
            {
                var s = Settings.Current;
                if (!s.SellPrisonerEnabled || !PlayerCaptivity.IsCaptive) return null;
                if (PlayerCaptivity.CaptiveTimeInDays < s.SellMinDays) return null;
                if (MBRandom.RandomInt(100) >= s.SellChancePercentPerDay) return null;

                var current = PlayerCaptivity.CaptorParty;
                if (current == null) return null;
                var currentFaction = current.MapFaction;

                // kandydat: inna druzyna lorda tej samej frakcji, albo dowolne miasto tej frakcji
                var candidates = new List<PartyBase>();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp.Party == current) continue;
                    if (mp.LeaderHero == null || !mp.LeaderHero.IsAlive) continue;
                    if (mp.MapFaction != currentFaction) continue;
                    if (mp.IsMainParty) continue;
                    candidates.Add(mp.Party);
                }
                if (candidates.Count == 0)
                {
                    foreach (var st in Settlement.All)
                        if (st != null && (st.IsTown || st.IsCastle) && st.MapFaction == currentFaction)
                            candidates.Add(st.Party);
                }
                if (candidates.Count == 0) return null;

                var target = candidates[MBRandom.RandomInt(candidates.Count)];
                var oldName = (current.LeaderHero != null) ? current.LeaderHero.Name.ToString() : current.Name.ToString();
                var newName = (target.LeaderHero != null) ? target.LeaderHero.Name.ToString() : target.Name.ToString();

                PlayerCaptivity.CaptorParty = target;   // setter sam przenosi jenca miedzy rosterami

                Log.Info("Gracz sprzedany: " + oldName + " -> " + newName);
                Log.Player("You have been sold. " + oldName + " handed you over to " + newName + " for a purse of silver.", true);
                return target.LeaderHero;
            }
            catch (Exception e) { Log.Error("TrySellPrisoner", e); return null; }
        }

        // ---------------------------------------------------------------- pomoc z zewnatrz
        /// <summary>Czy ktokolwiek z twojego rodu jest na wolnosci i moze przekupic straznika.</summary>
        internal static Hero FindOutsideHelper()
        {
            try
            {
                var clan = Clan.PlayerClan;
                if (clan == null) return null;
                foreach (var h in clan.Heroes)
                {
                    if (h == null || h == Hero.MainHero) continue;
                    if (!h.IsAlive || h.IsPrisoner || h.IsChild) continue;
                    return h;
                }
                return null;
            }
            catch (Exception e) { Log.Error("FindOutsideHelper", e); return null; }
        }

        // ---------------------------------------------------------------- zniewaga
        internal static void ApplyHumiliation(EndCaptivityDetail detail)
        {
            try
            {
                var s = Settings.Current;
                if (!s.HumiliationEnabled) return;
                if (detail != EndCaptivityDetail.ReleasedByChoice && detail != EndCaptivityDetail.ReleasedAfterBattle) return;
                var clan = Clan.PlayerClan;
                if (clan == null) return;
                clan.Renown = Math.Max(0f, clan.Renown - s.HumiliationRenownLoss);
                Log.Info("Zniewaga: -" + s.HumiliationRenownLoss + " reputacji.");
                Log.Player("They let you go as one lets go of a stray dog - not worth feeding, not worth ransoming. (-"
                           + s.HumiliationRenownLoss + " renown)", true);
            }
            catch (Exception e) { Log.Error("ApplyHumiliation", e); }
        }
    }
}
