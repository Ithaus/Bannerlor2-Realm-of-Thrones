using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 369 - ROSZCZENIA GRACZA (Jeff 09.11: "czemu regula ma oddawac ten zamek polnoc go zdobyla i nalezy do polnocy musze go odbic ale tak ja jestem wlascicielem
    /// to ma byc zapisane"). DOWOD: Armoury-2026-11-09_10-21-58.log:27481 (Ashemark zdobyty przez Polnoc "od KARDA"), :28170 (rada Polnocy nadaje Mormontom),
    /// :29773 (326b ZWROT -> Marbrand, pan startowy) - Jeff kupil zamek w 367b, stracil go na wojnie, a potem nasza regula oddala go Marbrandom.
    /// REGULA:
    ///  - twierdza rodu gracza zdobyta oblezeniem (BySiege) przez kogokolwiek - ROSZCZENIE gracza (zapis arm_k369): komunikat dla gracza, linia logu;
    ///    zdobywca ja zatrzymuje (326b i cesja 330 jej nikomu nie oddaja);
    ///  - odbicie zbrojne (BySiege) przez krolestwo gracza (nowy pan w frakcji gracza): po nadaniu lenna (decyzja SettlementClaimantDecision rozstrzygnieta,
    ///    pan przydzielony) twierdza przechodzi do rodu gracza (ApplyByDefault - ten sam rod krolestwa, zaloga zostaje z osada, jak nadanie lenna w grze);
    ///    odbicie przez sam rod gracza (gracz krolem albo bez krolestwa) - od razu jego, roszczenie spelnione;
    ///  - kazda zmiana pana na rod gracza (tez darowizna, kupno) - roszczenie spelnione i znika;
    ///  - roszczenie NIE wygasa, ale dziala tylko przy odbiciu zbrojnym przez krolestwo gracza (darowizny, pokoje, cesje innych - bez skutku);
    ///    zdobycie przez innego wroga - zmienia tylko zdobywce w roszczeniu.
    /// JEDNORAZOWA NAPRAWA zapisu Jeffa (flaga w zapisie): Ashemark (ROT_castle40) u Marbrandow (ROTclan_108) ze sladem zwrotu 326b (weto Polnocy na Ashemark)
    /// i tytulem BK de iure w rodzie gracza (kupiony w 367b) - wraca do Mormontow (Polnoc; brak - rod krola Polnocy) droga BookHoldings (jak 326b, bez obsady),
    /// weto 326b zdjete, roszczenie gracza dopisane.
    /// </summary>
    internal static class PlayerClaims369
    {
        internal const string Tag = "Roszczenia gracza (369): ";
        private const string NorthId = "battania", FixSite = "ROT_castle40", FixFrom = "ROTclan_108", FixTo = "clan_battania_8";
        private const int FixMaxHours = 72;   // naprawa czeka na warunki chwilowe najwyzej tyle prob godzinnych
        private const int MaxErrors = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Claim
        {
            public string Taker = "";   // rod zdobywcy (ostatni)
            public int Lost;            // godzina utraty
            public int Pending = -1;    // godzina odbicia przez krolestwo gracza (-1 - brak)
        }

        // ---- zapis arm_k369
        private static readonly Dictionary<string, Claim> _claims = new Dictionary<string, Claim>(StringComparer.Ordinal);
        private static bool _fixDone;

        // ---- sesja
        private static int _fixTries, _fixSayDay = -1, _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _err = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _waitSaid = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static void Reset()
        {
            _claims.Clear(); _fixDone = false;
            _fixTries = 0; _fixSayDay = -1; _stumbles = 0; _errSites.Clear(); _err.Clear(); _waitSaid.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("PlayerClaims369." + where, e); } catch { }
        }

        private static int NowHour { get { return (int)CampaignTime.Now.ToHours; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        internal static bool Has(string id) { return id != null && _claims.ContainsKey(id); }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder(64);
            sb.Append("pc1|").Append(_fixDone ? "1" : "0").Append('|');
            bool first = true;
            foreach (var kv in _claims)
            {
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key).Append(':').Append(kv.Value.Taker).Append(':').Append(kv.Value.Lost.ToString(Inv)).Append(':').Append(kv.Value.Pending.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string v)
        {
            Reset();
            if (string.IsNullOrEmpty(v)) return;   // zapis sprzed 369 - naprawa czeka
            try
            {
                var p = v.Split('|');
                if (p.Length < 3 || p[0] != "pc1") { _fixDone = true; Log.Info(Tag + "zapis w nieznanym formacie - roszczen brak, naprawa uznana za zrobiona."); return; }
                _fixDone = p[1] == "1";
                if (p[2].Length == 0) return;
                foreach (var e in p[2].Split(';'))
                {
                    var f = e.Split(':');
                    if (f.Length < 4 || f[0].Length == 0) continue;
                    int lost, pend;
                    var c = new Claim { Taker = f[1] };
                    c.Lost = int.TryParse(f[2], NumberStyles.Integer, Inv, out lost) ? lost : 0;
                    c.Pending = int.TryParse(f[3], NumberStyles.Integer, Inv, out pend) ? pend : -1;
                    _claims[f[0]] = c;
                }
            }
            catch (Exception e) { _fixDone = true; Stumble("Import", e); }
        }

        internal static string ListText()
        {
            if (_claims.Count == 0) return "brak";
            var l = new List<string>();
            foreach (var kv in _claims)
            {
                var st = Settlement.Find(kv.Key);
                l.Add((st != null ? Nm(st) : kv.Key) + " [" + kv.Key + "] u " + (st != null ? Nm(st.OwnerClan) + " (" + Nm(st.MapFaction) + ")" : "-") + ", zdobywca " + kv.Value.Taker
                      + ", utracona dnia " + (kv.Value.Lost / 24) + (kv.Value.Pending >= 0 ? ", ODBITA dnia " + (kv.Value.Pending / 24) + " - czeka na przekazanie" : ""));
            }
            return string.Join("; ", l.ToArray());
        }

        internal static void SessionStart()
        {
            try
            {
                Log.Info(Tag + "start sesji | roszczen " + _claims.Count + " [" + ListText() + "] | naprawa Ashemark (raz na zapis): " + (_fixDone ? "zrobiona" : "czeka (pierwsza godzina na mapie)") + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (st == null || !st.IsFortification || st.StringId == null) return;
                var pc = Clan.PlayerClan;
                if (pc == null) return;
                var nc = newOwner != null ? newOwner.Clan : null;
                var oc = oldOwner != null ? oldOwner.Clan : null;
                int hour = NowHour;
                Claim c;
                if (nc == pc)
                {
                    if (_claims.Remove(st.StringId)) Log.Info(Tag + "dzien " + (hour / 24) + " | " + Nm(st) + " [" + st.StringId + "] znow u rodu gracza (" + detail + ") - roszczenie spelnione | roszczen " + _claims.Count + ".");
                    return;
                }
                if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                if (oc == pc)
                {
                    c = new Claim { Taker = nc != null ? nc.StringId : "-", Lost = hour };
                    _claims[st.StringId] = c;
                    Log.Info(Tag + "dzien " + (hour / 24) + " | " + Nm(st) + " [" + st.StringId + "] ZDOBYTA na rodzie gracza przez " + Nm(nc) + " (" + Nm(newOwner.MapFaction) + ")"
                             + (capturer != null ? ", zdobyl " + capturer.Name : "") + " | ROSZCZENIE gracza zapisane - zostaje u zdobywcy (bez zwrotu panu startowemu), odbicie przez krolestwo gracza = osada rodu gracza | roszczen " + _claims.Count + ".");
                    try { Log.Player(Nm(st) + " has fallen to " + Nm(newOwner.MapFaction) + ". It is still yours by right - take it back.", true); } catch { }
                    return;
                }
                if (!_claims.TryGetValue(st.StringId, out c)) return;
                var mf = newOwner != null ? newOwner.MapFaction : null;
                if (mf != null && mf == pc.MapFaction)
                {
                    c.Pending = hour;
                    Log.Info(Tag + "dzien " + (hour / 24) + " | " + Nm(st) + " [" + st.StringId + "] ODBITA przez krolestwo gracza (" + Nm(mf) + ", nowy pan " + Nm(nc) + (capturer != null ? ", zdobyl " + capturer.Name : "")
                             + ") - przekazanie rodowi gracza po nadaniu lenna (decyzja krolestwa rozstrzygnieta).");
                }
                else
                {
                    c.Taker = nc != null ? nc.StringId : "-"; c.Pending = -1;
                    Log.Info(Tag + "dzien " + (hour / 24) + " | " + Nm(st) + " [" + st.StringId + "] zdobyta przez " + Nm(nc) + " (" + Nm(mf) + ") - roszczenie gracza trwa, nowy zdobywca.");
                }
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>HourlyTickEvent. Nie rzuca.</summary>
        internal static void Hourly()
        {
            if (_fixDone && _claims.Count == 0) return;
            try
            {
                var camp = Campaign.Current;
                if (camp == null || !camp.GameStarted) return;
                if (!_fixDone) TryFix();
                bool any = false;
                foreach (var kv in _claims) if (kv.Value.Pending >= 0) { any = true; break; }
                if (!any) return;
                foreach (var id in _claims.Keys.ToList())
                {
                    Claim c;
                    if (!_claims.TryGetValue(id, out c) || c.Pending < 0) continue;
                    try { Handover(id, c); }
                    catch (Exception e)
                    {
                        Stumble("Handover(" + id + ")", e);
                        int n; _err.TryGetValue(id, out n); _err[id] = ++n;
                        if (n >= MaxErrors) { c.Pending = -1; _err.Remove(id); Log.Info(Tag + id + " - " + n + " wyjatki przy przekazaniu, odbicie porzucone (roszczenie zostaje; blad w logu wyzej)."); }
                    }
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void Wait(string id, string why)
        {
            string prev;
            if (_waitSaid.TryGetValue(id, out prev) && prev == why) return;
            _waitSaid[id] = why;
            var st = Settlement.Find(id);
            Log.Info(Tag + "dzien " + (NowHour / 24) + " | " + (st != null ? Nm(st) : id) + " [" + id + "] odbita - przekazanie rodowi gracza czeka: " + why + ".");
        }

        /// <summary>Odbita przez krolestwo gracza - do rodu gracza, gdy lenno przydzielone i spokoj. Rzuca (wolajacy liczy potkniecia per osada).</summary>
        private static void Handover(string id, Claim c)
        {
            var st = Settlement.Find(id);
            var pc = Clan.PlayerClan;
            if (st == null || st.Town == null) { _claims.Remove(id); Log.Info(Tag + id + " - osady nie ma, roszczenie usuniete."); return; }
            if (st.OwnerClan == pc) { _claims.Remove(id); return; }
            if (pc == null || st.MapFaction == null || st.MapFaction != pc.MapFaction)
            {
                c.Pending = -1; _waitSaid.Remove(id);
                Log.Info(Tag + "dzien " + (NowHour / 24) + " | " + Nm(st) + " [" + id + "] juz nie w krolestwie gracza (" + Nm(st.MapFaction) + ") - odbicie nieaktualne, roszczenie zostaje.");
                return;
            }
            if (st.Town.IsOwnerUnassigned) { Wait(id, "lenno nieprzydzielone"); return; }
            var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
            if (k != null)
                foreach (var d in k.UnresolvedDecisions)
                {
                    var sd = d as SettlementClaimantDecision;
                    if (sd != null && sd.Settlement == st) { Wait(id, "decyzja krolestwa o lennie w toku"); return; }
                }
            if (st.IsUnderSiege || (st.Party != null && st.Party.MapEvent != null)) { Wait(id, "oblezenie albo bitwa przy osadzie"); return; }
            if (st.OwnerClan == null || st.OwnerClan.Leader == null) { Wait(id, "pan bez glowy rodu"); return; }
            var me = pc.Leader;
            if (me == null || !me.IsAlive) { Wait(id, "rod gracza bez zywej glowy"); return; }
            string why;
            if (!BookHoldings.PlayerOnMapFor(out why)) { Wait(id, why); return; }
            var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
            if (mp != null && mp.CurrentSettlement == st) { Wait(id, "gracz w osadzie"); return; }
            string from = Nm(st.OwnerClan);
            int men = st.Town.GarrisonParty != null && st.Town.GarrisonParty.MemberRoster != null ? st.Town.GarrisonParty.MemberRoster.TotalManCount : 0;
            try { ChangeOwnerOfSettlementAction.ApplyByDefault(me, st); }
            catch (Exception e) { Stumble("ApplyByDefault(" + id + ")", e); }
            if (st.OwnerClan != pc) { Wait(id, "zmiana pana nie weszla (pan " + Nm(st.OwnerClan) + ")"); return; }
            _claims.Remove(id); _waitSaid.Remove(id); _err.Remove(id);
            Log.Info(Tag + "dzien " + (NowHour / 24) + " | PRZEKAZANIE " + Nm(st) + " [" + id + "] od " + from + " -> rod gracza " + Nm(pc) + " (odbita dnia " + (c.Pending / 24)
                     + ", roszczenie z dnia " + (c.Lost / 24) + ") | zaloga " + men + " zostaje z osada (to samo krolestwo) | roszczen " + _claims.Count + ".");
            try { Log.Player(Nm(st) + " has been retaken and returns to you by right."); } catch { }
        }

        // ------------------------------------------------------------ jednorazowa naprawa zapisu Jeffa
        private static void FixDone(string why)
        {
            _fixDone = true;
            Log.Info(Tag + "naprawa Ashemark (raz na zapis): " + why + ".");
        }

        private static void TryFix()
        {
            try
            {
                var st = Settlement.Find(FixSite);
                if (st == null || st.Town == null) { FixDone("brak osady " + FixSite + " - nie dotyczy"); return; }
                var oc = st.OwnerClan;
                if (oc == null || oc.StringId != FixFrom) { FixDone("Ashemark u " + Nm(oc) + " (" + (oc != null ? oc.StringId : "-") + "), nie u Marbrandow - nie dotyczy"); return; }
                var pc = Clan.PlayerClan;
                bool veto = NorthConquest.HasVeto(FixSite);
                Hero dj = null;
                bool bk = LandBk.Ready;
                if (bk) { try { var t = LandBk.Title(st); dj = t != null ? LandBk.DeJure(t) : null; } catch (Exception e) { Stumble("Fix.title", e); } }
                bool titleMine = dj != null && pc != null && dj.Clan == pc;
                bool sameKingdom = pc != null && pc.Kingdom != null && oc.Kingdom == pc.Kingdom;
                // wyznacznik: slad zwrotu 326b (weto Polnocy na Ashemark) + tytul BK de iure w rodzie gracza (kupiony w 367b); bez BK - Marbrand w krolestwie gracza
                bool ok = veto && (titleMine || (!bk && sameKingdom));
                if (!ok)
                {
                    FixDone("warunki niespelnione - weto 326b " + (veto ? "TAK" : "NIE") + ", tytul BK de iure " + (bk ? (dj != null ? dj.Name + (titleMine ? " (rod gracza)" : "") : "-") : "BK brak")
                            + ", Marbrand w krolestwie gracza " + (sameKingdom ? "TAK" : "NIE") + " - bez zmian");
                    return;
                }
                Kingdom north = null;
                foreach (var kk in Kingdom.All) if (kk != null && kk.StringId == NorthId) { north = kk; break; }
                if (north == null || north.IsEliminated) { FixDone("Polnocy brak - Ashemark zostaje u Marbrandow"); return; }
                Clan to = null;
                foreach (var cl in Clan.All) if (cl != null && cl.StringId == FixTo) { to = cl; break; }
                string toWhy = "Mormont (rada Polnocy nadala im Ashemark 109746)";
                if (to == null || to.IsEliminated || to.Kingdom != north || to == pc || to.Leader == null || !to.Leader.IsAlive)
                {
                    to = north.RulingClan; toWhy = "rod krola Polnocy (Mormontow brak albo nie w Polnocy)";
                }
                if (to == null || to.IsEliminated || to == pc || to.Leader == null || !to.Leader.IsAlive) { FixDone("brak odbiorcy w Polnocy - Ashemark zostaje u Marbrandow"); return; }
                string why;
                if (!BookHoldings.PlayerOnMapFor(out why)) { FixWait(why); return; }
                var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                if (mp != null && mp.CurrentSettlement != null && (mp.CurrentSettlement == st || (mp.CurrentSettlement.IsVillage && mp.CurrentSettlement.Village != null && mp.CurrentSettlement.Village.Bound == st)))
                { FixWait("gracz w Ashemark albo jego wsi"); return; }
                int day = NowHour / 24;
                string wait;
                string res = BookHoldings.TransferFor(Tag, FixSite, null, to, day, out wait, false, 80f, noFill: true);   // jak 326b: bez obsady (326x)
                if (res == null)
                {
                    _fixTries++;
                    if (_fixTries >= FixMaxHours) { FixDone("porzucona po " + _fixTries + " probach (ostatnio: " + wait + ")"); return; }
                    FixWait(wait);
                    return;
                }
                if (!res.StartsWith("ok", StringComparison.Ordinal)) { FixDone("przeniesienie bez skutku: " + res + " (linia wyzej)"); return; }
                string vt = NorthConquest.DropVeto(FixSite);
                _claims[FixSite] = new Claim { Taker = to.StringId, Lost = NowHour };
                FixDone("WYKONANA - Ashemark od Marbrandow -> " + Nm(to) + " (" + to.StringId + ", " + toWhy + ") | " + vt + " | ROSZCZENIE gracza dopisane (roszczen " + _claims.Count + ")");
                try { Log.Player("Ashemark has been restored to " + Nm(north) + " (House " + Nm(to) + "), which took it from you by siege. It is still yours by right - take it back.", true); } catch { }
            }
            catch (Exception e) { Stumble("TryFix", e); _fixTries++; if (_fixTries >= FixMaxHours) FixDone("porzucona po wyjatkach (log wyzej)"); }
        }

        private static void FixWait(string why)
        {
            int day = NowHour / 24;
            if (day == _fixSayDay) return;
            _fixSayDay = day;
            Log.Info(Tag + "naprawa Ashemark czeka - dzien " + day + " | " + why + " (proba co godzine).");
        }
    }

    /// <summary>369: roszczenia gracza (zapis arm_k369), zdobycia i odbicia twierdz, jednorazowa naprawa Ashemark.</summary>
    internal sealed class PlayerClaims369Behavior : CampaignBehaviorBase
    {
        public PlayerClaims369Behavior() { PlayerClaims369.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => PlayerClaims369.SessionStart());
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, PlayerClaims369.Hourly);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, PlayerClaims369.OnOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? PlayerClaims369.Export() : null;
                SaveText.Sync(dataStore, "arm_k369", ref v);
                if (dataStore.IsLoading) PlayerClaims369.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_k369", e); }
        }
    }
}
