using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace CrashScribe
{
    /// <summary>
    /// ZEW NOCNEGO KROLA (Jeff 16.09: "a jak nocny krol, bo nic sie nie dzieje").
    ///
    /// DLACZEGO NIC SIE NIE DZIEJE (dekompilacja ROT.CampaignBehaviors.ROTOthersCampaignBehavior, 16.09):
    /// - inwazja na poludnie rusza, gdy WSZYSTKIE osady za Murem (procz Muru i Driftwood Hall)
    ///   naleza do Innych (OnDailyTick); dzis 0 z 25, od poczatku kampanii (dzien 840);
    /// - banda oblega TYLKO przy TotalHealthyCount >= 500 (OnAiHourlyTick), a potem jeszcze:
    ///   (PartySizeRatio >= 0.8 LUB > 2000 ludzi) i 2.5x garnizon, ALBO > 1000 ludzi i 4.5x;
    /// - rajd na wioske (glowne zrodlo trupow) wymaga > 400 ludzi w bandzie;
    /// - przyrost: +2 trupy dziennie na bande (OnDailyTickParty) i nekromancja po wygranej
    ///   bitwie (udzial w zwyciestwie x zdrowi wroga x mnoznik), ograniczona wolnym miejscem;
    /// - GWOZDZ: ROTSettings.OthersPartySizeLimitGrowth (3/dzien) dolicza sie do
    ///   _partySizeGrowth, ktore JEST ZAPISYWANE w save - po 840 dniach limit kazdej bandy
    ///   ma ~2500 miejsc premii, wiec PartySizeRatio >= 0.8 jest nieosiagalne na zawsze,
    ///   a bandy po 150-200 trupow (4 bandy, ~650 lacznie, log UMARLI) nie dobija ani do 400
    ///   na rajd, ani do 500 na oblezenie. Balans ROT zamrozil wlasna fabule.
    ///
    /// CO ROBI ZEW (raz na dzien kampanii; zadnej stalej ROT nie tyka):
    /// 1. Jesli zadna banda Innych nie ma NightKingCallTarget zdrowych trupow, trupy z
    ///    mniejszych band przechodza do najsilniejszej (Nocny Krol jako gospodarz tylko gdy
    ///    nie ma innej - ROT nie kaze mu oblegac zamkow), kazdy dawca zostawia sobie
    ///    NightKingCallKeep (Nocny Krol: NightKingCallKeepNk). Bandy w bitwie/oblezeniu
    ///    tego dnia nie daja i nie biora.
    /// 2. Premia do limitu partii (_partySizeGrowth) jest ustawiana tak, zeby najsilniejsza
    ///    banda byla pelna w NightKingCallFullness (85%) - dokladnie stan, jaki ROT zaklada
    ///    dla oblezenia (ratio >= 0.8), z miejscem na nekromancje. Dalej decyduje ROT:
    ///    cel, przewaga 2.5x, kajdany AI, oblezenie, po 25 osadach inwazja, smiertelnosc NK.
    /// Wylacznik i pokretla w ModuleData/CrashScribe.settings.xml.
    /// </summary>
    internal sealed class NightKingCall : CampaignBehaviorBase
    {
        private static Type _tOthers;
        private static object _beh;
        private static Campaign _behCampaign;
        private static System.Reflection.FieldInfo _fGrowth, _fNk;
        private static bool _greeted;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Daily);
        }

        public override void SyncData(IDataStore dataStore) { }

        private static object Others()
        {
            try
            {
                if (_beh != null && _behCampaign == Campaign.Current) return _beh;
                _beh = null; _behCampaign = Campaign.Current;
                if (_tOthers == null) _tOthers = Type.GetType("ROT.CampaignBehaviors.ROTOthersCampaignBehavior, ROT");
                if (_tOthers == null || Campaign.Current == null) return null;
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                if (mi != null) _beh = mi.MakeGenericMethod(_tOthers).Invoke(Campaign.Current, null);
                if (_fGrowth == null) _fGrowth = AccessTools.Field(_tOthers, "_partySizeGrowth");
                if (_fNk == null) _fNk = AccessTools.Field(_tOthers, "_nightKing");
                return _beh;
            }
            catch { return null; }
        }

        private static Clan WhiteWalkers()
        {
            try
            {
                var tClans = Type.GetType("ROT.Misc.ROTClans, ROT");
                var p = tClans != null ? AccessTools.Property(tClans, "WhiteWalkers") : null;
                var c = p != null ? p.GetValue(null, null) as Clan : null;
                if (c != null) return c;
            }
            catch { }
            foreach (var c in Clan.All) if (c != null && c.StringId == "ROTclan_126") return c;
            return null;
        }

        private static bool Busy(MobileParty mp)
        {
            try { return mp.MapEvent != null || mp.SiegeEvent != null; } catch { return true; }
        }

        /// <summary>Zdrowi NIE-bohaterowie dawcy przechodza do gospodarza; dawca zostawia
        /// sobie <paramref name="keep"/> zdrowych. Zwraca liczbe przeniesionych.</summary>
        private static int Transfer(MobileParty from, MobileParty to, int keep, int need)
        {
            if (from == null || to == null || need <= 0) return 0;
            var r = from.MemberRoster;
            int healthy = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero) continue;
                healthy += Math.Max(0, el.Number - el.WoundedNumber);
            }
            int allowed = healthy - Math.Max(0, keep);
            int moved = 0;
            for (int i = r.Count - 1; i >= 0 && allowed > 0 && need > 0; i--)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero) continue;
                int give = Math.Min(Math.Max(0, el.Number - el.WoundedNumber), Math.Min(allowed, need));
                if (give <= 0) continue;
                r.AddToCounts(ch, -give);
                to.MemberRoster.AddToCounts(ch, give);
                moved += give; allowed -= give; need -= give;
            }
            return moved;
        }

        private void Daily()
        {
            try
            {
                if (!Config.NightKingCallEnabled) return;
                var beh = Others();
                if (beh == null) return;
                var ww = WhiteWalkers();
                if (ww == null || ww.IsEliminated) return;
                Hero nk = null;
                try { nk = _fNk != null ? _fNk.GetValue(beh) as Hero : null; } catch { }
                int target = Math.Max(100, Config.NightKingCallTarget);
                float full = Config.NightKingCallFullness;
                if (full < 0.5f) full = 0.5f;
                if (full > 0.95f) full = 0.95f;

                if (!_greeted)
                {
                    _greeted = true;
                    Scribe.Line("Zew Nocnego Krola: czynny - cel " + target + " zdrowych w jednej bandzie, dawca zostawia sobie "
                                + Config.NightKingCallKeep + " (Nocny Krol " + Config.NightKingCallKeepNk + "), horda pelna w "
                                + (int)(full * 100) + "%.");
                }

                // bandy Innych
                var bands = new List<MobileParty>();
                foreach (var wp in ww.WarPartyComponents)
                {
                    var mp = wp != null ? wp.MobileParty : null;
                    if (mp == null || !mp.IsActive || mp.IsMainParty || mp.LeaderHero == null || mp.MemberRoster == null) continue;
                    bands.Add(mp);
                }
                if (bands.Count == 0) { Scribe.Line("Zew Nocnego Krola: Inni nie maja zadnej bandy w polu."); return; }

                // gotowa banda?
                MobileParty ready = null;
                foreach (var mp in bands)
                    if (mp.MemberRoster.TotalHealthyCount >= target && (ready == null || mp.MemberRoster.TotalHealthyCount > ready.MemberRoster.TotalHealthyCount)) ready = mp;

                // gospodarz: najsilniejsza wolna banda, Nocny Krol dopiero gdy nie ma innej
                MobileParty host = null; long hostScore = long.MinValue;
                foreach (var mp in bands)
                {
                    if (Busy(mp)) continue;
                    bool isNk = nk != null && mp.LeaderHero == nk;
                    long score = mp.MemberRoster.TotalHealthyCount - (isNk ? 1000000L : 0L);
                    if (score > hostScore) { hostScore = score; host = mp; }
                }

                int moved = 0, donors = 0;
                if (ready == null && host != null)
                {
                    foreach (var mp in bands)
                    {
                        if (mp == host || Busy(mp)) continue;
                        int need = target - host.MemberRoster.TotalHealthyCount;
                        if (need <= 0) break;
                        bool isNk = nk != null && mp.LeaderHero == nk;
                        int give = Transfer(mp, host, isNk ? Config.NightKingCallKeepNk : Config.NightKingCallKeep, need);
                        if (give > 0) { moved += give; donors++; }
                    }
                }

                // limit hordy: najsilniejsza banda pelna w ~85% (ROT: oblezenie przy ratio >= 0.8)
                var lead = ready ?? host;
                string limitNote = "";
                if (lead != null && _fGrowth != null)
                {
                    try
                    {
                        int bonusNow = (int)Convert.ToSingle(_fGrowth.GetValue(beh));
                        bool leadNk = nk != null && lead.LeaderHero == nk;
                        int limitNow = lead.Party.PartySizeLimit;
                        int baseLimit = Math.Max(1, limitNow - bonusNow - (leadNk ? 250 : 0));
                        int leadMen = lead.MemberRoster.TotalManCount;
                        int wantLimit = Math.Max(baseLimit + 50, (int)(leadMen / full));
                        int wantBonus = Math.Max(0, wantLimit - baseLimit);
                        if (Math.Abs(bonusNow - wantBonus) > 5)
                        {
                            _fGrowth.SetValue(beh, (float)wantBonus);
                            limitNote = "; premia limitu hordy " + bonusNow + " -> " + wantBonus + " (baza " + baseLimit + ", limit " + (baseLimit + wantBonus) + ")";
                        }
                        else limitNote = "; premia limitu " + bonusNow + " (limit " + limitNow + ")";
                    }
                    catch (Exception e) { try { Scribe.Report("CrashScribe", e, "NightKingCall.limit", null); } catch { } }
                }

                if (ready != null)
                    Scribe.Line("Zew Nocnego Krola: " + ready.LeaderHero.Name + " ma " + ready.MemberRoster.TotalHealthyCount
                                + " zdrowych trupow (cel " + target + ") - oblezenie w rekach ROT" + limitNote + ".");
                else if (host != null)
                    Scribe.Line("Zew Nocnego Krola: " + host.LeaderHero.Name + " przyjmuje " + moved + " trupow od " + donors + " band -> "
                                + host.MemberRoster.TotalHealthyCount + " zdrowych z " + host.MemberRoster.TotalManCount + " (cel " + target
                                + ", band w polu " + bands.Count + ")" + limitNote + ".");
                else
                    Scribe.Line("Zew Nocnego Krola: wszystkie " + bands.Count + " bandy w walce albo oblezeniu - dzis bez przemarszu.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "NightKingCall.Daily", null); } catch { } }
        }
    }
}
