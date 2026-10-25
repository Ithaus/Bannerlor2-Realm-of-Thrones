using System;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 139 (25.10, fala "AI walczy o zycie"; Jeff: "przeciwnik zachowuje sie jak prawdziwy zywy czlowiek, nie chce dac sie zabic ... dopiero ta zasada
    /// peka gdy jest panika, w panice jest zasada uciekaj i brak rozsadku ... Wtedy ginie najwiecej osob"). PRZELACZNIK TRYBU - jedna funkcja tylko do
    /// odczytu dla wszystkich paczek fali, zeby kazda pytala o to samo zjawisko tak samo (jedna zasada na jedno zjawisko):
    ///   PANIKA   = CommonAIComponent.IsPanicked || RoutHold zna czlowieka (zlamany raz na bitwe - takze gdy jego panike zgasil ktos inny) ||
    ///              CommonAIComponent.IsRetreating BEZ rozkazu odwrotu szyku (szarza RBM);
    ///   ROZSADEK = cala reszta, takze odwrot na rozkaz szyku (OrderedRetreat: szyk z MovementOrder Retreat, odwrot komponentu bez IsPanicked), odwrot rannego 143
    ///              (natywny Agent.Retreat(pos) - komponentu nie dotyka) i odwrot gracza.
    /// Paczki rozsadku (tarcza pod ostrzalem, oszczedzanie sil, ostroznosc rannego, odwrot rannego, bohater) pomijaja PANIKE; paczki paniki (bez powrotu,
    /// bieg bez odsapki, poscig) dzialaja tylko na nia.
    /// Dowod (CommonAIComponent 1.4.8, dekompilacja panB/CommonAI.cs): Panic ustawia IsPanicked PRZED Mission.OnAgentPanicked; Retreat (z
    /// MissionAgentPanicHandler w nastepnym ticku) ustawia IsRetreating i EnforceShieldUsage(None); StopRetreating zeruje oba (RBM AgentAi.cs:651-657 -
    /// morale > 0 i 10 s bez ciosu wrecz; gra na koncu misji zwyciezcom).
    /// Recenzja 139 (25.10): odwrot z rozkazu szyku TEZ idzie przez komponent - MovementOrder.RetreatAux (MovementOrder.cs:448-460; dolaczajacy:
    /// OnUnitJoinOrLeave:764-768) wola rozszerzenie agent.Retreat(useCachingSystem) = AgentComponentExtensions.Retreat -> CommonAIComponent.Retreat
    /// (IsRetreating = true, EnforceShieldUsage(None), bez Panic i bez OnAgentFleeing - czlowiek zostaje w szyku). Dotad przelacznik mowil o nim PANIKA (147 pil mu
    /// oddech jak uciekajacemu, 148 mogl go gonic). Teraz: IsRetreating && !IsPanicked && szyk ma rozkaz Retreat = ROZSADEK (OrderedRetreat) - rozkaz wodza
    /// (gracza albo BehaviorRetreat / TacticCoordinatedRetreat AI) to decyzja, nie panika. Zbior RoutHold (Knows) i IsPanicked wygrywaja (zlamany zostaje PANIKA).
    /// Znane (RBMAI AgentAi.cs:826-859): ofiara szarzy konnej RBM (przewrocona albo odrzucona, bitwa polowa) dostaje CommonAIComponent.Retreat BEZ paniki -
    /// do StopRetreating RBM (ok. 10 s) przelacznik mowi PANIKA. Ilu ich jest, pokazuje spis w linii "LOS ZLAMANYCH (139)" ("sam odwrot komponentu").
    /// Wolac z glownego watku (slownik RoutKeeper). Bez zapisu stanu, bez Harmony; wyjatek = ROZSADEK (nic sie nie zmienia).
    /// 145: PanicAnyThread - ta sama definicja dla postfiksow z ticku asynchronicznego (zbior RoutHold z tablicy wedlug Agent.Index).
    /// </summary>
    internal static class SurvivalMode
    {
        /// <summary>Recenzja 139: odwrot na rozkaz szyku - odwrot komponentu bez paniki w szyku z rozkazem Retreat (MovementOrder.RetreatAux). Tylko odczyt pol
        /// (dowolny watek - postfiksy 145); null / wyjatek = false.</summary>
        internal static bool OrderedRetreat(Agent a, CommonAIComponent cai)
        {
            if (a == null || cai == null || !cai.IsRetreating || cai.IsPanicked) return false;
            try
            {
                var f = a.Formation;
                return f != null && f.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Retreat;
            }
            catch { return false; }
        }

        /// <summary>Czlowiek jest w trybie PANIKA (patrz opis klasy). null = false.</summary>
        internal static bool InPanic(Agent a)
        {
            if (a == null) return false;
            try
            {
                var cai = a.CommonAIComponent;
                if (cai != null && (cai.IsPanicked || (cai.IsRetreating && !OrderedRetreat(a, cai)))) return true;
                var k = RoutHold.Live;
                return k != null && k.Knows(a);
            }
            catch { return false; }
        }

        /// <summary>145: ta sama PANIKA dla postfiksow wolanych z dowolnego watku (VeteranAi -> WoundedWary): flagi komponentu wprost, zbior RoutHold przez
        /// tablice RoutHold.KnownAnyThread (pisana tylko na glownym watku, ta sama tresc co RoutKeeper.Knows). 0 = ROZSADEK, 1 = IsPanicked / IsRetreating,
        /// 2 = RoutHold zna czlowieka. Wyjatek = 0 (nic sie nie zmienia). Odwrot na rozkaz szyku (OrderedRetreat) = ROZSADEK jak w InPanic.</summary>
        internal static int PanicAnyThread(Agent a)
        {
            if (a == null) return 0;
            try
            {
                var cai = a.CommonAIComponent;
                if (cai != null && (cai.IsPanicked || (cai.IsRetreating && !OrderedRetreat(a, cai)))) return 1;
                return RoutHold.KnownAnyThread(a) ? 2 : 0;
            }
            catch { return 0; }
        }
    }
}
