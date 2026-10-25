using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// UMARLI MAJA INNE PRAWA. Jeff: "Inni moga nie spac i nie maja staminy".
    /// Rozpoznajemy nieumarlych po kulturze klanu Bialych Wedrowcow z ROT
    /// (ROTclan_126 - tak samo sprawdza ich sam ROT), z zapasowym sitem po
    /// nazwach (wight/walker). Wight nie spi, nie krwawi, nie dostaje zadyszki
    /// i nie ucieka - trup nie zna zmeczenia ani strachu.
    /// </summary>
    internal static class Undead
    {
        private static string _cultureId;
        private static bool _resolved;

        private static string WwCultureId()
        {
            if (_resolved) return _cultureId;
            try
            {
                if (Campaign.Current == null) return null;   // jeszcze bez kampanii - sprobujemy pozniej
                var clan = Clan.FindFirst(c => c != null && c.StringId == "ROTclan_126");
                if (clan == null) return null;               // ROT bez Innych - tez sprobujemy pozniej
                _cultureId = clan.Culture != null ? clan.Culture.StringId : null;
                _resolved = true;
            }
            catch { }
            return _cultureId;
        }

        /// <summary>117 (recenzja 150b): kultura Innych - ta sama regula co Character (kultura rodu ROTclan_126 albo id z walker / wight); osady zajete
        /// przez Innych dostaja ja od ROT (ChangeToOthersCulture).</summary>
        internal static bool IsUndeadCulture(string cu)
        {
            if (string.IsNullOrEmpty(cu)) return false;
            var id = WwCultureId();
            return (id != null && cu == id) || cu.Contains("walker") || cu.Contains("wight");
        }

        internal static bool Character(BasicCharacterObject c)
        {
            try
            {
                if (c == null) return false;
                var cu = c.Culture != null ? (c.Culture.StringId ?? "") : "";
                var id = WwCultureId();
                if (id != null && cu == id) return true;
                var s = c.StringId ?? "";
                return cu.Contains("walker") || cu.Contains("wight")
                       || s.Contains("wight") || s.Contains("white_walker");
            }
            catch { return false; }
        }

        internal static bool Party(MobileParty mp)
        {
            try
            {
                if (mp == null) return false;
                if (mp.LeaderHero != null && Character(mp.LeaderHero.CharacterObject)) return true;
                var f = mp.MapFaction;
                if (f != null && f.Culture != null)
                {
                    var id = WwCultureId();
                    var cu = f.Culture.StringId ?? "";
                    if (id != null ? cu == id : (cu.Contains("walker") || cu.Contains("wight"))) return true;
                }
                return false;
            }
            catch { return false; }
        }
    }
}
