using System;
using System.Globalization;
using System.Threading;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 515 (perki 43 i 46 audytu perkow 14.11; Jeff 14.11: "43 perki predkosci Scouting (pomiar, potem perk podnosi sufit kolumny): ok", "46 CallToArms jak 43: ok").
    ///
    /// DOWOD (DefaultPartySpeedCalculatingModel.CalculateFinalSpeed, gra 1.4.8; BKROT / RBL / ROT / BEE / NavalDLC woluja te metode gry): premie zwiadowcy
    /// partii (EffectiveScout) to czynniki wyniku - Pathfinder (rownina, step), DesertBorn (pustynia, wydmy), NightRunner (noc), DayTraveler (dzien),
    /// UncannyInsight (epicki, od umiejetnosci ponad prog), ForcedMarch (morale > 75), Tracker (premia druga: poscig za wrogiem, EngageParty) oraz Tactics
    /// CallToArms (wodz armii; partia armii jeszcze niedoczepiona). Potem MarchPace.ColumnPostfix (Priority.Last) tnie wynik do sufitu kolumny
    /// (MarchPace.cs:225-285: LimitMax) - gdy sufit wiaze, perk nie daje nic (piechota idzie 4.0 z perkiem i bez).
    ///
    /// ZMIANA: gdy sufit kolumny wiaze (wynik > sufit), liczymy sume premii tych perkow tak jak gra (te same warunki, te same wartosci perkow, na probce
    /// ExplainedNumber(1) - Add i AddFactor jak w grze) i podnosimy sufit o ta sama czesc: sufit x (1 + suma), nigdy ponad wynik gry (perk nie przyspiesza
    /// ponad naturalna predkosc). Kolumna dalej idzie tempem najwolniejszego, tylko lepszy zwiadowca prowadzi ja szybciej - jak droga (MapRoads podnosi
    /// LimitMax o premie drogi). Klucz ScoutPerksLiftMarchCap (TAK; NIE = sam pomiar - sufit bez zmian, liczniki w linii doby dalej licza, ile perk traci).
    /// POMINIETE: Scouting Unburdened (glowna) - zmniejsza kare przeladowania w predkosci BAZOWEJ (GetOverburdenedEffect), a sufit kolumny nie zalezy od ladunku:
    /// partia przeladowana z perkiem bylaby wtedy szybsza od nieprzeladowanej; Riding NomadicTraditions i SweepingWind - czynniki jazdy w predkosci bazowej
    /// (sufit ma juz osobne tempo jazdy i piechoty na luzakach). Morze i przejscie lad-morze - kolumna sie nie stosuje (MarchPace wychodzi wczesniej).
    /// Wolane z MarchPace.ColumnPostfix tylko, gdy sufit wiaze (raz na lancuch - SpeedDepth.OutermostFinal); bez zapisu stanu, wiele watkow - liczniki Interlocked.
    /// Linia doby "Perki zwiadu a kolumna (515): dzien N" - ile razy sufit wiazal, ile z perkami, srednia premia i ile perk tracil przed / po podniesieniu.
    /// Bledy liczone (potkniecia) - wyjatek = sufit bez zmian dla tego wywolania, bez globalnego wylacznika.
    /// </summary>
    internal static class ScoutPace515
    {
        internal const string P = "Perki zwiadu a kolumna (515): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // liczniki doby (watki rownolegle - Interlocked; sumy w tysiecznych)
        private static long _bind, _withPerk, _player, _sumPerkM, _lostBeforeM, _lostAfterM, _curM, _lifted;
        private static long _nPath, _nDesert, _nNight, _nDay, _nUncanny, _nForced, _nTracker, _nCall;
        private static int _stumbles, _dStumbles;
        private static volatile string _sample = "-";   // ostatnie wywolanie gracza (tylko log)

        internal static void Reset()
        {
            ResetDay();
            _stumbles = 0;
            _sample = "-";
        }

        private static void ResetDay()
        {
            Interlocked.Exchange(ref _bind, 0); Interlocked.Exchange(ref _withPerk, 0); Interlocked.Exchange(ref _player, 0);
            Interlocked.Exchange(ref _sumPerkM, 0); Interlocked.Exchange(ref _lostBeforeM, 0); Interlocked.Exchange(ref _lostAfterM, 0);
            Interlocked.Exchange(ref _curM, 0); Interlocked.Exchange(ref _lifted, 0);
            Interlocked.Exchange(ref _nPath, 0); Interlocked.Exchange(ref _nDesert, 0); Interlocked.Exchange(ref _nNight, 0); Interlocked.Exchange(ref _nDay, 0);
            Interlocked.Exchange(ref _nUncanny, 0); Interlocked.Exchange(ref _nForced, 0); Interlocked.Exchange(ref _nTracker, 0); Interlocked.Exchange(ref _nCall, 0);
            _dStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            Interlocked.Increment(ref _dStumbles);
            if (n <= 3 || n % 1000 == 0) Log.Error("ScoutPace515." + where + " (potkniecie " + n + ")", e);
        }

        private static long M(float v) { return (long)Math.Round(v * 1000.0); }

        /// <summary>Suma premii perkow predkosci gry dla partii (czesc wyniku, jak SumOfFactors): te same warunki co DefaultPartySpeedCalculatingModel.CalculateFinalSpeed.
        /// mask - ktore perki daly premie (bity jak liczniki), tylko log.</summary>
        private static float PerkShare(MobileParty mp, out int mask)
        {
            mask = 0;
            if (mp.IsCurrentlyAtSea) return 0f;
            var probe = new ExplainedNumber(1f);
            var scout = mp.EffectiveScout;
            if (scout != null)
            {
                TerrainType face = TerrainType.Plain;
                bool haveFace = false;
                try { face = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace); haveFace = true; } catch { }
                if (haveFace)
                {
                    // jak gra: las i brod (woda, rzeka, most) bez tych perkow; pustynia i wydmy - DesertBorn; rownina i step - Pathfinder
                    if (face == TerrainType.Desert || face == TerrainType.Dune)
                    {
                        var pk = DefaultPerks.Scouting.DesertBorn;
                        if (pk != null && scout.GetPerkValue(pk)) { probe.AddFactor(pk.PrimaryBonus); mask |= 2; }
                    }
                    else if (face == TerrainType.Plain || face == TerrainType.Steppe)
                    {
                        var pk = DefaultPerks.Scouting.Pathfinder;
                        if (pk != null && scout.GetPerkValue(pk)) { probe.AddFactor(pk.PrimaryBonus); mask |= 1; }
                    }
                }
                if (Campaign.Current.IsNight)
                {
                    var pk = DefaultPerks.Scouting.NightRunner;
                    if (pk != null && scout.GetPerkValue(pk)) { probe.AddFactor(pk.PrimaryBonus); mask |= 4; }
                }
                else
                {
                    var pk = DefaultPerks.Scouting.DayTraveler;
                    if (pk != null && scout.GetPerkValue(pk)) { probe.AddFactor(pk.PrimaryBonus); mask |= 8; }
                }
                var ui = DefaultPerks.Scouting.UncannyInsight;
                if (ui != null && scout.GetPerkValue(ui))
                {
                    float before = probe.ResultNumber;
                    PerkHelper.AddEpicPerkBonusForCharacter(ui, scout.CharacterObject, DefaultSkills.Scouting, true, ref probe,
                        Campaign.Current.Models.CharacterDevelopmentModel.MinSkillRequiredForEpicPerkBonus);
                    if (probe.ResultNumber > before + 1e-6f) mask |= 16;
                }
                var fm = DefaultPerks.Scouting.ForcedMarch;
                if (fm != null && mp.Morale > 75f && scout.GetPerkValue(fm)) { probe.AddFactor(fm.PrimaryBonus); mask |= 32; }
                if (mp.DefaultBehavior == AiBehavior.EngageParty)
                {
                    var tp = mp.TargetParty;
                    var tr = DefaultPerks.Scouting.Tracker;
                    if (tp != null && tr != null && !tp.IsCurrentlyAtSea && tp.MapFaction != null && mp.MapFaction != null
                        && tp.MapFaction.IsAtWarWith(mp.MapFaction) && scout.GetPerkValue(tr))
                    { probe.AddFactor(tr.SecondaryBonus); mask |= 64; }
                }
            }
            var army = mp.Army;
            var lp = army != null ? army.LeaderParty : null;
            var cta = DefaultPerks.Tactics.CallToArms;
            if (lp != null && lp != mp && mp.AttachedTo != lp && cta != null && lp.HasPerk(cta)) { probe.AddFactor(cta.PrimaryBonus); mask |= 128; }
            return Math.Max(0f, probe.ResultNumber - 1f);
        }

        /// <summary>MarchPace.ColumnPostfix: sufit kolumny wiaze (current &gt; cap). Zwraca sufit podniesiony o premie perkow (klucz TAK), nigdy ponad current;
        /// sumOfFactors - suma czynnikow wyniku gry (do pomiaru: predkosc bez perkow).</summary>
        internal static float Lift(MobileParty mp, float cap, float current, float sumOfFactors)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || mp == null) return cap;
                Interlocked.Increment(ref _bind);
                int mask;
                float share = PerkShare(mp, out mask);
                if (share <= 0.0001f) return cap;
                Interlocked.Increment(ref _withPerk);
                if (mp == MobileParty.MainParty) Interlocked.Increment(ref _player);
                Interlocked.Add(ref _sumPerkM, M(share));
                Interlocked.Add(ref _curM, M(current));
                if ((mask & 1) != 0) Interlocked.Increment(ref _nPath);
                if ((mask & 2) != 0) Interlocked.Increment(ref _nDesert);
                if ((mask & 4) != 0) Interlocked.Increment(ref _nNight);
                if ((mask & 8) != 0) Interlocked.Increment(ref _nDay);
                if ((mask & 16) != 0) Interlocked.Increment(ref _nUncanny);
                if ((mask & 32) != 0) Interlocked.Increment(ref _nForced);
                if ((mask & 64) != 0) Interlocked.Increment(ref _nTracker);
                if ((mask & 128) != 0) Interlocked.Increment(ref _nCall);
                // predkosc gry bez perkow: wynik / (1 + F) x (1 + F - premia) - czesc wyniku, ktora daly perki
                float f = 1f + sumOfFactors;
                float natural = f > 0.01f ? current / f * Math.Max(0.01f, f - share) : current / (1f + share);
                float perkPart = Math.Max(0f, current - natural);
                float lostBefore = Math.Min(perkPart, current - cap);
                float lifted = s.ScoutPerksLiftMarchCap ? Math.Min(current, cap * (1f + share)) : cap;
                float lostAfter = Math.Max(0f, Math.Min(perkPart, current - lifted));
                Interlocked.Add(ref _lostBeforeM, M(lostBefore));
                Interlocked.Add(ref _lostAfterM, M(lostAfter));
                if (lifted > cap + 1e-4f) Interlocked.Increment(ref _lifted);
                if (mp == MobileParty.MainParty)
                    _sample = "gracz: wynik gry " + current.ToString("0.00", Inv) + ", premia perkow +" + (share * 100f).ToString("0", Inv) + "%, sufit "
                              + cap.ToString("0.00", Inv) + " -> " + lifted.ToString("0.00", Inv);
                return lifted;
            }
            catch (Exception e) { Stumble("Lift", e); return cap; }
        }

        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                Log.Info(P + "start sesji | klucz Scout Perks Lift March Cap " + (s != null && s.ScoutPerksLiftMarchCap ? "TAK" : "NIE (sam pomiar)")
                         + " | kolumna MarchPace " + (s != null && s.MarchPaceEnabled ? "TAK" : "NIE - sufitu brak, perki dzialaja jak w grze")
                         + " | perki: Pathfinder, DesertBorn, NightRunner, DayTraveler, UncannyInsight, ForcedMarch, Tracker (druga), CallToArms; pominiete Unburdened"
                         + " (kara ladunku w bazie) | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        internal static void Daily(int day)
        {
            try
            {
                long bind = Interlocked.Read(ref _bind), wp = Interlocked.Read(ref _withPerk);
                if (bind == 0 && _dStumbles == 0) { ResetDay(); return; }
                double n = Math.Max(1, wp);
                var s = Settings.Current;
                Log.Info(P + "dzien " + day + " | klucz " + (s != null && s.ScoutPerksLiftMarchCap ? "TAK" : "NIE (pomiar)")
                         + " | sufit kolumny wiazal " + bind + " razy, z premia perkow " + wp + " (gracz " + Interlocked.Read(ref _player) + ")"
                         + (wp > 0
                            ? " | srednio: wynik gry " + (Interlocked.Read(ref _curM) / 1000.0 / n).ToString("0.00", Inv)
                              + ", premia +" + (Interlocked.Read(ref _sumPerkM) / 10.0 / n).ToString("0.0", Inv) + "%"
                              + ", perk tracil pod sufitem " + (Interlocked.Read(ref _lostBeforeM) / 1000.0 / n).ToString("0.00", Inv)
                              + " -> po podniesieniu " + (Interlocked.Read(ref _lostAfterM) / 1000.0 / n).ToString("0.00", Inv) + " jedn. mapy"
                              + ", podniesien " + Interlocked.Read(ref _lifted)
                              + " | perki: Pathfinder " + _nPath + ", DesertBorn " + _nDesert + ", NightRunner " + _nNight + ", DayTraveler " + _nDay
                              + ", UncannyInsight " + _nUncanny + ", ForcedMarch " + _nForced + ", Tracker " + _nTracker + ", CallToArms " + _nCall
                            : "")
                         + " | " + _sample + " | potkniecia doby " + _dStumbles + " (od startu " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
