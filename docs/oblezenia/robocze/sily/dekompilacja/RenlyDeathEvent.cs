using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ROT.Misc;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using psai.net;

namespace ROT.Events;

internal class RenlyDeathEvent : EventBase
{
	public RenlyDeathEvent()
	{
		Initialize();
	}

	public override void Initialize()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		Title = ((object)new TextObject("{=ROTgnVsyL6t}Influence of Melisandre", (Dictionary<string, object>)null)).ToString();
		UpperText = ((object)new TextObject("{=ROT8Nn1nRpF}A messenger approaches your party with a look of concern on his face. 'My lord,' he says, 'I have news of great import. It is said that Stannis Baratheon has turned to a Red Priestess of R'hllor for aid in his bid for the Iron Throne.The rumors say that she wields great power and has convinced Stannis to resort to dark magic to defeat his own brother, Renly Baratheon.'", (Dictionary<string, object>)null)).ToString();
		LowerText = ((object)new TextObject("{=ROTbGGUyME6}The messenger's words leave you with a sense of unease. You have to make a choice: will you attempt to intervene and put a stop to Stannis' actions, or will you sit back and let the events unfold? The outcome of this conflict may have far-reaching consequences for the Realm.", (Dictionary<string, object>)null)).ToString();
		CancelButtonEnabled = false;
		DoneButtonEnabled = true;
		CancelButtonText = "";
		DoneButtonText = ((object)new TextObject("{=ROTasSnYXW5}Continue...", (Dictionary<string, object>)null)).ToString();
		BackgroundImage = "event-bg";
		HeaderImage = "melisandre";
	}

	public override bool ConditionsMet()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (base.TotalEvents > 0)
		{
			return false;
		}
		if (CampaignTime.Now < Campaign.Current.Models.CampaignTimeModel.CampaignStartTime + CampaignTime.Days(120f))
		{
			return false;
		}
		if (MainCheck())
		{
			return true;
		}
		return false;
	}

	private bool MainCheck()
	{
		return ROTLords.StannisBaratheon.NotDeadOrWhiteWalker() && ROTLords.RenlyBaratheon.NotDeadOrWhiteWalker() && !ROTKingdoms.IsEliminated(ROTKingdoms.Dragonstone) && !ROTKingdoms.IsEliminated(ROTKingdoms.Stormlands);
	}

	public override void StartEvent()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		base.StartEvent();
		OpenPopup();
		PsaiCore.Instance.StopMusic(true);
		PsaiCore.Instance.PlaySegment(419);
	}

	public override void DailyTickEvent()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		CampaignTime eventStartTime = base.EventStartTime;
		if (!(((CampaignTime)(ref eventStartTime)).ElapsedDaysUntilNow < 14f))
		{
			if (MainCheck())
			{
				Title = ((object)new TextObject("{=renlydeath1}A Shadow's Deed", (Dictionary<string, object>)null)).ToString();
				UpperText = ((object)new TextObject("{=renlydeath2}A messenger approaches your party, his face pale and voice trembling. 'My lord, I bear dark tidings,' he stammers. 'Renly Baratheon has been slain in his camp by a shadowy wraith. The whispers claim that the wraith bore the visage of Stannis himself. In the wake of this tragedy, Renly's bannermen have pledged their fealty to Stannis, who now controls Storm's End.", (Dictionary<string, object>)null)).ToString();
				LowerText = ((object)new TextObject("{=renlydeath3}The turn of events leaves you pondering your next move. Stannis' newfound power could make him a valuable ally, or it could be used against you should his ambitions go unchecked. Will you hold Stannis accountable for his actions, or join forces with him to seize the opportunities this new alliance may offer?", (Dictionary<string, object>)null)).ToString();
				HeaderImage = "renly-slain";
				OpenPopup();
				ExecuteRenly();
				PsaiCore.Instance.StopMusic(true);
				PsaiCore.Instance.PlaySegment(420);
			}
			CompleteEvent();
		}
	}

	private void ExecuteRenly()
	{
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		if (ROTSettlements.StormsEnd.OwnerClan == ROTClans.BaratheonRenly)
		{
			ChangeOwnerOfSettlementAction.ApplyByDefault(ROTLords.StannisBaratheon, ROTSettlements.StormsEnd);
		}
		if (ROTSettlements.Dragonstone.OwnerClan == ROTClans.BaratheonRenly)
		{
			ChangeOwnerOfSettlementAction.ApplyByDefault(ROTLords.StannisBaratheon, ROTSettlements.Dragonstone);
		}
		DestroyClanAction.ApplyByClanLeaderDeath(ROTClans.BaratheonRenly);
		List<Clan> list = (List<Clan>)AccessTools.Field(typeof(CampaignObjectManager), "_clans").GetValue(Campaign.Current.CampaignObjectManager);
		list.Remove(ROTClans.BaratheonRenly);
		KillCharacterAction.ApplyByMurder(ROTLords.RenlyBaratheon, (Hero)null, true);
		if (ROTLords.CortnayPenrose.NotDeadOrWhiteWalker())
		{
			KillCharacterAction.ApplyByMurder(ROTLords.CortnayPenrose, (Hero)null, true);
		}
		if (!ROTClans.IsEliminated(ROTClans.Buckler) && ROTClans.Buckler.Kingdom == ROTKingdoms.Stormlands && !ROTKingdoms.IsEliminated(ROTKingdoms.Westerlands))
		{
			ChangeKingdomAction.ApplyByJoinToKingdomByDefection(ROTClans.Buckler, ROTClans.Buckler.Kingdom, ROTKingdoms.Westerlands, default(CampaignTime), true);
		}
		foreach (Clan item in ((IEnumerable<Clan>)ROTKingdoms.Stormlands.Clans).ToList())
		{
			if (item != ROTClans.BaratheonRenly && item != Clan.PlayerClan && item != ROTClans.Buckler)
			{
				ChangeKingdomAction.ApplyByJoinToKingdomByDefection(item, item.Kingdom, ROTKingdoms.Dragonstone, default(CampaignTime), true);
			}
		}
		if (Hero.MainHero.Clan.Kingdom == ROTKingdoms.Stormlands)
		{
			ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(Hero.MainHero.Clan, false);
		}
		if (!ROTKingdoms.IsEliminated(ROTKingdoms.Stormlands))
		{
			DestroyKingdomAction.Apply(ROTKingdoms.Stormlands);
		}
		ROTEvents.OnRenlysDeath();
	}

	public override bool ConditionsHold()
	{
		return ROTLords.StannisBaratheon != null && ROTLords.StannisBaratheon.NotDeadOrWhiteWalker() && ROTLords.RenlyBaratheon != null && ROTLords.RenlyBaratheon.NotDeadOrWhiteWalker();
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.1.0.9782' (yours is '9.1.0.7988')
