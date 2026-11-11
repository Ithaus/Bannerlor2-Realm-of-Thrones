using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>406 (PLAN-396 commit 406, Jeff 11.11 "rob 5") - POKOJ POGRZEBU. Klucz Funeral Truce. Siedziba pogrzebu w trybie PELNYM i jej wsie przez D..D+5:
    ///  - partie AI: ScoreTruce w SiegeRansom.ScorePost (postfiks Last GetTargetScoreForFaction, zaraz po pakcie 384, przed okupem) - Besieger i Raider -> 0,
    ///    dla KAZDEJ frakcji (cs_full.cs:220839 - oblezenie i najazd ida przez te sama ocene celu);
    ///  - armie BK (FindArmyObjective nie pyta o ocene celu, bk.cs:88454): WarFront334.BkFirstPostfix - cel w pokoju zawetowany (nastepny cel albo null);
    ///    to samo weto w WarFront334.VetoedAny (NextBkTarget i opcje 334e dopisywane PO ocenie celu);
    ///  - gracz: SiegeRansom.BesiegeCondPost - "Besiege the town/castle" nieczynne z dymkiem.
    /// NIGDY: tryb cichy, osada fabuly ROT (SiegeRansom.IsStory), dopoki jej wydarzenie oblezenia nie ma stanu 2; siedziba, ktora przestala byc rodu.
    /// TRWAJACEGO oblezenia / najazdu NIE zdejmujemy (osada z SiegeEvent albo wies nie w stanie Normal = bez weta).
    /// Watki: ocena celu bywa liczona rownolegle - czytana jest NIEZMIENNA migawka (_truce), podmieniana w calosci w watku glownym (TruceRebuild);
    /// liczniki przez Interlocked. Zero nowych latek. Najazd gracza na wies i gracz w cudzej armii - bez blokady (odlozone).</summary>
    internal static partial class FuneralRites
    {
        private sealed class TruceRow
        {
            public Row Src; public string Clan, ClanName, SeatName; public Settlement Seat; public int End;
        }

        private sealed class TruceMap { public readonly Dictionary<string, TruceRow> By = new Dictionary<string, TruceRow>(StringComparer.Ordinal); }

        private static volatile TruceMap _truce;   // null = zadnego pokoju (szybkie wyjscie)

        private static bool TruceKey { get { var s = Settings.Current; return s != null && s.Funerals && s.FuneralTruce; } }

        private static void TruceReset() { _truce = null; }

        /// <summary>Wydarzenie oblezenia ROT osady fabuly (SiegeRansom.StoryIds); null = nie osada fabuly.</summary>
        private static string StoryEvent(string seatId)
        {
            switch (seatId)
            {
                case "town_EW4": case "ROT_castle61": return "BlackwaterSiegeEvent";
                case "town_B2": return "BastardsSiegeEvent";
                case "ROT_town32": return "HarrenhalSiegeEvent";
                case "ROT_castle60": case "town_B5": return "WallSiegeEvent";
                default: return null;
            }
        }

        /// <summary>Watek glowny: migawka pokoju od nowa (po smierci glowy, co dobe, po wczytaniu). Nie rzuca.</summary>
        private static void TruceRebuild()
        {
            try
            {
                if (_active.Count == 0) { _truce = null; return; }
                int today = Today();
                TruceMap map = null;
                foreach (var r in _active.Values)
                {
                    if (r.Mode != Full || today > r.End) continue;
                    try
                    {
                        var seat = Settlement.Find(r.Seat);
                        var cl = FindClan(r.Clan);
                        if (seat == null || cl == null || seat.OwnerClan != cl)
                        {
                            if (!r.TSaid) { r.TSaid = true; Info("pokoj pogrzebu rodu " + r.Clan + ": BRAK - siedziba " + r.Seat + " nie jest juz rodu."); }
                            continue;
                        }
                        if (SiegeRansom.IsStory(seat))
                        {
                            string ev = StoryEvent(seat.StringId);
                            int state = ev != null ? Prologue.RotEventState(ev) : -1;
                            if (state != 2)
                            {
                                if (!r.TSaid) { r.TSaid = true; Info("pokoj pogrzebu rodu " + r.Clan + ": BRAK - " + seat.StringId + " to osada fabuly ROT (" + (ev ?? "?") + " stan " + state + ", potrzeba 2)."); }
                                continue;
                            }
                        }
                        var t = new TruceRow { Src = r, Clan = r.Clan, ClanName = Nm(cl), SeatName = Nm(seat), Seat = seat, End = r.End };
                        if (map == null) map = new TruceMap();
                        map.By[seat.StringId] = t;
                        int villages = 0;
                        var bound = seat.BoundVillages;
                        if (bound != null)
                            foreach (var v in bound)
                            {
                                var vs = v != null ? v.Settlement : null;
                                if (vs == null || vs.StringId == null) continue;
                                map.By[vs.StringId] = t; villages++;
                            }
                        if (!r.TSaid)
                        {
                            r.TSaid = true;
                            Info("POKOJ POGRZEBU rodu " + r.Clan + ": siedziba " + seat.StringId + " i " + villages + " wsi do dnia " + r.End + " wlacznie (klucz " + YN(TruceKey)
                                 + "; oblezenie i najazd AI -> ocena 0, armie BK - weto, gracz - opcja oblezenia nieczynna).");
                        }
                    }
                    catch (Exception e) { Stumble("TruceRebuild.row", e); }
                }
                _truce = map;
            }
            catch (Exception e) { _truce = null; Stumble("TruceRebuild", e); }
        }

        /// <summary>Dowolny watek, sam odczyt: osada (siedziba albo jej wies) w czynnym pokoju pogrzebu. Siedziba musi dalej nalezec do rodu.</summary>
        private static bool TruceAt(Settlement st, out TruceRow t)
        {
            t = null;
            var m = _truce;
            if (m == null || st == null || st.StringId == null || !TruceKey) return false;
            if (!m.By.TryGetValue(st.StringId, out t)) return false;
            if (Today() > t.End) return false;
            var oc = t.Seat.OwnerClan;
            return oc != null && oc.StringId == t.Clan;
        }

        /// <summary>Oblezenie albo najazd juz trwa - pokoj go nie zdejmuje.</summary>
        private static bool Ongoing(Settlement st)
        {
            if (st.IsVillage) return st.Village != null && st.Village.VillageState != Village.VillageStates.Normal;
            return st.SiegeEvent != null;
        }

        /// <summary>Z SiegeRansom.ScorePost (postfiks Last GetTargetScoreForFaction; watki AI): cel oblezenia / najazdu w pokoju pogrzebu -> 0. Nie rzuca.</summary>
        internal static void ScoreTruce(Settlement st, Army.ArmyTypes type, MobileParty party, ref float result)
        {
            if (_truce == null || result <= 0f) return;
            try
            {
                if (st == null || party == null || (type != Army.ArmyTypes.Besieger && type != Army.ArmyTypes.Raider)) return;
                TruceRow t;
                if (!TruceAt(st, out t) || Ongoing(st)) return;
                result = 0f;
                Interlocked.Increment(ref t.Src.TZero);
            }
            catch (Exception e) { Stumble("ScoreTruce", e); }
        }

        /// <summary>Weto celu bez licznika (WarFront334.VetoedAny: NextBkTarget i opcje 334e dopisywane po ocenie celu; watki AI). Nie rzuca.</summary>
        internal static bool TruceVeto(Settlement st)
        {
            if (_truce == null) return false;
            try { TruceRow t; return TruceAt(st, out t) && !Ongoing(st); }
            catch (Exception e) { Stumble("TruceVeto", e); return false; }
        }

        /// <summary>Weto celu armii BK (WarFront334.BkFirstPostfix) - z licznikiem. Nie rzuca.</summary>
        internal static bool BkVeto(Settlement st)
        {
            if (_truce == null) return false;
            try
            {
                TruceRow t;
                if (!TruceAt(st, out t) || Ongoing(st)) return false;
                Interlocked.Increment(ref t.Src.TBk);
                return true;
            }
            catch (Exception e) { Stumble("BkVeto", e); return false; }
        }

        /// <summary>Z SiegeRansom.BesiegeCondPost: opcja gry "Besiege the town/castle" w pokoju pogrzebu - nieczynna z dymkiem. true = zablokowano. Nie rzuca.</summary>
        internal static bool PlayerBesiege(MenuCallbackArgs args)
        {
            if (_truce == null || args == null) return false;
            try
            {
                var st = Settlement.CurrentSettlement;
                TruceRow t;
                if (st == null || !TruceAt(st, out t) || Ongoing(st)) return false;
                int today = Today();
                int left = Math.Max(1, t.End - today);
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=!}" + t.ClanName + " is burying its lord at " + t.SeatName + ". No banner besieges a house in mourning; the funeral peace lasts " + left
                                              + (left == 1 ? " more day." : " more days."));
                if (t.Src.TPlayerDay != today) { t.Src.TPlayerDay = today; t.Src.TPlayer++; }   // warunek menu biegnie co odswiezenie - liczymy doby
                return true;
            }
            catch (Exception e) { Stumble("PlayerBesiege", e); return false; }
        }

        /// <summary>Doba (po wygaszeniu wierszy): migawka od nowa i pomiar - siedziba oblegana mimo pokoju (np. armia BK, ktora dostala cel przed smiercia glowy).</summary>
        private static void TruceDaily()
        {
            TruceRebuild();
            var m = _truce;
            if (m == null) return;
            foreach (var kv in m.By)
            {
                var t = kv.Value;
                try
                {
                    if (kv.Key != t.Seat.StringId || t.Src.TBroken || !t.Seat.IsUnderSiege) continue;
                    t.Src.TBroken = true;
                    var camp = t.Seat.SiegeEvent != null ? t.Seat.SiegeEvent.BesiegerCamp : null;
                    var lp = camp != null ? camp.LeaderParty : null;
                    Info("POKOJ POGRZEBU ZLAMANY: " + t.Seat.StringId + " (rod " + t.Clan + ") oblegana przez " + (lp != null ? lp.StringId + " / " + (lp.MapFaction != null ? lp.MapFaction.StringId : "-") : "?")
                         + (lp != null && lp.IsMainParty ? " [gracz]" : "") + " - oblezenia nie zdejmujemy (pomiar).");
                }
                catch (Exception e) { Stumble("TruceDaily.row", e); }
            }
        }

        private static string TruceEndText(Row r)
        {
            return " | rozejm: wyzerowane cele " + Volatile.Read(ref r.TZero) + ", weta BK " + Volatile.Read(ref r.TBk) + ", gracz zablokowany " + r.TPlayer + (r.TBroken ? ", oblezona mimo pokoju TAK" : "");
        }
    }
}
