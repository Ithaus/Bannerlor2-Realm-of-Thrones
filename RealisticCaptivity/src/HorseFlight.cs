using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Library;
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
    ///
    /// 515 (perk 101 audytu perkow 14.11, Shipmaster.GhostShip): model jest dodawany przez AddModel&lt;T&gt;, wiec gra wpisuje mu poprzedni model
    /// (BaseModel) - u Jeffa NavalDLCTroopSacrificeModel. Dotad wszystko poza nasza regula szlo do DefaultTroopSacrificeModel (base), z pominieciem
    /// NavalDLC: na morzu ucieczka nie wymagala 2 statkow i nie kosztowala statkow (Default nie oddaje zadnego do poswiecenia), GhostShip (-50% szkod
    /// statkow przy ucieczce, mniej strat przy wejsciu / wyjsciu z oblezonego portu) nie dzialal. Teraz kazde "zasady gry" (na morzu zawsze - AllMounted
    /// zwraca tam false, nosze 501, piechota) oddaje poprzedniemu modelowi (Prev), a gdy go brak - Default jak dotad. Linia instalacji podaje poprzedni model.
    /// </summary>
    internal sealed class HorseFlightModel : DefaultTroopSacrificeModel
    {
        /// <summary>515: poprzedni model lancucha (NavalDLC przy wlaczonym DLC) - null = Default (base) jak dotad.</summary>
        private TroopSacrificeModel Prev
        {
            get { var b = BaseModel; return b != null && !ReferenceEquals(b, this) && !(b is HorseFlightModel) ? b : null; }
        }

        public override int BreakOutArmyLeaderRelationPenalty { get { var p = Prev; return p != null ? p.BreakOutArmyLeaderRelationPenalty : base.BreakOutArmyLeaderRelationPenalty; } }

        public override int BreakOutArmyMemberRelationPenalty { get { var p = Prev; return p != null ? p.BreakOutArmyMemberRelationPenalty : base.BreakOutArmyMemberRelationPenalty; } }

        public override ExplainedNumber GetLostTroopCountForBreakingInBesiegedSettlement(MobileParty party, SiegeEvent siegeEvent)
        {
            var p = Prev;
            return p != null ? p.GetLostTroopCountForBreakingInBesiegedSettlement(party, siegeEvent) : base.GetLostTroopCountForBreakingInBesiegedSettlement(party, siegeEvent);
        }

        public override ExplainedNumber GetLostTroopCountForBreakingOutOfBesiegedSettlement(MobileParty party, SiegeEvent siegeEvent, bool isBreakingOutFromPort)
        {
            var p = Prev;
            return p != null ? p.GetLostTroopCountForBreakingOutOfBesiegedSettlement(party, siegeEvent, isBreakingOutFromPort)
                             : base.GetLostTroopCountForBreakingOutOfBesiegedSettlement(party, siegeEvent, isBreakingOutFromPort);
        }

        public override void GetShipsToSacrificeForTryingToGetAway(BattleSideEnum playerBattleSide, MapEvent mapEvent, out MBList<Ship> shipsToCapture, out Ship shipToTakeDamage, out float damageToApplyForLastShip)
        {
            var p = Prev;
            if (p != null) p.GetShipsToSacrificeForTryingToGetAway(playerBattleSide, mapEvent, out shipsToCapture, out shipToTakeDamage, out damageToApplyForLastShip);
            else base.GetShipsToSacrificeForTryingToGetAway(playerBattleSide, mapEvent, out shipsToCapture, out shipToTakeDamage, out damageToApplyForLastShip);
        }

        private bool PrevCanGetAway(out TextObject explanation)
        {
            var p = Prev;
            return p != null ? p.CanPlayerGetAwayFromEncounter(out explanation) : base.CanPlayerGetAwayFromEncounter(out explanation);
        }

        private int PrevSacrificed(BattleSideEnum side, MapEvent mapEvent)
        {
            var p = Prev;
            return p != null ? p.GetNumberOfTroopsSacrificedForTryingToGetAway(side, mapEvent) : base.GetNumberOfTroopsSacrificedForTryingToGetAway(side, mapEvent);
        }
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
                    // 501 (Armoury, nosze): lezacy gracz nie odjezdza galopem - zasady gry (straz tylna); odmowa gry z czytelnym powodem
                    if (ArmouryBridge.PlayerOnLitter)
                    {
                        if (PrevCanGetAway(out explanation)) return true;   // 515: zasady poprzedniego modelu (NavalDLC), nie samego Default
                        explanation = new TextObject("{=!}You lie on a litter - you cannot gallop away, and you have too few men to leave a rear guard.");
                        return false;
                    }
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
            return PrevCanGetAway(out explanation);   // 515: na morzu NavalDLC (2 statki, GhostShip), na ladzie przez NavalDLC do Default - jak gra
        }

        public override int GetNumberOfTroopsSacrificedForTryingToGetAway(BattleSideEnum playerBattleSide, MapEvent mapEvent)
        {
            try
            {
                var c = Settings.Current;
                if (c != null && c.MountedEncounterFlight && AllMounted() && !ArmouryBridge.PlayerOnLitter)   // 501: na noszach - straz tylna gry
                {
                    int need = EnemyRiders(playerBattleSide, mapEvent);
                    if (need <= HealthyRegulars()) return need;   // 0 przed piechota
                }
            }
            catch { }
            return PrevSacrificed(playerBattleSide, mapEvent);   // 515: poprzedni model, nie sam Default
        }

        internal static void Install(IGameStarter starter)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MountedEncounterFlight) return;
                if (starter == null) return;
                var m = new HorseFlightModel();
                starter.AddModel(m);
                var prev = m.Prev;
                Log.Info("Ucieczka konno ze spotkania wlaczona (straz tylna tylko przeciw jezdzie wroga); poza nasza regula - poprzedni model (515): "
                         + (prev != null ? prev.GetType().FullName : "brak - DefaultTroopSacrificeModel jak dotad") + ".");
            }
            catch (Exception e) { Log.Error("HorseFlightModel.Install", e); }
        }
    }
}
