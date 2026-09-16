using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SPRZET IMIENNYCH BOHATEROW LORE (Jeff 30.08: "jesli pokonam i przekuje
    /// pancerz, to moge potem go tworzyc - inaczej nie da sie tego nauczyc").
    /// Ta sama lista prefiksow co w CrashScribe/Mends.cs (UniquePrefixes) -
    /// tam pilnuje handlu i przydzialu DTE, tu kuzni. AKTUALIZUJ OBIE RAZEM.
    /// </summary>
    internal static class UniqueGear
    {
        private static readonly string[] Prefixes = {
            "aemon_", "baelish_", "blackfyre_", "brienne_", "cersei_", "dany_",
            "euron_crown", "hound_", "houndskull", "joffrey_crown", "robb_crown",
            "baratheon_crown", "renly_crown", "renly_armor", "renly_shoulders",
            "melisandre_", "nightking_", "ramsay_", "rhaegar_", "stannis_",
            "tyrion_", "varys_", "bull_helmet"
        };

        // ZESTAW GORY (Jeff 16.09: "polowa wojska w pancerzu Clegana lub Mountain").
        // NIE prefiks "mountain_" - mountain_hunting_bow to zwykly luk 20 kultur.
        // Dlatego pelne id. Ta sama lista w Mends.UniqueIds - AKTUALIZUJ OBIE RAZEM.
        private static readonly string[] Ids = {
            "mountain_armor", "mountain_helmet", "mountain_pauldrons",
            "mountain_boots", "mountain_gloves", "mountain_gauntlets"
        };

        internal static bool Is(ItemObject it)
        {
            if (it == null) return false;
            var id = it.StringId ?? "";
            for (int i = 0; i < Prefixes.Length; i++)
                if (id.StartsWith(Prefixes[i], System.StringComparison.Ordinal)) return true;
            return System.Array.IndexOf(Ids, id) >= 0;
        }
    }
}
