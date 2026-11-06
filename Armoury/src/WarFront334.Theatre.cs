using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334 B7 - TEATR KANONU POLNOCY DO KRWAWYCH GODOW (klucz NorthCanonTheatre334) i teatr Wysp (B9, klucz IronbornCanonReave334); B8 - BK nastepny cel (BkVetoNextTarget334).
    ///  B7: AI Polnocy (druzyny, wodz armii - takze armii, w ktorej jedzie gracz; BK) nie zaczyna oblezenia ani najazdu osady, ktorej Settlement.Culture (ROT-Map, nie
    ///      zmienia sie po zdobyciu) to crownlands, reach, stormlands, dragonstone, vale, aserai albo sturgia (rdzen Wysp) - Robb wygrywa w Dorzeczu i na Zachodzie,
    ///      nie podbija ziem koronnych ani Reach i nie plynie na Pyke. Wyjatek: odbicie osady, ktora Polnoc trzymala w tej kampanii (lista z pierwszego liczenia 334 i
    ///      kolejnych dob, bez zdobyczy spoza teatru - te zwraca 326b). Trwajace oblezenie Polnocy - dolaczenie bez weta. Obrona i odsiecz bez zmian.
    ///      Druzyna gracza nie ma weta (wybor gracza; jego zdobycz zostaje u niego - 326b pomija gracza). Faza: do Krwawych Godow (CanonWarClock - ta sama regula co 264/274).
    ///  B9 teatr Wysp: do Krwawych Godow bez celow kultury reach, stormlands, dragonstone, vale, aserai; po Krwawych Godach Reach dozwolony (wojna z nim to osobna paczka).
    ///  Mechanizm jak weto siedzib 274: postfiks (priorytet 48) CampaignEventDispatcher.AiHourlyTick - ocena -1 (ponizej startu wyboru gry), BK - ponizej.
    ///  B8: postfiks BKArmyBehavior.FindArmyObjective z priorytetem First (przed 274 i 326b): cel oblezenia z wetem (siedziba / powsciagliwosc 274, teatr 334, zwrot
    ///      326b) -> nastepna wroga twierdza wedlug reguly BK (wrogie krolestwa, twierdza z panem i bez oblezenia, brama w 280, najblizsza z droga ladem 0..50000 -
    ///      BK NearestReachable) bez weta; null tylko, gdy takiej nie ma. Postfiks Last liczy "BK null przy wecie" (cel byl, a ktos i tak dal null) - ma byc 0.
    /// </summary>
    internal static partial class WarFront334
    {
        private static readonly string[] NorthOutside = { "crownlands", "reach", "stormlands", "dragonstone", "vale", "aserai", "sturgia" };
        private static readonly string[] IronOutsideBeforeRw = { "reach", "stormlands", "dragonstone", "vale", "aserai" };
        private static readonly string[] IronOutsideAfterRw = { "stormlands", "dragonstone", "vale", "aserai" };

        private static int _dVetoThink, _dVetoIron, _dVetoBk, _dBkNext, _dBkNone, _dBkNull, _dOutSiegeAi, _dOutTaken, _sOutTaken;
        private static readonly HashSet<string> _dSaid = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> _dBkNotes = new List<string>();
        private static readonly List<string> _dTakenNotes = new List<string>();
        [ThreadStatic] private static bool _bkReplaced;

        private static void ResetTheatre() { ZeroTheatreDay(); _sOutTaken = 0; }   // latki raz na proces (_hooksTried bez zerowania - bez podwojnego wpiecia po wczytaniu)

        private static void ZeroTheatreDay()
        {
            _dVetoThink = _dVetoIron = _dVetoBk = _dBkNext = _dBkNone = _dBkNull = _dOutSiegeAi = _dOutTaken = 0;
            _dSaid.Clear(); _dBkNotes.Clear(); _dTakenNotes.Clear();
        }

        private static bool Say(string key) { var s = S; return s != null && s.LogEnabled && _dSaid.Add(key); }

        internal static string CultureOf(Settlement st)
        {
            try { return st != null && st.Culture != null ? st.Culture.StringId : null; } catch { return null; }
        }

        internal static bool OutsideNorthCulture(string cult) { return cult != null && Array.IndexOf(NorthOutside, cult) >= 0; }

        /// <summary>Czyste (autotest): weto teatru dla kultury osady. realm - "battania" / "sturgia"; retake - odbicie wlasnej osady Polnocy; afterRw - po Krwawych Godach.</summary>
        internal static bool TheatreVetoKind(string realm, string cult, bool retake, bool afterRw)
        {
            if (cult == null) return false;
            if (realm == NorthId) return !afterRw && !retake && Array.IndexOf(NorthOutside, cult) >= 0;
            if (realm == IronId) return Array.IndexOf(afterRw ? IronOutsideAfterRw : IronOutsideBeforeRw, cult) >= 0;
            return false;
        }

        /// <summary>Osada w teatrze Polnocy (linia startu): kultura nie z listy "poza".</summary>
        private static bool NorthTheatre(Settlement st, object unused) { return !OutsideNorthCulture(CultureOf(st)); }

        private static bool Retake(Settlement st)
        {
            if (st == null || st.StringId == null) return false;
            if (_northHeld.Contains(st.StringId)) return true;
            var b = st.IsVillage && st.Village != null ? st.Village.Bound : null;
            return b != null && b.StringId != null && _northHeld.Contains(b.StringId);
        }

        /// <summary>Weto teatru dla frakcji f na osade st teraz: 0 brak, 1 teatr Polnocy, 2 teatr Wysp.</summary>
        internal static int TheatreVeto(IFaction f, Settlement st)
        {
            if (f == null || st == null) return 0;
            string id = f.StringId;
            if (id == NorthId)
            {
                if (!TheatreKey || CanonWarClock.OverNow()) return 0;
                return TheatreVetoKind(NorthId, CultureOf(st), Retake(st), false) ? 1 : 0;
            }
            if (id == IronId)
            {
                if (!IronOn) return 0;
                return TheatreVetoKind(IronId, CultureOf(st), false, CanonWarClock.OverNow()) ? 2 : 0;
            }
            return 0;
        }

        private static bool BesiegedBy(Settlement st, IFaction f)
        {
            try
            {
                var ev = st.SiegeEvent;
                var lp = ev != null && ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
                return lp != null && lp.MapFaction == f;
            }
            catch { return false; }
        }

        // ================================================================ latki
        private static bool _hooksTried;
        private static string _wBk = "BRAK", _wThink = "BRAK";

        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            var h = new Harmony("com.jeff.armoury.front334");
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) _wThink = "BRAK (CampaignEventDispatcher.AiHourlyTick)";
                else { h.Patch(m, postfix: new HarmonyMethod(typeof(WarFront334), nameof(ThinkPostfix)) { priority = 48 }); _wThink = "ok"; }
            }
            catch (Exception e) { Stumble("EnsureHooks.think", e); _wThink = "BLAD (" + e.GetType().Name + ")"; }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKArmyBehavior");
                var m = t != null ? AccessTools.Method(t, "FindArmyObjective", new[] { typeof(MobileParty), typeof(Kingdom) }) : null;
                if (t == null) _wBk = "BRAK (brak BannerKings BKArmyBehavior)";
                else if (m == null) _wBk = "BRAK (FindArmyObjective)";
                else if (m.ReturnType != typeof(ValueTuple<Settlement, Army.ArmyTypes>?)) _wBk = "BRAK (sygnatura FindArmyObjective: " + m.ReturnType.Name + ")";
                else
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(WarFront334), nameof(BkFirstPostfix)) { priority = Priority.First });
                    h.Patch(m, postfix: new HarmonyMethod(typeof(WarFront334), nameof(BkLastPostfix)) { priority = Priority.Last });
                    _wBk = "ok";
                }
            }
            catch (Exception e) { Stumble("EnsureHooks.bk", e); _wBk = "BLAD (" + e.GetType().Name + ")"; }
            _wAmp = ReflectAmp();
        }

        // ================================================================ mysl AI (priorytet 48)
        /// <summary>Postfiks (priorytet 48) CampaignEventDispatcher.AiHourlyTick: weto teatru Polnocy / Wysp, Zelazni - opcje (Z0) i port (Z1/Z2). Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                var f = party.MapFaction;
                if (f == null) return;
                string id = f.StringId;
                if (id != NorthId && id != IronId) return;
                if (!Canon) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                if (id == IronId) IronOptions(party, partyThinkParams);
                if (scores.Count > 0) TheatreThink(party, partyThinkParams, f);
                if (id == IronId) IronPort(party, partyThinkParams);
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        private static void TheatreThink(MobileParty party, PartyThinkParams p, IFaction f)
        {
            var scores = p.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item2 <= -1f) continue;
                bool siege = e.Item1.AiBehavior == AiBehavior.BesiegeSettlement, raid = e.Item1.AiBehavior == AiBehavior.RaidSettlement;
                if (!siege && !raid) continue;
                var st = e.Item1.Party as Settlement;
                if (st == null) continue;
                int kind = TheatreVeto(f, st);
                if (kind == 0) continue;
                if (siege && BesiegedBy(st, f)) continue;   // trwajace oblezenie swojej strony - dolaczenie bez weta
                var d = e.Item1;
                p.SetBehaviorScore(in d, -1f);
                if (kind == 1) _dVetoThink++; else _dVetoIron++;
                if (Say("teatr|" + party.StringId + "|" + st.StringId))
                    Log.Info(Tag + "weto teatru | dzien " + Today + " | " + Nm(party) + " (" + Nm(f) + (party.Army != null ? ", wodz armii" + (ArmyHasPlayer(party.Army) ? " z graczem" : "") : "") + ") -> "
                             + (siege ? "oblezenie " : "najazd ") + Nm(st) + " (" + st.StringId + ", kultura " + (CultureOf(st) ?? "-") + ", " + Nm(st.MapFaction) + ", ocena gry " + F2(e.Item2) + ")"
                             + (kind == 1 ? " - Polnoc do Krwawych Godow walczy na Polnocy, w Dorzeczu i na Zachodzie." : " - Wyspy: cel poza teatrem tej fazy."));
            }
        }

        private static bool ArmyHasPlayer(Army a)
        {
            try { foreach (var mp in a.Parties) if (mp != null && mp.IsMainParty) return true; } catch { }
            return false;
        }

        // ================================================================ zdarzenia: oblezenia i zdobycia poza teatrem (B334-5)
        /// <summary>Oblezenie: AI Polnocy poza teatrem (droga spoza mysli i BK - linia) i oblezenia Wysp (Z0). Nie rzuca.</summary>
        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            try
            {
                if (!Canon || ev == null || ev.BesiegedSettlement == null || ev.BesiegerCamp == null) return;
                var lead = ev.BesiegerCamp.LeaderParty;
                if (lead == null || lead.MapFaction == null) return;
                var st = ev.BesiegedSettlement;
                string id = lead.MapFaction.StringId;
                if (id == IronId) { NoteIronSiege(lead, st); return; }
                if (id != NorthId || CanonWarClock.OverNow()) return;
                if (!OutsideNorthCulture(CultureOf(st)) || Retake(st)) return;
                bool player = lead.IsMainParty;
                if (!player) _dOutSiegeAi++;
                var s = S;
                if (s != null && s.LogEnabled)
                    Log.Info(Tag + "oblezenie poza teatrem | dzien " + Today + " | " + Nm(lead) + " (" + Nm(lead.MapFaction) + (player ? ", GRACZ - bez weta" : ", AI") + ") oblega " + Nm(st)
                             + " (" + st.StringId + ", kultura " + CultureOf(st) + ")" + (player ? "." : " - weto teatru " + (TheatreKey ? "nie zadzialalo (droga spoza mysli AI i BK)" : "wylaczone (klucz NIE)") + "."));
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        /// <summary>Zmiana pana: twierdza kultury crownlands / reach / stormlands zdobyta przez AI Polnocy przed Krwawymi Godami (B334-5; zdobycz gracza - INFO). Nie rzuca.</summary>
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (!Canon || st == null || !st.IsFortification || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                var nk = newOwner != null && newOwner.Clan != null ? newOwner.Clan.Kingdom : null;
                if (nk == null || nk.StringId != NorthId || CanonWarClock.OverNow()) return;
                string cult = CultureOf(st);
                if (cult != "crownlands" && cult != "reach" && cult != "stormlands") return;
                bool player = capturer == Hero.MainHero || (capturer != null && capturer.Clan == Clan.PlayerClan);
                if (!player) { _dOutTaken++; _sOutTaken++; }
                _dTakenNotes.Add(Nm(st) + (player ? " (gracz)" : " (AI)"));
                var s = S;
                if (s != null && s.LogEnabled)
                    Log.Info(Tag + "zdobycie poza teatrem | dzien " + Today + " | " + Nm(st) + " (" + st.StringId + ", kultura " + cult + ") zdobyte przez " + Nm(capturer) + " dla Polnocy"
                             + (player ? " - GRACZ (wolnosc gracza, zdobycz zostaje u niego)." : " - AI (326b zwroci; weto teatru " + Key01(TheatreKey) + ")."));
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>Linia 4 (teatr i weta) i 5 (BK nastepny cel - linia doby).</summary>
        private static void TheatreLine(int today)
        {
            Log.Info(Tag + "dzien " + today + " | teatr: klucz " + Key01(TheatreKey) + (CanonWarClock.OverNow() ? " (po Krwawych Godach - nieczynny)" : "") + ", weta mysli Polnocy " + _dVetoThink
                     + ", Wysp " + _dVetoIron + ", weta BK " + _dVetoBk + ", oblezenia AI Polnocy poza teatrem " + _dOutSiegeAi
                     + ", cele poza teatrem zdobyte przez AI Polnocy " + _dOutTaken + " (sesja " + _sOutTaken + ")" + (_dTakenNotes.Count > 0 ? " [" + string.Join(", ", _dTakenNotes.ToArray()) + "]" : "")
                     + ", osad na odbicie " + _northHeld.Count
                     + " | BK nastepny cel: klucz " + Key01(BkNextOn) + ", zamian " + _dBkNext + ", bez innego celu " + _dBkNone + ", BK null " + _dBkNull
                     + (_dBkNotes.Count > 0 ? " [" + string.Join("; ", _dBkNotes.ToArray()) + "]" : "") + ".");
        }

        // ================================================================ B8: BK
        private static bool VetoedAny(Kingdom k, Settlement st, out string why)
        {
            why = null;
            int v = 0;
            try { v = CanonWarBalance.VetoFor(k, st); } catch { }
            if (v == 1) { why = "siedziba 274"; return true; }
            if (v == 2) { why = "za frontem 274"; return true; }
            int t = TheatreVeto(k, st);
            if (t != 0) { why = t == 1 ? "teatr Polnocy" : "teatr Wysp"; return true; }
            try { if (NorthConquest.VetoedFor(k, st)) { why = "zwrot 326b"; return true; } } catch { }
            return false;
        }

        /// <summary>Postfiks First BKArmyBehavior.FindArmyObjective: cel oblezenia z wetem -> nastepny cel wedlug reguly BK. Nie rzuca.</summary>
        public static void BkFirstPostfix(MobileParty __0, Kingdom __1, ref ValueTuple<Settlement, Army.ArmyTypes>? __result)
        {
            _bkReplaced = false;
            if (!__result.HasValue || __0 == null || __1 == null) return;
            try
            {
                var v = __result.Value;
                if (v.Item2 != Army.ArmyTypes.Besieger || v.Item1 == null || !Canon) return;
                string why;
                if (!VetoedAny(__1, v.Item1, out why)) return;
                if (TheatreVeto(__1, v.Item1) != 0) _dVetoBk++;
                if (!BkNextOn) return;   // 167: 274 / 326b daja null jak dotad; teatr ponizej (BkLastPostfix)
                var next = NextBkTarget(__0, __1);
                if (next == null) { _dBkNone++; if (TheatreVeto(__1, v.Item1) != 0) __result = null; return; }
                __result = new ValueTuple<Settlement, Army.ArmyTypes>(next, Army.ArmyTypes.Besieger);
                _bkReplaced = true;
                _dBkNext++;
                if (_dBkNotes.Count < 8) _dBkNotes.Add(Nm(__0) + ": " + Nm(v.Item1) + " (" + why + ") -> " + Nm(next));
                if (Say("bk|" + __1.StringId + "|" + v.Item1.StringId + "|" + next.StringId))
                    Log.Info(Tag + "BK nastepny cel | dzien " + Today + " | " + Nm(__0) + " (" + Nm(__1) + ") -> zawetowany " + Nm(v.Item1) + " (" + why + ") -> nowy " + Nm(next)
                             + " (odl. " + F0(Math.Sqrt(__0.GetPosition2D.DistanceSquared(next.GetPosition2D))) + ").");
            }
            catch (Exception e) { Stumble("BkFirstPostfix", e); }
        }

        /// <summary>Postfiks Last: BK null mimo zamiany (ktos dal null po nas) - licznik B334-18; teatr przy kluczu BK NIE - null (jak weto 274). Nie rzuca.</summary>
        public static void BkLastPostfix(MobileParty __0, Kingdom __1, ref ValueTuple<Settlement, Army.ArmyTypes>? __result)
        {
            try
            {
                if (_bkReplaced && !__result.HasValue) _dBkNull++;
                _bkReplaced = false;
                if (!__result.HasValue || BkNextOn) return;
                var v = __result.Value;
                if (v.Item2 != Army.ArmyTypes.Besieger || v.Item1 == null || __1 == null) return;
                if (TheatreVeto(__1, v.Item1) != 0 && !BesiegedBy(v.Item1, __1)) __result = null;
            }
            catch (Exception e) { Stumble("BkLastPostfix", e); }
        }

        /// <summary>BK FindArmyObjective (galaz oblezenia) bez wetowanych: wrogie krolestwa, twierdza z panem, nie oblegana, brama w 280, najblizsza z droga ladem (0, 50000).</summary>
        private static Settlement NextBkTarget(MobileParty party, Kingdom k)
        {
            var pos = party.GetPosition2D;
            var cand = new List<KeyValuePair<Settlement, float>>();
            foreach (var ek in FactionHelper.GetEnemyKingdoms(k))
            {
                if (ek == null) continue;
                foreach (var st in ek.Settlements)
                {
                    if (st == null || !st.IsFortification || st.OwnerClan == null || st.IsUnderSiege) continue;
                    float d2 = pos.DistanceSquared(st.GatePosition.ToVec2());
                    if (d2 < 78400f) cand.Add(new KeyValuePair<Settlement, float>(st, d2));
                }
            }
            cand.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var c in cand)
            {
                string why;
                if (VetoedAny(k, c.Key, out why)) continue;
                float dist, lr;
                try { dist = Campaign.Current.Models.MapDistanceModel.GetDistance(party, c.Key, false, MobileParty.NavigationType.Default, out lr); }
                catch { continue; }
                if (dist > 0f && dist < 50000f) return c.Key;
            }
            return null;
        }
    }
}
