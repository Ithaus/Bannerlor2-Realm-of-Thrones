using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace GrandTourney
{
    public class Settings
    {
        public static Settings Current = new Settings();

        // --- The summons ---
        public bool Enabled = true;                        // the whole system on or off
        public int GatherDays = 4;                         // days between the proclamation and the opening of the lists
        public int MaxLordsInvited = 8;                    // how many lords get a summons (16 places exist in all)
        public float InviteRadius = 200f;                  // map units - how far the heralds ride
        public int MinLordsToHold = 8;
        public bool PoliceAllTournaments = true;           // tourneys other mods start also answer to the rule of eight
        public bool WidenWhenScarce = true;                // in thin country the heralds ride further rather than cancel the tourney
        public float MaxInviteRadius = 600f;               // but never past this - a herald is not a raven
        public float WidenStep = 120f;                     // how much further they push on each attempt                     // fewer knights than this and the tourney is called off
        public bool PeaceOnly = true;                      // war strikes a tourney down only when it stands at the gates - siege, rebellion, or an enemy warband close by
        public float WarDangerRadius = 120f;               // how close an enemy lord's warband must ride for the war rule to cancel the tourney
        public bool SummonOnlyPeacefulLords = true;        // heralds never summon a lord whose realm is at war - duty before games (stops tourneys hijacking war AI)
        public bool LocalWhenAtWar = true;                 // a realm at war holds LOCAL tourneys: no lords summoned, lists open at once, modest prize - the Grand affair waits for peace
        public int LocalPrizeMaxValue = 2000;              // the modest local prize is worth at most this much gold
        public float PlayerNoticeRadius = 150f;            // word of a tourney beyond this never reaches you

        // --- Hosting your own ---
        public bool PlayerHostingEnabled = true;           // you may proclaim a tourney of your own
        public int HostMinRenown = 300;                    // renown needed before anyone would answer your summons
        public int HostCooldownDays = 365;                 // days before you may hold another
        public int HostBaseFee = 2000;                     // what it costs you to open the lists
        public float HostFeeProsperityFactor = 0.5f;       // a richer town costs more to hire
        public int PrizeModest = 3000;                     // a modest purse
        public int PrizeWorthy = 8000;                     // a purse worth riding for
        public int PrizePrincely = 15000;                  // a princely purse
        public float PrizeRadiusBonus = 0.00008f;          // every gold of prize widens the summons

        // --- What the host gains ---
        public int HostRenownPerLord = 8;                  // renown you gain per lord who attends
        public int HostInfluencePerLord = 2;               // influence you gain per lord who attends
        public int HostRelationPerLord = 2;                // relation gained with each lord who attends
        public int HostProsperityPerLord = 25;             // prosperity the town gains per lord
        public int HostLoyaltyGain = 5;                    // loyalty the town gains for the spectacle
        public int HostSecurityLoss = 3;                   // security the town loses to the crowds
        public int HostTakingsPerLord = 200;               // your cut of the takings per lord
        public float HostTakingsProsperityFactor = 0.1f;   // a richer town pays out more
        public float CancelledFeeRefund = 0.5f;            // share of the fee returned if the tourney is called off

        // --- The lists themselves ---
        public bool NoblesFightInTournaments = true;       // lords present in town take their rightful places in the bracket - troops only fill what remains
        public bool NoblesIgnoreSkillGate = true;          // a lord enters even below the games' skill bar - pride before prudence
        public int MaxNoblesInBracket = 15;                // at most this many of the sixteen places go to lords

        public bool LogEnabled = true;                     // write a log file in the module folder

        public static void Load(string moduleDataDir)
        {
            var file = Path.Combine(moduleDataDir, "GrandTourney.settings.xml");
            try
            {
                if (!File.Exists(file)) { Save(file); return; }
                var doc = new XmlDocument();
                doc.Load(file);
                var root = doc.DocumentElement;
                if (root == null) return;
                var s = new Settings();
                foreach (XmlNode n in root.ChildNodes)
                {
                    if (n.NodeType != XmlNodeType.Element) continue;
                    var f = typeof(Settings).GetField(n.Name);
                    if (f == null) continue;
                    var v = n.InnerText.Trim();
                    if (f.FieldType == typeof(bool)) f.SetValue(s, v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");
                    else if (f.FieldType == typeof(int)) f.SetValue(s, int.Parse(v, CultureInfo.InvariantCulture));
                    else if (f.FieldType == typeof(float)) f.SetValue(s, float.Parse(v, CultureInfo.InvariantCulture));
                }
                Current = s;
            }
            catch (Exception e) { Log.Error("Settings.Load", e); }
        }

        private static void Save(string file)
        {
            try
            {
                var doc = new XmlDocument();
                var root = doc.CreateElement("GrandTourney");
                doc.AppendChild(root);
                foreach (var f in typeof(Settings).GetFields())
                {
                    if (f.IsStatic) continue;
                    var e = doc.CreateElement(f.Name);
                    var val = f.GetValue(Current);
                    e.InnerText = val is bool b ? (b ? "true" : "false") : Convert.ToString(val, CultureInfo.InvariantCulture);
                    root.AppendChild(e);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                doc.Save(file);
            }
            catch (Exception e) { Log.Error("Settings.Save", e); }
        }
    }
}
