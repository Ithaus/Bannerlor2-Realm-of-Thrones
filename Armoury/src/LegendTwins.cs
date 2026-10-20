using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m96-wzory po recenzjach (20.10, Jeff: "zobacz, czy nie ma wiecej takich bledow") - BLIZNIACZE CZESCI LEGEND (klucz LegendTwinPiecesHidden).
    ///
    /// DOWOD: ROT_crafting_pieces.xml ma do klingi kazdej legendy kopie "Dual" pod szablon OneHandedSwordDual (crafting_templates.xml), np.
    /// "valyrian_short_blade_duel" (tier 4, is_default=true, mesh sacwasere_short_blade_low - ta sama siatka co valyrian_short_blade z
    /// val_steel_sword_5/6/7), "darksister_blade_duel" (mesh dark_sister_blade), "needle_blade_duel", "oathkeeper_blade_duel"... Zaden przedmiot
    /// ROTassets.xml ich nie uzywa (grep: tylko crafting_templates.xml i weapon_descriptions.xml). LegendaryLaw.LockLegendPieces ukrywa wylacznie
    /// czesci z WeaponDesign legend (log 13-28-19: "129 dedykowanych czesci legend ukrytych"), wiec blizniaki zostaja. Gra: WeaponDesignVM bierze
    /// wszystkie szablony (CraftingTemplate.All), pokazuje czesc, gdy !IsHiddenOnDesigner, a CraftingCampaignBehavior.IsOpened zwraca true dla
    /// IsGivenByDefault - od dnia 1 mozna zlozyc miecz z klinga Dark Sister / stali valyrianskiej ze zwyklej stali (a wykuta bron dostaje
    /// wygenerowane id, ktorego ValyrianBlades nie rozpozna - stal valyrianska przestaje byc skonczonym zasobem). Przeglad danych: 21 blizniakow
    /// (19 domyslnie otwartych).
    ///
    /// REGULA: czesc, ktorej nie uzywa zaden przedmiot (ani zwykly, ani legenda), a ma TE SAMA SIATKE co dedykowana czesc legendy (uzywana przez
    /// legende i przez zaden zwykly przedmiot - te, ktore LockLegendPieces ukrywa), jest ukrywana jak sama czesc legendy. Czesc ze wspolna
    /// siatka zwyklej broni (uzywana przez zwykly przedmiot) zostaje - nie psujemy normalnego kucia. Raz na sesje (OnSessionLaunched, zaraz po
    /// LockLegendPieces), watek glowny; wylaczone = jak dotad.
    /// </summary>
    internal static class LegendTwins
    {
        internal static void Hide()
        {
            var s = Settings.Current;
            if (s == null || !s.LegendTwinPiecesHidden)
            {
                Log.Info("Czesci-blizniaki legend (m96-wzory): wylaczone (LegendTwinPiecesHidden NIE) - blizniacze klingi legend zostaja w projektowaniu broni jak dotad.");
                return;
            }
            var fHide = AccessTools.Field(typeof(CraftingPiece), "<IsHiddenOnDesigner>k__BackingField");
            if (fHide == null) { Log.Info("Czesci-blizniaki legend (m96-wzory): brak pola IsHiddenOnDesigner - nic nie ukryto."); return; }

            int stumbles = 0;
            var common = new HashSet<CraftingPiece>();
            var legend = new HashSet<CraftingPiece>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                try
                {
                    if (it == null || it.WeaponDesign == null || it.WeaponDesign.UsedPieces == null) continue;
                    var target = LegendaryLaw.IsLegend(it) ? legend : common;
                    foreach (var el in it.WeaponDesign.UsedPieces)
                        if (el != null && el.CraftingPiece != null) target.Add(el.CraftingPiece);
                }
                catch (Exception e) { stumbles++; if (stumbles <= 3) Log.Error("LegendTwins.items", e); }
            }

            // siatki dedykowanych (ukrytych) czesci legend
            var meshes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in legend)
                if (p != null && !common.Contains(p) && p.IsHiddenOnDesigner && !string.IsNullOrEmpty(p.MeshName)) meshes.Add(p.MeshName);

            int hidden = 0, openByDefault = 0;
            var ids = new List<string>();
            foreach (var p in MBObjectManager.Instance.GetObjectTypeList<CraftingPiece>())
            {
                try
                {
                    if (p == null || p.IsHiddenOnDesigner || common.Contains(p) || legend.Contains(p)) continue;
                    if (string.IsNullOrEmpty(p.MeshName) || !meshes.Contains(p.MeshName)) continue;
                    fHide.SetValue(p, true);
                    hidden++;
                    if (p.IsGivenByDefault) openByDefault++;
                    if (ids.Count < 30) ids.Add(p.StringId + (p.IsGivenByDefault ? " (domyslnie otwarta)" : ""));
                }
                catch (Exception e) { stumbles++; if (stumbles <= 3) Log.Error("LegendTwins.pieces", e); }
            }
            Log.Info("Czesci-blizniaki legend (m96-wzory): ukryte w projektowaniu broni " + hidden + " (domyslnie otwartych " + openByDefault + ") - ta sama siatka co dedykowana czesc legendy, "
                     + "zaden przedmiot ich nie uzywa: [" + string.Join(", ", ids.ToArray()) + (hidden > ids.Count ? ", ..." : "") + "] | siatek czesci legend " + meshes.Count
                     + ", czesci zwyklej broni " + common.Count + " | potkniecia " + stumbles + ".");
        }
    }
}
