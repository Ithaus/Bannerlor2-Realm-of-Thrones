using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// C4 (Jeff 14.10 "tak samo jak pokonany lord AI") - ZBROJOWNIA GRACZA PO POJMANIU. Klucz PlayerArmoryLikeAiLord.
    ///
    /// Co traci pokonany lord AI (kod, nie zalozenie): gra 1.4.8 MapEvent.CaptureDefeatedPartyMembers zdejmuje z pokonanej druzyny wszystkich szeregowych
    /// (jency zwyciezcow), wodza bierze do niewoli, a pusta druzyne niszczy; DTE EveryoneCampaignBehavior.OnMapEventEnded -> DistributeLootRandomly oddaje
    /// zwyciezcom CALA zbrojownie pokonanej druzyny (PartyArmories, wedlug sily zwyciezcow), a OnMobilePartyDestroyed kasuje ja u pokonanego - zbrojownia
    /// idzie do zwyciezcow w calosci (konie: HorseFate LootMove - przeniesienie). Gracza DTE w tej drodze nie ma (jego zbrojownia to ArmyArmory.Armory,
    /// nie PartyArmories) - dotad po pojmaniu zostawala nietknieta. Teraz jedna regula: przy pojmaniu gracza cala jego zbrojownia DTE przechodzi do zdobywcy
    /// (RC: ten sam odbiorca co tabor) - druzyna lorda: jej zbrojownia DTE (stan sztuki w zapisie obitych AiWear, jak zakup ze stanem); osada (milicja,
    /// zaloga, loch): zbrojownia jej zalogi, bez zalogi - zapas osady; banda i inna partia: jej juki (prawo band OutlawLaw); sztuka, ktorej nikt nie przyjal,
    /// zostaje u gracza (nic nie znika). Reszta straty gracza (zloto przez lup bitwy, rynsztunek zostaje, tabor caly) - RealisticCaptivity PlayerLosesLikeAiLord.
    /// </summary>
    internal static class CaptiveArmory
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.PlayerArmoryLikeAiLord; } }

        private static int _events, _stumbles;
        private static long _toArmory, _toRoster, _kept, _horses;

        internal static void Reset() { _events = _stumbles = 0; _toArmory = _toRoster = _kept = _horses = 0; }

        /// <summary>Nasluch HeroPrisonerTaken (ArmouryBehavior): pojmany gracz - zbrojownia DTE do zdobywcy.</summary>
        internal static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (!On || prisoner == null || prisoner != Hero.MainHero || capturer == null) return;
            try
            {
                var arm = HorseFate.PlayerArmory();
                if (arm == null || arm.Count == 0) { Log.Info("Zbrojownia gracza po pojmaniu (C4): pusta albo brak DTE - nic do oddania."); return; }
                MobileParty mp = capturer.IsMobile ? capturer.MobileParty : null;
                MobileParty armTo = null;
                ItemRoster rosterTo = null;
                string toName;
                if (mp != null && mp.IsLordParty && !mp.IsMainParty) { armTo = mp; toName = "zbrojownia druzyny " + mp.Name; }
                else
                {
                    Settlement st = capturer.IsSettlement ? capturer.Settlement : (mp != null && (mp.IsGarrison || mp.IsMilitia) ? (mp.CurrentSettlement ?? mp.HomeSettlement) : null);
                    if (st != null)
                    {
                        armTo = st.Town != null ? st.Town.GarrisonParty : null;
                        rosterTo = st.ItemRoster;
                        toName = (armTo != null ? "zbrojownia zalogi " : "zapas osady ") + st.Name;
                    }
                    else if (mp != null) { rosterTo = mp.ItemRoster; toName = "juki partii " + mp.Name; }
                    else toName = "nikt";
                }
                var list = new List<ItemRosterElement>();
                for (int i = 0; i < arm.Count; i++)
                {
                    var el = arm.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && el.EquipmentElement.Item != null) list.Add(el);
                }
                long a = 0, r = 0, k = 0, h = 0;
                foreach (var el in list)
                {
                    var ee = el.EquipmentElement;
                    int n = el.Amount;
                    bool done = false;
                    if (armTo != null && AiGear.AddToArmory(armTo, ee.Item, n))
                    {
                        try { AiWear.NoteBought(armTo, ee, n); } catch { }   // stan sztuki (modyfikator) do zapisu obitych odbiorcy
                        a += n; done = true;
                    }
                    else if (rosterTo != null) { rosterTo.AddToCounts(ee, n); r += n; done = true; }
                    if (done)
                    {
                        arm.AddToCounts(ee, -n);
                        if (ee.Item.ItemType == ItemObject.ItemTypeEnum.Horse) h += n;
                    }
                    else k += n;
                }
                if (h > 0 && armTo != null) { try { HorseState.Refresh(armTo); } catch { } }
                _events++; _toArmory += a; _toRoster += r; _kept += k; _horses += h;
                Log.Info("Zbrojownia gracza po pojmaniu (C4, jak lord AI): " + (a + r) + " szt. do " + toName + " (do zbrojowni DTE " + a + ", do jukow / zapasu " + r
                         + ", w tym konie " + h + "), zostalo u gracza " + k + " (nikt nie przyjal) | w tej sesji pojman " + _events + ", oddane " + (_toArmory + _toRoster)
                         + " (zbrojownie " + _toArmory + ", juki / zapas " + _toRoster + ", konie " + _horses + "), zostalo " + _kept + ", potkniecia " + _stumbles + ".");
                if (a + r > 0)
                    Log.Player((PartyWords.NoTroops ? "Your armoury - " : "Your company's armoury - ") + (a + r) + " pieces - is your captors' now, as a beaten lord's armoury goes to his victors.", true);
            }
            catch (Exception e) { _stumbles++; Log.Error("CaptiveArmory.OnPrisonerTaken", e); }
        }
    }
}
