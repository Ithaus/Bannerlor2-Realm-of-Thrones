using System;
using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace Armoury
{
    /// <summary>
    /// Rejestracja typow zapisu dla naszych SyncData. Armoury zapisuje
    /// Dictionary&lt;string,int&gt; (arm_player_stock) i dotad wisial na cudzej
    /// definicji: ROT (HeroRaceMapSaveableTypeDefiner) albo RealisticCaptivity
    /// (RcSaveDefiner, gdy ROT-a brak). Wylaczenie obu = "Cannot create save
    /// data" w Armoury. Definiujemy kontener sami - ale TYLKO gdy nie zrobil
    /// tego nikt przed nami, bo podwojna definicja wywala kolekcje typow
    /// rownie skutecznie jak jej brak (patrz RcSaveDefiner).
    /// </summary>
    public class ArmSaveDefiner : SaveableTypeDefiner
    {
        public ArmSaveDefiner() : base(928371600) { }

        protected override void DefineContainerDefinitions()
        {
            bool rot = Type.GetType("ROT.CampaignBehaviors.HeroRaceMapSaveableTypeDefiner, ROT") != null;
            bool rc = Type.GetType("RealisticCaptivity.RcSaveDefiner, RealisticCaptivity") != null;
            if (!rot && !rc)
                ConstructContainerDefinition(typeof(Dictionary<string, int>));
        }
    }
}
