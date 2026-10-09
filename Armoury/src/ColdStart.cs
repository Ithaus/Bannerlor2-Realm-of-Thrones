using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// DOROBEK STULECI (wpis 79; Jeff 05.10: "nie zaczynamy gry w prozni - minely setki lat produkcji, sprzet juz powinien byc
    /// wyprodukowany i na targach, a wyglada jakby wszystko bylo puste"; "A + B (14 dni)"). Raz na kampanie, tylko w nowej grze.
    ///
    /// A. Zbrojownie: DTE daje partii sprzet startowy przy jej utworzeniu (OnMobilePartyCreated), ale w nowej grze jego
    ///    OnNewGameCreated czysci PartyArmories PO utworzeniu partii startowych - wszyscy lordowie i garnizony zaczynali z pusta
    ///    zbrojownia, AiGear liczyl cale wojsko jako gole i pierwszego dnia wykupowal 5 296 sztuk z targow (sesja 02:55).
    ///    Teraz: kazda partia lorda AI i garnizon dostaje do zbrojowni brakujace sztuki kompletu swoich ludzi (to, co juz nosza).
    /// B. Zapas kupiecki: kazde miasto dostaje na polki ColdStartMarketDays dni pracy swoich rzemieslnikow (rece wedle dobrobytu
    ///    x udzialy cechow), sztuki kultury miasta, glownie nizszych tierow; roboczodni na sztuke z Needs (jak w warsztatach).
    /// </summary>
    internal static class ColdStart
    {
        private static bool _done;
        internal static void Reset() { _done = false; }
        internal static string Export() { return _done ? "done" : ""; }
        internal static void Import(string s) { _done = s == "done"; }

        internal static void Run()
        {
            if (_done) return;
            var s = Settings.Current;
            if (s == null || !s.ColdStartEnabled) return;
            _done = true;
            try
            {
                double age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays;
                if (age > 3.0) { Log.Info("ColdStart: kampania ma juz " + (int)age + " dni - dorobek stuleci pominiety (tylko nowa gra)."); return; }
            }
            catch { }
            try { Armories(); } catch (Exception e) { Log.Error("ColdStart.Armories", e); }
            try { Markets(); } catch (Exception e) { Log.Error("ColdStart.Markets", e); }
            try { int k = RecruitKit.SeedCampaignStart(); Log.Info("ColdStart: ochotnicy tieru 2+ w pulach notabli maja komplety (dorobek stuleci): " + k + "; zapisy sprzed startu zamienione na dorobek " + RecruitKit.SeedConverted + "."); } catch (Exception e) { Log.Error("ColdStart.RecruitKit", e); }
        }

        // ------------------------------------------------------------ A. zbrojownie
        private static void Armories()
        {
            var dict = AiGear.Armories();
            if (dict == null) { Log.Info("ColdStart: zbrojownie DTE niedostepne - pominiete."); return; }
            int parties = 0, pieces = 0, garrisons = 0;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || mp.IsMainParty || mp.MemberRoster == null) continue;
                bool garrison = mp.IsGarrison;
                if (!mp.IsLordParty && !garrison) continue;
                if (garrison && mp.CurrentSettlement != null && mp.CurrentSettlement.OwnerClan == Clan.PlayerClan) continue;
                int added = FillToTemplate(mp, dict);
                if (added > 0) { parties++; pieces += added; if (garrison) garrisons++; }
            }
            Log.Info("ColdStart: zbrojownie - " + parties + " partii (w tym garnizonow " + garrisons + ") dostalo " + pieces + " szt. kompletu swoich ludzi (dorobek stuleci, nie z targu).");
        }

        /// <summary>171 C9a: brakujace sztuki kompletu ludzi partii (wzorce, koszyki typ x tier jak AiGear, bez koni) do jej zbrojowni DTE;
        /// zwraca dolozone sztuki. Wydzielone z Armories() bez zmian - ColdStart i odtworzenie zalog po starym zapisie (GarrisonArmory).</summary>
        internal static int FillToTemplate(MobileParty mp)
        {
            var dict = AiGear.Armories();
            return dict != null && mp != null && mp.MemberRoster != null ? FillToTemplate(mp, dict) : 0;
        }

        private static int FillToTemplate(MobileParty mp, Dictionary<MBGUID, Dictionary<ItemObject, int>> dict)
        {
                // braki w koszykach typ x tier (jak AiGear)
                var need = new Dictionary<int, int>();
                var roster = mp.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    Equipment eq = null; try { eq = ch.Equipment; } catch { }
                    if (eq == null) continue;
                    for (int sl = 0; sl < 10; sl++)
                    {
                        var it = eq[(EquipmentIndex)sl].Item;
                        if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                        int k = AiGear.Bucket(it); int n; need.TryGetValue(k, out n); need[k] = n + el.Number;
                    }
                }
                Dictionary<ItemObject, int> arm;
                if (dict.TryGetValue(mp.Id, out arm) && arm != null)
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0) continue;
                        int k = AiGear.Bucket(kv.Key); int n; if (need.TryGetValue(k, out n)) need[k] = n - kv.Value;
                    }
                int added = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    Equipment eq = null; try { eq = ch.Equipment; } catch { }
                    if (eq == null) continue;
                    for (int sl = 0; sl < 10; sl++)
                    {
                        var it = eq[(EquipmentIndex)sl].Item;
                        if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                        int k = AiGear.Bucket(it); int d;
                        if (!need.TryGetValue(k, out d) || d <= 0) continue;
                        int n = Math.Min(d, el.Number);
                        if (AiGear.AddToArmory(mp, it, n)) { need[k] = d - n; added += n; }
                    }
                }
                return added;
        }

        // ------------------------------------------------------------ B. zapas kupiecki
        private static string GuildOfCategory(ItemObject it)
        {
            string id = it.ItemCategory != null ? it.ItemCategory.StringId : "";
            if (id == "garment") return "krawiec";
            if (id.EndsWith("_armor")) return "platnerz";
            if (id.StartsWith("melee_weapons")) return "miecznik";
            if (id == "arrows" || id.StartsWith("ranged_weapons")) return "lucznik";
            if (id.StartsWith("shield")) return "tarczownik";
            if (id.StartsWith("horse_equipment")) return "siodlarz";
            return null;
        }

        private static void Markets()
        {
            var s = Settings.Current;
            float days = Math.Max(0f, s.ColdStartMarketDays);
            if (days <= 0f) return;
            // pula wyrobow cechow: kultura -> cech -> tier -> sztuki
            var pool = new Dictionary<string, Dictionary<string, List<ItemObject>[]>>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || it.NotMerchandise || !SupplyDemand.Equipmentish(it)) continue;
                if (ArmsPricing.IsUnique(it) || LegendaryLaw.IsLegend(it) || WorkshopLaw.Forbidden(it)) continue;
                string g = GuildOfCategory(it); if (g == null) continue;
                string c = it.Culture == null || it.Culture.StringId == "neutral_culture" ? "" : it.Culture.StringId;
                Dictionary<string, List<ItemObject>[]> byG;
                if (!pool.TryGetValue(c, out byG)) pool[c] = byG = new Dictionary<string, List<ItemObject>[]>();
                List<ItemObject>[] byT;
                if (!byG.TryGetValue(g, out byT)) { byT = new List<ItemObject>[6]; for (int i = 0; i < 6; i++) byT[i] = new List<ItemObject>(); byG[g] = byT; }
                byT[Math.Max(0, Math.Min(5, (int)it.Tier))].Add(it);
            }
            float[] tierW = { 0.35f, 0.30f, 0.20f, 0.10f, 0.04f, 0.01f };   // tier 1..6: targ to glownie sprzet zwykly
            string[] guilds = { "krawiec", "platnerz", "miecznik", "siodlarz", "lucznik", "tarczownik" };
            float wsum = 0f; foreach (var g in guilds) wsum += WorkshopLaw.GuildWeight(g);
            if (wsum <= 0f) return;
            int towns = 0, pieces = 0; long worth = 0;
            var byType = new Dictionary<ItemObject.ItemTypeEnum, int>();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null || st.ItemRoster == null) continue;
                float hands = WorkshopLaw.TownHands(st.Town);
                string cul = st.Culture != null ? st.Culture.StringId : "";
                int made = 0;
                foreach (var g in guilds)
                {
                    float budget = days * hands * WorkshopLaw.GuildWeight(g) / wsum;   // roboczodni cechu
                    List<ItemObject>[] byT = null;
                    Dictionary<string, List<ItemObject>[]> byG;
                    if (pool.TryGetValue(cul, out byG)) byG.TryGetValue(g, out byT);
                    List<ItemObject>[] neutral = null;
                    if (pool.TryGetValue("", out byG)) byG.TryGetValue(g, out neutral);
                    int guard = 0;
                    while (budget > 0f && guard++ < 2000)
                    {
                        // tier wedle wag; sztuka kultury miasta, inaczej neutralna
                        float r = MBRandom.RandomFloat, acc = 0f; int t = 0;
                        for (; t < 5; t++) { acc += tierW[t]; if (r < acc) break; }
                        List<ItemObject> l = null;
                        for (int tt = t; tt >= 0 && (l == null || l.Count == 0); tt--)
                            l = byT != null && byT[tt].Count > 0 ? byT[tt] : neutral != null && neutral[tt].Count > 0 ? neutral[tt] : null;
                        if (l == null || l.Count == 0) break;
                        var it = l[MBRandom.RandomInt(l.Count)];
                        float d; var nd = WorkshopLaw.Needs(it, out d);
                        if (nd == null) d = 1f;
                        budget -= Math.Max(0.05f, d);
                        st.ItemRoster.AddToCounts(it, 1);
                        made++; worth += it.Value;
                        int n; byType.TryGetValue(it.ItemType, out n); byType[it.ItemType] = n + 1;
                    }
                }
                if (made > 0) { towns++; pieces += made; }
            }
            var parts = new List<string>(); foreach (var kv in byType) parts.Add(kv.Key + " " + kv.Value);
            Log.Info("ColdStart: zapas kupiecki - " + towns + " miast dostalo " + pieces + " szt. (" + days + " dni pracy rzemieslnikow, wartosc " + worth + " d) [" + string.Join(", ", parts.ToArray()) + "].");
        }
    }
}
