using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 320a (Krwawe Gody) - PROBA AUTOTESTU T2 (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\
    /// armoury-krwawe-gody320.flag; plik tylko czytany; w zwyklej grze nic nie robi). 2. doba sesji: linia warunkow ROT RedWeddingEvent.MainCheck i migawka rodow
    /// (krolestwo, lenna), potem ROTEventBehavior.TriggerEvent(RedWeddingEvent) (droga komendy rot.trigger_event - omija bramke daty). W chwili ExecuteRobb (RwPeace,
    /// przed przeniesieniem rodow przez ROT) - partie odchodzacych rodow w bitwie / oblezeniu. Pierwsza doba po Godach - linie rodow przed i po.
    /// Linie "Krwawe Gody bez pokoju (320): test - ...".
    /// </summary>
    internal static partial class KingInTheNorth
    {
        internal const string AutoFlag320 = "armoury-krwawe-gody320.flag";
        private const string T320 = "Krwawe Gody bez pokoju (320): test - ";
        private static readonly string[] Defectors320 = { "Bolton", "Dustin", "Whitehill", "Ryswell", "Stout", "Umber", "Frey", "Mooton", "Cox", "Bracken" };
        private static bool _t320Fired, _t320After, _t320Flag;
        private static int _t320FlagDay = -1, _t320FiredDay = -1;
        private static readonly Dictionary<string, string> _t320Before = new Dictionary<string, string>(StringComparer.Ordinal);

        private static void Test320Reset() { _t320Fired = _t320After = _t320Flag = false; _t320FlagDay = -1; _t320FiredDay = -1; _t320Before.Clear(); }

        private static bool Test320Wanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            if (_t320FlagDay != _sessionDays)
            {
                _t320FlagDay = _sessionDays;
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _t320Flag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlag320));
                }
                catch { _t320Flag = false; }
            }
            return _t320Flag;
        }

        private static string ClanState(Clan c)
        {
            if (c == null) return "brak";
            if (c.IsEliminated) return "wyeliminowany";
            return (c.Kingdom != null ? c.Kingdom.StringId : "-") + ", lenna " + c.Fiefs.Count + (c.Fiefs.Count > 0 ? " [" + string.Join(" ", c.Fiefs.Take(6).Select(f => f.Settlement != null ? f.Settlement.StringId : "?").ToArray()) + "]" : "");
        }

        private static object RotLordsGet(string name)
        {
            var t = AccessTools.TypeByName("ROT.Misc.ROTLords");
            var pi = t != null ? AccessTools.Property(t, name) : null;
            return pi != null ? pi.GetValue(null, null) : null;
        }

        /// <summary>Co dobe (Daily): wyzwolenie Godow na 2. dobie sesji i linia rodow pierwszej doby po ExecuteRobb.</summary>
        private static void Test320Daily()
        {
            if (!Test320Wanted()) return;
            try
            {
                if (!_t320Fired && _sessionDays >= 2)
                {
                    _t320Fired = true; _t320FiredDay = _sessionDays;
                    var roose = RotLordsGet("RooseBolton") as Hero; var walder = RotLordsGet("WalderFrey") as Hero; var robb = HeroOf(RobbId);
                    var twins = AccessTools.TypeByName("ROT.Misc.ROTSettlements") is Type ts && AccessTools.Property(ts, "TheTwins") is System.Reflection.PropertyInfo pt
                        ? pt.GetValue(null, null) as TaleWorlds.CampaignSystem.Settlements.Settlement : null;
                    var frey = RotClan("Frey"); var north = K(NorthId); var west = K(CrownId);
                    bool mc = roose != null && roose.IsAlive && walder != null && walder.IsAlive && twins != null && twins.OwnerClan == frey && robb != null && robb.IsAlive && Alive(north);
                    Log.Info(T320 + "warunki ROT MainCheck: Roose " + (roose != null && roose.IsAlive ? "TAK" : "NIE") + ", Walder " + (walder != null && walder.IsAlive ? "TAK" : "NIE")
                             + ", Blizniaki u Freyow " + (twins != null && twins.OwnerClan == frey ? "TAK" : "NIE (" + (twins != null && twins.OwnerClan != null ? twins.OwnerClan.StringId : "-") + ")")
                             + ", Robb " + (robb != null && robb.IsAlive ? "TAK" : "NIE") + ", Polnoc " + (Alive(north) ? "TAK" : "NIE") + " => " + (mc ? "TAK" : "NIE - ROT skasuje Gody")
                             + " | Polnoc-korona w wojnie " + (Alive(north) && Alive(west) && north.IsAtWarWith(west) ? "TAK" : "NIE") + " | stan 272 " + _st + ".");
                    foreach (var n in Defectors320) { var c = RotClan(n); _t320Before[n] = ClanState(c); }
                    Log.Info(T320 + "przed: " + string.Join("; ", Defectors320.Select(n => n + " " + _t320Before[n]).ToArray()) + ".");
                    var tBeh = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTEventBehavior");
                    var tEv = AccessTools.TypeByName("ROT.Events.RedWeddingEvent");
                    var mTrig = tBeh != null ? AccessTools.DeclaredMethod(tBeh, "TriggerEvent", new[] { typeof(Type) }) : null;
                    object beh = null;
                    if (tBeh != null) { var mi = typeof(Campaign).GetMethod("GetCampaignBehavior"); beh = mi != null ? mi.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null) : null; }
                    object r = mTrig != null && beh != null && tEv != null ? mTrig.Invoke(beh, new object[] { tEv }) : null;
                    Log.Info(T320 + "ROT TriggerEvent(RedWeddingEvent) " + (r is bool && (bool)r ? "TAK" : "NIE (" + (mTrig == null ? "brak metody" : beh == null ? "brak zachowania" : "wydarzenie juz bylo") + ")")
                             + " - ExecuteRobb po 14 dobach (dzien sesji " + _sessionDays + ").");
                    return;
                }
                if (_t320Fired && !_t320After && _rwPeaceCalls > 0)
                {
                    _t320After = true;
                    var parts = new List<string>();
                    foreach (var n in Defectors320) { string b; _t320Before.TryGetValue(n, out b); parts.Add(n + " " + (b ?? "?") + " -> " + ClanState(RotClan(n))); }
                    var north = K(NorthId); var west = K(CrownId); var river = K(RiverId);
                    Log.Info(T320 + "po Godach: " + string.Join("; ", parts.ToArray()) + " | Polnoc lenna " + (Alive(north) ? north.Fiefs.Count.ToString() : "-") + ", korona lenna "
                             + (Alive(west) ? west.Fiefs.Count.ToString() : "-") + ", Dorzecze " + (Alive(river) ? "lenna " + river.Fiefs.Count : "brak") + " | Polnoc-korona w wojnie "
                             + (Alive(north) && Alive(west) && north.IsAtWarWith(west) ? "TAK" : "NIE") + " | Roose zyje " + RooseAlive()
                             + " | Winterfell " + WfOwner320() + " | " + Rw320Text() + ".");
                }
            }
            catch (Exception e) { Stumble("Test320Daily", e); Log.Info(T320 + "wyjatek " + e.GetType().Name + " - NIE."); }
        }

        /// <summary>320a: pan Winterfell do linii testu (recenzja: Bekarci wymagaja Winterfell u Boltonow).</summary>
        private static string WfOwner320()
        {
            try { var wf = TaleWorlds.CampaignSystem.Settlements.Settlement.Find(WinterfellId); var o = wf != null ? wf.OwnerClan : null; return o == null ? "-" : Nm(o) + " (" + o.StringId + ", " + (o.Kingdom != null ? o.Kingdom.StringId : "-") + ")"; }
            catch { return "?"; }
        }

        /// <summary>Z RwPeace (przed przeniesieniem rodow przez ROT): partie odchodzacych rodow w bitwie albo oblezeniu.</summary>
        private static void Test320AtExecute()
        {
            if (!Test320Wanted()) return;
            try
            {
                int parties = 0, busy = 0; var l = new List<string>();
                foreach (var n in Defectors320)
                {
                    var c = RotClan(n);
                    if (c == null || c.IsEliminated) continue;
                    foreach (var wpc in c.WarPartyComponents.ToList())
                    {
                        var mp = wpc != null ? wpc.MobileParty : null;
                        if (mp == null) continue;
                        parties++;
                        bool b = mp.MapEvent != null || mp.SiegeEvent != null || (mp.BesiegedSettlement != null);
                        if (b) { busy++; if (l.Count < 8) l.Add(n + ":" + mp.StringId + (mp.MapEvent != null ? " bitwa" : " oblezenie")); }
                    }
                }
                Log.Info(T320 + "chwila ExecuteRobb: partii odchodzacych rodow " + parties + ", w bitwie albo oblezeniu " + busy + (l.Count > 0 ? " [" + string.Join(", ", l.ToArray()) + "]" : "") + ".");
            }
            catch (Exception e) { Stumble("Test320AtExecute", e); }
        }
    }
}
