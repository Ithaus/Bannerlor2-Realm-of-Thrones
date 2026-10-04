using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// Platnosci gracza za uslugi w osadzie (kowal, kuznia, naprawy, zamowienia) idą do KASY OSADY,
    /// a nie w nicosc (audyt dziur C2, 04.10). Poza osada - jak dotad (w nicosc nie ma komu).
    /// </summary>
    internal static class Pay
    {
        internal static void ToSettlement(int amount)
        {
            if (amount <= 0) return;
            var st = Settlement.CurrentSettlement;
            if (st != null && st.SettlementComponent != null) GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, amount, true);
            else GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, true);
        }
    }
}
