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
    ///  B7 (po recenzji 334 - BIALA lista jak w projekcie): AI Polnocy (druzyny, wodz armii - takze armii, w ktorej jedzie gracz; BK) nie zaczyna oblezenia ani najazdu
    ///      osady, ktorej Settlement.Culture (ROT-Map, nie zmienia sie po zdobyciu) NIE jest z teatru: battania (Polnoc), river (Dorzecze), vlandia (Westerlands) i ziemie
    ///      za Murem / przy Polnocy (freefolk, wildlings, nightswatch, skagosi). Poza teatrem m.in. crownlands, reach, stormlands, dragonstone, vale, aserai, sturgia
    ///      (rdzen Wysp) i Essos - Robb wygrywa w Dorzeczu i na Zachodzie, nie podbija ziem koronnych ani Reach i nie plynie na Pyke. Wyjatek: odbicie osady spoza
    ///      teatru, ktora z danych ROT nalezy do Polnocy i Polnoc ja trzymala (lista co dobe; zdobycze spoza ziem Polnocy zwraca 326b - tylko przy kluczu TAK, od 466 domyslnie NIE). Trwajace oblezenie Polnocy -
    ///      dolaczenie bez weta. Obrona i odsiecz bez zmian.
    ///      Druzyna gracza nie ma weta (wybor gracza; jego zdobycz zostaje u niego - 326b pomija gracza). Faza: do Krwawych Godow (CanonWarClock - ta sama regula co 264/274).
    ///  B9 teatr Wysp: do Krwawych Godow bez celow kultury reach, stormlands, dragonstone, vale, aserai; po Krwawych Godach Reach dozwolony (wojna z nim to osobna paczka).
    ///  Mechanizm jak weto siedzib 274: postfiks (priorytet 48) CampaignEventDispatcher.AiHourlyTick - ocena -1 (ponizej startu wyboru gry), BK - ponizej.
    ///  B8: postfiks BKArmyBehavior.FindArmyObjective z priorytetem First (przed 274 i 326b): cel oblezenia z wetem (siedziba / powsciagliwosc 274, teatr 334, zwrot
    ///      326b) -> nastepna wroga twierdza wedlug reguly BK (wrogie krolestwa, twierdza z panem i bez oblezenia, brama w 280, najblizsza z droga ladem 0..50000 -
    ///      BK NearestReachable) bez weta; null tylko, gdy takiej nie ma. Postfiks Last liczy "BK null przy wecie" (cel byl, a ktos i tak dal null) - ma byc 0.
    /// </summary>
    internal static partial class WarFront334
    {
        private static readonly string[] NorthTheatreCultures = { "battania", "river", "vlandia", "freefolk", "wildlings", "nightswatch", "skagosi" };
        private static readonly string[] IronOutsideBeforeRw = { "reach", "stormlands", "dragonstone", "vale", "aserai" };
        private static readonly string[] IronOutsideAfterRw = { "stormlands", "dragonstone", "vale", "aserai" };

        private static int _dVetoCrown;
        // 465 (Jeff 12.11, propozycja 1): do Krwawych Godow Polnoc na Zachodzie tylko najazdy i male zamki - jak w kanonie (Robb bierze Crag, Ashemark, bije pod Oxcross
        // i pali wsie, ale nie oblega Casterly Rock ani miast Zachodu). Lista z ROT-Map settlements.xml (kultura vlandia, Town bez is_castle = miasta Zachodu):
        //   castle_V5 Casterly Rock - siedziba Lannisterow, w kanonie nie do wziecia sila (w ROT zamek, ale glowna twierdza Zachodu);
        //   town_V9 Lannisport, town_V8 Kayce, ROT_town29 Hornvale - jedyne miasta kultury vlandia w ROT-Map (Robb zadnego nie oblegal).
        // Pozostale zamki Zachodu (Crakehall, Clegane's Keep, Goldentooth, Cornfield, Crag, Deep Den, Banefort, Silverhill, Ashemark, Faircastle, Sarsfield) - wolno.
        private static readonly string[] WestSeats465 = { "castle_V5", "town_V9", "town_V8", "ROT_town29" };
        private static int _dVetoWest, _dVetoWestBk, _dWestSiegeAi;
        private static int _dVetoThink, _dVetoIron, _dVetoBk, _dBkNext, _dBkNone, _dBkNull, _dBkIron, _dOutSiegeAi, _dOutTaken, _sOutTaken;
        private static readonly HashSet<string> _dSaid = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> _dBkNotes = new List<string>();
        private static readonly List<string> _dTakenNotes = new List<string>();
        [ThreadStatic] private static bool _bkReplaced;

        private static void ResetTheatre() { ZeroTheatreDay(); _sOutTaken = 0; }   // latki raz na proces (_hooksTried bez zerowania - bez podwojnego wpiecia po wczytaniu)

        private static void ZeroTheatreDay()
        {
            _dVetoCrown = 0;
            _dVetoThink = _dVetoIron = _dVetoBk = _dBkNext = _dBkNone = _dBkNull = _dBkIron = _dOutSiegeAi = _dOutTaken = 0;
            _dVetoWest = _dVetoWestBk = _dWestSiegeAi = 0;
            _dSaid.Clear(); _dBkNotes.Clear(); _dTakenNotes.Clear();
        }

        private static bool Say(string key) { var s = S; return s != null && s.LogEnabled && _dSaid.Add(key); }

        internal static string CultureOf(Settlement st)
        {
            try { return st != null && st.Culture != null ? st.Culture.StringId : null; } catch { return null; }
        }

        /// <summary>Kultura w teatrze Polnocy (biala lista). null - brak kultury: w teatrze (bez weta).</summary>
        internal static bool InNorthTheatre(string cult) { return cult == null || Array.IndexOf(NorthTheatreCultures, cult) >= 0; }

        internal static bool OutsideNorthCulture(string cult) { return !InNorthTheatre(cult); }

        /// <summary>Czyste (autotest): weto teatru dla kultury osady. realm - "battania" / "sturgia"; retake - odbicie wlasnej osady Polnocy; afterRw - po Krwawych Godach.</summary>
        internal static bool TheatreVetoKind(string realm, string cult, bool retake, bool afterRw)
        {
            if (cult == null) return false;
            if (realm == NorthId) return !afterRw && !retake && !InNorthTheatre(cult);
            if (realm == IronId) return Array.IndexOf(afterRw ? IronOutsideAfterRw : IronOutsideBeforeRw, cult) >= 0;
            return false;
        }

        internal static bool WestSeatsOn { get { var s = S; return s != null && s.NorthSparesWestSeats465 && Canon; } }

        /// <summary>Czyste (autotest) 465: zakaz oblezenia wielkiej siedziby / miasta Zachodu przez Polnoc do Krwawych Godow (po StringId osady).</summary>
        internal static bool WestSeatVetoKind(string realm, string sid, bool afterRw)
        {
            return realm == NorthId && !afterRw && sid != null && Array.IndexOf(WestSeats465, sid) >= 0;
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

        /// <summary>334e (recenzja W5): ziemie Polnocy - korona i jej strona nie szly za Przesmyk do Krwawych Godow (na Polnoc szli tylko Zelazni, potem Boltonowie).
        /// Hamulcem bylo weto 274 "za frontem"; sila lokalna je zdejmuje, wiec przy kluczu LocalRestraint334e zostaje teatr korony.</summary>
        private static readonly string[] CrownOutsideBeforeRw = { "battania", "skagosi", "freefolk", "wildlings", "nightswatch" };
        private static int _crownSideDay = -1, _crownSide;

        private static int CrownSide()
        {
            int d = Today;
            if (_crownSideDay != d) { var c = FindKingdom(CrownId); _crownSide = c != null ? CanonWarBalance.SideOf(c) : 0; _crownSideDay = d; }
            return _crownSide;
        }

        /// <summary>Weto teatru dla frakcji f na osade st teraz: 0 brak, 1 teatr Polnocy, 2 teatr Wysp, 3 teatr korony (334e), 4 wielka siedziba / miasto Zachodu (465).</summary>
        internal static int TheatreVeto(IFaction f, Settlement st)
        {
            if (f == null || st == null) return 0;
            string id = f.StringId;
            if (id != NorthId && id != IronId)
            {
                if (!LocalFront334e.RestraintOff || CanonWarClock.OverNow()) return 0;
                int cs = CrownSide();
                if ((id == CrownId || (cs != 0 && CanonWarBalance.SideOf(f) == cs)) && Array.IndexOf(CrownOutsideBeforeRw, CultureOf(st)) >= 0 && !SameSideHolds(st, f)) return 3;
                return 0;
            }
            if (id == NorthId)
            {
                if (CanonWarClock.OverNow()) return 0;
                if (TheatreKey && TheatreVetoKind(NorthId, CultureOf(st), Retake(st), false)) return 1;
                if (WestSeatsOn && WestSeatVetoKind(NorthId, st.StringId, false)) return 4;   // 465: same twierdze z listy - najazdy na ich wsie bez weta
                return 0;
            }
            if (id == IronId)
            {
                if (!IronOn) return 0;
                return TheatreVetoKind(IronId, CultureOf(st), false, CanonWarClock.OverNow()) ? 2 : 0;
            }
            return 0;
        }

        /// <summary>Osada w rekach strony f (obrona / odbicie wlasnej - bez weta teatru korony).</summary>
        private static bool SameSideHolds(Settlement st, IFaction f)
        {
            var o = st.MapFaction;
            if (o == null) return false;
            if (o == f) return true;
            int s = CanonWarBalance.SideOf(f);
            return s != 0 && CanonWarBalance.SideOf(o) == s;
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
        internal static string BkHookText { get { return _wBk == "ok" ? "TAK" : _wBk; } }   // 406: linia startu pogrzebow

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
                if (!Canon) return;
                if (id != NorthId && id != IronId)
                {
                    // 334e: teatr korony (tylko przy zdjetym "za frontem" 274)
                    if (!LocalFront334e.RestraintOff) return;
                    var sc = partyThinkParams.AIBehaviorScores;
                    if (sc != null && sc.Count > 0) TheatreThink(party, partyThinkParams, f);
                    return;
                }
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                if (scores.Count > 0) TheatreThink(party, partyThinkParams, f);
                if (id == IronId)
                {
                    // po recenzji 334: opcje liczone PO wecie teatru Wysp (cele w Reach -1 sie nie licza); slowniki Zelaznych pod zamkiem (mysl AI moze isc rownolegle)
                    lock (_ironLock)
                    {
                        IronOptions(party, partyThinkParams);
                        IronPort(party, partyThinkParams);
                    }
                }
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
                if (kind == 1) _dVetoThink++; else if (kind == 3) _dVetoCrown++; else if (kind == 4) _dVetoWest++; else _dVetoIron++;
                if (Say("teatr|" + party.StringId + "|" + st.StringId))
                    Log.Info(Tag + "weto teatru | dzien " + Today + " | " + Nm(party) + " (" + Nm(f) + (party.Army != null ? ", wodz armii" + (ArmyHasPlayer(party.Army) ? " z graczem" : "") : "") + ") -> "
                             + (siege ? "oblezenie " : "najazd ") + Nm(st) + " (" + st.StringId + ", kultura " + (CultureOf(st) ?? "-") + ", " + Nm(st.MapFaction) + ", ocena gry " + F2(e.Item2) + ")"
                             + (kind == 1 ? " - Polnoc do Krwawych Godow walczy na Polnocy, w Dorzeczu i na Zachodzie." : kind == 4 ? " - Polnoc do Krwawych Godow nie oblega Casterly Rock ani miast Zachodu (465)." : kind == 3 ? " - korona do Krwawych Godow nie idzie za Przesmyk (334e)." : " - Wyspy: cel poza teatrem tej fazy."));
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
                if (WestSeatVetoKind(NorthId, st.StringId, false))
                {
                    // 465: oblezenie wielkiej siedziby / miasta Zachodu mimo zakazu (droga spoza mysli AI i BK) - tylko pomiar
                    bool pl = lead.IsMainParty;
                    if (!pl) _dWestSiegeAi++;
                    var s5 = S;
                    if (s5 != null && s5.LogEnabled)
                        Log.Info(Tag + "oblezenie siedziby Zachodu | dzien " + Today + " | " + Nm(lead) + " (" + Nm(lead.MapFaction) + (pl ? ", GRACZ - bez weta" : ", AI") + ") oblega " + Nm(st)
                                 + " (" + st.StringId + ")" + (pl ? "." : " - zakaz 465 " + (WestSeatsOn ? "nie zadzialal (droga spoza mysli AI i BK)" : "wylaczony (klucz NIE)") + "."));
                    return;
                }
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

        /// <summary>Zmiana pana: twierdza spoza teatru Polnocy (te same kultury co weto, bez odbicia) zdobyta przez AI Polnocy przed Krwawymi Godami (B334-5; zdobycz gracza - INFO). Nie rzuca.</summary>
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (!Canon || st == null || !st.IsFortification || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                var nk = newOwner != null && newOwner.Clan != null ? newOwner.Clan.Kingdom : null;
                if (nk == null || nk.StringId != NorthId || CanonWarClock.OverNow()) return;
                string cult = CultureOf(st);
                if (!OutsideNorthCulture(cult) || Retake(st)) return;   // po recenzji 334: licznik tak szeroki jak weto (takze Dragonstone, Vale, Dorne, Wyspy)
                bool player = capturer == Hero.MainHero || (capturer != null && capturer.Clan == Clan.PlayerClan);
                if (!player) { _dOutTaken++; _sOutTaken++; }
                _dTakenNotes.Add(Nm(st) + (player ? " (gracz)" : " (AI)"));
                var s = S;
                if (s != null && s.LogEnabled)
                    Log.Info(Tag + "zdobycie poza teatrem | dzien " + Today + " | " + Nm(st) + " (" + st.StringId + ", kultura " + cult + ") zdobyte przez " + Nm(capturer) + " dla Polnocy"
                             + (player ? " - GRACZ (wolnosc gracza, zdobycz zostaje u niego)." : " - AI (" + (NorthConquest.On ? "326b zwroci" : "326b NIE - zostaje u Polnocy (466)") + "; weto teatru " + Key01(TheatreKey) + ")."));
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>Linia 4 (teatr i weta) i 5 (BK nastepny cel - linia doby).</summary>
        private static void TheatreLine(int today)
        {
            Log.Info(Tag + "dzien " + today + " | teatr: klucz " + Key01(TheatreKey) + (CanonWarClock.OverNow() ? " (po Krwawych Godach - nieczynny)" : "") + ", weta mysli Polnocy " + _dVetoThink
                     + ", Wysp " + _dVetoIron + ", korony 334e " + _dVetoCrown + ", weta BK " + _dVetoBk + ", oblezenia AI Polnocy poza teatrem " + _dOutSiegeAi
                     + ", cele poza teatrem zdobyte przez AI Polnocy " + _dOutTaken + " (sesja " + _sOutTaken + ")" + (_dTakenNotes.Count > 0 ? " [" + string.Join(", ", _dTakenNotes.ToArray()) + "]" : "")
                     + ", osad na odbicie " + _northHeld.Count
                     + " | siedziby Zachodu 465: klucz " + Key01(WestSeatsOn) + ", weta mysli " + _dVetoWest + ", weta BK " + _dVetoWestBk + ", oblezenia AI mimo zakazu " + _dWestSiegeAi
                     + " | BK nastepny cel: klucz " + Key01(BkNextOn) + ", zamian " + _dBkNext + ", bez innego celu " + _dBkNone + ", BK null " + _dBkNull + ", cele Wysp (lad i morze) " + _dBkIron
                     + (_dBkNotes.Count > 0 ? " [" + string.Join("; ", _dBkNotes.ToArray()) + "]" : "") + ".");
        }

        // ================================================================ B8: BK
        private static bool VetoedAny(Kingdom k, Settlement st, out string why) { bool local; return VetoedAny(k, st, out why, null, out local); }

        /// <summary>334e: weta celu (siedziba / za frontem 274, teatr Polnocy / Wysp / korony, zwrot 326b) - dla opcji dodawanych po wetach (SiegeWalls2.ThinkPost Last:
        /// armie lupia, Zelazni, KONTYNUUJ bez wpisu oblezenia). Bez sily lokalnej BK.</summary>
        internal static bool VetoedFor334e(Kingdom k, Settlement st, out string why) { bool local; return VetoedAny(k, st, out why, null, out local); }

        /// <summary>334e (B8): z wodzem BK - cel z q > 1.0 (sila wroga, ktora zdazy, wobec wodza i kandydatow zbiorki z modelu armii) jak zawetowany (local = true).</summary>
        private static bool VetoedAny(Kingdom k, Settlement st, out string why, MobileParty leader, out bool local)
        {
            why = null; local = false;
            if (FuneralRites.TruceVeto(st)) { why = "pokoj pogrzebu 406"; return true; }   // 406: siedziba / wies w pokoju pogrzebu (nie trwajace oblezenie); bez pogrzebow jedno porownanie
            int v = 0;
            try { v = CanonWarBalance.VetoFor(k, st); } catch { }
            if (v == 1) { why = "siedziba 274"; return true; }
            if (v == 2) { why = "za frontem 274"; return true; }
            int t = TheatreVeto(k, st);
            if (t != 0) { why = t == 1 ? "teatr Polnocy" : t == 4 ? "siedziba Zachodu 465" : t == 3 ? "teatr korony" : "teatr Wysp"; return true; }
            try { if (NorthConquest.VetoedFor(k, st)) { why = "zwrot 326b"; return true; } } catch { }
            if (leader != null)
            {
                double q;
                if (LocalFront334e.BkBlocked(leader, k, st, out q)) { why = "sila lokalna 334e q " + q.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); local = true; return true; }
            }
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
                if (v.Item2 != Army.ArmyTypes.Besieger || v.Item1 == null) return;
                // 406: pokoj pogrzebu - w kazdej kampanii (takze poza kanonem) i przy kazdym kluczu: nastepny cel wedlug reguly BK (kanon + klucz BK nastepny cel) albo brak celu
                if (FuneralRites.BkVeto(v.Item1))
                {
                    var nf = Canon && BkNextOn ? NextBkTarget(__0, __1) : null;   // NextBkTarget pomija cele w pokoju (VetoedAny)
                    if (nf == null) { __result = null; return; }
                    __result = new ValueTuple<Settlement, Army.ArmyTypes>(nf, Army.ArmyTypes.Besieger);
                    _bkReplaced = true;
                    return;
                }
                if (!Canon) return;
                string why; bool local;
                if (!VetoedAny(__1, v.Item1, out why, __0, out local)) return;
                int tv = TheatreVeto(__1, v.Item1);
                if (tv != 0) _dVetoBk++;
                if (tv == 4 && why == "siedziba Zachodu 465") _dVetoWestBk++;   // 465-p: Skala przy czynnym 274 - weto liczy 274 ("siedziba 274"), nie 465
                // po recenzji 334e (W9): sila lokalna nie daje null - bez nastepnego celu zostaje wybor BK, a cel wyzeruje ocena celu (ScorePost) w mysli armii
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

        /// <summary>Postfiks Last: BK null mimo zamiany (ktos dal null po nas) - licznik B334-18; teatr przy kluczu BK NIE - null (jak weto 274).
        /// Po recenzji 334 (K3): Zelazne Wyspy (klucz Zelaznych i BK nastepny cel) - BK szuka celu tylko z droga ladem, a z Pyke ladem nie ma zadnej twierdzy; gdy BK
        /// nie dal celu, cel oblezenia wedlug tej samej reguly z droga "lad i morze" (statki), bez wetowanych - jedyna przyczyna "armii 0", ktorej 334 nie ruszalo. Nie rzuca.</summary>
        public static void BkLastPostfix(MobileParty __0, Kingdom __1, ref ValueTuple<Settlement, Army.ArmyTypes>? __result)
        {
            try
            {
                if (_bkReplaced && !__result.HasValue) _dBkNull++;
                _bkReplaced = false;
                if (!__result.HasValue && __0 != null && __1 != null && __1.StringId == IronId && IronOn && BkNextOn && Canon)
                {
                    var t = NextBkTarget(__0, __1);
                    if (t != null)
                    {
                        __result = new ValueTuple<Settlement, Army.ArmyTypes>(t, Army.ArmyTypes.Besieger);
                        _dBkIron++;
                        if (_dBkNotes.Count < 8) _dBkNotes.Add(Nm(__0) + ": BK bez celu -> " + Nm(t) + " (Wyspy, lad i morze)");
                        if (Say("bkiron|" + t.StringId))
                            Log.Info(Tag + "BK cel Wysp | dzien " + Today + " | " + Nm(__0) + " -> " + Nm(t) + " (" + t.StringId + ", kultura " + (CultureOf(t) ?? "-") + ", " + Nm(t.MapFaction)
                                     + ") - BK szuka tylko droga ladem; cel wedlug reguly BK z droga lad i morze.");
                    }
                    return;
                }
                if (!__result.HasValue || BkNextOn) return;
                var v = __result.Value;
                if (v.Item2 != Army.ArmyTypes.Besieger || v.Item1 == null || __1 == null) return;
                if (TheatreVeto(__1, v.Item1) != 0 && !BesiegedBy(v.Item1, __1)) __result = null;
            }
            catch (Exception e) { Stumble("BkLastPostfix", e); }
        }

        /// <summary>BK FindArmyObjective (galaz oblezenia) bez wetowanych: wrogie krolestwa, twierdza z panem, nie oblegana, brama w 280, najblizsza z droga ladem (0, 50000).
        /// Po recenzji 334 (K3, drobne 8): wodz ze statkami - droga "lad i morze" (NavigationType.All) i brama w max(280, prog najazdu gry 5 x srednia odleglosc dwoch
        /// najblizszych miast) - z wyspy ladem nie ma celu.</summary>
        private static Settlement NextBkTarget(MobileParty party, Kingdom k)
        {
            var pos = party.GetPosition2D;
            bool sea = party.Ships != null && party.Ships.Count > 0;
            var nav = sea ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
            float gate2 = 78400f;
            if (sea)
            {
                try { float r = 5f * Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All); if (r * r > gate2) gate2 = r * r; } catch { }
            }
            var cand = new List<KeyValuePair<Settlement, float>>();
            foreach (var ek in FactionHelper.GetEnemyKingdoms(k))
            {
                if (ek == null) continue;
                foreach (var st in ek.Settlements)
                {
                    if (st == null || !st.IsFortification || st.OwnerClan == null || st.IsUnderSiege) continue;
                    float d2 = pos.DistanceSquared(st.GatePosition.ToVec2());
                    if (d2 < gate2) cand.Add(new KeyValuePair<Settlement, float>(st, d2));
                }
            }
            cand.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var c in cand)
            {
                string why;
                bool loc;
                if (VetoedAny(k, c.Key, out why, party, out loc)) continue;
                float dist, lr;
                try { dist = Campaign.Current.Models.MapDistanceModel.GetDistance(party, c.Key, false, nav, out lr); }
                catch { continue; }
                if (dist > 0f && dist < 50000f) return c.Key;
            }
            return null;
        }
    }
}
