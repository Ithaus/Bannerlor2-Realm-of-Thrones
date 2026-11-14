using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GrandTourney
{
    /// <summary>
    /// 498 (Jeff 14.11: "drugorzedna powinna byc zawsze moja bron wrecz czyli jak nosze miecz dwureczny mojh to mam moj miecz dwureczny"):
    /// w turnieju bron "pierwszorzedna" zostaje taka, jak dala gra / RBM (lanca, luk, wlocznia, tarcza), a bron biala "drugorzedna" gracza
    /// to zawsze jego wlasna bron z ekwipunku bojowego (Hero.MainHero.BattleEquipment, z modyfikatorem).
    ///
    /// Dowod [K]: gra 1.4.8 SandBox TournamentFightMissionController.PrepareForMatch - kazdy uczestnik dostaje MatchEquipment = kopia (Clone)
    /// zestawu z szablonu kultury + ubranie; StartMatch wola PrepareForMatch PRZED SpawnTournamentParticipant (Equipment(participant.MatchEquipment)).
    /// RBMTournament (Harmony "com.rbmt", postfiks PrepareForMatch, priorytet domyslny 400, RBM laty na nowo w OnGameStart) podmienia w slotach
    /// 0-3 kazdemu (graczowi tez): jednoreczna (typ 2) na losowa kultury miasta i tieru gracza, dwureczna (typ 3) na losowa dwureczna / drzewce
    /// z ciosem, tarcze, luki, strzaly, kusze, belty. Tu postfiks z Priority.Last i HarmonyAfter("com.rbmt") - biegnie PO RBM.
    ///
    /// Regula (tylko gracz, tylko TournamentFightMissionController - arena treningowa to inny kontroler, nie ruszana):
    ///  - wlasna bron = bron biala ze slotow 0-3 BattleEquipment: najpierw dwureczna, potem jednoreczna, potem drzewce (recenzja 498);
    ///    w meczu konnym (kon w MatchEquipment) pomijana bron z RequiresNoMount - gdy nic nie zostaje, bron meczu bez zmian (linia w logu);
    ///    tylko bron wrecz (IsMeleeWeapon, cios albo pchniecie > 0), bez rzucanych, lukow, kusz, amunicji, tarcz; brak - nic (linia w logu);
    ///  - w MatchEquipment: pierwszy slot z jednoreczna / dwureczna -> wlasna bron (tarcza, drzewce, luk, kon zostaja; dwureczna przy tarczy
    ///    dozwolona); zestaw bez jednorecznej / dwurecznej (np. sama wlocznia z tarcza) -> wlasna bron do pierwszego pustego slotu 0-3;
    ///    wszystkie 4 sloty zajete bez jednorecznej / dwurecznej -> nic (linia w logu).
    /// MatchEquipment to kopia - prawdziwy BattleEquipment gracza tylko czytany. Wyjatek per mecz - licznik potkniec, nic nie gasimy.
    /// </summary>
    internal static class TourneyOwnWeapon
    {
        private const string P = "Wlasna bron (498): ";
        private const string RbmTourneyHarmonyId = "com.rbmt";
        private static bool _patched;
        private static int _stumbles;
        private static int _swaps;

        private static bool On { get { var s = Settings.Current; return s != null && s.Enabled && s.TourneyOwnMeleeWeapon; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { Log.Error("TourneyOwnWeapon." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            if (_patched || h == null) return;
            _patched = true;
            try
            {
                var t = AccessTools.TypeByName("SandBox.Tournaments.MissionLogics.TournamentFightMissionController");
                var m = t != null ? AccessTools.DeclaredMethod(t, "PrepareForMatch") : null;
                if (m == null) { Log.Info(P + "BRAK TournamentFightMissionController.PrepareForMatch - latka nie weszla, bron turnieju jak dotad."); return; }
                var post = new HarmonyMethod(AccessTools.Method(typeof(TourneyOwnWeapon), nameof(PrepareForMatchPostfix)),
                                             Priority.Last, null, new[] { RbmTourneyHarmonyId }, null);
                h.Patch(m, postfix: post);
                Log.Info(P + "latka 1/1 (TournamentFightMissionController.PrepareForMatch, postfiks Priority.Last po " + RbmTourneyHarmonyId
                         + "); klucz TourneyOwnMeleeWeapon=" + (On ? "TAK" : "NIE")
                         + " (przed MCM - stan z MCM w kazdej linii meczu).");
            }
            catch (Exception e) { Stumble("ApplyAll", e); }
        }

        /// <summary>Bron biala wrecz (bez rzucanych, dystansowych, amunicji, tarcz).</summary>
        private static bool IsMelee(ItemObject it)
        {
            if (it == null) return false;
            var t = it.ItemType;
            if (t != ItemObject.ItemTypeEnum.OneHandedWeapon && t != ItemObject.ItemTypeEnum.TwoHandedWeapon && t != ItemObject.ItemTypeEnum.Polearm) return false;
            var w = it.PrimaryWeapon;
            if (w == null || !w.IsMeleeWeapon || w.IsRangedWeapon || w.IsConsumable) return false;
            return w.SwingDamage > 0 || w.ThrustDamage > 0;
        }

        private static bool IsSidearmType(ItemObject it)
        {
            return it != null && (it.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || it.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon);
        }

        /// <summary>Bron, ktorej nie da sie uzyc z konia (ItemUsage z RequiresNoMount, np. czesc dwurecznych toporow i dlugich drzewcow).</summary>
        private static bool NoMount(ItemObject it)
        {
            var w = it != null ? it.PrimaryWeapon : null;
            if (w == null || string.IsNullOrEmpty(w.ItemUsage)) return false;
            return (MBItem.GetItemUsageSetFlags(w.ItemUsage) & ItemObject.ItemUsageSetFlags.RequiresNoMount) != 0;
        }

        private static readonly ItemObject.ItemTypeEnum[] Order =
        {
            ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.Polearm
        };

        /// <summary>Wlasna bron gracza (sloty 0-3): recenzja 498 - najpierw dwureczna (przyklad Jeffa: "jak nosze miecz dwureczny"),
        /// potem jednoreczna, potem drzewce. Mecz konny (mounted): pomijamy bron z RequiresNoMount. noMountSkipped - czy cos odrzucil kon.</summary>
        private static EquipmentElement OwnWeapon(Equipment eq, bool mounted, out bool noMountSkipped)
        {
            noMountSkipped = false;
            if (eq == null) return EquipmentElement.Invalid;
            foreach (var type in Order)
                for (int i = 0; i < 4; i++)
                {
                    var el = eq[(EquipmentIndex)i];
                    if (el.IsEmpty || el.Item.ItemType != type || !IsMelee(el.Item)) continue;
                    if (mounted && NoMount(el.Item)) { noMountSkipped = true; continue; }
                    return el;
                }
            return EquipmentElement.Invalid;
        }

        /// <summary>Kandydaci (bron biala gracza w slotach 0-3) do linii logu.</summary>
        private static string Candidates(Equipment eq)
        {
            var sb = new System.Text.StringBuilder();
            if (eq == null) return "";
            for (int i = 0; i < 4; i++)
            {
                var el = eq[(EquipmentIndex)i];
                if (el.IsEmpty || !IsMelee(el.Item)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Name(el));
            }
            return sb.ToString();
        }

        private static string Name(EquipmentElement el)
        {
            if (el.IsEmpty || el.Item == null) return "pusto";
            return el.Item.StringId + (el.ItemModifier != null ? "+" + el.ItemModifier.StringId : "");
        }

        /// <summary>Po RBM: MatchEquipment gracza - slot "drugorzednej" broni bialej dostaje wlasna bron gracza (przed spawnem agentow).</summary>
        public static void PrepareForMatchPostfix(TournamentMatch ____match)
        {
            try
            {
                if (!On || ____match == null) return;
                foreach (var p in ____match.Participants)
                {
                    try
                    {
                        if (p == null || p.Character == null || !p.Character.IsPlayerCharacter) continue;
                        var me = p.MatchEquipment;
                        var hero = Hero.MainHero;
                        if (me == null || hero == null) return;
                        bool mounted = !me[EquipmentIndex.Horse].IsEmpty;
                        bool noMountSkipped;
                        var own = OwnWeapon(hero.BattleEquipment, mounted, out noMountSkipped);
                        if (own.IsEmpty)
                        {
                            Log.Info(P + (noMountSkipped
                                ? "mecz konny, wlasna bron biala [" + Candidates(hero.BattleEquipment) + "] nie do uzycia z siodla - bron meczu bez zmian [" + Kit(me) + "]."
                                : "gracz nie ma wlasnej broni bialej w ekwipunku bojowym (sloty 0-3) - bron meczu bez zmian."));
                            return;
                        }
                        int slot = -1;
                        for (int i = 0; i < 4 && slot < 0; i++)
                        {
                            var el = me[(EquipmentIndex)i];
                            if (!el.IsEmpty && IsSidearmType(el.Item)) slot = i;
                        }
                        bool insert = false;
                        if (slot < 0)
                            for (int i = 0; i < 4 && slot < 0; i++)
                                if (me[(EquipmentIndex)i].IsEmpty) { slot = i; insert = true; }
                        if (slot < 0)
                        {
                            Log.Info(P + "zestaw meczu bez jednorecznej / dwurecznej i bez wolnego slotu 0-3 [" + Kit(me) + "] - wlasna "
                                     + Name(own) + " nie weszla.");
                            return;
                        }
                        var old = me[(EquipmentIndex)slot];
                        if (!old.IsEmpty && old.Item == own.Item && old.ItemModifier == own.ItemModifier)
                        {
                            Log.Info(P + "slot " + slot + " juz ma wlasna bron " + Name(own) + " - bez zmian.");
                            return;
                        }
                        me.AddEquipmentToSlotWithoutAgent((EquipmentIndex)slot, new EquipmentElement(own.Item, own.ItemModifier));
                        _swaps++;
                        Log.Info(P + "mecz gracza, slot " + slot + ": " + (insert ? "pusty -> wstawiona " : Name(old) + " -> ") + Name(own)
                                 + " | wybrana z [" + Candidates(hero.BattleEquipment) + "]" + (mounted ? ", mecz konny" : "")
                                 + (noMountSkipped ? " (pominieta bron nie do uzycia z siodla)" : "")
                                 + " | zestaw meczu po zmianie [" + Kit(me) + "] | podmian w sesji " + _swaps + ", potkniec " + _stumbles
                                 + ", klucz TourneyOwnMeleeWeapon=TAK.");
                        return;
                    }
                    catch (Exception e) { Stumble("PrepareForMatch.p", e); }
                }
            }
            catch (Exception e) { Stumble("PrepareForMatch", e); }
        }

        private static string Kit(Equipment me)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(i).Append(':').Append(Name(me[(EquipmentIndex)i]));
            }
            var hs = me[EquipmentIndex.Horse];
            if (!hs.IsEmpty) sb.Append(", kon");
            return sb.ToString();
        }
    }
}
