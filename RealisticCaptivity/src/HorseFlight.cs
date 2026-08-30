using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace RealisticCaptivity
{
    /// <summary>
    /// KONNY MOZE UCIEC. Vanilla pozwala sprobowac ucieczki ze spotkania
    /// ("Try to get away") dopiero od DZIEWIECIU zdrowych szeregowych - bo cala
    /// mechanika opiera sie na zostawieniu strazy tylnej. Jezdziec z jednym
    /// pachotkiem slyszy "You don't have enough men!" i musi stanac do bitwy
    /// z pietnastoma zbojami (Jeff: "ucieklem z pola walki to czemu nie moge
    /// uciec, zostal zolnierz").
    ///
    /// Nasza zasada jest prostsza i uczciwsza: JESLI CALY ODDZIAL SIEDZI
    /// W SIODLE, wolno probowac ucieczki - a strazy tylnej potrzeba tylko
    /// tyle, ilu jest KONNYCH po drugiej stronie. Przed pieszymi lucznikami
    /// odjedziesz bez straty ludzi (juki i tak zostawiasz - to vanilla).
    /// Przed jazda bedziesz musial kogos poswiecic tak jak dotad, a jesli nie
    /// masz kogo - zostajesz i bijesz sie. Piechota bez koni: zasady vanilli.
    /// </summary>
    internal sealed class HorseFlightModel : DefaultTroopSacrificeModel
    {
        /// <summary>Ilu ludzi w oddziale gracza ma pod soba konia (wliczajac luzaki z jukow).</summary>
        private static bool AllMounted()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MemberRoster == null) return false;
                if (mp.IsCurrentlyAtSea) return false;
                int men = 0, riders = 0;
                for (int i = 0; i < mp.MemberRoster.Count; i++)
                {
                    var el = mp.MemberRoster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null) continue;
                    int healthy = el.Number - el.WoundedNumber;
                    if (healthy <= 0) continue;
                    men += healthy;
                    if (ch.IsMounted) riders += healthy;
                }
                if (men <= 0) return false;
                int spare = mp.ItemRoster != null ? mp.ItemRoster.NumberOfMounts : 0;
                riders += Math.Min(spare, men - riders);
                return riders >= men;
            }
            catch { return false; }
        }

        /// <summary>Ilu konnych stoi po drugiej stronie - tylu trzeba zatrzymac.</summary>
        private static int EnemyRiders(BattleSideEnum playerSide, MapEvent battle)
        {
            try
            {
                if (battle == null) return int.MaxValue;
                var side = battle.GetMapEventSide(playerSide.GetOppositeSide());
                if (side == null) return int.MaxValue;
                int riders = 0;
                foreach (var p in side.Parties)
                {
                    if (p == null || p.Party == null || p.Party.MemberRoster == null) continue;
                    var r = p.Party.MemberRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Character == null || el.Character.IsHero) continue;
                        if (!el.Character.IsMounted) continue;
                        int healthy = el.Number - el.WoundedNumber;
                        if (healthy > 0) riders += healthy;
                    }
                }
                return riders;
            }
            catch { return int.MaxValue; }
        }

        private static int HealthyRegulars()
        {
            try
            {
                var pb = PartyBase.MainParty;
                if (pb == null) return 0;
                return pb.NumberOfHealthyMembers - pb.MemberRoster.TotalHeroes;
            }
            catch { return 0; }
        }

        public override bool CanPlayerGetAwayFromEncounter(out TextObject explanation)
        {
            try
            {
                var c = Settings.Current;
                if (c != null && c.MountedEncounterFlight && AllMounted()
                    && PlayerEncounter.Current != null && PlayerEncounter.Battle != null)
                {
                    int need = EnemyRiders(PlayerEncounter.Current.PlayerSide, PlayerEncounter.Battle);
                    if (need <= HealthyRegulars())
                    {
                        explanation = TextObject.GetEmpty();
                        return true;
                    }
                    explanation = new TextObject("{=!}Their riders would run you down before you cleared the ridge.");
                    return false;
                }
            }
            catch { }
            return base.CanPlayerGetAwayFromEncounter(out explanation);
        }

        public override int GetNumberOfTroopsSacrificedForTryingToGetAway(BattleSideEnum playerBattleSide, MapEvent mapEvent)
        {
            try
            {
                var c = Settings.Current;
                if (c != null && c.MountedEncounterFlight && AllMounted())
                {
                    int need = EnemyRiders(playerBattleSide, mapEvent);
                    if (need <= HealthyRegulars()) return need;   // 0 przed piechota
                }
            }
            catch { }
            return base.GetNumberOfTroopsSacrificedForTryingToGetAway(playerBattleSide, mapEvent);
        }

        internal static void Install(IGameStarter starter)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MountedEncounterFlight) return;
                if (starter == null) return;
                starter.AddModel(new HorseFlightModel());
                Log.Info("Ucieczka konno ze spotkania wlaczona (straz tylna tylko przeciw jezdzie wroga).");
            }
            catch (Exception e) { Log.Error("HorseFlightModel.Install", e); }
        }
    }
}
