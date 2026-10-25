using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 131 (25.10, Jeff: "a kazdy punkt endurance daje +5 do udzwigu w menu inventory jako dodatkowe"): kazdy bohater w druzynie (gracz, towarzysze,
    /// lord i jego rodzina - kazda partia, ogolna zasada) dokłada EnduranceCarryKgPerPoint x Endurance do udzwigu partii; osobna linia "Endurance"
    /// w podpowiedzi udzwigu (ekran ekwipunku). Postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity (ten sam cel co CampCart i PackMarket,
    /// osobny wpis - tylko dodaje linie); armia (includeFollowers) - takze doczepione partie. Na ladzie i na morzu (ludzie niosa tak samo). Tylko odczyt.
    /// </summary>
    internal static class EnduranceCarry
    {
        private const string P = "Udzwig z Endurance (131): ";
        private static readonly TextObject _txt = new TextObject("{=!}Endurance");
        private static int _stum;

        /// <summary>__0 mobileParty, __6 includeFollowers (kolejnosc argumentow jak w CampCart.CapPostfix).</summary>
        public static void CapPostfix(MobileParty __0, bool __6, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null) return;
                var s = Settings.Current;
                if (s == null || s.EnduranceCarryKgPerPoint <= 0f) return;
                float kg = One(__0, s);
                if (__6)
                {
                    var att = __0.AttachedParties;
                    if (att != null) for (int i = 0; i < att.Count; i++) { var ap = att[i]; if (ap != null && ap != __0) kg += One(ap, s); }
                }
                if (kg > 0f) __result.Add(kg, _txt);
            }
            catch (Exception e) { if (_stum++ < 3) Log.Error("EnduranceCarry.CapPostfix", e); }
        }

        private static float One(MobileParty mp, Settings s)
        {
            var pb = mp.Party;
            var r = pb != null ? pb.MemberRoster : null;
            if (r == null) return 0f;
            float end = 0f;
            for (int i = 0; i < r.Count; i++)
            {
                var co = r.GetCharacterAtIndex(i);
                if (co == null || !co.IsHero) continue;
                var h = co.HeroObject;
                if (h == null || !h.IsAlive) continue;
                end += h.GetAttributeValue(DefaultCharacterAttributes.Endurance);
            }
            return end * s.EnduranceCarryKgPerPoint;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var cap = AccessTools.DeclaredMethod(typeof(DefaultInventoryCapacityModel), "CalculateInventoryCapacity");
                if (cap == null) { Log.Info(P + "BRAK CalculateInventoryCapacity - bez udzwigu z Endurance."); return; }
                h.Patch(cap, postfix: new HarmonyMethod(typeof(EnduranceCarry), nameof(CapPostfix)));
                var c = Settings.Current;
                Log.Info(P + "wpiete (postfiks CalculateInventoryCapacity) | " + (c != null ? c.EnduranceCarryKgPerPoint.ToString("0.#") : "?")
                         + " kg za punkt Endurance kazdego bohatera w druzynie (klucz EnduranceCarryKgPerPoint, 0 = wylaczone, na zywo).");
            }
            catch (Exception e) { Log.Error("EnduranceCarry.ApplyAll", e); }
        }
    }
}
