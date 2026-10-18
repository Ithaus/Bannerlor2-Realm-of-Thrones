using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// sw-uchodzcy (noc 17/18.10, klucz RaidRefugeesToTowns; RaidRefugees.cs): uchodzcy z najazdu w miescie sa biedota - klasa W kiesy ludu.
    ///  - kotwica jutra (Anchor): ich glowy w koszyku klasy W (RefugeeHeadsOf) - lud miasta kupuje z targu takze dla nich, kiesa W placi (Afford klasy W wobec
    ///    wiekszej potrzeby); zarobek klasy W (place, robocizna, uslugi) dzieli sie na wiecej glow - nikt nie placi z niczego;
    ///  - kiesa: czesc kiesy wsi przybylych -> kiesa W miasta (RefugeePurseIn) i z powrotem przy powrocie (RefugeePurseOut) - przelew posiadacz -> posiadacz
    ///    (kiesa wsi <-> kiesy ludu miast H22), kontrola zamknieta kies (_ctlIn/_ctlOut), ksiega pieniadza - pozycja MFolk (RaidRefugees.Daily biegnie w niej).
    /// Glowy: ludzie ksiegi x glowy BK osady na czlowieka podstawy (rynek miasta przy MarketByPeople - PeopleScale.TryPeople, inaczej ludzie miasta) - ta sama skala,
    /// w ktorej kiesa liczy glowy mieszczan. Klucz wylaczony / nikt w miescie - 0 (kotwica co do monety jak dotad). Watek glowny.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>Glowy BK na czlowieka ksiegi osady (r - skala glow tej doby). false - brak podstawy.</summary>
        private static bool HeadsPerPerson(Folk f, double r, out double hpp)
        {
            hpp = 0;
            if (f == null || f.St == null || f.Kind != 0) return false;
            double heads = (Math.Max(0, f.HW) + Math.Max(0, f.HR) + Math.Max(0, f.HP) + Math.Max(0, f.HS)) * r;
            if (!(heads > 0)) return false;
            double basis = 0;
            float p;
            if (PeopleMarket.MarketOn && PeopleScale.TryPeople(f.St, out p) && p > 0f) basis = p;
            else { try { basis = PopulationLaw.PeopleOf(f.St); } catch { basis = 0; } }
            if (!(basis > 0)) return false;
            hpp = heads / basis;
            return !(double.IsNaN(hpp) || double.IsInfinity(hpp));
        }

        /// <summary>Anchor: glowy uchodzcow z najazdu mieszkajacych w miescie (do koszyka klasy W). 0 - nikt, klucz wylaczony.</summary>
        private static double RefugeeHeadsOf(Folk f, double r)
        {
            try
            {
                if (f == null || f.Kind != 0 || f.St == null) return 0;
                double people = RaidRefugees.PresentIn(f.St);
                if (!(people > 0)) return 0;
                double hpp;
                if (!HeadsPerPerson(f, r, out hpp)) return 0;
                double h = people * hpp;
                return h > 0 && !double.IsInfinity(h) ? h : 0;
            }
            catch (Exception e) { Stumble("RefugeeHeadsOf", e); return 0; }
        }

        /// <summary>RaidRefugees: glowy BK na czlowieka ksiegi, glowy klasy W mieszczan i kiesa W miasta (kiesa czynna, miasto skalibrowane). false - brak.</summary>
        internal static bool RefugeeW(Settlement town, out double hpp, out double nativeW, out long kw)
        {
            hpp = 0; nativeW = 0; kw = 0;
            try
            {
                if (town == null || !town.IsTown || !Live || OffMain()) return false;
                EnsureImported();
                Folk f;
                if (!_f.TryGetValue(town, out f) || f == null || f.Kind != 0 || !f.Cal) return false;
                double r = Scale(f);
                if (!HeadsPerPerson(f, r, out hpp)) return false;
                nativeW = Math.Max(0, f.HW) * r;
                kw = f.KW;
                return true;
            }
            catch (Exception e) { Stumble("RefugeeW", e); hpp = 0; nativeW = 0; kw = 0; return false; }
        }

        /// <summary>Recenzja (drobne 5): kiesa W miasta istnieje (kiesa ludu czynna, miasto w kiesie ludu - takze nieskalibrowane) i jej stan - pokrycie salda i naleznych
        /// uchodzcow (RaidRefugees). false - kiesa nieczynna albo miasta brak (nalezne bez pokrycia).</summary>
        internal static bool RefugeePurse(Settlement town, out long kw)
        {
            kw = 0;
            try
            {
                if (town == null || !town.IsTown || !Live || OffMain()) return false;
                EnsureImported();
                Folk f;
                if (!_f.TryGetValue(town, out f) || f == null || f.Kind != 0) return false;
                kw = f.KW;
                return true;
            }
            catch (Exception e) { Stumble("RefugeePurse", e); kw = 0; return false; }
        }

        /// <summary>Przybyli uchodzcy: kiesa wsi `vil` -> kiesa W miasta `town`, najwyzej `want` i stan kiesy wsi. Zwraca przelane (0 - kiesa nieczynna, miasto
        /// nieskalibrowane, wyjatek - nic nie schodzi).</summary>
        internal static long RefugeePurseIn(Settlement town, Village vil, long want)
        {
            try
            {
                if (want <= 0 || town == null || vil == null || !town.IsTown || !Live || OffMain()) return 0;
                EnsureImported();
                Folk f;
                if (!_f.TryGetValue(town, out f) || f == null || f.Kind != 0 || !f.Cal) return 0;
                int ask = (int)Math.Min(int.MaxValue, Math.Min(want, Math.Max(0, vil.Gold)));
                if (ask <= 0) return 0;
                int had = vil.Gold;
                vil.ChangeGold(-ask);
                long got = had - vil.Gold;
                if (got <= 0) return 0;
                f.KW += got; _ctlIn += got;
                return got;
            }
            catch (Exception e) { Stumble("RefugeePurseIn", e); return 0; }
        }

        /// <summary>Powracajacy uchodzcy: kiesa W miasta `town` -> kiesa wsi `vil`, najwyzej `want` i stan kiesy W. Zwraca przelane.</summary>
        internal static long RefugeePurseOut(Settlement town, Village vil, long want)
        {
            bool taken = false; long x = 0; Folk f = null;
            try
            {
                if (want <= 0 || town == null || vil == null || !town.IsTown || !Live || OffMain()) return 0;
                EnsureImported();
                if (!_f.TryGetValue(town, out f) || f == null || f.Kind != 0) return 0;
                x = Math.Min(want, Math.Min(int.MaxValue, Math.Max(0, f.KW)));
                if (x <= 0) return 0;
                f.KW -= x; taken = true;
                vil.ChangeGold((int)x);
                _ctlOut += x;
                return x;
            }
            catch (Exception e)
            {
                if (taken && f != null) f.KW += x;      // wyjatek w ChangeGold - zloto zostaje w kiesie W (nikt nie traci)
                Stumble("RefugeePurseOut", e); return 0;
            }
        }
    }
}
