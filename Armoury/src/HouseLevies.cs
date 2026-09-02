using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZACIAG RODOWY (Jeff 02.09: "tak samo jak na Polnocy i w Nocnej Strazy -
    /// w grze czasami pojawiaja sie rekruci elitarni"). W danych ROT z osad
    /// wychodzi TYLKO drzewo kultury (zwykle + szlacheckie); linie rodowe
    /// (Blackwood -> Ravens' Teeth, Karstark, Bolton, Lannister...) zyja
    /// wylacznie w szablonach klanow: lord werbuje zwyklego rekruta, a ROT
    /// (ROTTroopRecruiter) podmienia mu go na czlowieka rodu - gracz jest
    /// z tego wylaczony. Nocna Straz rodow nie ma, wiec tam cale drzewo
    /// jest do zwerbowania - stad wrazenie roznicy.
    /// Nasza zasada: raz dziennie, w osadzie NALEZACEJ do rodu z wlasnym
    /// szablonem, kazdy ochotnik u notabla, ktory jest zwyklym zolnierzem
    /// kultury, z szansa HouseLevyChancePercent zamienia sie w zolnierza
    /// TEGO rodu tego samego tieru i tej samej szlacheckosci. Zamieniony
    /// zostaje w slocie, az go zwerbujesz (vanilla dopelnia tylko puste
    /// sloty). Ziemia Blackwoodow rodzi ludzi Blackwoodow - czasem.
    /// </summary>
    internal sealed class HouseLevies : CampaignBehaviorBase
    {
        private readonly Dictionary<Clan, List<CharacterObject>> _trees = new Dictionary<Clan, List<CharacterObject>>();
        private int _swappedToday;
        private int _lastLogDay = -1;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailySettlement);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailySettlement(Settlement settlement)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HouseLeviesEnabled || s.HouseLevyChancePercent <= 0) return;
                if (settlement == null || settlement.Notables == null) return;
                var owner = settlement.OwnerClan;
                if (owner == null || owner == Clan.PlayerClan || owner.DefaultPartyTemplate == null) return;
                var tree = TreeOf(owner);
                if (tree == null || tree.Count == 0) return;
                float chance = Math.Min(100, s.HouseLevyChancePercent) / 100f;

                foreach (var notable in settlement.Notables)
                {
                    if (notable == null || notable.VolunteerTypes == null) continue;
                    var slots = notable.VolunteerTypes;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var troop = slots[i];
                        if (troop == null || troop.IsHero || tree.Contains(troop)) continue;
                        if (MBRandom.RandomFloat >= chance) continue;
                        bool elite = IsElite(troop);
                        var repl = Pick(tree, troop.Tier, elite) ?? Pick(tree, troop.Tier, !elite);
                        if (repl == null || repl == troop) continue;
                        slots[i] = repl;
                        _swappedToday++;
                    }
                }

                int day = (int)CampaignTime.Now.ToDays;
                if (day != _lastLogDay)
                {
                    if (_swappedToday > 0)
                        Log.Info("HouseLevies: dzien " + day + " - " + _swappedToday + " ochotnikow u notabli zamienionych na ludzi rodow (ziemie z wlasna linia).");
                    _lastLogDay = day; _swappedToday = 0;
                }
            }
            catch (Exception e) { Log.Error("HouseLevies", e); }
        }

        /// <summary>Cale drzewo szablonu rodu (stacki + wszystkie awanse), jak ROT TraverseTree.</summary>
        private List<CharacterObject> TreeOf(Clan clan)
        {
            List<CharacterObject> tree;
            if (_trees.TryGetValue(clan, out tree)) return tree;
            tree = new List<CharacterObject>();
            try
            {
                var tpl = clan.DefaultPartyTemplate;
                if (tpl != null && tpl.Stacks != null)
                {
                    var stack = new Stack<CharacterObject>();
                    foreach (var st in tpl.Stacks) if (st.Character != null) stack.Push(st.Character);
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        if (c == null || tree.Contains(c)) continue;
                        tree.Add(c);
                        if (c.UpgradeTargets != null) foreach (var u in c.UpgradeTargets) stack.Push(u);
                    }
                }
            }
            catch (Exception e) { Log.Error("HouseLevies.TreeOf", e); }
            _trees[clan] = tree;
            return tree;
        }

        /// <summary>Szlachecki = siedzi w drzewie elite_basic_troop swojej kultury (jak ROT IsEliteTroop).</summary>
        private static bool IsElite(CharacterObject troop)
        {
            try
            {
                var cult = troop.Culture;
                if (cult == null || cult.EliteBasicTroop == null) return false;
                var stack = new Stack<CharacterObject>(); var seen = new HashSet<CharacterObject>();
                stack.Push(cult.EliteBasicTroop);
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    if (c == null || !seen.Add(c)) continue;
                    if (c == troop) return true;
                    if (c.UpgradeTargets != null) foreach (var u in c.UpgradeTargets) stack.Push(u);
                }
            }
            catch { }
            return false;
        }

        private static CharacterObject Pick(List<CharacterObject> tree, int tier, bool elite)
        {
            for (int spread = 0; spread <= 1; spread++)
            {
                var cands = new List<CharacterObject>();
                foreach (var c in tree)
                    if (!c.IsHero && Math.Abs(c.Tier - tier) == spread && IsElite(c) == elite) cands.Add(c);
                if (cands.Count > 0) return cands[MBRandom.RandomInt(cands.Count)];
            }
            return null;
        }
    }
}
