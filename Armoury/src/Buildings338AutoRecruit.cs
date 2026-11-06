using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 2 - AUTOWERBUNEK ZALOGI (Castellan's Office +1/+2/+3, projekt Train Militia +1, projekt Raise Troops +2 ludzi na dobe; GarrisonAutoRecruitment).
    /// Efektu nie czyta nikt (gra 1.4.x: DefaultSettlementGarrisonModel.GetMaximumDailyAutoRecruitmentCount = stala 1; BK, ROT, NavalDLC deleguja). Naprawa: tylko
    /// na zewnetrznym poziomie lancucha (ArmsFromPeople.K8Postfix, licznik _k8Depth): limit = gra + premia K8 + budynki (K8 z "max" na "dodaj" - inaczej budynek
    /// znikalby pod K8). Budynek dziala niezaleznie od K8 (takze K8 wyl. / poza K8), ale nie w krainie z hamulcem wolnych rak (K8Brake - brak ludzi tej ziemi).
    /// To tylko wyzszy sufit: werbunek dalej tna budzet zoldu (Wage Limit), miejsce w zalodze, ochotnicy notabli i flaga BK autowerbunku AI.
    /// Wartosc: town.AddEffectOfBuildings(GarrisonAutoRecruitment) - poziom 0 pominiety, projekty dzienne tylko biezace (gra sprawdza sama).
    /// </summary>
    internal static class Buildings338AutoRecruit
    {
        [ThreadStatic] private static Town _lastTown;
        [ThreadStatic] private static int _lastBase;
        private static long _menBld;

        internal static void SessionStart() { _menBld = 0; }

        /// <summary>Premia budynkow do limitu dziennego autowerbunku twierdzy (0 = klucz wyl., brak budynku, blad).</summary>
        internal static int Bonus(Town t)
        {
            if (t == null || !Buildings338.AutoRecruitOn) return 0;
            try
            {
                var e = new ExplainedNumber(0f);
                t.AddEffectOfBuildings(BuildingEffectEnum.GarrisonAutoRecruitment, ref e);
                int b = (int)Math.Round(e.ResultNumber);
                return b > 0 ? b : 0;
            }
            catch (Exception ex) { Buildings338.Stumble(Buildings338.GRecruit, "AutoRecruit.Bonus", ex); return 0; }
        }

        /// <summary>Z K8Postfix: limit przed budynkami (gra + K8) tej twierdzy - do rozdzielenia licznikow K8 i budynkow w A5LimitPostfix.</summary>
        internal static void NoteBase(Town t, int baseLimit) { _lastTown = t; _lastBase = baseLimit; }

        /// <summary>A5LimitPostfix: limit bez budynkow tej twierdzy (z ostatniego K8Postfix tej twierdzy na tym watku); inna twierdza - fallback.</summary>
        internal static long BaseCap(Town t, long fallback) { return ReferenceEquals(_lastTown, t) ? _lastBase : fallback; }

        /// <summary>A5LimitPostfix (okno A5, doba werbunku): ludzie dobrani ponad limit bez budynkow - zasluga budynkow.</summary>
        internal static void NoteA5(Town t, float res)
        {
            try
            {
                if (t == null || !ReferenceEquals(_lastTown, t) || !Buildings338.AutoRecruitOn) return;
                long got = (long)Math.Ceiling(res);
                if (got > _lastBase) Interlocked.Add(ref _menBld, got - _lastBase);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GRecruit, "AutoRecruit.NoteA5", e); }
        }

        internal static void Daily(int day)
        {
            int n = 0, off = 0, tm = 0, rt = 0; long sum = 0;
            if (Buildings338.AutoRecruitOn)
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        int b = Bonus(t);
                        if (b <= 0) continue;
                        n++; sum += b;
                        if (Buildings338.Level(t, DefaultBuildingTypes.CastleCastallansOffice.StringId) > 0) off++;
                        var cur = t.CurrentDefaultBuilding;
                        if (cur != null && cur.BuildingType == DefaultBuildingTypes.SettlementDailyTrainMilitia) tm++;
                        if (cur != null && cur.BuildingType == DefaultBuildingTypes.CastleDailyRaiseTroops) rt++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GRecruit, "AutoRecruit.Daily", e); }
                }
            Log.Info("Budynki (338) autowerbunek: dzien " + day + " | twierdz " + n + " z premia (biuro " + off + ", Train Militia " + tm + ", Raise Troops " + rt + "), suma limitu +" + sum + "/d"
                     + " | dobrano ponad K8 " + Interlocked.Exchange(ref _menBld, 0) + " ludzi" + Buildings338.Tail(Buildings338.GRecruit));
        }
    }
}
