using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 223 sonda: pojmania glow rodow i lordow AI (CampaignEvents.HeroPrisonerTaken) z obrazem partii z ostatniej mysli AI (gra zdejmuje bohatera z partii PRZED
    /// zdarzeniem - TakePrisonerAction.ApplyInternal, wiec stan partii trzeba miec wczesniej). Obraz: ludzie, wypelnienie, armia, stan 223, rozkaz gry i od kiedy Hold.
    /// Tylko liczniki i linia - nic nie zmienia.
    /// </summary>
    internal static partial class LordEscort
    {
        private sealed class Snapshot
        {
            internal double H, HoldSinceH = -1; internal int Men, Healthy, Rank; internal float Fill; internal bool Army, Weak, Free; internal string Beh = "-", Party = "-";
        }
        private static readonly Dictionary<Hero, Snapshot> _seen = new Dictionary<Hero, Snapshot>();
        private static int _cLeaders, _cAloneWeak, _cAloneStrong, _cArmy, _cSettle, _cUnknown, _cLords;

        private static void ClearDayProbe() { _cLeaders = _cAloneWeak = _cAloneStrong = _cArmy = _cSettle = _cUnknown = _cLords = 0; }

        /// <summary>Obraz partii glowy rodu AI przy mysli AI (watek glowny). Nie rzuca.</summary>
        private static void Snap(MobileParty mp, double now)
        {
            try
            {
                var h = mp.LeaderHero;
                var clan = h != null ? h.Clan : null;
                if (clan == null || clan.Leader != h || clan == Clan.PlayerClan) return;
                Snapshot o;
                _seen.TryGetValue(h, out o);
                var r = mp.MemberRoster;
                var st = StateOf(mp);
                var beh = mp.DefaultBehavior;
                var n = new Snapshot
                {
                    H = now, Men = r != null ? r.TotalManCount : 0, Healthy = r != null ? r.TotalHealthyCount : 0, Fill = Fill(mp), Army = mp.Army != null,
                    Weak = st != null && st.SinceH >= 0, Free = st != null && st.SinceH < 0 && st.FreeUntilH > now, Rank = st != null ? st.Rank : 0,
                    Beh = beh.ToString() + "/" + mp.ShortTermBehavior.ToString(), Party = Nm(mp)
                };
                if (beh == AiBehavior.Hold) n.HoldSinceH = o != null && o.HoldSinceH >= 0 ? o.HoldSinceH : now;
                _seen[h] = n;
                if (_seen.Count > 3000) _seen.Clear();
            }
            catch (Exception e) { Stumble("Snap", e); }
        }

        /// <summary>HeroPrisonerTaken: lord rodu AI pojmany - licznik; glowa rodu - klasa (w osadzie, w armii, sam slaby, sam silny) i linia "POJMANY".</summary>
        private static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (!On || prisoner == null || !prisoner.IsLord) return;
                var clan = prisoner.Clan;
                if (clan == null || clan == Clan.PlayerClan || clan.IsBanditFaction) return;
                _cLords++;
                if (clan.Leader != prisoner) return;
                _cLeaders++;
                double now = Math.Floor(CampaignTime.Now.ToHours);
                Snapshot o;
                bool have = _seen.TryGetValue(prisoner, out o) && now - o.H <= 7.0;   // mysl co 6 h (w armii co 3 h)
                string cls;
                if (capturer != null && capturer.IsSettlement) { _cSettle++; cls = "w osadzie"; }
                else if (!have) { _cUnknown++; cls = "bez obrazu partii"; }
                else if (o.Army) { _cArmy++; cls = "w armii"; }
                else if (o.Weak) { _cAloneWeak++; cls = "sam slaby"; }
                else { _cAloneStrong++; cls = "sam silny"; }
                string by = "?";
                try { by = capturer != null && capturer.Name != null ? capturer.Name.ToString() : "?"; } catch { }
                Log.Info("Eskorta lordow (223): POJMANY " + Hn(prisoner) + " (glowa rodu" + (Ransom178.IsRulerOrHeir(prisoner) ? ", wladca/nastepca" : "") + ") przez " + by
                         + " | " + cls + (have ? " | " + o.Party + " ludzi " + o.Men + ", zdrowych " + o.Healthy + ", wypelnienie " + F2(o.Fill) + "/" + F2(Ratio())
                         + ", stan 223 " + (o.Weak ? "narazony" : (o.Free ? "zwolniony" : "-")) + ", w armii " + (o.Army ? "TAK" : "NIE")
                         + ", ostatni rozkaz " + o.Beh + (o.HoldSinceH >= 0 ? " (Hold od " + F0(now - o.HoldSinceH) + " h)" : "") + ", obraz sprzed " + F0(now - o.H) + " h" : "") + ".");
            }
            catch (Exception e) { Stumble("OnPrisonerTaken", e); }
        }

        private static string ProbeText()
        {
            return "pojmania dzis: glowy rodow " + _cLeaders + " (sam slaby " + _cAloneWeak + ", sam silny " + _cAloneStrong + ", w armii " + _cArmy + ", w osadzie " + _cSettle
                   + ", bez obrazu " + _cUnknown + "), lordowie " + _cLords;
        }
    }
}
