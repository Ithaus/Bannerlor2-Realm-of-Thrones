using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PORZADEK W SZEREGU (Jeff 29.08: "recznie musze ustawiac jednostki,
    /// chcialbym zeby segregowaly sie same: kawaleria, konni lucznicy,
    /// piechota, strzelcy - po tierze"). Rebuild rosteru przy kazdym otwarciu
    /// ekranu partii; XP stacka jedzie razem z nim, herosi na gorze jak zawsze.
    ///
    /// COFNIETE 29.08 (Jeff: "MA NIE ODDAWAC - zwalniany zolnierz odchodzi
    /// ZE SWOIM ekwipunkiem, tak ma byc!"): wczesniejsza mechanika oddawania
    /// rynsztunku do magazynu przy zwolnieniu wylecialaa w calosci - vanilla
    /// zachowanie przywrocone, magazyn kwatermistrza nietkniety.
    /// </summary>
    internal static class MusterOut
    {
        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                if (!Settings.Current.AutoSortParty) { Log.Info("MusterOut: auto-sort wylaczony."); return; }
                var mInit = AccessTools.Method(typeof(PartyScreenLogic), "Initialize");
                if (mInit == null) { Log.Info("MusterOut: nie znalazlem PartyScreenLogic - patch spi."); return; }
                harmony.Patch(mInit, postfix: new HarmonyMethod(typeof(MusterOut), "AfterInit"));
                Log.Info("MusterOut: auto-sort partii aktywny (kawaleria, konni lucznicy, piechota, strzelcy; tier malejaco).");
            }
            catch (Exception e) { Log.Error("MusterOut.ApplyAll", e); }
        }

        public static void AfterInit() { try { AutoSort(); } catch { } }

        private static int ArmRank(CharacterObject ch)
        {
            try
            {
                switch (ch.DefaultFormationClass)
                {
                    case FormationClass.Cavalry: return 0;
                    case FormationClass.HorseArcher: return 1;
                    case FormationClass.Infantry: return 2;
                    case FormationClass.Ranged: return 3;
                    default: return 4;
                }
            }
            catch { return 4; }
        }

        internal static void AutoSort()
        {
            try
            {
                if (!Settings.Current.AutoSortParty) return;
                var main = MobileParty.MainParty;
                var roster = main != null ? main.MemberRoster : null;
                if (roster == null) return;
                // AUDYT 29.08: w trakcie potyczki kolejnosc rosteru to WYBOR
                // SKLADU do bitwy (BattleMuster) - sortowanie by go skasowalo
                if (main.MapEvent != null) return;

                var stacks = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    stacks.Add(el);
                }
                if (stacks.Count < 2) return;

                var sorted = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>(stacks);
                sorted.Sort((a, b) =>
                {
                    int r = ArmRank(a.Character).CompareTo(ArmRank(b.Character));
                    if (r != 0) return r;
                    r = b.Character.Tier.CompareTo(a.Character.Tier);
                    if (r != 0) return r;
                    return string.CompareOrdinal(a.Character.StringId, b.Character.StringId);
                });

                bool same = true;
                for (int i = 0; i < stacks.Count; i++)
                    if (!ReferenceEquals(stacks[i].Character, sorted[i].Character)) { same = false; break; }
                if (same) return;

                foreach (var el in stacks)
                    roster.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);
                foreach (var el in sorted)
                    roster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber, el.Xp);
            }
            catch (Exception e) { Log.Error("MusterOut.AutoSort", e); }
        }
    }
}
