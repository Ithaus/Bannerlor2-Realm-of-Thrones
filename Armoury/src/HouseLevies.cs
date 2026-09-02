using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZACIAG RODOWY (Jeff 02.09: "tak samo jak na Polnocy i w Nocnej Strazy
    /// czasami pojawiaja sie rekruci elitarni" -> "szansa wszedzie taka sama,
    /// daj ta sama zasade"). W danych ROT z osad wychodzi TYLKO drzewo
    /// kultury (zwykle + szlacheckie); linie rodowe (Blackwood -> Ravens'
    /// Teeth, Karstark, Bolton, Lannister...) zyja w szablonach klanow: lord
    /// werbuje zwyklego rekruta, a ROT (ROTTroopRecruiter) podmienia mu go
    /// na czlowieka rodu - gracz jest z tego wylaczony. Nocna Straz rodow
    /// nie ma, wiec tam cale drzewo jest do zwerbowania.
    /// TA SAMA ZASADA CO DLA ELIT: sami nic nie losujemy. Gra (vanilla/BK)
    /// wystawia u notabli rekrutow linii SZLACHECKIEJ wedle wlasnych regul
    /// (moc notabla, relacje) - i to jest czestotliwosc "elit" znana z Polnocy.
    /// My tylko mowimy: na ziemi rodu z wlasnym szablonem szlachecki ochotnik
    /// jest czlowiekiem TEGO rodu tego samego tieru (ziemia Blackwoodow rodzi
    /// ludzi Blackwoodow). Zwykle sloty zostaja zwykle. Przeglad raz dziennie
    /// per osada + natychmiast, gdy gracz wjezdza do osady.
    /// </summary>
    internal sealed class HouseLevies : CampaignBehaviorBase
    {
        private readonly Dictionary<Clan, List<CharacterObject>> _trees = new Dictionary<Clan, List<CharacterObject>>();
        private readonly Dictionary<CharacterObject, bool> _eliteCache = new Dictionary<CharacterObject, bool>();
        private int _swappedToday;
        private int _lastLogDay = -1;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, st => Convert(st));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, (party, st, hero) =>
            {
                if (party != null && party == MobileParty.MainParty) Convert(st);
            });
        }

        public override void SyncData(IDataStore dataStore) { }

        private void Convert(Settlement settlement)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HouseLeviesEnabled) return;
                if (settlement == null || settlement.Notables == null) return;
                var owner = settlement.OwnerClan;
                if (owner == null || owner == Clan.PlayerClan || owner.DefaultPartyTemplate == null) return;
                var tree = TreeOf(owner);
                if (tree == null || tree.Count == 0) return;

                foreach (var notable in settlement.Notables)
                {
                    if (notable == null || notable.VolunteerTypes == null) continue;
                    var slots = notable.VolunteerTypes;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var troop = slots[i];
                        if (troop == null || troop.IsHero || tree.Contains(troop)) continue;
                        if (!IsElite(troop)) continue;                    // tylko szlacheckie sloty - ta sama zasada co elity
                        var repl = Pick(tree, troop.Tier);
                        if (repl == null || repl == troop) continue;
                        slots[i] = repl;
                        _swappedToday++;
                    }
                }

                int day = (int)CampaignTime.Now.ToDays;
                if (day != _lastLogDay)
                {
                    if (_swappedToday > 0)
                        Log.Info("HouseLevies: dzien " + day + " - " + _swappedToday + " szlacheckich ochotnikow na ziemiach rodow to teraz ludzie rodu.");
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
        private bool IsElite(CharacterObject troop)
        {
            bool v;
            if (_eliteCache.TryGetValue(troop, out v)) return v;
            v = false;
            try
            {
                var cult = troop.Culture;
                if (cult != null && cult.EliteBasicTroop != null)
                {
                    var stack = new Stack<CharacterObject>(); var seen = new HashSet<CharacterObject>();
                    stack.Push(cult.EliteBasicTroop);
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        if (c == null || !seen.Add(c)) continue;
                        if (c == troop) { v = true; break; }
                        if (c.UpgradeTargets != null) foreach (var u in c.UpgradeTargets) stack.Push(u);
                    }
                }
            }
            catch { }
            _eliteCache[troop] = v;
            return v;
        }

        /// <summary>Czlowiek rodu tego samego tieru (albo o jeden nizej/wyzej), losowo z pasujacych.</summary>
        private static CharacterObject Pick(List<CharacterObject> tree, int tier)
        {
            for (int spread = 0; spread <= 1; spread++)
            {
                var cands = new List<CharacterObject>();
                foreach (var c in tree)
                    if (!c.IsHero && Math.Abs(c.Tier - tier) == spread) cands.Add(c);
                if (cands.Count > 0) return cands[MBRandom.RandomInt(cands.Count)];
            }
            return null;
        }
    }
}
