using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// JENIEC W LACHMANACH (Jeff 30.08: "jency nie stoja w zbroi, tylko
    /// w lachmanach, bo tak ich przeciez obrobilismy - ikony zostawiamy,
    /// po klikniecu pokazujemy ich w lachmanach, tylko w wersji jeniec").
    /// Panel druzyny buduje podglad 3D jednostki z jej SZABLONU
    /// (PartyCharacterVM.GetCharacterCode -> character.Equipment), wiec
    /// obszukany do naga jeniec dalej pozowal w pelnym rynsztunku.
    /// Postfix podmienia KOD WYGLADU wylacznie dla wierszy typu Prisoner
    /// (szeregowi, nie bohaterowie): cialo i twarz zostaja, na grzbiecie
    /// najtansze lachmany (ta sama heurystyka co GiveRags w Realistic
    /// Captivity). Czysto wizualne - zadnego wplywu na walke i ekonomie.
    /// </summary>
    internal static class CaptiveRags
    {
        private static ItemObject _rags;
        private static bool _ragsSearched;

        private static ItemObject Rags()
        {
            if (_ragsSearched) return _rags;
            _ragsSearched = true;
            try
            {
                foreach (var item in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.ItemType != ItemObject.ItemTypeEnum.BodyArmor) continue;
                    if (item.Value <= 0 || item.StringId == null) continue;
                    var id = item.StringId.ToLower();
                    bool looksLikeRags = id.Contains("rag") || id.Contains("burlap") || id.Contains("sack")
                        || id.Contains("peasant") || id.Contains("beggar");
                    if (_rags == null || (looksLikeRags && item.Value < 60) || item.Value < _rags.Value)
                        if (looksLikeRags || _rags == null || item.Value < _rags.Value) _rags = item;
                }
            }
            catch { }
            return _rags;
        }

        public static void Postfix(CharacterObject character, PartyScreenLogic.TroopType type, ref CharacterCode __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveRagsPreview || !s.CaptiveSpoilsEnabled) return;
                if (type != PartyScreenLogic.TroopType.Prisoner) return;
                if (character == null || character.IsHero) return;

                var eq = new Equipment();
                var rags = Rags();
                if (rags != null) eq[EquipmentIndex.Body] = new EquipmentElement(rags);
                uint color = Color.White.ToUnsignedInteger();
                uint color2 = Color.White.ToUnsignedInteger();
                if (character.Culture != null)
                {
                    color = character.Culture.Color;
                    color2 = character.Culture.Color2;
                }
                __result = CharacterCode.CreateFrom(eq.CalculateEquipmentCode(),
                    character.GetBodyProperties(character.Equipment), character.IsFemale, character.IsHero,
                    color, color2, character.DefaultFormationClass, character.Race);
            }
            catch (Exception e) { Log.Error("CaptiveRags", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveRagsPreview) { Log.Info("CaptiveRags: wylaczone."); return; }
                var m = AccessTools.Method(typeof(PartyCharacterVM), "GetCharacterCode");
                if (m == null) { Log.Info("CaptiveRags: brak PartyCharacterVM.GetCharacterCode - patch spi."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(CaptiveRags), "Postfix"));
                Log.Info("CaptiveRags: jency w podgladzie druzyny stoja w lachmanach (tak, jak ich trzymamy).");
            }
            catch (Exception e) { Log.Error("CaptiveRags.ApplyAll", e); }
        }
    }
}
