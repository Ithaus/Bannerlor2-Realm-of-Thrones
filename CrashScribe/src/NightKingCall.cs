using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

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

        // --- Pochod Nocnego Krola (20.09) ---
        private static System.Reflection.FieldInfo _fToSiege, _fInvasion, _fCooldowns;
        private static System.Reflection.MethodInfo _mBeyond;
        private static Settlement _theWall;
        private static bool _marchGreeted;

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
                _beh = null; _behCampaign = Campaign.Current; _theWall = null; _marchGreeted = false;
                if (_tOthers == null) _tOthers = Type.GetType("ROT.CampaignBehaviors.ROTOthersCampaignBehavior, ROT");
                if (_tOthers == null || Campaign.Current == null) return null;
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                if (mi != null) _beh = mi.MakeGenericMethod(_tOthers).Invoke(Campaign.Current, null);
                if (_fGrowth == null) _fGrowth = AccessTools.Field(_tOthers, "_partySizeGrowth");
                if (_fNk == null) _fNk = AccessTools.Field(_tOthers, "_nightKing");
                if (_fToSiege == null) _fToSiege = AccessTools.Field(_tOthers, "_settlementsToSiege");
                if (_fInvasion == null) _fInvasion = AccessTools.Field(_tOthers, "IsInvasionStarted");
                if (_fCooldowns == null) _fCooldowns = AccessTools.Field(_tOthers, "_siegeCooldowns");
                if (_mBeyond == null)
                {
                    var tUtil = Type.GetType("ROT.Misc.ROTUtilities, ROT");
                    _mBeyond = tUtil != null ? AccessTools.Method(tUtil, "IsBeyondTheWall") : null;
                }
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

        /// <summary>Czy osada lezy za Murem - pytamy o to sam ROT (ROT.Misc.ROTUtilities.IsBeyondTheWall),
        /// tym samym wzorem, ktorego ROT uzywa przy wyborze celu oblezenia (l.513).</summary>
        private static bool Beyond(Settlement s)
        {
            if (_mBeyond == null || s == null) return false;
            try { return Convert.ToBoolean(_mBeyond.Invoke(null, new object[] { s.GetPosition2D })); }
            catch { return false; }
        }

        /// <summary>"Inny" w ROT to KULTURA, nie id klanu (ROT.Misc/Extensions.cs:10).</summary>
        private static bool IsOthers(Clan c)
        {
            try { return c != null && c.Culture != null && c.Culture.StringId == "whitewalker"; }
            catch { return false; }
        }

        private static bool Invasion(object beh)
        {
            try { return _fInvasion != null && Convert.ToBoolean(_fInvasion.GetValue(beh)); }
            catch { return false; }
        }

        /// <summary>Czy ROT trzyma tego dowodce na 5-dniowej przerwie po oblezeniu (_siegeCooldowns,
        /// ustawiane w SiegeCompletedEvent - takze po PRZEGRANYM szturmie, isWin jest ignorowane).</summary>
        private static bool Resting(object beh, Hero leader)
        {
            try
            {
                var d = _fCooldowns != null ? _fCooldowns.GetValue(beh) as System.Collections.IDictionary : null;
                if (d == null || leader == null || !d.Contains(leader)) return false;
                return (CampaignTime)d[leader] > CampaignTime.Now;
            }
            catch { return false; }
        }

        /// <summary>Kandydaci na cel: WLASNA lista ROT (_settlementsToSiege - fortyfikacje na zachod
        /// od Tyrosh minus wyspy i Driftwood Hall), zeby nie wybrac czegos, co ROT z tej wojny wyjal.
        /// Gdyby ROT jej jeszcze nie zbudowal - bierzemy wszystkie osady i filtrujemy sami.</summary>
        private static System.Collections.Generic.IEnumerable<Settlement> Candidates(object beh)
        {
            try
            {
                var list = _fToSiege != null
                    ? _fToSiege.GetValue(beh) as System.Collections.Generic.IEnumerable<Settlement> : null;
                if (list != null) return list;
            }
            catch { }
            return Settlement.All;
        }

        /// <summary>Wybiera osade za Murem, ktora horda ma NAJWIEKSZA przewage (przy remisie - blizsza).
        /// ROT bierze zawsze najbardziej polnocna, czyli najpierw trzy duze miasta; nam zalezy na
        /// pierwszej zdobyczy, bo 18 wiosek przechodzi razem ze swoimi siedmioma lennami.</summary>
        private static Settlement PickTarget(object beh, Clan ww, MobileParty mp, System.Text.StringBuilder table)
        {
            int men = mp.MemberRoster.TotalManCount;
            Settlement best = null; float topOdds = -1f, bestDist = float.MaxValue;
            foreach (var s in Candidates(beh))
            {
                if (s == null || !s.IsFortification || s == _theWall) continue;   // Mur zostaje ROT
                if (!Beyond(s)) continue;                                         // na poludnie nie idziemy
                if (IsOthers(s.OwnerClan)) continue;                              // juz nasza
                int guard = 1;
                try { guard = s.Town != null ? Math.Max(1, s.Town.GetNumberOfTroops()) : 1; } catch { }
                float odds = (float)men / guard;
                float dist = 9999f;
                try { dist = mp.GetPosition2D.Distance(s.GetPosition2D); } catch { }
                bool war = false;
                try { war = FactionManager.IsAtWarAgainstFaction(ww, s.MapFaction); } catch { }
                bool taken = false;
                try { taken = s.IsUnderSiege; } catch { }
                if (table != null)
                    table.Append("   ").Append(s.Name)
                         .Append(" (").Append(s.MapFaction != null ? s.MapFaction.Name.ToString() : "?").Append(")")
                         .Append(": garnizon z milicja ").Append(guard)
                         .Append(", przewaga ").Append(odds.ToString("0.0"))
                         .Append("x, dystans ").Append(dist.ToString("0"))
                         .Append(war ? ", WOJNA" : ", POKOJ")
                         .Append(taken ? ", juz oblegana" : "")
                         .Append(Environment.NewLine);
                if (!war || taken) continue;
                if (odds < Config.NightKingMarchOdds) continue;
                if (odds > topOdds + 0.5f) { topOdds = odds; best = s; bestDist = dist; }
                else if (odds > topOdds - 0.5f)
                {
                    if (dist < bestDist) { best = s; bestDist = dist; }
                    if (odds > topOdds) topOdds = odds;
                }
            }
            return best;
        }

        /// <summary>
        /// POCHOD: gotowa horda dostaje CEL wprost. Raz na dzien i tylko wtedy, gdy nie ma juz
        /// wlasnego rozkazu oblezenia.
        ///
        /// DLACZEGO TRZEBA PCHNAC (dekompilacja 20.09):
        /// - ROT.HarmonyPatches.Core.AIThinkPatch podmienia AiPartyThinkBehavior.PartyHourlyAiTick
        ///   i przy !(MapFaction is Kingdom) - a klan Innych to minor faction bez krolestwa -
        ///   zwycieski wynik z willGatherArmy:true NIE JEST W OGOLE NADAWANY. Rajd (l.385, 428),
        ///   obrona (l.438, 480) i patrol (l.490) maja willGatherArmy:true, wiec jedynym wykonalnym
        ///   zachowaniem Innych jest OBLEZENIE (l.368, 561 - willGatherArmy:false).
        /// - Oblezenie ROT wystawia dopiero, gdy jego wlasny filtr znajdzie cel z przewaga 2.5x nad
        ///   garnizonem i milicja (l.527 i 558-559). Gdy nie znajdzie - goto IL_0402 - horda nie
        ///   dostaje ZADNEGO rozkazu i stoi. Z naszego logu wiemy, ze druga bramka l.559
        ///   (PartySizeRatio >= 0.8) jest spelniona: "520 zdrowych z 783 ... limit 921" = 0.85.
        ///
        /// PO NADANIU ROZKAZU DALEJ JEDZIE ROT: l.355-372, przy >= 500 zdrowych dopisuje TEMU SAMEMU
        /// celowi 999 punktow z willGatherArmy:false, a taki wynik latka ROT umie wykonac. Gdy horda
        /// spadnie ponizej 500, zwycieskie beda rajd/obrona/patrol - czyli nic, co da sie nadac -
        /// wiec DefaultBehavior zostaje na oblezeniu i marsz trwa dalej.
        /// </summary>
        private static void March(object beh, Clan ww, MobileParty mp)
        {
            try
            {
                if (!Config.NightKingMarchEnabled || beh == null || ww == null) return;
                if (mp == null || !mp.IsActive || mp.LeaderHero == null || mp.MemberRoster == null) return;

                if (!_marchGreeted)
                {
                    _marchGreeted = true;
                    Scribe.Line("Pochod Nocnego Krola: czynny - od " + Config.NightKingMarchMin
                                + " zdrowych horda dostaje cel za Murem, przy przewadze co najmniej "
                                + Config.NightKingMarchOdds.ToString("0.0") + "x nad garnizonem z milicja.");
                }

                if (Invasion(beh)) return;   // inwazja ruszyla - od tej chwili rzadzi ROT

                int healthy = mp.MemberRoster.TotalHealthyCount;
                int men = mp.MemberRoster.TotalManCount;
                int limit = 0;
                try { limit = mp.Party.PartySizeLimit; } catch { }

                if (Busy(mp))
                {
                    string what = "bitwa w polu";
                    try
                    {
                        if (mp.SiegeEvent != null)
                            what = "OBLEZENIE " + (mp.BesiegedSettlement != null ? mp.BesiegedSettlement.Name.ToString() : "?");
                    }
                    catch { }
                    Scribe.Line("Pochod Nocnego Krola: " + mp.LeaderHero.Name + " zajety (" + what + ", "
                                + healthy + " zdrowych z " + men + ") - dzis bez rozkazu.");
                    return;
                }

                var tgt = mp.TargetSettlement;
                if (mp.DefaultBehavior == AiBehavior.BesiegeSettlement && tgt != null && !IsOthers(tgt.OwnerClan)
                    && FactionManager.IsAtWarAgainstFaction(ww, tgt.MapFaction))
                {
                    float d = 0f;
                    try { d = mp.GetPosition2D.Distance(tgt.GetPosition2D); } catch { }
                    Scribe.Line("Pochod Nocnego Krola: " + mp.LeaderHero.Name + " juz idzie na " + tgt.Name
                                + " (dystans " + d.ToString("0") + ", " + healthy + " zdrowych z " + men
                                + ", limit " + limit + ") - rozkazu nie ruszamy.");
                    return;
                }

                if (healthy < Config.NightKingMarchMin) return;   // Zew jeszcze zbiera trupy

                if (Config.NightKingMarchRespectCooldown && Resting(beh, mp.LeaderHero))
                {
                    Scribe.Line("Pochod Nocnego Krola: " + mp.LeaderHero.Name
                                + " na 5-dniowej przerwie ROT po oblezeniu - dzis bez rozkazu.");
                    return;
                }

                var table = Config.NightKingMarchVerbose ? new System.Text.StringBuilder() : null;
                var target = PickTarget(beh, ww, mp, table);
                if (table != null && table.Length > 0)
                    Scribe.Line("Pochod Nocnego Krola: osady za Murem (horda " + men + " ludzi, limit " + limit + "):"
                                + Environment.NewLine + table.ToString().TrimEnd());

                if (target == null)
                {
                    Scribe.Line("Pochod Nocnego Krola: " + mp.LeaderHero.Name + " ma " + healthy + " zdrowych z " + men
                                + ", ale zadna osada za Murem nie daje przewagi "
                                + Config.NightKingMarchOdds.ToString("0.0") + "x (albo jest z nia pokoj).");
                    return;
                }

                int guard = 1;
                try { guard = target.Town != null ? Math.Max(1, target.Town.GetNumberOfTroops()) : 1; } catch { }
                float dist = 0f;
                try { dist = mp.GetPosition2D.Distance(target.GetPosition2D); } catch { }

                SetPartyAiAction.GetActionForBesiegingSettlement(mp, target, MobileParty.NavigationType.Default, false);

                Scribe.Line("Pochod Nocnego Krola: " + mp.LeaderHero.Name + " (" + healthy + " zdrowych z " + men
                            + ", limit " + limit + ") RUSZA NA " + target.Name + " - garnizon z milicja " + guard
                            + ", przewaga " + ((float)men / guard).ToString("0.0") + "x, dystans " + dist.ToString("0")
                            + "; rozkaz po nadaniu: " + mp.DefaultBehavior + " -> "
                            + (mp.TargetSettlement != null ? mp.TargetSettlement.Name.ToString() : "brak") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "NightKingCall.March", null); } catch { } }
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

                March(beh, ww, lead);   // Pochod: gotowa horda dostaje cel za Murem
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "NightKingCall.Daily", null); } catch { } }
        }
    }
}
