using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 330 - KAPITULACJA (projekt B.2 z poprawka G). L = przegrywajacy, W = zwyciezca; liczniki w zapisie, rosna co dobe wojny, warunek pekniety - 0; jeden prog wystarcza:
    ///  K1 L nie trzyma swojej siedziby (Winterfell town_B2 / King's Landing town_EW4), ma &lt;= max(WarSurrenderStrongholds, 1/3 twierdz z dnia startu licznikow)
    ///     i sila L/W &lt; WarSurrenderLoseRatio - WarSurrenderHoldDays dob;
    ///  K2 wladca L w niewoli W i sila L/W &lt; WarSurrenderLoseRatio - WarSurrenderCaptiveDays dob (zmiana / smierc wladcy - licznik od zera);
    ///  K3 TYLKO Polnoc (poprawka G: korona prowadzi wojne az do zwyciestwa): sila Polnoc/korona &lt; WarSurrenderRatio - WarSurrenderRatioDays dob;
    ///  K4 L bez twierdz - gra niszczy krolestwo (koniec wojny, nie pokoj) - tylko linia.
    /// SILA jak 274: suma PartyBase.EstimatedStrength druzyn lordow krolestwa i jego sojusznikow gry, ktore sa w wojnie z druga strona (z druzyna gracza).
    /// Obie strony z progiem tej samej doby - kapituluje ta z nizszym stosunkiem. F2 - odlozona (linia). Gracz wladca strony - bez akcji.
    /// POLNOC: pokoj ApplyByKingdomDecision z danina z modelu gry przez WarSurrenderTributeDays; twierdze Polnocy spoza ziem Polnocy (kultura nie battania i pan
    /// z danych ROT spoza Polnocy; ziemia Tridentu po unii 272 zostaje) - do korony droga 326b (BookHoldings.TransferFor bez obsady): dawny pan z korony, gdy zyje
    /// i jest w koronie, inaczej rod panujacy korony; gracz w osadzie / oblezenie - cesja czeka (ponowienie co dobe, najwyzej 30 dob). Rody do korony - klucz
    /// WarSurrenderClansToCrown (domyslnie NIE; TAK - okno wyboru gracza). KORONA ("pokoj Krola Polnocy"): kazdy zatrzymuje swoje, korona placi danine; bez niszczenia.
    /// Jency wracaja droga PeaceHold (M6a-3) - bez zmian. Potem zatrzask Z (WarTruce.cs).
    /// </summary>
    internal static partial class WarTruce
    {
        private const int CessionMaxDays = 30;

        // ---------------------------------------------------------------- czyste reguly (autotest)

        /// <summary>Licznik doby: warunek - +1, inaczej 0.</summary>
        internal static int Step(int prev, bool cond) { return cond ? Math.Max(0, prev) + 1 : 0; }

        /// <summary>K1: bez siedziby, malo twierdz (&lt;= max(strongholds, forts0/3)) i sila &lt; lose.</summary>
        internal static bool K1Cond(bool holdsSeat, int forts, int forts0, int strongholds, double ratio, double lose)
        {
            int lim = Math.Max(strongholds, forts0 > 0 ? forts0 / 3 : 0);
            return !holdsSeat && forts <= lim && ratio < lose;
        }

        /// <summary>Prog strony: K1 / K2 / K3 (K3 tylko Polnoc - poprawka G). kind - "K1 a/42" itd.; null - brak progu.</summary>
        internal static string DueKind(int k1, int k2, int k3, bool north, int hold, int captive, int ratioDays)
        {
            if (k1 >= hold) return "K1 " + k1 + "/" + hold;
            if (k2 >= captive) return "K2 " + k2 + "/" + captive;
            if (north && k3 >= ratioDays) return "K3 " + k3 + "/" + ratioDays;
            return null;
        }

        /// <summary>Kto kapituluje przy progach obu stron: nizszy stosunek sil (remis - Polnoc). 1 Polnoc, 2 korona, 0 nikt.</summary>
        internal static int WhoSurrenders(bool dueN, bool dueC, double rN, double rC)
        {
            if (dueN && dueC) return rN <= rC ? 1 : 2;
            return dueN ? 1 : dueC ? 2 : 0;
        }

        // ---------------------------------------------------------------- pomiar i liczniki

        private static bool SideOf(IFaction f, Kingdom k, Kingdom enemy)
        {
            if (f == null) return false;
            if (f == k) return true;
            var o = f as Kingdom;
            if (o == null || o == enemy || o.IsEliminated) return false;
            try { return o.IsAllyWith(k) && o.IsAtWarWith(enemy); } catch { return false; }
        }

        private static double Strength(Kingdom k, Kingdom enemy)
        {
            double s = 0;
            foreach (var mp in MobileParty.AllLordParties)
            {
                if (mp == null || !mp.IsActive || mp.Party == null) continue;
                if (SideOf(mp.MapFaction, k, enemy)) s += mp.Party.EstimatedStrength;
            }
            var main = MobileParty.MainParty;
            if (main != null && main.IsActive && main.Party != null && !main.IsLordParty && SideOf(main.MapFaction, k, enemy)) s += main.Party.EstimatedStrength;
            return s;
        }

        private static int Forts(Kingdom k)
        {
            int n = 0;
            foreach (var st in k.Settlements) if (st != null && st.IsFortification && st.Town != null) n++;
            return n;
        }

        private static bool HoldsSeat(string id, Kingdom k)
        {
            var st = Settlement.Find(id);
            return st == null || st.MapFaction == k;   // brak osady w swiecie - K1 nie liczy sie
        }

        private static bool LeaderHeldBy(Kingdom k, Kingdom captor)
        {
            var h = k.Leader;
            if (h == null || !h.IsPrisoner) return false;
            var p = h.PartyBelongedToAsPrisoner;
            return p != null && p.MapFaction == captor;
        }

        /// <summary>Pomiar doby (sila, twierdze, zmeczenie) - linia doby i liczniki. Nie rzuca.</summary>
        private static void Measure(Kingdom n, Kingdom c)
        {
            try
            {
                _lastSN = Strength(n, c); _lastSC = Strength(c, n);
                _lastRN = _lastSC > 0 ? _lastSN / _lastSC : 10.0;
                _lastRC = _lastSN > 0 ? _lastSC / _lastSN : 10.0;
                _lastFN = Forts(n); _lastFC = Forts(c);
                _lastExhN = Exh(n, c); _lastExhC = Exh(c, n);
            }
            catch (Exception e) { Stumble("Measure", e); }
        }

        /// <summary>Doba wojny: liczniki K1-K3 (K3 tylko Polnoc). K4 - linia.</summary>
        private static void StepCounters(Kingdom n, Kingdom c)
        {
            if (_lastSN < 0 || _lastSC < 0) return;
            if (_fortsN0 < 0) _fortsN0 = _lastFN;
            if (_fortsC0 < 0) _fortsC0 = _lastFC;
            string ln = n.Leader != null ? n.Leader.StringId ?? "" : "", lc = c.Leader != null ? c.Leader.StringId ?? "" : "";
            if (ln != _leadN) { if (_leadN.Length > 0 && _k[1] > 0) Info("wladca Polnocy zmieniony - licznik K2 od zera (" + _k[1] + ")."); _k[1] = 0; _leadN = ln; }
            if (lc != _leadC) { if (_leadC.Length > 0 && _k[4] > 0) Info("wladca korony zmieniony - licznik K2 od zera (" + _k[4] + ")."); _k[4] = 0; _leadC = lc; }
            double lose = LoseRatio;
            _k[0] = Step(_k[0], K1Cond(HoldsSeat(WinterfellId, n), _lastFN, _fortsN0, Strongholds, _lastRN, lose));
            _k[1] = Step(_k[1], LeaderHeldBy(n, c) && _lastRN < lose);
            _k[2] = Step(_k[2], _lastRN < LowRatio);
            _k[3] = Step(_k[3], K1Cond(HoldsSeat(KingsLandingId, c), _lastFC, _fortsC0, Strongholds, _lastRC, lose));
            _k[4] = Step(_k[4], LeaderHeldBy(c, n) && _lastRC < lose);
            _k[5] = 0;   // poprawka G: korona nie kapituluje z samego stosunku sil
            if (_lastFN == 0) InfoOnce("k4n", "K4: Polnoc bez miast i zamkow - gra konczy krolestwo (koniec wojny, nie pokoj).");
            if (_lastFC == 0) InfoOnce("k4c", "K4: korona bez miast i zamkow - gra konczy krolestwo (koniec wojny, nie pokoj).");
        }

        /// <summary>Prog kapitulacji ktorejs strony teraz (liczniki); loser - kto kapituluje, kind - "Kn a/b".</summary>
        internal static bool SurrenderDue(Kingdom n, Kingdom c, out Kingdom loser, out string kind)
        {
            loser = null; kind = null;
            string kn = DueKind(_k[0], _k[1], _k[2], true, HoldDays, CaptiveDays, RatioDays);
            string kc = DueKind(_k[3], _k[4], 0, false, HoldDays, CaptiveDays, RatioDays);
            int who = WhoSurrenders(kn != null, kc != null, _lastRN < 0 ? 1.0 : _lastRN, _lastRC < 0 ? 1.0 : _lastRC);
            if (who == 1) { loser = n; kind = kn; return true; }
            if (who == 2) { loser = c; kind = kc; return true; }
            return false;
        }

        // ---------------------------------------------------------------- kapitulacja

        private static int CountCaptives(Kingdom a, Kingdom b)
        {
            int m = 0;
            try
            {
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || !h.IsPrisoner || h.PartyBelongedToAsPrisoner == null) continue;
                    var cap = h.PartyBelongedToAsPrisoner.MapFaction;
                    if ((h.MapFaction == a && cap == b) || (h.MapFaction == b && cap == a)) m++;
                }
            }
            catch (Exception e) { Stumble("CountCaptives", e); }
            return m;
        }

        /// <summary>Kapitulacja L przed W (w wojnie albo z rozejmu). true - weszla. ph - faza (F2 i gracz - bez akcji, wolajacy pilnuje).</summary>
        internal static bool Surrender(Kingdom loser, Kingdom winner, string kind, Kingdom n, Kingdom c, int ph)
        {
            try
            {
                if (loser == null || winner == null) return false;
                if (PlayerRules(n, c)) { InfoOnce("kapgracz", "prog kapitulacji (" + kind + ") - gracz rzadzi strona, bez akcji."); return false; }
                if (ph == 1 || ph == 2) { InfoOnce("kapf2", "kapitulacja odlozona (F2) - " + (loser == n ? "Polnoc" : "korona") + " " + kind + "."); return false; }
                bool north = loser == n;
                int daily = ModelTribute(loser, winner), days = TributeDays;
                if (loser.IsAtWarWith(winner))
                {
                    _ourPeace = true;
                    try { MakePeaceAction.ApplyByKingdomDecision(loser, winner, daily, daily > 0 && days > 0 ? days : 0); }
                    finally { _ourPeace = false; }
                    if (loser.IsAtWarWith(winner)) { _stumbles++; _dStumbles++; Info("KAPITULACJA " + (north ? "Polnocy" : "korony") + " (" + kind + ") NIE weszla - pokoj gry odrzucony; wojna dalej."); return false; }
                }
                else
                {
                    var link = loser.GetStanceWith(winner);
                    if (link != null) link.SetDailyTributePaid(loser, daily, daily > 0 && days > 0 ? days : 0);   // z rozejmu: danina kapitulacji zamiast daniny rozejmu
                }
                int d = Today;
                ClearTruceState();
                int captives = CountCaptives(n, c);
                string ces = north ? Cede(n, c, d) : "bez cesji (kazdy zatrzymuje swoje)";
                // zatrzask Z: do nastepnego wydarzenia kanonu pary
                _z = true; _zDay = d;
                _zWhy = "KAPITULACJA " + (north ? "Polnocy" : "korony") + " (" + (kind ?? "?") + ")";
                _zRw = Prologue.RotEventState("RedWeddingEvent"); _zJon = Prologue.RotEventState("JonEvent");
                _zRwDone = KingInTheNorth.RwHappened || _zRw >= 2; _zRev = RevengeReached();
                double ratio = north ? _lastRN : _lastRC;
                for (int i = 0; i < _k.Length; i++) _k[i] = 0;
                _fortsN0 = _fortsC0 = -1;
                Info("KAPITULACJA " + (north ? "Polnocy" : "korony") + " (" + kind + ", sila " + (ratio < 0 ? "?" : ratio.ToString("0.00", Inv)) + "): " + ces
                     + ", danina " + TributeText(loser, winner, daily, days) + ", jency " + captives + " (wracaja wg PeaceHold) | zatrzask Z do: "
                     + (_zRwDone ? "" : "Krwawe Gody, ") + (_zJon >= 1 ? "" : "JonEvent, ") + (_zRev ? "" : "Revenge (d. opowiesci " + (RevengeRotDay * Prologue.StoryScale()).ToString("0", Inv) + "), ")
                     + "Bekarci = koniec 330.");
                try
                {
                    Log.Player(north ? Name(n) + " has surrendered to " + Name(c) + ": it gives up its lands outside the North and pays tribute. The peace holds until the story turns again."
                                     : Name(c) + " has made peace with the King in the North: each side keeps what it holds and the crown pays tribute.", false);
                }
                catch { }
                if (CanonPeaceLock.BastardsState330 >= 2) { _final = true; _z = false; Info("KONIEC 330 (Bekarci + kapitulacja) - pokoj staly, para wypada z regul 330."); }
                if (north && ClansKey) AskClans(n, c);
                return true;
            }
            catch (Exception e) { Stumble("Surrender", e); return false; }
        }

        /// <summary>Twierdza do cesji: twierdza Polnocy, kultura nie battania, pan z danych ROT spoza Polnocy (ziemia Tridentu po unii zostaje); Red Keep idzie z King's Landing.</summary>
        private static bool CedeCandidate(Settlement st, Kingdom n, out string origClan)
        {
            origClan = null;
            if (st == null || !st.IsFortification || st.Town == null || st.MapFaction != n || st.StringId == RedKeepId) return false;
            if (st.Culture != null && st.Culture.StringId == NorthId) return false;
            string ok;
            origClan = KingInTheNorth.OrigOwnerOf(st.StringId, out ok);
            if (ok == NorthId) return false;
            if (ok == RiverId && KingInTheNorth.Done) return false;   // unia 272: ziemia Tridentu jest Polnoca
            if (ok == null && KingInTheNorth.TridentLand(st)) return false;
            return true;
        }

        private static Clan Recipient(string origClan, Kingdom c)
        {
            if (!string.IsNullOrEmpty(origClan))
            {
                var cl = Clan.FindFirst(x => x != null && x.StringId == origClan);
                if (cl != null && !cl.IsEliminated && cl.Kingdom == c && cl != Clan.PlayerClan && cl.Leader != null && cl.Leader.IsAlive) return cl;
            }
            return c.RulingClan;
        }

        /// <summary>Cesja twierdz Polnocy do korony; zwraca opis "cesja n [..]" (czekajace w _cession).</summary>
        private static string Cede(Kingdom n, Kingdom c, int day)
        {
            var list = new List<Settlement>();
            var orig = new Dictionary<Settlement, string>();
            foreach (var st in Settlement.All)
            {
                string oc;
                if (CedeCandidate(st, n, out oc)) { list.Add(st); orig[st] = oc; }
            }
            var done = new List<string>();
            int wait = 0;
            foreach (var st in list)
            {
                var to = Recipient(orig[st], c);
                if (to == null) continue;
                string res = TryCede(st, to, day);
                if (res == null) { _cession.Add(st.StringId + ">" + to.StringId + ">" + day.ToString(Inv)); wait++; }
                else done.Add(st.Name + "->" + to.Name + " (" + res + ")");
            }
            return "cesja " + list.Count + " [" + string.Join(", ", done.ToArray()) + "]" + (wait > 0 ? ", czeka " + wait : "");
        }

        /// <summary>Jedna twierdza droga 326b; null - czeka (gracz nie na mapie, gracz w osadzie, oblezenie, warunek chwilowy BookHoldings).</summary>
        private static string TryCede(Settlement st, Clan to, int day)
        {
            try
            {
                string why;
                if (!BookHoldings.PlayerOnMapFor(out why)) return null;
                var main = MobileParty.MainParty;
                if (main != null && main.CurrentSettlement != null)
                {
                    var cs = main.CurrentSettlement;
                    var b = cs.IsVillage && cs.Village != null ? cs.Village.Bound : cs;
                    if (b == st || (st.StringId == KingsLandingId && b != null && b.StringId == RedKeepId)) return null;
                }
                if (st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null)) return null;
                string w;
                return BookHoldings.TransferFor(Tag, st.StringId, st.StringId == KingsLandingId ? RedKeepId : null, to, day, out w, false, 0f, true);
            }
            catch (Exception e) { Stumble("TryCede", e); return null; }
        }

        /// <summary>Doba: cesje czekajace (najwyzej CessionMaxDays od kapitulacji; twierdza juz nie Polnocy - wypada).</summary>
        private static void CessionDaily(Kingdom n, Kingdom c)
        {
            if (_cession.Count == 0) return;
            int day = Today;
            for (int i = _cession.Count - 1; i >= 0; i--)
            {
                try
                {
                    var f = _cession[i].Split('>');
                    var st = f.Length >= 3 ? Settlement.Find(f[0]) : null;
                    int since = f.Length >= 3 ? I(f[2], day) : day;
                    if (st == null || st.MapFaction != n) { Info("cesja " + (st != null ? st.Name.ToString() : f[0]) + " - juz nie Polnocy, wypada z kolejki."); _cession.RemoveAt(i); continue; }
                    if (day - since > CessionMaxDays) { Info("cesja " + st.Name + " - " + CessionMaxDays + " dob bez warunkow, porzucona."); _cession.RemoveAt(i); continue; }
                    var to = Clan.FindFirst(x => x != null && x.StringId == f[1]);
                    if (to == null || to.IsEliminated || to.Kingdom != c) to = c.RulingClan;
                    string res = TryCede(st, to, day);
                    if (res != null) { Info("cesja " + st.Name + " -> " + to.Name + " (" + res + ")."); _cession.RemoveAt(i); }
                }
                catch (Exception e) { Stumble("CessionDaily", e); _cession.RemoveAt(i); }
            }
        }

        /// <summary>Klucz WarSurrenderClansToCrown: okno gracza - rody Polnocy spoza Polnocy (kultura nie battania, nie rod panujacy, nie gracz, nie najemnicy) do korony.</summary>
        private static void AskClans(Kingdom n, Kingdom c)
        {
            var clans = new List<Clan>();
            foreach (var cl in n.Clans)
                if (cl != null && !cl.IsEliminated && cl != n.RulingClan && cl != Clan.PlayerClan && !cl.IsUnderMercenaryService && cl.Culture != null && cl.Culture.StringId != NorthId) clans.Add(cl);
            if (clans.Count == 0) { Info("rody do korony: brak rodow spoza Polnocy."); return; }
            var names = new List<string>();
            foreach (var cl in clans) names.Add(cl.Name.ToString());
            InformationManager.ShowInquiry(new InquiryData("The North has surrendered",
                "Should the lords of the North from outside the northern lands (" + string.Join(", ", names.ToArray()) + ") swear fealty to the crown?", true, true, "Yes", "No",
                delegate
                {
                    try
                    {
                        int moved = 0;
                        foreach (var cl in clans)
                            if (!cl.IsEliminated && cl.Kingdom == n && !c.IsEliminated) { ChangeKingdomAction.ApplyByJoinToKingdom(cl, c, default(CampaignTime), true); moved++; }
                        Info("rody do korony (wybor gracza TAK): przeszlo " + moved + " z " + clans.Count + ".");
                    }
                    catch (Exception e) { Stumble("AskClans.Yes", e); }
                },
                delegate { Info("rody do korony (wybor gracza NIE): zostaja w Polnocy."); }), true);
        }
    }
}
