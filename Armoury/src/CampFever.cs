using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO ZARAZY OBOZOWEJ (Jeff 31.08, "rob"). Historycznie dyzenteria
    /// i tyfus zabijaly wiecej wojska niz zelazo - a u nas armia stala pod
    /// murami pol roku i nikt nie kichnal. Odtad kazde oblezenie ma zegar:
    ///  - inkubacja (dom. 9 dni) - swiezy oboz jest czysty;
    ///  - potem dzienne zachorowania: baza 0.6%/dzien, rosnaca +15% za kazdy
    ///    dzien ponad inkubacje, razy TLOK (sqrt(ludzi/500) - wielkie
    ///    doomstacki gnija najszybciej);
    ///  - chorzy ida W RANNYCH (gorczka klade, rzadko scina) - wracaja naszym
    ///    powolnym leczeniem; co dziesiaty chory umiera;
    ///  - OBRONCY lapia 40% tempa oblegajacych, ale GLOD (pusty spichlerz)
    ///    podwaja ich tempo - oblezenie to wyscig spichlerza z latrynami;
    ///  - MEDYCYNA to tarcza: skill chirurga partii -0.25%/pkt (cap -50%),
    ///    Preventive Medicine -15% zachorowan, Siege Medic smiertelnosc /2,
    ///    Pristine Streets (gubernator) -30% za murami i glod boli o polowe
    ///    mniej; reszta perkow leczenia dziala sama, bo chorzy sa rannymi;
    ///  - BOHATEROWIE nie choruja z ticka; UMARLI nie choruja wcale - horda
    ///    Nocnego Krola oblega bez zegara (lore: zaraza to bron na zywych).
    /// </summary>
    internal static class CampFever
    {
        // ostrzezenie "zaraza wstaje" - raz na oblezenie (klucz: osada)
        private static readonly HashSet<string> _warned = new HashSet<string>();

        internal static void OnDaily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SiegeSicknessEnabled) return;
                var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
                if (mgr == null || mgr.SiegeEvents == null) return;

                foreach (var siege in mgr.SiegeEvents)
                {
                    try { TickSiege(s, siege); }
                    catch (Exception e) { Log.Error("CampFever.TickSiege", e); }
                }
            }
            catch (Exception e) { Log.Error("CampFever.OnDaily", e); }
        }

        private static void TickSiege(Settings s, SiegeEvent siege)
        {
            if (siege == null || siege.BesiegedSettlement == null) return;
            var st = siege.BesiegedSettlement;
            int days = (int)siege.SiegeStartTime.ElapsedDaysUntilNow;
            int incub = Math.Max(1, s.SiegeSicknessIncubationDays);
            if (days < incub) { _warned.Remove(st.StringId); return; }

            float ramp = 1f + (days - incub) * Math.Max(0, s.SiegeSicknessRampPercent) / 100f;

            // ostrzezenie, gdy zaraza wstaje a gracz jest strona
            bool mainAttacks = MobileParty.MainParty != null && MobileParty.MainParty.BesiegerCamp == siege.BesiegerCamp;
            bool mainDefends = MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == st;
            if (!_warned.Contains(st.StringId))
            {
                _warned.Add(st.StringId);
                if (mainAttacks || mainDefends)
                    Log.Player("Camp fever stirs among the tents at " + st.Name + " - the longer the siege, the deeper it bites.", true);
                Log.Info("CampFever: oblezenie " + st.StringId + " dzien " + days + " - zaraza wstaje.");
            }

            int attSick = 0, attDead = 0, defSick = 0, defDead = 0;

            // --- OBLEGAJACY ---
            if (siege.BesiegerCamp != null)
            {
                foreach (var pb in siege.BesiegerCamp.GetInvolvedPartiesForEventType())
                {
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp == null) continue;
                    int sick, dead;
                    Strike(s, mp, ramp, 1f, out sick, out dead);
                    attSick += sick; attDead += dead;
                    Shout(mp, sick, dead, st, mainAttacks || mainDefends);
                }
            }

            // --- OBRONCY: garnizon + partie lordow w srodku ---
            var town = st.Town;
            bool hunger = town != null && town.FoodStocks <= 0f;
            var gov = town != null ? town.Governor : null;
            bool pristine = gov != null && gov.GetPerkValue(DefaultPerks.Medicine.PristineStreets);
            float defMul = Math.Max(0, s.SiegeSicknessDefenderFactor) / 100f;
            if (pristine) defMul *= 0.7f;
            if (hunger) defMul *= pristine ? 1.5f : 2f;

            var defenders = new List<MobileParty>();
            if (town != null && town.GarrisonParty != null) defenders.Add(town.GarrisonParty);
            foreach (var mp in st.Parties)
                if (mp != null && mp.IsLordParty && mp.CurrentSettlement == st) defenders.Add(mp);
            foreach (var mp in defenders)
            {
                int sick, dead;
                Strike(s, mp, ramp, defMul, out sick, out dead);
                defSick += sick; defDead += dead;
                Shout(mp, sick, dead, st, mainAttacks || mainDefends);
            }

            if (attSick + defSick + attDead + defDead > 0)
                Log.Info("CampFever: " + st.StringId + " dzien " + days + " - oblegajacy " + attSick
                         + " chorych/" + attDead + " zmarlych, obroncy " + defSick + "/" + defDead
                         + (hunger ? " (GLOD w miescie)" : "") + ".");
        }

        /// <summary>Dzienne zniwo w jednej partii. Chorzy ida w rannych, czesc umiera.</summary>
        private static void Strike(Settings s, MobileParty mp, float ramp, float sideMul, out int sick, out int dead)
        {
            sick = 0; dead = 0;
            if (mp == null || mp.MemberRoster == null || sideMul <= 0f) return;
            if (Undead.Party(mp)) return;                      // umarli nie choruja

            var roster = mp.MemberRoster;
            int healthy = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero) continue;
                healthy += roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
            }
            if (healthy <= 0) return;

            float crowd = MathF.Clamp(MathF.Sqrt(roster.TotalManCount / 500f), 0.6f, 2.5f);

            var surgeon = mp.EffectiveSurgeon;
            int med = surgeon != null ? surgeon.GetSkillValue(DefaultSkills.Medicine) : 0;
            float relief = MathF.Min(Math.Max(0, s.SiegeSicknessMedicineMax) / 100f, med * 0.0025f);
            bool prevent = surgeon != null && surgeon.GetPerkValue(DefaultPerks.Medicine.PreventiveMedicine);
            bool siegeMedic = surgeon != null && surgeon.GetPerkValue(DefaultPerks.Medicine.SiegeMedic);

            float risk = Math.Max(0f, s.SiegeSicknessBasePercent) / 100f
                       * ramp * crowd * sideMul * (1f - relief) * (prevent ? 0.85f : 1f);
            float expected = healthy * risk;
            sick = (int)expected;
            if (MBRandom.RandomFloat < expected - sick) sick++;
            if (sick <= 0) return;
            if (sick > healthy) sick = healthy;

            float deathShare = Math.Max(0, s.SiegeSicknessDeathShare) / 100f * (siegeMedic ? 0.5f : 1f);
            float dexp = sick * deathShare;
            dead = (int)dexp;
            if (MBRandom.RandomFloat < dexp - dead) dead++;
            int wounded = sick - dead;

            for (int n = 0; n < wounded; n++) HitRandom(roster, false);
            for (int n = 0; n < dead; n++) HitRandom(roster, true);
        }

        /// <summary>Losowy zdrowy szeregowy (wazony liczebnoscia) - ranny albo do grobu.</summary>
        private static void HitRandom(TroopRoster roster, bool kill)
        {
            try
            {
                int pool = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster.GetCharacterAtIndex(i);
                    if (ch == null || ch.IsHero) continue;
                    pool += roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
                }
                if (pool <= 0) return;
                int pick = MBRandom.RandomInt(pool);
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster.GetCharacterAtIndex(i);
                    if (ch == null || ch.IsHero) continue;
                    int h = roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
                    if (h <= 0) continue;
                    if (pick >= h) { pick -= h; continue; }
                    if (kill) roster.AddToCounts(ch, -1);
                    else roster.WoundTroop(ch);
                    return;
                }
            }
            catch { }
        }

        private static void Shout(MobileParty mp, int sick, int dead, Settlement st, bool playerInvolved)
        {
            try
            {
                if (sick <= 0 || mp != MobileParty.MainParty) return;
                string line = "Camp fever: " + sick + " of your men down with the flux"
                              + (dead > 0 ? ", " + dead + " dead" : "") + ".";
                Log.Player(line, dead > 0);
            }
            catch { }
        }
    }
}
