using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CrashScribe
{
    /// <summary>
    /// Kronika wojen: po wczytaniu save'a i raz dziennie spisuje do logu,
    /// KTO z KIM wojuje (krolestwo vs krolestwo) oraz stan fabularnych
    /// wydarzen ROT (ktore juz odpalily, ktore trwaja). Jeff pyta "czy Robb
    /// walczy z poludniem?" - odpowiedz ma lezec w Scribe.log czarno na bialym.
    /// </summary>
    internal class WarReportBehavior : CampaignBehaviorBase
    {
        private double _lastDay = -10.0;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this,
                delegate (TaleWorlds.CampaignSystem.CampaignGameStarter s) { Mends.FeedNamelessCultures(); Report("po wczytaniu"); Mends.LocalLevies(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { Report("dzien"); Mends.LocalLevies(); });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, WatchArmy);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnEntered);
        }

        /// <summary>
        /// KTO STOI W OSADZIE. Banner Kings dobiera ochotnikow wedle kultury
        /// NOTABLA, nie osady - wiec kaplan czy starszyzna z obcej kultury
        /// wystawia obce wojsko (Jeff: "wolni ludzie do rekrutacji w wiosce
        /// Polnocy?"). Przy kazdym wejsciu spisujemy notabli z ich kultura
        /// i profesja, zeby bylo widac czarno na bialym, skad sie to bierze.
        /// </summary>
        private void OnEntered(TaleWorlds.CampaignSystem.Party.MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                if (settlement == null) return;
                if (party != TaleWorlds.CampaignSystem.Party.MobileParty.MainParty) return;
                var sb = new StringBuilder();
                sb.Append("OSADA ").Append(settlement.Name)
                  .Append(" | kultura osady: ")
                  .Append(settlement.Culture != null ? settlement.Culture.StringId : "?")
                  .Append(" | wlada: ")
                  .Append(settlement.OwnerClan != null ? settlement.OwnerClan.Name.ToString() : "?")
                  .Append(" | notable:");
                var list = settlement.Notables;
                if (list == null || list.Count == 0) sb.Append(" BRAK");
                else
                    foreach (var h in list)
                    {
                        if (h == null) continue;
                        // ochotnicy w slotach - rozstrzyga "puste okno rekrutacji":
                        // stan (0/6 wszedzie) czy zepsute UI (sloty pelne, okno puste)
                        int vols = 0, slots = 0;
                        try
                        {
                            var vt = h.VolunteerTypes;
                            if (vt != null) { slots = vt.Length; foreach (var v in vt) if (v != null) vols++; }
                        }
                        catch { }
                        sb.Append(" [").Append(h.Name).Append(" / ")
                          .Append(h.Culture != null ? h.Culture.StringId : "?").Append(" / ")
                          .Append(h.CharacterObject != null ? h.CharacterObject.Occupation.ToString() : "?")
                          .Append(" / ochotnicy ").Append(vols).Append("/").Append(slots)
                          .Append("]");
                    }
                Scribe.Line(sb.ToString());
                Mends.LocalLevies(settlement);   // wchodzisz - prostujemy od reki, nie za dobe
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "WarReport.OnEntered", null); } catch { } }
        }

        // ------------------------------------------------------------ podglad armii
        // Jeff siedzi zaciezny w armii i widzi bezsensowne lazenie tam i z powrotem.
        // Co godzine sprawdzamy, CZEGO armia chce (rozkaz + cel + spojnosc + jedzenie)
        // i logujemy TYLKO ZMIANY - jesli dowodca miota sie miedzy dwoma celami,
        // w logu wyjdzie z tego pilka ping-pongowa z nazwiskami i godzinami.
        private string _lastArmyLine = "";

        /// <summary>Oddzial lorda, u ktorego gracz sluzy w ROT (EnlistedParty) - odbicie w ciemno.</summary>
        private static TaleWorlds.CampaignSystem.Party.MobileParty RotEnlistedParty()
        {
            try
            {
                var sub = Type.GetType("ROT.SubModule, ROT");
                if (sub == null) return null;
                var f = sub.GetField("EnlistmentBehavior",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var beh = f != null ? f.GetValue(null) : null;
                if (beh == null) return null;
                var p = beh.GetType().GetProperty("EnlistedParty",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return p != null ? p.GetValue(beh, null) as TaleWorlds.CampaignSystem.Party.MobileParty : null;
            }
            catch { return null; }
        }

        private void WatchArmy()
        {
            try
            {
                var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                var attached = mp != null ? (mp.Army != null ? mp : (mp.AttachedTo != null ? mp.AttachedTo : null)) : null;
                var army = attached != null ? attached.Army : null;

                // sledzony oddzial: dowodca armii ALBO lord, u ktorego sluzymy w ROT
                TaleWorlds.CampaignSystem.Party.MobileParty lp = null;
                string label = null;
                if (army != null && army.LeaderParty != null)
                {
                    lp = army.LeaderParty;
                    label = "ARMIA [" + (army.Name != null ? army.Name.ToString() : "?") + "]";
                }
                else
                {
                    lp = RotEnlistedParty();
                    if (lp != null) label = "SLUZBA";
                }
                if (lp == null) { _lastArmyLine = ""; return; }

                string line = label + " dowodca " +
                              (lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : "?") +
                              " | rozkaz " + lp.DefaultBehavior +
                              (lp.TargetSettlement != null ? " -> " + lp.TargetSettlement.Name : "") +
                              (lp.TargetParty != null ? " -> " + lp.TargetParty.Name + (lp.TargetParty.IsCurrentlyAtSea ? " (NA MORZU)" : "") : "") +
                              " | krotki " + lp.ShortTermBehavior +
                              (army != null ? " | spojnosc " + ((int)army.Cohesion) : "") +
                              " | jedzenie " + ((int)lp.Food);
                if (line == _lastArmyLine) return;
                _lastArmyLine = line;
                Scribe.Line(line + "  (" + CampaignTime.Now.ToString() + ")");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "WatchArmy", null); } catch { } }
        }

        public override void SyncData(IDataStore dataStore) { }

        private void Report(string when)
        {
            try
            {
                double today = CampaignTime.Now.ToDays;
                if (today - _lastDay < 0.9) return;          // nie czesciej niz raz na dzien
                _lastDay = today;

                var sb = new StringBuilder();
                sb.Append("KRONIKA WOJEN (").Append(when).Append(", ")
                  .Append(CampaignTime.Now.ToString()).Append("): ");
                int wars = 0;
                var all = Kingdom.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var a = all[i];
                    if (a == null || a.IsEliminated) continue;
                    for (int j = i + 1; j < all.Count; j++)
                    {
                        var b = all[j];
                        if (b == null || b.IsEliminated) continue;
                        bool war = false;
                        try { war = FactionManager.IsAtWarAgainstFaction(a, b); } catch { }
                        if (!war) continue;
                        if (wars > 0) sb.Append("; ");
                        sb.Append(a.Name).Append(" vs ").Append(b.Name);
                        wars++;
                    }
                }
                if (wars == 0) sb.Append("POKOJ WSZEDZIE - zadne krolestwo nie prowadzi wojny.");
                Scribe.Line(sb.ToString());

                ReportRotEvents();
                ReportUndead();
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "WarReport", null); } catch { } }
        }

        /// <summary>
        /// RAPORT UMARLYCH (Jeff: "czy umarli cos robia, czy jest Nocny Krol?").
        /// Czytamy z ROT: Nocny Krol, stan klanu Innych (bandy/ludzie/osady),
        /// czy inwazja na poludnie ruszyla - a jesli nie, ILE osad za Murem
        /// jeszcze musza zdobyc, zeby ruszyla (taki jest warunek w kodzie ROT).
        /// </summary>
        private void ReportUndead()
        {
            try
            {
                var tOthers = Type.GetType("ROT.CampaignBehaviors.ROTOthersCampaignBehavior, ROT");
                if (tOthers == null) return;
                object beh = null;
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                if (mi != null) beh = mi.MakeGenericMethod(tOthers).Invoke(Campaign.Current, null);
                if (beh == null) return;

                bool invasion = false;
                try { var f = HarmonyLib.AccessTools.Field(tOthers, "IsInvasionStarted"); if (f != null) invasion = Convert.ToBoolean(f.GetValue(beh)); } catch { }
                Hero nk = null;
                try { var f = HarmonyLib.AccessTools.Field(tOthers, "_nightKing"); if (f != null) nk = f.GetValue(beh) as Hero; } catch { }

                Clan ww = null;
                try
                {
                    var tClans = Type.GetType("ROT.Misc.ROTClans, ROT");
                    var p = tClans != null ? HarmonyLib.AccessTools.Property(tClans, "WhiteWalkers") : null;
                    ww = p != null ? p.GetValue(null, null) as Clan : null;
                }
                catch { }
                if (ww == null)
                {
                    foreach (var c in Clan.All)
                        if (c != null && c.StringId == "ROTclan_126") { ww = c; break; }
                }

                int parties = 0, men = 0, fiefs = 0;
                if (ww != null)
                {
                    try
                    {
                        foreach (var wp in ww.WarPartyComponents)
                        {
                            parties++;
                            if (wp != null && wp.MobileParty != null) men += wp.MobileParty.MemberRoster.TotalManCount;
                        }
                    }
                    catch { }
                    try { fiefs = ww.Settlements.Count; } catch { }
                }

                var head = new StringBuilder("UMARLI: Nocny Krol ");
                if (nk != null) head.Append(nk.IsAlive ? "ZYJE (" + nk.Name + ")" : "MARTWY");
                else if (ww != null && ww.Leader != null) head.Append("czeka za Murem (przywodca Innych: " + ww.Leader.Name + (ww.Leader.IsAlive ? ", zyw" : ", martwy") + ")");
                else head.Append("nieznany");
                head.Append(" | Inni: ");
                head.Append(ww == null ? "BRAK KLANU" : (ww.IsEliminated ? "WYBICI DO NOGI" : parties + " band, " + men + " trupow w polu, " + fiefs + " osad"));
                head.Append(" | INWAZJA NA POLUDNIE: ").Append(invasion ? "RUSZYLA!" : "jeszcze nie");
                Scribe.Line(head.ToString());

                if (!invasion && ww != null && !ww.IsEliminated)
                {
                    // warunek inwazji z kodu ROT: WSZYSTKO za Murem (procz Muru
                    // i Driftwood Hall) w rekach Innych - liczymy, ile im brakuje
                    var tUtil = Type.GetType("ROT.Misc.ROTUtilities, ROT");
                    var mBeyond = tUtil != null ? HarmonyLib.AccessTools.Method(tUtil, "IsBeyondTheWall") : null;
                    var tSetts = Type.GetType("ROT.Misc.ROTSettlements, ROT");
                    Settlement wallS = null, driftS = null;
                    try { var p = tSetts != null ? HarmonyLib.AccessTools.Property(tSetts, "TheWall") : null; wallS = p != null ? p.GetValue(null, null) as Settlement : null; } catch { }
                    try { var p = tSetts != null ? HarmonyLib.AccessTools.Property(tSetts, "DriftwoodHall") : null; driftS = p != null ? p.GetValue(null, null) as Settlement : null; } catch { }
                    if (mBeyond != null)
                    {
                        var pt = mBeyond.GetParameters().Length > 0 ? mBeyond.GetParameters()[0].ParameterType : null;
                        int left = 0; var names = new StringBuilder();
                        foreach (var s1 in Settlement.All)
                        {
                            if (s1 == null || s1.IsHideout || s1 == wallS || s1 == driftS) continue;
                            bool beyond = false;
                            try
                            {
                                object arg = pt == typeof(TaleWorlds.Library.Vec2)
                                    ? (object)s1.GatePosition.ToVec2() : (object)s1.GatePosition;
                                beyond = Convert.ToBoolean(mBeyond.Invoke(null, new[] { arg }));
                            }
                            catch { continue; }
                            if (!beyond) continue;
                            if (s1.OwnerClan == ww) continue;
                            left++;
                            if (left <= 10) { if (names.Length > 0) names.Append(", "); names.Append(s1.Name); }
                        }
                        Scribe.Line("UMARLI: do ruszenia inwazji brakuje im " + left + " osad za Murem"
                                    + (names.Length > 0 ? ": " + names + (left > 10 ? "..." : "") : " - zaraz rusza!"));
                    }
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "WarReport.Undead", null); } catch { } }
        }

        /// <summary>Stan fabuly ROT: kazdy event z lancucha + czy juz odpalil.</summary>
        private void ReportRotEvents()
        {
            try
            {
                var tBeh = Type.GetType("ROT.CampaignBehaviors.ROTEventBehavior, ROT");
                if (tBeh == null) return;
                object beh = null;
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                if (mi != null) beh = mi.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null);
                if (beh == null) return;
                var fEvents = HarmonyLib.AccessTools.Field(tBeh, "_events");
                var listObj = fEvents != null ? fEvents.GetValue(beh) : null;
                var list = listObj as System.Collections.IEnumerable;
                if (list == null) return;

                // pusta lista = fabularne wydarzenia ROT sa WYLACZONE w ustawieniach
                // (ROTSettings.Events) - mowimy to wprost zamiast "nic/nic"
                var col = listObj as System.Collections.ICollection;
                if (col != null && col.Count == 0)
                {
                    bool evOn = true;
                    try
                    {
                        var tSet = Type.GetType("ROT.ROTSettings, ROT") ?? Type.GetType("ROT.Misc.ROTSettings, ROT");
                        var pEv = tSet != null ? HarmonyLib.AccessTools.Property(tSet, "Events") : null;
                        if (pEv != null) evOn = Convert.ToBoolean(pEv.GetValue(null, null));
                    }
                    catch { }
                    Scribe.Line(evOn
                        ? "FABULA ROT: lista wydarzen PUSTA mimo wlaczonych eventow (jeszcze nie zaladowana?)."
                        : "FABULA ROT: wydarzenia fabularne WYLACZONE w ustawieniach ROT - zadna fabula nie odpali.");
                    return;
                }

                var fired = new StringBuilder();
                var pending = new StringBuilder();
                foreach (var ev in list)
                {
                    if (ev == null) continue;
                    var t = ev.GetType();
                    int total = 0; bool active = false;
                    try
                    {
                        var pTot = HarmonyLib.AccessTools.Property(t, "TotalEvents");
                        if (pTot != null) total = Convert.ToInt32(pTot.GetValue(ev, null));
                        else { var fTot = HarmonyLib.AccessTools.Field(t, "TotalEvents"); if (fTot != null) total = Convert.ToInt32(fTot.GetValue(ev)); }
                    }
                    catch { }
                    try
                    {
                        var pAct = HarmonyLib.AccessTools.Property(t, "IsActive");
                        if (pAct != null) active = Convert.ToBoolean(pAct.GetValue(ev, null));
                        else { var fAct = HarmonyLib.AccessTools.Field(t, "IsActive"); if (fAct != null) active = Convert.ToBoolean(fAct.GetValue(ev)); }
                    }
                    catch { }
                    string name = t.Name.Replace("Event", "");
                    if (total > 0) { if (fired.Length > 0) fired.Append(", "); fired.Append(name); if (active) fired.Append("(TRWA)"); }
                    else { if (pending.Length > 0) pending.Append(", "); pending.Append(name); }
                }
                Scribe.Line("FABULA ROT odpalone: " + (fired.Length > 0 ? fired.ToString() : "nic"));
                Scribe.Line("FABULA ROT czekaja: " + (pending.Length > 0 ? pending.ToString() : "nic"));
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "WarReport.RotEvents", null); } catch { } }
        }
    }
}
