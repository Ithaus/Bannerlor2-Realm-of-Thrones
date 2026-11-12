using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L10 (migracja za praca, WorkMigration.cs): migranci ze wsi do miasta targowego zabieraja czesc kiesy wsi - kiesa wsi -> kiesa W (wyrobnicy) miasta.
    /// Przelew posiadacz -> posiadacz (jak uchodzcy z najazdu, RefugeePurseIn): ksiega kies _ctlIn i LDayIn (kontrola zamknieta kies). Nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>Kiesa W miasta czynna (kiesa ludu zywa, miasto skalibrowane) - cel migracji moze przyjac pieniadz. Sam odczyt.</summary>
        internal static bool WorkMigPurseLive(Settlement town)
        {
            try
            {
                if (town == null || !town.IsTown || !Live || OffMain()) return false;
                EnsureImported();
                Folk f;
                return _f.TryGetValue(town, out f) && f != null && f.Kind == 0 && f.Cal;
            }
            catch (Exception e) { Stumble("WorkMigPurseLive", e); return false; }
        }

        /// <summary>Kiesa wsi `vil` -> kiesa W miasta `town`, najwyzej `want` i stan kiesy wsi. Zwraca przelane (0 - kiesa nieczynna, miasto nieskalibrowane,
        /// wyjatek przed zdjeciem - nic nie schodzi; wyjatek po zdjeciu - zloto wraca do kiesy wsi).</summary>
        internal static long WorkMigPurseIn(Settlement town, Village vil, long want)
        {
            long got = 0; bool taken = false;
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
                got = had - vil.Gold;
                if (got <= 0) return 0;
                taken = true;
                f.KW += got; f.LDayIn += got; _ctlIn += got;
                return got;
            }
            catch (Exception e)
            {
                if (taken && got > 0) { try { vil.ChangeGold((int)got); } catch { } }
                Stumble("WorkMigPurseIn", e); return 0;
            }
        }
    }
}
