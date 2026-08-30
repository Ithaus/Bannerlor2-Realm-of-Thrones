using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// JENIEC W LACHMANACH (Jeff 30.08: "ikony zostawiamy tak jak sa, ale
    /// po klikniecu w ikone pokazujemy ich w lachmanach - tylko w wersji
    /// jeniec"). DUZY podglad 3D w panelu druzyny to CharacterTableauWidget
    /// karmiony przez PartyVM.SelectedCharacter (HeroViewModel), ktorego
    /// wypelnia RefreshCurrentCharacterInformation z SZABLONU jednostki -
    /// dlatego obszukany do naga jeniec pozowal w pelnym rynsztunku.
    /// Postfix po tej metodzie podmienia SAM ekwipunek modelu (SetEquipment)
    /// na najtansze lachmany, wylacznie gdy klikniety wiersz to JENIEC-
    /// szeregowy. Ikony listy (PartyCharacterVM.Code) NIETKNIETE - pierwsza
    /// wersja patcha celowala wlasnie w nie i Jeff od razu to wylapal.
    /// Heurystyka lachmanow ta sama co GiveRags w RealisticCaptivity.
    /// Czysto wizualne - zadnego wplywu na walke i ekonomie.
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

        public static void Postfix(PartyVM __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveRagsPreview || !s.CaptiveSpoilsEnabled) return;
                var cur = __instance != null ? __instance.CurrentCharacter : null;
                if (cur == null || cur.Type != PartyScreenLogic.TroopType.Prisoner) return;
                if (cur.Character == null || cur.Character.IsHero) return;
                var model = __instance.SelectedCharacter;
                if (model == null) return;

                var eq = new Equipment();
                var rags = Rags();
                if (rags != null) eq[EquipmentIndex.Body] = new EquipmentElement(rags);
                model.SetEquipment(eq);
            }
            catch (Exception e) { Log.Error("CaptiveRags", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveRagsPreview) { Log.Info("CaptiveRags: wylaczone."); return; }
                var m = AccessTools.Method(typeof(PartyVM), "RefreshCurrentCharacterInformation");
                if (m == null) { Log.Info("CaptiveRags: brak PartyVM.RefreshCurrentCharacterInformation - patch spi."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(CaptiveRags), "Postfix"));
                Log.Info("CaptiveRags: jeniec w DUZYM podgladzie druzyny stoi w lachmanach (ikony listy nietkniete).");
            }
            catch (Exception e) { Log.Error("CaptiveRags.ApplyAll", e); }
        }
    }
}
