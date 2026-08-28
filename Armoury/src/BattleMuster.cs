using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ODWOD PRZED BITWA. Jeff 28.08: "jak dolaczam do wspolnej bitwy, chce
    /// wybrac KIM bede walczyl - tak jak przy hideout". Opcja w menu potyczki
    /// otwiera ekran party: na LEWO odsylasz tych, ktorzy maja przeczekac.
    /// Trick bez grzebania w misji: odwod na czas bitwy liczy sie jako RANNI
    /// (gra sama nie wystawia rannych - ani na scenie, ani w symulacji),
    /// po bitwie wstaje zdrowy. Faktycznie rannych nie dotykamy - odejmujemy
    /// dokladnie tyle, ile sami dodalismy, z clampem do stanu rostera.
    /// </summary>
    internal sealed class BattleMuster : CampaignBehaviorBase
    {
        // characterId -> ilu zdrowych odeslalismy do odwodu (jako "rannych")
        private Dictionary<string, int> _benched = new Dictionary<string, int>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("armouryBattleMusterBenched", ref _benched);
        }

        private void OnSession(CampaignGameStarter starter)
        {
            // TYLKO dolaczanie do cudzej/wspolnej bitwy (Jeff: "jak sam walcze,
            // wystawiam wszystkich - po co wybierac; przy wspolnej jest duzo
            // wiecej wojska i nie wszyscy wchodza")
            try { AddOption(starter, "join_encounter"); } catch (Exception e) { Log.Error("BattleMuster.menu join_encounter", e); }
            // gra padla w trakcie bitwy - odwod wstaje przy wczytaniu
            try { RestoreBench("wczytanie zapisu"); } catch (Exception e) { Log.Error("BattleMuster.OnSession", e); }
        }

        private void AddOption(CampaignGameStarter starter, string menuId)
        {
            starter.AddGameMenuOption(menuId, "arm_muster_" + menuId,
                "Hand-pick who fights (the rest wait in reserve)",
                MusterCondition, MusterConsequence, false, 1, false);
        }

        private bool MusterCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            var r = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            return r != null && r.TotalManCount > 1;
        }

        private void MusterConsequence(MenuCallbackArgs args)
        {
            try
            {
                Helpers.PartyScreenHelper.OpenScreenWithCondition(
                    Transferable, DoneCondition, Done, null,
                    PartyScreenLogic.TransferState.Transferable,
                    PartyScreenLogic.TransferState.NotTransferable,
                    new TextObject("{=armMusterReserve}Reserve - they sit this one out", null),
                    MobileParty.MainParty.MemberRoster.TotalManCount,
                    false, false);
            }
            catch (Exception e) { Log.Error("BattleMuster.Consequence", e); }
        }

        private static bool Transferable(CharacterObject character, PartyScreenLogic.TroopType type,
            PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty)
        {
            return character != null && !character.IsPlayerCharacter;
        }

        private static Tuple<bool, TextObject> DoneCondition(TroopRoster l, TroopRoster lp,
            TroopRoster r, TroopRoster rp, int leftLimit, int rightLimit)
        {
            return new Tuple<bool, TextObject>(true, null);
        }

        private bool Done(TroopRoster leftMembers, TroopRoster leftPrison, TroopRoster rightMembers,
            TroopRoster rightPrison, FlattenedTroopRoster taken, FlattenedTroopRoster released,
            bool isForced, PartyBase leftParty, PartyBase rightParty)
        {
            try
            {
                RestoreBench("nowy wybor");   // stary odwod wstaje, liczymy od zera
                var roster = MobileParty.MainParty.MemberRoster;
                int total = 0;
                for (int i = 0; i < leftMembers.Count; i++)
                {
                    var el = leftMembers.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Number <= 0) continue;
                    if (el.Character.IsPlayerCharacter) continue;
                    int idx = roster.FindIndexOfTroop(el.Character);
                    if (idx < 0) continue;
                    var cur = roster.GetElementCopyAtIndex(idx);
                    int healthy = cur.Number - cur.WoundedNumber;
                    int bench = Math.Min(el.Number, healthy);
                    if (bench <= 0) continue;
                    roster.AddToCounts(el.Character, 0, false, bench);   // "ranny" na czas bitwy
                    var id = el.Character.StringId;
                    _benched[id] = (_benched.ContainsKey(id) ? _benched[id] : 0) + bench;
                    total += bench;
                }
                if (total > 0)
                    Log.Player("The reserve stands down - " + total + " men will sit this battle out.", true);
                return true;
            }
            catch (Exception e) { Log.Error("BattleMuster.Done", e); return true; }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                RestoreBench("bitwa skonczona");
            }
            catch (Exception e) { Log.Error("BattleMuster.OnMapEventEnded", e); }
        }

        private void RestoreBench(string why)
        {
            if (_benched == null || _benched.Count == 0) return;
            try
            {
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null) { _benched.Clear(); return; }
                int total = 0;
                foreach (var kv in _benched)
                {
                    var ch = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>(kv.Key);
                    if (ch == null) continue;
                    int idx = roster.FindIndexOfTroop(ch);
                    if (idx < 0) continue;
                    var cur = roster.GetElementCopyAtIndex(idx);
                    int heal = Math.Min(kv.Value, cur.WoundedNumber);
                    if (heal <= 0) continue;
                    roster.AddToCounts(ch, 0, false, -heal);
                    total += heal;
                }
                _benched.Clear();
                if (total > 0)
                {
                    Log.Info("BattleMuster: odwod wraca do szeregu (" + why + "): " + total + " ludzi.");
                    Log.Player("The reserve falls back in - " + total + " men return to the line.", true);
                }
            }
            catch (Exception e) { Log.Error("BattleMuster.RestoreBench", e); }
        }
    }
}
