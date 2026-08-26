using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.SaveSystem;

namespace RealisticCaptivity
{
    /// <summary>
    /// Rejestracja typow zapisu dla naszych SyncData. Bez tego silnik odmawia
    /// stworzenia save'a ("Cannot create save data") - kazda kombinacja kontenera
    /// musi byc zdefiniowana dokladnie RAZ w calym stacku.
    ///
    /// Dictionary&lt;string,int&gt; definiuje juz ROT (HeroRaceMapSaveableTypeDefiner),
    /// wiec definiujemy go tylko wtedy, gdy ROT-a nie ma - podwojna definicja
    /// wywala kolekcje typow rownie skutecznie jak jej brak.
    /// </summary>
    public class RcSaveDefiner : SaveableTypeDefiner
    {
        public RcSaveDefiner() : base(928371500) { }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, ItemRoster>));
            if (Type.GetType("ROT.CampaignBehaviors.HeroRaceMapSaveableTypeDefiner, ROT") == null)
                ConstructContainerDefinition(typeof(Dictionary<string, int>));
        }
    }
}
