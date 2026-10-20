using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PROBA AUTOTESTU ZOLDU I ZAKUPOW BOHATEROW RODU (CW / CG; recenzja 19/20.10: "ostatni autotest nie przejdzie ani jednej glownej sciezki zmiany").
    /// DOWOD: log autotestu 19.10 16:43 (Modules/Armoury/Armoury-2026-10-19_16-43-11.log) - wszystkie linie "Zold: dzien" maja "w tym ludzie gracza 0",
    /// "Towarzysze (C): dzien ... gracz: zloto 72657" bez zmian przez 152 doby (zold druzyny gracza w zapisie autotest-F-d240 nie schodzi), w druzynie gracza
    /// brak towarzyszy, gracz czeka w miescie The Eyrie (CrashScribe "AUTOTEST: ETAP miasto: czekam w The Eyrie [town_EN1]").
    /// Klucz CompanionAutotestHire; dziala TYLKO w biegu autotestu CrashScribe (CrashScribe.Autotest.Active - pole tylko w DLL autotestu; w grze Jeffa nieczynne).
    ///  - pierwsza doba sesji: druzyna gracza bez bohatera rodu -> najem wolnego wedrowca tak jak rozmowa gry (LordConversationsCampaignBehavior :2030-2032:
    ///    GiveGoldAction gracz -> wedrowiec z ceny CompanionHiringPriceCalculationModel, AddCompanionAction, AddHeroToPartyAction) - najpierw z miasta gracza;
    ///  - co dobe: zold rodu gracza do bohaterow druzyny nie poszedl od wczoraj -> wyplata probna (CompanionWage.AutotestPay - ta sama droga co zold partii,
    ///    zloto z kiesy gracza do kies bohaterow; bez ksiegi zoldu, bo to nie zold rodu);
    ///  - zakupy: druzyna gracza stoi w miescie - CompanionGear "postoj" co dobe sam (prawdziwy zakup z polki miasta).
    /// Linia "Towarzysze (CW/CG) autotest:". Nic w zapisie poza tym, co zapisuje gra (towarzysz w rodzie gracza - tylko w zapisie autotestu). Watek gry.
    /// </summary>
    internal static class CompanionAutotest
    {
        private static int _at = -1;     // autotest CrashScribe: -1 nie sprawdzone, 0 nie, 1 tak (raz na proces)
        private static bool _tried;

        internal static void SessionStart() { _tried = false; }

        private static bool AutotestActive()
        {
            if (_at >= 0) return _at == 1;
            _at = 0;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    FieldInfo f = t != null ? t.GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(null)) _at = 1;
                    break;
                }
            }
            catch { _at = 0; }
            return _at == 1;
        }

        /// <summary>Bohater rodu gracza w druzynie gracza (poza graczem) - bez wzgledu na klucz CW.</summary>
        private static bool HasMember(MobileParty main)
        {
            var pc = Clan.PlayerClan;
            var r = main.MemberRoster;
            if (pc == null || r == null) return false;
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                if (ch == null || !ch.IsHero) continue;
                var h = ch.HeroObject;
                if (h != null && h != Hero.MainHero && h.Clan == pc && h.IsAlive) return true;
            }
            return false;
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick PRZED CompanionWage.Daily (linia doby CW widzi najem i wyplate probna).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.CompanionAutotestHire || Campaign.Current == null || !AutotestActive()) return;
            // przeglad calosci 20.10: bieg profilu (LogGate.Mode 2) mierzy gre jak u Jeffa - bez najmu i wyplat probnych; tylko zwykly autotest (1)
            if (LogGate.Mode != 1) return;
            var main = MobileParty.MainParty;
            if (main == null || Hero.MainHero == null || Clan.PlayerClan == null) return;
            string hire = "";
            if (!_tried)
            {
                _tried = true;
                hire = HasMember(main) ? "najem niepotrzebny (bohater rodu juz w druzynie)" : Hire(main);
            }
            int paid = 0;
            try { paid = CompanionWage.AutotestPay(main); }
            catch (Exception e) { Log.Error("CompanionAutotest.AutotestPay", e); }
            if (hire.Length == 0 && paid == 0) return;
            Log.Info("Towarzysze (CW/CG) autotest: dzien " + (int)CampaignTime.Now.ToDays + " | " + (hire.Length > 0 ? hire + " | " : "")
                     + "wyplata probna z kiesy gracza do kies bohaterow druzyny " + paid + " d (tylko gdy zold rodu gracza do nich nie poszedl od wczoraj)"
                     + " | gracz: zloto " + Hero.MainHero.Gold + ", miasto " + (main.CurrentSettlement != null ? main.CurrentSettlement.Name.ToString() : "-") + ".");
        }

        private static string Hire(MobileParty main)
        {
            try
            {
                Hero pick = null; bool here = false;
                var at = main.CurrentSettlement;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || !h.IsWanderer || h.Clan != null || h.CompanionOf != null || h.IsPrisoner || h.PartyBelongedTo != null) continue;
                    var st = h.CurrentSettlement;
                    if (st == null || !st.IsTown) continue;
                    if (h.BattleEquipment == null || ReferenceEquals(h.BattleEquipment, Campaign.Current.DeadBattleEquipment)) continue;
                    if (at != null && st == at) { pick = h; here = true; break; }
                    if (pick == null) pick = h;
                }
                if (pick == null) return "najem: brak wolnego wedrowca w miastach";
                int price = Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(pick);
                if (price < 0) price = 0;
                if (Hero.MainHero.Gold < price) return "najem: gracza nie stac (" + pick.Name + " za " + price + " d, zloto gracza " + Hero.MainHero.Gold + ")";
                int g0 = pick.Gold;
                Settlement from = pick.CurrentSettlement;
                // jak rozmowa gry (LordConversationsCampaignBehavior :2030-2032): zaplata gracz -> wedrowiec, rod gracza, druzyna gracza
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, pick, price, true);
                AddCompanionAction.Apply(Clan.PlayerClan, pick);
                AddHeroToPartyAction.Apply(pick, main, true);
                var s = Settings.Current;
                int reserve = (int)Math.Ceiling(Math.Max(0f, s != null ? s.CompanionGearReserveDays : 0f) * CompanionWage.DailyOf(pick.Level));
                return "najety " + pick.Name + " poz. " + pick.Level + " z " + (from != null ? from.Name.ToString() : "-") + (here ? " (miasto gracza)" : "") + " za " + price
                       + " d (kiesa " + g0 + " -> " + pick.Gold + ", zold z tabeli " + CompanionWage.DailyOf(pick.Level) + " d/dobe, rezerwa zakupow " + reserve + ")";
            }
            catch (Exception e) { Log.Error("CompanionAutotest.Hire", e); return "najem: wyjatek (ERROR w logu)"; }
        }
    }
}
