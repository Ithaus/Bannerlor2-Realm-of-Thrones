using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>Robota w toku przy kowadle. Zapisywana w stanie: "itemId|dniDo|tempo|osadaId|rodzaj|modyfikator".</summary>
    internal struct Project
    {
        internal ItemObject Item;
        internal float DaysLeft;
        internal int Tempo;          // 0 pospiesznie, 1 zwyczajnie, 2 z dbaloscia
        internal string SettlementId;
        internal string Kind;        // "" nasza kuznia (rzut przy koncu); "van" bron z vanilla kucia - DOSTAWA bez drugiego rzutu
        internal string ModifierId;  // jakosc z rzutu przy kowadle - wraca na wyrobie przy dostawie

        internal static Project Parse(string line)
        {
            var p = new Project();
            try
            {
                var a = line.Split('|');
                p.Item = MBObjectManager.Instance.GetObject<ItemObject>(a[0]);
                p.DaysLeft = float.Parse(a[1], CultureInfo.InvariantCulture);
                p.Tempo = int.Parse(a[2]);
                p.SettlementId = a.Length > 3 ? a[3] : "";
                p.Kind = a.Length > 4 ? a[4] : "";
                p.ModifierId = a.Length > 5 ? a[5] : "";
            }
            catch (Exception e) { Log.Error("Project.Parse", e); }
            return p;
        }

        internal string Serialize()
        {
            return (Item != null ? Item.StringId : "") + "|" +
                   DaysLeft.ToString("0.##", CultureInfo.InvariantCulture) + "|" +
                   Tempo + "|" + SettlementId + "|" + (Kind ?? "") + "|" + (ModifierId ?? "");
        }

        internal string TempoName
        {
            get { return Tempo == 0 ? "hastily" : (Tempo == 2 ? "with care" : "at a steady pace"); }
        }

        internal static float TimeFactor(int tempo)
        {
            var s = Settings.Current;
            return tempo == 0 ? s.TempoHastyTime : (tempo == 2 ? s.TempoCarefulTime : 1f);
        }

        internal static float RiskFactor(int tempo)
        {
            var s = Settings.Current;
            return tempo == 0 ? s.TempoHastyRisk : (tempo == 2 ? s.TempoCarefulRisk : 1f);
        }

        internal static float QualityFactor(int tempo)
        {
            var s = Settings.Current;
            return tempo == 0 ? 0.15f : (tempo == 2 ? s.TempoCarefulQuality : 1f);
        }
    }
}
