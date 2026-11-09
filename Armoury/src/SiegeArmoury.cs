using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 375 (Jeff 09.11: "nie mam jak w zamku wejsc do armoury - dodaj w zamku, ze wchodze do armoury zamku obleganego") - ZBROJOWNIA W OBLEZONEJ TWIERDZY.
    /// W oblezonej twierdzy (zamek albo miasto), w ktorej gracz siedzi jako obronca, gra nie pokazuje menu "town"/"castle", tylko menu oblezenia
    /// (CampaignSystem GetGenericStateMenu): "encounter_interrupted_siege_preparations" (zwykle menu - gracz czekal w osadzie, gdy przyszlo oblezenie)
    /// albo "menu_siege_strategies" (MENU OCZEKIWANIA - AddWaitGameMenu). Dotad nie bylo stamtad drogi do zbrojowni:
    ///  - zbrojownia wojska DTE ("Manage Armory" -> "View Army Armory") jest tylko w menu "town" (DTE AddTownMenuOptions; zamek nigdy), a przycisk na
    ///    pasku mapy DTE dziala tylko bez spotkania (CanOpenArmory: PlayerEncounter.Current == null) - w oblezeniu spotkanie trwa;
    ///  - "Hand kit to the garrison" (GarrisonKit) jest tylko w menu "town"/"castle".
    /// Ta klasa dopisuje w OBU menu oblezenia (przed opcja wyjscia) dwie opcje, ktore otwieraja TE SAME okna co zwykle opcje:
    ///  - "View Army Armory": wprost DTE ArmyArmoryBehavior.OpenArmoryScreen (ta sama metoda co opcja "view_armory" DTE i przycisk paska mapy, z nasza
    ///    latka QuartermasterEscrow.HoldPrefix - kwatermistrz, ksiega, oplata ludziom jak w miescie; cena dla zamku - najblizsze miasto, jak przy pasku mapy);
    ///    warunek jak w DTE (zawsze) + DTE obecne;
    ///  - "Hand kit to the garrison": warunek i skutek GarrisonKit bez zmian (twoj rod jest panem, zaloga ma ludzi, DTE obecne, klucz Garrison Kit Menu).
    /// CTD (CLAUDE.md): z opcji menu oczekiwania NIE WOLNO wolac GameMenu.SwitchToMenu. Zwykla opcja DTE w miescie przelacza na podmenu
    /// "army_armory_submenu" (SwitchToMenu) - tu jej NIE powielamy: opcja tylko otwiera ekran ekwipunku (InventoryScreenHelper - nowy stan gry
    /// nad mapa), menu sie nie zmienia, po zamknieciu ekranu gracz wraca do tego samego menu oblezenia. Bez ExitToLast (niszczy MenuContext).
    /// Widoczne tylko gdy: klucz SiegeArmouryAccess, gracz w osadzie (MainParty.CurrentSettlement) bedacej oblezona twierdza, nie oblega jej
    /// (BesiegerCamp == null) i nie toczy sie bitwa (MapEvent gracza i osady null - wtedy gra i tak pokazuje menu "encounter").
    /// Zero zmian w logice zbrojowni. Bez stanu w zapisie, bez latek Harmony (tylko odczyt metody DTE). Potkniecia liczone, nic sie nie gasi.
    /// </summary>
    internal static class SiegeArmoury
    {
        private const string P = "SiegeArmoury (375): ";
        private static readonly string[] Menus = { "encounter_interrupted_siege_preparations", "menu_siege_strategies" };
        private static MethodInfo _open;
        private static int _stumbles, _opened;
        private static string _wiring = "nie wpiete";

        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeArmouryAccess; } }

        /// <summary>CampaignEvents.OnAfterSessionLaunchedEvent (ArmouryBehavior): opcje w menu oblezenia gry, przed "Leave". Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            var parts = new System.Collections.Generic.List<string>();
            try
            {
                _open = null;
                try
                {
                    var tBeh = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.ArmyArmoryBehavior");
                    _open = tBeh != null ? AccessTools.DeclaredMethod(tBeh, "OpenArmoryScreen") : null;
                }
                catch (Exception e) { Stumble("DTE OpenArmoryScreen", e); }
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                foreach (var id in Menus)
                {
                    try
                    {
                        var menu = mgr != null ? mgr.GetGameMenu(id) : null;
                        if (starter == null || menu == null) { parts.Add(id + " BRAK menu"); continue; }
                        int idx = -1, i = 0;
                        bool had = false;
                        foreach (var o in menu.MenuOptions)
                        {
                            if (o == null) { i++; continue; }
                            if (o.IdString == "arm_siege_armory_" + id) had = true;
                            if (o.IsLeave && idx < 0) idx = i;
                            i++;
                        }
                        if (had) { parts.Add(id + " juz byla"); continue; }
                        // ta sama pozycja dla obu: przed pierwsza opcja wyjscia (idx -1 = na koniec); kit zalogi wstawiony pierwszy, zbrojownia przed nim
                        starter.AddGameMenuOption(id, "arm_siege_garkit_" + id, "{=!}Hand kit to the garrison", KitCondition, KitConsequence, false, idx);
                        starter.AddGameMenuOption(id, "arm_siege_armory_" + id, "{=!}View Army Armory", ArmoryCondition, ArmoryConsequence, false, idx);
                        parts.Add(id + " TAK (pozycja " + idx + ")");
                    }
                    catch (Exception e) { parts.Add(id + " BLAD"); Stumble("AddMenus " + id, e); }
                }
            }
            catch (Exception e) { Stumble("AddMenus", e); }
            _wiring = string.Join(", ", parts.ToArray());
            try { Log.Info(P + "opcje zbrojowni w menu oblezenia: " + _wiring + "; DTE OpenArmoryScreen " + (_open != null ? "TAK" : "BRAK") + "; klucz Siege Armoury Access " + (On ? "TAK" : "NIE") + "."); } catch { }
        }

        /// <summary>Gracz siedzi w oblezonej twierdzy jako obronca (albo czekal w niej), bez trwajacej bitwy.</summary>
        private static Settlement BesiegedHere()
        {
            var main = MobileParty.MainParty;
            if (main == null || main.BesiegerCamp != null || main.MapEvent != null) return null;
            var st = main.CurrentSettlement;
            if (st == null || !st.IsFortification || st.SiegeEvent == null) return null;
            if (st.Party != null && st.Party.MapEvent != null) return null;
            return st;
        }

        private static bool ArmoryCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                if (!On || _open == null || BesiegedHere() == null) return false;
                args.Tooltip = new TextObject("{=!}The store of arms and armour your troops draw their kit from.");
                return true;
            }
            catch (Exception e) { Stumble("ArmoryCondition", e); return false; }
        }

        // opcja menu oczekiwania: TYLKO ekran (bez SwitchToMenu / ExitToLast) - patrz opis klasy
        private static void ArmoryConsequence(MenuCallbackArgs args)
        {
            try
            {
                if (_open == null) return;
                _opened++;
                Log.Info(P + "zbrojownia wojska otwarta w oblezonej " + Name(BesiegedHere()) + " (" + _opened + ". raz w sesji).");
                _open.Invoke(null, null);
            }
            catch (Exception e) { Stumble("ArmoryConsequence", e); }
        }

        private static bool KitCondition(MenuCallbackArgs args)
        {
            try
            {
                if (!On || BesiegedHere() == null) { args.optionLeaveType = GameMenuOption.LeaveType.Manage; return false; }
                return GarrisonKit.Condition(args);   // te same warunki i podpowiedz co w menu zamku/miasta (Settlement.CurrentSettlement = ta osada)
            }
            catch (Exception e) { Stumble("KitCondition", e); return false; }
        }

        private static void KitConsequence(MenuCallbackArgs args)
        {
            try
            {
                Log.Info(P + "kit dla zalogi otwarty w oblezonej " + Name(BesiegedHere()) + ".");
                GarrisonKit.Consequence(args);   // ekran ekwipunku, bez przelaczania menu
            }
            catch (Exception e) { Stumble("KitConsequence", e); }
        }

        private static string Name(Settlement st) { try { return st != null ? st.Name.ToString() : "?"; } catch { return "?"; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("SiegeArmoury." + where, e);
        }
    }
}
