using System;
using System.Globalization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Helpers;

namespace Armoury
{
    /// <summary>
    /// R3-10b INZYNIERIA PRZY BUDOWACH (POMYSLY-35 nr 32, Jeff 20.10 "pozostale tak"; audyt 13-UMIEJETNOSCI Z11; klucz EngineeringOnPaidWork).
    /// Problem: PowerPostfix (BuildFunding.cs) PODMIENIA wynik modelu budow gry (Naval -> BKROT -> BK -> vanilla) na punkty oplacone przez pana
    /// (+ przyspieszenie zlotem, + roboty ludu) - razem z wynikiem ginela premia zarzadcy z Inzynierii (DefaultSkillEffects.TownProjectBuildingBonus:
    /// +0.25% za punkt Inzynierii, AddFactor) i perki budowy zarzadcy: Carpenters (miasto +12%), Military Planner (zamek +25%), Stonecutters
    /// (mury, koszary, wodociagi +30% - lista budowli jak w BKConstructionModel), Clockwork (miasto: +20% skutku przyspieszenia zlotem).
    /// Inzynieria rosla tylko przy oblezeniach.
    /// Regula (tylko osady objete rozliczeniem - Covered; lenna gracza bez PaidConstructionPlayer ida modelem gry, ktory te premie juz ma - bez dubla):
    ///  - zarzadca NA MIEJSCU (warunek gry: Governor.CurrentSettlement.Town == town) mnozy punkty oplacone przez pana i przyspieszenie zlotem
    ///    tak jak gra: x (1 + Inzynieria x 0.25% + perki); Clockwork jak w grze bez warunku obecnosci (tylko miasto, tylko przyspieszenie zlotem);
    ///  - roboty ludu (P2, "Town works (paid by the townsfolk)") bez premii - pomysl mowi o budowach oplaconych przez pana;
    ///  - zarzadca dostaje 10 XP Inzynierii za kazdy punkt oplacony przez pana tej doby (przed premia), w bloku dobowym przy zaplacie
    ///    (HeroDeveloper.AddSkillXp z mnoznikiem nauki, jak XP zarzadcy w grze).
    /// Premia to wiecej pracy z tych samych pieniedzy (lepsza organizacja robot) - zloto i material bez zmian, ekonomia zamknieta jak dotad.
    /// Liczenie premii jest czyste (bez stanu, bez alokacji) - PowerPostfix wolaja tez dymki i GetDaysToComplete. Liczniki doby tylko w bloku dobowym
    /// (watek glowny); potkniecia liczone (Interlocked), blad do logu raz na kampanie. Linia logu "Inzynieria przy budowach (R3-10b)" na koncu doby budow
    /// (po "Budowy oplacone" i robotach ludu). EngPaidNeed - odwrotnosc premii dla liczenia "ile pan musi doplacic do konca poziomu" (R3-11a OneNeed przy scalaniu).
    /// </summary>
    internal static partial class BuildFunding
    {
        internal static bool EngOn { get { var s = Settings.Current; return s != null && s.PaidConstruction && s.EngineeringOnPaidWork; } }
        private const float EngXpPerPoint = 10f;   // XP Inzynierii zarzadcy za punkt oplacony przez pana (pomysl R3-10b)
        private static TextObject _engTxt;
        private static int _engErrN; private static bool _engErr;
        private static WeakReference _engErrCamp;   // kampania, w ktorej blad juz trafil do logu (recenzja: nowa kampania/wczytanie - znow do logu; bez wpiecia w BuildFunding.Reset)
        // liczniki doby (blok dobowy, watek glowny)
        private static int _engTowns, _engGov, _engGovTown, _engGovCastle, _engSkilled, _engSkillMax, _engCarp, _engPlan, _engStone, _engClock, _engXpHeroes;
        private static long _engSkillSum; private static double _engBase, _engExtra, _engXp;

        /// <summary>Punkty dodane przez zarzadce do punktow pana `paid` i przyspieszenia zlotem `boost` (0 - brak zarzadcy, klucz wylaczony, blad).
        /// Czyste: tylko odczyt zarzadcy i czola kolejki. count - tylko z bloku dobowego (watek glowny): liczniki perkow doby.</summary>
        private static float EngExtra(Town town, float paid, float boost, bool count)
        {
            try
            {
                if (!EngOn || town == null || paid + boost <= 0f) return 0f;
                var g = town.Governor;
                if (g == null || !g.IsAlive) return 0f;
                float clock = 0f;
                if (boost > 0f && town.IsTown && DefaultPerks.Engineering.Clockwork != null && g.GetPerkValue(DefaultPerks.Engineering.Clockwork))
                {
                    clock = boost * DefaultPerks.Engineering.Clockwork.SecondaryBonus;   // gra: GetBoostAmount x (1 + Clockwork) przed udzialem wplaty
                    if (count) _engClock++;
                }
                var cur = g.CurrentSettlement;
                if (cur == null || cur.Town != town) return Math.Max(0f, clock);   // gra: premia umiejetnosci i perki tylko przy zarzadcy na miejscu
                var q = town.BuildingsInProgress;
                if (q == null || q.Count == 0) return Math.Max(0f, clock);
                float baseN = paid + boost + clock;
                var e = new ExplainedNumber(baseN, false, null);
                SkillHelper.AddSkillBonusForTown(DefaultSkillEffects.TownProjectBuildingBonus, town, ref e);   // +0.25% za punkt Inzynierii (efekt gry, AddFactor)
                if (town.IsCastle && g.GetPerkValue(DefaultPerks.Engineering.MilitaryPlanner))
                {
                    e.AddFactor(DefaultPerks.Engineering.MilitaryPlanner.SecondaryBonus, null);
                    if (count) _engPlan++;
                }
                else if (town.IsTown && g.GetPerkValue(DefaultPerks.Engineering.Carpenters))
                {
                    e.AddFactor(DefaultPerks.Engineering.Carpenters.SecondaryBonus, null);
                    if (count) _engCarp++;
                }
                var head = q.Peek();
                var bt = head != null ? head.BuildingType : null;
                if (bt != null && (bt == DefaultBuildingTypes.SettlementFortifications || bt == DefaultBuildingTypes.CastleFortifications || bt == DefaultBuildingTypes.CastleBarracks
                    || bt == DefaultBuildingTypes.SettlementBarracks || bt == DefaultBuildingTypes.SettlementWaterworks) && g.GetPerkValue(DefaultPerks.Engineering.Stonecutters))
                {
                    e.AddFactor(DefaultPerks.Engineering.Stonecutters.PrimaryBonus, null);
                    if (count) _engStone++;
                }
                float extra = e.ResultNumber - paid - boost;
                return extra > 0f && !float.IsNaN(extra) && !float.IsInfinity(extra) ? extra : 0f;
            }
            catch (Exception ex) { EngErr("BuildFunding.EngExtra", ex); return 0f; }
        }

        /// <summary>Potkniecie: licznik (Interlocked) i blad do logu raz na kampanie (CLAUDE.md 7). Alokacja tylko przy bledzie.</summary>
        private static void EngErr(string where, Exception ex)
        {
            Interlocked.Increment(ref _engErrN);
            try
            {
                var c = Campaign.Current;
                var w = _engErrCamp;
                if (_engErr && w != null && ReferenceEquals(w.Target, c)) return;
                _engErr = true; _engErrCamp = new WeakReference(c);
                Log.Error(where, ex);
            }
            catch { }
        }

        /// <summary>
        /// Punkty, ktore pan musi oplacic, zeby przy ticku osady postep budowli na czole kolejki urosl o `rem` (koszt poziomu - postep), przy dzisiejszym
        /// przyspieszeniu zlotem i zarzadcy: postep doby = pana + przyspieszenie + EngExtra(pana, przyspieszenie), a EngExtra jest liniowe:
        /// EngExtra(p, b) = EngExtra(0, b) + p x EngExtra(1, 0). Klucz wylaczony / brak zarzadcy = rem - przyspieszenie (jak dotad). Czyste, bez alokacji.
        /// Dla R3-11a (p5-budowy, OneNeed): "double rem = cost - b.BuildingProgress - Boost(st.Town);" -> "double rem = EngPaidNeed(st.Town, cost - b.BuildingProgress);".
        /// </summary>
        internal static double EngPaidNeed(Town town, double rem)
        {
            float b = town != null ? Boost(town) : 0f;
            double left = rem - b - EngExtra(town, 0f, b, false);
            if (!(left > 0)) return 0;
            double f = 1.0 + EngExtra(town, 1f, 0f, false);
            return f > 1.0 ? left / f : left;
        }

        /// <summary>PowerPostfix: pozycja dymku zarzadcy (dodana do wyniku osady objetej rozliczeniem).</summary>
        private static void EngAdd(Town town, float paid, float boost, ref ExplainedNumber r)
        {
            float x = EngExtra(town, paid, boost, false);
            if (x <= 0f) return;
            if (_engTxt == null) _engTxt = new TextObject("{=!}Governor's engineering");
            r.Add(x, _engTxt);
        }

        /// <summary>Daily: poczatek doby - liczniki na zero.</summary>
        private static void EngDayStart()
        {
            _engTowns = _engGov = _engGovTown = _engGovCastle = _engSkilled = _engSkillMax = _engCarp = _engPlan = _engStone = _engClock = _engXpHeroes = 0;
            _engSkillSum = 0; _engBase = _engExtra = _engXp = 0;
        }

        /// <summary>Daily, po zaplacie za budowe osady: liczniki i XP Inzynierii zarzadcy na miejscu (pts - punkty pana przed premia).</summary>
        private static void EngPaid(Town town, float pts)
        {
            if (!EngOn || town == null || pts <= 0f) return;
            try
            {
                _engTowns++;
                float b = Boost(town);
                _engBase += pts + b;
                _engExtra += EngExtra(town, pts, b, true);
                var g = town.Governor;
                if (g == null || !g.IsAlive || g.CurrentSettlement == null || g.CurrentSettlement.Town != town) return;
                _engGov++;
                if (town.IsCastle) _engGovCastle++; else _engGovTown++;
                int sk = g.GetSkillValue(DefaultSkills.Engineering);
                if (sk > 0) { _engSkilled++; _engSkillSum += sk; if (sk > _engSkillMax) _engSkillMax = sk; }
                float xp = pts * EngXpPerPoint;
                if (xp > 0f && g.HeroDeveloper != null)
                {
                    g.HeroDeveloper.AddSkillXp(DefaultSkills.Engineering, xp, true, true);
                    _engXp += xp; _engXpHeroes++;
                }
            }
            catch (Exception ex) { EngErr("BuildFunding.EngPaid", ex); }
        }

        /// <summary>Daily, na koncu doby budow (po "Budowy oplacone" i robotach ludu): linia R3-10b.</summary>
        private static void EngLine()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                int err = Interlocked.Exchange(ref _engErrN, 0);
                Log.Info("Inzynieria przy budowach (R3-10b, Engineering On Paid Work " + (EngOn ? "tak" : "nie") + "): dzien " + (int)CampaignTime.Now.ToDays
                         + " - osad oplaconych przez pana " + _engTowns + ", z zarzadca na miejscu " + _engGov + " (miasta " + _engGovTown + ", zamki " + _engGovCastle
                         + "), z Inzynieria > 0 " + _engSkilled + " (srednio " + (_engSkilled > 0 ? ((double)_engSkillSum / _engSkilled).ToString("0", inv) : "-") + ", najwiecej " + _engSkillMax
                         + "); premia zarzadcow (stan przy zaplacie) +" + _engExtra.ToString("0.0", inv) + " pkt do " + _engBase.ToString("0.0", inv) + " pkt pana ("
                         + (_engBase > 0 ? (100.0 * _engExtra / _engBase).ToString("0.0", inv) : "0.0") + "%); perki: Carpenters " + _engCarp + ", Military Planner " + _engPlan
                         + ", Stonecutters " + _engStone + ", Clockwork " + _engClock + "; XP Inzynierii zarzadcom " + _engXp.ToString("0", inv) + " (zarzadcow " + _engXpHeroes + ")"
                         + "; potkniecia " + err + ".");
            }
            catch (Exception e) { Log.Error("BuildFunding.EngLine", e); }
        }
    }
}
