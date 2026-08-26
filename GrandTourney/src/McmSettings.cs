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

        [SettingPropertyBool("Enabled", HintText = "the whole system on or off")]
        [SettingPropertyGroup("The summons")]
        public bool Enabled { get; set; } = true;

        [SettingPropertyInteger("Gather Days", 0, 16, "0", HintText = "days between the proclamation and the opening of the lists")]
        [SettingPropertyGroup("The summons")]
        public int GatherDays { get; set; } = 4;

        [SettingPropertyInteger("Max Lords Invited", 0, 32, "0", HintText = "how many lords get a summons (16 places exist in all)")]
        [SettingPropertyGroup("The summons")]
        public int MaxLordsInvited { get; set; } = 8;

        [SettingPropertyFloatingInteger("Invite Radius", 0.00f, 800.00f, "0.00", HintText = "map units - how far the heralds ride")]
        [SettingPropertyGroup("The summons")]
        public float InviteRadius { get; set; } = 200f;

        [SettingPropertyInteger("Min Lords To Hold", 0, 32, "0", HintText = "")]
        [SettingPropertyGroup("The summons")]
        public int MinLordsToHold { get; set; } = 8;

        [SettingPropertyBool("Police All Tournaments", HintText = "tourneys other mods start also answer to the rule of eight")]
        [SettingPropertyGroup("The summons")]
        public bool PoliceAllTournaments { get; set; } = true;

        [SettingPropertyBool("Widen When Scarce", HintText = "in thin country the heralds ride further rather than cancel the tourney")]
        [SettingPropertyGroup("The summons")]
        public bool WidenWhenScarce { get; set; } = true;

        [SettingPropertyFloatingInteger("Max Invite Radius", 0.00f, 2400.00f, "0.00", HintText = "but never past this - a herald is not a raven")]
        [SettingPropertyGroup("The summons")]
        public float MaxInviteRadius { get; set; } = 600f;

        [SettingPropertyFloatingInteger("Widen Step", 0.00f, 480.00f, "0.00", HintText = "how much further they push on each attempt                     // fewer knights than this and the tourney is called off")]
        [SettingPropertyGroup("The summons")]
        public float WidenStep { get; set; } = 120f;

        [SettingPropertyBool("Peace Only", HintText = "war strikes a tourney down only when it stands at the gates - siege, rebellion, or an enemy warband close by")]
        [SettingPropertyGroup("The summons")]
        public bool PeaceOnly { get; set; } = true;

        [SettingPropertyFloatingInteger("War Danger Radius", 0.00f, 480.00f, "0.00", HintText = "how close an enemy lord's warband must ride for the war rule to cancel the tourney")]
        [SettingPropertyGroup("The summons")]
        public float WarDangerRadius { get; set; } = 120f;

        [SettingPropertyBool("Summon Only Peaceful Lords", HintText = "heralds never summon a lord whose realm is at war - duty before games (stops tourneys hijacking war AI)")]
        [SettingPropertyGroup("The summons")]
        public bool SummonOnlyPeacefulLords { get; set; } = true;

        [SettingPropertyBool("Local When At War", HintText = "a realm at war holds LOCAL tourneys: no lords summoned, lists open at once, modest prize - the Grand affair waits for peace")]
        [SettingPropertyGroup("The summons")]
        public bool LocalWhenAtWar { get; set; } = true;

        [SettingPropertyInteger("Local Prize Max Value", 0, 8000, "0", HintText = "the modest local prize is worth at most this much gold")]
        [SettingPropertyGroup("The summons")]
        public int LocalPrizeMaxValue { get; set; } = 2000;

        [SettingPropertyFloatingInteger("Player Notice Radius", 0.00f, 600.00f, "0.00", HintText = "word of a tourney beyond this never reaches you")]
        [SettingPropertyGroup("The summons")]
        public float PlayerNoticeRadius { get; set; } = 150f;

        [SettingPropertyBool("Player Hosting Enabled", HintText = "you may proclaim a tourney of your own")]
        [SettingPropertyGroup("Hosting your own")]
        public bool PlayerHostingEnabled { get; set; } = true;

        [SettingPropertyInteger("Host Min Renown", 0, 1200, "0", HintText = "renown needed before anyone would answer your summons")]
        [SettingPropertyGroup("Hosting your own")]
        public int HostMinRenown { get; set; } = 300;

        [SettingPropertyInteger("Host Cooldown Days", 0, 1460, "0", HintText = "days before you may hold another")]
        [SettingPropertyGroup("Hosting your own")]
        public int HostCooldownDays { get; set; } = 365;

        [SettingPropertyInteger("Host Base Fee", 0, 8000, "0", HintText = "what it costs you to open the lists")]
        [SettingPropertyGroup("Hosting your own")]
        public int HostBaseFee { get; set; } = 2000;

        [SettingPropertyFloatingInteger("Host Fee Prosperity Factor", 0.00f, 2.00f, "0.00", HintText = "a richer town costs more to hire")]
        [SettingPropertyGroup("Hosting your own")]
        public float HostFeeProsperityFactor { get; set; } = 0.5f;

        [SettingPropertyInteger("Prize Modest", 0, 12000, "0", HintText = "a modest purse")]
        [SettingPropertyGroup("Hosting your own")]
        public int PrizeModest { get; set; } = 3000;

        [SettingPropertyInteger("Prize Worthy", 0, 32000, "0", HintText = "a purse worth riding for")]
        [SettingPropertyGroup("Hosting your own")]
        public int PrizeWorthy { get; set; } = 8000;

        [SettingPropertyInteger("Prize Princely", 0, 60000, "0", HintText = "a princely purse")]
        [SettingPropertyGroup("Hosting your own")]
        public int PrizePrincely { get; set; } = 15000;

        [SettingPropertyFloatingInteger("Prize Radius Bonus", 0.00f, 1.00f, "0.00", HintText = "every gold of prize widens the summons")]
        [SettingPropertyGroup("Hosting your own")]
        public float PrizeRadiusBonus { get; set; } = 0.00008f;

        [SettingPropertyInteger("Host Renown Per Lord", 0, 32, "0", HintText = "renown you gain per lord who attends")]
        [SettingPropertyGroup("What the host gains")]
        public int HostRenownPerLord { get; set; } = 8;

        [SettingPropertyInteger("Host Influence Per Lord", 0, 10, "0", HintText = "influence you gain per lord who attends")]
        [SettingPropertyGroup("What the host gains")]
        public int HostInfluencePerLord { get; set; } = 2;

        [SettingPropertyInteger("Host Relation Per Lord", 0, 10, "0", HintText = "relation gained with each lord who attends")]
        [SettingPropertyGroup("What the host gains")]
        public int HostRelationPerLord { get; set; } = 2;

        [SettingPropertyInteger("Host Prosperity Per Lord", 0, 100, "0", HintText = "prosperity the town gains per lord")]
        [SettingPropertyGroup("What the host gains")]
        public int HostProsperityPerLord { get; set; } = 25;

        [SettingPropertyInteger("Host Loyalty Gain", 0, 20, "0", HintText = "loyalty the town gains for the spectacle")]
        [SettingPropertyGroup("What the host gains")]
        public int HostLoyaltyGain { get; set; } = 5;

        [SettingPropertyInteger("Host Security Loss", 0, 12, "0", HintText = "security the town loses to the crowds")]
        [SettingPropertyGroup("What the host gains")]
        public int HostSecurityLoss { get; set; } = 3;

        [SettingPropertyInteger("Host Takings Per Lord", 0, 800, "0", HintText = "your cut of the takings per lord")]
        [SettingPropertyGroup("What the host gains")]
        public int HostTakingsPerLord { get; set; } = 200;

        [SettingPropertyFloatingInteger("Host Takings Prosperity Factor", 0.00f, 1.00f, "0.00", HintText = "a richer town pays out more")]
        [SettingPropertyGroup("What the host gains")]
        public float HostTakingsProsperityFactor { get; set; } = 0.1f;

        [SettingPropertyFloatingInteger("Cancelled Fee Refund", 0.00f, 2.00f, "0.00", HintText = "share of the fee returned if the tourney is called off")]
        [SettingPropertyGroup("What the host gains")]
        public float CancelledFeeRefund { get; set; } = 0.5f;

        [SettingPropertyBool("Nobles Fight In Tournaments", HintText = "lords present in town take their rightful places in the bracket - troops only fill what remains")]
        [SettingPropertyGroup("The lists themselves")]
        public bool NoblesFightInTournaments { get; set; } = true;

        [SettingPropertyBool("Nobles Ignore Skill Gate", HintText = "a lord enters even below the games' skill bar - pride before prudence")]
        [SettingPropertyGroup("The lists themselves")]
        public bool NoblesIgnoreSkillGate { get; set; } = true;

        [SettingPropertyInteger("Max Nobles In Bracket", 0, 60, "0", HintText = "at most this many of the sixteen places go to lords")]
        [SettingPropertyGroup("The lists themselves")]
        public int MaxNoblesInBracket { get; set; } = 15;

        [SettingPropertyBool("Log Enabled", HintText = "write a log file in the module folder")]
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
            s.NoblesFightInTournaments = NoblesFightInTournaments;
            s.NoblesIgnoreSkillGate = NoblesIgnoreSkillGate;
            s.MaxNoblesInBracket = MaxNoblesInBracket;
            s.LogEnabled = LogEnabled;
        }

        internal static void Apply()
        {
            try { var i = Instance; if (i != null) i.ApplyTo(Settings.Current); }
            catch (System.Exception e) { Log.Error("Mcm.Apply", e); }
        }
    }
}