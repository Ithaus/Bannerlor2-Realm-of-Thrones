using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace GrandTourney
{
    /// <summary>Ustawienia w MCM. Plik XML dziala dalej jako wartosci startowe.</summary>
    public class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        public override string Id => "GrandTourney";
        public override string DisplayName => "Grand Tourney";
        public override string FolderName => "GrandTourney";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enabled", HintText = "the whole system on or off (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public bool Enabled { get; set; } = true;

        [SettingPropertyInteger("Gather Days", 0, 16, "0", HintText = "days between the proclamation and the opening of the lists (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public int GatherDays { get; set; } = 4;

        [SettingPropertyInteger("Max Lords Invited", 0, 32, "0", HintText = "how many lords get a summons (16 places exist in all) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public int MaxLordsInvited { get; set; } = 8;

        [SettingPropertyFloatingInteger("Invite Radius", 0.00f, 800.00f, "0.00", HintText = "map units - how far the heralds ride (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public float InviteRadius { get; set; } = 200f;

        [SettingPropertyInteger("Min Lords To Hold", 0, 32, "0", HintText = "the rule of eight: a grand tourney opens only when at least this many lords have come; with fewer the lists stay a local affair with a modest prize (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public int MinLordsToHold { get; set; } = 8;

        [SettingPropertyBool("Police All Tournaments", HintText = "tourneys other mods start also answer to the rule of eight (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public bool PoliceAllTournaments { get; set; } = true;

        [SettingPropertyBool("Widen When Scarce", HintText = "in thin country the heralds ride further rather than cancel the tourney (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public bool WidenWhenScarce { get; set; } = true;

        [SettingPropertyFloatingInteger("Max Invite Radius", 0.00f, 2400.00f, "0.00", HintText = "but never past this - a herald is not a raven (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public float MaxInviteRadius { get; set; } = 600f;

        [SettingPropertyFloatingInteger("Widen Step", 0.00f, 480.00f, "0.00", HintText = "how much further they push on each attempt                     // fewer knights than this and the tourney is called off (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public float WidenStep { get; set; } = 120f;

        [SettingPropertyBool("Peace Only", HintText = "war strikes a tourney down only when it stands at the gates - siege, rebellion, or an enemy warband close by (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public bool PeaceOnly { get; set; } = true;

        [SettingPropertyFloatingInteger("War Danger Radius", 0.00f, 480.00f, "0.00", HintText = "how close an enemy lord's warband must ride for the war rule to cancel the tourney (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public float WarDangerRadius { get; set; } = 120f;

        [SettingPropertyBool("Summon Only Peaceful Lords", HintText = "heralds never summon a lord whose realm is at war - duty before games (stops tourneys hijacking war AI) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public bool SummonOnlyPeacefulLords { get; set; } = true;

        [SettingPropertyBool("Local When At War", HintText = "a realm at war holds LOCAL tourneys: no lords summoned, lists open at once, modest prize - the Grand affair waits for peace (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public bool LocalWhenAtWar { get; set; } = true;

        [SettingPropertyInteger("Local Prize Max Value", 0, 8000, "0", HintText = "the modest local prize is worth at most this much gold (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public int LocalPrizeMaxValue { get; set; } = 2000;

        [SettingPropertyFloatingInteger("Player Notice Radius", 0.00f, 600.00f, "0.00", HintText = "word of a tourney beyond this never reaches you (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The summons")]
        public float PlayerNoticeRadius { get; set; } = 150f;

        [SettingPropertyBool("Player Hosting Enabled", HintText = "you may proclaim a tourney of your own (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public bool PlayerHostingEnabled { get; set; } = true;

        [SettingPropertyInteger("Host Min Renown", 0, 1200, "0", HintText = "renown needed before anyone would answer your summons (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public int HostMinRenown { get; set; } = 300;

        [SettingPropertyInteger("Host Cooldown Days", 0, 1460, "0", HintText = "days before you may hold another (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public int HostCooldownDays { get; set; } = 365;

        [SettingPropertyInteger("Host Base Fee", 0, 8000, "0", HintText = "what it costs you to open the lists (not used while Historical Town Rates is on) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public int HostBaseFee { get; set; } = 2000;

        [SettingPropertyFloatingInteger("Host Fee Prosperity Factor", 0.00f, 2.00f, "0.00", HintText = "a richer town costs more to hire (not used while Historical Town Rates is on) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public float HostFeeProsperityFactor { get; set; } = 0.5f;

        [SettingPropertyInteger("Prize Modest", 0, 12000, "0", HintText = "a modest purse (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public int PrizeModest { get; set; } = 3000;

        [SettingPropertyInteger("Prize Worthy", 0, 32000, "0", HintText = "a purse worth riding for (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public int PrizeWorthy { get; set; } = 8000;

        [SettingPropertyInteger("Prize Princely", 0, 60000, "0", HintText = "a princely purse (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public int PrizePrincely { get; set; } = 15000;

        [SettingPropertyFloatingInteger("Prize Radius Bonus", 0.00f, 1.00f, "0.00", HintText = "every gold of prize widens the summons (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("Hosting your own")]
        public float PrizeRadiusBonus { get; set; } = 0.00008f;

        [SettingPropertyInteger("Host Renown Per Lord", 0, 32, "0", HintText = "renown you gain per lord who attends (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostRenownPerLord { get; set; } = 8;

        [SettingPropertyInteger("Host Influence Per Lord", 0, 10, "0", HintText = "influence you gain per lord who attends (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostInfluencePerLord { get; set; } = 2;

        [SettingPropertyInteger("Host Relation Per Lord", 0, 10, "0", HintText = "relation gained with each lord who attends (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostRelationPerLord { get; set; } = 2;

        [SettingPropertyInteger("Host Prosperity Per Lord", 0, 100, "0", HintText = "prosperity the town gains per lord (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostProsperityPerLord { get; set; } = 25;

        [SettingPropertyInteger("Host Loyalty Gain", 0, 20, "0", HintText = "loyalty the town gains for the spectacle (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostLoyaltyGain { get; set; } = 5;

        [SettingPropertyInteger("Host Security Loss", 0, 12, "0", HintText = "security the town loses to the crowds (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostSecurityLoss { get; set; } = 3;

        [SettingPropertyInteger("Host Takings Per Lord", 0, 800, "0", HintText = "your cut of the takings per lord (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostTakingsPerLord { get; set; } = 200;

        [SettingPropertyFloatingInteger("Host Takings Prosperity Factor", 0.00f, 1.00f, "0.00", HintText = "a richer town pays out more (not used while Historical Town Rates is on) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public float HostTakingsProsperityFactor { get; set; } = 0.1f;

        [SettingPropertyFloatingInteger("Cancelled Fee Refund", 0.00f, 2.00f, "0.00", HintText = "share of the fee returned if the tourney is called off (not used while Historical Town Rates is on) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public float CancelledFeeRefund { get; set; } = 0.5f;

        [SettingPropertyBool("Historical Town Rates", HintText = "a tourney's money goes where it should, as dear as the town is prosperous (the town wage level of Armoury): the hosting fee pays the town's carpenters, heralds and cooks (into its coffers), your purse waits for the champion and goes to him (all of it back to you if the tourney is called off), the takings come out of the town's coffers (off = the old way: fee and purse vanish, takings from nobody, half the purse back) (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public bool HistoricalTownRates { get; set; } = true;

        [SettingPropertyInteger("Host Fee Pence", 0, 17600, "0", HintText = "with historical town rates: what opening the lists costs in a middling town, pence (lists, stands, heralds, the feast - about 18 pounds), as dear as the town is prosperous (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostFeePence { get; set; } = 4400;

        [SettingPropertyInteger("Host Takings Base Pence", 0, 1920, "0", HintText = "with historical town rates: the takings from the crowds in a middling town, pence, on top of the takings per lord - both as dear as the town is prosperous (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("What the host gains")]
        public int HostTakingsBasePence { get; set; } = 480;

        [SettingPropertyBool("Nobles Fight In Tournaments", HintText = "lords present in town take their rightful places in the bracket - troops only fill what remains (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The lists themselves")]
        public bool NoblesFightInTournaments { get; set; } = true;

        [SettingPropertyBool("Nobles Ignore Skill Gate", HintText = "a lord enters even below the games' skill bar - pride before prudence (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The lists themselves")]
        public bool NoblesIgnoreSkillGate { get; set; } = true;

        [SettingPropertyInteger("Max Nobles In Bracket", 0, 60, "0", HintText = "at most this many of the sixteen places go to lords (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The lists themselves")]
        public int MaxNoblesInBracket { get; set; } = 15;

        [SettingPropertyBool("Tourney Ransom", HintText = "whoever loses a fight in the lists forfeits his own horse and armour to the victor, or buys them back at their worth in their present state (worn gear is cheaper); you, your companions (only when you ride in the lists yourself) and the AI knights alike - a common soldier fights in borrowed kit, ladies and townsfolk wager nothing, kin never ransom kin, a tourney called off costs nobody; an AI lord pays only from what his house can spare beyond its war chest, the victor wears what is better than his own, and the vanquished buys plain kit at the market (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The lists themselves")]
        public bool TourneyRansom { get; set; } = true;

        [SettingPropertyBool("Log Enabled", HintText = "write a log file in the module folder (takes effect after loading a save)", RequireRestart = false)]
        [SettingPropertyGroup("The lists themselves")]
        public bool LogEnabled { get; set; } = true;

        public void ApplyTo(Settings s)
        {
            s.Enabled = Enabled;
            s.GatherDays = GatherDays;
            s.MaxLordsInvited = MaxLordsInvited;
            s.InviteRadius = InviteRadius;
            s.MinLordsToHold = MinLordsToHold;
            s.PoliceAllTournaments = PoliceAllTournaments;
            s.WidenWhenScarce = WidenWhenScarce;
            s.MaxInviteRadius = MaxInviteRadius;
            s.WidenStep = WidenStep;
            s.PeaceOnly = PeaceOnly;
            s.WarDangerRadius = WarDangerRadius;
            s.SummonOnlyPeacefulLords = SummonOnlyPeacefulLords;
            s.LocalWhenAtWar = LocalWhenAtWar;
            s.LocalPrizeMaxValue = LocalPrizeMaxValue;
            s.PlayerNoticeRadius = PlayerNoticeRadius;
            s.PlayerHostingEnabled = PlayerHostingEnabled;
            s.HostMinRenown = HostMinRenown;
            s.HostCooldownDays = HostCooldownDays;
            s.HostBaseFee = HostBaseFee;
            s.HostFeeProsperityFactor = HostFeeProsperityFactor;
            s.PrizeModest = PrizeModest;
            s.PrizeWorthy = PrizeWorthy;
            s.PrizePrincely = PrizePrincely;
            s.PrizeRadiusBonus = PrizeRadiusBonus;
            s.HostRenownPerLord = HostRenownPerLord;
            s.HostInfluencePerLord = HostInfluencePerLord;
            s.HostRelationPerLord = HostRelationPerLord;
            s.HostProsperityPerLord = HostProsperityPerLord;
            s.HostLoyaltyGain = HostLoyaltyGain;
            s.HostSecurityLoss = HostSecurityLoss;
            s.HostTakingsPerLord = HostTakingsPerLord;
            s.HostTakingsProsperityFactor = HostTakingsProsperityFactor;
            s.CancelledFeeRefund = CancelledFeeRefund;
            s.HistoricalTownRates = HistoricalTownRates;
            s.HostFeePence = HostFeePence;
            s.HostTakingsBasePence = HostTakingsBasePence;
            s.NoblesFightInTournaments = NoblesFightInTournaments;
            s.NoblesIgnoreSkillGate = NoblesIgnoreSkillGate;
            s.MaxNoblesInBracket = MaxNoblesInBracket;
            s.TourneyRansom = TourneyRansom;
            s.LogEnabled = LogEnabled;
        }

        internal static void Apply()
        {
            try { var i = Instance; if (i != null) i.ApplyTo(Settings.Current); }
            catch (System.Exception e) { Log.Error("Mcm.Apply", e); }
        }
    }
}