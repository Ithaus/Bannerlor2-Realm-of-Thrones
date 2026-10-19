using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// WDZIECZNE WIOSKI (Jeff 01.09: "jak walcze z banda zbojow w bliskiej
    /// odleglosci od wioski, np. 50, to w promieniu 50 od wioski poprawiaja
    /// sie relacje o np. 2-3, bo pokonuje zbirow i ludzie sie ciesza").
    /// Po wygranej gracza z partia bandytow kazda wioska w zadanym promieniu
    /// od pola bitwy dorzuca relacje z KAZDYM swoim notablem. Wywolywane
    /// z ArmouryBehavior.OnMapEventEnded po stwierdzeniu wygranej.
    ///
    /// n20-relacje (Jeff 19.10: "jak zabije bandytow to pobliskie wioski daja mi + do relacji, czy mozemy zrobic to tak, zeby nie wyskakiwalo mi
    /// 30 powiadomien ... czy nie da sie to jakos zbiorczo i tak mi to nic nie mowi"). DOWOD: log Jeffa 19.10 14:41-16:09 - 6 wygranych z bandami,
    /// "+2 relacji z 15 / 36 / 16 / 32 / 11 / 11 notablami"; kazde ApplyPlayerRelation(..., showQuickNotification: true) to jedno okienko gry
    /// (SandBox DefaultNotificationsCampaignBehavior.OnRelationChanged -> MBInformationManager.AddQuickInformation z portretem notabla).
    /// Klucz BanditReliefRelationSummary (domyslnie wlaczony): ta sama relacja, ta sama droga gry (ChangeRelationAction.ApplyPlayerRelation - mnoznik
    /// Charm/perkow, XP Charm z CharacterRelationCampaignBehavior.OnHeroRelationChanged bez zmian), tylko showQuickNotification = false i JEDNO
    /// zbiorcze okienko: ile relacji, z iloma notablami, ktore wioski i po co to graczu (relacja z notablem otwiera wiecej ochotnikow -
    /// BKVolunteerModel.CalculateMaximumRecruitmentIndex: GetRelationImpact). Zysk liczony jak w grze: roznica relacji bazowej miedzy "efektywnymi"
    /// bohaterami (DiplomacyModel.GetHeroesForEffectiveRelation - te same, ktorym ApplyInternal zapisuje relacje) przed i po wywolaniu.
    /// Inne zrodla relacji (zadania, kryjowki, rozmowy, obrona wiesniakow w grze) bez zmian. Wylaczony klucz = okienko gry przy kazdym notablu, jak dotad.
    /// Wolane z watku glownego (MapEventEnded), poza goraca sciezka - zwykle listy na zdarzenie.
    /// </summary>
    internal static class BanditCheer
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // wioska z zyskiem do zbiorczego komunikatu
        private struct VillageGain { public string Name; public float Dist; public int Notables; }

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
                bool summary = s.BanditReliefRelationSummary;
                var me = Hero.MainHero;
                int gained = 0, villages = 0;
                // zbiorczo: notable z zyskiem, bez zmiany (relacja juz na szczycie albo mnoznik zaokraglil do 0), potkniecia per notabl
                int raised = 0, flat = 0, stumbles = 0, sum = 0, lo = int.MaxValue, hi = 0;
                List<VillageGain> list = summary ? new List<VillageGain>() : null;
                foreach (var v in Settlement.All)
                {
                    if (v == null || !v.IsVillage) continue;
                    float d = pos.Distance(v.GetPosition2D);
                    if (d > radius) continue;
                    bool any = false;
                    int vRaised = 0;
                    foreach (var notable in v.Notables)
                    {
                        if (notable == null || !notable.IsAlive) continue;
                        if (!summary)
                        {
                            ChangeRelationAction.ApplyPlayerRelation(notable, s.BanditCheerRelation, false, true);
                            gained++; any = true;
                            continue;
                        }
                        try
                        {
                            int before = BaseRelation(me, notable);
                            ChangeRelationAction.ApplyPlayerRelation(notable, s.BanditCheerRelation, false, false);
                            int after = BaseRelation(me, notable);
                            gained++; any = true;
                            int delta = after - before;
                            if (delta > 0)
                            {
                                raised++; vRaised++; sum += delta;
                                if (delta < lo) lo = delta;
                                if (delta > hi) hi = delta;
                            }
                            else flat++;
                        }
                        catch (Exception e)
                        {
                            // potkniecie jednego notabla nie gasi reszty wiosek (licznik w linii logu, pierwszy blad zdarzenia w logu bledow)
                            if (stumbles++ == 0) Log.Error("BanditCheer.AfterVictory(notabl " + SafeName(notable) + ")", e);
                        }
                    }
                    if (any) villages++;
                    if (summary && vRaised > 0)
                        list.Add(new VillageGain { Name = v.Name != null ? v.Name.ToString() : v.StringId, Dist = d, Notables = vRaised });
                }
                if (gained > 0)
                    Log.Info("BanditCheer: bandyci pokonani - " + villages + " wiosek w promieniu "
                             + ((int)radius) + " ucieszonych, +" + s.BanditCheerRelation
                             + " relacji z " + gained + " notablami.");

                if (!summary)
                {
                    if (gained > 0)
                        Log.Info("BanditCheer (zbiorczo): wylaczone (Bandit Relief Relation Summary) - gra pokazala " + gained + " osobnych okienek relacji.");
                    return;
                }
                if (raised <= 0)
                {
                    if (gained > 0 || stumbles > 0)
                        Log.Info("BanditCheer (zbiorczo): bez komunikatu - zaden z " + gained + " notabli nie zyskal (bez zmiany " + flat
                                 + ", potkniecia " + stumbles + ").");
                    return;
                }

                list.Sort((a, b) => a.Dist.CompareTo(b.Dist));
                string text = BuildText(list, raised, lo, hi);
                try
                {
                    MBInformationManager.AddQuickInformation(new TextObject("{=!}" + text), 0, null, null, "event:/ui/notification/relation");
                }
                catch (Exception e) { Log.Error("BanditCheer.AfterVictory(komunikat)", e); }
                Log.Info("BanditCheer (zbiorczo): 1 komunikat zamiast " + gained + " | wiosek z zyskiem " + list.Count + "/" + villages
                         + ", notabli z zyskiem " + raised + " (bez zmiany " + flat + ", potkniecia " + stumbles + "), relacja +" + lo
                         + (hi != lo ? "..+" + hi : "") + " na osobe, suma +" + sum + " | najblizsza " + list[0].Name + " ("
                         + list[0].Dist.ToString("0", Inv) + " j.) | wioski: " + VillageList(list) + " | \"" + text + "\"");
            }
            catch (Exception e) { Log.Error("BanditCheer.AfterVictory", e); }
        }

        // relacja bazowa miedzy tymi samymi bohaterami, ktorym ChangeRelationAction.ApplyInternal ja zapisuje (notabl z rodem -> glowa rodu)
        private static int BaseRelation(Hero me, Hero notable)
        {
            Hero e1, e2;
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(me, notable, out e1, out e2);
            if (e1 == null || e2 == null) return 0;
            return CharacterRelationManager.GetHeroRelation(e1, e2);
        }

        // "Villages around Oldstones are grateful you beat the bandits: relation +2 with 14 notables in 5 villages (Oldstones, Fairmarket and 3 more).
        //  Better relations let you recruit more of their men."
        private static string BuildText(List<VillageGain> list, int raised, int lo, int hi)
        {
            string gain = lo == hi ? "+" + lo : "+" + lo + " to +" + hi;
            string who = raised + " notable" + (raised > 1 ? "s" : "");
            string head;
            if (list.Count == 1)
                head = list[0].Name + " is grateful you beat the bandits: relation " + gain + " with " + who + ".";
            else
            {
                string names;
                if (list.Count == 2) names = list[0].Name + " and " + list[1].Name;
                else if (list.Count == 3) names = list[0].Name + ", " + list[1].Name + " and " + list[2].Name;
                else names = list[0].Name + ", " + list[1].Name + " and " + (list.Count - 2) + " more";
                head = "Villages around " + list[0].Name + " are grateful you beat the bandits: relation " + gain + " with " + who
                       + " in " + list.Count + " villages (" + names + ").";
            }
            return head + " Better relations let you recruit more of their men.";
        }

        // do linii logu: "Oldstones 4 (12 j.), Fairmarket 3 (20 j.) ..." - najwyzej 12 wiosek
        private static string VillageList(List<VillageGain> list)
        {
            var sb = new System.Text.StringBuilder();
            int n = Math.Min(list.Count, 12);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(list[i].Name).Append(' ').Append(list[i].Notables).Append(" (").Append(list[i].Dist.ToString("0", Inv)).Append(" j.)");
            }
            if (list.Count > n) sb.Append(" i ").Append(list.Count - n).Append(" wiecej");
            return sb.ToString();
        }

        private static string SafeName(Hero h)
        {
            try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; }
        }
    }
}
