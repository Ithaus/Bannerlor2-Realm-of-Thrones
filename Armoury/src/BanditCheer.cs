using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// WDZIECZNE WIOSKI (Jeff 01.09: "jak walcze z banda zbojow w bliskiej
    /// odleglosci od wioski, np. 50, to w promieniu 50 od wioski poprawiaja
    /// sie relacje o np. 2-3, bo pokonuje zbirow i ludzie sie ciesza").
    /// Po wygranej gracza z partia bandytow kazda wioska w zadanym promieniu
    /// od pola bitwy dorzuca relacje z KAZDYM swoim notablem. Wywolywane
    /// z ArmouryBehavior.OnMapEventEnded po stwierdzeniu wygranej.
    /// </summary>
    internal static class BanditCheer
    {
        internal static void AfterVictory(MapEvent mapEvent)
        {
            try
            {
                var s = Settings.Current;
                if (!s.BanditCheerEnabled || s.BanditCheerRelation <= 0) return;

                // przeciwnik musi byc banda: ktoras partia strony pokonanej
                // nalezy do frakcji bandyckiej (ROT-owe bandy tez nia sa)
                bool bandits = false;
                var loser = mapEvent.PlayerSide == BattleSideEnum.Attacker
                    ? mapEvent.DefenderSide : mapEvent.AttackerSide;
                foreach (var p in loser.Parties)
                {
                    var f = p.Party != null ? p.Party.MapFaction : null;
                    if (f != null && f.IsBanditFaction) { bandits = true; break; }
                }
                if (!bandits) return;

                var pos = mapEvent.Position;
                float radius = s.BanditCheerRadius;
                int gained = 0, villages = 0;
                foreach (var v in Settlement.All)
                {
                    if (v == null || !v.IsVillage) continue;
                    if (pos.Distance(v.GetPosition2D) > radius) continue;
                    bool any = false;
                    foreach (var notable in v.Notables)
                    {
                        if (notable == null || !notable.IsAlive) continue;
                        ChangeRelationAction.ApplyPlayerRelation(notable, s.BanditCheerRelation, false, true);
                        gained++; any = true;
                    }
                    if (any) villages++;
                }
                if (gained > 0)
                    Log.Info("BanditCheer: bandyci pokonani - " + villages + " wiosek w promieniu "
                             + ((int)radius) + " ucieszonych, +" + s.BanditCheerRelation
                             + " relacji z " + gained + " notablami.");
            }
            catch (Exception e) { Log.Error("BanditCheer.AfterVictory", e); }
        }
    }
}
