using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SPRZET OLBRZYMOW JEST DLA OLBRZYMOW (Jeff 14.09: "maczugi gigantow maja
    /// miec tylko giganci, ludzie nimi nie walcza - to debilnie wyglada; usun
    /// je wszystkim armiom, ze skladow, z magazynow, wszedzie"). ROT daje
    /// giant_club / giant_bow / giant_arrows czterem oddzialom rasy giant
    /// (ROT-Troops.xml) - a DTE rozdaje je z magazynu kazdemu, kto ma skill,
    /// i kazda bitwa wysypuje je do lupu. Regula patrzy na NOSZACEGO, nie na
    /// przedmiot: wolno go miec temu, kto nosi go z WLASNEGO szablonu (olbrzymy,
    /// olbrzymi notabl i wedrowiec ROT). Czlowiek - nigdy, gracz tez.
    /// Bron, amunicja i pancerze (giant_club, giant_bow, giant_arrows,
    /// giant_garb, giant_boots, giant_handwraps) - Jeff 14.09: "pancerze tez".
    /// </summary>
    internal static class GiantGear
    {
        private static readonly Dictionary<CharacterObject, bool> _mayWear = new Dictionary<CharacterObject, bool>();

        internal static bool Is(ItemObject it)
        {
            // bron, amunicja I PANCERZ olbrzymow (Jeff 14.09: "pancerze tez zrob")
            if (it == null || it.StringId == null || (!it.HasWeaponComponent && !it.HasArmorComponent)) return false;
            return it.StringId.StartsWith("giant_", StringComparison.Ordinal);
        }

        /// <summary>Wolno nosic, gdy WLASNY szablon jednostki ma sprzet olbrzymow.</summary>
        internal static bool MayWear(CharacterObject co)
        {
            if (co == null) return false;
            bool ok;
            if (_mayWear.TryGetValue(co, out ok)) return ok;
            ok = false;
            try
            {
                foreach (var eq in co.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int s = 0; s <= 9; s++)
                        if (Is(eq[(EquipmentIndex)s].Item)) { ok = true; break; }
                    if (ok) break;
                }
            }
            catch { }
            _mayWear[co] = ok;
            return ok;
        }

        /// <summary>Czy w partii sluzy ktos, komu sprzet olbrzymow sie nalezy.</summary>
        internal static bool PartyHasGiants(MobileParty mp)
        {
            try
            {
                var roster = mp != null ? mp.MemberRoster : null;
                if (roster == null) return false;
                for (int i = 0; i < roster.Count; i++)
                    if (MayWear(roster.GetCharacterAtIndex(i))) return true;
            }
            catch { }
            return false;
        }
    }
}
