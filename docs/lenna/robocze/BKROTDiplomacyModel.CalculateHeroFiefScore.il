	// Methods
	.method public hidebysig virtual 
		instance valuetype [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.ExplainedNumber CalculateHeroFiefScore (
			class [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.Settlements.Settlement settlement,
			class [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.Hero annexing,
			[opt] bool explanations
		) cil managed 
	{
		.param [3] = bool(false)
		// Method begins at RVA 0x132cc
		// Header size: 12
		// Code size: 149 (0x95)
		.maxstack 4
		.locals init (
			[0] class [System]System.Diagnostics.Stopwatch,
			[1] valuetype [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.ExplainedNumber,
			[2] valuetype [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.ExplainedNumber,
			[3] class [BannerKings]BannerKings.Managers.Institutions.Religions.Religion
		)

		IL_0000: call class [System]System.Diagnostics.Stopwatch BKROTPatch.Diagnostics.BkrotPerf::Enter()
		IL_0005: stloc.0
		.try
		{
			IL_0006: ldstr "BKROTDiplomacyModel.CalculateHeroFiefScore"
			IL_000b: call bool BannerKings.Utils.GlobalRecursionGuard::TryEnter(string)
			IL_0010: brtrue.s IL_0021

			IL_0012: ldc.r4 0.0
			IL_0017: ldc.i4.0
			IL_0018: ldnull
			IL_0019: newobj instance void [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.ExplainedNumber::.ctor(float32, bool, class [TaleWorlds.Localization]TaleWorlds.Localization.TextObject)
			IL_001e: stloc.1
			IL_001f: leave.s IL_0093

			IL_0021: nop
			.try
			{
				IL_0022: ldarg.0
				IL_0023: ldarg.1
				IL_0024: ldarg.2
				IL_0025: ldarg.3
				IL_0026: callvirt instance valuetype [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.ExplainedNumber [BannerKings]BannerKings.Models.Vanilla.Abstract.DiplomacyModel::CalculateHeroFiefScore(class [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.Settlements.Settlement, class [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.Hero, bool)
				IL_002b: stloc.2
				IL_002c: call class [BannerKings]BannerKings.BannerKingsConfig [BannerKings]BannerKings.BannerKingsConfig::get_Instance()
				IL_0031: callvirt instance class [BannerKings]BannerKings.Managers.ReligionsManager [BannerKings]BannerKings.BannerKingsConfig::get_ReligionsManager()
				IL_0036: ldarg.2
				IL_0037: callvirt instance class [BannerKings]BannerKings.Managers.Institutions.Religions.Religion [BannerKings]BannerKings.Managers.ReligionsManager::GetHeroReligion(class [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.Hero)
				IL_003c: stloc.3
				IL_003d: ldloc.3
				IL_003e: brfalse.s IL_0078

				IL_0040: call class [BannerKings]BannerKings.BannerKingsConfig [BannerKings]BannerKings.BannerKingsConfig::get_Instance()
				IL_0045: callvirt instance class [BannerKings]BannerKings.Managers.ReligionsManager [BannerKings]BannerKings.BannerKingsConfig::get_ReligionsManager()
				IL_004a: ldarg.2
				IL_004b: call !0 class [BannerKings]BannerKings.DefaultTypeInitializer`2<class BKROTPatch.Divinities.ROTDivinities, class [BannerKings]BannerKings.Managers.Institutions.Religions.Divinity>::get_Instance()
				IL_0050: callvirt instance class [BannerKings]BannerKings.Managers.Institutions.Religions.Divinity BKROTPatch.Divinities.ROTDivinities::get_TheStranger()
				IL_0055: ldloc.3
				IL_0056: callvirt instance bool [BannerKings]BannerKings.Managers.ReligionsManager::HasBlessing(class [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.Hero, class [BannerKings]BannerKings.Managers.Institutions.Religions.Divinity, class [BannerKings]BannerKings.Managers.Institutions.Religions.Religion)
				IL_005b: brfalse.s IL_0078

				IL_005d: ldloca.s 2
				IL_005f: ldc.r4 0.2
				IL_0064: call !0 class [BannerKings]BannerKings.DefaultTypeInitializer`2<class BKROTPatch.Divinities.ROTDivinities, class [BannerKings]BannerKings.Managers.Institutions.Religions.Divinity>::get_Instance()
				IL_0069: callvirt instance class [BannerKings]BannerKings.Managers.Institutions.Religions.Divinity BKROTPatch.Divinities.ROTDivinities::get_TheStranger()
				IL_006e: callvirt instance class [TaleWorlds.Localization]TaleWorlds.Localization.TextObject [BannerKings]BannerKings.BannerKingsObject::get_Name()
				IL_0073: call instance void [TaleWorlds.CampaignSystem]TaleWorlds.CampaignSystem.ExplainedNumber::AddFactor(float32, class [TaleWorlds.Localization]TaleWorlds.Localization.TextObject)

				IL_0078: ldloc.2
				IL_0079: stloc.1
				IL_007a: leave.s IL_0093
			} // end .try
			finally
			{
				IL_007c: ldstr "BKROTDiplomacyModel.CalculateHeroFiefScore"
				IL_0081: call void BannerKings.Utils.GlobalRecursionGuard::Exit(string)
				IL_0086: endfinally
			} // end handler
		} // end .try
		finally
		{
			IL_0087: ldstr "BKROTModel.Diplomacy.CalculateHeroFiefScore"
			IL_008c: ldloc.0
			IL_008d: call void BKROTPatch.Diagnostics.BkrotPerf::Exit(string, class [System]System.Diagnostics.Stopwatch)
			IL_0092: endfinally
		} // end handler

		IL_0093: ldloc.1
		IL_0094: ret
	} // end of method BKROTDiplomacyModel::CalculateHeroFiefScore

