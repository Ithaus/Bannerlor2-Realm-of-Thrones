using System;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 139 (25.10, fala "AI walczy o zycie"; Jeff: "przeciwnik zachowuje sie jak prawdziwy zywy czlowiek, nie chce dac sie zabic ... dopiero ta zasada
    /// peka gdy jest panika, w panice jest zasada uciekaj i brak rozsadku ... Wtedy ginie najwiecej osob"). PRZELACZNIK TRYBU - jedna funkcja tylko do
    /// odczytu dla wszystkich paczek fali, zeby kazda pytala o to samo zjawisko tak samo (jedna zasada na jedno zjawisko):
    ///   PANIKA   = CommonAIComponent.IsPanicked || RoutHold zna czlowieka (zlamany raz na bitwe - takze gdy jego panike zgasil ktos inny) ||
    ///              CommonAIComponent.IsRetreating;
    ///   ROZSADEK = cala reszta, takze odwrot na rozkaz szyku (silnik: Agent.IsRunningAway bez odwrotu komponentu - RoutHold.Ordered) i odwrot gracza.
    /// Paczki rozsadku (tarcza pod ostrzalem, oszczedzanie sil, ostroznosc rannego, odwrot rannego, bohater) pomijaja PANIKE; paczki paniki (bez powrotu,
    /// bieg bez odsapki, poscig) dzialaja tylko na nia.
    /// Dowod (CommonAIComponent 1.4.8, dekompilacja panB/CommonAI.cs): Panic ustawia IsPanicked PRZED Mission.OnAgentPanicked; Retreat (z
    /// MissionAgentPanicHandler w nastepnym ticku) ustawia IsRetreating i EnforceShieldUsage(None); StopRetreating zeruje oba (RBM AgentAi.cs:651-657 -
    /// morale > 0 i 10 s bez ciosu wrecz; gra na koncu misji zwyciezcom). Odwrot z rozkazu szyku idzie natywnym Agent.Retreat - komponentu nie dotyka.
    /// Znane (RBMAI AgentAi.cs:826-859): ofiara szarzy konnej RBM (przewrocona albo odrzucona, bitwa polowa) dostaje CommonAIComponent.Retreat BEZ paniki -
    /// do StopRetreating RBM (ok. 10 s) przelacznik mowi PANIKA. Ilu ich jest, pokazuje spis w linii "LOS ZLAMANYCH (139)" ("sam odwrot komponentu").
    /// Wolac z glownego watku (slownik RoutKeeper). Bez zapisu stanu, bez Harmony; wyjatek = ROZSADEK (nic sie nie zmienia).
    /// 145: PanicAnyThread - ta sama definicja dla postfiksow z ticku asynchronicznego (zbior RoutHold z tablicy wedlug Agent.Index).
    /// </summary>
    internal static class SurvivalMode
    {
        /// <summary>Czlowiek jest w trybie PANIKA (patrz opis klasy). null = false.</summary>
        internal static bool InPanic(Agent a)
        {
            if (a == null) return false;
            try
            {
                var cai = a.CommonAIComponent;
                if (cai != null && (cai.IsPanicked || cai.IsRetreating)) return true;
                var k = RoutHold.Live;
                return k != null && k.Knows(a);
            }
            catch { return false; }
        }

        /// <summary>145: ta sama PANIKA dla postfiksow wolanych z dowolnego watku (VeteranAi -> WoundedWary): flagi komponentu wprost, zbior RoutHold przez
        /// tablice RoutHold.KnownAnyThread (pisana tylko na glownym watku, ta sama tresc co RoutKeeper.Knows). 0 = ROZSADEK, 1 = IsPanicked / IsRetreating,
        /// 2 = RoutHold zna czlowieka. Wyjatek = 0 (nic sie nie zmienia).</summary>
        internal static int PanicAnyThread(Agent a)
        {
            if (a == null) return 0;
            try
            {
                var cai = a.CommonAIComponent;
                if (cai != null && (cai.IsPanicked || cai.IsRetreating)) return 1;
                return RoutHold.KnownAnyThread(a) ? 2 : 0;
            }
            catch { return 0; }
        }
    }
}
