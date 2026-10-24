using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Test n36 (24.10, autotest 113a-bitwa): w 4 liniach "Bitwa (m113-walka2): misja" bohaterowie z profilem DuelAi 0 i bohaterowie regula weteranow 0 -
    /// druzyna gracza na zapisie Jeffa bez towarzyszy, bandyci bez bohaterow, wiec nowy kod bohaterow (DuelAi.ApplyField z VeteranAi, HeroTierFromSkill,
    /// DuelistBurstsInBattles, obrony bohaterow w DuelGuard / FieldDuel) w bitwie autotestu nie ruszal.
    /// Plik CrashScribe/armoury-walka-bohaterowie.flag (tylko DLL autotestu, LogGate.Mode 1/2 - jak FieldDuel.TestOff): raz na sesje przy starcie sesji
    /// (ArmouryBehavior.OnSessionLaunched, PO FieldDuel.SessionStart - etap bitew CrashScribe rusza kilka sekund po mapie, przed pierwsza godzina)
    /// do 4 wolnych wedrowcow (bez druzyny, bez rodu, nie jency, nie dzieci, zywi) do rodu i druzyny gracza akcjami gry (AddCompanionAction,
    /// AddHeroToPartyAction - jak najem w rozmowie), zdrowi. Rozrzut najlepszej umiejetnosci broni bialej: ponizej 100 (regula weteranow),
    /// 100-139, 140-179, 180+ (profil DuelAi); pasmo bez kandydata - najblizszy umiejetnoscia z reszty. Po kazdej bitwie gracza (MapEventEnded)
    /// zywi bohaterowie testu znowu zdrowi (ranny bohater nie wychodzi na pole w nastepnej bitwie autotestu).
    /// Linia "Bitwa (m113-walka2): TEST bohaterowie do bitwy: ... - tylko autotest". W grze Jeffa (LogGate 0) flaga nieczytana, nic sie nie dzieje.
    /// Zmiana tylko w zapisie autotestu (towarzysze w rodzie gracza). Watek gry.
    /// </summary>
    internal static class FieldDuelHeroes
    {
        internal const string FlagName = "armoury-walka-bohaterowie.flag";
        private const int Max = 4;
        private static int _flag = -1;
        private static readonly List<Hero> _added = new List<Hero>();

        /// <summary>Czy lezy flaga testu (w zwyklej grze - LogGate 0 - zawsze false). Pamiec na proces dopiero w kampanii (jak FieldDuel.TestOff).</summary>
        internal static bool TestOn
        {
            get
            {
                if (_flag >= 0) return _flag == 1;
                bool on = false;
                try
                {
                    int mode = LogGate.Mode;
                    if (mode == 1 || mode == 2)
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        on = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), FlagName));
                    }
                }
                catch { on = false; }
                if (Campaign.Current != null) _flag = on ? 1 : 0;
                return on;
            }
        }

        private static int BestMelee(Hero h)
        {
            return Math.Max(h.GetSkillValue(DefaultSkills.OneHanded), Math.Max(h.GetSkillValue(DefaultSkills.TwoHanded), h.GetSkillValue(DefaultSkills.Polearm)));
        }

        private static int Band(int s) { return s < 100 ? 0 : (s < 140 ? 1 : (s < 180 ? 2 : 3)); }

        // odleglosc umiejetnosci od pasma (0 w pasmie) - zastepstwo dla pustego pasma
        private static int Dist(int s, int band)
        {
            int lo = band == 0 ? 0 : (band == 1 ? 100 : (band == 2 ? 140 : 180));
            int hi = band == 0 ? 99 : (band == 1 ? 139 : (band == 2 ? 179 : int.MaxValue));
            return s < lo ? lo - s : (s > hi ? s - hi : 0);
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (PO FieldDuel.SessionStart) - raz na sesje; bez flagi tylko wyczyszczona lista. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            _added.Clear();
            if (!TestOn) return;
            try { Add(); }
            catch (Exception e) { Log.Error("FieldDuelHeroes.Add", e); }
        }

        private static void Add()
        {
            var main = MobileParty.MainParty;
            var pc = Clan.PlayerClan;
            if (main == null || pc == null || Hero.MainHero == null || Campaign.Current == null)
            {
                Log.Info("Bitwa (m113-walka2): TEST bohaterowie do bitwy: brak druzyny albo rodu gracza - tylko autotest.");
                return;
            }
            var cand = new List<Hero>();
            var skill = new Dictionary<Hero, int>();
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsWanderer || h.IsChild || !h.IsAlive || h.IsPrisoner) continue;
                if (h.Clan != null || h.CompanionOf != null || h.PartyBelongedTo != null || h.PartyBelongedToAsPrisoner != null) continue;
                if (h.HeroState != Hero.CharacterStates.Active) continue;
                if (h.BattleEquipment == null || ReferenceEquals(h.BattleEquipment, Campaign.Current.DeadBattleEquipment)) continue;
                cand.Add(h);
                skill[h] = BestMelee(h);
            }
            var pick = new Hero[Max];
            // najpierw kazde pasmo: najlepszy w pasmie
            for (int b = 0; b < Max; b++)
            {
                Hero best = null;
                foreach (var h in cand)
                {
                    if (Band(skill[h]) != b) continue;
                    if (best == null || skill[h] > skill[best]) best = h;
                }
                pick[b] = best;
            }
            // pasmo bez kandydata - najblizszy umiejetnoscia z reszty
            for (int b = 0; b < Max; b++)
            {
                if (pick[b] != null) continue;
                Hero best = null;
                foreach (var h in cand)
                {
                    if (Array.IndexOf(pick, h) >= 0) continue;
                    if (best == null || Dist(skill[h], b) < Dist(skill[best], b)) best = h;
                }
                pick[b] = best;
            }
            var sb = new StringBuilder(600);
            sb.Append("Bitwa (m113-walka2): TEST bohaterowie do bitwy: ");
            int n = 0, fail = 0;
            for (int b = 0; b < Max; b++)
            {
                var h = pick[b];
                if (h == null) continue;
                try
                {
                    AddCompanionAction.Apply(pc, h);
                    AddHeroToPartyAction.Apply(h, main, true);
                    h.HitPoints = h.MaxHitPoints;
                    _added.Add(h);
                    sb.Append(n > 0 ? ", " : "").Append(h.Name).Append(" (najlepsza umiejetnosc ").Append(skill[h]).Append(", poziom ").Append(h.Level).Append(')');
                    n++;
                }
                catch (Exception e) { fail++; Log.Error("FieldDuelHeroes.Apply", e); }
            }
            if (n == 0) sb.Append("brak");
            sb.Append(" - tylko autotest (wolnych wedrowcow ").Append(cand.Count).Append(", dodanych ").Append(n).Append(", nieudanych ").Append(fail)
              .Append("; flaga CrashScribe/").Append(FlagName).Append(", rod i druzyna gracza akcjami gry, zdrowi; po kazdej bitwie gracza znowu zdrowi).");
            Log.Info(sb.ToString());
        }

        /// <summary>CampaignEvents.MapEventEnded: po bitwie gracza zywi bohaterowie testu znowu zdrowi. Bez bohaterow testu - jedno porownanie.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (_added.Count == 0) return;
            try
            {
                if (me == null || !me.IsPlayerMapEvent) return;
                int healed = 0, dead = 0;
                foreach (var h in _added)
                {
                    if (h == null || !h.IsAlive) { dead++; continue; }
                    if (h.HitPoints < h.MaxHitPoints) { h.HitPoints = h.MaxHitPoints; healed++; }
                }
                Log.Info("Bitwa (m113-walka2): TEST bohaterowie po bitwie: wyleczonych " + healed + ", niezywych " + dead + " z " + _added.Count + " - tylko autotest.");
            }
            catch (Exception e) { Log.Error("FieldDuelHeroes.OnMapEventEnded", e); }
        }
    }
}
